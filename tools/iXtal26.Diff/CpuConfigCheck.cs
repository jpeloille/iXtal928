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

        string[] order = ["ibmpc", "ibmxt", "ibmat", "ami286"];
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
            {
                cpu_c.cpu_manufacturer = 0;
                cpu_c.cpu = c;
                var label = c < n ? $"{name,-7} cpu = {c}  {table[c].name,-10}" : $"{name,-7} cpu = {c}  (hors table)";

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

        Console.WriteLine();
        if (failures > 0)
        {
            Console.WriteLine($"ROUGE : {failures} configuration(s) divergente(s) ; {identical} identiques, {refusals} refus concordants.");
            return 1;
        }

        Console.WriteLine($"Vert : {identical} configurations identiques, {refusals} refus concordants.");
        return 0;
    }
}
