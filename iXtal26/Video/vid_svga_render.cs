// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/video/vid_svga_render.c + includes/private/video/vid_svga_render_remap.h
// STATUS: partial — le remappage d'adresse du CRTC, et les rendus que svga_recalctimings
//         choisit pour une VGA : vide, texte 40 et 80, 2, 4 et 8 bpp. Les rendus 15 à
//         32 bpp sont des fatal() : seul un bpp autre que 8 les atteint, et la VGA le fige.

using System.Runtime.CompilerServices;
using static iXtal26.Video.video;

namespace iXtal26.Video;

internal static partial class vid_svga_render
{
    /*Variables :
            byte/word/doubleword mode
            word has MA13/MA15->MA0
            ET4000 treats doubleword as byte
            row 0 -> MA13
            row 1 -> MA14
    */

    // S3 - enhanced mode mappings CR31.3 can force doubleword mode
    // Cirrus Logic handles SVGA writes seperately
    // S3, CL, TGUI blitters need checking

    // CL, S3, Mach64, ET4000, Banshee, TGUI all okay
    // Still to check - ViRGE, HT216

    // pcem: vid_svga_render_remap.h:18-24
    private const int VAR_BYTE_MODE = (0 << 0);
    private const int VAR_WORD_MODE_MA13 = (1 << 0);
    private const int VAR_WORD_MODE_MA15 = (2 << 0);
    private const int VAR_DWORD_MODE = (3 << 0);
    private const int VAR_MODE_MASK = (3 << 0);
    private const int VAR_ROW0_MA13 = (1 << 2);
    private const int VAR_ROW1_MA14 = (1 << 3);

    // pcem: vid_svga_render_remap.h:26-54
    //
    // DEVIATION: la macro ADDRESS_REMAP_FUNC(nr) engendre SEIZE fonctions dont `nr` est
    //   une constante de compilation. Ici une seule fonction à `nr` en paramètre, que
    //   les seize instanciations ci-dessous appellent avec leur constante : un
    //   AggressiveInlining par site la replie comme le préprocesseur le faisait.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint32_t ADDRESS_REMAP_FUNC(int nr, svga_t svga, uint32_t in_addr)
    {
        uint32_t out_addr;

        // CS0165: le switch porte sur `nr & 3` et ses quatre cas couvrent les quatre
        //   valeurs, donc out_addr est toujours écrit ; le compilateur ne le sait pas.
        out_addr = 0;
        switch (nr & VAR_MODE_MASK)
        {
        case VAR_BYTE_MODE:
                out_addr = in_addr;
                break;

        case VAR_WORD_MODE_MA13:
                out_addr = ((in_addr << 1) & 0x1fff8) | ((in_addr >> 13) & 0x4) | (in_addr & ~0x1ffffu);
                break;

        case VAR_WORD_MODE_MA15:
                out_addr = ((in_addr << 1) & 0x1fff8) | ((in_addr >> 15) & 0x4) | (in_addr & ~0x1ffffu);
                break;

        case VAR_DWORD_MODE:
                out_addr = ((in_addr << 2) & 0x3fff0) | ((in_addr >> 14) & 0xc) | (in_addr & ~0x3ffffu);
                break;
        }

        if ((nr & VAR_ROW0_MA13) != 0)
                out_addr = (out_addr & ~(1u << (13 + 2))) | (((svga.sc & 1) != 0) ? (1u << (13 + 2)) : 0);
        if ((nr & VAR_ROW1_MA14) != 0)
                out_addr = (out_addr & ~(1u << (14 + 2))) | (((svga.sc & 2) != 0) ? (1u << (14 + 2)) : 0);

        return out_addr;
    }

