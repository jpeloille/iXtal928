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
var turboSlices = 0; // 0 = pas de turbo : le POST se déroule à sa vitesse d'époque

// L'invite BASIC tombe à la tranche 5 729 (VERIFICATION.md § M4.6) ; on laisse de quoi
// voir la bannière s'écrire, et rendre la main un peu APRÈS plutôt qu'un peu avant.
const int DefaultTurboSlices = 5800;

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
        // d'un gestionnaire de fenêtres.
        string? type = null;
        if (i + 1 < args.Length && args[i + 1] == "--type")
        {
            i++;
            if (i + 1 >= args.Length)
            {
                Console.Error.WriteLine("--type attend une chaîne à taper.");
                return 2;
            }

            type = args[++i];
        }

        return BootTest.Run(paths.resolve_roms_path(roms), slices, type);
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

using var host = new SdlHost(romsPath, headless, maxSlices, verbose, turboSlices);

if (!host.Init())
    return 1;

// Pas de try/catch : pc.fatal() lève, et une trace d'exception est précisément le
// signal que le cœur est fait pour émettre. L'étouffer ici le perdrait.
return host.Run();

static void PrintUsage()
{
    Console.WriteLine("Usage : iXtal26 [--rom-path CHEMIN] [--slices N] [--headless] [--verbose] [--turbo [N]]");
    Console.WriteLine("        iXtal26 --boot [CHEMIN] [N]");
    Console.WriteLine("        iXtal26 --timer-check [CHEMIN] [SECONDES]");
    Console.WriteLine();
    Console.WriteLine("Sans argument : ouvre une fenêtre et émule l'IBM PC 5150 jusqu'à sa fermeture.");
    Console.WriteLine();
    Console.WriteLine("  --rom-path CHEMIN    où chercher les images de ROM du 5150 (défaut : roms,");
    Console.WriteLine("                       cherché d'abord depuis le répertoire courant, puis en");
    Console.WriteLine("                       remontant depuis l'emplacement du binaire)");
    Console.WriteLine("  --slices N           s'arrête au bout de N tranches de 10 ms émulées, et");
    Console.WriteLine("                       n'attend pas l'horloge murale entre elles : deux");
    Console.WriteLine("                       exécutions traversent alors les mêmes états");
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
    Console.WriteLine("      --type TEXTE     après --boot : tape TEXTE dans la machine, puis");
    Console.WriteLine("                       revide l'écran. C'est la seule vérification du");
    Console.WriteLine("                       chemin clavier qui ne dépende pas d'une fenêtre");
    Console.WriteLine("                       ayant le focus");
    Console.WriteLine("  --timer-check [CHEMIN] [SECONDES]");
    Console.WriteLine("                       amorce, vérifie que l'INT 8 du BIOS tourne, puis");
    Console.WriteLine("                       compte les tops de la BDA (0040:006C) sur SECONDES");
    Console.WriteLine("                       secondes ÉMULÉES et compare à 1193182/65536 =");
    Console.WriteLine("                       18,2065 Hz (défauts : roms, 300 s)");
    Console.WriteLine("  -h, --help           affiche cette aide");
    Console.WriteLine();
    Console.WriteLine("Codes de sortie : 0 succès, 1 échec d'exécution, 2 erreur d'usage.");
}
