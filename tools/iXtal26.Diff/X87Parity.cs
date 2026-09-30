// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// G4.0 — LES TROIS MESURES DE PARITÉ du x87, avant toute ligne de x87 côté C#
// (PLAN-G4.md § G4.0, point 3). PCem garde ST en double (x86.h:93) : un double C# le porte
// au bit près, et le risque se loge ailleurs. Chaque mesure confronte le C# à l'ORACLE — la
// .so, compilée avec les drapeaux de 386.c —, pas à un P/Invoke vers libm.so.6.
//
//   1. libm : Math.* contre h_libm, fonctions nues et expressions de x87_ops_misc.h verbatim ;
//   2. conversions double -> entier : le cast .NET, puis l'aide qui reproduit cvttsd2si,
//      contre h_conv ;
//   3. fesetround : l'arrondi dirigé EXACT (calculé ici par TwoSum/FMA) contre
//      (a) h_fpu_arith, le motif de x87_ops_arith.h:12-16 hors du handler, et
//      (b) LE VRAI HANDLER de PCem, FADD/FSUB/FMUL/FDIV m64, exécuté par l'oracle seul
//          (hasfpu = 1, cœur 386) — c'est lui qui décide ce que le C# devra reproduire.

using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using iXtal26.Diag;

namespace iXtal26.Diff;

internal static class X87Parity
{
    private const int H_LIBM_COUNT = 14;
    private const int FPU_387 = 4;

    private static readonly string[] LibmNames =
    [
        "sin", "cos", "tan", "atan2", "log", "pow", "sqrt", "fmod", "floor", "ceil",
        "F2XM1  pow(2,x)-1", "FYL2X  y*(log x/log 2)", "FYL2XP1", "FSCALE x*pow(2,(int64)y)",
    ];

    private static ulong _s = 0x9E3779B97F4A7C15UL;

    private static ulong N()
    {
        _s ^= _s << 13;
        _s ^= _s >> 7;
        _s ^= _s << 17;
        return _s;
    }

    private static double U() => (N() >> 11) * (1.0 / (1UL << 53));

    // Trois familles : bits bruts (NaN, infinis, dénormaux), petite plage, grande plage.
    private static double D(long k) => (k % 3) switch
    {
        0 => BitConverter.UInt64BitsToDouble(N()),
        1 => (U() * 2 - 1) * 10,
        _ => (U() * 2 - 1) * Math.Pow(2, (int)(N() % 128) - 32),
    };

    private static ulong B(double d) => BitConverter.DoubleToUInt64Bits(d);

    /// <summary>cvttsd2si 64 bits — ce que GCC émet pour (int64_t)d. « L'entier indéfini »
    /// 0x8000000000000000 sur NaN, infinis et dépassements, là où .NET sature.</summary>
    internal static long CvtI64(double d) => Sse2.X64.ConvertToInt64WithTruncation(Vector128.CreateScalar(d));

    /// <summary>La séquence de GCC x86-64 pour (uint64_t)d sans AVX-512 : cvttsd2si(d - 2^63)
    /// puis le bit 63 inversé si d &gt;= 2^63, sinon cvttsd2si direct. Le test est bien
    /// `&gt;=` et non `&lt;` : un NaN, non ordonné, prend la branche DIRECTE et rend
    /// 0x8000000000000000 — mesuré contre h_conv, le `&lt;` le rendait à 0.</summary>
    internal static ulong CvtU64(double d) => d >= 9223372036854775808.0
        ? (ulong)CvtI64(d - 9223372036854775808.0) ^ 0x8000000000000000UL
        : (ulong)CvtI64(d);

    /// <summary>cvttsd2si 32 bits : (int32_t)d. Indéfini 0x80000000.</summary>
    internal static int CvtI32(double d) => Sse2.ConvertToInt32WithTruncation(Vector128.CreateScalar(d));

    private static double Libm(int n, double a, double b) => n switch
    {
        0 => Math.Sin(a),
        1 => Math.Cos(a),
        2 => Math.Tan(a),
        3 => Math.Atan2(a, b),
        4 => Math.Log(a),
        5 => Math.Pow(a, b),
        6 => Math.Sqrt(a),
        7 => a % b,
        8 => Math.Floor(a),
        9 => Math.Ceiling(a),
        10 => Math.Pow(2.0, a) - 1.0,
        11 => b * (Math.Log(a) / Math.Log(2.0)),
        12 => b * (Math.Log(a + 1.0) / Math.Log(2.0)),
        _ => a != 0.0 ? a * Math.Pow(2.0, (double)CvtI64(b)) : a,
    };

