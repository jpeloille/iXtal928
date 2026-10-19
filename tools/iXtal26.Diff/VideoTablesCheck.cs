// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// video-tables-check (GR.0a) — LES POLICES ET LA PALETTE, des deux côtés, hors machine.
//
// Jusqu'à GR.0a, l'oracle avait loadfont et cgapal_rebuild en souches vides : son image était noire pour la CGA, la
// MDA, l'Hercules, la M24 et le PC1512, et aucune sonde ne pouvait la comparer. Les deux sont désormais recopiés
// verbatim de video.c dans le harnais (harness_stubs.c). Cette porte compare ce qu'ils rendent, avant que les sondes
// ne hachent l'image (GR.0b) : pour chaque machine du dépôt, dans l'ordre de la table, fontdat et fontdatm après
// loadbios (les tables gardent ce que les machines précédentes y ont laissé, comme chez PCem) ; puis cgapal pour les
// sept types d'écran et les deux contrastes.

using System.Runtime.InteropServices;
using iXtal26.Memory;
using iXtal26.Models;
using iXtal26.Video;

namespace iXtal26.Diff;

internal static class VideoTablesCheck
{
    [DllImport(Oracle.Lib)]
    private static extern int h_video_fonts(string romspath, int r, byte[] dat, byte[] datm);

    [DllImport(Oracle.Lib)]
    private static extern void h_cgapal(int display_type, int contrast, uint[] pal);

    internal static int Run(string romsPath)
    {
        Oracle.CheckAbi();
        PluginApi.paths.set_roms_paths(romsPath);

        var bad = 0;
        var n = 0;
        var dat = new byte[2048 * 8];
        var datm = new byte[2048 * 16];
        foreach (var m in model_c.models)
        {
            var o = h_video_fonts(romsPath, m.id, dat, datm);
            pc.romset = m.id;
            var c = mem_bios.loadbios();
            if (o != c)
            {
                Console.WriteLine($"  {m.internal_name,-13} loadbios : oracle {o}, C# {c}");
                bad++;
                continue;
            }
            var mc = new byte[2048 * 16];
            Buffer.BlockCopy(video.fontdatm, 0, mc, 0, mc.Length);
            var e = Cmp(m.internal_name, "fontdat", dat, video.fontdat) + Cmp(m.internal_name, "fontdatm", datm, mc);
            Console.WriteLine($"  {m.internal_name,-13} fontdat {Fnv(dat):x16}, fontdatm {Fnv(datm):x16} : " +
                              (e == 0 ? "identiques" : $"{e} octet(s) divergent(s)"));
            bad += e;
            n += dat.Length + datm.Length;
        }

        var pal = new uint[16];
        for (var dt = video.DISPLAY_RGB; dt <= video.DISPLAY_TRUECOLOUR; dt++)
        {
            for (var contrast = 0; contrast <= 1; contrast++)
            {
                h_cgapal(dt, contrast, pal);
                video.cgapal_rebuild(dt, contrast);
                var e = 0;
                for (var i = 0; i < 16; i++)
                {
                    if (pal[i] == video.cgapal[i])
                        continue;
                    if (e < 4)
                        Console.WriteLine($"  cgapal({dt}, {contrast})[{i:x}] : oracle {pal[i]:x6}, " +
                                          $"C# {video.cgapal[i]:x6}");
                    e++;
                }
                bad += e;
                n += 16;
            }
        }
        Console.WriteLine($"  cgapal : 7 types d'écran × 2 contrastes, {(bad == 0 ? "identiques" : "voir plus haut")}");

        Console.WriteLine(bad == 0
            ? $"\nVert : les polices de {model_c.models.Length} machines et les 14 palettes identiques ({n} entrées)."
            : $"\nROUGE : {bad} entrée(s) divergente(s).");
        return bad == 0 ? 0 : 1;
    }

    private static int Cmp(string machine, string nom, byte[] o, byte[] c)
    {
        var bad = 0;
        for (var i = 0; i < o.Length; i++)
        {
            if (o[i] == c[i])
                continue;
            if (bad < 4)
                Console.WriteLine($"  {machine} {nom}[{i}] : oracle {o[i]:x2}, C# {c[i]:x2}");
            bad++;
        }
        return bad;
    }

    private static ulong Fnv(byte[] b)
    {
        var h = 1469598103934665603UL;
        foreach (var x in b)
        {
            h ^= x;
            h *= 1099511628211UL;
        }
        return h;
    }
}
