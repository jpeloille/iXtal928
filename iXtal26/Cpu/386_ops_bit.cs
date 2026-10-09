// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/includes/private/cpu/x86_ops_bit.h
// SHA256: voir oracle.tsv ; vérifier avec tools/check-oracle.sh
// STATUS: partial — G2, D3 : les handlers de la table 0F du 386 que cet en-tête porte.

using iXtal26.Memory;
using static iXtal26.Cpu._386_common;
using static iXtal26.Cpu.x86;
using static iXtal26.Cpu.x86_flags;
using static iXtal26.Cpu._386_materiel;

namespace iXtal26.Cpu;

internal static partial class _386
{

    /// <summary>pcem: x86_ops_bit.h:84-194 — la macro opBT(name, operation), BTC (^=),
    /// BTR (&amp;= ~) et BTS (|=), en quatre formes. `eal_r = eal_w = 0` (NULL) force le
    /// chemin lent : l'adresse a bougé après fetch_ea, le raccourci n'y vaut plus.</summary>
    private static OpFn OpBTx(Func<uint32_t, uint32_t, uint32_t> operation, bool l, bool a32) => fetchdat =>
    {
        int tempc;
        if (a32 ? fetch_ea_32(fetchdat) : fetch_ea_16(fetchdat)) return 1;
        if (cpu_mod != 3)
                if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
        if (l)
        {
                uint32_t temp;
                // pcem bug, fixed in hardware mode: PB-183 — décalage non signé (x86_ops_bit.h:146, :173) : un
                //   registre négatif adresse en avant au lieu d'en arrière (386 PRM § 17.2, « -2 gigabits »).
                cpu_state.eaaddr += ((cpu_state.regs[cpu_reg].l / 32) * 4);
                if (materiel.pb_183)
                        bt_decalage_materiel(true, a32);
                eal_r = eal_w = -1;
                temp = geteal();
                if (cpu_state.abrt != 0)
                        return 1;
                tempc = (temp & (1u << (int)(cpu_state.regs[cpu_reg].l & 31))) != 0 ? 1 : 0;
                temp = operation(temp, 1u << (int)(cpu_state.regs[cpu_reg].l & 31));
                seteal(temp);
        }
        else
        {
                uint16_t temp;
                // pcem bug, fixed in hardware mode: PB-183 — décalage non signé (x86_ops_bit.h:92, :119) : un
                //   registre de 8000h à FFFFh adresse en avant au lieu d'en arrière (-32 768 à -1 bits).
                cpu_state.eaaddr += (uint32_t)((cpu_state.regs[cpu_reg].w / 16) * 2);
                if (materiel.pb_183)
                        bt_decalage_materiel(false, a32);
                eal_r = eal_w = -1;
                temp = geteaw();
                if (cpu_state.abrt != 0)
                        return 1;
                tempc = (temp & (1u << (cpu_state.regs[cpu_reg].w & 15))) != 0 ? 1 : 0;
                temp = (uint16_t)operation(temp, 1u << (cpu_state.regs[cpu_reg].w & 15));
                seteaw(temp);
        }
        if (cpu_state.abrt != 0)
                return 1;
        flags_rebuild();
        if (tempc != 0)
                cpu_state.flags |= C_FLAG;
        else
                cpu_state.flags &= unchecked((uint16_t)~C_FLAG);
        CLOCK_CYCLES(6);
        if (l)
                PREFETCH_RUN(6, 2, (int)fetchdat, 0, 1, 0, 1, a32 ? 1 : 0);
        else
                PREFETCH_RUN(6, 2, (int)fetchdat, 1, 0, 1, 0, a32 ? 1 : 0);
        return 0;
    };

    private static void PoserBTx386()
    {
        foreach (var (op, fn) in new (int, Func<uint32_t, uint32_t, uint32_t>)[]
                 { (0xBB, (t, b) => t ^ b), (0xB3, (t, b) => t & ~b), (0xAB, (t, b) => t | b) })
        {
                ops_386_0f[op] = OpBTx(fn, false, false);
                ops_386_0f[0x100 | op] = OpBTx(fn, true, false);
                ops_386_0f[0x200 | op] = OpBTx(fn, false, true);
                ops_386_0f[0x300 | op] = OpBTx(fn, true, true);
        }
    }

    // ---- G2, D3 : la table 0F du 386, x86_ops_bit.h ----

