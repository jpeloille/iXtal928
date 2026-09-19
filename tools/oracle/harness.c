/* SPDX-FileCopyrightText: 2026 Julien Peloille
 * SPDX-License-Identifier: GPL-2.0-only
 *
 * L'oracle : le vrai cœur 8088 de PCem, pilotable instruction par instruction.
 *
 * ------------------------------------------------------------------------------
 * POURQUOI ON INCLUT LE .c ET PAS SEULEMENT SON EN-TÊTE
 *
 * Tout le modèle de temps du 8088 vit dans des variables `static` de 808x.c :
 *
 *     static int      memcycs;                    (808x.c:50)
 *     static int      nextcyc = 0;                (808x.c:49)
 *     static int      cycdiff;                    (808x.c:52)
 *     static int      fetchcycles = 0, fetchclocks;   (808x.c:115)
 *     static uint8_t  prefetchqueue[6];           (808x.c:117)
 *     static uint16_t prefetchpc;                 (808x.c:118)
 *     static int      prefetchw = 0;              (808x.c:119)
 *     static uint64_t tsc_frac = 0;               (808x.c:891)
 *     static int      noint, inhlt, takeint;
 *
 * Aucun accesseur ne les exporte. En liant 808x.o normalement, elles sont
 * invisibles — et le risque n°1 du plan (dérive temporelle silencieuse, découverte
 * trois jalons trop tard) n'a alors aucune mitigation possible : on ne pourrait
 * comparer que les registres, qui restent justes quand le temps dérive.
 *
 * Compiler 808x.c DANS cette unité de traduction les met à portée. C'est ce qui
 * rend le diff par instruction de M1 réalisable.
 * ------------------------------------------------------------------------------
 */

#include <stdint.h>
#include <string.h>

#include "harness.h"

/* Le cœur lui-même. Chemin explicite plutôt qu'un -I : on veut que la ligne dise
 * ce qu'elle fait. Doit venir après harness.h et avant tout code ci-dessous. */
#include "../../pcem-dev/src/cpu/808x.c"

/* x86.h:122 définit `cycles` comme une macro vers cpu_state._cycles. Le
 * préprocesseur ne connaît pas l'accès à un membre : `out->cycles` deviendrait
 * `out->cpu_state._cycles`. On la retire donc ici et on écrit explicitement
 * cpu_state._cycles dans ce fichier — h_state peut alors garder le nom `cycles`,
 * qui est celui que le C# doit comparer. */
#undef cycles

/* Fournis par harness_stubs.c */
extern uint64_t h_n_inb, h_n_outb, h_n_picint, h_n_picinterrupt, h_n_timer_process;
extern uint64_t h_n_readmembl, h_n_writemembl, h_n_readmemwl, h_n_writememwl, h_n_fatal;
extern void h_stub_counters_reset(void);
extern void h_mem_init(void);
extern void h_set_verbose(int v);

static uint64_t h_ins_count;

uint32_t h_abi_version(void) {
        return H_ABI_VERSION;
}

uint32_t h_state_size(void) {
        return (uint32_t)sizeof(h_state);
}

uint8_t *h_ram(void) {
        return ram;
}

