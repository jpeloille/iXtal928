// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/keyboard/keyboard_amstrad.c + includes/private/keyboard/keyboard_amstrad.h
// STATUS: transcribed — le PPI de l'Amstrad PC1512 (0x60 clavier / SW1, 0x61, 0x62 SW2,
//         0x63-0x65), sa file de 16 scancodes, son poll, son reset, son init. Le fichier
//         entier. Omis : les pclog, sortie pure.

using static iXtal26.Keyboard.keyboard;
using static iXtal26.Models.pic;
using static iXtal26.Models.pit;
using static iXtal26.Sound.sound_speaker;
using static iXtal26.ppi_c;
using static iXtal26.timer;

namespace iXtal26.Keyboard;

// pcem: keyboard_amstrad.c:22-30 — la struct anonyme de PCem. Classe et non struct :
// timer_add prend l'adresse de send_delay_timer et la range dans sa liste.
internal sealed class keyboard_amstrad_t
{
    internal int wantirq;

    internal uint8_t key_waiting;
    internal uint8_t pa;
    internal uint8_t pb;

    internal pc_timer_t send_delay_timer = new();
}

internal static partial class keyboard_amstrad
{
    // pcem: keyboard_amstrad.c:13-20
    private const int STAT_PARITY = 0x80;
    private const int STAT_RTIMEOUT = 0x40;
    private const int STAT_TTIMEOUT = 0x20;
    private const int STAT_LOCK = 0x10;
    private const int STAT_CD = 0x08;
    private const int STAT_SYSFLAG = 0x04;
    private const int STAT_IFULL = 0x02;
    private const int STAT_OFULL = 0x01;

    // pcem: keyboard_amstrad.c:22-30
    // CS0542: la globale `keyboard_amstrad` (keyboard_amstrad.c:30) ne peut pas porter
    // le nom de la classe conteneur. Suffixe `_`, comme keyboard_xt_ (keyboard_xt.cs).
    internal static readonly keyboard_amstrad_t keyboard_amstrad_ = new();

    // pcem: keyboard_amstrad.c:32-33
    private static uint8_t[] key_queue = new uint8_t[16];
    private static int key_queue_start = 0, key_queue_end = 0;

    // pcem: keyboard_amstrad.c:35
    private static uint8_t amstrad_systemstat_1, amstrad_systemstat_2;

    // pcem: keyboard_amstrad.c:37-51
    internal static void keyboard_amstrad_poll()
    {
        timer_advance_u64(keyboard_amstrad_.send_delay_timer, (1000 * TIMER_USEC));
        if (keyboard_amstrad_.wantirq != 0)
        {
                keyboard_amstrad_.wantirq = 0;
                keyboard_amstrad_.pa = keyboard_amstrad_.key_waiting;
                picint(2);
                // omitted: pclog("keyboard_amstrad : take IRQ\n") — sortie pure.
        }
        if (key_queue_start != key_queue_end && keyboard_amstrad_.pa == 0)
        {
                keyboard_amstrad_.key_waiting = key_queue[key_queue_start];
                // omitted: pclog("Reading %02X from the key queue at %i\n", ...) — sortie pure.
                key_queue_start = (key_queue_start + 1) & 0xf;
                keyboard_amstrad_.wantirq = 1;
        }
    }

    // pcem: keyboard_amstrad.c:53-58
    // Reproduit : aucun contrôle de débordement. Au 16e octet en attente,
    // key_queue_end rattrape key_queue_start et la file paraît vide — son contenu
    // est perdu, sans accès hors du tableau (masque 0xf).
    internal static void keyboard_amstrad_adddata(uint8_t val)
    {
        key_queue[key_queue_end] = val;
        // omitted: pclog("keyboard_amstrad : %02X added to key queue at %i\n", ...) — sortie pure.
        key_queue_end = (key_queue_end + 1) & 0xf;
        return;
    }

