// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/video/vid_olivetti_m24.c + includes/private/video/vid_olivetti_m24.h
// STATUS: transcribed — m24_t, m24_out/m24_in, m24_write/m24_read,
//         m24_recalctimings, m24_poll, m24_init, m24_close, m24_speed_changed,
//         m24_device. Omis : les pclog, sortie pure. Déviation R9 : PB-88 ; PB-89 reproduit.

/*Olivetti M24 video emulation
  Essentially double-res CGA*/

// CS8600/CS8602/CS8604 : `m24_t m24 = (m24_t)p;` — même raison qu'à vid_cga.cs:9-16 :
// les delegates déclarent `object?`, m24_init enregistre toujours `m24`.
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

// pcem: vid_olivetti_m24.c:12-39
// Classe et non struct : son adresse est prise et stockée (timer_add,
// mem_mapping_add, io_sethandler la reçoivent tous comme `void *p`).
internal sealed class m24_t
{
    internal mem_mapping_t mapping = new();

    internal uint8_t[] crtc = new uint8_t[32];
    internal int crtcreg;

    internal uint8_t[] vram = [];
    internal uint8_t[] charbuffer = new uint8_t[256];

    internal uint8_t ctrl;
    internal uint32_t @base;

    internal uint8_t cgamode, cgacol;
    internal uint8_t stat;

    internal int linepos, displine;
    internal int sc, vc;
    internal int con, coff, cursoron, blink;
    internal int vsynctime, vadj;
    internal int lineff;
    internal uint16_t ma, maback;
    internal int dispon;

    internal uint64_t dispontime, dispofftime;
    internal pc_timer_t timer = new();

    internal int firstline, lastline;
}

internal static partial class vid_olivetti_m24
{
    // pcem: vid_olivetti_m24.c:41-42
    // pcem bug, reproduced: PB-230 — R16 et R17 s'écrivent (masque FFh), comme sur la CGA ; un 6845 les
    //   tient en lecture seule.
    private static uint8_t[] crtcmask = new uint8_t[32] {0xff, 0xff, 0xff, 0xff, 0x7f, 0x1f, 0x7f, 0x7f, 0xf3, 0x1f, 0x7f, 0x1f, 0x3f, 0xff, 0x3f, 0xff,
                                                         0xff, 0xff, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00};

    // pcem: vid_olivetti_m24.c:46-75
    internal static void m24_out(uint16_t addr, uint8_t val, object p)
    {
        m24_t m24 = (m24_t)p;
        uint8_t old;
        switch (addr)
        {
        case 0x3d4:
                m24.crtcreg = val & 31;
                return;
        case 0x3d5:
                old = m24.crtc[m24.crtcreg];
                m24.crtc[m24.crtcreg] = (uint8_t)(val & crtcmask[m24.crtcreg]);
                if (old != val)
                {
                        if (m24.crtcreg < 0xe || m24.crtcreg > 0x10)
                        {
                                fullchange = changeframecount;
                                m24_recalctimings(m24);
                        }
                }
                return;
        case 0x3d8:
                m24.cgamode = val;
                return;
        case 0x3d9:
                m24.cgacol = val;
                return;
        case 0x3de:
                m24.ctrl = val;
                m24.@base = (uint32_t)(((val & 0x08) != 0) ? 0x4000 : 0);
                return;
        }
    }

    // pcem: vid_olivetti_m24.c:77-88
    internal static uint8_t m24_in(uint16_t addr, object p)
    {
        m24_t m24 = (m24_t)p;
        switch (addr)
        {
        case 0x3d4:
                // pcem bug, reproduced: PB-230 — l'index se relit, et R0-R13 aussi (3D5h), comme sur la CGA.
                return (uint8_t)m24.crtcreg;
        case 0x3d5:
                return m24.crtc[m24.crtcreg];
        case 0x3da:
                return m24.stat;
        }
        return 0xff;
    }

