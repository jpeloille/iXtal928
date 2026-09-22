// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/cpu/386_common.c + includes/private/cpu/386_common.h
// SHA256: voir oracle.tsv ; vérifier avec tools/check-oracle.sh
// STATUS: partial — cpu_state, les globaux que 808x.c et x86seg.c consomment, et
//         depuis A2.2a le CHEMIN DE FETCH de l'interpréteur 386/286.
//         x86_int et le reste arrivent plus tard.
//
// C'est ici que PCem définit `cpu_state`, malgré que son type vive dans x86.h.
// On garde ce découpage plutôt que de tout rassembler dans x86.cs : c'est ce qui
// fait qu'un `grep -n cpu_state` tombe au même endroit des deux côtés.

using System.Runtime.CompilerServices;
using iXtal26.Memory;
using static iXtal26.Cpu.x86;

namespace iXtal26.Cpu;

internal static partial class _386_common
{
    // pcem: 386_common.c:6
    internal static readonly cpu_state_t cpu_state = new();

    // pcem: 386_common.c:28
    internal static int nmi_enable = 1;

    // -----------------------------------------------------------------------
    // Le chemin de FETCH (pcem: 386_common.c:36-37 et 386_common.h:100-176).
    //
    // exec386 fait `fastreadl(cs + pc)` à CHAQUE instruction (386.c:176) : c'est
    // le chemin d'instruction inconditionnel, mode réel compris. Rien ici n'est
    // propre à la pagination ni au dynarec.
    //
    // Le cache tient UNE page de 4 Ko. Tant que le pc reste dedans, aucun appel à
    // getpccache ; au franchissement, un seul, qui repose aussi le coût de préfetch
    // selon que la page est ROM ou RAM.
    // -----------------------------------------------------------------------

    // pcem: 386_common.c:36-37. pccache2 est un pointeur BIAISÉ en C ; ici porteur
    // et biais séparés, selon l'idiome déjà retenu pour mem_mapping_t.exec.
    // 0xFFFFFFFF est l'état « vide » : `a >> 12` ne l'atteint jamais.
    internal static uint32_t pccache = 0xFFFFFFFF;
    internal static byte[]? pccache2;
    internal static int pccache2_bias;

    /// <summary>Le déréférencement `pccache2[a]` du C, en un seul endroit.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int At(uint32_t a) => unchecked(pccache2_bias + (int)a);

    // pcem: 386_common.h:100
    internal static uint8_t fastreadb(uint32_t a)
    {
        if ((a >> 12) == pccache)
                return pccache2![At(a)];

        var t = mem.getpccache(a, out var bias);
        if (cpu_state.abrt != 0)
                return 0;
        pccache = a >> 12;
        pccache2 = t;
        pccache2_bias = bias;
        return pccache2[At(a)];
    }

    // pcem: 386_common.h:114. Le test `(a & 0xFFF) > 0xFFE` attrape le mot à cheval
    // sur deux pages : il faut alors DEUX getpccache, un par page.
    internal static uint16_t fastreadw(uint32_t a)
    {
        uint16_t val;
        if ((a & 0xFFF) > 0xFFE)
        {
                val = fastreadb(a);
                val |= (uint16_t)(fastreadb(a + 1) << 8);
                return val;
        }
        if ((a >> 12) == pccache)
                return ReadW(pccache2!, At(a));

        var t = mem.getpccache(a, out var bias);
        if (cpu_state.abrt != 0)
                return 0;
        pccache = a >> 12;
        pccache2 = t;
        pccache2_bias = bias;
        return ReadW(pccache2, At(a));
    }

    // pcem: 386_common.h:132
    internal static uint32_t fastreadl(uint32_t a)
    {
        uint32_t val;
        if ((a & 0xFFF) < 0xFFD)
        {
                if ((a >> 12) != pccache)
                {
                        var t = mem.getpccache(a, out var bias);
                        if (cpu_state.abrt != 0)
                                return 0;
                        pccache2 = t;
                        pccache2_bias = bias;
                        pccache = a >> 12;
                }
                return ReadL(pccache2!, At(a));
        }
        val = fastreadw(a);
        val |= (uint32_t)(fastreadw(a + 2) << 16);
        return val;
    }

    // DEVIATION: le C lit `*(uint16_t *)&pccache2[a]` et `*(uint32_t *)&...`, donc
    //   un accès non aligné en petit-boutiste sur le tableau d'octets. Reconstruit
    //   ici octet par octet : même valeur, sans dépendre du boutisme de l'hôte.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint16_t ReadW(byte[] p, int i) => (uint16_t)(p[i] | (p[i + 1] << 8));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint32_t ReadL(byte[] p, int i) =>
        (uint32_t)(p[i] | (p[i + 1] << 8) | (p[i + 2] << 16) | (p[i + 3] << 24));

