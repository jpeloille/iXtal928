// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — outillage de vérification.
//
// banc ATTENDUS -- ARGUMENTS… — l'exécuteur de bancs en C# seul (G13.2 ; PLAN-G13.md, § La vérification). Le mode
// matériel n'a pas d'oracle : un banc dirigé s'y vérifie contre des attendus écrits, tirés de la documentation. Le
// banc lance iXtal26 (`iXtal26.dll`, à côté de l'outil) avec les ARGUMENTS d'un --boot — la machine, ses images, les
// frappes injectées — et le mode de ce processus (--hardware-mode) ; puis il cherche dans la sortie, dans l'ordre, les
// lignes du fichier ATTENDUS :
//   « texte »            attendu dans les deux modes ;
//   « pcem: texte »      attendu en mode PCem seulement ;
//   « materiel: texte »  attendu en mode matériel seulement ;
//   « # … »              un commentaire, où va la source de l'attendu.
// Le scénario est joué DEUX fois, et les deux sorties doivent être identiques à l'octet près, une fois effacé le numéro
// du processus que portent les copies temporaires des images (« ixtal-boot-PID- ») : le déterminisme du mode.

using System.Diagnostics;
using System.Text.RegularExpressions;

namespace iXtal26.Diff;

internal static class Banc
{
    internal static int Run(string[] args)
    {
        var sep = Array.IndexOf(args, "--");
        if (args.Length < 3 || sep != 1)
        {
            Console.Error.WriteLine("banc ATTENDUS -- ARGUMENTS D'IXTAL26 (--boot …)");
            return 2;
        }
        var fichier = args[0];
        if (!File.Exists(fichier))
        {
            Console.Error.WriteLine($"banc : {fichier} introuvable");
            return 2;
        }
        var mode = ModeMateriel.Actif ? "materiel" : "pcem";
        var attendus = new List<string>();
        foreach (var brut in File.ReadAllLines(fichier))
        {
            var l = brut.Trim();
            if (l.Length == 0 || l.StartsWith('#'))
                continue;
            if (l.StartsWith("pcem:") || l.StartsWith("materiel:"))
            {
                var deux = l.IndexOf(':');
                if (l[..deux] == mode)
                    attendus.Add(l[(deux + 1)..].Trim());
            }
            else
                attendus.Add(l);
        }

        var emulateur = Path.Combine(AppContext.BaseDirectory, "iXtal26.dll");
        var argv = args[(sep + 1)..].Concat(["--hardware-mode", ModeMateriel.ListeDemandee]).ToArray();
        var (rc1, sortie1) = Lance(emulateur, argv);
        var (rc2, sortie2) = Lance(emulateur, argv);
        var bad = 0;
        Console.WriteLine($"  mode {ModeMateriel.Description} : deux séances, retours {rc1} et {rc2}");
        if (rc1 != 0 || rc2 != 0)
        {
            bad++;
            Console.WriteLine($"  une séance n'a pas fini proprement :\n{sortie1.Split('\n').TakeLast(8).Aggregate((x, y) => x + "\n" + y)}");
        }
        if (sortie1 != sortie2)
        {
            bad++;
            Console.WriteLine("  les deux séances diffèrent : le scénario n'est pas déterministe");
        }
        else
            Console.WriteLine($"  les deux séances sont identiques ({sortie1.Length} caractères)");

        var lignes = sortie1.Split('\n');
        var pos = 0;
        foreach (var a in attendus)
        {
            var trouve = Array.FindIndex(lignes, pos, l => l.Contains(a));
            if (trouve < 0)
            {
                bad++;
                Console.WriteLine($"  [ECHEC] « {a} » n'apparaît pas (après la ligne {pos})");
                continue;
            }
            Console.WriteLine($"  [ok] « {a} » : {lignes[trouve].Trim()}");
            pos = trouve + 1;
        }
        Console.WriteLine(bad == 0
            ? $"\nVert : {Path.GetFileName(fichier)}, les {attendus.Count} attendus du mode {mode}, deux séances identiques."
            : $"\n{bad} échec(s) : {Path.GetFileName(fichier)} en mode {mode}.");
        return bad == 0 ? 0 : 1;
    }

    private static (int rc, string sortie) Lance(string dll, string[] args)
    {
        var psi = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true };
        psi.ArgumentList.Add(dll);
        foreach (var a in args)
            psi.ArgumentList.Add(a);
        using var p = Process.Start(psi)!;
        var sortie = p.StandardOutput.ReadToEndAsync();
        var erreur = p.StandardError.ReadToEndAsync();
        if (!p.WaitForExit(600_000))
        {
            p.Kill(true);
            return (-1, "délai dépassé (600 s)");
        }
        return (p.ExitCode, Regex.Replace(sortie.Result + erreur.Result, @"ixtal-boot-\d+-", "ixtal-boot-PID-"));
    }
}
