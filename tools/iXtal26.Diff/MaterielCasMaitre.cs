// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — outillage de vérification.
//
// Les cas de la mise sous tension et du maître de bus de l'AHA-1542C (G13.7e4, PLAN-G13.md) : PB-142 (l'autotest,
// STST puis INIT et IDLE) et PB-260 (la carte ne prend le bus que par son canal du 8237, en cascade, démasqué, son
// 8237 actif), sur le banc des mailbox (MaterielCasMailbox.cs). Le canal est celui de la configuration de la carte :
// 7 en mode PCem et sous PB-260 seul, 5 sous « tout » (l'usine, PB-144) ; le banc le met en cascade comme la ROM.

using iXtal26.Memory;

namespace iXtal26.Diff;

internal static partial class MaterielCas
{
    // PB-142 — à la mise sous tension, la carte fait son autotest (AHA-1540C TR, Hard Reset Operations, « identical to
    // that of normal power-on ») : STST (80h), puis INIT et IDLE (30h). PCem part de l'état 0, relu 10h (IDLE, que le
    // port ajoute), jusqu'au premier reset. Le port d'état relu avant toute tranche, puis après une, puis après 300 (le
    // POST, sans la ROM de la carte). Témoins : HRST, relu avant toute tranche puis après une, 80h puis 30h dans les
    // deux modes (le chemin de PCem) ; et la réentrance (c), un NOP écrit pendant l'autotest, perdu dans les deux
    // modes.
    private static int Pb142(bool hw)
    {
        int Etats(int n) => AvecScsi(() =>
        {
            var e0 = R9Aha.Etat();
            pc.runpc();
            var v = $"{e0:X2} {R9Aha.Etat():X2}";
            var att = hw ? n == 0 ? "80 30" : "30 30" : "10 10";
            return Voir($"l'état à la mise sous tension, après {n} tranche(s), puis une de plus", v == att, v, att, hw);
        }, tranches: n);
        var bad = Etats(0) + Etats(300);
        bad += AvecScsi(() =>
        {
            io.outb(R9Aha.P, 0x80);
            var e0 = R9Aha.Etat();
            pc.runpc();
            var v = $"{e0:X2} {R9Aha.Etat():X2}";
            return Voir("HRST après 300 tranches, l'état relu, puis après une tranche (témoin)", v == "80 30", v,
                        "80 30", hw);
        }, tranches: 300);
        bad += AvecScsi(() =>
        {
            io.outb(R9Aha.P, 0x80);
            io.outb(R9Aha.P + 1, 0x00);
            for (var i = 0; i < 20; i++)
                pc.runpc();
            var v = (R9Aha.Isr() & 0x04) != 0 ? "HACC" : "pas de HACC";
            return Voir("HRST, un NOP écrit pendant l'autotest, 20 tranches (témoin, la réentrance)",
                        v == "pas de HACC", v, "pas de HACC", hw);
        });
        return bad;
    }

