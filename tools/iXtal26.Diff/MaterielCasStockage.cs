// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — outillage de vérification.
//
// Les cas qui discriminent les corrections du stockage (G13.7a, PLAN-G13.md), en C# seul : la disquette (PB-14, le
// débit de 300 kbit/s ; PB-19, l'écrasement en écriture hors DMA) et le disque MFM (PB-22, PB-23, PB-25, PB-217). Chacun
// sur une machine neuve dont l'UC reste arrêtée (XebecBanc) : le BIOS ne touche ni au contrôleur ni à l'horloge-octet,
// l'outil les conduit par leurs ports, et chaque attendu porte sa source.

using iXtal26.Disc;

namespace iXtal26.Diff;

internal static partial class MaterielCas
{
    /// <summary>Un cas sur une machine neuve, dans un dossier du TMPDIR effacé ensuite.</summary>
    private static int Avec(Func<string, int> corps)
    {
        var tmp = Path.Combine(Path.GetTempPath(), $"materiel-cas-{Environment.ProcessId}");
        Directory.CreateDirectory(tmp);
        try
        {
            return corps(tmp);
        }
        catch (Exception e)
        {
            Console.WriteLine($"  [ECHEC] ARRÊT — {e.GetType().Name} : {e.Message.Trim()}");
            return 1;
        }
        finally
        {
            try { pc.closepc(); } catch (Exception) { }
            Directory.Delete(tmp, true);
        }
    }

    private static string Cfg(string dossier, string texte)
    {
        var chemin = Path.Combine(dossier, "cas.cfg");
        File.WriteAllText(chemin, texte);
        return chemin;
    }

    private static int VoirArret() =>
        XebecBanc.Arretee() ? 0 : Voir("l'UC reste arrêtée", false, "elle a quitté 0000:0500", "CS:IP = 0000:0500", true);

    // PB-14 — à 300 kbit/s (CCR = 01h ; IBM PC AT Technical Reference, 1502494, « Fixed Disk and Diskette Drive
    // Adapter », Data Rates, et le listing du BIOS, « 01: 300 KBS »), l'octet dure 8 / 0,3 = 80/3 µs. PCem fait
    // retomber le cas 1 de disc_set_rate dans le cas 2 : 32 µs, le pas de 250 kbit/s. L'avance d'un appel de disc_poll,
    // au format 32:32 de TIMER_USEC ; les débits 0 (16 µs) et 2 (32 µs) en témoins, égaux dans les deux modes.
    private static int Pb14(bool hw) => Avec(tmp =>
    {
        XebecBanc.Demarrer("roms", Cfg(tmp, "model = ibmat\nmem_size = 640\ngfxcard = cga\n"));
        var u = timer.TIMER_USEC;
        var bad = 0;
        foreach (var (rate, attendu, nom) in new[] { (1, hw ? 80 * u / 3 : 32 * u, hw ? "80/3 µs" : "32 µs"),
                                                     (0, 16 * u, "16 µs"), (2, 32 * u, "32 µs") })
        {
            disc.disc_set_rate(0, 0, rate);
            var t = disc.disc_poll_timer;
            var avant = ((ulong)t.ts_integer << 32) | t.ts_frac;
            disc.disc_poll(null);
            var pas = (((ulong)t.ts_integer << 32) | t.ts_frac) - avant;
            // Une borne indépendante du calcul du code : 8 / 0,3 = 26,6667 µs à 300 kbit/s.
            var reel = hw && rate == 1 ? 8 / 0.3 : rate == 0 ? 16 : 32;
            bad += Voir($"disc_set_rate(débit {rate}), un appel de disc_poll",
                        pas == attendu && Math.Abs(pas / (double)u - reel) < 1e-4,
                        $"{pas / (double)u:F4} µs", nom, hw);
        }
        return bad;
    });

