// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/video/vid_ega.c + includes/private/video/vid_ega.h
// STATUS: transcribed — ega_t, ega_rotate, pallook16/64, egaswitchread, egaswitches, ega_out,
//         ega_in, ega_recalctimings, ega_draw_text, ega_draw_2bpp, ega_draw_4bpp_lowres,
//         ega_draw_4bpp_highres, ega_poll, ega_write, ega_read, ega_init, ega_standalone_init,
//         ega_standalone_available, ega_close, ega_speed_changed, ega_add_status_info, ega_config,
//         ega_device. Omis : les printf/pclog de journal (sortie pure).
//
// G9.2 (PLAN-G9.md). L'EGA d'IBM : ROM en C0000, ports 3A0-3DF (3Bx/3Dx échangés selon 3C2 bit 0),
// A0000 sur 128 Ko remappé par GDC6, quatre plans de 64 Ko (256 Ko alloués, 64/128/256 Ko
// configurés), texte, 16 couleurs (deux résolutions) et 4 couleurs.

// CS8600/CS8602/CS8604 : `(ega_t)p` part du `object?` des delegates de mem.cs, io.cs et timer.cs.
#pragma warning disable CS8600, CS8602, CS8604

using System.Text;
using iXtal26.Flash;
using iXtal26.Memory;
using iXtal26.Models;
using iXtal26.PluginApi;
using static iXtal26.Cpu.x86;
using static iXtal26.Flash.rom;
using static iXtal26.Memory.mem;
using static iXtal26.PluginApi.device;
using static iXtal26.Video.video;
using static iXtal26.io;
using static iXtal26.timer;

namespace iXtal26.Video;

// pcem: vid_ega.h:3-70
// Classe et non struct : son adresse est prise (timer_add, mem_mapping_add, io_sethandler).
internal sealed class ega_t
{
    internal mem_mapping_t mapping = new();

    internal rom_t bios_rom = new();

    internal uint8_t crtcreg;
    internal uint8_t[] crtc = new uint8_t[32];
    internal uint8_t[] gdcreg = new uint8_t[16];
    internal int gdcaddr;
    internal uint8_t[] attrregs = new uint8_t[32];
    internal int attraddr, attrff;
    internal uint8_t[] seqregs = new uint8_t[64];
    internal int seqaddr;

    internal uint8_t miscout;
    internal int vidclock;

    internal uint8_t la, lb, lc, ld;

    internal uint8_t stat;

    internal int fast;
    internal uint8_t colourcompare, colournocare;
    internal int readmode, writemode, readplane;
    internal int chain4, chain2_read, chain2_write;
    internal uint8_t writemask;
    internal uint32_t charseta, charsetb;

    internal uint8_t[] egapal = new uint8_t[16];
    internal uint32_t[] pallook = [];

    internal int vtotal, dispend, vsyncstart, split;
    internal int hdisp, htotal, hdisp_time, rowoffset;
    internal int lowres, interlace;
    internal int linedbl, rowcount;
    internal double clock;
    internal uint32_t ma_latch;

    internal int vres;

    internal uint64_t dispontime, dispofftime;
    internal pc_timer_t timer = new();

    internal uint8_t scrblank;

    internal int dispon;
    internal int hdisp_on;

    internal uint32_t ma, maback, ca;
    internal int vc;
    internal int sc;
    internal int linepos, vslines, linecountff, oddeven;
    internal int con, cursoron, blink;
    internal int scrollcache;

    internal int firstline, lastline;
    internal int firstline_draw, lastline_draw;
    internal int displine;

    internal uint8_t[] vram = [];
    internal int vrammask;
    internal uint32_t vram_limit;

    internal int video_res_x, video_res_y, video_bpp;
    internal int frames;
}

/*EGA emulation*/
internal static class vid_ega
{
    // pcem: vid_ega.c:14 — `static uint8_t ega_rotate[8][256]`, à plat.
    private static readonly uint8_t[] ega_rotate = new uint8_t[8 * 256];

    // pcem: vid_ega.c:16
    private static readonly uint32_t[] pallook16 = new uint32_t[256], pallook64 = new uint32_t[256];

    /*3C2 controls default mode on EGA. On VGA, it determines monitor type (mono or colour)*/
    // pcem: vid_ega.c:19. egaswitchread n'est jamais remis à zéro chez PCem : il traverse les
    //   amorçages d'un processus. DEVIATION (PLAN-G9.md, décision n° 4) : remis à zéro à chaque
    //   ega_init, des deux côtés (h_ega_attach côté oracle) — la phase 2 du boot-diff hériterait
    //   sinon de la phase 1.
    internal static int egaswitchread, egaswitches = 9; /*7=CGA mode (200 lines), 9=EGA mode (350 lines), 8=EGA mode (200 lines)*/

