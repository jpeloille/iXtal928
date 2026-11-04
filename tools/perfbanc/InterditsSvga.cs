// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — outillage de mesure.
//
// LES CAS DE LA VIDÉO (G15.0a ; l'en-tête d'InterditsBanc.cs dit les colonnes, l'équivalence et le mode), sur la
// GD5429 de l'ami486 en 640 × 480 et 256 couleurs, préparée par RenduBanc.PreparerSvga (le cas Rendu8bpp de la
// référence, son état vérifié) :
//   5. svga_recalctimings, ses affectations de svga.render : le groupe de méthodes que le compilateur met en cache,
//      contre une lambda qui capture la carte, créée à chaque appel ;
//   8. svga_write, ses deux journaux : gardés, contre les mêmes interpolations sans garde, écrites dans
//      TextWriter.Null. Chez PCem, la garde est `svga_output`, une constante (vid_svga.c:13, Video/vid_svga.cs:223) que
//      le compilateur plie : aucun chemin chaud d'iXtal26 n'a d'interpolation gardée par un champ (R1-interdits.md
//      § 1.5). Le vrai code est donc la garde pliée, que Retenue et AA copient ; Champ est la garde par un champ, la
//      forme que PLAN-G15 retient quand la garde n'est pas une constante ; Interdite n'a aucune garde.
// Chaque copie est un seul texte, le corps de la fonction ligne pour ligne (les commentaires ôtés, les lignes de plus
// de 120 caractères recoupées), dont seul ce qui est mesuré passe par une structure : T.Poser pour les affectations du
// rendu, T.Garde et T.Ecrire pour les journaux. Une structure liée à la compilation (un membre statique abstrait
// d'interface, sur une structure) est résolue par le JIT, qui compile un corps par structure et inline l'appel : la
// retenue est le code de PCem, que la colonne Reel compare.

using BenchmarkDotNet.Attributes;
using iXtal26.Ics;
using iXtal26.Models;
using iXtal26.Video;
using static iXtal26.Cpu.x86;
using static iXtal26.Memory.mem;
using static iXtal26.Video.video;
using static iXtal26.Video.vid_svga;
using static iXtal26.Video.vid_svga_render;
using static iXtal26.pc;
using static iXtal26.timer;
using int64_t = System.Int64;
using uint64_t = System.UInt64;

namespace iXtal26.PerfBanc;

// ---- 5. svga_recalctimings : un délégué mis en cache contre une lambda créée à chaque appel ----

/// <summary>Cas 5. svga_recalctimings, appelé comme svga_out l'appelle à chaque écriture d'un registre qui change la
/// géométrie : le vrai, la copie, et la même dont chaque affectation de svga.render (deux ici : svga_render_blank,
/// puis svga_render_8bpp_highres) crée une lambda qui capture la carte.</summary>
public class Interdit5Delegue
{
    [GlobalSetup]
    public void Preparer() => Interdit5.Preparer();

    [Benchmark(OperationsPerInvoke = Interdit5.Appels)]
    public int Reel() => Interdit5.BoucleReel();

    [Benchmark(Baseline = true, OperationsPerInvoke = Interdit5.Appels)]
    public int Retenue() => Interdit5.Boucle<RenduDirectA>();

    [Benchmark(OperationsPerInvoke = Interdit5.Appels)]
    public int AA() => Interdit5.Boucle<RenduDirectB>();

    [Benchmark(OperationsPerInvoke = Interdit5.Appels)]
    public int Interdite() => Interdit5.Boucle<RenduCapture>();
}

/// <summary>Ce que la copie de svga_recalctimings fait de chaque affectation du rendu.</summary>
internal interface IPoseRendu
{
    static abstract void Poser(svga_t svga, svga_render_fn rendu);
}

internal struct RenduDirectA : IPoseRendu
{
    public static void Poser(svga_t svga, svga_render_fn rendu) => svga.render = rendu;
}

internal struct RenduDirectB : IPoseRendu
{
    public static void Poser(svga_t svga, svga_render_fn rendu) => svga.render = rendu;
}

internal struct RenduCapture : IPoseRendu
{
    public static void Poser(svga_t svga, svga_render_fn rendu) => svga.render = s => rendu(svga);
}

internal static class Interdit5
{
    internal const int Appels = 256;

    private static svga_t _svga = null!;

