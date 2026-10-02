// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/sound/sound_sb.c + includes/private/sound/sound_sb.h
//         + includes/private/filters.h:103-140 (sb_iir)
// STATUS: partial — la Sound Blaster Pro v2 seule : sb_t (sa part SBPRO), sb_ct1345_mixer_t,
//         sb_att_4dbstep_3bits, sb_att_7dbstep_2bits, sb_iir, sb_get_buffer_sbpro,
//         sb_ct1345_mixer_write/read/reset, sb_pro_v2_init, sb_close, sb_speed_changed,
//         sb_pro_v2_config, sb_pro_v2_device.
//         Omis : les SB 1.0/1.5/MCV/2.0/Pro v1/Pro MCV/16/AWE32 (leurs get_buffer, init,
//         mélangeurs CT1335 et CT1745, configs et devices), la table sb_bass_treble_4bits et
//         sb_att_2dbstep_5bits (CT1745), sb_awe32_close, sb_add_status_info (texte de l'hôte),
//         le débogage SB_DSP_RECORD_DEBUG (#ifdef éteint).
//
// PERSISTANCE : l'état du filtre sb_iir est `static` LOCAL à une fonction `static inline` de
// filters.h, donc une copie par unité de compilation — celle de sound_sb.c. Rien ne le remet à
// zéro : il traverse les amorçages d'un même processus. Ici, champs statiques de cette classe,
// jamais remis à zéro non plus (PLAN-G8.md, décision n° 4).

// CS8600 : `(sb_t)p` part du `object?` des delegates de device.cs, sound.cs et io.cs, comme
// à sound_adlib.cs. CS8602 : même raison, à la déréférence qui suit.
#pragma warning disable CS8600, CS8602

using iXtal26.PluginApi;
using static iXtal26.io;
using static iXtal26.PluginApi.device;
using static iXtal26.Sound.sound;
using static iXtal26.Sound.sound_opl;
using static iXtal26.Sound.sound_sb_dsp;

namespace iXtal26.Sound;

// pcem: sound_sb.h:28-49 — SB PRO
internal sealed class sb_ct1345_mixer_t
{
    internal int32_t master_l, master_r;
    internal int32_t voice_l, voice_r;
    internal int32_t fm_l, fm_r;
    internal int32_t cd_l, cd_r;
    internal int32_t line_l, line_r;
    internal int32_t mic;
    /*see sb_ct1745_mixer for values for input selector*/
    internal int32_t input_selector;

    internal int input_filter;
    internal int in_filter_freq;
    internal int output_filter;

    internal int stereo;
    internal int stereo_isleft;

    internal uint8_t index;
    internal readonly uint8_t[] regs = new uint8_t[256];
}

// pcem: sound_sb.h:91-107
// omitted: les membres sb_ct1335_mixer_t mixer_sb2 et sb_ct1745_mixer_t mixer_sb16 de l'union
//   (:94-96), mpu401_uart_t mpu (:99) et emu8k_t emu8k (:100) — autres cartes. L'union n'a plus
//   qu'un membre : un champ.
internal sealed class sb_t
{
    internal readonly opl_t opl = new opl_t();
    internal readonly sb_dsp_t dsp = new sb_dsp_t();
    internal readonly sb_ct1345_mixer_t mixer_sbpro = new sb_ct1345_mixer_t();

    internal int pos;

    internal readonly uint8_t[] pos_regs = new uint8_t[8];

    internal int opl_emu;
}

internal static partial class sound_sb
{
    // pcem: sound_sb.h:72-76 — sélecteur d'entrée, lu par sb_ct1345_mixer_write.
    private const int INPUT_MIC = 1;
    private const int INPUT_CD_R = 2;
    private const int INPUT_CD_L = 4;
    private const int INPUT_LINE_R = 8;
    private const int INPUT_LINE_L = 16;

    // omitted: sb_bass_treble_4bits (sound_sb.c:24-27) et sb_att_2dbstep_5bits (:29-33) — CT1745.