    // pcem: x86_ops_bit.h:297
    private static int opBA_l_a16(uint32_t fetchdat)
    {
        int tempc, count;
        uint32_t temp;

        if (fetch_ea_16(fetchdat)) return 1;
        if (cpu_mod != 3)
                if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;

        temp = geteal();
        count = getbyte();
        if (cpu_state.abrt != 0)
                return 1;
        tempc = (int)(temp & (1u << count));
        flags_rebuild();
        switch (fetchdat & 0x38) {
        case 0x20: /*BT w,imm*/
                if ((tempc) != 0)
                        cpu_state.flags |= C_FLAG;
                else
                        cpu_state.flags &= unchecked((uint16_t)~(C_FLAG));
                CLOCK_CYCLES(3);
                PREFETCH_RUN(3, 3, (int)fetchdat, 0, (cpu_mod == 3) ? 0 : 1, 0, 0, 0);
                return 0;
        case 0x28: /*BTS w,imm*/
                temp = (uint32_t)(temp | (1u << count));
                break;
        case 0x30: /*BTR w,imm*/
                temp = (uint32_t)(temp & ~(1u << count));
                break;
        case 0x38: /*BTC w,imm*/
                temp = (uint32_t)(temp ^ (1u << count));
                break;

        default:
                // omitted: pclog — sortie de diagnostic.
                cpu_state.pc = cpu_state.oldpc;
                x86illegal();
                break;
        }
        seteal(temp);
        if (cpu_state.abrt != 0)
                return 1;
        if ((tempc) != 0)
                cpu_state.flags |= C_FLAG;
        else
                cpu_state.flags &= unchecked((uint16_t)~(C_FLAG));
        CLOCK_CYCLES(6);
        PREFETCH_RUN(6, 3, (int)fetchdat, 0, (cpu_mod == 3) ? 0 : 1, 0, (cpu_mod == 3) ? 0 : 1, 0);
        return 0;
    }

    // pcem: x86_ops_bit.h:347
    private static int opBA_l_a32(uint32_t fetchdat)
    {
        int tempc, count;
        uint32_t temp;

        if (fetch_ea_32(fetchdat)) return 1;
        if (cpu_mod != 3)
                if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;

        temp = geteal();
        count = getbyte();
        if (cpu_state.abrt != 0)
                return 1;
        tempc = (int)(temp & (1u << count));
        flags_rebuild();
        switch (fetchdat & 0x38) {
        case 0x20: /*BT w,imm*/
                if ((tempc) != 0)
                        cpu_state.flags |= C_FLAG;
                else
                        cpu_state.flags &= unchecked((uint16_t)~(C_FLAG));
                CLOCK_CYCLES(3);
                PREFETCH_RUN(3, 3, (int)fetchdat, 0, (cpu_mod == 3) ? 0 : 1, 0, 0, 1);
                return 0;
        case 0x28: /*BTS w,imm*/
                temp = (uint32_t)(temp | (1u << count));
                break;
        case 0x30: /*BTR w,imm*/
                temp = (uint32_t)(temp & ~(1u << count));
                break;
        case 0x38: /*BTC w,imm*/
                temp = (uint32_t)(temp ^ (1u << count));
                break;

        default:
                // omitted: pclog — sortie de diagnostic.
                cpu_state.pc = cpu_state.oldpc;
                x86illegal();
                break;
        }
        seteal(temp);
        if (cpu_state.abrt != 0)
                return 1;
        if ((tempc) != 0)
                cpu_state.flags |= C_FLAG;
        else
                cpu_state.flags &= unchecked((uint16_t)~(C_FLAG));
        CLOCK_CYCLES(6);
        PREFETCH_RUN(6, 3, (int)fetchdat, 0, (cpu_mod == 3) ? 0 : 1, 0, (cpu_mod == 3) ? 0 : 1, 1);
        return 0;
    }

