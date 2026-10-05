/* SPDX-FileCopyrightText: 2026 Julien Peloille
 * SPDX-License-Identifier: GPL-2.0-only
 *
 * G11.0 — scsi_aha1540.c et scsi_hd.c INCLUS ET NON LIÉS (PLAN-G11.md), comme ide.c par harness_ide.c : les
 * structs de la carte (aha154x_t, scsi_aha1540.c:61-179) et du disque (scsi_hd_data, scsi_hd.c:19-46) sont
 * privées à leur fichier, et la sonde de la carte (h_aha_probe) les lit. Les drapeaux et les -I sont ceux de
 * tous les objets de l'oracle. Les deux fichiers n'ont aucun symbole statique en commun.
 */
#include "../../pcem-dev/src/scsi/scsi_aha1540.c"
#include "../../pcem-dev/src/scsi/scsi_hd.c"

#include "harness.h"

extern device_t *devices[DEV_MAX];   /* device.c:15, :24 */
extern void *device_priv[DEV_MAX];

static uint64_t h_aha_fnv(const uint8_t *p, int n) {
        uint64_t h = 1469598103934665603ULL;
        int c;

        for (c = 0; c < n; c++) {
                h ^= p[c];
                h *= 1099511628211ULL;
        }
        return h;
}

/* La sonde de la carte (H_AHA_PROBE_N champs), dans l'ordre de scsi_aha1540.cs (Probe) : les trois machines
 * d'états, les mailbox, le CCB, la CDB, la configuration (dont la base de la fenêtre de ROM), les empreintes de l'EEPROM, de la RAM d'ombre,
 * d'int_buffer et du tampon de canal 2, le bus, puis, pour chacun des sept ID, l'état du disque SCSI (zéros
 * sans disque). Sans carte montée : tout à zéro, et le champ 0 vaut 0 (avec la carte, 1). */
void h_aha_probe(uint64_t *o) {
        aha154x_t *s = NULL;
        int c, i = 0;

        memset(o, 0, H_AHA_PROBE_N * sizeof(uint64_t));
        for (c = 0; c < DEV_MAX; c++) {
                if (devices[c] == &scsi_aha1542c_device) {
                        s = device_priv[c];
                        break;
                }
        }
        if (!s)
                return;
        o[i++] = 1;
        o[i++] = s->status;
        o[i++] = s->isr;
        o[i++] = s->cmd_state;
        o[i++] = s->ccb_state;
        o[i++] = s->scsi_state;
        o[i++] = s->bios_cmd_state;
        o[i++] = s->command;
        o[i++] = s->mbc;
        o[i++] = s->mba;
        o[i++] = s->mba_i;
        o[i++] = s->mbo_req;
        o[i++] = s->current_mbo;
        o[i++] = s->current_mbo_is_bios;
        o[i++] = s->current_mbi;
        o[i++] = s->bios_mbc;
        o[i++] = s->bios_mba;
        o[i++] = s->bios_mbo_req;
        o[i++] = s->bios_mbo_inited;
        o[i++] = s->mbo_irq_enable;
        o[i++] = s->ccb.addr;
        o[i++] = s->ccb.from_mailbox;
        o[i++] = s->ccb.status;
        o[i++] = s->ccb.req_sense_len;
        o[i++] = s->ccb.target_id;
        o[i++] = s->ccb.data_len;
        o[i++] = s->ccb.data_pointer;
        o[i++] = s->cdb.idx;
        o[i++] = s->cdb.len;
        o[i++] = s->cdb.data_idx;
        o[i++] = s->cdb.data_len;
        o[i++] = s->cdb.data_pointer;
        o[i++] = s->cdb.last_status;
        o[i++] = s->result_pos;
        o[i++] = s->result_len;
        o[i++] = s->data_in;
        o[i++] = s->reg3_idx;
        o[i++] = s->host_id;
        o[i++] = s->dma;
        o[i++] = s->irq;
        o[i++] = s->shadow;
        o[i++] = s->bios_bank;
        o[i++] = s->dipsw;
        o[i++] = s->mapping.base;
        o[i++] = s->e_d;
        o[i++] = s->to;
        o[i++] = s->bon;
        o[i++] = s->boff;
        o[i++] = s->atbs;
        o[i++] = s->mbu;
        o[i++] = s->mblt;
        o[i++] = h_aha_fnv(s->eeprom, 256);
        o[i++] = h_aha_fnv(s->shadow_ram, 0x4000);
        o[i++] = h_aha_fnv(s->int_buffer, 512);
        o[i++] = h_aha_fnv(s->dma_buffer, 64);
        o[i++] = s->bus.state;
        o[i++] = (uint64_t)(int64_t)s->bus.dev_id;
        o[i++] = s->bus.bus_out;
        o[i++] = s->bus.bus_in;
        o[i++] = s->bus.command_pos;
        o[i++] = s->bus.clear_req;
        o[i++] = s->bus.change_state_delay;
        o[i++] = s->bus.new_req_delay;
        for (c = 0; c < 7; c++) {
                scsi_hd_data *d = s->bus.devices[c] == &scsi_hd ? s->bus.device_data[c] : NULL;

                if (!d) {
                        i += 11;
                        continue;
                }
                o[i++] = 1;
                o[i++] = d->cmd_pos;
                o[i++] = (uint32_t)d->addr;
                o[i++] = (uint32_t)d->len;
                o[i++] = (uint32_t)d->data_pos_read;
                o[i++] = (uint32_t)d->data_pos_write;
                o[i++] = (uint32_t)d->bytes_received;
                o[i++] = (uint32_t)d->bytes_required;
                o[i++] = d->sense_key | (d->asc << 8) | (d->ascq << 16) | ((uint64_t)d->status << 24);
                o[i++] = h_aha_fnv(d->buf, 512);
                o[i++] = h_aha_fnv(d->data_in, 2 * BUFFER_SIZE);
        }
}
