// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// r9-cdcfg [DOSSIER] — les clés du lecteur de CD-ROM dans le .cfg (G10.4), en C# SEUL.
//
// Une valeur hors liste prend le défaut avec un avertissement (traitement de PB-93), un nom inconnu est
// refusé, et rien ne fait tomber l'émulateur :
//   - cd_speed hors des dix-huit vitesses : 24 (à 0, PCem divise par zéro à la première lecture,
//     scsi_cd.c:1081, :1170) ;
//   - cd_model inconnu : refusé, retour 2 (PCem lit cd_models[12], hors de la table, scsi_cd.c:510-514) ;
//   - cdrom_path de 1 024 octets ou plus : écarté, le lecteur vide (PB-110 : image_path[1024] débordé) ;
//   - cdrom_drive ni -1 ni 200 : le lecteur physique de l'hôte, exclu ; le lecteur vide ;
//   - une image présente mais illisible : le lecteur vide (PB-116, R9 : PCem laisse atapi nul, et le premier
//     reset IDE le déréférence, ide.c:802, :812) ;
//   - cdrom_channel hors de -1 à 3 : aucun lecteur ; zip_channel de même depuis G10.6 (refusé avant), et le ZIP
//     sur l'unité du CD : le CD la garde, averti ;
//   - deux avertissements sans effet : le CD sur le canal d'un disque, un cdrom_channel sans contrôleur IDE.
// Chaque essai amorce un ami486, compte les avertissements de la sortie d'erreur, vérifie ce qui a été retenu,
// puis conduit le canal secondaire : un reset logiciel (376h, SRST — le rappel de reset appelle atapi->stop()),
// TEST UNIT READY et READ(10). Il survit, ou l'exception est nommée.

using iXtal26.Cdrom;
using iXtal26.Diag;
using iXtal26.Ide;
using iXtal26.Scsi;

namespace iXtal26.Diff;

internal static class R9CdCfg
{
    private const string Machine = "model = ami486\ncpu = 10\nmem_size = 4096\ngfxcard = tvga9000b\n";

