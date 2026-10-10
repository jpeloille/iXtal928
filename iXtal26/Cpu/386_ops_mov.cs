// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/includes/private/cpu/x86_ops_mov.h  (lignes 3-99)
// SHA256: voir oracle.tsv ; vérifier avec tools/check-oracle.sh
// STATUS: complete pour ops_286 — les VINGT-SIX handlers du groupe.
//
// LE GROUPE COMPTE 26 HANDLERS, PAS 28. Relevé sur la table de la .so par gdb et
// non sur le texte de 386_ops.h : 8C et 8E (MOV r/m,seg et MOV seg,r/m) portent
// bien un nom en opMOV*, mais ils vivent dans x86_ops_mov_seg.h, un autre groupe,
// et passent par loadseg. A2.2c annonçait 28 ici ; c'était une confusion entre les
// deux en-têtes, corrigée à A2.2d.
//
// DEUX TRANCHES, ET POURQUOI DANS CET ORDRE.
//
// A2.2c — les seize B0 à BF, `MOV reg, imm`. Ni modrm ni adresse effective : ils
// lisent leur immédiat dans fetchdat et écrivent un registre. Ne dépendant que de
// ce qui existait déjà, ils ont porté SEULS la première mise en service du fuzzeur
// sur le cœur 286.
//
// A2.2d — les dix autres, qui réclamaient fetch_ea_16, eal_r/eal_w, la famille
// geteab et les gardes. Machinerie neuve, donc passage séparé.
//
// LE MODÈLE DE TEMPS EST DANS LA BOUCLE, PAS DANS LE HANDLER. CLOCK_CYCLES facture
// timing_rr (2 sur un 286) ; PREFETCH_RUN facture le rechargement de la file
// d'instruction. Le second n'était pas armé jusqu'à A2.2.0 : cpu_prefetch_cycles
// valait zéro des deux côtés, et le modèle ne tournait pas.

using static iXtal26.Cpu._386_common;
using static iXtal26.Cpu.x86;

namespace iXtal26.Cpu;

internal static partial class _386
{
    // pcem: x86_ops_mov.h:3-50 — MOV r8, imm8. Deux octets, aucun accès mémoire :
    // l'immédiat est déjà dans fetchdat, que la boucle a décalé de 8 bits.
    //
    // getbytef() est une MACRO en deux temps : une expression, puis `pc++`. On la
    // déplie littéralement plutôt que d'en faire une fonction — une fonction C#
    // devrait muter pc par effet de bord et se lirait moins bien que le C.

    private static int opMOV_AL_imm(uint32_t fetchdat)
    {
        AL = (uint8_t)fetchdat; cpu_state.pc++;
        CLOCK_CYCLES(cpu_c.timing_rr);
        PREFETCH_RUN(cpu_c.timing_rr, 2, -1, 0, 0, 0, 0, 0);
        return 0;
    }

    private static int opMOV_AH_imm(uint32_t fetchdat)
    {
        AH = (uint8_t)fetchdat; cpu_state.pc++;
        CLOCK_CYCLES(cpu_c.timing_rr);
        PREFETCH_RUN(cpu_c.timing_rr, 2, -1, 0, 0, 0, 0, 0);
        return 0;
    }

    private static int opMOV_BL_imm(uint32_t fetchdat)
    {
        BL = (uint8_t)fetchdat; cpu_state.pc++;
        CLOCK_CYCLES(cpu_c.timing_rr);
        PREFETCH_RUN(cpu_c.timing_rr, 2, -1, 0, 0, 0, 0, 0);
        return 0;
    }

    private static int opMOV_BH_imm(uint32_t fetchdat)
    {
        BH = (uint8_t)fetchdat; cpu_state.pc++;
        CLOCK_CYCLES(cpu_c.timing_rr);
        PREFETCH_RUN(cpu_c.timing_rr, 2, -1, 0, 0, 0, 0, 0);
        return 0;
    }

    private static int opMOV_CL_imm(uint32_t fetchdat)
    {
        CL = (uint8_t)fetchdat; cpu_state.pc++;
        CLOCK_CYCLES(cpu_c.timing_rr);
        PREFETCH_RUN(cpu_c.timing_rr, 2, -1, 0, 0, 0, 0, 0);
        return 0;
    }

    private static int opMOV_CH_imm(uint32_t fetchdat)
    {
        CH = (uint8_t)fetchdat; cpu_state.pc++;
        CLOCK_CYCLES(cpu_c.timing_rr);
        PREFETCH_RUN(cpu_c.timing_rr, 2, -1, 0, 0, 0, 0, 0);
        return 0;
    }

    private static int opMOV_DL_imm(uint32_t fetchdat)
    {
        DL = (uint8_t)fetchdat; cpu_state.pc++;
        CLOCK_CYCLES(cpu_c.timing_rr);
        PREFETCH_RUN(cpu_c.timing_rr, 2, -1, 0, 0, 0, 0, 0);
        return 0;
    }

    private static int opMOV_DH_imm(uint32_t fetchdat)
    {
        DH = (uint8_t)fetchdat; cpu_state.pc++;
        CLOCK_CYCLES(cpu_c.timing_rr);
        PREFETCH_RUN(cpu_c.timing_rr, 2, -1, 0, 0, 0, 0, 0);
        return 0;
    }

    // pcem: x86_ops_mov.h:52-99 — MOV r16, imm16. Trois octets : l'opcode et deux
    // d'immédiat, d'où le `3` passé à PREFETCH_RUN là où les précédents passent `2`.

    private static int opMOV_AX_imm(uint32_t fetchdat)
    {
        AX = (uint16_t)fetchdat; cpu_state.pc += 2;
        CLOCK_CYCLES(cpu_c.timing_rr);
        PREFETCH_RUN(cpu_c.timing_rr, 3, -1, 0, 0, 0, 0, 0);
        return 0;
    }

