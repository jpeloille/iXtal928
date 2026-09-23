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
    }

    /// <summary>L'entrée par défaut de la table : elle ÉCHOUE, et elle nomme
    /// l'opcode. Un handler manquant doit s'entendre — pas rendre zéro cycle et
    /// laisser la divergence se manifester trois mille instructions plus loin.
    ///
    /// L'opcode est relu depuis la mémoire plutôt que mémorisé dans un champ : la
    /// boucle a déjà avancé le pc, mais oldpc désigne toujours le début de
    /// l'instruction. Aucun état inventé.</summary>
    private static int opNonTranscrit(uint32_t fetchdat)
    {
        var op = fastreadb(cs + cpu_state.oldpc);
        pc.fatal($"opcode {op:X2} non transcrit (A2.2b : la table du 286 est vide) " +
                 $"a {CS:X4}:{cpu_state.oldpc:X4}\n");
        return 0;
    }

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
        int mem_cycles = reads * cpu.cpu_cycles_read + reads_l * cpu.cpu_cycles_read_l
                       + writes * cpu.cpu_cycles_write + writes_l * cpu.cpu_cycles_write_l;

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
                prefetch_bytes += cpu.cpu_prefetch_width;
                cycles -= cpu.cpu_prefetch_cycles;
        }

        /*Subtract cycles used for memory access by instruction*/
        instr_cycles -= mem_cycles;

        while (instr_cycles >= cpu.cpu_prefetch_cycles)
        {
                prefetch_bytes += cpu.cpu_prefetch_width;
                instr_cycles -= cpu.cpu_prefetch_cycles;
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
        if (cpu.cpu_prefetch_cycles != 0)
                prefetch_run(instr_cycles, bytes, modrm, reads, reads_l, writes, writes_l, ea32);
    }

    internal static void PREFETCH_PREFIX()
    {
        if (cpu.cpu_prefetch_cycles != 0)
                prefetch_prefixes++;
    }

    internal static void CLOCK_CYCLES(int c) => cycles -= c;

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
                                        cpu_state.abrt = 0;
                                        cpu_state.pc = cpu_state.oldpc;
                                        pc.fatal($"Double fault {_808x.ins}\n");
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
                                        pc.fatal("trap en mode protege : pmodeint n'est pas transcrit (Ap)\n");
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
                        else if (_808x.nmi != 0 && nmi_enable != 0 && _808x.nmi_mask != 0)
                        {
                                pc.fatal("NMI : x86_int n'est pas transcrit pour ce coeur (A2.2b)\n");
                        }
                        else if ((cpu_state.flags & I_FLAG) != 0 && Models.pic.pic_intpending != 0)
                        {
                                pc.fatal("interruption materielle : chemin non transcrit (A2.2b)\n");
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
                pc.fatal("x86_doabrt en mode protege : pmodeint n'est pas transcrit (Ap)\n");
                return;
        }

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
