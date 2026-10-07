// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/sound/sound_emu8k.c + includes/private/sound/sound_emu8k.h
// STATUS: partial — G12.2 : l'EMU8000 de l'AWE32, tel que PCem le compile (FILTER_MOOG, RESAMPLER_CUBIC,
//         sound_emu8k.c:11-20) : les types (emu8k.h:5-381), les tables, EMU8K_READ, EMU8K_READ_INTERP_CUBIC,
//         EMU8K_WRITE, emu8k_inw, emu8k_outw, emu8k_inb, emu8k_outb, le chorus, la réverbération (peigne,
//         diffuseur, queue, amortisseur), emu8k_work_eq (vide), emu8k_vol_slide, emu8k_update, emu8k_init,
//         emu8k_close.
//         Omis : PORT_NAMES (:24-85) et le bloc EMU8K_DEBUG_REGISTERS (:227-279, :346-481, :726-795), éteints ;
//         EMU8K_READ_INTERP_LINEAR (:289-299), compilé mais jamais appelé ; les branches mortes du
//         préprocesseur (FILTER_INITIAL :1649-1666 et :2161-2170, FILTER_CONSTANT :1705-1729 et :2181-2188,
//         RESAMPLER_LINEAR :1633-1634) et la branche `#elif 0` (:1116-1119) ; le commentaire documentaire de
//         l'en-tête (emu8k.h:383-766) ; les lignes que PCem commente.
//
// LES UNIONS (PLAN-G12.md, décision n° 15). Les unions anonymes du C (emu8k.h:14-35, :164-312) deviennent des
//   structures à disposition explicite, un champ par union, suffixé `_u` ; leurs membres gardent les noms du C
//   (`ccca_u.ccca_qcontrol` pour `ccca_qcontrol`). Petit-boutien, comme le C le suppose (emu8k.h:10-12).
//
// LA MÉMOIRE. La ROM (512 Ki mots), le bloc vide (64 Ki mots, partagé par tous les pointeurs « vides ») et la RAM
//   sont un seul tableau ; ram_pointers garde des décalages dans ce tableau au lieu de pointeurs. EMU8K_READ en
//   reste borné par construction : le bloc sur 8 bits, l'adresse sur 16 (le repli au-delà de 24 bits passe par
//   l'union, comme en C). emu8k_t n'est alloué que pour l'AWE32 (sb_t.emu8k), là où le C l'embarque dans toute SB.
//
// LES CONVERSIONS. Les float et double restent float et double, dans l'ordre du C. Les conversions vers int32_t
//   passent par l'aide du C (Cpu._386.CvtI32 : cvttsd2si, INT_MIN hors bornes et pour NaN), obligatoire aux six
//   sites de la réverbération (:1489, :1491, :1498, :1505, :1506, :1533), uniforme ailleurs ; vers uint64_t, par
//   CvtU64 (la séquence de GCC). Les entiers débordent comme en C (CheckForOverflowUnderflow faux).
//
// PERSISTANCE : random_helper (static), dmareadbit et dmawritebit (:99-101) et les tables ne sont jamais remis à
//   zéro : ils traversent les amorçages d'un même processus, comme chez PCem (PLAN-G12.md, décision n° 14).

// CS8600 : `(emu8k_t)p` part du `object` des delegates d'io.cs, comme à sound_sb.cs.
#pragma warning disable CS8600, CS8602

using System.Runtime.InteropServices;
using static iXtal26.io;
using static iXtal26.Sound.sound;

namespace iXtal26.Sound;

// pcem: sound_emu8k.h:14-23 — used for the increment of oscillator position
[StructLayout(LayoutKind.Explicit)]
internal struct emu8k_mem_internal_t
{
    [FieldOffset(0)] internal uint64_t addr;
    [FieldOffset(0)] internal uint16_t fract_lw_address;
    [FieldOffset(2)] internal uint16_t fract_address;
    [FieldOffset(4)] internal uint32_t int_address;
}

// pcem: sound_emu8k.h:87-93
internal sealed class emu8k_envelope_t
{
    internal int state;
    internal int32_t delay_samples, hold_samples, attack_samples;
    internal int32_t value_amp_hz, value_db_oct;
    internal int32_t sustain_value_db_oct;
    internal int32_t attack_amount_amp_hz, ramp_amount_db_oct;
}

// pcem: sound_emu8k.h:95-107. DEVIATION: les deux tampons en un seul, gauche puis droite (chorus_buffer) : le C les a
//   contigus (mesuré, décalages 48 et 65 584), et le chorus droit, aux réglages extrêmes, lit sous son tampon, dans
//   le gauche (PB-162, reproduit ; PLAN-G12.md, décision n° 15).
internal sealed class emu8k_chorus_eng_t
{
    internal int32_t write;
    internal int32_t feedback;
    internal int32_t delay_samples_central;
    internal double lfodepth_multip;
    internal double delay_offset_samples_right;
    internal emu8k_mem_internal_t lfo_inc;
    internal emu8k_mem_internal_t lfo_pos;

    internal readonly int32_t[] chorus_buffer = new int32_t[sound_emu8k.EMU8K_LFOCHORUS_SIZE * 2];
}

// pcem: sound_emu8k.h:133-142. DEVIATION: le tampon a 34 × 242 = 8 228 entrées, et non MAX_REFL_SIZE (7 744) : la
//   taille que l'invité peut demander (:1162-1174) ; le C déborde au-delà de 7 744 (R9, PB-161). La sonde ne hache
//   que [0, 7744).
internal sealed class emu8k_reverb_combfilter_t
{
    internal int read_pos;
    internal readonly int32_t[] reflection = new int32_t[sound_emu8k.REFL_TAMPON];
    internal float output_gain;
    internal float feedback;
    internal float damp1;
    internal float damp2;
    internal int bufsize;
    internal int32_t filterstore;
}

// pcem: sound_emu8k.h:144-158
internal sealed class emu8k_reverb_eng_t
{
    internal int16_t out_mix;
    internal int16_t link_return_amp; /* tail part output gain ? */
    internal int8_t link_return_type;

    internal uint8_t refl_in_amp;

    internal readonly emu8k_reverb_combfilter_t[] reflections = Nouveaux(6);
    internal readonly emu8k_reverb_combfilter_t[] allpass = Nouveaux(8);
    internal readonly emu8k_reverb_combfilter_t tailL = new();
    internal readonly emu8k_reverb_combfilter_t tailR = new();

    internal readonly emu8k_reverb_combfilter_t damper = new();

    private static emu8k_reverb_combfilter_t[] Nouveaux(int n)
    {
        var t = new emu8k_reverb_combfilter_t[n];
        for (var i = 0; i < n; i++)
            t[i] = new emu8k_reverb_combfilter_t();
        return t;
    }
}

// pcem: sound_emu8k.h:160-162
internal sealed class emu8k_slide_t
{
    internal int32_t last;
}

// pcem: sound_emu8k.h:165-171, :173-179, :181-186, :189-194, :202-209, :211-218, :220-226, :279-284, :286-291,
//   :293-298, :300-305, :307-312 — les unions du canal.
[StructLayout(LayoutKind.Explicit)]
internal struct emu8k_cpf_u
{
    [FieldOffset(0)] internal uint32_t cpf;
    [FieldOffset(0)] internal uint16_t cpf_curr_frac_addr; /* fractional part of the playing cursor. */
    [FieldOffset(2)] internal uint16_t cpf_curr_pitch;     /* 0x4000 = no shift. Linear increment */
}

[StructLayout(LayoutKind.Explicit)]
internal struct emu8k_ptrx_u
{
    [FieldOffset(0)] internal uint32_t ptrx;
    [FieldOffset(0)] internal uint8_t ptrx_pan_aux;
    [FieldOffset(1)] internal uint8_t ptrx_revb_send;
    [FieldOffset(2)] internal uint16_t ptrx_pit_target; /* target pitch to which slide at curr_pitch speed. */
}

[StructLayout(LayoutKind.Explicit)]
internal struct emu8k_cvcf_u
{
    [FieldOffset(0)] internal uint32_t cvcf;
    [FieldOffset(0)] internal uint16_t cvcf_curr_filt_ctoff;
    [FieldOffset(2)] internal uint16_t cvcf_curr_volume;
}

[StructLayout(LayoutKind.Explicit)]
internal struct emu8k_vtft_u
{
    [FieldOffset(0)] internal uint32_t vtft;
    [FieldOffset(0)] internal uint16_t vtft_filter_target;
    [FieldOffset(2)] internal uint16_t vtft_vol_target; /* written to by the envelope engine. */
}

[StructLayout(LayoutKind.Explicit)]
internal struct emu8k_psst_u
{
    [FieldOffset(0)] internal uint32_t psst;
    [FieldOffset(0)] internal uint16_t psst_lw_address;
    [FieldOffset(2)] internal uint8_t psst_hw_address;
    [FieldOffset(3)] internal uint8_t psst_pan;
}

[StructLayout(LayoutKind.Explicit)]
internal struct emu8k_csl_u
{
    [FieldOffset(0)] internal uint32_t csl;
    [FieldOffset(0)] internal uint16_t csl_lw_address;
    [FieldOffset(2)] internal uint8_t csl_hw_address;
    [FieldOffset(3)] internal uint8_t csl_chor_send;
}

[StructLayout(LayoutKind.Explicit)]
internal struct emu8k_ccca_u
{
    [FieldOffset(0)] internal uint32_t ccca;
    [FieldOffset(0)] internal uint16_t ccca_lw_addr;
    [FieldOffset(2)] internal uint8_t ccca_hb_addr;
    [FieldOffset(3)] internal uint8_t ccca_qcontrol;
}

[StructLayout(LayoutKind.Explicit)]
internal struct emu8k_ifatn_u
{
    [FieldOffset(0)] internal uint16_t ifatn;
    [FieldOffset(0)] internal uint8_t ifatn_attenuation;
    [FieldOffset(1)] internal uint8_t ifatn_init_filter;
}

[StructLayout(LayoutKind.Explicit)]
internal struct emu8k_pefe_u
{
    [FieldOffset(0)] internal uint16_t pefe;
    [FieldOffset(0)] internal int8_t pefe_modenv_filter_height;
    [FieldOffset(1)] internal int8_t pefe_modenv_pitch_height;
}

[StructLayout(LayoutKind.Explicit)]
internal struct emu8k_fmmod_u
{
    [FieldOffset(0)] internal uint16_t fmmod;
    [FieldOffset(0)] internal int8_t fmmod_lfo1_filt_mod;
    [FieldOffset(1)] internal int8_t fmmod_lfo1_vibrato;
}

[StructLayout(LayoutKind.Explicit)]
internal struct emu8k_tremfrq_u
{
    [FieldOffset(0)] internal uint16_t tremfrq;
    [FieldOffset(0)] internal uint8_t tremfrq_lfo1_freq;
    [FieldOffset(1)] internal int8_t tremfrq_lfo1_tremolo;
}

[StructLayout(LayoutKind.Explicit)]
internal struct emu8k_fm2frq2_u
{
    [FieldOffset(0)] internal uint16_t fm2frq2;
    [FieldOffset(0)] internal uint8_t fm2frq2_lfo2_freq;
    [FieldOffset(1)] internal int8_t fm2frq2_lfo2_vibrato;
}

// pcem: sound_emu8k.h:164-341
internal sealed class emu8k_voice_t
{
    internal emu8k_cpf_u cpf_u;
    internal emu8k_ptrx_u ptrx_u;
    internal emu8k_cvcf_u cvcf_u;
    internal readonly emu8k_slide_t volumeslide = new();
    internal emu8k_vtft_u vtft_u;
    /* These registers are used at least by the Windows drivers, and seem to be resetting
     * something, similarly to targets and current, but... of what?
     * what is curious is that if they are already zero, they are not written to, so it really
     * looks like they are information about the status of the channel. (lfo position maybe?) */
    internal uint32_t unknown_data0_4;
    internal uint32_t unknown_data0_5;
    internal emu8k_psst_u psst_u;
    internal emu8k_csl_u csl_u;
    internal emu8k_ccca_u ccca_u;

    internal uint16_t envvol;
    internal uint16_t dcysusv;
    internal uint16_t envval;
    internal uint16_t dcysus;
    internal uint16_t atkhldv;
    internal uint16_t lfo1val, lfo2val;
    internal uint16_t atkhld;
    internal uint16_t ip;

    internal emu8k_ifatn_u ifatn_u;
    internal emu8k_pefe_u pefe_u;
    internal emu8k_fmmod_u fmmod_u;
    internal emu8k_tremfrq_u tremfrq_u;
    internal emu8k_fm2frq2_u fm2frq2_u;

    internal int env_engine_on;

    internal emu8k_mem_internal_t addr, loop_start, loop_end;

    internal int32_t initial_att;
    internal int32_t initial_filter;

    internal readonly emu8k_envelope_t vol_envelope = new();
    internal readonly emu8k_envelope_t mod_envelope = new();

    internal int64_t lfo1_speed, lfo2_speed;
    internal emu8k_mem_internal_t lfo1_count, lfo2_count;
    internal int32_t lfo1_delay_samples, lfo2_delay_samples;
    internal int vol_l, vol_r;

    internal int16_t fixed_modenv_filter_height;
    internal int16_t fixed_modenv_pitch_height;
    internal int16_t fixed_lfo1_filt_mod;
    internal int16_t fixed_lfo1_vibrato;
    internal int16_t fixed_lfo1_tremolo;
    internal int16_t fixed_lfo2_vibrato;

    /* filter internal data. */
    internal int filterq_idx;
    internal int32_t filt_att;
    internal readonly int64_t[] filt_buffer = new int64_t[5];
}

// pcem: sound_emu8k.h:343-376
internal sealed class emu8k_t
{
    internal readonly emu8k_voice_t[] voice = NouvellesVoix();

    internal uint16_t hwcf1, hwcf2, hwcf3;
    internal uint32_t hwcf4, hwcf5, hwcf6, hwcf7;

    internal readonly uint16_t[] init1 = new uint16_t[32], init2 = new uint16_t[32], init3 = new uint16_t[32], init4 = new uint16_t[32];

    internal uint32_t smalr, smarr, smalw, smarw;
    internal uint16_t smld_buffer, smrd_buffer;

    internal uint16_t wc;

    internal uint16_t id;

    // DEVIATION: les trois pointeurs du C (ram, rom, empty) deviennent un seul tableau, `mem` : la ROM en 0, le bloc
    //   vide en ROM_MOTS, la RAM en RAM_DEBUT ; ram_present tient lieu du test `emu8k->ram` (:330).
    internal int16_t[] mem = [];
    internal bool ram_present;

    /* RAM pointers are a way to avoid checking ram boundaries on read */
    internal readonly int[] ram_pointers = new int[0x100];
    internal uint32_t ram_end_addr;

    internal int cur_reg, cur_voice;

    internal int16_t out_l, out_r;

    internal readonly emu8k_chorus_eng_t chorus_engine = new();
    internal readonly int32_t[] chorus_in_buffer = new int32_t[MAXSOUNDBUFLEN];
    internal readonly emu8k_reverb_eng_t reverb_engine = new();
    internal readonly int32_t[] reverb_in_buffer = new int32_t[MAXSOUNDBUFLEN];

    internal int pos;
    internal readonly int32_t[] buffer = new int32_t[MAXSOUNDBUFLEN * 2];

