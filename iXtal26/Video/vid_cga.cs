// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/video/vid_cga.c + includes/private/video/vid_cga.h
// STATUS: partial — cga_t, cga_out/cga_in, cga_write/cga_read (neige comprise),
//         cga_recalctimings, cga_poll, cga_init, cga_standalone_init, cga_close,
//         cga_speed_changed, cga_config, cga_device. Omis : le chemin composite.

// CS8600/CS8602/CS8604 : `cga_t cga = (cga_t)p;` — le `void *p` des handlers est
// déclaré `object?` par les delegates de mem.cs, timer.cs et device.cs, parce que
// le C y passe NULL tant que le périphérique n'a pas d'état privé. Ici il ne l'est
// jamais : cga_standalone_init enregistre `cga` à chaque timer_add /
// mem_mapping_add / io_sethandler. L'analyse de nullabilité de C# ne peut pas le
// savoir, et `cga` reste « peut-être nul » pour tout le corps. Mêmes trois codes
// qu'à device.cs:19, neutralisés ici plutôt qu'avec un `!` par site, qui
// réécrirait chaque ligne du dessin.
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

// pcem: vid_cga.h:3-36
// Classe et non struct : son adresse est prise et stockée (timer_add,
// mem_mapping_add, io_sethandler la reçoivent tous comme `void *p`).
internal sealed class cga_t
{
    internal mem_mapping_t mapping = new();

    internal int crtcreg;
    internal uint8_t[] crtc = new uint8_t[32];

    internal uint8_t cgastat;

    internal uint8_t cgamode, cgacol;

    internal int fontbase;
    internal int linepos, displine;
    internal int sc, vc;
    internal int cgadispon;
    internal int con, coff, cursoron, cgablink;
    internal int vsynctime, vadj;
    internal uint16_t ma, maback;
    internal int oddeven;

    internal uint64_t dispontime, dispofftime;
    internal pc_timer_t timer = new();

    internal int firstline, lastline;

    internal int drawcursor;

    internal uint8_t[] vram = [];

    internal uint8_t[] charbuffer = new uint8_t[256];

    internal int revision;
    internal int composite;
    internal int snow_enabled;
}

/*CGA emulation*/
internal static partial class vid_cga
{
    // pcem: vid_cga.c:13-14
    private const int COMPOSITE_OLD = 0;
    private const int COMPOSITE_NEW = 1;

    // pcem: vid_cga.c:16-17
    private static uint8_t[] crtcmask = new uint8_t[32] {0xff, 0xff, 0xff, 0xff, 0x7f, 0x1f, 0x7f, 0x7f, 0xf3, 0x1f, 0x7f, 0x1f, 0x3f, 0xff, 0x3f, 0xff,
                                                         0xff, 0xff, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00};

    // pcem: vid_cga.c:21-56
    internal static void cga_out(uint16_t addr, uint8_t val, object p)
    {
        cga_t cga = (cga_t)p;
        uint8_t old;
        switch (addr)
        {
        case 0x3d0:
        case 0x3d2:
        case 0x3d4:
        case 0x3d6:
                cga.crtcreg = val & 31;
                return;
        case 0x3d1:
        case 0x3d3:
        case 0x3d5:
        case 0x3d7:
                old = cga.crtc[cga.crtcreg];
                cga.crtc[cga.crtcreg] = (uint8_t)(val & crtcmask[cga.crtcreg]);
                if (old != val)
                {
                        if (cga.crtcreg < 0xe || cga.crtcreg > 0x10)
                        {
                                fullchange = changeframecount;
                                cga_recalctimings(cga);
                        }
                }
                return;
        case 0x3D8:
                if (((cga.cgamode ^ val) & 5) != 0)
                {
                        cga.cgamode = val;
                        // omitted: update_cga16_color(cga->cgamode) (vid_cga.c:48) —
                        //   dosbox/vid_cga_comp.c, chemin composite.
                }
                cga.cgamode = val;
                return;
        case 0x3D9:
                cga.cgacol = val;
                return;
        }
    }

