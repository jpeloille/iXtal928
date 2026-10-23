// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — outillage de mesure.
//
// LE RENDU VIDÉO, POUR LA RÉFÉRENCE DE PERFORMANCE (G14.0a ; PLAN.md § G15 : « rendu vidéo »).
//
// VideoBanc mesure l'EGA et l'ET4000 en texte. Ce banc ajoute ce qu'il n'a pas, sur le modèle de sa trame (les
// événements de la minuterie de la carte joués à la main, deux par ligne ; la machine montée sans BIOS, ses registres
// posés par les ports) :
// - RenduCga : la CGA de l'XT en texte de 80 colonnes (le mode 3, 262 lignes), cga_poll, le premier poste du profil
//   du 5150 (M5.1, 36,7 % du temps). Elle rend chaque ligne affichée à chaque trame ;
// - Rendu8bpp : un mode graphique de 640 × 480 en 256 couleurs, svga_render_8bpp_highres, sur la GD5429 de l'ami486
//   (SR7 bit 0, le mode étendu) et sur la Trident 8900D de l'ami286 (SR0D de l'ancien mode, bit 4 : les profils
//   ixtal26-486.cfg et ixtal26-286.cfg). Le socle ne redessine une ligne que si sa mémoire a changé : chaque trame
//   marque donc toute la mémoire vidéo changée, comme un programme qui redessine l'écran à chaque trame.
// Les deux cartes n'allument l'affichage qu'au retour de trame : depuis la mise sous tension, la première trame jouée
// ne dessine rien. Avant la mesure, chaque processus joue donc deux trames, puis imprime la géométrie, le rendu choisi
// par la carte et l'empreinte de l'image de la seconde.

using BenchmarkDotNet.Attributes;
using iXtal26.Video;

namespace iXtal26.PerfBanc;

public class RenduCga
{
    [Params("aucun", "tout")] public string Mode = "";

    [GlobalSetup]
    public void Preparer() => RenduBanc.PreparerCga(Mode);

    [Benchmark]
    public int Trame() => RenduBanc.TrameCga();
}

public class Rendu8bpp
{
    [Params("cl_gd5429", "tvga8900d")] public string Carte = "";
    [Params("aucun", "tout")] public string Mode = "";

    [GlobalSetup]
    public void Preparer() => RenduBanc.PreparerSvga(Mode, Carte);

    [Benchmark]
    public int Trame() => RenduBanc.TrameSvga();
}

internal static class RenduBanc
{
    private const int Lignes8bpp = 480, Points8bpp = 640;

    private static cga_t? _cga;
    private static int _lignesCga;
    private static svga_t? _svga;

    private static void Port(int index, int donnee, int i, int v)
    {
        io.outb((ushort)index, (byte)i);
        io.outb((ushort)donnee, (byte)v);
    }

    private static void Attr(int i, int v)
    {
        io.inb(0x3da);
        io.outb(0x3c0, (byte)i);
        io.outb(0x3c0, (byte)v);
    }

    private static ulong Image(int lignes) => Reference.Fnv<uint>(video.Buffer32.AsSpan(0, lignes * video.Stride));

    /// <summary>Pose le mode avant le gel, monte l'XT et sa CGA, le texte de 80 colonnes (R0-R15 du mode 3, 3D8h =
    /// 29h), un écran plein de caractères et d'attributs variés ; puis joue deux trames et imprime la
    /// seconde.</summary>
    internal static void PreparerCga(string mode)
    {
        Reference.Demander(mode, "RenduCga");
        Reference.Monter("model = ibmxt\nmem_size = 640\ngfxcard = cga\nsndcard = none\n", "RenduCga");
        Reference.VerifierMode(mode, "RenduCga");
        var c = vid_cga.Sonde.carte ?? throw new InvalidOperationException("RenduCga : pas de CGA après initpc");
        byte[] crtc = [0x71, 0x50, 0x5a, 0x0a, 0x1f, 0x06, 0x19, 0x1c, 0x02, 0x07, 0x06, 0x07, 0x00, 0x00, 0x00, 0x00];
        for (var r = 0; r < crtc.Length; r++)
            Port(0x3d4, 0x3d5, r, crtc[r]);
        io.outb(0x3d8, 0x29);
        io.outb(0x3d9, 0x00);
        for (var y = 0; y < 25; y++)
            for (var x = 0; x < 80; x++)
                (c.vram[(y * 80 + x) * 2], c.vram[(y * 80 + x) * 2 + 1]) = ((byte)(0x20 + (x + y) % 0x5f),
                                                                            (byte)((x & 0x0f) | ((y & 7) << 4)));
        _cga = c;
        _lignesCga = (c.crtc[4] + 1) * (c.crtc[9] + 1) + c.crtc[5];
        TrameCga();
        var retour = TrameCga();
        Reference.Verifier("RenduCga", "ibmxt, cga, texte 80 colonnes", mode,
                           $"une trame de {_lignesCga} lignes, mode {c.cgamode:X2}, displine {retour}, " +
                           $"l'image {Image(300):X16}");
    }

