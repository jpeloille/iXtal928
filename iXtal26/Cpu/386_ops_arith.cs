// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/includes/private/cpu/x86_ops_arith.h  (OP_ARITH, lignes 3-311)
// SHA256: voir oracle.tsv ; vérifier avec tools/check-oracle.sh
// STATUS: partial — A3a : la matrice OP_ARITH et la famille ADD. Les six autres
//         familles qu'elle engendre (ADC, SUB, SBB, OR, AND, XOR) arrivent en A3b,
//         CMP — qui n'écrit pas son résultat — dans son propre passage.
//
// LA SOURCE EST UNE MACRO, ET C'EST ELLE QU'ON TRANSCRIT.
//
// x86_ops_arith.h ne contient pas 48 fonctions : il contient UNE macro,
// OP_ARITH(name, operation, setflags, flagops, gettempc), invoquée sept fois
// (:312-318). Le préprocesseur en tire 42 fonctions, six formes par famille.
//
// La transcrire en 42 méthodes écrites à la main serait transcrire la SORTIE du
// préprocesseur, pas la source — et rendrait invisible ce que la macro dit :
// que les sept familles ne diffèrent que par trois expressions. On garde donc la
// paramétrisation, avec les mêmes quatre paramètres et les mêmes noms.
//
// DEVIATION: `operation` et `setflags` sont des expressions textuelles en C ; ici
//   des délégués. `flagops` disparaît comme paramètre : en C il vaut `(dst, src)`
//   pour l'arithmétique et `(dst | src)` pour la logique, deux arités que le C#
//   ne peut pas mélanger. Le délégué setflags reçoit toujours (dst, src) et la
//   famille logique y recompose son argument unique — même appel, même effet.
//
// LES DÉLÉGUÉS SONT CONSTRUITS UNE FOIS, au chargement de la table. Un OpFn est
// un champ de ops_286, pas une fermeture créée à chaque instruction.
//
// L'ORDRE DE `gettempc` N'EST PAS LE MÊME PARTOUT, et c'est porteur de sens :
// dans les formes à ModRM il est lu AVANT fetch_ea_16, dans les formes
// accumulateur-immédiat APRÈS la lecture de dst et src. CF_SET() consulte
// flags_op, que l'instruction précédente a posé — le déplacer changerait la
// retenue entrante d'un ADC.

using static iXtal26.Cpu._386_common;
using static iXtal26.Cpu.x86;
using static iXtal26.Cpu.x86_flags;

namespace iXtal26.Cpu;

internal static partial class _386
{
    // Les trois expressions que la macro fait varier, en délégués.
    internal delegate uint8_t ArithOp8(uint8_t dst, uint8_t src);
    internal delegate uint16_t ArithOp16(uint16_t dst, uint16_t src);
    internal delegate void ArithFlags8(uint8_t dst, uint8_t src);
    internal delegate void ArithFlags16(uint16_t dst, uint16_t src);