    public static int Run(long n)
    {
        Oracle.CheckAbi();
        var bad = 0;
        Console.WriteLine($"x87-parity — {n:N0} tirages par mesure, .NET {Environment.Version}");

        // --- 1. libm -------------------------------------------------------------
        Console.WriteLine("\n1. libm : Math.* contre h_libm (la .so de l'oracle)");
        for (var f = 0; f < H_LIBM_COUNT; f++)
        {
            long diff = 0, nanPayload = 0;
            string? first = null;
            for (long i = 0; i < n; i++)
            {
                double a = D(i), b = D(i / 3);
                var c = Oracle.h_libm(f, B(a), B(b));
                var s = B(Libm(f, a, b));
                if (c == s)
                    continue;
                if (double.IsNaN(BitConverter.UInt64BitsToDouble(c)) && double.IsNaN(BitConverter.UInt64BitsToDouble(s)))
                    nanPayload++;
                else
                    diff++;
                first ??= $"a={a:R} b={b:R} : oracle {c:X16}, C# {s:X16}";
            }
            bad += diff > 0 || nanPayload > 0 ? 1 : 0;
            Console.WriteLine($"   {LibmNames[f],-26} écarts de bits {diff,10:N0}   NaN de charge différente {nanPayload,8:N0}" +
                              (first is null ? "" : $"\n      1er : {first}"));
        }

        // --- 2. conversions --------------------------------------------------------
        Console.WriteLine("\n2. conversions double -> entier : cast .NET et aide cvttsd2si, contre h_conv");
        string[] convNames = ["(int64_t)", "(uint64_t)", "(int32_t)", "(int16_t)"];
        for (var k = 0; k < 4; k++)
        {
            long naive = 0, helper = 0;
            string? ex = null, exH = null;
            for (long i = 0; i < n; i++)
            {
                var d = D(i);
                var c = Oracle.h_conv(k, B(d));
                ulong nv, hv;
                switch (k)
                {
                    case 0: nv = (ulong)(long)d; hv = (ulong)CvtI64(d); break;
                    case 1: nv = (ulong)d; hv = CvtU64(d); break;
                    case 2: nv = (ulong)(long)(int)d; hv = (ulong)(long)CvtI32(d); break;
                    default: nv = (ulong)(long)(short)d; hv = (ulong)(long)(short)CvtI32(d); break;
                }
                if (nv != c) { naive++; ex ??= $"{d:R} : oracle {c:X16}, .NET {nv:X16}"; }
                if (hv != c) { helper++; exH ??= $"{d:R} ({B(d):X16}) : oracle {c:X16}, aide {hv:X16}"; }
            }
            bad += helper > 0 ? 1 : 0;
            Console.WriteLine($"   {convNames[k],-11} cast .NET : {naive,10:N0} écarts   aide cvttsd2si : {helper,8:N0} écarts" +
                              (ex is null ? "" : $"\n      ex. {ex}") +
                              (exH is null ? "" : $"\n      aide, 1er écart : {exH}"));
        }

        // --- 3. fesetround -----------------------------------------------------------
        Console.WriteLine("\n3. fesetround : l'arrondi dirigé exact (TwoSum/FMA) contre l'oracle");
        string[] modeNames = ["plus près", "vers -inf", "vers +inf", "vers zéro"];
        string[] opNames = ["+", "-", "*", "/"];
        var perMode = Math.Max(1, n / 16);
        Console.WriteLine($"   (a) h_fpu_arith, le motif de x87_ops_arith.h:12-16 hors du handler ; {perMode:N0} tirages par case");
        for (var op = 0; op < 4; op++)
        for (var mode = 0; mode < 4; mode++)
        {
            long exact = 0, nearest = 0, other = 0, inexact = 0;
            for (long i = 0; i < perMode; i++)
            {
                double a = Finite(), b = Finite();
                var r = Directed(op, mode, a, b, out var wasInexact);
                if (wasInexact) inexact++;
                var c = BitConverter.UInt64BitsToDouble(Oracle.h_fpu_arith(op, mode, B(a), B(b)));
                if (B(c) == B(r)) exact++;
                else if (B(c) == B(Directed(op, 0, a, b, out _))) nearest++;
                else other++;
            }
            if (mode != 0 && exact != perMode) bad++;
            Console.WriteLine($"      a {opNames[op]} b, {modeNames[mode],-9} : dirigé exact {exact,8:N0}   au plus près {nearest,8:N0}   autre {other,6:N0}   (inexacts {inexact:N0})");
        }

        var handler = Math.Max(1, n / 64);
        Console.WriteLine($"   (b) LE VRAI HANDLER, DC /r m64, oracle seul (fpu = 387, cœur 386) ; {handler:N0} tirages par case");
        int[] regField = [0, 4, 1, 6]; // DC /0 FADD, /4 FSUB, /1 FMUL, /6 FDIV (x87_ops.h:719-720)
        for (var op = 0; op < 4; op++)
        for (var mode = 0; mode < 4; mode++)
        {
            long exact = 0, nearest = 0, other = 0;
            string? ex = null;
            for (long i = 0; i < handler; i++)
            {
                double a = Finite(), b = Finite();
                var c = StepHandler(regField[op], mode, a, b);
                var r = Directed(op, mode, a, b, out _);
                if (B(c) == B(r)) exact++;
                else if (B(c) == B(Directed(op, 0, a, b, out _))) nearest++;
                else { other++; ex ??= $"a={a:R} b={b:R} : oracle {B(c):X16}, dirigé {B(r):X16}"; }
            }
            if (mode != 0 && exact != handler) bad++;
            Console.WriteLine($"      ST0 {opNames[op]} m64, {modeNames[mode],-9} : dirigé exact {exact,8:N0}   au plus près {nearest,8:N0}   autre {other,6:N0}" +
                              (ex is null ? "" : $"\n         ex. {ex}"));
        }
        Oracle.h_set_fpu(0);

        Console.WriteLine(bad == 0
            ? "\nParité complète : Math.*, l'aide de conversion et l'arrondi dirigé exact rendent les bits de l'oracle."
            : $"\n{bad} mesure(s) hors parité — voir ci-dessus. Ce sont des FAITS sur PCem compilé, pas des pannes de l'outil.");
        return 0;
    }

