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

#include <limits.h>
#include <stdarg.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#include "ibm.h"
#include "cpu.h"
#include "x87_timings.h"
#include "mem.h"
#include "pic.h"
#include "timer.h"
#include "video.h"
#include "viewer.h"   /* viewer_t — stubs des visionneuses, section vidéo */
#include "models/model.h"
#include "x86.h"
#include "x86_ops.h"   /* OpFn, et les extern des tables dynarec_ops_* */
#include "codegen.h"   /* codegen_timing_t, codeblock_t — pour STUBER, pas pour lier */

/* M12 : les types des stubs de disque dur en fin de fichier. minivhd.h tire
 * stdbool et les typedefs MVHDMeta/MVHDGeom/MVHDError ; ramdisk.h le typedef
 * ramdisk_t. Le -I de includes/private/hdd/ les rend accessibles sous ce chemin,
 * exactement comme hdd_file.c les inclut. */
#include "minivhd/minivhd.h"
#include "ramdisk/ramdisk.h"
#include "ide.h"          /* G5.0 */
#include "ide_atapi.h"
#include "scsi.h"
#include "scsi_cd.h"
#include "scsi_zip.h"

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
        /* continue de compter au-dela, pour que le depassement se voie ; sature a INT_MAX
         * (audit D1) : enroule, h_wlog_n devenait negatif et passait le test ci-dessus. */
        if (h_wlog_n != INT_MAX)
                h_wlog_n++;
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

/* DEPUIS LE JALON 286, src/cpu/cpu.c EST LIÉ, et il DÉFINIT lui-même les quarante-deux
 * globales que ce fichier posait à la main : is386, is486, hasfpu, fpu_type,
 * cpu_16bitbus, cpu_iscyrix, cpu_cyrix_alignment, cpu_cache_*, cpu_block_end,
 * cpu_busspeed, cpu_prefetch_*, timing_misaligned, tsc, isa_cycles, les vingt
 * timing_* de mode protégé, et les fonctions cpu_get_speed / cpu_set_edx /
 * cpu_set_turbo / cpu_update_waitstates.
 *
 * Elles sont donc RETIRÉES d'ici : les garder donnait quarante-deux « multiple
 * definition » au lien. Le commentaire qui expliquait pourquoi cpu.c n'était pas lié
 * — « cpu_set() référence toutes les tables d'opcodes et les modules codegen_timing_*,
 * ce qui ramène le dynarec » — reste vrai, et c'est pourquoi ces modules sont stubés
 * plus bas.
 *
 * RECTIFICATION du jalon 286 (A2.0). Ce commentaire affirmait, comme le message du
 * commit 62288a6, que lier cpu.c faisait prendre aux vingt timing_* de mode protégé
 * « leurs vraies valeurs, posées par cpu_set() depuis cpus_286[] ». C'EST FAUX, et
 * mesuré tel : une sonde qui les imprime après h_reset() rend VINGT ZÉROS. Lier une
 * unité fournit les symboles ; les valeurs, elles, sont assignées par cpu_set()
 * (cpu.c:323-353), et cpu_set() ne tourne JAMAIS ici — models[] est un tableau de
 * pointeurs nuls, c'est toute la raison des quatre interpositions ci-dessous.
 *
 * Aucune conséquence sur le palier (a) : 808x.c ne lit aucun de ces vingt symboles
 * (zéro occurrence), donc les cinq chiffres de régression restaient justes. La
 * conséquence est pour la SUITE : x86seg.c et tous les x86_ops_*.h les lisent, et à
 * zéro des deux côtés ils auraient produit exactement l'accord vide que le commentaire
 * se félicitait d'avoir évité. D'où h_cpu_config_286(), qui les pose à la main.
 *
 * DEUX VALEURS NE SONT PAS DES DÉFAUTS et doivent être ré-affirmées : cpu_busspeed et
 * isa_cycles. cpu.c les définit à zéro ; le harnais les posait à 4 772 728 et à 1. Les
 * laisser tomber déplacerait les cinq chiffres de régression du 8088 EN SILENCE. D'où
 * h_cpu_config_8088().
 *
 * M16 — « cpu_set() ne tourne JAMAIS ici » a cessé d'être vrai pour h_boot() : models[]
 * est peuplé (plus bas) et h_boot() appelle le vrai cpu_set() de PCem, avant mem_alloc
 * comme pc.c:363. Et depuis l'étape 6 h_reset() — fuzz, sst, selftest, core286-check —
 * aussi, sur la machine que son coeur désigne : les h_cpu_config_* ont disparu. */

int AMSTRAD = 0, AT = 0, PCI = 0, TANDY = 0, MCA = 0;
int insc = 0;
int amstrad_latch = 0;
int romset = 0; /* ROM_IBMPC */


/* h_cpu_config_8088() et h_cpu_config_286() ont vécu ici de M0 à M16 : recopies à la main
 * de ce que cpu_set() pose pour cpus_8088[0] et cpus_286[0], parce que cpu_set() ne
 * tournait pas — models[] était nul. Depuis M16 (étape 6), h_boot() ET h_reset() font
 * tourner le vrai, et l'empreinte CPU (cpu-config-check) le confronte au C#. Leur
 * histoire — les vingt timing_* à zéro (A2.0), les tables FPU nulles (A11),
 * cpu_16bitbus et rammask — est dans git log et VERIFICATION.md. */

