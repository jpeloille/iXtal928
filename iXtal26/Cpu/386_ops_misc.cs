// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/includes/private/cpu/x86_ops_misc.h  (opNOP, opSETALC, les
//         groupes F6 et F7, opCBW, opCWD, opBOUND, opHLT)
// SHA256: voir oracle.tsv ; vérifier avec tools/check-oracle.sh
// STATUS: partial — A9 pour opNOP, A11 pour les sept autres. Ce qui reste de
//         l'en-tête appartient à d'autres tables : les formes `_a32` et `_l`
//         (op32 nul), et les handlers 386 et au-delà. XLAT, LEA, LES et LDS
//         ne sont PAS ici malgré le voisinage : ils vivent dans x86_ops_mov.h
//         et x86_ops_mov_seg.h — vérifié, pas supposé.
//
// POURQUOI NOP ARRIVE AVANT SON GROUPE.
//
// Le fuzzeur et core286-check remplissent la RAM de 0x90 des deux côtés, et
// c'est un choix : si le cœur s'échappe, il tombe sur des no-op plutôt que sur
// du hasard. Ce choix ne valait que tant que 0x90 était transcrit — il ne
// l'était pas, et A9 a rendu la fuite POSSIBLE.
//
// LE MÉCANISME, mesuré. Un décalage de compte nul sort par `if (!c) return 0;`
// AVANT tout CLOCK_CYCLES : il consomme ZÉRO cycle. Or la boucle interne de
// exec386 est bornée par `cycdiff < cycle_period`, et h_step286 comme Step286
// posent cycle_period à 1 en rapprochant timer_target de tsc. Avec un cycdiff
// qui reste à zéro, la boucle REPART — et « un pas » exécute deux instructions.
//
// Les deux côtés le font à l'identique, donc la comparaison reste juste ; ce
// qui ne l'était pas, c'est que le C# rencontrait alors un opcode non
// transcrit là où l'oracle exécutait un NOP. La porte « un pas = une
// instruction » de core286-check reste vraie pour tout ce qui consomme au
// moins un cycle, et c'était le cas de tous les groupes jusqu'à A9.

using iXtal26.Models;
using static iXtal26.Cpu._386_common;
using static iXtal26.Cpu.x86;
using static iXtal26.Cpu.x86_flags;

namespace iXtal26.Cpu;

internal static partial class _386
{
    // pcem: x86_ops_misc.h:28-32 — opNOP.
    //
    // Il occupe l'emplacement 0x90, qui SERAIT XCHG AX, AX. PCem lui donne un
    // handler à part parce qu'un échange d'un registre avec lui-même n'écrit
    // rien et ne pose aucun drapeau — voir 386_ops_xchg.cs, dont la boucle part
    // de 1 pour lui laisser la place.
    private static int opNOP(uint32_t fetchdat)
    {
        CLOCK_CYCLES(is486 != 0 ? 1 : 3);
        PREFETCH_RUN(3, 1, -1, 0, 0, 0, 0, 0);
        return 0;
    }

    // pcem: x86_ops_misc.h:34-39 — opSETALC.
    //
    // UN OPCODE NON DOCUMENTÉ D'INTEL, et il marche depuis le 8086 : D6 recopie
    // la retenue dans tout AL. Il n'apparaît dans aucun manuel, ce qui ne l'a
    // pas empêché d'être utilisé. La table lui donne un emplacement ; on le pose.
    private static int opSETALC(uint32_t fetchdat)
    {
        AL = (CF_SET() != 0) ? (uint8_t)0xff : (uint8_t)0;
        CLOCK_CYCLES(cpu_c.timing_rr);
        PREFETCH_RUN(cpu_c.timing_rr, 1, -1, 0, 0, 0, 0, 0);
        return 0;
    }

    // pcem: x86_ops_misc.h — opCBW
    private static int opCBW(uint32_t fetchdat)
    {
        AH = ((AL & 0x80) != 0) ? (uint8_t)0xff : (uint8_t)0;
        CLOCK_CYCLES(3);
        PREFETCH_RUN(3, 1, -1, 0, 0, 0, 0, 0);
        return 0;
    }

    // pcem: x86_ops_misc.h — opCWD
    private static int opCWD(uint32_t fetchdat)
    {
        DX = ((AX & 0x8000) != 0) ? (uint16_t)0xFFFF : (uint16_t)0;
        CLOCK_CYCLES(2);
        PREFETCH_RUN(2, 1, -1, 0, 0, 0, 0, 0);
        return 0;
    }

    // pcem: x86_ops_misc.h:714-733 — opBOUND_w_a16.
    //
    // LA SEULE INSTRUCTION DE LA TABLE QUI LÈVE UNE INTERRUPTION SUR UNE
    // COMPARAISON. Elle lit deux bornes en mémoire et déclenche INT 5 si le
    // registre sort de l'intervalle — bornes INCLUSES, donc `< low` ou
    // `> high`. Et elle ne pose aucun drapeau.
    private static int opBOUND_w_a16(uint32_t fetchdat)
    {
        int16_t low, high;

        if (fetch_ea_16(fetchdat)) return 1;
        if (ILLEGAL_ON(cpu_mod == 3)) return 0;
        if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        low = (int16_t)geteaw();
        high = (int16_t)readmemw(easeg, cpu_state.eaaddr + 2);
        if (cpu_state.abrt != 0)
                return 1;

        if (((int16_t)cpu_state.regs[cpu_reg].w < low) || ((int16_t)cpu_state.regs[cpu_reg].w > high))
        {
                x86_int(5);
                return 1;
        }

        CLOCK_CYCLES(is486 != 0 ? 7 : 10);
        PREFETCH_RUN(is486 != 0 ? 7 : 10, 2, (int)fetchdat, 2, 0, 0, 0, 0);
        return 0;
    }