    /// <summary>htotal_materiel (Video/vid_svga.Materiel.cs:22), privé : sa garde, PB-220, est pliée à faux en mode
    /// « aucun ».</summary>
    private static Action<svga_t> htotal_materiel = null!;

    internal static void Preparer()
    {
        RenduBanc.PreparerSvga(Interdits.Mode, "cl_gd5429");
        Reference.VerifierMode(Interdits.Mode, "Interdit5Delegue");
        _svga = svga_get_pri() ?? throw new InvalidOperationException("Interdit5Delegue : pas de socle SVGA");
        htotal_materiel = Interdits.Methode<Action<svga_t>>(typeof(vid_svga), "htotal_materiel");
        Func<int>[] colonnes = [BoucleReel, Boucle<RenduDirectA>, Boucle<RenduDirectB>, Boucle<RenduCapture>];
        string[] noms = ["Reel", "Retenue", "AA", "Interdite"];
        string? attendu = null;
        for (var c = 0; c < colonnes.Length; c++)
        {
            var obtenu = $"{colonnes[c]()} : {Etat(_svga)}";
            if (attendu == null)
                attendu = obtenu;
            else if (obtenu != attendu)
                Interdits.Refuser("Interdit5Delegue", noms[c], attendu, obtenu);
        }
        Interdits.Imprimer("Interdit5Delegue", $"ami486, cl_gd5429, {Appels} appels par colonne, {attendu} dans les " +
                           "quatre");
    }

    /// <summary>Ce que svga_recalctimings écrit ; le rendu par son nom, celui que la lambda capture pour la forme
    /// interdite.</summary>
    private static string Etat(svga_t s) =>
        $"rendu {Rendu(s.render)}, vtotal {s.vtotal}, dispend {s.dispend}, vsyncstart {s.vsyncstart}, split " +
        $"{s.split}, vblankstart {s.vblankstart}, hdisp {s.hdisp}/{s.hdisp_time}/{s.hdisp_old}, htotal {s.htotal}, " +
        $"interlace {s.interlace}, ca_adj {s.ca_adj}, " +
        $"rowoffset {s.rowoffset}, clock {s.clock}, lowres {s.lowres}, ma_latch {s.ma_latch:X}, rowcount " +
        $"{s.rowcount}, linedbl {s.linedbl}, char_width {s.char_width}, dispontime {s.dispontime}, dispofftime " +
        $"{s.dispofftime}, bpp {s.bpp}";

    private static string Rendu(svga_render_fn? r)
    {
        if (r == null || !r.Method.Name.StartsWith('<'))
            return Reference.Nom(r);
        foreach (var f in r.Target!.GetType().GetFields())
            if (f.GetValue(r.Target) is svga_render_fn capture)
                return capture.Method.Name;
        return "(la lambda ne capture aucun rendu)";
    }

    internal static int BoucleReel()
    {
        var s = _svga;
        for (var i = 0; i < Appels; i++)
            svga_recalctimings(s);
        return s.hdisp;
    }

    internal static int Boucle<T>() where T : struct, IPoseRendu
    {
        var s = _svga;
        for (var i = 0; i < Appels; i++)
            Recalctimings<T>(s);
        return s.hdisp;
    }

