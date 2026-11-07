// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — outillage de mesure.
//
// LES CAS DU CŒUR (G15.0a ; l'en-tête d'InterditsBanc.cs dit les colonnes, l'équivalence et le mode) :
//   3. l'appel direct contre une interface et un `override` : l'ADD, l'OR et le SUB r/m8, r8 d'OP_ARITH ;
//   4. une fermeture contre une méthode nommée : le JE court ;
//   6. le tas contre stackalloc : le descripteur que loadseg lit en mode protégé.
// Les copies sont les gestionnaires de Cpu/, ligne pour ligne, appelés par un délégué OpFn comme exec386 les appelle
// (cas 3 et 4) ; elles portent le nom du gestionnaire qu'elles copient.

using BenchmarkDotNet.Attributes;
using iXtal26.Cpu;
using static iXtal26.Cpu._386;
using static iXtal26.Cpu._386_common;
using static iXtal26.Cpu.x86;
using static iXtal26.Cpu.x86_flags;
using static iXtal26.Cpu.x86seg_c;

using int8_t = System.SByte;

namespace iXtal26.PerfBanc;

// ---- 3. OP_ARITH : un appel direct contre une interface et un `override` ----

/// <summary>Cas 3. Trois instructions par tour, ADD AL,CL, OR AL,CL et SUB AL,CL (00h, 08h, 28h, ModRM C8h), sur
/// l'ami286. Le vrai gestionnaire appelle l'opération par sa structure, un corps par famille
/// (OP_ARITH&lt;TAlu&gt;, depuis G15.P) ; Delegue est la copie de celui d'avant : une seule fabrique pour les trois
/// familles, une fermeture qui appelait deux délégués (op8 et setflags8), dont le site d'appel voyait trois opérations.
/// Retenue et AA appellent l'opération par une structure (un appel direct, lié par le JIT, sans délégué, comme la
/// macro de PCem le développe) ; Interface et Virtuel, par une interface et par une classe abstraite, une classe par
/// famille (trois implémentations au même site : le JIT ne peut pas le dévirtualiser).</summary>
public class Interdit3Interface
{
    private OpFn[] reel = null!, retenue = null!, aa = null!, delegue = null!, iface = null!, virtuel = null!;

    [GlobalSetup]
    public void Preparer()
    {
        reel = Interdit3.Preparer();
        retenue = [Interdit3.opARITH_b_rmw_a16<AluAdd, CopieA>, Interdit3.opARITH_b_rmw_a16<AluOr, CopieA>,
                   Interdit3.opARITH_b_rmw_a16<AluSub, CopieA>];
        aa = [Interdit3.opARITH_b_rmw_a16<AluAdd, CopieB>, Interdit3.opARITH_b_rmw_a16<AluOr, CopieB>,
              Interdit3.opARITH_b_rmw_a16<AluSub, CopieB>];
        delegue = Interdit3.Delegues();
        iface = [Interdit3.Interface(new AluAddI()), Interdit3.Interface(new AluOrI()),
                 Interdit3.Interface(new AluSubI())];
        virtuel = [Interdit3.Virtuel(new AluAddV()), Interdit3.Virtuel(new AluOrV()), Interdit3.Virtuel(new AluSubV())];
        Interdit3.VerifierEquivalence(reel, [retenue, aa, delegue, iface, virtuel],
                                      ["Retenue", "AA", "Delegue", "Interface", "Virtuel"]);
    }

    [Benchmark(OperationsPerInvoke = Interdit3.Appels)]
    public int Reel() => Interdit3.Boucle(reel);

    [Benchmark(Baseline = true, OperationsPerInvoke = Interdit3.Appels)]
    public int Retenue() => Interdit3.Boucle(retenue);

    [Benchmark(OperationsPerInvoke = Interdit3.Appels)]
    public int AA() => Interdit3.Boucle(aa);

    [Benchmark(OperationsPerInvoke = Interdit3.Appels)]
    public int Delegue() => Interdit3.Boucle(delegue);

    [Benchmark(OperationsPerInvoke = Interdit3.Appels)]
    public int Interface() => Interdit3.Boucle(iface);

    [Benchmark(OperationsPerInvoke = Interdit3.Appels)]
    public int Virtuel() => Interdit3.Boucle(virtuel);
}

/// <summary>La forme retenue : l'opération d'une famille, liée à la compilation.</summary>
internal interface IAluStatique
{
    static abstract uint8_t Op(uint8_t dst, uint8_t src);
    static abstract void Drapeaux(uint8_t dst, uint8_t src);
}

// Les trois opérations, telles que PoserGroupeArith les passait à OP_ARITH avant G15.P (Cpu/386_ops_arith.cs:257-310 à
// 61a85d9) ; le produit les a depuis dans alu_ADD, alu_OR et alu_SUB.
internal struct AluAdd : IAluStatique
{
    public static uint8_t Op(uint8_t dst, uint8_t src) => (uint8_t)(dst + src);
    public static void Drapeaux(uint8_t dst, uint8_t src) => setadd8(dst, src);
}

internal struct AluOr : IAluStatique
{
    public static uint8_t Op(uint8_t dst, uint8_t src) => (uint8_t)(dst | src);
    public static void Drapeaux(uint8_t dst, uint8_t src) => setznp8((uint8_t)(dst | src));
}

