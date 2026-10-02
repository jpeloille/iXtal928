/* SPDX-FileCopyrightText: 2026 Julien Peloille
 * SPDX-License-Identifier: GPL-2.0-only
 *
 * G1.1 — LA VIDÉO DE L'OLIVETTI M24, INCLUSE ET NON LIÉE, pour la raison de harness_tvga.c :
 * la m24_t est PRIVÉE de vid_olivetti_m24.c, et la sonde de fin de boot-diff doit en lire les
 * registres, la VRAM et la charbuffer. vid_olivetti_m24.c cesse donc de figurer dans SRC et
 * se compile ICI.
 */

#include <stdint.h>
#include <string.h>

#include "../../pcem-dev/src/video/vid_olivetti_m24.c"

/* Le m24_t que m24_init vient de créer, retrouvé dans le registre des devices (device.c:296-316). */
static m24_t *h_m24;
extern device_t *devices[];
extern void *device_priv[];

/* DÉVIATION DE L'ORACLE (PLAN-G1.md, défaut n° 3) : m24_init alloue la VRAM par malloc sans
 * l'effacer (vid_olivetti_m24.c:424) — du tas. Le C# a un tableau CLR, nul. Même arbitrage que
 * la VRAM de svga_init (harness.c). */
/* À chaque h_boot : une M24 d'un amorçage précédent ne doit pas répondre à la sonde d'une autre
 * machine (device_close_all l'a libérée). */
void h_m24_forget(void) { h_m24 = NULL; }

void h_m24_attach(void) {
        int c;

        h_m24 = NULL;
        for (c = 0; c < DEV_MAX; c++)
                if (devices[c] == &m24_device) {
                        h_m24 = (m24_t *)device_priv[c];
                        memset(h_m24->vram, 0, 0x8000);
                        return;
                }
        fatal("h_m24_attach : m24_device absent du registre\n");
}

/* FNV-1a 64 bits, le h_fnv de harness.c (static là-bas). */
static uint64_t h_m24_fnv(uint64_t hash, const uint8_t *p, size_t n) {
        for (size_t i = 0; i < n; i++) {
                hash ^= p[i];
                hash *= 1099511628211ULL;
        }
        return hash;
}

/* Les vingt et un champs de la M24, après ceux de la Trio64 (offset 122), dans l'ordre de
 * Video.vid_svga.Probe() côté C#. Le champ 0 de la sonde vaut alors 2 (1 : une svga). Pas de
 * framebuffer : la police (fontdatm) n'est pas chargée par l'oracle — pixels seulement. */
void h_m24_probe(uint64_t *out) {
        const uint64_t basis = 1469598103934665603ULL;
        int f = 122;

        if (!h_m24)
                return;
        out[0] = 2;
        out[f++] = h_m24_fnv(basis, h_m24->crtc, sizeof(h_m24->crtc));
        out[f++] = (uint64_t)(int64_t)h_m24->crtcreg;
        out[f++] = h_m24_fnv(basis, h_m24->vram, 0x8000);
        out[f++] = h_m24_fnv(basis, h_m24->charbuffer, sizeof(h_m24->charbuffer));
        out[f++] = h_m24->ctrl;
        out[f++] = h_m24->base;
        out[f++] = h_m24->cgamode | ((uint64_t)h_m24->cgacol << 8) | ((uint64_t)h_m24->stat << 16);
        out[f++] = (uint64_t)(int64_t)h_m24->linepos;
        out[f++] = (uint64_t)(int64_t)h_m24->displine;
        out[f++] = (uint64_t)(int64_t)h_m24->sc;
        out[f++] = (uint64_t)(int64_t)h_m24->vc;
        out[f++] = (uint32_t)h_m24->con | ((uint64_t)(uint32_t)h_m24->coff << 32);
        out[f++] = (uint32_t)h_m24->cursoron | ((uint64_t)(uint32_t)h_m24->blink << 32);
        out[f++] = (uint32_t)h_m24->vsynctime | ((uint64_t)(uint32_t)h_m24->vadj << 32);
        out[f++] = (uint64_t)(int64_t)h_m24->lineff;
        out[f++] = h_m24->ma | ((uint64_t)h_m24->maback << 16);
        out[f++] = (uint64_t)(int64_t)h_m24->dispon;
        out[f++] = h_m24->dispontime;
        out[f++] = h_m24->dispofftime;
        out[f++] = h_m24->timer.ts_integer | ((uint64_t)h_m24->timer.ts_frac << 32);
        out[f++] = (uint32_t)h_m24->firstline | ((uint64_t)(uint32_t)h_m24->lastline << 32);
}