    private static emu8k_voice_t[] NouvellesVoix()
    {
        var t = new emu8k_voice_t[32];
        for (var i = 0; i < 32; i++)
            t[i] = new emu8k_voice_t();
        return t;
    }
}

internal static class sound_emu8k
{
    // pcem: sound_emu8k.h:5-9 — All these defines are in samples, not in bytes.
    internal const uint32_t EMU8K_MEM_ADDRESS_MASK = 0xFFFFFF;
    internal const uint32_t EMU8K_RAM_MEM_START = 0x200000;
    internal const uint32_t EMU8K_FM_MEM_ADDRESS = 0xFFFFE0;
    internal const int EMU8K_RAM_POINTERS_MASK = 0x3F;
    internal const int EMU8K_LFOCHORUS_SIZE = 0x4000;

    // pcem: sound_emu8k.h:110 — 32 * 242. 32 comes from the "right" room resso case.
    internal const int MAX_REFL_SIZE = 7744;
    // iXtal26 — la taille des tampons de la réverbération : 34 × 242, le maximum que l'invité atteint (PB-161).
    internal const int REFL_TAMPON = 34 * 242;

    // La disposition de `mem` (emu8k_t) : la ROM, le bloc vide, la RAM, en mots.
    internal const int ROM_MOTS = 1024 * 1024 / 2;
    private const int BLOCK_SIZE_WORDS = 0x10000;
    internal const int RAM_DEBUT = ROM_MOTS + BLOCK_SIZE_WORDS;

    // pcem: sound_emu8k.c:87-97
    private const int ENV_STOPPED = 0;
    private const int ENV_DELAY = 1;
    private const int ENV_ATTACK = 2;
    private const int ENV_HOLD = 3;
    // ENV_DECAY   = 4,
    private const int ENV_SUSTAIN = 5;
    // ENV_RELEASE = 6,
    private const int ENV_RAMP_DOWN = 7;
    private const int ENV_RAMP_UP = 8;

    // pcem: sound_emu8k.c:99-101
    internal static int random_helper = 0;
    internal static int dmareadbit = 0;
    internal static int dmawritebit = 0;

    /* cubic and linear tables resolution. Note: higher than 10 does not improve the result. */
    // pcem: sound_emu8k.c:104-107
    private const int CUBIC_RESOLUTION_LOG = 10;
    private const int CUBIC_RESOLUTION = 1 << CUBIC_RESOLUTION_LOG;
    /* cubic_table coefficients. */
    internal static readonly float[] cubic_table = new float[CUBIC_RESOLUTION * 4];

    // pcem: sound_emu8k.c:110-124
    /* conversion from current pitch to linear frequency change (in 32.32 fixed point). */
    internal static readonly int64_t[] freqtable = new int64_t[65536];
    /* Conversion from initial attenuation to 16 bit unsigned lineal amplitude (currently only a way to update volume target register)
     */
    internal static readonly int32_t[] attentable = new int32_t[256];
    /* Conversion from envelope dbs (once rigth shifted) (0 = 0dBFS, 65535 = -96dbFS and silence ) to 16 bit unsigned lineal
     * amplitude, to convert to current volume. (0 to 65536) */
    internal static readonly int32_t[] env_vol_db_to_vol_target = new int32_t[65537];
    /* Same as above, but to convert amplitude (once rigth shifted) (0 to 65536) to db (0 = 0dBFS, 65535 = -96dbFS and silence ).
     * it is needed so that the delay, attack and hold phase can be added to initial attenuation and tremolo */
    internal static readonly int32_t[] env_vol_amplitude_to_db = new int32_t[65537];
    /* Conversion from envelope herts (once right shifted) to octave . it is needed so that the delay, attack and hold phase can be
     * added to initial pitch ,lfos pitch , initial filter and lfo filter */
    internal static readonly int32_t[] env_mod_hertz_to_octave = new int32_t[65537];
    /* Conversion from envelope amount to time in samples. */
    internal static readonly int32_t[] env_attack_to_samples = new int32_t[128];

    // pcem: sound_emu8k.c:135-142
    private static readonly int32_t[] env_decay_to_dbs_or_oct =
    {
        0,    1,    2,    3,    4,    5,    6,    7,    8,    9,    10,   11,   12,   13,  14,  15,   16,   17,   18,
        19,   20,   20,   21,   22,   23,   24,   25,   27,   28,   29,   30,   32,   33,  34,  36,   38,   39,   41,
        43,   45,   49,   51,   53,   55,   58,   60,   63,   66,   69,   72,   75,   78,  82,  85,   89,   93,   97,
        102,  106,  111,  116,  121,  126,  132,  138,  144,  150,  157,  164,  171,  179, 186, 195,  203,  212,  222,
        232,  243,  253,  264,  276,  288,  301,  315,  328,  342,  358,  374,  390,  406, 425, 444,  466,  485,  506,
        528,  553,  580,  602,  634,  660,  689,  721,  755,  780,  820,  849,  897,  932, 970, 1012, 1057, 1106, 1160,
        1219, 1285, 1321, 1399, 1441, 1534, 1585, 1640, 1698, 1829, 1902, 1981, 2068, 2162,
    };
    // omitted: env_decay_to_millis (:143-158) — en commentaire chez PCem.

    /* Table represeting the LFO waveform (signed 16bits with 32768 max int. >> 15 to move back to +/-1 range). */
    // pcem: sound_emu8k.c:161-166
    internal static readonly int32_t[] lfotable = new int32_t[65536];
    /* Table to transform the speed parameter to emu8k_mem_internal_t range. */
    internal static readonly int64_t[] lfofreqtospeed = new int64_t[256];

    /* LFO used for the chorus. a sine wave.(signed 16bits with 32768 max int. >> 15 to move back to +/-1 range). */
    internal static readonly double[] chortable = new double[65536];

    // pcem: sound_emu8k.c:168
    private const int REV_BUFSIZE_STEP = 242;

    // pcem: sound_emu8k.c:201-202 — Attenuation as above, codified in amplitude.
    private static readonly int32_t[] filter_atten =
    {
        65536, 61869, 57079, 53269, 49145, 44820, 40877, 34792,
        32845, 30653, 28607, 26392, 24630, 22463, 20487, 18470,
    };

    /*Coefficients for the filters for a defined Q and cutoff.*/
    // pcem: sound_emu8k.c:205 — `int32_t filt_coeffs[16][256][3]`, à plat.
    internal static readonly int32_t[] filt_coeffs = new int32_t[16 * 256 * 3];

    private static int fc(int q, int cutoff, int k) => (q * 256 + cutoff) * 3 + k;

    // pcem: sound_emu8k.c:207-215, :280 — READ16 : le mot bas (adresse paire) ou le mot haut.
    private static uint16_t READ16(uint16_t addr, uint32_t var) =>
        (addr & 2) == 0 ? (uint16_t)(var & 0xffff) : (uint16_t)((var >> 16) & 0xffff);

    // pcem: sound_emu8k.c:217-225, :281 — WRITE16. `(val) << 16` sur un int en C ; ici en uint32_t, mêmes bits.
    private static void WRITE16(uint16_t addr, ref uint32_t var, uint32_t val)
    {
        switch (addr & 2) {
        case 0:
                var = (var & 0xffff0000) | val;
                break;
        case 2:
                var = (var & 0x0000ffff) | (val << 16);
                break;
        }
    }

    // pcem: sound_emu8k.c:284-287 — l'union emu8k_mem_pointers_t : les bits 16 à 23 font le bloc, 0 à 15 l'adresse.
    private static int16_t EMU8K_READ(emu8k_t emu8k, uint32_t addr) =>
        emu8k.mem[emu8k.ram_pointers[(addr >> 16) & 0xFF] + (int)(addr & 0xFFFF)];

    // omitted: EMU8K_READ_INTERP_LINEAR (sound_emu8k.c:289-299) — static inline, jamais appelé (RESAMPLER_CUBIC).

    // pcem: sound_emu8k.c:301-326. Les quatre produits int × float et leur somme en float, dans l'ordre du C, puis la
    //   conversion du C vers int32_t.
    // pcem bug, reproduced: PB-160 — une interpolation cubique, et non celle à trois points de l'AWE, que nul ne
    //   publie.
    private static int32_t EMU8K_READ_INTERP_CUBIC(emu8k_t emu8k, uint32_t int_addr, uint16_t fract)
    {
        /*Since there are four floats in the table for each fraction, the position is 16byte aligned. */
        fract >>= 16 - CUBIC_RESOLUTION_LOG;
        fract <<= 2;

        /* TODO: I still have to verify how this works, but I think that
         * the card could use two oscillators (usually 31 and 32) where it would
         * be writing the OPL3 output, and to which, chorus and reverb could be applied to get
         * those effects for OPL3 sounds.*/

        /* This is cubic interpolation.
         * Not the same than 3-point interpolation, but a better approximation than linear
         * interpolation.
         * Also, it takes into account the "Note that the actual audio location is the point
         * 1 word higher than this value due to interpolation offset".
         * That's why the pointers are 0, 1, 2, 3 and not -1, 0, 1, 2 */
        int32_t dat2 = EMU8K_READ(emu8k, int_addr + 1);
        int t = fract;
        int32_t dat1 = EMU8K_READ(emu8k, int_addr);
        int32_t dat3 = EMU8K_READ(emu8k, int_addr + 2);
        int32_t dat4 = EMU8K_READ(emu8k, int_addr + 3);
        /* Note: I've ended using float for the table values to avoid some cases of integer overflow. */
        dat2 = Cpu._386.CvtI32((double)(dat1 * cubic_table[t] + dat2 * cubic_table[t + 1] + dat3 * cubic_table[t + 2] +
                                        dat4 * cubic_table[t + 3]));
        return dat2;
    }

    // pcem: sound_emu8k.c:328-340
    private static void EMU8K_WRITE(emu8k_t emu8k, uint32_t addr, uint16_t val)
    {
        addr &= EMU8K_MEM_ADDRESS_MASK;
        if (!emu8k.ram_present || addr < EMU8K_RAM_MEM_START || addr >= EMU8K_FM_MEM_ADDRESS)
                return;

        /* It looks like if an application writes to a memory part outside of the available
         * amount on the card, it wraps, and opencubicplayer uses that to detect the amount
         * of memory, as opposed to simply check at the address that it has just tried to write. */
        while (addr >= emu8k.ram_end_addr)
                addr -= emu8k.ram_end_addr - EMU8K_RAM_MEM_START;

        emu8k.mem[RAM_DEBUT + (int)(addr - EMU8K_RAM_MEM_START)] = (int16_t)val;
    }

    // pcem: sound_emu8k.c:342-716
    // pcem bug, reproduced: PB-158 — WC et les registres courants n'avancent qu'aux écritures : emu8k_inw n'appelle
    //   jamais emu8k_update (TODO :647-649).
    internal static uint16_t emu8k_inw(uint16_t addr, object p)
    {
        emu8k_t emu8k = (emu8k_t)p;
        // omitted: EMU8K_DEBUG_REGISTERS (:346-481) — #ifdef éteint.

        switch (addr & 0xF02) {
        case 0x600:
        case 0x602: /*Data0. also known as BLASTER+0x400 and EMU+0x000 */
                switch (emu8k.cur_reg) {
                case 0:
                        return READ16(addr, emu8k.voice[emu8k.cur_voice].cpf_u.cpf);

                case 1:
                        return READ16(addr, emu8k.voice[emu8k.cur_voice].ptrx_u.ptrx);

                case 2:
                        return READ16(addr, emu8k.voice[emu8k.cur_voice].cvcf_u.cvcf);

                case 3:
                        return READ16(addr, emu8k.voice[emu8k.cur_voice].vtft_u.vtft);

                case 4:
                        return READ16(addr, emu8k.voice[emu8k.cur_voice].unknown_data0_4);

                case 5:
                        return READ16(addr, emu8k.voice[emu8k.cur_voice].unknown_data0_5);

                case 6:
                        return READ16(addr, emu8k.voice[emu8k.cur_voice].psst_u.psst);

                case 7:
                        return READ16(addr, emu8k.voice[emu8k.cur_voice].csl_u.csl);
                }
                break;

        case 0xA00: /*Data1. also known as BLASTER+0x800 and EMU+0x400 */
                switch (emu8k.cur_reg) {
                case 0:
                        return READ16(addr, emu8k.voice[emu8k.cur_voice].ccca_u.ccca);

                case 1:
                        switch (emu8k.cur_voice) {
                        case 9:
                                return READ16(addr, emu8k.hwcf4);
                        case 10:
                                return READ16(addr, emu8k.hwcf5);
                                /* Actually, these two might be command words rather than registers, or some LFO position/buffer
                                 * reset.*/
                        case 13:
                                return READ16(addr, emu8k.hwcf6);
                        case 14:
                                return READ16(addr, emu8k.hwcf7);

                        case 20:
                                return READ16(addr, emu8k.smalr);
                        case 21:
                                return READ16(addr, emu8k.smarr);
                        case 22:
                                return READ16(addr, emu8k.smalw);
                        case 23:
                                return READ16(addr, emu8k.smarw);

                        case 26: {
                                uint16_t val = emu8k.smld_buffer;
                                emu8k.smld_buffer = (uint16_t)EMU8K_READ(emu8k, emu8k.smalr);
                                emu8k.smalr = (emu8k.smalr + 1) & EMU8K_MEM_ADDRESS_MASK;
                                return val;
                        }

                                /*The EMU8000 PGM describes the return values of these registers as 'a VLSI error'*/
                        // pcem bug, reproduced: PB-160 — HWCF1 à HWCF3 relus par une permutation fixe de leurs bits ;
                        //   le guide les dit illisibles (p. 14, p. 21), sans la valeur lue.
                        case 29: /*Configuration Word 1*/
                                return (uint16_t)((emu8k.hwcf1 & 0xfe) | (emu8k.hwcf3 & 0x01));
                        case 30: /*Configuration Word 2*/
                                return (uint16_t)(((emu8k.hwcf2 >> 4) & 0x0e) | (emu8k.hwcf1 & 0x01) | ((emu8k.hwcf3 & 0x02) != 0 ? 0x10 : 0) |
                                       ((emu8k.hwcf3 & 0x04) != 0 ? 0x40 : 0) | ((emu8k.hwcf3 & 0x08) != 0 ? 0x20 : 0) |
                                       ((emu8k.hwcf3 & 0x10) != 0 ? 0x80 : 0));
                        case 31: /*Configuration Word 3*/
                                return (uint16_t)(emu8k.hwcf2 & 0x1f);
                        }
                        break;

                case 2:
                        return emu8k.init1[emu8k.cur_voice];

                case 3:
                        return emu8k.init3[emu8k.cur_voice];

                case 4:
                        return emu8k.voice[emu8k.cur_voice].envvol;

                case 5:
                        return emu8k.voice[emu8k.cur_voice].dcysusv;

                case 6:
                        return emu8k.voice[emu8k.cur_voice].envval;

                case 7:
                        return emu8k.voice[emu8k.cur_voice].dcysus;
                }
                break;

        case 0xA02: /*Data2. also known as BLASTER+0x802 and EMU+0x402 */
                switch (emu8k.cur_reg) {
                case 0:
                        return READ16(addr, emu8k.voice[emu8k.cur_voice].ccca_u.ccca);

                case 1:
                        switch (emu8k.cur_voice) {
                        case 9:
                                return READ16(addr, emu8k.hwcf4);
                        case 10:
                                return READ16(addr, emu8k.hwcf5);
                                /* Actually, these two might be command words rather than registers, or some LFO position/buffer
                                 * reset. */
                        case 13:
                                return READ16(addr, emu8k.hwcf6);
                        case 14:
                                return READ16(addr, emu8k.hwcf7);

                                /* Simulating empty/full bits by unsetting it once read. */
                        // pcem bug, reproduced: PB-159 — l'adresse paire de A22h rend le mot HAUT : le bit 15 posé par
                        //   dmareadbit et dmawritebit (8000h) tombe, le drapeau ne se voit jamais.
                        case 20: {
                                uint16_t ret = READ16(addr, emu8k.smalr | (uint32_t)dmareadbit);
                                /* xor with itself to set to zero faster. */
                                dmareadbit ^= dmareadbit;
                                return ret;
                        }
                        case 21: {
                                uint16_t ret = READ16(addr, emu8k.smarr | (uint32_t)dmareadbit);
                                /* xor with itself to set to zero faster.*/
                                dmareadbit ^= dmareadbit;
                                return ret;
                        }
                        case 22: {
                                uint16_t ret = READ16(addr, emu8k.smalw | (uint32_t)dmawritebit);
                                /*xor with itself to set to zero faster.*/
                                dmawritebit ^= dmawritebit;
                                return ret;
                        }
                        case 23: {
                                uint16_t ret = READ16(addr, emu8k.smarw | (uint32_t)dmawritebit);
                                /*xor with itself to set to zero faster.*/
                                dmawritebit ^= dmawritebit;
                                return ret;
                        }

                        case 26: {
                                uint16_t val = emu8k.smrd_buffer;
                                emu8k.smrd_buffer = (uint16_t)EMU8K_READ(emu8k, emu8k.smarr);
                                emu8k.smarr = (emu8k.smarr + 1) & EMU8K_MEM_ADDRESS_MASK;
                                return val;
                        }
                                /*TODO: We need to improve the precision of this clock, since
                                 it is used by programs to wait. Not critical, but should help reduce
                                 the amount of calls and wait time */
                        case 27: /*Sample Counter ( 44Khz clock) */
                                return emu8k.wc;
                        }
                        break;

                case 2:
                        return emu8k.init2[emu8k.cur_voice];

                case 3:
                        return emu8k.init4[emu8k.cur_voice];

                case 4:
                        return emu8k.voice[emu8k.cur_voice].atkhldv;

                case 5:
                        return emu8k.voice[emu8k.cur_voice].lfo1val;

                case 6:
                        return emu8k.voice[emu8k.cur_voice].atkhld;

                case 7:
                        return emu8k.voice[emu8k.cur_voice].lfo2val;
                }
                break;

        case 0xE00: /*Data3. also known as BLASTER+0xC00 and EMU+0x800 */
                switch (emu8k.cur_reg) {
                case 0:
                        return emu8k.voice[emu8k.cur_voice].ip;

                case 1:
                        return emu8k.voice[emu8k.cur_voice].ifatn_u.ifatn;

                case 2:
                        return emu8k.voice[emu8k.cur_voice].pefe_u.pefe;

                case 3:
                        return emu8k.voice[emu8k.cur_voice].fmmod_u.fmmod;

                case 4:
                        return emu8k.voice[emu8k.cur_voice].tremfrq_u.tremfrq;

                case 5:
                        return emu8k.voice[emu8k.cur_voice].fm2frq2_u.fm2frq2;

                case 6:
                        return 0xffff;

                case 7: /*ID?*/
                        return (uint16_t)(0x1c | ((emu8k.id & 0x0002) != 0 ? 0xff02 : 0));
                }
                break;

        case 0xE02: /* Pointer. also known as BLASTER+0xC02 and EMU+0x802 */
                /* LS five bits = channel number, next 3 bits = register number
                 * and MS 8 bits = VLSI test register.
                 * Impulse tracker tests the non variability of the LS byte that it has set, and the variability
                 * of the MS byte to determine that it really is an AWE32.
                 * cubic player has a similar code, where it waits until value & 0x1000 is nonzero, and then waits again until it
                 * changes to zero.*/
                // pcem bug, reproduced: PB-160 — l'octet haut tiré d'un compteur de 80h à 9Fh ; « random (actually a
                //   VLSI test register) » selon le guide (p. 7).
                random_helper = (random_helper + 1) & 0x1F;
                return (uint16_t)(((0x80 | random_helper) << 8) | (emu8k.cur_reg << 5) | emu8k.cur_voice);
        }
        // omitted: pclog("EMU8K READ : Unknown register read…") (:713-714) — sortie pure.
        return 0xffff;
    }