    /// <summary>Les six formes d'une famille, telles que OP_ARITH les engendre.
    /// L'ordre est celui de la macro.</summary>
    private static void OP_ARITH(int slotBRmw, int slotWRmw, int slotBRm, int slotWRm,
                                 int slotALImm, int slotAXImm,
                                 ArithOp8 op8, ArithOp16 op16,
                                 ArithFlags8 setflags8, ArithFlags16 setflags16,
                                 bool gettempc)
    {
        // pcem: x86_ops_arith.h:4 — op##name##_b_rmw_a16
        ops_286[slotBRmw] = fetchdat =>
        {
                if (gettempc)
                        tempc = CF_SET() != 0 ? 1 : 0;
                if (fetch_ea_16(fetchdat)) return 1;
                if (cpu_mod == 3)
                {
                        uint8_t dst = getr8(cpu_rm);
                        uint8_t src = getr8(cpu_reg);
                        setflags8(dst, src);
                        setr8(cpu_rm, op8(dst, src));
                        CLOCK_CYCLES(cpu.timing_rr);
                        PREFETCH_RUN(cpu.timing_rr, 2, (int)fetchdat, 0, 0, 0, 0, 0);
                }
                else
                {
                        uint8_t dst;
                        if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
                        dst = geteab();
                        if (cpu_state.abrt != 0)
                                return 1;
                        uint8_t src = getr8(cpu_reg);
                        seteab(op8(dst, src));
                        if (cpu_state.abrt != 0)
                                return 1;
                        setflags8(dst, src);
                        CLOCK_CYCLES(cpu.timing_mr);
                        PREFETCH_RUN(cpu.timing_mr, 2, (int)fetchdat, 1, 0, 1, 0, 0);
                }
                return 0;
        };

        // pcem: x86_ops_arith.h:61 — op##name##_w_rmw_a16
        ops_286[slotWRmw] = fetchdat =>
        {
                if (gettempc)
                        tempc = CF_SET() != 0 ? 1 : 0;
                if (fetch_ea_16(fetchdat)) return 1;
                if (cpu_mod == 3)
                {
                        uint16_t dst = cpu_state.regs[cpu_rm].w;
                        uint16_t src = cpu_state.regs[cpu_reg].w;
                        setflags16(dst, src);
                        cpu_state.regs[cpu_rm].w = op16(dst, src);
                        CLOCK_CYCLES(cpu.timing_rr);
                        PREFETCH_RUN(cpu.timing_rr, 2, (int)fetchdat, 0, 0, 0, 0, 0);
                }
                else
                {
                        uint16_t dst;
                        if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
                        dst = geteaw();
                        if (cpu_state.abrt != 0)
                                return 1;
                        uint16_t src = cpu_state.regs[cpu_reg].w;
                        seteaw(op16(dst, src));
                        if (cpu_state.abrt != 0)
                                return 1;
                        setflags16(dst, src);
                        CLOCK_CYCLES(cpu.timing_mr);
                        // timing_rr, ET NON timing_mr — ce n'est pas une faute de
                        // frappe de ce port mais de PCem (x86_ops_arith.h:82) : la
                        // forme OCTET passe timing_mr aux deux appels, la forme MOT
                        // passe timing_mr à CLOCK_CYCLES et timing_rr à PREFETCH_RUN.
                        // Mesuré : recopier la forme octet rend prefetch_bytes = 2 là
                        // où l'oracle rend 0. On transcrit PCem, incohérences comprises.
                        PREFETCH_RUN(cpu.timing_rr, 2, (int)fetchdat, 1, 0, 1, 0, 0);
                }
                return 0;
        };

        // pcem: x86_ops_arith.h:176 — op##name##_b_rm_a16. Le résultat va dans le
        // REGISTRE, donc pas de SEG_CHECK_WRITE ni de seteab.
        ops_286[slotBRm] = fetchdat =>
        {
                uint8_t dst, src;
                if (gettempc)
                        tempc = CF_SET() != 0 ? 1 : 0;
                if (fetch_ea_16(fetchdat)) return 1;
                if (cpu_mod != 3)
                        if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
                dst = getr8(cpu_reg);
                src = geteab();
                if (cpu_state.abrt != 0)
                        return 1;
                setflags8(dst, src);
                setr8(cpu_reg, op8(dst, src));
                CLOCK_CYCLES(cpu_mod == 3 ? cpu.timing_rr : cpu.timing_rm);
                PREFETCH_RUN(cpu_mod == 3 ? cpu.timing_rr : cpu.timing_rm, 2, (int)fetchdat,
                             cpu_mod == 3 ? 0 : 1, 0, 0, 0, 0);
                return 0;
        };

        // pcem: x86_ops_arith.h:232 — op##name##_w_rm_a16
        ops_286[slotWRm] = fetchdat =>
        {
                uint16_t dst, src;
                if (gettempc)
                        tempc = CF_SET() != 0 ? 1 : 0;
                if (fetch_ea_16(fetchdat)) return 1;
                if (cpu_mod != 3)
                        if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
                dst = cpu_state.regs[cpu_reg].w;
                src = geteaw();
                if (cpu_state.abrt != 0)
                        return 1;
                setflags16(dst, src);
                cpu_state.regs[cpu_reg].w = op16(dst, src);
                CLOCK_CYCLES(cpu_mod == 3 ? cpu.timing_rr : cpu.timing_rm);
                PREFETCH_RUN(cpu_mod == 3 ? cpu.timing_rr : cpu.timing_rm, 2, (int)fetchdat,
                             cpu_mod == 3 ? 0 : 1, 0, 0, 0, 0);
                return 0;
        };

        // pcem: x86_ops_arith.h:288 — op##name##_AL_imm. Ici gettempc vient APRÈS
        // la lecture de dst et src, contrairement aux formes à ModRM.
        ops_286[slotALImm] = fetchdat =>
        {
                uint8_t dst = AL;
                uint8_t src = (uint8_t)fetchdat; cpu_state.pc++;
                if (gettempc)
                        tempc = CF_SET() != 0 ? 1 : 0;
                setflags8(dst, src);
                AL = op8(dst, src);
                CLOCK_CYCLES(cpu.timing_rr);
                PREFETCH_RUN(cpu.timing_rr, 2, -1, 0, 0, 0, 0, 0);
                return 0;
        };

        // pcem: x86_ops_arith.h:300 — op##name##_AX_imm
        ops_286[slotAXImm] = fetchdat =>
        {
                uint16_t dst = AX;
                uint16_t src = (uint16_t)fetchdat; cpu_state.pc += 2;
                if (gettempc)
                        tempc = CF_SET() != 0 ? 1 : 0;
                setflags16(dst, src);
                AX = op16(dst, src);
                CLOCK_CYCLES(cpu.timing_rr);
                PREFETCH_RUN(cpu.timing_rr, 3, -1, 0, 0, 0, 0, 0);
                return 0;
        };
    }

    /// <summary>A3a — la première famille. pcem: x86_ops_arith.h:312
    /// `OP_ARITH(ADD, dst + src, setadd, (dst, src), 0)`.
    ///
    /// Emplacements relevés sur ops_286[] de la .so par gdb : 00, 01, 02, 03, 04,
    /// 05 — la bande ALU commence à zéro, et ADD l'ouvre.</summary>
    private static void PoserGroupeArith()
    {
        OP_ARITH(0x00, 0x01, 0x02, 0x03, 0x04, 0x05,
                 (dst, src) => (uint8_t)(dst + src),
                 (dst, src) => (uint16_t)(dst + src),
                 setadd8, setadd16, false);
    }
}
