// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — outillage de vérification.
//
// DIFF DE TRACE D'AMORÇAGE.
//
// Le fuzzer compare une instruction tirée au sort dans un état tiré au sort. Il
// ne dit rien d'une machine qui démarre : là, ce sont des millions
// d'instructions enchaînées, où l'état de l'une conditionne la suivante, et où
// une divergence se propage jusqu'à devenir illisible.
//
// D'où la forme en deux phases du plan :
//   1. chaque côté émet UN HACHAGE de 8 octets par instruction — 3 Mo/s au lieu
//      de 44 — et on cherche le premier index qui diffère ;
//   2. on rejoue les deux côtés autour de cet index avec l'état complet.
//
// Le hachage porte sur ce que les deux côtés peuvent reproduire exactement :
// CS, pc, les huit registres, DS/ES/SS, flags, tsc. TraceHash() doit rester le
// pendant EXACT de h_trace_note() (tools/oracle/harness.c) — même champs, même
// ordre, même FNV-1a — sans quoi la phase 1 signale une divergence de hachage là
// où les machines sont d'accord.

using iXtal26.Cpu;
using iXtal26.Host;
using iXtal26.Memory;

namespace iXtal26.Diff;

public static class BootDiff
{
    /// <param name="discA">Image de disquette du lecteur A, ou null : la MÊME image
    /// est montée des deux côtés, avant l'amorçage, comme pc.c le fait depuis argv
    /// (pc.c:231) — c'est la seule façon de comparer un amorçage DOS.</param>
    /// <param name="configPath">Fichier de configuration, ou null pour les défauts.
    /// C'est CET OUTIL qui le lit, une fois, et qui pousse ensuite chaque scalaire des
    /// DEUX côtés — jamais initpc de son côté et h_boot du sien. Deux lectures
    /// indépendantes, ce sont deux résolutions de chemin qui peuvent trouver deux
    /// fichiers différents : une divergence de configuration déguisée en divergence de
    /// cœur, et le harnais ne saurait pas faire la différence.</param>
    /// <param name="discB">Image du lecteur B. C'est là que va la disquette VIERGE
    /// d'un FORMAT : on ne formate pas le disque système sous les pieds de DOS.</param>
    /// <param name="types">Lignes à taper, une fois l'invite atteinte. C'est ce qui
    /// met le chemin d'ÉCRITURE du contrôleur sous comparaison — FORMAT et WRITE DATA
    /// ne s'atteignent pas autrement, et jusqu'ici l'oracle ne savait pas taper.</param>
    /// <param name="model">Nom interne de machine, ou null pour laisser parler la
    /// configuration. Appliqué APRÈS elle : la ligne de commande l'emporte.</param>
    /// <param name="typeAt">Tranche de la première frappe : le temps d'amorcer.</param>
    /// <param name="typeSettle">Tranches laissées à l'application après chaque
    /// Entrée. Le défaut suffit à un DIR ; un FORMAT en demande des milliers, et
    /// sans cela la comparaison d'images trouve une image INCHANGÉE — accord vide
    /// que CompareImages signale.</param>
    public static int Run(string romsPath, int slices, string? discA = null, string? configPath = null,
                          string? discB = null, IReadOnlyList<string>? types = null, int typeAt = 0,
                          int typeSettle = KeyScript.SlicesAfterLine, string? model = null)
    {
        Oracle.CheckAbi();

        if (configPath is not null && !pc.loadconfig(configPath))
            return 2;

        // APRÈS --config, comme dans le programme principal : la ligne de commande
        // l'emporte sur le fichier, quel que soit l'ordre de frappe.
        if (model is not null && !pc.setmodel(model))
            return 2;

        // La MACHINE en tête : depuis M10 le dépôt en a deux, et un diff qui ne dit pas
        // laquelle il compare laisse croire qu'il n'y en a qu'une.
        Console.WriteLine($"Configuration : machine = {Models.model_c.model_get_internal_name()}, " +
                          $"mem_size = {pc.cfg_mem_size} Ko, lecteurs {pc.cfg_drive_type[0]}/{pc.cfg_drive_type[1]}" +
                          (configPath is null ? " (défaut)" : $" ({configPath})"));

        // LE NUMÉRO DE PROCESSUS DANS LE NOM, et ce n'est pas de la précaution : ce
        // chemin était FIXE, comme celui des copies d'images de CopyForSide. Deux
        // boot-diff lancés en même temps écrivaient donc la même trace et les mêmes
        // copies, et le second lisait ce que le premier venait d'écrire.
        //
        // Constaté à M13, et le symptôme est sournois : une campagne de régression
        // lancée pendant une campagne à disque dur a rendu « PREMIÈRE DIVERGENCE à
        // l'instruction 21 » sur le 5150, puis « ÉCART DE LONGUEUR : oracle 35 622 286,
        // C# 26 750 702 » — 35 622 286 étant le compte de l'AUTRE campagne. Un rouge
        // entièrement fabriqué par le harnais, qui envoie chercher une régression qui
        // n'existe pas. Rejouées en série, les deux portes rendent leur chiffre exact.
        var oraclePath = Path.Combine(Path.GetTempPath(),
                                      $"ixtal-boot-oracle-{Environment.ProcessId}.bin");

        // LE CALENDRIER DE FRAPPE, construit UNE fois et rejoué à l'identique des deux
        // côtés. Purement arithmétique : il ne dépend d'aucun état de la machine, donc
        // les deux côtés le suivent sans avoir à se parler. C'est ce qui rend une frappe
        // comparable — et sans frappe, FORMAT et WRITE DATA restent hors d'atteinte.
        var script = new List<KeyScript.Event>();
        if (types is not null && types.Count > 0)
        {
            var (evts, endSlice) = KeyScript.Build(types, typeAt, typeSettle);
            script = evts;
            if (endSlice > slices)
            {
                Console.WriteLine($"  frappe : {script.Count} évènements de la tranche {typeAt} à {endSlice} ; " +
                                  $"tranches portées de {slices} à {endSlice}");
                slices = endSlice;
            }
            else
            {
                Console.WriteLine($"  frappe : {script.Count} évènements de la tranche {typeAt} à {endSlice}");
            }
        }

        // UNE COPIE PAR CÔTÉ. Les deux cœurs écrivent pour de vrai sur l'image montée :
        // leur donner le même fichier ferait lire au second ce que le premier vient
        // d'écrire, et la divergence serait fabriquée par le harnais. Les comparer
        // ensuite octet par octet est le vrai oracle du chemin d'écriture — le diff
        // d'instructions dit que les deux font pareil, les images disent ce qu'elles ont
        // produit.
        // --fda / --fdb l'emportent, mais la clé disc_a du fichier de configuration
        // compte aussi : loadconfig l'a déjà posée dans discfns[]. Avant ce correctif,
        // BootDiff ÉCRASAIT discfns[] par la valeur de --fda — donc "" quand l'option
        // était absente — et un `boot-diff --config` dont le .cfg monte une disquette
        // démarrait sans. Les deux côtés étant écrasés pareil, le diff restait VERT :
        // il comparait deux machines également amputées. Trouvé à M12, en cherchant
        // pourquoi un FORMAT ne s'exécutait pas ; introduit à M11 avec les copies par
        // côté, et invisible jusque-là parce que toutes les campagnes disquette
        // passaient --fda explicitement.
        discA ??= Floppy.fdd_c.discfns[0].Length == 0 ? null : Floppy.fdd_c.discfns[0];
        discB ??= Floppy.fdd_c.discfns[1].Length == 0 ? null : Floppy.fdd_c.discfns[1];

        var oracleA = CopyForSide(discA, "oracle-a");
        var oracleB = CopyForSide(discB, "oracle-b");
        var csharpA = CopyForSide(discA, "csharp-a");
        var csharpB = CopyForSide(discB, "csharp-b");

        // Le DISQUE DUR suit la même règle, et il en a d'autant plus besoin : un
        // FDISK ou un FORMAT C: écrit vraiment, et les deux côtés partageant un
        // fichier, le second lirait ce que le premier vient d'écrire. Le chemin vient
        // du fichier de configuration, pas d'une option : la clé hdc_fn le porte déjà.
        // D: SUIT C:, et pas par symétrie décorative : ide_fn[1] partait tel quel à
        // l'oracle pendant que le C# lisait le même fichier. Inerte tant que D: n'a
        // pas d'image — d'où le « Cannot open file '' » bénin — mais c'est le défaut
        // même qu'on vient de fermer pour A:, B: et C:, en attente d'un second disque.
        var discC = Disc.hdd_c.ide_fn[0].Length == 0 ? null : Disc.hdd_c.ide_fn[0];
        var discD = Disc.hdd_c.ide_fn[1].Length == 0 ? null : Disc.hdd_c.ide_fn[1];
        var oracleC = CopyForSide(discC, "oracle-c");
        var csharpC = CopyForSide(discC, "csharp-c");
        var oracleD = CopyForSide(discD, "oracle-d");
        var csharpD = CopyForSide(discD, "csharp-d");

        Console.WriteLine($"Amorçage de l'oracle C ({slices} tranches" +
                          (discA is null ? "" : $", A: = {discA}") +
                          (discB is null ? "" : $", B: = {discB}") + ")…");
        Oracle.h_set_discfn(0, oracleA ?? "");
        Oracle.h_set_discfn(1, oracleB ?? "");
        Oracle.h_set_mem_size(pc.cfg_mem_size);
        Oracle.h_set_drive_type(0, pc.cfg_drive_type[0]);
        Oracle.h_set_drive_type(1, pc.cfg_drive_type[1]);
        Oracle.h_set_bpb_disable(Disc.disc_img.bpb_disable);
        Oracle.h_set_romset(pc.romset);
        // LES DEUX CÔTÉS DOIVENT CHOISIR LE MÊME CŒUR, et avant h_boot : celui-ci
        // fait `AT = (h_core == H_CORE_286)` (harness.c:362), donc l'ordre compte.
        // Le discriminant est le flag du modèle, connu AVANT l'amorçage — pas la
        // globale AT, que resetpchard ne pose que pendant.
        Oracle.h_set_core(CoeurDuModele());
        Oracle.h_set_hdd_controller(pc.cfg_hdd_controller);
        Oracle.h_set_hdd(0, oracleC ?? "", Disc.hdd_c.hdc[0].spt,
                         Disc.hdd_c.hdc[0].hpc, Disc.hdd_c.hdc[0].tracks);
        Oracle.h_set_hdd(1, oracleD ?? "", Disc.hdd_c.hdc[1].spt,
                         Disc.hdd_c.hdc[1].hpc, Disc.hdd_c.hdc[1].tracks);
        if (Oracle.h_boot(romsPath) == 0)
        {
            Console.Error.WriteLine($"L'oracle n'a pas pu charger le BIOS depuis « {romsPath} ».");
            return 1;
        }
        if (Oracle.h_trace_open(oraclePath) == 0)
        {
            Console.Error.WriteLine($"Impossible d'écrire {oraclePath}.");
            return 1;
        }
        var e = 0;
        for (var i = 0; i < slices; i++)
        {
            // Les touches sont posées AVANT la tranche, comme BootTest les pose avant
            // runpc() : c'est keyboard_poll_host, à la fin de la tranche, qui les voit.
            while (e < script.Count && script[e].Slice == i)
            {
                Oracle.h_rawinputkey(script[e].Index, script[e].Value);
                e++;
            }
            Oracle.h_runpc();
            // pc.c:490-491 — runpc() appelle keyboard_poll_host puis keyboard_process
            // APRÈS execx86. h_runpc ne le fait pas ; on le fait ici, au même point.
            Oracle.h_kbd_process();
        }
        Oracle.h_trace_close();
        Oracle.h_closepc();

        var oracleTrace = File.ReadAllBytes(oraclePath);
        var nOracle = oracleTrace.Length / 8;
        Console.WriteLine($"  oracle : {nOracle} instructions tracées");

        // Supprimée dès qu'elle est en mémoire. Tant que le nom était fixe, chaque
        // campagne écrasait la précédente et /tmp ne grossissait pas ; maintenant qu'il
        // porte le numéro de processus, il faut le dire. Huit octets par instruction,
        // donc 792 Mo pour l'arc de § M12 — on ne laisse pas cela derrière soi. Les
        // copies d'images, elles, RESTENT : ce sont les pièces qu'on veut relire après
        // coup, et elles ne pèsent que la taille du disque émulé.
        try { File.Delete(oraclePath); }
        catch (IOException) { /* laissée sur place : sans conséquence pour la suite */ }

        Console.WriteLine($"Amorçage du cœur C# ({slices} tranches)…");
        _808x.ResetDiagState();
        // L'ÉTAT DE PRÉFETCH DU 286 SE REPORTAIT D'UNE PHASE À L'AUTRE, ET LES DEUX
        // CÔTÉS ARRIVAIENT SALES DIFFÉREMMENT.
        //
        // prefetch_bytes et prefetch_prefixes sont des statiques de 386_dynarec.c que
        // NI h_boot NI pc.initpc ne remettent — PCem n'amorce qu'une fois par processus,
        // le diff deux fois : la phase 1 court l'amorçage entier, la phase 2 le rejoue
        // en pas à pas. Chaque côté entrait donc en phase 2 avec ce que SON propre
        // phase 1 avait laissé, et comme elle divergeait, les deux restes différaient.
        //
        // MESURÉ : au premier boot-diff AT, tsc valait 11 côté oracle et 13 côté C# à
        // l'instruction 0, CS:IP F000:FFF0. Onze est exactement timing_jmp_rm, que les
        // DEUX côtés posent à 11 — vérifié. opJMP_far_a16 et prefetch_run sont des
        // transcriptions fidèles — vérifiées ligne à ligne. Les quatre entrées du modèle
        // de préfetch s'accordent — vérifiées. Il ne restait que l'état.
        //
        // h_prefetch_reset() était au contrat ABI depuis A2.2a sans jamais être importé.
        // Appelé sans condition de cœur, comme h_reset le fait (harness.c:391) : sur un
        // 8088 ces statiques ne servent à personne, execx86 portant son propre modèle.
        Oracle.h_prefetch_reset();
        Cpu._386.prefetch_reset();
        Oracle.h_seg_clear_residue();
        Cpu._386.ClearSegResidue();
        Floppy.fdd_c.discfns[0] = csharpA ?? "";
        Floppy.fdd_c.discfns[1] = csharpB ?? "";
        Disc.hdd_c.ide_fn[0] = csharpC ?? "";
        Disc.hdd_c.ide_fn[1] = csharpD ?? "";
        if (!pc.initpc(romsPath))
            return 1;

        var diverged = -1;
        var n = 0;
        var ev = 0;
        for (var s = 0; s < slices && diverged < 0; s++)
        {
            while (ev < script.Count && script[ev].Slice == s)
            {
                Keyboard.keyboard.rawinputkey[script[ev].Index] = script[ev].Value;
                ev++;
            }

            var budget = pc.cpu_get_speed() / 100;
            while (budget > 0)
            {
                budget -= PasCsharpTrace();
                var h = TraceHash();
                if (n < nOracle)
                {
                    var o = BitConverter.ToUInt64(oracleTrace, n * 8);
                    if (o != h) { diverged = n; break; }
                }
                n++;
            }

            Keyboard.keyboard.keyboard_poll_host();
            Keyboard.keyboard.keyboard_process();
        }

        pc.closepc();

        if (diverged < 0)
        {
            // Un vert ne vaut que si les DEUX côtés ont exécuté le même nombre
            // d'instructions dans le même budget de cycles. Si le C# en fait moins,
            // la queue de la trace oracle n'a jamais été comparée et « identiques »
            // décrirait un préfixe, pas la course.
            if (n != nOracle)
            {
                Console.Error.WriteLine(
                    $"\nÉCART DE LONGUEUR : oracle {nOracle} instructions, C# {n}, " +
                    "à budget de cycles égal. Les hachages concordent sur le préfixe " +
                    "commun ; c'est la comptabilité de cycles qui diverge.");
                return 1;
            }

            Console.WriteLine($"\nVert : {n} instructions, les deux amorçages sont identiques.");

            // L'écran du côté C#, quand on a tapé : un accord parfait sur une image
            // inchangée laisse la question « pourquoi la frappe n'a-t-elle rien
            // produit ? » sans réponse, et c'est là qu'elle se lit.
            if (script.Count > 0)
                iXtal26.BootTest.DumpTextScreen();

            // Le diff d'instructions dit que les deux cœurs font la même chose. Les
            // images disent ce qu'ils ont ÉCRIT — et c'est le seul oracle que le chemin
            // d'écriture ait jamais eu.
            // `|` ET PAS `&`, et c'est un correctif, pas un style. CompareImages rend 1
            // en cas de divergence et 0 sinon, y compris quand rien n'est monté ; un
            // `&` entre trois codes de sortie ne valait donc 1 que si les QUATRE images
            // divergeaient EN MÊME TEMPS, et un lecteur vide suffisait à masquer les
            // autres. Le verdict imprimé, lui, a toujours été juste : la porte des
            // images de M11 était lue à l'œil, jamais par le code de sortie. C'est
            // `|` qui la rend exécutable. Bitwise et non `||` : les quatre lignes
            // doivent s'imprimer, y compris après la première divergence.
            return CompareImages(discA, oracleA, csharpA, "A:")
                 | CompareImages(discB, oracleB, csharpB, "B:")
                 | CompareImages(discC, oracleC, csharpC, "C: (disque dur)")
                 | CompareImages(discD, oracleD, csharpD, "D: (disque dur)");
        }

        Console.WriteLine($"\nPREMIÈRE DIVERGENCE à l'instruction {diverged}");
        Console.WriteLine("Phase 2 — rejeu en pas à pas, état complet :\n");
        return Phase2(romsPath, diverged, discA);
    }