    // pcem: x86_ops_misc.h — opHLT.
    //
    // IL RECULE pc SUR LUI-MÊME. Tant qu'aucune interruption n'est en attente,
    // `cpu_state.pc--` fait que la prochaine instruction exécutée est CE MÊME
    // HLT : le processeur tourne sur place à 100 cycles le tour. Ce n'est pas un
    // arrêt, c'est une boucle — et c'est ce qui permet à exec386 de continuer à
    // faire avancer les chronomètres.
    private static int opHLT(uint32_t fetchdat)
    {
        if ((CPL != 0 || (cpu_state.eflags & VM_FLAG) != 0) && (cr0 & 1) != 0)
        {
                x86seg_c.x86gpf("", 0);
                return 1;
        }
        if (!((cpu_state.flags & I_FLAG) != 0 && pic.pic_intpending != 0))
        {
                CLOCK_CYCLES_ALWAYS(100);
                cpu_state.pc--;
        }
        else
                CLOCK_CYCLES(5);

        CPU_BLOCK_END();
        PREFETCH_RUN(100, 1, -1, 0, 0, 0, 0, 0);

        return 0;
    }

    // LES GROUPES F6 ET F7 : TEST, NOT, NEG, MUL, IMUL, DIV, IDIV.
    //
    // ILS NE SONT PAS SUBSTITUABLES EN LARGEUR, contrairement à OP_ARITH,
    // ARITH_MULTI ou OP_SHIFT. Leurs branches DIV et IDIV posent les drapeaux
    // DIFFÉREMMENT : F6 fait `flags_rebuild(); flags |= 0x8D5; flags &= ~1;` —
    // il allume tous les drapeaux arithmétiques sauf la retenue — tandis que F7
    // fait `setznp16(AX)`, qui repose une représentation PARESSEUSE. Le premier
    // laisse flags_op à UNKNOWN, le second le pose à FLAGS_ZN16.
    //
    // C'est une incohérence de PCem, pas de ce port, et elle est OBSERVABLE :
    // flags_op est dans le vecteur comparé depuis A2.1. Les écrire comme deux
    // instanciations d'une même macro aurait effacé la différence.
    //
    // ET LA DIVISION PAR ZÉRO LÈVE INT 0 SANS RIEN ÉCRIRE — `x86_int(0); return
    // 1;`. Le test est double : `dst` non nul ET le quotient tient dans la
    // largeur. Un `DIV` dont le quotient déborde lève la même exception qu'une
    // division par zéro, ce qui est conforme au 286.

    // pcem: x86_ops_misc.h:41-153 — opF6_a16
    private static int opF6_a16(uint32_t fetchdat)
    {
        int tempws, tempws2 = 0;
        uint16_t tempw = 0, src16;
        uint8_t src, dst;
        int8_t temps;

        if (fetch_ea_16(fetchdat)) return 1;
        if (cpu_mod != 3)
        {
                if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
                if (CHECK_READ(cpu_state.ea_seg!, cpu_state.eaaddr, cpu_state.eaaddr)) return 1;
        }
        dst = geteab();
        if (cpu_state.abrt != 0)
                return 1;
        switch (fetchdat & 0x38)
        {
        case 0x00: /*TEST b,#8*/
        case 0x08:
                src = readmemb(x86.cs, cpu_state.pc);
                cpu_state.pc++;
                if (cpu_state.abrt != 0)
                        return 1;
                setznp8((uint8_t)(src & dst));
                if (is486 != 0)
                        CLOCK_CYCLES((cpu_mod == 3) ? 1 : 2);
                else
                        CLOCK_CYCLES((cpu_mod == 3) ? 2 : 5);
                PREFETCH_RUN((cpu_mod == 3) ? 2 : 5, 3, (int)fetchdat, (cpu_mod == 3) ? 0 : 1, 0, 0, 0, 0);
                break;
        case 0x10: /*NOT b*/
                if (cpu_mod != 3)
                        if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
                seteab((uint8_t)~dst);
                if (cpu_state.abrt != 0)
                        return 1;
                CLOCK_CYCLES((cpu_mod == 3) ? cpu_c.timing_rr : cpu_c.timing_mm);
                PREFETCH_RUN((cpu_mod == 3) ? cpu_c.timing_rr : cpu_c.timing_mm, 2, (int)fetchdat,
                             (cpu_mod == 3) ? 0 : 1, 0, (cpu_mod == 3) ? 0 : 1, 0, 0);
                break;
        case 0x18: /*NEG b*/
                if (cpu_mod != 3)
                        if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
                seteab((uint8_t)(0 - dst));
                if (cpu_state.abrt != 0)
                        return 1;
                setsub8(0, dst);
                CLOCK_CYCLES((cpu_mod == 3) ? cpu_c.timing_rr : cpu_c.timing_mm);
                PREFETCH_RUN((cpu_mod == 3) ? cpu_c.timing_rr : cpu_c.timing_mm, 2, (int)fetchdat,
                             (cpu_mod == 3) ? 0 : 1, 0, (cpu_mod == 3) ? 0 : 1, 0, 0);
                break;
        case 0x20: /*MUL AL,b*/
                AX = (uint16_t)(AL * dst);
                flags_rebuild();
                if (AH != 0)
                        cpu_state.flags |= (C_FLAG | V_FLAG);
                else
                        cpu_state.flags &= unchecked((uint16_t)~(C_FLAG | V_FLAG));
                CLOCK_CYCLES(13);
                PREFETCH_RUN(13, 2, (int)fetchdat, (cpu_mod == 3) ? 0 : 1, 0, 0, 0, 0);
                break;
        case 0x28: /*IMUL AL,b*/
                tempws = (int)((int8_t)AL) * (int)((int8_t)dst);
                AX = (uint16_t)(tempws & 0xffff);
                flags_rebuild();
                if (((int16_t)AX >> 7) != 0 && ((int16_t)AX >> 7) != -1)
                        cpu_state.flags |= (C_FLAG | V_FLAG);
                else
                        cpu_state.flags &= unchecked((uint16_t)~(C_FLAG | V_FLAG));
                CLOCK_CYCLES(14);
                PREFETCH_RUN(14, 2, (int)fetchdat, (cpu_mod == 3) ? 0 : 1, 0, 0, 0, 0);
                break;
        case 0x30: /*DIV AL,b*/
                src16 = AX;
                if (dst != 0)
                        tempw = (uint16_t)(src16 / dst);
                if (dst != 0 && (tempw & 0xff00) == 0)
                {
                        AH = (uint8_t)(src16 % dst);
                        AL = (uint8_t)((src16 / dst) & 0xff);
                        if (cpu_c.cpu_iscyrix == 0)
                        {
                                flags_rebuild();
                                cpu_state.flags |= 0x8D5; /*Not a Cyrix*/
                                cpu_state.flags &= unchecked((uint16_t)~1);
                        }
                }
                else
                {
                        x86_int(0);
                        return 1;
                }
                CLOCK_CYCLES((is486 != 0 && cpu_c.cpu_iscyrix == 0) ? 16 : 14);
                PREFETCH_RUN((is486 != 0 && cpu_c.cpu_iscyrix == 0) ? 16 : 14, 2, (int)fetchdat,
                             (cpu_mod == 3) ? 0 : 1, 0, 0, 0, 0);
                break;
        case 0x38: /*IDIV AL,b*/
                tempws = (int)(int16_t)AX;
                if (dst != 0)
                        tempws2 = tempws / (int)((int8_t)dst);
                temps = (int8_t)(tempws2 & 0xff);
                if (dst != 0 && ((int)temps == tempws2))
                {
                        AH = (uint8_t)((tempws % (int)((int8_t)dst)) & 0xff);
                        AL = (uint8_t)(tempws2 & 0xff);
                        if (cpu_c.cpu_iscyrix == 0)
                        {
                                flags_rebuild();
                                cpu_state.flags |= 0x8D5; /*Not a Cyrix*/
                                cpu_state.flags &= unchecked((uint16_t)~1);
                        }
                }
                else
                {
                        x86_int(0);
                        return 1;
                }
                CLOCK_CYCLES(19);
                PREFETCH_RUN(19, 2, (int)fetchdat, (cpu_mod == 3) ? 0 : 1, 0, 0, 0, 0);
                break;

        default:
                x86illegal();
                break;
        }
        return 0;
    }