void h_reset(void) {
        h_mem_init();
        h_stub_counters_reset();
        h_ins_count = 0;

        /* Configuration machine : IBM XT, Intel 8088.
         * Posée avant resetx86() parce que celle-ci branche sur AT, is486 et
         * is386 (808x.c:671-687) pour choisir le vecteur de reset et rammask. */
        AT = 0;
        is386 = 0;
        is486 = 0;
        is8086 = 0; /* 8088 : file de préfetch de 4 octets, pas 6 */
        hasfpu = 0;
        cpu_16bitbus = 0;
        AMSTRAD = TANDY = PCI = MCA = 0;

        /* Multiplicateur TSC du XT. clockhardware() (808x.c:893-904) convertit les
         * cycles CPU en tops de l'oscillateur maître à 14,318 MHz en virgule fixe
         * 32:32, parce qu'il n'y a pas de rapport entier entre les deux fréquences.
         * Valeur reprise de setpitclock() (models/pit.c:52) pour un 8088 à
         * 4 772 728 Hz — cpus_8088[0].rspeed, cpu_tables.c:33. */
        xt_cpu_multi = (uint64_t)((14318184.0 * (double)(1ull << 32)) / 4772728.0);

        tsc = 0;
        timer_target = 0x7FFFFFFF;

        resetx86();

        /* resetx86() ne remet pas ces statiques : elles portent l'état de temps
         * entre instructions, et un reset partiel ferait diverger l'oracle de
         * lui-même d'un appel à l'autre. */
        nextcyc = 0;
        memcycs = 0;
        cycdiff = 0;
        current_diff = 0;
        fetchcycles = 0;
        fetchclocks = 0;
        tsc_frac = 0;
        noint = 0;
        inhlt = 0;
        takeint = 0;
        cpu_state._cycles = 0;
        ins = 0;
        insc = 0;
}

void h_load(uint32_t addr, const uint8_t *buf, uint32_t len) {
        for (uint32_t i = 0; i < len; i++)
                ram[(addr + i) & 0xFFFFF] = buf[i];
}

void h_fill_ram(uint8_t value) {
        memset(ram, value, H_RAM_SIZE);
}

void h_read(uint32_t addr, uint8_t *buf, uint32_t len) {
        for (uint32_t i = 0; i < len; i++)
                buf[i] = ram[(addr + i) & 0xFFFFF];
}

void h_set_cs_ip(uint16_t cs_sel, uint16_t ip) {
        loadcs(cs_sel);
        cpu_state.pc = ip;
        FETCHCLEAR(); /* vidange du pipeline, comme sur tout transfert de contrôle */
}

void h_setregs(const uint16_t r[H_R_COUNT]) {
        cpu_state.regs[0].w = r[H_R_AX];
        cpu_state.regs[3].w = r[H_R_BX];
        cpu_state.regs[1].w = r[H_R_CX];
        cpu_state.regs[2].w = r[H_R_DX];
        cpu_state.regs[4].w = r[H_R_SP];
        cpu_state.regs[5].w = r[H_R_BP];
        cpu_state.regs[6].w = r[H_R_SI];
        cpu_state.regs[7].w = r[H_R_DI];

        loadseg(r[H_R_SS], &cpu_state.seg_ss);
        loadseg(r[H_R_DS], &cpu_state.seg_ds);
        loadseg(r[H_R_ES], &cpu_state.seg_es);
        loadcs(r[H_R_CS]);

        cpu_state.pc = r[H_R_IP];
        cpu_state.flags = r[H_R_FLAGS];

        /* Vidange du pipeline : la file est reconstruite depuis la RAM à cs:ip.
         * C'est ce que fait tout transfert de contrôle (808x.c:229-255). */
        FETCHCLEAR();
}

void h_getregs(uint16_t r[H_R_COUNT]) {
        r[H_R_AX] = cpu_state.regs[0].w;
        r[H_R_BX] = cpu_state.regs[3].w;
        r[H_R_CX] = cpu_state.regs[1].w;
        r[H_R_DX] = cpu_state.regs[2].w;
        r[H_R_SP] = cpu_state.regs[4].w;
        r[H_R_BP] = cpu_state.regs[5].w;
        r[H_R_SI] = cpu_state.regs[6].w;
        r[H_R_DI] = cpu_state.regs[7].w;
        r[H_R_CS] = cpu_state.seg_cs.seg;
        r[H_R_SS] = cpu_state.seg_ss.seg;
        r[H_R_DS] = cpu_state.seg_ds.seg;
        r[H_R_ES] = cpu_state.seg_es.seg;
        r[H_R_IP] = (uint16_t)cpu_state.pc;
        r[H_R_FLAGS] = cpu_state.flags;
}