    // pcem: vid_svga_render_remap.h:56-71
    private static uint32_t address_remap_func_0(svga_t svga, uint32_t in_addr) => ADDRESS_REMAP_FUNC(0, svga, in_addr);
    private static uint32_t address_remap_func_1(svga_t svga, uint32_t in_addr) => ADDRESS_REMAP_FUNC(1, svga, in_addr);
    private static uint32_t address_remap_func_2(svga_t svga, uint32_t in_addr) => ADDRESS_REMAP_FUNC(2, svga, in_addr);
    private static uint32_t address_remap_func_3(svga_t svga, uint32_t in_addr) => ADDRESS_REMAP_FUNC(3, svga, in_addr);
    private static uint32_t address_remap_func_4(svga_t svga, uint32_t in_addr) => ADDRESS_REMAP_FUNC(4, svga, in_addr);
    private static uint32_t address_remap_func_5(svga_t svga, uint32_t in_addr) => ADDRESS_REMAP_FUNC(5, svga, in_addr);
    private static uint32_t address_remap_func_6(svga_t svga, uint32_t in_addr) => ADDRESS_REMAP_FUNC(6, svga, in_addr);
    private static uint32_t address_remap_func_7(svga_t svga, uint32_t in_addr) => ADDRESS_REMAP_FUNC(7, svga, in_addr);
    private static uint32_t address_remap_func_8(svga_t svga, uint32_t in_addr) => ADDRESS_REMAP_FUNC(8, svga, in_addr);
    private static uint32_t address_remap_func_9(svga_t svga, uint32_t in_addr) => ADDRESS_REMAP_FUNC(9, svga, in_addr);
    private static uint32_t address_remap_func_10(svga_t svga, uint32_t in_addr) => ADDRESS_REMAP_FUNC(10, svga, in_addr);
    private static uint32_t address_remap_func_11(svga_t svga, uint32_t in_addr) => ADDRESS_REMAP_FUNC(11, svga, in_addr);
    private static uint32_t address_remap_func_12(svga_t svga, uint32_t in_addr) => ADDRESS_REMAP_FUNC(12, svga, in_addr);
    private static uint32_t address_remap_func_13(svga_t svga, uint32_t in_addr) => ADDRESS_REMAP_FUNC(13, svga, in_addr);
    private static uint32_t address_remap_func_14(svga_t svga, uint32_t in_addr) => ADDRESS_REMAP_FUNC(14, svga, in_addr);
    private static uint32_t address_remap_func_15(svga_t svga, uint32_t in_addr) => ADDRESS_REMAP_FUNC(15, svga, in_addr);

    // pcem: vid_svga_render_remap.h:73-77
    private static readonly svga_remap_fn[] address_remap_funcs =
    [
        address_remap_func_0,  address_remap_func_1,  address_remap_func_2,  address_remap_func_3,
        address_remap_func_4,  address_remap_func_5,  address_remap_func_6,  address_remap_func_7,
        address_remap_func_8,  address_remap_func_9,  address_remap_func_10, address_remap_func_11,
        address_remap_func_12, address_remap_func_13, address_remap_func_14, address_remap_func_15,
    ];

    // pcem: vid_svga_render_remap.h:79-106
    internal static void svga_recalc_remap_func(svga_t svga)
    {
        int func_nr;

        if (svga.fb_only != 0)
                func_nr = 0;
        else
        {
                if (svga.force_dword_mode != 0)
                        func_nr = VAR_DWORD_MODE;
                else if ((svga.crtc[0x14] & (1 << 6)) != 0)
                        func_nr = svga.packed_chain4 != 0 ? VAR_BYTE_MODE : VAR_DWORD_MODE;
                else if ((svga.crtc[0x17] & (1 << 6)) != 0)
                        func_nr = VAR_BYTE_MODE;
                else if ((svga.crtc[0x17] & (1 << 5)) != 0)
                        func_nr = VAR_WORD_MODE_MA15;
                else
                        func_nr = VAR_WORD_MODE_MA13;

                if ((svga.crtc[0x17] & (1 << 0)) == 0)
                        func_nr |= VAR_ROW0_MA13;
                if ((svga.crtc[0x17] & (1 << 1)) == 0)
                        func_nr |= VAR_ROW1_MA14;
        }

        svga.remap_required = (func_nr != 0) ? 1 : 0;
        svga.remap_func = address_remap_funcs[func_nr];
    }

    // DEVIATION (tous les rendus ci-dessous) : `uint32_t *p =
    //   &((uint32_t *)buffer32->line[svga->displine])[offset]` devient un INDICE dans le
    //   tableau plat Buffer32, `p = svga.displine * Stride + offset`, et `p[i]` devient
    //   `Buffer32[p + i]`, `*p++` devient `Buffer32[p++]`. Même géométrie que
    //   create_bitmap(2048, 2048) : les lignes y sont contiguës comme dans le malloc
    //   unique du C, donc un débordement de ligne tombe au même octet des deux côtés.
    //   Voir video.cs, Buffer32.