    // pcem: vid_ega.c:21-140
    internal static void ega_out(uint16_t addr, uint8_t val, object? p)
    {
        ega_t ega = (ega_t)p;
        int c;
        uint8_t o, old;

        if (((addr & 0xfff0) == 0x3d0 || (addr & 0xfff0) == 0x3b0) && (ega.miscout & 1) == 0)
                addr ^= 0x60;

        switch (addr)
        {
        case 0x3c0:
                if (ega.attrff == 0)
                        ega.attraddr = val & 31;
                else
                {
                        ega.attrregs[ega.attraddr & 31] = val;
                        if (ega.attraddr < 16)
                                fullchange = changeframecount;
                        // pcem bug, reproduced: PB-99 — l'attribut 10h bit 7 et l'attribut 14h
                        //   (:39-42) sont des registres de la VGA ; l'EGA ne les a pas.
                        if (ega.attraddr == 0x10 || ega.attraddr == 0x14 || ega.attraddr < 0x10)
                        {
                                for (c = 0; c < 16; c++)
                                {
                                        if ((ega.attrregs[0x10] & 0x80) != 0)
                                                ega.egapal[c] = (uint8_t)((ega.attrregs[c] & 0xf) | ((ega.attrregs[0x14] & 0xf) << 4));
                                        else
                                                ega.egapal[c] = (uint8_t)((ega.attrregs[c] & 0x3f) | ((ega.attrregs[0x14] & 0xc) << 4));
                                }
                        }
                }
                ega.attrff ^= 1;
                break;
        case 0x3c2:
                egaswitchread = val & 0xc;
                ega.vres = (val & 0x80) == 0 ? 1 : 0;
                ega.pallook = ega.vres != 0 ? pallook16 : pallook64;
                ega.vidclock = val & 4; /*printf("3C2 write %02X\n",val);*/
                ega.miscout = val;
                break;
        case 0x3c4:
                ega.seqaddr = val;
                break;
        case 0x3c5:
                o = ega.seqregs[ega.seqaddr & 0xf];
                ega.seqregs[ega.seqaddr & 0xf] = val;
                if (o != val && (ega.seqaddr & 0xf) == 1)
                        ega_recalctimings(ega);
                switch (ega.seqaddr & 0xf)
                {
                case 1:
                        if (ega.scrblank != 0 && (val & 0x20) == 0)
                                fullchange = 3;
                        ega.scrblank = (uint8_t)((ega.scrblank & ~0x20) | (val & 0x20));
                        break;
                case 2:
                        ega.writemask = (uint8_t)(val & 0xf);
                        break;
                case 3:
                        ega.charsetb = (uint32_t)((((val >> 2) & 3) * 0x10000) + 2);
                        ega.charseta = (uint32_t)(((val & 3) * 0x10000) + 2);
                        break;
                case 4:
                        ega.chain2_write = (val & 4) == 0 ? 1 : 0;
                        break;
                }
                break;
        case 0x3ce:
                ega.gdcaddr = val;
                break;
        case 0x3cf:
                ega.gdcreg[ega.gdcaddr & 15] = val;
                switch (ega.gdcaddr & 15)
                {
                case 2:
                        ega.colourcompare = val;
                        break;
                case 4:
                        ega.readplane = val & 3;
                        break;
                case 5:
                        ega.writemode = val & 3;
                        ega.readmode = val & 8;
                        ega.chain2_read = val & 0x10;
                        break;
                case 6:
                        //                                pclog("Write mapping %02X\n", val);
                        switch (val & 0xc)
                        {
                        case 0x0: /*128k at A0000*/
                                mem_mapping_set_addr(ega.mapping, 0xa0000, 0x20000);
                                break;
                        case 0x4: /*64k at A0000*/
                                mem_mapping_set_addr(ega.mapping, 0xa0000, 0x10000);
                                break;
                        case 0x8: /*32k at B0000*/
                                mem_mapping_set_addr(ega.mapping, 0xb0000, 0x08000);
                                break;
                        case 0xC: /*32k at B8000*/
                                mem_mapping_set_addr(ega.mapping, 0xb8000, 0x08000);
                                break;
                        }
                        break;
                case 7:
                        ega.colournocare = val;
                        break;
                }
                break;
        case 0x3d4:
                // omitted: pclog("Write 3d4 …") (:122) — sortie pure.
                ega.crtcreg = (uint8_t)(val & 31);
                return;
        case 0x3d5:
                // omitted: pclog("Write 3d5 …") (:126) — sortie pure.
                //                if (ega->crtcreg == 1 && val == 0x14)
                //                        fatal("Here\n");
                // pcem bug, reproduced: PB-99 — la protection de CR0-CR7 par CR11 bit 7 (:128) est
                //   celle de la VGA.
                if (ega.crtcreg <= 7 && (ega.crtc[0x11] & 0x80) != 0)
                        return;
                old = ega.crtc[ega.crtcreg];
                ega.crtc[ega.crtcreg] = val;
                if (old != val)
                {
                        if (ega.crtcreg < 0xe || ega.crtcreg > 0x10)
                        {
                                fullchange = changeframecount;
                                ega_recalctimings(ega);
                        }
                }
                break;
        }
    }

    // pcem: vid_ega.c:142-187
    internal static uint8_t ega_in(uint16_t addr, object? p)
    {
        ega_t ega = (ega_t)p;

        // omitted: pclog("ega_in %04X\n", addr) (:145-146) — sortie pure.
        if (((addr & 0xfff0) == 0x3d0 || (addr & 0xfff0) == 0x3b0) && (ega.miscout & 1) == 0)
                addr ^= 0x60;

        // pcem bug, reproduced: PB-99 — tous les registres se relisent (:152-179) ; sur l'EGA, la
        //   plupart sont en écriture seule (CR10/CR11 rendent le crayon optique).
        switch (addr)
        {
        case 0x3c0:
                return (uint8_t)ega.attraddr;
        case 0x3c1:
                return ega.attrregs[ega.attraddr];
        case 0x3c2:
                // pcem bug, reproduced: PB-228 — Input Status 0 ne rend que le bit 4 ; son bit 7 (« CRT
                //   Interrupt », 0 pendant le retour vertical) reste à 0.
                //                printf("Read egaswitch %02X %02X %i\n",egaswitchread,egaswitches,VGA);
                switch (egaswitchread)
                {
                case 0xc:
                        return (uint8_t)((egaswitches & 1) != 0 ? 0x10 : 0);
                case 0x8:
                        return (uint8_t)((egaswitches & 2) != 0 ? 0x10 : 0);
                case 0x4:
                        return (uint8_t)((egaswitches & 4) != 0 ? 0x10 : 0);
                case 0x0:
                        return (uint8_t)((egaswitches & 8) != 0 ? 0x10 : 0);
                }
                break;
        case 0x3c4:
                return (uint8_t)ega.seqaddr;
        case 0x3c5:
                return ega.seqregs[ega.seqaddr & 0xf];
        case 0x3ce:
                return (uint8_t)ega.gdcaddr;
        case 0x3cf:
                return ega.gdcreg[ega.gdcaddr & 0xf];
        case 0x3d4:
                return ega.crtcreg;
        case 0x3d5:
                return ega.crtc[ega.crtcreg];
        case 0x3da:
                ega.attrff = 0;
                // pcem bug, reproduced: PB-99 — « Fools IBM EGA video BIOS self-test » : les bits 4
                //   et 5 basculent à chaque lecture, au lieu de rendre l'état des broches vidéo.
                ega.stat ^= 0x30; /*Fools IBM EGA video BIOS self-test*/
                return ega.stat;
        }
        //        printf("Bad EGA read %04X %04X:%04X\n",addr,cs>>4,pc);
        return 0xff;
    }

    // pcem: vid_ega.c:189-255
    internal static void ega_recalctimings(ega_t ega)
    {
        double _dispontime, _dispofftime, disptime;
        double crtcconst;

        ega.vtotal = ega.crtc[6];
        ega.dispend = ega.crtc[0x12];
        ega.vsyncstart = ega.crtc[0x10];
        ega.split = ega.crtc[0x18];

        // pcem bug, reproduced: PB-99 — CR07 bits 5-7 et CR09 bit 6 (:200-219) sont les bits 9 de la VGA ; l'EGA
        //   n'en a pas (07h bits 6-7 et 09h bits 5-7 « Not Used »).
        if ((ega.crtc[7] & 1) != 0)
                ega.vtotal |= 0x100;
        if ((ega.crtc[7] & 32) != 0)
                ega.vtotal |= 0x200;
        ega.vtotal++;

        if ((ega.crtc[7] & 2) != 0)
                ega.dispend |= 0x100;
        if ((ega.crtc[7] & 64) != 0)
                ega.dispend |= 0x200;
        ega.dispend++;

        if ((ega.crtc[7] & 4) != 0)
                ega.vsyncstart |= 0x100;
        if ((ega.crtc[7] & 128) != 0)
                ega.vsyncstart |= 0x200;
        ega.vsyncstart++;

        if ((ega.crtc[7] & 0x10) != 0)
                ega.split |= 0x100;
        if ((ega.crtc[9] & 0x40) != 0)
                ega.split |= 0x200;
        ega.split += 2;

        ega.hdisp = ega.crtc[1];
        ega.hdisp++;

        ega.rowoffset = ega.crtc[0x13];

        // omitted: printf("Recalc! …") (:228-229) — sortie pure ; l'oracle l'éteint de même
        //   (harness_ega.c).

        if (ega.vidclock != 0)
                crtcconst = (ega.seqregs[1] & 1) != 0 ? pit.MDACONST : (pit.MDACONST * (9.0 / 8.0));
        else
                crtcconst = (ega.seqregs[1] & 1) != 0 ? pit.CGACONST : (pit.CGACONST * (9.0 / 8.0));

        disptime = ega.crtc[0] + 2;
        _dispontime = ega.crtc[1] + 1;

        // omitted: printf("Disptime …") (:238) — sortie pure.
        if ((ega.seqregs[1] & 8) != 0)
        {
                disptime *= 2;
                _dispontime *= 2;
        }
        _dispofftime = disptime - _dispontime;
        _dispontime *= crtcconst;
        _dispofftime *= crtcconst;

        // pcem bug, reproduced: PB-36 — la conversion d'un double négatif de la CGA (G9.2, PB-36
        //   élargi).
        ega.dispontime = unchecked((uint64_t)(int64_t)_dispontime);
        ega.dispofftime = unchecked((uint64_t)(int64_t)_dispofftime);
        // omitted: pclog("dispontime …") (:249-250) — sortie pure.
    }

