// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/video/vid_et4000.c + includes/private/video/vid_et4000.h
// STATUS: partial — et4000_t (sa part ET4000AX), crtc_mask, et4000_out, et4000_in,
//         et4000_recalctimings, et4000_init, et4000_available, et4000_close,
//         et4000_speed_changed, et4000_force_redraw, et4000_add_status_info, et4000_device.
//         Omis (hors G9, PLAN.md) : les variantes coréennes — et4000k (Trigem) et Kasan
//         Hangulmadang-16 : leurs champs de et4000_t (:22-31), et4000k_out/in, et4000_kasan_out/in,
//         et4000k_recalctimings, et4000_kasan_recalctimings, leurs init, available et devices.
//
// G9.3 (PLAN-G9.md). La Tseng ET4000AX : le socle SVGA (vid_svga.cs), 1 Mo fixe, ROM et4000.bin,
// les banques en 3CDh, les registres étendus du CRTC (CR33-CR35, masque crtc_mask) et le RAMDAC
// SC1502x (vid_unk_ramdac.cs). Une carte ISA : elle équipe aussi les machines sans VLB.

// CS8600/CS8602/CS8604 : `(et4000_t)p` part du `object?` des delegates d'io.cs et de vid_svga.cs.
#pragma warning disable CS8600, CS8602, CS8604

using System.Text;
using iXtal26.Flash;
using iXtal26.Models;
using iXtal26.PluginApi;
using static iXtal26.Flash.rom;
using static iXtal26.Memory.mem;
using static iXtal26.Video.video;
using static iXtal26.Video.vid_svga;
using static iXtal26.Video.vid_unk_ramdac;
using static iXtal26.io;

namespace iXtal26.Video;

// pcem: vid_et4000.c:15-32
// omitted: port_22cb_val à kasan_font_data (:22-31) — les variantes coréennes.
internal sealed class et4000_t
{
    internal svga_t svga = new();
    internal unk_ramdac_t ramdac = new();

    internal rom_t bios_rom = new();

    internal uint8_t banking;
}

/*ET4000 emulation*/
internal static class vid_et4000
{
    // pcem: vid_et4000.c:34-37
    // pcem bug, reproduced: PB-100 — CR30 et CR31, que l'ET4000AX n'a pas (data book Tseng, table 4.3-2),
    //   s'écrivent et se relisent. CR38-CR3F effacés, CR3F compris : fidèle à l'AX, qui n'a pas CR3F ; le
    //   débordement de htotal que et4000_recalctimings y lit (:399) est celui de la W32.
    private static readonly uint8_t[] crtc_mask = new uint8_t[0x40]
    {
        0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff,
        0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0xff, 0xff, 0xff, 0x0f, 0xff, 0xff, 0xff, 0xff, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
    };

    // pcem: vid_et4000.c:42-100
    internal static void et4000_out(uint16_t addr, uint8_t val, object? p)
    {
        et4000_t et4000 = (et4000_t)p;
        svga_t svga = et4000.svga;

        uint8_t old;

        if (((addr & 0xFFF0) == 0x3D0 || (addr & 0xFFF0) == 0x3B0) && (svga.miscout & 1) == 0)
                addr ^= 0x60;

        //        pclog("ET4000 out %04X %02X\n", addr, val);

        // pcem bug, reproduced: PB-100 — pas de KEY (03h en 3BFh, puis A0h en 3D8h ou 3B8h) : ce qu'il garde
        //   s'écrit toujours (CRTC au-delà de 18h hors 33h et 35h, TS6, TS7, ATC 16h, 3CDh avant la première pose).
        switch (addr)
        {
        case 0x3C6:
        case 0x3C7:
        case 0x3C8:
        case 0x3C9:
                unk_ramdac_out(addr, val, et4000.ramdac, svga);
                return;

        case 0x3CD: /*Banking*/
                svga.write_bank = (uint32_t)((val & 0xf) * 0x10000);
                svga.read_bank = (uint32_t)(((val >> 4) & 0xf) * 0x10000);
                et4000.banking = val;
                //                pclog("Banking write %08X %08X %02X\n", svga->write_bank, svga->read_bank, val);
                return;

        case 0x3cf:
                if ((svga.gdcaddr & 15) == 6)
                {
                        old = svga.gdcreg[6];
                        svga_out(addr, val, svga);
                        // pcem bug, reproduced: PB-100 — la fenêtre de 128 Ko n'est posée que sur une
                        //   transition non nul → nul (:72-75) ; svga_init laisse banked_mask à 0, et
                        //   écrire 0 sur un GDC6 déjà nul ne le pose jamais.
                        if ((old & 0xc) != 0 && (val & 0xc) == 0)
                        {
                                /*override mask - ET4000 supports linear 128k at A0000*/
                                svga.banked_mask = 0x1ffff;
                        }
                        return;
                }
                break;

        case 0x3D4:
                svga.crtcreg = (uint8_t)(val & 0x3f);
                return;
        case 0x3D5:
                // pcem bug, reproduced: PB-227 — CR11 bit 7 ne protège que CR0-CR7 ; le data book protège
                //   aussi CR35 (table 4.3-2).
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
                break;
        }
        svga_out(addr, val, svga);
    }

