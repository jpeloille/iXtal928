// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only

using iXtal26.Diff;

if (args.Length == 0 || args[0] is "-h" or "--help")
{
    Console.WriteLine("Usage : iXtal26.Diff <commande> [options]");
    Console.WriteLine();
    Console.WriteLine("  sst-probe [--vectors DIR] [--op XX ...] [--limit N] [--baseline FICHIER]");
    Console.WriteLine("            [--target oracle|csharp]");
    Console.WriteLine("      Sonde SingleStepTests : passe les vecteurs à l'oracle C et");
    Console.WriteLine("      rapporte le taux de réussite. Porte de M0 — décide si le");
    Console.WriteLine("      harnais xunit complet vaut d'être construit.");
    Console.WriteLine();
    Console.WriteLine("  fetch-probe [CHEMIN_ROMS]");
    Console.WriteLine("      Sonde le chemin d'instruction de exec386 — getpccache, le cache");
    Console.WriteLine("      de page et son arithmetique de biais — contre l'oracle.\n");
    Console.WriteLine("  fuzz [--op XX ...] [--mode single|stream] [--iter N] [--core 286|386 [--cpu N]]");
    Console.WriteLine("       [--rounds N] [--instr N] [--seed N] [-v] [--ram-per-instr]");
    Console.WriteLine("      Diff différentiel : le cœur C# contre l'oracle C, état complet");
    Console.WriteLine("      comparé après chaque instruction. Par défaut 0xCE, le seul");
    Console.WriteLine("      opcode que 808x.c laisse tomber dans son `default:`.");
    Console.WriteLine();
    Console.WriteLine("  boot-diff [CHEMIN_ROMS] [TRANCHES] [--fda IMAGE] [--fdb IMAGE]");
    Console.WriteLine("            [--config FICHIER] [--model NOM] [--type TEXTE ...] [--type-at N]");
    Console.WriteLine("            [--type-settle N] [--gfxcard cga|vga|tvga8900d|tvga9000b] [--cpu N]");
    Console.WriteLine("            [--lockstep N [--lockstep-from S]]");
    Console.WriteLine("      Diff de traces d'amorçage. Phase 1 : hachage par instruction des");
    Console.WriteLine("      deux cœurs depuis le reset, pour situer la première divergence.");
    Console.WriteLine("      Phase 2 : rejeu en pas à pas jusque-là, vecteur d'état complet");
    Console.WriteLine("      plus une sonde des trois canaux du PIT et du sous-système disquette.");
    Console.WriteLine("      --fda IMAGE monte la même image .img dans le lecteur A des deux");
    Console.WriteLine("      côtés avant l'amorçage : c'est le diff d'un amorçage DOS.");
    Console.WriteLine("      --type TEXTE tape la MÊME chaîne des deux côtés, à la MÊME tranche,");
    Console.WriteLine("      à partir de --type-at N. C'est ce qui met le chemin d'ÉCRITURE du");
    Console.WriteLine("      contrôleur sous comparaison : FORMAT et WRITE DATA ne s'atteignent");
    Console.WriteLine("      qu'en tapant une commande. Les images montées sont COPIÉES par côté,");
    Console.WriteLine("      puis comparées octet par octet — les deux cœurs écrivent pour de vrai.");
    Console.WriteLine("      --config FICHIER règle la MÊME machine des deux côtés : l'outil");
    Console.WriteLine("      lit le fichier une fois et pousse chaque scalaire par h_set_*");
    Console.WriteLine("      côté C et par les globales côté C#. Jamais deux lectures.");
    Console.WriteLine("      --model NOM choisit la machine sans fichier, et l'emporte sur la");
    Console.WriteLine("      clé `model` de --config. Les images montées par le FICHIER comptent");
    Console.WriteLine("      autant que --fda/--fdb : les clés disc_a, disc_b et hdc_fn sont");
    Console.WriteLine("      copiées par côté, puis comparées, comme les options.");
    Console.WriteLine("      --lockstep N : diagnostic. Les deux côtés avancent ENSEMBLE, tranche par");
    Console.WriteLine("      tranche, frappe comprise ; état CPU comparé à chaque tranche, sonde VGA");
    Console.WriteLine("      toutes les N (et à chaque tranche dès --lockstep-from S). Nomme le premier");
    Console.WriteLine("      écart et la ligne tapée en cours — la phase 2 ne rejoue pas --type.");
    Console.WriteLine();
    Console.WriteLine("  config-check");
    Console.WriteLine("      Aller-retour du moteur de configuration : on écrit un fichier");
    Console.WriteLine("      avec config_set_*, on le relit avec config_load, on compare.");
    Console.WriteLine("      config.c n'est pas lié dans l'oracle — il n'y a rien à quoi le");
    Console.WriteLine("      comparer, donc c'est la seule porte de sa moitié écriture.");
    Console.WriteLine();
    Console.WriteLine("  speed-check [CHEMIN_ROMS] [TRANCHES]");
    Console.WriteLine("      Où passe le temps ÉMULÉ : cycles consommés contre TSC avancé, par");
    Console.WriteLine("      tranche. Mesure le temps ÉMULÉ, pas la vitesse hôte : voir bench.");
    Console.WriteLine();
    Console.WriteLine("  bench [CHEMIN_ROMS=roms] [TRANCHES=6000] [--repeat R=5] [--warmup W=600]");
    Console.WriteLine("        [--side c|csharp|both=both]");
    Console.WriteLine("      Vitesse HÔTE : Oracle.h_run(47727) contre _808x.Run(47727), sur la");
    Console.WriteLine("      même trajectoire (vérifiée en fin de banc), ordre alterné, chauffe");
    Console.WriteLine("      puis ré-amorçage avant mesure, ΔJIT/ΔGC relevés. Release seulement.");
    Console.WriteLine("      Épingler : taskset -c 0-3.");
    Console.WriteLine();
    Console.WriteLine("  disc-probe [CHEMIN_ROMS] [TRANCHES] [--fda IMAGE]");
    Console.WriteLine("      Amorce les deux côtés et imprime la sonde disquette de chacun. Vérifie");
    Console.WriteLine("      que h_disc_probe et Floppy.fdc_c.Probe lisent les mêmes champs dans");
    Console.WriteLine("      le même ordre — une sonde qui ne sert qu'en cas de divergence n'est");
    Console.WriteLine("      jamais exercée par un vert.");
    Console.WriteLine();
    Console.WriteLine("  speaker-probe [CHEMIN_ROMS] [TRANCHES] [--fda IMAGE]");
    Console.WriteLine("      Amorce les deux côtés et imprime la sonde du haut-parleur de chacun :");
    Console.WriteLine("      les six globales de sound_speaker.c, le curseur speaker_pos, la");
    Console.WriteLine("      position du mixeur, et une empreinte du son RÉELLEMENT produit. Une");
    Console.WriteLine("      empreinte restée à sa graine est un échec : deux silences concordants");
    Console.WriteLine("      ne prouvent rien. C'est la seule voix du chemin audio dans le diff.");
    Console.WriteLine();
    Console.WriteLine("  fdc-trace [CHEMIN_ROMS] [TRANCHES] [--fda IMAGE]");
    Console.WriteLine("      Amorce le cœur C# seul et imprime chaque transition du contrôleur, en");
    Console.WriteLine("      clair : état d'exécution, drapeaux du MSR, état du média. Le pendant à");
    Console.WriteLine("      l'exécution de la table // noms: de fdc.cs. L'oracle n'y participe pas,");
    Console.WriteLine("      son instance `fdc` étant static dans fdc.c.");
    Console.WriteLine();
    Console.WriteLine("  vga-probe [CHEMIN_ROMS] [TRANCHES=1000] [--model NOM=ibmat] [--fda IMAGE] [--gfxcard vga|tvga8900d|tvga9000b]");
    Console.WriteLine("      Amorce l'ORACLE SEUL avec une carte VGA et imprime la sonde de la");
    Console.WriteLine("      carte et l'écran texte lu dans sa VRAM brute. Le pendant de la sonde");
    Console.WriteLine("      AT de B2 : l'oracle doit savoir faire tourner la VGA avant qu'une");
    Console.WriteLine("      ligne de C# ne s'écrive.");
    Console.WriteLine();
    Console.WriteLine("  cpu-config-check [CHEMIN_ROMS] [--inverse]");
    Console.WriteLine("      Balaye les tables de CPU des quatre machines : amorce chaque entrée");
    Console.WriteLine("      des deux côtés — l'oracle fait tourner le vrai cpu_set() de PCem — et");
    Console.WriteLine("      confronte l'empreinte CPU (vitesse, budget, cycles mémoire, préfetch,");
    Console.WriteLine("      domaine d'horloge, temps vidéo). L'indice hors table doit être refusé");
    Console.WriteLine("      des deux côtés.");
    Console.WriteLine();
    Console.WriteLine("  sst386-probe [--vectors DIR] [--op FORME ...] [--limit N] [--target oracle|csharp]");
    Console.WriteLine("               [--baseline FICHIER]");
    Console.WriteLine("      Sonde SingleStepTests/80386 (386EX, mode réel, format MOO) : l'état final");
    Console.WriteLine("      de chaque cas contre le silicium. --baseline ÉCRIT la ligne de base.");
    Console.WriteLine();
    Console.WriteLine("  ops-count [--missing]");
    Console.WriteLine("      Compte les emplacements posés de ops_386 et ops_386_0f, par quadrant");
    Console.WriteLine("      op32 — la table vivante, pas les sources. --missing liste les trous.");
    Console.WriteLine();
    Console.WriteLine("  abi");
    Console.WriteLine("      Vérifie le contrat binaire avec libixtal26oracle.so.");
    return args.Length == 0 ? 2 : 0;
}

