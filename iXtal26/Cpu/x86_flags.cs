// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/includes/private/cpu/x86_flags.h
// SHA256: voir oracle.tsv ; vérifier avec tools/check-oracle.sh
// STATUS: complete — l'en-tête entier, y compris les formes 32 bits que le 286
//         n'atteint pas. Les découper aurait demandé de juger case par case ce
//         qui est atteignable ; les reprendre en bloc ne coûte que des étiquettes.
//
// LES DRAPEAUX PARESSEUX, ET POURQUOI ILS SONT PARESSEUX.
//
// Le 8088 calcule ses six drapeaux arithmétiques à chaque instruction. exec386,
// lui, retient l'OPÉRATION (flags_op) et jusqu'à trois valeurs — flags_op1,
// flags_op2, flags_res — et ne calcule les bits qu'au moment où quelqu'un les
// LIT. La plupart des instructions ne relisent jamais les drapeaux qu'elles
// posent : le calcul n'a alors jamais lieu.
//
// D'où six fonctions d'évaluation, une par bit, chacune un aiguillage sur
// flags_op. Et d'où flags_rebuild(), qui matérialise les six d'un coup et remet
// flags_op à FLAGS_UNKNOWN — après quoi `cpu_state.flags` redevient la vérité.
//
// NE PAS « CORRIGER » AF_SET. Les formes ADC et SBB de PCem s'écartent du
// silicium sur le bit AF (0x0010), et c'est consigné dans sst-baseline.tsv depuis
// M0 : « adc dl, ch: flags = 0xF482, attendu 0xF492 (diff masqué 0x0010) », ~96 %
// de réussite sur ces formes. Le fuzzeur compare à PCem, donc une transcription
// fidèle sera VERTE et restera fausse au regard du matériel. C'est le choix du
// projet : on transcrit PCem, y compris ses écarts, et on les consigne.

using static iXtal26.Cpu._386_common;
using static iXtal26.Cpu.x86;

namespace iXtal26.Cpu;

internal static class x86_flags
{
    // pcem: x86_flags.h:5-54. FLAGS_UNKNOWN vaut 0 : c'est ce qui rend
    // flags_rebuild() inoffensif tant qu'aucune opération n'a été posée.
    internal const int FLAGS_UNKNOWN = 0;
    internal const int FLAGS_ZN8 = 1, FLAGS_ZN16 = 2, FLAGS_ZN32 = 3;
    internal const int FLAGS_ADD8 = 4, FLAGS_ADD16 = 5, FLAGS_ADD32 = 6;
    internal const int FLAGS_SUB8 = 7, FLAGS_SUB16 = 8, FLAGS_SUB32 = 9;
    internal const int FLAGS_SHL8 = 10, FLAGS_SHL16 = 11, FLAGS_SHL32 = 12;
    internal const int FLAGS_SHR8 = 13, FLAGS_SHR16 = 14, FLAGS_SHR32 = 15;
    internal const int FLAGS_SAR8 = 16, FLAGS_SAR16 = 17, FLAGS_SAR32 = 18;
    internal const int FLAGS_ROL8 = 19, FLAGS_ROL16 = 20, FLAGS_ROL32 = 21;
    internal const int FLAGS_ROR8 = 22, FLAGS_ROR16 = 23, FLAGS_ROR32 = 24;
    internal const int FLAGS_INC8 = 25, FLAGS_INC16 = 26, FLAGS_INC32 = 27;
    internal const int FLAGS_DEC8 = 28, FLAGS_DEC16 = 29, FLAGS_DEC32 = 30;
    internal const int FLAGS_ADC8 = 31, FLAGS_ADC16 = 32, FLAGS_ADC32 = 33;
    internal const int FLAGS_SBC8 = 34, FLAGS_SBC16 = 35, FLAGS_SBC32 = 36;

