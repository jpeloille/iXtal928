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

using iXtal26.Cpu;
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
    //   porteur et un offset. Son consommateur est _mem_exec[], transcrit en A2.2a :
    //   ce champ EST lu depuis, par mem_mapping_recalc. (Il ne l'était pas au palier
    //   (a), et ce commentaire disait alors « il n'est jamais lu ».)
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

    // pcem: mem.c:28 — `static uint8_t *_mem_exec[0x40000]`. Transcrit en A2.2a ;
    // l'omission valait tant que seul le 8088 tournait.
    //
    // C'EST LE CHEMIN D'INSTRUCTION, et il est INCONDITIONNEL : exec386 fait
    // `fastreadl(cs + pc)` à CHAQUE instruction (386.c:176), et fastreadl passe par
    // getpccache, qui ne lit que ceci. Mode réel compris — rien de tout cela n'est
    // propre à la pagination, contrairement à ce que TRANSCRIPTION.md:170 laissait
    // entendre en le rangeant avec mmutranslatereal et page_lookup.
    //
    // DEVIATION: porteur + offset, pas un pointeur. C'est l'idiome déjà retenu pour
    //   mem_mapping_t.exec juste au-dessus, et il est OBLIGATOIRE ici plutôt que
    //   commode : readlookup2 peut se contenter d'un offset parce qu'il vise toujours
    //   ram[], alors que _mem_exec vise la RAM *ou* une ROM. Deux tableaux parallèles,
    //   donc, et non un seul int[].
    private static readonly byte[]?[] _mem_exec = new byte[0x40000][];
    private static readonly int[] _mem_exec_off = new int[0x40000];

    // pcem: mem.c:40 — le remplissage de 0xFF que getpccache rend sur un fetch hors
    // de toute cartographie. Une page entière, pour que la lecture reste dans les
    // bornes quel que soit le déplacement dans la page.
    private static readonly byte[] ff_array = new byte[0x1000];

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
    // omitted: mem_remap_top, ram_remapped_mapping — la remise en correspondance des
    //   384 Ko du haut, propre au chipset de l'AT. Bloc B3.

    // A20, ET C'EST LE 8042 QUI LA TIENT.
    //
    // La 21e ligne d'adresse d'un AT passe par une porte que le contrôleur de clavier
    // commande — une prothèse pour que les programmes qui comptaient sur le
    // rebouclage à 1 Mo du 8086 continuent de tourner sur un 286, qui a vingt-quatre
    // lignes. Fermée, `rammask` perd le bit 20 et l'adresse reboucle ; ouverte, la
    // mémoire haute devient atteignable.
    //
    // DÉ-OMISSION : cette ligne disait « mem_a20_* — 286+ », et B1b est l'endroit où
    // ça cesse d'être vrai. keyboard_at.cs les écrit en deux points, la commande 0xD1
    // et l'auto-test 0xAA.
    //
    // mem_a20_state VAUT 2 AU DÉPART, et ce n'est pas un détail : avec zéro, le
    // premier mem_a20_recalc() prendrait la branche `state && !mem_a20_state` et
    // poserait rammask = 0xFFFFFFFF — le rebouclage à 1 Mo du XT, cassé.
    // pcem: mem.c:1289-1290
    internal static int mem_a20_key = 0, mem_a20_alt = 0;
    private static int mem_a20_state = 2;

    // pcem: mem.c:1448-1460
    internal static void mem_a20_recalc()
    {
        int state = mem_a20_key | mem_a20_alt;
        if (state != 0 && mem_a20_state == 0)
        {
                rammask = (AT != 0 && cpu_16bitbus != 0) ? 0xffffff : 0xffffffff;
                flushmmucache();
        }
        else if (state == 0 && mem_a20_state != 0)
        {
                rammask = (AT != 0 && cpu_16bitbus != 0) ? 0xefffff : 0xffefffff;
                flushmmucache();
        }
        mem_a20_state = state;
    }

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
        _386_common.pccache = 0xFFFFFFFF; // pcem: mem.c:89
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
        // pcem: mem.c:113-115. `mmuflush++` est omis — compteur du dynarec.
        //
        // DEVIATION: le C pose `pccache2 = (uint8_t *)0xFFFFFFFF`, une valeur POISON
        //   qui ferait fauter si on la déréférençait. Ici, null : même intention, et
        //   la faute est nommée au lieu d'être un segfault. Inatteignable de toute
        //   façon — pccache vaut 0xFFFFFFFF et `a >> 12` ne dépasse jamais 0xFFFFF,
        //   donc le test de page ne peut pas réussir avant un remplissage.
        _386_common.pccache = 0xFFFFFFFF;
        _386_common.pccache2 = null;
    }

    // DEVIATION: flushmmucache_cr3() N'EST PAS flushmmucache(), et l'aliaser en est une.
    //
    //   flushmmucache_cr3() : la boucle sur les 256 anneaux, et RIEN d'autre.
    //   flushmmucache()     : la meme boucle, PLUS mmuflush++, pccache = 0xFFFFFFFF
    //                         et pccache2 = 0xFFFFFFFF.
    //
    // Le C# ecrase donc pccache et pccache2 la ou PCem les laisse intacts. Dormant au
    // palier (a) : les deux appelants vivants (x86seg.cs, dans loadcs et loadcsjmp)
    // exigent `CPL == 3 && oldcpl != 3`, et le POST de l'AT reste a CPL 0.
    //
    // MAIS LE BLOC C L'APPELLE DIX-HUIT FOIS — grep -c dans x86seg.c — donc cette
    // ligne doit devenir une vraie transcription avant que le mode protege tourne.
    // NON MESURE : pccache n'est pas dans h_state, donc l'effet sur les champs
    // compares, et le cout en cycles via getpccache, restent inconnus.
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
    // Le chemin d'INSTRUCTION (pcem: mem.c:420-442). Transcrit en A2.2a.
    // -----------------------------------------------------------------------

    /// <summary>pcem: mem.c:420 — rend le tableau porteur de la page d'instruction
    /// qui contient <paramref name="a"/>, et le biais à lui ajouter.
    ///
    /// En C la fonction rend un `uint8_t *` BIAISÉ, relu par l'appelant comme
    /// `pccache2[a]` avec l'adresse VIRTUELLE complète. Ici, porteur et biais
    /// séparés : `porteur[biais + a]` transcrit ce déréférencement littéralement.
    /// Le biais est négatif dans le cas général — c'est le principe même du procédé,
    /// pas un accident.</summary>
    internal static byte[] getpccache(uint32_t a, out int bias)
    {
        uint32_t a2 = a;

        // omitted: `if (cr0 >> 31) { a = mmutranslate_read(a); ... }` — pagination.
        //   Ni le 8088 ni le 286 ne paginent : cr0 bit 31 est un 386.
        a &= rammask;

        if (_mem_exec[a >> 14] != null)
        {
                // C'est ICI que le coût du préfetch bascule entre ROM et RAM, à chaque
                // changement de page d'instruction. Sur un 8088 les deux valeurs sont
                // nulles et l'écriture est sans effet.
                if ((read_mapping[a >> 14]!.flags & MEM_MAPPING_ROM) != 0)
                        cpu.cpu_prefetch_cycles = cpu.cpu_rom_prefetch_cycles;
                else
                        cpu.cpu_prefetch_cycles = cpu.cpu_mem_prefetch_cycles;

                bias = unchecked(_mem_exec_off[a >> 14] + (int)(a & 0x3000) - (int)(a2 & ~0xFFFu));
                return _mem_exec[a >> 14]!;
        }

        // omitted: pclog("Bad getpccache %08X\n", a) — trace de débogage, sortie pure,
        //   comme partout ailleurs dans ce port. Le RETOUR, lui, est transcrit : un
        //   fetch hors cartographie doit lire 0xFF, pas fauter.
        bias = unchecked(0 - (int)(a2 & ~0xFFFu));
        return ff_array;
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

    // readmemll / writememll vivent dans mem.c : leurs appels à readmemwl /
    // writememwl sont intra-unité, donc jamais détournés par --wrap. Ils visent
    // __real_.
    //
    // ILS ONT ETE DES RACCOURCIS JUSQU'A LA SECONDE TABLE. Ils se contentaient
    // de deux accès de mot, ce qui suffisait au palier (a) où personne ne les
    // appelait. SGDT les a exercés pour de vrai, et l'écart s'est vu tout de
    // suite :
    //   0F 01 /0 : SGDT vers [BX] — cycles : oracle 20, C# 38
    // Le C, lui, traite trois cas distincts sur une adresse mal alignée : à
    // cheval sur une page il scinde en deux mots, sinon il écrit les QUATRE
    // octets d'un coup par writelookup2, et il ne retombe sur les handlers de
    // mapping que si le raccourci n'est pas disponible.

    /// <summary>pcem: mem.c — readmemll</summary>
    internal static uint32_t readmemll(uint32_t addr)
    {
        mem_mapping_t? map;

        mem_logical_addr = addr;

        if ((addr & 3) != 0)
        {
                if (cpu.cpu_cyrix_alignment == 0 || (addr & 7) > 4)
                        x86.cycles -= cpu.timing_misaligned;
                if ((addr & 0xFFF) > 0xFFC)
                {
                        // omitted: mmutranslate_read — pas de pagination sur un 286.
                        return (uint32_t)__real_readmemwl(addr) |
                               ((uint32_t)__real_readmemwl(addr + 2) << 16);
                }
                else if (readlookup2[addr >> 12] != -1)
                {
                        var i = unchecked(readlookup2[addr >> 12] + (int)addr);
                        return (uint32_t)(ram[i] | (ram[i + 1] << 8) |
                                          (ram[i + 2] << 16) | (ram[i + 3] << 24));
                }
        }

        // omitted: page_lookup[] (dynarec) et mmutranslate_read (pagination).
        addr &= rammask;

        map = read_mapping[addr >> 14];
        if (map != null)
        {
                if (map.read_l != null)
                        return map.read_l(addr, map.p);

                if (map.read_w != null)
                        return (uint32_t)(map.read_w(addr, map.p) |
                                          (map.read_w(addr + 2, map.p) << 16));

                if (map.read_b != null)
                        return (uint32_t)(map.read_b(addr, map.p) |
                                          (map.read_b(addr + 1, map.p) << 8) |
                                          (map.read_b(addr + 2, map.p) << 16) |
                                          (map.read_b(addr + 3, map.p) << 24));
        }

        return 0xffffffff;
    }

    /// <summary>L'enveloppe de writememll, pendant de __wrap_writememll (C7a).
    ///
    /// QUATRE ENTREES DE JOURNAL, UNE PAR OCTET, et AUCUN compteur. Le compteur serait
    /// faux pour la raison qui a fait retirer les quatre de la mémoire à M2 : `--wrap`
    /// est un mécanisme de LIEN et n'intercepte pas les appels internes à mem.c, si bien
    /// qu'un accès à cheval compterait une fois en C et deux ici. Le journal, lui, note
    /// des ADRESSES et des VALEURS : il est vrai des deux côtés quel que soit le chemin.
    ///
    /// POURQUOI MAINTENANT : writememl est une macro (386_common.h:32-36) qui descend
    /// ici, et x86_doabrt empile le code d'erreur par writememl dès que
    /// intgatesize != 16 — or intgatesize vaut ZÉRO au départ. Cette écriture n'avait
    /// donc aucun témoin : ni compteur, ni journal, ni hachage de RAM, RunSingle
    /// n'appelant jamais h_ram_hash.</summary>
    internal static void writememll(uint32_t addr, uint32_t val)
    {
        wlog(addr & rammask, (uint8_t)val);
        wlog((addr + 1) & rammask, (uint8_t)(val >> 8));
        wlog((addr + 2) & rammask, (uint8_t)(val >> 16));
        wlog((addr + 3) & rammask, (uint8_t)(val >> 24));
        __real_writememll(addr, val);
    }

    /// <summary>pcem: mem.c — writememll, le CORPS. Son chemin à cheval appelle
    /// __real_writememwl et non writememwl, donc sans journaliser une seconde fois —
    /// exactement comme le C, où __wrap_writememll descend dans __real_writememll dont
    /// les appels INTERNES échappent à --wrap.</summary>
    private static void __real_writememll(uint32_t addr, uint32_t val)
    {
        mem_mapping_t? map;

        mem_logical_addr = addr;

        if ((addr & 3) != 0)
        {
                if (cpu.cpu_cyrix_alignment == 0 || (addr & 7) > 4)
                        x86.cycles -= cpu.timing_misaligned;
                if ((addr & 0xFFF) > 0xFFC)
                {
                        // omitted: mmutranslate_write — pas de pagination sur un 286.
                        __real_writememwl(addr, (uint16_t)val);
                        __real_writememwl(addr + 2, (uint16_t)(val >> 16));
                        return;
                }
                else if (writelookup2[addr >> 12] != -1)
                {
                        var i = unchecked(writelookup2[addr >> 12] + (int)addr);
                        ram[i] = (byte)val;
                        ram[i + 1] = (byte)(val >> 8);
                        ram[i + 2] = (byte)(val >> 16);
                        ram[i + 3] = (byte)(val >> 24);
                        return;
                }
        }

        // omitted: page_lookup[] (dynarec) et mmutranslate_write (pagination).
        addr &= rammask;

        map = write_mapping[addr >> 14];
        if (map != null)
        {
                if (map.write_l != null)
                        map.write_l(addr, val, map.p);
                else if (map.write_w != null)
                {
                        map.write_w(addr, (uint16_t)val, map.p);
                        map.write_w(addr + 2, (uint16_t)(val >> 16), map.p);
                }
                else if (map.write_b != null)
                {
                        map.write_b(addr, (uint8_t)val, map.p);
                        map.write_b(addr + 1, (uint8_t)(val >> 8), map.p);
                        map.write_b(addr + 2, (uint8_t)(val >> 16), map.p);
                        map.write_b(addr + 3, (uint8_t)(val >> 24), map.p);
                }
        }
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
                _mem_exec[c >> 14] = null;
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
                                        if (mapping.exec != null)
                                        {
                                                _mem_exec[c >> 14] = mapping.exec;
                                                _mem_exec_off[c >> 14] = mapping.exec_offset + (int)(c - mapping.@base);
                                        }
                                        else
                                                _mem_exec[c >> 14] = null;
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
    //
    // DEVIATION: la garde `dest == null` n'existe pas dans le C, qui écrit
    //   « while (dest != mapping) { prev = dest; dest = dest->next; } » SANS test de fin
    //   de liste (mem.c:1166-1170). Marquée à M13, quand elle a cessé d'être théorique :
    //   c'est exactement ce test qui empêche ici le mode de panne de PB-31. Chez PCem,
    //   cga_close libère le cga_t sans retirer le mem_mapping_t qu'il contient
    //   (vid_cga.c:441-446), le maillon reste chaîné en pointeur pendant, et le
    //   parcours suivant part dans la mémoire réallouée — faute de segmentation
    //   reproduite sous gdb dans l'oracle. Ici rien n'est libéré et la liste reste
    //   parcourable ; la garde ne sert donc jamais sur le chemin du 5150, mais sans
    //   elle un maillon manquant lèverait une NullReferenceException au lieu de rendre.
    //   Voir VERIFICATION.md § M13.
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

        // pcem: mem.c:1334
        Array.Fill(ff_array, (byte)0xff);
    }

    // pcem: mem.c:1340-1400, réduit au 5150 (pas de RAM au-delà de 640 Ko)
    internal static void mem_alloc()
    {
        // Quatre octets de marge : les accès 16/32 bits lisent ram[i+1..i+3]. En C
        // un accès à cheval sur la fin lit la mémoire adjacente ; ici il lèverait.
        ram = new byte[mem_size * 1024 + 4];

        Array.Clear(read_mapping);
        Array.Clear(write_mapping);
        Array.Clear(_mem_exec); // pcem: mem.c:1371
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

        // pcem: mem.c:1428-1430 — la fin de mem_alloc. Avec key = 2 et state = 2,
        // mem_a20_recalc() ne prend AUCUNE de ses deux branches : rammask garde ce
        // que resetx86() a posé. C'est voulu.
        mem_a20_key = 2;
        mem_a20_alt = 0;
        mem_a20_recalc();

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
    // 64 depuis C7a, et la valeur est CONFRONTEE a celle de l'oracle par CheckAbi
    // plutot que recopiee : les deux 16 etaient ecrits en dur et jamais compares, si
    // bien que monter un seul des deux donnait un vert TRONQUE au lieu d'une erreur.
    // Le motif de 64 est dans harness.h — 38 entrees pour la branche 286 de
    // taskswitch286, et un pire cas de porte d'appel a 66 qui reste au-dela.
    internal const int WLOG_MAX = 64;
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
