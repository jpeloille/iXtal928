// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — outillage de vérification.
//
// VITESSE HÔTE — le cœur C# contre l'oracle C, sur la MÊME trajectoire.
//
// speed-check mesure le temps ÉMULÉ (ce que le TSC avance par tranche). Ce banc
// mesure le temps HÔTE : combien de millisecondes de machine réelle coûte une
// seconde de 5150. Aucun chiffre de ce genre n'existait dans le dépôt.
//
// Ce qui rend le ratio C#/C honnête :
//   - la paire chronométrée est SYMÉTRIQUE : Oracle.h_run(budget) contre
//     _808x.Run(budget), soit « cycles = 0 ; execx86(budget) » des deux côtés.
//     PAS h_runpc / pc.runpc : l'un remet le report de cycles à zéro, l'autre
//     sonde le clavier de l'hôte — deux boucles différentes, et un ratio qui ne
//     comparerait plus les cœurs ;
//   - même amorçage que le diff de boot (BootDiff.cs), donc même trajectoire —
//     et on le VÉRIFIE en fin de banc : Δins, Σcycles, vecteur d'état et
//     hachage de RAM doivent concorder, sinon pas de ratio ;
//   - ordre alterné d'une répétition à l'autre, chauffe puis RÉ-amorçage avant
//     la mesure, ΔJIT et ΔGC relevés PENDANT le chronométrage : un JIT survenu
//     dans la fenêtre se voit, au lieu de se fondre dans la médiane.
//
// La boucle chronométrée est la même méthode pour les deux côtés (Measure) : ce
// qui diffère est derrière quatre délégués, appelés une fois par tranche de
// 10 ms — soit un appel indirect pour ~48 000 cycles émulés, invisible.

using System.Diagnostics;
using System.Reflection;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using iXtal26.Cpu;
using iXtal26.Diag;
using iXtal26.Memory;

namespace iXtal26.Diff;

public static class Bench
{
    /// <summary>Ce qu'un côté doit savoir faire pour être chronométré.</summary>
    private sealed class SideOps
    {
        public required string Name { get; init; }
        public required Func<bool> Boot { get; init; }
        public required Func<int> Slice { get; init; }   // rend les cycles consommés
        public required Func<HState> State { get; init; }
        public required Func<ulong> Ram { get; init; }
    }

    /// <summary>Une mesure : un côté, une répétition.</summary>
    private sealed record Sample(string Side, int Rep, int Slices, double Ms, long DIns, long Cycles,
                                 long DJit, int DGc, long DReadMembl);

    /// <summary>Ce que la mesure laisse derrière elle : de quoi prouver que les
    /// deux côtés ont parcouru la même trajectoire.</summary>
    private sealed record Trace(HState State, int LastCycles, long Cycles, long DIns, ulong Ram);

