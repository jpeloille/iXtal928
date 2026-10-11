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

    // FLDCW m16 : le mot de contrôle. Démasquer une exception en attente active ERROR# (387 PRM § 4.8.2) : N5. Les bits
    //   réservés (6 et 13 à 15, et 7, l'IEM du 8087, sur les autres) restent tels qu'écrits, relus par FNSTCW,
    //   comme PCem : ce que le silicium en relit n'est pas documenté (inconnu).
    internal static int opFLDCW_a16(uint32_t fetchdat)
    {
        uint16_t tempw;
        if (_386.FP_ENTER()) return 1;
        if (fetch_ea_16(fetchdat)) return 1;
        if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        if (_x87_noyau.limite(2, false)) return 1;
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
        if (_x87_noyau.limite(2, false)) return 1;
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
        if (_x87_noyau.limite(2, true)) return 1;
        seteaw(cpu_state.npxc);
        _386.CLOCK_CYCLES(x87_timings_c.x87_timings.fstcw_sw);
        return cpu_state.abrt;
    }

    internal static int opFNSTCW_a32(uint32_t fetchdat)
    {
        if (_386.FP_ENTER()) return 1;
        if (fetch_ea_32(fetchdat)) return 1;
        if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
        if (_x87_noyau.limite(2, true)) return 1;
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
        if (_x87_noyau.limite(2, true)) return 1;
        seteaw((uint16_t)((cpu_state.npxs & 0xC7FF) | ((cpu_state.TOP & 7) << 11)));
        _386.CLOCK_CYCLES(x87_timings_c.x87_timings.fstcw_sw);
        return cpu_state.abrt;
    }

    internal static int opFNSTSW_a32(uint32_t fetchdat)
    {
        if (_386.FP_ENTER()) return 1;
        if (fetch_ea_32(fetchdat)) return 1;
        if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
        if (_x87_noyau.limite(2, true)) return 1;
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
        _x87_noyau.pointeurs(0xD9, fetchdat);
        cpu_state.pc++;
        _386.CLOCK_CYCLES(x87_timings_c.x87_timings.fnop);
        return 0;
    }

    // FINCSTP : TOP monte, sans toucher aux étiquettes ni aux registres ; C1 à zéro sur le 387 et le 486 (486 PRM,
    //   table 15-1 ; le 287XL : déduit), indéfini sur le 8087 et le 287, laissé.
    internal static int opFINCSTP(uint32_t fetchdat)
    {
        if (_386.FP_ENTER()) return 1;
        _x87_noyau.pointeurs(0xD9, fetchdat);
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
        _x87_noyau.pointeurs(0xD9, fetchdat);
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
        _x87_noyau.pointeurs(0xDD, fetchdat);
        cpu_state.pc++;
        _x87_noyau.etiqueter(_x87_noyau.phys((int)(fetchdat & 7)), _x87_noyau.VIDE);
        _386.CLOCK_CYCLES(x87_timings_c.x87_timings.ffree);
        return 0;
    }

    // FLD m32 (_x87_noyau.fld_f32).
    internal static int opFLDs_a16(uint32_t fetchdat)
    {
        uint32_t t;
        if (_386.FP_ENTER()) return 1;
        if (fetch_ea_16(fetchdat)) return 1;
        if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        if (_x87_noyau.limite(4, false)) return 1;
        _x87_noyau.pointeurs_m(0xD9, fetchdat);
        t = geteal();
        if (cpu_state.abrt != 0) return 1;
        _x87_noyau.fld_f32(t);
        _386.CLOCK_CYCLES(x87_timings_c.x87_timings.fld_32);
        return 0;
    }

    internal static int opFLDs_a32(uint32_t fetchdat)
    {
        uint32_t t;
        if (_386.FP_ENTER()) return 1;
        if (fetch_ea_32(fetchdat)) return 1;
        if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        if (_x87_noyau.limite(4, false)) return 1;
        _x87_noyau.pointeurs_m(0xD9, fetchdat);
        t = geteal();
        if (cpu_state.abrt != 0) return 1;
        _x87_noyau.fld_f32(t);
        _386.CLOCK_CYCLES(x87_timings_c.x87_timings.fld_32);
        return 0;
    }

    // FLD m64 (_x87_noyau.fld_f64).
    internal static int opFLDd_a16(uint32_t fetchdat)
    {
        uint64_t t;
        if (_386.FP_ENTER()) return 1;
        if (fetch_ea_16(fetchdat)) return 1;
        if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        if (_x87_noyau.limite(8, false)) return 1;
        _x87_noyau.pointeurs_m(0xDD, fetchdat);
        t = geteaq();
        if (cpu_state.abrt != 0) return 1;
        _x87_noyau.fld_f64(t);
        _386.CLOCK_CYCLES(x87_timings_c.x87_timings.fld_64);
        return 0;
    }

    internal static int opFLDd_a32(uint32_t fetchdat)
    {
        uint64_t t;
        if (_386.FP_ENTER()) return 1;
        if (fetch_ea_32(fetchdat)) return 1;
        if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        if (_x87_noyau.limite(8, false)) return 1;
        _x87_noyau.pointeurs_m(0xDD, fetchdat);
        t = geteaq();
        if (cpu_state.abrt != 0) return 1;
        _x87_noyau.fld_f64(t);
        _386.CLOCK_CYCLES(x87_timings_c.x87_timings.fld_64);
        return 0;
    }

    // FLD m80 : les 80 bits tels quels (_x87_noyau.fld_f80).
    internal static int opFLDe_a16(uint32_t fetchdat)
    {
        uint64_t s; uint16_t e;
        if (_386.FP_ENTER()) return 1;
        if (fetch_ea_16(fetchdat)) return 1;
        if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        if (_x87_noyau.limite(10, false)) return 1;
        _x87_noyau.pointeurs_m(0xDB, fetchdat);
        s = readmemq(x86.easeg, cpu_state.eaaddr);
        e = readmemw(x86.easeg, cpu_state.eaaddr + 8);
        if (cpu_state.abrt != 0) return 1;
        _x87_noyau.fld_f80(s, e);
        _386.CLOCK_CYCLES(x87_timings_c.x87_timings.fld_80);
        return 0;
    }

    internal static int opFLDe_a32(uint32_t fetchdat)
    {
        uint64_t s; uint16_t e;
        if (_386.FP_ENTER()) return 1;
        if (fetch_ea_32(fetchdat)) return 1;
        if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        if (_x87_noyau.limite(10, false)) return 1;
        _x87_noyau.pointeurs_m(0xDB, fetchdat);
        s = readmemq(x86.easeg, cpu_state.eaaddr);
        e = readmemw(x86.easeg, cpu_state.eaaddr + 8);
        if (cpu_state.abrt != 0) return 1;
        _x87_noyau.fld_f80(s, e);
        _386.CLOCK_CYCLES(x87_timings_c.x87_timings.fld_80);
        return 0;
    }

    // FILD m16.
    internal static int opFILDiw_a16(uint32_t fetchdat)
    {
        uint16_t t;
        if (_386.FP_ENTER()) return 1;
        if (fetch_ea_16(fetchdat)) return 1;
        if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        if (_x87_noyau.limite(2, false)) return 1;
        _x87_noyau.pointeurs_m(0xDF, fetchdat);
        t = geteaw();
        if (cpu_state.abrt != 0) return 1;
        _x87_noyau.fild((int16_t)t);
        _386.CLOCK_CYCLES(x87_timings_c.x87_timings.fild_16);
        return 0;
    }

    internal static int opFILDiw_a32(uint32_t fetchdat)
    {
        uint16_t t;
        if (_386.FP_ENTER()) return 1;
        if (fetch_ea_32(fetchdat)) return 1;
        if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        if (_x87_noyau.limite(2, false)) return 1;
        _x87_noyau.pointeurs_m(0xDF, fetchdat);
        t = geteaw();
        if (cpu_state.abrt != 0) return 1;
        _x87_noyau.fild((int16_t)t);
        _386.CLOCK_CYCLES(x87_timings_c.x87_timings.fild_16);
        return 0;
    }

    // FILD m32.
    internal static int opFILDil_a16(uint32_t fetchdat)
    {
        uint32_t t;
        if (_386.FP_ENTER()) return 1;
        if (fetch_ea_16(fetchdat)) return 1;
        if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        if (_x87_noyau.limite(4, false)) return 1;
        _x87_noyau.pointeurs_m(0xDB, fetchdat);
        t = geteal();
        if (cpu_state.abrt != 0) return 1;
        _x87_noyau.fild((int32_t)t);
        _386.CLOCK_CYCLES(x87_timings_c.x87_timings.fild_32);
        return 0;
    }

    internal static int opFILDil_a32(uint32_t fetchdat)
    {
        uint32_t t;
        if (_386.FP_ENTER()) return 1;
        if (fetch_ea_32(fetchdat)) return 1;
        if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        if (_x87_noyau.limite(4, false)) return 1;
        _x87_noyau.pointeurs_m(0xDB, fetchdat);
        t = geteal();
        if (cpu_state.abrt != 0) return 1;
        _x87_noyau.fild((int32_t)t);
        _386.CLOCK_CYCLES(x87_timings_c.x87_timings.fild_32);
        return 0;
    }

    // FILD m64.
    internal static int opFILDiq_a16(uint32_t fetchdat)
    {
        uint64_t t;
        if (_386.FP_ENTER()) return 1;
        if (fetch_ea_16(fetchdat)) return 1;
        if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        if (_x87_noyau.limite(8, false)) return 1;
        _x87_noyau.pointeurs_m(0xDF, fetchdat);
        t = geteaq();
        if (cpu_state.abrt != 0) return 1;
        _x87_noyau.fild((int64_t)t);
        _386.CLOCK_CYCLES(x87_timings_c.x87_timings.fild_64);
        return 0;
    }

    internal static int opFILDiq_a32(uint32_t fetchdat)
    {
        uint64_t t;
        if (_386.FP_ENTER()) return 1;
        if (fetch_ea_32(fetchdat)) return 1;
        if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        if (_x87_noyau.limite(8, false)) return 1;
        _x87_noyau.pointeurs_m(0xDF, fetchdat);
        t = geteaq();
        if (cpu_state.abrt != 0) return 1;
        _x87_noyau.fild((int64_t)t);
        _386.CLOCK_CYCLES(x87_timings_c.x87_timings.fild_64);
        return 0;
    }

    // FBLD m80 : dix-huit chiffres décimaux (_x87_noyau.fbld ; PB-52).
    internal static int opFBLD_a16(uint32_t fetchdat)
    {
        uint64_t s; uint16_t e;
        if (_386.FP_ENTER()) return 1;
        if (fetch_ea_16(fetchdat)) return 1;
        if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        if (_x87_noyau.limite(10, false)) return 1;
        _x87_noyau.pointeurs_m(0xDF, fetchdat);
        s = readmemq(x86.easeg, cpu_state.eaaddr);
        e = readmemw(x86.easeg, cpu_state.eaaddr + 8);
        if (cpu_state.abrt != 0) return 1;
        _x87_noyau.fbld(s, e);
        _386.CLOCK_CYCLES(x87_timings_c.x87_timings.fbld);
        return 0;
    }

    internal static int opFBLD_a32(uint32_t fetchdat)
    {
        uint64_t s; uint16_t e;
        if (_386.FP_ENTER()) return 1;
        if (fetch_ea_32(fetchdat)) return 1;
        if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        if (_x87_noyau.limite(10, false)) return 1;
        _x87_noyau.pointeurs_m(0xDF, fetchdat);
        s = readmemq(x86.easeg, cpu_state.eaaddr);
        e = readmemw(x86.easeg, cpu_state.eaaddr + 8);
        if (cpu_state.abrt != 0) return 1;
        _x87_noyau.fbld(s, e);
        _386.CLOCK_CYCLES(x87_timings_c.x87_timings.fbld);
        return 0;
    }

    // FST m32 (_x87_noyau.fst_f32).
    internal static int opFSTs_a16(uint32_t fetchdat)
    {
        if (_386.FP_ENTER()) return 1;
        if (fetch_ea_16(fetchdat)) return 1;
        if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
        if (_x87_noyau.limite(4, true)) return 1;
        _x87_noyau.pointeurs_m(0xD9, fetchdat);
        if (!_x87_noyau.fst_f32(out var t))
        {
                seteal(t);
                if (cpu_state.abrt != 0) return 1;
        }
        _386.CLOCK_CYCLES(x87_timings_c.x87_timings.fst_32);
        return 0;
    }

    internal static int opFSTs_a32(uint32_t fetchdat)
    {
        if (_386.FP_ENTER()) return 1;
        if (fetch_ea_32(fetchdat)) return 1;
        if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
        if (_x87_noyau.limite(4, true)) return 1;
        _x87_noyau.pointeurs_m(0xD9, fetchdat);
        if (!_x87_noyau.fst_f32(out var t))
        {
                seteal(t);
                if (cpu_state.abrt != 0) return 1;
        }
        _386.CLOCK_CYCLES(x87_timings_c.x87_timings.fst_32);
        return 0;
    }

    // FSTP m32 (_x87_noyau.fst_f32).
    internal static int opFSTPs_a16(uint32_t fetchdat)
    {
        if (_386.FP_ENTER()) return 1;
        if (fetch_ea_16(fetchdat)) return 1;
        if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
        if (_x87_noyau.limite(4, true)) return 1;
        _x87_noyau.pointeurs_m(0xD9, fetchdat);
        if (!_x87_noyau.fst_f32(out var t))
        {
                seteal(t);
                if (cpu_state.abrt != 0) return 1;
                _x87_noyau.depiler();
        }
        _386.CLOCK_CYCLES(x87_timings_c.x87_timings.fst_32);
        return 0;
    }

    internal static int opFSTPs_a32(uint32_t fetchdat)
    {
        if (_386.FP_ENTER()) return 1;
        if (fetch_ea_32(fetchdat)) return 1;
        if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
        if (_x87_noyau.limite(4, true)) return 1;
        _x87_noyau.pointeurs_m(0xD9, fetchdat);
        if (!_x87_noyau.fst_f32(out var t))
        {
                seteal(t);
                if (cpu_state.abrt != 0) return 1;
                _x87_noyau.depiler();
        }
        _386.CLOCK_CYCLES(x87_timings_c.x87_timings.fst_32);
        return 0;
    }

    // FST m64 (_x87_noyau.fst_f64).
    internal static int opFSTd_a16(uint32_t fetchdat)
    {
        if (_386.FP_ENTER()) return 1;
        if (fetch_ea_16(fetchdat)) return 1;
        if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
        if (_x87_noyau.limite(8, true)) return 1;
        _x87_noyau.pointeurs_m(0xDD, fetchdat);
        if (!_x87_noyau.fst_f64(out var t))
        {
                seteaq(t);
                if (cpu_state.abrt != 0) return 1;
        }
        _386.CLOCK_CYCLES(x87_timings_c.x87_timings.fst_64);
        return 0;
    }

    internal static int opFSTd_a32(uint32_t fetchdat)
    {
        if (_386.FP_ENTER()) return 1;
        if (fetch_ea_32(fetchdat)) return 1;
        if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
        if (_x87_noyau.limite(8, true)) return 1;
        _x87_noyau.pointeurs_m(0xDD, fetchdat);
        if (!_x87_noyau.fst_f64(out var t))
        {
                seteaq(t);
                if (cpu_state.abrt != 0) return 1;
        }
        _386.CLOCK_CYCLES(x87_timings_c.x87_timings.fst_64);
        return 0;
    }

    // FSTP m64 (_x87_noyau.fst_f64).
    internal static int opFSTPd_a16(uint32_t fetchdat)
    {
        if (_386.FP_ENTER()) return 1;
        if (fetch_ea_16(fetchdat)) return 1;
        if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
        if (_x87_noyau.limite(8, true)) return 1;
        _x87_noyau.pointeurs_m(0xDD, fetchdat);
        if (!_x87_noyau.fst_f64(out var t))
        {
                seteaq(t);
                if (cpu_state.abrt != 0) return 1;
                _x87_noyau.depiler();
        }
        _386.CLOCK_CYCLES(x87_timings_c.x87_timings.fst_64);
        return 0;
    }

    internal static int opFSTPd_a32(uint32_t fetchdat)
    {
        if (_386.FP_ENTER()) return 1;
        if (fetch_ea_32(fetchdat)) return 1;
        if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
        if (_x87_noyau.limite(8, true)) return 1;
        _x87_noyau.pointeurs_m(0xDD, fetchdat);
        if (!_x87_noyau.fst_f64(out var t))
        {
                seteaq(t);
                if (cpu_state.abrt != 0) return 1;
                _x87_noyau.depiler();
        }
        _386.CLOCK_CYCLES(x87_timings_c.x87_timings.fst_64);
        return 0;
    }

    // FSTP m80 : les 80 bits tels quels (_x87_noyau.fst_f80).
    internal static int opFSTPe_a16(uint32_t fetchdat)
    {
        if (_386.FP_ENTER()) return 1;
        if (fetch_ea_16(fetchdat)) return 1;
        if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
        if (_x87_noyau.limite(10, true)) return 1;
        _x87_noyau.pointeurs_m(0xDB, fetchdat);
        if (!_x87_noyau.fst_f80(out var t))
        {
                writememq(x86.easeg, cpu_state.eaaddr, t.signif);
                writememw(x86.easeg, cpu_state.eaaddr + 8, t.signExp);
                if (cpu_state.abrt != 0) return 1;
                _x87_noyau.depiler();
        }
        _386.CLOCK_CYCLES(x87_timings_c.x87_timings.fst_80);
        return 0;
    }

    internal static int opFSTPe_a32(uint32_t fetchdat)
    {
        if (_386.FP_ENTER()) return 1;
        if (fetch_ea_32(fetchdat)) return 1;
        if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
        if (_x87_noyau.limite(10, true)) return 1;
        _x87_noyau.pointeurs_m(0xDB, fetchdat);
        if (!_x87_noyau.fst_f80(out var t))
        {
                writememq(x86.easeg, cpu_state.eaaddr, t.signif);
                writememw(x86.easeg, cpu_state.eaaddr + 8, t.signExp);
                if (cpu_state.abrt != 0) return 1;
                _x87_noyau.depiler();
        }
        _386.CLOCK_CYCLES(x87_timings_c.x87_timings.fst_80);
        return 0;
    }

    // FIST m16 (_x87_noyau.fist).
    internal static int opFISTiw_a16(uint32_t fetchdat)
    {
        if (_386.FP_ENTER()) return 1;
        if (fetch_ea_16(fetchdat)) return 1;
        if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
        if (_x87_noyau.limite(2, true)) return 1;
        _x87_noyau.pointeurs_m(0xDF, fetchdat);
        if (!_x87_noyau.fist(16, out var t))
        {
                seteaw((uint16_t)t);
                if (cpu_state.abrt != 0) return 1;
        }
        _386.CLOCK_CYCLES(x87_timings_c.x87_timings.fist_16);
        return 0;
    }

    internal static int opFISTiw_a32(uint32_t fetchdat)
    {
        if (_386.FP_ENTER()) return 1;
        if (fetch_ea_32(fetchdat)) return 1;
        if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
        if (_x87_noyau.limite(2, true)) return 1;
        _x87_noyau.pointeurs_m(0xDF, fetchdat);
        if (!_x87_noyau.fist(16, out var t))
        {
                seteaw((uint16_t)t);
                if (cpu_state.abrt != 0) return 1;
        }
        _386.CLOCK_CYCLES(x87_timings_c.x87_timings.fist_16);
        return 0;
    }

    // FISTP m16 (_x87_noyau.fist).
    internal static int opFISTPiw_a16(uint32_t fetchdat)
    {
        if (_386.FP_ENTER()) return 1;
        if (fetch_ea_16(fetchdat)) return 1;
        if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
        if (_x87_noyau.limite(2, true)) return 1;
        _x87_noyau.pointeurs_m(0xDF, fetchdat);
        if (!_x87_noyau.fist(16, out var t))
        {
                seteaw((uint16_t)t);
                if (cpu_state.abrt != 0) return 1;
                _x87_noyau.depiler();
        }
        _386.CLOCK_CYCLES(x87_timings_c.x87_timings.fist_16);
        return 0;
    }

    internal static int opFISTPiw_a32(uint32_t fetchdat)
    {
        if (_386.FP_ENTER()) return 1;
        if (fetch_ea_32(fetchdat)) return 1;
        if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
        if (_x87_noyau.limite(2, true)) return 1;
        _x87_noyau.pointeurs_m(0xDF, fetchdat);
        if (!_x87_noyau.fist(16, out var t))
        {
                seteaw((uint16_t)t);
                if (cpu_state.abrt != 0) return 1;
                _x87_noyau.depiler();
        }
        _386.CLOCK_CYCLES(x87_timings_c.x87_timings.fist_16);
        return 0;
    }

    // FIST m32 (_x87_noyau.fist).
    internal static int opFISTil_a16(uint32_t fetchdat)
    {
        if (_386.FP_ENTER()) return 1;
        if (fetch_ea_16(fetchdat)) return 1;
        if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
        if (_x87_noyau.limite(4, true)) return 1;
        _x87_noyau.pointeurs_m(0xDB, fetchdat);
        if (!_x87_noyau.fist(32, out var t))
        {
                seteal((uint32_t)t);
                if (cpu_state.abrt != 0) return 1;
        }
        _386.CLOCK_CYCLES(x87_timings_c.x87_timings.fist_32);
        return 0;
    }

    internal static int opFISTil_a32(uint32_t fetchdat)
    {
        if (_386.FP_ENTER()) return 1;
        if (fetch_ea_32(fetchdat)) return 1;
        if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
        if (_x87_noyau.limite(4, true)) return 1;
        _x87_noyau.pointeurs_m(0xDB, fetchdat);
        if (!_x87_noyau.fist(32, out var t))
        {
                seteal((uint32_t)t);
                if (cpu_state.abrt != 0) return 1;
        }
        _386.CLOCK_CYCLES(x87_timings_c.x87_timings.fist_32);
        return 0;
    }

    // FISTP m32 (_x87_noyau.fist).
    internal static int opFISTPil_a16(uint32_t fetchdat)
    {
        if (_386.FP_ENTER()) return 1;
        if (fetch_ea_16(fetchdat)) return 1;
        if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
        if (_x87_noyau.limite(4, true)) return 1;
        _x87_noyau.pointeurs_m(0xDB, fetchdat);
        if (!_x87_noyau.fist(32, out var t))
        {
                seteal((uint32_t)t);
                if (cpu_state.abrt != 0) return 1;
                _x87_noyau.depiler();
        }
        _386.CLOCK_CYCLES(x87_timings_c.x87_timings.fist_32);
        return 0;
    }

    internal static int opFISTPil_a32(uint32_t fetchdat)
    {
        if (_386.FP_ENTER()) return 1;
        if (fetch_ea_32(fetchdat)) return 1;
        if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
        if (_x87_noyau.limite(4, true)) return 1;
        _x87_noyau.pointeurs_m(0xDB, fetchdat);
        if (!_x87_noyau.fist(32, out var t))
        {
                seteal((uint32_t)t);
                if (cpu_state.abrt != 0) return 1;
                _x87_noyau.depiler();
        }
        _386.CLOCK_CYCLES(x87_timings_c.x87_timings.fist_32);
        return 0;
    }

    // FISTP m64 (_x87_noyau.fist).
    internal static int opFISTPiq_a16(uint32_t fetchdat)
    {
        if (_386.FP_ENTER()) return 1;
        if (fetch_ea_16(fetchdat)) return 1;
        if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
        if (_x87_noyau.limite(8, true)) return 1;
        _x87_noyau.pointeurs_m(0xDF, fetchdat);
        if (!_x87_noyau.fist(64, out var t))
        {
                seteaq((uint64_t)t);
                if (cpu_state.abrt != 0) return 1;
                _x87_noyau.depiler();
        }
        _386.CLOCK_CYCLES(x87_timings_c.x87_timings.fist_64);
        return 0;
    }

    internal static int opFISTPiq_a32(uint32_t fetchdat)
    {
        if (_386.FP_ENTER()) return 1;
        if (fetch_ea_32(fetchdat)) return 1;
        if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
        if (_x87_noyau.limite(8, true)) return 1;
        _x87_noyau.pointeurs_m(0xDF, fetchdat);
        if (!_x87_noyau.fist(64, out var t))
        {
                seteaq((uint64_t)t);
                if (cpu_state.abrt != 0) return 1;
                _x87_noyau.depiler();
        }
        _386.CLOCK_CYCLES(x87_timings_c.x87_timings.fist_64);
        return 0;
    }

    // FBSTP m80 (_x87_noyau.fbstp ; PB-195).
    internal static int opFBSTP_a16(uint32_t fetchdat)
    {
        if (_386.FP_ENTER()) return 1;
        if (fetch_ea_16(fetchdat)) return 1;
        if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
        if (_x87_noyau.limite(10, true)) return 1;
        _x87_noyau.pointeurs_m(0xDF, fetchdat);
        if (!_x87_noyau.fbstp(out var s, out var e))
        {
                writememq(x86.easeg, cpu_state.eaaddr, s);
                writememw(x86.easeg, cpu_state.eaaddr + 8, e);
                if (cpu_state.abrt != 0) return 1;
                _x87_noyau.depiler();
        }
        _386.CLOCK_CYCLES(x87_timings_c.x87_timings.fbstp);
        return 0;
    }

    internal static int opFBSTP_a32(uint32_t fetchdat)
    {
        if (_386.FP_ENTER()) return 1;
        if (fetch_ea_32(fetchdat)) return 1;
        if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
        if (_x87_noyau.limite(10, true)) return 1;
        _x87_noyau.pointeurs_m(0xDF, fetchdat);
        if (!_x87_noyau.fbstp(out var s, out var e))
        {
                writememq(x86.easeg, cpu_state.eaaddr, s);
                writememw(x86.easeg, cpu_state.eaaddr + 8, e);
                if (cpu_state.abrt != 0) return 1;
                _x87_noyau.depiler();
        }
        _386.CLOCK_CYCLES(x87_timings_c.x87_timings.fbstp);
        return 0;
    }

    // FLD ST(i).
    internal static int opFLD(uint32_t fetchdat)
    {
        if (_386.FP_ENTER()) return 1;
        _x87_noyau.pointeurs(0xD9, fetchdat);
        cpu_state.pc++;
        _x87_noyau.fld_st((int)(fetchdat & 7));
        _386.CLOCK_CYCLES(x87_timings_c.x87_timings.fld);
        return 0;
    }

    // FXCH ST(i).
    internal static int opFXCH(uint32_t fetchdat)
    {
        if (_386.FP_ENTER()) return 1;
        _x87_noyau.pointeurs(0xD9, fetchdat);
        cpu_state.pc++;
        _x87_noyau.fxch((int)(fetchdat & 7));
        _386.CLOCK_CYCLES(x87_timings_c.x87_timings.fxch);
        return 0;
    }

    // FST ST(i).
    internal static int opFST(uint32_t fetchdat)
    {
        if (_386.FP_ENTER()) return 1;
        _x87_noyau.pointeurs(0xDD, fetchdat);
        cpu_state.pc++;
        _x87_noyau.fst_st((int)(fetchdat & 7));
        _386.CLOCK_CYCLES(x87_timings_c.x87_timings.fst);
        return 0;
    }

    // FSTP ST(i).
    internal static int opFSTP(uint32_t fetchdat)
    {
        if (_386.FP_ENTER()) return 1;
        _x87_noyau.pointeurs(0xDD, fetchdat);
        cpu_state.pc++;
        if (!_x87_noyau.fst_st((int)(fetchdat & 7)))
                _x87_noyau.depiler();
        _386.CLOCK_CYCLES(x87_timings_c.x87_timings.fst);
        return 0;
    }

    // FSTP ST(i), D9 D8+i comme PCem.
    internal static int opFSTP_d9(uint32_t fetchdat)
    {
        if (_386.FP_ENTER()) return 1;
        _x87_noyau.pointeurs(0xD9, fetchdat);
        cpu_state.pc++;
        if (!_x87_noyau.fst_st((int)(fetchdat & 7)))
                _x87_noyau.depiler();
        _386.CLOCK_CYCLES(x87_timings_c.x87_timings.fst);
        return 0;
    }

    // FCHS.
    internal static int opFCHS(uint32_t fetchdat)
    {
        if (_386.FP_ENTER()) return 1;
        _x87_noyau.pointeurs(0xD9, fetchdat);
        cpu_state.pc++;
        _x87_noyau.signe(false);
        _386.CLOCK_CYCLES(x87_timings_c.x87_timings.fchs);
        return 0;
    }

    // FABS.
    internal static int opFABS(uint32_t fetchdat)
    {
        if (_386.FP_ENTER()) return 1;
        _x87_noyau.pointeurs(0xD9, fetchdat);
        cpu_state.pc++;
        _x87_noyau.signe(true);
        _386.CLOCK_CYCLES(x87_timings_c.x87_timings.fabs);
        return 0;
    }

    // FXAM (_x87_noyau.fxam).
    internal static int opFXAM(uint32_t fetchdat)
    {
        if (_386.FP_ENTER()) return 1;
        _x87_noyau.pointeurs(0xD9, fetchdat);
        cpu_state.pc++;
        _x87_noyau.fxam();
        _386.CLOCK_CYCLES(x87_timings_c.x87_timings.fxam);
        return 0;
    }

    // FLD1 et FLDZ.
    internal static int opFLDz1(uint32_t fetchdat)
    {
        if (_386.FP_ENTER()) return 1;
        _x87_noyau.pointeurs(0xD9, fetchdat);
        cpu_state.pc++;
        _x87_noyau.constante((int)(fetchdat & 7));
        _386.CLOCK_CYCLES(x87_timings_c.x87_timings.fld_z1);
        return 0;
    }

    // FLDL2T, FLDL2E, FLDPI, FLDLG2, FLDLN2 (_x87_noyau.constante).
    internal static int opFLDconst(uint32_t fetchdat)
    {
        if (_386.FP_ENTER()) return 1;
        _x87_noyau.pointeurs(0xD9, fetchdat);
        cpu_state.pc++;
        _x87_noyau.constante((int)(fetchdat & 7));
        _386.CLOCK_CYCLES(x87_timings_c.x87_timings.fld_const);
        return 0;
    }

    // FLDENV : l'environnement, dans la disposition du mode (_x87_noyau.fldenv ; PB-62, PB-208).
    internal static int opFLDENV_a16(uint32_t fetchdat)
    {
        int d;
        if (_386.FP_ENTER()) return 1;
        if (fetch_ea_16(fetchdat)) return 1;
        if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        d = _x87_noyau.disposition();
        if (_x87_noyau.limite(_x87_noyau.taille_env(d), false)) return 1;
        _x87_noyau.a_lire(d, false);
        for (var k = 0; k < _x87_noyau.n; k++)
        {
                _x87_noyau.mot[k] = readmemw(x86.easeg, cpu_state.eaaddr + _x87_noyau.off[k]);
                if (cpu_state.abrt != 0) return 1;
        }
        _x87_noyau.fldenv(d, false);
        _386.CLOCK_CYCLES(x87_timings_c.x87_timings.fldenv);
        return 0;
    }

    internal static int opFLDENV_a32(uint32_t fetchdat)
    {
        int d;
        if (_386.FP_ENTER()) return 1;
        if (fetch_ea_32(fetchdat)) return 1;
        if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        d = _x87_noyau.disposition();
        if (_x87_noyau.limite(_x87_noyau.taille_env(d), false)) return 1;
        _x87_noyau.a_lire(d, false);
        for (var k = 0; k < _x87_noyau.n; k++)
        {
                _x87_noyau.mot[k] = readmemw(x86.easeg, cpu_state.eaaddr + _x87_noyau.off[k]);
                if (cpu_state.abrt != 0) return 1;
        }
        _x87_noyau.fldenv(d, false);
        _386.CLOCK_CYCLES(x87_timings_c.x87_timings.fldenv);
        return 0;
    }

    // FNSTENV : l'environnement, puis les six exceptions masquées (PB-62 ; PB-207).
    internal static int opFNSTENV_a16(uint32_t fetchdat)
    {
        int d;
        if (_386.FP_ENTER()) return 1;
        if (fetch_ea_16(fetchdat)) return 1;
        if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
        d = _x87_noyau.disposition();
        if (_x87_noyau.limite(_x87_noyau.taille_env(d), true)) return 1;
        _x87_noyau.fstenv(d);
        for (var k = 0; k < _x87_noyau.n; k++)
        {
                writememw(x86.easeg, cpu_state.eaaddr + _x87_noyau.off[k], _x87_noyau.mot[k]);
                if (cpu_state.abrt != 0) return 1;
        }
        _x87_noyau.fstenv_fin();
        _386.CLOCK_CYCLES(x87_timings_c.x87_timings.fstenv);
        return 0;
    }

    internal static int opFNSTENV_a32(uint32_t fetchdat)
    {
        int d;
        if (_386.FP_ENTER()) return 1;
        if (fetch_ea_32(fetchdat)) return 1;
        if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
        d = _x87_noyau.disposition();
        if (_x87_noyau.limite(_x87_noyau.taille_env(d), true)) return 1;
        _x87_noyau.fstenv(d);
        for (var k = 0; k < _x87_noyau.n; k++)
        {
                writememw(x86.easeg, cpu_state.eaaddr + _x87_noyau.off[k], _x87_noyau.mot[k]);
                if (cpu_state.abrt != 0) return 1;
        }
        _x87_noyau.fstenv_fin();
        _386.CLOCK_CYCLES(x87_timings_c.x87_timings.fstenv);
        return 0;
    }

    // FRSTOR : l'environnement et les huit registres (PB-62, PB-199, PB-208).
    internal static int opFRSTOR_a16(uint32_t fetchdat)
    {
        int d;
        if (_386.FP_ENTER()) return 1;
        if (fetch_ea_16(fetchdat)) return 1;
        if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        d = _x87_noyau.disposition();
        if (_x87_noyau.limite(_x87_noyau.taille_env(d) + 80, false)) return 1;
        _x87_noyau.a_lire(d, true);
        for (var k = 0; k < _x87_noyau.n; k++)
        {
                _x87_noyau.mot[k] = readmemw(x86.easeg, cpu_state.eaaddr + _x87_noyau.off[k]);
                if (cpu_state.abrt != 0) return 1;
        }
        _x87_noyau.fldenv(d, true);
        _386.CLOCK_CYCLES(x87_timings_c.x87_timings.frstor);
        return 0;
    }

    internal static int opFRSTOR_a32(uint32_t fetchdat)
    {
        int d;
        if (_386.FP_ENTER()) return 1;
        if (fetch_ea_32(fetchdat)) return 1;
        if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        d = _x87_noyau.disposition();
        if (_x87_noyau.limite(_x87_noyau.taille_env(d) + 80, false)) return 1;
        _x87_noyau.a_lire(d, true);
        for (var k = 0; k < _x87_noyau.n; k++)
        {
                _x87_noyau.mot[k] = readmemw(x86.easeg, cpu_state.eaaddr + _x87_noyau.off[k]);
                if (cpu_state.abrt != 0) return 1;
        }
        _x87_noyau.fldenv(d, true);
        _386.CLOCK_CYCLES(x87_timings_c.x87_timings.frstor);
        return 0;
    }

    // FNSAVE : l'environnement et les huit registres de 80 bits, puis l'état de FNINIT (PB-62, PB-199).
    internal static int opFNSAVE_a16(uint32_t fetchdat)
    {
        int d;
        if (_386.FP_ENTER()) return 1;
        if (fetch_ea_16(fetchdat)) return 1;
        if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
        d = _x87_noyau.disposition();
        if (_x87_noyau.limite(_x87_noyau.taille_env(d) + 80, true)) return 1;
        _x87_noyau.fsave(d);
        for (var k = 0; k < _x87_noyau.n; k++)
        {
                writememw(x86.easeg, cpu_state.eaaddr + _x87_noyau.off[k], _x87_noyau.mot[k]);
                if (cpu_state.abrt != 0) return 1;
        }
        _x87_noyau.fsave_fin();
        _386.CLOCK_CYCLES(x87_timings_c.x87_timings.fsave);
        return 0;
    }

    internal static int opFNSAVE_a32(uint32_t fetchdat)
    {
        int d;
        if (_386.FP_ENTER()) return 1;
        if (fetch_ea_32(fetchdat)) return 1;
        if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
        d = _x87_noyau.disposition();
        if (_x87_noyau.limite(_x87_noyau.taille_env(d) + 80, true)) return 1;
        _x87_noyau.fsave(d);
        for (var k = 0; k < _x87_noyau.n; k++)
        {
                writememw(x86.easeg, cpu_state.eaaddr + _x87_noyau.off[k], _x87_noyau.mot[k]);
                if (cpu_state.abrt != 0) return 1;
        }
        _x87_noyau.fsave_fin();
        _386.CLOCK_CYCLES(x87_timings_c.x87_timings.fsave);
        return 0;
    }

    // FSETPM : le 287 et le 287XL passent au mode protégé, jusqu'au RESET (287 PRM, FSETPM ; PB-62) ; le 387 et le 486
    //   n'en font rien (387 PRM § 4.8). Le 8087 n'a pas DB E4 : N7.
    internal static int opFSETPM(uint32_t fetchdat)
    {
        if (_386.FP_ENTER()) return 1;
        cpu_state.pc++;
        if (cpu_c.fpu_type is cpu_c.FPU_287 or cpu_c.FPU_287XL)
                _x87_noyau.fsetpm = true;
        _386.CLOCK_CYCLES(x87_timings_c.x87_timings.fnop);
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
        _x87_noyau.memoire(_386.x86_opcodes_d9_a16!, 0, opFLDs_a16);
        _x87_noyau.memoire(_386.x86_opcodes_d9_a32!, 0, opFLDs_a32);
        _x87_noyau.memoire(_386.x86_opcodes_dd_a16!, 0, opFLDd_a16);
        _x87_noyau.memoire(_386.x86_opcodes_dd_a32!, 0, opFLDd_a32);
        _x87_noyau.memoire(_386.x86_opcodes_db_a16!, 5, opFLDe_a16);
        _x87_noyau.memoire(_386.x86_opcodes_db_a32!, 5, opFLDe_a32);
        _x87_noyau.memoire(_386.x86_opcodes_df_a16!, 0, opFILDiw_a16);
        _x87_noyau.memoire(_386.x86_opcodes_df_a32!, 0, opFILDiw_a32);
        _x87_noyau.memoire(_386.x86_opcodes_db_a16!, 0, opFILDil_a16);
        _x87_noyau.memoire(_386.x86_opcodes_db_a32!, 0, opFILDil_a32);
        _x87_noyau.memoire(_386.x86_opcodes_df_a16!, 5, opFILDiq_a16);
        _x87_noyau.memoire(_386.x86_opcodes_df_a32!, 5, opFILDiq_a32);
        _x87_noyau.memoire(_386.x86_opcodes_df_a16!, 4, opFBLD_a16);
        _x87_noyau.memoire(_386.x86_opcodes_df_a32!, 4, opFBLD_a32);
        _x87_noyau.memoire(_386.x86_opcodes_d9_a16!, 2, opFSTs_a16);
        _x87_noyau.memoire(_386.x86_opcodes_d9_a32!, 2, opFSTs_a32);
        _x87_noyau.memoire(_386.x86_opcodes_d9_a16!, 3, opFSTPs_a16);
        _x87_noyau.memoire(_386.x86_opcodes_d9_a32!, 3, opFSTPs_a32);
        _x87_noyau.memoire(_386.x86_opcodes_dd_a16!, 2, opFSTd_a16);
        _x87_noyau.memoire(_386.x86_opcodes_dd_a32!, 2, opFSTd_a32);
        _x87_noyau.memoire(_386.x86_opcodes_dd_a16!, 3, opFSTPd_a16);
        _x87_noyau.memoire(_386.x86_opcodes_dd_a32!, 3, opFSTPd_a32);
        _x87_noyau.memoire(_386.x86_opcodes_db_a16!, 7, opFSTPe_a16);
        _x87_noyau.memoire(_386.x86_opcodes_db_a32!, 7, opFSTPe_a32);
        _x87_noyau.memoire(_386.x86_opcodes_df_a16!, 2, opFISTiw_a16);
        _x87_noyau.memoire(_386.x86_opcodes_df_a32!, 2, opFISTiw_a32);
        _x87_noyau.memoire(_386.x86_opcodes_df_a16!, 3, opFISTPiw_a16);
        _x87_noyau.memoire(_386.x86_opcodes_df_a32!, 3, opFISTPiw_a32);
        _x87_noyau.memoire(_386.x86_opcodes_db_a16!, 2, opFISTil_a16);
        _x87_noyau.memoire(_386.x86_opcodes_db_a32!, 2, opFISTil_a32);
        _x87_noyau.memoire(_386.x86_opcodes_db_a16!, 3, opFISTPil_a16);
        _x87_noyau.memoire(_386.x86_opcodes_db_a32!, 3, opFISTPil_a32);
        _x87_noyau.memoire(_386.x86_opcodes_df_a16!, 7, opFISTPiq_a16);
        _x87_noyau.memoire(_386.x86_opcodes_df_a32!, 7, opFISTPiq_a32);
        _x87_noyau.memoire(_386.x86_opcodes_df_a16!, 6, opFBSTP_a16);
        _x87_noyau.memoire(_386.x86_opcodes_df_a32!, 6, opFBSTP_a32);
        _386.x86_opcodes_d9_a16![0xC0] = opFLD;
        _386.x86_opcodes_d9_a32![0xC0] = opFLD;
        _386.x86_opcodes_d9_a16![0xC1] = opFLD;
        _386.x86_opcodes_d9_a32![0xC1] = opFLD;
        _386.x86_opcodes_d9_a16![0xC2] = opFLD;
        _386.x86_opcodes_d9_a32![0xC2] = opFLD;
        _386.x86_opcodes_d9_a16![0xC3] = opFLD;
        _386.x86_opcodes_d9_a32![0xC3] = opFLD;
        _386.x86_opcodes_d9_a16![0xC4] = opFLD;
        _386.x86_opcodes_d9_a32![0xC4] = opFLD;
        _386.x86_opcodes_d9_a16![0xC5] = opFLD;
        _386.x86_opcodes_d9_a32![0xC5] = opFLD;
        _386.x86_opcodes_d9_a16![0xC6] = opFLD;
        _386.x86_opcodes_d9_a32![0xC6] = opFLD;
        _386.x86_opcodes_d9_a16![0xC7] = opFLD;
        _386.x86_opcodes_d9_a32![0xC7] = opFLD;
        _386.x86_opcodes_d9_a16![0xC8] = opFXCH;
        _386.x86_opcodes_d9_a32![0xC8] = opFXCH;
        _386.x86_opcodes_d9_a16![0xC9] = opFXCH;
        _386.x86_opcodes_d9_a32![0xC9] = opFXCH;
        _386.x86_opcodes_d9_a16![0xCA] = opFXCH;
        _386.x86_opcodes_d9_a32![0xCA] = opFXCH;
        _386.x86_opcodes_d9_a16![0xCB] = opFXCH;
        _386.x86_opcodes_d9_a32![0xCB] = opFXCH;
        _386.x86_opcodes_d9_a16![0xCC] = opFXCH;
        _386.x86_opcodes_d9_a32![0xCC] = opFXCH;
        _386.x86_opcodes_d9_a16![0xCD] = opFXCH;
        _386.x86_opcodes_d9_a32![0xCD] = opFXCH;
        _386.x86_opcodes_d9_a16![0xCE] = opFXCH;
        _386.x86_opcodes_d9_a32![0xCE] = opFXCH;
        _386.x86_opcodes_d9_a16![0xCF] = opFXCH;
        _386.x86_opcodes_d9_a32![0xCF] = opFXCH;
        _386.x86_opcodes_dd_a16![0xD0] = opFST;
        _386.x86_opcodes_dd_a32![0xD0] = opFST;
        _386.x86_opcodes_dd_a16![0xD1] = opFST;
        _386.x86_opcodes_dd_a32![0xD1] = opFST;
        _386.x86_opcodes_dd_a16![0xD2] = opFST;
        _386.x86_opcodes_dd_a32![0xD2] = opFST;
        _386.x86_opcodes_dd_a16![0xD3] = opFST;
        _386.x86_opcodes_dd_a32![0xD3] = opFST;
        _386.x86_opcodes_dd_a16![0xD4] = opFST;
        _386.x86_opcodes_dd_a32![0xD4] = opFST;
        _386.x86_opcodes_dd_a16![0xD5] = opFST;
        _386.x86_opcodes_dd_a32![0xD5] = opFST;
        _386.x86_opcodes_dd_a16![0xD6] = opFST;
        _386.x86_opcodes_dd_a32![0xD6] = opFST;
        _386.x86_opcodes_dd_a16![0xD7] = opFST;
        _386.x86_opcodes_dd_a32![0xD7] = opFST;
        _386.x86_opcodes_dd_a16![0xD8] = opFSTP;
        _386.x86_opcodes_dd_a32![0xD8] = opFSTP;
        _386.x86_opcodes_dd_a16![0xD9] = opFSTP;
        _386.x86_opcodes_dd_a32![0xD9] = opFSTP;
        _386.x86_opcodes_dd_a16![0xDA] = opFSTP;
        _386.x86_opcodes_dd_a32![0xDA] = opFSTP;
        _386.x86_opcodes_dd_a16![0xDB] = opFSTP;
        _386.x86_opcodes_dd_a32![0xDB] = opFSTP;
        _386.x86_opcodes_dd_a16![0xDC] = opFSTP;
        _386.x86_opcodes_dd_a32![0xDC] = opFSTP;
        _386.x86_opcodes_dd_a16![0xDD] = opFSTP;
        _386.x86_opcodes_dd_a32![0xDD] = opFSTP;
        _386.x86_opcodes_dd_a16![0xDE] = opFSTP;
        _386.x86_opcodes_dd_a32![0xDE] = opFSTP;
        _386.x86_opcodes_dd_a16![0xDF] = opFSTP;
        _386.x86_opcodes_dd_a32![0xDF] = opFSTP;
        _386.x86_opcodes_d9_a16![0xD8] = opFSTP_d9;
        _386.x86_opcodes_d9_a32![0xD8] = opFSTP_d9;
        _386.x86_opcodes_d9_a16![0xD9] = opFSTP_d9;
        _386.x86_opcodes_d9_a32![0xD9] = opFSTP_d9;
        _386.x86_opcodes_d9_a16![0xDA] = opFSTP_d9;
        _386.x86_opcodes_d9_a32![0xDA] = opFSTP_d9;
        _386.x86_opcodes_d9_a16![0xDB] = opFSTP_d9;
        _386.x86_opcodes_d9_a32![0xDB] = opFSTP_d9;
        _386.x86_opcodes_d9_a16![0xDC] = opFSTP_d9;
        _386.x86_opcodes_d9_a32![0xDC] = opFSTP_d9;
        _386.x86_opcodes_d9_a16![0xDD] = opFSTP_d9;
        _386.x86_opcodes_d9_a32![0xDD] = opFSTP_d9;
        _386.x86_opcodes_d9_a16![0xDE] = opFSTP_d9;
        _386.x86_opcodes_d9_a32![0xDE] = opFSTP_d9;
        _386.x86_opcodes_d9_a16![0xDF] = opFSTP_d9;
        _386.x86_opcodes_d9_a32![0xDF] = opFSTP_d9;
        _386.x86_opcodes_d9_a16![0xE0] = opFCHS;
        _386.x86_opcodes_d9_a32![0xE0] = opFCHS;
        _386.x86_opcodes_d9_a16![0xE1] = opFABS;
        _386.x86_opcodes_d9_a32![0xE1] = opFABS;
        _386.x86_opcodes_d9_a16![0xE5] = opFXAM;
        _386.x86_opcodes_d9_a32![0xE5] = opFXAM;
        _386.x86_opcodes_d9_a16![0xE8] = opFLDz1;
        _386.x86_opcodes_d9_a32![0xE8] = opFLDz1;
        _386.x86_opcodes_d9_a16![0xEE] = opFLDz1;
        _386.x86_opcodes_d9_a32![0xEE] = opFLDz1;
        _386.x86_opcodes_d9_a16![0xE9] = opFLDconst;
        _386.x86_opcodes_d9_a32![0xE9] = opFLDconst;
        _386.x86_opcodes_d9_a16![0xEA] = opFLDconst;
        _386.x86_opcodes_d9_a32![0xEA] = opFLDconst;
        _386.x86_opcodes_d9_a16![0xEB] = opFLDconst;
        _386.x86_opcodes_d9_a32![0xEB] = opFLDconst;
        _386.x86_opcodes_d9_a16![0xEC] = opFLDconst;
        _386.x86_opcodes_d9_a32![0xEC] = opFLDconst;
        _386.x86_opcodes_d9_a16![0xED] = opFLDconst;
        _386.x86_opcodes_d9_a32![0xED] = opFLDconst;
        _x87_noyau.memoire(_386.x86_opcodes_d9_a16!, 4, opFLDENV_a16);
        _x87_noyau.memoire(_386.x86_opcodes_d9_a32!, 4, opFLDENV_a32);
        _x87_noyau.memoire(_386.x86_opcodes_d9_a16!, 6, opFNSTENV_a16);
        _x87_noyau.memoire(_386.x86_opcodes_d9_a32!, 6, opFNSTENV_a32);
        _x87_noyau.memoire(_386.x86_opcodes_dd_a16!, 4, opFRSTOR_a16);
        _x87_noyau.memoire(_386.x86_opcodes_dd_a32!, 4, opFRSTOR_a32);
        _x87_noyau.memoire(_386.x86_opcodes_dd_a16!, 6, opFNSAVE_a16);
        _x87_noyau.memoire(_386.x86_opcodes_dd_a32!, 6, opFNSAVE_a32);
        _386.x86_opcodes_db_a16![0xE4] = opFSETPM;
        _386.x86_opcodes_db_a32![0xE4] = opFSETPM;
    }
}