    // PB-19 — en écriture hors DMA, le µPD765 attend chaque octet dans son temps, faute de quoi il pose OR dans ST1 (bit
    // 4) et termine la commande (NEC µPD765 Data Sheet, déc. 1978, WRITE DATA). PC, une 360 Ko en A:, SPECIFY ND = 1,
    // WRITE DATA du secteur (0, 0, 1) ; l'UC ne fournit qu'un octet à la demande du contrôleur, puis plus rien. PCem
    // écrit l'octet périmé et ne pose pas OR ; le contrôleur pose OR à l'octet suivant, ST1 = 10h.
    private static int Pb19(bool hw) => Avec(tmp =>
    {
        var a = Path.Combine(tmp, "a.img");
        File.WriteAllBytes(a, new byte[368640]);
        XebecBanc.Demarrer("roms", Cfg(tmp, "model = ibmpc\nmem_size = 640\ngfxcard = cga\n"), a);
        io.outb(0x3F2, 0x1C);
        for (var i = 0; i < 5; i++)
            pc.runpc();
        void Octets(params int[] octets)
        {
            foreach (var o in octets)
            {
                if ((io.inb(0x3F4) & 0xC0) != 0x80)
                    throw new InvalidOperationException($"le contrôleur ne demande pas d'octet : MSR {io.inb(0x3F4):X2}h");
                io.outb(0x3F5, (byte)o);
            }
        }
        Octets(0x03, 0xDF, 0x03);
        Octets(0x45, 0x00, 0x00, 0x00, 0x01, 0x02, 0x01, 0x2A, 0xFF);
        var donnes = 0;
        for (var i = 0; i < 200000 && (io.inb(0x3F4) & 0xC0) != 0xC0; i++)
        {
            if ((io.inb(0x3F4) & 0xE0) == 0xA0 && donnes < 1)
            {
                io.outb(0x3F5, 0x5A);
                donnes++;
            }
            disc.disc_poll(null);
            // La fin de la commande passe par le chronomètre du contrôleur (fdc.timer) : une tranche, de loin en loin,
            // longtemps après l'octet fourni.
            if (i % 1000 == 999)
                pc.runpc();
        }
        if ((io.inb(0x3F4) & 0xC0) != 0xC0)
            return Voir("WRITE DATA, un seul octet fourni", false, "pas de phase de résultat", "la phase de résultat", hw);
        var res = new byte[7];
        for (var i = 0; i < 7; i++)
            res[i] = io.inb(0x3F5);
        return Voir("WRITE DATA hors DMA, un seul octet fourni ; ST0, ST1", hw ? res[1] == 0x10 : (res[1] & 0x10) == 0,
                    $"ST0 = {res[0]:X2}h, ST1 = {res[1]:X2}h", hw ? "ST1 = 10h (OR)" : "ST1 sans OR", hw) + VoirArret();
    });

    // PB-22 — FORMAT TRACK (06h) porte un secteur nul dans son DCB : la piste désignée se formate depuis son début (IBM
    // Fixed Disk Adapter, 6361503, p. 11). XT, Xebec, type 16 : READ du secteur (1, 0, 5) — le secteur courant passe à 6
    // —, puis FORMAT TRACK (1, 0). PCem formate depuis le secteur 6 : (1, 0, 0) reste intact, (1, 1, 0) est effacé ; la
    // carte efface (1, 0, 0) et laisse (1, 1, 0).
    private static int Pb22(bool hw) => Avec(tmp =>
    {
        var marques = Enumerable.Range(0, 17).Select(s => (XebecBanc.Lba(1, 0, s), (byte)0xAA))
                                .Concat(Enumerable.Range(0, 6).Select(s => (XebecBanc.Lba(1, 1, s), (byte)0xAA))).ToArray();
        XebecBanc.Monter("roms", tmp, XebecBanc.Image(tmp, "c.img", XebecBanc.Cyl, XebecBanc.Tetes, XebecBanc.Spt, marques));
        var bad = 0;
        var (fin, lus) = XebecBanc.Lire(1, 0, 5);
        bad += Voir("READ (1, 0, 5)", fin == 0 && lus.Length == 512 && lus[0] == 0xAA, $"fin {fin:X2}h, {lus.Length} octets",
                    "fin 00h, 512 octets AAh", hw);
        (fin, _) = XebecBanc.Echange([0x06, 0x00, 0x00, 0x01, 0x01, 0x05]);
        bad += Voir("FORMAT TRACK (1, 0)", fin == 0, $"fin {fin:X2}h", "fin 00h", hw);
        var debut = XebecBanc.Lire(1, 0, 0).lus;
        var suivante = XebecBanc.Lire(1, 1, 0).lus;
        bad += Voir("puis (1, 0, 0) et (1, 1, 0)", debut.Length == 512 && suivante.Length == 512 &&
                    (debut[0], suivante[0]) == (hw ? ((byte)0x00, (byte)0xAA) : ((byte)0xAA, (byte)0x00)),
                    $"{(debut.Length > 0 ? debut[0] : -1):X2}h, {(suivante.Length > 0 ? suivante[0] : -1):X2}h",
                    hw ? "00h (effacé), AAh (intact)" : "AAh (intact), 00h (effacé)", hw);
        return bad + VoirArret();
    });

