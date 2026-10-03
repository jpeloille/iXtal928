/* SPDX-FileCopyrightText: 2026 Julien Peloille
 * SPDX-License-Identifier: GPL-2.0-only
 *
 * G9.2 — L'EGA, INCLUSE ET NON LIÉE, pour la raison de harness_m24.c : la sonde de fin de
 * boot-diff lit la ega_t (registres, palette, VRAM, balayage). vid_ega.c ne figure pas dans SRC et
 * se compile ICI.
 *
 * DÉVIATION DE L'ORACLE, de sortie seulement : ega_recalctimings écrit trois lignes par printf
 * à CHAQUE écriture du CRTC (vid_ega.c:228-229, :238 ; pclog est déjà muet) — des milliers de
 * lignes sur la sortie standard du processus d'iXtal26.Diff, qui noieraient ses journaux. Le
 * temps de l'inclusion, printf ne fait rien ; le C# omet ces lignes de même (`// omitted:`).
 */

#include <stdint.h>
#include <stdio.h>
#include <string.h>

#define printf(...) ((void)0)
#include "../../pcem-dev/src/video/vid_ega.c"
#undef printf

static ega_t *h_ega;
extern device_t *devices[];
extern void *device_priv[];

/* À chaque h_boot : une EGA d'un amorçage précédent ne doit pas répondre à la sonde. */
void h_ega_forget(void) { h_ega = NULL; }

/* DÉVIATIONS DE L'ORACLE (PLAN-G9.md, décision n° 4) : ega_init alloue la VRAM par malloc sans
 * l'effacer (vid_ega.c:937) — du tas ; et egaswitchread (:19) n'est jamais remis à zéro : il
 * traverserait les amorçages du processus. Le C# a un tableau nul et remet egaswitchread à 0
 * dans ega_init ; ici, juste après le montage, avant toute instruction de l'invité. */
void h_ega_attach(void) {
        int c;

        h_ega = NULL;
        for (c = 0; c < DEV_MAX; c++)
                if (devices[c] == &ega_device) {
                        h_ega = (ega_t *)device_priv[c];
                        memset(h_ega->vram, 0, 0x40000);
                        egaswitchread = 0;
                        return;
                }
        fatal("h_ega_attach : ega_device absent du registre\n");
}

static uint64_t h_ega_fnv(uint64_t hash, const uint8_t *p, size_t n) {
        for (size_t i = 0; i < n; i++) {
                hash ^= p[i];
                hash *= 1099511628211ULL;
        }
        return hash;
}

#define I(x) ((uint64_t)(uint32_t)(x))

/* Les champs de l'EGA, aux places de la M24 et du PC1512 (offset 122 ; aucune des trois ne se
 * monte avec une autre), dans l'ordre de Video.vid_svga.ProbeEga() côté C#. Champ 0 = 6. */
void h_ega_probe(uint64_t *out) {
        const uint64_t basis = 1469598103934665603ULL;
        ega_t *e = h_ega;
        int f = 122;

        if (!e)
                return;
        out[0] = 6;
        out[f++] = h_ega_fnv(basis, e->vram, 0x40000);
        out[f++] = h_ega_fnv(basis, e->crtc, sizeof(e->crtc));
        out[f++] = h_ega_fnv(basis, e->gdcreg, sizeof(e->gdcreg));
        out[f++] = h_ega_fnv(basis, e->attrregs, sizeof(e->attrregs));
        out[f++] = h_ega_fnv(basis, e->seqregs, sizeof(e->seqregs));
        out[f++] = h_ega_fnv(basis, e->egapal, sizeof(e->egapal));
        out[f++] = e->crtcreg | (I(e->gdcaddr) << 8) | (I(e->attraddr) << 16) | (I(e->attrff) << 24) | (I(e->seqaddr) << 32);
        out[f++] = e->miscout | (I(e->vidclock) << 8) | ((uint64_t)e->stat << 16) | ((uint64_t)e->scrblank << 24) | (I(e->vres) << 32);
        out[f++] = e->la | ((uint64_t)e->lb << 8) | ((uint64_t)e->lc << 16) | ((uint64_t)e->ld << 24);
        out[f++] = e->colourcompare | ((uint64_t)e->colournocare << 8) | (I(e->readmode) << 16) | (I(e->writemode) << 24) |
                   (I(e->readplane) << 32) | (I(e->chain2_read) << 40) | (I(e->chain2_write) << 48) | ((uint64_t)e->writemask << 56);
        out[f++] = I(e->charseta) | (I(e->charsetb) << 32);
        out[f++] = I(e->vtotal) | (I(e->dispend) << 32);
        out[f++] = I(e->vsyncstart) | (I(e->split) << 32);
        out[f++] = I(e->hdisp) | (I(e->rowoffset) << 32);
        out[f++] = e->dispontime;
        out[f++] = e->dispofftime;
        out[f++] = e->timer.ts_integer | ((uint64_t)e->timer.ts_frac << 32);
        out[f++] = I(e->dispon);
        out[f++] = I(e->ma) | (I(e->maback) << 32);
        out[f++] = I(e->ca);
        out[f++] = I(e->vc) | (I(e->sc) << 32);
        out[f++] = I(e->linepos) | (I(e->vslines) << 32);
        out[f++] = I(e->con) | (I(e->cursoron) << 32);
        out[f++] = I(e->blink) | (I(e->scrollcache) << 32);
        out[f++] = I(e->firstline) | (I(e->lastline) << 32);
        out[f++] = I(e->displine);
        out[f++] = I(e->vrammask) | (I(e->vram_limit) << 32);
        out[f++] = I(e->video_res_x) | (I(e->video_res_y) << 32);
        out[f++] = I(e->video_bpp) | (I(e->frames) << 32);
        out[f++] = I(egaswitchread) | (I(egaswitches) << 32);
}
