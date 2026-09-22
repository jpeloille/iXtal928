// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/wx-ui/wx-main.cc:17-35 (main) + wx-sdl2.c:444-479 (pc_main)
// STATUS: host

using iXtal26;
using iXtal26.Host;
using iXtal26.PluginApi;

// Point d'entrée : lit les arguments, puis laisse SdlHost tenir la fenêtre et la boucle.
var romsPath = "roms";
var headless = false;
var maxSlices = 0; // 0 = tourne jusqu'à la fermeture de la fenêtre
var verbose = false;
var setup = false;
var turboSlices = 0; // 0 = pas de turbo : le POST se déroule à sa vitesse d'époque

// Configuration machine. null = aucun fichier, donc tous les défauts — c'est-à-dire
// exactement la machine que décrit VERIFICATION.md. Les surcharges valent -1 tant que
// la ligne de commande n'a rien dit, pour distinguer « non demandé » de « demandé à 0 »
// (0 est un type de lecteur légitime : « aucun lecteur »).
string? configPath = null;
var ramOverride = -1;
string? modelOverride = null;
var driveOverride = new[] { -1, -1 };

// --hdd / --hdd-d. COLLECTÉS et non appliqués sur place, comme --model et --ram :
// loadconfig écrase ide_fn[] et hdc[] SANS condition (pc.cs), là où il saute disc_a
// quand discfns[] est déjà rempli. La ligne de commande doit l'emporter sur le
// fichier, donc elle s'applique après lui.
var hddOverride = new string?[] { null, null };

// Le type FORCÉ, quand la taille n'est pas déterminante. -1 = déduire.
var hddTypeOverride = new[] { -1, -1 };

// Le budget de turbo vit dans SdlHost : le menu Ctrl+F12 s'en sert aussi pour le réarmer
// après un reset, et deux 5800 dans l'arbre finiraient par diverger.
const int DefaultTurboSlices = SdlHost.DefaultTurboSlices;

