/* SPDX-FileCopyrightText: 2026 Julien Peloille
 * SPDX-License-Identifier: GPL-2.0-only
 *
 * Les symboles externes dont la machine a besoin pour tourner isolée.
 *
 * ------------------------------------------------------------------------------
 * PORTÉE — elle a changé en M2.
 *
 * L'oracle ne stube plus la mémoire, les E/S ni les timers : il lie désormais les
 * VRAIS fichiers de PCem —
 *
 *     mem.c  io.c  timer.c  pit.c  pic.c  dma.c  ppi.c  nmi.c  device.c  rom.c
 *
 * en plus du cœur CPU. Mesuré : le tout ne laisse que 64 symboles indéfinis, tous
 * triviaux, et aucun n'exige SDL, wx, OpenAL ni le dynarec. Le harnais différentiel
 * couvre donc maintenant la carte mère entière et pas seulement le CPU — ce qui
 * était la condition pour transcrire les périphériques sans voler à l'aveugle.
 *
 * On ne lie toujours PAS src/cpu/cpu.c : cpu_set() référence toutes les tables
 * d'opcodes et les modules codegen_timing_*, ce qui ramène le dynarec. Les valeurs
 * de configuration CPU sont donc posées à la main ici, pour un 8088 d'IBM PC 5150.
 *
 * ------------------------------------------------------------------------------
 * COMPTEURS D'APPELS — mitigation du risque n°3 du plan.
 *
 * Le cas dangereux n'est pas le désaccord entre les deux côtés, il est bruyant.
 * C'est l'accord vide : les deux stubent à la même constante, donc une divergence
 * dans le chemin qui la consomme est masquée. Les compteurs entrent dans le vecteur
 * d'état diffé, donc un stub resté à zéro des deux côtés alors que le test devait
 * l'exercer fait ÉCHOUER la passe.
 *
 * Depuis M2, les compteurs de mémoire et d'E/S sont posés par interposition (voir
 * h_count_*) puisque les vraies fonctions viennent de PCem.
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
 * par cote — et surtout en NOMMANT l'adresse divergente. Alimente par le crochet
 * h_wlog_note(), appele depuis le harnais. */

uint32_t h_wlog_addr[H_WLOG_MAX];
uint8_t h_wlog_val[H_WLOG_MAX];
int h_wlog_n;

void h_wlog_note(uint32_t addr, uint8_t val) {
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

/* --- configuration CPU : IBM PC 5150, Intel 8088 -------------------------- */

int AMSTRAD = 0, AT = 0, is386 = 0, PCI = 0, TANDY = 0, MCA = 0;
int is486 = 0;
int hasfpu = 0;
int fpu_type = 0;
int cpu_16bitbus = 0;
int cpu_iscyrix = 0;
int cpu_cyrix_alignment = 0;
int cpu_cache_int_enabled = 0, cpu_cache_ext_enabled = 0;
int cpu_block_end = 0;
int insc = 0;
int cpu_busspeed = 4772728;
int amstrad_latch = 0;
int romset = 0; /* ROM_IBMPC */

/* Le 8088 ne modélise pas de prefetch cote memoire (c'est 808x.c qui porte le
 * modele, cf. FETCH/FETCHADD) : ces compteurs restent nuls, comme le fait
 * cpu_update_waitstates() pour un 8088. */
int cpu_prefetch_cycles = 0, cpu_prefetch_width = 0;
int cpu_mem_prefetch_cycles = 0, cpu_rom_prefetch_cycles = 0;
int timing_misaligned = 0;

/* Le TSC : défini par cpu.c chez PCem, que l'oracle ne lie pas. */
uint64_t tsc = 0;

int cpu_get_speed(void) { return 4772728; }
void cpu_set_edx(void) { }
void cpu_update_waitstates(void) { }

/* --- timings de mode protégé : inatteignables sur un 8088 ----------------- */

int timing_rr = 0;
int timing_call_rm = 0, timing_call_pm = 0, timing_call_pm_gate = 0, timing_call_pm_gate_inner = 0;
int timing_int = 0, timing_int_rm = 0, timing_int_v86 = 0, timing_int_pm = 0, timing_int_pm_outer = 0;
int timing_iret_rm = 0, timing_iret_v86 = 0, timing_iret_pm = 0, timing_iret_pm_outer = 0;
int timing_jmp_rm = 0, timing_jmp_pm = 0, timing_jmp_pm_gate = 0;
int timing_retf_rm = 0, timing_retf_pm = 0, timing_retf_pm_outer = 0;

/* --- dynarec : non porté --------------------------------------------------- */

int codegen_flat_ds = 1, codegen_flat_ss = 1;
int codegen_in_recompile = 0;
uint32_t recomp_page = 0xffffffff;
void codegen_reset(void) { }
void codegen_flush(void) { }
void codegen_set_rounding_mode(int mode) { (void)mode; }
void ps2_cache_clean(void) { }
int xi8088_bios_128kb(void) { return 0; }

/* --- haut-parleur : inerte, mais les variables sont lues par ppi.c --------- */

int ppispeakon = 0;
int gated = 0, speakval = 0, speakon = 0;
void speaker_update(void) { }

/* --- vidéo : le cœur n'en a pas besoin pour le diff CPU ------------------- */

void video_updatetiming(void) { }

/* --- configuration et chemins --------------------------------------------- */

char logs_path[512] = "";
static char h_roms_path[512] = "roms/";
int num_roms_paths = 1;

int get_roms_path(int p, char *s, int size) {
        if (p != 0)
                return 0;
        strncpy(s, h_roms_path, size - 1);
        s[size - 1] = 0;
        return 1;
}

void h_set_roms_path(const char *p) {
        strncpy(h_roms_path, p, sizeof(h_roms_path) - 1);
        h_roms_path[sizeof(h_roms_path) - 1] = 0;
}

void put_backslash(char *s) {
        int c = (int)strlen(s) - 1;
        if (c >= 0 && s[c] != '/' && s[c] != '\\')
                strcat(s, "/");
}

int config_get_int(int is_global, char *head, char *name, int def) {
        (void)is_global; (void)head; (void)name;
        return def;
}

char *config_get_string(int is_global, char *head, char *name, char *def) {
        (void)is_global; (void)head; (void)name;
        return def;
}

/* --- journalisation -------------------------------------------------------
 * Silencieuse par défaut : l'oracle exécute des dizaines de millions
 * d'instructions. fatal() ne doit jamais être atteinte ; si elle l'est, on veut
 * un compteur et un message, pas un exit() qui emporterait le processus hôte. */

static int h_verbose = 0;

void h_set_verbose(int v) { h_verbose = v; }

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
