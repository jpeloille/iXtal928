// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/cpu/cpu.c + includes/private/cpu/cpu.h
// STATUS: partial — RÉDUIT À L'EXTRÊME. cpu.c fait 2 080 lignes dont l'essentiel
//         est cpu_set(), qui sélectionne les tables d'opcodes des 286 à 686 et
//         référence tout le dynarec. Rien de cela n'existe pour un 8088 : la
//         configuration tient dans les quatre valeurs ci-dessous.
//
// C'est exactement pour cette raison que l'oracle C ne lie pas cpu.c non plus
// (voir tools/oracle/harness_stubs.c) : le lier ferait passer les symboles
// manquants de 66 à 133 et ramènerait le recompilateur.

namespace iXtal26.Cpu;

internal static partial class cpu
{
    // pcem: cpu_tables.c:33 — cpus_8088[0], « 8088/4.77 ».
    internal const int CPU_SPEED_8088 = 4772728;

    internal static int cpu_busspeed = CPU_SPEED_8088;

    // pcem: cpu.c:2067-2071 — cpu_turbo ? cpu_turbo_speed : cpu_nonturbo_speed.
    // Le 5150 n'a pas de mode turbo : une seule vitesse.
    internal static int cpu_get_speed() => CPU_SPEED_8088;

    // pcem: cpu.h — cycles d'attente d'un accès au bus ISA, posés par
    // setpitclock() via isa_timing. Sur un 5150 tout est ISA à 4,77 MHz.
    internal static int isa_cycles = 1;

    // pcem: cpu.h — présence d'un 8087. Le PPI le rapporte au BIOS via les
    // interrupteurs DIP (port 0x62). Aucun coprocesseur sur cette machine.
    internal static int hasfpu = 0;

    // pcem: cpu.c — le bit turbo du port 0x61 sur les clones XT. Sans effet ici.
    internal static void cpu_set_turbo(int turbo) { }

    // pcem: cpu.h — le modèle de temps de PRÉFETCH de l'interpréteur, posé par
    // cpu_update_waitstates() (cpu.c:2010-2047). Ajoutés en A2.2a parce que
    // getpccache les ÉCRIT : c'est lui qui bascule entre le coût d'une ROM et celui
    // de la RAM, à chaque changement de page d'instruction.
    //
    // Tous à ZÉRO pour un 8088, et c'est le comportement juste : 808x.c porte son
    // propre modèle de préfetch dans ses statiques (fetchcycles, prefetchqueue), et
    // ne lit aucun de ces symboles — 0 occurrence, mesuré. Ils ne prennent des
    // valeurs que pour le 286, où PREFETCH_RUN est gardé par
    // `if (cpu_prefetch_cycles)` (386_dynarec.c:210).
    internal static int cpu_prefetch_cycles;
    internal static int cpu_mem_prefetch_cycles;
    internal static int cpu_rom_prefetch_cycles;
    internal static int cpu_prefetch_width;
    internal static int cpu_cycles_read, cpu_cycles_read_l;
    internal static int cpu_cycles_write, cpu_cycles_write_l;
}
