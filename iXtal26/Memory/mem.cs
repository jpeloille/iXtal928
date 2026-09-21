// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/memory/mem.c + includes/private/memory/mem.h
// SHA256: voir oracle.tsv ; vérifier avec tools/check-oracle.sh
// STATUS: partial — palier (a) : carte mémoire, cache de pages, accès et
//         mappages. Pagination, dynarec et remappage 386+ omis.
//
// La carte mémoire de PCem, et le cache de pages qui EST le chemin chaud du 8088.
//
// Trois structures coexistent à trois granularités : le dispatch physique par
// pages de 16 Ko (read_mapping/write_mapping/_mem_state, indexés addr >> 14), le
// cache de traduction par pages de 4 Ko (readlookup2/writelookup2, indexés
// addr >> 12), et les mem_mapping_t elles-mêmes en liste chaînée.
//
// Le cache de 4 Ko n'est PAS une optimisation : addreadlookup facture
// `cycles -= 9` (mem.c:378), donc son remplissage est visible dans le temps émulé.

using iXtal26.Diag;
using static iXtal26.Cpu.x86;

namespace iXtal26.Memory;

// pcem: mem.h:11-22
internal delegate uint8_t mem_read_b_fn(uint32_t addr, object? p);
internal delegate uint16_t mem_read_w_fn(uint32_t addr, object? p);
internal delegate uint32_t mem_read_l_fn(uint32_t addr, object? p);
internal delegate void mem_write_b_fn(uint32_t addr, uint8_t val, object? p);
internal delegate void mem_write_w_fn(uint32_t addr, uint16_t val, object? p);
internal delegate void mem_write_l_fn(uint32_t addr, uint32_t val, object? p);

// pcem: mem.h:6-26
// Classe et non struct : son adresse est prise partout (&ram_low_mapping,
// &bios_mapping[0], &cga->mapping) et elle est chaînée dans une liste.
internal sealed class mem_mapping_t
{
    internal mem_mapping_t? prev, next;

    internal int enable;

    internal uint32_t @base;   // `base` est un mot-clé C#
    internal uint32_t size;

    internal mem_read_b_fn? read_b;
    internal mem_read_w_fn? read_w;
    internal mem_read_l_fn? read_l;
    internal mem_write_b_fn? write_b;
    internal mem_write_w_fn? write_w;
    internal mem_write_l_fn? write_l;

    // DEVIATION: en C, `uint8_t *exec` pointe dans ram ou rom. Ici, le tableau
    //   porteur et un offset. Le seul consommateur est _mem_exec[], non transcrit
    //   (chemin 386/dynarec) : le champ existe pour la forme, il n'est jamais lu.
    internal byte[]? exec;
    internal int exec_offset;

    internal uint32_t flags;

    internal object? p;
}

internal static partial class mem
{
    // pcem: mem.h:29-34
    internal const uint32_t MEM_MAPPING_EXTERNAL = 1;
    internal const uint32_t MEM_MAPPING_INTERNAL = 2;
    internal const uint32_t MEM_MAPPING_ROM = 4;

    // pcem: mem.h:59-67
    internal const int MEM_READ_ANY = 0x00;
    internal const int MEM_READ_INTERNAL = 0x10;
    internal const int MEM_READ_EXTERNAL = 0x20;
    internal const int MEM_READ_MASK = 0xf0;
    internal const int MEM_WRITE_ANY = 0x00;
    internal const int MEM_WRITE_INTERNAL = 0x01;
    internal const int MEM_WRITE_EXTERNAL = 0x02;
    internal const int MEM_WRITE_DISABLED = 0x03;
    internal const int MEM_WRITE_MASK = 0x0f;

    // pcem: mem.c:26-29 — dispatch physique, granularité 16 Ko, 0x40000 entrées
    // couvrant 4 Go. Tableaux plats, ni arbre ni creux.
    private static readonly mem_mapping_t?[] read_mapping = new mem_mapping_t?[0x40000];
    private static readonly mem_mapping_t?[] write_mapping = new mem_mapping_t?[0x40000];
    private static readonly uint8_t[] _mem_state = new uint8_t[0x40000];
    // omitted: _mem_exec[0x40000] — chemin d'instruction du cœur 386 et des
    //   marches de table de pages. Mesuré : 0 occurrence dans 808x.c.

