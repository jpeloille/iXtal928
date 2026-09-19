/* SPDX-FileCopyrightText: 2026 Julien Peloille
 * SPDX-License-Identifier: GPL-2.0-only
 *
 * Interposition sur les fonctions de PCem, par -Wl,--wrap.
 *
 * Depuis M2 l'oracle lie les VRAIS mem.c, io.c, timer.c et pic.c. On ne peut donc
 * plus poser les compteurs et le journal d'écritures dans des stubs : il faudrait
 * modifier l'arbre vendoré, ce que la règle interdit — une référence modifiée n'est
 * plus une référence.
 *
 * -Wl,--wrap=f fait pointer toute référence EXTERNE à f vers __wrap_f, et laisse
 * __real_f désigner l'originale. Les appels de 808x.c vers readmembl passent donc
 * ici, comptent, puis descendent dans le vrai mem.c de PCem, inchangé.
 *
 * Côté C#, la transcription de mem.c porte ces mêmes compteurs EN LIGNE. Les deux
 * comptent les mêmes événements par des moyens différents — c'est une DÉVIATION
 * assumée, et le vecteur d'état diffé la vérifie à chaque instruction.
 */

#include <stdint.h>

#include "harness.h"

extern uint64_t h_n_inb, h_n_outb, h_n_picint, h_n_picinterrupt, h_n_timer_process;
extern uint64_t h_n_readmembl, h_n_writemembl, h_n_readmemwl, h_n_writememwl;
extern void h_wlog_note(uint32_t addr, uint8_t val);
extern uint32_t rammask;

/* --- mémoire --------------------------------------------------------------- */

extern uint8_t __real_readmembl(uint32_t addr);
uint8_t __wrap_readmembl(uint32_t addr) {
        h_n_readmembl++;
        return __real_readmembl(addr);
}

extern void __real_writemembl(uint32_t addr, uint8_t val);
void __wrap_writemembl(uint32_t addr, uint8_t val) {
        h_n_writemembl++;
        h_wlog_note(addr & rammask, val);
        __real_writemembl(addr, val);
}

extern uint16_t __real_readmemwl(uint32_t addr);
uint16_t __wrap_readmemwl(uint32_t addr) {
        h_n_readmemwl++;
        return __real_readmemwl(addr);
}

extern void __real_writememwl(uint32_t addr, uint16_t val);
void __wrap_writememwl(uint32_t addr, uint16_t val) {
        h_n_writememwl++;
        h_wlog_note(addr & rammask, (uint8_t)val);
        h_wlog_note((addr + 1) & rammask, (uint8_t)(val >> 8));
        __real_writememwl(addr, val);
}

/* --- E/S ------------------------------------------------------------------- */

extern uint8_t __real_inb(uint16_t port);
uint8_t __wrap_inb(uint16_t port) {
        h_n_inb++;
        return __real_inb(port);
}

extern void __real_outb(uint16_t port, uint8_t val);
void __wrap_outb(uint16_t port, uint8_t val) {
        h_n_outb++;
        __real_outb(port, val);
}

/* --- interruptions et temps ------------------------------------------------ */

extern void __real_picint(uint16_t num);
void __wrap_picint(uint16_t num) {
        h_n_picint++;
        __real_picint(num);
}

extern uint8_t __real_picinterrupt(void);
uint8_t __wrap_picinterrupt(void) {
        h_n_picinterrupt++;
        return __real_picinterrupt();
}

extern void __real_timer_process(void);
void __wrap_timer_process(void) {
        h_n_timer_process++;
        __real_timer_process();
}