    // pcem: vid_olivetti_m24.c:90-97
    // offset <= 0xfc, offset | 1 <= 0xfd : toujours dans charbuffer.
    internal static void m24_write(uint32_t addr, uint8_t val, object? p)
    {
        m24_t m24 = (m24_t)p;
        int offset = (int)(((timer_get_remaining_u64(m24.timer) / pit.CGACONST) * 4) & 0xfc);

        m24.vram[addr & 0x7FFF] = val;
        m24.charbuffer[offset] = val;
        m24.charbuffer[offset | 1] = val;
    }

    // pcem: vid_olivetti_m24.c:99-102
    internal static uint8_t m24_read(uint32_t addr, object? p)
    {
        m24_t m24 = (m24_t)p;
        return m24.vram[addr & 0x7FFF];
    }

    // pcem: vid_olivetti_m24.c:104-121
    internal static void m24_recalctimings(m24_t m24)
    {
        double _dispontime, _dispofftime, disptime;
        if ((m24.cgamode & 1) != 0)
        {
                disptime = m24.crtc[0] + 1;
                _dispontime = m24.crtc[1];
        }
        else
        {
                disptime = (m24.crtc[0] + 1) << 1;
                _dispontime = m24.crtc[1] << 1;
        }
        _dispofftime = disptime - _dispontime;
        _dispontime *= pit.CGACONST / 2;
        _dispofftime *= pit.CGACONST / 2;
        // pcem bug, reproduced: PB-36 — même conversion d'un double négatif qu'à
        //   cga_recalctimings, dès que crtc[1] dépasse crtc[0] + 1 : GCC rend
        //   (uint64_t)(int64_t)x, C# saturerait à 0.
        m24.dispontime = unchecked((uint64_t)(int64_t)_dispontime);
        m24.dispofftime = unchecked((uint64_t)(int64_t)_dispofftime);
    }

