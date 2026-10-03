// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/video/vid_mda.c + includes/private/video/vid_mda.h
// STATUS: transcribed — mdacols, mda_out, mda_in, mda_write, mda_read, mda_recalctimings,
//         mda_poll, mda_standalone_init, mda_init, mda_setcol, mda_close, mda_speed_changed,
//         mda_config, mda_device.
//
// G9.0 (PLAN-G9.md). La MDA d'IBM : un 6845 aux ports 3B0-3BF, 4 Ko de VRAM vus en B0000 sur
// 32 Ko, texte 80×25 en caractères de 9 points, police `fontdatm` (mda.rom, video.cs).

// CS8600/CS8602/CS8604 : `(mda_t)p` part du `object?` des delegates de mem.cs, io.cs et timer.cs,
// comme à vid_cga.cs.
#pragma warning disable CS8600, CS8602, CS8604

using iXtal26.Memory;
using iXtal26.Models;
using iXtal26.PluginApi;
using static iXtal26.Memory.mem;
using static iXtal26.PluginApi.device;
using static iXtal26.Video.video;
using static iXtal26.io;
using static iXtal26.timer;

namespace iXtal26.Video;

// pcem: vid_mda.h:4-25
// Classe et non struct : son adresse est prise (timer_add, mem_mapping_add, io_sethandler).
internal sealed class mda_t
{
    internal mem_mapping_t mapping = new();

    internal uint8_t[] crtc = new uint8_t[32];
    internal int crtcreg;

    internal uint8_t ctrl, stat;

    internal uint64_t dispontime, dispofftime;
    internal pc_timer_t timer = new();

    internal int firstline, lastline;

    internal int linepos, displine;
    internal int vc, sc;
    internal uint16_t ma, maback;
    internal int con, coff, cursoron;
    internal int dispon, blink;
    internal int vsynctime, vadj;

    internal uint8_t[] vram = [];
}

internal static class vid_mda
{
    // pcem: vid_mda.c:11 — `static uint32_t mdacols[256][2][2]`, à plat : [(attr * 2 + blink) * 2 + fg].
    private static readonly uint32_t[] mdacols = new uint32_t[256 * 2 * 2];

    private static ref uint32_t mdacol(int attr, int blink, int fg) => ref mdacols[(attr * 2 + blink) * 2 + fg];

    // pcem bug, reproduced: PB-96 — `fontdatm[chr][sc]` (vid_mda.c:119, :122) avec `sc` jusqu'à 31
    //   (`&= 31`, :160, :223) dans un `fontdatm[2048][16]` (video.c:921) : la lecture tombe dans
    //   les lignes du caractère suivant, jamais hors du tableau. fontdatm est `[2048, 16]` en C# ;
    //   l'accès se fait donc à plat, comme l'adresse que le C calcule.
    internal static uint8_t fontdatm_plat(int chr, int sc)
    {
        int i = chr * 16 + sc;
        return fontdatm[i >> 4, i & 15];
    }

    // pcem: vid_mda.c:15-41
    internal static void mda_out(uint16_t addr, uint8_t val, object? p)
    {
        mda_t mda = (mda_t)p;
        switch (addr)
        {
        case 0x3b0:
        case 0x3b2:
        case 0x3b4:
        case 0x3b6:
                mda.crtcreg = val & 31;
                return;
        case 0x3b1:
        case 0x3b3:
        case 0x3b5:
        case 0x3b7:
                // pcem bug, reproduced: PB-97 — le 6845 de la MDA n'a pas de masques : chaque
                //   registre s'écrit en entier et se relit (:55), là où le vrai rend R0-R13 en
                //   écriture seule ; et le correctif du « Generic Turbo XT » (:29-34) réécrit
                //   R10/R11 = 6/7 en B/C.
                mda.crtc[mda.crtcreg] = val;
                if (mda.crtc[10] == 6 &&
                    mda.crtc[11] == 7) /*Fix for Generic Turbo XT BIOS, which sets up cursor registers wrong*/
                {
                        mda.crtc[10] = 0xb;
                        mda.crtc[11] = 0xc;
                }
                mda_recalctimings(mda);
                return;
        case 0x3b8:
                mda.ctrl = val;
                return;
        }
    }

