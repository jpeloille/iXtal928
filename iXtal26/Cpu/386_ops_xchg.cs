// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/includes/private/cpu/x86_ops_xchg.h  (lignes 3-163)
// SHA256: voir oracle.tsv ; vérifier avec tools/check-oracle.sh
// STATUS: partial — A8 : les neuf emplacements qu'un 286 atteint. Les formes
//         `_l` et `_a32`, les sept XCHG_EAX_E** et la macro opBSWAP (:222-...)
//         restent dehors, déclarées au registre des omissions.
//
// L'ÉCHANGE, ET UN OPCODE QUI N'EST PAS LÀ.
//
// XCHG AX, AX EXISTE — c'est l'encodage 0x90 — et ce n'est pas un XCHG : la
// table y met opNOP, et opNOP n'est pas dans cet en-tête. L'échange d'un
// registre avec lui-même n'écrit rien et ne pose aucun drapeau, alors PCem lui
// donne un handler à part. 0x90 relève donc de A12 (`misc`), pas d'ici, et le
// vérifier valait mieux que de le supposer : c'est le seul trou de la bande
// 90-97.
//
// AUCUN DRAPEAU N'EST POSÉ PAR AUCUN DES NEUF. XCHG déplace des octets, point.
// C'est ce qui le rend ininteressant à lire et facile à écrire de travers :
// deux formes seulement, à EA et à accumulateur, et sept répétitions de la
// seconde.

using static iXtal26.Cpu._386_common;
using static iXtal26.Cpu.x86;

namespace iXtal26.Cpu;

internal static partial class _386
{
    // pcem: x86_ops_xchg.h:3-19 — opXCHG_b_a16.
    //
    // TROIS TEMPS, ET L'ORDRE EST LE SUJET : lire l'opérande mémoire, y écrire
    // le registre, puis seulement poser l'ancienne valeur dans le registre. Si
    // l'écriture abandonne, le registre n'a pas encore été touché — c'est le
    // rattrapage, et il est gratuit parce qu'il n'y a rien à défaire.
    private static int opXCHG_b_a16(uint32_t fetchdat)
    {
        uint8_t temp;

        if (fetch_ea_16(fetchdat)) return 1;
        if (cpu_mod != 3)
                if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
        temp = geteab();
        if (cpu_state.abrt != 0)
                return 1;
        seteab(getr8(cpu_reg));
        if (cpu_state.abrt != 0)
                return 1;
        setr8(cpu_reg, temp);
        CLOCK_CYCLES((cpu_mod == 3) ? 3 : 5);
        PREFETCH_RUN((cpu_mod == 3) ? 3 : 5, 2, (int)fetchdat,
                     (cpu_mod == 3) ? 0 : 1, 0, (cpu_mod == 3) ? 0 : 1, 0, 0);
        return 0;
    }

    // pcem: x86_ops_xchg.h:37-53 — opXCHG_w_a16
    private static int opXCHG_w_a16(uint32_t fetchdat)
    {
        uint16_t temp;

        if (fetch_ea_16(fetchdat)) return 1;
        if (cpu_mod != 3)
                if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
        temp = geteaw();
        if (cpu_state.abrt != 0)
                return 1;
        seteaw(cpu_state.regs[cpu_reg].w);
        if (cpu_state.abrt != 0)
                return 1;
        cpu_state.regs[cpu_reg].w = temp;
        CLOCK_CYCLES((cpu_mod == 3) ? 3 : 5);
        PREFETCH_RUN((cpu_mod == 3) ? 3 : 5, 2, (int)fetchdat,
                     (cpu_mod == 3) ? 0 : 1, 0, (cpu_mod == 3) ? 0 : 1, 0, 0);
        return 0;
    }

    /// <summary>pcem: x86_ops_xchg.h:108-163 — les sept opXCHG_AX_xx.
    ///
    /// PCem les écrit à la main, sept fois, sans macro — contrairement à
    /// INC_DEC_OP ou PUSH_W_OP juste à côté. Il n'y a rien à en tirer : les sept
    /// corps sont identiques au registre près. On les pose par une boucle sur
    /// l'indice x86, qui est la paramétrisation que le C n'a pas écrite.
    ///
    /// ATTENTION AU TROU : l'emplacement 0x90 serait XCHG AX, AX et la table y
    /// met opNOP. La boucle part donc de 1, pas de 0.</summary>
    private static void PoserXchgAccumulateur()
    {
        for (var n = 1; n < 8; n++)
        {
                var reg = n;

                ops_286[0x90 + reg] = fetchdat =>
                {
                        uint16_t temp = AX;
                        AX = cpu_state.regs[reg].w;
                        cpu_state.regs[reg].w = temp;
                        CLOCK_CYCLES(3);
                        PREFETCH_RUN(3, 1, -1, 0, 0, 0, 0, 0);
                        return 0;
                };
        }
    }

