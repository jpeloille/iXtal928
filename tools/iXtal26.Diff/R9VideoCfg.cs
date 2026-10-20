// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// r9-videocfg — les cavaliers de l'EGA et de la CGA (G13.12a ; Q25, Q26, Q30, Q31), en C# SEUL, dans les deux modes.
//
// Sur une IBM XT montée sans BIOS lancé, chaque essai écrit sa section [EGA] ou [CGA], puis lit ce que la carte en
// fait. L'EGA : monitor_type suit la liste du mode (PB-294 : 8 et 11 refusés en mode PCem, acceptés en mode matériel ;
// 0 à 5, 12, 15 et 99 refusés partout, 0 à 5 avec l'avertissement d'une carte secondaire en mode matériel, Q30) ;
// io_base (P3) : 0x200 fait répondre la carte en 2C4h et plus en 3C4h, avec l'avertissement du BIOS (Q31) ; une autre
// valeur, refusée. La CGA : font (P3) = 1, la rangée du « A » rendue par cga_poll est celle du simple trait de la ROM
// (mda.rom, 1000h) ; sans clé, celle du double trait (1800h) ; font = 2, refusée. Le verdict d'un essai : la valeur
// lue, le nombre d'avertissements « hors de la liste », l'avertissement nommé attendu, la survie.
// GR.1f — memory de l'EGA (64, 128, 256 Ko ; une autre valeur refusée, 256 retenu) : la mémoire que la carte borne
// (vram_limit) ; display_type de la MDA et de l'Hercules (3 vert, 4 ambre, 5 blanc ; une autre refusée, blanc retenu) :
// la famille de la couleur de l'attribut 0Fh (cgapal[0Fh] après cgapal_rebuild), lue sur ses trois composantes (le
// blanc de PCem est un blanc papier, FFFDEDh : le vert et le bleu au-dessus de C0h).

using iXtal26.PluginApi;
using iXtal26.Video;

namespace iXtal26.Diff;

