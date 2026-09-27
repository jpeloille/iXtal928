// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/includes/private/cpu/x86_ops_pmode.h  (opARPL_a16)
// SHA256: voir oracle.tsv ; vérifier avec tools/check-oracle.sh
// STATUS: partial — A11 : le SEUL emplacement de cet en-tête qu'un 286 atteigne
//         par la table à un octet. Tout le reste — LGDT, LIDT, LMSW, LAR, LSL,
//         VERR, VERW — vit dans la table 0F et relève du bloc C.
//
// L'UNIQUE OPCODE DE MODE PROTÉGÉ DE LA TABLE À UN OCTET.
//
// ARPL ajuste le niveau de privilège demandé d'un sélecteur, et c'est une
// nouveauté du 286 : un système d'exploitation s'en sert pour vérifier qu'un
// pointeur reçu d'un programme moins privilégié ne lui donne pas plus de droits
// qu'il n'en a.
//
// IL COMMENCE PAR NOTRM, ET C'EST TOUT CE QU'IL FAIT EN MODE RÉEL. La macro
// (386_common.h:94-98) lève INT 6 — opcode invalide — si msw & 1 est nul.
// Donc sur un 286 qui n'est jamais entré en mode protégé, 0x63 est un opcode
// illégal, et le reste du handler est inatteignable. Il est transcrit quand
// même : le jour où le mode protégé arrivera, ce handler marchera sans qu'on y
// revienne.
//
// LE `pclog("ARPL_a16")` DE PCem EST OMIS, comme toute sa journalisation.

using static iXtal26.Cpu._386_common;
using static iXtal26.Cpu.x86;

namespace iXtal26.Cpu;

internal static partial class _386
{
    /// <summary>pcem: 386_common.h:94-98 — la macro NOTRM.
    ///
    /// DEVIATION: `return 1` depuis le milieu du macro ; la méthode rend `true`
    ///   pour « l'appelant doit rendre 1 ».</summary>
    private static bool NOTRM()
    {
        if ((msw & 1) == 0 || (cpu_state.eflags & VM_FLAG) != 0)
        {
                x86_int(6);
                return true;
        }
        return false;
    }

    // pcem: x86_ops_pmode.h — opARPL_a16.
    //
    // IL POSE Z_FLAG A LA MAIN, comme le groupe BCD — d'ou le flags_rebuild()
    // avant. Z dit si l'ajustement a EU LIEU, pas si une comparaison est vraie.
    private static int opARPL_a16(uint32_t fetchdat)
    {
        uint16_t temp_seg;

        if (NOTRM()) return 1;
        if (fetch_ea_16(fetchdat)) return 1;
        if (cpu_mod != 3)
                if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
        temp_seg = geteaw();
        if (cpu_state.abrt != 0)
                return 1;

        _386.flags_rebuild();
        if ((temp_seg & 3) < (cpu_state.regs[cpu_reg].w & 3))
        {
                temp_seg = (uint16_t)((temp_seg & 0xfffc) | (cpu_state.regs[cpu_reg].w & 3));
                seteaw(temp_seg);
                if (cpu_state.abrt != 0)
                        return 1;
                cpu_state.flags |= Z_FLAG;
        }
        else
                cpu_state.flags &= unchecked((uint16_t)~Z_FLAG);

        CLOCK_CYCLES(is486 != 0 ? 9 : 20);
        PREFETCH_RUN(is486 != 0 ? 9 : 20, 2, (int)fetchdat, 1, 0, 1, 0, 0);
        return 0;
    }

    // omitted: opARPL_a32 — op32 nul sur un 286.
    // omitted: LGDT, LIDT, LMSW, SGDT, SIDT, SMSW, LAR, LSL, VERR, VERW — ils
    //   sont dans la table 0F (ops_286_0f), que le bloc C du plan transcrira.
    //   Six handlers seulement y sont atteignables sur un 286.

    /// <summary>pcem: 63 — relevé sur ops_286[] par gdb.</summary>
    private static void PoserGroupeModeProtege()
    {
        ops_286[0x63] = opARPL_a16;
    }

    // ---- G2, D2 : les formes 32 bits (_l, _a32) de x86_ops_pmode.h ----

    // pcem: x86_ops_pmode.h:29
    private static int opARPL_a32(uint32_t fetchdat)
    {
        uint16_t temp_seg;

        if (NOTRM()) return 1;
        if (fetch_ea_32(fetchdat)) return 1;
        if (cpu_mod != 3)
                if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
        // omitted: pclog — sortie de diagnostic.
        temp_seg = geteaw();
        if (cpu_state.abrt != 0)
                return 1;

        flags_rebuild();
        if ((temp_seg & 3) < (cpu_state.regs[cpu_reg].w & 3)) {
                temp_seg = (uint16_t)((temp_seg & 0xfffc) | (cpu_state.regs[cpu_reg].w & 3));
                seteaw((uint16_t)(temp_seg));
                if (cpu_state.abrt != 0)
                        return 1;
                cpu_state.flags |= Z_FLAG;
        } else
                cpu_state.flags &= unchecked((uint16_t)~(Z_FLAG));

        CLOCK_CYCLES(is486 != 0 ? 9 : 20);
        PREFETCH_RUN(is486 != 0 ? 9 : 20, 2, (int)fetchdat, 1, 0, 1, 0, 1);
        return 0;
    }

    // pcem: 386_ops.h — les emplacements de ces handlers dans OP_TABLE(386) et (386_0f).
    private static void PoserGroupe_pmode_386()
    {
        ops_386[0x263] = opARPL_a32;
        ops_386[0x363] = opARPL_a32;
    }
}
