// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// r9-hdccfg — les cavaliers des contrôleurs de disque (G13.12b ; Q25, Q26, Q31, Q33), en C# SEUL, dans les deux modes.
//
// Chaque essai écrit son .cfg, monte la machine sans lancer le BIOS, puis lit ce que la carte en fait. Le Xebec
// (l'IBM XT) : 322h, les interrupteurs, déduits de l'image sans clé ; drive0_type et drive1_type les posent (les types
// 1, 2, 13 et 16), un type que l'image n'a pas averti, une autre valeur refusée. L'adaptateur de l'AT (l'IBM AT) :
// addr = 0x170 fait répondre la carte en 172h et plus en 1F2h, avec l'avertissement du BIOS (Q31). L'IDE (l'IBM AT) :
// primary ou secondary à 0 coupe son canal, averti s'il porte un disque ou le CD (Q33) ; sous l'XTIDE, la section
// [Standard IDE] ne compte pas. Une carte répond si le compte de secteurs écrit (5Ah) se relit ; FF, le bus. Le
// verdict d'un essai : la valeur lue, le nombre d'avertissements « hors de la liste », l'avertissement nommé attendu,
// la survie.

namespace iXtal26.Diff;

internal static class R9HdcCfg
{
    internal static int Run(string romsPath)
    {
        var tmp = Path.Combine(Path.GetTempPath(), $"r9-hdccfg-{Environment.ProcessId}");
        Directory.CreateDirectory(tmp);
        var bad = 0;
        var n = 0;
        try
        {
            string Disque(string lettre, int cyl, int tetes)
            {
                var f = Path.Combine(tmp, $"{lettre}-{cyl}-{tetes}.img");
                using (var s = File.Create(f))
                    s.SetLength((long)cyl * tetes * 17 * 512);
                return $"hd{lettre}_cylinders = {cyl}\nhd{lettre}_heads = {tetes}\nhd{lettre}_sectors = 17\n" +
                       $"hd{lettre}_fn = {f}\n";
            }

            // Le Xebec : 322h, l'unité 0 en bits 3-2, l'unité 1 en bits 1-0 (le rang de FD_TBL : 1, 16, 2, 13).
            var xt = "model = ibmxt\n";
            var c306 = Disque("c", 306, 4);
            var c612 = Disque("c", 612, 4);
            var d306 = Disque("d", 306, 4);
            var xebec = new (string nom, string corps, string attendu, int avertis, string? nomme)[]
            {
                ("Xebec sans clé, C: 306/4 (type 1)", c306, "322h 00", 0, null),
                ("Xebec sans clé, C: 612/4 (type 16)", c612, "322h 04", 0, null),
                ("Xebec drive0_type = 1, C: 306/4", c306 + Section("drive0_type = 1"), "322h 00", 0, null),
                ("Xebec drive0_type = 16, C: 612/4", c612 + Section("drive0_type = 16"), "322h 04", 0, null),
                ("Xebec drive0_type = 1, C: 612/4", c612 + Section("drive0_type = 1"), "322h 00", 0, TypeImage),
                ("Xebec drive0_type = 16, C: 306/4", c306 + Section("drive0_type = 16"), "322h 04", 0, TypeImage),
                ("Xebec drive0_type = 2, C: 306/4", c306 + Section("drive0_type = 2"), "322h 08", 0, TypeImage),
                ("Xebec drive0_type = 13, C: 306/4", c306 + Section("drive0_type = 13"), "322h 0C", 0, TypeImage),
                ("Xebec drive1_type = 13, sans D:", c306 + Section("drive1_type = 13"), "322h 03", 0, null),
                ("Xebec drive1_type = 1, D: 306/4", c306 + d306 + Section("drive1_type = 1"), "322h 00", 0, null),
                ("Xebec drive1_type = 13, D: 306/4", c306 + d306 + Section("drive1_type = 13"), "322h 03", 0,
                 "l'image de D:"),
                ("Xebec drive1_type = 2 et drive0_type = 16, C: 612/4",
                 c612 + Section("drive0_type = 16\ndrive1_type = 2"), "322h 06", 0, null),
                ("Xebec drive0_type = 3 (refusé), C: 612/4", c612 + Section("drive0_type = 3"), "322h 04", 1, null),
            };
            foreach (var (nom, corps, attendu, avertis, nomme) in xebec)
                bad += Essai(romsPath, nom, xt + "hdd_controller = mfm_xebec\n" + corps, avertis, nomme, n++,
                             "IBM PC Fixed Disk Adapter", () =>
                {
                    var lu = $"322h {io.inb(0x322):X2}";
                    return (lu, lu == attendu ? null : $"ATTENDU {attendu}");
                });

            // L'adaptateur de l'AT et l'IDE : le compte de secteurs de chaque canal écrit puis relu. L'adaptateur, de
            // plus, sur chaque position : l'état (50h, prêt), le port de données lu par mot (0000h, le tampon vide),
            // puis le registre de contrôle écrit 04h (le reset tenu), l'état relu (80h, occupé).
            var at = "model = ibmat\nmem_size = 640\n";
            const string Rien = "1F7h FF, 1F0h FFFF, 3F6h←04 FF", Prim = "1F7h 50, 1F0h 0000, 3F6h←04 80";
            const string Sec = "177h 50, 170h 0000, 376h←04 80", SansSec = "177h FF, 170h FFFF, 376h←04 FF";
            var mfm = new (string nom, string cles, string attendu, int avertis, string? nomme)[]
            {
                ("AT sans clé", "", $"1F2h 5A, 172h FF ; {Prim} ; {SansSec}", 0, null),
                ("AT addr = 0x1F0", "addr = 496\n", $"1F2h 5A, 172h FF ; {Prim} ; {SansSec}", 0, null),
                ("AT addr = 0x170 (Q31)", "addr = 0x170\n", $"1F2h FF, 172h 5A ; {Rien} ; {Sec}", 0, Bios170),
                ("AT addr = 0x1E8 (refusé)", "addr = 0x1e8\n", $"1F2h 5A, 172h FF ; {Prim} ; {SansSec}", 1, null),
            };
            foreach (var (nom, cles, attendu, avertis, nomme) in mfm)
                bad += Essai(romsPath, nom, at + $"hdd_controller = mfm_at\n\n[IBM PC AT Fixed Disk Adapter]\n{cles}",
                             avertis, nomme, n++, "IBM PC AT Fixed Disk Adapter", () =>
                             {
                                 var lu = $"{Canal(0x1f2)} ; {Tache(0x1f0, 0x3f6)} ; {Tache(0x170, 0x376)}";
                                 return (lu, lu == attendu ? null : $"ATTENDU {attendu}");
                             });

            var e306 = Disque("e", 306, 4);
            var ide = new (string nom, string corps, string attendu, int avertis, string? nomme)[]
            {
                ("IDE sans clé", "", "1F2h 5A, 172h 5A", 0, null),
                ("IDE primary = 0", Section("primary = 0", "Standard IDE"), "1F2h FF, 172h 5A", 0, null),
                ("IDE secondary = 0", Section("secondary = 0", "Standard IDE"), "1F2h 5A, 172h FF", 0, null),
                ("IDE primary = 0, C: (Q33)", c306 + Section("primary = 0", "Standard IDE"), "1F2h FF, 172h 5A", 0,
                 Coupe),
                ("IDE secondary = 0, E: (Q33)", e306 + Section("secondary = 0", "Standard IDE"), "1F2h 5A, 172h FF",
                 0, Coupe),
                ("IDE secondary = 0, le CD (Q33)", "cdrom_channel = 2\n" + Section("secondary = 0", "Standard IDE"),
                 "1F2h 5A, 172h FF", 0, "le lecteur de CD-ROM, sur l'unité 2"),
                ("IDE primary = 0, D: (Q33)", d306 + Section("primary = 0", "Standard IDE"), "1F2h FF, 172h 5A", 0,
                 "sur l'unité 1"),
                ("IDE secondary = 0, le ZIP (Q33)", "zip_channel = 3\n" + Section("secondary = 0", "Standard IDE"),
                 "1F2h 5A, 172h FF", 0, "le lecteur ZIP, sur l'unité 3"),
                ("IDE primary = 0 et secondary = 0", Section("primary = 0\nsecondary = 0", "Standard IDE"),
                 "1F2h FF, 172h FF", 0, null),
                ("IDE primary = 1, C:", c306 + Section("primary = 1", "Standard IDE"), "1F2h 5A, 172h 5A", 0, null),
                ("IDE secondary = 2 (refusé)", Section("secondary = 2", "Standard IDE"), "1F2h 5A, 172h 5A", 1, null),
            };
            foreach (var (nom, corps, attendu, avertis, nomme) in ide)
                bad += Essai(romsPath, nom, at + "hdd_controller = ide\n" + corps, avertis, nomme, n++, "Standard IDE",
                             () => Canaux(attendu));

            // L'XTIDE monte l'IDE de l'intérieur : la section [Standard IDE] ne compte pas ; 302h répond, 1F2h et 172h
            // non.
            bad += Essai(romsPath, "XTIDE, [Standard IDE] primary = 0, C:",
                         xt + "hdd_controller = xtide\n" + c306 + Section("primary = 0", "Standard IDE"), 0, null, n++,
                         "Standard IDE", () =>
                         {
                             io.outb(0x302, 0x5a);
                             var lu = $"302h {io.inb(0x302):X2}, {Canal(0x1f2)}";
                             const string attendu = "302h 5A, 1F2h FF, 172h FF";
                             return (lu, lu == attendu ? null : $"ATTENDU {attendu}");
                         });
        }
        finally
        {
            Directory.Delete(tmp, true);
        }

        Console.WriteLine(bad == 0 ? "r9-hdccfg : les cavaliers des contrôleurs de disque suivent leurs clés."
                                   : $"r9-hdccfg : {bad} essai(s) en défaut.");
        return bad == 0 ? 0 : 1;
    }

