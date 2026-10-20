/* SPDX-FileCopyrightText: 2026 Julien Peloille
 * SPDX-License-Identifier: GPL-2.0-only
 *
 * G10.4 — ide.c INCLUS ET NON LIÉ (PLAN-G10.md, décision n° 6), comme 808x.c ou vid_*.c : la struct IDE
 * est privée au fichier (ide.c:57-79), et l'amorçage doit atteindre le pont ATAPI de chaque unité
 * (ide_drives[].atapi) pour le remettre à zéro, comme le C# (Ide/ide_atapi.cs,
 * atapi_clear_state_for_oracle_parity). Les drapeaux et les -I sont ceux de tous les objets de l'oracle ;
 * ide.c pose ses #define (_GNU_SOURCE, IDE_TIME) avant tout en-tête, d'où l'inclusion en tête.
 */
#include "../../pcem-dev/src/ide/ide.c"

#include "harness.h"

/* ORACLE PARITY : le pont des quatre unités repart de zéro à chaque amorçage ; chez PCem, son état traverse
 * resetide et resetpchard. resetide repose ensuite ses cinq pointeurs (ide.c:294-298). */
void h_atapi_reset(void) {
        int d;

        for (d = 0; d < 4; d++)
                memset(&ide_drives[d].atapi, 0, sizeof(ide_drives[d].atapi));
}

/* Le type de l'unité d (IDE_NONE 0, IDE_HDD 1, IDE_CDROM 2), pour --expect-cd. */
int h_ide_type(int d) {
        if (d < 0 || d > 3)
                return -1;
        return ide_drives[d].type;
}

static uint64_t h_ide_fnv(const uint8_t *p, int n) {
        uint64_t h = 1469598103934665603ULL;
        int c;

        for (c = 0; c < n; c++) {
                h ^= p[c];
                h *= 1099511628211ULL;
        }
        return h;
}

/* GR.2a — la sonde du pont ATAPI (H_ATAPI_PROBE_N champs), dans l'ordre de ide_atapi.cs (Probe) : pour chacune des
 * quatre unités, son type, la commande et sa position, l'état, la longueur de transfert, data[65536] haché et ses
 * positions, l'état du bus côté pont, la carte, le DMA ; puis le scsi_bus_t du pont (les états, les lignes, la
 * cible, la CDB hachée, les délais). atapi_device_t est publique (ide_atapi.h:33-56) : ide.c, déjà inclus, suffit. */
void h_atapi_probe(uint64_t *o) {
        int d, i = 0;

        memset(o, 0, H_ATAPI_PROBE_N * sizeof(uint64_t));
        for (d = 0; d < 4; d++) {
                atapi_device_t *a = &ide_drives[d].atapi;

                o[i++] = ide_drives[d].type;
                o[i++] = h_ide_fnv(a->command, 12);
                o[i++] = (uint32_t)a->command_pos;
                o[i++] = (uint32_t)a->state;
                o[i++] = (uint32_t)a->max_transfer_len;
                o[i++] = h_ide_fnv(a->data, 65536);
                o[i++] = (uint32_t)a->data_read_pos;
                o[i++] = (uint32_t)a->data_write_pos;
                o[i++] = (uint32_t)a->bus_state;
                o[i++] = (uint32_t)a->board;
                o[i++] = (uint32_t)a->use_dma;
                o[i++] = (uint32_t)a->bus.state;
                o[i++] = (uint32_t)a->bus.new_state;
                o[i++] = (uint32_t)a->bus.clear_req;
                o[i++] = a->bus.bus_in;
                o[i++] = a->bus.bus_out;
                o[i++] = (uint32_t)a->bus.dev_id;
                o[i++] = (uint32_t)a->bus.command_pos;
                o[i++] = h_ide_fnv(a->bus.command, CDB_MAX_LEN);
                o[i++] = (uint32_t)a->bus.change_state_delay;
                o[i++] = (uint32_t)a->bus.new_req_delay;
                o[i++] = (uint32_t)a->bus.is_atapi;
        }
}
