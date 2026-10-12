// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — outillage de vérification.
//
// Les cas qui discriminent les corrections du lecteur ZIP (G13.7d1, PLAN-G13.md), en C# seul : PB-125 (les transferts
// au-delà de 512 Ko) et PB-126 (la capacité, la fin du disque, les longueurs nulles, le disque gardé au reset). Sur le
// banc de r9-zip : l'ami486 amorcé, le lecteur ZIP en maître secondaire chargé d'une image vierge de 100 663 296 octets
// que le cas écrit (creuse), ses ports conduits depuis l'outil et nIEN posé. Chaque attendu porte sa source.

using iXtal26.Scsi;

namespace iXtal26.Diff;

internal static partial class MaterielCas
{
    /// <summary>L'ami486 et son lecteur ZIP chargé d'une image vierge, puis le corps ; la machine fermée ensuite.</summary>
    private static int AvecZip(Func<string, int> corps) => AvecCd(d =>
    {
        var img = Path.Combine(d, "zip100.img");
        using (var f = new FileStream(img, FileMode.Create, FileAccess.Write))
            f.SetLength(scsi_zip_c.ZIP_SECTORS * 512L);
        var cfg = Path.Combine(d, "zip.cfg");
        File.WriteAllText(cfg, "model = ami486\ncpu = 10\nmem_size = 4096\ngfxcard = tvga9000b\nhdd_controller = ide\n" +
                               $"zip_channel = 2\nzip_path = {img}\n");
        try
        {
            if (!pc.loadconfig(cfg))
                throw new InvalidOperationException("loadconfig");
            Floppy.fdd_c.discfns[0] = Floppy.fdd_c.discfns[1] = "";
            for (var u = 0; u < 4; u++)
                Disc.hdd_c.ide_fn[u] = "";
            if (!pc.initpc("roms"))
                throw new InvalidOperationException("initpc");
            R9Atapi.Tranches(100);
            io.outb(0x376, 0x02);
            R9Atapi.Prepare();
            return corps(img);
        }
        finally
        {
            try { pc.closepc(); } catch (Exception) { }
        }
    });

    /// <summary>Le mot <paramref name="w"/> (0 à 255) du secteur <paramref name="s"/> : l'octet bas, le numéro du secteur ;
    /// l'octet haut, la place du mot — un décalage d'un octet ou d'un mot se voit.</summary>
    private static ushort Mot(int s, int w) => (ushort)((s & 0xFF) | ((w & 0xFF) << 8));

    private static byte[] Secteur(int s)
    {
        var b = new byte[512];
        for (var w = 0; w < 256; w++)
        {
            b[2 * w] = (byte)Mot(s, w);
            b[2 * w + 1] = (byte)(Mot(s, w) >> 8);
        }
        return b;
    }

    /// <summary>La phase de données sortante, bloc DRQ par bloc DRQ, les secteurs depuis <paramref name="lba"/> au motif
    /// de <see cref="Mot"/> ; rend les octets écrits.</summary>
    private static int EcrireSecteurs(int lba)
    {
        var total = 0;
        for (var b = 0; b < 400 && R9Atapi.AttendreBsy() && (R9Atapi.St() & 0x08) != 0; b++)
        {
            var n = io.inb(0x174) | (io.inb(0x175) << 8);
            if (n <= 0)
                break;
            for (var i = 0; i < (n + 1) / 2; i++, total += 2)
                io.outw(0x170, Mot(lba + total / 512, total % 512 / 2));
        }
        return total;
    }

    private static int Ecriture(int lba, int n)
    {
        R9Atapi.Paquet(R9Atapi.Cdb(0x2A, 0, (lba >> 24) & 0xFF, (lba >> 16) & 0xFF, (lba >> 8) & 0xFF, lba & 0xFF, 0, n >> 8, n & 0xFF));
        var ecrits = EcrireSecteurs(lba);
        return (R9Atapi.Fin(ecrits).st & 1) == 0 ? ecrits : -ecrits - 1;
    }

