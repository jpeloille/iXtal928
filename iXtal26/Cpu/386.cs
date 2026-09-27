// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/cpu/386.c  (exec386, lignes 153-295)
// SHA256: voir oracle.tsv ; vérifier avec tools/check-oracle.sh
// STATUS: partial — A2.2b : la BOUCLE et l'aiguillage. La table d'opcodes est
//         vide : chaque entrée échoue bruyamment en nommant son opcode.
//
// LA BOUCLE DU 286 ET DU 386, ET CE QUI LA DISTINGUE DE CELLE DU 8088.
//
// execx86 (808x.c:1222) est `cycles += cycs; while (cycles > 0)` : une seule
// boucle, bornée par le budget. exec386 en a DEUX. L'externe est bornée par le
// budget ; l'INTERNE par `cycdiff < cycle_period`, où cycle_period est la distance
// au prochain réveil de chronomètre. Le budget ne borne pas l'interne — c'est ce
// qui rend le pas-à-pas différent (voir h_step286 dans tools/oracle/harness.c).
//
// CE QUI N'EST PAS ENCORE LÀ, et qui échoue au lieu de mentir :
//   - les 256 handlers (A2.2c et suivants) ;
//   - flags_rebuild(), dont les tables vivent dans x86_flags.h ;
//   - pmodeint, taskswitch, x86_smi_enter — mode protégé et SMM.
// Aucun de ces chemins n'est atteignable tant que la table est vide : sans
// handler, il n'y a ni abandon, ni piège, ni interruption logicielle.
//
// 386.c:1-152 n'est PAS transcrit, et c'est délibéré : ce bloc est du code MORT,
// dupliqué à l'identique dans 386_dynarec.c:30-154. Seule la copie de
// 386_dynarec.c est vivante, parce que c'est elle que l'inclusion de 386_ops.h
// voit. Vérifié : 386.c ne contient aucun `#include "386_ops.h"`.

using iXtal26.Memory;
using static iXtal26.Cpu._386_common;
using static iXtal26.Cpu.x86;

namespace iXtal26.Cpu;

// pcem: x86_ops.h:4 — `typedef int (*OpFn)(uint32_t fetchdat)`
internal delegate int OpFn(uint32_t fetchdat);

internal static partial class _386
{
    // pcem: x86_ops.h:8-11. Quatre tables chez PCem ; les deux dynarec ne sont
    // jamais lues ici — cpu_use_dynarec vaut 0, et le 286 comme le 386 sont
    // interpréteurs par conception (cpu_flags == 0 dans cpu_tables.c).
    internal static OpFn[]? x86_opcodes;
    internal static OpFn[]? x86_opcodes_0f;

    // pcem: cpu.c:2003
    internal static void x86_setopcodes(OpFn[] opcodes, OpFn[] opcodes_0f)
    {
        x86_opcodes = opcodes;
        x86_opcodes_0f = opcodes_0f;
    }

    // -----------------------------------------------------------------------
    // La table du 286 (pcem: 386_ops.h:10721 — `OpFn OP_TABLE(286)[1024]`).
    //
    // 1024 entrées, mais SEULES LES 256 PREMIÈRES SONT ATTEIGNABLES : l'index est
    // `(opcode | cpu_state.op32) & 0x3ff` et op32 vaut use32, nul sur un 286. Les
    // trois quadrants hauts sont les variantes 32 bits, que ce processeur n'a pas.
    // -----------------------------------------------------------------------
    internal static readonly OpFn[] ops_286 = BuildEmpty();
    internal static readonly OpFn[] ops_286_0f = BuildEmpty();

    // -----------------------------------------------------------------------
    // La table du 386 (pcem: 386_ops.h:11775 — `OpFn OP_TABLE(386)[1024]`) et sa
    // seconde (:1235 — `OP_TABLE(386_0f)`), G2 étape D0.2. Le 486 les emprunte aussi
    // (cpu.c:231) : ses instructions propres y sont, gardées par `if (!is486)`.
    //
    // ICI LES QUATRE QUADRANTS SONT DISTINCTS : op32 vaut use32 | les préfixes 66/67,
    // et un 386 a les deux. Elles se remplissent en DEUX temps : la part partagée avec
    // le 286 — même nom de handler, donc même fonction C — est posée par le fichier
    // GÉNÉRÉ 386_ops_table386.cs ; le reste demeure opNonTranscrit jusqu'à son étape.
    // Jamais par recopie de ops_286 : 66, 67, POPF, IRET y sont autres, et une recopie
    // les aurait laissés au 286 sans qu'aucun côté ne proteste.
    // -----------------------------------------------------------------------
    internal static readonly OpFn[] ops_386 = BuildEmpty();
    internal static readonly OpFn[] ops_386_0f = BuildEmpty();

