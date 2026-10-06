// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// r9-disquette — la survie aux images de disquette qui arrêtaient l'hôte, en C# SEUL (R9, TRANSCRIPTION.md ;
// G13.0, PLAN-G13.md).
//
// Trois chemins, tous à l'insertion (disc_load → img_load → fdd_disc_changed → img_seek), au démarrage comme au
// menu :
// - PB-17 : une piste plus longue que les 20 Ko de track_data (disc_img.c:9) — une XDF à densité étendue (46
//   secteurs), ou 41 secteurs de 128 octets lus par 512. PCem écrit hors du tableau, le C# levait
//   ArgumentOutOfRangeException ; track_data fait désormais 195 × 512 octets par face ;
// - PB-168 : une BPB par ailleurs valide à 0 secteur par piste, qui fait diviser par zéro (disc_img.c:219) ; la
//   garde la traite comme une BPB fantaisiste, et se signale à Diag.R9 ;
// - un fichier vide : fseek(f, -1, SEEK_END) échoue en C et PCem survit, Seek levait (une faute de transcription,
//   corrigée dans disc.cs et disc_img.cs).
// Les images sont écrites dans le TMPDIR. La porte exige qu'aucune exception ne sorte, et la garde de PB-168
// atteinte. Les contrôles négatifs (le tampon ramené à 20 Ko, la garde retirée, le Seek rétabli) font chacun lever.

using iXtal26.Diag;

namespace iXtal26.Diff;

internal static class R9Disquette
{
    internal static int Run(string romsPath)
    {
        var tmp = Path.Combine(Path.GetTempPath(), $"r9-disquette-{Environment.ProcessId}");
        Directory.CreateDirectory(tmp);
        var bad = 0;
        try
        {
            if (!pc.setmodel("ibmpc") || !pc.setgfxcard("cga"))
                return 2;
            Floppy.fdd_c.discfns[0] = "";
            Floppy.fdd_c.discfns[1] = "";
            if (!pc.initpc(romsPath))
                return 2;

            void Essai(string nom, string fichier, string? garde)
            {
                R9.Raz();
                try
                {
                    Disc.disc.disc_load(0, fichier);
                    Disc.disc.disc_close(0);
                    if (garde is not null && R9.Compte(garde) == 0)
                    {
                        bad++;
                        Console.WriteLine($"  {nom} : survit, mais la garde {garde} n'est pas atteinte");
                    }
                    else
                        Console.WriteLine($"  {nom} : survit{(garde is null ? "" : $", garde {garde} atteinte")}");
                }
                catch (Exception e)
                {
                    bad++;
                    Console.WriteLine($"  {nom} : ARRÊT — {e.GetType().Name} : {e.Message.Trim()}");
                }
            }

            Essai("PB-17, XDF à densité étendue (46 secteurs de 512 octets, 23 552 octets par face)",
                  Image(tmp, "xdf-ed.img", 512, 46, 2, 80), null);
            Essai("PB-17, 41 secteurs de 128 octets (lus par 512 : 20 992 octets par face)",
                  Image(tmp, "s41.img", 128, 41, 2, 80), null);
            Essai("PB-168, une BPB à 0 secteur par piste", Image(tmp, "s0.img", 512, 0, 2, 0, 1474560), "disc_img.c:219");
            var vide = Path.Combine(tmp, "vide.img");
            File.WriteAllBytes(vide, []);
            Essai("un fichier vide", vide, null);
        }
        catch (Exception e)
        {
            Console.WriteLine($"r9-disquette : ARRÊT hors essai — {e.GetType().Name} : {e.Message.Trim()}");
            bad++;
        }
        finally
        {
            Directory.Delete(tmp, true);
        }

        Console.WriteLine(bad == 0 ? "\nVert : iXtal26 survit aux pistes de plus de 20 Ko, à la BPB sans secteur et au fichier vide (PB-17, PB-168, R9)."
                                   : $"\n{bad} essai(s) en échec : R9 n'est pas tenue.");
        return bad == 0 ? 0 : 1;
    }

    // Une image dont seule la BPB est écrite (octets 0Bh à 1Bh), à la taille que lit img_seek : pistes × faces ×
    // secteurs × 512 (sector_size est forcé à 512), ou la taille donnée.
    private static string Image(string dossier, string nom, int octetsParSecteur, int secteurs, int faces, int pistes,
                                long taille = 0)
    {
        var chemin = Path.Combine(dossier, nom);
        var total = pistes * faces * secteurs;
        var image = new byte[taille > 0 ? taille : (long)pistes * faces * secteurs * 512];
        image[0x0B] = (byte)octetsParSecteur;
        image[0x0C] = (byte)(octetsParSecteur >> 8);
        image[0x13] = (byte)total;
        image[0x14] = (byte)(total >> 8);
        image[0x15] = 0xF0;
        image[0x18] = (byte)secteurs;
        image[0x1A] = (byte)faces;
        File.WriteAllBytes(chemin, image);
        return chemin;
    }
}