    // pcem: sound_emu8k.c:718-1403
    internal static void emu8k_outw(uint16_t addr, uint16_t val, object p)
    {
        emu8k_t emu8k = (emu8k_t)p;

        /*TODO: I would like to not call this here, but i found it was needed or else cubic player would not finish opening (take
         * a looot more of time than usual). Basically, being here means that the audio is generated in the emulation thread,
         * instead of the audio thread.*/
        emu8k_update(emu8k);
        // omitted: EMU8K_DEBUG_REGISTERS (:726-795) — #ifdef éteint.

        switch (addr & 0xF02) {
        case 0x600:
        case 0x602: /*Data0. also known as BLASTER+0x400 and EMU+0x000 */
                switch (emu8k.cur_reg) {
                case 0:
                        /* The docs says that this value is constantly updating, and it should have no actual effect. Actions
                         * should be done over ptrx */
                        WRITE16(addr, ref emu8k.voice[emu8k.cur_voice].cpf_u.cpf, val);
                        return;

                case 1:
                        WRITE16(addr, ref emu8k.voice[emu8k.cur_voice].ptrx_u.ptrx, val);
                        return;

                case 2:
                        /* The docs says that this value is constantly updating, and it should have no actual effect. Actions
                         * should be done over vtft */
                        WRITE16(addr, ref emu8k.voice[emu8k.cur_voice].cvcf_u.cvcf, val);
                        return;

                case 3:
                        WRITE16(addr, ref emu8k.voice[emu8k.cur_voice].vtft_u.vtft, val);
                        return;

                case 4:
                        WRITE16(addr, ref emu8k.voice[emu8k.cur_voice].unknown_data0_4, val);
                        return;

                case 5:
                        WRITE16(addr, ref emu8k.voice[emu8k.cur_voice].unknown_data0_5, val);
                        return;

                case 6: {
                        emu8k_voice_t emu_voice = emu8k.voice[emu8k.cur_voice];
                        WRITE16(addr, ref emu_voice.psst_u.psst, val);
                        /* TODO: Should we update only on MSB update, or this could be used as some sort of hack by applications?
                         */
                        emu_voice.loop_start.int_address = emu_voice.psst_u.psst & EMU8K_MEM_ADDRESS_MASK;
                        if ((addr & 2) != 0) {
                                emu_voice.vol_l = emu_voice.psst_u.psst_pan;
                                emu_voice.vol_r = 255 - (emu_voice.psst_u.psst_pan);
                        }
                }
                        return;

                case 7:
                        WRITE16(addr, ref emu8k.voice[emu8k.cur_voice].csl_u.csl, val);
                        /* TODO: Should we update only on MSB update, or this could be used as some sort of hack by applications?
                         */
                        emu8k.voice[emu8k.cur_voice].loop_end.int_address =
                                emu8k.voice[emu8k.cur_voice].csl_u.csl & EMU8K_MEM_ADDRESS_MASK;
                        return;
                }
                break;

        case 0xA00: /*Data1. also known as BLASTER+0x800 and EMU+0x400 */
                switch (emu8k.cur_reg) {
                case 0:
                        WRITE16(addr, ref emu8k.voice[emu8k.cur_voice].ccca_u.ccca, val);
                        /* TODO: Should we update only on MSB update, or this could be used as some sort of hack by applications?
                         */
                        emu8k.voice[emu8k.cur_voice].addr.int_address =
                                emu8k.voice[emu8k.cur_voice].ccca_u.ccca & EMU8K_MEM_ADDRESS_MASK;
                        return;

                case 1:
                        switch (emu8k.cur_voice) {
                        case 9:
                                WRITE16(addr, ref emu8k.hwcf4, val);
                                return;
                        case 10:
                                WRITE16(addr, ref emu8k.hwcf5, val);
                                return;
                                /* Actually, these two might be command words rather than registers, or some LFO position/buffer
                                 * reset. */
                        case 13:
                                WRITE16(addr, ref emu8k.hwcf6, val);
                                return;
                        case 14:
                                WRITE16(addr, ref emu8k.hwcf7, val);
                                return;

                        case 20:
                                WRITE16(addr, ref emu8k.smalr, val);
                                return;
                        case 21:
                                WRITE16(addr, ref emu8k.smarr, val);
                                return;
                        case 22:
                                WRITE16(addr, ref emu8k.smalw, val);
                                return;
                        case 23:
                                WRITE16(addr, ref emu8k.smarw, val);
                                return;

                        case 26:
                                EMU8K_WRITE(emu8k, emu8k.smalw, val);
                                emu8k.smalw = (emu8k.smalw + 1) & EMU8K_MEM_ADDRESS_MASK;
                                return;

                        case 29:
                                emu8k.hwcf1 = val;
                                return;
                        case 30:
                                emu8k.hwcf2 = val;
                                return;
                        case 31:
                                emu8k.hwcf3 = val;
                                return;
                        }
                        break;

                case 2:
                        emu8k.init1[emu8k.cur_voice] = val;
                        // pcem bug, reproduced: PB-160 — le pas d'initialisation deviné par init1[0] = 03FFh.
                        /* Skip if in first/second initialization step */
                        if (emu8k.init1[0] != 0x03FF) {
                                switch (emu8k.cur_voice) {
                                case 0x3:
                                        emu8k.reverb_engine.out_mix = (int16_t)(val & 0xFF);
                                        break;
                                case 0x5: {
                                        int c;
                                        for (c = 0; c < 8; c++) {
                                                emu8k.reverb_engine.allpass[c].feedback = (val & 0xFF) / ((float)0xFF);
                                        }
                                } break;
                                case 0x7:
                                        emu8k.reverb_engine.link_return_type = (int8_t)((val == 0x8474) ? 1 : 0);
                                        break;
                                case 0xF:
                                        emu8k.reverb_engine.reflections[0].output_gain = (float)(((val & 0xF0) >> 4) / 15.0);
                                        break;
                                case 0x17:
                                        emu8k.reverb_engine.reflections[1].output_gain = (float)(((val & 0xF0) >> 4) / 15.0);
                                        break;
                                case 0x1F:
                                        emu8k.reverb_engine.reflections[2].output_gain = (float)(((val & 0xF0) >> 4) / 15.0);
                                        break;
                                case 0x9:
                                        emu8k.reverb_engine.reflections[0].feedback = (float)((val & 0xF) / 15.0);
                                        break;
                                case 0xB: // emu8k->reverb_engine.reflections[0].feedback_r =  (val&0xF)/15.0;
                                        break;
                                case 0x11:
                                        emu8k.reverb_engine.reflections[1].feedback = (float)((val & 0xF) / 15.0);
                                        break;
                                case 0x13: // emu8k->reverb_engine.reflections[1].feedback_r =  (val&0xF)/15.0;
                                        break;
                                case 0x19:
                                        emu8k.reverb_engine.reflections[2].feedback = (float)((val & 0xF) / 15.0);
                                        break;
                                case 0x1B: // emu8k->reverb_engine.reflections[2].feedback_r =  (val&0xF)/15.0;
                                        break;
                                }
                        }
                        return;

                case 3:
                        emu8k.init3[emu8k.cur_voice] = val;
                        // pcem bug, reproduced: PB-160 — le pas d'initialisation deviné par init1[0] = 03FFh.
                        /* Skip if in first/second initialization step */
                        if (emu8k.init1[0] != 0x03FF) {
                                switch (emu8k.cur_voice) {
                                case 9:
                                        emu8k.chorus_engine.feedback = (val & 0xFF);
                                        break;
                                case 12:
                                        /* Limiting this to a sane value given our buffer. */
                                        emu8k.chorus_engine.delay_samples_central = (val & 0x1FFF);
                                        break;

                                case 1:
                                        emu8k.reverb_engine.refl_in_amp = (uint8_t)(val & 0xFF);
                                        break;
                                case 3: // emu8k->reverb_engine.refl_in_amp_r = val&0xFF;
                                        break;
                                }
                        }
                        return;

                case 4:
                        emu8k.voice[emu8k.cur_voice].envvol = val;
                        emu8k.voice[emu8k.cur_voice].vol_envelope.delay_samples = ENVVOL_TO_EMU_SAMPLES(val);
                        return;

                case 5: {
                        emu8k.voice[emu8k.cur_voice].dcysusv = val;
                        emu8k_envelope_t vol_env = emu8k.voice[emu8k.cur_voice].vol_envelope;
                        int old_on = emu8k.voice[emu8k.cur_voice].env_engine_on;
                        emu8k.voice[emu8k.cur_voice].env_engine_on = DCYSUSV_GENERATOR_ENGINE_ON(val);

                        if (emu8k.voice[emu8k.cur_voice].env_engine_on != 0 &&
                            old_on != emu8k.voice[emu8k.cur_voice].env_engine_on) {
                                // pcem bug, reproduced: PB-160 — la rustine : hwcf3 forcé à 4 (le son actif).
                                if (emu8k.hwcf3 != 0x04) {
                                        /* This is a hack for some programs like Doom or cubic player 1.7 that don't initialize
                                           the hwcfg and init registers (doom does not init the card at all. only tests the cfg
                                           registers) */
                                        emu8k.hwcf3 = 0x04;
                                }

                                // reset lfos.
                                emu8k.voice[emu8k.cur_voice].lfo1_count.addr = 0;
                                emu8k.voice[emu8k.cur_voice].lfo2_count.addr = 0;
                                // Trigger envelopes
                                if (ATKHLDV_TRIGGER(emu8k.voice[emu8k.cur_voice].atkhldv)) {
                                        vol_env.value_amp_hz = 0;
                                        if (vol_env.delay_samples != 0) {
                                                vol_env.state = ENV_DELAY;
                                        } else if (vol_env.attack_amount_amp_hz == 0) {
                                                // pcem bug, reproduced: PB-160 — l'attaque 0 est « jamais » : la voix se tait.
                                                vol_env.state = ENV_STOPPED;
                                        } else {
                                                vol_env.state = ENV_ATTACK;
                                                /* TODO: Verify if "never attack" means eternal mute,
                                                * or it means skip attack, go to hold". */
                                        }
                                }

                                if (ATKHLD_TRIGGER(emu8k.voice[emu8k.cur_voice].atkhld)) {
                                        emu8k_envelope_t mod_env = emu8k.voice[emu8k.cur_voice].mod_envelope;
                                        mod_env.value_amp_hz = 0;
                                        mod_env.value_db_oct = 0;
                                        if (mod_env.delay_samples != 0) {
                                                mod_env.state = ENV_DELAY;
                                        } else if (mod_env.attack_amount_amp_hz == 0) {
                                                mod_env.state = ENV_STOPPED;
                                        } else {
                                                mod_env.state = ENV_ATTACK;
                                                /* TODO: Verify if "never attack" means eternal start,
                                                    * or it means skip attack, go to hold". */
                                        }
                                }
                        }

                        /* Converting the input in dBs to envelope value range. */
                        vol_env.sustain_value_db_oct = DCYSUSV_SUS_TO_ENV_RANGE(DCYSUSV_SUSVALUE_GET(val));
                        vol_env.ramp_amount_db_oct = env_decay_to_dbs_or_oct[DCYSUSV_DECAYRELEASE_GET(val)];
                        if (DCYSUSV_IS_RELEASE(val)) {
                                if (vol_env.state == ENV_DELAY || vol_env.state == ENV_ATTACK || vol_env.state == ENV_HOLD) {
                                        vol_env.value_db_oct = env_vol_amplitude_to_db[vol_env.value_amp_hz >> 5] << 5;
                                        if (vol_env.value_db_oct > (1 << 21))
                                                vol_env.value_db_oct = 1 << 21;
                                }

                                vol_env.state =
                                        (vol_env.value_db_oct >= vol_env.sustain_value_db_oct) ? ENV_RAMP_DOWN : ENV_RAMP_UP;
                        }
                }
                        return;

                case 6:
                        emu8k.voice[emu8k.cur_voice].envval = val;
                        emu8k.voice[emu8k.cur_voice].mod_envelope.delay_samples = ENVVAL_TO_EMU_SAMPLES(val);
                        return;

                case 7: {
                        // TODO: Look for a bug on delay (first trigger it works, next trigger it doesn't)
                        emu8k.voice[emu8k.cur_voice].dcysus = val;
                        emu8k_envelope_t mod_env = emu8k.voice[emu8k.cur_voice].mod_envelope;
                        /* Converting the input in octaves to envelope value range. */
                        mod_env.sustain_value_db_oct = DCYSUS_SUS_TO_ENV_RANGE(DCYSUS_SUSVALUE_GET(val));
                        mod_env.ramp_amount_db_oct = env_decay_to_dbs_or_oct[DCYSUS_DECAYRELEASE_GET(val)];
                        if (DCYSUS_IS_RELEASE(val)) {
                                if (mod_env.state == ENV_DELAY || mod_env.state == ENV_ATTACK || mod_env.state == ENV_HOLD) {
                                        // pcem bug, reproduced: PB-160 — `>> 9` et `<< 9`, là où l'attaque prend `>> 5` (:1831).
                                        mod_env.value_db_oct = env_mod_hertz_to_octave[mod_env.value_amp_hz >> 9] << 9;
                                        if (mod_env.value_db_oct >= (1 << 21))
                                                mod_env.value_db_oct = (1 << 21) - 1;
                                }

                                mod_env.state =
                                        (mod_env.value_db_oct >= mod_env.sustain_value_db_oct) ? ENV_RAMP_DOWN : ENV_RAMP_UP;
                        }
                }
                        return;
                }
                break;

        case 0xA02: /*Data2. also known as BLASTER+0x802 and EMU+0x402 */
                switch (emu8k.cur_reg) {
                case 0: {
                        emu8k_voice_t emu_voice = emu8k.voice[emu8k.cur_voice];
                        WRITE16(addr, ref emu_voice.ccca_u.ccca, val);
                        emu_voice.addr.int_address = emu_voice.ccca_u.ccca & EMU8K_MEM_ADDRESS_MASK;
                        uint32_t paramq = CCCA_FILTQ_GET(emu_voice.ccca_u.ccca);
                        emu_voice.filt_att = filter_atten[paramq];
                        emu_voice.filterq_idx = (int)paramq;
                }
                        return;

                case 1:
                        switch (emu8k.cur_voice) {
                        case 9:
                                WRITE16(addr, ref emu8k.hwcf4, val);
                                // pcem bug, reproduced: PB-160 — le pas d'initialisation deviné par init1[0] = 03FFh.
                                /* Skip if in first/second initialization step */
                                if (emu8k.init1[0] != 0x03FF) {
                                        /*(1/256th of a 44Khz sample) */
                                        /* clip the value to a reasonable value given our buffer */
                                        int32_t tmp = (int32_t)(emu8k.hwcf4 & 0x1FFFFF);
                                        emu8k.chorus_engine.delay_offset_samples_right = ((double)tmp) / 256.0;
                                }
                                return;
                        case 10:
                                WRITE16(addr, ref emu8k.hwcf5, val);
                                // pcem bug, reproduced: PB-160 — le pas d'initialisation deviné par init1[0] = 03FFh.
                                /* Skip if in first/second initialization step */
                                if (emu8k.init1[0] != 0x03FF) {
                                        /* The scale of this value is unknown. I've taken it as milliHz.
                                         * Another interpretation could be periods. (and so, Hz = 1/period)*/
                                        double osc_speed = emu8k.hwcf5; //*1.316;
                                        // milliHz
                                        /*milliHz to lfotable samples.*/
                                        osc_speed *= 65.536 / 44100.0;
                                        // omitted: la branche `#elif 0` (:1116-1119), « periods ».
                                        /*left shift 32bits for 32.32 fixed.point*/
                                        osc_speed *= 65536.0 * 65536.0;
                                        emu8k.chorus_engine.lfo_inc.addr = Cpu._386.CvtU64(osc_speed);
                                }
                                return;
                                /* Actually, these two might be command words rather than registers, or some LFO position/buffer
                                 * reset.*/
                        case 13:
                                WRITE16(addr, ref emu8k.hwcf6, val);
                                return;
                        case 14:
                                WRITE16(addr, ref emu8k.hwcf7, val);
                                return;

                        case 20: /*Top 8 bits are for Empty (MT) bit or non-addressable.*/
                                WRITE16(addr, ref emu8k.smalr, (uint32_t)(val & 0xFF));
                                dmareadbit = 0x8000;
                                return;
                        case 21: /*Top 8 bits are for Empty (MT) bit or non-addressable.*/
                                WRITE16(addr, ref emu8k.smarr, (uint32_t)(val & 0xFF));
                                dmareadbit = 0x8000;
                                return;
                        case 22: /*Top 8 bits are for full bit or non-addressable.*/
                                WRITE16(addr, ref emu8k.smalw, (uint32_t)(val & 0xFF));
                                return;
                        case 23: /*Top 8 bits are for full bit or non-addressable.*/
                                WRITE16(addr, ref emu8k.smarw, (uint32_t)(val & 0xFF));
                                return;

                        case 26:
                                dmawritebit = 0x8000;
                                EMU8K_WRITE(emu8k, emu8k.smarw, val);
                                // pcem bug, reproduced: PB-159 — SMARW n'est pas masqué à 24 bits (SMALW l'est, :894).
                                emu8k.smarw++;
                                return;
                        }
                        break;

                case 2:
                        emu8k.init2[emu8k.cur_voice] = val;
                        // pcem bug, reproduced: PB-160 — le pas d'initialisation deviné par init1[0] = 03FFh.
                        /* Skip if in first/second initialization step */
                        if (emu8k.init1[0] != 0x03FF) {
                                switch (emu8k.cur_voice) {
                                case 0x14: {
                                        int multip = ((val & 0xF00) >> 8) + 18;
                                        // pcem bug, not reproduced: PB-161 — les quartets Eh et Fh donnent 33 et 34 × 242, au-delà de
                                        //   MAX_REFL_SIZE (7 744) : le C écrit au-delà du tampon (R9). Ici, le tampon a 8 228 entrées.
                                        if (multip * REV_BUFSIZE_STEP > MAX_REFL_SIZE)
                                                Diag.R9.Garde("sound_emu8k.c:1164");
                                        emu8k.reverb_engine.reflections[5].bufsize = multip * REV_BUFSIZE_STEP;
                                        if ((multip + 1) * REV_BUFSIZE_STEP > MAX_REFL_SIZE)
                                                Diag.R9.Garde("sound_emu8k.c:1165");
                                        emu8k.reverb_engine.tailL.bufsize = (multip + 1) * REV_BUFSIZE_STEP;
                                        if (emu8k.reverb_engine.link_return_type == 0) {
                                                if ((multip + 1) * REV_BUFSIZE_STEP > MAX_REFL_SIZE)
                                                        Diag.R9.Garde("sound_emu8k.c:1167");
                                                emu8k.reverb_engine.tailR.bufsize = (multip + 1) * REV_BUFSIZE_STEP;
                                        }
                                } break;
                                case 0x16:
                                        if (emu8k.reverb_engine.link_return_type == 1) {
                                                int multip = ((val & 0xF00) >> 8) + 18;
                                                if ((multip + 1) * REV_BUFSIZE_STEP > MAX_REFL_SIZE)
                                                        Diag.R9.Garde("sound_emu8k.c:1173");
                                                emu8k.reverb_engine.tailR.bufsize = (multip + 1) * REV_BUFSIZE_STEP;
                                        }
                                        break;
                                case 0x7:
                                        emu8k.reverb_engine.reflections[3].output_gain = (float)(((val & 0xF0) >> 4) / 15.0);
                                        break;
                                case 0xf:
                                        emu8k.reverb_engine.reflections[4].output_gain = (float)(((val & 0xF0) >> 4) / 15.0);
                                        break;
                                case 0x17:
                                        emu8k.reverb_engine.reflections[5].output_gain = (float)(((val & 0xF0) >> 4) / 15.0);
                                        break;
                                case 0x1d: {
                                        int c;
                                        for (c = 0; c < 6; c++) {
                                                emu8k.reverb_engine.reflections[c].damp1 = (float)((val & 0xFF) / 255.0);
                                                emu8k.reverb_engine.reflections[c].damp2 = (float)((0xFF - (val & 0xFF)) / 255.0);
                                                emu8k.reverb_engine.reflections[c].filterstore = 0;
                                        }
                                        emu8k.reverb_engine.damper.damp1 = (float)((val & 0xFF) / 255.0);
                                        emu8k.reverb_engine.damper.damp2 = (float)((0xFF - (val & 0xFF)) / 255.0);
                                        emu8k.reverb_engine.damper.filterstore = 0;
                                } break;
                                case 0x1f: /* filter r */
                                        break;
                                case 0x1:
                                        emu8k.reverb_engine.reflections[3].feedback = (float)((val & 0xF) / 15.0);
                                        break;
                                case 0x3: // emu8k->reverb_engine.reflections[3].feedback_r =  (val&0xF)/15.0;
                                        break;
                                case 0x9:
                                        emu8k.reverb_engine.reflections[4].feedback = (float)((val & 0xF) / 15.0);
                                        break;
                                case 0xb: // emu8k->reverb_engine.reflections[4].feedback_r =  (val&0xF)/15.0;
                                        break;
                                case 0x11:
                                        emu8k.reverb_engine.reflections[5].feedback = (float)((val & 0xF) / 15.0);
                                        break;
                                case 0x13: // emu8k->reverb_engine.reflections[5].feedback_r =  (val&0xF)/15.0;
                                        break;
                                }
                        }
                        return;

                case 3:
                        emu8k.init4[emu8k.cur_voice] = val;
                        // pcem bug, reproduced: PB-160 — le pas d'initialisation deviné par init1[0] = 03FFh.
                        /* Skip if in first/second initialization step */
                        if (emu8k.init1[0] != 0x03FF) {
                                switch (emu8k.cur_voice) {
                                case 0x3: {
                                        int32_t samples = ((val & 0xFF) * emu8k.chorus_engine.delay_samples_central) >> 8;
                                        emu8k.chorus_engine.lfodepth_multip = samples;

                                } break;

                                case 0x1F:
                                        emu8k.reverb_engine.link_return_amp = (int16_t)(val & 0xFF);
                                        break;
                                }
                        }
                        return;

                case 4: {
                        emu8k.voice[emu8k.cur_voice].atkhldv = val;
                        emu8k_envelope_t vol_env = emu8k.voice[emu8k.cur_voice].vol_envelope;
                        vol_env.attack_samples = env_attack_to_samples[ATKHLDV_ATTACK(val)];
                        if (vol_env.attack_samples == 0) {
                                vol_env.attack_amount_amp_hz = 0;
                        } else {
                                /* Linear amplitude increase each sample. */
                                vol_env.attack_amount_amp_hz = (1 << 21) / vol_env.attack_samples;
                        }
                        vol_env.hold_samples = ATKHLDV_HOLD_TO_EMU_SAMPLES(val);
                        if (ATKHLDV_TRIGGER(val) && emu8k.voice[emu8k.cur_voice].env_engine_on != 0) {
                                /*TODO: I assume that "envelope trigger" is the same as new note
                                 * (since changing the IP can be done when modulating pitch too) */
                                emu8k.voice[emu8k.cur_voice].lfo1_count.addr = 0;
                                emu8k.voice[emu8k.cur_voice].lfo2_count.addr = 0;

                                vol_env.value_amp_hz = 0;
                                if (vol_env.delay_samples != 0) {
                                        vol_env.state = ENV_DELAY;
                                } else if (vol_env.attack_amount_amp_hz == 0) {
                                        // pcem bug, reproduced: PB-160 — l'attaque 0 est « jamais » : la voix se tait.
                                        vol_env.state = ENV_STOPPED;
                                } else {
                                        vol_env.state = ENV_ATTACK;
                                        /* TODO: Verify if "never attack" means eternal mute,
                                        * or it means skip attack, go to hold". */
                                }
                        }
                }
                        return;

                case 5:
                        emu8k.voice[emu8k.cur_voice].lfo1val = val;
                        /* TODO: verify if this is set once, or set every time. */
                        emu8k.voice[emu8k.cur_voice].lfo1_delay_samples = LFOxVAL_TO_EMU_SAMPLES(val);
                        return;

                case 6: {
                        emu8k.voice[emu8k.cur_voice].atkhld = val;
                        emu8k_envelope_t mod_env = emu8k.voice[emu8k.cur_voice].mod_envelope;
                        mod_env.attack_samples = env_attack_to_samples[ATKHLD_ATTACK(val)];
                        if (mod_env.attack_samples == 0) {
                                mod_env.attack_amount_amp_hz = 0;
                        } else {
                                /* Linear amplitude increase each sample. */
                                mod_env.attack_amount_amp_hz = (1 << 21) / mod_env.attack_samples;
                        }
                        mod_env.hold_samples = ATKHLD_HOLD_TO_EMU_SAMPLES(val);
                        if (ATKHLD_TRIGGER(val) && emu8k.voice[emu8k.cur_voice].env_engine_on != 0) {
                                mod_env.value_amp_hz = 0;
                                mod_env.value_db_oct = 0;
                                if (mod_env.delay_samples != 0) {
                                        mod_env.state = ENV_DELAY;
                                } else if (mod_env.attack_amount_amp_hz == 0) {
                                        mod_env.state = ENV_STOPPED;
                                } else {
                                        mod_env.state = ENV_ATTACK;
                                        /* TODO: Verify if "never attack" means eternal start,
                                            * or it means skip attack, go to hold". */
                                }
                        }
                }
                        return;

                case 7:
                        emu8k.voice[emu8k.cur_voice].lfo2val = val;
                        emu8k.voice[emu8k.cur_voice].lfo2_delay_samples = LFOxVAL_TO_EMU_SAMPLES(val);

                        return;
                }
                break;

        case 0xE00: /*Data3. also known as BLASTER+0xC00 and EMU+0x800 */
                switch (emu8k.cur_reg) {
                case 0:
                        emu8k.voice[emu8k.cur_voice].ip = val;
                        emu8k.voice[emu8k.cur_voice].ptrx_u.ptrx_pit_target = (uint16_t)(freqtable[val] >> 18);
                        return;

                case 1: {
                        emu8k_voice_t the_voice = emu8k.voice[emu8k.cur_voice];
                        // pcem bug, reproduced: PB-160 — la rustine d'IFATN : l'écriture ignorée sous cinq conditions.
                        if ((val & 0xFF) == 0 && the_voice.cvcf_u.cvcf_curr_volume == 0 && the_voice.vtft_u.vtft_vol_target == 0 &&
                            the_voice.dcysusv == 0x80 && the_voice.ip == 0) {
                                // Patch to avoid some clicking noises with Impulse tracker or other software that sets
                                // different values to 0 to set noteoff, but here, 0 means no attenuation = full volume.
                                return;
                        }
                        the_voice.ifatn_u.ifatn = val;
                        the_voice.initial_att = (((int32_t)the_voice.ifatn_u.ifatn_attenuation << 21) / 0xFF);
                        the_voice.vtft_u.vtft_vol_target = (uint16_t)attentable[the_voice.ifatn_u.ifatn_attenuation];

                        the_voice.initial_filter = (((int32_t)the_voice.ifatn_u.ifatn_init_filter << 21) / 0xFF);
                        if (the_voice.ifatn_u.ifatn_init_filter == 0xFF) {
                                the_voice.vtft_u.vtft_filter_target = 0xFFFF;
                        } else {
                                the_voice.vtft_u.vtft_filter_target = (uint16_t)(the_voice.initial_filter >> 5);
                        }
                }
                        return;

                case 2: {
                        emu8k_voice_t the_voice = emu8k.voice[emu8k.cur_voice];
                        the_voice.pefe_u.pefe = val;

                        int divider = (the_voice.pefe_u.pefe_modenv_filter_height < 0) ? 0x80 : 0x7F;
                        the_voice.fixed_modenv_filter_height =
                                (int16_t)(((int32_t)the_voice.pefe_u.pefe_modenv_filter_height) * 0x4000 / divider);

                        divider = (the_voice.pefe_u.pefe_modenv_pitch_height < 0) ? 0x80 : 0x7F;
                        the_voice.fixed_modenv_pitch_height = (int16_t)(((int32_t)the_voice.pefe_u.pefe_modenv_pitch_height) * 0x4000 / divider);
                }
                        return;

                case 3: {
                        emu8k_voice_t the_voice = emu8k.voice[emu8k.cur_voice];
                        the_voice.fmmod_u.fmmod = val;

                        int divider = (the_voice.fmmod_u.fmmod_lfo1_filt_mod < 0) ? 0x80 : 0x7F;
                        the_voice.fixed_lfo1_filt_mod = (int16_t)(((int32_t)the_voice.fmmod_u.fmmod_lfo1_filt_mod) * 0x4000 / divider);

                        divider = (the_voice.fmmod_u.fmmod_lfo1_vibrato < 0) ? 0x80 : 0x7F;
                        the_voice.fixed_lfo1_vibrato = (int16_t)(((int32_t)the_voice.fmmod_u.fmmod_lfo1_vibrato) * 0x4000 / divider);
                }
                        return;

                case 4: {
                        emu8k_voice_t the_voice = emu8k.voice[emu8k.cur_voice];
                        the_voice.tremfrq_u.tremfrq = val;
                        the_voice.lfo1_speed = lfofreqtospeed[the_voice.tremfrq_u.tremfrq_lfo1_freq];

                        int divider = (the_voice.tremfrq_u.tremfrq_lfo1_tremolo < 0) ? 0x80 : 0x7F;
                        the_voice.fixed_lfo1_tremolo = (int16_t)(((int32_t)the_voice.tremfrq_u.tremfrq_lfo1_tremolo) * 0x4000 / divider);
                }
                        return;

                case 5: {
                        emu8k_voice_t the_voice = emu8k.voice[emu8k.cur_voice];
                        the_voice.fm2frq2_u.fm2frq2 = val;
                        the_voice.lfo2_speed = lfofreqtospeed[the_voice.fm2frq2_u.fm2frq2_lfo2_freq];

                        int divider = (the_voice.fm2frq2_u.fm2frq2_lfo2_vibrato < 0) ? 0x80 : 0x7F;
                        the_voice.fixed_lfo2_vibrato = (int16_t)(((int32_t)the_voice.fm2frq2_u.fm2frq2_lfo2_vibrato) * 0x4000 / divider);
                }
                        return;

                case 7: /*ID? I believe that this allows applications to know if the emu is in use by another application */
                        emu8k.id = val;
                        return;
                }
                break;

        case 0xE02: /* Pointer. also known as BLASTER+0xC02 and EMU+0x802 */
                emu8k.cur_voice = (val & 31);
                emu8k.cur_reg = ((val >> 5) & 7);
                return;
        }
        // omitted: pclog("EMU8K WRITE: Unknown register write…") (:1401-1402) — sortie pure.
    }