    // pcem: sound_sb.c:34
    private static readonly int32_t[] sb_att_4dbstep_3bits = {164, 2067, 3276, 5193, 8230, 13045, 20675, 32767};
    // pcem: sound_sb.c:35
    private static readonly int32_t[] sb_att_7dbstep_2bits = {164, 6537, 14637, 32767};

    // pcem: filters.h:104 — #define NCoef 2
    private const int NCoef = 2;

    // pcem: filters.h:108-110. Le C initialise des `float` par des littéraux `double` : la
    //   conversion double -> float de chaque littéral est reproduite par `(float)<double>`, et
    //   non par un littéral `f` (décimal -> float directement, double arrondi possible ailleurs).
    //   Tableaux locaux reconstruits à chaque appel en C, constants : hissés ici.
    private static readonly float[] sb_iir_ACoef = {(float)0.03356837051492005100, (float)0.06713674102984010200, (float)0.03356837051492005100};
    private static readonly float[] sb_iir_BCoef = {(float)1.00000000000000000000, (float)-1.41898265221812010000, (float)0.55326988968868285000};

    // pcem: filters.h:121-122 — `static float y[2][NCoef + 1]; static float x[2][NCoef + 1];`,
    //   locaux à sb_iir. Jamais remis à zéro (voir l'en-tête).
    private static readonly float[][] sb_iir_y = {new float[NCoef + 1], new float[NCoef + 1]}; // output samples
    private static readonly float[][] sb_iir_x = {new float[NCoef + 1], new float[NCoef + 1]}; // input samples

    // pcem: filters.h:106-138 — fc=3.2kHz. Tout en float (FLT_EVAL_METHOD == 0 sur x86-64 : SSE
    //   scalaire simple précision, sans contraction FMA — l'oracle est compilé sans -march).
    //   RyuJIT émet de même des opérations SSE simple précision, sans FMA implicite.
    private static float sb_iir(int i, float NewSample)
    {
        float[] ACoef = sb_iir_ACoef;
        float[] BCoef = sb_iir_BCoef;
        float[][] y = sb_iir_y;
        float[][] x = sb_iir_x;
        int n;

        // shift the old samples
        for (n = NCoef; n > 0; n--) {
                x[i][n] = x[i][n - 1];
                y[i][n] = y[i][n - 1];
        }

        // Calculate the new output
        x[i][0] = NewSample;
        y[i][0] = ACoef[0] * x[i][0];
        for (n = 1; n <= NCoef; n++)
                y[i][0] += ACoef[n] * x[i][n] - BCoef[n] * y[i][n];

        return y[i][0];
    }

    // omitted: sb_get_buffer_sb2, sb_get_buffer_sb2_mixer (sound_sb.c:38-85) — SB 1.x/2.0.

    // pcem: sound_sb.c:87-125
    //   `sb_iir(...) / 1.3` : float / double -> double, puis `* voice_l` (int -> double) et `/ 3`
    //   en double, la conversion (int32_t) tronque vers zéro, puis `>> 15` arithmétique — même
    //   ordre en C#. Amplitude bornée (|buffer| <= 32768, gain du filtre ~1, voice <= 32767) :
    //   |valeur| < 2^29, loin du débordement de la conversion (où C — cvttsd2si, 0x80000000 —
    //   et .NET 10 — saturation — divergeraient).
    internal static void sb_get_buffer_sbpro(int32_t[] buffer, int len, object? p)
    {
        sb_t sb = (sb_t)p;
        sb_ct1345_mixer_t mixer = sb.mixer_sbpro;

        int c;

        if (sb.dsp.sb_type == SBPRO)
                opl2_update2(sb.opl);
        else
                opl3_update2(sb.opl);

        sb_dsp_update(sb.dsp);
        for (c = 0; c < len * 2; c += 2) {
                int32_t out_l, out_r;

                out_l = ((((sb.opl.buffer[c] * mixer.fm_l) >> 16) * (sb.opl_emu != 0 ? 47000 : 51000)) >> 15);
                out_r = ((((sb.opl.buffer[c + 1] * mixer.fm_r) >> 16) * (sb.opl_emu != 0 ? 47000 : 51000)) >> 15);

                /*TODO: Implement the stereo switch on the mixer instead of on the dsp? */
                if (mixer.output_filter != 0) {
                        out_l += (int32_t)(((sb_iir(0, (float)sb.dsp.buffer[c]) / 1.3) * mixer.voice_l) / 3) >> 15;
                        out_r += (int32_t)(((sb_iir(1, (float)sb.dsp.buffer[c + 1]) / 1.3) * mixer.voice_r) / 3) >> 15;
                } else {
                        out_l += ((int32_t)(sb.dsp.buffer[c] * mixer.voice_l) / 3) >> 15;
                        out_r += ((int32_t)(sb.dsp.buffer[c + 1] * mixer.voice_r) / 3) >> 15;
                }
                // TODO: recording CD, Mic with AGC or line in. Note: mic volume does not affect recording.

                out_l = (out_l * mixer.master_l) >> 15;
                out_r = (out_r * mixer.master_r) >> 15;

                buffer[c] += out_l;
                buffer[c + 1] += out_r;
        }

        sb.pos = 0;
        sb.opl.pos = 0;
        sb.dsp.pos = 0;
    }

