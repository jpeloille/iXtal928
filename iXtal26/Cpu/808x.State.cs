// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: tools/oracle/harness.c  (h_reset / h_step / h_run / h_getstate)
// STATUS: host — porte de diagnostic, pas de code PCem transcrit.
//         R2 (parité de lignes) ne s'applique pas.
//
// LA PORTE, au singulier.
//
// Tout le modèle de temps vit dans des `private static` de _808x : memcycs,
// fetchcycles, fetchclocks, prefetchw, prefetchpc, prefetchqueue, nextcyc,
// cycdiff, tsc_frac. C'est la transcription fidèle du C, où ces variables sont
// `static` et donc invisibles hors de leur unité de traduction.
//
// InternalsVisibleTo n'ouvre pas `private` : iXtal26.Diff ne peut rien en voir.
// La solution est exactement celle retenue côté C, où harness.c compile 808x.c
// DANS son unité de traduction pour y accéder : ici, une classe partielle. Le
// fichier est une porte, pas une fenêtre — une seule méthode de lecture, et la
// règle « static C -> private C# » reste intacte.

using iXtal26.Diag;
using iXtal26.Memory;
using static iXtal26.Cpu._386_common;
using static iXtal26.Cpu.x86;

namespace iXtal26.Cpu;

internal static partial class _808x
{
    internal static uint64_t ins_count;

    /// <summary>Reset machine complet : XT, 8088. Doit rester le pendant exact
    /// de h_reset() (tools/oracle/harness.c), sans quoi les deux cœurs ne
    /// partiraient pas du même point et le diff ne mesurerait rien.</summary>
    private static bool mem_inited;

    /// <summary>Carte plate : 1 Mo de RAM sur tout l'espace d'adressage, à travers
    /// le vrai mem.c. Pendant exact de h_flat_map() (tools/oracle/harness.c) — les
    /// deux cœurs doivent partir de la MÊME carte mémoire, sinon le diff compare
    /// deux machines.</summary>
    /// <summary>Pendant de h_flat_map(). `kb` : 1 024 pour le 8088 et le 286, 16 384
    /// pour le 386 (G2, D2) — voir h_ram_top() côté oracle.</summary>
    private static void FlatMap(int kb = 1024)
    {
        // Pendant du chemin court de h_flat_map (G2, D3) : la carte de 16 Mo n'est
        // allouée qu'une fois ; si mem.ram est encore celle qu'elle a posée, on la remet
        // à zéro et on vide le cache de traduction. Une machine amorcée (initpc) a
        // réalloué mem.ram, donc ReferenceEquals échoue et le chemin complet reprend.
        if (kb == 16384 && ReferenceEquals(mem.ram, flatRam) && mem.mem_size == kb)
        {
                Array.Clear(mem.ram);
                mem.resetreadlookup();
                return;
        }
        mem.mem_size = kb;
        if (!mem_inited)
        {
                mem.mem_init();
                mem_inited = true;
        }
        mem.mem_alloc();
        var top = (uint32_t)kb * 1024;
        mem.mem_set_mem_state(0x000000, top, mem.MEM_READ_INTERNAL | mem.MEM_WRITE_INTERNAL);
        mem.mem_mapping_add(h_flat_mapping, 0x000000, top,
                            mem.mem_read_ram, mem.mem_read_ramw, mem.mem_read_raml,
                            mem.mem_write_ram, mem.mem_write_ramw, mem.mem_write_raml,
                            mem.ram, 0, mem.MEM_MAPPING_INTERNAL, null);
        flatRam = kb == 16384 ? mem.ram : null;
    }

    private static byte[]? flatRam;

    private static readonly mem_mapping_t h_flat_mapping = new();

    /// <summary>La même carte plate, pour le cœur 286. Pendant de h_flat_map()
    /// quand h_core vaut H_CORE_286 : c'est la MÊME fonction côté C, d'où l'appel
    /// à FlatMap() ici plutôt qu'une copie.</summary>
    internal static void FlatMap286() => FlatMap();

    /// <summary>La carte de 16 Mo du cœur 386 (G2, D2).</summary>
    internal static void FlatMap386() => FlatMap(16384);

