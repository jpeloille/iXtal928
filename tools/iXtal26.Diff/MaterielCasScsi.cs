// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — outillage de vérification.
//
// Les cas qui discriminent les corrections du disque SCSI (G13.7d2a, PLAN-G13.md), en C# seul : PB-128 (les transferts
// au-delà de 512 Ko), PB-129 (le dernier bloc, la plage), PB-131 (REQUEST SENSE, INQUIRY, les refus), PB-132 côté cible
// (les longueurs nulles). Sur le banc de r9-scsihd : l'ami486, l'AHA-1542C et deux disques vierges de 20 × 64 × 32
// (40 960 secteurs) écrits dans le TMPDIR ; la carte conduite par ses ports et sa mailbox (R9Aha), un CCB par commande
// vers l'ID 1. La sense automatique de la carte coupée (CCB, octet 3 = 01h) là où le cas lit la sense lui-même.

using iXtal26.Memory;

namespace iXtal26.Diff;

internal static partial class MaterielCas
{
    private const uint Lu = 0x200000;

    /// <summary>L'ami486, la carte et ses deux disques, puis le corps ; la machine fermée ensuite.</summary>
    private static int AvecScsi(Func<int> corps) => AvecCd(d =>
    {
        for (var u = 0; u < 2; u++)
        {
            using var f = new FileStream(Path.Combine(d, $"d{u}.img"), FileMode.Create, FileAccess.Write);
            f.SetLength(20 * 64 * 32 * 512L);
        }
        var cfg = Path.Combine(d, "scsi.cfg");
        File.WriteAllText(cfg, "model = ami486\ncpu = 10\nmem_size = 4096\ngfxcard = tvga9000b\nhdd_controller = aha1542c\n" +
                               $"hdc_cylinders = 20\nhdc_heads = 64\nhdc_sectors = 32\nhdc_fn = {d}/d0.img\n" +
                               $"hdd_cylinders = 20\nhdd_heads = 64\nhdd_sectors = 32\nhdd_fn = {d}/d1.img\n");
        try
        {
            if (!pc.loadconfig(cfg))
                throw new InvalidOperationException("loadconfig");
            Floppy.fdd_c.discfns[0] = Floppy.fdd_c.discfns[1] = "";
            if (!pc.initpc("roms"))
                throw new InvalidOperationException("initpc");
            R9Atapi.Tranches(300);
            return corps();
        }
        finally
        {
            try { pc.closepc(); } catch (Exception) { }
        }
    });

    /// <summary>Un CCB vers l'ID 1, la sense automatique coupée si demandé ; rend le statut d'hôte, le statut cible et
    /// le code du MBI.</summary>
    private static (byte hote, byte cible, byte mbi) Ccb(byte[] cdb, int longueur, uint donnees, bool sansSense = false, int tranches = 400)
    {
        var c = R9Aha.CcbDe(0, 1, cdb, longueur, donnees);
        if (sansSense)
            c[3] = 1;
        return R9Aha.Mailbox(c, tranches);
    }

    private static byte[] Lire(uint adresse, int n)
    {
        var b = new byte[n];
        for (var k = 0; k < n; k++)
            b[k] = mem.mem_readb_phys(adresse + (uint)k);
        return b;
    }

    private static byte Motif(int s, int k) => (byte)(s * 7 + k);

    private static byte[] SenseScsi()
    {
        Ccb([0x03, 0, 0, 0, 18, 0], 18, Lu, sansSense: true);
        return Lire(Lu, 18);
    }

