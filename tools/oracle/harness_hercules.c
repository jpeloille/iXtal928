/* SPDX-FileCopyrightText: 2026 Julien Peloille
 * SPDX-License-Identifier: GPL-2.0-only
 *
 * G9.1 — L'HERCULES, INCLUSE ET NON LIÉE, pour la raison de harness_m24.c : la sonde de fin de
 * boot-diff lit la hercules_t (registres, VRAM, balayage). vid_hercules.c ne figure pas dans SRC et se
 * compile ICI. La police (fontdatm) n'est pas chargée par l'oracle : la sonde ne lit pas les
 * pixels.
 */

#include <stdint.h>
#include <string.h>

#include "../../pcem-dev/src/video/vid_hercules.c"

static hercules_t *h_hercules;
extern device_t *devices[];
extern void *device_priv[];

/* À chaque h_boot : une Hercules d'un amorçage précédent ne doit pas répondre à la sonde. */
void h_hercules_forget(void) { h_hercules = NULL; }

/* DÉVIATION DE L'ORACLE (PLAN-G9.md, décision n° 4) : hercules_init alloue la VRAM par
 * malloc sans l'effacer (vid_hercules.c:308) — du tas. Le C# a un tableau nul ; même arbitrage que
 * la VRAM de svga_init et de la M24. */
void h_hercules_attach(void) {
        int c;

        h_hercules = NULL;
        for (c = 0; c < DEV_MAX; c++)
                if (devices[c] == &hercules_device) {
                        h_hercules = (hercules_t *)device_priv[c];
                        memset(h_hercules->vram, 0, 0x10000);
                        return;
                }
        fatal("h_hercules_attach : hercules_device absent du registre\n");
}

static uint64_t h_hercules_fnv(uint64_t hash, const uint8_t *p, size_t n) {
        for (size_t i = 0; i < n; i++) {
                hash ^= p[i];
                hash *= 1099511628211ULL;
        }
        return hash;
}

/* Les dix-sept champs de l'Hercules (ctrl2 avec ctrl et stat), aux places de la M24 (offset 122 ; les deux ne se montent
 * jamais ensemble), dans l'ordre de Video.vid_svga.ProbeHercules() côté C#. Champ 0 = 4. */
void h_hercules_probe(uint64_t *out) {
        const uint64_t basis = 1469598103934665603ULL;
        int f = 122;

        if (!h_hercules)
                return;
        out[0] = 5;
        out[f++] = h_hercules_fnv(basis, h_hercules->crtc, sizeof(h_hercules->crtc));
        out[f++] = (uint64_t)(int64_t)h_hercules->crtcreg;
        out[f++] = h_hercules_fnv(basis, h_hercules->vram, 0x10000);
        out[f++] = h_hercules->ctrl | ((uint64_t)h_hercules->stat << 8) | ((uint64_t)h_hercules->ctrl2 << 16);
        out[f++] = (uint64_t)(int64_t)h_hercules->linepos;
        out[f++] = (uint64_t)(int64_t)h_hercules->displine;
        out[f++] = (uint64_t)(int64_t)h_hercules->sc;
        out[f++] = (uint64_t)(int64_t)h_hercules->vc;
        out[f++] = (uint32_t)h_hercules->con | ((uint64_t)(uint32_t)h_hercules->coff << 32);
        out[f++] = (uint32_t)h_hercules->cursoron | ((uint64_t)(uint32_t)h_hercules->blink << 32);
        out[f++] = (uint32_t)h_hercules->vsynctime | ((uint64_t)(uint32_t)h_hercules->vadj << 32);
        out[f++] = h_hercules->ma | ((uint64_t)h_hercules->maback << 16);
        out[f++] = (uint64_t)(int64_t)h_hercules->dispon;
        out[f++] = h_hercules->dispontime;
        out[f++] = h_hercules->dispofftime;
        out[f++] = h_hercules->timer.ts_integer | ((uint64_t)h_hercules->timer.ts_frac << 32);
        out[f++] = (uint32_t)h_hercules->firstline | ((uint64_t)(uint32_t)h_hercules->lastline << 32);
}
