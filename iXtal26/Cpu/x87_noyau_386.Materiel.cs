// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — le noyau de 80 bits du x87, ses gestionnaires du 286, du 386 et du 486, a16 et a32 (G13.6, le noyau ; PLAN-G13.md).
// STATUS: materiel
//
// GÉNÉRÉ par tools/x87gen/genmat.py, d'une description partagée avec x87_noyau_808x.Materiel.cs ; ne pas éditer à la
// main. La couche commune (l'état, la pile, les exceptions, l'arrondi) est dans x87_noyau.Materiel.cs. Une classe à
// part : des membres de plus dans _386 renuméroteraient ses lambdas.

using static iXtal26.Cpu._386_common;

namespace iXtal26.Cpu;

internal static class _386_noyau
{
    // FNINIT : l'état de FNINIT selon le type (_x87_noyau.finit) ; sur le 8087, IR effacé, sa sortie INT retombe.
    internal static int opFNINIT(uint32_t fetchdat)
    {
        if (_386.FP_ENTER()) return 1;
        cpu_state.pc++;
        _x87_noyau.finit();
        _386.CLOCK_CYCLES(x87_timings_c.x87_timings.finit);
        _386.CPU_BLOCK_END();
        return 0;
    }

    // FNCLEX : les six exceptions, SF, ES et B effacés (387 PRM, FCLEX ; 287 PRM, FCLEX : B compris) ; C3-C0 et
    //   TOP gardés ; sur le 8087, la sortie INT retombe.
    internal static int opFNCLEX(uint32_t fetchdat)
    {
        if (_386.FP_ENTER()) return 1;
        cpu_state.pc++;
        cpu_state.npxs &= 0x7F00;
        _386.CLOCK_CYCLES(x87_timings_c.x87_timings.fclex);
        return 0;
    }

    // FLDCW m16 : le mot de contrôle. Démasquer une exception en attente active ERROR# (387 PRM § 4.8.2) : N5.
    internal static int opFLDCW_a16(uint32_t fetchdat)
    {
        uint16_t tempw;
        if (_386.FP_ENTER()) return 1;
        if (fetch_ea_16(fetchdat)) return 1;
        if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        tempw = geteaw();
        if (cpu_state.abrt != 0)
                return 1;
        cpu_state.npxc = tempw;
        _386.CLOCK_CYCLES(x87_timings_c.x87_timings.fldcw);
        return 0;
    }

    internal static int opFLDCW_a32(uint32_t fetchdat)
    {
        uint16_t tempw;
        if (_386.FP_ENTER()) return 1;
        if (fetch_ea_32(fetchdat)) return 1;
        if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        tempw = geteaw();
        if (cpu_state.abrt != 0)
                return 1;
        cpu_state.npxc = tempw;
        _386.CLOCK_CYCLES(x87_timings_c.x87_timings.fldcw);
        return 0;
    }

    // FNSTCW m16.
    internal static int opFNSTCW_a16(uint32_t fetchdat)
    {
        if (_386.FP_ENTER()) return 1;
        if (fetch_ea_16(fetchdat)) return 1;
        if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
        seteaw(cpu_state.npxc);
        _386.CLOCK_CYCLES(x87_timings_c.x87_timings.fstcw_sw);
        return cpu_state.abrt;
    }

    internal static int opFNSTCW_a32(uint32_t fetchdat)
    {
        if (_386.FP_ENTER()) return 1;
        if (fetch_ea_32(fetchdat)) return 1;
        if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
        seteaw(cpu_state.npxc);
        _386.CLOCK_CYCLES(x87_timings_c.x87_timings.fstcw_sw);
        return cpu_state.abrt;
    }

    // FNSTSW m16 : le mot d'état, TOP compris.
    internal static int opFNSTSW_a16(uint32_t fetchdat)
    {
        if (_386.FP_ENTER()) return 1;
        if (fetch_ea_16(fetchdat)) return 1;
        if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
        seteaw((uint16_t)((cpu_state.npxs & 0xC7FF) | ((cpu_state.TOP & 7) << 11)));
        _386.CLOCK_CYCLES(x87_timings_c.x87_timings.fstcw_sw);
        return cpu_state.abrt;
    }