    private static int opMOV_BX_imm(uint32_t fetchdat)
    {
        BX = (uint16_t)fetchdat; cpu_state.pc += 2;
        CLOCK_CYCLES(cpu_c.timing_rr);
        PREFETCH_RUN(cpu_c.timing_rr, 3, -1, 0, 0, 0, 0, 0);
        return 0;
    }

    private static int opMOV_CX_imm(uint32_t fetchdat)
    {
        CX = (uint16_t)fetchdat; cpu_state.pc += 2;
        CLOCK_CYCLES(cpu_c.timing_rr);
        PREFETCH_RUN(cpu_c.timing_rr, 3, -1, 0, 0, 0, 0, 0);
        return 0;
    }

    private static int opMOV_DX_imm(uint32_t fetchdat)
    {
        DX = (uint16_t)fetchdat; cpu_state.pc += 2;
        CLOCK_CYCLES(cpu_c.timing_rr);
        PREFETCH_RUN(cpu_c.timing_rr, 3, -1, 0, 0, 0, 0, 0);
        return 0;
    }

    private static int opMOV_SI_imm(uint32_t fetchdat)
    {
        SI = (uint16_t)fetchdat; cpu_state.pc += 2;
        CLOCK_CYCLES(cpu_c.timing_rr);
        PREFETCH_RUN(cpu_c.timing_rr, 3, -1, 0, 0, 0, 0, 0);
        return 0;
    }

    private static int opMOV_DI_imm(uint32_t fetchdat)
    {
        DI = (uint16_t)fetchdat; cpu_state.pc += 2;
        CLOCK_CYCLES(cpu_c.timing_rr);
        PREFETCH_RUN(cpu_c.timing_rr, 3, -1, 0, 0, 0, 0, 0);
        return 0;
    }

    private static int opMOV_BP_imm(uint32_t fetchdat)
    {
        BP = (uint16_t)fetchdat; cpu_state.pc += 2;
        CLOCK_CYCLES(cpu_c.timing_rr);
        PREFETCH_RUN(cpu_c.timing_rr, 3, -1, 0, 0, 0, 0, 0);
        return 0;
    }

    private static int opMOV_SP_imm(uint32_t fetchdat)
    {
        SP = (uint16_t)fetchdat; cpu_state.pc += 2;
        CLOCK_CYCLES(cpu_c.timing_rr);
        PREFETCH_RUN(cpu_c.timing_rr, 3, -1, 0, 0, 0, 0, 0);
        return 0;
    }

    // -----------------------------------------------------------------------
    // A2.2d — LES DIX AVEC ADRESSE EFFECTIVE (x86_ops_mov.h).
    //
    // Deux remarques qui valent pour tous :
    //
    // 1. `cpu_mod == 3` désigne un REGISTRE : aucun accès mémoire, aucune garde, et
    //    le coût tombe à timing_rr. C'est la moitié des cas, et c'est celle que les
    //    gardes ne traversent pas.
    //
    // 2. Les macros du C portent un `return` pour leur APPELANT — fetch_ea_16,
    //    SEG_CHECK_*, CHECK_*, ILLEGAL_ON. Une méthode C# ne peut pas faire sortir
    //    son appelant : elles rendent `true`, et le site d'appel écrit le `return`.
    //    Même arbre de décision, dit explicitement.
    //
    // `is486` est nul ici, mais l'expression est gardée telle quelle : c'est le C.
    // -----------------------------------------------------------------------

    // pcem: x86_ops_mov.h:449 — MOV r/m8, r8
    private static int opMOV_b_r_a16(uint32_t fetchdat)
    {
        if (fetch_ea_16(fetchdat)) return 1;
        if (cpu_mod == 3)
        {
                setr8(cpu_rm, getr8(cpu_reg));
                CLOCK_CYCLES(cpu_c.timing_rr);
                PREFETCH_RUN(cpu_c.timing_rr, 2, (int)fetchdat, 0, 0, 0, 0, 0);
        }
        else
        {
                if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
                if (CHECK_WRITE(cpu_state.ea_seg!, cpu_state.eaaddr, cpu_state.eaaddr)) return 1;
                seteab(getr8(cpu_reg));
                CLOCK_CYCLES(is486 != 0 ? 1 : 2);
                PREFETCH_RUN(2, 2, (int)fetchdat, 0, 0, 1, 0, 0);
        }
        return cpu_state.abrt;
    }

    // pcem: x86_ops_mov.h:464 — MOV r/m16, r16
    private static int opMOV_w_r_a16(uint32_t fetchdat)
    {
        if (fetch_ea_16(fetchdat)) return 1;
        if (cpu_mod == 3)
        {
                cpu_state.regs[cpu_rm].w = cpu_state.regs[cpu_reg].w;
                CLOCK_CYCLES(cpu_c.timing_rr);
                PREFETCH_RUN(cpu_c.timing_rr, 2, (int)fetchdat, 0, 0, 0, 0, 0);
        }
        else
        {
                if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
                if (CHECK_WRITE(cpu_state.ea_seg!, cpu_state.eaaddr, cpu_state.eaaddr + 1)) return 1;
                seteaw(cpu_state.regs[cpu_reg].w);
                CLOCK_CYCLES(is486 != 0 ? 1 : 2);
                PREFETCH_RUN(2, 2, (int)fetchdat, 0, 0, 1, 0, 0);
        }
        return cpu_state.abrt;
    }

