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
/* G8.1 — sound_dbopl.cc aussi, INCLUS : son opl[2] est static, et l'amorçage le remet à zéro. */
#include "../../pcem-dev/src/sound/sound_dbopl.cc"
#include <new>

/* G8.1 — NukedOPL (src/dosbox/nukedopl.cpp) n'est pas lié : opl_emu est figé à DBOPL (décision de
 * l'orchestrateur sous mandat, 02/10), et opl_init ne prend la branche Nuked que pour un OPL3 avec
 * opl_emu non nul (sound_dbopl.cc:32). Ses quatre fonctions sont inatteignables : arrêt bruyant. */
extern "C" void fatal(const char *format, ...);
void OPL3_Reset(opl3_chip *chip, Bit32u samplerate) { (void)chip; (void)samplerate; fatal("OPL3_Reset : NukedOPL non lie (G8)\n"); }
Bit32u OPL3_WriteAddr(opl3_chip *chip, Bit32u port, Bit8u val) { (void)chip; (void)port; (void)val; fatal("OPL3_WriteAddr : NukedOPL non lie (G8)\n"); return 0; }
void OPL3_WriteReg(opl3_chip *chip, Bit16u reg, Bit8u v) { (void)chip; (void)reg; (void)v; fatal("OPL3_WriteReg : NukedOPL non lie (G8)\n"); }
void OPL3_GenerateStream(opl3_chip *chip, Bit16s *sndptr, Bit32u numsamples) { (void)chip; (void)sndptr; (void)numsamples; fatal("OPL3_GenerateStream : NukedOPL non lie (G8)\n"); }

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

/* G8.1 — DÉVIATION DE L'ORACLE (PLAN-G8.md, défaut n° 4) : opl[] est statique (sound_dbopl.cc:5-18)
 * et traverse les amorçages d'un processus — opl_init ne remet ni l'état, ni le masque, ni les
 * minuteries, ni les opérateurs que Chip::Setup ne réécrit pas. À chaque h_boot : la puce
 * reconstruite en place, le reste à zéro. Le C# fait de même (opl_clear_state_for_oracle_parity). */
/* G8.1 — l'état de opl[nr] que le logiciel LIT (388h) et que les minuteries portent, pour la sonde
 * du son : adresse, état, masque, contrôle des minuteries, les deux périodes. */
void h_opl_state(int nr, uint64_t *out) {
        out[0] = (uint64_t)(int64_t)opl[nr].addr;
        out[1] = opl[nr].status;
        out[2] = opl[nr].status_mask;
        out[3] = opl[nr].timer_ctrl;
        out[4] = (uint64_t)(int64_t)opl[nr].timer[0];
        out[5] = (uint64_t)(int64_t)opl[nr].timer[1];
}

void h_opl_reset(void) {
        for (int nr = 0; nr < 2; nr++) {
                opl[nr].chip.~Chip();
                new (&opl[nr].chip) DBOPL::Chip();
                memset(&opl[nr].opl3chip, 0, sizeof(opl[nr].opl3chip));
                opl[nr].addr = 0;
                opl[nr].timer[0] = opl[nr].timer[1] = 0;
                opl[nr].timer_ctrl = opl[nr].status_mask = opl[nr].status = 0;
                opl[nr].is_opl3 = opl[nr].opl_emu = 0;
                opl[nr].timer_callback = NULL;
                opl[nr].timer_param = NULL;
        }
}

}
