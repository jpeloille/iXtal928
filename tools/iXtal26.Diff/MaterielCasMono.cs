// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — outillage de vérification.
//
// Les cas de la MDA et de l'Hercules (G13.9b, PLAN-G13.md) : PB-102 (le caractère de 9 points, 16 en graphique sur
// l'Hercules), PB-97 (les registres du 6845 à leur largeur, sans le correctif du « Turbo XT »), PB-96 (la police à la
// rangée sc & 15), PB-222 (le curseur qui force ses points), PB-223 (le curseur sur 16 trames) et PB-225 (3BAh bit 7
// de l'Hercules). Le banc : une IBM XT montée avec la carte, sans BIOS lancé ; les registres par les ports, les temps
// d'une ligne lus dans la carte (dispontime + dispofftime, en µs par TIMER_USEC) ; une ligne de texte ou une fin de
// rangée jouée par un appel de mda_poll sur un état posé à la main, les points lus dans buffer32.

using iXtal26.Video;

namespace iXtal26.Diff;

internal static partial class MaterielCas
{
    /// <summary>Un cas sur une IBM XT montée avec la carte <paramref name="carte"/> (mda ou hercules) : le corps, rendu
    /// face à l'attendu de PCem ou du matériel.</summary>
    private static int UnMono(string nom, string carte, Func<string> corps, string pcem, string mat, bool hw,
                              bool temoin = false) => AvecCd(d =>
    {
        var cfg = Path.Combine(d, "mono.cfg");
        File.WriteAllText(cfg, $"model = ibmxt\nmem_size = 640\ngfxcard = {carte}\nsndcard = none\n");
        try
        {
            if (!pc.loadconfig(cfg))
                throw new InvalidOperationException("loadconfig");
            Floppy.fdd_c.discfns[0] = Floppy.fdd_c.discfns[1] = "";
            if (!pc.initpc(RomsBanc))
                throw new InvalidOperationException("initpc");
            var v = corps();
            var att = temoin || hw ? mat : pcem;
            return Voir(nom, v == att, v, att, hw);
        }
        finally
        {
            try { pc.closepc(); } catch (Exception) { }
        }
    });

    private static mda_t Mda() => vid_mda.Probe ?? throw new InvalidOperationException("pas de MDA après initpc");

    private static hercules_t Herc() => vid_hercules.Probe ?? throw new InvalidOperationException("pas d'Hercules");

    private static void Crtc6845(int reg, int val)
    {
        io.outb(0x3b4, (byte)reg);
        io.outb(0x3b5, (byte)val);
    }

    private static string Ligne(ulong on, ulong off) => $"ligne={(double)(on + off) / timer.TIMER_USEC:F3}µs";

