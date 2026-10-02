// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/keyboard/keyboard_olim24.c + includes/private/keyboard/keyboard_olim24.h
// STATUS: transcribed — le clavier de la M24 (0x60, 0x61, 0x64), sa file de 16
//         scancodes, son poll, et la souris M24 (mouse_olim24_poll, mouse_olim24).
//         Omis : les pclog, sortie pure.

using iXtal26.Mouse;
using static iXtal26.Keyboard.keyboard;
using static iXtal26.Models.pic;
using static iXtal26.Models.pit;
using static iXtal26.Sound.sound_speaker;
using static iXtal26.ppi_c;
using static iXtal26.timer;

namespace iXtal26.Keyboard;

// pcem: keyboard_olim24.c:24-38 — la struct anonyme de PCem. Classe et non struct :
// timer_add prend l'adresse de send_delay_timer et la range dans sa liste.
internal sealed class keyboard_olim24_t
{
    internal int wantirq;
    internal uint8_t command;
    internal uint8_t status;
    internal uint8_t @out;

    internal uint8_t output_port;

    internal int param, param_total;
    internal uint8_t[] @params = new uint8_t[16];

    internal int mouse_mode;

    internal pc_timer_t send_delay_timer = new();
}

// pcem: keyboard_olim24.c:203-205
internal sealed class mouse_olim24_t
{
    internal int x, y, b;
}

internal static partial class keyboard_olim24
{
    // pcem: keyboard_olim24.c:15-22
    private const int STAT_PARITY = 0x80;
    private const int STAT_RTIMEOUT = 0x40;
    private const int STAT_TTIMEOUT = 0x20;
    private const int STAT_LOCK = 0x10;
    private const int STAT_CD = 0x08;
    private const int STAT_SYSFLAG = 0x04;
    private const int STAT_IFULL = 0x02;
    private const int STAT_OFULL = 0x01;

    // pcem: keyboard_olim24.c:24-38
    // CS0542: la globale `keyboard_olim24` (keyboard_olim24.c:38) ne peut pas porter
    // le nom de la classe conteneur. Suffixe `_`, comme keyboard_xt_ (keyboard_xt.cs).
    internal static readonly keyboard_olim24_t keyboard_olim24_ = new();

    // pcem: keyboard_olim24.c:40-41
    private static uint8_t[] key_queue = new uint8_t[16];
    private static int key_queue_start = 0, key_queue_end = 0;

    // pcem: keyboard_olim24.c:43
    private static uint8_t[] mouse_scancodes = new uint8_t[7];

    // pcem: keyboard_olim24.c:45-61
    internal static void keyboard_olim24_poll()
    {
        timer_advance_u64(keyboard_olim24_.send_delay_timer, (1000 * TIMER_USEC));
        if (keyboard_olim24_.wantirq != 0)
        {
                keyboard_olim24_.wantirq = 0;
                picint(2);
                // omitted: pclog("keyboard_olim24 : take IRQ\n") — sortie pure.
        }
        if ((keyboard_olim24_.status & STAT_OFULL) == 0 && key_queue_start != key_queue_end)
        {
                // omitted: pclog("Reading %02X from the key queue at %i\n", ...) — sortie pure.
                keyboard_olim24_.@out = key_queue[key_queue_start];
                key_queue_start = (key_queue_start + 1) & 0xf;
                keyboard_olim24_.status |= STAT_OFULL;
                keyboard_olim24_.status &= unchecked((uint8_t)~STAT_IFULL);
                keyboard_olim24_.wantirq = 1;
        }
    }

    // pcem: keyboard_olim24.c:63-68
    // Reproduit : aucun contrôle de débordement. Au 16e octet en attente,
    // key_queue_end rattrape key_queue_start et la file paraît vide — son contenu
    // est perdu, sans accès hors du tableau (masque 0xf).
    internal static void keyboard_olim24_adddata(uint8_t val)
    {
        key_queue[key_queue_end] = val;
        key_queue_end = (key_queue_end + 1) & 0xf;
        // omitted: pclog("keyboard_olim24 : %02X added to key queue %02X\n", ...) — sortie pure.
        return;
    }

