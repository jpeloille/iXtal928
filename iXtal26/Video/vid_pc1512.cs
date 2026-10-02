// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/video/vid_pc1512.c + includes/private/video/vid_pc1512.h
// STATUS: transcribed — pc1512_t, pc1512_out/pc1512_in, pc1512_write/pc1512_read,
//         pc1512_recalctimings, pc1512_poll, pc1512_init, pc1512_close,
//         pc1512_speed_changed, pc1512_config, pc1512_device. Le fichier entier.
//         Omis : les pclog et les blocs commentés, sortie pure. PB-89 reproduit.

/*PC1512 CGA emulation

  The PC1512 extends CGA with a bit-planar 640x200x16 mode.

  Most CRTC registers are fixed.

  The Technical Reference Manual lists the video waitstate time as between 12
  and 46 cycles. PCem currently always uses the lower number.*/

// CS8600/CS8602/CS8604 : `pc1512_t pc1512 = (pc1512_t)p;` — même raison qu'à
// vid_cga.cs:9-16 : les delegates déclarent `object?`, pc1512_init enregistre
// toujours `pc1512`.
#pragma warning disable CS8600, CS8602, CS8604

using iXtal26.Memory;
using iXtal26.Models;
using iXtal26.PluginApi;
using static iXtal26.Cpu.x86;
using static iXtal26.Memory.mem;
using static iXtal26.PluginApi.device;
using static iXtal26.Video.video;
using static iXtal26.io;
using static iXtal26.timer;

namespace iXtal26.Video;

// pcem: vid_pc1512.c:18-43
// Classe et non struct : son adresse est prise et stockée (timer_add,
// mem_mapping_add, io_sethandler la reçoivent tous comme `void *p`).
internal sealed class pc1512_t
{
    internal mem_mapping_t mapping = new();

    internal uint8_t[] crtc = new uint8_t[32];
    internal int crtcreg;

    internal uint8_t cgacol, cgamode, stat;

    internal uint8_t plane_write, plane_read, border;

    internal int fontbase;
    internal int linepos, displine;
    internal int sc, vc;
    internal int cgadispon;
    internal int con, coff, cursoron, cgablink;
    internal int vsynctime, vadj;
    internal uint16_t ma, maback;
    internal int dispon;
    internal int blink;

    internal uint64_t dispontime, dispofftime;
    internal pc_timer_t timer = new();
    internal int firstline, lastline;

    internal uint8_t[] vram = [];
}

internal static partial class vid_pc1512
{
    // pcem: vid_pc1512.c:45-46
    private static uint8_t[] crtcmask = new uint8_t[32] {0xff, 0xff, 0xff, 0xff, 0x7f, 0x1f, 0x7f, 0x7f, 0xf3, 0x1f, 0x7f, 0x1f, 0x3f, 0xff, 0x3f, 0xff,
                                                         0xff, 0xff, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00};

    // pcem: vid_pc1512.c:50-94
    internal static void pc1512_out(uint16_t addr, uint8_t val, object p)
    {
        pc1512_t pc1512 = (pc1512_t)p;
        uint8_t old;
        switch (addr)
        {
        case 0x3d0:
        case 0x3d2:
        case 0x3d4:
        case 0x3d6:
                pc1512.crtcreg = val & 31;
                return;
        case 0x3d1:
        case 0x3d3:
        case 0x3d5:
        case 0x3d7:
                old = pc1512.crtc[pc1512.crtcreg];
                pc1512.crtc[pc1512.crtcreg] = (uint8_t)(val & crtcmask[pc1512.crtcreg]);
                // Reproduit : `old` (masqué) est comparé à `val` (non masqué).
                if (old != val)
                {
                        if (pc1512.crtcreg < 0xe || pc1512.crtcreg > 0x10)
                        {
                                fullchange = changeframecount;
                                pc1512_recalctimings(pc1512);
                        }
                }
                return;
        case 0x3d8:
                if ((val & 0x12) == 0x12 && (pc1512.cgamode & 0x12) != 0x12)
                {
                        pc1512.plane_write = 0xf;
                        pc1512.plane_read = 0;
                }
                pc1512.cgamode = val;
                return;
        case 0x3d9:
                pc1512.cgacol = val;
                return;
        case 0x3dd:
                pc1512.plane_write = val;
                return;
        case 0x3de:
                pc1512.plane_read = (uint8_t)(val & 3);
                return;
        case 0x3df:
                pc1512.border = val;
                return;
        }
    }