    // PB-23 — l'octet de fin porte en bit 5 l'unité de la commande (IBM Fixed Disk Adapter, Status Register, p. 3). XT,
    // Xebec, l'unité 1 absente : TEST DRIVE READY (00h) vers l'unité 1, octet de fin 02h chez PCem, 22h sur la carte ;
    // vers l'unité 0, 00h des deux côtés. Puis l'unité 1 présente : TEST DRIVE READY réussi, 00h chez PCem, 20h sur la
    // carte.
    private static int Pb23(bool hw)
    {
        var bad = Pb23Absente(hw);
        bad += Avec(tmp =>
        {
            XebecBanc.Monter("roms", tmp, XebecBanc.Image(tmp, "c.img", XebecBanc.Cyl, XebecBanc.Tetes, XebecBanc.Spt),
                             XebecBanc.Image(tmp, "d.img", XebecBanc.Cyl, XebecBanc.Tetes, XebecBanc.Spt));
            var fin = XebecBanc.Echange([0x00, 0x20, 0, 0, 0, 0]).fin;
            return Voir("TEST DRIVE READY, unité 1 présente", fin == (hw ? 0x20 : 0x00), $"fin {fin:X2}h",
                        hw ? "20h" : "00h", hw) + VoirArret();
        });
        return bad;
    }

    private static int Pb23Absente(bool hw) => Avec(tmp =>
    {
        XebecBanc.Monter("roms", tmp, XebecBanc.Image(tmp, "c.img", XebecBanc.Cyl, XebecBanc.Tetes, XebecBanc.Spt));
        var bad = 0;
        var fin = XebecBanc.Echange([0x00, 0x20, 0, 0, 0, 0]).fin;
        bad += Voir("TEST DRIVE READY, unité 1 absente", fin == (hw ? 0x22 : 0x02), $"fin {fin:X2}h",
                    hw ? "22h" : "02h", hw);
        fin = XebecBanc.Echange([0x00, 0x00, 0, 0, 0, 0]).fin;
        bad += Voir("TEST DRIVE READY, unité 0", fin == 0x00, $"fin {fin:X2}h", "00h", hw);
        return bad + VoirArret();
    });

