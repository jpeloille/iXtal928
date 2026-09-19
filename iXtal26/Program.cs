// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/wx-ui/wx-main.cc:17-35 (main) + wx-sdl2.c:444-479 (pc_main)
// STATUS: host

using iXtal26;
using iXtal26.Host;

// Point d'entrée : lit les arguments, puis laisse SdlHost tenir la fenêtre et la boucle.
var romsPath = "roms";
var headless = false;
var maxSlices = 0; // 0 = tourne jusqu'à la fermeture de la fenêtre
var verbose = false;

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

        return BootTest.Run(roms, slices);
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

using var host = new SdlHost(romsPath, headless, maxSlices, verbose);

if (!host.Init())
    return 1;

// Pas de try/catch : pc.fatal() lève, et une trace d'exception est précisément le
// signal que le cœur est fait pour émettre. L'étouffer ici le perdrait.
return host.Run();

static void PrintUsage()
{
    Console.WriteLine("Usage : iXtal26 [--rom-path CHEMIN] [--slices N] [--headless] [--verbose]");
    Console.WriteLine("        iXtal26 --boot [CHEMIN] [N]");
    Console.WriteLine();
    Console.WriteLine("Sans argument : ouvre une fenêtre et émule l'IBM PC 5150 jusqu'à sa fermeture.");
    Console.WriteLine();
    Console.WriteLine("  --rom-path CHEMIN    où chercher les images de ROM du 5150 (défaut : roms)");
    Console.WriteLine("  --slices N           s'arrête au bout de N tranches de 10 ms émulées, et");
    Console.WriteLine("                       n'attend pas l'horloge murale entre elles : deux");
    Console.WriteLine("                       exécutions traversent alors les mêmes états");
    Console.WriteLine("  --headless           n'initialise aucune vidéo SDL ; exige --slices");
    Console.WriteLine("  --verbose            en fin d'exécution, compte les blits émis par le CGA");
    Console.WriteLine("                       et ceux réellement téléversés dans la texture");
    Console.WriteLine("  --boot [CHEMIN] [N]  amorce et raconte en console ce que le POST a écrit");
    Console.WriteLine("                       en mémoire et à l'écran (défauts : roms, 20 tranches)");
    Console.WriteLine("  -h, --help           affiche cette aide");
    Console.WriteLine();
    Console.WriteLine("Codes de sortie : 0 succès, 1 échec d'exécution, 2 erreur d'usage.");
}
