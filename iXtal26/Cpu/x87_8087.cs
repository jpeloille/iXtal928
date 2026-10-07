// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/includes/private/cpu/8087.h  (lignes 1-89)
//         et pcem-dev/src/cpu/808x.c  (lignes 3304-3366, les huit ESC)
// SHA256: voir oracle.tsv ; vérifier avec tools/check-oracle.sh
// STATUS: complete — G4.6 : le contexte du 8087.
//
// 8087.h ne contient pas d'instruction x87 : c'est un jeu de macros qui fait recompiler TOUT
// x87_ops.h dans l'unité du 808x (`#include "x87_ops.h"`, :86), avec d'autres primitives. Les
// handlers eux-mêmes sont donc ceux du 387, instanciés une seconde fois — x87_ops_808x.cs,
// généré depuis les fichiers du cœur 386 — et ce fichier-ci porte ce qui change :
//
//   - FP_ENTER se réduit à `fpucount++` (X8087, x87_ops.h:261-262) : pas de #NM sur un 8088 ;
//   - fetch_ea_16/32, SEG_CHECK_*, CHECK_WRITE, PREFETCH_RUN sont VIDES : le 808x a déjà
//     décodé l'adresse (fetchea) avant d'appeler la table ;
//   - les accès 32 et 64 bits passent par le readmemw / writememw du 808x — le bus 8 bits,
//     ses cycles (memcycs), et l'offset 16 bits qui boucle dans le segment ;
//   - geteal / geteaq / seteal / seteaq font fatal() en mode registre ;
//   - writememb est writememb_8087 (segment + offset).
//
// Les aides sans mémoire (ST, pile, conversions, arrondi dirigé, comparaisons, x87_div) sont
// celles du cœur 386, partagées par `using static _386` : le C les recompile à l'identique.

using static iXtal26.Cpu._386;
using static iXtal26.Cpu._386_common;
using static iXtal26.Cpu.x86;

namespace iXtal26.Cpu;

internal static partial class _808x
{
    // pcem: x87_ops.h:261-262 — sous X8087.
    private static bool FP_ENTER()
    {
        _386_common.fpucount++;
        return false;
    }

    // pcem: 8087.h:12-17 — vides : le 808x a déjà décodé l'adresse effective.
    private static bool fetch_ea_16(uint32_t fetchdat) => false;
    private static bool fetch_ea_32(uint32_t fetchdat) => false;
    private static bool SEG_CHECK_READ(x86seg seg) => false;
    private static bool SEG_CHECK_WRITE(x86seg seg) => false;
    private static bool CHECK_WRITE(x86seg seg, uint32_t low, uint32_t high) => false;
    private static void PREFETCH_RUN(int instr_cycles, int bytes, int modrm, int reads, int reads_l,
                                     int writes, int writes_l, int ea32) { }

    // pcem: 8087.h:9-10
    private static void CLOCK_CYCLES(int c) => cycles -= c;

    // Le readmemw du 808x prend un offset de 16 bits (808x.c) : le C tronque en silence
    // l'`eaaddr + 8` de x87_ld80, et l'offset boucle dans le segment. Même geste ici.
    // pcem bug, reproduced: PB-201 — les LECTURES bouclent dans le segment ; le 8087 incrémente l'adresse
    //   physique de 20 bits, comme le font ici les écritures (writememw du 808x, writememb_8087), sans repli.
    private static uint16_t readmemw(uint32_t s, uint32_t a) => readmemw(s, (uint16_t)a);

    // pcem: 8087.h:19-23
    private static uint32_t readmeml(uint32_t seg, uint32_t addr)
        => (uint32_t)(readmemw(seg, addr) | (readmemw(seg, addr + 2) << 16));

    private static uint64_t readmemq(uint32_t seg, uint32_t addr)
        => (uint64_t)readmemw(seg, addr) | ((uint64_t)readmemw(seg, addr + 2) << 16) |
           ((uint64_t)readmemw(seg, addr + 4) << 32) | ((uint64_t)readmemw(seg, addr + 6) << 48);

