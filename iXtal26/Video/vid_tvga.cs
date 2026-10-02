// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/video/vid_tvga.c + includes/private/video/vid_tvga.h
// STATUS: transcribed — tvga_t, crtc_mask, tvga_out/tvga_in, tvga_recalcbanking,
//         tvga_recalctimings, tvga_common_init, les deux *_init et *_available,
//         tvga_close, tvga_speed_changed, tvga_force_redraw, tvga_add_status_info,
//         tvga_config, tvga8900d_device et tvga9000b_device.

// CS8600/CS8602/CS8604 : même raison qu'à vid_svga.cs et vid_cga.cs:9-17.
#pragma warning disable CS8600, CS8602, CS8604

using System.Text;
using iXtal26.Flash;
using iXtal26.Models;
using iXtal26.PluginApi;
using static iXtal26.Flash.rom;
using static iXtal26.Memory.mem;
using static iXtal26.PluginApi.device;
using static iXtal26.Video.video;
using static iXtal26.Video.vid_svga;
using static iXtal26.Video.vid_svga_render;
using static iXtal26.Video.vid_tkd8001_ramdac;
using static iXtal26.io;

namespace iXtal26.Video;

// pcem: vid_tvga.c:16-34
// Classe et non struct : son adresse est prise (svga_init, io_sethandler).
// omitted: linear_mapping et accel_mapping (vid_tvga.c:17-18) — champs morts, jamais
//   passés à mem_mapping_add ni lus.
internal sealed class tvga_t
{
    internal svga_t svga = new();
    internal tkd8001_ramdac_t ramdac = new();

    internal rom_t bios_rom = new();

    internal uint8_t tvga_3d8, tvga_3d9;
    internal int oldmode;
    internal uint8_t oldctrl1;
    internal uint8_t oldctrl2, newctrl2;

    internal int vram_size;
    internal uint32_t vram_mask;

    internal uint8_t id;
}

/*Trident TVGA (8900D) emulation*/
internal static partial class vid_tvga
{
    // pcem: vid_tvga.c:14
    internal const int TVGA_8900D = 0x33;
    internal const int TVGA_9000 = 0x23;

    // pcem: vid_tvga.c:36-39
    private static readonly uint8_t[] crtc_mask = {0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0x7f, 0xff, 0x3f, 0x7f, 0xff, 0xff, 0xff, 0xff,
                                                   0xff, 0xff, 0xff, 0xff, 0x7f, 0xff, 0xff, 0xef, 0xff, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff,
                                                   0x7f, 0x00, 0x00, 0x2f, 0x00, 0x00, 0x00, 0x03, 0x00, 0x13, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                                                   0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00};

