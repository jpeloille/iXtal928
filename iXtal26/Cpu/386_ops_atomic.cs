// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/includes/private/cpu/x86_ops_atomic.h
// SHA256: voir oracle.tsv ; vérifier avec tools/check-oracle.sh
// STATUS: partial — G2, D3 : les handlers de la table 0F du 386 que cet en-tête porte.

using iXtal26.Memory;
using static iXtal26.Cpu._386_common;
using static iXtal26.Cpu.x86;
using static iXtal26.Cpu.x86_flags;

namespace iXtal26.Cpu;

internal static partial class _386
{

    // ---- G2, D3 : la table 0F du 386, x86_ops_atomic.h ----

    // pcem: x86_ops_atomic.h:3
    private static int opCMPXCHG_b_a16(uint32_t fetchdat)
    {
        uint8_t temp, temp2 = AL;
        if ((is486) == 0) {
                cpu_state.pc = cpu_state.oldpc;
                x86illegal();
                return 1;
        }
        if (fetch_ea_16(fetchdat)) return 1;
        if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
        temp = geteab();
        if (cpu_state.abrt != 0)
                return 1;
        if (AL == temp)
                seteab((uint8_t)(getr8(cpu_reg)));
        else
                AL = temp;
        if (cpu_state.abrt != 0)
                return 1;
        setsub8(temp2, temp);
        CLOCK_CYCLES((cpu_mod == 3) ? 6 : 10);
        return 0;
    }

    // pcem: x86_ops_atomic.h:25
    private static int opCMPXCHG_b_a32(uint32_t fetchdat)
    {
        uint8_t temp, temp2 = AL;
        if ((is486) == 0) {
                cpu_state.pc = cpu_state.oldpc;
                x86illegal();
                return 1;
        }
        if (fetch_ea_32(fetchdat)) return 1;
        if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
        temp = geteab();
        if (cpu_state.abrt != 0)
                return 1;
        if (AL == temp)
                seteab((uint8_t)(getr8(cpu_reg)));
        else
                AL = temp;
        if (cpu_state.abrt != 0)
                return 1;
        setsub8(temp2, temp);
        CLOCK_CYCLES((cpu_mod == 3) ? 6 : 10);
        return 0;
    }

    // pcem: x86_ops_atomic.h:93
    private static int opCMPXCHG_l_a16(uint32_t fetchdat)
    {
        uint32_t temp, temp2 = EAX;
        if ((is486) == 0) {
                cpu_state.pc = cpu_state.oldpc;
                x86illegal();
                return 1;
        }
        if (fetch_ea_16(fetchdat)) return 1;
        if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
        temp = geteal();
        if (cpu_state.abrt != 0)
                return 1;
        if (EAX == temp)
                seteal(cpu_state.regs[cpu_reg].l);
        else
                EAX = temp;
        if (cpu_state.abrt != 0)
                return 1;
        setsub32(temp2, temp);
        CLOCK_CYCLES((cpu_mod == 3) ? 6 : 10);
        return 0;
    }

    // pcem: x86_ops_atomic.h:115
    private static int opCMPXCHG_l_a32(uint32_t fetchdat)
    {
        uint32_t temp, temp2 = EAX;
        if ((is486) == 0) {
                cpu_state.pc = cpu_state.oldpc;
                x86illegal();
                return 1;
        }
        if (fetch_ea_32(fetchdat)) return 1;
        if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
        temp = geteal();
        if (cpu_state.abrt != 0)
                return 1;
        if (EAX == temp)
                seteal(cpu_state.regs[cpu_reg].l);
        else
                EAX = temp;
        if (cpu_state.abrt != 0)
                return 1;
        setsub32(temp2, temp);
        CLOCK_CYCLES((cpu_mod == 3) ? 6 : 10);
        return 0;
    }

    // pcem: x86_ops_atomic.h:48
    private static int opCMPXCHG_w_a16(uint32_t fetchdat)
    {
        uint16_t temp, temp2 = AX;
        if ((is486) == 0) {
                cpu_state.pc = cpu_state.oldpc;
                x86illegal();
                return 1;
        }
        if (fetch_ea_16(fetchdat)) return 1;
        if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
        temp = geteaw();
        if (cpu_state.abrt != 0)
                return 1;
        if (AX == temp)
                seteaw((uint16_t)(cpu_state.regs[cpu_reg].w));
        else
                AX = temp;
        if (cpu_state.abrt != 0)
                return 1;
        setsub16(temp2, temp);
        CLOCK_CYCLES((cpu_mod == 3) ? 6 : 10);
        return 0;
    }

    // pcem: x86_ops_atomic.h:70
    private static int opCMPXCHG_w_a32(uint32_t fetchdat)
    {
        uint16_t temp, temp2 = AX;
        if ((is486) == 0) {
                cpu_state.pc = cpu_state.oldpc;
                x86illegal();
                return 1;
        }
        if (fetch_ea_32(fetchdat)) return 1;
        if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
        temp = geteaw();
        if (cpu_state.abrt != 0)
                return 1;
        if (AX == temp)
                seteaw((uint16_t)(cpu_state.regs[cpu_reg].w));
        else
                AX = temp;
        if (cpu_state.abrt != 0)
                return 1;
        setsub16(temp2, temp);
        CLOCK_CYCLES((cpu_mod == 3) ? 6 : 10);
        return 0;
    }

