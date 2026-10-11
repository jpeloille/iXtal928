// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — le noyau de 80 bits du x87, ses gestionnaires du 8087, a16 (G13.6, le noyau ; PLAN-G13.md).
// STATUS: materiel
//
// GÉNÉRÉ par tools/x87gen/genmat.py, d'une description partagée avec x87_noyau_386.Materiel.cs ; ne pas éditer à la
// main. La couche commune (l'état, la pile, les exceptions, l'arrondi) est dans x87_noyau.Materiel.cs. Une classe à
// part : des membres de plus dans _808x renuméroteraient ses lambdas.

using static iXtal26.Cpu._386_common;

namespace iXtal26.Cpu;

internal static class _808x_noyau
{
    // FNINIT : l'état de FNINIT selon le type (_x87_noyau.finit) ; sur le 8087, IR effacé, sa sortie INT retombe.
    internal static int opFNINIT(uint32_t fetchdat)
    {
        if (_808x.FP_ENTER()) return 1;
        cpu_state.pc++;
        _x87_noyau.finit();
        _x87_noyau.int_retombe();
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.finit);
        _386.CPU_BLOCK_END();
        return 0;
    }

    // FNCLEX : les six exceptions, SF, ES et B effacés (387 PRM, FCLEX ; 287 PRM, FCLEX : B compris) ; C3-C0 et
    //   TOP gardés ; sur le 8087, la sortie INT retombe.
    internal static int opFNCLEX(uint32_t fetchdat)
    {
        if (_808x.FP_ENTER()) return 1;
        cpu_state.pc++;
        cpu_state.npxs &= 0x7F00;
        _x87_noyau.int_retombe();
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.fclex);
        return 0;
    }

    // FLDCW m16 : le mot de contrôle. Démasquer une exception en attente active ERROR# (387 PRM § 4.8.2) : N5.
    internal static int opFLDCW_a16(uint32_t fetchdat)
    {
        uint16_t tempw;
        if (_808x.FP_ENTER()) return 1;
        if (_808x.fetch_ea_16(fetchdat)) return 1;
        if (_808x.SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        tempw = _808x.geteaw();
        if (cpu_state.abrt != 0)
                return 1;
        cpu_state.npxc = tempw;
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.fldcw);
        return 0;
    }

    // FNSTCW m16.
    internal static int opFNSTCW_a16(uint32_t fetchdat)
    {
        if (_808x.FP_ENTER()) return 1;
        if (_808x.fetch_ea_16(fetchdat)) return 1;
        if (_808x.SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
        _808x.seteaw(cpu_state.npxc);
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.fstcw_sw);
        return cpu_state.abrt;
    }

    // FNSTSW m16 : le mot d'état, TOP compris.
    internal static int opFNSTSW_a16(uint32_t fetchdat)
    {
        if (_808x.FP_ENTER()) return 1;
        if (_808x.fetch_ea_16(fetchdat)) return 1;
        if (_808x.SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
        _808x.seteaw((uint16_t)((cpu_state.npxs & 0xC7FF) | ((cpu_state.TOP & 7) << 11)));
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.fstcw_sw);
        return cpu_state.abrt;
    }

    // FNOP.
    internal static int opFNOP(uint32_t fetchdat)
    {
        if (_808x.FP_ENTER()) return 1;
        cpu_state.pc++;
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.fnop);
        return 0;
    }

    // FINCSTP : TOP monte, sans toucher aux étiquettes ni aux registres ; C1 à zéro sur le 387 et le 486 (486 PRM,
    //   table 15-1 ; le 287XL : déduit), indéfini sur le 8087 et le 287, laissé.
    internal static int opFINCSTP(uint32_t fetchdat)
    {
        if (_808x.FP_ENTER()) return 1;
        cpu_state.pc++;
        cpu_state.TOP = (cpu_state.TOP + 1) & 7;
        if (cpu_c.fpu_type >= cpu_c.FPU_287XL)
                cpu_state.npxs &= unchecked((uint16_t)~_x87_noyau.C1);
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.fincdecstp);
        return 0;
    }

    // FDECSTP : TOP descend, sans toucher aux étiquettes ni aux registres ; C1 à zéro sur le 387 et le 486 (486 PRM,
    //   table 15-1 ; le 287XL : déduit), indéfini sur le 8087 et le 287, laissé.
    internal static int opFDECSTP(uint32_t fetchdat)
    {
        if (_808x.FP_ENTER()) return 1;
        cpu_state.pc++;
        cpu_state.TOP = (cpu_state.TOP - 1) & 7;
        if (cpu_c.fpu_type >= cpu_c.FPU_287XL)
                cpu_state.npxs &= unchecked((uint16_t)~_x87_noyau.C1);
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.fincdecstp);
        return 0;
    }

    // FFREE ST(i) : l'étiquette de ST(i) vide ; TOP et le registre gardés.
    internal static int opFFREE(uint32_t fetchdat)
    {
        if (_808x.FP_ENTER()) return 1;
        cpu_state.pc++;
        _x87_noyau.etiqueter(_x87_noyau.phys((int)(fetchdat & 7)), _x87_noyau.VIDE);
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.ffree);
        return 0;
    }

    /// <summary>Les huit tables du 8087, remplacées en place (execx86 les indexe en dur) : chaque instruction écrite
    /// à son entrée, les autres à l'arrêt (_x87_noyau.a_ecrire).</summary>
    internal static void poser_tables()
    {
        _x87_noyau.remplir(_808x.ops_808x_fpu_d8_a16, 0xD8);
        _x87_noyau.remplir(_808x.ops_808x_fpu_d9_a16, 0xD9);
        _x87_noyau.remplir(_808x.ops_808x_fpu_da_a16, 0xDA);
        _x87_noyau.remplir(_808x.ops_808x_fpu_db_a16, 0xDB);
        _x87_noyau.remplir(_808x.ops_808x_fpu_dc_a16, 0xDC);
        _x87_noyau.remplir(_808x.ops_808x_fpu_dd_a16, 0xDD);
        _x87_noyau.remplir(_808x.ops_808x_fpu_de_a16, 0xDE);
        _x87_noyau.remplir(_808x.ops_808x_fpu_df_a16, 0xDF);
        _808x.ops_808x_fpu_db_a16[0xE3] = opFNINIT;
        _808x.ops_808x_fpu_db_a16[0xE2] = opFNCLEX;
        _x87_noyau.memoire(_808x.ops_808x_fpu_d9_a16, 5, opFLDCW_a16);
        _x87_noyau.memoire(_808x.ops_808x_fpu_d9_a16, 7, opFNSTCW_a16);
        _x87_noyau.memoire(_808x.ops_808x_fpu_dd_a16, 7, opFNSTSW_a16);
        _808x.ops_808x_fpu_d9_a16[0xD0] = opFNOP;
        _808x.ops_808x_fpu_d9_a16[0xF7] = opFINCSTP;
        _808x.ops_808x_fpu_d9_a16[0xF6] = opFDECSTP;
        _808x.ops_808x_fpu_dd_a16[0xC0] = opFFREE;
        _808x.ops_808x_fpu_dd_a16[0xC1] = opFFREE;
        _808x.ops_808x_fpu_dd_a16[0xC2] = opFFREE;
        _808x.ops_808x_fpu_dd_a16[0xC3] = opFFREE;
        _808x.ops_808x_fpu_dd_a16[0xC4] = opFFREE;
        _808x.ops_808x_fpu_dd_a16[0xC5] = opFFREE;
        _808x.ops_808x_fpu_dd_a16[0xC6] = opFFREE;
        _808x.ops_808x_fpu_dd_a16[0xC7] = opFFREE;
    }
}
