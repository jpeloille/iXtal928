// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — outillage de mesure.
//
// CE QUE COÛTE LE DÉCODEUR DE LONGUEUR DE PB-50 ET PB-51 (G13.5, M1 ; PLAN-G13.md, § La vérification).
//
// En mode matériel, chaque instruction du cœur 286/386/486 passe par lire_instruction_materiel au lieu de
// `fastreadl(cs + pc)` : un appel, la limite de CS, et l'octet de tête comparé aux préfixes. C'est le seul ajout de G13
// sur le chemin de toutes les instructions de ce cœur. En mode PCem, le JIT plie la garde : le code d'exec386 est celui
// de M0 (tools/listings-jit.sh, M2). Ce banc mesure l'autre face, le débit d'exec386 quand la correction est demandée,
// dans quatre processus (BenchmarkDotNet en lance un par banc, et le mode se fige une fois par processus) : le mode PCem
// et PB-51 (avec PB-50, son groupe), sur deux boucles.
//
// Les boucles, en 0000:0100 sur l'ami386, en mode réel : MOV AX,1234h ; ADD BX,AX ; LOOP (-7), sans préfixe ; puis
// ES: MOV AX,[BX] ; ADD BX,AX ; LOOP (-7), un préfixe par tour, qui fait lire au chemin court les quatre octets de tête.
// Chaque appel joue 100 000 cycles. Avant la mesure, chaque processus imprime son état après un appel (BX, CX, IP) :
// ceux d'une même boucle doivent être les mêmes, la limite étant loin.
//
// G13.5d — PB-189 met un contrôle de limite sur chaque accès mémoire. Une troisième boucle en fait deux par tour :
// ADD [BX],AX ; MOV AX,[SI] ; LOOP (-6), sous le mode PCem et sous PB-189 seul.

using BenchmarkDotNet.Attributes;
using iXtal26.Cpu;
using iXtal26.Diag;
using iXtal26.Memory;

namespace iXtal26.PerfBanc;

public class Exec386Pcem
{
    [GlobalSetup]
    public void Preparer() => Exec386Banc.Preparer("aucun", Exec386Banc.SansPrefixe);

    [Benchmark(Baseline = true)]
    public int Boucle() => Exec386Banc.Boucle();
}

public class Exec386Pb51
{
    [GlobalSetup]
    public void Preparer() => Exec386Banc.Preparer("PB-51", Exec386Banc.SansPrefixe);

    [Benchmark]
    public int Boucle() => Exec386Banc.Boucle();
}

public class Exec386PrefixePcem
{
    [GlobalSetup]
    public void Preparer() => Exec386Banc.Preparer("aucun", Exec386Banc.AvecPrefixe);

    [Benchmark(Baseline = true)]
    public int Boucle() => Exec386Banc.Boucle();
}

public class Exec386PrefixePb51
{
    [GlobalSetup]
    public void Preparer() => Exec386Banc.Preparer("PB-51", Exec386Banc.AvecPrefixe);

    [Benchmark]
    public int Boucle() => Exec386Banc.Boucle();
}

public class Exec386MemoirePcem
{
    [GlobalSetup]
    public void Preparer() => Exec386Banc.Preparer("aucun", Exec386Banc.AvecMemoire);

    [Benchmark(Baseline = true)]
    public int Boucle() => Exec386Banc.Boucle();
}

public class Exec386MemoirePb189
{
    [GlobalSetup]
    public void Preparer() => Exec386Banc.Preparer("PB-189", Exec386Banc.AvecMemoire);

    [Benchmark]
    public int Boucle() => Exec386Banc.Boucle();
}

internal static class Exec386Banc
{
    internal const int Cycles = 100_000;

    internal static readonly byte[] SansPrefixe = [0xB8, 0x34, 0x12, 0x01, 0xC3, 0xE2, 0xF9];
    internal static readonly byte[] AvecPrefixe = [0x26, 0x8B, 0x07, 0x01, 0xC3, 0xE2, 0xF9];
    internal static readonly byte[] AvecMemoire = [0x01, 0x07, 0x8B, 0x04, 0xE2, 0xFA];

    /// <summary>Pose le mode avant le gel (le premier Reset386 fige), puis vérifie l'état après un appel : il ne doit
    /// pas dépendre du mode.</summary>
    internal static void Preparer(string mode, byte[] code)
    {
        if (!ModeMateriel.Demander(mode, "Exec386Banc"))
            throw new InvalidOperationException($"mode « {mode} » refusé");
        _386.Reset386();
        mem.fill_ram(0x90);
        for (var k = 0; k < code.Length; k++)
            mem.ram[0x100 + k] = code[k];
        Boucle();
        var r = new ushort[(int)R.COUNT];
        _808x.GetRegs(r);
        if (r[(int)R.IP] is < 0x100 or > 0x107 || r[(int)R.CX] == 0xFFFF)
            throw new InvalidOperationException($"la boucle ne tourne pas : IP = {r[(int)R.IP]:X4}, CX = {r[(int)R.CX]:X4}");
        var boucle = code == AvecMemoire ? "deux accès mémoire" : code[0] == 0x26 ? "avec préfixe" : "sans préfixe";
        Console.WriteLine($"// Exec386Banc ({ModeMateriel.Description}, {boucle}) : " +
                          $"après un appel, BX = {r[(int)R.BX]:X4}, CX = {r[(int)R.CX]:X4}, IP = {r[(int)R.IP]:X4}");
    }

    /// <summary>La boucle n'écrit pas dans son code (celle des accès mémoire écrit en 0000:0000, loin de lui) : le code
    /// posé une fois suffit ; seuls les registres repartent. Aucun
    /// timer n'est posé : timer_target vaut 7FFFFFFFh, `cycle_period` déborde en négatif et exec386 ne sort plus de sa
    /// boucle (mesuré, sans une instruction jouée). Le banc pose donc l'échéance au bout de ses cycles, comme Step286
    /// la pose au pas suivant.</summary>
    internal static int Boucle()
    {
        var r = new ushort[(int)R.COUNT];
        r[(int)R.CX] = 0xFFFF;
        r[(int)R.SP] = 0xFFFE;
        r[(int)R.IP] = 0x100;
        r[(int)R.FLAGS] = 0xF002;
        _808x.SetRegs(r);
        timer.timer_target = (uint)timer.tsc + Cycles;
        _386.exec386(Cycles);
        return x86.cycles;
    }
}
