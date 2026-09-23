// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/includes/private/cpu/x86_ops_prefix.h  (op_seg, lignes 3-68)
//         et pcem-dev/includes/private/cpu/386_ops.h  (ILLEGAL :106-112,
//         op0F_w_a16 :152-158)
// SHA256: voir oracle.tsv ; vérifier avec tools/check-oracle.sh
// STATUS: partial — A11 : les quatre préfixes de segment qu'un 286 porte, le
//         handler ILLEGAL et l'échappement 0F. Les variantes `_l`, `_a32`,
//         `_REPE`, `_REPNE`, FS/GS et les préfixes 66/67 restent dehors,
//         déclarés au registre des omissions.
//
// UN PRÉFIXE N'EST PAS UNE INSTRUCTION : IL EN EXÉCUTE UNE.
//
// op_seg va chercher l'opcode suivant, pose le segment de surcharge, et
// AIGUILLE — il rend directement ce que rend le handler appelé. C'est le
// troisième mécanisme de ce genre dans la table, après POP SS (A4) et le groupe
// FF (A6), et le seul qui soit là pour ça.
//
// CONSÉQUENCE SUR LE PAS-À-PAS, déjà rencontrée à A9 sous un autre angle : un
// « pas » qui commence sur un préfixe exécute DEUX handlers. Ici c'est voulu et
// symétrique des deux côtés — et c'est pourquoi le fuzzeur tire l'octet suivant
// dans le jeu testé (Fuzzer.EnchaineSurLaSuivante) depuis A2.2c.
//
// `ssegs = 1` EST CE QUI DIT « LA SURCHARGE EST ACTIVE ». fetch_ea_16 le lit
// pour décider s'il doit remettre ea_seg à SS quand le mode d'adressage le
// demande (les formes en BP). Sans lui, un `ES: MOV [BP], AX` écrirait dans SS.

using static iXtal26.Cpu._386_common;
using static iXtal26.Cpu.x86;

namespace iXtal26.Cpu;

internal static partial class _386
{
    /// <summary>pcem: x86_ops_prefix.h:3-18 — op_seg(name, seg, …), forme
    /// `_w_a16`.
    ///
    /// DEVIATION: la macro prend DEUX tables — `opcode_table` et
    ///   `normal_opcode_table` — pour que les variantes REPE et REPNE puissent
    ///   aiguiller vers une table spécialisée et retomber sur la normale. Les
    ///   six invocations d'un 286 passent x86_opcodes aux DEUX, donc le test
    ///   `if (opcode_table[...])` est toujours vrai et la seconde branche morte.
    ///   On garde le test : il dit ce que la macro dit.</summary>
    private static OpFn PrefixeSegment(x86seg seg) => fetchdat =>
    {
        fetchdat = fastreadl(x86.cs + cpu_state.pc);
        if (cpu_state.abrt != 0)
                return 1;
        cpu_state.pc++;

        cpu_state.ea_seg = seg;
        cpu_state.ssegs = 1;
        CLOCK_CYCLES(4);
        PREFETCH_PREFIX();

        if (x86_opcodes![fetchdat & 0xff] != null)
                return x86_opcodes[fetchdat & 0xff](fetchdat >> 8);
        return x86_opcodes[fetchdat & 0xff](fetchdat >> 8);
    };

    // pcem: 386_ops.h:106-112 — ILLEGAL.
    //
    // IL REMET pc EN ARRIÈRE avant de lever, pour que l'exception pointe sur
    // l'instruction et non après elle. Même geste que la macro ILLEGAL_ON, dont
    // il est le pendant pour un emplacement entier.
    private static int ILLEGAL(uint32_t fetchdat)
    {
        cpu_state.pc = cpu_state.oldpc;

        x86illegal();
        return 0;
    }

    // pcem: 386_ops.h:152-158 — op0F_w_a16, l'échappement à deux octets.
    //
    // Il n'avance pc que d'UN octet : le premier a déjà été consommé par la
    // boucle, le second est `fetchdat & 0xff`, et c'est lui qui indexe
    // ops_286_0f. Sur un 286 cette table ne porte que six handlers — LGDT,
    // LIDT, LMSW et leurs voisins, tous du mode protégé — donc elle reste vide
    // ici et l'échappement tombe sur opNonTranscrit, bruyamment.
    private static int op0F_w_a16(uint32_t fetchdat)
    {
        int opcode = (int)(fetchdat & 0xff);
        cpu_state.pc++;
        PREFETCH_PREFIX();

        return x86_opcodes_0f![opcode](fetchdat >> 8);
    }

    // pcem: x86_ops_misc.h:701-712 — opLOCK.
    //
    // C'EST UN PRÉFIXE, malgré son en-tête : il va chercher l'opcode suivant et
    // l'aiguille, comme op_seg. Deux différences qui comptent :
    //   - il rend 0 et non 1 quand l'abandon survient pendant le fetch. C'est
    //     PCem ; op_seg rend 1 au même endroit.
    //   - `LOCK NOP` est ILLÉGAL et lui seul. ILLEGAL_ON remet pc sur
    //     l'instruction avant de lever.
    private static int opLOCK(uint32_t fetchdat)
    {
        fetchdat = fastreadl(x86.cs + cpu_state.pc);
        if (cpu_state.abrt != 0)
                return 0;
        cpu_state.pc++;

        if (ILLEGAL_ON((fetchdat & 0xff) == 0x90)) return 0;

        CLOCK_CYCLES(4);
        PREFETCH_PREFIX();
        return x86_opcodes![(fetchdat & 0xff) | cpu_state.op32](fetchdat >> 8);
    }

    // omitted: les variantes `_l`, `_a32`, `_REPE` et `_REPNE` d'op_seg — op32
    //   nul sur un 286, et les tables REPE/REPNE sont posées par le groupe rep.
    // omitted: op_seg(FS, …) et op_seg(GS, …) — le 286 n'a ni FS ni GS, et la
    //   table ne leur donne aucun emplacement.
    // omitted: op_66 et op_67 (x86_ops_prefix.h:86-110) — les préfixes de taille
    //   de donnée et d'adresse sont une invention du 386. Les emplacements 66 et
    //   67 portent ILLEGAL sur un 286, relevé dans la .so.

    /// <summary>pcem: 26, 2E, 36, 3E pour les préfixes ; 64-67 pour ILLEGAL ;
    /// 0F pour l'échappement ; F0 et F1 pour LOCK — relevés sur ops_286[] par
    /// gdb.
    ///
    /// F1 EST UN SECOND LOCK, pas un trou : la table y met le même handler
    /// qu'en F0. Quatrième alias rencontré, après 0x82 (A3d).</summary>
    private static void PoserGroupePrefixes()
    {
        ops_286[0x26] = PrefixeSegment(cpu_state.seg_es);
        ops_286[0x2E] = PrefixeSegment(cpu_state.seg_cs);
        ops_286[0x36] = PrefixeSegment(cpu_state.seg_ss);
        ops_286[0x3E] = PrefixeSegment(cpu_state.seg_ds);

        ops_286[0x0F] = op0F_w_a16;

        ops_286[0x64] = ILLEGAL;
        ops_286[0x65] = ILLEGAL;
        ops_286[0x66] = ILLEGAL;
        ops_286[0x67] = ILLEGAL;

        ops_286[0xF0] = opLOCK;
        ops_286[0xF1] = opLOCK;
    }
}
