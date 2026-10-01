// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — outillage de diff.
//
// M15 — L'ORACLE FAIT-IL TOURNER UNE VGA ?
//
// Même ordre que la sonde AT de B2 : tant que l'oracle ne sait pas amorcer une
// machine à carte VGA, les trois fichiers de PCem qui la portent (vid_vga.c,
// vid_svga.c, vid_svga_render.c) s'écriraient côté C# sans rien à quoi les comparer.
// La sonde amorce l'ORACLE SEUL, sur la machine demandée, et rapporte ce que la carte
// a vu : ses registres, sa VRAM, l'écran texte qu'elle porte.
//
// L'écran se lit dans la VRAM BRUTE, pas par svga_read : en mode texte la VGA range
// le caractère au plan 0 et l'attribut au plan 1, adresse CPU décalée de deux
// (branche chain2_write de svga_write, vid_svga.c:834-839) — la cellule k est donc à
// vram[8k] et vram[8k + 1], à partir de l'adresse de début du CRTC.

using System.Runtime.InteropServices;
using iXtal26.Cpu;

namespace iXtal26.Diff;

public static class VgaProbe
{
    /// <summary>Les champs de h_vga_probe, dans l'ordre de harness.c. Le côté C# les
    /// rend dans le même ordre (Video.vid_svga.Probe).</summary>
    public static readonly string[] Fields =
    [
        "present", "#vram", "#changedvram", "#buffer32", "#crtc", "#seqregs", "#gdcreg",
        "#attrregs", "#vgapal", "#pallook", "#egapal", "miscout", "crtcreg", "seqaddr",
        "gdcaddr", "attraddr", "attrff", "attr_palette_enable", "dac_mask", "dac_status",
        "dac_read", "dac_write", "dac_pos", "la|lb|lc|ld", "writemode", "readmode",
        "readplane", "chain4", "chain2_write", "chain2_read", "writemask", "plane_mask",
        "charseta", "charsetb", "banked_mask", "mapping.base", "mapping.size", "cgastat",
        "vc", "sc", "displine", "linepos", "dispon", "ma", "maback", "ca", "vtotal",
        "dispend", "hdisp", "htotal", "dispontime", "dispofftime", "timer.ts_integer",
        "timer.ts_frac", "fullchange", "frames", "firstline", "lastline", "xsize", "ysize",
        "video_res_x", "video_res_y", "video_bpp", "blink",
        // M19 — l'état que la Trident rend atteignable, puis la tvga_t et son RAMDAC.
        "read_bank", "write_bank", "bpp", "vram_display_mask", "vram_mask", "rowoffset",
        "ma_latch", "interlace", "lowres", "hdisp_time", "clock(bits)",
        "tvga.id", "tvga.oldmode", "tvga.3d8", "tvga.3d9", "tvga.oldctrl1", "tvga.oldctrl2",
        "tvga.newctrl2", "tvga.vram_size", "tvga.vram_mask", "ramdac.state", "ramdac.ctrl",
        // G7.1 — la gd5429_t, puis le curseur matériel.
        "gd5429.type", "gd5429.bank[0]", "gd5429.bank[1]", "gd5429.mask", "gd5429.vram_mask",
        "gd5429.hidden_dac_reg", "gd5429.dac_3c6_count", "gd5429.lfb_base",
        "gd5429.mmio_vram_overlap", "gd5429.sr10_read", "gd5429.sr11_read", "gd5429.latch_ext",
        "hwcursor.ena", "hwcursor.x", "hwcursor.y", "hwcursor.addr",
        // G7.3 — la s3_t (h_s3_probe).
        "s3.chip", "s3.id|id_ext|id_ext_pci", "s3.bank", "s3.ma_ext", "s3.width", "s3.bpp",
        "s3.linear_base", "s3.linear_size", "s3.subsys_cntl|stat|accel.subsys_cntl|advfunc",
        "accel.cmd|short_stroke|multifunc_cntl", "accel.cur_x|cur_y|cur_x2|cur_y2",
        "accel.frgd|bkgd_color", "accel.wrt|rd_mask", "accel.mixes|color_cmp", "accel.cx|cy",
        "accel.sx|sy", "accel.dx|dy", "fifo_write|read_idx", "blitter_busy|force_busy",
        "hwc_fg|bg_col",
    ];

    // M19 — la carte, par son internal_name de PCem (video.c:177-191). Indépendant du
    // registre C# : la sonde amorce l'oracle SEUL, avant que le C# connaisse la carte.
    private static int Card(string name) => name switch
    {
        "vga" => Oracle.GFX_VGA,
        "tvga8900d" => Oracle.GFX_TVGA,
        "tvga9000b" => Oracle.GFX_TVGA9000B,
        "cl_gd5429" => Oracle.GFX_CL_GD5429,
        "px_trio64" => Oracle.GFX_PHOENIX_TRIO64,
        _ => -1,
    };

