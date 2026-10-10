// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/includes/private/cpu/x87_ops_arith.h  (lignes 1-452)
// SHA256: voir oracle.tsv ; vérifier avec tools/check-oracle.sh
// STATUS: partial — G4.3 : les quatre-vingt-huit handlers d'arithmétique que les tables non
//         686 référencent. La macro opFPU (:3-112) est EXPANSÉE pour ses huit instanciations
//         (:114-120) — FADD, FCOM, FCOMP, FDIV, FDIVR, FMUL, FSUB, FSUBR × s, d, iw, il × a16,
//         a32 : 64 handlers —, puis les 24 formes registre. FCOMI, FCOMIP, FUCOMI et FUCOMIP
//         (:209-236, :421-449) ne sont que dans les tables `_686_` : omis, hors de portée.
//         Comptés après `gcc -E` de l'unité de l'oracle : 92 handlers, 16 appels fesetround.
//
// GÉNÉRÉ par règles depuis le C (voir x87_ops_loadstore.cs), et trois écarts nommés :
//   - x87_div (macro, `return 1` du handler) devient `if (x87_div(ref dst, a, b)) return 1;` ;
//   - le bloc fesetround des opFADD mémoire devient x87_fadd_dirige (PB-48, DEVIATION) ;
//   - x87_compare / x87_ucompare, de l'asm x87 hôte chez PCem, sont leur sémantique (DEVIATION).
// Quatre défauts de PCem reproduits : PB-57 (opFCOM), PB-58 (opFCOMPP), PB-59 (x87_div) et
// PB-60 (le NaN propagé suit l'ordre des opérandes que GCC a choisi : X87AddSd, X87MulSd) ; PB-57 et
// PB-58 corrigés en mode matériel, dans les tables du mode (x87.Materiel.cs).

using static iXtal26.Cpu._386_common;
using static iXtal26.Cpu.x86;

namespace iXtal26.Cpu;

internal static partial class _386
{
    // pcem: x87_ops_arith.h:3-120, opFPU(s, x87_ts, 16)
    private static int opFADDs_a16(uint32_t fetchdat)
    {
            x87_ts t = default;
            if (FP_ENTER()) return 1;
            if (fetch_ea_16(fetchdat)) return 1;
            if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
            t.i = geteal();
            if (cpu_state.abrt != 0)
                    return 1;
            // pcem bug, reproduced: PB-48 — RC ne vaut que pour ce FADD mémoire, par x87_fadd_dirige :
            //   fesetround(rounding_modes[RC]) ; ST(0) += use_var ; fesetround(FE_TONEAREST) (x87_ops_arith.h:12-16).
            if (((cpu_state.npxc >> 10) & 3) != 0)
                    ST(0) = x87_fadd_dirige(t.s, ST(0), (cpu_state.npxc >> 10) & 3);
            else
                    // pcem bug, reproduced: PB-60 — le NaN qui survit suit l'ordre des opérandes que GCC a choisi.
                    ST(0) = X87AddSd(t.s, ST(0)); // PB-60 : la mémoire en premier
            cpu_state.tag[cpu_state.TOP & 7] = x87_c.TAG_VALID;
            CLOCK_CYCLES(x87_timings_c.x87_timings.fadd_32);
            return 0;
    }

    // pcem: x87_ops_arith.h:3-120, opFPU(s, x87_ts, 16)
    private static int opFCOMs_a16(uint32_t fetchdat)
    {
            x87_ts t = default;
            if (FP_ENTER()) return 1;
            if (fetch_ea_16(fetchdat)) return 1;
            if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
            t.i = geteal();
            if (cpu_state.abrt != 0)
                    return 1;
            // pcem bug, fixed in hardware mode: PB-213 — C1 n'est pas remis à zéro (387 et suivants ; x87.Materiel.cs).
            cpu_state.npxs &= unchecked((uint16_t)~(x87_c.C0 | x87_c.C2 | x87_c.C3));
            cpu_state.npxs |= x87_compare(ST(0), (double)t.s);
            CLOCK_CYCLES(x87_timings_c.x87_timings.fcom_32);
            return 0;
    }

    // pcem: x87_ops_arith.h:3-120, opFPU(s, x87_ts, 16)
    private static int opFCOMPs_a16(uint32_t fetchdat)
    {
            x87_ts t = default;
            if (FP_ENTER()) return 1;
            if (fetch_ea_16(fetchdat)) return 1;
            if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
            t.i = geteal();
            if (cpu_state.abrt != 0)
                    return 1;
            // pcem bug, fixed in hardware mode: PB-213 — C1 n'est pas remis à zéro (387 et suivants ; x87.Materiel.cs).
            cpu_state.npxs &= unchecked((uint16_t)~(x87_c.C0 | x87_c.C2 | x87_c.C3));
            cpu_state.npxs |= x87_compare(ST(0), (double)t.s);
            x87_pop();
            CLOCK_CYCLES(x87_timings_c.x87_timings.fcom_32);
            return 0;
    }

    // pcem: x87_ops_arith.h:3-120, opFPU(s, x87_ts, 16)
    private static int opFDIVs_a16(uint32_t fetchdat)
    {
            x87_ts t = default;
            if (FP_ENTER()) return 1;
            if (fetch_ea_16(fetchdat)) return 1;
            if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
            t.i = geteal();
            if (cpu_state.abrt != 0)
                    return 1;
            if (x87_div(ref ST(0), ST(0), t.s)) return 1;
            cpu_state.tag[cpu_state.TOP & 7] = x87_c.TAG_VALID;
            CLOCK_CYCLES(x87_timings_c.x87_timings.fdiv_32);
            return 0;
    }

    // pcem: x87_ops_arith.h:3-120, opFPU(s, x87_ts, 16)
    private static int opFDIVRs_a16(uint32_t fetchdat)
    {
            x87_ts t = default;
            if (FP_ENTER()) return 1;
            if (fetch_ea_16(fetchdat)) return 1;
            if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
            t.i = geteal();
            if (cpu_state.abrt != 0)
                    return 1;
            if (x87_div(ref ST(0), t.s, ST(0))) return 1;
            cpu_state.tag[cpu_state.TOP & 7] = x87_c.TAG_VALID;
            CLOCK_CYCLES(x87_timings_c.x87_timings.fdiv_32);
            return 0;
    }

    // pcem: x87_ops_arith.h:3-120, opFPU(s, x87_ts, 16)
    private static int opFMULs_a16(uint32_t fetchdat)
    {
            x87_ts t = default;
            if (FP_ENTER()) return 1;
            if (fetch_ea_16(fetchdat)) return 1;
            if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
            t.i = geteal();
            if (cpu_state.abrt != 0)
                    return 1;
            // pcem bug, reproduced: PB-60 — le NaN qui survit suit l'ordre des opérandes que GCC a choisi.
            ST(0) = X87MulSd(t.s, ST(0)); // PB-60 : la mémoire en premier
            cpu_state.tag[cpu_state.TOP & 7] = x87_c.TAG_VALID;
            CLOCK_CYCLES(x87_timings_c.x87_timings.fmul_32);
            return 0;
    }

    // pcem: x87_ops_arith.h:3-120, opFPU(s, x87_ts, 16)
    private static int opFSUBs_a16(uint32_t fetchdat)
    {
            x87_ts t = default;
            if (FP_ENTER()) return 1;
            if (fetch_ea_16(fetchdat)) return 1;
            if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
            t.i = geteal();
            if (cpu_state.abrt != 0)
                    return 1;
            ST(0) -= t.s;
            cpu_state.tag[cpu_state.TOP & 7] = x87_c.TAG_VALID;
            CLOCK_CYCLES(x87_timings_c.x87_timings.fadd_32);
            return 0;
    }

    // pcem: x87_ops_arith.h:3-120, opFPU(s, x87_ts, 16)
    private static int opFSUBRs_a16(uint32_t fetchdat)
    {
            x87_ts t = default;
            if (FP_ENTER()) return 1;
            if (fetch_ea_16(fetchdat)) return 1;
            if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
            t.i = geteal();
            if (cpu_state.abrt != 0)
                    return 1;
            ST(0) = t.s - ST(0);
            cpu_state.tag[cpu_state.TOP & 7] = x87_c.TAG_VALID;
            CLOCK_CYCLES(x87_timings_c.x87_timings.fadd_32);
            return 0;
    }

