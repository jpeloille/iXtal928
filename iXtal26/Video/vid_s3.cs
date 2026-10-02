// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/video/vid_s3.c
// STATUS: partial — s3_t, la FIFO (anneau, s3_queue, drainage SYNCHRONE), s3_update_irqs,
//         s3_accel_out_fifo et ses formes _w/_l, s3_accel_write_fifo et ses formes _w/_l,
//         s3_vblank_start, s3_out/s3_in, s3_recalctimings, s3_updatemapping,
//         s3_trio64_getclock, s3_accel_out/_w/_l, s3_accel_in, s3_accel_write/_w/_l,
//         s3_accel_read, polygon_setup, dword_remap*, l'accélérateur 2D (s3_accel_start :
//         ligne, rectangle, BitBlt, motif, polygones Trio64), s3_hwcursor_draw,
//         s3_io_remove/s3_io_set, s3_pci_read/s3_pci_write, s3_init, la Trio64 Phoenix
//         (init, available, config, device), s3_close, s3_speed_changed, s3_force_redraw,
//         s3_add_status_info. Omis : les trois autres cartes (Bahamas 64, 9FX, Trio32 Phoenix),
//         la RAMDAC SDAC de la Vision864, le fil FIFO (G7 n° 3 : accélérateur synchrone),
//         le câblage PCI des IRQ (PB-83).

// CS8600/CS8602/CS8604 : même raison qu'à vid_svga.cs et vid_cga.cs:9-17.
#pragma warning disable CS8600, CS8602, CS8604

// CS1717 : `dest_dat = dest_dat;` (vid_s3.c:1723), mix 0x3 de la macro MIX — une
// auto-affectation chez PCem aussi ; on garde la ligne pour que le switch se lise en regard du C.
#pragma warning disable CS1717

using System.Runtime.InteropServices;
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
using static iXtal26.Video.vid_svga;
using static iXtal26.Video.vid_svga_render;
using static iXtal26.io;

namespace iXtal26.Video;

// pcem: vid_s3.c:75 — `float (*getclock)(int clock, void *p)`, déclarateur en ligne ; C# exige
//   un nom.
internal delegate float s3_getclock_fn(int clock, object? p);

// pcem: bus/pci.h — les deux pointeurs de fonction que reçoit pci_add (pci.c:185) ; pci.h
//   n'est pas transcrit, C# exige un nom.
internal delegate uint8_t pci_card_read_fn(int func, int addr, object p);
internal delegate void pci_card_write_fn(int func, int addr, uint8_t val, object p);

// pcem: vid_s3.c:41-44
internal struct fifo_entry_t
{
    internal uint32_t addr_type;
    internal uint32_t val;
}

// pcem: vid_s3.c:78-116
// DEVIATION: structure ANONYME en C ; C# exige un nom. Classe : elle n'est jamais copiée,
//   seulement atteinte par s3->accel.
internal sealed class s3_accel_t
{
    internal uint8_t subsys_cntl;
    internal uint8_t setup_md;
    internal uint8_t advfunc_cntl;
    internal uint16_t cur_y, cur_y2;
    internal uint16_t cur_x, cur_x2;
    internal uint16_t x2;
    internal int16_t desty_axstp, desty_axstp2;
    internal int16_t destx_distp;
    internal int16_t err_term, err_term2;
    internal int16_t maj_axis_pcnt, maj_axis_pcnt2;
    internal uint16_t cmd;
    internal uint16_t short_stroke;
    internal uint32_t bkgd_color;
    internal uint32_t frgd_color;
    internal uint32_t wrt_mask;
    internal uint32_t rd_mask;
    internal uint32_t color_cmp;
    internal uint8_t bkgd_mix;
    internal uint8_t frgd_mix;
    internal uint16_t multifunc_cntl;
    internal uint16_t[] multifunc = new uint16_t[16];
    internal uint8_t[] pix_trans = new uint8_t[4];

    internal int cx, cy;
    internal int sx, sy;
    internal int dx, dy;
    internal uint32_t src, dest, pattern;
    internal int pix_trans_count;

    internal int poly_cx, poly_cx2;
    internal int poly_cy, poly_cy2;
    internal int point_1_updated, point_2_updated;
    internal int poly_dx1, poly_dx2;
    internal int poly_x;

    internal uint32_t dat_buf;
    internal int dat_count;
}

// pcem: vid_s3.c:46-135
// Classe et non struct : son adresse est prise (svga_init, io_sethandler, mem_mapping_add).
internal sealed class s3_t
{
    internal mem_mapping_t linear_mapping = new();
    internal mem_mapping_t mmio_mapping = new();

    internal rom_t bios_rom = new();

    internal svga_t svga = new();
    // omitted: sdac_ramdac_t ramdac (vid_s3.c:53) — la RAMDAC SDAC de la Vision864 ;
    //   vid_sdac_ramdac.c n'est pas transcrit et la Trio64 a sa RAMDAC intégrée (s3_out/s3_in
    //   passent par svga_out/svga_in quand chip vaut S3_TRIO64).

    internal uint8_t bank;
    internal uint8_t ma_ext;
    internal int width;
    internal int bpp;

    internal int chip;

    internal uint8_t id, id_ext, id_ext_pci;

    internal uint8_t int_line;

    internal int packed_mmio;

    internal uint32_t linear_base, linear_size;

    internal uint8_t[] pci_regs = new uint8_t[256];
    internal int card;

    internal uint32_t vram_mask;

    internal s3_getclock_fn? getclock;
    internal object? getclock_p;

    internal s3_accel_t accel = new();

    internal fifo_entry_t[] fifo = new fifo_entry_t[vid_s3.FIFO_SIZE];
    // `volatile` en C : partagés avec le fil FIFO ; sans fil (G7 n° 3), de simples champs.
    internal int fifo_read_idx, fifo_write_idx;

    // DEVIATION (décision utilisateur du 01/10, G7 n° 3): accélérateur synchrone — les champs
    //   thread_t *fifo_thread, event_t *wake_fifo_thread, event_t *fifo_not_full_event
    //   (vid_s3.c:121-123) disparaissent : aucun fil, aucun événement.

    internal int blitter_busy;
    internal uint64_t blitter_time;
    internal uint64_t status_time;

    internal uint8_t subsys_cntl, subsys_stat;

    internal uint32_t hwc_fg_col, hwc_bg_col;
    internal int hwc_col_stack_pos;

    // `volatile` en C (écrit par le fil FIFO, lu par le CPU) ; sans fil, un champ simple.
    internal int force_busy;

    // DEVIATION (décision utilisateur du 01/10, G7 n° 3): accélérateur synchrone — garde de
    //   ré-entrance de s3_fifo_drain ; pas de champ C correspondant.
    internal bool fifo_draining;
}

/*S3 emulation*/
internal static partial class vid_s3
{
    // pcem: vid_s3.c:16
    private const int S3_VISION864 = 0, S3_TRIO32 = 1, S3_TRIO64 = 2;

    // pcem: vid_s3.c:18
    private const int VRAM_4MB = 0, VRAM_8MB = 3, VRAM_2MB = 4, VRAM_1MB = 6, VRAM_512KB = 7;

    // pcem: vid_s3.c:20-22
    internal const int FIFO_SIZE = 65536;
    private const int FIFO_MASK = (FIFO_SIZE - 1);
    private const int FIFO_ENTRY_SIZE = (1 << 31);

    // pcem: vid_s3.c:24-26 (macros FIFO_ENTRIES, FIFO_FULL, FIFO_EMPTY)
    private static int FIFO_ENTRIES(s3_t s3) { return (s3.fifo_write_idx - s3.fifo_read_idx); }
    private static bool FIFO_FULL(s3_t s3) { return ((s3.fifo_write_idx - s3.fifo_read_idx) >= FIFO_SIZE); }
    private static bool FIFO_EMPTY(s3_t s3) { return (s3.fifo_read_idx == s3.fifo_write_idx); }

    // pcem: vid_s3.c:28-29
    private const uint32_t FIFO_TYPE = 0xff000000;
    private const uint32_t FIFO_ADDR = 0x00ffffff;

    // pcem: vid_s3.c:31-39
    private const uint32_t FIFO_INVALID = (0x00 << 24);
    private const uint32_t FIFO_WRITE_BYTE = (0x01 << 24);
    private const uint32_t FIFO_WRITE_WORD = (0x02 << 24);
    private const uint32_t FIFO_WRITE_DWORD = (0x03 << 24);
    private const uint32_t FIFO_OUT_BYTE = (0x04 << 24);
    private const uint32_t FIFO_OUT_WORD = (0x05 << 24);
    private const uint32_t FIFO_OUT_DWORD = (0x06 << 24);

    // pcem: vid_s3.c:137-141
    private const int INT_VSY = (1 << 0);
    private const int INT_GE_BSY = (1 << 1);
    private const int INT_FIFO_OVR = (1 << 2);
    private const int INT_FIFO_EMP = (1 << 3);
    private const int INT_MASK = 0xf;

    // pcem: bus/pci.h:14, :16, :17, :22
    // DEVIATION: pci.h n'est pas transcrit ; les constantes lues ici le sont sur place.
    private const int PCI_REG_COMMAND = 0x04;
    private const int PCI_COMMAND_IO = 0x01;
    private const int PCI_COMMAND_MEM = 0x02;
    private const int PCI_INTA = 1;

    // omitted: les prototypes (vid_s3.c:143-148, :168) — C# n'en a pas besoin.

    // pcem: ibm.h:16 (macro ABS)
    private static int ABS(int x) { return ((x) > 0 ? (x) : -(x)); }

    // pcem: pci.c:185-190
    // DEVIATION: pci.c n'est pas transcrit. Seule la garde d'entrée de pci_add est reprise ;
    //   PCI (x86.cs) n'est jamais mis à 1 sur nos machines (pci_init n'est pas transcrit),
    //   donc pci_add rend toujours -1, comme le C sans bus PCI.
    private static int pci_add(pci_card_read_fn read, pci_card_write_fn write, object priv)
    {
        if (PCI == 0)
                return -1;

        // omitted: la recherche d'un emplacement libre (pci.c:192-205) — pci.c n'est pas
        //   transcrit et PCI vaut 0 ; on rend le -1 final de pci.c:205.
        return -1;
    }

    // pcem: vid_s3.c:150-152
    // DEVIATION (décision utilisateur du 01/10, G7 n° 3): accélérateur synchrone —
    //   thread_set_event(s3->wake_fifo_thread) devient l'exécution immédiate, sur le fil
    //   appelant, d'un tour de la boucle du fil FIFO (s3_fifo_drain).
    private static void wake_fifo_thread(s3_t s3)
    {
        s3_fifo_drain(s3); /*Wake up FIFO thread if moving from idle*/
    }

    // pcem: vid_s3.c:154-159
    // DEVIATION (décision utilisateur du 01/10, G7 n° 3): accélérateur synchrone — le couple
    //   wake_fifo_thread / thread_wait_event(s3->fifo_not_full_event, 1) devient un drainage
    //   direct ; la boucle ne tourne qu'une fois, la FIFO sort vide de s3_fifo_drain.
    private static void s3_wait_fifo_idle(s3_t s3)
    {
        while (!FIFO_EMPTY(s3))
                s3_fifo_drain(s3);
    }

    // pcem: vid_s3.c:161-166
    // pcem bug, not reproduced: PB-83 — DEVIATION : sans PCI, s3->card vaut -1 ;
    //   pci_set_irq/pci_clear_irq indexent pci_irq_routing[-1] et écrivent pci_irq_active[-1]
    //   (pci.c:128-148). Rien n'est câblé : aucune IRQ.
    private static void s3_update_irqs(s3_t s3)
    {
        if ((s3.subsys_cntl & s3.subsys_stat & INT_MASK) != 0)
        {
                // pci_set_irq(s3->card, PCI_INTA) — non câblé (PB-83).
        }
        else
        {
                // pci_clear_irq(s3->card, PCI_INTA) — non câblé (PB-83).
        }
    }

    // pcem: vid_s3.c:170-184 (macro WRITE8, sur une variable uint32_t)
    private static void WRITE8(uint32_t addr, ref uint32_t var, uint8_t val)
    {
        switch ((addr) & 3)
        {
        case 0:
                var = (var & 0xffffff00) | (val);
                break;
        case 1:
                var = (var & 0xffff00ff) | ((uint32_t)(val) << 8);
                break;
        case 2:
                var = (var & 0xff00ffff) | ((uint32_t)(val) << 16);
                break;
        case 3:
                var = (var & 0x00ffffff) | ((uint32_t)(val) << 24);
                break;
        }
    }
    // pcem: vid_s3.c:170-184 (macro WRITE8, sur une variable uint16_t : multifunc[])
    //   Le C calcule en 32 bits puis tronque à l'affectation ; le cast le dit.
    private static void WRITE8(uint32_t addr, ref uint16_t var, uint8_t val)
    {
        switch ((addr) & 3)
        {
        case 0:
                var = (uint16_t)((var & 0xffffff00) | (val));
                break;
        case 1:
                var = (uint16_t)((var & 0xffff00ff) | ((uint32_t)(val) << 8));
                break;
        case 2:
                var = (uint16_t)((var & 0xff00ffff) | ((uint32_t)(val) << 16));
                break;
        case 3:
                var = (uint16_t)(((uint32_t)var & 0x00ffffff) | ((uint32_t)(val) << 24));
                break;
        }
    }

