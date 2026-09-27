/* SPDX-FileCopyrightText: 2026 Julien Peloille
 * SPDX-License-Identifier: GPL-2.0-only
 *
 * ------------------------------------------------------------------------------
 * POURQUOI ON INCLUT 386_dynarec.c ET PAS SEULEMENT SON OBJET
 *
 * Le modèle de temps de préfetch de l'INTERPRÉTEUR vit dans deux `static` de
 * 386_dynarec.c :
 *
 *     static int prefetch_bytes = 0;      (386_dynarec.c:152)
 *     static int prefetch_prefixes = 0;   (386_dynarec.c:153)
 *
 * Aucun accesseur ne les exporte. Liés normalement, ils sont invisibles — et ce
 * sont exactement les pendants 286 de memcycs, fetchcycles et prefetchqueue pour
 * le 8088, que le vecteur d'état compare depuis M1. Les laisser dehors, c'est
 * rouvrir le risque n°1 du plan sur le cœur neuf.
 *
 * C'est le MÊME remède que pour 808x.c, et pour la même raison. 386_dynarec.c
 * cesse donc de figurer dans SRC du Makefile : il est compilé ICI, sans quoi
 * chaque symbole serait défini deux fois.
 *
 * MESURE QUI A CONDUIT ICI. Un cas dirigé — `MOV [FFFF], AX` — rendait
 * « cycles consommés : oracle 24, C# 20 ». Les deux côtés avaient accumulé des
 * prefetch_bytes différents, sans qu'aucun champ comparé ne le dise. Le fuzzeur
 * ne l'aurait pas vu : tant que les deux exécutent la même suite d'instructions,
 * ils dérivent ensemble.
 * ------------------------------------------------------------------------------
 */

#include <stdint.h>

#include "../../pcem-dev/src/cpu/386_dynarec.c"

int h_prefetch_bytes(void) { return prefetch_bytes; }
int h_prefetch_prefixes(void) { return prefetch_prefixes; }

/* Remis à zéro au reset, des DEUX côtés. PCem ne le fait jamais : ce sont des
 * statiques de BSS et il n'amorce qu'une fois par processus. Le harnais, lui,
 * réinitialise en boucle — sans cela le second départ hérite du premier. */
void h_prefetch_reset(void) {
        prefetch_bytes = 0;
        prefetch_prefixes = 0;
}

/* G2, D0.5 — materialise les drapeaux paresseux, pour la sonde SST 386 seule : son
 * corpus donne EFLAGS en clair, et flags est perime entre deux reconstructions. Le
 * fuzzeur, lui, compare la representation paresseuse et ne l'appelle PAS. */
void h_flags_rebuild(void) { flags_rebuild(); }