/* __wrap_cpu_update_waitstates() a vécu ici jusqu'à M16 (étape 6) : un no-op qui
 * interposait les appels de resetx86 et softresetx86 (808x.c:676, :720), le vrai
 * déréférençant models[], nul. models[] est peuplé par h_boot et h_reset, et le vrai
 * tourne — au-delà de 8 MHz, c'est lui qui remet le préfetch au coût de la RAM. */

/* Les trois autres FONCTIONS que cpu.c définit et qui dépendent de models[] ou de
 * cpu_s. Corps repris À L'IDENTIQUE de ce que harness_stubs.c posait au palier (a) :
 * c'est le comportement mesuré des cinq chiffres de régression.
 *
 * M16 : models[] est peuplé sur le chemin de h_boot, donc « elles plantent » n'est plus
 * le motif. cpu_get_speed et cpu_set_turbo ne sont plus enveloppées depuis le levier A
 * (étape 4) : ce sont les vraies. Reste cpu_set_edx, DÉVIATION déclarée des deux
 * côtés : DX est gardé au reset là où PCem pose edx_reset (keyboard_at.cs, x86seg.cs).
 *
 * Sur les quarante-deux symboles retirés, ces quatre sont les seuls à être des
 * fonctions ; les trente-huit autres sont des données, que cpu.c fournit aux mêmes
 * valeurs — sauf cpu_busspeed et isa_cycles, que le vrai cpu_set() pose désormais. */
void __wrap_cpu_set_edx(void) { }


/* Cassette : pas de lecteur, l'entrée reste basse. */
int cassette_input(void) { return 0; }
void cassette_set_motor(int on) { (void)on; }

/* --- dynarec : non porté --------------------------------------------------- */

/* Les soixante-et-un symboles que lier 386_dynarec.c réclame et que src/codegen/ —
 * 39 307 lignes vives — fournirait. Mesuré au lien : les stuber en coûte 61 ; NE PAS
 * lier 386_dynarec.c en coûterait 98, parce que les tables ops_286 / ops_386 / ops_REPE
 * y sont instanciées et deviendraient manquantes à leur tour (386.c n'inclut PAS
 * 386_ops.h — zéro occurrence).
 *
 * Aucun n'est jamais atteint : cpu_use_dynarec vaut 0, et le 286 comme le 386 de PCem
 * sont interpréteurs PAR CONCEPTION — cpu_flags == 0 dans cpu_tables.c,
 * CPU_SUPPORTS_DYNAREC n'apparaît qu'au 486. 386.c neutralise même CPU_BLOCK_END() en
 * le redéfinissant à vide (386.c:17-18).
 *
 * Les tables dynarec_ops_* restent à NULL : x86_setopcodes les copie dans
 * x86_dynarec_opcodes, que seul exec386_dynarec déréférence. */

OpFn dynarec_ops_286[1024];
OpFn dynarec_ops_286_0f[1024];
OpFn dynarec_ops_386[1024];
OpFn dynarec_ops_386_0f[1024];
OpFn dynarec_ops_winchip_0f[1024];
OpFn dynarec_ops_winchip2_0f[1024];
OpFn dynarec_ops_pentium_0f[1024];
OpFn dynarec_ops_pentiummmx_0f[1024];
OpFn dynarec_ops_pentiumpro_0f[1024];
OpFn dynarec_ops_pentium2_0f[1024];
OpFn dynarec_ops_c6x86_0f[1024];
OpFn dynarec_ops_c6x86mx_0f[1024];
OpFn dynarec_ops_fpu_d8_a16[32];
OpFn dynarec_ops_fpu_d8_a32[32];
OpFn dynarec_ops_fpu_d9_a16[256];
OpFn dynarec_ops_fpu_d9_a32[256];
OpFn dynarec_ops_fpu_da_a16[256];
OpFn dynarec_ops_fpu_da_a32[256];
OpFn dynarec_ops_fpu_db_a16[256];
OpFn dynarec_ops_fpu_db_a32[256];
OpFn dynarec_ops_fpu_dc_a16[32];
OpFn dynarec_ops_fpu_dc_a32[32];
OpFn dynarec_ops_fpu_dd_a16[256];
OpFn dynarec_ops_fpu_dd_a32[256];
OpFn dynarec_ops_fpu_de_a16[256];
OpFn dynarec_ops_fpu_de_a32[256];
OpFn dynarec_ops_fpu_df_a16[256];
OpFn dynarec_ops_fpu_df_a32[256];
OpFn dynarec_ops_nofpu_a16[256];
OpFn dynarec_ops_nofpu_a32[256];
OpFn dynarec_ops_fpu_686_da_a16[256];
OpFn dynarec_ops_fpu_686_da_a32[256];
OpFn dynarec_ops_fpu_686_db_a16[256];
OpFn dynarec_ops_fpu_686_db_a32[256];
OpFn dynarec_ops_fpu_686_df_a16[256];
OpFn dynarec_ops_fpu_686_df_a32[256];
OpFn dynarec_ops_REPE[1024];
OpFn dynarec_ops_REPNE[1024];
OpFn dynarec_ops_3DNOW[256];

/* Les modules de temps des UC au-dessus du 386 — cpu_set() les référence pour les
 * 486 et suivants. Jamais lus ici. */
