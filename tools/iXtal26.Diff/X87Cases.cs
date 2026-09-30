// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// G4.3 — LES CAS DIRIGÉS DU x87 : ce que le fuzzeur ne tire presque jamais. Chaque cas pose
// un état x87 construit et une instruction, l'exécute des DEUX côtés (387, cœur 386, mode
// réel), et compare l'état complet, le journal d'écritures et la RAM à l'adresse effective.
//
// Les zéros signés sous FCOMPP (PB-58), les NaN sous FCOM registre (PB-57), la division par
// zéro masquée et démasquée (PB-59), et surtout les BORDS de l'arrondi dirigé de FADD mémoire
// (PB-48) : somme exactement nulle en mode vers le bas, débordement dans les quatre modes,
// dénormaux, infinis — là où x87_fadd_dirige ne peut pas se fier au signe de l'erreur.

using iXtal26.Cpu;
using iXtal26.Diag;
using iXtal26.Memory;

namespace iXtal26.Diff;

internal static class X87Cases
{
    private const int FPU_387 = 4;

    private static ulong B(double d) => BitConverter.DoubleToUInt64Bits(d);

    public static int Run()
    {
        Oracle.CheckAbi();
        var bad = 0;
        var n = 0;
        const double Max = double.MaxValue, Eps = 4.9406564584124654E-324;
        double[] bornes = [0.0, -0.0, 1.0, -1.0, Max, -Max, double.PositiveInfinity, double.NegativeInfinity,
                           Eps, -Eps, 2.2250738585072014E-308, -2.2250738585072014E-308, 1e308, -1e308,
                           double.NaN, BitConverter.UInt64BitsToDouble(0x7FF4000000000001), 0.5, 3.0];

        // FADD m64 (DC /0) dans les quatre modes, sur toutes les paires de bornes : zéros
        // exacts, débordements, dénormaux, infinis, NaN silencieux et signalants.
        foreach (var a in bornes)
        foreach (var b in bornes)
        for (var rc = 0; rc < 4; rc++)
        {
            n++;
            bad += Cas($"FADD m64 {a:R} + {b:R}, RC {rc}", [0xDC, 0x06, 0x00, 0x01], B(a), 0, B(b), 64, rc, 0x3F);
        }
        // FADD m32 (D8 /0) : l'opérande float, et les mêmes bords.
        float[] bf = [0f, -0f, 1f, float.MaxValue, float.PositiveInfinity, float.NaN, 1e-45f];
        foreach (var a in bornes)
        foreach (var b in bf)
        for (var rc = 0; rc < 4; rc++)
        {
            n++;
            bad += Cas($"FADD m32 {a:R} + {b:R}f, RC {rc}", [0xD8, 0x06, 0x00, 0x01], B(a), 0,
                       BitConverter.SingleToUInt32Bits(b), 32, rc, 0x3F);
        }
        // FCOMPP (DE D9) : le « hack » de PB-58 et ses voisins.
        (double, double)[] paires = [(-0.0, 0.0), (0.0, -0.0), (-0.0, -0.0), (0.0, 0.0), (double.NaN, 1.0), (1.0, 2.0), (2.0, 1.0)];
        foreach (var (s0, s1) in paires)
        {
            n++;
            bad += Cas($"FCOMPP ST0 {s0:R}, ST1 {s1:R}", [0xDE, 0xD9], B(s0), B(s1), 0, 0, 0, 0x3F);
            n++;
            bad += Cas($"FCOM ST1 ST0 {s0:R}, ST1 {s1:R}", [0xD8, 0xD1], B(s0), B(s1), 0, 0, 0, 0x3F);
            n++;
            bad += Cas($"FUCOMPP ST0 {s0:R}, ST1 {s1:R}", [0xDA, 0xE9], B(s0), B(s1), 0, 0, 0, 0x3F);
        }
        // FDIV ST0, ST1 (D8 F1) par zéro, ZE masquée puis démasquée (PB-59).
        foreach (var zm in new[] { 0x3F, 0x3B })
        foreach (var z in new[] { 0.0, -0.0 })
        {
            n++;
            bad += Cas($"FDIV 1 / {z:R}, npxc {zm:X2}", [0xD8, 0xF1], B(1.0), B(z), 0, 0, 0, zm);
        }

        Console.WriteLine(bad == 0 ? $"Vert : {n} cas dirigés, identiques à l'oracle."
                                   : $"\n{bad} cas divergents sur {n}.");
        return bad == 0 ? 0 : 1;
    }

    private static readonly ushort[] Regs = new ushort[(int)R.COUNT];
    private static readonly ulong[] St = new ulong[8], Mm = new ulong[8];
    private static readonly ushort[] MmW4 = new ushort[8];
    private static readonly byte[] Tag = new byte[8];

    /// <summary>Un cas : ST0, ST1, un opérande mémoire (32 ou 64 bits, en [DS:0100]), le mode
    /// d'arrondi et les masques d'exception de npxc ; une instruction, un pas des deux côtés.</summary>
    private static int Cas(string nom, byte[] code, ulong st0, ulong st1, ulong operande, int memBits, int rc, int masques)
    {
        var c = new byte[8];
        Array.Fill(c, (byte)0x90);
        Array.Copy(code, c, code.Length);
        var op = new byte[8];
        if (memBits == 32) BitConverter.TryWriteBytes(op, (uint)operande); else BitConverter.TryWriteBytes(op, operande);
        Array.Clear(Regs);
        Regs[(int)R.CS] = 0x2000;
        Regs[(int)R.DS] = 0x3000;
        Regs[(int)R.SS] = 0x4000;
        Regs[(int)R.SP] = 0xFFF0;
        Array.Clear(St);
        Array.Clear(Tag);
        St[0] = st0;
        St[1] = st1;
        Tag[0] = Tag[1] = 1;
        var npxc = (ushort)(0x0300 | masques | (rc << 10));

        Oracle.h_set_fpu(FPU_387);
        _386.FuzzFpu = FPU_387;
        Oracle.h_set_core(Oracle.Core386);
        Oracle.h_reset();
        Oracle.h_fill_ram(0x90);
        _386.Reset386();
        mem.fill_ram(0x90);
        Oracle.h_load(0x20000, c, 8);
        Oracle.h_load(0x30100, op, 8);
        for (var k = 0; k < 8; k++)
        {
            mem.ram[0x20000 + k] = c[k];
            mem.ram[0x30100 + k] = op[k];
        }
        Oracle.h_setregs(Regs);
        _808x.SetRegs(Regs);
        Oracle.h_setfpu(St, Mm, MmW4, Tag, 0, 0, npxc);
        _808x.SetFpu(St, Mm, MmW4, Tag, 0, 0, npxc);
        Oracle.h_wlog_reset();
        mem.wlog_reset();

        var cycC = Oracle.h_step();
        var cycS = _386.Step286();
        Oracle.h_getstate(out var a);
        var b = HState.Create();
        _808x.GetState(ref b);
        var diff = Fuzzer.CompareStates(a, b, cycC, cycS) ?? Fuzzer.CmpWrites();
        Oracle.h_set_fpu(0);
        _386.FuzzFpu = 0;
        if (diff is null)
            return 0;
        Console.WriteLine($"  {nom} : {diff}");
        return 1;
    }
}
