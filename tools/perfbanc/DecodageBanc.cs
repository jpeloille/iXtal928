// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — outillage de mesure.
//
// LE DÉCODAGE, POUR LA RÉFÉRENCE DE PERFORMANCE (G14.0a ; PLAN.md § G15 : « décodage 8088, 286, 386, 486 »).
//
// Exec286 et Exec486 reprennent les trois boucles d'Exec386Banc, ses tableaux d'octets et sa forme (100 000 cycles
// d'exec386, l'échéance posée au bout), sur le processeur des profils de la racine au lieu de l'i386SX/16 : le 286/16
// de l'ami286 (Reset286, ixtal26-286.cfg) et l'i486DX2/66 de l'ami486 (Reset486, ixtal26-486.cfg). Les boucles, en
// 0000:0100, en mode réel, IF = 0 : sans préfixe ; avec ES: ; à deux accès mémoire (voir Exec386Banc.cs). Une
// différence : ES vaut 1000h. Avec ES = 0, la boucle à préfixe, qui ajoute à BX le mot lu en ES:[BX], finit par lire
// son propre code, puis un mot en FFFFh : #GP. L'i386SX/16 n'y arrive pas en 100 000 cycles ; l'i486DX2/66 si
// (mesuré : IP = 9B20h après un appel, la boucle quittée). En 1000:0000-FFFFh, la mémoire ne porte que 90h.
//
// Exec8088 ajoute au FetchBanc ce qu'il n'a pas : un ModRM mémoire avec déplacement, un préfixe de segment, et une
// chaîne courte répétée. La boucle, en 0000:0100 sur la RAM plate du harnais (_808x.Reset, l'IBM PC) :
//   MOV SI,0800h ; MOV DI,0900h ; MOV AX,ES:[BX+SI+4] ; ADD [DI],AX ; PUSH CX ; MOV CX,4 ; REP MOVSB ; POP CX ; LOOP
// SI et DI repartent à chaque tour : la chaîne copie toujours 0800h-0803h en 0900h-0903h, loin du code et de la pile
// (0000:FFFC). Chaque appel joue 100 000 cycles d'execx86.
//
// Chaque processus imprime son état après un appel (les registres et les instructions jouées) : le même dans les deux
// modes, aucune correction ne touchant ces instructions loin des limites.

using BenchmarkDotNet.Attributes;
using iXtal26.Cpu;
using iXtal26.Diag;
using iXtal26.Memory;

namespace iXtal26.PerfBanc;

public class Exec286
{
    [Params("aucun", "tout")] public string Mode = "";
    [Params("sans-prefixe", "prefixe", "memoire")] public string Boucle = "";

    [GlobalSetup]
    public void Preparer() => DecodageBanc.Preparer386("Exec286", Mode, Boucle, "ami286", "286/16");

    [Benchmark]
    public int Cycles() => DecodageBanc.Boucle386();
}

public class Exec486
{
    [Params("aucun", "tout")] public string Mode = "";
    [Params("sans-prefixe", "prefixe", "memoire")] public string Boucle = "";

    [GlobalSetup]
    public void Preparer() => DecodageBanc.Preparer386("Exec486", Mode, Boucle, "ami486", "i486DX2/66");

    [Benchmark]
    public int Cycles() => DecodageBanc.Boucle386();
}

public class Exec8088
{
    [Params("aucun", "tout")] public string Mode = "";

    [GlobalSetup]
    public void Preparer() => DecodageBanc.Preparer8088(Mode);

    [Benchmark]
    public int Cycles() => DecodageBanc.Boucle8088();
}

internal static class DecodageBanc
{
    internal const int Cycles = 100_000;

    /// <summary>Les registres posés à chaque appel, dans un tableau gardé et remis à zéro : la partie mesurée
    /// n'alloue rien (les bancs de G13 allouent le leur à chaque appel, 56 octets).</summary>
    private static readonly ushort[] Registres = new ushort[(int)R.COUNT];

    /// <summary>La boucle du 8088 : ModRM mémoire, préfixe ES:, REP MOVSB de quatre octets.</summary>
    internal static readonly byte[] Code8088 =
    [
        0xBE, 0x00, 0x08,       // MOV SI,0800h
        0xBF, 0x00, 0x09,       // MOV DI,0900h
        0x26, 0x8B, 0x40, 0x04, // MOV AX,ES:[BX+SI+4]
        0x01, 0x05,             // ADD [DI],AX
        0x51,                   // PUSH CX
        0xB9, 0x04, 0x00,       // MOV CX,4
        0xF3, 0xA4,             // REP MOVSB
        0x59,                   // POP CX
        0xE2, 0xEB,             // LOOP 0100h
    ];

    private static byte[] Code(string boucle) => boucle switch
    {
        "sans-prefixe" => Exec386Banc.SansPrefixe,
        "prefixe" => Exec386Banc.AvecPrefixe,
        "memoire" => Exec386Banc.AvecMemoire,
        _ => throw new InvalidOperationException($"boucle inconnue : {boucle}"),
    };

