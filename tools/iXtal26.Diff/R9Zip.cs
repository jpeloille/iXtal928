// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// r9-zip — les sites où PCem déborde ou s'arrête dans le lecteur ZIP (G10.6), en C# SEUL.
//
// PB-125 : data_in écrit sans borne (scsi_zip.c:209) et lu sans borne (:979) ; data_out lu au-delà (:222-224) et
// écrit au-delà (:985-990) ; le secteur hors de l'image, ou d'un LBA de 2^31 et plus, qui envoie une taille négative
// à fread et fwrite (hdd_file.c:179, :212). Chaque site est nommé, « fichier.c:ligne », et atteint par un scénario
// de l'invité : les ports de l'unité IDE 2 (170h-177h) conduits depuis l'outil, sur un ami486 amorcé, le lecteur ZIP
// chargé d'une image vierge de 100 663 296 octets écrite dans le TMPDIR — le BIOS ne touche pas ce canal (mesuré,
// VERIFICATION.md § G10.4), et nIEN coupe les IRQ du lecteur. Chaque garde se signale à Diag.R9 : la porte exige
// que le site visé ait été atteint et qu'aucune exception ne sorte ; une garde retirée la rougit, l'exception
// nommée. L'oracle n'est jamais appelé : il déborderait.

using iXtal26.Diag;
using iXtal26.Scsi;
using static iXtal26.Diff.R9Atapi;

namespace iXtal26.Diff;

internal static class R9Zip
{
    private const ushort Base = 0x170;
    private const ushort Ctl = 0x376;

    internal static int Run(string romsPath)
    {
        var img = Path.Combine(Path.GetTempPath(), $"r9-zip-{Environment.ProcessId}.img");
        var cfg = Path.Combine(Path.GetTempPath(), $"r9-zip-{Environment.ProcessId}.cfg");
        var bad = 0;
        try
        {
            using (var f = new FileStream(img, FileMode.Create, FileAccess.Write))
                f.SetLength(scsi_zip_c.ZIP_SECTORS * 512L);
            File.WriteAllText(cfg, "model = ami486\ncpu = 10\nmem_size = 4096\ngfxcard = tvga9000b\nhdd_controller = ide\n" +
                                   $"zip_channel = 2\nzip_path = {img}\n");

            bad += Essai("scsi_zip.c:209", "READ(10) de 600 secteurs : data_in débordé à l'écriture", romsPath, cfg, () =>
            {
                if (!Paquet(Read10(0, 600)))
                    return "le paquet devait partir";
                var lus = Donnees(100);
                var f = Fin(lus);
                return lus == 600 * 512 && (f.st & 1) == 0 ? null : $"ATTENDU 307 200 octets et GOOD : {Dit(f)}";
            });
            bad += Essai("scsi_zip.c:979", "READ(10) de longueur nulle : la lecture ne finit pas, l'invité insiste", romsPath, cfg, () =>
            {
                if (!Paquet(Read10(0, 0)))
                    return "le paquet devait partir";
                // La phase de données ne finit jamais (PB-126) : 300 000 octets lus, au-delà des 262 144 de data_in.
                var lus = 0;
                for (var b = 0; b < 8 && lus < 300000; b++)
                {
                    var n = Bloc();
                    if (n <= 0)
                        return $"un bloc DRQ était attendu après {lus} octets";
                    lus += n;
                }
                return Reprise();
            });
            bad += Essai("scsi_zip.c:990", "WRITE(10) de 600 secteurs : data_out débordé à l'écriture", romsPath, cfg, () =>
            {
                if (!Paquet(Cdb(0x2A, 0, 0, 0, 0, 0, 0, 600 >> 8, 600 & 0xFF)))
                    return "le paquet devait partir";
                var ecrits = Ecrire(0x3C, 100);
                var f = Fin(ecrits);
                return ecrits == 600 * 512 && (f.st & 1) == 0 ? null : $"ATTENDU 307 200 octets écrits et GOOD : {Dit(f)}";
            });
            bad += Essai("scsi_zip.c:224", "WRITE(10) de 600 secteurs : data_out lu au-delà par l'écriture des secteurs", romsPath, cfg, () =>
            {
                if (!Paquet(Cdb(0x2A, 0, 0, 0, 0, 0, 0, 600 >> 8, 600 & 0xFF)))
                    return "le paquet devait partir";
                var ecrits = Ecrire(0x3C, 100);
                var f = Fin(ecrits);
                return (f.st & 1) == 0 ? null : $"ATTENDU GOOD : {Dit(f)}";
            });
            bad += Essai("hdd_file.c:179", "READ(10) du secteur 196 609, au-delà de la fin", romsPath, cfg, () =>
                Commande(Read10(196609, 1), out var f) && (f.st & 1) == 0 && f.lus == 512 ? null : $"ATTENDU 512 octets et GOOD : {Dit(f)}");
            bad += Essai("hdd_file.c:179", "READ(10) du LBA 80000000h (un offset négatif)", romsPath, cfg, () =>
                Commande(Read10(unchecked((int)0x80000000), 1), out var f) && (f.st & 1) == 0 ? null : $"ATTENDU GOOD : {Dit(f)}");
            bad += Essai("hdd_file.c:212", "WRITE(10) du secteur 196 610, au-delà de la fin", romsPath, cfg, () =>
            {
                if (!Paquet(Cdb(0x2A, 0, 0, 0x03, 0x00, 0x02, 0, 0, 1)))
                    return "le paquet devait partir";
                var f = Fin(Ecrire(0xA5, 4));
                return (f.st & 1) == 0 && new FileInfo(img).Length == scsi_zip_c.ZIP_SECTORS * 512L
                    ? null : $"ATTENDU GOOD, l'image à sa taille : {Dit(f)}, {new FileInfo(img).Length} octets";
            });
        }
        catch (Exception e)
        {
            Console.WriteLine($"r9-zip : ARRÊT hors essai — {e.GetType().Name} : {e.Message.Trim()}");
            bad++;
        }
        finally
        {
            try { pc.closepc(); } catch (Exception) { }
            File.Delete(cfg);
            File.Delete(img);
        }

        Console.WriteLine(bad == 0
            ? $"r9-zip : {Essais} essais — six sites de PCem —, chaque garde atteinte, tout survit."
            : $"r9-zip : {bad} essai(s) en défaut sur {Essais}.");
        return bad == 0 ? 0 : 1;
    }