    // omitted: svga_render_null (vid_svga_render.c:8-12) — rendu de la Voodoo Banshee
    //   (vid_voodoo_banshee.c:450) ; ni vid_svga.c ni vid_vga.c ne le citent.

    // pcem: vid_svga_render.c:14-41
    internal static void svga_render_blank(svga_t svga)
    {
        int x, xx;

        if (svga.firstline_draw == 2000)
                svga.firstline_draw = svga.displine;
        svga.lastline_draw = svga.displine;

        for (x = 0; x < svga.hdisp; x++)
        {
                switch (svga.seqregs[1] & 9)
                {
                case 0:
                        for (xx = 0; xx < 9; xx++)
                                Buffer32[svga.displine * Stride + (x * 9) + xx + 32] = 0;
                        break;
                case 1:
                        for (xx = 0; xx < 8; xx++)
                                Buffer32[svga.displine * Stride + (x * 8) + xx + 32] = 0;
                        break;
                case 8:
                        for (xx = 0; xx < 18; xx++)
                                Buffer32[svga.displine * Stride + (x * 18) + xx + 32] = 0;
                        break;
                case 9:
                        for (xx = 0; xx < 16; xx++)
                                Buffer32[svga.displine * Stride + (x * 16) + xx + 32] = 0;
                        break;
                }
        }
    }

    // pcem: vid_svga_render.c:43-100
    internal static void svga_render_text_40(svga_t svga)
    {
        if (svga.firstline_draw == 2000)
                svga.firstline_draw = svga.displine;
        svga.lastline_draw = svga.displine;

        if (svga.fullchange != 0)
        {
                int offset = ((8 - svga.scrollcache) << 1) + 16;
                int p = svga.displine * Stride + offset;
                int x, xx;
                int drawcursor;
                uint8_t chr, attr, dat;
                uint32_t charaddr;
                int fg, bg;
                int xinc = ((svga.seqregs[1] & 1) != 0) ? 16 : 18;

                for (x = 0; x < svga.hdisp; x += xinc)
                {
                        uint32_t addr = svga.remap_func!(svga, svga.ma) & svga.vram_display_mask;

                        drawcursor = ((svga.ma == svga.ca) && svga.con != 0 && svga.cursoron != 0) ? 1 : 0;
                        chr = svga.vram[addr];
                        attr = svga.vram[addr + 1];

                        if ((attr & 8) != 0)
                                charaddr = svga.charsetb + (uint32_t)(chr * 128);
                        else
                                charaddr = svga.charseta + (uint32_t)(chr * 128);

                        if (drawcursor != 0)
                        {
                                bg = (int)svga.pallook[svga.egapal[attr & 15]];
                                fg = (int)svga.pallook[svga.egapal[attr >> 4]];
                        }
                        else
                        {
                                fg = (int)svga.pallook[svga.egapal[attr & 15]];
                                bg = (int)svga.pallook[svga.egapal[attr >> 4]];
                                if ((attr & 0x80) != 0 && (svga.attrregs[0x10] & 8) != 0)
                                {
                                        bg = (int)svga.pallook[svga.egapal[(attr >> 4) & 7]];
                                        if ((svga.blink & 16) != 0)
                                                fg = bg;
                                }
                        }

                        dat = svga.vram[charaddr + (uint32_t)(svga.sc << 2)];
                        if ((svga.seqregs[1] & 1) != 0)
                        {
                                for (xx = 0; xx < 16; xx += 2)
                                        Buffer32[p + xx] = Buffer32[p + xx + 1] = (uint32_t)(((dat & (0x80 >> (xx >> 1))) != 0) ? fg : bg);
                        }
                        else
                        {
                                for (xx = 0; xx < 16; xx += 2)
                                        Buffer32[p + xx] = Buffer32[p + xx + 1] = (uint32_t)(((dat & (0x80 >> (xx >> 1))) != 0) ? fg : bg);
                                if ((chr & ~0x1F) != 0xC0 || (svga.attrregs[0x10] & 4) == 0)
                                        Buffer32[p + 16] = Buffer32[p + 17] = (uint32_t)bg;
                                else
                                        Buffer32[p + 16] = Buffer32[p + 17] = (uint32_t)(((dat & 1) != 0) ? fg : bg);
                        }
                        svga.ma += 4;
                        p += xinc;
                }
                svga.ma &= svga.vram_display_mask;
        }
    }

