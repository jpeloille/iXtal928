/* SPDX-FileCopyrightText: 2026 Julien Peloille
 * SPDX-License-Identifier: GPL-2.0-only
 *
 * Contrat entre l'oracle C et le harnais C#.
 *
 * L'oracle compile le VRAI cœur 8088 de PCem (pcem-dev/src/cpu/808x.c) dans une
 * bibliothèque partagée, et expose de quoi le piloter instruction par instruction.
 * C'est la seule source de vérité pour la comptabilité de cycles : SingleStepTests
 * valide l'état architectural, pas le modèle de temps inventé par PCem.
 *
 * Le point clé : harness.c fait `#include "808x.c"`. Sans ça, `memcycs`,
 * `fetchcycles`, `fetchclocks`, `prefetchw`, `prefetchpc`, `prefetchqueue[]`,
 * `nextcyc`, `cycdiff` et `tsc_frac` sont `static` et donc invisibles — et la
 * mitigation du risque n°1 du plan (dérive temporelle silencieuse) n'existe pas.
 */

#ifndef IXTAL26_HARNESS_H
#define IXTAL26_HARNESS_H

#include <stdint.h>

#define H_WLOG_MAX 16
#define H_RAM_SIZE 0x100000u /* 1 Mo — l'espace d'adressage complet du 8088 */

/* x86.h aplatit cpu_state en macros (`#define cycles cpu_state._cycles`, x86.h:122,
 * et la famille cs/CS/cr0/msw). Si cet en-tête est inclus APRÈS x86.h — ce que fait
 * harness_stubs.c — le champ `cycles` de h_state se ferait réécrire en
 * `cpu_state._cycles` et la structure ne compilerait pas.
 *
 * On neutralise donc ces noms ici. `#undef` d'une macro non définie est légal, donc
 * cet en-tête reste utilisable seul. Les fichiers qui ont besoin des macros de PCem
 * incluent x86.h après, ou écrivent cpu_state._cycles explicitement (cf. harness.c). */
#undef cycles
#undef cr0
#undef msw

/* Ordre des segments dans les tableaux ci-dessous. Fixé, partagé avec le C#. */
enum { H_SEG_CS = 0, H_SEG_DS, H_SEG_ES, H_SEG_SS, H_SEG_FS, H_SEG_GS, H_SEG_COUNT };

/* Disposition explicite, pas de padding implicite : le C# marshale ça tel quel.
 * Tout champ ajouté ici doit l'être aussi côté C#, et entrer dans le vecteur
 * diffé — un champ non comparé est un champ où la dérive se cache. */
typedef struct h_state {
        /* --- état architectural ------------------------------------------ */
        uint32_t regs[8];             /* .l de chaque x86reg : AX CX DX BX SP BP SI DI */
        uint32_t seg_base[H_SEG_COUNT];
        uint16_t seg_sel[H_SEG_COUNT];
        uint16_t flags;
        uint16_t eflags;
        uint16_t prefetchpc;
        uint32_t pc;
        uint32_t oldpc;
        uint32_t eaaddr;
        int32_t ea_seg_idx;           /* lequel des 6 segments ea_seg vise ; -1 sinon */
        int32_t ssegs;
        int32_t abrt;

        /* --- comptabilité de cycles (les `static` de 808x.c) -------------- */
        int32_t cycles;
        uint64_t tsc;
        uint64_t tsc_frac;
        int32_t memcycs;
        int32_t fetchcycles;
        int32_t fetchclocks;
        int32_t nextcyc;
        int32_t cycdiff;
        int32_t current_diff;
        int32_t prefetchw;
        uint8_t prefetchqueue[6];
        uint8_t _pad[2];

        /* --- état d'interruption ----------------------------------------- */
        int32_t noint;
        int32_t inhlt;
        int32_t takeint;

        /* --- compteurs d'appels des stubs --------------------------------
         * Mitigation du risque n°3 : le cas dangereux n'est pas le désaccord
         * (bruyant) mais l'accord vide — les deux côtés stubent à la même
         * constante et une divergence dans le chemin qui la consomme est
         * masquée. « Aucun côté n'a jamais appelé inb » se lit comme un accord.
         * Ces compteurs font partie du vecteur diffé, donc zéro-des-deux-côtés
         * sur un stub que le test devait exercer FAIT ÉCHOUER la passe. */
        uint64_t n_inb;
        uint64_t n_outb;
        uint64_t n_picint;
        uint64_t n_picinterrupt;
        uint64_t n_timer_process;
        uint64_t n_readmembl;
        uint64_t n_writemembl;
        uint64_t n_readmemwl;
        uint64_t n_writememwl;
        uint64_t n_fatal;

        /* --- divers ------------------------------------------------------- */
        uint64_t ins;                 /* instructions exécutées depuis h_reset */
} h_state;