    // pcem: keyboard_amstrad.c:60-104
    internal static void keyboard_amstrad_write(uint16_t port, uint8_t val, object priv)
    {
        // omitted: pclog("keyboard_amstrad : write %04X %02X %02X\n", ...) — sortie pure.

        switch (port)
        {
        case 0x61:
                // omitted: pclog("keyboard_amstrad : pb write ...") (keyboard_amstrad.c:65-66) — sortie pure.
                if ((keyboard_amstrad_.pb & 0x40) == 0 && (val & 0x40) != 0) /*Reset keyboard*/
                {
                        // omitted: pclog("keyboard_amstrad : reset keyboard\n") — sortie pure.
                        keyboard_amstrad_adddata(0xaa);
                }
                keyboard_amstrad_.pb = val;
                ppi.pb = val;

                timer_process();

                speaker_update();
                speaker_gated = val & 1;
                speaker_enable = val & 2;
                if (speaker_enable != 0)
                        was_speaker_enable = 1;
                pit_set_gate(pit_, 2, val & 1);

                if ((val & 0x80) != 0)
                        keyboard_amstrad_.pa = 0;
                break;

        case 0x63:
                break;

        case 0x64:
                amstrad_systemstat_1 = val;
                break;

        case 0x65:
                amstrad_systemstat_2 = val;
                break;

        default:
                // omitted: pclog("\nBad XT keyboard write %04X %02X\n", port, val) — sortie pure.
                break;
        }
    }

    // pcem: keyboard_amstrad.c:106-146
    internal static uint8_t keyboard_amstrad_read(uint16_t port, object priv)
    {
        uint8_t temp = 0xff;
        switch (port)
        {
        case 0x60:
                if ((keyboard_amstrad_.pb & 0x80) != 0)
                {
                        temp = (uint8_t)((amstrad_systemstat_1 | 0xd) & 0x7f);
                }
                else
                {
                        temp = keyboard_amstrad_.pa;
                        if (key_queue_start == key_queue_end)
                        {
                                keyboard_amstrad_.wantirq = 0;
                        }
                        else
                        {
                                keyboard_amstrad_.key_waiting = key_queue[key_queue_start];
                                key_queue_start = (key_queue_start + 1) & 0xf;
                                keyboard_amstrad_.wantirq = 1;
                        }
                }
                break;

        case 0x61:
                temp = keyboard_amstrad_.pb;
                break;

        case 0x62:
                if ((keyboard_amstrad_.pb & 0x04) != 0)
                        temp = (uint8_t)(amstrad_systemstat_2 & 0xf);
                else
                        temp = (uint8_t)(amstrad_systemstat_2 >> 4);
                temp |= (uint8_t)(ppispeakon != 0 ? 0x20 : 0);
                if (Cpu._808x.nmi != 0)
                        temp |= 0x40;
                break;

        default:
                // omitted: pclog("\nBad XT keyboard read %04X\n", port) — sortie pure.
                break;
        }
        return temp;
    }

    // pcem: keyboard_amstrad.c:148-152
    internal static void keyboard_amstrad_reset()
    {
        keyboard_amstrad_.wantirq = 0;

        keyboard_scan = 1;
    }

    // DEVIATION: keyboard_amstrad.c:162 caste `void (*)()` en `void (*)(void *)`
    // pour timer_add. Le cast n'existe pas en C# et keyboard_amstrad_poll doit rester
    // sans paramètre pour keyboard_poll : adaptateur nommé, comme keyboard_xt.cs.
    private static void keyboard_amstrad_poll_timer(object? p) => keyboard_amstrad_poll();

    // pcem: keyboard_amstrad.c:154-163
    internal static void keyboard_amstrad_init()
    {
        // omitted: pclog("keyboard_amstrad_init\n") — sortie pure.
        io.io_sethandler(0x0060, 0x0006, keyboard_amstrad_read, null, null, keyboard_amstrad_write, null, null, null);
        keyboard_amstrad_reset();
        keyboard_send = keyboard_amstrad_adddata;
        keyboard_poll = keyboard_amstrad_poll;

        timer_add(keyboard_amstrad_.send_delay_timer, keyboard_amstrad_poll_timer, null, 1);
    }
}