    // pcem: keyboard_olim24.c:70-148
    internal static void keyboard_olim24_write(uint16_t port, uint8_t val, object priv)
    {
        // omitted: pclog("keyboard_olim24 : write %04X %02X\n", port, val) — sortie pure.
        switch (port)
        {
        case 0x60:
                if (keyboard_olim24_.param != keyboard_olim24_.param_total)
                {
                        keyboard_olim24_.@params[keyboard_olim24_.param++] = val;
                        if (keyboard_olim24_.param == keyboard_olim24_.param_total)
                        {
                                switch (keyboard_olim24_.command)
                                {
                                case 0x11:
                                        keyboard_olim24_.mouse_mode = 0;
                                        mouse_scancodes[0] = keyboard_olim24_.@params[0];
                                        mouse_scancodes[1] = keyboard_olim24_.@params[1];
                                        mouse_scancodes[2] = keyboard_olim24_.@params[2];
                                        mouse_scancodes[3] = keyboard_olim24_.@params[3];
                                        mouse_scancodes[4] = keyboard_olim24_.@params[4];
                                        mouse_scancodes[5] = keyboard_olim24_.@params[5];
                                        mouse_scancodes[6] = keyboard_olim24_.@params[6];
                                        break;

                                case 0x12:
                                        keyboard_olim24_.mouse_mode = 1;
                                        mouse_scancodes[0] = keyboard_olim24_.@params[0];
                                        mouse_scancodes[1] = keyboard_olim24_.@params[1];
                                        mouse_scancodes[2] = keyboard_olim24_.@params[2];
                                        break;

                                default:
                                        // omitted: pclog("Bad keyboard command complete %02X\n", ...) — sortie pure.
                                        break;
                                }
                        }
                }
                else
                {
                        keyboard_olim24_.command = val;
                        switch (val)
                        {
                        case 0x01: /*Self-test*/
                                break;

                        case 0x05: /*Read ID*/
                                keyboard_olim24_adddata(0x00);
                                break;

                        case 0x11:
                                keyboard_olim24_.param = 0;
                                keyboard_olim24_.param_total = 9;
                                break;

                        case 0x12:
                                keyboard_olim24_.param = 0;
                                keyboard_olim24_.param_total = 4;
                                break;

                        default:
                                // omitted: pclog("Bad keyboard command %02X\n", val) — sortie pure.
                                break;
                        }
                }

                break;

        case 0x61:
                ppi.pb = val;

                timer_process();

                speaker_update();
                speaker_gated = val & 1;
                speaker_enable = val & 2;
                if (speaker_enable != 0)
                        was_speaker_enable = 1;
                pit_set_gate(pit_, 2, val & 1);
                break;
        }
    }

    // pcem: keyboard_olim24.c:150-183
    internal static uint8_t keyboard_olim24_read(uint16_t port, object priv)
    {
        uint8_t temp = 0xff;
        switch (port)
        {
        case 0x60:
                temp = keyboard_olim24_.@out;
                if (key_queue_start == key_queue_end)
                {
                        keyboard_olim24_.status &= unchecked((uint8_t)~STAT_OFULL);
                        keyboard_olim24_.wantirq = 0;
                }
                else
                {
                        keyboard_olim24_.@out = key_queue[key_queue_start];
                        key_queue_start = (key_queue_start + 1) & 0xf;
                        keyboard_olim24_.status |= STAT_OFULL;
                        keyboard_olim24_.status &= unchecked((uint8_t)~STAT_IFULL);
                        keyboard_olim24_.wantirq = 1;
                }
                break;

        case 0x61:
                return ppi.pb;

        case 0x64:
                temp = keyboard_olim24_.status;
                keyboard_olim24_.status &= unchecked((uint8_t)~(STAT_RTIMEOUT | STAT_TTIMEOUT));
                break;

        default:
                // omitted: pclog("\nBad olim24 keyboard read %04X\n", port) — sortie pure.
                break;
        }
        return temp;
    }

    // pcem: keyboard_olim24.c:185-201
    internal static void keyboard_olim24_reset()
    {
        keyboard_olim24_.status = STAT_LOCK | STAT_CD;
        keyboard_olim24_.wantirq = 0;

        keyboard_scan = 1;

        keyboard_olim24_.param = keyboard_olim24_.param_total = 0;

        keyboard_olim24_.mouse_mode = 0;
        mouse_scancodes[0] = 0x1c;
        mouse_scancodes[1] = 0x53;
        mouse_scancodes[2] = 0x01;
        mouse_scancodes[3] = 0x4b;
        mouse_scancodes[4] = 0x4d;
        mouse_scancodes[5] = 0x48;
        mouse_scancodes[6] = 0x50;
    }