    // Video/vid_svga.cs:550-728, ligne pour ligne, chaque `svga.render = …` par T.Poser.
    internal static void Recalctimings<T>(svga_t svga) where T : struct, IPoseRendu
    {
        double crtcconst;
        double _dispontime, _dispofftime, disptime;

        svga.vtotal = svga.crtc[6];
        svga.dispend = svga.crtc[0x12];
        svga.vsyncstart = svga.crtc[0x10];
        svga.split = svga.crtc[0x18];
        svga.vblankstart = svga.crtc[0x15];

        if ((svga.crtc[7] & 1) != 0)
                svga.vtotal |= 0x100;
        if ((svga.crtc[7] & 32) != 0)
                svga.vtotal |= 0x200;
        svga.vtotal += 2;

        if ((svga.crtc[7] & 2) != 0)
                svga.dispend |= 0x100;
        if ((svga.crtc[7] & 64) != 0)
                svga.dispend |= 0x200;
        svga.dispend++;

        if ((svga.crtc[7] & 4) != 0)
                svga.vsyncstart |= 0x100;
        if ((svga.crtc[7] & 128) != 0)
                svga.vsyncstart |= 0x200;
        svga.vsyncstart++;

        if ((svga.crtc[7] & 0x10) != 0)
                svga.split |= 0x100;
        if ((svga.crtc[9] & 0x40) != 0)
                svga.split |= 0x200;
        svga.split++;

        if ((svga.crtc[7] & 0x08) != 0)
                svga.vblankstart |= 0x100;
        if ((svga.crtc[9] & 0x20) != 0)
                svga.vblankstart |= 0x200;
        svga.vblankstart++;

        svga.hdisp = svga.crtc[1];
        svga.hdisp++;

        svga.htotal = svga.crtc[0];
        svga.htotal += 6; /*+6 is required for Tyrian*/
        if (materiel.pb_220)
                htotal_materiel(svga);

        svga.rowoffset = svga.crtc[0x13];

        svga.clock = (svga.vidclock != 0) ? Intel8253.VGACONST2 : Intel8253.VGACONST1;

        svga.lowres = svga.attrregs[0x10] & 0x40;

        svga.interlace = 0;

        svga.ma_latch = (uint32_t)(((svga.crtc[0xc] << 8) | svga.crtc[0xd]) + ((svga.crtc[8] & 0x60) >> 5));
        svga.ca_adj = 0;

        svga.rowcount = svga.crtc[9] & 31;
        svga.linedbl = svga.crtc[9] & 0x80;

        svga.hdisp_time = svga.hdisp;
        T.Poser(svga, svga_render_blank);
        if (svga.scrblank == 0 && svga.attr_palette_enable != 0)
        {
                if ((svga.gdcreg[6] & 1) == 0 && (svga.attrregs[0x10] & 1) == 0) /*Text mode*/
                {
                        if ((svga.seqregs[1] & 8) != 0) /*40 column*/
                        {
                                T.Poser(svga, svga_render_text_40);
                                svga.hdisp *= ((svga.seqregs[1] & 1) != 0) ? 16 : 18;
                        }
                        else
                        {
                                T.Poser(svga, svga_render_text_80);
                                svga.hdisp *= ((svga.seqregs[1] & 1) != 0) ? 8 : 9;
                        }
                        svga.hdisp_old = svga.hdisp;
                }
                else
                {
                        svga.hdisp *= ((svga.seqregs[1] & 8) != 0) ? 16 : 8;
                        svga.hdisp_old = svga.hdisp;

                        switch (svga.gdcreg[5] & 0x60)
                        {
                        case 0x00:                        /*16 colours*/
                                if ((svga.seqregs[1] & 8) != 0) /*Low res (320)*/
                                        T.Poser(svga, svga_render_4bpp_lowres);
                                else
                                        T.Poser(svga, svga_render_4bpp_highres);
                                break;
                        case 0x20:                        /*4 colours*/
                                if ((svga.seqregs[1] & 8) != 0) /*Low res (320)*/
                                        T.Poser(svga, svga_render_2bpp_lowres);
                                else
                                        T.Poser(svga, svga_render_2bpp_highres);
                                break;
                        case 0x40:
                        case 0x60: /*256+ colours*/
                                switch (svga.bpp)
                                {
                                case 8:
                                        if (svga.lowres != 0)
                                                T.Poser(svga, svga_render_8bpp_lowres);
                                        else
                                                T.Poser(svga, svga_render_8bpp_highres);
                                        break;
                                case 15:
                                        if (svga.lowres != 0)
                                                T.Poser(svga, svga_render_15bpp_lowres);
                                        else
                                                T.Poser(svga, svga_render_15bpp_highres);
                                        break;
                                case 16:
                                        if (svga.lowres != 0)
                                                T.Poser(svga, svga_render_16bpp_lowres);
                                        else
                                                T.Poser(svga, svga_render_16bpp_highres);
                                        break;
                                case 24:
                                        if (svga.lowres != 0)
                                                T.Poser(svga, svga_render_24bpp_lowres);
                                        else
                                                T.Poser(svga, svga_render_24bpp_highres);
                                        break;
                                case 32:
                                        if (svga.lowres != 0)
                                                T.Poser(svga, svga_render_32bpp_lowres);
                                        else
                                                T.Poser(svga, svga_render_32bpp_highres);
                                        break;
                                }
                                break;
                        }
                }
        }

        svga.char_width = ((svga.seqregs[1] & 1) != 0) ? 8 : 9;
        if (svga.recalctimings_ex != null)
                svga.recalctimings_ex(svga);

        if (svga.vblankstart < svga.dispend)
                svga.dispend = svga.vblankstart;

        crtcconst = svga.clock * svga.char_width;

        disptime = svga.htotal;
        _dispontime = svga.hdisp_time;

        if ((svga.seqregs[1] & 8) != 0)
        {
                disptime *= 2;
                _dispontime *= 2;
        }
        _dispofftime = disptime - _dispontime;
        _dispontime *= crtcconst;
        _dispofftime *= crtcconst;

        svga.dispontime = unchecked((uint64_t)(int64_t)_dispontime);
        svga.dispofftime = unchecked((uint64_t)(int64_t)_dispofftime);
        if (svga.dispontime < TIMER_USEC)
                svga.dispontime = TIMER_USEC;
        if (svga.dispofftime < TIMER_USEC)
                svga.dispofftime = TIMER_USEC;

        svga_recalc_remap_func(svga);
    }
}

