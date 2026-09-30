// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/includes/private/cpu/x87_ops_loadstore.h, x87_ops_arith.h, x87_ops_misc.h,
//         recompilés dans l'unité du 808x par 8087.h:86 (`#include "x87_ops.h"`)
// SHA256: voir oracle.tsv ; vérifier avec tools/check-oracle.sh
// STATUS: complete — G4.6 : la SECONDE instanciation des handlers x87, pour le 8087.
//
// GÉNÉRÉ : le corps de x87_ops_loadstore.cs, x87_ops_arith.cs et x87_ops_misc.cs, recopié tel
// quel dans _808x. Chaque nom qui dépend du contexte — FP_ENTER, fetch_ea_*, SEG_CHECK_*,
// CHECK_WRITE, PREFETCH_RUN, readmemw/l/q, writememb/w/l/q, geteaw/l/q, seteaw/l/q, x87_ld80,
// x87_st80 et les aides de FSAVE — se résout d'abord dans _808x (x87_8087.cs, 808x.cs), comme
// le C, où 8087.h les redéfinit avant l'#include ; le reste vient de _386 (`using static`).
// Ne pas éditer à la main : régénérer après toute modification des trois fichiers sources.

using static iXtal26.Cpu._386;
using static iXtal26.Cpu._386_common;
using static iXtal26.Cpu.x86;

namespace iXtal26.Cpu;

internal static partial class _808x
{
    // ======== depuis x87_ops_loadstore.cs ========

    // pcem: x87_ops_loadstore.h:3-18
    private static int opFILDiw_a16(uint32_t fetchdat)
    {
            int16_t temp;
            if (FP_ENTER()) return 1;
            if (fetch_ea_16(fetchdat)) return 1;
            if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
            temp = (int16_t)geteaw();
            if (cpu_state.abrt != 0)
                    return 1;
            x87_push((double)temp);
            CLOCK_CYCLES(x87_timings_c.x87_timings.fild_16);
            return 0;
    }

    // pcem: x87_ops_loadstore.h:19-34
    private static int opFILDiw_a32(uint32_t fetchdat)
    {
            int16_t temp;
            if (FP_ENTER()) return 1;
            if (fetch_ea_32(fetchdat)) return 1;
            if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
            temp = (int16_t)geteaw();
            if (cpu_state.abrt != 0)
                    return 1;
            x87_push((double)temp);
            CLOCK_CYCLES(x87_timings_c.x87_timings.fild_16);
            return 0;
    }

    // pcem: x87_ops_loadstore.h:36-49
    private static int opFISTiw_a16(uint32_t fetchdat)
    {
            int64_t temp64;
            if (FP_ENTER()) return 1;
            if (fetch_ea_16(fetchdat)) return 1;
            if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
            temp64 = x87_fround(ST(0));
            // /*                        if (temp64 > 32767 || temp64 < -32768)
                                       // fatal("FISTw overflow %i\n", temp64);*/
            seteaw(unchecked((uint16_t)(int16_t)temp64));
            CLOCK_CYCLES(x87_timings_c.x87_timings.fist_16);
            return cpu_state.abrt;
    }

    // pcem: x87_ops_loadstore.h:50-63
    private static int opFISTiw_a32(uint32_t fetchdat)
    {
            int64_t temp64;
            if (FP_ENTER()) return 1;
            if (fetch_ea_32(fetchdat)) return 1;
            if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
            temp64 = x87_fround(ST(0));
            // /*                        if (temp64 > 32767 || temp64 < -32768)
                                       // fatal("FISTw overflow %i\n", temp64);*/
            seteaw(unchecked((uint16_t)(int16_t)temp64));
            CLOCK_CYCLES(x87_timings_c.x87_timings.fist_16);
            return cpu_state.abrt;
    }

    // pcem: x87_ops_loadstore.h:65-81
    private static int opFISTPiw_a16(uint32_t fetchdat)
    {
            int64_t temp64;
            if (FP_ENTER()) return 1;
            if (fetch_ea_16(fetchdat)) return 1;
            if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
            temp64 = x87_fround(ST(0));
            // /*                        if (temp64 > 32767 || temp64 < -32768)
                                       // fatal("FISTw overflow %i\n", temp64);*/
            seteaw(unchecked((uint16_t)(int16_t)temp64));
            if (cpu_state.abrt != 0)
                    return 1;
            x87_pop();
            CLOCK_CYCLES(x87_timings_c.x87_timings.fist_16);
            return 0;
    }

    // pcem: x87_ops_loadstore.h:82-98
    private static int opFISTPiw_a32(uint32_t fetchdat)
    {
            int64_t temp64;
            if (FP_ENTER()) return 1;
            if (fetch_ea_32(fetchdat)) return 1;
            if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
            temp64 = x87_fround(ST(0));
            // /*                        if (temp64 > 32767 || temp64 < -32768)
                                       // fatal("FISTw overflow %i\n", temp64);*/
            seteaw(unchecked((uint16_t)(int16_t)temp64));
            if (cpu_state.abrt != 0)
                    return 1;
            x87_pop();
            CLOCK_CYCLES(x87_timings_c.x87_timings.fist_16);
            return 0;
    }

    // pcem: x87_ops_loadstore.h:100-119
    private static int opFILDiq_a16(uint32_t fetchdat)
    {
            int64_t temp64;
            if (FP_ENTER()) return 1;
            if (fetch_ea_16(fetchdat)) return 1;
            if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
            temp64 = (int64_t)geteaq();
            if (cpu_state.abrt != 0)
                    return 1;
            x87_push((double)temp64);
            cpu_state.MM[cpu_state.TOP & 7].q = (uint64_t)temp64;
            cpu_state.tag[cpu_state.TOP & 7] = x87_c.TAG_VALID | x87_c.TAG_UINT64;

            CLOCK_CYCLES(x87_timings_c.x87_timings.fild_64);
            return 0;
    }

    // pcem: x87_ops_loadstore.h:120-139
    private static int opFILDiq_a32(uint32_t fetchdat)
    {
            int64_t temp64;
            if (FP_ENTER()) return 1;
            if (fetch_ea_32(fetchdat)) return 1;
            if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
            temp64 = (int64_t)geteaq();
            if (cpu_state.abrt != 0)
                    return 1;
            x87_push((double)temp64);
            cpu_state.MM[cpu_state.TOP & 7].q = (uint64_t)temp64;
            cpu_state.tag[cpu_state.TOP & 7] = x87_c.TAG_VALID | x87_c.TAG_UINT64;

            CLOCK_CYCLES(x87_timings_c.x87_timings.fild_64);
            return 0;
    }

    // pcem: x87_ops_loadstore.h:141-170
    private static int FBSTP_a16(uint32_t fetchdat)
    {
            double tempd;
            int c;
            if (FP_ENTER()) return 1;
            if (fetch_ea_16(fetchdat)) return 1;
            if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
            tempd = ST(0);
            if (tempd < 0.0)
                    tempd = -tempd;
            for (c = 0; c < 9; c++) {
                    uint8_t tempc = (uint8_t)CvtI32(Math.Floor(tempd % 10.0));
                    tempd -= Math.Floor(tempd % 10.0);
                    tempd /= 10.0;
                    tempc |= (uint8_t)(((uint8_t)CvtI32(Math.Floor(tempd % 10.0))) << 4);
                    tempd -= Math.Floor(tempd % 10.0);
                    tempd /= 10.0;
                    writememb(easeg, cpu_state.eaaddr + (uint32_t)c, tempc);
            }
            // pcem bug, reproduced: PB-53 — le `tempc` de la boucle est hors de portée ici : ce
            //   `tempc` est la GLOBALE des drapeaux (x86_flags.h:3), qu'ADC et SBB lisent.
            x86_flags.tempc = (uint8_t)CvtI32(Math.Floor(tempd % 10.0));
            if (ST(0) < 0.0)
                    x86_flags.tempc |= 0x80;
            writememb(easeg, cpu_state.eaaddr + 9, (uint8_t)x86_flags.tempc);
            if (cpu_state.abrt != 0)
                    return 1;
            x87_pop();
            CLOCK_CYCLES(x87_timings_c.x87_timings.fbstp);
            return 0;
    }