switch (args[0])
{
    // G2, D0.5 — le corpus SingleStepTests/80386, oracle silicium du cœur 386.
    case "sst386-probe":
    {
        var vectors = "vectors/sst386";
        var forms = new List<string>();
        var limit = 0;
        var csharp = false;
        string? baseline = null;
        for (var i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--vectors" when i + 1 < args.Length: vectors = args[++i]; break;
                case "--op" when i + 1 < args.Length: forms.Add(args[++i]); break;
                case "--limit" when i + 1 < args.Length: limit = int.Parse(args[++i]); break;
                case "--target" when i + 1 < args.Length: csharp = args[++i] == "csharp"; break;
                case "--baseline" when i + 1 < args.Length: baseline = args[++i]; break;
                default:
                    Console.Error.WriteLine($"Option inconnue : {args[i]}");
                    return 2;
            }
        }
        return Sst386Probe.Run(vectors, forms, limit, csharp, baseline);
    }

    // G2, D0.6 — ce que la table du 386 porte réellement, lue vivante.
    case "ops-count":
        return OpsCount.Run(args.Contains("--missing"));

    case "abi":
        Oracle.CheckAbi();
        Console.WriteLine($"ABI {Oracle.h_abi_version()} OK, h_state = {Oracle.h_state_size()} octets.");
        return 0;

    // A2.2a — le chemin de fetch de exec386, mis sous oracle AVANT d'ecrire la
    // boucle. Une erreur de biais d'une page y est silencieuse : elle rend des
    // octets plausibles, pris au mauvais endroit.
    case "fetch-probe":
        return FetchProbe.Run(args.Length > 1 ? args[1] : "roms");

    // A2.2b — la porte de la boucle. Un pas sous le coeur 286 doit atteindre le
    // handler d'echec ET NOMMER SON OPCODE. Elle prouve la boucle, le fetch,
    // l'index d'aiguillage et la plomberie de cycles, sans un seul handler ecrit.
    // B2 — la sonde d'amorcage AT. Oracle seul, aucune comparaison : voir
    // AtProbe.cs pour pourquoi c'est l'ordre du plan.
    case "at-probe":
        return AtProbe.Run(args.Length > 1 ? args[1] : "roms");

    case "core286-check":
        return Core286Check.Run();

    // M20 — le mode protégé du 286, état construit par LOADALL des deux côtés.
    case "pm-check":
        return PmCheck.Run(args.Length >= 3 && args[1] == "--case" ? int.Parse(args[2]) : -1);

    // M16 — le balayage des tables de CPU : chaque entrée de chaque machine, amorcée
    // des deux côtés, l'empreinte CPU confrontée champ par champ. Le seul témoin
    // différentiel du 286/20 et du 286/25, qu'aucun boot-diff n'atteint.
    case "cpu-config-check":
    {
        var roms = "roms";
        var reverse = false;
        for (var i = 1; i < args.Length; i++)
        {
            if (args[i] == "--inverse") reverse = true;
            else roms = args[i];
        }
        return CpuConfigCheck.Run(roms, reverse);
    }

    // Aller-retour du moteur de configuration. Sa moitié ÉCRITURE — les six
    // config_set_* et config_save — n'a aucun appelant tant que le menu n'édite pas la
    // configuration, et un chemin mort est indiscernable d'un chemin cassé. Ceci lui
    // donne une porte : on écrit, on relit, on compare.
    case "config-check":
        return ConfigCheck.Run();

    case "speed-check":
    {
        var roms = args.Length > 1 ? args[1] : "roms";
        var n = args.Length > 2 ? int.Parse(args[2]) : 2000;
        return SpeedCheck.Run(roms, n);
    }

    // Vitesse HÔTE, symétrique : h_run contre _808x.Run. Voir Bench.cs.
    case "bench":
    {
        var roms = "roms";
        var slices = 6000;
        var repeat = 5;
        var warmup = 600;
        var side = "both";
        var positional = 0;

        for (var i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--repeat" when i + 1 < args.Length: repeat = int.Parse(args[++i]); break;
                case "--warmup" when i + 1 < args.Length: warmup = int.Parse(args[++i]); break;
                case "--side" when i + 1 < args.Length: side = args[++i]; break;
                default:
                    if (args[i].StartsWith("--", StringComparison.Ordinal) || positional > 1)
                    {
                        Console.Error.WriteLine($"Option inconnue : {args[i]}");
                        return 2;
                    }
                    if (positional++ == 0) roms = args[i];
                    else slices = int.Parse(args[i]);
                    break;
            }
        }

        if (side is not ("c" or "csharp" or "both"))
        {
            Console.Error.WriteLine($"--side : c, csharp ou both, pas « {side} ».");
            return 2;
        }

        return Bench.Run(roms, slices, repeat, warmup, sideC: side != "csharp", sideCs: side != "c");
    }

    // Profil de l'amorçage par adresse linéaire : mesure ce que le modèle de
    // coût PRÉDIT, au lieu de le prédire une seconde fois.
    case "boot-profile":
    {
        var roms = args.Length > 1 ? args[1] : "roms";
        var n = args.Length > 2 ? int.Parse(args[2]) : 30_000_000;
        return BootProfile.Run(roms, n);
    }

    // Départage l'origine des cycles jamais portés au tsc.
    case "refresh-check":
    {
        var roms = args.Length > 1 ? args[1] : "roms";
        var skip = args.Length > 2 ? long.Parse(args[2]) : 50_000_000L;
        var win = args.Length > 3 ? int.Parse(args[3]) : 2_000_000;
        return BootProfile.Refresh(roms, skip, win);
    }

    case "boot-diff":
    {
        var roms = "roms";
        var slices = 100;
        string? fda = null;
        string? fdb = null;
        string? cfg = null;
        string? model = null;
        string? gfx = null;
        var types = new List<string>();
        var typeAt = 0;
        var typeSettle = iXtal26.Host.KeyScript.SlicesAfterLine;
        var positional = 0;
        for (var i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--fda" when i + 1 < args.Length: fda = args[++i]; break;
                case "--fdb" when i + 1 < args.Length: fdb = args[++i]; break;
                case "--type" when i + 1 < args.Length: types.Add(args[++i]); break;
                case "--type-at" when i + 1 < args.Length: typeAt = int.Parse(args[++i]); break;
                case "--type-settle" when i + 1 < args.Length: typeSettle = int.Parse(args[++i]); break;
                // --no-tsc : retire tsc du hachage de trace des DEUX cotes, pour separer
                // une divergence de TEMPS d'une divergence FONCTIONNELLE. Voir harness.c.
                case "--no-tsc":
                        BootDiff.SansTsc = true;
                        Oracle.h_set_trace_notsc(1);
                        break;
                case "--config" when i + 1 < args.Length: cfg = args[++i]; break;
                // --model : le pendant de celui du programme principal, et il redevient
                // nécessaire à M12. Tant que boot-diff écrasait disc_a, « --config
                // ixtal26-xt.cfg » mesurait en fait le XT SANS disquette, donc sa ROM
                // BASIC. Le correctif rend la clé au fichier, et cette campagne-là
                // n'avait alors plus aucune commande pour l'exprimer.
                case "--model" when i + 1 < args.Length: model = args[++i]; break;
                // M15 — la carte vidéo, avec la même précédence que --model.
                case "--gfxcard" when i + 1 < args.Length: gfx = args[++i]; break;
                // M16 — l'indice dans la table de CPU de la machine, appliqué APRÈS
                // --model : `--cpu 3` est un 8088/10 sur l'ibmpc, un 286/12 sur l'ami286.
                case "--cpu" when i + 1 < args.Length: BootDiff.CpuOverride = int.Parse(args[++i]); break;
                // M19 — diagnostic : les deux côtés au pas commun, frappe comprise.
                case "--lockstep" when i + 1 < args.Length: BootDiff.LockstepEvery = int.Parse(args[++i]); break;
                case "--lockstep-from" when i + 1 < args.Length: BootDiff.LockstepFrom = int.Parse(args[++i]); break;
                default:
                    if (args[i].StartsWith("--", StringComparison.Ordinal) || positional > 1)
                    {
                        Console.Error.WriteLine($"Option inconnue : {args[i]}");
                        return 2;
                    }
                    if (positional++ == 0) roms = args[i];
                    else slices = int.Parse(args[i]);
                    break;
            }
        }
        if (fda is not null && !File.Exists(fda))
        {
            Console.Error.WriteLine($"Image de disquette introuvable : {fda}");
            return 2;
        }
        if (fdb is not null && !File.Exists(fdb))
        {
            Console.Error.WriteLine($"Image de disquette introuvable : {fdb}");
            return 2;
        }
        // Refusé plutôt qu'ignoré : taper à la tranche 0 tape dans le test mémoire,
        // et l'invite n'existe pas encore. Une frappe sans --type-at ne prouverait rien.
        if (types.Count > 0 && typeAt <= 0)
        {
            Console.Error.WriteLine("--type exige --type-at N : la tranche où l'invite est atteinte.");
            return 2;
        }
        // Refusé et non ignoré : un fichier mal tapé donnerait tous les défauts, donc
        // une comparaison verte qui ne prouve RIEN de la configuration demandée.
        if (cfg is not null && !File.Exists(cfg))
        {
            Console.Error.WriteLine($"Fichier de configuration introuvable : {cfg}");
            return 2;
        }
        return BootDiff.Run(roms, slices, fda, cfg, fdb, types, typeAt, typeSettle, model, gfx);
    }

    case "vga-probe":
    {
        var roms = "roms";
        var slices = 1000;
        var model = "ibmat";
        string? fda = null;
        var card = "vga";
        var positional = 0;
        for (var i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--fda" when i + 1 < args.Length: fda = args[++i]; break;
                case "--model" when i + 1 < args.Length: model = args[++i]; break;
                case "--gfxcard" when i + 1 < args.Length: card = args[++i]; break;
                default:
                    if (args[i].StartsWith("--", StringComparison.Ordinal) || positional > 1)
                    {
                        Console.Error.WriteLine($"Option inconnue : {args[i]}");
                        return 2;
                    }
                    if (positional++ == 0) roms = args[i];
                    else slices = int.Parse(args[i]);
                    break;
            }
        }
        return VgaProbe.Run(roms, slices, model, fda, card);
    }

    case "disc-probe":
    {
        var roms = "roms";
        var slices = 100;
        string? fda = null;
        var positional = 0;
        for (var i = 1; i < args.Length; i++)
        {
            if (args[i] == "--fda" && i + 1 < args.Length) { fda = args[++i]; continue; }
            if (positional++ == 0) roms = args[i]; else slices = int.Parse(args[i]);
        }
        return BootDiff.DiscProbe(roms, slices, fda);
    }

    case "speaker-probe":
    {
        var roms = "roms";
        var slices = 300;
        string? fda = null;
        var positional = 0;
        for (var i = 1; i < args.Length; i++)
        {
            if (args[i] == "--fda" && i + 1 < args.Length) { fda = args[++i]; continue; }
            if (positional++ == 0) roms = args[i]; else slices = int.Parse(args[i]);
        }
        return BootDiff.SpeakerProbe(roms, slices, fda);
    }

    case "fdc-trace":
    {
        var roms = "roms";
        var slices = 100;
        string? fda = null;
        var positional = 0;
        for (var i = 1; i < args.Length; i++)
        {
            if (args[i] == "--fda" && i + 1 < args.Length) { fda = args[++i]; continue; }
            if (positional++ == 0) roms = args[i]; else slices = int.Parse(args[i]);
        }
        if (fda is not null && !File.Exists(fda))
        {
            Console.Error.WriteLine($"Image de disquette introuvable : {fda}");
            return 2;
        }
        return FloppyTrace.Run(roms, slices, fda);
    }

    case "sst-diff":
    {
        // Rejoue des cas SST sur les DEUX cœurs et compare l'état complet.
        var vectors = "vectors/sst/v2";
        var op = args.Length > 1 ? args[1] : "00";
        var n = args.Length > 2 ? int.Parse(args[2]) : 20;
        Oracle.CheckAbi();
        var cases = SstProbe.LoadPublic(Path.Combine(vectors, $"{op}.json.gz"));
        var bad = 0;
        for (var i = 0; i < Math.Min(n, cases.Count); i++)
        {
            var d = SstProbe.DiffCase(cases[i]);
            if (d is null) continue;
            Console.WriteLine($"[{cases[i].idx}] {cases[i].name} : {d}");
            if (++bad >= 5) break;
        }
        Console.WriteLine(bad == 0
            ? $"Les deux cœurs sont d'accord sur {Math.Min(n, cases.Count)} cas."
            : $"{bad} divergence(s) coeur-a-coeur.");
        return bad == 0 ? 0 : 1;
    }

    case "sst-probe":
    {
        var vectors = "vectors/sst/v2";
        var ops = new List<string>();
        var limit = 0;
        string? baseline = null;
        var targetCs = false;

        for (var i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--vectors" when i + 1 < args.Length: vectors = args[++i]; break;
                case "--op" when i + 1 < args.Length: ops.Add(args[++i]); break;
                case "--limit" when i + 1 < args.Length: limit = int.Parse(args[++i]); break;
                case "--baseline" when i + 1 < args.Length: baseline = args[++i]; break;
                case "--target" when i + 1 < args.Length: targetCs = args[++i] == "csharp"; break;
                default:
                    Console.Error.WriteLine($"Option inconnue : {args[i]}");
                    return 2;
            }
        }

        if (ops.Count == 0)
            ops.AddRange(Directory.Exists(vectors)
                ? Directory.GetFiles(vectors, "*.json.gz").Select(f =>
                    Path.GetFileName(f).Replace(".json.gz", "")).Order()
                : []);

        return SstProbe.Run(vectors, ops.ToArray(), limit, baseline, targetCs);
    }

    case "fuzz":
    {
        var ops = new List<byte>();
        var rounds = 200;
        var instr = 50;
        ulong seed = 1;
        var verbose = false;
        var ramPerInstr = false;
        // A2.2c — quel cœur les DEUX côtés exécutent. Basculés ensemble, et avant
        // leur reset : c'est h_reset qui applique AT, et resetx86 en tire le vecteur
        // de reset et rammask.
        var fuzzCore = Oracle.Core8088;
        var single = false;
        var iterations = 20000;

        for (var i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--op" when i + 1 < args.Length:
                    ops.Add(Convert.ToByte(args[++i], 16)); break;
                case "--rounds" when i + 1 < args.Length: rounds = int.Parse(args[++i]); break;
                case "--instr" when i + 1 < args.Length: instr = int.Parse(args[++i]); break;
                case "--seed" when i + 1 < args.Length: seed = ulong.Parse(args[++i]); break;
                case "-v": verbose = true; break;
                case "--ram-per-instr": ramPerInstr = true; break;
                case "--core":
                    fuzzCore = args[++i] switch
                    {
                        "286" => Oracle.Core286,
                        "386" => Oracle.Core386,
                        _ => Oracle.Core8088,
                    };
                    break;
                // M16 — l'entrée de cpus_286 que le 286 fuzzé reçoit, des DEUX côtés :
                // h_reset et Reset286 font tourner cpu_set() sur la table de l'ami286.
                // --cpu 5 exerce le 286/20 : 4 cycles mémoire, isa 3, préfetch ROM 20.
                case "--cpu" when i + 1 < args.Length:
                {
                    // G2 : la table est celle du cœur — cpus_286 ou cpus_i386SX. --core doit
                    // donc précéder --cpu.
                    var n = int.Parse(args[++i]);
                    var table = fuzzCore == Oracle.Core386
                        ? iXtal26.Cpu.cpu_tables.cpus_i386SX : iXtal26.Cpu.cpu_tables.cpus_286;
                    var count = 0;
                    while (table[count].cpu_type != -1)
                        count++;
                    if (n < 0 || n >= count)
                    {
                        Console.Error.WriteLine($"--cpu {n} : hors de la table du cœur (0 à {count - 1}).");
                        return 2;
                    }
                    Oracle.h_set_cpu(0, n);
                    iXtal26.Cpu._386.FuzzCpu = n;
                    break;
                }
                case "--mode" when i + 1 < args.Length: single = args[++i] == "single"; break;
                case "--iter" when i + 1 < args.Length: iterations = int.Parse(args[++i]); break;
                default:
                    Console.Error.WriteLine($"Option inconnue : {args[i]}");
                    return 2;
            }
        }

        // 0xCE (INTO) : vérifié par extraction du switch de 808x.c, c'est le seul
        // des 256 opcodes qui n'a pas de `case` et tombe donc dans `default:`.
        if (ops.Count == 0)
            ops.Add(0xCE);

        return single
            ? Fuzzer.RunSingle(ops.ToArray(), iterations, seed, verbose, fuzzCore)
            : Fuzzer.Run(ops.ToArray(), rounds, instr, seed, verbose, fuzzCore, ramPerInstr);
    }

    default:
        Console.Error.WriteLine($"Commande inconnue : {args[0]}");
        return 2;
}
