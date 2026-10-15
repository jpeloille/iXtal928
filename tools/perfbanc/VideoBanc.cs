// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — outillage de mesure.
//
// CE QUE COÛTE LA VIDÉO DU MODE MATÉRIEL (G13.9g, M1 ; PLAN-G13.md, § La vérification).
//
// En mode PCem, le JIT plie les gardes de la vidéo : le code est celui de M0 (tools/listings-jit.sh, M2). Ce banc mesure
// l'autre face, le coût d'une trame quand les corrections sont demandées, dans un processus par mode (BenchmarkDotNet en
// lance un par banc, et le mode se fige une fois par processus) :
// - l'EGA en texte de 80 colonnes (le mode 3 de 350 lignes), en mode PCem et sous PB-99 : le texte redessiné à chaque
//   ligne (PB-99 (5)) au lieu de fullchange seulement, et 3DAh tiré du faisceau (PB-99 (4)) ;
// - l'ET4000 en texte de 80 colonnes, en mode PCem et sous PB-226 : 3DAh tiré du faisceau (la ligne rendue par le rendu
//   du socle, une fois par phase).
// Une trame : les événements de la minuterie de la carte joués à la main (deux par ligne), et, à chaque affichage d'une
// ligne, `Lectures` lectures de 3DAh au milieu de la phase — une boucle qui guette le retour vertical (0, le balayage
// seul ; 16, une boucle d'attente). La machine est montée sans BIOS (pc.initpc), ses registres posés par les ports.
// Avant la mesure, chaque processus imprime le nombre de lignes de sa trame et la somme de ses lectures.

using BenchmarkDotNet.Attributes;
using iXtal26.Video;

namespace iXtal26.PerfBanc;

public class VideoEgaPcem
{
    [Params(0, 16)] public int Lectures;

    [GlobalSetup]
    public void Preparer() => VideoBanc.Preparer("aucun", "ega", Lectures);

    [Benchmark(Baseline = true)]
    public int Trame() => VideoBanc.Trame(Lectures);
}

public class VideoEgaPb99
{
    [Params(0, 16)] public int Lectures;

    [GlobalSetup]
    public void Preparer() => VideoBanc.Preparer("PB-99", "ega", Lectures);

    [Benchmark]
    public int Trame() => VideoBanc.Trame(Lectures);
}

public class VideoEt4Pcem
{
    [Params(0, 16)] public int Lectures;

    [GlobalSetup]
    public void Preparer() => VideoBanc.Preparer("aucun", "et4000ax", Lectures);

    [Benchmark(Baseline = true)]
    public int Trame() => VideoBanc.Trame(Lectures);
}

public class VideoEt4Pb226
{
    [Params(0, 16)] public int Lectures;

    [GlobalSetup]
    public void Preparer() => VideoBanc.Preparer("PB-226", "et4000ax", Lectures);

    [Benchmark]
    public int Trame() => VideoBanc.Trame(Lectures);
}

internal static class VideoBanc
{
    private static pc_timer_t? _minuterie;
    private static object? _carte;
    private static Func<int>? _linepos;
    private static Func<ulong>? _dispontime;
    private static int _lignes;

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