    // pcem: vid_ega.c:257-325
    private static void ega_draw_text(ega_t ega)
    {
        int x, xx;
        int line = ega.displine * Stride;

        for (x = 0; x < ega.hdisp; x++)
        {
                int drawcursor = ((ega.ma == ega.ca) && ega.con != 0 && ega.cursoron != 0) ? 1 : 0;
                uint8_t chr = ega.vram[(ega.ma << 1) & ega.vrammask];
                uint8_t attr = ega.vram[((ega.ma << 1) + 1) & ega.vrammask];
                uint8_t dat;
                uint32_t fg, bg;
                uint32_t charaddr;

                if ((attr & 8) != 0)
                        charaddr = (uint32_t)(ega.charsetb + (chr * 128));
                else
                        charaddr = (uint32_t)(ega.charseta + (chr * 128));

                if (drawcursor != 0)
                {
                        bg = ega.pallook[ega.egapal[attr & 15]];
                        fg = ega.pallook[ega.egapal[attr >> 4]];
                }
                else
                {
                        fg = ega.pallook[ega.egapal[attr & 15]];
                        bg = ega.pallook[ega.egapal[attr >> 4]];
                        if ((attr & 0x80) != 0 && (ega.attrregs[0x10] & 8) != 0)
                        {
                                bg = ega.pallook[ega.egapal[(attr >> 4) & 7]];
                                if ((ega.blink & 16) != 0)
                                        fg = bg;
                        }
                }

                // pcem bug, reproduced: PB-99 — l'adresse de la police n'est pas masquée par vrammask : avec
                //   64 Ko, les tables 1 à 3 de SR3 (0x10002 et au-delà) lisent hors de la mémoire configurée.
                dat = ega.vram[charaddr + (uint32_t)(ega.sc << 2)];
                if ((ega.seqregs[1] & 8) != 0)
                {
                        if ((ega.seqregs[1] & 1) != 0)
                        {
                                for (xx = 0; xx < 8; xx++)
                                        Buffer32[line + (((x << 4) + 32 + (xx << 1)) & 2047)] =
                                                Buffer32[line + (((x << 4) + 33 + (xx << 1)) & 2047)] =
                                                        (dat & (0x80 >> xx)) != 0 ? fg : bg;
                        }
                        else
                        {
                                for (xx = 0; xx < 8; xx++)
                                        Buffer32[line + (((x * 18) + 32 + (xx << 1)) & 2047)] =
                                                Buffer32[line + (((x * 18) + 33 + (xx << 1)) & 2047)] =
                                                        (dat & (0x80 >> xx)) != 0 ? fg : bg;
                                if ((chr & ~0x1f) != 0xc0 || (ega.attrregs[0x10] & 4) == 0)
                                        Buffer32[line + (((x * 18) + 32 + 16) & 2047)] =
                                                Buffer32[line + (((x * 18) + 32 + 17) & 2047)] = bg;
                                else
                                        Buffer32[line + (((x * 18) + 32 + 16) & 2047)] =
                                                Buffer32[line + (((x * 18) + 32 + 17) & 2047)] =
                                                        (dat & 1) != 0 ? fg : bg;
                        }
                }
                else
                {
                        if ((ega.seqregs[1] & 1) != 0)
                        {
                                for (xx = 0; xx < 8; xx++)
                                        Buffer32[line + (((x << 3) + 32 + xx) & 2047)] =
                                                (dat & (0x80 >> xx)) != 0 ? fg : bg;
                        }
                        else
                        {
                                for (xx = 0; xx < 8; xx++)
                                        Buffer32[line + (((x * 9) + 32 + xx) & 2047)] =
                                                (dat & (0x80 >> xx)) != 0 ? fg : bg;
                                if ((chr & ~0x1f) != 0xc0 || (ega.attrregs[0x10] & 4) == 0)
                                        Buffer32[line + (((x * 9) + 32 + 8) & 2047)] = bg;
                                else
                                        Buffer32[line + (((x * 9) + 32 + 8) & 2047)] =
                                                (dat & 1) != 0 ? fg : bg;
                        }
                }
                ega.ma += 4;
                ega.ma &= (uint32_t)ega.vrammask;
        }
    }

    // pcem: vid_ega.c:327-384
    private static void ega_draw_2bpp(ega_t ega)
    {
        int x;
        int offset = ((8 - ega.scrollcache) << 1) + 16;
        int line = ega.displine * Stride;
        // pcem bug, reproduced: PB-89 — les colonnes peuvent dépasser 2047 (hdisp, soit CR01 + 1, jusqu'à 256) :
        //   le C écrit alors dans la ligne suivante de buffer32 (lignes contiguës, wx-sdl2-video.c:59-69), jamais
        //   hors du bloc — ici de même. Le texte, lui, borne par `& 2047`.

        for (x = 0; x <= ega.hdisp; x++)
        {
                uint8_t edat0, edat1;
                uint32_t addr = ega.ma;

                if ((ega.crtc[0x17] & 0x40) == 0)
                {
                        addr = (uint32_t)((addr << 1) & ega.vrammask);
                        addr &= ~7u;
                        if ((ega.crtc[0x17] & 0x20) != 0 && (ega.ma & 0x20000) != 0)
                                addr |= 4;
                        if ((ega.crtc[0x17] & 0x20) == 0 && (ega.ma & 0x8000) != 0)
                                addr |= 4;
                }
                if ((ega.crtc[0x17] & 0x01) == 0)
                        addr = (addr & ~0x8000u) | ((ega.sc & 1) != 0 ? 0x8000u : 0);
                if ((ega.crtc[0x17] & 0x02) == 0)
                        addr = (addr & ~0x10000u) | ((ega.sc & 2) != 0 ? 0x10000u : 0);

                edat0 = ega.vram[addr];
                edat1 = ega.vram[addr | 0x1];
                if ((ega.seqregs[1] & 4) != 0)
                        ega.ma += 2;
                else
                        ega.ma += 4;

                ega.ma &= (uint32_t)ega.vrammask;

                int b = line + (x << 4) + offset;
                Buffer32[b + 14] = Buffer32[b + 15] = ega.pallook[ega.egapal[edat1 & 3]];
                Buffer32[b + 12] = Buffer32[b + 13] = ega.pallook[ega.egapal[(edat1 >> 2) & 3]];
                Buffer32[b + 10] = Buffer32[b + 11] = ega.pallook[ega.egapal[(edat1 >> 4) & 3]];
                Buffer32[b + 8] = Buffer32[b + 9] = ega.pallook[ega.egapal[(edat1 >> 6) & 3]];
                Buffer32[b + 6] = Buffer32[b + 7] = ega.pallook[ega.egapal[(edat0 >> 0) & 3]];
                Buffer32[b + 4] = Buffer32[b + 5] = ega.pallook[ega.egapal[(edat0 >> 2) & 3]];
                Buffer32[b + 2] = Buffer32[b + 3] = ega.pallook[ega.egapal[(edat0 >> 4) & 3]];
                Buffer32[b] = Buffer32[b + 1] = ega.pallook[ega.egapal[(edat0 >> 6) & 3]];
        }
    }

