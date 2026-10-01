// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// svga-linear-check — le banc dirigé de G7.0 (PLAN-G7.md).
//
// La fenêtre LINÉAIRE du socle SVGA (svga_write_linear, svga_read_linear et leurs formes
// 16 et 32 bits, vid_svga.c:1137-1658) n'a encore aucune carte qui l'installe : aucun
// amorçage ne l'atteint. On l'appelle donc directement, des deux côtés, sur une VGA amorcée à
// l'identique : des accès linéaires tirés au hasard, entremêlés d'écritures aux registres qui
// en changent le sens — GDC 0-8 (mode d'écriture, fonction, masque de bits, set/reset) et
// séquenceur 2 et 4 (masque de plans, chain4). À chaque lecture la valeur rendue est
// comparée ; toutes les 1 000 opérations, la sonde VGA entière (VRAM, verrous, registres).

using System.Runtime.InteropServices;
using iXtal26.Video;

namespace iXtal26.Diff;

internal static class SvgaLinearCheck
{
    [DllImport(Oracle.Lib)] private static extern IntPtr svga_get_pri();
    [DllImport(Oracle.Lib)] private static extern void svga_write_linear(uint addr, byte val, IntPtr p);
    [DllImport(Oracle.Lib)] private static extern void svga_writew_linear(uint addr, ushort val, IntPtr p);
    [DllImport(Oracle.Lib)] private static extern void svga_writel_linear(uint addr, uint val, IntPtr p);
    [DllImport(Oracle.Lib)] private static extern byte svga_read_linear(uint addr, IntPtr p);
    [DllImport(Oracle.Lib)] private static extern ushort svga_readw_linear(uint addr, IntPtr p);
    [DllImport(Oracle.Lib)] private static extern uint svga_readl_linear(uint addr, IntPtr p);
    [DllImport(Oracle.Lib)] private static extern void outb(ushort port, byte val);