#ifdef __cplusplus
extern "C" {
#endif

/* Remet le cœur ET le harnais à l'état post-reset d'un XT 8088 :
 * CS=0xFFFF, pc=0, rammask=0xFFFFF, AT=0, is386/is486/hasfpu=0, is8086=0,
 * tables znp/mod1 reconstruites, file de préfetch vidée, compteurs à zéro. */
void h_reset(void);

/* Écrit len octets à l'adresse physique addr dans la RAM de l'oracle. */
void h_load(uint32_t addr, const uint8_t *buf, uint32_t len);

/* Lit len octets depuis la RAM de l'oracle. */
void h_read(uint32_t addr, uint8_t *buf, uint32_t len);

/* Remplit toute la RAM d'une valeur. SingleStepTests impose 0x90 : « all bytes
 * fetched after the initial instruction bytes are set to 0x90 ». Laisser des
 * zéros change tout opérande lu hors des octets listés dans initial.ram — un
 * diviseur, une source de chaîne — et fabrique des divergences qui ne sont ni
 * celles de PCem ni celles du silicium, mais celles du harnais. */
void h_fill_ram(uint8_t value);
void h_fill_ram2(uint8_t a, uint8_t b);

/* Positionne CS:IP (et la base de segment correspondante) avant exécution. */
void h_set_cs_ip(uint16_t cs_sel, uint16_t ip);

/* Ordre du vecteur de registres de h_setregs / h_getregs. Calqué sur l'objet
 * "regs" de SingleStepTests pour que le chargement d'un cas soit direct. */
enum {
        H_R_AX = 0, H_R_BX, H_R_CX, H_R_DX, H_R_CS, H_R_SS, H_R_DS, H_R_ES,
        H_R_SP, H_R_BP, H_R_SI, H_R_DI, H_R_IP, H_R_FLAGS, H_R_COUNT
};

/* Charge d'un coup les 14 registres architecturaux. Les segments passent par
 * loadcs/loadseg, donc leur base est recalculée (sel << 4 en mode réel), et la
 * file de préfetch est vidée — ce qui est le bon comportement pour un cas SST :
 * on sème la RAM à cs:ip et on ignore le champ "queue", redondant avec elle. */
void h_setregs(const uint16_t r[H_R_COUNT]);

/* Relit les 14 mêmes registres. */
void h_getregs(uint16_t r[H_R_COUNT]);

/* Exécute exactement une instruction et rend le nombre de cycles consommés.
 * Le budget est réarmé à l'intérieur : execx86() boucle tant que cycles > 0,
 * donc on l'appelle avec de quoi faire une seule instruction. */
int h_step(void);

/* Exécute jusqu'à épuisement d'un budget de cycs cycles — la forme qu'emploie
 * runpc() (execx86(cpu_get_speed() / 100), soit 47 727 cycles par tranche de
 * 10 ms sur un XT). Sert à vérifier que h_step() ne fausse pas la comptabilité,
 * et servira au diff plein régime. Rend les cycles réellement consommés. */
int h_run(int cycs);

/* Recopie l'état complet, y compris les `static` de 808x.c. */
void h_getstate(h_state *out);

/* sha256-like : somme de contrôle rapide de la RAM, pour diffé rer sans
 * transférer 1 Mo par instruction. */
uint64_t h_ram_hash(void);

/* --- amorçage machine complète (IBM PC 5150) -----------------------------
 * Miroir exact de pc.initpc() côté C#. Rend 1 si le BIOS a pu être chargé.
 * romspath est le répertoire des ROMs, p.ex. "roms". */
int h_boot(const char *romspath);

/* Exécute une tranche de 10 ms, comme runpc(). */
void h_runpc(void);

/* Trace d'amorçage : hachage d'une ligne d'état par instruction, écrit dans un
 * fichier. C'est la forme « phase 1 » du diff — 8 octets par instruction au lieu
 * de la ligne formatée, donc utilisable sur des millions d'instructions. */
int h_trace_open(const char *path);
void h_trace_close(void);

/* Sonde PIT — 19 champs du canal t, dans l'ordre de Models.pit.Probe() côté C#.
 * `pit` est une globale de pit.c et le harnais est lié avec : on lit l'arbre
 * vendoré, on ne l'instrumente pas. */
#define H_PIT_PROBE_N 19
void h_pit_probe(int t, uint64_t *out);

/* Sonde haut-parleur (M9) — 9 champs, dans l'ordre de
 * Sound.sound_speaker.Probe() côté C#. Le dernier est une empreinte cumulative
 * du son produit : c'est la seule voix du chemin audio dans le diff, et elle ne
 * vaut que si elle a bougé (voir h_speaker_probe dans harness.c). */
#define H_SPEAKER_PROBE_N 9
void h_speaker_probe(uint64_t *out);

/* --- disquette (M6) ---------------------------------------------------------
 * Pendant de discfns[] (fdd.c:6) : pc.c le remplit depuis argv AVANT initpc, et
 * resetpchard le consomme (pc.c:367-368). À appeler avant h_boot. NULL ou "" :
 * lecteur vide. Les types de lecteur sont posés par h_boot (5,25" DD, comme
 * pc.cs) et ne se configurent pas. */
void h_set_discfn(int drive, const char *fn);

/* --- configuration machine (M8) ---------------------------------------------
 * Taille RAM en Ko, pendant de mem_size (mem.c:102) que loadconfig pose AVANT
 * initpc chez PCem (pc.c:694). À appeler avant h_boot ; sans appel, h_boot garde
 * 640, la valeur qu'il portait en dur et que toutes les mesures de
 * VERIFICATION.md supposent.
 *
 * C'est le PATRON de tout paramètre machine configurable : l'outil de diff lit la
 * configuration UNE fois, puis pousse le même scalaire des deux côtés — ici, et
 * dans mem.mem_size côté C#. Laisser chaque côté relire le fichier ouvrirait la
 * porte à deux résolutions de chemin divergentes, soit exactement la panne que la
 * comparaison différentielle existe pour attraper. */
void h_set_mem_size(int kb);

/* Type de lecteur, pendant de fdd_set_type (fdd.c:176) que loadconfig pose AVANT
 * initpc (pc.c:776-777). Sans appel, h_boot garde 1 — 5,25" DD, le lecteur du
 * 5150. Le type gouverne max_track et les drapeaux de densité (fdd.c:38-64),
 * donc fdd_seek et fdd_can_read_medium : un type divergent fait diverger la
 * recherche de piste avant toute lecture de secteur. */
void h_set_drive_type(int drive, int type);

/* Pendant de bpb_disable (disc_img.c:22). Force img_load à deviner la géométrie
 * depuis la taille du fichier au lieu de lire le BPB. */
void h_set_bpb_disable(int v);

/* Sonde disquette — H_DISC_PROBE_N globales de disc.c et fdc.c, dans l'ordre de
 * Floppy.fdc_c.Probe() côté C#. L'instance `fdc` est static dans fdc.c, donc hors
 * de portée sans l'inclure ; les globales suffisent à nommer le champ divergent. */
#define H_DISC_PROBE_N 20
void h_disc_probe(uint64_t *out);

/* Journal d'ecritures de la derniere instruction. Voir harness_stubs.c. */
void h_wlog_reset(void);
int h_wlog_count(void);
uint32_t h_wlog_get_addr(int i);
uint8_t h_wlog_get_val(int i);

/* Pointeur direct sur la RAM de l'oracle (1 Mo), pour les cas où le C# doit
 * la comparer en entier. */
uint8_t *h_ram(void);

/* Version du contrat. Incrémentée dès que h_state change de forme, pour qu'un
 * .so périmé échoue bruyamment au lieu de marshaler du charabia. */
/* 2 depuis M8 : les setters de configuration (h_set_mem_size, h_set_drive_type,
 * h_set_bpb_disable) s'ajoutent au contrat. Un .so bâti avant ne les exporte pas,
 * et le C# doit le dire au lieu de tomber sur un symbole absent. */
#define H_ABI_VERSION 2
uint32_t h_abi_version(void);

/* sizeof(h_state) tel que le compilateur C l'a disposé. Le C# l'assène contre son
 * propre Marshal.SizeOf au démarrage : un décalage de champ entre les deux côtés
 * ne produirait pas d'erreur, il produirait des comparaisons silencieusement
 * fausses — exactement le genre de panne que ce projet existe pour éviter. */
uint32_t h_state_size(void);

#ifdef __cplusplus
}
#endif

#endif /* IXTAL26_HARNESS_H */