    // pcem: vid_et4000.c:259-288
    internal static uint8_t et4000_in(uint16_t addr, object? p)
    {
        et4000_t et4000 = (et4000_t)p;
        svga_t svga = et4000.svga;

        if (((addr & 0xFFF0) == 0x3D0 || (addr & 0xFFF0) == 0x3B0) && (svga.miscout & 1) == 0)
                addr ^= 0x60;

        //        if (addr != 0x3da) pclog("IN ET4000 %04X\n", addr);

        // pcem bug, reproduced: PB-100 — pas de séquence KEY (3BFh/3D8h) : ce qu'elle garde en lecture
        //   (Input Status 0 bits 5-6, 3CAh bit 7) reste toujours ouvert.
        switch (addr)
        {
        case 0x3C5:
                // pcem bug, reproduced: PB-100 — SR7 se relit bit 2 forcé (:270-271). « Set to 1 (always) »
                //   (data book, TS 7) règle l'écriture ; la valeur relue par la puce n'est pas documentée.
                if ((svga.seqaddr & 0xf) == 7)
                        return (uint8_t)(svga.seqregs[svga.seqaddr & 0xf] | 4);
                break;

        case 0x3C6:
        case 0x3C7:
        case 0x3C8:
        case 0x3C9:
                return unk_ramdac_in(addr, et4000.ramdac, svga);

        case 0x3CD: /*Banking*/
                return et4000.banking;
        case 0x3D4:
                return svga.crtcreg;
        case 0x3D5:
                return svga.crtc[svga.crtcreg];
        }
        // pcem bug, reproduced: PB-226 — 3DAh passe au socle (vid_svga.c:262-269) : bit 7 toujours
        //   nul, bits 4-5 basculés ; l'ET4000 met en 7 le complément du retour vertical et en 4-5 la vidéo d'AR12.
        return svga_in(addr, svga);
    }

    // pcem: vid_et4000.c:385-430
    internal static void et4000_recalctimings(svga_t svga)
    {
        svga.ma_latch |= (uint32_t)((svga.crtc[0x33] & 3) << 16);
        if ((svga.crtc[0x35] & 1) != 0)
                svga.vblankstart += 0x400;
        if ((svga.crtc[0x35] & 2) != 0)
                svga.vtotal += 0x400;
        if ((svga.crtc[0x35] & 4) != 0)
                svga.dispend += 0x400;
        if ((svga.crtc[0x35] & 8) != 0)
                svga.vsyncstart += 0x400;
        if ((svga.crtc[0x35] & 0x10) != 0)
                svga.split += 0x400;
        // pcem bug, reproduced: PB-100 — un CR13 nul vaut 256 (:397-398).
        if (svga.rowoffset == 0)
                svga.rowoffset = 0x100;
        if ((svga.crtc[0x3f] & 1) != 0)
                svga.htotal += 256;
        if ((svga.attrregs[0x16] & 0x20) != 0)
                svga.hdisp <<= 1;

        //        pclog("Rowoffset %i\n",svga_rowoffset);

        switch (((svga.miscout >> 2) & 3) | ((svga.crtc[0x34] << 1) & 4))
        {
        case 0:
        case 1:
                break;
        case 3:
                svga.clock = (pit.cpuclock * (double)(1ul << 32)) / 40000000.0;
                break;
        case 5:
                svga.clock = (pit.cpuclock * (double)(1ul << 32)) / 65000000.0;
                break;
        default:
                svga.clock = (pit.cpuclock * (double)(1ul << 32)) / 36000000.0;
                break;
        }

        switch (svga.bpp)
        {
        case 15:
        case 16:
                svga.hdisp /= 2;
                break;
        case 24:
                svga.hdisp /= 3;
                break;
        }
    }

    // pcem: vid_et4000.c:485-499
    internal static object et4000_init()
    {
        et4000_t et4000 = new et4000_t();
        // pcem: :486-487 — malloc + memset ; `new` zéro-initialise.

        rom_init(et4000.bios_rom, "et4000.bin", 0xc0000, 0x8000, 0x7fff, 0, MEM_MAPPING_EXTERNAL);

        io_sethandler(0x03c0, 0x0020, et4000_in, null, null, et4000_out, null, null, et4000);

        svga_init(et4000.svga, et4000, 1 << 20, /*1mb*/
                  et4000_recalctimings, et4000_in, et4000_out, null, null);

        et4000.svga.packed_chain4 = 1;

        return et4000;
    }

    // pcem: vid_et4000.c:572
    private static int et4000_available() { return rom_present("et4000.bin"); }

    // pcem: vid_et4000.c:584-590
    internal static void et4000_close(object? p)
    {
        et4000_t et4000 = (et4000_t)p;

        svga_close(et4000.svga);

        // omitted: free(et4000) (:589) — sous GC.
    }

    // pcem: vid_et4000.c:592-596
    internal static void et4000_speed_changed(object? p)
    {
        et4000_t et4000 = (et4000_t)p;

        svga_recalctimings(et4000.svga);
    }

    // pcem: vid_et4000.c:598-602
    internal static void et4000_force_redraw(object? p)
    {
        et4000_t et4000 = (et4000_t)p;

        et4000.svga.fullchange = changeframecount;
    }

    // pcem: vid_et4000.c:604-608
    internal static void et4000_add_status_info(StringBuilder s, int max_len, object? p)
    {
        et4000_t et4000 = (et4000_t)p;

        svga_add_status_info(s, max_len, et4000.svga);
    }

    // pcem: vid_et4000.c:610-612
    internal static device_t et4000_device = new device_t("Tseng Labs ET4000AX", 0, et4000_init, et4000_close, et4000_available,
                                                          et4000_speed_changed, et4000_force_redraw, et4000_add_status_info, null);
}
