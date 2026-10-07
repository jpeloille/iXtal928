// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/sound/sound_sb_dsp.c + includes/private/sound/sound_sb_dsp.h
// STATUS: partial — le DSP tel que les Sound Blaster 1.0 à Pro v2 l'atteignent (sb_type de SB1 à SBPRO2 : les
//         gardes de version, recopiées dès G8, suffisent aux DSP 1.05, 2.00, 2.01 et 3.00 de G12.0 ;
//         sb_subtype == SB_SUBTYPE_DEFAULT, DMA 8 bits) : sb_dsp_t, les tables (sbe2dat,
//         sb_commands, sb_dsp_versions, scaleMap*/adjustMap*), sb_irq, sb_irqc, sb_dsp_reset,
//         sb_doreset, sb_dsp_speed_changed, sb_add_data, sb_start_dma, sb_start_dma_i,
//         sb_8_read_dma, sb_8_write_dma, sb_dsp_setirq, sb_dsp_setdma8, sb_exec_command,
//         sb_write, sb_read, sb_wb_clear, sb_dsp_init, sb_dsp_setaddr, sb_dsp_set_stereo,
//         pollsb, sb_poll_i, sb_dsp_update, sb_dsp_close.
//         G12.1 : la SB 16 (sb_type == SB16) — le DMA 16 bits, recalc_sb16_filter et low_fir_sb16_coef,
//         sb16_copyright, sb_16_read_dma, sb_16_write_dma, sb_dsp_setdma16, les commandes 0x01, 0x41/0x42,
//         0xB0-0xCF, 0xD5/0xD6/0xD9, 0xE3, 0x08, 0x0E/0x0F et 0xF9, les branches 16 bits de pollsb et de
//         sb_poll_i ; sb_enable_i rétabli.
//         Omis : les corps Aztech (IS_AZTECH), le débogage SB_DSP_RECORD_DEBUG / SB_TEST_RECORDING_SAW
//         (#ifdef éteints) et sb_dsp_add_status_info (:1273-1326, hôte).

// CS8600 : `(sb_dsp_t)priv` part du `object` des delegates d'io.cs et de timer.cs, comme
// à sound_opl.cs. CS8602 : même raison, à la déréférence qui suit.
#pragma warning disable CS8600, CS8602

using iXtal26.Models;
using static iXtal26.io;
using static iXtal26.Models.dma;
using static iXtal26.Models.pic;
using static iXtal26.Sound.sound;
using static iXtal26.timer;

namespace iXtal26.Sound;

// pcem: sound_sb_dsp.h:15-87
// Classe et non struct : son adresse est prise (io_sethandler, timer_add, sb_t).
internal sealed class sb_dsp_t
{
    internal int sb_type;
    internal int sb_subtype; // which clone
    internal object? parent; // "sb_t *" if default subtype, "azt2316a_t *" if aztech.

    internal int sb_8_length, sb_8_format, sb_8_autoinit, sb_8_pause, sb_8_enable, sb_8_autolen, sb_8_output;
    internal int sb_8_dmanum;
    internal int sb_16_length, sb_16_format, sb_16_autoinit, sb_16_pause, sb_16_enable, sb_16_autolen, sb_16_output;
    internal int sb_16_dmanum;
    internal int sb_pausetime;

    internal readonly uint8_t[] sb_read_data = new uint8_t[256];
    internal int sb_read_wp, sb_read_rp;
    internal int sb_speaker;
    internal int muted;

    internal int sb_data_stat;

    internal int sb_irqnum;

    internal uint8_t sbe2;
    internal int sbe2count;

    internal readonly uint8_t[] sb_data = new uint8_t[8];

    internal int sb_freq;

    internal int16_t sbdat;
    internal int sbdat2;
    internal int16_t sbdatl, sbdatr;

    internal uint8_t sbref;
    internal int8_t sbstep;

    internal int sbdacpos;

    internal int sbleftright;

    internal int sbreset;
    internal uint8_t sbreaddat;
    internal uint8_t sb_command;
    internal uint8_t sb_test;
    internal int sb_timei, sb_timeo;

    internal int sb_irq8, sb_irq16;

    internal readonly uint8_t[] sb_asp_regs = new uint8_t[256];

    // pcem: sound_sb_dsp.h:63 — G12.1 : sb_enable_i, lu par sb_get_buffer_sb16 et sb_get_buffer_emu8k
    //   (sound_sb.c:180, :272), n'est écrit nulle part : il vaut toujours 0 (l'initialiseur tait CS0649).
    // pcem bug, reproduced: PB-148 — rien ne remplit record_buffer : l'entrée rend du silence.
    // omitted: sbenable — ni lecteur ni écrivain.
    internal int sb_enable_i = 0;

    internal readonly pc_timer_t output_timer = new pc_timer_t(), input_timer = new pc_timer_t();

    internal uint64_t sblatcho, sblatchi;

    internal uint16_t sb_addr;

    internal int stereo;

    internal int asp_data_len;

    internal readonly pc_timer_t wb_timer = new pc_timer_t();
    internal int wb_full;

    internal int busy_count;

    internal int record_pos_read;
    internal int record_pos_write;
    internal readonly int16_t[] record_buffer = new int16_t[0xFFFF];
    internal readonly int16_t[] buffer = new int16_t[MAXSOUNDBUFLEN * 2];
    internal int pos;

    // omitted: azt_eeprom[AZTECH_EEPROM_SIZE] (sound_sb_dsp.h:86) — Aztech seulement.
}

internal static partial class sound_sb_dsp
{
    // pcem: ibm.h:351-358 — les types de carte que les comparaisons de ce fichier lisent.
    internal const int SADLIB = 1, SB1 = 2, SB15 = 3, SB2 = 4, SBPRO = 5, SBPRO2 = 6, SB16 = 7;

    // pcem: sound_sb_dsp.h:5-7
    internal const int SB_SUBTYPE_DEFAULT = 0;
    internal const int SB_SUBTYPE_CLONE_AZT2316A_0X11 = 1;
    internal const int SB_SUBTYPE_CLONE_AZT1605_0X0C = 2;

    // pcem: sound_sb_dsp.h:10-12
    private static bool IS_AZTECH(sb_dsp_t dsp) =>
        dsp.sb_subtype == SB_SUBTYPE_CLONE_AZT2316A_0X11 ||
        dsp.sb_subtype == SB_SUBTYPE_CLONE_AZT1605_0X0C;

    // pcem: sound_sb_dsp.c:24
    private const int SB_DSP_REC_SAFEFTY_MARGIN = 4096;

    // pcem: sound_sb_dsp.c:40-43
    private static readonly int[][] sbe2dat =
    {
        new int[] {0x01, -0x02, -0x04, 0x08, -0x10, 0x20, 0x40, -0x80, -106},
        new int[] {-0x01, 0x02, -0x04, 0x08, 0x10, -0x20, 0x40, -0x80, 165},
        new int[] {-0x01, 0x02, 0x04, -0x08, 0x10, -0x20, -0x40, 0x80, -151},
        new int[] {0x01, -0x02, 0x04, -0x08, -0x10, 0x20, -0x40, 0x80, 90},
    };

    // pcem: sound_sb_dsp.c:45-54 — global MUTABLE (sb_doreset l'écrit) : il traverse les
    // amorçages d'un processus comme chez PCem.
    // pcem bug, reproduced: PB-240 — 30h à 38h (la MIDI de la SB) valent -1 : 38h s'exécute seule, et l'octet
    //   MIDI qui la suit est pris pour une commande ; 34h à 37h (le mode UART) n'ont pas de case.
    internal static readonly int[] sb_commands = new int[256] {
        -1, 2,  -1, -1, 1,  2,  -1, 0,  1,  -1, -1, -1, -1, -1, 2,  1,  1,  -1, -1, -1, 2,  -1, 2,  2,  -1, -1, -1, -1, 0,
        -1, -1, 0,  0,  -1, -1, -1, 2,  -1, -1, -1, -1, -1, -1, -1, 0,  -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
        -1, -1, -1, -1, -1, -1, 1,  2,  2,  -1, -1, -1, -1, -1, 2,  -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
        -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
        2,  2,  2,  2,  -1, -1, -1, -1, -1, 0,  -1, 0,  2,  2,  -1, -1, -1, -1, -1, -1, 2,  2,  -1, -1, -1, -1, -1, -1, 0,
        -1, -1, -1, -1, -1, -1, -1, 0,  -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
        -1, -1, 3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,
        3,  3,  3,  3,  3,  0,  0,  -1, 0,  0,  0,  0,  -1, 0,  0,  0,  -1, -1, -1, -1, -1, 1,  0,  1,  0,  1,  -1, -1, 0,
        0,  -1, -1, -1, -1, -1, -1, -1, -1, -1, 0,  -1, -1, -1, -1, -1, -1, 1,  2,  -1, -1, -1, -1, 0};

    // pcem: sound_sb_dsp.c:56 — G12.1 : 44 caractères ; 0xE3 les rend, puis un 0.
    private const string sb16_copyright = "COPYRIGHT (C) CREATIVE TECHNOLOGY LTD, 1992.";

    // pcem: sound_sb_dsp.c:57
    internal static readonly uint16_t[] sb_dsp_versions = {0, 0, 0x105, 0x200, 0x201, 0x300, 0x302, 0x405, 0x40d};

