// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — outillage de vérification.
//
// Les cas qui discriminent les corrections du côté SCSI de l'AHA-1542C (G13.7e2a et G13.7e2b, PLAN-G13.md), en C#
// seul : PB-276 (le surplus de la cible au bout du CCB, HRST et SCRST qui libèrent le bus), PB-278 (les bits de sens,
// les 24 bits, l'ID de l'hôte), PB-133 (le LUN du CCB, un LUN absent) et PB-137 (le sense automatique). Sur le banc
// des mailbox (AvecMb : le POST a fini d'effacer la mémoire), une machine neuve par situation ; les CCB vers l'ID 1 (le
// disque d'AvecScsi, rempli d'un motif), en 80000h, la mailbox d'un emplacement en 80100h (R9Aha.Mailbox), les données
// en 200000h. Quelques attentes du mode matériel suivent une autre correction en jeu (sous « tout ») : PB-131 (la sense
// effacée par la commande suivante), PB-137 (le sense rangé au bout du CCB).

using iXtal26.Memory;

namespace iXtal26.Diff;

internal static partial class MaterielCas
{
    /// <summary>Un CCB vers l'ID 1 : l'opcode, les bits de sens (CCB octet 1, bits 3 et 4), la sense automatique coupée
    /// ou non ; rend le statut d'hôte, le statut cible et le code du MBI (0 : aucun dans le temps donné).</summary>
    private static (byte hote, byte cible, byte mbi) CcbSens(int opcode, int sens, byte[] cdb, int longueur, uint donnees,
                                                             bool sansSense = true, int tranches = 400)
    {
        var c = R9Aha.CcbDe(opcode, 1, cdb, longueur, donnees);
        c[1] |= (byte)(sens << 3);
        if (sansSense)
            c[3] = 1;
        return R9Aha.Mailbox(c, tranches);
    }

    private static byte[] Lire10(int lba, int n) => [0x28, 0, (byte)(lba >> 24), (byte)(lba >> 16), (byte)(lba >> 8), (byte)lba, 0, (byte)(n >> 8), (byte)n, 0];

    private static byte[] Ecrire10(int lba, int n) => [0x2A, 0, (byte)(lba >> 24), (byte)(lba >> 16), (byte)(lba >> 8), (byte)lba, 0, (byte)(n >> 8), (byte)n, 0];

    /// <summary>Les secteurs 0 à n - 1 du disque de l'ID 1 écrits d'un motif (Motif(secteur, octet)), par WRITE(10).</summary>
    private static void Remplir(int n)
    {
        for (var s = 0; s < n; s++)
            for (var k = 0; k < 512; k++)
                mem.mem_writeb_phys(Lu + (uint)(s * 512 + k), Motif(s, k));
        var (h, t, m) = Ccb(Ecrire10(0, n), n * 512, Lu, sansSense: true);
        if (h != 0 || t != 0 || m != 1)
            throw new InvalidOperationException($"le remplissage du disque : {h:X2}/{t:X2}/{m:X2}");
        for (var i = 0; i < n * 512; i++)
            mem.mem_writeb_phys(Lu + (uint)i, 0xEE);
    }

    private static bool LuJuste(int s0, int n)
    {
        for (var s = 0; s < n; s++)
            for (var k = 0; k < 512; k++)
                if (mem.mem_readb_phys(Lu + (uint)(s * 512 + k)) != Motif(s0 + s, k))
                    return false;
        return true;
    }

    private static bool LuIntact(int octets)
    {
        for (var i = 0; i < octets; i++)
            if (mem.mem_readb_phys(Lu + (uint)i) != 0xEE)
                return false;
        return true;
    }

    private static int Residu() => mem.mem_readb_phys(R9Aha.Ccb + 4) << 16 | mem.mem_readb_phys(R9Aha.Ccb + 5) << 8 | mem.mem_readb_phys(R9Aha.Ccb + 6);

    private static string Rendu((byte hote, byte cible, byte mbi) r) => r.mbi == 0 ? "aucun MBI" : $"{r.hote:X2}/{r.cible:X2}/{r.mbi:X2}";

