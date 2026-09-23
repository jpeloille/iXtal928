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
        // l'adresse physique 0xFFFF0. On y pose un opcode ENCORE NON TRANSCRIT.
        //
        // 0xEA y figurait jusqu'a A5 — c'est l'opcode du vecteur de reset, et le
        // choisir avait du sens tant que la table etait vide. A5 l'a transcrit, et
        // ce controle est passe au ROUGE : la porte de A2.2b a tenu jusqu'au bout,
        // puis a dit qu'elle n'avait plus lieu d'etre sous cette forme.
        //
        // CETTE PORTE EST DESTINEE A DISPARAITRE. Quand les 256 emplacements
        // seront poses, plus aucun opcode ne tombera dans opNonTranscrit et il n'y
        // aura plus rien a verifier ici. D'ici la, tenir cette liste a jour fait
        // partie du travail de chaque groupe : un Check sur un opcode qu'on vient
        // de transcrire rougit, ce qui est exactement ce qu'on veut qu'il fasse.
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

        // (e) LA MATÉRIALISATION, UNE FOIS PAR ESPÈCE DE DRAPEAU PARESSEUX.
        //
        // Le fuzzeur compare flags_op/res/op1/op2 : il vérifie donc les POSEURS
        // (setadd, setsub, setadc, setsbc, setznp). Il ne vérifie PAS les
        // lecteurs — VF_SET, AF_SET, CF_SET, NF_SET par espèce — parce qu'en mode
        // simple chaque itération repart d'un reset, la paresse n'est jamais
        // effondrée, et les six branches de flags_rebuild ne tournent pas.
        //
        // Le cas (d) en effondrait une seule, FLAGS_ADD16. Les autres espèces que
        // la bande ALU sait produire — ADD8, SUB8/16, ADC8/16, SBC8/16, ZN8/16 —
        // ne seraient lues qu'au premier PUSHF, Jcc ou INT transcrit, c'est-à-dire
        // loin de leur cause. On les effondre ici, à l'instruction qui les pose.
        //
        // Trois jeux de valeurs : le premier ne déborde pas en signé, le deuxième
        // déborde à l'addition (0x7FFF + 1), le troisième à la soustraction
        // (0 - 0x8000). VF_SET est la branche la plus facile à écrire de travers,
        // et le débordement est la seule chose qui la distingue d'un zéro constant.
        (byte[] code, string nom)[] especes =
        [
                ([0x00, 0xC3], "ADD8"),  ([0x01, 0xC3], "ADD16"),
                ([0x28, 0xC3], "SUB8"),  ([0x29, 0xC3], "SUB16"),
                ([0x10, 0xC3], "ADC8"),  ([0x11, 0xC3], "ADC16"),
                ([0x18, 0xC3], "SBB8"),  ([0x19, 0xC3], "SBB16"),
                ([0x08, 0xC3], "OR8 (ZN8)"), ([0x09, 0xC3], "OR16 (ZN16)"),
        ];
        (ushort ax, ushort bx)[] jeux = [(0x1234, 0x0010), (0x0001, 0x7FFF), (0x8000, 0x0000)];
        foreach (var (code, nom) in especes)
                foreach (var (ax, bx) in jeux)
                        Cas($"piege apres {nom} (AX={ax:X4} BX={bx:X4})", code, ref echecs,
                            0x0102, ax, bx);

        // (f) LA RETENUE ENTRANTE VENUE D'UN ÉTAT PARESSEUX.
        //
        // ADC et SBB lisent CF_SET() pour poser tempc. En mode simple, flags_op
        // vaut toujours FLAGS_UNKNOWN au départ : CF_SET tombe dans sa dernière
        // branche et rend `flags & C_FLAG`. Autrement dit le fuzzeur, même sur
        // 40 000 itérations, n'exerce JAMAIS les branches ADD/SUB/ADC/SBC/ZN de
        // CF_SET comme source de tempc — celles-là mêmes que l'en-tête de
        // 386_ops_arith.cs dit être la raison de l'ordre de `gettempc`.
        //
        // Il faut deux instructions : une qui pose l'espèce, une qui la lit. C'est
        // exactement ce qu'un fuzzeur en mode simple ne peut pas construire.
        Suite("ADD16 pose la retenue, ADC16 la lit",
              [0x05, 0xFF, 0xFF, 0x15, 0x00, 0x00], 2, ref echecs);
        Suite("SUB16 pose l'emprunt, SBB16 le lit",
              [0x2D, 0xFF, 0xFF, 0x1D, 0x00, 0x00], 2, ref echecs);
        Suite("OR16 (ZN16) force la retenue a zero, ADC16 la lit",
              [0x0D, 0x00, 0x00, 0x15, 0x01, 0x00], 2, ref echecs);
        Suite("ADC16 chaine : la branche FLAGS_ADC16 de CF_SET",
              [0x15, 0xFF, 0xFF, 0x15, 0x00, 0x00], 2, ref echecs);
        Suite("ADD8 pose la retenue, ADC8 la lit",
              [0x04, 0xFF, 0x14, 0x00], 2, ref echecs);
        Suite("SUB8 pose l'emprunt, SBB8 le lit",
              [0x2C, 0xFF, 0x1C, 0x00], 2, ref echecs);
        Suite("ADC8 chaine : la branche FLAGS_ADC8 de CF_SET",
              [0x14, 0xFF, 0x14, 0x00], 2, ref echecs);
        Suite("SBB8 chaine : la branche FLAGS_SBC8 de CF_SET",
              [0x1C, 0xFF, 0x1C, 0x00], 2, ref echecs);

        // (i) LA RETENUE PARESSEUSE A TRAVERS LE GROUPE IMMEDIAT.
        //
        //     Les sous-opcodes /2 (ADC) et /3 (SBB) de 80/81/82/83 lisent CF_SET()
        //     DANS le switch de ARITH_MULTI. Meme angle mort qu'en (f) : le mode
        //     simple ne leur donne jamais un flags_op paresseux. Ces quatre
        //     suites sont la seule chose qui exerce ce chemin-la, et la derniere
        //     est aussi le seul endroit ou 0x82 — l'alias non documente de 0x80 —
        //     est nomme dans un cas dirige.
        Suite("ADD16 pose la retenue, 83 /2 (ADC imm8 etendu) la lit",
              [0x05, 0xFF, 0xFF, 0x83, 0xD0, 0x00], 2, ref echecs);
        Suite("SUB16 pose l'emprunt, 81 /3 (SBB imm16) le lit",
              [0x2D, 0xFF, 0xFF, 0x81, 0xD8, 0x00, 0x00], 2, ref echecs);
        Suite("ADD8 pose la retenue, 80 /2 (ADC imm8) la lit",
              [0x04, 0xFF, 0x80, 0xD0, 0x00], 2, ref echecs);
        Suite("SUB8 pose l'emprunt, 82 /3 le lit — l'ALIAS de 0x80",
              [0x2C, 0xFF, 0x82, 0xD8, 0x00], 2, ref echecs);

        // (j) TEST sur le chemin du piege. Meme espece paresseuse que OR — ZN —
        //     mais posee par un autre handler, et sans ecriture.
        Cas("piege apres TEST16", [0x85, 0xC3], ref echecs, 0x0102);
        Cas("piege apres TEST8", [0x84, 0xC3], ref echecs, 0x0102);

        // (k) LES SEIZE CONDITIONS, LUES SUR UN ETAT PARESSEUX.
        //
        //     A5 met VF_SET, CF_SET, ZF_SET, NF_SET et PF_SET sur le chemin
        //     chaud. Le fuzzeur en mode simple ne peut exercer que leur DERNIERE
        //     branche — celle qui lit cpu_state.flags — parce qu'il repart d'un
        //     reset a chaque iteration et que flags_op y vaut FLAGS_UNKNOWN.
        //
        //     Un Jcc qui suit un ADD, un SUB ou un OR dans la MEME suite lit au
        //     contraire une espece paresseuse reelle. Seize conditions x trois
        //     poseurs x deux jeux de valeurs, dont un qui deborde en signe : c'est
        //     ce qui separe un VF_SET juste d'un zero constant, et c'est la seule
        //     chose qui distingue JL de JB.
        (byte[] code, string nom)[] poseurs =
        [
                ([0x05, 0xFF, 0xFF], "ADD16 AX,FFFF"),
                ([0x2D, 0x01, 0x00], "SUB16 AX,1"),
                ([0x0D, 0x00, 0x00], "OR16 AX,0"),
        ];
        string[] noms = ["JO","JNO","JB","JNB","JE","JNE","JBE","JNBE",
                         "JS","JNS","JP","JNP","JL","JNL","JLE","JNLE"];
        (ushort ax, ushort bx)[] valeurs = [(0x1234, 0x0010), (0x8000, 0x0000)];
        foreach (var (pose, nomPose) in poseurs)
                foreach (var (ax, bx) in valeurs)
                        for (var j = 0; j < 16; j++)
                        {
                                var code = new byte[pose.Length + 2];
                                pose.CopyTo(code, 0);
                                code[pose.Length] = (byte)(0x70 + j);
                                code[pose.Length + 1] = 0x02;      // deplacement +2
                                Suite($"{nomPose} puis {noms[j]} (AX={ax:X4})", code, 2, ref echecs,
                                      0, ax, bx);
                        }

        // (l) LES BOUCLES ET LE SAUT LOINTAIN.
        //
        //     LOOP decremente CX AVANT de le tester et facture ses cycles AVANT le
        //     test — donc aussi quand il ne saute pas. JCXZ ne decremente rien et
        //     porte des cycles EN DUR. Trois cas ou CX vaut 1 : la decrementation
        //     l'amene a zero et la boucle NE saute PAS, ce qu'un CX aleatoire ne
        //     produit qu'une fois sur 65 536.
        Cas("LOOP avec CX=1 : la decrementation l'amene a zero, il NE saute PAS",
            [0xE2, 0x02], ref echecs, 0, 0x1234, 0x0010, 1);
        Cas("LOOP avec CX=0 : il reboucle 65 535 fois", [0xE2, 0x02], ref echecs,
            0, 0x1234, 0x0010, 0);
        Cas("LOOPE avec CX=1 et ZF pose", [0xE1, 0x02], ref echecs, 0x0042, 0x1234, 0x0010, 1);
        Cas("JCXZ avec CX=0 : saute", [0xE3, 0x02], ref echecs, 0, 0x1234, 0x0010, 0);
        Cas("JCXZ avec CX=1 : ne saute pas", [0xE3, 0x02], ref echecs, 0, 0x1234, 0x0010, 1);
        Cas("JMP far : l'opcode du vecteur de reset", [0xEA, 0x00, 0x10, 0x00, 0x20],
            ref echecs);

        // (m) L'APPEL ET LE RETOUR, EN PAIRE.
        //
        //     Un CALL seul ne prouve rien : c'est le RETURN qui dit si ce qui a
        //     ete empile est ce qu'on attendait. Le fuzzeur en mode simple ne
        //     peut pas former la paire — meme angle mort qu'en (f) et (k).
        //
        //     CALL_FAR_w empile par la branche `else`, donc PUSH_W deux fois,
        //     SEIZE bits. La branche PUSH_L n'est prise qu'a travers une porte
        //     d'appel 32 bits, que seul le mode protege pose. Si l'une des deux
        //     largeurs etait fausse, le RETF d'apres reviendrait ailleurs.
        Suite("CALL far 1000:0008 puis RETF",
              [0x9A, 0x08, 0x00, 0x00, 0x10, 0xB8, 0x00, 0x00, 0xCB], 2, ref echecs);
        Suite("CALL near +3 puis RET near",
              [0xE8, 0x03, 0x00, 0xB8, 0x00, 0x00, 0xC3], 2, ref echecs);
        Suite("CALL far puis RETF imm : la pile recule de 4 + imm",
              [0x9A, 0x08, 0x00, 0x00, 0x10, 0xB8, 0x00, 0x00, 0xCA, 0x04, 0x00],
              2, ref echecs);

        // (n) IRET, MONTE A LA MAIN. Il depile ip, cs, flags — dans cet ordre —
        //     donc on empile flags, cs, ip. Trois PUSH imm puis le IRET.
        //     nmi_enable = 1 est son vrai effet de bord, et il est HORS des deux
        //     branches : il s'execute meme quand le mode protege a fait le
        //     travail.
        Suite("PUSH flags, PUSH cs, PUSH ip, puis IRET",
              [0x68, 0x02, 0x00, 0x68, 0x00, 0x10, 0x68, 0x20, 0x00, 0xCF],
              4, ref echecs);

        // (o) ENTER ET LEAVE. ENTER est le seul handler du groupe pile qui
        //     BOUCLE, et son modele de temps est CUMULE — instr_cycles, reads et
        //     writes s'accumulent, PREFETCH_RUN ne recoit le total qu'a la fin.
        //     `while (--count)` saute le premier tour : un ENTER de niveau 1 ne
        //     recopie aucun lien. Trois niveaux pour couvrir les trois cas.
        Suite("ENTER 4,0 puis LEAVE", [0xC8, 0x04, 0x00, 0x00, 0xC9], 2, ref echecs);
        Suite("ENTER 4,1 puis LEAVE — le tour saute", [0xC8, 0x04, 0x00, 0x01, 0xC9],
              2, ref echecs);
        Suite("ENTER 4,3 puis LEAVE — deux tours de boucle",
              [0xC8, 0x04, 0x00, 0x03, 0xC9], 2, ref echecs);

        // (p) LE GROUPE FF, sous-opcode par sous-opcode. Sept cas dont un
        //     illegal ; le fuzzeur les tire au hasard mais ne nomme jamais
        //     lequel il a vu.
        Cas("FF /0 : INC BX vers registre", [0xFF, 0xC3], ref echecs);
        Cas("FF /1 : DEC BX vers registre", [0xFF, 0xCB], ref echecs);
        Cas("FF /2 : CALL proche indirect", [0xFF, 0xD3], ref echecs);
        Cas("FF /4 : JMP proche indirect", [0xFF, 0xE3], ref echecs);
        Cas("FF /6 : PUSH vers adresse effective", [0xFF, 0xF3], ref echecs);
        Cas("FF /7 : illegal", [0xFF, 0xFB], ref echecs);
        Cas("FF /0 en MEMOIRE : INC [BX], timing_mm et non timing_mr",
            [0xFF, 0x07], ref echecs);

        // (q) LE GROUPE DRAPEAUX MET flags_rebuild SUR LE CHEMIN CHAUD.
        //
        //     CMC, CLC, STC, SAHF, LAHF et PUSHF l'appellent en premiere ligne,
        //     parce qu'ils manipulent cpu_state.flags DIRECTEMENT et qu'une
        //     representation paresseuse vivante rendrait ce champ perime. Mais en
        //     mode simple flags_op vaut FLAGS_UNKNOWN au depart, donc
        //     flags_rebuild y est un NO-OP : le fuzzeur les exerce tous sans
        //     jamais faire travailler la fonction.
        //
        //     Il faut un poseur de drapeaux JUSTE AVANT. Six especes paresseuses
        //     x six opcodes qui reconstruisent.
        (byte[] code, string nom)[] poseursQ =
        [
                ([0x05, 0xFF, 0xFF], "ADD16"),
                ([0x2D, 0x01, 0x00], "SUB16"),
                ([0x15, 0xFF, 0xFF], "ADC16"),
                ([0x1D, 0x01, 0x00], "SBB16"),
                ([0x0D, 0x00, 0x00], "OR16 (ZN16)"),
                ([0x04, 0xFF], "ADD8"),
        ];
        (byte op, string nom)[] lecteurs =
        [
                (0xF5, "CMC"), (0xF8, "CLC"), (0xF9, "STC"),
                (0x9E, "SAHF"), (0x9F, "LAHF"), (0x9C, "PUSHF"),
        ];
        foreach (var (pose, nomP) in poseursQ)
                foreach (var (op, nomL) in lecteurs)
                {
                        var code = new byte[pose.Length + 1];
                        pose.CopyTo(code, 0);
                        code[pose.Length] = op;
                        Suite($"{nomP} puis {nomL} : flags_rebuild travaille", code, 2,
                              ref echecs, 0, 0x8000, 0x0000);
                }

        // (r) CLD, STD ET CLI N'APPELLENT PAS flags_rebuild, et ce n'est pas un
        //     oubli de PCem : D_FLAG et I_FLAG ne font pas partie des six bits
        //     que la paresse couvre. Le masque 0x8d5 de flags_rebuild les laisse
        //     intacts. Ces trois cas verifient que poser D ou I APRES un ADD ne
        //     detruit pas le resultat paresseux — si l'un des deux cotes
        //     reconstruisait la ou l'autre ne le fait pas, flags_op divergerait.
        Suite("ADD16 puis CLD : la paresse doit SURVIVRE",
              [0x05, 0xFF, 0xFF, 0xFC], 2, ref echecs);
        Suite("ADD16 puis STD : la paresse doit SURVIVRE",
              [0x05, 0xFF, 0xFF, 0xFD], 2, ref echecs);
        Suite("ADD16 puis CLI : la paresse doit SURVIVRE",
              [0x05, 0xFF, 0xFF, 0xFA], 2, ref echecs);
        Suite("ADD16 puis STI : la paresse doit SURVIVRE",
              [0x05, 0xFF, 0xFF, 0xFB], 2, ref echecs);

        // (s) PUSHF puis POPF_286 : l'aller-retour. Le masque 0x0fd5 de la
        //     branche mode reel garde IOPL et NT de l'ancien etat ; si l'un des
        //     deux cotes prenait un autre des quatre masques, la valeur relue
        //     differerait. flags_extract a la fin remet flags_op a UNKNOWN.
        Suite("ADD16, PUSHF, POPF_286 : l'aller-retour des drapeaux",
              [0x05, 0xFF, 0xFF, 0x9C, 0x9D], 3, ref echecs);
        Suite("SAHF depuis AH puis LAHF vers AH",
              [0x9E, 0x9F], 2, ref echecs, 0, 0xD500, 0x0010);

        // (t) INC ET DEC NE TOUCHENT PAS LA RETENUE, et c'est TOUT ce qui les
        //     separe d'un ADD 1 ou d'un SUB 1. Le mecanisme est dans les poseurs :
        //     setadd16nc appelle flags_rebuild_c() AVANT de poser l'operation et
        //     pose FLAGS_INC16 — CF_SET, voyant cette espece, ira relire le C
        //     MATERIALISE plutot que de le recalculer.
        //
        //     En mode simple le fuzzeur ne peut pas le voir : flags_op vaut
        //     FLAGS_UNKNOWN au depart, donc flags_rebuild_c n'a rien a sauver.
        //     Il faut poser une retenue AVANT, puis INC, puis la RELIRE.
        foreach (var (pose, nomP) in new (byte[] code, string nom)[] {
                     ([0x05, 0xFF, 0xFF], "ADD16 (retenue posee)"),
                     ([0x2D, 0x01, 0x00], "SUB16 (pas d'emprunt)"),
                 })
                foreach (var (op, nomO) in new (byte op, string nom)[] {
                             (0x40, "INC AX"), (0x48, "DEC AX"),
                             (0x43, "INC BX"), (0x4B, "DEC BX"),
                         })
                {
                        var code = new byte[pose.Length + 3];
                        pose.CopyTo(code, 0);
                        code[pose.Length] = op;
                        code[pose.Length + 1] = 0x15;      // ADC AX, imm16
                        code[pose.Length + 2] = 0x00;
                        Suite($"{nomP}, {nomO}, puis ADC relit la retenue", code, 3,
                              ref echecs, 0, 0x8000, 0x7FFF);
                }

        // (u) LE PASSAGE 0x7FFF -> 0x8000. setflags est appele AVANT que le
        //     registre ne bouge, donc les drapeaux decrivent l'ANCIENNE valeur.
        //     Inverser les deux lignes de la macro changerait VF_SET ici, et
        //     NULLE PART AILLEURS.
        Cas("INC AX avec AX=7FFF : debordement signe", [0x40], ref echecs, 0, 0x7FFF, 0);
        Cas("DEC AX avec AX=8000 : debordement signe", [0x48], ref echecs, 0, 0x8000, 0);
        Cas("INC AX avec AX=FFFF : passe a zero sans toucher CF", [0x40], ref echecs,
            0x0001, 0xFFFF, 0);

        // (v) FE : UN HANDLER POUR DEUX INSTRUCTIONS, separees par `rmdat & 0x38`.
        //     Le test est BINAIRE — tout ce qui n'est pas zero est un DEC — donc
        //     il n'y a pas de sous-opcode illegal, contrairement a FF.
        Cas("FE /0 : INC BL", [0xFE, 0xC3], ref echecs);
        Cas("FE /1 : DEC BL", [0xFE, 0xCB], ref echecs);
        Cas("FE /5 : DEC aussi — le test est binaire", [0xFE, 0xEB], ref echecs);
        Cas("FE /0 en MEMOIRE : INC [BX]", [0xFE, 0x07], ref echecs);

        // (w) XCHG NE POSE AUCUN DRAPEAU, et 0x90 n'est PAS XCHG AX,AX : la table
        //     y met opNOP, qui vit dans x86_ops_misc.h. C'est le seul trou de la
        //     bande 90-97, verifie sur ops_286[] et non suppose.
        Cas("XCHG AX, BX", [0x93], ref echecs);
        Cas("XCHG BL, [BX] : l'ordre lire / ecrire / poser", [0x86, 0x1F], ref echecs);
        Suite("ADD16 puis XCHG : aucun drapeau ne doit bouger",
              [0x05, 0xFF, 0xFF, 0x93], 2, ref echecs);

        // (g) LE MEME ENCODAGE QUE (b), ET IL NE DOIT PAS ABANDONNER.
        //
        //     Le cas (b) — 89 06 FF FF, MOV [FFFF], AX — abandonne : opMOV_w_r_a16
        //     porte CHECK_WRITE(seg, ea, ea+1), qui teste la LIMITE d'offset.
        //     39 06 FF FF, CMP [FFFF], AX, est le meme ModRM au meme offset et
        //     doit aller au bout : CMP ne porte que SEG_CHECK_READ, qui teste
        //     `base == 0xffffffff` et rien d'autre.
        //
        //     Ce que le cas verifie donc n'est pas un abandon mais son ABSENCE,
        //     plus la lecture mot a cheval sur 0xFFFF et le journal d'ecritures
        //     VIDE — CMP ne reecrit pas son operande. Lire `pc 0004` et non
        //     `pc 9090` est le resultat attendu.
        //
        //     (SEG_CHECK_READ et SEG_CHECK_WRITE testent la meme condition ;
        //     seule la chaine passee a x86gpf differe, et elle va au pclog. En
        //     mode reel les deux sont indiscernables — c'est l'absence de
        //     CHECK_WRITE qui separe CMP de opMOV_w_r_a16, pas le choix entre
        //     les deux SEG_CHECK_*.)
        Cas("pas d'abandon : CMP [FFFF], AX par ModRM direct",
            [0x39, 0x06, 0xFF, 0xFF], ref echecs);

        // (h) CMP SUR LE CHEMIN DU PIEGE. Meme espece que SUB — setsub16 — mais
        //     atteinte par un autre handler, et sans ecriture du resultat : c'est
        //     la matérialisation qui doit voir exactement les memes op1/op2.
        Cas("piege apres CMP16", [0x39, 0xC3], ref echecs, 0x0102);
        Cas("piege apres CMP8", [0x38, 0xC3], ref echecs, 0x0102);

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

    /// <summary>Un cas DIRIGÉ d'UNE instruction, comparé à l'oracle sur les 60
    /// champs. Les deux côtés reçoivent le même code au même endroit et le même
    /// état de registres.</summary>
    private static void Cas(string nom, byte[] code, ref int echecs, ushort flags = 0,
                            ushort ax = 0x1234, ushort bx = 0x0010, ushort cx = 0)
        => Suite(nom, code, 1, ref echecs, flags, ax, bx, cx);

    /// <summary>Le même, sur PLUSIEURS instructions d'affilée — et c'est le seul
    /// moyen d'exercer ce qu'une instruction laisse à la suivante.
    ///
    /// La comparaison a lieu APRÈS CHAQUE PAS, pas seulement au bout : si le
    /// second diverge, on veut lire que le premier était bon. Un seul état
    /// comparé à la fin dirait « divergence » sans dire laquelle des deux.</summary>
    private static void Suite(string nom, byte[] code, int pas, ref int echecs,
                              ushort flags = 0, ushort ax = 0x1234, ushort bx = 0x0010,
                              ushort cx = 0)
    {
        var regs = new ushort[(int)iXtal26.Diag.R.COUNT];
        for (var i = 0; i < regs.Length; i++)
                regs[i] = 0;
        regs[(int)iXtal26.Diag.R.CS] = 0x1000;
        regs[(int)iXtal26.Diag.R.SS] = 0x2000;
        regs[(int)iXtal26.Diag.R.DS] = 0x3000;
        regs[(int)iXtal26.Diag.R.SP] = 0x0100;
        regs[(int)iXtal26.Diag.R.AX] = ax;
        regs[(int)iXtal26.Diag.R.BX] = bx;
        regs[(int)iXtal26.Diag.R.CX] = cx;
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

        var a = iXtal26.Diag.HState.Create();
        var b = iXtal26.Diag.HState.Create();

        for (var n = 1; n <= pas; n++)
        {
                var cycC = Oracle.h_step();
                var cycS = _386.Step286();

                Oracle.h_getstate(out a);
                _808x.GetState(ref b);

                var diff = Fuzzer.Compare(a, b, cycC, cycS);
                if (diff is not null)
                {
                        var ou = pas == 1 ? "" : $" (pas {n}/{pas})";
                        Console.WriteLine($"  [ECHEC] {nom}{ou} : {diff}");
                        echecs++;
                        return;
                }
        }

        Console.WriteLine($"  [ok] {nom} : identique, pc {b.pc:X4} CS {b.seg_sel[1]:X4} " +
                          $"AX {b.regs[0]:X4} BX {b.regs[3]:X4} flags {b.flags:X4}");
    }
}
