// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/includes/private/cpu/x86_ops_mov.h  (lignes 3-99)
// SHA256: voir oracle.tsv ; vérifier avec tools/check-oracle.sh
// STATUS: partial — A2.2c, première tranche : les SEIZE `MOV reg, imm`.
//
// POURQUOI CETTE TRANCHE D'ABORD.
//
// Le groupe `mov` compte 28 handlers parmi les 256 entrées atteignables de
// ops_286 (relevé sur la table de la .so par gdb, pas sur le texte). Seize d'entre
// eux — B0 à BF — n'ont NI modrm NI adresse effective : ils lisent leur immédiat
// dans fetchdat, écrivent un registre, et c'est tout.
//
// Ils ne dépendent donc que de ce qui existe déjà : getbytef/getwordf (A2.2a) et
// CLOCK_CYCLES/PREFETCH_RUN (A2.2b). Les douze autres réclament fetch_ea_16,
// eal_r/eal_w et la famille geteab — machinerie neuve, qui mérite son propre
// passage plutôt que d'être couplée à la première mise en service du fuzzeur sur
// le cœur 286.
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
        CLOCK_CYCLES(cpu.timing_rr);
        PREFETCH_RUN(cpu.timing_rr, 2, -1, 0, 0, 0, 0, 0);
        return 0;
    }

    private static int opMOV_AH_imm(uint32_t fetchdat)
    {
        AH = (uint8_t)fetchdat; cpu_state.pc++;
        CLOCK_CYCLES(cpu.timing_rr);
        PREFETCH_RUN(cpu.timing_rr, 2, -1, 0, 0, 0, 0, 0);
        return 0;
    }

    private static int opMOV_BL_imm(uint32_t fetchdat)
    {
        BL = (uint8_t)fetchdat; cpu_state.pc++;
        CLOCK_CYCLES(cpu.timing_rr);
        PREFETCH_RUN(cpu.timing_rr, 2, -1, 0, 0, 0, 0, 0);
        return 0;
    }

    private static int opMOV_BH_imm(uint32_t fetchdat)
    {
        BH = (uint8_t)fetchdat; cpu_state.pc++;
        CLOCK_CYCLES(cpu.timing_rr);
        PREFETCH_RUN(cpu.timing_rr, 2, -1, 0, 0, 0, 0, 0);
        return 0;
    }

    private static int opMOV_CL_imm(uint32_t fetchdat)
    {
        CL = (uint8_t)fetchdat; cpu_state.pc++;
        CLOCK_CYCLES(cpu.timing_rr);
        PREFETCH_RUN(cpu.timing_rr, 2, -1, 0, 0, 0, 0, 0);
        return 0;
    }

    private static int opMOV_CH_imm(uint32_t fetchdat)
    {
        CH = (uint8_t)fetchdat; cpu_state.pc++;
        CLOCK_CYCLES(cpu.timing_rr);
        PREFETCH_RUN(cpu.timing_rr, 2, -1, 0, 0, 0, 0, 0);
        return 0;
    }

    private static int opMOV_DL_imm(uint32_t fetchdat)
    {
        DL = (uint8_t)fetchdat; cpu_state.pc++;
        CLOCK_CYCLES(cpu.timing_rr);
        PREFETCH_RUN(cpu.timing_rr, 2, -1, 0, 0, 0, 0, 0);
        return 0;
    }

    private static int opMOV_DH_imm(uint32_t fetchdat)
    {
        DH = (uint8_t)fetchdat; cpu_state.pc++;
        CLOCK_CYCLES(cpu.timing_rr);
        PREFETCH_RUN(cpu.timing_rr, 2, -1, 0, 0, 0, 0, 0);
        return 0;
    }

    // pcem: x86_ops_mov.h:52-99 — MOV r16, imm16. Trois octets : l'opcode et deux
    // d'immédiat, d'où le `3` passé à PREFETCH_RUN là où les précédents passent `2`.

    private static int opMOV_AX_imm(uint32_t fetchdat)
    {
        AX = (uint16_t)fetchdat; cpu_state.pc += 2;
        CLOCK_CYCLES(cpu.timing_rr);
        PREFETCH_RUN(cpu.timing_rr, 3, -1, 0, 0, 0, 0, 0);
        return 0;
    }

    private static int opMOV_BX_imm(uint32_t fetchdat)
    {
        BX = (uint16_t)fetchdat; cpu_state.pc += 2;
        CLOCK_CYCLES(cpu.timing_rr);
        PREFETCH_RUN(cpu.timing_rr, 3, -1, 0, 0, 0, 0, 0);
        return 0;
    }

    private static int opMOV_CX_imm(uint32_t fetchdat)
    {
        CX = (uint16_t)fetchdat; cpu_state.pc += 2;
        CLOCK_CYCLES(cpu.timing_rr);
        PREFETCH_RUN(cpu.timing_rr, 3, -1, 0, 0, 0, 0, 0);
        return 0;
    }

    private static int opMOV_DX_imm(uint32_t fetchdat)
    {
        DX = (uint16_t)fetchdat; cpu_state.pc += 2;
        CLOCK_CYCLES(cpu.timing_rr);
        PREFETCH_RUN(cpu.timing_rr, 3, -1, 0, 0, 0, 0, 0);
        return 0;
    }

    private static int opMOV_SI_imm(uint32_t fetchdat)
    {
        SI = (uint16_t)fetchdat; cpu_state.pc += 2;
        CLOCK_CYCLES(cpu.timing_rr);
        PREFETCH_RUN(cpu.timing_rr, 3, -1, 0, 0, 0, 0, 0);
        return 0;
    }

    private static int opMOV_DI_imm(uint32_t fetchdat)
    {
        DI = (uint16_t)fetchdat; cpu_state.pc += 2;
        CLOCK_CYCLES(cpu.timing_rr);
        PREFETCH_RUN(cpu.timing_rr, 3, -1, 0, 0, 0, 0, 0);
        return 0;
    }

    private static int opMOV_BP_imm(uint32_t fetchdat)
    {
        BP = (uint16_t)fetchdat; cpu_state.pc += 2;
        CLOCK_CYCLES(cpu.timing_rr);
        PREFETCH_RUN(cpu.timing_rr, 3, -1, 0, 0, 0, 0, 0);
        return 0;
    }

    private static int opMOV_SP_imm(uint32_t fetchdat)
    {
        SP = (uint16_t)fetchdat; cpu_state.pc += 2;
        CLOCK_CYCLES(cpu.timing_rr);
        PREFETCH_RUN(cpu.timing_rr, 3, -1, 0, 0, 0, 0, 0);
        return 0;
    }

    /// <summary>Pose les seize dans la table. Les emplacements sont ceux de la .so,
    /// relevés par gdb sur `ops_286[]` plutôt que lus dans le texte de 386_ops.h —
    /// deux tentatives de lecture du texte ont rendu 1008 entrées sur 1024 et donc
    /// des indices décalés de quatre. La table binaire ne ment pas.</summary>
    private static void PoserGroupeMov()
    {
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
    }
}
