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

    /// <summary>
    /// Mode « une instruction » : à chaque itération, un opcode du jeu suivi
    /// d'octets aléatoires est posé à CS:IP, les 14 registres sont tirés, et on
    /// exécute UNE instruction. C'est le mode qui couvre réellement les opérandes
    /// — ModRM, déplacements, immédiats — là où le mode flux ne fait varier que
    /// l'enchaînement.
    ///
    /// Les écritures mémoire sont comparées par le journal d'écritures, pas par
    /// un hachage de la RAM : c'est exact, ça coûte quelques comparaisons au lieu
    /// de 2 Mo, et ça nomme l'adresse fautive.
    /// </summary>
    /// <summary>`core` : voir Run. AVEC UNE TABLE D'OPCODES PARTIELLE, C'EST LE
    /// SEUL MODE UTILISABLE — le mode flux enchaîne les instructions et finit par
    /// tomber sur un opcode non transcrit, qui échoue bruyamment et à raison.</summary>
    public static int RunSingle(byte[] opcodes, int iterations, ulong seed, bool verbose, int core)
    {
        Oracle.CheckAbi();
        Console.WriteLine($"Diff différentiel (mode simple) — opcodes " +
                          $"{string.Join(",", opcodes.Select(o => $"0x{o:X2}"))}, " +
                          $"{iterations} itérations, graine {seed}");

        var rng = new Lcg(seed);
        var a = HState.Create();
        var b = HState.Create();
        var regs = new ushort[(int)R.COUNT];
        var code = new byte[8];
        var perOpcode = new Dictionary<byte, int>();
        var steeredCount = 0;

        for (var it = 0; it < iterations; it++)
        {
            var op = opcodes[rng.Next() % (uint)opcodes.Length];
            perOpcode[op] = perOpcode.GetValueOrDefault(op) + 1;

            Oracle.h_set_core(core);
            Oracle.h_reset();
            Oracle.h_fill_ram(0x90);
            if (core == Oracle.Core286)
                _386.Reset286();
            else
                _808x.Reset();
            mem.fill_ram(0x90);

            for (var i = 0; i < (int)R.COUNT; i++)
                regs[i] = rng.Next16();
            regs[(int)R.CS] = 0x2000;
            regs[(int)R.IP] = (ushort)(rng.Next() & 0x0FFF);

            code[0] = op;
            for (var i = 1; i < code.Length; i++)
                code[i] = (byte)rng.Next();

            // Un préfixe de segment (26/2E/36/3E) ne fait que poser l'override et
            // sauter à opcodestart : l'octet SUIVANT est l'instruction réelle. Si
            // on le laisse aléatoire, il tombe sur un opcode non encore transcrit
            // et la divergence mesure ce trou, pas le préfixe. On le tire donc
            // dans le jeu testé, en excluant les préfixes eux-mêmes pour ne pas
            // enchaîner indéfiniment.
            if (IsSegPrefix(op))
            {
                var inner = op;
                for (var guard = 0; guard < 16 && IsSegPrefix(inner); guard++)
                    inner = opcodes[rng.Next() % (uint)opcodes.Length];
                if (IsSegPrefix(inner))
                    inner = 0x90;                    // NOP : repli sûr
                code[1] = inner;
            }

            // Cas AUTO-RÉFÉRENTIEL, une fois sur huit.
            //
            // readmemb (808x.c:57-64) ne facture memcycs QUE si l'adresse lue
            // diffère de cs + pc. Cette garde ne change donc rien tant que
            // l'opérande ne tombe pas exactement sur le pointeur d'instruction —
            // ce que des opérandes aléatoires ne produisent jamais (1 chance sur
            // 65 536). Sans ces cas construits, la garde est du code non testé
            // qu'on croit vérifié.
            //
            // Construction : adressage direct (mod=0, rm=6), déplacement = IP+4
            // — pc a alors avancé de l'opcode, du ModRM et des deux octets de
            // déplacement — et DS forcé égal à CS pour que les bases coïncident.
            // Applicable à tout opcode porteur d'un ModRM. Dans la bande ALU
            // 0x00-0x3F, ce sont ceux dont les trois bits bas valent 0 à 3 —
            // donc 00-03, 08-0B, 10-13… — les formes /r. Les accumulateur-
            // immédiat (x4, x5) et les PUSH/POP segment (x6, x7) n'en ont pas.
            var steered = (rng.Next() & 7) == 0 && (op & 7) < 4;
            if (steered)
            {
                regs[(int)R.DS] = regs[(int)R.CS];
                code[1] = 0x06;                                  // mod=00, rm=110 : direct
                var target = (ushort)(regs[(int)R.IP] + 4);
                code[2] = (byte)target;
                code[3] = (byte)(target >> 8);
            }

            var linear = (uint)(regs[(int)R.CS] << 4) + regs[(int)R.IP];
            Oracle.h_load(linear, code, (uint)code.Length);
            for (var i = 0; i < code.Length; i++)
                mem.ram[(linear + i) & mem.rammask] = code[i];

            if (steered)
                steeredCount++;

            Oracle.h_setregs(regs);
            _808x.SetRegs(regs);

            Oracle.h_wlog_reset();
            mem.wlog_reset();

            var cycC = Oracle.h_step();
            var cycS = core == Oracle.Core286 ? _386.Step286() : _808x.Step();

            Oracle.h_getstate(out a);
            _808x.GetState(ref b);

            var diff = Compare(a, b, cycC, cycS) ?? CmpWrites();
            if (diff is null)
                continue;

            Console.WriteLine($"\nDIVERGENCE itération {it}");
            Console.WriteLine($"  opcode 0x{op:X2}, octets {string.Join(" ", code.Select(x => x.ToString("X2")))}");
            Console.WriteLine($"  CS:IP {regs[(int)R.CS]:X4}:{regs[(int)R.IP]:X4}  " +
                              $"AX {regs[(int)R.AX]:X4} BX {regs[(int)R.BX]:X4} " +
                              $"CX {regs[(int)R.CX]:X4} DX {regs[(int)R.DX]:X4}");
            Console.WriteLine($"  DS {regs[(int)R.DS]:X4} ES {regs[(int)R.ES]:X4} SS {regs[(int)R.SS]:X4} " +
                              $"SP {regs[(int)R.SP]:X4} BP {regs[(int)R.BP]:X4} " +
                              $"SI {regs[(int)R.SI]:X4} DI {regs[(int)R.DI]:X4} FL {regs[(int)R.FLAGS]:X4}");
            Console.WriteLine($"  {diff}");
            Console.WriteLine($"\n  Rejouer : --mode single --seed {seed} --iter {it + 1}");
            return 1;
        }

        Console.WriteLine($"\nVert : {iterations} instructions, zéro divergence.");
        foreach (var (op, n) in perOpcode.OrderBy(kv => kv.Key))
            Console.WriteLine($"    0x{op:X2} : {n} tirages");
        Console.WriteLine($"    dont {steeredCount} cas auto-référentiels (EA == cs+pc)");
        return 0;
    }

    /// <summary>Compare les écritures mémoire de la dernière instruction.
    /// Exact, et nomme l'adresse fautive — là où un hachage dirait seulement
    /// « la RAM diffère ».</summary>
    private static string? CmpWrites()
    {
        var nC = Oracle.h_wlog_count();
        var nS = mem.wlog_n;
        if (nC != nS)
            return $"nombre d'écritures : oracle {nC}, C# {nS}";
        for (var i = 0; i < Math.Min(nC, mem.WLOG_MAX); i++)
        {
            if (Oracle.h_wlog_get_addr(i) != mem.wlog_addr[i])
                return $"écriture {i} adresse : oracle 0x{Oracle.h_wlog_get_addr(i):X5}, " +
                       $"C# 0x{mem.wlog_addr[i]:X5}";
            if (Oracle.h_wlog_get_val(i) != mem.wlog_val[i])
                return $"écriture {i} en 0x{mem.wlog_addr[i]:X5} : oracle 0x{Oracle.h_wlog_get_val(i):X2}, " +
                       $"C# 0x{mem.wlog_val[i]:X2}";
        }
        return null;
    }

    /// <summary>`core` vaut Oracle.Core8088 ou Oracle.Core286. Les deux côtés
    /// doivent être basculés ENSEMBLE et avant leur reset : c'est h_reset qui
    /// applique AT, et resetx86 en tire le vecteur de reset et rammask.</summary>
    public static int Run(byte[] opcodes, int rounds, int instrPerRound, ulong seed, bool verbose, int core,
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

            // Un flux UNIFORME de préfixe de segment ne retire jamais
            // d'instruction : `goto opcodestart` (808x.c:1589/1664/1739/1798)
            // saute DANS le corps de la boucle, donc `while (cycles > 0)` n'est
            // jamais réévalué. Les deux cœurs bouclent — d'accord entre eux, et
            // c'est aussi ce que ferait un 8088 réel. La ronde, elle, ne rend
            // jamais la main : mesuré, la ronde 157 de `--rounds 1200` tenait le
            // processeur à 100 % pendant 40 minutes sans verdict.
            //
            // On alterne donc le préfixe avec un opcode réel. Le chemin de
            // préfixe reste exercé — c'est même le seul endroit du mode flux qui
            // le fasse — et la ronde termine.
            var inner = fill;
            if (IsSegPrefix(fill))
            {
                for (var guard = 0; guard < 16 && IsSegPrefix(inner); guard++)
                    inner = opcodes[rng.Next() % (uint)opcodes.Length];
                if (IsSegPrefix(inner))
                    inner = 0x90;                    // NOP : repli sûr
            }

            Oracle.h_set_core(core);
            Oracle.h_reset();
            if (core == Oracle.Core286)
                _386.Reset286();
            else
                _808x.Reset();
            if (inner != fill)
            {
                Oracle.h_fill_ram2(fill, inner);
                mem.fill_ram2(fill, inner);
            }
            else
            {
                Oracle.h_fill_ram(fill);
                mem.fill_ram(fill);
            }

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
                var cycS = core == Oracle.Core286 ? _386.Step286() : _808x.Step();

                Oracle.h_getstate(out a);
                _808x.GetState(ref b);
                total++;

                var diff = Compare(a, b, cycC, cycS)
                           ?? (ramPerInstr ? CmpRam() : null);
                if (diff is null)
                    continue;

                Console.WriteLine($"\nDIVERGENCE ronde {round}, instruction {n} (globale {total})");
                Console.WriteLine($"  remplissage 0x{fill:X2}" + (inner != fill ? $"/0x{inner:X2}" : "") + $", CS:IP initial {regs[(int)R.CS]:X4}:{regs[(int)R.IP]:X4}");
                Console.WriteLine($"  {diff}");
                DumpSides(a, b);
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

    /* 32 d'origine, + 7 champs de cache descripteur par segment, + 9 champs par
     * descripteur système, + les 6 registres de contrôle, + les 4 drapeaux paresseux.
     * Compté en formes de champ, pas en entrées de tableau : la boucle en couvre 6, la
     * suivante 4. */
    private const int FieldCount = 60;

    private static bool IsSegPrefix(byte b) => b is 0x26 or 0x2E or 0x36 or 0x3E;

    /// <summary>Rend null si les deux états sont identiques, sinon la
    /// description du premier champ divergent. Aucune réflexion : tout champ
    /// absent de cette liste est un champ non vérifié, et ça doit se voir.</summary>
    /// <param name="counters">Comparer les compteurs de stubs. Vrai pour le
    /// fuzzer et pour SST, où chaque cas repart d'un h_reset()/Reset() qui les
    /// remet à zéro des deux côtés. Faux pour le diff d'amorçage : ce ne sont pas
    /// des champs de la machine, ils sont absents du hachage de trace, et les
    /// comparer arrête donc la phase 2 sur du bruit d'instrumentation avant
    /// qu'elle atteigne la divergence que la phase 1 a signalée.</param>
    internal static string? CompareStates(in HState a, in HState b, int cycA, int cycB, bool counters = true)
        => Compare(a, b, cycA, cycB, counters);

    internal static string? Compare(in HState a, in HState b, int cycA, int cycB, bool counters = true)
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

            // Le cache descripteur. Inerte sur un 8088 — loadseg n'y touche pas quand
            // msw & 1 vaut zéro — mais comparé dès maintenant : c'est ce qui vérifie le
            // câblage des deux côtés pendant qu'il ne varie pas encore.
            if (a.seg_limit[i] != b.seg_limit[i])
                return $"{(Seg)i} limit : oracle 0x{a.seg_limit[i]:X8}, C# 0x{b.seg_limit[i]:X8}";
            if (a.seg_limit_raw[i] != b.seg_limit_raw[i])
                return $"{(Seg)i} limit_raw : oracle 0x{a.seg_limit_raw[i]:X8}, C# 0x{b.seg_limit_raw[i]:X8}";
            if (a.seg_limit_low[i] != b.seg_limit_low[i])
                return $"{(Seg)i} limit_low : oracle 0x{a.seg_limit_low[i]:X8}, C# 0x{b.seg_limit_low[i]:X8}";
            if (a.seg_limit_high[i] != b.seg_limit_high[i])
                return $"{(Seg)i} limit_high : oracle 0x{a.seg_limit_high[i]:X8}, C# 0x{b.seg_limit_high[i]:X8}";
            if (a.seg_access[i] != b.seg_access[i])
                return $"{(Seg)i} access : oracle 0x{a.seg_access[i]:X2}, C# 0x{b.seg_access[i]:X2}";
            if (a.seg_access2[i] != b.seg_access2[i])
                return $"{(Seg)i} access2 : oracle 0x{a.seg_access2[i]:X2}, C# 0x{b.seg_access2[i]:X2}";
            if (a.seg_checked[i] != b.seg_checked[i])
                return $"{(Seg)i} checked : oracle {a.seg_checked[i]}, C# {b.seg_checked[i]}";
        }

        // Les quatre descripteurs système. Inertes sur un 8088 : rien ne les écrit hors
        // de LGDT/LIDT/LLDT/LTR, qui n'existent pas ici. Comparés quand même, et les neuf
        // champs, y compris les trois que PCem ne pose jamais pour eux (limit_low,
        // limit_high, checked) — le cas à attraper est le C# qui écrirait là où le C
        // s'abstient.
        for (var i = 0; i < (int)Sys.COUNT; i++)
        {
            if (a.sys_base[i] != b.sys_base[i])
                return $"{(Sys)i} base : oracle 0x{a.sys_base[i]:X8}, C# 0x{b.sys_base[i]:X8}";
            if (a.sys_limit[i] != b.sys_limit[i])
                return $"{(Sys)i} limit : oracle 0x{a.sys_limit[i]:X8}, C# 0x{b.sys_limit[i]:X8}";
            if (a.sys_limit_raw[i] != b.sys_limit_raw[i])
                return $"{(Sys)i} limit_raw : oracle 0x{a.sys_limit_raw[i]:X8}, C# 0x{b.sys_limit_raw[i]:X8}";
            if (a.sys_limit_low[i] != b.sys_limit_low[i])
                return $"{(Sys)i} limit_low : oracle 0x{a.sys_limit_low[i]:X8}, C# 0x{b.sys_limit_low[i]:X8}";
            if (a.sys_limit_high[i] != b.sys_limit_high[i])
                return $"{(Sys)i} limit_high : oracle 0x{a.sys_limit_high[i]:X8}, C# 0x{b.sys_limit_high[i]:X8}";
            if (a.sys_checked[i] != b.sys_checked[i])
                return $"{(Sys)i} checked : oracle {a.sys_checked[i]}, C# {b.sys_checked[i]}";
            if (a.sys_sel[i] != b.sys_sel[i])
                return $"{(Sys)i} sélecteur : oracle 0x{a.sys_sel[i]:X4}, C# 0x{b.sys_sel[i]:X4}";
            if (a.sys_access[i] != b.sys_access[i])
                return $"{(Sys)i} access : oracle 0x{a.sys_access[i]:X2}, C# 0x{b.sys_access[i]:X2}";
            if (a.sys_access2[i] != b.sys_access2[i])
                return $"{(Sys)i} access2 : oracle 0x{a.sys_access2[i]:X2}, C# 0x{b.sys_access2[i]:X2}";
        }

        return Chk("cr0", a.cr0, b.cr0)
            ?? Chk("cr2", a.cr2, b.cr2)
            ?? Chk("cr3", a.cr3, b.cr3)
            ?? Chk("use32", a.use32, b.use32)
            ?? Chk("stack32", a.stack32, b.stack32)
            ?? Chk("cpl_override", a.cpl_override, b.cpl_override)
            // Les drapeaux paresseux AVANT `flags` : quand les deux divergent, c'est la
            // cause qu'on veut lire, pas la conséquence.
            ?? Chk("flags_op", a.flags_op, b.flags_op)
            ?? Chk("flags_res", a.flags_res, b.flags_res)
            ?? Chk("flags_op1", a.flags_op1, b.flags_op1)
            ?? Chk("flags_op2", a.flags_op2, b.flags_op2)
            ?? Chk("prefetch_bytes", a.prefetch_bytes, b.prefetch_bytes)
            ?? Chk("prefetch_prefixes", a.prefetch_prefixes, b.prefetch_prefixes)
            ?? Chk("flags", a.flags, b.flags)
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
            ?? (!counters ? null
            : Chk("n_inb", a.n_inb, b.n_inb)
            ?? Chk("n_outb", a.n_outb, b.n_outb)
            ?? Chk("n_picint", a.n_picint, b.n_picint)
            ?? Chk("n_picinterrupt", a.n_picinterrupt, b.n_picinterrupt)
            ?? Chk("n_timer_process", a.n_timer_process, b.n_timer_process))
            // Les quatre compteurs mémoire ne sont PLUS comparés depuis M2.
            //
            // Ils dataient de l'ère des stubs, où leur rôle était de détecter
            // l'« accord vide » — deux stubs rendant la même constante sans que le
            // chemin soit exercé. Les deux côtés exécutent désormais le VRAI mem.c,
            // donc ce risque a disparu : le comportement mémoire est comparé par les
            // cycles (addreadlookup facture -9), par le journal d'écritures et par
            // le hachage de RAM.
            //
            // Et ils ne peuvent PAS être rendus équivalents : côté C, -Wl,--wrap
            // n'intercepte que les appels venus d'une autre unité de traduction, si
            // bien que writememwl -> writemembl (mot à cheval sur une page) compte
            // une fois en C et deux en C#. Les garder produirait des faux positifs
            // qui masqueraient les vrais.
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
    /// <summary>État architectural des deux côtés, côte à côte. Le message de
    /// divergence ne nomme que le PREMIER champ qui diffère ; pour savoir d'où il
    /// sort il faut voir les segments et le pointeur de pile.</summary>
    private static void DumpSides(in HState a, in HState b)
    {
        string[] rn = { "AX", "CX", "DX", "BX", "SP", "BP", "SI", "DI" };
        for (var i = 0; i < 8; i++)
            Console.WriteLine($"    {rn[i]}  oracle {a.regs[i] & 0xFFFF:X4}   C# {b.regs[i] & 0xFFFF:X4}" +
                              (a.regs[i] != b.regs[i] ? "   <<<" : ""));
        for (var i = 0; i < (int)Seg.COUNT; i++)
            Console.WriteLine($"    {(Seg)i,-4} oracle {a.seg_sel[i]:X4}:{a.seg_base[i]:X5}   " +
                              $"C# {b.seg_sel[i]:X4}:{b.seg_base[i]:X5}" +
                              (a.seg_sel[i] != b.seg_sel[i] || a.seg_base[i] != b.seg_base[i] ? "   <<<" : ""));
        Console.WriteLine($"    pc    oracle {a.pc:X4} (old {a.oldpc:X4})   C# {b.pc:X4} (old {b.oldpc:X4})");
        Console.WriteLine($"    ea    oracle {a.eaaddr:X8} seg {a.ea_seg_idx}   C# {b.eaaddr:X8} seg {b.ea_seg_idx}");
        Console.WriteLine($"    flags oracle {a.flags:X4}   C# {b.flags:X4}");
    }
}
