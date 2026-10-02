// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/sound/sound_opl.c + includes/private/sound/sound_opl.h
// STATUS: transcribed — opl_t, opl2_read/write, opl2_l_*/opl2_r_*, opl3_read/write,
//         opl2_update2, opl3_update2, ym3812_timer_set_0/1, ymf262_timer_set,
//         opl_timer_callback00..11, opl2_init, opl3_init (sound_opl.c:1-134).
//         opl2_poll/opl3_poll (sound_opl.h:24-25) sont déclarés sans être définis.

// CS8600 : `(opl_t *)priv` part du `object` des delegates d'io.cs et de timer.cs, comme
// à vid_cga.cs. CS8602 : même raison, à la déréférence qui suit (`param` des
// ym*_timer_set, `object?` comme le `void *` de sound_dbopl.cc:16).
#pragma warning disable CS8600, CS8602

using iXtal26.Models;
using static iXtal26.Cpu.x86;
using static iXtal26.Sound.sound;
using static iXtal26.Sound.sound_dbopl;
using static iXtal26.timer;

namespace iXtal26.Sound;

// pcem: sound_opl.h:5-14
// Classe et non struct : son adresse est prise (io_sethandler, timer_add, opl_init).
internal sealed class opl_t
{
    internal readonly int[] chip_nr = new int[2];

    internal readonly pc_timer_t[][] timers =
    {
        new pc_timer_t[] { new pc_timer_t(), new pc_timer_t() },
        new pc_timer_t[] { new pc_timer_t(), new pc_timer_t() },
    };

    internal readonly int16_t[] filtbuf = new int16_t[2];

    internal readonly int16_t[] buffer = new int16_t[MAXSOUNDBUFLEN * 2];
    internal int pos;
}

internal static partial class sound_opl
{
    // pcem: sound_opl.h:32-33
    internal const int OPL_DBOPL = 0;
    internal const int OPL_NUKED = 1;

    /*Interfaces between PCem and the actual OPL emulator*/

    // pcem: sound_opl.c:11-17
    internal static uint8_t opl2_read(uint16_t a, object priv)
    {
        opl_t opl = (opl_t)priv;

        cycles -= (int)(pit.isa_timing * 8);
        opl2_update2(opl);
        return opl_read(0, a);
    }
    // pcem: sound_opl.c:18-24
    internal static void opl2_write(uint16_t a, uint8_t v, object priv)
    {
        opl_t opl = (opl_t)priv;

        opl2_update2(opl);
        opl_write(0, a, v);
        opl_write(1, a, v);
    }

    // pcem: sound_opl.c:26-32
    internal static uint8_t opl2_l_read(uint16_t a, object priv)
    {
        opl_t opl = (opl_t)priv;

        cycles -= (int)(pit.isa_timing * 8);
        opl2_update2(opl);
        return opl_read(0, a);
    }
    // pcem: sound_opl.c:33-38
    internal static void opl2_l_write(uint16_t a, uint8_t v, object priv)
    {
        opl_t opl = (opl_t)priv;

        opl2_update2(opl);
        opl_write(0, a, v);
    }

    // pcem: sound_opl.c:40-46
    internal static uint8_t opl2_r_read(uint16_t a, object priv)
    {
        opl_t opl = (opl_t)priv;

        cycles -= (int)(pit.isa_timing * 8);
        opl2_update2(opl);
        return opl_read(1, a);
    }
    // pcem: sound_opl.c:47-52
    internal static void opl2_r_write(uint16_t a, uint8_t v, object priv)
    {
        opl_t opl = (opl_t)priv;

        opl2_update2(opl);
        opl_write(1, a, v);
    }

    // pcem: sound_opl.c:54-60
    internal static uint8_t opl3_read(uint16_t a, object priv)
    {
        opl_t opl = (opl_t)priv;

        cycles -= (int)(pit.isa_timing * 8);
        opl3_update2(opl);
        return opl_read(0, a);
    }
    // pcem: sound_opl.c:61-66
    internal static void opl3_write(uint16_t a, uint8_t v, object priv)
    {
        opl_t opl = (opl_t)priv;

        opl3_update2(opl);
        opl_write(0, a, v);
    }