    private static readonly mem_mapping_t base_mapping = new();
    internal static readonly mem_mapping_t ram_low_mapping = new();
    internal static readonly mem_mapping_t[] bios_mapping = CreateMappings(8);

    private static mem_mapping_t[] CreateMappings(int n)
    {
        var a = new mem_mapping_t[n];
        for (var i = 0; i < n; i++)
                a[i] = new mem_mapping_t();
        return a;
    }

    internal const uint32_t RAM_SIZE = 0x100000; // 1 Mo — l'espace du 8088

    internal static int mem_size;
    internal static uint32_t biosmask;
    internal static int readlnum = 0, writelnum = 0;
    internal static int cachesize = 256;

    internal static byte[] ram = [];
    internal static byte[] rom = new byte[0x40000];
    internal static readonly byte[] romext = new byte[32768];

    // pcem: mem.c:52 — passage de paramètre HORS BANDE : écrit par readmembl et
    // writemembl, relu par mem_read_ram (mem.c:829). Le handler reçoit l'adresse
    // PHYSIQUE en paramètre et la VIRTUELLE par cette globale. C'est laid, et
    // c'est le contrat : en faire un paramètre ferait cesser à l'oracle d'être un
    // oracle.
    internal static uint32_t mem_logical_addr;

    internal static int mmu_perm = 4;

    // pcem: mem.c:61-67
    internal static readonly int[] readlookup = new int[256];
    internal static readonly int[] readlookupp = new int[256];
    internal static int readlnext;
    internal static readonly int[] writelookup = new int[256];
    internal static readonly int[] writelookupp = new int[256];
    internal static int writelnext;

    // DEVIATION: en C, `uintptr_t *readlookup2` tient un pointeur hôte BIAISÉ —
    //   readlookup2[virt>>12] = &ram[(phys & ~0xFFF) - (virt & ~0xFFF)], relu comme
    //   *(uint8_t *)(readlookup2[a>>12] + a).
    //
    //   Ici : la MÊME algèbre, base du tableau factorisée. On stocke un offset dans
    //   ram[] et ram[rl[a>>12] + a] transcrit le déréférencement littéralement.
    //
    //   La sentinelle -1 survit, et c'est démontrable : un biais réel est la
    //   différence de deux adresses alignées 4 Ko, donc ≡ 0 (mod 0x1000) ; -1 ne
    //   l'est pas. Aucun biais légitime ne peut la produire.
    internal static int[] readlookup2 = [];
    internal static int[] writelookup2 = [];

    internal static uint32_t rammask;

    // omitted: pages / page_lookup / byte_dirty_mask / byte_code_present_mask —
    //   suivi de pages sales du dynarec. Conséquence marquée plus bas : le chemin
    //   d'écriture est simplifié au lieu d'être transcrit.
    // omitted: mmutranslate_read/write, mmutranslatereal — pagination 386.
    //   `cr0 >> 31` vaut toujours 0 sur un 8088.
    // omitted: mem_remap_top, ram_remapped_mapping, mem_a20_* — 286+.