    // pcem: vid_svga_render.c:102-159
    internal static void svga_render_text_80(svga_t svga)
    {
        if (svga.firstline_draw == 2000)
                svga.firstline_draw = svga.displine;
        svga.lastline_draw = svga.displine;

        if (svga.fullchange != 0)
        {
                int offset = (8 - svga.scrollcache) + 24;
                int p = svga.displine * Stride + offset;
                int x, xx;
                int drawcursor;
                uint8_t chr, attr, dat;
                uint32_t charaddr;
                int fg, bg;
                int xinc = ((svga.seqregs[1] & 1) != 0) ? 8 : 9;

                for (x = 0; x < svga.hdisp; x += xinc)
                {
                        uint32_t addr = svga.remap_func!(svga, svga.ma) & svga.vram_display_mask;

                        drawcursor = ((svga.ma == svga.ca) && svga.con != 0 && svga.cursoron != 0) ? 1 : 0;
                        chr = svga.vram[addr];
                        attr = svga.vram[addr + 1];

                        if ((attr & 8) != 0)
                                charaddr = svga.charsetb + (uint32_t)(chr * 128);
                        else
                                charaddr = svga.charseta + (uint32_t)(chr * 128);

                        if (drawcursor != 0)
                        {
                                bg = (int)svga.pallook[svga.egapal[attr & 15]];
                                fg = (int)svga.pallook[svga.egapal[attr >> 4]];
                        }
                        else
                        {
                                fg = (int)svga.pallook[svga.egapal[attr & 15]];
                                bg = (int)svga.pallook[svga.egapal[attr >> 4]];
                                if ((attr & 0x80) != 0 && (svga.attrregs[0x10] & 8) != 0)
                                {
                                        bg = (int)svga.pallook[svga.egapal[(attr >> 4) & 7]];
                                        if ((svga.blink & 16) != 0)
                                                fg = bg;
                                }
                        }

                        dat = svga.vram[charaddr + (uint32_t)(svga.sc << 2)];
                        if ((svga.seqregs[1] & 1) != 0)
                        {
                                for (xx = 0; xx < 8; xx++)
                                        Buffer32[p + xx] = (uint32_t)(((dat & (0x80 >> xx)) != 0) ? fg : bg);
                        }
                        else
                        {
                                for (xx = 0; xx < 8; xx++)
                                        Buffer32[p + xx] = (uint32_t)(((dat & (0x80 >> xx)) != 0) ? fg : bg);
                                if ((chr & ~0x1F) != 0xC0 || (svga.attrregs[0x10] & 4) == 0)
                                        Buffer32[p + 8] = (uint32_t)bg;
                                else
                                        Buffer32[p + 8] = (uint32_t)(((dat & 1) != 0) ? fg : bg);
                        }
                        svga.ma += 4;
                        p += xinc;
                }
                svga.ma &= svga.vram_display_mask;
        }
    }

    // omitted: svga_render_text_80_ksc5601 (vid_svga_render.c:161-281) — texte coréen
    //   KSC-5601 des cartes ATI-28800 et ET4000 coréennes (vid_ati28800.c:327,
    //   vid_et4000.c:454, :481) ; svga_recalctimings ne le choisit jamais.

