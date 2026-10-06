// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// r9-cga et r9-m24 — la survie à PB-09 (la CGA) et à PB-88 (la M24), en C# SEUL (R9, TRANSCRIPTION.md ; G13.0,
// PLAN-G13.md).
//
// Un invité qui pose R1 au-delà de 128 en mode texte 80 colonnes (3D8h bit 0) fait déborder le tampon de ligne de
// 256 octets de PCem, à la copie de fin de ligne et à la relecture : PCem écrit hors du tableau (et, au-delà de R1 =
// 134, dans son tas), le C# levait IndexOutOfRangeException et le processus se terminait. La CGA a désormais un
// tampon de 512 octets (la carte lit son tampon d'affichage) ; la M24 lit 0 et abandonne l'écriture au-delà de 256
// (PB-88, G1.1). Sur une machine neuve, amorcée mais pas lancée, l'outil programme le 6845 par ses ports pour un
// texte 80 colonnes, pose R1, remplit la mémoire vidéo et déroule des trames. La porte exige qu'aucune exception
// ne sorte, et que le scénario passe bien par le débordement : pour la CGA, des octets au-delà de 256 lus dans la
// mémoire vidéo ; pour la M24, la garde vid_olivetti_m24.c:415 atteinte. L'oracle n'y est pas conduit.

using iXtal26.Diag;
using iXtal26.PluginApi;
using iXtal26.Video;

namespace iXtal26.Diff;

internal static class R9Cga
{
    // Le texte 80 × 25 du BIOS du 5150 (R0 à R11), R1 remplacé par l'essai.
    private static readonly byte[] Crtc80 = { 0x71, 0x50, 0x5A, 0x0A, 0x1F, 0x06, 0x19, 0x1C, 0x02, 0x07, 0x06, 0x07 };

    internal static int RunCga(string romsPath)
    {
        if (!pc.setmodel("ibmpc") || !pc.setgfxcard("cga"))
            return 2;
        Floppy.fdd_c.discfns[0] = "";
        Floppy.fdd_c.discfns[1] = "";
        if (!pc.initpc(romsPath))
            return 2;
        cga_t? cga = null;
        for (var c = 0; c < device.devices.Length; c++)
            if (device.devices[c] == vid_cga.cga_device && device.device_priv[c] is cga_t t)
                cga = t;
        if (cga is null)
        {
            Console.WriteLine("pas de CGA après initpc");
            return 2;
        }

        var bad = 0;
        foreach (var r1 in new byte[] { 129, 200, 255 })
            foreach (var mode in new byte[] { 0x01, 0x29 })
            {
                var nom = $"PB-09, R1 = {r1}, 3D8h = {mode:X2}h";
                try
                {
                    Array.Clear(cga.charbuffer);
                    for (var i = 0; i < cga.vram.Length; i++)
                        cga.vram[i] = (byte)(0x41 + (i % 26));
                    for (var r = 0; r < Crtc80.Length; r++)
                    {
                        vid_cga.cga_out(0x3d4, (byte)r, cga);
                        vid_cga.cga_out(0x3d5, r == 1 ? r1 : Crtc80[r], cga);
                    }
                    vid_cga.cga_out(0x3d8, mode, cga);
                    for (var n = 0; n < 4000; n++)
                        vid_cga.cga_poll(cga);
                    var lus = 0;
                    for (var i = 256; i < cga.charbuffer.Length; i++)
                        if (cga.charbuffer[i] != 0)
                            lus++;
                    if (lus == 0)
                    {
                        bad++;
                        Console.WriteLine($"  {nom} : survit, mais AUCUN octet au-delà de 256 n'a été lu : le débordement n'est pas atteint");
                    }
                    else
                        Console.WriteLine($"  {nom} : survit, {lus} octets lus au-delà de 256");
                }
                catch (Exception e)
                {
                    bad++;
                    Console.WriteLine($"  {nom} : ARRÊT — {e.GetType().Name} : {e.Message.Trim()}");
                }
            }

        Console.WriteLine(bad == 0 ? "\nVert : iXtal26 survit à R1 au-delà de 128 en 80 colonnes, le tampon lu jusqu'au bout (PB-09, R9)."
                                   : $"\n{bad} essai(s) en échec : R9 n'est pas tenue.");
        return bad == 0 ? 0 : 1;
    }

    internal static int RunM24(string romsPath)
    {
        if (!pc.setmodel("olivetti_m24"))
            return 2;
        Floppy.fdd_c.discfns[0] = "";
        Floppy.fdd_c.discfns[1] = "";
        if (!pc.initpc(romsPath))
            return 2;
        if (vid_olivetti_m24.m24_pri is not { } m24)
        {
            Console.WriteLine("pas de vidéo M24 après initpc");
            return 2;
        }

        var bad = 0;
        foreach (var r1 in new byte[] { 129, 200, 255 })
        {
            var nom = $"PB-88, R1 = {r1}, 3D8h = 01h";
            try
            {
                R9.Raz();
                for (var r = 0; r < Crtc80.Length; r++)
                {
                    vid_olivetti_m24.m24_out(0x3d4, (byte)r, m24);
                    vid_olivetti_m24.m24_out(0x3d5, r == 1 ? r1 : Crtc80[r], m24);
                }
                vid_olivetti_m24.m24_out(0x3d8, 0x01, m24);
                for (var n = 0; n < 4000; n++)
                    vid_olivetti_m24.m24_poll(m24);
                var atteint = R9.Compte("vid_olivetti_m24.c:415");
                if (atteint == 0)
                {
                    bad++;
                    Console.WriteLine($"  {nom} : survit, mais la garde vid_olivetti_m24.c:415 n'est pas atteinte");
                }
                else
                    Console.WriteLine($"  {nom} : survit, garde vid_olivetti_m24.c:415 atteinte {atteint} fois");
            }
            catch (Exception e)
            {
                bad++;
                Console.WriteLine($"  {nom} : ARRÊT — {e.GetType().Name} : {e.Message.Trim()}");
            }
        }

        Console.WriteLine(bad == 0 ? "\nVert : iXtal26 survit à R1 au-delà de 128 sur la M24, la garde atteinte (PB-88, R9)."
                                   : $"\n{bad} essai(s) en échec : R9 n'est pas tenue.");
        return bad == 0 ? 0 : 1;
    }
}
