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

    // omitted: readmemb/readmeml/readmemq et writememb/writememl/writememq —
    //   aucun appelant tant que les handlers n'existent pas. Ils arrivent avec le
    //   groupe d'opcodes qui les emploie, pas avant.

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
}
