// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/includes/private/cpu/x86_ops_flag.h  (lignes 3-164)
// SHA256: voir oracle.tsv ; vérifier avec tools/check-oracle.sh
// STATUS: partial — A7 : les onze emplacements qu'un 286 atteint. opPUSHFD,
//         opPOPF (la forme 386) et opPOPFD restent dehors, déclarés au registre
//         des omissions.
//
// LE GROUPE QUI FAIT TRAVAILLER flags_rebuild.
//
// Jusqu'ici, flags_rebuild() n'était atteint que par le chemin d'un piège
// T_FLAG — donc uniquement dans les cas dirigés de core286-check. CMC, CLC,
// STC, SAHF, LAHF et PUSHF l'appellent EN PREMIÈRE LIGNE, et pour une raison
// qui se lit : ils manipulent `cpu_state.flags` DIRECTEMENT. Tant que la
// représentation paresseuse est vivante, ce champ est périmé ; le matérialiser
// est la seule façon de ne pas écraser un résultat qui n'a pas encore été
// calculé.
//
// ET TROIS D'ENTRE EUX NE L'APPELLENT PAS : CLD, STD et CLI. Ce n'est pas un
// oubli de PCem — D_FLAG et I_FLAG ne font pas partie des six bits que la
// paresse couvre (C, P, A, Z, N, V). Les poser ne détruit rien, donc rien n'a
// besoin d'être reconstruit. Le masque 0x8d5 de flags_rebuild le dit
// exactement : il laisse D et I intacts.
//
// LE 286 A SON PROPRE POPF. opPOPF_286 (9D) n'est pas opPOPF : c'est la table
// qui choisit, relevée dans la .so par gdb. Les deux formes 386 ajoutent le
// mode virtuel 8086 et les drapeaux d'interruption virtuels.

using static iXtal26.Cpu._386_common;
using static iXtal26.Cpu.x86;
using static iXtal26.Cpu.x86_flags;

namespace iXtal26.Cpu;

internal static partial class _386
{
    // pcem: x86_ops_flag.h:3-9 — opCMC
    private static int opCMC(uint32_t fetchdat)
    {
        flags_rebuild();
        cpu_state.flags ^= C_FLAG;
        CLOCK_CYCLES(2);
        PREFETCH_RUN(2, 1, -1, 0, 0, 0, 0, 0);
        return 0;
    }

    // pcem: x86_ops_flag.h:11-17 — opCLC
    private static int opCLC(uint32_t fetchdat)
    {
        flags_rebuild();
        cpu_state.flags &= unchecked((uint16_t)~C_FLAG);
        CLOCK_CYCLES(2);
        PREFETCH_RUN(2, 1, -1, 0, 0, 0, 0, 0);
        return 0;
    }

    // pcem: x86_ops_flag.h:18-23 — opCLD. PAS de flags_rebuild : voir l'en-tête.
    private static int opCLD(uint32_t fetchdat)
    {
        cpu_state.flags &= unchecked((uint16_t)~D_FLAG);
        CLOCK_CYCLES(2);
        PREFETCH_RUN(2, 1, -1, 0, 0, 0, 0, 0);
        return 0;
    }

    // pcem: x86_ops_flag.h:24-39 — opCLI.
    //
    // EN MODE RÉEL, IOPLp EST TOUJOURS VRAI — `(!(msw & 1)) || ...` — donc seule
    // la branche `else` tourne et CLI ne lève jamais de faute. Les deux branches
    // internes sont du Pentium (VME, PVI) ; elles sont transcrites parce que les
    // omettre ferait mentir la structure, pas parce qu'un 286 les visite.
    private static int opCLI(uint32_t fetchdat)
    {
        if (!IOPLp)
        {
                if (((cpu_state.eflags & VM_FLAG) == 0 && (cr4 & CR4_PVI) != 0) ||
                    ((cpu_state.eflags & VM_FLAG) != 0 && (cr4 & CR4_VME) != 0))
                {
                        cpu_state.eflags &= unchecked((uint16_t)~VIF_FLAG);
                }
                else
                {
                        x86seg_c.x86gpf("", 0);
                        return 1;
                }
        }
        else
                cpu_state.flags &= unchecked((uint16_t)~I_FLAG);

        CLOCK_CYCLES(3);
        PREFETCH_RUN(3, 1, -1, 0, 0, 0, 0, 0);
        return 0;
    }

    // pcem: x86_ops_flag.h:40-46 — opSTC
    private static int opSTC(uint32_t fetchdat)
    {
        flags_rebuild();
        cpu_state.flags |= C_FLAG;
        CLOCK_CYCLES(2);
        PREFETCH_RUN(2, 1, -1, 0, 0, 0, 0, 0);
        return 0;
    }

    // pcem: x86_ops_flag.h:47-52 — opSTD
    private static int opSTD(uint32_t fetchdat)
    {
        cpu_state.flags |= D_FLAG;
        CLOCK_CYCLES(2);
        PREFETCH_RUN(2, 1, -1, 0, 0, 0, 0, 0);
        return 0;
    }