    // pcem: keyboard_olim24.c:207-292
    internal static void mouse_olim24_poll(int x, int y, int z, int b, object p)
    {
        mouse_olim24_t mouse = (mouse_olim24_t)p;

        mouse.x += x;
        mouse.y += y;

        if (((key_queue_end - key_queue_start) & 0xf) > 14)
                return;
        if ((b & 1) != 0 && (mouse.b & 1) == 0)
                keyboard_olim24_adddata(mouse_scancodes[0]);
        if ((b & 1) == 0 && (mouse.b & 1) != 0)
                keyboard_olim24_adddata((uint8_t)(mouse_scancodes[0] | 0x80));
        mouse.b = (mouse.b & ~1) | (b & 1);

        if (((key_queue_end - key_queue_start) & 0xf) > 14)
                return;
        if ((b & 2) != 0 && (mouse.b & 2) == 0)
                keyboard_olim24_adddata(mouse_scancodes[2]);
        if ((b & 2) == 0 && (mouse.b & 2) != 0)
                keyboard_olim24_adddata((uint8_t)(mouse_scancodes[2] | 0x80));
        mouse.b = (mouse.b & ~2) | (b & 2);

        if (((key_queue_end - key_queue_start) & 0xf) > 14)
                return;
        if ((b & 4) != 0 && (mouse.b & 4) == 0)
                keyboard_olim24_adddata(mouse_scancodes[1]);
        if ((b & 4) == 0 && (mouse.b & 4) != 0)
                keyboard_olim24_adddata((uint8_t)(mouse_scancodes[1] | 0x80));
        mouse.b = (mouse.b & ~4) | (b & 4);

        if (keyboard_olim24_.mouse_mode != 0)
        {
                if (((key_queue_end - key_queue_start) & 0xf) > 12)
                        return;
                if (mouse.x == 0 && mouse.y == 0)
                        return;

                mouse.y = -mouse.y;

                if (mouse.x < -127)
                        mouse.x = -127;
                if (mouse.x > 127)
                        mouse.x = 127;
                // pcem bug, reproduced: borne morte (keyboard_olim24.c:251-252) — x vient
                //   d'être ramené dans [-127, 127], le test ne peut plus être vrai. Le
                //   codage signe-amplitude visé n'a jamais lieu : l'octet envoyé ligne 277
                //   est le complément à deux de x.
                if (mouse.x < -127)
                        mouse.x = 0x80 | ((-mouse.x) & 0x7f);

                if (mouse.y < -127)
                        mouse.y = -127;
                if (mouse.y > 127)
                        mouse.y = 127;
                // pcem bug, reproduced: même borne morte pour y (keyboard_olim24.c:258-259).
                if (mouse.y < -127)
                        mouse.y = 0x80 | ((-mouse.y) & 0x7f);

                keyboard_olim24_adddata(0xfe);
                // (uint8_t) : la troncature implicite du C — complément à deux pour un
                // déplacement négatif.
                keyboard_olim24_adddata(unchecked((uint8_t)mouse.x));
                keyboard_olim24_adddata(unchecked((uint8_t)mouse.y));

                mouse.x = mouse.y = 0;
        }
        else
        {
                while (mouse.x < -4)
                {
                        if (((key_queue_end - key_queue_start) & 0xf) > 14)
                                return;
                        mouse.x += 4;
                        keyboard_olim24_adddata(mouse_scancodes[3]);
                }
                while (mouse.x > 4)
                {
                        if (((key_queue_end - key_queue_start) & 0xf) > 14)
                                return;
                        mouse.x -= 4;
                        keyboard_olim24_adddata(mouse_scancodes[4]);
                }
                while (mouse.y < -4)
                {
                        if (((key_queue_end - key_queue_start) & 0xf) > 14)
                                return;
                        mouse.y += 4;
                        keyboard_olim24_adddata(mouse_scancodes[5]);
                }
                while (mouse.y > 4)
                {
                        if (((key_queue_end - key_queue_start) & 0xf) > 14)
                                return;
                        mouse.y -= 4;
                        keyboard_olim24_adddata(mouse_scancodes[6]);
                }
        }
    }

    // pcem: keyboard_olim24.c:294-299
    private static object mouse_olim24_init()
    {
        mouse_olim24_t mouse = new mouse_olim24_t();
        // pcem: keyboard_olim24.c:296 — memset(mouse, 0, …) ; `new` zéro-initialise.

        return mouse;
    }

    // pcem: keyboard_olim24.c:301-305
    private static void mouse_olim24_close(object p)
    {
        // omitted: free(mouse) — libération manuelle, sans objet sous GC.
    }

    // pcem: keyboard_olim24.c:307
    internal static readonly mouse_t mouse_olim24 = new()
    {
        name = "Olivetti M24 mouse", init = mouse_olim24_init, close = mouse_olim24_close,
        poll = mouse_olim24_poll, type = mouse.MOUSE_TYPE_OLIM24,
    };

    // DEVIATION: keyboard_olim24.c:317 caste `void (*)()` en `void (*)(void *)`
    // pour timer_add. Le cast n'existe pas en C# et keyboard_olim24_poll doit rester
    // sans paramètre pour keyboard_poll : adaptateur nommé, comme keyboard_xt.cs.
    private static void keyboard_olim24_poll_timer(object? p) => keyboard_olim24_poll();

    // pcem: keyboard_olim24.c:309-318
    internal static void keyboard_olim24_init()
    {
        io.io_sethandler(0x0060, 0x0002, keyboard_olim24_read, null, null, keyboard_olim24_write, null, null, null);
        io.io_sethandler(0x0064, 0x0001, keyboard_olim24_read, null, null, keyboard_olim24_write, null, null, null);
        keyboard_olim24_reset();
        keyboard_send = keyboard_olim24_adddata;
        keyboard_poll = keyboard_olim24_poll;

        timer_add(keyboard_olim24_.send_delay_timer, keyboard_olim24_poll_timer, null, 1);
    }
}