    // pcem: x87_ops_arith.h:3-120, opFPU(s, x87_ts, 32)
    private static int opFADDs_a32(uint32_t fetchdat)
    {
            x87_ts t = default;
            if (FP_ENTER()) return 1;
            if (fetch_ea_32(fetchdat)) return 1;
            if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
            t.i = geteal();
            if (cpu_state.abrt != 0)
                    return 1;
            // pcem bug, reproduced: PB-48 — RC ne vaut que pour ce FADD mémoire, par x87_fadd_dirige :
            //   fesetround(rounding_modes[RC]) ; ST(0) += use_var ; fesetround(FE_TONEAREST) (x87_ops_arith.h:12-16).
            if (((cpu_state.npxc >> 10) & 3) != 0)
                    ST(0) = x87_fadd_dirige(t.s, ST(0), (cpu_state.npxc >> 10) & 3);
            else
                    // pcem bug, reproduced: PB-60 — le NaN qui survit suit l'ordre des opérandes que GCC a choisi.
                    ST(0) = X87AddSd(t.s, ST(0)); // PB-60 : la mémoire en premier
            cpu_state.tag[cpu_state.TOP & 7] = x87_c.TAG_VALID;
            CLOCK_CYCLES(x87_timings_c.x87_timings.fadd_32);
            return 0;
    }

    // pcem: x87_ops_arith.h:3-120, opFPU(s, x87_ts, 32)
    private static int opFCOMs_a32(uint32_t fetchdat)
    {
            x87_ts t = default;
            if (FP_ENTER()) return 1;
            if (fetch_ea_32(fetchdat)) return 1;
            if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
            t.i = geteal();
            if (cpu_state.abrt != 0)
                    return 1;
            // pcem bug, fixed in hardware mode: PB-213 — C1 n'est pas remis à zéro (387 et suivants ; x87.Materiel.cs).
            cpu_state.npxs &= unchecked((uint16_t)~(x87_c.C0 | x87_c.C2 | x87_c.C3));
            cpu_state.npxs |= x87_compare(ST(0), (double)t.s);
            CLOCK_CYCLES(x87_timings_c.x87_timings.fcom_32);
            return 0;
    }

    // pcem: x87_ops_arith.h:3-120, opFPU(s, x87_ts, 32)
    private static int opFCOMPs_a32(uint32_t fetchdat)
    {
            x87_ts t = default;
            if (FP_ENTER()) return 1;
            if (fetch_ea_32(fetchdat)) return 1;
            if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
            t.i = geteal();
            if (cpu_state.abrt != 0)
                    return 1;
            // pcem bug, fixed in hardware mode: PB-213 — C1 n'est pas remis à zéro (387 et suivants ; x87.Materiel.cs).
            cpu_state.npxs &= unchecked((uint16_t)~(x87_c.C0 | x87_c.C2 | x87_c.C3));
            cpu_state.npxs |= x87_compare(ST(0), (double)t.s);
            x87_pop();
            CLOCK_CYCLES(x87_timings_c.x87_timings.fcom_32);
            return 0;
    }

    // pcem: x87_ops_arith.h:3-120, opFPU(s, x87_ts, 32)
    private static int opFDIVs_a32(uint32_t fetchdat)
    {
            x87_ts t = default;
            if (FP_ENTER()) return 1;
            if (fetch_ea_32(fetchdat)) return 1;
            if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
            t.i = geteal();
            if (cpu_state.abrt != 0)
                    return 1;
            if (x87_div(ref ST(0), ST(0), t.s)) return 1;
            cpu_state.tag[cpu_state.TOP & 7] = x87_c.TAG_VALID;
            CLOCK_CYCLES(x87_timings_c.x87_timings.fdiv_32);
            return 0;
    }

    // pcem: x87_ops_arith.h:3-120, opFPU(s, x87_ts, 32)
    private static int opFDIVRs_a32(uint32_t fetchdat)
    {
            x87_ts t = default;
            if (FP_ENTER()) return 1;
            if (fetch_ea_32(fetchdat)) return 1;
            if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
            t.i = geteal();
            if (cpu_state.abrt != 0)
                    return 1;
            if (x87_div(ref ST(0), t.s, ST(0))) return 1;
            cpu_state.tag[cpu_state.TOP & 7] = x87_c.TAG_VALID;
            CLOCK_CYCLES(x87_timings_c.x87_timings.fdiv_32);
            return 0;
    }

    // pcem: x87_ops_arith.h:3-120, opFPU(s, x87_ts, 32)
    private static int opFMULs_a32(uint32_t fetchdat)
    {
            x87_ts t = default;
            if (FP_ENTER()) return 1;
            if (fetch_ea_32(fetchdat)) return 1;
            if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
            t.i = geteal();
            if (cpu_state.abrt != 0)
                    return 1;
            // pcem bug, reproduced: PB-60 — le NaN qui survit suit l'ordre des opérandes que GCC a choisi.
            ST(0) = X87MulSd(t.s, ST(0)); // PB-60 : la mémoire en premier
            cpu_state.tag[cpu_state.TOP & 7] = x87_c.TAG_VALID;
            CLOCK_CYCLES(x87_timings_c.x87_timings.fmul_32);
            return 0;
    }

    // pcem: x87_ops_arith.h:3-120, opFPU(s, x87_ts, 32)
    private static int opFSUBs_a32(uint32_t fetchdat)
    {
            x87_ts t = default;
            if (FP_ENTER()) return 1;
            if (fetch_ea_32(fetchdat)) return 1;
            if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
            t.i = geteal();
            if (cpu_state.abrt != 0)
                    return 1;
            ST(0) -= t.s;
            cpu_state.tag[cpu_state.TOP & 7] = x87_c.TAG_VALID;
            CLOCK_CYCLES(x87_timings_c.x87_timings.fadd_32);
            return 0;
    }

    // pcem: x87_ops_arith.h:3-120, opFPU(s, x87_ts, 32)
    private static int opFSUBRs_a32(uint32_t fetchdat)
    {
            x87_ts t = default;
            if (FP_ENTER()) return 1;
            if (fetch_ea_32(fetchdat)) return 1;
            if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
            t.i = geteal();
            if (cpu_state.abrt != 0)
                    return 1;
            ST(0) = t.s - ST(0);
            cpu_state.tag[cpu_state.TOP & 7] = x87_c.TAG_VALID;
            CLOCK_CYCLES(x87_timings_c.x87_timings.fadd_32);
            return 0;
    }

    // pcem: x87_ops_arith.h:3-120, opFPU(d, x87_td, 16)
    private static int opFADDd_a16(uint32_t fetchdat)
    {
            x87_td t = default;
            if (FP_ENTER()) return 1;
            if (fetch_ea_16(fetchdat)) return 1;
            if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
            t.i = geteaq();
            if (cpu_state.abrt != 0)
                    return 1;
            // pcem bug, reproduced: PB-48 — RC ne vaut que pour ce FADD mémoire, par x87_fadd_dirige :
            //   fesetround(rounding_modes[RC]) ; ST(0) += use_var ; fesetround(FE_TONEAREST) (x87_ops_arith.h:12-16).
            if (((cpu_state.npxc >> 10) & 3) != 0)
                    ST(0) = x87_fadd_dirige(t.d, ST(0), (cpu_state.npxc >> 10) & 3);
            else
                    // pcem bug, reproduced: PB-60 — le NaN qui survit suit l'ordre des opérandes que GCC a choisi.
                    ST(0) = X87AddSd(t.d, ST(0)); // PB-60 : la mémoire en premier
            cpu_state.tag[cpu_state.TOP & 7] = x87_c.TAG_VALID;
            CLOCK_CYCLES(x87_timings_c.x87_timings.fadd_64);
            return 0;
    }

    // pcem: x87_ops_arith.h:3-120, opFPU(d, x87_td, 16)
    private static int opFCOMd_a16(uint32_t fetchdat)
    {
            x87_td t = default;
            if (FP_ENTER()) return 1;
            if (fetch_ea_16(fetchdat)) return 1;
            if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
            t.i = geteaq();
            if (cpu_state.abrt != 0)
                    return 1;
            // pcem bug, fixed in hardware mode: PB-213 — C1 n'est pas remis à zéro (387 et suivants ; x87.Materiel.cs).
            cpu_state.npxs &= unchecked((uint16_t)~(x87_c.C0 | x87_c.C2 | x87_c.C3));
            cpu_state.npxs |= x87_compare(ST(0), (double)t.d);
            CLOCK_CYCLES(x87_timings_c.x87_timings.fcom_64);
            return 0;
    }

