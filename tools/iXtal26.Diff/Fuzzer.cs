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
            //
            // 0x17 (POP SS) TOMBE SOUS LA MÊME RÈGLE, pour une raison différente :
            // il n'est pas un préfixe, mais il EXÉCUTE l'instruction suivante
            // lui-même (x86_ops_stack.h:573-598 — l'ombre d'interruption du 286,
            // qui laisse un MOV SP,... suivre un chargement de SS sans qu'une
            // interruption ne survienne sur une pile a moitié chargée). L'octet
            // suivant est donc exécuté, pas ignoré.
            // DEUX FAMILLES FONT EXECUTER L'OCTET D'APRES L'ADRESSE EFFECTIVE,
            // et il faut donc le tirer dans le jeu teste comme on le fait pour
            // les prefixes — mais a un OFFSET qui depend du ModRM.
            //
            //   - MOV SS, r/m quand le champ `reg` vaut 2 : l'ombre
            //     d'interruption, meme mecanisme que POP SS (A4).
            //   - LES HUIT ESCAPE D8-DF. op_nofpu_a16 (x87_ops.h:278-286) ne
            //     porte AUCUN CLOCK_CYCLES : sans coprocesseur, une instruction
            //     de calcul flottant decode son adresse effective et rend la
            //     main a COUT NUL. C'est la classe trouvee a A9 — un cycdiff qui
            //     reste a zero fait repartir la boucle interne d'exec386, et le
            //     « pas » avale l'instruction suivante.
            //     Mesure : opcode 0xDF, octets DF 45 B5 0F D2 — le 0F au rang 3
            //     est l'octet d'apres, et il aiguille vers la table a deux
            //     octets, vide.
            //
            // Le remplissage RAM a 0x90 ne protege qu'AU-DELA du tampon ; a
            // l'interieur les octets sont aleatoires.
            if ((op == 0x8E && ((code[1] >> 3) & 7) == 2) || (op >= 0xD8 && op <= 0xDF))
            {
                // LE TIRAGE DOIT EXCLURE LES INSTRUCTIONS A COUT NUL ELLES-MEMES,
                // sinon la fuite se rallonge d'un cran : `DB 0D DB E7 0F` — deux
                // ESCAPE d'affilee, et le 0F au TROISIEME rang. Mesure a
                // l'iteration 9610. On borne donc la chaine a un seul cran.
                var suite = op;
                for (var guard = 0; guard < 16 &&
                                    (EnchaineSurLaSuivante(suite) || suite is 0x66 or 0x67
                                     || CoutNul(suite)); guard++)
                    suite = opcodes[rng.Next() % (uint)opcodes.Length];
                if (EnchaineSurLaSuivante(suite) || suite is 0x66 or 0x67 || CoutNul(suite))
                    suite = 0xB8;
                code[1 + TailleModRM16(code[1])] = suite;
            }

            if (EnchaineSurLaSuivante(op))
            {
                // 0x66 ET 0x67 SONT EXCLUS EN PLUS DES ENCHAINEURS, et la raison
                // n'est pas la commodité. ops_286[0x66] vaut ILLEGAL — un 66 nu
                // est un opcode invalide sur un 286. Mais ops_REPE et ops_REPNE
                // sont PARTAGEES entre generations dans PCem, et y mettent
                // op_66_REPE / op_67_REPE. Un `F2 66 AD` pose donc op32 = 0x100
                // et atteint ops_REPNE[0x1AD] = opREP_LODSL_a16, la forme 32
                // BITS. Mesure :
                //   opcode 0xF2, octets F2 66 AD — cycles : oracle 112, C# 22
                // Ces formes `_l` et `_a32` ne sont pas transcrites : elles sont
                // hors du jalon 286 et arriveront avec le 386. Le chemin qui y
                // mene n'existe sur AUCUN 286 reel, ou 66 et 67 sont invalides ;
                // c'est un artefact du partage de table, pas un comportement.
                var inner = op;
                for (var guard = 0; guard < 16 &&
                                    (EnchaineSurLaSuivante(inner) || inner is 0x66 or 0x67); guard++)
                    inner = opcodes[rng.Next() % (uint)opcodes.Length];
                if (EnchaineSurLaSuivante(inner) || inner is 0x66 or 0x67)
                    inner = 0xB8;                    // MOV AX,imm16 : repli sûr
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
            // Applicable au seul opcode porteur d'un ModRM : voir PorteModRM.
            var steered = (rng.Next() & 7) == 0 && PorteModRM(op);
            if (steered)
            {
                regs[(int)R.DS] = regs[(int)R.CS];
                code[1] = 0x06;                                  // mod=00, rm=110 : direct
                var target = (ushort)(regs[(int)R.IP] + 4);
                code[2] = (byte)target;
                code[3] = (byte)(target >> 8);
            }

            // UN DECALAGE DE COMPTE NUL NE CONSOMME AUCUN CYCLE (A9), et la
            // boucle interne de exec386 REPART alors : le pas execute
            // l'instruction suivante, prise dans les octets aleatoires du
            // tampon. Le remplissage RAM vaut 0x90 pour qu'une fuite tombe sur
            // un NOP, mais il ne protege qu'AU-DELA du tampon — a l'interieur,
            // l'octet suivant est du hasard, et une fois sur deux un opcode non
            // transcrit. Mesure : plantage au bout de ~40 000 iterations.
            //
            // On garantit donc un compte NON NUL. Le cas nul n'est pas perdu :
            // il est couvert par les suites (y) de core286-check, qui placent
            // une instruction CONNUE derriere. C'est le partage habituel — le
            // fuzzeur balaie l'espace, les cas diriges tiennent ce qu'il ne
            // peut pas construire.
            if (op is 0xD2 or 0xD3)
                regs[(int)R.CX] |= 1;                // CL & 31 != 0
            else if (op is 0xC0 or 0xC1)
                code[1 + TailleModRM16(code[1])] |= 1;

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
            int cycS;
            try
            {
                cycS = core == Oracle.Core286 ? _386.Step286() : _808x.Step();
            }
            catch (Exception e)
            {
                // UN ARRET FATAL DU CŒUR EST UN RESULTAT, pas un accident de
                // l'outil : il doit nommer l'iteration et les octets comme le
                // ferait une divergence. Sans ce bloc le fuzzeur mourait sur un
                // `opcode 0F xx non transcrit` sans dire par quel chemin il y
                // etait arrive — et le chemin est justement ce qu'on cherche.
                Console.WriteLine($"\nARRET FATAL iteration {it}");
                Console.WriteLine($"  opcode 0x{op:X2}, octets {string.Join(" ", code.Select(x => x.ToString("X2")))}");
                Console.WriteLine($"  CS:IP {regs[(int)R.CS]:X4}:{regs[(int)R.IP]:X4}  " +
                                  $"AX {regs[(int)R.AX]:X4} BX {regs[(int)R.BX]:X4} " +
                                  $"CX {regs[(int)R.CX]:X4} DX {regs[(int)R.DX]:X4}");
                Console.WriteLine($"  {e.Message.Trim()}");
                Console.WriteLine($"\n  Rejouer : --mode single --seed {seed} --iter {it + 1}");
                return 1;
            }

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

    /// <summary>L'instruction consomme-t-elle ZERO cycle ?
    ///
    /// Une instruction a cout nul laisse cycdiff a zero, donc la boucle interne
    /// d'exec386 REPART et le « pas » avale la suivante (A9). Deux familles :
    ///   - les huit ESCAPE D8-DF sans coprocesseur, dont op_nofpu_a16 ne porte
    ///     aucun CLOCK_CYCLES ;
    ///   - les decalages de compte nul, deja evites en garantissant un compte
    ///     non nul plus haut.
    /// Seule la premiere a besoin d'etre exclue ici.</summary>
    private static bool CoutNul(byte op) => op >= 0xD8 && op <= 0xDF;

    /// <summary>Nombre d'octets qu'occupent le ModRM et son déplacement, en
    /// adressage 16 bits. Sert à trouver où commence l'immédiat.
    /// pcem: la table de 386_dynarec.c:85-130, réduite à sa longueur.</summary>
    private static int TailleModRM16(byte modrm)
    {
        var mod = (modrm >> 6) & 3;
        var rm = modrm & 7;
        if (mod == 3) return 1;
        if (mod == 0) return rm == 6 ? 3 : 1;
        if (mod == 1) return 2;
        return 3;
    }

    /// <summary>L'octet SUIVANT sera-t-il exécuté comme une instruction ?
    ///
    /// Vrai des quatre préfixes de segment, qui sautent à opcodestart ; de
    /// POP SS, qui va chercher et aiguille l'opcode suivant lui-même (A4) ; et
    /// depuis A11 de LOCK (F0, F1) et des deux préfixes de répétition (F2, F3),
    /// qui font exactement la même chose.
    ///
    /// LES OUBLIER NE DONNE PAS UN FAUX VERT MAIS UN PLANTAGE : l'octet suivant
    /// est du hasard, et une fois sur seize c'est 0x0F, qui aiguille vers la
    /// table à DEUX octets — vide, et bruyante par conception. Mesuré :
    ///   `iXtal26 FATAL: opcode 0F 8B non transcrit` au bout de quelques
    ///   dizaines de milliers d'itérations, sur un F2 suivi d'un 0F.
    ///
    /// La boucle de garde qui suit redessine `inner` tant qu'il enchaîne à son
    /// tour, donc il n'y a jamais de chaîne à deux niveaux à couvrir.</summary>
    private static bool EnchaineSurLaSuivante(byte op) =>
        IsSegPrefix(op) || op == 0x17 || op is 0xF0 or 0xF1 or 0xF2 or 0xF3;

    /// <summary>L'opcode porte-t-il un octet ModRM ?
    ///
    /// LE TEST PRÉCÉDENT ÉTAIT `(op &amp; 7) &lt; 4`, vrai pour les formes /r de la
    /// bande ALU 0x00-0x3F et FAUX partout ailleurs — il rendait vrai pour A0-A3
    /// (moffs), B0-B3 et B8-BB (MOV reg,imm) et A8 (TEST AL,imm), qui n'ont pas
    /// de ModRM du tout. Ces itérations-là étaient comptées « auto-référentielles »
    /// alors qu'elles se contentaient d'écraser l'immédiat par 06 xx xx : pas un
    /// faux vert, mais un chiffre qui surestimait la couverture d'un bon quart.
    ///
    /// À TENIR À JOUR avec la table : un opcode à ModRM absent d'ici ne sera
    /// jamais dirigé, et la garde de readmemb restera non testée pour lui.</summary>
    private static bool PorteModRM(byte op) =>
        (op < 0x40 && (op & 7) < 4) ||      // bande ALU : les formes /r
        (op >= 0x80 && op <= 0x85) ||        // groupe immédiat 80-83, TEST 84-85
        (op >= 0x88 && op <= 0x8B) ||        // MOV /r
        op == 0xC6 || op == 0xC7;            // MOV ea, imm

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
     * descripteur système, + les 6 registres de contrôle, + les 4 drapeaux paresseux,
     * + les 7 globaux du mode protégé de C7a — abrt_error, intgatesize, cgate16,
     * cgate32, optype, oldcpl, cur_status.
     * Compté en formes de champ, pas en entrées de tableau : la boucle en couvre 6, la
     * suivante 4. Tenu À LA MAIN par doctrine : une réflexion sur HState rendrait ce
     * nombre juste sans garantir qu'un Chk() existe pour chaque champ, ce qui est
     * précisément ce qu'on veut savoir. */
    private const int FieldCount = 67;

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
            // LES SEPT GLOBAUX DU MODE PROTEGE (C7a), places ICI — apres les compteurs
            // mais avant `ins` — pour une raison de LECTURE : quand l'un d'eux diverge,
            // c'est la CAUSE qu'on veut lire, pas la consequence. Meme arbitrage que les
            // quatre drapeaux paresseux devant `flags` a A2.1.
            //
            // cgate16 ET cgate32 tous les deux, alors qu'ils sont redondants par
            // construction (cgate16 = !cgate32) : c'est justement ce qui attrape une
            // transcription qui n'en poserait qu'un.
            ?? Chk("abrt_error", a.abrt_error, b.abrt_error)
            ?? Chk("intgatesize", a.intgatesize, b.intgatesize)
            ?? Chk("cgate16", a.cgate16, b.cgate16)
            ?? Chk("cgate32", a.cgate32, b.cgate32)
            ?? Chk("optype", a.optype, b.optype)
            ?? Chk("oldcpl", a.oldcpl, b.oldcpl)
            ?? Chk("cur_status", a.cur_status, b.cur_status)
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
