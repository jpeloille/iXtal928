// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/includes/private/cpu/x86_ops_inc_dec.h  (lignes 3-72)
// SHA256: voir oracle.tsv ; vérifier avec tools/check-oracle.sh
// STATUS: partial — A8 : les 17 emplacements qu'un 286 atteint. Les seize
//         instanciations 32 bits de INC_DEC_OP et opINCDEC_b_a32 restent
//         dehors, déclarées au registre des omissions.
//
// INC ET DEC NE TOUCHENT PAS LA RETENUE, et c'est tout ce qui les distingue
// d'un ADD 1 ou d'un SUB 1. Le mécanisme est dans les poseurs, pas ici :
// setadd16nc et setsub16nc appellent flags_rebuild_c() avant de poser
// l'opération et posent FLAGS_INC16 / FLAGS_DEC16 — CF_SET, voyant ces
// espèces, ira relire le C matérialisé dans cpu_state.flags plutôt que de le
// recalculer. Ce sont les six fonctions qui manquaient à x86_flags.cs (cba30be).
//
// UNE MACRO À QUATRE PARAMÈTRES, et le quatrième est le poseur de drapeaux —
// c'est lui qui porte toute la différence entre INC et DEC. Le troisième, `inc`,
// vaut +1 ou -1 ; le second est le registre. L'ordre des emplacements 40-4F est
// l'ordre x86 — AX, CX, DX, BX, SP, BP, SI, DI — et non celui des invocations
// dans le fichier C, qui liste AX, BX, CX, DX. Relevé sur ops_286[] par gdb.

using static iXtal26.Cpu._386_common;
using static iXtal26.Cpu.x86;
using static iXtal26.Cpu.x86_flags;

namespace iXtal26.Cpu;

internal static partial class _386
{
    /// <summary>pcem: x86_ops_inc_dec.h:3-10 — la macro INC_DEC_OP, sur les huit
    /// registres généraux, en INC puis en DEC.
    ///
    /// L'ORDRE COMPTE : setflags(reg, 1) est appelé AVANT que reg ne bouge, donc
    /// les drapeaux décrivent l'opération sur l'ANCIENNE valeur. C'est ce que
    /// `setflags(reg, 1); reg += inc;` dit, et l'inverser changerait le
    /// résultat de VF_SET au passage de 0x7FFF à 0x8000.</summary>
    private static void PoserIncDecRegistres()
    {
        for (var n = 0; n < 8; n++)
        {
                var reg = n;

                // INC_DEC_OP(INC_xx, xx, 1, setadd16nc)
                ops_286[0x40 + reg] = fetchdat =>
                {
                        setadd16nc(cpu_state.regs[reg].w, 1);
                        cpu_state.regs[reg].w += 1;
                        CLOCK_CYCLES(cpu_c.timing_rr);
                        PREFETCH_RUN(cpu_c.timing_rr, 1, -1, 0, 0, 0, 0, 0);
                        return 0;
                };

                // INC_DEC_OP(DEC_xx, xx, -1, setsub16nc)
                ops_286[0x48 + reg] = fetchdat =>
                {
                        setsub16nc(cpu_state.regs[reg].w, 1);
                        cpu_state.regs[reg].w -= 1;
                        CLOCK_CYCLES(cpu_c.timing_rr);
                        PREFETCH_RUN(cpu_c.timing_rr, 1, -1, 0, 0, 0, 0, 0);
                        return 0;
                };
        }
    }

    // pcem: x86_ops_inc_dec.h:48-72 — opINCDEC_b_a16.
    //
    // UN SEUL HANDLER POUR DEUX INSTRUCTIONS, et le bit qui les sépare est
    // `rmdat & 0x38` — donc le champ `reg` du ModRM, comme pour le groupe
    // immédiat et pour FF. Mais ici le test est BINAIRE : tout ce qui n'est pas
    // zéro est un DEC. Il n'y a pas de sous-opcode illégal, contrairement à FF.
    //
    // Et les cycles passent par timing_mm là où une seule écriture mémoire a
    // lieu — même écart qu'au FF /0 et /1 de A6, invisible au fuzzeur puisque
    // timing_mm et timing_mr valent tous deux 7 sur un 286.
    private static int opINCDEC_b_a16(uint32_t fetchdat)
    {
        uint8_t temp;

        if (fetch_ea_16(fetchdat)) return 1;
        if (cpu_mod != 3)
                if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
        temp = geteab();
        if (cpu_state.abrt != 0)
                return 1;

        if ((fetchdat & 0x38) != 0)
        {
                seteab((uint8_t)(temp - 1));
                if (cpu_state.abrt != 0)
                        return 1;
                setsub8nc(temp, 1);
        }
        else
        {
                seteab((uint8_t)(temp + 1));
                if (cpu_state.abrt != 0)
                        return 1;
                setadd8nc(temp, 1);
        }
        CLOCK_CYCLES((cpu_mod == 3) ? cpu_c.timing_rr : cpu_c.timing_mm);
        PREFETCH_RUN((cpu_mod == 3) ? cpu_c.timing_rr : cpu_c.timing_mm, 2, (int)fetchdat,
                     (cpu_mod == 3) ? 0 : 1, 0, (cpu_mod == 3) ? 0 : 1, 0, 0);
        return 0;
    }

