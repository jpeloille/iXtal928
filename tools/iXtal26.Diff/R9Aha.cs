// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// r9-aha et r9-scsihd — les sites où PCem s'arrête ou déborde dans l'Adaptec AHA-1542C et ses disques SCSI
// (G11.2, PLAN-G11.md), en C# SEUL : l'oracle y rend la main à fatal() et sa suite est inexploitable, ou il
// déborde ses tampons.
//
// PB-135 (la carte) : :679 (les sept commandes 0Ch, 1Ch, 1Dh, 1Eh, 20h, 21h, 2Ah), :530 (un CCB d'opcode 1),
// :1743 (une CDB plus longue que son groupe), :980 (15h sur un ID vide après un sense de mailbox), :799,
// :841, :883 (02h, 03h, 04h sur un ID vide après un CHECK CONDITION) ; et la configuration : la ROM absente
// (:2083) et la carte sur un XT (wx-config.c:237). PB-128 (le disque) : :87 et :717 au-delà de 2 × 256 Ko,
// :728 et :101 au-delà de 256 Ko, et hdd_file.c:179 (un LBA de 2^31).
//
// Chaque essai sur une machine neuve : l'ami486, la carte, deux disques vierges écrits dans le TMPDIR (ID 0 et
// 1), le CMOS de référence — le POST s'arrête sur « Press F1 » AVANT le balayage des ROM d'extension, la carte
// est donc telle que son init l'a laissée, sans la ROM. L'outil la conduit par ses ports (334h-337h) et par la
// mémoire (mem_*_phys), entre des tranches. Chaque garde se signale à Diag.R9 : la porte exige que le site visé
// soit atteint et qu'aucune exception ne sorte.

using iXtal26.Diag;
using iXtal26.Memory;
using iXtal26.Scsi;

namespace iXtal26.Diff;

internal static class R9Aha
{
    private const ushort P = 0x334;
    private const uint Ccb = 0x80000, Mbox = 0x80100, Petit = 0x80200, Buf = 0x100000;

    private static int Essais;

    internal static int Run(string romsPath, bool disque)
    {
        var tmp = Path.Combine(Path.GetTempPath(), $"r9-aha-{Environment.ProcessId}");
        Directory.CreateDirectory(tmp);
        var cfg = Path.Combine(tmp, "r9.cfg");
        var bad = 0;
        try
        {
            for (var d = 0; d < 2; d++)
            {
                using var f = new FileStream(Path.Combine(tmp, $"d{d}.img"), FileMode.Create, FileAccess.Write);
                f.SetLength(20 * 64 * 32 * 512L);
            }
            File.WriteAllText(cfg, "model = ami486\ncpu = 10\nmem_size = 4096\ngfxcard = tvga9000b\nhdd_controller = aha1542c\n" +
                                   $"hdc_cylinders = 20\nhdc_heads = 64\nhdc_sectors = 32\nhdc_fn = {tmp}/d0.img\n" +
                                   $"hdd_cylinders = 20\nhdd_heads = 64\nhdd_sectors = 32\nhdd_fn = {tmp}/d1.img\n");
            if (!disque)
                bad += Carte(romsPath, cfg, tmp);
            else
                bad += Disque(romsPath, cfg);
        }
        catch (Exception e)
        {
            Console.WriteLine($"r9-{(disque ? "scsihd" : "aha")} : ARRÊT hors essai — {e.GetType().Name} : {e.Message.Trim()}");
            bad++;
        }
        finally
        {
            try { pc.closepc(); } catch (Exception) { }
            // Les liens vers roms/ d'abord, un par un (unlink) : un effacement récursif ne doit jamais les suivre.
            var sansRom = Path.Combine(tmp, "roms-sans-aha");
            if (Directory.Exists(sansRom))
            {
                foreach (var e in Directory.GetFileSystemEntries(sansRom))
                    File.Delete(e);
                Directory.Delete(sansRom);
            }
            Directory.Delete(tmp, true);
        }

        var nom = disque ? "r9-scsihd" : "r9-aha";
        Console.WriteLine(bad == 0
            ? $"{nom} : {Essais} essais, chaque garde atteinte, tout survit."
            : $"{nom} : {bad} essai(s) en défaut sur {Essais}.");
        return bad == 0 ? 0 : 1;
    }