for (var i = 0; i < args.Length; i++)
{
    var arg = args[i];

    if (arg is "-h" or "--help")
    {
        PrintUsage();
        return 0;
    }

    // Conservé tel quel : VERIFICATION.md invoque « --boot roms 6000 ». Ses deux
    // positionnels lui sont propres et ignorent --rom-path, ce qui garde la
    // commande de vérification indépendante du reste de la table.
    if (arg == "--boot")
    {
        var roms = i + 1 < args.Length ? args[++i] : "roms";
        var slices = 20;

        // Un nombre mal tapé ne doit PAS retomber sur le défaut. « --boot roms 60O »
        // (lettre O) exécutait 20 tranches, imprimait un écran vide et sortait 0 :
        // indiscernable d'un cœur qui n'affiche rien. Et le « if (slices != 20) i++ »
        // d'origine ne consommait même pas l'argument quand il valait justement 20.
        if (i + 1 < args.Length)
        {
            if (!int.TryParse(args[++i], out slices) || slices <= 0)
            {
                Console.Error.WriteLine("--boot attend un nombre de tranches entier positif.");
                return 2;
            }
        }

        // --type TEXTE : tape la chaîne dans la machine après l'amorçage et revide
        // l'écran. C'est la seule vérification du chemin clavier qui ne dépende pas
        // d'un gestionnaire de fenêtres. Répétable : DOS demande la date puis l'heure
        // avant de rendre son invite, donc « --type "" --type "" --type DIR ».
        // --floppy-a/-b : l'image à monter, comme en mode fenêtre (voir plus bas).
        var types = new List<string>();
        string? bootModel = null;
        string? bootHdd = null;
        var bootHddType = -1;
        var settle = KeyScript.SlicesAfterLine;
        while (i + 1 < args.Length && args[i + 1].StartsWith("--", StringComparison.Ordinal))
        {
            var opt = args[++i];
            if (i + 1 >= args.Length)
            {
                Console.Error.WriteLine($"{opt} attend un argument.");
                return 2;
            }

            var val = args[++i];
            switch (opt)
            {
                case "--type": types.Add(val); break;
                case "--floppy-a": if (!MountFloppy(0, val)) return 2; break;
                case "--floppy-b": if (!MountFloppy(1, val)) return 2; break;
                // COLLECTÉ, pas appliqué ici : dans cette boucle les options prennent
                // effet dans l'ordre écrit, et --config poserait alors le modèle après
                // --model. La précédence doit être la même qu'en mode fenêtre — défauts,
                // puis --config, puis la ligne de commande — quel que soit l'ordre de
                // frappe. Appliqué juste avant BootTest.Run.
                case "--model": bootModel = val; break;
                // COLLECTÉS pour la même raison que --model : ils doivent s'appliquer
                // APRÈS --config, qui écrase ide_fn[] et hdc[] sans condition.
                case "--hdd": bootHdd = val; break;
                case "--hdd-type":
                    if (!int.TryParse(val, out bootHddType) || bootHddType < 1 || bootHddType > 46)
                    {
                        Console.Error.WriteLine("--hdd-type attend un type de disque du BIOS, de 1 à 46.");
                        return 2;
                    }
                    break;
                // Tranches laissées à l'application après chaque Entrée. Le défaut
                // suffit à un DIR ; un FORMAT 360 Ko en demande ~4 000.
                case "--settle":
                    if (!int.TryParse(val, out settle) || settle < 0)
                    {
                        Console.Error.WriteLine("--settle attend un entier positif.");
                        return 2;
                    }
                    break;
                // La configuration se lit ICI et pas dans la boucle principale :
                // --boot rend avant d'y arriver. Les deux positionnels de --boot lui
                // sont propres, ses options aussi.
                case "--config":
                    var cfgPath = paths.resolve_file_path(val);
                    if (cfgPath is null)
                    {
                        Console.Error.WriteLine($"Fichier de configuration introuvable : « {val} ».");
                        return 2;
                    }
                    if (!pc.loadconfig(cfgPath)) return 2;
                    break;
                default:
                    Console.Error.WriteLine($"Option inconnue après --boot : {opt}");
                    return 2;
            }
        }

        if (bootModel is not null && !pc.setmodel(bootModel))
            return 2;

        if (bootHdd is null && bootHddType >= 0)
        {
            Console.Error.WriteLine("--hdd-type sans --hdd : rien à typer.");
            return 2;
        }

        if (bootHdd is not null && !MountHdd(0, bootHdd, bootHddType))
            return 2;

        return BootTest.Run(paths.resolve_roms_path(roms), slices, types, settle);
    }

    // --floppy-a CHEMIN, --floppy-b CHEMIN : image .img montée dans le lecteur avant
    // l'amorçage — le pendant de « --load_drive_a » de PCem (pc.c:227-233), qui
    // remplit discfns[] AVANT initpc pour que resetpchard la charge (pc.c:367). Le
    // BIOS du 5150 amorce alors dessus au lieu de basculer sur la ROM BASIC.
    if (arg is "--floppy-a" or "--floppy-b")
    {
        if (i + 1 >= args.Length)
        {
            Console.Error.WriteLine($"{arg} attend le chemin d'une image de disquette.");
            return 2;
        }

        if (!MountFloppy(arg == "--floppy-a" ? 0 : 1, args[++i]))
            return 2;
        continue;
    }

    // Monter un DISQUE DUR qui existe déjà. Il n'y avait aucun chemin pour cela avant
    // M13 : --floppy-a existait, et le disque dur n'était atteignable que par --config.
    //
    // La géométrie n'est pas demandée — elle se DÉDUIT de la taille du fichier, par la
    // branche MFM de check_hd_type que PCem applique au même endroit, après son
    // sélecteur de fichiers (wx-config.c:2085).
    if (arg is "--hdd" or "--hdd-d")
    {
        if (i + 1 >= args.Length)
        {
            Console.Error.WriteLine($"{arg} attend le chemin d'une image de disque dur.");
            return 2;
        }

        hddOverride[arg == "--hdd" ? 0 : 1] = args[++i];
        continue;
    }

    // Trancher une taille ambiguë. PCem n'a pas besoin de cela : son hd_file montre la
    // géométrie déduite dans un dialogue et laisse la corriger avant de l'appliquer
    // (wx-config.c:2085-2088). Une ligne de commande n'a pas ce dialogue.
    if (arg is "--hdd-type" or "--hdd-d-type")
    {
        if (i + 1 >= args.Length || !int.TryParse(args[i + 1], out var ht) || ht < 1 || ht > 46)
        {
            Console.Error.WriteLine($"{arg} attend un type de disque du BIOS, de 1 à 46.");
            Console.Error.WriteLine("--create-hdd sans argument les liste tous.");
            return 2;
        }

        i++;
        hddTypeOverride[arg == "--hdd-type" ? 0 : 1] = ht;
        continue;
    }

    // Contrôle de fréquence ABSOLUE. Deux positionnels comme --boot, et pour la
    // même raison : la commande doit rester citable telle quelle dans un rapport
    // sans dépendre du reste de la table d'options.
    if (arg == "--timer-check")
    {
        var roms = i + 1 < args.Length ? args[++i] : "roms";

        // 300 s émulées par défaut : à 18,2 Hz cela fait ~5 460 tops, et
        // l'alignement sur les fronts ramène l'incertitude à ±33 ppm — assez
        // pour affirmer quatre chiffres significatifs. Une seconde n'en donnerait
        // que 18, soit 5,5 % de quantification, et ne prouverait rien.
        var seconds = 300;

        if (i + 1 < args.Length)
        {
            if (!int.TryParse(args[++i], out seconds) || seconds <= 0)
            {
                Console.Error.WriteLine("--timer-check attend un nombre de secondes émulées entier positif.");
                return 2;
            }
        }

        return TimerCheck.Run(paths.resolve_roms_path(roms), seconds);
    }

    // FABRIQUER une image de disque dur vierge, puis sortir. PREMIÈRE commande du dépôt
    // qui produit un fichier : --boot et --timer-check racontent, celle-ci écrit.
    //
    // Elle existe parce que la géométrie n'est pas libre et que rien, à l'exécution, ne
    // le dit : le Fixed Disk Adapter n'accepte que 17 secteurs par piste et quatre
    // couples (cylindres, têtes), et hors de là il se contente d'un warning(), annonce
    // le disque en type 0 et laisse le POST diverger. Voir Host/HddImage.cs.
    if (arg == "--create-hdd")
    {
        return CreateHdd(args, ref i, romsPath);
    }

    // FABRIQUER une disquette FORMATÉE, puis sortir. Sa voisine --create-hdd, comme le
    // « Creer une disquette vierge » du menu, produit un fichier de zéros que DOS ne
    // sait pas lire tant que FORMAT n'est pas passé DANS la machine. Celle-ci pose le
    // système de fichiers depuis l'hôte, ce qui est la moitié qui manquait pour y faire
    // entrer un fichier du disque Linux. Voir Host/FatImage.cs.
    if (arg == "--create-floppy")
    {
        return CreateFloppy(args, ref i, romsPath);
    }

    // DÉPOSER des fichiers de l'hôte dans une image, puis sortir. L'autre moitié.
    if (arg == "--floppy-put")
    {
        return FloppyPut(args, ref i);
    }

    // --config CHEMIN : le fichier de configuration machine, comme PCem (pc.c:211-226).
    // Ordre de précédence : défauts, puis fichier, puis ligne de commande. Les options
    // machine ci-dessous surchargent donc ce que le fichier a dit.
    if (arg == "--config")
    {
        if (i + 1 >= args.Length)
        {
            Console.Error.WriteLine("--config attend le chemin d'un fichier de configuration.");
            return 2;
        }

        // Refusé et non ignoré : un chemin mal tapé donnerait tous les défauts en
        // silence, donc une machine autre que celle demandée. Même politique que
        // MountFloppy et que le contrôle de répertoire de ROM d'initpc.
        // Résolu comme --rom-path et --floppy-a : tel quel depuis le répertoire
        // courant, sinon en remontant depuis le binaire. Sans cela « --config
        // ixtal26.cfg » marcherait depuis la racine du dépôt et pas depuis Rider,
        // qui lance depuis bin/Debug/net10.0 — l'échec dépendrait d'où on lance.
        configPath = paths.resolve_file_path(args[++i]);
        if (configPath is null)
        {
            Console.Error.WriteLine($"Fichier de configuration introuvable : « {args[i]} ».");
            return 2;
        }

        continue;
    }

    if (arg is "--ram" or "--drive-a" or "--drive-b")
    {
        if (i + 1 >= args.Length || !int.TryParse(args[++i], out var v) || v < 0)
        {
            Console.Error.WriteLine($"{arg} attend un entier positif.");
            return 2;
        }

        switch (arg)
        {
            case "--ram": ramOverride = v; break;
            case "--drive-a": driveOverride[0] = v; break;
            default: driveOverride[1] = v; break;
        }

        continue;
    }

    if (arg == "--model")
    {
        if (i + 1 >= args.Length)
        {
            Console.Error.WriteLine("--model attend un nom de machine.");
            return 2;
        }

        modelOverride = args[++i];
        continue;
    }

    if (arg == "--rom-path")
    {
        if (i + 1 >= args.Length)
        {
            Console.Error.WriteLine("--rom-path attend un chemin de répertoire.");
            return 2;
        }

        romsPath = args[++i];
        continue;
    }

    // Zéro est refusé pour que « maxSlices != 0 » signifie exactement « --slices a
    // été donné » : c'est ce test, et lui seul, qui autorise --headless plus bas.
    if (arg == "--slices")
    {
        if (i + 1 >= args.Length || !int.TryParse(args[++i], out maxSlices) || maxSlices < 1)
        {
            Console.Error.WriteLine("--slices attend un entier strictement positif.");
            return 2;
        }

        continue;
    }

    if (arg == "--headless")
    {
        headless = true;
        continue;
    }

    // --turbo [N] : n'attend pas l'horloge murale pendant les N premières tranches,
    // puis rend la machine au temps réel. La trajectoire ÉMULÉE est rigoureusement la
    // même — mêmes instructions, mêmes cycles, même écran ; seule change la vitesse à
    // laquelle l'hôte la déroule. Le test mémoire de 640 Ko dure 46 s et c'est
    // authentique (§ M4.6) ; les regarder passer est un choix, pas une obligation.
    if (arg == "--turbo")
    {
        turboSlices = DefaultTurboSlices;

        // Un nombre ne peut pas commencer par un tiret : ce test distingue « --turbo
        // 3000 » de « --turbo --verbose » sans consommer l'option suivante.
        if (i + 1 < args.Length && !args[i + 1].StartsWith('-'))
        {
            if (!int.TryParse(args[++i], out turboSlices) || turboSlices < 1)
            {
                Console.Error.WriteLine("--turbo attend un nombre de tranches entier positif.");
                return 2;
            }
        }

        continue;
    }

    // Sans compteur de blits, une exécution muette ne distingue pas « le CGA a balayé
    // et l'hôte a téléversé » de « rien n'est jamais arrivé à la texture » : dans les
    // deux cas la fenêtre est noire et le code de sortie vaut 0.
    // OUVRIR l'écran de construction, même quand des arguments ont déjà parlé. Le
    // pendant du Configuration Manager de PCem, que `config_override` saute quand
    // --config est passé (wx-sdl2.c:481-488) : ici l'écran s'ouvre tout seul sur un
    // lancement NU, et --setup le force.
    // Auto-contrôle de l'écran de construction. Il existe parce que ce code n'a aucun
    // autre moyen d'être exécuté : rien ici ne donne le focus à une fenêtre SDL. Même
    // motif que config-check pour la moitié écriture du moteur de configuration.
    if (arg == "--setup-check")
        return iXtal26.Host.SdlSetup.SelfCheck(paths.resolve_roms_path(romsPath));

    // Auto-contrôle du formateur de disquette, même motif : un système de fichiers faux
    // ne se voit pas à l'œil — l'image a la bonne taille et le bon nombre de secteurs,
    // et DOS la lit de travers en silence. Il porte aussi l'invariant de géométrie, qui
    // ne peut se mesurer qu'en chargeant vraiment une image par les deux branches
    // d'img_load.
    if (arg == "--fat-check")
        return iXtal26.Host.FatImage.SelfCheck();

    // Auto-contrôle du menu Ctrl+F12, pour la même raison que --setup-check : ses chemins
    // clavier n'ont aucun autre moyen d'être exécutés.
    if (arg == "--menu-check")
        return iXtal26.Host.SdlMenu.SelfCheck(paths.resolve_roms_path(romsPath));

    if (arg == "--setup")
    {
        setup = true;
        continue;
    }

    if (arg == "--verbose")
    {
        verbose = true;
        continue;
    }

    // --frames n'est pas devenu un synonyme de --slices : il a été retiré. Une image
    // CGA dure 1/60 s et une tranche pc.runpc() 10 ms émulées, donc « --frames 60 »
    // aurait voulu dire 600 ms au lieu de 60 images — un compteur qui ment sans
    // prévenir. Le diagnostic reste explicite parce que README.md cite encore l'option.
    if (arg == "--frames")
    {
        Console.Error.WriteLine("--frames a été retiré : utiliser --slices N.");
        Console.Error.WriteLine("Une tranche vaut 10 ms émulées, pas une image : les deux comptes diffèrent.");
        return 2;
    }

    Console.Error.WriteLine($"Argument inconnu : {arg}");
    PrintUsage();
    return 2;
}

