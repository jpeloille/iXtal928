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
}
