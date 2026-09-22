// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — outillage de diff.
//
// A2.2b — LA PORTE DE LA BOUCLE, avant qu'aucun handler n'existe.
//
// Elle vérifie quatre choses qu'un handler écrit trop tôt masquerait :
//   - exec386 s'exécute et sort (les deux boucles imbriquées, le budget) ;
//   - le fetch fonctionne : rmdat porte bien l'opcode du vecteur de reset ;
//   - l'aiguillage atteint la BONNE entrée de table, `(opcode | op32) & 0x3ff` ;
//   - l'échec est BRUYANT et nomme l'opcode, au lieu de rendre zéro cycle.
//
// Le quatrième point est celui qui compte. Une table dont les trous rendent
// silencieusement 0 laisserait le cœur avancer sur du vide, et la divergence se
// manifesterait des milliers d'instructions plus loin, sans rapport visible.

using iXtal26.Cpu;
using static iXtal26.Cpu.x86;

namespace iXtal26.Diff;

public static class Core286Check
{
    public static int Run()
    {
        var echecs = 0;

        // Le 286 démarre à F000:FFF0 (AT = 1, resetx86 808x.c:680-683), soit
        // l'adresse physique 0xFFFF0. On y pose un opcode connu.
        Check("0xEA au vecteur de reset", 0xEA, ref echecs);
        Check("0x90 (NOP)", 0x90, ref echecs);
        Check("0xF4 (HLT)", 0xF4, ref echecs);

        // UN PAS EST-IL BIEN UNE INSTRUCTION ?
        //
        // Vérifié côté oracle dès A2.0 (« pas 0 : 1 instruction(s) »), jamais côté
        // C# : tant que la table était vide, aucun pas ne s'achevait. Maintenant
        // qu'un handler existe, on peut le mesurer — et il le faut. Si les deux
        // mécanismes de pas-à-pas ne comptaient pas pareil, chaque divergence du
        // fuzzeur désignerait ensuite le mauvais coupable.
        //
        // C'est _808x.ins, le compteur du CŒUR incrémenté DANS exec386, et non le
        // compteur du harnais qu'on avance nous-mêmes d'un cran par appel.
        _386.Reset286();
        iXtal26.Memory.mem.ram[0xFFFF0] = 0xB8;   // MOV AX, imm16
        iXtal26.Memory.mem.ram[0xFFFF1] = 0x34;
        iXtal26.Memory.mem.ram[0xFFFF2] = 0x12;
        var avant = _808x.ins;
        _386.Step286();
        var avance = _808x.ins - avant;
        if (avance == 1 && AX == 0x1234)
            Console.WriteLine($"  [ok] un pas = une instruction (AX = {AX:X4})");
        else
        {
            Console.WriteLine($"  [ECHEC] un pas a avance de {avance} instruction(s), AX = {AX:X4}");
            echecs++;
        }

        Console.WriteLine();
        if (echecs == 0)
        {
            Console.WriteLine("Vert : la boucle tourne, l'aiguillage atteint la bonne entrée,");
            Console.WriteLine("       un opcode non transcrit échoue en se nommant,");
            Console.WriteLine("       et un pas vaut exactement une instruction.");
            return 0;
        }
        Console.WriteLine($"ROUGE : {echecs} contrôle(s) en échec.");
        return 1;
    }

    private static void Check(string nom, byte opcode, ref int echecs)
    {
        _386.Reset286();
        iXtal26.Memory.mem.ram[0xFFFF0] = opcode;

        // rammask doit valoir 0x00FFFFFF : 24 lignes d'adresse, pas 32.
        if (iXtal26.Memory.mem.rammask != 0x00FFFFFF)
        {
            Console.WriteLine($"  [ECHEC] rammask = {iXtal26.Memory.mem.rammask:X8}, attendu 00FFFFFF");
            echecs++;
        }

        try
        {
            _386.Step286();
            Console.WriteLine($"  [ECHEC] {nom} : aucun échec levé — la table rend du vide en silence");
            echecs++;
        }
        catch (Exception e)
        {
            var m = e.Message;
            var attendu = $"opcode {opcode:X2} non transcrit";
            if (m.Contains(attendu, StringComparison.Ordinal))
                Console.WriteLine($"  [ok] {nom} : « {m.Trim()} »");
            else
            {
                Console.WriteLine($"  [ECHEC] {nom} : attendait « {attendu} », a eu « {m.Trim()} »");
                echecs++;
            }
        }
    }
}