    // pcem: vid_olivetti_m24.c:123-418
    internal static void m24_poll(object? p)
    {
        m24_t m24 = (m24_t)p;
        uint16_t ca = (uint16_t)((m24.crtc[15] | (m24.crtc[14] << 8)) & 0x3fff);
        int drawcursor;
        int x, c;
        int oldvc;
        uint8_t chr, attr;
        uint16_t dat, dat2;
        Span<uint32_t> cols = stackalloc uint32_t[4];
        int col;
        int oldsc;
        int i;
        uint32_t v;
        if (m24.linepos == 0)
        {
                timer_advance_u64(m24.timer, m24.dispofftime);
                m24.stat |= 1;
                m24.linepos = 1;
                oldsc = m24.sc;
                if ((m24.crtc[8] & 3) == 3)
                        m24.sc = (m24.sc << 1) & 7;
                if (m24.dispon != 0)
                {
                        // omitted: pclog("dispon %i\n", m24->linepos) (vid_olivetti_m24.c:144) — sortie pure.
                        if (m24.displine < m24.firstline)
                        {
                                m24.firstline = m24.displine;
                        }
                        m24.lastline = m24.displine;
                        for (c = 0; c < 8; c++)
                        {
                                // pcem bug, reproduced: PB-89 — crtc[1] n'est pas masqué (crtcmask[1] = 0xff) ;
                                //   l'abscisse c + (crtc[1] << 4) + 8 monte à 4 095 et déborde sur la ligne
                                //   suivante de buffer32 (lignes contiguës, wx-sdl2-video.c:59-69, ici le même
                                //   tableau plat). Jamais hors du tableau : displine < 720. De même les boucles de
                                //   40 colonnes et du graphique (vid_olivetti_m24.c:218-231, :258-259, :279) et
                                //   hline (:287-289).
                                if ((m24.cgamode & 0x12) == 0x12)
                                {
                                        Buffer32[m24.displine * Stride + c] = cgapal[0];
                                        if ((m24.cgamode & 1) != 0)
                                        {
                                                i = m24.displine * Stride + c + (m24.crtc[1] << 3) + 8;
                                                Buffer32[i] = cgapal[0];
                                        }
                                        else
                                        {
                                                i = m24.displine * Stride + c + (m24.crtc[1] << 4) + 8;
                                                Buffer32[i] = cgapal[0];
                                        }
                                }
                                else
                                {
                                        Buffer32[m24.displine * Stride + c] = cgapal[m24.cgacol & 15];
                                        if ((m24.cgamode & 1) != 0)
                                        {
                                                i = m24.displine * Stride + c + (m24.crtc[1] << 3) + 8;
                                                Buffer32[i] = cgapal[m24.cgacol & 15];
                                        }
                                        else
                                        {
                                                i = m24.displine * Stride + c + (m24.crtc[1] << 4) + 8;
                                                Buffer32[i] = cgapal[m24.cgacol & 15];
                                        }
                                }
                        }
                        if ((m24.cgamode & 1) != 0)
                        {
                                for (x = 0; x < m24.crtc[1]; x++)
                                {
                                        // pcem bug, not reproduced: PB-88 — DEVIATION : charbuffer fait 256 octets
                                        //   (vid_olivetti_m24.c:19) et crtc[1] non masqué porte l'index jusqu'à
                                        //   509. PCem lit alors les champs qui suivent charbuffer dans m24_t
                                        //   (ctrl, base, cgamode, cgacol, stat, linepos, displine…). Ici un index
                                        //   >= 256 rend 0 (R9).
                                        chr = (x << 1) < 256 ? m24.charbuffer[x << 1] : (uint8_t)0;
                                        attr = ((x << 1) + 1) < 256 ? m24.charbuffer[(x << 1) + 1] : (uint8_t)0; // PB-88
                                        drawcursor = ((m24.ma == ca) && m24.con != 0 && m24.cursoron != 0) ? 1 : 0;
                                        if ((m24.cgamode & 0x20) != 0)
                                        {
                                                cols[1] = cgapal[attr & 15];
                                                cols[0] = cgapal[(attr >> 4) & 7];
                                                if ((m24.blink & 16) != 0 && (attr & 0x80) != 0 && drawcursor == 0)
                                                        cols[1] = cols[0];
                                        }
                                        else
                                        {
                                                cols[1] = cgapal[attr & 15];
                                                cols[0] = cgapal[attr >> 4];
                                        }
                                        // (x << 3) + c + 8 <= 254 * 8 + 7 + 8 = 2 047 : toujours dans la ligne.
                                        if (drawcursor != 0)
                                        {
                                                for (c = 0; c < 8; c++)
                                                        Buffer32[m24.displine * Stride + (x << 3) + c + 8] =
                                                                cols[(fontdatm[chr, ((m24.sc & 7) << 1) | m24.lineff] &
                                                                      (1 << (c ^ 7))) != 0
                                                                             ? 1
                                                                             : 0] ^
                                                                0xffffffu;
                                        }
                                        else
                                        {
                                                for (c = 0; c < 8; c++)
                                                        Buffer32[m24.displine * Stride + (x << 3) + c + 8] =
                                                                cols[(fontdatm[chr, ((m24.sc & 7) << 1) | m24.lineff] &
                                                                      (1 << (c ^ 7))) != 0
                                                                             ? 1
                                                                             : 0];
                                        }
                                        m24.ma++;
                                }
                        }
                        else if ((m24.cgamode & 2) == 0)
                        {
                                for (x = 0; x < m24.crtc[1]; x++)
                                {
                                        chr = m24.vram[((m24.ma << 1) & 0x3fff) + m24.@base];
                                        attr = m24.vram[(((m24.ma << 1) + 1) & 0x3fff) + m24.@base];
                                        drawcursor = ((m24.ma == ca) && m24.con != 0 && m24.cursoron != 0) ? 1 : 0;
                                        if ((m24.cgamode & 0x20) != 0)
                                        {
                                                cols[1] = cgapal[attr & 15];
                                                cols[0] = cgapal[(attr >> 4) & 7];
                                                // pcem bug, reproduced: PB-232 — en 40 colonnes, le
                                                //   clignotement n'exempte pas la cellule du curseur (:209),
                                                //   comme il le fait en 80 colonnes (:177).
                                                if ((m24.blink & 16) != 0 && (attr & 0x80) != 0)
                                                        cols[1] = cols[0];
                                        }
                                        else
                                        {
                                                cols[1] = cgapal[attr & 15];
                                                cols[0] = cgapal[attr >> 4];
                                        }
                                        m24.ma++;
                                        if (drawcursor != 0)
                                        {
                                                for (c = 0; c < 8; c++)
                                                {
                                                        v = cols[(fontdatm[chr, ((m24.sc & 7) << 1) | m24.lineff] &
                                                                  (1 << (c ^ 7))) != 0
                                                                         ? 1
                                                                         : 0] ^
                                                            0xffffffu;
                                                        i = m24.displine * Stride + (x << 4) + (c << 1) + 1 + 8;
                                                        Buffer32[i] = v;
                                                        i = m24.displine * Stride + (x << 4) + (c << 1) + 8;
                                                        Buffer32[i] = v;
                                                }
                                        }
                                        else
                                        {
                                                for (c = 0; c < 8; c++)
                                                {
                                                        v = cols[(fontdatm[chr, ((m24.sc & 7) << 1) | m24.lineff] &
                                                                  (1 << (c ^ 7))) != 0
                                                                         ? 1
                                                                         : 0];
                                                        i = m24.displine * Stride + (x << 4) + (c << 1) + 1 + 8;
                                                        Buffer32[i] = v;
                                                        i = m24.displine * Stride + (x << 4) + (c << 1) + 8;
                                                        Buffer32[i] = v;
                                                }
                                        }
                                }
                        }
                        else if ((m24.cgamode & 16) == 0)
                        {
                                cols[0] = cgapal[m24.cgacol & 15];
                                col = ((m24.cgacol & 16) != 0) ? 8 : 0;
                                if ((m24.cgamode & 4) != 0)
                                {
                                        cols[1] = cgapal[col | 3];
                                        cols[2] = cgapal[col | 4];
                                        cols[3] = cgapal[col | 7];
                                }
                                else if ((m24.cgacol & 32) != 0)
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
                                for (x = 0; x < m24.crtc[1]; x++)
                                {
                                        dat = (uint16_t)((m24.vram[((m24.ma << 1) & 0x1fff) + ((m24.sc & 1) * 0x2000) + m24.@base] << 8) |
                                                         m24.vram[((m24.ma << 1) & 0x1fff) + ((m24.sc & 1) * 0x2000) + 1 + m24.@base]);
                                        m24.ma++;
                                        for (c = 0; c < 8; c++)
                                        {
                                                v = cols[dat >> 14];
                                                i = m24.displine * Stride + (x << 4) + (c << 1) + 1 + 8;
                                                Buffer32[i] = v;
                                                i = m24.displine * Stride + (x << 4) + (c << 1) + 8;
                                                Buffer32[i] = v;
                                                dat <<= 2;
                                        }
                                }
                        }
                        else
                        {
                                if ((m24.ctrl & 1) != 0)
                                {
                                        dat2 = (uint16_t)(((m24.sc & 1) * 0x4000) | (m24.lineff * 0x2000));
                                        cols[0] = cgapal[0];
                                        cols[1] = cgapal[15];
                                }
                                else
                                {
                                        dat2 = (uint16_t)((m24.sc & 1) * 0x2000);
                                        cols[0] = cgapal[0];
                                        cols[1] = cgapal[m24.cgacol & 15];
                                }
                                for (x = 0; x < m24.crtc[1]; x++)
                                {
                                        dat = (uint16_t)((m24.vram[((m24.ma << 1) & 0x1fff) + dat2] << 8) |
                                                         m24.vram[((m24.ma << 1) & 0x1fff) + dat2 + 1]);
                                        m24.ma++;
                                        for (c = 0; c < 16; c++)
                                        {
                                                i = m24.displine * Stride + (x << 4) + c + 8;
                                                Buffer32[i] = cols[dat >> 15];
                                                dat <<= 1;
                                        }
                                }
                        }
                }
                else
                {
                        cols[0] = cgapal[((m24.cgamode & 0x12) == 0x12) ? 0 : (m24.cgacol & 15)];
                        if ((m24.cgamode & 1) != 0)
                                hline(0, m24.displine, (m24.crtc[1] << 3) + 16, (int)cols[0]);
                        else
                                hline(0, m24.displine, (m24.crtc[1] << 4) + 16, (int)cols[0]);
                }

                if ((m24.cgamode & 1) != 0)
                        x = (m24.crtc[1] << 3) + 16;
                else
                        x = (m24.crtc[1] << 4) + 16;

                m24.sc = oldsc;
                if (m24.vc == m24.crtc[7] && m24.sc == 0)
                        m24.stat |= 8;
                m24.displine++;
                if (m24.displine >= 720)
                        m24.displine = 0;
        }
        else
        {
                timer_advance_u64(m24.timer, m24.dispontime);
                if (m24.dispon != 0)
                        m24.stat &= unchecked((uint8_t)~1);
                m24.linepos = 0;
                m24.lineff ^= 1;
                if (m24.lineff != 0)
                {
                        m24.ma = m24.maback;
                }
                else
                {
                        if (m24.vsynctime != 0)
                        {
                                m24.vsynctime--;
                                if (m24.vsynctime == 0)
                                        m24.stat &= unchecked((uint8_t)~8);
                        }
                        if (m24.sc == (m24.crtc[11] & 31) ||
                            ((m24.crtc[8] & 3) == 3 && m24.sc == ((m24.crtc[11] & 31) >> 1)))
                        {
                                m24.con = 0;
                                m24.coff = 1;
                        }
                        if (m24.vadj != 0)
                        {
                                m24.sc++;
                                m24.sc &= 31;
                                m24.ma = m24.maback;
                                m24.vadj--;
                                if (m24.vadj == 0)
                                {
                                        m24.dispon = 1;
                                        m24.ma = m24.maback = (uint16_t)((m24.crtc[13] | (m24.crtc[12] << 8)) & 0x3fff);
                                        m24.sc = 0;
                                }
                        }
                        else if (m24.sc == m24.crtc[9] || ((m24.crtc[8] & 3) == 3 && m24.sc == (m24.crtc[9] >> 1)))
                        {
                                m24.maback = m24.ma;
                                m24.sc = 0;
                                oldvc = m24.vc;
                                m24.vc++;
                                m24.vc &= 127;

                                if (m24.vc == m24.crtc[6])
                                        m24.dispon = 0;

                                if (oldvc == m24.crtc[4])
                                {
                                        m24.vc = 0;
                                        m24.vadj = m24.crtc[5];
                                        if (m24.vadj == 0)
                                                m24.dispon = 1;
                                        if (m24.vadj == 0)
                                                m24.ma = m24.maback = (uint16_t)((m24.crtc[13] | (m24.crtc[12] << 8)) & 0x3fff);
                                        // pcem bug, reproduced: PB-231 — R10 bits 5-6 : seul 01 (pas de
                                        //   curseur) est traité ; 10 et 11 font aussi clignoter le 6845 lui-même.
                                        if ((m24.crtc[10] & 0x60) == 0x20)
                                                m24.cursoron = 0;
                                        else
                                                m24.cursoron = m24.blink & 16;
                                }

                                if (m24.vc == m24.crtc[7])
                                {
                                        m24.dispon = 0;
                                        m24.displine = 0;
                                        m24.vsynctime = (m24.crtc[3] >> 4) + 1;
                                        if (m24.crtc[7] != 0)
                                        {
                                                if ((m24.cgamode & 1) != 0)
                                                        x = (m24.crtc[1] << 3) + 16;
                                                else
                                                        x = (m24.crtc[1] << 4) + 16;
                                                m24.lastline++;
                                                if (x != xsize || (m24.lastline - m24.firstline) != ysize)
                                                {
                                                        xsize = x;
                                                        ysize = m24.lastline - m24.firstline;
                                                        if (xsize < 64)
                                                                xsize = 656;
                                                        if (ysize < 32)
                                                                ysize = 200;
                                                        updatewindowsize(xsize, ysize + 16);
                                                }

                                                video_blit_memtoscreen(0, m24.firstline - 8, 0,
                                                                       (m24.lastline - m24.firstline) + 16, xsize,
                                                                       (m24.lastline - m24.firstline) + 16);
                                                frames++;

                                                video_res_x = xsize - 16;
                                                video_res_y = ysize;
                                                if ((m24.cgamode & 1) != 0)
                                                {
                                                        video_res_x /= 8;
                                                        video_res_y /= (m24.crtc[9] + 1) * 2;
                                                        video_bpp = 0;
                                                }
                                                else if ((m24.cgamode & 2) == 0)
                                                {
                                                        video_res_x /= 16;
                                                        video_res_y /= (m24.crtc[9] + 1) * 2;
                                                        video_bpp = 0;
                                                }
                                                else if ((m24.cgamode & 16) == 0)
                                                {
                                                        video_res_x /= 2;
                                                        video_res_y /= 2;
                                                        video_bpp = 2;
                                                }
                                                else if ((m24.ctrl & 1) == 0)
                                                {
                                                        video_res_y /= 2;
                                                        video_bpp = 1;
                                                }
                                        }
                                        m24.firstline = 1000;
                                        m24.lastline = 0;
                                        m24.blink++;
                                }
                        }
                        else
                        {
                                m24.sc++;
                                m24.sc &= 31;
                                m24.ma = m24.maback;
                        }
                        if ((m24.sc == (m24.crtc[10] & 31) ||
                             ((m24.crtc[8] & 3) == 3 && m24.sc == ((m24.crtc[10] & 31) >> 1))))
                                m24.con = 1;
                }
                if (m24.dispon != 0 && (m24.cgamode & 1) != 0)
                {
                        // pcem bug, not reproduced: PB-88 — DEVIATION : la borne crtc[1] << 1 monte à
                        //   510 pour un charbuffer de 256 octets ; PCem écrase alors ctrl, base,
                        //   cgamode, cgacol, stat, linepos, displine, sc… (les champs de m24_t qui
                        //   suivent charbuffer). Ici une écriture d'index >= 256 est abandonnée (R9).
                        for (x = 0; x < (m24.crtc[1] << 1); x++)
                                if (x < 256)
                                        m24.charbuffer[x] = m24.vram[(((m24.ma << 1) + x) & 0x3fff) + m24.@base];
                                else
                                        Diag.R9.Garde("vid_olivetti_m24.c:415");
                }
        }
    }