    // omitted: opXCHG_b_a32, opXCHG_w_a32, opXCHG_l_a16/a32, les sept
    //   opXCHG_EAX_E** et la macro opBSWAP — op32 nul sur un 286, et BSWAP est
    //   une instruction 486.
    // omitted: opNOP — l'emplacement 0x90 lui revient, mais il vit dans
    //   x86_ops_misc.h et arrivera avec A12.

    /// <summary>pcem: 86, 87, 91-97 — relevés sur ops_286[] par gdb.</summary>
    private static void PoserGroupeXchg()
    {
        ops_286[0x86] = opXCHG_b_a16;
        ops_286[0x87] = opXCHG_w_a16;
        PoserXchgAccumulateur();
    }

    // ---- G2, D2 : les formes 32 bits (_l, _a32) de x86_ops_xchg.h ----

    // pcem: x86_ops_xchg.h:205
    private static int opXCHG_EAX_EBP(uint32_t fetchdat)
    {
        uint32_t temp = EAX;
        EAX = EBP;
        EBP = temp;
        CLOCK_CYCLES(3);
        PREFETCH_RUN(3, 1, -1, 0, 0, 0, 0, 0);
        return 0;
    }

    // pcem: x86_ops_xchg.h:165
    private static int opXCHG_EAX_EBX(uint32_t fetchdat)
    {
        uint32_t temp = EAX;
        EAX = EBX;
        EBX = temp;
        CLOCK_CYCLES(3);
        PREFETCH_RUN(3, 1, -1, 0, 0, 0, 0, 0);
        return 0;
    }

    // pcem: x86_ops_xchg.h:173
    private static int opXCHG_EAX_ECX(uint32_t fetchdat)
    {
        uint32_t temp = EAX;
        EAX = ECX;
        ECX = temp;
        CLOCK_CYCLES(3);
        PREFETCH_RUN(3, 1, -1, 0, 0, 0, 0, 0);
        return 0;
    }

    // pcem: x86_ops_xchg.h:197
    private static int opXCHG_EAX_EDI(uint32_t fetchdat)
    {
        uint32_t temp = EAX;
        EAX = EDI;
        EDI = temp;
        CLOCK_CYCLES(3);
        PREFETCH_RUN(3, 1, -1, 0, 0, 0, 0, 0);
        return 0;
    }

    // pcem: x86_ops_xchg.h:181
    private static int opXCHG_EAX_EDX(uint32_t fetchdat)
    {
        uint32_t temp = EAX;
        EAX = EDX;
        EDX = temp;
        CLOCK_CYCLES(3);
        PREFETCH_RUN(3, 1, -1, 0, 0, 0, 0, 0);
        return 0;
    }

    // pcem: x86_ops_xchg.h:189
    private static int opXCHG_EAX_ESI(uint32_t fetchdat)
    {
        uint32_t temp = EAX;
        EAX = ESI;
        ESI = temp;
        CLOCK_CYCLES(3);
        PREFETCH_RUN(3, 1, -1, 0, 0, 0, 0, 0);
        return 0;
    }

    // pcem: x86_ops_xchg.h:213
    private static int opXCHG_EAX_ESP(uint32_t fetchdat)
    {
        uint32_t temp = EAX;
        EAX = ESP;
        ESP = temp;
        CLOCK_CYCLES(3);
        PREFETCH_RUN(3, 1, -1, 0, 0, 0, 0, 0);
        return 0;
    }

    // pcem: x86_ops_xchg.h:20
    private static int opXCHG_b_a32(uint32_t fetchdat)
    {
        uint8_t temp;

        if (fetch_ea_32(fetchdat)) return 1;
        if (cpu_mod != 3)
                if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
        temp = geteab();
        if (cpu_state.abrt != 0)
                return 1;
        seteab((uint8_t)(getr8(cpu_reg)));
        if (cpu_state.abrt != 0)
                return 1;
        setr8(cpu_reg, temp);
        CLOCK_CYCLES((cpu_mod == 3) ? 3 : 5);
        PREFETCH_RUN((cpu_mod == 3) ? 3 : 5, 2, (int)fetchdat, (cpu_mod == 3) ? 0 : 1, 0, (cpu_mod == 3) ? 0 : 1, 0, 1);
        return 0;
    }

