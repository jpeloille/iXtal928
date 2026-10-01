// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/video/vid_svga.c + includes/private/video/vid_svga.h
// STATUS: partial — svga_t, svga_out/svga_in, svga_recalctimings, svga_poll, svga_init,
//         svga_close, svga_write/read et leurs variantes w/l, svga_doblit. Omis : les
//         chemins *_linear, qu'aucune carte du dépôt n'enregistre.

// CS8600/CS8602/CS8604 : `svga_t svga = (svga_t)p;` — même raison qu'à vid_cga.cs:9-17.
// Le `void *p` des handlers est déclaré `object?` par les delegates de mem.cs et
// timer.cs, et svga_init enregistre toujours `svga` : l'analyse de nullabilité ne peut
// pas le savoir.
#pragma warning disable CS8600, CS8602, CS8604

// CS0162 : les six `pclog` gardés par `if (svga_output)` (vid_svga.c:821, :851, :1487,
// :1494, :1516, :1523) — svga_output est un #define à 0 (:13), donc une constante, et le
// compilateur signale le corps comme inatteignable. Il l'est chez PCem aussi ; on garde la
// ligne pour que le fichier se lise en regard du C.
#pragma warning disable CS0162

using System.Text;
using iXtal26.Memory;
using iXtal26.Models;
using static iXtal26.Cpu.x86;
using static iXtal26.Memory.mem;
using static iXtal26.Video.video;
using static iXtal26.Video.vid_svga_render;
using static iXtal26.io;
using static iXtal26.pc;
using static iXtal26.timer;

namespace iXtal26.Video;

// pcem: vid_svga.h:117-135, :160 — les pointeurs de fonction de svga_t. Le C n'a pas de
// typedef : ce sont des déclarateurs en ligne, et C# exige un nom. video_in et
// video_out ont la forme exacte de inb_fn et outb_fn (io.cs), qu'ils reprennent :
// svga_out les repasse tels quels à io_sethandler / io_removehandler.
internal delegate void svga_render_fn(svga_t svga);
internal delegate void svga_draw_fn(svga_t svga, int displine);
internal delegate int svga_line_compare_fn(svga_t svga);
internal delegate uint32_t svga_remap_fn(svga_t svga, uint32_t in_addr);

// pcem: vid_svga.h:101-109
// DEVIATION: structure ANONYME en C ; C# exige un nom. Struct et non classe :
//   `svga->hwcursor_latch = svga->hwcursor` (vid_svga.c:735) est une COPIE.
internal struct svga_hwcursor_t
{
    internal int ena;
    internal int x, y;
    internal int xoff, yoff;
    internal int xsize, ysize;
    internal uint32_t addr;
    internal uint32_t pitch;
    internal int v_acc, h_acc;
}

// pcem: vid_svga.h:3-161
// Classe et non struct : son adresse est prise et stockée (timer_add,
// mem_mapping_add, io_sethandler la reçoivent comme `void *p`).
internal sealed class svga_t
{
    internal mem_mapping_t mapping = new();

    internal uint8_t crtcreg;
    internal uint8_t[] crtc = new uint8_t[128];
    internal uint8_t[] gdcreg = new uint8_t[64];
    internal int gdcaddr;
    internal uint8_t[] attrregs = new uint8_t[32];
    internal int attraddr, attrff;
    internal int attr_palette_enable;
    internal uint8_t[] seqregs = new uint8_t[64];
    internal int seqaddr;

    internal uint8_t miscout;
    internal int vidclock;

    /*The three variables below allow us to implement memory maps like that seen on a 1MB Trio64 :
      0MB-1MB - VRAM
      1MB-2MB - VRAM mirror
      2MB-4MB - open bus
      4MB-xMB - mirror of above

      For the example memory map, decode_mask would be 4MB-1 (4MB address space), vram_max would be 2MB
      (present video memory only responds to first 2MB), vram_mask would be 1MB-1 (video memory wraps at 1MB)
    */
    internal uint32_t decode_mask;
    internal uint32_t vram_max;
    internal uint32_t vram_mask;

    internal uint8_t la, lb, lc, ld;

    internal uint8_t dac_mask, dac_status;
    internal int dac_read, dac_write, dac_pos;
    internal int dac_r, dac_g;

    internal uint8_t cgastat;

    internal uint8_t plane_mask;

    internal int fb_only;

    internal int fast;
    internal uint8_t colourcompare, colournocare;
    internal int readmode, writemode, readplane;
    internal int chain4, chain2_write, chain2_read;
    internal uint8_t writemask;
    internal uint32_t charseta, charsetb;

    internal int set_reset_disabled;

    internal uint8_t[] egapal = new uint8_t[16];
    internal uint32_t[] pallook = new uint32_t[512];
    internal RGB[] vgapal = new RGB[256]; // PALETTE, video.h:18

    internal int ramdac_type;

    internal int vtotal, dispend, vsyncstart, split, vblankstart;
    internal int hdisp, hdisp_old, htotal, hdisp_time, rowoffset;
    internal int lowres, interlace;
    internal int linedbl, rowcount;
    internal double clock;
    internal uint32_t ma_latch, ca_adj;
    internal int bpp;

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
    internal int char_width;

    internal int firstline, lastline;
    internal int firstline_draw, lastline_draw;
    internal int displine;

    internal uint8_t[] vram = [];
    internal uint8_t[] changedvram = [];
    internal uint32_t vram_display_mask;
    internal uint32_t banked_mask;

    internal uint32_t write_bank, read_bank;

    internal int fullchange;

    internal int video_res_x, video_res_y, video_bpp;
    internal int video_res_override; /*If clear then SVGA code will set above variables, if
                                       set then card code will*/
    internal int frames, fps;

    internal svga_hwcursor_t hwcursor, hwcursor_latch, overlay, overlay_latch;

    internal int hwcursor_on;
    internal int overlay_on;

    internal int hwcursor_oddeven;
    internal int overlay_oddeven;

    internal svga_render_fn? render;
    internal svga_render_fn? recalctimings_ex;

    internal outb_fn? video_out;
    internal inb_fn? video_in;

    internal svga_draw_fn? hwcursor_draw;

    internal svga_draw_fn? overlay_draw;

    internal svga_render_fn? vblank_start;

    /*Called when VC=R18 and friends. If this returns zero then MA resetting
      is skipped. Matrox Mystique in Power mode reuses this counter for
      vertical line interrupt*/
    internal svga_line_compare_fn? line_compare;

    /*Called at the start of vertical sync*/
    internal svga_render_fn? vsync_callback;

    /*If set then another device is driving the monitor output and the SVGA
      card should not attempt to display anything */
    internal int @override;
    internal object? p;

    internal uint8_t ksc5601_sbyte_mask;
    internal uint8_t[] ksc5601_udc_area_msb = new uint8_t[2];
    internal int ksc5601_swap_mode;
    internal uint16_t ksc5601_english_font_type;

    internal int vertical_linedbl;

    /*Used to implement CRTC[0x17] bit 2 hsync divisor*/
    internal int hsync_divisor;

    /*Tseng-style chain4 mode - CRTC dword mode is the same as byte mode, chain4
      addresses are shifted to match*/
    internal int packed_chain4;

    /*Force CRTC to dword mode, regardless of CR14/CR17. Required for S3 enhanced mode*/
    internal int force_dword_mode;

    internal int remap_required;
    internal svga_remap_fn? remap_func;
}

/*Generic SVGA handling*/
/*This is intended to be used by another SVGA driver, and not as a card in it's own right*/
internal static partial class vid_svga
{
    // pcem: vid_svga.h:193-194
    internal const int RAMDAC_6BIT = 0;
    internal const int RAMDAC_8BIT = 1;

    // pcem: vid_svga.c:13
    private const int svga_output = 0;

    // DEVIATION: pclog() appartient à plugin-api/logging.c, non transcrit. Même forme
    //   que video.cs, mem_bios.cs et rom.cs ; ses seuls appels ici sont gardés par
    //   svga_output, qui vaut 0.
    private static void pclog(string s) => Console.Error.Write(s);