    // omitted: sb_get_buffer_sb16, sb_get_buffer_emu8k (sound_sb.c:127-...), le mélangeur CT1335
    //   (sb_ct1335_mixer_write/read/reset, :...-398) — SB 2.0 CD, SB16, AWE32.

    // pcem: sound_sb.c:400-494
    internal static void sb_ct1345_mixer_write(uint16_t addr, uint8_t val, object p)
    {
        sb_t sb = (sb_t)p;
        sb_ct1345_mixer_t mixer = sb.mixer_sbpro;

        if ((addr & 1) == 0) {
                mixer.index = val;
                mixer.regs[0x01] = val;
        } else {
                if (mixer.index == 0) {
                        /* Reset */
                        mixer.regs[0x0A] = 0 << 1;
                        mixer.regs[0x0C] = (0 << 5) | (0 << 3) | (0 << 1);
                        mixer.regs[0x0E] = (0 << 5) | (0 << 1);
                        /* changed default from -11dB to 0dB */
                        mixer.regs[0x04] = (7 << 5) | (7 << 1);
                        mixer.regs[0x22] = (7 << 5) | (7 << 1);
                        mixer.regs[0x26] = (7 << 5) | (7 << 1);
                        mixer.regs[0x28] = (0 << 5) | (0 << 1);
                        mixer.regs[0x2E] = (0 << 5) | (0 << 1);
                        sb_dsp_set_stereo(sb.dsp, mixer.regs[0x0E] & 2);
                } else {
                        mixer.regs[mixer.index] = val;
                        switch (mixer.index) {
                                /* Compatibility: chain registers 0x02 and 0x22 as well as 0x06 and 0x26 */
                        case 0x02:
                        case 0x06:
                                mixer.regs[mixer.index + 0x20] = (uint8_t)(((val & 0xE) << 4) | (val & 0xE));
                                break;

                        case 0x22:
                        case 0x26:
                                mixer.regs[mixer.index - 0x20] = (uint8_t)(val & 0xE);
                                break;

                                /* More compatibility:  SoundBlaster Pro selects register 020h for 030h, 022h for 032h, 026h for
                                 * 036h,028h for 038h. */
                        case 0x30:
                        case 0x32:
                        case 0x36:
                        case 0x38:
                                mixer.regs[mixer.index - 0x10] = (uint8_t)(val & 0xEE);
                                break;

                        case 0x00:
                        case 0x04:
                        case 0x0a:
                        case 0x0c:
                        case 0x0e:
                        case 0x28:
                        case 0x2e:
                                break;

                        default:
                                // omitted: pclog("sb_ct1345: Unknown register WRITE…") (:453) — sortie pure.
                                break;
                        }
                }

                mixer.voice_l = sb_att_4dbstep_3bits[(mixer.regs[0x04] >> 5) & 0x7];
                mixer.voice_r = sb_att_4dbstep_3bits[(mixer.regs[0x04] >> 1) & 0x7];
                mixer.master_l = sb_att_4dbstep_3bits[(mixer.regs[0x22] >> 5) & 0x7];
                mixer.master_r = sb_att_4dbstep_3bits[(mixer.regs[0x22] >> 1) & 0x7];
                mixer.fm_l = sb_att_4dbstep_3bits[(mixer.regs[0x26] >> 5) & 0x7];
                mixer.fm_r = sb_att_4dbstep_3bits[(mixer.regs[0x26] >> 1) & 0x7];
                mixer.cd_l = sb_att_4dbstep_3bits[(mixer.regs[0x28] >> 5) & 0x7];
                mixer.cd_r = sb_att_4dbstep_3bits[(mixer.regs[0x28] >> 1) & 0x7];
                mixer.line_l = sb_att_4dbstep_3bits[(mixer.regs[0x2E] >> 5) & 0x7];
                mixer.line_r = sb_att_4dbstep_3bits[(mixer.regs[0x2E] >> 1) & 0x7];

                mixer.mic = sb_att_7dbstep_2bits[(mixer.regs[0x0A] >> 1) & 0x3];

                mixer.output_filter = (mixer.regs[0xE] & 0x20) == 0 ? 1 : 0;
                mixer.input_filter = (mixer.regs[0xC] & 0x20) == 0 ? 1 : 0;
                mixer.in_filter_freq = ((mixer.regs[0xC] & 0x8) == 0) ? 3200 : 8800;
                mixer.stereo = mixer.regs[0xE] & 2;
                if (mixer.index == 0xE)
                        sb_dsp_set_stereo(sb.dsp, val & 2);

                switch ((mixer.regs[0xc] & 6)) {
                case 2:
                        mixer.input_selector = INPUT_CD_L | INPUT_CD_R;
                        break;
                case 6:
                        mixer.input_selector = INPUT_LINE_L | INPUT_LINE_R;
                        break;
                default:
                        mixer.input_selector = INPUT_MIC;
                        break;
                }

                /* TODO: pcspeaker volume? Or is it not worth? */
                sound.sound_set_cd_volume(((uint32_t)mixer.master_l * (uint32_t)mixer.cd_l) / 65535,
                                          ((uint32_t)mixer.master_r * (uint32_t)mixer.cd_r) / 65535);
        }
    }

