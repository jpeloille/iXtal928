/* SPDX-FileCopyrightText: 2026 Julien Peloille
 * SPDX-License-Identifier: GPL-2.0-only
 *
 * Banc de vitesse hôte de l'oracle, en « C de production ». `make bench`.
 *
 *   bench ROMS [TRANCHES=6000] [REPET=5] [CHAUFFE=600]
 *
 * Mêmes sources que libixtal26oracle.so, mais lié en EXÉCUTABLE, sans
 * harness_wrap.c ni -Wl,--wrap (les compteurs h_n_* de harness_stubs.c restent
 * définis, plus rien ne les incrémente : n_readmembl vaut 0 ici), et avec les
 * drapeaux du build amont Release : -O2 -flto -march=x86-64-v2. C'est la
 * vitesse du cœur de PCem tel qu'il est livré — la référence que le C# doit
 * approcher, et que le ratio de `iXtal26.Diff bench` (mesuré contre la .so
 * instrumentée) surestime légèrement.
 *
 * Même protocole que Bench.cs : amorçage, chauffe, RÉ-amorçage, puis N x
 * h_run(4772728/100) sous CLOCK_MONOTONIC. L'empreinte finale (ins, Σcycles,
 * h_ram_hash) se compare à l'œil avec celle qu'imprime le C#.
 */

#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <time.h>

#include "harness.h"

#define BUDGET (4772728 / 100)

static double now_ms(void) {
        struct timespec ts;
        clock_gettime(CLOCK_MONOTONIC, &ts);
        return ts.tv_sec * 1e3 + ts.tv_nsec / 1e6;
}

static int cmp_double(const void *a, const void *b) {
        double x = *(const double *)a, y = *(const double *)b;
        return (x > y) - (x < y);
}

int main(int argc, char **argv) {
        if (argc < 2) {
                fprintf(stderr, "usage : %s ROMS [TRANCHES=6000] [REPET=5] [CHAUFFE=600]\n", argv[0]);
                return 2;
        }
        const char *roms = argv[1];
        int slices = argc > 2 ? atoi(argv[2]) : 6000;
        int repeat = argc > 3 ? atoi(argv[3]) : 5;
        int warmup = argc > 4 ? atoi(argv[4]) : 600;
        if (slices <= 0 || repeat <= 0 || warmup < 0) {
                fprintf(stderr, "bench : TRANCHES et REPET > 0, CHAUFFE >= 0.\n");
                return 2;
        }

        double *ms = calloc((size_t)repeat, sizeof *ms);
        uint64_t ins0 = 0, ram0 = 0;
        int64_t cyc0 = 0;
        h_state s;

        printf("épinglage recommandé : taskset -c 0-3 %s %s %d %d %d\n", argv[0], roms, slices, repeat, warmup);
        printf("paire mesurée        : h_run(%d) x %d tranches = %.2f s émulées  ·  %d répétition(s), chauffe %d, "
               "ré-amorçage avant mesure  ·  C de production (-O2 -flto, sans --wrap)\n\n",
               BUDGET, slices, slices / 100.0, repeat, warmup);
        printf("%3s %10s %9s %10s %13s\n", "rép", "ms", "ns/instr", "M instr/s", "× temps réel");

        for (int r = 0; r < repeat; r++) {
                if (!h_boot(roms))
                        goto boot_failed;
                for (int i = 0; i < warmup; i++)
                        h_run(BUDGET);
                if (!h_boot(roms))
                        goto boot_failed;

                h_getstate(&s);
                uint64_t ins_before = s.ins;
                int64_t cycles = 0;

                double t0 = now_ms();
                for (int i = 0; i < slices; i++)
                        cycles += h_run(BUDGET);
                double t1 = now_ms();

                h_getstate(&s);
                uint64_t ins = s.ins - ins_before, ram = h_ram_hash();
                ms[r] = t1 - t0;
                /* × temps réel = (N/100) s émulées / t — sur N, pas sur Σcycles :
                 * h_run remet cycles à 0 par tranche et Σ dépasse N x BUDGET. */
                printf("%3d %10.1f %9.2f %10.2f %13.2f\n", r + 1, ms[r], ms[r] * 1e6 / ins, ins / (ms[r] * 1e3),
                       (slices / 100.0) / (ms[r] / 1e3));

                /* Chaque répétition repart d'un ré-amorçage : même empreinte, sinon
                 * le ré-amorçage n'est pas un reset et la mesure porte sur autre chose. */
                if (r == 0) {
                        ins0 = ins, cyc0 = cycles, ram0 = ram;
                } else if (ins != ins0 || cycles != cyc0 || ram != ram0) {
                        printf("    rép %d diverge de la rép 1 : ins %llu / Σcycles %lld / RAM %016llx\n", r + 1,
                               (unsigned long long)ins, (long long)cycles, (unsigned long long)ram);
                        return 1;
                }
        }

        qsort(ms, (size_t)repeat, sizeof *ms, cmp_double);
        double med = repeat % 2 ? ms[repeat / 2] : (ms[repeat / 2 - 1] + ms[repeat / 2]) / 2;
        printf("\n%-8s %10.1f ms   min %10.1f ms   dispersion %.1f %%   %.2f ns/instr   %.2f M instr/s   x%.2f temps réel\n",
               "médiane", med, ms[0], 100.0 * (ms[repeat - 1] - ms[0]) / med, med * 1e6 / ins0, ins0 / (med * 1e3),
               (slices / 100.0) / (med / 1e3));
        printf("empreinte : ins %llu  Σcycles %lld  RAM 0x%016llX\n", (unsigned long long)ins0, (long long)cyc0,
               (unsigned long long)ram0);
        free(ms);
        return 0;

boot_failed:
        fprintf(stderr, "L'oracle n'a pas pu charger le BIOS depuis « %s ».\n", roms);
        free(ms);
        return 1;
}