    /// <summary>G4.3 — QUEL NaN GAGNE. `addsd` et `mulsd` propagent le NaN de leur PREMIER
    /// opérande quand les deux en sont ; l'addition et la multiplication étant commutatives,
    /// GCC choisit l'ordre handler par handler (allocation de registres). L'oracle seul, deux
    /// NaN de charges distinctes : ST(0) = A, l'autre opérande (ST(1) ou la mémoire) = B ; rend
    /// « ST0 » ou « autre » pour chaque handler commutatif, et pour FADD mémoire en mode
    /// d'arrondi dirigé (l'autre chemin du C).</summary>
    public static int NanOrder()
    {
        Oracle.CheckAbi();
        const ulong A = 0x7FF8000000000AAAUL, B = 0x7FF8000000000BBBUL;
        const uint Bf = 0x7FC00BBBu;
        // (nom, octets, opérande mémoire m32 / m64 / aucun, RC)
        (string nom, byte[] code, int mem, int rc)[] cas =
        [
            ("opFADD   D8 C1", [0xD8, 0xC1], 0, 0), ("opFADDr  DC C1", [0xDC, 0xC1], 0, 0),
            ("opFADDP  DE C1", [0xDE, 0xC1], 0, 0), ("opFMUL   D8 C9", [0xD8, 0xC9], 0, 0),
            ("opFMULr  DC C9", [0xDC, 0xC9], 0, 0), ("opFMULP  DE C9", [0xDE, 0xC9], 0, 0),
            ("opFADDs  D8 /0", [0xD8, 0x06, 0x00, 0x01], 32, 0), ("opFADDd  DC /0", [0xDC, 0x06, 0x00, 0x01], 64, 0),
            ("opFMULs  D8 /1", [0xD8, 0x0E, 0x00, 0x01], 32, 0), ("opFMULd  DC /1", [0xDC, 0x0E, 0x00, 0x01], 64, 0),
            ("opFADDs  D8 /0, RC bas", [0xD8, 0x06, 0x00, 0x01], 32, 1), ("opFADDd  DC /0, RC bas", [0xDC, 0x06, 0x00, 0x01], 64, 1),
        ];
        foreach (var (nom, code, m, rc) in cas)
        {
            Oracle.h_set_fpu(FPU_387);
            Oracle.h_set_core(Oracle.Core386);
            Oracle.h_reset();
            Oracle.h_fill_ram(0x90);
            Array.Clear(Regs);
            Regs[(int)R.CS] = 0x2000;
            Regs[(int)R.DS] = 0x3000;
            Regs[(int)R.SS] = 0x4000;
            Regs[(int)R.SP] = 0xFFF0;
            var c = new byte[8];
            Array.Fill(c, (byte)0x90);
            Array.Copy(code, c, code.Length);
            Oracle.h_load(0x20000, c, 8);
            var op = new byte[8];
            if (m == 32) BitConverter.TryWriteBytes(op, Bf); else BitConverter.TryWriteBytes(op, B);
            Oracle.h_load(0x30100, op, 8);
            Oracle.h_setregs(Regs);
            Array.Clear(St);
            Array.Clear(Tag);
            St[0] = A;
            St[1] = B;
            Tag[0] = Tag[1] = 1;
            Oracle.h_setfpu(St, Mm, MmW4, Tag, 0, 0, (ushort)(0x033F | (rc << 10)));
            Oracle.h_step();
            Oracle.h_getstate(out var s);
            // Le résultat est dans ST(0) sauf pour les formes r et P, qui écrivent ST(1).
            var dest = nom.StartsWith("opFADDr") || nom.StartsWith("opFMULr") || nom.StartsWith("opFADDP") || nom.StartsWith("opFMULP") ? 1 : 0;
            var v = s.fpu_st[dest];
            var qui = (v & 0xFFF) == 0xAAA ? "ST0" : (v & 0xFFF) == 0xBBB || (v & 0x7FFFFFFFFFF) >> 29 == 0xBBB ? "autre" : $"? {v:X16}";
            Console.WriteLine($"  {nom,-26} gagnant : {qui,-6} ({v:X16})");
        }
        Oracle.h_set_fpu(0);
        return 0;
    }

