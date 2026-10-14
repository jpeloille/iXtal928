// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — outillage de vérification.
//
// Les cas de la ligne bornée (G13.9c2, PLAN-G13.md) : PB-89, la ligne balayée qui déborde sur les lignes suivantes de
// buffer32 quand R1 (CR01 sur l'EGA) passe la largeur d'une ligne, sur la CGA, la MDA, l'Hercules, l'EGA, la M24 et le
// PC1512. Le banc : la machine montée avec la carte, sans BIOS lancé ; les registres par les ports ; une ligne jouée
// par la minuterie de la carte (le balayage de PCem, ou celui du mode matériel que la garde de l'init y a posé), sur un
// état posé à la main, les lignes 10 à 12 de buffer32 remplies d'un motif avant.

using iXtal26.Video;

namespace iXtal26.Diff;

internal static partial class MaterielCas
{
    private const uint MotifLigne = 0x00abcdef;

    /// <summary>La ligne 10 balayée par la minuterie <paramref name="minuterie"/> de la carte : la ligne 10 dessinée ou
    /// non, les lignes 11 et 12 intactes ou écrites.</summary>
    private static string Balayee(pc_timer_t minuterie, object carte)
    {
        var b = Video.video.Buffer32;
        var s = Video.video.Stride;
        Array.Fill(b, MotifLigne, 10 * s, 3 * s);
        minuterie.callback!(carte);
        string Etat(int l) => Enumerable.Range(l * s, s).All(i => b[i] == MotifLigne) ? "intacte" : "écrite";
        return $"10 {(Etat(10) == "écrite" ? "dessinée" : "laissée")}, 11 {Etat(11)}, 12 {Etat(12)}";
    }

    private static void Crtc6845(int port, int reg, int val)
    {
        io.outb((ushort)port, (byte)reg);
        io.outb((ushort)(port + 1), (byte)val);
    }