    private static uint8_t edat(int a, int b) => edatlookup[a, b];

    // pcem: vid_ega.c:386-458
    private static void ega_draw_4bpp_lowres(ega_t ega)
    {
        int x;
        int offset = ((8 - ega.scrollcache) << 1) + 16;
        int line = ega.displine * Stride;
        // pcem bug, reproduced: PB-89 — comme ega_draw_2bpp : jusqu'à 4 143, dans la ligne suivante.

        for (x = 0; x <= ega.hdisp; x++)
        {
                uint8_t e0, e1, e2, e3;
                uint8_t dat;
                uint32_t addr = ega.ma;
                int oddeven = 0;

                if ((ega.crtc[0x17] & 0x40) == 0)
                {
                        addr = (uint32_t)((addr << 1) & ega.vrammask);
                        if ((ega.seqregs[1] & 4) != 0)
                                oddeven = (addr & 4) != 0 ? 1 : 0;
                        addr &= ~7u;
                        if ((ega.crtc[0x17] & 0x20) != 0 && (ega.ma & 0x20000) != 0)
                                addr |= 4;
                        if ((ega.crtc[0x17] & 0x20) == 0 && (ega.ma & 0x8000) != 0)
                                addr |= 4;
                }
                if ((ega.crtc[0x17] & 0x01) == 0)
                        addr = (addr & ~0x8000u) | ((ega.sc & 1) != 0 ? 0x8000u : 0);
                if ((ega.crtc[0x17] & 0x02) == 0)
                        addr = (addr & ~0x10000u) | ((ega.sc & 2) != 0 ? 0x10000u : 0);

                if ((ega.seqregs[1] & 4) != 0)
                {
                        e0 = ega.vram[addr | (uint32_t)oddeven];
                        e2 = ega.vram[addr | (uint32_t)oddeven | 0x2];
                        e1 = e3 = 0;
                        ega.ma += 2;
                }
                else
                {
                        e0 = ega.vram[addr];
                        e1 = ega.vram[addr | 0x1];
                        e2 = ega.vram[addr | 0x2];
                        e3 = ega.vram[addr | 0x3];
                        ega.ma += 4;
                }
                ega.ma &= (uint32_t)ega.vrammask;

                int b = line + (x << 4) + offset;
                uint8_t m = ega.attrregs[0x12];
                dat = (uint8_t)(edat(e0 & 3, e1 & 3) | (edat(e2 & 3, e3 & 3) << 2));
                Buffer32[b + 14] = Buffer32[b + 15] = ega.pallook[ega.egapal[(dat & 0xf) & m]];
                Buffer32[b + 12] = Buffer32[b + 13] = ega.pallook[ega.egapal[(dat >> 4) & m]];
                dat = (uint8_t)(edat((e0 >> 2) & 3, (e1 >> 2) & 3) | (edat((e2 >> 2) & 3, (e3 >> 2) & 3) << 2));
                Buffer32[b + 10] = Buffer32[b + 11] = ega.pallook[ega.egapal[(dat & 0xf) & m]];
                Buffer32[b + 8] = Buffer32[b + 9] = ega.pallook[ega.egapal[(dat >> 4) & m]];
                dat = (uint8_t)(edat((e0 >> 4) & 3, (e1 >> 4) & 3) | (edat((e2 >> 4) & 3, (e3 >> 4) & 3) << 2));
                Buffer32[b + 6] = Buffer32[b + 7] = ega.pallook[ega.egapal[(dat & 0xf) & m]];
                Buffer32[b + 4] = Buffer32[b + 5] = ega.pallook[ega.egapal[(dat >> 4) & m]];
                dat = (uint8_t)(edat(e0 >> 6, e1 >> 6) | (edat(e2 >> 6, e3 >> 6) << 2));
                Buffer32[b + 2] = Buffer32[b + 3] = ega.pallook[ega.egapal[(dat & 0xf) & m]];
                Buffer32[b] = Buffer32[b + 1] = ega.pallook[ega.egapal[(dat >> 4) & m]];
        }
    }

    // pcem: vid_ega.c:460-524
    private static void ega_draw_4bpp_highres(ega_t ega)
    {
        int x;
        int offset = (8 - ega.scrollcache) + 24;
        int line = ega.displine * Stride;
        // pcem bug, reproduced: PB-89 — comme ega_draw_2bpp : jusqu'à 2 087, dans la ligne suivante.

        for (x = 0; x <= ega.hdisp; x++)
        {
                uint8_t e0, e1, e2, e3;
                uint8_t dat;
                uint32_t addr = ega.ma;
                int oddeven = 0;

                if ((ega.crtc[0x17] & 0x40) == 0)
                {
                        addr = (uint32_t)((addr << 1) & ega.vrammask);
                        if ((ega.seqregs[1] & 4) != 0)
                                oddeven = (addr & 4) != 0 ? 1 : 0;
                        addr &= ~7u;
                        if ((ega.crtc[0x17] & 0x20) != 0 && (ega.ma & 0x20000) != 0)
                                addr |= 4;
                        if ((ega.crtc[0x17] & 0x20) == 0 && (ega.ma & 0x8000) != 0)
                                addr |= 4;
                }
                if ((ega.crtc[0x17] & 0x01) == 0)
                        addr = (addr & ~0x8000u) | ((ega.sc & 1) != 0 ? 0x8000u : 0);
                if ((ega.crtc[0x17] & 0x02) == 0)
                        addr = (addr & ~0x10000u) | ((ega.sc & 2) != 0 ? 0x10000u : 0);

                if ((ega.seqregs[1] & 4) != 0)
                {
                        e0 = ega.vram[addr | (uint32_t)oddeven];
                        e2 = ega.vram[addr | (uint32_t)oddeven | 0x2];
                        e1 = e3 = 0;
                        ega.ma += 2;
                }
                else
                {
                        e0 = ega.vram[addr];
                        e1 = ega.vram[addr | 0x1];
                        e2 = ega.vram[addr | 0x2];
                        e3 = ega.vram[addr | 0x3];
                        ega.ma += 4;
                }
                ega.ma &= (uint32_t)ega.vrammask;

                int b = line + (x << 3) + offset;
                uint8_t m = ega.attrregs[0x12];
                dat = (uint8_t)(edat(e0 & 3, e1 & 3) | (edat(e2 & 3, e3 & 3) << 2));
                Buffer32[b + 7] = ega.pallook[ega.egapal[(dat & 0xf) & m]];
                Buffer32[b + 6] = ega.pallook[ega.egapal[(dat >> 4) & m]];
                dat = (uint8_t)(edat((e0 >> 2) & 3, (e1 >> 2) & 3) | (edat((e2 >> 2) & 3, (e3 >> 2) & 3) << 2));
                Buffer32[b + 5] = ega.pallook[ega.egapal[(dat & 0xf) & m]];
                Buffer32[b + 4] = ega.pallook[ega.egapal[(dat >> 4) & m]];
                dat = (uint8_t)(edat((e0 >> 4) & 3, (e1 >> 4) & 3) | (edat((e2 >> 4) & 3, (e3 >> 4) & 3) << 2));
                Buffer32[b + 3] = ega.pallook[ega.egapal[(dat & 0xf) & m]];
                Buffer32[b + 2] = ega.pallook[ega.egapal[(dat >> 4) & m]];
                dat = (uint8_t)(edat(e0 >> 6, e1 >> 6) | (edat(e2 >> 6, e3 >> 6) << 2));
                Buffer32[b + 1] = ega.pallook[ega.egapal[(dat & 0xf) & m]];
                Buffer32[b] = ega.pallook[ega.egapal[(dat >> 4) & m]];
        }
    }

