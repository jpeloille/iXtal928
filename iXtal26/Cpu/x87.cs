// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/cpu/x87.c  (lignes 22-61, 97)
//         et includes/private/cpu/x87.h  (lignes 3-9, 27-35)
// SHA256: voir oracle.tsv ; vérifier avec tools/check-oracle.sh
// STATUS: partial — G4.1 : les deux globales du dernier ESC, la conversion du mot de tags
//         (x87_gettag / x87_settag, lues par FSTENV, FSAVE, FLDENV, FRSTOR en G4.4) et
//         x87_reset, vide chez PCem. x87_dumpregs et x87_print sont omis : du pclog.
//
// PCem garde un tag par registre PHYSIQUE, dans son propre codage (TAG_EMPTY, TAG_VALID,
// TAG_UINT64) ; le mot de tags de l'architecture — deux bits par registre, 00 valide,
// 01 zéro, 10 spécial, 11 vide — n'existe qu'à la traduction, ici.

using static iXtal26.Cpu._386_common;

namespace iXtal26.Cpu;

internal static class x87_c
{
    // pcem: x87.h:3-6
    internal const uint16_t C0 = 1 << 8;
    internal const uint16_t C1 = 1 << 9;
    internal const uint16_t C2 = 1 << 10;
    internal const uint16_t C3 = 1 << 14;

    // pcem: x87.h:27-30
    internal const uint8_t TAG_EMPTY = 0;
    internal const uint8_t TAG_VALID = 1 << 0;
    /*Hack for FPU copy. If set then MM[].q contains the 64-bit integer loaded by FILD*/
    internal const uint8_t TAG_UINT64 = 1 << 7;

    // pcem: x87.h:32-35
    internal const int X87_ROUNDING_NEAREST = 0;
    internal const int X87_ROUNDING_DOWN = 1;
    internal const int X87_ROUNDING_UP = 2;
    internal const int X87_ROUNDING_CHOP = 3;

    // pcem: x87.c:22-23 (déclarées x87.h:8-9) — posées en G4.0 dans x86.cs, ici depuis G4.1.
    // pcem bug, reproduced: PB-62 — jamais écrites : FSAVE et FSTENV rangent des zéros pour les pointeurs.
    internal static uint32_t x87_pc_off, x87_op_off;
    internal static uint16_t x87_pc_seg, x87_op_seg;

    // pcem: x87.c:25-28
    private const int X87_TAG_VALID = 0;
    private const int X87_TAG_ZERO = 1;
    private const int X87_TAG_INVALID = 2;
    private const int X87_TAG_EMPTY = 3;

    // pcem: x87.c:30-46
    internal static uint16_t x87_gettag()
    {
        uint16_t ret = 0;
        int c;

        for (c = 0; c < 8; c++) {
                if (cpu_state.tag[c] == TAG_EMPTY)
                        ret |= (uint16_t)(X87_TAG_EMPTY << (c * 2));
                else if ((cpu_state.tag[c] & TAG_UINT64) != 0)
                        // pcem bug, reproduced: PB-199 — l'entier de FILD m64 est étiqueté 10, « spécial ».
                        ret |= (uint16_t)(2 << (c * 2));
                else if (cpu_state.ST[c] == 0.0 && cpu_state.ismmx == 0)
                        ret |= (uint16_t)(X87_TAG_ZERO << (c * 2));
                else
                        // pcem bug, reproduced: PB-208 — un NaN ou un infini est étiqueté 00, pas 10.
                        ret |= (uint16_t)(X87_TAG_VALID << (c * 2));
        }

        return ret;
    }

    // pcem: x87.c:48-61
    internal static void x87_settag(uint16_t new_tag)
    {
        int c;

        for (c = 0; c < 8; c++) {
                int tag = (new_tag >> (c * 2)) & 3;

                if (tag == X87_TAG_EMPTY)
                        cpu_state.tag[c] = TAG_EMPTY;
                else if (tag == 2)
                        // pcem bug, reproduced: PB-208 — 10 est relu comme TAG_UINT64 : après FLDENV, FISTP
                        //   m64 écrit le MM[].q périmé du registre.
                        cpu_state.tag[c] = TAG_VALID | TAG_UINT64;
                else
                        cpu_state.tag[c] = TAG_VALID;
        }
    }

    // omitted: x87_dumpregs et x87_print (x87.c:63-95) — des pclog, sortie pure.

    // pcem: x87.c:97 — VIDE chez PCem : ni ST, ni tag, ni TOP, ni npxs, ni npxc ne sont remis
    //   par un reset. Leur valeur est le zéro de la mise sous tension (VERIFICATION.md § G4.0,
    //   h_fpu_clear_residue).
    // pcem bug, reproduced: PB-203 — le silicium sort du RESET dans l'état de FNINIT (le 387 : IE et ES
    //   posés en plus).
    internal static void x87_reset() { }
}
