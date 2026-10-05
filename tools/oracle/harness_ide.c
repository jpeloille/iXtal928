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