    // PB-25 — les têtes se numérotent depuis 0 : la tête égale au nombre de têtes est hors du disque. Xebec (IBM Fixed
    // Disk Adapter, p. 6, l'erreur 21h « Illegal Disk Address ») : READ à la tête 4 d'un disque de 4 têtes, PCem lit
    // (1, 0, 0), octet de fin 00h ; la carte rend 02h et le sense 21h. AT (le WD1003, déduit : aucun champ ID sur une tête
    // absente, IDNF) : SET PARAMETERS à 4 têtes, READ SECTORS (20h) à la tête 4 : PCem lit (1, 0, 1) sans erreur ; le
    // contrôleur, ERR et l'erreur 10h.
    private static int Pb25(bool hw)
    {
        var bad = Avec(tmp =>
        {
            XebecBanc.Monter("roms", tmp, XebecBanc.Image(tmp, "c.img", XebecBanc.Cyl, XebecBanc.Tetes, XebecBanc.Spt,
                                                          (XebecBanc.Lba(1, 0, 0), 0x5A)));
            var (fin, lus) = XebecBanc.Lire(0, 4, 0);
            var sense = XebecBanc.Sense();
            return Voir("Xebec, READ (0, 4, 0) sur 4 têtes ; l'octet de fin, le sense",
                        hw ? fin == 0x02 && (sense[0] & 0x3F) == 0x21 : fin == 0x00 && lus.Length == 512 && lus[0] == 0x5A,
                        $"fin {fin:X2}h, {lus.Length} octets, sense {sense[0]:X2}h",
                        hw ? "fin 02h, sense 21h" : "fin 00h, le secteur (1, 0, 0)", hw) + VoirArret();
        });
        bad += Avec(tmp =>
        {
            var c = XebecBanc.Image(tmp, "c.img", 306, 4, 17, ((1 * 4 + 0) * 17 + 0, 0x5A));
            XebecBanc.Demarrer("roms", Cfg(tmp, "model = ibmat\nmem_size = 640\ngfxcard = cga\nhdd_controller = mfm_at\n" +
                                                $"hdc_cylinders = 306\nhdc_heads = 4\nhdc_sectors = 17\nhdc_fn = {c}\n"));
            byte Attendre()
            {
                for (var i = 0; i < 50 && (io.inb(0x1F7) & 0x80) != 0; i++)
                    pc.runpc();
                return io.inb(0x1F7);
            }
            io.outb(0x1F6, 0xA3);
            io.outb(0x1F2, 17);
            io.outb(0x1F7, 0x91);
            Attendre();
            io.outb(0x1F2, 1);
            io.outb(0x1F3, 1);
            io.outb(0x1F4, 0);
            io.outb(0x1F5, 0);
            io.outb(0x1F6, 0xA4);
            io.outb(0x1F7, 0x20);
            var st = Attendre();
            var err = io.inb(0x1F1);
            var mot = (st & 0x08) != 0 ? io.inw(0x1F0) : 0;
            return Voir("AT, SET PARAMETERS à 4 têtes, READ SECTORS (0, 4, 1) ; l'état, l'erreur",
                        hw ? (st & 0x01) != 0 && err == 0x10 : (st & 0x09) == 0x08 && mot == 0x5A5A,
                        $"état {st:X2}h, erreur {err:X2}h, premier mot {mot:X4}h",
                        hw ? "ERR, erreur 10h (IDNF)" : "DRQ, le secteur (1, 0, 1)", hw) + VoirArret();
        });
        return bad;
    }

