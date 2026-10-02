// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/video/vid_cl5429.c + includes/private/video/vid_cl5429.h
// STATUS: partial — gd5429_t, gd5429_out/gd5429_in, banques, fenêtres (banque, linéaire,
//         MMIO), gd5429_recalctimings, curseur matériel, modes d'écriture étendus 4/5,
//         verrous 8 octets, X8, blitter, cl_init, gd5429_device. Omis : MCA, PCI et les
//         dix autres cartes de la famille.

// CS8600/CS8602/CS8604 : même raison qu'à vid_svga.cs et vid_cga.cs:9-17.
#pragma warning disable CS8600, CS8602, CS8604

// CS1717 : `dst = dst;` (vid_cl5429.c:1484), ROP 0x06 du blitter — une auto-affectation chez
// PCem aussi ; on garde la ligne pour que le switch se lise en regard du C.
#pragma warning disable CS1717

using System.Text;
using iXtal26.Cpu;
using iXtal26.Flash;
using iXtal26.Memory;
using iXtal26.Models;
using iXtal26.PluginApi;
using static iXtal26.Cpu.x86;
using static iXtal26.Flash.rom;
using static iXtal26.Memory.mem;
using static iXtal26.PluginApi.device;
using static iXtal26.Video.video;
using static iXtal26.Video.vid_svga;
using static iXtal26.Video.vid_svga_render;
using static iXtal26.io;
using static iXtal26.pc;

namespace iXtal26.Video;

// pcem: vid_cl5429.c:51-66
// DEVIATION: structure ANONYME en C ; C# exige un nom. Classe : elle n'est jamais copiée,
//   seulement atteinte par gd5429->blt.
internal sealed class gd5429_blt_t
{
    internal uint32_t bg_col, fg_col;
    internal uint16_t trans_col, trans_mask;
    internal uint16_t width, height;
    internal uint16_t dst_pitch, src_pitch;
    internal uint32_t dst_addr, src_addr;
    internal uint8_t mask, mode, rop;

    internal uint32_t dst_addr_backup, src_addr_backup;
    internal uint16_t width_backup, height_internal;
    internal int x_count, y_count;
    internal int depth;

    internal int mem_word_sel;
    internal uint16_t mem_word_save;
}

// pcem: vid_cl5429.c:36-87
// Classe et non struct : son adresse est prise (svga_init, io_sethandler, mem_mapping_add).
internal sealed class gd5429_t
{
    internal mem_mapping_t mmio_mapping = new();
    internal mem_mapping_t linear_mapping = new();

    internal svga_t svga = new();

    internal rom_t bios_rom = new();

    internal uint32_t[] bank = new uint32_t[2];
    internal uint32_t mask;

    internal uint32_t vram_mask;

    internal int type;

    internal gd5429_blt_t blt = new();

    internal uint8_t hidden_dac_reg;
    internal int dac_3c6_count;

    internal uint8_t[] pci_regs = new uint8_t[256];
    internal uint8_t int_line;
    internal int card;

    internal uint8_t[] pos_regs = new uint8_t[8];
    internal svga_t? mb_vga;

    internal uint32_t lfb_base;

    internal int mmio_vram_overlap;

    internal uint8_t sr10_read, sr11_read;

    internal uint8_t[] latch_ext = new uint8_t[4];

    internal int vidsys_ena;
}

/*Cirrus Logic CL-GD5429 emulation*/
// SR7.0 = "true packed-pixel memory addressing"
internal static partial class vid_cl5429
{
    // pcem: vid_cl5429.c:19
    internal const int CL_TYPE_AVGA2 = 0, CL_TYPE_GD5426 = 1, CL_TYPE_GD5428 = 2, CL_TYPE_GD5429 = 3, CL_TYPE_GD5430 = 4, CL_TYPE_GD5434 = 5;

    // pcem: vid_cl5429.c:21-34
    private const int BLIT_DEPTH_8 = 0;
    private const int BLIT_DEPTH_16 = 1;
    private const int BLIT_DEPTH_32 = 3;

    private const int CL_GD5428_SYSTEM_BUS_MCA = 5;
    private const int CL_GD5428_SYSTEM_BUS_VESA = 6;
    private const int CL_GD5428_SYSTEM_BUS_ISA = 7;

    private const int CL_GD5429_SYSTEM_BUS_VESA = 5;
    private const int CL_GD5429_SYSTEM_BUS_ISA = 7;

    private const int CL_GD543X_SYSTEM_BUS_PCI = 4;
    private const int CL_GD543X_SYSTEM_BUS_VESA = 6;
    private const int CL_GD543X_SYSTEM_BUS_ISA = 7;

    // pcem: vid_cl5429.c:89-92
    private const int GRB_X8_ADDRESSING = (1 << 1);
    private const int GRB_WRITEMODE_EXT = (1 << 2);
    private const int GRB_8B_LATCHES = (1 << 3);
    private const int GRB_ENHANCED_16BIT = (1 << 4);

    // pcem: bus/pci.h:14, :17
    // DEVIATION: pci.h n'est pas transcrit ; ses deux constantes lues ici (gd5429_recalc_mapping)
    //   le sont sur place.
    private const int PCI_REG_COMMAND = 0x04;
    private const int PCI_COMMAND_MEM = 0x02;

    // omitted: les prototypes (vid_cl5429.c:94-109, :699) — C# n'en a pas besoin.