    /// <summary>Pose le mode avant le gel (Reset286 et Reset486 figent), le processeur cherché par son nom, la boucle
    /// en 0000:0100 ; puis joue un appel et vérifie l'état.</summary>
    internal static void Preparer386(string banc, string mode, string boucle, string machine, string processeur)
    {
        Reference.Demander(mode, banc);
        var m = Models.model_c.model_get_model_from_internal_name(machine);
        if (m < 0)
            throw new InvalidOperationException($"{banc} : machine inconnue : {machine}");
        var table = Models.model_c.models[m].cpu[0].cpus!;
        var indice = Array.FindIndex(table, c => c.name == processeur);
        if (indice < 0)
            throw new InvalidOperationException($"{banc} : {processeur} absent de la table de {machine}");
        _386.FuzzCpu = indice;
        if (machine == "ami486")
            _386.Reset486();
        else
            _386.Reset286();
        Reference.VerifierMode(mode, banc);
        if (x86.is486 != (machine == "ami486" ? 1 : 0) || x86.is386 != (machine == "ami486" ? 1 : 0))
            throw new InvalidOperationException($"{banc} : cpu_set() a posé is386 = {x86.is386}, is486 = {x86.is486}");
        var code = Code(boucle);
        mem.fill_ram(0x90);
        for (var k = 0; k < code.Length; k++)
            mem.ram[0x100 + k] = code[k];
        var avant = _808x.ins;
        Boucle386();
        var instructions = _808x.ins - avant;
        var r = new ushort[(int)R.COUNT];
        _808x.GetRegs(r);
        if (r[(int)R.IP] is < 0x100 or > 0x107 || r[(int)R.CX] == 0xFFFF)
            throw new InvalidOperationException($"{banc} : la boucle ne tourne pas : IP = {r[(int)R.IP]:X4}, " +
                                                $"CX = {r[(int)R.CX]:X4}");
        Reference.Verifier(banc, $"{processeur}, {boucle}", mode,
                           $"après un appel, {instructions} instructions, BX = {r[(int)R.BX]:X4}, " +
                           $"CX = {r[(int)R.CX]:X4}, IP = {r[(int)R.IP]:X4}");
    }

    /// <summary>La Boucle d'Exec386Banc, ES = 1000h : les registres repartent, l'échéance est posée au bout des
    /// cycles (aucune minuterie n'existe sans initpc).</summary>
    internal static int Boucle386()
    {
        var r = Registres;
        Array.Clear(r);
        r[(int)R.CX] = 0xFFFF;
        r[(int)R.SP] = 0xFFFE;
        r[(int)R.IP] = 0x100;
        r[(int)R.FLAGS] = 0xF002;
        r[(int)R.ES] = 0x1000;
        _808x.SetRegs(r);
        timer.timer_target = (uint)timer.tsc + Cycles;
        _386.exec386(Cycles);
        return x86.cycles;
    }

    /// <summary>Pose le mode avant le gel (le premier _808x.Reset fige), la boucle en 0000:0100 ; puis joue un appel
    /// et vérifie l'état.</summary>
    internal static void Preparer8088(string mode)
    {
        Reference.Demander(mode, "Exec8088");
        _808x.Reset();
        Reference.VerifierMode(mode, "Exec8088");
        mem.fill_ram(0x90);
        for (var k = 0; k < Code8088.Length; k++)
            mem.ram[0x100 + k] = Code8088[k];
        var avant = _808x.ins;
        Boucle8088();
        var instructions = _808x.ins - avant;
        var r = new ushort[(int)R.COUNT];
        _808x.GetRegs(r);
        if (r[(int)R.IP] is < 0x100 or > 0x114 || r[(int)R.SI] is < 0x800 or > 0x804)
            throw new InvalidOperationException($"Exec8088 : la boucle ne tourne pas : IP = {r[(int)R.IP]:X4}, " +
                                                $"SI = {r[(int)R.SI]:X4}");
        Reference.Verifier("Exec8088", "ModRM mémoire, ES:, REP MOVSB", mode,
                           $"après un appel, {instructions} instructions, AX = {r[(int)R.AX]:X4}, " +
                           $"CX = {r[(int)R.CX]:X4}, SI = {r[(int)R.SI]:X4}, DI = {r[(int)R.DI]:X4}, " +
                           $"IP = {r[(int)R.IP]:X4}, 0900h = {mem.ram[0x900]:X2}{mem.ram[0x901]:X2}");
    }

    /// <summary>Les registres repartent à chaque appel ; la mémoire écrite (0900h-0903h, la pile) est réécrite à
    /// l'identique à chaque tour.</summary>
    internal static int Boucle8088()
    {
        var r = Registres;
        Array.Clear(r);
        r[(int)R.CX] = 0xFFFF;
        r[(int)R.SP] = 0xFFFE;
        r[(int)R.IP] = 0x100;
        r[(int)R.FLAGS] = 0xF002;
        _808x.SetRegs(r);
        return _808x.Run(Cycles);
    }
}
