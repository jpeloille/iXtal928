// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/sound/sound.c
// STATUS: partial — le mixeur et son chronomètre à 48 kHz : sound_add_handler,
//         sound_update_buf_length, sound_poll, sound_speed_changed, sound_reset,
//         sound_init ; G8.1 : le registre SOUND_CARD réduit (sound.c:31-105). G10.5 : le fil CD
//         (sound.c:120-124, 143-197), son corps appelé à l'échéance de sound_poll (DEVIATION, plus bas).

// CS8602 : `sound_handlers[c].get_buffer` est un pointeur de fonction que le C
// laisse à NULL jusqu'à sound_add_handler (sound.c:212). Les entrées 0 à
// sound_handlers_num-1 sont posées, et c'est cette borne — celle de PCem — qui
// garde la boucle de sound_poll (sound.c:234). Un `!` par site réécrirait la
// ligne, comme dans device.cs.
#pragma warning disable CS8602

using static iXtal26.timer;

namespace iXtal26.Sound;

// pcem: sound.c:109 — void (*get_buffer)(int32_t *buffer, int len, void *p)
internal delegate void sound_get_buffer_t(int32_t[] buffer, int len, object? p);

// pcem: soundopenal.c:143 — void givealbuffer(int32_t *buf). C'est la frontière
// hôte : l'étage OpenAL de PCem devient Host/SdlAudio.cs, comme l'enveloppe
// wxWidgets est devenue Host/SdlMenu.cs. Non branché, sound_poll tourne et ne
// sort rien — c'est --headless, --boot et le diff d'amorçage.
internal delegate void sound_give_buffer_t(int32_t[] buf);

// pcem: soundopenal.c — void givealbuffer_cd(int16_t *buf), la voie hôte du CD (G10.5) : CD_BUFLEN * 2
//   échantillons stéréo à 44,1 kHz. Branchée par Host/SdlAudio.cs.
internal delegate void sound_give_cd_buffer_t(int16_t[] buf);

internal static partial class sound
{
    // pcem: sound.h:40
    internal const int MAXSOUNDBUFLEN = 48000 / 10;

    // pcem: sound.c:108-111
    internal struct sound_handler_t
    {
        internal sound_get_buffer_t? get_buffer;
        internal object? priv;
    }

    internal static sound_handler_t[] sound_handlers = new sound_handler_t[8];

    // pcem: sound.c:113
    internal static int sound_handlers_num;

    // pcem: sound.c:115-117
    internal static pc_timer_t sound_poll_timer = new();
    internal static uint64_t sound_poll_latch;
    internal static int sound_pos_global = 0;

    // pcem: sound.c:119
    internal static int soundon = 1;

    // pcem: sound.c:126-127. DEVIATION: sound.c pose 48000/10 = 4800, ce qui
    //   rendrait `1000 / sound_buf_len` NUL à sound.c:130 et
    //   sound_update_buf_length() une division par zéro. La valeur vivante est
    //   celle de pc.c:635, en MILLISECONDES : 200. Elle ne survit chez PCem que
    //   parce que le tiers CFG_GLOBAL, écarté ici (registre des omissions),
    //   l'écrase avant le premier appel. Même valeur dans harness.c : elle
    //   gouverne sound_buf_len_al, donc l'instant où sound_pos_global reboucle,
    //   donc speaker_pos — deux valeurs différentes feraient diverger les
    //   échantillons sans qu'aucune instruction ne diffère.
    internal static int sound_buf_len = 200;
    internal static int sound_gain = 0;

    // pcem: soundopenal.c:27
    internal static int sound_buf_len_al = 48000 / 20;

    // pcem: sound.c:199. DEVIATION: alloué ici et non dans sound_init()
    //   (sound.c:205), qui appartient à l'IHM chez PCem (wx-sdl2.c:470).
    //   --headless, --boot, --slices, --timer-check et BootDiff passent par
    //   pc.initpc() sans jamais monter d'hôte : sound_poll écrirait dans un
    //   tableau nul au premier tampon plein, soit ~50 ms émulées après le reset.
    internal static int32_t[] outbuffer = new int32_t[MAXSOUNDBUFLEN * 2];

    // Frontière hôte. Voir sound_give_buffer_t.
    internal static sound_give_buffer_t? sound_give_buffer_func;

    // Empreinte FNV-1a cumulative du son RÉELLEMENT produit, prise là où PCem
    // appelle givealbuffer — donc après le passage des handlers. C'est le
    // neuvième champ de sound_speaker.Probe(), et le pendant de h_sound_hash
    // (harness.c). speaker_buffer seul ne dirait rien de ce passage.
    internal static uint64_t sound_hash;

