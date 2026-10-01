/* SPDX-FileCopyrightText: 2026 Julien Peloille
 * SPDX-License-Identifier: GPL-2.0-only
 *
 * G7.3 — LA S3 TRIO64 (PHOENIX), INCLUSE ET NON LIÉE, pour la raison de harness_tvga.c et
 * harness_cl5429.c : la s3_t est PRIVÉE de vid_s3.c (vid_s3.c:46-131). vid_s3.c cesse donc de
 * figurer dans SRC et se compile ICI.
 *
 * DEVIATION DE L'ORACLE (décision utilisateur du 01/10, G7 n° 3) : L'ACCÉLÉRATEUR EST SYNCHRONE.
 * PCem le fait tourner dans un thread (fifo_thread, vid_s3.c:840-887), réveillé par
 * thread_set_event(wake_fifo_thread) — un oracle à thread n'est pas déterministe. Ni la
 * bibliothèque de threads ni bus/pci.c ne sont liés à l'oracle : les souches sont donc
 * DÉFINIES ici (pas d'enveloppe --wrap, il n'y aurait pas de __real à appeler).
 * thread_set_event(wake_fifo_thread) vide la FIFO sur-le-champ par le corps de la boucle de
 * fifo_thread, recopié dans h_s3_drain ; les attentes rendent la main. Le PCem vendoré reste
 * intact. Effet observable, le même côté C# (Video/vid_s3.cs) : la carte n'est jamais vue
 * « occupée » (9AE8/9AE9 voient la FIFO vide, force_busy mis à part) et la FIFO ne se remplit
 * jamais — s3_queue réveille dès qu'elle compte moins de 8 entrées (:911-912), donc à chaque
 * entrée. timer_read et blitter_time (statistiques d'affichage, :849, :880-881) sont omis.
 */

#include <stdint.h>

#include "../../pcem-dev/src/video/vid_s3.c"

/* Une seule S3 par machine : celle que thread_create a vue naître. */
static s3_t *h_s3;
static int h_s3_draining;

/* pcem: vid_s3.c:847-885 — le corps de la boucle de fifo_thread, sans l'attente ni
 * timer_read. Réentrance gardée : une écriture vidée qui réveillerait la FIFO n'y replonge pas. */
static void h_s3_drain(s3_t *s3) {
        if (h_s3_draining)
                return;
        h_s3_draining = 1;
        s3->blitter_busy = 1;
        while (!FIFO_EMPTY) {
                fifo_entry_t *fifo = &s3->fifo[s3->fifo_read_idx & FIFO_MASK];

                switch (fifo->addr_type & FIFO_TYPE) {
                case FIFO_WRITE_BYTE:
                        s3_accel_write_fifo(s3, fifo->addr_type & FIFO_ADDR, fifo->val);
                        break;
                case FIFO_WRITE_WORD:
                        s3_accel_write_fifo_w(s3, fifo->addr_type & FIFO_ADDR, fifo->val);
                        break;
                case FIFO_WRITE_DWORD:
                        s3_accel_write_fifo_l(s3, fifo->addr_type & FIFO_ADDR, fifo->val);
                        break;
                case FIFO_OUT_BYTE:
                        s3_accel_out_fifo(s3, fifo->addr_type & FIFO_ADDR, fifo->val);
                        break;
                case FIFO_OUT_WORD:
                        s3_accel_out_fifo_w(s3, fifo->addr_type & FIFO_ADDR, fifo->val);
                        break;
                case FIFO_OUT_DWORD:
                        s3_accel_out_fifo_l(s3, fifo->addr_type & FIFO_ADDR, fifo->val);
                        break;
                }

                s3->fifo_read_idx++;
                fifo->addr_type = FIFO_INVALID;
        }
        s3->blitter_busy = 0;
        s3->subsys_stat |= INT_FIFO_EMP;
        s3_update_irqs(s3);
        h_s3_draining = 0;
}

/* --- les souches de thread.h ------------------------------------------------------------- */
/* Des événements distincts et non nuls ; le thread n'est jamais lancé, son paramètre est noté. */
static int h_events[64];
static int h_n_events;