    // pcem: vid_pc1512.c:96-109
    internal static uint8_t pc1512_in(uint16_t addr, object p)
    {
        pc1512_t pc1512 = (pc1512_t)p;
        switch (addr)
        {
        case 0x3d4:
                return (uint8_t)pc1512.crtcreg;
        case 0x3d5:
                return pc1512.crtc[pc1512.crtcreg];
        case 0x3da:
                pc1512.stat ^= 0x01; /*Bit 0 is toggle bit on PC1512*/
                return (uint8_t)(pc1512.stat ^ 0x01);
        }
        return 0xff;
    }

    // pcem: vid_pc1512.c:111-129
    // addr & 0x3fff, | 0xc000 au plus : toujours dans les 0x10000 octets de vram.
    internal static void pc1512_write(uint32_t addr, uint8_t val, object? p)
    {
        pc1512_t pc1512 = (pc1512_t)p;

        egawrites++;
        cycles -= 12;
        addr &= 0x3fff;

        if ((pc1512.cgamode & 0x12) == 0x12)
        {
                if ((pc1512.plane_write & 1) != 0)
                        pc1512.vram[addr] = val;
                if ((pc1512.plane_write & 2) != 0)
                        pc1512.vram[addr | 0x4000] = val;
                if ((pc1512.plane_write & 4) != 0)
                        pc1512.vram[addr | 0x8000] = val;
                if ((pc1512.plane_write & 8) != 0)
                        pc1512.vram[addr | 0xc000] = val;
        }
        else
                pc1512.vram[addr] = val;
    }

    // pcem: vid_pc1512.c:131-141
    // plane_read est masqué à 3 (pc1512_out, 0x3de) : l'index reste sous 0x10000.
    internal static uint8_t pc1512_read(uint32_t addr, object? p)
    {
        pc1512_t pc1512 = (pc1512_t)p;

        egareads++;
        cycles -= 12;
        addr &= 0x3fff;

        if ((pc1512.cgamode & 0x12) == 0x12)
                return pc1512.vram[addr | (uint32_t)(pc1512.plane_read << 14)];
        return pc1512.vram[addr];
    }

    // pcem: vid_pc1512.c:143-155
    // Deux constantes positives : aucune conversion d'un double négatif (pas de PB-36 ici).
    internal static void pc1512_recalctimings(pc1512_t pc1512)
    {
        double _dispontime, _dispofftime, disptime;
        disptime = 114; /*Fixed on PC1512*/
        _dispontime = 80;
        _dispofftime = disptime - _dispontime;
        _dispontime *= pit.CGACONST;
        _dispofftime *= pit.CGACONST;
        pc1512.dispontime = (uint64_t)_dispontime;
        pc1512.dispofftime = (uint64_t)_dispofftime;
    }