    // pcem: x87_ops_loadstore.h:171-200
    private static int FBSTP_a32(uint32_t fetchdat)
    {
            double tempd;
            int c;
            if (FP_ENTER()) return 1;
            if (fetch_ea_32(fetchdat)) return 1;
            if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
            tempd = ST(0);
            if (tempd < 0.0)
                    tempd = -tempd;
            for (c = 0; c < 9; c++) {
                    uint8_t tempc = (uint8_t)CvtI32(Math.Floor(tempd % 10.0));
                    tempd -= Math.Floor(tempd % 10.0);
                    tempd /= 10.0;
                    tempc |= (uint8_t)(((uint8_t)CvtI32(Math.Floor(tempd % 10.0))) << 4);
                    tempd -= Math.Floor(tempd % 10.0);
                    tempd /= 10.0;
                    writememb(easeg, cpu_state.eaaddr + (uint32_t)c, tempc);
            }
            // pcem bug, reproduced: PB-53 — le `tempc` de la boucle est hors de portée ici : ce
            //   `tempc` est la GLOBALE des drapeaux (x86_flags.h:3), qu'ADC et SBB lisent.
            x86_flags.tempc = (uint8_t)CvtI32(Math.Floor(tempd % 10.0));
            if (ST(0) < 0.0)
                    x86_flags.tempc |= 0x80;
            writememb(easeg, cpu_state.eaaddr + 9, (uint8_t)x86_flags.tempc);
            if (cpu_state.abrt != 0)
                    return 1;
            x87_pop();
            CLOCK_CYCLES(x87_timings_c.x87_timings.fbstp);
            return 0;
    }

    // pcem: x87_ops_loadstore.h:202-219
    private static int FISTPiq_a16(uint32_t fetchdat)
    {
            int64_t temp64;
            if (FP_ENTER()) return 1;
            if (fetch_ea_16(fetchdat)) return 1;
            if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
            if ((cpu_state.tag[cpu_state.TOP & 7] & x87_c.TAG_UINT64) != 0)
                    temp64 = (int64_t)cpu_state.MM[cpu_state.TOP & 7].q;
            else
                    temp64 = x87_fround(ST(0));
            seteaq((uint64_t)temp64);
            if (cpu_state.abrt != 0)
                    return 1;
            x87_pop();
            CLOCK_CYCLES(x87_timings_c.x87_timings.fist_64);
            return 0;
    }

    // pcem: x87_ops_loadstore.h:220-237
    private static int FISTPiq_a32(uint32_t fetchdat)
    {
            int64_t temp64;
            if (FP_ENTER()) return 1;
            if (fetch_ea_32(fetchdat)) return 1;
            if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
            if ((cpu_state.tag[cpu_state.TOP & 7] & x87_c.TAG_UINT64) != 0)
                    temp64 = (int64_t)cpu_state.MM[cpu_state.TOP & 7].q;
            else
                    temp64 = x87_fround(ST(0));
            seteaq((uint64_t)temp64);
            if (cpu_state.abrt != 0)
                    return 1;
            x87_pop();
            CLOCK_CYCLES(x87_timings_c.x87_timings.fist_64);
            return 0;
    }

    // pcem: x87_ops_loadstore.h:239-254
    private static int opFILDil_a16(uint32_t fetchdat)
    {
            int32_t templ;
            if (FP_ENTER()) return 1;
            if (fetch_ea_16(fetchdat)) return 1;
            if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
            templ = (int32_t)geteal();
            if (cpu_state.abrt != 0)
                    return 1;
            x87_push((double)templ);
            CLOCK_CYCLES(x87_timings_c.x87_timings.fild_32);
            return 0;
    }

    // pcem: x87_ops_loadstore.h:255-270
    private static int opFILDil_a32(uint32_t fetchdat)
    {
            int32_t templ;
            if (FP_ENTER()) return 1;
            if (fetch_ea_32(fetchdat)) return 1;
            if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
            templ = (int32_t)geteal();
            if (cpu_state.abrt != 0)
                    return 1;
            x87_push((double)templ);
            CLOCK_CYCLES(x87_timings_c.x87_timings.fild_32);
            return 0;
    }

    // pcem: x87_ops_loadstore.h:272-285
    private static int opFISTil_a16(uint32_t fetchdat)
    {
            int64_t temp64;
            if (FP_ENTER()) return 1;
            if (fetch_ea_16(fetchdat)) return 1;
            if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
            temp64 = x87_fround(ST(0));
            // /*                        if (temp64 > 2147483647 || temp64 < -2147483647)
                                       // fatal("FISTl out of range! %i\n", temp64);*/
            seteal(unchecked((uint32_t)(int32_t)temp64));
            CLOCK_CYCLES(x87_timings_c.x87_timings.fist_32);
            return cpu_state.abrt;
    }

    // pcem: x87_ops_loadstore.h:286-299
    private static int opFISTil_a32(uint32_t fetchdat)
    {
            int64_t temp64;
            if (FP_ENTER()) return 1;
            if (fetch_ea_32(fetchdat)) return 1;
            if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
            temp64 = x87_fround(ST(0));
            // /*                        if (temp64 > 2147483647 || temp64 < -2147483647)
                                       // fatal("FISTl out of range! %i\n", temp64);*/
            seteal(unchecked((uint32_t)(int32_t)temp64));
            CLOCK_CYCLES(x87_timings_c.x87_timings.fist_32);
            return cpu_state.abrt;
    }

    // pcem: x87_ops_loadstore.h:301-317
    private static int opFISTPil_a16(uint32_t fetchdat)
    {
            int64_t temp64;
            if (FP_ENTER()) return 1;
            if (fetch_ea_16(fetchdat)) return 1;
            if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
            temp64 = x87_fround(ST(0));
            // /*                        if (temp64 > 2147483647 || temp64 < -2147483647)
                                       // fatal("FISTl out of range! %i\n", temp64);*/
            seteal(unchecked((uint32_t)(int32_t)temp64));
            if (cpu_state.abrt != 0)
                    return 1;
            x87_pop();
            CLOCK_CYCLES(x87_timings_c.x87_timings.fist_32);
            return 0;
    }

    // pcem: x87_ops_loadstore.h:318-334
    private static int opFISTPil_a32(uint32_t fetchdat)
    {
            int64_t temp64;
            if (FP_ENTER()) return 1;
            if (fetch_ea_32(fetchdat)) return 1;
            if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
            temp64 = x87_fround(ST(0));
            // /*                        if (temp64 > 2147483647 || temp64 < -2147483647)
                                       // fatal("FISTl out of range! %i\n", temp64);*/
            seteal(unchecked((uint32_t)(int32_t)temp64));
            if (cpu_state.abrt != 0)
                    return 1;
            x87_pop();
            CLOCK_CYCLES(x87_timings_c.x87_timings.fist_32);
            return 0;
    }

    // pcem: x87_ops_loadstore.h:336-351
    private static int opFLDe_a16(uint32_t fetchdat)
    {
            double t;
            if (FP_ENTER()) return 1;
            if (fetch_ea_16(fetchdat)) return 1;
            if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
            t = x87_ld80();
            if (cpu_state.abrt != 0)
                    return 1;
            x87_push(t);
            CLOCK_CYCLES(x87_timings_c.x87_timings.fld_80);
            return 0;
    }

    // pcem: x87_ops_loadstore.h:352-367
    private static int opFLDe_a32(uint32_t fetchdat)
    {
            double t;
            if (FP_ENTER()) return 1;
            if (fetch_ea_32(fetchdat)) return 1;
            if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
            t = x87_ld80();
            if (cpu_state.abrt != 0)
                    return 1;
            x87_push(t);
            CLOCK_CYCLES(x87_timings_c.x87_timings.fld_80);
            return 0;
    }

    // pcem: x87_ops_loadstore.h:369-381
    private static int opFSTPe_a16(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            if (fetch_ea_16(fetchdat)) return 1;
            if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
            x87_st80(ST(0));
            if (cpu_state.abrt != 0)
                    return 1;
            x87_pop();
            CLOCK_CYCLES(x87_timings_c.x87_timings.fst_80);
            return 0;
    }

    // pcem: x87_ops_loadstore.h:382-394
    private static int opFSTPe_a32(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            if (fetch_ea_32(fetchdat)) return 1;
            if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
            x87_st80(ST(0));
            if (cpu_state.abrt != 0)
                    return 1;
            x87_pop();
            CLOCK_CYCLES(x87_timings_c.x87_timings.fst_80);
            return 0;
    }

