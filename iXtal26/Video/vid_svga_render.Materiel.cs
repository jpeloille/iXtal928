// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — le code propre au mode matériel des rendus du socle SVGA (G13 ; R10 de TRANSCRIPTION.md).
// STATUS: materiel
//
// Les corrections des défauts de PCem dans vid_svga_render.c, appelées par les gardes de vid_svga_render.cs
// (`if (materiel.pb_nn)`), seules sur leur ligne. La source, le cas qui discrimine et la panne qui le rougit sont dans
// l'entrée PB de PCEM_BUGS.md. Les méthodes portent NoInlining (M2, G13.7c2).

using System.Runtime.CompilerServices;
using static iXtal26.Video.video;

namespace iXtal26.Video;

internal static partial class vid_svga_render
{
    /// <summary>PB-37 : le rendu 24 bpp basse résolution de PCem, la sortie avançant de huit points par groupe de quatre
    /// points de 24 bits, chacun doublé (le rendu haute résolution voisin écrit par `*p++` ; déduit, l'intention du
    /// code) ; sans remappage, quatre points par tour (`x += 4`), comme la branche remappée. `ma`, non masqué en sortie,
    /// reste celui de PCem (sans effet : il est masqué à chaque lecture).</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void rendu_24bpp_lowres_materiel(svga_t svga)
    {
        ModeMateriel.Sonde[37]++;
        uint32_t changed_addr = svga.remap_func!(svga, svga.ma);

        if (svga.changedvram[changed_addr >> 12] == 0 && svga.changedvram[(changed_addr >> 12) + 1] == 0 && svga.fullchange == 0)
                return;
        int offset = (8 - (svga.scrollcache & 6)) + 24;
        int p = svga.displine * Stride + offset;

        if (svga.firstline_draw == 2000)
                svga.firstline_draw = svga.displine;
        svga.lastline_draw = svga.displine;

        for (int x = 0; x <= svga.hdisp; x += 4)
        {
                uint32_t dat0, dat1, dat2;
                if (svga.remap_required == 0)
                {
                        dat0 = vram_l(svga, svga.ma & svga.vram_display_mask);
                        dat1 = vram_l(svga, (svga.ma + 4) & svga.vram_display_mask);
                        dat2 = vram_l(svga, (svga.ma + 8) & svga.vram_display_mask);
                }
                else
                {
                        dat0 = vram_l(svga, svga.remap_func(svga, svga.ma) & svga.vram_display_mask);
                        dat1 = vram_l(svga, svga.remap_func(svga, svga.ma + 4) & svga.vram_display_mask);
                        dat2 = vram_l(svga, svga.remap_func(svga, svga.ma + 8) & svga.vram_display_mask);
                }

                Buffer32[p + 0] = Buffer32[p + 1] = dat0 & 0xffffff;
                Buffer32[p + 2] = Buffer32[p + 3] = (dat0 >> 24) | ((dat1 & 0xffff) << 8);
                Buffer32[p + 4] = Buffer32[p + 5] = (dat1 >> 16) | ((dat2 & 0xff) << 16);
                Buffer32[p + 6] = Buffer32[p + 7] = dat2 >> 8;

                svga.ma += 12;
                p += 8;
        }
    }
}
