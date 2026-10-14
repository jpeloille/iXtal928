// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — outillage de vérification.
//
// Les cas de l'ET4000AX et de son RAMDAC (G13.9e et 9e2, PLAN-G13.md) : PB-227 (CR35 sous CR11 bit 7), PB-100 (CR13 nul
// vaut 0 ; le KEY et ce qu'il garde) et PB-229 (l'IPF du RAMDAC Sierra tient), sur le banc du socle
// (MaterielCasVideo.cs : une ami486 montée avec la carte, sans BIOS lancé, les registres par les ports).

using iXtal26.Video;

namespace iXtal26.Diff;

internal static partial class MaterielCas
{
    // PB-227 — CR35, le débordement vertical de l'ET4000, est « protected by bit 7 of CRTC 11 » (data book Tseng, p. 111,
    // table 4.3-2). Le cas de l'entrée : CR35 = 00h, CR11 = 80h, puis CR35 = 02h : PCem 02h (vtotal + 400h), l'ET4000
    // garde 00h. Témoins : CR11 = 00h ; CR33 sous CR11 bit 7, qui s'écrit.
    private static int Pb227(bool hw)
    {
        string Cr35(svga_t s, int cr11)
        {
            Crtc(0x35, 0x00);
            Crtc(0x11, cr11);
            Crtc(0x35, 0x02);
            return $"CR35={s.crtc[0x35]:X2}";
        }
        var bad = UnSvga("et4000ax : CR35 = 00h, CR11 = 80h, puis CR35 = 02h", "et4000ax", s => Cr35(s, 0x80), "CR35=02",
                         "CR35=00", hw);
        bad += UnSvga("et4000ax : CR35 = 00h, CR11 = 00h, puis CR35 = 02h (témoin)", "et4000ax", s => Cr35(s, 0x00), "",
                      "CR35=02", hw, temoin: true);
        bad += UnSvga("et4000ax : CR11 = 80h, puis CR33 = 01h (un registre que CR11 ne protège pas, témoin)", "et4000ax", s =>
        {
            Crtc(0x11, 0x80);
            Crtc(0x33, 0x01);
            return $"CR33={s.crtc[0x33]:X2}";
        }, "", "CR33=01", hw, temoin: true);
        return bad;
    }

    // PB-100, CR13 — un CR13 nul vaut 0 sur l'ET4000 (la même rangée répétée), 256 chez PCem. CR13 = 01h puis 00h ;
    // témoin, 28h.
    private static int Pb100(bool hw)
    {
        string Cr13(svga_t s, int v)
        {
            Crtc(0x13, 0x01);
            Crtc(0x13, v);
            return $"rowoffset={s.rowoffset}";
        }
        var bad = UnSvga("et4000ax : CR13 = 01h, puis 00h", "et4000ax", s => Cr13(s, 0x00), "rowoffset=256", "rowoffset=0",
                         hw);
        bad += UnSvga("et4000ax : CR13 = 01h, puis 28h (témoin)", "et4000ax", s => Cr13(s, 0x28), "", "rowoffset=40", hw,
                      temoin: true);
        return bad + Pb100Cle(hw);
    }