    // pcem: x87_ops_arith.h:3-120, opFPU(d, x87_td, 16)
    private static int opFCOMPd_a16(uint32_t fetchdat)
    {
            x87_td t = default;
            if (FP_ENTER()) return 1;
            if (fetch_ea_16(fetchdat)) return 1;
            if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
            t.i = geteaq();
            if (cpu_state.abrt != 0)
                    return 1;
            // pcem bug, fixed in hardware mode: PB-213 — C1 n'est pas remis à zéro (387 et suivants ; x87.Materiel.cs).
            cpu_state.npxs &= unchecked((uint16_t)~(x87_c.C0 | x87_c.C2 | x87_c.C3));
            cpu_state.npxs |= x87_compare(ST(0), (double)t.d);
            x87_pop();
            CLOCK_CYCLES(x87_timings_c.x87_timings.fcom_64);
            return 0;
    }

    // pcem: x87_ops_arith.h:3-120, opFPU(d, x87_td, 16)
    private static int opFDIVd_a16(uint32_t fetchdat)
    {
            x87_td t = default;
            if (FP_ENTER()) return 1;
            if (fetch_ea_16(fetchdat)) return 1;
            if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
            t.i = geteaq();
            if (cpu_state.abrt != 0)
                    return 1;
            if (x87_div(ref ST(0), ST(0), t.d)) return 1;
            cpu_state.tag[cpu_state.TOP & 7] = x87_c.TAG_VALID;
            CLOCK_CYCLES(x87_timings_c.x87_timings.fdiv_64);
            return 0;
    }

    // pcem: x87_ops_arith.h:3-120, opFPU(d, x87_td, 16)
    private static int opFDIVRd_a16(uint32_t fetchdat)
    {
            x87_td t = default;
            if (FP_ENTER()) return 1;
            if (fetch_ea_16(fetchdat)) return 1;
            if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
            t.i = geteaq();
            if (cpu_state.abrt != 0)
                    return 1;
            if (x87_div(ref ST(0), t.d, ST(0))) return 1;
            cpu_state.tag[cpu_state.TOP & 7] = x87_c.TAG_VALID;
            CLOCK_CYCLES(x87_timings_c.x87_timings.fdiv_64);
            return 0;
    }

    // pcem: x87_ops_arith.h:3-120, opFPU(d, x87_td, 16)
    private static int opFMULd_a16(uint32_t fetchdat)
    {
            x87_td t = default;
            if (FP_ENTER()) return 1;
            if (fetch_ea_16(fetchdat)) return 1;
            if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
            t.i = geteaq();
            if (cpu_state.abrt != 0)
                    return 1;
            // pcem bug, reproduced: PB-60 — le NaN qui survit suit l'ordre des opérandes que GCC a choisi.
            ST(0) = X87MulSd(t.d, ST(0)); // PB-60 : la mémoire en premier
            cpu_state.tag[cpu_state.TOP & 7] = x87_c.TAG_VALID;
            CLOCK_CYCLES(x87_timings_c.x87_timings.fmul_64);
            return 0;
    }

    // pcem: x87_ops_arith.h:3-120, opFPU(d, x87_td, 16)
    private static int opFSUBd_a16(uint32_t fetchdat)
    {
            x87_td t = default;
            if (FP_ENTER()) return 1;
            if (fetch_ea_16(fetchdat)) return 1;
            if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
            t.i = geteaq();
            if (cpu_state.abrt != 0)
                    return 1;
            ST(0) -= t.d;
            cpu_state.tag[cpu_state.TOP & 7] = x87_c.TAG_VALID;
            CLOCK_CYCLES(x87_timings_c.x87_timings.fadd_64);
            return 0;
    }

    // pcem: x87_ops_arith.h:3-120, opFPU(d, x87_td, 16)
    private static int opFSUBRd_a16(uint32_t fetchdat)
    {
            x87_td t = default;
            if (FP_ENTER()) return 1;
            if (fetch_ea_16(fetchdat)) return 1;
            if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
            t.i = geteaq();
            if (cpu_state.abrt != 0)
                    return 1;
            ST(0) = t.d - ST(0);
            cpu_state.tag[cpu_state.TOP & 7] = x87_c.TAG_VALID;
            CLOCK_CYCLES(x87_timings_c.x87_timings.fadd_64);
            return 0;
    }

    // pcem: x87_ops_arith.h:3-120, opFPU(d, x87_td, 32)
    private static int opFADDd_a32(uint32_t fetchdat)
    {
            x87_td t = default;
            if (FP_ENTER()) return 1;
            if (fetch_ea_32(fetchdat)) return 1;
            if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
            t.i = geteaq();
            if (cpu_state.abrt != 0)
                    return 1;
            // pcem bug, reproduced: PB-48 — RC ne vaut que pour ce FADD mémoire, par x87_fadd_dirige :
            //   fesetround(rounding_modes[RC]) ; ST(0) += use_var ; fesetround(FE_TONEAREST) (x87_ops_arith.h:12-16).
            if (((cpu_state.npxc >> 10) & 3) != 0)
                    ST(0) = x87_fadd_dirige(t.d, ST(0), (cpu_state.npxc >> 10) & 3);
            else
                    // pcem bug, reproduced: PB-60 — le NaN qui survit suit l'ordre des opérandes que GCC a choisi.
                    ST(0) = X87AddSd(t.d, ST(0)); // PB-60 : la mémoire en premier
            cpu_state.tag[cpu_state.TOP & 7] = x87_c.TAG_VALID;
            CLOCK_CYCLES(x87_timings_c.x87_timings.fadd_64);
            return 0;
    }

    // pcem: x87_ops_arith.h:3-120, opFPU(d, x87_td, 32)
    private static int opFCOMd_a32(uint32_t fetchdat)
    {
            x87_td t = default;
            if (FP_ENTER()) return 1;
            if (fetch_ea_32(fetchdat)) return 1;
            if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
            t.i = geteaq();
            if (cpu_state.abrt != 0)
                    return 1;
            // pcem bug, fixed in hardware mode: PB-213 — C1 n'est pas remis à zéro (387 et suivants ; x87.Materiel.cs).
            cpu_state.npxs &= unchecked((uint16_t)~(x87_c.C0 | x87_c.C2 | x87_c.C3));
            cpu_state.npxs |= x87_compare(ST(0), (double)t.d);
            CLOCK_CYCLES(x87_timings_c.x87_timings.fcom_64);
            return 0;
    }

    // pcem: x87_ops_arith.h:3-120, opFPU(d, x87_td, 32)
    private static int opFCOMPd_a32(uint32_t fetchdat)
    {
            x87_td t = default;
            if (FP_ENTER()) return 1;
            if (fetch_ea_32(fetchdat)) return 1;
            if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
            t.i = geteaq();
            if (cpu_state.abrt != 0)
                    return 1;
            // pcem bug, fixed in hardware mode: PB-213 — C1 n'est pas remis à zéro (387 et suivants ; x87.Materiel.cs).
            cpu_state.npxs &= unchecked((uint16_t)~(x87_c.C0 | x87_c.C2 | x87_c.C3));
            cpu_state.npxs |= x87_compare(ST(0), (double)t.d);
            x87_pop();
            CLOCK_CYCLES(x87_timings_c.x87_timings.fcom_64);
            return 0;
    }

    // pcem: x87_ops_arith.h:3-120, opFPU(d, x87_td, 32)
    private static int opFDIVd_a32(uint32_t fetchdat)
    {
            x87_td t = default;
            if (FP_ENTER()) return 1;
            if (fetch_ea_32(fetchdat)) return 1;
            if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
            t.i = geteaq();
            if (cpu_state.abrt != 0)
                    return 1;
            if (x87_div(ref ST(0), ST(0), t.d)) return 1;
            cpu_state.tag[cpu_state.TOP & 7] = x87_c.TAG_VALID;
            CLOCK_CYCLES(x87_timings_c.x87_timings.fdiv_64);
            return 0;
    }

    // pcem: x87_ops_arith.h:3-120, opFPU(d, x87_td, 32)
    private static int opFDIVRd_a32(uint32_t fetchdat)
    {
            x87_td t = default;
            if (FP_ENTER()) return 1;
            if (fetch_ea_32(fetchdat)) return 1;
            if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
            t.i = geteaq();
            if (cpu_state.abrt != 0)
                    return 1;
            if (x87_div(ref ST(0), t.d, ST(0))) return 1;
            cpu_state.tag[cpu_state.TOP & 7] = x87_c.TAG_VALID;
            CLOCK_CYCLES(x87_timings_c.x87_timings.fdiv_64);
            return 0;
    }

