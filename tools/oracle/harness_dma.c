/* SPDX-FileCopyrightText: 2026 Julien Peloille
 * SPDX-License-Identifier: GPL-2.0-only
 *
 * G12.1 — dma.c INCLUS ET NON LIÉ (PLAN-G12.md, décision n° 17), comme scsi_aha1540.c par harness_aha.c : l'état
 * du 8237 (dma_m, dma_stat, dma_stat_rq, les bascules, les deux registres de commande, dmaregs, dma16regs,
 * dmapages) est `static` (dma.c:13-21), et la sonde du DMA (h_dma_probe) le lit. Le DMA 16 bits (canaux 4 à 7)
 * passe sous l'oracle avec la SB 16. Les drapeaux et les -I sont ceux de tous les objets de l'oracle.
 */
#include "../../pcem-dev/src/models/dma.c"

#include "harness.h"

static uint64_t h_dma_fnv(const uint8_t *p, int n) {
        uint64_t h = 1469598103934665603ULL;
        int c;

        for (c = 0; c < n; c++) {
                h ^= p[c];
                h *= 1099511628211ULL;
        }
        return h;
}

/* La sonde du DMA (H_DMA_PROBE_N champs), dans l'ordre de Models.dma.ProbeState côté C#. */
void h_dma_probe(uint64_t *o) {
        int c;

        *o++ = dma_m | ((uint64_t)dma_stat << 8) | ((uint64_t)dma_stat_rq << 16) | ((uint64_t)dma_command << 24) |
               ((uint64_t)dma16_command << 32);
        *o++ = (uint32_t)dma_wp | ((uint64_t)(uint32_t)dma16_wp << 32);
        *o++ = h_dma_fnv(dmaregs, sizeof(dmaregs));
        *o++ = h_dma_fnv(dma16regs, sizeof(dma16regs));
        *o++ = h_dma_fnv(dmapages, sizeof(dmapages));
        for (c = 0; c < 8; c++) {
                dma_t *d = &dma[c];

                *o++ = d->ab | ((uint64_t)d->ac << 32);
                *o++ = d->cb | ((uint64_t)(uint32_t)d->cc << 32);
                *o++ = d->mode | ((uint64_t)d->page << 8) | ((uint64_t)(uint8_t)d->size << 16) | ((uint64_t)(uint8_t)d->wp << 24) |
                       ((uint64_t)d->m << 32) | ((uint64_t)d->stat << 40) | ((uint64_t)d->stat_rq << 48) |
                       ((uint64_t)d->command << 56);
        }
}