    // pcem: sound.c:129-136
    internal static void sound_update_buf_length()
    {
        int new_buf_len = (48000 / (1000 / sound_buf_len)) / 4;

        if (new_buf_len > MAXSOUNDBUFLEN)
                new_buf_len = MAXSOUNDBUFLEN;

        sound_buf_len_al = new_buf_len;
    }

    // pcem: sound.c:201-209
    internal static void sound_init()
    {
        // omitted: initalmain(0, NULL) et inital() (sound.c:202-203) — l'étage
        //   OpenAL, remplacé par Host/SdlAudio.cs.
        // omitted: malloc(outbuffer) (sound.c:205) — hissé au champ, voir plus haut.
        // omitted: sound_cd_event / sound_cd_thread (sound.c:207-208) — le fil lui-même : son corps est
        //   sound_cd_tick, appelé par sound_poll (G10.5).
    }

    // pcem: sound.c:211-215
    internal static void sound_add_handler(sound_get_buffer_t get_buffer, object? p)
    {
        sound_handlers[sound_handlers_num].get_buffer = get_buffer;
        sound_handlers[sound_handlers_num].priv = p;
        sound_handlers_num++;
    }

    private static void sound_mix_hash()
    {
        int c;
        for (c = 0; c < sound_buf_len_al * 2; c++)
        {
                sound_hash ^= (uint64_t)(uint32_t)outbuffer[c];
                sound_hash *= 1099511628211UL;
        }
    }

    // pcem: sound.c:218-256
    internal static void sound_poll(object? priv)
    {
        timer_advance_u64(sound_poll_timer, sound_poll_latch);

        // pcem: sound.c:221-225. DEVIATION (décision n° 7 de PLAN-G10) : thread_set_event(sound_cd_event)
        //   réveille chez PCem un fil, dont la cadence dépend de l'ordonnanceur de l'hôte ; ici le corps du
        //   fil s'exécute sur-le-champ, à l'échéance, des deux côtés (harness.c, h_sound_cd_tick).
        cd_pos++;
        if (cd_pos == (CD_BUFLEN * 48000) / CD_FREQ)
        {
                cd_pos = 0;
                sound_cd_tick();
        }

        sound_pos_global++;
        if (sound_pos_global == sound_buf_len_al)
        {
                int c;

                Array.Clear(outbuffer, 0, sound_buf_len_al * 2);

                for (c = 0; c < sound_handlers_num; c++)
                        sound_handlers[c].get_buffer(outbuffer, sound_buf_len_al, sound_handlers[c].priv);

                sound_mix_hash();

                if (soundon != 0)
                        sound_give_buffer_func?.Invoke(outbuffer);

                sound_pos_global = 0;
                sound_update_buf_length();
        }
    }

    // pcem: sound.c:258
    internal static void sound_speed_changed()
    {
        sound_poll_latch = (uint64_t)((double)TIMER_USEC * (1000000.0 / 48000.0));
    }

    // pcem: sound.c:260-268
    internal static void sound_reset()
    {
        sound_hash = 1469598103934665603UL;

        timer_add(sound_poll_timer, sound_poll, null, 1);

        sound_handlers_num = 0;

        sound_set_cd_volume(65535, 65535);
        // omitted: ioctl_audio_stop() (sound.c:266) — le lecteur physique de l'hôte, exclu (PLAN.md).
        Cdrom.cdrom_image.image_audio_stop();
    }

    // pcem: sound.c:36-37, :39-40, :42-44 — G8.1 : le registre SOUND_CARD.
    // DEVIATION: neuf entrées sur vingt, dans l'ordre RELATIF de sound_init_builtin (sound.c:270-291) —
    //   sc_none, sc_adlib, puis (G12.0) les SB 1.0, 1.5, 2.0 et Pro v1, la Pro v2 de G8, la 16 (G12.1) et l'AWE32 (G12.2) —,
    //   en tableau fixe comme video_cards (video.cs). La configuration écrit l'internal_name : aucun indice ne sort de ce
    //   fichier.
    //   Omis : adlib_mca, sbmcv et sbpromcv (MCA), adlibgold et les cartes d'après (PLAN.md, G12).
    internal sealed class SOUND_CARD
    {
        internal string name = "";
        internal string internal_name = "";
        internal PluginApi.device_t? device;
    }

