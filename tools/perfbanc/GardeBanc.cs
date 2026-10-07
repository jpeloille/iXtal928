// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — outillage de mesure.
//
// CE QUE COÛTE UNE GARDE DU MODE MATÉRIEL (G13, M1 ; PLAN-G13.md, § La vérification).
//
// La garde de PB-01 dans setadc8 (Cpu/808x.cs), en mode PCem, sous quatre formes, chacune dans une copie de la fonction,
// appelée par le même délégué :
//   - SansGarde : la fonction de PCem, telle qu'avant G13 ;
//   - Figee : la garde réelle, `materiel.pb_01`, figée (à faux) avant que la méthode soit compilée : le JIT la plie, et
//     la copie doit coûter exactement SansGarde ;
//   - Mutable : un `static bool` ordinaire (l'option A du plan, écartée) : une lecture, un test et un saut par appel ;
//   - FigeeTropTard : un `static readonly` dont la classe ne s'initialise qu'APRÈS la compilation de la méthode (le gel
//     manqué) : le JIT y laisse l'appel d'initialisation et la lecture.
// L'équivalence est vérifiée avant la mesure : les quatre copies laissent les mêmes drapeaux que le vrai setadc8, sur
// les 65 536 couples (a, b) et les deux valeurs de la retenue entrante.

using System.Reflection;
using BenchmarkDotNet.Attributes;
using iXtal26.Cpu;
using static iXtal26.Cpu._386_common;
using static iXtal26.Cpu.x86;

namespace iXtal26.PerfBanc;

public class GardePb01
{
    private GardeBanc.Aide8 sansGarde = null!, figee = null!, mutable = null!, tropTard = null!;

    [GlobalSetup]
    public void Preparer()
    {
        // Le mode se fige ici, en mode PCem, avant qu'aucune copie ne soit compilée (ModeMateriel.Figer).
        _808x.Reset();
        GardeMutable.pb_01 = ModeMateriel.Demande(1);
        sansGarde = GardeBanc.SansGarde;
        figee = GardeBanc.Figee;
        mutable = GardeBanc.Mutable;
        tropTard = GardeBanc.FigeeTropTard;
        GardeBanc.VerifierEquivalence([sansGarde, figee, mutable, tropTard]);
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = GardeBanc.Appels)]
    public int SansGarde() => GardeBanc.Boucle(sansGarde);

    [Benchmark(OperationsPerInvoke = GardeBanc.Appels)]
    public int Figee() => GardeBanc.Boucle(figee);

    [Benchmark(OperationsPerInvoke = GardeBanc.Appels)]
    public int Mutable() => GardeBanc.Boucle(mutable);

    [Benchmark(OperationsPerInvoke = GardeBanc.Appels)]
    public int FigeeTropTard() => GardeBanc.Boucle(tropTard);
}

/// <summary>L'option A : un champ modifiable, lu à chaque passage.</summary>
internal static class GardeMutable
{
    internal static bool pb_01;
}

/// <summary>Le gel manqué : un champ figé, dont la classe s'initialise après la compilation de la méthode qui le lit.
/// Rien ne la touche avant le premier appel de FigeeTropTard.</summary>
internal static class GardeTropTard
{
    internal static readonly bool pb_01;

    static GardeTropTard()
    {
        pb_01 = ModeMateriel.Demande(1);
    }
}

internal static class GardeBanc
{
    internal delegate void Aide8(uint8_t a, uint8_t b);

    internal const int Appels = 1000;

    internal static int Boucle(Aide8 aide)
    {
        for (var i = 0; i < Appels; i++)
        {
            _808x.tempc = i & 1;
            aide((uint8_t)i, (uint8_t)(i * 7));
        }
        return cpu_state.flags;
    }