    /// <summary>Copie une image dans un fichier temporaire propre à un côté, ou rend
    /// null si aucune image n'est montée. Les deux cœurs écrivent pour de vrai : sans
    /// cette copie, le second lirait ce que le premier a écrit.
    ///
    /// Le numéro de processus est dans le nom pour la MÊME raison, d'un cran plus haut :
    /// deux boot-diff concurrents partageaient ces copies, donc les deux campagnes
    /// écrivaient sur les mêmes images. Voir le commentaire d'oraclePath.</summary>
    /// <summary>Le cœur que ce modèle réclame, lu sur son flag MODEL_AT.
    ///
    /// pcem: pc.c:484 — `AT ? exec386 : execx86`. On ne peut pas lire la globale
    /// `AT` ici : elle est posée par model_init, pendant resetpchard, donc après
    /// que l'oracle ait eu besoin de la réponse.</summary>
    /// <summary>Un pas sur le cœur que le modèle réclame, et le pendant exact de
    /// h_step() qui aiguille sur h_core depuis A2.0.
    ///
    /// CE MANQUAIT, ET C'ÉTAIT LA PREMIÈRE DIVERGENCE DU PREMIER boot-diff AT :
    /// `tsc, oracle 11, C# 105` à l'instruction 0, CS:IP F000:FFF0. Onze est
    /// exactement timing_jmp_rm, le JMP FAR du vecteur de reset d'un AT ; le 105 était
    /// le 8088 exécutant des octets qui ne sont pas pour lui. L'étape 1 avait fait
    /// aiguiller h_runpc côté oracle ; ce côté-ci ne suivait pas.
    ///
    /// Step286 porte la DEVIATION du pas-à-pas — timer_target posé à tsc pour rendre
    /// cycle_period égal à 1 — et c'est voulu ici : la phase 2 compare l'état APRÈS
    /// CHAQUE instruction, donc un pas doit valoir exactement une instruction. h_step
    /// fait le même geste au même endroit (h_step286, harness.c:543).</summary>
    private static int PasCsharp()
        => CoeurDuModele() == Oracle.Core286 ? Cpu._386.Step286() : _808x.Step();