    internal static readonly SOUND_CARD sc_none = new() { name = "None", internal_name = "none", device = null };
    internal static readonly SOUND_CARD sc_adlib = new() { name = "Adlib", internal_name = "adlib", device = sound_adlib.adlib_device };
    // pcem: sound.c:39-40, :42-43 — G12.0.
    internal static readonly SOUND_CARD sc_sb = new() { name = "Sound Blaster 1.0", internal_name = "sb", device = sound_sb.sb_1_device };
    internal static readonly SOUND_CARD sc_sb1_5 = new() { name = "Sound Blaster 1.5", internal_name = "sb1.5", device = sound_sb.sb_15_device };
    internal static readonly SOUND_CARD sc_sb2_0 = new() { name = "Sound Blaster 2.0", internal_name = "sb2.0", device = sound_sb.sb_2_device };
    internal static readonly SOUND_CARD sc_sbprov1 = new() { name = "Sound Blaster Pro v1", internal_name = "sbprov1", device = sound_sb.sb_pro_v1_device };
    // pcem: sound.c:44 — G8.2.
    internal static readonly SOUND_CARD sc_sbprov2 = new() { name = "Sound Blaster Pro v2", internal_name = "sbprov2", device = sound_sb.sb_pro_v2_device };
    // pcem: sound.c:46 — G12.1. Une carte ISA 16 bits (Host/SoundCards.cs, pc.check_sndcard).
    internal static readonly SOUND_CARD sc_sb16 = new() { name = "Sound Blaster 16", internal_name = "sb16", device = sound_sb.sb_16_device };
    // pcem: sound.c:47 — G12.2. Une carte ISA 16 bits, et sa ROM (pc.check_sndcard).
    internal static readonly SOUND_CARD sc_sbawe32 = new() { name = "Sound Blaster AWE32", internal_name = "sbawe32", device = sound_sb.sb_awe32_device };

    internal static readonly SOUND_CARD[] sound_cards = { sc_none, sc_adlib, sc_sb, sc_sb1_5, sc_sb2_0, sc_sbprov1, sc_sbprov2, sc_sb16, sc_sbawe32 };

    // pcem: sound.c:124 — G8.2 : posés par le mélangeur CT1345 ; G10.5 : lus par le fil CD ; G12.0 : lus aussi par
    //   la sonde du son (sound_sb.ProbeSb), le volume CD que la carte pose.
    internal static uint cd_vol_l, cd_vol_r;

    // pcem: sound.c:138-141
    internal static void sound_set_cd_volume(uint vol_l, uint vol_r)
    {
        cd_vol_l = vol_l;
        cd_vol_r = vol_r;
    }

    // pcem: sound.h:18-19
    internal const int CD_FREQ = 44100;
    internal const int CD_BUFLEN = CD_FREQ / 10;

    // pcem: sound.c:121
    private static readonly int16_t[] cd_buffer = new int16_t[CD_BUFLEN * 2];

    // pcem: sound.c:217
    private static int cd_pos = 0;

    // iXtal26 — la voie hôte du CD (givealbuffer_cd), comme sound_give_buffer_func pour givealbuffer.
    internal static sound_give_cd_buffer_t? sound_give_cd_buffer_func;

    // iXtal26 (outillage) — la sonde du CD, pendant de h_cd_snd_hash et h_cd_snd_blocks (harness.c) :
    //   l'empreinte FNV-1a des échantillons que reçoit givealbuffer_cd, et le nombre de blocs.
    internal static uint64_t cd_sound_hash = 1469598103934665603UL;
    internal static uint64_t cd_sound_blocks;
    internal static uint64_t cd_sound_nonzero;

    // pcem: soundopenal.c — givealbuffer_cd, la frontière hôte du CD ; l'empreinte prise avant elle.
    private static void givealbuffer_cd(int16_t[] buf)
    {
        int c;
        for (c = 0; c < CD_BUFLEN * 2; c++)
        {
                cd_sound_hash ^= (uint64_t)(uint16_t)buf[c];
                cd_sound_hash *= 1099511628211UL;
                if (buf[c] != 0)
                        cd_sound_nonzero++;
        }
        cd_sound_blocks++;
        sound_give_cd_buffer_func?.Invoke(buf);
    }