    // pcem: x86_ops_misc.h:270-381 — opF7_w_a16.
    //
    // COMPARER SES BRANCHES 0x30 ET 0x38 A CELLES DE F6 : la ou F6 fait
    // `flags_rebuild(); flags |= 0x8D5; flags &= ~1;`, F7 fait `setznp16(AX)`.
    // Voir le commentaire de F6 — c'est la difference qui interdit d'en faire
    // deux instanciations d'une meme macro.
    private static int opF7_w_a16(uint32_t fetchdat)
    {
        uint32_t templ, templ2 = 0;
        int tempws, tempws2 = 0;
        int16_t temps16;
        uint16_t src, dst;

        if (fetch_ea_16(fetchdat)) return 1;
        if (cpu_mod != 3)
                if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        dst = geteaw();
        if (cpu_state.abrt != 0)
                return 1;
        switch (fetchdat & 0x38)
        {
        case 0x00: /*TEST w*/
        case 0x08:
                src = getword();
                if (cpu_state.abrt != 0)
                        return 1;
                setznp16((uint16_t)(src & dst));
                if (is486 != 0)
                        CLOCK_CYCLES((cpu_mod == 3) ? 1 : 2);
                else
                        CLOCK_CYCLES((cpu_mod == 3) ? 2 : 5);
                PREFETCH_RUN((cpu_mod == 3) ? 2 : 5, 4, (int)fetchdat, (cpu_mod == 3) ? 0 : 1, 0, 0, 0, 0);
                break;
        case 0x10: /*NOT w*/
                if (cpu_mod != 3)
                        if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
                seteaw((uint16_t)~dst);
                if (cpu_state.abrt != 0)
                        return 1;
                CLOCK_CYCLES((cpu_mod == 3) ? cpu_c.timing_rr : cpu_c.timing_mm);
                PREFETCH_RUN((cpu_mod == 3) ? cpu_c.timing_rr : cpu_c.timing_mm, 2, (int)fetchdat,
                             (cpu_mod == 3) ? 0 : 1, 0, (cpu_mod == 3) ? 0 : 1, 0, 0);
                break;
        case 0x18: /*NEG w*/
                if (cpu_mod != 3)
                        if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
                seteaw((uint16_t)(0 - dst));
                if (cpu_state.abrt != 0)
                        return 1;
                setsub16(0, dst);
                CLOCK_CYCLES((cpu_mod == 3) ? cpu_c.timing_rr : cpu_c.timing_mm);
                PREFETCH_RUN((cpu_mod == 3) ? cpu_c.timing_rr : cpu_c.timing_mm, 2, (int)fetchdat,
                             (cpu_mod == 3) ? 0 : 1, 0, (cpu_mod == 3) ? 0 : 1, 0, 0);
                break;
        case 0x20: /*MUL AX,w*/
                templ = (uint32_t)(AX * dst);
                AX = (uint16_t)(templ & 0xFFFF);
                DX = (uint16_t)(templ >> 16);
                flags_rebuild();
                if (DX != 0)
                        cpu_state.flags |= (C_FLAG | V_FLAG);
                else
                        cpu_state.flags &= unchecked((uint16_t)~(C_FLAG | V_FLAG));
                CLOCK_CYCLES(21);
                PREFETCH_RUN(21, 2, (int)fetchdat, (cpu_mod == 3) ? 0 : 1, 0, 0, 0, 0);
                break;
        case 0x28: /*IMUL AX,w*/
                templ = (uint32_t)((int)((int16_t)AX) * (int)((int16_t)dst));
                AX = (uint16_t)(templ & 0xFFFF);
                DX = (uint16_t)(templ >> 16);
                flags_rebuild();
                if (((int32_t)templ >> 15) != 0 && ((int32_t)templ >> 15) != -1)
                        cpu_state.flags |= (C_FLAG | V_FLAG);
                else
                        cpu_state.flags &= unchecked((uint16_t)~(C_FLAG | V_FLAG));
                CLOCK_CYCLES(22);
                PREFETCH_RUN(22, 2, (int)fetchdat, (cpu_mod == 3) ? 0 : 1, 0, 0, 0, 0);
                break;
        case 0x30: /*DIV AX,w*/
                templ = (uint32_t)((DX << 16) | AX);
                if (dst != 0)
                        templ2 = templ / dst;
                if (dst != 0 && (templ2 & 0xffff0000) == 0)
                {
                        DX = (uint16_t)(templ % dst);
                        AX = (uint16_t)((templ / dst) & 0xffff);
                        if (cpu_c.cpu_iscyrix == 0)
                                setznp16(AX); /*Not a Cyrix*/
                }
                else
                {
                        x86_int(0);
                        return 1;
                }
                CLOCK_CYCLES((is486 != 0 && cpu_c.cpu_iscyrix == 0) ? 24 : 22);
                PREFETCH_RUN((is486 != 0 && cpu_c.cpu_iscyrix == 0) ? 24 : 22, 2, (int)fetchdat,
                             (cpu_mod == 3) ? 0 : 1, 0, 0, 0, 0);
                break;
        case 0x38: /*IDIV AX,w*/
                tempws = (int)((DX << 16) | AX);
                if (dst != 0)
                        tempws2 = tempws / (int)((int16_t)dst);
                temps16 = (int16_t)(tempws2 & 0xffff);
                if ((dst != 0) && ((int)temps16 == tempws2))
                {
                        DX = (uint16_t)(tempws % (int)((int16_t)dst));
                        AX = (uint16_t)(tempws2 & 0xffff);
                        if (cpu_c.cpu_iscyrix == 0)
                                setznp16(AX); /*Not a Cyrix*/
                }
                else
                {
                        x86_int(0);
                        return 1;
                }
                CLOCK_CYCLES(27);
                PREFETCH_RUN(27, 2, (int)fetchdat, (cpu_mod == 3) ? 0 : 1, 0, 0, 0, 0);
                break;

        default:
                x86illegal();
                break;
        }
        return 0;
    }