    // pcem: vid_svga.c:19
    internal static readonly uint8_t[,] svga_rotate = new uint8_t[8, 256];

    /*Primary SVGA device. As multiple video cards are not yet supported this is the
      only SVGA device.*/
    // pcem: vid_svga.c:21-23
    private static svga_t? svga_pri;

    // pcem: vid_svga.c:25
    internal static svga_t? svga_get_pri() { return svga_pri; }

    // pcem: vid_svga.c:26-30
    internal static void svga_set_override(svga_t svga, int val)
    {
        if (svga.@override != 0 && val == 0)
                svga.fullchange = changeframecount;
        svga.@override = val;
    }

    // pcem bug, reproduced: PB-35 — `svga->dac_read = val - 1` (vid_svga.c:124) vaut -1 après
    //   OUT 3C8h,0, et les cas 0 et 1 de la lecture de 3C9h (vid_svga.c:241-247)
    //   indexent alors vgapal[-1] : hors du tableau, dans le champ qui le PRÉCÈDE dans
    //   svga_t — `uint32_t pallook[512]`, sans bourrage puisque RGB est aligné sur un
    //   octet. vgapal[-1] désigne donc les octets 1 à 3 de pallook[511]. Le C lit sans
    //   broncher, le C# lèverait. Et pour une VGA la valeur est CONNUE : pallook n'est
    //   écrit qu'aux indices 0-255 (3C9h et svga_set_ramdac_type), pallook[511] reste
    //   donc au zéro du memset de vga_init.
    private static RGB vgapal_at(svga_t svga, int i)
    {
        if (i < 0)
        {
                uint32_t v = svga.pallook[511];
                return new RGB { r = (uint8_t)(v >> 8), g = (uint8_t)(v >> 16), b = (uint8_t)(v >> 24) };
        }
        return svga.vgapal[i];
    }

    // pcem: vid_svga.c:32-208
    internal static void svga_out(uint16_t addr, uint8_t val, object p)
    {
        svga_t svga = (svga_t)p;
        int c;
        uint8_t o;
        switch (addr)
        {
        case 0x3C0:
                if (svga.attrff == 0)
                {
                        svga.attraddr = val & 31;
                        if ((val & 0x20) != svga.attr_palette_enable)
                        {
                                svga.fullchange = 3;
                                svga.attr_palette_enable = val & 0x20;
                                svga_recalctimings(svga);
                        }
                }
                else
                {
                        if ((svga.attraddr == 0x13) && (svga.attrregs[0x13] != val))
                                svga.fullchange = changeframecount;
                        svga.attrregs[svga.attraddr & 31] = val;
                        if (svga.attraddr < 16)
                                svga.fullchange = changeframecount;
                        if (svga.attraddr == 0x10 || svga.attraddr == 0x14 || svga.attraddr < 0x10)
                        {
                                for (c = 0; c < 16; c++)
                                {
                                        if ((svga.attrregs[0x10] & 0x80) != 0)
                                                svga.egapal[c] = (uint8_t)((svga.attrregs[c] & 0xf) | ((svga.attrregs[0x14] & 0xf) << 4));
                                        else
                                                svga.egapal[c] =
                                                        (uint8_t)((svga.attrregs[c] & 0x3f) | ((svga.attrregs[0x14] & 0xc) << 4));
                                }
                        }
                        if (svga.attraddr == 0x10)
                                svga_recalctimings(svga);
                        if (svga.attraddr == 0x12)
                        {
                                if ((val & 0xf) != svga.plane_mask)
                                        svga.fullchange = changeframecount;
                                svga.plane_mask = (uint8_t)(val & 0xf);
                        }
                }
                svga.attrff ^= 1;
                break;
        case 0x3C2:
                svga.miscout = val;
                svga.vidclock = val & 4;
                io_removehandler(0x03a0, 0x0020, svga.video_in, null, null, svga.video_out, null, null, svga.p);
                if ((val & 1) == 0)
                        io_sethandler(0x03a0, 0x0020, svga.video_in, null, null, svga.video_out, null, null, svga.p);
                svga_recalctimings(svga);
                break;
        case 0x3C4:
                svga.seqaddr = val;
                break;
        case 0x3C5:
                if (svga.seqaddr > 0xf)
                        return;
                o = svga.seqregs[svga.seqaddr & 0xf];
                svga.seqregs[svga.seqaddr & 0xf] = val;
                if (o != val && (svga.seqaddr & 0xf) == 1)
                        svga_recalctimings(svga);
                switch (svga.seqaddr & 0xf)
                {
                case 1:
                        if (svga.scrblank != 0 && (val & 0x20) == 0)
                                svga.fullchange = 3;
                        svga.scrblank = (uint8_t)((svga.scrblank & ~0x20) | (val & 0x20));
                        svga_recalctimings(svga);
                        break;
                case 2:
                        svga.writemask = (uint8_t)(val & 0xf);
                        break;
                case 3:
                        svga.charsetb = (uint32_t)((((val >> 2) & 3) * 0x10000) + 2);
                        svga.charseta = (uint32_t)(((val & 3) * 0x10000) + 2);
                        if ((val & 0x10) != 0)
                                svga.charseta += 0x8000;
                        if ((val & 0x20) != 0)
                                svga.charsetb += 0x8000;
                        break;
                case 4:
                        svga.chain2_write = (val & 4) == 0 ? 1 : 0;
                        svga.chain4 = val & 8;
                        svga.fast = ((svga.gdcreg[8] == 0xff && (svga.gdcreg[3] & 0x18) == 0 && svga.gdcreg[1] == 0) &&
                                     ((svga.chain4 != 0 && svga.packed_chain4 != 0) || svga.fb_only != 0)) ? 1 : 0;
                        break;
                }
                break;
        case 0x3c6:
                svga.dac_mask = val;
                break;
        case 0x3C7:
                svga.dac_read = val;
                svga.dac_pos = 0;
                break;
        case 0x3C8:
                svga.dac_write = val;
                svga.dac_read = val - 1;
                svga.dac_pos = 0;
                break;
        case 0x3C9:
                svga.dac_status = 0;
                svga.fullchange = changeframecount;
                switch (svga.dac_pos)
                {
                case 0:
                        svga.dac_r = val;
                        svga.dac_pos++;
                        break;
                case 1:
                        svga.dac_g = val;
                        svga.dac_pos++;
                        break;
                case 2:
                        svga.vgapal[svga.dac_write].r = (uint8_t)svga.dac_r;
                        svga.vgapal[svga.dac_write].g = (uint8_t)svga.dac_g;
                        svga.vgapal[svga.dac_write].b = val;
                        if (svga.ramdac_type == RAMDAC_8BIT)
                                svga.pallook[svga.dac_write] =
                                        makecol32(svga.vgapal[svga.dac_write].r, svga.vgapal[svga.dac_write].g,
                                                  svga.vgapal[svga.dac_write].b);
                        else
                                svga.pallook[svga.dac_write] = makecol32((svga.vgapal[svga.dac_write].r & 0x3f) * 4,
                                                                         (svga.vgapal[svga.dac_write].g & 0x3f) * 4,
                                                                         (svga.vgapal[svga.dac_write].b & 0x3f) * 4);
                        svga.dac_pos = 0;
                        svga.dac_write = (svga.dac_write + 1) & 255;
                        break;
                }
                break;
        case 0x3CE:
                svga.gdcaddr = val;
                break;
        case 0x3CF:
                o = svga.gdcreg[svga.gdcaddr & 15];
                switch (svga.gdcaddr & 15)
                {
                case 2:
                        svga.colourcompare = val;
                        break;
                case 4:
                        svga.readplane = val & 3;
                        break;
                case 5:
                        svga.writemode = val & 3;
                        svga.readmode = val & 8;
                        svga.chain2_read = val & 0x10;
                        break;
                case 6:
                        if ((svga.gdcreg[6] & 0xc) != (val & 0xc))
                        {
                                switch (val & 0xC)
                                {
                                case 0x0: /*128k at A0000*/
                                        mem_mapping_set_addr(svga.mapping, 0xa0000, 0x20000);
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
                                        break;
                                }
                        }
                        break;
                case 7:
                        svga.colournocare = val;
                        break;
                }
                svga.gdcreg[svga.gdcaddr & 15] = val;
                svga.fast = ((svga.gdcreg[8] == 0xff && (svga.gdcreg[3] & 0x18) == 0 && svga.gdcreg[1] == 0) &&
                             ((svga.chain4 != 0 && svga.packed_chain4 != 0) || svga.fb_only != 0)) ? 1 : 0;
                if (((svga.gdcaddr & 15) == 5 && ((val ^ o) & 0x70) != 0) || ((svga.gdcaddr & 15) == 6 && ((val ^ o) & 1) != 0))
                        svga_recalctimings(svga);
                break;
        }
    }

