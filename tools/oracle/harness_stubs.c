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
#include "video.h"
#include "x86.h"

/* M12 : les types des stubs de disque dur en fin de fichier. minivhd.h tire
 * stdbool et les typedefs MVHDMeta/MVHDGeom/MVHDError ; ramdisk.h le typedef
 * ramdisk_t. Le -I de includes/private/hdd/ les rend accessibles sous ce chemin,
 * exactement comme hdd_file.c les inclut. */
#include "minivhd/minivhd.h"
#include "ramdisk/ramdisk.h"

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

/* Bit turbo du port 0x61 sur les clones XT. Le 5150 n'en a pas. */
void cpu_set_turbo(int turbo) { (void)turbo; }

/* Cassette : pas de lecteur, l'entrée reste basse. */
int cassette_input(void) { return 0; }
void cassette_set_motor(int on) { (void)on; }
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

/* Couche hôte du clavier (plat-keyboard.h) : sans fenêtre, aucune touche
 * n'est enfoncée. keyboard_process() lit pcem_key[] et n'y trouve rien. */
uint8_t pcem_key[272];
int rawinputkey[272];

/* Tandy : EEPROM de configuration, machine hors cible. */
uint8_t tandy_eeprom_read(void) { return 0; }

/* --- haut-parleur : plus de stub, le vrai sound_speaker.c est compilé dans
 * harness.c depuis M9. Les cinq définitions qui étaient ici (was_speaker_enable,
 * ppispeakon, speaker_enable, speaker_gated, gated/speakval/speakon, et un
 * speaker_update() vide) entreraient maintenant en collision avec
 * sound_speaker.c:5-15. Voir l'en-tête « son » de harness.c. */

/* --- vidéo -----------------------------------------------------------------
 *
 * L'oracle lie le VRAI src/video/vid_cga.c depuis M4.2.
 *
 * Ce n'était pas un raffinement. Sans carte CGA, l'espace 0xB8000 de l'oracle
 * est de la RAM ordinaire, alors que celui d'iXtal26 traverse la carte et ses
 * états d'attente : les deux harnais modélisaient des MACHINES DIFFÉRENTES. Le
 * diff d'amorçage l'a sorti à l'instruction 801 677, sur le REP STOSW qui efface
 * l'écran — oracle 81 924 cycles, C# 147 462.
 *
 * src/video/video.c n'est PAS lié : il traîne tout le registre VIDEO_CARD, 90
 * symboles de cartes qu'on n'émule pas. Ce qui suit fournit donc ses globales et
 * ses fonctions de frontière hôte, en miroir de Video/video.cs.
 *
 * DEUX NIVEAUX, à ne pas confondre :
 *
 *   (a) ce qui porte du TEMPS — buffer32 et sa géométrie, hline. cga_poll y
 *       écrit à chaque balayage ; un buffer32 nul planterait, un buffer32 trop
 *       petit corromprait le tas. Alloué pour de vrai, 2048x2048, comme
 *       video.c:1067 et comme video.cs:84-86.
 *
 *   (b) ce qui ne porte que des PIXELS — cgapal, fontdat, le chemin composite.
 *       Stubé à zéro. Le diff d'amorçage compare l'état du CPU, et aucune de ces
 *       tables ne le touche : elles décident de la COULEUR des points, pas du
 *       nombre de cycles. À REPRENDRE avant l'oracle de framebuffer du plan
 *       (§ Vérification 3), qui hashera justement l'index de couleur.
 */

/* Couche hôte, verbatim de wx-ui/wx-sdl2-video.c:49-71. Transcrite UNE fois, en
 * C, et le C# la reprend (video.cs) — c'est la règle de la paire de stubs. */
VIDEO_BITMAP *create_bitmap(int x, int y) {
        VIDEO_BITMAP *b = malloc(sizeof(VIDEO_BITMAP) + (y * sizeof(uint8_t *)));
        int c;
        b->dat = malloc(x * y * 4);
        for (c = 0; c < y; c++) {
                b->line[c] = b->dat + (c * x * 4);
        }
        b->w = x;
        b->h = y;
        return b;
}

void destroy_bitmap(VIDEO_BITMAP *b) {
        free(b->dat);
        free(b);
}

