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
#include <stdlib.h>
#include <stdio.h>
#include <string.h>

#include "harness.h"

/* Le cœur lui-même. Chemin explicite plutôt qu'un -I : on veut que la ligne dise
 * ce qu'elle fait. Doit venir après harness.h et avant tout code ci-dessous. */
#include "../../pcem-dev/src/cpu/808x.c"

/* En-têtes de la carte mère, pour h_boot(). Après 808x.c : ils dépendent des
 * types que celui-ci a déjà tirés. */
#include "device.h"
#include "io.h"
#include "dma.h"
#include "pit.h"
#include "model.h"
#include "fdc.h"
#include "fdd.h"
#include "disc.h"
#include "disc_img.h"

void keyboard_xt_init(void);   /* déclaré dans models/model.c chez PCem */

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

/* L'oracle lie le vrai src/video/ depuis M4.2. On declare plutot que d'inclure
 * video.h/vid_cga.h : ces en-tetes referencent mem_mapping_t, pc_timer_t et
 * device_t, que seul l'include de 808x.c ci-dessus a fait entrer. */
extern device_t cga_device;
void initvideo(void);
extern void h_set_verbose(int v);
extern void h_set_roms_path(const char *p);

/* Mappage plat : 1 Mo de RAM sur tout l'espace d'adressage, à travers le VRAI
 * mem.c de PCem. Le fuzzer CPU travaille donc désormais avec les vraies
 * mem_mapping_t, le vrai remplissage de readlookup2 et sa facturation à
 * `cycles -= 9` (mem.c:378) — ce que les stubs de M1 ne faisaient pas. */
static mem_mapping_t h_flat_mapping;
static int h_mem_inited = 0;

/* --- marge de 4 octets sur `ram` : l'oracle doit etre une reference ---------
 *
 * PCem alloue `ram` a EXACTEMENT mem_size Ko (mem.c:1344), puis 808x.c:78 lit un
 * uint16_t a `readlookup2[..] + s + a`. Un acces MOT au sommet de l'espace
 * adressable lit donc un octet AU-DELA de l'allocation : du tas adjacent. Mesure
 * sur la meme entree (SS=FFFF, SP=000F, POP CX), trois executions du fuzzer :
 * l'oracle rend 0x0E59, 0x4959, 0xD859. A l'interieur d'un meme processus la
 * valeur est stable, d'ou l'insuffisance d'un test de reproductibilite local.
 *
 * Il n'y a la aucun comportement a transcrire : c'est de l'UB C, et iXtal26 ne
 * peut pas en etre le pendant fidele. On realloue donc `ram` avec quatre octets
 * a zero -- exactement ce que fait mem_alloc() cote C# (mem.cs:706) -- pour que
 * l'oracle reponde la meme chose a chaque execution.
 *
 * DEVIATION assumee, et du HARNAIS seulement : l'arbre vendore n'est pas touche.
 * On reecrit apres coup l'etat global de mem.c, ce que h_flat_map fait deja.
 *
 * Note pour plus tard : un vrai 8088 a 20 lignes d'adresse ferait reboucler
 * 0x100000 sur 0x00000. Ni PCem ni iXtal26 ne le font. C'est a SST de trancher,
 * pas a l'oracle -- ici on ne cherche que la fidelite de la transcription. */
static void h_pad_ram(void) {
        uint32_t n = mem_size * 1024u, c, npages;
        uint8_t *fresh = calloc(n + 4, 1);

        memcpy(fresh, ram, n);
        free(ram);
        ram = fresh;

        /* pages[].mem pointe DANS ram (mem.c:1357) et mem_write_ramb_page ecrit a
         * travers (mem.c:877). Sans ce rebasage, toute ecriture part dans le bloc
         * libere. Meme borne que mem_alloc, y compris ses pages hors-ram. */
        npages = ((mem_size + 384) * 1024u) >> 12;
        for (c = 0; c < npages; c++)
                pages[c].mem = &ram[c << 12];

        resetreadlookup();
}