    // pcem: vid_cl5429.c:111-380
    internal static void gd5429_out(uint16_t addr, uint8_t val, object p)
    {
        gd5429_t gd5429 = (gd5429_t)p;
        svga_t svga = gd5429.svga;
        uint8_t old;

        if (((addr & 0xfff0) == 0x3d0 || (addr & 0xfff0) == 0x3b0) && (svga.miscout & 1) == 0)
                addr ^= 0x60;

        switch (addr)
        {
        case 0x3c3:
                if (MCA != 0)
                {
                        gd5429.vidsys_ena = val & 1;
                        // omitted: ibm_gd5428_mapping_update(gd5429) (vid_cl5429.c:125) — MCA vaut 0 sur
                        //   nos machines ; la fonction (vid_cl5429.c:1815-1835) n'est pas transcrite.
                }
                break;

        case 0x3c4:
                svga.seqaddr = val;
                break;
        case 0x3c5:
                if (svga.seqaddr > 5)
                {
                        svga.seqregs[svga.seqaddr & 0x1f] = val;
                        switch (svga.seqaddr & 0x1f)
                        {
                        case 0x10:
                        case 0x30:
                        case 0x50:
                        case 0x70:
                        case 0x90:
                        case 0xb0:
                        case 0xd0:
                        case 0xf0:
                                svga.hwcursor.x = (val << 3) | ((svga.seqaddr >> 5) & 7);
                                gd5429.sr10_read = (uint8_t)(svga.seqaddr & 0xe0);
                                break;
                        case 0x11:
                        case 0x31:
                        case 0x51:
                        case 0x71:
                        case 0x91:
                        case 0xb1:
                        case 0xd1:
                        case 0xf1:
                                svga.hwcursor.y = (val << 3) | ((svga.seqaddr >> 5) & 7);
                                gd5429.sr11_read = (uint8_t)(svga.seqaddr & 0xe0);
                                break;
                        case 0x12:
                                svga.hwcursor.ena = val & 1;
                                svga.hwcursor.ysize = ((val & 4) != 0) ? 64 : 32;
                                svga.hwcursor.yoff = 0;
                                if (svga.hwcursor.ysize == 64)
                                        svga.hwcursor.addr = (uint32_t)((0x3fc000 + ((svga.seqregs[0x13] & 0x3c) * 256)) & svga.vram_mask);
                                else
                                        svga.hwcursor.addr = (uint32_t)((0x3fc000 + ((svga.seqregs[0x13] & 0x3f) * 256)) & svga.vram_mask);
                                break;
                        case 0x13:
                                if (svga.hwcursor.ysize == 64)
                                        svga.hwcursor.addr = (uint32_t)((0x3fc000 + ((val & 0x3c) * 256)) & svga.vram_mask);
                                else
                                        svga.hwcursor.addr = (uint32_t)((0x3fc000 + ((val & 0x3f) * 256)) & svga.vram_mask);
                                break;

                        case 0x07:
                                svga.set_reset_disabled = svga.seqregs[7] & 1;
                                svga.packed_chain4 = svga.seqregs[7] & 1;
                                svga_recalctimings(svga);
                                goto case 0x17;
                        case 0x17:
                                if (gd5429.type >= CL_TYPE_GD5429)
                                        gd5429_recalc_mapping(gd5429);
                                break;
                        }
                        return;
                }
                break;

        case 0x3c6:
                if (gd5429.dac_3c6_count == 4)
                {
                        gd5429.dac_3c6_count = 0;
                        gd5429.hidden_dac_reg = val;
                        svga_recalctimings(svga);
                        return;
                }
                gd5429.dac_3c6_count = 0;
                break;
        case 0x3c7:
        case 0x3c8:
        case 0x3c9:
                gd5429.dac_3c6_count = 0;
                break;

        case 0x3cf:
                if (svga.gdcaddr == 0)
                        gd5429_mmio_write(0xb8000, val, gd5429);
                if (svga.gdcaddr == 1)
                        gd5429_mmio_write(0xb8004, val, gd5429);
                if (svga.gdcaddr == 5)
                {
                        svga.gdcreg[5] = val;
                        if ((svga.gdcreg[0xb] & 0x04) != 0)
                                svga.writemode = svga.gdcreg[5] & 7;
                        else
                                svga.writemode = svga.gdcreg[5] & 3;
                        svga.readmode = val & 8;
                        svga.chain2_read = val & 0x10;
                        return;
                }
                if (svga.gdcaddr == 6)
                {
                        if ((svga.gdcreg[6] & 0xc) != (val & 0xc))
                        {
                                svga.gdcreg[6] = val;
                                gd5429_recalc_mapping(gd5429);
                        }

                        /*Hack - the Windows 3.x drivers for the GD5426/8 require VRAM wraparound
                          for pattern & cursor writes to work correctly, but the BIOSes require
                          no wrapping to detect memory size - albeit with odd/even mode enabled.
                          This may be a quirk of address mapping. So change wrapping mode based on
                          odd/even mode for now*/
                        if (gd5429.type == CL_TYPE_GD5426 || gd5429.type == CL_TYPE_GD5428)
                        {
                                if ((val & 2) != 0) /*Odd/Even*/
                                        svga.decode_mask = 0x1fffff;
                                else
                                        svga.decode_mask = svga.vram_mask;
                        }

                        svga.gdcreg[6] = val;
                        return;
                }
                if (svga.gdcaddr > 8)
                {
                        svga.gdcreg[svga.gdcaddr & 0x3f] = val;
                        if (gd5429.type < CL_TYPE_GD5426 && (svga.gdcaddr > 0xb))
                                return;
                        switch (svga.gdcaddr)
                        {
                        case 0x09:
                        case 0x0a:
                        case 0x0b:
                                gd5429_recalc_banking(gd5429);
                                if ((svga.gdcreg[0xb] & 0x04) != 0)
                                        svga.writemode = svga.gdcreg[5] & 7;
                                else
                                        svga.writemode = svga.gdcreg[5] & 3;
                                break;

                        case 0x10:
                                gd5429_mmio_write(0xb8001, val, gd5429);
                                break;
                        case 0x11:
                                gd5429_mmio_write(0xb8005, val, gd5429);
                                break;
                        case 0x12:
                                gd5429_mmio_write(0xb8002, val, gd5429);
                                break;
                        case 0x13:
                                gd5429_mmio_write(0xb8006, val, gd5429);
                                break;
                        case 0x14:
                                gd5429_mmio_write(0xb8003, val, gd5429);
                                break;
                        case 0x15:
                                gd5429_mmio_write(0xb8007, val, gd5429);
                                break;

                        case 0x20:
                                gd5429_mmio_write(0xb8008, val, gd5429);
                                break;
                        case 0x21:
                                gd5429_mmio_write(0xb8009, val, gd5429);
                                break;
                        case 0x22:
                                gd5429_mmio_write(0xb800a, val, gd5429);
                                break;
                        case 0x23:
                                gd5429_mmio_write(0xb800b, val, gd5429);
                                break;
                        case 0x24:
                                gd5429_mmio_write(0xb800c, val, gd5429);
                                break;
                        case 0x25:
                                gd5429_mmio_write(0xb800d, val, gd5429);
                                break;
                        case 0x26:
                                gd5429_mmio_write(0xb800e, val, gd5429);
                                break;
                        case 0x27:
                                gd5429_mmio_write(0xb800f, val, gd5429);
                                break;

                        case 0x28:
                                gd5429_mmio_write(0xb8010, val, gd5429);
                                break;
                        case 0x29:
                                gd5429_mmio_write(0xb8011, val, gd5429);
                                break;
                        case 0x2a:
                                gd5429_mmio_write(0xb8012, val, gd5429);
                                break;

                        case 0x2c:
                                gd5429_mmio_write(0xb8014, val, gd5429);
                                break;
                        case 0x2d:
                                gd5429_mmio_write(0xb8015, val, gd5429);
                                break;
                        case 0x2e:
                                gd5429_mmio_write(0xb8016, val, gd5429);
                                break;

                        case 0x2f:
                                gd5429_mmio_write(0xb8017, val, gd5429);
                                break;
                        case 0x30:
                                gd5429_mmio_write(0xb8018, val, gd5429);
                                break;

                        case 0x32:
                                gd5429_mmio_write(0xb801a, val, gd5429);
                                break;

                        case 0x31:
                                gd5429_mmio_write(0xb8040, val, gd5429);
                                break;

                        case 0x34:
                                if (gd5429.type <= CL_TYPE_GD5428)
                                        gd5429.blt.trans_col = (uint16_t)((gd5429.blt.trans_col & 0xff00) | val);
                                break;
                        case 0x35:
                                if (gd5429.type <= CL_TYPE_GD5428)
                                        gd5429.blt.trans_col = (uint16_t)((gd5429.blt.trans_col & 0x00ff) | (val << 8));
                                break;
                        case 0x36:
                                if (gd5429.type <= CL_TYPE_GD5428)
                                        gd5429.blt.trans_mask = (uint16_t)((gd5429.blt.trans_mask & 0xff00) | val);
                                break;
                        case 0x37:
                                if (gd5429.type <= CL_TYPE_GD5428)
                                        gd5429.blt.trans_mask = (uint16_t)((gd5429.blt.trans_mask & 0x00ff) | (val << 8));
                                break;
                        }
                        return;
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

    // pcem: vid_cl5429.c:382-491
    internal static uint8_t gd5429_in(uint16_t addr, object p)
    {
        gd5429_t gd5429 = (gd5429_t)p;
        svga_t svga = gd5429.svga;

        if (((addr & 0xfff0) == 0x3d0 || (addr & 0xfff0) == 0x3b0) && (svga.miscout & 1) == 0)
                addr ^= 0x60;

        switch (addr)
        {
        case 0x3c3:
                if (MCA != 0)
                        return (uint8_t)gd5429.vidsys_ena;
                break;

        case 0x3c4:
                if ((svga.seqaddr & 0x1f) == 0x10)
                        return (uint8_t)((svga.seqaddr & 0x1f) | gd5429.sr10_read);
                if ((svga.seqaddr & 0x1f) == 0x11)
                        return (uint8_t)((svga.seqaddr & 0x1f) | gd5429.sr11_read);
                return (uint8_t)(svga.seqaddr & 0x1f);

        case 0x3c5:
                if (svga.seqaddr > 5)
                {
                        uint8_t temp;

                        switch (svga.seqaddr)
                        {
                        case 6:
                                return (uint8_t)(((svga.seqregs[6] & 0x17) == 0x12) ? 0x12 : 0x0f);

                        case 0x17:
                                if (gd5429.type < CL_TYPE_GD5426)
                                        break;
                                temp = svga.seqregs[0x17];
                                temp = (uint8_t)(temp & ~(7 << 3));
                                if (gd5429.type == CL_TYPE_GD5426 || gd5429.type == CL_TYPE_GD5428)
                                {
                                        if (MCA != 0)
                                                temp |= (CL_GD5428_SYSTEM_BUS_MCA << 3);
                                        else if (cpu_c.has_vlb != 0)
                                                temp |= (CL_GD5428_SYSTEM_BUS_VESA << 3);
                                        else
                                                temp |= (CL_GD5428_SYSTEM_BUS_ISA << 3);
                                }
                                else if (gd5429.type == CL_TYPE_GD5429)
                                {
                                        if (cpu_c.has_vlb != 0)
                                                temp |= (CL_GD5429_SYSTEM_BUS_VESA << 3);
                                        else
                                                temp |= (CL_GD5429_SYSTEM_BUS_ISA << 3);
                                }
                                else
                                {
                                        if (PCI != 0)
                                                temp |= (CL_GD543X_SYSTEM_BUS_PCI << 3);
                                        else if (cpu_c.has_vlb != 0)
                                                temp |= (CL_GD543X_SYSTEM_BUS_VESA << 3);
                                        else
                                                temp |= (CL_GD543X_SYSTEM_BUS_ISA << 3);
                                }
                                return temp;
                        }
                        return svga.seqregs[svga.seqaddr & 0x3f];
                }
                break;

        case 0x3c6:
                if (gd5429.dac_3c6_count == 4)
                {
                        gd5429.dac_3c6_count = 0;
                        return gd5429.hidden_dac_reg;
                }
                gd5429.dac_3c6_count++;
                break;
        case 0x3c7:
        case 0x3c8:
        case 0x3c9:
                gd5429.dac_3c6_count = 0;
                break;

        case 0x3cf:
                if (svga.gdcaddr > 8)
                {
                        return svga.gdcreg[svga.gdcaddr & 0x3f];
                }
                break;

        case 0x3D4:
                return svga.crtcreg;
        case 0x3D5:
                switch (svga.crtcreg)
                {
                case 0x27: /*ID*/
                        switch (gd5429.type)
                        {
                        case CL_TYPE_AVGA2:
                                return 0x18; /*AVGA2*/
                        case CL_TYPE_GD5426:
                                return 0x90; /*GD5426*/
                        case CL_TYPE_GD5428:
                                return 0x98; /*GD5428*/
                        case CL_TYPE_GD5429:
                                return 0x9c; /*GD5429*/
                        case CL_TYPE_GD5430:
                                return 0xa0; /*GD5430*/
                        case CL_TYPE_GD5434:
                                return 0xa8; /*GD5434*/
                        }
                        break;
                case 0x28: /*Class ID*/
                        if (gd5429.type == CL_TYPE_GD5430)
                                return 0xff; /*Standard CL-GD5430*/
                        break;
                }
                return svga.crtc[svga.crtcreg];
        }
        return svga_in(addr, svga);
    }

    // pcem: vid_cl5429.c:493-508
    internal static void gd5429_recalc_banking(gd5429_t gd5429)
    {
        svga_t svga = gd5429.svga;

        if ((svga.gdcreg[0xb] & 0x20) != 0)
                gd5429.bank[0] = (uint32_t)((svga.gdcreg[0x09] & 0xff) << 14);
        else
                gd5429.bank[0] = (uint32_t)(svga.gdcreg[0x09] << 12);

        if ((svga.gdcreg[0xb] & 0x01) != 0)
        {
                if ((svga.gdcreg[0xb] & 0x20) != 0)
                        gd5429.bank[1] = (uint32_t)((svga.gdcreg[0x0a] & 0xff) << 14);
                else
                        gd5429.bank[1] = (uint32_t)(svga.gdcreg[0x0a] << 12);
        }
        else
                gd5429.bank[1] = gd5429.bank[0] + 0x8000;
    }

    // pcem: vid_cl5429.c:510-573
    internal static void gd5429_recalc_mapping(gd5429_t gd5429)
    {
        svga_t svga = gd5429.svga;

        if ((PCI != 0 && gd5429.type >= CL_TYPE_GD5430 && (gd5429.pci_regs[PCI_REG_COMMAND] & PCI_COMMAND_MEM) == 0) ||
            (MCA != 0 && ((gd5429.pos_regs[2] & 0x01) == 0 || gd5429.vidsys_ena == 0)))
        {
                mem_mapping_disable(svga.mapping);
                mem_mapping_disable(gd5429.linear_mapping);
                mem_mapping_disable(gd5429.mmio_mapping);
                return;
        }

        gd5429.mmio_vram_overlap = 0;

        if ((svga.seqregs[7] & 0xf0) == 0)
        {
                mem_mapping_disable(gd5429.linear_mapping);
                switch (svga.gdcreg[6] & 0x0C)
                {
                case 0x0: /*128k at A0000*/
                        mem_mapping_set_addr(svga.mapping, 0xa0000, 0x10000);
                        svga.banked_mask = 0xffff;
                        break;
                case 0x4: /*64k at A0000*/
                        mem_mapping_set_addr(svga.mapping, 0xa0000, 0x10000);
                        svga.banked_mask = 0xffff;
                        break;
                case 0x8: /*32k at B0000*/
                        mem_mapping_set_addr(svga.mapping, 0xb0000, 0x08000);
                        svga.banked_mask = 0x7fff;
                        break;
                case 0xC: /*32k at B8000*/
                        mem_mapping_set_addr(svga.mapping, 0xb8000, 0x08000);
                        svga.banked_mask = 0x7fff;
                        gd5429.mmio_vram_overlap = 1;
                        break;
                }
                if (gd5429.type >= CL_TYPE_GD5429 && (svga.seqregs[0x17] & 0x04) != 0)
                        mem_mapping_set_addr(gd5429.mmio_mapping, 0xb8000, 0x00100);
                else
                        mem_mapping_disable(gd5429.mmio_mapping);
        }
        else
        {
                uint32_t @base, size;

                if (gd5429.type <= CL_TYPE_GD5429 || (PCI == 0 && cpu_c.has_vlb == 0))
                {
                        @base = (uint32_t)((svga.seqregs[7] & 0xf0) << 16);
                        if ((svga.gdcreg[0xb] & 0x20) != 0)
                                size = 1 * 1024 * 1024;
                        else
                                size = 2 * 1024 * 1024;
                }
                else if (PCI != 0)
                {
                        @base = gd5429.lfb_base;
                        size = 4 * 1024 * 1024;
                }
                else /*VLB*/
                {
                        @base = 128 * 1024 * 1024;
                        size = 4 * 1024 * 1024;
                }
                mem_mapping_disable(svga.mapping);
                mem_mapping_set_addr(gd5429.linear_mapping, @base, size);
                if (gd5429.type >= CL_TYPE_GD5429 && (svga.seqregs[0x17] & 0x04) != 0)
                        mem_mapping_set_addr(gd5429.mmio_mapping, 0xb8000, 0x00100);
                else
                        mem_mapping_disable(gd5429.mmio_mapping);
        }
    }

    // pcem: vid_cl5429.c:575-644
    internal static void gd5429_recalctimings(svga_t svga)
    {
        gd5429_t gd5429 = (gd5429_t)svga.p;
        int clock = (svga.miscout >> 2) & 3;
        int n, d, p;
        double vclk;

        if ((svga.crtc[0x1b] & 0x10) != 0)
                svga.rowoffset |= 0x100;

        if (svga.rowoffset == 0)
                svga.rowoffset = 0x100;

        svga.interlace = svga.crtc[0x1a] & 1;

        if ((svga.seqregs[7] & 0x01) != 0)
                svga.render = svga_render_8bpp_highres;

        svga.ma_latch |= (uint32_t)(((svga.crtc[0x1b] & 0x01) << 16) | ((svga.crtc[0x1b] & 0xc) << 15));

        svga.bpp = 8;
        if ((gd5429.hidden_dac_reg & 0x80) != 0)
        {
                if ((gd5429.hidden_dac_reg & 0x40) != 0)
                {
                        switch (gd5429.hidden_dac_reg & 0xf)
                        {
                        case 0x0:
                                svga.render = svga_render_15bpp_highres;
                                svga.bpp = 15;
                                break;
                        case 0x1:
                                svga.render = svga_render_16bpp_highres;
                                svga.bpp = 16;
                                break;
                        case 0x5:
                                if (gd5429.type >= CL_TYPE_GD5434 && (svga.seqregs[7] & 8) != 0)
                                {
                                        svga.render = svga_render_32bpp_highres;
                                        svga.bpp = 32;
                                        svga.rowoffset *= 2;
                                }
                                else
                                {
                                        svga.render = svga_render_24bpp_highres;
                                        svga.bpp = 24;
                                }
                                break;
                        }
                }
                else
                {
                        svga.render = svga_render_15bpp_highres;
                        svga.bpp = 15;
                }
        }

        n = svga.seqregs[0xb + clock] & 0x7f;
        d = (svga.seqregs[0x1b + clock] >> 1) & 0x1f;
        p = svga.seqregs[0x1b + clock] & 1;

        /*Prevent divide by zero during clock setup*/
        if (d != 0 && n != 0)
                vclk = (14318184.0 * ((float)n / (float)d)) / (float)(1 + p);
        else
                vclk = 14318184.0;
        switch (svga.seqregs[7] & ((gd5429.type >= CL_TYPE_GD5434) ? 0xe : 0x6))
        {
        case 2:
                vclk /= 2.0;
                break;
        case 4:
                vclk /= 3.0;
                break;
        }
        svga.clock = (pit.cpuclock * (float)(1UL << 32)) / vclk;

        svga.vram_display_mask = ((svga.crtc[0x1b] & 2) != 0) ? gd5429.vram_mask : 0x3ffff;
    }

    // pcem: vid_cl5429.c:646-697
    internal static void gd5429_hwcursor_draw(svga_t svga, int displine)
    {
        int x;
        Span<uint8_t> dat = stackalloc uint8_t[2];
        int xx;
        int offset = svga.hwcursor_latch.x - svga.hwcursor_latch.xoff;
        int line_offset = ((svga.seqregs[0x12] & 0x04) != 0) ? 16 : 4;

        if (svga.interlace != 0 && svga.hwcursor_oddeven != 0)
                svga.hwcursor_latch.addr += (uint32_t)line_offset;

        // pcem bug, not reproduced: PB-84 — DEVIATION : en entrelacé, line_offset s'ajoute à chaque
        // ligne et le curseur logé en haut de la VRAM en sort (lecture du tas en C, exception en C#).
        // Les index sont masqués par vram_mask (R9). PB-85 : l'écriture hors de buffer32 est sautée.
        if ((svga.seqregs[0x12] & 0x04) != 0)
        {
                for (x = 0; x < 64; x += 8)
                {
                        dat[0] = svga.vram[svga.hwcursor_latch.addr & svga.vram_mask];
                        dat[1] = svga.vram[(svga.hwcursor_latch.addr + 8) & svga.vram_mask];
                        for (xx = 0; xx < 8; xx++)
                        {
                                if (offset >= svga.hwcursor_latch.x)
                                {
                                        if ((dat[1] & 0x80) != 0)
                                        if ((uint)(displine * Stride + offset + 32) < (uint)Buffer32.Length) // PB-85
                                                Buffer32[displine * Stride + offset + 32] = 0;
                                        if ((dat[0] & 0x80) != 0)
                                        if ((uint)(displine * Stride + offset + 32) < (uint)Buffer32.Length) // PB-85
                                                Buffer32[displine * Stride + offset + 32] ^= 0xffffff;
                                }

                                offset++;
                                dat[0] <<= 1;
                                dat[1] <<= 1;
                        }
                        svga.hwcursor_latch.addr++;
                }
                svga.hwcursor_latch.addr += 8;
        }
        else
        {
                for (x = 0; x < 32; x += 8)
                {
                        dat[0] = svga.vram[svga.hwcursor_latch.addr & svga.vram_mask];
                        dat[1] = svga.vram[(svga.hwcursor_latch.addr + 0x80) & svga.vram_mask];
                        for (xx = 0; xx < 8; xx++)
                        {
                                if (offset >= svga.hwcursor_latch.x)
                                {
                                        if ((dat[1] & 0x80) != 0)
                                        if ((uint)(displine * Stride + offset + 32) < (uint)Buffer32.Length) // PB-85
                                                Buffer32[displine * Stride + offset + 32] = 0;
                                        if ((dat[0] & 0x80) != 0)
                                        if ((uint)(displine * Stride + offset + 32) < (uint)Buffer32.Length) // PB-85
                                                Buffer32[displine * Stride + offset + 32] ^= 0xffffff;
                                }

                                offset++;
                                dat[0] <<= 1;
                                dat[1] <<= 1;
                        }
                        svga.hwcursor_latch.addr++;
                }
        }

        if (svga.interlace != 0 && svga.hwcursor_oddeven == 0)
                svga.hwcursor_latch.addr += (uint32_t)line_offset;
    }

    // pcem: vid_cl5429.c:701-709
    private static void gd5429_write(uint32_t addr, uint8_t val, object? p)
    {
        gd5429_t gd5429 = (gd5429_t)p;
        svga_t svga = gd5429.svga;

        addr &= svga.banked_mask;
        addr = (addr & 0x7fff) + gd5429.bank[(addr >> 15) & 1];

        gd5429_write_linear(addr, val, p);
    }
    // pcem: vid_cl5429.c:710-723
    private static void gd5429_writew(uint32_t addr, uint16_t val, object? p)
    {
        gd5429_t gd5429 = (gd5429_t)p;
        svga_t svga = gd5429.svga;

        addr &= svga.banked_mask;
        addr = (addr & 0x7fff) + gd5429.bank[(addr >> 15) & 1];

        if ((svga.writemode < 4) && (svga.gdcreg[0xb] & (GRB_X8_ADDRESSING | GRB_8B_LATCHES)) == 0)
                svga_writew_linear(addr, val, svga);
        else
        {
                gd5429_write_linear(addr, (uint8_t)val, p);
                gd5429_write_linear(addr + 1, (uint8_t)(val >> 8), p);
        }
    }
    // pcem: vid_cl5429.c:724-739
    private static void gd5429_writel(uint32_t addr, uint32_t val, object? p)
    {
        gd5429_t gd5429 = (gd5429_t)p;
        svga_t svga = gd5429.svga;

        addr &= svga.banked_mask;
        addr = (addr & 0x7fff) + gd5429.bank[(addr >> 15) & 1];

        if ((svga.writemode < 4) && (svga.gdcreg[0xb] & (GRB_X8_ADDRESSING | GRB_8B_LATCHES)) == 0)
                svga_writel_linear(addr, val, svga);
        else
        {
                gd5429_write_linear(addr, (uint8_t)val, p);
                gd5429_write_linear(addr + 1, (uint8_t)(val >> 8), p);
                gd5429_write_linear(addr + 2, (uint8_t)(val >> 16), p);
                gd5429_write_linear(addr + 3, (uint8_t)(val >> 24), p);
        }
    }

    // pcem: vid_cl5429.c:741-748
    private static uint8_t gd5429_read(uint32_t addr, object? p)
    {
        gd5429_t gd5429 = (gd5429_t)p;
        svga_t svga = gd5429.svga;

        addr &= svga.banked_mask;
        addr = (addr & 0x7fff) + gd5429.bank[(addr >> 15) & 1];
        return gd5429_read_linear(addr, gd5429);
    }
    // pcem: vid_cl5429.c:749-759
    private static uint16_t gd5429_readw(uint32_t addr, object? p)
    {
        gd5429_t gd5429 = (gd5429_t)p;
        svga_t svga = gd5429.svga;

        addr &= svga.banked_mask;
        addr = (addr & 0x7fff) + gd5429.bank[(addr >> 15) & 1];

        if ((svga.gdcreg[0xb] & (GRB_X8_ADDRESSING | GRB_8B_LATCHES)) == 0)
                return svga_readw_linear(addr, gd5429.svga);
        return (uint16_t)(gd5429_read_linear(addr, gd5429) | (gd5429_read_linear(addr + 1, gd5429) << 8));
    }
    // pcem: vid_cl5429.c:760-771
    private static uint32_t gd5429_readl(uint32_t addr, object? p)
    {
        gd5429_t gd5429 = (gd5429_t)p;
        svga_t svga = gd5429.svga;

        addr &= svga.banked_mask;
        addr = (addr & 0x7fff) + gd5429.bank[(addr >> 15) & 1];

        if ((svga.gdcreg[0xb] & (GRB_X8_ADDRESSING | GRB_8B_LATCHES)) == 0)
                return svga_readl_linear(addr, gd5429.svga);
        return (uint32_t)gd5429_read_linear(addr, gd5429) | ((uint32_t)gd5429_read_linear(addr + 1, gd5429) << 8) |
               ((uint32_t)gd5429_read_linear(addr + 2, gd5429) << 16) | ((uint32_t)gd5429_read_linear(addr + 3, gd5429) << 24);
    }

    // pcem: vid_cl5429.c:773-1158
    internal static void gd5429_write_linear(uint32_t addr, uint8_t val, object? p)
    {
        gd5429_t gd5429 = (gd5429_t)p;
        svga_t svga = gd5429.svga;
        uint8_t vala, valb, valc, vald, wm = svga.writemask;
        int writemask2 = svga.seqregs[2];

        cycles -= video_timing_write_b;
        cycles_lost += video_timing_write_b;

        egawrites++;

        if ((svga.gdcreg[6] & 1) == 0)
                svga.fullchange = 2;
        if ((svga.gdcreg[0xb] & GRB_ENHANCED_16BIT) != 0)
                addr <<= 4;
        else if ((svga.gdcreg[0xb] & GRB_X8_ADDRESSING) != 0)
                addr <<= 3;
        else if (((svga.chain4 != 0 && svga.packed_chain4 != 0) || svga.fb_only != 0) && (svga.writemode < 4))
        {
                writemask2 = 1 << (int)(addr & 3);
                addr &= ~3u;
        }
        else if (svga.chain4 != 0)
        {
                writemask2 = 1 << (int)(addr & 3);
                addr = ((addr & 0xfffc) << 2) | ((addr & 0x30000) >> 14) | (addr & ~0x3ffffu);
        }
        else if (svga.chain2_write != 0)
        {
                writemask2 &= ~0xa;
                if ((addr & 1) != 0)
                        writemask2 <<= 1;
                addr &= ~1u;
                addr <<= 2;
        }
        else
        {
                addr <<= 2;
        }
        addr &= svga.decode_mask;
        if (addr >= svga.vram_max)
                return;
        addr &= svga.vram_mask;
        svga.changedvram[addr >> 12] = (uint8_t)changeframecount;

        switch (svga.writemode)
        {
        case 4:
                if ((svga.gdcreg[0xb] & GRB_ENHANCED_16BIT) != 0)
                {
                        svga.changedvram[addr >> 12] = (uint8_t)changeframecount;
                        if ((val & svga.seqregs[2] & 0x80) != 0)
                        {
                                svga.vram[addr + 0] = svga.gdcreg[1];
                                svga.vram[addr + 1] = svga.gdcreg[0x11];
                        }
                        if ((val & svga.seqregs[2] & 0x40) != 0)
                        {
                                svga.vram[addr + 2] = svga.gdcreg[1];
                                svga.vram[addr + 3] = svga.gdcreg[0x11];
                        }
                        if ((val & svga.seqregs[2] & 0x20) != 0)
                        {
                                svga.vram[addr + 4] = svga.gdcreg[1];
                                svga.vram[addr + 5] = svga.gdcreg[0x11];
                        }
                        if ((val & svga.seqregs[2] & 0x10) != 0)
                        {
                                svga.vram[addr + 6] = svga.gdcreg[1];
                                svga.vram[addr + 7] = svga.gdcreg[0x11];
                        }
                        if ((val & svga.seqregs[2] & 0x08) != 0)
                        {
                                svga.vram[addr + 8] = svga.gdcreg[1];
                                svga.vram[addr + 9] = svga.gdcreg[0x11];
                        }
                        if ((val & svga.seqregs[2] & 0x04) != 0)
                        {
                                svga.vram[addr + 10] = svga.gdcreg[1];
                                svga.vram[addr + 11] = svga.gdcreg[0x11];
                        }
                        if ((val & svga.seqregs[2] & 0x02) != 0)
                        {
                                svga.vram[addr + 12] = svga.gdcreg[1];
                                svga.vram[addr + 13] = svga.gdcreg[0x11];
                        }
                        if ((val & svga.seqregs[2] & 0x01) != 0)
                        {
                                svga.vram[addr + 14] = svga.gdcreg[1];
                                svga.vram[addr + 15] = svga.gdcreg[0x11];
                        }
                }
                else
                {
                        svga.changedvram[addr >> 12] = (uint8_t)changeframecount;
                        // pcem bug, not reproduced: PB-82 — DEVIATION : sans X8 ni 16 bits étendus, addr n'est
                        // aligné que sur 4 (ou pas du tout en chain4) ; addr + 7 sort de la VRAM en haut du
                        // tableau (écriture dans le tas en C, exception ici). Chaque octet est masqué (R9).
                        if ((val & svga.seqregs[2] & 0x80) != 0)
                                svga.vram[(addr + 0) & svga.vram_mask] = svga.gdcreg[1];
                        if ((val & svga.seqregs[2] & 0x40) != 0)
                                svga.vram[(addr + 1) & svga.vram_mask] = svga.gdcreg[1];
                        if ((val & svga.seqregs[2] & 0x20) != 0)
                                svga.vram[(addr + 2) & svga.vram_mask] = svga.gdcreg[1];
                        if ((val & svga.seqregs[2] & 0x10) != 0)
                                svga.vram[(addr + 3) & svga.vram_mask] = svga.gdcreg[1];
                        if ((val & svga.seqregs[2] & 0x08) != 0)
                                svga.vram[(addr + 4) & svga.vram_mask] = svga.gdcreg[1];
                        if ((val & svga.seqregs[2] & 0x04) != 0)
                                svga.vram[(addr + 5) & svga.vram_mask] = svga.gdcreg[1];
                        if ((val & svga.seqregs[2] & 0x02) != 0)
                                svga.vram[(addr + 6) & svga.vram_mask] = svga.gdcreg[1];
                        if ((val & svga.seqregs[2] & 0x01) != 0)
                                svga.vram[(addr + 7) & svga.vram_mask] = svga.gdcreg[1];
                }
                break;

        case 5:
                if ((svga.gdcreg[0xb] & GRB_ENHANCED_16BIT) != 0)
                {
                        svga.changedvram[addr >> 12] = (uint8_t)changeframecount;
                        if ((svga.seqregs[2] & 0x80) != 0)
                        {
                                svga.vram[addr + 0] = ((val & 0x80) != 0) ? svga.gdcreg[1] : svga.gdcreg[0];
                                svga.vram[addr + 1] = ((val & 0x80) != 0) ? svga.gdcreg[0x11] : svga.gdcreg[0x10];
                        }
                        if ((svga.seqregs[2] & 0x40) != 0)
                        {
                                svga.vram[addr + 2] = ((val & 0x40) != 0) ? svga.gdcreg[1] : svga.gdcreg[0];
                                svga.vram[addr + 3] = ((val & 0x40) != 0) ? svga.gdcreg[0x11] : svga.gdcreg[0x10];
                        }
                        if ((svga.seqregs[2] & 0x20) != 0)
                        {
                                svga.vram[addr + 4] = ((val & 0x20) != 0) ? svga.gdcreg[1] : svga.gdcreg[0];
                                svga.vram[addr + 5] = ((val & 0x20) != 0) ? svga.gdcreg[0x11] : svga.gdcreg[0x10];
                        }
                        if ((svga.seqregs[2] & 0x10) != 0)
                        {
                                svga.vram[addr + 6] = ((val & 0x10) != 0) ? svga.gdcreg[1] : svga.gdcreg[0];
                                svga.vram[addr + 7] = ((val & 0x10) != 0) ? svga.gdcreg[0x11] : svga.gdcreg[0x10];
                        }
                        if ((svga.seqregs[2] & 0x08) != 0)
                        {
                                svga.vram[addr + 8] = ((val & 0x08) != 0) ? svga.gdcreg[1] : svga.gdcreg[0];
                                svga.vram[addr + 9] = ((val & 0x08) != 0) ? svga.gdcreg[0x11] : svga.gdcreg[0x10];
                        }
                        if ((svga.seqregs[2] & 0x04) != 0)
                        {
                                svga.vram[addr + 10] = ((val & 0x04) != 0) ? svga.gdcreg[1] : svga.gdcreg[0];
                                svga.vram[addr + 11] = ((val & 0x04) != 0) ? svga.gdcreg[0x11] : svga.gdcreg[0x10];
                        }
                        if ((svga.seqregs[2] & 0x02) != 0)
                        {
                                svga.vram[addr + 12] = ((val & 0x02) != 0) ? svga.gdcreg[1] : svga.gdcreg[0];
                                svga.vram[addr + 13] = ((val & 0x02) != 0) ? svga.gdcreg[0x11] : svga.gdcreg[0x10];
                        }
                        if ((svga.seqregs[2] & 0x01) != 0)
                        {
                                svga.vram[addr + 14] = ((val & 0x01) != 0) ? svga.gdcreg[1] : svga.gdcreg[0];
                                svga.vram[addr + 15] = ((val & 0x01) != 0) ? svga.gdcreg[0x11] : svga.gdcreg[0x10];
                        }
                }
                else
                {
                        svga.changedvram[addr >> 12] = (uint8_t)changeframecount;
                        // pcem bug, not reproduced: PB-82 — DEVIATION : sans X8 ni 16 bits étendus, addr n'est
                        // aligné que sur 4 (ou pas du tout en chain4) ; addr + 7 sort de la VRAM en haut du
                        // tableau (écriture dans le tas en C, exception ici). Chaque octet est masqué (R9).
                        if ((svga.seqregs[2] & 0x80) != 0)
                                svga.vram[(addr + 0) & svga.vram_mask] = ((val & 0x80) != 0) ? svga.gdcreg[1] : svga.gdcreg[0];
                        if ((svga.seqregs[2] & 0x40) != 0)
                                svga.vram[(addr + 1) & svga.vram_mask] = ((val & 0x40) != 0) ? svga.gdcreg[1] : svga.gdcreg[0];
                        if ((svga.seqregs[2] & 0x20) != 0)
                                svga.vram[(addr + 2) & svga.vram_mask] = ((val & 0x20) != 0) ? svga.gdcreg[1] : svga.gdcreg[0];
                        if ((svga.seqregs[2] & 0x10) != 0)
                                svga.vram[(addr + 3) & svga.vram_mask] = ((val & 0x10) != 0) ? svga.gdcreg[1] : svga.gdcreg[0];
                        if ((svga.seqregs[2] & 0x08) != 0)
                                svga.vram[(addr + 4) & svga.vram_mask] = ((val & 0x08) != 0) ? svga.gdcreg[1] : svga.gdcreg[0];
                        if ((svga.seqregs[2] & 0x04) != 0)
                                svga.vram[(addr + 5) & svga.vram_mask] = ((val & 0x04) != 0) ? svga.gdcreg[1] : svga.gdcreg[0];
                        if ((svga.seqregs[2] & 0x02) != 0)
                                svga.vram[(addr + 6) & svga.vram_mask] = ((val & 0x02) != 0) ? svga.gdcreg[1] : svga.gdcreg[0];
                        if ((svga.seqregs[2] & 0x01) != 0)
                                svga.vram[(addr + 7) & svga.vram_mask] = ((val & 0x01) != 0) ? svga.gdcreg[1] : svga.gdcreg[0];
                }
                break;

        case 1:
                if ((svga.gdcreg[0xb] & GRB_WRITEMODE_EXT) != 0)
                {
                        if ((writemask2 & 0x80) != 0)
                                svga.vram[addr] = svga.la;
                        if ((writemask2 & 0x40) != 0)
                                svga.vram[addr | 0x1] = svga.lb;
                        if ((writemask2 & 0x20) != 0)
                                svga.vram[addr | 0x2] = svga.lc;
                        if ((writemask2 & 0x10) != 0)
                                svga.vram[addr | 0x3] = svga.ld;
                        if ((svga.gdcreg[0xb] & GRB_8B_LATCHES) != 0)
                        {
                                if ((writemask2 & 0x08) != 0)
                                        svga.vram[addr | 0x4] = gd5429.latch_ext[0];
                                if ((writemask2 & 0x04) != 0)
                                        svga.vram[addr | 0x5] = gd5429.latch_ext[1];
                                if ((writemask2 & 0x02) != 0)
                                        svga.vram[addr | 0x6] = gd5429.latch_ext[2];
                                if ((writemask2 & 0x01) != 0)
                                        svga.vram[addr | 0x7] = gd5429.latch_ext[3];
                        }
                }
                else
                {
                        if ((writemask2 & 1) != 0)
                                svga.vram[addr] = svga.la;
                        if ((writemask2 & 2) != 0)
                                svga.vram[addr | 0x1] = svga.lb;
                        if ((writemask2 & 4) != 0)
                                svga.vram[addr | 0x2] = svga.lc;
                        if ((writemask2 & 8) != 0)
                                svga.vram[addr | 0x3] = svga.ld;
                }
                break;
        case 0:
                if ((svga.gdcreg[3] & 7) != 0)
                        val = svga_rotate[svga.gdcreg[3] & 7, val];
                if (svga.gdcreg[8] == 0xff && (svga.gdcreg[3] & 0x18) == 0 && (svga.gdcreg[1] == 0 || svga.set_reset_disabled != 0))
                {
                        if ((writemask2 & 1) != 0)
                                svga.vram[addr] = val;
                        if ((writemask2 & 2) != 0)
                                svga.vram[addr | 0x1] = val;
                        if ((writemask2 & 4) != 0)
                                svga.vram[addr | 0x2] = val;
                        if ((writemask2 & 8) != 0)
                                svga.vram[addr | 0x3] = val;
                }
                else
                {
                        if ((svga.gdcreg[1] & 1) != 0)
                                vala = (uint8_t)(((svga.gdcreg[0] & 1) != 0) ? 0xff : 0);
                        else
                                vala = val;
                        if ((svga.gdcreg[1] & 2) != 0)
                                valb = (uint8_t)(((svga.gdcreg[0] & 2) != 0) ? 0xff : 0);
                        else
                                valb = val;
                        if ((svga.gdcreg[1] & 4) != 0)
                                valc = (uint8_t)(((svga.gdcreg[0] & 4) != 0) ? 0xff : 0);
                        else
                                valc = val;
                        if ((svga.gdcreg[1] & 8) != 0)
                                vald = (uint8_t)(((svga.gdcreg[0] & 8) != 0) ? 0xff : 0);
                        else
                                vald = val;

                        switch (svga.gdcreg[3] & 0x18)
                        {
                        case 0: /*Set*/
                                if ((writemask2 & 1) != 0)
                                        svga.vram[addr] = (uint8_t)((vala & svga.gdcreg[8]) | (svga.la & ~svga.gdcreg[8]));
                                if ((writemask2 & 2) != 0)
                                        svga.vram[addr | 0x1] = (uint8_t)((valb & svga.gdcreg[8]) | (svga.lb & ~svga.gdcreg[8]));
                                if ((writemask2 & 4) != 0)
                                        svga.vram[addr | 0x2] = (uint8_t)((valc & svga.gdcreg[8]) | (svga.lc & ~svga.gdcreg[8]));
                                if ((writemask2 & 8) != 0)
                                        svga.vram[addr | 0x3] = (uint8_t)((vald & svga.gdcreg[8]) | (svga.ld & ~svga.gdcreg[8]));
                                break;
                        case 8: /*AND*/
                                if ((writemask2 & 1) != 0)
                                        svga.vram[addr] = (uint8_t)((vala | ~svga.gdcreg[8]) & svga.la);
                                if ((writemask2 & 2) != 0)
                                        svga.vram[addr | 0x1] = (uint8_t)((valb | ~svga.gdcreg[8]) & svga.lb);
                                if ((writemask2 & 4) != 0)
                                        svga.vram[addr | 0x2] = (uint8_t)((valc | ~svga.gdcreg[8]) & svga.lc);
                                if ((writemask2 & 8) != 0)
                                        svga.vram[addr | 0x3] = (uint8_t)((vald | ~svga.gdcreg[8]) & svga.ld);
                                break;
                        case 0x10: /*OR*/
                                if ((writemask2 & 1) != 0)
                                        svga.vram[addr] = (uint8_t)((vala & svga.gdcreg[8]) | svga.la);
                                if ((writemask2 & 2) != 0)
                                        svga.vram[addr | 0x1] = (uint8_t)((valb & svga.gdcreg[8]) | svga.lb);
                                if ((writemask2 & 4) != 0)
                                        svga.vram[addr | 0x2] = (uint8_t)((valc & svga.gdcreg[8]) | svga.lc);
                                if ((writemask2 & 8) != 0)
                                        svga.vram[addr | 0x3] = (uint8_t)((vald & svga.gdcreg[8]) | svga.ld);
                                break;
                        case 0x18: /*XOR*/
                                if ((writemask2 & 1) != 0)
                                        svga.vram[addr] = (uint8_t)((vala & svga.gdcreg[8]) ^ svga.la);
                                if ((writemask2 & 2) != 0)
                                        svga.vram[addr | 0x1] = (uint8_t)((valb & svga.gdcreg[8]) ^ svga.lb);
                                if ((writemask2 & 4) != 0)
                                        svga.vram[addr | 0x2] = (uint8_t)((valc & svga.gdcreg[8]) ^ svga.lc);
                                if ((writemask2 & 8) != 0)
                                        svga.vram[addr | 0x3] = (uint8_t)((vald & svga.gdcreg[8]) ^ svga.ld);
                                break;
                        }
                }
                break;
        case 2:
                if ((svga.gdcreg[3] & 0x18) == 0 && svga.gdcreg[1] == 0)
                {
                        if ((writemask2 & 1) != 0)
                                svga.vram[addr] = (uint8_t)(((((val & 1) != 0) ? 0xff : 0) & svga.gdcreg[8]) | (svga.la & ~svga.gdcreg[8]));
                        if ((writemask2 & 2) != 0)
                                svga.vram[addr | 0x1] =
                                        (uint8_t)(((((val & 2) != 0) ? 0xff : 0) & svga.gdcreg[8]) | (svga.lb & ~svga.gdcreg[8]));
                        if ((writemask2 & 4) != 0)
                                svga.vram[addr | 0x2] =
                                        (uint8_t)(((((val & 4) != 0) ? 0xff : 0) & svga.gdcreg[8]) | (svga.lc & ~svga.gdcreg[8]));
                        if ((writemask2 & 8) != 0)
                                svga.vram[addr | 0x3] =
                                        (uint8_t)(((((val & 8) != 0) ? 0xff : 0) & svga.gdcreg[8]) | (svga.ld & ~svga.gdcreg[8]));
                }
                else
                {
                        vala = (uint8_t)(((val & 1) != 0) ? 0xff : 0);
                        valb = (uint8_t)(((val & 2) != 0) ? 0xff : 0);
                        valc = (uint8_t)(((val & 4) != 0) ? 0xff : 0);
                        vald = (uint8_t)(((val & 8) != 0) ? 0xff : 0);
                        switch (svga.gdcreg[3] & 0x18)
                        {
                        case 0: /*Set*/
                                if ((writemask2 & 1) != 0)
                                        svga.vram[addr] = (uint8_t)((vala & svga.gdcreg[8]) | (svga.la & ~svga.gdcreg[8]));
                                if ((writemask2 & 2) != 0)
                                        svga.vram[addr | 0x1] = (uint8_t)((valb & svga.gdcreg[8]) | (svga.lb & ~svga.gdcreg[8]));
                                if ((writemask2 & 4) != 0)
                                        svga.vram[addr | 0x2] = (uint8_t)((valc & svga.gdcreg[8]) | (svga.lc & ~svga.gdcreg[8]));
                                if ((writemask2 & 8) != 0)
                                        svga.vram[addr | 0x3] = (uint8_t)((vald & svga.gdcreg[8]) | (svga.ld & ~svga.gdcreg[8]));
                                break;
                        case 8: /*AND*/
                                if ((writemask2 & 1) != 0)
                                        svga.vram[addr] = (uint8_t)((vala | ~svga.gdcreg[8]) & svga.la);
                                if ((writemask2 & 2) != 0)
                                        svga.vram[addr | 0x1] = (uint8_t)((valb | ~svga.gdcreg[8]) & svga.lb);
                                if ((writemask2 & 4) != 0)
                                        svga.vram[addr | 0x2] = (uint8_t)((valc | ~svga.gdcreg[8]) & svga.lc);
                                if ((writemask2 & 8) != 0)
                                        svga.vram[addr | 0x3] = (uint8_t)((vald | ~svga.gdcreg[8]) & svga.ld);
                                break;
                        case 0x10: /*OR*/
                                if ((writemask2 & 1) != 0)
                                        svga.vram[addr] = (uint8_t)((vala & svga.gdcreg[8]) | svga.la);
                                if ((writemask2 & 2) != 0)
                                        svga.vram[addr | 0x1] = (uint8_t)((valb & svga.gdcreg[8]) | svga.lb);
                                if ((writemask2 & 4) != 0)
                                        svga.vram[addr | 0x2] = (uint8_t)((valc & svga.gdcreg[8]) | svga.lc);
                                if ((writemask2 & 8) != 0)
                                        svga.vram[addr | 0x3] = (uint8_t)((vald & svga.gdcreg[8]) | svga.ld);
                                break;
                        case 0x18: /*XOR*/
                                if ((writemask2 & 1) != 0)
                                        svga.vram[addr] = (uint8_t)((vala & svga.gdcreg[8]) ^ svga.la);
                                if ((writemask2 & 2) != 0)
                                        svga.vram[addr | 0x1] = (uint8_t)((valb & svga.gdcreg[8]) ^ svga.lb);
                                if ((writemask2 & 4) != 0)
                                        svga.vram[addr | 0x2] = (uint8_t)((valc & svga.gdcreg[8]) ^ svga.lc);
                                if ((writemask2 & 8) != 0)
                                        svga.vram[addr | 0x3] = (uint8_t)((vald & svga.gdcreg[8]) ^ svga.ld);
                                break;
                        }
                }
                break;
        case 3:
                if ((svga.gdcreg[3] & 7) != 0)
                        val = svga_rotate[svga.gdcreg[3] & 7, val];
                wm = svga.gdcreg[8];
                svga.gdcreg[8] &= val;

                vala = (uint8_t)(((svga.gdcreg[0] & 1) != 0) ? 0xff : 0);
                valb = (uint8_t)(((svga.gdcreg[0] & 2) != 0) ? 0xff : 0);
                valc = (uint8_t)(((svga.gdcreg[0] & 4) != 0) ? 0xff : 0);
                vald = (uint8_t)(((svga.gdcreg[0] & 8) != 0) ? 0xff : 0);
                switch (svga.gdcreg[3] & 0x18)
                {
                case 0: /*Set*/
                        if ((writemask2 & 1) != 0)
                                svga.vram[addr] = (uint8_t)((vala & svga.gdcreg[8]) | (svga.la & ~svga.gdcreg[8]));
                        if ((writemask2 & 2) != 0)
                                svga.vram[addr | 0x1] = (uint8_t)((valb & svga.gdcreg[8]) | (svga.lb & ~svga.gdcreg[8]));
                        if ((writemask2 & 4) != 0)
                                svga.vram[addr | 0x2] = (uint8_t)((valc & svga.gdcreg[8]) | (svga.lc & ~svga.gdcreg[8]));
                        if ((writemask2 & 8) != 0)
                                svga.vram[addr | 0x3] = (uint8_t)((vald & svga.gdcreg[8]) | (svga.ld & ~svga.gdcreg[8]));
                        break;
                case 8: /*AND*/
                        if ((writemask2 & 1) != 0)
                                svga.vram[addr] = (uint8_t)((vala | ~svga.gdcreg[8]) & svga.la);
                        if ((writemask2 & 2) != 0)
                                svga.vram[addr | 0x1] = (uint8_t)((valb | ~svga.gdcreg[8]) & svga.lb);
                        if ((writemask2 & 4) != 0)
                                svga.vram[addr | 0x2] = (uint8_t)((valc | ~svga.gdcreg[8]) & svga.lc);
                        if ((writemask2 & 8) != 0)
                                svga.vram[addr | 0x3] = (uint8_t)((vald | ~svga.gdcreg[8]) & svga.ld);
                        break;
                case 0x10: /*OR*/
                        if ((writemask2 & 1) != 0)
                                svga.vram[addr] = (uint8_t)((vala & svga.gdcreg[8]) | svga.la);
                        if ((writemask2 & 2) != 0)
                                svga.vram[addr | 0x1] = (uint8_t)((valb & svga.gdcreg[8]) | svga.lb);
                        if ((writemask2 & 4) != 0)
                                svga.vram[addr | 0x2] = (uint8_t)((valc & svga.gdcreg[8]) | svga.lc);
                        if ((writemask2 & 8) != 0)
                                svga.vram[addr | 0x3] = (uint8_t)((vald & svga.gdcreg[8]) | svga.ld);
                        break;
                case 0x18: /*XOR*/
                        if ((writemask2 & 1) != 0)
                                svga.vram[addr] = (uint8_t)((vala & svga.gdcreg[8]) ^ svga.la);
                        if ((writemask2 & 2) != 0)
                                svga.vram[addr | 0x1] = (uint8_t)((valb & svga.gdcreg[8]) ^ svga.lb);
                        if ((writemask2 & 4) != 0)
                                svga.vram[addr | 0x2] = (uint8_t)((valc & svga.gdcreg[8]) ^ svga.lc);
                        if ((writemask2 & 8) != 0)
                                svga.vram[addr | 0x3] = (uint8_t)((vald & svga.gdcreg[8]) ^ svga.ld);
                        break;
                }
                svga.gdcreg[8] = wm;
                break;
        }
    }

    // pcem: vid_cl5429.c:1160-1240
    internal static uint8_t gd5429_read_linear(uint32_t addr, object? p)
    {
        gd5429_t gd5429 = (gd5429_t)p;
        svga_t svga = gd5429.svga;
        uint8_t temp, temp2, temp3, temp4;
        int readplane = svga.readplane;
        uint32_t latch_addr;

        cycles -= video_timing_read_b;
        cycles_lost += video_timing_read_b;

        egareads++;

        if ((svga.gdcreg[0xb] & GRB_ENHANCED_16BIT) != 0)
                latch_addr = (addr << 4) & svga.decode_mask;
        else if ((svga.gdcreg[0xb] & GRB_X8_ADDRESSING) != 0)
                latch_addr = (addr << 3) & svga.decode_mask;
        else
                latch_addr = (addr << 2) & svga.decode_mask;

        if ((svga.gdcreg[0xb] & GRB_ENHANCED_16BIT) != 0)
                addr <<= 4;
        else if ((svga.gdcreg[0xb] & GRB_X8_ADDRESSING) != 0)
                addr <<= 3;
        else if ((svga.chain4 != 0 && svga.packed_chain4 != 0) || svga.fb_only != 0)
        {
                // pcem bug, reproduced: PB-80 — sort sans charger les verrous ; ici la banque y passe aussi
                // (gd5429_read appelle cette fonction).
                addr &= svga.decode_mask;
                if (addr >= svga.vram_max)
                        return 0xff;
                return svga.vram[addr & svga.vram_mask];
        }
        else if (svga.chain4 != 0)
        {
                readplane = (int)(addr & 3);
                addr = ((addr & 0xfffc) << 2) | ((addr & 0x30000) >> 14) | (addr & ~0x3ffffu);
        }
        else if (svga.chain2_read != 0)
        {
                readplane = (readplane & 2) | (int)(addr & 1);
                addr &= ~1u;
                addr <<= 2;
        }
        else
                addr <<= 2;

        addr &= svga.decode_mask;

        if (latch_addr >= svga.vram_max)
        {
                svga.la = svga.lb = svga.lc = svga.ld = 0xff;
                if ((svga.gdcreg[0xb] & GRB_8B_LATCHES) != 0)
                        gd5429.latch_ext[0] = gd5429.latch_ext[1] = gd5429.latch_ext[2] = gd5429.latch_ext[3] = 0xff;
        }
        else
        {
                latch_addr &= svga.vram_mask;
                svga.la = svga.vram[latch_addr];
                svga.lb = svga.vram[latch_addr | 0x1];
                svga.lc = svga.vram[latch_addr | 0x2];
                svga.ld = svga.vram[latch_addr | 0x3];
                if ((svga.gdcreg[0xb] & GRB_8B_LATCHES) != 0)
                {
                        gd5429.latch_ext[0] = svga.vram[latch_addr | 0x4];
                        gd5429.latch_ext[1] = svga.vram[latch_addr | 0x5];
                        gd5429.latch_ext[2] = svga.vram[latch_addr | 0x6];
                        gd5429.latch_ext[3] = svga.vram[latch_addr | 0x7];
                }
        }

        if (addr >= svga.vram_max)
                return 0xff;

        addr &= svga.vram_mask;

        if (svga.readmode != 0)
        {
                temp = svga.la;
                temp ^= (uint8_t)(((svga.colourcompare & 1) != 0) ? 0xff : 0);
                temp &= (uint8_t)(((svga.colournocare & 1) != 0) ? 0xff : 0);
                temp2 = svga.lb;
                temp2 ^= (uint8_t)(((svga.colourcompare & 2) != 0) ? 0xff : 0);
                temp2 &= (uint8_t)(((svga.colournocare & 2) != 0) ? 0xff : 0);
                temp3 = svga.lc;
                temp3 ^= (uint8_t)(((svga.colourcompare & 4) != 0) ? 0xff : 0);
                temp3 &= (uint8_t)(((svga.colournocare & 4) != 0) ? 0xff : 0);
                temp4 = svga.ld;
                temp4 ^= (uint8_t)(((svga.colourcompare & 8) != 0) ? 0xff : 0);
                temp4 &= (uint8_t)(((svga.colournocare & 8) != 0) ? 0xff : 0);
                return (uint8_t)~(temp | temp2 | temp3 | temp4);
        }
        return svga.vram[addr | (uint32_t)readplane];
    }

    // pcem: vid_cl5429.c:1242-1250
    private static void gd5429_writeb_linear(uint32_t addr, uint8_t val, object? p)
    {
        gd5429_t gd5429 = (gd5429_t)p;
        svga_t svga = gd5429.svga;

        if ((svga.writemode < 4) && (svga.gdcreg[0xb] & (GRB_X8_ADDRESSING | GRB_8B_LATCHES)) == 0)
                svga_write_linear(addr, val, svga);
        else
                gd5429_write_linear(addr, (uint8_t)(val & 0xff), gd5429);
    }
    // pcem: vid_cl5429.c:1251-1261
    private static void gd5429_writew_linear(uint32_t addr, uint16_t val, object? p)
    {
        gd5429_t gd5429 = (gd5429_t)p;
        svga_t svga = gd5429.svga;

        if ((svga.writemode < 4) && (svga.gdcreg[0xb] & (GRB_X8_ADDRESSING | GRB_8B_LATCHES)) == 0)
                svga_writew_linear(addr, val, svga);
        else
        {
                gd5429_write_linear(addr, (uint8_t)(val & 0xff), gd5429);
                gd5429_write_linear(addr + 1, (uint8_t)(val >> 8), gd5429);
        }
    }
    // pcem: vid_cl5429.c:1262-1274
    private static void gd5429_writel_linear(uint32_t addr, uint32_t val, object? p)
    {
        gd5429_t gd5429 = (gd5429_t)p;
        svga_t svga = gd5429.svga;

        if ((svga.writemode < 4) && (svga.gdcreg[0xb] & (GRB_X8_ADDRESSING | GRB_8B_LATCHES)) == 0)
                svga_writel_linear(addr, val, svga);
        else
        {
                gd5429_write_linear(addr, (uint8_t)(val & 0xff), gd5429);
                gd5429_write_linear(addr + 1, (uint8_t)(val >> 8), gd5429);
                gd5429_write_linear(addr + 2, (uint8_t)(val >> 16), gd5429);
                gd5429_write_linear(addr + 3, (uint8_t)(val >> 24), gd5429);
        }
    }

    // pcem: vid_cl5429.c:1276-1283
    private static uint8_t gd5429_readb_linear(uint32_t addr, object? p)
    {
        gd5429_t gd5429 = (gd5429_t)p;
        svga_t svga = gd5429.svga;

        if ((svga.gdcreg[0xb] & (GRB_X8_ADDRESSING | GRB_8B_LATCHES)) == 0)
                return svga_read_linear(addr, gd5429.svga);
        return gd5429_read_linear(addr, gd5429);
    }
    // pcem: vid_cl5429.c:1284-1291
    private static uint16_t gd5429_readw_linear(uint32_t addr, object? p)
    {
        gd5429_t gd5429 = (gd5429_t)p;
        svga_t svga = gd5429.svga;

        if ((svga.gdcreg[0xb] & (GRB_X8_ADDRESSING | GRB_8B_LATCHES)) == 0)
                return svga_readw_linear(addr, gd5429.svga);
        return (uint16_t)(gd5429_read_linear(addr, gd5429) | (gd5429_read_linear(addr + 1, gd5429) << 8));
    }
    // pcem: vid_cl5429.c:1292-1300
    private static uint32_t gd5429_readl_linear(uint32_t addr, object? p)
    {
        gd5429_t gd5429 = (gd5429_t)p;
        svga_t svga = gd5429.svga;

        if ((svga.gdcreg[0xb] & (GRB_X8_ADDRESSING | GRB_8B_LATCHES)) == 0)
                return svga_readl_linear(addr, gd5429.svga);
        return (uint32_t)gd5429_read_linear(addr, gd5429) | ((uint32_t)gd5429_read_linear(addr + 1, gd5429) << 8) |
               ((uint32_t)gd5429_read_linear(addr + 2, gd5429) << 16) | ((uint32_t)gd5429_read_linear(addr + 3, gd5429) << 24);
    }

    // pcem: vid_cl5429.c:1302-1597
    internal static void gd5429_start_blit(uint32_t cpu_dat, int count, object p)
    {
        gd5429_t gd5429 = (gd5429_t)p;
        svga_t svga = gd5429.svga;
        int blt_mask = gd5429.blt.mask & 7;
        int x_max = 0;

        switch (gd5429.blt.depth)
        {
        case BLIT_DEPTH_8:
                x_max = 8;
                break;
        case BLIT_DEPTH_16:
                x_max = 16;
                blt_mask *= 2;
                break;
        case BLIT_DEPTH_32:
                x_max = 32;
                blt_mask *= 4;
                break;
        }

        if (count == -1)
        {
                gd5429.blt.dst_addr_backup = gd5429.blt.dst_addr;
                gd5429.blt.src_addr_backup = gd5429.blt.src_addr;
                gd5429.blt.width_backup = gd5429.blt.width;
                gd5429.blt.height_internal = gd5429.blt.height;
                gd5429.blt.x_count = 0;
                if ((gd5429.blt.mode & 0xc0) == 0xc0)
                        gd5429.blt.y_count = (int)(gd5429.blt.src_addr & 7);
                else
                        gd5429.blt.y_count = 0;

                if ((gd5429.blt.mode & 0x04) != 0)
                {
                        if ((svga.seqregs[7] & 0xf0) == 0)
                        {
                                mem_mapping_set_handler(svga.mapping, null, null, null, null, gd5429_blt_write_w,
                                                        gd5429_blt_write_l);
                                mem_mapping_set_p(svga.mapping, gd5429);
                        }
                        else
                        {
                                mem_mapping_set_handler(gd5429.linear_mapping, null, null, null, null, gd5429_blt_write_w,
                                                        gd5429_blt_write_l);
                                mem_mapping_set_p(gd5429.linear_mapping, gd5429);
                        }
                        gd5429_recalc_mapping(gd5429);
                        return;
                }
                else
                {
                        if ((svga.seqregs[7] & 0xf0) == 0)
                                mem_mapping_set_handler(svga.mapping, gd5429_read, gd5429_readw, gd5429_readl, gd5429_write,
                                                        gd5429_writew, gd5429_writel);
                        else
                                mem_mapping_set_handler(gd5429.linear_mapping, gd5429_readb_linear, gd5429_readw_linear,
                                                        gd5429_readl_linear, gd5429_writeb_linear, gd5429_writew_linear,
                                                        gd5429_writel_linear);
                        gd5429_recalc_mapping(gd5429);
                }
        }
        else if (gd5429.blt.height_internal == 0xffff)
                return;

        while (count != 0)
        {
                uint8_t src = 0, dst;
                int mask = 0;
                int shift;

                if (gd5429.blt.depth == BLIT_DEPTH_32)
                        shift = (gd5429.blt.x_count & 3) * 8;
                else if (gd5429.blt.depth == BLIT_DEPTH_8)
                        shift = 0;
                else
                        shift = (gd5429.blt.x_count & 1) * 8;

                if ((gd5429.blt.mode & 0x04) != 0)
                {
                        if ((gd5429.blt.mode & 0x80) != 0)
                        {
                                mask = (int)(cpu_dat & 0x80);

                                switch (gd5429.blt.depth)
                                {
                                case BLIT_DEPTH_8:
                                        src = (uint8_t)((mask != 0) ? gd5429.blt.fg_col : gd5429.blt.bg_col);
                                        cpu_dat <<= 1;
                                        count--;
                                        break;
                                case BLIT_DEPTH_16:
                                        src = (uint8_t)((mask != 0) ? (gd5429.blt.fg_col >> shift) : (gd5429.blt.bg_col >> shift));
                                        if ((gd5429.blt.x_count & 1) != 0)
                                        {
                                                cpu_dat <<= 1;
                                                count--;
                                        }
                                        break;
                                case BLIT_DEPTH_32:
                                        src = (uint8_t)((mask != 0) ? (gd5429.blt.fg_col >> shift) : (gd5429.blt.bg_col >> shift));
                                        if ((gd5429.blt.x_count & 3) == 3)
                                        {
                                                cpu_dat <<= 1;
                                                count--;
                                        }
                                        break;
                                }
                        }
                        else
                        {
                                src = (uint8_t)(cpu_dat & 0xff);
                                cpu_dat >>= 8;
                                count -= 8;
                                mask = 1;
                        }
                }
                else
                {
                        switch (gd5429.blt.mode & 0xc0)
                        {
                        case 0x00:
                                src = svga.vram[gd5429.blt.src_addr & svga.vram_mask];
                                gd5429.blt.src_addr += (uint32_t)(((gd5429.blt.mode & 0x01) != 0) ? -1 : 1);
                                mask = 1;
                                break;
                        case 0x40:
                                switch (gd5429.blt.depth)
                                {
                                // pcem bug, not reproduced: PB-81 — DEVIATION : PCem ajoute jusqu'à 63 ou 127
                                // octets APRÈS avoir masqué la source ; en haut de la VRAM l'index sort du
                                // tableau (lecture du tas en C, exception ici). Masqué une seconde fois (R9).
                                case BLIT_DEPTH_8:
                                        src = svga.vram[((gd5429.blt.src_addr & (svga.vram_mask & ~7u)) +
                                                        (uint32_t)(gd5429.blt.y_count << 3) + (uint32_t)(gd5429.blt.x_count & 7)) & svga.vram_mask];
                                        break;
                                case BLIT_DEPTH_16:
                                        src = svga.vram[((gd5429.blt.src_addr & (svga.vram_mask & ~3u)) +
                                                        (uint32_t)(gd5429.blt.y_count << 4) + (uint32_t)(gd5429.blt.x_count & 15)) & svga.vram_mask];
                                        break;
                                case BLIT_DEPTH_32:
                                        src = svga.vram[((gd5429.blt.src_addr & (svga.vram_mask & ~3u)) +
                                                        (uint32_t)(gd5429.blt.y_count << 5) + (uint32_t)(gd5429.blt.x_count & 31)) & svga.vram_mask];
                                        break;
                                }
                                mask = 1;
                                break;
                        case 0x80:
                                switch (gd5429.blt.depth)
                                {
                                case BLIT_DEPTH_8:
                                        mask = svga.vram[gd5429.blt.src_addr & svga.vram_mask] & (0x80 >> gd5429.blt.x_count);
                                        src = (uint8_t)((mask != 0) ? gd5429.blt.fg_col : gd5429.blt.bg_col);
                                        break;
                                case BLIT_DEPTH_16:
                                        mask = svga.vram[gd5429.blt.src_addr & svga.vram_mask] &
                                               (0x80 >> (gd5429.blt.x_count >> 1));
                                        src = (uint8_t)((mask != 0) ? (gd5429.blt.fg_col >> shift) : (gd5429.blt.bg_col >> shift));
                                        break;
                                case BLIT_DEPTH_32:
                                        mask = svga.vram[gd5429.blt.src_addr & svga.vram_mask] &
                                               (0x80 >> (gd5429.blt.x_count >> 2));
                                        src = (uint8_t)((mask != 0) ? (gd5429.blt.fg_col >> shift) : (gd5429.blt.bg_col >> shift));
                                        break;
                                }
                                break;
                        case 0xc0:
                                switch (gd5429.blt.depth)
                                {
                                case BLIT_DEPTH_8:
                                        mask = svga.vram[(gd5429.blt.src_addr & svga.vram_mask & ~7u) | (uint32_t)gd5429.blt.y_count] &
                                               (0x80 >> gd5429.blt.x_count);
                                        src = (uint8_t)((mask != 0) ? gd5429.blt.fg_col : gd5429.blt.bg_col);
                                        break;
                                case BLIT_DEPTH_16:
                                        mask = svga.vram[(gd5429.blt.src_addr & svga.vram_mask & ~7u) | (uint32_t)gd5429.blt.y_count] &
                                               (0x80 >> (gd5429.blt.x_count >> 1));
                                        src = (uint8_t)((mask != 0) ? (gd5429.blt.fg_col >> shift) : (gd5429.blt.bg_col >> shift));
                                        break;
                                case BLIT_DEPTH_32:
                                        mask = svga.vram[(gd5429.blt.src_addr & svga.vram_mask & ~7u) | (uint32_t)gd5429.blt.y_count] &
                                               (0x80 >> (gd5429.blt.x_count >> 2));
                                        src = (uint8_t)((mask != 0) ? (gd5429.blt.fg_col >> shift) : (gd5429.blt.bg_col >> shift));
                                        break;
                                }
                                break;
                        }
                        count--;
                }
                dst = svga.vram[gd5429.blt.dst_addr & svga.vram_mask];
                svga.changedvram[(gd5429.blt.dst_addr & svga.vram_mask) >> 12] = (uint8_t)changeframecount;

                switch (gd5429.blt.rop)
                {
                case 0x00:
                        dst = 0;
                        break;
                case 0x05:
                        dst = (uint8_t)(src & dst);
                        break;
                case 0x06:
                        dst = dst;
                        break;
                case 0x09:
                        dst = (uint8_t)(src & ~dst);
                        break;
                case 0x0b:
                        dst = (uint8_t)~dst;
                        break;
                case 0x0d:
                        dst = src;
                        break;
                case 0x0e:
                        dst = 0xff;
                        break;
                case 0x50:
                        dst = (uint8_t)(~src & dst);
                        break;
                case 0x59:
                        dst = (uint8_t)(src ^ dst);
                        break;
                case 0x6d:
                        dst = (uint8_t)(src | dst);
                        break;
                case 0x90:
                        dst = (uint8_t)~(src | dst);
                        break;
                case 0x95:
                        dst = (uint8_t)~(src ^ dst);
                        break;
                case 0xad:
                        dst = (uint8_t)(src | ~dst);
                        break;
                case 0xd0:
                        dst = (uint8_t)~src;
                        break;
                case 0xd6:
                        dst = (uint8_t)(~src | dst);
                        break;
                case 0xda:
                        dst = (uint8_t)~(src & dst);
                        break;
                }

                if (gd5429.type <= CL_TYPE_GD5428)
                {
                        if ((gd5429.blt.width_backup - gd5429.blt.width) >= blt_mask &&
                            ((gd5429.blt.mode & 0x08) == 0 || (dst & gd5429.blt.trans_mask) != gd5429.blt.trans_col))
                                svga.vram[gd5429.blt.dst_addr & svga.vram_mask] = dst;
                }
                else
                {
                        if ((gd5429.blt.width_backup - gd5429.blt.width) >= blt_mask && !((gd5429.blt.mode & 0x08) != 0 && mask == 0))
                                svga.vram[gd5429.blt.dst_addr & svga.vram_mask] = dst;
                }

                gd5429.blt.dst_addr += (uint32_t)(((gd5429.blt.mode & 0x01) != 0) ? -1 : 1);

                gd5429.blt.x_count++;
                if (gd5429.blt.x_count == x_max)
                {
                        gd5429.blt.x_count = 0;
                        if ((gd5429.blt.mode & 0xc0) == 0x80)
                                gd5429.blt.src_addr++;
                }

                gd5429.blt.width--;

                if (gd5429.blt.width == 0xffff)
                {
                        gd5429.blt.width = gd5429.blt.width_backup;

                        gd5429.blt.dst_addr = gd5429.blt.dst_addr_backup =
                                (uint32_t)(gd5429.blt.dst_addr_backup +
                                ((gd5429.blt.mode & 0x01) != 0 ? -gd5429.blt.dst_pitch : gd5429.blt.dst_pitch));

                        switch (gd5429.blt.mode & 0xc0)
                        {
                        case 0x00:
                                gd5429.blt.src_addr = gd5429.blt.src_addr_backup =
                                        (uint32_t)(gd5429.blt.src_addr_backup +
                                        ((gd5429.blt.mode & 0x01) != 0 ? -gd5429.blt.src_pitch : gd5429.blt.src_pitch));
                                break;
                        case 0x80:
                                if (gd5429.blt.x_count != 0)
                                        gd5429.blt.src_addr++;
                                break;
                        }

                        gd5429.blt.x_count = 0;
                        if ((gd5429.blt.mode & 0x01) != 0)
                                gd5429.blt.y_count = (gd5429.blt.y_count - 1) & 7;
                        else
                                gd5429.blt.y_count = (gd5429.blt.y_count + 1) & 7;

                        gd5429.blt.height_internal--;
                        if (gd5429.blt.height_internal == 0xffff)
                        {
                                if ((gd5429.blt.mode & 0x04) != 0)
                                {
                                        if ((svga.seqregs[7] & 0xf0) == 0)
                                                mem_mapping_set_handler(svga.mapping, gd5429_read, gd5429_readw, gd5429_readl,
                                                                        gd5429_write, gd5429_writew, gd5429_writel);
                                        else
                                                mem_mapping_set_handler(gd5429.linear_mapping, gd5429_readb_linear,
                                                                        gd5429_readw_linear, gd5429_readl_linear,
                                                                        gd5429_writeb_linear, gd5429_writew_linear,
                                                                        gd5429_writel_linear);
                                        gd5429_recalc_mapping(gd5429);
                                }
                                return;
                        }

                        if ((gd5429.blt.mode & 0x04) != 0)
                                return;
                }
        }
    }

    // pcem: vid_cl5429.c:1599-1727
    private static void gd5429_mmio_write(uint32_t addr, uint8_t val, object? p)
    {
        gd5429_t gd5429 = (gd5429_t)p;

        if ((addr & ~0xffu) == 0xb8000)
        {
                switch (addr & 0xff)
                {
                case 0x00:
                        if (gd5429.type >= CL_TYPE_GD5434)
                                gd5429.blt.bg_col = (gd5429.blt.bg_col & 0xffffff00) | val;
                        else
                                gd5429.blt.bg_col = (gd5429.blt.bg_col & 0xff00) | val;
                        break;
                case 0x01:
                        if (gd5429.type >= CL_TYPE_GD5434)
                                gd5429.blt.bg_col = (gd5429.blt.bg_col & 0xffff00ff) | ((uint32_t)val << 8);
                        else
                                gd5429.blt.bg_col = (gd5429.blt.bg_col & 0x00ff) | ((uint32_t)val << 8);
                        break;
                case 0x02:
                        if (gd5429.type >= CL_TYPE_GD5434)
                                gd5429.blt.bg_col = (gd5429.blt.bg_col & 0xff00ffff) | ((uint32_t)val << 16);
                        break;
                case 0x03:
                        if (gd5429.type >= CL_TYPE_GD5434)
                                gd5429.blt.bg_col = (gd5429.blt.bg_col & 0x00ffffff) | ((uint32_t)val << 24);
                        break;

                case 0x04:
                        if (gd5429.type >= CL_TYPE_GD5434)
                                gd5429.blt.fg_col = (gd5429.blt.fg_col & 0xffffff00) | val;
                        else
                                gd5429.blt.fg_col = (gd5429.blt.fg_col & 0xff00) | val;
                        break;
                case 0x05:
                        if (gd5429.type >= CL_TYPE_GD5434)
                                gd5429.blt.fg_col = (gd5429.blt.fg_col & 0xffff00ff) | ((uint32_t)val << 8);
                        else
                                gd5429.blt.fg_col = (gd5429.blt.fg_col & 0x00ff) | ((uint32_t)val << 8);
                        break;
                case 0x06:
                        if (gd5429.type >= CL_TYPE_GD5434)
                                gd5429.blt.fg_col = (gd5429.blt.fg_col & 0xff00ffff) | ((uint32_t)val << 16);
                        break;
                case 0x07:
                        if (gd5429.type >= CL_TYPE_GD5434)
                                gd5429.blt.fg_col = (gd5429.blt.fg_col & 0x00ffffff) | ((uint32_t)val << 24);
                        break;

                case 0x08:
                        gd5429.blt.width = (uint16_t)((gd5429.blt.width & 0xff00) | val);
                        break;
                case 0x09:
                        gd5429.blt.width = (uint16_t)((gd5429.blt.width & 0x00ff) | (val << 8));
                        if (gd5429.type >= CL_TYPE_GD5434)
                                gd5429.blt.width &= 0x1fff;
                        else
                                gd5429.blt.width &= 0x07ff;
                        break;
                case 0x0a:
                        gd5429.blt.height = (uint16_t)((gd5429.blt.height & 0xff00) | val);
                        break;
                case 0x0b:
                        gd5429.blt.height = (uint16_t)((gd5429.blt.height & 0x00ff) | (val << 8));
                        gd5429.blt.height &= 0x03ff;
                        break;
                case 0x0c:
                        gd5429.blt.dst_pitch = (uint16_t)((gd5429.blt.dst_pitch & 0xff00) | val);
                        break;
                case 0x0d:
                        gd5429.blt.dst_pitch = (uint16_t)((gd5429.blt.dst_pitch & 0x00ff) | (val << 8));
                        break;
                case 0x0e:
                        gd5429.blt.src_pitch = (uint16_t)((gd5429.blt.src_pitch & 0xff00) | val);
                        break;
                case 0x0f:
                        gd5429.blt.src_pitch = (uint16_t)((gd5429.blt.src_pitch & 0x00ff) | (val << 8));
                        break;

                case 0x10:
                        gd5429.blt.dst_addr = (gd5429.blt.dst_addr & 0xffff00) | val;
                        break;
                case 0x11:
                        gd5429.blt.dst_addr = (gd5429.blt.dst_addr & 0xff00ff) | ((uint32_t)val << 8);
                        break;
                case 0x12:
                        gd5429.blt.dst_addr = (gd5429.blt.dst_addr & 0x00ffff) | ((uint32_t)val << 16);
                        if (gd5429.type >= CL_TYPE_GD5434)
                                gd5429.blt.dst_addr &= 0x3fffff;
                        else
                                gd5429.blt.dst_addr &= 0x1fffff;
                        break;

                case 0x14:
                        gd5429.blt.src_addr = (gd5429.blt.src_addr & 0xffff00) | val;
                        break;
                case 0x15:
                        gd5429.blt.src_addr = (gd5429.blt.src_addr & 0xff00ff) | ((uint32_t)val << 8);
                        break;
                case 0x16:
                        gd5429.blt.src_addr = (gd5429.blt.src_addr & 0x00ffff) | ((uint32_t)val << 16);
                        if (gd5429.type >= CL_TYPE_GD5434)
                                gd5429.blt.src_addr &= 0x3fffff;
                        else
                                gd5429.blt.src_addr &= 0x1fffff;
                        break;

                case 0x17:
                        gd5429.blt.mask = val;
                        break;
                case 0x18:
                        gd5429.blt.mode = val;
                        if (gd5429.type >= CL_TYPE_GD5434)
                                gd5429.blt.depth = (val >> 4) & 3;
                        else
                                gd5429.blt.depth = (val >> 4) & 1;
                        break;

                case 0x1a:
                        gd5429.blt.rop = val;
                        break;

                case 0x40:
                        if ((val & 0x02) != 0)
                                gd5429_start_blit(0, -1, gd5429);
                        break;
                }
        }
        else if (gd5429.mmio_vram_overlap != 0)
                gd5429_write(addr, val, gd5429);
    }
    // pcem: vid_cl5429.c:1728-1736
    private static void gd5429_mmio_writew(uint32_t addr, uint16_t val, object? p)
    {
        gd5429_t gd5429 = (gd5429_t)p;

        if ((addr & ~0xffu) == 0xb8000)
        {
                gd5429_mmio_write(addr, (uint8_t)(val & 0xff), gd5429);
                gd5429_mmio_write(addr + 1, (uint8_t)(val >> 8), gd5429);
        }
        else if (gd5429.mmio_vram_overlap != 0)
                gd5429_writew(addr, val, gd5429);
    }
    // pcem: vid_cl5429.c:1737-1745
    private static void gd5429_mmio_writel(uint32_t addr, uint32_t val, object? p)
    {
        gd5429_t gd5429 = (gd5429_t)p;

        if ((addr & ~0xffu) == 0xb8000)
        {
                gd5429_mmio_writew(addr, (uint16_t)(val & 0xffff), gd5429);
                gd5429_mmio_writew(addr + 2, (uint16_t)(val >> 16), gd5429);
        }
        else if (gd5429.mmio_vram_overlap != 0)
                gd5429_writel(addr, val, gd5429);
    }

    // pcem: vid_cl5429.c:1747-1761
    private static uint8_t gd5429_mmio_read(uint32_t addr, object? p)
    {
        gd5429_t gd5429 = (gd5429_t)p;

        if ((addr & ~0xffu) == 0xb8000)
        {
                switch (addr & 0xff)
                {
                case 0x40: /*BLT status*/
                        return 0;
                }
                return 0xff; /*All other registers read-only*/
        }
        if (gd5429.mmio_vram_overlap != 0)
                return gd5429_read(addr, gd5429);
        return 0xff;
    }
    // pcem: vid_cl5429.c:1762-1770
    private static uint16_t gd5429_mmio_readw(uint32_t addr, object? p)
    {
        gd5429_t gd5429 = (gd5429_t)p;

        if ((addr & ~0xffu) == 0xb8000)
                return (uint16_t)(gd5429_mmio_read(addr, gd5429) | (gd5429_mmio_read(addr + 1, gd5429) << 8));
        if (gd5429.mmio_vram_overlap != 0)
                return gd5429_readw(addr, gd5429);
        return 0xffff;
    }
    // pcem: vid_cl5429.c:1771-1779
    private static uint32_t gd5429_mmio_readl(uint32_t addr, object? p)
    {
        gd5429_t gd5429 = (gd5429_t)p;

        if ((addr & ~0xffu) == 0xb8000)
                return (uint32_t)gd5429_mmio_readw(addr, gd5429) | ((uint32_t)gd5429_mmio_readw(addr + 2, gd5429) << 16);
        if (gd5429.mmio_vram_overlap != 0)
                return gd5429_readl(addr, gd5429);
        return 0xffffffff;
    }

    // pcem: vid_cl5429.c:1781-1790
    internal static void gd5429_blt_write_w(uint32_t addr, uint16_t val, object? p)
    {
        gd5429_t gd5429 = (gd5429_t)p;

        if (gd5429.blt.mem_word_sel == 0)
                gd5429.blt.mem_word_save = val;
        else
                gd5429_start_blit((uint32_t)(gd5429.blt.mem_word_save | (val << 16)), 32, p);
        gd5429.blt.mem_word_sel = (gd5429.blt.mem_word_sel == 0) ? 1 : 0;
    }

    // pcem: vid_cl5429.c:1792-1805
    internal static void gd5429_blt_write_l(uint32_t addr, uint32_t val, object? p)
    {
        gd5429_t gd5429 = (gd5429_t)p;

        gd5429.blt.mem_word_sel = 0;
        if ((gd5429.blt.mode & 0x84) == 0x84)
        {
                gd5429_start_blit(val & 0xff, 8, p);
                gd5429_start_blit((val >> 8) & 0xff, 8, p);
                gd5429_start_blit((val >> 16) & 0xff, 8, p);
                gd5429_start_blit((val >> 24) & 0xff, 8, p);
        }
        else
                gd5429_start_blit(val, 32, p);
    }

    // omitted: ibm_gd5428_mca_read, ibm_gd5428_mapping_update, ibm_gd5428_mca_write,
    //   ibm_gd5428_mca_reset (vid_cl5429.c:1807-1868) — la carte MCA d'IBM ; MCA vaut 0 sur nos
    //   machines et le bus MCA n'est pas transcrit.
    // omitted: cl_pci_read, cl_pci_write (vid_cl5429.c:1870-1966) — l'espace de configuration
    //   PCI des GD5430/5434 ; PCI vaut 0 sur nos machines et pci.c n'est pas transcrit.

    // pcem: vid_cl5429.c:1968-2037
    private static object cl_init(int type, string? fn, int pci_card, uint32_t force_vram_size)
    {
        gd5429_t gd5429 = new gd5429_t();
        svga_t svga = gd5429.svga;
        int vram_size;
        // omitted: memset(gd5429, 0, sizeof(gd5429_t)) (vid_cl5429.c:1972) — `new` zéro-initialise.

        if (force_vram_size != 0)
                vram_size = (int)force_vram_size;
        else
                vram_size = device_get_config_int("memory");
        if (vram_size >= 256)
                gd5429.vram_mask = (uint32_t)((vram_size << 10) - 1);
        else
                gd5429.vram_mask = (uint32_t)((vram_size << 20) - 1);

        gd5429.type = type;

        if (fn != null)
                rom_init(gd5429.bios_rom, fn, 0xc0000, 0x8000, 0x7fff, 0, MEM_MAPPING_EXTERNAL);

        svga_init(gd5429.svga, gd5429, (vram_size >= 256) ? (vram_size << 10) : (vram_size << 20), gd5429_recalctimings,
                  gd5429_in, gd5429_out, gd5429_hwcursor_draw, null);

        mem_mapping_set_handler(gd5429.svga.mapping, gd5429_read, gd5429_readw, gd5429_readl, gd5429_write, gd5429_writew,
                                gd5429_writel);
        mem_mapping_set_p(gd5429.svga.mapping, gd5429);

        mem_mapping_add(gd5429.mmio_mapping, 0, 0, gd5429_mmio_read, gd5429_mmio_readw, gd5429_mmio_readl, gd5429_mmio_write,
                        gd5429_mmio_writew, gd5429_mmio_writel, null, 0, 0, gd5429);
        mem_mapping_add(gd5429.linear_mapping, 0, 0, gd5429_readb_linear, gd5429_readw_linear, gd5429_readl_linear,
                        gd5429_writeb_linear, gd5429_writew_linear, gd5429_writel_linear, null, 0, 0, gd5429);

        io_sethandler(0x03c0, 0x0020, gd5429_in, null, null, gd5429_out, null, null, gd5429);
        if (type == CL_TYPE_AVGA2)
        {
                io_sethandler(0x0102, 0x0001, gd5429_in, null, null, gd5429_out, null, null, gd5429);
                io_sethandler(0x46e8, 0x0002, gd5429_in, null, null, gd5429_out, null, null, gd5429);
                svga.decode_mask = svga.vram_mask;
        }

        svga.hwcursor.yoff = 32;
        svga.hwcursor.xoff = 0;

        gd5429.bank[1] = 0x8000;

        /*Default VCLK values*/
        svga.seqregs[0xb] = 0x66;
        svga.seqregs[0xc] = 0x5b;
        svga.seqregs[0xd] = 0x45;
        svga.seqregs[0xe] = 0x7e;
        svga.seqregs[0x1b] = 0x3b;
        svga.seqregs[0x1c] = 0x2f;
        svga.seqregs[0x1d] = 0x30;
        svga.seqregs[0x1e] = 0x33;

        // omitted: `if (PCI && type >= CL_TYPE_GD5430) { pci_add_specific / pci_add, pci_regs }`
        //   (vid_cl5429.c:2022-2034) — PCI vaut 0 sur nos machines, le GD5429 n'est pas >= GD5430,
        //   et pci.c n'est pas transcrit.

        return gd5429;
    }

    // omitted: avga2_init, avga2_cbm_sl386sx_init, gd5426_ps1_init, gd5428_init, ibm_gd5428_init
    //   (vid_cl5429.c:2039-2062) — autres cartes de la famille, absentes de nos machines.
    // pcem: vid_cl5429.c:2063
    private static object gd5429_init() { return cl_init(CL_TYPE_GD5429, "5429.vbi", -1, 0); }
    // omitted: gd5430_init, gd5430_pb570_init, gd5434_init, gd5434_pb520r_init
    //   (vid_cl5429.c:2064-2067) — autres cartes de la famille, absentes de nos machines.

    // omitted: avga2_available, gd5428_available, ibm_gd5428_available (vid_cl5429.c:2069-2071)
    //   — même raison.
    // pcem: vid_cl5429.c:2072
    private static int gd5429_available() { return rom_present("5429.vbi"); }
    // omitted: gd5430_available, gd5434_available (vid_cl5429.c:2073-2074) — même raison.

    // pcem: vid_cl5429.c:2076-2082
    internal static void gd5429_close(object p)
    {
        gd5429_t gd5429 = (gd5429_t)p;

        svga_close(gd5429.svga);

        // omitted: free(gd5429) (vid_cl5429.c:2081) — libération manuelle, sans objet sous GC.
    }

    // pcem: vid_cl5429.c:2084-2088
    internal static void gd5429_speed_changed(object p)
    {
        gd5429_t gd5429 = (gd5429_t)p;

        svga_recalctimings(gd5429.svga);
    }

    // pcem: vid_cl5429.c:2090-2094
    internal static void gd5429_force_redraw(object p)
    {
        gd5429_t gd5429 = (gd5429_t)p;

        gd5429.svga.fullchange = changeframecount;
    }

    // pcem: vid_cl5429.c:2096-2100
    internal static void gd5429_add_status_info(StringBuilder s, int max_len, object p)
    {
        gd5429_t gd5429 = (gd5429_t)p;

        svga_add_status_info(s, max_len, gd5429.svga);
    }

    // omitted: avga2_config (vid_cl5429.c:2102-2108) — configuration de l'AVGA2, absente.
    // pcem: vid_cl5429.c:2110-2116
    // omitted: `.description` (vid_cl5429.c:2112) ; `.selection` (:2114) est transcrit (G8.3) :
    //   sans section [Cirrus Logic GD5429] dans le .cfg, le défaut, 2 Mo.
    internal static device_config_t[] gd5429_config =
    [
        new device_config_t { name = "memory", type = CONFIG_SELECTION, default_int = 2,
            selection = [new() { description = "1 MB", value = 1 }, new() { description = "2 MB", value = 2 }] },
        new device_config_t { type = -1 },
    ];
    // omitted: gd5434_config (vid_cl5429.c:2117-2123) — configuration du GD5434, absent.

    // omitted: avga2_device, avga2_cbm_sl386sx_device, gd5426_ps1_device, gd5428_device,
    //   ibm_gd5428_device (vid_cl5429.c:2125-2163) — autres cartes de la famille.
    // pcem: vid_cl5429.c:2165-2169
    internal static device_t gd5429_device = new device_t("Cirrus Logic GD5429", 0, gd5429_init, gd5429_close, gd5429_available,
                                                          gd5429_speed_changed, gd5429_force_redraw, gd5429_add_status_info,
                                                          gd5429_config);
    // omitted: gd5430_device, gd5430_pb570_device, gd5434_device, gd5434_pb520r_device
    //   (vid_cl5429.c:2171-2201) — autres cartes de la famille.
}
