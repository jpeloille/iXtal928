// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// r9-gfxfixe — la vidéo intégrée de la M24 et du PC1512 (GR.4a ; PLAN-GR.md, décision n° 6), en C# SEUL.
//
// Chaque essai écrit un .cfg, le charge, juge la machine (check_cpu, comme le lanceur) puis la monte sans lancer son
// BIOS, en captant la sortie d'erreur. Sur la M24 et le PC1512 : sans clé et `builtin`, rien à dire ; `vga` et `ega`,
// l'avertissement « a sa vidéo intégrée », une seule fois sur les deux jugements ; un nom inconnu, refusé par
// loadconfig. De même quand la machine est posée après la clé (--model). Chaque machine montée porte sa vidéo
// (m24_device ou pc1512_device), gfxcard interne à la CGA. Sur le XT, `builtin` est refusé. --make-nvr refuse un
// gfxcard hors du registre, `builtin` compris : NvrImage en tirait DisplayOwnRom en silence (R4-contre, A7).

using iXtal26.PluginApi;
using iXtal26.Video;

namespace iXtal26.Diff;

internal static class R9GfxFixe
{
    private const string Integree = "a sa vidéo intégrée";

    internal static int Run(string romsPath)
    {
        var bad = 0;
        var n = 0;
        foreach (var (machine, video) in new[] { ("olivetti_m24", vid_olivetti_m24.m24_device),
                                                 ("pc1512", vid_pc1512.pc1512_device) })
        {
            foreach (var (cle, avertis) in new[] { ("", 0), ("builtin", 0), ("vga", 1), ("ega", 1) })
                bad += Essai(romsPath, $"{machine}, gfxcard = « {cle} »", machine, cle, n++, true, true, avertis,
                             video);
            bad += Essai(romsPath, $"{machine}, gfxcard = « inconnue »", machine, "inconnue", n++, false, false, 0,
                         null);
        }
        bad += Essai(romsPath, "ibmxt, gfxcard = « builtin »", "ibmxt", "builtin", n++, true, false, 0, null);
        bad += Essai(romsPath, "ibmxt, gfxcard = « vga »", "ibmxt", "vga", n++, true, true, 0, null);
        // La machine posée après la clé, comme --model après --config.
        bad += Essai(romsPath, "ibmxt, gfxcard = « vga », puis olivetti_m24", "ibmxt", "vga", n++, true, true, 1,
                     vid_olivetti_m24.m24_device, "olivetti_m24");
        bad += Essai(romsPath, "olivetti_m24, gfxcard = « builtin », puis ibmxt", "olivetti_m24", "builtin", n++,
                     true, false, 0, null, "ibmxt");

        foreach (var (cle, rc) in new[] { ("vga", 0), ("cga", 0), ("builtin", 2), ("inconnue", 2) })
            bad += Nvr(romsPath, cle, rc, n++);

        Console.WriteLine(bad == 0
            ? "r9-gfxfixe : la M24 et le PC1512 gardent leur vidéo intégrée ; builtin ailleurs, refusé."
            : $"r9-gfxfixe : {bad} essai(s) en défaut.");
        return bad == 0 ? 0 : 1;
    }

    /// <summary>Un essai : loadconfig doit réussir ou non (<paramref name="charge"/>), la machine posée ensuite si
    /// <paramref name="puis"/> la nomme, check_cpu puis initpc de même (<paramref name="monte"/>) ; les
    /// avertissements « a sa vidéo intégrée » comptés ; la vidéo montée cherchée dans le registre des devices,
    /// gfxcard à la CGA. Rend 0 ou 1.</summary>
    private static int Essai(string romsPath, string nom, string machine, string cle, int n, bool charge, bool monte,
                             int avertis, device_t? video, string? puis = null)
    {
        var cfg = Path.Combine(Path.GetTempPath(), $"r9-gfxfixe-{Environment.ProcessId}-{n}.cfg");
        File.WriteAllText(cfg, $"model = {machine}\nmem_size = 640\nsndcard = none\n" +
                               (cle.Length > 0 ? $"gfxcard = {cle}\n" : ""));
        var err = Console.Error;
        var capte = new StringWriter();
        try
        {
            Console.SetError(capte);
            string? faute = null;
            var lu = "";
            var c = pc.loadconfig(cfg);
            if (c != charge)
                faute = $"loadconfig {(c ? "accepte" : "refuse")}, ATTENDU l'inverse";
            else if (c)
            {
                Floppy.fdd_c.discfns[0] = "";
                Floppy.fdd_c.discfns[1] = "";
                if (puis is not null && !pc.setmodel(puis))
                    throw new InvalidOperationException($"setmodel {puis}");
                var m = pc.check_cpu() && pc.initpc(romsPath);
                lu = m ? "monte" : "initpc refuse";
                if (m != monte)
                    faute = $"initpc {(m ? "monte" : "refuse")}, ATTENDU l'inverse";
                else if (m && video is not null)
                {
                    var trouve = false;
                    for (var d = 0; d < device.devices.Length; d++)
                        trouve |= device.devices[d] == video;
                    lu += $", {(trouve ? video.name : "SANS sa vidéo")}, gfxcard {pc.gfxcard}";
                    if (!trouve || pc.gfxcard != 0)
                        faute = "ATTENDU sa vidéo et gfxcard 0";
                }
            }
            else
                lu = "loadconfig refuse";
            Console.SetError(err);
            var sortie = capte.ToString().Split('\n');
            var lignes = sortie.Where(l => l.Contains(Integree)).ToList();
            foreach (var l in sortie.Where(l => l.Contains(Integree) || l.Contains("gfxcard =")))
                Console.WriteLine($"    {l.Trim()}");
            if (faute is null && lignes.Count != avertis)
                faute = $"{lignes.Count} avertissement(s), ATTENDU {avertis}";
            if (faute is null && (!charge || (charge && !monte)) && !sortie.Any(l => l.Contains("gfxcard =")))
                faute = "refusé sans le dire";
            Console.WriteLine($"  {nom} : {lu}{(faute is null ? "" : $" ; {faute}")}");
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

    /// <summary>--make-nvr sur l'IBM AT avec gfxcard = <paramref name="cle"/> : le code de retour attendu.</summary>
    private static int Nvr(string romsPath, string cle, int rc, int n)
    {
        var cfg = Path.Combine(Path.GetTempPath(), $"r9-gfxfixe-{Environment.ProcessId}-{n}.cfg");
        var nvr = Path.Combine(Path.GetTempPath(), $"r9-gfxfixe-{Environment.ProcessId}-{n}.nvr");
        File.WriteAllText(cfg, $"model = ibmat\nmem_size = 640\ngfxcard = {cle}\n");
        var err = Console.Error;
        var capte = new StringWriter();
        try
        {
            Console.SetError(capte);
            var r = Host.NvrImage.Make(cfg, nvr, romsPath, true);
            Console.SetError(err);
            Console.WriteLine($"  --make-nvr, ibmat, gfxcard = « {cle} » : retour {r}" +
                              (r == rc ? "" : $" ; ATTENDU {rc}"));
            return r == rc ? 0 : 1;
        }
        finally
        {
            Console.SetError(err);
            File.Delete(cfg);
            File.Delete(nvr);
        }
    }
}