    internal static int opFNSTSW_a32(uint32_t fetchdat)
    {
        if (_386.FP_ENTER()) return 1;
        if (fetch_ea_32(fetchdat)) return 1;
        if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
        seteaw((uint16_t)((cpu_state.npxs & 0xC7FF) | ((cpu_state.TOP & 7) << 11)));
        _386.CLOCK_CYCLES(x87_timings_c.x87_timings.fstcw_sw);
        return cpu_state.abrt;
    }

    // FNSTSW AX : le mot d'état, TOP compris (287, 387, 486 ; le 8087 n'a pas DF E0 : N7, PB-200).
    internal static int opFNSTSW_AX(uint32_t fetchdat)
    {
        if (_386.FP_ENTER()) return 1;
        cpu_state.pc++;
        x86.AX = (uint16_t)((cpu_state.npxs & 0xC7FF) | ((cpu_state.TOP & 7) << 11));
        _386.CLOCK_CYCLES(x87_timings_c.x87_timings.fstcw_sw);
        return 0;
    }

    // FNOP.
    internal static int opFNOP(uint32_t fetchdat)
    {
        if (_386.FP_ENTER()) return 1;
        cpu_state.pc++;
        _386.CLOCK_CYCLES(x87_timings_c.x87_timings.fnop);
        return 0;
    }

    // FINCSTP : TOP monte, sans toucher aux étiquettes ni aux registres ; C1 à zéro sur le 387 et le 486 (486 PRM,
    //   table 15-1 ; le 287XL : déduit), indéfini sur le 8087 et le 287, laissé.
    internal static int opFINCSTP(uint32_t fetchdat)
    {
        if (_386.FP_ENTER()) return 1;
        cpu_state.pc++;
        cpu_state.TOP = (cpu_state.TOP + 1) & 7;
        if (cpu_c.fpu_type >= cpu_c.FPU_287XL)
                cpu_state.npxs &= unchecked((uint16_t)~_x87_noyau.C1);
        _386.CLOCK_CYCLES(x87_timings_c.x87_timings.fincdecstp);
        return 0;
    }

    // FDECSTP : TOP descend, sans toucher aux étiquettes ni aux registres ; C1 à zéro sur le 387 et le 486 (486 PRM,
    //   table 15-1 ; le 287XL : déduit), indéfini sur le 8087 et le 287, laissé.
    internal static int opFDECSTP(uint32_t fetchdat)
    {
        if (_386.FP_ENTER()) return 1;
        cpu_state.pc++;
        cpu_state.TOP = (cpu_state.TOP - 1) & 7;
        if (cpu_c.fpu_type >= cpu_c.FPU_287XL)
                cpu_state.npxs &= unchecked((uint16_t)~_x87_noyau.C1);
        _386.CLOCK_CYCLES(x87_timings_c.x87_timings.fincdecstp);
        return 0;
    }

    // FFREE ST(i) : l'étiquette de ST(i) vide ; TOP et le registre gardés.
    internal static int opFFREE(uint32_t fetchdat)
    {
        if (_386.FP_ENTER()) return 1;
        cpu_state.pc++;
        _x87_noyau.etiqueter(_x87_noyau.phys((int)(fetchdat & 7)), _x87_noyau.VIDE);
        _386.CLOCK_CYCLES(x87_timings_c.x87_timings.ffree);
        return 0;
    }

