// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// PB-49 — LA BORNE DE L'OMBRE DE SS, prouvée des deux côtés.
//
// POP SS et MOV SS exécutent l'instruction suivante en appelant son handler, sans appel
// terminal : chez PCem une chaîne de chargements de SS est une récursion, et un segment
// rempli de 0x17 fait tomber l'oracle. Le C# borne la chaîne à _386.SS_SHADOW_MAX
// (`// pcem bug, not reproduced: PB-49`). Cette porte vérifie les deux moitiés du contrat :
//
//   1. EN DEÇÀ de la borne, le C# est l'oracle : N chargements de SS enchaînés puis un NOP,
//      un seul pas des deux côtés, état complet et RAM comparés — POP SS, MOV SS, et les
//      deux mêlés, sur le 286 et le 386 ;
//   2. AU-DELÀ, le C# rend la main au chargement SS_SHADOW_MAX + 1, sans plantage ni
//      profondeur résiduelle ; l'écart avec l'oracle, s'il y en a, est affiché ;
//   3. la RAM entière remplie de 0x17, C# seul (l'oracle y tombe) : des pas successifs,
//      chacun borné, sans plantage ni boucle, et la profondeur revenue à 0.

using iXtal26.Cpu;
using iXtal26.Diag;
using iXtal26.Memory;

namespace iXtal26.Diff;

internal static class PopSsCheck
{
    private const ushort CodeSeg = 0x1000;

    public static int Run()
    {
        Oracle.CheckAbi();
        var max = _386.SS_SHADOW_MAX;
        var bad = 0;
        Console.WriteLine($"popss-check — borne SS_SHADOW_MAX = {max} (PB-49)");

        (string nom, byte[] motif)[] chaines =
        [
            ("POP SS", [0x17]),
            ("MOV SS,AX", [0x8E, 0xD0]),
            ("POP SS + MOV SS,AX", [0x17, 0x8E, 0xD0]),
            // Le préfixe intercalé ne casse pas la chaîne : il saute sur le POP SS suivant, qui
            // le rappelle (la ronde 1139 du flux 386, graine 1). Le préfixe GS n'existe que
            // sur le 386 ; sur le 286, 65 est ILLEGAL et la chaîne s'arrête d'elle-même.
            ("POP SS + préfixe GS", [0x17, 0x65]),
        ];

        foreach (var core in new[] { Oracle.Core286, Oracle.Core386 })
        {
            var nomCoeur = core == Oracle.Core286 ? "286" : "386";
            foreach (var (nom, motif) in chaines)
            {
                // Chargements de SS par motif : 1 pour POP SS ou MOV SS, 2 pour le mélange.
                var parMotif = motif[0] == 0x17 && motif.Length == 3 ? 2 : 1;
                foreach (var n in new[] { 1, 2, 100, max - 1, max, max + 1, 4 * max })
                {
                    var repetitions = (n + parMotif - 1) / parMotif;
                    var charges = repetitions * parMotif;
                    var code = new byte[repetitions * motif.Length + 1];
                    for (var r = 0; r < repetitions; r++)
                        Array.Copy(motif, 0, code, r * motif.Length, motif.Length);
                    code[^1] = 0x90;

                    Prepare(core, code, oracleAussi: true);
                    var cycC = Oracle.h_step();
                    var cycS = _386.Step286();
                    Oracle.h_getstate(out var a);
                    var b = HState.Create();
                    _808x.GetState(ref b);
                    var diff = Fuzzer.CompareStates(a, b, cycC, cycS) ?? Fuzzer.CmpRam();

                    // En deçà de la borne : l'oracle, exactement. Au-delà : le C# a rendu la main
                    // au chargement (max+1) sans dispatcher la suite ; ce qui suit dépend du
                    // coût de ce chargement. POP SS compte 7 cycles et le pas s'arrête ; MOV SS
                    // n'en compte AUCUN (x86_ops_mov_seg.h:190 rend avant CLOCK_CYCLES), donc la
                    // boucle interne d'exec386 reprend la chaîne dans le même pas, ITÉRATIVEMENT.
                    // Le critère au-delà n'est donc pas un IP prédit : pas de plantage, pas de
                    // profondeur résiduelle, et l'écart éventuel est affiché, pas jugé.
                    string verdict;
                    if (charges <= max)
                    {
                        verdict = diff is null ? "identique à l'oracle" : $"ÉCART : {diff}";
                        if (diff is not null) bad++;
                    }
                    else
                        verdict = diff is null
                            ? $"au-delà de la borne, sans plantage ; identique à l'oracle (IP {b.pc:X4})"
                            : $"au-delà de la borne, sans plantage ; C# IP {b.pc:X4}, oracle IP {a.pc:X4} — écart attendu ({diff})";
                    if (_386.ss_shadow_depth != 0)
                    {
                        verdict += $" — profondeur non revenue à 0 ({_386.ss_shadow_depth})";
                        bad++;
                    }
                    Console.WriteLine($"  {nomCoeur} {nom,-19} × {charges,5} chargements : {verdict}");
                }
            }

            // La RAM entière remplie de 0x17, C# seul — le cas du fuzzeur en flux, où l'oracle
            // tombe. Le cœur 386 de PCem ne fait pas boucler IP en mode réel : la chaîne court
            // à travers toute la RAM, pas seulement un segment de 64 Ko.
            Prepare(core, [0x17], oracleAussi: false);
            mem.fill_ram(0x17);
            var pas = 0;
            var ipPrecedent = cpu_ip();
            for (; pas < 200; pas++)
            {
                _386.Step286();
                var ip = cpu_ip();
                if (ip - ipPrecedent != (uint)(max + 1) || _386.ss_shadow_depth != 0)
                    break;
                ipPrecedent = ip;
            }
            var okPlein = pas == 200;
            if (!okPlein) bad++;
            Console.WriteLine($"  {nomCoeur} RAM entière remplie de 0x17, C# seul : " +
                              (okPlein
                                  ? $"200 pas, chacun de {max + 1} POP SS, sans plantage ni boucle"
                                  : $"ÉCHEC au pas {pas} (IP {cpu_ip():X}, profondeur {_386.ss_shadow_depth})"));
        }

        Console.WriteLine(bad == 0 ? "\nVert : la borne tient, et l'oracle est reproduit en deçà."
                                   : $"\n{bad} échec(s).");
        return bad == 0 ? 0 : 1;
    }

    private static uint cpu_ip()
    {
        var s = HState.Create();
        _808x.GetState(ref s);
        return s.pc;
    }

    /// <summary>Les deux côtés remis comme le fuzzeur les remet, RAM à 0x90, le code en
    /// 1000:0000, SS:SP en 5000:0000. `oracleAussi` faux : le C# seul.</summary>
    private static void Prepare(int core, byte[] code, bool oracleAussi)
    {
        var regs = new ushort[(int)R.COUNT];
        regs[(int)R.CS] = CodeSeg;
        regs[(int)R.SS] = 0x5000;
        regs[(int)R.DS] = 0x6000;
        var linear = (uint)CodeSeg << 4;
        if (oracleAussi)
        {
            Oracle.h_set_core(core);
            Oracle.h_reset();
            Oracle.h_fill_ram(0x90);
            Oracle.h_load(linear, code, (uint)code.Length);
            Oracle.h_setregs(regs);
        }
        if (core == Oracle.Core386)
            _386.Reset386();
        else
            _386.Reset286();
        mem.fill_ram(0x90);
        for (var i = 0; i < code.Length; i++)
            mem.ram[(linear + i) & mem.rammask] = code[i];
        _808x.SetRegs(regs);
    }
}