    // pcem: sound_opl.c:68-77. `&opl->buffer[n]` devient (opl.buffer, n).
    internal static void opl2_update2(opl_t opl)
    {
        if (opl.pos < sound_pos_global) {
                opl2_update(0, opl.buffer, opl.pos * 2, sound_pos_global - opl.pos);
                opl2_update(1, opl.buffer, opl.pos * 2 + 1, sound_pos_global - opl.pos);
                for (; opl.pos < sound_pos_global; opl.pos++) {
                        opl.filtbuf[0] = opl.buffer[opl.pos * 2] = (int16_t)(opl.buffer[opl.pos * 2] / 2);
                        opl.filtbuf[1] = opl.buffer[opl.pos * 2 + 1] = (int16_t)(opl.buffer[opl.pos * 2 + 1] / 2);
                }
        }
    }

    // pcem: sound_opl.c:79-87
    internal static void opl3_update2(opl_t opl)
    {
        if (opl.pos < sound_pos_global) {
                opl3_update(0, opl.buffer, opl.pos * 2, sound_pos_global - opl.pos);
                for (; opl.pos < sound_pos_global; opl.pos++) {
                        opl.filtbuf[0] = opl.buffer[opl.pos * 2] = (int16_t)(opl.buffer[opl.pos * 2] / 2);
                        opl.filtbuf[1] = opl.buffer[opl.pos * 2 + 1] = (int16_t)(opl.buffer[opl.pos * 2 + 1] / 2);
                }
        }
    }

    // pcem: sound_opl.c:89-96. `period * TIMER_USEC * 20` : int64_t * uint64_t, calcul non signé.
    internal static void ym3812_timer_set_0(object? param, int timer, int64_t period)
    {
        opl_t opl = (opl_t)param;

        if (period != 0)
                timer_set_delay_u64(opl.timers[0][timer], (uint64_t)period * TIMER_USEC * 20);
        else
                timer_disable(opl.timers[0][timer]);
    }
    // pcem: sound_opl.c:97-104
    internal static void ym3812_timer_set_1(object? param, int timer, int64_t period)
    {
        opl_t opl = (opl_t)param;

        if (period != 0)
                timer_set_delay_u64(opl.timers[1][timer], (uint64_t)period * TIMER_USEC * 20);
        else
                timer_disable(opl.timers[1][timer]);
    }

    // pcem: sound_opl.c:106-113
    internal static void ymf262_timer_set(object? param, int timer, int64_t period)
    {
        opl_t opl = (opl_t)param;

        if (period != 0)
                timer_set_delay_u64(opl.timers[0][timer], (uint64_t)period * TIMER_USEC * 20);
        else
                timer_disable(opl.timers[0][timer]);
    }

    // pcem: sound_opl.c:115-118
    private static void opl_timer_callback00(object? p) { opl_timer_over(0, 0); }
    private static void opl_timer_callback01(object? p) { opl_timer_over(0, 1); }
    private static void opl_timer_callback10(object? p) { opl_timer_over(1, 0); }
    private static void opl_timer_callback11(object? p) { opl_timer_over(1, 1); }

    // pcem: sound_opl.c:120-127
    internal static void opl2_init(opl_t opl)
    {
        opl_init(ym3812_timer_set_0, opl, 0, 0, 0);
        opl_init(ym3812_timer_set_1, opl, 1, 0, 0);
        timer_add(opl.timers[0][0], opl_timer_callback00, opl, 0);
        timer_add(opl.timers[0][1], opl_timer_callback01, opl, 0);
        timer_add(opl.timers[1][0], opl_timer_callback10, opl, 0);
        timer_add(opl.timers[1][1], opl_timer_callback11, opl, 0);
    }

    // pcem: sound_opl.c:129-133. opl_emu : OPL_DBOPL seul est câblé (voir sound_dbopl.cs).
    internal static void opl3_init(opl_t opl, int opl_emu)
    {
        opl_init(ymf262_timer_set, opl, 0, 1, opl_emu);
        timer_add(opl.timers[0][0], opl_timer_callback00, opl, 0);
        timer_add(opl.timers[0][1], opl_timer_callback01, opl, 0);
    }
}
