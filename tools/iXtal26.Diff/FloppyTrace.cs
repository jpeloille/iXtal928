// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: aucun — outillage.
// STATUS: host — décodeur de diagnostic, pas de code PCem transcrit.
//
// Le pendant lisible de Floppy/fdc.cs : il traduit en français ce que les registres du
// 765 disent en hexadécimal. La table // noms: de fdc.cs explique les identifiants à la
// lecture ; ceci explique les VALEURS à l'exécution.
//
// Il ne s'accroche nulle part dans le cœur : il échantillonne après chaque instruction
// et n'imprime qu'aux transitions. Un appel de trace posé au fil de fdc.cs y aurait
// ajouté des lignes vivantes (R2) et du temps dans la boucle chaude ; ici, rien n'entre
// dans le cœur et rien ne tourne pendant les passes de comparaison.
//
// L'oracle ne peut pas participer : l'instance `fdc` est `static` dans fdc.c, donc
// invisible depuis harness.c. Cette trace décrit le côté C# seul. Ce qui garantit que
// l'autre côté fait la même chose reste le diff d'amorçage, instruction par instruction.

using iXtal26;
using iXtal26.Cpu;
using iXtal26.Floppy;

namespace iXtal26.Diff;

internal static class FloppyTrace
{
    /// <summary>Opcodes du 765. Les bits hauts (MT, MFM, SK) ne changent pas la
    /// commande : on masque comme le fait fdc_write au moment du dispatch.</summary>
    private static string Command(int op) => (op & 0x1f) switch
    {
        0x01 => "Mode (NSC)",
        0x02 => "Read track",
        0x03 => "Specify",
        0x04 => "Sense drive status",
        0x05 => "Write data",
        0x06 => "Read data",
        0x07 => "Recalibrate",
        0x08 => "Sense interrupt status",
        0x0a => "Read sector ID",
        0x0d => "Format track",
        0x0e => "Dump registers",
        0x0f => "Seek",
        0x10 => "Version",
        0x12 => "Perpendicular mode",
        0x13 => "Configure",
        0x14 => "Unlock",
        0x18 => "NSC",
        _ => $"0x{op:x2}",
    };

    /// <summary>L'état d'exécution : les quatre sentinelles hors espace d'opcodes,
    /// puis la commande en cours.</summary>
    private static string ExecState(int s) => s switch
    {
        -3 => "fin + interruption",
        -2 => "fin de commande",
        -1 => "reset",
        0 => "au repos",
        0xfc => "commande invalide",
        0x94 => "Lock",
        _ => Command(s),
    };

    /// <summary>Les quatre drapeaux du Main Status Register, plus les bits « ce lecteur
    /// cherche ». C'est la traduction des 0x90 / 0xD0 / 0xF0 / 0xB0 de fdc.c.</summary>
    private static string Msr(int v)
    {
        var f = new List<string>();
        if ((v & 0x80) != 0) f.Add("RQM");
        if ((v & 0x40) != 0) f.Add("DIO→UC");
        if ((v & 0x20) != 0) f.Add("hors-DMA");
        if ((v & 0x10) != 0) f.Add("occupé");
        for (var d = 0; d < 4; d++)
            if ((v & (1 << d)) != 0) f.Add($"seek{d}");
        return f.Count == 0 ? "—" : string.Join('|', f);
    }

    private static string St0(int v)
    {
        var f = new List<string>();
        var ic = (v >> 6) & 3;
        f.Add(ic switch { 0 => "normal", 1 => "anormal", 2 => "invalide", _ => "prêt-changé" });
        if ((v & 0x20) != 0) f.Add("seek-end");
        if ((v & 0x08) != 0) f.Add("non-prêt");
        if ((v & 0x04) != 0) f.Add("tête1");
        f.Add($"lecteur {v & 3}");
        return string.Join('|', f);
    }

    private static string SectorState(int s) => s switch
    {
        0 => "repos",
        1 => "cherche secteur",
        2 => "lit secteur",
        3 => "cherche 1er secteur",
        4 => "lit 1er secteur",
        5 => "cherche secteur suivant",
        6 => "lit secteur suivant",
        7 => "cherche pour écrire",
        8 => "écrit secteur",
        9 => "cherche adresse",
        10 => "lit adresse",
        11 => "cherche pour formater",
        12 => "formate",
        _ => $"?{s}",
    };

    /// <summary>Amorce le cœur C# seul et imprime chaque transition du contrôleur.
    /// Rend 0 : c'est un observateur, il n'a rien à réussir ou à rater.</summary>
    public static int Run(string romsPath, int slices, string? discA)
    {
        _808x.ResetDiagState();
        fdd_c.discfns[0] = discA ?? "";
        if (!pc.initpc(romsPath))
        {
            Console.Error.WriteLine($"Amorçage impossible depuis « {romsPath} ».");
            return 1;
        }

        Console.WriteLine($"Trace du contrôleur, {slices} tranches"
                          + (discA is null ? ", sans disquette" : $", disquette {discA}"));
        Console.WriteLine("  tranche  instr.  état d'exécution / commande      MSR                  média");

        var fdc = fdc_c.fdc;
        (int exec, int msr, int cmd, int sect, int motor) last = (0, 0, 0, 0, 0);
        var first = true;
        long instructions = 0;
        var transitions = 0;

        for (var s = 0; s < slices; s++)
        {
            var budget = Cpu.cpu_c.cpu_get_speed() / 100;
            while (budget > 0)
            {
                budget -= _808x.Step();
                instructions++;

                var now = (fdc_c.exec_state, fdc.msr, fdc.command,
                           Disc.disc_sector.disc_sector_state, Disc.disc.motoron);
                if (!first && now == last)
                    continue;
                first = false;
                last = now;
                transitions++;

                Console.WriteLine($"  {s,7}  {instructions,8}  {ExecState(now.Item1),-30}  "
                                  + $"{Msr(now.Item2),-20}  moteur {(now.Item5 != 0 ? "on " : "off")}  "
                                  + $"{SectorState(now.Item4)}");
            }
        }

        Console.WriteLine($"\n{transitions} transitions sur {instructions} instructions.");
        Console.WriteLine($"Dernier ST0 : {St0(fdc.st0)}   lecteur {fdc.drive}, "
                          + $"piste {fdc.track[fdc.drive]}, tête {fdc.head}, secteur {fdc.sector}");
        return 0;
    }
}
