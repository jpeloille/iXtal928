// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// cpu-config-check (M16) — LE BALAYAGE DES TABLES DE CPU, des deux côtés.
//
// Pour chaque machine et chaque entrée de sa table, amorce l'oracle — qui fait tourner le
// VRAI cpu_set() de PCem — puis le C#, et confronte l'empreinte CPU champ par champ. Puis
// l'indice qui suit la dernière entrée : les deux côtés doivent le REFUSER.
//
// C'EST LE SEUL TÉMOIN DIFFÉRENTIEL des vitesses qu'aucun boot-diff n'atteint : le 286/20
// et le 286/25 n'existent que sur l'ami286, dont l'oracle n'a pas le chipset NEAT. Ce
// balayage vérifie leur CONFIGURATION — la table transcrite et le cpu_set() C# contre
// ceux de PCem — et rien de leur comportement.
//
// Aucune tranche n'est exécutée : l'empreinte est prise juste après l'amorçage.

using iXtal26.Cpu;
using iXtal26.Models;

namespace iXtal26.Diff;

internal static class CpuConfigCheck
{
    /// <param name="reverse">Balaye les machines de la dernière à la première : un AT
    /// d'abord, puis les 8088. Le vert ne doit pas dépendre de l'ordre — c'est ce qui
    /// prouve que rien d'une machine ne fuit dans la suivante (AT, cpu_16bitbus…).</param>
    internal static int Run(string romsPath, bool reverse = false)
    {
        Oracle.CheckAbi();

        Console.WriteLine("Balayage des tables de CPU — oracle (vrai cpu_set de PCem) contre C#.\n");

        var identical = 0;
        var refusals = 0;
        var failures = 0;
        var skipped = 0;

        // G4.1 : l'ami386 et l'ami386dx entrent au balayage — ce sont eux qui proposent le 387.
        // G6.3 : l'ami486 EN DERNIER — cpu_features n'est jamais remis à zéro par cpu_set
        // (PB-77) : l'ordre des entrées décide de ce qu'un i486 hérite d'un iDX4. Fixé ici.
        // G1.1 — l'Olivetti M24 (cpus_8086) après les deux 8088 : cpu_set y pose is8086, et le balayage
        // inverse prouve qu'il ne fuit pas vers le 5150 (PLAN-G1.md, risque n° 1).
        // G1.2 — l'Amstrad PC1512 (cpus_pc1512) derrière la M24.
        string[] order = ["ibmpc", "ibmxt", "olivetti_m24", "pc1512", "ibmat", "ami286", "ami386", "ami386dx", "ami486"];
        if (reverse)
            Array.Reverse(order);

        foreach (var name in order)
        {
            if (!pc.setmodel(name))
                return 2;

            var mdl = model_c.models[model_c.model];
            var table = mdl.cpu[0].cpus!;
            var n = 0;
            while (table[n].cpu_type != -1)
                n++;

            var core = Oracle.CoreForModel(mdl);

            // L'indice n, un cran après la dernière entrée, est la sentinelle : les deux
            // côtés doivent le refuser, et c'est vérifié comme le reste.
            for (var c = 0; c <= n; c++)
            // G4.1 — et, pour chaque entrée, CHACUN des coprocesseurs de sa liste (fpus,
            // cpu_tables.c:25-29) : la clé `fpu` passe par pc.cfg_fpu, qu'initpc résout ; l'oracle
            // reçoit le même type par h_set_fpu. La sentinelle, elle, n'a pas de liste.
            for (var f = 0; c == n ? f == 0 : table[c].fpus![f].internal_name != null; f++)
            {
                cpu_c.cpu_manufacturer = 0;
                cpu_c.cpu = c;
                pc.cfg_fpu = c < n ? table[c].fpus![f].internal_name! : "none";
                cpu_c.fpu_type = c < n ? table[c].fpus![f].type : cpu_c.FPU_NONE;
                var label = c < n ? $"{name,-8} cpu = {c}  {table[c].name,-10} fpu = {pc.cfg_fpu,-5}"
                                  : $"{name,-8} cpu = {c}  (hors table)";

                // G6.3 — les Pentium OverDrive de cpus_i486 : exclus (Intel seul, 8088 → 486 DX4,
                // décision utilisateur du 03/10) ; cpu_set les arrête (cpu.cs, `default: fatal`).
                // Sautés, et dit — pas comptés verts.
                if (c < n && table[c].cpu_type == cpu_c.CPU_PENTIUM)
                {
                        Console.WriteLine($"  {label} : exclu (CPU_PENTIUM, décision utilisateur), sauté");
                        skipped++;
                        continue;
                }

                Oracle.h_set_discfn(0, "");
                Oracle.h_set_discfn(1, "");
                Oracle.h_set_mem_size(pc.cfg_mem_size);
                Oracle.h_set_drive_type(0, pc.cfg_drive_type[0]);
                Oracle.h_set_drive_type(1, pc.cfg_drive_type[1]);
                Oracle.h_set_romset(pc.romset);
                Oracle.h_set_cpu(cpu_c.cpu_manufacturer, cpu_c.cpu);
                Oracle.h_set_fpu(cpu_c.fpu_type);
                Oracle.h_set_core(core);
                Oracle.h_set_gfxcard(pc.gfxcard);
                Oracle.h_set_hdd_controller("");
                var okOracle = Oracle.h_boot(romsPath) != 0;
                var fpOracle = okOracle ? CpuFingerprint.Oracle_() : null;

                _808x.ResetDiagState();
                Floppy.fdd_c.discfns[0] = "";
                Floppy.fdd_c.discfns[1] = "";
                var okCsharp = pc.initpc(romsPath);
                var fpCsharp = okCsharp ? CpuFingerprint.Csharp() : null;

                if (c == n)
                {
                    if (!okOracle && !okCsharp)
                    {
                        Console.WriteLine($"  {label} : refusé des deux côtés");
                        refusals++;
                    }
                    else
                    {
                        Console.WriteLine($"  {label} : ROUGE — accepté par " +
                                          (okOracle ? "l'oracle" : "") + (okOracle && okCsharp ? " et " : "") +
                                          (okCsharp ? "le C#" : ""));
                        failures++;
                    }
                    continue;
                }

                if (!okOracle || !okCsharp)
                {
                    Console.WriteLine($"  {label} : ROUGE — refusé par " +
                                      (!okOracle ? "l'oracle" : "") + (!okOracle && !okCsharp ? " et " : "") +
                                      (!okCsharp ? "le C#" : ""));
                    failures++;
                    continue;
                }

                if (CpuFingerprint.Compare(fpOracle!, fpCsharp!) != 0)
                {
                    Console.WriteLine($"  {label} : ROUGE (écarts ci-dessus)");
                    failures++;
                    continue;
                }

                Console.WriteLine($"  {label} : identique — {CpuFingerprint.Summary(fpCsharp!)}");
                identical++;
            }
        }

        pc.cfg_fpu = "none"; // ne rien laisser fuir vers l'appelant
        Console.WriteLine();
        if (failures > 0)
        {
            Console.WriteLine($"ROUGE : {failures} configuration(s) divergente(s) ; {identical} identiques, {refusals} refus concordants.");
            return 1;
        }

        Console.WriteLine($"Vert : {identical} configurations identiques, {refusals} refus concordants" + (skipped > 0 ? $", {skipped} entrée(s) non transcrite(s) sautée(s)." : "."));
        return 0;
    }
}