    // pcem: sound_emu8k.c:1405-1411
    // pcem bug, reproduced: PB-158 — un octet impair rend `>> 1` (les bits 1 à 8), et non l'octet haut.
    internal static uint8_t emu8k_inb(uint16_t addr, object p)
    {
        /* Reading a single byte is a feature that at least Impulse tracker uses,
         * but only on detection code and not for odd addresses.*/
        if ((addr & 1) != 0)
                return (uint8_t)(emu8k_inw((uint16_t)(addr & ~1), p) >> 1);
        return (uint8_t)(emu8k_inw(addr, p) & 0xff);
    }

    // pcem: sound_emu8k.c:1413-1420
    // pcem bug, reproduced: PB-158 — chaque octet est écrit comme un mot entier.
    internal static void emu8k_outb(uint16_t addr, uint8_t val, object p)
    {
        /* TODO: AWE32 docs says that you cannot write in bytes, but if
         * an app were to use this implementation, the content of the LS Byte would be lost.*/
        if ((addr & 1) != 0)
                emu8k_outw((uint16_t)(addr & ~1), (uint16_t)(val << 8), p);
        else
                emu8k_outw(addr, val, p);
    }

    /* TODO: This is not a correct emulation, just a workalike implementation. */
    // pcem: sound_emu8k.c:1422-1481. Le tampon droit est chorus_buffer[EMU8K_LFOCHORUS_SIZE + i] (voir le type).
    // pcem bug, reproduced: PB-162 — le canal droit interpole avec la fraction du canal gauche, et, aux réglages
    //   extrêmes, lit sous son tampon, dans le gauche.
    // pcem bug, reproduced: PB-160 — un chorus « workalike » (le TODO ci-dessus) : le microcode de l'AWE n'est pas
    //   publié.
    internal static void emu8k_work_chorus(int32_t[] inbuf, int inoff, int32_t[] outbuf, int outoff, emu8k_chorus_eng_t engine, int count)
    {
        int pos;
        int32_t[] b = engine.chorus_buffer;
        const int R = EMU8K_LFOCHORUS_SIZE;
        for (pos = 0; pos < count; pos++) {
                double lfo_inter1 = chortable[engine.lfo_pos.int_address];
                // double lfo_inter2 = chortable[(engine->lfo_pos.int_address+1)&0xFFFF];

                double offset_lfo = lfo_inter1; //= lfo_inter1 + ((lfo_inter2-lfo_inter1)*engine->lfo_pos.fract_address/65536.0);
                offset_lfo *= engine.lfodepth_multip;

                /* Work left */
                double readdouble = (double)engine.write - (double)engine.delay_samples_central - offset_lfo;
                int read = Cpu._386.CvtI32(Math.Floor(readdouble));
                int fraction_part = Cpu._386.CvtI32((readdouble - (double)read) * 65536.0);
                int next_value = read + 1;
                if (read < 0) {
                        read += EMU8K_LFOCHORUS_SIZE;
                        if (next_value < 0)
                                next_value += EMU8K_LFOCHORUS_SIZE;
                } else if (next_value >= EMU8K_LFOCHORUS_SIZE) {
                        next_value -= EMU8K_LFOCHORUS_SIZE;
                        if (read >= EMU8K_LFOCHORUS_SIZE)
                                read -= EMU8K_LFOCHORUS_SIZE;
                }
                int32_t dat1 = b[read];
                int32_t dat2 = b[next_value];
                dat1 += ((dat2 - dat1) * fraction_part) >> 16;

                b[engine.write] = inbuf[inoff] + ((dat1 * engine.feedback) >> 8);

                /* Work right */
                readdouble = (double)engine.write - (double)engine.delay_samples_central - engine.delay_offset_samples_right -
                             offset_lfo;
                read = Cpu._386.CvtI32(Math.Floor(readdouble));
                next_value = read + 1;
                if (read < 0) {
                        read += EMU8K_LFOCHORUS_SIZE;
                        if (next_value < 0)
                                next_value += EMU8K_LFOCHORUS_SIZE;
                } else if (next_value >= EMU8K_LFOCHORUS_SIZE) {
                        next_value -= EMU8K_LFOCHORUS_SIZE;
                        if (read >= EMU8K_LFOCHORUS_SIZE)
                                read -= EMU8K_LFOCHORUS_SIZE;
                }
                int32_t dat3 = b[R + read];
                int32_t dat4 = b[R + next_value];
                dat3 += ((dat4 - dat3) * fraction_part) >> 16;

                b[R + engine.write] = inbuf[inoff] + ((dat3 * engine.feedback) >> 8);

                ++engine.write;
                engine.write %= EMU8K_LFOCHORUS_SIZE;
                engine.lfo_pos.addr += engine.lfo_inc.addr;
                engine.lfo_pos.int_address &= 0xFFFF;

                outbuf[outoff++] += dat1;
                outbuf[outoff++] += dat3;
                inoff++;
        }
    }

