// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/includes/private/cpu/x86_ops_int.h  (lignes 3-118)
// SHA256: voir oracle.tsv ; vérifier avec tools/check-oracle.sh
// STATUS: partial — A11 : les trois emplacements qu'un 286 atteint. opINT1
//         reste dehors : il n'a aucun emplacement dans ops_286.
//
// LA PORTE D'ENTRÉE DU BIOS.
//
// Trois opcodes, et le plus court des trois fichiers de ce jalon — mais c'est
// par là que tout logiciel appelle le système, et rien ne marchait sans lui.
//
// LES TROIS PASSENT PAR x86_int_sw, PAS PAR x86_int, et la différence tient en
// une ligne absente : x86_int remet pc sur l'instruction fautive, x86_int_sw
// non. Une FAUTE doit pouvoir être rejouée, une INTERRUPTION LOGICIELLE doit
// reprendre après. Voir 386_common.cs.
//
// LES TROIS RENDENT 1 QUAND ILS DÉCLENCHENT, et c'est ce qui fait sortir
// exec386 de sa boucle interne pour qu'il reprenne le fetch à la nouvelle
// adresse. INTO est le seul à pouvoir rendre 0 — quand V n'est pas posé, il ne
// fait rien qu'attendre trois cycles.
//
// LE GROS BLOC MORT D'opINT. La forme VME (cr4 & CR4_VME) lit la carte de
// redirection d'interruptions dans le TSS, 25 lignes qui n'existent qu'à partir
// du Pentium. Elle est OMISE et non transcrite-puis-morte, parce qu'elle
// appelle x86_int_sw_rm, elle-même omise : transcrire l'une sans l'autre aurait
// fait une coquille. Le garde qui y mène est conservé.

using static iXtal26.Cpu._386_common;
using static iXtal26.Cpu.x86;
using static iXtal26.Cpu.x86_flags;

namespace iXtal26.Cpu;

internal static partial class _386
{
    // pcem: x86_ops_int.h:3-14 — opINT3.
    //
    // CC EST UN OCTET, et c'est pour ça qu'il existe : un débogueur le pose sur
    // n'importe quelle instruction sans en déplacer aucune autre.
    private static int opINT3(uint32_t fetchdat)
    {
        int cycles_old = cycles;
        if ((cr0 & 1) != 0 && (cpu_state.eflags & VM_FLAG) != 0 && IOPL != 3)
        {
                x86seg_c.x86gpf("", 0);
                return 1;
        }
        x86_int_sw(3);
        CLOCK_CYCLES(is486 != 0 ? 44 : 59);
        PREFETCH_RUN(cycles_old - cycles, 1, -1, 0, 0, 0, 0, 0);
        return 1;
    }

    // pcem: x86_ops_int.h:29-98 — opINT.
    private static int opINT(uint32_t fetchdat)
    {
        int cycles_old = cycles;
        uint8_t temp = (uint8_t)fetchdat; cpu_state.pc++;   // getbytef()

        if ((cr0 & 1) != 0 && (cpu_state.eflags & VM_FLAG) != 0 && IOPL != 3)
        {
                // omitted: la branche `if (cr4 & CR4_VME)` (x86_ops_int.h:34-59)
                //   — la carte de redirection d'interruptions du TSS, Pentium et
                //   au-dela. Elle appelle x86_int_sw_rm, elle-meme omise.
                x86seg_c.x86gpf_expected("", 0);
                return 1;
        }

        x86_int_sw(temp);
        PREFETCH_RUN(cycles_old - cycles, 2, -1, 0, 0, 0, 0, 0);
        return 1;
    }

    // pcem: x86_ops_int.h:100-118 — opINTO.
    //
    // IL REMET oldpc SUR pc AVANT DE DÉCLENCHER, et lui seul des trois. INT et
    // INT3 laissent oldpc tel quel. La conséquence se lit sur la pile : une INTO
    // empile l'adresse de l'instruction SUIVANTE, comme les deux autres — mais
    // en passant par oldpc, donc si quelque chose avait modifié pc entre-temps,
    // c'est la valeur courante qui gagne. PCem l'écrit ainsi ; transcrit tel quel.
    private static int opINTO(uint32_t fetchdat)
    {
        int cycles_old = cycles;

        if ((cr0 & 1) != 0 && (cpu_state.eflags & VM_FLAG) != 0 && IOPL != 3)
        {
                x86seg_c.x86gpf("", 0);
                return 1;
        }
        if (VF_SET() != 0)
        {
                cpu_state.oldpc = cpu_state.pc;
                x86_int_sw(4);
                PREFETCH_RUN(cycles_old - cycles, 1, -1, 0, 0, 0, 0, 0);
                return 1;
        }
        CLOCK_CYCLES(3);
        PREFETCH_RUN(3, 1, -1, 0, 0, 0, 0, 0);
        return 0;
    }

    // omitted: opINT1 (x86_ops_int.h:16-27) — l'opcode F1, que la table donne à
    //   opLOCK sur un 286. Verifie dans la .so : ops_286[0xF1] nomme opLOCK.

    /// <summary>pcem: CC, CD, CE — relevés sur ops_286[] par gdb.</summary>
    private static void PoserGroupeInterruptions()
    {
        ops_286[0xCC] = opINT3;
        ops_286[0xCD] = opINT;
        ops_286[0xCE] = opINTO;
    }

    // ---- G2, D2 : les formes 32 bits (_l, _a32) de x86_ops_int.h ----

    // pcem: x86_ops_int.h:16
    private static int opINT1(uint32_t fetchdat)
    {
        int cycles_old = cycles;
        if ((cr0 & 1) != 0 && (cpu_state.eflags & VM_FLAG) != 0 && IOPL != 3)
        {
                x86seg_c.x86gpf("", 0);
                return 1;
        }
        x86_int_sw(1);
        CLOCK_CYCLES((is486 != 0) ? 44 : 59);
        PREFETCH_RUN(cycles_old - cycles, 1, -1, 0, 0, 0, 0, 0);
        return 1;
    }

    // pcem: 386_ops.h — les emplacements de ces handlers dans OP_TABLE(386) et (386_0f).
    private static void PoserGroupe_int_386()
    {
        ops_386[0x0F1] = opINT1;
        ops_386[0x1F1] = opINT1;
        ops_386[0x2F1] = opINT1;
        ops_386[0x3F1] = opINT1;
    }
}
