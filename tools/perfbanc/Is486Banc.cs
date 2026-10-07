// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — outillage de mesure.
//
// CE QUE COÛTE UN TEST DE FAMILLE DANS LA BOUCLE.
//
// Le `if` de pc.runpc() ne tourne que 100 fois par seconde. Ceux qui tournent à chaque
// instruction sont dans les handlers, comme `CLOCK_CYCLES(is486 != 0 ? 8 : 10)` dans
// opCMPSB_a16 (Cpu/386_ops_string.cs:100-124). Ce banc mesure ce que coûte cette lecture
// de is486 : le vrai handler contre une copie où le ternaire devient la constante que le
// processeur prend de toute façon — 10 sur un 386, 8 sur un 486.
//
// Ce qui rend la comparaison honnête :
//   - les deux formes passent par un délégué OpFn, comme exec386 appelle les handlers :
//     le coût d'appel est le même des deux côtés ;
//   - la copie est vérifiée AVANT la mesure : un appel de chaque forme, depuis le même
//     état, doit laisser les mêmes SI, DI, cycles, drapeaux paresseux et prefetch_bytes ;
//   - la machine est posée par le VRAI cpu_set(), au travers des portes du harnais
//     différentiel (Reset386 et Reset486, Cpu/386.State.cs), sur sa carte plate de 16 Mo.

using BenchmarkDotNet.Attributes;
using iXtal26.Cpu;
using static iXtal26.Cpu._386;
using static iXtal26.Cpu._386_common;
using static iXtal26.Cpu.x86;
using static iXtal26.Cpu.x86_flags;

namespace iXtal26.PerfBanc;

/// <summary>Le 386 : is486 nul, le ternaire rend 10.</summary>
public class Cmpsb386
{
    private OpFn avecTest = null!, sansTest = null!;

    [GlobalSetup]
    public void Preparer()
    {
        avecTest = Is486Banc.Preparer("ami386", "i386SX/33");
        sansTest = Is486Banc.CmpsbSans10;
        Is486Banc.VerifierEquivalence(avecTest, sansTest);
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = Is486Banc.Appels)]
    public int SansTest() => Is486Banc.Boucle(sansTest);

    [Benchmark(OperationsPerInvoke = Is486Banc.Appels)]
    public int AvecTest() => Is486Banc.Boucle(avecTest);
}

/// <summary>Le 486 : is486 à 1, le ternaire rend 8.</summary>
public class Cmpsb486
{
    private OpFn avecTest = null!, sansTest = null!;

    [GlobalSetup]
    public void Preparer()
    {
        avecTest = Is486Banc.Preparer("ami486", "i486DX2/66");
        sansTest = Is486Banc.CmpsbSans8;
        Is486Banc.VerifierEquivalence(avecTest, sansTest);
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = Is486Banc.Appels)]
    public int SansTest() => Is486Banc.Boucle(sansTest);

    [Benchmark(OperationsPerInvoke = Is486Banc.Appels)]
    public int AvecTest() => Is486Banc.Boucle(avecTest);
}

internal static class Is486Banc
{
    /// <summary>Appels de handler par invocation : de quoi rendre négligeable le coût du
    /// banc lui-même, que BenchmarkDotNet divise ensuite (OperationsPerInvoke).</summary>
    internal const int Appels = 1000;

    /// <summary>Pose la machine et rend le vrai handler, pris dans la table comme exec386
    /// le prend.</summary>
    internal static OpFn Preparer(string machine, string processeur)
    {
        // L'indice du processeur se cherche par son nom : un indice en dur suivrait en
        // silence une table qui bouge.
        var m = Models.model_c.model_get_model_from_internal_name(machine);
        if (m < 0)
            throw new InvalidOperationException($"machine inconnue : {machine}");
        var table = Models.model_c.models[m].cpu[0].cpus!;
        var indice = Array.FindIndex(table, c => c.name == processeur);
        if (indice < 0)
            throw new InvalidOperationException($"{processeur} absent de la table de {machine}");

        FuzzCpu = indice;
        if (machine == "ami486")
            Reset486();
        else
            Reset386();
        if (is486 != (machine == "ami486" ? 1 : 0))
            throw new InvalidOperationException($"cpu_set() a posé is486 = {is486} pour {processeur}");

        // Mode réel, DS = ES = 0x2000. exec386 pose ea_seg sur DS avant chaque
        // instruction ; ici, personne d'autre ne le ferait.
        x86seg_c.loadseg(0x2000, cpu_state.seg_ds);
        x86seg_c.loadseg(0x2000, cpu_state.seg_es);
        cpu_state.ea_seg = cpu_state.seg_ds;
        cpu_state.flags &= unchecked((uint16_t)~D_FLAG);

        var handler = ops_386[0x0A6];
        if (handler.Method.Name != "opCMPSB_a16")
            throw new InvalidOperationException($"ops_386[0x0A6] est {handler.Method.Name}, pas opCMPSB_a16");
        return handler;
    }

