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
    // pcem bug, fixed in hardware mode: PB-189 — en mode matériel, RET, RETF et IRET contrôlent SS à chaque mot dépilé contre la limite du
    //   segment (386.Materiel.cs, « La limite des données »).

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
                if (materiel.pb_189)
                        if (_386_materiel.limite_pile_materiel((uint32_t)(ESP), 2)) return true;
                cpu_state.pc = readmemw(ss, ESP);
                if (materiel.pb_189)
                        if (_386_materiel.limite_pile_materiel((uint32_t)(ESP + 2), 2)) return true;
                x86seg_c.loadcs(readmemw(ss, ESP + 2));
        }
        else
        {
                if (materiel.pb_189)
                        if (_386_materiel.limite_pile_materiel((uint32_t)(SP), 2)) return true;
                cpu_state.pc = readmemw(ss, SP);
                if (materiel.pb_189)
                        if (_386_materiel.limite_pile_materiel((uint32_t)((uint32_t)(SP + 2)), 2)) return true;
                // pcem bug, fixed in hardware mode: PB-189 — sur une pile de 16 bits, CS se lit à l'offset replié (SS:0000h
                //   pour SP = FFFEh) ; PCem le lit en SS:10000h.
                if (materiel.pb_189)
                        x86seg_c.loadcs(readmemw(ss, (uint32_t)((SP + 2) & 0xFFFF)));
                else
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
                        if (materiel.pb_189)
                                if (_386_materiel.limite_pile_materiel((uint32_t)(ESP), 2)) return 1;
                        cpu_state.pc = readmemw(ss, ESP);
                        if (materiel.pb_189)
                                if (_386_materiel.limite_pile_materiel((uint32_t)(ESP + 2), 2)) return 1;
                        new_cs = readmemw(ss, ESP + 2);
                        if (materiel.pb_189)
                                if (_386_materiel.limite_pile_materiel((uint32_t)(ESP + 4), 2)) return 1;
                        cpu_state.flags = (uint16_t)((cpu_state.flags & 0x7000) |
                                                     (readmemw(ss, ESP + 4) & 0xffd5) | 2);
                        ESP += 6;
                }
                else
                {
                        if (materiel.pb_189)
                                if (_386_materiel.limite_pile_materiel((uint32_t)(SP), 2)) return 1;
                        cpu_state.pc = readmemw(ss, SP);
                        if (materiel.pb_189)
                                if (_386_materiel.limite_pile_materiel((uint32_t)((uint32_t)((SP + 2) & 0xffff)), 2)) return 1;
                        new_cs = readmemw(ss, (uint32_t)((SP + 2) & 0xffff));
                        if (materiel.pb_189)
                                if (_386_materiel.limite_pile_materiel((uint32_t)((uint32_t)((SP + 4) & 0xffff)), 2)) return 1;
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

    // Les formes 386 (RETF_a32, opIRET, opIRETD) suivent plus bas, depuis G2 D2 : ops_286[0xCF]
    //   nomme opIRET_286, ops_386 les autres — c'est la TABLE qui choisit.

    /// <summary>pcem: CA, CB, CF — relevés sur ops_286[] par gdb.</summary>
    private static void PoserGroupeRetour()
    {
        ops_286[0xCA] = opRETF_a16_imm;
        ops_286[0xCB] = opRETF_a16;
        ops_286[0xCF] = opIRET_286;
    }

    // ---- G2, D2 : les formes 32 bits (_l, _a32) de x86_ops_ret.h ----

    // pcem: x86_ops_ret.h:23-41 — la macro RETF_a32, même convention que RETF_a16.
    private static bool RETF_a32(uint16_t stack_offset)
    {
        if ((msw & 1) != 0 && (cpu_state.eflags & VM_FLAG) == 0)
        {
                x86seg_c.pmoderetf(1, stack_offset);
                return true;
        }
        if (stack32 != 0)
        {
                if (materiel.pb_189)
                        if (_386_materiel.limite_pile_materiel((uint32_t)(ESP), 4)) return true;
                cpu_state.pc = readmeml(ss, ESP);
                if (materiel.pb_189)
                        if (_386_materiel.limite_pile_materiel((uint32_t)((uint32_t)(ESP + 4)), 4)) return true;
                x86seg_c.loadcs((uint16_t)(readmeml(ss, (uint32_t)(ESP + 4)) & 0xffff));
        }
        else
        {
                if (materiel.pb_189)
                        if (_386_materiel.limite_pile_materiel((uint32_t)(SP), 4)) return true;
                cpu_state.pc = readmeml(ss, SP);
                if (materiel.pb_189)
                        if (_386_materiel.limite_pile_materiel((uint32_t)((uint32_t)(SP + 4)), 4)) return true;
                // pcem bug, fixed in hardware mode: PB-189 — l'offset replié, de même.
                if (materiel.pb_189)
                        x86seg_c.loadcs((uint16_t)(readmeml(ss, (uint32_t)((SP + 4) & 0xFFFF)) & 0xffff));
                else
                        x86seg_c.loadcs((uint16_t)(readmeml(ss, (uint32_t)(SP + 4)) & 0xffff));
        }
        if (cpu_state.abrt != 0)
                return true;
        if (stack32 != 0)
                ESP += (uint32_t)(8 + stack_offset);
        else
                SP += (uint16_t)(8 + stack_offset);
        cycles -= cpu_c.timing_retf_rm;
        return false;
    }

    // pcem: x86_ops_ret.h:128-190 — IRET du 386 (VME compris).
    private static int opIRET(uint32_t fetchdat)
    {
        int cycles_old = cycles;

        if ((cr0 & 1) != 0 && (cpu_state.eflags & VM_FLAG) != 0 && IOPL != 3)
        {
                if ((cr4 & CR4_VME) != 0)
                {
                        uint16_t new_pc, new_cs, new_flags;

                        if (materiel.pb_189)
                                if (_386_materiel.limite_pile_materiel((uint32_t)(SP), 2)) return 1;
                        new_pc = readmemw(ss, SP);
                        if (materiel.pb_189)
                                if (_386_materiel.limite_pile_materiel((uint32_t)((uint32_t)((SP + 2) & 0xffff)), 2)) return 1;
                        new_cs = readmemw(ss, (uint32_t)((SP + 2) & 0xffff));
                        if (materiel.pb_189)
                                if (_386_materiel.limite_pile_materiel((uint32_t)((uint32_t)((SP + 4) & 0xffff)), 2)) return 1;
                        new_flags = readmemw(ss, (uint32_t)((SP + 4) & 0xffff));
                        if (cpu_state.abrt != 0)
                                return 1;

                        if ((new_flags & T_FLAG) != 0 || ((new_flags & I_FLAG) != 0 && (cpu_state.eflags & VIP_FLAG) != 0))
                        {
                                x86seg_c.x86gpf("", 0);
                                return 1;
                        }
                        SP += 6;
                        if ((new_flags & I_FLAG) != 0)
                                cpu_state.eflags |= VIF_FLAG;
                        else
                                cpu_state.eflags &= unchecked((uint16_t)~VIF_FLAG);
                        cpu_state.flags = (uint16_t)((cpu_state.flags & 0x3300) | (new_flags & 0x4cd5) | 2);
                        x86seg_c.loadcs(new_cs);
                        cpu_state.pc = new_pc;

                        cycles -= cpu_c.timing_iret_rm;
                }
                else
                {
                        x86seg_c.x86gpf_expected("", 0);
                        return 1;
                }
        }
        else
        {
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
                                if (materiel.pb_189)
                                        if (_386_materiel.limite_pile_materiel((uint32_t)(ESP), 2)) return 1;
                                cpu_state.pc = readmemw(ss, ESP);
                                if (materiel.pb_189)
                                        if (_386_materiel.limite_pile_materiel((uint32_t)((uint32_t)(ESP + 2)), 2)) return 1;
                                new_cs = readmemw(ss, (uint32_t)(ESP + 2));
                                if (materiel.pb_189)
                                        if (_386_materiel.limite_pile_materiel((uint32_t)((uint32_t)(ESP + 4)), 2)) return 1;
                                cpu_state.flags = (uint16_t)((readmemw(ss, (uint32_t)(ESP + 4)) & 0xffd5) | 2);
                                ESP += 6;
                        }
                        else
                        {
                                if (materiel.pb_189)
                                        if (_386_materiel.limite_pile_materiel((uint32_t)(SP), 2)) return 1;
                                cpu_state.pc = readmemw(ss, SP);
                                if (materiel.pb_189)
                                        if (_386_materiel.limite_pile_materiel((uint32_t)((uint32_t)((SP + 2) & 0xffff)), 2)) return 1;
                                new_cs = readmemw(ss, (uint32_t)((SP + 2) & 0xffff));
                                if (materiel.pb_189)
                                        if (_386_materiel.limite_pile_materiel((uint32_t)((uint32_t)((SP + 4) & 0xffff)), 2)) return 1;
                                cpu_state.flags = (uint16_t)((readmemw(ss, (uint32_t)((SP + 4) & 0xffff)) & 0xffd5) | 2);
                                SP += 6;
                        }
                        x86seg_c.loadcs(new_cs);
                        cycles -= cpu_c.timing_iret_rm;
                }
        }
        flags_extract();
        nmi_enable = 1;
        CPU_BLOCK_END();

        PREFETCH_RUN(cycles_old - cycles, 1, -1, 2, 0, 0, 0, 0);
        PREFETCH_FLUSH();
        return cpu_state.abrt;
    }

    // pcem: x86_ops_ret.h:191-225
    private static int opIRETD(uint32_t fetchdat)
    {
        int cycles_old = cycles;

        if ((cr0 & 1) != 0 && (cpu_state.eflags & VM_FLAG) != 0 && IOPL != 3)
        {
                x86seg_c.x86gpf_expected("", 0);
                return 1;
        }
        if ((msw & 1) != 0)
        {
                optype = IRET;
                x86seg_c.pmodeiret(1);
                optype = 0;
        }
        else
        {
                uint16_t new_cs;
                if (stack32 != 0)
                {
                        if (materiel.pb_189)
                                if (_386_materiel.limite_pile_materiel((uint32_t)(ESP), 4)) return 1;
                        cpu_state.pc = readmeml(ss, ESP);
                        if (materiel.pb_189)
                                if (_386_materiel.limite_pile_materiel((uint32_t)((uint32_t)(ESP + 4)), 2)) return 1;
                        new_cs = readmemw(ss, (uint32_t)(ESP + 4));
                        if (materiel.pb_189)
                                if (_386_materiel.limite_pile_materiel((uint32_t)((uint32_t)(ESP + 8)), 2)) return 1;
                        if (materiel.pb_189)
                                if (_386_materiel.limite_pile_materiel((uint32_t)((uint32_t)(ESP + 10)), 2)) return 1;
                        cpu_state.flags = (uint16_t)((readmemw(ss, (uint32_t)(ESP + 8)) & 0xffd5) | 2);
                        cpu_state.eflags = readmemw(ss, (uint32_t)(ESP + 10));
                        ESP += 12;
                }
                else
                {
                        if (materiel.pb_189)
                                if (_386_materiel.limite_pile_materiel((uint32_t)(SP), 4)) return 1;
                        cpu_state.pc = readmeml(ss, SP);
                        if (materiel.pb_189)
                                if (_386_materiel.limite_pile_materiel((uint32_t)((uint32_t)((SP + 4) & 0xffff)), 2)) return 1;
                        new_cs = readmemw(ss, (uint32_t)((SP + 4) & 0xffff));
                        if (materiel.pb_189)
                                if (_386_materiel.limite_pile_materiel((uint32_t)((uint32_t)((SP + 8) & 0xffff)), 2)) return 1;
                        if (materiel.pb_189)
                                if (_386_materiel.limite_pile_materiel((uint32_t)((uint32_t)((SP + 10) & 0xffff)), 2)) return 1;
                        cpu_state.flags = (uint16_t)((readmemw(ss, (uint32_t)((SP + 8) & 0xffff)) & 0xffd5) | 2);
                        cpu_state.eflags = readmemw(ss, (uint32_t)((SP + 10) & 0xffff));
                        SP += 12;
                }
                x86seg_c.loadcs(new_cs);
                cycles -= cpu_c.timing_iret_rm;
        }
        flags_extract();
        nmi_enable = 1;
        CPU_BLOCK_END();

        PREFETCH_RUN(cycles_old - cycles, 1, -1, 0, 2, 0, 0, 1);
        PREFETCH_FLUSH();
        return cpu_state.abrt;
    }

    // pcem: x86_ops_ret.h:54-64
    private static int opRETF_a32(uint32_t fetchdat)
    {
        int cycles_old = cycles;

        CPU_BLOCK_END();
        if (RETF_a32(0)) return 1;

        PREFETCH_RUN(cycles_old - cycles, 1, -1, 0, 2, 0, 0, 1);
        PREFETCH_FLUSH();
        return 0;
    }

    // pcem: x86_ops_ret.h:78-89
    private static int opRETF_a32_imm(uint32_t fetchdat)
    {
        uint16_t offset = (uint16_t)fetchdat; cpu_state.pc += 2;
        int cycles_old = cycles;

        CPU_BLOCK_END();
        if (RETF_a32(offset)) return 1;

        PREFETCH_RUN(cycles_old - cycles, 3, -1, 0, 2, 0, 0, 1);
        PREFETCH_FLUSH();
        return 0;
    }

    // pcem: 386_ops.h — les emplacements de ces handlers dans OP_TABLE(386) et (386_0f).
    private static void PoserGroupe_ret_386()
    {
        ops_386[0x0CF] = opIRET;
        ops_386[0x1CA] = opRETF_a32_imm;
        ops_386[0x1CB] = opRETF_a32;
        ops_386[0x1CF] = opIRETD;
        ops_386[0x2CF] = opIRET;
        ops_386[0x3CA] = opRETF_a32_imm;
        ops_386[0x3CB] = opRETF_a32;
        ops_386[0x3CF] = opIRETD;
    }
}
