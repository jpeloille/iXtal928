// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// L'EMPREINTE CPU (M16) — pendant de h_cpu_fingerprint() (tools/oracle/harness_stubs.c).
//
// Tout ce que cpu_set() et setpitclock() posent et que le temps de l'invité lit, pris
// juste après l'amorçage des deux côtés et confronté champ par champ. Elle existe parce
// qu'une asymétrie de CONFIGURATION ne se voit pas forcément dans la trace : sur un AT, le
// boot-diff diverge bien avant son contrôle de longueur final, et un budget de tranche à
// 60 000 d'un côté et 47 727 de l'autre y donnerait le même rapport.
//
// L'ORDRE EST LE CONTRAT : il doit suivre h_cpu_fingerprint() à l'identique.

using iXtal26.Cpu;
using iXtal26.Memory;
using iXtal26.Models;

namespace iXtal26.Diff;

internal static class CpuFingerprint
{
    internal static readonly string[] Names =
    [
        "cpu_get_speed", "budget de tranche", "cpu_busspeed", "isa_cycles", "cpu_16bitbus",
        "is8086", "is386", "is486", "hasfpu", "cpu_iscyrix",
        "cpu_prefetch_width", "cpu_mem_prefetch_cycles", "cpu_rom_prefetch_cycles",
        "cpu_cycles_read", "cpu_cycles_read_l", "cpu_cycles_write", "cpu_cycles_write_l",
        "timing_misaligned", "FNV des 28 timing_*", "cpuclock (bits)", "PITCONST", "CGACONST",
        "RTCCONST", "TIMER_USEC", "xt_cpu_multi", "isa_timing (bits)", "bus_timing (bits)",
        "video_timing_read_b", "video_timing_read_w", "video_timing_read_l",
        "video_timing_write_b", "video_timing_write_w", "video_timing_write_l",
        "mem_size", "cpu", "cpu_manufacturer", "rspeed de cpu_s",
        "fpu_type", "FNV des 72 champs de x87_timings",
        "CPUID", "cpu_features", "cpu_CR4_mask", "cpu_multi", "has_vlb",
    ];

