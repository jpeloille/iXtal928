/* SPDX-FileCopyrightText: 2026 Julien Peloille
 * SPDX-License-Identifier: GPL-2.0-only
 *
 * G8.0 — DBOPL (src/dosbox/dbopl.cpp), INCLUS ET NON LIÉ : ses tables (MulTable, WaveTable,
 * KslTable, TremoloTable) sont `static` au fichier, et `opl-tables-check` doit les lire pour les
 * comparer au C#. InitTables les calcule par pow() et sin() de la libm de l'HÔTE
 * (dbopl.cpp:1311-1464) : c'est le seul point flottant de l'OPL dont la parité n'est pas acquise.
 * dbopl.cpp cesse donc de figurer dans les sources compilées à part et se compile ICI, en C++.
 */

#include "../../pcem-dev/src/dosbox/dbopl.cpp"

extern "C" {

/* Rend les tables après InitTables, dans des tampons de l'appelant : MulTable[384],
 * WaveTable[8*512], KslTable[8*16], TremoloTable[TREMOLO_TABLE]. Rend TREMOLO_TABLE. */
int h_opl_tables(uint16_t *mul, int16_t *wave, uint8_t *ksl, uint8_t *trem) {
        DBOPL::InitTables();
        memcpy(mul, DBOPL::MulTable, sizeof(DBOPL::MulTable));
        memcpy(wave, DBOPL::WaveTable, sizeof(DBOPL::WaveTable));
        memcpy(ksl, DBOPL::KslTable, sizeof(DBOPL::KslTable));
        memcpy(trem, DBOPL::TremoloTable, sizeof(DBOPL::TremoloTable));
        return TREMOLO_TABLE;
}

}