internal struct AluSub : IAluStatique
{
    public static uint8_t Op(uint8_t dst, uint8_t src) => (uint8_t)(dst - src);
    public static void Drapeaux(uint8_t dst, uint8_t src) => setsub8(dst, src);
}

/// <summary>La forme interdite : une interface.</summary>
internal interface IAlu
{
    uint8_t Op(uint8_t dst, uint8_t src);
    void Drapeaux(uint8_t dst, uint8_t src);
}

internal sealed class AluAddI : IAlu
{
    public uint8_t Op(uint8_t dst, uint8_t src) => (uint8_t)(dst + src);
    public void Drapeaux(uint8_t dst, uint8_t src) => setadd8(dst, src);
}

internal sealed class AluOrI : IAlu
{
    public uint8_t Op(uint8_t dst, uint8_t src) => (uint8_t)(dst | src);
    public void Drapeaux(uint8_t dst, uint8_t src) => setznp8((uint8_t)(dst | src));
}

internal sealed class AluSubI : IAlu
{
    public uint8_t Op(uint8_t dst, uint8_t src) => (uint8_t)(dst - src);
    public void Drapeaux(uint8_t dst, uint8_t src) => setsub8(dst, src);
}

/// <summary>La forme interdite : une classe abstraite et ses `override`.</summary>
internal abstract class AluBase
{
    internal abstract uint8_t Op(uint8_t dst, uint8_t src);
    internal abstract void Drapeaux(uint8_t dst, uint8_t src);
}

internal sealed class AluAddV : AluBase
{
    internal override uint8_t Op(uint8_t dst, uint8_t src) => (uint8_t)(dst + src);
    internal override void Drapeaux(uint8_t dst, uint8_t src) => setadd8(dst, src);
}

internal sealed class AluOrV : AluBase
{
    internal override uint8_t Op(uint8_t dst, uint8_t src) => (uint8_t)(dst | src);
    internal override void Drapeaux(uint8_t dst, uint8_t src) => setznp8((uint8_t)(dst | src));
}

internal sealed class AluSubV : AluBase
{
    internal override uint8_t Op(uint8_t dst, uint8_t src) => (uint8_t)(dst - src);
    internal override void Drapeaux(uint8_t dst, uint8_t src) => setsub8(dst, src);
}

internal static class Interdit3
{
    internal const int Appels = 3000;

    /// <summary>ModRM C8h : mod 3, reg 1 (CL), r/m 0 (AL).</summary>
    private const uint32_t Modrm = 0xc8;

    internal static OpFn[] Preparer()
    {
        Reference.Demander(Interdits.Mode, "Interdit3Interface");
        Reset286();
        Reference.VerifierMode(Interdits.Mode, "Interdit3Interface");
        OpFn[] reel = [ops_286[0x00], ops_286[0x08], ops_286[0x28]];
        foreach (var h in reel)
            if (!h.Method.Name.StartsWith("<OP_ARITH>"))
                throw new InvalidOperationException($"Interdit3Interface : {h.Method.Name} n'est pas d'OP_ARITH");
        return reel;
    }

    internal static OpFn[] Delegues() =>
    [
        Fabrique((dst, src) => (uint8_t)(dst + src), setadd8, false),
        Fabrique((dst, src) => (uint8_t)(dst | src), (dst, src) => setznp8((uint8_t)(dst | src)), false),
        Fabrique((dst, src) => (uint8_t)(dst - src), setsub8, false),
    ];

    internal static void VerifierEquivalence(OpFn[] reel, OpFn[][] colonnes, string[] noms)
    {
        var attendu = Releve(reel);
        for (var c = 0; c < colonnes.Length; c++)
        {
            var obtenu = Releve(colonnes[c]);
            if (obtenu != attendu)
                Interdits.Refuser("Interdit3Interface", noms[c], attendu, obtenu);
        }
        Interdits.Imprimer("Interdit3Interface", $"ami286, ADD, OR et SUB AL,CL, 256 × 256 opérandes par colonne, " +
                           $"{attendu} dans les six");
    }

    /// <summary>Chaque couple (AL, CL), chacune des trois instructions : ce qu'elles laissent, résumé.</summary>
    private static string Releve(OpFn[] h)
    {
        var e = 0xcbf29ce484222325UL;
        void Melanger(ulong v) => e = (e ^ v) * 0x100000001b3UL;
        for (var a = 0; a < 256; a++)
            for (var b = 0; b < 256; b++)
                for (var k = 0; k < 3; k++)
                {
                    cpu_state.regs[0].b.l = (uint8_t)a;
                    cpu_state.regs[1].b.l = (uint8_t)b;
                    cpu_state.pc = 0;
                    cycles = 0;
                    prefetch_bytes = 0;
                    var r = h[k](Modrm);
                    Melanger((ulong)(uint)r | (ulong)cpu_state.regs[0].b.l << 8 | (ulong)cpu_state.regs[1].b.l << 16);
                    Melanger((ulong)(uint)cpu_state.flags_op | (ulong)cpu_state.flags_res << 8);
                    Melanger(cpu_state.flags_op1 | (ulong)cpu_state.flags_op2 << 32);
                    Melanger((ulong)(uint)cycles | (ulong)(uint)prefetch_bytes << 32 | (ulong)cpu_state.pc << 48);
                }
        return $"{e:X16}";
    }

