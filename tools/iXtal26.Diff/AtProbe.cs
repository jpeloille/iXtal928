// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — outillage de diff.
//
// B2 — L'ORACLE AMORCE-T-IL UN AT ?
//
// La sonde ne compare rien : elle fait tourner le POST de l'IBM AT 5170 dans
// l'ORACLE SEUL et rapporte où il va. C'est délibéré, et c'est l'ordre du plan
// (PLAN-286.md, bloc B2) : tant que l'oracle ne sait pas amorcer un AT, la
// machine AT et le mode protégé s'écriraient côté C# sans rien à quoi les
// comparer.
//
// CE QU'ELLE MESURE :
//   - combien d'instructions le POST exécute avant de s'arrêter ou de boucler ;
//   - où il en est (CS:IP) à intervalles réguliers ;
//   - S'IL ENTRE EN MODE PROTEGE, et au bout de combien d'instructions — le
//     chiffre que PLAN-286.md dit explicitement ne pas connaître, et dont il
//     dit qu'il pourrait faire remonter le bloc C dans l'ordre.

using iXtal26.Diag;

namespace iXtal26.Diff;

public static class AtProbe
{
    private const int ROM_IBMAT = 25;

    public static int Run(string romsPath)
    {
        Oracle.CheckAbi();

        Console.WriteLine("Sonde d'amorçage AT — ORACLE SEUL, aucune comparaison.\n");

        Oracle.h_set_core(Oracle.Core286);
        Oracle.h_set_romset(ROM_IBMAT);
        Oracle.h_set_mem_size(512);

        if (Oracle.h_boot(romsPath) == 0)
        {
            Console.WriteLine("ROUGE : h_boot a échoué — le BIOS de l'AT n'a pas pu être chargé.");
            Console.WriteLine("        Attendu : roms/ibmat/62x0820.u27 et 62x0821.u47");
            return 1;
        }
        Console.WriteLine("h_boot : le BIOS de l'AT est chargé, la machine est montée.\n");

        var s = HState.Create();
        Oracle.h_getstate(out s);
        Console.WriteLine($"  au reset        CS:IP {s.seg_sel[0]:X4}:{s.pc:X4}   msw {s.cr0 & 0xFFFF:X4}");

        // Le POST tourne par tranches. On s'arrête a la PREMIERE des deux
        // conditions : le bit 0 du mot d'état machine passe a un (entrée en mode
        // protégé), ou le budget est épuisé.
        const int tranche = 200_000;
        const int tranches = 5000;
        ulong total = 0;
        var pmodeA = -1L;

        for (var t = 0; t < tranches; t++)
        {
            var n = Oracle.h_run(tranche);
            total += (ulong)n;
            Oracle.h_getstate(out s);

            if (pmodeA < 0 && (s.cr0 & 1) != 0)
            {
                pmodeA = (long)total;
                Console.WriteLine($"\n  *** MODE PROTEGE *** apres ~{pmodeA:N0} instructions");
                Console.WriteLine($"      CS:IP {s.seg_sel[0]:X4}:{s.pc:X4}  msw {s.cr0 & 0xFFFF:X4}");
                Console.WriteLine($"      gdt base {s.sys_base[0]:X8} limit {s.sys_limit[0]:X4}");
                Console.WriteLine($"      idt base {s.sys_base[2]:X8} limit {s.sys_limit[2]:X4}");
                break;
            }

            if (t % 250 == 0)
                Console.WriteLine($"  {total,12:N0} instr   CS:IP {s.seg_sel[0]:X4}:{s.pc:X4}   " +
                                  $"AX {s.regs[0]:X4}  msw {s.cr0 & 0xFFFF:X4}   " +
                                  $"timer {s.n_timer_process,10:N0}  inb {s.n_inb,9:N0}  " +
                                  $"pic {s.n_picinterrupt,6:N0}");
        }

        Console.WriteLine();
        Oracle.h_getstate(out s);
        Console.WriteLine($"  arret           CS:IP {s.seg_sel[0]:X4}:{s.pc:X4}   " +
                          $"{total:N0} instructions");

        if (pmodeA < 0)
            Console.WriteLine("\n  Le POST n'est PAS entre en mode protege dans ce budget.");

        Console.WriteLine("\nVert : la sonde a tourne. Ce qu'elle rapporte est une MESURE,");
        Console.WriteLine("       pas une comparaison — le cote C# n'a pas encore de machine AT.");
        return 0;
    }
}