    // PB-102 — la MDA dessine des caractères de 9 points à 16,257 MHz (IBM, p. 2, schéma feuille 3) ; l'Hercules,
    // 0,5625 µs par caractère en texte et 1 µs en graphique (GB101, p. 10). PCem compte 8 points à 16,257 MHz (MDACONST).
    // Les cas de l'entrée : la MDA en mode 7 (R0 = 61h, R1 = 50h) : 48,23 µs chez PCem, 54,25 sur la carte ; l'Hercules en
    // graphique (R0 = 35h, R1 = 2Dh, 3BFh = 01h, 3B8h = 02h) : 26,57 µs contre 54,00, que le passage au graphique après
    // l'écriture du CRTC recalcule aussi, 3B8h ou 3BFh écrit le dernier ; l'Hercules en texte (R0 = 61h) : 48,23 contre 55,125.
    private static int Pb102(bool hw)
    {
        var bad = UnMono("MDA : R0 = 61h, R1 = 50h (le mode 7)", "mda", () =>
        {
            Crtc6845(1, 0x50);
            Crtc6845(0, 0x61);
            return Ligne(Mda().dispontime, Mda().dispofftime);
        }, "ligne=48.225µs", "ligne=54.254µs", hw);
        bad += UnMono("Hercules : R0 = 61h, R1 = 50h (le texte)", "hercules", () =>
        {
            Crtc6845(1, 0x50);
            Crtc6845(0, 0x61);
            return Ligne(Herc().dispontime, Herc().dispofftime);
        }, "ligne=48.225µs", "ligne=55.125µs", hw);
        bad += UnMono("Hercules : 3BFh = 01h, 3B8h = 02h, puis R0 = 35h, R1 = 2Dh (le graphique)", "hercules", () =>
        {
            io.outb(0x3bf, 0x01);
            io.outb(0x3b8, 0x02);
            Crtc6845(1, 0x2d);
            Crtc6845(0, 0x35);
            return Ligne(Herc().dispontime, Herc().dispofftime);
        }, "ligne=26.573µs", "ligne=54.000µs", hw);
        bad += UnMono("Hercules : R0 = 35h, R1 = 2Dh, puis 3BFh = 01h et 3B8h = 02h", "hercules", () =>
        {
            Crtc6845(1, 0x2d);
            Crtc6845(0, 0x35);
            io.outb(0x3bf, 0x01);
            io.outb(0x3b8, 0x02);
            return Ligne(Herc().dispontime, Herc().dispofftime);
        }, "ligne=26.573µs", "ligne=54.000µs", hw);
        bad += UnMono("Hercules : R0 = 35h, R1 = 2Dh, puis 3B8h = 02h et 3BFh = 01h", "hercules", () =>
        {
            Crtc6845(1, 0x2d);
            Crtc6845(0, 0x35);
            io.outb(0x3b8, 0x02);
            io.outb(0x3bf, 0x01);
            return Ligne(Herc().dispontime, Herc().dispofftime);
        }, "ligne=26.573µs", "ligne=54.000µs", hw);
        return bad;
    }

    // PB-97 (a) — le MC6845 tient R4, R6, R7 et R10 sur 7 bits, R5, R9 et R11 sur 5, R8 sur 6, R12 et R14 sur 6 (data
    // sheet), R3 sur 4 ; le BIOS d'IBM envoie le curseur tel quel à R10-R11 : 0607h reste 06h-07h (PCem le réécrit en 0Bh-0Ch, pour
    // un « Generic Turbo XT »). La relecture rend la valeur écrite dans les deux modes (la vraie, inconnue) : le témoin.
    private static int Pb97(bool hw)
    {
        string Regs(Func<byte[]> crtc, params (int r, int v)[] ecrits)
        {
            foreach (var (r, v) in ecrits)
                Crtc6845(r, v);
            var c = crtc();
            return string.Join(" ", ecrits.Select(e => $"R{e.r}={c[e.r]:X2}"));
        }
        var bad = UnMono("MDA : R10 = 06h, puis R11 = 07h", "mda", () => Regs(() => Mda().crtc, (10, 0x06), (11, 0x07)),
                         "R10=0B R11=0C", "R10=06 R11=07", hw);
        bad += UnMono("Hercules : R10 = 06h, puis R11 = 07h", "hercules",
                      () => Regs(() => Herc().crtc, (10, 0x06), (11, 0x07)), "R10=0B R11=0C", "R10=06 R11=07", hw);
        bad += UnMono("MDA : R3 = FFh, R4 = 99h, R9 = 2Dh, R8 = FFh, R14 = FFh", "mda",
                      () => Regs(() => Mda().crtc, (3, 0xff), (4, 0x99), (9, 0x2d), (8, 0xff), (14, 0xff)),
                      "R3=FF R4=99 R9=2D R8=FF R14=FF", "R3=0F R4=19 R9=0D R8=F3 R14=3F", hw);
        bad += UnMono("Hercules : R6 = F0h, R7 = 80h, R12 = C1h", "hercules",
                      () => Regs(() => Herc().crtc, (6, 0xf0), (7, 0x80), (12, 0xc1)), "R6=F0 R7=80 R12=C1",
                      "R6=70 R7=00 R12=01", hw);
        bad += UnMono("MDA : R4 = 99h, puis R4 relu en 3B5h (témoin)", "mda", () =>
        {
            Crtc6845(4, 0x99);
            return $"relu={io.inb(0x3b5):X2}";
        }, "", "relu=99", hw, temoin: true);
        bad += UnMono("Hercules : R4 = 99h, puis R4 relu en 3B5h (témoin)", "hercules", () =>
        {
            Crtc6845(4, 0x99);
            return $"relu={io.inb(0x3b5):X2}";
        }, "", "relu=99", hw, temoin: true);
        bad += UnMono("MDA : R3 = FFh (témoin de la relecture : la valeur écrite)", "mda", () =>
        {
            Crtc6845(3, 0xff);
            return $"relu={io.inb(0x3b5):X2}";
        }, "", "relu=FF", hw, temoin: true);
        bad += UnMono("MDA : R10 = 06h, puis R11 = 08h (témoin)", "mda", () => Regs(() => Mda().crtc, (10, 0x06), (11, 0x08)),
                      "", "R10=06 R11=08", hw, temoin: true);
        return bad;
    }