    /// <summary>Les seize tables du noyau, qui remplacent celles du mode : chaque instruction écrite à son entrée,
    /// les autres à l'arrêt (_x87_noyau.a_ecrire).</summary>
    internal static void poser_tables()
    {
        _386.x86_opcodes_d8_a16 = _x87_noyau.table(32, 0xD8);
        _386.x86_opcodes_d8_a32 = _x87_noyau.table(32, 0xD8);
        _386.x86_opcodes_d9_a16 = _x87_noyau.table(256, 0xD9);
        _386.x86_opcodes_d9_a32 = _x87_noyau.table(256, 0xD9);
        _386.x86_opcodes_da_a16 = _x87_noyau.table(256, 0xDA);
        _386.x86_opcodes_da_a32 = _x87_noyau.table(256, 0xDA);
        _386.x86_opcodes_db_a16 = _x87_noyau.table(256, 0xDB);
        _386.x86_opcodes_db_a32 = _x87_noyau.table(256, 0xDB);
        _386.x86_opcodes_dc_a16 = _x87_noyau.table(32, 0xDC);
        _386.x86_opcodes_dc_a32 = _x87_noyau.table(32, 0xDC);
        _386.x86_opcodes_dd_a16 = _x87_noyau.table(256, 0xDD);
        _386.x86_opcodes_dd_a32 = _x87_noyau.table(256, 0xDD);
        _386.x86_opcodes_de_a16 = _x87_noyau.table(256, 0xDE);
        _386.x86_opcodes_de_a32 = _x87_noyau.table(256, 0xDE);
        _386.x86_opcodes_df_a16 = _x87_noyau.table(256, 0xDF);
        _386.x86_opcodes_df_a32 = _x87_noyau.table(256, 0xDF);
        _386.x86_opcodes_db_a16![0xE3] = opFNINIT;
        _386.x86_opcodes_db_a32![0xE3] = opFNINIT;
        _386.x86_opcodes_db_a16![0xE2] = opFNCLEX;
        _386.x86_opcodes_db_a32![0xE2] = opFNCLEX;
        _x87_noyau.memoire(_386.x86_opcodes_d9_a16!, 5, opFLDCW_a16);
        _x87_noyau.memoire(_386.x86_opcodes_d9_a32!, 5, opFLDCW_a32);
        _x87_noyau.memoire(_386.x86_opcodes_d9_a16!, 7, opFNSTCW_a16);
        _x87_noyau.memoire(_386.x86_opcodes_d9_a32!, 7, opFNSTCW_a32);
        _x87_noyau.memoire(_386.x86_opcodes_dd_a16!, 7, opFNSTSW_a16);
        _x87_noyau.memoire(_386.x86_opcodes_dd_a32!, 7, opFNSTSW_a32);
        _386.x86_opcodes_df_a16![0xE0] = opFNSTSW_AX;
        _386.x86_opcodes_df_a32![0xE0] = opFNSTSW_AX;
        _386.x86_opcodes_d9_a16![0xD0] = opFNOP;
        _386.x86_opcodes_d9_a32![0xD0] = opFNOP;
        _386.x86_opcodes_d9_a16![0xF7] = opFINCSTP;
        _386.x86_opcodes_d9_a32![0xF7] = opFINCSTP;
        _386.x86_opcodes_d9_a16![0xF6] = opFDECSTP;
        _386.x86_opcodes_d9_a32![0xF6] = opFDECSTP;
        _386.x86_opcodes_dd_a16![0xC0] = opFFREE;
        _386.x86_opcodes_dd_a32![0xC0] = opFFREE;
        _386.x86_opcodes_dd_a16![0xC1] = opFFREE;
        _386.x86_opcodes_dd_a32![0xC1] = opFFREE;
        _386.x86_opcodes_dd_a16![0xC2] = opFFREE;
        _386.x86_opcodes_dd_a32![0xC2] = opFFREE;
        _386.x86_opcodes_dd_a16![0xC3] = opFFREE;
        _386.x86_opcodes_dd_a32![0xC3] = opFFREE;
        _386.x86_opcodes_dd_a16![0xC4] = opFFREE;
        _386.x86_opcodes_dd_a32![0xC4] = opFFREE;
        _386.x86_opcodes_dd_a16![0xC5] = opFFREE;
        _386.x86_opcodes_dd_a32![0xC5] = opFFREE;
        _386.x86_opcodes_dd_a16![0xC6] = opFFREE;
        _386.x86_opcodes_dd_a32![0xC6] = opFFREE;
        _386.x86_opcodes_dd_a16![0xC7] = opFFREE;
        _386.x86_opcodes_dd_a32![0xC7] = opFFREE;
    }
}