    // pcem: vid_s3.c:186-556
    private static void s3_accel_out_fifo(s3_t s3, uint16_t port, uint8_t val)
    {
        switch (port)
        {
        case 0x82e8:
                s3.accel.cur_y = (uint16_t)((s3.accel.cur_y & 0xf00) | val);
                s3.accel.poly_cy = s3.accel.cur_y;
                break;
        case 0x82e9:
                s3.accel.cur_y = (uint16_t)((s3.accel.cur_y & 0xff) | ((val & 0x1f) << 8));
                s3.accel.poly_cy = s3.accel.cur_y;
                break;
        case 0x82ea:
                s3.accel.cur_y2 = (uint16_t)((s3.accel.cur_y2 & 0xf00) | val);
                s3.accel.poly_cy2 = s3.accel.cur_y2;
                break;
        case 0x82eb:
                s3.accel.cur_y2 = (uint16_t)((s3.accel.cur_y2 & 0xff) | ((val & 0x1f) << 8));
                s3.accel.poly_cy2 = s3.accel.cur_y2;
                break;

        case 0x86e8:
                s3.accel.cur_x = (uint16_t)((s3.accel.cur_x & 0xf00) | val);
                s3.accel.poly_cx = s3.accel.cur_x << 20;
                s3.accel.poly_x = s3.accel.poly_cx >> 20;
                break;
        case 0x86e9:
                s3.accel.cur_x = (uint16_t)((s3.accel.cur_x & 0xff) | ((val & 0x1f) << 8));
                s3.accel.poly_cx = s3.accel.poly_x = s3.accel.cur_x << 20;
                s3.accel.poly_x = s3.accel.poly_cx >> 20;
                break;
        case 0x86ea:
                s3.accel.cur_x2 = (uint16_t)((s3.accel.cur_x2 & 0xf00) | val);
                s3.accel.poly_cx2 = s3.accel.cur_x2 << 20;
                break;
        case 0x86eb:
                s3.accel.cur_x2 = (uint16_t)((s3.accel.cur_x2 & 0xff) | ((val & 0x1f) << 8));
                s3.accel.poly_cx2 = s3.accel.cur_x2 << 20;
                break;

        case 0x8ae8:
                s3.accel.desty_axstp = (int16_t)((s3.accel.desty_axstp & 0x3f00) | val);
                s3.accel.point_1_updated = 1;
                break;
        case 0x8ae9:
                s3.accel.desty_axstp = (int16_t)((s3.accel.desty_axstp & 0xff) | ((val & 0x3f) << 8));
                if ((val & 0x20) != 0)
                        s3.accel.desty_axstp = (int16_t)(s3.accel.desty_axstp | ~0x3fff);
                s3.accel.point_1_updated = 1;
                break;
        case 0x8aea:
                s3.accel.desty_axstp2 = (int16_t)((s3.accel.desty_axstp2 & 0x3f00) | val);
                s3.accel.point_2_updated = 1;
                break;
        case 0x8aeb:
                s3.accel.desty_axstp2 = (int16_t)((s3.accel.desty_axstp2 & 0xff) | ((val & 0x3f) << 8));
                if ((val & 0x20) != 0)
                        s3.accel.desty_axstp2 = (int16_t)(s3.accel.desty_axstp2 | ~0x3fff);
                s3.accel.point_2_updated = 1;
                break;

        case 0x8ee8:
                s3.accel.destx_distp = (int16_t)((s3.accel.destx_distp & 0x3f00) | val);
                s3.accel.point_1_updated = 1;
                break;
        case 0x8ee9:
                s3.accel.destx_distp = (int16_t)((s3.accel.destx_distp & 0xff) | ((val & 0x3f) << 8));
                if ((val & 0x20) != 0)
                        s3.accel.destx_distp = (int16_t)(s3.accel.destx_distp | ~0x3fff);
                s3.accel.point_1_updated = 1;
                break;
        case 0x8eea:
                s3.accel.x2 = (uint16_t)((s3.accel.x2 & 0xf00) | val);
                s3.accel.point_2_updated = 1;
                break;
        case 0x8eeb:
                s3.accel.x2 = (uint16_t)((s3.accel.x2 & 0xff) | ((val & 0xf) << 8));
                s3.accel.point_2_updated = 1;
                break;

        case 0x92e8:
                s3.accel.err_term = (int16_t)((s3.accel.err_term & 0x3f00) | val);
                break;
        case 0x92e9:
                s3.accel.err_term = (int16_t)((s3.accel.err_term & 0xff) | ((val & 0x3f) << 8));
                if ((val & 0x20) != 0)
                        s3.accel.err_term = (int16_t)(s3.accel.err_term | ~0x3fff);
                break;
        case 0x92ea:
                s3.accel.err_term2 = (int16_t)((s3.accel.err_term2 & 0x3f00) | val);
                break;
        case 0x92eb:
                s3.accel.err_term2 = (int16_t)((s3.accel.err_term2 & 0xff) | ((val & 0x3f) << 8));
                if ((val & 0x20) != 0)
                        s3.accel.err_term2 = (int16_t)(s3.accel.err_term2 | ~0x3fff);
                break;

        case 0x96e8:
                s3.accel.maj_axis_pcnt = (int16_t)((s3.accel.maj_axis_pcnt & 0x3f00) | val);
                break;
        case 0x96e9:
                s3.accel.maj_axis_pcnt = (int16_t)((s3.accel.maj_axis_pcnt & 0xff) | ((val & 0x0f) << 8));
                if ((val & 0x08) != 0)
                        s3.accel.maj_axis_pcnt = (int16_t)(s3.accel.maj_axis_pcnt | ~0x0fff);
                break;
        case 0x96ea:
                s3.accel.maj_axis_pcnt2 = (int16_t)((s3.accel.maj_axis_pcnt2 & 0xf00) | val);
                break;
        case 0x96eb:
                s3.accel.maj_axis_pcnt2 = (int16_t)((s3.accel.maj_axis_pcnt2 & 0xff) | ((val & 0x0f) << 8));
                if ((val & 0x08) != 0)
                        s3.accel.maj_axis_pcnt2 = (int16_t)(s3.accel.maj_axis_pcnt2 | ~0x0fff);
                break;

        case 0x9ae8:
                s3.accel.cmd = (uint16_t)((s3.accel.cmd & 0xff00) | val);
                break;
        case 0x9ae9:
                s3.accel.cmd = (uint16_t)((s3.accel.cmd & 0xff) | (val << 8));
                s3_accel_start(-1, 0, 0xffffffff, 0, s3);
                s3.accel.pix_trans_count = 0;
                s3.accel.multifunc[0xe] = (uint16_t)(s3.accel.multifunc[0xe] & ~0x10); /*hack*/
                break;

        case 0x9ee8:
                s3.accel.short_stroke = (uint16_t)((s3.accel.short_stroke & 0xff00) | val);
                break;
        case 0x9ee9:
                s3.accel.short_stroke = (uint16_t)((s3.accel.short_stroke & 0xff) | (val << 8));
                break;

        case 0xa2e8:
                if (s3.bpp == 3 && (s3.accel.multifunc[0xe] & 0x10) != 0 && (s3.accel.multifunc[0xe] & 0x200) == 0)
                        s3.accel.bkgd_color = (s3.accel.bkgd_color & ~0x00ff0000u) | ((uint32_t)val << 16);
                else
                        s3.accel.bkgd_color = (s3.accel.bkgd_color & ~0x000000ffu) | val;
                break;
        case 0xa2e9:
                if (s3.bpp == 3 && (s3.accel.multifunc[0xe] & 0x10) != 0 && (s3.accel.multifunc[0xe] & 0x200) == 0)
                        s3.accel.bkgd_color = (s3.accel.bkgd_color & ~0xff000000u) | ((uint32_t)val << 24);
                else
                        s3.accel.bkgd_color = (s3.accel.bkgd_color & ~0x0000ff00u) | ((uint32_t)val << 8);
                if ((s3.accel.multifunc[0xe] & 0x200) == 0)
                        s3.accel.multifunc[0xe] ^= 0x10;
                break;
        case 0xa2ea:
                if ((s3.accel.multifunc[0xe] & 0x200) != 0)
                        s3.accel.bkgd_color = (s3.accel.bkgd_color & ~0x00ff0000u) | ((uint32_t)val << 16);
                else if (s3.bpp == 3)
                {
                        if ((s3.accel.multifunc[0xe] & 0x10) != 0)
                                s3.accel.bkgd_color = (s3.accel.bkgd_color & ~0x00ff0000u) | ((uint32_t)val << 16);
                        else
                                s3.accel.bkgd_color = (s3.accel.bkgd_color & ~0x000000ffu) | val;
                }
                break;
        case 0xa2eb:
                if ((s3.accel.multifunc[0xe] & 0x200) != 0)
                        s3.accel.bkgd_color = (s3.accel.bkgd_color & ~0xff000000u) | ((uint32_t)val << 24);
                else if (s3.bpp == 3)
                {
                        if ((s3.accel.multifunc[0xe] & 0x10) != 0)
                                s3.accel.bkgd_color = (s3.accel.bkgd_color & ~0xff000000u) | ((uint32_t)val << 24);
                        else
                                s3.accel.bkgd_color = (s3.accel.bkgd_color & ~0x0000ff00u) | ((uint32_t)val << 8);
                        s3.accel.multifunc[0xe] ^= 0x10;
                }
                break;

        case 0xa6e8:
                if (s3.bpp == 3 && (s3.accel.multifunc[0xe] & 0x10) != 0 && (s3.accel.multifunc[0xe] & 0x200) == 0)
                        s3.accel.frgd_color = (s3.accel.frgd_color & ~0x00ff0000u) | ((uint32_t)val << 16);
                else
                        s3.accel.frgd_color = (s3.accel.frgd_color & ~0x000000ffu) | val;
                break;
        case 0xa6e9:
                if (s3.bpp == 3 && (s3.accel.multifunc[0xe] & 0x10) != 0 && (s3.accel.multifunc[0xe] & 0x200) == 0)
                        s3.accel.frgd_color = (s3.accel.frgd_color & ~0xff000000u) | ((uint32_t)val << 24);
                else
                        s3.accel.frgd_color = (s3.accel.frgd_color & ~0x0000ff00u) | ((uint32_t)val << 8);
                if ((s3.accel.multifunc[0xe] & 0x200) == 0)
                        s3.accel.multifunc[0xe] ^= 0x10;
                break;
        case 0xa6ea:
                if ((s3.accel.multifunc[0xe] & 0x200) != 0)
                        s3.accel.frgd_color = (s3.accel.frgd_color & ~0x00ff0000u) | ((uint32_t)val << 16);
                else if (s3.bpp == 3)
                {
                        if ((s3.accel.multifunc[0xe] & 0x10) != 0)
                                s3.accel.frgd_color = (s3.accel.frgd_color & ~0x00ff0000u) | ((uint32_t)val << 16);
                        else
                                s3.accel.frgd_color = (s3.accel.frgd_color & ~0x000000ffu) | val;
                }
                break;
        case 0xa6eb:
                if ((s3.accel.multifunc[0xe] & 0x200) != 0)
                        s3.accel.frgd_color = (s3.accel.frgd_color & ~0xff000000u) | ((uint32_t)val << 24);
                else if (s3.bpp == 3)
                {
                        if ((s3.accel.multifunc[0xe] & 0x10) != 0)
                                s3.accel.frgd_color = (s3.accel.frgd_color & ~0xff000000u) | ((uint32_t)val << 24);
                        else
                                s3.accel.frgd_color = (s3.accel.frgd_color & ~0x0000ff00u) | ((uint32_t)val << 8);
                        s3.accel.multifunc[0xe] ^= 0x10;
                }
                break;

        case 0xaae8:
                if (s3.bpp == 3 && (s3.accel.multifunc[0xe] & 0x10) != 0 && (s3.accel.multifunc[0xe] & 0x200) == 0)
                        s3.accel.wrt_mask = (s3.accel.wrt_mask & ~0x00ff0000u) | ((uint32_t)val << 16);
                else
                        s3.accel.wrt_mask = (s3.accel.wrt_mask & ~0x000000ffu) | val;
                break;
        case 0xaae9:
                if (s3.bpp == 3 && (s3.accel.multifunc[0xe] & 0x10) != 0 && (s3.accel.multifunc[0xe] & 0x200) == 0)
                        s3.accel.wrt_mask = (s3.accel.wrt_mask & ~0xff000000u) | ((uint32_t)val << 24);
                else
                        s3.accel.wrt_mask = (s3.accel.wrt_mask & ~0x0000ff00u) | ((uint32_t)val << 8);
                if ((s3.accel.multifunc[0xe] & 0x200) == 0)
                        s3.accel.multifunc[0xe] ^= 0x10;
                break;
        case 0xaaea:
                if ((s3.accel.multifunc[0xe] & 0x200) != 0)
                        s3.accel.wrt_mask = (s3.accel.wrt_mask & ~0x00ff0000u) | ((uint32_t)val << 16);
                else if (s3.bpp == 3)
                {
                        if ((s3.accel.multifunc[0xe] & 0x10) != 0)
                                s3.accel.wrt_mask = (s3.accel.wrt_mask & ~0x00ff0000u) | ((uint32_t)val << 16);
                        else
                                s3.accel.wrt_mask = (s3.accel.wrt_mask & ~0x000000ffu) | val;
                }
                break;
        case 0xaaeb:
                if ((s3.accel.multifunc[0xe] & 0x200) != 0)
                        s3.accel.wrt_mask = (s3.accel.wrt_mask & ~0xff000000u) | ((uint32_t)val << 24);
                else if (s3.bpp == 3)
                {
                        if ((s3.accel.multifunc[0xe] & 0x10) != 0)
                                s3.accel.wrt_mask = (s3.accel.wrt_mask & ~0xff000000u) | ((uint32_t)val << 24);
                        else
                                s3.accel.wrt_mask = (s3.accel.wrt_mask & ~0x0000ff00u) | ((uint32_t)val << 8);
                        s3.accel.multifunc[0xe] ^= 0x10;
                }
                break;

        case 0xaee8:
                if (s3.bpp == 3 && (s3.accel.multifunc[0xe] & 0x10) != 0 && (s3.accel.multifunc[0xe] & 0x200) == 0)
                        s3.accel.rd_mask = (s3.accel.rd_mask & ~0x00ff0000u) | ((uint32_t)val << 16);
                else
                        s3.accel.rd_mask = (s3.accel.rd_mask & ~0x000000ffu) | val;
                break;
        case 0xaee9:
                if (s3.bpp == 3 && (s3.accel.multifunc[0xe] & 0x10) != 0 && (s3.accel.multifunc[0xe] & 0x200) == 0)
                        s3.accel.rd_mask = (s3.accel.rd_mask & ~0xff000000u) | ((uint32_t)val << 24);
                else
                        s3.accel.rd_mask = (s3.accel.rd_mask & ~0x0000ff00u) | ((uint32_t)val << 8);
                if ((s3.accel.multifunc[0xe] & 0x200) == 0)
                        s3.accel.multifunc[0xe] ^= 0x10;
                break;
        case 0xaeea:
                if ((s3.accel.multifunc[0xe] & 0x200) != 0)
                        s3.accel.rd_mask = (s3.accel.rd_mask & ~0x00ff0000u) | ((uint32_t)val << 16);
                else if (s3.bpp == 3)
                {
                        if ((s3.accel.multifunc[0xe] & 0x10) != 0)
                                s3.accel.rd_mask = (s3.accel.rd_mask & ~0x00ff0000u) | ((uint32_t)val << 16);
                        else
                                s3.accel.rd_mask = (s3.accel.rd_mask & ~0x000000ffu) | val;
                }
                break;
        case 0xaeeb:
                if ((s3.accel.multifunc[0xe] & 0x200) != 0)
                        s3.accel.rd_mask = (s3.accel.rd_mask & ~0xff000000u) | ((uint32_t)val << 24);
                else if (s3.bpp == 3)
                {
                        if ((s3.accel.multifunc[0xe] & 0x10) != 0)
                                s3.accel.rd_mask = (s3.accel.rd_mask & ~0xff000000u) | ((uint32_t)val << 24);
                        else
                                s3.accel.rd_mask = (s3.accel.rd_mask & ~0x0000ff00u) | ((uint32_t)val << 8);
                        s3.accel.multifunc[0xe] ^= 0x10;
                }
                break;

        case 0xb2e8:
                if (s3.bpp == 3 && (s3.accel.multifunc[0xe] & 0x10) != 0 && (s3.accel.multifunc[0xe] & 0x200) == 0)
                        s3.accel.color_cmp = (s3.accel.color_cmp & ~0x00ff0000u) | ((uint32_t)val << 16);
                else
                        s3.accel.color_cmp = (s3.accel.color_cmp & ~0x000000ffu) | val;
                break;
        case 0xb2e9:
                if (s3.bpp == 3 && (s3.accel.multifunc[0xe] & 0x10) != 0 && (s3.accel.multifunc[0xe] & 0x200) == 0)
                        s3.accel.color_cmp = (s3.accel.color_cmp & ~0xff000000u) | ((uint32_t)val << 24);
                else
                        s3.accel.color_cmp = (s3.accel.color_cmp & ~0x0000ff00u) | ((uint32_t)val << 8);
                if ((s3.accel.multifunc[0xe] & 0x200) == 0)
                        s3.accel.multifunc[0xe] ^= 0x10;
                break;
        case 0xb2ea:
                if ((s3.accel.multifunc[0xe] & 0x200) != 0)
                        s3.accel.color_cmp = (s3.accel.color_cmp & ~0x00ff0000u) | ((uint32_t)val << 16);
                else if (s3.bpp == 3)
                {
                        if ((s3.accel.multifunc[0xe] & 0x10) != 0)
                                s3.accel.color_cmp = (s3.accel.color_cmp & ~0x00ff0000u) | ((uint32_t)val << 16);
                        else
                                s3.accel.color_cmp = (s3.accel.color_cmp & ~0x000000ffu) | val;
                }
                break;
        case 0xb2eb:
                if ((s3.accel.multifunc[0xe] & 0x200) != 0)
                        s3.accel.color_cmp = (s3.accel.color_cmp & ~0xff000000u) | ((uint32_t)val << 24);
                else if (s3.bpp == 3)
                {
                        if ((s3.accel.multifunc[0xe] & 0x10) != 0)
                                s3.accel.color_cmp = (s3.accel.color_cmp & ~0xff000000u) | ((uint32_t)val << 24);
                        else
                                s3.accel.color_cmp = (s3.accel.color_cmp & ~0x0000ff00u) | ((uint32_t)val << 8);
                        s3.accel.multifunc[0xe] ^= 0x10;
                }
                break;

        case 0xb6e8:
                s3.accel.bkgd_mix = val;
                break;

        case 0xbae8:
                s3.accel.frgd_mix = val;
                break;

        case 0xbee8:
                s3.accel.multifunc_cntl = (uint16_t)((s3.accel.multifunc_cntl & 0xff00) | val);
                break;
        case 0xbee9:
                s3.accel.multifunc_cntl = (uint16_t)((s3.accel.multifunc_cntl & 0xff) | (val << 8));
                s3.accel.multifunc[s3.accel.multifunc_cntl >> 12] = (uint16_t)(s3.accel.multifunc_cntl & 0xfff);
                break;

        case 0xe2e8:
                s3.accel.pix_trans[0] = val;
                if ((s3.accel.multifunc[0xa] & 0xc0) == 0x80 && (s3.accel.cmd & 0x600) == 0 && (s3.accel.cmd & 0x100) != 0)
                        s3_accel_start(8, 1, s3.accel.pix_trans[0], 0, s3);
                else if ((s3.accel.cmd & 0x600) == 0 && (s3.accel.cmd & 0x100) != 0)
                        s3_accel_start(1, 1, 0xffffffff, s3.accel.pix_trans[0], s3);
                break;
        case 0xe2e9:
                s3.accel.pix_trans[1] = val;
                if ((s3.accel.multifunc[0xa] & 0xc0) == 0x80 && (s3.accel.cmd & 0x600) == 0x200 && (s3.accel.cmd & 0x100) != 0)
                {
                        if ((s3.accel.cmd & 0x1000) != 0)
                                s3_accel_start(16, 1, (uint32_t)(s3.accel.pix_trans[1] | (s3.accel.pix_trans[0] << 8)), 0, s3);
                        else
                                s3_accel_start(16, 1, (uint32_t)(s3.accel.pix_trans[0] | (s3.accel.pix_trans[1] << 8)), 0, s3);
                }
                else if ((s3.accel.cmd & 0x600) == 0x200 && (s3.accel.cmd & 0x100) != 0)
                {
                        if ((s3.accel.cmd & 0x1000) != 0)
                                s3_accel_start(2, 1, 0xffffffff, (uint32_t)(s3.accel.pix_trans[1] | (s3.accel.pix_trans[0] << 8)), s3);
                        else
                                s3_accel_start(2, 1, 0xffffffff, (uint32_t)(s3.accel.pix_trans[0] | (s3.accel.pix_trans[1] << 8)), s3);
                }
                break;
        case 0xe2ea:
                s3.accel.pix_trans[2] = val;
                break;
        case 0xe2eb:
                s3.accel.pix_trans[3] = val;
                if ((s3.accel.multifunc[0xa] & 0xc0) == 0x80 && (s3.accel.cmd & 0x600) == 0x600 && (s3.accel.cmd & 0x100) != 0 &&
                    s3.chip == S3_TRIO32)
                {
                        s3_accel_start(8, 1, s3.accel.pix_trans[3], 0, s3);
                        s3_accel_start(8, 1, s3.accel.pix_trans[2], 0, s3);
                        s3_accel_start(8, 1, s3.accel.pix_trans[1], 0, s3);
                        s3_accel_start(8, 1, s3.accel.pix_trans[0], 0, s3);
                }
                else if ((s3.accel.multifunc[0xa] & 0xc0) == 0x80 && (s3.accel.cmd & 0x400) == 0x400 &&
                         (s3.accel.cmd & 0x100) != 0)
                        s3_accel_start(32, 1,
                                       (uint32_t)(s3.accel.pix_trans[0] | (s3.accel.pix_trans[1] << 8) | (s3.accel.pix_trans[2] << 16) |
                                               (s3.accel.pix_trans[3] << 24)),
                                       0, s3);
                else if ((s3.accel.cmd & 0x600) == 0x400 && (s3.accel.cmd & 0x100) != 0)
                        s3_accel_start(4, 1, 0xffffffff,
                                       (uint32_t)(s3.accel.pix_trans[0] | (s3.accel.pix_trans[1] << 8) | (s3.accel.pix_trans[2] << 16) |
                                               (s3.accel.pix_trans[3] << 24)),
                                       s3);
                break;
        }
    }

    // pcem: vid_s3.c:558-579
    private static void s3_accel_out_fifo_w(s3_t s3, uint16_t port, uint16_t val)
    {
        //        pclog("Accel out w %04X %04X\n", port, val);
        if ((s3.accel.cmd & 0x100) != 0)
        {
                if ((s3.accel.multifunc[0xa] & 0xc0) == 0x80)
                {
                        if ((s3.accel.cmd & 0x1000) != 0)
                                val = (uint16_t)((val >> 8) | (val << 8));
                        if ((s3.accel.cmd & 0x600) == 0x600 && s3.chip == S3_TRIO32)
                        {
                                s3_accel_start(8, 1, (uint32_t)((val >> 8) & 0xff), 0, s3);
                                s3_accel_start(8, 1, (uint32_t)(val & 0xff), 0, s3);
                        }
                        if ((s3.accel.cmd & 0x600) == 0x000)
                                s3_accel_start(8, 1, (uint32_t)(val | (val << 16)), 0, s3);
                        else
                                s3_accel_start(16, 1, (uint32_t)(val | (val << 16)), 0, s3);
                }
                else
                {
                        if ((s3.accel.cmd & 0x600) == 0x000)
                                s3_accel_start(1, 1, 0xffffffff, (uint32_t)(val | (val << 16)), s3);
                        else
                                s3_accel_start(2, 1, 0xffffffff, (uint32_t)(val | (val << 16)), s3);
                }
        }
    }

