// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/sound/sound_adlib.c + includes/private/sound/sound_adlib.h
// STATUS: partial — adlib_t, adlib_get_buffer, adlib_init, adlib_close, adlib_device.
//         Omis : la variante MCA (adlib_mca_read/write, adlib_mca_init, adlib_mca_device,
//         sound_adlib.c:29-61, :71) — pas de bus MCA transcrit.

// CS8600 : `(adlib_t *)p` part du `object?` du delegate de sound.cs, comme à vid_cga.cs.
// CS8602 : même raison, à la déréférence qui suit.
#pragma warning disable CS8600, CS8602

using iXtal26.PluginApi;
using static iXtal26.io;
using static iXtal26.Sound.sound;
using static iXtal26.Sound.sound_opl;

namespace iXtal26.Sound;

// pcem: sound_adlib.c:11-15
internal sealed class adlib_t
{
    internal readonly opl_t opl = new opl_t();

    internal readonly uint8_t[] pos_regs = new uint8_t[8];
}

internal static partial class sound_adlib
{
    // pcem: sound_adlib.c:17-27
    private static void adlib_get_buffer(int32_t[] buffer, int len, object? p)
    {
        adlib_t adlib = (adlib_t)p;
        int c;

        opl2_update2(adlib.opl);

        for (c = 0; c < len * 2; c++)
                buffer[c] += (int32_t)adlib.opl.buffer[c];

        adlib.opl.pos = 0;
    }

    // omitted: adlib_mca_read, adlib_mca_write (sound_adlib.c:29-54) — MCA.

    // pcem: sound_adlib.c:56-66
    internal static object? adlib_init()
    {
        adlib_t adlib = new adlib_t();
        // pcem: sound_adlib.c:58 — memset(adlib, 0, sizeof(adlib_t)) ; `new` zéro-initialise.

        // omitted: pclog("adlib_init\n") (:60) — sortie pure.
        opl2_init(adlib.opl);
        io_sethandler(0x0388, 0x0002, opl2_read, null, null, opl2_write, null, null, adlib.opl);
        sound_add_handler(adlib_get_buffer, adlib);

        return adlib;
    }

    // omitted: adlib_mca_init (sound_adlib.c:68-77) — MCA.

    // pcem: sound_adlib.c:79-83
    internal static void adlib_close(object p)
    {
        adlib_t adlib = (adlib_t)p;

        // omitted: free(adlib) (:82) — libération manuelle, sans objet sous GC.
    }

    // pcem: sound_adlib.c:85
    internal static device_t adlib_device = new device_t("AdLib", 0, adlib_init, adlib_close, null, null, null, null, null);

    // omitted: adlib_mca_device (sound_adlib.c:87) — MCA.
}