    // PB-217 — le sense (IBM Fixed Disk Adapter, Sense Bytes, p. 4) : octet 0, le bit 7 « address valid » quand la
    // commande précédente visait une adresse, puis l'erreur ; octet 1, l'unité et la tête ; octet 2, le haut du cylindre
    // et le secteur ; octet 3, le bas du cylindre. XT, Xebec : READ au cylindre 123h, tête 2, secteur 20 (au-delà des
    // 17) : octet de fin 02h ; le sense, PCem 21 00 00 00, la carte A1 02 54 23 ; un second sense, 00 00 00 00 des deux
    // côtés (REQUEST SENSE ne vise pas d'adresse) ; un SEEK réussi au cylindre 210h, tête 1, puis le sense : PCem
    // 00 00 00 00, la carte 80 01 80 10 (l'adresse valide, sans erreur).
    private static int Pb217(bool hw) => Avec(tmp =>
    {
        XebecBanc.Monter("roms", tmp, XebecBanc.Image(tmp, "c.img", XebecBanc.Cyl, XebecBanc.Tetes, XebecBanc.Spt));
        var bad = 0;
        var fin = XebecBanc.Lire(0x123, 2, 20).fin;
        bad += Voir("READ (123h, 2, 20)", fin == 0x02, $"fin {fin:X2}h", "02h", hw);
        var s = XebecBanc.Sense();
        byte[] attendu = hw ? [0xA1, 0x02, 0x54, 0x23] : [0x21, 0x00, 0x00, 0x00];
        bad += Voir("le sense", s.SequenceEqual(attendu), string.Join(" ", s.Select(o => o.ToString("X2"))),
                    string.Join(" ", attendu.Select(o => o.ToString("X2"))), hw);
        s = XebecBanc.Sense();
        bad += Voir("un second sense", s.SequenceEqual(new byte[4]), string.Join(" ", s.Select(o => o.ToString("X2"))),
                    "00 00 00 00", hw);
        // SEEK (0Bh) réussi au cylindre 210h, tête 1 : la commande visait une adresse, le sense la rend sans erreur.
        fin = XebecBanc.Echange([0x0B, 0x01, 0x80, 0x10, 0, 0x05]).fin;
        s = XebecBanc.Sense();
        attendu = hw ? [0x80, 0x01, 0x80, 0x10] : [0x00, 0x00, 0x00, 0x00];
        bad += Voir("SEEK (210h, 1), puis le sense", fin == 0 && s.SequenceEqual(attendu),
                    $"fin {fin:X2}h, {string.Join(" ", s.Select(o => o.ToString("X2")))}",
                    $"fin 00h, {string.Join(" ", attendu.Select(o => o.ToString("X2")))}", hw);
        return bad + VoirArret();
    });

    // ===== L'IDE (G13.7b) =====

    /// <summary>L'ibmat et son IDE, un disque de 20 × 4 × 17 en C:, l'UC arrêtée.</summary>
    private static void MonterIde(string tmp)
    {
        var c = XebecBanc.Image(tmp, "c.img", 20, 4, 17);
        XebecBanc.Demarrer("roms", Cfg(tmp, "model = ibmat\ndrive_a_type = 5\ndrive_b_type = 2\nhdd_controller = ide\n" +
                                            $"hdc_cylinders = 20\nhdc_heads = 4\nhdc_sectors = 17\nhdc_fn = {c}\n"));
    }

    private static byte IdeAttendre(ushort b = 0x1F0)
    {
        for (var i = 0; i < 50 && (io.inb((ushort)(b + 7)) & 0x80) != 0; i++)
            pc.runpc();
        return io.inb((ushort)(b + 7));
    }

    // PB-71 — chaque canal a sa ligne (ATA-1 r4 § 6.3.10 ; l'IRQ 15 du secondaire, ide.c:118-136). L'unité maîtresse
    // du canal secondaire a levé l'IRQ 15 ; une IRQ 14 attend au PIC esclave ; une écriture de 176h (la même unité) :
    // PCem efface la demande de l'IRQ 15 (le bit 7 de pic2.pend), le matériel la garde.
    private static int Pb71(bool hw) => Avec(tmp =>
    {
        MonterIde(tmp);
        io.outb(0x176, 0xA0);
        Ide.ide.ide_irq_raise(Ide.ide.ide_drives[2]);
        Models.pic.picint(1 << 14);
        io.outb(0x176, 0xA0);
        var garde = (Models.pic.pic2.pend & 0x80) != 0;
        var bad = Voir("IRQ 15 levée par le secondaire, IRQ 14 en attente, OUT 176h,A0h ; la demande de l'IRQ 15",
                       garde == hw, garde ? "gardée" : "perdue", hw ? "gardée" : "perdue", hw);
        // Puis nIEN posé sur le secondaire (OUT 376h,02h) : sa ligne retombe, la demande de l'IRQ 15 se retire, celle de
        // l'IRQ 14 reste — dans les deux modes.
        io.outb(0x376, 0x02);
        var (i15, i14) = ((Models.pic.pic2.pend & 0x80) != 0, (Models.pic.pic2.pend & 0x40) != 0);
        bad += Voir("puis OUT 376h,02h (nIEN) ; les demandes des IRQ 15 et 14", !i15 && i14,
                    $"IRQ 15 {(i15 ? "posée" : "retirée")}, IRQ 14 {(i14 ? "posée" : "retirée")}", "IRQ 15 retirée, IRQ 14 posée", hw);
        return bad + VoirArret();
    });

