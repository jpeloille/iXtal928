/* SPDX-FileCopyrightText: 2026 Julien Peloille
 * SPDX-License-Identifier: GPL-2.0-only
 *
 * G1.2 — LA VIDÉO DE L'AMSTRAD PC1512, INCLUSE ET NON LIÉE, pour la raison de harness_m24.c : la
 * pc1512_t est PRIVÉE de vid_pc1512.c, et la sonde de fin de boot-diff doit en lire les
 * registres et la VRAM. vid_pc1512.c cesse donc de figurer dans SRC et se compile ICI.
 */

#include <stdint.h>
#include <string.h>

#include "../../pcem-dev/src/video/vid_pc1512.c"

static pc1512_t *h_pc1512;
extern device_t *devices[];
extern void *device_priv[];

/* À chaque h_boot : un PC1512 d'un amorçage précédent ne répond pas à la sonde d'une autre
 * machine. */
void h_pc1512_forget(void) { h_pc1512 = NULL; }

/* DÉVIATION DE L'ORACLE (PLAN-G1.md, défaut n° 3) : pc1512_init alloue la VRAM par malloc sans
 * l'effacer (vid_pc1512.c:439) — du tas. Le C# a un tableau CLR, nul. */
void h_pc1512_attach(void) {
        int c;

        h_pc1512 = NULL;
        for (c = 0; c < DEV_MAX; c++)
                if (devices[c] == &pc1512_device) {
                        h_pc1512 = (pc1512_t *)device_priv[c];
                        memset(h_pc1512->vram, 0, 0x10000);
                        return;
                }
        fatal("h_pc1512_attach : pc1512_device absent du registre\n");
}

static uint64_t h_pc1512_fnv(uint64_t hash, const uint8_t *p, size_t n) {
        for (size_t i = 0; i < n; i++) {
                hash ^= p[i];
                hash *= 1099511628211ULL;
        }
        return hash;
}

/* Les vingt champs du PC1512, à partir de l'offset 143, dans l'ordre de Video.vid_svga.Probe()
 * côté C#. Le champ 0 de la sonde vaut alors 3. Pas de framebuffer (police non chargée). */
void h_pc1512_probe(uint64_t *out) {
        const uint64_t basis = 1469598103934665603ULL;
        pc1512_t *p = h_pc1512;
        int f = 143;

        if (!p)
                return;
        out[0] = 3;
        out[f++] = h_pc1512_fnv(basis, p->crtc, sizeof(p->crtc));
        out[f++] = (uint64_t)(int64_t)p->crtcreg;
        out[f++] = h_pc1512_fnv(basis, p->vram, 0x10000);
        out[f++] = p->cgacol | ((uint64_t)p->cgamode << 8) | ((uint64_t)p->stat << 16);
        out[f++] = p->plane_write | ((uint64_t)p->plane_read << 8) | ((uint64_t)p->border << 16);
        out[f++] = (uint64_t)(int64_t)p->fontbase;
        out[f++] = (uint64_t)(int64_t)p->linepos;
        out[f++] = (uint64_t)(int64_t)p->displine;
        out[f++] = (uint64_t)(int64_t)p->sc;
        out[f++] = (uint64_t)(int64_t)p->vc;
        out[f++] = (uint64_t)(int64_t)p->cgadispon;
        out[f++] = (uint32_t)p->con | ((uint64_t)(uint32_t)p->coff << 32);
        out[f++] = (uint32_t)p->cursoron | ((uint64_t)(uint32_t)p->cgablink << 32);
        out[f++] = (uint32_t)p->vsynctime | ((uint64_t)(uint32_t)p->vadj << 32);
        out[f++] = p->ma | ((uint64_t)p->maback << 16);
        out[f++] = (uint32_t)p->dispon | ((uint64_t)(uint32_t)p->blink << 32);
        out[f++] = p->dispontime;
        out[f++] = p->dispofftime;
        out[f++] = p->timer.ts_integer | ((uint64_t)p->timer.ts_frac << 32);
        out[f++] = (uint32_t)p->firstline | ((uint64_t)(uint32_t)p->lastline << 32);
}