    // pcem: sound_sb.c:496-527
    internal static uint8_t sb_ct1345_mixer_read(uint16_t addr, object p)
    {
        sb_t sb = (sb_t)p;
        sb_ct1345_mixer_t mixer = sb.mixer_sbpro;

        if ((addr & 1) == 0)
                return mixer.index;

        switch (mixer.index) {
        case 0x00:
        case 0x04:
        case 0x0a:
        case 0x0c:
        case 0x0e:
        case 0x22:
        case 0x26:
        case 0x28:
        case 0x2e:
        case 0x02:
        case 0x06:
        case 0x30:
        case 0x32:
        case 0x36:
        case 0x38:
                return mixer.regs[mixer.index];

        default:
                // omitted: pclog("sb_ct1345: Unknown register READ…") (:522) — sortie pure.
                break;
        }

        return 0xff;
    }
    // pcem: sound_sb.c:528-531
    internal static void sb_ct1345_mixer_reset(sb_t sb)
    {
        sb_ct1345_mixer_write(4, 0, sb);
        sb_ct1345_mixer_write(5, 0, sb);
    }

    // omitted: sb_ct1745_mixer_write/read/reset, sb_mcv_*, sb_pro_mcv_*, sb_1_init, sb_15_init,
    //   sb_mcv_init, sb_2_init, sb_pro_v1_init (sound_sb.c:533-996) — autres cartes.

    // pcem: sound_sb.c:998-1024
    // G8.2 — la carte montée, pour la sonde du son (BootDiff) ; pendant de h_sb côté oracle. Pas un
    // état de PCem.
    internal static sb_t? sb_pri;