void hline(VIDEO_BITMAP *b, int x1, int y, int x2, int col) {
        if (y < 0 || y >= buffer32->h)
                return;

        for (; x1 < x2; x1++)
                ((uint32_t *)b->line[y])[x1] = col;
}

/* (a) le cadre où cga_poll dessine. */
VIDEO_BITMAP *buffer32 = NULL;
VIDEO_BITMAP *screen = NULL;

void initvideo(void) {
        if (!buffer32)
                buffer32 = create_bitmap(2048, 2048);
        cgapal_rebuild(0 /*DISPLAY_RGB*/, 0);
}

/* Globales de video.c lues ou écrites par vid_cga.c. Valeurs initiales de
 * video.c, reprises telles quelles par video.cs:89-97, 182, 213. */
int egareads = 0, egawrites = 0;
int changeframecount = 2;
int frames = 0;
int fullchange = 0;
int video_res_x = 0, video_res_y = 0, video_bpp = 0;
int xsize = 1, ysize = 1;

/* (b) pixels seulement — voir l'avertissement en tête de section. */
uint32_t cgapal[16];
void cgapal_rebuild(int display_type, int contrast) { (void)display_type; (void)contrast; }
uint8_t fontdat[2048][8];
void cga_comp_init(int revision) { (void)revision; }
void update_cga16_color(uint8_t cgamode) { (void)cgamode; }
void Composite_Process(uint8_t cgamode, uint32_t blend, int border, uint32_t *line) {
        (void)cgamode; (void)blend; (void)border; (void)line;
}

/* Frontière hôte. video.cs les rend par un drapeau et un rendu hors boucle ;
 * ici il n'y a pas d'hôte, donc rien à faire — mais les sites d'appel de
 * vid_cga.c restent intacts des deux côtés, ce qui est le but. */
void video_blit_memtoscreen(int x, int y, int y1, int y2, int w, int h) {
        (void)x; (void)y; (void)y1; (void)y2; (void)w; (void)h;
}
void video_wait_for_buffer(void) { }
void updatewindowsize(int x, int y) { (void)x; (void)y; }
void video_updatetiming(void) { }

/* Chargement des polices : sans rendu, on ne remplit aucune table. Le POST du
 * 5150 n'interroge pas les polices, il écrit dans la VRAM du CGA. */
void loadfont(char *s, fontformat_t format) { (void)s; (void)format; }

/* Interrogation du registre des cartes vidéo. Une seule carte ici, le CGA :
 * ce sont ces trois réponses que le PPI compose en interrupteurs DIP pour le
 * POST, et le C# rend exactement les mêmes (Video/video.cs:191-193). */
int video_is_mda(void) { return 0; }
int video_is_cga(void) { return 1; }
int video_is_ega_vga(void) { return 0; }

/* Toshiba T1000 : touche système, machine hors cible. */
void t1000_syskey(uint8_t andmask, uint8_t ormask, uint8_t xormask) {
        (void)andmask; (void)ormask; (void)xormask;
}

/* --- disquette (M6) --------------------------------------------------------
 *
 * L'oracle lie les VRAIS floppy/fdc.c, floppy/fdd.c, disc/disc.c, disc/disc_img.c
 * et disc/disc_sector.c. Restent à fournir : deux globales de pc.c et cpu.c
 * qu'ils lisent, le chargeur FDI que la table de disc.c:53 référence sans qu'on
 * le lie (disc_fdi.c tire fdi2raw.c — Disc/disc.cs omet la même entrée, marquée),
 * et get_extension, dont config.c n'est pas lié (il tire tout le fichier de
 * configuration). */

int readflash;      /* pc.c:78 — readflash_set() y pose le témoin d'activité disque */
int isa_cycles = 1; /* cpu.c:17 — cpu_set() y copie atclk_div : 1 pour cpus_8088[0] (cpu_tables.c:33) */

void fdi_init(void) { }
void fdi_load(int drive, char *fn) { (void)drive; (void)fn; }
void fdi_close(int drive) { (void)drive; }

