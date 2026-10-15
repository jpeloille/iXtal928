// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — outillage de vérification.
//
// Les cas du faisceau (G13.9f, PLAN-G13.md) : le point sous le faisceau à l'instant d'une lecture de l'état de la carte
// — PB-99 (4), 3DAh bits 4-5 de l'EGA, et (8), 3C1h ; PB-228, 3C2h bit 7 de l'EGA ; PB-224, 3BAh bit 3 de la MDA et de
// l'Hercules ; PB-226, 3DAh bits 4-5 et 7 de l'ET4000. Le banc : la machine montée avec la carte, sans BIOS lancé ; les
// registres par les ports ; la ligne posée à la main (sa phase, sa ligne, son compteur vertical), la minuterie de la
// carte réglée pour que la lecture tombe à une part donnée de la phase. POD14, le test du BIOS de l'EGA, est dans
// MaterielCasEga.cs (Pod14).

using iXtal26.Video;

namespace iXtal26.Diff;

internal static partial class MaterielCas
{
    /// <summary>Deux lectures du port <paramref name="port"/>, le masque <paramref name="masque"/>, à la part <paramref
    /// name="f"/> de la phase de la ligne (l'affichage, linepos à 0, ou le reste) : la minuterie de la carte réglée à
    /// la durée qui reste. Deux valeurs différentes au même instant : « bascule » (celle de PCem, dont la phase suit
    /// les lectures d'avant).</summary>
    private static string DeuxLectures(pc_timer_t minuterie, ulong duree, double f, int port, int masque)
    {
        timer.timer_set_delay_u64(minuterie, (ulong)((1 - f) * duree));
        var a = io.inb((ushort)port) & masque;
        var b = io.inb((ushort)port) & masque;
        return a == b ? $"{a:X2} {b:X2}" : "bascule";
    }

    /// <summary>L'EGA en texte de 80 colonnes, le mode 3 de 350 lignes (CR00 = 5Bh, CR01 = 4Fh, le blanc de 83 à 86,
    /// CR15 = 5Eh et CR16 = 0Ah : le blanc vertical de 350 à 361, le total de 365 lignes), des caractères de 8 points ;
    /// la rangée : les colonnes 0 à 39, l'attribut 40h (le fond, la palette 4), les colonnes 40 à 79, 00h ; la police
    /// nulle.</summary>
    private static void EgaTexte(ega_t e)
    {
        foreach (var (r, v) in new[] { (0x00, 0x5b), (0x01, 0x4f), (0x02, 0x53), (0x03, 0x37), (0x04, 0x51),
                                       (0x05, 0x5b), (0x06, 0x6c), (0x07, 0x1f), (0x09, 0x0d), (0x10, 0x5e),
                                       (0x11, 0x2b), (0x12, 0x5d), (0x13, 0x28), (0x15, 0x5e), (0x16, 0x0a),
                                       (0x17, 0xa3) })
            CrtcEga(r, v);
        Seq(0x01, 0x01);
        Gr(0x06, 0x0e);
        Attr(0x00, 0x00);
        for (var x = 0; x < 80; x++)
            (e.vram[x * 8], e.vram[x * 8 + 1]) = (0x00, (byte)(x < 40 ? 0x40 : 0x00));
        (e.dispon, e.displine, e.vc, e.sc, e.ma, e.con, e.scrblank) = (1, 10, 10, 0, 0, 0, 0);
    }

    /// <summary>Deux lectures de 3DAh, bits 4-5, à la part <paramref name="f"/> de l'affichage (ou du reste) de la
    /// ligne.</summary>
    private static string Ega3da(ega_t e, double f, bool affichage = true)
    {
        e.linepos = affichage ? 0 : 1;
        return DeuxLectures(e.timer, affichage ? e.dispontime : e.dispofftime, f, 0x3da, 0x30);
    }

