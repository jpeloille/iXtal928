// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/includes/private/cpu/x86_ops_mul.h  (opIMUL_w_iw_a16 et
//         opIMUL_w_ib_a16)
// SHA256: voir oracle.tsv ; vérifier avec tools/check-oracle.sh
// STATUS: partial — A11 : les deux emplacements que la TABLE donne à cet
//         en-tête, 69 et 6B. Les MUL, IMUL, DIV et IDIV à un opérande ne sont
//         PAS ici : ils sont dans les groupes F6 et F7 de x86_ops_misc.h, et
//         arrivent avec eux.
//
// LA MULTIPLICATION À TROIS OPÉRANDES, une nouveauté du 286.
//
// `IMUL reg, r/m, imm` — le seul endroit du jeu où une instruction lit deux
// sources et écrit une destination distincte. Deux formes : immédiat mot (69)
// et immédiat octet ÉTENDU EN SIGNE (6B), et c'est l'extension qui fait tout
// l'intérêt de la seconde.
//
// LE RÉSULTAT EST TRONQUÉ, LES DRAPEAUX DISENT SI ÇA COMPTE. Le produit tient
// sur 32 bits, seuls 16 sont écrits — et C et V sont posés quand la partie
// jetée n'était pas une simple extension de signe. Le test `(templ >> 15) != 0
// && (templ >> 15) != -1` dit exactement cela : les 17 bits hauts doivent être
// tous à zéro ou tous à un.
//
// ET SEULS C ET V SONT TOUCHÉS. Z, N, P, A gardent leur valeur d'avant — d'où
// le flags_rebuild() qui matérialise l'état paresseux avant modification, comme
// dans le groupe BCD.

using static iXtal26.Cpu._386_common;
using static iXtal26.Cpu.x86;
using static iXtal26.Cpu.x86_flags;

namespace iXtal26.Cpu;

internal static partial class _386
{
    // pcem: x86_ops_mul.h — opIMUL_w_iw_a16
    private static int opIMUL_w_iw_a16(uint32_t fetchdat)
    {
        int32_t templ;
        int16_t tempw, tempw2;

        if (fetch_ea_16(fetchdat)) return 1;
        if (cpu_mod != 3)
                if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;

        tempw = (int16_t)geteaw();
        if (cpu_state.abrt != 0)
                return 1;
        tempw2 = (int16_t)getword();
        if (cpu_state.abrt != 0)
                return 1;

        templ = ((int)tempw) * ((int)tempw2);
        flags_rebuild();
        if ((templ >> 15) != 0 && (templ >> 15) != -1)
                cpu_state.flags |= C_FLAG | V_FLAG;
        else
                cpu_state.flags &= unchecked((uint16_t)~(C_FLAG | V_FLAG));
        cpu_state.regs[cpu_reg].w = (uint16_t)(templ & 0xffff);

        CLOCK_CYCLES((cpu_mod == 3) ? 14 : 17);
        PREFETCH_RUN((cpu_mod == 3) ? 14 : 17, 4, (int)fetchdat, 1, 0, 0, 0, 0);
        return 0;
    }

    // pcem: x86_ops_mul.h — opIMUL_w_ib_a16. L'immédiat est un OCTET étendu en
    // signe, et l'extension se fait À LA MAIN sur un int16_t : `if (tempw2 &
    // 0x80) tempw2 |= 0xff00;`.
    private static int opIMUL_w_ib_a16(uint32_t fetchdat)
    {
        int32_t templ;
        int16_t tempw, tempw2;

        if (fetch_ea_16(fetchdat)) return 1;
        if (cpu_mod != 3)
                if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;

        tempw = (int16_t)geteaw();
        if (cpu_state.abrt != 0)
                return 1;
        tempw2 = getbyte();
        if (cpu_state.abrt != 0)
                return 1;
        if ((tempw2 & 0x80) != 0)
                tempw2 |= unchecked((int16_t)0xff00);

        templ = ((int)tempw) * ((int)tempw2);
        flags_rebuild();
        if ((templ >> 15) != 0 && (templ >> 15) != -1)
                cpu_state.flags |= C_FLAG | V_FLAG;
        else
                cpu_state.flags &= unchecked((uint16_t)~(C_FLAG | V_FLAG));
        cpu_state.regs[cpu_reg].w = (uint16_t)(templ & 0xffff);

        CLOCK_CYCLES((cpu_mod == 3) ? 14 : 17);
        PREFETCH_RUN((cpu_mod == 3) ? 14 : 17, 3, (int)fetchdat, 1, 0, 0, 0, 0);
        return 0;
    }

    // omitted: les formes `_l` et `_a32` des deux, et opIMUL_l_iw/ib — op32 nul
    //   sur un 286.
    // omitted: opMUL_*, opIMUL_* a un operande, opDIV_*, opIDIV_* — ils sont
    //   dans les groupes F6 et F7 de x86_ops_misc.h, pas ici.

    /// <summary>pcem: 69 et 6B — relevés sur ops_286[] par gdb.</summary>
    private static void PoserGroupeMul()
    {
        ops_286[0x69] = opIMUL_w_iw_a16;
        ops_286[0x6B] = opIMUL_w_ib_a16;
    }
}