    /// <summary>Le pas de la PHASE 1, qui n'est pas celui de la phase 2.
    ///
    /// h_runpc en mode tracé fait `cpu_state._cycles = 1; exec386(0);` SANS poser
    /// timer_target — voir Step286Trace. h_step, lui, passe par h_step286 qui le pose.
    /// Les deux phases doivent donc appeler des steppers différents, et les appeler
    /// tous deux Step286 rendait la phase 1 incohérente avec la phase 2.
    ///
    /// Sur un 8088 il n'y a qu'un pas : execx86 n'a pas de boucle interne bornée par
    /// cycle_period, donc _808x.Step() vaut pour les deux phases — et c'est pourquoi le
    /// défaut n'existait pas avant l'AT.</summary>
    private static int PasCsharpTrace()
        => CoeurDuModele() == Oracle.Core286 ? Cpu._386.Step286Trace() : _808x.Step();

    private static int CoeurDuModele()
        => (Models.model_c.models[Models.model_c.model].flags & Models.model_c.MODEL_AT) != 0
                ? Oracle.Core286 : Oracle.Core8088;

    private static string? CopyForSide(string? src, string tag)
    {
        if (src is null)
            return null;

        var dst = Path.Combine(Path.GetTempPath(),
                               $"ixtal-{tag}-{Environment.ProcessId}-{Path.GetFileName(src)}");
        File.Copy(src, dst, overwrite: true);
        return dst;
    }