codegen_timing_t codegen_timing_486;
codegen_timing_t codegen_timing_686;
codegen_timing_t codegen_timing_cyrixiii;
codegen_timing_t codegen_timing_k6;
codegen_timing_t codegen_timing_p6;
codegen_timing_t codegen_timing_pentium;
codegen_timing_t codegen_timing_winchip;
codegen_timing_t codegen_timing_winchip2;

/* Une FONCTION, pas un module : cpu_set() l'appelle pour chaque famille d'UC. */
void codegen_timing_set(codegen_timing_t *timing) { (void)timing; }

uint32_t codegen_endpc = 0;
int codegen_flags_changed = 0;
codeblock_t *codeblock = 0;
uint16_t *codeblock_hash = 0;

/* PCI : aucune machine du dépôt n'en a. */
int pci_burst_time = 0, pci_nonburst_time = 0;

void codegen_block_init(uint32_t phys_addr) { (void)phys_addr; }
void codegen_block_start_recompile(codeblock_t *block) { (void)block; }
void codegen_block_end_recompile(codeblock_t *block) { (void)block; }
void codegen_block_end(void) { }
void codegen_block_remove(void) { }
void codegen_check_flush(struct page_t *page, uint64_t mask, uint32_t phys_addr) {
        (void)page; (void)mask; (void)phys_addr;
}
void codegen_generate_call(uint8_t opcode, OpFn op, uint32_t fetchdat, uint32_t new_pc,
                           uint32_t old_pc) {
        (void)opcode; (void)op; (void)fetchdat; (void)new_pc; (void)old_pc;
}

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

/* video.c:559 — lue par svga_render_4bpp_* (vid_svga_render.c:372-424). Remplie par
 * initvideo, comme video.c:1076-1089 le fait. */
uint8_t edatlookup[4][4];

/* video.c:546 — lues par svga_render_15bpp_* et _16bpp_*. NULLES jusqu'à M19 : la VGA
 * ne quitte pas bpp = 8. Le RAMDAC TKD8001 de la Trident 8900D pose 15, 16 ou 24
 * (vid_tkd8001_ramdac.c:20-29), et initvideo les remplit donc comme video.c. */
uint32_t *video_15to32 = NULL, *video_16to32 = NULL;