    // pcem: vid_svga_render.c:283-316
    internal static void svga_render_2bpp_lowres(svga_t svga)
    {
        uint32_t changed_addr = svga.remap_func!(svga, svga.ma);

        if (svga.changedvram[changed_addr >> 12] != 0 || svga.changedvram[(changed_addr >> 12) + 1] != 0 || svga.fullchange != 0)
        {
                int x;
                int offset = ((8 - svga.scrollcache) << 1) + 16;
                int p = svga.displine * Stride + offset;

                if (svga.firstline_draw == 2000)
                        svga.firstline_draw = svga.displine;
                svga.lastline_draw = svga.displine;

                for (x = 0; x <= svga.hdisp; x += 16)
                {
                        uint32_t addr = svga.remap_func(svga, svga.ma);
                        uint8_t dat0, dat1;

                        // DEVIATION: `uint8_t dat[2]`, tableau de pile, en deux locales.
                        dat0 = svga.vram[addr];
                        dat1 = svga.vram[addr + 1];
                        svga.ma += 4;
                        svga.ma &= svga.vram_display_mask;

                        Buffer32[p + 0] = Buffer32[p + 1] = svga.pallook[svga.egapal[(dat0 >> 6) & 3]];
                        Buffer32[p + 2] = Buffer32[p + 3] = svga.pallook[svga.egapal[(dat0 >> 4) & 3]];
                        Buffer32[p + 4] = Buffer32[p + 5] = svga.pallook[svga.egapal[(dat0 >> 2) & 3]];
                        Buffer32[p + 6] = Buffer32[p + 7] = svga.pallook[svga.egapal[dat0 & 3]];
                        Buffer32[p + 8] = Buffer32[p + 9] = svga.pallook[svga.egapal[(dat1 >> 6) & 3]];
                        Buffer32[p + 10] = Buffer32[p + 11] = svga.pallook[svga.egapal[(dat1 >> 4) & 3]];
                        Buffer32[p + 12] = Buffer32[p + 13] = svga.pallook[svga.egapal[(dat1 >> 2) & 3]];
                        Buffer32[p + 14] = Buffer32[p + 15] = svga.pallook[svga.egapal[dat1 & 3]];

                        p += 16;
                }
        }
    }

    // pcem: vid_svga_render.c:318-349
    internal static void svga_render_2bpp_highres(svga_t svga)
    {
        uint32_t changed_addr = svga.remap_func!(svga, svga.ma);

        if (svga.changedvram[changed_addr >> 12] != 0 || svga.changedvram[(changed_addr >> 12) + 1] != 0 || svga.fullchange != 0)
        {
                int x;
                int offset = (8 - svga.scrollcache) + 24;
                int p = svga.displine * Stride + offset;

                if (svga.firstline_draw == 2000)
                        svga.firstline_draw = svga.displine;
                svga.lastline_draw = svga.displine;

                for (x = 0; x <= svga.hdisp; x += 8)
                {
                        uint32_t addr = svga.remap_func(svga, svga.ma);
                        uint8_t dat0, dat1;

                        dat0 = svga.vram[addr];
                        dat1 = svga.vram[addr + 1];
                        svga.ma += 4;
                        svga.ma &= svga.vram_display_mask;

                        Buffer32[p++] = svga.pallook[svga.egapal[(dat0 >> 6) & 3]];
                        Buffer32[p++] = svga.pallook[svga.egapal[(dat0 >> 4) & 3]];
                        Buffer32[p++] = svga.pallook[svga.egapal[(dat0 >> 2) & 3]];
                        Buffer32[p++] = svga.pallook[svga.egapal[dat0 & 3]];
                        Buffer32[p++] = svga.pallook[svga.egapal[(dat1 >> 6) & 3]];
                        Buffer32[p++] = svga.pallook[svga.egapal[(dat1 >> 4) & 3]];
                        Buffer32[p++] = svga.pallook[svga.egapal[(dat1 >> 2) & 3]];
                        Buffer32[p++] = svga.pallook[svga.egapal[dat1 & 3]];
                }
        }
    }