internal static class R9VideoCfg
{
    internal static int Run(string romsPath)
    {
        var bad = 0;
        var n = 0;
        var hw = materiel.pb_294;

        // L'EGA : la valeur des interrupteurs, puis où répond l'index du séquenceur (3C4h, ou 2C4h sous P3).
        var ega = new (string nom, string cles, string attendu, int avertis, string? nomme)[]
        {
            ("EGA sans clé", "", "SW 9, 3C4h 02, 2C4h FF", 0, null),
            ("EGA monitor_type = 7", "monitor_type = 7\n", "SW 7, 3C4h 02, 2C4h FF", 0, null),
            ("EGA monitor_type = 8 (PB-294)", "monitor_type = 8\n", $"SW {(hw ? 8 : 9)}, 3C4h 02, 2C4h FF", hw ? 0 : 1,
             null),
            ("EGA monitor_type = 91 (PB-294, 11)", "monitor_type = 91\n", $"SW {(hw ? 11 : 9)}, 3C4h 02, 2C4h FF",
             hw ? 0 : 1, null),
            ("EGA monitor_type = 3 (secondaire, Q30)", "monitor_type = 3\n", "SW 9, 3C4h 02, 2C4h FF", 1,
             hw ? Secondaire : null),
            ("EGA monitor_type = 48 (0, le tube vert ; Q30)", "monitor_type = 48\n", "SW 9, 3C4h 02, 2C4h FF", 1,
             hw ? Secondaire : null),
            ("EGA monitor_type = 12", "monitor_type = 12\n", "SW 9, 3C4h 02, 2C4h FF", 1, null),
            ("EGA monitor_type = 15", "monitor_type = 15\n", "SW 9, 3C4h 02, 2C4h FF", 1, null),
            ("EGA monitor_type = 99", "monitor_type = 99\n", "SW 9, 3C4h 02, 2C4h FF", 1, null),
            ("EGA io_base = 0x200 (P3, Q31)", "io_base = 0x200\n", "SW 9, 3C4h FF, 2C4h 02", 0, Bios2xx),
            ("EGA io_base = 0x300", "io_base = 768\n", "SW 9, 3C4h 02, 2C4h FF", 0, null),
            ("EGA io_base = 0x250", "io_base = 0x250\n", "SW 9, 3C4h 02, 2C4h FF", 1, null),
        };
        foreach (var (nom, cles, attendu, avertis, nomme) in ega)
            bad += Essai(romsPath, nom, $"gfxcard = ega\n\n[EGA]\n{cles}", avertis, nomme, n++, () =>
            {
                _ = vid_ega.Probe ?? throw new InvalidOperationException("pas d'EGA après initpc");
                var lu = $"SW {vid_ega.egaswitches}, 3C4h {Index(0x3c4)}, 2C4h {Index(0x2c4)}";
                return (lu, lu == attendu ? null : $"ATTENDU {attendu}");
            });

        // GR.1f — la mémoire de l'EGA.
        var memoire = new (string nom, string cles, uint attendu, int avertis)[]
        {
            ("EGA sans memory", "", 256 * 1024, 0),
            ("EGA memory = 64", "memory = 64\n", 64 * 1024, 0),
            ("EGA memory = 128", "memory = 128\n", 128 * 1024, 0),
            ("EGA memory = 256", "memory = 256\n", 256 * 1024, 0),
            ("EGA memory = 32", "memory = 32\n", 256 * 1024, 1),
            ("EGA memory = 512", "memory = 512\n", 256 * 1024, 1),
        };
        foreach (var (nom, cles, attendu, avertis) in memoire)
            bad += Essai(romsPath, nom, $"gfxcard = ega\n\n[EGA]\n{cles}", avertis, null, n++, () =>
            {
                var ega = vid_ega.Probe ?? throw new InvalidOperationException("pas d'EGA après initpc");
                var lu = $"vram_limit {ega.vram_limit}";
                return (lu, ega.vram_limit == attendu ? null : $"ATTENDU {attendu}");
            });

        // GR.1f — l'écran de la MDA et de l'Hercules.
        var ecrans = new (string cles, string attendu, int avertis)[]
        {
            ("", "blanc", 0), ("display_type = 3\n", "vert", 0), ("display_type = 4\n", "ambre", 0),
            ("display_type = 5\n", "blanc", 0), ("display_type = 2\n", "blanc", 1), ("display_type = 6\n", "blanc", 1),
        };
        foreach (var (carte, section) in new[] { ("mda", "MDA"), ("hercules", "Hercules") })
            foreach (var (cles, attendu, avertis) in ecrans)
                bad += Essai(romsPath, $"{section} {(cles.Length == 0 ? "sans display_type" : cles.Trim())}",
                             $"gfxcard = {carte}\n\n[{section}]\n{cles}", avertis, null, n++, () =>
                {
                    var c = video.cgapal[0xf];
                    int r = (int)((c >> 16) & 0xff), g = (int)((c >> 8) & 0xff), b = (int)(c & 0xff);
                    var famille = g >= 0xc0 && b >= 0xc0 ? "blanc" : g > r ? "vert" : r > g && g > b ? "ambre" : "?";
                    var lu = $"cgapal[0Fh] {c & 0xffffff:X6}, {famille}";
                    return (lu, famille == attendu ? null : $"ATTENDU {attendu}");
                });

        // La CGA : la rangée 1 du « A » (41h), lue dans la ROM (le simple trait en 1000h, le double en 1800h).
        var rom = File.ReadAllBytes(Path.Combine(romsPath, "mda.rom"));
        string Rom(int @base) => Convert.ToString(rom[@base + 0x41 * 8 + 1], 2).PadLeft(8, '0');
        var cga = new (string nom, string cles, int @base, int avertis)[]
        {
            ("CGA sans clé (double trait)", "", 0x1800, 0),
            ("CGA font = 0", "font = 0\n", 0x1800, 0),
            ("CGA font = 1 (simple trait, P3)", "font = 1\n", 0x1000, 0),
            ("CGA font = 2", "font = 2\n", 0x1800, 1),
        };
        foreach (var (nom, cles, @base, avertis) in cga)
            bad += Essai(romsPath, nom, $"gfxcard = cga\n\n[CGA]\n{cles}", avertis, null, n++, () =>
            {
                var lu = RangeeA();
                var attendu = Rom(@base);
                return ($"rangée 1 du A {lu}", lu == attendu ? null : $"ATTENDU {attendu}");
            });

        Console.WriteLine(bad == 0
            ? "r9-videocfg : les cavaliers de l'EGA et de la CGA, la mémoire de l'EGA et l'écran de la MDA et de " +
              "l'Hercules suivent leurs clés."
            : $"r9-videocfg : {bad} essai(s) en défaut.");
        return bad == 0 ? 0 : 1;
    }