    // pcem: vid_svga.c:210-273
    internal static uint8_t svga_in(uint16_t addr, object p)
    {
        svga_t svga = (svga_t)p;
        uint8_t temp;
        switch (addr)
        {
        case 0x3C0:
                return (uint8_t)(svga.attraddr | svga.attr_palette_enable);
        case 0x3C1:
                return svga.attrregs[svga.attraddr];
        case 0x3c2:
                if ((svga.vgapal[0].r + svga.vgapal[0].g + svga.vgapal[0].b) >= 0x50)
                        temp = 0;
                else
                        temp = 0x10;
                return temp;
        case 0x3C4:
                return (uint8_t)svga.seqaddr;
        case 0x3C5:
                return svga.seqregs[svga.seqaddr & 0xF];
        case 0x3c6:
                return svga.dac_mask;
        case 0x3c7:
                return svga.dac_status;
        case 0x3c8:
                return (uint8_t)svga.dac_write;
        case 0x3c9:
                svga.dac_status = 3;
                switch (svga.dac_pos)
                {
                case 0:
                        svga.dac_pos++;
                        if (svga.ramdac_type == RAMDAC_8BIT)
                                return vgapal_at(svga, svga.dac_read).r;
                        return (uint8_t)(vgapal_at(svga, svga.dac_read).r & 0x3f);
                case 1:
                        svga.dac_pos++;
                        if (svga.ramdac_type == RAMDAC_8BIT)
                                return vgapal_at(svga, svga.dac_read).g;
                        return (uint8_t)(vgapal_at(svga, svga.dac_read).g & 0x3f);
                case 2:
                        svga.dac_pos = 0;
                        svga.dac_read = (svga.dac_read + 1) & 255;
                        if (svga.ramdac_type == RAMDAC_8BIT)
                                return svga.vgapal[(svga.dac_read - 1) & 255].b;
                        return (uint8_t)(svga.vgapal[(svga.dac_read - 1) & 255].b & 0x3f);
                }
                break;
        case 0x3CC:
                return svga.miscout;
        case 0x3CE:
                return (uint8_t)svga.gdcaddr;
        case 0x3CF:
                return svga.gdcreg[svga.gdcaddr & 0xf];
        case 0x3DA:
                svga.attrff = 0;

                if ((svga.cgastat & 0x01) != 0)
                        svga.cgastat &= unchecked((uint8_t)~0x30);
                else
                        svga.cgastat ^= 0x30;
                return svga.cgastat;
        }
        return 0xFF;
    }

    // pcem: vid_svga.c:275-289
    internal static void svga_set_ramdac_type(svga_t svga, int type)
    {
        int c;

        if (svga.ramdac_type != type)
        {
                svga.ramdac_type = type;

                for (c = 0; c < 256; c++)
                {
                        if (svga.ramdac_type == RAMDAC_8BIT)
                                svga.pallook[c] = makecol32(svga.vgapal[c].r, svga.vgapal[c].g, svga.vgapal[c].b);
                        else
                                svga.pallook[c] = makecol32((svga.vgapal[c].r & 0x3f) * 4, (svga.vgapal[c].g & 0x3f) * 4,
                                                            (svga.vgapal[c].b & 0x3f) * 4);
                }
        }
    }

    // pcem: vid_svga.c:291-461
    internal static void svga_recalctimings(svga_t svga)
    {
        double crtcconst;
        double _dispontime, _dispofftime, disptime;

        svga.vtotal = svga.crtc[6];
        svga.dispend = svga.crtc[0x12];
        svga.vsyncstart = svga.crtc[0x10];
        svga.split = svga.crtc[0x18];
        svga.vblankstart = svga.crtc[0x15];

        if ((svga.crtc[7] & 1) != 0)
                svga.vtotal |= 0x100;
        if ((svga.crtc[7] & 32) != 0)
                svga.vtotal |= 0x200;
        svga.vtotal += 2;

        if ((svga.crtc[7] & 2) != 0)
                svga.dispend |= 0x100;
        if ((svga.crtc[7] & 64) != 0)
                svga.dispend |= 0x200;
        svga.dispend++;

        if ((svga.crtc[7] & 4) != 0)
                svga.vsyncstart |= 0x100;
        if ((svga.crtc[7] & 128) != 0)
                svga.vsyncstart |= 0x200;
        svga.vsyncstart++;

        if ((svga.crtc[7] & 0x10) != 0)
                svga.split |= 0x100;
        if ((svga.crtc[9] & 0x40) != 0)
                svga.split |= 0x200;
        svga.split++;

        if ((svga.crtc[7] & 0x08) != 0)
                svga.vblankstart |= 0x100;
        if ((svga.crtc[9] & 0x20) != 0)
                svga.vblankstart |= 0x200;
        svga.vblankstart++;

        svga.hdisp = svga.crtc[1];
        svga.hdisp++;

        svga.htotal = svga.crtc[0];
        svga.htotal += 6; /*+6 is required for Tyrian*/

        svga.rowoffset = svga.crtc[0x13];

        svga.clock = (svga.vidclock != 0) ? pit.VGACONST2 : pit.VGACONST1;

        svga.lowres = svga.attrregs[0x10] & 0x40;

        svga.interlace = 0;

        svga.ma_latch = (uint32_t)(((svga.crtc[0xc] << 8) | svga.crtc[0xd]) + ((svga.crtc[8] & 0x60) >> 5));
        svga.ca_adj = 0;

        svga.rowcount = svga.crtc[9] & 31;
        svga.linedbl = svga.crtc[9] & 0x80;

        svga.hdisp_time = svga.hdisp;
        svga.render = svga_render_blank;
        if (svga.scrblank == 0 && svga.attr_palette_enable != 0)
        {
                if ((svga.gdcreg[6] & 1) == 0 && (svga.attrregs[0x10] & 1) == 0) /*Text mode*/
                {
                        if ((svga.seqregs[1] & 8) != 0) /*40 column*/
                        {
                                svga.render = svga_render_text_40;
                                svga.hdisp *= ((svga.seqregs[1] & 1) != 0) ? 16 : 18;
                        }
                        else
                        {
                                svga.render = svga_render_text_80;
                                svga.hdisp *= ((svga.seqregs[1] & 1) != 0) ? 8 : 9;
                        }
                        svga.hdisp_old = svga.hdisp;
                }
                else
                {
                        svga.hdisp *= ((svga.seqregs[1] & 8) != 0) ? 16 : 8;
                        svga.hdisp_old = svga.hdisp;

                        switch (svga.gdcreg[5] & 0x60)
                        {
                        case 0x00:                        /*16 colours*/
                                if ((svga.seqregs[1] & 8) != 0) /*Low res (320)*/
                                        svga.render = svga_render_4bpp_lowres;
                                else
                                        svga.render = svga_render_4bpp_highres;
                                break;
                        case 0x20:                        /*4 colours*/
                                if ((svga.seqregs[1] & 8) != 0) /*Low res (320)*/
                                        svga.render = svga_render_2bpp_lowres;
                                else
                                        svga.render = svga_render_2bpp_highres;
                                break;
                        case 0x40:
                        case 0x60: /*256+ colours*/
                                switch (svga.bpp)
                                {
                                case 8:
                                        if (svga.lowres != 0)
                                                svga.render = svga_render_8bpp_lowres;
                                        else
                                                svga.render = svga_render_8bpp_highres;
                                        break;
                                case 15:
                                        if (svga.lowres != 0)
                                                svga.render = svga_render_15bpp_lowres;
                                        else
                                                svga.render = svga_render_15bpp_highres;
                                        break;
                                case 16:
                                        if (svga.lowres != 0)
                                                svga.render = svga_render_16bpp_lowres;
                                        else
                                                svga.render = svga_render_16bpp_highres;
                                        break;
                                case 24:
                                        if (svga.lowres != 0)
                                                svga.render = svga_render_24bpp_lowres;
                                        else
                                                svga.render = svga_render_24bpp_highres;
                                        break;
                                case 32:
                                        if (svga.lowres != 0)
                                                svga.render = svga_render_32bpp_lowres;
                                        else
                                                svga.render = svga_render_32bpp_highres;
                                        break;
                                }
                                break;
                        }
                }
        }

        svga.char_width = ((svga.seqregs[1] & 1) != 0) ? 8 : 9;
        if (svga.recalctimings_ex != null)
                svga.recalctimings_ex(svga);

        if (svga.vblankstart < svga.dispend)
                svga.dispend = svga.vblankstart;

        crtcconst = svga.clock * svga.char_width;

        disptime = svga.htotal;
        _dispontime = svga.hdisp_time;

        if ((svga.seqregs[1] & 8) != 0)
        {
                disptime *= 2;
                _dispontime *= 2;
        }
        _dispofftime = disptime - _dispontime;
        _dispontime *= crtcconst;
        _dispofftime *= crtcconst;

        // pcem bug, reproduced: PB-36 — `(uint64_t)_dispofftime` d'un double NÉGATIF, dès
        //   que hdisp_time dépasse htotal (CR01 + 1 > CR00 + 6). Le C ne le borne pas, et
        //   la conversion est de l'UB que GCC compile en cvttsd2si : le négatif devient
        //   (uint64_t)(int64_t)x, une valeur énorme que le `<` non signé de :450 laisse
        //   passer, et timer_advance_u64 fait alors RECULER le chronomètre. C# depuis .NET 9
        //   SATURE à 0, que la borne relevait à TIMER_USEC : une autre période de ligne. La
        //   double conversion reproduit GCC pour |x| < 2^63 — mesuré des deux côtés.
        svga.dispontime = unchecked((uint64_t)(int64_t)_dispontime);
        svga.dispofftime = unchecked((uint64_t)(int64_t)_dispofftime);
        if (svga.dispontime < TIMER_USEC)
                svga.dispontime = TIMER_USEC;
        if (svga.dispofftime < TIMER_USEC)
                svga.dispofftime = TIMER_USEC;

        svga_recalc_remap_func(svga);
    }