    // G8.2 — les vingt champs du DSP et du mélangeur (h_sb_probe), à partir de l'offset f.
    internal static void ProbeSb(uint64_t[] o, int f)
    {
        var sb = sb_pri;
        if (sb == null)
                return;
        var d = sb.dsp;
        o[f++] = (uint32_t)d.sb_8_length | ((uint64_t)(uint32_t)d.sb_8_autolen << 32);
        o[f++] = (uint8_t)d.sb_8_format | ((uint64_t)(uint8_t)d.sb_8_autoinit << 8) | ((uint64_t)(uint8_t)d.sb_8_pause << 16) |
                 ((uint64_t)(uint8_t)d.sb_8_enable << 24);
        o[f++] = (uint8_t)d.sb_8_output | ((uint64_t)(uint8_t)d.sb_8_dmanum << 8) | ((uint64_t)(uint8_t)d.sb_speaker << 16) |
                 ((uint64_t)(uint8_t)d.muted << 24);
        o[f++] = (uint64_t)(long)d.sb_pausetime;
        o[f++] = (uint32_t)d.sb_read_wp | ((uint64_t)(uint32_t)d.sb_read_rp << 32);
        o[f++] = Fnv(d.sb_read_data);
        o[f++] = (uint32_t)d.sb_data_stat | ((uint64_t)(uint32_t)d.sb_irqnum << 32);
        o[f++] = d.sbe2 | ((uint64_t)(uint32_t)d.sbe2count << 8);
        o[f++] = (uint16_t)d.sbdat | ((uint64_t)(uint32_t)d.sbdat2 << 32);
        o[f++] = (uint16_t)d.sbdatl | ((uint64_t)(uint16_t)d.sbdatr << 16) | ((uint64_t)d.sbref << 32) | ((uint64_t)(uint8_t)d.sbstep << 40);
        o[f++] = (uint32_t)d.sbdacpos | ((uint64_t)(uint32_t)d.sbleftright << 32);
        o[f++] = (uint8_t)d.sbreset | ((uint64_t)d.sbreaddat << 8) | ((uint64_t)d.sb_command << 16) | ((uint64_t)d.sb_test << 24);
        o[f++] = (uint32_t)d.sb_timeo | ((uint64_t)(uint32_t)d.sb_timei << 32);
        o[f++] = d.sblatcho;
        o[f++] = d.output_timer.ts_integer | ((uint64_t)d.output_timer.ts_frac << 32);
        o[f++] = (uint32_t)d.stereo | ((uint64_t)(uint32_t)d.wb_full << 32);
        o[f++] = (uint32_t)d.busy_count | ((uint64_t)(uint32_t)d.pos << 32);
        o[f++] = Fnv(sb.mixer_sbpro.regs);
        o[f++] = (uint32_t)sb.pos;
        o[f++] = (uint32_t)sb.mixer_sbpro.master_l | ((uint64_t)(uint32_t)sb.mixer_sbpro.master_r << 32);
    }

    private static uint64_t Fnv(ReadOnlySpan<uint8_t> p)
    {
        uint64_t hash = 1469598103934665603UL;
        foreach (var b in p)
        {
                hash ^= b;
                hash *= 1099511628211UL;
        }
        return hash;
    }

    internal static object? sb_pro_v2_init()
    {
        /*sbpro port mappings. 220h or 240h.
          2x0 to 2x3 -> FM chip (18 voices)
          2x4 to 2x5 -> Mixer interface
          2x6, 2xA, 2xC, 2xE -> DSP chip
          2x8, 2x9, 388 and 389 FM chip (9 voices)
          2x0+10 to 2x0+13 CDROM interface.*/
        sb_t sb = new sb_t();
        sb_pri = sb;
        // pcem: sound_sb.c:1006 — memset(sb, 0, sizeof(sb_t)) ; `new` zéro-initialise.