static void h_flat_map(void) {
        mem_size = 1024; /* 1 Mo : l'espace complet du 8088 */
        if (!h_mem_inited) {
                mem_init();
                h_mem_inited = 1;
        }
        mem_alloc();
        h_pad_ram();
        mem_set_mem_state(0x000000, 0x100000, MEM_READ_INTERNAL | MEM_WRITE_INTERNAL);
        mem_mapping_add(&h_flat_mapping, 0x000000, 0x100000, mem_read_ram, mem_read_ramw, mem_read_raml,
                        mem_write_ram, mem_write_ramw, mem_write_raml, ram, MEM_MAPPING_INTERNAL, NULL);
}

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
        h_flat_map();
        h_stub_counters_reset();
        h_wlog_reset();
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

/* Motif de DEUX octets alternes. Un flux uniforme de prefixe de segment ne
 * retire jamais d'instruction -- ni ici ni sur un vrai 8088 : `goto opcodestart`
 * (808x.c:1589/1664/1739/1798) saute DANS le corps de la boucle, donc la
 * condition `while (cycles > 0)` n'est jamais reevaluee. Alterner le prefixe et
 * un opcode reel exerce le chemin de prefixe et termine. */
void h_fill_ram2(uint8_t a, uint8_t b) {
        uint32_t i;
        for (i = 0; i < H_RAM_SIZE; i++)
                ram[i] = (i & 1) ? b : a;
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

/* -----------------------------------------------------------------------
 * Amorçage de la machine complète, et trace.
 * ----------------------------------------------------------------------- */

static FILE *h_trace_fp = NULL;

int h_trace_open(const char *path) {
        h_trace_close();
        h_trace_fp = fopen(path, "wb");
        return h_trace_fp != NULL;
}

void h_trace_close(void) {
        if (h_trace_fp) {
                fclose(h_trace_fp);
                h_trace_fp = NULL;
        }
}

/* Une ligne d'état par instruction, réduite à 8 octets. On hache ce que le C#
 * peut reproduire exactement : l'état architectural et le temps. */
static void h_trace_note(void) {
        uint64_t h = 1469598103934665603ULL;
#define MIX(v)                                                                                                           \
        do {                                                                                                             \
                uint64_t _x = (uint64_t)(v);                                                                             \
                for (int _i = 0; _i < 8; _i++) {                                                                         \
                        h ^= (_x >> (_i * 8)) & 0xff;                                                                    \
                        h *= 1099511628211ULL;                                                                           \
                }                                                                                                        \
        } while (0)
        MIX(cpu_state.seg_cs.seg);
        MIX(cpu_state.pc);
        for (int i = 0; i < 8; i++)
                MIX(cpu_state.regs[i].w);
        MIX(cpu_state.seg_ds.seg);
        MIX(cpu_state.seg_es.seg);
        MIX(cpu_state.seg_ss.seg);
        MIX(cpu_state.flags);
        MIX(tsc);
#undef MIX
        fwrite(&h, sizeof(h), 1, h_trace_fp);
}

/* Crochets que la couche UI de PCem installe au démarrage (wx-sdl2.c:450) et
 * que device.c appelle sans les tester. Sans UI liée, ils restent NULL et
 * device_speed_changed() saute à l'adresse 0 dès le premier setpitclock().
 * Le C# a le même besoin ; c'est une vraie frontière hôte, pas un détail. */
static void h_noop(void) { }
extern void (*_sound_speed_changed)(void);

int h_boot(const char *romspath) {
        h_set_roms_path(romspath);
        _sound_speed_changed = h_noop;

        /* Pendant de _808x.ResetCounters() : sans ça, un second h_boot() dans le
           même processus repart avec les compteurs du premier. */
        h_stub_counters_reset();
        h_wlog_reset();

        device_init();
        initvideo();          /* pc.c:59 */
        mem_size = 640;

        if (!h_mem_inited) {
                mem_init();
                h_mem_inited = 1;
        }

        if (!loadbios())
                return 0;

        timer_reset();       /* pc.c:276 */
        io_init();           /* pc.c:278 */
        fdc_init();          /* pc.c:279 */
        disc_init();         /* pc.c:280 */
        img_init();          /* pc.c:282 ; fdi_init (pc.c:281) est un stub vide, chargeur FDI non lié */

        /* pc.c:776-777 — loadconfig() pose les types de lecteur AVANT initpc. Le
         * 5150 a des 5,25" double densité : type 1 (fdd.c:44-46), comme pc.cs. */
        fdd_set_type(0, 1);
        fdd_set_type(1, 1);

        /* resetpchard() réduit, miroir de pc.resetpchard() côté C# (pc.c:353) */
        timer_reset();
        device_close_all();
        device_init();
        io_init();
        mem_alloc();
        h_pad_ram();
        fdc_init();                  /* pc.c:365 */
        disc_reset();                /* pc.c:366 */
        disc_load(0, discfns[0]);    /* pc.c:367 */
        disc_load(1, discfns[1]);    /* pc.c:368 */

        /* model_init() -> xt_init() */
        dma_init();
        fdc_add();                   /* model.c:194 */
        pic_init();
        pit_init();
        mem_add_bios();
        pit_set_out_func(&pit, 1, pit_refresh_timer_xt);
        keyboard_xt_init();
        nmi_init();

        /* video_init(), pc.c:~366. On appelle directement device_add(&cga_device)
         * plutot que video_init() : le switch sur romset de video.c:761-914 tombe
         * dans un default qui traverse le registre VIDEO_CARD, lequel n'est pas
         * transcrit cote C#. Video.video.video_init() (video.cs) fait exactement
         * ce meme raccourci, marque // DEVIATION. Les deux cotes ajoutent donc la
         * MEME carte de la MEME facon -- ce qui est tout ce que l'oracle doit
         * garantir. */
        device_add(&cga_device);

        /* pc_reset(), pc.c:176. timer_reset() y est COMMENTÉ (pc.c:178) : l'appeler
           ici invalide (magic = 0) tous les chronomètres que model_init() vient
           d'enregistrer, et la machine tourne sans PIT. setpitclock() appartient
           bien à pc_reset (pc.c:184-187), donc APRÈS pit_init. */
        resetx86();
        fdc_reset();                 /* pc.c:180 */
        pic_reset();
        setpitclock(14318184.0f);

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
        h_ins_count = 0;
        return 1;
}

void h_runpc(void) {
        int cycles_to_run = 4772728 / 100;

        if (!h_trace_fp) {
                cpu_state._cycles = 0;
                execx86(cycles_to_run);
                return;
        }

        /* Mode tracé : une instruction à la fois, pour pouvoir noter l'état
         * après chacune. Le budget total reste celui de la tranche. */
        int budget = cycles_to_run;
        while (budget > 0) {
                cpu_state._cycles = 1;
                execx86(0);
                budget -= (1 - cpu_state._cycles);
                h_trace_note();
                h_ins_count++;
        }
}

/* FNV-1a 64 bits sur la RAM allouée. Sert à différer la mémoire sans transférer
 * 1 Mo par instruction ; en cas de divergence, le C# compare octet par octet via
 * h_read pour localiser.
 *
 * Bornée à mem_size Ko, pas à H_RAM_SIZE. Sur la carte plate de h_reset() les
 * deux valent 1 Mo ; mais après h_boot() la machine a 640 Ko (h_pad_ram alloue
 * mem_size*1024 + 4 octets) et lire 1 Mo sort du bloc — segfault, mesuré par
 * bench.c. Même borne côté C# : Bench.RamHash() hache mem.ram sur mem_size Ko. */
uint64_t h_ram_hash(void) {
        uint64_t hash = 1469598103934665603ULL;
        uint32_t n = (uint32_t)mem_size * 1024u;
        for (uint32_t i = 0; i < n; i++) {
                hash ^= ram[i];
                hash *= 1099511628211ULL;
        }
        return hash;
}

/* --- disquette (M6) --------------------------------------------------------- */

void h_set_discfn(int drive, const char *fn) {
        if (drive < 0 || drive > 1)
                return;
        strncpy(discfns[drive], fn ? fn : "", sizeof(discfns[drive]) - 1);
        discfns[drive][sizeof(discfns[drive]) - 1] = 0;
}

/* Globales non static de disc.c et fdc.c : on les lit, on n'instrumente rien.
   Même ordre que Floppy.fdc_c.Probe() côté C#. */
extern int discint, lastbyte, paramstogo, bit_rate;
extern uint8_t disc_3f7;
extern int motoron, disc_drivesel, curdrive, disc_notfound;
extern int disc_track[2], drive_empty[2], disc_changed[2], writeprot[2];
extern pc_timer_t disc_poll_timer;

void h_disc_probe(uint64_t *out) {
        out[0] = (uint64_t)(int64_t)discint;
        out[1] = disc_3f7;
        out[2] = (uint64_t)(int64_t)lastbyte;
        out[3] = (uint64_t)(int64_t)paramstogo;
        out[4] = (uint64_t)(int64_t)bit_rate;
        out[5] = (uint64_t)(int64_t)motoron;
        out[6] = (uint64_t)(int64_t)disc_drivesel;
        out[7] = (uint64_t)(int64_t)curdrive;
        out[8] = (uint64_t)(int64_t)disc_track[0];
        out[9] = (uint64_t)(int64_t)disc_track[1];
        out[10] = (uint64_t)(int64_t)drive_empty[0];
        out[11] = (uint64_t)(int64_t)drive_empty[1];
        out[12] = (uint64_t)(int64_t)disc_changed[0];
        out[13] = (uint64_t)(int64_t)disc_changed[1];
        out[14] = (uint64_t)(int64_t)writeprot[0];
        out[15] = (uint64_t)(int64_t)writeprot[1];
        out[16] = (uint64_t)(int64_t)disc_notfound;
        out[17] = (uint64_t)(int64_t)readflash;
        out[18] = (uint64_t)(int64_t)disc_poll_timer.enabled;
        out[19] = timer_get_remaining_u64(&disc_poll_timer);
}

/* --- sonde PIT : diff de boot, phase 2 ------------------------------------
   Aucun accesseur n'expose l'état du PIT. pit est une globale de pit.c et le
   harnais est lié avec, donc un extern suffit : on n'instrumente pas le C, on
   le lit. Ordre identique à Models.pit.Probe() côté C#. */
extern PIT pit;
void h_pit_probe(int t, uint64_t *out) {
        out[0] = pit.l[t];
        out[1] = pit.m[t];
        out[2] = (uint64_t)pit.count[t];
        out[3] = pit.rl[t];
        out[4] = (uint64_t)pit.using_timer[t];
        out[5] = (uint64_t)pit.gate[t];
        out[6] = (uint64_t)pit.enabled[t];
        out[7] = (uint64_t)pit.running[t];
        out[8] = (uint64_t)pit.disabled[t];
        out[9] = (uint64_t)pit.thit[t];
        out[10] = (uint64_t)pit.latched[t];
        out[11] = (uint64_t)pit.rereadlatch[t];
        out[12] = (uint64_t)pit.rm[t];
        out[13] = (uint64_t)pit.out[t];
        out[14] = pit.timer[t].enabled;
        out[15] = ((uint64_t)pit.timer[t].ts_integer << 32) | pit.timer[t].ts_frac;
        out[16] = tsc;
        out[17] = PITCONST;
        out[18] = timer_get_remaining_u64(&pit.timer[t]);
}