    internal static int Run(string romsPath, int iterations, ulong seed)
    {
        Oracle.CheckAbi();
        if (!pc.setmodel("ibmpc") || !pc.setgfxcard("vga"))
            return 2;
        const int slices = 3000;
        Console.WriteLine($"svga-linear-check — VGA sur le 5150, {slices} tranches, puis {iterations} opérations, graine {seed}");

        Oracle.h_set_discfn(0, "");
        Oracle.h_set_discfn(1, "");
        Oracle.h_set_mem_size(pc.cfg_mem_size);
        Oracle.h_set_drive_type(0, pc.cfg_drive_type[0]);
        Oracle.h_set_drive_type(1, pc.cfg_drive_type[1]);
        Oracle.h_set_romset(pc.romset);
        Oracle.h_set_cpu(Cpu.cpu_c.cpu_manufacturer, Cpu.cpu_c.cpu);
        Oracle.h_set_fpu(Cpu.cpu_c.fpu_type);
        Oracle.h_set_core(Oracle.CoreForModel(Models.model_c.models[Models.model_c.model]));
        Oracle.h_set_gfxcard(pc.gfxcard);
        Oracle.h_set_hdd_controller("");
        if (Oracle.h_boot(romsPath) == 0)
            return 1;
        // LE PAS COMMUN DU BOOT-DIFF (--lockstep) : l'oracle trace — h_runpc change de
        // mécanique quand la trace est ouverte —, le C# avance instruction par instruction
        // sur le même budget. Une tranche de pc.runpc() côté C# n'est PAS le pendant d'une
        // tranche de h_runpc : mesuré, la sonde VGA divergeait sur `vc` dès l'amorçage.
        var trace = Path.Combine(Path.GetTempPath(), $"ixtal-svga-linear-{Environment.ProcessId}.bin");
        if (Oracle.h_trace_open(trace) == 0)
            return 1;
        Cpu._808x.ResetDiagState();
        Cpu._386.prefetch_reset();
        Cpu._386.ClearSegResidue();
        Floppy.fdd_c.discfns[0] = "";
        Floppy.fdd_c.discfns[1] = "";
        if (!pc.initpc(romsPath))
            return 1;
        for (var s = 0; s < slices; s++)
        {
            Oracle.h_runpc();
            Oracle.h_kbd_process();
            var budget = Cpu.cpu_c.cpu_get_speed() / 100;
            while (budget > 0)
                budget -= BootDiff.PasCsharpTrace();
            Keyboard.keyboard.keyboard_poll_host();
            Keyboard.keyboard.keyboard_process();
        }
        Oracle.h_trace_close();
        File.Delete(trace);

        var po = svga_get_pri();
        var pcs = vid_svga.svga_get_pri();
        if (po == IntPtr.Zero || pcs is null)
        {
            Console.Error.WriteLine("Pas de carte svga montée.");
            return 1;
        }

        if (Probe("après amorçage") != 0)
            return 1;

        var st = seed == 0 ? 0x9E3779B97F4A7C15UL : seed;
        uint Next() { st ^= st << 13; st ^= st >> 7; st ^= st << 17; return (uint)(st >> 32); }
        var counts = new int[8];

        for (var it = 0; it < iterations; it++)
        {
            var r = Next() % 100;
            var addr = Next() & 0x7FFFF;
            var v = Next();
            string? diff = null;
            if (r < 10)
            {
                // GDC : index 0-8, valeur tirée.
                var idx = (byte)(Next() % 9);
                outb(0x3CE, idx); outb(0x3CF, (byte)v);
                io.outb(0x3CE, idx); io.outb(0x3CF, (byte)v);
                counts[6]++;
            }
            else if (r < 15)
            {
                // Séquenceur : 2 (masque de plans) ou 4 (mode mémoire, chain4).
                var idx = (byte)((Next() & 1) != 0 ? 2 : 4);
                outb(0x3C4, idx); outb(0x3C5, (byte)v);
                io.outb(0x3C4, idx); io.outb(0x3C5, (byte)v);
                counts[7]++;
            }
            else
            {
                switch (r % 6)
                {
                case 0: svga_write_linear(addr, (byte)v, po); vid_svga.svga_write_linear(addr, (byte)v, pcs); break;
                case 1: svga_writew_linear(addr, (ushort)v, po); vid_svga.svga_writew_linear(addr, (ushort)v, pcs); break;
                case 2: svga_writel_linear(addr, v, po); vid_svga.svga_writel_linear(addr, v, pcs); break;
                case 3:
                {
                    var a = svga_read_linear(addr, po); var b = vid_svga.svga_read_linear(addr, pcs);
                    if (a != b) diff = $"svga_read_linear({addr:X5}) : oracle {a:X2}, C# {b:X2}";
                    break;
                }
                case 4:
                {
                    var a = svga_readw_linear(addr, po); var b = vid_svga.svga_readw_linear(addr, pcs);
                    if (a != b) diff = $"svga_readw_linear({addr:X5}) : oracle {a:X4}, C# {b:X4}";
                    break;
                }
                default:
                {
                    var a = svga_readl_linear(addr, po); var b = vid_svga.svga_readl_linear(addr, pcs);
                    if (a != b) diff = $"svga_readl_linear({addr:X5}) : oracle {a:X8}, C# {b:X8}";
                    break;
                }
                }
                counts[r % 6]++;
            }
            if (diff is not null)
            {
                Console.WriteLine($"DIVERGENCE opération {it} : {diff}");
                return 1;
            }
            if ((it + 1) % 1000 == 0 && Probe($"opération {it + 1}") != 0)
                return 1;
        }

        Console.WriteLine($"Vert : {iterations} opérations, zéro divergence — écritures b/w/l {counts[0]}/{counts[1]}/{counts[2]}, " +
                          $"lectures b/w/l {counts[3]}/{counts[4]}/{counts[5]}, GDC {counts[6]}, séquenceur {counts[7]}.");
        return 0;
    }

    private static int Probe(string quand)
    {
        var o = new ulong[Oracle.VgaProbeN];
        var c = new ulong[Oracle.VgaProbeN];
        Oracle.h_vga_probe(o);
        vid_svga.Probe(c);
        for (var f = 0; f < VgaProbe.Fields.Length; f++)
            if (o[f] != c[f])
            {
                Console.WriteLine($"DIVERGENCE {quand} : sonde VGA, {VgaProbe.Fields[f]} — oracle {o[f]}, C# {c[f]}");
                return 1;
            }
        return 0;
    }
}
