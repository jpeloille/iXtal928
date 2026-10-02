// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/models/amstrad.c + includes/private/models/amstrad.h
// STATUS: partial — ce que l'Amstrad PC1512 atteint : amstrad_read/amstrad_write,
//         la souris Amstrad (amstrad_mouse_read/write, mouse_amstrad_*), amstrad_init,
//         ams1512_init, ams1512_config, ams1512_device. Omis : le latch du PC1640
//         (amstrad_latch et sa lecture), ams2086/ams3086, les pclog. lpt1_read/lpt1_write : Lpt/lpt.cs,
//         sans périphérique branché.

using iXtal26.Mouse;
using iXtal26.PluginApi;
using static iXtal26.Keyboard.keyboard;
using static iXtal26.PluginApi.device;
using static iXtal26.io;
using static iXtal26.pc;

namespace iXtal26.Models;

// pcem: amstrad.c:106-108
internal sealed class mouse_amstrad_t
{
    internal int oldb;
}

internal static partial class amstrad
{
    // pcem: ibm.h:252 — ROM_PPC512, absent de pc.cs. Valeur lue par le compilateur sur
    // l'énumération de ibm.h (88), pas comptée à la main. Ne sert qu'aux deux gardes
    // PC200/PPC512 ci-dessous, fausses sur un PC1512.
    private const int ROM_PPC512 = 88;

    // pcem: amstrad.c:13
    private static uint8_t amstrad_dead;
    // omitted: `int amstrad_latch = 0;` (amstrad.c:14) — le latch du PC1640. io.c:111-116
    //   le pose à chaque inb quand AMSTRAD, mais seule la branche ROM_PC1640 de
    //   amstrad_read (amstrad.c:40-57) le lit ; io.cs:167-169 l'omet déjà.
    // pcem: amstrad.c:15
    private static uint8_t amstrad_language;

    // pcem: amstrad.c:17-70
    internal static uint8_t amstrad_read(uint16_t port, object priv)
    {
        uint8_t temp;

        // omitted: pclog("amstrad_read : %04X\n", port) — sortie pure.
        switch (port)
        {
        case 0x378:
                return Lpt.lpt.lpt1_read(port, null);
        case 0x379:
                return (uint8_t)(Lpt.lpt.lpt1_read(port, null) | (amstrad_language & 7));
        case 0x37a:
                temp = (uint8_t)(Lpt.lpt.lpt1_read(port, null) & 0x1f);
                if (romset == ROM_PC1512)
                        return (uint8_t)(temp | 0x20);
                if (romset == ROM_PC200 || romset == ROM_PPC512)
                {
                        if (Floppy.fdc_c.fdc_discchange_read() != 0)
                                temp |= 0x20;
                        /* DIP switches 4,5: Initial video mode */
                        if (Video.video.video_is_cga() != 0)
                                temp |= 0x80; /* CGA 80 */
                        else if (Video.video.video_is_mda() != 0)
                                temp |= 0xc0; /* MDA */
                        return temp;
                }
                // omitted: amstrad.c:40-57 — la branche ROM_PC1640 (interrupteurs vidéo et
                //   lecture de amstrad_latch). Machine hors cible ; romset n'y vaut jamais
                //   ROM_PC1640 ici.
                return temp;
        case 0x3de:
                if (romset == ROM_PC200 || romset == ROM_PPC512)
                {
                        /* Read DIP switches / internal display adapter status. This code is only
                         * reached if the IDA is disabled; if it is enabled this read will be
                         * handled by the video card */
                }
                return 0x20; /* Disable IDA */
        case 0xdead:
                return amstrad_dead;
        }
        return 0xff;
    }

    // pcem: amstrad.c:72-88
    internal static void amstrad_write(uint16_t port, uint8_t val, object priv)
    {
        switch (port)
        {
        case 0x66:
                Cpu._808x.softresetx86();
                break;

        case 0x378:
        case 0x379:
        case 0x37a:
                Lpt.lpt.lpt1_write(port, val, null);
                break;

        case 0xdead:
                amstrad_dead = val;
                break;
        }
    }

    // pcem: amstrad.c:90
    private static uint8_t mousex, mousey;

    // pcem: amstrad.c:91-97
    private static void amstrad_mouse_write(uint16_t addr, uint8_t val, object p)
    {
        if (addr == 0x78)
                mousex = 0;
        else
                mousey = 0;
    }

    // pcem: amstrad.c:99-104
    private static uint8_t amstrad_mouse_read(uint16_t addr, object p)
    {
        if (addr == 0x78)
                return mousex;
        return mousey;
    }