    // pcem: x86_ops_mov.h:540 — MOV r8, r/m8
    private static int opMOV_r_b_a16(uint32_t fetchdat)
    {
        if (fetch_ea_16(fetchdat)) return 1;
        if (cpu_mod == 3)
        {
                setr8(cpu_reg, getr8(cpu_rm));
                CLOCK_CYCLES(cpu_c.timing_rr);
                PREFETCH_RUN(cpu_c.timing_rr, 2, (int)fetchdat, 0, 0, 0, 0, 0);
        }
        else
        {
                uint8_t temp;
                if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
                if (CHECK_READ(cpu_state.ea_seg!, cpu_state.eaaddr, cpu_state.eaaddr)) return 1;
                temp = geteab();
                if (cpu_state.abrt != 0)
                        return 1;
                setr8(cpu_reg, temp);
                CLOCK_CYCLES(is486 != 0 ? 1 : 4);
                PREFETCH_RUN(4, 2, (int)fetchdat, 1, 0, 0, 0, 0);
        }
        return 0;
    }

    // pcem: x86_ops_mov.h:556 — MOV r16, r/m16
    private static int opMOV_r_w_a16(uint32_t fetchdat)
    {
        if (fetch_ea_16(fetchdat)) return 1;
        if (cpu_mod == 3)
        {
                cpu_state.regs[cpu_reg].w = cpu_state.regs[cpu_rm].w;
                CLOCK_CYCLES(cpu_c.timing_rr);
                PREFETCH_RUN(cpu_c.timing_rr, 2, (int)fetchdat, 0, 0, 0, 0, 0);
        }
        else
        {
                uint16_t temp;
                if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
                if (CHECK_READ(cpu_state.ea_seg!, cpu_state.eaaddr, cpu_state.eaaddr + 1)) return 1;
                temp = geteaw();
                if (cpu_state.abrt != 0)
                        return 1;
                cpu_state.regs[cpu_reg].w = temp;
                CLOCK_CYCLES(is486 != 0 ? 1 : 4);
                PREFETCH_RUN(4, 2, (int)fetchdat, 1, 0, 0, 0, 0);
        }
        return 0;
    }

    // pcem: x86_ops_mov.h:256-342 — les MOFFS : adresse DIRECTE sur deux octets,
    // sans ModRM. D'où le -1 passé à PREFETCH_RUN, et l'absence de fetch_ea.

    private static int opMOV_AL_a16(uint32_t fetchdat)
    {
        uint16_t addr = (uint16_t)fetchdat; cpu_state.pc += 2;
        uint8_t temp;
        if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        if (CHECK_READ(cpu_state.ea_seg!, addr, addr)) return 1;
        temp = readmemb(cpu_state.ea_seg!.@base, addr);
        if (cpu_state.abrt != 0)
                return 1;
        AL = temp;
        CLOCK_CYCLES(is486 != 0 ? 1 : 4);
        PREFETCH_RUN(4, 3, -1, 1, 0, 0, 0, 0);
        return 0;
    }

    private static int opMOV_AX_a16(uint32_t fetchdat)
    {
        uint16_t addr = (uint16_t)fetchdat; cpu_state.pc += 2;
        uint16_t temp;
        if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        if (CHECK_READ(cpu_state.ea_seg!, addr, (uint32_t)(addr + 1))) return 1;
        temp = readmemw(cpu_state.ea_seg!.@base, addr);
        if (cpu_state.abrt != 0)
                return 1;
        AX = temp;
        CLOCK_CYCLES(is486 != 0 ? 1 : 4);
        PREFETCH_RUN(4, 3, -1, 1, 0, 0, 0, 0);
        return 0;
    }

    private static int opMOV_a16_AL(uint32_t fetchdat)
    {
        uint16_t addr = (uint16_t)fetchdat; cpu_state.pc += 2;
        if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
        writememb(cpu_state.ea_seg!.@base, addr, AL);
        CLOCK_CYCLES(is486 != 0 ? 1 : 2);
        PREFETCH_RUN(2, 3, -1, 0, 0, 1, 0, 0);
        return cpu_state.abrt;
    }

    private static int opMOV_a16_AX(uint32_t fetchdat)
    {
        uint16_t addr = (uint16_t)fetchdat; cpu_state.pc += 2;
        if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
        writememw(cpu_state.ea_seg!.@base, addr, AX);
        CLOCK_CYCLES(is486 != 0 ? 1 : 2);
        PREFETCH_RUN(2, 3, -1, 0, 0, 1, 0, 0);
        return cpu_state.abrt;
    }

    // pcem: x86_ops_mov.h:174-205 — MOV r/m, imm. ILLEGAL_ON mord dès que le champ
    // `reg` du ModRM est non nul : l'encodage n'en prévoit qu'une forme, /0.

    private static int opMOV_b_imm_a16(uint32_t fetchdat)
    {
        uint8_t temp;
        if (fetch_ea_16(fetchdat)) return 1;
        if (ILLEGAL_ON((fetchdat & 0x38) != 0)) return 0;
        if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
        temp = readmemb(cs, cpu_state.pc);
        cpu_state.pc++;
        if (cpu_state.abrt != 0)
                return 1;
        if (CHECK_WRITE(cpu_state.ea_seg!, cpu_state.eaaddr, cpu_state.eaaddr)) return 1;
        seteab(temp);
        CLOCK_CYCLES(cpu_c.timing_rr);
        PREFETCH_RUN(cpu_c.timing_rr, 3, (int)fetchdat, 0, 0, cpu_mod == 3 ? 1 : 0, 0, 0);
        return cpu_state.abrt;
    }

    private static int opMOV_w_imm_a16(uint32_t fetchdat)
    {
        uint16_t temp;
        if (fetch_ea_16(fetchdat)) return 1;
        if (ILLEGAL_ON((fetchdat & 0x38) != 0)) return 0;
        if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
        temp = getword();
        if (cpu_state.abrt != 0)
                return 1;
        seteaw(temp);
        CLOCK_CYCLES(cpu_c.timing_rr);
        PREFETCH_RUN(cpu_c.timing_rr, 4, (int)fetchdat, 0, 0, cpu_mod == 3 ? 1 : 0, 0, 0);
        return cpu_state.abrt;
    }