    // DEVIATION: l'instrumentation est posée en MIROIR de -Wl,--wrap.
    //
    //   Côté C, compteurs et journal d'écritures viennent de -Wl,--wrap
    //   (tools/oracle/harness_wrap.c), qui n'intercepte QUE les appels venus d'une
    //   autre unité de traduction. Quand writememwl appelle writemembl en interne
    //   (mot à cheval sur une page), le C ne compte donc qu'une fois.
    //
    //   Chacune des quatre fonctions se dédouble ici comme ld les dédouble :
    //   `readmembl` est l'enveloppe — elle compte, puis appelle `__real_readmembl`,
    //   qui porte le corps de mem.c verbatim. Les appels INTERNES à ce fichier
    //   visent `__real_`, exactement comme les références intra-unité du C, que ld
    //   ne détourne pas. Tout appelant d'un autre fichier passe par l'enveloppe.
    //
    //   Le nom `__real_` n'est PAS un identifiant de PCem et déroge donc à la règle
    //   de nommage : c'est délibéré. Il vient de l'éditeur de liens (harness_wrap.c:31
    //   le déclare `extern`), et c'est précisément ce qu'on transcrit ici — pas une
    //   fonction de mem.c, mais le mécanisme d'interposition. Un nom inventé
    //   masquerait d'où vient cette paire.
    //
    //   Remplace un compteur de profondeur (obs_depth) et quatre régions try/finally
    //   qui visaient le même invariant par un détour : mêmes valeurs de compteurs,
    //   sans la trame compatible funclet ni les trois instructions par accès.
    //   VERIFICATION.md § M5.2.

    // DEVIATION: fatal() appartient à pc.c, pas encore transcrit. Comme
    //   harness_stubs.c, il compte et n'interrompt pas — on est dans un processus
    //   de test, pas dans un émulateur autonome.
    private static void fatal(string s)
    {
        Counters.n_fatal++;
        Console.Error.Write("iXtal26 FATAL: " + s);
    }

    // -----------------------------------------------------------------------
    // Le cache de pages de 4 Ko (pcem: mem.c:77-110, 354-418)
    // -----------------------------------------------------------------------

    // pcem: mem.c:77-91
    internal static void resetreadlookup()
    {
        int c;
        Array.Fill(readlookup2, -1);
        for (c = 0; c < 256; c++)
                readlookup[c] = unchecked((int)0xFFFFFFFF);
        readlnext = 0;
        Array.Fill(writelookup2, -1);
        for (c = 0; c < 256; c++)
                writelookup[c] = unchecked((int)0xFFFFFFFF);
        writelnext = 0;
        // omitted: pccache = 0xFFFFFFFF — cache d'instruction du cœur 386.
    }

    // pcem: mem.c:93-...
    internal static void flushmmucache()
    {
        int c;
        for (c = 0; c < 256; c++)
        {
                if (readlookup[c] != unchecked((int)0xFFFFFFFF))
                {
                        readlookup2[readlookup[c]] = -1;
                        readlookup[c] = unchecked((int)0xFFFFFFFF);
                }
                if (writelookup[c] != unchecked((int)0xFFFFFFFF))
                {
                        writelookup2[writelookup[c]] = -1;
                        writelookup[c] = unchecked((int)0xFFFFFFFF);
                }
        }
    }

    internal static void flushmmucache_cr3() => flushmmucache();

    // pcem: mem.c:354-379 — le `cycles -= 9` final est de l'ÉMULATION, pas de la
    // comptabilité parasite : le remplissage du cache coûte du temps au 8088.
    internal static void addreadlookup(uint32_t virt, uint32_t phys)
    {
        if (virt == 0xffffffff)
                return;

        if (readlookup2[virt >> 12] != -1)
                return;

        if (readlookup[readlnext] != unchecked((int)0xFFFFFFFF))
                readlookup2[readlookup[readlnext]] = -1;

        readlookup2[virt >> 12] = (int)(phys & ~0xFFFu) - (int)(virt & ~0xFFFu);
        readlookupp[readlnext] = mmu_perm;
        readlookup[readlnext++] = (int)(virt >> 12);
        readlnext &= (cachesize - 1);

        cycles -= 9;
    }

