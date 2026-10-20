/* SPDX-FileCopyrightText: 2026 Julien Peloille
 * SPDX-License-Identifier: GPL-2.0-only
 *
 * GR.2a — lpt.c, lpt_dac.c et lpt_dss.c INCLUS ET NON LIÉS (PLAN-GR.md), dans une même unité : lpt1_device_p est
 * statique (lpt.c:38-39), dss_t et lpt_dac_t privées à leur fichier (lpt_dss.c:8-17, lpt_dac.c:8-15). Les noms
 * statiques des deux périphériques ne se croisent pas (dss_* contre dac_*). La sonde du port parallèle
 * (h_lpt_probe) les lit.
 */
#include "../../pcem-dev/src/lpt/lpt.c"
#include "../../pcem-dev/src/lpt/lpt_dac.c"
#include "../../pcem-dev/src/lpt/lpt_dss.c"

#include "harness.h"

/* La sonde de LPT1 et LPT2 (H_LPT_PROBE_N champs), dans l'ordre de lpt.cs (Probe) : les registres de données et de
 * contrôle des deux ports ; le périphérique de LPT1 (0 aucun, 1 la DSS, 2 le Covox, 3 le Covox stéréo) ; la DSS (la
 * file de seize octets en deux mots, les deux index, la valeur du DAC, le chronomètre) ; le Covox (les deux valeurs,
 * stéréo, le canal). */
void h_lpt_probe(uint64_t *o) {
        int c, i = 0;

        memset(o, 0, H_LPT_PROBE_N * sizeof(uint64_t));
        o[i++] = lpt1_dat;
        o[i++] = lpt1_ctrl;
        o[i++] = lpt2_dat;
        o[i++] = lpt2_ctrl;
        if (lpt1_device == &dss_device) {
                dss_t *d = lpt1_device_p;

                o[i++] = 1;
                for (c = 0; c < 8; c++) {
                        o[i] |= (uint64_t)d->fifo[c] << (8 * c);
                        o[i + 1] |= (uint64_t)d->fifo[c + 8] << (8 * c);
                }
                i += 2;
                o[i++] = (uint32_t)d->read_idx;
                o[i++] = (uint32_t)d->write_idx;
                o[i++] = d->dac_val;
                o[i++] = (uint32_t)d->timer.enabled;
        } else if (lpt1_device == &lpt_dac_device || lpt1_device == &lpt_dac_stereo_device) {
                lpt_dac_t *d = lpt1_device_p;

                o[i++] = lpt1_device == &lpt_dac_device ? 2 : 3;
                i += 6;
                o[i++] = d->dac_val_l;
                o[i++] = d->dac_val_r;
                o[i++] = (uint32_t)d->is_stereo;
                o[i++] = (uint32_t)d->channel;
        }
}