    // pcem: vid_s3.c:581-621
    private static void s3_accel_out_fifo_l(s3_t s3, uint16_t port, uint32_t val)
    {
        //        pclog("Accel out l %04X %08X\n", port, val);
        if ((s3.accel.cmd & 0x100) != 0)
        {
                if ((s3.accel.multifunc[0xa] & 0xc0) == 0x80)
                {
                        if ((s3.accel.cmd & 0x600) == 0x600 && s3.chip == S3_TRIO32)
                        {
                                if ((s3.accel.cmd & 0x1000) != 0)
                                        val = ((val & 0xff000000) >> 24) | ((val & 0x00ff0000) >> 8) | ((val & 0x0000ff00) << 8) |
                                              ((val & 0x000000ff) << 24);
                                s3_accel_start(8, 1, (val >> 24) & 0xff, 0, s3);
                                s3_accel_start(8, 1, (val >> 16) & 0xff, 0, s3);
                                s3_accel_start(8, 1, (val >> 8) & 0xff, 0, s3);
                                s3_accel_start(8, 1, val & 0xff, 0, s3);
                        }
                        else if ((s3.accel.cmd & 0x400) != 0)
                        {
                                if ((s3.accel.cmd & 0x1000) != 0)
                                        val = ((val & 0xff000000) >> 24) | ((val & 0x00ff0000) >> 8) | ((val & 0x0000ff00) << 8) |
                                              ((val & 0x000000ff) << 24);
                                s3_accel_start(32, 1, val, 0, s3);
                        }
                        else if ((s3.accel.cmd & 0x600) == 0x200)
                        {
                                if ((s3.accel.cmd & 0x1000) != 0)
                                        val = ((val & 0xff00ff00) >> 8) | ((val & 0x00ff00ff) << 8);
                                s3_accel_start(16, 1, val, 0, s3);
                                s3_accel_start(16, 1, val >> 16, 0, s3);
                        }
                        else
                        {
                                if ((s3.accel.cmd & 0x1000) != 0)
                                        val = ((val & 0xff00ff00) >> 8) | ((val & 0x00ff00ff) << 8);
                                s3_accel_start(8, 1, val, 0, s3);
                                s3_accel_start(8, 1, val >> 16, 0, s3);
                        }
                }
                else
                {
                        if ((s3.accel.cmd & 0x400) != 0)
                                s3_accel_start(4, 1, 0xffffffff, val, s3);
                        else if ((s3.accel.cmd & 0x600) == 0x200)
                        {
                                s3_accel_start(2, 1, 0xffffffff, val, s3);
                                s3_accel_start(2, 1, 0xffffffff, val >> 16, s3);
                        }
                        else
                        {
                                s3_accel_start(1, 1, 0xffffffff, val, s3);
                                s3_accel_start(1, 1, 0xffffffff, val >> 16, s3);
                        }
                }
        }
    }

    // pcem: vid_s3.c:623-762
    private static void s3_accel_write_fifo(s3_t s3, uint32_t addr, uint8_t val)
    {
        //        pclog("Write S3 accel %08X %02X\n", addr, val);
        if (s3.packed_mmio != 0)
        {
                int addr_lo = (int)(addr & 1);
                switch (addr & 0xfffe)
                {
                case 0x8100:
                        addr = 0x82e8;
                        break; /*ALT_CURXY*/
                case 0x8102:
                        addr = 0x86e8;
                        break;

                case 0x8104:
                        addr = 0x82ea;
                        break; /*ALT_CURXY2*/
                case 0x8106:
                        addr = 0x86ea;
                        break;

                case 0x8108:
                        addr = 0x8ae8;
                        break; /*ALT_STEP*/
                case 0x810a:
                        addr = 0x8ee8;
                        break;

                case 0x810c:
                        addr = 0x8aea;
                        break; /*ALT_STEP2*/
                case 0x810e:
                        addr = 0x8eea;
                        break;

                case 0x8110:
                        addr = 0x92e8;
                        break; /*ALT_ERR*/
                case 0x8112:
                        addr = 0x92ee;
                        break;

                case 0x8118:
                        addr = 0x9ae8;
                        break; /*ALT_CMD*/
                case 0x811a:
                        addr = 0x9aea;
                        break;

                case 0x811c:
                        addr = 0x9ee8;
                        break; /*SHORT_STROKE*/

                case 0x8120:
                case 0x8122: /*BKGD_COLOR*/
                        WRITE8(addr, ref s3.accel.bkgd_color, val);
                        return;

                case 0x8124:
                case 0x8126: /*FRGD_COLOR*/
                        WRITE8(addr, ref s3.accel.frgd_color, val);
                        return;

                case 0x8128:
                case 0x812a: /*WRT_MASK*/
                        WRITE8(addr, ref s3.accel.wrt_mask, val);
                        return;

                case 0x812c:
                case 0x812e: /*RD_MASK*/
                        WRITE8(addr, ref s3.accel.rd_mask, val);
                        return;

                case 0x8130:
                case 0x8132: /*COLOR_CMP*/
                        WRITE8(addr, ref s3.accel.color_cmp, val);
                        return;

                case 0x8134:
                        addr = 0xb6e8;
                        break; /*ALT_MIX*/
                case 0x8136:
                        addr = 0xbae8;
                        break;

                case 0x8138: /*SCISSORS_T*/
                        WRITE8(addr & 1, ref s3.accel.multifunc[1], val);
                        return;
                case 0x813a: /*SCISSORS_L*/
                        WRITE8(addr & 1, ref s3.accel.multifunc[2], val);
                        return;
                case 0x813c: /*SCISSORS_B*/
                        WRITE8(addr & 1, ref s3.accel.multifunc[3], val);
                        return;
                case 0x813e: /*SCISSORS_R*/
                        WRITE8(addr & 1, ref s3.accel.multifunc[4], val);
                        return;

                case 0x8140: /*PIX_CNTL*/
                        WRITE8(addr & 1, ref s3.accel.multifunc[0xa], val);
                        return;
                case 0x8142: /*MULT_MISC2*/
                        WRITE8(addr & 1, ref s3.accel.multifunc[0xd], val);
                        return;
                case 0x8144: /*MULT_MISC*/
                        WRITE8(addr & 1, ref s3.accel.multifunc[0xe], val);
                        return;
                case 0x8146: /*READ_SEL*/
                        WRITE8(addr & 1, ref s3.accel.multifunc[0xf], val);
                        return;

                case 0x8148: /*ALT_PCNT*/
                        WRITE8(addr & 1, ref s3.accel.multifunc[0], val);
                        return;
                case 0x814a:
                        addr = 0x96e8;
                        break;
                case 0x814c:
                        addr = 0x96ea;
                        break;

                case 0x8168:
                        addr = 0xeae8;
                        break;
                case 0x816a:
                        addr = 0xeaea;
                        break;
                }
                addr |= (uint32_t)addr_lo;
        }

        if ((addr & 0x8000) != 0)
        {
                s3_accel_out_fifo(s3, (uint16_t)(addr & 0xffff), val);
        }
        else
        {
                if ((s3.accel.cmd & 0x100) != 0)
                {
                        if ((s3.accel.multifunc[0xa] & 0xc0) == 0x80)
                                s3_accel_start(8, 1, (uint32_t)(val | (val << 8) | (val << 16) | (val << 24)), 0, s3);
                        else
                                s3_accel_start(1, 1, 0xffffffff, (uint32_t)(val | (val << 8) | (val << 16) | (val << 24)), s3);
                }
        }
    }

    // pcem: vid_s3.c:764-789
    private static void s3_accel_write_fifo_w(s3_t s3, uint32_t addr, uint16_t val)
    {
        //        pclog("Write S3 accel w %08X %04X\n", addr, val);
        if ((addr & 0x8000) != 0)
        {
                s3_accel_write_fifo(s3, addr, (uint8_t)val);
                s3_accel_write_fifo(s3, addr + 1, (uint8_t)(val >> 8));
        }
        else
        {
                if ((s3.accel.cmd & 0x100) != 0)
                {
                        if ((s3.accel.multifunc[0xa] & 0xc0) == 0x80)
                        {
                                if ((s3.accel.cmd & 0x1000) != 0)
                                        val = (uint16_t)((val >> 8) | (val << 8));
                                if ((s3.accel.cmd & 0x600) == 0x600 && s3.chip == S3_TRIO32)
                                {
                                        s3_accel_start(8, 1, (uint32_t)((val >> 8) & 0xff), 0, s3);
                                        s3_accel_start(8, 1, (uint32_t)(val & 0xff), 0, s3);
                                }
                                else if ((s3.accel.cmd & 0x600) == 0x000)
                                        s3_accel_start(8, 1, (uint32_t)(val | (val << 16)), 0, s3);
                                else
                                        s3_accel_start(16, 1, (uint32_t)(val | (val << 16)), 0, s3);
                        }
                        else
                        {
                                if ((s3.accel.cmd & 0x600) == 0x000)
                                        s3_accel_start(1, 1, 0xffffffff, (uint32_t)(val | (val << 16)), s3);
                                else
                                        s3_accel_start(2, 1, 0xffffffff, (uint32_t)(val | (val << 16)), s3);
                        }
                }
        }
    }

    // pcem: vid_s3.c:791-838
    private static void s3_accel_write_fifo_l(s3_t s3, uint32_t addr, uint32_t val)
    {
        //        pclog("Write S3 accel l %08X %08X\n", addr, val);
        if ((addr & 0x8000) != 0)
        {
                s3_accel_write_fifo(s3, addr, (uint8_t)val);
                s3_accel_write_fifo(s3, addr + 1, (uint8_t)(val >> 8));
                s3_accel_write_fifo(s3, addr + 2, (uint8_t)(val >> 16));
                s3_accel_write_fifo(s3, addr + 3, (uint8_t)(val >> 24));
        }
        else
        {
                if ((s3.accel.cmd & 0x100) != 0)
                {
                        if ((s3.accel.multifunc[0xa] & 0xc0) == 0x80)
                        {
                                if ((s3.accel.cmd & 0x600) == 0x600 && s3.chip == S3_TRIO32)
                                {
                                        if ((s3.accel.cmd & 0x1000) != 0)
                                                val = ((val & 0xff000000) >> 24) | ((val & 0x00ff0000) >> 8) |
                                                      ((val & 0x0000ff00) << 8) | ((val & 0x000000ff) << 24);
                                        s3_accel_start(8, 1, (val >> 24) & 0xff, 0, s3);
                                        s3_accel_start(8, 1, (val >> 16) & 0xff, 0, s3);
                                        s3_accel_start(8, 1, (val >> 8) & 0xff, 0, s3);
                                        s3_accel_start(8, 1, val & 0xff, 0, s3);
                                }
                                else if ((s3.accel.cmd & 0x400) != 0)
                                {
                                        if ((s3.accel.cmd & 0x1000) != 0)
                                                val = ((val & 0xff000000) >> 24) | ((val & 0x00ff0000) >> 8) |
                                                      ((val & 0x0000ff00) << 8) | ((val & 0x000000ff) << 24);
                                        s3_accel_start(32, 1, val, 0, s3);
                                }
                                else if ((s3.accel.cmd & 0x600) == 0x200)
                                {
                                        if ((s3.accel.cmd & 0x1000) != 0)
                                                val = ((val & 0xff00ff00) >> 8) | ((val & 0x00ff00ff) << 8);
                                        s3_accel_start(16, 1, val, 0, s3);
                                        s3_accel_start(16, 1, val >> 16, 0, s3);
                                }
                                else
                                {
                                        if ((s3.accel.cmd & 0x1000) != 0)
                                                val = ((val & 0xff00ff00) >> 8) | ((val & 0x00ff00ff) << 8);
                                        s3_accel_start(8, 1, val, 0, s3);
                                        s3_accel_start(8, 1, val >> 16, 0, s3);
                                }
                        }
                        else
                        {
                                if ((s3.accel.cmd & 0x400) != 0)
                                        s3_accel_start(4, 1, 0xffffffff, val, s3);
                                else if ((s3.accel.cmd & 0x600) == 0x200)
                                {
                                        s3_accel_start(2, 1, 0xffffffff, val, s3);
                                        s3_accel_start(2, 1, 0xffffffff, val >> 16, s3);
                                }
                                else
                                {
                                        s3_accel_start(1, 1, 0xffffffff, val, s3);
                                        s3_accel_start(1, 1, 0xffffffff, val >> 16, s3);
                                }
                        }
                }
        }
    }

    // pcem: vid_s3.c:840-887 (fifo_thread, corps de la boucle `while (1)`)
    // DEVIATION (décision utilisateur du 01/10, G7 n° 3): accélérateur synchrone. PCem fait
    //   tourner l'accélérateur dans un fil (fifo_thread), nourri par s3_queue au travers de
    //   l'anneau s3->fifo et réveillé par l'événement wake_fifo_thread. Ici, ni fil ni
    //   événement : l'anneau, fifo_write_idx/fifo_read_idx et s3_queue sont gardés tels quels,
    //   et chaque réveil (wake_fifo_thread) exécute UNE fois, sur le fil appelant, le corps de
    //   la boucle du fil : blitter_busy = 1, vidage de l'anneau, blitter_busy = 0,
    //   INT_FIFO_EMP, s3_update_irqs. Effets observables : la carte n'est jamais vue « occupée »
    //   par le CPU — les lectures d'état 9AE8/9AE9 trouvent toujours la FIFO vide (force_busy
    //   mis à part : s3_accel_start le lève et seule la lecture de 9AE9 le baisse) — et la
    //   FIFO ne se remplit jamais (FIFO_FULL toujours faux, au plus une entrée en vol).
    //   thread_set_event(fifo_not_full_event) et thread_wait_event/thread_reset_event
    //   (vid_s3.c:844-846, :877-878) disparaissent ; timer_read et l'accumulation de
    //   blitter_time (vid_s3.c:849-850, :880-881) aussi : blitter_time n'est lu que par
    //   s3_add_status_info (fenêtre d'état de l'hôte, ni invité ni oracle) et reste à 0.
    //   Une garde de ré-entrance (fifo_draining) rend la main si un drainage est déjà en cours.
    private static void s3_fifo_drain(s3_t s3)
    {
        if (s3.fifo_draining)
                return;
        s3.fifo_draining = true;

        s3.blitter_busy = 1;
        while (!FIFO_EMPTY(s3))
        {
                int fifo = s3.fifo_read_idx & FIFO_MASK;

                switch (s3.fifo[fifo].addr_type & FIFO_TYPE)
                {
                case FIFO_WRITE_BYTE:
                        s3_accel_write_fifo(s3, s3.fifo[fifo].addr_type & FIFO_ADDR, (uint8_t)s3.fifo[fifo].val);
                        break;
                case FIFO_WRITE_WORD:
                        s3_accel_write_fifo_w(s3, s3.fifo[fifo].addr_type & FIFO_ADDR, (uint16_t)s3.fifo[fifo].val);
                        break;
                case FIFO_WRITE_DWORD:
                        s3_accel_write_fifo_l(s3, s3.fifo[fifo].addr_type & FIFO_ADDR, s3.fifo[fifo].val);
                        break;
                case FIFO_OUT_BYTE:
                        s3_accel_out_fifo(s3, (uint16_t)(s3.fifo[fifo].addr_type & FIFO_ADDR), (uint8_t)s3.fifo[fifo].val);
                        break;
                case FIFO_OUT_WORD:
                        s3_accel_out_fifo_w(s3, (uint16_t)(s3.fifo[fifo].addr_type & FIFO_ADDR), (uint16_t)s3.fifo[fifo].val);
                        break;
                case FIFO_OUT_DWORD:
                        s3_accel_out_fifo_l(s3, (uint16_t)(s3.fifo[fifo].addr_type & FIFO_ADDR), s3.fifo[fifo].val);
                        break;
                }

                s3.fifo_read_idx++;
                s3.fifo[fifo].addr_type = FIFO_INVALID;
        }
        s3.blitter_busy = 0;
        s3.subsys_stat |= INT_FIFO_EMP;
        s3_update_irqs(s3);

        s3.fifo_draining = false;
    }

    // pcem: vid_s3.c:889-894
    private static void s3_vblank_start(svga_t svga)
    {
        s3_t s3 = (s3_t)svga.p;

        s3.subsys_stat |= INT_VSY;
        s3_update_irqs(s3);
    }

    // pcem: vid_s3.c:896-913
    private static void s3_queue(s3_t s3, uint32_t addr, uint32_t val, uint32_t type)
    {
        int fifo = s3.fifo_write_idx & FIFO_MASK;

        if (FIFO_FULL(s3))
        {
                // DEVIATION (décision utilisateur du 01/10, G7 n° 3): accélérateur synchrone —
                //   thread_reset_event(s3->fifo_not_full_event) disparaît.
                if (FIFO_FULL(s3))
                {
                        // DEVIATION (décision utilisateur du 01/10, G7 n° 3): accélérateur synchrone —
                        //   thread_wait_event(s3->fifo_not_full_event, -1) devient un drainage direct.
                        //   Inatteignable : la FIFO ne porte jamais plus d'une entrée.
                        s3_wait_fifo_idle(s3); /*Wait for room in ringbuffer*/
                }
        }

        s3.fifo[fifo].val = val;
        s3.fifo[fifo].addr_type = (addr & FIFO_ADDR) | type;

        s3.fifo_write_idx++;

        if (FIFO_ENTRIES(s3) > 0xe000 || FIFO_ENTRIES(s3) < 8)
                wake_fifo_thread(s3);
    }