    // pcem: 8087.h:25-35, :85 — writememb_8087, writememl, writememq.
    private static void writememb(uint32_t seg, uint32_t addr, uint8_t val) => writememb(seg + addr, val);

    private static void writememl(uint32_t seg, uint32_t addr, uint32_t val)
    {
        writememw(seg, addr, (uint16_t)(val & 0xffff));
        writememw(seg, addr + 2, (uint16_t)((val >> 16) & 0xffff));
    }

    private static void writememq(uint32_t seg, uint32_t addr, uint64_t val)
    {
        writememw(seg, addr, (uint16_t)(val & 0xffff));
        writememw(seg, addr + 2, (uint16_t)((val >> 16) & 0xffff));
        writememw(seg, addr + 4, (uint16_t)((val >> 32) & 0xffff));
        writememw(seg, addr + 6, (uint16_t)((val >> 48) & 0xffff));
    }

    // pcem: 8087.h:37-58
    private static uint32_t geteal()
    {
        if (cpu_mod == 3)
                pc.fatal("geteal cpu_mod==3\n");
        return readmeml(easeg, cpu_state.eaaddr);
    }

    private static uint64_t geteaq()
    {
        if (cpu_mod == 3)
                pc.fatal("geteaq cpu_mod==3\n");
        return readmemq(easeg, cpu_state.eaaddr);
    }

    private static void seteal(uint32_t val)
    {
        if (cpu_mod == 3)
                pc.fatal("seteal cpu_mod==3\n");
        else
                writememl(easeg, cpu_state.eaaddr, val);
    }

    private static void seteaq(uint64_t val)
    {
        if (cpu_mod == 3)
                pc.fatal("seteaq cpu_mod==3\n");
        else
                writememq(easeg, cpu_state.eaaddr, val);
    }

    // ---- Les aides de x87_ops.h qui touchent la mémoire, réinstanciées ------------------

    // pcem: x87_ops.h:86-115, dans l'unité du 808x.
    private static double x87_ld80()
    {
        uint64_t ll = readmeml(easeg, cpu_state.eaaddr);
        ll |= (uint64_t)readmeml(easeg, cpu_state.eaaddr + 4) << 32;
        int16_t begin = (int16_t)readmemw(easeg, cpu_state.eaaddr + 8);

        // pcem bug, reproduced: PB-55 — exposant replié modulo 1024, exposant nul gardé nul (dénormaux faux),
        //   retenue de l'arrondi collée par OU dans l'exposant, bit entier ignoré.
        int64_t exp64 = (((begin & 0x7fff) - BIAS80));
        int64_t blah = ((exp64 > 0) ? exp64 : -exp64) & 0x3ff;
        int64_t exp64final = ((exp64 > 0) ? blah : -blah) + BIAS64;

        int64_t mant64 = (int64_t)((ll >> 11) & (0xfffffffffffff));
        int64_t sign = (begin & 0x8000) != 0 ? 1 : 0;

        if ((begin & 0x7fff) == 0x7fff)
                exp64final = 0x7ff;
        if ((begin & 0x7fff) == 0)
                exp64final = 0;
        if ((ll & 0x400) != 0)
                mant64++;

        ll = unchecked((uint64_t)((sign << 63) | (exp64final << 52) | mant64));

        return BitConverter.UInt64BitsToDouble(ll);
    }

