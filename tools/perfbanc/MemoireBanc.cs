// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — outillage de mesure.
//
// L'ACCÈS MÉMOIRE LENT, POUR LA RÉFÉRENCE DE PERFORMANCE (G14.0a ; PLAN.md § G15 : « accès mémoire »).
//
// Les boucles d'Exec386Banc et d'Exec8088 lisent et écrivent la RAM plate du harnais, par le chemin rapide (la table
// de traduction). Ce banc prend l'autre chemin : une lecture en ROM et une écriture en mémoire vidéo, qui passent par
// readmembl et writemembl, puis par les gestionnaires des mappages (la ROM du BIOS, la carte vidéo). La machine est
// montée sans exécuter le BIOS (pc.initpc), comme VideoBanc : ses minuteries tournent (la vidéo, le son), le PIT n'est
// pas programmé, et la boucle court IF = 0. Deux machines, deux cœurs :
// - l'XT et la CGA (execx86) : la ROM du BIOS en FE00:0000, la CGA en B800:0000 ;
// - l'ami486 (i486DX2/66) et la GD5429 (exec386, mode réel) : la ROM en FE00:0000, B800:0000 dans la fenêtre de la
//   VGA (A0000h-BFFFFh au reset, GR06 = 0), servie par svga_write.
// La boucle, en 0000:0100 : MOV AL,[SI] ; MOV ES:[DI],AL ; INC SI ; INC DI ; AND SI,1FFFh ; AND DI,0FFFh ; LOOP, avec
// DS = FE00h et ES = B800h. Chaque appel joue 100 000 cycles. Avant la mesure, chaque processus imprime son état
// après un appel : les registres, les accès servis par le chemin lent (les compteurs de readmembl et de writemembl),
// et l'empreinte des 4 Ko de mémoire vidéo écrits.

using BenchmarkDotNet.Attributes;
using iXtal26.Cpu;
using iXtal26.Diag;
using iXtal26.Memory;

namespace iXtal26.PerfBanc;

public class MemoireLente
{
    [Params("ibmxt", "ami486")] public string Machine = "";
    [Params("aucun", "tout")] public string Mode = "";

    [GlobalSetup]
    public void Preparer() => MemoireBanc.Preparer(Mode, Machine);

    [Benchmark]
    public int Cycles() => MemoireBanc.Boucle();
}

internal static class MemoireBanc
{
    internal const int Cycles = 100_000;

    /// <summary>Les registres posés à chaque appel, dans un tableau gardé et remis à zéro : la partie mesurée
    /// n'alloue rien (les bancs de G13 allouent le leur à chaque appel, 56 octets).</summary>
    private static readonly ushort[] Registres = new ushort[(int)R.COUNT];

    private static readonly byte[] Code =
    [
        0x8A, 0x04,             // MOV AL,[SI]
        0x26, 0x88, 0x05,       // MOV ES:[DI],AL
        0x46,                   // INC SI
        0x47,                   // INC DI
        0x81, 0xE6, 0xFF, 0x1F, // AND SI,1FFFh
        0x81, 0xE7, 0xFF, 0x0F, // AND DI,0FFFh
        0xE2, 0xEF,             // LOOP 0100h
    ];

    private static bool _at;

    /// <summary>Pose le mode avant le gel, monte la machine, la boucle en 0000:0100 ; puis joue un appel et vérifie
    /// l'état.</summary>
    internal static void Preparer(string mode, string machine)
    {
        Reference.Demander(mode, "MemoireLente");
        Reference.Monter(machine == "ibmxt"
            ? "model = ibmxt\nmem_size = 640\ngfxcard = cga\nsndcard = none\n"
            : "model = ami486\ncpu = 10\nfpu = builtin\nmem_size = 4096\ngfxcard = cl_gd5429\nsndcard = none\n",
            "MemoireLente");
        Reference.VerifierMode(mode, "MemoireLente");
        _at = x86.AT != 0;
        for (var k = 0; k < Code.Length; k++)
            mem.ram[0x100 + k] = Code[k];
        mem.flushmmucache();
        var (lus, ecrits) = (Counters.n_readmembl, Counters.n_writemembl);
        var avant = _808x.ins;
        Boucle();
        var instructions = _808x.ins - avant;
        (lus, ecrits) = (Counters.n_readmembl - lus, Counters.n_writemembl - ecrits);
        var r = new ushort[(int)R.COUNT];
        _808x.GetRegs(r);
        if (r[(int)R.IP] is < 0x100 or > 0x110 || r[(int)R.CX] == 0xFFFF || ecrits == 0)
            throw new InvalidOperationException($"MemoireLente : la boucle ne tourne pas : IP = {r[(int)R.IP]:X4}, " +
                                                $"CX = {r[(int)R.CX]:X4}, {ecrits} écritures lentes");
        var video = new byte[0x1000];
        for (var k = 0; k < video.Length; k++)
            video[k] = mem.mem_readb_phys(0xb8000u + (uint)k);
        var processeur = Models.model_c.models[Models.model_c.model].cpu[cpu_c.cpu_manufacturer].cpus![cpu_c.cpu].name;
        Reference.Verifier("MemoireLente", $"{machine}, {processeur}, {pc.CarteVideo.Nom}", mode,
                           $"après un appel, {instructions} instructions, {lus} lectures et {ecrits} écritures par " +
                           $"le chemin lent, CX = {r[(int)R.CX]:X4}, SI = {r[(int)R.SI]:X4}, DI = {r[(int)R.DI]:X4}, " +
                           $"la mémoire vidéo {Reference.Fnv<byte>(video):X16}");
    }

    /// <summary>Les registres repartent à chaque appel ; la boucle n'écrit que dans la mémoire vidéo.</summary>
    internal static int Boucle()
    {
        var r = Registres;
        Array.Clear(r);
        r[(int)R.CX] = 0xFFFF;
        r[(int)R.SP] = 0xFFFE;
        r[(int)R.IP] = 0x100;
        r[(int)R.FLAGS] = 0xF002;
        r[(int)R.DS] = 0xFE00;
        r[(int)R.ES] = 0xB800;
        _808x.SetRegs(r);
        if (!_at)
            return _808x.Run(Cycles);
        x86.cycles = 0;
        _386.exec386(Cycles);
        return x86.cycles;
    }
}
