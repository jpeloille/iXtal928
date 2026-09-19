// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/keyboard/keyboard_xt.c
// STATUS: partial — PPI du XT (0x60 SW1, 0x61 gate/turbo/reset, 0x62 SW2), file
//         de 16 scancodes, poll. Omis : t1000_syskey, tandy_eeprom_read, pclog.

using static iXtal26.Cpu.cpu;
using static iXtal26.Devices.cassette;
using static iXtal26.Keyboard.keyboard;
using static iXtal26.Memory.mem;
using static iXtal26.Models.pic;
using static iXtal26.Models.pit;
using static iXtal26.Sound.sound_speaker;
using static iXtal26.Video.video;
using static iXtal26.io;
using static iXtal26.pc;
using static iXtal26.ppi_c;   // le conteneur s'appelle ppi_c : le typedef PPI de ibm.h prend le nom ppi
using static iXtal26.timer;

namespace iXtal26.Keyboard;

// pcem: keyboard_xt.c:28-41 — la struct anonyme de PCem. Classe et non struct :
// timer_add prend l'adresse de send_delay_timer et la range dans sa liste.
// pcem: keyboard_xt.c — mémorise l'état précédent du gate haut-parleur
// pour ne déclencher speaker_update() que sur un front.
internal static partial class keyboard_xt { internal static int was_speaker_enable; }

internal sealed class keyboard_xt_t
{
    internal int wantirq;
    internal uint8_t key_waiting;

    internal uint8_t pa;
    internal uint8_t pb;

    internal int shift_full;

    internal int tandy;
    internal int pb2_turbo;

    internal pc_timer_t send_delay_timer = new();
}

internal static partial class keyboard_xt
{
    // pcem: keyboard_xt.c:19-26
    private const int STAT_PARITY = 0x80;
    private const int STAT_RTIMEOUT = 0x40;
    private const int STAT_TTIMEOUT = 0x20;
    private const int STAT_LOCK = 0x10;
    private const int STAT_CD = 0x08;
    private const int STAT_SYSFLAG = 0x04;
    private const int STAT_IFULL = 0x02;
    private const int STAT_OFULL = 0x01;

    // pcem: keyboard_xt.c:28-41
    // CS0542: la globale `keyboard_xt` (keyboard_xt.c:41) ne peut pas porter le
    // nom de la classe conteneur. Suffixe `_`, comme pic_ (pic.cs) et pit_ (pit.cs).
    internal static readonly keyboard_xt_t keyboard_xt_ = new();

    // pcem: keyboard_xt.c:43-44
    private static uint8_t[] key_queue = new uint8_t[16];
    private static int key_queue_start = 0, key_queue_end = 0;

    // pcem: keyboard_xt.c:46-63
    internal static void keyboard_xt_poll()
    {
        timer_advance_u64(keyboard_xt_.send_delay_timer, (1000 * TIMER_USEC));
        if ((keyboard_xt_.pb & 0x40) == 0 && romset != ROM_TANDY)
                return;
        if (keyboard_xt_.wantirq != 0)
        {
                keyboard_xt_.wantirq = 0;
                keyboard_xt_.pa = keyboard_xt_.key_waiting;
                keyboard_xt_.shift_full = 1;
                picint(2);
                // omitted: pclog("keyboard_xt : take IRQ\n") — sortie pure.
        }
        if (key_queue_start != key_queue_end && keyboard_xt_.shift_full == 0)
        {
                keyboard_xt_.key_waiting = key_queue[key_queue_start];
                // omitted: pclog("Reading %02X from the key queue at %i\n", ...) — sortie pure.
                key_queue_start = (key_queue_start + 1) & 0xf;
                keyboard_xt_.wantirq = 1;
        }
    }

    // pcem: keyboard_xt.c:65-103
    internal static void keyboard_xt_adddata(uint8_t val)
    {
        // omitted: keyboard_xt.c:66-97 — le test de la touche 'Fn' du T1000/T1200
        //          (t1000_syskey, src/models/t1000.c), hors cible 5150.

        key_queue[key_queue_end] = val;
        // omitted: pclog("keyboard_xt : %02X added to key queue at %i\n", ...) — sortie pure.
        key_queue_end = (key_queue_end + 1) & 0xf;
        return;
    }

    // pcem: keyboard_xt.c:105-147
    internal static void keyboard_xt_write(uint16_t port, uint8_t val, object priv)
    {
        switch (port)
        {
        case 0x61:
                if ((keyboard_xt_.pb & 0x40) == 0 && (val & 0x40) != 0) /*Reset keyboard*/
                {
                        // omitted: pclog("keyboard_xt : reset keyboard\n") — sortie pure.
                        key_queue_start = key_queue_end = 0;
                        keyboard_xt_.wantirq = 0;
                        keyboard_xt_.shift_full = 0;
                        keyboard_xt_adddata(0xaa);
                }
                keyboard_xt_.pb = val;
                ppi.pb = val;

                timer_process();

                if (romset == ROM_IBMPC)
                        cassette_set_motor((uint8_t)((val & 8) != 0 ? 0 : 1));
                else if (keyboard_xt_.pb2_turbo != 0)
                        cpu_set_turbo((val & 4) != 0 ? 0 : 1);

                speaker_update();
                speaker_gated = val & 1;
                speaker_enable = val & 2;
                if (speaker_enable != 0)
                        was_speaker_enable = 1;
                pit_set_gate(pit_, 2, val & 1);

                if ((val & 0x80) != 0)
                {
                        keyboard_xt_.pa = 0;
                        keyboard_xt_.shift_full = 0;
                        picintc(2);
                }
                break;
        }
    }

