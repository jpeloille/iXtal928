// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — outillage de vérification.
//
// Les cas de la CGA (G13.9c, PLAN-G13.md) : PB-04 (la cellule du curseur exemptée du clignotement), PB-222 (le curseur
// qui force ses points), PB-223 (les caractères sur 32 trames) et PB-230 (R16 et R17 en lecture seule). Le banc : une
// IBM XT montée avec la CGA, sans BIOS lancé ; les registres par les ports ; une ligne de texte jouée par un appel de
// cga_poll sur un état posé à la main (le tampon de caractères de la ligne, en 80 colonnes, rempli comme cga_poll le
// remplit), les points lus dans buffer32 et rendus à leur indice de palette.

using iXtal26.PluginApi;
using iXtal26.Video;

namespace iXtal26.Diff;

internal static partial class MaterielCas
{
    private static cga_t Cga()
    {
        for (var c = 0; c < device.devices.Length; c++)
            if (device.devices[c] == vid_cga.cga_device && device.device_priv[c] is cga_t t)
                return t;
        throw new InvalidOperationException("pas de CGA après initpc");
    }

    /// <summary>Une ligne de texte de la CGA, un caractère, à la ligne <paramref name="sc"/> de la cellule : 3D8h à
    /// <paramref name="mode"/>, le compteur de trames à <paramref name="blink"/>, la cellule sous le curseur ou non (le
    /// curseur allumé, sur ses lignes) ; les huit points (seize en 40 colonnes, un sur deux), en indices de palette.</summary>
    private static string LigneCga(byte mode, byte chr, byte attr, int sc, int blink, bool curseur, bool? phase = null,
                                   bool con = true, int r10 = 0x06)
    {
        var g = Cga();
        io.outb(0x3d4, 1);
        io.outb(0x3d5, 1);
        io.outb(0x3d4, 14);
        io.outb(0x3d5, 0);
        io.outb(0x3d4, 15);
        io.outb(0x3d5, (byte)(curseur ? 0 : 1));
        io.outb(0x3d4, 10);
        io.outb(0x3d5, (byte)r10);
        io.outb(0x3d8, mode);
        g.charbuffer[0] = chr;
        g.charbuffer[1] = attr;
        g.vram[0] = chr;
        g.vram[1] = attr;
        (g.linepos, g.cgadispon, g.sc, g.ma, g.displine, g.firstline) = (0, 1, sc, 0, 10, 0);
        (g.con, g.cursoron, g.cgablink) = (curseur && con ? 1 : 0, (phase ?? curseur) ? 1 : 0, blink);
        vid_cga.cga_poll(g);
        var pas = (mode & 1) != 0 ? 1 : 2;
        var p = new string[8];
        for (var c = 0; c < 8; c++)
            p[c] = Array.IndexOf(Video.video.cgapal, Video.video.Buffer32[10 * Video.video.Stride + 8 + c * pas]).ToString("X");
        return $"points={string.Concat(p)}";
    }

    /// <summary>Les points d'une rangée de la police de la CGA, l'avant-plan et le fond en indices de palette.</summary>
    private static string PointsCga(int chr, int rangee, int fg, int bg)
    {
        var b = Video.video.fontdat[(chr << 3) | rangee];
        var p = new char[8];
        for (var c = 0; c < 8; c++)
            p[c] = ((b & (1 << (c ^ 7))) != 0 ? fg : bg).ToString("X")[0];
        return $"points={new string(p)}";
    }

    private static int UnCga(string nom, Func<string> corps, string pcem, string mat, bool hw, bool temoin = false) =>
        UnMono(nom, "cga", corps, pcem, mat, hw, temoin);

    // PB-04 — sur ses lignes, la cellule du curseur n'est jamais éteinte par le clignotement (feuille 1 : le NAND de
    // -CURSOR DLY dans +ALPHA DOTS), quelle que soit la phase du curseur. PCem lit un champ jamais écrit : pas
    // d'exemption. Le cas de l'entrée : 3D8h = 29h, 41h d'attribut 87h sous le curseur, la ligne 6, la phase éteinte des
    // caractères (le compteur à 8, sous PB-223 à 16) : PCem un pavé uni (le fond 0 inversé, Fh) ; la carte le glyphe,
    // inversé par le curseur de PCem (8h sur Fh). En 40 colonnes (3D8h = 28h), de même. Témoin : hors du curseur, la
    // phase allumée.
    private static int Pb04(bool hw)
    {
        int blink = materiel.pb_223 ? 16 : 8;
        string Glyphe() => PointsCga(0x41, 6, 8, 15);
        // Sous PB-222, le curseur est un pavé de l'avant-plan : 7h exempté, 0h éteint.
        var plein = materiel.pb_222;
        var bad = 0;
        foreach (var (mode, nom) in new[] { ((byte)0x29, "80"), ((byte)0x28, "40") })
            bad += UnCga($"CGA {nom} colonnes : 41h d'attribut 87h sous le curseur, ligne 6, la phase éteinte", () =>
            {
                var v = LigneCga(mode, 0x41, 0x87, 6, blink, true);
                return v == Glyphe() ? "le glyphe" : v == "points=FFFFFFFF" ? "un pavé uni Fh" : v == "points=77777777"
                    ? "un pavé 7h" : v == "points=00000000" ? "un pavé 0h" : v;
            }, plein ? "un pavé 0h" : "un pavé uni Fh", plein ? "un pavé 7h" : "le glyphe", hw);
        // La phase éteinte du curseur : la cellule reste exemptée (le glyphe, sans inversion), PCem l'éteint.
        bad += UnCga("CGA 80 colonnes : 41h d'attribut 87h sous le curseur éteint (cursoron = 0), la phase éteinte", () =>
        {
            var v = LigneCga(0x29, 0x41, 0x87, 6, blink, true, phase: false);
            return v == PointsCga(0x41, 6, 7, 0) ? "le glyphe" : v == "points=00000000" ? "éteint" : v;
        }, "éteint", "le glyphe", hw);
        // Témoins : la cellule hors des lignes du curseur (con = 0), et le curseur masqué par R10 (bits 5-6 à 01) :
        // éteints dans les deux modes.
        bad += UnCga("CGA 80 colonnes : 41h d'attribut 87h, ma = ca hors des lignes du curseur, la phase éteinte (témoin)", () =>
        {
            var v = LigneCga(0x29, 0x41, 0x87, 6, blink, true, phase: false, con: false);
            return v == "points=00000000" ? "éteint" : v;
        }, "", "éteint", hw, temoin: true);
        bad += UnCga("CGA 80 colonnes : 41h d'attribut 87h sous le curseur masqué (R10 = 26h), la phase éteinte (témoin)", () =>
        {
            var v = LigneCga(0x29, 0x41, 0x87, 6, blink, true, phase: false, r10: 0x26);
            return v == "points=00000000" ? "éteint" : v;
        }, "", "éteint", hw, temoin: true);
        bad += UnCga("CGA 80 colonnes : 41h d'attribut 87h hors du curseur, ligne 6, la phase allumée (témoin)", () =>
        {
            var v = LigneCga(0x29, 0x41, 0x87, 6, 0, false);
            return v == PointsCga(0x41, 6, 7, 0) ? "le glyphe" : v;
        }, "", "le glyphe", hw, temoin: true);
        return bad;
    }