    // PB-128 — READ(10) et WRITE(10) transfèrent la longueur demandée, jusqu'à 65 535 blocs (SCSI-2 § 9.2.6, § 9.2.21) ;
    // la carte transfère ce que la cible rend. WRITE(10) de 1 100 secteurs (un motif qui varie d'un octet à l'autre) :
    // PCem n'en garde que 512 (data_out ; au-delà, l'octet compté pas gardé, R9) ; le matériel les écrit tous. READ(10)
    // de 1 100 secteurs : les secteurs 511, 512, 1 023, 1 024, 1 050 et 1 099 du transfert (les frontières de data_out et
    // des deux tampons, la fin) — le 511 juste des deux côtés, les autres nuls chez PCem (non écrits), justes sur le
    // matériel.
    private static int Pb128(bool hw) => AvecScsi(() =>
    {
        var bad = 0;
        for (var s = 0; s < 1100; s++)
            for (var k = 0; k < 512; k++)
                mem.mem_writeb_phys(R9Aha.Buf + (uint)(s * 512 + k), Motif(s, k));
        var (h1, c1, m1) = Ccb([0x2A, 0, 0, 0, 0, 0, 0, 1100 >> 8, 1100 & 0xFF, 0], 1100 * 512, R9Aha.Buf);
        bad += Voir("WRITE(10) de 1 100 secteurs : le MBI, les statuts", m1 != 0 && h1 == 0 && c1 == 0, $"MBI {m1:X2}h, hôte {h1:X2}h, cible {c1:X2}h",
                    "achevé, 00h, 00h (dans les deux modes)", hw);
        var (_, c2, m2) = Ccb([0x28, 0, 0, 0, 0, 0, 0, 1100 >> 8, 1100 & 0xFF, 0], 1100 * 512, Lu);
        bad += Voir("READ(10) de 1 100 secteurs : le MBI, le statut cible", m2 != 0 && c2 == 0, $"MBI {m2:X2}h, cible {c2:X2}h", "achevé, 00h", hw);
        foreach (var n in new[] { 511, 512, 1023, 1024, 1050, 1099 })
        {
            var b = Lire(Lu + (uint)(n * 512), 512);
            var juste = Enumerable.Range(0, 512).All(k => b[k] == Motif(n, k));
            var nul = b.All(x => x == 0);
            var attendu = hw || n < 512;
            bad += Voir($"READ(10) de 1 100 secteurs : le secteur {n} du transfert", attendu ? juste : nul, juste ? "juste" : nul ? "nul" : "faux",
                        attendu ? "juste" : "nul (non écrit, R9)", hw);
        }
        return bad;
    });

    // PB-129 — READ CAPACITY rend l'adresse du dernier bloc (SCSI-2 § 9.2.7) : 40 959 pour 40 960 secteurs ; PCem 40 960.
    // Un accès hors du disque (§ 9.1.2) : PCem GOOD, le matériel CHECK CONDITION (cible 02h), la sense 05/21h relevée par
    // REQUEST SENSE (la sense automatique de la carte coupée : PCem ne la range pas dans le CCB, PB-137) — READ(10),
    // VERIFY(10), WRITE(10), READ(6) et WRITE(6) du LBA 40 960, READ(10) de deux blocs depuis le 40 959. Le dernier bloc
    // lui-même se lit, GOOD dans les deux modes.
    private static int Pb129(bool hw) => AvecScsi(() =>
    {
        var bad = 0;
        var (_, c1, _) = Ccb([0x25, 0, 0, 0, 0, 0, 0, 0, 0, 0], 8, Lu);
        var cap = Lire(Lu, 4);
        var dernier = cap[0] << 24 | cap[1] << 16 | cap[2] << 8 | cap[3];
        bad += Voir("READ CAPACITY", c1 == 0 && dernier == (hw ? 40959 : 40960), $"{dernier}", hw ? "40959" : "40960", hw);
        var (_, c0, m0) = Ccb([0x28, 0, 0, 0, 0x9F, 0xFF, 0, 0, 1, 0], 512, Lu, sansSense: true);
        bad += Voir("READ(10) du LBA 40 959, le dernier", m0 != 0 && c0 == 0, $"cible {c0:X2}h", "00h (GOOD, dans les deux modes)", hw);
        foreach (var (nom, cdb) in new[] { ("READ(10) du LBA 40 960", new byte[] { 0x28, 0, 0, 0, 0xA0, 0, 0, 0, 1, 0 }),
                                          ("VERIFY(10) du LBA 40 960", new byte[] { 0x2F, 0, 0, 0, 0xA0, 0, 0, 0, 1, 0 }),
                                          ("WRITE(10) du LBA 40 960", new byte[] { 0x2A, 0, 0, 0, 0xA0, 0, 0, 0, 1, 0 }),
                                          ("READ(10) de deux blocs depuis le 40 959", new byte[] { 0x28, 0, 0, 0, 0x9F, 0xFF, 0, 0, 2, 0 }),
                                          ("READ(6) du LBA 40 960", new byte[] { 0x08, 0, 0xA0, 0, 1, 0 }),
                                          ("WRITE(6) du LBA 40 960", new byte[] { 0x0A, 0, 0xA0, 0, 1, 0 }) })
        {
            var (_, c, m) = Ccb(cdb, cdb[^2] * 512 + (cdb.Length == 10 ? cdb[7] * 512 * 256 : 0), Lu, sansSense: true);
            var sense = SenseScsi();
            bad += Voir($"{nom} : le statut cible, puis REQUEST SENSE", m != 0 && (hw ? c == 2 && (sense[2] & 0x0F) == 5 && sense[12] == 0x21 : c == 0),
                        $"cible {c:X2}h, sense {sense[2] & 0x0F:X2}/{sense[12]:X2}h", hw ? "02h, 05/21h" : "00h (GOOD)", hw);
        }
        return bad;
    });

