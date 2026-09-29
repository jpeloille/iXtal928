// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/includes/private/cpu/x87_ops.h  (lignes 7-8, 11, 34-58, 60-150, 262-272,
//         297-308, 322-1040 pour la disposition des tables)
// SHA256: voir oracle.tsv ; vérifier avec tools/check-oracle.sh
// STATUS: partial — G4.2 : les aides des chargements et stockages (ST, x87_push, x87_pop,
//         x87_fround, x87_ld80, x87_st80, FP_ENTER), FPU_ILLEGAL, et les rangées MÉMOIRE de
//         D9, DB, DD et DF qui portent un handler de x87_ops_loadstore.h. Le reste des tables
//         (les rangées de mode registre, FLDENV, FLDCW, FSTENV, FSTCW, FRSTOR, FSAVE, FSTSW,
//         et D8, DA, DC, DE entières) reste en opX87NonTranscrit jusqu'à G4.3-G4.5.
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
    private static ref double ST(int x) => ref cpu_state.ST[(cpu_state.TOP + x) & 7];

    // pcem: x87_ops.h:264-272 — FP_ENTER. #NM (INT 7) si CR0.EM ou CR0.TS ; rend vrai quand
    //   le handler doit sortir, comme fetch_ea_16 et les gardes SEG_CHECK_*.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool FP_ENTER()
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
    private static void x87_push(double i)
    {
        cpu_state.TOP--;
        cpu_state.ST[cpu_state.TOP & 7] = i;
        cpu_state.tag[cpu_state.TOP & 7] = x87_c.TAG_VALID;
    }

    // pcem: x87_ops.h:53-58
    private static double x87_pop()
    {
        double t = cpu_state.ST[cpu_state.TOP & 7];
        cpu_state.tag[cpu_state.TOP & 7] = x87_c.TAG_EMPTY;
        cpu_state.TOP++;
        return t;
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
    private static int64_t x87_fround(double b)
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
    private const int BIAS80 = 16383;
    private const int BIAS64 = 1023;

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

    // ---- La disposition des tables (x87_ops.h:322-1040) -----------------------------
    //
    // D9, DB, DD et DF ont 256 emplacements indexés par l'octet ModRM entier : trois blocs de
    // 64 pour mod = 0, 1, 2 (une rangée de huit par champ `reg`, le même handler pour les huit
    // `rm`), puis 64 pour mod = 3. G4.2 pose les rangées mémoire dont le handler est un
    // chargement ou un stockage (x87_ops_loadstore.h) ou ILLEGAL ; les autres restent en
    // opX87NonTranscrit. `null` dans une rangée : pas encore transcrit.

    /// <summary>La table d'échappement `op` (0xD8 à 0xDF) pour la taille d'adresse donnée.
    /// D8 et DC ont 32 emplacements, `[(fetchdat >> 3) &amp; 0x1f]`.</summary>
    private static OpFn[] TableFpu(int op, bool a32)
    {
        var t = X87NonTranscrit(op is 0xD8 or 0xDC ? 32 : 256);
        OpFn ill = a32 ? FPU_ILLEGAL_a32 : FPU_ILLEGAL_a16;
        OpFn?[]? rangees = op switch
        {
            // pcem: x87_ops.h:322 (a16), :360 (a32)
            0xD9 => [a32 ? opFLDs_a32 : opFLDs_a16, ill, a32 ? opFSTs_a32 : opFSTs_a16,
                     a32 ? opFSTPs_a32 : opFSTPs_a16, null, null, null, null],
            // pcem: x87_ops.h:557 (a16), :596 (a32)
            0xDB => [a32 ? opFILDil_a32 : opFILDil_a16, ill, a32 ? opFISTil_a32 : opFISTil_a16,
                     a32 ? opFISTPil_a32 : opFISTPil_a16, ill, a32 ? opFLDe_a32 : opFLDe_a16, ill,
                     a32 ? opFSTPe_a32 : opFSTPe_a16],
            // pcem: x87_ops.h:726 (a16), :765 (a32)
            0xDD => [a32 ? opFLDd_a32 : opFLDd_a16, ill, a32 ? opFSTd_a32 : opFSTd_a16,
                     a32 ? opFSTPd_a32 : opFSTPd_a16, null, ill, null, null],
            // pcem: x87_ops.h:884 (a16), :923 (a32) — /4, FBLD, est ILLEGAL (PB-52).
            0xDF => [a32 ? opFILDiw_a32 : opFILDiw_a16, ill, a32 ? opFISTiw_a32 : opFISTiw_a16,
                     a32 ? opFISTPiw_a32 : opFISTPiw_a16, ill, a32 ? opFILDiq_a32 : opFILDiq_a16,
                     a32 ? FBSTP_a32 : FBSTP_a16, a32 ? FISTPiq_a32 : FISTPiq_a16],
            _ => null,
        };
        if (rangees is null)
                return t;
        for (var mod = 0; mod < 3; mod++)
        for (var reg = 0; reg < 8; reg++)
        for (var rm = 0; rm < 8; rm++)
                if (rangees[reg] is { } h)
                        t[(mod << 6) | (reg << 3) | rm] = h;
        return t;
    }
}