    // PB-99 (4) — 3DAh bits 4-5, deux des six sorties de l'attribut, choisies par 12h bits 4-5 : 00 le rouge (P2) et le
    // bleu (P0), 01 le bleu secondaire (P3) et le vert (P1), 10 le rouge secondaire (P5) et le vert secondaire (P4), 11
    // « Not Used » (Options and Adapters, vol. 2, EGA, pp. 16-17 et 60). Deux lectures au même instant : PCem bascule
    // les bits à chaque lecture ; l'EGA rend deux fois le point sous le faisceau. Au quart de l'affichage (la colonne
    // 20, le fond de la palette 4) : la palette 4 à 05h, 0Ah et 30h sous 12h = 0Fh, 1Fh et 2Fh, 30h ; 04h et 01h sous
    // 0Fh, 20h et 10h ; aux trois quarts (la colonne 60, la palette 0), 00h. Hors de l'affichage, la bordure (11h =
    // 05h) : le caractère 81, 30h ; le caractère 84, dans le blanc, 00h ; aux bornes du blanc vertical (350 à 361), les
    // lignes 349 et 362, la bordure, 350 et 361, 00h. La colonne 20 écrite (egawrites) entre deux lectures : la seconde
    // voit le point neuf. Un blanc horizontal dans l'affichage (CR02 = 20h), l'écran éteint de SR1 : 00h. Le graphique
    // de 16 couleurs (la couleur 5 à 05h) : 30h, puis 00h. PCem bascule partout. Témoins : 12h = 3Fh, « Not Used », la
    // bascule de PCem dans les deux modes ; buffer32 rendu tel qu'il était après une lecture.
    private static int Faisceau99(bool hw)
    {
        var bad = 0;
        foreach (var (ar12, pal4, f, att) in new[] { (0x0f, 0x05, 0.25, "30 30"), (0x1f, 0x0a, 0.25, "30 30"),
                                                     (0x2f, 0x30, 0.25, "30 30"), (0x0f, 0x04, 0.25, "20 20"),
                                                     (0x0f, 0x01, 0.25, "10 10"), (0x0f, 0x05, 0.75, "00 00"),
                                                     (0x1f, 0x05, 0.25, "00 00") })
            bad += UnEga($"EGA : 12h = {ar12:X2}h, la palette 4 = {pal4:X2}h, 3DAh deux fois, à {f:0.##} de" +
                         " l'affichage", e =>
            {
                EgaTexte(e);
                Attr(0x04, pal4);
                Attr(0x12, ar12);
                return Ega3da(e, f);
            }, "bascule", att, hw);
        foreach (var (c, vc, att) in new[] { (81, 10, "30 30"), (84, 10, "00 00"), (81, 349, "30 30"),
                                             (81, 350, "00 00"), (81, 361, "00 00"), (81, 362, "30 30") })
            bad += UnEga($"EGA : 11h = 05h, hors de l'affichage, le caractère {c}, la ligne {vc}, 3DAh deux fois", e =>
            {
                EgaTexte(e);
                Attr(0x11, 0x05);
                Attr(0x12, 0x0f);
                e.vc = vc;
                e.stat |= 1;
                return Ega3da(e, (c - 80 + 0.5) / 13, affichage: false);
            }, "bascule", att, hw);
        bad += UnEga("EGA : la colonne 20 écrite entre deux lectures de 3DAh, au quart de l'affichage", e =>
        {
            EgaTexte(e);
            Attr(0x04, 0x05);
            Attr(0x12, 0x0f);
            e.linepos = 0;
            timer.timer_set_delay_u64(e.timer, (ulong)(0.75 * e.dispontime));
            var a = io.inb(0x3da) & 0x30;
            e.vram[20 * 8 + 1] = 0x00;
            Video.video.egawrites++;
            var b = io.inb(0x3da) & 0x30;
            return $"{a:X2} puis {b:X2}";
        }, "00 puis 30", "30 puis 00", hw);
        bad += UnEga("EGA : les lignes 10 à 12 de buffer32 après une lecture de 3DAh (témoin)", e =>
        {
            EgaTexte(e);
            Attr(0x04, 0x05);
            Attr(0x12, 0x0f);
            var b = Video.video.Buffer32;
            var st = Video.video.Stride;
            Array.Fill(b, MotifLigne, 10 * st, 3 * st);
            Ega3da(e, 0.25);
            return Enumerable.Range(10 * st, 3 * st).All(i => b[i] == MotifLigne) ? "intactes" : "écrites";
        }, "", "intactes", hw, temoin: true);
        bad += UnEga("EGA : CR02 = 20h, CR03 = 0Ah (le blanc de 32 à 41, dans l'affichage), 3DAh deux fois, le" +
                     " caractère 36", e =>
        {
            EgaTexte(e);
            CrtcEga(0x02, 0x20);
            CrtcEga(0x03, 0x0a);
            Attr(0x04, 0x05);
            Attr(0x12, 0x0f);
            return Ega3da(e, 36.5 / 80);
        }, "bascule", "00 00", hw);
        bad += UnEga("EGA : SR1 = 21h (l'écran éteint), la palette 4 = 05h, 3DAh deux fois, au quart de l'affichage",
                     e =>
        {
            EgaTexte(e);
            Seq(0x01, 0x21);
            Attr(0x04, 0x05);
            Attr(0x12, 0x0f);
            return Ega3da(e, 0.25);
        }, "bascule", "00 00", hw);
        foreach (var (f, att) in new[] { (0.25, "30 30"), (0.75, "00 00") })
            bad += UnEga("EGA : le graphique de 16 couleurs, les points de 0 à 319 à la couleur 5 (05h), 3DAh deux" +
                         $" fois, à {f:0.##} de l'affichage", e =>
            {
                EgaTexte(e);
                Gr(0x06, 0x05);
                Gr(0x05, 0x00);
                CrtcEga(0x17, 0xe3);
                Attr(0x05, 0x05);
                Attr(0x12, 0x0f);
                Array.Clear(e.vram, 0, 1024);
                for (var x = 0; x < 40; x++)
                    e.vram[x * 4] = e.vram[x * 4 + 2] = 0xff;
                return Ega3da(e, f);
            }, "bascule", att, hw);
        bad += UnEga("EGA : 12h = 3Fh (« Not Used »), la palette 4 = 3Fh, 3DAh deux fois (témoin)", e =>
        {
            EgaTexte(e);
            Attr(0x04, 0x3f);
            Attr(0x12, 0x3f);
            return Ega3da(e, 0.25);
        }, "", "bascule", hw, temoin: true);
        // (8) — 3C1h : le BIOS d'IBM écrit la donnée de l'attribut en DX + 1 (OUT_DX ; POD14, la palette 0Fh puis 12h).
        // PCem ignore 3C1h : la bascule reste sur la donnée, l'index 32h suivant devient la valeur de la palette 0Fh.
        bad += UnEga("EGA : 0Fh en 3C0h, 3Fh en 3C1h, puis 32h en 3C0h, 1Fh en 3C1h", e =>
        {
            io.inb(0x3da);
            io.outb(0x3c0, 0x0f);
            io.outb(0x3c1, 0x3f);
            io.outb(0x3c0, 0x32);
            io.outb(0x3c1, 0x1f);
            return $"0Fh={e.attrregs[0x0f]:X2} 12h={e.attrregs[0x12]:X2}";
        }, "0Fh=32 12h=00", "0Fh=3F 12h=1F", hw);
        return bad;
    }

