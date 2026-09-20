// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — outillage de vérification.
//
// OÙ PASSE LE TEMPS ÉMULÉ.
//
// runpc() demande cpu_get_speed()/100 = 47 727 cycles par tranche de 10 ms, et le
// TSC devrait donc avancer de 14 318 184/100 = 143 181,84 par tranche, puisque
// xt_cpu_multi vaut EXACTEMENT 3 (4 772 728 x 3 = 14 318 184). Mesuré sur
// l'amorçage : 140 365. Soit 1,97 % de moins, et de façon PROPORTIONNELLE.
//
// Deux maillons peuvent en être responsables, et il faut les séparer :
//   (a) execx86 ne consomme pas les 47 727 cycles demandés ;
//   (b) clockhardware n'en convertit pas la totalité en TSC.
//
// Ce fichier compte les deux, séparément, tranche par tranche.

using iXtal26;
using iXtal26.Cpu;

namespace iXtal26.Diff;

public static class SpeedCheck
{
    public static int Run(string romsPath, int slices)
    {
        _808x.ResetDiagState();
        if (!pc.initpc(romsPath))
            return 1;

        var budget = pc.cpu_get_speed() / 100;
        long consumed = 0;
        ulong tsc0 = timer.tsc;

        Console.WriteLine($"budget par tranche : {budget} cycles  ·  xt_cpu_multi = " +
                          $"{_808x.xt_cpu_multi / (double)(1UL << 32):0.######}");

        for (var s = 0; s < slices; s++)
        {
            // runpc() fait « cycles += budget » puis boucle tant que cycles > 0.
            // Le solde AVANT plus le budget, moins le solde APRÈS, est exactement
            // ce que le cœur a consommé — report négatif de la tranche précédente
            // compris, ce qui est précisément ce qu'on veut compter.
            var before = x86.cycles;
            pc.runpc();
            consumed += before + budget - x86.cycles;
        }

        var tsc = timer.tsc - tsc0;
        var attendu = (long)slices * budget;

        Console.WriteLine($"\ntranches            : {slices}");
        Console.WriteLine($"cycles demandés     : {attendu,15:N0}");
        Console.WriteLine($"cycles consommés    : {consumed,15:N0}   " +
                          $"({100.0 * consumed / attendu:0.000} % du budget)");
        Console.WriteLine($"TSC avancé          : {tsc,15:N0}");
        Console.WriteLine($"TSC / consommés     : {tsc / (double)consumed,15:0.000000}   (attendu 3,000000)");
        Console.WriteLine($"TSC attendu         : {attendu * 3,15:N0}");
        Console.WriteLine($"secondes émulées    : {tsc / 14318184.0,15:0.000} s pour " +
                          $"{slices / 100.0:0.000} s de budget");

        Console.WriteLine();
        if (Math.Abs(tsc / (double)consumed - 3.0) > 1e-6)
            Console.WriteLine("-> (b) : clockhardware ne convertit pas tous les cycles consommés.");
        else if (Math.Abs(100.0 * consumed / attendu - 100.0) > 0.01)
            Console.WriteLine("-> (a) : execx86 ne consomme pas le budget demandé. La conversion est juste.");
        else
            Console.WriteLine("-> les deux maillons sont exacts.");

        PerOpcode(slices);
        return 0;
    }

    /// <summary>Où, exactement, les cycles cessent d'être convertis. On avance
    /// instruction par instruction et on compare ce que le cœur a CONSOMMÉ à ce que
    /// clockhardware a CONVERTI, en imputant l'écart à l'opcode responsable.</summary>
    private static void PerOpcode(int slices)
    {
        const int N = 400000;
        var perdu = new long[256];
        var vus = new long[256];
        long consomme = 0, converti = 0;
        // xt_cpu_multi vaut exactement 3 << 32 ; on travaille donc en unités de 2^32.

        Console.WriteLine($"\n--- imputation par opcode, {N} instructions ---");

        var st = Diag.HState.Create();

        for (var i = 0; i < N; i++)
        {
            var lin = (x86.cs + Cpu._386_common.cpu_state.pc) & 0xFFFFF;
            var op = Memory.mem.mem_readb_phys((uint)lin);

            _808x.GetState(ref st);
            var t0 = ((UInt128)st.tsc << 32) + st.tsc_frac;

            var c = _808x.Step();

            _808x.GetState(ref st);
            var t1 = ((UInt128)st.tsc << 32) + st.tsc_frac;

            // tsc et tsc_frac forment un 32:32 : il faut les DEUX, sinon on confond
            // « rien converti » et « converti moins d'une unité ».
            var dTsc = t1 - t0;
            var attendu = (UInt128)(ulong)c * _808x.xt_cpu_multi;

            consomme += c;
            converti += (long)(dTsc / _808x.xt_cpu_multi);
            vus[op]++;
            if (dTsc < attendu)
                perdu[op] += (long)((attendu - dTsc) / _808x.xt_cpu_multi);
        }

        Console.WriteLine($"consommés {consomme:N0}, convertis {converti:N0} " +
                          $"({100.0 * converti / consomme:0.00} %)");

        var idx = new int[256];
        for (var i = 0; i < 256; i++) idx[i] = i;
        Array.Sort(idx, (a2, b2) => perdu[b2].CompareTo(perdu[a2]));

        Console.WriteLine("opcode   exécutions      cycles perdus   perdus/exéc");
        for (var k = 0; k < 8 && perdu[idx[k]] > 0; k++)
        {
            var o = idx[k];
            Console.WriteLine($"  0x{o:X2}   {vus[o],12:N0}   {perdu[o],14:N0}   " +
                              $"{(vus[o] == 0 ? 0 : perdu[o] / (double)vus[o]),11:0.00}");
        }
    }
}