event_t *thread_create_event(void) {
        if (h_n_events >= 64)
                fatal("thread_create_event : plus de 64 evenements (oracle)\n");
        return &h_events[h_n_events++];
}
thread_t *thread_create(void (*thread_rout)(void *param), void *param) {
        if (thread_rout != fifo_thread)
                fatal("thread_create : seul le fifo_thread de la S3 est connu de l'oracle\n");
        h_s3 = (s3_t *)param;
        return (thread_t *)param;
}
void thread_set_event(event_t *event) {
        if (h_s3 && event == h_s3->wake_fifo_thread)
                h_s3_drain(h_s3);
}
void thread_reset_event(event_t *event) { (void)event; }
int thread_wait_event(event_t *event, int timeout) {
        (void)event;
        (void)timeout;
        return 0;
}
void thread_kill(thread_t *handle) {
        (void)handle;
        h_s3 = NULL;
}
void thread_destroy_event(event_t *event) { (void)event; }

/* timer_read / timer_freq (ibm.h:395-396) : l'horloge de l'HÔTE, que vid_s3.c ne lit que dans
 * fifo_thread (jamais lancé ici) et s3_add_status_info (jamais appelée). Arrêt bruyant. */
uint64_t timer_freq;
uint64_t timer_read(void) {
        fatal("timer_read : horloge de l'hote, hors de l'oracle (G7.3)\n");
        return 0;
}

/* pcem bug, not reproduced: PB-83 — sans PCI, pci_add rend -1 (pci.c:189-190) et
 * s3_update_irqs (vid_s3.c:161-166) appelle pci_set_irq / pci_clear_irq(-1, PCI_INTA), qui
 * lisent pci_irq_routing[-1] et, s'il est non nul, ÉCRIVENT pci_irq_active[-1] (pci.c:128-148) :
 * un global voisin de l'hôte. Rien n'est câblé : aucune IRQ, rien n'est écrit. */
void pci_set_irq(int card, int pci_int) {
        (void)card;
        (void)pci_int;
}
void pci_clear_irq(int card, int pci_int) {
        (void)card;
        (void)pci_int;
}

/* Les vingt champs de la carte, après ceux de la GD5429, dans l'ordre de
 * Video.vid_svga.Probe() côté C#. Tout à zéro pour une autre carte. */
void h_s3_probe(svga_t *svga, uint64_t *out) {
        s3_t *s3;
        int f = 0;

        if (gfxcard != GFX_PHOENIX_TRIO64)
                return;
        s3 = (s3_t *)svga->p;

        out[f++] = (uint64_t)(int64_t)s3->chip;
        out[f++] = s3->id | ((uint64_t)s3->id_ext << 8) | ((uint64_t)s3->id_ext_pci << 16);
        out[f++] = s3->bank;
        out[f++] = s3->ma_ext;
        out[f++] = (uint64_t)(int64_t)s3->width;
        out[f++] = (uint64_t)(int64_t)s3->bpp;
        out[f++] = s3->linear_base;
        out[f++] = s3->linear_size;
        out[f++] = s3->subsys_cntl | ((uint64_t)s3->subsys_stat << 8) | ((uint64_t)s3->accel.subsys_cntl << 16) |
                   ((uint64_t)s3->accel.advfunc_cntl << 24);
        out[f++] = s3->accel.cmd | ((uint64_t)s3->accel.short_stroke << 16) |
                   ((uint64_t)s3->accel.multifunc_cntl << 32);
        out[f++] = s3->accel.cur_x | ((uint64_t)s3->accel.cur_y << 16) | ((uint64_t)s3->accel.cur_x2 << 32) |
                   ((uint64_t)s3->accel.cur_y2 << 48);
        out[f++] = s3->accel.frgd_color | ((uint64_t)s3->accel.bkgd_color << 32);
        out[f++] = s3->accel.wrt_mask | ((uint64_t)s3->accel.rd_mask << 32);
        out[f++] = s3->accel.frgd_mix | ((uint64_t)s3->accel.bkgd_mix << 8) | ((uint64_t)s3->accel.color_cmp << 32);
        out[f++] = (uint32_t)s3->accel.cx | ((uint64_t)(uint32_t)s3->accel.cy << 32);
        out[f++] = (uint32_t)s3->accel.sx | ((uint64_t)(uint32_t)s3->accel.sy << 32);
        out[f++] = (uint32_t)s3->accel.dx | ((uint64_t)(uint32_t)s3->accel.dy << 32);
        out[f++] = (uint32_t)s3->fifo_write_idx | ((uint64_t)(uint32_t)s3->fifo_read_idx << 32);
        out[f++] = (uint64_t)(int64_t)s3->blitter_busy | ((uint64_t)(uint32_t)s3->force_busy << 32);
        out[f++] = s3->hwc_fg_col | ((uint64_t)s3->hwc_bg_col << 32);
}