    /// <summary>Une trame : deux événements de la minuterie par ligne.</summary>
    internal static int TrameCga()
    {
        var c = _cga!;
        var t = c.timer;
        for (var i = 0; i < 2 * _lignesCga; i++)
            t.callback!(c);
        return c.displine;
    }

    /// <summary>Pose le mode avant le gel, monte la machine de la carte, 640 × 480 en 256 couleurs (le CRTC du mode
    /// 12h, GR05 = 40h, la mémoire en chaîne de 4, le mode étendu de la carte), la palette et une image en dégradé ;
    /// puis joue deux trames et imprime la seconde.</summary>
    internal static void PreparerSvga(string mode, string carte)
    {
        Reference.Demander(mode, "Rendu8bpp");
        Reference.Monter(carte == "cl_gd5429"
            ? "model = ami486\ncpu = 10\nfpu = builtin\nmem_size = 4096\ngfxcard = cl_gd5429\nsndcard = none\n"
            : "model = ami286\ncpu = 4\nmem_size = 4096\ngfxcard = tvga8900d\nsndcard = none\n", "Rendu8bpp");
        Reference.VerifierMode(mode, "Rendu8bpp");
        var s = vid_svga.svga_get_pri() ?? throw new InvalidOperationException("Rendu8bpp : pas de socle SVGA");
        io.outb(0x3c2, 0xe3);
        Port(0x3c4, 0x3c5, 0x01, 0x01);
        Port(0x3c4, 0x3c5, 0x02, 0x0f);
        Port(0x3c4, 0x3c5, 0x04, 0x0e);
        if (carte == "cl_gd5429")
        {
            Port(0x3c4, 0x3c5, 0x06, 0x12);
            Port(0x3c4, 0x3c5, 0x07, 0x01);
        }
        else
        {
            Port(0x3c4, 0x3c5, 0x0b, 0x00);
            Port(0x3c4, 0x3c5, 0x0d, 0x10);
        }
        Port(0x3ce, 0x3cf, 0x05, 0x40);
        Port(0x3ce, 0x3cf, 0x06, 0x05);
        for (var i = 0; i < 16; i++)
            Attr(i, i);
        Attr(0x10, 0x01);
        Attr(0x12, 0x0f);
        io.inb(0x3da);
        io.outb(0x3c0, 0x20);
        Port(0x3d4, 0x3d5, 0x11, 0x0c);
        foreach (var (r, v) in new[] { (0x00, 0x5f), (0x01, 0x4f), (0x02, 0x50), (0x03, 0x82), (0x04, 0x54),
                                       (0x05, 0x80), (0x06, 0x0b), (0x07, 0x3e), (0x08, 0x00), (0x09, 0x40),
                                       (0x10, 0xea), (0x11, 0x8c), (0x12, 0xdf), (0x13, 0x50), (0x14, 0x40),
                                       (0x15, 0xe7), (0x16, 0x04), (0x17, 0xa3) })
            Port(0x3d4, 0x3d5, r, v);
        io.outb(0x3c8, 0x00);
        for (var i = 0; i < 256; i++)
        {
            io.outb(0x3c9, (byte)(i >> 2));
            io.outb(0x3c9, (byte)(i & 0x3f));
            io.outb(0x3c9, (byte)((255 - i) >> 2));
        }
        for (var k = 0; k < Points8bpp * Lignes8bpp; k++)
            s.vram[k] = (byte)((k % Points8bpp) ^ (k / Points8bpp));
        vid_svga.svga_recalctimings(s);
        var rendu = Reference.Nom(s.render);
        if (rendu != "svga_render_8bpp_highres" || s.bpp != 8)
            throw new InvalidOperationException($"Rendu8bpp ({carte}) : la carte rend par {rendu}, {s.bpp} bpp");
        _svga = s;
        TrameSvga();
        TrameSvga();
        Reference.Verifier("Rendu8bpp", carte, mode,
                           $"une trame de {s.vtotal} lignes, {s.hdisp} × {s.dispend} points, {rendu}, {s.bpp} bpp, " +
                           $"l'image {Image(s.dispend + 16):X16}");
    }

    /// <summary>Une trame : toute la mémoire vidéo marquée changée, puis deux événements de la minuterie par
    /// ligne.</summary>
    internal static int TrameSvga()
    {
        var s = _svga!;
        Array.Fill(s.changedvram, (byte)video.changeframecount);
        var t = s.timer;
        for (var i = 0; i < 2 * s.vtotal; i++)
            t.callback!(s);
        return s.displine;
    }
}