    /// <summary>pcem: 90 — relevé sur ops_286[] par gdb.</summary>
    private static void PoserNop()
    {
        ops_286[0x90] = opNOP;
    }

    /// <summary>pcem: 98, 99, D6, F4 et 62 — relevés sur ops_286[] par gdb.</summary>
    private static void PoserGroupeMisc()
    {
        ops_286[0x62] = opBOUND_w_a16;
        ops_286[0x98] = opCBW;
        ops_286[0x99] = opCWD;
        ops_286[0xD6] = opSETALC;
        ops_286[0xF4] = opHLT;
        ops_286[0xF6] = opF6_a16;
        ops_286[0xF7] = opF7_w_a16;
    }

    // ---- G2, D2 : les formes 32 bits (_l, _a32) de x86_ops_misc.h ----

    // pcem: x86_ops_misc.h:755
    private static int opBOUND_l_a16(uint32_t fetchdat)
    {
        int32_t low, high;

        if (fetch_ea_16(fetchdat)) return 1;
        if (ILLEGAL_ON(cpu_mod == 3)) return 0;
        if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        low = (int32_t)geteal();
        high = (int32_t)readmeml(easeg, (uint32_t)(cpu_state.eaaddr + 4));
        if (cpu_state.abrt != 0)
                return 1;

        if (((int32_t)cpu_state.regs[cpu_reg].l < low) || ((int32_t)cpu_state.regs[cpu_reg].l > high)) {
                x86_int(5);
                return 1;
        }

        CLOCK_CYCLES(is486 != 0 ? 7 : 10);
        PREFETCH_RUN(is486 != 0 ? 7 : 10, 2, (int)fetchdat, 1, 1, 0, 0, 0);
        return 0;
    }

    // pcem: x86_ops_misc.h:775
    private static int opBOUND_l_a32(uint32_t fetchdat)
    {
        int32_t low, high;

        if (fetch_ea_32(fetchdat)) return 1;
        if (ILLEGAL_ON(cpu_mod == 3)) return 0;
        if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        low = (int32_t)geteal();
        high = (int32_t)readmeml(easeg, (uint32_t)(cpu_state.eaaddr + 4));
        if (cpu_state.abrt != 0)
                return 1;

        if (((int32_t)cpu_state.regs[cpu_reg].l < low) || ((int32_t)cpu_state.regs[cpu_reg].l > high)) {
                x86_int(5);
                return 1;
        }

        CLOCK_CYCLES(is486 != 0 ? 7 : 10);
        PREFETCH_RUN(is486 != 0 ? 7 : 10, 2, (int)fetchdat, 1, 1, 0, 0, 1);
        return 0;
    }

    // pcem: x86_ops_misc.h:734
    private static int opBOUND_w_a32(uint32_t fetchdat)
    {
        int16_t low, high;

        if (fetch_ea_32(fetchdat)) return 1;
        if (ILLEGAL_ON(cpu_mod == 3)) return 0;
        if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        low = (int16_t)geteaw();
        high = (int16_t)readmemw(easeg, (uint32_t)(cpu_state.eaaddr + 2));
        if (cpu_state.abrt != 0)
                return 1;

        if (((int16_t)cpu_state.regs[cpu_reg].w < low) || ((int16_t)cpu_state.regs[cpu_reg].w > high)) {
                x86_int(5);
                return 1;
        }

        CLOCK_CYCLES(is486 != 0 ? 7 : 10);
        PREFETCH_RUN(is486 != 0 ? 7 : 10, 2, (int)fetchdat, 2, 0, 0, 0, 1);
        return 0;
    }

    // pcem: x86_ops_misc.h:21
    private static int opCDQ(uint32_t fetchdat)
    {
        EDX = (EAX & 0x80000000) != 0 ? 0xffffffff : 0;
        CLOCK_CYCLES(2);
        PREFETCH_RUN(2, 1, -1, 0, 0, 0, 0, 0);
        return 0;
    }

    // pcem: x86_ops_misc.h:9
    private static int opCWDE(uint32_t fetchdat)
    {
        EAX = (AX & 0x8000) != 0 ? (0xffff0000 | AX) : AX;
        CLOCK_CYCLES(3);
        PREFETCH_RUN(3, 1, -1, 0, 0, 0, 0, 0);
        return 0;
    }