    internal static int Run(string romsPath, string? dossier)
    {
        string? genere = null;
        var bad = 0;
        var n = 0;
        try
        {
            if (dossier is null)
                dossier = genere = IsoGen.Generer();
            var iso = Path.Combine(dossier, "iso-2048.iso");
            var ide = "hdd_controller = ide\ncdrom_channel = 2\n";
            var image = ide + $"cdrom_drive = 200\ncdrom_path = {iso}\n";

            bad += Essai(romsPath, n++, "dans la liste : cd_speed 4, cd_model cr-587-b", image + "cd_speed = 4\ncd_model = cr-587-b\n", 0,
                         () => Lecture(true) ?? (scsi_cd_c.cd_speed == 4 && scsi_cd_c.cd_model == "Creative CR-587-B (24X)" ? null : "vitesse ou modèle perdus"));
            foreach (var v in new[] { 0, -5, 5 })
                bad += Essai(romsPath, n++, $"PB-93 : cd_speed = {v}", image + $"cd_speed = {v}\n", 1,
                             () => Lecture(true) ?? (scsi_cd_c.cd_speed == 24 ? null : $"ATTENDU 24, lu {scsi_cd_c.cd_speed}"));
            bad += Refus(romsPath, n++, "PB-93 : cd_model inconnu", image + "cd_model = xyz\n", "modèle de lecteur de CD-ROM inconnu");
            bad += Essai(romsPath, n++, "PB-110 : cdrom_path de 1 500 octets", ide + $"cdrom_drive = 200\ncdrom_path = /{new string('x', 1499)}\n", 1,
                         () => Vide());
            foreach (var v in new[] { 0, 5, -7 })
                bad += Essai(romsPath, n++, $"cdrom_drive = {v} (lecteur physique)", ide + $"cdrom_drive = {v}\ncdrom_path = {iso}\n",
                             1, () => Vide());
            foreach (var nom in new[] { "vide.iso", "sans-pvd.iso", "." })
                bad += Essai(romsPath, n++, $"PB-116 : image illisible ({nom})", ide + $"cdrom_drive = 200\ncdrom_path = {Path.Combine(dossier, nom)}\n", 1,
                             () => Vide() ?? (R9.Compte("pc.c:299") == 1 ? null : "la garde de pc.c:299 n'a pas été atteinte"));
            foreach (var v in new[] { 9, -3 })
                bad += Essai(romsPath, n++, $"PB-93 : cdrom_channel = {v}", $"hdd_controller = ide\ncdrom_channel = {v}\n", 1,
                             () => Ide.ide.cdrom_channel == -1 && Ide.ide.ide_drives[2].type == Ide.ide.IDE_NONE ? null : "aucun lecteur attendu");
            bad += Essai(romsPath, n++, "le CD sur le canal d'un disque configuré", $"hdd_controller = ide\ncdrom_channel = 0\nhdc_fn = {iso}\n", 1,
                         () => Ide.ide.ide_drives[0].type == Ide.ide.IDE_CDROM ? null : "le CD devait prendre l'unité 0, comme chez PCem");
            bad += Essai(romsPath, n++, "un cdrom_channel sans contrôleur IDE", "hdd_controller = mfm_at\ncdrom_channel = 2\n", 1,
                         () => Ide.ide.ide_drives[2].type == Ide.ide.IDE_NONE ? null : "aucun lecteur attendu");
            // G10.6 — la décision n° 18 levée : zip_channel est reçu de -1 à 3 ; hors de là, ramené à -1, averti.
            bad += Essai(romsPath, n++, "zip_channel = 9", "hdd_controller = ide\nzip_channel = 9\n", 1,
                         () => Ide.ide.zip_channel == -1 ? null : $"zip_channel devait revenir à -1 : {Ide.ide.zip_channel}");
            bad += Essai(romsPath, n++, "le ZIP sur l'unité du CD", "hdd_controller = ide\ncdrom_channel = 2\nzip_channel = 2\n", 1,
                         () => Ide.ide.ide_drives[2].type == Ide.ide.IDE_CDROM && scsi_zip_c.zip_data is null
                             ? null : "le CD devait garder l'unité 2, sans lecteur ZIP");
        }
        finally
        {
            if (genere is not null)
                IsoGen.Effacer(genere);
        }

        Console.WriteLine(bad == 0 ? "r9-cdcfg : tout survit ; hors liste → défaut averti, nom inconnu refusé." : $"r9-cdcfg : {bad} essai(s) en défaut.");
        return bad == 0 ? 0 : 1;
    }

    /// <summary>Un essai : le .cfg chargé et la machine amorcée en captant la sortie d'erreur ; les avertissements
    /// du lecteur comptés (les lignes qui commencent par sa clé) ; puis `verif` (null : conforme).</summary>
    private static int Essai(string romsPath, int n, string nom, string corps, int avertis, Func<string?> verif)
    {
        var cfg = Path.Combine(Path.GetTempPath(), $"r9-cdcfg-{Environment.ProcessId}-{n}.cfg");
        File.WriteAllText(cfg, Machine + corps);
        var err = Console.Error;
        var capte = new StringWriter();
        R9.Raz();
        try
        {
            Console.SetError(capte);
            if (!pc.loadconfig(cfg))
                throw new InvalidOperationException("loadconfig");
            Floppy.fdd_c.discfns[0] = Floppy.fdd_c.discfns[1] = "";
            if (!pc.initpc(romsPath))
                throw new InvalidOperationException("initpc");
            R9Atapi.Tranches(50);
            var faute = verif();
            pc.closepc();
            Console.SetError(err);
            var lignes = Avertissements(capte.ToString());
            foreach (var l in lignes)
                Console.WriteLine($"    {l}");
            if (faute is null && lignes.Count != avertis)
                faute = $"{lignes.Count} avertissement(s), ATTENDU {avertis}";
            Console.WriteLine($"  {nom} : {(faute is null ? "survit" : $"EN DÉFAUT — {faute}")}");
            return faute is null ? 0 : 1;
        }
        catch (Exception e)
        {
            Console.SetError(err);
            Console.WriteLine($"  {nom} : ARRÊT — {e.GetType().Name} : {e.Message.Trim()}");
            return 1;
        }
        finally
        {
            Console.SetError(err);
            File.Delete(cfg);
            // Une machine arrêtée en route n'est pas refermée : l'amorçage suivant la reconstruit (resetpchard).
        }
    }

