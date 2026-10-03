/* SPDX-FileCopyrightText: 2026 Julien Peloille
 * SPDX-License-Identifier: GPL-2.0-only
 *
 * G9.0 — LA MDA, INCLUSE ET NON LIÉE, pour la raison de harness_m24.c : la sonde de fin de
 * boot-diff lit la mda_t (registres, VRAM, balayage). vid_mda.c ne figure pas dans SRC et se
 * compile ICI. La police (fontdatm) n'est pas chargée par l'oracle : la sonde ne lit pas les
 * pixels.
 */

#include <stdint.h>
#include <string.h>

#include "../../pcem-dev/src/video/vid_mda.c"

static mda_t *h_mda;
extern device_t *devices[];
extern void *device_priv[];

/* À chaque h_boot : une MDA d'un amorçage précédent ne doit pas répondre à la sonde. */
void h_mda_forget(void) { h_mda = NULL; }

/* DÉVIATION DE L'ORACLE (PLAN-G9.md, décision n° 4) : mda_standalone_init alloue la VRAM par
 * malloc sans l'effacer (vid_mda.c:239) — du tas. Le C# a un tableau nul ; même arbitrage que
 * la VRAM de svga_init et de la M24. */
void h_mda_attach(void) {
        int c;

        h_mda = NULL;
        for (c = 0; c < DEV_MAX; c++)
                if (devices[c] == &mda_device) {
                        h_mda = (mda_t *)device_priv[c];
                        memset(h_mda->vram, 0, 0x1000);
                        return;
                }
        fatal("h_mda_attach : mda_device absent du registre\n");
}

static uint64_t h_mda_fnv(uint64_t hash, const uint8_t *p, size_t n) {
        for (size_t i = 0; i < n; i++) {
                hash ^= p[i];
                hash *= 1099511628211ULL;
        }
        return hash;
}

/* Les dix-sept champs de la MDA, aux places de la M24 (offset 122 ; les deux ne se montent
 * jamais ensemble), dans l'ordre de Video.vid_svga.ProbeMda() côté C#. Champ 0 = 4. */
void h_mda_probe(uint64_t *out) {
        const uint64_t basis = 1469598103934665603ULL;
        int f = 122;

        if (!h_mda)
                return;
        out[0] = 4;
        out[f++] = h_mda_fnv(basis, h_mda->crtc, sizeof(h_mda->crtc));
        out[f++] = (uint64_t)(int64_t)h_mda->crtcreg;
        out[f++] = h_mda_fnv(basis, h_mda->vram, 0x1000);
        out[f++] = h_mda->ctrl | ((uint64_t)h_mda->stat << 8);
        out[f++] = (uint64_t)(int64_t)h_mda->linepos;
        out[f++] = (uint64_t)(int64_t)h_mda->displine;
        out[f++] = (uint64_t)(int64_t)h_mda->sc;
        out[f++] = (uint64_t)(int64_t)h_mda->vc;
        out[f++] = (uint32_t)h_mda->con | ((uint64_t)(uint32_t)h_mda->coff << 32);
        out[f++] = (uint32_t)h_mda->cursoron | ((uint64_t)(uint32_t)h_mda->blink << 32);
        out[f++] = (uint32_t)h_mda->vsynctime | ((uint64_t)(uint32_t)h_mda->vadj << 32);
        out[f++] = h_mda->ma | ((uint64_t)h_mda->maback << 16);
        out[f++] = (uint64_t)(int64_t)h_mda->dispon;
        out[f++] = h_mda->dispontime;
        out[f++] = h_mda->dispofftime;
        out[f++] = h_mda->timer.ts_integer | ((uint64_t)h_mda->timer.ts_frac << 32);
        out[f++] = (uint32_t)h_mda->firstline | ((uint64_t)(uint32_t)h_mda->lastline << 32);
}
