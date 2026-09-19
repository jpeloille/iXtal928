// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — outillage de vérification.
//
// DIFF DE TRACE D'AMORÇAGE.
//
// Le fuzzer compare une instruction tirée au sort dans un état tiré au sort. Il
// ne dit rien d'une machine qui démarre : là, ce sont des millions
// d'instructions enchaînées, où l'état de l'une conditionne la suivante, et où
// une divergence se propage jusqu'à devenir illisible.
//
// D'où la forme en deux phases du plan :
//   1. chaque côté émet UN HACHAGE de 8 octets par instruction — 3 Mo/s au lieu
//      de 44 — et on cherche le premier index qui diffère ;
//   2. on rejoue les deux côtés autour de cet index avec l'état complet.
//
// Le hachage porte sur ce que les deux côtés peuvent reproduire exactement :
// CS, pc, les huit registres, DS/ES/SS, flags, tsc. TraceHash() doit rester le
// pendant EXACT de h_trace_note() (tools/oracle/harness.c) — même champs, même
// ordre, même FNV-1a — sans quoi la phase 1 signale une divergence de hachage là
// où les machines sont d'accord.

using iXtal26.Cpu;
using iXtal26.Memory;

namespace iXtal26.Diff;

public static class BootDiff
{
    public static int Run(string romsPath, int slices)
    {
        Oracle.CheckAbi();

        var oraclePath = Path.Combine(Path.GetTempPath(), "ixtal-boot-oracle.bin");

        Console.WriteLine($"Amorçage de l'oracle C ({slices} tranches)…");
        if (Oracle.h_boot(romsPath) == 0)
        {
            Console.Error.WriteLine($"L'oracle n'a pas pu charger le BIOS depuis « {romsPath} ».");
            return 1;
        }
        if (Oracle.h_trace_open(oraclePath) == 0)
        {
            Console.Error.WriteLine($"Impossible d'écrire {oraclePath}.");
            return 1;
        }
        for (var i = 0; i < slices; i++)
            Oracle.h_runpc();
        Oracle.h_trace_close();

        var oracleTrace = File.ReadAllBytes(oraclePath);
        var nOracle = oracleTrace.Length / 8;
        Console.WriteLine($"  oracle : {nOracle} instructions tracées");

        Console.WriteLine($"Amorçage du cœur C# ({slices} tranches)…");
        _808x.ResetDiagState();
        if (!pc.initpc(romsPath))
            return 1;

        var diverged = -1;
        var n = 0;
        for (var s = 0; s < slices && diverged < 0; s++)
        {
            var budget = pc.cpu_get_speed() / 100;
            while (budget > 0)
            {
                budget -= _808x.Step();
                var h = TraceHash();
                if (n < nOracle)
                {
                    var o = BitConverter.ToUInt64(oracleTrace, n * 8);
                    if (o != h) { diverged = n; break; }
                }
                n++;
            }
        }

        if (diverged < 0)
        {
            // Un vert ne vaut que si les DEUX côtés ont exécuté le même nombre
            // d'instructions dans le même budget de cycles. Si le C# en fait moins,
            // la queue de la trace oracle n'a jamais été comparée et « identiques »
            // décrirait un préfixe, pas la course.
            if (n != nOracle)
            {
                Console.Error.WriteLine(
                    $"\nÉCART DE LONGUEUR : oracle {nOracle} instructions, C# {n}, " +
                    "à budget de cycles égal. Les hachages concordent sur le préfixe " +
                    "commun ; c'est la comptabilité de cycles qui diverge.");
                return 1;
            }

            Console.WriteLine($"\nVert : {n} instructions, les deux amorçages sont identiques.");
            return 0;
        }

        Console.WriteLine($"\nPREMIÈRE DIVERGENCE à l'instruction {diverged}");
        Console.WriteLine("Phase 2 — rejeu en pas à pas, état complet :\n");
        return Phase2(romsPath, diverged);
    }