    // pcem: vid_pc1512.c:157-432
    internal static void pc1512_poll(object? p)
    {
        pc1512_t pc1512 = (pc1512_t)p;
        uint16_t ca = (uint16_t)((pc1512.crtc[15] | (pc1512.crtc[14] << 8)) & 0x3fff);
        int drawcursor;
        int x, c;
        uint8_t chr, attr;
        uint16_t dat, dat2, dat3, dat4;
        // `int cols[4]` en C : les valeurs de cgapal (uint32_t) y passent sans perte de bits,
        // et le XOR 0xffffff comme l'écriture dans buffer32 rendent les mêmes 32 bits.
        Span<uint32_t> cols = stackalloc uint32_t[4];
        int col;
        int oldsc;
        int i;
        uint32_t v;

        if (pc1512.linepos == 0)
        {
                timer_advance_u64(pc1512.timer, pc1512.dispofftime);
                pc1512.linepos = 1;
                oldsc = pc1512.sc;
                if (pc1512.dispon != 0)
                {
                        if (pc1512.displine < pc1512.firstline)
                        {
                                pc1512.firstline = pc1512.displine;
                                video_wait_for_buffer();
                        }
                        pc1512.lastline = pc1512.displine;
                        for (c = 0; c < 8; c++)
                        {
                                // pcem bug, reproduced: PB-89 (comme la M24) — crtc[1] n'est pas masqué
                                //   (crtcmask[1] = 0xff) ; l'abscisse c + (crtc[1] << 4) + 8 monte à 4 095
                                //   et déborde sur la ligne suivante de buffer32 (lignes contiguës,
                                //   wx-sdl2-video.c:59-69, ici le même tableau plat). Jamais hors du
                                //   tableau : displine < 360 (vid_pc1512.c:326-327), donc l'index plat
                                //   reste sous 359 * 2048 + 4 095 < 2048 * 2048.
                                if ((pc1512.cgamode & 0x12) == 0x12)
                                {
                                        Buffer32[pc1512.displine * Stride + c] = cgapal[pc1512.border & 15];
                                        if ((pc1512.cgamode & 1) != 0)
                                        {
                                                i = pc1512.displine * Stride + c + (pc1512.crtc[1] << 3) + 8;
                                                Buffer32[i] = 0;
                                        }
                                        else
                                        {
                                                i = pc1512.displine * Stride + c + (pc1512.crtc[1] << 4) + 8;
                                                Buffer32[i] = 0;
                                        }
                                }
                                else
                                {
                                        Buffer32[pc1512.displine * Stride + c] = cgapal[pc1512.cgacol & 15];
                                        if ((pc1512.cgamode & 1) != 0)
                                        {
                                                i = pc1512.displine * Stride + c + (pc1512.crtc[1] << 3) + 8;
                                                Buffer32[i] = cgapal[pc1512.cgacol & 15];
                                        }
                                        else
                                        {
                                                i = pc1512.displine * Stride + c + (pc1512.crtc[1] << 4) + 8;
                                                Buffer32[i] = cgapal[pc1512.cgacol & 15];
                                        }
                                }
                        }
                        if ((pc1512.cgamode & 1) != 0)
                        {
                                for (x = 0; x < 80; x++)
                                {
                                        chr = pc1512.vram[((pc1512.ma << 1) & 0x3fff)];
                                        attr = pc1512.vram[(((pc1512.ma << 1) + 1) & 0x3fff)];
                                        drawcursor = ((pc1512.ma == ca) && pc1512.con != 0 && pc1512.cursoron != 0) ? 1 : 0;
                                        if ((pc1512.cgamode & 0x20) != 0)
                                        {
                                                cols[1] = cgapal[attr & 15];
                                                cols[0] = cgapal[(attr >> 4) & 7];
                                                if ((pc1512.blink & 16) != 0 && (attr & 0x80) != 0 && drawcursor == 0)
                                                        cols[1] = cols[0];
                                        }
                                        else
                                        {
                                                cols[1] = cgapal[attr & 15];
                                                cols[0] = cgapal[attr >> 4];
                                        }
                                        // fontbase <= 768, chr <= 255 : caractère < 1 024, dans fontdat.
                                        if (drawcursor != 0)
                                        {
                                                for (c = 0; c < 8; c++)
                                                        Buffer32[pc1512.displine * Stride + (x << 3) + c + 8] =
                                                                cols[(fontdat[((chr + pc1512.fontbase) << 3) | (pc1512.sc & 7)] &
                                                                      (1 << (c ^ 7))) != 0
                                                                             ? 1
                                                                             : 0] ^
                                                                0xffffffu;
                                        }
                                        else
                                        {
                                                for (c = 0; c < 8; c++)
                                                        Buffer32[pc1512.displine * Stride + (x << 3) + c + 8] =
                                                                cols[(fontdat[((chr + pc1512.fontbase) << 3) | (pc1512.sc & 7)] &
                                                                      (1 << (c ^ 7))) != 0
                                                                             ? 1
                                                                             : 0];
                                        }
                                        pc1512.ma++;
                                }
                        }
                        else if ((pc1512.cgamode & 2) == 0)
                        {
                                for (x = 0; x < 40; x++)
                                {
                                        chr = pc1512.vram[((pc1512.ma << 1) & 0x3fff)];
                                        attr = pc1512.vram[(((pc1512.ma << 1) + 1) & 0x3fff)];
                                        drawcursor = ((pc1512.ma == ca) && pc1512.con != 0 && pc1512.cursoron != 0) ? 1 : 0;
                                        if ((pc1512.cgamode & 0x20) != 0)
                                        {
                                                cols[1] = cgapal[attr & 15];
                                                cols[0] = cgapal[(attr >> 4) & 7];
                                                if ((pc1512.blink & 16) != 0 && (attr & 0x80) != 0)
                                                        cols[1] = cols[0];
                                        }
                                        else
                                        {
                                                cols[1] = cgapal[attr & 15];
                                                cols[0] = cgapal[attr >> 4];
                                        }
                                        pc1512.ma++;
                                        if (drawcursor != 0)
                                        {
                                                for (c = 0; c < 8; c++)
                                                {
                                                        v = cols[(fontdat[((chr + pc1512.fontbase) << 3) | (pc1512.sc & 7)] &
                                                                  (1 << (c ^ 7))) != 0
                                                                         ? 1
                                                                         : 0] ^
                                                            0xffffffu;
                                                        i = pc1512.displine * Stride + (x << 4) + (c << 1) + 1 + 8;
                                                        Buffer32[i] = v;
                                                        i = pc1512.displine * Stride + (x << 4) + (c << 1) + 8;
                                                        Buffer32[i] = v;
                                                }
                                        }
                                        else
                                        {
                                                for (c = 0; c < 8; c++)
                                                {
                                                        v = cols[(fontdat[((chr + pc1512.fontbase) << 3) | (pc1512.sc & 7)] &
                                                                  (1 << (c ^ 7))) != 0
                                                                         ? 1
                                                                         : 0];
                                                        i = pc1512.displine * Stride + (x << 4) + (c << 1) + 1 + 8;
                                                        Buffer32[i] = v;
                                                        i = pc1512.displine * Stride + (x << 4) + (c << 1) + 8;
                                                        Buffer32[i] = v;
                                                }
                                        }
                                }
                        }
                        else if ((pc1512.cgamode & 16) == 0)
                        {
                                cols[0] = cgapal[pc1512.cgacol & 15];
                                col = ((pc1512.cgacol & 16) != 0) ? 8 : 0;
                                if ((pc1512.cgamode & 4) != 0)
                                {
                                        cols[1] = cgapal[col | 3];
                                        cols[2] = cgapal[col | 4];
                                        cols[3] = cgapal[col | 7];
                                }
                                else if ((pc1512.cgacol & 32) != 0)
                                {
                                        cols[1] = cgapal[col | 3];
                                        cols[2] = cgapal[col | 5];
                                        cols[3] = cgapal[col | 7];
                                }
                                else
                                {
                                        cols[1] = cgapal[col | 2];
                                        cols[2] = cgapal[col | 4];
                                        cols[3] = cgapal[col | 6];
                                }
                                for (x = 0; x < 40; x++)
                                {
                                        dat = (uint16_t)((pc1512.vram[((pc1512.ma << 1) & 0x1fff) + ((pc1512.sc & 1) * 0x2000)] << 8) |
                                                         pc1512.vram[((pc1512.ma << 1) & 0x1fff) + ((pc1512.sc & 1) * 0x2000) + 1]);
                                        pc1512.ma++;
                                        for (c = 0; c < 8; c++)
                                        {
                                                v = cols[dat >> 14];
                                                i = pc1512.displine * Stride + (x << 4) + (c << 1) + 1 + 8;
                                                Buffer32[i] = v;
                                                i = pc1512.displine * Stride + (x << 4) + (c << 1) + 8;
                                                Buffer32[i] = v;
                                                dat <<= 2;
                                        }
                                }
                        }
                        else
                        {
                                for (x = 0; x < 40; x++)
                                {
                                        // (ma << 1) & 0x1fff est pair, donc ca <= 0x3ffe et ca + 0xc001 <= 0xffff :
                                        // toujours dans les 0x10000 octets de vram.
                                        ca = (uint16_t)(((pc1512.ma << 1) & 0x1fff) + ((pc1512.sc & 1) * 0x2000));
                                        dat = (uint16_t)((pc1512.vram[ca] << 8) | pc1512.vram[ca + 1]);
                                        dat2 = (uint16_t)((pc1512.vram[ca + 0x4000] << 8) | pc1512.vram[ca + 0x4001]);
                                        dat3 = (uint16_t)((pc1512.vram[ca + 0x8000] << 8) | pc1512.vram[ca + 0x8001]);
                                        dat4 = (uint16_t)((pc1512.vram[ca + 0xc000] << 8) | pc1512.vram[ca + 0xc001]);

                                        pc1512.ma++;
                                        for (c = 0; c < 16; c++)
                                        {
                                                Buffer32[pc1512.displine * Stride + (x << 4) + c + 8] =
                                                        cgapal[((dat >> 15) | ((dat2 >> 15) << 1) | ((dat3 >> 15) << 2) |
                                                                ((dat4 >> 15) << 3)) &
                                                               (pc1512.cgacol & 15)];
                                                dat <<= 1;
                                                dat2 <<= 1;
                                                dat3 <<= 1;
                                                dat4 <<= 1;
                                        }
                                }
                        }
                }
                else
                {
                        cols[0] = cgapal[((pc1512.cgamode & 0x12) == 0x12) ? 0 : (pc1512.cgacol & 15)];
                        // pcem bug, reproduced: PB-89 (comme la M24) — x2 = (crtc[1] << 4) + 16 monte à
                        //   4 096 : hline déborde sur la ligne suivante, jamais hors du tableau
                        //   (displine < 360).
                        if ((pc1512.cgamode & 1) != 0)
                                hline(0, pc1512.displine, (pc1512.crtc[1] << 3) + 16, (int)cols[0]);
                        else
                                hline(0, pc1512.displine, (pc1512.crtc[1] << 4) + 16, (int)cols[0]);
                }

                pc1512.sc = oldsc;
                if (pc1512.vsynctime != 0)
                        pc1512.stat |= 8;
                pc1512.displine++;
                if (pc1512.displine >= 360)
                        pc1512.displine = 0;
                // omitted: pclog("Line %i %i %i %i  %i %i\n", …) (vid_pc1512.c:328), commenté chez PCem.
        }
        else
        {
                timer_advance_u64(pc1512.timer, pc1512.dispontime);
                if ((pc1512.lastline - pc1512.firstline) == 199)
                        pc1512.dispon = 0; /*Amstrad PC1512 always displays 200 lines, regardless of CRTC settings*/
                pc1512.linepos = 0;
                if (pc1512.vsynctime != 0)
                {
                        pc1512.vsynctime--;
                        if (pc1512.vsynctime == 0)
                                pc1512.stat &= unchecked((uint8_t)~8);
                }
                if (pc1512.sc == (pc1512.crtc[11] & 31))
                {
                        pc1512.con = 0;
                        pc1512.coff = 1;
                }
                if (pc1512.vadj != 0)
                {
                        pc1512.sc++;
                        pc1512.sc &= 31;
                        pc1512.ma = pc1512.maback;
                        pc1512.vadj--;
                        if (pc1512.vadj == 0)
                        {
                                pc1512.dispon = 1;
                                pc1512.ma = pc1512.maback = (uint16_t)((pc1512.crtc[13] | (pc1512.crtc[12] << 8)) & 0x3fff);
                                pc1512.sc = 0;
                        }
                }
                else if (pc1512.sc == pc1512.crtc[9])
                {
                        pc1512.maback = pc1512.ma;
                        pc1512.sc = 0;
                        pc1512.vc++;
                        pc1512.vc &= 127;

                        if (pc1512.displine == 32) // oldvc == (cgamode & 2) ? 127 : 31)
                        {
                                pc1512.vc = 0;
                                pc1512.vadj = 6;
                                if ((pc1512.crtc[10] & 0x60) == 0x20)
                                        pc1512.cursoron = 0;
                                else
                                        pc1512.cursoron = pc1512.blink & 16;
                        }

                        if (pc1512.displine >= 262) // vc == (cgamode & 2) ? 111 : 27)
                        {
                                pc1512.dispon = 0;
                                pc1512.displine = 0;
                                pc1512.vsynctime = 46;

                                // Reproduit : x calculé puis écrasé par 640 + 16 (vid_pc1512.c:375-379).
                                if ((pc1512.cgamode & 1) != 0)
                                        x = (pc1512.crtc[1] << 3) + 16;
                                else
                                        x = (pc1512.crtc[1] << 4) + 16;
                                x = 640 + 16;
                                pc1512.lastline++;

                                if (x != xsize || (pc1512.lastline - pc1512.firstline) != ysize)
                                {
                                        xsize = x;
                                        ysize = pc1512.lastline - pc1512.firstline;
                                        if (xsize < 64)
                                                xsize = 656;
                                        if (ysize < 32)
                                                ysize = 200;
                                        updatewindowsize(xsize, (ysize << 1) + 16);
                                }

                                video_blit_memtoscreen(0, pc1512.firstline - 4, 0, (pc1512.lastline - pc1512.firstline) + 8,
                                                       xsize, (pc1512.lastline - pc1512.firstline) + 8);
                                // omitted: vid_pc1512.c:394-401 — blit/stretch_blit/readflash, commentés chez PCem.

                                video_res_x = xsize - 16;
                                video_res_y = ysize;
                                // crtc[9] est masqué à 0x1f : le diviseur vaut 1 à 32, jamais 0.
                                if ((pc1512.cgamode & 1) != 0)
                                {
                                        video_res_x /= 8;
                                        video_res_y /= pc1512.crtc[9] + 1;
                                        video_bpp = 0;
                                }
                                else if ((pc1512.cgamode & 2) == 0)
                                {
                                        video_res_x /= 16;
                                        video_res_y /= pc1512.crtc[9] + 1;
                                        video_bpp = 0;
                                }
                                else if ((pc1512.cgamode & 16) == 0)
                                {
                                        video_res_x /= 2;
                                        video_bpp = 2;
                                }
                                else
                                {
                                        video_bpp = 4;
                                }

                                pc1512.firstline = 1000;
                                pc1512.lastline = 0;
                                pc1512.blink++;
                        }
                }
                else
                {
                        pc1512.sc++;
                        pc1512.sc &= 31;
                        pc1512.ma = pc1512.maback;
                }
                if (pc1512.sc == (pc1512.crtc[10] & 31))
                        pc1512.con = 1;
        }
    }