    private static OpFn[] BuildEmpty()
    {
        var t = new OpFn[1024];
        for (var i = 0; i < t.Length; i++)
                t[i] = opNonTranscrit;
        return t;
    }

    // Les groupes transcrits se posent après coup, chacun dans son fichier, ce qui
    // laisse les trous bruyants. Un groupe absent de cette liste est un groupe dont
    // chaque opcode échoue en se nommant.
    static _386()
    {
        PoserGroupeMov();
        PoserGroupeArith();
        PoserGroupePile();
        PoserGroupeSauts();
        PoserGroupeAppel();
        PoserGroupeRetour();
        PoserGroupeDrapeaux();
        PoserGroupeIncDec();
        PoserGroupeXchg();
        PoserGroupeDecalages();
        PoserNop();
        PoserGroupeChaines();
        PoserGroupePrefixes();
        PoserGroupeES();
        PoserGroupeBCD();
        PoserGroupeInterruptions();
        PoserGroupeMovSeg();
        PoserGroupeMul();
        PoserGroupeMisc();
        PoserGroupeFPU();
        PoserGroupeRep();
        PoserGroupeModeProtege();
        PoserTable0F();

        // LES QUATRE QUADRANTS SONT IDENTIQUES, et il faut les recopier.
        //
        // L'index d'aiguillage est `(opcode | cpu_state.op32) & 0x3ff`, et op32
        // vaut use32 — nul sur un 286, d'ou l'idee recue que seuls les 256
        // premiers comptent. FAUX : les prefixes 66 et 67 POSENT op32 eux-memes,
        // sans consulter use32. Ils sont ILLEGAL dans ops_286, mais les tables
        // ops_REPE et ops_REPNE, elles, sont PARTAGEES entre generations et y
        // mettent op_66_REPE / op_67_REPE. Un `REPNE 67 xx` pose donc op32 =
        // 0x200 sur un 286, et l'index sort du quadrant zero.
        //
        // Verifie dans la .so par gdb : ops_286[0x67], [0x167], [0x267] et
        // [0x367] nomment tous ILLEGAL ; ops_286[0xA4], [0x1A4] et [0x2A4] tous
        // opMOVSB_a16. Sans cette recopie, le C# tombait sur opNonTranscrit la
        // ou l'oracle executait le handler — mesure a A11 :
        //   opcode 0xF2, octets F2 67 — cycles : oracle 4, C# 90
        for (var q = 1; q < 4; q++)
                for (var i = 0; i < 256; i++)
                        ops_286[(q << 8) | i] = ops_286[i];

        // APRÈS la table du 286 et ses recopies : la part partagée s'y lit.
        PoserTable386Partagee();
        PoserPrefixes386();
        PoserBSWAP386();
        PoserSHxD386();
        PoserBTx386();
        PoserSautsLongs386();
        PoserGroupe_misc_0f_386();
        PoserGroupe_mov_seg_0f_386();
        PoserGroupe_mul_0f_386();
        PoserGroupe_atomic_0f_386();
        PoserGroupe_movx_0f_386();
        PoserGroupe_bitscan_0f_386();
        PoserGroupe_bit_0f_386();
        PoserRep386();
        PoserPile386();
        PoserIncDec386();
        PoserLsel386();
        PoserOp0F386();
        PoserGroupeArith386();
        PoserGroupe_fpu_386();
        PoserGroupe_arith_386();
        PoserGroupe_string_386();
        PoserGroupe_stack_386();
        PoserGroupe_shift_386();
        PoserGroupe_pmode_386();
        PoserGroupe_mul_386();
        PoserGroupe_misc_386();
        PoserGroupe_jump_386();
        PoserGroupe_inc_dec_386();
        PoserGroupe_int_386();
        PoserGroupe_call_386();
        PoserGroupe_ret_386();
        PoserGroupe_io_386();
        PoserGroupe_flag_386();
        PoserGroupe_xchg_386();
        PoserGroupeMovSeg386();
        PoserGroupeMov386();
        PoserGroupe_pmode_0f_386();
        PoserGroupe_mov_ctrl_0f_386();
    }