    /// <summary>Trois instructions par tour ; AL court, CL reste.</summary>
    internal static int Boucle(OpFn[] h)
    {
        OpFn add = h[0], or = h[1], sub = h[2];
        cpu_state.pc = 0;
        cycles = 0;
        cpu_state.regs[0].b.l = 0x5a;
        cpu_state.regs[1].b.l = 0x3c;
        var r = 0;
        for (var i = 0; i < Appels / 3; i++)
        {
            r += add(Modrm);
            r += or(Modrm);
            r += sub(Modrm);
        }
        return r + cpu_state.regs[0].b.l;
    }

    // Cpu/386_ops_arith.cs:96-133 à 61a85d9, op##name##_b_rmw_a16, ligne pour ligne, gettempc faux (ADD, OR, SUB) et
    // l'opération liée par la structure : la forme que le produit a prise en G15.P.
    internal static int opARITH_b_rmw_a16<TOp, T>(uint32_t fetchdat) where TOp : struct, IAluStatique
                                                                    where T : struct
    {
        if (fetch_ea_16(fetchdat)) return 1;
        if (cpu_mod == 3)
        {
                uint8_t dst = getr8(cpu_rm);
                uint8_t src = getr8(cpu_reg);
                TOp.Drapeaux(dst, src);
                setr8(cpu_rm, TOp.Op(dst, src));
                CLOCK_CYCLES(cpu_c.timing_rr);
                PREFETCH_RUN(cpu_c.timing_rr, 2, (int)fetchdat, 0, 0, 0, 0, 0);
        }
        else
        {
                uint8_t dst;
                if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
                dst = geteab();
                if (cpu_state.abrt != 0)
                        return 1;
                uint8_t src = getr8(cpu_reg);
                seteab(TOp.Op(dst, src));
                if (cpu_state.abrt != 0)
                        return 1;
                TOp.Drapeaux(dst, src);
                CLOCK_CYCLES(cpu_c.timing_mr);
                PREFETCH_RUN(cpu_c.timing_mr, 2, (int)fetchdat, 1, 0, 1, 0, 0);
        }
        return 0;
    }

    // Le vrai jusqu'à G15.P : la fabrique d'OP_ARITH (Cpu/386_ops_arith.cs:96-133 à 61a85d9), ligne pour ligne, et
    // ses deux délégués (:77 et :79), que le produit n'a plus.
    internal delegate uint8_t ArithOp8(uint8_t dst, uint8_t src);
    internal delegate void ArithFlags8(uint8_t dst, uint8_t src);

    private static OpFn Fabrique(ArithOp8 op8, ArithFlags8 setflags8, bool gettempc) => fetchdat =>
    {
        if (gettempc)
                tempc = CF_SET() != 0 ? 1 : 0;
        if (fetch_ea_16(fetchdat)) return 1;
        if (cpu_mod == 3)
        {
                uint8_t dst = getr8(cpu_rm);
                uint8_t src = getr8(cpu_reg);
                setflags8(dst, src);
                setr8(cpu_rm, op8(dst, src));
                CLOCK_CYCLES(cpu_c.timing_rr);
                PREFETCH_RUN(cpu_c.timing_rr, 2, (int)fetchdat, 0, 0, 0, 0, 0);
        }
        else
        {
                uint8_t dst;
                if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
                dst = geteab();
                if (cpu_state.abrt != 0)
                        return 1;
                uint8_t src = getr8(cpu_reg);
                seteab(op8(dst, src));
                if (cpu_state.abrt != 0)
                        return 1;
                setflags8(dst, src);
                CLOCK_CYCLES(cpu_c.timing_mr);
                PREFETCH_RUN(cpu_c.timing_mr, 2, (int)fetchdat, 1, 0, 1, 0, 0);
        }
        return 0;
    };

    // La même fabrique, l'opération par une interface ; gettempc faux pour les trois familles, son test omis comme
    // dans la retenue.
    internal static OpFn Interface(IAlu alu) => fetchdat =>
    {
        if (fetch_ea_16(fetchdat)) return 1;
        if (cpu_mod == 3)
        {
                uint8_t dst = getr8(cpu_rm);
                uint8_t src = getr8(cpu_reg);
                alu.Drapeaux(dst, src);
                setr8(cpu_rm, alu.Op(dst, src));
                CLOCK_CYCLES(cpu_c.timing_rr);
                PREFETCH_RUN(cpu_c.timing_rr, 2, (int)fetchdat, 0, 0, 0, 0, 0);
        }
        else
        {
                uint8_t dst;
                if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
                dst = geteab();
                if (cpu_state.abrt != 0)
                        return 1;
                uint8_t src = getr8(cpu_reg);
                seteab(alu.Op(dst, src));
                if (cpu_state.abrt != 0)
                        return 1;
                alu.Drapeaux(dst, src);
                CLOCK_CYCLES(cpu_c.timing_mr);
                PREFETCH_RUN(cpu_c.timing_mr, 2, (int)fetchdat, 1, 0, 1, 0, 0);
        }
        return 0;
    };

