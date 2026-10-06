// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// emu8k-kernel-check (G12.2) — LES NOYAUX DE L'EMU8000, des deux côtés, sur des états fabriqués.
//
// Les peignes, les diffuseurs, les queues et l'amortisseur de la réverbération, le chorus et la pente du volume
// (sound_emu8k.c:1416-1602) font leurs calculs en float et en double, et les rendent à l'int32 par la conversion du C
// (cvttss2si : INT_MIN hors bornes, PB-163). Aucun invité n'y pousse de valeurs extrêmes en temps de porte : les
// états sont donc fabriqués, des deux côtés, par le même générateur (splitmix64, le pendant de h_emu8k_kernel dans
// harness_emu8k.c) — des valeurs jusqu'aux bornes de l'int32, des réglages pris parmi ceux que les registres posent —
// puis chaque noyau tourne 4 096 pas, et l'on compare ses sorties et son état final. Les tables (chortable) sont
// d'abord remplies des deux côtés par le vrai emu8k_init (h_emu8k_tables, emu8k-tables-check).

using iXtal26.Sound;

namespace iXtal26.Diff;

internal static class EmuKernelCheck
{
    private const int N = 4096;
    private const int Graines = 48;
    private const ulong Seed = 1469598103934665603UL;
    private const ulong Prime = 1099511628211UL;

    private static readonly string[] Noms = ["le peigne", "le diffuseur", "la queue", "l'amortisseur", "le chorus",
                                             "la réverbération", "la pente du volume"];

    internal static int Run(string romsPath)
    {
        Oracle.CheckAbi();
        if (Oracle.h_emu8k_tables(romsPath, new long[65536], new int[256], new int[65537], new int[65537], new int[65537],
                                  new int[128], new int[65536], new long[256], new double[65536], new int[16 * 256 * 3],
                                  new float[4096], out _) == 0)
        {
            Console.WriteLine("ROUGE : roms/awe32.raw absente — rien n'est comparé.");
            return 1;
        }
        sound_emu8k.tables_init();

        var bad = 0;
        var o = new ulong[3];
        for (var kind = 0; kind < Noms.Length; kind++)
        {
            long minimums = 0;
            var faux = 0;
            for (var g = 1; g <= Graines; g++)
            {
                var graine = (ulong)(kind * 1000 + g);
                Oracle.h_emu8k_kernel(kind, graine, o);
                var c = Noyau(kind, graine);
                if (o[0] != c[0] || o[1] != c[1] || o[2] != c[2])
                {
                    if (faux++ < 3)
                        Console.WriteLine($"  {Noms[kind]}, graine {graine} : sorties {o[0]:X16} | {c[0]:X16}, état {o[1]:X16} | " +
                                          $"{c[1]:X16}, INT_MIN {o[2]} | {c[2]}");
                }
                minimums += (long)o[2];
            }
            Console.WriteLine($"  {Noms[kind]} : {(faux == 0 ? $"{Graines} états identiques" : $"{faux} état(s) divergent(s) sur {Graines}")}, " +
                              $"{minimums} sortie(s) à INT_MIN");
            bad += faux;
        }
        Console.WriteLine(bad == 0
            ? $"Vert : les sept noyaux de l'EMU8000 identiques sur {Graines} états fabriqués chacun, {N} pas par état."
            : $"ROUGE : {bad} état(s) divergent(s).");
        return bad == 0 ? 0 : 1;
    }