    private static string Section(string cles, string nom = "IBM PC Fixed Disk Adapter") => $"\n[{nom}]\n{cles}\n";

    /// <summary>Le compte de secteurs écrit (5Ah) puis relu au port <paramref name="port"/>, et de même à
    /// 172h.</summary>
    private static string Canal(ushort port)
    {
        io.outb(port, 0x5a);
        var a = io.inb(port);
        io.outb(0x172, 0x5a);
        return $"{port:X3}h {a:X2}, 172h {io.inb(0x172):X2}";
    }

    /// <summary>Le task file en <paramref name="b"/> : l'état, le port de données par mot, puis 04h écrit au registre
    /// de contrôle <paramref name="ctl"/> et l'état relu.</summary>
    private static string Tache(ushort b, ushort ctl)
    {
        var s1 = io.inb((ushort)(b + 7));
        var w = io.inw(b);
        io.outb(ctl, 0x04);
        return $"{b + 7:X3}h {s1:X2}, {b:X3}h {w:X4}, {ctl:X3}h←04 {io.inb((ushort)(b + 7)):X2}";
    }

    private static (string lu, string? faute) Canaux(string attendu)
    {
        var lu = Canal(0x1f2);
        return (lu, lu == attendu ? null : $"ATTENDU {attendu}");
    }