    /// <summary>Un CCB posté et 02h, sans attendre le MBI ; puis `tranches` tranches.</summary>
    private static void Poster(byte[] ccb, int tranches)
    {
        R9Aha.Cmd(0x01, 1, (int)(R9Aha.Mbox >> 16) & 0xFF, (int)(R9Aha.Mbox >> 8) & 0xFF, (int)R9Aha.Mbox & 0xFF);
        R9Aha.Hacc();
        for (var i = 0; i < ccb.Length; i++)
            mem.mem_writeb_phys(R9Aha.Ccb + (uint)i, ccb[i]);
        mem.mem_writeb_phys(R9Aha.Mbox + 1, (byte)(R9Aha.Ccb >> 16));
        mem.mem_writeb_phys(R9Aha.Mbox + 2, unchecked((byte)(R9Aha.Ccb >> 8)));
        mem.mem_writeb_phys(R9Aha.Mbox + 3, unchecked((byte)R9Aha.Ccb));
        mem.mem_writeb_phys(R9Aha.Mbox + 4, 0);
        mem.mem_writeb_phys(R9Aha.Mbox, 1);
        R9Aha.Cmd(0x02);
        Tourner(tranches);
    }

    // PB-276 — le surplus et les resets (AHA-1540C TR, chapitre 5, CCB octet 1 : sans bits de sens, la commande
    // « completes without error » ; chapitre 4 : HRST « A Reset Condition is generated on the SCSI bus », SCRST
    // « causes a SCSI Bus Reset »). READ(10) de deux secteurs dans un CCB de 512 octets, sans bits de sens : PCem fait la
    // navette, aucun MBI ; la carte passe le second secteur et rend GOOD, le premier lu. WRITE(10) de deux secteurs dans
    // 512 octets : PCem fait la navette ; la carte complète de zéros (déduit), et le second secteur se relit nul. Un
    // READ(10) de 1 000 secteurs interrompu par HRST (puis par SCRST) après quelques tranches, puis TEST UNIT READY :
    // PCem laisse la cible BSY, la sélection échoue, 11h ; la carte a remis le bus au repos, GOOD.
    private static int Pb276(bool hw)
    {
        var bad = 0;
        bad += AvecMb(() =>
        {
            Remplir(2);
            var r = CcbSens(0, 0, Lire10(0, 2), 512, Lu);
            var v = $"{Rendu(r)}, {(LuJuste(0, 1) ? "le secteur 0 lu" : "le secteur 0 faux")}";
            var att = hw ? "00/00/01, le secteur 0 lu" : "aucun MBI, le secteur 0 lu";
            return Voir("READ(10) de 2 secteurs, CCB de 512 octets, sans bits de sens", v == att, v, att, hw);
        });
        bad += AvecMb(() =>
        {
            Remplir(2);
            for (var k = 0; k < 512; k++)
                mem.mem_writeb_phys(Lu + (uint)(4096 + k), 0x5A);
            var r = CcbSens(0, 0, Ecrire10(0, 2), 512, Lu + 4096);
            if (r.mbi == 0)
                return Voir("WRITE(10) de 2 secteurs, CCB de 512 octets, sans bits de sens", !hw, "aucun MBI", hw ? "00/00/01, le second secteur à zéro" : "aucun MBI", hw);
            Ccb(Lire10(1, 1), 512, Lu, sansSense: true);
            var zero = Lire(Lu, 512).All(o => o == 0);
            var v = $"{Rendu(r)}, {(zero ? "le second secteur à zéro" : "le second secteur autre")}";
            var att = hw ? "00/00/01, le second secteur à zéro" : "aucun MBI";
            return Voir("WRITE(10) de 2 secteurs, CCB de 512 octets, sans bits de sens", v == att, v, att, hw);
        });
        foreach (var (ctrl, nom) in new[] { (0x80, "HRST"), (0x10, "SCRST") })
            bad += AvecMb(() =>
            {
                var b = 0;
                var c = R9Aha.CcbDe(0, 1, Lire10(0, 1000), 1000 * 512, Lu);
                c[3] = 1;
                Poster(c, 3);
                var enVol = mem.mem_readb_phys(R9Aha.Mbox + 4) == 0;
                b += Voir($"{nom} : le READ(10) de 1 000 secteurs encore en vol", enVol, enVol ? "en vol" : "achevé", "en vol", hw);
                iXtal26.io.outb(R9Aha.P, (byte)ctrl);
                Tourner(50);
                iXtal26.io.outb(R9Aha.P, 0x20);
                var r = Ccb([0x00, 0, 0, 0, 0, 0], 0, Lu, sansSense: true);
                var v = Rendu(r);
                var att = hw ? "00/00/01" : "11/00/04";
                b += Voir($"{nom} pendant le transfert, puis TEST UNIT READY", v == att, v, att, hw);
                return b;
            });
        return bad;
    }