void initvideo(void) {
        int c, d;

        if (!buffer32)
                buffer32 = create_bitmap(2048, 2048);
        /* DEVIATION: video.c:1067 alloue un buffer32 NEUF à chaque initvideo ; ici il
         * est alloué une fois et REMIS À ZÉRO, comme video.cs le fait (Array.Clear).
         * Sans ce memset, la phase 2 du boot-diff hériterait des pixels de la phase 1,
         * et la comparaison de framebuffer de fin de course — le seul oracle des pixels
         * de la VGA — comparerait deux histoires différentes. Neutre pour la CGA :
         * cgapal est stubé à zéro, donc cga_poll n'y écrit que des zéros. */
        memset(buffer32->dat, 0, 2048 * 2048 * 4);

        /* omitted: rotatevga[] (video.c:1069-1075) — EGA seule ; vid_svga.c porte sa
         * propre svga_rotate. */
        for (c = 0; c < 4; c++) {
                for (d = 0; d < 4; d++) {
                        edatlookup[c][d] = 0;
                        if (c & 1)
                                edatlookup[c][d] |= 1;
                        if (d & 1)
                                edatlookup[c][d] |= 2;
                        if (c & 2)
                                edatlookup[c][d] |= 0x10;
                        if (d & 2)
                                edatlookup[c][d] |= 0x20;
                }
        }
        /* video.c:1091-1097. Allouées une fois : initvideo tourne à chaque h_boot. */
        if (!video_15to32)
                video_15to32 = malloc(4 * 65536);
        for (c = 0; c < 65536; c++)
                video_15to32[c] = ((c & 31) << 3) | (((c >> 5) & 31) << 11) | (((c >> 10) & 31) << 19);

        if (!video_16to32)
                video_16to32 = malloc(4 * 65536);
        for (c = 0; c < 65536; c++)
                video_16to32[c] = ((c & 31) << 3) | (((c >> 5) & 63) << 10) | (((c >> 11) & 31) << 19);
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
/* video.c:925-926 — lues par svga_render_text_80_ksc5601 SEULEMENT, rendu des cartes
 * coréennes que la VGA n'installe jamais (svga_recalctimings ne le choisit pas).
 * Définies pour l'édition de liens, à zéro, et jamais lues. */
uint8_t fontdatksc5601[16384][32];
uint8_t fontdatksc5601_user[192][32];
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

/* video.c:594-596 et 598-749 — RÉELLE depuis la VGA, et c'était le piège du jalon.
 *
 * Tant que seule la CGA était liée, ce stub pouvait rester vide : vid_cga.c ne lit
 * aucune des six globales video_timing_*. vid_svga.c les facture à CHAQUE accès —
 * `cycles -= video_timing_write_b` (vid_svga.c:818), `-= video_timing_read_b`
 * (:1068). Laissées à zéro ici pendant que video.cs les calcule, chaque octet écrit
 * en VRAM aurait coûté 8 cycles d'un côté et 0 de l'autre : une dérive de tsc sans un
 * registre de différence, exactement la panne que l'en-tête de ce fichier décrit.
 *
 * Depuis M19, la branche `video_speed == -1` est là, comme dans video.cs : c'est
 * celle que PCem prend, pc.c:665 posant -1 quand la clé est absente. Elle lit la
 * table de la CARTE, et les Trident n'ont pas celle de la ligne 0. video.c n'étant
 * pas lié (90 symboles de cartes), le registre est réduit à h_video_cards[], recopié
 * des entrées de video.c pour les cartes que video.cs enregistre. Le switch (romset)
 * (video.c:607-718) n'a de cas pour aucun des quatre romsets du dépôt. */
enum { VIDEO_ISA = 0, VIDEO_BUS }; /* video.c:62 — local à video.c, pas d'en-tête */
/* DEVIATION de l'oracle : 0 chez PCem (video.c:594), mais loadconfig() l'écrase
 * toujours par la clé, défaut -1 (pc.c:665) ; l'oracle n'a pas de loadconfig. */
int video_speed = -1;
int video_timing[7][4] = {{VIDEO_ISA, 8, 16, 32}, {VIDEO_ISA, 6, 8, 16}, {VIDEO_ISA, 3, 3, 6},
                          {VIDEO_BUS, 4, 8, 16},  {VIDEO_BUS, 4, 5, 10}, {VIDEO_BUS, 3, 3, 4}};
int video_timing_read_b, video_timing_read_w, video_timing_read_l;
int video_timing_write_b, video_timing_write_w, video_timing_write_l;
extern float bus_timing;

/* video.c:64-67 — locales à video.c, pas d'en-tête. */
#define VIDEO_FLAG_TYPE_CGA 0
#define VIDEO_FLAG_TYPE_MDA 1
#define VIDEO_FLAG_TYPE_SPECIAL 2
#define VIDEO_FLAG_TYPE_MASK 3

/* video.c:96, :177-181, :191 — legacy_id, drapeaux et table de chaque carte. */
typedef struct h_video_card_t {
        int legacy_id;
        int flags;
        video_timings_t timing;
} h_video_card_t;
static const h_video_card_t h_video_cards[] = {
        {GFX_CGA, VIDEO_FLAG_TYPE_CGA, {VIDEO_ISA, 8, 16, 32, 8, 16, 32}},
        {GFX_TVGA, VIDEO_FLAG_TYPE_SPECIAL, {VIDEO_ISA, 3, 3, 6, 8, 8, 12}},
        {GFX_TVGA9000B, VIDEO_FLAG_TYPE_SPECIAL, {VIDEO_ISA, 7, 7, 12, 7, 7, 12}},
        {GFX_VGA, VIDEO_FLAG_TYPE_SPECIAL, {VIDEO_ISA, 8, 16, 32, 8, 16, 32}},
};
static const h_video_card_t *h_video_card(int card) {
        for (unsigned c = 0; c < sizeof(h_video_cards) / sizeof(h_video_cards[0]); c++)
                if (h_video_cards[c].legacy_id == card)
                        return &h_video_cards[c];
        fatal("h_video_card : gfxcard %i absente de h_video_cards[]\n", card);
        return NULL;
}

void video_updatetiming(void) {
        if (video_speed == -1) {
                const video_timings_t *timing;

                timing = &h_video_card(gfxcard)->timing;

                if (timing->type == VIDEO_ISA) {
                        video_timing_read_b = ISA_CYCLES(timing->read_b);
                        video_timing_read_w = ISA_CYCLES(timing->read_w);
                        video_timing_read_l = ISA_CYCLES(timing->read_l);
                        video_timing_write_b = ISA_CYCLES(timing->write_b);
                        video_timing_write_w = ISA_CYCLES(timing->write_w);
                        video_timing_write_l = ISA_CYCLES(timing->write_l);
                } else {
                        video_timing_read_b = (int)(bus_timing * timing->read_b);
                        video_timing_read_w = (int)(bus_timing * timing->read_w);
                        video_timing_read_l = (int)(bus_timing * timing->read_l);
                        video_timing_write_b = (int)(bus_timing * timing->write_b);
                        video_timing_write_w = (int)(bus_timing * timing->write_w);
                        video_timing_write_l = (int)(bus_timing * timing->write_l);
                }
        } else if (video_timing[video_speed][0] == VIDEO_ISA) {
                video_timing_read_b = ISA_CYCLES(video_timing[video_speed][1]);
                video_timing_read_w = ISA_CYCLES(video_timing[video_speed][2]);
                video_timing_read_l = ISA_CYCLES(video_timing[video_speed][3]);
                video_timing_write_b = ISA_CYCLES(video_timing[video_speed][1]);
                video_timing_write_w = ISA_CYCLES(video_timing[video_speed][2]);
                video_timing_write_l = ISA_CYCLES(video_timing[video_speed][3]);
        } else {
                video_timing_read_b = (int)(bus_timing * video_timing[video_speed][1]);
                video_timing_read_w = (int)(bus_timing * video_timing[video_speed][2]);
                video_timing_read_l = (int)(bus_timing * video_timing[video_speed][3]);
                video_timing_write_b = (int)(bus_timing * video_timing[video_speed][1]);
                video_timing_write_w = (int)(bus_timing * video_timing[video_speed][2]);
                video_timing_write_l = (int)(bus_timing * video_timing[video_speed][3]);
        }
        if (cpu_16bitbus) {
                video_timing_read_l = video_timing_read_w * 2;
                video_timing_write_l = video_timing_write_w * 2;
        }
}

/* pc.c:87 — lu par svga_doblit (vid_svga.c:1453, :1466). Défaut de la configuration
 * globale (pc.c:622), que ce dépôt ne lit pas. */
int vid_resize = 0;

/* pc.c:89 — svga_read/svga_write l'incrémentent à chaque accès ; personne ne le lit
 * dans l'oracle. */
int cycles_lost = 0;

/* Les visionneuses de débogage de l'UI (wx-ui/viewer.h) : vid_svga.c les enregistre
 * (svga_init, :796-799) et les rafraîchit (svga_poll, :628-633). Sorties pures. */
viewer_t viewer_font, viewer_palette, viewer_palette_16, viewer_vram;
void viewer_add(char *title, viewer_t *viewer, void *p) { (void)title; (void)viewer; (void)p; }
void viewer_update(viewer_t *viewer, void *p) { (void)viewer; (void)p; }

/* Chargement des polices : sans rendu, on ne remplit aucune table. Le POST du
 * 5150 n'interroge pas les polices, il écrit dans la VRAM du CGA. */
void loadfont(char *s, fontformat_t format) { (void)s; (void)format; }

/* Interrogation du registre des cartes vidéo, video.c:409-540. Deux cartes depuis la
 * VGA, choisies par gfxcard (pc.c:77), que h_set_gfxcard pose AVANT h_boot. Ce sont
 * ces réponses que le PPI du XT compose en interrupteurs DIP, et le C# rend les mêmes
 * (Video/video.cs).
 *
 * Réduites : le switch sur romset qui ouvre chacune (video.c:410-452, :456-500,
 * :504-538) n'a de cas pour aucun des quatre romsets du dépôt, donc on tombe toujours
 * dans la lecture des drapeaux de la carte — VIDEO_FLAG_TYPE_CGA pour v_cga
 * (video.c:96), VIDEO_FLAG_TYPE_SPECIAL pour v_vga (video.c:191). */
int gfxcard = 0; /* pc.c:77 ; GFX_CGA = 0 (ibm.h:276) */

/* M19 : lues sur les drapeaux de h_video_cards[], comme video.c:499, :539 et
 * video.cs:362-375 — plus sur gfxcard. Avec trois cartes SPECIAL, `gfxcard == GFX_VGA`
 * rendait 0 pour une Trident là où le C# rend 1 : les interrupteurs du PPI du XT
 * divergeaient avant la première instruction. */
int video_is_mda(void) { return (h_video_card(gfxcard)->flags & VIDEO_FLAG_TYPE_MASK) == VIDEO_FLAG_TYPE_MDA; }
int video_is_cga(void) { return (h_video_card(gfxcard)->flags & VIDEO_FLAG_TYPE_MASK) == VIDEO_FLAG_TYPE_CGA; }
int video_is_ega_vga(void) { return (h_video_card(gfxcard)->flags & VIDEO_FLAG_TYPE_MASK) == VIDEO_FLAG_TYPE_SPECIAL; }

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
/* G5.0 — ide_fn est desormais defini par src/ide/ide.c, lie. */

/* --- G5.0 : ce que ide.c reclame hors du disque dur ATA ----------------------
 *
 * hdd_controller_current_is_ide (hdd.c:125) consulte le registre des seize
 * HDD_CONTROLLER, qu'on ne lie pas. Des cartes que l'oracle peut monter, seule « ide »
 * a is_ide (hdd.c:155) ; xtide, xtide_at, xtide_ps1 l'ont aussi mais ne sont pas liees.
 *
 * ATAPI est hors G5 (PLAN-G5.md, decision n° 1) : ni ide_atapi.c, ni scsi_cd.c, ni
 * scsi_zip.c. Le harnais pose cdrom_channel = -1 — la configuration PCem ou le canal 2
 * est declare « Hard drive » (wx-config.c:891). Les atapi_* ne sont alors atteints que
 * si l'invite envoie WIN_PACKETCMD a un disque dur (ide.c:311, :735) : arret bruyant,
 * jamais un vert muet. Le C# fait de meme.
 *
 * `atapi` est le pilote CD de l'hote ; PCem le pose TOUJOURS, cdrom_null_open a
 * defaut de lecteur (pc.c:293, cdrom-null.c:50-51). Le rappel de reset appelle son
 * stop() meme pour un lecteur absent (ide.c:796-812) : c'est le null_stop vide de
 * cdrom-null.c:19, reproduit ici — pas un pointeur nul. */
int hdd_controller_current_is_ide(void) { return !strcmp(hdd_controller_name, "ide"); }

static void h_null_stop(void) { }
static ATAPI h_null_atapi = { .stop = h_null_stop };
ATAPI *atapi = &h_null_atapi;

scsi_device_t scsi_cd;
scsi_device_t scsi_zip;
void scsi_bus_atapi_init(scsi_bus_t *bus, scsi_device_t *device, int id, atapi_device_t *atapi_dev) {
        fatal("ATAPI non lie a l'oracle (G5) : scsi_bus_atapi_init\n");
}
void atapi_data_write(atapi_device_t *atapi_dev, uint16_t val) { fatal("ATAPI non lie a l'oracle (G5) : atapi_data_write\n"); }
uint16_t atapi_data_read(atapi_device_t *atapi_dev) { fatal("ATAPI non lie a l'oracle (G5) : atapi_data_read\n"); return 0; }
void atapi_command_start(atapi_device_t *a, uint8_t features) { fatal("ATAPI non lie a l'oracle (G5) : atapi_command_start\n"); }
uint8_t atapi_read_iir(atapi_device_t *atapi_dev) { fatal("ATAPI non lie a l'oracle (G5) : atapi_read_iir\n"); return 0; }
uint8_t atapi_read_drq(atapi_device_t *atapi_dev) { fatal("ATAPI non lie a l'oracle (G5) : atapi_read_drq\n"); return 0; }
void atapi_process_packet(atapi_device_t *atapi_dev) { fatal("ATAPI non lie a l'oracle (G5) : atapi_process_packet\n"); }
void atapi_reset(atapi_device_t *atapi_dev) { fatal("ATAPI non lie a l'oracle (G5) : atapi_reset\n"); }

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

/* ---------------------------------------------------------------------------
 * B2 — LES VINGT SYMBOLES QUE nvr.c TRAINE AVEC LUI.
 *
 * nvr.c ne porte pas que le MC146818 de l'AT : il sert aussi de point d'entree
 * a la sauvegarde d'etat de six AUTRES machines — Toshiba T1000, T1200, T3100e,
 * la Tandy tc8521, la Xi8088. Lier nvr.c amene donc leurs crochets, dont aucun
 * n'est atteint sur un AT : nvr_load() et nvr_save() n'appellent ces fonctions
 * que si `romset` designe la machine correspondante.
 *
 * Ce sont de VRAIS stubs, pas des transcriptions : ils existent pour que
 * -Wl,--no-undefined passe, et ils ECHOUENT s'ils sont appeles — un appel
 * signifierait que le harnais monte une machine qu'il ne devrait pas.
 * ------------------------------------------------------------------------- */

char nvr_path[512];
char nvr_default_path[512];
char config_name[256];
int mouse_scan;

#define H_STUB_NVR(nom)                                                                                                                  void nom(void) { h_fatal_stub(#nom); }

static void h_fatal_stub(const char *nom) {
        fprintf(stderr, "iXtal26 oracle FATAL: %s appele — le harnais monte une machine "
                        "qui n'est pas l'AT ni le XT\n", nom);
        abort();
}

H_STUB_NVR(t1000_configsys_loadnvr)
H_STUB_NVR(t1000_configsys_savenvr)
H_STUB_NVR(t1000_emsboard_loadnvr)
H_STUB_NVR(t1000_emsboard_savenvr)
H_STUB_NVR(t1200_state_loadnvr)
H_STUB_NVR(t1200_state_savenvr)
H_STUB_NVR(tc8521_loadnvr)
H_STUB_NVR(tc8521_savenvr)
H_STUB_NVR(t3100e_config_get)
H_STUB_NVR(t3100e_display_set)
H_STUB_NVR(t3100e_mono_get)
H_STUB_NVR(t3100e_mono_set)
H_STUB_NVR(t3100e_notify_set)
H_STUB_NVR(t3100e_turbo_set)
H_STUB_NVR(xi8088_turbo_get)
H_STUB_NVR(xi8088_turbo_set)

/* ---------------------------------------------------------------------------
 * B2 — models[] CESSE D'ETRE ENTIEREMENT NUL.
 *
 * Il l'etait depuis M0, et sept commentaires de ce fichier s'appuient dessus :
 * cpu_set() le dereference, donc on ne l'appelle jamais, donc h_cpu_config_286()
 * existe. Ca tenait tant que RIEN d'autre ne le lisait.
 *
 * nvr.c le lit. writenvr (nvr.c:201) teste `models[model]->flags & (MODEL_MCA |
 * MODEL_AMSTRAD)` a chaque ecriture dans le CMOS — et le POST de l'AT ecrit dans
 * le CMOS avant tout le reste. Mesure, sous gdb :
 *
 *   Program received signal SIGSEGV
 *   #0  writenvr (nvr.c:201)
 *   #1  outb (port=112, val=141)
 *   #2  opOUT_AL_imm
 *   #3  exec386
 *
 * On pose donc UNE entree, celle de l'AT, avec les seuls champs que le harnais
 * fait lire : `flags`. Le reste est a zero, et c'est volontaire — un champ qui
 * compterait un jour doit planter, pas rendre du plausible.
 *
 * CE N'EST PAS UNE TRANSCRIPTION de m_ibmat (model.c:1106-1115) : c'est le
 * MINIMUM que le harnais doit fournir pour que nvr.c tourne, et son en-tete le
 * dit. Le jour ou une autre fonction lira un autre champ, elle lira zero et le
 * defaut se verra — ce qui est preferable a un tableau rempli de valeurs
 * inventees.
 * ------------------------------------------------------------------------- */

/* M16 — LES QUATRE MACHINES, et leur membre `cpu`.
 *
 * Jusqu'à M16 une seule entrée existait, et sans `cpu` : cpu_set() ne tournait jamais,
 * h_cpu_config_286() en tenait lieu. Le vrai cpu_set() de PCem tourne désormais dans
 * h_boot(), et il lit `models[model]->cpu[cpu_manufacturer].cpus[cpu]` — d'où les
 * tables, qui sont celles de cpu_tables.c, LIÉ : ce sont les données de PCem, pas une
 * recopie.
 *
 * DEVIATION de l'ORACLE : models[] est indexé par ROMSET (model = romset dans h_boot),
 * pas par l'ordre d'enregistrement de model_init_builtin (model.c:1625-1746), que le
 * harnais ne lie pas. Les champs sont ceux de model.c:777-778, :782-783, :986-995 et
 * :1106-1115 ; `init` et `device` restent nuls, le harnais inlinant les deux inits.
 * L'IBM AT a sa vraie table, cpus_ibmat, depuis le levier B de M16 (étape 5). */
static MODEL h_model_ibmpc = {
        .name = "[8088] IBM PC",
        .id = ROM_IBMPC,
        .internal_name = "ibmpc",
        .cpu = {{"", cpus_8088}, {"", NULL}, {"", NULL}},
        .flags = MODEL_GFX_NONE,
        .min_ram = 64,
        .max_ram = 640,
        .ram_granularity = 32,
};

static MODEL h_model_ibmxt = {
        .name = "[8088] IBM XT",
        .id = ROM_IBMXT,
        .internal_name = "ibmxt",
        .cpu = {{"", cpus_8088}, {"", NULL}, {"", NULL}},
        .flags = MODEL_GFX_NONE,
        .min_ram = 64,
        .max_ram = 640,
        .ram_granularity = 64,
};

static MODEL h_model_ibmat = {
        .name = "[286] IBM AT",
        .id = ROM_IBMAT,
        .internal_name = "ibmat",
        .cpu = {{"", cpus_ibmat}, {"", NULL}, {"", NULL}},
        .flags = MODEL_GFX_NONE | MODEL_AT,
        .min_ram = 256,
        .max_ram = 15872,
        .ram_granularity = 128,
};

static MODEL h_model_ami286 = {
        .name = "[286] AMI 286 clone",
        .id = ROM_AMI286,
        .internal_name = "ami286",
        .cpu = {{"", cpus_286}, {"", NULL}, {"", NULL}},
        .flags = MODEL_GFX_NONE | MODEL_AT | MODEL_HAS_IDE,
        .min_ram = 512,
        .max_ram = 16384,
        .ram_granularity = 128,
};

/* G2, D0.2 — m_ami386 (model.c:1238-1247), la table Intel SEULE : cpus_Am386SX et
 * cpus_486SLC sont omis ici COMME dans model.cs, pour que les deux cotes lisent la meme
 * table. Son init n'est pas liee : h_boot la refuse. Elle sert a cpu_set() et au
 * fuzzeur du coeur 386 (h_reset, H_CORE_386). */
static MODEL h_model_ami386 = {
        .name = "[386SX] AMI 386SX clone",
        .id = ROM_AMI386SX,
        .internal_name = "ami386",
        .cpu = {{"Intel", cpus_i386SX}, {"", NULL}, {"", NULL}},
        .flags = MODEL_GFX_NONE | MODEL_AT | MODEL_HAS_IDE,
        .min_ram = 512,
        .max_ram = 16384,
        .ram_granularity = 128,
};

/* G3.2 — m_ami386dx (model.c:1340-1349), la table Intel SEULE comme pour l'ami386 :
 * cpus_Am386DX et cpus_486DLC omis des deux cotes. RAM en Mo (AT, granularite < 128 :
 * pc.c:695-700). */
static MODEL h_model_ami386dx = {
        .name = "[386DX] AMI 386DX clone",
        .id = ROM_AMI386DX_OPTI495,
        .internal_name = "ami386dx",
        .cpu = {{"Intel", cpus_i386DX}, {"", NULL}, {"", NULL}},
        .flags = MODEL_GFX_NONE | MODEL_AT | MODEL_HAS_IDE,
        .min_ram = 1,
        .max_ram = 256,
        .ram_granularity = 1,
};

void h_models_init(void) {
        models[ROM_IBMPC] = &h_model_ibmpc;
        models[ROM_IBMXT] = &h_model_ibmxt;
        models[ROM_IBMAT] = &h_model_ibmat;
        models[ROM_AMI286] = &h_model_ami286;
        models[ROM_AMI386SX] = &h_model_ami386;
        models[ROM_AMI386DX_OPTI495] = &h_model_ami386dx;
}

/* --- processeur (M16) -------------------------------------------------------
 *
 * Le fabricant et l'INDICE dans la table de la machine, pendant des clés
 * `cpu_manufacturer` et `cpu` (pc.c:653-654). Poussés par h_set_cpu AVANT h_boot,
 * comme le romset : une lecture de la configuration côté C#, deux poussées. Écrits
 * EXPLICITEMENT dans les globales de cpu.c par h_boot, parce que cpu.c:82 initialise
 * `cpu` à 3 — un 8088/10 ou un 286/12 en silence. */
int h_cpu_manu = 0;
int h_cpu_index = 0;

void h_set_cpu(int manu, int n) {
        h_cpu_manu = manu;
        h_cpu_index = n;
}

/* L'indice poussé est-il dans la table de la machine ? PCem ne le vérifie pas
 * (cpu.c:171-175 ne teste que le pointeur de table) ; le C# refuse par check_cpu, et
 * h_boot refuse ici de la même façon plutôt que de lire hors du tableau. */
int h_cpu_table_ok(void) {
        MODEL *m = models[romset];
        CPU *t;
        int n = 0;

        if (!m || h_cpu_manu < 0 || h_cpu_manu >= 5 || !(t = m->cpu[h_cpu_manu].cpus))
                return 0;
        while (t[n].cpu_type != -1)
                n++;
        return h_cpu_index >= 0 && h_cpu_index < n;
}

/* La même borne pour h_reset(), qui choisit sa machine par le coeur et non par romset. */
int h_cpu_index_ok(int rs, int n) {
        MODEL *m = models[rs];
        CPU *t;
        int c = 0;

        if (!m || !(t = m->cpu[0].cpus))
                return 0;
        while (t[c].cpu_type != -1)
                c++;
        return n >= 0 && n < c;
}

/* Le budget d'une tranche, pc.c:473, par la MÊME fonction que h_runpc emploie. Exposé
 * pour que l'outil de diff le CONFRONTE à celui du C# : un budget asymétrique ne se voit
 * pas dans la trace d'un AT, qui diverge avant le contrôle de longueur final. */
int h_slice_budget(void) {
        return cpu_get_speed() / 100;
}

/* L'EMPREINTE CPU — H_CPU_FP_N champs, dans l'ordre exact de CpuFingerprint.Csharp()
 * (tools/iXtal26.Diff). Tout ce que cpu_set() et setpitclock() posent et que le temps
 * de l'invité lit, en scalaires bruts pour que la sonde NOMME le champ divergent.
 *
 * cpu_prefetch_cycles est EXCLU : getpccache() le réécrit à chaque changement de page
 * d'instruction (ROM contre RAM), il ne dit rien de la configuration. */
extern uint64_t PITCONST;

static uint32_t h_fbits(float f) {
        uint32_t u;
        memcpy(&u, &f, sizeof(u));
        return u;
}

void h_cpu_fingerprint(uint64_t *out) {
        const int *timings[] = {
                &timing_rr, &timing_rm, &timing_mr, &timing_mm, &timing_rml, &timing_mrl, &timing_mml,
                &timing_bt, &timing_bnt, &timing_int, &timing_int_rm, &timing_int_v86, &timing_int_pm,
                &timing_int_pm_outer, &timing_iret_rm, &timing_iret_v86, &timing_iret_pm,
                &timing_iret_pm_outer, &timing_call_rm, &timing_call_pm, &timing_call_pm_gate,
                &timing_call_pm_gate_inner, &timing_retf_rm, &timing_retf_pm, &timing_retf_pm_outer,
                &timing_jmp_rm, &timing_jmp_pm, &timing_jmp_pm_gate};
        uint64_t hash = 1469598103934665603ULL;
        int i = 0;

        for (size_t k = 0; k < sizeof(timings) / sizeof(timings[0]); k++) {
                hash ^= (uint32_t)*timings[k];
                hash *= 1099511628211ULL;
        }

        out[i++] = (uint64_t)(int64_t)cpu_get_speed();
        out[i++] = (uint64_t)(int64_t)h_slice_budget();
        out[i++] = (uint64_t)(int64_t)cpu_busspeed;
        out[i++] = (uint64_t)(int64_t)isa_cycles;
        out[i++] = (uint64_t)(int64_t)cpu_16bitbus;
        out[i++] = (uint64_t)(int64_t)is8086;
        out[i++] = (uint64_t)(int64_t)is386;
        out[i++] = (uint64_t)(int64_t)is486;
        out[i++] = (uint64_t)(int64_t)hasfpu;
        out[i++] = (uint64_t)(int64_t)cpu_iscyrix;
        out[i++] = (uint64_t)(int64_t)cpu_prefetch_width;
        out[i++] = (uint64_t)(int64_t)cpu_mem_prefetch_cycles;
        out[i++] = (uint64_t)(int64_t)cpu_rom_prefetch_cycles;
        out[i++] = (uint64_t)(int64_t)cpu_cycles_read;
        out[i++] = (uint64_t)(int64_t)cpu_cycles_read_l;
        out[i++] = (uint64_t)(int64_t)cpu_cycles_write;
        out[i++] = (uint64_t)(int64_t)cpu_cycles_write_l;
        out[i++] = (uint64_t)(int64_t)timing_misaligned;
        out[i++] = hash;
        out[i++] = h_fbits(cpuclock);
        out[i++] = PITCONST;
        out[i++] = CGACONST;
        out[i++] = RTCCONST;
        out[i++] = TIMER_USEC;
        out[i++] = xt_cpu_multi;
        out[i++] = h_fbits(isa_timing);
        out[i++] = h_fbits(bus_timing);
        out[i++] = (uint64_t)(int64_t)video_timing_read_b;
        out[i++] = (uint64_t)(int64_t)video_timing_read_w;
        out[i++] = (uint64_t)(int64_t)video_timing_read_l;
        out[i++] = (uint64_t)(int64_t)video_timing_write_b;
        out[i++] = (uint64_t)(int64_t)video_timing_write_w;
        out[i++] = (uint64_t)(int64_t)video_timing_write_l;
        out[i++] = (uint64_t)(int64_t)mem_size;
        out[i++] = (uint64_t)(int64_t)cpu;
        out[i++] = (uint64_t)(int64_t)cpu_manufacturer;
        out[i++] = (uint64_t)(int64_t)(cpu_s ? cpu_s->rspeed : 0);
        /* G4.1 — le coprocesseur, et la table de temps que cpu_set() a copiée selon lui
         * (cpu.c:1132-1152) : ses 72 int, dans l'ordre du struct, en FNV. */
        out[i++] = (uint64_t)(int64_t)fpu_type;
        {
                const int *x = (const int *)&x87_timings;
                uint64_t h = 1469598103934665603ULL;
                for (size_t k = 0; k < sizeof(x87_timings) / sizeof(int); k++) {
                        h ^= (uint32_t)x[k];
                        h *= 1099511628211ULL;
                }
                out[i++] = h;
        }
        while (i < H_CPU_FP_N)
                out[i++] = 0;
}
