// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/includes/private/cpu/x86_ops_arith.h  (OP_ARITH et ses sept
//         invocations, lignes 3-318)
// SHA256: voir oracle.tsv ; vérifier avec tools/check-oracle.sh
// STATUS: partial — A3b : la matrice OP_ARITH et les SEPT familles qu'elle
//         engendre (ADD, OR, ADC, SBB, AND, SUB, XOR), soit 42 handlers.
//         Le reste du fichier C attend son passage : CMP — qui n'écrit pas son
//         résultat et porte ses cycles en dur — puis TEST (84/85/A8/A9) et le
//         groupe immédiat ARITH_MULTI (80/81/83). Le compte final du fichier
//         n'est donc pas 42 mais 42 + 6 + 4 + 3.
//
// LA SOURCE EST UNE MACRO, ET C'EST ELLE QU'ON TRANSCRIT.
//
// Sa part principale n'est pas une liste de fonctions : c'est UNE macro,
// OP_ARITH(name, operation, setflags, flagops, gettempc), invoquée sept fois
// (:312-318). Le préprocesseur en tire quinze formes par famille — b/w/l ×
// rmw/rm × a16/a32, plus AL/AX/EAX-immédiat — soit 105. Sur un 286, où
// op32 et les adresses 32 bits n'existent pas, SIX par famille sont
// atteignables : ce sont ces 42-là qu'on pose.
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
//
// ET LE FUZZEUR NE PEUT PAS LE VÉRIFIER. En mode simple, chaque itération part
// d'un reset : flags_op vaut FLAGS_UNKNOWN, CF_SET tombe dans sa dernière
// branche et rend `flags & C_FLAG`. Les branches ADD/SUB/ADC/SBC/ZN de CF_SET
// — exactement celles dont parle le paragraphe ci-dessus — ne sont donc JAMAIS
// la source de tempc, quel que soit le nombre d'itérations. Il faut deux
// instructions enchaînées, et c'est ce que font les suites (f) de
// tools/iXtal26.Diff/Core286Check.cs.
//
// Même angle mort pour la matérialisation : le fuzzeur compare flags_op/res/
// op1/op2, donc il vérifie les POSEURS (setadd, setsub, setadc, setsbc, setznp)
// et pas les LECTEURS (VF_SET, AF_SET, NF_SET par espèce), qui ne tournent que
// dans flags_rebuild. Les cas (e) du même fichier les effondrent espèce par
// espèce, à l'instruction qui les pose.
//
// AF SUR ADC ET SBB : NE PAS « CORRIGER ». AF_SET des formes ADC/SBC de PCem
// s'écarte du silicium, et l'écart est consigné dans sst-baseline.tsv depuis M0
// (`adc dl, ch: flags = 0xF482, attendu 0xF492`, diff masqué 0x0010). Le
// fuzzeur compare à PCem : une transcription fidèle est verte ET fausse au
// regard du matériel. C'est voulu — l'oracle de ce dépôt est PCem.

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

    // omitted: les NEUF autres formes que la macro engendre par famille — les six
    //   variantes `_a32` (fetch_ea_32), et les trois formes longues `_l_rmw_a16`,
    //   `_l_rm_a16`, `_EAX_imm` (geteal/seteal, setflags32). Toutes inatteignables
    //   sur un 286 : l'index de table est `(opcode | cpu_state.op32) & 0x3ff` et
    //   op32 vaut use32, nul faute de bit D dans un descripteur 286 ; les quatre
    //   quadrants de ops_286 sont d'ailleurs des copies identiques. C'est CETTE
    //   omission, et elle seule, qui explique que le C# vif fasse 150 lignes contre
    //   286 au C : 6 formes posées sur 15 écrites.
    //   Elles reviendront telles quelles au jalon 386, sans rien changer ici.

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

    /// <summary>Les sept invocations de la macro. pcem: x86_ops_arith.h:312-318,
    /// dans cet ordre — c'est celui du fichier C, pas celui des opcodes.
    ///
    /// Les emplacements sont relevés sur ops_286[] de la .so par gdb, jamais lus
    /// dans le texte de 386_ops.h : deux tentatives de parsing avaient rendu des
    /// indices décalés de quatre (A2.2c). La bande ALU occupe 0x00-0x3F par blocs
    /// de huit, dont les six premiers octets sont la famille et les deux derniers
    /// autre chose (PUSH/POP segment, préfixe, ajustement BCD).</summary>
    private static void PoserGroupeArith()
    {
        // x86_ops_arith.h:312 — OP_ARITH(ADD, dst + src, setadd, (dst, src), 0)
        OP_ARITH(0x00, 0x01, 0x02, 0x03, 0x04, 0x05,
                 (dst, src) => (uint8_t)(dst + src),
                 (dst, src) => (uint16_t)(dst + src),
                 setadd8, setadd16, false);

        // x86_ops_arith.h:313 — OP_ARITH(ADC, dst + src + tempc, setadc, (dst, src), 1)
        OP_ARITH(0x10, 0x11, 0x12, 0x13, 0x14, 0x15,
                 (dst, src) => (uint8_t)(dst + src + tempc),
                 (dst, src) => (uint16_t)(dst + src + tempc),
                 setadc8, setadc16, true);

        // x86_ops_arith.h:314 — OP_ARITH(SUB, dst - src, setsub, (dst, src), 0)
        OP_ARITH(0x28, 0x29, 0x2A, 0x2B, 0x2C, 0x2D,
                 (dst, src) => (uint8_t)(dst - src),
                 (dst, src) => (uint16_t)(dst - src),
                 setsub8, setsub16, false);

        // x86_ops_arith.h:315 — OP_ARITH(SBB, dst - (src + tempc), setsbc, (dst, src), 1)
        OP_ARITH(0x18, 0x19, 0x1A, 0x1B, 0x1C, 0x1D,
                 (dst, src) => (uint8_t)(dst - (src + tempc)),
                 (dst, src) => (uint16_t)(dst - (src + tempc)),
                 setsbc8, setsbc16, true);

        // LES TROIS FAMILLES LOGIQUES ont `flagops` = (dst | src), un argument au
        // lieu de deux — c'est ce que le paramètre `flagops` de la macro permet et
        // que le C# ne peut pas mélanger. Le délégué reçoit toujours (dst, src) et
        // recompose ici l'argument unique de setznp. La recomposition doit être
        // CELLE DE LA FAMILLE et non l'opération : PCem passe `(dst | src)` à OR,
        // `(dst & src)` à AND, `(dst ^ src)` à XOR — le même texte que l'opération,
        // mais ce sont deux paramètres distincts de la macro, pas un seul.
        //
        // Le cast est obligatoire : C# promeut byte|byte en int.

        // x86_ops_arith.h:316 — OP_ARITH(OR, dst | src, setznp, (dst | src), 0)
        OP_ARITH(0x08, 0x09, 0x0A, 0x0B, 0x0C, 0x0D,
                 (dst, src) => (uint8_t)(dst | src),
                 (dst, src) => (uint16_t)(dst | src),
                 (dst, src) => setznp8((uint8_t)(dst | src)),
                 (dst, src) => setznp16((uint16_t)(dst | src)), false);

        // x86_ops_arith.h:317 — OP_ARITH(AND, dst &src, setznp, (dst & src), 0)
        OP_ARITH(0x20, 0x21, 0x22, 0x23, 0x24, 0x25,
                 (dst, src) => (uint8_t)(dst & src),
                 (dst, src) => (uint16_t)(dst & src),
                 (dst, src) => setznp8((uint8_t)(dst & src)),
                 (dst, src) => setznp16((uint16_t)(dst & src)), false);

        // x86_ops_arith.h:318 — OP_ARITH(XOR, dst ^ src, setznp, (dst ^ src), 0)
        OP_ARITH(0x30, 0x31, 0x32, 0x33, 0x34, 0x35,
                 (dst, src) => (uint8_t)(dst ^ src),
                 (dst, src) => (uint16_t)(dst ^ src),
                 (dst, src) => setznp8((uint8_t)(dst ^ src)),
                 (dst, src) => setznp16((uint16_t)(dst ^ src)), false);
    }
}