    // pcem: x87_ops_arith.h:3-120, opFPU(d, x87_td, 32)
    private static int opFMULd_a32(uint32_t fetchdat)
    {
            x87_td t = default;
            if (FP_ENTER()) return 1;
            if (fetch_ea_32(fetchdat)) return 1;
            if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
            t.i = geteaq();
            if (cpu_state.abrt != 0)
                    return 1;
            // pcem bug, reproduced: PB-60 — le NaN qui survit suit l'ordre des opérandes que GCC a choisi.
            ST(0) = X87MulSd(t.d, ST(0)); // PB-60 : la mémoire en premier
            cpu_state.tag[cpu_state.TOP & 7] = x87_c.TAG_VALID;
            CLOCK_CYCLES(x87_timings_c.x87_timings.fmul_64);
            return 0;
    }

    // pcem: x87_ops_arith.h:3-120, opFPU(d, x87_td, 32)
    private static int opFSUBd_a32(uint32_t fetchdat)
    {
            x87_td t = default;
            if (FP_ENTER()) return 1;
            if (fetch_ea_32(fetchdat)) return 1;
            if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
            t.i = geteaq();
            if (cpu_state.abrt != 0)
                    return 1;
            ST(0) -= t.d;
            cpu_state.tag[cpu_state.TOP & 7] = x87_c.TAG_VALID;
            CLOCK_CYCLES(x87_timings_c.x87_timings.fadd_64);
            return 0;
    }

    // pcem: x87_ops_arith.h:3-120, opFPU(d, x87_td, 32)
    private static int opFSUBRd_a32(uint32_t fetchdat)
    {
            x87_td t = default;
            if (FP_ENTER()) return 1;
            if (fetch_ea_32(fetchdat)) return 1;
            if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
            t.i = geteaq();
            if (cpu_state.abrt != 0)
                    return 1;
            ST(0) = t.d - ST(0);
            cpu_state.tag[cpu_state.TOP & 7] = x87_c.TAG_VALID;
            CLOCK_CYCLES(x87_timings_c.x87_timings.fadd_64);
            return 0;
    }

    // pcem: x87_ops_arith.h:3-120, opFPU(iw, uint16_t, 16)
    private static int opFADDiw_a16(uint32_t fetchdat)
    {
            uint16_t t;
            if (FP_ENTER()) return 1;
            if (fetch_ea_16(fetchdat)) return 1;
            if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
            t = geteaw();
            if (cpu_state.abrt != 0)
                    return 1;
            // pcem bug, reproduced: PB-48 — RC ne vaut que pour ce FADD mémoire, par x87_fadd_dirige :
            //   fesetround(rounding_modes[RC]) ; ST(0) += use_var ; fesetround(FE_TONEAREST) (x87_ops_arith.h:12-16).
            if (((cpu_state.npxc >> 10) & 3) != 0)
                    ST(0) = x87_fadd_dirige((double)unchecked((int16_t)t), ST(0), (cpu_state.npxc >> 10) & 3);
            else
                    // pcem bug, reproduced: PB-60 — le NaN qui survit suit l'ordre des opérandes que GCC a choisi.
                    ST(0) = X87AddSd((double)unchecked((int16_t)t), ST(0)); // PB-60 : la mémoire en premier
            cpu_state.tag[cpu_state.TOP & 7] = x87_c.TAG_VALID;
            CLOCK_CYCLES(x87_timings_c.x87_timings.fadd_i16);
            return 0;
    }

    // pcem: x87_ops_arith.h:3-120, opFPU(iw, uint16_t, 16)
    private static int opFCOMiw_a16(uint32_t fetchdat)
    {
            uint16_t t;
            if (FP_ENTER()) return 1;
            if (fetch_ea_16(fetchdat)) return 1;
            if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
            t = geteaw();
            if (cpu_state.abrt != 0)
                    return 1;
            // pcem bug, fixed in hardware mode: PB-213 — C1 n'est pas remis à zéro (387 et suivants ; x87.Materiel.cs).
            cpu_state.npxs &= unchecked((uint16_t)~(x87_c.C0 | x87_c.C2 | x87_c.C3));
            cpu_state.npxs |= x87_compare(ST(0), (double)unchecked((int16_t)t));
            CLOCK_CYCLES(x87_timings_c.x87_timings.fcom_i16);
            return 0;
    }

    // pcem: x87_ops_arith.h:3-120, opFPU(iw, uint16_t, 16)
    private static int opFCOMPiw_a16(uint32_t fetchdat)
    {
            uint16_t t;
            if (FP_ENTER()) return 1;
            if (fetch_ea_16(fetchdat)) return 1;
            if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
            t = geteaw();
            if (cpu_state.abrt != 0)
                    return 1;
            // pcem bug, fixed in hardware mode: PB-213 — C1 n'est pas remis à zéro (387 et suivants ; x87.Materiel.cs).
            cpu_state.npxs &= unchecked((uint16_t)~(x87_c.C0 | x87_c.C2 | x87_c.C3));
            cpu_state.npxs |= x87_compare(ST(0), (double)unchecked((int16_t)t));
            x87_pop();
            CLOCK_CYCLES(x87_timings_c.x87_timings.fcom_i16);
            return 0;
    }

    // pcem: x87_ops_arith.h:3-120, opFPU(iw, uint16_t, 16)
    private static int opFDIViw_a16(uint32_t fetchdat)
    {
            uint16_t t;
            if (FP_ENTER()) return 1;
            if (fetch_ea_16(fetchdat)) return 1;
            if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
            t = geteaw();
            if (cpu_state.abrt != 0)
                    return 1;
            if (x87_div(ref ST(0), ST(0), (double)unchecked((int16_t)t))) return 1;
            cpu_state.tag[cpu_state.TOP & 7] = x87_c.TAG_VALID;
            CLOCK_CYCLES(x87_timings_c.x87_timings.fdiv_i16);
            return 0;
    }

    // pcem: x87_ops_arith.h:3-120, opFPU(iw, uint16_t, 16)
    private static int opFDIVRiw_a16(uint32_t fetchdat)
    {
            uint16_t t;
            if (FP_ENTER()) return 1;
            if (fetch_ea_16(fetchdat)) return 1;
            if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
            t = geteaw();
            if (cpu_state.abrt != 0)
                    return 1;
            if (x87_div(ref ST(0), (double)unchecked((int16_t)t), ST(0))) return 1;
            cpu_state.tag[cpu_state.TOP & 7] = x87_c.TAG_VALID;
            CLOCK_CYCLES(x87_timings_c.x87_timings.fdiv_i16);
            return 0;
    }

    // pcem: x87_ops_arith.h:3-120, opFPU(iw, uint16_t, 16)
    private static int opFMULiw_a16(uint32_t fetchdat)
    {
            uint16_t t;
            if (FP_ENTER()) return 1;
            if (fetch_ea_16(fetchdat)) return 1;
            if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
            t = geteaw();
            if (cpu_state.abrt != 0)
                    return 1;
            // pcem bug, reproduced: PB-60 — le NaN qui survit suit l'ordre des opérandes que GCC a choisi.
            ST(0) = X87MulSd((double)unchecked((int16_t)t), ST(0)); // PB-60 : la mémoire en premier
            cpu_state.tag[cpu_state.TOP & 7] = x87_c.TAG_VALID;
            CLOCK_CYCLES(x87_timings_c.x87_timings.fmul_i16);
            return 0;
    }

    // pcem: x87_ops_arith.h:3-120, opFPU(iw, uint16_t, 16)
    private static int opFSUBiw_a16(uint32_t fetchdat)
    {
            uint16_t t;
            if (FP_ENTER()) return 1;
            if (fetch_ea_16(fetchdat)) return 1;
            if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
            t = geteaw();
            if (cpu_state.abrt != 0)
                    return 1;
            ST(0) -= (double)unchecked((int16_t)t);
            cpu_state.tag[cpu_state.TOP & 7] = x87_c.TAG_VALID;
            CLOCK_CYCLES(x87_timings_c.x87_timings.fadd_i16);
            return 0;
    }