    // ---- PB-135 : la carte et sa configuration ------------------------------------------------------
    private static int Carte(string romsPath, string cfg, string tmp)
    {
        var bad = 0;
        bad += Essai("scsi_aha1540.c:679", "les sept commandes 0Ch, 1Ch, 1Dh, 1Eh, 20h, 21h, 2Ah", romsPath, cfg, () =>
        {
            foreach (var c in new[] { 0x0C, 0x1C, 0x1D, 0x1E, 0x20, 0x21, 0x2A })
            {
                Cmd(c);
                var st = Hacc();
                if ((st & 0x01) == 0)
                    return $"{c:X2}h : INVDCMD attendu, état {st:X2}h";
            }
            return R9.Compte("scsi_aha1540.c:679") == 7 ? null : $"{R9.Compte("scsi_aha1540.c:679")} passages au lieu de 7";
        });
        bad += Essai("scsi_aha1540.c:530", "un CCB d'opcode 1 (le mode cible)", romsPath, cfg, () =>
        {
            var (hote, _, mbi) = Mailbox(CcbDe(1, 0, [0x00, 0, 0, 0, 0, 0], 0));
            return hote == 0x16 && mbi == 4 ? null : $"ATTENDU statut d'hôte 16h et MBI 04 : {hote:X2}h, MBI {mbi:X2}";
        });
        bad += Essai("scsi_aha1540.c:1743", "TEST UNIT READY déclaré sur 10 octets", romsPath, cfg, () =>
        {
            var (hote, cible, mbi) = Mailbox(CcbDe(0, 0, [0x00, 0, 0, 0, 0, 0, 0, 0, 0, 0], 0));
            return mbi is 1 or 4 ? null : $"le CCB devait s'achever : hôte {hote:X2}h, cible {cible:X2}h, MBI {mbi:X2}";
        });
        bad += Essai("scsi_aha1540.c:980", "15h sur l'ID 5, vide, après le sense d'un CCB de mailbox", romsPath, cfg, () =>
        {
            Mailbox(CcbDe(0, 0, [0x12, 0, 0, 0, 36, 0], 36));
            var (st, _) = Bios(0x15, 5);
            return (st & 0x01) != 0 ? null : $"INVDCMD attendu, état {st:X2}h";
        });
        foreach (var (site, f) in new[] { ("799", 0x02), ("841", 0x03), ("883", 0x04) })
            bad += Essai($"scsi_aha1540.c:{site}", $"{f:X2}h sur l'ID 5, vide, après un CHECK CONDITION", romsPath, cfg, () =>
            {
                var (_, cible, _) = Mailbox(CcbDe(0, 0, [0x12, 0x20, 0, 0, 36, 0], 36));
                if (cible != 2)
                    return $"le CCB devait rendre CHECK CONDITION : {cible:X2}h";
                var (_, donnee) = Bios(f, 5);
                return donnee == 0x20 ? null : $"l'erreur 20h attendue : {donnee:X2}h";
            });

        // La configuration : la ROM absente, la carte sur un XT. Aucune carte montée, et la machine amorce.
        var sansRom = Path.Combine(tmp, "roms-sans-aha");
        Directory.CreateDirectory(sansRom);
        foreach (var e in Directory.GetFileSystemEntries(romsPath))
        {
            if (Path.GetFileName(e) != "adaptec_aha1542c_bios_534201-00.bin")
                File.CreateSymbolicLink(Path.Combine(sansRom, Path.GetFileName(e)), Path.GetFullPath(e));
        }
        bad += Essai("scsi_aha1540.c:2083", "la ROM de la carte absente", sansRom, cfg, () =>
            scsi_aha1540.courante() is null ? null : "la carte ne devait pas être montée");
        var xt = Path.Combine(tmp, "xt.cfg");
        File.WriteAllText(xt, "model = ibmxt\nhdd_controller = aha1542c\n");
        bad += Essai("wx-config.c:237", "la carte sur un XT (8088)", romsPath, xt, () =>
            scsi_aha1540.courante() is null ? null : "la carte ne devait pas être montée");

        // Les configurations ramenées (décisions n° 2 et n° 11 de PLAN-G11.md ; config_hors_liste) : pas de site
        // R9, un état attendu.
        var conf = Path.Combine(tmp, "conf.cfg");
        File.WriteAllText(conf, File.ReadAllText(cfg) + "cdrom_channel = 2\nzip_channel = 3\n" +
                                $"hdg_fn = {tmp}/d1.img\n\n[Adaptec AHA-1542C (SCSI)]\naddr = 999\nbios_addr = 1\n");
        bad += Essai("", "cdrom_channel, zip_channel, addr et bios_addr hors liste, hdg_fn sans géométrie", romsPath, conf, () =>
        {
            var o = new ulong[scsi_aha1540.PROBE_N];
            scsi_aha1540.Probe(o);
            if (Ide.ide.cdrom_channel != -1 || Ide.ide.zip_channel != -1)
                return $"les canaux devaient valoir -1 : {Ide.ide.cdrom_channel}, {Ide.ide.zip_channel}";
            if (o[0] != 1 || o[42] != 1 || o[43] != 0xDC000)
                return $"la carte aux défauts (334h, DC000h) attendue : montée {o[0]}, interrupteurs {o[42]}, ROM {o[43]:X5}h";
            // L'ID 4 : un disque de capacité 0, comme chez PCem (hdd_load ouvre le fichier) — averti seulement.
            return o[63 + 4 * 11] == 1 ? null : "l'ID 4 devait être monté, de capacité 0, comme chez PCem";
        });
        return bad;
    }