    /*These tables were 'borrowed' from DOSBox*/
    // pcem: sound_sb_dsp.c:60-62
    private static readonly int8_t[] scaleMap4 = {0,   1,   2,   3,   4,  5,  6,  7,   0,   -1,  -2, -3, -4, -5,  -6,  -7,  1,   3,   5,   7,  9,   11,
                                                  13,  15,  -1,  -3,  -5, -7, -9, -11, -13, -15, 2,  6,  10, 14,  18,  22,  26,  30,  -2,  -6, -10, -14,
                                                  -18, -22, -26, -30, 4,  12, 20, 28,  36,  44,  52, 60, -4, -12, -20, -28, -36, -44, -52, -60};
    // pcem: sound_sb_dsp.c:63-65
    private static readonly uint8_t[] adjustMap4 = {0,  0,  0,   0,  0,   16, 16, 16, 0,  0,  0,   0, 0,   16, 16, 16, 240, 0,  0,   0, 0, 16,
                                                    16, 16, 240, 0,  0,   0,  0,  16, 16, 16, 240, 0, 0,   0,  0,  16, 16,  16, 240, 0, 0, 0,
                                                    0,  16, 16,  16, 240, 0,  0,  0,  0,  0,  0,   0, 240, 0,  0,  0,  0,   0,  0,   0};

    // pcem: sound_sb_dsp.c:67-68
    private static readonly int8_t[] scaleMap26 = {0,  1,  2,   3,   0, -1, -2, -3, 1,  3,   5,   7,   -1, -3, -5, -7, 2,  6,   10,  14,
                                                   -2, -6, -10, -14, 4, 12, 20, 28, -4, -12, -20, -28, 5,  15, 25, 35, -5, -15, -25, -35};
    // pcem: sound_sb_dsp.c:69-70
    private static readonly uint8_t[] adjustMap26 = {0,   0, 0, 8, 0,   0, 0, 8, 248, 0, 0, 8, 248, 0, 0, 8, 248, 0, 0, 8,
                                                     248, 0, 0, 8, 248, 0, 0, 8, 248, 0, 0, 8, 248, 0, 0, 0, 248, 0, 0, 0};

    // pcem: sound_sb_dsp.c:72
    private static readonly int8_t[] scaleMap2 = {0, 1, 0, -1, 1, 3, -1, -3, 2, 6, -2, -6, 4, 12, -4, -12, 8, 24, -8, -24, 6, 48, -16, -48};
    // pcem: sound_sb_dsp.c:73
    private static readonly uint8_t[] adjustMap2 = {0, 4, 0, 4, 252, 4, 252, 4, 252, 4, 252, 4, 252, 4, 252, 4, 252, 4, 252, 4, 252, 0, 252, 0};

    // pcem: filters.h:270 — #define SB16_NCoef 51
    internal const int SB16_NCoef = 51;

    // pcem: sound_sb_dsp.c:75 — global, lu par low_fir_sb16 (filters.h:274-295), donc par sb_get_buffer_sb16 et
    //   sb_get_buffer_emu8k. sb_dsp_init le recalcule pour TOUTES les cartes (:838) ; seules la SB 16 et l'AWE32
    //   le lisent.
    internal static readonly float[] low_fir_sb16_coef = new float[SB16_NCoef];

    // pcem: sound_sb_dsp.c:77 — sin de la libm, comme Math.Sin (la glibc des deux côtés, PLAN-G12.md décision
    //   n° 13 ; sb16-filter-check compare les 51 coefficients bit à bit).
    private static double sinc(double x) { return Math.Sin(Math.PI * x) / (Math.PI * x); }

    // pcem: sound_sb_dsp.c:79-105 — l'ordre d'évaluation du C, à la lettre : fC en float depuis un calcul en
    //   double ; la fenêtre et le sinus cardinal en double, le produit rangé en float ; le gain sommé en float,
    //   dans l'ordre ; chaque coefficient divisé en float. Le NaN de sinc(0), à n = 25, est écrasé par 1.0.
    internal static void recalc_sb16_filter(int playback_freq)
    {
        /*Cutoff frequency = playback / 2*/
        float fC = (float)(((double)(float)playback_freq / 2.0) / 48000.0);
        float gain;
        int n;

        for (n = 0; n < SB16_NCoef; n++) {
                /*Blackman window*/
                double w = 0.42 - (0.5 * Math.Cos((2.0 * n * Math.PI) / (double)(SB16_NCoef - 1))) +
                           (0.08 * Math.Cos((4.0 * n * Math.PI) / (double)(SB16_NCoef - 1)));
                /*Sinc filter*/
                double h = sinc(2.0 * fC * ((double)n - ((double)(SB16_NCoef - 1) / 2.0)));

                /*Create windowed-sinc filter*/
                low_fir_sb16_coef[n] = (float)(w * h);
        }

        low_fir_sb16_coef[(SB16_NCoef - 1) / 2] = 1.0f;

        gain = 0.0f;
        for (n = 0; n < SB16_NCoef; n++)
                gain += low_fir_sb16_coef[n];

        /*Normalise filter, to produce unity gain*/
        for (n = 0; n < SB16_NCoef; n++)
                low_fir_sb16_coef[n] /= gain;
    }

    // pcem: sound_sb_dsp.c:107-114
    internal static void sb_irq(sb_dsp_t dsp, int irq8)
    {
        //        pclog("IRQ %i %02X\n",irq8,pic.mask);
        if (irq8 != 0)
                dsp.sb_irq8 = 1;
        else
                dsp.sb_irq16 = 1;
        // pcem bug, reproduced: PB-92 — l'IRQ 10 que propose la configuration (sound_sb.c) tombe,
        //   sur une machine sans second PIC, dans le `num <= 0xff` de picint (pic.c:302-308) :
        //   perdue, sans un mot. Comme sur la carte : l'IRQ 10 n'est que sur la rallonge de 36
        //   broches de l'AT (D3, AT TR p. 1-21), absente d'un PC ou d'un XT.
        picint((uint16_t)(1 << dsp.sb_irqnum));
    }
    // pcem: sound_sb_dsp.c:115-121
    internal static void sb_irqc(sb_dsp_t dsp, int irq8)
    {
        if (irq8 != 0)
                dsp.sb_irq8 = 0;
        else
                dsp.sb_irq16 = 0;
        picintc((uint16_t)(1 << dsp.sb_irqnum));
    }

    // pcem: sound_sb_dsp.c:123-157
    internal static void sb_dsp_reset(sb_dsp_t dsp)
    {
        timer_disable(dsp.output_timer);
        timer_disable(dsp.input_timer);

        dsp.sb_command = 0;

        // pcem bug, reproduced: PB-236 — le reset remet le DSP à froid : bloc 07FFh, constante 9Ch, sortie
        //   coupée (2.02, 3.02 ; le bloc en 4.xx aussi). PCem pose FFFFh et garde la constante et `muted`, que rien ne
        //   pose non plus au démarrage.
        dsp.sb_8_length = 0xffff;
        dsp.sb_8_autolen = 0xffff;

        sb_irqc(dsp, 0);
        sb_irqc(dsp, 1);
        dsp.sb_16_pause = 0;
        dsp.sb_read_wp = dsp.sb_read_rp = 0;
        dsp.sb_data_stat = -1;
        dsp.sb_speaker = 0;
        dsp.sb_pausetime = -1;
        dsp.sbe2 = 0xAA;
        dsp.sbe2count = 0;

        dsp.sbreset = 0;

        dsp.record_pos_read = 0;
        dsp.record_pos_write = SB_DSP_REC_SAFEFTY_MARGIN;

        picintc((uint16_t)(1 << dsp.sb_irqnum));

        dsp.asp_data_len = 0;

        // omitted: SB_DSP_RECORD_DEBUG (:151-156) — #ifdef éteint.
    }

    // pcem: sound_sb_dsp.c:159-178
    internal static void sb_doreset(sb_dsp_t dsp)
    {
        int c;

        sb_dsp_reset(dsp);

        if (IS_AZTECH(dsp)) {
                // omitted: sb_commands[8] = 1 ; sb_commands[9] = 1 (:165-166) — Aztech.
        } else {
                // pcem bug, reproduced: PB-165 — 08h attend un paramètre sur la SB 16 ; le DSP 4.05 n'en lit aucun,
                //   comme 4.04 à 4.16 et comme l'AWE32 de PCem (type SB16 + 1), qui prend -1.
                if (dsp.sb_type == SB16)
                        sb_commands[8] = 1;
                else
                        sb_commands[8] = -1;
        }

        for (c = 0; c < 256; c++)
                dsp.sb_asp_regs[c] = 0;
        dsp.sb_asp_regs[5] = 0x01;
        dsp.sb_asp_regs[9] = 0xf8;
    }

    // pcem: sound_sb_dsp.c:180-190. `TIMER_USEC * (1000000.0f / (float)x)` : uint64_t * float,
    //   calculé en float (TIMER_USEC converti en float), puis tronqué vers uint64_t. La branche >= 256 est
    //   celle de 41h/42h (G12.1, sb_timeo = 256 + freq) ; freq n'y vaut jamais 0 (PB-150), aucune division
    //   par zéro.
    internal static void sb_dsp_speed_changed(sb_dsp_t dsp)
    {
        if (dsp.sb_timeo < 256)
                dsp.sblatcho = TIMER_USEC * (uint64_t)(256 - dsp.sb_timeo);
        else
                dsp.sblatcho = Cpu._386.CvtU64((double)((float)TIMER_USEC * (1000000.0f / (float)(dsp.sb_timeo - 256))));

        if (dsp.sb_timei < 256)
                dsp.sblatchi = TIMER_USEC * (uint64_t)(256 - dsp.sb_timei);
        else
                dsp.sblatchi = Cpu._386.CvtU64((double)((float)TIMER_USEC * (1000000.0f / (float)(dsp.sb_timei - 256))));
    }