    // pcem: mem.c:381-418
    //
    // ATTENTION, asymétrie avec addreadlookup : celle-ci teste
    // `readlookup2[virt>>12] != -1` et sort si l'entrée est déjà là.
    // addwritelookup, elle, teste `page_lookup[virt>>12]` — une structure du
    // dynarec, toujours nulle sans lui. Elle REFAIT donc le remplissage, et
    // REFACTURE les 9 cycles, à chaque écriture sur une page déjà connue.
    //
    // J'avais d'abord transposé la garde sur writelookup2, par symétrie. Le
    // fuzzer l'a rattrapé en 9 946 itérations : PUSH SP, 64 cycles côté oracle
    // contre 55 côté C# — exactement une facturation manquante. La symétrie
    // apparente était fausse ; on transcrit l'asymétrie.
    internal static void addwritelookup(uint32_t virt, uint32_t phys)
    {
        if (virt == 0xffffffff)
                return;

        // omitted: `if (page_lookup[virt >> 12]) return;` — page_lookup est une
        //   structure du dynarec, toujours nulle ici. La garde ne peut pas tirer.

        if (writelookup[writelnext] != -1)
                writelookup2[writelookup[writelnext]] = -1;

        writelookup2[virt >> 12] = (int)(phys & ~0xFFFu) - (int)(virt & ~0xFFFu);
        writelookupp[writelnext] = mmu_perm;
        writelookup[writelnext++] = (int)(virt >> 12);
        writelnext &= (cachesize - 1);

        cycles -= 9;
    }

    // -----------------------------------------------------------------------
    // Accès mémoire de niveau bas (pcem: mem.c:444-560)
    // -----------------------------------------------------------------------

    internal static uint8_t readmembl(uint32_t addr)
    {
        Counters.n_readmembl++;
        return __real_readmembl(addr);
    }

    private static uint8_t __real_readmembl(uint32_t addr)
    {
        mem_mapping_t? map;

        mem_logical_addr = addr;
        // omitted: `if (cr0 >> 31) addr = mmutranslate_read(addr);` — pagination.
        addr &= rammask;

        map = read_mapping[addr >> 14];
        if (map != null && map.read_b != null)
                return map.read_b(addr, map.p);
        return 0xFF;
    }

    internal static void writemembl(uint32_t addr, uint8_t val)
    {
        Counters.n_writemembl++;
        wlog(addr & rammask, val);
        __real_writemembl(addr, val);
    }

    private static void __real_writemembl(uint32_t addr, uint8_t val)
    {
        mem_mapping_t? map;

        mem_logical_addr = addr;
        // omitted: page_lookup[] (dynarec) et mmutranslate_write (pagination).
        addr &= rammask;

        map = write_mapping[addr >> 14];
        if (map != null && map.write_b != null)
                map.write_b(addr, val, map.p);
    }

    internal static uint16_t readmemwl(uint32_t addr)
    {
        Counters.n_readmemwl++;
        return __real_readmemwl(addr);
    }

    private static uint16_t __real_readmemwl(uint32_t addr)
    {
        mem_mapping_t? map;

        mem_logical_addr = addr;

        if ((addr & 1) != 0)
        {
                // omitted: `cycles -= timing_misaligned` — nul sur un 8088.
                if ((addr & 0xFFF) > 0xFFE)
                        return (uint16_t)(__real_readmembl(addr) | (__real_readmembl(addr + 1) << 8));
                else if (readlookup2[addr >> 12] != -1)
                {
                        var i = readlookup2[addr >> 12] + addr;
                        return (uint16_t)(ram[i] | (ram[i + 1] << 8));
                }
        }

        addr &= rammask;

        map = read_mapping[addr >> 14];
        if (map != null)
        {
                if (map.read_w != null)
                        return map.read_w(addr, map.p);
                if (map.read_b != null)
                        return (uint16_t)(map.read_b(addr, map.p) | (map.read_b(addr + 1, map.p) << 8));
        }

        return 0xffff;
    }

    internal static void writememwl(uint32_t addr, uint16_t val)
    {
        Counters.n_writememwl++;
        wlog(addr & rammask, (uint8_t)val);
        wlog((addr + 1) & rammask, (uint8_t)(val >> 8));
        __real_writememwl(addr, val);
    }

