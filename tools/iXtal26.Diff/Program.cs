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
    Console.WriteLine("  fuzz [--op XX ...] [--mode single|stream] [--iter N]");
    Console.WriteLine("       [--rounds N] [--instr N] [--seed N] [-v] [--ram-per-instr]");
    Console.WriteLine("      Diff différentiel : le cœur C# contre l'oracle C, état complet");
    Console.WriteLine("      comparé après chaque instruction. Par défaut 0xCE, le seul");
    Console.WriteLine("      opcode que 808x.c laisse tomber dans son `default:`.");
    Console.WriteLine();
    Console.WriteLine("  boot-diff [CHEMIN_ROMS] [TRANCHES]");
    Console.WriteLine("      Diff de traces d'amorçage. Phase 1 : hachage par instruction des");
    Console.WriteLine("      deux cœurs depuis le reset, pour situer la première divergence.");
    Console.WriteLine("      Phase 2 : rejeu en pas à pas jusque-là, vecteur d'état complet");
    Console.WriteLine("      plus une sonde des trois canaux du PIT.");
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

    case "speed-check":
    {
        var roms = args.Length > 1 ? args[1] : "roms";
        var n = args.Length > 2 ? int.Parse(args[2]) : 2000;
        return SpeedCheck.Run(roms, n);
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
        var roms = args.Length > 1 ? args[1] : "roms";
        var slices = args.Length > 2 ? int.Parse(args[2]) : 100;
        return BootDiff.Run(roms, slices);
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
            ? Fuzzer.RunSingle(ops.ToArray(), iterations, seed, verbose)
            : Fuzzer.Run(ops.ToArray(), rounds, instr, seed, verbose, ramPerInstr);
    }

    default:
        Console.Error.WriteLine($"Commande inconnue : {args[0]}");
        return 2;
}