    // PB-260 — un maître de bus prend le bus par un canal du 8237 en mode cascade (AT TR p. 1-26, –MASTER ; 8237A,
    // Cascade Mode) : le canal masqué, ou son 8237 désactivé, ne l'accorde pas, et la carte attend ; elle reprend au
    // démasquage. PCem lit et écrit la mémoire sans regarder le 8237. Un READ(10) d'un secteur vers l'ID 1 ; la
    // commande 1Ah (64 octets lus en mémoire) ; 03h/08h (4 octets écrits). La MBO reste à 01h tant que la carte n'a pas
    // le bus (PCem la libère en prenant le CCB), et le CCB se lit au démasquage : récrit pendant le masque, la carte
    // lit le nouveau (PCem l'a déjà lu) ; de même 1Ah, relu par 1Bh. Le canal hors cascade (Q28 (A)) : la carte
    // attend. Le canal 0, sous PB-253, passe par la cascade du canal 4. Témoins : le canal en cascade, démasqué ; et,
    // le canal masqué, Adapter Inquiry (04h) et HRST, qui ne prennent pas le bus.
    private static int Pb260(bool hw)
    {
        // Le CCB d'un READ(10) d'un secteur vers l'ID 1, en Lu, sans sense automatique.
        static byte[] CcbLu()
        {
            var c = R9Aha.CcbDe(0, 1, Lire10(0, 1), 512, Lu);
            c[3] = 1;
            return c;
        }
        var bad = AvecMb(() =>
        {
            Remplir(1);
            var r = Ccb(Lire10(0, 1), 512, Lu, sansSense: true);
            var v = $"{Rendu(r)}, {(LuJuste(0, 1) ? "lu" : "faux")}";
            return Voir("le canal en cascade, démasqué : READ(10) d'un secteur (témoin)", v == "00/00/01, lu", v,
                        "00/00/01, lu", hw);
        });
        bad += AvecMb(() =>
        {
            Remplir(1);
            CanalAha(masque: true);
            Poster(CcbLu(), 200);
            var avant = $"MBO {mem.mem_readb_phys(R9Aha.Mbox):X2}, MBI {mem.mem_readb_phys(R9Aha.Mbox + 4):X2}, " +
                        $"{(LuIntact(512) ? "intact" : LuJuste(0, 1) ? "lu" : "touché")}";
            CanalAha(masque: false);
            Tourner(200);
            var apres = $"MBI {mem.mem_readb_phys(R9Aha.Mbox + 4):X2}, {(LuJuste(0, 1) ? "lu" : "faux")}";
            var v = $"{avant} ; démasqué : {apres}";
            var att = hw ? "MBO 01, MBI 00, intact ; démasqué : MBI 01, lu"
                         : "MBO 00, MBI 01, lu ; démasqué : MBI 01, lu";
            return Voir("le canal masqué : READ(10) posté, 200 tranches, puis démasqué, 200 tranches", v == att, v, att,
                        hw);
        });
        bad += AvecMb(() =>
        {
            Remplir(2);
            CanalAha(masque: true);
            Poster(CcbLu(), 100);
            var cdb = Lire10(1, 1);
            for (var k = 0; k < cdb.Length; k++)
                mem.mem_writeb_phys(R9Aha.Ccb + 0x12 + (uint)k, cdb[k]);
            CanalAha(masque: false);
            Tourner(200);
            var v = LuJuste(0, 1) ? "le secteur 0" : LuJuste(1, 1) ? "le secteur 1" : "autre";
            var att = hw ? "le secteur 1" : "le secteur 0";
            return Voir("le canal masqué : READ(10) du secteur 0 posté, le CCB récrit vers le 1, puis démasqué : " +
                        "le CCB lu au démasquage", v == att, v, att, hw);
        });
        bad += AvecMb(() =>
        {
            for (var k = 0; k < 64; k++)
                mem.mem_writeb_phys(R9Aha.Petit + (uint)k, (byte)(0x40 + k));
            CanalAha(masque: true);
            R9Aha.Cmd(0x1A, (int)(R9Aha.Petit >> 16) & 0xFF, (int)(R9Aha.Petit >> 8) & 0xFF, (int)R9Aha.Petit & 0xFF);
            Tourner(50);
            var avant = (R9Aha.Isr() & 0x04) != 0 ? "HACC" : "pas de HACC";
            // Le motif récrit pendant le masque : la carte, qui n'a pas encore lu, lit le nouveau.
            for (var k = 0; k < 64; k++)
                mem.mem_writeb_phys(R9Aha.Petit + (uint)k, (byte)(0x80 + k));
            CanalAha(masque: false);
            Tourner(50);
            var apres = (R9Aha.Isr() & 0x04) != 0 ? "HACC" : "pas de HACC";
            io.outb(R9Aha.P, 0x20);
            R9Aha.Cmd(0x1B, (int)(Lu >> 16) & 0xFF, (int)(Lu >> 8) & 0xFF, (int)Lu & 0xFF);
            R9Aha.Hacc();
            var octet = mem.mem_readb_phys(Lu);
            var lu = octet == 0x80 ? "le nouveau motif" : octet == 0x40 ? "l'ancien" : "autre";
            var v = $"{avant} ; démasqué : {apres}, 1Bh rend {lu}";
            var att = hw ? "pas de HACC ; démasqué : HACC, 1Bh rend le nouveau motif"
                         : "HACC ; démasqué : HACC, 1Bh rend l'ancien";
            return Voir("le canal masqué : 1Ah (64 octets), le motif récrit, puis démasqué ; 1Bh relu", v == att, v,
                        att, hw);
        });
        bad += AvecMb(() =>
        {
            for (var k = 0; k < 4; k++)
                mem.mem_writeb_phys(R9Aha.Petit + (uint)k, 0xEE);
            CanalAha(masque: true);
            R9Aha.Cmd(0x03, 0x08, 1, 0, 0, 0, 1, 1, (int)(R9Aha.Petit >> 16) & 0xFF, (int)(R9Aha.Petit >> 8) & 0xFF,
                      (int)R9Aha.Petit & 0xFF);
            Tourner(100);
            var avant = $"{((R9Aha.Isr() & 0x04) != 0 ? "HACC" : "pas de HACC")}, " +
                        $"{(mem.mem_readb_phys(R9Aha.Petit) != 0xEE ? "écrit" : "EE")}";
            CanalAha(masque: false);
            Tourner(100);
            var apres = $"{((R9Aha.Isr() & 0x04) != 0 ? "HACC" : "pas de HACC")}, " +
                        $"{(mem.mem_readb_phys(R9Aha.Petit) != 0xEE ? "écrit" : "EE")}";
            io.outb(R9Aha.P, 0x20);
            var v = $"{avant} ; démasqué : {apres}";
            var att = hw ? "pas de HACC, EE ; démasqué : HACC, écrit" : "HACC, écrit ; démasqué : HACC, écrit";
            return Voir("le canal masqué : 03h/08h (les paramètres de l'ID 1), 100 tranches, puis démasqué", v == att,
                        v, att, hw);
        });
        bad += AvecMb(() =>
        {
            Remplir(1);
            CanalAha(masque: false, mode: 0x40);
            Poster(CcbLu(), 200);
            var v = $"MBI {mem.mem_readb_phys(R9Aha.Mbox + 4):X2}";
            var att = hw ? "MBI 00" : "MBI 01";
            return Voir("le canal démasqué en mode simple (40h), hors cascade : READ(10) posté, 200 tranches (Q28 (A))",
                        v == att, v, att, hw);
        });
        bad += AvecMb(() =>
        {
            CanalAha(masque: true);
            R9Aha.Cmd(0x04);
            var st = R9Aha.Hacc();
            var v = (st & 0x01) == 0 ? "HACC, sans erreur" : $"état {st:X2}";
            return Voir("le canal masqué : Adapter Inquiry (04h), qui ne prend pas le bus (témoin)",
                        v == "HACC, sans erreur", v, "HACC, sans erreur", hw);
        });
        bad += AvecMb(() =>
        {
            CanalAha(masque: true);
            io.outb(R9Aha.P, 0x80);
            pc.runpc();
            var v = $"{R9Aha.Etat():X2}";
            return Voir("le canal masqué : HRST, une tranche, l'état (témoin : le port de contrôle n'est pas gardé)",
                        v == "30", v, "30", hw);
        });
        // Le canal 0 (l'EEPROM de la carte le permet ; posé ici sur la carte, comme 22h le ferait), en cascade et
        // démasqué, le canal 4 masqué : sous PB-253, le 8237 bas n'a pas le bus, et la carte attend ; sans PB-253, le
        // canal 0 suffit.
        bad += AvecMb(() =>
        {
            Remplir(1);
            Scsi.scsi_aha1540.courante()!.dma = 0;
            CanalAha(masque: false);
            io.outb(0xd4, 0x04);
            Poster(CcbLu(), 200);
            var v = $"MBI {mem.mem_readb_phys(R9Aha.Mbox + 4):X2}";
            var att = hw && materiel.pb_253 ? "MBI 00" : "MBI 01";
            return Voir("le canal 0 en cascade, démasqué, le canal 4 masqué : READ(10) posté (sous PB-253, la cascade)",
                        v == att, v, att, hw);
        });
        if (materiel.pb_157)
        {
            bad += AvecMb(() =>
            {
                Remplir(1);
                io.outb(0xd0, 0x04);
                Poster(CcbLu(), 200);
                var avant = $"MBI {mem.mem_readb_phys(R9Aha.Mbox + 4):X2}";
                io.outb(0xd0, 0x00);
                Tourner(200);
                var v = $"{avant} ; réactivé : MBI {mem.mem_readb_phys(R9Aha.Mbox + 4):X2}";
                var att = hw ? "MBI 00 ; réactivé : MBI 01" : "MBI 01 ; réactivé : MBI 01";
                return Voir("le 8237 haut désactivé (D0h = 04h, rangé sous PB-157) : READ(10) posté, puis réactivé",
                            v == att, v, att, hw);
            });
        }
        return bad;
    }
}