    // pcem: x87_ops_arith.h:3-120, opFPU(iw, uint16_t, 16)
    private static int opFSUBRiw_a16(uint32_t fetchdat)
    {
            uint16_t t;
            if (FP_ENTER()) return 1;
            if (fetch_ea_16(fetchdat)) return 1;
            if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
            t = geteaw();
            if (cpu_state.abrt != 0)
                    return 1;
            ST(0) = (double)unchecked((int16_t)t) - ST(0);
            cpu_state.tag[cpu_state.TOP & 7] = x87_c.TAG_VALID;
            CLOCK_CYCLES(x87_timings_c.x87_timings.fadd_i16);
            return 0;
    }

    // pcem: x87_ops_arith.h:3-120, opFPU(iw, uint16_t, 32)
    private static int opFADDiw_a32(uint32_t fetchdat)
    {
            uint16_t t;
            if (FP_ENTER()) return 1;
            if (fetch_ea_32(fetchdat)) return 1;
            if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
            t = geteaw();
            if (cpu_state.abrt != 0)
                    return 1;
            // pcem bug, reproduced: PB-48 — RC ne vaut que pour ce FADD mémoire, par x87_fadd_dirige :
            //   fesetround(rounding_modes[RC]) ; ST(0) += use_var ; fesetround(FE_TONEAREST) (x87_ops_arith.h:12-16).
            if (((cpu_state.npxc >> 10) & 3) != 0)
                    ST(0) = x87_fadd_dirige((double)unchecked((int16_t)t), ST(0), (cpu_state.npxc >> 10) & 3);
            else
                    // pcem bug, reproduced: PB-60 — le NaN qui survit suit l'ordre des opérandes que GCC a choisi.
                    ST(0) = X87AddSd((double)unchecked((int16_t)t), ST(0)); // PB-60 : la mémoire en premier
            cpu_state.tag[cpu_state.TOP & 7] = x87_c.TAG_VALID;
            CLOCK_CYCLES(x87_timings_c.x87_timings.fadd_i16);
            return 0;
    }

    // pcem: x87_ops_arith.h:3-120, opFPU(iw, uint16_t, 32)
    private static int opFCOMiw_a32(uint32_t fetchdat)
    {
            uint16_t t;
            if (FP_ENTER()) return 1;
            if (fetch_ea_32(fetchdat)) return 1;
            if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
            t = geteaw();
            if (cpu_state.abrt != 0)
                    return 1;
            // pcem bug, fixed in hardware mode: PB-213 — C1 n'est pas remis à zéro (387 et suivants ; x87.Materiel.cs).
            cpu_state.npxs &= unchecked((uint16_t)~(x87_c.C0 | x87_c.C2 | x87_c.C3));
            cpu_state.npxs |= x87_compare(ST(0), (double)unchecked((int16_t)t));
            CLOCK_CYCLES(x87_timings_c.x87_timings.fcom_i16);
            return 0;
    }

    // pcem: x87_ops_arith.h:3-120, opFPU(iw, uint16_t, 32)
    private static int opFCOMPiw_a32(uint32_t fetchdat)
    {
            uint16_t t;
            if (FP_ENTER()) return 1;
            if (fetch_ea_32(fetchdat)) return 1;
            if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
            t = geteaw();
            if (cpu_state.abrt != 0)
                    return 1;
            // pcem bug, fixed in hardware mode: PB-213 — C1 n'est pas remis à zéro (387 et suivants ; x87.Materiel.cs).
            cpu_state.npxs &= unchecked((uint16_t)~(x87_c.C0 | x87_c.C2 | x87_c.C3));
            cpu_state.npxs |= x87_compare(ST(0), (double)unchecked((int16_t)t));
            x87_pop();
            CLOCK_CYCLES(x87_timings_c.x87_timings.fcom_i16);
            return 0;
    }

    // pcem: x87_ops_arith.h:3-120, opFPU(iw, uint16_t, 32)
    private static int opFDIViw_a32(uint32_t fetchdat)
    {
            uint16_t t;
            if (FP_ENTER()) return 1;
            if (fetch_ea_32(fetchdat)) return 1;
            if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
            t = geteaw();
            if (cpu_state.abrt != 0)
                    return 1;
            if (x87_div(ref ST(0), ST(0), (double)unchecked((int16_t)t))) return 1;
            cpu_state.tag[cpu_state.TOP & 7] = x87_c.TAG_VALID;
            CLOCK_CYCLES(x87_timings_c.x87_timings.fdiv_i16);
            return 0;
    }

    // pcem: x87_ops_arith.h:3-120, opFPU(iw, uint16_t, 32)
    private static int opFDIVRiw_a32(uint32_t fetchdat)
    {
            uint16_t t;
            if (FP_ENTER()) return 1;
            if (fetch_ea_32(fetchdat)) return 1;
            if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
            t = geteaw();
            if (cpu_state.abrt != 0)
                    return 1;
            if (x87_div(ref ST(0), (double)unchecked((int16_t)t), ST(0))) return 1;
            cpu_state.tag[cpu_state.TOP & 7] = x87_c.TAG_VALID;
            CLOCK_CYCLES(x87_timings_c.x87_timings.fdiv_i16);
            return 0;
    }

    // pcem: x87_ops_arith.h:3-120, opFPU(iw, uint16_t, 32)
    private static int opFMULiw_a32(uint32_t fetchdat)
    {
            uint16_t t;
            if (FP_ENTER()) return 1;
            if (fetch_ea_32(fetchdat)) return 1;
            if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
            t = geteaw();
            if (cpu_state.abrt != 0)
                    return 1;
            // pcem bug, reproduced: PB-60 — le NaN qui survit suit l'ordre des opérandes que GCC a choisi.
            ST(0) = X87MulSd((double)unchecked((int16_t)t), ST(0)); // PB-60 : la mémoire en premier
            cpu_state.tag[cpu_state.TOP & 7] = x87_c.TAG_VALID;
            CLOCK_CYCLES(x87_timings_c.x87_timings.fmul_i16);
            return 0;
    }

    // pcem: x87_ops_arith.h:3-120, opFPU(iw, uint16_t, 32)
    private static int opFSUBiw_a32(uint32_t fetchdat)
    {
            uint16_t t;
            if (FP_ENTER()) return 1;
            if (fetch_ea_32(fetchdat)) return 1;
            if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
            t = geteaw();
            if (cpu_state.abrt != 0)
                    return 1;
            ST(0) -= (double)unchecked((int16_t)t);
            cpu_state.tag[cpu_state.TOP & 7] = x87_c.TAG_VALID;
            CLOCK_CYCLES(x87_timings_c.x87_timings.fadd_i16);
            return 0;
    }

    // pcem: x87_ops_arith.h:3-120, opFPU(iw, uint16_t, 32)
    private static int opFSUBRiw_a32(uint32_t fetchdat)
    {
            uint16_t t;
            if (FP_ENTER()) return 1;
            if (fetch_ea_32(fetchdat)) return 1;
            if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
            t = geteaw();
            if (cpu_state.abrt != 0)
                    return 1;
            ST(0) = (double)unchecked((int16_t)t) - ST(0);
            cpu_state.tag[cpu_state.TOP & 7] = x87_c.TAG_VALID;
            CLOCK_CYCLES(x87_timings_c.x87_timings.fadd_i16);
            return 0;
    }

    // pcem: x87_ops_arith.h:3-120, opFPU(il, uint32_t, 16)
    private static int opFADDil_a16(uint32_t fetchdat)
    {
            uint32_t t;
            if (FP_ENTER()) return 1;
            if (fetch_ea_16(fetchdat)) return 1;
            if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
            t = geteal();
            if (cpu_state.abrt != 0)
                    return 1;
            // pcem bug, reproduced: PB-48 — RC ne vaut que pour ce FADD mémoire, par x87_fadd_dirige :
            //   fesetround(rounding_modes[RC]) ; ST(0) += use_var ; fesetround(FE_TONEAREST) (x87_ops_arith.h:12-16).
            if (((cpu_state.npxc >> 10) & 3) != 0)
                    ST(0) = x87_fadd_dirige((double)unchecked((int32_t)t), ST(0), (cpu_state.npxc >> 10) & 3);
            else
                    // pcem bug, reproduced: PB-60 — le NaN qui survit suit l'ordre des opérandes que GCC a choisi.
                    ST(0) = X87AddSd((double)unchecked((int32_t)t), ST(0)); // PB-60 : la mémoire en premier
            cpu_state.tag[cpu_state.TOP & 7] = x87_c.TAG_VALID;
            CLOCK_CYCLES(x87_timings_c.x87_timings.fadd_i32);
            return 0;
    }

