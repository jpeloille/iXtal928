// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// emu8k-tables-check (G12.2) — LES TABLES DE L'EMU8000, des deux côtés.
//
// emu8k_init (sound_emu8k.c:2066-2225) calcule ses tables par exp2, sqrt, log10, log2, exp, log, sin et pow de la
// libm de l'HÔTE : glibc côté oracle, .NET côté C# (qui l'appelle ; exp2 n'y existe pas, Math.Pow(2, x) en tient
// lieu). GCC plie sqrt(1.09018) et log(0.5) à la compilation ; le C# les calcule à l'exécution. Un ulp d'écart
// changerait les échantillons de l'AWE32 sans qu'aucune instruction ne diverge : on compare donc les tables entrée
// par entrée, au bit près (PLAN-G12.md, décision n° 13), et l'empreinte de la ROM chargée, après la correction
// AWE-DUMP (:2026-2029). Des deux côtés, le vrai emu8k_init, sur un EMU8000 de brouillon sans RAM.

using System.Runtime.InteropServices;
using iXtal26.Sound;

namespace iXtal26.Diff;

internal static class EmuTablesCheck
{
    internal static int Run(string romsPath)
    {
        Oracle.CheckAbi();

        var freq = new long[65536];
        var atten = new int[256];
        var voldb = new int[65537];
        var ampdb = new int[65537];
        var hzoct = new int[65537];
        var attack = new int[128];
        var lfo = new int[65536];
        var lfospeed = new long[256];
        var chor = new double[65536];
        var filt = new int[16 * 256 * 3];
        var cubic = new float[4096];
        if (Oracle.h_emu8k_tables(romsPath, freq, atten, voldb, ampdb, hzoct, attack, lfo, lfospeed, chor, filt, cubic, out var romO) == 0)
        {
            Console.WriteLine("ROUGE : roms/awe32.raw absente — rien n'est comparé.");
            return 1;
        }

        iXtal26.PluginApi.paths.set_roms_paths(romsPath);
        var e = new emu8k_t();
        sound_emu8k.emu8k_init(e, 0x620, 0);
        ulong romC = 1469598103934665603UL;
        foreach (var b in MemoryMarshal.AsBytes(e.mem.AsSpan(0, sound_emu8k.ROM_MOTS)))
        {
            romC ^= b;
            romC *= 1099511628211UL;
        }

        var bad = 0;
        bad += Cmp("freqtable", freq.Select(x => x).ToArray(), sound_emu8k.freqtable);
        bad += Cmp("attentable", atten.Select(x => (long)x).ToArray(), sound_emu8k.attentable.Select(x => (long)x).ToArray());
        bad += Cmp("env_vol_db_to_vol_target", voldb.Select(x => (long)x).ToArray(), sound_emu8k.env_vol_db_to_vol_target.Select(x => (long)x).ToArray());
        bad += Cmp("env_vol_amplitude_to_db", ampdb.Select(x => (long)x).ToArray(), sound_emu8k.env_vol_amplitude_to_db.Select(x => (long)x).ToArray());
        bad += Cmp("env_mod_hertz_to_octave", hzoct.Select(x => (long)x).ToArray(), sound_emu8k.env_mod_hertz_to_octave.Select(x => (long)x).ToArray());
        bad += Cmp("env_attack_to_samples", attack.Select(x => (long)x).ToArray(), sound_emu8k.env_attack_to_samples.Select(x => (long)x).ToArray());
        bad += Cmp("lfotable", lfo.Select(x => (long)x).ToArray(), sound_emu8k.lfotable.Select(x => (long)x).ToArray());
        bad += Cmp("lfofreqtospeed", lfospeed, sound_emu8k.lfofreqtospeed);
        bad += Cmp("chortable", chor.Select(BitConverter.DoubleToInt64Bits).ToArray(), sound_emu8k.chortable.Select(BitConverter.DoubleToInt64Bits).ToArray());
        bad += Cmp("filt_coeffs", filt.Select(x => (long)x).ToArray(), sound_emu8k.filt_coeffs.Select(x => (long)x).ToArray());
        bad += Cmp("cubic_table", cubic.Select(x => (long)BitConverter.SingleToInt32Bits(x)).ToArray(),
                   sound_emu8k.cubic_table.Select(x => (long)BitConverter.SingleToInt32Bits(x)).ToArray());
        if (romO != romC)
        {
            Console.WriteLine($"  la ROM chargée : oracle {romO:X16}, C# {romC:X16}");
            bad++;
        }
        var n = freq.Length + atten.Length + voldb.Length + ampdb.Length + hzoct.Length + attack.Length + lfo.Length +
                lfospeed.Length + chor.Length + filt.Length + cubic.Length;
        Console.WriteLine(bad == 0
            ? $"Vert : les onze tables de l'EMU8000 identiques au bit près ({n} entrées), la ROM chargée identique ({romO:X16}) " +
              "— exp2, pow, log10, log2, exp, log, sin et sqrt de .NET rendent ici ce que rend glibc."
            : $"ROUGE : {bad} entrée(s) divergente(s).");
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
        return bad;
    }
}