    /// <summary>Compare les deux images produites. Rend 0 si elles concordent — ou si
    /// rien n'était monté. Une image identique à celle de DÉPART est signalée : deux
    /// disquettes intactes se ressemblent parfaitement, et ne prouvent rien du chemin
    /// d'écriture.</summary>
    private static int CompareImages(string? src, string? a, string? b, string label)
    {
        if (src is null || a is null || b is null)
            return 0;

        var oa = File.ReadAllBytes(a);
        var ob = File.ReadAllBytes(b);
        var orig = File.ReadAllBytes(src);

        if (oa.Length != ob.Length)
        {
            Console.Error.WriteLine($"Image {label} : tailles différentes, oracle {oa.Length}, C# {ob.Length}.");
            return 1;
        }

        var diffs = 0;
        var first = -1;
        for (var i = 0; i < oa.Length; i++)
            if (oa[i] != ob[i])
            {
                if (first < 0) first = i;
                diffs++;
            }

        if (diffs != 0)
        {
            Console.Error.WriteLine(
                $"Image {label} : {diffs} octet(s) divergent(s), le premier en 0x{first:X}" +
                $" — oracle {oa[first]:X2}, C# {ob[first]:X2}.");
            return 1;
        }

        var touched = 0;
        if (orig.Length == oa.Length)
            for (var i = 0; i < oa.Length; i++)
                if (oa[i] != orig[i])
                    touched++;

        Console.WriteLine(touched == 0
            ? $"Image {label} : identique des deux côtés — mais INCHANGÉE depuis le départ. " +
              "Le chemin d'écriture n'a pas été exercé ; cet accord ne prouve rien."
            : $"Image {label} : identique des deux côtés, {touched} octet(s) écrits par l'invité.");
        return 0;
    }

