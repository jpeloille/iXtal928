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

/* harness_stubs.c — M16 : les quatre machines de models[], et le processeur poussé. */
void h_models_init(void);
int h_cpu_table_ok(void);
int h_cpu_index_ok(int rs, int n);
extern int h_cpu_manu, h_cpu_index;

/* harness_386.c — le modèle de préfetch du 286, `static` chez PCem. */
int h_prefetch_bytes(void);
int h_prefetch_prefixes(void);
void h_prefetch_reset(void);

/* QUEL CŒUR le harnais exécute. Le sélecteur n'est pas inventé : chez PCem c'est
 * `is386 ? exec386 : AT ? exec386 : execx86` (pc.c:478-487), donc un 286 est très
 * exactement « AT sans is386 ». On garde ce couple comme état de vérité et on ne
 * mémorise ici que le choix, pour que h_reset() le réapplique — resetx86() branche
 * sur AT (808x.c:680) pour le vecteur de reset et rammask.
 *
 * Défaut H_CORE_8088 : un appelant qui ignore h_set_core() obtient le palier (a)
 * inchangé, ce que les cinq chiffres de régression vérifient. */
static int h_core = H_CORE_8088;

void h_set_core(int core) {
        h_core = (core == H_CORE_286 || core == H_CORE_386 || core == H_CORE_486 || core == H_CORE_8086) ? core
                                                                                                    : H_CORE_8088;
}

/* LE 286 ET LE 386 EMPRUNTENT LE MEME exec386 (pc.c:478-487), et c'est ce predicat — pas
 * `h_core == H_CORE_286` — qui aiguille. G2, D0.2 : les dix sites qui testaient le 286
 * seul, relevés par grep, le lisent tous ; un seul oublié renvoyait le 386 vers
 * execx86 sans rien dire. Seul le CHOIX DE LA MACHINE distingue encore les deux. */
static int h_exec386(void) { return h_core == H_CORE_286 || h_core == H_CORE_386 || h_core == H_CORE_486; }

/* G6.0 — la carte plate de 16 Mo est celle du 386 ET du 486 : même exec386, même espace. */
static int h_flat16(void) { return h_core == H_CORE_386 || h_core == H_CORE_486; }



int h_get_core(void) { return h_core; }

/* G4.0 — le coprocesseur que h_reset et h_boot donnent à cpu_set(), pendant de la clé
 * `fpu` (pc.c:655-656). FPU_NONE par défaut : les chiffres de régression ne bougent pas. */
static int h_fpu_type = 0;

void h_set_fpu(int type) {
        h_fpu_type = type;
}

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

/* --- son : le chronomètre à 48 kHz, et le haut-parleur (M9) ------------------
 *
 * MÊME MOTIF QUE 808x.c, ET POUR LA MÊME RAISON. speaker_buffer
 * (sound_speaker.c:10), speaker_pos (:12) et speaker_get_buffer (:39) sont
 * `static`. Lié normalement, le fichier ne montre rien et h_speaker_probe n'a
 * rien à lire. On le compile donc DANS cette unité de traduction — il ne doit
 * pas figurer dans SRC du Makefile, sinon chaque symbole est défini deux fois.
 *
 * src/sound/sound.c, lui, n'est PAS lié : il traîne dix-neuf SOUND_CARD, le fil
 * CD, les appels ATAPI et OpenAL. Même arbitrage que src/video/video.c et ses
 * quatre-vingt-dix symboles de cartes (voir harness_stubs.c). Ce qui suit en
 * reprend la part qui compte, copiée verbatim.
 *
 * DEUX NIVEAUX, à ne pas confondre :
 *
 *   (a) ce qui PORTE DU TEMPS — sound_poll_timer, réarmé à 48 kHz. C'est un
 *       chronomètre de plus dans la liste de timer.c, donc des timer_process()
 *       en plus, donc n_timer_process qui change. Sans lui ici, l'oracle et le
 *       C# ne modélisent pas la même machine et le diff d'amorçage vire au rouge
 *       sur le compteur avant d'avoir rien prouvé.
 *
 *   (b) ce qui ne porte QUE du son — givealbuffer. Réduit à rien : l'oracle est
 *       muet, et le silence ne coûte pas un cycle. C'est l'empreinte
 *       h_sound_hash, prise au même endroit des deux côtés, qui compare les
 *       échantillons.
 */
#include "../../pcem-dev/src/sound/sound_speaker.c"

/* sound.c:108-117 */
static struct {
        void (*get_buffer)(int32_t *buffer, int len, void *p);
        void *priv;
} sound_handlers[8];

static int sound_handlers_num;

static pc_timer_t sound_poll_timer;
static uint64_t sound_poll_latch;
int sound_pos_global = 0;

int soundon = 1;   /* sound.c:119 */

/* sound.c:126 pose 48000/10 = 4800, ce qui rendrait 1000/sound_buf_len NUL et
 * sound_update_buf_length() une division par zéro. La valeur vivante est celle
 * de pc.c:635, en MILLISECONDES : 200. Elle gouverne sound_buf_len_al, donc
 * l'instant où sound_pos_global reboucle, donc speaker_pos, donc l'empreinte.
 * Elle DOIT valoir 200 des deux côtés (Sound/sound.cs). */
int sound_buf_len = 200;
int sound_gain = 0;                  /* sound.c:127 */
int sound_buf_len_al = 48000 / 20;   /* soundopenal.c:27 */

/* sound.c:129-136 */
void sound_update_buf_length(void) {
        int new_buf_len = (48000 / (1000 / sound_buf_len)) / 4;

        if (new_buf_len > MAXSOUNDBUFLEN)
                new_buf_len = MAXSOUNDBUFLEN;

        sound_buf_len_al = new_buf_len;
}

static int32_t *outbuffer;   /* sound.c:199 */

/* sound.c:211-215 */
void sound_add_handler(void (*get_buffer)(int32_t *buffer, int len, void *p), void *p) {
        sound_handlers[sound_handlers_num].get_buffer = get_buffer;
        sound_handlers[sound_handlers_num].priv = p;
        sound_handlers_num++;
}

/* Empreinte FNV-1a cumulative du son RÉELLEMENT produit, prise là où PCem
 * appelle givealbuffer — donc après le passage des handlers. speaker_buffer seul
 * ne dirait rien de ce passage, et speaker_get_buffer étant `static` dans un
 * fichier vendoré, on ne peut ni l'intercepter ni y ajouter une ligne. Même
 * point de prise côté C#, au même endroit de sound_poll. */
uint64_t h_sound_hash;

static void h_sound_mix_hash(void) {
        int c;
        for (c = 0; c < sound_buf_len_al * 2; c++) {
                h_sound_hash ^= (uint64_t)(uint32_t)outbuffer[c];
                h_sound_hash *= 1099511628211ULL;
        }
}

/* sound.c:218-256 */
void sound_poll(void *priv) {
        timer_advance_u64(&sound_poll_timer, sound_poll_latch);

        /* omitted: cd_pos et thread_set_event(sound_cd_event) (sound.c:221-225) —
           fil CD, hors périmètre 5150. */

        sound_pos_global++;
        if (sound_pos_global == sound_buf_len_al) {
                int c;

                memset(outbuffer, 0, sound_buf_len_al * 2 * sizeof(int32_t));

                for (c = 0; c < sound_handlers_num; c++)
                        sound_handlers[c].get_buffer(outbuffer, sound_buf_len_al, sound_handlers[c].priv);

                h_sound_mix_hash();

                if (soundon)
                        givealbuffer(outbuffer);

                sound_pos_global = 0;
                sound_update_buf_length();
        }
}

/* sound.c:258 */
/* sound.c:123, :138-141 — G8.2 : le mélangeur CT1345 l'appelle ; lu par le seul fil CD (omis). */
static unsigned int cd_vol_l, cd_vol_r;
void sound_set_cd_volume(unsigned int vol_l, unsigned int vol_r) {
        cd_vol_l = vol_l;
        cd_vol_r = vol_r;
}

void sound_speed_changed(void) { sound_poll_latch = (uint64_t)((double)TIMER_USEC * (1000000.0 / 48000.0)); }

/* sound.c:260-268. L'allocation d'outbuffer est hissée de sound_init()
 * (sound.c:205), qui appartient à l'IHM chez PCem (wx-sdl2.c:470) : l'oracle
 * n'en a pas, et sound_poll écrirait dans un pointeur nul au premier tampon
 * plein. Même hissage côté C#, pour la même raison. */
void sound_reset(void) {
        h_sound_hash = 1469598103934665603ULL;

        if (!outbuffer)
                outbuffer = malloc(MAXSOUNDBUFLEN * 2 * sizeof(int32_t));

        timer_add(&sound_poll_timer, sound_poll, NULL, 1);

        sound_handlers_num = 0;

        /* omitted: sound_set_cd_volume(), ioctl_audio_stop(), image_audio_stop()
           (sound.c:265-267) — CD, hors périmètre. */
}

/* L'étage hôte de PCem (soundopenal.c:143). Muet ici : voir le niveau (b). */
void givealbuffer(int32_t *buf) { (void)buf; }

void keyboard_xt_init(void);   /* déclaré dans models/model.c chez PCem */

/* B2 — les quatre symboles de l'AT. Leurs en-têtes existent (keyboard_at.h,
 * devices/nvr.h) mais les inclure tirerait device.h et ses dépendances dans
 * cette unité, qui inclut déjà 808x.c. On les déclare, comme pour
 * keyboard_xt_init juste au-dessus. */
void keyboard_at_init(void);
void loadnvr(void); /* nvr.h — declare la, comme les quatre symboles de l'AT. */
extern uint8_t nvrram[128]; /* nvr.h:16 — G1.1 */
void neat_init(void); /* neat.h — G3.0, le chipset de l'ami286. */
void headland_init(void); /* headland.h — G3.1, le chipset de l'ami386. */
void opti495_init(void); /* opti495.h — G3.2, le chipset de l'ami386dx. */
void ali1429_init(void);  /* ali1429.h — G6.3, le chipset de l'ami486. */
void ali1429_reset(void);

/* intgatesize est le SEUL des sept globaux de C7a qu'aucun en-tete ne declare : c'est
 * un `int intgatesize;` nu a x86seg.c:32, sans extern nulle part dans l'arbre — les six
 * autres sont dans x86.h (:179, :202, :216, :250, :268). Declare ici, comme loadnvr et
 * les quatre symboles de l'AT. */
extern int intgatesize;
void h_models_init(void);
extern int model;
extern device_t nvr_device;

/* M12 — disque dur. hdc[] et hdd_controller_name sont definis par hdd.c, ide_fn
 * par ide.c ; aucun des deux n'est lie (registres de cartes), donc harness_stubs.c
 * les porte. Les deux device_t viennent de mfm_xebec.c, lie. */
extern PcemHDC hdc[7];
extern char hdd_controller_name[16];
extern char ide_fn[7][512];
extern device_t mfm_xebec_device;
extern device_t dtc_5150x_device;
extern device_t mfm_at_device;     /* G5.0 */
extern device_t ide_device;        /* G5.0 */
extern int cdrom_channel, zip_channel;
void resetide(void);

/* x86.h:122 définit `cycles` comme une macro vers cpu_state._cycles. Le
 * préprocesseur ne connaît pas l'accès à un membre : `out->cycles` deviendrait
 * `out->cpu_state._cycles`. On la retire donc ici et on écrit explicitement
 * cpu_state._cycles dans ce fichier — h_state peut alors garder le nom `cycles`,
 * qui est celui que le C# doit comparer. */