    // pcem: vid_mda.c:43-60
    internal static uint8_t mda_in(uint16_t addr, object? p)
    {
        mda_t mda = (mda_t)p;
        switch (addr)
        {
        case 0x3b0:
        case 0x3b2:
        case 0x3b4:
        case 0x3b6:
                return (uint8_t)mda.crtcreg;
        case 0x3b1:
        case 0x3b3:
        case 0x3b5:
        case 0x3b7:
                return mda.crtc[mda.crtcreg];
        case 0x3ba:
                return (uint8_t)(mda.stat | 0xF0);
        }
        return 0xff;
    }

    // pcem: vid_mda.c:62-66
    internal static void mda_write(uint32_t addr, uint8_t val, object? p)
    {
        mda_t mda = (mda_t)p;
        egawrites++;
        mda.vram[addr & 0xfff] = val;
    }

    // pcem: vid_mda.c:68-72
    internal static uint8_t mda_read(uint32_t addr, object? p)
    {
        mda_t mda = (mda_t)p;
        egareads++;
        return mda.vram[addr & 0xfff];
    }

    // pcem: vid_mda.c:74-83
    internal static void mda_recalctimings(mda_t mda)
    {
        double _dispontime, _dispofftime, disptime;
        disptime = mda.crtc[0] + 1;
        _dispontime = mda.crtc[1];
        _dispofftime = disptime - _dispontime;
        _dispontime *= pit.MDACONST;
        _dispofftime *= pit.MDACONST;
        // pcem bug, reproduced: PB-36 — même conversion d'un double négatif qu'à
        //   cga_recalctimings, dès que R1 dépasse R0 + 1 (G9.0, PB-36 élargi).
        mda.dispontime = unchecked((uint64_t)(int64_t)_dispontime);
        mda.dispofftime = unchecked((uint64_t)(int64_t)_dispofftime);
    }