    // pcem: x86_ops_misc.h — opF6_a32, dérivé de opF6_a16 : seules diffèrent fetch_ea_32 et ea32 = 1
    private static int opF6_a32(uint32_t fetchdat)
    {
        int tempws, tempws2 = 0;
        uint16_t tempw = 0, src16;
        uint8_t src, dst;
        int8_t temps;

        if (fetch_ea_32(fetchdat)) return 1;
        // verbatim : la forme a32 de PCem n'a PAS le CHECK_READ de la forme a16.
        if (cpu_mod != 3)
                if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        dst = geteab();
        if (cpu_state.abrt != 0)
                return 1;
        switch (fetchdat & 0x38)
        {
        case 0x00: /*TEST b,#8*/
        case 0x08:
                src = readmemb(x86.cs, cpu_state.pc);
                cpu_state.pc++;
                if (cpu_state.abrt != 0)
                        return 1;
                setznp8((uint8_t)(src & dst));
                if (is486 != 0)
                        CLOCK_CYCLES((cpu_mod == 3) ? 1 : 2);
                else
                        CLOCK_CYCLES((cpu_mod == 3) ? 2 : 5);
                PREFETCH_RUN((cpu_mod == 3) ? 2 : 5, 3, (int)fetchdat, (cpu_mod == 3) ? 0 : 1, 0, 0, 0, 1);
                break;
        case 0x10: /*NOT b*/
                if (cpu_mod != 3)
                        if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
                seteab((uint8_t)~dst);
                if (cpu_state.abrt != 0)
                        return 1;
                CLOCK_CYCLES((cpu_mod == 3) ? cpu_c.timing_rr : cpu_c.timing_mm);
                PREFETCH_RUN((cpu_mod == 3) ? cpu_c.timing_rr : cpu_c.timing_mm, 2, (int)fetchdat,
                             (cpu_mod == 3) ? 0 : 1, 0, (cpu_mod == 3) ? 0 : 1, 0, 1);
                break;
        case 0x18: /*NEG b*/
                if (cpu_mod != 3)
                        if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
                seteab((uint8_t)(0 - dst));
                if (cpu_state.abrt != 0)
                        return 1;
                setsub8(0, dst);
                CLOCK_CYCLES((cpu_mod == 3) ? cpu_c.timing_rr : cpu_c.timing_mm);
                PREFETCH_RUN((cpu_mod == 3) ? cpu_c.timing_rr : cpu_c.timing_mm, 2, (int)fetchdat,
                             (cpu_mod == 3) ? 0 : 1, 0, (cpu_mod == 3) ? 0 : 1, 0, 1);
                break;
        case 0x20: /*MUL AL,b*/
                AX = (uint16_t)(AL * dst);
                flags_rebuild();
                if (AH != 0)
                        cpu_state.flags |= (C_FLAG | V_FLAG);
                else
                        cpu_state.flags &= unchecked((uint16_t)~(C_FLAG | V_FLAG));
                CLOCK_CYCLES(13);
                PREFETCH_RUN(13, 2, (int)fetchdat, (cpu_mod == 3) ? 0 : 1, 0, 0, 0, 1);
                break;
        case 0x28: /*IMUL AL,b*/
                tempws = (int)((int8_t)AL) * (int)((int8_t)dst);
                AX = (uint16_t)(tempws & 0xffff);
                flags_rebuild();
                if (((int16_t)AX >> 7) != 0 && ((int16_t)AX >> 7) != -1)
                        cpu_state.flags |= (C_FLAG | V_FLAG);
                else
                        cpu_state.flags &= unchecked((uint16_t)~(C_FLAG | V_FLAG));
                CLOCK_CYCLES(14);
                PREFETCH_RUN(14, 2, (int)fetchdat, (cpu_mod == 3) ? 0 : 1, 0, 0, 0, 1);
                break;
        case 0x30: /*DIV AL,b*/
                src16 = AX;
                if (dst != 0)
                        tempw = (uint16_t)(src16 / dst);
                if (dst != 0 && (tempw & 0xff00) == 0)
                {
                        AH = (uint8_t)(src16 % dst);
                        AL = (uint8_t)((src16 / dst) & 0xff);
                        if (cpu_c.cpu_iscyrix == 0)
                        {
                                flags_rebuild();
                                cpu_state.flags |= 0x8D5; /*Not a Cyrix*/
                                cpu_state.flags &= unchecked((uint16_t)~1);
                        }
                }
                else
                {
                        x86_int(0);
                        return 1;
                }
                CLOCK_CYCLES((is486 != 0 && cpu_c.cpu_iscyrix == 0) ? 16 : 14);
                PREFETCH_RUN((is486 != 0 && cpu_c.cpu_iscyrix == 0) ? 16 : 14, 2, (int)fetchdat,
                             (cpu_mod == 3) ? 0 : 1, 0, 0, 0, 1);
                break;
        case 0x38: /*IDIV AL,b*/
                tempws = (int)(int16_t)AX;
                if (dst != 0)
                        tempws2 = tempws / (int)((int8_t)dst);
                temps = (int8_t)(tempws2 & 0xff);
                if (dst != 0 && ((int)temps == tempws2))
                {
                        AH = (uint8_t)((tempws % (int)((int8_t)dst)) & 0xff);
                        AL = (uint8_t)(tempws2 & 0xff);
                        if (cpu_c.cpu_iscyrix == 0)
                        {
                                flags_rebuild();
                                cpu_state.flags |= 0x8D5; /*Not a Cyrix*/
                                cpu_state.flags &= unchecked((uint16_t)~1);
                        }
                }
                else
                {
                        x86_int(0);
                        return 1;
                }
                CLOCK_CYCLES(19);
                PREFETCH_RUN(19, 2, (int)fetchdat, (cpu_mod == 3) ? 0 : 1, 0, 0, 0, 1);
                break;

        default:
                x86illegal();
                break;
        }
        return 0;
    }