    /// <summary>La racine du dépôt, au-dessus du banc (roms/ et iXtal26.sln) : BenchmarkDotNet lance ses processus
    /// depuis un répertoire de build sous tools/perfbanc.</summary>
    private static string Depot()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "iXtal26.sln")) && Directory.Exists(Path.Combine(d.FullName, "roms")))
                return d.FullName;
        throw new InvalidOperationException("le dépôt (roms/, iXtal26.sln) est introuvable au-dessus du banc");
    }

    /// <summary>Pose le mode avant le gel, monte la machine, la carte en texte de 80 colonnes, une rangée de caractères à
    /// fond coloré sur la moitié gauche ; puis joue une trame et l'imprime.</summary>
    internal static void Preparer(string mode, string carte, int lectures)
    {
        if (!ModeMateriel.Demander(mode, "VideoBanc"))
            throw new InvalidOperationException($"mode « {mode} » refusé");
        var depot = Depot();
        var cfg = Path.Combine(Path.GetTempPath(), $"videobanc-{Environment.ProcessId}.cfg");
        File.WriteAllText(cfg, carte == "ega"
            ? "model = ibmxt\nmem_size = 640\ngfxcard = ega\nsndcard = none\n"
            : "model = ami486\ncpu = 10\nmem_size = 4096\ngfxcard = et4000ax\nsndcard = none\n");
        if (!pc.loadconfig(cfg) || !pc.initpc(Path.Combine(depot, "roms")))
            throw new InvalidOperationException("la machine ne se monte pas");
        File.Delete(cfg);
        io.outb(0x3c2, carte == "ega" ? (byte)0x01 : (byte)0x63);
        Port(0x3c4, 0x3c5, 0x01, 0x01);
        Port(0x3ce, 0x3cf, 0x06, 0x0e);
        Attr(0x04, 0x05);
        Attr(0x12, 0x0f);
        if (carte == "ega")
        {
            foreach (var (r, v) in new[] { (0x00, 0x5b), (0x01, 0x4f), (0x02, 0x53), (0x03, 0x37), (0x04, 0x51),
                                           (0x05, 0x5b), (0x06, 0x6c), (0x07, 0x1f), (0x09, 0x0d), (0x10, 0x5e),
                                           (0x11, 0x2b), (0x12, 0x5d), (0x13, 0x28), (0x15, 0x5e), (0x16, 0x0a),
                                           (0x17, 0xa3) })
                Port(0x3d4, 0x3d5, r, v);
            var e = vid_ega.Probe ?? throw new InvalidOperationException("pas d'EGA");
            for (var x = 0; x < 80; x++)
                (e.vram[x * 8], e.vram[x * 8 + 1]) = (0x00, (byte)(x < 40 ? 0x40 : 0x00));
            (_minuterie, _carte, _linepos, _dispontime) = (e.timer, e, () => e.linepos, () => e.dispontime);
            _lignes = 365;
        }
        else
        {
            foreach (var (r, v) in new[] { (0x00, 0x5f), (0x01, 0x4f), (0x02, 0x55), (0x03, 0x81), (0x04, 0x55),
                                           (0x05, 0x01), (0x06, 0xbf), (0x07, 0x1f), (0x09, 0x4f), (0x10, 0x9c),
                                           (0x11, 0x0e), (0x12, 0x8f), (0x13, 0x28), (0x15, 0x96), (0x16, 0xb9),
                                           (0x17, 0xe3) })
                Port(0x3d4, 0x3d5, r, v);
            io.inb(0x3da);
            io.outb(0x3c0, 0x20);
            var s = vid_svga.svga_get_pri() ?? throw new InvalidOperationException("pas de socle SVGA");
            for (var x = 0; x < 80; x++)
                (s.vram[x * 4], s.vram[x * 4 + 1]) = (0x00, (byte)(x < 40 ? 0x40 : 0x00));
            vid_svga.svga_recalctimings(s);
            (_minuterie, _carte, _linepos, _dispontime) = (s.timer, s, () => s.linepos, () => s.dispontime);
            _lignes = 449;
        }
        Console.WriteLine($"// VideoBanc ({ModeMateriel.Description}, {carte}, {lectures} lectures) : une trame de " +
                          $"{_lignes} lignes, la somme des lectures {Trame(lectures)}");
    }

    /// <summary>Une trame : deux événements de la minuterie par ligne ; à chaque affichage, les lectures de 3DAh au
    /// milieu de la phase, l'échéance de la minuterie rendue ensuite.</summary>
    internal static int Trame(int lectures)
    {
        var t = _minuterie!;
        var somme = 0;
        for (var i = 0; i < 2 * _lignes; i++)
        {
            t.callback!(_carte);
            if (lectures == 0 || _linepos!() != 0)
                continue;
            var (ti, tf) = (t.ts_integer, t.ts_frac);
            timer.timer_set_delay_u64(t, _dispontime!() / 2);
            for (var k = 0; k < lectures; k++)
                somme += io.inb(0x3da);
            (t.ts_integer, t.ts_frac) = (ti, tf);
        }
        return somme;
    }
}