    // pcem: amstrad.c:110-126
    private static void mouse_amstrad_poll(int x, int y, int z, int b, object p)
    {
        mouse_amstrad_t mouse = (mouse_amstrad_t)p;

        // (uint8_t) : la troncature implicite du C — les compteurs bouclent modulo 256.
        mousex = unchecked((uint8_t)(mousex + x));
        mousey = unchecked((uint8_t)(mousey - y));

        if ((b & 1) != 0 && (mouse.oldb & 1) == 0)
                keyboard_send(0x7e);
        if ((b & 2) != 0 && (mouse.oldb & 2) == 0)
                keyboard_send(0x7d);
        if ((b & 1) == 0 && (mouse.oldb & 1) != 0)
                keyboard_send(0xfe);
        if ((b & 2) == 0 && (mouse.oldb & 2) != 0)
                keyboard_send(0xfd);

        mouse.oldb = b;
    }

    // pcem: amstrad.c:128-133
    private static object mouse_amstrad_init()
    {
        mouse_amstrad_t mouse = new mouse_amstrad_t();
        // pcem: amstrad.c:130 — memset(mouse, 0, …) ; `new` zéro-initialise.

        return mouse;
    }

    // pcem: amstrad.c:135-139
    private static void mouse_amstrad_close(object p)
    {
        // omitted: free(mouse) — libération manuelle, sans objet sous GC.
    }

    // pcem: amstrad.c:141
    internal static readonly mouse_t mouse_amstrad = new()
    {
        name = "Amstrad mouse", init = mouse_amstrad_init, close = mouse_amstrad_close,
        poll = mouse_amstrad_poll, type = mouse.MOUSE_TYPE_AMSTRAD,
    };

    // pcem: amstrad.c:143-153
    internal static void amstrad_init()
    {
        // omitted: lpt2_remove_ams() (amstrad.c:144, lpt.c:166) — io_removehandler de
        //   lpt2_read/lpt2_write sur 0x379-0x37a. lpt_init (lpt.c:141-144) ne les pose
        //   qu'en 0x278 : chez PCem aussi, ce retrait ne trouve rien et ne fait rien.

        io_sethandler(0x0078, 0x0001, amstrad_mouse_read, null, null, amstrad_mouse_write, null, null, null);
        io_sethandler(0x007a, 0x0001, amstrad_mouse_read, null, null, amstrad_mouse_write, null, null, null);
        io_sethandler(0x0066, 0x0001, null, null, null, amstrad_write, null, null, null);
        io_sethandler(0x0378, 0x0003, amstrad_read, null, null, amstrad_write, null, null, null);
        io_sethandler(0xdead, 0x0001, amstrad_read, null, null, amstrad_write, null, null, null);
        if ((romset == ROM_PC200 || romset == ROM_PPC512) && gfxcard != GFX_BUILTIN)
                io_sethandler(0x03de, 0x0001, amstrad_read, null, null, amstrad_write, null, null, null);
    }

    // pcem: amstrad.c:155-158
    // `return &amstrad_language;` : un pointeur non nul que device_priv garde et que
    // personne ne relit (ams1512_device n'a ni close ni speed_changed). Ici l'octet
    // encapsulé — non nul lui aussi, ce que device_add teste.
    private static object? ams1512_init()
    {
        amstrad_language = (uint8_t)device_get_config_int("language");
        return amstrad_language;
    }

    // pcem: amstrad.c:160-173
    // omitted: `.description` (amstrad.c:161-171) ; `.selection` est transcrit (G8.3,
    //   device_get_config_int y valide la valeur du .cfg).
    internal static device_config_t[] ams1512_config =
    [
        new device_config_t { name = "language", type = CONFIG_SELECTION, default_int = 7,
            selection = [new() { description = "English", value = 7 }, new() { description = "German", value = 6 }, new() { description = "French", value = 5 }, new() { description = "Spanish", value = 4 }, new() { description = "Danish", value = 3 }, new() { description = "Swedish", value = 2 }, new() { description = "Italian", value = 1 }, new() { description = "Diagnostic mode", value = 0 }] },
        new device_config_t { type = -1 },
    ];

    // omitted: ams2086_config, ams3086_config (amstrad.c:175-191) — PC2086 / PC3086, hors cible.

    // pcem: amstrad.c:193
    internal static device_t ams1512_device = new device_t("Amstrad PC1512 (BIOS)", 0, ams1512_init, null, null, null, null, null,
                                                           ams1512_config);

    // omitted: ams2086_device, ams3086_device (amstrad.c:195-197) — PC2086 / PC3086, hors cible.
}