/* config.c:416-428, verbatim. PluginApi/config.cs le transcrit de même. */
char *get_extension(char *s) {
        int c = strlen(s) - 1;

        if (c <= 0)
                return s;

        while (c && s[c] != '.')
                c--;

        if (!c)
                return &s[strlen(s)];

        return &s[c + 1];
}

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

/* --- disque dur (M12) -------------------------------------------------------
 *
 * L'oracle lie src/mfm/mfm_xebec.c et src/hdd/hdd_file.c, mais PAS src/hdd/hdd.c :
 * celui-ci traîne les seize HDD_CONTROLLER et leurs quinze device_t (MFM AT, ESDI,
 * IDE, XTIDE, SCSI…). Même arbitrage que src/video/video.c et ses quatre-vingt-dix
 * symboles de cartes, et que src/sound/sound.c a M9. Ce qui suit fournit donc les
 * globales de hdd.c et d'ide.c, en miroir de Disc/hdd.cs cote C#.
 *
 * ide_fn[] est defini par src/ide/ide.c chez PCem, fichier qu'on ne lie pas non
 * plus ; ses six consommateurs re-declarent l'extern localement, mfm_xebec.c:26
 * compris. Il DOIT porter la meme valeur des deux cotes : sinon hdd_load ouvre un
 * fichier d'un cote et pas de l'autre, et xebec_set_switches calcule deux
 * `switches` differents -- divergence des le premier `in 0x322`. */
PcemHDC hdc[7];
char hdd_controller_name[16];
char ide_fn[7][512];

/* logging.c:99 — pclog, error et fatal etaient deja la, pas warning.
 * xebec_set_switches l'appelle sur une geometrie non supportee. */
void warning(const char *format, ...) { }

/* --- les deux branches mortes de hdd_file.c ---------------------------------
 *
 * hdd_file.c aiguille vers trois formats : brut, VHD, ramdisk. Un 5160 avec une
 * image brute n'emprunte que le premier — mais le LIEN, lui, reclame les dix-sept
 * symboles des deux autres, et -Wl,--no-undefined ne pardonne pas.
 *
 * Stuber ici coute dix-sept lignes triviales. Lier minivhd/ et ramdisk/ en
 * couterait 3 996, dont 1 797 (cwalk.c, libxml2_encoding.c) ne servent qu'aux VHD
 * DIFFERENTIELS et a leurs chemins parents UTF-16.
 *
 * Les deux predicats d'aiguillage rendent FAUX, donc aucune des autres fonctions
 * n'est jamais atteinte : ce sont des stubs de lien, pas de comportement. Cote C#,
 * Disc/hdd_file.cs ne porte carrement pas ces branches. */
int mvhd_errno = 0;
bool mvhd_file_is_vhd(FILE *f) { return false; }
MVHDMeta *mvhd_open(const char *path, bool readonly, int *err) { return NULL; }
void mvhd_close(MVHDMeta *vhdm) { }
int mvhd_read_sectors(MVHDMeta *vhdm, uint32_t offset, int num_sectors, void *out_buff) { return 1; }
int mvhd_write_sectors(MVHDMeta *vhdm, uint32_t offset, int num_sectors, void *in_buff) { return 1; }
int mvhd_format_sectors(MVHDMeta *vhdm, uint32_t offset, int num_sectors) { return 1; }
MVHDGeom mvhd_get_geometry(MVHDMeta *vhdm) { MVHDGeom g = {0, 0, 0}; return g; }
const char *mvhd_strerr(MVHDError err) { return ""; }

ramdisk_t *ramdisk_init(void) { return NULL; }
void ramdisk_free(ramdisk_t *ramdisk) { }
int ramdisk_set_size(ramdisk_t *ramdisk, size_t size) { return -1; }
int ramdisk_write(ramdisk_t *ramdisk, const char *buf, size_t size) { return -1; }
int ramdisk_read(ramdisk_t *ramdisk, char *buf, size_t size) { return -1; }
int ramdisk_seek(ramdisk_t *ramdisk, off_t offset, int whence) { return -1; }
int ramdisk_get_cursor_mem(ramdisk_t *ramdisk, char **mem, size_t *size) { return -1; }
int ramdisk_load_file(ramdisk_t *ramdisk, FILE *fp) { return -1; }