    // pcem: vid_ega.c:526-698
    internal static void ega_poll(object? p)
    {
        ega_t ega = (ega_t)p;
        int x, xx;

        if (ega.linepos == 0)
        {
                timer_advance_u64(ega.timer, ega.dispofftime);

                ega.stat |= 1;
                ega.linepos = 1;

                if (ega.dispon != 0)
                {
                        if (ega.firstline == 2000)
                        {
                                ega.firstline = ega.displine;
                                video_wait_for_buffer();
                        }

                        if (ega.scrblank != 0)
                        {
                                int line = ega.displine * Stride;
                                // pcem bug, reproduced: PB-89 — (x * 18) + xx + 32 monte à 4 639 (:549-550) :
                                //   la ligne suivante de buffer32, jamais hors du tableau.
                                for (x = 0; x < ega.hdisp; x++)
                                {
                                        switch (ega.seqregs[1] & 9)
                                        {
                                        case 0:
                                                for (xx = 0; xx < 9; xx++)
                                                        Buffer32[line + (x * 9) + xx + 32] = 0;
                                                break;
                                        case 1:
                                                for (xx = 0; xx < 8; xx++)
                                                        Buffer32[line + (x * 8) + xx + 32] = 0;
                                                break;
                                        case 8:
                                                for (xx = 0; xx < 18; xx++)
                                                        Buffer32[line + (x * 18) + xx + 32] = 0;
                                                break;
                                        case 9:
                                                for (xx = 0; xx < 16; xx++)
                                                        Buffer32[line + (x * 16) + xx + 32] = 0;
                                                break;
                                        }
                                }
                        }
                        else if ((ega.gdcreg[6] & 1) == 0)
                        {
                                // pcem bug, reproduced: PB-99 — le texte n'est redessiné que si
                                //   fullchange (:559) : un changement de curseur, de police (SR3) ou de
                                //   l'attribut 10h ne se voit qu'au prochain rafraîchissement complet.
                                if (fullchange != 0)
                                        ega_draw_text(ega);
                        }
                        else
                        {
                                switch (ega.gdcreg[5] & 0x20)
                                {
                                case 0x00:
                                        if ((ega.seqregs[1] & 8) != 0)
                                                ega_draw_4bpp_lowres(ega);
                                        else
                                                ega_draw_4bpp_highres(ega);
                                        break;
                                case 0x20:
                                        ega_draw_2bpp(ega);
                                        break;
                                }
                        }
                        if (ega.lastline < ega.displine)
                                ega.lastline = ega.displine;
                }

                ega.displine++;
                if ((ega.stat & 8) != 0 && ((ega.displine & 15) == (ega.crtc[0x11] & 15)) && ega.vslines != 0)
                        ega.stat &= unchecked((uint8_t)~8);
                ega.vslines++;
                if (ega.displine > 500)
                        ega.displine = 0;
        }
        else
        {
                timer_advance_u64(ega.timer, ega.dispontime);
                //                if (output) printf("Display on %f\n",vidtime);
                if (ega.dispon != 0)
                        ega.stat &= unchecked((uint8_t)~1);
                ega.linepos = 0;
                if (ega.sc == (ega.crtc[11] & 31))
                        ega.con = 0;
                if (ega.dispon != 0)
                {
                        if (ega.sc == (ega.crtc[9] & 31))
                        {
                                ega.sc = 0;
                                if (ega.sc == (ega.crtc[11] & 31))
                                        ega.con = 0;

                                ega.maback += (uint32_t)(ega.rowoffset << 3);
                                ega.maback &= (uint32_t)ega.vrammask;
                                ega.ma = ega.maback;
                        }
                        else
                        {
                                ega.sc++;
                                ega.sc &= 31;
                                ega.ma = ega.maback;
                        }
                }
                ega.vc++;
                ega.vc &= 1023;
                //                printf("Line now %i %i ma %05X\n",vc,displine,ma);
                if (ega.vc == ega.split)
                {
                        //                        printf("Split at line %i %i\n",displine,vc);
                        ega.ma = ega.maback = 0;
                        // pcem bug, reproduced: PB-99 — l'attribut 10h bit 5 (:613) remet le défilement fin à 0
                        //   après la ligne de partage, comme la VGA ; sur l'EGA, 10h bits 4-7 sont « Not Used ».
                        if ((ega.attrregs[0x10] & 0x20) != 0)
                                ega.scrollcache = 0;
                }
                if (ega.vc == ega.dispend)
                {
                        //                        printf("Display over at line %i %i\n",displine,vc);
                        ega.dispon = 0;
                        // pcem bug, reproduced: PB-99 — CR0A bit 5 (:619) éteint le curseur, comme la VGA ; sur
                        //   l'EGA, 0Ah bits 5-7 sont « Not Used ».
                        if ((ega.crtc[10] & 0x20) != 0)
                                ega.cursoron = 0;
                        else
                                ega.cursoron = ega.blink & 16;
                        if ((ega.gdcreg[6] & 1) == 0 && (ega.blink & 15) == 0)
                                fullchange = 2;
                        ega.blink++;

                        if (fullchange != 0)
                                fullchange--;
                }
                if (ega.vc == ega.vsyncstart)
                {
                        ega.dispon = 0;
                        //                        printf("Vsync on at line %i %i\n",displine,vc);
                        ega.stat |= 8;
                        if ((ega.seqregs[1] & 8) != 0)
                                x = ega.hdisp * ((ega.seqregs[1] & 1) != 0 ? 8 : 9) * 2;
                        else
                                x = ega.hdisp * ((ega.seqregs[1] & 1) != 0 ? 8 : 9);
                        //                        pclog("Cursor %02X %02X\n",crtc[10],crtc[11]);
                        //                        pclog("Firstline %i Lastline %i wx %i %i\n",firstline,lastline,wx,oddeven);
                        //                        doblit();
                        if (x != xsize || (ega.lastline - ega.firstline) != ysize)
                        {
                                xsize = x;
                                ysize = ega.lastline - ega.firstline;
                                if (xsize < 64)
                                        xsize = 656;
                                if (ysize < 32)
                                        ysize = 200;
                                if (ega.vres != 0 || ysize <= 200)
                                        updatewindowsize(xsize, ysize << 1);
                                else
                                        updatewindowsize(xsize, ysize);
                        }

                        video_blit_memtoscreen(32, 0, ega.firstline, ega.lastline, xsize, ega.lastline - ega.firstline);

                        ega.frames++;
                        ega.video_res_x = xsize;
                        ega.video_res_y = ysize + 1;
                        if ((ega.gdcreg[6] & 1) == 0) /*Text mode*/
                        {
                                ega.video_res_x /= (ega.seqregs[1] & 1) != 0 ? 8 : 9;
                                ega.video_res_y /= (ega.crtc[9] & 31) + 1;
                                ega.video_bpp = 0;
                        }
                        else
                        {
                                if ((ega.crtc[9] & 0x80) != 0)
                                        ega.video_res_y /= 2;
                                if ((ega.crtc[0x17] & 1) == 0)
                                        ega.video_res_y *= 2;
                                ega.video_res_y /= (ega.crtc[9] & 31) + 1;
                                if ((ega.seqregs[1] & 8) != 0)
                                        ega.video_res_x /= 2;
                                ega.video_bpp = (ega.gdcreg[5] & 0x20) != 0 ? 2 : 4;
                        }

                        //                        wakeupblit();
                        pc.readflash = 0;
                        // framecount++;
                        ega.firstline = 2000;
                        ega.lastline = 0;

                        ega.maback = ega.ma = (uint32_t)((ega.crtc[0xc] << 8) | ega.crtc[0xd]);
                        ega.ca = (uint32_t)((ega.crtc[0xe] << 8) | ega.crtc[0xf]);
                        ega.ma <<= 2;
                        ega.maback <<= 2;
                        ega.ca <<= 2;
                        changeframecount = 2;
                        ega.vslines = 0;
                }
                if (ega.vc == ega.vtotal)
                {
                        ega.vc = 0;
                        ega.sc = ega.crtc[8] & 0x1f;
                        ega.dispon = 1;
                        ega.displine = 0;
                        ega.scrollcache = ega.attrregs[0x13] & 7;
                }
                if (ega.sc == (ega.crtc[10] & 31))
                        ega.con = 1;
        }
    }

