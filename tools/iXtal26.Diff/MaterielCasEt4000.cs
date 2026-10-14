// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — outillage de vérification.
//
// Les cas de l'ET4000AX et de son RAMDAC (G13.9e, PLAN-G13.md) : PB-227 (CR35 sous CR11 bit 7), PB-100 (CR13 nul vaut 0)
// et PB-229 (l'IPF du RAMDAC Sierra tient), sur le banc du socle (MaterielCasVideo.cs : une ami486 montée avec la carte,
// sans BIOS lancé, les registres par les ports).

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