    /// <summary>L'index du séquenceur écrit puis relu au port <paramref name="port"/> : 02 si la carte y répond, FF
    /// sinon (le bus).</summary>
    private static string Index(ushort port)
    {
        io.outb(port, 0x02);
        return $"{io.inb(port):X2}";
    }

    /// <summary>Une ligne de texte en 80 colonnes rendue par cga_poll, la rangée 1 de la cellule : un « A » (07h) puis
    /// une espace (07h), qui donne le fond ; chaque point du « A » lu 1 s'il diffère du fond.</summary>
    private static string RangeeA()
    {
        cga_t? cga = null;
        for (var c = 0; c < device.devices.Length; c++)
            if (device.devices[c] == vid_cga.cga_device && device.device_priv[c] is cga_t t)
                cga = t;
        if (cga is null)
            throw new InvalidOperationException("pas de CGA après initpc");
        vid_cga.cga_out(0x3d4, 1, cga);
        vid_cga.cga_out(0x3d5, 2, cga);
        vid_cga.cga_out(0x3d8, 0x09, cga);
        (cga.charbuffer[0], cga.charbuffer[1], cga.charbuffer[2], cga.charbuffer[3]) = (0x41, 0x07, 0x20, 0x07);
        (cga.linepos, cga.cgadispon, cga.sc, cga.displine, cga.ma, cga.con) = (0, 1, 1, 10, 0, 0);
        vid_cga.cga_poll(cga);
        var l = 10 * video.Stride;
        var fond = video.Buffer32[l + 8 + 8];
        var r = "";
        for (var c = 0; c < 8; c++)
            r += video.Buffer32[l + 8 + c] != fond ? '1' : '0';
        return r;
    }

    /// <summary>Un essai : écrit le .cfg (une IBM XT), monte la machine en captant la sortie d'erreur, compte les
    /// avertissements « hors de la liste » et cherche l'avertissement nommé, puis `verif`. Rend 0 ou 1.</summary>
    private static int Essai(string romsPath, string nom, string corps, int avertis, string? nomme, int n,
                             Func<(string lu, string? faute)> verif)
    {
        var cfg = Path.Combine(Path.GetTempPath(), $"r9-videocfg-{Environment.ProcessId}-{n}.cfg");
        File.WriteAllText(cfg, "model = ibmxt\nmem_size = 640\nsndcard = none\n" + corps);
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
            var lignes = sortie.Where(l => l.Contains("hors de la liste")).ToList();
            foreach (var l in sortie.Where(l => l.Contains("hors de la liste") ||
                                                (nomme is not null && l.Contains(nomme))))
                Console.WriteLine($"    {l.Trim()}");
            if (faute is null && lignes.Count != avertis)
                faute = $"{lignes.Count} avertissement(s), ATTENDU {avertis}";
            if (faute is null && nomme is not null && !sortie.Any(l => l.Contains(nomme)))
                faute = $"pas d'avertissement « {nomme} »";
            if (faute is null && nomme is null && sortie.Any(l => l.Contains(Secondaire) || l.Contains(Bios2xx)))
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

    private const string Secondaire = "carte secondaire";
    private const string Bios2xx = "io_base = 2XX";
}