    // pcem: vid_cga.c:58-70
    internal static uint8_t cga_in(uint16_t addr, object p)
    {
        cga_t cga = (cga_t)p;
        switch (addr)
        {
        case 0x3D4:
                return (uint8_t)cga.crtcreg;
        case 0x3D5:
                return cga.crtc[cga.crtcreg];
        case 0x3DA:
                return cga.cgastat;
        }
        return 0xFF;
    }

    // pcem: vid_cga.c:72-83
    internal static void cga_write(uint32_t addr, uint8_t val, object? p)
    {
        cga_t cga = (cga_t)p;
        cga.vram[addr & 0x3fff] = val;
        if (cga.snow_enabled != 0)
        {
                int offset = (int)(((timer_get_remaining_u64(cga.timer) / pit.CGACONST) * 2) & 0xfc);
                cga.charbuffer[offset] = cga.vram[addr & 0x3fff];
                cga.charbuffer[offset | 1] = cga.vram[addr & 0x3fff];
        }
        egawrites++;
        cycles -= 4;
    }

    // pcem: vid_cga.c:85-96
    internal static uint8_t cga_read(uint32_t addr, object? p)
    {
        cga_t cga = (cga_t)p;
        cycles -= 4;
        if (cga.snow_enabled != 0)
        {
                int offset = (int)(((timer_get_remaining_u64(cga.timer) / pit.CGACONST) * 2) & 0xfc);
                cga.charbuffer[offset] = cga.vram[addr & 0x3fff];
                cga.charbuffer[offset | 1] = cga.vram[addr & 0x3fff];
        }
        egareads++;
        return cga.vram[addr & 0x3fff];
    }

    // pcem: vid_cga.c:98-117
    internal static void cga_recalctimings(cga_t cga)
    {
        double disptime;
        double _dispontime, _dispofftime;
        // omitted: pclog("Recalc - %i %i %i\n", …) (vid_cga.c:101) — sortie pure.
        if ((cga.cgamode & 1) != 0)
        {
                disptime = cga.crtc[0] + 1;
                _dispontime = cga.crtc[1];
        }
        else
        {
                disptime = (cga.crtc[0] + 1) << 1;
                _dispontime = cga.crtc[1] << 1;
        }
        _dispofftime = disptime - _dispontime;
        _dispontime *= pit.CGACONST;
        _dispofftime *= pit.CGACONST;
        cga.dispontime = (uint64_t)_dispontime;
        cga.dispofftime = (uint64_t)_dispofftime;
    }

