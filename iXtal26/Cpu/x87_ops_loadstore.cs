// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/includes/private/cpu/x87_ops_loadstore.h  (lignes 1-576)
// SHA256: voir oracle.tsv ; vérifier avec tools/check-oracle.sh
// STATUS: complete — G4.2 : les trente-quatre handlers de chargement et de stockage du x87,
//         formes a16 et a32. Les `if (fplog) pclog(...)` sont omis (sortie pure).
//
// TRANSCRIT PAR RÈGLES, pas à la main — FP_ENTER, fetch_ea_*, SEG_CHECK_* et CHECK_WRITE
// deviennent `if (…) return 1;` comme ailleurs dans le cœur ; les conversions double ->
// entier passent par les aides cvttsd2si de x87_ops.cs (DEVIATION, mesurées en G4.0) ;
// `floor(fmod(x, 10.0))` devient `Math.Floor(x % 10.0)`, dont la parité avec la glibc est
// mesurée (x87-parity) ; les unions x87_td et x87_ts gardent leur nom. Les conversions
// entier -> double et double <-> float sont celles du C (cvtsi2sd, cvtsd2ss, cvtss2sd).
//
// Les défauts de PCem reproduits ici portent leur marqueur sur place (PB-48, PB-53, PB-54 et des
// voisins inscrits en G13) ; FBLD n'existe pas (DF /4 est FPU_ILLEGAL, PB-52, dans la table).

using static iXtal26.Cpu._386_common;
using static iXtal26.Cpu.x86;

namespace iXtal26.Cpu;