    // pcem: vid_mda.c:85-231
    internal static void mda_poll(object? p)
    {
        mda_t mda = (mda_t)p;
        uint16_t ca = (uint16_t)((mda.crtc[15] | (mda.crtc[14] << 8)) & 0x3fff);
        int drawcursor;
        int x, c;
        int oldvc;
        uint8_t chr, attr;
        int oldsc;
        int blink;
        if (mda.linepos == 0)
        {
                timer_advance_u64(mda.timer, mda.dispofftime);
                mda.stat |= 1;
                mda.linepos = 1;
                oldsc = mda.sc;
                // pcem bug, reproduced: PB-97 — en entrelacé, `(sc << 1) & 7` perd les lignes 8 et
                //   au-delà (:99-100).
                if ((mda.crtc[8] & 3) == 3)
                        mda.sc = (mda.sc << 1) & 7;
                if (mda.dispon != 0)
                {
                        if (mda.displine < mda.firstline)
                        {
                                mda.firstline = mda.displine;
                                video_wait_for_buffer();
                        }
                        mda.lastline = mda.displine;
                        int line = mda.displine * Stride;
                        for (x = 0; x < mda.crtc[1]; x++)
                        {
                                chr = mda.vram[(mda.ma << 1) & 0xfff];
                                attr = mda.vram[((mda.ma << 1) + 1) & 0xfff];
                                drawcursor = ((mda.ma == ca) && mda.con != 0 && mda.cursoron != 0) ? 1 : 0;
                                blink = ((mda.blink & 16) != 0 && (mda.ctrl & 0x20) != 0 && (attr & 0x80) != 0 && drawcursor == 0) ? 1 : 0;
                                if (mda.sc == 12 && ((attr & 7) == 1))
                                {
                                        for (c = 0; c < 9; c++)
                                                Buffer32[line + (x * 9) + c] = mdacol(attr, blink, 1);
                                }
                                else
                                {
                                        for (c = 0; c < 8; c++)
                                                Buffer32[line + (x * 9) + c] =
                                                        mdacol(attr, blink, (fontdatm_plat(chr, mda.sc) & (1 << (c ^ 7))) != 0 ? 1 : 0);
                                        if ((chr & ~0x1f) == 0xc0)
                                                Buffer32[line + (x * 9) + 8] = mdacol(attr, blink, fontdatm_plat(chr, mda.sc) & 1);
                                        else
                                                Buffer32[line + (x * 9) + 8] = mdacol(attr, blink, 0);
                                }
                                mda.ma++;
                                if (drawcursor != 0)
                                {
                                        for (c = 0; c < 9; c++)
                                                Buffer32[line + (x * 9) + c] ^= mdacol(attr, 0, 1);
                                }
                        }
                }
                mda.sc = oldsc;
                if (mda.vc == mda.crtc[7] && mda.sc == 0)
                {
                        mda.stat |= 8;
                        //                        printf("VSYNC on %i %i\n",vc,sc);
                }
                mda.displine++;
                if (mda.displine >= 500)
                        mda.displine = 0;
        }
        else
        {
                timer_advance_u64(mda.timer, mda.dispontime);
                if (mda.dispon != 0)
                        mda.stat &= unchecked((uint8_t)~1);
                mda.linepos = 0;
                if (mda.vsynctime != 0)
                {
                        mda.vsynctime--;
                        if (mda.vsynctime == 0)
                        {
                                mda.stat &= unchecked((uint8_t)~8);
                                //                                printf("VSYNC off %i %i\n",vc,sc);
                        }
                }
                if (mda.sc == (mda.crtc[11] & 31) || ((mda.crtc[8] & 3) == 3 && mda.sc == ((mda.crtc[11] & 31) >> 1)))
                {
                        mda.con = 0;
                        mda.coff = 1;
                }
                if (mda.vadj != 0)
                {
                        mda.sc++;
                        mda.sc &= 31;
                        mda.ma = mda.maback;
                        mda.vadj--;
                        if (mda.vadj == 0)
                        {
                                mda.dispon = 1;
                                mda.ma = mda.maback = (uint16_t)((mda.crtc[13] | (mda.crtc[12] << 8)) & 0x3fff);
                                mda.sc = 0;
                        }
                }
                else if (mda.sc == mda.crtc[9] || ((mda.crtc[8] & 3) == 3 && mda.sc == (mda.crtc[9] >> 1)))
                {
                        mda.maback = mda.ma;
                        mda.sc = 0;
                        oldvc = mda.vc;
                        mda.vc++;
                        mda.vc &= 127;
                        if (mda.vc == mda.crtc[6])
                                mda.dispon = 0;
                        if (oldvc == mda.crtc[4])
                        {
                                //                                printf("Display over at %i\n",displine);
                                mda.vc = 0;
                                mda.vadj = mda.crtc[5];
                                if (mda.vadj == 0)
                                        mda.dispon = 1;
                                if (mda.vadj == 0)
                                        mda.ma = mda.maback = (uint16_t)((mda.crtc[13] | (mda.crtc[12] << 8)) & 0x3fff);
                                if ((mda.crtc[10] & 0x60) == 0x20)
                                        mda.cursoron = 0;
                                else
                                        mda.cursoron = mda.blink & 16;
                        }
                        if (mda.vc == mda.crtc[7])
                        {
                                mda.dispon = 0;
                                mda.displine = 0;
                                mda.vsynctime = 16;
                                if (mda.crtc[7] != 0)
                                {
                                        //                                        printf("Lastline %i Firstline %i
                                        //                                        %i\n",lastline,firstline,lastline-firstline);
                                        x = mda.crtc[1] * 9;
                                        mda.lastline++;
                                        if (x != xsize || (mda.lastline - mda.firstline) != ysize)
                                        {
                                                xsize = x;
                                                ysize = mda.lastline - mda.firstline;
                                                //                                                printf("Resize to %i,%i - R1
                                                //                                                %i\n",xsize,ysize,crtcm[1]);
                                                if (xsize < 64)
                                                        xsize = 656;
                                                if (ysize < 32)
                                                        ysize = 200;
                                                updatewindowsize(xsize, ysize);
                                        }

                                        video_blit_memtoscreen(0, mda.firstline, 0, ysize, xsize, ysize);

                                        frames++;
                                        video_res_x = mda.crtc[1];
                                        video_res_y = mda.crtc[6];
                                        video_bpp = 0;
                                }
                                mda.firstline = 1000;
                                mda.lastline = 0;
                                mda.blink++;
                        }
                }
                else
                {
                        mda.sc++;
                        mda.sc &= 31;
                        mda.ma = mda.maback;
                }
                if ((mda.sc == (mda.crtc[10] & 31) || ((mda.crtc[8] & 3) == 3 && mda.sc == ((mda.crtc[10] & 31) >> 1))))
                {
                        mda.con = 1;
                        //                        printf("Cursor on - %02X %02X %02X\n",crtcm[8],crtcm[10],crtcm[11]);
                }
        }
    }

