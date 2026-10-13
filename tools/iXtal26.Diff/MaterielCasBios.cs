// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — outillage de vérification.
//
// Les cas qui discriminent les corrections de G13.7e3 (PLAN-G13.md), en C# seul : la commande BIOS 03h de l'AHA-1542C,
// PB-138 (le CCB de la mailbox intact, 80h sur une cible absente) et PB-279 (le secteur CHS compté à partir de 1). Sur
// le banc des mailbox (AvecMb), une machine neuve par situation ; le disque de l'ID 1 (20 cylindres, 64 têtes, 32
// secteurs, la géométrie que PCem prête à la commande BIOS), les IDs 2 à 6 vides ; les données de la commande BIOS en
// 80200h (R9Aha.Petit). Les attendus sont déduits : l'interface est privée (« reserved for use by the Adaptec host
// adapter BIOS »). La capacité que rend 08h suit PB-129 sous « tout » (le dernier bloc, 9FFFh ; PCem, le nombre de
// blocs, A000h).

using iXtal26.Memory;

namespace iXtal26.Diff;

internal static partial class MaterielCas
{
    private static string Octets(uint adresse, int n) => string.Join(" ", Lire(adresse, n).Select(b => b.ToString("X2")));

    private static void Poser(uint adresse, int n, byte v)
    {
        for (var i = 0; i < n; i++)
            mem.mem_writeb_phys(adresse + (uint)i, v);
    }