    // pcem: vid_ega.c:700-886
    internal static void ega_write(uint32_t addr, uint8_t val, object? p)
    {
        ega_t ega = (ega_t)p;
        uint8_t vala, valb, valc, vald;
        int writemask2 = ega.writemask;

        egawrites++;
        cycles -= video_timing_write_b;
        pc.cycles_lost += video_timing_write_b;

        if (addr >= 0xB0000)
                addr &= 0x7fff;
        else
                addr &= 0xffff;

        if (ega.chain2_write != 0)
        {
                writemask2 &= ~0xa;
                if ((addr & 1) != 0)
                        writemask2 <<= 1;
                addr &= ~1u;
                if ((addr & 0x4000) != 0)
                        addr |= 1;
                addr &= ~0x4000u;
        }

        addr <<= 2;

        if (addr >= ega.vram_limit)
                return;

        if ((ega.gdcreg[6] & 1) == 0)
                fullchange = 2;

        //        pclog("%i %08X %i %i %02X   %02X %02X %02X
        //        %02X\n",chain4,addr,writemode,writemask,gdcreg[8],vram[0],vram[1],vram[2],vram[3]);
        uint8_t m8 = ega.gdcreg[8];
        switch (ega.writemode)
        {
        case 1:
                if ((writemask2 & 1) != 0)
                        ega.vram[addr] = ega.la;
                if ((writemask2 & 2) != 0)
                        ega.vram[addr | 0x1] = ega.lb;
                if ((writemask2 & 4) != 0)
                        ega.vram[addr | 0x2] = ega.lc;
                if ((writemask2 & 8) != 0)
                        ega.vram[addr | 0x3] = ega.ld;
                break;
        case 0:
                if ((ega.gdcreg[3] & 7) != 0)
                        val = ega_rotate[(ega.gdcreg[3] & 7) * 256 + val];

                if (ega.gdcreg[8] == 0xff && (ega.gdcreg[3] & 0x18) == 0 && ega.gdcreg[1] == 0)
                {
                        if ((writemask2 & 1) != 0)
                                ega.vram[addr] = val;
                        if ((writemask2 & 2) != 0)
                                ega.vram[addr | 0x1] = val;
                        if ((writemask2 & 4) != 0)
                                ega.vram[addr | 0x2] = val;
                        if ((writemask2 & 8) != 0)
                                ega.vram[addr | 0x3] = val;
                }
                else
                {
                        if ((ega.gdcreg[1] & 1) != 0)
                                vala = (uint8_t)((ega.gdcreg[0] & 1) != 0 ? 0xff : 0);
                        else
                                vala = val;
                        if ((ega.gdcreg[1] & 2) != 0)
                                valb = (uint8_t)((ega.gdcreg[0] & 2) != 0 ? 0xff : 0);
                        else
                                valb = val;
                        if ((ega.gdcreg[1] & 4) != 0)
                                valc = (uint8_t)((ega.gdcreg[0] & 4) != 0 ? 0xff : 0);
                        else
                                valc = val;
                        if ((ega.gdcreg[1] & 8) != 0)
                                vald = (uint8_t)((ega.gdcreg[0] & 8) != 0 ? 0xff : 0);
                        else
                                vald = val;
                        //                                pclog("Write %02X %01X %02X %02X %02X %02X
                        //                                %02X\n",gdcreg[3]&0x18,writemask,vala,valb,valc,vald,gdcreg[8]);
                        ega_logic(ega, addr, writemask2, vala, valb, valc, vald, m8);
                }
                break;
        case 2:
                if ((ega.gdcreg[3] & 0x18) == 0 && ega.gdcreg[1] == 0)
                {
                        if ((writemask2 & 1) != 0)
                                ega.vram[addr] = (uint8_t)((((val & 1) != 0 ? 0xff : 0) & m8) | (ega.la & ~m8));
                        if ((writemask2 & 2) != 0)
                                ega.vram[addr | 0x1] = (uint8_t)((((val & 2) != 0 ? 0xff : 0) & m8) | (ega.lb & ~m8));
                        if ((writemask2 & 4) != 0)
                                ega.vram[addr | 0x2] = (uint8_t)((((val & 4) != 0 ? 0xff : 0) & m8) | (ega.lc & ~m8));
                        if ((writemask2 & 8) != 0)
                                ega.vram[addr | 0x3] = (uint8_t)((((val & 8) != 0 ? 0xff : 0) & m8) | (ega.ld & ~m8));
                }
                else
                {
                        vala = (uint8_t)((val & 1) != 0 ? 0xff : 0);
                        valb = (uint8_t)((val & 2) != 0 ? 0xff : 0);
                        valc = (uint8_t)((val & 4) != 0 ? 0xff : 0);
                        vald = (uint8_t)((val & 8) != 0 ? 0xff : 0);
                        ega_logic(ega, addr, writemask2, vala, valb, valc, vald, m8);
                }
                break;
        }
    }