    // PB-100, le KEY — 03h en 3BFh, puis A0h en 3#8 (3D8h en couleur, 3B8h en mono) le pose (data book Tseng, p. 102) ;
    // 01h en 3BFh, puis 29h en 3#8 le retire (la routine du BIOS de la carte, en 1050h). Sans lui, les registres du CRTC
    // au-delà de 18h ne s'écrivent pas, sauf 33h et 35h (p. 111), ni TS 6 et TS 7 (p. 142), ni l'ATC 16h (p. 158) ; 3CDh
    // ne s'écrit qu'après une pose depuis la mise sous tension (p. 144). PCem écrit tout. L'état du KEY à la mise sous
    // tension est inconnu : jusqu'au premier retrait, tout s'écrit (un témoin) ; « sans KEY », c'est après un retrait.
    private static int Pb100Cle(bool hw)
    {
        void Pose(int herc, int mode, int port = 0x3d8)
        {
            io.outb(0x3bf, (byte)herc);
            io.outb((ushort)port, (byte)mode);
        }
        void Retrait() => Pose(0x01, 0x29);
        string Cr(svga_t s, params int[] r) => string.Join(" ", r.Select(i => $"CR{i:X2}={s.crtc[i]:X2}"));
        string Etendus(svga_t s)
        {
            foreach (var i in new[] { 0x18, 0x19, 0x30, 0x31, 0x32, 0x33, 0x34, 0x35, 0x36, 0x37 })
                Crtc(i, 0xff);
            return Cr(s, 0x18, 0x19, 0x30, 0x31, 0x32, 0x33, 0x34, 0x35, 0x36, 0x37);
        }
        var bad = UnSvga("et4000ax : à la mise sous tension, CR18, CR19 et CR30-CR37 = FFh (témoin)", "et4000ax", Etendus, "",
                         "CR18=FF CR19=00 CR30=FF CR31=FF CR32=FF CR33=0F CR34=FF CR35=FF CR36=FF CR37=FF", hw, temoin: true);
        bad += UnSvga("et4000ax : sans KEY (01h en 3BFh, 29h en 3D8h), CR18, CR19 et CR30-CR37 = FFh", "et4000ax", s =>
        {
            Retrait();
            return Etendus(s);
        }, "CR18=FF CR19=00 CR30=FF CR31=FF CR32=FF CR33=0F CR34=FF CR35=FF CR36=FF CR37=FF",
           "CR18=FF CR19=00 CR30=00 CR31=00 CR32=00 CR33=0F CR34=00 CR35=FF CR36=00 CR37=00", hw);
        bad += UnSvga("et4000ax : le KEY retiré, puis posé (03h en 3BFh, A0h en 3D8h), CR18, CR19 et CR30-CR37 = FFh (témoin)",
                      "et4000ax", s =>
        {
            Retrait();
            Pose(0x03, 0xa0);
            return Etendus(s);
        }, "", "CR18=FF CR19=00 CR30=FF CR31=FF CR32=FF CR33=0F CR34=FF CR35=FF CR36=FF CR37=FF", hw, temoin: true);
        bad += UnSvga("et4000ax : le KEY posé, puis retiré (01h en 3BFh, 29h en 3D8h), CR36 = 5Ah", "et4000ax", s =>
        {
            Pose(0x03, 0xa0);
            Retrait();
            Crtc(0x36, 0x5a);
            return Cr(s, 0x36);
        }, "CR36=5A", "CR36=00", hw);
        bad += UnSvga("et4000ax : le KEY retiré ; 03h en 3BFh, puis 00h, puis A0h en 3D8h ; CR36 = 5Ah", "et4000ax", s =>
        {
            Retrait();
            io.outb(0x3bf, 0x03);
            Pose(0x00, 0xa0);
            Crtc(0x36, 0x5a);
            return Cr(s, 0x36);
        }, "CR36=5A", "CR36=00", hw);
        // Les écritures de 3#8 hors des deux séquences laissent le KEY tel quel : 29h sous 3BFh = 03h, 00h ; l'ordre
        // inverse (A0h, puis 03h en 3BFh) ne pose rien.
        bad += UnSvga("et4000ax : le KEY posé, puis 29h et 00h en 3D8h (3BFh à 03h) ; CR36 = 5Ah (témoin)", "et4000ax", s =>
        {
            Retrait();
            Pose(0x03, 0xa0);
            io.outb(0x3d8, 0x29);
            io.outb(0x3d8, 0x00);
            Crtc(0x36, 0x5a);
            return Cr(s, 0x36);
        }, "", "CR36=5A", hw, temoin: true);
        bad += UnSvga("et4000ax : le KEY retiré ; A0h en 3D8h, puis 03h en 3BFh ; CR36 = 5Ah", "et4000ax", s =>
        {
            Retrait();
            Pose(0x01, 0xa0);
            io.outb(0x3bf, 0x03);
            Crtc(0x36, 0x5a);
            return Cr(s, 0x36);
        }, "CR36=5A", "CR36=00", hw);
        bad += UnSvga("et4000ax : sans KEY, TS 6 et TS 7 = 5Ah", "et4000ax", s =>
        {
            Retrait();
            Seq(0x06, 0x5a);
            Seq(0x07, 0x5a);
            return $"TS6={s.seqregs[6]:X2} TS7={s.seqregs[7]:X2}";
        }, "TS6=5A TS7=5A", "TS6=00 TS7=00", hw);
        string Atc(svga_t s)
        {
            io.inb(0x3da);
            io.outb(0x3c0, 0x16);
            io.outb(0x3c0, 0x20);
            io.outb(0x3c0, 0x00);
            io.outb(0x3c0, 0x01);
            return $"ATC16={s.attrregs[0x16]:X2} ATC00={s.attrregs[0]:X2}";
        }
        bad += UnSvga("et4000ax : sans KEY, l'ATC 16h = 20h, puis l'ATC 00h = 01h", "et4000ax", s =>
        {
            Retrait();
            return Atc(s);
        }, "ATC16=20 ATC00=01", "ATC16=00 ATC00=01", hw);
        bad += UnSvga("et4000ax : le KEY retiré, puis posé, l'ATC 16h = 20h, puis l'ATC 00h = 01h (témoin)", "et4000ax", s =>
        {
            Retrait();
            Pose(0x03, 0xa0);
            return Atc(s);
        }, "", "ATC16=20 ATC00=01", hw, temoin: true);
        string Banque(svga_t s)
        {
            io.outb(0x3cd, 0x35);
            return $"3CD={io.inb(0x3cd):X2} banques={s.read_bank >> 16:X}/{s.write_bank >> 16:X}";
        }
        bad += UnSvga("et4000ax : à la mise sous tension, avant toute pose, 3CDh = 35h", "et4000ax", Banque,
                      "3CD=35 banques=3/5", "3CD=00 banques=0/0", hw);
        bad += UnSvga("et4000ax : le KEY posé, puis retiré, 3CDh = 35h (témoin)", "et4000ax", s =>
        {
            Pose(0x03, 0xa0);
            Retrait();
            return Banque(s);
        }, "", "3CD=35 banques=3/5", hw, temoin: true);
        // Le reset synchrone après la pose, puis le KEY retiré, comme le changement de mode du BIOS de la carte : 3CDh
        // reste ouvert (la règle du data book, à la lettre, contredit ce BIOS ; inconnue, elle reste celle de PCem).
        bad += UnSvga("et4000ax : le KEY posé, le reset synchrone (TS 0 = 01h, puis 03h), le KEY retiré, 3CDh = 35h (témoin)",
                      "et4000ax", s =>
        {
            Pose(0x03, 0xa0);
            Seq(0x00, 0x01);
            Seq(0x00, 0x03);
            Retrait();
            return Banque(s);
        }, "", "3CD=35 banques=3/5", hw, temoin: true);
        // En mono (3C2h = 62h), le CRTC en 3B4h-3B5h et le registre de mode en 3B8h ; le KEY retiré en 3B8h.
        string Mono(svga_t s, int port)
        {
            io.outb(0x3c2, 0x62);
            Pose(0x01, 0x29, 0x3b8);
            Pose(0x03, 0xa0, port);
            io.outb(0x3b4, 0x36);
            io.outb(0x3b5, 0x5a);
            return Cr(s, 0x36);
        }
        bad += UnSvga("et4000ax en mono : 03h en 3BFh, A0h en 3D8h (l'adresse couleur) ; CR36 = 5Ah", "et4000ax",
                      s => Mono(s, 0x3d8), "CR36=5A", "CR36=00", hw);
        bad += UnSvga("et4000ax en mono : 03h en 3BFh, A0h en 3B8h ; CR36 = 5Ah (témoin)", "et4000ax", s => Mono(s, 0x3b8), "",
                      "CR36=5A", hw, temoin: true);
        bad += UnSvga("et4000ax en mono : le KEY posé, puis retiré en 3B8h ; CR36 = 5Ah", "et4000ax", s =>
        {
            Mono(s, 0x3b8);
            Pose(0x01, 0x29, 0x3b8);
            io.outb(0x3b4, 0x36);
            io.outb(0x3b5, 0x00);
            return Cr(s, 0x36);
        }, "CR36=00", "CR36=5A", hw);
        return bad;
    }