    // PB-278 — le CCB (AHA-1540C TR, chapitre 5, CCB octet 1 : « If bits 3 or 4 are set for an Initiator CCB, the data
    // length is checked » ; « if a data underrun/overrun condition occurs, the CCB is returned with … (12H) » ; « If
    // both bits are set … the command must perform no data transfer » ; le sens « established by the SCSI command …
    // independent of the value of bits 3 and 4 » ; octets 4-6 : le résiduel à zéro au dépassement). READ(10) de deux
    // secteurs, CCB de 512 octets, bit 3, opcode 03h : PCem fait la navette ; la carte rend 12h, MBI 04h, le résiduel à
    // 0 — et de même avec la sense automatique, et au bit 4 seul. Un secteur dans 1 024 octets au bit 3, opcode 03h (le
    // manque) : GOOD chez PCem, 12h sur la carte, le résiduel 512 des deux côtés ; suivi d'un CCB exact au bit 3 : GOOD
    // des deux côtés (rien n'est hérité) — sans, puis avec la sense automatique (sous « tout », PB-137 achève le CCB GOOD
    // sans sense, et le 12h doit sortir quand même). Les deux bits, READ(10) d'un secteur : PCem le lit ; la carte ne transfère
    // rien, sans erreur (déduit). Témoin, les deux côtés d'accord : le bit 4 seul sur un READ exact (le sens ignoré). Les
    // 24 bits : WRITE(10) d'un secteur depuis FFFF00h, relu : sa seconde moitié est la table des vecteurs (0 à FFh) sur
    // la carte, l'espace au-delà de 16 Mo chez PCem. L'ID de l'hôte posé à 1 (Set EEPROM 22h, sur un nvr/ à part) : TEST
    // UNIT READY vers l'ID 1, GOOD chez PCem, 11h sur la carte.
    private static int Pb278(bool hw)
    {
        var bad = 0;
        foreach (var (sens, sansSense) in new[] { (1, true), (1, false), (2, true) })
            bad += AvecMb(() =>
            {
                Remplir(2);
                var r = CcbSens(3, sens, Lire10(0, 2), 512, Lu, sansSense);
                var v = r.mbi == 0 ? "aucun MBI" : $"{Rendu(r)}, résiduel {Residu()}";
                var att = hw ? "12/00/04, résiduel 0" : "aucun MBI";
                return Voir($"bit {sens + 2}, 2 secteurs dans 512 octets, opcode 03h{(sansSense ? "" : ", sense automatique")}", v == att, v, att, hw);
            });
        bad += AvecMb(() =>
        {
            Remplir(1);
            var r = CcbSens(0, 3, Lire10(0, 1), 512, Lu);
            var v = $"{Rendu(r)}, {(LuIntact(512) ? "rien lu" : LuJuste(0, 1) ? "le secteur lu" : "le tampon touché")}";
            var att = hw ? "00/00/01, rien lu" : "00/00/01, le secteur lu";
            return Voir("les deux bits, READ(10) d'un secteur", v == att, v, att, hw);
        });
        bad += AvecMb(() =>
        {
            Remplir(1);
            var r = CcbSens(0, 2, Lire10(0, 1), 512, Lu);
            var v = $"{Rendu(r)}, {(LuJuste(0, 1) ? "le secteur lu" : "le secteur faux")}";
            const string att = "00/00/01, le secteur lu";
            return Voir("le bit 4 seul sur un READ(10) (témoin : le sens vient de la commande)", v == att, v, att, hw);
        });
        foreach (var sansSense in new[] { true, false })
            bad += AvecMb(() =>
            {
                Remplir(1);
                var r = CcbSens(3, 1, Lire10(0, 1), 1024, Lu, sansSense);
                var v = $"{Rendu(r)}, résiduel {Residu()}";
                var r2 = CcbSens(3, 1, Lire10(0, 1), 512, Lu, sansSense);
                v += $" ; puis un CCB exact : {Rendu(r2)}";
                var att = (hw ? "12/00/04" : "00/00/01") + ", résiduel 512 ; puis un CCB exact : 00/00/01";
                return Voir($"bit 3, 1 secteur dans 1 024 octets, opcode 03h (le manque){(sansSense ? "" : ", sense automatique")}", v == att, v, att, hw);
            });
        bad += AvecMb(() =>
        {
            var w = Ccb(Ecrire10(5, 1), 512, 0xFFFF00, sansSense: true);
            var r = Ccb(Lire10(5, 1), 512, Lu, sansSense: true);
            var vecteurs = true;
            for (var i = 0; i < 256; i++)
                if (mem.mem_readb_phys(Lu + 256 + (uint)i) != mem.mem_readb_phys((uint)i))
                    vecteurs = false;
            var v = $"{Rendu(w)} {Rendu(r)}, {(vecteurs ? "la seconde moitié : les vecteurs" : "la seconde moitié : autre chose")}";
            var att = hw ? "00/00/01 00/00/01, la seconde moitié : les vecteurs" : "00/00/01 00/00/01, la seconde moitié : autre chose";
            return Voir("WRITE(10) depuis FFFF00h, relu", v == att, v, att, hw);
        });
        bad += AvecNvr(_ => AvecMb(() =>
        {
            Commande(SetEeprom(0, 0x01));
            var r = Ccb([0x00, 0, 0, 0, 0, 0], 0, Lu, sansSense: true);
            var v = Rendu(r);
            var att = hw ? "11/00/04" : "00/00/01";
            return Voir("l'ID de l'hôte à 1, TEST UNIT READY vers l'ID 1", v == att, v, att, hw);
        }));
        return bad;
    }

