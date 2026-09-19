// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: tools/oracle/harness_stubs.c  (couche mémoire et E/S)
// SHA256: voir oracle.tsv ; vérifier avec tools/check-oracle.sh
// STATUS: partial — stub de M1, PAIRE de transcription avec harness_stubs.c.
//         Remplacé à M2 par la transcription du vrai pcem-dev/src/memory/mem.c.
//
// Pourquoi une paire et non une réimplémentation indépendante : c'est la
// mitigation du risque n°3. Le cas dangereux n'est pas que les deux côtés soient
// en désaccord — c'est bruyant — mais qu'ils stubent tous deux à la même
// constante, masquant une divergence dans le chemin qui la consomme. Écrire la
// couche une fois en C et la transcrire ici fait des deux une seule décision,
// pas deux suppositions qui se ressemblent par chance.
//
// Les compteurs d'appels entrent dans le vecteur d'état diffé : un stub resté à
// zéro des deux côtés alors que le test devait l'exercer FAIT ÉCHOUER la passe,
// au lieu de se lire comme un accord.

using iXtal26.Cpu;

namespace iXtal26.Memory;

internal static partial class mem
{
    internal const uint32_t RAM_SIZE = 0x100000; // 1 Mo — l'espace du 8088

    internal static byte[] ram = new byte[RAM_SIZE];
    internal static uint32_t rammask = 0xFFFFF;  // XT : bus 20 bits, tout reboucle
    internal static int mem_size = 640;          // Ko
    internal static int mmu_perm = 4;

    // pcem: mem.c:62,65 — en C ce sont des uintptr_t* tenant un pointeur hôte
    // biaisé : readlookup2[virt>>12] = &ram[(phys & ~0xFFF) - (virt & ~0xFFF)],
    // relu comme *(uint8_t *)(readlookup2[a>>12] + a).
    //
    // Ici : le MÊME calcul, avec la base du tableau factorisée. On stocke un
    // offset dans ram[] au lieu d'une adresse absolue, et ram[rl[a>>12] + a]
    // est la transcription littérale du déréférencement.
    //
    // La sentinelle -1 survit, et c'est démontrable : un biais réel est la
    // différence de deux adresses alignées sur 4 Ko, donc toujours ≡ 0 (mod
    // 0x1000) ; -1 ne l'est pas. Aucun biais légitime ne peut la produire.
    internal static int[] readlookup2 = new int[1 << 20];
    internal static int[] writelookup2 = new int[1 << 20];
    internal static int readlnum, writelnum;

    // Compteurs — voir l'en-tête.
    internal static uint64_t n_readmembl, n_writemembl, n_readmemwl, n_writememwl;
    internal static uint64_t n_inb, n_outb, n_picint, n_picinterrupt, n_timer_process, n_fatal;

    internal static void counters_reset()
    {
        n_readmembl = n_writemembl = n_readmemwl = n_writememwl = 0;
        n_inb = n_outb = n_picint = n_picinterrupt = n_timer_process = n_fatal = 0;
    }

    /// <summary>
    /// Le cache de pages de 4 Ko est laissé entièrement à -1, ce qui force chaque
    /// accès à descendre dans readmembl/writemembl. Chemin déterministe, et
    /// exactement celui que M1 doit reproduire. Son remplissage — et sa
    /// facturation à `cycles -= 9` (mem.c:378) — arrivent à M2 avec le vrai mem.c.
    /// </summary>
    internal static void mem_init()
    {
        Array.Fill(readlookup2, -1);
        Array.Fill(writelookup2, -1);
        readlnum = writelnum = 0;
    }

    internal static void fill_ram(uint8_t value) => Array.Fill(ram, value);

    // pcem: harness_stubs.c — readmembl/writemembl/readmemwl/writememwl
    internal static uint8_t readmembl(uint32_t addr)
    {
        n_readmembl++;
        return ram[addr & rammask];
    }

    internal static void writemembl(uint32_t addr, uint8_t val)
    {
        n_writemembl++;
        ram[addr & rammask] = val;
    }

    internal static uint16_t readmemwl(uint32_t addr)
    {
        n_readmemwl++;
        // Rebouclage 20 bits octet par octet : une lecture en 0xFFFFF relit
        // l'octet 0. Reproduit le stub C, qui reproduit le silicium.
        return (uint16_t)(ram[addr & rammask] | (ram[(addr + 1) & rammask] << 8));
    }

    internal static void writememwl(uint32_t addr, uint16_t val)
    {
        n_writememwl++;
        ram[addr & rammask] = (uint8_t)val;
        ram[(addr + 1) & rammask] = (uint8_t)(val >> 8);
    }

    internal static void resetreadlookup() => mem_init();

    internal static void flushmmucache() { /* pas de pagination sur XT (cr0 >> 31 == 0) */ }

    // --- E/S : bus ouvert, comme un XT sans carte sur le port visé -----------
    internal static uint8_t inb(uint16_t port)
    {
        n_inb++;
        return 0xFF;
    }

    internal static void outb(uint16_t port, uint8_t val) => n_outb++;

    // --- interruptions et temps : inertes en M1, réels en M2/M3 -------------
    internal static void picint(uint16_t num) => n_picint++;

    internal static uint8_t picinterrupt()
    {
        n_picinterrupt++;
        return 0xFF;
    }

    internal static uint64_t tsc;
    internal static uint32_t timer_target = 0x7FFFFFFF;

    internal static void timer_process() => n_timer_process++;
}