// Vérifié après la boucle pour que l'ordre des deux options n'ait pas d'importance.
// Sans borne, une exécution sans fenêtre n'a plus rien pour l'arrêter : ni Quit SDL,
// ni Échap.
if (headless && maxSlices == 0)
{
    Console.Error.WriteLine("--headless exige --slices N : sans fenêtre, rien ne peut demander l'arrêt.");
    return 2;
}

// Refusé plutôt qu'ignoré : --slices N ne cadence RIEN, de la première tranche à la
// dernière (c'est ce qui rend deux exécutions identiques). Accepter --turbo à côté
// donnerait une option sans effet, indiscernable d'une option qui ne marche pas.
if (turboSlices > 0 && maxSlices > 0)
{
    Console.Error.WriteLine("--turbo et --slices sont incompatibles : --slices n'attend jamais l'horloge murale.");
    return 2;
}

// Résolu ICI, et pas dans initpc : c'est l'hôte qui sait d'où il a été lancé, et
// initpc ne fait que consommer le chemin qu'on lui tend. Sans cela, « roms » était
// interprété depuis le répertoire courant — bin/Debug/net10.0/ sous Rider, où il
// n'existe pas — et l'amorçage échouait selon l'endroit d'où on lançait le binaire.
// Voir paths.resolve_roms_path.
romsPath = paths.resolve_roms_path(romsPath);

// Défauts → fichier → ligne de commande, dans cet ordre, et AVANT initpc : c'est la
// séquence de pc_main, qui appelle loadconfig() puis initpc() (wx-sdl2.c:451, :459).
// Sans --config on ne lit aucun fichier : un lancement nu reste un lancement nu, et
// rend la machine que VERIFICATION.md décrit.
if (configPath is not null && !pc.loadconfig(configPath))
    return 2;

// --model AVANT --ram : c'est le modèle qui porte les bornes mémoire, et le XT est à
// 64 Ko de granularité là où le 5150 est à 32. Dans l'autre ordre, --model ibmxt --ram 96
// passerait le contrôle du 5150 puis monterait une machine que PCem ne décrit pas.
if (modelOverride is not null && !pc.setmodel(modelOverride))
    return 2;