    // PB-138 — la commande BIOS 03h et le CCB de la mailbox. Après un CCB de la mailbox (TEST UNIT READY, GOOD ; ses
    // octets 0Eh et 0Fh remis à A5h, le MBI à 00h, et un START reposé dans le MBO) : la lecture 02h réussit des deux
    // côtés, mais PCem achève aussi l'ancien CCB (00h, 00h), libère le MBO — le START est perdu — et poste un MBI (01h) ;
    // le matériel n'y touche pas. Le sense de la lecture, lui, suit PB-137 : PCem le demande même après GOOD, et sous
    // PB-138 seul, le CCB n'étant plus de la mailbox, il le range à l'adresse du CCB périmé (80000h + 12h + 10, 70h) ;
    // sous « tout », PB-137 ne le demande pas. Après un CCB à liste de dispersion (opcode 02h, une liste d'un segment
    // vers 210000h) : PCem envoie la lecture suivante dans l'ancienne liste ; le matériel à l'adresse de la commande.
    // Vers un ID vide après une lecture réussie — 02h, 03h, 04h vers l'ID 5, 02h vers l'ID 7, celui de la carte : 00h
    // chez PCem (le statut périmé), 80h sur le matériel (TR, chapitre 6, « Time-out »). 08h vers l'ID 5 après 08h vers
    // l'ID 1 : PCem rend 00h et recopie les quatre octets de la capacité précédente ; le matériel 80h, le tampon
    // intact. 15h vers l'ID 5 après 15h vers l'ID 1 : 00h contre 80h. Témoin, d'accord des deux côtés : 08h vers l'ID
    // 1, 00h et la capacité.
    private static int Pb138(bool hw)
    {
        var bad = 0;
        var capacite = hw && iXtal26.materiel.pb_129 ? "00 00 9F FF" : "00 00 A0 00";
        bad += AvecMb(() =>
        {
            CcbLun([0x00, 0, 0, 0, 0, 0], 0);
            Poser(R9Aha.Ccb + 0xE, 2, 0xA5);
            mem.mem_writeb_phys(R9Aha.Mbox + 4, 0);
            mem.mem_writeb_phys(R9Aha.Mbox, 1);
            var (st, d) = R9Aha.Bios(0x02, 1);
            var v = $"{d:X2}h, l'ancien CCB {Octets(R9Aha.Ccb + 0xE, 2)}, MBO {mem.mem_readb_phys(R9Aha.Mbox):X2}h, " +
                    $"MBI {mem.mem_readb_phys(R9Aha.Mbox + 4):X2}h, en +1Ch {mem.mem_readb_phys(R9Aha.Ccb + 0x1C):X2}h";
            var att = !hw ? "00h, l'ancien CCB 00 00, MBO 00h, MBI 01h, en +1Ch EEh" :
                      $"00h, l'ancien CCB A5 A5, MBO 01h, MBI 00h, en +1Ch {(iXtal26.materiel.pb_137 ? "EE" : "70")}h";
            return Voir($"02h après un CCB de la mailbox (état {st:X2}h)", v == att, v, att, hw);
        });
        bad += AvecMb(() =>
        {
            Remplir(2);
            for (var k = 0; k < 6; k++)
                mem.mem_writeb_phys(0x80600 + (uint)k, new byte[] { 0x00, 0x02, 0x00, 0x21, 0x00, 0x00 }[k]);
            var c = R9Aha.CcbDe(0x02, 1, [0x00, 0, 0, 0, 0, 0], 6, 0x80600);
            c[3] = 1;
            R9Aha.Mailbox(c, 400);
            Poser(R9Aha.Petit, 512, 0xEE);
            Poser(0x210000, 512, 0xEE);
            var (st, d) = R9Aha.Bios(0x02, 1);
            string Ou(uint a) => mem.mem_readb_phys(a) == 0xEE ? "rien" : Enumerable.Range(0, 2).FirstOrDefault(
                b => Enumerable.Range(0, 512).All(k => mem.mem_readb_phys(a + (uint)k) == Motif(b, k)), -1) is var b && b >= 0 ? $"bloc {b}" : "autre";
            var v = $"{d:X2}h, à l'adresse de la commande : {Ou(R9Aha.Petit)}, dans l'ancienne liste : {Ou(0x210000)}";
            var bloc = hw && iXtal26.materiel.pb_279 ? 0 : 1;
            var att = hw ? $"00h, à l'adresse de la commande : bloc {bloc}, dans l'ancienne liste : rien" :
                           "00h, à l'adresse de la commande : rien, dans l'ancienne liste : bloc 1";
            return Voir($"02h après un CCB à liste de dispersion (état {st:X2}h)", v == att, v, att, hw);
        });
        bad += AvecMb(() =>
        {
            var (_, d1) = R9Aha.Bios(0x02, 1);
            var r = new[] { (0x02, 5), (0x03, 5), (0x04, 5), (0x02, 7) }.Select(x => R9Aha.Bios(x.Item1, x.Item2).donnee);
            var v = $"{d1:X2}h ; {string.Join(" ", r.Select(x => $"{x:X2}h"))}";
            var att = hw ? "00h ; 80h 80h 80h 80h" : "00h ; 00h 00h 00h 00h";
            return Voir("02h vers l'ID 1, puis 02h, 03h, 04h vers l'ID 5 vide et 02h vers l'ID 7", v == att, v, att, hw);
        });
        bad += AvecMb(() =>
        {
            R9Aha.Bios(0x08, 1);
            Poser(R9Aha.Petit, 4, 0xEE);
            var (st, d) = R9Aha.Bios(0x08, 5);
            var v = $"{d:X2}h, {Octets(R9Aha.Petit, 4)}";
            var att = hw ? "80h, EE EE EE EE" : $"00h, {capacite}";
            return Voir($"08h vers l'ID 1, puis vers l'ID 5 vide (état {st:X2}h)", v == att, v, att, hw);
        });
        bad += AvecMb(() =>
        {
            R9Aha.Bios(0x15, 1);
            var (st, d) = R9Aha.Bios(0x15, 5);
            var v = $"{d:X2}h";
            var att = hw ? "80h" : "00h";
            return Voir($"15h vers l'ID 1, puis vers l'ID 5 vide (état {st:X2}h)", v == att, v, att, hw);
        });
        bad += AvecMb(() =>
        {
            Poser(R9Aha.Petit, 4, 0xEE);
            var (st, d) = R9Aha.Bios(0x08, 1);
            var v = $"{d:X2}h, {Octets(R9Aha.Petit, 4)}";
            var att = $"00h, {capacite}";
            return Voir($"08h vers l'ID 1 (témoin, état {st:X2}h)", v == att, v, att, hw);
        });
        return bad;
    }

