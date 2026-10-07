// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — outillage de vérification.
//
// materiel-mode — le mécanisme du mode matériel (G13.2, PLAN-G13.md § Le mécanisme), en C# seul, sans aucune correction
// en jeu : ses refus, sa clé, sa ligne de commande, son gel.
//   - Les refus : l'oracle est PCem. boot-diff, le fuzzeur hors contrôle de fuite et sst-probe hors --target csharp
//     rendent 2 sous --hardware-mode ; boot-diff rend 2 sur un .cfg qui porte hardware_mode = 1 ; une liste inconnue
//     rend 2. Une commande acceptée (config-check) tourne.
//   - Le gel : ce processus s'est figé en mode PCem avant l'aiguillage ; une demande qui change le mode est refusée,
//     une demande qui ne change rien passe ; la clé du .cfg ne fait pas exception.
//   - Le lanceur d'iXtal26 : --boot dit le mode quand il est actif, et le fige à son point de gel, en tête d'initpc (un
//     gel manqué se dit : « hors de son point de gel ») ; la ligne de commande l'emporte sur le fichier ; une valeur de
//     la clé hors de 0 et 1 retombe sur le mode PCem, avec un avertissement ; un PB inconnu rend 2.

using System.Diagnostics;

namespace iXtal26.Diff;

internal static class MaterielMode
{
    private static int bad;

    internal static int Run()
    {
        var outil = Path.Combine(AppContext.BaseDirectory, "iXtal26.Diff.dll");
        var emulateur = Path.Combine(AppContext.BaseDirectory, "iXtal26.dll");
        var tmp = Path.Combine(Path.GetTempPath(), $"materiel-mode-{Environment.ProcessId}");
        Directory.CreateDirectory(tmp);
        bad = 0;
        try
        {
            var hw1 = Path.Combine(tmp, "hw1.cfg");
            var hw7 = Path.Combine(tmp, "hw7.cfg");
            File.WriteAllText(hw1, "model = ibmxt\nhardware_mode = 1\n");
            File.WriteAllText(hw7, "model = ibmxt\nhardware_mode = 7\n");

            Console.WriteLine("Les refus :");
            Lance("--hardware-mode tout boot-diff", outil, ["--hardware-mode", "tout", "boot-diff", "roms", "10"], 2, "refusé");
            Lance("--hardware-mode tout fuzz", outil, ["--hardware-mode", "tout", "fuzz", "--iter", "10"], 2, "contrôle de fuite");
            Lance("--hardware-mode tout sst-probe", outil, ["--hardware-mode", "tout", "sst-probe"], 2, "--target csharp");
            Lance("boot-diff --config hardware_mode = 1", outil, ["boot-diff", "roms", "10", "--config", hw1], 2,
                  "l'oracle est PCem");
            Lance("--hardware-mode PB-99", outil, ["--hardware-mode", "PB-99", "config-check"], 2, "n'est pas corrigé");
            Lance("--hardware-mode sans liste", outil, ["--hardware-mode", "config-check"], 2, "attend une liste");
            Lance("--hardware-mode tout config-check, acceptée", outil, ["--hardware-mode", "tout", "config-check"], 0,
                  "mode materiel (tout)");

            Console.WriteLine("\nLe gel, dans ce processus (figé en mode PCem avant l'aiguillage) :");
            Verifie("figé, en mode PCem", ModeMateriel.Fige && !ModeMateriel.Actif && !materiel.pb_01, ModeMateriel.Description);
            Verifie("une demande qui change le mode est refusée", !ModeMateriel.Demander("tout", "materiel-mode"), ModeMateriel.Description);
            Verifie("une demande qui ne change rien passe", ModeMateriel.Demander("aucun", "materiel-mode"), ModeMateriel.Description);
            Verifie("la clé hardware_mode = 1 est refusée par loadconfig", !pc.loadconfig(hw1), ModeMateriel.Description);
            Verifie("la clé hardware_mode = 7 retombe sur le mode PCem, qui ne change rien", pc.loadconfig(hw7) && !ModeMateriel.Actif,
                    ModeMateriel.Description);

            Console.WriteLine("\nLe lanceur d'iXtal26 :");
            Lance("--boot --hardware-mode tout, figé à son point de gel", emulateur,
                  ["--boot", "roms", "30", "--model", "ibmxt", "--hardware-mode", "tout"], 0, "mode materiel (tout)",
                  "hors de son point de gel", "non figé");
            Lance("--boot sans le mode, figé par initpc", emulateur, ["--boot", "roms", "30", "--model", "ibmxt"], 0,
                  "initpc OK", "mode ");
            Lance("--boot --config hardware_mode = 1", emulateur, ["--boot", "roms", "30", "--config", hw1], 0, "mode materiel (tout)");
            Lance("--boot --config hardware_mode = 1 --hardware-mode aucun", emulateur,
                  ["--boot", "roms", "30", "--config", hw1, "--hardware-mode", "aucun"], 0, "initpc OK", "mode materiel");
            Lance("--boot --config hardware_mode = 7", emulateur, ["--boot", "roms", "30", "--config", hw7], 0,
                  "hors de 0 et 1", "mode materiel");
            Lance("--boot --hardware-mode PB-99", emulateur, ["--boot", "roms", "30", "--hardware-mode", "PB-99"], 2, "n'est pas corrigé");
        }
        finally
        {
            Directory.Delete(tmp, true);
        }
        Console.WriteLine(bad == 0
            ? "\nVert : le mode matériel se refuse là où l'oracle compare, se fige avant le premier cœur, et se choisit au lancement."
            : $"\n{bad} échec(s) : le mécanisme du mode matériel ne tient pas.");
        return bad == 0 ? 0 : 1;
    }

    private static void Verifie(string quoi, bool ok, string etat)
    {
        Console.WriteLine($"  [{(ok ? "ok" : "ECHEC")}] {quoi} : {etat}");
        if (!ok)
            bad++;
    }

    // Lance `dll` avec `args` ; vert si le retour vaut `rc`, si la sortie contient `doit` et aucun de `nePasDire`.
    private static void Lance(string quoi, string dll, string[] args, int rc, string doit, params string[] nePasDire)
    {
        var psi = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true };
        psi.ArgumentList.Add(dll);
        foreach (var a in args)
            psi.ArgumentList.Add(a);
        using var p = Process.Start(psi)!;
        var sortie = p.StandardOutput.ReadToEndAsync();
        var erreur = p.StandardError.ReadToEndAsync();
        if (!p.WaitForExit(120_000))
        {
            p.Kill(true);
            Verifie(quoi, false, "délai dépassé (120 s)");
            return;
        }
        var texte = sortie.Result + erreur.Result;
        var ok = p.ExitCode == rc && texte.Contains(doit) && !nePasDire.Any(texte.Contains);
        var ligne = texte.Split('\n').FirstOrDefault(l => l.Contains(doit))?.Trim() ?? texte.Trim().Split('\n')[^1].Trim();
        Verifie(quoi, ok, $"retour {p.ExitCode} (attendu {rc}), « {(ligne.Length > 100 ? ligne[..100] + "…" : ligne)} »");
    }
}