// ---- 8. svga_write : une interpolation gardée par un champ contre la même sans garde ----

/// <summary>Cas 8. Une écriture d'octet dans la fenêtre A0000h en 256 couleurs (la chaîne de 4), comme le mappage de
/// la carte la passe à svga_write : le vrai et sa copie (la garde pliée), la même gardée par un champ nul, et la même
/// sans garde, ses deux journaux formatés à chaque écriture.</summary>
public class Interdit8Chaine
{
    [GlobalSetup]
    public void Preparer() => Interdit8.Preparer();

    [Benchmark(OperationsPerInvoke = Interdit8.Appels)]
    public int Reel() => Interdit8.BoucleReel();

    [Benchmark(Baseline = true, OperationsPerInvoke = Interdit8.Appels)]
    public int Retenue() => Interdit8.Boucle<JournalPlieA>();

    [Benchmark(OperationsPerInvoke = Interdit8.Appels)]
    public int AA() => Interdit8.Boucle<JournalPlieB>();

    [Benchmark(OperationsPerInvoke = Interdit8.Appels)]
    public int Champ() => Interdit8.Boucle<JournalChamp>();

    [Benchmark(OperationsPerInvoke = Interdit8.Appels)]
    public int Interdite() => Interdit8.Boucle<JournalSansGarde>();
}

/// <summary>Ce que la copie de svga_write fait de ses journaux.</summary>
internal interface IJournalSvga
{
    static abstract bool Garde { get; }
    static abstract void Ecrire(string s);
}

internal struct JournalPlieA : IJournalSvga
{
    public static bool Garde => false;
    public static void Ecrire(string s) => TextWriter.Null.Write(s);
}

internal struct JournalPlieB : IJournalSvga
{
    public static bool Garde => false;
    public static void Ecrire(string s) => TextWriter.Null.Write(s);
}

internal struct JournalChamp : IJournalSvga
{
    public static bool Garde => Interdit8.svga_output != 0;
    public static void Ecrire(string s) => TextWriter.Null.Write(s);
}

internal struct JournalSansGarde : IJournalSvga
{
    public static bool Garde => true;
    public static void Ecrire(string s) => TextWriter.Null.Write(s);
}

internal static class Interdit8
{
    internal const int Appels = 1024;

    /// <summary>La garde par un champ : nul, comme la constante de PCem, mais lu à chaque écriture.</summary>
    internal static int svga_output = 0;

    private static svga_t _svga = null!;
    private static byte[] _vram = [];
    private static byte[] _changee = [];

    private static uint32_t Adresse(int i) => 0xa0000 + ((uint32_t)(i * 13) & 0xffff);