    // PB-222 — le curseur force les points de la cellule à 1 : un pavé de la couleur d'avant-plan (feuille 1). PCem
    // inverse le glyphe. Le cas de l'entrée : 20h d'attribut 07h sous le curseur, la ligne 6 : PCem Fh (le fond inversé),
    // la carte 7h ; en 80 et en 40 colonnes. Témoin : 41h hors du curseur.
    private static int Pb222Cga(bool hw)
    {
        var bad = 0;
        foreach (var (mode, nom) in new[] { ((byte)0x29, "80"), ((byte)0x28, "40") })
            bad += UnCga($"CGA {nom} colonnes : 20h d'attribut 07h sous le curseur, ligne 6", () => LigneCga(mode, 0x20, 0x07, 6, 0, true),
                         "points=FFFFFFFF", "points=77777777", hw);
        bad += UnCga("CGA 80 colonnes : 41h d'attribut 07h hors du curseur, ligne 6 (témoin)", () =>
        {
            var v = LigneCga(0x29, 0x41, 0x07, 6, 0, false);
            return v == PointsCga(0x41, 6, 7, 0) ? "le glyphe" : v;
        }, "", "le glyphe", hw, temoin: true);
        return bad;
    }

    // PB-223 — les caractères de la CGA clignotent sur 32 trames (feuille 3, U12 : -BLINK, la sortie QA du second
    // compteur), le bit 4 du compteur de trames ; PCem prend le bit 3, celui du curseur. 41h d'attribut 87h hors du
    // curseur, 3D8h = 29h : le compteur à 8, PCem l'éteint (fond 0), la carte le montre ; à 16, l'inverse.
    private static int Pb223Cga(bool hw)
    {
        string Vu(byte mode, int blink)
        {
            var v = LigneCga(mode, 0x41, 0x87, 6, blink, false);
            return v == PointsCga(0x41, 6, 7, 0) ? "montré" : v == "points=00000000" ? "éteint" : v;
        }
        var bad = 0;
        foreach (var (mode, nom) in new[] { ((byte)0x29, "80"), ((byte)0x28, "40") })
        {
            bad += UnCga($"CGA {nom} colonnes : 41h d'attribut 87h, le compteur de trames à 8", () => Vu(mode, 8), "éteint", "montré",
                         hw);
            bad += UnCga($"CGA {nom} colonnes : 41h d'attribut 87h, le compteur de trames à 16", () => Vu(mode, 16), "montré",
                         "éteint", hw);
        }
        return bad;
    }

    // PB-230 (a) — R16 et R17, le crayon optique, sont en lecture seule sur le MC6845 (data sheet ; la CGA porte le 6845
    // de Motorola, IBM p. 5). Le cas de l'entrée : OUT 3D4h,10h, OUT 3D5h,5Ah : PCem garde 5Ah, la carte rien (le verrou du
    // crayon, ici à 0). Témoin : R15, qui s'écrit.
    private static int Pb230(bool hw)
    {
        string Ecrit(int reg, int val)
        {
            io.outb(0x3d4, (byte)reg);
            io.outb(0x3d5, (byte)val);
            return $"R{reg}={Cga().crtc[reg]:X2}";
        }
        var bad = UnCga("CGA : R16 = 5Ah", () => Ecrit(16, 0x5a), "R16=5A", "R16=00", hw);
        bad += UnCga("CGA : R17 = A5h", () => Ecrit(17, 0xa5), "R17=A5", "R17=00", hw);
        bad += UnCga("CGA : R15 = 5Ah (témoin)", () => Ecrit(15, 0x5a), "", "R15=5A", hw, temoin: true);
        return bad;
    }
}
