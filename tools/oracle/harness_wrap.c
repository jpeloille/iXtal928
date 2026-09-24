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

/* LES DEUX ACCES DE DOUBLE MOT, ajoutes a C7a.
 *
 * writememl est une MACRO (386_common.h:32-36) : elle prend un chemin rapide par
 * eal_w quand l'adresse est alignee, sinon elle descend dans writememll — qui
 * n'etait PAS dans WRAP. Or x86_doabrt empile le code d'erreur par writememl des que
 * intgatesize != 16, et intgatesize vaut ZERO au depart : cette ecriture n'avait donc
 * aucun temoin. Ni compteur — les quatre de la memoire ont ete retires a M2 — ni
 * journal, ni hachage, RunSingle n'appelant jamais h_ram_hash (ses deux appels sont
 * dans Run, lignes 442 et 457). C'est l'accord vide au sens exact de harness.h.
 *
 * PAS DE COMPTEUR ICI, seulement le journal : ajouter n_writememll ou n_readmemll au
 * vecteur les ferait diverger pour la meme raison que les quatre retires — --wrap est
 * un mecanisme de LIEN et n'intercepte pas les appels internes a mem.c, si bien qu'un
 * acces a cheval compte une fois en C et quatre en C#. Le journal, lui, note des
 * ADRESSES et des VALEURS : il est vrai des deux cotes quel que soit le chemin.
 *
 * QUATRE h_wlog_note, un par octet, et c'est le pendant exact de __wrap_writememwl
 * qui en pose deux. */
extern void __real_writememll(uint32_t addr, uint32_t val);
void __wrap_writememll(uint32_t addr, uint32_t val) {
        h_wlog_note(addr & rammask, (uint8_t)val);
        h_wlog_note((addr + 1) & rammask, (uint8_t)(val >> 8));
        h_wlog_note((addr + 2) & rammask, (uint8_t)(val >> 16));
        h_wlog_note((addr + 3) & rammask, (uint8_t)(val >> 24));
        __real_writememll(addr, val);
}

extern uint32_t __real_readmemll(uint32_t addr);
uint32_t __wrap_readmemll(uint32_t addr) { return __real_readmemll(addr); }

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