    // pcem: x86_ops_misc.h:499
    private static int opF7_l_a16(uint32_t fetchdat)
    {
        uint64_t temp64;
        uint32_t src, dst;

        if (fetch_ea_16(fetchdat)) return 1;
        if (cpu_mod != 3)
                if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        dst = geteal();
        if (cpu_state.abrt != 0)
                return 1;

        switch (fetchdat & 0x38) {
        case 0x00: /*TEST l*/
        case 0x08:
                src = getlong();
                if (cpu_state.abrt != 0)
                        return 1;
                setznp32(src & dst);
                if (is486 != 0)
                        CLOCK_CYCLES((cpu_mod == 3) ? 1 : 2);
                else
                        CLOCK_CYCLES((cpu_mod == 3) ? 2 : 5);
                PREFETCH_RUN((cpu_mod == 3) ? 2 : 5, 5, (int)fetchdat, 0, (cpu_mod == 3) ? 0 : 1, 0, 0, 0);
                break;
        case 0x10: /*NOT l*/
                if (cpu_mod != 3)
                        if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
                seteal(~dst);
                if (cpu_state.abrt != 0)
                        return 1;
                CLOCK_CYCLES((cpu_mod == 3) ? cpu_c.timing_rr : cpu_c.timing_mml);
                PREFETCH_RUN((cpu_mod == 3) ? cpu_c.timing_rr : cpu_c.timing_mm, 2, (int)fetchdat, 0, (cpu_mod == 3) ? 0 : 1, 0,
                             (cpu_mod == 3) ? 0 : 1, 0);
                break;
        case 0x18: /*NEG l*/
                if (cpu_mod != 3)
                        if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
                seteal(0 - dst);
                if (cpu_state.abrt != 0)
                        return 1;
                setsub32(0, dst);
                CLOCK_CYCLES((cpu_mod == 3) ? cpu_c.timing_rr : cpu_c.timing_mml);
                PREFETCH_RUN((cpu_mod == 3) ? cpu_c.timing_rr : cpu_c.timing_mm, 2, (int)fetchdat, 0, (cpu_mod == 3) ? 0 : 1, 0,
                             (cpu_mod == 3) ? 0 : 1, 0);
                break;
        case 0x20: /*MUL EAX,l*/
                temp64 = (uint64_t)EAX * (uint64_t)dst;
                EAX = (uint32_t)(temp64 & 0xffffffff);
                EDX = (uint32_t)(temp64 >> 32);
                flags_rebuild();
                if ((EDX) != 0)
                        cpu_state.flags |= (C_FLAG | V_FLAG);
                else
                        cpu_state.flags &= unchecked((uint16_t)~((C_FLAG | V_FLAG)));
                CLOCK_CYCLES(21);
                PREFETCH_RUN(21, 2, (int)fetchdat, 0, (cpu_mod == 3) ? 0 : 1, 0, 0, 0);
                break;
        case 0x28: /*IMUL EAX,l*/
                temp64 = (uint64_t)((int64_t)(int32_t)EAX * (int64_t)(int32_t)dst);
                EAX = (uint32_t)(temp64 & 0xffffffff);
                EDX = (uint32_t)(temp64 >> 32);
                flags_rebuild();
                if (((int64_t)temp64 >> 31) != 0 && ((int64_t)temp64 >> 31) != -1)
                        cpu_state.flags |= (C_FLAG | V_FLAG);
                else
                        cpu_state.flags &= unchecked((uint16_t)~((C_FLAG | V_FLAG)));
                CLOCK_CYCLES(38);
                PREFETCH_RUN(38, 2, (int)fetchdat, 0, (cpu_mod == 3) ? 0 : 1, 0, 0, 0);
                break;
        case 0x30: /*DIV EAX,l*/
                if (divl(dst) != 0)
                        return 1;
                if (cpu_c.cpu_iscyrix == 0)
                        setznp32(EAX); /*Not a Cyrix*/
                CLOCK_CYCLES((is486 != 0) ? 40 : 38);
                PREFETCH_RUN(is486 != 0 ? 40 : 38, 2, (int)fetchdat, 0, (cpu_mod == 3) ? 0 : 1, 0, 0, 0);
                break;
        case 0x38: /*IDIV EAX,l*/
                if (idivl((int32_t)dst) != 0)
                        return 1;
                if (cpu_c.cpu_iscyrix == 0)
                        setznp32(EAX); /*Not a Cyrix*/
                CLOCK_CYCLES(43);
                PREFETCH_RUN(43, 2, (int)fetchdat, 0, (cpu_mod == 3) ? 0 : 1, 0, 0, 0);
                break;

        default:
                // omitted: pclog — sortie de diagnostic.
                x86illegal();
                break;
        }
        return 0;
    }