    // PB-89 — le MC6845 veut R1 inférieur à R0 ; au-delà, ce qui dépasse la ligne balayée tombe dans le retour de
    // ligne, rien de la ligne n ne s'affiche en n + 1 (déduit). Le cas de l'entrée, sur chaque carte : R1 = FFh (CR01
    // sur l'EGA), une ligne balayée : PCem écrit dans la ligne suivante de buffer32 (la CGA en 40 colonnes jusqu'à
    // 4 095, la MDA et l'Hercules en texte jusqu'à 2 294, l'EGA en 2 bpp jusqu'à 4 143 et sous l'écran éteint jusqu'à
    // 4 639 pour CR01 = FFh, la M24 et le PC1512 jusqu'à 4 095), la carte n'y touche pas ; aux bornes, la MDA à
    // R1 = E4h, l'EGA à CR01 = 70h. Témoins : R1 = 28h (CR01 = 27h), la ligne tient dans la sienne ; CR01 = 6Fh.
    private static int Pb89(bool hw)
    {
        const string Bornee = "10 dessinée, 11 intacte, 12 intacte";
        var xt = (string carte) => $"model = ibmxt\nmem_size = 640\ngfxcard = {carte}\nsndcard = none\n";
        var bad = 0;
        foreach (var (r1, temoin) in new[] { (0xff, false), (0x28, true) })
        {
            var t = temoin ? " (témoin)" : "";
            bad += UnCfg($"CGA : 3D8h = 08h (40 colonnes), R1 = {r1:X2}h, une ligne{t}", xt("cga"), () =>
            {
                var g = Cga();
                Crtc6845(0x3d4, 1, r1);
                io.outb(0x3d8, 0x08);
                (g.linepos, g.cgadispon, g.sc, g.ma, g.displine, g.firstline) = (0, 1, 0, 0, 10, 0);
                return Balayee(g.timer, g);
            }, "10 dessinée, 11 écrite, 12 intacte", Bornee, hw, temoin);
            bad += UnCfg($"MDA : 3B8h = 08h, R1 = {r1:X2}h, une ligne{t}", xt("mda"), () =>
            {
                var g = Mda();
                Crtc6845(0x3b4, 1, r1);
                io.outb(0x3b8, 0x08);
                (g.linepos, g.dispon, g.sc, g.ma, g.displine, g.firstline) = (0, 1, 0, 0, 10, 0);
                return Balayee(g.timer, g);
            }, "10 dessinée, 11 écrite, 12 intacte", Bornee, hw, temoin);
            bad += UnCfg($"Hercules : 3B8h = 08h (le texte), R1 = {r1:X2}h, une ligne{t}", xt("hercules"), () =>
            {
                var g = Herc();
                Crtc6845(0x3b4, 1, r1);
                io.outb(0x3b8, 0x08);
                (g.linepos, g.dispon, g.sc, g.ma, g.displine, g.firstline) = (0, 1, 0, 0, 10, 0);
                return Balayee(g.timer, g);
            }, "10 dessinée, 11 écrite, 12 intacte", Bornee, hw, temoin);
            var cr01 = temoin ? 0x27 : 0xff;
            bad += UnCfg($"EGA : GR6 = 01h, GR5 = 20h (2 bpp), CR01 = {cr01:X2}h, une ligne{t}", xt("ega"), () =>
            {
                var g = Ega();
                io.outb(0x3c2, 0x01);
                Crtc6845(0x3d4, 1, cr01);
                Crtc6845(0x3ce, 6, 0x01);
                Crtc6845(0x3ce, 5, 0x20);
                (g.linepos, g.dispon, g.scrblank, g.displine, g.firstline) = (0, 1, 0, 10, 0);
                return Balayee(g.timer, g);
            }, "10 dessinée, 11 écrite, 12 écrite", Bornee, hw, temoin);
            bad += UnCfg($"EGA : l'écran éteint, SR1 = 08h (18 points), CR01 = {cr01:X2}h, une ligne{t}", xt("ega"),
                         () =>
            {
                var g = Ega();
                io.outb(0x3c2, 0x01);
                Crtc6845(0x3d4, 1, cr01);
                Crtc6845(0x3c4, 1, 0x08);
                (g.linepos, g.dispon, g.scrblank, g.displine, g.firstline) = (0, 1, 0x20, 10, 0);
                return Balayee(g.timer, g);
            }, "10 dessinée, 11 écrite, 12 écrite", Bornee, hw, temoin);
            bad += UnCfg($"M24 : 3D8h = 08h (40 colonnes), R1 = {r1:X2}h, une ligne{t}",
                         "model = olivetti_m24\nmem_size = 640\nsndcard = none\n", () =>
            {
                var g = vid_olivetti_m24.m24_pri ?? throw new InvalidOperationException("pas de vidéo M24");
                Crtc6845(0x3d4, 1, r1);
                io.outb(0x3d8, 0x08);
                (g.linepos, g.dispon, g.sc, g.ma, g.displine, g.firstline) = (0, 1, 0, 0, 10, 0);
                return Balayee(g.timer, g);
            }, "10 dessinée, 11 écrite, 12 intacte", Bornee, hw, temoin);
            bad += UnCfg($"PC1512 : 3D8h = 08h (40 colonnes), R1 = {r1:X2}h, une ligne{t}",
                         "model = pc1512\nmem_size = 640\nsndcard = none\n", () =>
            {
                var g = vid_pc1512.pc1512_pri ?? throw new InvalidOperationException("pas de vidéo PC1512");
                Crtc6845(0x3d4, 1, r1);
                io.outb(0x3d8, 0x08);
                (g.linepos, g.dispon, g.sc, g.ma, g.displine, g.firstline) = (0, 1, 0, 0, 10, 0);
                return Balayee(g.timer, g);
            }, "10 dessinée, 11 écrite, 12 intacte", Bornee, hw, temoin);
        }
        // Les bornes : le plus petit R1 qui sort de la ligne, à 9 points par caractère (la MDA, R1 = E4h : 2 051) et à
        // 18 (l'écran éteint de l'EGA, CR01 = 70h, hdisp = CR01 + 1 : 2 065) ; témoin, CR01 = 6Fh (2 047), au-dessus du
        // seuil du mode matériel (112 × 18 + 48 > 2 048), que la ligne bornée ne change pas.
        bad += UnCfg("MDA : 3B8h = 08h, R1 = E4h, une ligne", xt("mda"), () =>
        {
            var g = Mda();
            Crtc6845(0x3b4, 1, 0xe4);
            io.outb(0x3b8, 0x08);
            (g.linepos, g.dispon, g.sc, g.ma, g.displine, g.firstline) = (0, 1, 0, 0, 10, 0);
            return Balayee(g.timer, g);
        }, "10 dessinée, 11 écrite, 12 intacte", Bornee, hw);
        foreach (var (cr01, temoin) in new[] { (0x70, false), (0x6f, true) })
            bad += UnCfg($"EGA : l'écran éteint, SR1 = 08h (18 points), CR01 = {cr01:X2}h, une ligne" +
                         (temoin ? " (témoin)" : ""), xt("ega"), () =>
            {
                var g = Ega();
                io.outb(0x3c2, 0x01);
                Crtc6845(0x3d4, 1, cr01);
                Crtc6845(0x3c4, 1, 0x08);
                (g.linepos, g.dispon, g.scrblank, g.displine, g.firstline) = (0, 1, 0x20, 10, 0);
                return Balayee(g.timer, g);
            }, "10 dessinée, 11 écrite, 12 intacte", Bornee, hw, temoin);
        return bad;
    }
}
