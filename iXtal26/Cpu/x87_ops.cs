// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/includes/private/cpu/x87_ops.h  (lignes 7-8, 11, 34-58, 60-150, 262-272,
//         297-308, 322-1040 pour la disposition des tables)
// SHA256: voir oracle.tsv ; vérifier avec tools/check-oracle.sh
// STATUS: partial — G4.2 : les aides des chargements et stockages (ST, x87_push, x87_pop,
//         x87_fround, x87_ld80, x87_st80, FP_ENTER), FPU_ILLEGAL ; G4.3 : x87_div,
//         x87_compare, x87_ucompare, et l'arrondi dirigé de FADD mémoire (PB-48). Les tables
//         sont dans x87_ops_tables.cs.
//
// PCem garde ST en DOUBLE (x86.h:93) : le format 80 bits n'existe qu'aux frontières mémoire,
// x87_ld80 et x87_st80, qui le convertissent À LEUR FAÇON — exposant replié modulo 1024,
// dénormaux écrasés, arrondi de la mantisse par le bit 10 qui peut déborder dans l'exposant
// (PB-52). Un double C# les reproduit au bit près.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

using static iXtal26.Cpu._386_common;
using static iXtal26.Cpu.x86;

namespace iXtal26.Cpu;

// pcem: x87_ops.h:251-259 — les deux unions qui relisent des bits en flottant.
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit)]
internal struct x87_ts
{
    [System.Runtime.InteropServices.FieldOffset(0)] internal float s;
    [System.Runtime.InteropServices.FieldOffset(0)] internal uint32_t i;
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit)]
internal struct x87_td
{
    [System.Runtime.InteropServices.FieldOffset(0)] internal double d;
    [System.Runtime.InteropServices.FieldOffset(0)] internal uint64_t i;
}

internal static partial class _386
{
    // pcem: x87_ops.h:11 — ST(x), le registre x de la pile, relatif à TOP.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ref double ST(int x) => ref cpu_state.ST[(cpu_state.TOP + x) & 7];

    // pcem: x87_ops.h:264-272 — FP_ENTER. #NM (INT 7) si CR0.EM ou CR0.TS ; rend vrai quand
    //   le handler doit sortir, comme fetch_ea_16 et les gardes SEG_CHECK_*.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool FP_ENTER()
    {
        if ((cr0 & 0xc) != 0)
        {
                x86_int(7);
                return true;
        }
        fpucount++;
        return false;
    }

    // pcem: x87_ops.h:34-38
    internal static void x87_push(double i)
    {
        cpu_state.TOP--;
        cpu_state.ST[cpu_state.TOP & 7] = i;
        cpu_state.tag[cpu_state.TOP & 7] = x87_c.TAG_VALID;
    }

    // pcem: x87_ops.h:53-58
    internal static double x87_pop()
    {
        double t = cpu_state.ST[cpu_state.TOP & 7];
        cpu_state.tag[cpu_state.TOP & 7] = x87_c.TAG_EMPTY;
        cpu_state.TOP++;
        return t;
    }

    // pcem: x87_ops.h:15
    internal const uint16_t FPCW_DISI = 1 << 7;

    // pcem: x87_ops.h:40-51 — G4.4 (FLDLN2). Pousse des BITS, pas une valeur.
    internal static void x87_push_u64(uint64_t i)
    {
        cpu_state.TOP--;
        cpu_state.ST[cpu_state.TOP & 7] = BitConverter.UInt64BitsToDouble(i);
        cpu_state.tag[cpu_state.TOP & 7] = x87_c.TAG_VALID;
    }

    // pcem: x87_ops.h:152-161 — G4.4 (FSAVE). Un registre qui porte l'entier exact de FILD
    //   64 bits (TAG_UINT64) s'écrit comme cet entier suivi de 0x5555, pas comme un réel.
    private static void x87_st_fsave(int reg)
    {
        reg = (cpu_state.TOP + reg) & 7;

        if ((cpu_state.tag[reg] & x87_c.TAG_UINT64) != 0)
        {
                writememl(easeg, cpu_state.eaaddr, (uint32_t)(cpu_state.MM[reg].q & 0xffffffff));
                writememl(easeg, cpu_state.eaaddr + 4, (uint32_t)(cpu_state.MM[reg].q >> 32));
                writememw(easeg, cpu_state.eaaddr + 8, 0x5555);
        }
        else
                x87_st80(cpu_state.ST[reg]);
    }