    // pcem: x86_flags.h:3 — la retenue entrante d'un ADC/SBB, portée hors de
    // cpu_state parce que le handler la calcule avant de poser l'opération.
    internal static int tempc;

    // DEVIATION: le C rend un `int` de ses expressions booléennes — `!x`, `a && b`,
    //   `a || b` valent 0 ou 1, et `x & MASQUE` vaut le masque. Le C# n'a pas cette
    //   conversion implicite. Chaque `return` garde donc la forme du C, avec une
    //   conversion explicite là où elle est nécessaire. Ce qui compte pour les
    //   appelants — flags_rebuild teste `if (X_SET())` — c'est nul ou non nul, et
    //   cette propriété est préservée expression par expression.

    // pcem: x86_flags.h:57
    internal static int ZF_SET()
    {
        switch (cpu_state.flags_op)
        {
        case FLAGS_ZN8: case FLAGS_ZN16: case FLAGS_ZN32:
        case FLAGS_ADD8: case FLAGS_ADD16: case FLAGS_ADD32:
        case FLAGS_SUB8: case FLAGS_SUB16: case FLAGS_SUB32:
        case FLAGS_SHL8: case FLAGS_SHL16: case FLAGS_SHL32:
        case FLAGS_SHR8: case FLAGS_SHR16: case FLAGS_SHR32:
        case FLAGS_SAR8: case FLAGS_SAR16: case FLAGS_SAR32:
        case FLAGS_INC8: case FLAGS_INC16: case FLAGS_INC32:
        case FLAGS_DEC8: case FLAGS_DEC16: case FLAGS_DEC32:
        case FLAGS_ADC8: case FLAGS_ADC16: case FLAGS_ADC32:
        case FLAGS_SBC8: case FLAGS_SBC16: case FLAGS_SBC32:
                return cpu_state.flags_res == 0 ? 1 : 0;

        case FLAGS_ROL8: case FLAGS_ROL16: case FLAGS_ROL32:
        case FLAGS_ROR8: case FLAGS_ROR16: case FLAGS_ROR32:
        case FLAGS_UNKNOWN:
                return cpu_state.flags & Z_FLAG;
        }
        return 0;
    }

    // pcem: x86_flags.h:103
    internal static int NF_SET()
    {
        switch (cpu_state.flags_op)
        {
        case FLAGS_ZN8: case FLAGS_ADD8: case FLAGS_SUB8:
        case FLAGS_SHL8: case FLAGS_SHR8: case FLAGS_SAR8:
        case FLAGS_INC8: case FLAGS_DEC8: case FLAGS_ADC8: case FLAGS_SBC8:
                return (int)(cpu_state.flags_res & 0x80);

        case FLAGS_ZN16: case FLAGS_ADD16: case FLAGS_SUB16:
        case FLAGS_SHL16: case FLAGS_SHR16: case FLAGS_SAR16:
        case FLAGS_INC16: case FLAGS_DEC16: case FLAGS_ADC16: case FLAGS_SBC16:
                return (int)(cpu_state.flags_res & 0x8000);

        case FLAGS_ZN32: case FLAGS_ADD32: case FLAGS_SUB32:
        case FLAGS_SHL32: case FLAGS_SHR32: case FLAGS_SAR32:
        case FLAGS_INC32: case FLAGS_DEC32: case FLAGS_ADC32: case FLAGS_SBC32:
                return (cpu_state.flags_res & 0x80000000) != 0 ? 1 : 0;

        case FLAGS_ROL8: case FLAGS_ROL16: case FLAGS_ROL32:
        case FLAGS_ROR8: case FLAGS_ROR16: case FLAGS_ROR32:
        case FLAGS_UNKNOWN:
                return cpu_state.flags & N_FLAG;
        }
        return 0;
    }

