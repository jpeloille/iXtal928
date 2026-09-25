/* SPDX-FileCopyrightText: 2026 Julien Peloille
 * SPDX-License-Identifier: GPL-2.0-only
 *
 * ------------------------------------------------------------------------------
 * POURQUOI ON INCLUT vid_tvga.c ET PAS SEULEMENT SON OBJET
 *
 * La tvga_t est une struct PRIVÉE de vid_tvga.c (vid_tvga.c:16-34) : aucun en-tête
 * ne l'expose. Or ses champs — les deux banques 3D8/3D9, le mode old/new, les trois
 * registres de contrôle, l'état du RAMDAC TKD8001 — sont ce que la carte ajoute à la
 * VGA, et le diff d'instructions n'en voit que ce que le CPU relit. La sonde VGA
 * doit donc les lire, et la redéclarer à la main serait une copie qui dérive en
 * silence (le même argument que harness.c:267-270).
 *
 * Même remède que pour 808x.c et 386_dynarec.c : vid_tvga.c cesse de figurer dans
 * SRC du Makefile et se compile ICI.
 * ------------------------------------------------------------------------------
 */

#include <stdint.h>

#include "../../pcem-dev/src/video/vid_tvga.c"

/* Les onze champs de la carte, en fin de h_vga_probe, dans l'ordre de
 * Video.vid_svga.Probe() côté C#. On aiguille sur gfxcard AVANT de convertir
 * svga->p : sur une VGA, p pointe une vga_t, et la conversion lirait n'importe quoi.
 * Tout à zéro pour une carte qui n'est pas une Trident. */
void h_tvga_probe(svga_t *svga, uint64_t *out) {
        tvga_t *tvga;
        int f = 0;

        if (gfxcard != GFX_TVGA && gfxcard != GFX_TVGA9000B)
                return;
        tvga = (tvga_t *)svga->p;

        out[f++] = tvga->id;
        out[f++] = (uint64_t)(int64_t)tvga->oldmode;
        out[f++] = tvga->tvga_3d8;
        out[f++] = tvga->tvga_3d9;
        out[f++] = tvga->oldctrl1;
        out[f++] = tvga->oldctrl2;
        out[f++] = tvga->newctrl2;
        out[f++] = (uint64_t)(int64_t)tvga->vram_size;
        out[f++] = tvga->vram_mask;
        out[f++] = (uint64_t)(int64_t)tvga->ramdac.state;
        out[f++] = tvga->ramdac.ctrl;
}