/* Exécute exactement une instruction et rend les cycles consommés.
 *
 * execx86() est `cycles += cycs; while (cycles > 0) { ... }` (808x.c:1222). On
 * pose donc le budget à 1 : le corps s'exécute une fois, puis `cycles` devient
 * négatif ou nul (tout opcode coûte au moins un cycle) et la boucle sort.
 *
 * Poser `cycles = 1` plutôt que d'accumuler est sans effet sur la fidélité : la
 * comptabilité interne est entièrement relative (`cycdiff = cycles` au sommet de
 * boucle, puis tout se mesure en `cycdiff - cycles`). La dette réelle entre
 * instructions est portée par `nextcyc`, qu'on ne touche pas. */
int h_step(void) {
        cpu_state._cycles = 1;
        execx86(0);
        h_ins_count++;
        return 1 - cpu_state._cycles;
}

int h_run(int cycs) {
        uint64_t before = ins;
        cpu_state._cycles = 0;
        execx86(cycs);
        h_ins_count += (uint64_t)(ins - before);
        return cycs - cpu_state._cycles;
}

void h_getstate(h_state *out) {
        memset(out, 0, sizeof(*out));

        for (int i = 0; i < 8; i++)
                out->regs[i] = cpu_state.regs[i].l;

        const x86seg *segs[H_SEG_COUNT] = { &cpu_state.seg_cs, &cpu_state.seg_ds, &cpu_state.seg_es,
                                            &cpu_state.seg_ss, &cpu_state.seg_fs, &cpu_state.seg_gs };
        for (int i = 0; i < H_SEG_COUNT; i++) {
                out->seg_sel[i] = segs[i]->seg;
                out->seg_base[i] = segs[i]->base;
        }

        out->ea_seg_idx = -1;
        for (int i = 0; i < H_SEG_COUNT; i++)
                if (cpu_state.ea_seg == segs[i])
                        out->ea_seg_idx = i;

        out->flags = cpu_state.flags;
        out->eflags = cpu_state.eflags;
        out->pc = cpu_state.pc;
        out->oldpc = cpu_state.oldpc;
        out->eaaddr = cpu_state.eaaddr;
        out->ssegs = cpu_state.ssegs;
        out->abrt = cpu_state.abrt;

        out->cycles = cpu_state._cycles;
        out->tsc = tsc;
        out->tsc_frac = tsc_frac;
        out->memcycs = memcycs;
        out->fetchcycles = fetchcycles;
        out->fetchclocks = fetchclocks;
        out->nextcyc = nextcyc;
        out->cycdiff = cycdiff;
        out->current_diff = current_diff;
        out->prefetchw = prefetchw;
        out->prefetchpc = prefetchpc;
        memcpy(out->prefetchqueue, prefetchqueue, 6);

        out->noint = noint;
        out->inhlt = inhlt;
        out->takeint = takeint;

        out->n_inb = h_n_inb;
        out->n_outb = h_n_outb;
        out->n_picint = h_n_picint;
        out->n_picinterrupt = h_n_picinterrupt;
        out->n_timer_process = h_n_timer_process;
        out->n_readmembl = h_n_readmembl;
        out->n_writemembl = h_n_writemembl;
        out->n_readmemwl = h_n_readmemwl;
        out->n_writememwl = h_n_writememwl;
        out->n_fatal = h_n_fatal;

        out->ins = h_ins_count;
}

/* FNV-1a 64 bits sur la RAM entière. Sert à diffé rer la mémoire sans transférer
 * 1 Mo par instruction ; en cas de divergence, le C# compare octet par octet via
 * h_read pour localiser. */
uint64_t h_ram_hash(void) {
        uint64_t hash = 1469598103934665603ULL;
        for (uint32_t i = 0; i < H_RAM_SIZE; i++) {
                hash ^= ram[i];
                hash *= 1099511628211ULL;
        }
        return hash;
}