    // pcem: vid_tvga.c:42-162
    internal static void tvga_out(uint16_t addr, uint8_t val, object p)
    {
        tvga_t tvga = (tvga_t)p;
        svga_t svga = tvga.svga;

        uint8_t old;

        if (((addr & 0xFFF0) == 0x3D0 || (addr & 0xFFF0) == 0x3B0) && (svga.miscout & 1) == 0)
                addr ^= 0x60;

        switch (addr)
        {
        case 0x3C5:
                switch (svga.seqaddr & 0xf)
                {
                case 0xB:
                        tvga.oldmode = 1;
                        break;
                case 0xC:
                        if ((svga.seqregs[0xe] & 0x80) != 0)
                                svga.seqregs[0xc] = val;
                        break;
                case 0xd:
                        if (tvga.oldmode != 0)
                                tvga.oldctrl2 = val;
                        else
                        {
                                tvga.newctrl2 = val;
                                svga_recalctimings(svga);
                        }
                        break;
                case 0xE:
                        if (tvga.oldmode != 0)
                        {
                                tvga.oldctrl1 = val;
                                svga_recalctimings(svga);
                        }
                        else
                        {
                                svga.seqregs[0xe] = (uint8_t)(val ^ 2);
                                tvga.tvga_3d8 = (uint8_t)(svga.seqregs[0xe] & 0xf);
                                tvga_recalcbanking(tvga);
                        }
                        return;
                }
                break;

        case 0x3C6:
        case 0x3C7:
        case 0x3C8:
        case 0x3C9:
                if (tvga.id != TVGA_9000)
                {
                        tkd8001_ramdac_out(addr, val, tvga.ramdac, svga);
                        return;
                }
                break;

        case 0x3CF:
                switch (svga.gdcaddr & 15)
                {
                case 0x6:
                        old = svga.gdcreg[6];
                        svga_out(addr, val, svga);
                        if ((old & 0xc) != 0 && (val & 0xc) == 0)
                        {
                                /*override mask - TVGA supports linear 128k at A0000*/
                                svga.banked_mask = 0x1ffff;
                        }
                        return;
                case 0xE:
                        svga.gdcreg[0xe] = (uint8_t)(val ^ 2);
                        tvga.tvga_3d9 = (uint8_t)(svga.gdcreg[0xe] & 0xf);
                        tvga_recalcbanking(tvga);
                        break;
                case 0xF:
                        svga.gdcreg[0xf] = val;
                        tvga_recalcbanking(tvga);
                        break;
                }
                break;
        case 0x3D4:
                svga.crtcreg = (uint8_t)(val & 0x3f);
                return;
        case 0x3D5:
                if ((svga.crtcreg < 7) && (svga.crtc[0x11] & 0x80) != 0)
                        return;
                if ((svga.crtcreg == 7) && (svga.crtc[0x11] & 0x80) != 0)
                        val = (uint8_t)((svga.crtc[7] & ~0x10) | (val & 0x10));
                old = svga.crtc[svga.crtcreg];
                val &= crtc_mask[svga.crtcreg];
                svga.crtc[svga.crtcreg] = val;
                if (old != val)
                {
                        if (svga.crtcreg < 0xE || svga.crtcreg > 0x10)
                        {
                                svga.fullchange = changeframecount;
                                svga_recalctimings(svga);
                        }
                }
                switch (svga.crtcreg)
                {
                case 0x1e:
                        svga.vram_display_mask = (val & 0x80) != 0 ? tvga.vram_mask : 0x3ffff;
                        break;
                }
                return;
        case 0x3D8:
                if ((svga.gdcreg[0xf] & 4) != 0)
                {
                        tvga.tvga_3d8 = val;
                        tvga_recalcbanking(tvga);
                }
                return;
        case 0x3D9:
                if ((svga.gdcreg[0xf] & 4) != 0)
                {
                        tvga.tvga_3d9 = val;
                        tvga_recalcbanking(tvga);
                }
                return;
        case 0x3DB:
                if (tvga.id == TVGA_8900D)
                {
                        /*3db appears to be a 4 bit clock select register on 8900D*/
                        svga.miscout = (uint8_t)((svga.miscout & ~0x0c) | ((val & 3) << 2));
                        tvga.newctrl2 = (uint8_t)((tvga.newctrl2 & ~0x01) | ((val & 4) >> 2));
                        tvga.oldctrl1 = (uint8_t)((tvga.oldctrl1 & ~0x10) | ((val & 8) << 1));
                        svga_recalctimings(svga);
                }
                break;
        }
        svga_out(addr, val, svga);
    }

    // pcem: vid_tvga.c:164-213
    internal static uint8_t tvga_in(uint16_t addr, object p)
    {
        tvga_t tvga = (tvga_t)p;
        svga_t svga = tvga.svga;

        if (((addr & 0xFFF0) == 0x3D0 || (addr & 0xFFF0) == 0x3B0) && (svga.miscout & 1) == 0)
                addr ^= 0x60;

        switch (addr)
        {
        case 0x3C5:
                if ((svga.seqaddr & 0xf) == 0xb)
                {
                        tvga.oldmode = 0;
                        return tvga.id;
                }
                if ((svga.seqaddr & 0xf) == 0xd)
                {
                        if (tvga.oldmode != 0)
                                return tvga.oldctrl2;
                        return tvga.newctrl2;
                }
                if ((svga.seqaddr & 0xf) == 0xe)
                {
                        if (tvga.oldmode != 0)
                                return tvga.oldctrl1;
                }
                break;
        case 0x3C6:
        case 0x3C7:
        case 0x3C8:
        case 0x3C9:
                if (tvga.id != TVGA_9000)
                        return tkd8001_ramdac_in(addr, tvga.ramdac, svga);
                break;
        case 0x3D4:
                return svga.crtcreg;
        case 0x3D5:
                if (svga.crtcreg > 0x18 && svga.crtcreg < 0x1e)
                        return 0xff;
                return svga.crtc[svga.crtcreg];
        case 0x3d8:
                return tvga.tvga_3d8;
        case 0x3d9:
                return tvga.tvga_3d9;
        }
        return svga_in(addr, svga);
    }