    // pcem: vid_pc1512.c:434-451
    // G1.2 — le PC1512 monté, pour la sonde de fin de boot-diff (vid_svga.Probe) ; pendant de
    // h_pc1512 côté oracle (harness_pc1512.c). Pas un état de PCem.
    internal static pc1512_t? pc1512_pri;

    internal static object pc1512_init()
    {
        int display_type;
        pc1512_t pc1512 = new pc1512_t();
        pc1512_pri = pc1512;
        // pcem: vid_pc1512.c:437 — memset(pc1512, 0, sizeof(pc1512_t)) ; `new` zéro-initialise.

        // Déviation de l'ORACLE : malloc(0x10000) sans memset (vid_pc1512.c:439) ;
        //   le tableau C# naît à zéro, et l'oracle memset cette VRAM pour s'y aligner.
        pc1512.vram = new uint8_t[0x10000];

        pc1512.cgacol = 7;
        pc1512.cgamode = 0x12;
        pc1512.fontbase = (device_get_config_int("codepage") & 3) * 256;
        display_type = device_get_config_int("display_type");

        timer_add(pc1512.timer, pc1512_poll, pc1512, 1);
        mem_mapping_add(pc1512.mapping, 0xb8000, 0x08000, pc1512_read, null, null, pc1512_write, null, null, null, 0, 0, pc1512);
        io_sethandler(0x03d0, 0x0010, pc1512_in, null, null, pc1512_out, null, null, pc1512);
        cgapal_rebuild(display_type, 0);
        return pc1512;
    }