    /// <summary>La boucle mesurée, la même pour les deux formes. SI et DI avancent et
    /// bouclent sur 64 Ko ; cycles repart de zéro à chaque invocation.</summary>
    internal static int Boucle(OpFn handler)
    {
        cycles = 0;
        var r = 0;
        for (var i = 0; i < Appels; i++)
            r += handler(0);
        return r;
    }

    /// <summary>Échoue si la copie ne fait pas exactement ce que fait le vrai handler.</summary>
    internal static void VerifierEquivalence(OpFn vrai, OpFn copie)
    {
        var attendu = Releve(vrai);
        var obtenu = Releve(copie);
        if (attendu != obtenu)
            throw new InvalidOperationException($"la copie diverge du vrai handler : {attendu} contre {obtenu}");

        // L'état de départ de la mesure.
        SI = 0;
        DI = 0;
        cycles = 0;
        prefetch_reset();
    }

    /// <summary>Un appel depuis un état fixé, et tout ce que le handler y écrit.</summary>
    private static (uint16_t Si, uint16_t Di, int Cycles, int FlagsOp, uint32_t FlagsRes, uint32_t Op1, uint32_t Op2,
        int PrefetchBytes, int Retour) Releve(OpFn handler)
    {
        SI = 0x1234;
        DI = 0x4321;
        writememb(ds, 0x1234, 0x5A);
        writememb(es, 0x4321, 0x3C);

        // Les deux pages lues d'abord HORS relevé : le premier accès à une page remplit le
        // cache de traduction, et addreadlookup facture ce remplissage 9 cycles
        // (Memory/mem.cs:479, mem.c:378). Sans ces deux lectures, le premier handler relevé
        // payait 18 cycles que le second ne payait plus — mesuré : -34 contre -16.
        readmemb(ds, 0x1234);
        readmemb(es, 0x4321);
        cycles = 0;
        prefetch_reset();
        cpu_state.flags_op = 0;
        cpu_state.flags_res = 0;
        cpu_state.flags_op1 = 0;
        cpu_state.flags_op2 = 0;

        var retour = handler(0);
        return (SI, DI, cycles, cpu_state.flags_op, cpu_state.flags_res, cpu_state.flags_op1, cpu_state.flags_op2,
                prefetch_bytes, retour);
    }

    // ---- Les copies : Cpu/386_ops_string.cs:100-124 (opCMPSB_a16), ligne pour ligne, SAUF
    //      le ternaire `is486 != 0 ? 8 : 10`, remplacé par la constante du processeur. ----

    internal static int CmpsbSans10(uint32_t fetchdat)
    {
        uint8_t src, dst;

        if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        if (SEG_CHECK_READ(cpu_state.seg_es!)) return 1;
        src = readmemb(cpu_state.ea_seg!.@base, SI);
        dst = readmemb(es, DI);
        if (cpu_state.abrt != 0)
                return 1;
        setsub8(src, dst);
        if ((cpu_state.flags & D_FLAG) != 0)
        {
                DI--;
                SI--;
        }
        else
        {
                DI++;
                SI++;
        }
        CLOCK_CYCLES(10);
        PREFETCH_RUN(10, 1, -1, 2, 0, 0, 0, 0);
        return 0;
    }

    internal static int CmpsbSans8(uint32_t fetchdat)
    {
        uint8_t src, dst;

        if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        if (SEG_CHECK_READ(cpu_state.seg_es!)) return 1;
        src = readmemb(cpu_state.ea_seg!.@base, SI);
        dst = readmemb(es, DI);
        if (cpu_state.abrt != 0)
                return 1;
        setsub8(src, dst);
        if ((cpu_state.flags & D_FLAG) != 0)
        {
                DI--;
                SI--;
        }
        else
        {
                DI++;
                SI++;
        }
        CLOCK_CYCLES(8);
        PREFETCH_RUN(8, 1, -1, 2, 0, 0, 0, 0);
        return 0;
    }
}