    // pcem: vid_cga.c:119-409
    internal static void cga_poll(object? p)
    {
        cga_t cga = (cga_t)p;
        uint16_t ca = (uint16_t)((cga.crtc[15] | (cga.crtc[14] << 8)) & 0x3fff);
        int drawcursor;
        int x, c;
        int oldvc;
        uint8_t chr, attr;
        uint16_t dat;
        uint32_t[] cols = new uint32_t[4];
        int col;
        int oldsc;

        if (cga.linepos == 0)
        {
                timer_advance_u64(cga.timer, cga.dispofftime);
                cga.cgastat |= 1;
                cga.linepos = 1;
                oldsc = cga.sc;
                if ((cga.crtc[8] & 3) == 3)
                        cga.sc = ((cga.sc << 1) + cga.oddeven) & 7;
                if (cga.cgadispon != 0)
                {
                        if (cga.displine < cga.firstline)
                        {
                                cga.firstline = cga.displine;
                                video_wait_for_buffer();
                        }
                        cga.lastline = cga.displine;

                        cols[0] = (uint32_t)(((cga.cgamode & 0x12) == 0x12) ? 0 : (cga.cgacol & 15));
                        for (c = 0; c < 8; c++)
                        {
                                Buffer32[cga.displine * Stride + c] = cols[0];
                                if ((cga.cgamode & 1) != 0)
                                        Buffer32[cga.displine * Stride + c + (cga.crtc[1] << 3) + 8] = cols[0];
                                else
                                        Buffer32[cga.displine * Stride + c + (cga.crtc[1] << 4) + 8] = cols[0];
                        }
                        if ((cga.cgamode & 1) != 0)
                        {
                                for (x = 0; x < cga.crtc[1]; x++)
                                {
                                        if ((cga.cgamode & 8) != 0)
                                        {
                                                chr = cga.charbuffer[x << 1];
                                                attr = cga.charbuffer[(x << 1) + 1];
                                        }
                                        else
                                                chr = attr = 0;
                                        drawcursor = ((cga.ma == ca) && cga.con != 0 && cga.cursoron != 0) ? 1 : 0;
                                        if ((cga.cgamode & 0x20) != 0)
                                        {
                                                cols[1] = (uint32_t)(attr & 15);
                                                cols[0] = (uint32_t)((attr >> 4) & 7);
                                                // pcem bug, reproduced: `!cga->drawcursor` (vid_cga.c:165 et
                                                //   :198) lit le CHAMP cga_t.drawcursor, que rien n'écrit —
                                                //   donc toujours 0 — là où la locale `drawcursor` était visée.
                                                if ((cga.cgablink & 8) != 0 && (attr & 0x80) != 0 && cga.drawcursor == 0)
                                                        cols[1] = cols[0];
                                        }
                                        else
                                        {
                                                cols[1] = (uint32_t)(attr & 15);
                                                cols[0] = (uint32_t)(attr >> 4);
                                        }
                                        if (drawcursor != 0)
                                        {
                                                for (c = 0; c < 8; c++)
                                                        Buffer32[cga.displine * Stride + (x << 3) + c + 8] =
                                                                cols[(fontdat[chr + cga.fontbase, cga.sc & 7] & (1 << (c ^ 7))) != 0
                                                                             ? 1
                                                                             : 0] ^
                                                                0xffffffu;
                                        }
                                        else
                                        {
                                                for (c = 0; c < 8; c++)
                                                        Buffer32[cga.displine * Stride + (x << 3) + c + 8] =
                                                                cols[(fontdat[chr + cga.fontbase, cga.sc & 7] & (1 << (c ^ 7))) != 0
                                                                             ? 1
                                                                             : 0];
                                        }
                                        cga.ma++;
                                }
                        }
                        else if ((cga.cgamode & 2) == 0)
                        {
                                for (x = 0; x < cga.crtc[1]; x++)
                                {
                                        if ((cga.cgamode & 8) != 0)
                                        {
                                                chr = cga.vram[((cga.ma << 1) & 0x3fff)];
                                                attr = cga.vram[(((cga.ma << 1) + 1) & 0x3fff)];
                                        }
                                        else
                                                chr = attr = 0;
                                        drawcursor = ((cga.ma == ca) && cga.con != 0 && cga.cursoron != 0) ? 1 : 0;
                                        if ((cga.cgamode & 0x20) != 0)
                                        {
                                                cols[1] = (uint32_t)(attr & 15);
                                                cols[0] = (uint32_t)((attr >> 4) & 7);
                                                if ((cga.cgablink & 8) != 0 && (attr & 0x80) != 0 && cga.drawcursor == 0)
                                                        cols[1] = cols[0];
                                        }
                                        else
                                        {
                                                cols[1] = (uint32_t)(attr & 15);
                                                cols[0] = (uint32_t)(attr >> 4);
                                        }
                                        cga.ma++;
                                        if (drawcursor != 0)
                                        {
                                                for (c = 0; c < 8; c++)
                                                        Buffer32[cga.displine * Stride + (x << 4) + (c << 1) + 8] =
                                                                Buffer32[cga.displine * Stride + (x << 4) + (c << 1) + 1 + 8] =
                                                                        cols[(fontdat[chr + cga.fontbase, cga.sc & 7] &
                                                                              (1 << (c ^ 7))) != 0
                                                                                     ? 1
                                                                                     : 0] ^
                                                                        0xffffffu;
                                        }
                                        else
                                        {
                                                for (c = 0; c < 8; c++)
                                                        Buffer32[cga.displine * Stride + (x << 4) + (c << 1) + 8] =
                                                                Buffer32[cga.displine * Stride + (x << 4) + (c << 1) + 1 + 8] =
                                                                        cols[(fontdat[chr + cga.fontbase, cga.sc & 7] &
                                                                              (1 << (c ^ 7))) != 0
                                                                                     ? 1
                                                                                     : 0];
                                        }
                                }
                        }
                        else if ((cga.cgamode & 16) == 0)
                        {
                                cols[0] = (uint32_t)(cga.cgacol & 15);
                                col = ((cga.cgacol & 16) != 0) ? 8 : 0;
                                if ((cga.cgamode & 4) != 0)
                                {
                                        cols[1] = (uint32_t)(col | 3);
                                        cols[2] = (uint32_t)(col | 4);
                                        cols[3] = (uint32_t)(col | 7);
                                }
                                else if ((cga.cgacol & 32) != 0)
                                {
                                        cols[1] = (uint32_t)(col | 3);
                                        cols[2] = (uint32_t)(col | 5);
                                        cols[3] = (uint32_t)(col | 7);
                                }
                                else
                                {
                                        cols[1] = (uint32_t)(col | 2);
                                        cols[2] = (uint32_t)(col | 4);
                                        cols[3] = (uint32_t)(col | 6);
                                }
                                for (x = 0; x < cga.crtc[1]; x++)
                                {
                                        if ((cga.cgamode & 8) != 0)
                                                dat = (uint16_t)((cga.vram[((cga.ma << 1) & 0x1fff) + ((cga.sc & 1) * 0x2000)] << 8) |
                                                                 cga.vram[((cga.ma << 1) & 0x1fff) + ((cga.sc & 1) * 0x2000) + 1]);
                                        else
                                                dat = 0;
                                        cga.ma++;
                                        for (c = 0; c < 8; c++)
                                        {
                                                Buffer32[cga.displine * Stride + (x << 4) + (c << 1) + 8] =
                                                        Buffer32[cga.displine * Stride + (x << 4) + (c << 1) + 1 + 8] =
                                                                cols[dat >> 14];
                                                dat <<= 2;
                                        }
                                }
                        }
                        else
                        {
                                cols[0] = 0;
                                cols[1] = (uint32_t)(cga.cgacol & 15);
                                for (x = 0; x < cga.crtc[1]; x++)
                                {
                                        if ((cga.cgamode & 8) != 0)
                                                dat = (uint16_t)((cga.vram[((cga.ma << 1) & 0x1fff) + ((cga.sc & 1) * 0x2000)] << 8) |
                                                                 cga.vram[((cga.ma << 1) & 0x1fff) + ((cga.sc & 1) * 0x2000) + 1]);
                                        else
                                                dat = 0;
                                        cga.ma++;
                                        for (c = 0; c < 16; c++)
                                        {
                                                Buffer32[cga.displine * Stride + (x << 4) + c + 8] = cols[dat >> 15];
                                                dat <<= 1;
                                        }
                                }
                        }
                }
                else
                {
                        cols[0] = (uint32_t)(((cga.cgamode & 0x12) == 0x12) ? 0 : (cga.cgacol & 15));
                        if ((cga.cgamode & 1) != 0)
                                hline(0, cga.displine, (cga.crtc[1] << 3) + 16, (int)cols[0]);
                        else
                                hline(0, cga.displine, (cga.crtc[1] << 4) + 16, (int)cols[0]);
                }

                if ((cga.cgamode & 1) != 0)
                        x = (cga.crtc[1] << 3) + 16;
                else
                        x = (cga.crtc[1] << 4) + 16;

                // omitted: la branche `if (cga->composite)` (vid_cga.c:285-289) — le
                //   repliement sur 4 bits puis Composite_Process de
                //   dosbox/vid_cga_comp.c. Elle contient de plus l'écriture octet via
                //   lecture dword de la ligne 287.
                for (c = 0; c < x; c++)
                        Buffer32[cga.displine * Stride + c] =
                                cgapal[Buffer32[cga.displine * Stride + c] & 0xf];

                cga.sc = oldsc;
                if (cga.vc == cga.crtc[7] && cga.sc == 0)
                        cga.cgastat |= 8;
                cga.displine++;
                if (cga.displine >= 360)
                        cga.displine = 0;
        }
        else
        {
                timer_advance_u64(cga.timer, cga.dispontime);
                cga.linepos = 0;
                if (cga.vsynctime != 0)
                {
                        cga.vsynctime--;
                        if (cga.vsynctime == 0)
                                cga.cgastat &= unchecked((uint8_t)~8);
                }
                if (cga.sc == (cga.crtc[11] & 31) || ((cga.crtc[8] & 3) == 3 && cga.sc == ((cga.crtc[11] & 31) >> 1)))
                {
                        cga.con = 0;
                        cga.coff = 1;
                }
                if ((cga.crtc[8] & 3) == 3 && cga.sc == (cga.crtc[9] >> 1))
                        cga.maback = cga.ma;
                if (cga.vadj != 0)
                {
                        cga.sc++;
                        cga.sc &= 31;
                        cga.ma = cga.maback;
                        cga.vadj--;
                        if (cga.vadj == 0)
                        {
                                cga.cgadispon = 1;
                                cga.ma = cga.maback = (uint16_t)((cga.crtc[13] | (cga.crtc[12] << 8)) & 0x3fff);
                                cga.sc = 0;
                        }
                }
                else if (cga.sc == cga.crtc[9])
                {
                        cga.maback = cga.ma;
                        cga.sc = 0;
                        oldvc = cga.vc;
                        cga.vc++;
                        cga.vc &= 127;

                        if (cga.vc == cga.crtc[6])
                                cga.cgadispon = 0;

                        if (oldvc == cga.crtc[4])
                        {
                                cga.vc = 0;
                                cga.vadj = cga.crtc[5];
                                if (cga.vadj == 0)
                                        cga.cgadispon = 1;
                                if (cga.vadj == 0)
                                        cga.ma = cga.maback = (uint16_t)((cga.crtc[13] | (cga.crtc[12] << 8)) & 0x3fff);
                                if ((cga.crtc[10] & 0x60) == 0x20)
                                        cga.cursoron = 0;
                                else
                                        cga.cursoron = cga.cgablink & 8;
                        }

                        if (cga.vc == cga.crtc[7])
                        {
                                cga.cgadispon = 0;
                                cga.displine = 0;
                                cga.vsynctime = 16;
                                if (cga.crtc[7] != 0)
                                {
                                        if ((cga.cgamode & 1) != 0)
                                                x = (cga.crtc[1] << 3) + 16;
                                        else
                                                x = (cga.crtc[1] << 4) + 16;
                                        cga.lastline++;
                                        if (x != xsize || (cga.lastline - cga.firstline) != ysize)
                                        {
                                                xsize = x;
                                                ysize = cga.lastline - cga.firstline;
                                                if (xsize < 64)
                                                        xsize = 656;
                                                if (ysize < 32)
                                                        ysize = 200;
                                                updatewindowsize(xsize, (ysize << 1) + 16);
                                        }

                                        video_blit_memtoscreen(0, cga.firstline - 4, 0, (cga.lastline - cga.firstline) + 8,
                                                               xsize, (cga.lastline - cga.firstline) + 8);
                                        frames++;

                                        video_res_x = xsize - 16;
                                        video_res_y = ysize;
                                        if ((cga.cgamode & 1) != 0)
                                        {
                                                video_res_x /= 8;
                                                video_res_y /= cga.crtc[9] + 1;
                                                video_bpp = 0;
                                        }
                                        else if ((cga.cgamode & 2) == 0)
                                        {
                                                video_res_x /= 16;
                                                video_res_y /= cga.crtc[9] + 1;
                                                video_bpp = 0;
                                        }
                                        else if ((cga.cgamode & 16) == 0)
                                        {
                                                video_res_x /= 2;
                                                video_bpp = 2;
                                        }
                                        else
                                        {
                                                video_bpp = 1;
                                        }
                                }
                                cga.firstline = 1000;
                                cga.lastline = 0;
                                cga.cgablink++;
                                cga.oddeven ^= 1;
                        }
                }
                else
                {
                        cga.sc++;
                        cga.sc &= 31;
                        cga.ma = cga.maback;
                }
                if (cga.cgadispon != 0)
                        cga.cgastat &= unchecked((uint8_t)~1);
                if ((cga.sc == (cga.crtc[10] & 31) || ((cga.crtc[8] & 3) == 3 && cga.sc == ((cga.crtc[10] & 31) >> 1))))
                        cga.con = 1;
                // pcem bug, reproduced: charbuffer fait 256 octets (vid_cga.h:31) et la
                //   borne `cga->crtc[1] << 1` que crtcmask[1] = 0xff laisse monter à 510
                //   vaut ici (vid_cga.c:405) comme à la relecture 80 colonnes
                //   (vid_cga.c:157-158). Au-delà de crtc[1] = 128 le C déborde en silence
                //   sur les champs voisins de cga_t ; le C# lève. Le BIOS du 5150 pose
                //   40 ou 80.
                if (cga.cgadispon != 0 && (cga.cgamode & 1) != 0)
                {
                        for (x = 0; x < (cga.crtc[1] << 1); x++)
                                cga.charbuffer[x] = cga.vram[(((cga.ma << 1) + x) & 0x3fff)];
                }
        }
    }