    internal static void Preparer()
    {
        RenduBanc.PreparerSvga(Interdits.Mode, "cl_gd5429");
        Reference.VerifierMode(Interdits.Mode, "Interdit8Chaine");
        _svga = svga_get_pri() ?? throw new InvalidOperationException("Interdit8Chaine : pas de socle SVGA");
        _vram = (byte[])_svga.vram.Clone();
        _changee = (byte[])_svga.changedvram.Clone();
        Func<int>[] colonnes = [BoucleReel, Boucle<JournalPlieA>, Boucle<JournalPlieB>, Boucle<JournalChamp>,
                                Boucle<JournalSansGarde>];
        string[] noms = ["Reel", "Retenue", "AA", "Champ", "Interdite"];
        string? attendu = null;
        for (var c = 0; c < colonnes.Length; c++)
        {
            _vram.CopyTo(_svga.vram, 0);
            _changee.CopyTo(_svga.changedvram, 0);
            egawrites = 0;
            cycles = 0;
            cycles_lost = 0;
            var r = colonnes[c]();
            var obtenu = $"{r}, la mémoire vidéo {Reference.Fnv<byte>(_svga.vram):X16}, les pages changées " +
                         $"{Reference.Fnv<byte>(_svga.changedvram):X16}, {egawrites} écritures, cycles {cycles}, " +
                         $"perdus {cycles_lost}, fullchange {_svga.fullchange}, verrous {_svga.la:X2}{_svga.lb:X2}" +
                         $"{_svga.lc:X2}{_svga.ld:X2}";
            if (attendu == null)
                attendu = obtenu;
            else if (obtenu != attendu)
                Interdits.Refuser("Interdit8Chaine", noms[c], attendu, obtenu);
        }
        Interdits.Imprimer("Interdit8Chaine", $"ami486, cl_gd5429, {Appels} écritures par colonne, {attendu} dans " +
                           "les cinq");
    }

    internal static int BoucleReel()
    {
        var s = _svga;
        for (var i = 0; i < Appels; i++)
            svga_write(Adresse(i), (uint8_t)i, s);
        return s.vram[0];
    }

    internal static int Boucle<T>() where T : struct, IJournalSvga
    {
        var s = _svga;
        for (var i = 0; i < Appels; i++)
            Write<T>(Adresse(i), (uint8_t)i, s);
        return s.vram[0];
    }