    // pcem: sound_sb_dsp.c:192-195
    internal static void sb_add_data(sb_dsp_t dsp, uint8_t v)
    {
        dsp.sb_read_data[dsp.sb_read_wp++] = v;
        dsp.sb_read_wp &= 0xff;
    }

    // pcem: sound_sb_dsp.c:197-199
    private const int ADPCM_4 = 1;
    private const int ADPCM_26 = 2;
    private const int ADPCM_2 = 3;

    // pcem: sound_sb_dsp.c:201-230
    internal static void sb_start_dma(sb_dsp_t dsp, int dma8, int autoinit, uint8_t format, int len)
    {
        // pcem bug, reproduced: PB-239 — la commande relance sur-le-champ ; pendant un automatique, le DSP
        //   finit d'abord le bloc en cours (le 2.02 aussi pendant un simple cycle ; le 4.05 jette l'octet de mode).
        dsp.sb_pausetime = -1;
        if (dma8 != 0) {
                dsp.sb_8_length = len;
                dsp.sb_8_format = format;
                dsp.sb_8_autoinit = autoinit;
                dsp.sb_8_pause = 0;
                dsp.sb_8_enable = 1;
                if (dsp.sb_16_enable != 0 && dsp.sb_16_output != 0)
                        dsp.sb_16_enable = 0;
                dsp.sb_8_output = 1;
                if (timer_is_enabled(dsp.output_timer) == 0)
                        timer_set_delay_u64(dsp.output_timer, dsp.sblatcho);
                dsp.sbleftright = 0;
                dsp.sbdacpos = 0;
                //                pclog("Start 8-bit DMA addr %06X len %04X\n",dma.ac[1]+(dma.page[1]<<16),len);
        } else {
                dsp.sb_16_length = len;
                dsp.sb_16_format = format;
                dsp.sb_16_autoinit = autoinit;
                dsp.sb_16_pause = 0;
                dsp.sb_16_enable = 1;
                if (dsp.sb_8_enable != 0 && dsp.sb_8_output != 0)
                        dsp.sb_8_enable = 0;
                dsp.sb_16_output = 1;
                if (timer_is_enabled(dsp.output_timer) == 0)
                        timer_set_delay_u64(dsp.output_timer, dsp.sblatcho);
                //                pclog("Start 16-bit DMA addr %06X len %04X\n",dma16.ac[1]+(dma16.page[1]<<16),len);
        }
    }

    // pcem: sound_sb_dsp.c:232-290
    internal static void sb_start_dma_i(sb_dsp_t dsp, int dma8, int autoinit, uint8_t format, int len)
    {
        if (dma8 != 0) {
                // omitted: SB_TEST_RECORDING_SAW (:234-245) — #ifdef éteint.
                dsp.sb_8_length = len;
                dsp.sb_8_format = format;
                dsp.sb_8_autoinit = autoinit;
                dsp.sb_8_pause = 0;
                dsp.sb_8_enable = 1;
                if (dsp.sb_16_enable != 0 && dsp.sb_16_output == 0)
                        dsp.sb_16_enable = 0;
                dsp.sb_8_output = 0;
                if (timer_is_enabled(dsp.input_timer) == 0)
                        timer_set_delay_u64(dsp.input_timer, dsp.sblatchi);
                //                pclog("Start 8-bit input DMA addr %06X len %04X\n",dma.ac[1]+(dma.page[1]<<16),len);
        } else {
                // omitted: SB_TEST_RECORDING_SAW (:258-269) — #ifdef éteint.
                dsp.sb_16_length = len;
                dsp.sb_16_format = format;
                dsp.sb_16_autoinit = autoinit;
                dsp.sb_16_pause = 0;
                dsp.sb_16_enable = 1;
                if (dsp.sb_8_enable != 0 && dsp.sb_8_output == 0)
                        dsp.sb_8_enable = 0;
                dsp.sb_16_output = 0;
                if (timer_is_enabled(dsp.input_timer) == 0)
                        timer_set_delay_u64(dsp.input_timer, dsp.sblatchi);
                //                pclog("Start 16-bit input DMA addr %06X len %04X\n",dma16.ac[1]+(dma16.page[1]<<16),len);
        }
        Array.Clear(dsp.record_buffer);

        // omitted: SB_DSP_RECORD_DEBUG (:284-289) — #ifdef éteint.
    }

    // pcem: sound_sb_dsp.c:292
    internal static int sb_8_read_dma(sb_dsp_t dsp) { return dma_channel_read(dsp.sb_8_dmanum); }
    // pcem: sound_sb_dsp.c:293-300
    internal static void sb_8_write_dma(sb_dsp_t dsp, uint8_t val)
    {
        dma_channel_write(dsp.sb_8_dmanum, val);
        // omitted: SB_DSP_RECORD_DEBUG (:295-299) — #ifdef éteint.
    }
    // pcem: sound_sb_dsp.c:301 — G12.1.
    internal static int sb_16_read_dma(sb_dsp_t dsp) { return dma_channel_read(dsp.sb_16_dmanum); }
    // pcem: sound_sb_dsp.c:302-310 — G12.1. Le paramètre est un uint16_t : `record_buffer[i] ^ 0x8000` y est
    //   tronqué par l'appelant.
    internal static int sb_16_write_dma(sb_dsp_t dsp, uint16_t val)
    {
        int ret = dma_channel_write(dsp.sb_16_dmanum, val);
        // omitted: SB_DSP_RECORD_DEBUG (:304-308) — #ifdef éteint.
        return ret == DMA_NODATA ? 1 : 0;
    }

    // pcem: sound_sb_dsp.c:312
    internal static void sb_dsp_setirq(sb_dsp_t dsp, int irq) { dsp.sb_irqnum = irq; }

    // pcem: sound_sb_dsp.c:314
    internal static void sb_dsp_setdma8(sb_dsp_t dsp, int dma) { dsp.sb_8_dmanum = dma; }

    // pcem: sound_sb_dsp.c:316 — G12.1 : appelé par le seul registre 81h du CT1745 (sound_sb.c:631-635) ; les init
    //   n'y touchent pas (TODO :1059, :1084), le défaut est celui de sb_dsp_init (5).
    internal static void sb_dsp_setdma16(sb_dsp_t dsp, int dma) { dsp.sb_16_dmanum = dma; }