    // PB-229 — l'IPF du RAMDAC Sierra, armé par quatre lectures de 3C6h, ne retombe qu'à une écriture ou à la lecture
    // d'une autre adresse (data sheet SC15025/26, pp. 3-88/3-89). Le registre de commande posé à 20h, puis six lectures
    // de 3C6h de suite : la cinquième rend 20h dans les deux modes ; la sixième, chez PCem, le masque des points (00h,
    // sans BIOS lancé), sur la Sierra encore 20h. Témoin : une lecture de 3C8h entre la cinquième et la sixième, qui fait
    // retomber l'IPF.
    private static int Pb229(bool hw)
    {
        string Lectures(bool autre)
        {
            for (var i = 0; i < 4; i++)
                io.inb(0x3c6);
            io.outb(0x3c6, 0x20);
            for (var i = 0; i < 4; i++)
                io.inb(0x3c6);
            var cinquieme = io.inb(0x3c6);
            if (autre)
                io.inb(0x3c8);
            var sixieme = io.inb(0x3c6);
            return $"5e={cinquieme:X2} 6e={sixieme:X2}";
        }
        var bad = UnSvga("et4000ax : le registre de commande à 20h, six lectures de 3C6h", "et4000ax", _ => Lectures(false),
                         "5e=20 6e=00", "5e=20 6e=20", hw);
        bad += UnSvga("et4000ax : de même, 3C8h lu avant la sixième (témoin)", "et4000ax", _ => Lectures(true), "",
                      "5e=20 6e=00", hw, temoin: true);
        return bad;
    }
}
