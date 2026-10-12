// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// r9-xebec — les dix-neuf sites où PCem s'arrête dans l'IBM Fixed Disk Adapter (Xebec), en C# SEUL (R9,
// TRANSCRIPTION.md ; PB-267 ; G13.7a, PLAN-G13.md) : l'oracle y rend la main à fatal() et sa suite est inexploitable.
//
// Chaque essai sur une machine neuve (XebecBanc : l'ibmxt, le type 16, l'UC arrêtée), par les ports de la carte :
// - le registre de données lu ou écrit hors de sa phase (:121, :131, :144, :174, :189, :203), après un reset de la carte
//   (321h) ou sans commande ; au-delà de sa longueur (:133, :176, :191), par un état forgé — l'invité n'y arrive pas ;
// - le rappel dans un état que la commande n'a pas (:365, :476, :555, :590, :634, :671) : une impulsion de sélection
//   (322h) entre le sixième octet du DCB et le rappel ; la carte répond ensuite à TEST DRIVE READY ;
// - le masque de DMA (323h) effacé entre deux temps d'un transfert (:438, :516, :626), le canal 3 du 8237 masqué pour
//   que le transfert attende : la carte finit par le registre de données, et les octets arrivent ;
// - une commande que PCem n'a pas (:711) : 02h, 07h, 0Dh, E5h, E6h et 40h rendent l'erreur 20h (Invalid Command), E3h
//   teste l'unité, FORMAT DRIVE (04h) efface de l'adresse donnée à la fin du disque (trois cylindres), READ DATA FROM
//   SECTOR BUFFER (0Eh) rend le tampon ;
// - l'unité 1 absente, après INITIALIZE DRIVE CHARACTERISTICS sur elle (hdd_file.c:178, :211, :248, un FILE nul) :
//   READ, WRITE, FORMAT TRACK et FORMAT DRIVE rendent 04h ; VERIFY, qui n'accède pas au fichier, reste celui de PCem.
// La porte exige que chaque site soit atteint, qu'aucune exception ne sorte et que la carte rende ce qui est attendu.

using iXtal26.Diag;

namespace iXtal26.Diff;

internal static class R9Xebec
{
    private static int Essais;

    internal static int Run(string roms)
    {
        var tmp = Path.Combine(Path.GetTempPath(), $"r9-xebec-{Environment.ProcessId}");
        Directory.CreateDirectory(tmp);
        var bad = 0;
        try
        {
            bad += Lecture(roms, tmp);
            bad += Ecriture(roms, tmp);
            bad += Rappels(roms, tmp);
            bad += SansDma(roms, tmp);
            bad += Commandes(roms, tmp);
            bad += Absente(roms, tmp);
        }
        catch (Exception e)
        {
            Console.WriteLine($"r9-xebec : ARRÊT hors essai — {e.GetType().Name} : {e.Message.Trim()}");
            bad++;
        }
        finally
        {
            try { pc.closepc(); } catch (Exception) { }
            Directory.Delete(tmp, true);
        }
        Console.WriteLine(bad == 0
            ? $"\nVert : r9-xebec, {Essais} essais, chaque garde atteinte, la carte répond, rien ne s'arrête (PB-267, R9)."
            : $"\nr9-xebec : {bad} essai(s) en défaut sur {Essais}.");
        return bad == 0 ? 0 : 1;
    }

    /// <summary>Un essai : la machine montée (et ses marques), `corps` rend null s'il est conforme ; chaque site doit
    /// avoir été atteint.</summary>
    private static int Essai(string roms, string tmp, string[] sites, string nom, Func<string?> corps,
                             params (int lba, byte octet)[] marques)
    {
        Essais++;
        try
        {
            XebecBanc.Monter(roms, tmp, XebecBanc.Image(tmp, "c.img", XebecBanc.Cyl, XebecBanc.Tetes, XebecBanc.Spt, marques));
            var faute = corps();
            foreach (var site in sites)
                if (faute is null && R9.Compte(site.Contains(".c:") ? site : $"mfm_xebec.c:{site}") == 0)
                    faute = $"la garde :{site} n'a pas été atteinte — l'essai ne prouve rien";
            if (faute is null && !XebecBanc.Arretee())
                faute = "l'UC a quitté sa boucle d'arrêt";
            Console.WriteLine($"  {string.Join(", ", sites.Select(s => $":{s}"))} — {nom} : " +
                              (faute is null ? "survit, conforme" : $"EN DÉFAUT — {faute}"));
            pc.closepc();
            return faute is null ? 0 : 1;
        }
        catch (Exception e)
        {
            Console.WriteLine($"  {string.Join(", ", sites.Select(s => $":{s}"))} — {nom} : ARRÊT — {e.GetType().Name} : {e.Message.Trim()}");
            try { pc.closepc(); } catch (Exception) { }
            return 1;
        }
    }

