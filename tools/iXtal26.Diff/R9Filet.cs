// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// r9-filet — le filet des images (Host/FiletImages.cs ; G13.0, PLAN-G13.md), de bout en bout, en C# seul.
//
// closepc est le seul endroit qui vide les tampons d'écriture des images ; une exception que rien ne rattrape
// terminait le processus sans y passer, et les dernières écritures de l'invité étaient perdues. Il faut une écriture
// EN TAMPON au moment du plantage : celle d'un disque dur, par secteur de 512 octets (une disquette écrit des pistes
// entières, plus grandes que le tampon du FileStream, qui partent aussitôt). L'essai lance donc deux fois iXtal26
// --boot sur l'XT, un disque dur vierge de 10 Mo en C: (le Xebec), PC-DOS 2.00 en A:, sa disquette complémentaire en
// B:, avec la même frappe : DEBUG, un secteur de « A » écrit par l'INT 13h en LBA 0, puis Q. Une fois jusqu'à la
// sortie normale ; une fois avec IXTAL26_FAUTE_PLANTAGE=1, qui lève une exception non rattrapée juste après la frappe,
// avant savenvr et closepc. La porte exige que la seconde séance finisse sur cette exception (le filet sort en 70),
// que la première ait écrit le secteur, et que les deux disques soient identiques à l'octet près : le filet a poussé
// tout ce que closepc aurait poussé. Le contrôle négatif (le filet retiré du lanceur) laisse le secteur à zéro.
//
// Avant les séances, PB-33 en C# seul : savenvr sur le CMOS de l'IBM AT, le répertoire nvr/ absent. Il levait
// NullReferenceException, et la sortie normale sautait closepc ; il dit maintenant que le CMOS n'est pas écrit, et
// rend la main.

using System.Diagnostics;
using iXtal26.PluginApi;

namespace iXtal26.Diff;

internal static class R9Filet
{
    private const string Dos = "os/pcdos20/pcdos20b.img";
    private const string Supplement = "os/pcdos20/pcdos20s.img";
    private const long Disque10Mo = 306 * 4 * 17 * 512L;

    private static readonly string[] Frappe =
    {
        "", "", "B:DEBUG", "F 200 3FF 41", "A 100", "MOV AX,0301", "MOV CX,0001", "MOV DX,0080", "MOV BX,0200",
        "INT 13", "INT 3", "", "G=100", "Q",
    };