    private static int Essais;

    /// <summary>Un scénario de l'invité, sur une machine neuve où le disque ZIP vient d'être chargé : `corps` rend
    /// null s'il est conforme ; le site doit avoir été atteint.</summary>
    private static int Essai(string site, string nom, string romsPath, string cfg, Func<string?> corps)
    {
        Essais++;
        try
        {
            Amorce(romsPath, cfg);
            R9.Raz();
            var faute = corps();
            if (faute is null && R9.Compte(site) == 0)
                faute = "la garde n'a pas été atteinte — l'essai ne prouve rien";
            Console.WriteLine($"  {site} — {nom} : {(faute is null ? $"survit (garde atteinte {R9.Compte(site)} fois)" : $"EN DÉFAUT — {faute}")}");
            return faute is null ? 0 : 1;
        }
        catch (Exception e)
        {
            Console.WriteLine($"  {site} — {nom} : ARRÊT — {e.GetType().Name} : {e.Message.Trim()}");
            return 1;
        }
    }

    /// <summary>L'ami486 amorcé, le ZIP sur l'unité IDE 2 chargé à la fin d'initpc, cent tranches de POST, nIEN ;
    /// puis TEST UNIT READY et REQUEST SENSE consomment l'attention du disque chargé.</summary>
    private static void Amorce(string romsPath, string cfg)
    {
        if (!pc.loadconfig(cfg))
            throw new InvalidOperationException("loadconfig");
        Floppy.fdd_c.discfns[0] = Floppy.fdd_c.discfns[1] = "";
        for (var d = 0; d < 4; d++)
            Disc.hdd_c.ide_fn[d] = "";
        if (!pc.initpc(romsPath))
            throw new InvalidOperationException("initpc");
        if (scsi_zip_c.zip_loaded() == 0)
            throw new InvalidOperationException("le disque ZIP n'est pas chargé");
        Tranches(100);
        io.outb(Ctl, 0x02);
        Prepare();
    }

    /// <summary>La phase de données sortante, bloc DRQ par bloc DRQ, des mots `motif` ; rend les octets écrits.</summary>
    private static int Ecrire(int motif, int max)
    {
        var total = 0;
        for (var b = 0; b < max; b++)
        {
            if (!AttendreBsy() || (St() & 0x08) == 0)
                break;
            var n = io.inb((ushort)(Base + 4)) | (io.inb((ushort)(Base + 5)) << 8);
            if (n <= 0)
                break;
            for (var i = 0; i < (n + 1) / 2; i++)
                io.outw(Base, (ushort)(motif | (motif << 8)));
            total += n;
        }
        return total;
    }

    /// <summary>La reprise d'un pilote : DEVICE RESET, puis TEST UNIT READY, qui doit finir.</summary>
    private static string? Reprise()
    {
        Out(7, 0x08);
        Tranches(2);
        return Commande(Cdb(0x00), out var f) ? null : $"la reprise par DEVICE RESET devait rendre le lecteur : {Dit(f)}";
    }
}
