/* SPDX-FileCopyrightText: 2026 Julien Peloille
 * SPDX-License-Identifier: GPL-2.0-only
 *
 * Les symboles externes dont le cœur 8088 a besoin pour tourner isolé.
 *
 * Portée : 808x.c + x86seg.c + x87.c + x87_timings.c + 386_common.c laissent 66
 * symboles PCem indéfinis. Ce fichier les fournit. Ni SDL, ni wx, ni OpenAL, ni
 * ROM : gcc seul suffit, et M0-M3 sont donc vérifiables sans un seul apt install.
 *
 * On ne lie PAS le vrai src/cpu/cpu.c : cpu_set() référence toutes les tables
 * d'opcodes (ops_386, ops_fpu_*) et tous les modules codegen_timing_*, ce qui fait
 * passer les symboles manquants de 66 à 133 et ramène le dynarec qu'on ne porte
 * pas. Les valeurs de configuration CPU sont donc posées ici, à la main, pour un
 * 8088 d'IBM XT.
 *
 * Sur les timing_* : 808x.c n'en utilise AUCUN (grep -c timing_ -> 0) ; ses coûts
 * par opcode sont des constantes littérales dans le switch. x86seg.c les lit, mais
 * uniquement dans les branches de mode protégé, inatteignables quand msw & 1 == 0.
 * Les mettre à zéro est donc sans effet sur la cible XT — et si le palier (b)
 * arrive un jour, il faudra les remplacer par cpu_set(), pas les patcher ici.
 *
 * ------------------------------------------------------------------------------
 * COMPTEURS D'APPELS — mitigation du risque n°3 du plan.
 *
 * Le cas dangereux n'est pas le désaccord entre les deux côtés, il est bruyant.
 * C'est l'accord vide : les deux stubent à la même constante, donc une vraie
 * divergence dans le chemin qui consomme cette constante est masquée. « Aucun
 * côté n'a jamais appelé inb » se lit exactement comme un accord.
 *
 * Chaque stub incrémente donc un compteur, et les compteurs font partie du
 * vecteur d'état diffé. Un stub que le test devait exercer et qui reste à zéro
 * des deux côtés fait échouer la passe au lieu de la faire passer.
 * ------------------------------------------------------------------------------
 */

#include <stdarg.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#include "ibm.h"
#include "cpu.h"
#include "mem.h"
#include "pic.h"
#include "timer.h"
#include "x86.h"

#include "harness.h"

/* --- journal d'ecritures --------------------------------------------------
 * Une instruction n'ecrit qu'a une poignee d'endroits. Enregistrer ces adresses
 * permet de comparer la memoire exactement, sans hacher 1 Mo par instruction et
 * par cote — et surtout en NOMMANT l'adresse divergente au lieu de dire « la RAM
 * differe quelque part ». */

uint32_t h_wlog_addr[H_WLOG_MAX];
uint8_t h_wlog_val[H_WLOG_MAX];
int h_wlog_n;

static void wlog(uint32_t addr, uint8_t val) {
        if (h_wlog_n < H_WLOG_MAX) {
                h_wlog_addr[h_wlog_n] = addr;
                h_wlog_val[h_wlog_n] = val;
        }
        h_wlog_n++; /* continue de compter au-dela, pour que le depassement se voie */
}

void h_wlog_reset(void) { h_wlog_n = 0; }
int h_wlog_count(void) { return h_wlog_n; }
uint32_t h_wlog_get_addr(int i) { return (i >= 0 && i < H_WLOG_MAX) ? h_wlog_addr[i] : 0; }
uint8_t h_wlog_get_val(int i) { return (i >= 0 && i < H_WLOG_MAX) ? h_wlog_val[i] : 0; }

/* --- compteurs ------------------------------------------------------------ */

uint64_t h_n_inb, h_n_outb, h_n_picint, h_n_picinterrupt, h_n_timer_process;
uint64_t h_n_readmembl, h_n_writemembl, h_n_readmemwl, h_n_writememwl, h_n_fatal;

void h_stub_counters_reset(void) {
        h_n_inb = h_n_outb = h_n_picint = h_n_picinterrupt = h_n_timer_process = 0;
        h_n_readmembl = h_n_writemembl = h_n_readmemwl = h_n_writememwl = h_n_fatal = 0;
}

/* --- RAM et tables de correspondance -------------------------------------- */

uint8_t *ram;
uint32_t rammask = 0xFFFFF; /* XT : bus d'adresse 20 bits, tout reboucle */
int mem_size = 640;         /* Ko — 640 Ko, le maximum d'un XT */
int mmu_perm = 4;

uintptr_t *readlookup2;
uintptr_t *writelookup2;
int readlnum = 0, writelnum = 0;

/* Le cache de pages de 4 Ko EST le chemin chaud du 8088 et il porte du temps :
 * addreadlookup facture cycles -= 9 (mem.c:378). L'oracle le laisse entièrement
 * à -1, ce qui force chaque accès à descendre dans readmembl/writemembl — donc
 * un chemin déterministe, et exactement celui que M1 doit reproduire. Le
 * remplissage du cache et sa facturation arrivent à M2 avec le vrai mem.c. */
void h_mem_init(void) {
        if (!ram)
                ram = calloc(H_RAM_SIZE, 1);
        if (!readlookup2) {
                readlookup2 = malloc(sizeof(uintptr_t) * (1 << 20));
                writelookup2 = malloc(sizeof(uintptr_t) * (1 << 20));
        }
        memset(readlookup2, 0xFF, sizeof(uintptr_t) * (1 << 20)); /* -1 partout */
        memset(writelookup2, 0xFF, sizeof(uintptr_t) * (1 << 20));
        readlnum = writelnum = 0;
}

