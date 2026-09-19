// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — outillage de vérification.
//
// LE DIFF DIFFÉRENTIEL — la mitigation du risque n°1.
//
// La réconciliation entre cycdiff, cycles, memcycs, fetchclocks et nextcyc n'a
// aucun invariant qui échoue bruyamment. Se tromper d'un cycle ne plante rien et
// ne fausse aucun registre : ça se découvre trois jalons plus tard sous forme
// d'un CGA à la mauvaise fréquence, avec des milliers de lignes de suspects.
//
// D'où la règle : on compare TOUT le vecteur d'état après CHAQUE instruction, y
// compris les statiques de préfetch, dès la première passe. Une erreur d'un
// cycle sort alors sur l'instruction qui l'a causée, opcode en main.
//
// La liste des champs comparés est écrite à la main, pas obtenue par réflexion :
// un champ oublié doit se voir en relecture. C'est le seul endroit du projet où
// l'exhaustivité prime sur la concision.

using iXtal26.Cpu;
using iXtal26.Diag;
using iXtal26.Memory;

namespace iXtal26.Diff;

public static class Fuzzer
{
    /// <summary>Générateur déterministe. Pas System.Random : on veut qu'un échec
    /// se rejoue à l'identique, y compris sur une autre machine ou runtime.</summary>
    private struct Lcg(ulong seed)
    {
        private ulong _s = seed == 0 ? 0x9E3779B97F4A7C15UL : seed;

        public uint Next()
        {
            _s ^= _s << 13;
            _s ^= _s >> 7;
            _s ^= _s << 17;
            return (uint)(_s >> 32);
        }

        /// <summary>Valeur 16 bits pondérée vers les frontières de signe et de
        /// retenue. Un tirage uniforme touche 0x8000 une fois sur 65 536 — c'est
        /// la différence entre trouver le bug à M1 et ne pas le trouver.</summary>
        public ushort Next16()
        {
            var r = Next();
            if ((r & 3) != 0)
                return (ushort)r;
            ushort[] edges = [0x0000, 0x0001, 0x007F, 0x0080, 0x00FF, 0x7FFF, 0x8000, 0xFFFF];
            return edges[(r >> 8) % (uint)edges.Length];
        }
    }

    public static int Run(byte[] opcodes, int rounds, int instrPerRound, ulong seed, bool verbose,
                          bool ramPerInstr = false)
    {
        Oracle.CheckAbi();
        Console.WriteLine($"Diff différentiel — opcodes {string.Join(",", opcodes.Select(o => $"0x{o:X2}"))}, " +
                          $"{rounds} rondes x {instrPerRound} instructions, graine {seed}");

        var rng = new Lcg(seed);
        var a = HState.Create(); // oracle C
        var b = HState.Create(); // C#
        var regs = new ushort[(int)R.COUNT];
        var total = 0L;

        for (var round = 0; round < rounds; round++)
        {
            // Remplissage identique des deux RAM : l'opcode choisi partout, si
            // bien que le flux ne s'épuise jamais et que tout préfetch, même
            // très en avance, lit la même chose des deux côtés.
            var fill = opcodes[rng.Next() % (uint)opcodes.Length];
            Oracle.h_reset();
            Oracle.h_fill_ram(fill);
            _808x.Reset();
            mem.fill_ram(fill);

            for (var i = 0; i < (int)R.COUNT; i++)
                regs[i] = rng.Next16();
            // CS:IP dans une zone sûre, loin de la table des vecteurs.
            regs[(int)R.CS] = 0x1000;
            regs[(int)R.IP] = (ushort)(rng.Next() & 0x0FFF);

            Oracle.h_setregs(regs);
            _808x.SetRegs(regs);

            for (var n = 0; n < instrPerRound; n++)
            {
                var cycC = Oracle.h_step();
                var cycS = _808x.Step();

                Oracle.h_getstate(out a);
                _808x.GetState(ref b);
                total++;

                var diff = Compare(a, b, cycC, cycS)
                           ?? (ramPerInstr ? CmpRam() : null);
                if (diff is null)
                    continue;

                Console.WriteLine($"\nDIVERGENCE ronde {round}, instruction {n} (globale {total})");
                Console.WriteLine($"  opcode 0x{fill:X2}, CS:IP initial {regs[(int)R.CS]:X4}:{regs[(int)R.IP]:X4}");
                Console.WriteLine($"  {diff}");
                Console.WriteLine($"\n  Rejouer : --seed {seed} --rounds {round + 1}");
                return 1;
            }

            // Comparaison mémoire en fin de ronde. Une divergence ici n'indique
            // pas QUELLE instruction l'a causée — mais elle est rare, et le rejeu
            // de la ronde avec --ram-per-instr la localise.
            var ramC = Oracle.h_ram_hash();
            var ramS = _808x.RamHash();
            if (ramC != ramS)
            {
                Console.WriteLine($"\nDIVERGENCE MÉMOIRE en fin de ronde {round}");
                Console.WriteLine($"  oracle {ramC:X16}, C# {ramS:X16}");
                Console.WriteLine($"  Localiser : --seed {seed} --rounds {round + 1} --ram-per-instr");
                return 1;
            }

            if (verbose && (round + 1) % 500 == 0)
                Console.WriteLine($"  ronde {round + 1}/{rounds} — {total} instructions, aucune divergence");
        }

        Console.WriteLine($"\nVert : {total} instructions, zéro divergence sur les {FieldCount} champs comparés.");
        return 0;
    }