    /// <summary>Le vecteur côté C#, dans l'ordre de h_cpu_fingerprint().</summary>
    internal static ulong[] Csharp()
    {
        int[] timings =
        [
            cpu_c.timing_rr, cpu_c.timing_rm, cpu_c.timing_mr, cpu_c.timing_mm, cpu_c.timing_rml,
            cpu_c.timing_mrl, cpu_c.timing_mml, cpu_c.timing_bt, cpu_c.timing_bnt, cpu_c.timing_int,
            cpu_c.timing_int_rm, cpu_c.timing_int_v86, cpu_c.timing_int_pm, cpu_c.timing_int_pm_outer,
            cpu_c.timing_iret_rm, cpu_c.timing_iret_v86, cpu_c.timing_iret_pm, cpu_c.timing_iret_pm_outer,
            cpu_c.timing_call_rm, cpu_c.timing_call_pm, cpu_c.timing_call_pm_gate,
            cpu_c.timing_call_pm_gate_inner, cpu_c.timing_retf_rm, cpu_c.timing_retf_pm,
            cpu_c.timing_retf_pm_outer, cpu_c.timing_jmp_rm, cpu_c.timing_jmp_pm, cpu_c.timing_jmp_pm_gate,
        ];

        var hash = 1469598103934665603UL;
        foreach (var t in timings)
        {
            hash ^= (uint)t;
            hash = unchecked(hash * 1099511628211UL);
        }

        static ulong I(int v) => unchecked((ulong)(long)v);

        var o = new ulong[Oracle.CpuFpN];
        var i = 0;
        o[i++] = I(cpu_c.cpu_get_speed());
        o[i++] = I(cpu_c.cpu_get_speed() / 100);
        o[i++] = I(cpu_c.cpu_busspeed);
        o[i++] = I(cpu_c.isa_cycles);
        o[i++] = I(x86.cpu_16bitbus);
        o[i++] = I(_808x.is8086);
        o[i++] = I(x86.is386);
        o[i++] = I(x86.is486);
        o[i++] = I(cpu_c.hasfpu);
        o[i++] = I(cpu_c.cpu_iscyrix);
        o[i++] = I(cpu_c.cpu_prefetch_width);
        o[i++] = I(cpu_c.cpu_mem_prefetch_cycles);
        o[i++] = I(cpu_c.cpu_rom_prefetch_cycles);
        o[i++] = I(cpu_c.cpu_cycles_read);
        o[i++] = I(cpu_c.cpu_cycles_read_l);
        o[i++] = I(cpu_c.cpu_cycles_write);
        o[i++] = I(cpu_c.cpu_cycles_write_l);
        o[i++] = I(cpu_c.timing_misaligned);
        o[i++] = hash;
        o[i++] = BitConverter.SingleToUInt32Bits(pit.cpuclock);
        o[i++] = pit.PITCONST;
        o[i++] = pit.CGACONST;
        o[i++] = pit.RTCCONST;
        o[i++] = timer.TIMER_USEC;
        o[i++] = _808x.xt_cpu_multi;
        o[i++] = BitConverter.SingleToUInt32Bits(pit.isa_timing);
        o[i++] = BitConverter.SingleToUInt32Bits(pit.bus_timing);
        o[i++] = I(Video.video.video_timing_read_b);
        o[i++] = I(Video.video.video_timing_read_w);
        o[i++] = I(Video.video.video_timing_read_l);
        o[i++] = I(Video.video.video_timing_write_b);
        o[i++] = I(Video.video.video_timing_write_w);
        o[i++] = I(Video.video.video_timing_write_l);
        o[i++] = I(mem.mem_size);
        o[i++] = I(cpu_c.cpu);
        o[i++] = I(cpu_c.cpu_manufacturer);
        o[i++] = I(cpu_c.cpu_s?.rspeed ?? 0);
        // G4.1 — le coprocesseur et sa table de temps, comme h_cpu_fingerprint : les 72 int
        // du struct (Sequential) dans leur ordre, en FNV.
        o[i++] = I(cpu_c.fpu_type);
        var x87 = System.Runtime.InteropServices.MemoryMarshal.Cast<x87_timings_t, int>(
            System.Runtime.InteropServices.MemoryMarshal.CreateReadOnlySpan(ref x87_timings_c.x87_timings, 1));
        var hx = 1469598103934665603UL;
        foreach (var v in x87)
        {
            hx ^= (uint)v;
            hx = unchecked(hx * 1099511628211UL);
        }
        o[i++] = hx;
        // G6.1 — les cinq champs du 486, comme h_cpu_fingerprint.
        o[i++] = I(x86.CPUID);
        o[i++] = cpu_c.cpu_features;
        o[i++] = x86.cpu_CR4_mask;
        o[i++] = I(cpu_c.cpu_multi);
        o[i++] = I(cpu_c.has_vlb);
        return o;
    }

    /// <summary>Le vecteur côté oracle, à prendre juste après h_boot.</summary>
    internal static ulong[] Oracle_()
    {
        var o = new ulong[Oracle.CpuFpN];
        Oracle.h_cpu_fingerprint(o);
        return o;
    }

    /// <summary>Compare, imprime chaque champ divergent par son nom, et rend le nombre
    /// d'écarts. Les places au-delà des champs nommés doivent valoir zéro des deux
    /// côtés : une place non nulle dit qu'un côté a gagné un champ que l'autre n'a pas.</summary>
    internal static int Compare(ulong[] oracle, ulong[] csharp)
    {
        var n = 0;
        for (var k = 0; k < Oracle.CpuFpN; k++)
        {
            if (oracle[k] == csharp[k])
                continue;
            var name = k < Names.Length ? Names[k] : $"place {k} (non nommée)";
            Console.WriteLine($"  CONFIGURATION CPU DIVERGENTE : {name} — oracle {Show(oracle[k])}, C# {Show(csharp[k])}");
            n++;
        }

        return n;
    }

    /// <summary>Une ligne lisible pour un vecteur : les champs qui disent la machine.</summary>
    internal static string Summary(ulong[] v) =>
        $"vitesse {(long)v[0]}, budget {(long)v[1]}, busspeed {(long)v[2]}, isa {(long)v[3]}, " +
        $"16 bits {(long)v[4]}, largeur {(long)v[10]}, mem {(long)v[11]}, rom {(long)v[12]}, " +
        $"lecture {(long)v[13]}/{(long)v[14]}, écriture {(long)v[15]}/{(long)v[16]}, " +
        $"horloge {BitConverter.UInt32BitsToSingle((uint)v[19]):F0}, " +
        $"vidéo {(long)v[27]}/{(long)v[28]}/{(long)v[29]}, mem_size {(long)v[33]}";

    private static string Show(ulong v) => v > 0xFFFFFFFFUL ? $"0x{v:X16}" : $"{(long)v}";
}
