// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — le filet des images (G13.0, PLAN-G13.md, décision n° 2).
// STATUS: host — pas de code PCem transcrit.
//
// closepc est le seul endroit qui vide les tampons d'écriture des images (pc.cs) : img_writeback écrit dans le
// FileStream de la disquette sans Flush, les disques durs et le ZIP tiennent le leur. Une exception que rien ne
// rattrape, ou un signal de fin, termine le processus sans y passer, et les dernières écritures de l'invité — un MD,
// une copie — restaient dans ces tampons. Le filet les vide sur ces sorties-là sans rien exécuter de l'émulateur :
// il ne fait que pousser les octets déjà écrits. Une sortie normale l'a déjà fait par closepc, et le filet n'y trouve
// plus rien. Il ne vide que les tampons : le CMOS, que savenvr écrit, n'en fait pas partie.
//
// Sur une exception non rattrapée, le filet écrit l'exception sur la sortie d'erreur et sort en 70 (EX_SOFTWARE)
// au lieu de laisser le runtime avorter : un avortement passe par le gestionnaire de plantages du système, qui
// garde un rapport de plusieurs dizaines de Mo par plantage (apport, sous Ubuntu).

namespace iXtal26.Host;

internal static class FiletImages
{
    private static bool _pose;

    internal static void Poser()
    {
        if (_pose)
            return;
        _pose = true;
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            Vider();
            Console.Error.WriteLine($"Erreur interne, l'émulateur s'arrête ; les images sont vidées.\n{e.ExceptionObject}");
            Environment.Exit(70);
        };
        AppDomain.CurrentDomain.ProcessExit += (_, _) => Vider();
    }

    internal static void Vider()
    {
        Pousser(Disc.hdd_file.fflush_tous);
        for (var drive = 0; drive < 2; drive++)
        {
            var d = drive;
            Pousser(() => Disc.disc_img.img_vider_tampon(d));
        }
        Pousser(() => Scsi.scsi_zip_c.zip_data?.hdd.f?.Flush());
    }

    // Un flux déjà fermé par closepc, ou qui refuse d'écrire, n'empêche pas de vider les autres.
    private static void Pousser(Action vidage)
    {
        try
        {
            vidage();
        }
        catch (ObjectDisposedException)
        {
        }
        catch (IOException)
        {
        }
    }
}