    // pcem: sound_emu8k.c:1483-1499. Les produits int × float en float ; la conversion du C.
    // pcem bug, reproduced: PB-163 — la conversion float → int32_t du C (cvttss2si, par CvtI32) : hors bornes,
    //   INT_MIN, même pour un dépassement positif ; .NET saturerait.
    internal static int32_t emu8k_reverb_comb_work(emu8k_reverb_combfilter_t comb, int32_t @in)
    {

        int32_t bufin;
        /* get echo */
        int32_t output = comb.reflection[comb.read_pos];
        /* apply lowpass */
        comb.filterstore = Cpu._386.CvtI32((double)((output * comb.damp2) + (comb.filterstore * comb.damp1)));
        /* appply feedback */
        bufin = Cpu._386.CvtI32((double)(@in - (comb.filterstore * comb.feedback)));
        /* store new value in delayed buffer */
        comb.reflection[comb.read_pos] = bufin;

        if (++comb.read_pos >= comb.bufsize)
                comb.read_pos = 0;

        return Cpu._386.CvtI32((double)(output * comb.output_gain));
    }

    // pcem: sound_emu8k.c:1501-1514. `-in` déborde pour INT_MIN, comme en C.
    // pcem bug, reproduced: PB-163 — les deux conversions du C (CvtI32), et `-in` qui déborde pour INT_MIN.
    internal static int32_t emu8k_reverb_diffuser_work(emu8k_reverb_combfilter_t comb, int32_t @in)
    {

        int32_t bufout = comb.reflection[comb.read_pos];
        /*diffuse*/
        int32_t bufin = Cpu._386.CvtI32((double)(-@in + (bufout * comb.feedback)));
        int32_t output = Cpu._386.CvtI32((double)(bufout - (bufin * comb.feedback)));
        /* store new value in delayed buffer */
        comb.reflection[comb.read_pos] = bufin;

        if (++comb.read_pos >= comb.bufsize)
                comb.read_pos = 0;

        return output;
    }

    // pcem: sound_emu8k.c:1516-1530
    internal static int32_t emu8k_reverb_tail_work(emu8k_reverb_combfilter_t comb, emu8k_reverb_combfilter_t[] allpasses, int a0, int32_t @in)
    {
        int32_t output = comb.reflection[comb.read_pos];
        /* store new value in delayed buffer */
        comb.reflection[comb.read_pos] = @in;

        // output = emu8k_reverb_allpass_work(&allpasses[0],output);
        output = emu8k_reverb_diffuser_work(allpasses[a0 + 1], output);
        output = emu8k_reverb_diffuser_work(allpasses[a0 + 2], output);
        // output = emu8k_reverb_allpass_work(&allpasses[3],output);

        if (++comb.read_pos >= comb.bufsize)
                comb.read_pos = 0;

        return output;
    }

    // pcem: sound_emu8k.c:1531-1535
    // pcem bug, reproduced: PB-163 — la conversion du C (CvtI32) : INT_MIN hors bornes.
    internal static int32_t emu8k_reverb_damper_work(emu8k_reverb_combfilter_t comb, int32_t @in)
    {
        /* apply lowpass */
        comb.filterstore = Cpu._386.CvtI32((double)((@in * comb.damp2) + (comb.filterstore * comb.damp1)));
        return comb.filterstore;
    }

    /* TODO: This is not a correct emulation, just a workalike implementation. */
    // pcem: sound_emu8k.c:1537-1586
    // pcem bug, reproduced: PB-160 — une réverbération « workalike » (le TODO ci-dessus) : le microcode de l'AWE n'est
    //   pas publié.
    internal static void emu8k_work_reverb(int32_t[] inbuf, int inoff, int32_t[] outbuf, int outoff, emu8k_reverb_eng_t engine, int count)
    {
        int pos;
        if (engine.link_return_type != 0) {
                for (pos = 0; pos < count; pos++) {
                        int32_t dat1, dat2, @in, in2;
                        @in = emu8k_reverb_damper_work(engine.damper, inbuf[inoff + pos]);
                        in2 = (@in * engine.refl_in_amp) >> 8;
                        dat2 = emu8k_reverb_comb_work(engine.reflections[0], in2);
                        dat2 += emu8k_reverb_comb_work(engine.reflections[1], in2);
                        dat1 = emu8k_reverb_comb_work(engine.reflections[2], in2);
                        dat2 += emu8k_reverb_comb_work(engine.reflections[3], in2);
                        dat1 += emu8k_reverb_comb_work(engine.reflections[4], in2);
                        dat2 += emu8k_reverb_comb_work(engine.reflections[5], in2);

                        dat1 += (emu8k_reverb_tail_work(engine.tailL, engine.allpass, 0, @in + dat1) *
                                 engine.link_return_amp) >>
                                8;
                        dat2 += (emu8k_reverb_tail_work(engine.tailR, engine.allpass, 4, @in + dat2) *
                                 engine.link_return_amp) >>
                                8;

                        outbuf[outoff++] += (dat1 * engine.out_mix) >> 8;
                        outbuf[outoff++] += (dat2 * engine.out_mix) >> 8;
                }
        } else {
                for (pos = 0; pos < count; pos++) {
                        int32_t dat1, dat2, @in, in2;
                        @in = emu8k_reverb_damper_work(engine.damper, inbuf[inoff + pos]);
                        in2 = (@in * engine.refl_in_amp) >> 8;
                        dat1 = emu8k_reverb_comb_work(engine.reflections[0], in2);
                        dat1 += emu8k_reverb_comb_work(engine.reflections[1], in2);
                        dat1 += emu8k_reverb_comb_work(engine.reflections[2], in2);
                        dat1 += emu8k_reverb_comb_work(engine.reflections[3], in2);
                        dat1 += emu8k_reverb_comb_work(engine.reflections[4], in2);
                        dat1 += emu8k_reverb_comb_work(engine.reflections[5], in2);
                        dat2 = dat1;

                        dat1 += (emu8k_reverb_tail_work(engine.tailL, engine.allpass, 0, @in + dat1) *
                                 engine.link_return_amp) >>
                                8;
                        dat2 += (emu8k_reverb_tail_work(engine.tailR, engine.allpass, 4, @in + dat2) *
                                 engine.link_return_amp) >>
                                8;

                        outbuf[outoff++] += (dat1 * engine.out_mix) >> 8;
                        outbuf[outoff++] += (dat2 * engine.out_mix) >> 8;
                }
        }
    }

