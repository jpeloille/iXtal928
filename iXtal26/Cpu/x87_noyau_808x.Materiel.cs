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

    // FLDCW m16 : le mot de contrôle. Démasquer une exception en attente active ERROR# (387 PRM § 4.8.2) : N5. Les bits
    //   réservés (6 et 13 à 15, et 7, l'IEM du 8087, sur les autres) restent tels qu'écrits, relus par FNSTCW,
    //   comme PCem : ce que le silicium en relit n'est pas documenté (inconnu).
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
        _x87_noyau.pointeurs_8087(0xD9, fetchdat, false);
        cpu_state.pc++;
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.fnop);
        return 0;
    }

    // FINCSTP : TOP monte, sans toucher aux étiquettes ni aux registres ; C1 à zéro sur le 387 et le 486 (486 PRM,
    //   table 15-1 ; le 287XL : déduit), indéfini sur le 8087 et le 287, laissé.
    internal static int opFINCSTP(uint32_t fetchdat)
    {
        if (_808x.FP_ENTER()) return 1;
        _x87_noyau.pointeurs_8087(0xD9, fetchdat, false);
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
        _x87_noyau.pointeurs_8087(0xD9, fetchdat, false);
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
        _x87_noyau.pointeurs_8087(0xDD, fetchdat, false);
        cpu_state.pc++;
        _x87_noyau.etiqueter(_x87_noyau.phys((int)(fetchdat & 7)), _x87_noyau.VIDE);
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.ffree);
        return 0;
    }

    // FLD m32 (_x87_noyau.fld_f32).
    internal static int opFLDs_a16(uint32_t fetchdat)
    {
        uint32_t t;
        if (_808x.FP_ENTER()) return 1;
        if (_808x.fetch_ea_16(fetchdat)) return 1;
        if (_808x.SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        _x87_noyau.pointeurs_8087(0xD9, fetchdat, true);
        t = _808x.geteal();
        if (cpu_state.abrt != 0) return 1;
        _x87_noyau.fld_f32(t);
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.fld_32);
        return 0;
    }

    // FLD m64 (_x87_noyau.fld_f64).
    internal static int opFLDd_a16(uint32_t fetchdat)
    {
        uint64_t t;
        if (_808x.FP_ENTER()) return 1;
        if (_808x.fetch_ea_16(fetchdat)) return 1;
        if (_808x.SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        _x87_noyau.pointeurs_8087(0xDD, fetchdat, true);
        t = _808x.geteaq();
        if (cpu_state.abrt != 0) return 1;
        _x87_noyau.fld_f64(t);
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.fld_64);
        return 0;
    }

    // FLD m80 : les 80 bits tels quels (_x87_noyau.fld_f80).
    internal static int opFLDe_a16(uint32_t fetchdat)
    {
        uint64_t s; uint16_t e;
        if (_808x.FP_ENTER()) return 1;
        if (_808x.fetch_ea_16(fetchdat)) return 1;
        if (_808x.SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        _x87_noyau.pointeurs_8087(0xDB, fetchdat, true);
        s = _808x.readmemq(x86.easeg, cpu_state.eaaddr);
        e = _808x.readmemw(x86.easeg, cpu_state.eaaddr + 8);
        if (cpu_state.abrt != 0) return 1;
        _x87_noyau.fld_f80(s, e);
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.fld_80);
        return 0;
    }

    // FILD m16.
    internal static int opFILDiw_a16(uint32_t fetchdat)
    {
        uint16_t t;
        if (_808x.FP_ENTER()) return 1;
        if (_808x.fetch_ea_16(fetchdat)) return 1;
        if (_808x.SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        _x87_noyau.pointeurs_8087(0xDF, fetchdat, true);
        t = _808x.geteaw();
        if (cpu_state.abrt != 0) return 1;
        _x87_noyau.fild((int16_t)t);
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.fild_16);
        return 0;
    }

    // FILD m32.
    internal static int opFILDil_a16(uint32_t fetchdat)
    {
        uint32_t t;
        if (_808x.FP_ENTER()) return 1;
        if (_808x.fetch_ea_16(fetchdat)) return 1;
        if (_808x.SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        _x87_noyau.pointeurs_8087(0xDB, fetchdat, true);
        t = _808x.geteal();
        if (cpu_state.abrt != 0) return 1;
        _x87_noyau.fild((int32_t)t);
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.fild_32);
        return 0;
    }

    // FILD m64.
    internal static int opFILDiq_a16(uint32_t fetchdat)
    {
        uint64_t t;
        if (_808x.FP_ENTER()) return 1;
        if (_808x.fetch_ea_16(fetchdat)) return 1;
        if (_808x.SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        _x87_noyau.pointeurs_8087(0xDF, fetchdat, true);
        t = _808x.geteaq();
        if (cpu_state.abrt != 0) return 1;
        _x87_noyau.fild((int64_t)t);
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.fild_64);
        return 0;
    }

    // FBLD m80 : dix-huit chiffres décimaux (_x87_noyau.fbld ; PB-52).
    internal static int opFBLD_a16(uint32_t fetchdat)
    {
        uint64_t s; uint16_t e;
        if (_808x.FP_ENTER()) return 1;
        if (_808x.fetch_ea_16(fetchdat)) return 1;
        if (_808x.SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        _x87_noyau.pointeurs_8087(0xDF, fetchdat, true);
        s = _808x.readmemq(x86.easeg, cpu_state.eaaddr);
        e = _808x.readmemw(x86.easeg, cpu_state.eaaddr + 8);
        if (cpu_state.abrt != 0) return 1;
        _x87_noyau.fbld(s, e);
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.fbld);
        return 0;
    }

    // FST m32 (_x87_noyau.fst_f32).
    internal static int opFSTs_a16(uint32_t fetchdat)
    {
        if (_808x.FP_ENTER()) return 1;
        if (_808x.fetch_ea_16(fetchdat)) return 1;
        if (_808x.SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
        _x87_noyau.pointeurs_8087(0xD9, fetchdat, true);
        if (!_x87_noyau.fst_f32(out var t))
        {
                _808x.seteal(t);
                if (cpu_state.abrt != 0) return 1;
        }
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.fst_32);
        return 0;
    }

    // FSTP m32 (_x87_noyau.fst_f32).
    internal static int opFSTPs_a16(uint32_t fetchdat)
    {
        if (_808x.FP_ENTER()) return 1;
        if (_808x.fetch_ea_16(fetchdat)) return 1;
        if (_808x.SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
        _x87_noyau.pointeurs_8087(0xD9, fetchdat, true);
        if (!_x87_noyau.fst_f32(out var t))
        {
                _808x.seteal(t);
                if (cpu_state.abrt != 0) return 1;
                _x87_noyau.depiler();
        }
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.fst_32);
        return 0;
    }

    // FST m64 (_x87_noyau.fst_f64).
    internal static int opFSTd_a16(uint32_t fetchdat)
    {
        if (_808x.FP_ENTER()) return 1;
        if (_808x.fetch_ea_16(fetchdat)) return 1;
        if (_808x.SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
        _x87_noyau.pointeurs_8087(0xDD, fetchdat, true);
        if (!_x87_noyau.fst_f64(out var t))
        {
                _808x.seteaq(t);
                if (cpu_state.abrt != 0) return 1;
        }
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.fst_64);
        return 0;
    }

    // FSTP m64 (_x87_noyau.fst_f64).
    internal static int opFSTPd_a16(uint32_t fetchdat)
    {
        if (_808x.FP_ENTER()) return 1;
        if (_808x.fetch_ea_16(fetchdat)) return 1;
        if (_808x.SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
        _x87_noyau.pointeurs_8087(0xDD, fetchdat, true);
        if (!_x87_noyau.fst_f64(out var t))
        {
                _808x.seteaq(t);
                if (cpu_state.abrt != 0) return 1;
                _x87_noyau.depiler();
        }
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.fst_64);
        return 0;
    }

    // FSTP m80 : les 80 bits tels quels (_x87_noyau.fst_f80).
    internal static int opFSTPe_a16(uint32_t fetchdat)
    {
        if (_808x.FP_ENTER()) return 1;
        if (_808x.fetch_ea_16(fetchdat)) return 1;
        if (_808x.SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
        _x87_noyau.pointeurs_8087(0xDB, fetchdat, true);
        if (!_x87_noyau.fst_f80(out var t))
        {
                _808x.writememq(x86.easeg, cpu_state.eaaddr, t.signif);
                _808x.writememw(x86.easeg, cpu_state.eaaddr + 8, t.signExp);
                if (cpu_state.abrt != 0) return 1;
                _x87_noyau.depiler();
        }
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.fst_80);
        return 0;
    }

    // FIST m16 (_x87_noyau.fist).
    internal static int opFISTiw_a16(uint32_t fetchdat)
    {
        if (_808x.FP_ENTER()) return 1;
        if (_808x.fetch_ea_16(fetchdat)) return 1;
        if (_808x.SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
        _x87_noyau.pointeurs_8087(0xDF, fetchdat, true);
        if (!_x87_noyau.fist(16, out var t))
        {
                _808x.seteaw((uint16_t)t);
                if (cpu_state.abrt != 0) return 1;
        }
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.fist_16);
        return 0;
    }

    // FISTP m16 (_x87_noyau.fist).
    internal static int opFISTPiw_a16(uint32_t fetchdat)
    {
        if (_808x.FP_ENTER()) return 1;
        if (_808x.fetch_ea_16(fetchdat)) return 1;
        if (_808x.SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
        _x87_noyau.pointeurs_8087(0xDF, fetchdat, true);
        if (!_x87_noyau.fist(16, out var t))
        {
                _808x.seteaw((uint16_t)t);
                if (cpu_state.abrt != 0) return 1;
                _x87_noyau.depiler();
        }
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.fist_16);
        return 0;
    }

    // FIST m32 (_x87_noyau.fist).
    internal static int opFISTil_a16(uint32_t fetchdat)
    {
        if (_808x.FP_ENTER()) return 1;
        if (_808x.fetch_ea_16(fetchdat)) return 1;
        if (_808x.SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
        _x87_noyau.pointeurs_8087(0xDB, fetchdat, true);
        if (!_x87_noyau.fist(32, out var t))
        {
                _808x.seteal((uint32_t)t);
                if (cpu_state.abrt != 0) return 1;
        }
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.fist_32);
        return 0;
    }

    // FISTP m32 (_x87_noyau.fist).
    internal static int opFISTPil_a16(uint32_t fetchdat)
    {
        if (_808x.FP_ENTER()) return 1;
        if (_808x.fetch_ea_16(fetchdat)) return 1;
        if (_808x.SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
        _x87_noyau.pointeurs_8087(0xDB, fetchdat, true);
        if (!_x87_noyau.fist(32, out var t))
        {
                _808x.seteal((uint32_t)t);
                if (cpu_state.abrt != 0) return 1;
                _x87_noyau.depiler();
        }
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.fist_32);
        return 0;
    }

    // FISTP m64 (_x87_noyau.fist).
    internal static int opFISTPiq_a16(uint32_t fetchdat)
    {
        if (_808x.FP_ENTER()) return 1;
        if (_808x.fetch_ea_16(fetchdat)) return 1;
        if (_808x.SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
        _x87_noyau.pointeurs_8087(0xDF, fetchdat, true);
        if (!_x87_noyau.fist(64, out var t))
        {
                _808x.seteaq((uint64_t)t);
                if (cpu_state.abrt != 0) return 1;
                _x87_noyau.depiler();
        }
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.fist_64);
        return 0;
    }

    // FBSTP m80 (_x87_noyau.fbstp ; PB-195).
    internal static int opFBSTP_a16(uint32_t fetchdat)
    {
        if (_808x.FP_ENTER()) return 1;
        if (_808x.fetch_ea_16(fetchdat)) return 1;
        if (_808x.SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
        _x87_noyau.pointeurs_8087(0xDF, fetchdat, true);
        if (!_x87_noyau.fbstp(out var s, out var e))
        {
                _808x.writememq(x86.easeg, cpu_state.eaaddr, s);
                _808x.writememw(x86.easeg, cpu_state.eaaddr + 8, e);
                if (cpu_state.abrt != 0) return 1;
                _x87_noyau.depiler();
        }
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.fbstp);
        return 0;
    }

    // FLD ST(i).
    internal static int opFLD(uint32_t fetchdat)
    {
        if (_808x.FP_ENTER()) return 1;
        _x87_noyau.pointeurs_8087(0xD9, fetchdat, false);
        cpu_state.pc++;
        _x87_noyau.fld_st((int)(fetchdat & 7));
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.fld);
        return 0;
    }

    // FXCH ST(i).
    internal static int opFXCH(uint32_t fetchdat)
    {
        if (_808x.FP_ENTER()) return 1;
        _x87_noyau.pointeurs_8087(0xD9, fetchdat, false);
        cpu_state.pc++;
        _x87_noyau.fxch((int)(fetchdat & 7));
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.fxch);
        return 0;
    }

    // FST ST(i).
    internal static int opFST(uint32_t fetchdat)
    {
        if (_808x.FP_ENTER()) return 1;
        _x87_noyau.pointeurs_8087(0xDD, fetchdat, false);
        cpu_state.pc++;
        _x87_noyau.fst_st((int)(fetchdat & 7));
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.fst);
        return 0;
    }

    // FSTP ST(i).
    internal static int opFSTP(uint32_t fetchdat)
    {
        if (_808x.FP_ENTER()) return 1;
        _x87_noyau.pointeurs_8087(0xDD, fetchdat, false);
        cpu_state.pc++;
        if (!_x87_noyau.fst_st((int)(fetchdat & 7)))
                _x87_noyau.depiler();
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.fst);
        return 0;
    }

    // FSTP ST(i), D9 D8+i comme PCem.
    internal static int opFSTP_d9(uint32_t fetchdat)
    {
        if (_808x.FP_ENTER()) return 1;
        _x87_noyau.pointeurs_8087(0xD9, fetchdat, false);
        cpu_state.pc++;
        if (!_x87_noyau.fst_st((int)(fetchdat & 7)))
                _x87_noyau.depiler();
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.fst);
        return 0;
    }

    // FCHS.
    internal static int opFCHS(uint32_t fetchdat)
    {
        if (_808x.FP_ENTER()) return 1;
        _x87_noyau.pointeurs_8087(0xD9, fetchdat, false);
        cpu_state.pc++;
        _x87_noyau.signe(false);
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.fchs);
        return 0;
    }

    // FABS.
    internal static int opFABS(uint32_t fetchdat)
    {
        if (_808x.FP_ENTER()) return 1;
        _x87_noyau.pointeurs_8087(0xD9, fetchdat, false);
        cpu_state.pc++;
        _x87_noyau.signe(true);
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.fabs);
        return 0;
    }

    // FXAM (_x87_noyau.fxam).
    internal static int opFXAM(uint32_t fetchdat)
    {
        if (_808x.FP_ENTER()) return 1;
        _x87_noyau.pointeurs_8087(0xD9, fetchdat, false);
        cpu_state.pc++;
        _x87_noyau.fxam();
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.fxam);
        return 0;
    }

    // FLD1 et FLDZ.
    internal static int opFLDz1(uint32_t fetchdat)
    {
        if (_808x.FP_ENTER()) return 1;
        _x87_noyau.pointeurs_8087(0xD9, fetchdat, false);
        cpu_state.pc++;
        _x87_noyau.constante((int)(fetchdat & 7));
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.fld_z1);
        return 0;
    }

    // FLDL2T, FLDL2E, FLDPI, FLDLG2, FLDLN2 (_x87_noyau.constante).
    internal static int opFLDconst(uint32_t fetchdat)
    {
        if (_808x.FP_ENTER()) return 1;
        _x87_noyau.pointeurs_8087(0xD9, fetchdat, false);
        cpu_state.pc++;
        _x87_noyau.constante((int)(fetchdat & 7));
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.fld_const);
        return 0;
    }

    // FLDENV : l'environnement, dans la disposition du mode (_x87_noyau.fldenv ; PB-62, PB-208).
    internal static int opFLDENV_a16(uint32_t fetchdat)
    {
        int d;
        if (_808x.FP_ENTER()) return 1;
        if (_808x.fetch_ea_16(fetchdat)) return 1;
        if (_808x.SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        d = _x87_noyau.disposition();
        _x87_noyau.a_lire(d, false);
        for (var k = 0; k < _x87_noyau.n; k++)
        {
                _x87_noyau.mot[k] = _808x.readmemw(x86.easeg, cpu_state.eaaddr + _x87_noyau.off[k]);
                if (cpu_state.abrt != 0) return 1;
        }
        _x87_noyau.fldenv(d, false);
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.fldenv);
        return 0;
    }

    // FNSTENV : l'environnement, puis les six exceptions masquées (PB-62 ; PB-207).
    internal static int opFNSTENV_a16(uint32_t fetchdat)
    {
        int d;
        if (_808x.FP_ENTER()) return 1;
        if (_808x.fetch_ea_16(fetchdat)) return 1;
        if (_808x.SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
        d = _x87_noyau.disposition();
        _x87_noyau.fstenv(d);
        for (var k = 0; k < _x87_noyau.n; k++)
        {
                _808x.writememw(x86.easeg, cpu_state.eaaddr + _x87_noyau.off[k], _x87_noyau.mot[k]);
                if (cpu_state.abrt != 0) return 1;
        }
        _x87_noyau.fstenv_fin();
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.fstenv);
        return 0;
    }

    // FRSTOR : l'environnement et les huit registres (PB-62, PB-199, PB-208).
    internal static int opFRSTOR_a16(uint32_t fetchdat)
    {
        int d;
        if (_808x.FP_ENTER()) return 1;
        if (_808x.fetch_ea_16(fetchdat)) return 1;
        if (_808x.SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        d = _x87_noyau.disposition();
        _x87_noyau.a_lire(d, true);
        for (var k = 0; k < _x87_noyau.n; k++)
        {
                _x87_noyau.mot[k] = _808x.readmemw(x86.easeg, cpu_state.eaaddr + _x87_noyau.off[k]);
                if (cpu_state.abrt != 0) return 1;
        }
        _x87_noyau.fldenv(d, true);
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.frstor);
        return 0;
    }

    // FNSAVE : l'environnement et les huit registres de 80 bits, puis l'état de FNINIT (PB-62, PB-199).
    internal static int opFNSAVE_a16(uint32_t fetchdat)
    {
        int d;
        if (_808x.FP_ENTER()) return 1;
        if (_808x.fetch_ea_16(fetchdat)) return 1;
        if (_808x.SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
        d = _x87_noyau.disposition();
        _x87_noyau.fsave(d);
        for (var k = 0; k < _x87_noyau.n; k++)
        {
                _808x.writememw(x86.easeg, cpu_state.eaaddr + _x87_noyau.off[k], _x87_noyau.mot[k]);
                if (cpu_state.abrt != 0) return 1;
        }
        _x87_noyau.fsave_fin();
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.fsave);
        return 0;
    }

    // FADD m32 (_x87_noyau.arith_m ; PB-48, PB-60).
    internal static int opFADDs_a16(uint32_t fetchdat)
    {
        uint32_t t;
        if (_808x.FP_ENTER()) return 1;
        if (_808x.fetch_ea_16(fetchdat)) return 1;
        if (_808x.SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        _x87_noyau.pointeurs_8087(0xD8, fetchdat, true);
        t = _808x.geteal();
        if (cpu_state.abrt != 0) return 1;
        _x87_noyau.arith_m(_x87_noyau.ADD, false, _x87_noyau.de_f32(t, out var den), den);
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.fadd_32);
        return 0;
    }

    // FMUL m32 (_x87_noyau.arith_m ; PB-48, PB-60).
    internal static int opFMULs_a16(uint32_t fetchdat)
    {
        uint32_t t;
        if (_808x.FP_ENTER()) return 1;
        if (_808x.fetch_ea_16(fetchdat)) return 1;
        if (_808x.SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        _x87_noyau.pointeurs_8087(0xD8, fetchdat, true);
        t = _808x.geteal();
        if (cpu_state.abrt != 0) return 1;
        _x87_noyau.arith_m(_x87_noyau.MUL, false, _x87_noyau.de_f32(t, out var den), den);
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.fmul_32);
        return 0;
    }

    // FSUB m32 (_x87_noyau.arith_m ; PB-48, PB-60).
    internal static int opFSUBs_a16(uint32_t fetchdat)
    {
        uint32_t t;
        if (_808x.FP_ENTER()) return 1;
        if (_808x.fetch_ea_16(fetchdat)) return 1;
        if (_808x.SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        _x87_noyau.pointeurs_8087(0xD8, fetchdat, true);
        t = _808x.geteal();
        if (cpu_state.abrt != 0) return 1;
        _x87_noyau.arith_m(_x87_noyau.SUB, false, _x87_noyau.de_f32(t, out var den), den);
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.fadd_32);
        return 0;
    }

    // FSUBR m32 (_x87_noyau.arith_m ; PB-48, PB-60).
    internal static int opFSUBRs_a16(uint32_t fetchdat)
    {
        uint32_t t;
        if (_808x.FP_ENTER()) return 1;
        if (_808x.fetch_ea_16(fetchdat)) return 1;
        if (_808x.SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        _x87_noyau.pointeurs_8087(0xD8, fetchdat, true);
        t = _808x.geteal();
        if (cpu_state.abrt != 0) return 1;
        _x87_noyau.arith_m(_x87_noyau.SUB, true, _x87_noyau.de_f32(t, out var den), den);
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.fadd_32);
        return 0;
    }

    // FDIV m32 (_x87_noyau.arith_m ; PB-48, PB-60).
    internal static int opFDIVs_a16(uint32_t fetchdat)
    {
        uint32_t t;
        if (_808x.FP_ENTER()) return 1;
        if (_808x.fetch_ea_16(fetchdat)) return 1;
        if (_808x.SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        _x87_noyau.pointeurs_8087(0xD8, fetchdat, true);
        t = _808x.geteal();
        if (cpu_state.abrt != 0) return 1;
        _x87_noyau.arith_m(_x87_noyau.DIV, false, _x87_noyau.de_f32(t, out var den), den);
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.fdiv_32);
        return 0;
    }

    // FDIVR m32 (_x87_noyau.arith_m ; PB-48, PB-60).
    internal static int opFDIVRs_a16(uint32_t fetchdat)
    {
        uint32_t t;
        if (_808x.FP_ENTER()) return 1;
        if (_808x.fetch_ea_16(fetchdat)) return 1;
        if (_808x.SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        _x87_noyau.pointeurs_8087(0xD8, fetchdat, true);
        t = _808x.geteal();
        if (cpu_state.abrt != 0) return 1;
        _x87_noyau.arith_m(_x87_noyau.DIV, true, _x87_noyau.de_f32(t, out var den), den);
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.fdiv_32);
        return 0;
    }

    // FCOM m32 (_x87_noyau.comparer).
    internal static int opFCOMs_a16(uint32_t fetchdat)
    {
        uint32_t t;
        if (_808x.FP_ENTER()) return 1;
        if (_808x.fetch_ea_16(fetchdat)) return 1;
        if (_808x.SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        _x87_noyau.pointeurs_8087(0xD8, fetchdat, true);
        t = _808x.geteal();
        if (cpu_state.abrt != 0) return 1;
        _x87_noyau.fcom_m(_x87_noyau.de_f32(t, out var den), den);
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.fcom_32);
        return 0;
    }

    // FCOMP m32 (_x87_noyau.comparer).
    internal static int opFCOMPs_a16(uint32_t fetchdat)
    {
        uint32_t t;
        if (_808x.FP_ENTER()) return 1;
        if (_808x.fetch_ea_16(fetchdat)) return 1;
        if (_808x.SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        _x87_noyau.pointeurs_8087(0xD8, fetchdat, true);
        t = _808x.geteal();
        if (cpu_state.abrt != 0) return 1;
        if (!_x87_noyau.fcom_m(_x87_noyau.de_f32(t, out var den), den))
                _x87_noyau.depiler();
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.fcom_32);
        return 0;
    }

    // FADD m64 (_x87_noyau.arith_m ; PB-48, PB-60).
    internal static int opFADDd_a16(uint32_t fetchdat)
    {
        uint64_t t;
        if (_808x.FP_ENTER()) return 1;
        if (_808x.fetch_ea_16(fetchdat)) return 1;
        if (_808x.SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        _x87_noyau.pointeurs_8087(0xDC, fetchdat, true);
        t = _808x.geteaq();
        if (cpu_state.abrt != 0) return 1;
        _x87_noyau.arith_m(_x87_noyau.ADD, false, _x87_noyau.de_f64(t, out var den), den);
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.fadd_64);
        return 0;
    }

    // FMUL m64 (_x87_noyau.arith_m ; PB-48, PB-60).
    internal static int opFMULd_a16(uint32_t fetchdat)
    {
        uint64_t t;
        if (_808x.FP_ENTER()) return 1;
        if (_808x.fetch_ea_16(fetchdat)) return 1;
        if (_808x.SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        _x87_noyau.pointeurs_8087(0xDC, fetchdat, true);
        t = _808x.geteaq();
        if (cpu_state.abrt != 0) return 1;
        _x87_noyau.arith_m(_x87_noyau.MUL, false, _x87_noyau.de_f64(t, out var den), den);
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.fmul_64);
        return 0;
    }

    // FSUB m64 (_x87_noyau.arith_m ; PB-48, PB-60).
    internal static int opFSUBd_a16(uint32_t fetchdat)
    {
        uint64_t t;
        if (_808x.FP_ENTER()) return 1;
        if (_808x.fetch_ea_16(fetchdat)) return 1;
        if (_808x.SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        _x87_noyau.pointeurs_8087(0xDC, fetchdat, true);
        t = _808x.geteaq();
        if (cpu_state.abrt != 0) return 1;
        _x87_noyau.arith_m(_x87_noyau.SUB, false, _x87_noyau.de_f64(t, out var den), den);
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.fadd_64);
        return 0;
    }

    // FSUBR m64 (_x87_noyau.arith_m ; PB-48, PB-60).
    internal static int opFSUBRd_a16(uint32_t fetchdat)
    {
        uint64_t t;
        if (_808x.FP_ENTER()) return 1;
        if (_808x.fetch_ea_16(fetchdat)) return 1;
        if (_808x.SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        _x87_noyau.pointeurs_8087(0xDC, fetchdat, true);
        t = _808x.geteaq();
        if (cpu_state.abrt != 0) return 1;
        _x87_noyau.arith_m(_x87_noyau.SUB, true, _x87_noyau.de_f64(t, out var den), den);
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.fadd_64);
        return 0;
    }

    // FDIV m64 (_x87_noyau.arith_m ; PB-48, PB-60).
    internal static int opFDIVd_a16(uint32_t fetchdat)
    {
        uint64_t t;
        if (_808x.FP_ENTER()) return 1;
        if (_808x.fetch_ea_16(fetchdat)) return 1;
        if (_808x.SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        _x87_noyau.pointeurs_8087(0xDC, fetchdat, true);
        t = _808x.geteaq();
        if (cpu_state.abrt != 0) return 1;
        _x87_noyau.arith_m(_x87_noyau.DIV, false, _x87_noyau.de_f64(t, out var den), den);
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.fdiv_64);
        return 0;
    }

    // FDIVR m64 (_x87_noyau.arith_m ; PB-48, PB-60).
    internal static int opFDIVRd_a16(uint32_t fetchdat)
    {
        uint64_t t;
        if (_808x.FP_ENTER()) return 1;
        if (_808x.fetch_ea_16(fetchdat)) return 1;
        if (_808x.SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        _x87_noyau.pointeurs_8087(0xDC, fetchdat, true);
        t = _808x.geteaq();
        if (cpu_state.abrt != 0) return 1;
        _x87_noyau.arith_m(_x87_noyau.DIV, true, _x87_noyau.de_f64(t, out var den), den);
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.fdiv_64);
        return 0;
    }

    // FCOM m64 (_x87_noyau.comparer).
    internal static int opFCOMd_a16(uint32_t fetchdat)
    {
        uint64_t t;
        if (_808x.FP_ENTER()) return 1;
        if (_808x.fetch_ea_16(fetchdat)) return 1;
        if (_808x.SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        _x87_noyau.pointeurs_8087(0xDC, fetchdat, true);
        t = _808x.geteaq();
        if (cpu_state.abrt != 0) return 1;
        _x87_noyau.fcom_m(_x87_noyau.de_f64(t, out var den), den);
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.fcom_64);
        return 0;
    }

    // FCOMP m64 (_x87_noyau.comparer).
    internal static int opFCOMPd_a16(uint32_t fetchdat)
    {
        uint64_t t;
        if (_808x.FP_ENTER()) return 1;
        if (_808x.fetch_ea_16(fetchdat)) return 1;
        if (_808x.SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        _x87_noyau.pointeurs_8087(0xDC, fetchdat, true);
        t = _808x.geteaq();
        if (cpu_state.abrt != 0) return 1;
        if (!_x87_noyau.fcom_m(_x87_noyau.de_f64(t, out var den), den))
                _x87_noyau.depiler();
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.fcom_64);
        return 0;
    }

    // FIADD m32 entier (_x87_noyau.arith_m ; PB-48, PB-60).
    internal static int opFADDil_a16(uint32_t fetchdat)
    {
        uint32_t t;
        if (_808x.FP_ENTER()) return 1;
        if (_808x.fetch_ea_16(fetchdat)) return 1;
        if (_808x.SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        _x87_noyau.pointeurs_8087(0xDA, fetchdat, true);
        t = _808x.geteal();
        if (cpu_state.abrt != 0) return 1;
        _x87_noyau.arith_m(_x87_noyau.ADD, false, _x87_noyau.de_i((int32_t)t), false);
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.fadd_i32);
        return 0;
    }

    // FIMUL m32 entier (_x87_noyau.arith_m ; PB-48, PB-60).
    internal static int opFMULil_a16(uint32_t fetchdat)
    {
        uint32_t t;
        if (_808x.FP_ENTER()) return 1;
        if (_808x.fetch_ea_16(fetchdat)) return 1;
        if (_808x.SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        _x87_noyau.pointeurs_8087(0xDA, fetchdat, true);
        t = _808x.geteal();
        if (cpu_state.abrt != 0) return 1;
        _x87_noyau.arith_m(_x87_noyau.MUL, false, _x87_noyau.de_i((int32_t)t), false);
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.fmul_i32);
        return 0;
    }

    // FISUB m32 entier (_x87_noyau.arith_m ; PB-48, PB-60).
    internal static int opFSUBil_a16(uint32_t fetchdat)
    {
        uint32_t t;
        if (_808x.FP_ENTER()) return 1;
        if (_808x.fetch_ea_16(fetchdat)) return 1;
        if (_808x.SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        _x87_noyau.pointeurs_8087(0xDA, fetchdat, true);
        t = _808x.geteal();
        if (cpu_state.abrt != 0) return 1;
        _x87_noyau.arith_m(_x87_noyau.SUB, false, _x87_noyau.de_i((int32_t)t), false);
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.fadd_i32);
        return 0;
    }

    // FISUBR m32 entier (_x87_noyau.arith_m ; PB-48, PB-60).
    internal static int opFSUBRil_a16(uint32_t fetchdat)
    {
        uint32_t t;
        if (_808x.FP_ENTER()) return 1;
        if (_808x.fetch_ea_16(fetchdat)) return 1;
        if (_808x.SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        _x87_noyau.pointeurs_8087(0xDA, fetchdat, true);
        t = _808x.geteal();
        if (cpu_state.abrt != 0) return 1;
        _x87_noyau.arith_m(_x87_noyau.SUB, true, _x87_noyau.de_i((int32_t)t), false);
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.fadd_i32);
        return 0;
    }

    // FIDIV m32 entier (_x87_noyau.arith_m ; PB-48, PB-60).
    internal static int opFDIVil_a16(uint32_t fetchdat)
    {
        uint32_t t;
        if (_808x.FP_ENTER()) return 1;
        if (_808x.fetch_ea_16(fetchdat)) return 1;
        if (_808x.SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        _x87_noyau.pointeurs_8087(0xDA, fetchdat, true);
        t = _808x.geteal();
        if (cpu_state.abrt != 0) return 1;
        _x87_noyau.arith_m(_x87_noyau.DIV, false, _x87_noyau.de_i((int32_t)t), false);
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.fdiv_i32);
        return 0;
    }

    // FIDIVR m32 entier (_x87_noyau.arith_m ; PB-48, PB-60).
    internal static int opFDIVRil_a16(uint32_t fetchdat)
    {
        uint32_t t;
        if (_808x.FP_ENTER()) return 1;
        if (_808x.fetch_ea_16(fetchdat)) return 1;
        if (_808x.SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        _x87_noyau.pointeurs_8087(0xDA, fetchdat, true);
        t = _808x.geteal();
        if (cpu_state.abrt != 0) return 1;
        _x87_noyau.arith_m(_x87_noyau.DIV, true, _x87_noyau.de_i((int32_t)t), false);
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.fdiv_i32);
        return 0;
    }

    // FICOM m32 entier (_x87_noyau.comparer).
    internal static int opFCOMil_a16(uint32_t fetchdat)
    {
        uint32_t t;
        if (_808x.FP_ENTER()) return 1;
        if (_808x.fetch_ea_16(fetchdat)) return 1;
        if (_808x.SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        _x87_noyau.pointeurs_8087(0xDA, fetchdat, true);
        t = _808x.geteal();
        if (cpu_state.abrt != 0) return 1;
        _x87_noyau.fcom_m(_x87_noyau.de_i((int32_t)t), false);
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.fcom_i32);
        return 0;
    }

    // FICOMP m32 entier (_x87_noyau.comparer).
    internal static int opFCOMPil_a16(uint32_t fetchdat)
    {
        uint32_t t;
        if (_808x.FP_ENTER()) return 1;
        if (_808x.fetch_ea_16(fetchdat)) return 1;
        if (_808x.SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        _x87_noyau.pointeurs_8087(0xDA, fetchdat, true);
        t = _808x.geteal();
        if (cpu_state.abrt != 0) return 1;
        if (!_x87_noyau.fcom_m(_x87_noyau.de_i((int32_t)t), false))
                _x87_noyau.depiler();
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.fcom_i32);
        return 0;
    }

    // FIADD m16 entier (_x87_noyau.arith_m ; PB-48, PB-60).
    internal static int opFADDiw_a16(uint32_t fetchdat)
    {
        uint16_t t;
        if (_808x.FP_ENTER()) return 1;
        if (_808x.fetch_ea_16(fetchdat)) return 1;
        if (_808x.SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        _x87_noyau.pointeurs_8087(0xDE, fetchdat, true);
        t = _808x.geteaw();
        if (cpu_state.abrt != 0) return 1;
        _x87_noyau.arith_m(_x87_noyau.ADD, false, _x87_noyau.de_i((int16_t)t), false);
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.fadd_i16);
        return 0;
    }

    // FIMUL m16 entier (_x87_noyau.arith_m ; PB-48, PB-60).
    internal static int opFMULiw_a16(uint32_t fetchdat)
    {
        uint16_t t;
        if (_808x.FP_ENTER()) return 1;
        if (_808x.fetch_ea_16(fetchdat)) return 1;
        if (_808x.SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        _x87_noyau.pointeurs_8087(0xDE, fetchdat, true);
        t = _808x.geteaw();
        if (cpu_state.abrt != 0) return 1;
        _x87_noyau.arith_m(_x87_noyau.MUL, false, _x87_noyau.de_i((int16_t)t), false);
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.fmul_i16);
        return 0;
    }

    // FISUB m16 entier (_x87_noyau.arith_m ; PB-48, PB-60).
    internal static int opFSUBiw_a16(uint32_t fetchdat)
    {
        uint16_t t;
        if (_808x.FP_ENTER()) return 1;
        if (_808x.fetch_ea_16(fetchdat)) return 1;
        if (_808x.SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        _x87_noyau.pointeurs_8087(0xDE, fetchdat, true);
        t = _808x.geteaw();
        if (cpu_state.abrt != 0) return 1;
        _x87_noyau.arith_m(_x87_noyau.SUB, false, _x87_noyau.de_i((int16_t)t), false);
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.fadd_i16);
        return 0;
    }

    // FISUBR m16 entier (_x87_noyau.arith_m ; PB-48, PB-60).
    internal static int opFSUBRiw_a16(uint32_t fetchdat)
    {
        uint16_t t;
        if (_808x.FP_ENTER()) return 1;
        if (_808x.fetch_ea_16(fetchdat)) return 1;
        if (_808x.SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        _x87_noyau.pointeurs_8087(0xDE, fetchdat, true);
        t = _808x.geteaw();
        if (cpu_state.abrt != 0) return 1;
        _x87_noyau.arith_m(_x87_noyau.SUB, true, _x87_noyau.de_i((int16_t)t), false);
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.fadd_i16);
        return 0;
    }

    // FIDIV m16 entier (_x87_noyau.arith_m ; PB-48, PB-60).
    internal static int opFDIViw_a16(uint32_t fetchdat)
    {
        uint16_t t;
        if (_808x.FP_ENTER()) return 1;
        if (_808x.fetch_ea_16(fetchdat)) return 1;
        if (_808x.SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        _x87_noyau.pointeurs_8087(0xDE, fetchdat, true);
        t = _808x.geteaw();
        if (cpu_state.abrt != 0) return 1;
        _x87_noyau.arith_m(_x87_noyau.DIV, false, _x87_noyau.de_i((int16_t)t), false);
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.fdiv_i16);
        return 0;
    }

    // FIDIVR m16 entier (_x87_noyau.arith_m ; PB-48, PB-60).
    internal static int opFDIVRiw_a16(uint32_t fetchdat)
    {
        uint16_t t;
        if (_808x.FP_ENTER()) return 1;
        if (_808x.fetch_ea_16(fetchdat)) return 1;
        if (_808x.SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        _x87_noyau.pointeurs_8087(0xDE, fetchdat, true);
        t = _808x.geteaw();
        if (cpu_state.abrt != 0) return 1;
        _x87_noyau.arith_m(_x87_noyau.DIV, true, _x87_noyau.de_i((int16_t)t), false);
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.fdiv_i16);
        return 0;
    }

    // FICOM m16 entier (_x87_noyau.comparer).
    internal static int opFCOMiw_a16(uint32_t fetchdat)
    {
        uint16_t t;
        if (_808x.FP_ENTER()) return 1;
        if (_808x.fetch_ea_16(fetchdat)) return 1;
        if (_808x.SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        _x87_noyau.pointeurs_8087(0xDE, fetchdat, true);
        t = _808x.geteaw();
        if (cpu_state.abrt != 0) return 1;
        _x87_noyau.fcom_m(_x87_noyau.de_i((int16_t)t), false);
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.fcom_i16);
        return 0;
    }

    // FICOMP m16 entier (_x87_noyau.comparer).
    internal static int opFCOMPiw_a16(uint32_t fetchdat)
    {
        uint16_t t;
        if (_808x.FP_ENTER()) return 1;
        if (_808x.fetch_ea_16(fetchdat)) return 1;
        if (_808x.SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        _x87_noyau.pointeurs_8087(0xDE, fetchdat, true);
        t = _808x.geteaw();
        if (cpu_state.abrt != 0) return 1;
        if (!_x87_noyau.fcom_m(_x87_noyau.de_i((int16_t)t), false))
                _x87_noyau.depiler();
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.fcom_i16);
        return 0;
    }

    // FADD ST(0), ST(i) (D8 C0+i).
    internal static int opFADD_st0(uint32_t fetchdat)
    {
        if (_808x.FP_ENTER()) return 1;
        _x87_noyau.pointeurs_8087(0xD8, fetchdat, false);
        cpu_state.pc++;
        _x87_noyau.arith_st0(_x87_noyau.ADD, false, (int)(fetchdat & 7));
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.fadd);
        return 0;
    }

    // FMUL ST(0), ST(i) (D8 C8+i).
    internal static int opFMUL_st0(uint32_t fetchdat)
    {
        if (_808x.FP_ENTER()) return 1;
        _x87_noyau.pointeurs_8087(0xD8, fetchdat, false);
        cpu_state.pc++;
        _x87_noyau.arith_st0(_x87_noyau.MUL, false, (int)(fetchdat & 7));
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.fmul);
        return 0;
    }

    // FSUB ST(0), ST(i) (D8 E0+i).
    internal static int opFSUB_st0(uint32_t fetchdat)
    {
        if (_808x.FP_ENTER()) return 1;
        _x87_noyau.pointeurs_8087(0xD8, fetchdat, false);
        cpu_state.pc++;
        _x87_noyau.arith_st0(_x87_noyau.SUB, false, (int)(fetchdat & 7));
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.fadd);
        return 0;
    }

    // FSUBR ST(0), ST(i) (D8 E8+i).
    internal static int opFSUBR_st0(uint32_t fetchdat)
    {
        if (_808x.FP_ENTER()) return 1;
        _x87_noyau.pointeurs_8087(0xD8, fetchdat, false);
        cpu_state.pc++;
        _x87_noyau.arith_st0(_x87_noyau.SUB, true, (int)(fetchdat & 7));
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.fadd);
        return 0;
    }

    // FDIV ST(0), ST(i) (D8 F0+i).
    internal static int opFDIV_st0(uint32_t fetchdat)
    {
        if (_808x.FP_ENTER()) return 1;
        _x87_noyau.pointeurs_8087(0xD8, fetchdat, false);
        cpu_state.pc++;
        _x87_noyau.arith_st0(_x87_noyau.DIV, false, (int)(fetchdat & 7));
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.fdiv);
        return 0;
    }

    // FDIVR ST(0), ST(i) (D8 F8+i).
    internal static int opFDIVR_st0(uint32_t fetchdat)
    {
        if (_808x.FP_ENTER()) return 1;
        _x87_noyau.pointeurs_8087(0xD8, fetchdat, false);
        cpu_state.pc++;
        _x87_noyau.arith_st0(_x87_noyau.DIV, true, (int)(fetchdat & 7));
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.fdiv);
        return 0;
    }

    // FADD ST(i), ST(0) (DC C0+i).
    internal static int opFADD_sti(uint32_t fetchdat)
    {
        if (_808x.FP_ENTER()) return 1;
        _x87_noyau.pointeurs_8087(0xDC, fetchdat, false);
        cpu_state.pc++;
        _x87_noyau.arith_sti(_x87_noyau.ADD, false, (int)(fetchdat & 7), false);
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.fadd);
        return 0;
    }

    // FADDP ST(i), ST(0) (DE C0+i).
    internal static int opFADDP(uint32_t fetchdat)
    {
        if (_808x.FP_ENTER()) return 1;
        _x87_noyau.pointeurs_8087(0xDE, fetchdat, false);
        cpu_state.pc++;
        _x87_noyau.arith_sti(_x87_noyau.ADD, false, (int)(fetchdat & 7), true);
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.fadd);
        return 0;
    }

    // FMUL ST(i), ST(0) (DC C8+i).
    internal static int opFMUL_sti(uint32_t fetchdat)
    {
        if (_808x.FP_ENTER()) return 1;
        _x87_noyau.pointeurs_8087(0xDC, fetchdat, false);
        cpu_state.pc++;
        _x87_noyau.arith_sti(_x87_noyau.MUL, false, (int)(fetchdat & 7), false);
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.fmul);
        return 0;
    }

    // FMULP ST(i), ST(0) (DE C8+i).
    internal static int opFMULP(uint32_t fetchdat)
    {
        if (_808x.FP_ENTER()) return 1;
        _x87_noyau.pointeurs_8087(0xDE, fetchdat, false);
        cpu_state.pc++;
        _x87_noyau.arith_sti(_x87_noyau.MUL, false, (int)(fetchdat & 7), true);
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.fmul);
        return 0;
    }

    // FSUBR ST(i), ST(0) (DC E0+i).
    internal static int opFSUBR_sti(uint32_t fetchdat)
    {
        if (_808x.FP_ENTER()) return 1;
        _x87_noyau.pointeurs_8087(0xDC, fetchdat, false);
        cpu_state.pc++;
        _x87_noyau.arith_sti(_x87_noyau.SUB, true, (int)(fetchdat & 7), false);
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.fadd);
        return 0;
    }

    // FSUBRP ST(i), ST(0) (DE E0+i).
    internal static int opFSUBRP(uint32_t fetchdat)
    {
        if (_808x.FP_ENTER()) return 1;
        _x87_noyau.pointeurs_8087(0xDE, fetchdat, false);
        cpu_state.pc++;
        _x87_noyau.arith_sti(_x87_noyau.SUB, true, (int)(fetchdat & 7), true);
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.fadd);
        return 0;
    }

    // FSUB ST(i), ST(0) (DC E8+i).
    internal static int opFSUB_sti(uint32_t fetchdat)
    {
        if (_808x.FP_ENTER()) return 1;
        _x87_noyau.pointeurs_8087(0xDC, fetchdat, false);
        cpu_state.pc++;
        _x87_noyau.arith_sti(_x87_noyau.SUB, false, (int)(fetchdat & 7), false);
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.fadd);
        return 0;
    }

    // FSUBP ST(i), ST(0) (DE E8+i).
    internal static int opFSUBP(uint32_t fetchdat)
    {
        if (_808x.FP_ENTER()) return 1;
        _x87_noyau.pointeurs_8087(0xDE, fetchdat, false);
        cpu_state.pc++;
        _x87_noyau.arith_sti(_x87_noyau.SUB, false, (int)(fetchdat & 7), true);
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.fadd);
        return 0;
    }

    // FDIVR ST(i), ST(0) (DC F0+i).
    internal static int opFDIVR_sti(uint32_t fetchdat)
    {
        if (_808x.FP_ENTER()) return 1;
        _x87_noyau.pointeurs_8087(0xDC, fetchdat, false);
        cpu_state.pc++;
        _x87_noyau.arith_sti(_x87_noyau.DIV, true, (int)(fetchdat & 7), false);
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.fdiv);
        return 0;
    }

    // FDIVRP ST(i), ST(0) (DE F0+i).
    internal static int opFDIVRP(uint32_t fetchdat)
    {
        if (_808x.FP_ENTER()) return 1;
        _x87_noyau.pointeurs_8087(0xDE, fetchdat, false);
        cpu_state.pc++;
        _x87_noyau.arith_sti(_x87_noyau.DIV, true, (int)(fetchdat & 7), true);
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.fdiv);
        return 0;
    }

    // FDIV ST(i), ST(0) (DC F8+i).
    internal static int opFDIV_sti(uint32_t fetchdat)
    {
        if (_808x.FP_ENTER()) return 1;
        _x87_noyau.pointeurs_8087(0xDC, fetchdat, false);
        cpu_state.pc++;
        _x87_noyau.arith_sti(_x87_noyau.DIV, false, (int)(fetchdat & 7), false);
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.fdiv);
        return 0;
    }

    // FDIVP ST(i), ST(0) (DE F8+i).
    internal static int opFDIVP(uint32_t fetchdat)
    {
        if (_808x.FP_ENTER()) return 1;
        _x87_noyau.pointeurs_8087(0xDE, fetchdat, false);
        cpu_state.pc++;
        _x87_noyau.arith_sti(_x87_noyau.DIV, false, (int)(fetchdat & 7), true);
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.fdiv);
        return 0;
    }

    // FSQRT (_x87_noyau.fsqrt).
    internal static int opFSQRT(uint32_t fetchdat)
    {
        if (_808x.FP_ENTER()) return 1;
        _x87_noyau.pointeurs_8087(0xD9, fetchdat, false);
        cpu_state.pc++;
        _x87_noyau.fsqrt();
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.fsqrt);
        return 0;
    }

    // FTST (_x87_noyau.ftst ; PB-64, en projectif PB-70).
    internal static int opFTST(uint32_t fetchdat)
    {
        if (_808x.FP_ENTER()) return 1;
        _x87_noyau.pointeurs_8087(0xD9, fetchdat, false);
        cpu_state.pc++;
        _x87_noyau.ftst();
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.ftst);
        return 0;
    }

    // FCOM ST(i) (D8 D0+i ; PB-57, au temps de fcom).
    internal static int opFCOM_st(uint32_t fetchdat)
    {
        if (_808x.FP_ENTER()) return 1;
        _x87_noyau.pointeurs_8087(0xD8, fetchdat, false);
        cpu_state.pc++;
        _x87_noyau.fcom_st((int)(fetchdat & 7), true);
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.fcom);
        return 0;
    }

    // FCOMP ST(i) (D8 D8+i ; PB-57).
    internal static int opFCOMP_st(uint32_t fetchdat)
    {
        if (_808x.FP_ENTER()) return 1;
        _x87_noyau.pointeurs_8087(0xD8, fetchdat, false);
        cpu_state.pc++;
        if (!_x87_noyau.fcom_st((int)(fetchdat & 7), true))
        {
                _x87_noyau.depiler();
        }
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.fcom);
        return 0;
    }

    // FCOM ST(i), DC D0+i : un alias, comme PCem.
    internal static int opFCOM_dc(uint32_t fetchdat)
    {
        if (_808x.FP_ENTER()) return 1;
        _x87_noyau.pointeurs_8087(0xDC, fetchdat, false);
        cpu_state.pc++;
        _x87_noyau.fcom_st((int)(fetchdat & 7), true);
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.fcom);
        return 0;
    }

    // FCOMP ST(i), DC D8+i : un alias, comme PCem.
    internal static int opFCOMP_dc(uint32_t fetchdat)
    {
        if (_808x.FP_ENTER()) return 1;
        _x87_noyau.pointeurs_8087(0xDC, fetchdat, false);
        cpu_state.pc++;
        if (!_x87_noyau.fcom_st((int)(fetchdat & 7), true))
        {
                _x87_noyau.depiler();
        }
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.fcom);
        return 0;
    }

    // FCOMPP (DE D9 ; PB-57, PB-58 : −0 et +0 égaux).
    internal static int opFCOMPP(uint32_t fetchdat)
    {
        if (_808x.FP_ENTER()) return 1;
        _x87_noyau.pointeurs_8087(0xDE, fetchdat, false);
        cpu_state.pc++;
        if (!_x87_noyau.fcom_st(1, true))
        {
                _x87_noyau.depiler();
                _x87_noyau.depiler();
        }
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.fcom);
        return 0;
    }

    // FUCOM ST(i) : un QNaN ne lève pas IE.
    internal static int opFUCOM(uint32_t fetchdat)
    {
        if (_808x.FP_ENTER()) return 1;
        _x87_noyau.pointeurs_8087(0xDD, fetchdat, false);
        cpu_state.pc++;
        _x87_noyau.fcom_st((int)(fetchdat & 7), false);
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.fucom);
        return 0;
    }

    // FUCOMP ST(i).
    internal static int opFUCOMP(uint32_t fetchdat)
    {
        if (_808x.FP_ENTER()) return 1;
        _x87_noyau.pointeurs_8087(0xDD, fetchdat, false);
        cpu_state.pc++;
        if (!_x87_noyau.fcom_st((int)(fetchdat & 7), false))
        {
                _x87_noyau.depiler();
        }
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.fucom);
        return 0;
    }

    // FUCOMPP (DA E9).
    internal static int opFUCOMPP(uint32_t fetchdat)
    {
        if (_808x.FP_ENTER()) return 1;
        _x87_noyau.pointeurs_8087(0xDA, fetchdat, false);
        cpu_state.pc++;
        if (!_x87_noyau.fcom_st(1, false))
        {
                _x87_noyau.depiler();
                _x87_noyau.depiler();
        }
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.fucom);
        return 0;
    }

    // FRNDINT (_x87_noyau.frndint ; PB-197, PB-211).
    internal static int opFRNDINT(uint32_t fetchdat)
    {
        if (_808x.FP_ENTER()) return 1;
        _x87_noyau.pointeurs_8087(0xD9, fetchdat, false);
        cpu_state.pc++;
        _x87_noyau.frndint();
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.frndint);
        return 0;
    }

    // FSCALE (_x87_noyau.fscale ; PB-198).
    internal static int opFSCALE(uint32_t fetchdat)
    {
        if (_808x.FP_ENTER()) return 1;
        _x87_noyau.pointeurs_8087(0xD9, fetchdat, false);
        cpu_state.pc++;
        _x87_noyau.fscale();
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.fscale);
        return 0;
    }

    // FXTRACT (_x87_noyau.fxtract ; PB-194).
    internal static int opFXTRACT(uint32_t fetchdat)
    {
        if (_808x.FP_ENTER()) return 1;
        _x87_noyau.pointeurs_8087(0xD9, fetchdat, false);
        cpu_state.pc++;
        _x87_noyau.fxtract();
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.fxtract);
        return 0;
    }

    // FPREM (_x87_noyau.fprem ; PB-65).
    internal static int opFPREM(uint32_t fetchdat)
    {
        if (_808x.FP_ENTER()) return 1;
        _x87_noyau.pointeurs_8087(0xD9, fetchdat, false);
        cpu_state.pc++;
        _x87_noyau.fprem(false);
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.fprem);
        return 0;
    }

    // FPREM1 (_x87_noyau.fprem ; PB-65).
    internal static int opFPREM1(uint32_t fetchdat)
    {
        if (_808x.FP_ENTER()) return 1;
        _x87_noyau.pointeurs_8087(0xD9, fetchdat, false);
        cpu_state.pc++;
        _x87_noyau.fprem(true);
        _808x.CLOCK_CYCLES(x87_timings_c.x87_timings.fprem1);
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
        _x87_noyau.memoire(_808x.ops_808x_fpu_d9_a16, 0, opFLDs_a16);
        _x87_noyau.memoire(_808x.ops_808x_fpu_dd_a16, 0, opFLDd_a16);
        _x87_noyau.memoire(_808x.ops_808x_fpu_db_a16, 5, opFLDe_a16);
        _x87_noyau.memoire(_808x.ops_808x_fpu_df_a16, 0, opFILDiw_a16);
        _x87_noyau.memoire(_808x.ops_808x_fpu_db_a16, 0, opFILDil_a16);
        _x87_noyau.memoire(_808x.ops_808x_fpu_df_a16, 5, opFILDiq_a16);
        _x87_noyau.memoire(_808x.ops_808x_fpu_df_a16, 4, opFBLD_a16);
        _x87_noyau.memoire(_808x.ops_808x_fpu_d9_a16, 2, opFSTs_a16);
        _x87_noyau.memoire(_808x.ops_808x_fpu_d9_a16, 3, opFSTPs_a16);
        _x87_noyau.memoire(_808x.ops_808x_fpu_dd_a16, 2, opFSTd_a16);
        _x87_noyau.memoire(_808x.ops_808x_fpu_dd_a16, 3, opFSTPd_a16);
        _x87_noyau.memoire(_808x.ops_808x_fpu_db_a16, 7, opFSTPe_a16);
        _x87_noyau.memoire(_808x.ops_808x_fpu_df_a16, 2, opFISTiw_a16);
        _x87_noyau.memoire(_808x.ops_808x_fpu_df_a16, 3, opFISTPiw_a16);
        _x87_noyau.memoire(_808x.ops_808x_fpu_db_a16, 2, opFISTil_a16);
        _x87_noyau.memoire(_808x.ops_808x_fpu_db_a16, 3, opFISTPil_a16);
        _x87_noyau.memoire(_808x.ops_808x_fpu_df_a16, 7, opFISTPiq_a16);
        _x87_noyau.memoire(_808x.ops_808x_fpu_df_a16, 6, opFBSTP_a16);
        _808x.ops_808x_fpu_d9_a16[0xC0] = opFLD;
        _808x.ops_808x_fpu_d9_a16[0xC1] = opFLD;
        _808x.ops_808x_fpu_d9_a16[0xC2] = opFLD;
        _808x.ops_808x_fpu_d9_a16[0xC3] = opFLD;
        _808x.ops_808x_fpu_d9_a16[0xC4] = opFLD;
        _808x.ops_808x_fpu_d9_a16[0xC5] = opFLD;
        _808x.ops_808x_fpu_d9_a16[0xC6] = opFLD;
        _808x.ops_808x_fpu_d9_a16[0xC7] = opFLD;
        _808x.ops_808x_fpu_d9_a16[0xC8] = opFXCH;
        _808x.ops_808x_fpu_d9_a16[0xC9] = opFXCH;
        _808x.ops_808x_fpu_d9_a16[0xCA] = opFXCH;
        _808x.ops_808x_fpu_d9_a16[0xCB] = opFXCH;
        _808x.ops_808x_fpu_d9_a16[0xCC] = opFXCH;
        _808x.ops_808x_fpu_d9_a16[0xCD] = opFXCH;
        _808x.ops_808x_fpu_d9_a16[0xCE] = opFXCH;
        _808x.ops_808x_fpu_d9_a16[0xCF] = opFXCH;
        _808x.ops_808x_fpu_dd_a16[0xD0] = opFST;
        _808x.ops_808x_fpu_dd_a16[0xD1] = opFST;
        _808x.ops_808x_fpu_dd_a16[0xD2] = opFST;
        _808x.ops_808x_fpu_dd_a16[0xD3] = opFST;
        _808x.ops_808x_fpu_dd_a16[0xD4] = opFST;
        _808x.ops_808x_fpu_dd_a16[0xD5] = opFST;
        _808x.ops_808x_fpu_dd_a16[0xD6] = opFST;
        _808x.ops_808x_fpu_dd_a16[0xD7] = opFST;
        _808x.ops_808x_fpu_dd_a16[0xD8] = opFSTP;
        _808x.ops_808x_fpu_dd_a16[0xD9] = opFSTP;
        _808x.ops_808x_fpu_dd_a16[0xDA] = opFSTP;
        _808x.ops_808x_fpu_dd_a16[0xDB] = opFSTP;
        _808x.ops_808x_fpu_dd_a16[0xDC] = opFSTP;
        _808x.ops_808x_fpu_dd_a16[0xDD] = opFSTP;
        _808x.ops_808x_fpu_dd_a16[0xDE] = opFSTP;
        _808x.ops_808x_fpu_dd_a16[0xDF] = opFSTP;
        _808x.ops_808x_fpu_d9_a16[0xD8] = opFSTP_d9;
        _808x.ops_808x_fpu_d9_a16[0xD9] = opFSTP_d9;
        _808x.ops_808x_fpu_d9_a16[0xDA] = opFSTP_d9;
        _808x.ops_808x_fpu_d9_a16[0xDB] = opFSTP_d9;
        _808x.ops_808x_fpu_d9_a16[0xDC] = opFSTP_d9;
        _808x.ops_808x_fpu_d9_a16[0xDD] = opFSTP_d9;
        _808x.ops_808x_fpu_d9_a16[0xDE] = opFSTP_d9;
        _808x.ops_808x_fpu_d9_a16[0xDF] = opFSTP_d9;
        _808x.ops_808x_fpu_d9_a16[0xE0] = opFCHS;
        _808x.ops_808x_fpu_d9_a16[0xE1] = opFABS;
        _808x.ops_808x_fpu_d9_a16[0xE5] = opFXAM;
        _808x.ops_808x_fpu_d9_a16[0xE8] = opFLDz1;
        _808x.ops_808x_fpu_d9_a16[0xEE] = opFLDz1;
        _808x.ops_808x_fpu_d9_a16[0xE9] = opFLDconst;
        _808x.ops_808x_fpu_d9_a16[0xEA] = opFLDconst;
        _808x.ops_808x_fpu_d9_a16[0xEB] = opFLDconst;
        _808x.ops_808x_fpu_d9_a16[0xEC] = opFLDconst;
        _808x.ops_808x_fpu_d9_a16[0xED] = opFLDconst;
        _x87_noyau.memoire(_808x.ops_808x_fpu_d9_a16, 4, opFLDENV_a16);
        _x87_noyau.memoire(_808x.ops_808x_fpu_d9_a16, 6, opFNSTENV_a16);
        _x87_noyau.memoire(_808x.ops_808x_fpu_dd_a16, 4, opFRSTOR_a16);
        _x87_noyau.memoire(_808x.ops_808x_fpu_dd_a16, 6, opFNSAVE_a16);
        _x87_noyau.memoire(_808x.ops_808x_fpu_d8_a16, 0, opFADDs_a16);
        _x87_noyau.memoire(_808x.ops_808x_fpu_d8_a16, 1, opFMULs_a16);
        _x87_noyau.memoire(_808x.ops_808x_fpu_d8_a16, 4, opFSUBs_a16);
        _x87_noyau.memoire(_808x.ops_808x_fpu_d8_a16, 5, opFSUBRs_a16);
        _x87_noyau.memoire(_808x.ops_808x_fpu_d8_a16, 6, opFDIVs_a16);
        _x87_noyau.memoire(_808x.ops_808x_fpu_d8_a16, 7, opFDIVRs_a16);
        _x87_noyau.memoire(_808x.ops_808x_fpu_d8_a16, 2, opFCOMs_a16);
        _x87_noyau.memoire(_808x.ops_808x_fpu_d8_a16, 3, opFCOMPs_a16);
        _x87_noyau.memoire(_808x.ops_808x_fpu_dc_a16, 0, opFADDd_a16);
        _x87_noyau.memoire(_808x.ops_808x_fpu_dc_a16, 1, opFMULd_a16);
        _x87_noyau.memoire(_808x.ops_808x_fpu_dc_a16, 4, opFSUBd_a16);
        _x87_noyau.memoire(_808x.ops_808x_fpu_dc_a16, 5, opFSUBRd_a16);
        _x87_noyau.memoire(_808x.ops_808x_fpu_dc_a16, 6, opFDIVd_a16);
        _x87_noyau.memoire(_808x.ops_808x_fpu_dc_a16, 7, opFDIVRd_a16);
        _x87_noyau.memoire(_808x.ops_808x_fpu_dc_a16, 2, opFCOMd_a16);
        _x87_noyau.memoire(_808x.ops_808x_fpu_dc_a16, 3, opFCOMPd_a16);
        _x87_noyau.memoire(_808x.ops_808x_fpu_da_a16, 0, opFADDil_a16);
        _x87_noyau.memoire(_808x.ops_808x_fpu_da_a16, 1, opFMULil_a16);
        _x87_noyau.memoire(_808x.ops_808x_fpu_da_a16, 4, opFSUBil_a16);
        _x87_noyau.memoire(_808x.ops_808x_fpu_da_a16, 5, opFSUBRil_a16);
        _x87_noyau.memoire(_808x.ops_808x_fpu_da_a16, 6, opFDIVil_a16);
        _x87_noyau.memoire(_808x.ops_808x_fpu_da_a16, 7, opFDIVRil_a16);
        _x87_noyau.memoire(_808x.ops_808x_fpu_da_a16, 2, opFCOMil_a16);
        _x87_noyau.memoire(_808x.ops_808x_fpu_da_a16, 3, opFCOMPil_a16);
        _x87_noyau.memoire(_808x.ops_808x_fpu_de_a16, 0, opFADDiw_a16);
        _x87_noyau.memoire(_808x.ops_808x_fpu_de_a16, 1, opFMULiw_a16);
        _x87_noyau.memoire(_808x.ops_808x_fpu_de_a16, 4, opFSUBiw_a16);
        _x87_noyau.memoire(_808x.ops_808x_fpu_de_a16, 5, opFSUBRiw_a16);
        _x87_noyau.memoire(_808x.ops_808x_fpu_de_a16, 6, opFDIViw_a16);
        _x87_noyau.memoire(_808x.ops_808x_fpu_de_a16, 7, opFDIVRiw_a16);
        _x87_noyau.memoire(_808x.ops_808x_fpu_de_a16, 2, opFCOMiw_a16);
        _x87_noyau.memoire(_808x.ops_808x_fpu_de_a16, 3, opFCOMPiw_a16);
        _808x.ops_808x_fpu_d8_a16[0x18] = opFADD_st0;
        _808x.ops_808x_fpu_d8_a16[0x19] = opFMUL_st0;
        _808x.ops_808x_fpu_d8_a16[0x1C] = opFSUB_st0;
        _808x.ops_808x_fpu_d8_a16[0x1D] = opFSUBR_st0;
        _808x.ops_808x_fpu_d8_a16[0x1E] = opFDIV_st0;
        _808x.ops_808x_fpu_d8_a16[0x1F] = opFDIVR_st0;
        _808x.ops_808x_fpu_dc_a16[0x18] = opFADD_sti;
        _808x.ops_808x_fpu_de_a16[0xC0] = opFADDP;
        _808x.ops_808x_fpu_de_a16[0xC1] = opFADDP;
        _808x.ops_808x_fpu_de_a16[0xC2] = opFADDP;
        _808x.ops_808x_fpu_de_a16[0xC3] = opFADDP;
        _808x.ops_808x_fpu_de_a16[0xC4] = opFADDP;
        _808x.ops_808x_fpu_de_a16[0xC5] = opFADDP;
        _808x.ops_808x_fpu_de_a16[0xC6] = opFADDP;
        _808x.ops_808x_fpu_de_a16[0xC7] = opFADDP;
        _808x.ops_808x_fpu_dc_a16[0x19] = opFMUL_sti;
        _808x.ops_808x_fpu_de_a16[0xC8] = opFMULP;
        _808x.ops_808x_fpu_de_a16[0xC9] = opFMULP;
        _808x.ops_808x_fpu_de_a16[0xCA] = opFMULP;
        _808x.ops_808x_fpu_de_a16[0xCB] = opFMULP;
        _808x.ops_808x_fpu_de_a16[0xCC] = opFMULP;
        _808x.ops_808x_fpu_de_a16[0xCD] = opFMULP;
        _808x.ops_808x_fpu_de_a16[0xCE] = opFMULP;
        _808x.ops_808x_fpu_de_a16[0xCF] = opFMULP;
        _808x.ops_808x_fpu_dc_a16[0x1C] = opFSUBR_sti;
        _808x.ops_808x_fpu_de_a16[0xE0] = opFSUBRP;
        _808x.ops_808x_fpu_de_a16[0xE1] = opFSUBRP;
        _808x.ops_808x_fpu_de_a16[0xE2] = opFSUBRP;
        _808x.ops_808x_fpu_de_a16[0xE3] = opFSUBRP;
        _808x.ops_808x_fpu_de_a16[0xE4] = opFSUBRP;
        _808x.ops_808x_fpu_de_a16[0xE5] = opFSUBRP;
        _808x.ops_808x_fpu_de_a16[0xE6] = opFSUBRP;
        _808x.ops_808x_fpu_de_a16[0xE7] = opFSUBRP;
        _808x.ops_808x_fpu_dc_a16[0x1D] = opFSUB_sti;
        _808x.ops_808x_fpu_de_a16[0xE8] = opFSUBP;
        _808x.ops_808x_fpu_de_a16[0xE9] = opFSUBP;
        _808x.ops_808x_fpu_de_a16[0xEA] = opFSUBP;
        _808x.ops_808x_fpu_de_a16[0xEB] = opFSUBP;
        _808x.ops_808x_fpu_de_a16[0xEC] = opFSUBP;
        _808x.ops_808x_fpu_de_a16[0xED] = opFSUBP;
        _808x.ops_808x_fpu_de_a16[0xEE] = opFSUBP;
        _808x.ops_808x_fpu_de_a16[0xEF] = opFSUBP;
        _808x.ops_808x_fpu_dc_a16[0x1E] = opFDIVR_sti;
        _808x.ops_808x_fpu_de_a16[0xF0] = opFDIVRP;
        _808x.ops_808x_fpu_de_a16[0xF1] = opFDIVRP;
        _808x.ops_808x_fpu_de_a16[0xF2] = opFDIVRP;
        _808x.ops_808x_fpu_de_a16[0xF3] = opFDIVRP;
        _808x.ops_808x_fpu_de_a16[0xF4] = opFDIVRP;
        _808x.ops_808x_fpu_de_a16[0xF5] = opFDIVRP;
        _808x.ops_808x_fpu_de_a16[0xF6] = opFDIVRP;
        _808x.ops_808x_fpu_de_a16[0xF7] = opFDIVRP;
        _808x.ops_808x_fpu_dc_a16[0x1F] = opFDIV_sti;
        _808x.ops_808x_fpu_de_a16[0xF8] = opFDIVP;
        _808x.ops_808x_fpu_de_a16[0xF9] = opFDIVP;
        _808x.ops_808x_fpu_de_a16[0xFA] = opFDIVP;
        _808x.ops_808x_fpu_de_a16[0xFB] = opFDIVP;
        _808x.ops_808x_fpu_de_a16[0xFC] = opFDIVP;
        _808x.ops_808x_fpu_de_a16[0xFD] = opFDIVP;
        _808x.ops_808x_fpu_de_a16[0xFE] = opFDIVP;
        _808x.ops_808x_fpu_de_a16[0xFF] = opFDIVP;
        _808x.ops_808x_fpu_d9_a16[0xFA] = opFSQRT;
        _808x.ops_808x_fpu_d9_a16[0xE4] = opFTST;
        _808x.ops_808x_fpu_d8_a16[0x1A] = opFCOM_st;
        _808x.ops_808x_fpu_d8_a16[0x1B] = opFCOMP_st;
        _808x.ops_808x_fpu_dc_a16[0x1A] = opFCOM_dc;
        _808x.ops_808x_fpu_dc_a16[0x1B] = opFCOMP_dc;
        _808x.ops_808x_fpu_de_a16[0xD9] = opFCOMPP;
        _808x.ops_808x_fpu_dd_a16[0xE0] = opFUCOM;
        _808x.ops_808x_fpu_dd_a16[0xE1] = opFUCOM;
        _808x.ops_808x_fpu_dd_a16[0xE2] = opFUCOM;
        _808x.ops_808x_fpu_dd_a16[0xE3] = opFUCOM;
        _808x.ops_808x_fpu_dd_a16[0xE4] = opFUCOM;
        _808x.ops_808x_fpu_dd_a16[0xE5] = opFUCOM;
        _808x.ops_808x_fpu_dd_a16[0xE6] = opFUCOM;
        _808x.ops_808x_fpu_dd_a16[0xE7] = opFUCOM;
        _808x.ops_808x_fpu_dd_a16[0xE8] = opFUCOMP;
        _808x.ops_808x_fpu_dd_a16[0xE9] = opFUCOMP;
        _808x.ops_808x_fpu_dd_a16[0xEA] = opFUCOMP;
        _808x.ops_808x_fpu_dd_a16[0xEB] = opFUCOMP;
        _808x.ops_808x_fpu_dd_a16[0xEC] = opFUCOMP;
        _808x.ops_808x_fpu_dd_a16[0xED] = opFUCOMP;
        _808x.ops_808x_fpu_dd_a16[0xEE] = opFUCOMP;
        _808x.ops_808x_fpu_dd_a16[0xEF] = opFUCOMP;
        _808x.ops_808x_fpu_da_a16[0xE9] = opFUCOMPP;
        _808x.ops_808x_fpu_d9_a16[0xFC] = opFRNDINT;
        _808x.ops_808x_fpu_d9_a16[0xFD] = opFSCALE;
        _808x.ops_808x_fpu_d9_a16[0xF4] = opFXTRACT;
        _808x.ops_808x_fpu_d9_a16[0xF8] = opFPREM;
        _808x.ops_808x_fpu_d9_a16[0xF5] = opFPREM1;
    }
}