    /// <summary>Un refus : loadconfig rend faux, avec le message attendu.</summary>
    private static int Refus(string romsPath, int n, string nom, string corps, string message)
    {
        var cfg = Path.Combine(Path.GetTempPath(), $"r9-cdcfg-{Environment.ProcessId}-{n}.cfg");
        File.WriteAllText(cfg, Machine + corps);
        var err = Console.Error;
        var capte = new StringWriter();
        try
        {
            Console.SetError(capte);
            var charge = pc.loadconfig(cfg);
            Console.SetError(err);
            var dit = capte.ToString();
            var faute = charge ? "le .cfg devait être refusé" : !dit.Contains(message) ? $"message attendu : « {message} »" : null;
            Console.WriteLine($"    {dit.Split('\n')[0].Trim()}");
            Console.WriteLine($"  {nom} : {(faute is null ? "refusé" : $"EN DÉFAUT — {faute}")}");
            return faute is null ? 0 : 1;
        }
        catch (Exception e)
        {
            Console.SetError(err);
            Console.WriteLine($"  {nom} : ARRÊT — {e.GetType().Name} : {e.Message.Trim()}");
            return 1;
        }
        finally
        {
            Console.SetError(err);
            File.Delete(cfg);
        }
    }

    private static List<string> Avertissements(string sortie)
    {
        var r = new List<string>();
        foreach (var l in sortie.Split('\n'))
        {
            if (l.StartsWith("cd_") || l.StartsWith("cdrom_") || l.StartsWith("zip_channel"))
                r.Add(l.Trim());
        }
        return r;
    }

    /// <summary>Le canal secondaire conduit : un reset logiciel (le rappel appelle atapi->stop()), puis, avec un
    /// disque, l'attention du changement consommée et READ(10) du PVD (la vitesse sert au minutage).</summary>
    private static string? Lecture(bool disque)
    {
        io.outb(0x376, 0x06);
        R9Atapi.Tranches(1);
        io.outb(0x376, 0x02);
        R9Atapi.Tranches(2);
        R9Atapi.Prepare();
        if (!R9Atapi.Commande(R9Atapi.Read10(16, 1), out var f))
            return $"READ(10) devait finir : {R9Atapi.Dit(f)}";
        if (disque)
            return (f.st & 1) == 0 && f.lus == 2048 ? null : $"ATTENDU 2 048 octets sans erreur : {R9Atapi.Dit(f)}";
        return (f.st & 1) != 0 && f.err == 0x24 ? null : $"ATTENDU NOT READY (erreur 24h) : {R9Atapi.Dit(f)}";
    }

    /// <summary>Le lecteur vide : le canal conduit d'abord (NOT READY à la lecture), puis le pilote sans disque posé et
    /// cdrom_drive ramené à -1.</summary>
    private static string? Vide()
    {
        var faute = Lecture(false);
        if (faute is not null)
            return faute;
        return ide_atapi.atapi == cdrom_null.null_atapi && cdrom_ioctl.cdrom_drive == -1
            ? null
            : $"ATTENDU le lecteur vide (cdrom_drive {cdrom_ioctl.cdrom_drive})";
    }
}
