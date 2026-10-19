/* SPDX-FileCopyrightText: 2026 Julien Peloille
 * SPDX-License-Identifier: GPL-2.0-only
 *
 * GR.0b — L'ET4000AX, INCLUSE ET NON LIÉE, pour la raison de harness_tvga.c : l'et4000_t est
 * PRIVÉE de vid_et4000.c (:15-31), et la sonde VGA doit en lire la banque (3CDh) et l'état du
 * RAMDAC, que le diff d'instructions ne voit que si le CPU les relit. vid_et4000.c cesse de figurer
 * dans SRC et se compile ICI.
 */

#include <stdint.h>

#include "../../pcem-dev/src/video/vid_et4000.c"

extern int gfxcard;

/* Aux places de la Trident (aucune des deux ne se monte avec l'autre), dans l'ordre de
 * Video.vid_svga.Probe() côté C# : la banque à la place de tvga.id, l'état et la commande du RAMDAC
 * à celles du RAMDAC de la Trident. Rien pour une autre carte : svga->p n'est une et4000_t que sur
 * l'ET4000. */
void h_et4000_probe(svga_t *svga, uint64_t *out) {
        et4000_t *et4000;

        if (gfxcard != GFX_ET4000)
                return;
        et4000 = (et4000_t *)svga->p;
        out[0] = et4000->banking;
        out[9] = (uint64_t)(int64_t)et4000->ramdac.state;
        out[10] = et4000->ramdac.ctrl;
}
