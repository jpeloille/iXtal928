// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/mouse/mouse_serial.c
// STATUS: transcribed — mouse_serial_t, mouse_serial_poll, mouse_serial_rcr,
//         mousecallback, mouse_serial_init, mouse_serial_close,
//         mouse_serial_microsoft.

using iXtal26.Models;

namespace iXtal26.Mouse;

// pcem: mouse_serial.c:8-13
// Classe et non struct : son adresse est prise (timer_add, rcr_callback_p).
internal sealed class mouse_serial_t
{
    internal int mousepos;
    internal pc_timer_t mousedelay_timer = new();
    internal int oldb;
    internal SERIAL serial = null!;
}

internal static partial class mouse_serial
{
    // pcem: mouse_serial.c:15-54
    internal static void mouse_serial_poll(int x, int y, int z, int b, object p)
    {
        mouse_serial_t mouse = (mouse_serial_t)p;
        SERIAL serial = mouse.serial;
        var mousedat = new uint8_t[3];

        if ((serial.ier & 1) == 0)
                return;
        if (x == 0 && y == 0 && b == mouse.oldb)
                return;

        mouse.oldb = b;
        if (x > 127)
                x = 127;
        if (y > 127)
                y = 127;
        if (x < -128)
                x = -128;
        if (y < -128)
                y = -128;

        /*Use Microsoft format*/
        mousedat[0] = 0x40;
        mousedat[0] |= (uint8_t)(((y >> 6) & 3) << 2);
        mousedat[0] |= (uint8_t)((x >> 6) & 3);
        if ((b & 1) != 0)
                mousedat[0] |= 0x20;
        if ((b & 2) != 0)
                mousedat[0] |= 0x10;
        mousedat[1] = (uint8_t)(x & 0x3F);
        mousedat[2] = (uint8_t)(y & 0x3F);

        if ((serial.mctrl & 0x10) == 0)
        {
                Models.serial.serial_write_fifo(mouse.serial, mousedat[0]);
                Models.serial.serial_write_fifo(mouse.serial, mousedat[1]);
                Models.serial.serial_write_fifo(mouse.serial, mousedat[2]);
        }
    }

    // pcem: mouse_serial.c:56-61
    internal static void mouse_serial_rcr(SERIAL serial, object? p)
    {
        mouse_serial_t mouse = (mouse_serial_t)p!;

        mouse.mousepos = -1;
        timer.timer_set_delay_u64(mouse.mousedelay_timer, timer.TIMER_USEC * 5000);
    }

    // pcem: mouse_serial.c:63-70
    internal static void mousecallback(object? p)
    {
        mouse_serial_t mouse = (mouse_serial_t)p!;

        if (mouse.mousepos == -1)
        {
                mouse.mousepos = 0;
                Models.serial.serial_write_fifo(mouse.serial, (uint8_t)'M');
        }
    }

    // pcem: mouse_serial.c:72-83
    internal static object mouse_serial_init()
    {
        mouse_serial_t mouse = new mouse_serial_t();
        // pcem: mouse_serial.c:74 — memset(mouse, 0, …) ; `new` zéro-initialise.

        mouse.serial = Models.serial.serial1;
        Models.serial.serial1.rcr_callback = mouse_serial_rcr;
        Models.serial.serial1.rcr_callback_p = mouse;
        timer.timer_add(mouse.mousedelay_timer, mousecallback, mouse, 0);

        return mouse;
    }

    // pcem: mouse_serial.c:85-91
    internal static void mouse_serial_close(object p)
    {
        // omitted: free(mouse) — libération manuelle, sans objet sous GC.
        Models.serial.serial1.rcr_callback = null;
    }

    // pcem: mouse_serial.c:93-94
    internal static readonly mouse_t mouse_serial_microsoft = new()
    {
        name = "Microsoft 2-button mouse (serial)", init = mouse_serial_init, close = mouse_serial_close,
        poll = mouse_serial_poll, type = mouse.MOUSE_TYPE_SERIAL,
    };
}