    public static int Run(string romsPath, int slices, int repeat, int warmup, bool sideC, bool sideCs)
    {
        // Un chiffre pris en Debug ne mesure que l'absence d'optimiseur.
        var dbg = typeof(pc).Assembly.GetCustomAttribute<DebuggableAttribute>();
        if (dbg is { IsJITOptimizerDisabled: true })
        {
            Console.Error.WriteLine("build Debug : chiffre sans valeur. Reconstruire avec `dotnet build -c Release`.");
            return 2;
        }

        if (slices <= 0 || repeat <= 0 || warmup < 0 || !(sideC || sideCs))
        {
            Console.Error.WriteLine("bench : TRANCHES et --repeat doivent être > 0, --warmup >= 0.");
            return 2;
        }

        // Le banc est un banc de 8088 PAR CONSTRUCTION, et son budget un littéral : le
        // pendant de BUDGET dans tools/oracle/bench.c. Lire cpu_get_speed() ici, AVANT
        // tout amorçage, rendrait la vitesse de la machine précédente — ou rien.
        var budget = 4772728 / 100;
        Header(budget, slices, repeat, warmup);

        // AggressiveOptimization sur ce que le banc appelle dans la fenêtre
        // chronométrée (les délégués, et Measure) : compilé optimisé d'emblée,
        // sans paliers ni OSR. Le ΔJIT relevé n'impute alors que le cœur — pas
        // le tiering du banc lui-même, qui sinon se déclenche en pleine mesure.
        var c = new SideOps
        {
            Name = "C",
            Boot = () => Oracle.h_boot(romsPath) != 0,
            Slice = [MethodImpl(MethodImplOptions.AggressiveOptimization)] () => Oracle.h_run(budget),
            State = () => { Oracle.h_getstate(out var s); return s; },
            Ram = Oracle.h_ram_hash,
        };
        var cs = new SideOps
        {
            Name = "C#",
            Boot = () => { _808x.ResetDiagState(); return pc.initpc(romsPath); },
            Slice = [MethodImpl(MethodImplOptions.AggressiveOptimization)] () => _808x.Run(budget),
            State = () => { var s = HState.Create(); _808x.GetState(ref s); return s; },
            Ram = _808x.RamHash,
        };

        if (sideC)
            Oracle.CheckAbi();

        var samples = new List<Sample>();
        var traces = new Dictionary<string, List<Trace>>();
        var shortWarmup = false;

        Console.WriteLine();
        Console.WriteLine($"{"côté",-4} {"rép",3} {"ms",10} {"ns/instr",9} {"M instr/s",10} " +
                          $"{"× temps réel",13} {"readmembl/instr",16} {"ΔJIT",5} {"ΔGC",4}");

        for (var rep = 1; rep <= repeat; rep++)
        {
            // Ordre alterné : C puis C#, puis C# puis C… Un côté qui profiterait
            // du cache ou de la fréquence laissés par l'autre y perd son avantage.
            var order = rep % 2 == 1 ? new[] { c, cs } : new[] { cs, c };
            foreach (var side in order)
            {
                if (side == c && !sideC || side == cs && !sideCs)
                    continue;

                var m = Measure(side, rep, slices, warmup);
                if (m is null)
                {
                    Console.Error.WriteLine($"Amorçage impossible côté {side.Name} depuis « {romsPath} ».");
                    return 1;
                }

                var (s, t) = m.Value;
                samples.Add(s);
                if (!traces.TryGetValue(side.Name, out var list))
                    traces[side.Name] = list = [];
                list.Add(t);

                var warn = s.DJit > 0 ? "   <- chauffe trop courte" : "";
                shortWarmup |= s.DJit > 0;
                Console.WriteLine(Row(s) + warn);
            }
        }

        Console.WriteLine();
        Summary(samples, sideC && sideCs);

        // Mesuré avec DOTNET_JitDisasmSummary=1 : dans la fenêtre C#, ce ne sont pas
        // des méthodes nouvelles mais des PROMOTIONS de palier (Tier0 -> instrumenté
        // -> Tier1) de méthodes rarement appelées — pit_write, dma_read,
        // pit_read_timer… — qui franchissent le seuil de 30 appels au fil des
        // ré-amorçages, plus les promotions en arrière-plan des méthodes R2R du
        // chemin d'amorçage (FileStream, Path). Dans la fenêtre C, seul ce fil
        // d'arrière-plan tourne : le code mesuré, lui, n'est pas JITé.
        if (shortWarmup)
            Console.WriteLine("\nAVERTISSEMENT : du JIT a eu lieu PENDANT la mesure (ΔJIT > 0) : chauffe trop courte. " +
                              "Côté C#, ce sont des promotions de palier de méthodes rarement appelées ; côté C, le fil " +
                              "d'arrière-plan du JIT. Allonger --warmup, ou figer les paliers : DOTNET_TieredPGO=0, " +
                              "ou DOTNET_TieredCompilation=0 (tout compilé optimisé au premier appel, sans PGO).");

        return Trajectory(traces, sideC && sideCs);
    }