#undef cycles

/* Même piège, même remède, au jalon 286 : x86.h:127 définit `cr0` comme une macro
 * vers cpu_state.CR0.l, donc `out->cr0` deviendrait `out->cpu_state.CR0.l`. Le
 * `#undef` de harness.h ne suffit pas — x86.h est inclus APRÈS lui, par 808x.c
 * ligne 43, et le redéfinit. On le retire donc ici aussi, une fois 808x.c déplié.
 * Rien au-delà de ce point n'utilise la macro : vérifié, harness.c n'écrit `cr0`
 * qu'une fois, dans h_getstate. */
#undef cr0

/* Fournis par harness_stubs.c */
extern uint64_t h_n_inb, h_n_outb, h_n_picint, h_n_picinterrupt, h_n_timer_process;
extern uint64_t h_n_readmembl, h_n_writemembl, h_n_readmemwl, h_n_writememwl, h_n_fatal;
extern void h_stub_counters_reset(void);

/* L'oracle lie le vrai src/video/ depuis M4.2. On declare plutot que d'inclure
 * video.h/vid_cga.h : ces en-tetes referencent mem_mapping_t, pc_timer_t et
 * device_t, que seul l'include de 808x.c ci-dessus a fait entrer. */
extern device_t cga_device;
/* M15 — la VGA. vid_svga.h est inclus, lui : h_vga_probe lit svga_t champ par champ,
 * et une redéclaration à la main de cette structure de 70 champs dériverait sans bruit
 * de celle que vid_svga.c compile. mem_mapping_t et pc_timer_t sont déjà là, PALETTE
 * vient de video.h. */
#include "video.h"
#include "vid_svga.h"
extern device_t vga_device;
/* M21 — le port serie et la souris serie Microsoft (serial.c, mouse_serial.c). */
#include "serial.h"
#include "mouse.h"
/* PS2.0 — mouse.c est lié (le registre mouse_list, mouse_emu_init, mouse_poll) : la souris
 * montée est celle de mouse_type, posé par h_set_mouse_type avant h_boot (pc.c:784). La
 * globale de l'hôte que lisent les souris (plat-mouse.h ; wx-sdl2-mouse.c n'est pas lié). */
int mouse_buttons;
void h_set_mouse_type(int t) { mouse_type = t; }
void h_ps2_forget(void);                /* harness_ps2.c */
void h_mouse_probe(uint64_t *o);        /* harness_ps2.c */
/* Pendant de pollmouse (pc.c:110-121), au mouvement près : le harnais n'a pas de souris
 * hôte ; cet appel injecte des mickeys et l'état des boutons, comme mouse_poll_host puis
 * mouse_get_mickeys les rendraient, pour qu'un diff exerce le chemin de réception. */
void h_mouse_poll(int x, int y, int z, int b) {
        mouse_buttons = b;
        mouse_poll(x, y, z, b);
}
/* M19 — les deux Trident (vid_tvga.h), compilées par harness_tvga.c qui inclut
 * vid_tvga.c pour en lire la tvga_t privée. */
#include "vid_tvga.h"
#include "vid_cl5429.h"   /* G7.1 — gd5429_device, compilée par harness_cl5429.c */
void h_tvga_probe(svga_t *svga, uint64_t *out);
void h_cl5429_probe(svga_t *svga, uint64_t *out); /* harness_cl5429.c, G7.1 */
#include "vid_s3.h"       /* G7.3 — s3_phoenix_trio64_device, compilée par harness_s3.c */
void h_s3_probe(svga_t *svga, uint64_t *out);     /* harness_s3.c, G7.3 */
#include "olivetti_m24.h"     /* G1.1 — l'Olivetti M24 */
#include "keyboard_olim24.h"
#include "gameport.h"
#include "vid_olivetti_m24.h" /* m24_device, compilée par harness_m24.c */
void h_m24_attach(void);      /* harness_m24.c */
void h_m24_probe(uint64_t *out);
void h_m24_forget(void);
#include "amstrad.h"          /* G1.2 — l'Amstrad PC1512 */
#include "keyboard_amstrad.h"
#include "vid_pc1512.h"       /* pc1512_device, compilée par harness_pc1512.c */
#include "vid_mda.h"          /* G9.0 — mda_device, compilée par harness_mda.c */
void h_pc1512_attach(void);   /* harness_pc1512.c */
void h_pc1512_probe(uint64_t *out);
void h_mda_attach(void);       /* harness_mda.c — G9.0 */
void h_mda_probe(uint64_t *out);
void h_mda_forget(void);
void h_pc1512_forget(void);
#include "sound_adlib.h"      /* G8.1 — l'AdLib */
/* G8.2 — la Sound Blaster Pro v2, dans l'ordre d'inclusion de sound_sb.c (:1-12) : sb_t embarque
 * opl_t, emu8k_t et mpu401_uart_t. */
#include "sound.h"
#include "sound_emu8k.h"
#include "sound_mpu401_uart.h"
#include "sound_opl.h"
#include "sound_sb.h"
void h_opl_reset(void);       /* harness_dbopl.cpp */
static char h_sndcard_name[32]; /* h_set_sndcard, plus bas */
extern int gfxcard;
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

        /* ET LE CHEMIN D'INSTRUCTION, trouve par la porte de A2.2a.
         *
         * mem_alloc() enregistre ram_low_mapping avec `ram` comme pointeur d'EXEC
         * (mem.c:1404), et mem_mapping_recalc en derive _mem_exec[] (mem.c:1111).
         * Reallouer `ram` juste apres laisse les deux pendants. Le rebasage de
         * pages[] ci-dessus attrapait le chemin d'ECRITURE ; celui-ci manquait.
         *
         * Inerte pendant tout le palier (a) : ni 808x.c ni aucun chemin du 8088 ne
         * lit mapping->exec ou _mem_exec. La premiere lecture est getpccache, donc
         * la PREMIERE INSTRUCTION de exec386 -- qui dereferencait un bloc libere.
         * Mesure avant correction : ram = 0x...42ba010, ram_low_mapping.exec =
         * 0x...435b010, soit ram + 0xA1000, hors d'une allocation de 640 Ko.
         * SIGSEGV net.
         *
         * mem_mapping_set_exec rappelle mem_mapping_recalc, donc _mem_exec[] est
         * reconstruit par la meme occasion. On ne touche que ram_low_mapping : c'est
         * la seule que mem_alloc fasse pointer dans `ram` en deca de 1 Mo, et le
         * harnais ne depasse jamais cette borne. */
        if (ram_low_mapping.size)
                mem_mapping_set_exec(&ram_low_mapping, ram);
        /* G3.2 — ET LES DEUX AUTRES, des qu'une machine amorcee depasse 768 Ko. La phrase
         * « le harnais ne depasse jamais cette borne » est tombee avec les machines 386 a
         * 4 Mo : mem_alloc pose ram_mid_mapping (exec = ram + 0xa0000) au-dela de 768 Ko et
         * ram_high_mapping (ram + 0x100000) au-dela de 1 Mo (mem.c:1406-1421), et h_pad_ram
         * les laissait pointer dans le bloc libere. Invisible sur l'ami386 : Headland les
         * desactive et pose ses propres mappages apres ce rebasage. Mesure sur l'ami386dx
         * (OPTi 495, 4 Mo) : le BIOS ombre F0000 en RAM, et le premier fetch dans
         * ram_mid_mapping (F000:3350) faisait SIGSEGV dans fastreadl. Memes conditions que
         * mem_alloc. */
        if (mem_size > 768)
                mem_mapping_set_exec(&ram_mid_mapping, ram + 0xa0000);
        if (mem_size > 1024)
                mem_mapping_set_exec(&ram_high_mapping, ram + 0x100000);

        resetreadlookup();
}

/* LA TAILLE DE LA CARTE, par coeur. 1 Mo pour le 8088 et le 286 ; 16 Mo pour le 386
 * (G2, D2) : l'adressage 32 bits tire des adresses effectives partout, rammask les
 * ramene sous 16 Mo (bus 24 bits du 386SX), et au-dela du bloc alloue PCem lirait hors
 * de `ram` — le C# y leve une exception. C'est aussi ce qui rend jouables les cas du
 * corpus SST 386 que la carte de 1 Mo mettait « hors carte ». */
static uint32_t h_ram_top(void) { return (uint32_t)mem_size * 1024u; }

/* LA CARTE DE 16 Mo N'EST ALLOUEE QU'UNE FOIS (G2, D3, accord de Julien). mem_alloc +
 * h_pad_ram reallouaient, remettaient a zero et recopiaient 16 Mo a CHAQUE h_reset —
 * ~11 ms par iteration de fuzz 386, surtout ici et dans le new byte[] du C#. Si la RAM
 * courante est celle qu'a posee la derniere carte 386, on la remet seulement a zero et
 * on vide le cache de traduction : les mappages, eux, n'ont pas bouge. h_boot invalide
 * (h_flat_ram = NULL) : une machine amorcee refait ses propres mappages. Meme geste
 * cote C# (FlatMap), meme etat final des deux cotes — une RAM nulle, la carte plate. */
static uint8_t *h_flat_ram;

static void h_flat_map(void) {
        if (h_flat16() && h_flat_ram && ram == h_flat_ram && mem_size == 16384) {
                memset(ram, 0, h_ram_top() + 4);
                resetreadlookup();
                return;
        }
        mem_size = h_flat16() ? 16384 : 1024; /* Ko */
        if (!h_mem_inited) {
                mem_init();
                h_mem_inited = 1;
        }
        mem_alloc();
        h_pad_ram();
        mem_set_mem_state(0x000000, h_ram_top(), MEM_READ_INTERNAL | MEM_WRITE_INTERNAL);
        mem_mapping_add(&h_flat_mapping, 0x000000, h_ram_top(), mem_read_ram, mem_read_ramw, mem_read_raml,
                        mem_write_ram, mem_write_ramw, mem_write_raml, ram, MEM_MAPPING_INTERNAL, NULL);
        h_flat_ram = h_flat16() ? ram : NULL;
}

/* G4.0 — L'ÉTAT x87 QUE RIEN NE REMET. x87_reset() est vide (x87.c:97) et resetx86 ne
 * touche ni ST, ni tag, ni TOP, ni npxs, ni npxc : chez PCem ils valent le zéro de .bss
 * à la mise sous tension et gardent ensuite ce qu'on y a mis. DEVIATION du harnais,
 * comme h_seg_clear_residue : le fuzzeur amorce à chaque itération, et sans ce nettoyage
 * l'itération N+1 hériterait de l'état tiré à N. Zéro, la valeur d'un premier amorçage.
 * Pendant de ClearFpuResidue côté C#. */
static void h_fpu_clear_residue(void) {
        memset(cpu_state.tag, 0, sizeof(cpu_state.tag));
        cpu_state.TOP = 0;
        cpu_state.ismmx = 0;
        cpu_state.npxs = 0;
        cpu_state.npxc = 0;
        memset(cpu_state.ST, 0, sizeof(cpu_state.ST));
        memset(cpu_state.MM_w4, 0, sizeof(cpu_state.MM_w4));
        memset(cpu_state.MM, 0, sizeof(cpu_state.MM));
        x87_pc_off = x87_op_off = 0;
        x87_pc_seg = x87_op_seg = 0;
}