// Refusé, pas corrigé : une taille tapée en ligne de commande est explicite. Avant M10
// cette affectation ne passait par AUCUN contrôle — le seul chemin du dépôt qui pouvait
// fabriquer un SW2 absurde sans rien dire.
if (ramOverride >= 0)
{
    if (!pc.check_mem_size(ramOverride))
        return 2;
    pc.cfg_mem_size = ramOverride;
}

for (var d = 0; d < 2; d++)
    if (driveOverride[d] >= 0)
        pc.cfg_drive_type[d] = driveOverride[d];

for (var d = 0; d < 2; d++)
{
    if (hddOverride[d] is null && hddTypeOverride[d] >= 0)
    {
        Console.Error.WriteLine(
            $"--hdd{(d == 0 ? "" : "-d")}-type sans --hdd{(d == 0 ? "" : "-d")} : rien à typer.");
        return 2;
    }

    if (hddOverride[d] is not null && !MountHdd(d, hddOverride[d]!, hddTypeOverride[d]))
        return 2;
}

if (verbose)
    // La MACHINE en tête, comme BootDiff l'a gagnée à M10 : depuis qu'il y en a deux,
    // une ligne de diagnostic qui ne la nomme pas laisse croire qu'il n'y en a qu'une.
    Console.WriteLine($"machine : {iXtal26.Models.model_c.models[iXtal26.Models.model_c.model].name}, " +
                      $"mem_size = {pc.cfg_mem_size} Ko, " +
                      $"lecteurs {pc.cfg_drive_type[0]}/{pc.cfg_drive_type[1]}" +
                      (configPath is null ? " (défauts)" : $" ({configPath})"));

// L'ÉCRAN S'OUVRE SUR UN LANCEMENT NU, et seulement là. Dès qu'un argument décrit la
// machine, on la monte telle qu'il l'a dite : toutes les recettes de VERIFICATION.md et
// les six profils de Rider gardent leur comportement au cycle près. --setup passe outre.
//
// --slices et --headless ne le voient jamais non plus : le premier existe pour que deux
// exécutions traversent les mêmes états, et un écran qui attend une touche n'a pas sa
// place dans ce contrat — c'est le même arbitrage que le menu Ctrl+F12, inerte sous
// --slices depuis M7.
var machineChosen = configPath is not null || modelOverride is not null || ramOverride >= 0 ||
                    driveOverride[0] >= 0 || driveOverride[1] >= 0 ||
                    hddOverride[0] is not null || hddOverride[1] is not null ||
                    iXtal26.Floppy.fdd_c.discfns[0].Length != 0 ||
                    iXtal26.Floppy.fdd_c.discfns[1].Length != 0;

var showSetup = (setup || !machineChosen) && !headless && maxSlices <= 0;

using var host = new SdlHost(romsPath, headless, maxSlices, verbose, turboSlices);

if (!host.Init(showSetup))
    // Renoncer n'est pas échouer : quitter l'écran de construction sort par 0.
    return host.SetupCancelled ? 0 : 1;

// Pas de try/catch : pc.fatal() lève, et une trace d'exception est précisément le
// signal que le cœur est fait pour émettre. L'étouffer ici le perdrait.
return host.Run();