    /// <summary>L'entrée par défaut de la table : elle ÉCHOUE, et elle nomme
    /// l'opcode. Un handler manquant doit s'entendre — pas rendre zéro cycle et
    /// laisser la divergence se manifester trois mille instructions plus loin.
    ///
    /// LA TABLE À UN OCTET EST PLEINE DEPUIS A11 — 256 sur 256 — donc cette
    /// entrée n'y figure plus. Elle reste la garnissure de ops_286_0f, la table
    /// à DEUX octets, dont les six handlers d'un 286 relèvent du bloc C du plan.
    ///
    /// L'opcode est relu depuis la mémoire plutôt que mémorisé dans un champ : la
    /// boucle a déjà avancé le pc, mais oldpc désigne toujours le début de
    /// l'instruction. Aucun état inventé.</summary>
    private static int opNonTranscrit(uint32_t fetchdat)
    {
        var op = fastreadb(cs + cpu_state.oldpc);
        // UN ÉCHAPPEMENT 0F NOMMAIT « 0F » ET NON LE SECOND OCTET, parce que
        // oldpc pointe sur le premier. Le message mentait sur ce qui manque ;
        // il donne maintenant les deux, et dit laquelle des deux tables est
        // en cause.
        if (op == 0x0F)
        {
                var op2 = fastreadb(cs + cpu_state.oldpc + 1);
                pc.fatal($"opcode 0F {op2:X2} non transcrit ({NomTable()}_0f, op32 " +
                         $"0x{cpu_state.op32:X3}) a {CS:X4}:{cpu_state.oldpc:X4}\n");
                return 0;
        }
        // G2 : sur un 386, la table a un octet n'est PAS pleine — 450 emplacements y
        // attendent leur étape (PLAN-386.md). Le quadrant op32 dit laquelle des quatre
        // formes manque : 0x100 opérande 32 bits, 0x200 adresse 32 bits.
        pc.fatal($"opcode {op:X2} non transcrit ({NomTable()}, op32 0x{cpu_state.op32:X3}) " +
                 $"a {CS:X4}:{cpu_state.oldpc:X4}\n");
        return 0;
    }

    private static string NomTable() => ReferenceEquals(x86_opcodes, ops_386) ? "ops_386" : "ops_286";

    // -----------------------------------------------------------------------
    // pcem: 386_dynarec.c:210-225 — les macros de temps de l'INTERPRÉTEUR.
    //
    // PREFETCH_RUN est gardé par `if (cpu_prefetch_cycles)`. Sur un 8088 ce
    // compteur vaut zéro et le modèle ne tourne pas ; il ne prend ses valeurs que
    // pour le 286, via cpu_update_waitstates (cpu.c:2010-2047).
    // -----------------------------------------------------------------------

    // pcem: 386_dynarec.c:152-153. `internal` et non `private` : ils entrent dans le
    // vecteur d'état comparé, comme les statiques de temps de 808x.c — dont l'oracle
    // a dû, pour la même raison, compiler le .c dans son unité de traduction.
    internal static int prefetch_bytes;
    internal static int prefetch_prefixes;

    /// <summary>Pendant de h_prefetch_reset(). PCem ne le fait jamais : ce sont des
    /// statiques de BSS et il n'amorce qu'une fois par processus.</summary>
    internal static void prefetch_reset()
    {
        prefetch_bytes = 0;
        prefetch_prefixes = 0;
    }