    // pcem: x86_ops_mov.h — opLEA_w_a16.
    //
    // IL NE LIT RIEN. C'est la seule instruction à adresse effective de toute la
    // table qui calcule eaaddr puis s'arrête : ni geteaw, ni SEG_CHECK, ni
    // segment. D'où l'ILLEGAL_ON(mod == 3) — sans opérande mémoire il n'y a
    // pas d'adresse à calculer.
    private static int opLEA_w_a16(uint32_t fetchdat)
    {
        if (fetch_ea_16(fetchdat)) return 1;
        if (ILLEGAL_ON(cpu_mod == 3)) return 0;
        cpu_state.regs[cpu_reg].w = (uint16_t)cpu_state.eaaddr;
        CLOCK_CYCLES(cpu_c.timing_rr);
        PREFETCH_RUN(cpu_c.timing_rr, 2, (int)fetchdat, 0, 0, 0, 0, 0);
        return 0;
    }

    // pcem: x86_ops_mov.h — opXLAT_a16.
    //
    // `(BX + AL) & 0xFFFF` : la table ne peut pas déborder du segment, le
    // masquage est dans l'instruction. Et le segment est cpu_state.ea_seg —
    // donc surchargeable par un préfixe, contrairement aux opérations de chaîne
    // dont la destination est figée sur ES.
    private static int opXLAT_a16(uint32_t fetchdat)
    {
        uint32_t addr = (uint32_t)((BX + AL) & 0xFFFF);
        uint8_t temp;

        if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        // pcem bug, fixed in hardware mode: PB-189 — l'octet de la table contre la limite du segment.
        if (materiel.pb_189)
                if (_386_materiel.limite_seg_materiel(cpu_state.ea_seg!, addr, 1)) return 1;
        temp = readmemb(cpu_state.ea_seg!.@base, addr);
        if (cpu_state.abrt != 0)
                return 1;
        AL = temp;
        CLOCK_CYCLES(5);
        PREFETCH_RUN(5, 1, -1, 1, 0, 0, 0, 0);
        return 0;
    }

    /// <summary>Pose les seize dans la table. Les emplacements sont ceux de la .so,
    /// relevés par gdb sur `ops_286[]` plutôt que lus dans le texte de 386_ops.h —
    /// deux tentatives de lecture du texte ont rendu 1008 entrées sur 1024 et donc
    /// des indices décalés de quatre. La table binaire ne ment pas.</summary>
    private static void PoserGroupeMov()
    {
        ops_286[0x8D] = opLEA_w_a16;
        ops_286[0xD7] = opXLAT_a16;

        // B0-B7 : MOV r8, imm8
        ops_286[0xB0] = opMOV_AL_imm;
        ops_286[0xB1] = opMOV_CL_imm;
        ops_286[0xB2] = opMOV_DL_imm;
        ops_286[0xB3] = opMOV_BL_imm;
        ops_286[0xB4] = opMOV_AH_imm;
        ops_286[0xB5] = opMOV_CH_imm;
        ops_286[0xB6] = opMOV_DH_imm;
        ops_286[0xB7] = opMOV_BH_imm;

        // B8-BF : MOV r16, imm16
        ops_286[0xB8] = opMOV_AX_imm;
        ops_286[0xB9] = opMOV_CX_imm;
        ops_286[0xBA] = opMOV_DX_imm;
        ops_286[0xBB] = opMOV_BX_imm;
        ops_286[0xBC] = opMOV_SP_imm;
        ops_286[0xBD] = opMOV_BP_imm;
        ops_286[0xBE] = opMOV_SI_imm;
        ops_286[0xBF] = opMOV_DI_imm;

        // A2.2d — les dix avec adresse effective.
        ops_286[0x88] = opMOV_b_r_a16;
        ops_286[0x89] = opMOV_w_r_a16;
        ops_286[0x8A] = opMOV_r_b_a16;
        ops_286[0x8B] = opMOV_r_w_a16;
        ops_286[0xA0] = opMOV_AL_a16;
        ops_286[0xA1] = opMOV_AX_a16;
        ops_286[0xA2] = opMOV_a16_AL;
        ops_286[0xA3] = opMOV_a16_AX;
        ops_286[0xC6] = opMOV_b_imm_a16;
        ops_286[0xC7] = opMOV_w_imm_a16;

        // 8C et 8E (MOV r/m, seg et MOV seg, r/m) N'Y SONT PAS : ils vivent dans
        // x86_ops_mov_seg.h, un autre groupe, et passent par loadseg. Le groupe
        // `mov` proprement dit compte donc 26 handlers atteignables, pas 28.
    }

    // ---- G2, D2.1 : les formes 32 bits (_l, _a32) de x86_ops_mov.h ----

    // pcem: x86_ops_mov.h:405
    private static int opLEA_l_a16(uint32_t fetchdat)
    {
        if (fetch_ea_16(fetchdat)) return 1;
        if (ILLEGAL_ON(cpu_mod == 3)) return 0;
        cpu_state.regs[cpu_reg].l = cpu_state.eaaddr & 0xffff;
        CLOCK_CYCLES(cpu_c.timing_rr);
        PREFETCH_RUN(cpu_c.timing_rr, 2, (int)fetchdat, 0, 0, 0, 0, 0);
        return 0;
    }

    // pcem: x86_ops_mov.h:413
    private static int opLEA_l_a32(uint32_t fetchdat)
    {
        if (fetch_ea_32(fetchdat)) return 1;
        if (ILLEGAL_ON(cpu_mod == 3)) return 0;
        cpu_state.regs[cpu_reg].l = cpu_state.eaaddr;
        CLOCK_CYCLES(cpu_c.timing_rr);
        PREFETCH_RUN(cpu_c.timing_rr, 2, (int)fetchdat, 0, 0, 0, 0, 1);
        return 0;
    }