    // pcem: sound_emu8k.c:1587-1589
    // pcem bug, reproduced: PB-160 — l'égaliseur est vide : les registres d'aigus et de graves sont sans effet.
    internal static void emu8k_work_eq(int32_t[] inoutbuf, int off, int count)
    {
        // TODO: Work EQ over buf
    }

    // pcem: sound_emu8k.c:1591-1602
    // pcem bug, reproduced: PB-160 — le volume glisse de 400h par échantillon vers sa cible : une règle de PCem, que
    //   le guide ne décrit pas.
    internal static int32_t emu8k_vol_slide(emu8k_slide_t slide, int32_t target)
    {
        if (slide.last < target) {
                slide.last += 0x400;
                if (slide.last > target)
                        slide.last = target;
        } else if (slide.last > target) {
                slide.last -= 0x400;
                if (slide.last < target)
                        slide.last = target;
        }
        return slide.last;
    }

    // pcem: sound_emu8k.c:1607-2009 (FILTER_MOOG, RESAMPLER_CUBIC). `buf` du C est (buffer, bp).
    internal static void emu8k_update(emu8k_t emu8k)
    {
        int new_pos = (sound_pos_global * 44100) / 48000;
        if (emu8k.pos >= new_pos)
                return;

        int32_t[] buf = emu8k.buffer;
        int bp;
        emu8k_voice_t emu_voice;
        int pos;
        int c;

        /* Clean the buffers since we will accumulate into them. */
        bp = emu8k.pos * 2;
        Array.Clear(buf, bp, 2 * (new_pos - emu8k.pos));
        Array.Clear(emu8k.chorus_in_buffer, emu8k.pos, new_pos - emu8k.pos);
        Array.Clear(emu8k.reverb_in_buffer, emu8k.pos, new_pos - emu8k.pos);

        /* Voices section  */
        for (c = 0; c < 32; c++) {
                emu_voice = emu8k.voice[c];
                bp = emu8k.pos * 2;

                for (pos = emu8k.pos; pos < new_pos; pos++) {
                        int32_t dat;

                        if (emu_voice.cvcf_u.cvcf_curr_volume != 0) {
                                /* Waveform oscillator */
                                dat = EMU8K_READ_INTERP_CUBIC(emu8k, emu_voice.addr.int_address, emu_voice.addr.fract_address);

                                /* Filter section */
                                if (emu_voice.filterq_idx != 0 || emu_voice.cvcf_u.cvcf_curr_filt_ctoff != 0xFFFF) {
                                        int cutoff = emu_voice.cvcf_u.cvcf_curr_filt_ctoff >> 8;
                                        int64_t coef0 = filt_coeffs[fc(emu_voice.filterq_idx, cutoff, 0)];
                                        int64_t coef1 = filt_coeffs[fc(emu_voice.filterq_idx, cutoff, 1)];
                                        int64_t coef2 = filt_coeffs[fc(emu_voice.filterq_idx, cutoff, 2)];
                                        int64_t[] fb = emu_voice.filt_buffer;
                                        /* clip at twice the range */

                                        /*move to 24bits*/
                                        dat <<= 8;

                                        dat = (int32_t)(dat - ((coef2 * fb[4]) >> 24)); /*feedback*/
                                        int64_t t1 = fb[1];
                                        fb[1] =
                                                ((dat + fb[0]) * coef0 - fb[1] * coef1) >>
                                                24;
                                        fb[1] = ClipBuffer(fb[1]);

                                        int64_t t2 = fb[2];
                                        fb[2] =
                                                ((fb[1] + t1) * coef0 - fb[2] * coef1) >>
                                                24;
                                        fb[2] = ClipBuffer(fb[2]);

                                        int64_t t3 = fb[3];
                                        fb[3] =
                                                ((fb[2] + t2) * coef0 - fb[3] * coef1) >>
                                                24;
                                        fb[3] = ClipBuffer(fb[3]);

                                        fb[4] =
                                                ((fb[3] + t3) * coef0 - fb[4] * coef1) >>
                                                24;
                                        fb[4] = ClipBuffer(fb[4]);

                                        fb[0] = ClipBuffer(dat);

                                        dat = (int32_t)(fb[4] >> 8);
                                        if (dat > 32767) {
                                                dat = 32767;
                                        } else if (dat < -32768) {
                                                dat = -32768;
                                        }
                                }
                                if ((emu8k.hwcf3 & 0x04) != 0 && !CCCA_DMA_ACTIVE(emu_voice.ccca_u.ccca)) {
                                        /*volume and pan*/
                                        // pcem bug, reproduced: PB-160 — en cubique non filtré, |dat| va jusqu'à 40 960 : le
                                        //   produit par le volume déborde l'int32_t, enveloppé comme en C.
                                        dat = (dat * emu_voice.cvcf_u.cvcf_curr_volume) >> 16;

                                        buf[bp++] += (dat * emu_voice.vol_l) >> 8;
                                        buf[bp++] += (dat * emu_voice.vol_r) >> 8;

                                        /* Effects section */
                                        if (emu_voice.ptrx_u.ptrx_revb_send > 0) {
                                                emu8k.reverb_in_buffer[pos] += (dat * emu_voice.ptrx_u.ptrx_revb_send) >> 8;
                                        }
                                        if (emu_voice.csl_u.csl_chor_send > 0) {
                                                emu8k.chorus_in_buffer[pos] += (dat * emu_voice.csl_u.csl_chor_send) >> 8;
                                        }
                                }
                        }

                        if (emu_voice.env_engine_on != 0) {
                                int32_t attenuation = emu_voice.initial_att;
                                int32_t filtercut = emu_voice.initial_filter;
                                int32_t currentpitch = emu_voice.ip;
                                /* run envelopes */
                                emu8k_envelope_t volenv = emu_voice.vol_envelope;
                                switch (volenv.state) {
                                case ENV_DELAY:
                                        volenv.delay_samples--;
                                        if (volenv.delay_samples <= 0) {
                                                volenv.state = ENV_ATTACK;
                                                volenv.delay_samples = 0;
                                        }
                                        attenuation = 0x1FFFFF;
                                        break;

                                case ENV_ATTACK:
                                        /* Attack amount is in linear amplitude */
                                        volenv.value_amp_hz += volenv.attack_amount_amp_hz;
                                        if (volenv.value_amp_hz >= (1 << 21)) {
                                                volenv.value_amp_hz = 1 << 21;
                                                volenv.value_db_oct = 0;
                                                if (volenv.hold_samples != 0) {
                                                        volenv.state = ENV_HOLD;
                                                } else {
                                                        /* RAMP_UP since db value is inverted and it is 0 at this point. */
                                                        volenv.state = ENV_RAMP_UP;
                                                }
                                        }
                                        attenuation += env_vol_amplitude_to_db[volenv.value_amp_hz >> 5] << 5;
                                        break;

                                case ENV_HOLD:
                                        volenv.hold_samples--;
                                        if (volenv.hold_samples <= 0) {
                                                volenv.state = ENV_RAMP_UP;
                                        }
                                        attenuation += volenv.value_db_oct;
                                        break;

                                case ENV_RAMP_DOWN:
                                        /* Decay/release amount is in fraction of dBs and is always positive */
                                        volenv.value_db_oct -= volenv.ramp_amount_db_oct;
                                        if (volenv.value_db_oct <= volenv.sustain_value_db_oct) {
                                                volenv.value_db_oct = volenv.sustain_value_db_oct;
                                                volenv.state = ENV_SUSTAIN;
                                        }
                                        attenuation += volenv.value_db_oct;
                                        break;

                                case ENV_RAMP_UP:
                                        /* Decay/release amount is in fraction of dBs and is always positive */
                                        volenv.value_db_oct += volenv.ramp_amount_db_oct;
                                        if (volenv.value_db_oct >= volenv.sustain_value_db_oct) {
                                                volenv.value_db_oct = volenv.sustain_value_db_oct;
                                                volenv.state = ENV_SUSTAIN;
                                        }
                                        attenuation += volenv.value_db_oct;
                                        break;

                                case ENV_SUSTAIN:
                                        attenuation += volenv.value_db_oct;
                                        break;

                                case ENV_STOPPED:
                                        attenuation = 0x1FFFFF;
                                        break;
                                }

                                emu8k_envelope_t modenv = emu_voice.mod_envelope;
                                switch (modenv.state) {
                                case ENV_DELAY:
                                        modenv.delay_samples--;
                                        if (modenv.delay_samples <= 0) {
                                                modenv.state = ENV_ATTACK;
                                                modenv.delay_samples = 0;
                                        }
                                        break;

                                case ENV_ATTACK:
                                        /* Attack amount is in linear amplitude */
                                        modenv.value_amp_hz += modenv.attack_amount_amp_hz;
                                        // pcem bug, reproduced: PB-164 — l'indice dépasse la table (65 537 entrées) avant le
                                        //   bornage qui suit : en C, une lecture hors table aussitôt écrasée (:1834). DEVIATION de
                                        //   forme : l'indice borné, le résultat identique (PLAN-G12.md, décision n° 15).
                                        modenv.value_db_oct = env_mod_hertz_to_octave[Math.Min(modenv.value_amp_hz >> 5, 0x10000)] << 5;
                                        if (modenv.value_amp_hz >= (1 << 21)) {
                                                modenv.value_amp_hz = 1 << 21;
                                                modenv.value_db_oct = 1 << 21;
                                                if (modenv.hold_samples != 0) {
                                                        modenv.state = ENV_HOLD;
                                                } else {
                                                        modenv.state = ENV_RAMP_DOWN;
                                                }
                                        }
                                        break;

                                case ENV_HOLD:
                                        modenv.hold_samples--;
                                        // pcem bug, reproduced: PB-160 — au bout du maintien, RAMP_UP alors que la valeur est au
                                        //   sommet : elle est posée au palier au tic suivant, sans décroissance.
                                        if (modenv.hold_samples <= 0) {
                                                modenv.state = ENV_RAMP_UP;
                                        }
                                        break;

                                case ENV_RAMP_DOWN:
                                        /* Decay/release amount is in fraction of octave and is always positive */
                                        modenv.value_db_oct -= modenv.ramp_amount_db_oct;
                                        if (modenv.value_db_oct <= modenv.sustain_value_db_oct) {
                                                modenv.value_db_oct = modenv.sustain_value_db_oct;
                                                modenv.state = ENV_SUSTAIN;
                                        }
                                        break;

                                case ENV_RAMP_UP:
                                        /* Decay/release amount is in fraction of octave and is always positive */
                                        modenv.value_db_oct += modenv.ramp_amount_db_oct;
                                        if (modenv.value_db_oct >= modenv.sustain_value_db_oct) {
                                                modenv.value_db_oct = modenv.sustain_value_db_oct;
                                                modenv.state = ENV_SUSTAIN;
                                        }
                                        break;
                                }

                                /* run lfos */
                                // pcem bug, reproduced: PB-160 — les délais des LFO, de l'enveloppe et le maintien sont décomptés
                                //   sur la valeur programmée : une note redéclenchée sans réécrire ces registres n'en a plus.
                                if (emu_voice.lfo1_delay_samples != 0) {
                                        emu_voice.lfo1_delay_samples--;
                                } else {
                                        emu_voice.lfo1_count.addr += (uint64_t)emu_voice.lfo1_speed;
                                        emu_voice.lfo1_count.int_address &= 0xFFFF;
                                }
                                if (emu_voice.lfo2_delay_samples != 0) {
                                        emu_voice.lfo2_delay_samples--;
                                } else {
                                        emu_voice.lfo2_count.addr += (uint64_t)emu_voice.lfo2_speed;
                                        emu_voice.lfo2_count.int_address &= 0xFFFF;
                                }

                                if (emu_voice.fixed_modenv_pitch_height != 0) {
                                        /* modenv range 1<<21, pitch height range 1<<14 desired range 0x1000 (+/-one octave) */
                                        currentpitch +=
                                                ((modenv.value_db_oct >> 9) * emu_voice.fixed_modenv_pitch_height) >> 14;
                                }

                                if (emu_voice.fixed_lfo1_vibrato != 0) {
                                        /* table range 1<<15, pitch mod range 1<<14 desired range 0x1000 (+/-one octave) */
                                        int32_t lfo1_vibrato =
                                                (lfotable[emu_voice.lfo1_count.int_address] * emu_voice.fixed_lfo1_vibrato) >>
                                                17;
                                        currentpitch += lfo1_vibrato;
                                }
                                if (emu_voice.fixed_lfo2_vibrato != 0) {
                                        /* table range 1<<15, pitch mod range 1<<14 desired range 0x1000 (+/-one octave) */
                                        int32_t lfo2_vibrato =
                                                (lfotable[emu_voice.lfo2_count.int_address] * emu_voice.fixed_lfo2_vibrato) >>
                                                17;
                                        currentpitch += lfo2_vibrato;
                                }

                                if (emu_voice.fixed_modenv_filter_height != 0) {
                                        /* modenv range 1<<21, pitch height range 1<<14 desired range 0x200000 (+/-full filter
                                         * range) */
                                        filtercut += ((modenv.value_db_oct >> 9) * emu_voice.fixed_modenv_filter_height) >> 5;
                                }

                                if (emu_voice.fixed_lfo1_filt_mod != 0) {
                                        /* table range 1<<15, pitch mod range 1<<14 desired range 0x100000 (+/-three octaves) */
                                        int32_t lfo1_filtmod =
                                                (lfotable[emu_voice.lfo1_count.int_address] * emu_voice.fixed_lfo1_filt_mod) >>
                                                9;
                                        filtercut += lfo1_filtmod;
                                }

                                if (emu_voice.fixed_lfo1_tremolo != 0) {
                                        /* table range 1<<15, pitch mod range 1<<14 desired range 0x40000 (+/-12dBs). */
                                        int32_t lfo1_tremolo =
                                                (lfotable[emu_voice.lfo1_count.int_address] * emu_voice.fixed_lfo1_tremolo) >>
                                                11;
                                        attenuation += lfo1_tremolo;
                                }

                                if (currentpitch > 0xFFFF)
                                        currentpitch = 0xFFFF;
                                if (currentpitch < 0)
                                        currentpitch = 0;
                                if (attenuation > 0x1FFFFF)
                                        attenuation = 0x1FFFFF;
                                if (attenuation < 0)
                                        attenuation = 0;
                                if (filtercut > 0x1FFFFF)
                                        filtercut = 0x1FFFFF;
                                if (filtercut < 0)
                                        filtercut = 0;

                                emu_voice.vtft_u.vtft_vol_target = (uint16_t)env_vol_db_to_vol_target[attenuation >> 5];
                                emu_voice.vtft_u.vtft_filter_target = (uint16_t)(filtercut >> 5);
                                emu_voice.ptrx_u.ptrx_pit_target = (uint16_t)(freqtable[currentpitch] >> 18);
                        }
                        /*
                        I've recopilated these sentences to get an idea of how to loop

                        - Set its PSST register and its CLS register to zero to cause no loops to occur.
                        -Setting the Loop Start Offset and the Loop End Offset to the same value, will cause the oscillator to
                        loop the entire memory.

                        -Setting the PlayPosition greater than the Loop End Offset, will cause the oscillator to play in reverse,
                        back to the Loop End Offset. It's pretty neat, but appears to be uncontrollable (the rate at which the
                        samples are played in reverse).

                        -Note that due to interpolator offset, the actual loop point is one greater than the start address
                        -Note that due to interpolator offset, the actual loop point will end at an address one greater than the
                        loop address -Note that the actual audio location is the point 1 word higher than this value due to
                        interpolation offset -In programs that use the awe, they generally set the loop address as "loopaddress
                        -1" to compensate for the above. (Note: I am already using address+1 in the interpolators so these things
                        are already as they should.)
                        */
                        emu_voice.addr.addr += ((uint64_t)emu_voice.cpf_u.cpf_curr_pitch) << 18;
                        if (emu_voice.addr.addr >= emu_voice.loop_end.addr) {
                                emu_voice.addr.int_address -=
                                        (emu_voice.loop_end.int_address - emu_voice.loop_start.int_address);
                                emu_voice.addr.int_address &= EMU8K_MEM_ADDRESS_MASK;
                        }

                        /* TODO: How and when are the target and current values updated */
                        // pcem bug, reproduced: PB-160 — la hauteur et la coupure posées à leur cible à chaque
                        //   échantillon, le volume par emu8k_vol_slide : le guide ne le décrit pas.
                        emu_voice.cpf_u.cpf_curr_pitch = emu_voice.ptrx_u.ptrx_pit_target;
                        emu_voice.cvcf_u.cvcf_curr_volume = (uint16_t)emu8k_vol_slide(emu_voice.volumeslide, emu_voice.vtft_u.vtft_vol_target);
                        emu_voice.cvcf_u.cvcf_curr_filt_ctoff = emu_voice.vtft_u.vtft_filter_target;
                }

                /* Update EMU voice registers. */
                emu_voice.ccca_u.ccca = (((uint32_t)emu_voice.ccca_u.ccca_qcontrol) << 24) | emu_voice.addr.int_address;
                emu_voice.cpf_u.cpf_curr_frac_addr = emu_voice.addr.fract_address;
        }

        bp = emu8k.pos * 2;
        emu8k_work_reverb(emu8k.reverb_in_buffer, emu8k.pos, buf, bp, emu8k.reverb_engine, new_pos - emu8k.pos);
        emu8k_work_chorus(emu8k.chorus_in_buffer, emu8k.pos, buf, bp, emu8k.chorus_engine, new_pos - emu8k.pos);
        emu8k_work_eq(buf, bp, new_pos - emu8k.pos);

        // Clip signal
        for (pos = emu8k.pos; pos < new_pos; pos++) {
                if (buf[bp] < -32768)
                        buf[bp] = -32768;
                else if (buf[bp] > 32767)
                        buf[bp] = 32767;

                if (buf[bp + 1] < -32768)
                        buf[bp + 1] = -32768;
                else if (buf[bp + 1] > 32767)
                        buf[bp + 1] = 32767;

                bp += 2;
        }

        /* Update EMU clock. */
        emu8k.wc = (uint16_t)(emu8k.wc + (new_pos - emu8k.pos));

        emu8k.pos = new_pos;
    }

