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
    private static bool ConfigChecked;

    /// <summary>M16 — l'empreinte CPU des deux côtés, confrontée UNE fois, après le premier
    /// reset : h_reset et Reset286/Reset font tourner cpu_set() depuis l'étape 6, et un
    /// `--cpu N` qui n'atteindrait qu'un côté fuzzerait deux machines différentes — en
    /// vert tant que les instructions tirées ne touchent pas la mémoire.</summary>
    private static bool CheckCpuConfig()
    {
        ConfigChecked = true;
        var fpOracle = CpuFingerprint.Oracle_();
        var fpCsharp = CpuFingerprint.Csharp();
        if (CpuFingerprint.Compare(fpOracle, fpCsharp) != 0)
        {
            Console.WriteLine("ROUGE : les deux côtés ne fuzzent pas le même processeur.");
            return false;
        }
        Console.WriteLine($"  empreinte CPU identique : {CpuFingerprint.Summary(fpCsharp)}");
        return true;
    }

    /// <param name="second0F">G2, D4 — non nul : chaque itération tire `0F xx`, xx pris
    /// dans cette liste, précédé d'un préfixe 66, 67, des deux ou d'aucun. Voir Poser0F.</param>
    public static int RunSingle(byte[] opcodes, int iterations, ulong seed, bool verbose, int core,
                                byte[]? second0F = null)
    {
        Oracle.CheckAbi();
        Console.WriteLine($"Diff différentiel (mode simple) — opcodes " +
                          $"{string.Join(",", opcodes.Select(o => $"0x{o:X2}"))}, " +
                          $"{iterations} itérations, graine {seed}");

        var rng = new Lcg(seed);
        var fpuRng = new Lcg(seed ^ FpuSalt);
        var a = HState.Create();
        var b = HState.Create();
        var regs = new ushort[(int)R.COUNT];
        // Douze octets en mode --0f : deux préfixes, 0F xx, ModRM, SIB et un disp32
        // en font neuf. Huit sinon, pour que les graines des autres modes rejouent.
        var code = new byte[second0F is null ? 8 : 12];
        var perOpcode = new Dictionary<byte, int>();
        var per0F = new Dictionary<byte, int>();
        var steeredCount = 0;

        for (var it = 0; it < iterations; it++)
        {
            var op = opcodes[rng.Next() % (uint)opcodes.Length];
            perOpcode[op] = perOpcode.GetValueOrDefault(op) + 1;

            Oracle.h_set_core(core);
            Oracle.h_reset();
            Oracle.h_fill_ram(0x90);
            if (core == Oracle.Core486)
                _386.Reset486();
            else if (core == Oracle.Core386)
                _386.Reset386();
            else if (core == Oracle.Core286)
                _386.Reset286();
            else if (core == Oracle.Core8086)
                _808x.Reset8086();
            else
                _808x.Reset();
            if (!ConfigChecked && !CheckCpuConfig())
                return 1;
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
            // G4.2 — `--x87 mem` : pour D9, DB, DD et DF, un ModRM MÉMOIRE dont le `reg` désigne
            // une rangée transcrite (chargements, stockages, ILLEGAL — TableFpu, x87_ops.cs).
            // Dérivé des bits DÉJÀ tirés, sans consommer le générateur : mod 3 devient mod 0-2,
            // `reg` est ramené dans la liste de l'opcode.
            if (X87Mem && X87MemRegs(op) is { } rangees)
            {
                var mod = code[1] >> 6;
                if (mod == 3)
                    mod = code[1] & 1;
                var reg = rangees[((code[1] >> 3) & 7) % rangees.Length];
                code[1] = (byte)((mod << 6) | (reg << 3) | (code[1] & 7));
            }

            if ((op == 0x8E && ((code[1] >> 3) & 7) == 2) || (op >= 0xD8 && op <= 0xDF))
            {
                // LE TIRAGE DOIT EXCLURE LES INSTRUCTIONS A COUT NUL ELLES-MEMES,
                // sinon la fuite se rallonge d'un cran : `DB 0D DB E7 0F` — deux
                // ESCAPE d'affilee, et le 0F au TROISIEME rang. Mesure a
                // l'iteration 9610. On borne donc la chaine a un seul cran.
                //
                // 0x8E EST EXCLU POUR LA MEME RAISON (G2, D0.4) : MOV SS reexecute la
                // suivante quand son `reg` vaut 2, et son ModRM n'est tire qu'apres.
                // Mesure, coeur 386 : `8E 94 2B 8F 8E D5 64` — le second MOV SS
                // execute le 64 du troisieme rang, prefixe FS pas encore transcrit.
                var suite = op;
                for (var guard = 0; guard < 16 &&
                                    (EnchaineSurLaSuivante(suite) || suite is 0x66 or 0x67 or 0x8E
                                     || CoutNul(suite)); guard++)
                    suite = opcodes[rng.Next() % (uint)opcodes.Length];
                if (EnchaineSurLaSuivante(suite) || suite is 0x66 or 0x67 or 0x8E || CoutNul(suite))
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

            // G6.1 — LOADALL386 (0F 07) SUR LE CŒUR 486, tiré au hasard : PCem l'exécute — le 486
            // partage ops_386 —, il lit 0xCC octets non tenus, charge un CR0 avec PG et un CR3
            // quelconque, et la traduction de page déréférence _mem_exec hors RAM : l'oracle
            // tombe (segfault, mesuré : graine 1, itération 2 588, `0F 07 6B E6…`). Le vrai 486
            // n'a pas de LOADALL. Remplacé par un voisin fixe, sans tirage de plus ; le cœur 386
            // garde son tirage (ses séries de référence n'y passent pas). PARTOUT dans le tampon,
            // pas seulement en tête : un ESC à coût nul fait exécuter l'octet d'après (mesuré :
            // `DB E7 0F 07`, graine 1, i486DX2/66, itération 4 060).
            if (core == Oracle.Core486 && second0F is null)
                for (var k = 0; k + 1 < code.Length; k++)
                    if (code[k] == 0x0F && code[k + 1] == 0x07)
                        code[k + 1] = 0x06; // CLTS

            // G4.4 — `--x87 g44` : tout D9/DB/DD/DF est transcrit sauf les huit transcendantes de
            // D9 en mode registre, encore des souches (G4.5). Leur ModRM est remplacé par un voisin
            // transcrit, par une table fixe : aucun tirage de plus. APRÈS la réécriture du préfixe
            // enchaîné, qui place l'ESC en position 1.
            if (X87G44)
            {
                X87SansTranscendante(code, 0);
                if (code[0] is 0x66 or 0x67)
                    X87SansTranscendante(code, 1);
            }

            // G4.2 — `--x87 mem` derrière un préfixe de taille : `66 D9 /r`, `67 DD /r`… — l'ESC
            // est en 1, son ModRM en 2 ; même règle, pour atteindre les formes a32 (67).
            if (X87Mem && code[0] is 0x66 or 0x67 && X87MemRegs(code[1]) is { } rangees2)
            {
                var mod = code[2] >> 6;
                if (mod == 3)
                    mod = code[2] & 1;
                var reg = rangees2[((code[2] >> 3) & 7) % rangees2.Length];
                code[2] = (byte)((mod << 6) | (reg << 3) | (code[2] & 7));
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

            // AAM 0 FAIT MOURIR L'ORACLE 8088 (PB-46) : `808x.c` divise par l'immédiat sans le
            // tester, SIGFPE, et l'oracle vit dans le processus du diff. Mesuré en G4.0 : le
            // tir des 256 opcodes, graine 1, tombait avant la fin des 100 000 itérations,
            // arbre d'avant G4.0 compris. Le chemin 286/386 teste `!base` et lève #DE : il
            // n'est pas touché. On n'interdit que la VALEUR fautive, pas l'opcode — sur le
            // modèle du compte non nul ci-dessus : l'immédiat nul devient 1, les 255 autres
            // restent tels que tirés. D4 s'exécute en tête, ou derrière un préfixe enchaîné.
            // Seules les recettes --seed 8088 qui tiraient `D4 00` changent (VERIFICATION.md
            // § G4.0) ; aucun octet n'est tiré en plus, la suite du générateur ne bouge pas.
            if (!Oracle.Exec386(core))
            {
                if (code[0] == 0xD4 && code[1] == 0)
                    code[1] = 1;
                else if (EnchaineSurLaSuivante(code[0]) && code[1] == 0xD4 && code[2] == 0)
                    code[2] = 1;
            }

            // G2, D4 — le second octet est tiré ici, APRÈS les réécritures du chemin à un
            // octet : aucune ne touche un 0x0F (ni préfixe, ni ModRM porté), et le tampon
            // est de toute façon réécrit en entier.
            byte op2 = 0;
            var rmSansPG = -1;
            if (second0F is not null)
            {
                op2 = Poser0F(code, second0F, ref rng, out var modrm);
                // MOV CR0,r AVEC PG TIRÉ FAIT TOMBER L'ORACLE, mesuré (segfault, graine 1).
                // TF est tiré lui aussi : l'INT 1 qui suit le pas empile sous pagination,
                // et mmutranslatereal (mem.c:220-277) lit le répertoire en cr3 & ~0xFFF —
                // un cr3 tiré, jusqu'à 4 Go, dans une RAM de 16 Mo, sans borne. Le bit 31
                // de la source est donc effacé, comme dans SeedSys386 et le bloc LOADALL.
                if (op2 == 0x22)
                    rmSansPG = modrm & 7;
                per0F[op2] = per0F.GetValueOrDefault(op2) + 1;
                // LOADALL386 lit 0xCC octets en ES:EDI. Hors d'un tampon tenu, il lirait
                // le 0x90 de remplissage, ou le code lui-même ; ES est donc tenu entre
                // 0x3000 et 0x6FFF, loin du code en 2000:0xxx, et EDI sans moitié haute.
                if (op2 == 0x07)
                    regs[(int)R.ES] = (ushort)(0x3000 + (rng.Next() % 0x4000));
            }

            var linear = (uint)(regs[(int)R.CS] << 4) + regs[(int)R.IP];
            Oracle.h_load(linear, code, (uint)code.Length);
            for (var i = 0; i < code.Length; i++)
                mem.ram[(linear + i) & mem.rammask] = code[i];

            if (steered)
                steeredCount++;

            Oracle.h_setregs(regs);
            _808x.SetRegs(regs);
            if (Oracle.Is386Class(core))
                Seed386(ref rng, ediHautNul: op2 == 0x07 && second0F is not null, sansPG: rmSansPG);
            if (second0F is not null)
            {
                SeedSys386(ref rng);
                if (op2 == 0x07)
                    PoserBlocLoadall386(ref rng, (uint)(regs[(int)R.ES] << 4) + regs[(int)R.DI]);
            }

            if (FpuState)
                SeedFpu(ref fpuRng);

            Oracle.h_wlog_reset();
            mem.wlog_reset();

            var cycC = Oracle.h_step();
            int cycS;
            try
            {
                cycS = Oracle.Exec386(core) ? _386.Step286() : _808x.Step();
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
                // Une exception .NET (et non pc.fatal) : où, sans quoi le message ne dit rien.
                if (e is not InvalidOperationException)
                    Console.WriteLine($"  {e.StackTrace?.Split('\n').FirstOrDefault()?.Trim()}");
                Console.WriteLine($"\n  Rejouer : --mode single --seed {seed} --iter {it + 1}");
                return 1;
            }

            Oracle.h_getstate(out a);
            _808x.GetState(ref b);

            var diff = Compare(a, b, cycC, cycS) ?? CmpWrites() ?? (X87Mem || X87G44 || X87All ? CmpEa(a, core) : null);
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
        foreach (var (op2, n) in per0F.OrderBy(kv => kv.Key))
            Console.WriteLine($"    0F {op2:X2} : {n} tirages");
        Console.WriteLine($"    dont {steeredCount} cas auto-référentiels (EA == cs+pc)");
        return 0;
    }

    /// <summary>G2, D4 — l'instruction `[66|67|66 67] 0F xx` et des octets tirés.
    ///
    /// LE PRÉFIXE CHOISIT LE QUADRANT de ops_386_0f : 66 le 1 (o32/a16), 67 le 2
    /// (o16/a32), les deux le 3. Sans lui, seules les formes w_a16 seraient vues.
    /// Rend le second octet.</summary>
    private static byte Poser0F(byte[] code, byte[] seconds, ref Lcg rng, out byte modrm)
    {
        var n = 0;
        switch (rng.Next() & 3)
        {
            case 1: code[n++] = 0x66; break;
            case 2: code[n++] = 0x67; break;
            case 3: code[n++] = 0x66; code[n++] = 0x67; break;
        }
        code[n++] = 0x0F;
        var op2 = seconds[rng.Next() % (uint)seconds.Length];
        code[n++] = op2;
        for (var i = n; i < code.Length; i++)
            code[i] = (byte)rng.Next();
        modrm = code[n];
        return op2;
    }

    /// <summary>G2, D4 — CR0, CR3, DR6 et DR7 tirés, des deux côtés, par h_setsys386.
    ///
    /// CR0 GARDE PE ET PG NULS. PE à 1 ferait du pas un pas en mode protégé, avec des
    /// descripteurs que personne n'a posés ; PG à 1 ferait traduire chaque accès par la
    /// pagination de PCem, que le C# omet encore (D6). Les trente autres bits sont
    /// tirés : MOV r,CR0, SMSW et LMSW les lisent.
    ///
    /// CR2 ET DR0 À DR5 RESTENT À ZÉRO : h_setsys386 ne les pose pas, et les remettre
    /// au hasard coûterait un cran d'ABI. Leur LECTURE ne voit donc que zéro ; leur
    /// écriture, elle, est comparée comme tout le vecteur.</summary>
    private static void SeedSys386(ref Lcg rng)
    {
        var cr0 = rng.Next() & 0x7FFFFFFE;
        var cr3 = rng.Next();
        var dr6 = rng.Next();
        var dr7 = rng.Next();
        Oracle.h_setsys386(cr0, cr3, dr6, dr7);
        iXtal26.Cpu.x86.cr0 = cr0;
        iXtal26.Cpu.x86.cr3 = cr3;
        iXtal26.Cpu.x86.dr[6] = dr6;
        iXtal26.Cpu.x86.dr[7] = dr7;
    }

    /// <summary>G2, D4 — le bloc de 0xCC octets que LOADALL386 lit en ES:EDI, tiré et
    /// écrit des deux côtés.
    ///
    /// LE BIT PG DU MOT CR0 EST EFFACÉ (octet 3, bit 7). LOADALL386 écrit CR0 EN
    /// PREMIER : avec PG posé, toutes les lectures suivantes du bloc passeraient par
    /// la pagination de PCem, omise côté C#. Le reste est du hasard pur — sélecteurs,
    /// bases, limites, drapeaux, EIP — et le vecteur d'état compare chaque champ.</summary>
    private static void PoserBlocLoadall386(ref Lcg rng, uint linear)
    {
        var bloc = new byte[0xCC];
        for (var i = 0; i < bloc.Length; i++)
            bloc[i] = (byte)rng.Next();
        bloc[3] &= 0x7F;
        Oracle.h_load(linear, bloc, (uint)bloc.Length);
        for (var i = 0; i < bloc.Length; i++)
            mem.ram[(linear + i) & mem.rammask] = bloc[i];
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
    // 64 à 67 (G2) : préfixes FS, GS, taille d'opérande et d'adresse sur un 386 ; ILLEGAL sur un 286, où les
    // tenir pour enchaîneurs ne fait que choisir l'octet suivant.
    private static bool EnchaineSurLaSuivante(byte op) =>
        IsSegPrefix(op) || op is 0x64 or 0x65 or 0x66 or 0x67 || op == 0x17 || op is 0xF0 or 0xF1 or 0xF2 or 0xF3;

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
    internal static string? CmpWrites()
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
        var fpuRng = new Lcg(seed ^ FpuSalt);
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
            //
            // POP SS (0x17) TOMBE SOUS LA MÊME RÈGLE (PB-49) : il exécute la suivante en
            // APPELANT son handler, et un flux uniforme de 0x17 est une récursion sans fin —
            // l'oracle meurt de débordement de pile au premier pas, 286 et 386. L'alternance
            // la borne à un cran. 8E est exclu de l'opcode intercalé derrière 0x17 : `8E 17`
            // est MOV SS,[BX], qui enchaîne lui aussi, et `17 8E 17 8E…` referait la chaîne.
            // Seules changent les rondes dont le remplissage est 0x17 : elles tombaient toutes.
            // Cœurs exec386 seulement : le 808x pose `noint` au lieu de récurser, et le flux
            // 8088 de 0x17 était vert — ses recettes ne bougent pas.
            var inner = fill;
            if (IsSegPrefix(fill) || (fill == 0x17 && Oracle.Exec386(core)))
            {
                // Sur exec386, 0x17 est aussi refusé derrière un préfixe : `26 17 26 17…` fait
                // sauter le préfixe sur POP SS, qui rappelle le préfixe — la même chaîne. Et
                // derrière un remplissage 0x17, TOUT octet qui enchaîne sur la suivante est
                // refusé, pas seulement les quatre préfixes de segment : `17 65 17 65…` (POP SS,
                // préfixe GS) tombait à la ronde 1139 de la graine 1 — le préfixe saute sur le
                // POP SS suivant, qui le rappelle. EnchaineSurLaSuivante couvre 17, les six
                // préfixes de segment, 66, 67 et F0-F3 ; 8E en plus (`8E 17` = MOV SS,[BX]).
                var x386 = Oracle.Exec386(core);
                bool Refus(byte b) => IsSegPrefix(b)
                                      || (x386 && (b == 0x17
                                                   || (fill == 0x17 && (EnchaineSurLaSuivante(b) || b == 0x8E))));
                for (var guard = 0; guard < 16 && Refus(inner); guard++)
                    inner = opcodes[rng.Next() % (uint)opcodes.Length];
                if (Refus(inner))
                    inner = 0x90;                    // NOP : repli sûr
            }

            Oracle.h_set_core(core);
            Oracle.h_reset();
            if (core == Oracle.Core486)
                _386.Reset486();
            else if (core == Oracle.Core386)
                _386.Reset386();
            else if (core == Oracle.Core286)
                _386.Reset286();
            else if (core == Oracle.Core8086)
                _808x.Reset8086();
            else
                _808x.Reset();
            if (!ConfigChecked && !CheckCpuConfig())
                return 1;
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
            if (Oracle.Is386Class(core))
                Seed386(ref rng);
            if (FpuState)
                SeedFpu(ref fpuRng);

            for (var n = 0; n < instrPerRound; n++)
            {
                var cycC = Oracle.h_step();
                var cycS = Oracle.Exec386(core) ? _386.Step286() : _808x.Step();

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
            // L'accélération du 4 octobre : la comparaison octet par octet de CmpRam, au lieu de deux
            // FNV de 16 Mo ; le message garde les deux FNV.
            var ramDiff = CmpRam();
            if (ramDiff is not null)
            {
                Console.WriteLine($"\nDIVERGENCE MÉMOIRE en fin de ronde {round}");
                Console.WriteLine($"  {ramDiff}");
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
     * cgate32, optype, oldcpl, cur_status, + cr4 et dr[] de G2 D0.1, + les quatorze du x87
     * de G4.0 : ST, MM, MM_w4, tag (tableaux), TOP, npxs, npxc, x87_pc_off, x87_op_off,
     * x87_pc_seg, x87_op_seg, ismmx, fpu_type, hasfpu.
     * Compté en formes de champ, pas en entrées de tableau : la boucle en couvre 6, la
     * suivante 4. Tenu À LA MAIN par doctrine : une réflexion sur HState rendrait ce
     * nombre juste sans garantir qu'un Chk() existe pour chaque champ, ce qui est
     * précisément ce qu'on veut savoir. */
    private const int FieldCount = 83;

    /// <summary>G2, D0.4 — ce qu'un 386 a de plus qu'un 286, tiré au hasard et posé des
    /// DEUX côtés après SetRegs : les moitiés hautes des huit registres généraux, FS et
    /// GS. Le mot haut d'EFLAGS reste à zéro — VM y est, et le fuzzeur part en mode
    /// réel ; RF n'a de lecteur que le piège de débogage. Sans ces moitiés hautes, un
    /// handler 16 bits qui écrase EAX tout entier passerait vert : zéro des deux côtés.</summary>
    private static void Seed386(ref Lcg rng, bool ediHautNul = false, int sansPG = -1)
    {
        var hi = new ushort[8];
        for (var i = 0; i < hi.Length; i++)
            hi[i] = rng.Next16();
        if (ediHautNul)
            hi[7] = 0;
        if (sansPG >= 0)
            hi[sansPG] &= 0x7FFF;
        var fs = rng.Next16();
        var gs = rng.Next16();
        Oracle.h_setregs386(hi, 0, fs, gs);
        _808x.SetRegs386(hi, 0, fs, gs);
    }

    /// <summary>G4.0 — `--fpu-state` : un état x87 tiré et posé des DEUX côtés à chaque
    /// itération. Son générateur est À PART (graine ^ FpuSalt) : la suite principale ne
    /// bouge pas, donc une recette `--seed` d'avant G4.0 rejoue les mêmes instructions,
    /// avec ou sans l'option.</summary>
    internal static bool FpuState;
    private const ulong FpuSalt = 0x8087_0287_0387_0487UL;

    // Les valeurs où un x87 se trompe : zéros signés, infinis, NaN silencieux et
    // signalants, charges de NaN, dénormaux, bornes des conversions entières (2^15, 2^31,
    // 2^63, 2^64), milieux d'arrondi (0,5 ; 1,5 ; 2,5), et la plus grande valeur finie.
    private static readonly ulong[] FpuSpecial =
    [
        0x0000000000000000, 0x8000000000000000, 0x7FF0000000000000, 0xFFF0000000000000,
        0x7FF8000000000000, 0xFFF8000000000000, 0x7FF0000000000001, 0x7FF4000000000000,
        0x7FFFFFFFFFFFFFFF, 0x0000000000000001, 0x800FFFFFFFFFFFFF, 0x0010000000000000,
        0x7FEFFFFFFFFFFFFF, 0x3FF0000000000000, 0xBFF0000000000000, 0x3FE0000000000000,
        0x3FF8000000000000, 0x4004000000000000, 0x40E0000000000000, 0xC0E0000000000000,
        0x41E0000000000000, 0xC1E0000000000000, 0x43E0000000000000, 0xC3E0000000000000,
        0x43F0000000000000, 0x400921FB54442D18,
    ];

    private static readonly ulong[] SeedSt = new ulong[8], SeedMm = new ulong[8];
    private static readonly ushort[] SeedMmW4 = new ushort[8];
    private static readonly byte[] SeedTag = new byte[8];

    private static ulong Next64(ref Lcg r) => ((ulong)r.Next() << 32) | r.Next();

    /// <summary>La pile vide, pleine ou partielle ; ST spéciaux, entiers ou bruts ; tags
    /// VALID, EMPTY ou VALID|UINT64 (FILD 64 bits, x87.h:30) ; npxc avec ses deux bits
    /// d'arrondi, sa précision et ses masques, tous tirés.</summary>
    private static void SeedFpu(ref Lcg r)
    {
        var top = (int)(r.Next() & 7);
        var depth = (r.Next() % 3) switch { 0 => 0, 1 => 8, _ => 1 + (int)(r.Next() % 7) };
        Array.Clear(SeedTag);
        for (var k = 0; k < depth; k++)
            SeedTag[(top + k) & 7] = (r.Next() & 7) == 0 ? (byte)0x81 : (byte)0x01;
        for (var i = 0; i < 8; i++)
        {
            SeedSt[i] = (r.Next() % 3) switch
            {
                0 => FpuSpecial[r.Next() % (uint)FpuSpecial.Length],
                1 => (ulong)BitConverter.DoubleToInt64Bits((int)r.Next() >> (int)(r.Next() & 31)),
                _ => Next64(ref r),
            };
            SeedMm[i] = Next64(ref r);
            SeedMmW4[i] = (r.Next() & 3) == 0 ? (ushort)0x5555 : (ushort)r.Next();
        }
        var npxs = (ushort)r.Next();
        var npxc = (ushort)r.Next();
        Oracle.h_setfpu(SeedSt, SeedMm, SeedMmW4, SeedTag, top, npxs, npxc);
        _808x.SetFpu(SeedSt, SeedMm, SeedMmW4, SeedTag, top, npxs, npxc);
    }

    /// <summary>G4.2 — `--x87 mem` : les rangées `reg` de chaque table d'échappement que G4.2
    /// transcrit, ou null. D9 : FLD, ILLEGAL, FST, FSTP m32 ; DB : FILD, FIST, FISTP m32,
    /// FLD, FSTP m80, trois ILLEGAL ; DD : FLD, FST, FSTP m64, deux ILLEGAL ; DF : FILD, FIST,
    /// FISTP m16, FILD, FISTP m64, FBSTP, deux ILLEGAL — dont /4, FBLD (PB-52).</summary>
    internal static bool X87Mem;

    /// <summary>G4.4 — `--x87 g44`, voir RunSingle.</summary>
    internal static bool X87G44;

    /// <summary>G4.5 — `--x87 all` : tout D8-DF, sans filtre ; l'EA comparée sur 112 octets.</summary>
    internal static bool X87All;

    // D9 F0-F3, F9, FB, FE, FF : F2XM1, FYL2X, FPTAN, FPATAN, FYL2XP1, FSINCOS, FSIN, FCOS.
    private static readonly Dictionary<byte, byte> X87Voisin = new()
    {
        [0xF0] = 0xF4, [0xF1] = 0xF5, [0xF2] = 0xF6, [0xF3] = 0xF7,
        [0xF9] = 0xFA, [0xFB] = 0xFC, [0xFE] = 0xFD, [0xFF] = 0xF8,
    };

    internal static void X87SansTranscendante(byte[] code, int k)
    {
        if (code[k] == 0xD9 && X87Voisin.TryGetValue(code[k + 1], out var v))
            code[k + 1] = v;
    }

    private static int[]? X87MemRegs(byte op) => op switch
    {
        0xD9 => [0, 1, 2, 3],
        0xDB => [0, 1, 2, 3, 4, 5, 6, 7],
        0xDD => [0, 1, 2, 3, 5],
        0xDF => [0, 1, 2, 3, 4, 5, 6, 7],
        _ => null,
    };

    /// <summary>G4.2 — les seize octets à l'adresse effective, des deux côtés. Le journal
    /// d'écritures ne voit pas writememql (FST, FSTP, FISTP m64) : ni l'oracle ni le C#
    /// n'enveloppent la forme 64 bits.</summary>
    private static string? CmpEa(in HState a, int core)
    {
        // G4.6 — le 808x ne pose pas cpu_state.ea_seg : fetchea (808x.c) calcule `easeg`, une base
        // linéaire, et ses écritures rapides (writelookup2) échappent au journal. Sans cette
        // branche, un FSTP m64 du 8087 n'était comparé NULLE PART — contre-épreuve de G4.6 :
        // writememq faussé, fuzzeur vert. La base est celle du côté C# ; `eaaddr` est déjà
        // comparé à l'oracle par Compare.
        uint lin;
        if (a.ea_seg_idx >= 0)
            lin = (a.seg_base[a.ea_seg_idx] + a.eaaddr) & mem.rammask;
        else if (!Oracle.Exec386(core))
            lin = (x86.easeg + (a.eaaddr & 0xffff)) & mem.rammask;
        else
            return null;
        // Au-delà de la RAM plate du fuzzeur (1 Mo sur le 286, où l'EA peut viser la HMA) : rien
        // à comparer, et h_read ne doit pas lire hors de son tableau.
        // 112 octets : FSAVE en écrit 94 (16 bits) ou 108 (32 bits), au-delà des 64 entrées du
        // journal d'écritures.
        const int n = 112;
        if (lin + n > (uint)mem.mem_size * 1024u)
            return null;
        var o = new byte[n];
        Oracle.h_read(lin, o, n);
        for (var k = 0; k < n; k++)
        {
            var c = mem.ram[lin + (uint)k];
            if (o[k] != c)
                return $"RAM à l'EA {lin + (uint)k:X6} : oracle 0x{o[k]:X2}, C# 0x{c:X2}";
        }
        return null;
    }

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

        for (var i = 0; i < 8; i++)
            if (a.dr[i] != b.dr[i])
                return $"dr{i} : oracle 0x{a.dr[i]:X8}, C# 0x{b.dr[i]:X8}";

        // G4.0 — le x87, ST en BITS BRUTS : deux NaN de charges différentes, ou 0 et -0,
        // sont des états différents, et une comparaison de doubles les confondrait.
        for (var i = 0; i < 8; i++)
        {
            if (a.fpu_st[i] != b.fpu_st[i])
                return $"ST[{i}] : oracle 0x{a.fpu_st[i]:X16}, C# 0x{b.fpu_st[i]:X16}";
            if (a.fpu_mm[i] != b.fpu_mm[i])
                return $"MM[{i}].q : oracle 0x{a.fpu_mm[i]:X16}, C# 0x{b.fpu_mm[i]:X16}";
            if (a.fpu_mm_w4[i] != b.fpu_mm_w4[i])
                return $"MM_w4[{i}] : oracle 0x{a.fpu_mm_w4[i]:X4}, C# 0x{b.fpu_mm_w4[i]:X4}";
            if (a.fpu_tag[i] != b.fpu_tag[i])
                return $"tag[{i}] : oracle 0x{a.fpu_tag[i]:X2}, C# 0x{b.fpu_tag[i]:X2}";
        }

        return Chk("cr0", a.cr0, b.cr0)
            ?? Chk("cr2", a.cr2, b.cr2)
            ?? Chk("cr3", a.cr3, b.cr3)
            ?? Chk("cr4", a.cr4, b.cr4)
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
            ?? Chk("TOP", a.fpu_top, b.fpu_top)
            ?? Chk("npxs", a.npxs, b.npxs)
            ?? Chk("npxc", a.npxc, b.npxc)
            ?? Chk("x87_pc_off", a.x87_pc_off, b.x87_pc_off)
            ?? Chk("x87_op_off", a.x87_op_off, b.x87_op_off)
            ?? Chk("x87_pc_seg", a.x87_pc_seg, b.x87_pc_seg)
            ?? Chk("x87_op_seg", a.x87_op_seg, b.x87_op_seg)
            ?? Chk("ismmx", a.ismmx, b.ismmx)
            ?? Chk("fpu_type", a.fpu_type, b.fpu_type)
            ?? Chk("hasfpu", a.hasfpu, b.hasfpu)
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

    /// <summary>L'accélération du 4 octobre — les deux RAM comparées OCTET PAR OCTET (h_ram_cmp, la
    /// RAM C# épinglée par le marshalling, sans copie), au lieu de deux FNV de 16 Mo : environ
    /// 12 ms par côté et par pas, 90 % de pm-fuzz. Exactement mem_size × 1 024 octets, après avoir
    /// vérifié que les deux mem_size sont égaux ; h_ram est relue à chaque appel. La détection est
    /// au moins celle du hachage : tout écart qu'il voyait, la comparaison le voit, et en plus ses
    /// collisions. Le message garde les deux FNV et nomme le premier octet différent.
    /// IXTAL26_FAUTE_RAM=DÉCALAGE (hexadécimal, ou « fin » pour le dernier octet), le contrôle
    /// négatif : un octet de la RAM C# inversé avant la première comparaison.</summary>
    internal static string? CmpRam()
    {
        var kb = Oracle.h_mem_size();
        if (kb != mem.mem_size)
            return $"RAM : mem_size oracle {kb} Ko, C# {mem.mem_size} Ko";
        var n = (uint)kb * 1024u;
        if (n > (uint)mem.ram.Length)
            return $"RAM : {n} octets à comparer, la RAM C# n'en a que {mem.ram.Length}";
        FauteRam(n);
        var off = Oracle.h_ram_cmp(mem.ram, n, out var octet);
        if (off == -1)
            return null;
        if (off == -2)
            return $"RAM : {n} octets à comparer, au-delà de la RAM de l'oracle";
        return $"RAM : oracle {Oracle.h_ram_hash():X16}, C# {_808x.RamHash():X16} — premier octet différent en " +
               $"{off:X6} (oracle {octet:X2}, C# {mem.ram[off]:X2})";
    }

    private static readonly string? FauteRamSpec = Environment.GetEnvironmentVariable("IXTAL26_FAUTE_RAM");
    private static bool fauteRamFaite;

    private static void FauteRam(uint n)
    {
        if (FauteRamSpec is null || fauteRamFaite)
            return;
        fauteRamFaite = true;
        var off = FauteRamSpec == "fin" ? n - 1 : Convert.ToUInt32(FauteRamSpec, 16);
        mem.ram[off] ^= 0xFF;
        Console.WriteLine($"  IXTAL26_FAUTE_RAM : octet {off:X6} de la RAM C# inversé (contrôle négatif)");
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