    /// <summary>Des tranches jusqu'à ce que l'état réponde (au plus cinquante).</summary>
    private static bool Attendre(Func<byte, bool> etat)
    {
        for (var i = 0; i < 50 && !etat(XebecBanc.Etat()); i++)
            pc.runpc();
        return etat(XebecBanc.Etat());
    }

    private static bool Phase(byte st, byte phase) =>
        (st & (XebecBanc.REQ | XebecBanc.CD | XebecBanc.IO)) == phase;

    /// <summary>La carte répond encore : TEST DRIVE READY de l'unité 0, octet de fin 00h.</summary>
    private static string? Repond()
    {
        var fin = XebecBanc.Echange([0x00, 0, 0, 0, 0, 0]).fin;
        return fin == 0 ? null : $"TEST DRIVE READY rend {fin:X2}h";
    }

    // ---- le registre de données lu ----------------------------------------------------------------------
    private static int Lecture(string roms, string tmp)
    {
        var bad = 0;
        bad += Essai(roms, tmp, ["121"], "l'octet de fin lu après un reset de la carte", () =>
        {
            XebecBanc.Dcb([0x00, 0, 0, 0, 0, 0]);
            if (!Attendre(st => Phase(st, XebecBanc.REQ | XebecBanc.CD | XebecBanc.IO)))
                return "pas de phase de fin";
            io.outb(XebecBanc.P + 1, 0);
            var v = io.inb(XebecBanc.P);
            return v == 0xFF ? Repond() : $"la lecture rend {v:X2}h au lieu de FFh";
        });
        bad += Essai(roms, tmp, ["131"], "une donnée lue après un reset de la carte", () =>
        {
            XebecBanc.Dcb([0x08, 0, 0, 0, 1, 5]);
            if (!Attendre(st => Phase(st, XebecBanc.REQ | XebecBanc.IO)))
                return "pas de phase de données";
            io.outb(XebecBanc.P + 1, 0);
            var v = io.inb(XebecBanc.P);
            return v == 0xFF ? Repond() : $"la lecture rend {v:X2}h au lieu de FFh";
        });
        bad += Essai(roms, tmp, ["133"], "une donnée lue au-delà de sa longueur (état forgé)", () =>
        {
            var x = XebecBanc.Carte;
            x.state = 5;
            x.status = XebecBanc.BSY | XebecBanc.IO | XebecBanc.REQ;
            x.data_pos = x.data_len = 4;
            var v = io.inb(XebecBanc.P);
            return v == 0xFF && x.data_pos == 4 ? Repond() : $"la lecture rend {v:X2}h, data_pos {x.data_pos}";
        });
        bad += Essai(roms, tmp, ["144"], "le registre de données lu sans commande", () =>
        {
            var v = io.inb(XebecBanc.P);
            return v == 0xFF ? Repond() : $"la lecture rend {v:X2}h au lieu de FFh";
        });
        return bad;
    }

    // ---- le registre de données écrit -------------------------------------------------------------------
    private static int Ecriture(string roms, string tmp)
    {
        var bad = 0;
        bad += Essai(roms, tmp, ["174"], "un octet de commande écrit après un reset de la carte", () =>
        {
            io.outb(XebecBanc.P + 2, 0);
            io.outb(XebecBanc.P + 1, 0);
            io.outb(XebecBanc.P, 0x00);
            return XebecBanc.Carte.command_pos == 0 ? Repond() : "l'octet a été pris";
        });
        bad += Essai(roms, tmp, ["176"], "un septième octet de commande (état forgé)", () =>
        {
            var x = XebecBanc.Carte;
            x.state = 1;
            x.status = XebecBanc.BSY | XebecBanc.CD | XebecBanc.REQ;
            x.command_pos = 6;
            io.outb(XebecBanc.P, 0x55);
            return x.command_pos == 6 ? Repond() : $"command_pos {x.command_pos}";
        });
        bad += Essai(roms, tmp, ["189"], "une donnée écrite après un reset de la carte", () =>
        {
            XebecBanc.Dcb([0x0A, 0, 0, 0, 1, 5]);
            if (!Attendre(st => Phase(st, XebecBanc.REQ)))
                return "pas de phase de données";
            io.outb(XebecBanc.P + 1, 0);
            io.outb(XebecBanc.P, 0x55);
            return XebecBanc.Carte.data_pos == 0 ? Repond() : "l'octet a été pris";
        });
        bad += Essai(roms, tmp, ["191"], "une donnée écrite au-delà de sa longueur (état forgé)", () =>
        {
            var x = XebecBanc.Carte;
            x.state = 3;
            x.status = XebecBanc.BSY | XebecBanc.REQ;
            x.data_pos = x.data_len = 8;
            io.outb(XebecBanc.P, 0x55);
            return x.data_pos == 8 ? Repond() : $"data_pos {x.data_pos}";
        });
        bad += Essai(roms, tmp, ["203"], "le registre de données écrit sans commande", () =>
        {
            io.outb(XebecBanc.P, 0x55);
            return Repond();
        });
        return bad;
    }