    /// <summary>pcem: 386_dynarec.c:155-206 — LE MODÈLE DE PRÉFETCH DE
    /// L'INTERPRÉTEUR, et non du recompilateur.
    ///
    /// Il facture le remplissage de la file d'instruction : chaque instruction
    /// consomme des octets, et la file se recharge par tranches de
    /// cpu_prefetch_width au prix de cpu_prefetch_cycles chacune. Quand
    /// l'instruction met plus de temps qu'il n'en faut pour recharger, la file se
    /// remplit gratuitement — d'où la seconde boucle.
    ///
    /// Il ne tourne QUE si cpu_prefetch_cycles est non nul (386_dynarec.c:210). Sur
    /// un 8088 c'est zéro et le modèle est inerte ; sur un 286 il vaut 2, et ce
    /// modèle entre alors dans les cycles comparés.</summary>
    private static void prefetch_run(int instr_cycles, int bytes, int modrm, int reads, int reads_l,
                                     int writes, int writes_l, int ea32)
    {
        int mem_cycles = reads * cpu_c.cpu_cycles_read + reads_l * cpu_c.cpu_cycles_read_l
                       + writes * cpu_c.cpu_cycles_write + writes_l * cpu_c.cpu_cycles_write_l;

        if (instr_cycles < mem_cycles)
                instr_cycles = mem_cycles;

        prefetch_bytes -= prefetch_prefixes;
        prefetch_bytes -= bytes;
        if (modrm != -1)
        {
                if (ea32 != 0)
                {
                        if ((modrm & 7) == 4)
                        {
                                if ((modrm & 0x700) == 0x500)
                                        prefetch_bytes -= 5;
                                else if ((modrm & 0xc0) == 0x40)
                                        prefetch_bytes -= 2;
                                else if ((modrm & 0xc0) == 0x80)
                                        prefetch_bytes -= 5;
                        }
                        else
                        {
                                if ((modrm & 0xc7) == 0x05)
                                        prefetch_bytes -= 4;
                                else if ((modrm & 0xc0) == 0x40)
                                        prefetch_bytes--;
                                else if ((modrm & 0xc0) == 0x80)
                                        prefetch_bytes -= 4;
                        }
                }
                else
                {
                        if ((modrm & 0xc7) == 0x06)
                                prefetch_bytes -= 2;
                        else if ((modrm & 0xc0) != 0xc0)
                                prefetch_bytes -= ((modrm & 0xc0) >> 6);
                }
        }

        /*Fill up prefetch queue*/
        while (prefetch_bytes < 0)
        {
                prefetch_bytes += cpu_c.cpu_prefetch_width;
                cycles -= cpu_c.cpu_prefetch_cycles;
        }

        /*Subtract cycles used for memory access by instruction*/
        instr_cycles -= mem_cycles;

        while (instr_cycles >= cpu_c.cpu_prefetch_cycles)
        {
                prefetch_bytes += cpu_c.cpu_prefetch_width;
                instr_cycles -= cpu_c.cpu_prefetch_cycles;
        }

        prefetch_prefixes = 0;
        if (prefetch_bytes > 16)
                prefetch_bytes = 16;
    }

    // pcem: 386_dynarec.c:208
    internal static void prefetch_flush() => prefetch_bytes = 0;

    // pcem: 386_dynarec.c:210-221 — les gardes. PREFETCH_RUN ne fait rien tant que
    // cpu_prefetch_cycles est nul, ce qui est le cas de tout le palier (a).
    internal static void PREFETCH_RUN(int instr_cycles, int bytes, int modrm, int reads, int reads_l,
                                      int writes, int writes_l, int ea32)
    {
        if (cpu_c.cpu_prefetch_cycles != 0)
                prefetch_run(instr_cycles, bytes, modrm, reads, reads_l, writes, writes_l, ea32);
    }

    internal static void PREFETCH_PREFIX()
    {
        if (cpu_c.cpu_prefetch_cycles != 0)
                prefetch_prefixes++;
    }

    internal static void CLOCK_CYCLES(int c) => cycles -= c;

    // pcem: 386_dynarec.c:225. Même corps que CLOCK_CYCLES dans l'interpréteur —
    // la distinction n'existe que pour le recompilateur, qui n'est pas lié ici.
    internal static void CLOCK_CYCLES_ALWAYS(int c) => cycles -= c;

    // pcem: 386_dynarec.c:221
    internal static void PREFETCH_FLUSH() => prefetch_flush();