    /// <summary>Un CCB vers l'ID 1 au LUN `lun` (CCB octet 1, bits 0-2) ; l'octet 3 (la sense automatique) donné, et
    /// `bout` octets à EEh posés au bout du CCB, après la CDB.</summary>
    private static (byte hote, byte cible, byte mbi) CcbLun(byte[] cdb, int longueur, int lun = 0, int octet3 = 1, int bout = 14)
    {
        var c = R9Aha.CcbDe(0, 1, cdb, longueur, Lu);
        Array.Resize(ref c, 0x12 + cdb.Length + bout);
        for (var k = 0x12 + cdb.Length; k < c.Length; k++)
            c[k] = 0xEE;
        c[1] |= (byte)lun;
        c[3] = (byte)octet3;
        return R9Aha.Mailbox(c, 400);
    }

    /// <summary>La sense rangée au bout du CCB (après une CDB de `m` octets) : « rien » si le premier octet est resté EEh ;
    /// sinon le code, la clé et le nombre d'octets écrits avant le premier EEh.</summary>
    private static string SenseCcb(int m, int bout)
    {
        var s = Lire(R9Aha.Ccb + 0x12 + (uint)m, bout);
        if (s[0] == 0xEE)
            return "rien au bout du CCB";
        var n = Array.IndexOf(s, (byte)0xEE);
        return $"au bout du CCB : {s[0]:X2}h, clé {s[2] & 0xF:X2}h, {(n < 0 ? bout : n)} octets";
    }