    /// <summary>
    /// Phase 2 : on rejoue les DEUX cœurs depuis le reset, une instruction à la
    /// fois, et on compare le vecteur d'état complet à l'index fautif. La phase 1
    /// dit OÙ ; celle-ci dit QUOI.
    /// </summary>
    private static int Phase2(string romsPath, int index, string? discA)
    {
        Oracle.h_set_discfn(0, discA ?? "");
        Oracle.h_set_mem_size(pc.cfg_mem_size);
        Oracle.h_set_drive_type(0, pc.cfg_drive_type[0]);
        Oracle.h_set_drive_type(1, pc.cfg_drive_type[1]);
        Oracle.h_set_bpb_disable(Disc.disc_img.bpb_disable);
        Oracle.h_set_romset(pc.romset);
        // LES DEUX CÔTÉS DOIVENT CHOISIR LE MÊME CŒUR, et avant h_boot : celui-ci
        // fait `AT = (h_core == H_CORE_286)` (harness.c:362), donc l'ordre compte.
        // Le discriminant est le flag du modèle, connu AVANT l'amorçage — pas la
        // globale AT, que resetpchard ne pose que pendant.
        Oracle.h_set_core(CoeurDuModele());
        Oracle.h_set_hdd_controller(pc.cfg_hdd_controller);
        for (var hd = 0; hd < 2; hd++)
            Oracle.h_set_hdd(hd, Disc.hdd_c.ide_fn[hd], Disc.hdd_c.hdc[hd].spt,
                             Disc.hdd_c.hdc[hd].hpc, Disc.hdd_c.hdc[hd].tracks);
        if (Oracle.h_boot(romsPath) == 0) return 1;
        _808x.ResetDiagState();
        // L'ÉTAT DE PRÉFETCH DU 286 SE REPORTAIT D'UNE PHASE À L'AUTRE, ET LES DEUX
        // CÔTÉS ARRIVAIENT SALES DIFFÉREMMENT.
        //
        // prefetch_bytes et prefetch_prefixes sont des statiques de 386_dynarec.c que
        // NI h_boot NI pc.initpc ne remettent — PCem n'amorce qu'une fois par processus,
        // le diff deux fois : la phase 1 court l'amorçage entier, la phase 2 le rejoue
        // en pas à pas. Chaque côté entrait donc en phase 2 avec ce que SON propre
        // phase 1 avait laissé, et comme elle divergeait, les deux restes différaient.
        //
        // MESURÉ : au premier boot-diff AT, tsc valait 11 côté oracle et 13 côté C# à
        // l'instruction 0, CS:IP F000:FFF0. Onze est exactement timing_jmp_rm, que les
        // DEUX côtés posent à 11 — vérifié. opJMP_far_a16 et prefetch_run sont des
        // transcriptions fidèles — vérifiées ligne à ligne. Les quatre entrées du modèle
        // de préfetch s'accordent — vérifiées. Il ne restait que l'état.
        //
        // h_prefetch_reset() était au contrat ABI depuis A2.2a sans jamais être importé.
        // Appelé sans condition de cœur, comme h_reset le fait (harness.c:391) : sur un
        // 8088 ces statiques ne servent à personne, execx86 portant son propre modèle.
        Oracle.h_prefetch_reset();
        Cpu._386.prefetch_reset();
        Oracle.h_seg_clear_residue();
        Cpu._386.ClearSegResidue();
        Floppy.fdd_c.discfns[0] = discA ?? "";
        if (!pc.initpc(romsPath)) return 1;

        var a = Diag.HState.Create();
        var b = Diag.HState.Create();
        var regs = new ushort[(int)Diag.R.COUNT];

        var pitFirst = -1;
        var a0 = Diag.HState.Create();
        var before = new ulong[3][];
        for (var t = 0; t < 3; t++) before[t] = new ulong[PitFields.Length];

        for (var i = 0; i <= index; i++)
        {
            // État du PIT AVANT l'instruction. Les deux côtés sont encore
            // d'accord ici : c'est la ligne de départ commune qui rend lisible
            // ce que l'instruction a fait diverger.
            if (pitFirst < 0)
                for (var t = 0; t < 3; t++)
                    Oracle.h_pit_probe(t, before[t]);

            // LES DEUX PHASES EMPRUNTENT LE MEME PAS, et c'est ce qui rend l'indice de
            // la phase 1 rejouable. h_step / Step286 posent timer_target pour forcer
            // cycle_period a 1 ; la boucle tracee de h_runpc ne le fait pas. Utiliser
            // l'un en phase 1 et l'autre en phase 2 faisait de la phase 2 une TROISIEME
            // execution, qui n'avait pas la divergence de la phase 1 au meme indice —
            // le diff le disait sans pouvoir la localiser.
            var cycC = Oracle.h_step_trace();
            var cycS = PasCsharpTrace();

            if (pitFirst < 0)
                Oracle.h_getstate(out a0);

            if (pitFirst < 0 && PitDiverges())
            {
                pitFirst = i;
                Console.WriteLine($"  [PIT] premier écart d'état à l'instruction {i}, " +
                                  $"CS:IP {a0.seg_sel[0]:X4}:{a0.oldpc:X4} — soit {index - i} " +
                                  "instructions AVANT la divergence architecturale.");
                DumpPitFull(before);
                Console.WriteLine();
            }

            if (i < index - 3)
                continue;

            Oracle.h_getstate(out a);
            _808x.GetState(ref b);
            Oracle.h_getregs(regs);

            var mark = i == index ? ">>" : "  ";
            Console.WriteLine($"{mark} #{i}  oracle CS:IP {a.seg_sel[0]:X4}:{a.pc:X4} " +
                              $"AX {a.regs[0]:X4} BX {a.regs[3]:X4} CX {a.regs[1]:X4} DX {a.regs[2]:X4} " +
                              $"FL {a.flags:X4} cyc {cycC}");
            Console.WriteLine($"{mark}      C#     CS:IP {b.seg_sel[0]:X4}:{b.pc:X4} " +
                              $"AX {b.regs[0]:X4} BX {b.regs[3]:X4} CX {b.regs[1]:X4} DX {b.regs[2]:X4} " +
                              $"FL {b.flags:X4} cyc {cycS}");

            var d = Fuzzer.CompareStates(a, b, cycC, cycS, counters: false);
            if (d is not null)
            {
                Console.WriteLine($"\n  -> {d}");
                DumpPit();
                DumpDisc();
                DumpSpeaker();
                var lin = (a.seg_base[0] + a.oldpc) & 0xFFFFF;
                var bo = new byte[6];
                Oracle.h_read(lin, bo, 6);
                Console.WriteLine($"  octets à cs:oldpc {lin:X5} : {string.Join(" ", bo.Select(x => x.ToString("X2")))}");
                return 1;
            }
        }

        Console.WriteLine("\n  Les états concordent à l'index signalé — le hachage porte sur moins de");
        Console.WriteLine("  champs que le vecteur complet ; vérifier TraceHash contre h_trace_note.");
        return 1;
    }

