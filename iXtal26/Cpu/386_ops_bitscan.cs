// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/includes/private/cpu/x86_ops_bitscan.h
// SHA256: voir oracle.tsv ; vérifier avec tools/check-oracle.sh
// STATUS: partial — G2, D3 : les handlers de la table 0F du 386 que cet en-tête porte.

using iXtal26.Memory;
using static iXtal26.Cpu._386_common;
using static iXtal26.Cpu.x86;
using static iXtal26.Cpu.x86_flags;

namespace iXtal26.Cpu;

internal static partial class _386
{

    /// <summary>pcem: x86_ops_bitscan.h:3-19 — la macro BS_common. Rend le rang du bit
    /// trouvé, ou -1 : la destination n'est ÉCRITE QUE si un bit est trouvé, et reste
    /// intacte sinon (ZF posé) — le comportement indéfini que PCem fige.</summary>
    private static int BS_common(uint32_t temp, int start, int end, int dir, int time, out int instr_cycles)
    {
        int found = -1;
        flags_rebuild();
        instr_cycles = 0;
        if (temp != 0)
        {
                int c;
                cpu_state.flags &= unchecked((uint16_t)~Z_FLAG);
                for (c = start; c != end; c += dir)
                {
                        CLOCK_CYCLES(time);
                        instr_cycles += time;
                        if ((temp & (1u << c)) != 0)
                        {
                                found = c;
                                break;
                        }
                }
        }
        else
                cpu_state.flags |= Z_FLAG;
        return found;
    }

    // ---- G2, D3 : la table 0F du 386, x86_ops_bitscan.h ----

    // pcem: x86_ops_bitscan.h:56
    private static int opBSF_l_a16(uint32_t fetchdat)
    {
        uint32_t temp;
        int instr_cycles;

        if (fetch_ea_16(fetchdat)) return 1;
        if (cpu_mod != 3)
                if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        temp = geteal();
        if (cpu_state.abrt != 0)
                return 1;

        int bs = BS_common(temp, 0, 32, 1, (is486 != 0) ? 1 : 3, out instr_cycles);
        if (bs >= 0)
                cpu_state.regs[cpu_reg].l = (uint32_t)bs;

        CLOCK_CYCLES((is486 != 0) ? 6 : 10);
        instr_cycles += ((is486 != 0) ? 6 : 10);
        PREFETCH_RUN(instr_cycles, 2, (int)fetchdat, 0, (cpu_mod == 3) ? 0 : 1, 0, 0, 0);
        return 0;
    }

    // pcem: x86_ops_bitscan.h:74
    private static int opBSF_l_a32(uint32_t fetchdat)
    {
        uint32_t temp;
        int instr_cycles;

        if (fetch_ea_32(fetchdat)) return 1;
        if (cpu_mod != 3)
                if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        temp = geteal();
        if (cpu_state.abrt != 0)
                return 1;

        int bs = BS_common(temp, 0, 32, 1, (is486 != 0) ? 1 : 3, out instr_cycles);
        if (bs >= 0)
                cpu_state.regs[cpu_reg].l = (uint32_t)bs;

        CLOCK_CYCLES((is486 != 0) ? 6 : 10);
        instr_cycles += ((is486 != 0) ? 6 : 10);
        PREFETCH_RUN(instr_cycles, 2, (int)fetchdat, 0, (cpu_mod == 3) ? 0 : 1, 0, 0, 1);
        return 0;
    }

    // pcem: x86_ops_bitscan.h:20
    private static int opBSF_w_a16(uint32_t fetchdat)
    {
        uint16_t temp;
        int instr_cycles;

        if (fetch_ea_16(fetchdat)) return 1;
        if (cpu_mod != 3)
                if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        temp = geteaw();
        if (cpu_state.abrt != 0)
                return 1;

        int bs = BS_common(temp, 0, 16, 1, (is486 != 0) ? 1 : 3, out instr_cycles);
        if (bs >= 0)
                cpu_state.regs[cpu_reg].w = (uint16_t)bs;

        CLOCK_CYCLES((is486 != 0) ? 6 : 10);
        instr_cycles += ((is486 != 0) ? 6 : 10);
        PREFETCH_RUN(instr_cycles, 2, (int)fetchdat, (cpu_mod == 3) ? 0 : 1, 0, 0, 0, 0);
        return 0;
    }

