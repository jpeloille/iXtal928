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

        // LES CAS-LIMITES, CONSTRUITS PLUTÔT QU'ESPÉRÉS.
        //
        // Chacun demande une valeur précise d'adresse effective : une chance sur
        // 65 536 en tirage uniforme, donc 30 000 instructions de fuzzeur ne les
        // voient probablement JAMAIS. Les écrire à la main est la seule façon
        // d'exercer les fonctions qu'ils traversent.

        // (a) L'ÉCRITURE MOT QUI FRANCHIT 0xFFFF. opMOV_a16_AX n'a pas de
        //     CHECK_WRITE : l'accès a bien lieu, et writememwl se scinde en deux
        //     écritures d'octet de part et d'autre de la frontière de page.
        Cas("ecriture mot a cheval : MOV [FFFF], AX", [0xA3, 0xFF, 0xFF], ref echecs);

        // (b) L'ABANDON, le vrai. opMOV_w_r_a16 porte CHECK_WRITE(seg, ea, ea+1) :
        //     à l'offset 0xFFFF, `high` vaut 0x10000 et dépasse limit_high. C'est le
        //     seul cas qui traverse x86gpf, la branche d'abandon de exec386,
        //     x86_doabrt et son empilement. Encodage : mod=00, rm=110 (direct),
        //     reg=000 (AX).
        Cas("abandon : MOV [FFFF], AX par ModRM direct", [0x89, 0x06, 0xFF, 0xFF], ref echecs);

        // (c) L'OPCODE ILLÉGAL : C6 avec un champ `reg` non nul. Le fuzzeur le voit
        //     sept fois sur huit, mais le chemin ILLEGAL_ON -> x86illegal -> x86_int
        //     mérite d'être nommé quelque part.
        Cas("opcode illegal : MOV [BX], imm8 avec reg != 0", [0xC6, 0x08, 0x42], ref echecs);

        // (d) LE PIÈGE PAS-À-PAS, avec T_FLAG armé.
        //
        // C'est le premier cas où flags_rebuild() fait vraiment son travail des deux
        // côtés. exec386 appelle flags_rebuild sur le chemin du piège (386.c:227) ;
        // tant qu'aucun handler ne posait flags_op, l'appel était un no-op. Un ADD
        // suivi d'un piège force la matérialisation, puis l'empilement des flags
        // MATÉRIALISÉS — si les deux côtés ne reconstruisaient pas à l'identique, la
        // valeur empilée les séparerait.
        //
        // C'est aussi ce que le commentaire de h_trace_note (ab8f50b) annonçait :
        // sous le 286, la phase 1 de boot-diff hache un `flags` périmé. Ici il cesse
        // de l'être, et on vérifie que les deux côtés le matérialisent au même
        // instant. T_FLAG = 0x100, plus le bit 1 toujours à un.
        Cas("piege apres ADD : T_FLAG arme", [0x01, 0xC3], ref echecs, 0x0102);

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

    /// <summary>Un cas DIRIGÉ, comparé à l'oracle sur les 58 champs. Les deux côtés
    /// reçoivent le même code au même endroit et le même état de registres.</summary>
    private static void Cas(string nom, byte[] code, ref int echecs, ushort flags = 0)
    {
        var regs = new ushort[(int)iXtal26.Diag.R.COUNT];
        for (var i = 0; i < regs.Length; i++)
                regs[i] = 0;
        regs[(int)iXtal26.Diag.R.CS] = 0x1000;
        regs[(int)iXtal26.Diag.R.SS] = 0x2000;
        regs[(int)iXtal26.Diag.R.DS] = 0x3000;
        regs[(int)iXtal26.Diag.R.SP] = 0x0100;
        regs[(int)iXtal26.Diag.R.AX] = 0x1234;
        regs[(int)iXtal26.Diag.R.BX] = 0x0010;
        regs[(int)iXtal26.Diag.R.FLAGS] = flags;

        Oracle.h_set_core(Oracle.Core286);
        Oracle.h_reset();
        Oracle.h_fill_ram(0x90);
        _386.Reset286();
        iXtal26.Memory.mem.fill_ram(0x90);

        var linear = (uint)(regs[(int)iXtal26.Diag.R.CS] << 4);
        Oracle.h_load(linear, code, (uint)code.Length);
        for (var i = 0; i < code.Length; i++)
                iXtal26.Memory.mem.ram[linear + i] = code[i];

        Oracle.h_setregs(regs);
        _808x.SetRegs(regs);

        var cycC = Oracle.h_step();
        var cycS = _386.Step286();

        var a = iXtal26.Diag.HState.Create();
        var b = iXtal26.Diag.HState.Create();
        Oracle.h_getstate(out a);
        _808x.GetState(ref b);

        var diff = Fuzzer.Compare(a, b, cycC, cycS);
        if (diff is null)
                Console.WriteLine($"  [ok] {nom} : identique, pc {b.pc:X4} CS {b.seg_sel[1]:X4} cycles {cycS}");
        else
        {
                Console.WriteLine($"  [ECHEC] {nom} : {diff}");
                echecs++;
        }
    }
}