    // PB-125 — READ(10) et WRITE(10) transfèrent la longueur demandée, jusqu'à 65 535 blocs (SCSI-2 § 9.2.6, § 9.2.21 ;
    // SFF-8070i r1.3, Table 37) : rien ne borne un transfert à 512 Ko. Chaque secteur porte un motif qui varie d'un mot à
    // l'autre (Mot). WRITE(10) de 1 100 secteurs depuis le LBA 0 : PCem n'en garde que 512 (data_out, 256 Ko ; au-delà,
    // l'octet compté pas gardé, R9) ; le matériel les écrit tous — relus un à un, les secteurs 511, 512 et 1 099 (les
    // frontières du tampon et la fin). Puis les secteurs 1 000 et 1 050, écrits un à un dans les deux modes, et READ(10)
    // de 1 100 secteurs : le secteur 1 000, dans data_out déborde de data_in (le reliquat de G11), juste dans les deux
    // modes ; le 1 050, au-delà des deux tampons, nul chez PCem (R9), juste sur le matériel.
    private static int Pb125(bool hw) => AvecZip(image =>
    {
        var bad = 0;
        var ecrits = Ecriture(0, 1100);
        bad += Voir("WRITE(10) de 1 100 secteurs : les octets écrits", ecrits == 1100 * 512, $"{ecrits}", "563 200, GOOD (dans les deux modes)", hw);
        foreach (var s in new[] { 511, 512, 1099 })
        {
            var (st, _, b) = Paquet(R9Atapi.Read10(s, 1));
            var juste = (st & 1) == 0 && b.Length == 512 && b.SequenceEqual(Secteur(s));
            var nul = (st & 1) == 0 && b.Length == 512 && b.All(x => x == 0);
            var attendu = hw || s < 512;
            bad += Voir($"le secteur {s} relu", attendu ? juste : nul, juste ? "le motif écrit" : nul ? "nul" : $"autre (état {st:X2}h, {b.Length} octets)",
                        attendu ? "le motif écrit" : "nul (non écrit, R9)", hw);
        }
        var e1 = Ecriture(1000, 1);
        var e2 = Ecriture(1050, 1);
        var (s3, _, b3) = Paquet(R9Atapi.Read10(0, 1100));
        bool Dans(int s, Func<byte[], bool> p) => b3.Length == 1100 * 512 && p(b3.AsSpan(s * 512, 512).ToArray());
        var j1000 = Dans(1000, x => x.SequenceEqual(Secteur(1000)));
        var j1050 = Dans(1050, x => x.SequenceEqual(Secteur(1050)));
        var n1050 = Dans(1050, x => x.All(v => v == 0));
        bad += Voir("WRITE(10) des secteurs 1 000 et 1 050, puis READ(10) de 1 100 secteurs : les secteurs 1 000 et 1 050 dans le transfert",
                    e1 == 512 && e2 == 512 && (s3 & 1) == 0 && j1000 && (hw ? j1050 : n1050),
                    $"1 000 {(j1000 ? "juste" : "faux")}, 1 050 {(j1050 ? "juste" : n1050 ? "nul" : "faux")}, {b3.Length} octets",
                    hw ? "les deux justes" : "1 000 juste (le reliquat), 1 050 nul (R9)", hw);
        return bad;
    });