    // ---- PB-128 : le disque ---------------------------------------------------------------------------
    private static int Disque(string romsPath, string cfg)
    {
        var bad = 0;
        bad += Essai("scsi_hd.c:87", "READ(10) de 1 100 secteurs : au-delà de data_in et data_out", romsPath, cfg, () =>
        {
            var (hote, cible, mbi) = Mailbox(CcbDe(0, 1, [0x28, 0, 0, 0, 0, 0, 0, 0x04, 0x4C, 0], 1100 * 512, Buf), 400);
            if (mbi == 0)
                return $"le CCB devait s'achever : hôte {hote:X2}h, cible {cible:X2}h";
            return R9.Compte("scsi_hd.c:717") > 0 ? null : "la relecture au-delà (scsi_hd.c:717) n'a pas été atteinte";
        });
        bad += Essai("scsi_hd.c:717", "READ(10) de compte 0, un CCB de 600 000 octets (PB-132 : le bus perdu)", romsPath, cfg, () =>
        {
            Mailbox(CcbDe(0, 1, [0x28, 0, 0, 0, 0, 0, 0, 0, 0, 0], 600000, Buf), 60);
            return null;
        });
        bad += Essai("scsi_hd.c:728", "WRITE(10) de 600 secteurs : au-delà de data_out", romsPath, cfg, () =>
        {
            var (hote, cible, mbi) = Mailbox(CcbDe(0, 1, [0x2A, 0, 0, 0, 0, 0, 0, 0x02, 0x58, 0], 600 * 512, Buf), 400);
            if (mbi == 0)
                return $"le CCB devait s'achever : hôte {hote:X2}h, cible {cible:X2}h";
            return R9.Compte("scsi_hd.c:101") > 0 ? null : "la lecture au-delà (scsi_hd.c:101) n'a pas été atteinte";
        });
        bad += Essai("scsi_hd.c:728", "MODE SELECT(6) de longueur 0, un CCB de 300 000 octets", romsPath, cfg, () =>
        {
            Mailbox(CcbDe(0, 1, [0x15, 0x10, 0, 0, 0, 0], 300000, Buf), 60);
            return null;
        });
        bad += Essai("hdd_file.c:179", "READ(10) du LBA 80000000h (un offset négatif)", romsPath, cfg, () =>
        {
            var (hote, cible, mbi) = Mailbox(CcbDe(0, 1, [0x28, 0, 0x80, 0, 0, 0, 0, 0, 1, 0], 512, Buf));
            return mbi != 0 ? null : $"le CCB devait s'achever : hôte {hote:X2}h, cible {cible:X2}h";
        });
        return bad;
    }

    /// <summary>Un scénario sur une machine neuve : `corps` rend null s'il est conforme ; le site doit avoir été atteint.</summary>
    private static int Essai(string site, string nom, string romsPath, string cfg, Func<string?> corps)
    {
        Essais++;
        try
        {
            if (!pc.loadconfig(cfg))
                throw new InvalidOperationException("loadconfig");
            Floppy.fdd_c.discfns[0] = Floppy.fdd_c.discfns[1] = "";
            R9.Raz();
            if (!pc.initpc(romsPath))
                throw new InvalidOperationException("initpc");
            R9Atapi.Tranches(300);
            var faute = corps();
            if (faute is null && site.Length != 0 && R9.Compte(site) == 0)
                faute = "la garde n'a pas été atteinte — l'essai ne prouve rien";
            Console.WriteLine($"  {(site.Length != 0 ? site : "configuration")} — {nom} : " +
                              (faute is null ? site.Length != 0 ? $"survit (garde atteinte {R9.Compte(site)} fois)" : "conforme"
                                             : $"EN DÉFAUT — {faute}"));
            pc.closepc();
            return faute is null ? 0 : 1;
        }
        catch (Exception e)
        {
            Console.WriteLine($"  {site} — {nom} : ARRÊT — {e.GetType().Name} : {e.Message.Trim()}");
            try { pc.closepc(); } catch (Exception) { }
            return 1;
        }
    }

