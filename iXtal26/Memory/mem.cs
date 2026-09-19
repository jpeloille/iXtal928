// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: tools/oracle/harness_stubs.c  (couche mémoire)
// SHA256: voir oracle.tsv ; vérifier avec tools/check-oracle.sh
// STATUS: partial — stub de M1, PAIRE de transcription avec harness_stubs.c.
//         Remplacé à M2 par la transcription de pcem-dev/src/memory/mem.c.
//
// Pourquoi une paire et non une réimplémentation indépendante : c'est la
// mitigation du risque n°3. Écrire la couche une fois en C et la transcrire ici
// fait des deux une seule décision, pas deux suppositions qui se ressemblent par
// chance. Voir Diag/Counters.cs pour l'autre moitié de cette mitigation.

using iXtal26.Diag;

namespace iXtal26.Memory;

internal static partial class mem
{
    internal const uint32_t RAM_SIZE = 0x100000; // 1 Mo — l'espace du 8088

    // Deux octets de marge : le chemin rapide lit un mot de 16 bits à
    // ram[offset] et ram[offset + 1]. En C, un mot à cheval sur la fin de la RAM
    // lit la mémoire adjacente ; ici il lirait hors borne et lèverait. La marge
    // reproduit le comportement bénin du C sans masquer d'erreur d'adressage,
    // puisque tout accès légitime passe d'abord par `& rammask`.
    internal static byte[] ram = new byte[RAM_SIZE + 2];

    internal static uint32_t rammask = 0xFFFFF; // XT : bus 20 bits, tout reboucle
    internal static int mem_size = 640;         // Ko
    internal static int mmu_perm = 4;

    // pcem: mem.c:62,65 — en C ce sont des uintptr_t* tenant un pointeur hôte
    // biaisé : readlookup2[virt>>12] = &ram[(phys & ~0xFFF) - (virt & ~0xFFF)],
    // relu comme *(uint8_t *)(readlookup2[a>>12] + a).
    //
    // Ici : la MÊME algèbre, avec la base du tableau factorisée. On stocke un
    // offset dans ram[] au lieu d'une adresse absolue, et ram[rl[a>>12] + a] est
    // la transcription littérale du déréférencement.
    //
    // La sentinelle -1 survit, et c'est démontrable : un biais réel est la
    // différence de deux adresses alignées sur 4 Ko, donc toujours ≡ 0 (mod
    // 0x1000) ; -1 ne l'est pas. Aucun biais légitime ne peut la produire.
    internal static int[] readlookup2 = new int[1 << 20];
    internal static int[] writelookup2 = new int[1 << 20];
    internal static int readlnum, writelnum;

    // --- journal d'écritures (paire avec harness_stubs.c) -------------------
    // Une instruction n'écrit qu'à une poignée d'endroits. Enregistrer ces
    // adresses permet de comparer la mémoire exactement, sans hacher 1 Mo par
    // instruction et par côté — et surtout en NOMMANT l'adresse divergente au
    // lieu de dire « la RAM diffère quelque part ».
    internal const int WLOG_MAX = 16;
    internal static readonly uint32_t[] wlog_addr = new uint32_t[WLOG_MAX];
    internal static readonly uint8_t[] wlog_val = new uint8_t[WLOG_MAX];
    internal static int wlog_n;

    private static void wlog(uint32_t addr, uint8_t val)
    {
        if (wlog_n < WLOG_MAX)
        {
                wlog_addr[wlog_n] = addr;
                wlog_val[wlog_n] = val;
        }
        wlog_n++; // continue de compter au-delà, pour que le dépassement se voie
    }

    internal static void wlog_reset() => wlog_n = 0;

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

    internal static void fill_ram(uint8_t value) => Array.Fill(ram, value, 0, (int)RAM_SIZE);

    // pcem: harness_stubs.c — readmembl / writemembl / readmemwl / writememwl
    internal static uint8_t readmembl(uint32_t addr)
    {
        Counters.n_readmembl++;
        return ram[addr & rammask];
    }

    internal static void writemembl(uint32_t addr, uint8_t val)
    {
        Counters.n_writemembl++;
        ram[addr & rammask] = val;
        wlog(addr & rammask, val);
    }

    internal static uint16_t readmemwl(uint32_t addr)
    {
        Counters.n_readmemwl++;
        // Rebouclage 20 bits octet par octet : une lecture en 0xFFFFF relit
        // l'octet 0. Reproduit le stub C, qui reproduit le silicium.
        return (uint16_t)(ram[addr & rammask] | (ram[(addr + 1) & rammask] << 8));
    }

    internal static void writememwl(uint32_t addr, uint16_t val)
    {
        Counters.n_writememwl++;
        ram[addr & rammask] = (uint8_t)val;
        ram[(addr + 1) & rammask] = (uint8_t)(val >> 8);
        wlog(addr & rammask, (uint8_t)val);
        wlog((addr + 1) & rammask, (uint8_t)(val >> 8));
    }

    internal static void resetreadlookup() => mem_init();

    internal static void flushmmucache() { /* pas de pagination sur XT (cr0 >> 31 == 0) */ }

    internal static void flushmmucache_cr3() { }
}