// Monte une image dans le lecteur : résout le chemin comme resolve_roms_path résout
// un répertoire (tel quel depuis le répertoire courant, sinon en remontant depuis
// le binaire), puis la dépose dans discfns[] pour que resetpchard la charge. Une
// image absente est refusée ICI, avec son chemin : disc_load, lui, se tairait et
// laisserait le lecteur vide — le BIOS irait sur BASIC et rien ne dirait pourquoi.
/// <summary>
/// Monte une image de DISQUE DUR existante, géométrie déduite de sa taille.
///
/// Trois écarts avec MountFloppy, tous imposés par le matériel :
///
///   1. La géométrie d'une disquette se déduit de la taille PAR LE CŒUR, dans img_load
///      (disc_img.cs). Celle d'un disque dur vient de la CONFIGURATION — hdd_load_ext
///      pose spt/hpc/tracks depuis ses paramètres — donc c'est ici qu'il faut la
///      calculer, et c'est check_hd_type qui le fait chez PCem.
///   2. Une taille qui ne correspond à aucun type est REFUSÉE. Le repli 63/16 du C
///      (wx-config.c:1355-1357) existe pour les contrôleurs IDE, que ce dépôt n'a pas :
///      les deux cartes transcrites câblent 17 secteurs dans leur ADRESSAGE
///      (xebec_get_sector), donc un disque à 63 secteurs serait annoncé sans être
///      adressable. Mieux vaut le dire que produire une machine qui diverge au POST.
///   3. Une carte est posée si la configuration n'en a pas nommé : sans contrôleur, une
///      image montée n'est vue par personne.
/// </summary>
static bool MountHdd(int drive, string path, int forcedType)
{
    var resolved = paths.resolve_file_path(path);

    if (resolved is null)
    {
        Console.Error.WriteLine($"Image de disque dur introuvable : « {path} ».");
        Console.Error.WriteLine("Pour en fabriquer une : --create-hdd (sans argument, il liste les types).");
        return false;
    }

    long size;

    try
    {
        size = new FileInfo(resolved).Length;
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
    {
        Console.Error.WriteLine($"Image de disque dur illisible : « {resolved} » — {ex.Message}");
        return false;
    }

    var (cylinders, heads, spt, type) = HddImage.GuessGeometry(size);

    // Un type imposé l'emporte, mais seulement s'il décrit BIEN ce fichier : accepter
    // « --hdd-type 16 » sur une image de 10 Mo monterait un disque deux fois trop grand,
    // dont la moitié n'existe pas.
    if (forcedType >= 1)
    {
        var (fc, fh) = HddImage.hd_types[forcedType - 1];

        if (HddImage.SizeOf(fc, fh, HddImage.TypeSectorsPerTrack) != size)
        {
            Console.Error.WriteLine(
                $"--hdd-type {forcedType} décrit {HddImage.SizeOf(fc, fh, HddImage.TypeSectorsPerTrack)} " +
                $"octets, mais « {resolved} » en fait {size}.");
            return false;
        }

        (cylinders, heads, spt, type) = (fc, fh, HddImage.TypeSectorsPerTrack, forcedType);
    }

    if (type == 0)
    {
        Console.Error.WriteLine(
            $"« {resolved} » fait {size} octets, ce qui ne correspond à aucun des 46 types " +
            "de disque du BIOS.");
        Console.Error.WriteLine(
            "Les cartes transcrites câblent 17 secteurs par piste : une géométrie déduite " +
            "autrement serait annoncée sans être adressable.");
        Console.Error.WriteLine("--create-hdd liste les tailles admises.");
        return false;
    }

    iXtal26.Disc.hdd_c.ide_fn[drive] = resolved;
    iXtal26.Disc.hdd_c.hdc[drive].spt = spt;
    iXtal26.Disc.hdd_c.hdc[drive].hpc = heads;
    iXtal26.Disc.hdd_c.hdc[drive].tracks = cylinders;

    if (pc.cfg_hdd_controller.Length == 0)
        pc.cfg_hdd_controller = "mfm_xebec";

    Console.WriteLine($"Disque {(drive == 0 ? "C" : "D")}: {resolved} — {HddImage.Label(type)}" +
                      $", carte {pc.cfg_hdd_controller}");

    // La taille ne suffit pas toujours à nommer un type, et il faut le DIRE : sept
    // tailles de la table en désignent plusieurs, et pour l'une d'elles les deux
    // candidats ont une géométrie DIFFÉRENTE et sont tous deux acceptés par le Fixed
    // Disk Adapter — 21 307 392 octets, c'est le type 13 (306 x 8) ou le type 16
    // (612 x 4). Se tromper garde la bonne capacité et change l'adressage CHS, donc le
    // système de fichiers se lit de travers sans qu'aucune erreur n'apparaisse.
    // La carte accepte-t-elle seulement cette géométrie ? Sans ce mot, une image de
    // taille valide mais de géométrie inconnue de la carte donne un POST qui diverge,
    // et xebec_set_switches se contente d'un warning() que personne ne lit.
    if (HddImage.XebecSwitch(cylinders, heads, spt) < 0)
    {
        Console.Error.WriteLine(
            $"  ATTENTION : le Fixed Disk Adapter n'accepte pas {cylinders} x {heads}. Il " +
            "annoncera le disque en type 0 et le POST divergera.");
        Console.Error.WriteLine("  Les géométries admises sont listées par --create-hdd.");
    }

    var candidates = HddImage.TypesWithSize(size, out var ambiguous);

    if (ambiguous && forcedType < 0)
    {
        Console.Error.WriteLine($"  ATTENTION : {size} octets ne désigne pas un type unique.");

        foreach (var t in candidates)
            Console.Error.WriteLine($"    {HddImage.Label(t)}");

        Console.Error.WriteLine(
            $"  Le type {type:D2} a été retenu, comme le ferait PCem. Si l'image a été " +
            $"formatée avec une autre géométrie, --hdd{(drive == 0 ? "" : "-d")}-type N le dit.");
    }

    return true;
}

static bool MountFloppy(int drive, string path)
{
    // La remontée vit dans paths.resolve_file_path, pour qu'il n'existe qu'UNE
    // politique de résolution de fichier : la clé disc_a d'un fichier de configuration
    // doit se comporter exactement comme --floppy-a, sans quoi la même image marche
    // par un chemin et pas par l'autre.
    var resolved = paths.resolve_file_path(path);

    if (resolved is null)
    {
        Console.Error.WriteLine($"Image de disquette introuvable : « {path} ».");
        return false;
    }

    iXtal26.Floppy.fdd_c.discfns[drive] = resolved;
    return true;
}

/// <summary>
/// Les 46 types de disque du BIOS, et lesquels la carte du dépôt sait adresser. Lister
/// est le service le plus utile de cette commande : la table est longue, ses doublons
/// ne se voient pas, et son entrée 15 est un piège.
/// </summary>
static void PrintHddTypes()
{
    Console.WriteLine("Les 46 types de disque du BIOS (pcem-dev/src/wx-ui/wx-config.c:1295-1302).");
    Console.WriteLine("17 secteurs par piste, implicites dans toute la table.");
    Console.WriteLine();
    Console.WriteLine("« * » : géométrie que l'IBM Fixed Disk Adapter accepte. Les autres se créent,");
    Console.WriteLine("mais la carte n'en dira qu'un avertissement, annoncera le disque en type 0 et");
    Console.WriteLine("le POST divergera. La carte dtc5150x, elle, n'impose aucune restriction.");
    Console.WriteLine();

    for (var t = 1; t <= 46; t++)
    {
        var (cyl, heads) = HddImage.hd_types[t - 1];

        // Le type 15 est RÉSERVÉ dans la table de l'IBM AT, et PCem le laisse dans sa
        // liste déroulante où il affiche « size=0MB ». On le montre aussi — le cacher
        // décalerait les numéros — mais on dit ce qu'il est.
        if (cyl == 0 || heads == 0)
        {
            Console.WriteLine($"    {HddImage.Label(t)}   (réservé, non créable)");
            continue;
        }

        var sw = HddImage.XebecSwitch(cyl, heads, HddImage.TypeSectorsPerTrack);

        Console.WriteLine($"  {(sw >= 0 ? "*" : " ")} {HddImage.Label(t)}" +
                          $"   {HddImage.SizeOf(cyl, heads, HddImage.TypeSectorsPerTrack)} octets" +
                          (sw >= 0 ? $", interrupteurs type {sw}" : ""));
    }
}

/// <summary>
/// --create-floppy. Même forme que CreateHdd, et pour les mêmes raisons : sans argument
/// on LISTE et on sort sans rien créer, et la garde StartsWith('-') empêche
/// « --create-floppy 360k -v » de créer un fichier nommé « -v ».
/// </summary>
static int CreateFloppy(string[] args, ref int i, string romsPath)
{
    if (i + 1 >= args.Length || args[i + 1].StartsWith('-'))
    {
        Console.WriteLine("Formats de disquette (les seuls que le lecteur 5,25\" DD du 5150 sait lire) :");
        Console.WriteLine();

        for (var c = 0; c < FatImage.Formats.Length; c++)
        {
            var g = FatImage.Formats[c];

            Console.WriteLine($"  {g.Stem,-5} {g.Name}");
            Console.WriteLine($"        {FatImage.ClusterCount(g)} clusters de " +
                              $"{FatImage.BytesPerCluster(g)} octets, " +
                              $"{g.RootEntries} entrées de répertoire, " +
                              $"média 0x{g.MediaDescriptor:X2}");
        }

        Console.WriteLine();
        Console.WriteLine("L'image est formatée en FAT12, vide et NON SYSTÈME : pour la rendre");
        Console.WriteLine("amorçable, faire SYS B: depuis DOS dans la machine.");
        return 0;
    }

    var spec = args[++i];
    var index = FatImage.FormatIndex(spec);

    if (index < 0)
    {
        Console.Error.WriteLine($"--create-floppy : « {spec} » n'est pas un format connu.");
        Console.Error.WriteLine("--create-floppy sans argument les liste tous.");
        return 2;
    }

    var format = FatImage.Formats[index];

    // Le chemin donné, sinon le premier nom libre dans os/ — le MÊME os/ que le menu
    // Ctrl+F12 et que --create-hdd, par le même ImagesRoot.
    string path;

    if (i + 1 < args.Length && !args[i + 1].StartsWith('-'))
    {
        path = args[++i];
    }
    else
    {
        var root = SdlMenu.ImagesRoot(romsPath);

        try
        {
            // os/ est .gitignore'd, donc absent d'un clone neuf.
            Directory.CreateDirectory(root);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"--create-floppy : création de {root} impossible : {ex.Message}");
            return 1;
        }

        var free = SdlMenu.FreeName(root, "fat" + format.Stem);

        if (free is null)
        {
            Console.Error.WriteLine($"--create-floppy : trop d'images « vierge-fat{format.Stem} » dans {root}.");
            return 1;
        }

        path = free;
    }

    Console.WriteLine($"Création de {path}");
    Console.WriteLine($"  format      : {format.Name}");
    Console.WriteLine($"  taille      : {FatImage.ImageSize(format)} octets " +
                      $"({FatImage.ImageSize(format) / 1024} Ko)");
    Console.WriteLine($"  FAT12       : {format.NumberOfFats} copies de " +
                      $"{format.SectorsPerFat} secteur(s), " +
                      $"{FatImage.ClusterCount(format)} clusters de " +
                      $"{FatImage.BytesPerCluster(format)} octets");
    Console.WriteLine($"  racine      : {format.RootEntries} entrées, " +
                      $"au secteur {format.ReservedSectors + format.NumberOfFats * format.SectorsPerFat}");

    if (!FatImage.Create(path, format, out var message))
    {
        Console.Error.WriteLine($"--create-floppy : {message}");
        return 1;
    }

    Console.WriteLine(message);
    Console.WriteLine();
    Console.WriteLine("Disquette NON SYSTÈME : amorcer dessus affiche un message et attend une");
    Console.WriteLine("touche, en boucle. Pour y déposer des fichiers : --floppy-put.");
    return 0;
}

