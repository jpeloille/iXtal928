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

/* Positionne CS:IP (et la base de segment correspondante) avant exécution. */
void h_set_cs_ip(uint16_t cs_sel, uint16_t ip);

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

/* Pointeur direct sur la RAM de l'oracle (1 Mo), pour les cas où le C# doit
 * la comparer en entier. */
uint8_t *h_ram(void);

/* Version du contrat. Incrémentée dès que h_state change de forme, pour qu'un
 * .so périmé échoue bruyamment au lieu de marshaler du charabia. */
#define H_ABI_VERSION 1
uint32_t h_abi_version(void);

#ifdef __cplusplus
}
#endif

#endif /* IXTAL26_HARNESS_H */
