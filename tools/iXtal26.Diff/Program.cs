// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only

using iXtal26.Diff;

if (args.Length == 0 || args[0] is "-h" or "--help")
{
    Console.WriteLine("Usage : iXtal26.Diff <commande> [options]");
    Console.WriteLine();
    Console.WriteLine("  sst-probe [--vectors DIR] [--op XX ...] [--limit N] [--baseline FICHIER]");
    Console.WriteLine("      Sonde SingleStepTests : passe les vecteurs à l'oracle C et");
    Console.WriteLine("      rapporte le taux de réussite. Porte de M0 — décide si le");
    Console.WriteLine("      harnais xunit complet vaut d'être construit.");
    Console.WriteLine();
    Console.WriteLine("  abi");
    Console.WriteLine("      Vérifie le contrat binaire avec libixtal26oracle.so.");
    return args.Length == 0 ? 2 : 0;
}

switch (args[0])
{
    case "abi":
        Oracle.CheckAbi();
        Console.WriteLine($"ABI {Oracle.h_abi_version()} OK, h_state = {Oracle.h_state_size()} octets.");
        return 0;

    case "sst-probe":
    {
        var vectors = "vectors/sst/v2";
        var ops = new List<string>();
        var limit = 0;
        string? baseline = null;

        for (var i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--vectors" when i + 1 < args.Length: vectors = args[++i]; break;
                case "--op" when i + 1 < args.Length: ops.Add(args[++i]); break;
                case "--limit" when i + 1 < args.Length: limit = int.Parse(args[++i]); break;
                case "--baseline" when i + 1 < args.Length: baseline = args[++i]; break;
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

        return SstProbe.Run(vectors, ops.ToArray(), limit, baseline);
    }

    default:
        Console.Error.WriteLine($"Commande inconnue : {args[0]}");
        return 2;
}