    // -----------------------------------------------------------------------
    // pcem: 386_common.h:8-40 — les accès mémoire RAPIDES de l'interpréteur 386.
    //
    // À ne pas confondre avec les readmemw/writememw de 808x.c : ce sont DEUX jeux
    // de macros homonymes, dans deux unités de traduction différentes, et c'est
    // pour cela qu'ils coexistent chez PCem. Ceux-ci court-circuitent le dispatch
    // par mem_mapping quand la page est dans readlookup2/writelookup2 ET que
    // l'accès est aligné ; sinon ils retombent sur readmemwl/writememwl.
    //
    // Le repli n'est PAS équivalent au chemin rapide du point de vue du vecteur
    // d'état : readmemwl incrémente Counters.n_readmemwl, un champ comparé. Sauter
    // le chemin rapide ferait donc rougir le fuzzeur — c'est voulu.
    // -----------------------------------------------------------------------

    internal static uint16_t readmemw(uint32_t s, uint32_t a)
    {
        var addr = s + a;
        if (mem.readlookup2[addr >> 12] == -1 || (addr & 1) != 0)
                return mem.readmemwl(addr);
        return ReadW(mem.ram, unchecked(mem.readlookup2[addr >> 12] + (int)addr));
    }

    internal static void writememw(uint32_t s, uint32_t a, uint16_t v)
    {
        var addr = s + a;
        if (mem.writelookup2[addr >> 12] == -1 || (addr & 1) != 0)
        {
                mem.writememwl(addr, v);
                return;
        }
        var i = unchecked(mem.writelookup2[addr >> 12] + (int)addr);
        mem.ram[i] = (byte)v;
        mem.ram[i + 1] = (byte)(v >> 8);
    }

    internal static uint8_t readmemb(uint32_t s, uint32_t a)
    {
        var addr = s + a;
        if (mem.readlookup2[addr >> 12] == -1)
                return mem.readmembl(addr);
        return mem.ram[unchecked(mem.readlookup2[addr >> 12] + (int)addr)];
    }

    internal static void writememb(uint32_t s, uint32_t a, uint8_t v)
    {
        var addr = s + a;
        if (mem.writelookup2[addr >> 12] == -1)
        {
                mem.writemembl(addr, v);
                return;
        }
        mem.ram[unchecked(mem.writelookup2[addr >> 12] + (int)addr)] = v;
    }

    // omitted: readmeml/readmemq et writememl/writememq — aucun appelant tant que
    //   les handlers 32 bits n'existent pas. Ils arrivent avec le groupe qui les
    //   emploie, pas avant.

    // -----------------------------------------------------------------------
    // L'ADRESSE EFFECTIVE (pcem: 386_dynarec.c:85-130).
    //
    // fetch_ea_16 décode le ModRM, calcule eaaddr, et — c'est le point — CACHE un
    // pointeur direct vers l'octet visé quand la page est dans readlookup2 ou
    // writelookup2. geteab/seteab s'en servent pour court-circuiter le dispatch.
    //
    // CE CACHE N'EST PAS UNE OPTIMISATION QU'ON POURRAIT SAUTER. Sans lui, geteab
    // appelle readmemb -> readmembl, qui incrémente Counters.n_readmembl — un champ
    // COMPARÉ. Le fuzzeur verrait donc la déviation, ce qui est exactement ce qu'on
    // veut, mais cela signifie qu'il faut le transcrire, pas le contourner.
    //
    // DEVIATION: eal_r/eal_w sont des `uint32_t *` en C. Ici, validité + offset dans
    //   ram[] : la sentinelle -1 dit « pas de raccourci », comme readlookup2.
    // -----------------------------------------------------------------------
    internal static int eal_r = -1;
    internal static int eal_w = -1;

    // pcem: 386_dynarec.c:85
    private static void fetch_ea_16_long(uint32_t rmdat)
    {
        eal_r = eal_w = -1;
        easeg = cpu_state.ea_seg!.@base;
        if (cpu_mod == 0 && cpu_rm == 6)
        {
                cpu_state.eaaddr = getword();
        }
        else
        {
                switch (cpu_mod)
                {
                case 0:
                        cpu_state.eaaddr = 0;
                        break;
                case 1:
                        cpu_state.eaaddr = (uint16_t)(int8_t)(rmdat >> 8);
                        cpu_state.pc++;
                        break;
                case 2:
                        cpu_state.eaaddr = getword();
                        break;
                }
                cpu_state.eaaddr += (uint32_t)(_808x.Mod1Add(0, cpu_rm) + _808x.Mod1Add(1, cpu_rm));
                if (_808x.Mod1IsSS(cpu_rm) && cpu_state.ssegs == 0)
                {
                        easeg = ss;
                        cpu_state.ea_seg = cpu_state.seg_ss;
                }
                cpu_state.eaaddr &= 0xFFFF;
        }
        if (easeg != 0xFFFFFFFF && ((easeg + cpu_state.eaaddr) & 0xFFF) <= 0xFFC)
        {
                var addr = easeg + cpu_state.eaaddr;
                if (mem.readlookup2[addr >> 12] != -1)
                        eal_r = unchecked(mem.readlookup2[addr >> 12] + (int)addr);
                if (mem.writelookup2[addr >> 12] != -1)
                        eal_w = unchecked(mem.writelookup2[addr >> 12] + (int)addr);
        }
    }