    // pcem: sound_sb_dsp.c:317-715
    internal static void sb_exec_command(sb_dsp_t dsp)
    {
        int temp, c;
        //        pclog("sb_exec_command : SB command %02X\n", dsp->sb_command);
        switch (dsp.sb_command) {
        case 0x01: /*???*/
                if (dsp.sb_type < SB16)
                        break;
                dsp.asp_data_len = dsp.sb_data[0] + (dsp.sb_data[1] << 8) + 1;
                break;
        case 0x03: /*ASP status*/
                sb_add_data(dsp, 0);
                break;
        case 0x10: /*8-bit direct mode*/
                sb_dsp_update(dsp);
                dsp.sbdat = dsp.sbdatl = dsp.sbdatr = (int16_t)((dsp.sb_data[0] ^ 0x80) << 8);
                break;
        case 0x14: /*8-bit single cycle DMA output*/
                sb_start_dma(dsp, 1, 0, 0, dsp.sb_data[0] + (dsp.sb_data[1] << 8));
                break;
        case 0x17: /*2-bit ADPCM output with reference*/
                dsp.sbref = (uint8_t)sb_8_read_dma(dsp);
                dsp.sbstep = 0;
                //                pclog("Ref byte 2 %02X\n",sbref);
                goto case 0x16;
        case 0x16: /*2-bit ADPCM output*/
                sb_start_dma(dsp, 1, 0, ADPCM_2, dsp.sb_data[0] + (dsp.sb_data[1] << 8));
                dsp.sbdat2 = sb_8_read_dma(dsp);
                dsp.sb_8_length--;
                if (dsp.sb_command == 0x17)
                        dsp.sb_8_length--;
                break;
        case 0x1C: /*8-bit autoinit DMA output*/
                if (dsp.sb_type < SB15)
                        break;
                sb_start_dma(dsp, 1, 1, 0, dsp.sb_8_autolen);
                break;
        case 0x1F: /*2-bit ADPCM autoinit output*/
                if (dsp.sb_type < SB15)
                        break;
                // pcem bug, reproduced: PB-146 — sb_commands[0x1F] vaut 0 : sb_data[0..1] sont les octets de la
                //   commande précédente, et non la taille de 48h ; l'octet de référence n'est pas lu.
                sb_start_dma(dsp, 1, 1, ADPCM_2, dsp.sb_data[0] + (dsp.sb_data[1] << 8));
                dsp.sbdat2 = sb_8_read_dma(dsp);
                dsp.sb_8_length--;
                break;
        case 0x20: /*8-bit direct input*/
                sb_add_data(dsp, (uint8_t)((dsp.record_buffer[dsp.record_pos_read] >> 8) ^ 0x80));
                /*Due to the current implementation, I need to emulate a samplerate, even if this
                 * mode does not imply such samplerate. Position is increased in sb_poll_i*/
                if (timer_is_enabled(dsp.input_timer) == 0) {
                        dsp.sb_timei = 256 - 22;
                        dsp.sblatchi = TIMER_USEC * 22;
                        temp = 1000000 / 22;
                        dsp.sb_freq = temp;
                        timer_set_delay_u64(dsp.input_timer, dsp.sblatchi);
                }
                break;
        case 0x24: /*8-bit single cycle DMA input*/
                sb_start_dma_i(dsp, 1, 0, 0, dsp.sb_data[0] + (dsp.sb_data[1] << 8));
                break;
        case 0x2C: /*8-bit autoinit DMA input*/
                if (dsp.sb_type < SB15)
                        break;
                // pcem bug, reproduced: PB-146 — sb_commands[0x2C] vaut 0, comme pour 1Fh.
                sb_start_dma_i(dsp, 1, 1, 0, dsp.sb_data[0] + (dsp.sb_data[1] << 8));
                break;
        case 0x40: /*Set time constant*/
                // pcem bug, reproduced: PB-155 — sur la SB 16, 1 000 000 / (256 - c), jusqu'à 1 MHz ; le DSP 4.05 borne
                //   la constante à EBh, puis la traduit par sa table en registre de fréquence.
                dsp.sb_timei = dsp.sb_timeo = dsp.sb_data[0];
                dsp.sblatcho = dsp.sblatchi = TIMER_USEC * (uint64_t)(256 - dsp.sb_data[0]);
                temp = 256 - dsp.sb_data[0];
                temp = 1000000 / temp;
                //                pclog("Sample rate - %ihz (%i)\n",temp, dsp->sblatcho);
                if (dsp.sb_freq != temp && dsp.sb_type >= SB16)
                        recalc_sb16_filter(temp);
                dsp.sb_freq = temp;
                break;
        case 0x41: /*Set output sampling rate*/
        case 0x42: /*Set input sampling rate*/
                if (dsp.sb_type < SB16)
                        break;
                {
                // pcem: sound_sb_dsp.c:393-401 — G12.1. L'octet FORT vient en premier. `TIMER_USEC * (1000000.0f /
                //   (float)f)` : uint64_t par float, en float ; la conversion vers uint64_t est celle de GCC (CvtU64).
                // pcem bug, reproduced: PB-155 — la fréquence reste libre, de 1 à 65 535 Hz ; le DSP 4.05 la quantifie
                //   en un registre de 8 bits (≈ 23 × f / 4096) : FFh dès l'octet fort B1h, 1Ch sous 13h.
                // pcem bug, not reproduced: PB-150 — la fréquence 0 rend sblatcho nul en C (ulong.MaxValue en .NET) :
                //   l'échéance ne recule plus et timer_process boucle sans fin dès qu'une minuterie du DSP tourne
                //   (R9). DEVIATION (PLAN-G12.md, décision n° 10) : 0 est ramené à 1 Hz, le reste de la commande
                //   gardé (sb_timeo 257, des coefficients finis).
                int freq = dsp.sb_data[1] + (dsp.sb_data[0] << 8);
                if (freq == 0) {
                        Diag.R9.Garde("sound_sb_dsp.c:393");
                        freq = 1;
                }
                dsp.sblatcho = Cpu._386.CvtU64((double)((float)TIMER_USEC * (1000000.0f / (float)freq)));
                //                pclog("Sample rate - %ihz (%i)\n",dsp->sb_data[1]+(dsp->sb_data[0]<<8), dsp->sblatcho);
                temp = dsp.sb_freq;
                dsp.sb_freq = freq;
                dsp.sb_timeo = 256 + dsp.sb_freq;
                dsp.sblatchi = dsp.sblatcho;
                dsp.sb_timei = dsp.sb_timeo;
                if (dsp.sb_freq != temp && dsp.sb_type >= SB16)
                        recalc_sb16_filter(dsp.sb_freq);
                }
                break;
        case 0x48: /*Set DSP block transfer size*/
                // pcem bug, reproduced: PB-242 — sans garde de version : le guide ne donne 48h qu'à partir du
                //   DSP 2.00 ; ce qu'en fait le 1.05 est inconnu.
                dsp.sb_8_autolen = dsp.sb_data[0] + (dsp.sb_data[1] << 8);
                break;
        case 0x75: /*4-bit ADPCM output with reference*/
                dsp.sbref = (uint8_t)sb_8_read_dma(dsp);
                dsp.sbstep = 0;
                //                pclog("Ref byte 4 %02X\n",sbref);
                goto case 0x74;
        case 0x74: /*4-bit ADPCM output*/
                sb_start_dma(dsp, 1, 0, ADPCM_4, dsp.sb_data[0] + (dsp.sb_data[1] << 8));
                // pcem bug, reproduced: PB-90 — `octet | DMA_OVER` si le compte du 8237 finit sur cet octet.
                dsp.sbdat2 = sb_8_read_dma(dsp);
                dsp.sb_8_length--;
                if (dsp.sb_command == 0x75)
                        dsp.sb_8_length--;
                break;
        case 0x77: /*2.6-bit ADPCM output with reference*/
                dsp.sbref = (uint8_t)sb_8_read_dma(dsp);
                dsp.sbstep = 0;
                //                pclog("Ref byte 26 %02X\n",sbref);
                goto case 0x76;
        case 0x76: /*2.6-bit ADPCM output*/
                sb_start_dma(dsp, 1, 0, ADPCM_26, dsp.sb_data[0] + (dsp.sb_data[1] << 8));
                // pcem bug, reproduced: PB-90 — `octet | DMA_OVER` si le compte du 8237 finit sur cet octet.
                dsp.sbdat2 = sb_8_read_dma(dsp);
                dsp.sb_8_length--;
                if (dsp.sb_command == 0x77)
                        dsp.sb_8_length--;
                break;
        case 0x7D: /*4-bit ADPCM autoinit output*/
                if (dsp.sb_type < SB15)
                        break;
                // pcem bug, reproduced: PB-146 — sb_commands[0x7D] vaut 0, comme pour 1Fh : ni la taille de 48h ni
                //   l'octet de référence.
                sb_start_dma(dsp, 1, 1, ADPCM_4, dsp.sb_data[0] + (dsp.sb_data[1] << 8));
                // pcem bug, reproduced: PB-90 — `octet | DMA_OVER` si le compte du 8237 finit sur cet octet.
                dsp.sbdat2 = sb_8_read_dma(dsp);
                dsp.sb_8_length--;
                break;
        case 0x7F: /*2.6-bit ADPCM autoinit output*/
                if (dsp.sb_type < SB15)
                        break;
                // pcem bug, reproduced: PB-146 — sb_commands[0x7F] vaut 0, comme pour 1Fh : ni la taille de 48h ni
                //   l'octet de référence.
                sb_start_dma(dsp, 1, 1, ADPCM_26, dsp.sb_data[0] + (dsp.sb_data[1] << 8));
                // pcem bug, reproduced: PB-90 — `octet | DMA_OVER` si le compte du 8237 finit sur cet octet.
                dsp.sbdat2 = sb_8_read_dma(dsp);
                dsp.sb_8_length--;
                break;
        case 0x80: /*Pause DAC*/
                dsp.sb_pausetime = dsp.sb_data[0] + (dsp.sb_data[1] << 8);
                //                pclog("SB pause %04X\n",sb_pausetime);
                if (timer_is_enabled(dsp.output_timer) == 0)
                        timer_set_delay_u64(dsp.output_timer, dsp.sblatcho);
                break;
        case 0x90: /*High speed 8-bit autoinit DMA output*/
                if (dsp.sb_type < SB2)
                        break;
                // pcem bug, reproduced: PB-241 — la grande vitesse n'arrête pas les commandes : le DSP 2.01 à
                //   3.xx n'en prend plus jusqu'au reset (ou la fin d'un 91h), qui restaure alors l'état antérieur.
                sb_start_dma(dsp, 1, 1, 0, dsp.sb_8_autolen);
                break;
        case 0x91: /*High speed 8-bit single cycle DMA output*/
                if (dsp.sb_type < SB2)
                        break;
                sb_start_dma(dsp, 1, 0, 0, dsp.sb_8_autolen);
                break;
        case 0x98: /*High speed 8-bit autoinit DMA input*/
                if (dsp.sb_type < SB2)
                        break;
                // pcem bug, reproduced: PB-241 — de même à l'entrée (98h, 99h).
                sb_start_dma_i(dsp, 1, 1, 0, dsp.sb_8_autolen);
                break;
        case 0x99: /*High speed 8-bit single cycle DMA input*/
                if (dsp.sb_type < SB2)
                        break;
                sb_start_dma_i(dsp, 1, 0, 0, dsp.sb_8_autolen);
                break;
        case 0xA0: /*Set input mode to mono*/
        case 0xA8: /*Set input mode to stereo*/
                // pcem bug, reproduced: PB-148 — A0h et A8h ne font rien, et la garde admet la 2.0 (DSP 2.01) quand le
                //   guide les réserve au 3.xx.
                if (dsp.sb_type < SB2 || dsp.sb_type > SBPRO2)
                        break;
                // TODO: Implement. 3.xx-only command.
                break;
        case 0xB0:
        case 0xB1:
        case 0xB2:
        case 0xB3:
        case 0xB4:
        case 0xB5:
        case 0xB6:
        case 0xB7: /*16-bit DMA output*/
                if (dsp.sb_type < SB16)
                        break;
                // pcem bug, reproduced: PB-149 — l'octet de mode (sb_data[0]) n'est pas masqué.
                sb_start_dma(dsp, 0, dsp.sb_command & 4, dsp.sb_data[0], dsp.sb_data[1] + (dsp.sb_data[2] << 8));
                dsp.sb_16_autolen = dsp.sb_data[1] + (dsp.sb_data[2] << 8);
                break;
        case 0xB8:
        case 0xB9:
        case 0xBA:
        case 0xBB:
        case 0xBC:
        case 0xBD:
        case 0xBE:
        case 0xBF: /*16-bit DMA input*/
                if (dsp.sb_type < SB16)
                        break;
                // pcem bug, reproduced: PB-149 — l'octet de mode n'est pas masqué : hors de 00h, 10h, 20h et 30h,
                //   sb_poll_i n'a pas de case, ni fin ni IRQ.
                sb_start_dma_i(dsp, 0, dsp.sb_command & 4, dsp.sb_data[0], dsp.sb_data[1] + (dsp.sb_data[2] << 8));
                dsp.sb_16_autolen = dsp.sb_data[1] + (dsp.sb_data[2] << 8);
                break;
        case 0xC0:
        case 0xC1:
        case 0xC2:
        case 0xC3:
        case 0xC4:
        case 0xC5:
        case 0xC6:
        case 0xC7: /*8-bit DMA output*/
                if (dsp.sb_type < SB16)
                        break;
                // pcem bug, reproduced: PB-149 — 01h à 03h prennent le chemin ADPCM, sur un sbdat2 et un sbref périmés.
                sb_start_dma(dsp, 1, dsp.sb_command & 4, dsp.sb_data[0], dsp.sb_data[1] + (dsp.sb_data[2] << 8));
                dsp.sb_8_autolen = dsp.sb_data[1] + (dsp.sb_data[2] << 8);
                break;
        case 0xC8:
        case 0xC9:
        case 0xCA:
        case 0xCB:
        case 0xCC:
        case 0xCD:
        case 0xCE:
        case 0xCF: /*8-bit DMA input*/
                if (dsp.sb_type < SB16)
                        break;
                // pcem bug, reproduced: PB-149 — l'octet de mode n'est pas masqué : hors de 00h, 10h, 20h et 30h,
                //   sb_poll_i n'a pas de case, ni fin ni IRQ.
                sb_start_dma_i(dsp, 1, dsp.sb_command & 4, dsp.sb_data[0], dsp.sb_data[1] + (dsp.sb_data[2] << 8));
                dsp.sb_8_autolen = dsp.sb_data[1] + (dsp.sb_data[2] << 8);
                break;
        case 0xD0: /*Pause 8-bit DMA*/
                dsp.sb_8_pause = 1;
                break;
        case 0xD1: /*Speaker on*/
                // pcem bug, reproduced: PB-147 — sur la SB 1.0, D1h et D3h ne font que la pause du DMA, celle du guide
                //   (p. 6-25) : la sortie n'est ni reliée ni coupée.
                // pcem bug, reproduced: PB-244 — sur-le-champ ; le guide donne jusqu'à 112 ms.
                if (dsp.sb_type < SB15)
                        dsp.sb_8_pause = 1;
                else if (dsp.sb_type < SB16)
                        dsp.muted = 0;
                dsp.sb_speaker = 1;
                break;
        case 0xD3: /*Speaker off*/
                // pcem bug, reproduced: PB-147 — sur la SB 1.0, la pause seule : D3h ne coupe pas le son.
                // pcem bug, reproduced: PB-244 — sur-le-champ ; le guide donne jusqu'à 220 ms.
                if (dsp.sb_type < SB15)
                        dsp.sb_8_pause = 1;
                else if (dsp.sb_type < SB16)
                        dsp.muted = 1;
                dsp.sb_speaker = 0;
                break;
        case 0xD4: /*Continue 8-bit DMA*/
                dsp.sb_8_pause = 0;
                break;
        case 0xD5: /*Pause 16-bit DMA*/
                if (dsp.sb_type < SB16)
                        break;
                dsp.sb_16_pause = 1;
                break;
        case 0xD6: /*Continue 16-bit DMA*/
                if (dsp.sb_type < SB16)
                        break;
                dsp.sb_16_pause = 0;
                break;
        case 0xD8: /*Get speaker status*/
                // pcem bug, reproduced: PB-242 — sans garde de version : le guide ne donne D8h qu'à partir du
                //   DSP 2.00 ; ce qu'en fait le 1.05 est inconnu.
                sb_add_data(dsp, (uint8_t)(dsp.sb_speaker != 0 ? 0xff : 0));
                break;
        case 0xD9: /*Exit 16-bit auto-init mode*/
                if (dsp.sb_type < SB16)
                        break;
                dsp.sb_16_autoinit = 0;
                break;
        case 0xDA: /*Exit 8-bit auto-init mode*/
                dsp.sb_8_autoinit = 0;
                break;
        case 0xE0: /*DSP identification*/
                sb_add_data(dsp, (uint8_t)~dsp.sb_data[0]);
                break;
        case 0xE1: /*Get DSP version*/
                if (IS_AZTECH(dsp)) {
                        // omitted: :572-578 — Aztech.
                        break;
                }
                sb_add_data(dsp, (uint8_t)(sb_dsp_versions[dsp.sb_type] >> 8));
                sb_add_data(dsp, (uint8_t)(sb_dsp_versions[dsp.sb_type] & 0xff));
                break;
        case 0xE2: /*Stupid ID/protection*/
                for (c = 0; c < 8; c++)
                        if ((dsp.sb_data[0] & (1 << c)) != 0)
                                dsp.sbe2 = (uint8_t)(dsp.sbe2 + sbe2dat[dsp.sbe2count & 3][c]);
                dsp.sbe2 = (uint8_t)(dsp.sbe2 + sbe2dat[dsp.sbe2count & 3][8]);
                dsp.sbe2count++;
                sb_8_write_dma(dsp, dsp.sbe2);
                break;
        case 0xE3: /*DSP copyright*/
                if (dsp.sb_type < SB16)
                        break;
                c = 0;
                while (c < sb16_copyright.Length)
                        sb_add_data(dsp, (uint8_t)sb16_copyright[c++]);
                sb_add_data(dsp, 0);
                break;
        case 0xE4: /*Write test register*/
                dsp.sb_test = dsp.sb_data[0];
                break;
        case 0xE8: /*Read test register*/
                sb_add_data(dsp, dsp.sb_test);
                break;
        case 0xF2: /*Trigger 8-bit IRQ*/
                   //                pclog("Trigger IRQ\n");
                sb_irq(dsp, 1);
                break;
        case 0xF3: /*Trigger 16-bit IRQ*/
                   //                pclog("Trigger IRQ\n");
                sb_irq(dsp, 0);
                break;
        case 0xE7: /*???*/
        case 0xFA: /*???*/
                break;
        case 0x07: /*No, that's not how you program auto-init DMA*/
        case 0xFF:
                break;
        case 0x08: /*ASP get version*/
                if (IS_AZTECH(dsp)) {
                        // omitted: :622-645 — Aztech (EEPROM, type de puce).
                        break;
                }
                if (dsp.sb_type < SB16)
                        break;
                // pcem bug, reproduced: PB-165 — 18h ; le DSP 4.xx rend le port 82h de son bus X, inconnu sans la puce
                //   (FFh sur une ViBRA 16 sans ASP, 10h avec, selon DOSBox-X).
                sb_add_data(dsp, 0x18);
                break;
        case 0x0E: /*ASP set register*/
                if (dsp.sb_type < SB16)
                        break;
                dsp.sb_asp_regs[dsp.sb_data[0]] = dsp.sb_data[1];
                //                pclog("ASP write reg %02X %02X\n", sb_data[0], sb_data[1]);
                break;
        case 0x0F: /*ASP get register*/
                if (dsp.sb_type < SB16)
                        break;
                //                sb_add_data(0);
                sb_add_data(dsp, dsp.sb_asp_regs[dsp.sb_data[0]]);
                //                pclog("ASP read reg %02X %02X\n", sb_data[0], sb_asp_regs[sb_data[0]]);
                break;
        case 0xF8:
                if (dsp.sb_type >= SB16)
                        break;
                sb_add_data(dsp, 0);
                break;
        case 0xF9:
                if (dsp.sb_type < SB16)
                        break;
                if (dsp.sb_data[0] == 0x0e)
                        sb_add_data(dsp, 0xff);
                else if (dsp.sb_data[0] == 0x0f)
                        sb_add_data(dsp, 0x07);
                else if (dsp.sb_data[0] == 0x37)
                        sb_add_data(dsp, 0x38);
                else
                        sb_add_data(dsp, 0x00);
                // Le C tombe ensuite dans `case 0x04: case 0x05: break;` (:681-683) : un break.
                break;
        case 0x04:
        case 0x05:
                break;
        case 0x09: /*AZTECH mode set*/
                if (IS_AZTECH(dsp)) {
                        // omitted: :686-693 — Aztech (azt2316a_enable_wss).
                }
                break;
        case 0x38: /*TODO: AZTECH MIDI-related? */
                // pcem bug, reproduced: PB-240 — 38h (« Send MIDI data ») ne fait rien.
                break;
                //                default:
                //                fatal("Exec bad SB command %02X\n",dsp->sb_command);
        }
    }