    // pcem: 386_dynarec.c:22 — voir x86.cs pour pourquoi c'est inerte.
    internal static void CPU_BLOCK_END() => cpu_block_end = 1;

    // -----------------------------------------------------------------------
    // pcem: 386.c:153-295
    // -----------------------------------------------------------------------
    internal static void exec386(int cycs)
    {
        uint32_t addr;
        int tempi;
        int cycdiff;
        int oldcyc;

        cycles += cycs;

        while (cycles > 0)
        {
                int cycle_period = (int)(timer.timer_target - (uint32_t)timer.tsc) + 1;

                x86_was_reset = 0;
                cycdiff = 0;
                oldcyc = cycles;

                while (cycdiff < cycle_period)
                {
                        int ins_cycles = cycles;

                        cpu_state.oldpc = cpu_state.pc;
                        cpu_state.op32 = use32;

                        cpu_state.ea_seg = cpu_state.seg_ds;
                        cpu_state.ssegs = 0;

                        rmdat = fastreadl(cs + cpu_state.pc);

                        if (cpu_state.abrt == 0)
                        {
                                var opcode = (uint8_t)(rmdat & 0xFF);
                                rmdat >>= 8;
                                trap = cpu_state.flags & T_FLAG;

                                // omitted: le bloc `if (output == 3) pclog(...)` —
                                //   trace de débogage, sortie pure.
                                cpu_state.pc++;
                                x86_opcodes![(opcode | cpu_state.op32) & 0x3ff](rmdat);
                                if (x86_was_reset != 0)
                                        break;
                        }

                        if (cpu_state.abrt != 0)
                        {
                                flags_rebuild();
                                tempi = cpu_state.abrt & ABRT_MASK;
                                cpu_state.abrt = 0;
                                x86_doabrt(tempi);
                                if (cpu_state.abrt != 0)
                                {
                                        // pcem: 386.c:206-216 — LA DOUBLE FAUTE, M20. C'était un
                                        //   fatal() sans marqueur ; PCem livre #DF par pmodeint(8, 0),
                                        //   et une faute de plus est la triple faute : reset.
                                        cpu_state.abrt = 0;
                                        cpu_state.pc = cpu_state.oldpc;
                                        // omitted: pclog("Double fault %i\n", ins) — sortie pure.
                                        x86seg_c.pmodeint(8, 0);
                                        if (cpu_state.abrt != 0)
                                        {
                                                cpu_state.abrt = 0;
                                                _808x.softresetx86();
                                                // DEVIATION: cpu_set_edx() (386.c:214) non appelé, et
                                                //   l'oracle l'enveloppe à vide (__wrap_cpu_set_edx,
                                                //   harness_stubs.c). Même arbitrage que les deux autres
                                                //   resets, x86seg.cs (triple faute de pmodeint) et
                                                //   keyboard_at.cs : DX garde sa valeur, des deux côtés.
                                                // omitted: pclog("Triple fault - reset\n") — sortie pure.
                                        }
                                }
                        }

                        // omitted: `if (cpu_state.smi_pending) x86_smi_enter()` — SMM,
                        //   486 et au-delà. Le champ smi_pending n'existe pas dans le
                        //   cpu_state de ce port.
                        if (trap != 0)
                        {
                                flags_rebuild();
                                if ((msw & 1) != 0)
                                {
                                        x86seg_c.pmodeint(1, 0);
                                }
                                else
                                {
                                        writememw(ss, (uint32_t)((SP - 2) & 0xFFFF), cpu_state.flags);
                                        writememw(ss, (uint32_t)((SP - 4) & 0xFFFF), CS);
                                        writememw(ss, (uint32_t)((SP - 6) & 0xFFFF), (uint16_t)cpu_state.pc);
                                        SP -= 6;
                                        addr = (1 << 2) + idt.@base;
                                        cpu_state.flags &= unchecked((uint16_t)~I_FLAG);
                                        cpu_state.flags &= unchecked((uint16_t)~T_FLAG);
                                        cpu_state.pc = readmemw(0, addr);
                                        x86seg_c.loadcs(readmemw(0, addr + 2));
                                }
                        }
                        // pcem: 386.c:240-248 — LA NMI, ET ELLE SE DESARME ELLE-MEME.
                        //
                        // `nmi_enable = 0` apres l'avoir prise : une seconde NMI ne sera
                        // pas servie avant que quelque chose la reactive. C'est le
                        // comportement du 8088 comme du 286, et c'est pourquoi le port
                        // 0xA0 du XT et le bit 7 du port 0x70 de l'AT existent.
                        //
                        // `oldpc` EST POSE AVANT L'APPEL, et ca compte : x86_int fait
                        // `cpu_state.pc = cpu_state.oldpc` pour que l'instruction fautive
                        // soit REJOUEE apres l'interruption. Sans cette ligne, x86_int
                        // rejouerait celle d'avant.
                        else if (_808x.nmi != 0 && nmi_enable != 0 && _808x.nmi_mask != 0)
                        {
                                cpu_state.oldpc = cpu_state.pc;
                                _386_common.x86_int(2);
                                nmi_enable = 0;
                                if (_808x.nmi_auto_clear != 0)
                                {
                                        _808x.nmi_auto_clear = 0;
                                        _808x.nmi = 0;
                                }
                        }
                        // pcem: 386.c:249-275 — L'INTERRUPTION MATERIELLE, et c'est ELLE
                        // qui manquait pour qu'un AT tourne pour de vrai.
                        //
                        // Le boot-diff ne l'atteignait pas : il compare instruction par
                        // instruction et la premiere IRQ0 du PIT arrive bien avant. Mais
                        // `--boot roms 6000 --model ibmat` tombait dessus a la tranche 0,
                        // apres 8 690 instructions. Un fatal() sur le chemin le plus
                        // ordinaire d'une machine.
                        //
                        // 0xFF VEUT DIRE « PERSONNE », et il faut le tester : picinterrupt
                        // rend 0xFF quand aucune ligne n'est finalement servie — une IRQ
                        // masquee entre-temps, ou un acquittement spontane. Prendre le
                        // vecteur 0xFF serait sauter dans la table par son dernier
                        // emplacement.
                        else if ((cpu_state.flags & I_FLAG) != 0 && Models.pic.pic_intpending != 0)
                        {
                                var temp = Models.pic.picinterrupt();
                                if (temp != 0xFF)
                                {
                                        flags_rebuild();
                                        if ((msw & 1) != 0)
                                        {
                                                x86seg_c.pmodeint(temp, 0);
                                        }
                                        else
                                        {
                                                writememw(ss, (uint32_t)((SP - 2) & 0xFFFF), cpu_state.flags);
                                                writememw(ss, (uint32_t)((SP - 4) & 0xFFFF), CS);
                                                writememw(ss, (uint32_t)((SP - 6) & 0xFFFF), (uint16_t)cpu_state.pc);
                                                SP -= 6;
                                                addr = (uint32_t)(temp << 2) + idt.@base;
                                                cpu_state.flags &= unchecked((uint16_t)~I_FLAG);
                                                cpu_state.flags &= unchecked((uint16_t)~T_FLAG);
                                                cpu_state.pc = readmemw(0, addr);
                                                x86seg_c.loadcs(readmemw(0, addr + 2));
                                        }
                                }
                        }

                        _808x.ins++;
                        _808x.insc++;

                        ins_cycles -= cycles;
                        timer.tsc += (uint64_t)ins_cycles;

                        cycdiff = oldcyc - cycles;

                        // omitted: le bloc `timetolive` — garde-fou de débogage de PCem.
                }

                if (timer.TIMER_VAL_LESS_THAN_VAL(timer.timer_target, (uint32_t)timer.tsc))
                        timer.timer_process();
        }
    }