    // pcem: vid_svga_render.c:351-390
    internal static void svga_render_4bpp_lowres(svga_t svga)
    {
        uint32_t changed_addr = svga.remap_func!(svga, svga.ma);

        if (svga.changedvram[changed_addr >> 12] != 0 || svga.changedvram[(changed_addr >> 12) + 1] != 0 || svga.fullchange != 0)
        {
                int x;
                int offset = ((8 - svga.scrollcache) << 1) + 16;
                int p = svga.displine * Stride + offset;

                if (svga.firstline_draw == 2000)
                        svga.firstline_draw = svga.displine;
                svga.lastline_draw = svga.displine;

                for (x = 0; x <= svga.hdisp; x += 16)
                {
                        uint8_t edat0, edat1, edat2, edat3;
                        uint8_t dat;
                        uint32_t addr = svga.remap_func(svga, svga.ma);

                        // DEVIATION: `*(uint32_t *)(&edat[0]) = *(uint32_t *)(&svga->vram[addr])`
                        //   — une copie de quatre octets par cast de pointeur, en quatre
                        //   locales.
                        edat0 = svga.vram[addr];
                        edat1 = svga.vram[addr + 1];
                        edat2 = svga.vram[addr + 2];
                        edat3 = svga.vram[addr + 3];
                        svga.ma += 4;
                        svga.ma &= svga.vram_display_mask;

                        dat = (uint8_t)(edatlookup[edat0 >> 6, edat1 >> 6] | (edatlookup[edat2 >> 6, edat3 >> 6] << 2));
                        Buffer32[p + 0] = Buffer32[p + 1] = svga.pallook[svga.egapal[(dat >> 4) & svga.plane_mask]];
                        Buffer32[p + 2] = Buffer32[p + 3] = svga.pallook[svga.egapal[dat & svga.plane_mask]];
                        dat = (uint8_t)(edatlookup[(edat0 >> 4) & 3, (edat1 >> 4) & 3] |
                                        (edatlookup[(edat2 >> 4) & 3, (edat3 >> 4) & 3] << 2));
                        Buffer32[p + 4] = Buffer32[p + 5] = svga.pallook[svga.egapal[(dat >> 4) & svga.plane_mask]];
                        Buffer32[p + 6] = Buffer32[p + 7] = svga.pallook[svga.egapal[dat & svga.plane_mask]];
                        dat = (uint8_t)(edatlookup[(edat0 >> 2) & 3, (edat1 >> 2) & 3] |
                                        (edatlookup[(edat2 >> 2) & 3, (edat3 >> 2) & 3] << 2));
                        Buffer32[p + 8] = Buffer32[p + 9] = svga.pallook[svga.egapal[(dat >> 4) & svga.plane_mask]];
                        Buffer32[p + 10] = Buffer32[p + 11] = svga.pallook[svga.egapal[dat & svga.plane_mask]];
                        dat = (uint8_t)(edatlookup[edat0 & 3, edat1 & 3] | (edatlookup[edat2 & 3, edat3 & 3] << 2));
                        Buffer32[p + 12] = Buffer32[p + 13] = svga.pallook[svga.egapal[(dat >> 4) & svga.plane_mask]];
                        Buffer32[p + 14] = Buffer32[p + 15] = svga.pallook[svga.egapal[dat & svga.plane_mask]];

                        p += 16;
                }
        }
    }

    // pcem: vid_svga_render.c:392-429
    internal static void svga_render_4bpp_highres(svga_t svga)
    {
        uint32_t changed_addr = svga.remap_func!(svga, svga.ma);

        if (svga.changedvram[changed_addr >> 12] != 0 || svga.changedvram[(changed_addr >> 12) + 1] != 0 || svga.fullchange != 0)
        {
                int x;
                int offset = (8 - svga.scrollcache) + 24;
                int p = svga.displine * Stride + offset;

                if (svga.firstline_draw == 2000)
                        svga.firstline_draw = svga.displine;
                svga.lastline_draw = svga.displine;

                for (x = 0; x <= svga.hdisp; x += 8)
                {
                        uint8_t edat0, edat1, edat2, edat3;
                        uint8_t dat;
                        uint32_t addr = svga.remap_func(svga, svga.ma);

                        edat0 = svga.vram[addr];
                        edat1 = svga.vram[addr + 1];
                        edat2 = svga.vram[addr + 2];
                        edat3 = svga.vram[addr + 3];
                        svga.ma += 4;
                        svga.ma &= svga.vram_display_mask;

                        dat = (uint8_t)(edatlookup[edat0 >> 6, edat1 >> 6] | (edatlookup[edat2 >> 6, edat3 >> 6] << 2));
                        Buffer32[p++] = svga.pallook[svga.egapal[(dat >> 4) & svga.plane_mask]];
                        Buffer32[p++] = svga.pallook[svga.egapal[dat & svga.plane_mask]];
                        dat = (uint8_t)(edatlookup[(edat0 >> 4) & 3, (edat1 >> 4) & 3] |
                                        (edatlookup[(edat2 >> 4) & 3, (edat3 >> 4) & 3] << 2));
                        Buffer32[p++] = svga.pallook[svga.egapal[(dat >> 4) & svga.plane_mask]];
                        Buffer32[p++] = svga.pallook[svga.egapal[dat & svga.plane_mask]];
                        dat = (uint8_t)(edatlookup[(edat0 >> 2) & 3, (edat1 >> 2) & 3] |
                                        (edatlookup[(edat2 >> 2) & 3, (edat3 >> 2) & 3] << 2));
                        Buffer32[p++] = svga.pallook[svga.egapal[(dat >> 4) & svga.plane_mask]];
                        Buffer32[p++] = svga.pallook[svga.egapal[dat & svga.plane_mask]];
                        dat = (uint8_t)(edatlookup[edat0 & 3, edat1 & 3] | (edatlookup[edat2 & 3, edat3 & 3] << 2));
                        Buffer32[p++] = svga.pallook[svga.egapal[(dat >> 4) & svga.plane_mask]];
                        Buffer32[p++] = svga.pallook[svga.egapal[dat & svga.plane_mask]];
                }
        }
    }