internal static partial class _386
{
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
            // pcem bug, reproduced: PB-54 — pas de CHECK_WRITE : la limite de l'opérande n'est pas contrôlée.
            temp64 = x87_fround(ST(0));
            // /*                        if (temp64 > 32767 || temp64 < -32768)
                                       // fatal("FISTw overflow %i\n", temp64);*/
            // pcem bug, reproduced: PB-196 — hors bornes : les bits bas, pas l'indéfini entier ni IE.
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
            // pcem bug, reproduced: PB-54 — pas de CHECK_WRITE : la limite de l'opérande n'est pas contrôlée.
            temp64 = x87_fround(ST(0));
            // /*                        if (temp64 > 32767 || temp64 < -32768)
                                       // fatal("FISTw overflow %i\n", temp64);*/
            // pcem bug, reproduced: PB-196 — hors bornes : les bits bas, pas l'indéfini entier ni IE.
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
            // pcem bug, reproduced: PB-54 — pas de CHECK_WRITE : la limite de l'opérande n'est pas contrôlée.
            temp64 = x87_fround(ST(0));
            // /*                        if (temp64 > 32767 || temp64 < -32768)
                                       // fatal("FISTw overflow %i\n", temp64);*/
            // pcem bug, reproduced: PB-196 — hors bornes : les bits bas, pas l'indéfini entier ni IE.
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
            // pcem bug, reproduced: PB-54 — pas de CHECK_WRITE : la limite de l'opérande n'est pas contrôlée.
            temp64 = x87_fround(ST(0));
            // /*                        if (temp64 > 32767 || temp64 < -32768)
                                       // fatal("FISTw overflow %i\n", temp64);*/
            // pcem bug, reproduced: PB-196 — hors bornes : les bits bas, pas l'indéfini entier ni IE.
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
            // pcem bug, reproduced: PB-54 — pas de CHECK_WRITE : la limite de l'opérande n'est pas contrôlée.
            // pcem bug, reproduced: PB-195 — chiffres tronqués, pas arrondis selon RC ; ni IE ni BCD indéfini.
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
            // pcem bug, reproduced: PB-54 — pas de CHECK_WRITE : la limite de l'opérande n'est pas contrôlée.
            // pcem bug, reproduced: PB-195 — chiffres tronqués, pas arrondis selon RC ; ni IE ni BCD indéfini.
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
            // pcem bug, reproduced: PB-54 — pas de CHECK_WRITE : la limite de l'opérande n'est pas contrôlée.
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
            // pcem bug, reproduced: PB-54 — pas de CHECK_WRITE : la limite de l'opérande n'est pas contrôlée.
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
            // pcem bug, reproduced: PB-54 — pas de CHECK_WRITE : la limite de l'opérande n'est pas contrôlée.
            temp64 = x87_fround(ST(0));
            // /*                        if (temp64 > 2147483647 || temp64 < -2147483647)
                                       // fatal("FISTl out of range! %i\n", temp64);*/
            // pcem bug, reproduced: PB-196 — hors bornes : les bits bas, pas l'indéfini entier ni IE.
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
            // pcem bug, reproduced: PB-54 — pas de CHECK_WRITE : la limite de l'opérande n'est pas contrôlée.
            temp64 = x87_fround(ST(0));
            // /*                        if (temp64 > 2147483647 || temp64 < -2147483647)
                                       // fatal("FISTl out of range! %i\n", temp64);*/
            // pcem bug, reproduced: PB-196 — hors bornes : les bits bas, pas l'indéfini entier ni IE.
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
            // pcem bug, reproduced: PB-54 — pas de CHECK_WRITE : la limite de l'opérande n'est pas contrôlée.
            temp64 = x87_fround(ST(0));
            // /*                        if (temp64 > 2147483647 || temp64 < -2147483647)
                                       // fatal("FISTl out of range! %i\n", temp64);*/
            // pcem bug, reproduced: PB-196 — hors bornes : les bits bas, pas l'indéfini entier ni IE.
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
            // pcem bug, reproduced: PB-54 — pas de CHECK_WRITE : la limite de l'opérande n'est pas contrôlée.
            temp64 = x87_fround(ST(0));
            // /*                        if (temp64 > 2147483647 || temp64 < -2147483647)
                                       // fatal("FISTl out of range! %i\n", temp64);*/
            // pcem bug, reproduced: PB-196 — hors bornes : les bits bas, pas l'indéfini entier ni IE.
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
            // pcem bug, reproduced: PB-54 — pas de CHECK_WRITE : la limite de l'opérande n'est pas contrôlée.
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
            // pcem bug, reproduced: PB-54 — pas de CHECK_WRITE : la limite de l'opérande n'est pas contrôlée.
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
            // pcem bug, reproduced: PB-212 — un SNaN reste signalant, sans IE (387 et suivants : IE, QNaN).
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
            // pcem bug, reproduced: PB-212 — un SNaN reste signalant, sans IE (387 et suivants : IE, QNaN).
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
            // pcem bug, reproduced: PB-54 — pas de CHECK_WRITE : la limite de l'opérande n'est pas contrôlée.
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
            // pcem bug, reproduced: PB-54 — pas de CHECK_WRITE : la limite de l'opérande n'est pas contrôlée.
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
            // pcem bug, reproduced: PB-54 — le seul contrôle de limite des stockages x87 (FST m64 n'en a pas).
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
            // pcem bug, reproduced: PB-54 — le seul contrôle de limite des stockages x87 (FST m64 n'en a pas).
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
            // pcem bug, reproduced: PB-206 — un SNaN est rendu silencieux sans IE (387 et suivants : IE).
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
            // pcem bug, reproduced: PB-206 — un SNaN est rendu silencieux sans IE (387 et suivants : IE).
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
            // pcem bug, reproduced: PB-54 — pas de CHECK_WRITE : la limite de l'opérande n'est pas contrôlée.
            // pcem bug, reproduced: PB-48 — RC ignoré : la conversion en float arrondit au plus près.
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
            // pcem bug, reproduced: PB-54 — pas de CHECK_WRITE : la limite de l'opérande n'est pas contrôlée.
            // pcem bug, reproduced: PB-48 — RC ignoré : la conversion en float arrondit au plus près.
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
            // pcem bug, reproduced: PB-54 — pas de CHECK_WRITE : la limite de l'opérande n'est pas contrôlée.
            // pcem bug, reproduced: PB-48 — RC ignoré : la conversion en float arrondit au plus près.
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
            // pcem bug, reproduced: PB-54 — pas de CHECK_WRITE : la limite de l'opérande n'est pas contrôlée.
            // pcem bug, reproduced: PB-48 — RC ignoré : la conversion en float arrondit au plus près.
            ts.s = (float)ST(0);
            seteal(ts.i);
            if (cpu_state.abrt != 0)
                    return 1;
            x87_pop();
            CLOCK_CYCLES(x87_timings_c.x87_timings.fst_32);
            return 0;
    }
}