    // DEVIATION: les deux `switch (ega->gdcreg[3] & 0x18)` d'ega_write (:770-823 et :837-880),
    //   identiques au caractère près, en une fonction : Set, AND, OR, XOR sur les quatre plans.
    //   Même arithmétique, mêmes écritures, dans le même ordre.
    private static void ega_logic(ega_t ega, uint32_t addr, int writemask2, uint8_t vala, uint8_t valb, uint8_t valc,
                                  uint8_t vald, uint8_t m8)
    {
        switch (ega.gdcreg[3] & 0x18)
        {
        case 0: /*Set*/
                if ((writemask2 & 1) != 0)
                        ega.vram[addr] = (uint8_t)((vala & m8) | (ega.la & ~m8));
                if ((writemask2 & 2) != 0)
                        ega.vram[addr | 0x1] = (uint8_t)((valb & m8) | (ega.lb & ~m8));
                if ((writemask2 & 4) != 0)
                        ega.vram[addr | 0x2] = (uint8_t)((valc & m8) | (ega.lc & ~m8));
                if ((writemask2 & 8) != 0)
                        ega.vram[addr | 0x3] = (uint8_t)((vald & m8) | (ega.ld & ~m8));
                break;
        case 8: /*AND*/
                if ((writemask2 & 1) != 0)
                        ega.vram[addr] = (uint8_t)((vala | ~m8) & ega.la);
                if ((writemask2 & 2) != 0)
                        ega.vram[addr | 0x1] = (uint8_t)((valb | ~m8) & ega.lb);
                if ((writemask2 & 4) != 0)
                        ega.vram[addr | 0x2] = (uint8_t)((valc | ~m8) & ega.lc);
                if ((writemask2 & 8) != 0)
                        ega.vram[addr | 0x3] = (uint8_t)((vald | ~m8) & ega.ld);
                break;
        case 0x10: /*OR*/
                if ((writemask2 & 1) != 0)
                        ega.vram[addr] = (uint8_t)((vala & m8) | ega.la);
                if ((writemask2 & 2) != 0)
                        ega.vram[addr | 0x1] = (uint8_t)((valb & m8) | ega.lb);
                if ((writemask2 & 4) != 0)
                        ega.vram[addr | 0x2] = (uint8_t)((valc & m8) | ega.lc);
                if ((writemask2 & 8) != 0)
                        ega.vram[addr | 0x3] = (uint8_t)((vald & m8) | ega.ld);
                break;
        case 0x18: /*XOR*/
                if ((writemask2 & 1) != 0)
                        ega.vram[addr] = (uint8_t)((vala & m8) ^ ega.la);
                if ((writemask2 & 2) != 0)
                        ega.vram[addr | 0x1] = (uint8_t)((valb & m8) ^ ega.lb);
                if ((writemask2 & 4) != 0)
                        ega.vram[addr | 0x2] = (uint8_t)((valc & m8) ^ ega.lc);
                if ((writemask2 & 8) != 0)
                        ega.vram[addr | 0x3] = (uint8_t)((vald & m8) ^ ega.ld);
                break;
        }
    }

    // pcem: vid_ega.c:886-933
    internal static uint8_t ega_read(uint32_t addr, object? p)
    {
        ega_t ega = (ega_t)p;
        uint8_t temp, temp2, temp3, temp4;
        int readplane = ega.readplane;

        egareads++;
        cycles -= video_timing_read_b;
        pc.cycles_lost += video_timing_read_b;
        //        pclog("Readega %06X   ",addr);
        if (addr >= 0xb0000)
                addr &= 0x7fff;
        else
                addr &= 0xffff;

        if (ega.chain2_read != 0)
        {
                readplane = (readplane & 2) | (int)(addr & 1);
                addr &= ~1u;
                if ((addr & 0x4000) != 0)
                        addr |= 1;
                addr &= ~0x4000u;
        }

        addr <<= 2;
        if (addr >= ega.vram_limit)
                return 0xff;

        ega.la = ega.vram[addr];
        ega.lb = ega.vram[addr | 0x1];
        ega.lc = ega.vram[addr | 0x2];
        ega.ld = ega.vram[addr | 0x3];
        if (ega.readmode != 0)
        {
                temp = ega.la;
                temp ^= (uint8_t)((ega.colourcompare & 1) != 0 ? 0xff : 0);
                temp &= (uint8_t)((ega.colournocare & 1) != 0 ? 0xff : 0);
                temp2 = ega.lb;
                temp2 ^= (uint8_t)((ega.colourcompare & 2) != 0 ? 0xff : 0);
                temp2 &= (uint8_t)((ega.colournocare & 2) != 0 ? 0xff : 0);
                temp3 = ega.lc;
                temp3 ^= (uint8_t)((ega.colourcompare & 4) != 0 ? 0xff : 0);
                temp3 &= (uint8_t)((ega.colournocare & 4) != 0 ? 0xff : 0);
                temp4 = ega.ld;
                temp4 ^= (uint8_t)((ega.colourcompare & 8) != 0 ? 0xff : 0);
                temp4 &= (uint8_t)((ega.colournocare & 8) != 0 ? 0xff : 0);
                return (uint8_t)~(temp | temp2 | temp3 | temp4);
        }
        return ega.vram[addr | (uint32_t)readplane];
    }

    private static void pal(int c, int r, int g, int b) => pallook64[c] = pallook16[c] = makecol32(r, g, b);