    // pcem: x86_ops_bitscan.h:38
    private static int opBSF_w_a32(uint32_t fetchdat)
    {
        uint16_t temp;
        int instr_cycles;

        if (fetch_ea_32(fetchdat)) return 1;
        if (cpu_mod != 3)
                if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        temp = geteaw();
        if (cpu_state.abrt != 0)
                return 1;

        int bs = BS_common(temp, 0, 16, 1, (is486 != 0) ? 1 : 3, out instr_cycles);
        if (bs >= 0)
                cpu_state.regs[cpu_reg].w = (uint16_t)bs;

        CLOCK_CYCLES((is486 != 0) ? 6 : 10);
        instr_cycles += ((is486 != 0) ? 6 : 10);
        PREFETCH_RUN(instr_cycles, 2, (int)fetchdat, (cpu_mod == 3) ? 0 : 1, 0, 0, 0, 1);
        return 0;
    }

    // pcem: x86_ops_bitscan.h:129
    private static int opBSR_l_a16(uint32_t fetchdat)
    {
        uint32_t temp;
        int instr_cycles;

        if (fetch_ea_16(fetchdat)) return 1;
        if (cpu_mod != 3)
                if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        temp = geteal();
        if (cpu_state.abrt != 0)
                return 1;

        int bs = BS_common(temp, 31, -1, -1, 3, out instr_cycles);
        if (bs >= 0)
                cpu_state.regs[cpu_reg].l = (uint32_t)bs;

        CLOCK_CYCLES((is486 != 0) ? 6 : 10);
        instr_cycles += ((is486 != 0) ? 6 : 10);
        PREFETCH_RUN(instr_cycles, 2, (int)fetchdat, 0, (cpu_mod == 3) ? 0 : 1, 0, 0, 0);
        return 0;
    }

    // pcem: x86_ops_bitscan.h:147
    private static int opBSR_l_a32(uint32_t fetchdat)
    {
        uint32_t temp;
        int instr_cycles;

        if (fetch_ea_32(fetchdat)) return 1;
        if (cpu_mod != 3)
                if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        temp = geteal();
        if (cpu_state.abrt != 0)
                return 1;

        int bs = BS_common(temp, 31, -1, -1, 3, out instr_cycles);
        if (bs >= 0)
                cpu_state.regs[cpu_reg].l = (uint32_t)bs;

        CLOCK_CYCLES((is486 != 0) ? 6 : 10);
        instr_cycles += ((is486 != 0) ? 6 : 10);
        PREFETCH_RUN(instr_cycles, 2, (int)fetchdat, 0, (cpu_mod == 3) ? 0 : 1, 0, 0, 1);
        return 0;
    }

    // pcem: x86_ops_bitscan.h:93
    private static int opBSR_w_a16(uint32_t fetchdat)
    {
        uint16_t temp;
        int instr_cycles;

        if (fetch_ea_16(fetchdat)) return 1;
        if (cpu_mod != 3)
                if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        temp = geteaw();
        if (cpu_state.abrt != 0)
                return 1;

        int bs = BS_common(temp, 15, -1, -1, 3, out instr_cycles);
        if (bs >= 0)
                cpu_state.regs[cpu_reg].w = (uint16_t)bs;

        CLOCK_CYCLES((is486 != 0) ? 6 : 10);
        instr_cycles += ((is486 != 0) ? 6 : 10);
        PREFETCH_RUN(instr_cycles, 2, (int)fetchdat, (cpu_mod == 3) ? 0 : 1, 0, 0, 0, 0);
        return 0;
    }

    // pcem: x86_ops_bitscan.h:111
    private static int opBSR_w_a32(uint32_t fetchdat)
    {
        uint16_t temp;
        int instr_cycles;

        if (fetch_ea_32(fetchdat)) return 1;
        if (cpu_mod != 3)
                if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        temp = geteaw();
        if (cpu_state.abrt != 0)
                return 1;

        int bs = BS_common(temp, 15, -1, -1, 3, out instr_cycles);
        if (bs >= 0)
                cpu_state.regs[cpu_reg].w = (uint16_t)bs;

        CLOCK_CYCLES((is486 != 0) ? 6 : 10);
        instr_cycles += ((is486 != 0) ? 6 : 10);
        PREFETCH_RUN(instr_cycles, 2, (int)fetchdat, (cpu_mod == 3) ? 0 : 1, 0, 0, 0, 1);
        return 0;
    }

    // pcem: 386_ops.h — les emplacements de ces handlers dans OP_TABLE(386) et (386_0f).
    private static void PoserGroupe_bitscan_0f_386()
    {
        ops_386_0f[0x0BC] = opBSF_w_a16;
        ops_386_0f[0x0BD] = opBSR_w_a16;
        ops_386_0f[0x1BC] = opBSF_l_a16;
        ops_386_0f[0x1BD] = opBSR_l_a16;
        ops_386_0f[0x2BC] = opBSF_w_a32;
        ops_386_0f[0x2BD] = opBSR_w_a32;
        ops_386_0f[0x3BC] = opBSF_l_a32;
        ops_386_0f[0x3BD] = opBSR_l_a32;
    }
}
