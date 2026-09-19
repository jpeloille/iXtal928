// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/io.c
// SHA256: voir oracle.tsv ; vérifier avec tools/check-oracle.sh
// STATUS: partial — io_init, io_sethandler, io_removehandler, inb/outb/inw/outw/
//         inl/outl, cpu_readport ; omis : le latch Amstrad de inb (io.c:111-116)
//         et les globales vestigiales io.c:98-102.

// CS8602/CS8604 : l'analyse de nullabilité de C# ne suit pas l'état des éléments
// de tableau. `if (port_inb[port, 0] != null)` — le test de PCem, une ligne plus
// haut — ne la renseigne donc pas, et `port_priv[port, 0]` reste `object?` là où
// le delegate déclare `object` (le C y passe un `void *` qui vaut NULL tant que
// le périphérique n'a pas d'état privé).
#pragma warning disable CS8602, CS8604

using iXtal26.Diag;

namespace iXtal26;

// pcem: io.c:8-16 — les pointeurs de fonction de port_in*/port_out*.
internal delegate uint8_t inb_fn(uint16_t addr, object priv);
internal delegate uint16_t inw_fn(uint16_t addr, object priv);
internal delegate uint32_t inl_fn(uint16_t addr, object priv);

internal delegate void outb_fn(uint16_t addr, uint8_t val, object priv);
internal delegate void outw_fn(uint16_t addr, uint16_t val, object priv);
internal delegate void outl_fn(uint16_t addr, uint32_t val, object priv);

internal static partial class io
{
    // pcem: io.c:8-16
    internal static inb_fn?[,] port_inb = new inb_fn?[0x10000, 2];
    internal static inw_fn?[,] port_inw = new inw_fn?[0x10000, 2];
    internal static inl_fn?[,] port_inl = new inl_fn?[0x10000, 2];

    internal static outb_fn?[,] port_outb = new outb_fn?[0x10000, 2];
    internal static outw_fn?[,] port_outw = new outw_fn?[0x10000, 2];
    internal static outl_fn?[,] port_outl = new outl_fn?[0x10000, 2];

    internal static object?[,] port_priv = new object?[0x10000, 2];

    // pcem: io.c:18-37
    internal static void io_init()
    {
        int c;
        // omitted: pclog("io_init\n") — trace de débogage, sortie pure.
        for (c = 0; c < 0x10000; c++)
        {
            port_inb[c, 0] = null;
            port_inw[c, 0] = null;
            port_inl[c, 0] = null;
            port_outb[c, 0] = null;
            port_outw[c, 0] = null;
            port_outl[c, 0] = null;
            port_inb[c, 1] = null;
            port_inw[c, 1] = null;
            port_inl[c, 1] = null;
            port_outb[c, 1] = null;
            port_outw[c, 1] = null;
            port_outl[c, 1] = null;
            port_priv[c, 0] = null;
            port_priv[c, 1] = null;
        }
    }

    // pcem: io.c:39-65
    internal static void io_sethandler(uint16_t @base, int size, inb_fn? inb, inw_fn? inw,
                                       inl_fn? inl, outb_fn? outb,
                                       outw_fn? outw, outl_fn? outl,
                                       object? priv)
    {
        int c;
        for (c = 0; c < size; c++)
        {
            if (port_inb[@base + c, 0] == null && port_inw[@base + c, 0] == null && port_inl[@base + c, 0] == null && port_outb[@base + c, 0] == null &&
                port_outw[@base + c, 0] == null && port_outl[@base + c, 0] == null)
            {
                port_inb[@base + c, 0] = inb;
                port_inw[@base + c, 0] = inw;
                port_inl[@base + c, 0] = inl;
                port_outb[@base + c, 0] = outb;
                port_outw[@base + c, 0] = outw;
                port_outl[@base + c, 0] = outl;
                port_priv[@base + c, 0] = priv;
            }
            else if (port_inb[@base + c, 1] == null && port_inw[@base + c, 1] == null && port_inl[@base + c, 1] == null &&
                     port_outb[@base + c, 1] == null && port_outw[@base + c, 1] == null && port_outl[@base + c, 1] == null)
            {
                port_inb[@base + c, 1] = inb;
                port_inw[@base + c, 1] = inw;
                port_inl[@base + c, 1] = inl;
                port_outb[@base + c, 1] = outb;
                port_outw[@base + c, 1] = outw;
                port_outl[@base + c, 1] = outl;
                port_priv[@base + c, 1] = priv;
            }
        }
    }