    // DEVIATION: `*(uint32_t *)(&svga->vram[a])` — lecture de quatre octets par cast de
    //   pointeur, reconstruite en petit-boutien. Utilisée par les deux rendus 8 bpp.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint32_t vram_l(svga_t svga, uint32_t a)
        => (uint32_t)(svga.vram[a] | (svga.vram[a + 1] << 8) | (svga.vram[a + 2] << 16) | (svga.vram[a + 3] << 24));

    // pcem: vid_svga_render.c:431-471
    internal static void svga_render_8bpp_lowres(svga_t svga)
    {
        uint32_t changed_addr = svga.remap_func!(svga, svga.ma);

        if (svga.changedvram[changed_addr >> 12] != 0 || svga.changedvram[(changed_addr >> 12) + 1] != 0 || svga.fullchange != 0)
        {
                int x;
                int offset = (8 - (svga.scrollcache & 6)) + 24;
                int p = svga.displine * Stride + offset;

                if (svga.firstline_draw == 2000)
                        svga.firstline_draw = svga.displine;
                svga.lastline_draw = svga.displine;

                if (svga.remap_required == 0)
                {
                        for (x = 0; x <= svga.hdisp; x += 8)
                        {
                                uint32_t dat = vram_l(svga, svga.ma & svga.vram_display_mask);

                                Buffer32[p + 0] = Buffer32[p + 1] = svga.pallook[dat & 0xff];
                                Buffer32[p + 2] = Buffer32[p + 3] = svga.pallook[(dat >> 8) & 0xff];
                                Buffer32[p + 4] = Buffer32[p + 5] = svga.pallook[(dat >> 16) & 0xff];
                                Buffer32[p + 6] = Buffer32[p + 7] = svga.pallook[(dat >> 24) & 0xff];

                                svga.ma += 4;
                                p += 8;
                        }
                }
                else
                {
                        for (x = 0; x <= svga.hdisp; x += 8)
                        {
                                uint32_t addr = svga.remap_func(svga, svga.ma);
                                uint32_t dat = vram_l(svga, addr & svga.vram_display_mask);

                                Buffer32[p + 0] = Buffer32[p + 1] = svga.pallook[dat & 0xff];
                                Buffer32[p + 2] = Buffer32[p + 3] = svga.pallook[(dat >> 8) & 0xff];
                                Buffer32[p + 4] = Buffer32[p + 5] = svga.pallook[(dat >> 16) & 0xff];
                                Buffer32[p + 6] = Buffer32[p + 7] = svga.pallook[(dat >> 24) & 0xff];

                                svga.ma += 4;
                                p += 8;
                        }
                }
                svga.ma &= svga.vram_display_mask;
        }
    }

