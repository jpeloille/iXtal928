// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// r9-cue [DOSSIER] — les feuilles CUE qui font tomber PCem (G10.3), en C# SEUL.
//
// Une feuille CUE est une donnée de l'utilisateur, comme un .cfg (décision n° 4 de G10.3, 04/10). Deux
// font tomber PCem, et nulle autre lecture du parseur ne touche un fichier nul (VERIFICATION.md
// § G10.3) :
//   - des pistes sans FILE : la feuille est montée, et le premier secteur lu dans une de ces pistes
//     appelle read() sur un fichier nul (cdrom_image.cpp:183) — par READ, par READ CD brut, ou par le
//     rappel audio ;
//   - une piste avant le premier FILE, puis une piste qui en a un : AddTrack appelle getLength() sur le
//     fichier nul de la précédente (:427), pendant image_open.
// Ici, la lecture échoue et la feuille est refusée (Cdrom/cdrom_image.cs, « pcem bug, not
// reproduced ») ; chaque essai rougit, en nommant l'exception, si l'on retire sa garde. L'oracle n'est
// jamais appelé : il tomberait.

using iXtal26.Cdrom;
using iXtal26.Ide;

namespace iXtal26.Diff;

internal static class R9Cue
{
    internal static int Run(string? dossier)
    {
        string? genere = null;
        var bad = 0;
        try
        {
            if (dossier is null)
                dossier = genere = IsoGen.Generer();

            bad += Essai("des pistes sans FILE, lues (cdrom_image.cpp:183)", () =>
            {
                if (Ouvre(Path.Combine(dossier, "r9-sans-file.cue")) != 0)
                    return "la feuille devait être montée, comme chez PCem";
                var t = ide_atapi.atapi!;
                var b = new byte[4096];
                for (var s = 0; s <= 12; s++)
                {
                    if (t.readsector(b, 0, s, 1) != 1)
                        return $"READ du secteur {s} devait échouer";
                }
                t.readsector_raw(b, 0, 0);
                t.playaudio(150, 5, 0);
                cdrom_image.image_audio_callback(new short[8820], 8820);
                return cdrom_image.image_cd_state == cdrom_image.CD_STOPPED ? null : "le rappel audio devait s'arrêter";
            });

            bad += Essai("une piste avant le premier FILE (cdrom_image.cpp:427)", () =>
                Ouvre(Path.Combine(dossier, "r9-piste-avant-file.cue")) == 1 ? null : "la feuille devait être refusée");
        }
        finally
        {
            Raz();
            if (genere is not null)
                IsoGen.Effacer(genere);
        }

        Console.WriteLine(bad == 0
            ? "r9-cue : les deux feuilles survivent — la lecture échoue, la feuille fautive est refusée."
            : $"r9-cue : {bad} essai(s) en défaut.");
        return bad == 0 ? 0 : 1;
    }

    private static int Ouvre(string chemin)
    {
        Raz();
        cdrom_ioctl.cdrom_drive = cdrom_ioctl.old_cdrom_drive = ide.CDROM_IMAGE;
        return cdrom_image.image_open(chemin);
    }

    private static void Raz()
    {
        cdrom_image.image_clear_state_for_oracle_parity();
        ide_atapi.atapi = null;
    }

    private static int Essai(string nom, Func<string?> essai)
    {
        string? faute;
        try
        {
            faute = essai();
        }
        catch (Exception e)
        {
            faute = $"{e.GetType().Name} : {e.Message}";
        }

        Console.WriteLine(faute is null ? $"  survit : {nom}" : $"  ÉCHEC : {nom} — {faute}");
        return faute is null ? 0 : 1;
    }
}