    internal static void Reset()
    {
        FlatMap();
        ResetCounters();

        // Configuration machine, posée AVANT resetx86() : celle-ci branche sur
        // AT, is486 et is386 pour choisir le vecteur de reset et rammask.
        AT = 0;
        is386 = 0;
        is486 = 0;
        is8086 = 0; // 8088 : file de préfetch de 4 octets, pas 6
        cpu_16bitbus = 0;
        AMSTRAD = TANDY = PCI = MCA = 0;

        // LE VRAI cpu_set(), pour cpus_8088[0] (M16, étape 6) — pendant exact de h_reset()
        // côté oracle. Ce chemin n'amorce aucune machine, mais resetx86() appelle
        // cpu_update_waitstates(), qui lit la table de la machine : il faut donc une
        // machine et une entrée. Celles du 5150, la seule que ce chemin ait jamais servie.
        Models.model_c.model = 0;
        cpu_c.cpu_manufacturer = 0;
        cpu_c.cpu = 0;
        cpu_c.cpu_set();

        // pcem: pit.c:52 — tops de l'oscillateur maître (14,318 MHz) par cycle
        // CPU, en 32:32, pour un 8088 à 4 772 728 Hz (cpu_tables.c:33).
        xt_cpu_multi = (uint64_t)((14318184.0 * (double)(1UL << 32)) / 4772728.0);

        timer.tsc = 0;
        timer.timer_target = 0x7FFFFFFF;

        resetx86();
        ResetTimingState();
    }

    /// <summary>Compteurs de diagnostic. Pendant de h_reset():2-4 et du haut de
    /// h_boot() — dans les deux cas AVANT resetx86().</summary>
    internal static void ResetCounters()
    {
        Counters.Reset();
        mem.wlog_reset();
        ins_count = 0;
    }

    /// <summary>Remet à zéro l'état de temps ENTRE instructions et les compteurs
    /// de diagnostic, sans toucher à la carte mémoire. Pendant exact du bloc final
    /// de h_boot() (tools/oracle/harness.c).
    ///
    /// resetx86() ne touche à rien de tout ça : ce sont des statiques de fichier
    /// qui, en C, valent zéro parce qu'elles sont en BSS et que PCem n'amorce
    /// qu'une fois par processus. Le diff de boot amorce deux fois — il faut donc
    /// l'équivalent explicite des deux côtés, sinon le second amorçage part avec
    /// l'état du premier et le diff signale une divergence qui n'existe pas.</summary>
    internal static void ResetDiagState()
    {
        ResetCounters();
        ResetTimingState();
    }

    internal static void ResetTimingState()
    {
        nextcyc = 0;
        memcycs = 0;
        cycdiff = 0;
        current_diff = 0;
        fetchcycles = 0;
        fetchclocks = 0;
        tsc_frac = 0;
        noint = 0;
        inhlt = 0;
        takeint = 0;
        cycles = 0;
        ins = 0;
        insc = 0;
    }

    /// <summary>Une instruction exactement. Pendant de h_step().
    /// execx86 boucle tant que cycles > 0 : en posant le budget à 1, le corps
    /// s'exécute une fois puis sort, tout opcode coûtant au moins un cycle.</summary>
    internal static int Step()
    {
        cycles = 1;
        execx86(0);
        ins_count++;
        return 1 - cycles;
    }

    /// <summary>Budget long, la forme de runpc(). Pendant de h_run().</summary>
    internal static int Run(int cycs)
    {
        var before = ins;
        cycles = 0;
        execx86(cycs);
        ins_count += (uint64_t)(ins - before);
        return cycs - cycles;
    }

    internal static void SetRegs(ushort[] r)
    {
        cpu_state.regs[0].w = r[(int)R.AX];
        cpu_state.regs[3].w = r[(int)R.BX];
        cpu_state.regs[1].w = r[(int)R.CX];
        cpu_state.regs[2].w = r[(int)R.DX];
        cpu_state.regs[4].w = r[(int)R.SP];
        cpu_state.regs[5].w = r[(int)R.BP];
        cpu_state.regs[6].w = r[(int)R.SI];
        cpu_state.regs[7].w = r[(int)R.DI];

        x86seg_c.loadseg(r[(int)R.SS], cpu_state.seg_ss);
        x86seg_c.loadseg(r[(int)R.DS], cpu_state.seg_ds);
        x86seg_c.loadseg(r[(int)R.ES], cpu_state.seg_es);
        x86seg_c.loadcs(r[(int)R.CS]);

        cpu_state.pc = r[(int)R.IP];
        cpu_state.flags = r[(int)R.FLAGS];

        FETCHCLEAR();
    }