    // ---- les ports et la mailbox -------------------------------------------------------------------
    private static byte Etat() => io.inb(P);

    private static byte Isr() => io.inb(P + 2);

    /// <summary>Un octet de commande ou de paramètre, CDF libre d'abord (au plus cent tranches).</summary>
    private static void Cmd(params int[] octets)
    {
        foreach (var o in octets)
        {
            for (var i = 0; i < 100 && (Etat() & 0x08) != 0; i++)
                pc.runpc();
            io.outb(P + 1, (byte)o);
        }
    }

    /// <summary>Attendre HACC (au plus cent tranches) ; l'état relevé, puis IRST.</summary>
    private static byte Hacc()
    {
        for (var i = 0; i < 100 && (Isr() & 0x04) == 0; i++)
            pc.runpc();
        var st = Etat();
        io.outb(P, 0x20);
        return st;
    }

    /// <summary>Un CCB de format 4 : opcode, ID, la CDB (sa longueur déclarée est la sienne), les données.</summary>
    private static byte[] CcbDe(int opcode, int id, byte[] cdb, int longueur, uint donnees = Petit)
    {
        var c = new byte[0x12 + cdb.Length + 14];
        c[0] = (byte)opcode;
        c[1] = (byte)(id << 5);
        c[2] = (byte)cdb.Length;
        c[4] = (byte)(longueur >> 16);
        c[5] = (byte)(longueur >> 8);
        c[6] = (byte)longueur;
        c[7] = (byte)(donnees >> 16);
        c[8] = (byte)(donnees >> 8);
        c[9] = (byte)donnees;
        cdb.CopyTo(c, 0x12);
        return c;
    }

    /// <summary>MAILBOX INIT (une MBO, une MBI), le CCB écrit, START, 02h ; puis au plus `tranches` tranches pour
    /// le MBI. Rend le statut d'hôte, le statut cible et le code du MBI (0 : rien).</summary>
    private static (byte hote, byte cible, byte mbi) Mailbox(byte[] ccb, int tranches = 100)
    {
        Cmd(0x01, 1, (int)(Mbox >> 16) & 0xFF, (int)(Mbox >> 8) & 0xFF, (int)Mbox & 0xFF);
        Hacc();
        for (var i = 0; i < ccb.Length; i++)
            mem.mem_writeb_phys(Ccb + (uint)i, ccb[i]);
        mem.mem_writeb_phys(Mbox + 1, (byte)(Ccb >> 16));
        mem.mem_writeb_phys(Mbox + 2, unchecked((byte)(Ccb >> 8)));
        mem.mem_writeb_phys(Mbox + 3, unchecked((byte)Ccb));
        mem.mem_writeb_phys(Mbox + 4, 0);
        mem.mem_writeb_phys(Mbox, 1);
        Cmd(0x02);
        for (var i = 0; i < tranches && (Isr() & 0x01) == 0; i++)
            pc.runpc();
        io.outb(P, 0x20);
        return (mem.mem_readb_phys(Ccb + 0xE), mem.mem_readb_phys(Ccb + 0xF), mem.mem_readb_phys(Mbox + 4));
    }

    /// <summary>La commande BIOS 03h, sous-fonction `f`, sur l'ID `id`, un secteur (cylindre 0, tête 0, secteur
    /// 1), les données en Petit ; HACC attendu. Rend l'état et l'octet de données.</summary>
    private static (byte st, byte donnee) Bios(int f, int id)
    {
        Cmd(0x03, f, id, 0, 0, 0, 1, 1, (int)(Petit >> 16) & 0xFF, (int)(Petit >> 8) & 0xFF, (int)Petit & 0xFF);
        for (var i = 0; i < 100 && (Isr() & 0x04) == 0; i++)
            pc.runpc();
        var st = Etat();
        var donnee = io.inb(P + 1);
        io.outb(P, 0x20);
        return (st, donnee);
    }
}