    // pcem: x86_ops_flag.h:53-75 — opSTI.
    //
    // LE COMMENTAIRE DE PCem EST REPRIS VERBATIM : c'est lui qui explique
    // pourquoi cpu_end_block_after_ins vaut DEUX et non un.
    private static int opSTI(uint32_t fetchdat)
    {
        if (!IOPLp)
        {
                if (((cpu_state.eflags & VM_FLAG) == 0 && (cr4 & CR4_PVI) != 0) ||
                    ((cpu_state.eflags & VM_FLAG) != 0 && (cr4 & CR4_VME) != 0))
                {
                        if ((cpu_state.eflags & VIP_FLAG) != 0)
                        {
                                x86seg_c.x86gpf("", 0);
                                return 1;
                        }
                        else
                                cpu_state.eflags |= VIF_FLAG;
                }
                else
                {
                        x86seg_c.x86gpf("", 0);
                        return 1;
                }
        }
        else
                cpu_state.flags |= I_FLAG;

        /*First instruction after STI will always execute, regardless of whether
          there is a pending interrupt*/
        cpu_end_block_after_ins = 2;

        CLOCK_CYCLES(2);
        PREFETCH_RUN(2, 1, -1, 0, 0, 0, 0, 0);
        return 0;
    }

    // pcem: x86_ops_flag.h:77-86 — opSAHF. Le masque 0xd5 est celui des six bits
    // arithmétiques, et le `| 2` rallume le bit 1, toujours à un sur un x86.
    private static int opSAHF(uint32_t fetchdat)
    {
        flags_rebuild();
        cpu_state.flags = (uint16_t)((cpu_state.flags & 0xff00) | (AH & 0xd5) | 2);
        CLOCK_CYCLES(3);
        PREFETCH_RUN(3, 1, -1, 0, 0, 0, 0, 0);

        codegen_flags_changed = 0;

        return 0;
    }

    // pcem: x86_ops_flag.h:87-93 — opLAHF
    private static int opLAHF(uint32_t fetchdat)
    {
        flags_rebuild();
        AH = (uint8_t)(cpu_state.flags & 0xff);
        CLOCK_CYCLES(3);
        PREFETCH_RUN(3, 1, -1, 0, 0, 0, 0, 0);
        return 0;
    }

    // pcem: x86_ops_flag.h:95-116 — opPUSHF
    private static int opPUSHF(uint32_t fetchdat)
    {
        if ((cpu_state.eflags & VM_FLAG) != 0 && IOPL < 3)
        {
                if ((cr4 & CR4_VME) != 0)
                {
                        uint16_t temp;

                        flags_rebuild();
                        temp = (uint16_t)((cpu_state.flags & unchecked((uint16_t)~I_FLAG)) | 0x3000);
                        if ((cpu_state.eflags & VIF_FLAG) != 0)
                                temp |= I_FLAG;
                        PUSH_W(temp);
                }
                else
                {
                        x86seg_c.x86gpf("", 0);
                        return 1;
                }
        }
        else
        {
                flags_rebuild();
                PUSH_W(cpu_state.flags);
        }
        CLOCK_CYCLES(4);
        PREFETCH_RUN(4, 1, -1, 0, 0, 1, 0, 0);
        return cpu_state.abrt;
    }

    // pcem: x86_ops_flag.h:136-164 — opPOPF_286.
    //
    // QUATRE MASQUES, ET C'EST LE PRIVILÈGE QUI CHOISIT. En mode réel c'est
    // toujours le premier — `(flags & 0x7000) | (tempw & 0x0fd5) | 2` — qui
    // garde IOPL et NT de l'ancien état et ne laisse passer que les six bits
    // arithmétiques plus D et I. Les trois autres sont du mode protégé.
    //
    // flags_extract() À LA FIN N'EST PAS DÉCORATIF : il remet flags_op à
    // FLAGS_UNKNOWN. Sans lui, la valeur qu'on vient de dépiler serait écrasée
    // au premier lecteur de drapeau par un calcul paresseux périmé.
    private static int opPOPF_286(uint32_t fetchdat)
    {
        uint16_t tempw;

        if ((cpu_state.eflags & VM_FLAG) != 0 && IOPL < 3)
        {
                x86seg_c.x86gpf("", 0);
                return 1;
        }

        tempw = POP_W();
        if (cpu_state.abrt != 0)
                return 1;

        if ((msw & 1) == 0)
                cpu_state.flags = (uint16_t)((cpu_state.flags & 0x7000) | (tempw & 0x0fd5) | 2);
        else if (CPL == 0)
                cpu_state.flags = (uint16_t)((tempw & 0x7fd5) | 2);
        else if (IOPLp)
                cpu_state.flags = (uint16_t)((cpu_state.flags & 0x3000) | (tempw & 0x4fd5) | 2);
        else
                cpu_state.flags = (uint16_t)((cpu_state.flags & 0x3200) | (tempw & 0x4dd5) | 2);
        flags_extract();

        CLOCK_CYCLES(5);
        PREFETCH_RUN(5, 1, -1, 1, 0, 0, 0, 0);

        codegen_flags_changed = 0;

        return 0;
    }

    // omitted: opPUSHFD (:117-135), opPOPF (:165-212) et opPOPFD (:213-...) —
    //   les formes 386 et 486. C'est la TABLE qui choisit : ops_286[0x9C] nomme
    //   opPUSHF et ops_286[0x9D] opPOPF_286.

    /// <summary>pcem: 9C, 9D, 9E, 9F, F5, F8-FD — relevés sur ops_286[] par gdb.
    /// L'ordre de F8-FD est CLC, STC, CLI, STI, CLD, STD : les paires
    /// poser/effacer y sont entrelacées, pas groupées.</summary>
    private static void PoserGroupeDrapeaux()
    {
        ops_286[0x9C] = opPUSHF;
        ops_286[0x9D] = opPOPF_286;
        ops_286[0x9E] = opSAHF;
        ops_286[0x9F] = opLAHF;
        ops_286[0xF5] = opCMC;
        ops_286[0xF8] = opCLC;
        ops_286[0xF9] = opSTC;
        ops_286[0xFA] = opCLI;
        ops_286[0xFB] = opSTI;
        ops_286[0xFC] = opCLD;
        ops_286[0xFD] = opSTD;
    }
}