    // pcem: x87_ops_arith.h:3-120, opFPU(il, uint32_t, 16)
    private static int opFCOMil_a16(uint32_t fetchdat)
    {
            uint32_t t;
            if (FP_ENTER()) return 1;
            if (fetch_ea_16(fetchdat)) return 1;
            if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
            t = geteal();
            if (cpu_state.abrt != 0)
                    return 1;
            // pcem bug, fixed in hardware mode: PB-213 — C1 n'est pas remis à zéro (387 et suivants ; x87.Materiel.cs).
            cpu_state.npxs &= unchecked((uint16_t)~(x87_c.C0 | x87_c.C2 | x87_c.C3));
            cpu_state.npxs |= x87_compare(ST(0), (double)unchecked((int32_t)t));
            CLOCK_CYCLES(x87_timings_c.x87_timings.fcom_i32);
            return 0;
    }

    // pcem: x87_ops_arith.h:3-120, opFPU(il, uint32_t, 16)
    private static int opFCOMPil_a16(uint32_t fetchdat)
    {
            uint32_t t;
            if (FP_ENTER()) return 1;
            if (fetch_ea_16(fetchdat)) return 1;
            if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
            t = geteal();
            if (cpu_state.abrt != 0)
                    return 1;
            // pcem bug, fixed in hardware mode: PB-213 — C1 n'est pas remis à zéro (387 et suivants ; x87.Materiel.cs).
            cpu_state.npxs &= unchecked((uint16_t)~(x87_c.C0 | x87_c.C2 | x87_c.C3));
            cpu_state.npxs |= x87_compare(ST(0), (double)unchecked((int32_t)t));
            x87_pop();
            CLOCK_CYCLES(x87_timings_c.x87_timings.fcom_i32);
            return 0;
    }

    // pcem: x87_ops_arith.h:3-120, opFPU(il, uint32_t, 16)
    private static int opFDIVil_a16(uint32_t fetchdat)
    {
            uint32_t t;
            if (FP_ENTER()) return 1;
            if (fetch_ea_16(fetchdat)) return 1;
            if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
            t = geteal();
            if (cpu_state.abrt != 0)
                    return 1;
            if (x87_div(ref ST(0), ST(0), (double)unchecked((int32_t)t))) return 1;
            cpu_state.tag[cpu_state.TOP & 7] = x87_c.TAG_VALID;
            CLOCK_CYCLES(x87_timings_c.x87_timings.fdiv_i32);
            return 0;
    }

    // pcem: x87_ops_arith.h:3-120, opFPU(il, uint32_t, 16)
    private static int opFDIVRil_a16(uint32_t fetchdat)
    {
            uint32_t t;
            if (FP_ENTER()) return 1;
            if (fetch_ea_16(fetchdat)) return 1;
            if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
            t = geteal();
            if (cpu_state.abrt != 0)
                    return 1;
            if (x87_div(ref ST(0), (double)unchecked((int32_t)t), ST(0))) return 1;
            cpu_state.tag[cpu_state.TOP & 7] = x87_c.TAG_VALID;
            CLOCK_CYCLES(x87_timings_c.x87_timings.fdiv_i32);
            return 0;
    }

    // pcem: x87_ops_arith.h:3-120, opFPU(il, uint32_t, 16)
    private static int opFMULil_a16(uint32_t fetchdat)
    {
            uint32_t t;
            if (FP_ENTER()) return 1;
            if (fetch_ea_16(fetchdat)) return 1;
            if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
            t = geteal();
            if (cpu_state.abrt != 0)
                    return 1;
            // pcem bug, reproduced: PB-60 — le NaN qui survit suit l'ordre des opérandes que GCC a choisi.
            ST(0) = X87MulSd((double)unchecked((int32_t)t), ST(0)); // PB-60 : la mémoire en premier
            cpu_state.tag[cpu_state.TOP & 7] = x87_c.TAG_VALID;
            CLOCK_CYCLES(x87_timings_c.x87_timings.fmul_i32);
            return 0;
    }

    // pcem: x87_ops_arith.h:3-120, opFPU(il, uint32_t, 16)
    private static int opFSUBil_a16(uint32_t fetchdat)
    {
            uint32_t t;
            if (FP_ENTER()) return 1;
            if (fetch_ea_16(fetchdat)) return 1;
            if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
            t = geteal();
            if (cpu_state.abrt != 0)
                    return 1;
            ST(0) -= (double)unchecked((int32_t)t);
            cpu_state.tag[cpu_state.TOP & 7] = x87_c.TAG_VALID;
            CLOCK_CYCLES(x87_timings_c.x87_timings.fadd_i32);
            return 0;
    }

    // pcem: x87_ops_arith.h:3-120, opFPU(il, uint32_t, 16)
    private static int opFSUBRil_a16(uint32_t fetchdat)
    {
            uint32_t t;
            if (FP_ENTER()) return 1;
            if (fetch_ea_16(fetchdat)) return 1;
            if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
            t = geteal();
            if (cpu_state.abrt != 0)
                    return 1;
            ST(0) = (double)unchecked((int32_t)t) - ST(0);
            cpu_state.tag[cpu_state.TOP & 7] = x87_c.TAG_VALID;
            CLOCK_CYCLES(x87_timings_c.x87_timings.fadd_i32);
            return 0;
    }

    // pcem: x87_ops_arith.h:3-120, opFPU(il, uint32_t, 32)
    private static int opFADDil_a32(uint32_t fetchdat)
    {
            uint32_t t;
            if (FP_ENTER()) return 1;
            if (fetch_ea_32(fetchdat)) return 1;
            if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
            t = geteal();
            if (cpu_state.abrt != 0)
                    return 1;
            // pcem bug, reproduced: PB-48 — RC ne vaut que pour ce FADD mémoire, par x87_fadd_dirige :
            //   fesetround(rounding_modes[RC]) ; ST(0) += use_var ; fesetround(FE_TONEAREST) (x87_ops_arith.h:12-16).
            if (((cpu_state.npxc >> 10) & 3) != 0)
                    ST(0) = x87_fadd_dirige((double)unchecked((int32_t)t), ST(0), (cpu_state.npxc >> 10) & 3);
            else
                    // pcem bug, reproduced: PB-60 — le NaN qui survit suit l'ordre des opérandes que GCC a choisi.
                    ST(0) = X87AddSd((double)unchecked((int32_t)t), ST(0)); // PB-60 : la mémoire en premier
            cpu_state.tag[cpu_state.TOP & 7] = x87_c.TAG_VALID;
            CLOCK_CYCLES(x87_timings_c.x87_timings.fadd_i32);
            return 0;
    }

    // pcem: x87_ops_arith.h:3-120, opFPU(il, uint32_t, 32)
    private static int opFCOMil_a32(uint32_t fetchdat)
    {
            uint32_t t;
            if (FP_ENTER()) return 1;
            if (fetch_ea_32(fetchdat)) return 1;
            if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
            t = geteal();
            if (cpu_state.abrt != 0)
                    return 1;
            // pcem bug, fixed in hardware mode: PB-213 — C1 n'est pas remis à zéro (387 et suivants ; x87.Materiel.cs).
            cpu_state.npxs &= unchecked((uint16_t)~(x87_c.C0 | x87_c.C2 | x87_c.C3));
            cpu_state.npxs |= x87_compare(ST(0), (double)unchecked((int32_t)t));
            CLOCK_CYCLES(x87_timings_c.x87_timings.fcom_i32);
            return 0;
    }

    // pcem: x87_ops_arith.h:3-120, opFPU(il, uint32_t, 32)
    private static int opFCOMPil_a32(uint32_t fetchdat)
    {
            uint32_t t;
            if (FP_ENTER()) return 1;
            if (fetch_ea_32(fetchdat)) return 1;
            if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
            t = geteal();
            if (cpu_state.abrt != 0)
                    return 1;
            // pcem bug, fixed in hardware mode: PB-213 — C1 n'est pas remis à zéro (387 et suivants ; x87.Materiel.cs).
            cpu_state.npxs &= unchecked((uint16_t)~(x87_c.C0 | x87_c.C2 | x87_c.C3));
            cpu_state.npxs |= x87_compare(ST(0), (double)unchecked((int32_t)t));
            x87_pop();
            CLOCK_CYCLES(x87_timings_c.x87_timings.fcom_i32);
            return 0;
    }

