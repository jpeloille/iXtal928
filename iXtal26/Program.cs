using iXtal26;

// Point d'entrée : lit les arguments, puis laisse Game gérer la fenêtre et la boucle.
var maxFrames = 0; // 0 = tourne jusqu'à la fermeture de la fenêtre

for (var i = 0; i < args.Length; i++)
{
    var arg = args[i];

    if (arg is "-h" or "--help")
    {
        PrintUsage();
        return 0;
    }

    if (arg == "--gates")
        return M0Gates.Run();

    if (arg == "--frames")
    {
        if (i + 1 >= args.Length || !int.TryParse(args[++i], out maxFrames) || maxFrames < 0)
        {
            Console.Error.WriteLine("--frames attend un entier positif.");
            return 2;
        }

        continue;
    }

    Console.Error.WriteLine($"Argument inconnu : {arg}");
    PrintUsage();
    return 2;
}

using var game = new Game();

if (!game.Init())
    return 1;

return game.Run(maxFrames);

static void PrintUsage()
{
    Console.WriteLine("Usage : iXtal26 [--frames N]");
    Console.WriteLine();
    Console.WriteLine("  --frames N   affiche N images puis quitte (test automatisé, sans interaction)");
    Console.WriteLine("  --gates      exécute les portes M0 (G1 ref, G2 switch géant, G3 union)");
    Console.WriteLine("  -h, --help   affiche cette aide");
}