    // PB-131 — SCSI-2. Un code inconnu (0Eh), la sense automatique coupée : PCem 05/25h (LOGICAL UNIT NOT SUPPORTED), le
    // matériel 05/20h (INVALID COMMAND OPERATION CODE) — relevés par REQUEST SENSE, dont la longueur additionnelle vaut
    // 0Ah sur le matériel (§ 8.2.14, Table 65), 00h chez PCem ; 70h dans les deux modes. Puis le code inconnu, TEST UNIT
    // READY, REQUEST SENSE : la sense persiste chez PCem (05/25h), elle est effacée par la commande suivante sur le
    // matériel (00/00h, § 8.2.14). REQUEST SENSE coupé à 8 octets : 70h et 0Ah sur le matériel. INQUIRY de 96 octets :
    // PCem `00 00 00 02 00 00 00 02`, le matériel `00 00 02 02 5B 00 00 00` (§ 8.2.5.1). INQUIRY avec EVPD, avec EVPD et
    // une allocation nulle, avec un code de page sans EVPD : PCem GOOD, le matériel 05/24h. SEND DIAGNOSTIC sans
    // autotest, liste vide (§ 8.2.15) : PCem 05/25h, le matériel GOOD. TEST UNIT READY au LUN 1 : 05/25h dans les deux
    // modes (le refus du LUN gardé).
    private static int Pb131(bool hw) => AvecScsi(() =>
    {
        var bad = 0;
        var (_, c0, _) = Ccb([0x0E, 0, 0, 0, 0, 0], 0, Lu, sansSense: true);
        var s1 = SenseScsi();
        bad += Voir("un code inconnu (0Eh), puis REQUEST SENSE : le statut, l'octet 0, la clé et l'ASC, la longueur additionnelle",
                    c0 == 2 && s1[0] == 0x70 && (s1[2] & 0x0F) == 5 && s1[12] == (hw ? 0x20 : 0x25) && s1[7] == (hw ? 0x0A : 0),
                    $"cible {c0:X2}h, {s1[0]:X2}h, {s1[2] & 0x0F:X2}/{s1[12]:X2}h, longueur {s1[7]:X2}h", hw ? "02h, 70h, 05/20h, 0Ah" : "02h, 70h, 05/25h, 00h", hw);
        Ccb([0x0E, 0, 0, 0, 0, 0], 0, Lu, sansSense: true);
        Ccb([0x00, 0, 0, 0, 0, 0], 0, Lu, sansSense: true);
        var s2 = SenseScsi();
        bad += Voir("un code inconnu, TEST UNIT READY, REQUEST SENSE : la clé et l'ASC", hw ? (s2[2] & 0x0F) == 0 && s2[12] == 0 : (s2[2] & 0x0F) == 5 && s2[12] == 0x25,
                    $"{s2[2] & 0x0F:X2}/{s2[12]:X2}h", hw ? "00/00h (effacée)" : "05/25h (persistante)", hw);
        Ccb([0x0E, 0, 0, 0, 0, 0], 0, Lu, sansSense: true);
        for (var k = 0; k < 18; k++)
            mem.mem_writeb_phys(Lu + (uint)k, 0xEE);
        Ccb([0x03, 0, 0, 0, 8, 0], 8, Lu, sansSense: true);
        var s3 = Lire(Lu, 18);
        bad += Voir("un code inconnu, puis REQUEST SENSE de 8 octets : les octets 0, 7 et 8", s3[0] == 0x70 && s3[7] == (hw ? 0x0A : 0) && s3[8] == 0xEE,
                    $"{s3[0]:X2}h, {s3[7]:X2}h, {s3[8]:X2}h", hw ? "70h, 0Ah, EEh (non écrit)" : "70h, 00h, EEh (non écrit)", hw);
        var (_, c3, _) = Ccb([0x12, 0, 0, 0, 96, 0], 96, Lu);
        var inq = Convert.ToHexString(Lire(Lu, 8));
        bad += Voir("INQUIRY de 96 octets : les huit premiers", c3 == 0 && inq == (hw ? "000002025B000000" : "0000000200000002"), inq,
                    hw ? "000002025B000000" : "0000000200000002", hw);
        foreach (var (nom, cdb, n) in new[] { ("INQUIRY avec EVPD", new byte[] { 0x12, 1, 0, 0, 96, 0 }, 96),
                                             ("INQUIRY avec EVPD, d'allocation 0", new byte[] { 0x12, 1, 0, 0, 0, 0 }, 0),
                                             ("INQUIRY du code de page 80h, sans EVPD", new byte[] { 0x12, 0, 0x80, 0, 96, 0 }, 96) })
        {
            if (!hw && n == 0)
                continue; // PCem : l'allocation nulle fige le bus (PB-132).
            var (_, c4, _) = Ccb(cdb, n, Lu, sansSense: true);
            var s4 = SenseScsi();
            bad += Voir($"{nom} : le statut, l'ASC", hw ? c4 == 2 && s4[12] == 0x24 : c4 == 0, $"cible {c4:X2}h, ASC {s4[12]:X2}h",
                        hw ? "02h, 24h" : "00h (les 96 octets)", hw);
        }
        var (_, c5, _) = Ccb([0x1D, 0, 0, 0, 0, 0], 0, Lu, sansSense: true);
        var s5 = SenseScsi();
        bad += Voir("SEND DIAGNOSTIC sans autotest, liste vide : le statut, l'ASC", hw ? c5 == 0 : c5 == 2 && s5[12] == 0x25, $"cible {c5:X2}h, ASC {s5[12]:X2}h",
                    hw ? "00h (GOOD)" : "02h, 25h", hw);
        var (_, c6, _) = Ccb([0x00, 0x20, 0, 0, 0, 0], 0, Lu, sansSense: true);
        var s6 = SenseScsi();
        bad += Voir("TEST UNIT READY au LUN 1 (cdb[1]) : le statut, l'ASC", c6 == 2 && s6[12] == 0x25, $"cible {c6:X2}h, ASC {s6[12]:X2}h",
                    "02h, 25h (dans les deux modes)", hw);
        return bad;
    });