    // Des finis normaux de plage modérée : l'erreur d'arrondi y est exacte par TwoSum/FMA,
    // et aucun résultat ne déborde ni ne devient dénormal.
    private static double Finite()
    {
        var m = 1.0 + U();
        var e = (int)(N() % 120) - 60;
        return ((N() & 1) != 0 ? -m : m) * Math.Pow(2, e);
    }

    /// <summary>a op b arrondi selon RC (npxc bits 10-11) : le résultat au plus près, corrigé
    /// d'un ulp selon le signe de l'erreur EXACTE — TwoSum pour + et -, FMA pour * et /.</summary>
    internal static double Directed(int op, int mode, double a, double b, out bool inexact)
    {
        double r, err;
        switch (op)
        {
            case 0: r = a + b; err = TwoSumErr(a, b, r); break;
            case 1: r = a - b; err = TwoSumErr(a, -b, r); break;
            case 2: r = a * b; err = Math.FusedMultiplyAdd(a, b, -r); break;
            default:
                r = a / b;
                // a - r*b exact ; l'erreur vraie a/b - r a le signe de (a - r*b) / b.
                err = Math.FusedMultiplyAdd(-r, b, a) * Math.Sign(b);
                break;
        }
        inexact = err != 0;
        if (err == 0 || mode == 0)
            return r;
        return mode switch
        {
            1 => err < 0 ? Math.BitDecrement(r) : r,
            2 => err > 0 ? Math.BitIncrement(r) : r,
            _ => (r > 0 && err < 0) ? Math.BitDecrement(r) : (r < 0 && err > 0) ? Math.BitIncrement(r) : r,
        };
    }

    private static double TwoSumErr(double a, double b, double s)
    {
        var bb = s - a;
        return (a - (s - bb)) + (b - bb);
    }

    private static readonly ulong[] St = new ulong[8], Mm = new ulong[8];
    private static readonly ushort[] MmW4 = new ushort[8];
    private static readonly byte[] Tag = new byte[8];
    private static readonly ushort[] Regs = new ushort[(int)R.COUNT];
    // DC /r, mod 00 rm 110 : [disp16], ici [0100h]. Puis des NOP.
    private static readonly byte[] Code = [0xDC, 0x06, 0x00, 0x01, 0x90, 0x90, 0x90, 0x90];
    private static readonly byte[] Operand = new byte[8];

    /// <summary>Un pas de l'ORACLE seul : ST0 = a, [DS:0100] = b en double, npxc tous masques
    /// posés et RC = mode, puis `DC /reg [0100h]`. Rend ST0.</summary>
    private static double StepHandler(int reg, int mode, double a, double b)
    {
        Oracle.h_set_fpu(FPU_387);
        Oracle.h_set_core(Oracle.Core386);
        Oracle.h_reset();
        Oracle.h_fill_ram(0x90);
        Array.Clear(Regs);
        Regs[(int)R.CS] = 0x2000;
        Regs[(int)R.DS] = 0x3000;
        Regs[(int)R.SS] = 0x4000;
        Regs[(int)R.SP] = 0xFFF0;
        Code[1] = (byte)(0x06 | (reg << 3));
        Oracle.h_load(0x20000, Code, (uint)Code.Length);
        BitConverter.TryWriteBytes(Operand, B(b));
        Oracle.h_load(0x30100, Operand, 8);
        Oracle.h_setregs(Regs);
        Array.Clear(St);
        Array.Clear(Tag);
        St[0] = B(a);
        Tag[0] = 1;
        Oracle.h_setfpu(St, Mm, MmW4, Tag, 0, 0, (ushort)(0x033F | (mode << 10)));
        Oracle.h_step();
        Oracle.h_getstate(out var s);
        return BitConverter.UInt64BitsToDouble(s.fpu_st[s.fpu_top & 7]);
    }
}