    // pcem: vid_tvga.c:215-228
    private static void tvga_recalcbanking(tvga_t tvga)
    {
        svga_t svga = tvga.svga;

        svga.write_bank = (uint32_t)((tvga.tvga_3d8 & 0x1f) * 65536);

        if ((svga.gdcreg[0xf] & 1) != 0)
                svga.read_bank = (uint32_t)((tvga.tvga_3d9 & 0x1f) * 65536);
        else
                svga.read_bank = svga.write_bank;
    }

    // pcem: vid_tvga.c:230-347
    internal static void tvga_recalctimings(svga_t svga)
    {
        tvga_t tvga = (tvga_t)svga.p;
        int clksel;
        int high_res_256 = 0;

        if (svga.rowoffset == 0)
                svga.rowoffset = 0x100; /*This is the only sensible way I can see this being handled,
                                                         given that TVGA8900D has no overflow bits.
                                                         Some sort of overflow is required for 320x200x24 and 1024x768x16*/
        if ((svga.crtc[0x29] & 0x10) != 0)
                svga.rowoffset += 0x100;

        if (svga.bpp == 24)
                svga.hdisp = (svga.crtc[1] + 1) * 8;

        if ((svga.crtc[0x1e] & 0xA0) == 0xA0)
                svga.ma_latch |= 0x10000;
        if ((svga.crtc[0x27] & 0x01) == 0x01)
                svga.ma_latch |= 0x20000;
        if ((svga.crtc[0x27] & 0x02) == 0x02)
                svga.ma_latch |= 0x40000;

        if ((tvga.oldctrl2 & 0x10) != 0)
        {
                svga.rowoffset <<= 1;
                svga.ma_latch <<= 1;
        }
        if ((svga.gdcreg[0xf] & 0x08) != 0)
        {
                svga.htotal *= 2;
                svga.hdisp *= 2;
                svga.hdisp_time *= 2;
        }

        svga.interlace = svga.crtc[0x1e] & 4;
        if (svga.interlace != 0)
                svga.rowoffset >>= 1;

        if (tvga.id == TVGA_8900D)
                clksel = ((svga.miscout >> 2) & 3) | ((tvga.newctrl2 & 0x01) << 2) | ((tvga.oldctrl1 & 0x10) >> 1);
        else
                clksel = ((svga.miscout >> 2) & 3) | ((tvga.newctrl2 & 0x01) << 2) | ((tvga.newctrl2 & 0x40) >> 3);
        switch (clksel)
        {
        case 0x2:
                svga.clock = (pit.cpuclock * (double)(1UL << 32)) / 44900000.0;
                break;
        case 0x3:
                svga.clock = (pit.cpuclock * (double)(1UL << 32)) / 36000000.0;
                break;
        case 0x4:
                svga.clock = (pit.cpuclock * (double)(1UL << 32)) / 57272000.0;
                break;
        case 0x5:
                svga.clock = (pit.cpuclock * (double)(1UL << 32)) / 65000000.0;
                break;
        case 0x6:
                svga.clock = (pit.cpuclock * (double)(1UL << 32)) / 50350000.0;
                break;
        case 0x7:
                svga.clock = (pit.cpuclock * (double)(1UL << 32)) / 40000000.0;
                break;
        case 0x8:
                svga.clock = (pit.cpuclock * (double)(1UL << 32)) / 88000000.0;
                break;
        case 0x9:
                svga.clock = (pit.cpuclock * (double)(1UL << 32)) / 98000000.0;
                break;
        case 0xa:
                svga.clock = (pit.cpuclock * (double)(1UL << 32)) / 118800000.0;
                break;
        case 0xb:
                svga.clock = (pit.cpuclock * (double)(1UL << 32)) / 108000000.0;
                break;
        case 0xc:
                svga.clock = (pit.cpuclock * (double)(1UL << 32)) / 72000000.0;
                break;
        case 0xd:
                svga.clock = (pit.cpuclock * (double)(1UL << 32)) / 77000000.0;
                break;
        case 0xe:
                svga.clock = (pit.cpuclock * (double)(1UL << 32)) / 80000000.0;
                break;
        case 0xf:
                svga.clock = (pit.cpuclock * (double)(1UL << 32)) / 75000000.0;
                break;
        }

        if (tvga.id == TVGA_9000)
        {
                /*TVGA9000 doesn't seem to have support for a 'high res' 256 colour mode
                  (without the VGA pixel doubling). Instead it implements these modes by
                  doubling the horizontal pixel count and pixel clock. Hence we use a
                  basic heuristic to detect this*/
                if (svga.interlace != 0)
                        high_res_256 = (svga.htotal * 8) > (svga.vtotal * 4) ? 1 : 0;
                else
                        high_res_256 = (svga.htotal * 8) > (svga.vtotal * 2) ? 1 : 0;
        }
        if ((tvga.oldctrl2 & 0x10) != 0 || high_res_256 != 0)
        {
                if (high_res_256 != 0)
                        svga.hdisp /= 2;
                switch (svga.bpp)
                {
                case 8:
                        svga.render = svga_render_8bpp_highres;
                        break;
                case 15:
                        svga.render = svga_render_15bpp_highres;
                        svga.hdisp /= 2;
                        break;
                case 16:
                        svga.render = svga_render_16bpp_highres;
                        svga.hdisp /= 2;
                        break;
                case 24:
                        svga.render = svga_render_24bpp_highres;
                        svga.hdisp /= 3;
                        break;
                }
                svga.lowres = 0;
        }
    }