    /// <summary>Pendant de h_setregs386 (G2, D0.4), à appeler après SetRegs : moitiés
    /// hautes des registres généraux dans l'ordre de cpu_state.regs, mot haut d'EFLAGS,
    /// sélecteurs de FS et GS.</summary>
    internal static void SetRegs386(ushort[] hi, ushort eflags, ushort fs, ushort gs)
    {
        for (var i = 0; i < 8; i++)
                cpu_state.regs[i].l = (cpu_state.regs[i].l & 0xffff) | ((uint32_t)hi[i] << 16);
        cpu_state.eflags = eflags;
        x86seg_c.loadseg(fs, cpu_state.seg_fs);
        x86seg_c.loadseg(gs, cpu_state.seg_gs);
    }

    internal static void GetRegs(ushort[] r)
    {
        r[(int)R.AX] = cpu_state.regs[0].w;
        r[(int)R.BX] = cpu_state.regs[3].w;
        r[(int)R.CX] = cpu_state.regs[1].w;
        r[(int)R.DX] = cpu_state.regs[2].w;
        r[(int)R.SP] = cpu_state.regs[4].w;
        r[(int)R.BP] = cpu_state.regs[5].w;
        r[(int)R.SI] = cpu_state.regs[6].w;
        r[(int)R.DI] = cpu_state.regs[7].w;
        r[(int)R.CS] = cpu_state.seg_cs.seg;
        r[(int)R.SS] = cpu_state.seg_ss.seg;
        r[(int)R.DS] = cpu_state.seg_ds.seg;
        r[(int)R.ES] = cpu_state.seg_es.seg;
        r[(int)R.IP] = (uint16_t)cpu_state.pc;
        r[(int)R.FLAGS] = cpu_state.flags;
    }

