// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/lpt/lpt_dss.c + includes/private/filters.h:218-239 (dss_iir)
// STATUS: transcribed — dss_t, dss_update, dss_write_data, dss_write_ctrl, dss_read_status,
//         dss_get_buffer, dss_callback, dss_init, dss_close, dss_device ; dss_iir.
//
// G10.0 (PLAN-G10.md). La Disney Sound Source : une file de 16 octets, vidée à 7 kHz par un
// chronomètre ; le bit 6 de l'état (379h) dit la file pleine.
//
// PERSISTANCE : l'état de dss_iir (`static` local de filters.h, une copie pour lpt_dss.c) traverse
// les amorçages d'un processus, comme sb_iir (G8) et dac_iir : champs statiques, de même.

using static iXtal26.Sound.sound;
using static iXtal26.timer;

namespace iXtal26.Lpt;

// pcem: lpt_dss.c:8-18
internal sealed class dss_t
{
    internal uint8_t[] fifo = new uint8_t[16];
    internal int read_idx, write_idx;

    internal uint8_t dac_val;

    internal pc_timer_t timer = new();

    internal int16_t[] buffer = new int16_t[MAXSOUNDBUFLEN];
    internal int pos;
}

internal static class lpt_dss
{
    // pcem: filters.h:218-239 — dss_iir, NCoef = 2 ; état statique local, voir l'en-tête.
    // fc=3.2kHz - probably incorrect
    private const int NCoef = 2;
    private static readonly float[] dss_iir_ACoef = { (float)0.03356837051492005100, (float)0.06713674102984010200, (float)0.03356837051492005100 };
    private static readonly float[] dss_iir_BCoef = { (float)1.00000000000000000000, (float)-1.41898265221812010000, (float)0.55326988968868285000 };
    private static readonly float[] dss_iir_y = new float[NCoef + 1]; // output samples
    private static readonly float[] dss_iir_x = new float[NCoef + 1]; // input samples

    private static float dss_iir(float NewSample)
    {
        float[] ACoef = dss_iir_ACoef;
        float[] BCoef = dss_iir_BCoef;
        float[] y = dss_iir_y;
        float[] x = dss_iir_x;
        int n;

        // shift the old samples
        for (n = NCoef; n > 0; n--) {
                x[n] = x[n - 1];
                y[n] = y[n - 1];
        }

        // Calculate the new output
        x[0] = NewSample;
        y[0] = ACoef[0] * x[0];
        for (n = 1; n <= NCoef; n++)
                y[0] += ACoef[n] * x[n] - BCoef[n] * y[n];

        return y[0];
    }

    // pcem: lpt_dss.c:20-23
    private static void dss_update(dss_t dss)
    {
        for (; dss.pos < sound_pos_global; dss.pos++)
                dss.buffer[dss.pos] = (int16_t)((int8_t)(dss.dac_val ^ 0x80) * 0x40);
    }

    // pcem: lpt_dss.c:25-32
    private static void dss_write_data(uint8_t val, object p)
    {
        dss_t dss = (dss_t)p;

        if ((dss.write_idx - dss.read_idx) < 16)
        {
                dss.fifo[dss.write_idx & 15] = val;
                dss.write_idx++;
        }
    }

    // pcem: lpt_dss.c:34
    private static void dss_write_ctrl(uint8_t val, object p) { }

    // pcem: lpt_dss.c:36-42
    private static uint8_t dss_read_status(object p)
    {
        dss_t dss = (dss_t)p;

        if ((dss.write_idx - dss.read_idx) >= 16)
                return 0x40;
        return 0;
    }

    // pcem: lpt_dss.c:44-58
    private static void dss_get_buffer(int32_t[] buffer, int len, object? p)
    {
        dss_t dss = (dss_t)p!;
        int c;

        dss_update(dss);

        for (c = 0; c < len * 2; c += 2)
        {
                int16_t val = (int16_t)dss_iir((float)dss.buffer[c >> 1]);

                buffer[c] += val;
                buffer[c + 1] += val;
        }

        dss.pos = 0;
    }

    // pcem: lpt_dss.c:60-71. Le délai `TIMER_USEC * (1000000.0 / 7000.0)` est un double, converti
    //   en uint64_t à l'appel de timer_advance_u64 (troncature), comme en C.
    private static void dss_callback(object? p)
    {
        dss_t dss = (dss_t)p!;

        dss_update(dss);

        if ((dss.write_idx - dss.read_idx) > 0)
        {
                dss.dac_val = dss.fifo[dss.read_idx & 15];
                dss.read_idx++;
        }

        timer_advance_u64(dss.timer, (uint64_t)(TIMER_USEC * (1000000.0 / 7000.0)));
    }

    // pcem: lpt_dss.c:73-81
    private static object dss_init()
    {
        dss_t dss = new dss_t();
        // pcem: :74-75 — malloc + memset ; `new` zéro-initialise.

        sound_add_handler(dss_get_buffer, dss);
        timer_add(dss.timer, dss_callback, dss, 1);

        return dss;
    }

    // pcem: lpt_dss.c:82-86 — free, sous GC.
    private static void dss_close(object p) { }

    // pcem: lpt_dss.c:88-89
    internal static readonly lpt_device_t dss_device = new()
    {
        name = "Disney Sound Source", flags = 0, init = dss_init, close = dss_close,
        write_data = dss_write_data, write_ctrl = dss_write_ctrl, read_status = dss_read_status,
    };
}