void h_setfpu(const uint64_t st[8], const uint64_t mm[8], const uint16_t mm_w4[8],
              const uint8_t tag[8], int top, uint16_t npxs, uint16_t npxc) {
        for (int i = 0; i < 8; i++) {
                memcpy(&cpu_state.ST[i], &st[i], sizeof(double));
                cpu_state.MM[i].q = mm[i];
                cpu_state.MM_w4[i] = mm_w4[i];
                cpu_state.tag[i] = tag[i];
        }
        cpu_state.TOP = top;
        cpu_state.npxs = npxs;
        cpu_state.npxc = npxc;
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
        h_fpu_clear_residue();

        /* Configuration machine : IBM XT, Intel 8088.
         * Posée avant resetx86() parce que celle-ci branche sur AT, is486 et
         * is386 (808x.c:671-687) pour choisir le vecteur de reset et rammask. */
        AT = h_exec386(); /* pc.c:484 — c'est AT qui aiguille vers exec386 */
        is386 = 0;
        is486 = 0;
        is8086 = 0; /* 8088 : file de préfetch de 4 octets, pas 6 */
        hasfpu = 0;
        /* G4.0 — ÉCRIT, et non plus un zéro de .bss : cpu_set() en tire hasfpu (cpu.c:184).
         * Pendant de FuzzFpu côté C#. */
        fpu_type = h_fpu_type;
        cpu_16bitbus = 0;
        AMSTRAD = TANDY = PCI = MCA = 0;

        /* LE VRAI cpu_set() (M16, étape 6), là où h_cpu_config_286/_8088 en recopiaient
         * les valeurs : le 5150 pour le coeur 8088, l'ami286 pour le 286 — son entrée 0,
         * le 286/6, par défaut, ou celle que h_set_cpu a poussée (`fuzz --cpu N`).
         * resetx86() appelle désormais le vrai cpu_update_waitstates(), qui lit la table
         * de la machine : il faut une machine, même ici. Pendant exact de _808x.Reset()
         * et de _386.Reset286() côté C#. */
        h_models_init();
        /* G1.0 — H_CORE_8086 : le MEME execx86, sur l'Olivetti M24 (cpus_8086) ; cpu_set()
         * y pose is8086 (cpu.c:181), donc la file de 6 octets et les mots sans pénalité. */
        model = (h_core == H_CORE_486) ? ROM_AMI486 : (h_core == H_CORE_386) ? ROM_AMI386SX
                : (h_core == H_CORE_286) ? ROM_AMI286 : (h_core == H_CORE_8086) ? ROM_OLIM24 : ROM_IBMPC;
        cpu_manufacturer = 0;
        cpu = (h_exec386() || h_core == H_CORE_8086) ? h_cpu_index : 0;
        if (!h_cpu_index_ok(model, cpu)) {
                fprintf(stderr, "h_reset : cpu %d hors de la table du romset %d, 0 à la place\n", cpu, model);
                cpu = 0;
        }
        cpu_set();

        /* Multiplicateur TSC du XT. clockhardware() (808x.c:893-904) convertit les
         * cycles CPU en tops de l'oscillateur maître à 14,318 MHz en virgule fixe
         * 32:32, parce qu'il n'y a pas de rapport entier entre les deux fréquences.
         * Valeur reprise de setpitclock() (models/pit.c:52) pour un 8088 à
         * 4 772 728 Hz — cpus_8088[0].rspeed, cpu_tables.c:33. */
        xt_cpu_multi = (uint64_t)((14318184.0 * (double)(1ull << 32)) / 4772728.0);
        /* G1.0 — le 8086 : la vitesse de l'entrée, comme setpitclock() la reçoit (pc.c:857). */
        if (h_core == H_CORE_8086)
                xt_cpu_multi = (uint64_t)((14318184.0 * (double)(1ull << 32)) /
                                          (double)models[model]->cpu[0].cpus[cpu].rspeed);

        tsc = 0;
        timer_target = 0x7FFFFFFF;

        /* Même raison que les statiques de temps de 808x.c remises plus bas : PCem
         * n'amorce qu'une fois par processus, le harnais en boucle. */
        h_prefetch_reset();

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
                ram[(addr + i) & (h_ram_top() - 1)] = buf[i];
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

/* LIT LA RAM, ET RIEN D'AUTRE. Le masque a 0xFFFFF est un masque d'ADRESSE, pas
 * une borne du tableau : ram[] fait mem_size Ko, et 0xB8000 sort d'une machine a
 * 512 Ko. Mesure a la sonde AT : SIGSEGV en lisant la memoire video.
 *
 * On borne donc, et on rend 0xFF au-dela — la valeur d'un bus flottant, celle que
 * readmembl rend sur une adresse sans mappage. Pour lire ce qui n'est PAS de la
 * RAM, voir h_read_phys ci-dessous. */
void h_read(uint32_t addr, uint8_t *buf, uint32_t len) {
        uint32_t taille = (uint32_t)mem_size * 1024;
        for (uint32_t i = 0; i < len; i++) {
                uint32_t a = (addr + i) & (h_ram_top() - 1);
                buf[i] = (a < taille) ? ram[a] : 0xFF;
        }
}

/* LIT PAR LES MAPPAGES, donc la memoire video, les ROM d'extension et tout ce
 * qui n'est pas de la RAM. C'est ce qu'il faut pour voir l'ECRAN — et sans lui on
 * ne sait pas distinguer « le POST a fini » de « le POST affiche une erreur et
 * attend F1 ».
 *
 * ON APPELLE __real_readmembl ET NON readmembl, et la difference compte :
 * readmembl est detourne par --wrap et incrementerait h_n_readmembl, un champ
 * COMPARE par le fuzzeur. Une sonde ne doit pas laisser de trace dans ce qu'elle
 * observe.
 *
 * Il reste UNE trace : __real_readmembl pose mem_logical_addr et peut peupler
 * readlookup2 par addreadlookup. C'est assume — cette fonction est faite pour
 * etre appelee A LA FIN d'une campagne, quand plus rien n'est compare. Elle n'a
 * pas sa place dans une boucle de mesure. */
extern uint8_t __real_readmembl(uint32_t addr);

void h_read_phys(uint32_t addr, uint8_t *buf, uint32_t len) {
        for (uint32_t i = 0; i < len; i++)
                buf[i] = __real_readmembl((addr + i) & rammask);
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

void h_setregs386(const uint16_t hi[8], uint16_t eflags, uint16_t fs_sel, uint16_t gs_sel) {
        for (int i = 0; i < 8; i++)
                cpu_state.regs[i].l = (cpu_state.regs[i].l & 0xffff) | ((uint32_t)hi[i] << 16);
        cpu_state.eflags = eflags;
        loadseg(fs_sel, &cpu_state.seg_fs);
        loadseg(gs_sel, &cpu_state.seg_gs);
}

void h_setsys386(uint32_t cr0_val, uint32_t cr3_val, uint32_t dr6, uint32_t dr7) {
        cpu_state.CR0.l = cr0_val;
        cr3 = cr3_val;
        dr[6] = dr6;
        dr[7] = dr7;
}

/* G2, D6 — page-check : UN appel de mmutranslatereal (mem.c:220-317) dans un contexte que
 * l'appelant pose. CPL se lit dans seg_cs.access (x86.h), d'où l'écriture de ses bits 5-6 ;
 * cpl_override et abrt sont posés tels quels — abrt non nul exerce le retour anticipé de
 * la première ligne. abrt_error est remis à zéro pour que seule CET appel le pose.
 * cr0 (WP, bit 16) et cr3 se posent par h_setsys386 ; les tables, par h_load. */
uint32_t h_mmutranslate(uint32_t addr, int rw, int cpl, int cpl_ovr, int abrt_in) {
        uint32_t r;

        cpu_state.seg_cs.access = (cpu_state.seg_cs.access & ~0x60) | ((cpl & 3) << 5);
        cpl_override = cpl_ovr;
        cpu_state.abrt = abrt_in;
        abrt_error = 0;
        r = mmutranslatereal(addr, rw);
        cpl_override = 0;
        return r;
}

/* G2, D6 — mmu_perm (mem.c:59), que mmutranslatereal pose et que h_state ne porte pas. */
int h_mmu_perm(void) { return mmu_perm; }

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
/* LE PAS-À-PAS DU 286 N'EST PAS CELUI DU 8088, et la différence est structurelle.
 *
 * execx86() est `cycles += cycs; while (cycles > 0)` (808x.c:1222) : un budget de 1
 * suffit à n'exécuter qu'une instruction. exec386() a DEUX boucles (386.c:162-172), et
 * l'interne est bornée par `cycdiff < cycle_period`, où
 * `cycle_period = (timer_target - (uint32_t)tsc) + 1`. Le budget `cycles` ne la borne
 * pas : avec timer_target à 0x7FFFFFFF, elle continue quoi qu'il arrive.
 *
 * On rapproche donc la borne : timer_target posé à tsc rend cycle_period == 1, l'interne
 * sort après une instruction (tout opcode coûte au moins un cycle), et l'externe sort
 * parce que cycles est retombé à zéro ou moins.
 *
 * DEVIATION assumée, et son prix dit : le bas de la boucle externe fait alors
 * `if (TIMER_VAL_LESS_THAN_VAL(timer_target, tsc)) timer_process();` — donc un
 * timer_process() par pas, là où le 8088 n'en déclenche aucun. C'est acceptable ici
 * parce que le harnais est une porte de diagnostic et que le C# devra faire LE MÊME
 * geste au même endroit ; c'est inacceptable en silence, d'où ce bloc. timer_target est
 * restauré après coup pour que l'état comparé ne porte pas la trace du mécanisme. */
static int h_step286(void) {
        uint32_t saved_target = timer_target;

        cpu_state._cycles = 1;
        timer_target = (uint32_t)tsc;
        exec386(0);
        timer_target = saved_target;

        h_ins_count++;
        return 1 - cpu_state._cycles;
}

/* LE MEME PAS QUE LA BOUCLE TRACEE DE h_runpc, ET C'EST TOUT L'INTERET.
 *
 * h_step286 pose timer_target = tsc pour forcer cycle_period a 1, ce qui declenche un
 * timer_process() par pas. h_runpc en mode trace ne le fait PAS. Les deux phases du
 * boot-diff empruntaient donc des pas differents, et apres six cent mille instructions
 * leurs horloges avaient divergé : la phase 2 devenait une TROISIEME execution,
 * coherente avec elle-meme, qui n'avait pas la divergence de la phase 1 au meme indice.
 * Le diff le disait — « les etats concordent a l'index signale » — sans pouvoir la
 * localiser.
 *
 * ET LA RACINE EST PLUS PROFONDE : la boucle tracee appelle h_trace_note UNE FOIS PAR
 * ITERATION, et une iteration de exec386 n'est pas forcement une instruction — sa
 * boucle interne est bornee par cycdiff, que le budget cycles ne borne pas. L'indice de
 * la trace compte donc des ITERATIONS. Pour que la phase 2 rejoue ce que la phase 1 a
 * mesure, elle doit compter la meme chose, donc faire le meme geste. C'est cette
 * fonction.
 *
 * Sur un 8088 il n'y a rien a distinguer : execx86(0) rend la main apres exactement une
 * instruction, et c'est pourquoi le probleme n'existait pas avant l'AT. */
/* La borne du journal, exposee pour que le C# la CONFRONTE. Voir harness.h. */
int h_wlog_max(void) { return H_WLOG_MAX; }

int h_step_trace(void) {
        if (h_exec386()) {
                cpu_state._cycles = 1;
                exec386(0);
                h_ins_count++;
                return 1 - cpu_state._cycles;
        }
        return h_step();
}

int h_step(void) {
        if (h_exec386())
                return h_step286();
        cpu_state._cycles = 1;
        execx86(0);
        h_ins_count++;
        return 1 - cpu_state._cycles;
}

int h_run(int cycs) {
        uint64_t before = ins;
        cpu_state._cycles = 0;
        if (h_exec386())
                exec386(cycs);
        else
                execx86(cycs);
        h_ins_count += (uint64_t)(ins - before);
        return cycs - cpu_state._cycles;
}

/* LES DEUX CHAMPS QUE seg_reset() NE REMET PAS — UNE DEVIATION DU HARNAIS, PAS DU
 * COEUR.
 *
 * seg_reset() (x86seg.c:50-65) pose access, access2, limit, limit_low, limit_high,
 * base et seg. Il laisse EXACTEMENT DEUX champs du cache descripteur intacts, et
 * les deux ont ete trouves l'un apres l'autre par le boot-diff AT :
 *   - limit_raw, ecrit seulement par do_seg_load (x86seg.c:181) et LLDT / LTR
 *     (x86_ops_pmode.h:218, :255) ;
 *   - checked, ecrit par loadseg (:413, :423), taskswitch286 (:2864-2902) et
 *     cyrix_load_seg_descriptor (:2968).
 * Releve en enumerant la struct x86seg (x86.h:37-45) champ par champ contre le
 * corps de seg_reset, plutot qu'en attendant que le diff les sorte un par un.
 *
 * C'EST FIDELE, ET SANS CONSEQUENCE CHEZ PCem PARCE QU'IL N'AMORCE QU'UNE FOIS PAR
 * PROCESSUS. Le diff amorce deux fois : la phase 1 court l'amorcage entier, la
 * phase 2 le rejoue en pas a pas. L'oracle entrait donc en phase 2 avec ce que son
 * mode protege avait laisse — CS.limit_raw a 0xFFFF, DS.checked a 1 — et le C# avec
 * zero, faute d'etre alle aussi loin. La phase 2 rapportait ca comme sa premiere
 * divergence, a l'instruction 0, MASQUANT la vraie cause deux instructions plus
 * loin.
 *
 * Meme famille que h_prefetch_reset : ce n'est pas le coeur qu'on corrige, c'est le
 * harnais qu'on rend deterministe. A appeler des DEUX cotes au meme point. */
/* Les DEUX champs que seg_reset() ne touche pas. */
static void h_seg_clear_one(x86seg *s) {
        s->limit_raw = 0;
        s->checked = 0;
}

/* TOUS les champs, pour les descripteurs que resetx86() n'initialise pas du tout. */
static void h_seg_clear_all(x86seg *s) {
        s->base = 0;
        s->limit = 0;
        s->limit_raw = 0;
        s->access = 0;
        s->access2 = 0;
        s->seg = 0;
        s->limit_low = 0;
        s->limit_high = 0;
        s->checked = 0;
}

void h_seg_clear_residue(void) {
        /* Les six segments : resetx86() -> x86seg_reset() -> seg_reset() pose sept de
         * leurs neuf champs, donc seuls limit_raw et checked trainent. */
        h_seg_clear_one(&cpu_state.seg_cs);
        h_seg_clear_one(&cpu_state.seg_ds);
        h_seg_clear_one(&cpu_state.seg_es);
        h_seg_clear_one(&cpu_state.seg_fs);
        h_seg_clear_one(&cpu_state.seg_gs);
        h_seg_clear_one(&cpu_state.seg_ss);

        /* gdt, ldt et tr : resetx86() n'en pose AUCUN champ — il ne cite que idt
         * (808x.c:690-694). Leur valeur d'un amorcage propre est donc zero partout, et
         * c'est MESURE et non suppose : A1b l'avait sonde en faisant rendre 0x12345678
         * au C# pour lire celle de l'oracle, et les trois s'accordaient a zero la ou
         * idt.limit portait 0xFFFF. Ce sont des x86seg comme les autres, donc ils ont
         * limit_raw et checked aussi — c'est LDT.limit_raw qui a sorti ce cas, apres
         * que gdt.base et ldt.base avaient ete traites. */
        h_seg_clear_all(&gdt);
        h_seg_clear_all(&ldt);
        h_seg_clear_all(&tr);

        /* idt, lui, EST initialise par resetx86 : base = 0 et limit = 0xFFFF sur un
         * 286. On ne touche donc que les deux champs qu'il laisse, comme pour les six
         * segments — ecraser sa limite serait effacer une valeur juste. */
        h_seg_clear_one(&idt);

        /* ET LE RESTE DE h_state QUE resetx86() NE CITE PAS.
         *
         * Enumere sur la struct h_state (harness.h) champ par champ, en cochant ceux
         * que resetx86, x86seg_reset, ResetTimingState, ResetCounters ou
         * h_prefetch_reset posent deja. Ce qui suit est ce qui restait, et c'etait la
         * seule facon d'arreter de les decouvrir un par un : limit_raw, puis checked,
         * puis gdt.base, puis ldt.limit_raw, puis flags_op — cinq tours de diff pour
         * cinq champs de la MEME famille.
         *
         * Leur valeur d'un amorcage propre est zero : ce sont des globales de .bss que
         * PCem n'a aucune raison de remettre, n'amorcant qu'une fois par processus.
         * Pour flags_op, zero EST la valeur juste et pas seulement la valeur neuve —
         * c'est FLAGS_UNKNOWN (x86_flags.h), l'etat « aucun drapeau paresseux en
         * attente », et c'est ce que le fuzzeur voit a chaque iteration. */
        cr2 = 0;
        cr3 = 0;
        memset(dr, 0, sizeof(dr));
        cpl_override = 0;
        cpu_state.flags_op = 0;
        cpu_state.flags_res = 0;
        cpu_state.flags_op1 = 0;
        cpu_state.flags_op2 = 0;
        cpu_state.oldpc = 0;
        cpu_state.eaaddr = 0;
        cpu_state.ssegs = 0;
        cpu_state.abrt = 0;

        /* LES SIX GLOBAUX DU MODE PROTEGE QUE resetx86 NE REMET PAS (C7a).
         *
         * cgate32 en est absent A DESSEIN : resetx86 le pose deja (808x.c:679, :723),
         * donc l'ajouter ici serait une ligne morte.
         *
         * optype est le plus important des six : c'est le SEUL a porter de l'etat
         * entre instructions — `grep -c 'optype = 0' x86seg.c` rend ZERO, et les
         * affectations JMP (:765) et OPTYPE_INT (:1995) ne se defont jamais. Sans ce
         * nettoyage, l'iteration N+1 du fuzz herite de N des deux cotes : vert, vide,
         * et la recette --seed ne rejoue plus le meme etat. */
        abrt_error = 0;
        intgatesize = 0;
        cgate16 = 0;
        optype = 0;
        oldcpl = 0;
        cpu_cur_status = 0;

        h_fpu_clear_residue();
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
                out->seg_limit[i] = segs[i]->limit;
                out->seg_limit_raw[i] = segs[i]->limit_raw;
                out->seg_limit_low[i] = segs[i]->limit_low;
                out->seg_limit_high[i] = segs[i]->limit_high;
                out->seg_access[i] = segs[i]->access;
                out->seg_access2[i] = segs[i]->access2;
                out->seg_checked[i] = segs[i]->checked;
        }

        /* Les quatre descripteurs système. Globaux chez PCem (x86.h:171), pas membres
         * de cpu_state — d'où le tableau séparé, et non un élargissement de segs[]. */
        const x86seg *sys[H_SYS_COUNT] = { &gdt, &ldt, &idt, &tr };
        for (int i = 0; i < H_SYS_COUNT; i++) {
                out->sys_base[i] = sys[i]->base;
                out->sys_limit[i] = sys[i]->limit;
                out->sys_limit_raw[i] = sys[i]->limit_raw;
                out->sys_limit_low[i] = sys[i]->limit_low;
                out->sys_limit_high[i] = sys[i]->limit_high;
                out->sys_checked[i] = sys[i]->checked;
                out->sys_sel[i] = sys[i]->seg;
                out->sys_access[i] = sys[i]->access;
                out->sys_access2[i] = sys[i]->access2;
        }

        /* cpu_state.CR0.l, écrit sans passer par la macro : harness.h fait `#undef cr0`
         * pour que le nom reste disponible comme champ de structure. */
        out->cr0 = cpu_state.CR0.l;
        out->cr2 = cr2;
        out->cr3 = cr3;
        out->use32 = use32;
        out->stack32 = stack32;
        out->cpl_override = cpl_override;
        out->cr4 = cr4;
        for (int i = 0; i < 8; i++)
                out->dr[i] = dr[i];

        /* Les quatre drapeaux paresseux, LUS et non matérialisés. Appeler
         * flags_rebuild() ici poserait flags_op à FLAGS_UNKNOWN et rendrait la
         * représentation paresseuse invisible à toutes les captures — voir harness.h. */
        out->flags_op = cpu_state.flags_op;
        out->flags_res = cpu_state.flags_res;
        out->flags_op1 = cpu_state.flags_op1;
        out->flags_op2 = cpu_state.flags_op2;

        out->prefetch_bytes = h_prefetch_bytes();
        out->prefetch_prefixes = h_prefetch_prefixes();

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

        /* LES SEPT GLOBAUX DU MODE PROTEGE (C7a). Declares dans x86.h et x86seg.c,
         * fournis par le lien — aucune unite a ajouter. */
        out->abrt_error = abrt_error;
        out->intgatesize = intgatesize;
        out->cgate16 = cgate16;
        out->cgate32 = cgate32;
        out->optype = optype;
        out->oldcpl = oldcpl;
        out->cur_status = cpu_cur_status;

        /* G4.0 — le x87, ST en bits bruts (voir harness.h). */
        for (int i = 0; i < 8; i++) {
                memcpy(&out->fpu_st[i], &cpu_state.ST[i], sizeof(double));
                out->fpu_mm[i] = cpu_state.MM[i].q;
                out->fpu_mm_w4[i] = cpu_state.MM_w4[i];
                out->fpu_tag[i] = cpu_state.tag[i];
        }
        out->fpu_top = cpu_state.TOP;
        out->npxs = cpu_state.npxs;
        out->npxc = cpu_state.npxc;
        out->x87_pc_off = x87_pc_off;
        out->x87_op_off = x87_op_off;
        out->x87_pc_seg = x87_pc_seg;
        out->x87_op_seg = x87_op_seg;
        out->ismmx = cpu_state.ismmx;
        out->fpu_type = fpu_type;
        out->hasfpu = hasfpu;

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
 * peut reproduire exactement : l'état architectural et le temps.
 *
 * CE QUE `MIX(cpu_state.flags)` VOUDRA DIRE SOUS LE 286, et qu'il vaut mieux savoir
 * avant de le diagnostiquer à chaud. Le 8088 matérialise ses drapeaux à chaque
 * instruction, donc hacher `flags` hache une valeur vivante. exec386 ne les matérialise
 * PAS (voir les quatre champs paresseux dans harness.h) : la trace hachera une valeur
 * PÉRIMÉE, et les deux côtés ne s'accorderont dessus que s'ils effondrent la paresse aux
 * MÊMES frontières d'instruction — ce que fait une transcription fidèle, et rien d'autre.
 *
 * Conséquence pratique : sous le 286, une divergence de phase 1 dont le champ affiché est
 * `flags` désigne très probablement une divergence de `flags_op` ou de `flags_res`, une
 * instruction plus tôt. Ne pas chercher le défaut dans le calcul des drapeaux : le
 * chercher dans QUAND chaque côté appelle flags_rebuild(). La phase 2 et le fuzzeur
 * comparent les quatre champs et nomment la vraie cause ; la phase 1 ne le peut pas.
 *
 * On ne matérialise pas ici pour y remédier : ce serait muter l'état au point de capture,
 * et rendre la représentation paresseuse invisible à la phase 2 aussi. */
/* HACHER SANS tsc, POUR SEPARER LE TEMPS DU FONCTIONNEL.
 *
 * Le boot-diff AT decroche a l'instruction 627 262 sur `cycles consommes : oracle 68,
 * C# 87` a F000:3C99 — un MOV AL,AH dans la boucle qui programme le CRTC de la CGA,
 * donc du TEMPS DE CARTE VIDEO. Ce seul ecart empeche de voir tout ce qui suit, et ce
 * qui suit contient un defaut FONCTIONNEL : le POST de l'AT rend « 104-System Board
 * Error » cote C# et pas cote oracle, et 104 sur un AT veut dire « protected mode
 * failure ».
 *
 * tsc est le SEUL champ du hachage que le temps touche — les autres sont CS, pc, les
 * huit registres, DS/ES/SS et flags. L'exclure rend donc le diff aveugle au temps et
 * voyant au reste, ce qui est exactement ce qu'on veut pour trouver le 104.
 *
 * A POSER DES DEUX COTES, sans quoi les deux hachages ne portent pas sur la meme
 * chose — et c'est un drapeau de DIAGNOSTIC, pas une porte : un boot-diff sans tsc ne
 * remplace pas un boot-diff complet, il precede son diagnostic. */
static int h_trace_notsc = 0;
void h_set_trace_notsc(int on) { h_trace_notsc = on ? 1 : 0; }

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
        if (!h_trace_notsc)
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

/* --- configuration machine (M8) ---------------------------------------------
 * 640 par défaut : la valeur que h_boot portait en dur, et que TOUTE mesure déjà
 * consignée dans VERIFICATION.md suppose. Un h_boot sans h_set_mem_size préalable
 * doit donc rendre exactement la machine d'avant M8. */
int h_mem_size_kb = 640;

/* 1 = 5,25" DD, le lecteur du 5150 (fdd.c:44-46), comme pc.cs. */
int h_drive_type[2] = {1, 1};

extern int bpb_disable;

int h_boot(const char *romspath) {
        h_flat_ram = NULL; /* voir h_flat_map : la machine refait ses mappages */
        h_set_roms_path(romspath);

        /* AVANT tout le reste. h_boot() ne passe PAS par h_reset() — il appelle
         * resetx86() directement — donc sans cet appel isa_cycles restait à la valeur
         * que cpu.c lui donne, ZÉRO, au lieu de 1.
         *
         * Mesuré : les cinq portes de régression sont passées au rouge, avec un état
         * architectural IDENTIQUE des deux côtés et un seul champ divergent, `tsc`.
         * isa_cycles gouverne le coût en cycles d'un accès d'E/S (io.c) : à zéro, les
         * in/out du POST ne facturent plus rien, et l'horloge dérive sans que jamais un
         * registre ne bouge. Exactement le mode de panne que l'en-tête de ce fichier
         * décrit — « se tromper ici ne plante rien, ne fausse aucun registre ». */
        /* M16 — la configuration du processeur N'EST PLUS POSÉE ICI. Elle l'était, par
         * h_cpu_config_286/_8088 selon h_core, avant tout le reste ; c'est désormais le
         * vrai cpu_set() de PCem qui la pose, à SA place : dans resetpchard, après
         * io_init et avant mem_alloc (pc.c:362-364), plus bas. */

        /* Depuis M9 ce crochet n'est plus un no-op : c'est lui qui pose
         * sound_poll_latch. Sans lui, le latch vaut 0, timer_advance_u64(t, 0)
         * ne fait pas avancer l'échéance et timer_process() boucle à l'infini
         * sur le chronomètre du son. */
        _sound_speed_changed = sound_speed_changed;

        /* Pendant de _808x.ResetCounters() : sans ça, un second h_boot() dans le
           même processus repart avec les compteurs du premier. */
        h_stub_counters_reset();
        h_wlog_reset();

        device_init();
        initvideo();          /* pc.c:261 */
        mem_size = h_mem_size_kb;

        if (!h_mem_inited) {
                mem_init();
                h_mem_inited = 1;
        }

        if (!loadbios())
                return 0;

        timer_reset();       /* pc.c:276 */
        sound_reset();       /* pc.c:277 */
        io_init();           /* pc.c:278 */
        fdc_init();          /* pc.c:279 */
        disc_init();         /* pc.c:280 */
        img_init();          /* pc.c:282 ; fdi_init (pc.c:281) est un stub vide, chargeur FDI non lié */

        /* pc.c:776-777 — loadconfig() pose les types de lecteur AVANT initpc. Le
         * 5150 a des 5,25" double densité : type 1 (fdd.c:44-46), comme pc.cs. */
        fdd_set_type(0, h_drive_type[0]);
        fdd_set_type(1, h_drive_type[1]);

        /* resetpchard() réduit, miroir de pc.resetpchard() côté C# (pc.c:353) */
        timer_reset();
        device_close_all();
        device_init();
        h_opl_reset();               /* G8.1 — déviation de l'oracle : opl[] repart de zéro */
        sound_reset();               /* pc.c:361 — AVANT speaker_init : il remet
                                        sound_handlers_num à 0 et effacerait
                                        l'enregistrement du handler. */
        io_init();

        /* pc.c:363 — cpu_set(), LE VRAI, avant mem_alloc qui lit cpu_16bitbus (M16).
         *
         * `model` vaut le romset (DEVIATION de l'ORACLE, harness_stubs.c : models[] y est
         * indexé par romset). cpu et cpu_manufacturer sont écrits EXPLICITEMENT : cpu.c:82
         * initialise `cpu` à 3, et chez PCem c'est loadconfig qui l'écrase toujours.
         *
         * Deux refus, là où PCem n'en a aucun : un indice hors de la table (le C# refuse de
         * même, check_cpu dans pc.cs), et une table qui contredit le coeur choisi par
         * h_set_core — le harnais inline l'init de l'AT sur h_core, cpu_set lit le type
         * dans la table : les deux doivent dire la même machine. */
        h_models_init();
        model = romset;
        if (!h_cpu_table_ok()) {
                fprintf(stderr, "h_boot : cpu %d (fabricant %d) hors de la table du romset %d\n",
                        h_cpu_index, h_cpu_manu, romset);
                return 0;
        }
        cpu_manufacturer = h_cpu_manu;
        cpu = h_cpu_index;
        fpu_type = h_fpu_type; /* G4.0 — pc.c:656, la clé `fpu`, poussée par h_set_fpu */
        cpu_set();
        if ((cpu_s->cpu_type >= CPU_i486SX ? H_CORE_486 : cpu_s->cpu_type >= CPU_386SX ? H_CORE_386
             : cpu_s->cpu_type == CPU_286 ? H_CORE_286 : H_CORE_8088)
            != h_core) {
                fprintf(stderr, "h_boot : la table du romset %d (cpu_type %d) contredit le coeur %d\n",
                        romset, cpu_s->cpu_type, h_core);
                return 0;
        }
        /* G1.0 — les deux machines 8086 sont dans models[] pour cpu_set() et le fuzzeur, mais
         * leur matériel (vidéo, clavier, ports) n'entre qu'en G1.1 et G1.2 : refus bruyant,
         * comme le C# (initpc, init nulle), plutôt qu'un XT qui exécuterait leur BIOS. */

        mem_alloc();
        h_pad_ram();
        fdc_init();                  /* pc.c:365 */
        disc_reset();                /* pc.c:366 */
        disc_load(0, discfns[0]);    /* pc.c:367 */
        disc_load(1, discfns[1]);    /* pc.c:368 */

        /* model_init() -> xt_init() ou at_init(), model.c:180-196 et 335-351.
         *
         * LES DEUX PARTAGENT common_init() — dma_init, fdc_add, pic_init,
         * pit_init — et divergent ensuite. Le harnais inline common_init plutot
         * que de l'appeler : lpt_init, serial1_init et serial2_init ne sont pas
         * lies, et le cote C# ne les transcrit pas davantage.
         *
         * CE QUI SEPARE UN AT D'UN XT tient en six lignes, et chacune compte :
         *   - AT = 1, qui aiguille resetx86 vers le vecteur du 286 ET rammask ;
         *   - pit_refresh_timer_at au lieu de _xt, un rafraichissement memoire
         *     de periode differente ;
         *   - dma16_init : le second controleur DMA, celui des transferts 16 bits ;
         *   - keyboard_at_init : le 8042, qui tient A20 ET LA LIGNE DE RESET —
         *     seule sortie du mode protege d'un 286 ;
         *   - nvr_device : le MC146818, horloge temps reel et CMOS. Le POST le
         *     lit avant tout le reste et s'arrete sur une somme de controle
         *     fausse ;
         *   - pic2_init : le second 8259, cascade sur l'IRQ 2.
         *
         * nmi_mask = 0 sur un AT la ou xt_init appelle nmi_init().
         *
         * model.c:688 d'abord — AMSTRAD = AT = PCI = TANDY = MCA = 0. Absente jusqu'a
         * M16 des deux cotes : un 8088 amorce apres un AT dans le meme processus
         * gardait AT = 1. Pendant de model_init() cote C# (model.cs). */
        AMSTRAD = AT = PCI = TANDY = MCA = 0;
        /* G1.2 — ams_init (model.c:259-270) pose AMSTRAD AVANT common_init. */
        if (romset == ROM_PC1512)
                AMSTRAD = 1;
        dma_init();
        fdc_add();                   /* model.c:194 */
        pic_init();
        pit_init();
        /* model.c:198-199 — M21 : les deux UART, COM1 et COM2. lpt_init reste dehors. */
        serial1_init(0x3f8, 4, 1);
        serial2_init(0x2f8, 3, 1);
        mem_add_bios();

        if (romset == ROM_PC1512) {
                /* G1.2 — ams_init (model.c:259-270), après common_init et mem_add_bios. PAS de
                 * rafraîchissement mémoire, comme la M24. lpt1_remove (:263) omis, des deux
                 * côtés : lpt_init n'a rien posé. Puis model_init ajoute le device de la machine
                 * (model.c:692-693), ams1512_device, la langue. */
                amstrad_init();
                keyboard_amstrad_init();
                device_add(&nvr_device);
                nmi_init();
                fdc_set_dskchg_activelow();
                device_add(&gameport_device);
                device_add(&ams1512_device);
        } else if (romset == ROM_OLIM24) {
                /* G1.1 — olim24_init (model.c:292-300), après common_init et mem_add_bios :
                 * PAS de rafraîchissement mémoire (le canal 1 du PIT reste nul, contrairement à
                 * xt_init, model.c:205), le clavier propre de la M24, le CMOS, les ports 66h/67h,
                 * le masque NMI, la manette (sans manette branchée, décision n° 2). */
                keyboard_olim24_init();
                device_add(&nvr_device);
                olivetti_m24_init();
                nmi_init();
                device_add(&gameport_device);
        } else if (h_exec386()) {
                /* models[] et `model` sont posés plus haut, avant cpu_set() (M16) :
                 * nvr.c les déréférence à chaque écriture CMOS. */
                AT = 1;
                pit_set_out_func(&pit, 1, pit_refresh_timer_at);
                dma16_init();
                keyboard_at_init();
                device_add(&nvr_device);
                pic2_init();
                nmi_mask = 0;
                /* G3.0 — le chipset de la machine, comme model.c le chaine apres at_init :
                 * at_neat_init (model.c:452-455). Le C# l'appelait deja (model.cs:199) et
                 * l'oracle non : les deux cotes n'amorcaient pas le meme ami286. */
                if (romset == ROM_AMI286)
                        neat_init();
                /* G3.1 — at_headland_init (model.c:482-485) : at_init puis le chipset. */
                if (romset == ROM_AMI386SX)
                        headland_init();
                /* G3.2 — at_opti495_init (model.c:492-495). */
                if (romset == ROM_AMI386DX_OPTI495)
                        opti495_init();
                /* G6.3 — at_ali1429_init (model.c:502-505). */
                if (romset == ROM_AMI486)
                        ali1429_init();
                /* omitted: device_add(&gameport_device) — le port jeu n'est pas
                   lie, et le cote C# ne le transcrit pas. */
        } else {
                pit_set_out_func(&pit, 1, pit_refresh_timer_xt);
                keyboard_xt_init();
                nmi_init();
        }

        /* mouse_emu_init(), pc.c:373 — M21, puis PS2.0 : mouse.c est lié, la souris est celle
         * de mouse_type (0, la série Microsoft, par défaut, pc.c:784). */
        h_ps2_forget();
        mouse_emu_init();

        /* video_init(), pc.c:374. On appelle directement device_add plutot que
         * video_init() : le switch sur romset de video.c:761-914 n'a de cas pour
         * aucun des quatre romsets du depot, on en sort donc vers la ligne 915, qui
         * traverse le registre VIDEO_CARD -- et video.c n'est pas lie
         * (90 symboles de cartes). Le cote C# TRAVERSE ce registre depuis M15 --
         * video.cs le transcrit reduit a v_cga et v_vga --, et ce raccourci-ci en
         * est le pendant exact : gfxcard (h_set_gfxcard) choisit la carte que
         * video_cards[video_old_to_new(gfxcard)]->device designe (video.c:96, :191).
         * Les deux cotes ajoutent donc la MEME carte de la MEME facon -- ce qui est
         * tout ce que l'oracle doit garantir. */
        /* G1.1 — video.c:800-802 : la M24 a SA vidéo, choisie par le romset, gfxcard ignoré. */
        h_m24_forget();
        h_pc1512_forget();
        h_mda_forget();
        /* G1.2 — video.c:775-777 : le PC1512 a SA vidéo, choisie par le romset. */
        if (romset == ROM_PC1512) {
                device_add(&pc1512_device);
                h_pc1512_attach();
        } else if (romset == ROM_OLIM24) {
                device_add(&m24_device);
                h_m24_attach();
        } else if (gfxcard == GFX_VGA || gfxcard == GFX_TVGA || gfxcard == GFX_TVGA9000B || gfxcard == GFX_CL_GD5429 ||
            gfxcard == GFX_PHOENIX_TRIO64) {
                svga_t *svga;

                /* M19 : les deux Trident, video.c:177-181. */
                if (gfxcard == GFX_TVGA)
                        device_add(&tvga8900d_device);
                else if (gfxcard == GFX_TVGA9000B)
                        device_add(&tvga9000b_device);
                /* G7.1 — video.c:99-100, la GD5429. */
                else if (gfxcard == GFX_CL_GD5429)
                        device_add(&gd5429_device);
                /* G7.3 — video.c:164-166, la Trio64 Phoenix. */
                else if (gfxcard == GFX_PHOENIX_TRIO64)
                        device_add(&s3_phoenix_trio64_device);
                else
                        device_add(&vga_device);
                /* DEVIATION de l'ORACLE (pas d'iXtal26) : svga_init alloue la VRAM et
                 * changedvram par malloc SANS les effacer (vid_svga.c:772, :777) — du
                 * tas, donc de l'UB et pas un comportement. Le premier amorçage du
                 * processus reçoit une VRAM (256 Ko, 512 Ko ou 1 Mo) issue de mmap, donc nulle ; mais
                 * vga_close la libère, glibc relève alors son seuil de mmap dynamique,
                 * et la VRAM du SECOND h_boot — la phase 2 du boot-diff — vient du tas
                 * avec les octets de la première. changedvram (4 Ko) vient du tas dès
                 * le départ. Le côté C# alloue des tableaux CLR, nuls. Même arbitrage
                 * que h_pad_ram et PB-24 : il n'y a rien dont être le pendant fidèle. */
                svga = svga_get_pri();
                memset(svga->vram, 0, svga->vram_max);
                memset(svga->changedvram, 0, 0x1000000 >> 12);
        } else if (gfxcard == GFX_MDA) {
                /* G9.0 — video.c:140, la MDA. */
                device_add(&mda_device);
                h_mda_attach();
        } else
                device_add(&cga_device);
        speaker_init();              /* pc.c:375, juste après video_init() */
        /* G8.1 — sound_card_init (pc.c:383) : la carte son nommée par h_set_sndcard. */
        if (!strcmp(h_sndcard_name, "adlib"))
                device_add(&adlib_device);
        else if (!strcmp(h_sndcard_name, "sbprov2"))
                device_add(&sb_pro_v2_device);

        /* pc.c:392 — hdd_controller_init(hdd_controller_name), reduit. APRES
           mem_alloc() : celui-ci detruit toute la liste de mappages, et une carte a
           ROM d'extension posee avant verrait la sienne effacee. */
        if (!strcmp(hdd_controller_name, "mfm_xebec"))
                device_add(&mfm_xebec_device);
        else if (!strcmp(hdd_controller_name, "dtc5150x"))
                device_add(&dtc_5150x_device);
        /* G5.0 — le Fixed Disk Adapter de l'AT, que les profils 286 et 386 emploient depuis
           M13 sans qu'aucun boot-diff l'ait jamais vu : mfm_at.c n'était pas lié. */
        else if (!strcmp(hdd_controller_name, "mfm_at"))
                device_add(&mfm_at_device);
        else if (!strcmp(hdd_controller_name, "ide")) {
                /* G5.0 — pc.c:703-705 lit cdrom_channel (défaut 2) et zip_channel (-1). Sans
                   ATAPI (PLAN-G5.md, décision n° 1), le canal 2 est un disque dur : la
                   configuration PCem « Hard drive » sur les quatre lecteurs (wx-config.c:891). */
                cdrom_channel = -1;
                zip_channel = -1;
                device_add(&ide_device);
        }

        /* pc_reset(), pc.c:176. timer_reset() y est COMMENTÉ (pc.c:178) : l'appeler
           ici invalide (magic = 0) tous les chronomètres que model_init() vient
           d'enregistrer, et la machine tourne sans PIT. setpitclock() appartient
           bien à pc_reset (pc.c:184-187), donc APRÈS pit_init. */
        resetx86();
        fdc_reset();                 /* pc.c:180 */
        pic_reset();
        serial_reset();              /* pc.c:182, M21 */
        /* pc.c:184-187 — `if (AT) setpitclock(rspeed) else setpitclock(14318184.0)`.
                 *
                 * PITCONST = cpuclock / 1193182, donc le NOMBRE DE CYCLES CPU par tic
                 * du PIT. Le passer a 14318184 sur un AT donnait 12 cycles par tic la
                 * ou un 286/6 en demande 5,03 : le PIT tournait 2,4 fois trop lentement
                 * PAR RAPPORT AU PROCESSEUR.
                 *
                 * Mesure : le POST de l'AT compte les bascules du bit de rafraichissement
                 * et exige au moins 0xF600 (F000:05B8). Il en trouvait moins et s'arretait
                 * sur un HLT en F000:05C4, apres 100 millions d'instructions.
                 *
                 * VERBATIM depuis M16 : c'est la rspeed de l'entree de CPU choisie, que
                 * cpu_set() vient de lire. C'etait le litteral 6000000 tant que models[]
                 * etait nul — la rspeed de cpus_286[0] comme de cpus_ibmat[0]. */
        if (AT)
                setpitclock(models[model]->cpu[cpu_manufacturer].cpus[cpu].rspeed);
        else
                setpitclock(14318184.0);
        ali1429_reset();             /* pc.c:191 — G6.3, sur toute machine comme le C */

        /* pc.c:397 — loadnvr(), ET ELLE MANQUAIT. Le harnais l'omettait, ce qui
         * laissait le CMOS a ZERO au lieu de la branche « pas de fichier » de
         * loadnvr : 0xFF partout sauf la date du 1er janvier 1980.
         *
         * SANS ELLE LE POST TOMBE DANS DE L'UB DE PCem. nvrram[RTC_MONTH] valant 0,
         * rtc_recalc() appelle rtc_get_days(0, ...) qui fait
         * `rtc_days_in_month[org_month - 1]`, donc l'indice -1 : le C lit l'octet qui
         * precede le tableau et continue, le C# leve. Les deux cotes ne pouvaient
         * s'accorder que par accident.
         *
         * Le comportement VISIBLE ne change pas : sans fichier at.nvr la somme de
         * controle reste fausse et le POST s'arrete toujours sur « 162-System Options
         * Not Set ». Ce qui change est qu'on prend le chemin que PCem prend, au lieu
         * d'un chemin que personne n'a ecrit. A appeler des DEUX cotes, au meme point.
         *
         * Corrige une affirmation de B1a : nvr.cs disait « l'oracle n'appelle pas
         * loadnvr du tout », et c'etait vrai — ca ne l'est plus. */
        /* pc.c:395 — resetide(), entre pc_reset et loadnvr. Elle charge les images des quatre
           lecteurs IDE si la carte est « ide » (ide.c:278), et ne fait sinon que refermer
           des fichiers jamais ouverts. Celle de pc.c:290 (initpc) ouvre les mêmes images, que
           celle-ci referme puis rouvre (ide.c:268-290) : l'effet net est le sien seul. */
        resetide();

        /* G1.1 — DÉVIATION DE L'ORACLE (PLAN-G1.md, défaut 6c) : loadnvr n'a pas de cas pour la
         * M24 (nvr.c:521-522, `default: return`) ; nvrram garde ce qu'un amorçage précédent du
         * processus y a laissé — la phase 2 du boot-diff, celui de la phase 1. Zéro, comme le C#. */
        if (romset == ROM_OLIM24)
                memset(nvrram, 0, sizeof(nvrram));
        loadnvr();
        ali1429_reset();             /* pc.c:403 — G6.3 */

        /* pc.c:407 — inerte (aucun cache n'est actif), transcrit des deux côtés pour que
         * cpu_update_waitstates() ne repose sur aucun zéro implicite. */
        cpu_cache_int_enabled = cpu_cache_ext_enabled = 0;

        /* pc.c:439 — le VRAI depuis le levier A de M16, inerte : cpu_set() a posé
         * cpu_turbo à 1. Pendant de pc.resetpchard côté C#. */
        cpu_set_turbo(1);

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
        ali1429_reset();             /* pc.c:317 — G6.3, la fin d'initpc */
        return 1;
}

/* h_runpc AIGUILLAIT SUR RIEN, ET C'EST LE SEUL CHEMIN QUI NE LE FAISAIT PAS.
 *
 * h_step() et h_run() branchent sur h_core depuis A2.0 ; h_runpc appelait execx86
 * en dur. Or c'est LUI que la phase 1 de boot-diff emprunte, donc aucun amorçage
 * différentiel ne pouvait tourner sur le cœur 286 — ce qui rendait le bloc B3
 * invérifiable, et le bloc C avec lui puisqu'il n'est exerçable que par un
 * amorçage AT.
 *
 * pc.c:479-487 aiguille sur `is386 ? … : AT ? exec386 : execx86`. Ici is386 est
 * toujours nul (le harnais ne monte pas de 386), donc le pendant est h_core.
 *
 * LE MODE TRACÉ NE FAIT PAS LE GESTE DE h_step286. Celui-ci pose timer_target =
 * tsc pour forcer cycle_period à 1, parce qu'un PAS doit valoir exactement une
 * instruction. Ici on veut la BOUCLE telle qu'elle est : exec386(0) rend la main
 * après une instruction de lui-même, le budget de la tranche restant maître.
 * Poser timer_target changerait le temps que voit l'invité, donc l'amorçage. */
void h_runpc(void) {
        /* pc.c:473 — `cpu_get_speed() / 100`, par h_slice_budget(), la MÊME fonction que
         * l'empreinte CPU confronte au C#. C'était le littéral 4772728 / 100 jusqu'à M16. */
        int cycles_to_run = h_slice_budget();

        if (!h_trace_fp) {
                cpu_state._cycles = 0;
                if (h_exec386())
                        exec386(cycles_to_run);
                else
                        execx86(cycles_to_run);
                return;
        }

        /* Mode tracé : une instruction à la fois, pour pouvoir noter l'état
         * après chacune. Le budget total reste celui de la tranche. */
        int budget = cycles_to_run;
        while (budget > 0) {
                cpu_state._cycles = 1;
                if (h_exec386())
                        exec386(0);
                else
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

void h_set_mem_size(int kb) { h_mem_size_kb = kb; }

void h_set_drive_type(int drive, int type) {
        if (drive < 0 || drive > 1)
                return;
        h_drive_type[drive] = type;
}

void h_set_bpb_disable(int v) { bpb_disable = v; }

/* M10 — la machine. `romset` est une globale du HARNAIS (harness_stubs.c:100),
 * pas de pcem-dev/ : pc.c n'est pas lie. Ecriture directe, meme patron que
 * bpb_disable, et rien a patcher dans l'arbre vendore. A poser avant h_boot :
 * loadbios() le lit depuis l'interieur. */
extern int romset;
void h_set_romset(int r) { romset = r; }

/* G3.0 — les deux chemins que nvrfopen compose (nvr.c:33-54), POUSSES par l'appelant.
 *
 * Ils etaient VIDES : nvr_path et nvr_default_path sont des globales de paths.c, non lie,
 * que harness_stubs.c definit sans jamais les remplir. nvrfopen cherchait donc « /.at.nvr »
 * puis « at.nvr » dans le repertoire courant, ne trouvait rien, et loadnvr prenait la
 * branche « pas de fichier » : 0xFF partout. Le C#, lui, les resout dans initpc
 * (pc.cs:538-543) et lit nvr/default/at.nvr. Mesure : boot-diff --model ibmat divergeait a
 * l'instruction 50, IN AL,71h en F000:0169, oracle 0xFF, C# 0x00. Meme chaine des deux
 * cotes, poussee AVANT h_boot : la resolution appartient au C#, l'oracle la recoit. */
extern char nvr_path[512];         /* harness_stubs.c:736-737 */
extern char nvr_default_path[512];
void h_set_nvr_paths(const char *nvr, const char *nvr_default) {
        strncpy(nvr_path, nvr ? nvr : "", sizeof(nvr_path) - 1);
        nvr_path[sizeof(nvr_path) - 1] = 0;
        strncpy(nvr_default_path, nvr_default ? nvr_default : "", sizeof(nvr_default_path) - 1);
        nvr_default_path[sizeof(nvr_default_path) - 1] = 0;
}

/* --- vidéo (M15) ------------------------------------------------------------
 *
 * gfxcard est une globale de pc.c (pc.c:77) que le harnais définit lui-même
 * (harness_stubs.c) : même patron que romset. À poser avant h_boot. */
void h_set_gfxcard(int g) { gfxcard = g; }

static uint64_t h_fnv(uint64_t hash, const uint8_t *p, size_t n) {
        for (size_t i = 0; i < n; i++) {
                hash ^= p[i];
                hash *= 1099511628211ULL;
        }
        return hash;
}

/* L'état de la VGA en fin de course, dans l'ordre de Video.vid_svga.Probe() côté
 * C#. Le diff d'instructions voit tout ce que le CPU relit — une IN 3DA, une lecture
 * de VRAM — et RIEN de ce qu'il ne relit pas : la palette, la police du plan 2, les
 * pixels. Ce vecteur est l'oracle de tout cela. Les tableaux sont hachés ; les
 * scalaires sont rendus bruts, pour que la sonde NOMME le champ divergent.
 *
 * Rend tout à zéro sans carte svga : la sonde est alors muette, pas fausse. */
void h_vga_probe(uint64_t *out) {
        const uint64_t basis = 1469598103934665603ULL;
        svga_t *svga = svga_get_pri();
        uint32_t pl[512];
        int f = 0, c;

        memset(out, 0, H_VGA_PROBE_N * sizeof(uint64_t));
        if (!svga) {
                h_m24_probe(out);   /* G1.1 — la M24, si elle est montée (champ 0 = 2) */
                h_pc1512_probe(out); /* G1.2 — le PC1512 (champ 0 = 3) */
                h_mda_probe(out);   /* G9.0 — la MDA (champ 0 = 4) */
                return;
        }

        /* pallook en petit-boutiste explicite : le C# hache les mêmes octets. */
        for (c = 0; c < 512; c++)
                pl[c] = svga->pallook[c];

        out[f++] = 1;
        out[f++] = h_fnv(basis, svga->vram, svga->vram_max);
        out[f++] = h_fnv(basis, svga->changedvram, 0x1000000 >> 12);
        out[f++] = h_fnv(basis, buffer32->dat, 2048 * 2048 * 4);
        out[f++] = h_fnv(basis, svga->crtc, sizeof(svga->crtc));
        out[f++] = h_fnv(basis, svga->seqregs, sizeof(svga->seqregs));
        out[f++] = h_fnv(basis, svga->gdcreg, sizeof(svga->gdcreg));
        out[f++] = h_fnv(basis, svga->attrregs, sizeof(svga->attrregs));
        out[f++] = h_fnv(basis, (const uint8_t *)svga->vgapal, 256 * 3);
        out[f++] = h_fnv(basis, (const uint8_t *)pl, sizeof(pl));
        out[f++] = h_fnv(basis, svga->egapal, sizeof(svga->egapal));
        out[f++] = svga->miscout;
        out[f++] = svga->crtcreg;
        out[f++] = (uint64_t)(int64_t)svga->seqaddr;
        out[f++] = (uint64_t)(int64_t)svga->gdcaddr;
        out[f++] = (uint64_t)(int64_t)svga->attraddr;
        out[f++] = (uint64_t)(int64_t)svga->attrff;
        out[f++] = (uint64_t)(int64_t)svga->attr_palette_enable;
        out[f++] = svga->dac_mask;
        out[f++] = svga->dac_status;
        out[f++] = (uint64_t)(int64_t)svga->dac_read;
        out[f++] = (uint64_t)(int64_t)svga->dac_write;
        out[f++] = (uint64_t)(int64_t)svga->dac_pos;
        out[f++] = (uint64_t)svga->la | ((uint64_t)svga->lb << 8) | ((uint64_t)svga->lc << 16) |
                   ((uint64_t)svga->ld << 24);
        out[f++] = (uint64_t)(int64_t)svga->writemode;
        out[f++] = (uint64_t)(int64_t)svga->readmode;
        out[f++] = (uint64_t)(int64_t)svga->readplane;
        out[f++] = (uint64_t)(int64_t)svga->chain4;
        out[f++] = (uint64_t)(int64_t)svga->chain2_write;
        out[f++] = (uint64_t)(int64_t)svga->chain2_read;
        out[f++] = svga->writemask;
        out[f++] = svga->plane_mask;
        out[f++] = svga->charseta;
        out[f++] = svga->charsetb;
        out[f++] = svga->banked_mask;
        out[f++] = svga->mapping.base;
        out[f++] = svga->mapping.size;
        out[f++] = svga->cgastat;
        out[f++] = (uint64_t)(int64_t)svga->vc;
        out[f++] = (uint64_t)(int64_t)svga->sc;
        out[f++] = (uint64_t)(int64_t)svga->displine;
        out[f++] = (uint64_t)(int64_t)svga->linepos;
        out[f++] = (uint64_t)(int64_t)svga->dispon;
        out[f++] = svga->ma;
        out[f++] = svga->maback;
        out[f++] = svga->ca;
        out[f++] = (uint64_t)(int64_t)svga->vtotal;
        out[f++] = (uint64_t)(int64_t)svga->dispend;
        out[f++] = (uint64_t)(int64_t)svga->hdisp;
        out[f++] = (uint64_t)(int64_t)svga->htotal;
        out[f++] = svga->dispontime;
        out[f++] = svga->dispofftime;
        out[f++] = svga->timer.ts_integer;
        out[f++] = svga->timer.ts_frac;
        out[f++] = (uint64_t)(int64_t)svga->fullchange;
        out[f++] = (uint64_t)(int64_t)svga->frames;
        out[f++] = (uint64_t)(int64_t)svga->firstline;
        out[f++] = (uint64_t)(int64_t)svga->lastline;
        out[f++] = (uint64_t)(int64_t)xsize;
        out[f++] = (uint64_t)(int64_t)ysize;
        out[f++] = (uint64_t)(int64_t)svga->video_res_x;
        out[f++] = (uint64_t)(int64_t)svga->video_res_y;
        out[f++] = (uint64_t)(int64_t)svga->video_bpp;
        out[f++] = (uint64_t)(int64_t)svga->blink;
        /* M19 — ce que la Trident rend atteignable et que les 64 premiers ne voyaient pas. */
        out[f++] = svga->read_bank;
        out[f++] = svga->write_bank;
        out[f++] = (uint64_t)(int64_t)svga->bpp;
        out[f++] = svga->vram_display_mask;
        out[f++] = svga->vram_mask;
        out[f++] = (uint64_t)(int64_t)svga->rowoffset;
        out[f++] = svga->ma_latch;
        out[f++] = (uint64_t)(int64_t)svga->interlace;
        out[f++] = (uint64_t)(int64_t)svga->lowres;
        out[f++] = (uint64_t)(int64_t)svga->hdisp_time;
        memcpy(&out[f++], &svga->clock, sizeof(uint64_t));
        h_tvga_probe(svga, &out[f]);
        h_cl5429_probe(svga, &out[f + 11]);   /* G7.1 */
        h_s3_probe(svga, &out[f + 27]);       /* G7.3 */
}

/* Pointeur direct sur la VRAM de l'oracle, NULL sans carte svga : la sonde VGA y lit
 * l'écran texte SANS passer par svga_read, qui mettrait à jour les verrous la..ld et
 * facturerait des cycles — lire l'écran changerait la machine. */
uint8_t *h_vga_vram(void) {
        svga_t *svga = svga_get_pri();
        return svga ? svga->vram : NULL;
}

/* --- disque dur (M12) -------------------------------------------------------
 *
 * La carte est ajoutee comme la CGA l'est : device_add direct, sans traverser le
 * registre HDD_CONTROLLER de hdd.c -- seize cartes et quinze device_t que l'oracle
 * ne lie pas. hdd_controller_init (hdd.c:128-140) se reduit alors a son unique
 * effet, et c'est le miroir exact de pc.resetpchard() cote C#.
 *
 * Un nom inconnu, y compris le "" par defaut, ne monte aucune carte et ne dit
 * rien : le `fatal` de hdd.c:140 est COMMENTE chez PCem. */
/* Geometrie et image d'un disque, a poser AVANT h_boot : xebec_init les lit par
 * hdd_load des sa construction. Patron de h_set_discfn. */
void h_set_hdd(int drive, const char *fn, int spt, int hpc, int tracks) {
        if (drive < 0 || drive > 6)
                return;
        strncpy(ide_fn[drive], fn ? fn : "", sizeof(ide_fn[drive]) - 1);
        ide_fn[drive][sizeof(ide_fn[drive]) - 1] = 0;
        hdc[drive].spt = spt;
        hdc[drive].hpc = hpc;
        hdc[drive].tracks = tracks;
}

/* Le nom INTERNE de la carte : "mfm_xebec", "dtc5150x", ou rien. */
/* G8.1 — la carte son, par son internal_name (pc.c:666-670 : la clé sndcard). Le registre
 * SOUND_CARD n'est pas lié (sound.c) : h_boot monte la carte nommée, après speaker_init comme
 * sound_card_init (pc.c:383). Vide ou « none » : aucune. */
void h_set_sndcard(const char *name) {
        strncpy(h_sndcard_name, name ? name : "", sizeof(h_sndcard_name) - 1);
        h_sndcard_name[sizeof(h_sndcard_name) - 1] = 0;
}

/* La sonde du son (G8.1) : les neuf champs du haut-parleur (h_speaker_probe, sound_hash compris),
 * puis l'état des deux OPL (harness_dbopl.cpp). H_SOUND_PROBE_N champs. */
void h_opl_state(int nr, uint64_t *out);
void h_speaker_probe(uint64_t *out);
/* G8.2 — les vingt champs du DSP et du mélangeur de la SB Pro v2, dans l'ordre de
 * Sound.sound_sb.ProbeSb() côté C#. La carte est retrouvée dans le registre des devices. */
extern device_t *devices[];
extern void *device_priv[];
static uint64_t h_sb_fnv(const uint8_t *p, size_t n) {
        uint64_t hash = 1469598103934665603ULL;
        for (size_t i = 0; i < n; i++) {
                hash ^= p[i];
                hash *= 1099511628211ULL;
        }
        return hash;
}
static void h_sb_probe(uint64_t *o) {
        sb_t *sb = NULL;
        sb_dsp_t *d;
        int c;

        for (c = 0; c < DEV_MAX; c++)
                if (devices[c] == &sb_pro_v2_device) {
                        sb = (sb_t *)device_priv[c];
                        break;
                }
        if (!sb)
                return;
        d = &sb->dsp;
        *o++ = (uint32_t)d->sb_8_length | ((uint64_t)(uint32_t)d->sb_8_autolen << 32);
        *o++ = (uint8_t)d->sb_8_format | ((uint64_t)(uint8_t)d->sb_8_autoinit << 8) | ((uint64_t)(uint8_t)d->sb_8_pause << 16) |
               ((uint64_t)(uint8_t)d->sb_8_enable << 24);
        *o++ = (uint8_t)d->sb_8_output | ((uint64_t)(uint8_t)d->sb_8_dmanum << 8) | ((uint64_t)(uint8_t)d->sb_speaker << 16) |
               ((uint64_t)(uint8_t)d->muted << 24);
        *o++ = (uint64_t)(int64_t)d->sb_pausetime;
        *o++ = (uint32_t)d->sb_read_wp | ((uint64_t)(uint32_t)d->sb_read_rp << 32);
        *o++ = h_sb_fnv(d->sb_read_data, sizeof(d->sb_read_data));
        *o++ = (uint32_t)d->sb_data_stat | ((uint64_t)(uint32_t)d->sb_irqnum << 32);
        *o++ = d->sbe2 | ((uint64_t)(uint32_t)d->sbe2count << 8) | ((uint64_t)d->sb_addr << 48);
        *o++ = (uint16_t)d->sbdat | ((uint64_t)(uint32_t)d->sbdat2 << 32);
        *o++ = (uint16_t)d->sbdatl | ((uint64_t)(uint16_t)d->sbdatr << 16) | ((uint64_t)d->sbref << 32) | ((uint64_t)(uint8_t)d->sbstep << 40);
        *o++ = (uint32_t)d->sbdacpos | ((uint64_t)(uint32_t)d->sbleftright << 32);
        *o++ = (uint8_t)d->sbreset | ((uint64_t)d->sbreaddat << 8) | ((uint64_t)d->sb_command << 16) | ((uint64_t)d->sb_test << 24);
        *o++ = (uint32_t)d->sb_timeo | ((uint64_t)(uint32_t)d->sb_timei << 32);
        *o++ = d->sblatcho;
        *o++ = d->output_timer.ts_integer | ((uint64_t)d->output_timer.ts_frac << 32);
        *o++ = (uint32_t)d->stereo | ((uint64_t)(uint32_t)d->wb_full << 32);
        *o++ = (uint32_t)d->busy_count | ((uint64_t)(uint32_t)d->pos << 32);
        *o++ = h_sb_fnv(sb->mixer_sbpro.regs, sizeof(sb->mixer_sbpro.regs));
        *o++ = (uint32_t)sb->pos;
        *o++ = (uint32_t)sb->mixer_sbpro.master_l | ((uint64_t)(uint32_t)sb->mixer_sbpro.master_r << 32);
}

void h_sound_probe(uint64_t *out) {
        memset(out, 0, H_SOUND_PROBE_N * sizeof(uint64_t));
        h_speaker_probe(out);
        h_opl_state(0, out + 9);
        h_opl_state(1, out + 15);
        h_sb_probe(out + 21);       /* G8.2 */
}

void h_set_hdd_controller(const char *name) {
        strncpy(hdd_controller_name, name ? name : "", sizeof(hdd_controller_name) - 1);
        hdd_controller_name[sizeof(hdd_controller_name) - 1] = 0;
}

/* --- clavier (M11) : injecter une frappe dans l'oracle ----------------------
 *
 * Le chemin d'ECRITURE du contrôleur de disquettes n'a jamais été sous oracle,
 * et la raison tenait ici : le harnais ne savait pas taper. FORMAT et WRITE DATA
 * ne s'atteignent qu'en tapant une commande sous DOS, donc § M7.1 et § M8.1 les
 * ont exercés côté C# SEUL — « une preuve d'usage, pas une preuve de fidélité ».
 *
 * pcem_key[] et rawinputkey[] sont définis par harness_stubs.c, qui tient lieu
 * de wx-sdl2-keyboard.c. On y ajoute les deux seules fonctions qui manquaient. */
extern uint8_t pcem_key[272];
extern int rawinputkey[272];
void keyboard_process(void);

/* Pendant exact de ce que Host/SdlKeyboard écrit depuis la pompe SDL, et de ce
 * que BootTest écrit à la main : un horodatage à l'appui, 0 au relâchement. */
void h_rawinputkey(int idx, int val) {
        if (idx < 0 || idx >= 272)
                return;
        rawinputkey[idx] = val;
}

/* wx-sdl2-keyboard.c:11-16 (keyboard_poll_host, quatre lignes) puis
 * keyboard_process() — l'ordre de pc.c:490-491, où runpc() les appelle APRES
 * execx86. h_runpc ne les appelle pas : l'oracle n'a pas de couche hôte, et les
 * y mettre changerait toutes les mesures déjà consignées. C'est donc l'outil de
 * diff qui déclenche, au même point de la tranche que le C#. */
void h_kbd_process(void) {
        int c;

        for (c = 0; c < 272; ++c)
                pcem_key[c] = rawinputkey[c] > 0;

        keyboard_process();
}

/* --- disquette (M6) --------------------------------------------------------- */

/* Pendant de pc.closepc() (pc.c:584-585), et de la MEME nécessité : img_writeback
 * écrit dans le FILE* sans fflush, et c'est le fclose de img_close qui pousse. Sans
 * cet appel, comparer les deux images après un FORMAT compare un fichier vidé à un
 * fichier qui ne l'est pas — une divergence entièrement fabriquée par le harnais.
 *
 * Le DISQUE DUR a la MEME necessite -- mesure cote C# : un FDISK seul ecrit 512 octets
 * qui ne sortent jamais du tampon, et l'image reste a zero. Mais il ne peut pas etre
 * vide de la meme facon des deux cotes.
 *
 * DEVIATION: fflush(NULL) la ou pc.closepc() appelle device_close_all() (pc.c:589).
 *   Le pendant fidele a ete ecrit, puis RETIRE apres plantage : device_close_all()
 *   ferme la CGA avant la carte, cga_close() fait free(cga) SANS mem_mapping_remove
 *   (vid_cga.c:441-446), le mem_mapping_t de la CGA reste donc chaine dans la liste
 *   globale en pointeur pendant, et le rom_deinit de xebec_close le traverse --
 *   mem_mapping_remove boucle « while (dest != mapping) » sans garde de fin de liste
 *   (mem.c:1166-1170). Trace obtenue sous gdb :
 *       #0 mem_mapping_remove  mem.c:1170
 *       #1 rom_deinit          rom.c:113
 *       #2 xebec_close         mfm_xebec.c:769
 *       #3 device_close_all    device.c:40
 *   C'est un defaut de PCem, atteignable chez lui : closepc() est bien appele
 *   (wx-sdl2.c:649), et quitter PCem sur une machine CGA + Xebec suit ce chemin.
 *   Registre : PB-31.
 *
 *   Le cote C#, lui, garde device_close_all() et ne plante pas : rien n'y est libere,
 *   la liste de mappages reste parcourable, et mem_mapping_remove trouve sa cible.
 *   Meme famille que h_pad_ram -- ce qui diverge est de la gestion memoire manuelle,
 *   pas un comportement emule. Ce qui compte pour la comparaison d'images est que les
 *   octets soient sur le disque des deux cotes, et fflush(NULL) le garantit : il vide
 *   TOUS les flux ouverts en ecriture du processus, disque dur compris. */
void h_closepc(void) {
        disc_close(0);
        disc_close(1);
        fflush(NULL);
}

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

/* --- sonde haut-parleur (M9) ------------------------------------------------
   Neuf champs, ordre identique à Sound.sound_speaker.Probe() côté C#.

   speaker_pos est `static` dans sound_speaker.c : lisible ici, et SEULEMENT ici,
   parce que ce .c est compilé dans cette unité de traduction. C'est le curseur
   de rattrapage du générateur — une désynchronisation s'y voit avant de s'entendre.

   Le neuvième champ est l'empreinte du son réellement produit, cumulée bloc par
   bloc dans sound_poll. Une empreinte identique des deux côtés ne prouve rien si
   elle est restée à sa graine : le C# doit vérifier qu'elle a BOUGÉ. */
void h_speaker_probe(uint64_t *out) {
        out[0] = (uint64_t)(int64_t)speaker_gated;
        out[1] = (uint64_t)(int64_t)speaker_enable;
        out[2] = (uint64_t)(int64_t)was_speaker_enable;
        out[3] = (uint64_t)(int64_t)speakon;
        out[4] = (uint64_t)(int64_t)speakval;
        out[5] = (uint64_t)(int64_t)ppispeakon;
        out[6] = (uint64_t)(int64_t)speaker_pos;
        out[7] = (uint64_t)(int64_t)sound_pos_global;
        out[8] = h_sound_hash;
}
