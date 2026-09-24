// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/includes/private/cpu/x86_ops_ret.h  (lignes 3-127)
// SHA256: voir oracle.tsv ; vérifier avec tools/check-oracle.sh
// STATUS: partial — A6 : les trois emplacements qu'un 286 atteint, CA, CB, CF.
//         RETF_a32 et opIRET / opIRETD (les formes 386 et 486 de IRET) restent
//         dehors, déclarées au registre des omissions.
//
// LE RETOUR LOINTAIN ET LE RETOUR D'INTERRUPTION.
//
// RET PROCHE est à A5 : il vit dans x86_ops_jump.h. Ici il n'y a que ce qui
// recharge CS — donc ce qui, en mode protégé, passe par un descripteur.
//
// IL Y A TROIS IRET DANS PCem, et c'est le 286 qui prend opIRET_286. Les deux
// autres (opIRET pour le 386, opIRETD) ajoutent le retour en V86 et les formes
// 32 bits. Le choix est fait par la TABLE, pas par un test à l'exécution : c'est
// ops_286[0xCF] qui nomme opIRET_286. Vérifié dans la .so par gdb, pas supposé.
//
// LE MASQUE DE DRAPEAUX N'EST PAS LE MÊME DANS LES DEUX BRANCHES d'opIRET_286 :
// 0xffd5 quand stack32, 0x0fd5 sinon. Sur un 286 seule la seconde tourne, et
// 0x0fd5 y laisse IOPL et NT modifiables là où 0xffd5 laisserait passer des bits
// qui n'existent pas encore. Transcrit tel quel, écart compris.

using static iXtal26.Cpu._386_common;
using static iXtal26.Cpu.x86;
using static iXtal26.Cpu.x86_flags;

namespace iXtal26.Cpu;

internal static partial class _386
{
    /// <summary>pcem: x86_ops_ret.h:3-21 — la macro RETF_a16(stack_offset).
    ///
    /// DEVIATION: `return 1` depuis le milieu du macro ; la méthode rend `true`
    ///   pour « l'appelant doit rendre 1 », comme fetch_ea_16.
    ///
    /// LA PILE EST LUE PAR readmemw ET NON PAR POP_W : le pointeur n'avance
    /// qu'APRÈS le chargement de CS, en une seule addition de `4 + offset`. Si
    /// loadcs abandonne, la pile n'a pas bougé — c'est le rattrapage, et il est
    /// gratuit parce qu'il n'y a rien à défaire.</summary>
    private static bool RETF_a16(uint16_t stack_offset)
    {
        if ((msw & 1) != 0 && (cpu_state.eflags & VM_FLAG) == 0)
        {
                x86seg_c.pmoderetf(0, stack_offset);
                return true;
        }
        if (stack32 != 0)
        {
                cpu_state.pc = readmemw(ss, ESP);
                x86seg_c.loadcs(readmemw(ss, ESP + 2));
        }
        else
        {
                cpu_state.pc = readmemw(ss, SP);
                x86seg_c.loadcs(readmemw(ss, (uint32_t)(SP + 2)));
        }
        if (cpu_state.abrt != 0)
                return true;
        if (stack32 != 0)
                ESP += (uint32_t)(4 + stack_offset);
        else
                SP += (uint16_t)(4 + stack_offset);
        cycles -= cpu_c.timing_retf_rm;
        return false;
    }

    // pcem: x86_ops_ret.h:43-53 — opRETF_a16
    private static int opRETF_a16(uint32_t fetchdat)
    {
        int cycles_old = cycles;

        CPU_BLOCK_END();
        if (RETF_a16(0)) return 1;

        PREFETCH_RUN(cycles_old - cycles, 1, -1, 2, 0, 0, 0, 0);
        PREFETCH_FLUSH();
        return 0;
    }

    // pcem: x86_ops_ret.h:66-77 — opRETF_a16_imm
    private static int opRETF_a16_imm(uint32_t fetchdat)
    {
        uint16_t offset = (uint16_t)fetchdat; cpu_state.pc += 2;   // getwordf()
        int cycles_old = cycles;

        CPU_BLOCK_END();
        if (RETF_a16(offset)) return 1;

        PREFETCH_RUN(cycles_old - cycles, 3, -1, 2, 0, 0, 0, 0);
        PREFETCH_FLUSH();
        return 0;
    }

    // pcem: x86_ops_ret.h:91-126 — opIRET_286.
    //
    // nmi_enable = 1 EST LE VRAI EFFET DE BORD. Une NMI est inhibée depuis sa
    // prise en charge jusqu'au IRET qui la termine ; c'est cette ligne, et elle
    // seule, qui rouvre la porte. Elle est HORS des deux branches, donc elle
    // s'exécute même quand le mode protégé a fait le travail.
    private static int opIRET_286(uint32_t fetchdat)
    {
        int cycles_old = cycles;

        if ((cr0 & 1) != 0 && (cpu_state.eflags & VM_FLAG) != 0 && IOPL != 3)
        {
                x86seg_c.x86gpf("", 0);   // pcem passe NULL : la chaine ne sert qu'au pclog, omis
                return 1;
        }
        if ((msw & 1) != 0)
        {
                optype = IRET;
                x86seg_c.pmodeiret(0);
                optype = 0;
        }
        else
        {
                uint16_t new_cs;
                if (stack32 != 0)
                {
                        cpu_state.pc = readmemw(ss, ESP);
                        new_cs = readmemw(ss, ESP + 2);
                        cpu_state.flags = (uint16_t)((cpu_state.flags & 0x7000) |
                                                     (readmemw(ss, ESP + 4) & 0xffd5) | 2);
                        ESP += 6;
                }
                else
                {
                        cpu_state.pc = readmemw(ss, SP);
                        new_cs = readmemw(ss, (uint32_t)((SP + 2) & 0xffff));
                        cpu_state.flags = (uint16_t)((cpu_state.flags & 0x7000) |
                                                     (readmemw(ss, (uint32_t)((SP + 4) & 0xffff)) & 0x0fd5) | 2);
                        SP += 6;
                }
                x86seg_c.loadcs(new_cs);
                cycles -= cpu_c.timing_iret_rm;
        }
        flags_extract();
        nmi_enable = 1;
        CPU_BLOCK_END();

        PREFETCH_RUN(cycles_old - cycles, 1, -1, 2, 0, 0, 0, 0);
        PREFETCH_FLUSH();
        return cpu_state.abrt;
    }

    // omitted: RETF_a32, opRETF_a32, opRETF_a32_imm — op32 nul sur un 286.
    // omitted: opIRET (x86_ops_ret.h:128-190) et opIRETD (:191-...) — ce sont les
    //   formes 386 et 486, et c'est la TABLE qui choisit : ops_286[0xCF] nomme
    //   opIRET_286.

    /// <summary>pcem: CA, CB, CF — relevés sur ops_286[] par gdb.</summary>
    private static void PoserGroupeRetour()
    {
        ops_286[0xCA] = opRETF_a16_imm;
        ops_286[0xCB] = opRETF_a16;
        ops_286[0xCF] = opIRET_286;
    }
}