    // pcem: x86_flags.h:153. znptable8 porte la parité pré-calculée ; elle vit
    // dans 808x.cs, où 808x.c la définit (makeznptable).
    internal static int PF_SET()
    {
        switch (cpu_state.flags_op)
        {
        case FLAGS_ZN8: case FLAGS_ZN16: case FLAGS_ZN32:
        case FLAGS_ADD8: case FLAGS_ADD16: case FLAGS_ADD32:
        case FLAGS_SUB8: case FLAGS_SUB16: case FLAGS_SUB32:
        case FLAGS_SHL8: case FLAGS_SHL16: case FLAGS_SHL32:
        case FLAGS_SHR8: case FLAGS_SHR16: case FLAGS_SHR32:
        case FLAGS_SAR8: case FLAGS_SAR16: case FLAGS_SAR32:
        case FLAGS_INC8: case FLAGS_INC16: case FLAGS_INC32:
        case FLAGS_DEC8: case FLAGS_DEC16: case FLAGS_DEC32:
        case FLAGS_ADC8: case FLAGS_ADC16: case FLAGS_ADC32:
        case FLAGS_SBC8: case FLAGS_SBC16: case FLAGS_SBC32:
                return _808x.znptable8[cpu_state.flags_res & 0xff] & P_FLAG;

        case FLAGS_ROL8: case FLAGS_ROL16: case FLAGS_ROL32:
        case FLAGS_ROR8: case FLAGS_ROR16: case FLAGS_ROR32:
        case FLAGS_UNKNOWN:
                return cpu_state.flags & P_FLAG;
        }
        return 0;
    }

    // pcem: x86_flags.h:199
    internal static int VF_SET()
    {
        switch (cpu_state.flags_op)
        {
        case FLAGS_ZN8: case FLAGS_ZN16: case FLAGS_ZN32:
        case FLAGS_SAR8: case FLAGS_SAR16: case FLAGS_SAR32:
                return 0;

        case FLAGS_ADC8: case FLAGS_ADD8: case FLAGS_INC8:
                return ((cpu_state.flags_op1 ^ cpu_state.flags_op2) & 0x80) == 0 &&
                       ((cpu_state.flags_op1 ^ cpu_state.flags_res) & 0x80) != 0 ? 1 : 0;
        case FLAGS_ADC16: case FLAGS_ADD16: case FLAGS_INC16:
                return ((cpu_state.flags_op1 ^ cpu_state.flags_op2) & 0x8000) == 0 &&
                       ((cpu_state.flags_op1 ^ cpu_state.flags_res) & 0x8000) != 0 ? 1 : 0;
        case FLAGS_ADC32: case FLAGS_ADD32: case FLAGS_INC32:
                return ((cpu_state.flags_op1 ^ cpu_state.flags_op2) & 0x80000000) == 0 &&
                       ((cpu_state.flags_op1 ^ cpu_state.flags_res) & 0x80000000) != 0 ? 1 : 0;

        case FLAGS_SBC8: case FLAGS_SUB8: case FLAGS_DEC8:
                return (int)((cpu_state.flags_op1 ^ cpu_state.flags_op2) & (cpu_state.flags_op1 ^ cpu_state.flags_res) & 0x80);
        case FLAGS_SBC16: case FLAGS_SUB16: case FLAGS_DEC16:
                return (int)((cpu_state.flags_op1 ^ cpu_state.flags_op2) & (cpu_state.flags_op1 ^ cpu_state.flags_res) & 0x8000);
        case FLAGS_SBC32: case FLAGS_SUB32: case FLAGS_DEC32:
                return ((cpu_state.flags_op1 ^ cpu_state.flags_op2) & (cpu_state.flags_op1 ^ cpu_state.flags_res) & 0x80000000) != 0 ? 1 : 0;

        case FLAGS_SHL8:
                return (int)(((cpu_state.flags_op1 << (int)cpu_state.flags_op2) ^ (cpu_state.flags_op1 << (int)(cpu_state.flags_op2 - 1))) & 0x80);
        case FLAGS_SHL16:
                return (int)(((cpu_state.flags_op1 << (int)cpu_state.flags_op2) ^ (cpu_state.flags_op1 << (int)(cpu_state.flags_op2 - 1))) & 0x8000);
        case FLAGS_SHL32:
                return (((cpu_state.flags_op1 << (int)cpu_state.flags_op2) ^ (cpu_state.flags_op1 << (int)(cpu_state.flags_op2 - 1))) & 0x80000000) != 0 ? 1 : 0;

        case FLAGS_SHR8:
                return cpu_state.flags_op2 == 1 && (cpu_state.flags_op1 & 0x80) != 0 ? 1 : 0;
        case FLAGS_SHR16:
                return cpu_state.flags_op2 == 1 && (cpu_state.flags_op1 & 0x8000) != 0 ? 1 : 0;
        case FLAGS_SHR32:
                return cpu_state.flags_op2 == 1 && (cpu_state.flags_op1 & 0x80000000) != 0 ? 1 : 0;

        case FLAGS_ROL8:
                return (int)((cpu_state.flags_res ^ (cpu_state.flags_res >> 7)) & 1);
        case FLAGS_ROL16:
                return (int)((cpu_state.flags_res ^ (cpu_state.flags_res >> 15)) & 1);
        case FLAGS_ROL32:
                return (int)((cpu_state.flags_res ^ (cpu_state.flags_res >> 31)) & 1);

        case FLAGS_ROR8:
                return (int)((cpu_state.flags_res ^ (cpu_state.flags_res >> 1)) & 0x40);
        case FLAGS_ROR16:
                return (int)((cpu_state.flags_res ^ (cpu_state.flags_res >> 1)) & 0x4000);
        case FLAGS_ROR32:
                return ((cpu_state.flags_res ^ (cpu_state.flags_res >> 1)) & 0x40000000) != 0 ? 1 : 0;

        case FLAGS_UNKNOWN:
                return cpu_state.flags & V_FLAG;
        }
        return 0;
    }

