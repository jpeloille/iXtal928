// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: -
// STATUS: host — outillage, pas de code PCem transcrit.
//
// G2, D0.6 — le compteur « N / 1 024 » de chaque commit du cœur 386.
//
// Il lit la table VIVANTE, pas les sources : un emplacement est posé quand son délégué
// n'est plus opNonTranscrit. C'est le pendant C# du gdb qui comptait ops_286[] dans la
// .so au jalon 286 — et la seule façon de savoir ce que le C# exécute vraiment, là où
// tools/ops386-table.py ne dit que ce que PCem attend.

using iXtal26.Cpu;

namespace iXtal26.Diff;

internal static class OpsCount
{
    internal static int Run(bool listMissing)
    {
        Console.WriteLine("Emplacements posés (délégué autre que opNonTranscrit), par quadrant op32 :");
        Console.WriteLine("  0 = o16/a16, 1 = o32/a16, 2 = o16/a32, 3 = o32/a32\n");

        var total = 0;
        foreach (var (name, table) in new[] { ("ops_386", _386.ops_386), ("ops_386_0f", _386.ops_386_0f) })
        {
            var perQuad = new int[4];
            var missing = new List<int>();
            for (var i = 0; i < table.Length; i++)
            {
                if (table[i].Method.Name == "opNonTranscrit")
                    missing.Add(i);
                else
                    perQuad[i >> 8]++;
            }

            var posed = perQuad.Sum();
            total += posed;
            Console.WriteLine($"  {name,-11} {posed,4} / 1024   [{string.Join(" ", perQuad.Select(n => $"{n,3}"))}]");
            if (listMissing && missing.Count > 0)
                Console.WriteLine($"    manquent : {string.Join(" ", missing.Select(i => $"{i:X3}"))}");
        }

        Console.WriteLine($"\n  total {total} / 2048");
        return 0;
    }
}