    // pcem: x86_ops_misc.h:591
    private static int opF7_l_a32(uint32_t fetchdat)
    {
        uint64_t temp64;
        uint32_t src, dst;

        if (fetch_ea_32(fetchdat)) return 1;
        if (cpu_mod != 3)
                if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        dst = geteal();
        if (cpu_state.abrt != 0)
                return 1;

        switch (fetchdat & 0x38) {
        case 0x00: /*TEST l*/
        case 0x08:
                src = getlong();
                if (cpu_state.abrt != 0)
                        return 1;
                setznp32(src & dst);
                if (is486 != 0)
                        CLOCK_CYCLES((cpu_mod == 3) ? 1 : 2);
                else
                        CLOCK_CYCLES((cpu_mod == 3) ? 2 : 5);
                PREFETCH_RUN((cpu_mod == 3) ? 2 : 5, 5, (int)fetchdat, 0, (cpu_mod == 3) ? 0 : 1, 0, 0, 1);
                break;
        case 0x10: /*NOT l*/
                if (cpu_mod != 3)
                        if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
                seteal(~dst);
                if (cpu_state.abrt != 0)
                        return 1;
                CLOCK_CYCLES((cpu_mod == 3) ? cpu_c.timing_rr : cpu_c.timing_mml);
                PREFETCH_RUN((cpu_mod == 3) ? cpu_c.timing_rr : cpu_c.timing_mm, 2, (int)fetchdat, 0, (cpu_mod == 3) ? 0 : 1, 0,
                             (cpu_mod == 3) ? 0 : 1, 1);
                break;
        case 0x18: /*NEG l*/
                if (cpu_mod != 3)
                        if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
                seteal(0 - dst);
                if (cpu_state.abrt != 0)
                        return 1;
                setsub32(0, dst);
                CLOCK_CYCLES((cpu_mod == 3) ? cpu_c.timing_rr : cpu_c.timing_mml);
                PREFETCH_RUN((cpu_mod == 3) ? cpu_c.timing_rr : cpu_c.timing_mm, 2, (int)fetchdat, 0, (cpu_mod == 3) ? 0 : 1, 0,
                             (cpu_mod == 3) ? 0 : 1, 1);
                break;
        case 0x20: /*MUL EAX,l*/
                temp64 = (uint64_t)EAX * (uint64_t)dst;
                EAX = (uint32_t)(temp64 & 0xffffffff);
                EDX = (uint32_t)(temp64 >> 32);
                flags_rebuild();
                if ((EDX) != 0)
                        cpu_state.flags |= (C_FLAG | V_FLAG);
                else
                        cpu_state.flags &= unchecked((uint16_t)~((C_FLAG | V_FLAG)));
                CLOCK_CYCLES(21);
                PREFETCH_RUN(21, 2, (int)fetchdat, 0, (cpu_mod == 3) ? 0 : 1, 0, 0, 1);
                break;
        case 0x28: /*IMUL EAX,l*/
                temp64 = (uint64_t)((int64_t)(int32_t)EAX * (int64_t)(int32_t)dst);
                EAX = (uint32_t)(temp64 & 0xffffffff);
                EDX = (uint32_t)(temp64 >> 32);
                flags_rebuild();
                if (((int64_t)temp64 >> 31) != 0 && ((int64_t)temp64 >> 31) != -1)
                        cpu_state.flags |= (C_FLAG | V_FLAG);
                else
                        cpu_state.flags &= unchecked((uint16_t)~((C_FLAG | V_FLAG)));
                CLOCK_CYCLES(38);
                PREFETCH_RUN(38, 2, (int)fetchdat, 0, (cpu_mod == 3) ? 0 : 1, 0, 0, 1);
                break;
        case 0x30: /*DIV EAX,l*/
                if (divl(dst) != 0)
                        return 1;
                if (cpu_c.cpu_iscyrix == 0)
                        setznp32(EAX); /*Not a Cyrix*/
                CLOCK_CYCLES((is486 != 0) ? 40 : 38);
                PREFETCH_RUN(is486 != 0 ? 40 : 38, 2, (int)fetchdat, 0, (cpu_mod == 3) ? 0 : 1, 0, 0, 1);
                break;
        case 0x38: /*IDIV EAX,l*/
                if (idivl((int32_t)dst) != 0)
                        return 1;
                if (cpu_c.cpu_iscyrix == 0)
                        setznp32(EAX); /*Not a Cyrix*/
                CLOCK_CYCLES(43);
                PREFETCH_RUN(43, 2, (int)fetchdat, 0, (cpu_mod == 3) ? 0 : 1, 0, 0, 1);
                break;

        default:
                // omitted: pclog — sortie de diagnostic.
                x86illegal();
                break;
        }
        return 0;
    }