    // pcem: x86_flags.h:275. VOIR L'EN-TÊTE : les branches ADC et SBC s'écartent du
    // silicium, et c'est consigné dans sst-baseline.tsv. Transcrites telles quelles.
    internal static int AF_SET()
    {
        switch (cpu_state.flags_op)
        {
        case FLAGS_ZN8: case FLAGS_ZN16: case FLAGS_ZN32:
        case FLAGS_SHL8: case FLAGS_SHL16: case FLAGS_SHL32:
        case FLAGS_SHR8: case FLAGS_SHR16: case FLAGS_SHR32:
        case FLAGS_SAR8: case FLAGS_SAR16: case FLAGS_SAR32:
                return 0;

        case FLAGS_ADD8: case FLAGS_ADD16: case FLAGS_ADD32:
        case FLAGS_INC8: case FLAGS_INC16: case FLAGS_INC32:
                return (int)(((cpu_state.flags_op1 & 0xF) + (cpu_state.flags_op2 & 0xF)) & 0x10);

        case FLAGS_ADC8:
                return (cpu_state.flags_res & 0xf) < (cpu_state.flags_op1 & 0xf) ||
                       ((cpu_state.flags_res & 0xf) == (cpu_state.flags_op1 & 0xf) && cpu_state.flags_op2 == 0xff) ? 1 : 0;
        case FLAGS_ADC16:
                return (cpu_state.flags_res & 0xf) < (cpu_state.flags_op1 & 0xf) ||
                       ((cpu_state.flags_res & 0xf) == (cpu_state.flags_op1 & 0xf) && cpu_state.flags_op2 == 0xffff) ? 1 : 0;
        case FLAGS_ADC32:
                return (cpu_state.flags_res & 0xf) < (cpu_state.flags_op1 & 0xf) ||
                       ((cpu_state.flags_res & 0xf) == (cpu_state.flags_op1 & 0xf) && cpu_state.flags_op2 == 0xffffffff) ? 1 : 0;

        case FLAGS_SUB8: case FLAGS_SUB16: case FLAGS_SUB32:
        case FLAGS_DEC8: case FLAGS_DEC16: case FLAGS_DEC32:
                return (int)(((cpu_state.flags_op1 & 0xF) - (cpu_state.flags_op2 & 0xF)) & 0x10);

        case FLAGS_SBC8: case FLAGS_SBC16: case FLAGS_SBC32:
                return (cpu_state.flags_op1 & 0xf) < (cpu_state.flags_op2 & 0xf) ||
                       ((cpu_state.flags_op1 & 0xf) == (cpu_state.flags_op2 & 0xf) && (cpu_state.flags_res & 0xf) != 0) ? 1 : 0;

        case FLAGS_ROL8: case FLAGS_ROL16: case FLAGS_ROL32:
        case FLAGS_ROR8: case FLAGS_ROR16: case FLAGS_ROR32:
        case FLAGS_UNKNOWN:
                return cpu_state.flags & A_FLAG;
        }
        return 0;
    }