    private static void __real_writememwl(uint32_t addr, uint16_t val)
    {
        mem_mapping_t? map;

        mem_logical_addr = addr;

        if ((addr & 1) != 0)
        {
                if ((addr & 0xFFF) > 0xFFE)
                {
                        __real_writemembl(addr, (uint8_t)val);
                        __real_writemembl(addr + 1, (uint8_t)(val >> 8));
                        return;
                }
                else if (writelookup2[addr >> 12] != -1)
                {
                        var i = writelookup2[addr >> 12] + addr;
                        ram[i] = (uint8_t)val;
                        ram[i + 1] = (uint8_t)(val >> 8);
                        return;
                }
        }

        addr &= rammask;

        map = write_mapping[addr >> 14];
        if (map != null)
        {
                if (map.write_w != null)
                {
                        map.write_w(addr, val, map.p);
                        return;
                }
                if (map.write_b != null)
                {
                        map.write_b(addr, (uint8_t)val, map.p);
                        map.write_b(addr + 1, (uint8_t)(val >> 8), map.p);
                }
        }
    }

    // readmemll / writememll vivent dans mem.c : leurs appels à readmemwl / writememwl
    // sont intra-unité, donc jamais détournés par --wrap. Ils visent __real_.
    internal static uint32_t readmemll(uint32_t addr)
        => (uint32_t)__real_readmemwl(addr) | ((uint32_t)__real_readmemwl(addr + 2) << 16);

    internal static void writememll(uint32_t addr, uint32_t val)
    {
        __real_writememwl(addr, (uint16_t)val);
        __real_writememwl(addr + 2, (uint16_t)(val >> 16));
    }

    // -----------------------------------------------------------------------
    // Handlers RAM / BIOS (pcem: mem.c:827-1030)
    // -----------------------------------------------------------------------

    internal static uint8_t mem_read_ram(uint32_t addr, object? priv)
    {
        addreadlookup(mem_logical_addr, addr);
        return ram[addr];
    }

    internal static uint16_t mem_read_ramw(uint32_t addr, object? priv)
    {
        addreadlookup(mem_logical_addr, addr);
        return (uint16_t)(ram[addr] | (ram[addr + 1] << 8));
    }

    internal static uint32_t mem_read_raml(uint32_t addr, object? priv)
    {
        addreadlookup(mem_logical_addr, addr);
        return (uint32_t)(ram[addr] | (ram[addr + 1] << 8) | (ram[addr + 2] << 16) | (ram[addr + 3] << 24));
    }

    // DEVIATION: en C, mem_write_ram délègue à mem_write_ramb_page(addr, val,
    //   &pages[addr >> 12]), qui met à jour les masques de pages sales du dynarec
    //   AVANT d'écrire. Le dynarec n'étant pas porté, `pages` est omis et le chemin
    //   d'écriture se réduit au magasin. Ce n'est pas une suppression de lignes,
    //   c'est une réécriture du chemin — d'où le marqueur.
    internal static void mem_write_ram(uint32_t addr, uint8_t val, object? priv)
    {
        addwritelookup(mem_logical_addr, addr);
        ram[addr] = val;
    }

    internal static void mem_write_ramw(uint32_t addr, uint16_t val, object? priv)
    {
        addwritelookup(mem_logical_addr, addr);
        ram[addr] = (uint8_t)val;
        ram[addr + 1] = (uint8_t)(val >> 8);
    }

    internal static void mem_write_raml(uint32_t addr, uint32_t val, object? priv)
    {
        addwritelookup(mem_logical_addr, addr);
        ram[addr] = (uint8_t)val;
        ram[addr + 1] = (uint8_t)(val >> 8);
        ram[addr + 2] = (uint8_t)(val >> 16);
        ram[addr + 3] = (uint8_t)(val >> 24);
    }

    internal static uint8_t mem_read_bios(uint32_t addr, object? priv) => rom[addr & biosmask];

    internal static uint16_t mem_read_biosw(uint32_t addr, object? priv)
        => (uint16_t)(rom[addr & biosmask] | (rom[(addr + 1) & biosmask] << 8));

    internal static uint32_t mem_read_biosl(uint32_t addr, object? priv)
        => (uint32_t)(rom[addr & biosmask] | (rom[(addr + 1) & biosmask] << 8)
                    | (rom[(addr + 2) & biosmask] << 16) | (rom[(addr + 3) & biosmask] << 24));