    private const int FieldCount = 32;

    /// <summary>Rend null si les deux états sont identiques, sinon la
    /// description du premier champ divergent. Aucune réflexion : tout champ
    /// absent de cette liste est un champ non vérifié, et ça doit se voir.</summary>
    private static string? Compare(in HState a, in HState b, int cycA, int cycB)
    {
        if (cycA != cycB) return $"cycles consommés : oracle {cycA}, C# {cycB}";

        for (var i = 0; i < 8; i++)
            if (a.regs[i] != b.regs[i])
                return $"regs[{i}] : oracle 0x{a.regs[i]:X8}, C# 0x{b.regs[i]:X8}";

        for (var i = 0; i < (int)Seg.COUNT; i++)
        {
            if (a.seg_sel[i] != b.seg_sel[i])
                return $"{(Seg)i} sélecteur : oracle 0x{a.seg_sel[i]:X4}, C# 0x{b.seg_sel[i]:X4}";
            if (a.seg_base[i] != b.seg_base[i])
                return $"{(Seg)i} base : oracle 0x{a.seg_base[i]:X8}, C# 0x{b.seg_base[i]:X8}";
        }

        return Chk("flags", a.flags, b.flags)
            ?? Chk("eflags", a.eflags, b.eflags)
            ?? Chk("pc", a.pc, b.pc)
            ?? Chk("oldpc", a.oldpc, b.oldpc)
            ?? Chk("eaaddr", a.eaaddr, b.eaaddr)
            ?? Chk("ea_seg_idx", a.ea_seg_idx, b.ea_seg_idx)
            ?? Chk("ssegs", a.ssegs, b.ssegs)
            ?? Chk("abrt", a.abrt, b.abrt)
            // --- le modèle de temps : la raison d'être de ce harnais ---
            ?? Chk("cycles", a.cycles, b.cycles)
            ?? Chk("tsc", a.tsc, b.tsc)
            ?? Chk("tsc_frac", a.tsc_frac, b.tsc_frac)
            ?? Chk("memcycs", a.memcycs, b.memcycs)
            ?? Chk("fetchcycles", a.fetchcycles, b.fetchcycles)
            ?? Chk("fetchclocks", a.fetchclocks, b.fetchclocks)
            ?? Chk("nextcyc", a.nextcyc, b.nextcyc)
            ?? Chk("cycdiff", a.cycdiff, b.cycdiff)
            ?? Chk("current_diff", a.current_diff, b.current_diff)
            ?? Chk("prefetchw", a.prefetchw, b.prefetchw)
            ?? Chk("prefetchpc", a.prefetchpc, b.prefetchpc)
            ?? CmpQueue(a, b)
            // --- état d'interruption ---
            ?? Chk("noint", a.noint, b.noint)
            ?? Chk("inhlt", a.inhlt, b.inhlt)
            ?? Chk("takeint", a.takeint, b.takeint)
            // --- compteurs de stubs : détectent l'accord vide ---
            ?? Chk("n_inb", a.n_inb, b.n_inb)
            ?? Chk("n_outb", a.n_outb, b.n_outb)
            ?? Chk("n_picint", a.n_picint, b.n_picint)
            ?? Chk("n_picinterrupt", a.n_picinterrupt, b.n_picinterrupt)
            ?? Chk("n_timer_process", a.n_timer_process, b.n_timer_process)
            ?? Chk("n_readmembl", a.n_readmembl, b.n_readmembl)
            ?? Chk("n_writemembl", a.n_writemembl, b.n_writemembl)
            ?? Chk("n_readmemwl", a.n_readmemwl, b.n_readmemwl)
            ?? Chk("n_writememwl", a.n_writememwl, b.n_writememwl)
            ?? Chk("n_fatal", a.n_fatal, b.n_fatal)
            ?? Chk("ins", a.ins, b.ins);
        // La RAM n'est PAS hachée ici : 1 Mo par côté et par instruction, soit
        // des centaines de Go sur une passe longue. Elle est comparée en fin de
        // ronde (voir Run), et une divergence y déclenche un rejeu instruction
        // par instruction pour la localiser.
    }

    private static string? Chk<T>(string name, T x, T y) where T : IEquatable<T>
        => x.Equals(y) ? null : $"{name} : oracle {Fmt(x)}, C# {Fmt(y)}";

    private static string Fmt<T>(T v) => v switch
    {
        ulong u => $"0x{u:X}",
        uint u => $"0x{u:X8}",
        ushort u => $"0x{u:X4}",
        _ => v?.ToString() ?? "null",
    };

    private static string? CmpQueue(in HState a, in HState b)
    {
        for (var i = 0; i < 6; i++)
            if (a.prefetchqueue[i] != b.prefetchqueue[i])
                return $"prefetchqueue[{i}] : oracle 0x{a.prefetchqueue[i]:X2}, C# 0x{b.prefetchqueue[i]:X2}";
        return null;
    }

    private static string? CmpRam()
    {
        var ha = Oracle.h_ram_hash();
        var hb = _808x.RamHash();
        return ha == hb ? null : $"RAM : oracle {ha:X16}, C# {hb:X16}";
    }
}