    // PB-133 — le LUN (AHA-1540C TR, chapitre 5, CCB octet 1 : le LUN passe par IDENTIFY, « The LUN field in the SCSI
    // Command Descriptor Block (CDB) is expected to be zero » ; SCSI-2 § 7.5.3 et § 8.2.5.1, un LUN que la cible n'a pas).
    // INQUIRY au LUN 1 du CCB, 0 dans la CDB : PCem sert le disque (00h), le matériel 7Fh. TEST UNIT READY au LUN 1 du
    // CCB : GOOD chez PCem, CHECK CONDITION sur le matériel — et, avec la sense automatique, 05/25h au bout du CCB sous
    // « tout » (PB-137). REQUEST SENSE au LUN 1 du CCB : la sense du LUN 0 (vide) chez
    // PCem, 05/25h sur le matériel. TEST UNIT READY au LUN 0 du CCB, 1 dans la CDB : CHECK CONDITION chez PCem (le disque
    // refuse le LUN de la CDB), GOOD sur le matériel (la CDB ignorée). Témoin, d'accord des deux côtés : la sense du LUN 0
    // (un opcode inconnu, CHECK CONDITION : 05/25h, 05/20h sous PB-131) survit à un INQUIRY au LUN 1.
    private static int Pb133(bool hw)
    {
        var bad = 0;
        bad += AvecMb(() =>
        {
            var r = CcbLun([0x12, 0, 0, 0, 36, 0], 36, lun: 1);
            var v = $"{Rendu(r)}, {mem.mem_readb_phys(Lu):X2}h";
            var att = hw ? "00/00/01, 7Fh" : "00/00/01, 00h";
            return Voir("INQUIRY au LUN 1 du CCB", v == att, v, att, hw);
        });
        bad += AvecMb(() =>
        {
            var v = Rendu(CcbLun([0x00, 0, 0, 0, 0, 0], 0, lun: 1));
            var att = hw ? "00/02/04" : "00/00/01";
            return Voir("TEST UNIT READY au LUN 1 du CCB", v == att, v, att, hw);
        });
        bad += AvecMb(() =>
        {
            var r = CcbLun([0x03, 0, 0, 0, 18, 0], 18, lun: 1);
            var s = Lire(Lu, 18);
            var v = $"{Rendu(r)}, clé {s[2] & 0xF:X2}h, ASC {s[12]:X2}h";
            var att = hw ? "00/00/01, clé 05h, ASC 25h" : "00/00/01, clé 00h, ASC 00h";
            return Voir("REQUEST SENSE au LUN 1 du CCB", v == att, v, att, hw);
        });
        bad += AvecMb(() =>
        {
            var r = CcbLun([0x00, 0, 0, 0, 0, 0], 0, lun: 1, octet3: 0, bout: 32);
            var v = $"{Rendu(r)}, {SenseCcb(6, 32)}";
            var att = !hw ? "00/00/01, rien au bout du CCB" :
                      iXtal26.materiel.pb_137 ? "00/02/04, au bout du CCB : 70h, clé 05h, 14 octets" : "00/02/04, rien au bout du CCB";
            return Voir("TEST UNIT READY au LUN 1 du CCB, la sense automatique", v == att, v, att, hw);
        });
        bad += AvecMb(() =>
        {
            var v = Rendu(CcbLun([0x00, 0x20, 0, 0, 0, 0], 0));
            var att = hw ? "00/00/01" : "00/02/04";
            return Voir("TEST UNIT READY au LUN 0 du CCB, 1 dans la CDB", v == att, v, att, hw);
        });
        bad += AvecMb(() =>
        {
            CcbLun([0x06, 0, 0, 0, 0, 0], 0);
            CcbLun([0x12, 0, 0, 0, 36, 0], 36, lun: 1);
            var s = SenseScsi();
            var v = $"clé {s[2] & 0xF:X2}h, ASC {s[12]:X2}h";
            var att = hw && iXtal26.materiel.pb_131 ? "clé 05h, ASC 20h" : "clé 05h, ASC 25h";
            return Voir("la sense du LUN 0 après un INQUIRY au LUN 1 (témoin)", v == att, v, att, hw);
        });
        return bad;
    }