    // pcem: x86_ops_bit.h:196
    private static int opBA_w_a16(uint32_t fetchdat)
    {
        int tempc, count;
        uint16_t temp;

        if (fetch_ea_16(fetchdat)) return 1;
        if (cpu_mod != 3)
                if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;

        temp = geteaw();
        // pcem bug, fixed in hardware mode: PB-262 — le décalage immédiat n'est pas réduit modulo 16 : `1 << count`
        //   vise au-delà du mot quand `count & 31` vaut 16 ou plus, et BT, BTS, BTR, BTC ne lisent ni n'écrivent
        //   rien (x86_ops_bit.h:205, :255).
        count = getbyte();
        if (materiel.pb_262)
                count = bt_immediat_materiel(count);
        if (cpu_state.abrt != 0)
                return 1;
        tempc = (int)(temp & (1u << count));
        flags_rebuild();
        switch (fetchdat & 0x38) {
        case 0x20: /*BT w,imm*/
                if ((tempc) != 0)
                        cpu_state.flags |= C_FLAG;
                else
                        cpu_state.flags &= unchecked((uint16_t)~(C_FLAG));
                CLOCK_CYCLES(3);
                PREFETCH_RUN(3, 3, (int)fetchdat, (cpu_mod == 3) ? 0 : 1, 0, 0, 0, 0);
                return 0;
        case 0x28: /*BTS w,imm*/
                temp = (uint16_t)(temp | (1u << count));
                break;
        case 0x30: /*BTR w,imm*/
                temp = (uint16_t)(temp & ~(1u << count));
                break;
        case 0x38: /*BTC w,imm*/
                temp = (uint16_t)(temp ^ (1u << count));
                break;

        default:
                // omitted: pclog — sortie de diagnostic.
                cpu_state.pc = cpu_state.oldpc;
                x86illegal();
                break;
        }
        seteaw(temp);
        if (cpu_state.abrt != 0)
                return 1;
        if ((tempc) != 0)
                cpu_state.flags |= C_FLAG;
        else
                cpu_state.flags &= unchecked((uint16_t)~(C_FLAG));
        CLOCK_CYCLES(6);
        PREFETCH_RUN(6, 3, (int)fetchdat, (cpu_mod == 3) ? 0 : 1, 0, (cpu_mod == 3) ? 0 : 1, 0, 0);
        return 0;
    }

    // pcem: x86_ops_bit.h:246
    private static int opBA_w_a32(uint32_t fetchdat)
    {
        int tempc, count;
        uint16_t temp;

        if (fetch_ea_32(fetchdat)) return 1;
        if (cpu_mod != 3)
                if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;

        temp = geteaw();
        // pcem bug, fixed in hardware mode: PB-262 — le décalage immédiat n'est pas réduit modulo 16 : `1 << count`
        //   vise au-delà du mot quand `count & 31` vaut 16 ou plus, et BT, BTS, BTR, BTC ne lisent ni n'écrivent
        //   rien (x86_ops_bit.h:205, :255).
        count = getbyte();
        if (materiel.pb_262)
                count = bt_immediat_materiel(count);
        if (cpu_state.abrt != 0)
                return 1;
        tempc = (int)(temp & (1u << count));
        flags_rebuild();
        switch (fetchdat & 0x38) {
        case 0x20: /*BT w,imm*/
                if ((tempc) != 0)
                        cpu_state.flags |= C_FLAG;
                else
                        cpu_state.flags &= unchecked((uint16_t)~(C_FLAG));
                CLOCK_CYCLES(3);
                PREFETCH_RUN(3, 3, (int)fetchdat, (cpu_mod == 3) ? 0 : 1, 0, 0, 0, 1);
                return 0;
        case 0x28: /*BTS w,imm*/
                temp = (uint16_t)(temp | (1u << count));
                break;
        case 0x30: /*BTR w,imm*/
                temp = (uint16_t)(temp & ~(1u << count));
                break;
        case 0x38: /*BTC w,imm*/
                temp = (uint16_t)(temp ^ (1u << count));
                break;

        default:
                // omitted: pclog — sortie de diagnostic.
                cpu_state.pc = cpu_state.oldpc;
                x86illegal();
                break;
        }
        seteaw(temp);
        if (cpu_state.abrt != 0)
                return 1;
        if ((tempc) != 0)
                cpu_state.flags |= C_FLAG;
        else
                cpu_state.flags &= unchecked((uint16_t)~(C_FLAG));
        CLOCK_CYCLES(6);
        PREFETCH_RUN(6, 3, (int)fetchdat, (cpu_mod == 3) ? 0 : 1, 0, (cpu_mod == 3) ? 0 : 1, 0, 0);
        return 0;
    }

    // pcem: x86_ops_bit.h:43
    private static int opBT_l_r_a16(uint32_t fetchdat)
    {
        uint32_t temp;

        if (fetch_ea_16(fetchdat)) return 1;
        if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        // pcem bug, fixed in hardware mode: PB-183 — décalage non signé (x86_ops_bit.h:48) : voir OpBTx.
        cpu_state.eaaddr += ((cpu_state.regs[cpu_reg].l / 32) * 4);
        if (materiel.pb_183)
                bt_decalage_materiel(true, false);
        eal_r = -1; // pcem: `eal_r = 0` — NULL ; -1 est le « pas de raccourci » du C#
        temp = geteal();
        if (cpu_state.abrt != 0)
                return 1;
        flags_rebuild();
        if ((temp & (1u << (int)(cpu_state.regs[cpu_reg].l & 31))) != 0)
                cpu_state.flags |= C_FLAG;
        else
                cpu_state.flags &= unchecked((uint16_t)~(C_FLAG));

        CLOCK_CYCLES(3);
        PREFETCH_RUN(3, 2, (int)fetchdat, 0, 1, 0, 0, 0);
        return 0;
    }