    /// <summary>Doit rester le pendant exact de h_trace_note() (harness.c).</summary>
    /// <summary>Retire tsc du hachage, des DEUX cotes. Voir harness.c pour le motif :
    /// separer une divergence de TEMPS d'une divergence FONCTIONNELLE. Drapeau de
    /// DIAGNOSTIC — un boot-diff sans tsc ne remplace pas un boot-diff complet.</summary>
    internal static bool SansTsc;

    internal static ulong TraceHash()
    {
        ulong h = 1469598103934665603UL;
        void Mix(ulong x)
        {
            for (var i = 0; i < 8; i++)
            {
                h ^= (x >> (i * 8)) & 0xff;
                h *= 1099511628211UL;
            }
        }
        var st = _386_common.cpu_state;
        Mix(st.seg_cs.seg);
        Mix(st.pc);
        for (var i = 0; i < 8; i++) Mix(st.regs[i].w);
        Mix(st.seg_ds.seg);
        Mix(st.seg_es.seg);
        Mix(st.seg_ss.seg);
        Mix(st.flags);
        if (!SansTsc)
                Mix(timer.tsc);
        return h;
    }
    private static readonly string[] PitFields =
    {
        "l", "m", "count", "rl", "using_timer", "gate", "enabled", "running", "disabled", "thit", "latched", "rereadlatch", "rm", "out", "timer.enabled", "timer.ts", "tsc", "PITCONST", "remaining",
    };

    /// <summary>Affiche l'état des trois canaux du PIT des deux côtés. La sonde
    /// n'instrumente pas le C : pit est une globale de pit.c et le harnais est
    /// lié avec, donc h_pit_probe() se contente de la lire.</summary>
    private static void DumpPit()
    {
        var oc = new ulong[PitFields.Length];
        var cs = new ulong[PitFields.Length];

        for (var t = 0; t < 3; t++)
        {
            Oracle.h_pit_probe(t, oc);
            iXtal26.Models.pit.Probe(t, cs);

            var diff = new List<string>();
            for (var f = 0; f < PitFields.Length; f++)
                if (oc[f] != cs[f])
                    diff.Add($"{PitFields[f]}: oracle {oc[f]} / C# {cs[f]}");

            Console.WriteLine(diff.Count == 0
                ? $"  PIT canal {t} : identique"
                : $"  PIT canal {t} : {string.Join("  |  ", diff)}");
        }
    }

    private static readonly string[] SpeakerFields =
    {
        "speaker_gated", "speaker_enable", "was_speaker_enable", "speakon", "speakval", "ppispeakon",
        "speaker_pos", "sound_pos_global", "sound_hash",
    };

    /// <summary>La graine FNV-1a, celle que sound_reset() pose des deux côtés. Une
    /// empreinte restée là signifie qu'aucun bloc n'a été produit : deux silences
    /// concordants ne prouvent rien, et c'est le faux vert que cette sonde existe
    /// pour attraper.</summary>
    private const ulong HashSeed = 1469598103934665603UL;