    // pcem: sound_sb_dsp.c:717-765
    internal static void sb_write(uint16_t a, uint8_t v, object priv)
    {
        sb_dsp_t dsp = (sb_dsp_t)priv;
        //        pclog("sb_write : Write soundblaster %04X %02X %04X:%04X %02X\n",a,v,CS,pc,dsp->sb_command);
        // pcem bug, reproduced: PB-243 — avant la SB 16, 2x7h et 2xDh répètent 2x6h et 2xCh (DOSBox-X, mesuré
        //   sur une SB 2.0 et une Pro) ; ici, ils ne font rien.
        switch (a & 0xF) {
        case 6: /*Reset*/
                if ((v & 1) == 0 && (dsp.sbreset & 1) != 0) {
                        sb_dsp_reset(dsp);
                        sb_add_data(dsp, 0xAA);
                }
                dsp.sbreset = v;
                return;
        case 0xC: /*Command/data write*/
                timer_set_delay_u64(dsp.wb_timer, TIMER_USEC * 1);
                if (dsp.asp_data_len != 0) {
                        //                        pclog("ASP data %i\n", dsp->asp_data_len);
                        dsp.asp_data_len--;
                        if (dsp.asp_data_len == 0)
                                sb_add_data(dsp, 0);
                        return;
                }
                if (dsp.sb_data_stat == -1) {
                        dsp.sb_command = v;
                        if (v == 0x01)
                                sb_add_data(dsp, 0);
                        //                        if (sb_commands[v]==-1)
                        //                           fatal("Bad SB command %02X\n",v);
                        dsp.sb_data_stat++;
                } else {
                        dsp.sb_data[dsp.sb_data_stat++] = v;
                        if (IS_AZTECH(dsp)) {
                                // omitted: :747-751 — longueur variable de la commande 0x08 Aztech.
                        }
                }
                if (dsp.sb_data_stat == sb_commands[dsp.sb_command] || sb_commands[dsp.sb_command] == -1) {
                        sb_exec_command(dsp);
                        dsp.sb_data_stat = -1;
                        if (IS_AZTECH(dsp)) {
                                // omitted: :758-760 — Aztech.
                        }
                }
                break;
        }
    }

