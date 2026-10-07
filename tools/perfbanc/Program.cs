// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — outillage de mesure.
//
// LE BANC DE PERFORMANCE (PLAN.md : G15 et l'annexe des outils).
//
//     dotnet run -c Release --project tools/perfbanc -- --filter '*'
//
// BenchmarkDotNet fait tourner chaque banc dans un processus enfant qu'il génère. Le job y
// pose DOTNET_TieredCompilation=0, comme iXtal26.csproj : on mesure le code que le produit
// exécute, compilé une fois et optimisé d'emblée, et non un palier intermédiaire du JIT.
// C'est un job MUTATEUR (AsMutator) : il s'applique à tous les autres, donc un `--job short`
// ou `--job dry` de la ligne de commande garde la variable. AsDefault ne le faisait pas —
// mesuré : le `--job dry` était ignoré et le job complet tournait à sa place.
// Les rapports vont sous bin/, que .gitignore écarte déjà.

using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;

namespace iXtal26.PerfBanc;

internal static class Program
{
    private static void Main(string[] args)
    {
        var config = DefaultConfig.Instance
            .AddJob(Job.Default.WithEnvironmentVariables(new EnvironmentVariable("DOTNET_TieredCompilation", "0")).AsMutator())
            .AddDiagnoser(MemoryDiagnoser.Default)
            .WithArtifactsPath(Path.Combine(AppContext.BaseDirectory, "BenchmarkDotNet.Artifacts"));

        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);
    }
}