    // pcem: vid_mda.c:233-245
    internal static object mda_standalone_init()
    {
        mda_t mda = new mda_t();
        // pcem: :236 — memset(mda, 0, sizeof(mda_t)) ; `new` zéro-initialise.
        mda_init(mda);

        // DEVIATION: `malloc(0x1000)` (:239) sans effacement — du tas. Un tableau nul ici ;
        //   l'oracle efface la sienne à l'amorçage (h_mda_attach), comme la VRAM de svga_init
        //   et de la M24 (PLAN-G9.md, décision n° 4).
        mda.vram = new uint8_t[0x1000];
        mem_mapping_add(mda.mapping, 0xb0000, 0x08000, mda_read, null, null, mda_write, null, null, null, 0,
                        MEM_MAPPING_EXTERNAL, mda);
        io_sethandler(0x03b0, 0x0010, mda_in, null, null, mda_out, null, null, mda);

        Probe = mda;
        return mda;
    }

    // pcem: vid_mda.c:247-275
    internal static void mda_init(mda_t mda)
    {
        int display_type;
        int c;

        display_type = device_get_config_int("display_type");
        cgapal_rebuild(display_type, 0);

        for (c = 0; c < 256; c++)
        {
                mdacol(c, 0, 0) = mdacol(c, 1, 0) = mdacol(c, 1, 1) = cgapal[0];
                if ((c & 8) != 0)
                        mdacol(c, 0, 1) = cgapal[0xf];
                else
                        mdacol(c, 0, 1) = cgapal[0x7];
        }
        mdacol(0x70, 0, 1) = cgapal[0];
        mdacol(0x70, 0, 0) = mdacol(0x70, 1, 0) = mdacol(0x70, 1, 1) = cgapal[0xf];
        mdacol(0xF0, 0, 1) = cgapal[0];
        mdacol(0xF0, 0, 0) = mdacol(0xF0, 1, 0) = mdacol(0xF0, 1, 1) = cgapal[0xf];
        mdacol(0x78, 0, 1) = cgapal[0x7];
        mdacol(0x78, 0, 0) = mdacol(0x78, 1, 0) = mdacol(0x78, 1, 1) = cgapal[0xf];
        mdacol(0xF8, 0, 1) = cgapal[0x7];
        mdacol(0xF8, 0, 0) = mdacol(0xF8, 1, 0) = mdacol(0xF8, 1, 1) = cgapal[0xf];
        mdacol(0x00, 0, 1) = mdacol(0x00, 1, 1) = cgapal[0];
        mdacol(0x08, 0, 1) = mdacol(0x08, 1, 1) = cgapal[0];
        mdacol(0x80, 0, 1) = mdacol(0x80, 1, 1) = cgapal[0];
        mdacol(0x88, 0, 1) = mdacol(0x88, 1, 1) = cgapal[0];

        timer_add(mda.timer, mda_poll, mda, 1);
    }

    // pcem: vid_mda.c:277 — sans appelant dans le dépôt (vid_pc200.c, absent).
    internal static void mda_setcol(int chr, int blink, int fg, uint8_t cga_ink) { mdacol(chr, blink, fg) = cgapal[cga_ink]; }

    // pcem: vid_mda.c:279-285
    internal static void mda_close(object? p)
    {
        mda_t mda = (mda_t)p;

        mem_mapping_remove(mda.mapping);
        // omitted: free(mda->vram), free(mda) (:283-284) — sous GC.
        Probe = null;
    }

    // pcem: vid_mda.c:287-291
    internal static void mda_speed_changed(object? p)
    {
        mda_t mda = (mda_t)p;

        mda_recalctimings(mda);
    }

    // pcem: vid_mda.c:293-301. omitted: `.description`.
    internal static device_config_t[] mda_config =
    [
        new device_config_t { name = "display_type", type = CONFIG_SELECTION, default_int = DISPLAY_WHITE,
            selection = [new() { description = "Green", value = DISPLAY_GREEN }, new() { description = "Amber", value = DISPLAY_AMBER }, new() { description = "White", value = DISPLAY_WHITE }] },
        new device_config_t { type = -1 },
    ];

    // pcem: vid_mda.c:303
    internal static device_t mda_device = new device_t("MDA", 0, mda_standalone_init, mda_close, null, mda_speed_changed, null,
                                                       null, mda_config);

    // iXtal26 (outillage, sans pendant C) — G9.0 : la MDA montée, pour la sonde de iXtal26.Diff
    // (pendant de h_mda_probe).
    internal static mda_t? Probe;
}
