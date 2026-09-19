/* SPDX-FileCopyrightText: 2026 Julien Peloille
 * SPDX-License-Identifier: GPL-2.0-only
 *
 * Autotest de l'oracle. Aucun C# requis : `make selftest`.
 *
 * Ce qu'il prouve — c'est le critère de sortie de M0 :
 *   1. l'ABI est celle attendue ;
 *   2. h_reset() place la machine dans l'état post-reset d'un XT ;
 *   3. l'oracle exécute un programme écrit à la main, une instruction à la fois,
 *      et produit les bons registres et la bonne mémoire ;
 *   4. la file de préfetch se remplit réellement (FETCH/FETCHADD fonctionnent) ;
 *   5. le TSC avance via clockhardware() en virgule fixe 32:32 ;
 *   6. les compteurs de stubs discriminent les chemins réellement empruntés ;
 *   7. deux exécutions identiques donnent un état identique — sans déterminisme,
 *      tout le diff différentiel de M1 est sans objet.
 *
 * Les comptages de cycles sont AFFICHÉS, pas asservis à une valeur attendue. Le
 * modèle de temps de PCem lui est propre (constantes littérales par opcode, puis
 * réconciliation à 808x.c:3930-3934) et ne coïncide pas avec les traces de
 * SingleStepTests ; la référence pour ces nombres, c'est cet oracle lui-même.
 * C'est précisément pour ça qu'il existe.
 */

#include <stdarg.h>
#include <stdio.h>
#include <string.h>

#include "harness.h"

static int failures;

static void check(const char *what, int ok, const char *fmt, ...) {
        if (ok) {
                printf("  OK   %s\n", what);
                return;
        }
        printf("  FAIL %s", what);
        if (fmt && *fmt) {
                va_list ap;
                va_start(ap, fmt);
                printf(" : ");
                vprintf(fmt, ap);
                va_end(ap);
        }
        printf("\n");
        failures++;
}

/* Programme de test, 14 octets. Chargé en CS=0x0100:0000 (physique 0x1000) pour
 * ne pas chevaucher la cible d'écriture en DS:0x0100 (physique 0x0100). */
static const uint8_t prog[] = {
        0xB8, 0x34, 0x12,       /* MOV AX, 1234h            */
        0xBB, 0x05, 0x00,       /* MOV BX, 0005h            */
        0x01, 0xD8,             /* ADD AX, BX               */
        0xA3, 0x00, 0x01,       /* MOV [0100h], AX          */
        0x40,                   /* INC AX                   */
        0x90,                   /* NOP                      */
        0xF4,                   /* HLT                      */
};
#define PROG_CS 0x0100
#define PROG_INSTRUCTIONS 7

static void load_and_reset(void) {
        h_reset();
        h_load(PROG_CS * 16, prog, sizeof(prog));
        h_set_cs_ip(PROG_CS, 0x0000);
}