    internal static uint8_t mem_read_romext(uint32_t addr, object? priv) => romext[addr & 0x7fff];
    internal static uint16_t mem_read_romextw(uint32_t addr, object? priv)
        => (uint16_t)(romext[addr & 0x7fff] | (romext[(addr + 1) & 0x7fff] << 8));
    internal static uint32_t mem_read_romextl(uint32_t addr, object? priv)
        => (uint32_t)(romext[addr & 0x7fff] | (romext[(addr + 1) & 0x7fff] << 8)
                    | (romext[(addr + 2) & 0x7fff] << 16) | (romext[(addr + 3) & 0x7fff] << 24));

    // pcem: mem.c — accès PHYSIQUES, utilisés par le DMA. Ils passent par la carte
    // des mappages, pas par ram[] directement : le rafraîchissement DRAM du XT lit
    // au-dessus de 640 Ko, là où aucune RAM n'est mappée, et doit y trouver 0xFF —
    // pas une exception. mem_logical_addr est mis à 0xffffffff pour qu'addreadlookup
    // sorte immédiatement (mem.c:357) : un accès DMA ne remplit pas le cache du CPU.
    internal static uint8_t mem_readb_phys(uint32_t addr)
    {
        mem_mapping_t? map = read_mapping[addr >> 14];

        mem_logical_addr = 0xffffffff;

        if (map != null && map.read_b != null)
                return map.read_b(addr, map.p);

        return 0xff;
    }

    internal static void mem_writeb_phys(uint32_t addr, uint8_t val)
    {
        mem_mapping_t? map = write_mapping[addr >> 14];

        mem_logical_addr = 0xffffffff;

        if (map != null && map.write_b != null)
                map.write_b(addr, val, map.p);
    }

    internal static void mem_write_null(uint32_t addr, uint8_t val, object? p) { }
    internal static void mem_write_nullw(uint32_t addr, uint16_t val, object? p) { }
    internal static void mem_write_nulll(uint32_t addr, uint32_t val, object? p) { }

    // -----------------------------------------------------------------------
    // Mappages (pcem: mem.c:1050-1233)
    // -----------------------------------------------------------------------

    // pcem: mem.c:1050-1063
    private static bool mem_mapping_read_allowed(uint32_t flags, int state)
    {
        switch (state & MEM_READ_MASK)
        {
        case MEM_READ_ANY:
                return true;
        case MEM_READ_EXTERNAL:
                return (flags & MEM_MAPPING_INTERNAL) == 0;
        case MEM_READ_INTERNAL:
                return (flags & MEM_MAPPING_EXTERNAL) == 0;
        default:
                fatal($"mem_mapping_read_allowed : bad state {state:x}\n");
                return false;
        }
    }

    // pcem: mem.c:1065-1079
    private static bool mem_mapping_write_allowed(uint32_t flags, int state)
    {
        switch (state & MEM_WRITE_MASK)
        {
        case MEM_WRITE_DISABLED:
                return false;
        case MEM_WRITE_ANY:
                return true;
        case MEM_WRITE_EXTERNAL:
                return (flags & MEM_MAPPING_INTERNAL) == 0;
        case MEM_WRITE_INTERNAL:
                return (flags & MEM_MAPPING_EXTERNAL) == 0;
        default:
                fatal($"mem_mapping_write_allowed : bad state {state:x}\n");
                return false;
        }
    }