    private static ulong Sm(ref ulong s)
    {
        var z = s += 0x9E3779B97F4A7C15UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    // Un int32 : un échantillon, une somme de voix (±2^25), n'importe lequel, ou l'une des bornes ; pendant de h_val.
    private static int Val(ref ulong s)
    {
        var r = Sm(ref s);
        return (r & 3) switch
        {
            0 => (int)((r >> 8) & 0xFFFF) - 0x8000,
            1 => (int)((r >> 8) & 0x3FFFFFF) - 0x2000000,
            2 => unchecked((int)(uint)(r >> 32)),
            _ => (r & 4) != 0 ? unchecked((int)(0x7FFFFFFFu - (uint)((r >> 8) & 0xFF)))
                              : unchecked((int)(0x80000000u + (uint)((r >> 8) & 0xFF))),
        };
    }

    // Pendant de h_fab_comb.
    private static void FabComb(emu8k_reverb_combfilter_t c, ref ulong s, bool diff)
    {
        var r = Sm(ref s);
        var b = Sm(ref s);
        c.bufsize = 1 + (int)((r >> 8) % sound_emu8k.MAX_REFL_SIZE);
        c.read_pos = (int)((r >> 24) % (ulong)c.bufsize);
        var v = (int)(r & 0xFF);
        if ((b & 1) != 0)
            v |= 0xF0;
        c.output_gain = (float)(((v & 0xF0) >> 4) / 15.0);
        var f = (int)((r >> 40) & 0xFF);
        if ((b & 2) != 0)
            f = 0xFF;
        c.feedback = diff ? f / (float)0xFF : (float)((f & 0xF) / 15.0);
        v = (int)((r >> 48) & 0xFF);
        if ((b & 4) != 0)
            v = (b & 8) != 0 ? 0 : 0xFF;
        c.damp1 = (float)(v / 255.0);
        c.damp2 = (float)((0xFF - v) / 255.0);
        c.filterstore = Val(ref s);
        for (var i = 0; i < sound_emu8k.MAX_REFL_SIZE; i++)
            c.reflection[i] = Val(ref s);
    }

    // Pendant de h_fab_chorus : le tampon commun, la gauche puis la droite.
    private static void FabChorus(emu8k_chorus_eng_t e, ref ulong s)
    {
        var r = Sm(ref s);
        e.write = (int)(r % sound_emu8k.EMU8K_LFOCHORUS_SIZE);
        e.feedback = (int)((r >> 16) & 0xFF);
        e.delay_samples_central = (int)((r >> 24) & 0x1FFF);
        e.lfodepth_multip = ((int)((r >> 40) & 0xFF) * e.delay_samples_central) >> 8;
        r = Sm(ref s);
        e.delay_offset_samples_right = ((double)(int)(r & 0x1FFFFF)) / 256.0;
        var osc = (double)(uint)(r >> 32);
        osc *= 65.536 / 44100.0;
        osc *= 65536.0 * 65536.0;
        e.lfo_inc.addr = Cpu._386.CvtU64(osc);
        e.lfo_pos.addr = Sm(ref s) & 0xFFFFFFFFFFFFUL;
        for (var i = 0; i < 2 * sound_emu8k.EMU8K_LFOCHORUS_SIZE; i++)
            e.chorus_buffer[i] = Val(ref s);
    }

    // Pendant de h_fab_reverb.
    private static void FabReverb(emu8k_reverb_eng_t e, ref ulong s)
    {
        var r = Sm(ref s);
        e.out_mix = (short)(r & 0xFF);
        e.link_return_amp = (short)((r >> 8) & 0xFF);
        e.link_return_type = (sbyte)((r >> 16) & 1);
        e.refl_in_amp = (byte)(r >> 24);
        for (var c = 0; c < 6; c++)
            FabComb(e.reflections[c], ref s, false);
        for (var c = 0; c < 8; c++)
            FabComb(e.allpass[c], ref s, true);
        FabComb(e.tailL, ref s, false);
        FabComb(e.tailR, ref s, false);
        FabComb(e.damper, ref s, false);
    }

    private static ulong Mot(ulong h, ulong v) => (h ^ v) * Prime;

    private static ulong Octets(ulong h, ReadOnlySpan<byte> p)
    {
        foreach (var b in p)
            h = (h ^ b) * Prime;
        return h;
    }

    private static ulong ReverbFnv(emu8k_reverb_eng_t e)
    {
        var h = Mot(Seed, (ulong)(long)e.out_mix);
        h = Mot(h, (ulong)(long)e.link_return_amp);
        h = Mot(h, (ulong)(long)e.link_return_type);
        h = Mot(h, e.refl_in_amp);
        for (var c = 0; c < 6; c++)
            h = Mot(h, sound_emu8k.CombFnv(e.reflections[c]));
        for (var c = 0; c < 8; c++)
            h = Mot(h, sound_emu8k.CombFnv(e.allpass[c]));
        h = Mot(h, sound_emu8k.CombFnv(e.tailL));
        h = Mot(h, sound_emu8k.CombFnv(e.tailR));
        return Mot(h, sound_emu8k.CombFnv(e.damper));
    }

    /// <summary>Pendant de h_emu8k_kernel : les sorties, l'état final, le nombre de sorties à INT_MIN.</summary>
    private static ulong[] Noyau(int kind, ulong graine)
    {
        var s = graine;
        var h = Seed;
        var n = 0UL;
        var o = new ulong[3];
        var rv = new emu8k_reverb_eng_t();
        var @in = new int[N];
        var @out = new int[2 * N];
        switch (kind)
        {
            case 0:
            case 1:
            case 2:
            case 3:
                FabComb(rv.tailL, ref s, kind == 1);
                for (var c = 0; c < 4; c++)
                    FabComb(rv.allpass[c], ref s, true);
                for (var c = 0; c < N; c++)
                {
                    var x = Val(ref s);
                    x = kind switch
                    {
                        0 => sound_emu8k.emu8k_reverb_comb_work(rv.tailL, x),
                        1 => sound_emu8k.emu8k_reverb_diffuser_work(rv.tailL, x),
                        2 => sound_emu8k.emu8k_reverb_tail_work(rv.tailL, rv.allpass, 0, x),
                        _ => sound_emu8k.emu8k_reverb_damper_work(rv.tailL, x),
                    };
                    n += x == int.MinValue ? 1UL : 0UL;
                    h = Mot(h, (ulong)(long)x);
                }
                o[1] = sound_emu8k.CombFnv(rv.tailL);
                for (var c = 0; c < 4; c++)
                    o[1] = Mot(o[1], sound_emu8k.CombFnv(rv.allpass[c]));
                break;
            case 4:
            {
                var ch = new emu8k_chorus_eng_t();
                FabChorus(ch, ref s);
                for (var c = 0; c < N; c++)
                    @in[c] = Val(ref s);
                for (var c = 0; c < 2 * N; c++)
                    @out[c] = Val(ref s);
                sound_emu8k.emu8k_work_chorus(@in, 0, @out, 0, ch, N);
                o[1] = Mot(Mot(Seed, (ulong)(long)ch.write), ch.lfo_pos.addr);
                o[1] = Octets(o[1], System.Runtime.InteropServices.MemoryMarshal.AsBytes(ch.chorus_buffer.AsSpan()));
                break;
            }
            case 5:
                FabReverb(rv, ref s);
                for (var c = 0; c < N; c++)
                    @in[c] = Val(ref s);
                for (var c = 0; c < 2 * N; c++)
                    @out[c] = Val(ref s);
                sound_emu8k.emu8k_work_reverb(@in, 0, @out, 0, rv, N);
                o[1] = ReverbFnv(rv);
                break;
            default:
            {
                var sl = new emu8k_slide_t { last = (int)(Sm(ref s) & 0x3FFFF) - 0x20000 };
                for (var c = 0; c < N; c++)
                {
                    var x = sound_emu8k.emu8k_vol_slide(sl, (int)(Sm(ref s) & 0x3FFFF) - 0x20000);
                    h = Mot(h, (ulong)(long)x);
                }
                o[1] = (ulong)(long)sl.last;
                break;
            }
        }
        if (kind is 4 or 5)
            foreach (var x in @out)
            {
                n += x == int.MinValue ? 1UL : 0UL;
                h = Mot(h, (ulong)(long)x);
            }
        o[0] = h;
        o[2] = n;
        return o;
    }
}