        uint16_t addr = (uint16_t)device_get_config_int("addr");
        sb.opl_emu = device_get_config_int("opl_emu");
        opl3_init(sb.opl, sb.opl_emu);
        sb_dsp_init(sb.dsp, SBPRO2, SB_SUBTYPE_DEFAULT, sb);
        sb_dsp_setaddr(sb.dsp, addr);
        sb_dsp_setirq(sb.dsp, device_get_config_int("irq"));
        sb_dsp_setdma8(sb.dsp, device_get_config_int("dma"));
        sb_ct1345_mixer_reset(sb);
        /* DSP I/O handler is activated in sb_dsp_setaddr */
        io_sethandler((uint16_t)(addr + 0), 0x0004, opl3_read, null, null, opl3_write, null, null, sb.opl);
        io_sethandler((uint16_t)(addr + 8), 0x0002, opl3_read, null, null, opl3_write, null, null, sb.opl);
        io_sethandler(0x0388, 0x0004, opl3_read, null, null, opl3_write, null, null, sb.opl);
        io_sethandler((uint16_t)(addr + 4), 0x0002, sb_ct1345_mixer_read, null, null, sb_ct1345_mixer_write, null, null, sb);
        sound_add_handler(sb_get_buffer_sbpro, sb);

        return sb;
    }

    // omitted: sb_pro_mcv_init, sb_16_init, sb_awe32_available, sb_awe32_init (sound_sb.c:1026-1095).

    // pcem: sound_sb.c:1097-1112
    internal static void sb_close(object p)
    {
        sb_t sb = (sb_t)p;
        if (sb_pri == sb)
                sb_pri = null;
        sb_dsp_close(sb.dsp);
        // omitted: SB_DSP_RECORD_DEBUG (:1100-1109) — #ifdef éteint.

        // omitted: free(sb) (:1111) — libération manuelle, sans objet sous GC.
    }

    // omitted: sb_awe32_close (sound_sb.c:1114-1120) — AWE32.

    // pcem: sound_sb.c:1122-1126
    internal static void sb_speed_changed(object p)
    {
        sb_t sb = (sb_t)p;

        sb_dsp_speed_changed(sb.dsp);
    }

    // omitted: sb_add_status_info (sound_sb.c:1128-1132) — texte d'état de l'hôte, comme
    //   sb_dsp_add_status_info.

    // omitted: sb_config, sb2_config, sb_mcv_config, sb_pro_v1_config (sound_sb.c:1134-1228).

    // pcem: sound_sb.c:1230-1259. Les listes `selection` (libellés et valeurs offertes : 220h/240h,
    //   IRQ 2/5/7/10, DMA 1/3, DBOPL/NukedOPL) appartiennent au dialogue de configuration, omis
    //   par device.cs ; restent le nom, le type et la valeur par défaut.
    internal static readonly device_config_t[] sb_pro_v2_config =
    [
        new device_config_t { name = "addr", type = CONFIG_SELECTION, default_int = 0x220 },
        new device_config_t { name = "irq", type = CONFIG_SELECTION, default_int = 7 },
        new device_config_t { name = "dma", type = CONFIG_SELECTION, default_int = 1 },
        new device_config_t { name = "opl_emu", type = CONFIG_SELECTION, default_int = OPL_DBOPL },
        new device_config_t { type = -1 },
    ];

    // omitted: sb_pro_mcv_config, sb_16_config, sb_awe32_config (sound_sb.c:1261-1333).

    // omitted: sb_1_device, sb_15_device, sb_mcv_device, sb_2_device, sb_pro_v1_device
    //   (sound_sb.c:1335-1344).

    // pcem: sound_sb.c:1345-1346
    // DEVIATION: add_status_info à null au lieu de sb_add_status_info — texte d'état de l'hôte,
    //   omis (voir plus haut) ; device_add_status_info teste ce pointeur avant l'appel.
    internal static readonly device_t sb_pro_v2_device = new device_t("Sound Blaster Pro v2", 0, sb_pro_v2_init, sb_close, null,
                                                                      sb_speed_changed, null, null, sb_pro_v2_config);

    // omitted: sb_pro_mcv_device, sb_16_device, sb_awe32_device (sound_sb.c:1347-1352).
}