    // pcem: vid_s3.c:915-1127
    internal static void s3_out(uint16_t addr, uint8_t val, object p)
    {
        s3_t s3 = (s3_t)p;
        svga_t svga = s3.svga;
        uint8_t old;

        if (((addr & 0xfff0) == 0x3d0 || (addr & 0xfff0) == 0x3b0) && (svga.miscout & 1) == 0)
                addr ^= 0x60;

        //        pclog("S3 out %04X %02X %04x:%08x\n", addr, val, CS, pc);

        switch (addr)
        {
        case 0x3c5:
                if (svga.seqaddr >= 0x10 && svga.seqaddr < 0x20)
                {
                        svga.seqregs[svga.seqaddr] = val;
                        switch (svga.seqaddr)
                        {
                        case 0x12:
                        case 0x13:
                                svga_recalctimings(svga);
                                return;
                        }
                }
                if (svga.seqaddr == 4) /*Chain-4 - update banking*/
                {
                        if ((val & 8) != 0)
                                svga.write_bank = svga.read_bank = (uint32_t)(s3.bank << 16);
                        else
                                svga.write_bank = svga.read_bank = (uint32_t)(s3.bank << 14);
                }
                break;

        case 0x3C6:
        case 0x3C7:
        case 0x3C8:
        case 0x3C9:
                //                pclog("Write RAMDAC %04X %02X %04X:%04X\n", addr, val, CS, pc);
                if (s3.chip == S3_TRIO32 || s3.chip == S3_TRIO64)
                        svga_out(addr, val, svga);
                else
                {
                        // omitted: sdac_ramdac_out((addr & 3) | 4 ou addr & 3, val, &s3->ramdac, svga)
                        //   (vid_s3.c:953-956) — la RAMDAC SDAC de la Vision864 ; vid_sdac_ramdac.c
                        //   n'est pas transcrit et chip vaut toujours S3_TRIO64 ici.
                }
                return;

        case 0x3D4:
                svga.crtcreg = (uint8_t)(val & 0x7f);
                return;
        case 0x3D5:
                if ((svga.crtcreg < 7) && (svga.crtc[0x11] & 0x80) != 0)
                        return;
                if ((svga.crtcreg == 7) && (svga.crtc[0x11] & 0x80) != 0)
                        val = (uint8_t)((svga.crtc[7] & ~0x10) | (val & 0x10));
                if (svga.crtcreg >= 0x20 && svga.crtcreg != 0x38 && svga.crtcreg != 0x39 &&
                    (svga.crtc[0x38] & 0xcc) != 0x48)
                {
                        if (!((svga.crtc[0x39] & 0xe0) == 0xa0 && svga.crtcreg >= 0x40 && svga.crtcreg <= 0x6d))
                                return;
                }
                old = svga.crtc[svga.crtcreg];
                svga.crtc[svga.crtcreg] = val;
                switch (svga.crtcreg)
                {
                case 0x31:
                        s3.ma_ext = (uint8_t)((s3.ma_ext & 0x1c) | ((val & 0x30) >> 4));
                        svga.force_dword_mode = val & 0x08;
                        break;
                case 0x32:
                        svga.vram_display_mask = ((val & 0x40) != 0) ? 0x3ffff : s3.vram_mask;
                        break;

                case 0x50:
                        switch (svga.crtc[0x50] & 0xc1)
                        {
                        case 0x00:
                                s3.width = ((svga.crtc[0x31] & 2) != 0) ? 2048 : 1024;
                                break;
                        case 0x01:
                                s3.width = 1152;
                                break;
                        case 0x40:
                                s3.width = 640;
                                break;
                        case 0x80:
                                s3.width = 800;
                                break;
                        case 0x81:
                                s3.width = 1600;
                                break;
                        case 0xc0:
                                s3.width = 1280;
                                break;
                        }
                        s3.bpp = (svga.crtc[0x50] >> 4) & 3;
                        break;
                case 0x69:
                        s3.ma_ext = (uint8_t)(val & 0x1f);
                        break;

                case 0x35:
                        s3.bank = (uint8_t)((s3.bank & 0x70) | (val & 0xf));
                        //                        pclog("CRTC write R35 %02X\n", val);
                        if (svga.chain4 != 0)
                                svga.write_bank = svga.read_bank = (uint32_t)(s3.bank << 16);
                        else
                                svga.write_bank = svga.read_bank = (uint32_t)(s3.bank << 14);
                        break;
                case 0x51:
                        s3.bank = (uint8_t)((s3.bank & 0x4f) | ((val & 0xc) << 2));
                        //                        pclog("CRTC write R51 %02X\n", val);
                        if (svga.chain4 != 0)
                                svga.write_bank = svga.read_bank = (uint32_t)(s3.bank << 16);
                        else
                                svga.write_bank = svga.read_bank = (uint32_t)(s3.bank << 14);
                        s3.ma_ext = (uint8_t)((s3.ma_ext & ~0xc) | ((val & 3) << 2));
                        break;
                case 0x6a:
                        s3.bank = val;
                        //                        pclog("CRTC write R6a %02X\n", val);
                        if (svga.chain4 != 0)
                                svga.write_bank = svga.read_bank = (uint32_t)(s3.bank << 16);
                        else
                                svga.write_bank = svga.read_bank = (uint32_t)(s3.bank << 14);
                        break;

                case 0x3a:
                        if ((val & 0x10) != 0)
                                svga.gdcreg[5] |= 0x40; /*Horrible cheat*/
                        break;

                case 0x45:
                        svga.hwcursor.ena = val & 1;
                        break;
                case 0x48:
                        svga.hwcursor.x = ((svga.crtc[0x46] << 8) | svga.crtc[0x47]) & 0x7ff;
                        if (svga.bpp == 32)
                                svga.hwcursor.x >>= 1;
                        svga.hwcursor.y = ((svga.crtc[0x48] << 8) | svga.crtc[0x49]) & 0x7ff;
                        svga.hwcursor.xoff = svga.crtc[0x4e] & 63;
                        svga.hwcursor.yoff = svga.crtc[0x4f] & 63;
                        svga.hwcursor.addr =
                                (uint32_t)(((((svga.crtc[0x4c] << 8) | svga.crtc[0x4d]) & 0xfff) * 1024) + (svga.hwcursor.yoff * 16));
                        if ((s3.chip == S3_TRIO32 || s3.chip == S3_TRIO64) && svga.bpp == 32)
                                svga.hwcursor.x <<= 1;
                        break;

                case 0x4a:
                        switch (s3.hwc_col_stack_pos)
                        {
                        case 0:
                                s3.hwc_fg_col = (s3.hwc_fg_col & 0xffff00) | val;
                                break;
                        case 1:
                                s3.hwc_fg_col = (s3.hwc_fg_col & 0xff00ff) | ((uint32_t)val << 8);
                                break;
                        case 2:
                                s3.hwc_fg_col = (s3.hwc_fg_col & 0x00ffff) | ((uint32_t)val << 16);
                                break;
                        }
                        s3.hwc_col_stack_pos = (s3.hwc_col_stack_pos + 1) % 3;
                        break;
                case 0x4b:
                        switch (s3.hwc_col_stack_pos)
                        {
                        case 0:
                                s3.hwc_bg_col = (s3.hwc_bg_col & 0xffff00) | val;
                                break;
                        case 1:
                                s3.hwc_bg_col = (s3.hwc_bg_col & 0xff00ff) | ((uint32_t)val << 8);
                                break;
                        case 2:
                                s3.hwc_bg_col = (s3.hwc_bg_col & 0x00ffff) | ((uint32_t)val << 16);
                                break;
                        }
                        s3.hwc_col_stack_pos = (s3.hwc_col_stack_pos + 1) % 3;
                        break;

                case 0x53:
                case 0x58:
                case 0x59:
                case 0x5a:
                        s3_updatemapping(s3);
                        break;

                case 0x67:
                        if (s3.chip == S3_TRIO32 || s3.chip == S3_TRIO64)
                        {
                                switch (val >> 4)
                                {
                                case 3:
                                        svga.bpp = 15;
                                        break;
                                case 5:
                                        svga.bpp = 16;
                                        break;
                                case 7:
                                        svga.bpp = 24;
                                        break;
                                case 13:
                                        svga.bpp = 32;
                                        break;
                                default:
                                        svga.bpp = 8;
                                        break;
                                }
                        }
                        break;
                        // case 0x55: case 0x43:
                        //                                pclog("Write CRTC R%02X %02X\n", crtcreg, val);
                }
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

    // pcem: vid_s3.c:1129-1189
    internal static uint8_t s3_in(uint16_t addr, object p)
    {
        s3_t s3 = (s3_t)p;
        svga_t svga = s3.svga;

        if (((addr & 0xfff0) == 0x3d0 || (addr & 0xfff0) == 0x3b0) && (svga.miscout & 1) == 0)
                addr ^= 0x60;

        //        if (addr != 0x3da) pclog("S3 in %04X %08x:%02x\n", addr, CS, pc);
        switch (addr)
        {
        case 0x3c1:
                if (svga.attraddr > 0x14)
                        return 0xff;
                break;

        case 0x3c5:
                if (svga.seqaddr >= 0x10 && svga.seqaddr < 0x20)
                        return svga.seqregs[svga.seqaddr];
                break;

        case 0x3c6:
        case 0x3c7:
        case 0x3c8:
        case 0x3c9:
                //                pclog("Read RAMDAC %04X  %04X:%04X\n", addr, CS, pc);
                if (s3.chip == S3_TRIO32 || s3.chip == S3_TRIO64)
                        return svga_in(addr, svga);
                // omitted: les deux `return sdac_ramdac_in(...)` (vid_s3.c:1155-1157) — la RAMDAC
                //   SDAC de la Vision864 ; vid_sdac_ramdac.c n'est pas transcrit et chip vaut
                //   toujours S3_TRIO64 ici. DEVIATION : la branche, inatteignable, lève.
                throw new InvalidOperationException("vid_s3: sdac_ramdac_in non transcrit (Vision864)");

        case 0x3d4:
                return svga.crtcreg;
        case 0x3d5:
                //                pclog("Read CRTC R%02X %02x %04X:%04X\n", svga->crtcreg, svga->crtc[svga->crtcreg], CS, pc);
                switch (svga.crtcreg)
                {
                case 0x2d:
                        return 0x88; /*Extended chip ID*/
                case 0x2e:
                        return s3.id_ext; /*New chip ID*/
                case 0x2f:
                        return 0; /*Revision level*/
                case 0x30:
                        return s3.id; /*Chip ID*/
                case 0x31:
                        return (uint8_t)((svga.crtc[0x31] & 0xcf) | ((s3.ma_ext & 3) << 4));
                case 0x35:
                        return (uint8_t)((svga.crtc[0x35] & 0xf0) | (s3.bank & 0xf));
                case 0x45:
                        s3.hwc_col_stack_pos = 0;
                        break;
                case 0x51:
                        return (uint8_t)((svga.crtc[0x51] & 0xf0) | ((s3.bank >> 2) & 0xc) | ((s3.ma_ext >> 2) & 3));
                case 0x69:
                        return s3.ma_ext;
                case 0x6a:
                        return s3.bank;
                }
                return svga.crtc[svga.crtcreg];
        }
        return svga_in(addr, svga);
    }

    // pcem: vid_s3.c:1191-1261
    internal static void s3_recalctimings(svga_t svga)
    {
        s3_t s3 = (s3_t)svga.p;
        svga.hdisp = svga.hdisp_old;
        int clk_sel = (svga.miscout >> 2) & 3;

        if (clk_sel == 3 && s3.chip == S3_VISION864)
                clk_sel = svga.crtc[0x42] & 0xf;

        //        pclog("%i %i\n", svga->hdisp, svga->hdisp_time);
        //        pclog("recalctimings\n");
        svga.ma_latch |= (uint32_t)(s3.ma_ext << 16);
        //        pclog("SVGA_MA %08X\n", svga_ma);
        if ((svga.crtc[0x5d] & 0x01) != 0)
                svga.htotal += 0x100;
        if ((svga.crtc[0x5d] & 0x02) != 0)
        {
                svga.hdisp_time += 0x100;
                svga.hdisp += 0x100 * (((svga.seqregs[1] & 8) != 0) ? 16 : 8);
        }
        if ((svga.crtc[0x5e] & 0x01) != 0)
                svga.vtotal += 0x400;
        if ((svga.crtc[0x5e] & 0x02) != 0)
                svga.dispend += 0x400;
        if ((svga.crtc[0x5e] & 0x04) != 0)
                svga.vblankstart += 0x400;
        if ((svga.crtc[0x5e] & 0x10) != 0)
                svga.vsyncstart += 0x400;
        if ((svga.crtc[0x5e] & 0x40) != 0)
                svga.split += 0x400;
        if ((svga.crtc[0x51] & 0x30) != 0)
                svga.rowoffset += (svga.crtc[0x51] & 0x30) << 4;
        else if ((svga.crtc[0x43] & 0x04) != 0)
                svga.rowoffset += 0x100;
        if (svga.rowoffset == 0)
                svga.rowoffset = 256;
        svga.interlace = svga.crtc[0x42] & 0x20;
        svga.clock = (pit.cpuclock * (float)(1UL << 32)) / s3.getclock(clk_sel, s3.getclock_p);

        switch (svga.crtc[0x67] >> 4)
        {
        case 3:
        case 5:
        case 7:
                svga.clock /= 2;
                break;
        }

        svga.lowres = ((svga.gdcreg[5] & 0x40) != 0 && (svga.crtc[0x3a] & 0x10) != 0) ? 0 : 1;
        if ((svga.gdcreg[5] & 0x40) != 0 && (svga.crtc[0x3a] & 0x10) != 0)
        {
                switch (svga.bpp)
                {
                case 8:
                        svga.render = svga_render_8bpp_highres;
                        break;
                case 15:
                        svga.render = svga_render_15bpp_highres;
                        svga.hdisp /= 2;
                        break;
                case 16:
                        svga.render = svga_render_16bpp_highres;
                        svga.hdisp /= 2;
                        break;
                case 24:
                        svga.render = svga_render_24bpp_highres;
                        svga.hdisp /= 3;
                        break;
                case 32:
                        svga.render = svga_render_32bpp_highres;
                        if (s3.chip != S3_TRIO32 && s3.chip != S3_TRIO64)
                                svga.hdisp /= 4;
                        break;
                }
        }
    }

    // pcem: vid_s3.c:1263-1353
    internal static void s3_updatemapping(s3_t s3)
    {
        svga_t svga = s3.svga;

        //        video_write_a000_w = video_write_a000_l = NULL;

        if ((s3.pci_regs[PCI_REG_COMMAND] & PCI_COMMAND_MEM) == 0)
        {
                //                pclog("Update mapping - PCI disabled\n");
                mem_mapping_disable(svga.mapping);
                mem_mapping_disable(s3.linear_mapping);
                mem_mapping_disable(s3.mmio_mapping);
                return;
        }

        //        pclog("Update mapping - bank %02X ", svga->gdcreg[6] & 0xc);
        /*Banked framebuffer*/
        if ((svga.crtc[0x31] & 0x08) != 0) /*Enhanced mode mappings*/
        {
                /*Enhanced mapping forces 64kb at 0xa0000*/
                mem_mapping_set_addr(svga.mapping, 0xa0000, 0x10000);
                svga.banked_mask = 0xffff;
        }
        else
                switch (svga.gdcreg[6] & 0xc) /*VGA mapping*/
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

        //        pclog("Linear framebuffer %02X ", svga->crtc[0x58] & 0x10);
        if ((svga.crtc[0x58] & 0x10) != 0) /*Linear framebuffer*/
        {
                mem_mapping_disable(svga.mapping);

                s3.linear_base = (uint32_t)((svga.crtc[0x5a] << 16) | (svga.crtc[0x59] << 24));
                switch (svga.crtc[0x58] & 3)
                {
                case 0: /*64k*/
                        s3.linear_size = 0x10000;
                        break;
                case 1: /*1mb*/
                        s3.linear_size = 0x100000;
                        break;
                case 2: /*2mb*/
                        s3.linear_size = 0x200000;
                        break;
                case 3: /* 4MB or 8MB depending on card maximum */
                        switch (s3.chip)
                        {
                        case S3_TRIO64: /* 4MB cards go first.... */
                                s3.linear_size = 0x400000;
                                break;
                        default: /* All other S3's should be 8MB maximum */
                                s3.linear_size = 0x800000;
                                break;
                        }
                        break;
                }
                s3.linear_base &= ~(s3.linear_size - 1);
                //                pclog("%08X %08X  %02X %02X %02X\n", linear_base, linear_size, crtc[0x58], crtc[0x59],
                //                crtc[0x5a]); pclog("Linear framebuffer at %08X size %08X\n", s3->linear_base, s3->linear_size);
                if (s3.linear_base == 0xa0000)
                {
                        mem_mapping_disable(s3.linear_mapping);
                        if ((svga.crtc[0x53] & 0x10) == 0)
                        {
                                mem_mapping_set_addr(svga.mapping, 0xa0000, 0x10000);
                                svga.banked_mask = 0xffff;
                        }
                        //                        mem_mapping_set_addr(&s3->linear_mapping, 0xa0000, 0x10000);
                }
                else
                        mem_mapping_set_addr(s3.linear_mapping, s3.linear_base, s3.linear_size);
        }
        else
                mem_mapping_disable(s3.linear_mapping);

        //        pclog("Memory mapped IO %02X\n", svga->crtc[0x53] & 0x10);
        if ((svga.crtc[0x53] & 0x10) != 0) /*Memory mapped IO*/
        {
                mem_mapping_disable(svga.mapping);
                mem_mapping_enable(s3.mmio_mapping);
        }
        else
                mem_mapping_disable(s3.mmio_mapping);
    }

    // pcem: vid_s3.c:1355-1371
    private static float s3_trio64_getclock(int clock, object? p)
    {
        s3_t s3 = (s3_t)p;
        svga_t svga = s3.svga;
        float t;
        int m, n1, n2;
        //        pclog("Trio64_getclock %i %02X %02X\n", clock, svga->seqregs[0x13], svga->seqregs[0x12]);
        if (clock == 0)
                return (float)25175000.0;
        if (clock == 1)
                return (float)28322000.0;
        m = svga.seqregs[0x13] + 2;
        n1 = (svga.seqregs[0x12] & 0x1f) + 2;
        n2 = ((svga.seqregs[0x12] >> 5) & 0x07);
        t = (float)((14318184.0 * ((float)m / (float)n1)) / (float)(1 << n2));
        //        pclog("TRIO64 clock %i %i %i %f  %f %i\n", m, n1, n2, t, 14318184.0 * ((float)m / (float)n1), 1 << n2);
        return t;
    }

    // pcem: vid_s3.c:1373-1396
    internal static void s3_accel_out(uint16_t port, uint8_t val, object p)
    {
        s3_t s3 = (s3_t)p;
        //        pclog("Accel out %04X %02X\n", port, val);

        if (port >= 0x8000)
        {
                s3_queue(s3, port, val, FIFO_OUT_BYTE);
        }
        else
                switch (port)
                {
                case 0x42e8:
                        s3.subsys_stat = (uint8_t)(s3.subsys_stat & ~val);
                        s3_update_irqs(s3);
                        break;
                case 0x42e9:
                        s3.subsys_cntl = val;
                        s3_update_irqs(s3);
                        break;
                case 0x46e8:
                        s3.accel.setup_md = val;
                        break;
                case 0x4ae8:
                        s3.accel.advfunc_cntl = val;
                        break;
                }
    }

    // pcem: vid_s3.c:1398-1402
    internal static void s3_accel_out_w(uint16_t port, uint16_t val, object p)
    {
        s3_t s3 = (s3_t)p;
        //        pclog("Accel out w %04X %04X\n", port, val);
        s3_queue(s3, port, val, FIFO_OUT_WORD);
    }

    // pcem: vid_s3.c:1404-1408
    internal static void s3_accel_out_l(uint16_t port, uint32_t val, object p)
    {
        s3_t s3 = (s3_t)p;
        //        pclog("Accel out l %04X %08X\n", port, val);
        s3_queue(s3, port, val, FIFO_OUT_DWORD);
    }

    // pcem: vid_s3.c:1410-1619
    internal static uint8_t s3_accel_in(uint16_t port, object p)
    {
        s3_t s3 = (s3_t)p;
        int temp;
        //        pclog("Accel in  %04X\n", port);
        switch (port)
        {
        case 0x42e8:
                return s3.subsys_stat;
        case 0x42e9:
                return s3.subsys_cntl;

        case 0x82e8:
                s3_wait_fifo_idle(s3);
                return (uint8_t)(s3.accel.cur_y & 0xff);
        case 0x82e9:
                s3_wait_fifo_idle(s3);
                return (uint8_t)(s3.accel.cur_y >> 8);

        case 0x86e8:
                s3_wait_fifo_idle(s3);
                return (uint8_t)(s3.accel.cur_x & 0xff);
        case 0x86e9:
                s3_wait_fifo_idle(s3);
                return (uint8_t)(s3.accel.cur_x >> 8);

        case 0x8ae8:
                s3_wait_fifo_idle(s3);
                return (uint8_t)(s3.accel.desty_axstp & 0xff);
        case 0x8ae9:
                s3_wait_fifo_idle(s3);
                return (uint8_t)(s3.accel.desty_axstp >> 8);

        case 0x8ee8:
                s3_wait_fifo_idle(s3);
                return (uint8_t)(s3.accel.destx_distp & 0xff);
        case 0x8ee9:
                s3_wait_fifo_idle(s3);
                return (uint8_t)(s3.accel.destx_distp >> 8);

        case 0x92e8:
                s3_wait_fifo_idle(s3);
                return (uint8_t)(s3.accel.err_term & 0xff);
        case 0x92e9:
                s3_wait_fifo_idle(s3);
                return (uint8_t)(s3.accel.err_term >> 8);

        case 0x96e8:
                s3_wait_fifo_idle(s3);
                return (uint8_t)(s3.accel.maj_axis_pcnt & 0xff);
        case 0x96e9:
                s3_wait_fifo_idle(s3);
                return (uint8_t)(s3.accel.maj_axis_pcnt >> 8);

        case 0x9ae8:
                if (s3.blitter_busy == 0)
                        wake_fifo_thread(s3);
                if (FIFO_FULL(s3))
                        return 0xff; /*FIFO full*/
                return 0;            /*FIFO empty*/
        case 0x9ae9:
                if (s3.blitter_busy == 0)
                        wake_fifo_thread(s3);
                temp = 0;
                if (!FIFO_EMPTY(s3) || s3.force_busy != 0)
                        temp |= 0x02; /*Hardware busy*/
                else
                        temp |= 0x04; /*FIFO empty*/
                s3.force_busy = 0;
                if (FIFO_FULL(s3))
                        temp |= 0xf8; /*FIFO full*/
                return (uint8_t)temp;

        case 0xa2e8:
                s3_wait_fifo_idle(s3);
                return (uint8_t)(s3.accel.bkgd_color & 0xff);
        case 0xa2e9:
                s3_wait_fifo_idle(s3);
                return (uint8_t)(s3.accel.bkgd_color >> 8);
        case 0xa2ea:
                s3_wait_fifo_idle(s3);
                return (uint8_t)(s3.accel.bkgd_color >> 16);
        case 0xa2eb:
                s3_wait_fifo_idle(s3);
                return (uint8_t)(s3.accel.bkgd_color >> 24);

        case 0xa6e8:
                s3_wait_fifo_idle(s3);
                return (uint8_t)(s3.accel.frgd_color & 0xff);
        case 0xa6e9:
                s3_wait_fifo_idle(s3);
                return (uint8_t)(s3.accel.frgd_color >> 8);
        case 0xa6ea:
                s3_wait_fifo_idle(s3);
                return (uint8_t)(s3.accel.frgd_color >> 16);
        case 0xa6eb:
                s3_wait_fifo_idle(s3);
                return (uint8_t)(s3.accel.frgd_color >> 24);

        case 0xaae8:
                s3_wait_fifo_idle(s3);
                return (uint8_t)(s3.accel.wrt_mask & 0xff);
        case 0xaae9:
                s3_wait_fifo_idle(s3);
                return (uint8_t)(s3.accel.wrt_mask >> 8);
        case 0xaaea:
                s3_wait_fifo_idle(s3);
                return (uint8_t)(s3.accel.wrt_mask >> 16);
        case 0xaaeb:
                s3_wait_fifo_idle(s3);
                return (uint8_t)(s3.accel.wrt_mask >> 24);

        case 0xaee8:
                s3_wait_fifo_idle(s3);
                return (uint8_t)(s3.accel.rd_mask & 0xff);
        case 0xaee9:
                s3_wait_fifo_idle(s3);
                return (uint8_t)(s3.accel.rd_mask >> 8);
        case 0xaeea:
                s3_wait_fifo_idle(s3);
                return (uint8_t)(s3.accel.rd_mask >> 16);
        case 0xaeeb:
                s3_wait_fifo_idle(s3);
                return (uint8_t)(s3.accel.rd_mask >> 24);

        case 0xb2e8:
                s3_wait_fifo_idle(s3);
                return (uint8_t)(s3.accel.color_cmp & 0xff);
        case 0xb2e9:
                s3_wait_fifo_idle(s3);
                return (uint8_t)(s3.accel.color_cmp >> 8);
        case 0xb2ea:
                s3_wait_fifo_idle(s3);
                return (uint8_t)(s3.accel.color_cmp >> 16);
        case 0xb2eb:
                s3_wait_fifo_idle(s3);
                return (uint8_t)(s3.accel.color_cmp >> 24);

        case 0xb6e8:
                s3_wait_fifo_idle(s3);
                return s3.accel.bkgd_mix;

        case 0xbae8:
                s3_wait_fifo_idle(s3);
                return s3.accel.frgd_mix;

        case 0xbee8:
                s3_wait_fifo_idle(s3);
                temp = s3.accel.multifunc[0xf] & 0xf;
                switch (temp)
                {
                case 0x0:
                        return (uint8_t)(s3.accel.multifunc[0x0] & 0xff);
                case 0x1:
                        return (uint8_t)(s3.accel.multifunc[0x1] & 0xff);
                case 0x2:
                        return (uint8_t)(s3.accel.multifunc[0x2] & 0xff);
                case 0x3:
                        return (uint8_t)(s3.accel.multifunc[0x3] & 0xff);
                case 0x4:
                        return (uint8_t)(s3.accel.multifunc[0x4] & 0xff);
                case 0x5:
                        return (uint8_t)(s3.accel.multifunc[0xa] & 0xff);
                case 0x6:
                        return (uint8_t)(s3.accel.multifunc[0xe] & 0xff);
                case 0x7:
                        return (uint8_t)(s3.accel.cmd & 0xff);
                case 0x8:
                        return (uint8_t)(s3.accel.subsys_cntl & 0xff);
                case 0x9:
                        return (uint8_t)(s3.accel.setup_md & 0xff);
                case 0xa:
                        return (uint8_t)(s3.accel.multifunc[0xd] & 0xff);
                }
                return 0xff;
        case 0xbee9:
                s3_wait_fifo_idle(s3);
                temp = s3.accel.multifunc[0xf] & 0xf;
                s3.accel.multifunc[0xf]++;
                switch (temp)
                {
                case 0x0:
                        return (uint8_t)(s3.accel.multifunc[0x0] >> 8);
                case 0x1:
                        return (uint8_t)(s3.accel.multifunc[0x1] >> 8);
                case 0x2:
                        return (uint8_t)(s3.accel.multifunc[0x2] >> 8);
                case 0x3:
                        return (uint8_t)(s3.accel.multifunc[0x3] >> 8);
                case 0x4:
                        return (uint8_t)(s3.accel.multifunc[0x4] >> 8);
                case 0x5:
                        return (uint8_t)(s3.accel.multifunc[0xa] >> 8);
                case 0x6:
                        return (uint8_t)(s3.accel.multifunc[0xe] >> 8);
                case 0x7:
                        return (uint8_t)(s3.accel.cmd >> 8);
                case 0x8:
                        return (uint8_t)((s3.accel.subsys_cntl >> 8) & ~0xe000);
                case 0x9:
                        return (uint8_t)((s3.accel.setup_md >> 8) & ~0xf000);
                case 0xa:
                        return (uint8_t)(s3.accel.multifunc[0xd] >> 8);
                }
                return 0xff;

        case 0xe2e8:
        case 0xe2e9:
        case 0xe2ea:
        case 0xe2eb: /*PIX_TRANS*/
                break;
        }
        return 0;
    }

    // pcem: vid_s3.c:1621-1625
    internal static void s3_accel_write(uint32_t addr, uint8_t val, object? p)
    {
        s3_t s3 = (s3_t)p;
        //        pclog("s3_accel_write %08x %02x\n", addr, val);
        s3_queue(s3, addr & 0xffff, val, FIFO_WRITE_BYTE);
    }
    // pcem: vid_s3.c:1626-1630
    internal static void s3_accel_write_w(uint32_t addr, uint16_t val, object? p)
    {
        s3_t s3 = (s3_t)p;
        //        pclog("s3_accel_write_w %08x %04x\n", addr, val);
        s3_queue(s3, addr & 0xffff, val, FIFO_WRITE_WORD);
    }
    // pcem: vid_s3.c:1631-1635
    internal static void s3_accel_write_l(uint32_t addr, uint32_t val, object? p)
    {
        s3_t s3 = (s3_t)p;
        //        pclog("s3_accel_write_l %08x %08x\n", addr, val);
        s3_queue(s3, addr & 0xffff, val, FIFO_WRITE_DWORD);
    }

    // pcem: vid_s3.c:1637-1641
    internal static uint8_t s3_accel_read(uint32_t addr, object? p)
    {
        if ((addr & 0x8000) != 0)
                return s3_accel_in((uint16_t)(addr & 0xffff), p);
        return 0;
    }

    // pcem: vid_s3.c:1643-1678
    internal static void polygon_setup(s3_t s3)
    {
        if (s3.accel.point_1_updated != 0)
        {
                int start_x = s3.accel.poly_cx;
                int start_y = s3.accel.poly_cy;
                int end_x = s3.accel.destx_distp << 20;
                int end_y = s3.accel.desty_axstp;

                if ((end_y - start_y) != 0)
                        // pcem bug, not reproduced: PB-86 — DEVIATION : (end_x - start_x) déborde (coordonnées
                        // << 20) ; INT_MIN / -1 fait tomber PCem (SIGFPE). Le quotient est pris replié (R9).
                        s3.accel.poly_dx1 = (end_y - start_y) == -1 ? unchecked(-(end_x - start_x)) : (end_x - start_x) / (end_y - start_y);
                else
                        s3.accel.poly_dx1 = 0;

                s3.accel.point_1_updated = 0;

                if (end_y == s3.accel.poly_cy)
                {
                        s3.accel.poly_cx = end_x;
                        s3.accel.poly_x = end_x >> 20;
                }
        }
        if (s3.accel.point_2_updated != 0)
        {
                int start_x = s3.accel.poly_cx2;
                int start_y = s3.accel.poly_cy2;
                int end_x = s3.accel.x2 << 20;
                int end_y = s3.accel.desty_axstp2;

                if ((end_y - start_y) != 0)
                        // pcem bug, not reproduced: PB-86 (voir poly_dx1)
                        s3.accel.poly_dx2 = (end_y - start_y) == -1 ? unchecked(-(end_x - start_x)) : (end_x - start_x) / (end_y - start_y);
                else
                        s3.accel.poly_dx2 = 0;

                s3.accel.point_2_updated = 0;

                if (end_y == s3.accel.poly_cy)
                        s3.accel.poly_cx2 = end_x;
        }
    }

    /*Remap address for chain-4/doubleword style layout*/
    // pcem: vid_s3.c:1681-1683
    private static uint32_t dword_remap(uint32_t in_addr)
    {
        return ((in_addr << 2) & 0x3fff0) | ((in_addr >> 14) & 0xc) | (in_addr & ~0x3fffcu);
    }
    // pcem: vid_s3.c:1684-1686
    private static uint32_t dword_remap_w(uint32_t in_addr)
    {
        return ((in_addr << 2) & 0x1fff8) | ((in_addr >> 14) & 0x6) | (in_addr & ~0x1fffeu);
    }
    // pcem: vid_s3.c:1687-1689
    private static uint32_t dword_remap_l(uint32_t in_addr)
    {
        return ((in_addr << 2) & 0xfffc) | ((in_addr >> 14) & 0x3) | (in_addr & ~0xffffu);
    }

    // Les macros READ_SRC, READ_DST, MIX et WRITE de PCem lisent les locales de s3_accel_start
    // (vram_w, vram_l, vram_mask, rd_mask, mix_dat, mix_mask) : elles deviennent des fonctions
    // qui les reçoivent en paramètres. Les alias `uint16_t *vram_w = (uint16_t *)svga->vram` et
    // `uint32_t *vram_l` (vid_s3.c:1787-1788) deviennent MemoryMarshal.Cast sur le même tableau :
    // même lecture petit-boutiste qu'en C sur un hôte x86.

    // pcem: vid_s3.c:1691-1699 (macro READ_SRC)
    private static uint32_t READ_SRC(s3_t s3, svga_t svga, uint32_t addr, int vram_mask, uint32_t rd_mask)
    {
        uint32_t dat;
        if (s3.bpp == 0)
                dat = svga.vram[dword_remap(addr) & s3.vram_mask];
        else if (s3.bpp == 1)
                dat = MemoryMarshal.Cast<uint8_t, uint16_t>(svga.vram.AsSpan())[(int)(dword_remap_w(addr) & (s3.vram_mask >> 1))];
        else
                dat = MemoryMarshal.Cast<uint8_t, uint32_t>(svga.vram.AsSpan())[(int)(dword_remap_l(addr) & (s3.vram_mask >> 2))];
        if (vram_mask != 0)
                dat = ((dat & rd_mask) == rd_mask) ? 1u : 0u;
        return dat;
    }

    // pcem: vid_s3.c:1701-1707 (macro READ_DST)
    private static uint32_t READ_DST(s3_t s3, svga_t svga, uint32_t addr)
    {
        uint32_t dat;
        if (s3.bpp == 0)
                dat = svga.vram[dword_remap(addr) & s3.vram_mask];
        else if (s3.bpp == 1)
                dat = MemoryMarshal.Cast<uint8_t, uint16_t>(svga.vram.AsSpan())[(int)(dword_remap_w(addr) & (s3.vram_mask >> 1))];
        else
                dat = MemoryMarshal.Cast<uint8_t, uint32_t>(svga.vram.AsSpan())[(int)(dword_remap_l(addr) & (s3.vram_mask >> 2))];
        return dat;
    }

    // pcem: vid_s3.c:1709-1763 (macro MIX)
    private static uint32_t MIX(s3_t s3, uint32_t mix_dat, uint32_t mix_mask, uint32_t src_dat, uint32_t dest_dat)
    {
        {
                uint32_t old_dest_dat = dest_dat;
                switch (((mix_dat & mix_mask) != 0) ? (s3.accel.frgd_mix & 0xf) : (s3.accel.bkgd_mix & 0xf))
                {
                case 0x0:
                        dest_dat = ~dest_dat;
                        break;
                case 0x1:
                        dest_dat = 0;
                        break;
                case 0x2:
                        dest_dat = ~0u;
                        break;
                case 0x3:
                        dest_dat = dest_dat;
                        break;
                case 0x4:
                        dest_dat = ~src_dat;
                        break;
                case 0x5:
                        dest_dat = src_dat ^ dest_dat;
                        break;
                case 0x6:
                        dest_dat = ~(src_dat ^ dest_dat);
                        break;
                case 0x7:
                        dest_dat = src_dat;
                        break;
                case 0x8:
                        dest_dat = ~(src_dat & dest_dat);
                        break;
                case 0x9:
                        dest_dat = ~src_dat | dest_dat;
                        break;
                case 0xa:
                        dest_dat = src_dat | ~dest_dat;
                        break;
                case 0xb:
                        dest_dat = src_dat | dest_dat;
                        break;
                case 0xc:
                        dest_dat = src_dat & dest_dat;
                        break;
                case 0xd:
                        dest_dat = src_dat & ~dest_dat;
                        break;
                case 0xe:
                        dest_dat = ~src_dat & dest_dat;
                        break;
                case 0xf:
                        dest_dat = ~(src_dat | dest_dat);
                        break;
                }
                dest_dat = (dest_dat & s3.accel.wrt_mask) | (old_dest_dat & ~s3.accel.wrt_mask);
        }
        return dest_dat;
    }

    // pcem: vid_s3.c:1765-1775 (macro WRITE)
    private static void WRITE(s3_t s3, svga_t svga, uint32_t addr, uint32_t dest_dat)
    {
        if (s3.bpp == 0)
        {
                svga.vram[dword_remap(addr) & s3.vram_mask] = (uint8_t)dest_dat;
                svga.changedvram[(dword_remap(addr) & s3.vram_mask) >> 12] = (uint8_t)changeframecount;
        }
        else if (s3.bpp == 1)
        {
                MemoryMarshal.Cast<uint8_t, uint16_t>(svga.vram.AsSpan())[(int)(dword_remap_w(addr) & (s3.vram_mask >> 1))] = (uint16_t)dest_dat;
                svga.changedvram[(dword_remap_w(addr) & (s3.vram_mask >> 1)) >> 11] = (uint8_t)changeframecount;
        }
        else
        {
                MemoryMarshal.Cast<uint8_t, uint32_t>(svga.vram.AsSpan())[(int)(dword_remap_l(addr) & (s3.vram_mask >> 2))] = dest_dat;
                svga.changedvram[(dword_remap_l(addr) & (s3.vram_mask >> 2)) >> 10] = (uint8_t)changeframecount;
        }
    }

    // pcem: vid_s3.c:1777-2637
    internal static void s3_accel_start(int count, int cpu_input, uint32_t mix_dat, uint32_t cpu_dat, s3_t s3)
    {
        svga_t svga = s3.svga;
        // C# exige l'affectation définie : en C src_dat/dest_dat naissent indéterminés, mais chaque
        //   switch qui les lit couvre les quatre valeurs possibles (0-3) avant toute lecture.
        uint32_t src_dat = 0, dest_dat = 0;
        int frgd_mix, bkgd_mix;
        int clip_t = s3.accel.multifunc[1] & 0xfff;
        int clip_l = s3.accel.multifunc[2] & 0xfff;
        int clip_b = s3.accel.multifunc[3] & 0xfff;
        int clip_r = s3.accel.multifunc[4] & 0xfff;
        int vram_mask = ((s3.accel.multifunc[0xa] & 0xc0) == 0xc0) ? 1 : 0;
        uint32_t mix_mask = 0;
        // vram_w / vram_l (vid_s3.c:1787-1788) : voir READ_SRC, READ_DST et WRITE.
        uint32_t compare = s3.accel.color_cmp;
        int compare_mode = (s3.accel.multifunc[0xe] >> 7) & 3;
        uint32_t rd_mask = s3.accel.rd_mask;
        int cmd = s3.accel.cmd >> 13;
        uint32_t srcbase, dstbase;

        if ((s3.chip == S3_TRIO64) && (s3.accel.cmd & (1 << 11)) != 0)
                cmd |= 8;

        if (((s3.accel.multifunc[13] >> 4) & 7) != 0)
                srcbase = (uint32_t)(0x100000 * ((s3.accel.multifunc[13] >> 4) & 3));
        else
                srcbase = (uint32_t)(0x100000 * ((s3.accel.multifunc[14] >> 2) & 3));
        if (((s3.accel.multifunc[13] >> 0) & 7) != 0)
                dstbase = (uint32_t)(0x100000 * ((s3.accel.multifunc[13] >> 0) & 3));
        else
                dstbase = (uint32_t)(0x100000 * ((s3.accel.multifunc[14] >> 0) & 3));
        if (s3.bpp == 1)
        {
                srcbase >>= 1;
                dstbase >>= 1;
        }
        else if (s3.bpp == 3)
        {
                srcbase >>= 2;
                dstbase >>= 2;
        }

        s3.force_busy = 1;
        // return;
        //        if (!cpu_input) pclog("Start S3 command %i  %i, %i  %i, %i (clip %i, %i to %i, %i  %i)\n", s3->accel.cmd >> 13,
        //        s3->accel.cur_x, s3->accel.cur_y, s3->accel.maj_axis_pcnt & 0xfff, s3->accel.multifunc[0]  & 0xfff, clip_l,
        //        clip_t, clip_r, clip_b, s3->accel.multifunc[0xe] & 0x20); else            pclog("      S3 command %i, %i, %08x
        //        %08x\n", s3->accel.cmd >> 13, count, mix_dat, cpu_dat);

        if (cpu_input == 0)
                s3.accel.dat_count = 0;
        if (cpu_input != 0 && (s3.accel.multifunc[0xa] & 0xc0) != 0x80)
        {
                if (s3.bpp == 3 && count == 2)
                {
                        if (s3.accel.dat_count != 0)
                        {
                                cpu_dat = ((cpu_dat & 0xffff) << 16) | s3.accel.dat_buf;
                                count = 4;
                                s3.accel.dat_count = 0;
                        }
                        else
                        {
                                s3.accel.dat_buf = cpu_dat & 0xffff;
                                s3.accel.dat_count = 1;
                        }
                }
                if (s3.bpp == 1)
                        count >>= 1;
                if (s3.bpp == 3)
                        count >>= 2;
        }

        if (s3.bpp == 0)
                rd_mask &= 0xff;
        else if (s3.bpp == 1)
                rd_mask &= 0xffff;

        switch (s3.accel.cmd & 0x600)
        {
        case 0x000:
                mix_mask = 0x80;
                break;
        case 0x200:
                mix_mask = 0x8000;
                break;
        case 0x400:
                mix_mask = 0x80000000;
                break;
        case 0x600:
                mix_mask = (s3.chip == S3_TRIO32) ? 0x80u : 0x80000000u;
                break;
        }

        if (s3.bpp == 0)
                compare &= 0xff;
        if (s3.bpp == 1)
                compare &= 0xffff;
        switch (cmd)
        {
        case 1:                 /*Draw line*/
                if (cpu_input == 0) /*!cpu_input is trigger to start operation*/
                {
                        s3.accel.cx = s3.accel.cur_x;
                        if ((s3.accel.cur_x & 0x1000) != 0)
                                s3.accel.cx |= ~0xfff;
                        s3.accel.cy = s3.accel.cur_y;
                        if ((s3.accel.cur_y & 0x1000) != 0)
                                s3.accel.cy |= ~0xfff;

                        s3.accel.sy = s3.accel.maj_axis_pcnt;
                }
                if ((s3.accel.cmd & 0x100) != 0 && cpu_input == 0)
                        return; /*Wait for data from CPU*/

                frgd_mix = (s3.accel.frgd_mix >> 5) & 3;
                bkgd_mix = (s3.accel.bkgd_mix >> 5) & 3;

                if ((s3.accel.cmd & 8) != 0) /*Radial*/
                {
                        while (count-- != 0 && s3.accel.sy >= 0)
                        {
                                if ((s3.accel.cx & 0xfff) >= clip_l && (s3.accel.cx & 0xfff) <= clip_r &&
                                    (s3.accel.cy & 0xfff) >= clip_t && (s3.accel.cy & 0xfff) <= clip_b)
                                {
                                        switch (((mix_dat & mix_mask) != 0) ? frgd_mix : bkgd_mix)
                                        {
                                        case 0:
                                                src_dat = s3.accel.bkgd_color;
                                                break;
                                        case 1:
                                                src_dat = s3.accel.frgd_color;
                                                break;
                                        case 2:
                                                src_dat = cpu_dat;
                                                break;
                                        case 3:
                                                src_dat = 0;
                                                break;
                                        }

                                        if ((compare_mode == 2 && src_dat != compare) ||
                                            (compare_mode == 3 && src_dat == compare) || compare_mode < 2)
                                        {
                                                dest_dat = READ_DST(s3, svga, (uint32_t)((s3.accel.cy * s3.width) + s3.accel.cx));

                                                dest_dat = MIX(s3, mix_dat, mix_mask, src_dat, dest_dat);

                                                        WRITE(s3, svga, (uint32_t)((s3.accel.cy * s3.width) + s3.accel.cx), dest_dat);
                                        }
                                }

                                mix_dat <<= 1;
                                mix_dat |= 1;
                                if (s3.bpp == 0)
                                        cpu_dat >>= 8;
                                else
                                        cpu_dat >>= 16;
                                if (s3.accel.sy == 0)
                                        break;

                                switch (s3.accel.cmd & 0xe0)
                                {
                                case 0x00:
                                        s3.accel.cx++;
                                        break;
                                case 0x20:
                                        s3.accel.cx++;
                                        s3.accel.cy--;
                                        break;
                                case 0x40:
                                        s3.accel.cy--;
                                        break;
                                case 0x60:
                                        s3.accel.cx--;
                                        s3.accel.cy--;
                                        break;
                                case 0x80:
                                        s3.accel.cx--;
                                        break;
                                case 0xa0:
                                        s3.accel.cx--;
                                        s3.accel.cy++;
                                        break;
                                case 0xc0:
                                        s3.accel.cy++;
                                        break;
                                case 0xe0:
                                        s3.accel.cx++;
                                        s3.accel.cy++;
                                        break;
                                }
                                s3.accel.sy--;
                        }
                        s3.accel.cur_x = (uint16_t)s3.accel.cx;
                        s3.accel.cur_y = (uint16_t)s3.accel.cy;
                }
                else /*Bresenham*/
                {
                        while (count-- != 0 && s3.accel.sy >= 0)
                        {
                                if ((s3.accel.cx & 0xfff) >= clip_l && (s3.accel.cx & 0xfff) <= clip_r &&
                                    (s3.accel.cy & 0xfff) >= clip_t && (s3.accel.cy & 0xfff) <= clip_b)
                                {
                                        switch (((mix_dat & mix_mask) != 0) ? frgd_mix : bkgd_mix)
                                        {
                                        case 0:
                                                src_dat = s3.accel.bkgd_color;
                                                break;
                                        case 1:
                                                src_dat = s3.accel.frgd_color;
                                                break;
                                        case 2:
                                                src_dat = cpu_dat;
                                                break;
                                        case 3:
                                                src_dat = 0;
                                                break;
                                        }

                                        if ((compare_mode == 2 && src_dat != compare) ||
                                            (compare_mode == 3 && src_dat == compare) || compare_mode < 2)
                                        {
                                                dest_dat = READ_DST(s3, svga, (uint32_t)((s3.accel.cy * s3.width) + s3.accel.cx));

                                                //                                        pclog("Line : %04i, %04i (%06X) - %02X
                                                //                                        (%02X %04X %05X) %02X (%02X %02X)  ",
                                                //                                        s3->accel.cx, s3->accel.cy,
                                                //                                        s3->accel.dest + s3->accel.cx, src_dat,
                                                //                                        vram[s3->accel.src + s3->accel.cx],
                                                //                                        mix_dat & mix_mask, s3->accel.src +
                                                //                                        s3->accel.cx, dest_dat,
                                                //                                        s3->accel.frgd_color,
                                                //                                        s3->accel.bkgd_color);

                                                dest_dat = MIX(s3, mix_dat, mix_mask, src_dat, dest_dat);

                                                        //                                        pclog("%02X\n", dest_dat);

                                                        WRITE(s3, svga, (uint32_t)((s3.accel.cy * s3.width) + s3.accel.cx), dest_dat);
                                        }
                                }

                                mix_dat <<= 1;
                                mix_dat |= 1;
                                if (s3.bpp == 0)
                                        cpu_dat >>= 8;
                                else
                                        cpu_dat >>= 16;

                                //                                pclog("%i, %i - %i %i  %i %i\n", s3->accel.cx, s3->accel.cy,
                                //                                s3->accel.err_term, s3->accel.maj_axis_pcnt,
                                //                                s3->accel.desty_axstp, s3->accel.destx_distp);

                                if (s3.accel.sy == 0)
                                        break;

                                if (s3.accel.err_term >= s3.accel.maj_axis_pcnt)
                                {
                                        s3.accel.err_term += s3.accel.destx_distp;
                                        /*Step minor axis*/
                                        switch (s3.accel.cmd & 0xe0)
                                        {
                                        case 0x00:
                                                s3.accel.cy--;
                                                break;
                                        case 0x20:
                                                s3.accel.cy--;
                                                break;
                                        case 0x40:
                                                s3.accel.cx--;
                                                break;
                                        case 0x60:
                                                s3.accel.cx++;
                                                break;
                                        case 0x80:
                                                s3.accel.cy++;
                                                break;
                                        case 0xa0:
                                                s3.accel.cy++;
                                                break;
                                        case 0xc0:
                                                s3.accel.cx--;
                                                break;
                                        case 0xe0:
                                                s3.accel.cx++;
                                                break;
                                        }
                                }
                                else
                                        s3.accel.err_term += s3.accel.desty_axstp;

                                /*Step major axis*/
                                switch (s3.accel.cmd & 0xe0)
                                {
                                case 0x00:
                                        s3.accel.cx--;
                                        break;
                                case 0x20:
                                        s3.accel.cx++;
                                        break;
                                case 0x40:
                                        s3.accel.cy--;
                                        break;
                                case 0x60:
                                        s3.accel.cy--;
                                        break;
                                case 0x80:
                                        s3.accel.cx--;
                                        break;
                                case 0xa0:
                                        s3.accel.cx++;
                                        break;
                                case 0xc0:
                                        s3.accel.cy++;
                                        break;
                                case 0xe0:
                                        s3.accel.cy++;
                                        break;
                                }
                                s3.accel.sy--;
                        }
                        s3.accel.cur_x = (uint16_t)s3.accel.cx;
                        s3.accel.cur_y = (uint16_t)s3.accel.cy;
                }
                break;

        case 2:                 /*Rectangle fill*/
                if (cpu_input == 0) /*!cpu_input is trigger to start operation*/
                {
                        s3.accel.sx = s3.accel.maj_axis_pcnt & 0xfff;
                        s3.accel.sy = s3.accel.multifunc[0] & 0xfff;
                        s3.accel.cx = s3.accel.cur_x;
                        if ((s3.accel.cur_x & 0x1000) != 0)
                                s3.accel.cx |= ~0xfff;
                        s3.accel.cy = s3.accel.cur_y;
                        if ((s3.accel.cur_y & 0x1000) != 0)
                                s3.accel.cy |= ~0xfff;

                        s3.accel.dest = dstbase + (uint32_t)(s3.accel.cy * s3.width);

                        //                        pclog("Dest %08X  (%i, %i) %04X %04X\n", s3->accel.dest, s3->accel.cx,
                        //                        s3->accel.cy, s3->accel.cur_x, s3->accel.cur_x & 0x1000);
                }
                if ((s3.accel.cmd & 0x100) != 0 && cpu_input == 0)
                        return; /*Wait for data from CPU*/
                //                if ((s3->accel.multifunc[0xa] & 0xc0) == 0x80 && !cpu_input) /*Mix data from CPU*/
                //                   return;

                frgd_mix = (s3.accel.frgd_mix >> 5) & 3;
                bkgd_mix = (s3.accel.bkgd_mix >> 5) & 3;

                while (count-- != 0 && s3.accel.sy >= 0)
                {
                        if ((s3.accel.cx & 0xfff) >= clip_l && (s3.accel.cx & 0xfff) <= clip_r &&
                            (s3.accel.cy & 0xfff) >= clip_t && (s3.accel.cy & 0xfff) <= clip_b)
                        {
                                switch (((mix_dat & mix_mask) != 0) ? frgd_mix : bkgd_mix)
                                {
                                case 0:
                                        src_dat = s3.accel.bkgd_color;
                                        break;
                                case 1:
                                        src_dat = s3.accel.frgd_color;
                                        break;
                                case 2:
                                        src_dat = cpu_dat;
                                        break;
                                case 3:
                                        src_dat = 0;
                                        break;
                                }

                                if ((compare_mode == 2 && src_dat != compare) || (compare_mode == 3 && src_dat == compare) ||
                                    compare_mode < 2)
                                {
                                        dest_dat = READ_DST(s3, svga, s3.accel.dest + (uint32_t)s3.accel.cx);

                                        //                                if (CS != 0xc000) pclog("Write %05X  %02X %02X  %04X
                                        //                                (%02X %02X)  ", s3->accel.dest + s3->accel.cx, src_dat,
                                        //                                dest_dat, mix_dat, s3->accel.frgd_mix,
                                        //                                s3->accel.bkgd_mix);

                                        dest_dat = MIX(s3, mix_dat, mix_mask, src_dat, dest_dat);

                                                //                                if (CS != 0xc000) pclog("%02X\n", dest_dat);

                                                WRITE(s3, svga, s3.accel.dest + (uint32_t)s3.accel.cx, dest_dat);
                                }
                        }

                        mix_dat <<= 1;
                        mix_dat |= 1;
                        if (s3.bpp == 0)
                                cpu_dat >>= 8;
                        else
                                cpu_dat >>= 16;

                        if ((s3.accel.cmd & 0x20) != 0)
                                s3.accel.cx++;
                        else
                                s3.accel.cx--;
                        s3.accel.sx--;
                        if (s3.accel.sx < 0)
                        {
                                if ((s3.accel.cmd & 0x20) != 0)
                                        s3.accel.cx -= (s3.accel.maj_axis_pcnt & 0xfff) + 1;
                                else
                                        s3.accel.cx += (s3.accel.maj_axis_pcnt & 0xfff) + 1;
                                //                                s3->accel.dest -= (s3->accel.maj_axis_pcnt & 0xfff) + 1;
                                s3.accel.sx = s3.accel.maj_axis_pcnt & 0xfff;

                                //                                s3->accel.dest  += s3_width;
                                if ((s3.accel.cmd & 0x80) != 0)
                                        s3.accel.cy++;
                                else
                                        s3.accel.cy--;

                                s3.accel.dest = dstbase + (uint32_t)(s3.accel.cy * s3.width);
                                s3.accel.sy--;

                                if (cpu_input != 0 /* && (s3->accel.multifunc[0xa] & 0xc0) == 0x80*/)
                                        return;
                                if (s3.accel.sy < 0)
                                {
                                        s3.accel.cur_x = (uint16_t)s3.accel.cx;
                                        s3.accel.cur_y = (uint16_t)s3.accel.cy;
                                        return;
                                }
                        }
                }
                break;

        case 6:                 /*BitBlt*/
                if (cpu_input == 0) /*!cpu_input is trigger to start operation*/
                {
                        s3.accel.sx = s3.accel.maj_axis_pcnt & 0xfff;
                        s3.accel.sy = s3.accel.multifunc[0] & 0xfff;

                        s3.accel.dx = s3.accel.destx_distp & 0xfff;
                        if ((s3.accel.destx_distp & 0x1000) != 0)
                                s3.accel.dx |= ~0xfff;
                        s3.accel.dy = s3.accel.desty_axstp & 0xfff;
                        if ((s3.accel.desty_axstp & 0x1000) != 0)
                                s3.accel.dy |= ~0xfff;

                        s3.accel.cx = s3.accel.cur_x & 0xfff;
                        if ((s3.accel.cur_x & 0x1000) != 0)
                                s3.accel.cx |= ~0xfff;
                        s3.accel.cy = s3.accel.cur_y & 0xfff;
                        if ((s3.accel.cur_y & 0x1000) != 0)
                                s3.accel.cy |= ~0xfff;

                        s3.accel.src = srcbase + (uint32_t)(s3.accel.cy * s3.width);
                        s3.accel.dest = dstbase + (uint32_t)(s3.accel.dy * s3.width);

                        //                        pclog("Source %08X Dest %08X  (%i, %i) - (%i, %i)\n", s3->accel.src,
                        //                        s3->accel.dest, s3->accel.cx, s3->accel.cy, s3->accel.dx, s3->accel.dy);
                }
                if ((s3.accel.cmd & 0x100) != 0 && cpu_input == 0)
                        return; /*Wait for data from CPU*/
                //                if ((s3->accel.multifunc[0xa] & 0xc0) == 0x80 && !cpu_input) /*Mix data from CPU*/
                //                  return;

                if (s3.accel.sy < 0)
                        return;

                frgd_mix = (s3.accel.frgd_mix >> 5) & 3;
                bkgd_mix = (s3.accel.bkgd_mix >> 5) & 3;

                if (cpu_input == 0 && frgd_mix == 3 && vram_mask == 0 && compare_mode == 0 && (s3.accel.cmd & 0xa0) == 0xa0 &&
                    (s3.accel.frgd_mix & 0xf) == 7)
                {
                        while (true)
                        {
                                if ((s3.accel.dx & 0xfff) >= clip_l && (s3.accel.dx & 0xfff) <= clip_r &&
                                    (s3.accel.dy & 0xfff) >= clip_t && (s3.accel.dy & 0xfff) <= clip_b)
                                {
                                        src_dat = READ_SRC(s3, svga, s3.accel.src + (uint32_t)s3.accel.cx, vram_mask, rd_mask);
                                        dest_dat = READ_DST(s3, svga, s3.accel.dest + (uint32_t)s3.accel.dx);

                                        dest_dat = (src_dat & s3.accel.wrt_mask) | (dest_dat & ~s3.accel.wrt_mask);

                                        WRITE(s3, svga, s3.accel.dest + (uint32_t)s3.accel.dx, dest_dat);
                                }

                                s3.accel.cx++;
                                s3.accel.dx++;
                                s3.accel.sx--;
                                if (s3.accel.sx < 0)
                                {
                                        s3.accel.cx -= (s3.accel.maj_axis_pcnt & 0xfff) + 1;
                                        s3.accel.dx -= (s3.accel.maj_axis_pcnt & 0xfff) + 1;
                                        s3.accel.sx = s3.accel.maj_axis_pcnt & 0xfff;

                                        s3.accel.cy++;
                                        s3.accel.dy++;

                                        s3.accel.src = srcbase + (uint32_t)(s3.accel.cy * s3.width);
                                        s3.accel.dest = dstbase + (uint32_t)(s3.accel.dy * s3.width);

                                        s3.accel.sy--;

                                        if (s3.accel.sy < 0)
                                                return;
                                }
                        }
                }
                else
                {
                        while (count-- != 0 && s3.accel.sy >= 0)
                        {
                                if ((s3.accel.dx & 0xfff) >= clip_l && (s3.accel.dx & 0xfff) <= clip_r &&
                                    (s3.accel.dy & 0xfff) >= clip_t && (s3.accel.dy & 0xfff) <= clip_b)
                                {
                                        if (vram_mask != 0)
                                        {
                                                mix_dat = READ_SRC(s3, svga, s3.accel.src + (uint32_t)s3.accel.cx, vram_mask, rd_mask);

                                                mix_dat = (mix_dat != 0) ? mix_mask : 0;
                                        }
                                        switch (((mix_dat & mix_mask) != 0) ? frgd_mix : bkgd_mix)
                                        {
                                        case 0:
                                                src_dat = s3.accel.bkgd_color;
                                                break;
                                        case 1:
                                                src_dat = s3.accel.frgd_color;
                                                break;
                                        case 2:
                                                src_dat = cpu_dat;
                                                break;
                                        case 3:
                                                src_dat = READ_SRC(s3, svga, s3.accel.src + (uint32_t)s3.accel.cx, vram_mask, rd_mask);
                                                break;
                                        }

                                        if ((compare_mode == 2 && src_dat != compare) ||
                                            (compare_mode == 3 && src_dat == compare) || compare_mode < 2)
                                        {
                                                dest_dat = READ_DST(s3, svga, s3.accel.dest + (uint32_t)s3.accel.dx);

                                                //                                pclog("BitBlt : %04i, %04i (%06X) - %02X (%02X
                                                //                                %04X %05X) %02X   ", s3->accel.dx, s3->accel.dy,
                                                //                                s3->accel.dest + s3->accel.dx, src_dat,
                                                //                                vram[s3->accel.src + s3->accel.cx], mix_dat,
                                                //                                s3->accel.src + s3->accel.cx, dest_dat);

                                                dest_dat = MIX(s3, mix_dat, mix_mask, src_dat, dest_dat);

                                                        //                                pclog("%02X\n", dest_dat);

                                                        WRITE(s3, svga, s3.accel.dest + (uint32_t)s3.accel.dx, dest_dat);
                                        }
                                }

                                mix_dat <<= 1;
                                mix_dat |= 1;
                                if (s3.bpp == 0)
                                        cpu_dat >>= 8;
                                else
                                        cpu_dat >>= 16;

                                if ((s3.accel.cmd & 0x20) != 0)
                                {
                                        s3.accel.cx++;
                                        s3.accel.dx++;
                                }
                                else
                                {
                                        s3.accel.cx--;
                                        s3.accel.dx--;
                                }
                                s3.accel.sx--;
                                if (s3.accel.sx < 0)
                                {
                                        if ((s3.accel.cmd & 0x20) != 0)
                                        {
                                                s3.accel.cx -= (s3.accel.maj_axis_pcnt & 0xfff) + 1;
                                                s3.accel.dx -= (s3.accel.maj_axis_pcnt & 0xfff) + 1;
                                        }
                                        else
                                        {
                                                s3.accel.cx += (s3.accel.maj_axis_pcnt & 0xfff) + 1;
                                                s3.accel.dx += (s3.accel.maj_axis_pcnt & 0xfff) + 1;
                                        }
                                        s3.accel.sx = s3.accel.maj_axis_pcnt & 0xfff;

                                        if ((s3.accel.cmd & 0x80) != 0)
                                        {
                                                s3.accel.cy++;
                                                s3.accel.dy++;
                                        }
                                        else
                                        {
                                                s3.accel.cy--;
                                                s3.accel.dy--;
                                        }

                                        s3.accel.src = srcbase + (uint32_t)(s3.accel.cy * s3.width);
                                        s3.accel.dest = dstbase + (uint32_t)(s3.accel.dy * s3.width);

                                        s3.accel.sy--;

                                        if (cpu_input != 0 /* && (s3->accel.multifunc[0xa] & 0xc0) == 0x80*/)
                                                return;
                                        if (s3.accel.sy < 0)
                                                return;
                                }
                        }
                }
                break;

        case 7:                 /*Pattern fill - BitBlt but with source limited to 8x8*/
                if (cpu_input == 0) /*!cpu_input is trigger to start operation*/
                {
                        s3.accel.sx = s3.accel.maj_axis_pcnt & 0xfff;
                        s3.accel.sy = s3.accel.multifunc[0] & 0xfff;

                        s3.accel.dx = s3.accel.destx_distp & 0xfff;
                        if ((s3.accel.destx_distp & 0x1000) != 0)
                                s3.accel.dx |= ~0xfff;
                        s3.accel.dy = s3.accel.desty_axstp & 0xfff;
                        if ((s3.accel.desty_axstp & 0x1000) != 0)
                                s3.accel.dy |= ~0xfff;

                        s3.accel.cx = s3.accel.cur_x & 0xfff;
                        if ((s3.accel.cur_x & 0x1000) != 0)
                                s3.accel.cx |= ~0xfff;
                        s3.accel.cy = s3.accel.cur_y & 0xfff;
                        if ((s3.accel.cur_y & 0x1000) != 0)
                                s3.accel.cy |= ~0xfff;

                        /*Align source with destination*/
                        //                        s3->accel.cx = (s3->accel.cx & ~7) | (s3->accel.dx & 7);
                        //                        s3->accel.cy = (s3->accel.cy & ~7) | (s3->accel.dy & 7);

                        s3.accel.pattern = (uint32_t)((s3.accel.cy * s3.width) + s3.accel.cx);
                        s3.accel.dest = dstbase + (uint32_t)(s3.accel.dy * s3.width);

                        s3.accel.cx = s3.accel.dx & 7;
                        s3.accel.cy = s3.accel.dy & 7;

                        s3.accel.src = srcbase + s3.accel.pattern + (uint32_t)(s3.accel.cy * s3.width);

                        //                        pclog("Source %08X Dest %08X  (%i, %i) - (%i, %i)\n", s3->accel.src,
                        //                        s3->accel.dest, s3->accel.cx, s3->accel.cy, s3->accel.dx, s3->accel.dy);
                        //                        dumpregs();
                        //                        exit(-1);
                }
                if ((s3.accel.cmd & 0x100) != 0 && cpu_input == 0)
                        return; /*Wait for data from CPU*/
                //                if ((s3->accel.multifunc[0xa] & 0xc0) == 0x80 && !cpu_input) /*Mix data from CPU*/
                //                   return;

                frgd_mix = (s3.accel.frgd_mix >> 5) & 3;
                bkgd_mix = (s3.accel.bkgd_mix >> 5) & 3;

                while (count-- != 0 && s3.accel.sy >= 0)
                {
                        if ((s3.accel.dx & 0xfff) >= clip_l && (s3.accel.dx & 0xfff) <= clip_r &&
                            (s3.accel.dy & 0xfff) >= clip_t && (s3.accel.dy & 0xfff) <= clip_b)
                        {
                                if (vram_mask != 0)
                                {
                                        mix_dat = READ_SRC(s3, svga, s3.accel.src + (uint32_t)s3.accel.cx, vram_mask, rd_mask);
                                        mix_dat = (mix_dat != 0) ? mix_mask : 0;
                                }
                                switch (((mix_dat & mix_mask) != 0) ? frgd_mix : bkgd_mix)
                                {
                                case 0:
                                        src_dat = s3.accel.bkgd_color;
                                        break;
                                case 1:
                                        src_dat = s3.accel.frgd_color;
                                        break;
                                case 2:
                                        src_dat = cpu_dat;
                                        break;
                                case 3:
                                        src_dat = READ_SRC(s3, svga, s3.accel.src + (uint32_t)s3.accel.cx, vram_mask, rd_mask);
                                        break;
                                }

                                if ((compare_mode == 2 && src_dat != compare) || (compare_mode == 3 && src_dat == compare) ||
                                    compare_mode < 2)
                                {
                                        dest_dat = READ_DST(s3, svga, s3.accel.dest + (uint32_t)s3.accel.dx);

                                        //                                pclog("Pattern fill : %04i, %04i (%06X) - %02X (%02X
                                        //                                %04X %05X) %02X   ", s3->accel.dx, s3->accel.dy,
                                        //                                s3->accel.dest + s3->accel.dx, src_dat,
                                        //                                vram[s3->accel.src + s3->accel.cx], mix_dat,
                                        //                                s3->accel.src + s3->accel.cx, dest_dat);

                                        dest_dat = MIX(s3, mix_dat, mix_mask, src_dat, dest_dat);

                                                //                                pclog("%02X\n", dest_dat);

                                                WRITE(s3, svga, s3.accel.dest + (uint32_t)s3.accel.dx, dest_dat);
                                }
                        }

                        mix_dat <<= 1;
                        mix_dat |= 1;
                        if (s3.bpp == 0)
                                cpu_dat >>= 8;
                        else
                                cpu_dat >>= 16;

                        if ((s3.accel.cmd & 0x20) != 0)
                        {
                                s3.accel.cx = ((s3.accel.cx + 1) & 7) | (s3.accel.cx & ~7);
                                s3.accel.dx++;
                        }
                        else
                        {
                                s3.accel.cx = ((s3.accel.cx - 1) & 7) | (s3.accel.cx & ~7);
                                s3.accel.dx--;
                        }
                        s3.accel.sx--;
                        if (s3.accel.sx < 0)
                        {
                                if ((s3.accel.cmd & 0x20) != 0)
                                {
                                        s3.accel.cx = ((s3.accel.cx - ((s3.accel.maj_axis_pcnt & 0xfff) + 1)) & 7) |
                                                       (s3.accel.cx & ~7);
                                        s3.accel.dx -= (s3.accel.maj_axis_pcnt & 0xfff) + 1;
                                }
                                else
                                {
                                        s3.accel.cx = ((s3.accel.cx + ((s3.accel.maj_axis_pcnt & 0xfff) + 1)) & 7) |
                                                       (s3.accel.cx & ~7);
                                        s3.accel.dx += (s3.accel.maj_axis_pcnt & 0xfff) + 1;
                                }
                                s3.accel.sx = s3.accel.maj_axis_pcnt & 0xfff;

                                if ((s3.accel.cmd & 0x80) != 0)
                                {
                                        s3.accel.cy = ((s3.accel.cy + 1) & 7) | (s3.accel.cy & ~7);
                                        s3.accel.dy++;
                                }
                                else
                                {
                                        s3.accel.cy = ((s3.accel.cy - 1) & 7) | (s3.accel.cy & ~7);
                                        s3.accel.dy--;
                                }

                                s3.accel.src = srcbase + s3.accel.pattern + (uint32_t)(s3.accel.cy * s3.width);
                                s3.accel.dest = dstbase + (uint32_t)(s3.accel.dy * s3.width);

                                s3.accel.sy--;

                                if (cpu_input != 0 /* && (s3->accel.multifunc[0xa] & 0xc0) == 0x80*/)
                                        return;
                                if (s3.accel.sy < 0)
                                        return;
                        }
                }
                break;

        case 3: /*Polygon Fill Solid (Trio64 only)*/
        {
                int end_y1, end_y2;

                if (s3.chip != S3_TRIO64)
                        break;

                polygon_setup(s3);

                if ((s3.accel.cmd & 0x100) != 0 && cpu_input == 0)
                        return; /*Wait for data from CPU*/

                end_y1 = s3.accel.desty_axstp;
                end_y2 = s3.accel.desty_axstp2;

                frgd_mix = (s3.accel.frgd_mix >> 5) & 3;

                while ((s3.accel.poly_cy < end_y1) && (s3.accel.poly_cy2 < end_y2))
                {
                        int y = s3.accel.poly_cy;
                        int x_count = ABS((s3.accel.poly_cx2 >> 20) - s3.accel.poly_x) + 1;

                        s3.accel.dest = dstbase + (uint32_t)(y * s3.width);

                        while (x_count-- != 0 && count-- != 0)
                        {
                                if ((s3.accel.poly_x & 0xfff) >= clip_l && (s3.accel.poly_x & 0xfff) <= clip_r &&
                                    (s3.accel.poly_cy & 0xfff) >= clip_t && (s3.accel.poly_cy & 0xfff) <= clip_b)
                                {
                                        switch (frgd_mix)
                                        {
                                        case 0:
                                                src_dat = s3.accel.bkgd_color;
                                                break;
                                        case 1:
                                                src_dat = s3.accel.frgd_color;
                                                break;
                                        case 2:
                                                src_dat = cpu_dat;
                                                break;
                                        case 3:
                                                src_dat = 0; /*Nor supported?*/
                                                break;
                                        }

                                        if ((compare_mode == 2 && src_dat != compare) ||
                                            (compare_mode == 3 && src_dat == compare) || compare_mode < 2)
                                        {
                                                dest_dat = READ_DST(s3, svga, s3.accel.dest + (uint32_t)s3.accel.poly_x);

                                                dest_dat = MIX(s3, mix_dat, mix_mask, src_dat, dest_dat);

                                                        WRITE(s3, svga, s3.accel.dest + (uint32_t)s3.accel.poly_x, dest_dat);
                                        }
                                }
                                if (s3.bpp == 0)
                                        cpu_dat >>= 8;
                                else
                                        cpu_dat >>= 16;

                                if (s3.accel.poly_x < (s3.accel.poly_cx2 >> 20))
                                        s3.accel.poly_x++;
                                else
                                        s3.accel.poly_x--;
                        }

                        s3.accel.poly_cx += s3.accel.poly_dx1;
                        s3.accel.poly_cx2 += s3.accel.poly_dx2;
                        s3.accel.poly_x = s3.accel.poly_cx >> 20;

                        s3.accel.poly_cy++;
                        s3.accel.poly_cy2++;

                        if (count == 0)
                                break;
                }

                s3.accel.cur_x = (uint16_t)(s3.accel.poly_cx & 0xfff);
                s3.accel.cur_y = (uint16_t)(s3.accel.poly_cy & 0xfff);
                s3.accel.cur_x2 = (uint16_t)(s3.accel.poly_cx2 & 0xfff);
                s3.accel.cur_y2 = (uint16_t)(s3.accel.poly_cy & 0xfff);
        } break;

        case 11: /*Polygon Fill Pattern (Trio64 only)*/
        {
                int end_y1, end_y2;

                if (s3.chip != S3_TRIO64)
                        break;

                polygon_setup(s3);

                if ((s3.accel.cmd & 0x100) != 0 && cpu_input == 0)
                        return; /*Wait for data from CPU*/

                end_y1 = s3.accel.desty_axstp;
                end_y2 = s3.accel.desty_axstp2;

                frgd_mix = (s3.accel.frgd_mix >> 5) & 3;
                bkgd_mix = (s3.accel.bkgd_mix >> 5) & 3;

                while ((s3.accel.poly_cy < end_y1) && (s3.accel.poly_cy2 < end_y2))
                {
                        int y = s3.accel.poly_cy;
                        int x_count = ABS((s3.accel.poly_cx2 >> 20) - s3.accel.poly_x) + 1;

                        s3.accel.src = srcbase + s3.accel.pattern + (uint32_t)((y & 7) * s3.width);
                        s3.accel.dest = dstbase + (uint32_t)(y * s3.width);

                        while (x_count-- != 0 && count-- != 0)
                        {
                                int pat_x = s3.accel.poly_x & 7;

                                if ((s3.accel.poly_x & 0xfff) >= clip_l && (s3.accel.poly_x & 0xfff) <= clip_r &&
                                    (s3.accel.poly_cy & 0xfff) >= clip_t && (s3.accel.poly_cy & 0xfff) <= clip_b)
                                {
                                        if (vram_mask != 0)
                                        {
                                                mix_dat = READ_SRC(s3, svga, s3.accel.src + (uint32_t)pat_x, vram_mask, rd_mask);
                                                mix_dat = (mix_dat != 0) ? mix_mask : 0;
                                        }
                                        switch (((mix_dat & mix_mask) != 0) ? frgd_mix : bkgd_mix)
                                        {
                                        case 0:
                                                src_dat = s3.accel.bkgd_color;
                                                break;
                                        case 1:
                                                src_dat = s3.accel.frgd_color;
                                                break;
                                        case 2:
                                                src_dat = cpu_dat;
                                                break;
                                        case 3:
                                                src_dat = READ_SRC(s3, svga, s3.accel.src + (uint32_t)pat_x, vram_mask, rd_mask);
                                                break;
                                        }

                                        if ((compare_mode == 2 && src_dat != compare) ||
                                            (compare_mode == 3 && src_dat == compare) || compare_mode < 2)
                                        {
                                                dest_dat = READ_DST(s3, svga, s3.accel.dest + (uint32_t)s3.accel.poly_x);

                                                dest_dat = MIX(s3, mix_dat, mix_mask, src_dat, dest_dat);

                                                        WRITE(s3, svga, s3.accel.dest + (uint32_t)s3.accel.poly_x, dest_dat);
                                        }
                                }
                                if (s3.bpp == 0)
                                        cpu_dat >>= 8;
                                else
                                        cpu_dat >>= 16;

                                mix_dat <<= 1;
                                mix_dat |= 1;

                                if (s3.accel.poly_x < (s3.accel.poly_cx2 >> 20))
                                        s3.accel.poly_x++;
                                else
                                        s3.accel.poly_x--;
                        }

                        s3.accel.poly_cx += s3.accel.poly_dx1;
                        s3.accel.poly_cx2 += s3.accel.poly_dx2;
                        s3.accel.poly_x = s3.accel.poly_cx >> 20;

                        s3.accel.poly_cy++;
                        s3.accel.poly_cy2++;

                        if (count == 0)
                                break;
                }

                s3.accel.cur_x = (uint16_t)(s3.accel.poly_cx & 0xfff);
                s3.accel.cur_y = (uint16_t)(s3.accel.poly_cy & 0xfff);
                s3.accel.cur_x2 = (uint16_t)(s3.accel.poly_cx2 & 0xfff);
                s3.accel.cur_y2 = (uint16_t)(s3.accel.poly_cy & 0xfff);
        } break;
        }
    }

    // pcem: vid_s3.c:2639-2702
    internal static void s3_hwcursor_draw(svga_t svga, int displine)
    {
        s3_t s3 = (s3_t)svga.p;
        int x;
        Span<uint16_t> dat = stackalloc uint16_t[2];
        int xx;
        int offset = svga.hwcursor_latch.x - svga.hwcursor_latch.xoff;
        uint32_t fg = 0, bg = 0;

        switch (svga.bpp)
        {
        case 15:
                fg = video_15to32[s3.hwc_fg_col & 0xffff];
                bg = video_15to32[s3.hwc_bg_col & 0xffff];
                break;

        case 16:
                fg = video_16to32[s3.hwc_fg_col & 0xffff];
                bg = video_16to32[s3.hwc_bg_col & 0xffff];
                break;

        case 24:
        case 32:
                fg = s3.hwc_fg_col;
                bg = s3.hwc_bg_col;
                break;

        default:
                if (s3.chip == S3_TRIO32 || s3.chip == S3_TRIO64)
                {
                        fg = svga.pallook[s3.hwc_fg_col & 0xff];
                        bg = svga.pallook[s3.hwc_bg_col & 0xff];
                }
                else
                {
                        fg = svga.pallook[svga.crtc[0xe]];
                        bg = svga.pallook[svga.crtc[0xf]];
                }
                break;
        }

        if (svga.interlace != 0 && svga.hwcursor_oddeven != 0)
                svga.hwcursor_latch.addr += 16;

        //        pclog("HWcursor %i %i\n", svga->hwcursor_latch.x, svga->hwcursor_latch.y);
        for (x = 0; x < 64; x += 16)
        {
                uint32_t remapped_addr = dword_remap(svga.hwcursor_latch.addr);

                // pcem bug, not reproduced: PB-84 — DEVIATION : l'adresse du curseur n'est pas masquée
                // (CR4C/4D jusqu'à 4 Mo, plus 16 à 32 octets par ligne) ; au-delà de la VRAM, PCem
                // lit le tas, le C# lèverait. Chaque index est masqué par vram_mask (R9).
                dat[0] = (uint16_t)((svga.vram[remapped_addr & svga.vram_mask] << 8) | svga.vram[(remapped_addr + 1) & svga.vram_mask]);
                dat[1] = (uint16_t)((svga.vram[(remapped_addr + 2) & svga.vram_mask] << 8) | svga.vram[(remapped_addr + 3) & svga.vram_mask]);
                for (xx = 0; xx < 16; xx++)
                {
                        if (offset >= svga.hwcursor_latch.x)
                        {
                                if ((dat[0] & 0x8000) == 0)
                                {
                                        // pcem bug, not reproduced: PB-85 — DEVIATION : offset + 32 dépasse la ligne
                                        // (déborder sur la suivante est reproduit) ; à la dernière ligne, PCem écrit
                                        // après buffer32, dans le tas. Hors du tableau, rien n'est écrit (R9).
                                        if ((uint)(displine * Stride + offset + 32) < (uint)Buffer32.Length)
                                                Buffer32[displine * Stride + offset + 32] = ((dat[1] & 0x8000) != 0) ? fg : bg;
                                }
                                else if ((dat[1] & 0x8000) != 0)
                                {
                                        if ((uint)(displine * Stride + offset + 32) < (uint)Buffer32.Length) // PB-85
                                                Buffer32[displine * Stride + offset + 32] ^= 0xffffff;
                                }
                                //                                pclog("Plot %i, %i (%i %i) %04X %04X\n", offset, displine, x+xx,
                                //                                svga_hwcursor_on, dat[0], dat[1]);
                        }

                        offset++;
                        dat[0] <<= 1;
                        dat[1] <<= 1;
                }
                svga.hwcursor_latch.addr += 4;
        }
        if (svga.interlace != 0 && svga.hwcursor_oddeven == 0)
                svga.hwcursor_latch.addr += 16;
    }

    // pcem: vid_s3.c:2704-2727
    private static void s3_io_remove(s3_t s3)
    {
        io_removehandler(0x03c0, 0x0020, s3_in, null, null, s3_out, null, null, s3);

        io_removehandler(0x42e8, 0x0002, s3_accel_in, null, null, s3_accel_out, null, null, s3);
        io_removehandler(0x46e8, 0x0002, s3_accel_in, null, null, s3_accel_out, null, null, s3);
        io_removehandler(0x4ae8, 0x0002, s3_accel_in, null, null, s3_accel_out, null, null, s3);
        io_removehandler(0x82e8, 0x0002, s3_accel_in, null, null, s3_accel_out, null, null, s3);
        io_removehandler(0x86e8, 0x0002, s3_accel_in, null, null, s3_accel_out, null, null, s3);
        io_removehandler(0x8ae8, 0x0002, s3_accel_in, null, null, s3_accel_out, null, null, s3);
        io_removehandler(0x8ee8, 0x0002, s3_accel_in, null, null, s3_accel_out, null, null, s3);
        io_removehandler(0x92e8, 0x0002, s3_accel_in, null, null, s3_accel_out, null, null, s3);
        io_removehandler(0x96e8, 0x0002, s3_accel_in, null, null, s3_accel_out, null, null, s3);
        io_removehandler(0x9ae8, 0x0002, s3_accel_in, null, null, s3_accel_out, null, null, s3);
        io_removehandler(0x9ee8, 0x0002, s3_accel_in, null, null, s3_accel_out, null, null, s3);
        io_removehandler(0xa2e8, 0x0004, s3_accel_in, null, null, s3_accel_out, null, null, s3);
        io_removehandler(0xa6e8, 0x0004, s3_accel_in, null, null, s3_accel_out, null, null, s3);
        io_removehandler(0xaae8, 0x0004, s3_accel_in, null, null, s3_accel_out, null, null, s3);
        io_removehandler(0xaee8, 0x0004, s3_accel_in, null, null, s3_accel_out, null, null, s3);
        io_removehandler(0xb2e8, 0x0004, s3_accel_in, null, null, s3_accel_out, null, null, s3);
        io_removehandler(0xb6e8, 0x0002, s3_accel_in, null, null, s3_accel_out, null, null, s3);
        io_removehandler(0xbae8, 0x0002, s3_accel_in, null, null, s3_accel_out, null, null, s3);
        io_removehandler(0xbee8, 0x0002, s3_accel_in, null, null, s3_accel_out, null, null, s3);
        io_removehandler(0xe2e8, 0x0004, s3_accel_in, null, null, s3_accel_out, s3_accel_out_w, s3_accel_out_l, s3);
    }

    // pcem: vid_s3.c:2729-2763
    private static void s3_io_set(s3_t s3)
    {
        s3_io_remove(s3);

        io_sethandler(0x03c0, 0x0020, s3_in, null, null, s3_out, null, null, s3);

        io_sethandler(0x42e8, 0x0002, s3_accel_in, null, null, s3_accel_out, null, null, s3);
        io_sethandler(0x46e8, 0x0002, s3_accel_in, null, null, s3_accel_out, null, null, s3);
        io_sethandler(0x4ae8, 0x0002, s3_accel_in, null, null, s3_accel_out, null, null, s3);
        if (s3.chip == S3_TRIO64)
        {
                io_sethandler(0x82e8, 0x0004, s3_accel_in, null, null, s3_accel_out, null, null, s3);
                io_sethandler(0x86e8, 0x0004, s3_accel_in, null, null, s3_accel_out, null, null, s3);
                io_sethandler(0x8ae8, 0x0004, s3_accel_in, null, null, s3_accel_out, null, null, s3);
                io_sethandler(0x8ee8, 0x0004, s3_accel_in, null, null, s3_accel_out, null, null, s3);
                io_sethandler(0x92e8, 0x0004, s3_accel_in, null, null, s3_accel_out, null, null, s3);
                io_sethandler(0x96e8, 0x0004, s3_accel_in, null, null, s3_accel_out, null, null, s3);
        }
        else
        {
                io_sethandler(0x82e8, 0x0002, s3_accel_in, null, null, s3_accel_out, null, null, s3);
                io_sethandler(0x86e8, 0x0002, s3_accel_in, null, null, s3_accel_out, null, null, s3);
                io_sethandler(0x8ae8, 0x0002, s3_accel_in, null, null, s3_accel_out, null, null, s3);
                io_sethandler(0x8ee8, 0x0002, s3_accel_in, null, null, s3_accel_out, null, null, s3);
                io_sethandler(0x92e8, 0x0002, s3_accel_in, null, null, s3_accel_out, null, null, s3);
                io_sethandler(0x96e8, 0x0002, s3_accel_in, null, null, s3_accel_out, null, null, s3);
        }
        io_sethandler(0x9ae8, 0x0002, s3_accel_in, null, null, s3_accel_out, null, null, s3);
        io_sethandler(0x9ee8, 0x0002, s3_accel_in, null, null, s3_accel_out, null, null, s3);
        io_sethandler(0xa2e8, 0x0004, s3_accel_in, null, null, s3_accel_out, null, null, s3);
        io_sethandler(0xa6e8, 0x0004, s3_accel_in, null, null, s3_accel_out, null, null, s3);
        io_sethandler(0xaae8, 0x0004, s3_accel_in, null, null, s3_accel_out, null, null, s3);
        io_sethandler(0xaee8, 0x0004, s3_accel_in, null, null, s3_accel_out, null, null, s3);
        io_sethandler(0xb2e8, 0x0004, s3_accel_in, null, null, s3_accel_out, null, null, s3);
        io_sethandler(0xb6e8, 0x0002, s3_accel_in, null, null, s3_accel_out, null, null, s3);
        io_sethandler(0xbae8, 0x0002, s3_accel_in, null, null, s3_accel_out, null, null, s3);
        io_sethandler(0xbee8, 0x0002, s3_accel_in, null, null, s3_accel_out, null, null, s3);
        io_sethandler(0xe2e8, 0x0004, s3_accel_in, null, null, s3_accel_out, s3_accel_out_w, s3_accel_out_l, s3);
    }

    // pcem: vid_s3.c:2765-2820
    // Gardée telle quelle : sans PCI (pci_add rend -1), personne ne l'appelle.
    internal static uint8_t s3_pci_read(int func, int addr, object p)
    {
        s3_t s3 = (s3_t)p;
        svga_t svga = s3.svga;
        //        pclog("S3 PCI read %08X\n", addr);
        switch (addr)
        {
        case 0x00:
                return 0x33; /*'S3'*/
        case 0x01:
                return 0x53;

        case 0x02:
                return s3.id_ext_pci;
        case 0x03:
                return 0x88;

        case PCI_REG_COMMAND:
                return s3.pci_regs[PCI_REG_COMMAND]; /*Respond to IO and memory accesses*/

        case 0x07:
                return 1 << 1; /*Medium DEVSEL timing*/

        case 0x08:
                return 0; /*Revision ID*/
        case 0x09:
                return 0; /*Programming interface*/

        case 0x0a:
                return 0x00; /*Supports VGA interface*/
        case 0x0b:
                return 0x03;

        case 0x10:
                return 0x00; /*Linear frame buffer address*/
        case 0x11:
                return 0x00;
        case 0x12:
                return (uint8_t)(svga.crtc[0x5a] & 0x80);
        case 0x13:
                return svga.crtc[0x59];

        case 0x30:
                return (uint8_t)(s3.pci_regs[0x30] & 0x01); /*BIOS ROM address*/
        case 0x31:
                return 0x00;
        case 0x32:
                return s3.pci_regs[0x32];
        case 0x33:
                return s3.pci_regs[0x33];

        case 0x3c:
                return s3.int_line;
        case 0x3d:
                return PCI_INTA;
        }
        return 0;
    }

    // pcem: vid_s3.c:2822-2863
    // Gardée telle quelle : sans PCI (pci_add rend -1), personne ne l'appelle.
    internal static void s3_pci_write(int func, int addr, uint8_t val, object p)
    {
        s3_t s3 = (s3_t)p;
        svga_t svga = s3.svga;
        //        pclog("s3_pci_write: addr=%02x val=%02x\n", addr, val);
        switch (addr)
        {
        case PCI_REG_COMMAND:
                s3.pci_regs[PCI_REG_COMMAND] = (uint8_t)(val & 0x23);
                if ((val & PCI_COMMAND_IO) != 0)
                        s3_io_set(s3);
                else
                        s3_io_remove(s3);
                s3_updatemapping(s3);
                break;

        case 0x12:
                svga.crtc[0x5a] = (uint8_t)(val & 0x80);
                s3_updatemapping(s3);
                break;
        case 0x13:
                svga.crtc[0x59] = val;
                s3_updatemapping(s3);
                break;

        case 0x30:
        case 0x32:
        case 0x33:
                s3.pci_regs[addr] = val;
                if ((s3.pci_regs[0x30] & 0x01) != 0)
                {
                        // DEVIATION: le C nomme cette locale `addr`, masquant le paramètre ; C# interdit
                        //   le masquage (CS0136), d'où rom_addr.
                        uint32_t rom_addr = (uint32_t)((s3.pci_regs[0x32] << 16) | (s3.pci_regs[0x33] << 24));
                        //                        pclog("S3 bios_rom enabled at %08x\n", addr);
                        mem_mapping_set_addr(s3.bios_rom.mapping, rom_addr, 0x8000);
                }
                else
                {
                        //                        pclog("S3 bios_rom disabled\n");
                        mem_mapping_disable(s3.bios_rom.mapping);
                }
                return;

        case 0x3c:
                s3.int_line = val;
                return;
        }
    }

    // pcem: vid_s3.c:2865-2871
    private static readonly int[] vram_sizes =
    [
        7,         /*512 kB*/
        6,         /*1 MB*/
        4,         /*2 MB*/
        0, 0,      /*4 MB*/
        0, 0, 0, 3 /*8 MB*/
    ];

    // pcem: vid_s3.c:2873-2953
    private static object s3_init(string bios_fn, int chip)
    {
        s3_t s3 = new s3_t();
        svga_t svga = s3.svga;
        int vram;
        uint32_t vram_size;

        // omitted: malloc + memset(s3, 0, sizeof(s3_t)) (vid_s3.c:2874, :2879) — `new` zéro-initialise.

        vram = device_get_config_int("memory");
        if (vram != 0)
                vram_size = (uint32_t)(vram << 20);
        else
                vram_size = 512 << 10;
        s3.vram_mask = vram_size - 1;

        rom_init(s3.bios_rom, bios_fn, 0xc0000, 0x8000, 0x7fff, 0, MEM_MAPPING_EXTERNAL);
        if (PCI != 0)
                mem_mapping_disable(s3.bios_rom.mapping);

        mem_mapping_add(s3.linear_mapping, 0, 0, svga_read_linear, svga_readw_linear, svga_readl_linear, svga_write_linear,
                        svga_writew_linear, svga_writel_linear, null, 0, MEM_MAPPING_EXTERNAL, s3.svga);
        mem_mapping_add(s3.mmio_mapping, 0xa0000, 0x10000, s3_accel_read, null, null, s3_accel_write, s3_accel_write_w,
                        s3_accel_write_l, null, 0, MEM_MAPPING_EXTERNAL, s3);
        mem_mapping_disable(s3.mmio_mapping);

        svga_init(s3.svga, s3, (int)vram_size, /*4mb - 864 supports 8mb but buggy VESA driver reports 0mb*/
                  s3_recalctimings, s3_in, s3_out, s3_hwcursor_draw, null);

        svga.decode_mask = (4 << 20) - 1;
        switch (vram)
        {
        case 0: /*512kb*/
                svga.vram_mask = (1 << 19) - 1;
                svga.vram_max = 2 << 20;
                break;
        case 1: /*1MB*/
                /*VRAM in first MB, mirrored in 2nd MB, 3rd and 4th MBs are open bus*/
                /*This works with the #9 9FX BIOS, and matches how my real Trio64 behaves,
                  but does not work with the Phoenix EDO BIOS. Possibly an FPM/EDO difference?*/
                svga.vram_mask = (1 << 20) - 1;
                svga.vram_max = 2 << 20;
                break;
        case 2:
        default: /*2MB*/
                /*VRAM in first 2 MB, 3rd and 4th MBs are open bus*/
                svga.vram_mask = (2 << 20) - 1;
                svga.vram_max = 2 << 20;
                break;
        case 4: /*4MB*/
                svga.vram_mask = (4 << 20) - 1;
                svga.vram_max = 4 << 20;
                break;
        }

        if (PCI != 0)
                svga.crtc[0x36] = (uint8_t)(2 | (3 << 2) | (1 << 4) | (vram_sizes[vram] << 5));
        else
                svga.crtc[0x36] = (uint8_t)(1 | (3 << 2) | (1 << 4) | (vram_sizes[vram] << 5));
        svga.crtc[0x37] = 1 | (7 << 5);

        svga.vblank_start = s3_vblank_start;

        s3_io_set(s3);

        s3.card = pci_add(s3_pci_read, s3_pci_write, s3);

        s3.pci_regs[0x04] = 7;

        s3.pci_regs[0x30] = 0x00;
        s3.pci_regs[0x32] = 0x0c;
        s3.pci_regs[0x33] = 0x00;

        s3.chip = chip;

        // DEVIATION (décision utilisateur du 01/10, G7 n° 3): accélérateur synchrone —
        //   thread_create_event() x2 et thread_create(fifo_thread, s3) (vid_s3.c:2946-2948)
        //   disparaissent : la FIFO est drainée par s3_fifo_drain sur le fil appelant.

        s3.int_line = 0;

        return s3;
    }

    // omitted: s3_bahamas64_init (vid_s3.c:2955-2967) — G7 décision n° 2 : la Trio64 Phoenix seule
    // omitted: s3_bahamas64_available (vid_s3.c:2969) — G7 décision n° 2 : la Trio64 Phoenix seule
    // omitted: s3_9fx_init (vid_s3.c:2971-2982) — G7 décision n° 2 : la Trio64 Phoenix seule
    // omitted: s3_9fx_available (vid_s3.c:2984) — G7 décision n° 2 : la Trio64 Phoenix seule
    // omitted: s3_phoenix_trio32_init (vid_s3.c:2986-2998) — G7 décision n° 2 : la Trio64 Phoenix seule
    // omitted: s3_phoenix_trio32_available (vid_s3.c:3000) — G7 décision n° 2 : la Trio64 Phoenix seule

    // pcem: vid_s3.c:3002-3015
    private static object s3_phoenix_trio64_init()
    {
        s3_t s3 = (s3_t)s3_init("86c764x1.bin", S3_TRIO64);

        s3.id = 0xe1; /*Trio64*/
        s3.id_ext = s3.id_ext_pci = 0x11;
        s3.packed_mmio = 1;
        if (device_get_config_int("memory") == 1)
                s3.svga.vram_max = 1 << 20; /*Phoenix BIOS does not expect VRAM to be mirrored*/

        s3.getclock = s3_trio64_getclock;
        s3.getclock_p = s3;

        return s3;
    }

    // pcem: vid_s3.c:3017
    private static int s3_phoenix_trio64_available() { return rom_present("86c764x1.bin"); }

    // pcem: vid_s3.c:3019-3029
    internal static void s3_close(object p)
    {
        s3_t s3 = (s3_t)p;

        svga_close(s3.svga);

        // DEVIATION (décision utilisateur du 01/10, G7 n° 3): accélérateur synchrone —
        //   thread_kill(s3->fifo_thread), thread_destroy_event(s3->wake_fifo_thread) et
        //   thread_destroy_event(s3->fifo_not_full_event) (vid_s3.c:3024-3026) disparaissent :
        //   ni fil ni événement.

        // omitted: free(s3) (vid_s3.c:3028) — libération manuelle, sans objet sous GC.
    }

    // pcem: vid_s3.c:3031-3035
    internal static void s3_speed_changed(object p)
    {
        s3_t s3 = (s3_t)p;

        svga_recalctimings(s3.svga);
    }

    // pcem: vid_s3.c:3037-3041
    internal static void s3_force_redraw(object p)
    {
        s3_t s3 = (s3_t)p;

        s3.svga.fullchange = changeframecount;
    }

    // pcem: vid_s3.c:3043-3059
    internal static void s3_add_status_info(StringBuilder s, int max_len, object p)
    {
        s3_t s3 = (s3_t)p;

        // omitted: timer_read(), status_diff et le sprintf/strncat des deux pourcentages CPU
        //   (vid_s3.c:3045-3051, :3054-3056) — timer_read/timer_freq (l'horloge hôte) ne sont pas
        //   transcrits, et blitter_time reste à 0 sans le fil FIFO (voir s3_fifo_drain) : la
        //   ligne afficherait 0 %. Fenêtre d'état de l'hôte seulement, ni invité ni oracle.

        svga_add_status_info(s, max_len, s3.svga);

        s3.blitter_time = 0;
    }

    // omitted: s3_bahamas64_config (vid_s3.c:3061-3072) — G7 décision n° 2 : la Trio64 Phoenix seule
    // omitted: s3_9fx_config (vid_s3.c:3074-3082) — G7 décision n° 2 : la Trio64 Phoenix seule
    // omitted: s3_phoenix_trio32_config (vid_s3.c:3084-3092) — G7 décision n° 2 : la Trio64 Phoenix seule

    // pcem: vid_s3.c:3094-3102
    // omitted: `.description` (vid_s3.c:3096) ; `.selection` (:3097-3100) est transcrit (G8.3) :
    //   sans section [Phoenix S3 Trio64] dans le .cfg, le défaut, 2 Mo.
    internal static device_config_t[] s3_phoenix_trio64_config =
    [
        new device_config_t { name = "memory", type = CONFIG_SELECTION, default_int = 2,
            selection = [new() { description = "1 MB", value = 1 }, new() { description = "2 MB", value = 2 }, new() { description = "4 MB", value = 4 }] },
        new device_config_t { type = -1 },
    ];

    // omitted: s3_bahamas64_device (vid_s3.c:3104-3112) — G7 décision n° 2 : la Trio64 Phoenix seule
    // omitted: s3_9fx_device (vid_s3.c:3114-3122) — G7 décision n° 2 : la Trio64 Phoenix seule
    // omitted: s3_phoenix_trio32_device (vid_s3.c:3124-3132) — G7 décision n° 2 : la Trio64 Phoenix seule

    // pcem: vid_s3.c:3134-3142
    internal static device_t s3_phoenix_trio64_device = new device_t("Phoenix S3 Trio64", 0, s3_phoenix_trio64_init, s3_close,
                                                                     s3_phoenix_trio64_available, s3_speed_changed,
                                                                     s3_force_redraw, s3_add_status_info,
                                                                     s3_phoenix_trio64_config);
}