    // pcem: vid_tvga.c:349-364
    private static object tvga_common_init(string fn, uint32_t id, int vram_size)
    {
        tvga_t tvga = new tvga_t();
        // pcem: vid_tvga.c:351 — memset(tvga, 0, sizeof(tvga_t)) ; `new` zéro-initialise.

        tvga.vram_size = vram_size << 10;
        tvga.vram_mask = (uint32_t)(tvga.vram_size - 1);
        tvga.id = (uint8_t)id;

        rom_init(tvga.bios_rom, fn, 0xc0000, 0x8000, 0x7fff, 0, MEM_MAPPING_EXTERNAL);

        svga_init(tvga.svga, tvga, tvga.vram_size, tvga_recalctimings, tvga_in, tvga_out, null, null);

        io_sethandler(0x03c0, 0x0020, tvga_in, null, null, tvga_out, null, null, tvga);

        return tvga;
    }
    // pcem: vid_tvga.c:365-369
    private static object tvga8900d_init()
    {
        int vram_size = device_get_config_int("memory");

        return tvga_common_init("trident.bin", TVGA_8900D, vram_size);
    }
    // pcem: vid_tvga.c:370
    private static object tvga9000b_init() { return tvga_common_init("tvga9000b/BIOS.BIN", TVGA_9000, 512); }

    // pcem: vid_tvga.c:372-373
    private static int tvga8900d_available() { return rom_present("trident.bin"); }
    private static int tvga9000b_available() { return rom_present("tvga9000b/BIOS.BIN"); }

    // pcem: vid_tvga.c:375-381
    internal static void tvga_close(object p)
    {
        tvga_t tvga = (tvga_t)p;

        svga_close(tvga.svga);

        // omitted: free(tvga) (vid_tvga.c:380) — libération manuelle, sans objet sous GC.
    }

    // pcem: vid_tvga.c:383-387
    internal static void tvga_speed_changed(object p)
    {
        tvga_t tvga = (tvga_t)p;

        svga_recalctimings(tvga.svga);
    }

    // pcem: vid_tvga.c:389-393
    internal static void tvga_force_redraw(object p)
    {
        tvga_t tvga = (tvga_t)p;

        tvga.svga.fullchange = changeframecount;
    }

    // pcem: vid_tvga.c:395-399
    internal static void tvga_add_status_info(StringBuilder s, int max_len, object p)
    {
        tvga_t tvga = (tvga_t)p;

        svga_add_status_info(s, max_len, tvga.svga);
    }

    // pcem: vid_tvga.c:401-410
    // omitted: `.description` (vid_tvga.c:402) ; `.selection` (:404-408) est transcrit (G8.3) :
    //   sans section [Trident TVGA 8900D] dans le .cfg, le défaut, 1024 Ko.
    internal static device_config_t[] tvga_config =
    [
        new device_config_t { name = "memory", type = CONFIG_SELECTION, default_int = 1024,
            selection = [new() { description = "256 kB", value = 256 }, new() { description = "512 kB", value = 512 }, new() { description = "1 MB", value = 1024 }] },
        new device_config_t { type = -1 },
    ];

    // pcem: vid_tvga.c:412-417
    internal static device_t tvga8900d_device = new device_t("Trident TVGA 8900D", 0, tvga8900d_init, tvga_close, tvga8900d_available,
                                                             tvga_speed_changed, tvga_force_redraw, tvga_add_status_info, tvga_config);
    internal static device_t tvga9000b_device = new device_t("Trident TVGA 9000B", 0, tvga9000b_init, tvga_close, tvga9000b_available,
                                                             tvga_speed_changed, tvga_force_redraw, tvga_add_status_info, null);
}
