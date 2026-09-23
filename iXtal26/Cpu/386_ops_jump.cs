// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/includes/private/cpu/x86_ops_jump.h  (lignes 3-351)
// SHA256: voir oracle.tsv ; vérifier avec tools/check-oracle.sh
// STATUS: partial — A5 : les 26 emplacements de saut qu'un 286 atteint, CALL et
//         RET PROCHES compris — ils vivent ici et non dans x86_ops_call.h /
//         x86_ops_ret.h, qui ne portent que les formes lointaines. Les formes
//         `_l` (opJ##cond##_l, opLOOP*_l, opJMP_r32, opRET_l*, opCALL_r32) et
//         `_a32` restent dehors, déclarées au registre des omissions.
//
// LE VERROU. Sans saut conditionnel, aucun BIOS ne boucle — et rien de ce qui
// a été transcrit jusqu'ici n'en dépendait, ce qui explique qu'on ait pu poser
// 110 emplacements sans jamais exercer un seul LECTEUR de drapeau en volume.
//
// C'EST ICI QUE LES DRAPEAUX PARESSEUX SE FONT LIRE. Les seize conditions ne
// touchent pas `cpu_state.flags` : elles appellent VF_SET, CF_SET, ZF_SET,
// NF_SET, PF_SET, qui aiguillent chacune sur `flags_op` et recalculent depuis
// flags_op1/op2/res. Jusqu'à A4 ces six fonctions ne tournaient que sur le
// chemin d'un piège T_FLAG, donc dans les seuls cas dirigés de core286-check.
// À partir d'ici elles sont sur le chemin chaud.
//
// ET LE FUZZEUR NE PEUT TOUJOURS PAS LES VOIR SEUL : en mode simple chaque
// itération repart d'un reset, flags_op vaut FLAGS_UNKNOWN, et les six
// fonctions tombent toutes dans leur dernière branche — celle qui lit
// `cpu_state.flags` directement. Un Jcc qui suit un ADD dans la MÊME itération
// est la seule façon d'exercer les autres branches ; d'où les suites ajoutées
// à core286-check.
//
// DEVIATION: la macro opJ(condition) se paramètre par un fragment de texte
//   collé (`cond_##condition`). En C# la condition devient un délégué rendu
//   par ConditionDe(), et les seize noms sont ceux du C.

using static iXtal26.Cpu._386_common;
using static iXtal26.Cpu.x86;
using static iXtal26.Cpu.x86_flags;

namespace iXtal26.Cpu;

internal static partial class _386
{
    internal delegate bool CondFn();

    /// <summary>pcem: x86_ops_jump.h:3-18 — les seize macros cond_##name, dans
    /// l'ordre où elles y sont écrites, qui est aussi celui des emplacements
    /// 70-7F.
    ///
    /// cond_L et cond_LE comparent NF_SET() et VF_SET() APRÈS les avoir réduits
    /// à 0 ou 1 : `((NF_SET()) ? 1 : 0) != ((VF_SET()) ? 1 : 0)`. C'est
    /// nécessaire en C parce que les deux rendent des entiers non normalisés —
    /// VF_SET peut rendre 0x8000. En C# les six rendent déjà un int qu'on
    /// compare à zéro, mais on garde la réduction explicite : elle dit ce que le
    /// C dit, et la supprimer ferait dépendre le résultat du choix de type.</summary>
    private static readonly CondFn[] conditions =
    [
        () => VF_SET() != 0,                                    // cond_O
        () => VF_SET() == 0,                                    // cond_NO
        () => CF_SET() != 0,                                    // cond_B
        () => CF_SET() == 0,                                    // cond_NB
        () => ZF_SET() != 0,                                    // cond_E
        () => ZF_SET() == 0,                                    // cond_NE
        () => CF_SET() != 0 || ZF_SET() != 0,                   // cond_BE
        () => CF_SET() == 0 && ZF_SET() == 0,                   // cond_NBE
        () => NF_SET() != 0,                                    // cond_S
        () => NF_SET() == 0,                                    // cond_NS
        () => PF_SET() != 0,                                    // cond_P
        () => PF_SET() == 0,                                    // cond_NP
        () => (NF_SET() != 0 ? 1 : 0) != (VF_SET() != 0 ? 1 : 0),                         // cond_L
        () => (NF_SET() != 0 ? 1 : 0) == (VF_SET() != 0 ? 1 : 0),                         // cond_NL
        () => (NF_SET() != 0 ? 1 : 0) != (VF_SET() != 0 ? 1 : 0) || ZF_SET() != 0,        // cond_LE
        () => (NF_SET() != 0 ? 1 : 0) == (VF_SET() != 0 ? 1 : 0) && ZF_SET() == 0,        // cond_NLE
    ];

