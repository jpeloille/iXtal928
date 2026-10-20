/* SPDX-FileCopyrightText: 2026 Julien Peloille
 * SPDX-License-Identifier: GPL-2.0-only
 *
 * GR.2a — scsi_cd.c INCLUS ET NON LIÉ (PLAN-GR.md), sur le patron de harness_aha.c : scsi_cd_data_t est privée au
 * fichier (scsi_cd.c:147-193), et la sonde du lecteur de CD (h_cd_data_probe) la lit. Une unité de traduction à
 * lui : scsi_cd.c, scsi_zip.c et scsi_hd.c définissent chacun BUFFER_SIZE et un scsi_add_data statique.
 */
#include "../../pcem-dev/src/scsi/scsi_cd.c"

#include "harness.h"

static uint64_t h_cdd_fnv(const uint8_t *p, int n) {
        uint64_t h = 1469598103934665603ULL;
        int c;

        for (c = 0; c < n; c++) {
                h ^= p[c];
                h *= 1099511628211ULL;
        }
        return h;
}

/* La sonde du lecteur de CD SCSI ou ATAPI (H_CD_DATA_PROBE_N champs), dans l'ordre de scsi_cd.cs (Probe). Sans
 * lecteur monté : tout à zéro, le champ 0 aussi (avec le lecteur, 1). */
void h_cd_data_probe(uint64_t *o) {
        scsi_cd_data_t *d = cd_data;
        int i = 0;

        memset(o, 0, H_CD_DATA_PROBE_N * sizeof(uint64_t));
        if (!d)
                return;
        o[i++] = 1;
        o[i++] = (uint32_t)d->blocks;
        o[i++] = (uint32_t)d->cmd_pos;
        o[i++] = (uint32_t)d->new_cmd_pos;
        o[i++] = (uint32_t)d->callback_timer.enabled;
        o[i++] = (uint32_t)d->wait_time;
        o[i++] = (uint32_t)d->addr;
        o[i++] = (uint32_t)d->len;
        o[i++] = (uint32_t)d->sector_pos;
        o[i++] = h_cdd_fnv(d->buf, 512);
        o[i++] = d->status;
        o[i++] = (uint32_t)d->data_pos_read;
        o[i++] = (uint32_t)d->data_pos_write;
        o[i++] = (uint32_t)d->data_bytes_read;
        o[i++] = (uint32_t)d->bytes_expected;
        o[i++] = (uint32_t)d->cdlen;
        o[i++] = (uint32_t)d->cdpos;
        o[i++] = (uint32_t)d->bytes_received;
        o[i++] = (uint32_t)d->bytes_required;
        o[i++] = (uint32_t)d->sense_key | ((uint64_t)(uint32_t)d->asc << 16) | ((uint64_t)(uint32_t)d->ascq << 32);
        o[i++] = (uint32_t)d->id;
        o[i++] = (uint32_t)d->received_cdb;
        o[i++] = h_cdd_fnv(d->cdb, CDB_MAX_LEN);
        o[i++] = (uint32_t)d->prev_status;
        o[i++] = (uint32_t)d->cd_status;
        o[i++] = d->prefix_len;
        o[i++] = d->page_current;
        o[i++] = (uint32_t)d->is_atapi;
        o[i++] = (uint32_t)d->pio_mode;
        o[i++] = (uint32_t)d->mdma_mode;
        o[i++] = (uint32_t)d->short_seek;
        o[i++] = (uint32_t)d->long_seek;
        o[i++] = (uint32_t)d->max_speed;
        o[i++] = (uint32_t)d->cur_speed;
        o[i++] = (uint32_t)d->cur_model;
        o[i++] = h_cdd_fnv(d->data_in, BUFFER_SIZE);
        o[i++] = h_cdd_fnv(d->data_out, BUFFER_SIZE);
}