    // PB-228 — 3C2h bit 7, « CRT Interrupt » : 1 pendant l'affichage, 0 pendant le retour vertical (Options and
    // Adapters, vol. 2, EGA, p. 15), l'interruption autorisée par CR11 bit 5 à 0 (pp. 36-37). L'interrupteur lu par le
    // bit 4 (3C2h écrit à 01h : le quatrième, fermé, 10h). PCem rend le bit 7 à 0. Témoins : le retour vertical (3DAh
    // bit 3) ; l'interruption interdite, inconnue.
    private static int Pb228(bool hw)
    {
        var bad = UnEga("EGA : CR11 = 0Bh (l'interruption autorisée), l'affichage, 3C2h", e =>
        {
            CrtcEga(0x11, 0x0b);
            e.stat = 0;
            return $"{io.inb(0x3c2):X2}";
        }, "10", "90", hw);
        bad += UnEga("EGA : CR11 = 0Bh, le retour vertical, 3C2h (témoin)", e =>
        {
            CrtcEga(0x11, 0x0b);
            e.stat = 9;
            return $"{io.inb(0x3c2):X2}";
        }, "", "10", hw, temoin: true);
        bad += UnEga("EGA : CR11 = 2Bh (l'interruption interdite), l'affichage, 3C2h (témoin)", e =>
        {
            CrtcEga(0x11, 0x2b);
            e.stat = 0;
            return $"{io.inb(0x3c2):X2}";
        }, "", "10", hw, temoin: true);
        return bad;
    }