    // ---- le rappel dans un état que la commande n'a pas ---------------------------------------------------
    private static int Rappels(string roms, string tmp)
    {
        var bad = 0;
        foreach (var (site, op, nom) in new[] { ("365", 0x05, "VERIFY"), ("476", 0x08, "READ"), ("555", 0x0A, "WRITE"),
                                                ("590", 0x0C, "INITIALIZE DRIVE CHARACTERISTICS"),
                                                ("634", 0x0F, "WRITE DATA TO SECTOR BUFFER"),
                                                ("671", 0xFB, "GET DRIVE PARAMS du DTC") })
            bad += Essai(roms, tmp, [site], $"{nom}, une impulsion de sélection avant son rappel", () =>
            {
                XebecBanc.Dcb([op, 0, 0, 0, 1, 5]);
                io.outb(XebecBanc.P + 2, 0);
                for (var i = 0; i < 3; i++)
                    pc.runpc();
                return Repond();
            });
        return bad;
    }

    // ---- le masque de DMA effacé en cours de transfert ------------------------------------------------------
    private static int SansDma(string roms, string tmp)
    {
        var bad = 0;
        // Le canal 3 du 8237 masqué (0Ah) : le transfert par DMA attend ; puis la carte sans DMA (323h à 0).
        static void DmaPuisRien(int[] dcb)
        {
            io.outb(0x0A, 0x07);
            io.outb(XebecBanc.P + 3, 0x01);
            XebecBanc.Dcb(dcb);
            for (var i = 0; i < 3; i++)
                pc.runpc();
            io.outb(XebecBanc.P + 3, 0x00);
            pc.runpc();
        }
        bad += Essai(roms, tmp, ["438"], "READ, le DMA coupé avant le transfert", () =>
        {
            DmaPuisRien([0x08, 0, 3, 1, 1, 5]);
            var (fin, lus) = XebecBanc.Suite();
            return fin == 0 && lus.Length == 512 && lus.All(o => o == 0x3C) ? Repond()
                : $"fin {fin:X2}h, {lus.Length} octets";
        }, (XebecBanc.Lba(1, 0, 3), 0x3C));
        bad += Essai(roms, tmp, ["516"], "WRITE, le DMA coupé avant le transfert", () =>
        {
            DmaPuisRien([0x0A, 0, 4, 1, 1, 5]);
            var fin = XebecBanc.Suite(Enumerable.Repeat((byte)0x77, 512).ToArray()).fin;
            var (relu, lus) = XebecBanc.Lire(1, 0, 4);
            return fin == 0 && relu == 0 && lus.All(o => o == 0x77) ? Repond()
                : $"fin {fin:X2}h, relu {relu:X2}h, premier octet {(lus.Length > 0 ? lus[0] : -1):X2}h";
        });
        bad += Essai(roms, tmp, ["626", "711"], "WRITE DATA TO SECTOR BUFFER, le DMA coupé ; puis READ DATA FROM SECTOR BUFFER (0Eh)", () =>
        {
            DmaPuisRien([0x0F, 0, 0, 0, 0, 0]);
            var fin = XebecBanc.Suite(Enumerable.Repeat((byte)0x66, 512).ToArray()).fin;
            var (finLu, lus) = XebecBanc.Echange([0x0E, 0, 0, 0, 0, 0]);
            return fin == 0 && finLu == 0 && lus.Length == 512 && lus.All(o => o == 0x66) ? Repond()
                : $"fin {fin:X2}h, 0Eh : fin {finLu:X2}h, {lus.Length} octets";
        });
        return bad;
    }

