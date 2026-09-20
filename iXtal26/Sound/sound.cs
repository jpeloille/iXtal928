// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/sound/sound.c
// STATUS: partial — le mixeur et son chronomètre à 48 kHz : sound_add_handler,
//         sound_update_buf_length, sound_poll, sound_speed_changed, sound_reset,
//         sound_init. Omis : le fil CD (sound.c:120-124, 143-197) et le registre
//         SOUND_CARD (sound.c:31-105).

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
        // omitted: sound_cd_event / sound_cd_thread (sound.c:207-208) — fil CD.
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

        // omitted: cd_pos et thread_set_event(sound_cd_event) (sound.c:221-225) —
        //   fil CD, hors périmètre 5150.

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

        // omitted: sound_set_cd_volume(), ioctl_audio_stop(), image_audio_stop()
        //   (sound.c:265-267) — CD, hors périmètre.
    }
}