    // pcem: x86_ops_atomic.h:199
    private static int opXADD_b_a16(uint32_t fetchdat)
    {
        uint8_t temp;
        if ((is486) == 0) {
                cpu_state.pc = cpu_state.oldpc;
                x86illegal();
                return 1;
        }
        if (fetch_ea_16(fetchdat)) return 1;
        if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
        temp = geteab();
        if (cpu_state.abrt != 0)
                return 1;
        seteab((uint8_t)(temp + getr8(cpu_reg)));
        if (cpu_state.abrt != 0)
                return 1;
        setadd8(temp, getr8(cpu_reg));
        setr8(cpu_reg, temp);
        CLOCK_CYCLES((cpu_mod == 3) ? 3 : 4);
        return 0;
    }

    // pcem: x86_ops_atomic.h:219
    private static int opXADD_b_a32(uint32_t fetchdat)
    {
        uint8_t temp;
        if ((is486) == 0) {
                cpu_state.pc = cpu_state.oldpc;
                x86illegal();
                return 1;
        }
        if (fetch_ea_32(fetchdat)) return 1;
        if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
        temp = geteab();
        if (cpu_state.abrt != 0)
                return 1;
        seteab((uint8_t)(temp + getr8(cpu_reg)));
        if (cpu_state.abrt != 0)
                return 1;
        setadd8(temp, getr8(cpu_reg));
        setr8(cpu_reg, temp);
        CLOCK_CYCLES((cpu_mod == 3) ? 3 : 4);
        return 0;
    }

    // pcem: x86_ops_atomic.h:281
    private static int opXADD_l_a16(uint32_t fetchdat)
    {
        uint32_t temp;
        if ((is486) == 0) {
                cpu_state.pc = cpu_state.oldpc;
                x86illegal();
                return 1;
        }
        if (fetch_ea_16(fetchdat)) return 1;
        if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
        temp = geteal();
        if (cpu_state.abrt != 0)
                return 1;
        seteal(temp + cpu_state.regs[cpu_reg].l);
        if (cpu_state.abrt != 0)
                return 1;
        setadd32(temp, cpu_state.regs[cpu_reg].l);
        cpu_state.regs[cpu_reg].l = temp;
        CLOCK_CYCLES((cpu_mod == 3) ? 3 : 4);
        return 0;
    }

    // pcem: x86_ops_atomic.h:301
    private static int opXADD_l_a32(uint32_t fetchdat)
    {
        uint32_t temp;
        if ((is486) == 0) {
                cpu_state.pc = cpu_state.oldpc;
                x86illegal();
                return 1;
        }
        if (fetch_ea_32(fetchdat)) return 1;
        if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
        temp = geteal();
        if (cpu_state.abrt != 0)
                return 1;
        seteal(temp + cpu_state.regs[cpu_reg].l);
        if (cpu_state.abrt != 0)
                return 1;
        setadd32(temp, cpu_state.regs[cpu_reg].l);
        cpu_state.regs[cpu_reg].l = temp;
        CLOCK_CYCLES((cpu_mod == 3) ? 3 : 4);
        return 0;
    }

    // pcem: x86_ops_atomic.h:240
    private static int opXADD_w_a16(uint32_t fetchdat)
    {
        uint16_t temp;
        if ((is486) == 0) {
                cpu_state.pc = cpu_state.oldpc;
                x86illegal();
                return 1;
        }
        if (fetch_ea_16(fetchdat)) return 1;
        if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
        temp = geteaw();
        if (cpu_state.abrt != 0)
                return 1;
        seteaw((uint16_t)(temp + cpu_state.regs[cpu_reg].w));
        if (cpu_state.abrt != 0)
                return 1;
        setadd16(temp, cpu_state.regs[cpu_reg].w);
        cpu_state.regs[cpu_reg].w = temp;
        CLOCK_CYCLES((cpu_mod == 3) ? 3 : 4);
        return 0;
    }

    // pcem: x86_ops_atomic.h:260
    private static int opXADD_w_a32(uint32_t fetchdat)
    {
        uint16_t temp;
        if ((is486) == 0) {
                cpu_state.pc = cpu_state.oldpc;
                x86illegal();
                return 1;
        }
        if (fetch_ea_32(fetchdat)) return 1;
        if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
        temp = geteaw();
        if (cpu_state.abrt != 0)
                return 1;
        seteaw((uint16_t)(temp + cpu_state.regs[cpu_reg].w));
        if (cpu_state.abrt != 0)
                return 1;
        setadd16(temp, cpu_state.regs[cpu_reg].w);
        cpu_state.regs[cpu_reg].w = temp;
        CLOCK_CYCLES((cpu_mod == 3) ? 3 : 4);
        return 0;
    }

    // pcem: 386_ops.h — les emplacements de ces handlers dans OP_TABLE(386) et (386_0f).
    private static void PoserGroupe_atomic_0f_386()
    {
        ops_386_0f[0x0B0] = opCMPXCHG_b_a16;
        ops_386_0f[0x0B1] = opCMPXCHG_w_a16;
        ops_386_0f[0x0C0] = opXADD_b_a16;
        ops_386_0f[0x0C1] = opXADD_w_a16;
        ops_386_0f[0x1B0] = opCMPXCHG_b_a16;
        ops_386_0f[0x1B1] = opCMPXCHG_l_a16;
        ops_386_0f[0x1C0] = opXADD_b_a16;
        ops_386_0f[0x1C1] = opXADD_l_a16;
        ops_386_0f[0x2B0] = opCMPXCHG_b_a32;
        ops_386_0f[0x2B1] = opCMPXCHG_w_a32;
        ops_386_0f[0x2C0] = opXADD_b_a32;
        ops_386_0f[0x2C1] = opXADD_w_a32;
        ops_386_0f[0x3B0] = opCMPXCHG_b_a32;
        ops_386_0f[0x3B1] = opCMPXCHG_l_a32;
        ops_386_0f[0x3C0] = opXADD_b_a32;
        ops_386_0f[0x3C1] = opXADD_l_a32;
    }
}
