// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// r9-portscfg — les cartes de COM et de LPT (G13.13b ; Q25, Q26), en C# SEUL, dans les deux modes.
//
// Chaque essai écrit son .cfg, monte la machine sans lancer le BIOS, puis sonde les quatre ports : 5Ah écrit au
// registre de brouillon de COM1 (3FFh) et de COM2 (2FFh), AAh au registre de données de LPT1 (378h) et de LPT2 (278h),
// chacun relu ; FF, le bus. Sans clé, les quatre ports de PCem ; serial1, serial2, parallel1 et parallel2 à 0 retirent
// chacun sa carte, une autre valeur est refusée (1 à la place), une valeur illisible rend le défaut. Avertis : COM1
// retiré sous la souris série, LPT1 retiré sous un périphérique de LPT1 ; sur le PC1512, COM1 et LPT1, sur la carte
// mère, restent. Le LPT2 du PC1512 suit PB-101 : là en mode PCem, absent en mode matériel. Le verdict d'un essai : les
// valeurs lues, le nombre de refus, l'avertissement nommé attendu, la survie.

namespace iXtal26.Diff;

internal static class R9PortsCfg
{
    internal static int Run(string romsPath)
    {
        var bad = 0;
        var n = 0;
        // Le LPT2 du PC1512 : PB-101, corrigé en mode matériel.
        var lpt2Pc1512 = materiel.pb_101 ? "FF" : "AA";
        var essais = new (string nom, string corps, string attendu, int refus, string? nomme)[]
        {
            ("XT sans clé", "model = ibmxt\n", "5A 5A AA AA", 0, null),
            ("XT, les quatre à 1", "model = ibmxt\nserial1 = 1\nserial2 = 1\nparallel1 = 1\nparallel2 = 1\n",
             "5A 5A AA AA", 0, null),
            ("XT serial1 = 0, la souris série", "model = ibmxt\nserial1 = 0\n", "FF 5A AA AA", 0, Souris),
            ("XT serial2 = 0", "model = ibmxt\nserial2 = 0\n", "5A FF AA AA", 0, null),
            ("XT parallel1 = 0", "model = ibmxt\nparallel1 = 0\n", "5A 5A FF AA", 0, null),
            ("XT parallel2 = 0", "model = ibmxt\nparallel2 = 0\n", "5A 5A AA FF", 0, null),
            ("XT, les quatre à 0", "model = ibmxt\nserial1 = 0\nserial2 = 0\nparallel1 = 0\nparallel2 = 0\n",
             "FF FF FF FF", 0, Souris),
            ("XT parallel1 = 0, lpt1_device = dss", "model = ibmxt\nparallel1 = 0\nlpt1_device = dss\n",
             "5A 5A FF AA", 0, Peripherique),
            ("XT serial1 = 2 (refusé)", "model = ibmxt\nserial1 = 2\n", "5A 5A AA AA", 1, null),
            ("XT parallel2 = -1 (refusé)", "model = ibmxt\nparallel2 = -1\n", "5A 5A AA AA", 1, null),
            ("XT serial2 = abc (illisible, le défaut)", "model = ibmxt\nserial2 = abc\n", "5A 5A AA AA", 0, null),
            ("AT sans clé", "model = ibmat\n", "5A 5A AA AA", 0, null),
            ("AT serial2 = 0, parallel2 = 0", "model = ibmat\nserial2 = 0\nparallel2 = 0\n", "5A FF AA FF", 0,
             null),
            ("M24 serial1 = 0, la souris de la M24", "model = olivetti_m24\nmouse_type = 5\nserial1 = 0\n",
             "FF 5A AA AA", 0, null),
            ("PC1512 sans clé", "model = pc1512\nmouse_type = 4\n", $"5A 5A AA {lpt2Pc1512}", 0, null),
            ("PC1512 serial1 = 0 (la carte mère)", "model = pc1512\nmouse_type = 4\nserial1 = 0\n",
             $"5A 5A AA {lpt2Pc1512}", 0, CarteMere),
            ("PC1512 parallel1 = 0 (la carte mère)", "model = pc1512\nmouse_type = 4\nparallel1 = 0\n",
             $"5A 5A AA {lpt2Pc1512}", 0, CarteMere),
            ("PC1512 serial2 = 0", "model = pc1512\nmouse_type = 4\nserial2 = 0\n", $"5A FF AA {lpt2Pc1512}", 0,
             null),
            ("PC1512 parallel2 = 0", "model = pc1512\nmouse_type = 4\nparallel2 = 0\n", "5A 5A AA FF", 0, null),
        };
        foreach (var (nom, corps, attendu, refus, nomme) in essais)
            bad += Essai(romsPath, nom, corps, attendu, refus, nomme, n++);

        Console.WriteLine(bad == 0 ? "r9-portscfg : les cartes de COM et de LPT suivent leurs clés."
                                   : $"r9-portscfg : {bad} essai(s) en défaut.");
        return bad == 0 ? 0 : 1;
    }