    /// <summary>Une ligne de texte de la MDA, un caractère, jouée par mda_poll (l'état linepos = 0, l'affichage actif) :
    /// les neuf points, 1 pour l'avant-plan de l'attribut, 0 pour le fond.</summary>
    private static string LigneMda(byte chr, byte attr, int sc, bool curseur)
    {
        var m = Mda();
        Crtc6845(1, 1);
        Crtc6845(14, 0);
        Crtc6845(15, curseur ? 0 : 1);
        m.vram[0] = chr;
        m.vram[1] = attr;
        (m.linepos, m.dispon, m.sc, m.ma, m.displine, m.firstline) = (0, 1, sc, 0, 10, 0);
        (m.con, m.cursoron, m.blink) = (curseur ? 1 : 0, curseur ? 1 : 0, 0);
        vid_mda.mda_poll(m);
        var fg = Video.video.cgapal[7];
        var p = new char[9];
        for (var c = 0; c < 9; c++)
            p[c] = Video.video.Buffer32[10 * Video.video.Stride + c] == fg ? '1' : '0';
        return $"points={new string(p)}";
    }

    /// <summary>Les huit points d'une rangée de la police monochrome, et le neuvième (nul hors de C0h-DFh).</summary>
    private static string PointsPolice(int chr, int rangee)
    {
        var b = vid_mda.fontdatm_plat(chr, rangee);
        var p = new char[9];
        for (var c = 0; c < 8; c++)
            p[c] = (b & (1 << (c ^ 7))) != 0 ? '1' : '0';
        p[8] = '0';
        return $"points={new string(p)}";
    }

    // PB-96 (a) — quatre lignes RA vont au générateur de caractères de la MDA (IBM, p. 3) : la ligne 19 d'une cellule est
    // la rangée 3 du caractère. PCem lit la rangée 19 du tableau à plat, la rangée 3 du caractère suivant. Le cas de
    // l'entrée, à la rangée 19 (le haut des deux caractères est vide) : 41h (A), PCem dessine la rangée 3 de 42h (B).
    // Témoin : la rangée 3 elle-même.
    private static int Pb96(bool hw)
    {
        var bad = UnMono("MDA : 41h, attribut 07h, à la ligne 19 de la cellule", "mda", () =>
        {
            var (b, a) = (PointsPolice(0x42, 3), PointsPolice(0x41, 3));
            if (a == b)
                throw new InvalidOperationException("les rangées 3 de 41h et de 42h sont égales : le cas ne discrimine pas");
            var v = LigneMda(0x41, 0x07, 19, false);
            return v == a ? "la rangée 3 de 41h" : v == b ? "la rangée 3 de 42h" : v;
        }, "la rangée 3 de 42h", "la rangée 3 de 41h", hw);
        bad += UnMono("MDA : 41h, attribut 07h, à la ligne 3 de la cellule (témoin)", "mda", () =>
        {
            var v = LigneMda(0x41, 0x07, 3, false);
            return v == PointsPolice(0x41, 3) ? "la rangée 3 de 41h" : v;
        }, "", "la rangée 3 de 41h", hw, temoin: true);
        return bad;
    }

