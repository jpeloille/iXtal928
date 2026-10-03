// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/video/vid_hercules.c + includes/private/video/vid_hercules.h
// STATUS: transcribed — hercules_t, mdacols, hercules_out, hercules_in, hercules_write,
//         hercules_read, hercules_recalctimings, hercules_poll, hercules_init, hercules_close,
//         hercules_speed_changed, hercules_config, hercules_device.
//
// G9.1 (PLAN-G9.md). La Hercules Graphics Card : la MDA (vid_mda.cs), plus le graphique 720×348 en
// deux pages de 32 Ko (3BF bit 0 l'autorise, bit 1 ouvre B8000), 64 Ko de VRAM. Comme chez PCem,
// deux fichiers et deux `mdacols` statiques distincts.

// CS8600/CS8602/CS8604 : `(hercules_t)p` part du `object?` des delegates de mem.cs, io.cs et
// timer.cs, comme à vid_cga.cs.
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

// pcem: vid_hercules.c:11-32
// Classe et non struct : son adresse est prise (timer_add, mem_mapping_add, io_sethandler).
internal sealed class hercules_t
{
    internal mem_mapping_t mapping = new();

    internal uint8_t[] crtc = new uint8_t[32];
    internal int crtcreg;

    internal uint8_t ctrl, ctrl2, stat;

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

/*Hercules emulation*/
internal static class vid_hercules
{
    // pcem: vid_hercules.c:34 — `static uint32_t mdacols[256][2][2]`, à plat, distinct de celui de
    //   vid_mda.c (deux statiques de deux unités de compilation).
    private static readonly uint32_t[] mdacols = new uint32_t[256 * 2 * 2];

    private static ref uint32_t mdacol(int attr, int blink, int fg) => ref mdacols[(attr * 2 + blink) * 2 + fg];

    // pcem: vid_hercules.c:40-75
    internal static void hercules_out(uint16_t addr, uint8_t val, object? p)
    {
        hercules_t hercules = (hercules_t)p;
        //        pclog("Herc out %04X %02X\n",addr,val);
        switch (addr)
        {
        case 0x3b0:
        case 0x3b2:
        case 0x3b4:
        case 0x3b6:
                hercules.crtcreg = val & 31;
                return;
        case 0x3b1:
        case 0x3b3:
        case 0x3b5:
        case 0x3b7:
                // pcem bug, reproduced: PB-97 — le 6845 sans masques et le correctif du « Generic
                //   Turbo XT », comme la MDA (vid_hercules.c:54-60).
                hercules.crtc[hercules.crtcreg] = val;
                if (hercules.crtc[10] == 6 &&
                    hercules.crtc[11] == 7) /*Fix for Generic Turbo XT BIOS, which sets up cursor registers wrong*/
                {
                        hercules.crtc[10] = 0xb;
                        hercules.crtc[11] = 0xc;
                }
                hercules_recalctimings(hercules);
                return;
        case 0x3b8:
                hercules.ctrl = val;
                return;
        case 0x3bf:
                hercules.ctrl2 = val;
                if ((val & 2) != 0)
                        mem_mapping_set_addr(hercules.mapping, 0xb0000, 0x10000);
                else
                        mem_mapping_set_addr(hercules.mapping, 0xb0000, 0x08000);
                return;
        }
    }

    // pcem: vid_hercules.c:77-96
    internal static uint8_t hercules_in(uint16_t addr, object? p)
    {
        hercules_t hercules = (hercules_t)p;
        //       pclog("Herc in %04X %02X %04X:%04X %04X\n",addr,(hercules_stat & 0xF) | ((hercules_stat & 8) << 4),CS,pc,CX);
        switch (addr)
        {
        case 0x3b0:
        case 0x3b2:
        case 0x3b4:
        case 0x3b6:
                return (uint8_t)hercules.crtcreg;
        case 0x3b1:
        case 0x3b3:
        case 0x3b5:
        case 0x3b7:
                return hercules.crtc[hercules.crtcreg];
        case 0x3ba:
                return (uint8_t)((hercules.stat & 0xf) | ((hercules.stat & 8) << 4));
        }
        return 0xff;
    }

    // pcem: vid_hercules.c:98-102
    internal static void hercules_write(uint32_t addr, uint8_t val, object? p)
    {
        hercules_t hercules = (hercules_t)p;
        egawrites++;
        //        pclog("Herc write %08X %02X\n",addr,val);
        hercules.vram[addr & 0xffff] = val;
    }