    /// <summary>Un essai : écrit le .cfg, monte la machine en captant la sortie d'erreur, compte les avertissements
    /// « hors de la liste » de la section <paramref name="section"/> et cherche l'avertissement nommé, puis `verif`.
    /// Rend 0 ou 1.</summary>
    private static int Essai(string romsPath, string nom, string corps, int avertis, string? nomme, int n,
                             string section, Func<(string lu, string? faute)> verif)
    {
        var cfg = Path.Combine(Path.GetTempPath(), $"r9-hdccfg-{Environment.ProcessId}-{n}.cfg");
        File.WriteAllText(cfg, "sndcard = none\n" + corps);
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
            var (lu, faute) = verif();
            Console.SetError(err);
            var sortie = capte.ToString().Split('\n');
            var lignes = sortie.Where(l => l.Contains($"[{section}]") && l.Contains("hors de la liste")).ToList();
            foreach (var l in sortie.Where(l => l.Contains("hors de la liste") ||
                                                (nomme is not null && l.Contains(nomme))))
                Console.WriteLine($"    {l.Trim()}");
            if (faute is null && lignes.Count != avertis)
                faute = $"{lignes.Count} avertissement(s), ATTENDU {avertis}";
            if (faute is null && nomme is not null && !sortie.Any(l => l.Contains(nomme)))
                faute = $"pas d'avertissement « {nomme} »";
            if (faute is null && nomme is null && sortie.Any(l => l.Contains(TypeImage) || l.Contains(Bios170) ||
                                                                  l.Contains(Coupe)))
                faute = "un avertissement de cavalier inattendu";
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

    private const string TypeImage = "l'interrupteur dit le type";
    private const string Bios170 = "addr = 170h";
    private const string Coupe = "le canal est coupé";
}
