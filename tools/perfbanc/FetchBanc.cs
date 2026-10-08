// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — outillage de mesure.
//
// CE QUE COÛTE LA CORRECTION DE PB-87 DANS FETCH (G13.3, M1 ; PLAN-G13.md, § La vérification).
//
// FETCH est le site le plus chaud du cœur 8088 : inliné dans execx86 à chaque lecture d'instruction. La garde de PB-87
// y est sur la lecture principale, file vide, celle que chaque saut provoque. En mode PCem, le JIT la plie : le code
// machine d'execx86 est celui de M0, et c'est la preuve du 0 % (tools/listings-jit.sh, M2) ; une copie d'execx86 sans
// garde, à mesurer à côté, n'existe pas. Ce banc mesure donc l'autre face : ce que coûte la correction QUAND elle est
// demandée, le débit d'execx86 sur une boucle qui vide la file à chaque tour, dans trois processus (BenchmarkDotNet en
// lance un par banc, et le mode se fige une fois par processus) : le mode PCem, PB-87 seul, le mode matériel entier.
//
// La boucle, en 0000:0100 sur la RAM plate du harnais : MOV AX,1234h ; ADD BX,AX ; LOOP (-7) — un saut par tour, donc
// une file vide et une lecture principale par tour. Chaque appel joue 100 000 cycles. Avant la mesure, chaque processus
// imprime son état après un appel (BX, CX, IP) : les trois doivent être les mêmes, PB-87 ne changeant rien loin de
// FFFFh.

using BenchmarkDotNet.Attributes;
using iXtal26.Cpu;
using iXtal26.Diag;
using iXtal26.Memory;

namespace iXtal26.PerfBanc;

public class FetchPcem
{
    [GlobalSetup]
    public void Preparer() => FetchBanc.Preparer("aucun");

    [Benchmark(Baseline = true)]
    public int Boucle() => FetchBanc.Boucle();
}

public class FetchPb87
{
    [GlobalSetup]
    public void Preparer() => FetchBanc.Preparer("PB-87");

    [Benchmark]
    public int Boucle() => FetchBanc.Boucle();
}

public class FetchMateriel
{
    [GlobalSetup]
    public void Preparer() => FetchBanc.Preparer("tout");

    [Benchmark]
    public int Boucle() => FetchBanc.Boucle();
}

internal static class FetchBanc
{
    internal const int Cycles = 100_000;

    private static readonly byte[] Code = [0xB8, 0x34, 0x12, 0x01, 0xC3, 0xE2, 0xF9];

    /// <summary>Pose le mode avant le gel (le premier _808x.Reset fige), puis vérifie l'état après un appel : il ne doit
    /// pas dépendre du mode.</summary>
    internal static void Preparer(string mode)
    {
        if (!ModeMateriel.Demander(mode, "FetchBanc"))
            throw new InvalidOperationException($"mode « {mode} » refusé");
        _808x.Reset();
        mem.fill_ram(0x90);
        for (var k = 0; k < Code.Length; k++)
            mem.ram[0x100 + k] = Code[k];
        Boucle();
        var r = new ushort[(int)R.COUNT];
        _808x.GetRegs(r);
        if (r[(int)R.IP] is < 0x100 or > 0x107 || r[(int)R.CX] == 0xFFFF)
            throw new InvalidOperationException($"la boucle ne tourne pas : IP = {r[(int)R.IP]:X4}, CX = {r[(int)R.CX]:X4}");
        Console.WriteLine($"// FetchBanc ({ModeMateriel.Description}) : après un appel, BX = {r[(int)R.BX]:X4}, " +
                          $"CX = {r[(int)R.CX]:X4}, IP = {r[(int)R.IP]:X4}");
    }

    /// <summary>La boucle n'écrit pas en mémoire : le code posé une fois suffit ; seuls les registres repartent.</summary>
    internal static int Boucle()
    {
        var r = new ushort[(int)R.COUNT];
        r[(int)R.CX] = 0xFFFF;
        r[(int)R.SP] = 0xFFFE;
        r[(int)R.IP] = 0x100;
        r[(int)R.FLAGS] = 0xF002;
        _808x.SetRegs(r);
        return _808x.Run(Cycles);
    }
}