    // La même fabrique, l'opération par une méthode virtuelle ; le test de gettempc omis de même.
    internal static OpFn Virtuel(AluBase alu) => fetchdat =>
    {
        if (fetch_ea_16(fetchdat)) return 1;
        if (cpu_mod == 3)
        {
                uint8_t dst = getr8(cpu_rm);
                uint8_t src = getr8(cpu_reg);
                alu.Drapeaux(dst, src);
                setr8(cpu_rm, alu.Op(dst, src));
                CLOCK_CYCLES(cpu_c.timing_rr);
                PREFETCH_RUN(cpu_c.timing_rr, 2, (int)fetchdat, 0, 0, 0, 0, 0);
        }
        else
        {
                uint8_t dst;
                if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
                dst = geteab();
                if (cpu_state.abrt != 0)
                        return 1;
                uint8_t src = getr8(cpu_reg);
                seteab(alu.Op(dst, src));
                if (cpu_state.abrt != 0)
                        return 1;
                alu.Drapeaux(dst, src);
                CLOCK_CYCLES(cpu_c.timing_mr);
                PREFETCH_RUN(cpu_c.timing_mr, 2, (int)fetchdat, 1, 0, 1, 0, 0);
        }
        return 0;
    };
}

// ---- 4. Le Jcc : une fermeture contre une méthode nommée ----

/// <summary>Cas 4. JE court (74h) sur l'ami286, pris une fois sur deux (ZF posé par un setznp8 de 0 ou de 1). Le
/// vrai est une fermeture qui appelle sa condition par un délégué capturé (CondFn) : c'est la forme interdite, et
/// Interdite en est la copie. Retenue et AA sont la méthode nommée de PCem, opJE, sa condition écrite en
/// ligne ; dans la table, elle serait un délégué de méthode statique, comme elle l'est ici, quand le vrai et sa copie
/// sont des délégués de fermeture : la colonne compte la fermeture et l'appel de sa condition.</summary>
public class Interdit4Fermeture
{
    private OpFn reel = null!, retenue = null!, aa = null!, interdite = null!;

    [GlobalSetup]
    public void Preparer()
    {
        reel = Interdit4.Preparer();
        retenue = Interdit4.opJE<CopieA>;
        aa = Interdit4.opJE<CopieB>;
        interdite = Interdit4.Fabrique(() => ZF_SET() != 0);
        Interdit4.VerifierEquivalence(reel, [retenue, aa, interdite]);
    }

    [Benchmark(OperationsPerInvoke = Interdit4.Appels)]
    public int Reel() => Interdit4.Boucle(reel);

    [Benchmark(Baseline = true, OperationsPerInvoke = Interdit4.Appels)]
    public int Retenue() => Interdit4.Boucle(retenue);

    [Benchmark(OperationsPerInvoke = Interdit4.Appels)]
    public int AA() => Interdit4.Boucle(aa);

    [Benchmark(OperationsPerInvoke = Interdit4.Appels)]
    public int Interdite() => Interdit4.Boucle(interdite);
}

internal static class Interdit4
{
    internal const int Appels = 4096;

    internal static OpFn Preparer()
    {
        Reference.Demander(Interdits.Mode, "Interdit4Fermeture");
        Reset286();
        Reference.VerifierMode(Interdits.Mode, "Interdit4Fermeture");
        var reel = ops_286[0x74];
        if (!reel.Method.Name.StartsWith("<PoserSautsConditionnels>"))
            throw new InvalidOperationException($"Interdit4Fermeture : {reel.Method.Name} n'est pas le Jcc");
        return reel;
    }

    internal static void VerifierEquivalence(OpFn reel, OpFn[] copies)
    {
        var attendu = Releve(reel);
        string[] noms = ["Retenue", "AA", "Interdite"];
        for (var c = 0; c < copies.Length; c++)
        {
            var obtenu = Releve(copies[c]);
            if (obtenu != attendu)
                Interdits.Refuser("Interdit4Fermeture", noms[c], attendu, obtenu);
        }
        Interdits.Imprimer("Interdit4Fermeture", $"ami286, JE pris et non pris, 256 déplacements, 16 bits et 32, " +
                           $"{attendu} dans les quatre");
    }

    private static string Releve(OpFn h)
    {
        var e = 0xcbf29ce484222325UL;
        for (var op32 = 0; op32 <= 0x100; op32 += 0x100)
            for (var d = 0; d < 256; d++)
                for (var z = 0; z < 2; z++)
                {
                    cpu_state.op32 = (uint32_t)op32;
                    cpu_state.pc = 0xfff0;
                    cycles = 0;
                    prefetch_bytes = 0;
                    cpu_block_end = 0;
                    setznp8((uint8_t)z);
                    var r = h((uint32_t)d);
                    e = (e ^ ((ulong)(uint)r | (ulong)cpu_state.pc << 8 | (ulong)(uint)cycles << 40)) * 0x100000001b3UL;
                    e = (e ^ ((ulong)(uint)prefetch_bytes | (ulong)(uint)cpu_block_end << 32)) * 0x100000001b3UL;
                }
        cpu_state.op32 = 0;
        return $"{e:X16}";
    }