    /// <summary>Amorce les deux côtés sur N tranches et imprime les deux sondes du
    /// haut-parleur côte à côte. Même montage que DiscProbe — h_run / _808x.Run,
    /// pas h_runpc / pc.runpc.</summary>
    public static int SpeakerProbe(string romsPath, int slices, string? discA)
    {
        Oracle.CheckAbi();
        var budget = pc.cpu_get_speed() / 100;
        Oracle.h_set_discfn(0, discA ?? "");
        Oracle.h_set_mem_size(pc.cfg_mem_size);
        Oracle.h_set_drive_type(0, pc.cfg_drive_type[0]);
        Oracle.h_set_drive_type(1, pc.cfg_drive_type[1]);
        Oracle.h_set_bpb_disable(Disc.disc_img.bpb_disable);
        Oracle.h_set_romset(pc.romset);
        // LES DEUX CÔTÉS DOIVENT CHOISIR LE MÊME CŒUR, et avant h_boot : celui-ci
        // fait `AT = (h_core == H_CORE_286)` (harness.c:362), donc l'ordre compte.
        // Le discriminant est le flag du modèle, connu AVANT l'amorçage — pas la
        // globale AT, que resetpchard ne pose que pendant.
        Oracle.h_set_core(CoeurDuModele());
        Oracle.h_set_hdd_controller(pc.cfg_hdd_controller);
        for (var hd = 0; hd < 2; hd++)
            Oracle.h_set_hdd(hd, Disc.hdd_c.ide_fn[hd], Disc.hdd_c.hdc[hd].spt,
                             Disc.hdd_c.hdc[hd].hpc, Disc.hdd_c.hdc[hd].tracks);
        if (Oracle.h_boot(romsPath) == 0) return 1;
        for (var i = 0; i < slices; i++) Oracle.h_run(budget);

        _808x.ResetDiagState();
        // L'ÉTAT DE PRÉFETCH DU 286 SE REPORTAIT D'UNE PHASE À L'AUTRE, ET LES DEUX
        // CÔTÉS ARRIVAIENT SALES DIFFÉREMMENT.
        //
        // prefetch_bytes et prefetch_prefixes sont des statiques de 386_dynarec.c que
        // NI h_boot NI pc.initpc ne remettent — PCem n'amorce qu'une fois par processus,
        // le diff deux fois : la phase 1 court l'amorçage entier, la phase 2 le rejoue
        // en pas à pas. Chaque côté entrait donc en phase 2 avec ce que SON propre
        // phase 1 avait laissé, et comme elle divergeait, les deux restes différaient.
        //
        // MESURÉ : au premier boot-diff AT, tsc valait 11 côté oracle et 13 côté C# à
        // l'instruction 0, CS:IP F000:FFF0. Onze est exactement timing_jmp_rm, que les
        // DEUX côtés posent à 11 — vérifié. opJMP_far_a16 et prefetch_run sont des
        // transcriptions fidèles — vérifiées ligne à ligne. Les quatre entrées du modèle
        // de préfetch s'accordent — vérifiées. Il ne restait que l'état.
        //
        // h_prefetch_reset() était au contrat ABI depuis A2.2a sans jamais être importé.
        // Appelé sans condition de cœur, comme h_reset le fait (harness.c:391) : sur un
        // 8088 ces statiques ne servent à personne, execx86 portant son propre modèle.
        Oracle.h_prefetch_reset();
        Cpu._386.prefetch_reset();
        Oracle.h_seg_clear_residue();
        Cpu._386.ClearSegResidue();
        Floppy.fdd_c.discfns[0] = discA ?? "";
        if (!pc.initpc(romsPath)) return 1;
        for (var i = 0; i < slices; i++) _808x.Run(budget);

        var oc = new ulong[SpeakerFields.Length];
        var cs = new ulong[SpeakerFields.Length];
        Oracle.h_speaker_probe(oc);
        Sound.sound_speaker.Probe(cs);

        var bad = 0;
        for (var f = 0; f < SpeakerFields.Length; f++)
        {
            var flag = oc[f] == cs[f] ? " " : "*";
            if (oc[f] != cs[f]) bad++;
            Console.WriteLine($" {flag} {SpeakerFields[f],-20} oracle {oc[f],22} | C# {cs[f],22}");
        }

        var mute = oc[8] == HashSeed;
        Console.WriteLine();
        Console.WriteLine(mute
            ? $"Empreinte restée à sa graine après {slices} tranches : AUCUN bloc produit. " +
              "Un accord ici ne prouve rien."
            : $"Empreinte : {oc[8]:X16} — le son a bien été produit des deux côtés.");
        Console.WriteLine(bad == 0
            ? $"Sonde haut-parleur : {SpeakerFields.Length} champs identiques après {slices} tranches."
            : $"Sonde haut-parleur : {bad} champ(s) divergent(s).");
        return bad == 0 && !mute ? 0 : 1;
    }

    /// <summary>Les neuf champs du haut-parleur des deux côtés, en phase 2 du diff.
    /// speaker_buffer et speaker_pos sont `static` dans sound_speaker.c : ils ne
    /// sont lisibles que parce que le harnais compile ce .c dans son unité de
    /// traduction, comme il le fait de 808x.c.</summary>
    private static void DumpSpeaker()
    {
        var oc = new ulong[SpeakerFields.Length];
        var cs = new ulong[SpeakerFields.Length];
        Oracle.h_speaker_probe(oc);
        Sound.sound_speaker.Probe(cs);

        var diff = new List<string>();
        for (var f = 0; f < SpeakerFields.Length; f++)
            if (oc[f] != cs[f])
                diff.Add($"{SpeakerFields[f]}: oracle {oc[f]} / C# {cs[f]}");

        Console.WriteLine(diff.Count == 0
            ? "  haut-parleur : identique"
            : $"  haut-parleur : {string.Join("  |  ", diff)}");
    }

    private static readonly string[] DiscFields =
    {
        "discint", "disc_3f7", "lastbyte", "paramstogo", "bit_rate", "motoron", "disc_drivesel", "curdrive",
        "disc_track[0]", "disc_track[1]", "drive_empty[0]", "drive_empty[1]", "disc_changed[0]", "disc_changed[1]",
        "writeprot[0]", "writeprot[1]", "disc_notfound", "readflash", "poll_timer.enabled", "poll_remaining",
    };