    // PB-216 — INTRQ est actif tant que l'unité choisie a une interruption en attente et que nIEN vaut 0 (ATA-1 r4
    // § 6.3.10) ; le 8259A de l'AT ne prend que les fronts. Unité 0 du primaire, son interruption levée : OUT 1F6h,A0h
    // (la même unité), PCem retire la demande (le bit 6 de pic2.pend), le matériel la garde. Puis la demande servie (le
    // bit effacé, comme l'acquittement), l'état non lu : un second OUT 1F6h,A0h, PCem pose une seconde demande, le
    // matériel aucune (pas de front).
    private static int Pb216(bool hw) => Avec(tmp =>
    {
        MonterIde(tmp);
        var bad = 0;
        io.outb(0x1F6, 0xA0);
        Ide.ide.ide_irq_raise(Ide.ide.ide_drives[0]);
        io.outb(0x1F6, 0xA0);
        var garde = (Models.pic.pic2.pend & 0x40) != 0;
        bad += Voir("l'interruption levée, OUT 1F6h,A0h ; la demande de l'IRQ 14", garde == hw, garde ? "gardée" : "perdue",
                    hw ? "gardée" : "perdue", hw);
        Models.pic.pic2.pend &= unchecked((byte)~0x40);
        io.outb(0x1F6, 0xA0);
        var seconde = (Models.pic.pic2.pend & 0x40) != 0;
        bad += Voir("la demande servie, l'état non lu, OUT 1F6h,A0h ; une seconde demande", seconde == !hw,
                    seconde ? "posée" : "aucune", hw ? "aucune" : "posée", hw);
        // L'état lu (1F7h, l'interruption retombe), une interruption neuve, servie avant toute écriture de 1F6h, puis
        // OUT 1F6h,A0h : le matériel ne voit pas de front neuf.
        io.inb(0x1F7);
        Ide.ide.ide_irq_raise(Ide.ide.ide_drives[0]);
        Models.pic.pic2.pend &= unchecked((byte)~0x40);
        io.outb(0x1F6, 0xA0);
        seconde = (Models.pic.pic2.pend & 0x40) != 0;
        bad += Voir("l'état lu, une interruption neuve servie, OUT 1F6h,A0h ; une seconde demande", seconde == !hw,
                    seconde ? "posée" : "aucune", hw ? "aucune" : "posée", hw);
        // SRST négue INTRQ (§ 6.3.10) : l'état lu, une interruption neuve, OUT 3F6h,04h (SRST), puis OUT 3F6h,00h. PCem
        // garde l'interruption en attente et la repose à la fin du reset ; le matériel l'a effacée : aucune demande.
        io.inb(0x1F7);
        Ide.ide.ide_irq_raise(Ide.ide.ide_drives[0]);
        io.outb(0x3F6, 0x04);
        var pendant = (Models.pic.pic2.pend & 0x40) != 0;
        io.outb(0x3F6, 0x00);
        var apres = (Models.pic.pic2.pend & 0x40) != 0;
        bad += Voir("une interruption levée, OUT 3F6h,04h (SRST), puis OUT 3F6h,00h ; la demande pendant, puis après",
                    !pendant && apres == !hw, $"{(pendant ? "posée" : "retirée")}, puis {(apres ? "posée" : "aucune")}",
                    hw ? "retirée, puis aucune" : "retirée, puis posée", hw);
        // nIEN : l'interruption levée pendant que nIEN vaut 1 ne pose rien ; nIEN remis à 0, le fil monte — dans les deux
        // modes.
        IdeAttendre();
        io.inb(0x1F7);
        io.outb(0x3F6, 0x02);
        Ide.ide.ide_irq_raise(Ide.ide.ide_drives[0]);
        var masquee = (Models.pic.pic2.pend & 0x40) != 0;
        io.outb(0x3F6, 0x00);
        var demasquee = (Models.pic.pic2.pend & 0x40) != 0;
        bad += Voir("nIEN posé, une interruption levée, puis nIEN remis à 0 ; la demande", !masquee && demasquee,
                    $"{(masquee ? "posée" : "aucune")}, puis {(demasquee ? "posée" : "aucune")}", "aucune, puis posée", hw);
        return bad + VoirArret();
    });