    /// <summary>
    /// Phase 2 : on rejoue les DEUX cœurs depuis le reset, une instruction à la
    /// fois, et on compare le vecteur d'état complet à l'index fautif. La phase 1
    /// dit OÙ ; celle-ci dit QUOI.
    /// </summary>
    private static int Phase2(string romsPath, int index)
    {
        if (Oracle.h_boot(romsPath) == 0) return 1;
        _808x.ResetDiagState();
        if (!pc.initpc(romsPath)) return 1;

        var a = Diag.HState.Create();
        var b = Diag.HState.Create();
        var regs = new ushort[(int)Diag.R.COUNT];

        var pitFirst = -1;
        var a0 = Diag.HState.Create();
        var before = new ulong[3][];
        for (var t = 0; t < 3; t++) before[t] = new ulong[PitFields.Length];

        for (var i = 0; i <= index; i++)
        {
            // État du PIT AVANT l'instruction. Les deux côtés sont encore
            // d'accord ici : c'est la ligne de départ commune qui rend lisible
            // ce que l'instruction a fait diverger.
            if (pitFirst < 0)
                for (var t = 0; t < 3; t++)
                    Oracle.h_pit_probe(t, before[t]);

            var cycC = Oracle.h_step();
            var cycS = _808x.Step();

            if (pitFirst < 0)
                Oracle.h_getstate(out a0);

            if (pitFirst < 0 && PitDiverges())
            {
                pitFirst = i;
                Console.WriteLine($"  [PIT] premier écart d'état à l'instruction {i}, " +
                                  $"CS:IP {a0.seg_sel[0]:X4}:{a0.oldpc:X4} — soit {index - i} " +
                                  "instructions AVANT la divergence architecturale.");
                DumpPitFull(before);
                Console.WriteLine();
            }

            if (i < index - 3)
                continue;

            Oracle.h_getstate(out a);
            _808x.GetState(ref b);
            Oracle.h_getregs(regs);

            var mark = i == index ? ">>" : "  ";
            Console.WriteLine($"{mark} #{i}  oracle CS:IP {a.seg_sel[0]:X4}:{a.pc:X4} " +
                              $"AX {a.regs[0]:X4} BX {a.regs[3]:X4} CX {a.regs[1]:X4} DX {a.regs[2]:X4} " +
                              $"FL {a.flags:X4} cyc {cycC}");
            Console.WriteLine($"{mark}      C#     CS:IP {b.seg_sel[0]:X4}:{b.pc:X4} " +
                              $"AX {b.regs[0]:X4} BX {b.regs[3]:X4} CX {b.regs[1]:X4} DX {b.regs[2]:X4} " +
                              $"FL {b.flags:X4} cyc {cycS}");

            var d = Fuzzer.CompareStates(a, b, cycC, cycS, counters: false);
            if (d is not null)
            {
                Console.WriteLine($"\n  -> {d}");
                DumpPit();
                var lin = (a.seg_base[0] + a.oldpc) & 0xFFFFF;
                var bo = new byte[6];
                Oracle.h_read(lin, bo, 6);
                Console.WriteLine($"  octets à cs:oldpc {lin:X5} : {string.Join(" ", bo.Select(x => x.ToString("X2")))}");
                return 1;
            }
        }

        Console.WriteLine("\n  Les états concordent à l'index signalé — le hachage porte sur moins de");
        Console.WriteLine("  champs que le vecteur complet ; vérifier TraceHash contre h_trace_note.");
        return 1;
    }

    /// <summary>Doit rester le pendant exact de h_trace_note() (harness.c).</summary>
    internal static ulong TraceHash()
    {
        ulong h = 1469598103934665603UL;
        void Mix(ulong x)
        {
            for (var i = 0; i < 8; i++)
            {
                h ^= (x >> (i * 8)) & 0xff;
                h *= 1099511628211UL;
            }
        }
        var st = _386_common.cpu_state;
        Mix(st.seg_cs.seg);
        Mix(st.pc);
        for (var i = 0; i < 8; i++) Mix(st.regs[i].w);
        Mix(st.seg_ds.seg);
        Mix(st.seg_es.seg);
        Mix(st.seg_ss.seg);
        Mix(st.flags);
        Mix(timer.tsc);
        return h;
    }
    private static readonly string[] PitFields =
    {
        "l", "m", "count", "rl", "using_timer", "gate", "enabled", "running", "disabled", "thit", "latched", "rereadlatch", "rm", "out", "timer.enabled", "timer.ts", "tsc", "PITCONST", "remaining",
    };

    /// <summary>Affiche l'état des trois canaux du PIT des deux côtés. La sonde
    /// n'instrumente pas le C : pit est une globale de pit.c et le harnais est
    /// lié avec, donc h_pit_probe() se contente de la lire.</summary>
    private static void DumpPit()
    {
        var oc = new ulong[PitFields.Length];
        var cs = new ulong[PitFields.Length];

        for (var t = 0; t < 3; t++)
        {
            Oracle.h_pit_probe(t, oc);
            iXtal26.Models.pit.Probe(t, cs);

            var diff = new List<string>();
            for (var f = 0; f < PitFields.Length; f++)
                if (oc[f] != cs[f])
                    diff.Add($"{PitFields[f]}: oracle {oc[f]} / C# {cs[f]}");

            Console.WriteLine(diff.Count == 0
                ? $"  PIT canal {t} : identique"
                : $"  PIT canal {t} : {string.Join("  |  ", diff)}");
        }
    }

    /// <summary>Vrai dès qu'un champ de la sonde diverge sur l'un des trois canaux.</summary>
    private static bool PitDiverges()
    {
        var oc = new ulong[PitFields.Length];
        var cs = new ulong[PitFields.Length];

        for (var t = 0; t < 3; t++)
        {
            Oracle.h_pit_probe(t, oc);
            iXtal26.Models.pit.Probe(t, cs);
            for (var f = 0; f < PitFields.Length; f++)
                if (oc[f] != cs[f])
                    return true;
        }

        return false;
    }

    /// <summary>Dump complet des trois canaux : état d'entrée (commun aux deux
    /// cœurs) puis état de sortie de chaque côté. Sans l'état d'entrée on ne peut
    /// pas rejouer à la main la branche prise par pit_set_gate_no_timer.</summary>
    private static void DumpPitFull(ulong[][] before)
    {
        var oc = new ulong[PitFields.Length];
        var cs = new ulong[PitFields.Length];

        for (var t = 0; t < 3; t++)
        {
            Oracle.h_pit_probe(t, oc);
            iXtal26.Models.pit.Probe(t, cs);

            var same = true;
            for (var f = 0; f < PitFields.Length; f++) same &= oc[f] == cs[f];
            if (same) continue;

            Console.WriteLine($"  canal {t} :");
            for (var f = 0; f < PitFields.Length; f++)
            {
                var flag = oc[f] == cs[f] ? " " : "*";
                Console.WriteLine($"   {flag} {PitFields[f],-14} avant {before[t][f],22}" +
                                  $" | oracle {oc[f],22} | C# {cs[f],22}");
            }
        }
    }

}