    // pcem: x86_ops_mov.h:396
    private static int opLEA_w_a32(uint32_t fetchdat)
    {
        if (fetch_ea_32(fetchdat)) return 1;
        if (ILLEGAL_ON(cpu_mod == 3)) return 0;
        cpu_state.regs[cpu_reg].w = (uint16_t)cpu_state.eaaddr;
        CLOCK_CYCLES(cpu_c.timing_rr);
        PREFETCH_RUN(cpu_c.timing_rr, 2, (int)fetchdat, 0, 0, 0, 0, 1);
        return 0;
    }

    // pcem: x86_ops_mov.h:269
    private static int opMOV_AL_a32(uint32_t fetchdat)
    {
        uint32_t addr = getlong();
        uint8_t temp;
        if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        if (CHECK_READ(cpu_state.ea_seg!, addr, addr)) return 1;
        temp = readmemb(cpu_state.ea_seg!.@base, addr);
        if (cpu_state.abrt != 0)
                return 1;
        AL = temp;
        CLOCK_CYCLES((is486 != 0) ? 1 : 4);
        PREFETCH_RUN(4, 5, -1, 1, 0, 0, 0, 1);
        return 0;
    }

    // pcem: x86_ops_mov.h:295
    private static int opMOV_AX_a32(uint32_t fetchdat)
    {
        uint32_t addr = getlong();
        uint16_t temp;
        if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        if (CHECK_READ(cpu_state.ea_seg!, addr, addr + 1)) return 1;
        temp = readmemw(cpu_state.ea_seg!.@base, addr);
        if (cpu_state.abrt != 0)
                return 1;
        AX = temp;
        CLOCK_CYCLES((is486 != 0) ? 1 : 4);
        PREFETCH_RUN(4, 5, -1, 1, 0, 0, 0, 1);
        return 0;
    }

    // pcem: x86_ops_mov.h:308
    private static int opMOV_EAX_a16(uint32_t fetchdat)
    {
        uint16_t addr = (uint16_t)fetchdat; cpu_state.pc += 2;
        uint32_t temp;
        if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        if (CHECK_READ(cpu_state.ea_seg!, addr, (uint32_t)(addr + 3))) return 1;
        temp = readmeml(cpu_state.ea_seg!.@base, addr);
        if (cpu_state.abrt != 0)
                return 1;
        EAX = temp;
        CLOCK_CYCLES((is486 != 0) ? 1 : 4);
        PREFETCH_RUN(4, 3, -1, 0, 1, 0, 0, 0);
        return 0;
    }

    // pcem: x86_ops_mov.h:321
    private static int opMOV_EAX_a32(uint32_t fetchdat)
    {
        uint32_t addr = getlong();
        uint32_t temp;
        if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        if (CHECK_READ(cpu_state.ea_seg!, addr, addr + 3)) return 1;
        temp = readmeml(cpu_state.ea_seg!.@base, addr);
        if (cpu_state.abrt != 0)
                return 1;
        EAX = temp;
        CLOCK_CYCLES((is486 != 0) ? 1 : 4);
        PREFETCH_RUN(4, 5, -1, 0, 1, 0, 0, 1);
        return 0;
    }

    // pcem: x86_ops_mov.h:101
    private static int opMOV_EAX_imm(uint32_t fetchdat)
    {
        uint32_t templ = getlong();
        if (cpu_state.abrt != 0)
                return 1;
        EAX = templ;
        CLOCK_CYCLES(cpu_c.timing_rr);
        PREFETCH_RUN(cpu_c.timing_rr, 5, -1, 0, 0, 0, 0, 0);
        return 0;
    }

    // pcem: x86_ops_mov.h:155
    private static int opMOV_EBP_imm(uint32_t fetchdat)
    {
        uint32_t templ = getlong();
        if (cpu_state.abrt != 0)
                return 1;
        EBP = templ;
        CLOCK_CYCLES(cpu_c.timing_rr);
        PREFETCH_RUN(cpu_c.timing_rr, 5, -1, 0, 0, 0, 0, 0);
        return 0;
    }

    // pcem: x86_ops_mov.h:110
    private static int opMOV_EBX_imm(uint32_t fetchdat)
    {
        uint32_t templ = getlong();
        if (cpu_state.abrt != 0)
                return 1;
        EBX = templ;
        CLOCK_CYCLES(cpu_c.timing_rr);
        PREFETCH_RUN(cpu_c.timing_rr, 5, -1, 0, 0, 0, 0, 0);
        return 0;
    }

    // pcem: x86_ops_mov.h:119
    private static int opMOV_ECX_imm(uint32_t fetchdat)
    {
        uint32_t templ = getlong();
        if (cpu_state.abrt != 0)
                return 1;
        ECX = templ;
        CLOCK_CYCLES(cpu_c.timing_rr);
        PREFETCH_RUN(cpu_c.timing_rr, 5, -1, 0, 0, 0, 0, 0);
        return 0;
    }

    // pcem: x86_ops_mov.h:146
    private static int opMOV_EDI_imm(uint32_t fetchdat)
    {
        uint32_t templ = getlong();
        if (cpu_state.abrt != 0)
                return 1;
        EDI = templ;
        CLOCK_CYCLES(cpu_c.timing_rr);
        PREFETCH_RUN(cpu_c.timing_rr, 5, -1, 0, 0, 0, 0, 0);
        return 0;
    }

    // pcem: x86_ops_mov.h:128
    private static int opMOV_EDX_imm(uint32_t fetchdat)
    {
        uint32_t templ = getlong();
        if (cpu_state.abrt != 0)
                return 1;
        EDX = templ;
        CLOCK_CYCLES(cpu_c.timing_rr);
        PREFETCH_RUN(cpu_c.timing_rr, 5, -1, 0, 0, 0, 0, 0);
        return 0;
    }