    /// <summary>Les quatre ports sondés : 3FFh, 2FFh, 378h, 278h.</summary>
    private static string Ports()
    {
        string Sonde(ushort port, byte v)
        {
            io.outb(port, v);
            return $"{io.inb(port):X2}";
        }
        return $"{Sonde(0x3ff, 0x5a)} {Sonde(0x2ff, 0x5a)} {Sonde(0x378, 0xaa)} {Sonde(0x278, 0xaa)}";
    }

    /// <summary>Un essai : écrit le .cfg, monte la machine en captant la sortie d'erreur, sonde les ports, compte les
    /// refus « hors de la liste » des quatre clés et cherche l'avertissement nommé. Rend 0 ou 1.</summary>
    private static int Essai(string romsPath, string nom, string corps, string attendu, int refus, string? nomme,
                             int n)
    {
        var cfg = Path.Combine(Path.GetTempPath(), $"r9-portscfg-{Environment.ProcessId}-{n}.cfg");
        File.WriteAllText(cfg, "sndcard = none\nmem_size = 640\n" + corps);
        var err = Console.Error;
        var capte = new StringWriter();
        try
        {
            Console.SetError(capte);
            if (!pc.loadconfig(cfg))
                throw new InvalidOperationException("loadconfig");
            Floppy.fdd_c.discfns[0] = "";
            Floppy.fdd_c.discfns[1] = "";
            if (!pc.initpc(romsPath))
                throw new InvalidOperationException("initpc");
            var lu = Ports();
            Console.SetError(err);
            var sortie = capte.ToString().Split('\n');
            string[] cles = ["serial1 =", "serial2 =", "parallel1 =", "parallel2 ="];
            var lignes = sortie.Where(l => l.Contains("hors de la liste") && cles.Any(l.Contains)).ToList();
            foreach (var l in sortie.Where(l => (l.Contains("hors de la liste") && cles.Any(l.Contains)) ||
                                                l.Contains(Souris) || l.Contains(Peripherique) ||
                                                l.Contains(CarteMere)))
                Console.WriteLine($"    {l.Trim()}");
            string? faute = lu == attendu ? null : $"ATTENDU {attendu}";
            if (faute is null && lignes.Count != refus)
                faute = $"{lignes.Count} refus, ATTENDU {refus}";
            if (faute is null && nomme is not null && !sortie.Any(l => l.Contains(nomme)))
                faute = $"pas d'avertissement « {nomme} »";
            if (faute is null && sortie.Any(l => (l.Contains(Souris) || l.Contains(Peripherique) ||
                                                  l.Contains(CarteMere)) && (nomme is null || !l.Contains(nomme))))
                faute = "un avertissement de port inattendu";
            Console.WriteLine($"  {nom} : survit — {lu}{(faute is null ? "" : $" ; {faute}")}");
            return faute is null ? 0 : 1;
        }
        catch (Exception e)
        {
            Console.SetError(err);
            Console.WriteLine($"  {nom} : ARRÊT — {e.GetType().Name} : {e.Message.Trim()}");
            return 1;
        }
        finally
        {
            Console.SetError(err);
            try { pc.closepc(); } catch (Exception) { }
            File.Delete(cfg);
        }
    }

    private const string Souris = "COM1 retiré ; la souris série";
    private const string Peripherique = "ne recevra rien";
    private const string CarteMere = "sur sa carte mère";
}