    // PB-126 — SFF-8070i r1.3 et SCSI-2. READ CAPACITY (Table 41, « the last valid LBA ») : PCem 196 608, le matériel
    // 196 607. READ(10) du LBA 196 608 (§ 9.1.2) : PCem GOOD, le matériel 05/21h. READ(10) de longueur 0 (§ 9.2.6) : PCem
    // entre en phase de données (DRQ) et n'en sort pas, le matériel rend GOOD. MODE SELECT(6) de longueur 0 (§ 8.2.8) :
    // de même. Un reset matériel (resetide), puis TEST UNIT READY : PCem a perdu le disque (02/3Ah) ; le matériel le
    // garde, rend 06/29h, puis GOOD ; puis, sur le matériel, un second reset : INQUIRY passe et laisse l'attention,
    // REQUEST SENSE la rapporte (06/29h) et l'efface (SFF-8070i r1.3 § 9.6).
    private static int Pb126(bool hw) => AvecZip(image =>
    {
        var bad = 0;
        var (st, _, b) = Paquet(R9Atapi.Cdb(0x25));
        var cap = b.Length >= 8 ? b[0] << 24 | b[1] << 16 | b[2] << 8 | b[3] : -1;
        bad += Voir("READ CAPACITY", (st & 1) == 0 && cap == (hw ? 196607 : 196608), $"{cap}", hw ? "196607" : "196608", hw);
        (st, _, b) = Paquet(R9Atapi.Read10(196608, 1));
        var asc = (st & 1) != 0 ? Asc() : 0;
        bad += Voir("READ(10) du LBA 196 608", hw ? (st & 1) != 0 && asc == 0x21 : (st & 1) == 0, $"état {st:X2}h, ASC {asc:X2}h",
                    hw ? "ERR, 21h" : "GOOD (le tampon d'avant)", hw);
        foreach (var (nom, cdb) in new[] { ("READ(10) de longueur 0", R9Atapi.Read10(0, 0)), ("MODE SELECT(6) de longueur 0", R9Atapi.Cdb(0x15, 0x10, 0, 0, 0)) })
        {
            R9Atapi.Paquet(cdb);
            R9Atapi.AttendreBsy();
            st = R9Atapi.St();
            bad += Voir($"{nom} : DRQ après le paquet", (st & 1) == 0 && (st & 0x08) == (hw ? 0 : 0x08), $"état {st:X2}h",
                        hw ? "GOOD, sans DRQ" : "DRQ (une phase sans fin)", hw);
            if ((st & 0x08) != 0)
            {
                R9Atapi.Out(7, 0x08);
                R9Atapi.Tranches(2);
                R9Atapi.Prepare();
            }
        }
        Ide.ide.resetide();
        R9Atapi.Tranches(10);
        io.outb(0x376, 0x02);
        var (t1, _, _) = Paquet(R9Atapi.Cdb(0x00));
        var a1 = (t1 & 1) != 0 ? Asc() : 0;
        var (t2, _, _) = Paquet(R9Atapi.Cdb(0x00));
        var a2 = (t2 & 1) != 0 ? Asc() : 0;
        bad += Voir("un reset matériel, puis deux TEST UNIT READY : les ASC", hw ? a1 == 0x29 && (t2 & 1) == 0 : a1 == 0x3A && a2 == 0x3A,
                    $"{a1:X2}h, {((t2 & 1) == 0 ? "GOOD" : $"{a2:X2}h")}", hw ? "29h, GOOD (le disque gardé)" : "3Ah, 3Ah (le disque perdu)", hw);
        if (hw)
        {
            // § 9.6 : INQUIRY s'exécute et laisse l'attention ; REQUEST SENSE la rapporte (06/29h, GOOD) et l'efface.
            Ide.ide.resetide();
            R9Atapi.Tranches(10);
            io.outb(0x376, 0x02);
            var (i1, _, _) = Paquet(R9Atapi.Cdb(0x12, 0, 0, 0, 36));
            var (r1, _, sense) = Paquet(R9Atapi.Cdb(0x03, 0, 0, 0, 18));
            var (t3, _, _) = Paquet(R9Atapi.Cdb(0x00));
            var rapporte = sense.Length >= 13 ? $"{sense[2] & 0x0F:X2}/{sense[12]:X2}h" : "?";
            bad += Voir("un second reset, puis INQUIRY, REQUEST SENSE et TEST UNIT READY", (i1 & 1) == 0 && (r1 & 1) == 0 &&
                        rapporte == "06/29h" && (t3 & 1) == 0, $"INQUIRY {i1:X2}h, REQUEST SENSE {r1:X2}h {rapporte}, TEST UNIT READY {t3:X2}h",
                        "GOOD, GOOD 06/29h, GOOD", hw);
        }
        return bad;
    });
}