    // pcem: x87_ops_arith.h:3-120, opFPU(il, uint32_t, 32)
    private static int opFDIVil_a32(uint32_t fetchdat)
    {
            uint32_t t;
            if (FP_ENTER()) return 1;
            if (fetch_ea_32(fetchdat)) return 1;
            if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
            t = geteal();
            if (cpu_state.abrt != 0)
                    return 1;
            if (x87_div(ref ST(0), ST(0), (double)unchecked((int32_t)t))) return 1;
            cpu_state.tag[cpu_state.TOP & 7] = x87_c.TAG_VALID;
            CLOCK_CYCLES(x87_timings_c.x87_timings.fdiv_i32);
            return 0;
    }

    // pcem: x87_ops_arith.h:3-120, opFPU(il, uint32_t, 32)
    private static int opFDIVRil_a32(uint32_t fetchdat)
    {
            uint32_t t;
            if (FP_ENTER()) return 1;
            if (fetch_ea_32(fetchdat)) return 1;
            if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
            t = geteal();
            if (cpu_state.abrt != 0)
                    return 1;
            if (x87_div(ref ST(0), (double)unchecked((int32_t)t), ST(0))) return 1;
            cpu_state.tag[cpu_state.TOP & 7] = x87_c.TAG_VALID;
            CLOCK_CYCLES(x87_timings_c.x87_timings.fdiv_i32);
            return 0;
    }

    // pcem: x87_ops_arith.h:3-120, opFPU(il, uint32_t, 32)
    private static int opFMULil_a32(uint32_t fetchdat)
    {
            uint32_t t;
            if (FP_ENTER()) return 1;
            if (fetch_ea_32(fetchdat)) return 1;
            if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
            t = geteal();
            if (cpu_state.abrt != 0)
                    return 1;
            // pcem bug, reproduced: PB-60 — le NaN qui survit suit l'ordre des opérandes que GCC a choisi.
            ST(0) = X87MulSd((double)unchecked((int32_t)t), ST(0)); // PB-60 : la mémoire en premier
            cpu_state.tag[cpu_state.TOP & 7] = x87_c.TAG_VALID;
            CLOCK_CYCLES(x87_timings_c.x87_timings.fmul_i32);
            return 0;
    }

    // pcem: x87_ops_arith.h:3-120, opFPU(il, uint32_t, 32)
    private static int opFSUBil_a32(uint32_t fetchdat)
    {
            uint32_t t;
            if (FP_ENTER()) return 1;
            if (fetch_ea_32(fetchdat)) return 1;
            if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
            t = geteal();
            if (cpu_state.abrt != 0)
                    return 1;
            ST(0) -= (double)unchecked((int32_t)t);
            cpu_state.tag[cpu_state.TOP & 7] = x87_c.TAG_VALID;
            CLOCK_CYCLES(x87_timings_c.x87_timings.fadd_i32);
            return 0;
    }

    // pcem: x87_ops_arith.h:3-120, opFPU(il, uint32_t, 32)
    private static int opFSUBRil_a32(uint32_t fetchdat)
    {
            uint32_t t;
            if (FP_ENTER()) return 1;
            if (fetch_ea_32(fetchdat)) return 1;
            if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
            t = geteal();
            if (cpu_state.abrt != 0)
                    return 1;
            ST(0) = (double)unchecked((int32_t)t) - ST(0);
            cpu_state.tag[cpu_state.TOP & 7] = x87_c.TAG_VALID;
            CLOCK_CYCLES(x87_timings_c.x87_timings.fadd_i32);
            return 0;
    }

    // pcem: x87_ops_arith.h:122
    private static int opFADD(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            cpu_state.pc++;
            // pcem bug, reproduced: PB-60 — le NaN qui survit suit l'ordre des opérandes que GCC a choisi.
            ST(0) = X87AddSd(ST((int)(fetchdat & 7)), ST(0)); // PB-60 : ST(i) en premier
            cpu_state.tag[cpu_state.TOP & 7] = x87_c.TAG_VALID;
            CLOCK_CYCLES(x87_timings_c.x87_timings.fadd);
            return 0;
    }

    // pcem: x87_ops_arith.h:132
    private static int opFADDr(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            cpu_state.pc++;
            // pcem bug, reproduced: PB-60 — le NaN qui survit suit l'ordre des opérandes que GCC a choisi.
            ST((int)(fetchdat & 7)) = X87AddSd(ST(0), ST((int)(fetchdat & 7))); // PB-60 : ST(0) en premier
            cpu_state.tag[(cpu_state.TOP + (int)fetchdat) & 7] = x87_c.TAG_VALID;
            CLOCK_CYCLES(x87_timings_c.x87_timings.fadd);
            return 0;
    }

    // pcem: x87_ops_arith.h:142
    private static int opFADDP(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            cpu_state.pc++;
            // pcem bug, reproduced: PB-60 — le NaN qui survit suit l'ordre des opérandes que GCC a choisi.
            ST((int)(fetchdat & 7)) = X87AddSd(ST((int)(fetchdat & 7)), ST(0)); // PB-60 : ST(i) en premier
            cpu_state.tag[(cpu_state.TOP + (int)fetchdat) & 7] = x87_c.TAG_VALID;
            x87_pop();
            CLOCK_CYCLES(x87_timings_c.x87_timings.fadd);
            return 0;
    }

    // pcem: x87_ops_arith.h:154
    private static int opFCOM(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            cpu_state.pc++;
            // pcem bug, fixed in hardware mode: PB-213 — C1 n'est pas remis à zéro (387 et suivants ; x87.Materiel.cs).
            cpu_state.npxs &= unchecked((uint16_t)~(x87_c.C0 | x87_c.C2 | x87_c.C3));
            // pcem bug, fixed in hardware mode: PB-57 — `==` et `<` du C, pas x87_compare : un NaN rend
            //   « plus grand » (C3 = C2 = C0 = 0) au lieu de « non ordonné » ; en mode matériel, la table du
            //   mode porte le gestionnaire corrigé (x87.Materiel.cs).
            if (ST(0) == ST((int)(fetchdat & 7)))
                    cpu_state.npxs |= x87_c.C3;
            else if (ST(0) < ST((int)(fetchdat & 7)))
                    cpu_state.npxs |= x87_c.C0;
            CLOCK_CYCLES(x87_timings_c.x87_timings.fadd);
            return 0;
    }

    // pcem: x87_ops_arith.h:168
    private static int opFCOMP(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            cpu_state.pc++;
            // pcem bug, fixed in hardware mode: PB-213 — C1 n'est pas remis à zéro (387 et suivants ; x87.Materiel.cs).
            cpu_state.npxs &= unchecked((uint16_t)~(x87_c.C0 | x87_c.C2 | x87_c.C3));
            cpu_state.npxs |= x87_compare(ST(0), ST((int)(fetchdat & 7)));
            x87_pop();
            CLOCK_CYCLES(x87_timings_c.x87_timings.fadd);
            return 0;
    }

    // pcem: x87_ops_arith.h:180
    private static int opFCOMPP(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            cpu_state.pc++;
            // pcem bug, fixed in hardware mode: PB-213 — C1 n'est pas remis à zéro (387 et suivants ; x87.Materiel.cs).
            cpu_state.npxs &= unchecked((uint16_t)~(x87_c.C0 | x87_c.C2 | x87_c.C3));
            // pcem bug, fixed in hardware mode: PB-58 — −0 contre +0 rend C0 (« plus petit »), pas C3 ; en mode
            //   matériel, la table du mode porte le gestionnaire corrigé (x87.Materiel.cs).
            if (BitConverter.DoubleToUInt64Bits(ST(0)) == ((uint64_t)1 << 63) && BitConverter.DoubleToUInt64Bits(ST(1)) == 0)
                    cpu_state.npxs |= x87_c.C0; /*Nasty hack to fix 80387 detection*/
            else
                    cpu_state.npxs |= x87_compare(ST(0), ST(1));

            x87_pop();
            x87_pop();
            CLOCK_CYCLES(x87_timings_c.x87_timings.fadd);
            return 0;
    }

    // pcem: x87_ops_arith.h:196
    private static int opFUCOMPP(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            cpu_state.pc++;
            // pcem bug, fixed in hardware mode: PB-213 — C1 n'est pas remis à zéro (387 et suivants ; x87.Materiel.cs).
            cpu_state.npxs &= unchecked((uint16_t)~(x87_c.C0 | x87_c.C2 | x87_c.C3));
            cpu_state.npxs |= x87_ucompare(ST(0), ST(1));
            x87_pop();
            x87_pop();
            CLOCK_CYCLES(x87_timings_c.x87_timings.fucom);
            return 0;
    }