    // pcem: sound_emu8k.c:1647 — `#define ClipBuffer(buf) (buf < -16777216) ? -16777216 : (buf > 16777216) ? 16777216 : buf`.
    private static int64_t ClipBuffer(int64_t b) => (b < -16777216) ? -16777216 : (b > 16777216) ? 16777216 : b;

    // pcem: sound_emu8k.h:235-272 — les macros des registres d'enveloppe. ENVVOL_TO_EMU_SAMPLES et ses sœurs n'ont pas de
    //   parenthèses extérieures : elles ne sont employées qu'en affectation simple, où la précédence est sans effet.
    private static int32_t ENVVOL_TO_EMU_SAMPLES(uint16_t envvol) => (envvol & 0x8000) != 0 ? 0 : ((0x8000 - (envvol & 0x7FFF)) << 5);
    private static int DCYSUSV_GENERATOR_ENGINE_ON(uint16_t dcysusv) => (dcysusv & 0x0080) == 0 ? 1 : 0;
    private static bool DCYSUSV_IS_RELEASE(uint16_t dcysusv) => (dcysusv & 0x8000) != 0;
    private static int DCYSUSV_SUSVALUE_GET(uint16_t dcysusv) => (dcysusv >> 8) & 0x7F;
    /* Inverting the range compared to documentation because the envelope runs from 0dBFS = 0 to -96dBFS = (1 <<21) */
    private static int32_t DCYSUSV_SUS_TO_ENV_RANGE(int susvalue) => ((0x7F - susvalue) << 21) / 0x7F;
    private static int DCYSUSV_DECAYRELEASE_GET(uint16_t dcysusv) => dcysusv & 0x7F;
    private static int32_t ENVVAL_TO_EMU_SAMPLES(uint16_t envval) => (envval & 0x8000) != 0 ? 0 : ((0x8000 - (envval & 0x7FFF)) << 5);
    private static bool DCYSUS_IS_RELEASE(uint16_t dcysus) => (dcysus & 0x8000) != 0;
    private static int DCYSUS_SUSVALUE_GET(uint16_t dcysus) => (dcysus >> 8) & 0x7F;
    private static int32_t DCYSUS_SUS_TO_ENV_RANGE(int susvalue) => (susvalue << 21) / 0x7F;
    private static int DCYSUS_DECAYRELEASE_GET(uint16_t dcysus) => dcysus & 0x7F;
    private static bool ATKHLDV_TRIGGER(uint16_t atkhldv) => (atkhldv & 0x8000) == 0;
    private static int32_t ATKHLDV_HOLD_TO_EMU_SAMPLES(uint16_t atkhldv) => 4096 * (0x7F - ((atkhldv >> 8) & 0x7F));
    private static int ATKHLDV_ATTACK(uint16_t atkhldv) => atkhldv & 0x7F;
    private static int32_t LFOxVAL_TO_EMU_SAMPLES(uint16_t lfoxval) => (lfoxval & 0x8000) != 0 ? 0 : ((0x8000 - (lfoxval & 0x7FFF)) << 5);
    private static bool ATKHLD_TRIGGER(uint16_t atkhld) => (atkhld & 0x8000) == 0;
    private static int32_t ATKHLD_HOLD_TO_EMU_SAMPLES(uint16_t atkhld) => 4096 * (0x7F - ((atkhld >> 8) & 0x7F));
    private static int ATKHLD_ATTACK(uint16_t atkhld) => atkhld & 0x7F;
    // pcem: sound_emu8k.h:227-232
    private static uint32_t CCCA_FILTQ_GET(uint32_t ccca) => ccca >> 28;
    private static bool CCCA_DMA_ACTIVE(uint32_t ccca) => (ccca & 0x04000000) != 0;

    /* onboard_ram in kilobytes */
    // pcem: sound_emu8k.c:2011-2232. La ROM : pc.check_sndcard l'a exigée, de 1 048 576 octets (PLAN-G12.md, décision
    //   n° 5) ; le fatal de :2019 n'est donc jamais atteint, le refus avant le montage tient lieu de garde.
    internal static void emu8k_init(emu8k_t emu8k, uint16_t emu_addr, int onboard_ram)
    {
        int c;

        var rom = new byte[1024 * 1024];
        using (var f = Flash.rom.romfopen("awe32.raw", "rb") ?? throw new InvalidOperationException("AWE32.RAW not found"))
                f.ReadExactly(rom);

        int ram_mots = 0;
        if (onboard_ram != 0) {
                /*Clip to 28MB, since that's the max that we can address. */
                if (onboard_ram > 0x7000)
                        onboard_ram = 0x7000;
                ram_mots = onboard_ram * 1024 / 2;
        }
        // La ROM, le bloc vide (zéros, :2031-2032) et la RAM (zéros, :2047) : `new` zéro-initialise.
        emu8k.mem = new int16_t[RAM_DEBUT + ram_mots];
        Buffer.BlockCopy(rom, 0, emu8k.mem, 0, rom.Length);
        /*AWE-DUMP creates ROM images offset by 2 bytes, so if we detect this
          then correct it*/
        if ((uint16_t)emu8k.mem[3] == 0x314d && (uint16_t)emu8k.mem[4] == 0x474d) {
                Array.Copy(emu8k.mem, 1, emu8k.mem, 0, (1024 * 1024 - 2) / 2);
                emu8k.mem[0x7ffff] = 0;
        }

        int j = 0;
        for (; j < 0x8; j++) {
                emu8k.ram_pointers[j] = j * BLOCK_SIZE_WORDS;
        }
        for (; j < 0x20; j++) {
                emu8k.ram_pointers[j] = ROM_MOTS;
        }

        if (onboard_ram != 0) {
                emu8k.ram_present = true;
                int i_end = onboard_ram >> 7;
                int i = 0;
                for (; i < i_end; i++, j++) {
                        emu8k.ram_pointers[j] = RAM_DEBUT + i * BLOCK_SIZE_WORDS;
                }
                emu8k.ram_end_addr = EMU8K_RAM_MEM_START + (uint32_t)(onboard_ram << 9);
        } else {
                emu8k.ram_present = false;
                emu8k.ram_end_addr = EMU8K_RAM_MEM_START;
        }
        for (; j < 0x100; j++) {
                emu8k.ram_pointers[j] = ROM_MOTS;
        }

        io_sethandler(emu_addr, 0x0004, emu8k_inb, emu8k_inw, null, emu8k_outb, emu8k_outw, null, emu8k);
        io_sethandler((uint16_t)(emu_addr + 0x400), 0x0004, emu8k_inb, emu8k_inw, null, emu8k_outb, emu8k_outw, null, emu8k);
        io_sethandler((uint16_t)(emu_addr + 0x800), 0x0004, emu8k_inb, emu8k_inw, null, emu8k_outb, emu8k_outw, null, emu8k);

        tables_init();

        /* NOTE! read_pos and buffer content is implicitly initialized to zero by the sb_t structure memset on sb_awe32_init() */
        emu8k.reverb_engine.reflections[0].bufsize = 2 * REV_BUFSIZE_STEP;
        emu8k.reverb_engine.reflections[1].bufsize = 4 * REV_BUFSIZE_STEP;
        emu8k.reverb_engine.reflections[2].bufsize = 8 * REV_BUFSIZE_STEP;
        emu8k.reverb_engine.reflections[3].bufsize = 13 * REV_BUFSIZE_STEP;
        emu8k.reverb_engine.reflections[4].bufsize = 19 * REV_BUFSIZE_STEP;
        emu8k.reverb_engine.reflections[5].bufsize = 26 * REV_BUFSIZE_STEP;

        /*This is a bit random.*/
        for (c = 0; c < 4; c++) {
                emu8k.reverb_engine.allpass[3 - c].feedback = 0.5f;
                emu8k.reverb_engine.allpass[3 - c].bufsize = (4 * c) * REV_BUFSIZE_STEP + 55;
                emu8k.reverb_engine.allpass[7 - c].feedback = 0.5f;
                emu8k.reverb_engine.allpass[7 - c].bufsize = (4 * c) * REV_BUFSIZE_STEP + 55;
        }

        /* Even when the documentation says that this has to be written by applications to initialize the card,
         * several applications and drivers ( aweman on windows, linux oss driver..) read it to detect an AWE card. */
        emu8k.hwcf1 = 0x59;
        emu8k.hwcf2 = 0x20;
        /* Initial state is muted. 0x04 is unmuted. */
        emu8k.hwcf3 = 0x00;
    }