    internal static int Boucle(OpFn h)
    {
        cpu_state.pc = 0;
        cycles = 0;
        var r = 0;
        for (var i = 0; i < Appels; i++)
        {
            setznp8((uint8_t)(i & 1));
            r += h((uint32_t)(i & 0x7f));
        }
        return r + cycles;
    }

    // Cpu/386_ops_jump.cs:83-100, le corps de la fermeture, ligne pour ligne, en méthode nommée, la condition de JE
    // (cond_E, :60) écrite en ligne.
    internal static int opJE<T>(uint32_t fetchdat) where T : struct
    {
        int8_t offset = (int8_t)(uint8_t)fetchdat; cpu_state.pc++;   // (int8_t)getbytef()
        CLOCK_CYCLES(cpu_c.timing_bnt);
        if (ZF_SET() != 0)
        {
                cpu_state.pc += (uint32_t)offset;
                if ((cpu_state.op32 & 0x100) == 0)
                        cpu_state.pc &= 0xffff;
                CLOCK_CYCLES_ALWAYS(cpu_c.timing_bt);
                CPU_BLOCK_END();
                PREFETCH_RUN(cpu_c.timing_bt + cpu_c.timing_bnt, 2, -1, 0, 0, 0, 0, 0);
                PREFETCH_FLUSH();
                return 1;
        }
        PREFETCH_RUN(cpu_c.timing_bnt, 2, -1, 0, 0, 0, 0, 0);
        return 0;
    }

    // Le vrai : la fermeture de PoserSautsConditionnels (Cpu/386_ops_jump.cs:83-100), ligne pour ligne.
    internal static OpFn Fabrique(CondFn cond) => fetchdat =>
    {
        int8_t offset = (int8_t)(uint8_t)fetchdat; cpu_state.pc++;   // (int8_t)getbytef()
        CLOCK_CYCLES(cpu_c.timing_bnt);
        if (cond())
        {
                cpu_state.pc += (uint32_t)offset;
                if ((cpu_state.op32 & 0x100) == 0)
                        cpu_state.pc &= 0xffff;
                CLOCK_CYCLES_ALWAYS(cpu_c.timing_bt);
                CPU_BLOCK_END();
                PREFETCH_RUN(cpu_c.timing_bt + cpu_c.timing_bnt, 2, -1, 0, 0, 0, 0, 0);
                PREFETCH_FLUSH();
                return 1;
        }
        PREFETCH_RUN(cpu_c.timing_bnt, 2, -1, 0, 0, 0, 0, 0);
        return 0;
    };
}

// ---- 6. loadseg : le tas contre stackalloc ----

/// <summary>Cas 6. MOV ES, 0Bh en mode protégé, sur l'ami386 : loadseg lit le descripteur 8 de la GDT (un segment
/// de données de DPL 3, présent, en 20000h) dans un tableau qu'il alloue à chaque appel, puis le recopie dans ES par
/// do_seg_load. Le vrai est la forme interdite, et Interdite en est la copie ; Retenue et AA lisent le descripteur dans
/// un Span de stackalloc, que leur do_seg_load reçoit (la signature de do_seg_load et de ses appelants changerait dans
/// le produit : seule la copie la porte).</summary>
public class Interdit6Allocation
{
    [GlobalSetup]
    public void Preparer() => Interdit6.Preparer();

    [Benchmark(OperationsPerInvoke = Interdit6.Appels)]
    public int Reel() => Interdit6.BoucleReel();

    [Benchmark(Baseline = true, OperationsPerInvoke = Interdit6.Appels)]
    public int Retenue() => Interdit6.BouclePile<CopieA>();

    [Benchmark(OperationsPerInvoke = Interdit6.Appels)]
    public int AA() => Interdit6.BouclePile<CopieB>();

    [Benchmark(OperationsPerInvoke = Interdit6.Appels)]
    public int Interdite() => Interdit6.BoucleTas();
}

internal static class Interdit6
{
    internal const int Appels = 1024;

    private const uint32_t Gdt = 0x1000;
    private const uint16_t Selecteur = 0x0b;

