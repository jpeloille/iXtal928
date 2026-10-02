// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// opl-tables-check (G8.0) — LES TABLES DE DBOPL, des deux côtés.
//
// InitTables (dbopl.cpp:1311-1464) calcule MulTable et WaveTable par pow() et sin() de la libm
// de l'HÔTE : glibc côté oracle, .NET côté C#. Un ulp d'écart changerait un échantillon de
// l'OPL, que le hachage du son verrait sans qu'aucune instruction ne diverge. On compare donc
// les tables elles-mêmes, entrée par entrée, avant d'écrire une ligne de l'OPL (PLAN-G8.md,
// décision n° 3 : mesurer d'abord).

using System.Runtime.InteropServices;
using iXtal26.Sound;

namespace iXtal26.Diff;

internal static class OplTablesCheck
{
    [DllImport(Oracle.Lib)]
    private static extern int h_opl_tables(ushort[] mul, short[] wave, byte[] ksl, byte[] trem);

    internal static int Run()
    {
        Oracle.CheckAbi();

        var mul = new ushort[384];
        var wave = new short[8 * 512];
        var ksl = new byte[8 * 16];
        var trem = new byte[DBOPL.TREMOLO_TABLE];
        var n = h_opl_tables(mul, wave, ksl, trem);
        if (n != DBOPL.TREMOLO_TABLE)
        {
            Console.WriteLine($"TREMOLO_TABLE : oracle {n}, C# {DBOPL.TREMOLO_TABLE}.");
            return 1;
        }
        DBOPL.InitTables();

        var bad = 0;
        bad += Cmp("MulTable", mul.Select(x => (long)x).ToArray(), DBOPL.MulTable.Select(x => (long)x).ToArray());
        bad += Cmp("WaveTable", wave.Select(x => (long)x).ToArray(), DBOPL.WaveTable.Select(x => (long)x).ToArray());
        bad += Cmp("KslTable", ksl.Select(x => (long)x).ToArray(), DBOPL.KslTable.Select(x => (long)x).ToArray());
        bad += Cmp("TremoloTable", trem.Select(x => (long)x).ToArray(), DBOPL.TremoloTable.Select(x => (long)x).ToArray());

        Console.WriteLine(bad == 0
            ? $"\nVert : les quatre tables de DBOPL identiques ({mul.Length + wave.Length + ksl.Length + trem.Length} entrées) — pow et sin de .NET rendent ici ce que rend glibc."
            : $"\nROUGE : {bad} entrée(s) divergente(s).");
        return bad == 0 ? 0 : 1;
    }

    private static int Cmp(string name, long[] o, long[] c)
    {
        var bad = 0;
        for (var i = 0; i < o.Length; i++)
        {
            if (o[i] == c[i])
                continue;
            if (bad < 10)
                Console.WriteLine($"  {name}[{i}] : oracle {o[i]}, C# {c[i]}");
            bad++;
        }
        Console.WriteLine($"  {name,-13} {o.Length,5} entrées : " + (bad == 0 ? "identiques" : $"{bad} divergente(s)"));
        return bad;
    }
}