/// <summary>
/// --floppy-put IMAGE FICHIER... — déposer des fichiers de l'hôte dans une image.
/// </summary>
static int FloppyPut(string[] args, ref int i)
{
    if (i + 1 >= args.Length || args[i + 1].StartsWith('-'))
    {
        Console.Error.WriteLine("--floppy-put attend une image puis au moins un fichier.");
        Console.Error.WriteLine("  --floppy-put os/disquette.img /home/moi/PROG.COM LISEZ.TXT");
        return 2;
    }

    // Résolu comme --floppy-a et --config : tel quel depuis le répertoire courant, sinon
    // en remontant depuis le binaire. Sans cela la commande marcherait depuis la racine
    // du dépôt et pas depuis Rider, qui lance depuis bin/Debug/net10.0.
    var image = paths.resolve_file_path(args[++i]);

    if (image is null)
    {
        Console.Error.WriteLine($"--floppy-put : image introuvable « {args[i]} ».");
        return 2;
    }

    var n = 0;

    while (i + 1 + n < args.Length && !args[i + 1 + n].StartsWith('-'))
        n++;

    if (n == 0)
    {
        Console.Error.WriteLine("--floppy-put : aucun fichier à déposer.");
        return 2;
    }

    var sources = new string[n];

    for (var c = 0; c < n; c++)
        sources[c] = args[++i];

    if (!FatImage.Put(image, sources, out var message))
    {
        Console.Error.WriteLine($"--floppy-put : {message}");
        return 1;
    }

    Console.WriteLine(message);
    return 0;
}