    public static int Run(string romsPath, int slices, string model, string? fda, string card = "vga")
    {
        Oracle.CheckAbi();
        if (Fields.Length != Oracle.VgaProbeN)
        {
            Console.Error.WriteLine($"Sonde VGA : {Fields.Length} noms pour {Oracle.VgaProbeN} champs.");
            return 2;
        }
        if (!pc.setmodel(model))
            return 2;
        var gfx = Card(card);
        if (gfx < 0)
        {
            Console.Error.WriteLine($"--gfxcard « {card} » : vga, tvga8900d ou tvga9000b.");
            return 2;
        }

        var core = Oracle.CoreForModel(Models.model_c.models[Models.model_c.model]);
        Console.WriteLine($"Sonde VGA — ORACLE SEUL : machine {model}, carte {card}, {pc.cfg_mem_size} Ko, " +
                          $"{slices} tranches" + (fda is null ? "" : $", A: = {fda}") + "\n");

        Oracle.h_set_discfn(0, fda ?? "");
        Oracle.h_set_discfn(1, "");
        Oracle.h_set_mem_size(pc.cfg_mem_size);
        Oracle.h_set_drive_type(0, pc.cfg_drive_type[0]);
        Oracle.h_set_drive_type(1, pc.cfg_drive_type[1]);
        Oracle.h_set_romset(pc.romset);
        Oracle.h_set_cpu(Cpu.cpu_c.cpu_manufacturer, Cpu.cpu_c.cpu);
        Oracle.h_set_fpu(Cpu.cpu_c.fpu_type);
        Oracle.h_set_core(core);
        Oracle.h_set_gfxcard(gfx);
        if (Oracle.h_boot(romsPath) == 0)
        {
            Console.Error.WriteLine($"h_boot a échoué depuis « {romsPath} » (ROM de la carte présente ?).");
            return 1;
        }
        // M19 — la temporisation de la carte, telle que video_updatetiming l'a posée.
        var fp = CpuFingerprint.Oracle_();
        for (var k = 0; k < CpuFingerprint.Names.Length; k++)
            if (CpuFingerprint.Names[k].StartsWith("video_timing", StringComparison.Ordinal))
                Console.WriteLine($"  {CpuFingerprint.Names[k],-22} {(long)fp[k]}");
        for (var i = 0; i < slices; i++)
        {
            Oracle.h_runpc();
            Oracle.h_kbd_process();
        }
        Oracle.h_closepc();

        var o = new ulong[Oracle.VgaProbeN];
        Oracle.h_vga_probe(o);
        if (o[0] == 0)
        {
            Console.WriteLine("ROUGE : aucune carte svga montée — svga_get_pri() rend NULL.");
            return 1;
        }
        for (var f = 0; f < Fields.Length; f++)
            Console.WriteLine($"  {Fields[f],-20} {o[f],22}   0x{o[f]:X}");

        var vram = new byte[256 * 1024];
        Marshal.Copy(Oracle.h_vga_vram(), vram, 0, vram.Length);
        var bda = new byte[0x100];
        Oracle.h_read_phys(0x400, bda, (uint)bda.Length);
        // Le vecteur d'INT 10h en fin de course : en C000:xxxx, la ROM de la carte a
        // tourné — une ROM jamais appelée ne peut pas s'y inscrire.
        var ivt = new byte[4];
        Oracle.h_read_phys(0x40, ivt, 4);
        Console.WriteLine($"\n  INT 10h -> {ivt[3]:X2}{ivt[2]:X2}:{ivt[1]:X2}{ivt[0]:X2}");
        PrintText(vram, bda, "oracle");
        return 0;
    }

    /// <summary>L'écran texte porté par une VRAM de VGA : cellule k en vram[8k]
    /// (caractère, plan 0) et vram[8k + 1] (attribut, plan 1), à partir de l'adresse
    /// de début du CRTC. Seules les lignes non vides sont imprimées.
    ///
    /// Le CRTC n'est pas lu ici — la sonde n'expose que son hachage — donc l'origine
    /// et la largeur viennent de la zone de données du BIOS, que le BIOS VGA tient
    /// à jour : 0040:004A colonnes, 0040:004E décalage de page en octets.</summary>
    public static void PrintText(byte[] vram, byte[] bda, string side)
    {
        var cols = bda[0x4A] | (bda[0x4B] << 8);
        var pageOff = bda[0x4E] | (bda[0x4F] << 8);
        if (cols is not (40 or 80))
            cols = 80;

        Console.WriteLine($"\n  Écran {side} ({cols} colonnes, page en +0x{pageOff:X}, lignes non vides) :");
        for (var row = 0; row < 25; row++)
        {
            var line = new char[cols];
            for (var col = 0; col < cols; col++)
            {
                var cpu = (pageOff + 2 * (row * cols + col)) & 0x7fff;
                var ch = vram[(cpu & ~1) << 2];
                line[col] = ch is >= 0x20 and < 0x7f ? (char)ch : ' ';
            }
            var s = new string(line).TrimEnd();
            if (s.Length > 0)
                Console.WriteLine($"  {row,2}| {s}");
        }
    }
}