    /// <summary>Une trame de la MDA ou de l'Hercules jouée ligne par ligne par la minuterie de la carte : à chaque
    /// affichage (linepos à 0), 3BAh lu au milieu de la phase ; le nombre de lignes où le bit 3 vaut 1.</summary>
    private static string Trame3ba(pc_timer_t minuterie, object carte, Func<int> linepos, Func<ulong> dispontime)
    {
        var n = 0;
        for (var i = 0; i < 2 * 1000; i++)
        {
            minuterie.callback!(carte);
            if (linepos() != 0)
                continue;
            var (ti, tf) = (minuterie.ts_integer, minuterie.ts_frac);
            timer.timer_set_delay_u64(minuterie, dispontime() / 2);
            if ((io.inb(0x3ba) & 8) != 0)
                n++;
            (minuterie.ts_integer, minuterie.ts_frac) = (ti, tf);
        }
        return $"{n} lignes";
    }

    // PB-224 — 3BAh bit 3 de la MDA, « +Black/White Video » (IBM PC TR 6025008, pp. 2-42/2-43 ; Options and Adapters,
    // vol. 2, MDA, p. 8 ; feuille 5, U54 : +ALPHA DOTS ⊕ RVV), et de l'Hercules, « 1 = dots on » (GB101, pp. 12-13) :
    // le point sous le faisceau. PCem y met le retour vertical, seize lignes. Le cas de l'entrée : le mode 7, un écran
    // d'espaces d'attribut 07h, le curseur éteint (R10 = 20h), 1 000 lignes jouées, 3BAh lu au milieu de l'affichage de
    // chacune : PCem compte les lignes du retour, la carte aucune ; l'attribut 70h (la vidéo inverse) : la carte,
    // chaque ligne affichée (la première trame est éteinte, l'affichage commence à son total ; puis 350 lignes et 261
    // de la suivante). L'Hercules de même, et en graphique (3BFh = 01h, 3B8h = 0Ah), la page 0 pleine. L'Hercules, 3BAh
    // bits 7 et 3 au milieu d'une ligne affichée de vidéo inverse : le bit 3 allumé ; le bit 7, celui de PB-225 sous
    // son drapeau (80h), sinon celui de PCem (00h).
    private static int Pb224(bool hw)
    {
        var bad = 0;
        void Mode7(Action<int, int> crtc)
        {
            foreach (var (r, v) in new[] { (0, 0x61), (1, 0x50), (2, 0x52), (3, 0x0f), (4, 0x19), (5, 0x06), (6, 0x19),
                                           (7, 0x19), (8, 0x02), (9, 0x0d), (10, 0x20), (11, 0x0c), (12, 0), (13, 0) })
                crtc(r, v);
        }
        foreach (var (attr, att) in new[] { (0x07, "0 lignes"), (0x70, "611 lignes") })
        {
            bad += UnMono($"MDA : le mode 7, des espaces d'attribut {attr:X2}h, le curseur éteint, mille lignes", "mda",
                          () =>
            {
                var m = Mda();
                Mode7(Crtc6845);
                io.outb(0x3b8, 0x08);
                for (var i = 0; i < 4096; i += 2)
                    (m.vram[i], m.vram[i + 1]) = (0x20, (byte)attr);
                return Trame3ba(m.timer, m, () => m.linepos, () => m.dispontime);
            }, "30 lignes", att, hw);
            bad += UnMono($"Hercules : le mode 7, des espaces d'attribut {attr:X2}h, le curseur éteint, mille lignes",
                          "hercules", () =>
            {
                var h = Herc();
                Mode7(Crtc6845);
                io.outb(0x3b8, 0x08);
                for (var i = 0; i < 4096; i += 2)
                    (h.vram[i], h.vram[i + 1]) = (0x20, (byte)attr);
                return Trame3ba(h.timer, h, () => h.linepos, () => h.dispontime);
            }, "30 lignes", att, hw);
        }
        bad += UnMono("Hercules : des espaces d'attribut 70h, 3BAh bits 7 et 3 au milieu d'une ligne affichée",
                      "hercules", () =>
        {
            var h = Herc();
            Mode7(Crtc6845);
            io.outb(0x3b8, 0x08);
            for (var i = 0; i < 4096; i += 2)
                (h.vram[i], h.vram[i + 1]) = (0x20, 0x70);
            for (var i = 0; i < 2000 && !(h.linepos == 0 && h.dispon != 0 && h.displine > 40); i++)
                h.timer.callback!(h);
            timer.timer_set_delay_u64(h.timer, h.dispontime / 2);
            return $"{io.inb(0x3ba) & 0x88:X2}";
        }, "00", iXtal26.materiel.pb_225 ? "88" : "08", hw);
        bad += UnMono("Hercules : le graphique (3BFh = 01h, 3B8h = 0Ah), la page 0 pleine, mille lignes", "hercules",
                      () =>
        {
            var h = Herc();
            foreach (var (r, v) in new[] { (0, 0x35), (1, 0x2d), (2, 0x2e), (3, 0x07), (4, 0x5b), (5, 0x02), (6, 0x57),
                                           (7, 0x57), (8, 0x02), (9, 0x03), (10, 0x00), (11, 0x00), (12, 0), (13, 0) })
                Crtc6845(r, v);
            io.outb(0x3bf, 0x01);
            io.outb(0x3b8, 0x0a);
            Array.Fill(h.vram, (byte)0xff, 0, 0x8000);
            return Trame3ba(h.timer, h, () => h.linepos, () => h.dispontime);
        }, "30 lignes", "609 lignes", hw);
        return bad;
    }

