// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/includes/private/cpu/x86_ops_movx.h
// SHA256: voir oracle.tsv ; vérifier avec tools/check-oracle.sh
// STATUS: partial — G2, D3 : les handlers de la table 0F du 386 que cet en-tête porte.

using iXtal26.Memory;
using static iXtal26.Cpu._386_common;
using static iXtal26.Cpu.x86;
using static iXtal26.Cpu.x86_flags;

namespace iXtal26.Cpu;

internal static partial class _386
{

    // ---- G2, D3 : la table 0F du 386, x86_ops_movx.h ----

    // pcem: x86_ops_movx.h:158
    private static int opMOVSX_l_b_a16(uint32_t fetchdat)
    {
        uint8_t temp;

        if (fetch_ea_16(fetchdat)) return 1;
        if (cpu_mod != 3)
                if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        temp = geteab();
        if (cpu_state.abrt != 0)
                return 1;
        cpu_state.regs[cpu_reg].l = (uint32_t)temp;
        if ((temp & 0x80) != 0)
                cpu_state.regs[cpu_reg].l |= 0xffffff00;

        CLOCK_CYCLES(3);
        PREFETCH_RUN(3, 2, (int)fetchdat, (cpu_mod == 3) ? 0 : 1, 0, 0, 0, 0);
        return 0;
    }

    // pcem: x86_ops_movx.h:175
    private static int opMOVSX_l_b_a32(uint32_t fetchdat)
    {
        uint8_t temp;

        if (fetch_ea_32(fetchdat)) return 1;
        if (cpu_mod != 3)
                if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        temp = geteab();
        if (cpu_state.abrt != 0)
                return 1;
        cpu_state.regs[cpu_reg].l = (uint32_t)temp;
        if ((temp & 0x80) != 0)
                cpu_state.regs[cpu_reg].l |= 0xffffff00;

        CLOCK_CYCLES(3);
        PREFETCH_RUN(3, 2, (int)fetchdat, (cpu_mod == 3) ? 0 : 1, 0, 0, 0, 1);
        return 0;
    }

    // pcem: x86_ops_movx.h:192
    private static int opMOVSX_l_w_a16(uint32_t fetchdat)
    {
        uint16_t temp;

        if (fetch_ea_16(fetchdat)) return 1;
        if (cpu_mod != 3)
                if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        temp = geteaw();
        if (cpu_state.abrt != 0)
                return 1;
        cpu_state.regs[cpu_reg].l = (uint32_t)temp;
        if ((temp & 0x8000) != 0)
                cpu_state.regs[cpu_reg].l |= 0xffff0000;

        CLOCK_CYCLES(3);
        PREFETCH_RUN(3, 2, (int)fetchdat, (cpu_mod == 3) ? 0 : 1, 0, 0, 0, 0);
        return 0;
    }

    // pcem: x86_ops_movx.h:209
    private static int opMOVSX_l_w_a32(uint32_t fetchdat)
    {
        uint16_t temp;

        if (fetch_ea_32(fetchdat)) return 1;
        if (cpu_mod != 3)
                if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        temp = geteaw();
        if (cpu_state.abrt != 0)
                return 1;
        cpu_state.regs[cpu_reg].l = (uint32_t)temp;
        if ((temp & 0x8000) != 0)
                cpu_state.regs[cpu_reg].l |= 0xffff0000;

        CLOCK_CYCLES(3);
        PREFETCH_RUN(3, 2, (int)fetchdat, (cpu_mod == 3) ? 0 : 1, 0, 0, 0, 1);
        return 0;
    }

    // pcem: x86_ops_movx.h:124
    private static int opMOVSX_w_b_a16(uint32_t fetchdat)
    {
        uint8_t temp;

        if (fetch_ea_16(fetchdat)) return 1;
        if (cpu_mod != 3)
                if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        temp = geteab();
        if (cpu_state.abrt != 0)
                return 1;
        cpu_state.regs[cpu_reg].w = (uint16_t)temp;
        if ((temp & 0x80) != 0)
                cpu_state.regs[cpu_reg].w |= 0xff00;

        CLOCK_CYCLES(3);
        PREFETCH_RUN(3, 2, (int)fetchdat, (cpu_mod == 3) ? 0 : 1, 0, 0, 0, 0);
        return 0;
    }

    // pcem: x86_ops_movx.h:141
    private static int opMOVSX_w_b_a32(uint32_t fetchdat)
    {
        uint8_t temp;

        if (fetch_ea_32(fetchdat)) return 1;
        if (cpu_mod != 3)
                if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        temp = geteab();
        if (cpu_state.abrt != 0)
                return 1;
        cpu_state.regs[cpu_reg].w = (uint16_t)temp;
        if ((temp & 0x80) != 0)
                cpu_state.regs[cpu_reg].w |= 0xff00;

        CLOCK_CYCLES(3);
        PREFETCH_RUN(3, 2, (int)fetchdat, (cpu_mod == 3) ? 0 : 1, 0, 0, 0, 1);
        return 0;
    }

    // pcem: x86_ops_movx.h:33
    private static int opMOVZX_l_b_a16(uint32_t fetchdat)
    {
        uint8_t temp;

        if (fetch_ea_16(fetchdat)) return 1;
        if (cpu_mod != 3)
                if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        temp = geteab();
        if (cpu_state.abrt != 0)
                return 1;
        cpu_state.regs[cpu_reg].l = (uint32_t)temp;

        CLOCK_CYCLES(3);
        PREFETCH_RUN(3, 2, (int)fetchdat, (cpu_mod == 3) ? 0 : 1, 0, 0, 0, 0);
        return 0;
    }

