// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/video/vid_vga.c + includes/private/video/vid_vga.h
// STATUS: partial — vga_t, vga_out/vga_in, vga_disable/vga_enable, vga_init,
//         vga_available, vga_close, vga_speed_changed, vga_force_redraw,
//         vga_add_status_info, vga_device. Omis : ps1vga_init et ps1vga_device.

// CS8600/CS8602/CS8604 : même raison qu'à vid_svga.cs et vid_cga.cs:9-17.
#pragma warning disable CS8600, CS8602, CS8604

using System.Text;
using iXtal26.Flash;
using iXtal26.PluginApi;
using static iXtal26.Flash.rom;
using static iXtal26.Memory.mem;
using static iXtal26.Video.video;
using static iXtal26.Video.vid_svga;
using static iXtal26.io;

namespace iXtal26.Video;

// pcem: vid_vga.c:14-18
// Classe et non struct : son adresse est prise (svga_init, io_sethandler).
internal sealed class vga_t
{
    internal svga_t svga = new();

    internal rom_t bios_rom = new();
}

/*IBM VGA emulation*/
internal static partial class vid_vga
{
    // pcem: vid_vga.c:12
    internal static svga_t? mb_vga = null;

    // pcem: vid_vga.c:20-52
    internal static void vga_out(uint16_t addr, uint8_t val, object p)
    {
        vga_t vga = (vga_t)p;
        svga_t svga = vga.svga;
        uint8_t old;

        if (((addr & 0xfff0) == 0x3d0 || (addr & 0xfff0) == 0x3b0) && (svga.miscout & 1) == 0)
                addr ^= 0x60;

        switch (addr)
        {
        case 0x3D4:
                svga.crtcreg = (uint8_t)(val & 0x3f);
                return;
        case 0x3D5:
                if ((svga.crtcreg & 0x20) != 0)
                        return;
                if ((svga.crtcreg < 7) && (svga.crtc[0x11] & 0x80) != 0)
                        return;
                if ((svga.crtcreg == 7) && (svga.crtc[0x11] & 0x80) != 0)
                        val = (uint8_t)((svga.crtc[7] & ~0x10) | (val & 0x10));
                old = svga.crtc[svga.crtcreg];
                svga.crtc[svga.crtcreg] = val;
                if (old != val)
                {
                        if (svga.crtcreg < 0xe || svga.crtcreg > 0x10)
                        {
                                svga.fullchange = changeframecount;
                                svga_recalctimings(svga);
                        }
                }
                break;
        }
        svga_out(addr, val, svga);
    }

    // pcem: vid_vga.c:54-80
    internal static uint8_t vga_in(uint16_t addr, object p)
    {
        vga_t vga = (vga_t)p;
        svga_t svga = vga.svga;
        uint8_t temp;

        if (((addr & 0xfff0) == 0x3d0 || (addr & 0xfff0) == 0x3b0) && (svga.miscout & 1) == 0)
                addr ^= 0x60;

        switch (addr)
        {
        case 0x3D4:
                temp = svga.crtcreg;
                break;
        case 0x3D5:
                if ((svga.crtcreg & 0x20) != 0)
                        temp = 0xff;
                else
                        temp = svga.crtc[svga.crtcreg];
                break;
        default:
                temp = svga_in(addr, svga);
                break;
        }
        return temp;
    }

    // pcem: vid_vga.c:82-89
    internal static void vga_disable(object p)
    {
        vga_t vga = (vga_t)p;
        svga_t svga = vga.svga;

        io_removehandler(0x03a0, 0x0040, vga_in, null, null, vga_out, null, null, vga);
        mem_mapping_disable(svga.mapping);
    }

    // pcem: vid_vga.c:91-101
    internal static void vga_enable(object p)
    {
        vga_t vga = (vga_t)p;
        svga_t svga = vga.svga;

        io_sethandler(0x03c0, 0x0020, vga_in, null, null, vga_out, null, null, vga);
        if ((svga.miscout & 1) == 0)
                io_sethandler(0x03a0, 0x0020, vga_in, null, null, vga_out, null, null, vga);

        mem_mapping_enable(svga.mapping);
    }

    // pcem: vid_vga.c:103-118
    internal static object vga_init()
    {
        vga_t vga = new vga_t();
        // pcem: vid_vga.c:105 — memset(vga, 0, sizeof(vga_t)) ; `new` zéro-initialise.

        rom_init(vga.bios_rom, "ibm_vga.bin", 0xc0000, 0x8000, 0x7fff, 0x2000, MEM_MAPPING_EXTERNAL);

        svga_init(vga.svga, vga, 1 << 18, /*256kb*/
                  null, vga_in, vga_out, null, null);

        io_sethandler(0x03c0, 0x0020, vga_in, null, null, vga_out, null, null, vga);

        vga.svga.bpp = 8;
        vga.svga.miscout = 1;

        return vga;
    }

    // omitted: ps1vga_init (vid_vga.c:120-136) et ps1vga_device (vid_vga.c:169-170) —
    //   la VGA intégrée des IBM PS/1 et PS/2, sans ROM d'extension. Son seul appelant
    //   est video_card_getdevice / video_init pour les romsets PS/1 et PS/2
    //   (video.c:296-306, :851-861), qu'aucune machine du dépôt n'a.

    // pcem: vid_vga.c:138
    internal static int vga_available() { return rom_present("ibm_vga.bin"); }

    // pcem: vid_vga.c:140-148
    internal static void vga_close(object p)
    {
        vga_t vga = (vga_t)p;

        mb_vga = null;

        svga_close(vga.svga);

        // omitted: free(vga) (vid_vga.c:147) — libération manuelle, sans objet sous GC.
    }

    // pcem: vid_vga.c:150-154
    internal static void vga_speed_changed(object p)
    {
        vga_t vga = (vga_t)p;

        svga_recalctimings(vga.svga);
    }

    // pcem: vid_vga.c:156-160
    internal static void vga_force_redraw(object p)
    {
        vga_t vga = (vga_t)p;

        vga.svga.fullchange = changeframecount;
    }

    // pcem: vid_vga.c:162-166
    internal static void vga_add_status_info(StringBuilder s, int max_len, object p)
    {
        vga_t vga = (vga_t)p;

        svga_add_status_info(s, max_len, vga.svga);
    }

    // pcem: vid_vga.c:168
    internal static device_t vga_device = new device_t("VGA", 0, vga_init, vga_close, vga_available, vga_speed_changed,
                                                       vga_force_redraw, vga_add_status_info, null);
}