    // pcem: vid_hercules.c:104-108
    internal static uint8_t hercules_read(uint32_t addr, object? p)
    {
        hercules_t hercules = (hercules_t)p;
        egareads++;
        return hercules.vram[addr & 0xffff];
    }

    // pcem: vid_hercules.c:110-119
    internal static void hercules_recalctimings(hercules_t hercules)
    {
        double disptime;
        double _dispontime, _dispofftime;
        disptime = hercules.crtc[0] + 1;
        _dispontime = hercules.crtc[1];
        _dispofftime = disptime - _dispontime;
        _dispontime *= pit.MDACONST;
        _dispofftime *= pit.MDACONST;
        // pcem bug, reproduced: PB-36 — la conversion d'un double négatif de la CGA et de la MDA
        //   (G9.1, PB-36 élargi).
        hercules.dispontime = unchecked((uint64_t)(int64_t)_dispontime);
        hercules.dispofftime = unchecked((uint64_t)(int64_t)_dispofftime);
    }

    // pcem: vid_hercules.c:121-299
    internal static void hercules_poll(object? p)
    {
        hercules_t hercules = (hercules_t)p;
        uint16_t ca = (uint16_t)((hercules.crtc[15] | (hercules.crtc[14] << 8)) & 0x3fff);
        int drawcursor;
        int x, c;
        int oldvc;
        uint8_t chr, attr;
        uint16_t dat;
        int oldsc;
        int blink;
        if (hercules.linepos == 0)
        {
                // pclog("Poll %i %i\n",vc,sc);
                timer_advance_u64(hercules.timer, hercules.dispofftime);
                hercules.stat |= 1;
                hercules.linepos = 1;
                oldsc = hercules.sc;
                // pcem bug, reproduced: PB-97 — l'entrelacé perd les lignes 8 et au-delà.
                if ((hercules.crtc[8] & 3) == 3)
                        hercules.sc = (hercules.sc << 1) & 7;
                if (hercules.dispon != 0)
                {
                        if (hercules.displine < hercules.firstline)
                        {
                                hercules.firstline = hercules.displine;
                                video_wait_for_buffer();
                        }
                        hercules.lastline = hercules.displine;
                        int line = hercules.displine * Stride;
                        if ((hercules.ctrl & 2) != 0 && (hercules.ctrl2 & 1) != 0)
                        {
                                ca = (uint16_t)((hercules.sc & 3) * 0x2000);
                                if ((hercules.ctrl & 0x80) != 0 && (hercules.ctrl2 & 2) != 0)
                                        ca += 0x8000;
                                //                                printf("Draw herc %04X\n",ca);
                                for (x = 0; x < hercules.crtc[1]; x++)
                                {
                                        dat = (uint16_t)((hercules.vram[((hercules.ma << 1) & 0x1fff) + ca] << 8) |
                                                         hercules.vram[((hercules.ma << 1) & 0x1fff) + ca + 1]);
                                        hercules.ma++;
                                        for (c = 0; c < 16; c++)
                                                Buffer32[line + (x << 4) + c] = (dat & (32768 >> c)) != 0 ? cgapal[0x7] : 0;
                                }
                        }
                        else
                        {
                                for (x = 0; x < hercules.crtc[1]; x++)
                                {
                                        chr = hercules.vram[(hercules.ma << 1) & 0xfff];
                                        attr = hercules.vram[((hercules.ma << 1) + 1) & 0xfff];
                                        drawcursor = ((hercules.ma == ca) && hercules.con != 0 && hercules.cursoron != 0) ? 1 : 0;
                                        blink = ((hercules.blink & 16) != 0 && (hercules.ctrl & 0x20) != 0 && (attr & 0x80) != 0 &&
                                                 drawcursor == 0) ? 1 : 0;
                                        if (hercules.sc == 12 && ((attr & 7) == 1))
                                        {
                                                for (c = 0; c < 9; c++)
                                                        Buffer32[line + (x * 9) + c] = mdacol(attr, blink, 1);
                                        }
                                        else
                                        {
                                                // pcem bug, reproduced: PB-96 — fontdatm lu au-delà de 16 lignes
                                                //   (vid_hercules.c:173, :176), à plat.
                                                for (c = 0; c < 8; c++)
                                                        Buffer32[line + (x * 9) + c] =
                                                                mdacol(attr, blink,
                                                                       (vid_mda.fontdatm_plat(chr, hercules.sc) & (1 << (c ^ 7))) != 0 ? 1 : 0);
                                                if ((chr & ~0x1f) == 0xc0)
                                                        Buffer32[line + (x * 9) + 8] = mdacol(attr, blink, vid_mda.fontdatm_plat(chr, hercules.sc) & 1);
                                                else
                                                        Buffer32[line + (x * 9) + 8] = mdacol(attr, blink, 0);
                                        }
                                        hercules.ma++;
                                        if (drawcursor != 0)
                                        {
                                                for (c = 0; c < 9; c++)
                                                        Buffer32[line + (x * 9) + c] ^= mdacol(attr, 0, 1);
                                        }
                                }
                        }
                }
                hercules.sc = oldsc;
                if (hercules.vc == hercules.crtc[7] && hercules.sc == 0)
                {
                        hercules.stat |= 8;
                        //                        printf("VSYNC on %i %i\n",vc,sc);
                }
                hercules.displine++;
                if (hercules.displine >= 500)
                        hercules.displine = 0;
        }
        else
        {
                timer_advance_u64(hercules.timer, hercules.dispontime);
                if (hercules.dispon != 0)
                        hercules.stat &= unchecked((uint8_t)~1);
                hercules.linepos = 0;
                if (hercules.vsynctime != 0)
                {
                        hercules.vsynctime--;
                        if (hercules.vsynctime == 0)
                        {
                                hercules.stat &= unchecked((uint8_t)~8);
                                //                                printf("VSYNC off %i %i\n",vc,sc);
                        }
                }
                if (hercules.sc == (hercules.crtc[11] & 31) ||
                    ((hercules.crtc[8] & 3) == 3 && hercules.sc == ((hercules.crtc[11] & 31) >> 1)))
                {
                        hercules.con = 0;
                        hercules.coff = 1;
                }
                if (hercules.vadj != 0)
                {
                        hercules.sc++;
                        hercules.sc &= 31;
                        hercules.ma = hercules.maback;
                        hercules.vadj--;
                        if (hercules.vadj == 0)
                        {
                                hercules.dispon = 1;
                                hercules.ma = hercules.maback = (uint16_t)((hercules.crtc[13] | (hercules.crtc[12] << 8)) & 0x3fff);
                                hercules.sc = 0;
                        }
                }
                else if (hercules.sc == hercules.crtc[9] ||
                         ((hercules.crtc[8] & 3) == 3 && hercules.sc == (hercules.crtc[9] >> 1)))
                {
                        hercules.maback = hercules.ma;
                        hercules.sc = 0;
                        oldvc = hercules.vc;
                        hercules.vc++;
                        hercules.vc &= 127;
                        if (hercules.vc == hercules.crtc[6])
                                hercules.dispon = 0;
                        if (oldvc == hercules.crtc[4])
                        {
                                //                                printf("Display over at %i\n",displine);
                                hercules.vc = 0;
                                hercules.vadj = hercules.crtc[5];
                                if (hercules.vadj == 0)
                                        hercules.dispon = 1;
                                if (hercules.vadj == 0)
                                        hercules.ma = hercules.maback =
                                                (uint16_t)((hercules.crtc[13] | (hercules.crtc[12] << 8)) & 0x3fff);
                                if ((hercules.crtc[10] & 0x60) == 0x20)
                                        hercules.cursoron = 0;
                                else
                                        hercules.cursoron = hercules.blink & 16;
                        }
                        if (hercules.vc == hercules.crtc[7])
                        {
                                hercules.dispon = 0;
                                hercules.displine = 0;
                                hercules.vsynctime = 16; //(crtcm[3]>>4)+1;
                                if (hercules.crtc[7] != 0)
                                {
                                        //                                        printf("Lastline %i Firstline %i
                                        //                                        %i\n",lastline,firstline,lastline-firstline);
                                        if ((hercules.ctrl & 2) != 0 && (hercules.ctrl2 & 1) != 0)
                                                x = hercules.crtc[1] << 4;
                                        else
                                                x = hercules.crtc[1] * 9;
                                        hercules.lastline++;
                                        if (x != xsize || (hercules.lastline - hercules.firstline) != ysize)
                                        {
                                                xsize = x;
                                                ysize = hercules.lastline - hercules.firstline;
                                                //                                                printf("Resize to %i,%i - R1
                                                //                                                %i\n",xsize,ysize,crtcm[1]);
                                                if (xsize < 64)
                                                        xsize = 656;
                                                if (ysize < 32)
                                                        ysize = 200;
                                                updatewindowsize(xsize, ysize);
                                        }
                                        video_blit_memtoscreen(0, hercules.firstline, 0, ysize, xsize, ysize);
                                        frames++;
                                        if ((hercules.ctrl & 2) != 0 && (hercules.ctrl2 & 1) != 0)
                                        {
                                                video_res_x = hercules.crtc[1] * 16;
                                                video_res_y = hercules.crtc[6] * 4;
                                                video_bpp = 1;
                                        }
                                        else
                                        {
                                                video_res_x = hercules.crtc[1];
                                                video_res_y = hercules.crtc[6];
                                                video_bpp = 0;
                                        }
                                }
                                hercules.firstline = 1000;
                                hercules.lastline = 0;
                                hercules.blink++;
                        }
                }
                else
                {
                        hercules.sc++;
                        hercules.sc &= 31;
                        hercules.ma = hercules.maback;
                }
                if ((hercules.sc == (hercules.crtc[10] & 31) ||
                     ((hercules.crtc[8] & 3) == 3 && hercules.sc == ((hercules.crtc[10] & 31) >> 1))))
                {
                        hercules.con = 1;
                        //                        printf("Cursor on - %02X %02X %02X\n",crtcm[8],crtcm[10],crtcm[11]);
                }
        }
    }