    internal static int Run(string romsPath)
    {
        if (!File.Exists(Dos) || !File.Exists(Supplement))
        {
            Console.WriteLine($"r9-filet : {Dos} ou {Supplement} introuvable (la porte se lance depuis la racine du dépôt ou de son bac à sable)");
            return 2;
        }
        var emulateur = Path.Combine(AppContext.BaseDirectory, "iXtal26.dll");
        var tmp = Path.Combine(Path.GetTempPath(), $"r9-filet-{Environment.ProcessId}");
        Directory.CreateDirectory(tmp);
        var bad = 0;
        try
        {
            var chemin = paths.nvr_path;
            var romset = Devices.nvr.oldromset;
            try
            {
                paths.nvr_path = Path.Combine(tmp, "absent") + "/";
                Devices.nvr.oldromset = pc.ROM_IBMAT;
                Devices.nvr.savenvr();
                Console.WriteLine("  PB-33, savenvr sans répertoire nvr/ : survit, le CMOS n'est pas écrit et la sortie continue");
            }
            catch (Exception e)
            {
                bad++;
                Console.WriteLine($"  PB-33, savenvr sans répertoire nvr/ : ARRÊT — {e.GetType().Name} : {e.Message.Trim()}");
            }
            finally
            {
                paths.nvr_path = chemin;
                Devices.nvr.oldromset = romset;
            }

            var romsAbsolu = Path.GetFullPath(romsPath);
            var (rc1, err1, propre) = Seance(emulateur, romsAbsolu, Path.Combine(tmp, "propre"), false);
            var (rc2, err2, plante) = Seance(emulateur, romsAbsolu, Path.Combine(tmp, "plante"), true);
            Console.WriteLine($"  séance normale : retour {rc1}");
            Console.WriteLine($"  séance plantée : retour {rc2}");

            if (rc1 != 0)
            {
                bad++;
                Console.WriteLine($"  la séance normale n'a pas fini proprement :\n{err1}");
            }
            if (rc2 != 70 || !err2.Contains("IXTAL26_FAUTE_PLANTAGE"))
            {
                bad++;
                Console.WriteLine($"  la séance plantée n'a pas fini sur la panne injectée par le filet (retour {rc2}, attendu 70) :\n{err2}");
            }
            var secteurA = new byte[512];
            Array.Fill(secteurA, (byte)'A');
            if (!propre.AsSpan(0, 512).SequenceEqual(secteurA))
            {
                bad++;
                Console.WriteLine("  la séance normale n'a pas écrit son secteur de « A » : l'essai ne prouve rien");
            }
            if (propre.AsSpan().SequenceEqual(plante))
                Console.WriteLine("  les deux disques sont identiques, le secteur de « A » écrit par l'INT 13h compris");
            else
            {
                bad++;
                Console.WriteLine($"  les deux disques diffèrent de {Differents(propre, plante)} octets : des écritures se sont perdues au plantage");
            }
        }
        catch (Exception e)
        {
            bad++;
            Console.WriteLine($"r9-filet : ARRÊT — {e.GetType().Name} : {e.Message.Trim()}");
        }
        finally
        {
            Directory.Delete(tmp, true);
        }

        Console.WriteLine(bad == 0 ? "\nVert : un plantage après la frappe garde sur le disque tout ce qu'une sortie normale y écrit (le filet)."
                                   : $"\n{bad} échec(s) : le filet des images ne tient pas.");
        return bad == 0 ? 0 : 1;
    }

    private static (int rc, string err, byte[] disque) Seance(string emulateur, string roms, string dossier, bool plantage)
    {
        Directory.CreateDirectory(dossier);
        var a = Path.Combine(dossier, "a.img");
        var b = Path.Combine(dossier, "b.img");
        var hd = Path.Combine(dossier, "hd.img");
        File.Copy(Dos, a);
        File.Copy(Supplement, b);
        using (var f = new FileStream(hd, FileMode.Create, FileAccess.Write))
            f.SetLength(Disque10Mo);

        var psi = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = dossier,
        };
        foreach (var x in new[] { emulateur, "--boot", roms, "8000", "--model", "ibmxt", "--hdd", hd, "--floppy-a", a,
                                  "--floppy-b", b, "--in-place", "--settle", "600" })
            psi.ArgumentList.Add(x);
        foreach (var t in Frappe)
        {
            psi.ArgumentList.Add("--type");
            psi.ArgumentList.Add(t);
        }
        // G13 — les séances jouent le mode de ce processus : sans cela, la porte lancée en mode matériel n'éprouverait
        //   que le mode PCem.
        psi.ArgumentList.Add("--hardware-mode");
        psi.ArgumentList.Add(ModeMateriel.ListeDemandee);
        psi.Environment.Remove("IXTAL26_FAUTE_PLANTAGE");
        if (plantage)
            psi.Environment["IXTAL26_FAUTE_PLANTAGE"] = "1";
        using var p = Process.Start(psi)!;
        var sortie = p.StandardOutput.ReadToEndAsync();
        var err = p.StandardError.ReadToEndAsync();
        if (!p.WaitForExit(300_000))
        {
            p.Kill(true);
            return (-1, "délai dépassé (300 s)", []);
        }
        sortie.Wait();
        return (p.ExitCode, err.Result, File.ReadAllBytes(hd));
    }

    private static int Differents(byte[] x, byte[] y)
    {
        var n = Math.Abs(x.Length - y.Length);
        for (var i = 0; i < Math.Min(x.Length, y.Length); i++)
            if (x[i] != y[i])
                n++;
        return n;
    }
}