    // pcem: vid_olivetti_m24.c:420-430
    // G1.1 — la M24 montée, pour la sonde de fin de boot-diff (vid_svga.Probe) ; pendant de
    // h_m24 côté oracle (harness_m24.c). Pas un état de PCem.
    internal static m24_t? m24_pri;

    internal static object m24_init()
    {
        m24_t m24 = new m24_t();
        m24_pri = m24;
        // pcem: vid_olivetti_m24.c:422 — memset(m24, 0, sizeof(m24_t)) ; `new` zéro-initialise.

        // Déviation de l'ORACLE : malloc(0x8000) sans memset (vid_olivetti_m24.c:424) ;
        //   le tableau C# naît à zéro, et l'oracle memset cette VRAM pour s'y aligner.
        m24.vram = new uint8_t[0x8000];

        timer_add(m24.timer, m24_poll, m24, 1);
        mem_mapping_add(m24.mapping, 0xb8000, 0x08000, m24_read, null, null, m24_write, null, null, null, 0, 0, m24);
        io_sethandler(0x03d0, 0x0010, m24_in, null, null, m24_out, null, null, m24);
        return m24;
    }

    // pcem: vid_olivetti_m24.c:432-437
    internal static void m24_close(object? p)
    {
        m24_t m24 = (m24_t)p;

        if (m24_pri == m24)
                m24_pri = null;
        // omitted: free(m24->vram) et free(m24) — libération manuelle, sans objet sous GC.
    }

    // pcem: vid_olivetti_m24.c:439-443
    internal static void m24_speed_changed(object? p)
    {
        m24_t m24 = (m24_t)p;

        m24_recalctimings(m24);
    }

    // pcem: vid_olivetti_m24.c:445
    internal static device_t m24_device = new device_t("Olivetti M24 (video)", 0, m24_init, m24_close, null, m24_speed_changed, null,
                                                       null, null);
}