    // pcem: mem.c:1081-1124
    private static void mem_mapping_recalc(uint64_t @base, uint64_t size)
    {
        uint64_t c;
        mem_mapping_t? mapping = base_mapping.next;

        if (size == 0)
                return;

        /*Clear out old mappings*/
        for (c = @base; c < @base + size; c += 0x4000)
        {
                read_mapping[c >> 14] = null;
                write_mapping[c >> 14] = null;
        }

        /*Walk mapping list*/
        while (mapping != null)
        {
                /*In range?*/
                if (mapping.enable != 0 && (uint64_t)mapping.@base < (@base + size) &&
                    ((uint64_t)mapping.@base + mapping.size) > @base)
                {
                        uint64_t start = (mapping.@base < @base) ? mapping.@base : @base;
                        uint64_t end = (((uint64_t)mapping.@base + mapping.size) < (@base + size))
                                               ? ((uint64_t)mapping.@base + mapping.size)
                                               : (@base + size);
                        if (start < mapping.@base)
                                start = mapping.@base;

                        for (c = start; c < end; c += 0x4000)
                        {
                                if ((mapping.read_b != null || mapping.read_w != null || mapping.read_l != null) &&
                                    mem_mapping_read_allowed(mapping.flags, _mem_state[c >> 14]))
                                {
                                        read_mapping[c >> 14] = mapping;
                                }
                                if ((mapping.write_b != null || mapping.write_w != null || mapping.write_l != null) &&
                                    mem_mapping_write_allowed(mapping.flags, _mem_state[c >> 14]))
                                {
                                        write_mapping[c >> 14] = mapping;
                                }
                        }
                }
                mapping = mapping.next;
        }
        flushmmucache_cr3();
    }

    // pcem: mem.c:1126-1155
    internal static void mem_mapping_add(mem_mapping_t mapping, uint32_t @base, uint32_t size,
                                         mem_read_b_fn? read_b, mem_read_w_fn? read_w, mem_read_l_fn? read_l,
                                         mem_write_b_fn? write_b, mem_write_w_fn? write_w, mem_write_l_fn? write_l,
                                         byte[]? exec, int exec_offset, uint32_t flags, object? p)
    {
        mem_mapping_t dest = base_mapping;

        /*Add mapping to the end of the list*/
        while (dest.next != null)
                dest = dest.next;
        dest.next = mapping;

        if (size != 0)
                mapping.enable = 1;
        else
                mapping.enable = 0;
        mapping.@base = @base;
        mapping.size = size;
        mapping.read_b = read_b;
        mapping.read_w = read_w;
        mapping.read_l = read_l;
        mapping.write_b = write_b;
        mapping.write_w = write_w;
        mapping.write_l = write_l;
        mapping.exec = exec;
        mapping.exec_offset = exec_offset;
        mapping.flags = flags;
        mapping.p = p;
        mapping.next = null;

        mem_mapping_recalc(mapping.@base, mapping.size);
    }

    // pcem: mem.c:1157-1175
    internal static void mem_mapping_remove(mem_mapping_t mapping)
    {
        mem_mapping_t prev;
        mem_mapping_t? dest;

        prev = base_mapping;
        dest = prev.next;
        while (dest != mapping)
        {
                if (dest == null)
                        return; // absente de la liste : rien à retirer
                prev = dest;
                dest = dest.next;
        }
        prev.next = mapping.next;

        mem_mapping_recalc(mapping.@base, mapping.size);
    }

    // pcem: mem.c:1192-1200
    internal static void mem_mapping_set_addr(mem_mapping_t mapping, uint32_t @base, uint32_t size)
    {
        mem_mapping_recalc(mapping.@base, mapping.size);
        mapping.@base = @base;
        mapping.size = size;
        mem_mapping_recalc(mapping.@base, mapping.size);
    }

    internal static void mem_mapping_set_exec(mem_mapping_t mapping, byte[]? exec, int exec_offset)
    {
        mapping.exec = exec;
        mapping.exec_offset = exec_offset;
        mem_mapping_recalc(mapping.@base, mapping.size);
    }

    internal static void mem_mapping_set_p(mem_mapping_t mapping, object? p) => mapping.p = p;

    internal static void mem_mapping_disable(mem_mapping_t mapping)
    {
        mapping.enable = 0;
        mem_mapping_recalc(mapping.@base, mapping.size);
    }

    internal static void mem_mapping_enable(mem_mapping_t mapping)
    {
        mapping.enable = 1;
        mem_mapping_recalc(mapping.@base, mapping.size);
    }

    // pcem: mem.c:1225-1233
    internal static void mem_set_mem_state(uint32_t @base, uint32_t size, int state)
    {
        uint32_t c;
        for (c = 0; c < size; c += 0x4000)
                _mem_state[(c + @base) >> 14] = (uint8_t)state;

        mem_mapping_recalc(@base, size);
    }