    // omitted: les seize instanciations 32 bits de INC_DEC_OP (INC_EAX … DEC_ESP)
    //   et opINCDEC_b_a32 — op32 nul sur un 286.

    /// <summary>pcem: 40-4F et FE — relevés sur ops_286[] par gdb.</summary>
    private static void PoserGroupeIncDec()
    {
        PoserIncDecRegistres();
        ops_286[0xFE] = opINCDEC_b_a16;
    }

    /// <summary>G2, D2 — les seize instanciations 32 bits de INC_DEC_OP
    /// (x86_ops_inc_dec.h:21-38), aux quadrants 1 et 3.</summary>
    private static void PoserIncDec386()
    {
        for (var n = 0; n < 8; n++)
        {
                var reg = n;
                // INC_DEC_OP(INC_Exx, Exx, 1, setadd32nc)
                OpFn inc = fetchdat =>
                {
                        setadd32nc(cpu_state.regs[reg].l, 1);
                        cpu_state.regs[reg].l += 1;
                        CLOCK_CYCLES(cpu_c.timing_rr);
                        PREFETCH_RUN(cpu_c.timing_rr, 1, -1, 0, 0, 0, 0, 0);
                        return 0;
                };
                // INC_DEC_OP(DEC_Exx, Exx, -1, setsub32nc)
                OpFn dec = fetchdat =>
                {
                        setsub32nc(cpu_state.regs[reg].l, 1);
                        cpu_state.regs[reg].l -= 1;
                        CLOCK_CYCLES(cpu_c.timing_rr);
                        PREFETCH_RUN(cpu_c.timing_rr, 1, -1, 0, 0, 0, 0, 0);
                        return 0;
                };
                ops_386[0x140 + reg] = inc;
                ops_386[0x340 + reg] = inc;
                ops_386[0x148 + reg] = dec;
                ops_386[0x348 + reg] = dec;
        }
    }

    // ---- G2, D2 : les formes 32 bits (_l, _a32) de x86_ops_inc_dec.h ----

    // pcem: x86_ops_inc_dec.h:73
    private static int opINCDEC_b_a32(uint32_t fetchdat)
    {
        uint8_t temp;

        if (fetch_ea_32(fetchdat)) return 1;
        if (cpu_mod != 3)
                if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
        temp = geteab();
        if (cpu_state.abrt != 0)
                return 1;

        if ((fetchdat & 0x38) != 0) {
                seteab((uint8_t)(temp - 1));
                if (cpu_state.abrt != 0)
                        return 1;
                setsub8nc(temp, 1);
        } else {
                seteab((uint8_t)(temp + 1));
                if (cpu_state.abrt != 0)
                        return 1;
                setadd8nc(temp, 1);
        }
        CLOCK_CYCLES((cpu_mod == 3) ? cpu_c.timing_rr : cpu_c.timing_mm);
        PREFETCH_RUN((cpu_mod == 3) ? cpu_c.timing_rr : cpu_c.timing_mm, 2, (int)fetchdat, (cpu_mod == 3) ? 0 : 1, 0, (cpu_mod == 3) ? 0 : 1, 0, 1);
        return 0;
    }

    // pcem: 386_ops.h — les emplacements de ces handlers dans OP_TABLE(386) et (386_0f).
    private static void PoserGroupe_inc_dec_386()
    {
        ops_386[0x2FE] = opINCDEC_b_a32;
        ops_386[0x3FE] = opINCDEC_b_a32;
    }
}