    // pcem: x87_ops.h:117-150, dans l'unité du 808x.
    private static void x87_st80(double d)
    {
        uint64_t ll = BitConverter.DoubleToUInt64Bits(d);

        int64_t sign80 = (ll & (0x8000000000000000)) != 0 ? 1 : 0;
        int64_t exp80 = (int64_t)(ll & (0x7ff0000000000000));
        int64_t exp80final = (exp80 >> 52);
        int64_t mant80 = (int64_t)(ll & (0x000fffffffffffff));
        uint64_t mant80final = (uint64_t)(mant80 << 11);

        // pcem bug, reproduced: PB-56 — un double dénormal reçoit le bit entier et l'exposant rebiaisé (le normal
        //   2^-1023 × 1,f) ; les onze bits bas de la mantisse sont toujours nuls : ST n'a que 53 bits.
        if (exp80final == 0x7ff) /*Infinity / Nan*/
        {
                exp80final = 0x7fff;
                mant80final |= (0x8000000000000000);
        }
        else if (d != 0) // Zero is a special case
        {
                // Elvira wants the 8 and tcalc doesn't
                mant80final |= (0x8000000000000000);
                // Ca-cyber doesn't like this when result is zero.
                exp80final += (BIAS80 - BIAS64);
        }
        int16_t begin = unchecked((int16_t)((sign80 << 15) | (exp80final & 0xffff)));
        ll = mant80final;

        writememl(easeg, cpu_state.eaaddr, (uint32_t)ll);
        writememl(easeg, cpu_state.eaaddr + 4, (uint32_t)(ll >> 32));
        writememw(easeg, cpu_state.eaaddr + 8, (uint16_t)begin);
    }

    // pcem: x87_ops.h:152-161
    private static void x87_st_fsave(int reg)
    {
        reg = (cpu_state.TOP + reg) & 7;

        // pcem bug, reproduced: PB-199 — un registre TAG_UINT64 s'écrit en entier suivi de 0x5555,
        //   pas en réel de 80 bits.
        if ((cpu_state.tag[reg] & x87_c.TAG_UINT64) != 0)
        {
                writememl(easeg, cpu_state.eaaddr, (uint32_t)(cpu_state.MM[reg].q & 0xffffffff));
                writememl(easeg, cpu_state.eaaddr + 4, (uint32_t)(cpu_state.MM[reg].q >> 32));
                writememw(easeg, cpu_state.eaaddr + 8, 0x5555);
        }
        else
                x87_st80(cpu_state.ST[reg]);
    }

    // pcem: x87_ops.h:163-175
    private static void x87_ld_frstor(int reg)
    {
        reg = (cpu_state.TOP + reg) & 7;

        cpu_state.MM[reg].q = readmemq(easeg, cpu_state.eaaddr);
        cpu_state.MM_w4[reg] = readmemw(easeg, cpu_state.eaaddr + 8);

        // pcem bug, reproduced: PB-199 — une image qui finit par 0x5555 sous une étiquette 10 est relue
        //   comme un entier de 64 bits, pas comme un réel de 80 bits.
        if ((cpu_state.MM_w4[reg] == 0x5555) && (cpu_state.tag[reg] & x87_c.TAG_UINT64) != 0)
        {
                cpu_state.ST[reg] = (double)cpu_state.MM[reg].q; // uint64_t -> double, NON signé
        }
        else
        {
                cpu_state.tag[reg] &= unchecked((uint8_t)~x87_c.TAG_UINT64);
                cpu_state.ST[reg] = x87_ld80();
        }
    }

    // pcem: x87_ops.h:183-187
    private static void x87_stmmx(MMX_REG r)
    {
        writememl(easeg, cpu_state.eaaddr, (uint32_t)r.q);
        writememl(easeg, cpu_state.eaaddr + 4, (uint32_t)(r.q >> 32));
        writememw(easeg, cpu_state.eaaddr + 8, 0xffff);
    }

    // pcem: x87_ops.h:297-308 — FPU_ILLEGAL, fetch_ea et PREFETCH_RUN vides ici.
    private static int FPU_ILLEGAL_a16(uint32_t fetchdat)
    {
        if (fetch_ea_16(fetchdat)) return 1;
        CLOCK_CYCLES(cpu_c.timing_rr);
        PREFETCH_RUN(cpu_c.timing_rr, 2, (int)fetchdat, 0, 0, 0, 0, 0);
        return 0;
    }

    private static int FPU_ILLEGAL_a32(uint32_t fetchdat)
    {
        if (fetch_ea_32(fetchdat)) return 1;
        CLOCK_CYCLES(cpu_c.timing_rr);
        PREFETCH_RUN(cpu_c.timing_rr, 2, (int)fetchdat, 0, 0, 0, 0, 0);
        return 0;
    }
}