int main(void) {
        h_state s;
        uint8_t mem[2];
        int cyc[PROG_INSTRUCTIONS];
        int queue_ever_filled = 0;

        printf("Autotest de l'oracle iXtal26 (cœur 8088 de PCem v18)\n\n");

        check("version d'ABI", h_abi_version() == H_ABI_VERSION, "attendu %d, obtenu %u",
              H_ABI_VERSION, h_abi_version());

        /* --- 2. état post-reset ------------------------------------------- */
        h_reset();
        h_getstate(&s);
        check("reset : CS = 0xFFFF", s.seg_sel[H_SEG_CS] == 0xFFFF, "obtenu 0x%04X", s.seg_sel[H_SEG_CS]);
        check("reset : pc = 0", s.pc == 0, "obtenu 0x%X", s.pc);
        check("reset : flags = 2", s.flags == 2, "obtenu 0x%04X", s.flags);
        check("reset : file de prefetch vide", s.prefetchw == 0, "obtenu %d", s.prefetchw);
        check("reset : tsc = 0", s.tsc == 0, "obtenu %llu", (unsigned long long)s.tsc);
        check("reset : compteurs de stubs a zero", s.n_inb == 0 && s.n_outb == 0 && s.n_picinterrupt == 0, "");

        /* --- 3. exécution pas à pas --------------------------------------- */
        load_and_reset();
        for (int i = 0; i < PROG_INSTRUCTIONS; i++) {
                cyc[i] = h_step();
                h_getstate(&s);
                if (s.prefetchw > 0)
                        queue_ever_filled = 1;
        }

        check("MOV/ADD/INC : AX = 0x123A", s.regs[0] == 0x123A, "obtenu 0x%08X", s.regs[0]);
        check("MOV BX : BX = 0x0005", s.regs[3] == 0x0005, "obtenu 0x%08X", s.regs[3]);

        h_read(0x0100, mem, 2);
        check("MOV [0100h], AX : memoire = 39 12", mem[0] == 0x39 && mem[1] == 0x12,
              "obtenu %02X %02X", mem[0], mem[1]);

        check("HLT atteint (inhlt)", s.inhlt != 0, "obtenu %d", s.inhlt);
        check("nombre d'instructions", s.ins == PROG_INSTRUCTIONS, "obtenu %llu",
              (unsigned long long)s.ins);

        /* --- 4. la file de préfetch vit ----------------------------------- */
        check("la file de prefetch s'est remplie", queue_ever_filled, "prefetchw est reste a 0");

        /* --- 5. le TSC avance --------------------------------------------- */
        check("le tsc a avance", s.tsc > 0, "obtenu %llu", (unsigned long long)s.tsc);

        /* --- 6. les compteurs discriminent -------------------------------- */
        check("lectures memoire comptees", s.n_readmembl > 0, "obtenu %llu",
              (unsigned long long)s.n_readmembl);
        check("ecriture memoire comptee", s.n_writememwl > 0, "obtenu %llu",
              (unsigned long long)s.n_writememwl);
        check("aucune E/S sur ce programme", s.n_inb == 0 && s.n_outb == 0,
              "inb=%llu outb=%llu", (unsigned long long)s.n_inb, (unsigned long long)s.n_outb);

        /* Un IN AL,DX doit, lui, faire bouger le compteur : c'est ce qui distingue
         * « le chemin n'a pas divergé » de « le chemin n'a jamais été emprunté ». */
        {
                static const uint8_t io_prog[] = { 0xEC }; /* IN AL, DX */
                h_reset();
                h_load(PROG_CS * 16, io_prog, sizeof(io_prog));
                h_set_cs_ip(PROG_CS, 0x0000);
                h_step();
                h_getstate(&s);
                check("IN AL,DX incremente n_inb", s.n_inb == 1, "obtenu %llu",
                      (unsigned long long)s.n_inb);
                check("IN AL,DX ramene 0xFF (bus ouvert)", (s.regs[0] & 0xFF) == 0xFF,
                      "obtenu 0x%02X", s.regs[0] & 0xFF);
        }

        /* --- 7. déterminisme ---------------------------------------------- */
        {
                h_state a, b;
                uint64_t ha, hb;

                load_and_reset();
                for (int i = 0; i < PROG_INSTRUCTIONS; i++)
                        h_step();
                h_getstate(&a);
                ha = h_ram_hash();

                load_and_reset();
                for (int i = 0; i < PROG_INSTRUCTIONS; i++)
                        h_step();
                h_getstate(&b);
                hb = h_ram_hash();

                check("deux executions donnent le meme etat", memcmp(&a, &b, sizeof(a)) == 0,
                      "les h_state different");
                check("deux executions donnent la meme RAM", ha == hb,
                      "%016llx vs %016llx", (unsigned long long)ha, (unsigned long long)hb);
        }

        /* --- 8. h_step() est-il fidèle à une exécution en budget long ? -----
         *
         * h_step() pose cycles = 1 à chaque appel au lieu de laisser le budget
         * s'écouler comme le fait runpc() (47 727 cycles par tranche de 10 ms).
         * L'argument est que toute la comptabilité de 808x.c est relative
         * (cycdiff = cycles au sommet de boucle, puis tout se mesure en
         * cycdiff - cycles) et que la dette entre instructions est portée par
         * nextcyc, qu'on ne touche pas.
         *
         * C'est un argument, pas une preuve. On le vérifie : même programme, une
         * fois pas à pas, une fois d'une traite via h_run(), et on compare tout
         * sauf `cycles` (dont la valeur absolue dépend forcément du budget). Si
         * ça diverge, tout le diff par instruction de M1 mesure autre chose que
         * ce que fait PCem en vrai. */
        {
                h_state stepwise, oneshot;
                uint64_t ram_stepwise, ram_oneshot, n;

                /* D'abord le budget long, parce que c'est lui qui décide combien
                 * d'instructions sont exécutées : execx86() ne s'arrête pas au HLT,
                 * elle continue de boucler dessus jusqu'à épuiser le budget. On lit
                 * donc le compte réel, puis on en fait autant pas à pas. Comparer
                 * 7 pas contre « 1000 cycles de budget » comparerait deux
                 * exécutions de longueurs différentes. */
                load_and_reset();
                h_run(1000);
                h_getstate(&oneshot);
                ram_oneshot = h_ram_hash();
                n = oneshot.ins;

                load_and_reset();
                for (uint64_t i = 0; i < n; i++)
                        h_step();
                h_getstate(&stepwise);
                ram_stepwise = h_ram_hash();

                printf("  (comparaison sur %llu instructions)\n", (unsigned long long)n);

                check("pas-a-pas == budget long : registres",
                      memcmp(stepwise.regs, oneshot.regs, sizeof(stepwise.regs)) == 0, "");
                check("pas-a-pas == budget long : tsc", stepwise.tsc == oneshot.tsc,
                      "%llu vs %llu", (unsigned long long)stepwise.tsc, (unsigned long long)oneshot.tsc);
                check("pas-a-pas == budget long : etat de prefetch",
                      stepwise.prefetchw == oneshot.prefetchw && stepwise.prefetchpc == oneshot.prefetchpc &&
                              stepwise.fetchcycles == oneshot.fetchcycles,
                      "prefetchw %d/%d prefetchpc %04X/%04X fetchcycles %d/%d", stepwise.prefetchw,
                      oneshot.prefetchw, stepwise.prefetchpc, oneshot.prefetchpc, stepwise.fetchcycles,
                      oneshot.fetchcycles);
                check("pas-a-pas == budget long : pc et memoire",
                      stepwise.pc == oneshot.pc && ram_stepwise == ram_oneshot,
                      "pc %04X vs %04X, ram %016llx vs %016llx", stepwise.pc, oneshot.pc,
                      (unsigned long long)ram_stepwise, (unsigned long long)ram_oneshot);
        }

        /* --- relevé, pas assertion ----------------------------------------- */
        printf("\n  Cycles par instruction (reference PCem, a reproduire en C#) :\n");
        static const char *names[PROG_INSTRUCTIONS] = { "MOV AX,1234h", "MOV BX,0005h", "ADD AX,BX",
                                                        "MOV [0100h],AX", "INC AX", "NOP", "HLT" };
        for (int i = 0; i < PROG_INSTRUCTIONS; i++)
                printf("    %-16s %3d\n", names[i], cyc[i]);

        printf("\n%s (%d echec%s)\n", failures ? "ECHEC" : "Autotest vert", failures,
               failures > 1 ? "s" : "");
        return failures ? 1 : 0;
}