    // pcem: x86_ops_mov.h:137
    private static int opMOV_ESI_imm(uint32_t fetchdat)
    {
        uint32_t templ = getlong();
        if (cpu_state.abrt != 0)
                return 1;
        ESI = templ;
        CLOCK_CYCLES(cpu_c.timing_rr);
        PREFETCH_RUN(cpu_c.timing_rr, 5, -1, 0, 0, 0, 0, 0);
        return 0;
    }

    // pcem: x86_ops_mov.h:164
    private static int opMOV_ESP_imm(uint32_t fetchdat)
    {
        uint32_t templ = getlong();
        if (cpu_state.abrt != 0)
                return 1;
        ESP = templ;
        CLOCK_CYCLES(cpu_c.timing_rr);
        PREFETCH_RUN(cpu_c.timing_rr, 5, -1, 0, 0, 0, 0, 0);
        return 0;
    }

    // pcem: x86_ops_mov.h:369
    private static int opMOV_a16_EAX(uint32_t fetchdat)
    {
        uint16_t addr = (uint16_t)fetchdat; cpu_state.pc += 2;
        if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
        writememl(cpu_state.ea_seg!.@base, addr, EAX);
        CLOCK_CYCLES((is486 != 0) ? 1 : 2);
        PREFETCH_RUN(2, 3, -1, 0, 0, 0, 1, 0);
        return cpu_state.abrt;
    }

    // pcem: x86_ops_mov.h:343
    private static int opMOV_a32_AL(uint32_t fetchdat)
    {
        uint32_t addr = getlong();
        if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
        writememb(cpu_state.ea_seg!.@base, addr, AL);
        CLOCK_CYCLES((is486 != 0) ? 1 : 2);
        PREFETCH_RUN(2, 5, -1, 0, 0, 1, 0, 1);
        return cpu_state.abrt;
    }

    // pcem: x86_ops_mov.h:359
    private static int opMOV_a32_AX(uint32_t fetchdat)
    {
        uint32_t addr = getlong();
        if (cpu_state.abrt != 0)
                return 1;
        if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
        writememw(cpu_state.ea_seg!.@base, addr, AX);
        CLOCK_CYCLES((is486 != 0) ? 1 : 2);
        PREFETCH_RUN(2, 5, -1, 0, 0, 1, 0, 1);
        return cpu_state.abrt;
    }

    // pcem: x86_ops_mov.h:377
    private static int opMOV_a32_EAX(uint32_t fetchdat)
    {
        uint32_t addr = getlong();
        if (cpu_state.abrt != 0)
                return 1;
        if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
        writememl(cpu_state.ea_seg!.@base, addr, EAX);
        CLOCK_CYCLES((is486 != 0) ? 1 : 2);
        PREFETCH_RUN(2, 5, -1, 0, 0, 0, 1, 1);
        return cpu_state.abrt;
    }

    // pcem: x86_ops_mov.h:189
    private static int opMOV_b_imm_a32(uint32_t fetchdat)
    {
        uint8_t temp;
        if (fetch_ea_32(fetchdat)) return 1;
        if (ILLEGAL_ON((fetchdat & 0x38) != 0)) return 0;
        if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
        temp = getbyte();
        if (cpu_state.abrt != 0)
                return 1;
        seteab(temp);
        CLOCK_CYCLES(cpu_c.timing_rr);
        PREFETCH_RUN(cpu_c.timing_rr, 3, (int)fetchdat, 0, 0, (cpu_mod == 3) ? 1 : 0, 0, 1);
        return cpu_state.abrt;
    }

    // pcem: x86_ops_mov.h:464
    private static int opMOV_b_r_a32(uint32_t fetchdat)
    {
        if (fetch_ea_32(fetchdat)) return 1;
        if (cpu_mod == 3) {
                setr8(cpu_rm, getr8(cpu_reg));
                CLOCK_CYCLES(cpu_c.timing_rr);
                PREFETCH_RUN(cpu_c.timing_rr, 2, (int)fetchdat, 0, 0, 0, 0, 1);
        } else {
                if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
                if (CHECK_WRITE(cpu_state.ea_seg!, cpu_state.eaaddr, cpu_state.eaaddr)) return 1;
                seteab(getr8(cpu_reg));
                CLOCK_CYCLES(is486 != 0 ? 1 : 2);
                PREFETCH_RUN(2, 2, (int)fetchdat, 0, 0, 1, 0, 1);
        }
        return cpu_state.abrt;
    }

    // pcem: x86_ops_mov.h:229
    private static int opMOV_l_imm_a16(uint32_t fetchdat)
    {
        uint32_t temp;
        if (fetch_ea_16(fetchdat)) return 1;
        if (ILLEGAL_ON((fetchdat & 0x38) != 0)) return 0;
        if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
        temp = getlong();
        if (cpu_state.abrt != 0)
                return 1;
        seteal(temp);
        CLOCK_CYCLES(cpu_c.timing_rr);
        PREFETCH_RUN(cpu_c.timing_rr, 6, (int)fetchdat, 0, 0, 0, (cpu_mod == 3) ? 1 : 0, 0);
        return cpu_state.abrt;
    }

    // pcem: x86_ops_mov.h:242
    private static int opMOV_l_imm_a32(uint32_t fetchdat)
    {
        uint32_t temp;
        if (fetch_ea_32(fetchdat)) return 1;
        if (ILLEGAL_ON((fetchdat & 0x38) != 0)) return 0;
        if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
        temp = getlong();
        if (cpu_state.abrt != 0)
                return 1;
        seteal(temp);
        CLOCK_CYCLES(cpu_c.timing_rr);
        PREFETCH_RUN(cpu_c.timing_rr, 6, (int)fetchdat, 0, 0, 0, (cpu_mod == 3) ? 1 : 0, 1);
        return cpu_state.abrt;
    }