    // pcem: sound_sb_dsp.c:767-816
    internal static uint8_t sb_read(uint16_t a, object priv)
    {
        sb_dsp_t dsp = (sb_dsp_t)priv;
        //        pclog("sb_read : Read soundblaster %04X %04X:%04X\n",a,CS,pc);
        // pcem bug, reproduced: PB-243 — avant la SB 16, 2x7h, 2xBh, 2xDh et 2xFh répètent 2x6h, 2xAh, 2xCh et
        //   2xEh ; ici, les trois premiers rendent 0, et 2xFh acquitte l'IRQ 16 bits sur toutes les cartes.
        switch (a & 0xf) {
        case 0xA: /*Read data*/
                dsp.sbreaddat = dsp.sb_read_data[dsp.sb_read_rp];
                if (dsp.sb_read_rp != dsp.sb_read_wp) {
                        dsp.sb_read_rp++;
                        dsp.sb_read_rp &= 0xFF;
                }
                //                pclog("SB read %02X\n",sbreaddat);
                return dsp.sbreaddat;
        case 0xC: /*Write data ready*/
                if (dsp.sb_8_enable != 0 || dsp.sb_type >= SB16)
                        dsp.busy_count = (dsp.busy_count + 1) & 3;
                else
                        dsp.busy_count = 0;
                if (dsp.wb_full != 0 || (dsp.busy_count & 2) != 0) {
                        dsp.wb_full = timer_is_enabled(dsp.wb_timer);
                        //                        pclog("SB read 0x80\n");
                        if (IS_AZTECH(dsp))
                                return 0x80;
                        else
                                return 0xFF;
                }
                //                pclog("SB read 0x00\n");
                if (IS_AZTECH(dsp))
                        return 0x00;
                else
                        return 0x7F;
        case 0xE: /*Read data ready*/
                // pcem bug, reproduced: PB-238 — 2xEh n'acquitte que l'IRQ 8 bits (et SB-MIDI) ; ici, l'IRQ
                //   16 bits aussi, et la ligne que partagent les sources retombe au PIC.
                picintc((uint16_t)(1 << dsp.sb_irqnum));
                dsp.sb_irq8 = dsp.sb_irq16 = 0;
                // Only bit 7 is defined but aztech diagnostics fail if the others are set. Keep the original behavior to not
                // interfere with what's already working.
                if (IS_AZTECH(dsp)) {
                        //                        pclog("SB read %02X\n",(dsp->sb_read_rp == dsp->sb_read_wp) ? 0x00 : 0x80);
                        return (uint8_t)((dsp.sb_read_rp == dsp.sb_read_wp) ? 0x00 : 0x80);
                } else {
                        //                        pclog("SB read %02X\n",(dsp->sb_read_rp == dsp->sb_read_wp) ? 0x7F : 0xFF);
                        return (uint8_t)((dsp.sb_read_rp == dsp.sb_read_wp) ? 0x7F : 0xFF);
                }
        case 0xF: /*16-bit ack*/
                dsp.sb_irq16 = 0;
                if (dsp.sb_irq8 == 0)
                        picintc((uint16_t)(1 << dsp.sb_irqnum));
                return 0xff;
        }
        return 0;
    }

    // pcem: sound_sb_dsp.c:818
    private static void sb_wb_clear(object? p) { }

    // pcem: sound_sb_dsp.c:820-839
    internal static void sb_dsp_init(sb_dsp_t dsp, int type, int subtype, object? parent)
    {
        dsp.sb_type = type;
        dsp.sb_subtype = subtype;
        dsp.parent = parent;

        // Default values. Use sb_dsp_setxxx() methods to change.
        dsp.sb_irqnum = 7;
        dsp.sb_8_dmanum = 1;
        dsp.sb_16_dmanum = 5;

        sb_doreset(dsp);

        timer_add(dsp.output_timer, pollsb, dsp, 0);
        timer_add(dsp.input_timer, sb_poll_i, dsp, 0);
        timer_add(dsp.wb_timer, sb_wb_clear, dsp, 0);

        /*Initialise SB16 filter to same cutoff as 8-bit SBs (3.2 kHz). This will be recalculated when
          a set frequency command is sent.*/
        recalc_sb16_filter(3200 * 2);
    }

    // pcem: sound_sb_dsp.c:841-850
    internal static void sb_dsp_setaddr(sb_dsp_t dsp, uint16_t addr)
    {
        //        pclog("sb_dsp_setaddr : %04X\n", addr);
        io_removehandler((uint16_t)(dsp.sb_addr + 6), 0x0002, sb_read, null, null, sb_write, null, null, dsp);
        io_removehandler((uint16_t)(dsp.sb_addr + 0xa), 0x0006, sb_read, null, null, sb_write, null, null, dsp);
        dsp.sb_addr = addr;
        if (dsp.sb_addr != 0) {
                io_sethandler((uint16_t)(dsp.sb_addr + 6), 0x0002, sb_read, null, null, sb_write, null, null, dsp);
                io_sethandler((uint16_t)(dsp.sb_addr + 0xa), 0x0006, sb_read, null, null, sb_write, null, null, dsp);
        }
    }

    // pcem: sound_sb_dsp.c:852
    internal static void sb_dsp_set_stereo(sb_dsp_t dsp, int stereo) { dsp.stereo = stereo; }