    // pcem: vid_svga.c:464-749
    internal static void svga_poll(object? p)
    {
        svga_t svga = (svga_t)p;
        int x;

        if (svga.linepos == 0)
        {
                if (svga.displine == svga.hwcursor_latch.y && svga.hwcursor_latch.ena != 0)
                {
                        svga.hwcursor_on = svga.hwcursor.ysize - svga.hwcursor_latch.yoff;
                        if (svga.hwcursor_on < 0)
                                svga.hwcursor_on = 0;
                        svga.hwcursor_oddeven = 0;
                }

                if (svga.displine == svga.hwcursor_latch.y + 1 && svga.hwcursor_latch.ena != 0 && svga.interlace != 0)
                {
                        svga.hwcursor_on = svga.hwcursor.ysize - svga.hwcursor_latch.yoff;
                        if (svga.hwcursor_on < 0)
                                svga.hwcursor_on = 0;
                        svga.hwcursor_oddeven = 1;
                }

                if (svga.displine == svga.overlay_latch.y && svga.overlay_latch.ena != 0)
                {
                        svga.overlay_on = svga.overlay_latch.ysize - svga.overlay_latch.yoff;
                        svga.overlay_oddeven = 0;
                }
                if (svga.displine == svga.overlay_latch.y + 1 && svga.overlay_latch.ena != 0 && svga.interlace != 0)
                {
                        svga.overlay_on = svga.overlay_latch.ysize - svga.overlay_latch.yoff;
                        svga.overlay_oddeven = 1;
                }

                timer_advance_u64(svga.timer, svga.dispofftime);
                svga.cgastat |= 1;
                svga.linepos = 1;

                if (svga.dispon != 0)
                {
                        svga.hdisp_on = 1;

                        svga.ma &= svga.vram_display_mask;
                        if (svga.firstline == 2000)
                        {
                                svga.firstline = svga.displine;
                                video_wait_for_buffer();
                        }

                        if (svga.hwcursor_on != 0 || svga.overlay_on != 0)
                                svga.changedvram[svga.ma >> 12] = svga.changedvram[(svga.ma >> 12) + 1] =
                                        (uint8_t)(svga.interlace != 0 ? 3 : 2);

                        if (svga.@override == 0)
                                svga.render!(svga);

                        if (svga.overlay_on != 0)
                        {
                                if (svga.@override == 0)
                                        svga.overlay_draw!(svga, svga.displine);
                                svga.overlay_on--;
                                if (svga.overlay_on != 0 && svga.interlace != 0)
                                        svga.overlay_on--;
                        }

                        if (svga.hwcursor_on != 0)
                        {
                                if (svga.@override == 0)
                                        svga.hwcursor_draw!(svga, svga.displine);
                                svga.hwcursor_on--;
                                if (svga.hwcursor_on != 0 && svga.interlace != 0)
                                        svga.hwcursor_on--;
                        }

                        if (svga.lastline < svga.displine)
                                svga.lastline = svga.displine;
                }

                svga.displine++;
                if (svga.interlace != 0)
                        svga.displine++;
                if ((svga.cgastat & 8) != 0 && ((svga.displine & 15) == (svga.crtc[0x11] & 15)) && svga.vslines != 0)
                {
                        svga.cgastat &= unchecked((uint8_t)~8);
                }
                svga.vslines++;
                if (svga.displine > 1500)
                        svga.displine = 0;
        }
        else
        {
                timer_advance_u64(svga.timer, svga.dispontime);

                if (svga.dispon != 0)
                        svga.cgastat &= unchecked((uint8_t)~1);
                svga.hdisp_on = 0;

                svga.linepos = 0;
                if (svga.sc == (svga.crtc[11] & 31))
                        svga.con = 0;
                if (svga.dispon != 0)
                {
                        if (svga.linedbl != 0 && svga.linecountff == 0)
                        {
                                svga.linecountff = 1;
                                svga.ma = svga.maback;
                        }
                        else if (svga.sc == svga.rowcount)
                        {
                                svga.linecountff = 0;
                                svga.sc = 0;

                                svga.maback += (uint32_t)(svga.rowoffset << 3);
                                if (svga.interlace != 0)
                                        svga.maback += (uint32_t)(svga.rowoffset << 3);
                                svga.maback &= svga.vram_display_mask;
                                svga.ma = svga.maback;
                        }
                        else
                        {
                                svga.linecountff = 0;
                                svga.sc++;
                                svga.sc &= 31;
                                svga.ma = svga.maback;
                        }
                }
                svga.hsync_divisor = svga.hsync_divisor == 0 ? 1 : 0;

                if (svga.hsync_divisor != 0 && (svga.crtc[0x17] & 4) != 0)
                        return;

                svga.vc++;
                svga.vc &= 2047;

                if (svga.vc == svga.split)
                {
                        int ret = 1;

                        if (svga.line_compare != null)
                                ret = svga.line_compare(svga);

                        if (ret != 0)
                        {
                                svga.ma = svga.maback = 0;
                                svga.sc = 0;
                                if ((svga.attrregs[0x10] & 0x20) != 0)
                                        svga.scrollcache = 0;
                        }
                }
                if (svga.vc == svga.dispend)
                {
                        int changed = 0;

                        if (svga.vblank_start != null)
                                svga.vblank_start(svga);
                        svga.dispon = 0;
                        if ((svga.crtc[10] & 0x20) != 0)
                                svga.cursoron = 0;
                        else
                                svga.cursoron = svga.blink & 16;
                        if ((svga.gdcreg[6] & 1) == 0 && (svga.blink & 15) == 0)
                                svga.fullchange = 2;
                        svga.blink++;

                        for (x = 0; x < ((svga.vram_mask + 1) >> 12); x++)
                        {
                                if (svga.changedvram[x] != 0)
                                {
                                        svga.changedvram[x]--;
                                        changed = 1;
                                }
                        }
                        if (svga.fullchange != 0)
                        {
                                svga.fullchange--;
                                // omitted: viewer_update(&viewer_palette, svga) et
                                //   viewer_update(&viewer_palette_16, svga) (vid_svga.c:628-629)
                                //   — visionneuses de débogage de l'UI, sorties pures.
                        }
                        if (changed != 0)
                        {
                                // omitted: viewer_update(&viewer_font, svga) et
                                //   viewer_update(&viewer_vram, svga) (vid_svga.c:632-633).
                        }
                }
                if (svga.vc == svga.vsyncstart)
                {
                        int wx, wy;
                        svga.dispon = 0;
                        svga.cgastat |= 8;
                        x = svga.hdisp;

                        if (svga.interlace != 0 && svga.oddeven == 0)
                                svga.lastline++;
                        if (svga.interlace != 0 && svga.oddeven != 0)
                                svga.firstline--;

                        wx = x;
                        wy = svga.lastline - svga.firstline;

                        if (svga.@override == 0)
                                svga_doblit(svga.firstline_draw, svga.lastline_draw + 1, wx, wy, svga);

                        readflash = 0;

                        svga.firstline = 2000;
                        svga.lastline = 0;

                        svga.firstline_draw = 2000;
                        svga.lastline_draw = 0;

                        svga.oddeven ^= 1;

                        changeframecount = svga.interlace != 0 ? 3 : 2;
                        svga.vslines = 0;

                        if (svga.interlace != 0 && svga.oddeven != 0)
                                svga.ma = svga.maback = svga.ma_latch + (uint32_t)(svga.rowoffset << 1);
                        else
                                svga.ma = svga.maback = svga.ma_latch;
                        svga.ca = (uint32_t)(((svga.crtc[0xe] << 8) | svga.crtc[0xf]) + svga.ca_adj);

                        svga.ma <<= 2;
                        svga.maback <<= 2;
                        svga.ca <<= 2;

                        if (svga.video_res_override == 0)
                        {
                                svga.video_res_x = wx;
                                svga.video_res_y = wy + 1;

                                if ((svga.gdcreg[6] & 1) == 0 && (svga.attrregs[0x10] & 1) == 0) /*Text mode*/
                                {
                                        svga.video_res_x /= svga.char_width;
                                        svga.video_res_y /= (svga.crtc[9] & 31) + 1;
                                        svga.video_bpp = 0;
                                }
                                else
                                {
                                        if ((svga.crtc[9] & 0x80) != 0)
                                                svga.video_res_y /= 2;
                                        if ((svga.crtc[0x17] & 2) == 0)
                                                svga.video_res_y *= 4;
                                        else if ((svga.crtc[0x17] & 1) == 0)
                                                svga.video_res_y *= 2;
                                        svga.video_res_y /= (svga.crtc[9] & 31) + 1;
                                        if (svga.render == svga_render_8bpp_lowres || svga.render == svga_render_15bpp_lowres ||
                                            svga.render == svga_render_16bpp_lowres ||
                                            svga.render == svga_render_24bpp_lowres || svga.render == svga_render_32bpp_lowres)
                                                svga.video_res_x /= 2;

                                        switch (svga.gdcreg[5] & 0x60)
                                        {
                                        case 0x00:
                                                svga.video_bpp = 4;
                                                break;
                                        case 0x20:
                                                svga.video_bpp = 2;
                                                break;
                                        case 0x40:
                                        case 0x60:
                                                svga.video_bpp = svga.bpp;
                                                break;
                                        }
                                }
                        }

                        if (svga.vsync_callback != null)
                                svga.vsync_callback(svga);
                }
                if (svga.vc == svga.vtotal)
                {
                        svga.vc = 0;
                        svga.sc = svga.crtc[8] & 0x1f;
                        svga.dispon = 1;
                        svga.displine = (svga.interlace != 0 && svga.oddeven != 0) ? 1 : 0;
                        svga.scrollcache = svga.attrregs[0x13] & 7;
                        svga.linecountff = 0;

                        svga.hwcursor_on = 0;
                        svga.hwcursor_latch = svga.hwcursor;

                        svga.overlay_on = 0;
                        svga.overlay_latch = svga.overlay;
                }
                if (svga.sc == (svga.crtc[10] & 31))
                        svga.con = 1;
        }
    }