    internal static void VerifierEquivalence(Aide8[] copies)
    {
        var vrai = typeof(_808x).GetMethod("setadc8", BindingFlags.NonPublic | BindingFlags.Static)!;
        for (var t = 0; t < 2; t++)
            for (var a = 0; a < 256; a++)
                for (var b = 0; b < 256; b++)
                {
                    _808x.tempc = t;
                    cpu_state.flags = 0xF002;
                    vrai.Invoke(null, [(uint8_t)a, (uint8_t)b]);
                    var attendu = cpu_state.flags;
                    foreach (var copie in copies)
                    {
                        cpu_state.flags = 0xF002;
                        copie((uint8_t)a, (uint8_t)b);
                        if (cpu_state.flags != attendu)
                            throw new InvalidOperationException(
                                $"{copie.Method.Name} : a = {a:X2}, b = {b:X2}, retenue {t} : drapeaux {cpu_state.flags:X4}, " +
                                $"setadc8 {attendu:X4}");
                    }
                }
    }

    internal static void SansGarde(uint8_t a, uint8_t b)
    {
        uint16_t c = (uint16_t)(a + b + _808x.tempc);
        cpu_state.flags &= unchecked((uint16_t)~0x8D5);
        cpu_state.flags |= _808x.znptable8[c & 0xFF];
        if ((c & 0x100) != 0)
                cpu_state.flags |= C_FLAG;
        if (((a ^ b) & 0x80) == 0 && ((a ^ c) & 0x80) != 0)
                cpu_state.flags |= V_FLAG;
        if ((((a & 0xF) + (b & 0xF)) & 0x10) != 0)
                cpu_state.flags |= A_FLAG;
    }

    internal static void Figee(uint8_t a, uint8_t b)
    {
        uint16_t c = (uint16_t)(a + b + _808x.tempc);
        cpu_state.flags &= unchecked((uint16_t)~0x8D5);
        cpu_state.flags |= _808x.znptable8[c & 0xFF];
        if ((c & 0x100) != 0)
                cpu_state.flags |= C_FLAG;
        if (((a ^ b) & 0x80) == 0 && ((a ^ c) & 0x80) != 0)
                cpu_state.flags |= V_FLAG;
        if (materiel.pb_01)
                AfMateriel(a, b, c);
        else
                if ((((a & 0xF) + (b & 0xF)) & 0x10) != 0)
                        cpu_state.flags |= A_FLAG;
    }

    internal static void Mutable(uint8_t a, uint8_t b)
    {
        uint16_t c = (uint16_t)(a + b + _808x.tempc);
        cpu_state.flags &= unchecked((uint16_t)~0x8D5);
        cpu_state.flags |= _808x.znptable8[c & 0xFF];
        if ((c & 0x100) != 0)
                cpu_state.flags |= C_FLAG;
        if (((a ^ b) & 0x80) == 0 && ((a ^ c) & 0x80) != 0)
                cpu_state.flags |= V_FLAG;
        if (GardeMutable.pb_01)
                AfMateriel(a, b, c);
        else
                if ((((a & 0xF) + (b & 0xF)) & 0x10) != 0)
                        cpu_state.flags |= A_FLAG;
    }

    internal static void FigeeTropTard(uint8_t a, uint8_t b)
    {
        uint16_t c = (uint16_t)(a + b + _808x.tempc);
        cpu_state.flags &= unchecked((uint16_t)~0x8D5);
        cpu_state.flags |= _808x.znptable8[c & 0xFF];
        if ((c & 0x100) != 0)
                cpu_state.flags |= C_FLAG;
        if (((a ^ b) & 0x80) == 0 && ((a ^ c) & 0x80) != 0)
                cpu_state.flags |= V_FLAG;
        if (GardeTropTard.pb_01)
                AfMateriel(a, b, c);
        else
                if ((((a & 0xF) + (b & 0xF)) & 0x10) != 0)
                        cpu_state.flags |= A_FLAG;
    }

    private static void AfMateriel(uint32_t a, uint32_t b, uint32_t c)
    {
        if (((a ^ b ^ c) & 0x10) != 0)
                cpu_state.flags |= A_FLAG;
    }
}
