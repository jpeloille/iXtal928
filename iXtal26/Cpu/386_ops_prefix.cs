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

using System.Runtime.CompilerServices;

using static iXtal26.Cpu._386_common;
using static iXtal26.Cpu.x86;
using static iXtal26.Cpu._386_materiel;

namespace iXtal26.Cpu;

internal static partial class _386
{
    // LE TRAMPOLINE DES PRÉFIXES (PB-50). Un préfixe rend ce que rend le handler qu'il
    // aiguille : `return x86_opcodes[…](fetchdat >> 8)`, un APPEL TERMINAL. GCC en fait un
    // saut (`jmp *%rax`, mesuré dans l'oracle) ; C# ne le garantit pas — RyuJIT le fait en
    // Release, pas en Debug. Or PCem ne borne pas la longueur d'une instruction : une RAM
    // remplie de préfixes est UNE instruction d'environ 1,7 million d'octets, et le C# Debug
    // y tombait par StackOverflow (`fuzz --core 386 --rounds 1 --instr 3 --op 64`).
    // DEVIATION: le préfixe ne s'appelle pas lui-même : il range le handler suivant et son
    //   fetchdat, et rend TAIL ; Dispatch, à chaque site qui aiguille un opcode depuis la
    //   boucle ou l'ombre de SS, appelle le handler rangé tant qu'on lui rend TAIL. Rien ne
    //   s'exécute entre le `return` du préfixe et l'appel : même handler, même fetchdat, même
    //   valeur rendue, même ordre — au bit près l'appel terminal de PCem, sans pile.
    private const int TAIL = int.MinValue;
    private static OpFn? tail_fn;
    private static uint32_t tail_fetchdat;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int TailCall(OpFn fn, uint32_t fetchdat)
    {
        tail_fn = fn;
        tail_fetchdat = fetchdat;
        return TAIL;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int Dispatch(OpFn fn, uint32_t fetchdat)
    {
        var r = fn(fetchdat);
        // pcem bug, fixed in hardware mode: PB-50 — aucun compte des octets d'instruction : la boucle suit une suite
        //   de préfixes sans fin, là où le 386 et le 486 lèvent #GP(0) au-delà de 15 octets, le 286 au-delà de 10. En
        //   mode matériel, le décodeur de longueur l'a refusée avant le premier préfixe (386.Materiel.cs).
        while (r == TAIL)
        {
                var f = tail_fn!;
                tail_fn = null;
                r = f(tail_fetchdat);
        }
        return r;
    }

    /// <summary>pcem: x86_ops_prefix.h:3-18 — op_seg(name, seg, …), forme
    /// `_w_a16`.
    ///
    /// DEVIATION: la macro prend DEUX tables — `opcode_table` et
    ///   `normal_opcode_table` — pour que les variantes REPE et REPNE puissent
    ///   aiguiller vers une table spécialisée et retomber sur la normale. Les
    ///   six invocations d'un 286 passent x86_opcodes aux DEUX, donc le test
    ///   `if (opcode_table[...])` est toujours vrai et la seconde branche morte.
    ///   On garde le test : il dit ce que la macro dit.</summary>
    /// <remarks>G2, D1 : `quadrant` porte les quatre formes de la macro — 0 pour
    /// `_w_a16`, 0x100 `_l_a16`, 0x200 `_w_a32`, 0x300 `_l_a32` — qui ne diffèrent que
    /// par le `| 0x…` de l'index.</remarks>
    private static OpFn PrefixeSegment(x86seg seg, uint32_t quadrant = 0) => fetchdat =>
    {
        fetchdat = fastreadl(x86.cs + cpu_state.pc);
        if (cpu_state.abrt != 0)
                return 1;
        cpu_state.pc++;

        cpu_state.ea_seg = seg;
        cpu_state.ssegs = 1;
        CLOCK_CYCLES(4);
        PREFETCH_PREFIX();

        if (x86_opcodes![(fetchdat & 0xff) | quadrant] != null)
                return TailCall(x86_opcodes[(fetchdat & 0xff) | quadrant], fetchdat >> 8);
        return TailCall(x86_opcodes[(fetchdat & 0xff) | quadrant], fetchdat >> 8);
    };

    // pcem: x86_ops_prefix.h:86-97 — G2, D1.
    private static int op_66(uint32_t fetchdat) /*Data size select*/
    {
        fetchdat = fastreadl(x86.cs + cpu_state.pc);
        if (cpu_state.abrt != 0)
                return 1;
        cpu_state.pc++;

        cpu_state.op32 = ((use32 & 0x100) ^ 0x100) | (cpu_state.op32 & 0x200);
        CLOCK_CYCLES(2);
        PREFETCH_PREFIX();
        return TailCall(x86_opcodes![(fetchdat & 0xff) | cpu_state.op32], fetchdat >> 8);
    }

    // pcem: x86_ops_prefix.h:98-109 — G2, D1.
    private static int op_67(uint32_t fetchdat) /*Address size select*/
    {
        fetchdat = fastreadl(x86.cs + cpu_state.pc);
        if (cpu_state.abrt != 0)
                return 1;
        cpu_state.pc++;

        cpu_state.op32 = ((use32 & 0x200) ^ 0x200) | (cpu_state.op32 & 0x100);
        CLOCK_CYCLES(2);
        PREFETCH_PREFIX();
        return TailCall(x86_opcodes![(fetchdat & 0xff) | cpu_state.op32], fetchdat >> 8);
    }

    /// <summary>G2, D1 — les préfixes du 386 dans ops_386 (386_ops.h, OP_TABLE(386)) :
    /// les six de segment, FS et GS compris, en leurs quatre formes, et 66/67 dans les
    /// quatre quadrants.</summary>
    private static void PoserPrefixes386()
    {
        for (uint32_t q = 0; q < 0x400; q += 0x100)
        {
                ops_386[0x26 | q] = PrefixeSegment(cpu_state.seg_es, q);
                ops_386[0x2E | q] = PrefixeSegment(cpu_state.seg_cs, q);
                ops_386[0x36 | q] = PrefixeSegment(cpu_state.seg_ss, q);
                ops_386[0x3E | q] = PrefixeSegment(cpu_state.seg_ds, q);
                ops_386[0x64 | q] = PrefixeSegment(cpu_state.seg_fs, q);
                ops_386[0x65 | q] = PrefixeSegment(cpu_state.seg_gs, q);
                ops_386[0x66 | q] = op_66;
                ops_386[0x67 | q] = op_67;
        }
    }

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

        // pcem bug, fixed in hardware mode: PB-182 — seul LOCK NOP est refusé : le 386 lève #UD devant toute
        //   instruction hors de sa liste, et devant une forme registre (386 PRM § 14.7, point 9). La table
        //   du 286 partage ce handler, dont le vrai comportement y est inconnu.
        if (ILLEGAL_ON((fetchdat & 0xff) == 0x90)) return 0;
        if (materiel.pb_182)
                if (lock_materiel())
                        return 0;

        CLOCK_CYCLES(4);
        PREFETCH_PREFIX();
        return TailCall(x86_opcodes![(fetchdat & 0xff) | cpu_state.op32], fetchdat >> 8);
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

    // pcem: 386_ops.h:159-179 — op0F_l_a16, op0F_w_a32, op0F_l_a32 (G2, D2) : la table
    // 0F, au quadrant de la forme.
    private static OpFn Op0F(int quadrant) => fetchdat =>
    {
        int opcode = (int)(fetchdat & 0xff);
        cpu_state.pc++;
        PREFETCH_PREFIX();

        return x86_opcodes_0f![opcode | quadrant](fetchdat >> 8);
    };

    private static void PoserOp0F386()
    {
        ops_386[0x10F] = Op0F(0x100);
        ops_386[0x20F] = Op0F(0x200);
        ops_386[0x30F] = Op0F(0x300);
    }
}