    // pcem: vid_svga.c:751-802
    internal static int svga_init(svga_t svga, object? p, int memsize, svga_render_fn? recalctimings_ex,
                                  inb_fn? video_in, outb_fn? video_out,
                                  svga_draw_fn? hwcursor_draw,
                                  svga_draw_fn? overlay_draw)
    {
        int c, d, e;

        svga.p = p;

        for (c = 0; c < 256; c++)
        {
                e = c;
                for (d = 0; d < 8; d++)
                {
                        svga_rotate[d, c] = (uint8_t)e;
                        e = (e >> 1) | (((e & 1) != 0) ? 0x80 : 0);
                }
        }
        svga.readmode = 0;

        svga.crtc[0] = 63;
        svga.crtc[6] = 255;
        svga.dispontime = 1000UL << 32;
        svga.dispofftime = 1000UL << 32;
        svga.bpp = 8;
        // DEVIATION: malloc(memsize) SANS effacement, donc du tas en C ; un tableau CLR
        //   est nul. L'oracle efface les deux tableaux après device_add (harness.c) :
        //   même arbitrage que h_pad_ram et PB-24.
        svga.vram = new uint8_t[memsize];
        svga.vram_max = (uint32_t)memsize;
        svga.vram_display_mask = (uint32_t)(memsize - 1);
        svga.vram_mask = (uint32_t)(memsize - 1);
        svga.decode_mask = 0x7fffff;
        svga.changedvram = new uint8_t[/*(memsize >> 12) << 1*/ 0x1000000 >> 12];
        svga.recalctimings_ex = recalctimings_ex;
        svga.video_in = video_in;
        svga.video_out = video_out;
        svga.hwcursor_draw = hwcursor_draw;
        svga.overlay_draw = overlay_draw;
        svga.hwcursor.ysize = 64;
        svga.ksc5601_english_font_type = 0;

        mem_mapping_add(svga.mapping, 0xa0000, 0x20000, svga_read, svga_readw, svga_readl, svga_write, svga_writew, svga_writel,
                        null, 0, MEM_MAPPING_EXTERNAL, svga);

        timer_add(svga.timer, svga_poll, svga, 1);

        svga_pri = svga;

        svga.ramdac_type = RAMDAC_6BIT;

        // omitted: les quatre viewer_add (vid_svga.c:796-799) — visionneuses de
        //   débogage de l'UI.

        return 0;
    }

    // pcem: vid_svga.c:804-809
    internal static void svga_close(svga_t svga)
    {
        // omitted: free(svga->changedvram) et free(svga->vram) (vid_svga.c:805-806) —
        //   libération manuelle, sans objet sous GC.

        svga_pri = null;
    }