    // pcem: sound_sb_dsp.c:854-1108
    internal static void pollsb(object? p)
    {
        sb_dsp_t dsp = (sb_dsp_t)p;
        int tempi, @ref;

        timer_advance_u64(dsp.output_timer, dsp.sblatcho);

        //        pclog("PollSB %i %i %i %i\n",sb_8_enable,sb_8_pause,sb_pausetime,sb_8_output);
        if (dsp.sb_8_enable != 0 && dsp.sb_8_pause == 0 && dsp.sb_pausetime < 0 && dsp.sb_8_output != 0) {
                Span<int> data = stackalloc int[2];

                sb_dsp_update(dsp);
                //                pclog("Dopoll %i %02X %i\n", sb_8_length, sb_8_format, sblatcho);
                switch (dsp.sb_8_format) {
                case 0x00: /*Mono unsigned*/
                        data[0] = sb_8_read_dma(dsp);
                        /*Needed to prevent clicking in Worms, which programs the DSP to
                          auto-init DMA but programs the DMA controller to single cycle*/
                        if (data[0] == DMA_NODATA)
                                break;
                        // `data | DMA_OVER` au dernier octet : 0x10000 << 8 sort des 16 bits et
                        //   la conversion en int16_t l'efface, comme en C.
                        dsp.sbdat = (int16_t)((data[0] ^ 0x80) << 8);
                        if (dsp.sb_type >= SBPRO && dsp.sb_type < SB16 && dsp.stereo != 0) {
                                if (dsp.sbleftright != 0)
                                        dsp.sbdatl = dsp.sbdat;
                                else
                                        dsp.sbdatr = dsp.sbdat;
                                dsp.sbleftright = dsp.sbleftright == 0 ? 1 : 0;
                        } else
                                dsp.sbdatl = dsp.sbdatr = dsp.sbdat;
                        dsp.sb_8_length--;
                        break;
                case 0x10: /*Mono signed*/
                        data[0] = sb_8_read_dma(dsp);
                        if (data[0] == DMA_NODATA)
                                break;
                        dsp.sbdat = (int16_t)(data[0] << 8);
                        if (dsp.sb_type >= SBPRO && dsp.sb_type < SB16 && dsp.stereo != 0) {
                                if (dsp.sbleftright != 0)
                                        dsp.sbdatl = dsp.sbdat;
                                else
                                        dsp.sbdatr = dsp.sbdat;
                                dsp.sbleftright = dsp.sbleftright == 0 ? 1 : 0;
                        } else
                                dsp.sbdatl = dsp.sbdatr = dsp.sbdat;
                        dsp.sb_8_length--;
                        break;
                case 0x20: /*Stereo unsigned*/
                        data[0] = sb_8_read_dma(dsp);
                        data[1] = sb_8_read_dma(dsp);
                        if (data[0] == DMA_NODATA || data[1] == DMA_NODATA)
                                break;
                        dsp.sbdatl = (int16_t)((data[0] ^ 0x80) << 8);
                        dsp.sbdatr = (int16_t)((data[1] ^ 0x80) << 8);
                        dsp.sb_8_length -= 2;
                        break;
                case 0x30: /*Stereo signed*/
                        data[0] = sb_8_read_dma(dsp);
                        data[1] = sb_8_read_dma(dsp);
                        if (data[0] == DMA_NODATA || data[1] == DMA_NODATA)
                                break;
                        dsp.sbdatl = (int16_t)(data[0] << 8);
                        dsp.sbdatr = (int16_t)(data[1] << 8);
                        dsp.sb_8_length -= 2;
                        break;

                case ADPCM_4:
                        if (dsp.sbdacpos != 0)
                                tempi = (dsp.sbdat2 & 0xF) + dsp.sbstep;
                        else
                                tempi = (dsp.sbdat2 >> 4) + dsp.sbstep;
                        if (tempi < 0)
                                tempi = 0;
                        if (tempi > 63)
                                tempi = 63;

                        @ref = dsp.sbref + scaleMap4[tempi];
                        if (@ref > 0xff)
                                dsp.sbref = 0xff;
                        else if (@ref < 0x00)
                                dsp.sbref = 0x00;
                        else
                                dsp.sbref = (uint8_t)@ref;

                        dsp.sbstep = (int8_t)((dsp.sbstep + adjustMap4[tempi]) & 0xff);

                        dsp.sbdat = (int16_t)((dsp.sbref ^ 0x80) << 8);

                        dsp.sbdacpos++;
                        if (dsp.sbdacpos >= 2) {
                                dsp.sbdacpos = 0;
                                // pcem bug, reproduced: PB-90 — à l'octet qui finit le compte du 8237, dma_channel_read
                                //   rend `octet | DMA_OVER` (0x10000) : sbdat2 >> 4 vaut alors 0x1000 + quartet,
                                //   tempi sature à 63 et l'échantillon suivant saute de scaleMap4[63].
                                // pcem bug, reproduced: PB-235 — DMA_NODATA (-1) est décodé et compté ; le
                                //   DSP attend l'octet (les chemins PCM, eux, sautent le tic).
                                dsp.sbdat2 = sb_8_read_dma(dsp);
                                dsp.sb_8_length--;
                        }

                        if (dsp.sb_type >= SBPRO && dsp.sb_type < SB16 && dsp.stereo != 0) {
                                if (dsp.sbleftright != 0)
                                        dsp.sbdatl = dsp.sbdat;
                                else
                                        dsp.sbdatr = dsp.sbdat;
                                dsp.sbleftright = dsp.sbleftright == 0 ? 1 : 0;
                        } else
                                dsp.sbdatl = dsp.sbdatr = dsp.sbdat;
                        break;

                case ADPCM_26:
                        if (dsp.sbdacpos == 0)
                                tempi = (dsp.sbdat2 >> 5) + dsp.sbstep;
                        else if (dsp.sbdacpos == 1)
                                tempi = ((dsp.sbdat2 >> 2) & 7) + dsp.sbstep;
                        else
                                tempi = ((dsp.sbdat2 << 1) & 7) + dsp.sbstep;

                        if (tempi < 0)
                                tempi = 0;
                        if (tempi > 39)
                                tempi = 39;

                        @ref = dsp.sbref + scaleMap26[tempi];
                        if (@ref > 0xff)
                                dsp.sbref = 0xff;
                        else if (@ref < 0x00)
                                dsp.sbref = 0x00;
                        else
                                dsp.sbref = (uint8_t)@ref;
                        dsp.sbstep = (int8_t)((dsp.sbstep + adjustMap26[tempi]) & 0xff);

                        dsp.sbdat = (int16_t)((dsp.sbref ^ 0x80) << 8);

                        dsp.sbdacpos++;
                        if (dsp.sbdacpos >= 3) {
                                dsp.sbdacpos = 0;
                                // pcem bug, reproduced: PB-90 — `octet | DMA_OVER` au terme du compte du 8237 :
                                //   sbdat2 >> 5 vaut 0x800 + ..., tempi sature à 39.
                                // pcem bug, reproduced: PB-235 — DMA_NODATA (-1) est décodé et compté ; le
                                //   DSP attend l'octet.
                                dsp.sbdat2 = sb_8_read_dma(dsp);
                                dsp.sb_8_length--;
                        }

                        if (dsp.sb_type >= SBPRO && dsp.sb_type < SB16 && dsp.stereo != 0) {
                                if (dsp.sbleftright != 0)
                                        dsp.sbdatl = dsp.sbdat;
                                else
                                        dsp.sbdatr = dsp.sbdat;
                                dsp.sbleftright = dsp.sbleftright == 0 ? 1 : 0;
                        } else
                                dsp.sbdatl = dsp.sbdatr = dsp.sbdat;
                        break;

                case ADPCM_2:
                        tempi = ((dsp.sbdat2 >> ((3 - dsp.sbdacpos) * 2)) & 3) + dsp.sbstep;
                        if (tempi < 0)
                                tempi = 0;
                        if (tempi > 23)
                                tempi = 23;

                        @ref = dsp.sbref + scaleMap2[tempi];
                        if (@ref > 0xff)
                                dsp.sbref = 0xff;
                        else if (@ref < 0x00)
                                dsp.sbref = 0x00;
                        else
                                dsp.sbref = (uint8_t)@ref;
                        dsp.sbstep = (int8_t)((dsp.sbstep + adjustMap2[tempi]) & 0xff);

                        dsp.sbdat = (int16_t)((dsp.sbref ^ 0x80) << 8);

                        dsp.sbdacpos++;
                        if (dsp.sbdacpos >= 4) {
                                dsp.sbdacpos = 0;
                                // pcem bug, reproduced: PB-91 — sb_8_length n'est jamais décrémenté ici
                                //   (cf. ADPCM_4 :944, ADPCM_26 :985) : 0x16/0x17 ne finissent jamais, sans IRQ ;
                                //   0x1F ne recharge jamais. Et DMA_OVER entre dans sbdat2 comme à :943/:984 (PB-90 ;
                                //   masqué ici par le `& 3` de :999, sans effet audible).
                                // pcem bug, reproduced: PB-235 — DMA_NODATA (-1) est décodé ; le DSP attend
                                //   l'octet.
                                dsp.sbdat2 = sb_8_read_dma(dsp);
                        }

                        if (dsp.sb_type >= SBPRO && dsp.sb_type < SB16 && dsp.stereo != 0) {
                                if (dsp.sbleftright != 0)
                                        dsp.sbdatl = dsp.sbdat;
                                else
                                        dsp.sbdatr = dsp.sbdat;
                                dsp.sbleftright = dsp.sbleftright == 0 ? 1 : 0;
                        } else
                                dsp.sbdatl = dsp.sbdatr = dsp.sbdat;
                        break;

                        //                        default:
                        // fatal("Unrecognised SB 8-bit format %02X\n",sb_8_format);
                }

                // pcem bug, reproduced: PB-234 — en ADPCM, le bloc finit à la lecture de son dernier octet,
                //   avant de le jouer (en simple cycle, jamais) ; le DSP 2.02 lève l'IRQ après son dernier échantillon.
                if (dsp.sb_8_length < 0) {
                        if (dsp.sb_8_autoinit != 0)
                                dsp.sb_8_length = dsp.sb_8_autolen;
                        else {
                                dsp.sb_8_enable = 0;
                                timer_disable(dsp.output_timer);
                        }
                        sb_irq(dsp, 1);
                }
        }
        if (dsp.sb_16_enable != 0 && dsp.sb_16_pause == 0 && dsp.sb_pausetime < 0 && dsp.sb_16_output != 0) {
                // pcem: sound_sb_dsp.c:1047-1097 — G12.1. `data | DMA_OVER` (0x10000) disparaît à la conversion en
                //   int16_t de sbdatl et sbdatr : pas de pendant 16 bits à PB-90.
                Span<int> data = stackalloc int[2];

                sb_dsp_update(dsp);

                switch (dsp.sb_16_format) {
                case 0x00: /*Mono unsigned*/
                        data[0] = sb_16_read_dma(dsp);
                        if (data[0] == DMA_NODATA)
                                break;
                        dsp.sbdatl = dsp.sbdatr = (int16_t)(data[0] ^ 0x8000);
                        dsp.sb_16_length--;
                        break;
                case 0x10: /*Mono signed*/
                        data[0] = sb_16_read_dma(dsp);
                        if (data[0] == DMA_NODATA)
                                break;
                        dsp.sbdatl = dsp.sbdatr = (int16_t)data[0];
                        dsp.sb_16_length--;
                        break;
                case 0x20: /*Stereo unsigned*/
                        data[0] = sb_16_read_dma(dsp);
                        data[1] = sb_16_read_dma(dsp);
                        if (data[0] == DMA_NODATA || data[1] == DMA_NODATA)
                                break;
                        dsp.sbdatl = (int16_t)(data[0] ^ 0x8000);
                        dsp.sbdatr = (int16_t)(data[1] ^ 0x8000);
                        dsp.sb_16_length -= 2;
                        break;
                case 0x30: /*Stereo signed*/
                        data[0] = sb_16_read_dma(dsp);
                        data[1] = sb_16_read_dma(dsp);
                        if (data[0] == DMA_NODATA || data[1] == DMA_NODATA)
                                break;
                        dsp.sbdatl = (int16_t)data[0];
                        dsp.sbdatr = (int16_t)data[1];
                        dsp.sb_16_length -= 2;
                        break;
                        //                        default:
                        //                                fatal("Unrecognised SB 16-bit format %02X\n",sb_16_format);
                // pcem bug, reproduced: PB-149 — un format hors de 00/10/20/30 n'a pas de case : la longueur ne
                //   bouge plus, aucune IRQ.
                }

                if (dsp.sb_16_length < 0) {
                        //                        pclog("16DMA over %i\n",dsp->sb_16_autoinit);
                        if (dsp.sb_16_autoinit != 0)
                                dsp.sb_16_length = dsp.sb_16_autolen;
                        else {
                                dsp.sb_16_enable = 0;
                                timer_disable(dsp.output_timer);
                        }
                        sb_irq(dsp, 0);
                }
        }
        if (dsp.sb_pausetime > -1) {
                dsp.sb_pausetime--;
                if (dsp.sb_pausetime < 0) {
                        sb_irq(dsp, 1);
                        if (dsp.sb_8_enable == 0)
                                timer_disable(dsp.output_timer);
                        //                        pclog("SB pause over\n");
                }
        }
    }