    // pcem: vid_cga.c:411-414
    internal static void cga_init(cga_t cga)
    {
        timer_add(cga.timer, cga_poll, cga, 1);
        cga.composite = 0;
    }

    // pcem: vid_cga.c:416-439
    internal static object cga_standalone_init()
    {
        int display_type, contrast;
        cga_t cga = new cga_t();
        // pcem: vid_cga.c:419 — memset(cga, 0, sizeof(cga_t)) ; `new` zéro-initialise.

        display_type = device_get_config_int("display_type");
        cga.composite = (display_type == DISPLAY_COMPOSITE) ? 1 : 0;
        cga.revision = device_get_config_int("composite_type");
        cga.snow_enabled = device_get_config_int("snow_enabled");
        contrast = device_get_config_int("contrast");

        cga.vram = new uint8_t[0x4000];

        // omitted: cga_comp_init(cga->revision) (vid_cga.c:429) —
        //   dosbox/vid_cga_comp.c, chemin composite.

        timer_add(cga.timer, cga_poll, cga, 1);
        mem_mapping_add(cga.mapping, 0xb8000, 0x08000, cga_read, null, null, cga_write, null, null, null, 0,
                        MEM_MAPPING_EXTERNAL, cga);
        io_sethandler(0x03d0, 0x0010, cga_in, null, null, cga_out, null, null, cga);