    // pcem: x87_ops_arith.h:239
    private static int opFDIV(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            cpu_state.pc++;
            if (x87_div(ref ST(0), ST(0), ST((int)(fetchdat & 7)))) return 1;
            cpu_state.tag[cpu_state.TOP & 7] = x87_c.TAG_VALID;
            CLOCK_CYCLES(x87_timings_c.x87_timings.fdiv);
            return 0;
    }

    // pcem: x87_ops_arith.h:249
    private static int opFDIVr(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            cpu_state.pc++;
            if (x87_div(ref ST((int)(fetchdat & 7)), ST((int)(fetchdat & 7)), ST(0))) return 1;
            cpu_state.tag[(cpu_state.TOP + (int)fetchdat) & 7] = x87_c.TAG_VALID;
            CLOCK_CYCLES(x87_timings_c.x87_timings.fdiv);
            return 0;
    }

    // pcem: x87_ops_arith.h:259
    private static int opFDIVP(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            cpu_state.pc++;
            if (x87_div(ref ST((int)(fetchdat & 7)), ST((int)(fetchdat & 7)), ST(0))) return 1;
            cpu_state.tag[(cpu_state.TOP + (int)fetchdat) & 7] = x87_c.TAG_VALID;
            x87_pop();
            CLOCK_CYCLES(x87_timings_c.x87_timings.fdiv);
            return 0;
    }

    // pcem: x87_ops_arith.h:271
    private static int opFDIVR(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            cpu_state.pc++;
            if (x87_div(ref ST(0), ST((int)(fetchdat & 7)), ST(0))) return 1;
            cpu_state.tag[cpu_state.TOP & 7] = x87_c.TAG_VALID;
            CLOCK_CYCLES(x87_timings_c.x87_timings.fdiv);
            return 0;
    }

    // pcem: x87_ops_arith.h:281
    private static int opFDIVRr(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            cpu_state.pc++;
            if (x87_div(ref ST((int)(fetchdat & 7)), ST(0), ST((int)(fetchdat & 7)))) return 1;
            cpu_state.tag[(cpu_state.TOP + (int)fetchdat) & 7] = x87_c.TAG_VALID;
            CLOCK_CYCLES(x87_timings_c.x87_timings.fdiv);
            return 0;
    }

    // pcem: x87_ops_arith.h:291
    private static int opFDIVRP(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            cpu_state.pc++;
            if (x87_div(ref ST((int)(fetchdat & 7)), ST(0), ST((int)(fetchdat & 7)))) return 1;
            cpu_state.tag[(cpu_state.TOP + (int)fetchdat) & 7] = x87_c.TAG_VALID;
            x87_pop();
            CLOCK_CYCLES(x87_timings_c.x87_timings.fdiv);
            return 0;
    }

    // pcem: x87_ops_arith.h:303
    private static int opFMUL(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            cpu_state.pc++;
            // pcem bug, reproduced: PB-60 — le NaN qui survit suit l'ordre des opérandes que GCC a choisi.
            ST(0) = X87MulSd(ST((int)(fetchdat & 7)), ST(0)); // PB-60 : ST(i) en premier
            cpu_state.tag[cpu_state.TOP & 7] = x87_c.TAG_VALID;
            CLOCK_CYCLES(x87_timings_c.x87_timings.fmul);
            return 0;
    }

    // pcem: x87_ops_arith.h:313
    private static int opFMULr(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            cpu_state.pc++;
            // pcem bug, reproduced: PB-60 — le NaN qui survit suit l'ordre des opérandes que GCC a choisi.
            ST((int)(fetchdat & 7)) = X87MulSd(ST(0), ST((int)(fetchdat & 7))); // PB-60 : ST(0) en premier
            cpu_state.tag[(cpu_state.TOP + (int)fetchdat) & 7] = x87_c.TAG_VALID;
            CLOCK_CYCLES(x87_timings_c.x87_timings.fmul);
            return 0;
    }

    // pcem: x87_ops_arith.h:323
    private static int opFMULP(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            cpu_state.pc++;
            // pcem bug, reproduced: PB-60 — le NaN qui survit suit l'ordre des opérandes que GCC a choisi.
            ST((int)(fetchdat & 7)) = X87MulSd(ST(0), ST((int)(fetchdat & 7))); // PB-60 : ST(0) en premier
            cpu_state.tag[(cpu_state.TOP + (int)fetchdat) & 7] = x87_c.TAG_VALID;
            x87_pop();
            CLOCK_CYCLES(x87_timings_c.x87_timings.fmul);
            return 0;
    }

    // pcem: x87_ops_arith.h:335
    private static int opFSUB(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            cpu_state.pc++;
            ST(0) = ST(0) - ST((int)(fetchdat & 7));
            cpu_state.tag[cpu_state.TOP & 7] = x87_c.TAG_VALID;
            CLOCK_CYCLES(x87_timings_c.x87_timings.fadd);
            return 0;
    }

    // pcem: x87_ops_arith.h:345
    private static int opFSUBr(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            cpu_state.pc++;
            ST((int)(fetchdat & 7)) = ST((int)(fetchdat & 7)) - ST(0);
            cpu_state.tag[(cpu_state.TOP + (int)fetchdat) & 7] = x87_c.TAG_VALID;
            CLOCK_CYCLES(x87_timings_c.x87_timings.fadd);
            return 0;
    }

    // pcem: x87_ops_arith.h:355
    private static int opFSUBP(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            cpu_state.pc++;
            ST((int)(fetchdat & 7)) = ST((int)(fetchdat & 7)) - ST(0);
            cpu_state.tag[(cpu_state.TOP + (int)fetchdat) & 7] = x87_c.TAG_VALID;
            x87_pop();
            CLOCK_CYCLES(x87_timings_c.x87_timings.fadd);
            return 0;
    }

    // pcem: x87_ops_arith.h:367
    private static int opFSUBR(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            cpu_state.pc++;
            ST(0) = ST((int)(fetchdat & 7)) - ST(0);
            cpu_state.tag[cpu_state.TOP & 7] = x87_c.TAG_VALID;
            CLOCK_CYCLES(x87_timings_c.x87_timings.fadd);
            return 0;
    }

    // pcem: x87_ops_arith.h:377
    private static int opFSUBRr(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            cpu_state.pc++;
            ST((int)(fetchdat & 7)) = ST(0) - ST((int)(fetchdat & 7));
            cpu_state.tag[(cpu_state.TOP + (int)fetchdat) & 7] = x87_c.TAG_VALID;
            CLOCK_CYCLES(x87_timings_c.x87_timings.fadd);
            return 0;
    }

    // pcem: x87_ops_arith.h:387
    private static int opFSUBRP(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            cpu_state.pc++;
            ST((int)(fetchdat & 7)) = ST(0) - ST((int)(fetchdat & 7));
            cpu_state.tag[(cpu_state.TOP + (int)fetchdat) & 7] = x87_c.TAG_VALID;
            x87_pop();
            CLOCK_CYCLES(x87_timings_c.x87_timings.fadd);
            return 0;
    }

    // pcem: x87_ops_arith.h:399
    private static int opFUCOM(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            cpu_state.pc++;
            // pcem bug, fixed in hardware mode: PB-213 — C1 n'est pas remis à zéro (387 et suivants ; x87.Materiel.cs).
            cpu_state.npxs &= unchecked((uint16_t)~(x87_c.C0 | x87_c.C2 | x87_c.C3));
            cpu_state.npxs |= x87_ucompare(ST(0), ST((int)(fetchdat & 7)));
            CLOCK_CYCLES(x87_timings_c.x87_timings.fucom);
            return 0;
    }

    // pcem: x87_ops_arith.h:410
    private static int opFUCOMP(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            cpu_state.pc++;
            // pcem bug, fixed in hardware mode: PB-213 — C1 n'est pas remis à zéro (387 et suivants ; x87.Materiel.cs).
            cpu_state.npxs &= unchecked((uint16_t)~(x87_c.C0 | x87_c.C2 | x87_c.C3));
            cpu_state.npxs |= x87_ucompare(ST(0), ST((int)(fetchdat & 7)));
            x87_pop();
            CLOCK_CYCLES(x87_timings_c.x87_timings.fucom);
            return 0;
    }
}