    // pcem: x86_flags.h:334
    internal static int CF_SET()
    {
        switch (cpu_state.flags_op)
        {
        case FLAGS_ADD8:
                return ((cpu_state.flags_op1 + cpu_state.flags_op2) & 0x100) != 0 ? 1 : 0;
        case FLAGS_ADD16:
                return ((cpu_state.flags_op1 + cpu_state.flags_op2) & 0x10000) != 0 ? 1 : 0;
        case FLAGS_ADD32:
                return cpu_state.flags_res < cpu_state.flags_op1 ? 1 : 0;

        case FLAGS_ADC8:
                return cpu_state.flags_res < cpu_state.flags_op1 ||
                       (cpu_state.flags_res == cpu_state.flags_op1 && cpu_state.flags_op2 == 0xff) ? 1 : 0;
        case FLAGS_ADC16:
                return cpu_state.flags_res < cpu_state.flags_op1 ||
                       (cpu_state.flags_res == cpu_state.flags_op1 && cpu_state.flags_op2 == 0xffff) ? 1 : 0;
        case FLAGS_ADC32:
                return cpu_state.flags_res < cpu_state.flags_op1 ||
                       (cpu_state.flags_res == cpu_state.flags_op1 && cpu_state.flags_op2 == 0xffffffff) ? 1 : 0;

        case FLAGS_SUB8: case FLAGS_SUB16: case FLAGS_SUB32:
                return cpu_state.flags_op1 < cpu_state.flags_op2 ? 1 : 0;

        case FLAGS_SBC8: case FLAGS_SBC16: case FLAGS_SBC32:
                return cpu_state.flags_op1 < cpu_state.flags_op2 ||
                       (cpu_state.flags_op1 == cpu_state.flags_op2 && cpu_state.flags_res != 0) ? 1 : 0;

        case FLAGS_SHL8:
                return ((cpu_state.flags_op1 << (int)(cpu_state.flags_op2 - 1)) & 0x80) != 0 ? 1 : 0;
        case FLAGS_SHL16:
                return ((cpu_state.flags_op1 << (int)(cpu_state.flags_op2 - 1)) & 0x8000) != 0 ? 1 : 0;
        case FLAGS_SHL32:
                return ((cpu_state.flags_op1 << (int)(cpu_state.flags_op2 - 1)) & 0x80000000) != 0 ? 1 : 0;

        case FLAGS_SHR8: case FLAGS_SHR16: case FLAGS_SHR32:
                return (int)((cpu_state.flags_op1 >> (int)(cpu_state.flags_op2 - 1)) & 1);

        case FLAGS_SAR8:
                return ((sbyte)cpu_state.flags_op1 >> (int)(cpu_state.flags_op2 - 1)) & 1;
        case FLAGS_SAR16:
                return ((short)cpu_state.flags_op1 >> (int)(cpu_state.flags_op2 - 1)) & 1;
        case FLAGS_SAR32:
                return ((int)cpu_state.flags_op1 >> (int)(cpu_state.flags_op2 - 1)) & 1;

        case FLAGS_ZN8: case FLAGS_ZN16: case FLAGS_ZN32:
                return 0;

        case FLAGS_ROL8: case FLAGS_ROL16: case FLAGS_ROL32:
                return (int)(cpu_state.flags_res & 1);

        case FLAGS_ROR8:
                return (cpu_state.flags_res & 0x80) != 0 ? 1 : 0;
        case FLAGS_ROR16:
                return (cpu_state.flags_res & 0x8000) != 0 ? 1 : 0;
        case FLAGS_ROR32:
                return (cpu_state.flags_res & 0x80000000) != 0 ? 1 : 0;

        case FLAGS_DEC8: case FLAGS_DEC16: case FLAGS_DEC32:
        case FLAGS_INC8: case FLAGS_INC16: case FLAGS_INC32:
        case FLAGS_UNKNOWN:
                return cpu_state.flags & C_FLAG;
        }
        return 0;
    }