        cgapal_rebuild(display_type, contrast);

        return cga;
    }

    // pcem: vid_cga.c:441-446
    internal static void cga_close(object? p)
    {
        cga_t cga = (cga_t)p;

        // omitted: free(cga->vram) et free(cga) (vid_cga.c:444-445) — libération
        //   manuelle, sans objet sous GC.
    }

    // pcem: vid_cga.c:448-452
    internal static void cga_speed_changed(object? p)
    {
        cga_t cga = (cga_t)p;

        cga_recalctimings(cga);
    }

    // pcem: vid_cga.c:454-475
    // omitted: `.description` et `.selection` de chaque entrée (vid_cga.c:456-472) —
    //   device_config_t (device.cs:42-48) ne porte plus ces champs, le 5150 n'ayant
    //   pas de dialogue de configuration. Les quatre valeurs que
    //   device_get_config_int rend restent celles de PCem : display_type =
    //   DISPLAY_RGB, composite_type = COMPOSITE_OLD, snow_enabled = 1, contrast = 0.
    internal static device_config_t[] cga_config =
    [
        new device_config_t { name = "display_type", type = CONFIG_SELECTION, default_int = DISPLAY_RGB },
        new device_config_t { name = "composite_type", type = CONFIG_SELECTION, default_int = COMPOSITE_OLD },
        new device_config_t { name = "snow_enabled", type = CONFIG_BINARY, default_int = 1 },
        new device_config_t { name = "contrast", type = CONFIG_BINARY, default_int = 0 },
        new device_config_t { type = -1 },
    ];

    // pcem: vid_cga.c:477
    internal static device_t cga_device = new device_t("CGA", 0, cga_standalone_init, cga_close, null, cga_speed_changed, null,
                                                       null, cga_config);
}