    /// <summary>Le mode protégé à la main : PE dans MSW, une GDT de trois entrées en 1000h, le descripteur 8 (base
    /// 20000h, limite FFFFh, accès F2h : présent, DPL 3, données en lecture et écriture) ; CS reste celui du mode
    /// réel (DPL 3, CPL 3).</summary>
    internal static void Preparer()
    {
        Reference.Demander(Interdits.Mode, "Interdit6Allocation");
        FuzzCpu = 0;
        Reset386();
        Reference.VerifierMode(Interdits.Mode, "Interdit6Allocation");
        Descripteur();
        gdt.@base = Gdt;
        gdt.limit = 0x17;
        msw |= 1;
        cpu_state.eflags = 0;
        Func<int>[] colonnes = [BoucleReel, BouclePile<CopieA>, BouclePile<CopieB>, BoucleTas];
        string[] noms = ["Reel", "Retenue", "AA", "Interdite"];
        string? attendu = null;
        for (var c = 0; c < colonnes.Length; c++)
        {
            Descripteur();
            cpu_state.seg_es.seg = 0;
            cpu_state.seg_es.@base = 0;
            cpu_state.abrt = 0;
            var r = colonnes[c]();
            var es = cpu_state.seg_es;
            var obtenu = $"{r}, ES {es.seg:X4} base {es.@base:X8} limite {es.limit:X8} [{es.limit_low:X8}, " +
                         $"{es.limit_high:X8}] accès {es.access:X2}/{es.access2:X2} vérifié {es.@checked}, l'accès " +
                         $"en table {readmemw(0, Gdt + 0xc):X4}, cpu_cur_status {cpu_cur_status:X4}";
            if (cpu_state.abrt != 0 || es.@base != 0x20000)
                throw new InvalidOperationException($"Interdit6Allocation ({noms[c]}) : loadseg n'a pas chargé ES : " +
                                                    obtenu);
            if (attendu == null)
                attendu = obtenu;
            else if (obtenu != attendu)
                Interdits.Refuser("Interdit6Allocation", noms[c], attendu, obtenu);
        }
        Interdits.Imprimer("Interdit6Allocation", $"ami386, {Appels} chargements par colonne, {attendu} dans les " +
                           "quatre");
    }

    private static void Descripteur()
    {
        writememw(0, Gdt + 8, 0xffff);
        writememw(0, Gdt + 0xa, 0x0000);
        writememw(0, Gdt + 0xc, 0xf202);
        writememw(0, Gdt + 0xe, 0x0000);
    }

    internal static int BoucleReel()
    {
        var r = 0;
        for (var i = 0; i < Appels; i++)
            r += loadseg(Selecteur, cpu_state.seg_es);
        return r;
    }

    internal static int BouclePile<T>() where T : struct
    {
        var r = 0;
        for (var i = 0; i < Appels; i++)
            r += LoadsegPile<T>(Selecteur, cpu_state.seg_es);
        return r;
    }

    internal static int BoucleTas()
    {
        var r = 0;
        for (var i = 0; i < Appels; i++)
            r += LoadsegTas(Selecteur, cpu_state.seg_es);
        return r;
    }

    // Cpu/x86seg.cs:68-268 (loadseg, sa branche du mode protégé ; le mode réel par le vrai) et :2735-2772
    // (do_seg_load), ligne pour ligne, le descripteur dans un Span de stackalloc.
    private static int LoadsegPile<T>(uint16_t seg, x86seg s) where T : struct
    {
        if ((msw & 1) != 0 && (cpu_state.eflags & VM_FLAG) == 0)
        {
                Span<uint16_t> segdat = stackalloc uint16_t[4];

                if ((seg & ~3) == 0)
                {
                        if (s == cpu_state.seg_ss)
                        {
                                x86ss(null!, 0);
                                return 1;
                        }
                        s.seg = 0;
                        s.access = 0;
                        s.@base = unchecked((uint32_t)(-1));
                        if (s == cpu_state.seg_ds)
                                cpu_cur_status |= CPU_STATUS_NOTFLATDS;
                        return 0;
                }

                uint32_t addr = (uint32_t)(seg & ~7);
                if ((seg & 4) != 0)
                {
                        if (addr >= ldt.limit)
                        {
                                x86gpf(null!, (uint16_t)(seg & ~3));
                                return 1;
                        }
                        addr += ldt.@base;
                }
                else
                {
                        if (addr >= gdt.limit)
                        {
                                x86gpf(null!, (uint16_t)(seg & ~3));
                                return 1;
                        }
                        addr += gdt.@base;
                }

                cpl_override = 1;
                segdat[0] = readmemw(0, addr);
                segdat[1] = readmemw(0, addr + 2);
                segdat[2] = readmemw(0, addr + 4);
                segdat[3] = readmemw(0, addr + 6);
                cpl_override = 0;
                if (cpu_state.abrt != 0)
                        return 1;
                int dpl = (segdat[2] >> 13) & 3;

                if (s == cpu_state.seg_ss)
                {
                        if ((seg & ~3) == 0)
                        {
                                x86gpf(null!, (uint16_t)(seg & ~3));
                                return 1;
                        }
                        if ((seg & 3) != CPL || dpl != CPL)
                        {
                                x86gpf(null!, (uint16_t)(seg & ~3));
                                return 1;
                        }
                        switch ((segdat[2] >> 8) & 0x1F)
                        {
                        case 0x12:
                        case 0x13:
                        case 0x16:
                        case 0x17: /*r/w*/
                                break;
                        default:
                                x86gpf(null!, (uint16_t)(seg & ~3));
                                return 1;
                        }
                        if ((segdat[2] & 0x8000) == 0)
                        {
                                x86ss(null!, (uint16_t)(seg & ~3));
                                return 1;
                        }
                        set_stack32((segdat[3] & 0x40) != 0 ? 1 : 0);
                }
                else if (s != cpu_state.seg_cs)
                {
                        switch ((segdat[2] >> 8) & 0x1F)
                        {
                        case 0x10:
                        case 0x11:
                        case 0x12:
                        case 0x13: /*Data segments*/
                        case 0x14:
                        case 0x15:
                        case 0x16:
                        case 0x17:
                        case 0x1A:
                        case 0x1B: /*Readable non-conforming code*/
                                if ((seg & 3) > dpl || (CPL) > dpl)
                                {
                                        x86gpf(null!, (uint16_t)(seg & ~3));
                                        return 1;
                                }
                                break;
                        case 0x1E:
                        case 0x1F: /*Readable conforming code*/
                                break;
                        default:
                                x86gpf(null!, (uint16_t)(seg & ~3));
                                return 1;
                        }
                }

                if ((segdat[2] & 0x8000) == 0)
                {
                        x86np("Load data seg not present", (uint16_t)(seg & 0xfffc));
                        return 1;
                }
                s.seg = seg;
                DoSegLoadPile(s, segdat);

                cpl_override = 1;
                writememw(0, addr + 4, (uint16_t)(segdat[2] | 0x100)); /*Set accessed bit*/
                cpl_override = 0;

                s.@checked = 0;
                if (s == cpu_state.seg_ds)
                        codegen_flat_ds = 0;
                if (s == cpu_state.seg_ss)
                        codegen_flat_ss = 0;

                if (s == cpu_state.seg_ds)
                {
                        if (s.@base == 0 && s.limit_low == 0 && s.limit_high == 0xffffffff)
                                cpu_cur_status &= unchecked((uint16_t)~CPU_STATUS_NOTFLATDS);
                        else
                                cpu_cur_status |= CPU_STATUS_NOTFLATDS;
                }
                if (s == cpu_state.seg_ss)
                {
                        if (s.@base == 0 && s.limit_low == 0 && s.limit_high == 0xffffffff)
                                cpu_cur_status &= unchecked((uint16_t)~CPU_STATUS_NOTFLATSS);
                        else
                                cpu_cur_status |= CPU_STATUS_NOTFLATSS;
                }

                return cpu_state.abrt;
        }

        return ModeReel(seg, s);
    }