    // pcem: io.c:67-96
    internal static void io_removehandler(uint16_t @base, int size, inb_fn? inb,
                                          inw_fn? inw, inl_fn? inl,
                                          outb_fn? outb, outw_fn? outw,
                                          outl_fn? outl, object? priv)
    {
        int c;
        for (c = 0; c < size; c++)
        {
            if (port_priv[@base + c, 0] == priv && port_inb[@base + c, 0] == inb && port_inw[@base + c, 0] == inw &&
                port_inl[@base + c, 0] == inl && port_outb[@base + c, 0] == outb && port_outw[@base + c, 0] == outw &&
                port_outl[@base + c, 0] == outl)
            {
                port_inb[@base + c, 0] = null;
                port_inw[@base + c, 0] = null;
                port_inl[@base + c, 0] = null;
                port_outb[@base + c, 0] = null;
                port_outw[@base + c, 0] = null;
                port_outl[@base + c, 0] = null;
                port_priv[@base + c, 0] = null;
            }
            if (port_priv[@base + c, 1] == priv && port_inb[@base + c, 1] == inb && port_inw[@base + c, 1] == inw &&
                port_inl[@base + c, 1] == inl && port_outb[@base + c, 1] == outb && port_outw[@base + c, 1] == outw &&
                port_outl[@base + c, 1] == outl)
            {
                port_inb[@base + c, 1] = null;
                port_inw[@base + c, 1] = null;
                port_inl[@base + c, 1] = null;
                port_outb[@base + c, 1] = null;
                port_outw[@base + c, 1] = null;
                port_outl[@base + c, 1] = null;
                port_priv[@base + c, 1] = null;
            }
        }
    }

    // omitted: cgamode/cgastat/cgacol/hsync/lpt2dat/sw9/t237 (io.c:98-102) —
    //          globales vestigiales : aucune lecture ni écriture ailleurs dans
    //          l'arbre PCem (les homonymes de vid_cga.c & co sont des membres de
    //          cga_t), seule trace un `extern uint8_t cgastat;` inutilisé à
    //          pc.c:107.

    // pcem: io.c:103-122
    internal static uint8_t inb(uint16_t port)
    {
        Counters.n_inb++;
        uint8_t temp = 0xff;

        if (port_inb[port, 0] != null)
            temp &= port_inb[port, 0](port, port_priv[port, 0]);
        if (port_inb[port, 1] != null)
            temp &= port_inb[port, 1](port, port_priv[port, 1]);

        // omitted: le latch Amstrad, io.c:111-116 (amstrad_latch, AMSTRAD_NOLATCH
        //          /SW9/SW10) — écrit à chaque IN, relu par le seul
        //          models/amstrad.c, machine hors cible IBM PC 5150.

        return temp;
    }

    // pcem: io.c:124
    internal static uint8_t cpu_readport(uint32_t port) { return inb((uint16_t)port); }

    // pcem: io.c:126-135
    internal static void outb(uint16_t port, uint8_t val)
    {
        Counters.n_outb++;
        if (port_outb[port, 0] != null)
            port_outb[port, 0](port, val, port_priv[port, 0]);
        if (port_outb[port, 1] != null)
            port_outb[port, 1](port, val, port_priv[port, 1]);

        return;
    }

    // pcem: io.c:137-145
    internal static uint16_t inw(uint16_t port)
    {
        if (port_inw[port, 0] != null)
            return port_inw[port, 0](port, port_priv[port, 0]);
        if (port_inw[port, 1] != null)
            return port_inw[port, 1](port, port_priv[port, 1]);

        return (uint16_t)(inb(port) | (inb((uint16_t)(port + 1)) << 8));
    }

    // pcem: io.c:147-162
    internal static void outw(uint16_t port, uint16_t val)
    {
        if (port_outw[port, 0] != null)
            port_outw[port, 0](port, val, port_priv[port, 0]);
        if (port_outw[port, 1] != null)
            port_outw[port, 1](port, val, port_priv[port, 1]);

        if (port_outw[port, 0] != null || port_outw[port, 1] != null)
            return;

        outb(port, (uint8_t)val);
        outb((uint16_t)(port + 1), (uint8_t)(val >> 8));
    }

    // pcem: io.c:164-172
    internal static uint32_t inl(uint16_t port)
    {
        if (port_inl[port, 0] != null)
            return port_inl[port, 0](port, port_priv[port, 0]);
        if (port_inl[port, 1] != null)
            return port_inl[port, 1](port, port_priv[port, 1]);

        return (uint32_t)(inw(port) | (inw((uint16_t)(port + 2)) << 16));
    }

    // pcem: io.c:174-188
    internal static void outl(uint16_t port, uint32_t val)
    {
        if (port_outl[port, 0] != null)
            port_outl[port, 0](port, val, port_priv[port, 0]);
        if (port_outl[port, 1] != null)
            port_outl[port, 1](port, val, port_priv[port, 1]);

        if (port_outl[port, 0] != null || port_outl[port, 1] != null)
            return;

        outw(port, (uint16_t)val);
        outw((uint16_t)(port + 2), (uint16_t)(val >> 16));
    }
}