/// <summary>
/// --create-hdd. Rend un code de sortie : 0 succès, 1 échec d'exécution, 2 erreur
/// d'usage, comme le reste de la table.
/// </summary>
static int CreateHdd(string[] args, ref int i, string romsPath)
{
    // Sans argument, on liste et on sort SANS RIEN CRÉER. Un défaut implicite qui
    // fabriquerait 10 Mo parce qu'on a tapé la commande pour voir serait exactement le
    // genre de surprise que ce dépôt refuse ailleurs.
    // StartsWith('-') et non "--", comme --turbo : sans cela « --create-hdd 1 -v »
    // avalerait -v comme chemin et créerait un fichier nommé « -v ». Aucun type ni
    // géométrie valide ne commence par un tiret.
    if (i + 1 >= args.Length || args[i + 1].StartsWith('-'))
    {
        PrintHddTypes();
        return 0;
    }

    var spec = args[++i];
    int cylinders, heads, spt;

    if (spec.Contains(','))
    {
        // La saisie libre du dialogue de PCem (wx-config.c:1613-1621), en ligne de
        // commande et pas au menu : saisir trois nombres dans une surimpression SDL
        // demanderait un éditeur de texte que le menu n'a pas — il n'a qu'une liste et
        // le sélecteur de fichiers du système.
        var f = spec.Split(',');

        if (f.Length != 3 || !int.TryParse(f[0], out cylinders) ||
            !int.TryParse(f[1], out heads) || !int.TryParse(f[2], out spt))
        {
            Console.Error.WriteLine(
                $"--create-hdd : géométrie illisible « {spec} », attendu CYLINDRES,TETES,SECTEURS.");
            return 2;
        }
    }
    else
    {
        if (!int.TryParse(spec, out var type) || type < 1 || type > 46)
        {
            Console.Error.WriteLine(
                $"--create-hdd : « {spec} » n'est ni un type de 1 à 46, ni une géométrie " +
                "CYLINDRES,TETES,SECTEURS.");
            Console.Error.WriteLine("--create-hdd sans argument liste les 46 types.");
            return 2;
        }

        (cylinders, heads) = HddImage.hd_types[type - 1];
        spt = HddImage.TypeSectorsPerTrack;
    }

    if (!HddImage.Validate(cylinders, heads, spt, out var error))
    {
        Console.Error.WriteLine($"--create-hdd : {error}");
        return 2;
    }

    // Le chemin donné, sinon le premier nom libre dans os/ — le MÊME os/ que le menu
    // Ctrl+F12, par le même ImagesRoot : deux résolutions, ce sont deux répertoires qui
    // finissent par différer, et une image invisible dans la liste du menu.
    string path;

    var imagesRoot = SdlMenu.ImagesRoot(romsPath);

    if (i + 1 < args.Length && !args[i + 1].StartsWith('-'))
    {
        path = args[++i];
    }
    else
    {
        var root = imagesRoot;

        try
        {
            // os/ est .gitignore'd, donc absent d'un clone neuf.
            Directory.CreateDirectory(root);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"--create-hdd : création de {root} impossible : {ex.Message}");
            return 1;
        }

        var known = HddImage.TypeFor(cylinders, heads, spt);
        var stem = known != 0 ? $"hdd-type{known:D2}" : $"hdd-{cylinders}x{heads}x{spt}";
        var free = SdlMenu.FreeName(root, stem);

        if (free is null)
        {
            Console.Error.WriteLine($"--create-hdd : trop d'images « vierge-{stem} » dans {root}.");
            return 1;
        }

        path = free;
    }

    var sectors = (long)cylinders * heads * spt;
    var bytes = HddImage.SizeOf(cylinders, heads, spt);

    Console.WriteLine($"Création de {path}");
    Console.WriteLine($"  géométrie   : {cylinders} cylindres x {heads} têtes x {spt} secteurs " +
                      $"= {sectors} secteurs");
    Console.WriteLine($"  taille      : {bytes} octets ({bytes / (1024 * 1024)} Mo)");

    var biosType = HddImage.TypeFor(cylinders, heads, spt);
    Console.WriteLine(biosType != 0
        ? $"  type BIOS   : {HddImage.Label(biosType)}"
        : "  type BIOS   : aucun — « Custom type » chez PCem");

    var switches = HddImage.XebecSwitch(cylinders, heads, spt);
    Console.WriteLine(switches >= 0
        ? $"  carte       : acceptée par l'IBM Fixed Disk Adapter, interrupteurs type {switches}"
        : "  carte       : REFUSÉE par l'IBM Fixed Disk Adapter — il n'accepte que 17 secteurs\n" +
          "                et (306,4) (612,4) (615,4) (306,8). Il n'en dira qu'un\n" +
          "                avertissement, annoncera le disque en type 0, et le POST\n" +
          "                divergera. Sans objet pour la carte dtc5150x.");

    if (!HddImage.Create(path, cylinders, heads, spt, out var message))
    {
        Console.Error.WriteLine($"--create-hdd : {message}");
        return 1;
    }

    Console.WriteLine(message);
    Console.WriteLine();
    Console.WriteLine("À ajouter au fichier de configuration :");
    Console.WriteLine();
    Console.Write(HddImage.ConfigBlock(HddImage.ConfigPath(path, imagesRoot), cylinders, heads, spt));

    return 0;
}