    // pcem: sound_sb_dsp.c:1110-1252
    internal static void sb_poll_i(object? p)
    {
        sb_dsp_t dsp = (sb_dsp_t)p;
        int processed = 0;

        timer_advance_u64(dsp.input_timer, dsp.sblatchi);

        //        pclog("PollSBi %i %i %i %i\n",sb_8_enable,sb_8_pause,sb_pausetime,sb_8_output);
        if (dsp.sb_8_enable != 0 && dsp.sb_8_pause == 0 && dsp.sb_pausetime < 0 && dsp.sb_8_output == 0) {
                // omitted: SB_TEST_RECORDING_SAW (:1119-1134) — #ifdef éteint ; la branche #else suit.
                switch (dsp.sb_8_format) {
                case 0x00: /*Mono unsigned As the manual says, only the left channel is recorded*/
                        sb_8_write_dma(dsp, (uint8_t)((dsp.record_buffer[dsp.record_pos_read] >> 8) ^ 0x80));
                        dsp.sb_8_length--;
                        dsp.record_pos_read += 2;
                        dsp.record_pos_read &= 0xFFFF;
                        break;
                case 0x10: /*Mono signed As the manual says, only the left channel is recorded*/
                        sb_8_write_dma(dsp, (uint8_t)(dsp.record_buffer[dsp.record_pos_read] >> 8));
                        dsp.sb_8_length--;
                        dsp.record_pos_read += 2;
                        dsp.record_pos_read &= 0xFFFF;
                        break;
                // Les formats stéréo lisent record_buffer[record_pos_read + 1], soit l'indice 0xFFFF quand
                //   record_pos_read vaut 0xFFFE : record_lu (PB-151). Atteints par les commandes 0xC8-0xCF de la
                //   SB 16 (G12.1) : sur la SBPRO2, sb_8_format ne vaut en entrée que 0 (0x24, 0x2C, 0x98, 0x99).
                case 0x20: /*Stereo unsigned*/
                        sb_8_write_dma(dsp, (uint8_t)((dsp.record_buffer[dsp.record_pos_read] >> 8) ^ 0x80));
                        // pcem bug, reproduced: PB-151 — l'indice 0xFFFF lit buffer[0] (record_lu).
                        sb_8_write_dma(dsp, (uint8_t)((record_lu(dsp, dsp.record_pos_read + 1) >> 8) ^ 0x80));
                        dsp.sb_8_length -= 2;
                        dsp.record_pos_read += 2;
                        dsp.record_pos_read &= 0xFFFF;
                        break;
                case 0x30: /*Stereo signed*/
                        sb_8_write_dma(dsp, (uint8_t)(dsp.record_buffer[dsp.record_pos_read] >> 8));
                        // pcem bug, reproduced: PB-151 — l'indice 0xFFFF lit buffer[0] (record_lu).
                        sb_8_write_dma(dsp, (uint8_t)(record_lu(dsp, dsp.record_pos_read + 1) >> 8));
                        dsp.sb_8_length -= 2;
                        dsp.record_pos_read += 2;
                        dsp.record_pos_read &= 0xFFFF;
                        break;
                        //                        default:
                        //                                fatal("Unrecognised SB 8-bit input format %02X\n",sb_8_format);
                }

                if (dsp.sb_8_length < 0) {
                        //                        pclog("Input DMA over %i\n",sb_8_autoinit);
                        if (dsp.sb_8_autoinit != 0)
                                dsp.sb_8_length = dsp.sb_8_autolen;
                        else {
                                dsp.sb_8_enable = 0;
                                timer_disable(dsp.input_timer);
                        }
                        sb_irq(dsp, 1);
                }
                processed = 1;
        }
        if (dsp.sb_16_enable != 0 && dsp.sb_16_pause == 0 && dsp.sb_pausetime < 0 && dsp.sb_16_output == 0) {
                // pcem: sound_sb_dsp.c:1180-1246 — G12.1. Le `return` sur DMA_NODATA saute aussi `processed = 1` et
                //   l'avance du mode direct (:1248-1251), comme en C.
                // omitted: SB_TEST_RECORDING_SAW (:1181-1198) — #ifdef éteint ; la branche #else suit.
                switch (dsp.sb_16_format) {
                case 0x00: /*Unsigned mono. As the manual says, only the left channel is recorded*/
                        if (sb_16_write_dma(dsp, (uint16_t)(dsp.record_buffer[dsp.record_pos_read] ^ 0x8000)) != 0)
                                return;
                        dsp.sb_16_length--;
                        dsp.record_pos_read += 2;
                        dsp.record_pos_read &= 0xFFFF;
                        break;
                case 0x10: /*Signed mono. As the manual says, only the left channel is recorded*/
                        if (sb_16_write_dma(dsp, (uint16_t)dsp.record_buffer[dsp.record_pos_read]) != 0)
                                return;
                        dsp.sb_16_length--;
                        dsp.record_pos_read += 2;
                        dsp.record_pos_read &= 0xFFFF;
                        break;
                case 0x20: /*Unsigned stereo*/
                        if (sb_16_write_dma(dsp, (uint16_t)(dsp.record_buffer[dsp.record_pos_read] ^ 0x8000)) != 0)
                                return;
                        // pcem bug, reproduced: PB-151 — l'indice 0xFFFF lit buffer[0] (record_lu).
                        sb_16_write_dma(dsp, (uint16_t)(record_lu(dsp, dsp.record_pos_read + 1) ^ 0x8000));
                        dsp.sb_16_length -= 2;
                        dsp.record_pos_read += 2;
                        dsp.record_pos_read &= 0xFFFF;
                        break;
                case 0x30: /*Signed stereo*/
                        if (sb_16_write_dma(dsp, (uint16_t)dsp.record_buffer[dsp.record_pos_read]) != 0)
                                return;
                        // pcem bug, reproduced: PB-151 — l'indice 0xFFFF lit buffer[0] (record_lu).
                        sb_16_write_dma(dsp, (uint16_t)record_lu(dsp, dsp.record_pos_read + 1));
                        dsp.sb_16_length -= 2;
                        dsp.record_pos_read += 2;
                        dsp.record_pos_read &= 0xFFFF;
                        break;
                        //                        default:
                        //                                fatal("Unrecognised SB 16-bit input format %02X\n",sb_16_format);
                }

                if (dsp.sb_16_length < 0) {
                        //                        pclog("16iDMA over %i\n",sb_16_autoinit);
                        if (dsp.sb_16_autoinit != 0)
                                dsp.sb_16_length = dsp.sb_16_autolen;
                        else {
                                dsp.sb_16_enable = 0;
                                timer_disable(dsp.input_timer);
                        }
                        sb_irq(dsp, 0);
                }
                processed = 1;
        }
        // Assume this is direct mode
        if (processed == 0) {
                dsp.record_pos_read += 2;
                dsp.record_pos_read &= 0xFFFF;
        }
    }

    // pcem bug, reproduced: PB-151 — record_buffer[0xFFFF] lit un élément au-delà du tableau de 0xFFFF : dans la
    //   disposition du C, buffer[0], un échantillon de SORTIE (sound_sb_dsp.h:82-83, int16_t sans bourrage ;
    //   l'assertion statique de harness.c le fige). Seuls les formats stéréo d'entrée l'atteignent.
    private static int16_t record_lu(sb_dsp_t dsp, int i) => i < 0xFFFF ? dsp.record_buffer[i] : dsp.buffer[i - 0xFFFF];

    // pcem: sound_sb_dsp.c:1254-1263
    internal static void sb_dsp_update(sb_dsp_t dsp)
    {
        if (dsp.muted != 0) {
                dsp.sbdatl = 0;
                dsp.sbdatr = 0;
        }
        for (; dsp.pos < sound_pos_global; dsp.pos++) {
                dsp.buffer[dsp.pos * 2] = dsp.sbdatl;
                dsp.buffer[dsp.pos * 2 + 1] = dsp.sbdatr;
        }
    }
    // pcem: sound_sb_dsp.c:1264-1271
    internal static void sb_dsp_close(sb_dsp_t dsp)
    {
        // omitted: SB_DSP_RECORD_DEBUG (:1265-1270) — #ifdef éteint.
    }

    // omitted: sb_dsp_add_status_info (sound_sb_dsp.c:1273-1326) — texte d'état de l'hôte.
}