    // pcem: vid_svga_render.c:473-517
    internal static void svga_render_8bpp_highres(svga_t svga)
    {
        uint32_t changed_addr = svga.remap_func!(svga, svga.ma);

        if (svga.changedvram[changed_addr >> 12] != 0 || svga.changedvram[(changed_addr >> 12) + 1] != 0 || svga.fullchange != 0)
        {
                int x;
                int offset = (8 - ((svga.scrollcache & 6) >> 1)) + 24;
                int p = svga.displine * Stride + offset;

                if (svga.firstline_draw == 2000)
                        svga.firstline_draw = svga.displine;
                svga.lastline_draw = svga.displine;

                if (svga.remap_required == 0)
                {
                        for (x = 0; x <= svga.hdisp; x += 8)
                        {
                                uint32_t dat = vram_l(svga, svga.ma & svga.vram_display_mask);
                                Buffer32[p++] = svga.pallook[dat & 0xff];
                                Buffer32[p++] = svga.pallook[(dat >> 8) & 0xff];
                                Buffer32[p++] = svga.pallook[(dat >> 16) & 0xff];
                                Buffer32[p++] = svga.pallook[(dat >> 24) & 0xff];

                                dat = vram_l(svga, (svga.ma + 4) & svga.vram_display_mask);
                                Buffer32[p++] = svga.pallook[dat & 0xff];
                                Buffer32[p++] = svga.pallook[(dat >> 8) & 0xff];
                                Buffer32[p++] = svga.pallook[(dat >> 16) & 0xff];
                                Buffer32[p++] = svga.pallook[(dat >> 24) & 0xff];

                                svga.ma += 8;
                        }
                }
                else
                {
                        for (x = 0; x <= svga.hdisp; x += 4)
                        {
                                uint32_t addr = svga.remap_func(svga, svga.ma);
                                uint32_t dat = vram_l(svga, addr & svga.vram_display_mask);

                                svga.ma += 4;

                                Buffer32[p++] = svga.pallook[dat & 0xff];
                                Buffer32[p++] = svga.pallook[(dat >> 8) & 0xff];
                                Buffer32[p++] = svga.pallook[(dat >> 16) & 0xff];
                                Buffer32[p++] = svga.pallook[(dat >> 24) & 0xff];
                        }
                }

                svga.ma &= svga.vram_display_mask;
        }
    }

    // omitted: les corps de svga_render_15bpp_lowres / _highres, 16bpp, 24bpp et 32bpp
    //   (vid_svga_render.c:519-854). svga_recalctimings ne les choisit que pour
    //   svga->bpp = 15, 16, 24 ou 32 (vid_svga.c:391-414). Or bpp vaut 8 : svga_init le
    //   pose (vid_svga.c:771), vga_init le repose (vid_vga.c:114), et aucune autre ligne
    //   de vid_vga.c ni de vid_svga.c ne l'écrit — seuls les pilotes de cartes SVGA le
    //   changent. Les FONCTIONS restent, parce que svga_recalctimings les nomme toutes
    //   (vid_svga.c:391-414) et svga_poll les variantes _lowres (:694-696) : un corps
    //   fatal() rend leur atteinte bruyante (R6). L'oracle, qui laisse video_15to32 et
    //   video_16to32 NULL, planterait sur les rendus 15 et 16 bpp ; pas sur 24 et 32,
    //   qui ne lisent aucune table — là, seul le C# lèverait.
    internal static void svga_render_15bpp_lowres(svga_t svga) => pc.fatal("not implemented: vid_svga_render.c:519");
    internal static void svga_render_15bpp_highres(svga_t svga) => pc.fatal("not implemented: vid_svga_render.c:559");
    internal static void svga_render_16bpp_lowres(svga_t svga) => pc.fatal("not implemented: vid_svga_render.c:606");
    internal static void svga_render_16bpp_highres(svga_t svga) => pc.fatal("not implemented: vid_svga_render.c:647");
    internal static void svga_render_24bpp_lowres(svga_t svga) => pc.fatal("not implemented: vid_svga_render.c:694");
    internal static void svga_render_24bpp_highres(svga_t svga) => pc.fatal("not implemented: vid_svga_render.c:742");
    internal static void svga_render_32bpp_lowres(svga_t svga) => pc.fatal("not implemented: vid_svga_render.c:791");
    internal static void svga_render_32bpp_highres(svga_t svga) => pc.fatal("not implemented: vid_svga_render.c:825");

    // omitted: svga_render_ABGR8888_highres et svga_render_RGBA8888_highres
    //   (vid_svga_render.c:856-916) — rendus du RAMDAC ATI 68860 (vid_ati68860_ramdac.c:
    //   106, :109) ; ni vid_svga.c ni vid_vga.c ne les citent.
}
