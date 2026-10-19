/* SPDX-FileCopyrightText: 2026 Julien Peloille
 * SPDX-License-Identifier: GPL-2.0-only
 *
 * GR.0b — LA SONDE DE LA CGA. La cga_t est publique (vid_cga.h) : vid_cga.c reste lié, rien n'est
 * inclus. Jusqu'ici la CGA n'avait pas de sonde (champ 0 = 0) : boot-diff ne comparait que les
 * instructions. Elle a désormais ses registres, sa VRAM, son balayage, et l'image (champ 3).
 */

#include <stdint.h>
#include <string.h>

#include "ibm.h"
#include "device.h"
#include "mem.h"
#include "timer.h"
#include "video.h"
#include "vid_cga.h"

static cga_t *h_cga;
extern device_t *devices[];
extern void *device_priv[];
uint64_t h_image_hash(void);
uint64_t h_cgapal_hash(void);

/* À chaque h_boot : une CGA d'un amorçage précédent ne doit pas répondre à la sonde. */
void h_cga_forget(void) { h_cga = NULL; }

/* DÉVIATION DE L'ORACLE, par analogie avec la décision n° 4 de PLAN-G9 : cga_standalone_init alloue
 * la VRAM par malloc sans l'effacer (vid_cga.c:427) — du tas. Le C# a un tableau nul ; même arbitrage
 * que la VRAM de la MDA, de la M24 et de svga_init. */
void h_cga_attach(void) {
        int c;

        h_cga = NULL;
        for (c = 0; c < DEV_MAX; c++)
                if (devices[c] == &cga_device) {
                        h_cga = (cga_t *)device_priv[c];
                        memset(h_cga->vram, 0, 0x4000);
                        return;
                }
        fatal("h_cga_attach : cga_device absent du registre\n");
}

static uint64_t h_cga_fnv(uint64_t hash, const uint8_t *p, size_t n) {
        for (size_t i = 0; i < n; i++) {
                hash ^= p[i];
                hash *= 1099511628211ULL;
        }
        return hash;
}

#define I(x) ((uint64_t)(uint32_t)(x))

/* Les vingt-deux champs de la CGA, aux places de la M24 et à la première du PC1512 (de 122 à 143 ; la CGA
 * ne se monte avec aucune des deux), dans l'ordre de Video.vid_svga.ProbeCga() côté C#, plus l'image (champ 3) et la
 * palette (champ 4). Champ 0 = 7. h_closepc ne ferme pas les cartes (PB-31) : la cga_t est encore
 * vivante quand la sonde la lit. */
void h_cga_probe(uint64_t *out) {
        const uint64_t basis = 1469598103934665603ULL;
        cga_t *g = h_cga;
        int f = 122;

        if (!g)
                return;
        out[0] = 7;
        out[3] = h_image_hash();
        out[4] = h_cgapal_hash();
        out[f++] = h_cga_fnv(basis, g->crtc, sizeof(g->crtc));
        out[f++] = I(g->crtcreg);
        out[f++] = h_cga_fnv(basis, g->vram, 0x4000);
        out[f++] = g->cgamode | ((uint64_t)g->cgacol << 8) | ((uint64_t)g->cgastat << 16);
        out[f++] = I(g->fontbase);
        out[f++] = I(g->linepos);
        out[f++] = I(g->displine);
        out[f++] = I(g->sc);
        out[f++] = I(g->vc);
        out[f++] = I(g->cgadispon);
        out[f++] = I(g->con) | (I(g->coff) << 32);
        out[f++] = I(g->cursoron) | (I(g->cgablink) << 32);
        out[f++] = I(g->vsynctime) | (I(g->vadj) << 32);
        out[f++] = g->ma | ((uint64_t)g->maback << 16);
        out[f++] = I(g->oddeven);
        out[f++] = g->dispontime;
        out[f++] = g->dispofftime;
        out[f++] = g->timer.ts_integer | ((uint64_t)g->timer.ts_frac << 32);
        out[f++] = I(g->firstline) | (I(g->lastline) << 32);
        out[f++] = I(g->drawcursor);
        out[f++] = h_cga_fnv(basis, g->charbuffer, sizeof(g->charbuffer));
        out[f++] = I(g->revision) | (I(g->composite) << 16) | (I(g->snow_enabled) << 32);
}
