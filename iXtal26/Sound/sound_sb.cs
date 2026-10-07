// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/sound/sound_sb.c + includes/private/sound/sound_sb.h
//         + includes/private/filters.h:103-140 (sb_iir)
// STATUS: partial — la Sound Blaster Pro v2 (G8) : sb_t, sb_ct1345_mixer_t,
//         sb_att_4dbstep_3bits, sb_att_7dbstep_2bits, sb_iir, sb_get_buffer_sbpro,
//         sb_ct1345_mixer_write/read/reset, sb_pro_v2_init, sb_close, sb_speed_changed,
//         sb_pro_v2_config, sb_pro_v2_device. G12.0 : les SB 1.0, 1.5, 2.0 et Pro v1 —
//         sb_ct1335_mixer_t, sb_get_buffer_sb2 et sb_get_buffer_sb2_mixer, sb_ct1335_mixer_write/read/reset,
//         sb_1_init, sb_15_init, sb_2_init, sb_pro_v1_init, sb_config, sb2_config, sb_pro_v1_config et leurs
//         quatre devices.
//         Omis : les SB MCV et Pro MCV (DEVICE_MCA, PLAN.md) ; les SB 16 et AWE32 (G12.1, G12.2 : leurs
//         get_buffer, init, le mélangeur CT1745, configs et devices, les tables sb_bass_treble_4bits et
//         sb_att_2dbstep_5bits, sb_awe32_close) ; sb_add_status_info (texte de l'hôte), le débogage
//         SB_DSP_RECORD_DEBUG (#ifdef éteint).
//
// PERSISTANCE : l'état du filtre sb_iir est `static` LOCAL à une fonction `static inline` de
// filters.h, donc une copie par unité de compilation — celle de sound_sb.c. Rien ne le remet à
// zéro : il traverse les amorçages d'un même processus. Ici, champs statiques de cette classe,
// jamais remis à zéro non plus (PLAN-G8.md, décision n° 4).

// CS8600 : `(sb_t)p` part du `object?` des delegates de device.cs, sound.cs et io.cs, comme
// à sound_adlib.cs. CS8602 : même raison, à la déréférence qui suit.
#pragma warning disable CS8600, CS8602

using System.Runtime.InteropServices;
using iXtal26.PluginApi;
using static iXtal26.io;
using static iXtal26.PluginApi.device;
using static iXtal26.Sound.sound;
using static iXtal26.Sound.sound_opl;
using static iXtal26.Sound.sound_sb_dsp;

namespace iXtal26.Sound;

// pcem: sound_sb.h:18-27 — SB 2.0 CD version
internal sealed class sb_ct1335_mixer_t
{
    internal int32_t master;
    internal int32_t voice;
    internal int32_t fm;
    internal int32_t cd;

    internal uint8_t index;
    internal readonly uint8_t[] regs = new uint8_t[256];
}

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

// pcem: sound_sb.h:50-90 — SB16 and AWE32 (G12.1)
internal sealed class sb_ct1745_mixer_t
{
    internal int32_t master_l, master_r;
    internal int32_t voice_l, voice_r;
    internal int32_t fm_l, fm_r;
    internal int32_t cd_l, cd_r;
    internal int32_t line_l, line_r;
    internal int32_t mic;
    internal int32_t speaker;

    internal int bass_l, bass_r;
    internal int treble_l, treble_r;

    internal int output_selector;

    internal int input_selector_left;
    internal int input_selector_right;

    // pcem: sound_sb.h:80 — mic_agc, déclaré, jamais lu ni écrit (l'initialiseur tait CS0649).
    internal int mic_agc = 0;

    internal int32_t input_gain_L;
    internal int32_t input_gain_R;
    internal int32_t output_gain_L;
    internal int32_t output_gain_R;

    internal uint8_t index;
    internal readonly uint8_t[] regs = new uint8_t[256];
}

// pcem: sound_sb.h:91-107
// DEVIATION: (G12.0, PLAN-G12.md décision n° 2) l'union des mélangeurs (:94-98) devient des champs
//   distincts. Aucun lecteur ne croise ses membres : chaque carte n'écrit et ne lit que le sien
//   (dsp->parent ne sert qu'à l'Aztech, sound_sb_dsp.c:688-691, exclu).
internal sealed class sb_t
{
    internal readonly opl_t opl = new opl_t();
    internal readonly sb_dsp_t dsp = new sb_dsp_t();
    internal readonly sb_ct1335_mixer_t mixer_sb2 = new sb_ct1335_mixer_t();
    internal readonly sb_ct1345_mixer_t mixer_sbpro = new sb_ct1345_mixer_t();
    internal readonly sb_ct1745_mixer_t mixer_sb16 = new sb_ct1745_mixer_t();
    // pcem: sound_sb.h:99 — G12.1 : le MPU-401 de la SB 16 et de l'AWE32.
    internal readonly mpu401_uart_t mpu = new mpu401_uart_t();
    // pcem: sound_sb.h:100 — G12.2. DEVIATION de forme : alloué pour l'AWE32 seule (sb_awe32_init), là où le C
    //   l'embarque dans toute SB (sizeof(sb_t) = 917 592 octets) ; PLAN-G12.md, décision n° 15.
    internal emu8k_t? emu8k;

    internal int pos;

    internal readonly uint8_t[] pos_regs = new uint8_t[8];

    internal int opl_emu;
}

internal static partial class sound_sb
{
    // pcem: sound_sb.h:64-68 — G12.1 : le sélecteur de sortie du CT1745.
    private const int OUTPUT_MIC = 1;
    private const int OUTPUT_CD_R = 2;
    private const int OUTPUT_CD_L = 4;
    private const int OUTPUT_LINE_R = 8;
    private const int OUTPUT_LINE_L = 16;

    // pcem: sound_sb.h:72-78 — sélecteur d'entrée, lu par sb_ct1345_mixer_write ; G12.1 : et le CT1745.
    private const int INPUT_MIC = 1;
    private const int INPUT_CD_R = 2;
    private const int INPUT_CD_L = 4;
    private const int INPUT_LINE_R = 8;
    private const int INPUT_LINE_L = 16;
    private const int INPUT_MIDI_R = 32;
    private const int INPUT_MIDI_L = 64;

    /* 0 to 7 -> -14dB to 0dB i 2dB steps. 8 to 15 -> 0 to +14dB in 2dB steps.
      Note that for positive dB values, this is not amplitude, it is amplitude-1. */
    // pcem: sound_sb.c:26-27 — G12.1. Des float initialisés par des littéraux double : `(float)<double>`.
    private static readonly float[] sb_bass_treble_4bits =
    {
        (float)0.199526231, (float)0.25, (float)0.316227766, (float)0.398107170, (float)0.5, (float)0.63095734,
        (float)0.794328234, (float)1, (float)0, (float)0.25892541, (float)0.584893192, (float)1, (float)1.511886431,
        (float)2.16227766, (float)3, (float)4.011872336,
    };

    /* Attenuation tables for the mixer. Max volume = 32767 in order to give 6dB of
     * headroom and avoid integer overflow */
    // pcem: sound_sb.c:31-33 — G12.1, le CT1745.
    private static readonly int32_t[] sb_att_2dbstep_5bits =
    {
        25,   32,   41,   51,   65,    82,    103,   130,   164,   206,  260,
        327,  412,  519,  653,  822,   1036,  1304,  1641,  2067,  2602, 3276,
        4125, 5192, 6537, 8230, 10362, 13044, 16422, 20674, 26027, 32767,
    };

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
    // pcem: filters.h:6-101 — G12.1 : les quatre IIR des basses et des aigus du CT1745. Comme sb_iir : des float, un
    //   état statique propre à l'unité de compilation de sound_sb.c, jamais remis à zéro (PLAN-G12.md, décision
    //   n° 14), partagé par la SB 16 et l'AWE32 d'un même processus.
    private static readonly float[] low_iir_ACoef = {(float)0.00049713569693400649, (float)0.00099427139386801299, (float)0.00049713569693400649};
    private static readonly float[] low_iir_BCoef = {(float)1.00000000000000000000, (float)-1.93522955470669530000, (float)0.93726236021404663000};
    private static readonly float[][] low_iir_y = {new float[NCoef + 1], new float[NCoef + 1]}; // output samples
    private static readonly float[][] low_iir_x = {new float[NCoef + 1], new float[NCoef + 1]}; // input samples

    // fc=350Hz
    private static float low_iir(int i, float NewSample) => iir(low_iir_ACoef, low_iir_BCoef, low_iir_y, low_iir_x, i, NewSample);

    private static readonly float[] low_cut_iir_ACoef = {(float)0.96839970114733542000, (float)-1.93679940229467080000, (float)0.96839970114733542000};
    private static readonly float[] low_cut_iir_BCoef = {(float)1.00000000000000000000, (float)-1.93522955471202770000, (float)0.93726236021916731000};
    private static readonly float[][] low_cut_iir_y = {new float[NCoef + 1], new float[NCoef + 1]};
    private static readonly float[][] low_cut_iir_x = {new float[NCoef + 1], new float[NCoef + 1]};

    // fc=350Hz
    private static float low_cut_iir(int i, float NewSample) => iir(low_cut_iir_ACoef, low_cut_iir_BCoef, low_cut_iir_y, low_cut_iir_x, i, NewSample);

    private static readonly float[] high_iir_ACoef = {(float)0.72248704753064896000, (float)-1.44497409506129790000, (float)0.72248704753064896000};
    private static readonly float[] high_iir_BCoef = {(float)1.00000000000000000000, (float)-1.36640781670578510000, (float)0.52352474706139873000};
    private static readonly float[][] high_iir_y = {new float[NCoef + 1], new float[NCoef + 1]};
    private static readonly float[][] high_iir_x = {new float[NCoef + 1], new float[NCoef + 1]};

    // fc=3.5kHz
    private static float high_iir(int i, float NewSample) => iir(high_iir_ACoef, high_iir_BCoef, high_iir_y, high_iir_x, i, NewSample);

    private static readonly float[] high_cut_iir_ACoef = {(float)0.03927726802250377400, (float)0.07855453604500754700, (float)0.03927726802250377400};
    private static readonly float[] high_cut_iir_BCoef = {(float)1.00000000000000000000, (float)-1.36640781666419950000, (float)0.52352474703279628000};
    private static readonly float[][] high_cut_iir_y = {new float[NCoef + 1], new float[NCoef + 1]};
    private static readonly float[][] high_cut_iir_x = {new float[NCoef + 1], new float[NCoef + 1]};

