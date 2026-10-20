// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun)
// STATUS: host
//
// G9.1 (prévention, après l'incident du 03/10, VERIFICATION.md § G9.1) : `--boot` monte des
// COPIES des images de disquette, de disque dur et (GR.2d1) du ZIP, faites dans le répertoire temporaire, comme
// boot-diff le fait depuis M6 — une commande tapée dans l'invité (W de DEBUG, COPY, FORMAT) ne
// peut plus écrire dans les images de l'utilisateur. `--in-place` rend l'écriture en place ;
// aucun outil du dépôt ne s'en sert. L'usage normal de l'émulateur (hors --boot) ne change pas.

using iXtal26.Disc;
using iXtal26.Floppy;

namespace iXtal26.Host.CommandLine;

internal static class BootImageCopies
{
    /// <summary>Remplace chaque image montée (A:, B:, les disques durs) par une copie temporaire.</summary>
    public static void CopyMountedImages()
    {
        for (var d = 0; d < fdd_c.discfns.Length; d++)
            if (!string.IsNullOrEmpty(fdd_c.discfns[d]))
                fdd_c.discfns[d] = Copy(fdd_c.discfns[d], d == 0 ? "a" : "b");
        for (var d = 0; d < hdd_c.ide_fn.Length; d++)
            if (!string.IsNullOrEmpty(hdd_c.ide_fn[d]))
                hdd_c.ide_fn[d] = Copy(hdd_c.ide_fn[d], $"hd{d}");
        // GR.2d1 — l'image du lecteur ZIP aussi (zip_path, chargée par initpc) : ZIPBANC y écrit, en C# seul.
        if (!string.IsNullOrEmpty(pc.cfg_zip_path))
            pc.cfg_zip_path = Copy(pc.cfg_zip_path, "zip");
    }

    /// <summary>Une copie de `path` dans le répertoire temporaire, nommée par le processus et le
    /// rôle ; rend son chemin. Une image absente est laissée telle quelle (le montage le dira).</summary>
    public static string Copy(string path, string role)
    {
        if (!File.Exists(path))
            return path;
        var dest = Path.Combine(Path.GetTempPath(), $"ixtal-boot-{Environment.ProcessId}-{role}-{Path.GetFileName(path)}");
        File.Copy(path, dest, overwrite: true);
        Console.WriteLine($"--boot : {role} sur une copie, {dest}");
        Copies.Add(dest);
        return dest;
    }

    // GR.2d1 — les copies faites, effacées à la sortie de --boot (Purge) : elles restaient dans le répertoire
    //   temporaire, 160 Mo par disque dur et 100 Mo par image ZIP à chaque passage.
    private static readonly List<string> Copies = [];

    /// <summary>Efface les copies faites par ce processus (après closepc, ou sur une exception).</summary>
    public static void Purge()
    {
        foreach (var c in Copies)
            try
            {
                File.Delete(c);
            }
            catch (IOException)
            {
            }
        Copies.Clear();
    }

    /// <summary>Vrai sauf --in-place : les échanges de disquette du script (@A:, @B:) copient aussi.</summary>
    public static bool Enabled = true;
}