    // pcem: keyboard_xt.c:149-213
    internal static uint8_t keyboard_xt_read(uint16_t port, object priv)
    {
        uint8_t temp = 0xff;
        switch (port)
        {
        case 0x60:
                if ((romset == ROM_IBMPC || romset == ROM_LEDGE_MODELM) && (keyboard_xt_.pb & 0x80) != 0)
                {
                        if (video_is_ega_vga() != 0)
                                temp = 0x4D;
                        else if (video_is_mda() != 0)
                                temp = 0x7D;
                        else
                                temp = 0x6D;
                        if (hasfpu != 0)
                                temp |= 0x02;
                }
                else if ((romset == ROM_ATARIPC3) && (keyboard_xt_.pb & 0x80) != 0)
                {
                        temp = 0x7f;
                }
                else
                {
                        temp = keyboard_xt_.pa;
                }
                break;

        case 0x61:
                temp = keyboard_xt_.pb;
                break;

        case 0x62:
                if (romset == ROM_IBMPC)
                {
                        if ((keyboard_xt_.pb & 0x04) != 0)
                                temp = (uint8_t)(((mem_size - 64) / 32) & 0xf);
                        else
                                temp = (uint8_t)(((mem_size - 64) / 32) >> 4);

                        temp |= (uint8_t)((cassette_input() != 0) ? 0x10 : 0);
                }
                else if (romset == ROM_LEDGE_MODELM)
                {
                        /*High bit of memory size is read from port 0xa0*/
                        temp = (uint8_t)(((mem_size - 64) / 32) & 0xf);
                }
                else if (romset == ROM_ATARIPC3)
                {
                        if ((keyboard_xt_.pb & 0x04) != 0)
                                temp = 0xf;
                        else
                                temp = 4;
                }
                else
                {
                        if ((keyboard_xt_.pb & 0x08) != 0)
                        {
                                if (video_is_ega_vga() != 0)
                                        temp = 4;
                                else if (video_is_mda() != 0)
                                        temp = 7;
                                else
                                        temp = 6;
                        }
                        else
                                temp = (uint8_t)(hasfpu != 0 ? 0xf : 0xd);
                }
                temp |= (uint8_t)(ppispeakon != 0 ? 0x20 : 0);
                // omitted: keyboard_xt.c:202-203 — tandy_eeprom_read()
                //          (src/devices/tandy_eeprom.c), hors cible 5150.
                break;

        // omitted: default: pclog("\nBad XT keyboard read %04X\n", port) — sortie pure.
        }
        return temp;
    }

    // pcem: keyboard_xt.c:215-222
    private static uint8_t ledge_modelm_read(uint16_t port, object p)
    {
        uint8_t temp = 0;

        if ((((mem_size - 64) / 32) >> 4) != 0)
                temp |= 0x40;

        return temp;
    }

    // pcem: keyboard_xt.c:224-228
    internal static void keyboard_xt_reset()
    {
        keyboard_xt_.wantirq = 0;

        keyboard_scan = 1;
    }

    // DEVIATION: keyboard_xt.c:241 et 252 castent `void (*)()` en
    // `void (*)(void *)` pour timer_add. Le cast n'existe pas en C# et
    // keyboard_xt_poll doit rester sans paramètre pour keyboard_poll :
    // adaptateur nommé.
    private static void keyboard_xt_poll_timer(object? p) => keyboard_xt_poll();

    // pcem: keyboard_xt.c:230-242
    internal static void keyboard_xt_init()
    {
        io_sethandler(0x0060, 0x0004, keyboard_xt_read, null, null, keyboard_xt_write, null, null, null);
        if (romset == ROM_LEDGE_MODELM)
                io_sethandler(0x00a0, 0x0001, ledge_modelm_read, null, null, null, null, null, null);
        keyboard_xt_reset();
        keyboard_send = keyboard_xt_adddata;
        keyboard_poll = keyboard_xt_poll;
        keyboard_xt_.tandy = 0;
        keyboard_xt_.pb2_turbo = (romset == ROM_GENXT || romset == ROM_DTKXT || romset == ROM_AMIXT || romset == ROM_PXXT) ? 1 : 0;

        timer_add(keyboard_xt_.send_delay_timer, keyboard_xt_poll_timer, null, 1);
    }

    // pcem: keyboard_xt.c:244-253
    internal static void keyboard_tandy_init()
    {
        io_sethandler(0x0060, 0x0004, keyboard_xt_read, null, null, keyboard_xt_write, null, null, null);
        keyboard_xt_reset();
        keyboard_send = keyboard_xt_adddata;
        keyboard_poll = keyboard_xt_poll;
        keyboard_xt_.tandy = (romset != ROM_TANDY) ? 1 : 0;

        timer_add(keyboard_xt_.send_delay_timer, keyboard_xt_poll_timer, null, 1);
    }
}