    // fc=3.5kHz
    private static float high_cut_iir(int i, float NewSample) => iir(high_cut_iir_ACoef, high_cut_iir_BCoef, high_cut_iir_y, high_cut_iir_x, i, NewSample);

    // Le corps commun des cinq IIR de filters.h (:6-138), identiques au tableau de coefficients et à l'état près :
    //   `static inline` en C, une copie par fonction, chacune avec ses `static` ; ici, leurs états passés en
    //   paramètres. Même ordre d'opérations, en float.
    private static float iir(float[] ACoef, float[] BCoef, float[][] y, float[][] x, int i, float NewSample)
    {
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

    // pcem: filters.h:275-276 — `static float x[2][SB16_NCoef + 1]; static int pos = 0;`, locaux à low_fir_sb16.
    private static readonly float[][] low_fir_sb16_x = {new float[SB16_NCoef + 1], new float[SB16_NCoef + 1]}; // input samples
    private static int low_fir_sb16_pos = 0;

    // pcem: filters.h:274-295 — G12.1. Le FIR de la SB 16, sur low_fir_sb16_coef (sound_sb_dsp.c:75). pos n'avance
    //   que pour i == 1 : les deux voies partagent la même position.
    private static float low_fir_sb16(int i, float NewSample)
    {
        float[][] x = low_fir_sb16_x;
        float @out = 0.0f;
        int n;

        // Calculate the new output
        x[i][low_fir_sb16_pos] = NewSample;

        for (n = 0; n < ((SB16_NCoef + 1) - low_fir_sb16_pos) && n < SB16_NCoef; n++)
                @out += low_fir_sb16_coef[n] * x[i][n + low_fir_sb16_pos];
        for (; n < SB16_NCoef; n++)
                @out += low_fir_sb16_coef[n] * x[i][(n + low_fir_sb16_pos) - (SB16_NCoef + 1)];

        if (i == 1) {
                low_fir_sb16_pos++;
                if (low_fir_sb16_pos > SB16_NCoef)
                        low_fir_sb16_pos = 0;
        }

        return @out;
    }

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

    /* sb 1, 1.5, 2, 2 mvc do not have a mixer, so signal is hardwired */
    // pcem: sound_sb.c:38-58 — G12.0. `sb_iir(...) / 1.3` en double, `* 65536` et `/ 3` en double, la
    //   conversion (int32_t) tronque vers zéro, puis `>> 16` : |valeur| < 2^30, loin du débordement de la
    //   conversion (voir sb_get_buffer_sbpro).
    internal static void sb_get_buffer_sb2(int32_t[] buffer, int len, object? p)
    {
        sb_t sb = (sb_t)p;

        int c;

        opl2_update2(sb.opl);
        sb_dsp_update(sb.dsp);
        for (c = 0; c < len * 2; c += 2) {
                int32_t @out;
                @out = ((sb.opl.buffer[c] * 51000) >> 16);
                // TODO: Recording: Mic and line In with AGC
                @out += (int32_t)(((sb_iir(0, (float)sb.dsp.buffer[c]) / 1.3) * 65536) / 3) >> 16;

                buffer[c] += @out;
                buffer[c + 1] += @out;
        }

        sb.pos = 0;
        sb.opl.pos = 0;
        sb.dsp.pos = 0;
    }

    // pcem: sound_sb.c:60-85 — G12.0, la SB 2.0 et son mélangeur CT1335 (mixaddr).
    internal static void sb_get_buffer_sb2_mixer(int32_t[] buffer, int len, object? p)
    {
        sb_t sb = (sb_t)p;
        sb_ct1335_mixer_t mixer = sb.mixer_sb2;

        int c;

        opl2_update2(sb.opl);
        sb_dsp_update(sb.dsp);
        for (c = 0; c < len * 2; c += 2) {
                int32_t @out;

                @out = ((((sb.opl.buffer[c] * mixer.fm) >> 16) * 51000) >> 15);
                /* TODO: Recording : I assume it has direct mic and line in like sb2 */
                /* It is unclear from the docs if it has a filter, but it probably does */
                @out += (int32_t)(((sb_iir(0, (float)sb.dsp.buffer[c]) / 1.3) * mixer.voice) / 3) >> 15;

                @out = (@out * mixer.master) >> 15;

                buffer[c] += @out;
                buffer[c + 1] += @out;
        }

        sb.pos = 0;
        sb.opl.pos = 0;
        sb.dsp.pos = 0;
    }

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
                // pcem bug, reproduced: PB-148 — rien n'est enregistré : ni le CD ni la ligne que choisit 0Ch.
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

    // pcem: sound_sb.c:127-208 — G12.1, la SB 16. Les conversions float -> int32_t passent par l'aide du C
    //   (cvttss2si : INT_MIN hors bornes et pour NaN ; .NET saturerait), obligatoire à :150-151, uniforme
    //   ailleurs (PLAN-G12.md, décision n° 12). Les entiers débordent comme en C (CheckForOverflowUnderflow faux).
    internal static void sb_get_buffer_sb16(int32_t[] buffer, int len, object? p)
    {
        sb_t sb = (sb_t)p;
        sb_ct1745_mixer_t mixer = sb.mixer_sb16;

        int c;

        opl3_update2(sb.opl);
        sb_dsp_update(sb.dsp);
        int dsp_rec_pos = sb.dsp.record_pos_write;
        for (c = 0; c < len * 2; c += 2) {
                int32_t out_l, out_r, in_l, in_r;

                out_l = ((((sb.opl.buffer[c] * mixer.fm_l) >> 16) * (sb.opl_emu != 0 ? 47000 : 51000)) >> 15);
                out_r = ((((sb.opl.buffer[c + 1] * mixer.fm_r) >> 16) * (sb.opl_emu != 0 ? 47000 : 51000)) >> 15);

                /*TODO: multi-recording mic with agc/+20db, cd and line in with channel inversion */
                // pcem bug, reproduced: PB-153 — `a ? out_l : 0 + b ? out_r : 0` se lit `a ? out_l : ((0 + b) ? out_r : 0)`.
                in_l = (mixer.input_selector_left & INPUT_MIDI_L) != 0       ? out_l
                       : (0 + (mixer.input_selector_left & INPUT_MIDI_R)) != 0 ? out_r
                                                                              : 0;
                in_r = (mixer.input_selector_right & INPUT_MIDI_L) != 0       ? out_l
                       : (0 + (mixer.input_selector_right & INPUT_MIDI_R)) != 0 ? out_r
                                                                               : 0;

                // pcem bug, reproduced: PB-155 — au-delà de ~34,7 kHz le FIR dépasse le gain unité : un signal pleine
                //   échelle déborde la conversion, INT_MIN en C, que CvtI32 rend aussi.
                out_l += (Cpu._386.CvtI32((double)(low_fir_sb16(0, (float)sb.dsp.buffer[c]) * mixer.voice_l)) / 3) >> 15;
                out_r += (Cpu._386.CvtI32((double)(low_fir_sb16(1, (float)sb.dsp.buffer[c + 1]) * mixer.voice_r)) / 3) >> 15;

                out_l = (out_l * mixer.master_l) >> 15;
                out_r = (out_r * mixer.master_r) >> 15;

                if (mixer.bass_l != 8 || mixer.bass_r != 8 || mixer.treble_l != 8 || mixer.treble_r != 8) {
                        /* This is not exactly how one does bass/treble controls, but the end result is like it. A better
                         * implementation would reduce the cpu usage */
                        if (mixer.bass_l > 8)
                                out_l += Cpu._386.CvtI32((double)(low_iir(0, (float)out_l) * sb_bass_treble_4bits[mixer.bass_l]));
                        if (mixer.bass_r > 8)
                                out_r += Cpu._386.CvtI32((double)(low_iir(1, (float)out_r) * sb_bass_treble_4bits[mixer.bass_r]));
                        if (mixer.treble_l > 8)
                                out_l += Cpu._386.CvtI32((double)(high_iir(0, (float)out_l) * sb_bass_treble_4bits[mixer.treble_l]));
                        if (mixer.treble_r > 8)
                                out_r += Cpu._386.CvtI32((double)(high_iir(1, (float)out_r) * sb_bass_treble_4bits[mixer.treble_r]));
                        if (mixer.bass_l < 8)
                                out_l = Cpu._386.CvtI32((double)((out_l)*sb_bass_treble_4bits[mixer.bass_l] +
                                                  low_cut_iir(0, (float)out_l) * (1.0f - sb_bass_treble_4bits[mixer.bass_l])));
                        if (mixer.bass_r < 8)
                                out_r = Cpu._386.CvtI32((double)((out_r)*sb_bass_treble_4bits[mixer.bass_r] +
                                                  low_cut_iir(1, (float)out_r) * (1.0f - sb_bass_treble_4bits[mixer.bass_r])));
                        if (mixer.treble_l < 8)
                                out_l = Cpu._386.CvtI32((double)((out_l)*sb_bass_treble_4bits[mixer.treble_l] +
                                                  high_cut_iir(0, (float)out_l) * (1.0f - sb_bass_treble_4bits[mixer.treble_l])));
                        if (mixer.treble_r < 8)
                                out_r = Cpu._386.CvtI32((double)((out_r)*sb_bass_treble_4bits[mixer.treble_r] +
                                                  high_cut_iir(1, (float)out_r) * (1.0f - sb_bass_treble_4bits[mixer.treble_r])));
                }
                // pcem bug, reproduced: PB-148 — sb_enable_i n'est jamais écrit : ce bloc ne s'exécute pas.
                if (sb.dsp.sb_enable_i != 0) {
                        int c_record = dsp_rec_pos;
                        c_record += (((c / 2) * sb.dsp.sb_freq) / 48000) * 2;
                        in_l <<= mixer.input_gain_L;
                        in_r <<= mixer.input_gain_R;
                        // Clip signal
                        if (in_l < -32768)
                                in_l = -32768;
                        else if (in_l > 32767)
                                in_l = 32767;

                        if (in_r < -32768)
                                in_r = -32768;
                        else if (in_r > 32767)
                                in_r = 32767;
                        // pcem bug, reproduced: PB-151 — l'indice 0xFFFF écrit buffer[0] (record_ecrit).
                        record_ecrit(sb.dsp, c_record & 0xFFFF, (int16_t)in_l);
                        record_ecrit(sb.dsp, (c_record + 1) & 0xFFFF, (int16_t)in_r);
                }

                buffer[c] += (out_l << mixer.output_gain_L);
                buffer[c + 1] += (out_r << mixer.output_gain_R);
        }
        // pcem bug, reproduced: PB-156 — `len * sb_freq` déborde après 40h FFh (2 400 × 1 000 000), comportement
        //   indéfini que GCC enveloppe (imul) ; le C# enveloppe de même.
        sb.dsp.record_pos_write += ((len * sb.dsp.sb_freq) / 48000) * 2;
        sb.dsp.record_pos_write &= 0xFFFF;

        sb.pos = 0;
        sb.opl.pos = 0;
        sb.dsp.pos = 0;
    }

    // pcem bug, reproduced: PB-151 — l'écriture sœur de record_lu (G12.1) : record_buffer[0xFFFF] est buffer[0] dans
    //   la disposition du C. Seul le bloc mort de sb_enable_i (sound_sb.c:195-196, :303-304) l'emploierait.
    private static void record_ecrit(sb_dsp_t dsp, int i, int16_t v)
    {
        if (i < 0xFFFF)
                dsp.record_buffer[i] = v;
        else
                dsp.buffer[i - 0xFFFF] = v;
    }

    // pcem: sound_sb.c:214-331 — G12.2, l'AWE32 : sb_get_buffer_sb16, plus l'EMU8000, rééchantillonné de 44,1 à 48 kHz
    //   par répétition (:226). Les gains de l'OPL sont `>> 15` puis `>> 16` (:228-229), l'inverse de la SB 16 : on ne
    //   factorise pas.
    // pcem bug, reproduced: PB-160 — le rééchantillonnage par répétition, sans interpolation (repliement).
    internal static void sb_get_buffer_emu8k(int32_t[] buffer, int len, object? p)
    {
        sb_t sb = (sb_t)p;
        sb_ct1745_mixer_t mixer = sb.mixer_sb16;
        emu8k_t emu = sb.emu8k!;

        int c;

        opl3_update2(sb.opl);
        sound_emu8k.emu8k_update(emu);
        sb_dsp_update(sb.dsp);
        int dsp_rec_pos = sb.dsp.record_pos_write;
        for (c = 0; c < len * 2; c += 2) {
                int32_t out_l, out_r, in_l, in_r;
                int c_emu8k = (((c / 2) * 44100) / 48000) * 2;

                out_l = ((((sb.opl.buffer[c] * mixer.fm_l) >> 15) * (sb.opl_emu != 0 ? 47000 : 51000)) >> 16);
                out_r = ((((sb.opl.buffer[c + 1] * mixer.fm_r) >> 15) * (sb.opl_emu != 0 ? 47000 : 51000)) >> 16);

                out_l += ((emu.buffer[c_emu8k] * mixer.fm_l) >> 15);
                out_r += ((emu.buffer[c_emu8k + 1] * mixer.fm_r) >> 15);

                /*TODO: multi-recording mic with agc/+20db, cd and line in with channel inversion  */
                // pcem bug, reproduced: PB-153 — la sélection d'entrée MIDI mal parenthésée.
                in_l = (mixer.input_selector_left & INPUT_MIDI_L) != 0       ? out_l
                       : (0 + (mixer.input_selector_left & INPUT_MIDI_R)) != 0 ? out_r
                                                                              : 0;
                in_r = (mixer.input_selector_right & INPUT_MIDI_L) != 0       ? out_l
                       : (0 + (mixer.input_selector_right & INPUT_MIDI_R)) != 0 ? out_r
                                                                               : 0;

                // pcem bug, reproduced: PB-155 — la conversion du C (cvttss2si), comme à sb_get_buffer_sb16.
                out_l += (Cpu._386.CvtI32((double)(low_fir_sb16(0, (float)sb.dsp.buffer[c]) * mixer.voice_l)) / 3) >> 15;
                out_r += (Cpu._386.CvtI32((double)(low_fir_sb16(1, (float)sb.dsp.buffer[c + 1]) * mixer.voice_r)) / 3) >> 15;

                out_l = (out_l * mixer.master_l) >> 15;
                out_r = (out_r * mixer.master_r) >> 15;

                if (mixer.bass_l != 8 || mixer.bass_r != 8 || mixer.treble_l != 8 || mixer.treble_r != 8) {
                        /* This is not exactly how one does bass/treble controls, but the end result is like it. A better
                         * implementation would reduce the cpu usage */
                        if (mixer.bass_l > 8)
                                out_l += Cpu._386.CvtI32((double)(low_iir(0, (float)out_l) * sb_bass_treble_4bits[mixer.bass_l]));
                        if (mixer.bass_r > 8)
                                out_r += Cpu._386.CvtI32((double)(low_iir(1, (float)out_r) * sb_bass_treble_4bits[mixer.bass_r]));
                        if (mixer.treble_l > 8)
                                out_l += Cpu._386.CvtI32((double)(high_iir(0, (float)out_l) * sb_bass_treble_4bits[mixer.treble_l]));
                        if (mixer.treble_r > 8)
                                out_r += Cpu._386.CvtI32((double)(high_iir(1, (float)out_r) * sb_bass_treble_4bits[mixer.treble_r]));
                        if (mixer.bass_l < 8)
                                out_l = Cpu._386.CvtI32((double)(out_l * sb_bass_treble_4bits[mixer.bass_l] +
                                                  low_cut_iir(0, (float)out_l) * (1.0f - sb_bass_treble_4bits[mixer.bass_l])));
                        if (mixer.bass_r < 8)
                                out_r = Cpu._386.CvtI32((double)(out_r * sb_bass_treble_4bits[mixer.bass_r] +
                                                  low_cut_iir(1, (float)out_r) * (1.0f - sb_bass_treble_4bits[mixer.bass_r])));
                        if (mixer.treble_l < 8)
                                out_l = Cpu._386.CvtI32((double)(out_l * sb_bass_treble_4bits[mixer.treble_l] +
                                                  high_cut_iir(0, (float)out_l) * (1.0f - sb_bass_treble_4bits[mixer.treble_l])));
                        if (mixer.treble_r < 8)
                                out_r = Cpu._386.CvtI32((double)(out_r * sb_bass_treble_4bits[mixer.treble_r] +
                                                  high_cut_iir(1, (float)out_r) * (1.0f - sb_bass_treble_4bits[mixer.treble_r])));
                }
                // pcem bug, reproduced: PB-148 — sb_enable_i n'est jamais écrit : ce bloc ne s'exécute pas.
                if (sb.dsp.sb_enable_i != 0) {
                        //                      in_l += (mixer->input_selector_left&INPUT_CD_L) ?
                        //                      audio_cd_buffer[cd_read_pos+c_emu8k] : 0 + (mixer->input_selector_left&INPUT_CD_R)
                        //                      ? audio_cd_buffer[cd_read_pos+c_emu8k+1] : 0; in_r +=
                        //                      (mixer->input_selector_right&INPUT_CD_L) ? audio_cd_buffer[cd_read_pos+c_emu8k]: 0
                        //                      + (mixer->input_selector_right&INPUT_CD_R) ?
                        //                      audio_cd_buffer[cd_read_pos+c_emu8k+1] : 0;

                        int c_record = dsp_rec_pos;
                        c_record += (((c / 2) * sb.dsp.sb_freq) / 48000) * 2;
                        // omitted: SB_DSP_RECORD_DEBUG (:282-290) — #ifdef éteint.
                        in_l <<= mixer.input_gain_L;
                        in_r <<= mixer.input_gain_R;
                        // Clip signal
                        if (in_l < -32768)
                                in_l = -32768;
                        else if (in_l > 32767)
                                in_l = 32767;

                        if (in_r < -32768)
                                in_r = -32768;
                        else if (in_r > 32767)
                                in_r = 32767;
                        // pcem bug, reproduced: PB-151 — l'indice 0xFFFF écrit buffer[0] (record_ecrit).
                        record_ecrit(sb.dsp, c_record & 0xFFFF, (int16_t)in_l);
                        record_ecrit(sb.dsp, (c_record + 1) & 0xFFFF, (int16_t)in_r);
                        // omitted: SB_DSP_RECORD_DEBUG (:305-312) — #ifdef éteint.
                }

                buffer[c] += (out_l << mixer.output_gain_L);
                buffer[c + 1] += (out_r << mixer.output_gain_R);
        }
        // omitted: SB_DSP_RECORD_DEBUG (:318-323) — #ifdef éteint.

        // pcem bug, reproduced: PB-156 — `len * sb_freq` déborde après 40h FFh, enveloppé comme en C.
        sb.dsp.record_pos_write += ((len * sb.dsp.sb_freq) / 48000) * 2;
        sb.dsp.record_pos_write &= 0xFFFF;
        sb.pos = 0;
        sb.opl.pos = 0;
        sb.dsp.pos = 0;
        emu.pos = 0;
    }

    // pcem: sound_sb.c:333-371 — G12.0. Le mélangeur est remis à zéro par sb_2_init MÊME sans mixaddr
    //   (:952) : sound_set_cd_volume(20, 20), master 4 << 1 (8230) par le CD au minimum (164) — l'audio
    //   CD d'une SB 2.0 sans mélangeur est à −70 dB, et l'invité n'a aucun port pour le relever (PB-145). Avec
    //   le mélangeur, le CD au minimum est la valeur de reset du guide.
    internal static void sb_ct1335_mixer_write(uint16_t addr, uint8_t val, object p)
    {
        sb_t sb = (sb_t)p;
        sb_ct1335_mixer_t mixer = sb.mixer_sb2;

        if ((addr & 1) == 0) {
                mixer.index = val;
                mixer.regs[0x01] = val;
        } else {
                if (mixer.index == 0) {
                        /* Reset */
                        mixer.regs[0x02] = 4 << 1;
                        mixer.regs[0x06] = 4 << 1;
                        mixer.regs[0x08] = 0 << 1;
                        // pcem bug, reproduced: PB-237 — la voix relevée à 0 dB ; le guide la met à 0 ⇒ −46 dB
                        //   (p. 4-5). 02h, 06h et 08h sont ceux du guide.
                        /* changed default from -46dB to 0dB*/
                        mixer.regs[0x0A] = 3 << 1;
                } else {
                        mixer.regs[mixer.index] = val;
                        switch (mixer.index) {
                        case 0x00:
                        case 0x02:
                        case 0x06:
                        case 0x08:
                        case 0x0A:
                                break;

                        default:
                                // omitted: pclog("sb_ct1335: Unknown register WRITE…") (:359) — sortie pure.
                                break;
                        }
                }
                mixer.master = sb_att_4dbstep_3bits[(mixer.regs[0x02] >> 1) & 0x7];
                mixer.fm = sb_att_4dbstep_3bits[(mixer.regs[0x06] >> 1) & 0x7];
                mixer.cd = sb_att_4dbstep_3bits[(mixer.regs[0x08] >> 1) & 0x7];
                mixer.voice = sb_att_7dbstep_2bits[(mixer.regs[0x0A] >> 1) & 0x3];

                sound.sound_set_cd_volume(((uint32_t)mixer.master * (uint32_t)mixer.cd) / 65535,
                                          ((uint32_t)mixer.master * (uint32_t)mixer.cd) / 65535);
        }
    }

    // pcem: sound_sb.c:373-393
    internal static uint8_t sb_ct1335_mixer_read(uint16_t addr, object p)
    {
        sb_t sb = (sb_t)p;
        sb_ct1335_mixer_t mixer = sb.mixer_sb2;

        if ((addr & 1) == 0)
                return mixer.index;

        switch (mixer.index) {
        case 0x00:
        case 0x02:
        case 0x06:
        case 0x08:
        case 0x0A:
                return mixer.regs[mixer.index];
        default:
                // omitted: pclog("sb_ct1335: Unknown register READ…") (:388) — sortie pure.
                break;
        }

        return 0xff;
    }

    // pcem: sound_sb.c:395-398
    internal static void sb_ct1335_mixer_reset(sb_t sb)
    {
        sb_ct1335_mixer_write(0x254, 0, sb);
        sb_ct1335_mixer_write(0x255, 0, sb);
    }

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
                        // pcem bug, reproduced: PB-237 — la voix, le général et la MIDI relevés à 0 dB (7) ; le
                        //   guide les met à 4 ⇒ −11 dB (p. 4-9). 28h, le CD à 0, est celui du guide.
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

    // pcem: sound_sb.c:533-673 — G12.1, la SB 16 et l'AWE32. Le reset laisse le CD au minimum, la valeur du guide :
    //   32767 × 25 / 65535, 12 sur 65 535, avec le général relevé.
    internal static void sb_ct1745_mixer_write(uint16_t addr, uint8_t val, object p)
    {
        sb_t sb = (sb_t)p;
        sb_ct1745_mixer_t mixer = sb.mixer_sb16;

        if ((addr & 1) == 0) {
                mixer.index = val;
        } else {
                // pcem bug, reproduced: PB-153 — 01h n'est pas tenu (le CT1335 et le CT1345 y rangent l'index) : il se
                //   relit FFh.
                // TODO: and this?  001h:
                /*DESCRIPTION
         Contains previously selected register value.  Mixer Data Register value
             NOTES
         * SoundBlaster 16 sets bit 7 if previous mixer index invalid.
         * Status bytes initially 080h on startup for all but level bytes (SB16)
                 */

                if (mixer.index == 0) {
                        /* Reset */
                        // pcem bug, reproduced: PB-237 — le général, la voix et la MIDI relevés à 0 dB (31) ;
                        //   le guide les met à 24 ⇒ −14 dB (p. 4-15). Le CD à 0 est celui du guide.
                        /* Changed defaults from -14dB to 0dB*/
                        mixer.regs[0x30] = 31 << 3;
                        mixer.regs[0x31] = 31 << 3;
                        mixer.regs[0x32] = 31 << 3;
                        mixer.regs[0x33] = 31 << 3;
                        mixer.regs[0x34] = 31 << 3;
                        mixer.regs[0x35] = 31 << 3;
                        mixer.regs[0x36] = 0 << 3;
                        mixer.regs[0x37] = 0 << 3;
                        mixer.regs[0x38] = 0 << 3;
                        mixer.regs[0x39] = 0 << 3;

                        mixer.regs[0x3A] = 0 << 3;
                        mixer.regs[0x3B] = 0 << 6;
                        mixer.regs[0x3C] = OUTPUT_MIC | OUTPUT_CD_R | OUTPUT_CD_L | OUTPUT_LINE_R | OUTPUT_LINE_L;
                        // pcem bug, reproduced: PB-153 — 3Dh et 3Eh à 55h et 2Bh, la MIDI reliée ; le guide : 15h et
                        //   0Bh, la MIDI ouverte (p. 4-16).
                        mixer.regs[0x3D] = INPUT_MIC | INPUT_CD_L | INPUT_LINE_L | INPUT_MIDI_L;
                        mixer.regs[0x3E] = INPUT_MIC | INPUT_CD_R | INPUT_LINE_R | INPUT_MIDI_R;

                        mixer.regs[0x3F] = mixer.regs[0x40] = 0 << 6;
                        mixer.regs[0x41] = mixer.regs[0x42] = 0 << 6;

                        mixer.regs[0x44] = mixer.regs[0x45] = 8 << 4;
                        mixer.regs[0x46] = mixer.regs[0x47] = 8 << 4;

                        mixer.regs[0x43] = 0;
                } else {
                        mixer.regs[mixer.index] = val;
                }
                switch (mixer.index) {
                        /* SBPro compatibility. Copy values to sb16 registers. */
                case 0x22:
                        mixer.regs[0x30] = (uint8_t)((mixer.regs[0x22] & 0xF0) | 0x8);
                        mixer.regs[0x31] = (uint8_t)(((mixer.regs[0x22] & 0xf) << 4) | 0x8);
                        break;
                case 0x04:
                        mixer.regs[0x32] = (uint8_t)((mixer.regs[0x04] & 0xF0) | 0x8);
                        mixer.regs[0x33] = (uint8_t)(((mixer.regs[0x04] & 0xf) << 4) | 0x8);
                        break;
                case 0x26:
                        mixer.regs[0x34] = (uint8_t)((mixer.regs[0x26] & 0xF0) | 0x8);
                        mixer.regs[0x35] = (uint8_t)(((mixer.regs[0x26] & 0xf) << 4) | 0x8);
                        break;
                case 0x28:
                        mixer.regs[0x36] = (uint8_t)((mixer.regs[0x28] & 0xF0) | 0x8);
                        mixer.regs[0x37] = (uint8_t)(((mixer.regs[0x28] & 0xf) << 4) | 0x8);
                        break;
                case 0x2E:
                        mixer.regs[0x38] = (uint8_t)((mixer.regs[0x2E] & 0xF0) | 0x8);
                        mixer.regs[0x39] = (uint8_t)(((mixer.regs[0x2E] & 0xf) << 4) | 0x8);
                        break;
                case 0x0A:
                        // pcem bug, reproduced: PB-153 — 0Ah × 3 + 10, sans `& 7` ni `<< 3` (3Ah a ses 5 bits en D7:D3,
                        //   que :654 relit `>> 3`), tronqué à 8 bits.
                        mixer.regs[0x3A] = (uint8_t)((mixer.regs[0x0A] * 3) + 10);
                        break;

                        /*
                         (DSP 4.xx feature) The Interrupt Setup register, addressed as register 80h on the Mixer register map, is
                         used to configure or determine the Interrupt request line. The DMA setup register, addressed as register
                         81h on the Mixer register map, is used to configure or determine the DMA channels.

                         Note: Registers 80h and 81h are Read-only for PnP boards.
                         */
                // pcem bug, reproduced: PB-153 — plusieurs bits : le dernier gagne ; 0 : rien ne change.
                case 0x80:
                        if ((val & 1) != 0)
                                sb_dsp_setirq(sb.dsp, 2);
                        if ((val & 2) != 0)
                                sb_dsp_setirq(sb.dsp, 5);
                        if ((val & 4) != 0)
                                sb_dsp_setirq(sb.dsp, 7);
                        if ((val & 8) != 0)
                                sb_dsp_setirq(sb.dsp, 10);
                        break;

                // pcem bug, reproduced: PB-153 — plusieurs bits : le dernier gagne ; aucun bit 16 bits : rien ne
                //   change, et la traduction des requêtes 16 bits vers le canal 8 bits (:728-734) n'existe pas.
                case 0x81:
                        /* The documentation is confusing. sounds as if multple dma8 channels could be set. */
                        if ((val & 1) != 0)
                                sb_dsp_setdma8(sb.dsp, 0);
                        if ((val & 2) != 0)
                                sb_dsp_setdma8(sb.dsp, 1);
                        if ((val & 8) != 0)
                                sb_dsp_setdma8(sb.dsp, 3);
                        if ((val & 0x20) != 0)
                                sb_dsp_setdma16(sb.dsp, 5);
                        if ((val & 0x40) != 0)
                                sb_dsp_setdma16(sb.dsp, 6);
                        if ((val & 0x80) != 0)
                                sb_dsp_setdma16(sb.dsp, 7);
                        break;
                }

                mixer.output_selector = mixer.regs[0x3C];
                mixer.input_selector_left = mixer.regs[0x3D];
                mixer.input_selector_right = mixer.regs[0x3E];

                mixer.master_l = sb_att_2dbstep_5bits[mixer.regs[0x30] >> 3];
                mixer.master_r = sb_att_2dbstep_5bits[mixer.regs[0x31] >> 3];
                mixer.voice_l = sb_att_2dbstep_5bits[mixer.regs[0x32] >> 3];
                mixer.voice_r = sb_att_2dbstep_5bits[mixer.regs[0x33] >> 3];
                mixer.fm_l = sb_att_2dbstep_5bits[mixer.regs[0x34] >> 3];
                mixer.fm_r = sb_att_2dbstep_5bits[mixer.regs[0x35] >> 3];
                mixer.cd_l = (mixer.output_selector & OUTPUT_CD_L) != 0 ? sb_att_2dbstep_5bits[mixer.regs[0x36] >> 3] : 0;
                mixer.cd_r = (mixer.output_selector & OUTPUT_CD_R) != 0 ? sb_att_2dbstep_5bits[mixer.regs[0x37] >> 3] : 0;
                mixer.line_l = (mixer.output_selector & OUTPUT_LINE_L) != 0 ? sb_att_2dbstep_5bits[mixer.regs[0x38] >> 3] : 0;
                mixer.line_r = (mixer.output_selector & OUTPUT_LINE_R) != 0 ? sb_att_2dbstep_5bits[mixer.regs[0x39] >> 3] : 0;

                mixer.mic = sb_att_2dbstep_5bits[mixer.regs[0x3A] >> 3];
                // pcem bug, not reproduced: PB-152 — regs[0x3B] lu sans `>> 6` : l'indice va jusqu'à 787 dans une table
                //   de 32, une lecture hors tableau en C (dans .rodata), sans conséquence : speaker n'est lu nulle part.
                //   Ici l'indice est borné (rien d'observable ne change) ; speaker reste hors de la sonde.
                int speaker_i = mixer.regs[0x3B] * 3 + 22;
                mixer.speaker = sb_att_2dbstep_5bits[speaker_i < sb_att_2dbstep_5bits.Length ? speaker_i : sb_att_2dbstep_5bits.Length - 1];

                mixer.input_gain_L = (mixer.regs[0x3F] >> 6);
                mixer.input_gain_R = (mixer.regs[0x40] >> 6);
                mixer.output_gain_L = (mixer.regs[0x41] >> 6);
                mixer.output_gain_R = (mixer.regs[0x42] >> 6);

                mixer.bass_l = mixer.regs[0x46] >> 4;
                mixer.bass_r = mixer.regs[0x47] >> 4;
                mixer.treble_l = mixer.regs[0x44] >> 4;
                mixer.treble_r = mixer.regs[0x45] >> 4;

                /*TODO: pcspeaker volume, with "output_selector" check? or better not? */
                sound.sound_set_cd_volume(((uint32_t)mixer.master_l * (uint32_t)mixer.cd_l) / 65535,
                                          ((uint32_t)mixer.master_r * (uint32_t)mixer.cd_r) / 65535);
                //                pclog("sb_ct1745: Received register WRITE: %02X\t%02X\n", mixer->index,
                //                mixer->regs[mixer->index]);
        }
    }

    // pcem: sound_sb.c:675-780 — G12.1.
    internal static uint8_t sb_ct1745_mixer_read(uint16_t addr, object p)
    {
        sb_t sb = (sb_t)p;
        sb_ct1745_mixer_t mixer = sb.mixer_sb16;

        if ((addr & 1) == 0)
                return mixer.index;

        //        pclog("sb_ct1745: received register READ: %02X\t%02X\n", mixer->index, mixer->regs[mixer->index]);

        if (mixer.index >= 0x30 && mixer.index <= 0x47) {
                return mixer.regs[mixer.index];
        }
        switch (mixer.index) {
        case 0x00:
                return mixer.regs[mixer.index];

                /*SB Pro compatibility*/
        case 0x04:
                return (uint8_t)(((mixer.regs[0x33] >> 4) & 0x0f) | (mixer.regs[0x32] & 0xf0));
        case 0x0a:
                // pcem bug, reproduced: PB-153 — l'aller-retour de 0Ah ne tient que pour 0Ah <= 51h ; (0 - 10) / 3 vaut -3,
                //   tronqué vers zéro comme en C, rendu FDh.
                return (uint8_t)((mixer.regs[0x3a] - 10) / 3);
        case 0x22:
                return (uint8_t)(((mixer.regs[0x31] >> 4) & 0x0f) | (mixer.regs[0x30] & 0xf0));
        case 0x26:
                return (uint8_t)(((mixer.regs[0x35] >> 4) & 0x0f) | (mixer.regs[0x34] & 0xf0));
        case 0x28:
                return (uint8_t)(((mixer.regs[0x37] >> 4) & 0x0f) | (mixer.regs[0x36] & 0xf0));
        case 0x2e:
                return (uint8_t)(((mixer.regs[0x39] >> 4) & 0x0f) | (mixer.regs[0x38] & 0xf0));

        case 0x48:
                // Undocumented. The Creative Windows Mixer calls this after calling 3C (input selector). even when writing.
                // Also, the version I have (5.17) does not use the MIDI.L/R input selectors. it uses the volume to mute
                // (Affecting the output, obviously)
                return mixer.regs[mixer.index];

        case 0x80:
                /*TODO: Unaffected by mixer reset or soft reboot.
                 * Enabling multiple bits enables multiple IRQs.
                 */

                switch (sb.dsp.sb_irqnum) {
                case 2:
                        return 1;
                case 5:
                        return 2;
                case 7:
                        return 4;
                case 10:
                        return 8;
                }
                break;

        case 0x81: {
                /* TODO: Unaffected by mixer reset or soft reboot.
                * Enabling multiple 8 or 16-bit DMA bits enables multiple DMA channels.
                * Disabling all 8-bit DMA channel bits disables 8-bit DMA requests,
                    including translated 16-bit DMA requests.
                * Disabling all 16-bit DMA channel bits enables translation of 16-bit DMA
                    requests to 8-bit ones, using the selected 8-bit DMA channel.*/

                uint8_t result = 0;
                switch (sb.dsp.sb_8_dmanum) {
                case 0:
                        result |= 1;
                        break;
                case 1:
                        result |= 2;
                        break;
                case 3:
                        result |= 8;
                        break;
                }
                switch (sb.dsp.sb_16_dmanum) {
                case 5:
                        result |= 0x20;
                        break;
                case 6:
                        result |= 0x40;
                        break;
                case 7:
                        result |= 0x80;
                        break;
                }
                return result;
        }

                /* The Interrupt status register, addressed as register 82h on the Mixer register map,
                 is used by the ISR to determine whether the interrupt is meant for it or for some other ISR,
                 in which case it should chain to the previous routine.
                 */
        case 0x82:
                /* 0 = none, 1 =  digital 8bit or SBMIDI, 2 = digital 16bit, 4 = MPU-401 */
                /* 0x02000 DSP v4.04, 0x4000 DSP v4.05 0x8000 DSP v4.12. I haven't seen this making any difference, but I'm
                 * keeping it for now. */
                // pcem bug, reproduced: PB-153 — rendu dans un uint8_t : le `| 0x4000` disparaît, et le bit 2 (4, le
                //   MPU) n'est jamais posé.
                return (uint8_t)(((sb.dsp.sb_irq8 != 0) ? 1 : 0) | ((sb.dsp.sb_irq16 != 0) ? 2 : 0) | 0x4000);

                /* TODO: creative drivers read and write on 0xFE and 0xFF. not sure what they are supposed to be. */

        default:
                // omitted: pclog("sb_ct1745: Unknown register READ…") (:775) — sortie pure.
                break;
        }

        return 0xff;
    }

    // pcem: sound_sb.c:782-785 — G12.1.
    internal static void sb_ct1745_mixer_reset(sb_t sb)
    {
        sb_ct1745_mixer_write(4, 0, sb);
        sb_ct1745_mixer_write(5, 0, sb);
    }
    // omitted: sb_mcv_addr, sb_mcv_read/write, sb_pro_mcv_irqs, sb_pro_mcv_read/write (sound_sb.c:787-865) —
    //   les cartes MCA, exclues (PLAN.md, G12).

    // G8.2 — la carte montée, pour la sonde du son (BootDiff) ; pendant de h_sb côté oracle. Pas un
    // état de PCem. G12.0 : posé par chaque init.
    internal static sb_t? sb_pri;

    // pcem: sound_sb.c:867-887 — G12.0. Le CMS (2x0-2x3) est omis (PLAN-G12.md, décision n° 1) : rien n'y
    //   répond.
    internal static object? sb_1_init()
    {
        /*sb1/2 port mappings, 210h to 260h in 10h steps
          2x0 to 2x3 -> CMS chip
          2x6, 2xA, 2xC, 2xE -> DSP chip
          2x8, 2x9, 388 and 389 FM chip*/
        sb_t sb = new sb_t();
        sb_pri = sb;
        uint16_t addr = (uint16_t)device_get_config_int("addr");
        // pcem: sound_sb.c:874 — memset(sb, 0, sizeof(sb_t)) ; `new` zéro-initialise.

        opl2_init(sb.opl);
        sb_dsp_init(sb.dsp, SB1, SB_SUBTYPE_DEFAULT, sb);
        sb_dsp_setaddr(sb.dsp, addr);
        sb_dsp_setirq(sb.dsp, device_get_config_int("irq"));
        sb_dsp_setdma8(sb.dsp, device_get_config_int("dma"));
        /* CMS I/O handler is activated on the dedicated sound_cms module
           DSP I/O handler is activated in sb_dsp_setaddr */
        io_sethandler((uint16_t)(addr + 8), 0x0002, opl2_read, null, null, opl2_write, null, null, sb.opl);
        io_sethandler(0x0388, 0x0002, opl2_read, null, null, opl2_write, null, null, sb.opl);
        sound_add_handler(sb_get_buffer_sb2, sb);
        return sb;
    }

    // pcem: sound_sb.c:888-908 — G12.0.
    internal static object? sb_15_init()
    {
        /*sb1/2 port mappings, 210h to 260h in 10h steps
          2x0 to 2x3 -> CMS chip
          2x6, 2xA, 2xC, 2xE -> DSP chip
          2x8, 2x9, 388 and 389 FM chip*/
        sb_t sb = new sb_t();
        sb_pri = sb;
        uint16_t addr = (uint16_t)device_get_config_int("addr");
        // pcem: sound_sb.c:895 — memset(sb, 0, sizeof(sb_t)).

        opl2_init(sb.opl);
        sb_dsp_init(sb.dsp, SB15, SB_SUBTYPE_DEFAULT, sb);
        sb_dsp_setaddr(sb.dsp, addr);
        sb_dsp_setirq(sb.dsp, device_get_config_int("irq"));
        sb_dsp_setdma8(sb.dsp, device_get_config_int("dma"));
        /* CMS I/O handler is activated on the dedicated sound_cms module
           DSP I/O handler is activated in sb_dsp_setaddr */
        io_sethandler((uint16_t)(addr + 8), 0x0002, opl2_read, null, null, opl2_write, null, null, sb.opl);
        io_sethandler(0x0388, 0x0002, opl2_read, null, null, opl2_write, null, null, sb.opl);
        sound_add_handler(sb_get_buffer_sb2, sb);
        return sb;
    }

    // omitted: sb_mcv_init (sound_sb.c:910-928) — la SB MCV, exclue.

    // pcem: sound_sb.c:929-968 — G12.0. GAMEBLASTER vaut 0 (pc.GAMEBLASTER, PLAN-G12.md décision n° 3) :
    //   le miroir de l'OPL2 en 2x0-2x1 est toujours posé.
    internal static object? sb_2_init()
    {
        /*sb2 port mappings. 220h or 240h.
          2x0 to 2x3 -> CMS chip
          2x6, 2xA, 2xC, 2xE -> DSP chip
          2x8, 2x9, 388 and 389 FM chip
        "CD version" also uses 250h or 260h for
          2x0 to 2x3 -> CDROM interface
          2x4 to 2x5 -> Mixer interface*/
        /*My SB 2.0 mirrors the OPL2 at ports 2x0/2x1. Presumably this mirror is
          disabled when the CMS chips are present.
          This mirror may also exist on SB 1.5 & MCV, however I am unable to
          test this. It shouldn't exist on SB 1.0 as the CMS chips are always
          present there.
          Syndicate requires this mirror for music to play.*/
        sb_t sb = new sb_t();
        sb_pri = sb;
        uint16_t addr = (uint16_t)device_get_config_int("addr");
        // pcem: sound_sb.c:945 — memset(sb, 0, sizeof(sb_t)).

        opl2_init(sb.opl);
        sb_dsp_init(sb.dsp, SB2, SB_SUBTYPE_DEFAULT, sb);
        sb_dsp_setaddr(sb.dsp, addr);
        sb_dsp_setirq(sb.dsp, device_get_config_int("irq"));
        sb_dsp_setdma8(sb.dsp, device_get_config_int("dma"));
        // pcem bug, reproduced: PB-145 — le mélangeur remis à zéro même sans mixaddr : le CD à 20/65535.
        sb_ct1335_mixer_reset(sb);
        /* CMS I/O handler is activated on the dedicated sound_cms module
           DSP I/O handler is activated in sb_dsp_setaddr */
        if (pc.GAMEBLASTER == 0)
                io_sethandler(addr, 0x0002, opl2_read, null, null, opl2_write, null, null, sb.opl);
        io_sethandler((uint16_t)(addr + 8), 0x0002, opl2_read, null, null, opl2_write, null, null, sb.opl);
        io_sethandler(0x0388, 0x0002, opl2_read, null, null, opl2_write, null, null, sb.opl);

        int mixer_addr = device_get_config_int("mixaddr");
        if (mixer_addr > 0) {
                io_sethandler((uint16_t)(mixer_addr + 4), 0x0002, sb_ct1335_mixer_read, null, null, sb_ct1335_mixer_write, null, null, sb);
                sound_add_handler(sb_get_buffer_sb2_mixer, sb);
        } else
                sound_add_handler(sb_get_buffer_sb2, sb);

        return sb;
    }

    // pcem: sound_sb.c:970-996 — G12.0. Deux OPL2 (opl2_l, opl2_r), et sb_get_buffer_sbpro, qui les mélange
    //   par opl2_update2 (sb_type == SBPRO, :93-94).
    internal static object? sb_pro_v1_init()
    {
        /*sbpro port mappings. 220h or 240h.
          2x0 to 2x3 -> FM chip, Left and Right (9*2 voices)
          2x4 to 2x5 -> Mixer interface
          2x6, 2xA, 2xC, 2xE -> DSP chip
          2x8, 2x9, 388 and 389 FM chip (9 voices)
          2x0+10 to 2x0+13 CDROM interface.*/
        sb_t sb = new sb_t();
        sb_pri = sb;
        uint16_t addr = (uint16_t)device_get_config_int("addr");
        // pcem: sound_sb.c:979 — memset(sb, 0, sizeof(sb_t)).

        opl2_init(sb.opl);
        sb_dsp_init(sb.dsp, SBPRO, SB_SUBTYPE_DEFAULT, sb);
        sb_dsp_setaddr(sb.dsp, addr);
        sb_dsp_setirq(sb.dsp, device_get_config_int("irq"));
        sb_dsp_setdma8(sb.dsp, device_get_config_int("dma"));
        sb_ct1345_mixer_reset(sb);
        /* DSP I/O handler is activated in sb_dsp_setaddr */
        io_sethandler((uint16_t)(addr + 0), 0x0002, opl2_l_read, null, null, opl2_l_write, null, null, sb.opl);
        io_sethandler((uint16_t)(addr + 2), 0x0002, opl2_r_read, null, null, opl2_r_write, null, null, sb.opl);
        io_sethandler((uint16_t)(addr + 8), 0x0002, opl2_read, null, null, opl2_write, null, null, sb.opl);
        io_sethandler(0x0388, 0x0002, opl2_read, null, null, opl2_write, null, null, sb.opl);
        io_sethandler((uint16_t)(addr + 4), 0x0002, sb_ct1345_mixer_read, null, null, sb_ct1345_mixer_write, null, null, sb);
        sound_add_handler(sb_get_buffer_sbpro, sb);

        return sb;
    }

    // G12.0 — le mélangeur que porte chaque DSP (sound_sb.h:94-98) : aucun (SB 1.0, 1.5), CT1335 (2.0), CT1345
    //   (Pro v1, Pro v2), CT1745 (16, AWE32). Pendant de h_sb_mixer_kind (harness.c).
    internal static int MixerKind(int sb_type) => sb_type switch
    {
        SB2 => 1,
        SBPRO or SBPRO2 => 2,
        >= SB16 => 3,
        _ => 0,
    };

    // G8.2, G12.0 — les champs du DSP et du mélangeur (h_sb_probe), à partir de l'offset f : vingt depuis G8.2,
    //   quatre de plus depuis G12.0 (le type, le volume CD de la carte, le mélangeur de la carte, quel qu'il soit),
    //   dix-sept depuis G12.1 (le DSP 16 bits, le CT1745 sans `speaker` (PB-152), le MPU-401, l'empreinte MIDI).
    internal static void ProbeSb(uint64_t[] o, int f)
    {
        var sb = sb_pri;
        if (sb == null)
                return;
        var d = sb.dsp;
        var kind = MixerKind(d.sb_type);
        var m16 = sb.mixer_sb16;
        uint8_t[]? regs = kind switch { 1 => sb.mixer_sb2.regs, 2 => sb.mixer_sbpro.regs, 3 => m16.regs, _ => null };
        uint8_t index = kind switch { 1 => sb.mixer_sb2.index, 2 => sb.mixer_sbpro.index, 3 => m16.index, _ => 0 };
        var (ml, mr, vl, vr, fl, fr) = kind switch
        {
            1 => (sb.mixer_sb2.master, sb.mixer_sb2.master, sb.mixer_sb2.voice, sb.mixer_sb2.voice, sb.mixer_sb2.fm, sb.mixer_sb2.fm),
            2 => (sb.mixer_sbpro.master_l, sb.mixer_sbpro.master_r, sb.mixer_sbpro.voice_l, sb.mixer_sbpro.voice_r,
                  sb.mixer_sbpro.fm_l, sb.mixer_sbpro.fm_r),
            3 => (m16.master_l, m16.master_r, m16.voice_l, m16.voice_r, m16.fm_l, m16.fm_r),
            _ => (0, 0, 0, 0, 0, 0),
        };
        o[f++] = (uint32_t)d.sb_8_length | ((uint64_t)(uint32_t)d.sb_8_autolen << 32);
        o[f++] = (uint8_t)d.sb_8_format | ((uint64_t)(uint8_t)d.sb_8_autoinit << 8) | ((uint64_t)(uint8_t)d.sb_8_pause << 16) |
                 ((uint64_t)(uint8_t)d.sb_8_enable << 24);
        o[f++] = (uint8_t)d.sb_8_output | ((uint64_t)(uint8_t)d.sb_8_dmanum << 8) | ((uint64_t)(uint8_t)d.sb_speaker << 16) |
                 ((uint64_t)(uint8_t)d.muted << 24);
        o[f++] = (uint64_t)(long)d.sb_pausetime;
        o[f++] = (uint32_t)d.sb_read_wp | ((uint64_t)(uint32_t)d.sb_read_rp << 32);
        o[f++] = Fnv(d.sb_read_data);
        o[f++] = (uint32_t)d.sb_data_stat | ((uint64_t)(uint32_t)d.sb_irqnum << 32);
        o[f++] = d.sbe2 | ((uint64_t)(uint32_t)d.sbe2count << 8) | ((uint64_t)d.sb_addr << 48);
        o[f++] = (uint16_t)d.sbdat | ((uint64_t)(uint32_t)d.sbdat2 << 32);
        o[f++] = (uint16_t)d.sbdatl | ((uint64_t)(uint16_t)d.sbdatr << 16) | ((uint64_t)d.sbref << 32) | ((uint64_t)(uint8_t)d.sbstep << 40);
        o[f++] = (uint32_t)d.sbdacpos | ((uint64_t)(uint32_t)d.sbleftright << 32);
        o[f++] = (uint8_t)d.sbreset | ((uint64_t)d.sbreaddat << 8) | ((uint64_t)d.sb_command << 16) | ((uint64_t)d.sb_test << 24);
        o[f++] = (uint32_t)d.sb_timeo | ((uint64_t)(uint32_t)d.sb_timei << 32);
        o[f++] = d.sblatcho;
        o[f++] = d.output_timer.ts_integer | ((uint64_t)d.output_timer.ts_frac << 32);
        o[f++] = (uint32_t)d.stereo | ((uint64_t)(uint32_t)d.wb_full << 32);
        o[f++] = (uint32_t)d.busy_count | ((uint64_t)(uint32_t)d.pos << 32);
        o[f++] = regs is null ? 0 : Fnv(regs);
        o[f++] = (uint32_t)sb.pos;
        o[f++] = (uint32_t)ml | ((uint64_t)(uint32_t)mr << 32);
        // G12.0
        o[f++] = (uint8_t)d.sb_type | ((uint64_t)(uint8_t)kind << 8) | ((uint64_t)index << 16);
        o[f++] = sound.cd_vol_l | ((uint64_t)sound.cd_vol_r << 32);
        o[f++] = (uint32_t)vl | ((uint64_t)(uint32_t)vr << 32);
        o[f++] = (uint32_t)fl | ((uint64_t)(uint32_t)fr << 32);
        // G12.1 — le DSP 16 bits.
        o[f++] = (uint32_t)d.sb_16_length | ((uint64_t)(uint32_t)d.sb_16_autolen << 32);
        o[f++] = (uint8_t)d.sb_16_format | ((uint64_t)(uint8_t)d.sb_16_autoinit << 8) | ((uint64_t)(uint8_t)d.sb_16_pause << 16) |
                 ((uint64_t)(uint8_t)d.sb_16_enable << 24) | ((uint64_t)(uint8_t)d.sb_16_output << 32) | ((uint64_t)(uint8_t)d.sb_16_dmanum << 40);
        o[f++] = (uint8_t)d.sb_irq8 | ((uint64_t)(uint8_t)d.sb_irq16 << 8) | ((uint64_t)(uint32_t)d.sb_freq << 32);
        o[f++] = d.sblatchi;
        o[f++] = d.input_timer.ts_integer | ((uint64_t)d.input_timer.ts_frac << 32);
        o[f++] = (uint32_t)d.asp_data_len | ((uint64_t)(uint32_t)d.record_pos_read << 32);
        // sb_commands, `static` dans sound_sb_dsp.c, échappe à l'oracle : son effet (08h) passe par la trace.
        o[f++] = (uint32_t)d.record_pos_write;
        o[f++] = Fnv(d.sb_asp_regs);
        o[f++] = Fnv(MemoryMarshal.AsBytes(low_fir_sb16_coef.AsSpan()));
        o[f++] = Fnv(MemoryMarshal.AsBytes(d.record_buffer.AsSpan()));
        // G12.1 — le CT1745 (zéro pour les autres mélangeurs) ; `speaker` n'y entre pas (PB-152).
        if (kind == 3) {
                o[f++] = (uint8_t)m16.bass_l | ((uint64_t)(uint8_t)m16.bass_r << 8) | ((uint64_t)(uint8_t)m16.treble_l << 16) |
                         ((uint64_t)(uint8_t)m16.treble_r << 24) | ((uint64_t)(uint8_t)m16.input_gain_L << 32) |
                         ((uint64_t)(uint8_t)m16.input_gain_R << 40) | ((uint64_t)(uint8_t)m16.output_gain_L << 48) |
                         ((uint64_t)(uint8_t)m16.output_gain_R << 56);
                o[f++] = (uint16_t)m16.output_selector | ((uint64_t)(uint16_t)m16.input_selector_left << 16) |
                         ((uint64_t)(uint16_t)m16.input_selector_right << 32) | ((uint64_t)(uint16_t)m16.mic << 48);
                o[f++] = (uint32_t)m16.cd_l | ((uint64_t)(uint32_t)m16.cd_r << 32);
                o[f++] = (uint32_t)m16.line_l | ((uint64_t)(uint32_t)m16.line_r << 32);
        } else
                f += 4;
        // G12.1 — le MPU-401 (la SB 16 et l'AWE32 ; zéro, comme le memset du C, sur les autres cartes) et la sortie MIDI.
        var mpu = sb.mpu;
        o[f++] = mpu.status | ((uint64_t)mpu.rx_data << 8) | ((uint64_t)(uint8_t)mpu.uart_mode << 16) | ((uint64_t)mpu.addr << 32) |
                 ((uint64_t)(uint8_t)mpu.irq << 48);
        o[f++] = sound_mpu401_uart.midi_count;
        o[f++] = sound_mpu401_uart.midi_hash;
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

    // pcem: sound_sb.c:998-1024
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
        // DEVIATION: (G8.3) NukedOPL est omis (sound_dbopl.cs) ; une section de device qui
        //   demande opl_emu = 1 (OPL_NUKED) aurait chez PCem le son de NukedOPL. Ici, le DBOPL,
        //   dit sur la sortie d'erreur, plutôt qu'un mélange des deux (le gain de sound_sb.c:102-103 suit
        //   opl_emu).
        if (sb.opl_emu != OPL_DBOPL)
        {
                Console.Error.WriteLine($"iXtal26 : opl_emu = {sb.opl_emu} — NukedOPL n'est pas transcrit, DBOPL à sa place.");
                sb.opl_emu = OPL_DBOPL;
        }
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

    // omitted: sb_pro_mcv_init (sound_sb.c:1026-1048) — la SB Pro MCV, exclue.

    // pcem: sound_sb.c:1050-1069 — G12.1, la SB 16. Ni IRQ ni DMA dans sa configuration (TODO :1059) : les défauts de
    //   sb_dsp_init (IRQ 7, DMA 1, DMA 16 bits 5), que l'invité change par 80h et 81h (PB-153). Le MPU-401 est fixe en
    //   330h, sans IRQ (PB-154).
    internal static object? sb_16_init()
    {
        sb_t sb = new sb_t();
        sb_pri = sb;
        // pcem: sound_sb.c:1052 — memset(sb, 0, sizeof(sb_t)) ; `new` zéro-initialise.

        uint16_t addr = (uint16_t)device_get_config_int("addr");
        sb.opl_emu = device_get_config_int("opl_emu");
        // DEVIATION: (G8.3) NukedOPL est omis, comme pour la Pro v2 (sb_pro_v2_init).
        if (sb.opl_emu != OPL_DBOPL)
        {
                Console.Error.WriteLine($"iXtal26 : opl_emu = {sb.opl_emu} — NukedOPL n'est pas transcrit, DBOPL à sa place.");
                sb.opl_emu = OPL_DBOPL;
        }
        opl3_init(sb.opl, sb.opl_emu);
        sb_dsp_init(sb.dsp, SB16, SB_SUBTYPE_DEFAULT, sb);
        sb_dsp_setaddr(sb.dsp, addr);
        // pcem bug, reproduced: PB-153 — ni IRQ ni DMA dans la configuration : l'IRQ 7 de sb_dsp_init, quand la carte
        //   sort d'usine à l'IRQ 5 (guide p. 5-5).
        // TODO: irq and dma options too?
        sb_ct1745_mixer_reset(sb);
        io_sethandler(addr, 0x0004, opl3_read, null, null, opl3_write, null, null, sb.opl);
        io_sethandler((uint16_t)(addr + 8), 0x0002, opl3_read, null, null, opl3_write, null, null, sb.opl);
        io_sethandler(0x0388, 0x0004, opl3_read, null, null, opl3_write, null, null, sb.opl);
        io_sethandler((uint16_t)(addr + 4), 0x0002, sb_ct1745_mixer_read, null, null, sb_ct1745_mixer_write, null, null, sb);
        sound_add_handler(sb_get_buffer_sb16, sb);
        // pcem bug, reproduced: PB-154 — le MPU-401 sans IRQ (-1), en 330h fixe (le réglage d'usine).
        sound_mpu401_uart.mpu401_uart_init(sb.mpu, 0x330, -1, 0);

        return sb;
    }

    // pcem: sound_sb.c:1071 — G12.2. Lu par device_available (device.c:46-52), donc par pc.check_sndcard ; PCem ne le
    //   lit que dans son écran (sound_card_init ne le teste pas, sound.c:102-106).
    internal static int sb_awe32_available() { return Flash.rom.rom_present("awe32.raw"); }

    // pcem: sound_sb.c:1073-1095 — G12.2, l'AWE32. Le DSP 4.13 (le type SB16 + 1, que le `== SB16` de sb_doreset
    //   écarte : 08h y est sans paramètre, juste, PB-165) ; ni IRQ ni DMA dans la configuration (TODO :1084) ; le
    //   MPU-401 fixe en 330h, sans IRQ ; l'EMU8000 en emu_addr, +400h et +800h.
    internal static object? sb_awe32_init()
    {
        sb_t sb = new sb_t();
        sb_pri = sb;
        int onboard_ram = device_get_config_int("onboard_ram");
        // pcem: sound_sb.c:1076 — memset(sb, 0, sizeof(sb_t)) ; `new` zéro-initialise.

        uint16_t addr = (uint16_t)device_get_config_int("addr");
        uint16_t emu_addr = (uint16_t)device_get_config_int("emu_addr");
        sb.opl_emu = device_get_config_int("opl_emu");
        // DEVIATION: (G8.3) NukedOPL est omis, comme pour la Pro v2 (sb_pro_v2_init).
        if (sb.opl_emu != OPL_DBOPL)
        {
                Console.Error.WriteLine($"iXtal26 : opl_emu = {sb.opl_emu} — NukedOPL n'est pas transcrit, DBOPL à sa place.");
                sb.opl_emu = OPL_DBOPL;
        }
        opl3_init(sb.opl, sb.opl_emu);
        sb_dsp_init(sb.dsp, SB16 + 1, SB_SUBTYPE_DEFAULT, sb);
        sb_dsp_setaddr(sb.dsp, addr);
        // pcem bug, reproduced: PB-153 — ni IRQ ni DMA dans la configuration : l'IRQ 7 de sb_dsp_init, quand la carte
        //   sort d'usine à l'IRQ 5 (guide p. 5-5).
        // TODO: irq and dma options too?
        sb_ct1745_mixer_reset(sb);
        io_sethandler(addr, 0x0004, opl3_read, null, null, opl3_write, null, null, sb.opl);
        io_sethandler((uint16_t)(addr + 8), 0x0002, opl3_read, null, null, opl3_write, null, null, sb.opl);
        io_sethandler(0x0388, 0x0004, opl3_read, null, null, opl3_write, null, null, sb.opl);
        io_sethandler((uint16_t)(addr + 4), 0x0002, sb_ct1745_mixer_read, null, null, sb_ct1745_mixer_write, null, null, sb);
        sound_add_handler(sb_get_buffer_emu8k, sb);
        // pcem bug, reproduced: PB-154 — le MPU-401 sans IRQ (-1), en 330h fixe (le réglage d'usine).
        sound_mpu401_uart.mpu401_uart_init(sb.mpu, 0x330, -1, 0);
        sb.emu8k = new emu8k_t();
        sound_emu8k.emu8k_init(sb.emu8k, emu_addr, onboard_ram);

        return sb;
    }

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

    // pcem: sound_sb.c:1114-1120 — G12.2.
    internal static void sb_awe32_close(object p)
    {
        sb_t sb = (sb_t)p;

        sound_emu8k.emu8k_close(sb.emu8k!);

        sb_close(sb);
    }

    // pcem: sound_sb.c:1122-1126
    internal static void sb_speed_changed(object p)
    {
        sb_t sb = (sb_t)p;

        sb_dsp_speed_changed(sb.dsp);
    }

    // omitted: sb_add_status_info (sound_sb.c:1128-1132) — texte d'état de l'hôte, comme
    //   sb_dsp_add_status_info.

    // pcem: sound_sb.c:1134-1160 — G12.0, les SB 1.0 et 1.5. omitted: `.description`, et l'entrée terminale
    //   `{.description = ""}` de chaque liste (la longueur du tableau en tient lieu, comme sb_pro_v2_config).
    internal static readonly device_config_t[] sb_config =
    [
        new device_config_t { name = "addr", type = CONFIG_SELECTION, default_int = 0x220,
            selection = [new() { description = "0x210", value = 0x210 }, new() { description = "0x220", value = 0x220 },
                         new() { description = "0x230", value = 0x230 }, new() { description = "0x240", value = 0x240 },
                         new() { description = "0x250", value = 0x250 }, new() { description = "0x260", value = 0x260 }] },
        new device_config_t { name = "irq", type = CONFIG_SELECTION, default_int = 7,
            selection = [new() { description = "IRQ 2", value = 2 }, new() { description = "IRQ 3", value = 3 }, new() { description = "IRQ 5", value = 5 }, new() { description = "IRQ 7", value = 7 }] },
        new device_config_t { name = "dma", type = CONFIG_SELECTION, default_int = 1,
            selection = [new() { description = "DMA 1", value = 1 }, new() { description = "DMA 3", value = 3 }] },
        new device_config_t { type = -1 },
    ];

    // pcem: sound_sb.c:1162-1190 — G12.0, la SB 2.0. « No mixer » (0) est une vraie entrée de la liste.
    internal static readonly device_config_t[] sb2_config =
    [
        new device_config_t { name = "addr", type = CONFIG_SELECTION, default_int = 0x220,
            selection = [new() { description = "0x220", value = 0x220 }, new() { description = "0x240", value = 0x240 }] },
        new device_config_t { name = "mixaddr", type = CONFIG_SELECTION, default_int = 0,
            selection = [new() { description = "No mixer", value = 0 }, new() { description = "0x250", value = 0x250 }, new() { description = "0x260", value = 0x260 }] },
        new device_config_t { name = "irq", type = CONFIG_SELECTION, default_int = 7,
            selection = [new() { description = "IRQ 2", value = 2 }, new() { description = "IRQ 3", value = 3 }, new() { description = "IRQ 5", value = 5 }, new() { description = "IRQ 7", value = 7 }] },
        new device_config_t { name = "dma", type = CONFIG_SELECTION, default_int = 1,
            selection = [new() { description = "DMA 1", value = 1 }, new() { description = "DMA 3", value = 3 }] },
        new device_config_t { type = -1 },
    ];

    // omitted: sb_mcv_config (sound_sb.c:1192-1206) — la SB MCV, exclue.

    // pcem: sound_sb.c:1208-1228 — G12.0, la SB Pro v1. L'IRQ 10 est dans la liste (PB-92).
    internal static readonly device_config_t[] sb_pro_v1_config =
    [
        new device_config_t { name = "addr", type = CONFIG_SELECTION, default_int = 0x220,
            selection = [new() { description = "0x220", value = 0x220 }, new() { description = "0x240", value = 0x240 }] },
        new device_config_t { name = "irq", type = CONFIG_SELECTION, default_int = 7,
            selection = [new() { description = "IRQ 2", value = 2 }, new() { description = "IRQ 5", value = 5 }, new() { description = "IRQ 7", value = 7 }, new() { description = "IRQ 10", value = 10 }] },
        new device_config_t { name = "dma", type = CONFIG_SELECTION, default_int = 1,
            selection = [new() { description = "DMA 1", value = 1 }, new() { description = "DMA 3", value = 3 }] },
        new device_config_t { type = -1 },
    ];

    // pcem: sound_sb.c:1230-1259. omitted: `.description`. Les listes `selection` (G8.3) :
    //   device_get_config_int y valide la valeur de la section [Sound Blaster Pro v2] du .cfg.
    internal static readonly device_config_t[] sb_pro_v2_config =
    [
        new device_config_t { name = "addr", type = CONFIG_SELECTION, default_int = 0x220,
            selection = [new() { description = "0x220", value = 0x220 }, new() { description = "0x240", value = 0x240 }] },
        new device_config_t { name = "irq", type = CONFIG_SELECTION, default_int = 7,
            selection = [new() { description = "IRQ 2", value = 2 }, new() { description = "IRQ 5", value = 5 }, new() { description = "IRQ 7", value = 7 }, new() { description = "IRQ 10", value = 10 }] },
        new device_config_t { name = "dma", type = CONFIG_SELECTION, default_int = 1,
            selection = [new() { description = "DMA 1", value = 1 }, new() { description = "DMA 3", value = 3 }] },
        new device_config_t { name = "opl_emu", type = CONFIG_SELECTION, default_int = OPL_DBOPL,
            selection = [new() { description = "DBOPL", value = OPL_DBOPL }, new() { description = "NukedOPL", value = OPL_NUKED }] },
        new device_config_t { type = -1 },
    ];

    // omitted: sb_pro_mcv_config (sound_sb.c:1261-1270) — la SB Pro MCV, exclue.

    // pcem: sound_sb.c:1272-1292 — G12.1, la SB 16. La clé « midi » (CONFIG_MIDI, le périphérique MIDI de l'hôte) n'a
    //   pas de liste : config_hors_liste la rend telle quelle, et rien ne la lit (sb_16_init ne la demande pas).
    internal static readonly device_config_t[] sb_16_config =
    [
        new device_config_t { name = "addr", type = CONFIG_SELECTION, default_int = 0x220,
            selection = [new() { description = "0x220", value = 0x220 }, new() { description = "0x240", value = 0x240 },
                         new() { description = "0x260", value = 0x260 }, new() { description = "0x280", value = 0x280 }] },
        new device_config_t { name = "midi", type = CONFIG_MIDI, default_int = 0 },
        new device_config_t { name = "opl_emu", type = CONFIG_SELECTION, default_int = OPL_DBOPL,
            selection = [new() { description = "DBOPL", value = OPL_DBOPL }, new() { description = "NukedOPL", value = OPL_NUKED }] },
        new device_config_t { type = -1 },
    ];

    // pcem: sound_sb.c:1294-1333 — G12.2, l'AWE32. « None » (0) est une vraie entrée de onboard_ram ; 28 Mo vaut 28 672.
    internal static readonly device_config_t[] sb_awe32_config =
    [
        new device_config_t { name = "addr", type = CONFIG_SELECTION, default_int = 0x220,
            selection = [new() { description = "0x220", value = 0x220 }, new() { description = "0x240", value = 0x240 },
                         new() { description = "0x260", value = 0x260 }, new() { description = "0x280", value = 0x280 }] },
        new device_config_t { name = "emu_addr", type = CONFIG_SELECTION, default_int = 0x620,
            selection = [new() { description = "0x620", value = 0x620 }, new() { description = "0x640", value = 0x640 },
                         new() { description = "0x660", value = 0x660 }, new() { description = "0x680", value = 0x680 }] },
        new device_config_t { name = "midi", type = CONFIG_MIDI, default_int = 0 },
        new device_config_t { name = "onboard_ram", type = CONFIG_SELECTION, default_int = 512,
            selection = [new() { description = "None", value = 0 }, new() { description = "512 KB", value = 512 },
                         new() { description = "2 MB", value = 2048 }, new() { description = "8 MB", value = 8192 },
                         new() { description = "28 MB", value = 28 * 1024 }] },
        new device_config_t { name = "opl_emu", type = CONFIG_SELECTION, default_int = OPL_DBOPL,
            selection = [new() { description = "DBOPL", value = OPL_DBOPL }, new() { description = "NukedOPL", value = OPL_NUKED }] },
        new device_config_t { type = -1 },
    ];

    // pcem: sound_sb.c:1335-1346 — G12.0 : les SB 1.0, 1.5, 2.0 et Pro v1, à côté de la Pro v2 de G8.
    // DEVIATION: add_status_info à null au lieu de sb_add_status_info — texte d'état de l'hôte,
    //   omis (voir plus haut) ; device_add_status_info teste ce pointeur avant l'appel.
    // omitted: sb_mcv_device (:1339-1340) — la SB MCV, exclue.
    internal static readonly device_t sb_1_device = new device_t("Sound Blaster v1.0", 0, sb_1_init, sb_close, null,
                                                                 sb_speed_changed, null, null, sb_config);
    internal static readonly device_t sb_15_device = new device_t("Sound Blaster v1.5", 0, sb_15_init, sb_close, null,
                                                                  sb_speed_changed, null, null, sb_config);
    internal static readonly device_t sb_2_device = new device_t("Sound Blaster v2.0", 0, sb_2_init, sb_close, null,
                                                                 sb_speed_changed, null, null, sb2_config);
    internal static readonly device_t sb_pro_v1_device = new device_t("Sound Blaster Pro v1", 0, sb_pro_v1_init, sb_close, null,
                                                                      sb_speed_changed, null, null, sb_pro_v1_config);
    internal static readonly device_t sb_pro_v2_device = new device_t("Sound Blaster Pro v2", 0, sb_pro_v2_init, sb_close, null,
                                                                      sb_speed_changed, null, null, sb_pro_v2_config);

    // omitted: sb_pro_mcv_device (sound_sb.c:1347-1348) — la SB Pro MCV, exclue.

    // pcem: sound_sb.c:1349-1350 — G12.1. Les drapeaux valent 0 chez PCem : la règle ISA 16 bits est celle de
    //   l'hôte (Host/SoundCards.cs, pc.check_sndcard ; PLAN-G12.md, décision n° 4).
    internal static readonly device_t sb_16_device = new device_t("Sound Blaster 16", 0, sb_16_init, sb_close, null,
                                                                  sb_speed_changed, null, null, sb_16_config);

    // pcem: sound_sb.c:1351-1352 — G12.2. available = sb_awe32_available, gardé : pc.check_sndcard le lit.
    internal static readonly device_t sb_awe32_device = new device_t("Sound Blaster AWE32", 0, sb_awe32_init, sb_awe32_close,
                                                                     sb_awe32_available, sb_speed_changed, null, null,
                                                                     sb_awe32_config);
}
