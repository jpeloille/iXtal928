// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// r9-sbcfg — les sections de device du .cfg (G8.3), en C# SEUL.
//
// Depuis G8.3, device_get_config_int lit la section du .cfg qui porte le nom du device, comme
// PCem (device.c:94-104), et rend le DÉFAUT, avec un avertissement, d'une valeur hors de la
// liste `selection` — l'interface de PCem n'offre que celles-là (device.cs, DEVIATION). Chez
// PCem, une valeur hors liste écrite à la main est prise telle quelle et peut indexer hors des
// tableaux (PB-93). Essais, sur une ami486 amorcée, chacun avec son verdict :
//   - la SB Pro v2 : clé inconnue et section d'un device absent (ignorées), IRQ 3 (→ 7, averti),
//     non numérique (→ défaut), DMA 9 / IRQ 99 et base FFFEh (PB-93 : → défauts, avertis),
//     opl_emu = 1 (NukedOPL, dans la liste mais omis : DBOPL) ; le DSP est ensuite conduit par
//     ses ports, un DMA 8 bits simple compris ;
//   - les trois cartes SVGA, clé memory hors liste (PB-93 : les douze cas, dont huit arrêtaient
//     l'hôte) : → défaut, averti, puis 400 tranches d'amorçage (le BIOS vidéo sonde sa VRAM).
// Le verdict d'un essai : les valeurs retenues, l'avertissement attendu (ou son absence), et la
// survie ; sinon l'exception nommée.

namespace iXtal26.Diff;

internal static class R9SbCfg
{
    private const string Sb = "Sound Blaster Pro v2";

    internal static int Run(string romsPath)
    {
        var bad = 0;
        var n = 0;
        var essais = new (string nom, string section, string attendu, int avertis)[]
        {
            ("clé inconnue, device inconnu", $"[{Sb}]\nirq = 5\nfoo = 1\n[Carte inconnue]\nirq = 3\n", "220h, IRQ 5, DMA 1", 0),
            ("dans la liste : 240h, IRQ 10, DMA 3", $"[{Sb}]\naddr = 576\nirq = 10\ndma = 3\n", "240h, IRQ 10, DMA 3", 0),
            ("hors liste : IRQ 3", $"[{Sb}]\nirq = 3\n", "220h, IRQ 7, DMA 1", 1),
            ("non numérique, hexadécimal", $"[{Sb}]\nirq = cinq\ndma = 0x3\n", "220h, IRQ 7, DMA 3", 0),
            ("PB-93 : DMA 9, IRQ 99", $"[{Sb}]\nirq = 99\ndma = 9\n", "220h, IRQ 7, DMA 1", 2),
            ("PB-93 : base FFFEh", $"[{Sb}]\naddr = 65534\n", "220h, IRQ 7, DMA 1", 1),
            ("opl_emu = 1 (NukedOPL)", $"[{Sb}]\nopl_emu = 1\n", "220h, IRQ 7, DMA 1", 0),
        };
        foreach (var (nom, section, attendu, avertis) in essais)
            bad += Essai(romsPath, nom, "gfxcard = tvga9000b\nsndcard = sbprov2\n\n" + section, avertis, n++, () =>
            {
                var sb = Sound.sound_sb.sb_pri;
                if (sb is null)
                    return "pas de SB après initpc";
                var d = sb.dsp;
                var lu = $"{d.sb_addr:X}h, IRQ {d.sb_irqnum}, DMA {d.sb_8_dmanum}";
                Conduire(d);
                if (lu != attendu || sb.opl_emu != Sound.sound_opl.OPL_DBOPL)
                    return $"ATTENDU {attendu}, opl_emu 0 (lu opl_emu {sb.opl_emu})";
                return null;
            }, () => Sound.sound_sb.sb_pri is { } sb ? $"{sb.dsp.sb_addr:X}h, IRQ {sb.dsp.sb_irqnum}, DMA {sb.dsp.sb_8_dmanum}" : "-");

        // Les cartes SVGA : memory hors liste → le défaut (1024 Ko, 2 Mo, 2 Mo). La Trio64 lit la
        // clé deux fois (vid_s3.c:2881 et :3008) : deux avertissements.
        var videos = new (string gfx, string device, string[] valeurs, uint vram, int avertis)[]
        {
            ("tvga8900d", "Trident TVGA 8900D", ["0", "3", "4096", "-1"], 1024u << 10, 1),
            ("cl_gd5429", "Cirrus Logic GD5429", ["0", "3", "255", "-1"], 2u << 20, 1),
            ("px_trio64", "Phoenix S3 Trio64", ["0", "3", "64", "-1"], 2u << 20, 2),
        };
        foreach (var (gfx, device, valeurs, vram, avertis) in videos)
            foreach (var v in valeurs)
                bad += Essai(romsPath, $"{gfx}, memory = {v}", $"gfxcard = {gfx}\n\n[{device}]\nmemory = {v}\n", avertis, n++, () =>
                {
                    for (var i = 0; i < 400; i++)
                        pc.runpc();
                    var svga = Video.vid_svga.svga_get_pri();
                    return svga is not null && svga.vram_mask == vram - 1 ? null
                        : $"ATTENDU vram_mask {vram - 1:X}";
                }, () => $"vram_mask {Video.vid_svga.svga_get_pri()?.vram_mask:X}");

        Console.WriteLine(bad == 0 ? "r9-sbcfg : tout survit, hors liste → défaut averti." : $"r9-sbcfg : {bad} essai(s) en défaut.");
        return bad == 0 ? 0 : 1;
    }

    /// <summary>Un essai : écrit le .cfg, amorce en captant la sortie d'erreur, compte les
    /// avertissements « hors de la liste », puis `verif` (null : conforme). Rend 0 ou 1.</summary>
    private static int Essai(string romsPath, string nom, string corps, int avertis, int n,
                             Func<string?> verif, Func<string> valeurs)
    {
        var cfg = Path.Combine(Path.GetTempPath(), $"r9-sbcfg-{Environment.ProcessId}-{n}.cfg");
        File.WriteAllText(cfg, "model = ami486\ncpu = 10\nmem_size = 4096\n" + corps);
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
            var faute = verif();
            Console.SetError(err);
            var lignes = capte.ToString().Split('\n').Where(l => l.Contains("hors de la liste")).ToList();
            foreach (var l in lignes)
                Console.WriteLine($"    {l.Trim()}");
            if (faute is null && lignes.Count != avertis)
                faute = $"{lignes.Count} avertissement(s), ATTENDU {avertis}";
            Console.WriteLine($"  {nom} : survit — {valeurs()}{(faute is null ? "" : $" ; {faute}")}");
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
            File.Delete(cfg);
        }
    }

    /// <summary>Reset du DSP, constante de temps, DMA 8 bits simple (14h) de 256 octets, sortie
    /// directe, puis 4 096 tours de pollsb : le DMA lit son canal, l'IRQ de fin tombe.</summary>
    private static void Conduire(Sound.sb_dsp_t d)
    {
        var a = d.sb_addr;
        void W(int v) => io.outb((ushort)(a + 0xc), (byte)v);
        io.outb((ushort)(a + 6), 1);
        io.outb((ushort)(a + 6), 0);
        _ = io.inb((ushort)(a + 0xa));
        W(0xd1);
        W(0x40); W(0xa6);
        W(0x14); W(0xff); W(0x00);
        for (var i = 0; i < 4096; i++)
            Sound.sound_sb_dsp.pollsb(d);
        W(0x10); W(0x80);
        _ = io.inb((ushort)(a + 0xe));
    }
}