    // PB-137 — le sense automatique (AHA-1540C TR, chapitre 5, CCB octet 3 et octets 18 + m : il suit un CHECK
    // CONDITION ; 00h, 14 octets ; 01h, aucun ; rangé à la fin du CCB). Un opcode inconnu (06h, CHECK CONDITION des deux
    // côtés), l'octet 3 à 12h : PCem range le sense dans son tampon interne, rien au bout du CCB ; le matériel y range 18
    // octets, 70h, clé 05h. L'octet 3 à 00h : 14 octets sur le matériel. L'octet 3 à 01h : rien des deux côtés (témoin).
    // Un opcode inconnu sans sense (la sense reste au disque), puis TEST UNIT READY GOOD, l'octet 3 à 00h : rien au bout du
    // CCB des deux côtés, mais PCem a envoyé son REQUEST SENSE, et la sense du disque est vide ; sur le matériel, elle
    // reste 05h (vide sous « tout » : PB-131 l'efface à TEST UNIT READY). La commande BIOS 03h (lecture d'un secteur, GOOD) après 15h, qui pose
    // « hors mailbox », et un CCB de mailbox en 80000h : PCem envoie le sense après GOOD, et le range au CCB périmé, en
    // 80000h + 12h + 10 ; le matériel n'envoie rien.
    private static int Pb137(bool hw)
    {
        var bad = 0;
        foreach (var (octet3, nom, n) in new[] { (0x12, "12h", 18), (0x00, "00h", 14) })
            bad += AvecMb(() =>
            {
                var r = CcbLun([0x06, 0, 0, 0, 0, 0], 0, octet3: octet3, bout: 32);
                var v = $"{Rendu(r)}, {SenseCcb(6, 32)}";
                var att = "00/02/04, " + (hw ? $"au bout du CCB : 70h, clé 05h, {n} octets" : "rien au bout du CCB");
                return Voir($"opcode inconnu, l'octet 3 à {nom}", v == att, v, att, hw);
            });
        bad += AvecMb(() =>
        {
            var r = CcbLun([0x06, 0, 0, 0, 0, 0], 0, octet3: 1, bout: 32);
            var v = $"{Rendu(r)}, {SenseCcb(6, 32)}";
            const string att = "00/02/04, rien au bout du CCB";
            return Voir("opcode inconnu, l'octet 3 à 01h (témoin)", v == att, v, att, hw);
        });
        bad += AvecMb(() =>
        {
            CcbLun([0x06, 0, 0, 0, 0, 0], 0);
            var r = CcbLun([0x00, 0, 0, 0, 0, 0], 0, octet3: 0, bout: 32);
            var v = $"{Rendu(r)}, {SenseCcb(6, 32)}, la sense du disque : clé {SenseScsi()[2] & 0xF:X2}h";
            var att = "00/00/01, rien au bout du CCB, la sense du disque : clé " + (hw && !iXtal26.materiel.pb_131 ? "05h" : "00h");
            return Voir("TEST UNIT READY GOOD, l'octet 3 à 00h, après un refus", v == att, v, att, hw);
        });
        bad += AvecMb(() =>
        {
            CcbLun([0x00, 0, 0, 0, 0, 0], 0);
            for (var k = 0; k < 32; k++)
                mem.mem_writeb_phys(R9Aha.Ccb + 0x12 + 10 + (uint)k, 0xEE);
            R9Aha.Bios(0x15, 1);
            var (st, _) = R9Aha.Bios(0x02, 1);
            var v = $"{SenseCcb(10, 32)}";
            var att = hw ? "rien au bout du CCB" : "au bout du CCB : 70h, clé 00h, 14 octets";
            return Voir($"BIOS 03h, lecture GOOD après 15h (état {st:X2}h), le CCB périmé", v == att, v, att, hw);
        });
        return bad;
    }
}