static void PrintUsage()
{
    Console.WriteLine("Usage : iXtal26 [--rom-path CHEMIN] [--floppy-a IMG] [--floppy-b IMG] [--slices N]");
    Console.WriteLine("                [--headless] [--verbose] [--turbo [N]]");
    Console.WriteLine("        iXtal26 --boot [CHEMIN] [N] [--floppy-a IMG] [--type TEXTE]...");
    Console.WriteLine("        iXtal26 --timer-check [CHEMIN] [SECONDES]");
    Console.WriteLine();
    Console.WriteLine("Sans argument : ouvre une fenêtre et émule l'IBM PC 5150 jusqu'à sa fermeture.");
    Console.WriteLine("Dans la fenêtre, Ctrl+F12 ouvre le menu : insérer ou éjecter une disquette,");
    Console.WriteLine("réinitialiser la machine. La disquette en place au moment du reset est celle");
    Console.WriteLine("sur laquelle le BIOS amorce.");
    Console.WriteLine();
    Console.WriteLine("  --config CHEMIN      fichier de configuration machine (format .cfg de PCem :");
    Console.WriteLine("                       « clé = valeur », sections [entre crochets], # en");
    Console.WriteLine("                       commentaire). Clés : model, mem_size, drive_a_type,");
    Console.WriteLine("                       drive_b_type, disc_a, disc_b, bpb_disable");
    Console.WriteLine("  --model NOM          machine : ibmpc (IBM PC 5150) ou ibmxt (IBM XT 5160).");
    Console.WriteLine("                       Un nom inconnu est refusé en citant ce qui existe");
    Console.WriteLine("  --ram N              taille RAM en Ko. Les bornes viennent de la MACHINE :");
    Console.WriteLine("                       64 à 640 par pas de 32 sur ibmpc, par pas de 64 sur ibmxt");
    Console.WriteLine("  --drive-a T          type de lecteur : 0 aucun, 1 5,25\" DD (le 5150),");
    Console.WriteLine("                       2 5,25\" HD, 4 3,5\" DD, 5 3,5\" HD. --drive-b T de même");
    Console.WriteLine();
    Console.WriteLine("  Précédence : défauts, puis --config, puis les options ci-dessus. Sans");
    Console.WriteLine("  --config aucun fichier n'est lu, et la machine est celle que décrit");
    Console.WriteLine("  VERIFICATION.md : 640 Ko, deux lecteurs 5,25\" DD, CGA.");
    Console.WriteLine();
    Console.WriteLine("  --rom-path CHEMIN    où chercher les images de ROM du 5150 (défaut : roms,");
    Console.WriteLine("                       cherché d'abord depuis le répertoire courant, puis en");
    Console.WriteLine("                       remontant depuis l'emplacement du binaire)");
    Console.WriteLine("  --floppy-a IMG       image .img (brute, 160 à 360 Ko sur ce lecteur 5,25\" DD)");
    Console.WriteLine("                       montée dans le lecteur A: avant l'amorçage ; le BIOS");
    Console.WriteLine("                       démarre dessus. --floppy-b IMG : le lecteur B:.");
    Console.WriteLine("                       Ouverte en lecture-écriture : DOS y écrit pour de vrai");
    Console.WriteLine("  --slices N           s'arrête au bout de N tranches de 10 ms émulées, et");
    Console.WriteLine("                       n'attend pas l'horloge murale entre elles : deux");
    Console.WriteLine("                       exécutions traversent alors les mêmes états. Ctrl+F12");
    Console.WriteLine("                       y est inerte, pour ne pas ruiner ce contrat");
    Console.WriteLine("  --headless           n'initialise aucune vidéo SDL ; exige --slices");
    Console.WriteLine("  --verbose            en fin d'exécution, compte les blits émis par le CGA");
    Console.WriteLine("                       et ceux réellement téléversés dans la texture");
    Console.WriteLine($"  --turbo [N]          n'attend pas l'horloge murale pendant les N premières");
    Console.WriteLine($"                       tranches (défaut : {DefaultTurboSlices}, soit juste après l'invite");
    Console.WriteLine("                       BASIC), puis rend la machine au temps réel. Le POST du");
    Console.WriteLine("                       5150 dure 57 s, dont 46 s de test mémoire : c'est");
    Console.WriteLine("                       authentique, et la trajectoire émulée est inchangée —");
    Console.WriteLine("                       seule la vitesse à laquelle l'hôte la déroule change.");
    Console.WriteLine("                       Une frappe y met fin. Incompatible avec --slices, qui");
    Console.WriteLine("                       n'attend déjà jamais l'horloge");
    Console.WriteLine("  --boot [CHEMIN] [N]  amorce et raconte en console ce que le POST a écrit");
    Console.WriteLine("                       en mémoire et à l'écran (défauts : roms, 20 tranches)");
    Console.WriteLine("      --hdd IMG        après --boot : monte un disque dur, comme l'option");
    Console.WriteLine("                       principale. --hdd-type N la complète");
    Console.WriteLine("      --type TEXTE     après --boot : tape TEXTE puis Entrée dans la machine,");
    Console.WriteLine("                       et revide l'écran. Répétable, dans l'ordre. C'est la");
    Console.WriteLine("                       seule vérification du chemin clavier qui ne dépende");
    Console.WriteLine("                       pas d'une fenêtre ayant le focus");
    Console.WriteLine("  --hdd IMG            monte une image de disque dur EXISTANTE en C:, géométrie");
    Console.WriteLine("                       déduite de sa taille. --hdd-d IMG : le disque D:. Une");
    Console.WriteLine("                       taille qui ne correspond à aucun des 46 types du BIOS");
    Console.WriteLine("                       est refusée. Pose la carte mfm_xebec si --config n'en a");
    Console.WriteLine("                       pas nommé, et l'emporte sur les clés hdc_*/hdd_*");
    Console.WriteLine("  --hdd-type N         force le type de disque de C: quand sa taille en désigne");
    Console.WriteLine("                       plusieurs — 21 307 392 octets, c'est le type 13 (306x8)");
    Console.WriteLine("                       ou le type 16 (612x4). --hdd-d-type N : le disque D:");
    Console.WriteLine("  --create-hdd [TYPE|CYL,TETES,SECT] [CHEMIN]");
    Console.WriteLine("                       fabrique une image de disque dur VIERGE — des zéros, à");
    Console.WriteLine("                       la taille exacte de la géométrie — puis imprime les");
    Console.WriteLine("                       clés à coller dans un .cfg. Sans argument : liste les");
    Console.WriteLine("                       46 types de disque du BIOS et sort sans rien créer.");
    Console.WriteLine("                       TYPE va de 1 à 46 ; CYL,TETES,SECT est la saisie libre,");
    Console.WriteLine("                       bornée comme chez PCem (secteurs 63, têtes 16). Sans");
    Console.WriteLine("                       CHEMIN, écrit le premier nom libre dans os/. Un fichier");
    Console.WriteLine("                       existant est refusé, jamais écrasé");
    Console.WriteLine("  --create-floppy [FORMAT] [CHEMIN]");
    Console.WriteLine("                       fabrique une image de disquette FORMATÉE en FAT12, vide");
    Console.WriteLine("                       et non système : secteur d'amorce, BPB, deux FAT,");
    Console.WriteLine("                       répertoire racine. Contrairement au « Creer une");
    Console.WriteLine("                       disquette vierge » du menu, qui écrit des zéros que DOS");
    Console.WriteLine("                       ne sait pas lire tant que FORMAT n'est pas passé DANS la");
    Console.WriteLine("                       machine. Sans argument : liste les quatre formats et sort");
    Console.WriteLine("                       sans rien créer. FORMAT vaut 160k, 180k, 320k ou 360k —");
    Console.WriteLine("                       les seuls que le lecteur 5,25\" DD du 5150 sait lire.");
    Console.WriteLine("                       Sans CHEMIN, écrit le premier nom libre dans os/. Un");
    Console.WriteLine("                       fichier existant est refusé, jamais écrasé");
    Console.WriteLine("  --floppy-put IMAGE FICHIER...");
    Console.WriteLine("                       dépose des fichiers du disque hôte dans la racine d'une");
    Console.WriteLine("                       image formatée. Les noms doivent tenir en 8.3 : un nom");
    Console.WriteLine("                       trop long est REFUSÉ, jamais tronqué — tronquer");
    Console.WriteLine("                       fabriquerait un doublon silencieux. Refuse aussi une");
    Console.WriteLine("                       image montée dans A: ou B:, qu'il faut éjecter d'abord");
    Console.WriteLine("  --fat-check          auto-contrôle du formateur : l'empaquetage FAT12 contre");
    Console.WriteLine("                       la FAT d'une disquette réellement formatée par DOS 2.00,");
    Console.WriteLine("                       le secteur d'amorce, et l'invariant de géométrie — les");
    Console.WriteLine("                       deux branches d'img_load doivent lire la même chose");
    Console.WriteLine("  --timer-check [CHEMIN] [SECONDES]");
    Console.WriteLine("                       amorce, vérifie que l'INT 8 du BIOS tourne, puis");
    Console.WriteLine("                       compte les tops de la BDA (0040:006C) sur SECONDES");
    Console.WriteLine("                       secondes ÉMULÉES et compare à 1193182/65536 =");
    Console.WriteLine("                       18,2065 Hz (défauts : roms, 300 s)");
    Console.WriteLine("  --setup              ouvre l'écran de construction de machine avant de");
    Console.WriteLine("                       démarrer : modèle, mémoire, disquettes, disques durs,");
    Console.WriteLine("                       charger ou enregistrer une machine de configs/. Il");
    Console.WriteLine("                       s'ouvre DÉJÀ tout seul sur un lancement sans argument ;");
    Console.WriteLine("                       jamais sous --slices, --headless ni --boot");
    Console.WriteLine("  --setup-check        auto-contrôle de l'écran de construction, sans fenêtre :");
    Console.WriteLine("                       navigation, bascules d'écran, recalage de la liste. Ce");
    Console.WriteLine("                       code n'a aucun autre moyen d'être exécuté ici");
    Console.WriteLine("  -h, --help           affiche cette aide");
    Console.WriteLine();
    Console.WriteLine("Codes de sortie : 0 succès, 1 échec d'exécution, 2 erreur d'usage.");
}
