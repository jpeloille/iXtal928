/* SPDX-FileCopyrightText: 2026 Julien Peloille
 * SPDX-License-Identifier: GPL-2.0-only
 *
 * GR.2a — scsi_zip.c INCLUS ET NON LIÉ (PLAN-GR.md), sur le patron de harness_aha.c : scsi_zip_data est privée au
 * fichier (scsi_zip.c:38-74), et la sonde du ZIP (h_zip_probe) la lit. Une unité de traduction à lui, comme
 * harness_scsi_cd.c.
 */
#include "../../pcem-dev/src/scsi/scsi_zip.c"

#include "harness.h"

static uint64_t h_zip_fnv(const uint8_t *p, int n) {
        uint64_t h = 1469598103934665603ULL;
        int c;

        for (c = 0; c < n; c++) {
                h ^= p[c];
                h *= 1099511628211ULL;
        }
        return h;
}

/* La sonde du ZIP SCSI ou ATAPI (H_ZIP_PROBE_N champs), dans l'ordre de scsi_zip.cs (Probe) ; data_in puis
 * data_out hachés d'un seul tenant (contigus dans le struct, comme io côté C#). Sans ZIP monté : tout à zéro. */
void h_zip_probe(uint64_t *o) {
        scsi_zip_data *d = zip_data;
        int i = 0;

        memset(o, 0, H_ZIP_PROBE_N * sizeof(uint64_t));
        if (!d)
                return;
        o[i++] = 1;
        o[i++] = (uint32_t)d->blocks;
        o[i++] = (uint32_t)d->cmd_pos;
        o[i++] = (uint32_t)d->new_cmd_pos;
        o[i++] = (uint32_t)d->addr;
        o[i++] = (uint32_t)d->len;
        o[i++] = (uint32_t)d->sector_pos;
        o[i++] = h_zip_fnv(d->buf, 512);
        o[i++] = d->status;
        o[i++] = h_zip_fnv(d->data_in, 2 * BUFFER_SIZE);
        o[i++] = (uint32_t)d->data_pos_read;
        o[i++] = (uint32_t)d->data_pos_write;
        o[i++] = (uint32_t)d->bytes_received;
        o[i++] = (uint32_t)d->bytes_required;
        o[i++] = (uint32_t)d->disc_loaded;
        o[i++] = (uint32_t)d->disc_changed;
        o[i++] = (uint32_t)d->sense_key | ((uint64_t)(uint32_t)d->asc << 16) | ((uint64_t)(uint32_t)d->ascq << 32);
        o[i++] = (uint32_t)d->hd_id;
        o[i++] = (uint32_t)d->callback_timer.enabled;
        o[i++] = (uint32_t)d->is_atapi;
        o[i++] = (uint32_t)d->read_only;
        o[i++] = (uint32_t)d->pio_mode;
        o[i++] = (uint32_t)d->mdma_mode;
}