    private static void Header(int budget, int slices, int repeat, int warmup)
    {
        var argv = Environment.GetCommandLineArgs();
        var dll = Path.GetRelativePath(Environment.CurrentDirectory, argv[0]);
        Console.WriteLine($"épinglage recommandé : taskset -c 0-3 dotnet {dll} {string.Join(" ", argv.Skip(1))}");
        Console.WriteLine($"runtime              : {RuntimeInformation.FrameworkDescription}  " +
                          $"({RuntimeInformation.RuntimeIdentifier}, {RuntimeInformation.ProcessArchitecture})  ·  " +
                          $"affinité effective {Affinity()}  ·  GC {(GCSettings.IsServerGC ? "serveur" : "station")}");
        Console.WriteLine($"paire mesurée        : Oracle.h_run({budget}) / _808x.Run({budget})  ·  " +
                          $"{slices} tranches = {slices / 100.0:0.00} s émulées  ·  " +
                          $"{repeat} répétition(s), chauffe {warmup} tranches, ré-amorçage avant mesure");
    }

    /// <summary>Le masque d'affinité effectif : c'est lui qui dit si taskset a
    /// été appliqué, pas l'en-tête qui le recommande.</summary>
    private static string Affinity()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsWindows())
            return "inconnue";
        var mask = (ulong)Process.GetCurrentProcess().ProcessorAffinity;
        return $"0x{mask:X} ({System.Numerics.BitOperations.PopCount(mask)} cœurs)";
    }

    /// <summary>Amorce, chauffe, RÉ-amorce, mesure. La mesure part donc du reset
    /// et non de la fin de la chauffe : même trajectoire à chaque répétition,
    /// et la même que celle du diff d'amorçage.</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static (Sample, Trace)? Measure(SideOps side, int rep, int slices, int warmup)
    {
        if (!side.Boot())
            return null;
        for (var i = 0; i < warmup; i++)
            side.Slice();
        if (!side.Boot())
            return null;

        var before = side.State();

        // On part le tas propre : un ΔGC pendant la mesure est alors imputable
        // au cœur, pas à ce que l'amorçage a laissé traîner.
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var jit0 = JitInfo.GetCompiledMethodCount();
        var gc0 = GC.CollectionCount(0);
        long cycles = 0;
        var last = 0;

        var t0 = Stopwatch.GetTimestamp();
        for (var i = 0; i < slices; i++)
        {
            last = side.Slice();
            cycles += last;
        }
        var t1 = Stopwatch.GetTimestamp();

        var dJit = JitInfo.GetCompiledMethodCount() - jit0;
        var dGc = GC.CollectionCount(0) - gc0;

        var after = side.State();
        var dIns = (long)(after.ins - before.ins);
        var dRead = (long)(after.n_readmembl - before.n_readmembl);
        var ms = Stopwatch.GetElapsedTime(t0, t1).TotalMilliseconds;

        return (new Sample(side.Name, rep, slices, ms, dIns, cycles, dJit, dGc, dRead),
                new Trace(after, last, cycles, dIns, side.Ram()));
    }

    // Le hachage de RAM est _808x.RamHash(), borné à mem_size Ko comme h_ram_hash()
    // depuis M5.1 : c'est ce banc qui a révélé que les deux lisaient 1 Mo sur une
    // machine amorcée à 640 Ko.

    // × temps réel = (N/100) s émulées / t. Sur N, pas sur Σcycles : h_run et
    // Run remettent `cycles = 0` à chaque tranche, le report négatif de la
    // dernière instruction est perdu et Σ(cycs − cycles) dépasse N × budget.
    private static string Row(Sample s)
    {
        var ns = s.DIns == 0 ? double.NaN : s.Ms * 1e6 / s.DIns;
        var mips = s.DIns / (s.Ms * 1e3);
        var realtime = (s.Slices / 100.0) / (s.Ms / 1e3);
        var rd = s.DIns == 0 ? double.NaN : s.DReadMembl / (double)s.DIns;
        return $"{s.Side,-4} {s.Rep,3} {s.Ms,10:0.0} {ns,9:0.00} {mips,10:0.00} " +
               $"{realtime,13:0.00} {rd,16:0.00} {s.DJit,5} {s.DGc,4}";
    }

    private static void Summary(List<Sample> samples, bool both)
    {
        Console.WriteLine($"{"côté",-4} {"médiane ms",11} {"min ms",10} {"dispersion",10} " +
                          $"{"ns/instr",9} {"M instr/s",10} {"× temps réel",13}");

        var med = new Dictionary<string, double>();
        var min = new Dictionary<string, double>();

        foreach (var g in samples.GroupBy(s => s.Side))
        {
            var ms = g.Select(s => s.Ms).Order().ToArray();
            var m = ms.Length % 2 == 1 ? ms[ms.Length / 2] : (ms[ms.Length / 2 - 1] + ms[ms.Length / 2]) / 2;
            var dIns = g.First().DIns;
            var slices = g.First().Slices;
            med[g.Key] = m;
            min[g.Key] = ms[0];
            Console.WriteLine($"{g.Key,-4} {m,11:0.0} {ms[0],10:0.0} {(ms[^1] - ms[0]) / m,9:0.0 %} " +
                              $"{m * 1e6 / dIns,9:0.00} {dIns / (m * 1e3),10:0.00} {(slices / 100.0) / (m / 1e3),13:0.00}");
        }

        if (both && med.ContainsKey("C") && med.ContainsKey("C#"))
            Console.WriteLine($"ratio C#/C : médiane {med["C#"] / med["C"]:0.000}  ·  min {min["C#"] / min["C"]:0.000}");
    }

    /// <summary>Le contrôle qui donne son sens au ratio : un chiffre pris sur
    /// deux trajectoires différentes ne compare pas deux cœurs, il compare deux
    /// programmes. Rend le code de sortie.</summary>
    private static int Trajectory(Dictionary<string, List<Trace>> traces, bool both)
    {
        Console.WriteLine();
        var ok = true;

        // Reproductibilité par côté : chaque répétition repart d'un ré-amorçage
        // dans le même processus, et doit laisser exactement la même empreinte.
        foreach (var (side, list) in traces)
        {
            var first = list[0];
            Console.WriteLine($"empreinte {side,-3}: ins {first.DIns:N0}  Σcycles {first.Cycles:N0}  RAM 0x{first.Ram:X16}");
            for (var i = 1; i < list.Count; i++)
            {
                var t = list[i];
                if (t.DIns == first.DIns && t.Cycles == first.Cycles && t.Ram == first.Ram)
                    continue;
                Console.WriteLine($"  rép {i + 1} côté {side} diverge de la rép 1 : ins {t.DIns:N0}  " +
                                  $"Σcycles {t.Cycles:N0}  RAM 0x{t.Ram:X16} — ré-amorçage non reproductible.");
                ok = false;
            }
        }

        if (!both)
        {
            Console.WriteLine("trajectoire : un seul côté mesuré, pas de comparaison C / C#.");
            return ok ? 0 : 1;
        }

        var c = traces["C"][^1];
        var cs = traces["C#"][^1];
        var diff = c.DIns != cs.DIns ? $"Δins : C {c.DIns:N0}, C# {cs.DIns:N0}"
            : c.Cycles != cs.Cycles ? $"Σcycles : C {c.Cycles:N0}, C# {cs.Cycles:N0}"
            : Fuzzer.CompareStates(c.State, cs.State, c.LastCycles, cs.LastCycles, counters: false) is { } d ? $"état : {d}"
            : c.Ram != cs.Ram ? $"RAM : C 0x{c.Ram:X16}, C# 0x{cs.Ram:X16}"
            : null;

        if (diff is not null)
        {
            Console.WriteLine($"TRAJECTOIRES DIFFÉRENTES — {diff}");
            Console.WriteLine("Le ratio ci-dessus ne compare pas deux cœurs sur le même chemin : il est sans valeur.");
            return 1;
        }

        Console.WriteLine($"trajectoire : identique (Δins, Σcycles, vecteur d'état, RAM)" +
                          (ok ? $", {traces["C"].Count + traces["C#"].Count} mesures reproductibles." : "."));
        return ok ? 0 : 1;
    }
}