    /// <summary>Le vecteur d'état complet. Tout champ ajouté ici doit l'être
    /// aussi dans h_getstate(), et entrer dans la comparaison : un champ non
    /// comparé est un champ où la dérive se cache.</summary>
    internal static void GetState(ref HState s)
    {
        for (var i = 0; i < 8; i++)
                s.regs[i] = cpu_state.regs[i].l;

        var segs = new[] { cpu_state.seg_cs, cpu_state.seg_ds, cpu_state.seg_es,
                           cpu_state.seg_ss, cpu_state.seg_fs, cpu_state.seg_gs };
        for (var i = 0; i < (int)Seg.COUNT; i++)
        {
                s.seg_sel[i] = segs[i].seg;
                s.seg_base[i] = segs[i].@base;
                s.seg_limit[i] = segs[i].limit;
                s.seg_limit_raw[i] = segs[i].limit_raw;
                s.seg_limit_low[i] = segs[i].limit_low;
                s.seg_limit_high[i] = segs[i].limit_high;
                s.seg_access[i] = segs[i].access;
                s.seg_access2[i] = segs[i].access2;
                s.seg_checked[i] = segs[i].@checked;
        }

        // Les quatre descripteurs système. Globaux hors cpu_state des deux côtés
        // (x86.h:171), d'où le tableau séparé et non un élargissement de segs[].
        var sys = new[] { x86.gdt, x86.ldt, x86.idt, x86.tr };
        for (var i = 0; i < (int)Sys.COUNT; i++)
        {
                s.sys_base[i] = sys[i].@base;
                s.sys_limit[i] = sys[i].limit;
                s.sys_limit_raw[i] = sys[i].limit_raw;
                s.sys_limit_low[i] = sys[i].limit_low;
                s.sys_limit_high[i] = sys[i].limit_high;
                s.sys_checked[i] = sys[i].@checked;
                s.sys_sel[i] = sys[i].seg;
                s.sys_access[i] = sys[i].access;
                s.sys_access2[i] = sys[i].access2;
        }

        s.cr0 = cpu_state.CR0;
        s.cr2 = x86.cr2;
        s.cr3 = x86.cr3;
        s.use32 = x86.use32;
        s.stack32 = x86.stack32;
        s.cpl_override = x86.cpl_override;
        s.cr4 = x86.cr4;
        for (var i = 0; i < 8; i++)
                s.dr[i] = x86.dr[i];

        // Les quatre drapeaux paresseux, LUS et non matérialisés : appeler un
        // flags_rebuild() ici rendrait la représentation paresseuse invisible à toutes
        // les captures. Voir x86.cs pour le raisonnement complet.
        s.flags_op = cpu_state.flags_op;
        s.flags_res = cpu_state.flags_res;
        s.flags_op1 = cpu_state.flags_op1;
        s.flags_op2 = cpu_state.flags_op2;

        s.prefetch_bytes = _386.prefetch_bytes;
        s.prefetch_prefixes = _386.prefetch_prefixes;

        s.ea_seg_idx = -1;
        for (var i = 0; i < (int)Seg.COUNT; i++)
                if (ReferenceEquals(cpu_state.ea_seg, segs[i]))
                        s.ea_seg_idx = i;

        s.flags = cpu_state.flags;
        s.eflags = cpu_state.eflags;
        s.pc = cpu_state.pc;
        s.oldpc = cpu_state.oldpc;
        s.eaaddr = cpu_state.eaaddr;
        s.ssegs = cpu_state.ssegs;
        s.abrt = cpu_state.abrt;

        s.cycles = cycles;
        s.tsc = timer.tsc;
        s.tsc_frac = tsc_frac;
        s.memcycs = memcycs;
        s.fetchcycles = fetchcycles;
        s.fetchclocks = fetchclocks;
        s.nextcyc = nextcyc;
        s.cycdiff = cycdiff;
        s.current_diff = current_diff;
        s.prefetchw = prefetchw;
        s.prefetchpc = prefetchpc;
        Array.Copy(prefetchqueue, s.prefetchqueue, 6);

        s.noint = noint;
        s.inhlt = inhlt;
        s.takeint = takeint;

        s.n_inb = Counters.n_inb;
        s.n_outb = Counters.n_outb;
        s.n_picint = Counters.n_picint;
        s.n_picinterrupt = Counters.n_picinterrupt;
        s.n_timer_process = Counters.n_timer_process;
        s.n_readmembl = Counters.n_readmembl;
        s.n_writemembl = Counters.n_writemembl;
        s.n_readmemwl = Counters.n_readmemwl;
        s.n_writememwl = Counters.n_writememwl;
        s.n_fatal = Counters.n_fatal;

        // LES SEPT GLOBAUX DU MODE PROTEGE (C7a). Ils vivent dans x86.cs et x86seg.cs,
        // pas dans cpu_state — d'ou leur place a la fin du vecteur.
        s.abrt_error = x86.abrt_error;
        s.intgatesize = x86seg_c.intgatesize;
        s.cgate16 = x86.cgate16;
        s.cgate32 = x86.cgate32;
        s.optype = x86.optype;
        s.oldcpl = x86.oldcpl;
        s.cur_status = x86.cpu_cur_status;

        s.ins = ins_count;
    }

    /// <summary>FNV-1a 64 bits sur la RAM. Même constante et même parcours que
    /// h_ram_hash(), sinon la comparaison n'a aucun sens.
    ///
    /// Bornée à mem_size Ko, comme h_ram_hash() : sur la carte plate de Reset() c'est
    /// 1 Mo, mais après initpc() la machine a 640 Ko (+ 4 octets de marge) et lire
    /// RAM_SIZE sortait du tableau. Jamais appelée après un amorçage jusqu'à M5.1 ;
    /// le banc `bench` l'a révélé (IndexOutOfRange ici, segfault côté C).</summary>
    internal static uint64_t RamHash()
    {
        uint64_t hash = 1469598103934665603UL;
        var n = (uint32_t)mem.mem_size * 1024u;
        for (uint32_t i = 0; i < n; i++)
        {
                hash ^= mem.ram[i];
                hash *= 1099511628211UL;
        }
        return hash;
    }
}
