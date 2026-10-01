// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// r9-s3 — la survie à PB-84, PB-85 et PB-86 sur la Trio64, en C# SEUL (R9, TRANSCRIPTION.md).
//
// Trois chemins où PCem sort de ses tableaux ou divise INT_MIN par -1 sur l'ordre de l'invité :
// le curseur matériel lu à une adresse non masquée (PB-84) et écrit après la fin de buffer32 à
// la dernière ligne (PB-85), et la pente d'un polygone (PB-86). L'oracle n'y est pas conduit ;
// on prouve ici qu'iXtal26 y survit, sur une ami486 + Trio64 amorcée mais pas lancée. Le verdict
// est « survit » ou une exception nommée.

using iXtal26.Video;

namespace iXtal26.Diff;

internal static class R9S3
{
    internal static int Run(string romsPath)
    {
        if (!pc.setmodel("ami486") || !pc.setgfxcard("px_trio64"))
            return 2;
        Floppy.fdd_c.discfns[0] = "";
        Floppy.fdd_c.discfns[1] = "";
        if (!pc.initpc(romsPath))
            return 2;
        var svga = vid_svga.svga_get_pri();
        if (svga?.p is not s3_t s3)
        {
            Console.WriteLine("pas de Trio64 après initpc");
            return 2;
        }
        Console.WriteLine($"Trio64 : vram_mask {svga.vram_mask:X}\n");

        var bad = 0;

        void Essai(string nom, Action a)
        {
            try
            {
                a();
                Console.WriteLine($"  {nom} : survit");
            }
            catch (Exception e)
            {
                bad++;
                Console.WriteLine($"  {nom} : ARRÊT — {e.GetType().Name} : {e.Message.Trim()}");
            }
        }

        // PB-84 et PB-85 : un curseur au sommet de l'espace de 4 Mo (CR4C/4D), en x = 2040, tracé
        // sur la dernière ligne de buffer32 : la lecture sort de la VRAM, l'écriture du tableau.
        Essai("PB-84/85, curseur en 3FFFF0, x 2040, dernière ligne", () =>
        {
            svga.hwcursor_latch.addr = 0x3FFFF0;
            svga.hwcursor_latch.x = 2040;
            svga.hwcursor_latch.xoff = 0;
            vid_s3.s3_hwcursor_draw(svga, video.Height - 1);
        });

        // PB-86 : destx_distp = -2048, donc end_x = -2048 << 20 = INT_MIN, depuis x = 0, et une
        // hauteur de -1 : INT_MIN / -1. Les deux sommets du polygone.
        Essai("PB-86, pente INT_MIN / -1, sommets 1 et 2", () =>
        {
            s3.accel.poly_cx = 0;
            s3.accel.poly_cy = 5;
            s3.accel.destx_distp = -2048;
            s3.accel.desty_axstp = 4;
            s3.accel.point_1_updated = 1;
            s3.accel.poly_cx2 = 0;
            s3.accel.poly_cy2 = 5;
            s3.accel.x2 = 0xF800;
            s3.accel.desty_axstp2 = 4;
            s3.accel.point_2_updated = 1;
            vid_s3.polygon_setup(s3);
            Console.WriteLine($"    poly_dx1 {s3.accel.poly_dx1:X8}, poly_dx2 {s3.accel.poly_dx2:X8}");
        });

        Console.WriteLine(bad == 0 ? "\nVert : iXtal26 survit au curseur hors VRAM et hors buffer32, et à la pente INT_MIN / -1 (PB-84 à PB-86, R9)."
                                   : $"\n{bad} arrêt(s) : R9 n'est pas tenue.");
        return bad == 0 ? 0 : 1;
    }
}