    // pcem: mem.c:1235-1258, branche non-AT seulement
    internal static void mem_add_bios()
    {
        // omitted: la branche `if (AT || romset == ROM_XI8088)` qui mappe aussi
        //   0xE0000-0xEFFFF. Le 5150 n'a que 0xF0000-0xFFFFF.
        for (var i = 0; i < 4; i++)
        {
                mem_mapping_add(bios_mapping[4 + i], (uint32_t)(0xf0000 + i * 0x4000), 0x04000,
                                mem_read_bios, mem_read_biosw, mem_read_biosl,
                                mem_write_null, mem_write_nullw, mem_write_nulll,
                                rom, (int)((0x30000 + i * 0x4000) & biosmask),
                                MEM_MAPPING_EXTERNAL | MEM_MAPPING_ROM, null);
        }
        // omitted: bios_high_mapping[] — alias haut 0xFFFE0000, 386+ seulement.
    }

    // pcem: mem.c:1329-1338
    internal static void mem_init()
    {
        readlookup2 = new int[1024 * 1024];
        writelookup2 = new int[1024 * 1024];
        Array.Fill(readlookup2, -1);
        Array.Fill(writelookup2, -1);
    }

    // pcem: mem.c:1340-1400, réduit au 5150 (pas de RAM au-delà de 640 Ko)
    internal static void mem_alloc()
    {
        // Quatre octets de marge : les accès 16/32 bits lisent ram[i+1..i+3]. En C
        // un accès à cheval sur la fin lit la mémoire adjacente ; ici il lèverait.
        ram = new byte[mem_size * 1024 + 4];

        Array.Clear(read_mapping);
        Array.Clear(write_mapping);
        Array.Clear(_mem_state);
        base_mapping.next = null;

        mem_set_mem_state(0x000000, (uint32_t)((mem_size > 640) ? 0xa0000 : mem_size * 1024),
                          MEM_READ_INTERNAL | MEM_WRITE_INTERNAL);
        mem_set_mem_state(0x0a0000, 0x60000, MEM_READ_EXTERNAL | MEM_WRITE_EXTERNAL);

        mem_mapping_add(ram_low_mapping, 0x00000, (uint32_t)((mem_size > 640) ? 0xa0000 : mem_size * 1024),
                        mem_read_ram, mem_read_ramw, mem_read_raml,
                        mem_write_ram, mem_write_ramw, mem_write_raml,
                        ram, 0, MEM_MAPPING_INTERNAL, null);

        // omitted: ram_high_mapping / ram_mid_mapping — mémoire au-delà de 1 Mo et
        //   RAM d'ombre 640-768 Ko, hors du 5150.
        // omitted: romext_mapping (mem.c:1422-1425) — sous garde `romset ==
        //   ROM_IBMPS1_2011`, machine absente de model.cs. L'omission était réelle
        //   mais NON MARQUÉE jusqu'à M12 : un trou du registre, relevé en câblant le
        //   Fixed Disk Adapter, qui vise justement 0xc8000 — la même adresse. Les
        //   deux ne se rencontrent pas : la carte passe par rom_init/mem_mapping_add,
        //   et sur un XT cette garde est fausse.

        resetreadlookup();
    }

    internal static void fill_ram(uint8_t value) => Array.Fill(ram, value, 0, (int)RAM_SIZE);

    /// <summary>Motif de deux octets alternés. Pendant de h_fill_ram2().</summary>
    internal static void fill_ram2(uint8_t a, uint8_t b)
    {
        for (var i = 0; i < (int)RAM_SIZE; i++)
                ram[i] = (i & 1) != 0 ? b : a;
    }

    // --- journal d'écritures (paire avec tools/oracle/harness_wrap.c) --------
    // Une instruction n'écrit qu'à une poignée d'endroits. Enregistrer ces
    // adresses permet de comparer la mémoire exactement, sans hacher 1 Mo par
    // instruction et par côté, et en NOMMANT l'adresse divergente.
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
        wlog_n++;
    }

    internal static void wlog_reset() => wlog_n = 0;
}