    // pcem: x87_ops.h:163-175 — G4.4 (FRSTOR). Le marqueur 0x5555 n'est cru que si le tag,
    //   déjà rechargé par x87_settag, dit TAG_UINT64 ; sinon, un réel de 80 bits (PB-55).
    private static void x87_ld_frstor(int reg)
    {
        reg = (cpu_state.TOP + reg) & 7;

        cpu_state.MM[reg].q = readmemq(easeg, cpu_state.eaaddr);
        cpu_state.MM_w4[reg] = readmemw(easeg, cpu_state.eaaddr + 8);

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

    // pcem: x87_ops.h:183-187 — G4.4 (FSAVE en mode MMX). MMX_REG réduite à .q : l[0], l[1]
    //   sont ses moitiés basse et haute.
    private static void x87_stmmx(MMX_REG r)
    {
        writememl(easeg, cpu_state.eaaddr, (uint32_t)r.q);
        writememl(easeg, cpu_state.eaaddr + 4, (uint32_t)(r.q >> 32));
        writememw(easeg, cpu_state.eaaddr + 8, 0xffff);
    }

    // `*(uint64_t *)cpu_state.tag == 0x0101010101010101ull` (x87_ops_misc.h:141) : huit TAG_VALID.
    internal static bool TagsTousValides()
    {
        for (var c = 0; c < 8; c++)
                if (cpu_state.tag[c] != 0x01)
                        return false;
        return true;
    }

    // ---- Les conversions double -> entier ---------------------------------------------
    //
    // DEVIATION: C convertit par `(int64_t)d`, `(int32_t)d`, `(uint8_t)d` — de l'UB hors
    //   bornes, que GCC compile en cvttsd2si : « l'entier indéfini » 0x8000… sur NaN, infinis
    //   et dépassements. .NET SATURE depuis la version 9 (NaN -> 0, +inf -> MaxValue). Mesuré
    //   en G4.0 (`x87-parity`, contre h_conv dans l'oracle) : le cast .NET s'écarte ~1,2 M fois
    //   sur 10^7, ces aides jamais. Elles exécutent l'instruction elle-même.

    /// <summary>(int64_t)d : cvttsd2si 64 bits.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static long CvtI64(double d) => Sse2.X64.ConvertToInt64WithTruncation(Vector128.CreateScalar(d));

    /// <summary>(uint64_t)d : la séquence de GCC x86-64 sans AVX-512 — d − 2^63 puis le
    /// bit 63 inversé si d &gt;= 2^63, sinon cvttsd2si direct. Un NaN, non ordonné, prend la
    /// branche directe.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ulong CvtU64(double d) => d >= 9223372036854775808.0
        ? (ulong)CvtI64(d - 9223372036854775808.0) ^ 0x8000000000000000UL
        : (ulong)CvtI64(d);

    /// <summary>(int32_t)d, et (int16_t)d / (uint8_t)d par troncature du résultat : cvttsd2si
    /// 32 bits, indéfini 0x80000000.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int CvtI32(double d) => Sse2.ConvertToInt32WithTruncation(Vector128.CreateScalar(d));

    // pcem: x87_ops.h:60-83
    internal static int64_t x87_fround(double b)
    {
        int64_t a, c;

        switch ((cpu_state.npxc >> 10) & 3)
        {
        case 0: /*Nearest*/
                a = CvtI64(Math.Floor(b));
                c = CvtI64(Math.Floor(b + 1.0));
                if ((b - a) < (c - b))
                        return a;
                else if ((b - a) > (c - b))
                        return c;
                else
                        return (a & 1) != 0 ? c : a;
        case 1: /*Down*/
                return CvtI64(Math.Floor(b));
        case 2: /*Up*/
                return CvtI64(Math.Ceiling(b));
        case 3: /*Chop*/
                return CvtI64(b);
        }

        return 0;
    }

    // pcem: x87_ops.h:84-85
    internal const int BIAS80 = 16383;
    internal const int BIAS64 = 1023;

    // pcem: x87_ops.h:86-115 — le réel 80 bits en mémoire, ramené à un double. Trois lectures
    //   dans l'ordre du C, sans test d'abandon entre elles.
    private static double x87_ld80()
    {
        uint64_t ll = readmeml(easeg, cpu_state.eaaddr);
        ll |= (uint64_t)readmeml(easeg, cpu_state.eaaddr + 4) << 32;
        int16_t begin = (int16_t)readmemw(easeg, cpu_state.eaaddr + 8);

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

    // pcem: x87_ops.h:117-150
    private static void x87_st80(double d)
    {
        uint64_t ll = BitConverter.DoubleToUInt64Bits(d);

        int64_t sign80 = (ll & (0x8000000000000000)) != 0 ? 1 : 0;
        int64_t exp80 = (int64_t)(ll & (0x7ff0000000000000));
        int64_t exp80final = (exp80 >> 52);
        int64_t mant80 = (int64_t)(ll & (0x000fffffffffffff));
        uint64_t mant80final = (uint64_t)(mant80 << 11);

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
        // `(((int16_t)sign80) << 15) | (int16_t)exp80final` en C, promu en int puis tronqué à
        // 16 bits : seuls les 16 bits bas comptent, et ce sont ceux-ci.
        int16_t begin = unchecked((int16_t)((sign80 << 15) | (exp80final & 0xffff)));
        ll = mant80final;

        writememl(easeg, cpu_state.eaaddr, (uint32_t)ll);
        writememl(easeg, cpu_state.eaaddr + 4, (uint32_t)(ll >> 32));
        writememw(easeg, cpu_state.eaaddr + 8, (uint16_t)begin);
    }

    // pcem: x87_ops.h:13 — le bit ZE du mot d'état, et son masque au même rang dans npxc.
    internal const uint16_t STATUS_ZERODIVIDE = 4;

    // pcem: x87_ops.h:17-30 — x87_div. SEULE exception que PCem modélise : le diviseur nul.
    //   Masquée (npxc bit 2), le quotient IEEE (±∞ ou NaN) ; démasquée, IRQ13 et le handler
    //   sort AUSSITÔT par `return 1` — sans poser le tag ni compter ses cycles (PB-59).
    //   Rend vrai quand le handler doit sortir, comme les gardes.
    internal static bool x87_div(ref double dst, double src1, double src2)
    {
        if (((double)src2) == 0.0)
        {
                cpu_state.npxs |= STATUS_ZERODIVIDE;
                if ((cpu_state.npxc & STATUS_ZERODIVIDE) != 0)
                        dst = src1 / (double)src2;
                else
                {
                        // omitted: pclog("FPU : divide by zero\n") — sortie pure.
                        Models.pic.picint(1 << 13);
                        return true;
                }
        }
        else
                dst = src1 / (double)src2;
        return false;
    }

    // pcem: x87_ops.h:189-217 et :219-247 — x87_compare et x87_ucompare.
    // DEVIATION: sur amd64, PCem compare en exécutant `fldl ; fldl ; fclex ; fcompp ; fnstsw`
    //   sur le x87 de l'HÔTE (asm en ligne), puis masque C0|C2|C3. Le C# n'a pas de x87 ; il
    //   rend ce que ce fcompp rend : deux doubles chargés sans perte, comparés exactement —
    //   a < b → C0, a == b → C3 (+0 et −0 égaux), non ordonnés (un NaN) → C3|C2|C0, a > b → 0.
    //   fcompp et fucompp ne diffèrent que par l'exception levée, que fclex et le masque
    //   effacent. Le fuzzeur de G4.3 le confronte à l'oracle, NaN et zéros signés tirés.
    internal static uint16_t x87_compare(double a, double b)
    {
        if (double.IsNaN(a) || double.IsNaN(b))
                return x87_c.C0 | x87_c.C2 | x87_c.C3;
        if (a < b)
                return x87_c.C0;
        if (a == b)
                return x87_c.C3;
        return 0;
    }

    internal static uint16_t x87_ucompare(double a, double b) => x87_compare(a, b);

    // QUEL NaN GAGNE (PB-60). `addsd` et `mulsd` rendent le PREMIER opérande NaN, rendu
    // silencieux, quand les deux en sont ; l'addition et la multiplication étant commutatives,
    // GCC a choisi l'ordre handler par handler, et RyuJIT n'y est pas tenu. Ces deux aides
    // imposent la règle SSE avec un ordre EXPLICITE, celui que `x87-nan-order` a mesuré sur
    // l'oracle. Hors NaN, l'ordre est indifférent (et ∞ − ∞ rend le NaN par défaut des deux
    // côtés). La soustraction et la division ne sont pas concernées : leur ordre est fixé.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static double X87Quiet(double d) => BitConverter.UInt64BitsToDouble(BitConverter.DoubleToUInt64Bits(d) | 0x0008000000000000UL);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static double X87AddSd(double first, double second)
        => double.IsNaN(first) ? X87Quiet(first) : double.IsNaN(second) ? X87Quiet(second) : first + second;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static double X87MulSd(double first, double second)
        => double.IsNaN(first) ? X87Quiet(first) : double.IsNaN(second) ? X87Quiet(second) : first * second;

    // L'ARRONDI DIRIGÉ DE FADD MÉMOIRE (x87_ops_arith.h:12-16), décision n° 3 réduite par PB-48.
    // DEVIATION: PCem encadre `ST(0) += use_var` de fesetround(RC) … fesetround(FE_TONEAREST) ;
    //   .NET n'a pas d'équivalent, et toucher MXCSR par P/Invoke atteindrait le JIT. La somme
    //   est donc arrondie au plus près, puis corrigée d'un ulp selon le signe de son erreur
    //   EXACTE (TwoSum, exact pour deux doubles finis sans débordement), avec les trois cas que
    //   l'erreur ne dit pas : le débordement (vers le bas ou vers zéro, le plus grand fini au
    //   lieu de ±∞), les opérandes infinis ou NaN (le résultat ne dépend pas du mode), et le
    //   signe d'un zéro exact (−0 vers le bas, sauf +0 + +0). Mesuré en G4.0 contre le
    //   fesetround de l'oracle (x87-parity (a), 100 %), et confronté au vrai handler en G4.3.
    //   Appelé pour les SEULS opFADD mémoire : FSUB, FMUL, FDIV restent au plus près (PB-48).
    // `a` est l'opérande que GCC met en premier — la mémoire, mesuré (PB-60) — pour les NaN.
    internal static double x87_fadd_dirige(double a, double b, int rc)
    {
        double r = a + b;
        if (double.IsNaN(r))
                return X87AddSd(a, b);
        if (double.IsInfinity(r))
        {
                if (double.IsInfinity(a) || double.IsInfinity(b))
                        return r;
                if (rc == 3 || (rc == 1 && r > 0) || (rc == 2 && r < 0))
                        return r > 0 ? double.MaxValue : -double.MaxValue;
                return r;
        }
        double bb = r - a;
        double err = (a - (r - bb)) + (b - bb);
        if (err == 0)
        {
                if (r == 0 && rc == 1 && !(double.IsPositive(a) && a == 0 && double.IsPositive(b) && b == 0))
                        return -0.0;
                return r;
        }
        return rc switch
        {
            1 => err < 0 ? Math.BitDecrement(r) : r,
            2 => err > 0 ? Math.BitIncrement(r) : r,
            _ => (r > 0 && err < 0) ? Math.BitDecrement(r) : (r < 0 && err > 0) ? Math.BitIncrement(r) : r,
        };
    }

    // pcem: x87_ops.h:297-308 — FPU_ILLEGAL : décode l'adresse effective, compte timing_rr, ne
    //   fait rien. C'est aussi ce que PCem met sous DF /4, FBLD (PB-52).
    //   Le `rmdat` de PREFETCH_RUN est le PARAMÈTRE : x86.h:197 fait `#define fetchdat rmdat`,
    //   si bien que le `uint32_t fetchdat` du handler s'appelle rmdat et masque la globale.
    //   (Première transcription : la globale x86.rmdat — le fuzzeur G4.2 l'a vue, prefetch_bytes
    //   oracle 0, C# 1, sur `66 DB 65 92`.)
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

    // La disposition des tables (x87_ops.h:310-1040) est dans x87_ops_tables.cs, générée
    // verbatim depuis le C en G4.3 ; G4.2 la construisait rangée par rangée (TableFpu).

}