    /// <summary>L'ET4000 en texte de 80 colonnes (CR00 = 5Fh, CR01 = 4Fh, le blanc de 85 à 96 ou plus, CR15 = 96h et
    /// CR16 = B9h, le mode 3 de la VGA, mais CR17 = E3h, le mode octet, la colonne x en 4x), des caractères de 8
    /// points ; la rangée : les colonnes 0 à 39, l'attribut 40h, les colonnes 40 à 79, 00h ; la police nulle. Les
    /// registres de l'attribut de <paramref name="attributs"/>, puis l'index de l'ATC bit 5 à 1 (la palette à
    /// l'affichage) ; la ligne 10 dans l'affichage.</summary>
    private static void Et4Texte(svga_t s, params (int idx, int val)[] attributs)
    {
        foreach (var (r, v) in new[] { (0x00, 0x5f), (0x01, 0x4f), (0x02, 0x55), (0x03, 0x81), (0x04, 0x55),
                                       (0x05, 0x01), (0x06, 0xbf), (0x07, 0x1f), (0x09, 0x4f), (0x10, 0x9c),
                                       (0x11, 0x0e), (0x12, 0x8f), (0x13, 0x28), (0x15, 0x96), (0x16, 0xb9),
                                       (0x17, 0xe3) })
            Crtc(r, v);
        Seq(0x01, 0x01);
        Gr(0x06, 0x0e);
        Attr(0x00, 0x00);
        Attr(0x10, 0x00);
        foreach (var (idx, val) in attributs)
            Attr(idx, val);
        io.inb(0x3da);
        io.outb(0x3c0, 0x20);
        for (var x = 0; x < 80; x++)
            (s.vram[x * 4], s.vram[x * 4 + 1]) = (0x00, (byte)(x < 40 ? 0x40 : 0x00));
        vid_svga.svga_recalctimings(s);
        (s.dispon, s.displine, s.vc, s.sc, s.ma, s.con, s.cgastat) = (1, 10, 10, 0, 0, 0, 0);
    }