    // pcem: x87_ops_loadstore.h:396-411
    private static int opFLDd_a16(uint32_t fetchdat)
    {
            x87_td t = default;
            if (FP_ENTER()) return 1;
            if (fetch_ea_16(fetchdat)) return 1;
            if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
            t.i = geteaq();
            if (cpu_state.abrt != 0)
                    return 1;
            x87_push(t.d);
            CLOCK_CYCLES(x87_timings_c.x87_timings.fld_64);
            return 0;
    }

    // pcem: x87_ops_loadstore.h:412-427
    private static int opFLDd_a32(uint32_t fetchdat)
    {
            x87_td t = default;
            if (FP_ENTER()) return 1;
            if (fetch_ea_32(fetchdat)) return 1;
            if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
            t.i = geteaq();
            if (cpu_state.abrt != 0)
                    return 1;
            x87_push(t.d);
            CLOCK_CYCLES(x87_timings_c.x87_timings.fld_64);
            return 0;
    }

    // pcem: x87_ops_loadstore.h:429-440
    private static int opFSTd_a16(uint32_t fetchdat)
    {
            x87_td t = default;
            if (FP_ENTER()) return 1;
            if (fetch_ea_16(fetchdat)) return 1;
            if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
            t.d = ST(0);
            seteaq(t.i);
            CLOCK_CYCLES(x87_timings_c.x87_timings.fst_64);
            return cpu_state.abrt;
    }

    // pcem: x87_ops_loadstore.h:441-452
    private static int opFSTd_a32(uint32_t fetchdat)
    {
            x87_td t = default;
            if (FP_ENTER()) return 1;
            if (fetch_ea_32(fetchdat)) return 1;
            if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
            t.d = ST(0);
            seteaq(t.i);
            CLOCK_CYCLES(x87_timings_c.x87_timings.fst_64);
            return cpu_state.abrt;
    }

    // pcem: x87_ops_loadstore.h:454-469
    private static int opFSTPd_a16(uint32_t fetchdat)
    {
            x87_td t = default;
            if (FP_ENTER()) return 1;
            if (fetch_ea_16(fetchdat)) return 1;
            if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
            if (CHECK_WRITE(cpu_state.ea_seg!, cpu_state.eaaddr, cpu_state.eaaddr + 7)) return 1;
            t.d = ST(0);
            seteaq(t.i);
            if (cpu_state.abrt != 0)
                    return 1;
            x87_pop();
            CLOCK_CYCLES(x87_timings_c.x87_timings.fst_64);
            return 0;
    }

    // pcem: x87_ops_loadstore.h:470-485
    private static int opFSTPd_a32(uint32_t fetchdat)
    {
            x87_td t = default;
            if (FP_ENTER()) return 1;
            if (fetch_ea_32(fetchdat)) return 1;
            if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
            if (CHECK_WRITE(cpu_state.ea_seg!, cpu_state.eaaddr, cpu_state.eaaddr + 7)) return 1;
            t.d = ST(0);
            seteaq(t.i);
            if (cpu_state.abrt != 0)
                    return 1;
            x87_pop();
            CLOCK_CYCLES(x87_timings_c.x87_timings.fst_64);
            return 0;
    }

    // pcem: x87_ops_loadstore.h:487-502
    private static int opFLDs_a16(uint32_t fetchdat)
    {
            x87_ts ts = default;
            if (FP_ENTER()) return 1;
            if (fetch_ea_16(fetchdat)) return 1;
            if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
            ts.i = geteal();
            if (cpu_state.abrt != 0)
                    return 1;
            x87_push((double)ts.s);
            CLOCK_CYCLES(x87_timings_c.x87_timings.fld_32);
            return 0;
    }

    // pcem: x87_ops_loadstore.h:503-518
    private static int opFLDs_a32(uint32_t fetchdat)
    {
            x87_ts ts = default;
            if (FP_ENTER()) return 1;
            if (fetch_ea_32(fetchdat)) return 1;
            if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
            ts.i = geteal();
            if (cpu_state.abrt != 0)
                    return 1;
            x87_push((double)ts.s);
            CLOCK_CYCLES(x87_timings_c.x87_timings.fld_32);
            return 0;
    }

    // pcem: x87_ops_loadstore.h:520-531
    private static int opFSTs_a16(uint32_t fetchdat)
    {
            x87_ts ts = default;
            if (FP_ENTER()) return 1;
            if (fetch_ea_16(fetchdat)) return 1;
            if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
            ts.s = (float)ST(0);
            seteal(ts.i);
            CLOCK_CYCLES(x87_timings_c.x87_timings.fst_32);
            return cpu_state.abrt;
    }

    // pcem: x87_ops_loadstore.h:532-543
    private static int opFSTs_a32(uint32_t fetchdat)
    {
            x87_ts ts = default;
            if (FP_ENTER()) return 1;
            if (fetch_ea_32(fetchdat)) return 1;
            if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
            ts.s = (float)ST(0);
            seteal(ts.i);
            CLOCK_CYCLES(x87_timings_c.x87_timings.fst_32);
            return cpu_state.abrt;
    }

    // pcem: x87_ops_loadstore.h:545-559
    private static int opFSTPs_a16(uint32_t fetchdat)
    {
            x87_ts ts = default;
            if (FP_ENTER()) return 1;
            if (fetch_ea_16(fetchdat)) return 1;
            if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
            ts.s = (float)ST(0);
            seteal(ts.i);
            if (cpu_state.abrt != 0)
                    return 1;
            x87_pop();
            CLOCK_CYCLES(x87_timings_c.x87_timings.fst_32);
            return 0;
    }

    // pcem: x87_ops_loadstore.h:560-574
    private static int opFSTPs_a32(uint32_t fetchdat)
    {
            x87_ts ts = default;
            if (FP_ENTER()) return 1;
            if (fetch_ea_32(fetchdat)) return 1;
            if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
            ts.s = (float)ST(0);
            seteal(ts.i);
            if (cpu_state.abrt != 0)
                    return 1;
            x87_pop();
            CLOCK_CYCLES(x87_timings_c.x87_timings.fst_32);
            return 0;
    }