    /// <summary>pcem: x86_ops_jump.h:20-36 — la macro opJ(condition), forme
    /// courte (déplacement de un octet signé). C'est la seule des trois formes
    /// qu'un 286 atteigne.</summary>
    private static void PoserSautsConditionnels()
    {
        for (var n = 0; n < 16; n++)
        {
                var cond = conditions[n];

                ops_286[0x70 + n] = fetchdat =>
                {
                        int8_t offset = (int8_t)(uint8_t)fetchdat; cpu_state.pc++;   // (int8_t)getbytef()
                        CLOCK_CYCLES(cpu.timing_bnt);
                        if (cond())
                        {
                                cpu_state.pc += (uint32_t)offset;
                                if ((cpu_state.op32 & 0x100) == 0)
                                        cpu_state.pc &= 0xffff;
                                CLOCK_CYCLES_ALWAYS(cpu.timing_bt);
                                CPU_BLOCK_END();
                                PREFETCH_RUN(cpu.timing_bt + cpu.timing_bnt, 2, -1, 0, 0, 0, 0, 0);
                                PREFETCH_FLUSH();
                                return 1;
                        }
                        PREFETCH_RUN(cpu.timing_bnt, 2, -1, 0, 0, 0, 0, 0);
                        return 0;
                };
        }
    }

    // ------------------------------------------------------------ les boucles

    /// <summary>pcem: x86_ops_jump.h — opLOOPNE_w (:93-106), opLOOPE_w (:122-135)
    /// et opLOOP_w (:151-164). Trois fonctions écrites à la main, pas une macro,
    /// et elles ne diffèrent que par leur condition.
    ///
    /// L'ORDRE COMPTE : CX est décrémenté AVANT que la condition ne le lise, et
    /// CLOCK_CYCLES comme PREFETCH_RUN sont facturés AVANT le test — donc aussi
    /// quand la boucle ne saute pas. Ce n'est pas le cas des Jcc, où
    /// PREFETCH_RUN est dans chacune des deux branches avec un argument
    /// différent.</summary>
    private static OpFn Loop(CondFn? extra) => fetchdat =>
    {
        int8_t offset = (int8_t)(uint8_t)fetchdat; cpu_state.pc++;
        CX--;
        CLOCK_CYCLES(is486 != 0 ? 7 : 11);
        PREFETCH_RUN(11, 2, -1, 0, 0, 0, 0, 0);
        if (CX != 0 && (extra is null || extra()))
        {
                cpu_state.pc += (uint32_t)offset;
                if ((cpu_state.op32 & 0x100) == 0)
                        cpu_state.pc &= 0xffff;
                CPU_BLOCK_END();
                PREFETCH_FLUSH();
                return 1;
        }
        return 0;
    };

