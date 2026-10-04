// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — outillage de vérification.
//
// trace-hash-check [N] — l'accélération du 4 octobre (VERIFICATION.md, § L'accélération des
// portes). Le hachage de la trace d'amorçage est désormais PLIÉ, des deux côtés (h_trace_hash dans
// harness.c, BootDiff.TraceHashOf) ; l'ancien, octet par octet, reste la référence
// (h_trace_hash_ref, BootDiff.TraceHashRef). Sur N états tirés au sort (défaut dix millions), un
// sur deux sans tsc, et un sur seize aux valeurs extrêmes (0 et tous les bits à 1), les QUATRE
// valeurs doivent être égales : la référence et le pliage, en C et en C#. Les constantes P^5 et P^7
// sont vérifiées d'abord. Une seule différence suffit à rougir la porte.

using iXtal26.Cpu;

namespace iXtal26.Diff;

public static class TraceHashCheck
{
    public static int Run(long n)
    {
        // Les puissances de P, recalculées à l'exécution : les constantes C# et celles de harness.c
        // (H_FNV_P5, H_FNV_P7, écrites en clair) doivent leur être égales.
        ulong p = 1, p5 = 0;
        for (var k = 1; k <= 7; k++)
        {
            p = unchecked(p * BootDiff.FnvP);
            if (k == 5)
                p5 = p;
        }
        if (p5 != BootDiff.FnvP5 || p != BootDiff.FnvP7 || p5 != 913917546033277539UL || p != 14218562807570617051UL)
        {
            Console.WriteLine($"ROUGE : P^5 = {p5}, P^7 = {p} ; constantes {BootDiff.FnvP5} et {BootDiff.FnvP7}.");
            return 1;
        }

        var regs = new x86reg[8];
        var regs16 = new ushort[8];
        ulong s = 0x9E3779B97F4A7C15UL;
        ulong Next()
        {
            s ^= s << 13;
            s ^= s >> 7;
            s ^= s << 17;
            return s;
        }

        long ecarts = 0;
        for (long i = 0; i < n; i++)
        {
            var extreme = (i & 15) == 15;
            var tout = (i & 32) != 0;
            ushort W() => extreme ? (tout ? (ushort)0xFFFF : (ushort)0) : (ushort)Next();
            var cs = W();
            var pc = extreme ? (tout ? 0xFFFFFFFFu : 0u) : (uint)Next();
            for (var k = 0; k < 8; k++)
            {
                regs16[k] = W();
                regs[k].w = regs16[k];
            }
            var ds = W();
            var es = W();
            var ss = W();
            var flags = W();
            var tsc = extreme ? (tout ? ulong.MaxValue : 0UL) : Next();
            var sansTsc = (i & 1) != 0;

            var refCs = BootDiff.TraceHashRef(cs, pc, regs, ds, es, ss, flags, tsc, sansTsc);
            var pliCs = BootDiff.TraceHashOf(cs, pc, regs, ds, es, ss, flags, tsc, sansTsc);
            var refC = Oracle.h_trace_hash_value(1, cs, pc, regs16, ds, es, ss, flags, tsc, sansTsc ? 1 : 0);
            var pliC = Oracle.h_trace_hash_value(0, cs, pc, regs16, ds, es, ss, flags, tsc, sansTsc ? 1 : 0);
            if (refCs == pliCs && refCs == refC && refCs == pliC)
                continue;
            if (ecarts++ < 5)
                Console.WriteLine($"  ÉCART, état {i} : référence C# {refCs:X16}, plié C# {pliCs:X16}, " +
                                  $"référence C {refC:X16}, plié C {pliC:X16}");
        }

        Console.WriteLine(ecarts == 0
            ? $"Vert : {n} états, le hachage plié rend la référence au bit près, en C et en C#, avec et sans tsc."
            : $"ROUGE : {ecarts} état(s) sur {n} où les quatre hachages diffèrent.");
        return ecarts == 0 ? 0 : 1;
    }
}