    // pcem: x86_flags.h:420 — LA MATÉRIALISATION. Après elle, cpu_state.flags est
    // à nouveau la vérité, et flags_op retombe à FLAGS_UNKNOWN pour que le prochain
    // appel ne refasse pas le travail. 0x8D5 est le masque des six bits calculés :
    // C, P, A, Z, N, V.
    internal static void flags_rebuild()
    {
        if (cpu_state.flags_op != FLAGS_UNKNOWN)
        {
                uint16_t tempf = 0;
                if (CF_SET() != 0) tempf |= C_FLAG;
                if (PF_SET() != 0) tempf |= P_FLAG;
                if (AF_SET() != 0) tempf |= A_FLAG;
                if (ZF_SET() != 0) tempf |= Z_FLAG;
                if (NF_SET() != 0) tempf |= N_FLAG;
                if (VF_SET() != 0) tempf |= V_FLAG;
                cpu_state.flags = (uint16_t)((cpu_state.flags & unchecked((uint16_t)~0x8d5)) | tempf);
                cpu_state.flags_op = FLAGS_UNKNOWN;
        }
    }

    // pcem: x86_flags.h:440 — l'inverse : on vient d'écrire cpu_state.flags à la
    // main (POPF, IRET), donc la représentation paresseuse est périmée.
    internal static void flags_extract() => cpu_state.flags_op = FLAGS_UNKNOWN;

    // pcem: x86_flags.h:442 — la retenue seule, pour les instructions qui ne lisent
    // qu'elle. Ne remet PAS flags_op à FLAGS_UNKNOWN : les cinq autres bits restent
    // paresseux.
    internal static void flags_rebuild_c()
    {
        if (cpu_state.flags_op != FLAGS_UNKNOWN)
        {
                if (CF_SET() != 0)
                        cpu_state.flags |= C_FLAG;
                else
                        cpu_state.flags &= unchecked((uint16_t)~C_FLAG);
        }
    }

    // pcem: x86_flags.h:452 — les rotations ne posent pas flags_res de façon
    // exploitable ; le recompilateur s'en sert pour décider s'il peut l'optimiser.
    internal static int flags_res_valid()
    {
        if (cpu_state.flags_op == FLAGS_UNKNOWN ||
            (cpu_state.flags_op >= FLAGS_ROL8 && cpu_state.flags_op <= FLAGS_ROR32))
                return 0;
        return 1;
    }

    // ---- LES POSEURS (pcem: x86_flags.h:458-599) -------------------------
    // Chacun enregistre l'opération et ses opérandes, et ne calcule RIEN.

