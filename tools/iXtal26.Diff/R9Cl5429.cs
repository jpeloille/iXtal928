// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// r9-cl5429 — la survie à PB-81 et PB-82, en C# SEUL (R9, TRANSCRIPTION.md).
//
// Deux chemins de la GD5429 où PCem indexe sa VRAM au-delà du tableau (comportement indéfini
// en C, une exception en C#) : la lecture du motif du blitter, source en haut de la VRAM
// (PB-81), et les modes d'écriture 4 et 5 sans X8, adresse alignée sur 4 seulement (PB-82).
// L'oracle n'y est pas conduit ; on prouve ici qu'iXtal26 y survit, sur une ami486 + GD5429
// amorcée mais pas lancée — les registres sont posés par les ports, comme un pilote le ferait.
// Le verdict est « survit » ou une exception nommée.

using iXtal26.Video;

namespace iXtal26.Diff;

internal static class R9Cl5429
{
    internal static int Run(string romsPath)
    {
        if (!pc.setmodel("ami486") || !pc.setgfxcard("cl_gd5429"))
            return 2;
        Floppy.fdd_c.discfns[0] = "";
        Floppy.fdd_c.discfns[1] = "";
        if (!pc.initpc(romsPath))
            return 2;
        var svga = vid_svga.svga_get_pri();
        if (svga?.p is not gd5429_t gd5429)
        {
            Console.WriteLine("pas de GD5429 après initpc");
            return 2;
        }
        Console.WriteLine($"GD5429 : vram_max {svga.vram_max:X}, vram_mask {svga.vram_mask:X}\n");

        var bad = 0;

        void Gr(int idx, int val)
        {
            io.outb(0x3ce, (byte)idx);
            io.outb(0x3cf, (byte)val);
        }

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

        // PB-81 — BitBLT 64 × 8, source en motif (mode 0x40), en 8 puis 16 bpp (bit 4 du mode ;
        // la GD5429 n'a que ces deux profondeurs, vid_cl5429.c:1710-1713). La source pointe sur
        // les derniers octets de la VRAM : y_count << 3 (ou << 4) l'emmène au-delà.
        foreach (var (mode, prof) in new[] { (0x40, "8 bpp"), (0x50, "16 bpp") })
            Essai($"PB-81, motif {prof}, source {svga.vram_mask & ~7u:X}", () =>
            {
                var src = svga.vram_mask & ~7u;
                Gr(0x20, 63); Gr(0x21, 0);          // largeur - 1
                Gr(0x22, 7); Gr(0x23, 0);           // hauteur - 1
                Gr(0x24, 64); Gr(0x25, 0);          // pas destination
                Gr(0x26, 64); Gr(0x27, 0);          // pas source
                Gr(0x28, 0); Gr(0x29, 0); Gr(0x2a, 0);
                Gr(0x2c, (int)(src & 0xff)); Gr(0x2d, (int)((src >> 8) & 0xff)); Gr(0x2e, (int)(src >> 16));
                Gr(0x30, mode);
                Gr(0x32, 0x0d);                     // ROP : copie de la source
                Gr(0x31, 0x02);                     // départ
            });

        // PB-82 — GRB = 0x04 (modes étendus, sans X8 ni 16 bits) : l'adresse n'est que
        // décalée de 2 (vid_cl5429.c:804). Masque de plans plein, modes 4 puis 5, écriture
        // au dernier mot de la VRAM : addr + 4 à addr + 7 sortent du tableau.
        foreach (var wm in new[] { 4, 5 })
            Essai($"PB-82, mode d'écriture {wm}, adresse {(svga.vram_max - 4) >> 2:X} (<< 2)", () =>
            {
                io.outb(0x3c4, 2);
                io.outb(0x3c5, 0xff);
                Gr(0x0b, 0x04);
                Gr(0x05, wm);
                vid_cl5429.gd5429_write_linear((svga.vram_max - 4) >> 2, 0xff, gd5429);
                Gr(0x05, 0);
                Gr(0x0b, 0);
            });

        Console.WriteLine(bad == 0 ? "\nVert : iXtal26 survit au motif du blitter et aux modes 4/5 en haut de VRAM (PB-81, PB-82, R9)."
                                   : $"\n{bad} arrêt(s) : R9 n'est pas tenue.");
        return bad == 0 ? 0 : 1;
    }
}
