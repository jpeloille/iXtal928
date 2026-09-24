// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/includes/private/cpu/x86_ops_arith.h  (en entier, lignes
//         3-914 : OP_ARITH et ses sept invocations, CMP, TEST, ARITH_MULTI)
// SHA256: voir oracle.tsv ; vérifier avec tools/check-oracle.sh
// STATUS: partial — A3d : le fichier C est couvert de bout en bout pour ce
//         qu'un 286 atteint. 42 handlers de OP_ARITH (ADD, OR, ADC, SBB, AND,
//         SUB, XOR), 6 de CMP, 4 de TEST, 3 du groupe immédiat ARITH_MULTI :
//         55 handlers DISTINCTS pour 56 EMPLACEMENTS, parce que 0x82 est un
//         alias de 0x80.
//         `partial` et non `transcribed`, et ça ne changera pas au 286 : les
//         formes `_a32` et `_l_*` de chaque famille restent dehors, et ce sont
//         elles que le registre des omissions ci-dessous couvre.
//         TEST n'est pas fini pour autant : TEST r/m,imm (F6 /0, F7 /0) vit
//         dans un AUTRE en-tête.
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
        // pcem: x86_ops_arith.h:4-30 — op##name##_b_rmw_a16
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
                        CLOCK_CYCLES(cpu_c.timing_rr);
                        PREFETCH_RUN(cpu_c.timing_rr, 2, (int)fetchdat, 0, 0, 0, 0, 0);
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
                        CLOCK_CYCLES(cpu_c.timing_mr);
                        PREFETCH_RUN(cpu_c.timing_mr, 2, (int)fetchdat, 1, 0, 1, 0, 0);
                }
                return 0;
        };

        // pcem: x86_ops_arith.h:59-85 — op##name##_w_rmw_a16
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
                        CLOCK_CYCLES(cpu_c.timing_rr);
                        PREFETCH_RUN(cpu_c.timing_rr, 2, (int)fetchdat, 0, 0, 0, 0, 0);
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
                        CLOCK_CYCLES(cpu_c.timing_mr);
                        // timing_rr, ET NON timing_mr — ce n'est pas une faute de
                        // frappe de ce port mais de PCem (x86_ops_arith.h:82) : la
                        // forme OCTET passe timing_mr aux deux appels, la forme MOT
                        // passe timing_mr à CLOCK_CYCLES et timing_rr à PREFETCH_RUN.
                        // Mesuré : recopier la forme octet rend prefetch_bytes = 2 là
                        // où l'oracle rend 0. On transcrit PCem, incohérences comprises.
                        PREFETCH_RUN(cpu_c.timing_rr, 2, (int)fetchdat, 1, 0, 1, 0, 0);
                }
                return 0;
        };

        // pcem: x86_ops_arith.h:169-185 — op##name##_b_rm_a16. Le résultat va dans le
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
                CLOCK_CYCLES(cpu_mod == 3 ? cpu_c.timing_rr : cpu_c.timing_rm);
                PREFETCH_RUN(cpu_mod == 3 ? cpu_c.timing_rr : cpu_c.timing_rm, 2, (int)fetchdat,
                             cpu_mod == 3 ? 0 : 1, 0, 0, 0, 0);
                return 0;
        };

        // pcem: x86_ops_arith.h:204-220 — op##name##_w_rm_a16
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
                CLOCK_CYCLES(cpu_mod == 3 ? cpu_c.timing_rr : cpu_c.timing_rm);
                PREFETCH_RUN(cpu_mod == 3 ? cpu_c.timing_rr : cpu_c.timing_rm, 2, (int)fetchdat,
                             cpu_mod == 3 ? 0 : 1, 0, 0, 0, 0);
                return 0;
        };

        // pcem: x86_ops_arith.h:274-285 — op##name##_AL_imm. Ici gettempc vient APRÈS
        // la lecture de dst et src, contrairement aux formes à ModRM.
        ops_286[slotALImm] = fetchdat =>
        {
                uint8_t dst = AL;
                uint8_t src = (uint8_t)fetchdat; cpu_state.pc++;
                if (gettempc)
                        tempc = CF_SET() != 0 ? 1 : 0;
                setflags8(dst, src);
                AL = op8(dst, src);
                CLOCK_CYCLES(cpu_c.timing_rr);
                PREFETCH_RUN(cpu_c.timing_rr, 2, -1, 0, 0, 0, 0, 0);
                return 0;
        };

        // pcem: x86_ops_arith.h:286-297 — op##name##_AX_imm
        ops_286[slotAXImm] = fetchdat =>
        {
                uint16_t dst = AX;
                uint16_t src = (uint16_t)fetchdat; cpu_state.pc += 2;
                if (gettempc)
                        tempc = CF_SET() != 0 ? 1 : 0;
                setflags16(dst, src);
                AX = op16(dst, src);
                CLOCK_CYCLES(cpu_c.timing_rr);
                PREFETCH_RUN(cpu_c.timing_rr, 3, -1, 0, 0, 0, 0, 0);
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

        PoserCmp();
    }

    // CMP N'EST PAS UNE HUITIÈME INVOCATION DE LA MACRO, et PCem ne l'a pas écrit
    // comme telle : x86_ops_arith.h:320-524 aligne quinze fonctions à la main. Le
    // reproduire en réglant OP_ARITH aurait l'air plus court et serait FAUX sur
    // trois points, chacun observable :
    //
    //   - CMP n'écrit pas son résultat : ni seteab/seteaw, ni le second test
    //     d'abandon qui les suit dans la macro. L'opérande mémoire est intact,
    //     et le journal d'écritures le voit.
    //     (Le SEG_CHECK_READ des formes _rmw, lui, est une différence de TEXTE
    //     et non de comportement : SEG_CHECK_WRITE teste la MÊME condition,
    //     `base == 0xffffffff`, et seule la chaîne passée à x86gpf change —
    //     elle va au pclog, pas à l'état. Ni l'un ni l'autre ne vérifie la
    //     limite d'offset ; c'est CHECK_WRITE, un autre macro, qui le fait, et
    //     ni CMP ni OP_ARITH ne l'appellent.)
    //   - les cycles des formes _rmw sont EN DUR, et dépendent du modèle :
    //     `is486 ? (mod==3 ? 1 : 2) : (mod==3 ? 2 : 5)`. Sur un 286 cela donne
    //     2 ou 5 — quand timing_rr et timing_rm, que PREFETCH_RUN reçoit au même
    //     endroit, valent 2 et 7. Les deux appels ne sont PAS d'accord, comme
    //     dans la forme mot de _rmw (voir OP_ARITH ci-dessus).
    //   - l'ordre des opérandes de setsub s'inverse entre _rmw et _rm : la
    //     première compare (mémoire, registre), la seconde (registre, mémoire).
    //
    // Il n'y a pas non plus de gettempc : CMP ne lit aucune retenue entrante.

    // pcem: x86_ops_arith.h:320-334 — opCMP_b_rmw_a16
    private static int opCMP_b_rmw_a16(uint32_t fetchdat)
    {
        uint8_t dst;
        if (fetch_ea_16(fetchdat)) return 1;
        if (cpu_mod != 3)
                if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        dst = geteab();
        if (cpu_state.abrt != 0)
                return 1;
        setsub8(dst, getr8(cpu_reg));
        if (is486 != 0)
                CLOCK_CYCLES((cpu_mod == 3) ? 1 : 2);
        else
                CLOCK_CYCLES((cpu_mod == 3) ? 2 : 5);
        PREFETCH_RUN((cpu_mod == 3) ? cpu_c.timing_rr : cpu_c.timing_rm, 2, (int)fetchdat,
                     (cpu_mod == 3) ? 0 : 1, 0, 0, 0, 0);
        return 0;
    }

    // pcem: x86_ops_arith.h:353-367 — opCMP_w_rmw_a16
    private static int opCMP_w_rmw_a16(uint32_t fetchdat)
    {
        uint16_t dst;
        if (fetch_ea_16(fetchdat)) return 1;
        if (cpu_mod != 3)
                if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        dst = geteaw();
        if (cpu_state.abrt != 0)
                return 1;
        setsub16(dst, cpu_state.regs[cpu_reg].w);
        if (is486 != 0)
                CLOCK_CYCLES((cpu_mod == 3) ? 1 : 2);
        else
                CLOCK_CYCLES((cpu_mod == 3) ? 2 : 5);
        PREFETCH_RUN((cpu_mod == 3) ? cpu_c.timing_rr : cpu_c.timing_rm, 2, (int)fetchdat,
                     (cpu_mod == 3) ? 0 : 1, 0, 0, 0, 0);
        return 0;
    }

    // pcem: x86_ops_arith.h:419-431 — opCMP_b_rm_a16. Ici les cycles suivent
    // timing_rr/timing_rm, comme PREFETCH_RUN : seules les formes _rmw portent
    // les constantes en dur.
    private static int opCMP_b_rm_a16(uint32_t fetchdat)
    {
        uint8_t src;
        if (fetch_ea_16(fetchdat)) return 1;
        if (cpu_mod != 3)
                if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        src = geteab();
        if (cpu_state.abrt != 0)
                return 1;
        setsub8(getr8(cpu_reg), src);
        CLOCK_CYCLES((cpu_mod == 3) ? cpu_c.timing_rr : cpu_c.timing_rm);
        PREFETCH_RUN((cpu_mod == 3) ? cpu_c.timing_rr : cpu_c.timing_rm, 2, (int)fetchdat,
                     (cpu_mod == 3) ? 0 : 1, 0, 0, 0, 0);
        return 0;
    }

    // pcem: x86_ops_arith.h:446-458 — opCMP_w_rm_a16
    private static int opCMP_w_rm_a16(uint32_t fetchdat)
    {
        uint16_t src;
        if (fetch_ea_16(fetchdat)) return 1;
        if (cpu_mod != 3)
                if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        src = geteaw();
        if (cpu_state.abrt != 0)
                return 1;
        setsub16(cpu_state.regs[cpu_reg].w, src);
        CLOCK_CYCLES((cpu_mod == 3) ? cpu_c.timing_rr : cpu_c.timing_rm);
        PREFETCH_RUN((cpu_mod == 3) ? cpu_c.timing_rr : cpu_c.timing_rm, 2, (int)fetchdat,
                     (cpu_mod == 3) ? 0 : 1, 0, 0, 0, 0);
        return 0;
    }

    // pcem: x86_ops_arith.h:500-507 — opCMP_AL_imm. `getbytef()` est une macro à
    // deux instructions (386_common.h:267) : la valeur, puis cpu_state.pc++. Le
    // C# la déplie, comme les formes AL_imm de OP_ARITH.
    private static int opCMP_AL_imm(uint32_t fetchdat)
    {
        uint8_t src = (uint8_t)fetchdat; cpu_state.pc++;
        setsub8(AL, src);
        CLOCK_CYCLES(cpu_c.timing_rr);
        PREFETCH_RUN(cpu_c.timing_rr, 2, -1, 0, 0, 0, 0, 0);
        return 0;
    }

    // pcem: x86_ops_arith.h:508-515 — opCMP_AX_imm
    private static int opCMP_AX_imm(uint32_t fetchdat)
    {
        uint16_t src = (uint16_t)fetchdat; cpu_state.pc += 2;
        setsub16(AX, src);
        CLOCK_CYCLES(cpu_c.timing_rr);
        PREFETCH_RUN(cpu_c.timing_rr, 3, -1, 0, 0, 0, 0, 0);
        return 0;
    }

    // omitted: les neuf autres formes de CMP — `_a32`, `_l_*` et `_EAX_imm` —
    //   pour la raison qui écarte leurs homologues de OP_ARITH : inatteignables
    //   tant que op32 est nul.

    /// <summary>Emplacements relevés sur ops_286[] de la .so par gdb : 38 à 3D,
    /// le dernier bloc de la bande ALU. L'ordre des six est celui des autres
    /// familles — b_rmw, w_rmw, b_rm, w_rm, AL_imm, AX_imm.</summary>
    private static void PoserCmp()
    {
        ops_286[0x38] = opCMP_b_rmw_a16;
        ops_286[0x39] = opCMP_w_rmw_a16;
        ops_286[0x3A] = opCMP_b_rm_a16;
        ops_286[0x3B] = opCMP_w_rm_a16;
        ops_286[0x3C] = opCMP_AL_imm;
        ops_286[0x3D] = opCMP_AX_imm;

        PoserTest();
        PoserGroupeImmediat();
    }

    // ---------------------------------------------------------------- TEST

    // TEST a la forme de CMP — il lit, il ne réécrit pas, et ses formes à EA
    // portent les mêmes cycles EN DUR — mais il pose setznp et non setsub. Les
    // quatre handlers sont écrits à la main comme PCem les écrit : pas de macro
    // ici non plus.

    // pcem: x86_ops_arith.h:526-542 — opTEST_b_a16
    private static int opTEST_b_a16(uint32_t fetchdat)
    {
        uint8_t temp, temp2;
        if (fetch_ea_16(fetchdat)) return 1;
        if (cpu_mod != 3)
                if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        temp = geteab();
        if (cpu_state.abrt != 0)
                return 1;
        temp2 = getr8(cpu_reg);
        setznp8((uint8_t)(temp & temp2));
        if (is486 != 0)
                CLOCK_CYCLES((cpu_mod == 3) ? 1 : 2);
        else
                CLOCK_CYCLES((cpu_mod == 3) ? 2 : 5);
        PREFETCH_RUN((cpu_mod == 3) ? cpu_c.timing_rr : cpu_c.timing_rm, 2, (int)fetchdat,
                     (cpu_mod == 3) ? 0 : 1, 0, 0, 0, 0);
        return 0;
    }

    // pcem: x86_ops_arith.h:561-577 — opTEST_w_a16
    private static int opTEST_w_a16(uint32_t fetchdat)
    {
        uint16_t temp, temp2;
        if (fetch_ea_16(fetchdat)) return 1;
        if (cpu_mod != 3)
                if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        temp = geteaw();
        if (cpu_state.abrt != 0)
                return 1;
        temp2 = cpu_state.regs[cpu_reg].w;
        setznp16((uint16_t)(temp & temp2));
        if (is486 != 0)
                CLOCK_CYCLES((cpu_mod == 3) ? 1 : 2);
        else
                CLOCK_CYCLES((cpu_mod == 3) ? 2 : 5);
        PREFETCH_RUN((cpu_mod == 3) ? cpu_c.timing_rr : cpu_c.timing_rm, 2, (int)fetchdat,
                     (cpu_mod == 3) ? 0 : 1, 0, 0, 0, 0);
        return 0;
    }

    // pcem: x86_ops_arith.h:631-637 — opTEST_AL
    private static int opTEST_AL(uint32_t fetchdat)
    {
        uint8_t temp = (uint8_t)fetchdat; cpu_state.pc++;
        setznp8((uint8_t)(AL & temp));
        CLOCK_CYCLES(cpu_c.timing_rr);
        PREFETCH_RUN(cpu_c.timing_rr, 2, -1, 0, 0, 0, 0, 0);
        return 0;
    }

    // pcem: x86_ops_arith.h:638-644 — opTEST_AX
    private static int opTEST_AX(uint32_t fetchdat)
    {
        uint16_t temp = (uint16_t)fetchdat; cpu_state.pc += 2;
        setznp16((uint16_t)(AX & temp));
        CLOCK_CYCLES(cpu_c.timing_rr);
        PREFETCH_RUN(cpu_c.timing_rr, 3, -1, 0, 0, 0, 0, 0);
        return 0;
    }

    // omitted: opTEST_b_a32, opTEST_w_a32, opTEST_l_a16/a32, opTEST_EAX — même
    //   raison que partout ailleurs dans ce fichier : op32 est nul sur un 286.
    //   ATTENTION : TEST n'est pas fini pour autant. Les formes TEST r/m, imm
    //   (F6 /0 et F7 /0) vivent dans un AUTRE en-tête et ne relèvent pas d'ici.

    /// <summary>pcem: 84, 85, A8, A9 — relevés sur ops_286[] par gdb.</summary>
    private static void PoserTest()
    {
        ops_286[0x84] = opTEST_b_a16;
        ops_286[0x85] = opTEST_w_a16;
        ops_286[0xA8] = opTEST_AL;
        ops_286[0xA9] = opTEST_AX;
    }

    // ------------------------------------------------- le groupe immédiat

    // ARITH_MULTI : LA SECONDE MACRO DU FICHIER (x86_ops_arith.h:655-720).
    //
    // Elle porte le CORPS COMMUN des opcodes 80/81/82/83 — l'aiguillage sur les
    // trois bits `reg` du ModRM, qui choisit laquelle des huit opérations ALU
    // s'applique à (ea, immédiat). Ses deux paramètres sont des LARGEURS, collées
    // par ## : `getea##ea_width`, `setadd##flag_width`.
    //
    // DEVIATION: le C# n'a pas de collage de jetons. Là où OP_ARITH se
    //   paramétrait par des délégués (les trois expressions variables sont des
    //   VALEURS), ici les sept opérations variables sont des TYPES, et le corps
    //   porte des `return 1` en son milieu. Des délégués ne gagneraient rien et
    //   forceraient à tout passer en uint32_t, donc à perdre le masquage que
    //   setadd8 et setadd16 ne font pas pareil. On écrit donc les DEUX
    //   instanciations de largeur atteignables sur un 286 — (b, 8) et (w, 16) —
    //   et pas une de plus. Substituer b<->w et 8<->16 dans l'une doit rendre
    //   l'autre, à l'identique.
    //
    // DEVIATION: le macro fait `return 1` depuis le milieu du corps ; une méthode
    //   C# ne peut pas rendre la main pour son appelant. Même idiome que
    //   fetch_ea_16 : la méthode rend `true` pour « l'appelant doit rendre 1 ».
    //
    // LE SOUS-OPCODE CMP (reg == 7) COÛTE 7 CYCLES EN MÉMOIRE, pas 5 comme les
    // opCMP_*_rmw de A3c. Ce n'est pas une coquille de ce port : les deux
    // constantes sont bien différentes dans PCem.

    // pcem: x86_ops_arith.h:656-720, ARITH_MULTI(b, 8)
    private static bool ARITH_MULTI_b8(uint32_t rmdat, uint8_t src)
    {
        uint8_t dst = geteab();
        if (cpu_state.abrt != 0)
                return true;
        switch (rmdat & 0x38)
        {
        case 0x00: /*ADD ea, #*/
                seteab((uint8_t)(dst + src));
                if (cpu_state.abrt != 0)
                        return true;
                setadd8(dst, src);
                CLOCK_CYCLES((cpu_mod == 3) ? cpu_c.timing_rr : cpu_c.timing_mr);
                break;
        case 0x08: /*OR ea, #*/
                dst |= src;
                seteab(dst);
                if (cpu_state.abrt != 0)
                        return true;
                setznp8(dst);
                CLOCK_CYCLES((cpu_mod == 3) ? cpu_c.timing_rr : cpu_c.timing_mr);
                break;
        case 0x10: /*ADC ea, #*/
                tempc = CF_SET() != 0 ? 1 : 0;
                seteab((uint8_t)(dst + src + tempc));
                if (cpu_state.abrt != 0)
                        return true;
                setadc8(dst, src);
                CLOCK_CYCLES((cpu_mod == 3) ? cpu_c.timing_rr : cpu_c.timing_mr);
                break;
        case 0x18: /*SBB ea, #*/
                tempc = CF_SET() != 0 ? 1 : 0;
                seteab((uint8_t)(dst - (src + tempc)));
                if (cpu_state.abrt != 0)
                        return true;
                setsbc8(dst, src);
                CLOCK_CYCLES((cpu_mod == 3) ? cpu_c.timing_rr : cpu_c.timing_mr);
                break;
        case 0x20: /*AND ea, #*/
                dst &= src;
                seteab(dst);
                if (cpu_state.abrt != 0)
                        return true;
                setznp8(dst);
                CLOCK_CYCLES((cpu_mod == 3) ? cpu_c.timing_rr : cpu_c.timing_mr);
                break;
        case 0x28: /*SUB ea, #*/
                seteab((uint8_t)(dst - src));
                if (cpu_state.abrt != 0)
                        return true;
                setsub8(dst, src);
                CLOCK_CYCLES((cpu_mod == 3) ? cpu_c.timing_rr : cpu_c.timing_mr);
                break;
        case 0x30: /*XOR ea, #*/
                dst ^= src;
                seteab(dst);
                if (cpu_state.abrt != 0)
                        return true;
                setznp8(dst);
                CLOCK_CYCLES((cpu_mod == 3) ? cpu_c.timing_rr : cpu_c.timing_mr);
                break;
        case 0x38: /*CMP ea, #*/
                setsub8(dst, src);
                if (is486 != 0)
                        CLOCK_CYCLES((cpu_mod == 3) ? 1 : 2);
                else
                        CLOCK_CYCLES((cpu_mod == 3) ? 2 : 7);
                break;
        }
        return false;
    }

    // pcem: x86_ops_arith.h:656-720, ARITH_MULTI(w, 16)
    private static bool ARITH_MULTI_w16(uint32_t rmdat, uint16_t src)
    {
        uint16_t dst = geteaw();
        if (cpu_state.abrt != 0)
                return true;
        switch (rmdat & 0x38)
        {
        case 0x00: /*ADD ea, #*/
                seteaw((uint16_t)(dst + src));
                if (cpu_state.abrt != 0)
                        return true;
                setadd16(dst, src);
                CLOCK_CYCLES((cpu_mod == 3) ? cpu_c.timing_rr : cpu_c.timing_mr);
                break;
        case 0x08: /*OR ea, #*/
                dst |= src;
                seteaw(dst);
                if (cpu_state.abrt != 0)
                        return true;
                setznp16(dst);
                CLOCK_CYCLES((cpu_mod == 3) ? cpu_c.timing_rr : cpu_c.timing_mr);
                break;
        case 0x10: /*ADC ea, #*/
                tempc = CF_SET() != 0 ? 1 : 0;
                seteaw((uint16_t)(dst + src + tempc));
                if (cpu_state.abrt != 0)
                        return true;
                setadc16(dst, src);
                CLOCK_CYCLES((cpu_mod == 3) ? cpu_c.timing_rr : cpu_c.timing_mr);
                break;
        case 0x18: /*SBB ea, #*/
                tempc = CF_SET() != 0 ? 1 : 0;
                seteaw((uint16_t)(dst - (src + tempc)));
                if (cpu_state.abrt != 0)
                        return true;
                setsbc16(dst, src);
                CLOCK_CYCLES((cpu_mod == 3) ? cpu_c.timing_rr : cpu_c.timing_mr);
                break;
        case 0x20: /*AND ea, #*/
                dst &= src;
                seteaw(dst);
                if (cpu_state.abrt != 0)
                        return true;
                setznp16(dst);
                CLOCK_CYCLES((cpu_mod == 3) ? cpu_c.timing_rr : cpu_c.timing_mr);
                break;
        case 0x28: /*SUB ea, #*/
                seteaw((uint16_t)(dst - src));
                if (cpu_state.abrt != 0)
                        return true;
                setsub16(dst, src);
                CLOCK_CYCLES((cpu_mod == 3) ? cpu_c.timing_rr : cpu_c.timing_mr);
                break;
        case 0x30: /*XOR ea, #*/
                dst ^= src;
                seteaw(dst);
                if (cpu_state.abrt != 0)
                        return true;
                setznp16(dst);
                CLOCK_CYCLES((cpu_mod == 3) ? cpu_c.timing_rr : cpu_c.timing_mr);
                break;
        case 0x38: /*CMP ea, #*/
                setsub16(dst, src);
                if (is486 != 0)
                        CLOCK_CYCLES((cpu_mod == 3) ? 1 : 2);
                else
                        CLOCK_CYCLES((cpu_mod == 3) ? 2 : 7);
                break;
        }
        return false;
    }

    // pcem: x86_ops_arith.h:723-740 — op80_a16
    private static int op80_a16(uint32_t fetchdat)
    {
        uint8_t src;

        if (fetch_ea_16(fetchdat)) return 1;
        if (cpu_mod != 3)
                if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
        src = getbyte();
        if (cpu_state.abrt != 0)
                return 1;
        if (ARITH_MULTI_b8(fetchdat, src)) return 1;
        if ((fetchdat & 0x38) == 0x38)
                PREFETCH_RUN((cpu_mod == 3) ? cpu_c.timing_rr : cpu_c.timing_mr, 3, (int)fetchdat,
                             (cpu_mod == 3) ? 0 : 1, 0, 0, 0, 0);
        else
                PREFETCH_RUN((cpu_mod == 3) ? cpu_c.timing_rr : cpu_c.timing_rm, 3, (int)fetchdat,
                             (cpu_mod == 3) ? 0 : 1, 0, (cpu_mod == 3) ? 0 : 1, 0, 0);

        return 0;
    }

    // pcem: x86_ops_arith.h:759-776 — op81_w_a16
    private static int op81_w_a16(uint32_t fetchdat)
    {
        uint16_t src;

        if (fetch_ea_16(fetchdat)) return 1;
        if (cpu_mod != 3)
                if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
        src = getword();
        if (cpu_state.abrt != 0)
                return 1;
        if (ARITH_MULTI_w16(fetchdat, src)) return 1;
        if ((fetchdat & 0x38) == 0x38)
                PREFETCH_RUN((cpu_mod == 3) ? cpu_c.timing_rr : cpu_c.timing_mr, 4, (int)fetchdat,
                             (cpu_mod == 3) ? 0 : 1, 0, 0, 0, 0);
        else
                PREFETCH_RUN((cpu_mod == 3) ? cpu_c.timing_rr : cpu_c.timing_rm, 4, (int)fetchdat,
                             (cpu_mod == 3) ? 0 : 1, 0, (cpu_mod == 3) ? 0 : 1, 0, 0);

        return 0;
    }

    // pcem: x86_ops_arith.h:832-851 — op83_w_a16. Même corps que 81, à ceci près
    // que l'immédiat est un OCTET étendu en signe : c'est toute la raison d'être
    // de l'opcode.
    private static int op83_w_a16(uint32_t fetchdat)
    {
        uint16_t src;

        if (fetch_ea_16(fetchdat)) return 1;
        if (cpu_mod != 3)
                if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
        src = getbyte();
        if (cpu_state.abrt != 0)
                return 1;
        if ((src & 0x80) != 0)
                src |= 0xff00;
        if (ARITH_MULTI_w16(fetchdat, src)) return 1;
        if ((fetchdat & 0x38) == 0x38)
                PREFETCH_RUN((cpu_mod == 3) ? cpu_c.timing_rr : cpu_c.timing_mr, 3, (int)fetchdat,
                             (cpu_mod == 3) ? 0 : 1, 0, 0, 0, 0);
        else
                PREFETCH_RUN((cpu_mod == 3) ? cpu_c.timing_rr : cpu_c.timing_rm, 3, (int)fetchdat,
                             (cpu_mod == 3) ? 0 : 1, 0, (cpu_mod == 3) ? 0 : 1, 0, 0);

        return 0;
    }

    // omitted: op80_a32, op81_l_a16/a32, op81_w_a32, op83_l_a16/a32, op83_w_a32 —
    //   op32 nul sur un 286, comme partout dans ce fichier.

    /// <summary>pcem: 80, 81, 82, 83 — relevés sur ops_286[] par gdb.
    ///
    /// 0x82 N'EST PAS UN TROU : la table y met op80_a16, le MÊME handler qu'en
    /// 0x80. C'est l'alias non documenté du 8086, et il survit jusqu'ici. Lu dans
    /// la .so, pas supposé — supposer « 80, 81, 83 » aurait laissé 0x82 tomber
    /// dans le handler d'échec bruyant.</summary>
    private static void PoserGroupeImmediat()
    {
        ops_286[0x80] = op80_a16;
        ops_286[0x81] = op81_w_a16;
        ops_286[0x82] = op80_a16;
        ops_286[0x83] = op83_w_a16;
    }
}