    // pcem: x86_flags.h:420 — la matérialisation des drapeaux paresseux, transcrite
    // en A3a dans Cpu/x86_flags.cs. Jusque-là, ce point n'était qu'une garde qui
    // échouait : sans corps, un handler posant flags_op aurait rendu des drapeaux
    // faux EN SILENCE.
    internal static void flags_rebuild() => x86_flags.flags_rebuild();

    internal const int FLAGS_UNKNOWN = x86_flags.FLAGS_UNKNOWN;

    /// <summary>pcem: x86seg.c:76-112 — LA PRISE EN CHARGE DE L'ABANDON, branche
    /// MODE RÉEL seulement.
    ///
    /// Elle devient atteignable en A2.2d, et pas par le mode protégé : CHECK_READ
    /// mord quand un accès MOT déborde de limit_high, ce qui arrive à l'offset
    /// 0xFFFF en mode réel. Le fuzzeur y tombe tout seul.
    ///
    /// Le geste est celui d'une interruption matérielle : empiler flags, CS et pc,
    /// puis sauter par le vecteur. `pc = oldpc` d'abord — l'instruction fautive est
    /// REJOUÉE après l'exception, elle n'est pas passée.</summary>
    private static void x86_doabrt(int x86_abrt)
    {
        cpu_state.pc = cpu_state.oldpc;
        cpu_state.seg_cs.access = (uint8_t)(oldcpl << 5);

        if ((msw & 1) != 0)
        {
                x86seg_c.pmodeint(x86_abrt, 0);
        }
        else
        {
                RealModeAbrt(x86_abrt);
                return;
        }

        // pcem: x86seg.c:113-133 — LA QUEUE MODE PROTEGE, et elle n'empile le code
        // d'erreur qu'APRES que pmodeint ait change de pile.
        //
        // L'ORDRE EST LE POINT : pmodeint empile flags, CS et pc sur la pile du niveau
        // CIBLE, puis on ajoute le code d'erreur par-dessus. L'inverse le mettrait sur
        // l'ancienne pile, que le gestionnaire ne lit pas.
        //
        // ET LE TEST DE SORTIE COMPTE AUTANT. Si pmodeint a lui-meme faute — abrt non
        // nul — ou s'il a provoque un reset par triple faute — x86_was_reset — il n'y a
        // plus de pile ou ecrire : on rend la main sans rien empiler.
        if (cpu_state.abrt != 0 || x86_was_reset != 0)
                return;

        // intgatesize EST ECRIT PAR pmodeint ET LU ICI, ET NULLE PART AILLEURS. A zero
        // il ferait prendre la branche 32 bits, la MAUVAISE sur un 286 — quatre octets
        // empiles au lieu de deux, et le gestionnaire lirait tout de travers. C'est
        // pourquoi les deux arrivent dans le meme commit.
        if (x86seg_c.intgatesize == 16)
        {
                if (stack32 != 0)
                {
                        writememw(ss, ESP - 2, (uint16_t)abrt_error);   // le C tronque implicitement
                        ESP -= 2;
                }
                else
                {
                        writememw(ss, (uint32_t)((SP - 2) & 0xFFFF), (uint16_t)abrt_error);
                        SP -= 2;
                }
        }
        else
        {
                if (stack32 != 0)
                {
                        writememl(ss, ESP - 4, abrt_error);
                        ESP -= 4;
                }
                else
                {
                        writememl(ss, (uint32_t)((SP - 4) & 0xFFFF), abrt_error);
                        SP -= 4;
                }
        }
    }