    // pcem: vid_ega.c:935-1033
    internal static void ega_init(ega_t ega, int monitor_type, int is_mono)
    {
        int c, d, e;

        // DEVIATION: `malloc(0x40000)` (:937) sans effacement — du tas. Un tableau nul ici ;
        //   l'oracle efface la sienne à l'amorçage (h_ega_attach), PLAN-G9.md n° 4.
        ega.vram = new uint8_t[0x40000];
        ega.vrammask = 0x3ffff;

        for (c = 0; c < 256; c++)
        {
                e = c;
                for (d = 0; d < 8; d++)
                {
                        ega_rotate[d * 256 + c] = (uint8_t)e;
                        e = (e >> 1) | ((e & 1) != 0 ? 0x80 : 0);
                }
        }

        for (c = 0; c < 4; c++)
        {
                for (d = 0; d < 4; d++)
                {
                        edatlookup[c, d] = 0;
                        if ((c & 1) != 0)
                                edatlookup[c, d] |= 1;
                        if ((d & 1) != 0)
                                edatlookup[c, d] |= 2;
                        if ((c & 2) != 0)
                                edatlookup[c, d] |= 0x10;
                        if ((d & 2) != 0)
                                edatlookup[c, d] |= 0x20;
                }
        }

        if (is_mono != 0)
        {
                for (c = 0; c < 256; c++)
                {
                        switch (monitor_type >> 4)
                        {
                        case DISPLAY_GREEN:
                                switch ((c >> 3) & 3)
                                {
                                case 0: pal(c, 0, 0, 0); break;
                                case 2: pal(c, 0x04, 0x8a, 0x20); break;
                                case 1: pal(c, 0x08, 0xc7, 0x2c); break;
                                case 3: pal(c, 0x34, 0xff, 0x5d); break;
                                }
                                break;
                        case DISPLAY_AMBER:
                                switch ((c >> 3) & 3)
                                {
                                case 0: pal(c, 0, 0, 0); break;
                                case 2: pal(c, 0xb2, 0x4d, 0x00); break;
                                case 1: pal(c, 0xef, 0x79, 0x00); break;
                                case 3: pal(c, 0xff, 0xe3, 0x34); break;
                                }
                                break;
                        case DISPLAY_WHITE:
                        default:
                                switch ((c >> 3) & 3)
                                {
                                case 0: pal(c, 0, 0, 0); break;
                                case 2: pal(c, 0x7a, 0x81, 0x83); break;
                                case 1: pal(c, 0xaf, 0xb3, 0xb0); break;
                                case 3: pal(c, 0xff, 0xfd, 0xed); break;
                                }
                                break;
                        }
                }
        }
        else
        {
                for (c = 0; c < 256; c++)
                {
                        pallook64[c] = makecol32(((c >> 2) & 1) * 0xaa, ((c >> 1) & 1) * 0xaa, (c & 1) * 0xaa);
                        pallook64[c] += makecol32(((c >> 5) & 1) * 0x55, ((c >> 4) & 1) * 0x55, ((c >> 3) & 1) * 0x55);
                        pallook16[c] = makecol32(((c >> 2) & 1) * 0xaa, ((c >> 1) & 1) * 0xaa, (c & 1) * 0xaa);
                        pallook16[c] += makecol32(((c >> 4) & 1) * 0x55, ((c >> 4) & 1) * 0x55, ((c >> 4) & 1) * 0x55);
                        if ((c & 0x17) == 6)
                                pallook16[c] = makecol32(0xaa, 0x55, 0);
                }
        }
        ega.pallook = pallook16;

        egaswitches = monitor_type & 0xf;
        egaswitchread = 0;   // DEVIATION: voir la déclaration (PLAN-G9.md, décision n° 4).
        ega.vram_limit = 256 * 1024;
        ega.vrammask = (int)(ega.vram_limit - 1);

        timer_add(ega.timer, ega_poll, ega, 1);
    }

    // pcem: vid_ega.c:1035-1064
    internal static object ega_standalone_init()
    {
        ega_t ega = new ega_t();
        // pcem: :1036-1037 — malloc + memset ; `new` zéro-initialise.
        int monitor_type;

        rom_init(ega.bios_rom, "ibm_6277356_ega_card_u44_27128.bin", 0xc0000, 0x8000, 0x7fff, 0, MEM_MAPPING_EXTERNAL);

        if (ega.bios_rom.rom[0x3ffe] == 0xaa && ega.bios_rom.rom[0x3fff] == 0x55)
        {
                int c;
                // omitted: pclog("Read EGA ROM in reverse\n") (:1044) — sortie pure.

                for (c = 0; c < 0x2000; c++)
                {
                        uint8_t temp = ega.bios_rom.rom[c];
                        ega.bios_rom.rom[c] = ega.bios_rom.rom[0x3fff - c];
                        ega.bios_rom.rom[0x3fff - c] = temp;
                }
        }

        monitor_type = device_get_config_int("monitor_type");
        ega_init(ega, monitor_type, (monitor_type & 0xf) == 10 ? 1 : 0);

        // pcem bug, reproduced: PB-99 — le rendu est borné par vrammask, mais les substitutions de rangée
        //   de CR17 s'appliquent après (:344-347, :405-408, :477-480) : avec 64 Ko, le bit 0x10000 (CR17
        //   bit 1 = 0) lit des octets qu'une vraie carte de 64 Ko n'a pas. La police du texte, lue sans
        //   masque (:287), sort de même par les tables de SR3 qui dépassent la mémoire.
        ega.vram_limit = (uint32_t)(device_get_config_int("memory") * 1024);
        ega.vrammask = (int)(ega.vram_limit - 1);

        mem_mapping_add(ega.mapping, 0xa0000, 0x20000, ega_read, null, null, ega_write, null, null, null, 0,
                        MEM_MAPPING_EXTERNAL, ega);
        io_sethandler(0x03a0, 0x0040, ega_in, null, null, ega_out, null, null, ega);

        Probe = ega;
        return ega;
    }

    // pcem: vid_ega.c:1066
    private static int ega_standalone_available() { return rom_present("ibm_6277356_ega_card_u44_27128.bin"); }

    // pcem: vid_ega.c:1068-1073
    internal static void ega_close(object? p)
    {
        // pcem bug, reproduced: PB-98 — ni mem_mapping_remove ni rom_deinit : la projection et la ROM
        //   restent dans la liste de mem.c jusqu'au mem_alloc suivant, comme en C.
        // omitted: free(ega->vram), free(ega) (:1071-1072) — sous GC.
        Probe = null;
    }

    // pcem: vid_ega.c:1075-1079
    internal static void ega_speed_changed(object? p)
    {
        ega_t ega = (ega_t)p;

        ega_recalctimings(ega);
    }

    // pcem: vid_ega.c:1081-1097
    internal static void ega_add_status_info(StringBuilder s, int max_len, object? p)
    {
        ega_t ega = (ega_t)p;

        if (ega.video_bpp == 0)
                s.Append("EGA in text mode\n");
        else
                s.Append($"EGA colour depth : {ega.video_bpp} bpp\n");

        s.Append($"EGA resolution : {ega.video_res_x} x {ega.video_res_y}\n");

        s.Append($"EGA refresh rate : {ega.frames} Hz\n\n");
        ega.frames = 0;
    }

    // pcem: vid_ega.c:1099-1119. omitted: `.description`.
    internal static device_config_t[] ega_config =
    [
        new device_config_t { name = "memory", type = CONFIG_SELECTION, default_int = 256,
            selection = [new() { description = "64 kB", value = 64 }, new() { description = "128 kB", value = 128 }, new() { description = "256 kB", value = 256 }] },
        new device_config_t { name = "monitor_type", type = CONFIG_SELECTION, default_int = 9,
            selection = [new() { description = "EGA Colour, 40x25", value = 6 }, new() { description = "EGA Colour, 80x25", value = 7 },
                         new() { description = "EGA Colour, ECD", value = 9 },
                         new() { description = "EGA Monochrome (white)", value = 10 | (DISPLAY_WHITE << 4) },
                         new() { description = "EGA Monochrome (green)", value = 10 | (DISPLAY_GREEN << 4) },
                         new() { description = "EGA Monochrome (amber)", value = 10 | (DISPLAY_AMBER << 4) }] },
        new device_config_t { type = -1 },
    ];

    // pcem: vid_ega.c:1121-1123
    internal static device_t ega_device = new device_t("EGA", 0, ega_standalone_init, ega_close, ega_standalone_available,
                                                       ega_speed_changed, null, ega_add_status_info, ega_config);

    // iXtal26 (outillage, sans pendant C) — G9.2 : la carte montée, pour la sonde (h_ega_probe).
    internal static ega_t? Probe;
}