    // pcem: x86_ops_jump.h:180-194 — opJCXZ.
    //
    // IL NE RESSEMBLE PAS AUX TROIS BOUCLES, malgré l'emplacement voisin : il ne
    // décrémente pas CX, ses cycles sont EN DUR (5, plus 4 si le saut est pris)
    // et non timing_bnt/timing_bt, et PREFETCH_RUN est dans chacune des deux
    // branches. C'est le profil d'un Jcc, pas d'un LOOP.
    private static int opJCXZ(uint32_t fetchdat)
    {
        int8_t offset = (int8_t)(uint8_t)fetchdat; cpu_state.pc++;
        CLOCK_CYCLES(5);
        if (CX == 0)
        {
                cpu_state.pc += (uint32_t)offset;
                if ((cpu_state.op32 & 0x100) == 0)
                        cpu_state.pc &= 0xffff;
                CLOCK_CYCLES(4);
                CPU_BLOCK_END();
                PREFETCH_RUN(9, 2, -1, 0, 0, 0, 0, 0);
                PREFETCH_FLUSH();
                return 1;
        }
        PREFETCH_RUN(5, 2, -1, 0, 0, 0, 0, 0);
        return 0;
    }

    // ---------------------------------------------------- les sauts inconditionnels

    // pcem: x86_ops_jump.h — opJMP_r8
    private static int opJMP_r8(uint32_t fetchdat)
    {
        int8_t offset = (int8_t)(uint8_t)fetchdat; cpu_state.pc++;
        cpu_state.pc += (uint32_t)offset;
        if ((cpu_state.op32 & 0x100) == 0)
                cpu_state.pc &= 0xffff;
        CPU_BLOCK_END();
        CLOCK_CYCLES(is486 != 0 ? 3 : 7);
        PREFETCH_RUN(7, 2, -1, 0, 0, 0, 0, 0);
        PREFETCH_FLUSH();
        return 0;
    }

    // pcem: x86_ops_jump.h — opJMP_r16. Le masquage de pc est INCONDITIONNEL ici,
    // là où la forme courte le subordonne à op32 : c'est PCem, pas une coquille.
    private static int opJMP_r16(uint32_t fetchdat)
    {
        int16_t offset = (int16_t)(uint16_t)fetchdat; cpu_state.pc += 2;
        cpu_state.pc += (uint32_t)offset;
        cpu_state.pc &= 0xffff;
        CPU_BLOCK_END();
        CLOCK_CYCLES(is486 != 0 ? 3 : 7);
        PREFETCH_RUN(7, 3, -1, 0, 0, 0, 0, 0);
        PREFETCH_FLUSH();
        return 0;
    }

    // pcem: x86_ops_jump.h — opJMP_far_a16.
    //
    // C'EST L'OPCODE DU VECTEUR DE RESET : le 286 démarre à F000:FFF0 sur un
    // EA. Il passe par loadcsjmp, dont seule la branche mode réel est transcrite
    // (x86seg.cs) — la branche protégée échoue en se nommant.
    //
    // `old_pc` est lu APRÈS getword() et donc APRÈS que pc a avancé de quatre
    // octets : loadcsjmp le reçoit pour pouvoir le restaurer si le chargement
    // abandonne. En mode réel il n'en fait rien.
    private static int opJMP_far_a16(uint32_t fetchdat)
    {
        uint16_t addr = (uint16_t)fetchdat; cpu_state.pc += 2;   // getwordf()
        uint16_t seg = getword();
        if (cpu_state.abrt != 0)
                return 1;
        uint32_t old_pc = cpu_state.pc;
        cpu_state.pc = addr;
        x86seg_c.loadcsjmp(seg, old_pc);
        CPU_BLOCK_END();
        PREFETCH_RUN(11, 5, -1, 0, 0, 0, 0, 0);
        PREFETCH_FLUSH();
        return 0;
    }

    // ------------------------------------------- l'appel et le retour PROCHES

    // CE SONT BIEN DES SAUTS, et ils vivent dans x86_ops_jump.h — pas dans
    // x86_ops_call.h ni x86_ops_ret.h, qui ne portent que les formes LOINTAINES
    // (CALL far, RETF, IRET) et FF /2-/5. Le découpage de PCem suit la mécanique,
    // pas la mnémonique : CALL near et RET near ne touchent ni CS ni descripteur,
    // ils empilent ou dépilent un offset et sautent. Un fichier, un en-tête.