    // pcem: x86_ops_mov.h:509
    private static int opMOV_l_r_a16(uint32_t fetchdat)
    {
        if (fetch_ea_16(fetchdat)) return 1;
        if (cpu_mod == 3) {
                cpu_state.regs[cpu_rm].l = cpu_state.regs[cpu_reg].l;
                CLOCK_CYCLES(cpu_c.timing_rr);
                PREFETCH_RUN(cpu_c.timing_rr, 2, (int)fetchdat, 0, 0, 0, 0, 0);
        } else {
                if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
                if (CHECK_WRITE(cpu_state.ea_seg!, cpu_state.eaaddr, cpu_state.eaaddr + 3)) return 1;
                seteal(cpu_state.regs[cpu_reg].l);
                CLOCK_CYCLES(is486 != 0 ? 1 : 2);
                PREFETCH_RUN(2, 2, (int)fetchdat, 0, 0, 0, 1, 0);
        }
        return cpu_state.abrt;
    }

    // pcem: x86_ops_mov.h:524
    private static int opMOV_l_r_a32(uint32_t fetchdat)
    {
        if (fetch_ea_32(fetchdat)) return 1;
        if (cpu_mod == 3) {
                cpu_state.regs[cpu_rm].l = cpu_state.regs[cpu_reg].l;
                CLOCK_CYCLES(cpu_c.timing_rr);
                PREFETCH_RUN(cpu_c.timing_rr, 2, (int)fetchdat, 0, 0, 0, 0, 1);
        } else {
                if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
                if (CHECK_WRITE(cpu_state.ea_seg!, cpu_state.eaaddr, cpu_state.eaaddr + 3)) return 1;
                seteal(cpu_state.regs[cpu_reg].l);
                CLOCK_CYCLES(is486 != 0 ? 1 : 2);
                PREFETCH_RUN(2, 2, (int)fetchdat, 0, 0, 0, 1, 1);
        }
        return cpu_state.abrt;
    }

    // pcem: x86_ops_mov.h:559
    private static int opMOV_r_b_a32(uint32_t fetchdat)
    {
        if (fetch_ea_32(fetchdat)) return 1;
        if (cpu_mod == 3) {
                setr8(cpu_reg, getr8(cpu_rm));
                CLOCK_CYCLES(cpu_c.timing_rr);
                PREFETCH_RUN(cpu_c.timing_rr, 2, (int)fetchdat, 0, 0, 0, 0, 1);
        } else {
                uint8_t temp;
                if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
                if (CHECK_READ(cpu_state.ea_seg!, cpu_state.eaaddr, cpu_state.eaaddr)) return 1;
                temp = geteab();
                if (cpu_state.abrt != 0)
                        return 1;
                setr8(cpu_reg, temp);
                CLOCK_CYCLES(is486 != 0 ? 1 : 4);
                PREFETCH_RUN(4, 2, (int)fetchdat, 1, 0, 0, 0, 1);
        }
        return 0;
    }

    // pcem: x86_ops_mov.h:616
    private static int opMOV_r_l_a16(uint32_t fetchdat)
    {
        if (fetch_ea_16(fetchdat)) return 1;
        if (cpu_mod == 3) {
                cpu_state.regs[cpu_reg].l = cpu_state.regs[cpu_rm].l;
                CLOCK_CYCLES(cpu_c.timing_rr);
                PREFETCH_RUN(cpu_c.timing_rr, 2, (int)fetchdat, 0, 0, 0, 0, 0);
        } else {
                uint32_t temp;
                if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
                if (CHECK_READ(cpu_state.ea_seg!, cpu_state.eaaddr, cpu_state.eaaddr + 3)) return 1;
                temp = geteal();
                if (cpu_state.abrt != 0)
                        return 1;
                cpu_state.regs[cpu_reg].l = temp;
                CLOCK_CYCLES(is486 != 0 ? 1 : 4);
                PREFETCH_RUN(4, 2, (int)fetchdat, 0, 1, 0, 0, 0);
        }
        return 0;
    }

    // pcem: x86_ops_mov.h:635
    private static int opMOV_r_l_a32(uint32_t fetchdat)
    {
        if (fetch_ea_32(fetchdat)) return 1;
        if (cpu_mod == 3) {
                cpu_state.regs[cpu_reg].l = cpu_state.regs[cpu_rm].l;
                CLOCK_CYCLES(cpu_c.timing_rr);
                PREFETCH_RUN(cpu_c.timing_rr, 2, (int)fetchdat, 0, 0, 0, 0, 1);
        } else {
                uint32_t temp;
                if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
                if (CHECK_READ(cpu_state.ea_seg!, cpu_state.eaaddr, cpu_state.eaaddr + 3)) return 1;
                temp = geteal();
                if (cpu_state.abrt != 0)
                        return 1;
                cpu_state.regs[cpu_reg].l = temp;
                CLOCK_CYCLES(is486 != 0 ? 1 : 4);
                PREFETCH_RUN(4, 2, (int)fetchdat, 0, 1, 0, 0, 1);
        }
        return 0;
    }

    // pcem: x86_ops_mov.h:597
    private static int opMOV_r_w_a32(uint32_t fetchdat)
    {
        if (fetch_ea_32(fetchdat)) return 1;
        if (cpu_mod == 3) {
                cpu_state.regs[cpu_reg].w = cpu_state.regs[cpu_rm].w;
                CLOCK_CYCLES(cpu_c.timing_rr);
                PREFETCH_RUN(cpu_c.timing_rr, 2, (int)fetchdat, 0, 0, 0, 0, 1);
        } else {
                uint16_t temp;
                if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
                if (CHECK_READ(cpu_state.ea_seg!, cpu_state.eaaddr, cpu_state.eaaddr + 1)) return 1;
                temp = geteaw();
                if (cpu_state.abrt != 0)
                        return 1;
                cpu_state.regs[cpu_reg].w = temp;
                CLOCK_CYCLES((is486 != 0) ? 1 : 4);
                PREFETCH_RUN(4, 2, (int)fetchdat, 1, 0, 0, 0, 1);
        }
        return 0;
    }

