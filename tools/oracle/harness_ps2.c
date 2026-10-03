/* SPDX-FileCopyrightText: 2026 Julien Peloille
 * SPDX-License-Identifier: GPL-2.0-only
 *
 * PS2.0 — LA SOURIS PS/2, INCLUSE ET NON LIÉE, pour la raison de harness_tvga.c : la
 * mouse_ps2_t est PRIVÉE de mouse_ps2.c, et la sonde de fin de boot-diff doit en lire l'état.
 * mouse_ps2.c ne figure donc pas dans SRC et se compile ICI.
 *
 * Le pointeur de la souris montée : mouse_ps2_init le passe à keyboard_at_set_mouse
 * (mouse_ps2.c:227). Le temps de l'inclusion, ce nom désigne h_ps2_set_mouse, qui le retient
 * puis appelle le vrai. Le texte vendoré n'est pas touché.
 */

#include <stdint.h>
#include <string.h>

#define keyboard_at_set_mouse h_ps2_set_mouse
#include "../../pcem-dev/src/mouse/mouse_ps2.c"
#undef keyboard_at_set_mouse

void keyboard_at_set_mouse(void (*mouse_write)(uint8_t val, void *p), void *p); /* keyboard_at.c:850 */

static mouse_ps2_t *h_ps2;

void h_ps2_set_mouse(void (*mouse_write)(uint8_t val, void *p), void *p) {
        h_ps2 = (mouse_ps2_t *)p;
        keyboard_at_set_mouse(mouse_write, p);
}

/* À chaque h_boot, avant mouse_emu_init : une souris d'un amorçage précédent (libérée par
 * mouse_ps2_close) ne doit pas répondre à la sonde. */
void h_ps2_forget(void) { h_ps2 = NULL; }

/* La file de la souris du 8042 (keyboard_at.c:87-88). */
extern uint8_t mouse_queue[16];
extern int mouse_queue_start, mouse_queue_end;

static uint64_t h_ps2_fnv(const uint8_t *p, int n) {
        uint64_t h = 1469598103934665603ULL;
        int c;
        for (c = 0; c < n; c++)
                h = (h ^ p[c]) * 1099511628211ULL;
        return h;
}

/* La sonde de la souris PS/2 : neuf champs, dans l'ordre de Mouse/mouse_ps2.cs, ProbeState. */
void h_mouse_probe(uint64_t *o) {
        mouse_ps2_t *m = h_ps2;

        memset(o, 0, 9 * sizeof(uint64_t));
        o[0] = (uint64_t)(uint32_t)mouse_scan | ((uint64_t)(m ? 1u : 0u) << 32);
        o[1] = (uint32_t)mouse_queue_start | ((uint64_t)(uint32_t)mouse_queue_end << 32);
        o[2] = h_ps2_fnv(mouse_queue, 16);
        if (!m)
                return;
        o[3] = (uint32_t)m->mode | ((uint64_t)m->flags << 32) | ((uint64_t)m->resolution << 40) |
               ((uint64_t)m->sample_rate << 48);
        o[4] = m->command | ((uint64_t)(uint32_t)m->cd << 32);
        o[5] = (uint32_t)m->x | ((uint64_t)(uint32_t)m->y << 32);
        o[6] = (uint32_t)m->z | ((uint64_t)(uint32_t)m->b << 32);
        o[7] = (uint32_t)m->is_intellimouse | ((uint64_t)(uint32_t)m->intellimouse_mode << 32);
        o[8] = h_ps2_fnv(m->last_data, 6);
}