    // PB-132, côté cible — une longueur nulle : aucun transfert, la cible passe au statut (SCSI-2 § 8.2.5, § 8.2.8,
    // § 8.2.10, § 8.2.14, § 9.2.6, § 9.2.21). READ(10) et WRITE(10) de compte 0, INQUIRY, MODE SENSE(6) et REQUEST SENSE
    // d'allocation 0, MODE SELECT(6) de longueur 0 : PCem entre en phase de données et n'en sort plus — la carte ne poste
    // aucun MBI ; le matériel rend GOOD, sans données. REQUEST SENSE d'allocation 0 efface la sense : après un code
    // inconnu, puis lui, REQUEST SENSE rend 00/00h sur le matériel. (Une machine neuve pour chacun : PCem fige le bus.)
    private static int Pb132(bool hw)
    {
        var bad = 0;
        foreach (var (nom, cdb) in new[] { ("READ(10) de compte 0", new byte[] { 0x28, 0, 0, 0, 0, 0, 0, 0, 0, 0 }),
                                          ("WRITE(10) de compte 0", new byte[] { 0x2A, 0, 0, 0, 0, 0, 0, 0, 0, 0 }),
                                          ("INQUIRY d'allocation 0", new byte[] { 0x12, 0, 0, 0, 0, 0 }),
                                          ("MODE SENSE(6) d'allocation 0", new byte[] { 0x1A, 0, 0x3F, 0, 0, 0 }),
                                          ("MODE SELECT(6) de longueur 0", new byte[] { 0x15, 0x10, 0, 0, 0, 0 }),
                                          ("REQUEST SENSE d'allocation 0", new byte[] { 0x03, 0, 0, 0, 0, 0 }) })
            bad += AvecScsi(() =>
            {
                Ccb([0x0E, 0, 0, 0, 0, 0], 0, Lu, sansSense: true);
                var (h, c, m) = Ccb(cdb, 4096, Lu, sansSense: true, tranches: 200);
                var fini = hw || cdb[0] == 0x2A;
                var b = Voir($"{nom} : le MBI, le statut cible", fini ? m != 0 && c == 0 : m == 0, $"MBI {m:X2}h, hôte {h:X2}h, cible {c:X2}h",
                             fini ? "achevé, GOOD" : "aucun MBI (la phase vide ne finit pas)", hw);
                if (hw && cdb[0] == 0x03)
                {
                    var s = SenseScsi();
                    b += Voir("REQUEST SENSE d'allocation 0 après un code inconnu, puis REQUEST SENSE : la clé et l'ASC", (s[2] & 0x0F) == 0 && s[12] == 0,
                              $"{s[2] & 0x0F:X2}/{s[12]:X2}h", "00/00h (effacée)", hw);
                }
                return b;
            });
        return bad;
    }
}