    // pcem: x86_ops_movx.h:48
    private static int opMOVZX_l_b_a32(uint32_t fetchdat)
    {
        uint8_t temp;

        if (fetch_ea_32(fetchdat)) return 1;
        if (cpu_mod != 3)
                if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        temp = geteab();
        if (cpu_state.abrt != 0)
                return 1;
        cpu_state.regs[cpu_reg].l = (uint32_t)temp;

        CLOCK_CYCLES(3);
        PREFETCH_RUN(3, 2, (int)fetchdat, (cpu_mod == 3) ? 0 : 1, 0, 0, 0, 1);
        return 0;
    }

    // pcem: x86_ops_movx.h:93
    private static int opMOVZX_l_w_a16(uint32_t fetchdat)
    {
        uint16_t temp;

        if (fetch_ea_16(fetchdat)) return 1;
        if (cpu_mod != 3)
                if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        temp = geteaw();
        if (cpu_state.abrt != 0)
                return 1;
        cpu_state.regs[cpu_reg].l = (uint32_t)temp;

        CLOCK_CYCLES(3);
        PREFETCH_RUN(3, 2, (int)fetchdat, (cpu_mod == 3) ? 0 : 1, 0, 0, 0, 0);
        return 0;
    }

    // pcem: x86_ops_movx.h:108
    private static int opMOVZX_l_w_a32(uint32_t fetchdat)
    {
        uint16_t temp;

        if (fetch_ea_32(fetchdat)) return 1;
        if (cpu_mod != 3)
                if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        temp = geteaw();
        if (cpu_state.abrt != 0)
                return 1;
        cpu_state.regs[cpu_reg].l = (uint32_t)temp;

        CLOCK_CYCLES(3);
        PREFETCH_RUN(3, 2, (int)fetchdat, (cpu_mod == 3) ? 0 : 1, 0, 0, 0, 1);
        return 0;
    }

    // pcem: x86_ops_movx.h:3
    private static int opMOVZX_w_b_a16(uint32_t fetchdat)
    {
        uint8_t temp;

        if (fetch_ea_16(fetchdat)) return 1;
        if (cpu_mod != 3)
                if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        temp = geteab();
        if (cpu_state.abrt != 0)
                return 1;
        cpu_state.regs[cpu_reg].w = (uint16_t)temp;

        CLOCK_CYCLES(3);
        PREFETCH_RUN(3, 2, (int)fetchdat, (cpu_mod == 3) ? 0 : 1, 0, 0, 0, 0);
        return 0;
    }

    // pcem: x86_ops_movx.h:18
    private static int opMOVZX_w_b_a32(uint32_t fetchdat)
    {
        uint8_t temp;

        if (fetch_ea_32(fetchdat)) return 1;
        if (cpu_mod != 3)
                if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        temp = geteab();
        if (cpu_state.abrt != 0)
                return 1;
        cpu_state.regs[cpu_reg].w = (uint16_t)temp;

        CLOCK_CYCLES(3);
        PREFETCH_RUN(3, 2, (int)fetchdat, (cpu_mod == 3) ? 0 : 1, 0, 0, 0, 1);
        return 0;
    }

    // pcem: x86_ops_movx.h:63
    private static int opMOVZX_w_w_a16(uint32_t fetchdat)
    {
        uint16_t temp;

        if (fetch_ea_16(fetchdat)) return 1;
        if (cpu_mod != 3)
                if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        temp = geteaw();
        if (cpu_state.abrt != 0)
                return 1;
        cpu_state.regs[cpu_reg].w = temp;

        CLOCK_CYCLES(3);
        PREFETCH_RUN(3, 2, (int)fetchdat, (cpu_mod == 3) ? 0 : 1, 0, 0, 0, 0);
        return 0;
    }

    // pcem: x86_ops_movx.h:78
    private static int opMOVZX_w_w_a32(uint32_t fetchdat)
    {
        uint16_t temp;

        if (fetch_ea_32(fetchdat)) return 1;
        if (cpu_mod != 3)
                if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        temp = geteaw();
        if (cpu_state.abrt != 0)
                return 1;
        cpu_state.regs[cpu_reg].w = temp;

        CLOCK_CYCLES(3);
        PREFETCH_RUN(3, 2, (int)fetchdat, (cpu_mod == 3) ? 0 : 1, 0, 0, 0, 1);
        return 0;
    }

    // pcem: 386_ops.h — les emplacements de ces handlers dans OP_TABLE(386) et (386_0f).
    private static void PoserGroupe_movx_0f_386()
    {
        ops_386_0f[0x0B6] = opMOVZX_w_b_a16;
        ops_386_0f[0x0B7] = opMOVZX_w_w_a16;
        ops_386_0f[0x0BE] = opMOVSX_w_b_a16;
        ops_386_0f[0x1B6] = opMOVZX_l_b_a16;
        ops_386_0f[0x1B7] = opMOVZX_l_w_a16;
        ops_386_0f[0x1BE] = opMOVSX_l_b_a16;
        ops_386_0f[0x1BF] = opMOVSX_l_w_a16;
        ops_386_0f[0x2B6] = opMOVZX_w_b_a32;
        ops_386_0f[0x2B7] = opMOVZX_w_w_a32;
        ops_386_0f[0x2BE] = opMOVSX_w_b_a32;
        ops_386_0f[0x3B6] = opMOVZX_l_b_a32;
        ops_386_0f[0x3B7] = opMOVZX_l_w_a32;
        ops_386_0f[0x3BE] = opMOVSX_l_b_a32;
        ops_386_0f[0x3BF] = opMOVSX_l_w_a32;
    }
}