    /// <summary>Amorce les deux côtés sur N tranches et imprime les deux sondes
    /// disquette côte à côte. Rend 0 si elles concordent champ à champ.
    ///
    /// Les tranches passent par h_run / _808x.Run, comme le banc : les deux remettent
    /// `cycles` à zéro en tête de tranche, donc suivent la MÊME trajectoire. Ni
    /// h_runpc (remise à zéro) ni pc.runpc (report du reliquat, comme pc.c) ne
    /// conviennent l'un contre l'autre : une sonde prise un reliquat plus tôt d'un
    /// côté montrait un moteur encore allumé là où l'autre l'avait déjà coupé.</summary>
    public static int DiscProbe(string romsPath, int slices, string? discA)
    {
        Oracle.CheckAbi();
        var budget = pc.cpu_get_speed() / 100;
        Oracle.h_set_discfn(0, discA ?? "");
        Oracle.h_set_mem_size(pc.cfg_mem_size);
        Oracle.h_set_drive_type(0, pc.cfg_drive_type[0]);
        Oracle.h_set_drive_type(1, pc.cfg_drive_type[1]);
        Oracle.h_set_bpb_disable(Disc.disc_img.bpb_disable);
        Oracle.h_set_romset(pc.romset);
        // LES DEUX CÔTÉS DOIVENT CHOISIR LE MÊME CŒUR, et avant h_boot : celui-ci
        // fait `AT = (h_core == H_CORE_286)` (harness.c:362), donc l'ordre compte.
        // Le discriminant est le flag du modèle, connu AVANT l'amorçage — pas la
        // globale AT, que resetpchard ne pose que pendant.
        Oracle.h_set_core(CoeurDuModele());
        Oracle.h_set_hdd_controller(pc.cfg_hdd_controller);
        for (var hd = 0; hd < 2; hd++)
            Oracle.h_set_hdd(hd, Disc.hdd_c.ide_fn[hd], Disc.hdd_c.hdc[hd].spt,
                             Disc.hdd_c.hdc[hd].hpc, Disc.hdd_c.hdc[hd].tracks);
        if (Oracle.h_boot(romsPath) == 0) return 1;
        for (var i = 0; i < slices; i++) Oracle.h_run(budget);

        _808x.ResetDiagState();
        // L'ÉTAT DE PRÉFETCH DU 286 SE REPORTAIT D'UNE PHASE À L'AUTRE, ET LES DEUX
        // CÔTÉS ARRIVAIENT SALES DIFFÉREMMENT.
        //
        // prefetch_bytes et prefetch_prefixes sont des statiques de 386_dynarec.c que
        // NI h_boot NI pc.initpc ne remettent — PCem n'amorce qu'une fois par processus,
        // le diff deux fois : la phase 1 court l'amorçage entier, la phase 2 le rejoue
        // en pas à pas. Chaque côté entrait donc en phase 2 avec ce que SON propre
        // phase 1 avait laissé, et comme elle divergeait, les deux restes différaient.
        //
        // MESURÉ : au premier boot-diff AT, tsc valait 11 côté oracle et 13 côté C# à
        // l'instruction 0, CS:IP F000:FFF0. Onze est exactement timing_jmp_rm, que les
        // DEUX côtés posent à 11 — vérifié. opJMP_far_a16 et prefetch_run sont des
        // transcriptions fidèles — vérifiées ligne à ligne. Les quatre entrées du modèle
        // de préfetch s'accordent — vérifiées. Il ne restait que l'état.
        //
        // h_prefetch_reset() était au contrat ABI depuis A2.2a sans jamais être importé.
        // Appelé sans condition de cœur, comme h_reset le fait (harness.c:391) : sur un
        // 8088 ces statiques ne servent à personne, execx86 portant son propre modèle.
        Oracle.h_prefetch_reset();
        Cpu._386.prefetch_reset();
        Oracle.h_seg_clear_residue();
        Cpu._386.ClearSegResidue();
        Floppy.fdd_c.discfns[0] = discA ?? "";
        if (!pc.initpc(romsPath)) return 1;
        for (var i = 0; i < slices; i++) _808x.Run(budget);

        var oc = new ulong[DiscFields.Length];
        var cs = new ulong[DiscFields.Length];
        Oracle.h_disc_probe(oc);
        iXtal26.Floppy.fdc_c.Probe(cs);

        var bad = 0;
        for (var f = 0; f < DiscFields.Length; f++)
        {
            var flag = oc[f] == cs[f] ? " " : "*";
            if (oc[f] != cs[f]) bad++;
            Console.WriteLine($" {flag} {DiscFields[f],-20} oracle {oc[f],22} | C# {cs[f],22}");
        }

        Console.WriteLine(bad == 0
            ? $"\nSonde disquette : {DiscFields.Length} champs identiques après {slices} tranches."
            : $"\nSonde disquette : {bad} champ(s) divergent(s).");
        return bad == 0 ? 0 : 1;
    }

    /// <summary>Le sous-système disquette des deux côtés — les globales de disc.c et
    /// fdc.c que l'oracle expose sans instrumenter (h_disc_probe). L'instance
    /// `fdc` est static dans fdc.c et n'y figure pas : si tout concorde ici, la
    /// divergence est dans fdc.c ou en amont, dans le DMA ou le PIC.</summary>
    private static void DumpDisc()
    {
        var oc = new ulong[DiscFields.Length];
        var cs = new ulong[DiscFields.Length];
        Oracle.h_disc_probe(oc);
        iXtal26.Floppy.fdc_c.Probe(cs);

        var diff = new List<string>();
        for (var f = 0; f < DiscFields.Length; f++)
            if (oc[f] != cs[f])
                diff.Add($"{DiscFields[f]}: oracle {oc[f]} / C# {cs[f]}");

        Console.WriteLine(diff.Count == 0
            ? "  disquette : identique"
            : $"  disquette : {string.Join("  |  ", diff)}");
    }

    /// <summary>Vrai dès qu'un champ de la sonde diverge sur l'un des trois canaux.</summary>
    private static bool PitDiverges()
    {
        var oc = new ulong[PitFields.Length];
        var cs = new ulong[PitFields.Length];

        for (var t = 0; t < 3; t++)
        {
            Oracle.h_pit_probe(t, oc);
            iXtal26.Models.pit.Probe(t, cs);
            for (var f = 0; f < PitFields.Length; f++)
                if (oc[f] != cs[f])
                    return true;
        }

        return false;
    }

    /// <summary>Dump complet des trois canaux : état d'entrée (commun aux deux
    /// cœurs) puis état de sortie de chaque côté. Sans l'état d'entrée on ne peut
    /// pas rejouer à la main la branche prise par pit_set_gate_no_timer.</summary>
    private static void DumpPitFull(ulong[][] before)
    {
        var oc = new ulong[PitFields.Length];
        var cs = new ulong[PitFields.Length];

        for (var t = 0; t < 3; t++)
        {
            Oracle.h_pit_probe(t, oc);
            iXtal26.Models.pit.Probe(t, cs);

            var same = true;
            for (var f = 0; f < PitFields.Length; f++) same &= oc[f] == cs[f];
            if (same) continue;

            Console.WriteLine($"  canal {t} :");
            for (var f = 0; f < PitFields.Length; f++)
            {
                var flag = oc[f] == cs[f] ? " " : "*";
                Console.WriteLine($"   {flag} {PitFields[f],-14} avant {before[t][f],22}" +
                                  $" | oracle {oc[f],22} | C# {cs[f],22}");
            }
        }
    }

}