    private static void DoSegLoadPile(x86seg s, ReadOnlySpan<uint16_t> segdat)
    {
        s.limit = (uint32_t)(segdat[0] | ((segdat[3] & 0xF) << 16));
        s.limit_raw = s.limit;
        if ((segdat[3] & 0x80) != 0)
                s.limit = (s.limit << 12) | 0xFFF;
        s.@base = (uint32_t)(segdat[1] | ((segdat[2] & 0xFF) << 16));
        if (is386 != 0)
                s.@base |= (uint32_t)((segdat[3] >> 8) << 24);
        s.access = (uint8_t)(segdat[2] >> 8);
        s.access2 = (uint8_t)(segdat[3] & 0xf0);

        if ((segdat[2] & 0x1800) != 0x1000 || (segdat[2] & (1 << 10)) == 0) /*expand-down*/
        {
                s.limit_high = s.limit;
                s.limit_low = 0;
        }
        else
        {
                s.limit_high = (segdat[3] & 0x40) != 0 ? 0xffffffff : 0xffff;
                s.limit_low = s.limit + 1;
        }

        if (s == cpu_state.seg_ds)
        {
                if (s.@base == 0 && s.limit_low == 0 && s.limit_high == 0xffffffff)
                        cpu_cur_status &= unchecked((uint16_t)~CPU_STATUS_NOTFLATDS);
                else
                        cpu_cur_status |= CPU_STATUS_NOTFLATDS;
        }
        if (s == cpu_state.seg_ss)
        {
                if (s.@base == 0 && s.limit_low == 0 && s.limit_high == 0xffffffff)
                        cpu_cur_status &= unchecked((uint16_t)~CPU_STATUS_NOTFLATSS);
                else
                        cpu_cur_status |= CPU_STATUS_NOTFLATSS;
        }
    }