    // pcem: vid_pc1512.c:453-458
    internal static void pc1512_close(object? p)
    {
        if (pc1512_pri == p)
                pc1512_pri = null;
        // omitted: free(pc1512->vram) et free(pc1512) — libération manuelle, sans objet sous GC.
    }

    // pcem: vid_pc1512.c:460-464
    internal static void pc1512_speed_changed(object? p)
    {
        pc1512_t pc1512 = (pc1512_t)p;

        pc1512_recalctimings(pc1512);
    }

    // pcem: vid_pc1512.c:466-480
    // omitted: `.description` (vid_pc1512.c:467-478) ; `.selection` est transcrit (G8.3,
    //   device_get_config_int y valide la valeur du .cfg). display_type n'a pas de .default_int
    //   chez PCem : 0, c'est-à-dire DISPLAY_RGB (PC-CM) ; l'autre choix est DISPLAY_WHITE
    //   (PC-MM). codepage : 3 US English (défaut), 1 Danish, 0 Greek.
    internal static device_config_t[] pc1512_config =
    [
        new device_config_t { name = "display_type", type = CONFIG_SELECTION, default_int = 0,
            selection = [new() { description = "PC-CM (Colour)", value = DISPLAY_RGB }, new() { description = "PC-MM (Monochrome)", value = DISPLAY_WHITE }] },
        new device_config_t { name = "codepage", type = CONFIG_SELECTION, default_int = 3,
            selection = [new() { description = "US English", value = 3 }, new() { description = "Danish", value = 1 }, new() { description = "Greek", value = 0 }] },
        new device_config_t { type = -1 },
    ];

    // pcem: vid_pc1512.c:482-483
    internal static device_t pc1512_device = new device_t("Amstrad PC1512 (video)", 0, pc1512_init, pc1512_close, null,
                                                          pc1512_speed_changed, null, null, pc1512_config);
}