    /// <summary>pcem: x86seg.c:95-112 — la branche MODE REEL de x86_doabrt, extraite
    /// pour que la queue mode protege puisse rendre la main en un seul endroit. Le C
    /// fait `return` au milieu de la fonction ; l'extraction garde le meme flot.</summary>
    private static void RealModeAbrt(int x86_abrt)
    {
        uint32_t addr = (uint32_t)(x86_abrt << 2) + idt.@base;
        if (stack32 != 0)
        {
                writememw(ss, ESP - 2, cpu_state.flags);
                writememw(ss, ESP - 4, CS);
                writememw(ss, ESP - 6, (uint16_t)cpu_state.pc);
                ESP -= 6;
        }
        else
        {
                writememw(ss, (uint32_t)((SP - 2) & 0xFFFF), cpu_state.flags);
                writememw(ss, (uint32_t)((SP - 4) & 0xFFFF), CS);
                writememw(ss, (uint32_t)((SP - 6) & 0xFFFF), (uint16_t)cpu_state.pc);
                SP -= 6;
        }

        cpu_state.flags &= unchecked((uint16_t)~I_FLAG);
        cpu_state.flags &= unchecked((uint16_t)~T_FLAG);
        cpu_state.pc = readmemw(0, addr);
        x86seg_c.loadcs(readmemw(0, addr + 2));
    }
}