    private static string Et43da(svga_t s, double f, int masque, bool affichage = true)
    {
        s.linepos = affichage ? 0 : 1;
        return DeuxLectures(s.timer, affichage ? s.dispontime : s.dispofftime, f, 0x3da, masque);
    }

    // PB-226 — l'Input Status 1 de l'ET4000 (data book, p. 103) : le bit 7, le complément du retour vertical ; les bits
    // 4-5, sous 12h bits 4-5 à 00, P2 et P0, le point sous le faisceau. PCem rend le bit 7 à 0 et bascule les bits
    // 4-5 à chaque lecture pendant l'affichage (les efface hors de l'affichage). Les cas : l'affichage, le bit 7 (80h
    // contre 00h) ; le retour vertical, 00h des deux côtés (témoin) ; au quart de l'affichage, la palette 4 à 05h, 30h ;
    // aux trois quarts, 00h ; hors de l'affichage, 11h = 05h, le caractère 82, la bordure, 30h ; le caractère 90, dans le
    // blanc, 00h. Le blanc vertical commence à vblankstart, 407 (CR15 = 96h et CR07 bit 3, plus un, p. 124) et finit
    // sur CR16 = B9h (p. 125) : TS 7 bit 7 à 0 (le banc sans BIOS, l'EGA), cinq bits, à 409 : les lignes 406 et 409, la
    // bordure, 407 et 408, 00h ; TS 7 = 80h (la VGA), huit bits, à 441 : la ligne 440, 00h, la ligne 441, la bordure.
    // Les 00h de ces cas sont des témoins : PCem efface de même hors de l'affichage. Le graphique de 8 bits, la mémoire
    // à 05h, 30h, à 00h, 00h ; l'index de l'ATC bit 5 à 0, dans l'affichage, la bordure (p. 153 ; le rendu de PCem est
    // alors le blanc, svga_render_blank), 30h. Témoin : 12h = 1Fh, le choix que le data book contredit (p. 157), la
    // bascule de PCem. Les autres cas, l'index de l'ATC bit 5 à 1.
    private static int Pb226(bool hw)
    {
        var bad = UnSvga("et4000ax : l'affichage, 3DAh deux fois, le bit 7", "et4000ax", s =>
        {
            Et4Texte(s);
            return Et43da(s, 0.25, 0x80);
        }, "00 00", "80 80", hw);
        bad += UnSvga("et4000ax : le retour vertical, 3DAh deux fois, le bit 7 (témoin)", "et4000ax", s =>
        {
            Et4Texte(s);
            s.cgastat = 9;
            return Et43da(s, 0.25, 0x80);
        }, "", "00 00", hw, temoin: true);
        foreach (var (f, att) in new[] { (0.25, "30 30"), (0.75, "00 00") })
            bad += UnSvga($"et4000ax : 12h = 0Fh, la palette 4 = 05h, 3DAh deux fois, à {f:0.##} de l'affichage",
                          "et4000ax", s =>
            {
                Et4Texte(s, (0x04, 0x05), (0x12, 0x0f));
                return Et43da(s, f, 0x30);
            }, "bascule", att, hw);
        foreach (var (c, vc, att) in new[] { (82, 10, "30 30"), (90, 10, "00 00"), (82, 406, "30 30"),
                                             (82, 407, "00 00"), (82, 408, "00 00"), (82, 409, "30 30") })
            bad += UnSvga($"et4000ax : 11h = 05h, hors de l'affichage, le caractère {c}, la ligne {vc}, 3DAh" +
                          " deux fois" +
                          (att == "00 00" ? " (témoin)" : ""), "et4000ax", s =>
            {
                Et4Texte(s, (0x11, 0x05), (0x12, 0x0f));
                s.vc = vc;
                s.cgastat |= 1;
                return Et43da(s, (c - 80 + 0.5) / (s.htotal - s.hdisp_time), 0x30, affichage: false);
            }, "00 00", att, hw, temoin: att == "00 00");
        bad += UnSvga("et4000ax : l'index de l'ATC bit 5 à 0, 11h = 05h, au quart de l'affichage, 3DAh deux fois",
                      "et4000ax", s =>
        {
            Et4Texte(s, (0x04, 0x00), (0x11, 0x05), (0x12, 0x0f));
            io.inb(0x3da);
            io.outb(0x3c0, 0x00);
            return Et43da(s, 0.25, 0x30);
        }, "bascule", "30 30", hw);
        foreach (var (vc, att) in new[] { (440, "00 00"), (441, "30 30") })
            bad += UnSvga("et4000ax : TS 7 = 80h (la VGA, sous le KEY), 11h = 05h, hors de l'affichage, la ligne" +
                          $" {vc}, 3DAh deux fois" + (att == "00 00" ? " (témoin)" : ""), "et4000ax", s =>
            {
                Et4Texte(s, (0x11, 0x05), (0x12, 0x0f));
                io.outb(0x3bf, 0x03);
                io.outb(0x3d8, 0xa0);
                Seq(0x07, 0x80);
                s.vc = vc;
                s.cgastat |= 1;
                return Et43da(s, 2.5 / (s.htotal - s.hdisp_time), 0x30, affichage: false);
            }, "00 00", att, hw, temoin: att == "00 00");
        foreach (var (octet, att) in new[] { (0x05, "30 30"), (0x00, "00 00") })
            bad += UnSvga($"et4000ax : le graphique de 8 bits (GR5 = 40h, 10h = 41h), la mémoire à {octet:X2}h, 3DAh" +
                          " deux fois", "et4000ax", s =>
            {
                Et4Texte(s, (0x10, 0x41), (0x12, 0x0f));
                Gr(0x06, 0x05);
                Gr(0x05, 0x40);
                Seq(0x04, 0x0e);
                Crtc(0x14, 0x40);
                vid_svga.svga_recalctimings(s);
                (s.dispon, s.displine, s.vc, s.sc, s.ma, s.cgastat) = (1, 10, 10, 0, 0, 0);
                Array.Fill(s.vram, (byte)octet, 0, 0x40000);
                return s.bpp + " " + Et43da(s, 0.25, 0x30);
            }, "8 bascule", "8 " + att, hw);
        bad += UnSvga("et4000ax : 12h = 1Fh, la palette 4 = 3Fh, 3DAh deux fois (témoin)", "et4000ax", s =>
        {
            Et4Texte(s, (0x04, 0x3f), (0x12, 0x1f));
            return Et43da(s, 0.25, 0x30);
        }, "", "bascule", hw, temoin: true);
        return bad;
    }
}