    /// <summary>pcem: 386_dynarec.c:128 — la macro fetch_ea_16.
    ///
    /// En C elle contient un `return 1` sur abandon, ce qu'une méthode C# ne peut
    /// pas faire pour son appelant. Elle rend donc `true` quand le handler doit
    /// sortir, et chaque site d'appel écrit `if (fetch_ea_16(...)) return 1;` —
    /// la même chose, dite explicitement.</summary>
    internal static bool fetch_ea_16(uint32_t rmdat)
    {
        cpu_state.pc++;
        cpu_mod = (int8_t)((rmdat >> 6) & 3);
        cpu_reg = (int8_t)((rmdat >> 3) & 7);
        cpu_rm = (int8_t)(rmdat & 7);
        if (cpu_mod != 3)
        {
                fetch_ea_16_long(rmdat);
                if (cpu_state.abrt != 0)
                        return true;
        }
        return false;
    }

    // pcem: 386_common.h:180-220 — lecture et écriture par l'adresse effective.
    // `cpu_mod == 3` désigne un REGISTRE, pas la mémoire : aucun accès n'a lieu.

    internal static uint8_t geteab()
    {
        if (cpu_mod == 3)
                return (cpu_rm & 4) != 0 ? cpu_state.regs[cpu_rm & 3].b.h : cpu_state.regs[cpu_rm & 3].b.l;
        if (eal_r != -1)
                return mem.ram[eal_r];
        return readmemb(easeg, cpu_state.eaaddr);
    }

    internal static uint16_t geteaw()
    {
        if (cpu_mod == 3)
                return cpu_state.regs[cpu_rm].w;
        if (eal_r != -1)
                return ReadW(mem.ram, eal_r);
        return readmemw(easeg, cpu_state.eaaddr);
    }

    // pcem: 386_common.h:224-236 — seteab/seteaw sont des MACROS en C, avec un
    // `if/else` à trois branches. Transcrites en méthodes : même arbre de décision.
    internal static void seteab(uint8_t v)
    {
        if (cpu_mod != 3)
        {
                if (eal_w != -1)
                        mem.ram[eal_w] = v;
                else
                        mem.writemembl(easeg + cpu_state.eaaddr, v);
        }
        else if ((cpu_rm & 4) != 0)
                cpu_state.regs[cpu_rm & 3].b.h = v;
        else
                cpu_state.regs[cpu_rm & 3].b.l = v;
    }

    internal static void seteaw(uint16_t v)
    {
        if (cpu_mod != 3)
        {
                if (eal_w != -1)
                {
                        mem.ram[eal_w] = (byte)v;
                        mem.ram[eal_w + 1] = (byte)(v >> 8);
                }
                else
                        mem.writememwl(easeg + cpu_state.eaaddr, v);
        }
        else
                cpu_state.regs[cpu_rm].w = v;
    }

    // -----------------------------------------------------------------------
    // Les GARDES (pcem: 386_common.h:58-92). Elles ne sont PAS inertes en mode
    // réel : CHECK_WRITE teste le bit « inscriptible » d'access, et loadseg pose
    // access = 2 justement pour qu'elles passent (x86seg.cs:26). CHECK_READ mord
    // pour de bon quand un accès MOT déborde de limit_high à l'offset 0xFFFF.
    //
    // Ce sont les premiers lecteurs des champs de cache descripteur entrés à A1a :
    // limit_low, limit_high et access cessent ici d'être des zéros comparés à des
    // zéros. Comme fetch_ea_16, elles rendent `true` quand le handler doit sortir.
    // -----------------------------------------------------------------------

    internal static bool SEG_CHECK_READ(x86seg seg)
    {
        if (seg.@base == 0xffffffff)
        {
                x86seg_c.x86gpf("Segment can't read", 0);
                return true;
        }
        return false;
    }