    // pcem: x86_ops_mov.h:216
    private static int opMOV_w_imm_a32(uint32_t fetchdat)
    {
        uint16_t temp;
        if (fetch_ea_32(fetchdat)) return 1;
        if (ILLEGAL_ON((fetchdat & 0x38) != 0)) return 0;
        if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
        temp = getword();
        if (cpu_state.abrt != 0)
                return 1;
        seteaw(temp);
        CLOCK_CYCLES(cpu_c.timing_rr);
        PREFETCH_RUN(cpu_c.timing_rr, 4, (int)fetchdat, 0, 0, (cpu_mod == 3) ? 1 : 0, 0, 1);
        return cpu_state.abrt;
    }

    // pcem: x86_ops_mov.h:494
    private static int opMOV_w_r_a32(uint32_t fetchdat)
    {
        if (fetch_ea_32(fetchdat)) return 1;
        if (cpu_mod == 3) {
                cpu_state.regs[cpu_rm].w = cpu_state.regs[cpu_reg].w;
                CLOCK_CYCLES(cpu_c.timing_rr);
                PREFETCH_RUN(cpu_c.timing_rr, 2, (int)fetchdat, 0, 0, 0, 0, 1);
        } else {
                if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
                if (CHECK_WRITE(cpu_state.ea_seg!, cpu_state.eaaddr, cpu_state.eaaddr + 1)) return 1;
                seteaw(cpu_state.regs[cpu_reg].w);
                CLOCK_CYCLES(is486 != 0 ? 1 : 2);
                PREFETCH_RUN(2, 2, (int)fetchdat, 0, 0, 1, 0, 1);
        }
        return cpu_state.abrt;
    }

    // pcem: x86_ops_mov.h:435
    private static int opXLAT_a32(uint32_t fetchdat)
    {
        uint32_t addr = EBX + AL;
        uint8_t temp;

        if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        // pcem bug, fixed in hardware mode: PB-189 — l'octet de la table contre la limite du segment.
        if (materiel.pb_189)
                if (_386_materiel.limite_seg_materiel(cpu_state.ea_seg!, addr, 1)) return 1;
        temp = readmemb(cpu_state.ea_seg!.@base, addr);
        if (cpu_state.abrt != 0)
                return 1;
        AL = temp;
        CLOCK_CYCLES(5);
        PREFETCH_RUN(5, 1, -1, 1, 0, 0, 0, 1);
        return 0;
    }

    // pcem: 386_ops.h — les emplacements de ces handlers dans OP_TABLE(386) et (386_0f).
    private static void PoserGroupeMov386()
    {
        ops_386[0x189] = opMOV_l_r_a16;
        ops_386[0x18B] = opMOV_r_l_a16;
        ops_386[0x18D] = opLEA_l_a16;
        ops_386[0x1A1] = opMOV_EAX_a16;
        ops_386[0x1A3] = opMOV_a16_EAX;
        ops_386[0x1B8] = opMOV_EAX_imm;
        ops_386[0x1B9] = opMOV_ECX_imm;
        ops_386[0x1BA] = opMOV_EDX_imm;
        ops_386[0x1BB] = opMOV_EBX_imm;
        ops_386[0x1BC] = opMOV_ESP_imm;
        ops_386[0x1BD] = opMOV_EBP_imm;
        ops_386[0x1BE] = opMOV_ESI_imm;
        ops_386[0x1BF] = opMOV_EDI_imm;
        ops_386[0x1C7] = opMOV_l_imm_a16;
        ops_386[0x288] = opMOV_b_r_a32;
        ops_386[0x289] = opMOV_w_r_a32;
        ops_386[0x28A] = opMOV_r_b_a32;
        ops_386[0x28B] = opMOV_r_w_a32;
        ops_386[0x28D] = opLEA_w_a32;
        ops_386[0x2A0] = opMOV_AL_a32;
        ops_386[0x2A1] = opMOV_AX_a32;
        ops_386[0x2A2] = opMOV_a32_AL;
        ops_386[0x2A3] = opMOV_a32_AX;
        ops_386[0x2C6] = opMOV_b_imm_a32;
        ops_386[0x2C7] = opMOV_w_imm_a32;
        ops_386[0x2D7] = opXLAT_a32;
        ops_386[0x388] = opMOV_b_r_a32;
        ops_386[0x389] = opMOV_l_r_a32;
        ops_386[0x38A] = opMOV_r_b_a32;
        ops_386[0x38B] = opMOV_r_l_a32;
        ops_386[0x38D] = opLEA_l_a32;
        ops_386[0x3A0] = opMOV_AL_a32;
        ops_386[0x3A1] = opMOV_EAX_a32;
        ops_386[0x3A2] = opMOV_a32_AL;
        ops_386[0x3A3] = opMOV_a32_EAX;
        ops_386[0x3B8] = opMOV_EAX_imm;
        ops_386[0x3B9] = opMOV_ECX_imm;
        ops_386[0x3BA] = opMOV_EDX_imm;
        ops_386[0x3BB] = opMOV_EBX_imm;
        ops_386[0x3BC] = opMOV_ESP_imm;
        ops_386[0x3BD] = opMOV_EBP_imm;
        ops_386[0x3BE] = opMOV_ESI_imm;
        ops_386[0x3BF] = opMOV_EDI_imm;
        ops_386[0x3C6] = opMOV_b_imm_a32;
        ops_386[0x3C7] = opMOV_l_imm_a32;
        ops_386[0x3D7] = opXLAT_a32;
    }
}