    // pcem: sound_emu8k.c:2066-2199, :2216-2225 — les tables de emu8k_init, dans leur ordre. Séparées de emu8k_init pour
    //   emu8k-tables-check, qui les compare à celles de l'oracle sans monter de carte. exp2 n'a pas d'équivalent .NET
    //   direct : Math.Pow(2, x), une autre fonction de la glibc (un ulp d'écart sur 14 des 65 536 valeurs, effacé par la
    //   troncature ; mesuré, emu8k-tables-check le prouve). sqrt(1.09018) et log(0.5), pliés par GCC à la compilation,
    //   sont calculés ici à l'exécution (arrondis corrects, même valeur).
    internal static void tables_init()
    {
        int c;
        double @out;

        /*Create frequency table. (Convert initial pitch register value to a linear speed change)
         * The input is encoded such as 0xe000 is center note (no pitch shift)
         * and from then on , changing up or down 0x1000 (4096) increments/decrements an octave.
         * Note that this is in reference to the 44.1Khz clock that the channels play at.
         * The 65536 * 65536 is in order to left-shift the 32bit value to a 64bit value as a 32.32 fixed point.
         */
        for (c = 0; c < 0x10000; c++) {
                freqtable[c] = (int64_t)Cpu._386.CvtU64(Math.Pow(2.0, (double)(c - 0xe000) / 4096.0) * 65536.0 * 65536.0);
        }
        /* Shortcut: minimum pitch equals stopped. I don't really know if this is true, but it's better
         * since some programs set the pitch to 0 for unused channels. */
        freqtable[0] = 0;

        /* starting at 65535 because it is used for "volume target" register conversion. */
        @out = 65535.0;
        for (c = 0; c < 256; c++) {
                attentable[c] = Cpu._386.CvtI32(@out);
                @out /= Math.Sqrt(1.09018); /*0.375 dB steps*/
        }
        /* Shortcut: max attenuation is silent, not -96dB. */
        attentable[255] = 0;

        /* Note: these two tables have "db" inverted: 0 dB is max volume, 65535 "db" (-96.32dBFS) is silence.
         * Important: Using 65535 as max output value because this is intended to be used with the volume target register! */
        @out = 65535.0;
        for (c = 0; c < 0x10000; c++) {
                // double db = -(c*6.0205999/65535.0)*16.0;
                // out = powf(10.f,db/20.f) * 65536.0;
                env_vol_db_to_vol_target[c] = Cpu._386.CvtI32(@out);
                /* calculated from the 65536th root of 65536 */
                @out /= 1.00016923970;
        }
        /* Shortcut: max attenuation is silent, not -96dB. */
        env_vol_db_to_vol_target[0x10000 - 1] = 0;
        /* One more position to accept max value being 65536. */
        env_vol_db_to_vol_target[0x10000] = 0;

        for (c = 1; c < 0x10000; c++) {
                @out = -680.32142884264 * 20.0 * Math.Log10(((double)c) / 65535.0);
                env_vol_amplitude_to_db[c] = Cpu._386.CvtI32(@out);
        }
        /*Shortcut: max attenuation is silent, not -96dB.*/
        env_vol_amplitude_to_db[0] = 65535;
        /* One more position to accept max value being 65536. */
        env_vol_amplitude_to_db[0x10000] = 0;

        for (c = 1; c < 0x10000; c++) {
                @out = Math.Log2((((double)c) / 0x10000) + 1.0) * 65536.0;
                env_mod_hertz_to_octave[c] = Cpu._386.CvtI32(@out);
        }
        /*No hertz change, no octave change. */
        env_mod_hertz_to_octave[0] = 0;
        /* One more position to accept max value being 65536. */
        env_mod_hertz_to_octave[0x10000] = 65536;

        /* This formula comes from vince vu/judge dredd's awe32p10 and corresponds to what the freebsd/linux AWE32 driver has. */
        float millis;
        for (c = 0; c < 128; c++) {
                if (c == 0)
                        millis = 0; /* This means never attack. */
                else if (c < 32)
                        millis = (float)(11878.0 / c);
                else
                        millis = (float)(360 * Math.Exp((c - 32) / (16.0 / Math.Log(1.0 / 2.0))));

                env_attack_to_samples[c] = Cpu._386.CvtI32(44.1 * millis);
                /* This is an alternate formula with linear increments, but probably incorrect:
                 * millis = (256+4096*(0x7F-c)) */
        }

        /* The LFOs use a triangular waveform starting at zero and going 1/-1/1/-1.
         * This table is stored in signed 16bits precision, with a period of 65536 samples */
        for (c = 0; c < 65536; c++) {
                int d = (c + 16384) & 65535;
                if (d >= 32768)
                        lfotable[c] = 32768 + ((32768 - d) * 2);
                else
                        lfotable[c] = (d * 2) - 32768;
        }
        /* The 65536 * 65536 is in order to left-shift the 32bit value to a 64bit value as a 32.32 fixed point. */
        @out = 0.01;
        for (c = 0; c < 256; c++) {
                lfofreqtospeed[c] = (int64_t)Cpu._386.CvtU64(@out * 65536.0 / 44100.0 * 65536.0 * 65536.0);
                @out += 0.042;
        }

        for (c = 0; c < 65536; c++) {
                chortable[c] = Math.Sin(c * Math.PI / 32768.0);
        }

        /* Filter coefficients tables. Note: Values are multiplied by *16777216 to left shift 24 bits. (i.e. 8.24 fixed point) */
        int qidx;
        for (qidx = 0; qidx < 16; qidx++) {
                @out = 125.0; /* Start at 125Hz */
                for (c = 0; c < 256; c++) {
                        // FILTER_MOOG (:2171-2180)
                        float w0 = (float)Math.Sin(2.0 * Math.PI * @out / 44100.0);
                        float q_factor = 1.0f - w0;
                        float p = w0 + 0.8f * w0 * q_factor;
                        float f = p + p - 1.0f;
                        float resonance = (float)((1.0 - Math.Pow(2.0, -qidx * 24.0 / 90.0)) * 0.8);
                        float q = resonance * (1.0f + 0.5f * q_factor * (w0 + 5.6f * q_factor * q_factor));
                        filt_coeffs[fc(qidx, c, 0)] = Cpu._386.CvtI32(p * 16777216.0);
                        filt_coeffs[fc(qidx, c, 1)] = Cpu._386.CvtI32(f * 16777216.0);
                        filt_coeffs[fc(qidx, c, 2)] = Cpu._386.CvtI32(q * 16777216.0);
                        /* 42.66 divisions per octave (the doc says quarter seminotes which is 48, but then it would be almost an
                         * octave less) */
                        @out *= 1.016378315;
                        /* 42 divisions. This moves the max frequency to 8.5Khz.*/
                        // out *= 1.0166404394;
                        /* This is a linear increment method, that corresponds to the NRPN table, but contradicts the EMU8KPRM
                         * doc: */
                        // out = 100.0 + (c+1.0)*31.25; //31.25Hz steps */
                }
        }

        /* Cubic Resampling  ( 4point cubic spline) */
        double resdouble = 1.0 / (double)CUBIC_RESOLUTION;
        for (c = 0; c < CUBIC_RESOLUTION; c++) {
                double x = (double)c * resdouble;
                /* Cubic resolution is made of four table, but I've put them all in one table to optimize memory access. */
                cubic_table[c * 4] = (float)(-0.5 * x * x * x + x * x - 0.5 * x);
                cubic_table[c * 4 + 1] = (float)(1.5 * x * x * x - 2.5 * x * x + 1.0);
                cubic_table[c * 4 + 2] = (float)(-1.5 * x * x * x + 2.0 * x * x + 0.5 * x);
                cubic_table[c * 4 + 3] = (float)(0.5 * x * x * x - 0.5 * x * x);
        }
    }

    // iXtal26 (outillage) — la sonde de l'EMU8000, pendant de h_emu8k_probe (harness_emu8k.c), dans le même ordre :
    //   l'état de la puce, la mémoire entière, le chorus, la réverbération (dix-sept peignes), les trente-deux canaux.
    //   Tout à zéro sans AWE32 montée.
    internal const int ProbeN = 72;

    private const uint64_t FnvSeed = 1469598103934665603UL;
    private const uint64_t FnvPrime = 1099511628211UL;

    private static uint64_t Octets(uint64_t h, ReadOnlySpan<uint8_t> p)
    {
        foreach (var b in p)
        {
            h ^= b;
            h *= FnvPrime;
        }
        return h;
    }

    // Une valeur, étendue sur 64 bits (le signe pour les types signés), mêlée d'un coup.
    private static uint64_t Mot(uint64_t h, uint64_t v)
    {
        h ^= v;
        h *= FnvPrime;
        return h;
    }

    private static uint64_t Env(uint64_t h, emu8k_envelope_t e)
    {
        h = Mot(h, (uint64_t)(int64_t)e.state);
        h = Mot(h, (uint64_t)(int64_t)e.delay_samples);
        h = Mot(h, (uint64_t)(int64_t)e.hold_samples);
        h = Mot(h, (uint64_t)(int64_t)e.attack_samples);
        h = Mot(h, (uint64_t)(int64_t)e.value_amp_hz);
        h = Mot(h, (uint64_t)(int64_t)e.value_db_oct);
        h = Mot(h, (uint64_t)(int64_t)e.sustain_value_db_oct);
        h = Mot(h, (uint64_t)(int64_t)e.attack_amount_amp_hz);
        h = Mot(h, (uint64_t)(int64_t)e.ramp_amount_db_oct);
        return h;
    }

    // Un canal, champ par champ, dans l'ordre du struct du C (sound_emu8k.h:164-341) ; pendant de h_voice.
    internal static uint64_t VoiceFnv(emu8k_voice_t v)
    {
        uint64_t h = FnvSeed;
        h = Mot(h, v.cpf_u.cpf);
        h = Mot(h, v.ptrx_u.ptrx);
        h = Mot(h, v.cvcf_u.cvcf);
        h = Mot(h, (uint64_t)(int64_t)v.volumeslide.last);
        h = Mot(h, v.vtft_u.vtft);
        h = Mot(h, v.unknown_data0_4);
        h = Mot(h, v.unknown_data0_5);
        h = Mot(h, v.psst_u.psst);
        h = Mot(h, v.csl_u.csl);
        h = Mot(h, v.ccca_u.ccca);
        h = Mot(h, v.envvol);
        h = Mot(h, v.dcysusv);
        h = Mot(h, v.envval);
        h = Mot(h, v.dcysus);
        h = Mot(h, v.atkhldv);
        h = Mot(h, v.lfo1val);
        h = Mot(h, v.lfo2val);
        h = Mot(h, v.atkhld);
        h = Mot(h, v.ip);
        h = Mot(h, v.ifatn_u.ifatn);
        h = Mot(h, v.pefe_u.pefe);
        h = Mot(h, v.fmmod_u.fmmod);
        h = Mot(h, v.tremfrq_u.tremfrq);
        h = Mot(h, v.fm2frq2_u.fm2frq2);
        h = Mot(h, (uint64_t)(int64_t)v.env_engine_on);
        h = Mot(h, v.addr.addr);
        h = Mot(h, v.loop_start.addr);
        h = Mot(h, v.loop_end.addr);
        h = Mot(h, (uint64_t)(int64_t)v.initial_att);
        h = Mot(h, (uint64_t)(int64_t)v.initial_filter);
        h = Env(h, v.vol_envelope);
        h = Env(h, v.mod_envelope);
        h = Mot(h, (uint64_t)v.lfo1_speed);
        h = Mot(h, (uint64_t)v.lfo2_speed);
        h = Mot(h, v.lfo1_count.addr);
        h = Mot(h, v.lfo2_count.addr);
        h = Mot(h, (uint64_t)(int64_t)v.lfo1_delay_samples);
        h = Mot(h, (uint64_t)(int64_t)v.lfo2_delay_samples);
        h = Mot(h, (uint64_t)(int64_t)v.vol_l);
        h = Mot(h, (uint64_t)(int64_t)v.vol_r);
        h = Mot(h, (uint64_t)(int64_t)v.fixed_modenv_filter_height);
        h = Mot(h, (uint64_t)(int64_t)v.fixed_modenv_pitch_height);
        h = Mot(h, (uint64_t)(int64_t)v.fixed_lfo1_filt_mod);
        h = Mot(h, (uint64_t)(int64_t)v.fixed_lfo1_vibrato);
        h = Mot(h, (uint64_t)(int64_t)v.fixed_lfo1_tremolo);
        h = Mot(h, (uint64_t)(int64_t)v.fixed_lfo2_vibrato);
        h = Mot(h, (uint64_t)(int64_t)v.filterq_idx);
        h = Mot(h, (uint64_t)(int64_t)v.filt_att);
        for (var c = 0; c < 5; c++)
            h = Mot(h, (uint64_t)v.filt_buffer[c]);
        return h;
    }

    // Un peigne : ses scalaires, puis les MAX_REFL_SIZE premières entrées de son tampon ; pendant de h_comb.
    internal static uint64_t CombFnv(emu8k_reverb_combfilter_t r)
    {
        uint64_t h = FnvSeed;
        h = Mot(h, (uint64_t)(int64_t)r.read_pos);
        h = Mot(h, (uint64_t)(int64_t)r.bufsize);
        h = Mot(h, (uint64_t)(int64_t)r.filterstore);
        h = Mot(h, BitConverter.SingleToUInt32Bits(r.output_gain));
        h = Mot(h, BitConverter.SingleToUInt32Bits(r.feedback));
        h = Mot(h, BitConverter.SingleToUInt32Bits(r.damp1));
        h = Mot(h, BitConverter.SingleToUInt32Bits(r.damp2));
        for (var c = 0; c < MAX_REFL_SIZE; c++)
            h = Mot(h, (uint64_t)(int64_t)r.reflection[c]);
        return h;
    }

    internal static void Probe(uint64_t[] o)
    {
        Array.Clear(o, 0, ProbeN);
        var e = sound_sb.sb_pri?.emu8k;
        if (e is null)
            return;
        var rv = e.reverb_engine;
        var ch = e.chorus_engine;
        int f = 0;
        // L'adresse de la puce : le premier port où emu8k_init a posé emu8k_inw pour elle.
        int p = 0;
        for (; p < 0x10000; p++)
            if ((io.port_inw[p, 0] is not null && ReferenceEquals(io.port_priv[p, 0], e)) ||
                (io.port_inw[p, 1] is not null && ReferenceEquals(io.port_priv[p, 1], e)))
                break;
        o[f++] = 1 | ((uint64_t)(uint8_t)e.cur_reg << 8) | ((uint64_t)(uint8_t)e.cur_voice << 16) | ((uint64_t)(p & 0xffff) << 24);
        o[f++] = e.hwcf1 | ((uint64_t)e.hwcf2 << 16) | ((uint64_t)e.hwcf3 << 32);
        o[f++] = e.hwcf4 | ((uint64_t)e.hwcf5 << 32);
        o[f++] = e.hwcf6 | ((uint64_t)e.hwcf7 << 32);
        o[f++] = e.smalr | ((uint64_t)e.smarr << 32);
        o[f++] = e.smalw | ((uint64_t)e.smarw << 32);
        o[f++] = e.smld_buffer | ((uint64_t)e.smrd_buffer << 16) | ((uint64_t)e.wc << 32) | ((uint64_t)e.id << 48);
        uint64_t h = Octets(FnvSeed, MemoryMarshal.AsBytes(e.init1.AsSpan()));
        h = Octets(h, MemoryMarshal.AsBytes(e.init2.AsSpan()));
        h = Octets(h, MemoryMarshal.AsBytes(e.init3.AsSpan()));
        o[f++] = Octets(h, MemoryMarshal.AsBytes(e.init4.AsSpan()));
        o[f++] = e.ram_end_addr | ((uint64_t)(uint32_t)e.pos << 32);
        o[f++] = (uint16_t)e.out_l | ((uint64_t)(uint16_t)e.out_r << 16);
        o[f++] = (uint32_t)random_helper | ((uint64_t)(uint32_t)dmareadbit << 16) | ((uint64_t)(uint32_t)dmawritebit << 40);
        // La ROM, le bloc vide, la RAM : dans l'ordre de `mem` (voir le type), celui des trois blocs du C.
        o[f++] = Octets(FnvSeed, MemoryMarshal.AsBytes(e.mem.AsSpan()));
        o[f++] = (uint32_t)ch.write | ((uint64_t)(uint32_t)ch.feedback << 32);
        o[f++] = (uint32_t)ch.delay_samples_central;
        o[f++] = BitConverter.DoubleToUInt64Bits(ch.lfodepth_multip);
        o[f++] = BitConverter.DoubleToUInt64Bits(ch.delay_offset_samples_right);
        o[f++] = ch.lfo_inc.addr;
        o[f++] = ch.lfo_pos.addr;
        o[f++] = Octets(FnvSeed, MemoryMarshal.AsBytes(ch.chorus_buffer.AsSpan()));
        o[f++] = Octets(FnvSeed, MemoryMarshal.AsBytes(e.chorus_in_buffer.AsSpan()));
        o[f++] = (uint16_t)rv.out_mix | ((uint64_t)(uint16_t)rv.link_return_amp << 16) |
                 ((uint64_t)(uint8_t)rv.link_return_type << 32) | ((uint64_t)rv.refl_in_amp << 40);
        o[f++] = Octets(FnvSeed, MemoryMarshal.AsBytes(e.reverb_in_buffer.AsSpan()));
        o[f++] = Octets(FnvSeed, MemoryMarshal.AsBytes(e.buffer.AsSpan()));
        for (var c = 0; c < 6; c++)
            o[f++] = CombFnv(rv.reflections[c]);
        for (var c = 0; c < 8; c++)
            o[f++] = CombFnv(rv.allpass[c]);
        o[f++] = CombFnv(rv.tailL);
        o[f++] = CombFnv(rv.tailR);
        o[f++] = CombFnv(rv.damper);
        for (var c = 0; c < 32; c++)
            o[f++] = VoiceFnv(e.voice[c]);
    }

    // pcem: sound_emu8k.c:2234-2237
    internal static void emu8k_close(emu8k_t emu8k)
    {
        // omitted: free(emu8k->rom), free(emu8k->ram) — sans objet sous GC.
        emu8k.mem = [];
        emu8k.ram_present = false;
    }
}