    // pcem: x86_ops_xchg.h:73
    private static int opXCHG_l_a16(uint32_t fetchdat)
    {
        uint32_t temp;

        if (fetch_ea_16(fetchdat)) return 1;
        if (cpu_mod != 3)
                if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
        temp = geteal();
        if (cpu_state.abrt != 0)
                return 1;
        seteal(cpu_state.regs[cpu_reg].l);
        if (cpu_state.abrt != 0)
                return 1;
        cpu_state.regs[cpu_reg].l = temp;
        CLOCK_CYCLES((cpu_mod == 3) ? 3 : 5);
        PREFETCH_RUN((cpu_mod == 3) ? 3 : 5, 2, (int)fetchdat, 0, (cpu_mod == 3) ? 0 : 1, 0, (cpu_mod == 3) ? 0 : 1, 0);
        return 0;
    }

    // pcem: x86_ops_xchg.h:90
    private static int opXCHG_l_a32(uint32_t fetchdat)
    {
        uint32_t temp;

        if (fetch_ea_32(fetchdat)) return 1;
        if (cpu_mod != 3)
                if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
        temp = geteal();
        if (cpu_state.abrt != 0)
                return 1;
        seteal(cpu_state.regs[cpu_reg].l);
        if (cpu_state.abrt != 0)
                return 1;
        cpu_state.regs[cpu_reg].l = temp;
        CLOCK_CYCLES((cpu_mod == 3) ? 3 : 5);
        PREFETCH_RUN((cpu_mod == 3) ? 3 : 5, 2, (int)fetchdat, 0, (cpu_mod == 3) ? 0 : 1, 0, (cpu_mod == 3) ? 0 : 1, 1);
        return 0;
    }

    // pcem: x86_ops_xchg.h:55
    private static int opXCHG_w_a32(uint32_t fetchdat)
    {
        uint16_t temp;

        if (fetch_ea_32(fetchdat)) return 1;
        if (cpu_mod != 3)
                if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
        temp = geteaw();
        if (cpu_state.abrt != 0)
                return 1;
        seteaw((uint16_t)(cpu_state.regs[cpu_reg].w));
        if (cpu_state.abrt != 0)
                return 1;
        cpu_state.regs[cpu_reg].w = temp;
        CLOCK_CYCLES((cpu_mod == 3) ? 3 : 5);
        PREFETCH_RUN((cpu_mod == 3) ? 3 : 5, 2, (int)fetchdat, (cpu_mod == 3) ? 0 : 1, 0, (cpu_mod == 3) ? 0 : 1, 0, 1);
        return 0;
    }

    // pcem: 386_ops.h — les emplacements de ces handlers dans OP_TABLE(386) et (386_0f).
    private static void PoserGroupe_xchg_386()
    {
        ops_386[0x187] = opXCHG_l_a16;
        ops_386[0x191] = opXCHG_EAX_ECX;
        ops_386[0x192] = opXCHG_EAX_EDX;
        ops_386[0x193] = opXCHG_EAX_EBX;
        ops_386[0x194] = opXCHG_EAX_ESP;
        ops_386[0x195] = opXCHG_EAX_EBP;
        ops_386[0x196] = opXCHG_EAX_ESI;
        ops_386[0x197] = opXCHG_EAX_EDI;
        ops_386[0x286] = opXCHG_b_a32;
        ops_386[0x287] = opXCHG_w_a32;
        ops_386[0x386] = opXCHG_b_a32;
        ops_386[0x387] = opXCHG_l_a32;
        ops_386[0x391] = opXCHG_EAX_ECX;
        ops_386[0x392] = opXCHG_EAX_EDX;
        ops_386[0x393] = opXCHG_EAX_EBX;
        ops_386[0x394] = opXCHG_EAX_ESP;
        ops_386[0x395] = opXCHG_EAX_EBP;
        ops_386[0x396] = opXCHG_EAX_ESI;
        ops_386[0x397] = opXCHG_EAX_EDI;
    }
}
