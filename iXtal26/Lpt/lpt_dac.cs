// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/lpt/lpt_dac.c + includes/private/filters.h:241-270 (dac_iir)
// STATUS: transcribed — lpt_dac_t, dac_update, dac_write_data, dac_write_ctrl, dac_read_status,
//         dac_get_buffer, dac_init, dac_stereo_init, dac_close, lpt_dac_device,
//         lpt_dac_stereo_device ; dac_iir.
//
// G10.0 (PLAN-G10.md). Le Covox Speech Thing : un octet écrit sur le port de données de LPT1
// devient un échantillon ; la version stéréo choisit son canal par le bit 0 du port de contrôle.
//
// PERSISTANCE : l'état du filtre dac_iir est `static` LOCAL à une fonction `static inline` de
// filters.h — une copie par unité de compilation, celle de lpt_dac.c — jamais remis à zéro : il
// traverse les amorçages d'un processus, comme celui de sb_iir (G8). Ici, champs statiques, de même.

using static iXtal26.Sound.sound;

namespace iXtal26.Lpt;

// pcem: lpt_dac.c:8-16
internal sealed class lpt_dac_t
{
    internal uint8_t dac_val_l, dac_val_r;

    internal int is_stereo;
    internal int channel;

    internal int16_t[][] buffer = { new int16_t[MAXSOUNDBUFLEN], new int16_t[MAXSOUNDBUFLEN] };
    internal int pos;
}

internal static class lpt_dac
{
    // pcem: filters.h:244-270 — dac_iir, NCoef = 1 ; état statique local, voir l'en-tête.
    private const int NCoef = 1;
    private static readonly float[] dac_iir_ACoef = { (float)0.99901119820285345000, (float)-0.99901119820285345000 };
    private static readonly float[] dac_iir_BCoef = { (float)1.00000000000000000000, (float)-0.99869185905052738000 };
    private static readonly float[][] dac_iir_y = { new float[NCoef + 1], new float[NCoef + 1] }; // output samples
    private static readonly float[][] dac_iir_x = { new float[NCoef + 1], new float[NCoef + 1] }; // input samples

    private static float dac_iir(int i, float NewSample)
    {
        float[] ACoef = dac_iir_ACoef;
        float[] BCoef = dac_iir_BCoef;
        float[][] y = dac_iir_y;
        float[][] x = dac_iir_x;
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

    // pcem: lpt_dac.c:18-23
    private static void dac_update(lpt_dac_t lpt_dac)
    {
        for (; lpt_dac.pos < sound_pos_global; lpt_dac.pos++)
        {
                lpt_dac.buffer[0][lpt_dac.pos] = (int16_t)((int8_t)(lpt_dac.dac_val_l ^ 0x80) * 0x40);
                lpt_dac.buffer[1][lpt_dac.pos] = (int16_t)((int8_t)(lpt_dac.dac_val_r ^ 0x80) * 0x40);
        }
    }

    // pcem: lpt_dac.c:25-36
    private static void dac_write_data(uint8_t val, object p)
    {
        lpt_dac_t lpt_dac = (lpt_dac_t)p;

        if (lpt_dac.is_stereo != 0)
        {
                if (lpt_dac.channel != 0)
                        lpt_dac.dac_val_r = val;
                else
                        lpt_dac.dac_val_l = val;
        }
        else
                lpt_dac.dac_val_l = lpt_dac.dac_val_r = val;
        dac_update(lpt_dac);
    }

    // pcem: lpt_dac.c:38-43
    private static void dac_write_ctrl(uint8_t val, object p)
    {
        lpt_dac_t lpt_dac = (lpt_dac_t)p;

        if (lpt_dac.is_stereo != 0)
                lpt_dac.channel = val & 0x01;
    }

    // pcem: lpt_dac.c:45
    private static uint8_t dac_read_status(object p) { return 0; }

    // pcem: lpt_dac.c:47-58. `buffer[c * 2] += dac_iir(...)` : un int32_t plus un float, calculé en
    //   float puis tronqué vers l'entier, comme en C.
    private static void dac_get_buffer(int32_t[] buffer, int len, object? p)
    {
        lpt_dac_t lpt_dac = (lpt_dac_t)p!;
        int c;

        dac_update(lpt_dac);

        for (c = 0; c < len; c++)
        {
                buffer[c * 2] = (int32_t)(buffer[c * 2] + dac_iir(0, lpt_dac.buffer[0][c]));
                buffer[c * 2 + 1] = (int32_t)(buffer[c * 2 + 1] + dac_iir(1, lpt_dac.buffer[1][c]));
        }
        lpt_dac.pos = 0;
    }

    // pcem: lpt_dac.c:60-67
    private static object dac_init()
    {
        lpt_dac_t lpt_dac = new lpt_dac_t();
        // pcem: :61-62 — malloc + memset ; `new` zéro-initialise.

        sound_add_handler(dac_get_buffer, lpt_dac);

        return lpt_dac;
    }

    // pcem: lpt_dac.c:68-74
    private static object dac_stereo_init()
    {
        lpt_dac_t lpt_dac = (lpt_dac_t)dac_init();

        lpt_dac.is_stereo = 1;

        return lpt_dac;
    }

    // pcem: lpt_dac.c:75-79 — free, sous GC.
    private static void dac_close(object p) { }

    // pcem: lpt_dac.c:81-94
    internal static readonly lpt_device_t lpt_dac_device = new()
    {
        name = "LPT DAC / Covox Speech Thing", flags = 0, init = dac_init, close = dac_close,
        write_data = dac_write_data, write_ctrl = dac_write_ctrl, read_status = dac_read_status,
    };

    // pcem: lpt_dac.c:95-98
    internal static readonly lpt_device_t lpt_dac_stereo_device = new()
    {
        name = "Stereo LPT DAC", flags = 0, init = dac_stereo_init, close = dac_close,
        write_data = dac_write_data, write_ctrl = dac_write_ctrl, read_status = dac_read_status,
    };
}