    // pcem: vid_hercules.c:302-339
    internal static object hercules_init()
    {
        int display_type;
        int c;
        hercules_t hercules = new hercules_t();
        // pcem: :306 — memset(hercules, 0, sizeof(hercules_t)) ; `new` zéro-initialise.

        // DEVIATION: `malloc(0x10000)` (:308) sans effacement — du tas. Un tableau nul ici ;
        //   l'oracle efface la sienne à l'amorçage (h_hercules_attach), PLAN-G9.md n° 4.
        hercules.vram = new uint8_t[0x10000];

        timer_add(hercules.timer, hercules_poll, hercules, 1);
        mem_mapping_add(hercules.mapping, 0xb0000, 0x08000, hercules_read, null, null, hercules_write, null, null, null, 0,
                        MEM_MAPPING_EXTERNAL, hercules);
        io_sethandler(0x03b0, 0x0010, hercules_in, null, null, hercules_out, null, null, hercules);

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

        Probe = hercules;
        return hercules;
    }

    // pcem: vid_hercules.c:341-346
    internal static void hercules_close(object? p)
    {
        // pcem bug, reproduced: PB-98 — pas de mem_mapping_remove : le C libère la carte (free,
        //   :344-345) et laisse sa projection, pendante, dans la liste de mem.c — jusqu'au
        //   mem_alloc de l'amorçage suivant, qui vide la liste (mem.c:1373) ; rien ne la lit
        //   entre-temps. Ici la projection reste de même jusque-là, l'objet vivant sous GC.
        // omitted: free(hercules->vram), free(hercules) — sous GC.
        Probe = null;
    }

    // pcem: vid_hercules.c:348-352
    internal static void hercules_speed_changed(object? p)
    {
        hercules_t hercules = (hercules_t)p;

        hercules_recalctimings(hercules);
    }

    // pcem: vid_hercules.c:354-362. omitted: `.description`.
    internal static device_config_t[] hercules_config =
    [
        new device_config_t { name = "display_type", type = CONFIG_SELECTION, default_int = DISPLAY_WHITE,
            selection = [new() { description = "Green", value = DISPLAY_GREEN }, new() { description = "Amber", value = DISPLAY_AMBER }, new() { description = "White", value = DISPLAY_WHITE }] },
        new device_config_t { type = -1 },
    ];

    // pcem: vid_hercules.c:364-365
    internal static device_t hercules_device = new device_t("Hercules", 0, hercules_init, hercules_close, null,
                                                            hercules_speed_changed, null, null, hercules_config);

    // iXtal26 (outillage, sans pendant C) — G9.1 : la carte montée, pour la sonde (h_hercules_probe).
    internal static hercules_t? Probe;
}