    // ---- les commandes que PCem n'a pas -----------------------------------------------------------------
    private static int Commandes(string roms, string tmp)
    {
        var bad = 0;
        foreach (var op in new[] { 0x02, 0x07, 0x0D, 0xE5, 0xE6, 0x40 })
            bad += Essai(roms, tmp, ["711"], $"{op:X2}h : Invalid Command", () =>
            {
                var fin = XebecBanc.Echange([op, 0, 0, 0, 1, 5]).fin;
                var s = XebecBanc.Sense();
                return (fin & 0x02) != 0 && (s[0] & 0x3F) == 0x20 ? Repond() : $"fin {fin:X2}h, sense {s[0]:X2}h";
            });
        bad += Essai(roms, tmp, ["711"], "E3h, DRIVE DIAGNOSTIC des unités 0 et 1 (absente)", () =>
        {
            var f0 = XebecBanc.Echange([0xE3, 0, 0, 0, 0, 0]).fin;
            var f1 = XebecBanc.Echange([0xE3, 0x20, 0, 0, 0, 0]).fin;
            var s = XebecBanc.Sense(1);
            return f0 == 0 && (f1 & 0x02) != 0 && (s[0] & 0x3F) == 0x04 ? Repond()
                : $"unité 0 : {f0:X2}h, unité 1 : {f1:X2}h, sense {s[0]:X2}h";
        });
        // Depuis (609, 3) : la fin de son cylindre, puis les cylindres 610 et 611 entiers ; (609, 2, 16), juste avant le
        // départ, reste intact.
        var depart = XebecBanc.Cyl - 3;
        (int c, int h, int s)[] efface = [(depart, 3, 0), (depart + 1, 0, 0), (depart + 1, 3, 16), (depart + 2, 2, 5),
                                          (XebecBanc.Cyl - 1, 3, 16)];
        bad += Essai(roms, tmp, ["711"], $"04h, FORMAT DRIVE depuis ({depart}, 3), jusqu'à la fin du disque", () =>
        {
            var fin = XebecBanc.Echange([0x04, 3, (depart >> 2) & 0xC0, depart & 0xFF, 1, 5]).fin;
            var avant = XebecBanc.Lire(depart, 2, 16).lus;
            var faux = efface.Where(a => !XebecBanc.Lire(a.c, a.h, a.s).lus.All(o => o == 0)).ToArray();
            return fin == 0 && avant.All(o => o == 0xEE) && faux.Length == 0 ? Repond()
                : $"fin {fin:X2}h ; ({depart}, 2, 16) {avant.FirstOrDefault():X2}h ; non effacés : " +
                  string.Join(" ", faux.Select(a => $"({a.c}, {a.h}, {a.s})"));
        }, efface.Select(a => (XebecBanc.Lba(a.c, a.h, a.s), (byte)0xEE)).Append((XebecBanc.Lba(depart, 2, 16), (byte)0xEE)).ToArray());
        return bad;
    }

    // ---- l'unité 1 absente, après INITIALIZE DRIVE CHARACTERISTICS sur elle -------------------------------------
    // READ, WRITE, FORMAT TRACK et FORMAT DRIVE atteignaient hdd_file sur un FILE nul : l'erreur 04h (Not Ready).
    // VERIFY, qui n'accède pas au fichier, reste celui de PCem : octet de fin 00h.
    private static int Absente(string roms, string tmp)
    {
        var bad = 0;
        foreach (var (nom, dcb) in new (string, int[])[] { ("READ", [0x08, 0x20, 0, 0, 1, 5]),
                                                             ("WRITE", [0x0A, 0x20, 0, 0, 1, 5]),
                                                             ("FORMAT TRACK", [0x06, 0x20, 0, 0, 1, 5]),
                                                             ("FORMAT DRIVE", [0x04, 0x20, 0, 0, 1, 5]) })
            bad += Essai(roms, tmp, ["hdd_file.c:178"], $"{nom} sur l'unité 1 absente, initialisée", () =>
            {
                XebecBanc.Echange([0x0C, 0x20, 0, 0, 0, 0], [XebecBanc.Cyl >> 8, XebecBanc.Cyl & 0xFF, XebecBanc.Tetes, 0, 0, 0, 0, 11]);
                var fin = XebecBanc.Echange(dcb, nom == "WRITE" ? Enumerable.Repeat(0x55, 512).ToArray() : null).fin;
                var s = XebecBanc.Sense(1);
                return (fin & 0x02) != 0 && (s[0] & 0x3F) == 0x04 ? Repond() : $"fin {fin:X2}h, sense {s[0]:X2}h";
            });
        bad += Essai(roms, tmp, [], "VERIFY sur l'unité 1 absente, initialisée (celui de PCem)", () =>
        {
            XebecBanc.Echange([0x0C, 0x20, 0, 0, 0, 0], [XebecBanc.Cyl >> 8, XebecBanc.Cyl & 0xFF, XebecBanc.Tetes, 0, 0, 0, 0, 11]);
            var fin = XebecBanc.Echange([0x05, 0x20, 0, 0, 1, 5]).fin;
            return (fin & 0x02) == 0 && R9.Compte("hdd_file.c:178") == 0 ? Repond() : $"fin {fin:X2}h";
        });
        return bad;
    }
}