uint8_t readmembl(uint32_t addr) {
        h_n_readmembl++;
        return ram[addr & rammask];
}

void writemembl(uint32_t addr, uint8_t val) {
        h_n_writemembl++;
        ram[addr & rammask] = val;
        wlog(addr & rammask, val);
}

uint16_t readmemwl(uint32_t addr) {
        h_n_readmemwl++;
        /* Reproduit le rebouclage 20 bits octet par octet : une lecture à
         * 0xFFFFF relit l'octet 0. */
        return (uint16_t)(ram[addr & rammask] | ((uint16_t)ram[(addr + 1) & rammask] << 8));
}

void writememwl(uint32_t addr, uint16_t val) {
        h_n_writememwl++;
        ram[addr & rammask] = (uint8_t)val;
        ram[(addr + 1) & rammask] = (uint8_t)(val >> 8);
        wlog(addr & rammask, (uint8_t)val);
        wlog((addr + 1) & rammask, (uint8_t)(val >> 8));
}

uint32_t readmemll(uint32_t addr) {
        return (uint32_t)readmemwl(addr) | ((uint32_t)readmemwl(addr + 2) << 16);
}

void writememll(uint32_t addr, uint32_t val) {
        writememwl(addr, (uint16_t)val);
        writememwl(addr + 2, (uint16_t)(val >> 16));
}

void resetreadlookup(void) {
        h_mem_init();
}

void flushmmucache(void) { /* pas de pagination sur XT (cr0 >> 31 == 0) */ }
void flushmmucache_cr3(void) { }

/* --- E/S ------------------------------------------------------------------
 * Bus ouvert : 0xFF en lecture, écritures avalées. C'est ce que voit un XT sans
 * carte sur le port visé, et c'est ce que le C# doit reproduire à l'identique. */

uint8_t inb(uint16_t port) {
        (void)port;
        h_n_inb++;
        return 0xFF;
}

void outb(uint16_t port, uint8_t val) {
        (void)port;
        (void)val;
        h_n_outb++;
}

/* --- interruptions -------------------------------------------------------- */

PIC pic, pic2;
int nmi_mask = 0;

void picint(uint16_t num) {
        (void)num;
        h_n_picint++;
}

uint8_t picinterrupt(void) {
        h_n_picinterrupt++;
        return 0xFF;
}

/* --- temps ----------------------------------------------------------------
 * timer_target à son maximum : aucun timer n'échoit jamais, donc clockhardware()
 * fait avancer le TSC sans jamais appeler de callback. Le vrai timer.c arrive
 * à M2. */

uint64_t tsc = 0;
uint32_t timer_target = 0x7FFFFFFF;

void timer_process(void) {
        h_n_timer_process++;
}

/* --- configuration CPU : IBM XT, Intel 8088 ------------------------------- */

int AMSTRAD = 0, AT = 0, is386 = 0, PCI = 0, TANDY = 0, MCA = 0;
int is486 = 0;
int hasfpu = 0;
int fpu_type = 0;
int cpu_16bitbus = 0;
int cpu_iscyrix = 0;
int cpu_cache_int_enabled = 0, cpu_cache_ext_enabled = 0;
int cpu_block_end = 0;
int insc = 0;

void cpu_set_edx(void) { }
void cpu_update_waitstates(void) { }

/* --- timings de mode protégé : inatteignables sur XT (cf. en-tête) -------- */

int timing_rr = 0;
int timing_call_rm = 0, timing_call_pm = 0, timing_call_pm_gate = 0, timing_call_pm_gate_inner = 0;
int timing_int = 0, timing_int_rm = 0, timing_int_v86 = 0, timing_int_pm = 0, timing_int_pm_outer = 0;
int timing_iret_rm = 0, timing_iret_v86 = 0, timing_iret_pm = 0, timing_iret_pm_outer = 0;
int timing_jmp_rm = 0, timing_jmp_pm = 0, timing_jmp_pm_gate = 0;
int timing_retf_rm = 0, timing_retf_pm = 0, timing_retf_pm_outer = 0;

/* --- dynarec et SMM : non portés ------------------------------------------ */

int codegen_flat_ds = 1, codegen_flat_ss = 1;
void codegen_reset(void) { }
void codegen_set_rounding_mode(int mode) { (void)mode; }

static void smram_nop(void) { }
void (*smram_enable)(void) = smram_nop;
void (*smram_disable)(void) = smram_nop;

/* --- journalisation -------------------------------------------------------
 * Silencieuse par défaut : l'oracle exécute des dizaines de millions
 * d'instructions. fatal() ne doit jamais être atteinte ; si elle l'est, on veut
 * un compteur et un message, pas un exit() qui emporterait le processus C# hôte. */

char logs_path[512] = "";
static int h_verbose = 0;

void h_set_verbose(int v) {
        h_verbose = v;
}

void pclog(const char *format, ...) {
        if (!h_verbose)
                return;
        va_list ap;
        va_start(ap, format);
        vfprintf(stderr, format, ap);
        va_end(ap);
}

void error(const char *format, ...) {
        va_list ap;
        va_start(ap, format);
        fprintf(stderr, "oracle error: ");
        vfprintf(stderr, format, ap);
        va_end(ap);
}

void fatal(const char *format, ...) {
        va_list ap;
        h_n_fatal++;
        va_start(ap, format);
        fprintf(stderr, "oracle FATAL: ");
        vfprintf(stderr, format, ap);
        va_end(ap);
        /* Pas d'exit() : on est dans une .so chargée par le processus de test. */
}

void pclog_end(void) { }