    // ======== depuis x87_ops_arith.cs ========

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
            // pcem: fesetround(rounding_modes[RC]) ; ST(0) += use_var ; fesetround(FE_TONEAREST)
            //   (x87_ops_arith.h:12-16) — l'arrondi dirigé, pour ce seul FADD mémoire (PB-48).
            if (((cpu_state.npxc >> 10) & 3) != 0)
                    ST(0) = x87_fadd_dirige(t.s, ST(0), (cpu_state.npxc >> 10) & 3);
            else
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
            // pcem: fesetround(rounding_modes[RC]) ; ST(0) += use_var ; fesetround(FE_TONEAREST)
            //   (x87_ops_arith.h:12-16) — l'arrondi dirigé, pour ce seul FADD mémoire (PB-48).
            if (((cpu_state.npxc >> 10) & 3) != 0)
                    ST(0) = x87_fadd_dirige(t.s, ST(0), (cpu_state.npxc >> 10) & 3);
            else
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
            // pcem: fesetround(rounding_modes[RC]) ; ST(0) += use_var ; fesetround(FE_TONEAREST)
            //   (x87_ops_arith.h:12-16) — l'arrondi dirigé, pour ce seul FADD mémoire (PB-48).
            if (((cpu_state.npxc >> 10) & 3) != 0)
                    ST(0) = x87_fadd_dirige(t.d, ST(0), (cpu_state.npxc >> 10) & 3);
            else
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
            // pcem: fesetround(rounding_modes[RC]) ; ST(0) += use_var ; fesetround(FE_TONEAREST)
            //   (x87_ops_arith.h:12-16) — l'arrondi dirigé, pour ce seul FADD mémoire (PB-48).
            if (((cpu_state.npxc >> 10) & 3) != 0)
                    ST(0) = x87_fadd_dirige(t.d, ST(0), (cpu_state.npxc >> 10) & 3);
            else
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
            // pcem: fesetround(rounding_modes[RC]) ; ST(0) += use_var ; fesetround(FE_TONEAREST)
            //   (x87_ops_arith.h:12-16) — l'arrondi dirigé, pour ce seul FADD mémoire (PB-48).
            if (((cpu_state.npxc >> 10) & 3) != 0)
                    ST(0) = x87_fadd_dirige((double)unchecked((int16_t)t), ST(0), (cpu_state.npxc >> 10) & 3);
            else
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
            // pcem: fesetround(rounding_modes[RC]) ; ST(0) += use_var ; fesetround(FE_TONEAREST)
            //   (x87_ops_arith.h:12-16) — l'arrondi dirigé, pour ce seul FADD mémoire (PB-48).
            if (((cpu_state.npxc >> 10) & 3) != 0)
                    ST(0) = x87_fadd_dirige((double)unchecked((int16_t)t), ST(0), (cpu_state.npxc >> 10) & 3);
            else
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
            // pcem: fesetround(rounding_modes[RC]) ; ST(0) += use_var ; fesetround(FE_TONEAREST)
            //   (x87_ops_arith.h:12-16) — l'arrondi dirigé, pour ce seul FADD mémoire (PB-48).
            if (((cpu_state.npxc >> 10) & 3) != 0)
                    ST(0) = x87_fadd_dirige((double)unchecked((int32_t)t), ST(0), (cpu_state.npxc >> 10) & 3);
            else
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
            // pcem: fesetround(rounding_modes[RC]) ; ST(0) += use_var ; fesetround(FE_TONEAREST)
            //   (x87_ops_arith.h:12-16) — l'arrondi dirigé, pour ce seul FADD mémoire (PB-48).
            if (((cpu_state.npxc >> 10) & 3) != 0)
                    ST(0) = x87_fadd_dirige((double)unchecked((int32_t)t), ST(0), (cpu_state.npxc >> 10) & 3);
            else
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
            cpu_state.npxs &= unchecked((uint16_t)~(x87_c.C0 | x87_c.C2 | x87_c.C3));
            // pcem bug, reproduced: PB-57 — `==` et `<` du C, pas x87_compare : un NaN rend « plus
            //   grand » (C3 = C2 = C0 = 0) au lieu de « non ordonné ».
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
            cpu_state.npxs &= unchecked((uint16_t)~(x87_c.C0 | x87_c.C2 | x87_c.C3));
            // pcem bug, reproduced: PB-58 — −0 contre +0 rend C0 (« plus petit »), pas C3.
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
            cpu_state.npxs &= unchecked((uint16_t)~(x87_c.C0 | x87_c.C2 | x87_c.C3));
            cpu_state.npxs |= x87_ucompare(ST(0), ST((int)(fetchdat & 7)));
            x87_pop();
            CLOCK_CYCLES(x87_timings_c.x87_timings.fucom);
            return 0;
    }

    // ======== depuis x87_ops_misc.cs ========

    // pcem: x87_ops_misc.h:3-12
    private static int opFDISI(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            cpu_state.pc++;
            if (cpu_c.fpu_type == cpu_c.FPU_8087) {
                    cpu_state.npxc |= FPCW_DISI;
                    CLOCK_CYCLES(x87_timings_c.x87_timings.fdisi_eni);
            } else
                    CLOCK_CYCLES(x87_timings_c.x87_timings.fnop);
            return 0;
    }

    // pcem: x87_ops_misc.h:13-22
    private static int opFENI(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            cpu_state.pc++;
            if (cpu_c.fpu_type == cpu_c.FPU_8087) {
                    cpu_state.npxc &= unchecked((uint16_t)~FPCW_DISI);
                    CLOCK_CYCLES(x87_timings_c.x87_timings.fdisi_eni);
            } else
                    CLOCK_CYCLES(x87_timings_c.x87_timings.fnop);
            return 0;
    }

    // pcem: x87_ops_misc.h:24-32
    private static int opFSTSW_AX(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            cpu_state.pc++;
            // pcem bug, reproduced: PB-61 — npxs BRUT : sans les trois bits de TOP que la forme
            //   mémoire (opFSTSW_a16) y compose.
            AX = cpu_state.npxs;
            CLOCK_CYCLES(x87_timings_c.x87_timings.fstcw_sw);
            return 0;
    }

    // pcem: x87_ops_misc.h:34-39
    private static int opFNOP(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            cpu_state.pc++;
            CLOCK_CYCLES(x87_timings_c.x87_timings.fnop);
            return 0;
    }

    // pcem: x87_ops_misc.h:41-47
    private static int opFCLEX(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            cpu_state.pc++;
            cpu_state.npxs &= 0xff00;
            CLOCK_CYCLES(x87_timings_c.x87_timings.fclex);
            return 0;
    }

    // pcem: x87_ops_misc.h:49-64
    private static int opFINIT(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            cpu_state.pc++;
            if (cpu_c.fpu_type == cpu_c.FPU_8087)
                    cpu_state.npxc = 0x3ff;
            else
                    cpu_state.npxc = 0x37f;
            // omitted: codegen_set_rounding_mode(...) — le dynarec (souche vide dans l'oracle).
            cpu_state.npxs = 0;
            Array.Clear(cpu_state.tag);
            cpu_state.TOP = 0;
            cpu_state.ismmx = 0;
            CLOCK_CYCLES(x87_timings_c.x87_timings.finit);
            CPU_BLOCK_END();
            return 0;
    }

    // pcem: x87_ops_misc.h:66-74
    private static int opFFREE(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            cpu_state.pc++;
            cpu_state.tag[(cpu_state.TOP + (int)fetchdat) & 7] = x87_c.TAG_EMPTY;
            CLOCK_CYCLES(x87_timings_c.x87_timings.ffree);
            return 0;
    }

    // pcem: x87_ops_misc.h:76-85
    private static int opFST(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            cpu_state.pc++;
            ST((int)(fetchdat & 7)) = ST(0);
            // pcem bug, reproduced: PB-67 — le tag est copié, TAG_UINT64 compris, mais pas MM[].q.
            cpu_state.tag[(cpu_state.TOP + (int)fetchdat) & 7] = cpu_state.tag[cpu_state.TOP & 7];
            CLOCK_CYCLES(x87_timings_c.x87_timings.fst);
            return 0;
    }

    // pcem: x87_ops_misc.h:87-97
    private static int opFSTP(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            cpu_state.pc++;
            ST((int)(fetchdat & 7)) = ST(0);
            // pcem bug, reproduced: PB-67 — le tag est copié, TAG_UINT64 compris, mais pas MM[].q.
            cpu_state.tag[(cpu_state.TOP + (int)fetchdat) & 7] = cpu_state.tag[cpu_state.TOP & 7];
            x87_pop();
            CLOCK_CYCLES(x87_timings_c.x87_timings.fst);
            return 0;
    }

    // pcem: x87_ops_misc.h:99-150
    private static int FSTOR()
    {
            if (FP_ENTER()) return 1;
            switch ((cr0 & 1) | (cpu_state.op32 & 0x100)) {
            case 0x000: /*16-bit real mode*/
            case 0x001: /*16-bit protected mode*/
                    cpu_state.npxc = readmemw(easeg, cpu_state.eaaddr);
                    // omitted: codegen_set_rounding_mode(...) — le dynarec (souche vide dans l'oracle).
                    cpu_state.npxs = readmemw(easeg, cpu_state.eaaddr + 2);
                    x87_c.x87_settag(readmemw(easeg, cpu_state.eaaddr + 4));
                    cpu_state.TOP = (cpu_state.npxs >> 11) & 7;
                    cpu_state.eaaddr += 14;
                    break;
            case 0x100: /*32-bit real mode*/
            case 0x101: /*32-bit protected mode*/
                    cpu_state.npxc = readmemw(easeg, cpu_state.eaaddr);
                    // omitted: codegen_set_rounding_mode(...) — le dynarec (souche vide dans l'oracle).
                    cpu_state.npxs = readmemw(easeg, cpu_state.eaaddr + 4);
                    x87_c.x87_settag(readmemw(easeg, cpu_state.eaaddr + 8));
                    cpu_state.TOP = (cpu_state.npxs >> 11) & 7;
                    cpu_state.eaaddr += 28;
                    break;
            }
            x87_ld_frstor(0);
            cpu_state.eaaddr += 10;
            x87_ld_frstor(1);
            cpu_state.eaaddr += 10;
            x87_ld_frstor(2);
            cpu_state.eaaddr += 10;
            x87_ld_frstor(3);
            cpu_state.eaaddr += 10;
            x87_ld_frstor(4);
            cpu_state.eaaddr += 10;
            x87_ld_frstor(5);
            cpu_state.eaaddr += 10;
            x87_ld_frstor(6);
            cpu_state.eaaddr += 10;
            x87_ld_frstor(7);

            cpu_state.ismmx = 0;
            // Horrible hack, but as PCem doesn't keep the FPU stack in 80-bit precision at all times
            // something like this is needed
            if (cpu_state.MM_w4[0] == 0xffff && cpu_state.MM_w4[1] == 0xffff && cpu_state.MM_w4[2] == 0xffff &&
                cpu_state.MM_w4[3] == 0xffff && cpu_state.MM_w4[4] == 0xffff && cpu_state.MM_w4[5] == 0xffff &&
                cpu_state.MM_w4[6] == 0xffff && cpu_state.MM_w4[7] == 0xffff && cpu_state.TOP == 0 &&
                TagsTousValides())
                    cpu_state.ismmx = 1;

            CLOCK_CYCLES(x87_timings_c.x87_timings.frstor);
            return cpu_state.abrt;
    }

    // pcem: x87_ops_misc.h:151-157
    private static int opFSTOR_a16(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            if (fetch_ea_16(fetchdat)) return 1;
            if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
            _ = FSTOR();
            return cpu_state.abrt;
    }

    // pcem: x87_ops_misc.h:158-164
    private static int opFSTOR_a32(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            if (fetch_ea_32(fetchdat)) return 1;
            if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
            _ = FSTOR();
            return cpu_state.abrt;
    }

    // pcem: x87_ops_misc.h:166-353
    private static int FSAVE()
    {
            if (FP_ENTER()) return 1;
            cpu_state.npxs = (uint16_t)((cpu_state.npxs & ~(7 << 11)) | ((cpu_state.TOP & 7) << 11));

            switch ((cr0 & 1) | (cpu_state.op32 & 0x100)) {
            case 0x000: /*16-bit real mode*/
                    writememw(easeg, cpu_state.eaaddr, cpu_state.npxc);
                    writememw(easeg, cpu_state.eaaddr + 2, cpu_state.npxs);
                    writememw(easeg, cpu_state.eaaddr + 4, x87_c.x87_gettag());
                    writememw(easeg, cpu_state.eaaddr + 6, (uint16_t)x87_c.x87_pc_off);
                    writememw(easeg, cpu_state.eaaddr + 10, (uint16_t)x87_c.x87_op_off);
                    cpu_state.eaaddr += 14;
                    if (cpu_state.ismmx != 0) {
                            x87_stmmx(cpu_state.MM[0]);
                            cpu_state.eaaddr += 10;
                            x87_stmmx(cpu_state.MM[1]);
                            cpu_state.eaaddr += 10;
                            x87_stmmx(cpu_state.MM[2]);
                            cpu_state.eaaddr += 10;
                            x87_stmmx(cpu_state.MM[3]);
                            cpu_state.eaaddr += 10;
                            x87_stmmx(cpu_state.MM[4]);
                            cpu_state.eaaddr += 10;
                            x87_stmmx(cpu_state.MM[5]);
                            cpu_state.eaaddr += 10;
                            x87_stmmx(cpu_state.MM[6]);
                            cpu_state.eaaddr += 10;
                            x87_stmmx(cpu_state.MM[7]);
                    } else {
                            x87_st_fsave(0);
                            cpu_state.eaaddr += 10;
                            x87_st_fsave(1);
                            cpu_state.eaaddr += 10;
                            x87_st_fsave(2);
                            cpu_state.eaaddr += 10;
                            x87_st_fsave(3);
                            cpu_state.eaaddr += 10;
                            x87_st_fsave(4);
                            cpu_state.eaaddr += 10;
                            x87_st_fsave(5);
                            cpu_state.eaaddr += 10;
                            x87_st_fsave(6);
                            cpu_state.eaaddr += 10;
                            x87_st_fsave(7);
                    }
                    break;
            case 0x001: /*16-bit protected mode*/
                    writememw(easeg, cpu_state.eaaddr, cpu_state.npxc);
                    writememw(easeg, cpu_state.eaaddr + 2, cpu_state.npxs);
                    writememw(easeg, cpu_state.eaaddr + 4, x87_c.x87_gettag());
                    writememw(easeg, cpu_state.eaaddr + 6, (uint16_t)x87_c.x87_pc_off);
                    writememw(easeg, cpu_state.eaaddr + 8, (uint16_t)x87_c.x87_pc_seg);
                    writememw(easeg, cpu_state.eaaddr + 10, (uint16_t)x87_c.x87_op_off);
                    writememw(easeg, cpu_state.eaaddr + 12, (uint16_t)x87_c.x87_op_seg);
                    cpu_state.eaaddr += 14;
                    if (cpu_state.ismmx != 0) {
                            x87_stmmx(cpu_state.MM[0]);
                            cpu_state.eaaddr += 10;
                            x87_stmmx(cpu_state.MM[1]);
                            cpu_state.eaaddr += 10;
                            x87_stmmx(cpu_state.MM[2]);
                            cpu_state.eaaddr += 10;
                            x87_stmmx(cpu_state.MM[3]);
                            cpu_state.eaaddr += 10;
                            x87_stmmx(cpu_state.MM[4]);
                            cpu_state.eaaddr += 10;
                            x87_stmmx(cpu_state.MM[5]);
                            cpu_state.eaaddr += 10;
                            x87_stmmx(cpu_state.MM[6]);
                            cpu_state.eaaddr += 10;
                            x87_stmmx(cpu_state.MM[7]);
                    } else {
                            x87_st_fsave(0);
                            cpu_state.eaaddr += 10;
                            x87_st_fsave(1);
                            cpu_state.eaaddr += 10;
                            x87_st_fsave(2);
                            cpu_state.eaaddr += 10;
                            x87_st_fsave(3);
                            cpu_state.eaaddr += 10;
                            x87_st_fsave(4);
                            cpu_state.eaaddr += 10;
                            x87_st_fsave(5);
                            cpu_state.eaaddr += 10;
                            x87_st_fsave(6);
                            cpu_state.eaaddr += 10;
                            x87_st_fsave(7);
                    }
                    break;
            case 0x100: /*32-bit real mode*/
                    writememw(easeg, cpu_state.eaaddr, cpu_state.npxc);
                    writememw(easeg, cpu_state.eaaddr + 4, cpu_state.npxs);
                    writememw(easeg, cpu_state.eaaddr + 8, x87_c.x87_gettag());
                    writememw(easeg, cpu_state.eaaddr + 12, (uint16_t)x87_c.x87_pc_off);
                    writememw(easeg, cpu_state.eaaddr + 20, (uint16_t)x87_c.x87_op_off);
                    writememl(easeg, cpu_state.eaaddr + 24, (x87_c.x87_op_off >> 16) << 12);
                    cpu_state.eaaddr += 28;
                    if (cpu_state.ismmx != 0) {
                            x87_stmmx(cpu_state.MM[0]);
                            cpu_state.eaaddr += 10;
                            x87_stmmx(cpu_state.MM[1]);
                            cpu_state.eaaddr += 10;
                            x87_stmmx(cpu_state.MM[2]);
                            cpu_state.eaaddr += 10;
                            x87_stmmx(cpu_state.MM[3]);
                            cpu_state.eaaddr += 10;
                            x87_stmmx(cpu_state.MM[4]);
                            cpu_state.eaaddr += 10;
                            x87_stmmx(cpu_state.MM[5]);
                            cpu_state.eaaddr += 10;
                            x87_stmmx(cpu_state.MM[6]);
                            cpu_state.eaaddr += 10;
                            x87_stmmx(cpu_state.MM[7]);
                    } else {
                            x87_st_fsave(0);
                            cpu_state.eaaddr += 10;
                            x87_st_fsave(1);
                            cpu_state.eaaddr += 10;
                            x87_st_fsave(2);
                            cpu_state.eaaddr += 10;
                            x87_st_fsave(3);
                            cpu_state.eaaddr += 10;
                            x87_st_fsave(4);
                            cpu_state.eaaddr += 10;
                            x87_st_fsave(5);
                            cpu_state.eaaddr += 10;
                            x87_st_fsave(6);
                            cpu_state.eaaddr += 10;
                            x87_st_fsave(7);
                    }
                    break;
            case 0x101: /*32-bit protected mode*/
                    writememw(easeg, cpu_state.eaaddr, cpu_state.npxc);
                    writememw(easeg, cpu_state.eaaddr + 4, cpu_state.npxs);
                    writememw(easeg, cpu_state.eaaddr + 8, x87_c.x87_gettag());
                    writememl(easeg, cpu_state.eaaddr + 12, x87_c.x87_pc_off);
                    writememl(easeg, cpu_state.eaaddr + 16, (uint32_t)x87_c.x87_pc_seg);
                    writememl(easeg, cpu_state.eaaddr + 20, x87_c.x87_op_off);
                    writememl(easeg, cpu_state.eaaddr + 24, (uint32_t)x87_c.x87_op_seg);
                    cpu_state.eaaddr += 28;
                    if (cpu_state.ismmx != 0) {
                            x87_stmmx(cpu_state.MM[0]);
                            cpu_state.eaaddr += 10;
                            x87_stmmx(cpu_state.MM[1]);
                            cpu_state.eaaddr += 10;
                            x87_stmmx(cpu_state.MM[2]);
                            cpu_state.eaaddr += 10;
                            x87_stmmx(cpu_state.MM[3]);
                            cpu_state.eaaddr += 10;
                            x87_stmmx(cpu_state.MM[4]);
                            cpu_state.eaaddr += 10;
                            x87_stmmx(cpu_state.MM[5]);
                            cpu_state.eaaddr += 10;
                            x87_stmmx(cpu_state.MM[6]);
                            cpu_state.eaaddr += 10;
                            x87_stmmx(cpu_state.MM[7]);
                    } else {
                            x87_st_fsave(0);
                            cpu_state.eaaddr += 10;
                            x87_st_fsave(1);
                            cpu_state.eaaddr += 10;
                            x87_st_fsave(2);
                            cpu_state.eaaddr += 10;
                            x87_st_fsave(3);
                            cpu_state.eaaddr += 10;
                            x87_st_fsave(4);
                            cpu_state.eaaddr += 10;
                            x87_st_fsave(5);
                            cpu_state.eaaddr += 10;
                            x87_st_fsave(6);
                            cpu_state.eaaddr += 10;
                            x87_st_fsave(7);
                    }
                    break;
            }

            cpu_state.npxc = 0x37F;
            // omitted: codegen_set_rounding_mode(...) — le dynarec (souche vide dans l'oracle).
            cpu_state.npxs = 0;
            Array.Clear(cpu_state.tag);
            cpu_state.TOP = 0;
            cpu_state.ismmx = 0;

            CLOCK_CYCLES(x87_timings_c.x87_timings.fsave);
            return cpu_state.abrt;
    }

    // pcem: x87_ops_misc.h:354-360
    private static int opFSAVE_a16(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            if (fetch_ea_16(fetchdat)) return 1;
            if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
            _ = FSAVE();
            return cpu_state.abrt;
    }

    // pcem: x87_ops_misc.h:361-367
    private static int opFSAVE_a32(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            if (fetch_ea_32(fetchdat)) return 1;
            if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
            _ = FSAVE();
            return cpu_state.abrt;
    }

    // pcem: x87_ops_misc.h:369-378
    private static int opFSTSW_a16(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            if (fetch_ea_16(fetchdat)) return 1;
            if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
            seteaw((uint16_t)((cpu_state.npxs & 0xC7FF) | ((cpu_state.TOP & 7) << 11)));
            CLOCK_CYCLES(x87_timings_c.x87_timings.fstcw_sw);
            return cpu_state.abrt;
    }

    // pcem: x87_ops_misc.h:379-388
    private static int opFSTSW_a32(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            if (fetch_ea_32(fetchdat)) return 1;
            if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
            seteaw((uint16_t)((cpu_state.npxs & 0xC7FF) | ((cpu_state.TOP & 7) << 11)));
            CLOCK_CYCLES(x87_timings_c.x87_timings.fstcw_sw);
            return cpu_state.abrt;
    }

    // pcem: x87_ops_misc.h:390-405
    private static int opFLD(uint32_t fetchdat)
    {
            int old_tag;
            uint64_t old_i64;

            if (FP_ENTER()) return 1;
            cpu_state.pc++;
            old_tag = cpu_state.tag[(cpu_state.TOP + (int)fetchdat) & 7];
            old_i64 = cpu_state.MM[(cpu_state.TOP + (int)fetchdat) & 7].q;
            x87_push(ST((int)(fetchdat & 7)));
            cpu_state.tag[cpu_state.TOP & 7] = (uint8_t)old_tag;
            cpu_state.MM[cpu_state.TOP & 7].q = old_i64;
            CLOCK_CYCLES(x87_timings_c.x87_timings.fld);
            return 0;
    }

    // pcem: x87_ops_misc.h:407-427
    private static int opFXCH(uint32_t fetchdat)
    {
            double td;
            uint8_t old_tag;
            uint64_t old_i64;
            if (FP_ENTER()) return 1;
            cpu_state.pc++;
            td = ST(0);
            ST(0) = ST((int)(fetchdat & 7));
            ST((int)(fetchdat & 7)) = td;
            old_tag = cpu_state.tag[cpu_state.TOP & 7];
            cpu_state.tag[cpu_state.TOP & 7] = cpu_state.tag[(cpu_state.TOP + (int)fetchdat) & 7];
            cpu_state.tag[(cpu_state.TOP + (int)fetchdat) & 7] = old_tag;
            old_i64 = cpu_state.MM[cpu_state.TOP & 7].q;
            cpu_state.MM[cpu_state.TOP & 7].q = cpu_state.MM[(cpu_state.TOP + (int)fetchdat) & 7].q;
            cpu_state.MM[(cpu_state.TOP + (int)fetchdat) & 7].q = old_i64;

            CLOCK_CYCLES(x87_timings_c.x87_timings.fxch);
            return 0;
    }

    // pcem: x87_ops_misc.h:429-438
    private static int opFCHS(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            cpu_state.pc++;
            ST(0) = -ST(0);
            cpu_state.tag[cpu_state.TOP & 7] = x87_c.TAG_VALID;
            CLOCK_CYCLES(x87_timings_c.x87_timings.fchs);
            return 0;
    }

    // pcem: x87_ops_misc.h:440-449
    private static int opFABS(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            cpu_state.pc++;
            ST(0) = Math.Abs(ST(0));
            cpu_state.tag[cpu_state.TOP & 7] = x87_c.TAG_VALID;
            CLOCK_CYCLES(x87_timings_c.x87_timings.fabs);
            return 0;
    }

    // pcem: x87_ops_misc.h:451-463
    private static int opFTST(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            cpu_state.pc++;
            cpu_state.npxs &= unchecked((uint16_t)~(x87_c.C0 | x87_c.C2 | x87_c.C3));
            // pcem bug, reproduced: PB-64 — un NaN rend « plus grand », pas « non ordonné ».
            if (ST(0) == 0.0)
                    cpu_state.npxs |= x87_c.C3;
            else if (ST(0) < 0.0)
                    cpu_state.npxs |= x87_c.C0;
            CLOCK_CYCLES(x87_timings_c.x87_timings.ftst);
            return 0;
    }

    // pcem: x87_ops_misc.h:465-481
    private static int opFXAM(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            cpu_state.pc++;
            // pcem bug, reproduced: PB-63 — trois classes seulement : vide, zéro, « normal ».
            cpu_state.npxs &= unchecked((uint16_t)~(x87_c.C0 | x87_c.C1 | x87_c.C2 | x87_c.C3));
            if (cpu_state.tag[cpu_state.TOP & 7] == x87_c.TAG_EMPTY)
                    cpu_state.npxs |= x87_c.C0 | x87_c.C3;
            else if (ST(0) == 0.0)
                    cpu_state.npxs |= x87_c.C3;
            else
                    cpu_state.npxs |= x87_c.C2;
            if (ST(0) < 0.0)
                    cpu_state.npxs |= x87_c.C1;
            CLOCK_CYCLES(x87_timings_c.x87_timings.fxam);
            return 0;
    }

    // pcem: x87_ops_misc.h:483-491
    private static int opFLD1(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            cpu_state.pc++;
            x87_push(1.0);
            CLOCK_CYCLES(x87_timings_c.x87_timings.fld_z1);
            return 0;
    }

    // pcem: x87_ops_misc.h:493-501
    private static int opFLDL2T(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            cpu_state.pc++;
            x87_push(3.3219280948873623);
            CLOCK_CYCLES(x87_timings_c.x87_timings.fld_const);
            return 0;
    }

    // pcem: x87_ops_misc.h:503-511
    private static int opFLDL2E(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            cpu_state.pc++;
            x87_push(1.4426950408889634);
            CLOCK_CYCLES(x87_timings_c.x87_timings.fld_const);
            return 0;
    }

    // pcem: x87_ops_misc.h:513-521
    private static int opFLDPI(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            cpu_state.pc++;
            x87_push(3.141592653589793);
            CLOCK_CYCLES(x87_timings_c.x87_timings.fld_const);
            return 0;
    }

    // pcem: x87_ops_misc.h:523-531
    private static int opFLDEG2(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            cpu_state.pc++;
            x87_push(0.3010299956639812);
            CLOCK_CYCLES(x87_timings_c.x87_timings.fld_const);
            return 0;
    }

    // pcem: x87_ops_misc.h:533-541
    private static int opFLDLN2(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            cpu_state.pc++;
            // pcem bug, reproduced: PB-66 — ln 2 d'un ulp au-dessus du double le plus proche.
            x87_push_u64(0x3fe62e42fefa39f0UL);
            CLOCK_CYCLES(x87_timings_c.x87_timings.fld_const);
            return 0;
    }

    // pcem: x87_ops_misc.h:543-552
    private static int opFLDZ(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            cpu_state.pc++;
            x87_push(0.0);
            cpu_state.tag[cpu_state.TOP & 7] = x87_c.TAG_VALID;
            CLOCK_CYCLES(x87_timings_c.x87_timings.fld_z1);
            return 0;
    }

    // pcem: x87_ops_misc.h:554-563
    private static int opF2XM1(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            cpu_state.pc++;
            ST(0) = Math.Pow(2.0, ST(0)) - 1.0;
            cpu_state.tag[cpu_state.TOP & 7] = x87_c.TAG_VALID;
            CLOCK_CYCLES(x87_timings_c.x87_timings.f2xm1);
            return 0;
    }

    // pcem: x87_ops_misc.h:565-575
    private static int opFYL2X(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            cpu_state.pc++;
            ST(1) = ST(1) * (Math.Log(ST(0)) / Math.Log(2.0));
            cpu_state.tag[(cpu_state.TOP + 1) & 7] = x87_c.TAG_VALID;
            x87_pop();
            CLOCK_CYCLES(x87_timings_c.x87_timings.fyl2x);
            return 0;
    }

    // pcem: x87_ops_misc.h:577-587
    private static int opFYL2XP1(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            cpu_state.pc++;
            ST(1) = ST(1) * (Math.Log(ST(0) + 1.0) / Math.Log(2.0));
            cpu_state.tag[(cpu_state.TOP + 1) & 7] = x87_c.TAG_VALID;
            x87_pop();
            CLOCK_CYCLES(x87_timings_c.x87_timings.fyl2xp1);
            return 0;
    }

    // pcem: x87_ops_misc.h:589-600
    private static int opFPTAN(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            cpu_state.pc++;
            ST(0) = Math.Tan(ST(0));
            cpu_state.tag[cpu_state.TOP & 7] = x87_c.TAG_VALID;
            x87_push(1.0);
            // pcem bug, reproduced: PB-68 — C2 toujours effacé : pas de borne |x| < 2^63, la libm
            //   réduit tout argument, et « réduction incomplète » n'est jamais signalée.
            cpu_state.npxs &= unchecked((uint16_t)~x87_c.C2);
            CLOCK_CYCLES(x87_timings_c.x87_timings.fptan);
            return 0;
    }

    // pcem: x87_ops_misc.h:602-612
    private static int opFPATAN(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            cpu_state.pc++;
            ST(1) = Math.Atan2(ST(1), ST(0));
            cpu_state.tag[(cpu_state.TOP + 1) & 7] = x87_c.TAG_VALID;
            x87_pop();
            CLOCK_CYCLES(x87_timings_c.x87_timings.fpatan);
            return 0;
    }

    // pcem: x87_ops_misc.h:614-622
    private static int opFDECSTP(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            cpu_state.pc++;
            cpu_state.TOP--;
            CLOCK_CYCLES(x87_timings_c.x87_timings.fincdecstp);
            return 0;
    }

    // pcem: x87_ops_misc.h:624-632
    private static int opFINCSTP(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            cpu_state.pc++;
            cpu_state.TOP++;
            CLOCK_CYCLES(x87_timings_c.x87_timings.fincdecstp);
            return 0;
    }

    // pcem: x87_ops_misc.h:634-654
    private static int opFPREM(uint32_t fetchdat)
    {
            int64_t temp64;
            if (FP_ENTER()) return 1;
            cpu_state.pc++;
            // pcem bug, reproduced: PB-65 — un quotient tronqué d'un coup, C2 jamais posé, et
            //   FPREM1 identique à FPREM.
            temp64 = CvtI64(ST(0) / ST(1));
            ST(0) = ST(0) - (ST(1) * (double)temp64);
            cpu_state.tag[cpu_state.TOP & 7] = x87_c.TAG_VALID;
            cpu_state.npxs &= unchecked((uint16_t)~(x87_c.C0 | x87_c.C1 | x87_c.C2 | x87_c.C3));
            if ((temp64 & 4) != 0)
                    cpu_state.npxs |= x87_c.C0;
            if ((temp64 & 2) != 0)
                    cpu_state.npxs |= x87_c.C3;
            if ((temp64 & 1) != 0)
                    cpu_state.npxs |= x87_c.C1;
            CLOCK_CYCLES(x87_timings_c.x87_timings.fprem);
            return 0;
    }

    // pcem: x87_ops_misc.h:655-675
    private static int opFPREM1(uint32_t fetchdat)
    {
            int64_t temp64;
            if (FP_ENTER()) return 1;
            cpu_state.pc++;
            // pcem bug, reproduced: PB-65 — un quotient tronqué d'un coup, C2 jamais posé, et
            //   FPREM1 identique à FPREM.
            temp64 = CvtI64(ST(0) / ST(1));
            ST(0) = ST(0) - (ST(1) * (double)temp64);
            cpu_state.tag[cpu_state.TOP & 7] = x87_c.TAG_VALID;
            cpu_state.npxs &= unchecked((uint16_t)~(x87_c.C0 | x87_c.C1 | x87_c.C2 | x87_c.C3));
            if ((temp64 & 4) != 0)
                    cpu_state.npxs |= x87_c.C0;
            if ((temp64 & 2) != 0)
                    cpu_state.npxs |= x87_c.C3;
            if ((temp64 & 1) != 0)
                    cpu_state.npxs |= x87_c.C1;
            CLOCK_CYCLES(x87_timings_c.x87_timings.fprem1);
            return 0;
    }

    // pcem: x87_ops_misc.h:677-686
    private static int opFSQRT(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            cpu_state.pc++;
            ST(0) = Math.Sqrt(ST(0));
            cpu_state.tag[cpu_state.TOP & 7] = x87_c.TAG_VALID;
            CLOCK_CYCLES(x87_timings_c.x87_timings.fsqrt);
            return 0;
    }

    // pcem: x87_ops_misc.h:688-701
    private static int opFSINCOS(uint32_t fetchdat)
    {
            double td;
            if (FP_ENTER()) return 1;
            cpu_state.pc++;
            td = ST(0);
            ST(0) = Math.Sin(td);
            cpu_state.tag[cpu_state.TOP & 7] = x87_c.TAG_VALID;
            x87_push(Math.Cos(td));
            // pcem bug, reproduced: PB-68 — C2 toujours effacé : pas de borne |x| < 2^63, la libm
            //   réduit tout argument, et « réduction incomplète » n'est jamais signalée.
            cpu_state.npxs &= unchecked((uint16_t)~x87_c.C2);
            CLOCK_CYCLES(x87_timings_c.x87_timings.fsincos);
            return 0;
    }

    // pcem: x87_ops_misc.h:703-714
    private static int opFRNDINT(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            cpu_state.pc++;
            ST(0) = (double)x87_fround(ST(0));
            cpu_state.tag[cpu_state.TOP & 7] = x87_c.TAG_VALID;
            CLOCK_CYCLES(x87_timings_c.x87_timings.frndint);
            return 0;
    }

    // pcem: x87_ops_misc.h:716-728
    private static int opFSCALE(uint32_t fetchdat)
    {
            int64_t temp64;
            if (FP_ENTER()) return 1;
            cpu_state.pc++;
            temp64 = CvtI64(ST(1));
            if (ST(0) != 0.0)
                    ST(0) = ST(0) * Math.Pow(2.0, (double)temp64);
            cpu_state.tag[cpu_state.TOP & 7] = x87_c.TAG_VALID;
            CLOCK_CYCLES(x87_timings_c.x87_timings.fscale);
            return 0;
    }

    // pcem: x87_ops_misc.h:730-740
    private static int opFSIN(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            cpu_state.pc++;
            ST(0) = Math.Sin(ST(0));
            cpu_state.tag[cpu_state.TOP & 7] = x87_c.TAG_VALID;
            // pcem bug, reproduced: PB-68 — C2 toujours effacé : pas de borne |x| < 2^63, la libm
            //   réduit tout argument, et « réduction incomplète » n'est jamais signalée.
            cpu_state.npxs &= unchecked((uint16_t)~x87_c.C2);
            CLOCK_CYCLES(x87_timings_c.x87_timings.fsin_cos);
            return 0;
    }

    // pcem: x87_ops_misc.h:742-752
    private static int opFCOS(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            cpu_state.pc++;
            ST(0) = Math.Cos(ST(0));
            cpu_state.tag[cpu_state.TOP & 7] = x87_c.TAG_VALID;
            // pcem bug, reproduced: PB-68 — C2 toujours effacé : pas de borne |x| < 2^63, la libm
            //   réduit tout argument, et « réduction incomplète » n'est jamais signalée.
            cpu_state.npxs &= unchecked((uint16_t)~x87_c.C2);
            CLOCK_CYCLES(x87_timings_c.x87_timings.fsin_cos);
            return 0;
    }

    // pcem: x87_ops_misc.h:754-778
    private static int FLDENV()
    {
            if (FP_ENTER()) return 1;
            switch ((cr0 & 1) | (cpu_state.op32 & 0x100)) {
            case 0x000: /*16-bit real mode*/
            case 0x001: /*16-bit protected mode*/
                    cpu_state.npxc = readmemw(easeg, cpu_state.eaaddr);
                    // omitted: codegen_set_rounding_mode(...) — le dynarec (souche vide dans l'oracle).
                    cpu_state.npxs = readmemw(easeg, cpu_state.eaaddr + 2);
                    x87_c.x87_settag(readmemw(easeg, cpu_state.eaaddr + 4));
                    cpu_state.TOP = (cpu_state.npxs >> 11) & 7;
                    break;
            case 0x100: /*32-bit real mode*/
            case 0x101: /*32-bit protected mode*/
                    cpu_state.npxc = readmemw(easeg, cpu_state.eaaddr);
                    // omitted: codegen_set_rounding_mode(...) — le dynarec (souche vide dans l'oracle).
                    cpu_state.npxs = readmemw(easeg, cpu_state.eaaddr + 4);
                    x87_c.x87_settag(readmemw(easeg, cpu_state.eaaddr + 8));
                    cpu_state.TOP = (cpu_state.npxs >> 11) & 7;
                    break;
            }
            CLOCK_CYCLES(x87_timings_c.x87_timings.fldenv);
            return cpu_state.abrt;
    }

    // pcem: x87_ops_misc.h:780-786
    private static int opFLDENV_a16(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            if (fetch_ea_16(fetchdat)) return 1;
            if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
            _ = FLDENV();
            return cpu_state.abrt;
    }

    // pcem: x87_ops_misc.h:787-793
    private static int opFLDENV_a32(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            if (fetch_ea_32(fetchdat)) return 1;
            if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
            _ = FLDENV();
            return cpu_state.abrt;
    }

    // pcem: x87_ops_misc.h:795-809
    private static int opFLDCW_a16(uint32_t fetchdat)
    {
            uint16_t tempw;
            if (FP_ENTER()) return 1;
            if (fetch_ea_16(fetchdat)) return 1;
            if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
            tempw = geteaw();
            if (cpu_state.abrt != 0)
                    return 1;
            cpu_state.npxc = tempw;
            // omitted: codegen_set_rounding_mode(...) — le dynarec (souche vide dans l'oracle).
            CLOCK_CYCLES(x87_timings_c.x87_timings.fldcw);
            return 0;
    }

    // pcem: x87_ops_misc.h:810-824
    private static int opFLDCW_a32(uint32_t fetchdat)
    {
            uint16_t tempw;
            if (FP_ENTER()) return 1;
            if (fetch_ea_32(fetchdat)) return 1;
            if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
            tempw = geteaw();
            if (cpu_state.abrt != 0)
                    return 1;
            cpu_state.npxc = tempw;
            // omitted: codegen_set_rounding_mode(...) — le dynarec (souche vide dans l'oracle).
            CLOCK_CYCLES(x87_timings_c.x87_timings.fldcw);
            return 0;
    }

    // pcem: x87_ops_misc.h:826-869
    private static int FSTENV()
    {
            if (FP_ENTER()) return 1;
            cpu_state.npxs = (uint16_t)((cpu_state.npxs & ~(7 << 11)) | ((cpu_state.TOP & 7) << 11));

            switch ((cr0 & 1) | (cpu_state.op32 & 0x100)) {
            case 0x000: /*16-bit real mode*/
                    writememw(easeg, cpu_state.eaaddr, cpu_state.npxc);
                    writememw(easeg, cpu_state.eaaddr + 2, cpu_state.npxs);
                    writememw(easeg, cpu_state.eaaddr + 4, x87_c.x87_gettag());
                    writememw(easeg, cpu_state.eaaddr + 6, (uint16_t)x87_c.x87_pc_off);
                    writememw(easeg, cpu_state.eaaddr + 10, (uint16_t)x87_c.x87_op_off);
                    break;
            case 0x001: /*16-bit protected mode*/
                    writememw(easeg, cpu_state.eaaddr, cpu_state.npxc);
                    writememw(easeg, cpu_state.eaaddr + 2, cpu_state.npxs);
                    writememw(easeg, cpu_state.eaaddr + 4, x87_c.x87_gettag());
                    writememw(easeg, cpu_state.eaaddr + 6, (uint16_t)x87_c.x87_pc_off);
                    writememw(easeg, cpu_state.eaaddr + 8, (uint16_t)x87_c.x87_pc_seg);
                    writememw(easeg, cpu_state.eaaddr + 10, (uint16_t)x87_c.x87_op_off);
                    writememw(easeg, cpu_state.eaaddr + 12, (uint16_t)x87_c.x87_op_seg);
                    break;
            case 0x100: /*32-bit real mode*/
                    writememw(easeg, cpu_state.eaaddr, cpu_state.npxc);
                    writememw(easeg, cpu_state.eaaddr + 4, cpu_state.npxs);
                    writememw(easeg, cpu_state.eaaddr + 8, x87_c.x87_gettag());
                    writememw(easeg, cpu_state.eaaddr + 12, (uint16_t)x87_c.x87_pc_off);
                    writememw(easeg, cpu_state.eaaddr + 20, (uint16_t)x87_c.x87_op_off);
                    writememl(easeg, cpu_state.eaaddr + 24, (x87_c.x87_op_off >> 16) << 12);
                    break;
            case 0x101: /*32-bit protected mode*/
                    writememw(easeg, cpu_state.eaaddr, cpu_state.npxc);
                    writememw(easeg, cpu_state.eaaddr + 4, cpu_state.npxs);
                    writememw(easeg, cpu_state.eaaddr + 8, x87_c.x87_gettag());
                    writememl(easeg, cpu_state.eaaddr + 12, x87_c.x87_pc_off);
                    writememl(easeg, cpu_state.eaaddr + 16, (uint32_t)x87_c.x87_pc_seg);
                    writememl(easeg, cpu_state.eaaddr + 20, x87_c.x87_op_off);
                    writememl(easeg, cpu_state.eaaddr + 24, (uint32_t)x87_c.x87_op_seg);
                    break;
            }
            CLOCK_CYCLES(x87_timings_c.x87_timings.fstenv);
            return cpu_state.abrt;
    }

    // pcem: x87_ops_misc.h:871-877
    private static int opFSTENV_a16(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            if (fetch_ea_16(fetchdat)) return 1;
            if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
            _ = FSTENV();
            return cpu_state.abrt;
    }

    // pcem: x87_ops_misc.h:878-884
    private static int opFSTENV_a32(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            if (fetch_ea_32(fetchdat)) return 1;
            if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
            _ = FSTENV();
            return cpu_state.abrt;
    }

    // pcem: x87_ops_misc.h:886-895
    private static int opFSTCW_a16(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            if (fetch_ea_16(fetchdat)) return 1;
            if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
            seteaw(cpu_state.npxc);
            CLOCK_CYCLES(x87_timings_c.x87_timings.fstcw_sw);
            return cpu_state.abrt;
    }

    // pcem: x87_ops_misc.h:896-905
    private static int opFSTCW_a32(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            if (fetch_ea_32(fetchdat)) return 1;
            if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
            seteaw(cpu_state.npxc);
            CLOCK_CYCLES(x87_timings_c.x87_timings.fstcw_sw);
            return cpu_state.abrt;
    }
}