    // pcem: x86_ops_bit.h:63
    private static int opBT_l_r_a32(uint32_t fetchdat)
    {
        uint32_t temp;

        if (fetch_ea_32(fetchdat)) return 1;
        if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        // pcem bug, fixed in hardware mode: PB-183 — décalage non signé (x86_ops_bit.h:68) : voir OpBTx.
        cpu_state.eaaddr += ((cpu_state.regs[cpu_reg].l / 32) * 4);
        if (materiel.pb_183)
                bt_decalage_materiel(true, true);
        eal_r = -1; // pcem: `eal_r = 0` — NULL ; -1 est le « pas de raccourci » du C#
        temp = geteal();
        if (cpu_state.abrt != 0)
                return 1;
        flags_rebuild();
        if ((temp & (1u << (int)(cpu_state.regs[cpu_reg].l & 31))) != 0)
                cpu_state.flags |= C_FLAG;
        else
                cpu_state.flags &= unchecked((uint16_t)~(C_FLAG));

        CLOCK_CYCLES(3);
        PREFETCH_RUN(3, 2, (int)fetchdat, 0, 1, 0, 0, 1);
        return 0;
    }

    // pcem: x86_ops_bit.h:3
    private static int opBT_w_r_a16(uint32_t fetchdat)
    {
        uint16_t temp;

        if (fetch_ea_16(fetchdat)) return 1;
        if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        // pcem bug, fixed in hardware mode: PB-183 — décalage non signé (x86_ops_bit.h:8) : voir OpBTx.
        cpu_state.eaaddr += (uint32_t)((cpu_state.regs[cpu_reg].w / 16) * 2);
        if (materiel.pb_183)
                bt_decalage_materiel(false, false);
        eal_r = -1; // pcem: `eal_r = 0` — NULL ; -1 est le « pas de raccourci » du C#
        temp = geteaw();
        if (cpu_state.abrt != 0)
                return 1;
        flags_rebuild();
        if ((temp & (1u << (int)(cpu_state.regs[cpu_reg].w & 15))) != 0)
                cpu_state.flags |= C_FLAG;
        else
                cpu_state.flags &= unchecked((uint16_t)~(C_FLAG));

        CLOCK_CYCLES(3);
        PREFETCH_RUN(3, 2, (int)fetchdat, 1, 0, 0, 0, 0);
        return 0;
    }

    // pcem: x86_ops_bit.h:23
    private static int opBT_w_r_a32(uint32_t fetchdat)
    {
        uint16_t temp;

        if (fetch_ea_32(fetchdat)) return 1;
        if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        // pcem bug, fixed in hardware mode: PB-183 — décalage non signé (x86_ops_bit.h:28) : voir OpBTx.
        cpu_state.eaaddr += (uint32_t)((cpu_state.regs[cpu_reg].w / 16) * 2);
        if (materiel.pb_183)
                bt_decalage_materiel(false, true);
        eal_r = -1; // pcem: `eal_r = 0` — NULL ; -1 est le « pas de raccourci » du C#
        temp = geteaw();
        if (cpu_state.abrt != 0)
                return 1;
        flags_rebuild();
        if ((temp & (1u << (int)(cpu_state.regs[cpu_reg].w & 15))) != 0)
                cpu_state.flags |= C_FLAG;
        else
                cpu_state.flags &= unchecked((uint16_t)~(C_FLAG));

        CLOCK_CYCLES(3);
        PREFETCH_RUN(3, 2, (int)fetchdat, 1, 0, 0, 0, 1);
        return 0;
    }

    // pcem: 386_ops.h — les emplacements de ces handlers dans OP_TABLE(386) et (386_0f).
    private static void PoserGroupe_bit_0f_386()
    {
        ops_386_0f[0x0A3] = opBT_w_r_a16;
        ops_386_0f[0x0BA] = opBA_w_a16;
        ops_386_0f[0x1A3] = opBT_l_r_a16;
        ops_386_0f[0x1BA] = opBA_l_a16;
        ops_386_0f[0x2A3] = opBT_w_r_a32;
        ops_386_0f[0x2BA] = opBA_w_a32;
        ops_386_0f[0x3A3] = opBT_l_r_a32;
        ops_386_0f[0x3BA] = opBA_l_a32;
    }
}