    // pcem: sound.c:143-197 — la boucle du fil CD sans son attente (thread_wait_event, thread_reset_event).
    private static void sound_cd_tick()
    {
        int c;

        Array.Clear(cd_buffer);
        // omitted: ioctl_audio_callback(cd_buffer, CD_BUFLEN * 2) (sound.c:150) — le lecteur physique, exclu.
        Cdrom.cdrom_image.image_audio_callback(cd_buffer, CD_BUFLEN * 2);
        if (soundon != 0)
        {
                int32_t atapi_vol_l = (int32_t)Scsi.scsi_cd_c.atapi_get_cd_volume(0);
                int32_t atapi_vol_r = (int32_t)Scsi.scsi_cd_c.atapi_get_cd_volume(1);
                Span<int> channel_select = stackalloc int[2];

                channel_select[0] = (int)Scsi.scsi_cd_c.atapi_get_cd_channel(0);
                channel_select[1] = (int)Scsi.scsi_cd_c.atapi_get_cd_channel(1);

                for (c = 0; c < CD_BUFLEN * 2; c += 2)
                {
                        int32_t cd_buffer_temp0 = 0, cd_buffer_temp1 = 0;

                        /*First, adjust input from drive according to ATAPI volume.*/
                        cd_buffer[c] = (int16_t)(((int32_t)cd_buffer[c] * atapi_vol_l) / 255);
                        cd_buffer[c + 1] = (int16_t)(((int32_t)cd_buffer[c + 1] * atapi_vol_r) / 255);

                        /*Apply ATAPI channel select*/
                        if ((channel_select[0] & 1) != 0)
                                cd_buffer_temp0 += cd_buffer[c];
                        if ((channel_select[0] & 2) != 0)
                                cd_buffer_temp1 += cd_buffer[c];
                        if ((channel_select[1] & 1) != 0)
                                cd_buffer_temp0 += cd_buffer[c + 1];
                        if ((channel_select[1] & 2) != 0)
                                cd_buffer_temp1 += cd_buffer[c + 1];

                        /*Apply sound card CD volume*/
                        cd_buffer_temp0 = (cd_buffer_temp0 * (int)cd_vol_l) / 65535;
                        cd_buffer_temp1 = (cd_buffer_temp1 * (int)cd_vol_r) / 65535;

                        if (cd_buffer_temp0 > 32767)
                                cd_buffer_temp0 = 32767;
                        if (cd_buffer_temp0 < -32768)
                                cd_buffer_temp0 = -32768;
                        if (cd_buffer_temp1 > 32767)
                                cd_buffer_temp1 = 32767;
                        if (cd_buffer_temp1 < -32768)
                                cd_buffer_temp1 = -32768;

                        cd_buffer[c] = (int16_t)cd_buffer_temp0;
                        cd_buffer[c + 1] = (int16_t)cd_buffer_temp1;
                }

                givealbuffer_cd(cd_buffer);
        }
    }

    // iXtal26 (outillage) — ORACLE PARITY, pendant de h_sound_cd_raz (harness.c) : à chaque amorçage, comme
    //   le lancement de PCem, cd_pos et le tampon à zéro, l'empreinte à sa graine. PCem ne remet pas cd_pos
    //   à zéro au reset matériel, seulement au chargement du programme.
    internal static void sound_cd_raz()
    {
        cd_pos = 0;
        Array.Clear(cd_buffer);
        cd_sound_hash = 1469598103934665603UL;
        cd_sound_blocks = 0;
        cd_sound_nonzero = 0;
    }

    // iXtal26 (outillage) — la sonde du CD, pendant de h_cd_sound_probe (harness.c).
    internal static void CdSoundProbe(ulong[] o)
    {
        o[0] = (ulong)cd_pos;
        o[1] = cd_vol_l;
        o[2] = cd_vol_r;
        o[3] = cd_sound_hash;
        o[4] = cd_sound_blocks;
        o[5] = cd_sound_nonzero;
    }

    // pcem: sound.c:33
    internal static int sound_card_current = 0;
    // pcem: sound.c:34
    private static int sound_card_last = 0;

    // pcem: sound.c:90-100 — la boucle s'arrête à la sentinelle NULL : ici, la longueur du tableau.
    internal static int sound_card_get_from_internal_name(string s)
    {
        int c = 0;

        while (c < sound_cards.Length)
        {
                if (sound_cards[c].internal_name == s)
                        return c;
                c++;
        }

        return 0;
    }

    // pcem: sound.c:84-88
    internal static string sound_card_get_internal_name(int card) => sound_cards[card].internal_name;

    // pcem: sound.c:102-106
    internal static void sound_card_init()
    {
        if (sound_cards[sound_card_current].device != null)
                PluginApi.device.device_add(sound_cards[sound_card_current].device!);
        sound_card_last = sound_card_current;
    }
}