    // pcem: x86_ops_jump.h — opCALL_r16
    private static int opCALL_r16(uint32_t fetchdat)
    {
        int16_t addr = (int16_t)(uint16_t)fetchdat; cpu_state.pc += 2;
        PUSH_W((uint16_t)cpu_state.pc);
        cpu_state.pc += (uint32_t)addr;
        cpu_state.pc &= 0xffff;
        CPU_BLOCK_END();
        CLOCK_CYCLES(is486 != 0 ? 3 : 7);
        PREFETCH_RUN(7, 3, -1, 0, 0, 1, 0, 0);
        PREFETCH_FLUSH();
        return 0;
    }

    // pcem: x86_ops_jump.h — opRET_w
    private static int opRET_w(uint32_t fetchdat)
    {
        uint16_t ret;

        ret = POP_W();
        if (cpu_state.abrt != 0)
                return 1;
        cpu_state.pc = ret;
        CPU_BLOCK_END();

        CLOCK_CYCLES(is486 != 0 ? 5 : 10);
        PREFETCH_RUN(10, 1, -1, 1, 0, 0, 0, 0);
        PREFETCH_FLUSH();
        return 0;
    }

    // pcem: x86_ops_jump.h — opRET_w_imm. Le `5` de PREFETCH_RUN compte cinq
    // octets d'instruction pour un RET imm16 qui en fait TROIS. C'est PCem :
    // opRET_w_imm et opRET_l_imm portent la même constante, celle de la forme
    // longue. Transcrit tel quel — le fuzzeur le verrait si c'était corrigé.
    private static int opRET_w_imm(uint32_t fetchdat)
    {
        uint16_t offset = (uint16_t)fetchdat; cpu_state.pc += 2;
        uint16_t ret;

        ret = POP_W();
        if (cpu_state.abrt != 0)
                return 1;
        if (stack32 != 0)
                ESP += offset;
        else
                SP += offset;
        cpu_state.pc = ret;
        CPU_BLOCK_END();

        CLOCK_CYCLES(is486 != 0 ? 5 : 10);
        PREFETCH_RUN(10, 5, -1, 1, 0, 0, 0, 0);
        PREFETCH_FLUSH();
        return 0;
    }

    // omitted: les formes `_l` de opJ (x86_ops_jump.h:56-70), opLOOPNE_l,
    //   opLOOPE_l, opLOOP_l, opJCXZ_16/32 des autres largeurs, opJMP_r32,
    //   opJMP_far_a32 — inatteignables tant qu'op32 est nul.
    // omitted: opJMP_far_a16 lit loadcsjmp ; la branche mode protégé de celle-ci
    //   est déclarée dans x86seg.cs, pas ici.

    /// <summary>Emplacements relevés sur ops_286[] de la .so par gdb : 70-7F pour
    /// les seize conditions, E0-E3 pour les boucles et JCXZ, E9/EA/EB pour les
    /// sauts inconditionnels. E8 (CALL near) est voisin mais appartient à
    /// x86_ops_call.h.</summary>
    private static void PoserGroupeSauts()
    {
        PoserSautsConditionnels();

        ops_286[0xE0] = Loop(() => ZF_SET() == 0);   // LOOPNE
        ops_286[0xE1] = Loop(() => ZF_SET() != 0);   // LOOPE
        ops_286[0xE2] = Loop(null);                  // LOOP
        ops_286[0xE3] = opJCXZ;

        ops_286[0xC2] = opRET_w_imm;
        ops_286[0xC3] = opRET_w;
        ops_286[0xE8] = opCALL_r16;
        ops_286[0xE9] = opJMP_r16;
        ops_286[0xEA] = opJMP_far_a16;
        ops_286[0xEB] = opJMP_r8;
    }
}