    // PB-279 — les secteurs de la commande BIOS comptent à partir de 1, comme ceux de l'INT 13h. Les blocs 0 à 33 du
    // disque écrits d'un motif. 02h au cylindre 0, tête 0, secteur 1 : PCem lit le bloc 1, le matériel le bloc 0. 02h à
    // la tête 1, secteur 1 : le bloc 33 contre le bloc 32 (la tête compte 32 secteurs, des deux côtés). 03h au secteur
    // 2, les données à 5Ah : PCem écrit le bloc 2, le matériel le bloc 1. Témoins, d'accord des deux côtés : le
    // secteur 0, hors de la convention, lu au bloc 0 (inconnu, laissé tel quel) ; 04h au dernier secteur du disque
    // (cylindre 19, tête 63, secteur 32 : le bloc 40 959), 00h — la vérification de PCem ne lit rien et réussit hors du
    // disque ; joué sous « tout » (hors de la série, à chaque étape), où PB-129 refuse le bloc 40 960, il prouve le −1
    // de 04h, qu'aucune situation de la série ne voit.
    private static int Pb279(bool hw)
    {
        var bad = 0;
        string Bloc(uint adresse)
        {
            for (var s = 0; s < 34; s++)
                if (Enumerable.Range(0, 512).All(k => mem.mem_readb_phys(adresse + (uint)k) == Motif(s, k)))
                    return $"bloc {s}";
            return mem.mem_readb_phys(adresse) == 0x5A ? "5Ah" : $"{mem.mem_readb_phys(adresse):X2}h…";
        }
        bad += AvecMb(() =>
        {
            Remplir(34);
            var (st, d) = R9Aha.BiosChs(0x02, 1, 0, 0, 1);
            var v = $"{d:X2}h, {Bloc(R9Aha.Petit)}";
            var att = hw ? "00h, bloc 0" : "00h, bloc 1";
            return Voir($"02h, secteur 1 (état {st:X2}h)", v == att, v, att, hw);
        });
        bad += AvecMb(() =>
        {
            Remplir(34);
            var (st, d) = R9Aha.BiosChs(0x02, 1, 0, 1, 1);
            var v = $"{d:X2}h, {Bloc(R9Aha.Petit)}";
            var att = hw ? "00h, bloc 32" : "00h, bloc 33";
            return Voir($"02h, tête 1, secteur 1 (état {st:X2}h)", v == att, v, att, hw);
        });
        bad += AvecMb(() =>
        {
            Remplir(3);
            Poser(R9Aha.Petit, 512, 0x5A);
            var (st, d) = R9Aha.BiosChs(0x03, 1, 0, 0, 2);
            Ccb(Lire10(1, 2), 1024, Lu, sansSense: true);
            var v = $"{d:X2}h, bloc 1 : {Bloc(Lu)}, bloc 2 : {Bloc(Lu + 512)}";
            var att = hw ? "00h, bloc 1 : 5Ah, bloc 2 : bloc 2" : "00h, bloc 1 : bloc 1, bloc 2 : 5Ah";
            return Voir($"03h, secteur 2 (état {st:X2}h)", v == att, v, att, hw);
        });
        bad += AvecMb(() =>
        {
            Remplir(3);
            var (st, d) = R9Aha.BiosChs(0x02, 1, 0, 0, 0);
            var v = $"{d:X2}h, {Bloc(R9Aha.Petit)}";
            const string att = "00h, bloc 0";
            return Voir($"02h, secteur 0 (témoin, état {st:X2}h)", v == att, v, att, hw);
        });
        bad += AvecMb(() =>
        {
            var (st, d) = R9Aha.BiosChs(0x04, 1, 19, 63, 32);
            var v = $"{d:X2}h";
            const string att = "00h";
            return Voir($"04h, le dernier secteur (témoin, état {st:X2}h)", v == att, v, att, hw);
        });
        return bad;
    }
}
