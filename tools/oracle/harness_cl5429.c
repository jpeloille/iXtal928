/* SPDX-FileCopyrightText: 2026 Julien Peloille
 * SPDX-License-Identifier: GPL-2.0-only
 *
 * G7.1 — LA GD5429 DE CIRRUS LOGIC, INCLUSE ET NON LIÉE, pour la même raison que
 * harness_tvga.c : la gd5429_t est PRIVÉE de vid_cl5429.c (vid_cl5429.c:36-87), et ce que
 * la carte ajoute à la VGA — ses deux banques, le DAC caché, la fenêtre linéaire, les
 * verrous étendus, le curseur matériel — ne se voit au diff d'instructions que si le CPU
 * le relit. vid_cl5429.c cesse donc de figurer dans SRC et se compile ICI.
 */

#include <stdint.h>

#include "../../pcem-dev/src/video/vid_cl5429.c"

/* Les seize champs de la carte, après ceux de la Trident, dans l'ordre de
 * Video.vid_svga.Probe() côté C#. Aiguillage sur gfxcard AVANT de convertir svga->p.
 * Tout à zéro pour une carte qui n'est pas une GD5429. */
void h_cl5429_probe(svga_t *svga, uint64_t *out) {
        gd5429_t *gd5429;
        int f = 0;

        if (gfxcard != GFX_CL_GD5429)
                return;
        gd5429 = (gd5429_t *)svga->p;

        out[f++] = (uint64_t)(int64_t)gd5429->type;
        out[f++] = gd5429->bank[0];
        out[f++] = gd5429->bank[1];
        out[f++] = gd5429->mask;
        out[f++] = gd5429->vram_mask;
        out[f++] = gd5429->hidden_dac_reg;
        out[f++] = (uint64_t)(int64_t)gd5429->dac_3c6_count;
        out[f++] = gd5429->lfb_base;
        out[f++] = (uint64_t)(int64_t)gd5429->mmio_vram_overlap;
        out[f++] = gd5429->sr10_read;
        out[f++] = gd5429->sr11_read;
        out[f++] = (uint64_t)gd5429->latch_ext[0] | ((uint64_t)gd5429->latch_ext[1] << 8) |
                   ((uint64_t)gd5429->latch_ext[2] << 16) | ((uint64_t)gd5429->latch_ext[3] << 24);
        out[f++] = (uint64_t)(int64_t)svga->hwcursor.ena;
        out[f++] = (uint64_t)(int64_t)svga->hwcursor.x;
        out[f++] = (uint64_t)(int64_t)svga->hwcursor.y;
        out[f++] = svga->hwcursor.addr;
}