    // pcem: x86_ops_misc.h — opF7_w_a32, dérivé de opF7_w_a16 : seules diffèrent fetch_ea_32 et ea32 = 1
    //
    // COMPARER SES BRANCHES 0x30 ET 0x38 A CELLES DE F6 : la ou F6 fait
    // `flags_rebuild(); flags |= 0x8D5; flags &= ~1;`, F7 fait `setznp16(AX)`.
    // Voir le commentaire de F6 — c'est la difference qui interdit d'en faire
    // deux instanciations d'une meme macro.
    private static int opF7_w_a32(uint32_t fetchdat)
    {
        uint32_t templ, templ2 = 0;
        int tempws, tempws2 = 1; // verbatim : 1 dans la forme a32 de PCem
        int16_t temps16;
        uint16_t src, dst;

        if (fetch_ea_32(fetchdat)) return 1;
        if (cpu_mod != 3)
                if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        dst = geteaw();
        if (cpu_state.abrt != 0)
                return 1;
        switch (fetchdat & 0x38)
        {
        case 0x00: /*TEST w*/
        case 0x08:
                src = getword();
                if (cpu_state.abrt != 0)
                        return 1;
                setznp16((uint16_t)(src & dst));
                if (is486 != 0)
                        CLOCK_CYCLES((cpu_mod == 3) ? 1 : 2);
                else
                        CLOCK_CYCLES((cpu_mod == 3) ? 2 : 5);
                PREFETCH_RUN((cpu_mod == 3) ? 2 : 5, 4, (int)fetchdat, (cpu_mod == 3) ? 0 : 1, 0, 0, 0, 1);
                break;
        case 0x10: /*NOT w*/
                if (cpu_mod != 3)
                        if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
                seteaw((uint16_t)~dst);
                if (cpu_state.abrt != 0)
                        return 1;
                CLOCK_CYCLES((cpu_mod == 3) ? cpu_c.timing_rr : cpu_c.timing_mm);
                PREFETCH_RUN((cpu_mod == 3) ? cpu_c.timing_rr : cpu_c.timing_mm, 2, (int)fetchdat,
                             (cpu_mod == 3) ? 0 : 1, 0, (cpu_mod == 3) ? 0 : 1, 0, 1);
                break;
        case 0x18: /*NEG w*/
                if (cpu_mod != 3)
                        if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
                seteaw((uint16_t)(0 - dst));
                if (cpu_state.abrt != 0)
                        return 1;
                setsub16(0, dst);
                CLOCK_CYCLES((cpu_mod == 3) ? cpu_c.timing_rr : cpu_c.timing_mm);
                PREFETCH_RUN((cpu_mod == 3) ? cpu_c.timing_rr : cpu_c.timing_mm, 2, (int)fetchdat,
                             (cpu_mod == 3) ? 0 : 1, 0, (cpu_mod == 3) ? 0 : 1, 0, 1);
                break;
        case 0x20: /*MUL AX,w*/
                templ = (uint32_t)(AX * dst);
                AX = (uint16_t)(templ & 0xFFFF);
                DX = (uint16_t)(templ >> 16);
                flags_rebuild();
                if (DX != 0)
                        cpu_state.flags |= (C_FLAG | V_FLAG);
                else
                        cpu_state.flags &= unchecked((uint16_t)~(C_FLAG | V_FLAG));
                CLOCK_CYCLES(21);
                PREFETCH_RUN(21, 2, (int)fetchdat, (cpu_mod == 3) ? 0 : 1, 0, 0, 0, 1);
                break;
        case 0x28: /*IMUL AX,w*/
                templ = (uint32_t)((int)((int16_t)AX) * (int)((int16_t)dst));
                AX = (uint16_t)(templ & 0xFFFF);
                DX = (uint16_t)(templ >> 16);
                flags_rebuild();
                if (((int32_t)templ >> 15) != 0 && ((int32_t)templ >> 15) != -1)
                        cpu_state.flags |= (C_FLAG | V_FLAG);
                else
                        cpu_state.flags &= unchecked((uint16_t)~(C_FLAG | V_FLAG));
                CLOCK_CYCLES(22);
                PREFETCH_RUN(22, 2, (int)fetchdat, (cpu_mod == 3) ? 0 : 1, 0, 0, 0, 1);
                break;
        case 0x30: /*DIV AX,w*/
                templ = (uint32_t)((DX << 16) | AX);
                if (dst != 0)
                        templ2 = templ / dst;
                if (dst != 0 && (templ2 & 0xffff0000) == 0)
                {
                        DX = (uint16_t)(templ % dst);
                        AX = (uint16_t)((templ / dst) & 0xffff);
                        if (cpu_c.cpu_iscyrix == 0)
                                setznp16(AX); /*Not a Cyrix*/
                }
                else
                {
                        x86_int(0);
                        return 1;
                }
                CLOCK_CYCLES((is486 != 0 && cpu_c.cpu_iscyrix == 0) ? 24 : 22);
                PREFETCH_RUN((is486 != 0 && cpu_c.cpu_iscyrix == 0) ? 24 : 22, 2, (int)fetchdat,
                             (cpu_mod == 3) ? 0 : 1, 0, 0, 0, 1);
                break;
        case 0x38: /*IDIV AX,w*/
                tempws = (int)((DX << 16) | AX);
                if (dst != 0)
                        tempws2 = tempws / (int)((int16_t)dst);
                temps16 = (int16_t)(tempws2 & 0xffff);
                if ((dst != 0) && ((int)temps16 == tempws2))
                {
                        DX = (uint16_t)(tempws % (int)((int16_t)dst));
                        AX = (uint16_t)(tempws2 & 0xffff);
                        if (cpu_c.cpu_iscyrix == 0)
                                setznp16(AX); /*Not a Cyrix*/
                }
                else
                {
                        x86_int(0);
                        return 1;
                }
                CLOCK_CYCLES(27);
                PREFETCH_RUN(27, 2, (int)fetchdat, (cpu_mod == 3) ? 0 : 1, 0, 0, 0, 1);
                break;

        default:
                x86illegal();
                break;
        }
        return 0;
    }

    // pcem: 386_ops.h — les emplacements de ces handlers dans OP_TABLE(386) et (386_0f).
    private static void PoserGroupe_misc_386()
    {
        ops_386[0x162] = opBOUND_l_a16;
        ops_386[0x198] = opCWDE;
        ops_386[0x199] = opCDQ;
        ops_386[0x1F7] = opF7_l_a16;
        ops_386[0x262] = opBOUND_w_a32;
        ops_386[0x2F6] = opF6_a32;
        ops_386[0x2F7] = opF7_w_a32;
        ops_386[0x362] = opBOUND_l_a32;
        ops_386[0x398] = opCWDE;
        ops_386[0x399] = opCDQ;
        ops_386[0x3F6] = opF6_a32;
        ops_386[0x3F7] = opF7_l_a32;
    }

    // ---- G2, D3 : la table 0F du 386, x86_ops_misc.h ----

    // pcem: x86_ops_misc.h:976
    private static int opCPUID(uint32_t fetchdat)
    {
        if ((CPUID) != 0) {
                cpu_c.cpu_CPUID();
                CLOCK_CYCLES(9);
                return 0;
        }
        cpu_state.pc = cpu_state.oldpc;
        x86illegal();
        return 1;
    }

    // pcem: x86_ops_misc.h:808
    private static int opINVD(uint32_t fetchdat)
    {
        if ((is486) == 0) {
                x86illegal();
                return 1;
        }
        CLOCK_CYCLES(1000);
        CPU_BLOCK_END();
        return 0;
    }


    // pcem: x86_ops_misc.h:817
    private static int opWBINVD(uint32_t fetchdat)
    {
        if ((is486) == 0) {
                x86illegal();
                return 1;
        }
        CLOCK_CYCLES(10000);
        CPU_BLOCK_END();
        return 0;
    }

    // pcem: 386_ops.h — les emplacements de ces handlers dans OP_TABLE(386) et (386_0f).
    private static void PoserGroupe_misc_0f_386()
    {
        ops_386_0f[0x008] = opINVD;
        ops_386_0f[0x009] = opWBINVD;
        ops_386_0f[0x0A2] = opCPUID;
        ops_386_0f[0x108] = opINVD;
        ops_386_0f[0x109] = opWBINVD;
        ops_386_0f[0x1A2] = opCPUID;
        ops_386_0f[0x208] = opINVD;
        ops_386_0f[0x209] = opWBINVD;
        ops_386_0f[0x2A2] = opCPUID;
        ops_386_0f[0x308] = opINVD;
        ops_386_0f[0x309] = opWBINVD;
        ops_386_0f[0x3A2] = opCPUID;
    }
}