    // pcem: vid_svga.c:811-1060
    internal static void svga_write(uint32_t addr, uint8_t val, object? p)
    {
        svga_t svga = (svga_t)p;
        uint8_t vala, valb, valc, vald, wm = svga.writemask;
        int writemask2 = svga.writemask;

        egawrites++;

        cycles -= video_timing_write_b;
        cycles_lost += video_timing_write_b;

        if (svga_output != 0)
                pclog($"Writeega {addr:X6}   ");
        addr &= svga.banked_mask;
        addr += svga.write_bank;

        if ((svga.gdcreg[6] & 1) == 0)
                svga.fullchange = 2;
        if ((svga.chain4 != 0 && svga.packed_chain4 != 0) || svga.fb_only != 0)
        {
                writemask2 = 1 << (int)(addr & 3);
                addr &= ~3u;
        }
        else if (svga.chain4 != 0)
        {
                writemask2 = 1 << (int)(addr & 3);
                addr &= ~3u;
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

        if (svga_output != 0)
                pclog($"{addr:X8} ({addr & 1023}, {addr >> 10}) {val:X2} {writemask2} {svga.writemode} {svga.chain4} {svga.gdcreg[8]:X2}\n");
        svga.changedvram[addr >> 12] = (uint8_t)changeframecount;

        switch (svga.writemode)
        {
        case 1:
                if ((writemask2 & 1) != 0)
                        svga.vram[addr] = svga.la;
                if ((writemask2 & 2) != 0)
                        svga.vram[addr | 0x1] = svga.lb;
                if ((writemask2 & 4) != 0)
                        svga.vram[addr | 0x2] = svga.lc;
                if ((writemask2 & 8) != 0)
                        svga.vram[addr | 0x3] = svga.ld;
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
                if ((svga.gdcreg[3] & 0x18) == 0 && (svga.gdcreg[1] == 0 || svga.set_reset_disabled != 0))
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

    // pcem: vid_svga.c:1062-1135
    internal static uint8_t svga_read(uint32_t addr, object? p)
    {
        svga_t svga = (svga_t)p;
        uint8_t temp, temp2, temp3, temp4;
        uint32_t latch_addr;
        int readplane = svga.readplane;

        cycles -= video_timing_read_b;
        cycles_lost += video_timing_read_b;

        egareads++;

        addr &= svga.banked_mask;
        addr += svga.read_bank;

        latch_addr = (addr << 2) & svga.decode_mask;

        if ((svga.chain4 != 0 && svga.packed_chain4 != 0) || svga.fb_only != 0)
        {
                addr &= svga.decode_mask;
                if (addr >= svga.vram_max)
                        return 0xff;
                latch_addr = addr & svga.vram_mask & ~3u;
                svga.la = svga.vram[latch_addr];
                svga.lb = svga.vram[latch_addr | 0x1];
                svga.lc = svga.vram[latch_addr | 0x2];
                svga.ld = svga.vram[latch_addr | 0x3];
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
        }
        else
        {
                latch_addr &= svga.vram_mask;
                svga.la = svga.vram[latch_addr];
                svga.lb = svga.vram[latch_addr | 0x1];
                svga.lc = svga.vram[latch_addr | 0x2];
                svga.ld = svga.vram[latch_addr | 0x3];
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

    // pcem: vid_svga.c:1137-1378 — G7.0. Le corps du switch est celui de svga_write, à la
    // ligne près (vérifié par diff) ; seule la tête change : pas de banque.
    internal static void svga_write_linear(uint32_t addr, uint8_t val, object? p)
    {
        svga_t svga = (svga_t)p;
        uint8_t vala, valb, valc, vald, wm = svga.writemask;
        int writemask2 = svga.writemask;

        cycles -= video_timing_write_b;
        cycles_lost += video_timing_write_b;

        egawrites++;

        if (svga_output != 0)
                pclog($"Write LFB {addr:X8} {val:X2} ");

        if ((svga.gdcreg[6] & 1) == 0)
                svga.fullchange = 2;
        if ((svga.chain4 != 0 && svga.packed_chain4 != 0) || svga.fb_only != 0)
        {
                writemask2 = 1 << (int)(addr & 3);
                addr &= ~3u;
        }
        else if (svga.chain4 != 0)
        {
                // Pas de `addr &= ~3` ici, à la différence de svga_write (vid_svga.c:833) :
                // les deux bits bas ne survivent pas au décalage de toute façon.
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

        if (svga_output != 0)
                pclog($"{addr:X8} ({addr & 1023}, {addr >> 10}) {val:X2} {writemask2} {svga.writemode} {svga.chain4} {svga.gdcreg[8]:X2}\n");
        svga.changedvram[addr >> 12] = (uint8_t)changeframecount;

        switch (svga.writemode)
        {
        case 1:
                if ((writemask2 & 1) != 0)
                        svga.vram[addr] = svga.la;
                if ((writemask2 & 2) != 0)
                        svga.vram[addr | 0x1] = svga.lb;
                if ((writemask2 & 4) != 0)
                        svga.vram[addr | 0x2] = svga.lc;
                if ((writemask2 & 8) != 0)
                        svga.vram[addr | 0x3] = svga.ld;
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
                if ((svga.gdcreg[3] & 0x18) == 0 && (svga.gdcreg[1] == 0 || svga.set_reset_disabled != 0))
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

    // pcem: vid_svga.c:1380-1441 — G7.0.
    internal static uint8_t svga_read_linear(uint32_t addr, object? p)
    {
        svga_t svga = (svga_t)p;
        uint8_t temp, temp2, temp3, temp4;
        int readplane = svga.readplane;
        uint32_t latch_addr = (addr << 2) & svga.decode_mask;

        cycles -= video_timing_read_b;
        cycles_lost += video_timing_read_b;

        egareads++;

        // EN CHAIN4 COMPACT, LES VERROUS NE SONT PAS CHARGÉS : svga_read les recharge depuis
        // `addr & ~3` (vid_svga.c:1085-1089), la forme linéaire rend l'octet et sort. Une
        // écriture en mode 1 qui suit recopie donc les verrous de la lecture d'AVANT.
        // pcem bug, reproduced: PB-80
        if ((svga.chain4 != 0 && svga.packed_chain4 != 0) || svga.fb_only != 0)
        {
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
        {
                addr <<= 2;
        }

        addr &= svga.decode_mask;

        if (latch_addr >= svga.vram_max)
        {
                svga.la = svga.lb = svga.lc = svga.ld = 0xff;
        }
        else
        {
                latch_addr &= svga.vram_mask;
                svga.la = svga.vram[latch_addr];
                svga.lb = svga.vram[latch_addr | 0x1];
                svga.lc = svga.vram[latch_addr | 0x2];
                svga.ld = svga.vram[latch_addr | 0x3];
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

    // pcem: vid_svga.c:1443-1472
    internal static void svga_doblit(int y1, int y2, int wx, int wy, svga_t svga)
    {
        svga.frames++;
        if (y1 > y2)
        {
                video_blit_memtoscreen(32, 0, 0, 0, xsize, ysize);
                return;
        }

        if ((wx != xsize || wy != ysize) && vid_resize == 0)
        {
                xsize = wx;
                ysize = wy + 1;
                if (xsize < 64)
                        xsize = 656;
                if (ysize < 32)
                        ysize = 200;

                if (svga.vertical_linedbl != 0)
                        updatewindowsize(xsize, ysize * 2);
                else
                        updatewindowsize(xsize, ysize);
        }
        if (vid_resize != 0)
        {
                xsize = wx;
                ysize = wy + 1;
        }
        video_blit_memtoscreen(32, 0, y1, y2, xsize, ysize);
    }

    // pcem: vid_svga.c:1474-1498
    //
    // DEVIATION: `*(uint16_t *)&svga->vram[addr] = val` — écriture par cast de
    //   pointeur, reconstruite octet par octet en petit-boutien, comme rom_readw
    //   (rom.cs). Même chose pour les trois variantes suivantes.
    internal static void svga_writew(uint32_t addr, uint16_t val, object? p)
    {
        svga_t svga = (svga_t)p;
        if (svga.fast == 0)
        {
                svga_write(addr, (uint8_t)val, p);
                svga_write(addr + 1, (uint8_t)(val >> 8), p);
                return;
        }

        egawrites += 2;

        cycles -= video_timing_write_w;
        cycles_lost += video_timing_write_w;

        if (svga_output != 0)
                pclog($"svga_writew: {addr:X5} ");
        addr = (addr & svga.banked_mask) + svga.write_bank;
        addr &= svga.decode_mask;
        if (addr >= svga.vram_max)
                return;
        addr &= svga.vram_mask;
        if (svga_output != 0)
                pclog($"{addr:X8} ({addr & 1023}, {addr >> 10}) {val:X4}\n");
        svga.changedvram[addr >> 12] = (uint8_t)changeframecount;
        svga.vram[addr] = (uint8_t)val;
        svga.vram[addr + 1] = (uint8_t)(val >> 8);
    }

    // pcem: vid_svga.c:1500-1528
    internal static void svga_writel(uint32_t addr, uint32_t val, object? p)
    {
        svga_t svga = (svga_t)p;

        if (svga.fast == 0)
        {
                svga_write(addr, (uint8_t)val, p);
                svga_write(addr + 1, (uint8_t)(val >> 8), p);
                svga_write(addr + 2, (uint8_t)(val >> 16), p);
                svga_write(addr + 3, (uint8_t)(val >> 24), p);
                return;
        }

        egawrites += 4;

        cycles -= video_timing_write_l;
        cycles_lost += video_timing_write_l;

        if (svga_output != 0)
                pclog($"svga_writel: {addr:X5} ");
        addr = (addr & svga.banked_mask) + svga.write_bank;
        addr &= svga.decode_mask;
        if (addr >= svga.vram_max)
                return;
        addr &= svga.vram_mask;
        if (svga_output != 0)
                pclog($"{addr:X8} ({addr & 1023}, {addr >> 10}) {val:X8}\n");

        svga.changedvram[addr >> 12] = (uint8_t)changeframecount;
        svga.vram[addr] = (uint8_t)val;
        svga.vram[addr + 1] = (uint8_t)(val >> 8);
        svga.vram[addr + 2] = (uint8_t)(val >> 16);
        svga.vram[addr + 3] = (uint8_t)(val >> 24);
    }

    // pcem: vid_svga.c:1530-1549
    //
    // L'ORDRE D'ÉVALUATION des deux svga_read de la ligne 1534 n'est PAS fixé par le C —
    // les deux opérandes d'un `|` sont non séquencés — et chacun a des effets de bord :
    // les verrous la..ld, les cycles, egareads. C# évalue de gauche à droite. Vérifié sur
    // l'oracle compilé (objdump de build/vid_svga.o) : GCC appelle aussi svga_read(addr)
    // PUIS svga_read(addr + 1). Si l'oracle change de compilateur, c'est ici que ça
    // divergera, sur les verrous.
    internal static uint16_t svga_readw(uint32_t addr, object? p)
    {
        svga_t svga = (svga_t)p;

        if (svga.fast == 0)
                return (uint16_t)(svga_read(addr, p) | (svga_read(addr + 1, p) << 8));

        egareads += 2;

        cycles -= video_timing_read_w;
        cycles_lost += video_timing_read_w;

        addr = (addr & svga.banked_mask) + svga.read_bank;
        addr &= svga.decode_mask;
        if (addr >= svga.vram_max)
                return 0xffff;

        return (uint16_t)(svga.vram[addr & svga.vram_mask] | (svga.vram[(addr & svga.vram_mask) + 1] << 8));
    }

    // pcem: vid_svga.c:1551-1571
    internal static uint32_t svga_readl(uint32_t addr, object? p)
    {
        svga_t svga = (svga_t)p;

        if (svga.fast == 0)
                return (uint32_t)(svga_read(addr, p) | (svga_read(addr + 1, p) << 8) | (svga_read(addr + 2, p) << 16) |
                                  (svga_read(addr + 3, p) << 24));

        egareads += 4;

        cycles -= video_timing_read_l;
        cycles_lost += video_timing_read_l;

        addr = (addr & svga.banked_mask) + svga.read_bank;
        addr &= svga.decode_mask;
        if (addr >= svga.vram_max)
                return 0xffffffff;

        return (uint32_t)(svga.vram[addr & svga.vram_mask] | (svga.vram[(addr & svga.vram_mask) + 1] << 8) |
                          (svga.vram[(addr & svga.vram_mask) + 2] << 16) | (svga.vram[(addr & svga.vram_mask) + 3] << 24));
    }

    // pcem: vid_svga.c:1573-1594 — G7.0.
    internal static void svga_writew_linear(uint32_t addr, uint16_t val, object? p)
    {
        svga_t svga = (svga_t)p;

        if (svga.fast == 0)
        {
                svga_write_linear(addr, (uint8_t)val, p);
                svga_write_linear(addr + 1, (uint8_t)(val >> 8), p);
                return;
        }

        egawrites += 2;

        cycles -= video_timing_write_w;
        cycles_lost += video_timing_write_w;

        if (svga_output != 0)
                pclog($"Write LFBw {addr:X8} {val:X4}\n");
        addr &= svga.decode_mask;
        if (addr >= svga.vram_max)
                return;
        addr &= svga.vram_mask;
        svga.changedvram[addr >> 12] = (uint8_t)changeframecount;
        svga.vram[addr] = (uint8_t)val;
        svga.vram[addr + 1] = (uint8_t)(val >> 8);
    }

    // pcem: vid_svga.c:1596-1620 — G7.0.
    internal static void svga_writel_linear(uint32_t addr, uint32_t val, object? p)
    {
        svga_t svga = (svga_t)p;

        if (svga.fast == 0)
        {
                svga_write_linear(addr, (uint8_t)val, p);
                svga_write_linear(addr + 1, (uint8_t)(val >> 8), p);
                svga_write_linear(addr + 2, (uint8_t)(val >> 16), p);
                svga_write_linear(addr + 3, (uint8_t)(val >> 24), p);
                return;
        }

        egawrites += 4;

        cycles -= video_timing_write_l;
        cycles_lost += video_timing_write_l;

        if (svga_output != 0)
                pclog($"Write LFBl {addr:X8} {val:X8}\n");
        addr &= svga.decode_mask;
        if (addr >= svga.vram_max)
                return;
        addr &= svga.vram_mask;
        svga.changedvram[addr >> 12] = (uint8_t)changeframecount;
        svga.vram[addr] = (uint8_t)val;
        svga.vram[addr + 1] = (uint8_t)(val >> 8);
        svga.vram[addr + 2] = (uint8_t)(val >> 16);
        svga.vram[addr + 3] = (uint8_t)(val >> 24);
    }

    // pcem: vid_svga.c:1622-1638 — G7.0. L'ordre des deux lectures non rapides est celui de
    // svga_readw (gauche puis droite), comme GCC le compile.
    internal static uint16_t svga_readw_linear(uint32_t addr, object? p)
    {
        svga_t svga = (svga_t)p;

        if (svga.fast == 0)
                return (uint16_t)(svga_read_linear(addr, p) | (svga_read_linear(addr + 1, p) << 8));

        egareads += 2;

        cycles -= video_timing_read_w;
        cycles_lost += video_timing_read_w;

        addr &= svga.decode_mask;
        if (addr >= svga.vram_max)
                return 0xffff;

        return (uint16_t)(svga.vram[addr & svga.vram_mask] | (svga.vram[(addr & svga.vram_mask) + 1] << 8));
    }

    // pcem: vid_svga.c:1640-1658 — G7.0.
    internal static uint32_t svga_readl_linear(uint32_t addr, object? p)
    {
        svga_t svga = (svga_t)p;

        if (svga.fast == 0)
                return (uint32_t)(svga_read_linear(addr, p) | (svga_read_linear(addr + 1, p) << 8) |
                                  (svga_read_linear(addr + 2, p) << 16) | (svga_read_linear(addr + 3, p) << 24));

        egareads += 4;

        cycles -= video_timing_read_l;
        cycles_lost += video_timing_read_l;

        addr &= svga.decode_mask;
        if (addr >= svga.vram_max)
                return 0xffffffff;

        return (uint32_t)(svga.vram[addr & svga.vram_mask] | (svga.vram[(addr & svga.vram_mask) + 1] << 8) |
                          (svga.vram[(addr & svga.vram_mask) + 2] << 16) | (svga.vram[(addr & svga.vram_mask) + 3] << 24));
    }

    /// <summary>Sonde de diagnostic — pendant exact de h_vga_probe()
    /// (tools/oracle/harness.c). Même ordre de champs, mêmes hachages FNV-1a sur les
    /// mêmes octets, pour que la comparaison de fin de course NOMME le champ divergent :
    /// le diff d'instructions ne voit ni la palette, ni la police du plan 2, ni les
    /// pixels. Tout à zéro sans carte svga.</summary>
    internal static void Probe(uint64_t[] o)
    {
        svga_t? svga = svga_pri;
        int f = 0, c;

        Array.Clear(o);
        if (svga == null)
                return;

        var pal = new uint8_t[256 * 3];
        for (c = 0; c < 256; c++)
        {
                pal[c * 3] = svga.vgapal[c].r;
                pal[c * 3 + 1] = svga.vgapal[c].g;
                pal[c * 3 + 2] = svga.vgapal[c].b;
        }

        o[f++] = 1;
        o[f++] = Fnv(svga.vram.AsSpan(0, (int)svga.vram_max));
        o[f++] = Fnv(svga.changedvram);
        o[f++] = Fnv(System.Runtime.InteropServices.MemoryMarshal.AsBytes(Buffer32.AsSpan()));
        o[f++] = Fnv(svga.crtc);
        o[f++] = Fnv(svga.seqregs);
        o[f++] = Fnv(svga.gdcreg);
        o[f++] = Fnv(svga.attrregs);
        o[f++] = Fnv(pal);
        o[f++] = Fnv(System.Runtime.InteropServices.MemoryMarshal.AsBytes(svga.pallook.AsSpan()));
        o[f++] = Fnv(svga.egapal);
        o[f++] = svga.miscout;
        o[f++] = svga.crtcreg;
        o[f++] = (uint64_t)(long)svga.seqaddr;
        o[f++] = (uint64_t)(long)svga.gdcaddr;
        o[f++] = (uint64_t)(long)svga.attraddr;
        o[f++] = (uint64_t)(long)svga.attrff;
        o[f++] = (uint64_t)(long)svga.attr_palette_enable;
        o[f++] = svga.dac_mask;
        o[f++] = svga.dac_status;
        o[f++] = (uint64_t)(long)svga.dac_read;
        o[f++] = (uint64_t)(long)svga.dac_write;
        o[f++] = (uint64_t)(long)svga.dac_pos;
        o[f++] = svga.la | ((uint64_t)svga.lb << 8) | ((uint64_t)svga.lc << 16) | ((uint64_t)svga.ld << 24);
        o[f++] = (uint64_t)(long)svga.writemode;
        o[f++] = (uint64_t)(long)svga.readmode;
        o[f++] = (uint64_t)(long)svga.readplane;
        o[f++] = (uint64_t)(long)svga.chain4;
        o[f++] = (uint64_t)(long)svga.chain2_write;
        o[f++] = (uint64_t)(long)svga.chain2_read;
        o[f++] = svga.writemask;
        o[f++] = svga.plane_mask;
        o[f++] = svga.charseta;
        o[f++] = svga.charsetb;
        o[f++] = svga.banked_mask;
        o[f++] = svga.mapping.@base;
        o[f++] = svga.mapping.size;
        o[f++] = svga.cgastat;
        o[f++] = (uint64_t)(long)svga.vc;
        o[f++] = (uint64_t)(long)svga.sc;
        o[f++] = (uint64_t)(long)svga.displine;
        o[f++] = (uint64_t)(long)svga.linepos;
        o[f++] = (uint64_t)(long)svga.dispon;
        o[f++] = svga.ma;
        o[f++] = svga.maback;
        o[f++] = svga.ca;
        o[f++] = (uint64_t)(long)svga.vtotal;
        o[f++] = (uint64_t)(long)svga.dispend;
        o[f++] = (uint64_t)(long)svga.hdisp;
        o[f++] = (uint64_t)(long)svga.htotal;
        o[f++] = svga.dispontime;
        o[f++] = svga.dispofftime;
        o[f++] = svga.timer.ts_integer;
        o[f++] = svga.timer.ts_frac;
        o[f++] = (uint64_t)(long)svga.fullchange;
        o[f++] = (uint64_t)(long)svga.frames;
        o[f++] = (uint64_t)(long)svga.firstline;
        o[f++] = (uint64_t)(long)svga.lastline;
        o[f++] = (uint64_t)(long)xsize;
        o[f++] = (uint64_t)(long)ysize;
        o[f++] = (uint64_t)(long)svga.video_res_x;
        o[f++] = (uint64_t)(long)svga.video_res_y;
        o[f++] = (uint64_t)(long)svga.video_bpp;
        o[f++] = (uint64_t)(long)svga.blink;
        // M19 — ce que la Trident rend atteignable et que les 64 premiers ne voyaient pas.
        o[f++] = svga.read_bank;
        o[f++] = svga.write_bank;
        o[f++] = (uint64_t)(long)svga.bpp;
        o[f++] = svga.vram_display_mask;
        o[f++] = svga.vram_mask;
        o[f++] = (uint64_t)(long)svga.rowoffset;
        o[f++] = svga.ma_latch;
        o[f++] = (uint64_t)(long)svga.interlace;
        o[f++] = (uint64_t)(long)svga.lowres;
        o[f++] = (uint64_t)(long)svga.hdisp_time;
        o[f++] = BitConverter.DoubleToUInt64Bits(svga.clock);
        // Les onze champs de la Trident : sa tvga_t et son RAMDAC. Zéro pour une autre
        // carte — svga.p n'est une tvga_t que si la carte en est une.
        if (svga.p is tvga_t tvga)
        {
                o[f++] = tvga.id;
                o[f++] = (uint64_t)(long)tvga.oldmode;
                o[f++] = tvga.tvga_3d8;
                o[f++] = tvga.tvga_3d9;
                o[f++] = tvga.oldctrl1;
                o[f++] = tvga.oldctrl2;
                o[f++] = tvga.newctrl2;
                o[f++] = (uint64_t)(long)tvga.vram_size;
                o[f++] = tvga.vram_mask;
                o[f++] = (uint64_t)(long)tvga.ramdac.state;
                o[f++] = tvga.ramdac.ctrl;
        }
    }

    // FNV-1a 64 bits, le h_fnv de harness.c.
    private static uint64_t Fnv(ReadOnlySpan<uint8_t> p)
    {
        uint64_t hash = 1469598103934665603UL;
        foreach (var b in p)
        {
                hash ^= b;
                hash *= 1099511628211UL;
        }
        return hash;
    }

    // DEVIATION: strncat(s, temps, max_len) de la libc, sur le StringBuilder que
    //   device_add_status_info_fn reçoit à la place du `char *`.
    private static void strncat(StringBuilder s, string temps, int max_len)
        => s.Append(temps.Length > max_len ? temps[..max_len] : temps);

    // pcem: vid_svga.c:1660-1682
    internal static void svga_add_status_info(StringBuilder s, int max_len, object p)
    {
        svga_t svga = (svga_t)p;
        string temps;

        if (svga.chain4 != 0)
                temps = "SVGA chained (possibly mode 13h)\n";
        else
                temps = "SVGA unchained (possibly mode-X)\n";
        strncat(s, temps, max_len);

        if (svga.video_bpp == 0)
                temps = "SVGA in text mode\n";
        else
                temps = $"SVGA colour depth : {svga.video_bpp} bpp\n";
        strncat(s, temps, max_len);

        temps = $"SVGA resolution : {svga.video_res_x} x {svga.video_res_y}\n";
        strncat(s, temps, max_len);

        temps = $"SVGA refresh rate : {svga.frames} Hz\n\n";
        svga.frames = 0;
        strncat(s, temps, max_len);
    }
}