    // PB-74 — READ VERIFY SECTOR(S) (ATA-1 r4 § 9.19, § 7.2.11) : en fin normale, l'adresse du dernier secteur vérifié
    // et le compte nul ; en erreur, IDNF, l'adresse du secteur fautif et le compte restant. VERIFY de 5 secteurs depuis
    // CHS 0/0/1 : PCem rend le compte 5 et le secteur 1, ATA-1 le compte 0 et le secteur 5. VERIFY de 5 secteurs depuis
    // le LBA 1358 d'un disque de 1360 : PCem, READY, compte 5, LBA 1358 ; ATA-1, ERR et IDNF, compte 3, LBA 1360.
    private static int Pb74(bool hw) => Avec(tmp =>
    {
        MonterIde(tmp);
        var bad = 0;
        void Verify(int compte, int s, int cl, int ch, int dh)
        {
            io.outb(0x1F2, (byte)compte);
            io.outb(0x1F3, (byte)s);
            io.outb(0x1F4, (byte)cl);
            io.outb(0x1F5, (byte)ch);
            io.outb(0x1F6, (byte)dh);
            io.outb(0x1F7, 0x40);
        }
        Verify(5, 1, 0, 0, 0xA0);
        var st = IdeAttendre();
        var (n, sec) = (io.inb(0x1F2), io.inb(0x1F3));
        bad += Voir("VERIFY de 5 secteurs depuis CHS 0/0/1 ; l'état, le compte, le secteur",
                    (st & 0x01) == 0 && (n, sec) == (hw ? (0, 5) : (5, 1)), $"état {st:X2}h, compte {n}, secteur {sec}",
                    hw ? "compte 0, secteur 5" : "compte 5, secteur 1", hw);
        Verify(2, 100, 0, 0, 0xE0);
        st = IdeAttendre();
        n = io.inb(0x1F2);
        var lba100 = io.inb(0x1F3) | io.inb(0x1F4) << 8 | io.inb(0x1F5) << 16;
        bad += Voir("VERIFY de 2 secteurs depuis le LBA 100 ; l'état, le compte, le LBA",
                    (st & 0x01) == 0 && (n, lba100) == (hw ? (0, 101) : (2, 100)), $"état {st:X2}h, compte {n}, LBA {lba100}",
                    hw ? "compte 0, LBA 101" : "compte 2, LBA 100", hw);
        Verify(5, 0x4E, 0x05, 0, 0xE0);
        st = IdeAttendre();
        var err = io.inb(0x1F1);
        n = io.inb(0x1F2);
        var lba = io.inb(0x1F3) | io.inb(0x1F4) << 8 | io.inb(0x1F5) << 16;
        bad += Voir("VERIFY de 5 secteurs depuis le LBA 1358 d'un disque de 1360 ; l'état, l'erreur, le compte, le LBA",
                    hw ? (st & 0x01) != 0 && err == 0x10 && n == 3 && lba == 1360 : (st & 0x01) == 0 && n == 5 && lba == 1358,
                    $"état {st:X2}h, erreur {err:X2}h, compte {n}, LBA {lba}",
                    hw ? "ERR, IDNF, compte 3, LBA 1360" : "sans erreur, compte 5, LBA 1358", hw);
        return bad + VoirArret();
    });
}
