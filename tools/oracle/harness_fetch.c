/* SPDX-FileCopyrightText: 2026 Julien Peloille
 * SPDX-License-Identifier: GPL-2.0-only
 *
 * A2.2a — LE CHEMIN DE FETCH DE exec386, exposé pour être diffé.
 *
 * ------------------------------------------------------------------------------
 * POURQUOI UNE UNITÉ DE TRADUCTION À PART
 *
 * fastreadb/w/l, getbyte/getword/getlong et la famille geteab sont des
 * `static inline` de 386_common.h : ils n'existent dans aucun objet de la .so tant
 * que personne ne les instancie. Il faut donc inclure l'en-tête.
 *
 * Mais harness.c ne le peut PAS : il fait `#include "808x.c"`, et 808x.c définit ses
 * propres getword, geteab, geteaw, geteal, geteaq et seteaq. Inclure 386_common.h
 * après lui donne six redéfinitions. Les deux jeux de noms coexistent chez PCem
 * précisément parce qu'ils vivent dans des unités de traduction différentes —
 * 808x.c d'un côté, 386_dynarec.c de l'autre. On reproduit cette séparation ici au
 * lieu de la contourner.
 *
 * Même motif que harness_wrap.c et harness_stubs.c.
 * ------------------------------------------------------------------------------
 *
 * CE QUE CETTE PORTE SERT À PROUVER
 *
 * exec386 fait `fastreadl(cs + pc)` à CHAQUE instruction (386.c:176) : c'est le
 * chemin d'instruction inconditionnel, mode réel compris. Il traverse getpccache,
 * le cache de page pccache/pccache2, et l'arithmétique de BIAIS qui fait qu'un
 * pointeur décalé relu avec l'adresse virtuelle complète tombe au bon octet.
 *
 * Côté C# ce biais est transcrit en porteur + offset. Une erreur d'une page y est
 * parfaitement silencieuse : elle rend des octets plausibles, pris au mauvais
 * endroit. Sans cette porte, elle ne deviendrait observable qu'une fois exec386
 * transcrit — c'est-à-dire mêlée à cent autres changements, donc illisible.
 */

#include <stdio.h>
#include <stdint.h>
#include <stdlib.h>

/* L'ordre est celui de 386_dynarec.c:1-28, la seule unité de PCem qui inclut
 * 386_common.h ; codegen.h et codegen_backend.h en moins, le recompilateur n'étant
 * pas lié (voir le Makefile). */
#include "ibm.h"
#include "x86.h"
#include "x86_ops.h"
#include "x87.h"
#include "mem.h"
#include "cpu.h"
#include "timer.h"
#include "386_common.h"

uint32_t h_fastreadl(uint32_t a) { return fastreadl(a); }

uint32_t h_fastreadw(uint32_t a) { return fastreadw(a); }

uint32_t h_fastreadb(uint32_t a) { return fastreadb(a); }

/* L'état du cache de page. Comparer les octets rendus ne suffit pas : deux
 * implémentations peuvent rendre les mêmes valeurs en franchissant les pages à des
 * moments différents, et c'est le franchissement qui repose cpu_prefetch_cycles —
 * donc le temps. */
uint32_t h_pccache(void) { return pccache; }