    // Le vrai, ligne pour ligne : le descripteur dans un tableau alloué à chaque appel.
    private static int LoadsegTas(uint16_t seg, x86seg s)
    {
        if ((msw & 1) != 0 && (cpu_state.eflags & VM_FLAG) == 0)
        {
                var segdat = new uint16_t[4];

                if ((seg & ~3) == 0)
                {
                        if (s == cpu_state.seg_ss)
                        {
                                x86ss(null!, 0);
                                return 1;
                        }
                        s.seg = 0;
                        s.access = 0;
                        s.@base = unchecked((uint32_t)(-1));
                        if (s == cpu_state.seg_ds)
                                cpu_cur_status |= CPU_STATUS_NOTFLATDS;
                        return 0;
                }

                uint32_t addr = (uint32_t)(seg & ~7);
                if ((seg & 4) != 0)
                {
                        if (addr >= ldt.limit)
                        {
                                x86gpf(null!, (uint16_t)(seg & ~3));
                                return 1;
                        }
                        addr += ldt.@base;
                }
                else
                {
                        if (addr >= gdt.limit)
                        {
                                x86gpf(null!, (uint16_t)(seg & ~3));
                                return 1;
                        }
                        addr += gdt.@base;
                }

                cpl_override = 1;
                segdat[0] = readmemw(0, addr);
                segdat[1] = readmemw(0, addr + 2);
                segdat[2] = readmemw(0, addr + 4);
                segdat[3] = readmemw(0, addr + 6);
                cpl_override = 0;
                if (cpu_state.abrt != 0)
                        return 1;
                int dpl = (segdat[2] >> 13) & 3;

                if (s == cpu_state.seg_ss)
                {
                        if ((seg & ~3) == 0)
                        {
                                x86gpf(null!, (uint16_t)(seg & ~3));
                                return 1;
                        }
                        if ((seg & 3) != CPL || dpl != CPL)
                        {
                                x86gpf(null!, (uint16_t)(seg & ~3));
                                return 1;
                        }
                        switch ((segdat[2] >> 8) & 0x1F)
                        {
                        case 0x12:
                        case 0x13:
                        case 0x16:
                        case 0x17: /*r/w*/
                                break;
                        default:
                                x86gpf(null!, (uint16_t)(seg & ~3));
                                return 1;
                        }
                        if ((segdat[2] & 0x8000) == 0)
                        {
                                x86ss(null!, (uint16_t)(seg & ~3));
                                return 1;
                        }
                        set_stack32((segdat[3] & 0x40) != 0 ? 1 : 0);
                }
                else if (s != cpu_state.seg_cs)
                {
                        switch ((segdat[2] >> 8) & 0x1F)
                        {
                        case 0x10:
                        case 0x11:
                        case 0x12:
                        case 0x13: /*Data segments*/
                        case 0x14:
                        case 0x15:
                        case 0x16:
                        case 0x17:
                        case 0x1A:
                        case 0x1B: /*Readable non-conforming code*/
                                if ((seg & 3) > dpl || (CPL) > dpl)
                                {
                                        x86gpf(null!, (uint16_t)(seg & ~3));
                                        return 1;
                                }
                                break;
                        case 0x1E:
                        case 0x1F: /*Readable conforming code*/
                                break;
                        default:
                                x86gpf(null!, (uint16_t)(seg & ~3));
                                return 1;
                        }
                }

                if ((segdat[2] & 0x8000) == 0)
                {
                        x86np("Load data seg not present", (uint16_t)(seg & 0xfffc));
                        return 1;
                }
                s.seg = seg;
                DoSegLoadTas(s, segdat);

                cpl_override = 1;
                writememw(0, addr + 4, (uint16_t)(segdat[2] | 0x100)); /*Set accessed bit*/
                cpl_override = 0;

                s.@checked = 0;
                if (s == cpu_state.seg_ds)
                        codegen_flat_ds = 0;
                if (s == cpu_state.seg_ss)
                        codegen_flat_ss = 0;

                if (s == cpu_state.seg_ds)
                {
                        if (s.@base == 0 && s.limit_low == 0 && s.limit_high == 0xffffffff)
                                cpu_cur_status &= unchecked((uint16_t)~CPU_STATUS_NOTFLATDS);
                        else
                                cpu_cur_status |= CPU_STATUS_NOTFLATDS;
                }
                if (s == cpu_state.seg_ss)
                {
                        if (s.@base == 0 && s.limit_low == 0 && s.limit_high == 0xffffffff)
                                cpu_cur_status &= unchecked((uint16_t)~CPU_STATUS_NOTFLATSS);
                        else
                                cpu_cur_status |= CPU_STATUS_NOTFLATSS;
                }

                return cpu_state.abrt;
        }

        return ModeReel(seg, s);
    }

    private static void DoSegLoadTas(x86seg s, uint16_t[] segdat)
    {
        s.limit = (uint32_t)(segdat[0] | ((segdat[3] & 0xF) << 16));
        s.limit_raw = s.limit;
        if ((segdat[3] & 0x80) != 0)
                s.limit = (s.limit << 12) | 0xFFF;
        s.@base = (uint32_t)(segdat[1] | ((segdat[2] & 0xFF) << 16));
        if (is386 != 0)
                s.@base |= (uint32_t)((segdat[3] >> 8) << 24);
        s.access = (uint8_t)(segdat[2] >> 8);
        s.access2 = (uint8_t)(segdat[3] & 0xf0);

        if ((segdat[2] & 0x1800) != 0x1000 || (segdat[2] & (1 << 10)) == 0) /*expand-down*/
        {
                s.limit_high = s.limit;
                s.limit_low = 0;
        }
        else
        {
                s.limit_high = (segdat[3] & 0x40) != 0 ? 0xffffffff : 0xffff;
                s.limit_low = s.limit + 1;
        }

        if (s == cpu_state.seg_ds)
        {
                if (s.@base == 0 && s.limit_low == 0 && s.limit_high == 0xffffffff)
                        cpu_cur_status &= unchecked((uint16_t)~CPU_STATUS_NOTFLATDS);
                else
                        cpu_cur_status |= CPU_STATUS_NOTFLATDS;
        }
        if (s == cpu_state.seg_ss)
        {
                if (s.@base == 0 && s.limit_low == 0 && s.limit_high == 0xffffffff)
                        cpu_cur_status &= unchecked((uint16_t)~CPU_STATUS_NOTFLATSS);
                else
                        cpu_cur_status |= CPU_STATUS_NOTFLATSS;
        }
    }

    /// <summary>La branche du mode réel, que la mesure ne prend jamais (la boucle tourne en mode protégé) : le vrai
    /// loadseg, plutôt qu'une troisième copie.</summary>
    private static int ModeReel(uint16_t seg, x86seg s) => loadseg(seg, s);
}