    // Video/vid_svga.cs:1089-1353, ligne pour ligne, `svga_output != 0` par T.Garde et pclog par T.Ecrire ; le `!`
    // du transtypage, que les avertissements pleins du banc demandent, ne change pas le code.
    internal static void Write<T>(uint32_t addr, uint8_t val, object? p) where T : struct, IJournalSvga
    {
        svga_t svga = (svga_t)p!;
        uint8_t vala, valb, valc, vald, wm = svga.writemask;
        int writemask2 = svga.writemask;

        egawrites++;

        cycles -= video_timing_write_b;
        cycles_lost += video_timing_write_b;

        if (T.Garde)
                T.Ecrire($"Writeega {addr:X6}   ");
        addr &= svga.banked_mask;
        addr += svga.write_bank;

        if ((svga.gdcreg[6] & 1) == 0)
                svga.fullchange = 2;
        if ((svga.chain4 != 0 && svga.packed_chain4 != 0) || svga.fb_only != 0)
        {
                writemask2 = 1 << (int)(addr & 3);
                addr &= ~3u;
        }
        else if (svga.chain4 != 0)
        {
                writemask2 = 1 << (int)(addr & 3);
                addr &= ~3u;
                addr = ((addr & 0xfffc) << 2) | ((addr & 0x30000) >> 14) | (addr & ~0x3ffffu);
        }
        else if (svga.chain2_write != 0)
        {
                writemask2 &= ~0xa;
                if ((addr & 1) != 0)
                        writemask2 <<= 1;
                addr &= ~1u;
                addr <<= 2;
        }
        else
        {
                addr <<= 2;
        }
        addr &= svga.decode_mask;

        if (addr >= svga.vram_max)
                return;

        addr &= svga.vram_mask;

        if (T.Garde)
                T.Ecrire($"{addr:X8} ({addr & 1023}, {addr >> 10}) {val:X2} {writemask2} " +
                        $"{svga.writemode} {svga.chain4} {svga.gdcreg[8]:X2}\n");
        svga.changedvram[addr >> 12] = (uint8_t)changeframecount;

        switch (svga.writemode)
        {
        case 1:
                if ((writemask2 & 1) != 0)
                        svga.vram[addr] = svga.la;
                if ((writemask2 & 2) != 0)
                        svga.vram[addr | 0x1] = svga.lb;
                if ((writemask2 & 4) != 0)
                        svga.vram[addr | 0x2] = svga.lc;
                if ((writemask2 & 8) != 0)
                        svga.vram[addr | 0x3] = svga.ld;
                break;
        case 0:
                if ((svga.gdcreg[3] & 7) != 0)
                        val = svga_rotate[svga.gdcreg[3] & 7, val];
                if (svga.gdcreg[8] == 0xff && (svga.gdcreg[3] & 0x18) == 0
                    && (svga.gdcreg[1] == 0 || svga.set_reset_disabled != 0))
                {
                        if ((writemask2 & 1) != 0)
                                svga.vram[addr] = val;
                        if ((writemask2 & 2) != 0)
                                svga.vram[addr | 0x1] = val;
                        if ((writemask2 & 4) != 0)
                                svga.vram[addr | 0x2] = val;
                        if ((writemask2 & 8) != 0)
                                svga.vram[addr | 0x3] = val;
                }
                else
                {
                        if ((svga.gdcreg[1] & 1) != 0)
                                vala = (uint8_t)(((svga.gdcreg[0] & 1) != 0) ? 0xff : 0);
                        else
                                vala = val;
                        if ((svga.gdcreg[1] & 2) != 0)
                                valb = (uint8_t)(((svga.gdcreg[0] & 2) != 0) ? 0xff : 0);
                        else
                                valb = val;
                        if ((svga.gdcreg[1] & 4) != 0)
                                valc = (uint8_t)(((svga.gdcreg[0] & 4) != 0) ? 0xff : 0);
                        else
                                valc = val;
                        if ((svga.gdcreg[1] & 8) != 0)
                                vald = (uint8_t)(((svga.gdcreg[0] & 8) != 0) ? 0xff : 0);
                        else
                                vald = val;

                        switch (svga.gdcreg[3] & 0x18)
                        {
                        case 0: /*Set*/
                                if ((writemask2 & 1) != 0)
                                        svga.vram[addr] =
                                                (uint8_t)((vala & svga.gdcreg[8]) | (svga.la & ~svga.gdcreg[8]));
                                if ((writemask2 & 2) != 0)
                                        svga.vram[addr | 0x1] =
                                                (uint8_t)((valb & svga.gdcreg[8]) | (svga.lb & ~svga.gdcreg[8]));
                                if ((writemask2 & 4) != 0)
                                        svga.vram[addr | 0x2] =
                                                (uint8_t)((valc & svga.gdcreg[8]) | (svga.lc & ~svga.gdcreg[8]));
                                if ((writemask2 & 8) != 0)
                                        svga.vram[addr | 0x3] =
                                                (uint8_t)((vald & svga.gdcreg[8]) | (svga.ld & ~svga.gdcreg[8]));
                                break;
                        case 8: /*AND*/
                                if ((writemask2 & 1) != 0)
                                        svga.vram[addr] = (uint8_t)((vala | ~svga.gdcreg[8]) & svga.la);
                                if ((writemask2 & 2) != 0)
                                        svga.vram[addr | 0x1] = (uint8_t)((valb | ~svga.gdcreg[8]) & svga.lb);
                                if ((writemask2 & 4) != 0)
                                        svga.vram[addr | 0x2] = (uint8_t)((valc | ~svga.gdcreg[8]) & svga.lc);
                                if ((writemask2 & 8) != 0)
                                        svga.vram[addr | 0x3] = (uint8_t)((vald | ~svga.gdcreg[8]) & svga.ld);
                                break;
                        case 0x10: /*OR*/
                                if ((writemask2 & 1) != 0)
                                        svga.vram[addr] = (uint8_t)((vala & svga.gdcreg[8]) | svga.la);
                                if ((writemask2 & 2) != 0)
                                        svga.vram[addr | 0x1] = (uint8_t)((valb & svga.gdcreg[8]) | svga.lb);
                                if ((writemask2 & 4) != 0)
                                        svga.vram[addr | 0x2] = (uint8_t)((valc & svga.gdcreg[8]) | svga.lc);
                                if ((writemask2 & 8) != 0)
                                        svga.vram[addr | 0x3] = (uint8_t)((vald & svga.gdcreg[8]) | svga.ld);
                                break;
                        case 0x18: /*XOR*/
                                if ((writemask2 & 1) != 0)
                                        svga.vram[addr] = (uint8_t)((vala & svga.gdcreg[8]) ^ svga.la);
                                if ((writemask2 & 2) != 0)
                                        svga.vram[addr | 0x1] = (uint8_t)((valb & svga.gdcreg[8]) ^ svga.lb);
                                if ((writemask2 & 4) != 0)
                                        svga.vram[addr | 0x2] = (uint8_t)((valc & svga.gdcreg[8]) ^ svga.lc);
                                if ((writemask2 & 8) != 0)
                                        svga.vram[addr | 0x3] = (uint8_t)((vald & svga.gdcreg[8]) ^ svga.ld);
                                break;
                        }
                }
                break;
        case 2:
                if ((svga.gdcreg[3] & 0x18) == 0 && (svga.gdcreg[1] == 0 || svga.set_reset_disabled != 0))
                {
                        if ((writemask2 & 1) != 0)
                                svga.vram[addr] =
                                        (uint8_t)(((((val & 1) != 0) ? 0xff : 0) & svga.gdcreg[8])
                                          | (svga.la & ~svga.gdcreg[8]));
                        if ((writemask2 & 2) != 0)
                                svga.vram[addr | 0x1] =
                                        (uint8_t)(((((val & 2) != 0) ? 0xff : 0) & svga.gdcreg[8])
                                          | (svga.lb & ~svga.gdcreg[8]));
                        if ((writemask2 & 4) != 0)
                                svga.vram[addr | 0x2] =
                                        (uint8_t)(((((val & 4) != 0) ? 0xff : 0) & svga.gdcreg[8])
                                          | (svga.lc & ~svga.gdcreg[8]));
                        if ((writemask2 & 8) != 0)
                                svga.vram[addr | 0x3] =
                                        (uint8_t)(((((val & 8) != 0) ? 0xff : 0) & svga.gdcreg[8])
                                          | (svga.ld & ~svga.gdcreg[8]));
                }
                else
                {
                        vala = (uint8_t)(((val & 1) != 0) ? 0xff : 0);
                        valb = (uint8_t)(((val & 2) != 0) ? 0xff : 0);
                        valc = (uint8_t)(((val & 4) != 0) ? 0xff : 0);
                        vald = (uint8_t)(((val & 8) != 0) ? 0xff : 0);
                        switch (svga.gdcreg[3] & 0x18)
                        {
                        case 0: /*Set*/
                                if ((writemask2 & 1) != 0)
                                        svga.vram[addr] =
                                                (uint8_t)((vala & svga.gdcreg[8]) | (svga.la & ~svga.gdcreg[8]));
                                if ((writemask2 & 2) != 0)
                                        svga.vram[addr | 0x1] =
                                                (uint8_t)((valb & svga.gdcreg[8]) | (svga.lb & ~svga.gdcreg[8]));
                                if ((writemask2 & 4) != 0)
                                        svga.vram[addr | 0x2] =
                                                (uint8_t)((valc & svga.gdcreg[8]) | (svga.lc & ~svga.gdcreg[8]));
                                if ((writemask2 & 8) != 0)
                                        svga.vram[addr | 0x3] =
                                                (uint8_t)((vald & svga.gdcreg[8]) | (svga.ld & ~svga.gdcreg[8]));
                                break;
                        case 8: /*AND*/
                                if ((writemask2 & 1) != 0)
                                        svga.vram[addr] = (uint8_t)((vala | ~svga.gdcreg[8]) & svga.la);
                                if ((writemask2 & 2) != 0)
                                        svga.vram[addr | 0x1] = (uint8_t)((valb | ~svga.gdcreg[8]) & svga.lb);
                                if ((writemask2 & 4) != 0)
                                        svga.vram[addr | 0x2] = (uint8_t)((valc | ~svga.gdcreg[8]) & svga.lc);
                                if ((writemask2 & 8) != 0)
                                        svga.vram[addr | 0x3] = (uint8_t)((vald | ~svga.gdcreg[8]) & svga.ld);
                                break;
                        case 0x10: /*OR*/
                                if ((writemask2 & 1) != 0)
                                        svga.vram[addr] = (uint8_t)((vala & svga.gdcreg[8]) | svga.la);
                                if ((writemask2 & 2) != 0)
                                        svga.vram[addr | 0x1] = (uint8_t)((valb & svga.gdcreg[8]) | svga.lb);
                                if ((writemask2 & 4) != 0)
                                        svga.vram[addr | 0x2] = (uint8_t)((valc & svga.gdcreg[8]) | svga.lc);
                                if ((writemask2 & 8) != 0)
                                        svga.vram[addr | 0x3] = (uint8_t)((vald & svga.gdcreg[8]) | svga.ld);
                                break;
                        case 0x18: /*XOR*/
                                if ((writemask2 & 1) != 0)
                                        svga.vram[addr] = (uint8_t)((vala & svga.gdcreg[8]) ^ svga.la);
                                if ((writemask2 & 2) != 0)
                                        svga.vram[addr | 0x1] = (uint8_t)((valb & svga.gdcreg[8]) ^ svga.lb);
                                if ((writemask2 & 4) != 0)
                                        svga.vram[addr | 0x2] = (uint8_t)((valc & svga.gdcreg[8]) ^ svga.lc);
                                if ((writemask2 & 8) != 0)
                                        svga.vram[addr | 0x3] = (uint8_t)((vald & svga.gdcreg[8]) ^ svga.ld);
                                break;
                        }
                }
                break;
        case 3:
                if ((svga.gdcreg[3] & 7) != 0)
                        val = svga_rotate[svga.gdcreg[3] & 7, val];
                wm = svga.gdcreg[8];
                svga.gdcreg[8] &= val;

                vala = (uint8_t)(((svga.gdcreg[0] & 1) != 0) ? 0xff : 0);
                valb = (uint8_t)(((svga.gdcreg[0] & 2) != 0) ? 0xff : 0);
                valc = (uint8_t)(((svga.gdcreg[0] & 4) != 0) ? 0xff : 0);
                vald = (uint8_t)(((svga.gdcreg[0] & 8) != 0) ? 0xff : 0);
                switch (svga.gdcreg[3] & 0x18)
                {
                case 0: /*Set*/
                        if ((writemask2 & 1) != 0)
                                svga.vram[addr] = (uint8_t)((vala & svga.gdcreg[8]) | (svga.la & ~svga.gdcreg[8]));
                        if ((writemask2 & 2) != 0)
                                svga.vram[addr | 0x1] =
                                        (uint8_t)((valb & svga.gdcreg[8]) | (svga.lb & ~svga.gdcreg[8]));
                        if ((writemask2 & 4) != 0)
                                svga.vram[addr | 0x2] =
                                        (uint8_t)((valc & svga.gdcreg[8]) | (svga.lc & ~svga.gdcreg[8]));
                        if ((writemask2 & 8) != 0)
                                svga.vram[addr | 0x3] =
                                        (uint8_t)((vald & svga.gdcreg[8]) | (svga.ld & ~svga.gdcreg[8]));
                        break;
                case 8: /*AND*/
                        if ((writemask2 & 1) != 0)
                                svga.vram[addr] = (uint8_t)((vala | ~svga.gdcreg[8]) & svga.la);
                        if ((writemask2 & 2) != 0)
                                svga.vram[addr | 0x1] = (uint8_t)((valb | ~svga.gdcreg[8]) & svga.lb);
                        if ((writemask2 & 4) != 0)
                                svga.vram[addr | 0x2] = (uint8_t)((valc | ~svga.gdcreg[8]) & svga.lc);
                        if ((writemask2 & 8) != 0)
                                svga.vram[addr | 0x3] = (uint8_t)((vald | ~svga.gdcreg[8]) & svga.ld);
                        break;
                case 0x10: /*OR*/
                        if ((writemask2 & 1) != 0)
                                svga.vram[addr] = (uint8_t)((vala & svga.gdcreg[8]) | svga.la);
                        if ((writemask2 & 2) != 0)
                                svga.vram[addr | 0x1] = (uint8_t)((valb & svga.gdcreg[8]) | svga.lb);
                        if ((writemask2 & 4) != 0)
                                svga.vram[addr | 0x2] = (uint8_t)((valc & svga.gdcreg[8]) | svga.lc);
                        if ((writemask2 & 8) != 0)
                                svga.vram[addr | 0x3] = (uint8_t)((vald & svga.gdcreg[8]) | svga.ld);
                        break;
                case 0x18: /*XOR*/
                        if ((writemask2 & 1) != 0)
                                svga.vram[addr] = (uint8_t)((vala & svga.gdcreg[8]) ^ svga.la);
                        if ((writemask2 & 2) != 0)
                                svga.vram[addr | 0x1] = (uint8_t)((valb & svga.gdcreg[8]) ^ svga.lb);
                        if ((writemask2 & 4) != 0)
                                svga.vram[addr | 0x2] = (uint8_t)((valc & svga.gdcreg[8]) ^ svga.lc);
                        if ((writemask2 & 8) != 0)
                                svga.vram[addr | 0x3] = (uint8_t)((vald & svga.gdcreg[8]) ^ svga.ld);
                        break;
                }
                svga.gdcreg[8] = wm;
                break;
        }
    }
}