    // PB-222 (a) — le curseur de la MDA force les points de la cellule à 1 (feuille 5, U3 dans le OU U43) : un pavé plein,
    // de la couleur d'avant-plan. PCem inverse la cellule. Le cas de l'entrée : DBh (le pavé plein), attribut 07h, sous le
    // curseur : PCem noir, la carte l'avant-plan ; et 20h (l'espace) : l'avant-plan dans les deux modes. Témoin : 41h
    // hors du curseur.
    private static int Pb222(bool hw)
    {
        var bad = UnMono("MDA : DBh, attribut 07h, sous le curseur, ligne 12", "mda", () => LigneMda(0xdb, 0x07, 12, true),
                         "points=000000000", "points=111111111", hw);
        bad += UnMono("MDA : 20h, attribut 07h, sous le curseur, ligne 12", "mda", () => LigneMda(0x20, 0x07, 12, true),
                      "points=111111111", "points=111111111", hw);
        bad += UnMono("MDA : 41h, attribut 07h, hors du curseur, ligne 3 (témoin)", "mda", () =>
        {
            var v = LigneMda(0x41, 0x07, 3, false);
            return v == PointsPolice(0x41, 3) ? "la rangée 3 de 41h" : v;
        }, "", "la rangée 3 de 41h", hw, temoin: true);
        return bad + Pb222Cga(hw);
    }

    // PB-223 (a) — le curseur de la MDA clignote sur 16 trames, les caractères sur 32 (feuille 5, U28) : le curseur suit
    // le bit 3 du compteur de trames, PCem le bit 4. Une fin de trame jouée par mda_poll (l'état linepos = 1, la dernière
    // rangée de la dernière ligne de caractères) : le compteur à 8, le curseur éteint chez PCem et allumé sur la carte ;
    // à 16, l'inverse. Témoin : R10 bits 5-6 = 01 (pas de curseur), éteint dans les deux modes.
    private static int Pb223(bool hw)
    {
        string Fin(int blink, int r10)
        {
            var m = Mda();
            Crtc6845(4, 0x19);
            Crtc6845(7, 0x7f);
            Crtc6845(9, 0x0d);
            Crtc6845(10, r10);
            (m.linepos, m.vadj, m.sc, m.vc, m.blink, m.cursoron) = (1, 0, 0x0d, 0x19, blink, 0);
            vid_mda.mda_poll(m);
            return $"curseur={(m.cursoron != 0 ? "allumé" : "éteint")}";
        }
        var bad = UnMono("MDA : fin de trame, le compteur de trames à 8", "mda", () => Fin(8, 0x0b), "curseur=éteint",
                         "curseur=allumé", hw);
        bad += UnMono("MDA : fin de trame, le compteur de trames à 16", "mda", () => Fin(16, 0x0b), "curseur=allumé",
                      "curseur=éteint", hw);
        bad += UnMono("MDA : fin de trame, le compteur à 8, R10 = 2Bh (pas de curseur, témoin)", "mda", () => Fin(8, 0x2b),
                      "", "curseur=éteint", hw, temoin: true);
        return bad + Pb223Cga(hw);
    }

    // PB-225 (a) — 3BAh bit 7 de l'Hercules : « 0 = vertical retrace … 1 = active display » (GB101, pp. 12-13). PCem y
    // recopie le bit 3, à 1 pendant le retour. Le retour posé à la main (le bit 3 de stat) et levé.
    private static int Pb225(bool hw)
    {
        string Lu(bool retour)
        {
            var h = Herc();
            h.stat = (byte)(retour ? 0x09 : 0x00);
            return $"3BAh={io.inb(0x3ba):X2}";
        }
        var bad = UnMono("Hercules : 3BAh pendant le retour vertical", "hercules", () => Lu(true), "3BAh=89", "3BAh=09", hw);
        bad += UnMono("Hercules : 3BAh pendant l'affichage", "hercules", () => Lu(false), "3BAh=00", "3BAh=80", hw);
        return bad;
    }
}
