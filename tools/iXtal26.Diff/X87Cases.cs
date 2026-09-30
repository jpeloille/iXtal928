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
    private const int FPU_287 = 2;
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

        // ---- G4.4 ----------------------------------------------------------------------
        double[] classes = [0.0, -0.0, 1.0, -1.0, double.PositiveInfinity, double.NegativeInfinity, double.NaN,
                            -double.NaN, Eps, -Eps, BitConverter.UInt64BitsToDouble(0x7FF4000000000001)];
        // FXAM (D9 E5), FTST (D9 E4), FCHS (D9 E0), FABS (D9 E1), FRNDINT (D9 FC) sur chaque classe,
        // tag VALID puis EMPTY (PB-63, PB-64).
        foreach (var v in classes)
        foreach (var tag in new byte[] { 1, 0 })
        foreach (var (nom, c) in new (string, byte[])[] { ("FXAM", [0xD9, 0xE5]), ("FTST", [0xD9, 0xE4]),
                                                          ("FCHS", [0xD9, 0xE0]), ("FABS", [0xD9, 0xE1]) })
        {
            n++;
            bad += Cas2($"{nom} {v:R} tag {tag}", c, [B(v)], [tag], 0, 0, 0x037F, 0x10, 1);
        }
        foreach (var v in new[] { 2.5, -2.5, 3.5, 0.5, -0.5, 1e300, -1e300, double.NaN, 9.3e18, -9.3e18 })
        for (var rc = 0; rc < 4; rc++)
        {
            n++;
            bad += Cas2($"FRNDINT {v:R} RC {rc}", [0xD9, 0xFC], [B(v)], [1], 0, 0, (ushort)(0x037F & ~0xC00 | rc << 10), 0x10, 1);
        }
        // FPREM (D9 F8), FPREM1 (D9 F5), FSCALE (D9 FD) sur des bornes (PB-65).
        (double, double)[] bornesPrem = [(10.0, 3.0), (-10.0, 3.0), (1e300, 1e-300), (5.0, 0.0), (double.PositiveInfinity, 2.0),
                                         (7.0, double.PositiveInfinity), (double.NaN, 1.0), (1e20, 3.0), (2.0, 1e20)];
        foreach (var (x, y) in bornesPrem)
        foreach (var (nom, c) in new (string, byte[])[] { ("FPREM", [0xD9, 0xF8]), ("FPREM1", [0xD9, 0xF5]), ("FSCALE", [0xD9, 0xFD]) })
        {
            n++;
            bad += Cas2($"{nom} {x:R}, {y:R}", c, [B(x), B(y)], [1, 1], 0, 0, 0x037F, 0x10, 1);
        }
        // FNSTSW AX (DF E0) avec TOP non nul (PB-61), et FSTSW m16 (DD 3E [0100]) en regard.
        for (var top = 0; top < 8; top += 3)
        {
            n++;
            bad += Cas2($"FNSTSW AX, TOP {top}", [0xDF, 0xE0], [B(1.0)], [1], 0, top, 0x037F, 0x10, 1, npxs: 0x3800);
            n++;
            bad += Cas2($"FSTSW m16, TOP {top}", [0xDD, 0x3E, 0x00, 0x01], [B(1.0)], [1], 0, top, 0x037F, 0x10, 1, npxs: 0x3800);
        }
        // Les sept constantes (D9 E8-EE) (PB-66), FNINIT (DB E3), FNCLEX (DB E2), FLD ST(1) (D9 C1),
        // FXCH ST(1) (D9 C9), FFREE ST(1) (DD C1), FDECSTP (D9 F6), FINCSTP (D9 F7).
        foreach (var m in new byte[] { 0xE8, 0xE9, 0xEA, 0xEB, 0xEC, 0xED, 0xEE, 0xC1, 0xC9, 0xF6, 0xF7 })
        {
            n++;
            bad += Cas2($"D9 {m:X2}", [0xD9, m], [B(1.0), B(2.0)], [1, 0x81], 0x1122334455667788, 0, 0x037F, 0x10, 1);
        }
        foreach (var (nom, c) in new (string, byte[])[] { ("FNINIT", [0xDB, 0xE3]), ("FNCLEX", [0xDB, 0xE2]), ("FFREE ST1", [0xDD, 0xC1]),
                                                          ("FST ST1", [0xDD, 0xD1]), ("FSTP ST1", [0xDD, 0xD9]) })
        {
            n++;
            bad += Cas2(nom, c, [B(1.0), B(2.0)], [0x81, 1], 0x1122334455667788, 0, 0x037F, 0x10, 1, npxs: 0xBFFF);
        }
        // FST ST(1) puis FISTP m64 [0100] de ST(1) (PB-67) : le tag UINT64 copié, pas MM[].q.
        n++;
        bad += Cas2("FST ST1 ; FINCSTP ; FISTP m64", [0xDD, 0xD1, 0xD9, 0xF7, 0xDF, 0x3E, 0x00, 0x01], [B(7.0), B(2.0)], [0x81, 1],
                    0x1122334455667788, 0, 0x037F, 0x10, 3);
        // FSAVE [0100] puis FRSTOR [0100] (DD /6, DD /4), 16 et 32 bits (préfixe 66), réel puis PE,
        // un registre TAG_UINT64, des zéros, un NaN : l'aller-retour entier.
        foreach (var cr0 in new uint[] { 0x10, 0x11 })
        foreach (var p66 in new[] { false, true })
        {
            byte[] c = p66 ? [0x66, 0xDD, 0x36, 0x00, 0x01, 0x66, 0xDD, 0x26, 0x00, 0x01] : [0xDD, 0x36, 0x00, 0x01, 0xDD, 0x26, 0x00, 0x01];
            n++;
            bad += Cas2($"FSAVE ; FRSTOR, cr0 {cr0:X2}, {(p66 ? 32 : 16)} bits", c,
                        [B(1.5), B(-0.0), B(double.NaN), B(1e300), B(Eps), B(3.0), B(-2.0), B(0.25)],
                        [0x81, 1, 1, 1, 1, 0, 1, 1], 0x8000000000000001, 3, 0x0B7F, cr0, 2, npxs: 0x4521);
            n++;
            bad += Cas2($"FSTENV ; FLDENV, cr0 {cr0:X2}, {(p66 ? 32 : 16)} bits",
                        p66 ? [0x66, 0xD9, 0x36, 0x00, 0x01, 0x66, 0xD9, 0x26, 0x00, 0x01] : [0xD9, 0x36, 0x00, 0x01, 0xD9, 0x26, 0x00, 0x01],
                        [B(1.5), B(2.5)], [1, 1], 0, 5, 0x0B7F, cr0, 2, npxs: 0x4521);
            n++;
            bad += Cas2($"FSTCW ; FLDCW, cr0 {cr0:X2}", [0xD9, 0x3E, 0x00, 0x01, 0xD9, 0x2E, 0x00, 0x02], [B(1.0)], [1], 0, 0, 0x0B7F, cr0, 2);
        }
        // #NM : CR0.EM (bit 2) ou CR0.TS (bit 3), un ESC de chaque table — INT 7 en mode réel.
        foreach (var cr0 in new uint[] { 0x14, 0x18, 0x1C })
        foreach (var c in new byte[][] { [0xD8, 0xC1], [0xD9, 0xE8], [0xDA, 0xE9], [0xDB, 0xE3], [0xDC, 0x06, 0x00, 0x01],
                                         [0xDD, 0x36, 0x00, 0x01], [0xDE, 0xD9], [0xDF, 0xE0] })
        {
            n++;
            bad += Cas2($"#NM cr0 {cr0:X2}, {c[0]:X2} {c[1]:X2}", c, [B(1.0), B(2.0)], [1, 1], 0, 0, 0x037F, cr0, 1);
        }

        // ---- G4.5 : les transcendantes aux bornes ----------------------------------------
        double[] bt = [0.0, -0.0, 0.5, -0.5, 1.0, -1.0, 2.0, Math.PI, -Math.PI, 1e20, -1e20, 9.3e18, 1e300,
                       double.PositiveInfinity, double.NegativeInfinity, double.NaN, Eps, -Eps, -3.0];
        (string, byte[])[] trans = [("F2XM1", [0xD9, 0xF0]), ("FYL2X", [0xD9, 0xF1]), ("FPTAN", [0xD9, 0xF2]),
                                    ("FPATAN", [0xD9, 0xF3]), ("FYL2XP1", [0xD9, 0xF9]), ("FSINCOS", [0xD9, 0xFB]),
                                    ("FSIN", [0xD9, 0xFE]), ("FCOS", [0xD9, 0xFF])];
        foreach (var (nom, c) in trans)
        foreach (var x in bt)
        foreach (var y in new[] { 1.0, -2.5, 0.0, double.NaN })
        {
            n++;
            bad += Cas2($"{nom} ST0 {x:R}, ST1 {y:R}", c, [B(x), B(y)], [1, 1], 0, 1, 0x037F, 0x10, 1, npxs: 0x0400);
        }

        // ---- G4.7 : le test de génération 287 / 387 par l'infini (PB-70) ---------------------
        // FNINIT ; FLD1 ; FLDZ ; FDIVP (+inf, ZE masqué) ; FLD ST ; FCHS ; FCOMPP ; FNSTSW [0100].
        // Sur le silicium, le 287 après FNINIT est en infini projectif : +inf = -inf, C3 posé.
        // PCem ne lit jamais le bit IC : C3 reste à zéro sur les deux — ce que MSD lit « 80387 ».
        foreach (var (nom, f) in new[] { ("287", FPU_287), ("387", FPU_387) })
        {
            n++;
            bad += Cas2($"infini projectif ou affine, {nom}",
                        [0xDB, 0xE3, 0xD9, 0xE8, 0xD9, 0xEE, 0xDE, 0xF9, 0xD9, 0xC0, 0xD9, 0xE0, 0xDE, 0xD9, 0xDD, 0x3E, 0x00, 0x01],
                        [], [], 0, 0, 0x037F, 0x10, 8, fpu: f);
        }

        Console.WriteLine(bad == 0 ? $"Vert : {n} cas dirigés, identiques à l'oracle."
                                   : $"\n{bad} cas divergents sur {n}.");
        return bad == 0 ? 0 : 1;
    }

    private static readonly ushort[] Regs = new ushort[(int)R.COUNT];
    private static readonly ulong[] St = new ulong[8], Mm = new ulong[8];
    private static readonly ushort[] MmW4 = new ushort[8];
    private static readonly byte[] Tag = new byte[8];

    /// <summary>G4.4 — un cas général : jusqu'à huit ST (dans l'ordre physique à partir de
    /// TOP), leurs tags, MM[TOP].q, TOP, npxs, npxc, CR0 ; `pas` instructions des deux côtés,
    /// l'état complet, le journal et les 112 octets en [DS:0100] comparés après chacune.</summary>
    private static int Cas2(string nom, byte[] code, ulong[] st, byte[] tags, ulong mm0, int top, ushort npxc, uint cr0,
                            int pas, ushort npxs = 0, int fpu = FPU_387)
    {
        var c = new byte[Math.Max(16, code.Length + 8)];
        Array.Fill(c, (byte)0x90);
        Array.Copy(code, c, code.Length);
        var zone = new byte[112];
        for (var k = 0; k < zone.Length; k++)
            zone[k] = (byte)(k * 37 + 11);
        Array.Clear(Regs);
        Regs[(int)R.CS] = 0x2000;
        Regs[(int)R.DS] = 0x3000;
        Regs[(int)R.SS] = 0x4000;
        Regs[(int)R.SP] = 0xFFF0;
        Array.Clear(St);
        Array.Clear(Tag);
        Array.Clear(Mm);
        for (var k = 0; k < st.Length; k++)
        {
            St[(top + k) & 7] = st[k];
            Tag[(top + k) & 7] = tags[k];
        }
        Mm[top & 7] = mm0;

        Oracle.h_set_fpu(fpu);
        _386.FuzzFpu = fpu;
        Oracle.h_set_core(Oracle.Core386);
        Oracle.h_reset();
        Oracle.h_fill_ram(0x90);
        _386.Reset386();
        mem.fill_ram(0x90);
        Oracle.h_load(0x20000, c, (uint)c.Length);
        Oracle.h_load(0x30100, zone, (uint)zone.Length);
        for (var k = 0; k < c.Length; k++)
            mem.ram[0x20000 + k] = c[k];
        for (var k = 0; k < zone.Length; k++)
            mem.ram[0x30100 + k] = zone[k];
        Oracle.h_setregs(Regs);
        _808x.SetRegs(Regs);
        Oracle.h_setfpu(St, Mm, MmW4, Tag, top, npxs, npxc);
        _808x.SetFpu(St, Mm, MmW4, Tag, top, npxs, npxc);
        Oracle.h_setsys386(cr0, 0, 0, 0);
        x86.cr0 = cr0;

        string? diff = null;
        for (var p = 0; p < pas && diff is null; p++)
        {
            Oracle.h_wlog_reset();
            mem.wlog_reset();
            var cycC = Oracle.h_step();
            var cycS = _386.Step286();
            Oracle.h_getstate(out var a);
            var b = HState.Create();
            _808x.GetState(ref b);
            diff = Fuzzer.CompareStates(a, b, cycC, cycS) ?? Fuzzer.CmpWrites();
            if (diff is null)
            {
                var o = new byte[112];
                Oracle.h_read(0x30100, o, 112);
                for (var k = 0; k < 112 && diff is null; k++)
                    if (o[k] != mem.ram[0x30100 + k])
                        diff = $"[DS:{0x100 + k:X4}] : oracle 0x{o[k]:X2}, C# 0x{mem.ram[0x30100 + k]:X2}";
            }
            if (diff is not null)
                diff = $"pas {p} — {diff}";
        }
        Oracle.h_set_fpu(0);
        _386.FuzzFpu = 0;
        if (diff is null)
            return 0;
        Console.WriteLine($"  {nom} : {diff}");
        return 1;
    }

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