    internal static bool SEG_CHECK_WRITE(x86seg seg)
    {
        if (seg.@base == 0xffffffff)
        {
                x86seg_c.x86gpf("Segment can't write", 0);
                return true;
        }
        return false;
    }

    internal static bool CHECK_READ(x86seg seg, uint32_t low, uint32_t high)
    {
        if ((low < seg.limit_low) || (high > seg.limit_high) ||
            ((x86.msw & 1) != 0 && (cpu_state.eflags & x86.VM_FLAG) == 0 && ((seg.access & 10) == 8)))
        {
                x86seg_c.x86gpf("Limit check", 0);
                return true;
        }
        return false;
    }

    internal static bool CHECK_WRITE(x86seg seg, uint32_t low, uint32_t high)
    {
        if ((low < seg.limit_low) || (high > seg.limit_high) || (seg.access & 2) == 0 ||
            ((x86.msw & 1) != 0 && (cpu_state.eflags & x86.VM_FLAG) == 0 && (seg.access & 8) != 0))
        {
                x86seg_c.x86gpf("Limit check", 0);
                return true;
        }
        return false;
    }

    // pcem: 386_common.h:160-176. Le pc avance AVANT la lecture, et la lecture se
    // fait à l'adresse d'avant : c'est ce qui permet aux handlers de relire leurs
    // propres octets d'immédiat après coup.
    internal static uint8_t getbyte()
    {
        cpu_state.pc++;
        return fastreadb(x86.cs + (cpu_state.pc - 1));
    }

    internal static uint16_t getword()
    {
        cpu_state.pc += 2;
        return fastreadw(x86.cs + (cpu_state.pc - 2));
    }

    internal static uint32_t getlong()
    {
        cpu_state.pc += 4;
        return fastreadl(x86.cs + (cpu_state.pc - 4));
    }

    // -----------------------------------------------------------------------
    // pcem: 386_common.c:117-155 — x86_int, et x86illegal qui n'est que son
    // habillage pour l'opcode invalide.
    //
    // Atteignable dès A2.2d : ILLEGAL_ON mord sur C6/C7 dès que le champ `reg` du
    // ModRM est non nul, ce que des octets aléatoires produisent sept fois sur huit.
    // -----------------------------------------------------------------------
    internal static void x86_int(int num)
    {
        uint32_t addr;
        _386.flags_rebuild();
        cpu_state.pc = cpu_state.oldpc;
        if ((msw & 1) != 0)
        {
                pc.fatal("x86_int en mode protege : pmodeint n'est pas transcrit (Ap)\n");
                return;
        }

        addr = (uint32_t)(num << 2) + idt.@base;

        if ((uint32_t)((num << 2) + 3) > idt.limit)
        {
                // Inatteignable en mode réel : resetx86 pose idt.limit = 0xFFFF pour
                // un 286 comme pour un 8088 (808x.c:689-690). Ce n'est donc pas une
                // omission mais une branche morte — et elle échoue, pour qu'un jour
                // où elle cesserait de l'être on l'apprenne au lieu de le subir.
                pc.fatal($"x86_int : idt.limit = {idt.limit:X} — branche de triple faute non transcrite\n");
                return;
        }

        if (stack32 != 0)
        {
                writememw(ss, ESP - 2, cpu_state.flags);
                writememw(ss, ESP - 4, CS);
                writememw(ss, ESP - 6, (uint16_t)cpu_state.pc);
                ESP -= 6;
        }
        else
        {
                writememw(ss, (uint32_t)((SP - 2) & 0xFFFF), cpu_state.flags);
                writememw(ss, (uint32_t)((SP - 4) & 0xFFFF), CS);
                writememw(ss, (uint32_t)((SP - 6) & 0xFFFF), (uint16_t)cpu_state.pc);
                SP -= 6;
        }

        cpu_state.flags &= unchecked((uint16_t)~I_FLAG);
        cpu_state.flags &= unchecked((uint16_t)~T_FLAG);
        cpu_state.pc = readmemw(0, addr);
        x86seg_c.loadcs(readmemw(0, addr + 2));

        cycles -= 70;
        // omitted: CPU_BLOCK_END() — vide pour l'interpréteur (386.c:18).
    }

    // pcem: 386_common.c:156-165
    internal static void x86illegal() => x86_int(6);

    /// <summary>pcem: 386_ops.h:5-12 — la macro ILLEGAL_ON. Comme fetch_ea_16, elle
    /// rend `true` quand le handler doit sortir, le `return 0` du C étant
    /// intransportable tel quel.</summary>
    internal static bool ILLEGAL_ON(bool cond)
    {
        if (cond)
        {
                cpu_state.pc = cpu_state.oldpc;
                x86illegal();
                return true;
        }
        return false;
    }
}