    internal static void setznp8(uint8_t val) { cpu_state.flags_op = FLAGS_ZN8; cpu_state.flags_res = val; }
    internal static void setznp16(uint16_t val) { cpu_state.flags_op = FLAGS_ZN16; cpu_state.flags_res = val; }
    internal static void setznp32(uint32_t val) { cpu_state.flags_op = FLAGS_ZN32; cpu_state.flags_res = val; }

    internal static void setadd8(uint8_t a, uint8_t b)
    {
        cpu_state.flags_op1 = a; cpu_state.flags_op2 = b;
        cpu_state.flags_res = (uint32_t)((a + b) & 0xff);
        cpu_state.flags_op = FLAGS_ADD8;
    }
    internal static void setadd16(uint16_t a, uint16_t b)
    {
        cpu_state.flags_op1 = a; cpu_state.flags_op2 = b;
        cpu_state.flags_res = (uint32_t)((a + b) & 0xffff);
        cpu_state.flags_op = FLAGS_ADD16;
    }
    internal static void setadd32(uint32_t a, uint32_t b)
    {
        cpu_state.flags_op1 = a; cpu_state.flags_op2 = b;
        cpu_state.flags_res = a + b;
        cpu_state.flags_op = FLAGS_ADD32;
    }

    internal static void setsub8(uint8_t a, uint8_t b)
    {
        cpu_state.flags_op1 = a; cpu_state.flags_op2 = b;
        cpu_state.flags_res = (uint32_t)((a - b) & 0xff);
        cpu_state.flags_op = FLAGS_SUB8;
    }
    internal static void setsub16(uint16_t a, uint16_t b)
    {
        cpu_state.flags_op1 = a; cpu_state.flags_op2 = b;
        cpu_state.flags_res = (uint32_t)((a - b) & 0xffff);
        cpu_state.flags_op = FLAGS_SUB16;
    }
    internal static void setsub32(uint32_t a, uint32_t b)
    {
        cpu_state.flags_op1 = a; cpu_state.flags_op2 = b;
        cpu_state.flags_res = a - b;
        cpu_state.flags_op = FLAGS_SUB32;
    }

    // `tempc` est la retenue ENTRANTE, posée par le handler avant l'appel.
    internal static void setadc8(uint8_t a, uint8_t b)
    {
        cpu_state.flags_op1 = a; cpu_state.flags_op2 = b;
        cpu_state.flags_res = (uint32_t)((a + b + tempc) & 0xff);
        cpu_state.flags_op = FLAGS_ADC8;
    }
    internal static void setadc16(uint16_t a, uint16_t b)
    {
        cpu_state.flags_op1 = a; cpu_state.flags_op2 = b;
        cpu_state.flags_res = (uint32_t)((a + b + tempc) & 0xffff);
        cpu_state.flags_op = FLAGS_ADC16;
    }
    internal static void setadc32(uint32_t a, uint32_t b)
    {
        cpu_state.flags_op1 = a; cpu_state.flags_op2 = b;
        cpu_state.flags_res = (uint32_t)(a + b + (uint32_t)tempc);
        cpu_state.flags_op = FLAGS_ADC32;
    }

    internal static void setsbc8(uint8_t a, uint8_t b)
    {
        cpu_state.flags_op1 = a; cpu_state.flags_op2 = b;
        cpu_state.flags_res = (uint32_t)((a - (b + tempc)) & 0xff);
        cpu_state.flags_op = FLAGS_SBC8;
    }
    internal static void setsbc16(uint16_t a, uint16_t b)
    {
        cpu_state.flags_op1 = a; cpu_state.flags_op2 = b;
        cpu_state.flags_res = (uint32_t)((a - (b + tempc)) & 0xffff);
        cpu_state.flags_op = FLAGS_SBC16;
    }
    internal static void setsbc32(uint32_t a, uint32_t b)
    {
        cpu_state.flags_op1 = a; cpu_state.flags_op2 = b;
        cpu_state.flags_res = (uint32_t)(a - (b + (uint32_t)tempc));
        cpu_state.flags_op = FLAGS_SBC32;
    }
}
