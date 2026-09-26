// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/includes/private/cpu/x86_ops_io.h  (lignes 3-160)
// SHA256: voir oracle.tsv ; vérifier avec tools/check-oracle.sh
// STATUS: partial — A11 : les huit emplacements qu'un 286 atteint. opIN_EAX_imm,
//         opOUT_EAX_imm, opIN_EAX_DX et opOUT_EAX_DX restent dehors, déclarés
//         au registre des omissions.
//
// LES HUIT PORTES DU MONDE EXTÉRIEUR.
//
// Quatre formes d'accès — immédiat ou DX, octet ou mot — en lecture et en
// écriture. Le corps est court ; tout l'intérêt est dans la QUEUE.
//
// LES TROIS SORTIES ANTICIPÉES. Chaque handler finit par la même séquence :
//   1. `if (cpu_state.smi_pending) return 1;` — OMIS, comme partout dans ce
//      port : le mode SMM est du 486, et le champ n'existe pas dans x86.cs.
//   2. `if (nmi && nmi_enable && nmi_mask) return 1;` — une E/S peut ARMER une
//      NMI (le contrôleur de parité mémoire, par exemple), et rendre 1 sort de
//      la boucle interne pour que exec386 la prenne en compte tout de suite.
//   3. `return x86_was_reset;` — et celle-là ne vaut QUE pour les deux OUT qui
//      peuvent toucher le port 0x64. Voir plus bas.
//
// LE PORT 0x64 EST LA LIGNE DE RESET, et c'est la seule raison pour laquelle
// opOUT_AL_imm porte `if (port == 0x64) return x86_was_reset;`. Sur un AT, le
// 8042 y reçoit la commande qui abaisse RESET — c'est ainsi qu'on SORT du mode
// protégé sur un 286, faute d'autre moyen. opOUT_AL_DX rend x86_was_reset
// INCONDITIONNELLEMENT, sans tester le port : PCem ne s'embarrasse pas d'un
// test que DX rendrait coûteux. Les deux formes MOT, elles, rendent 0 — donc un
// `OUT DX, AX` sur 0x64 ne provoquerait pas le reset. Transcrit tel quel.
//
// ET LES DEUX OCTETS D'UN PORT MOT SONT VÉRIFIÉS SÉPARÉMENT : check_io_perm
// (port) puis check_io_perm(port + 1). Même piège qu'aux formes mot de INS et
// OUTS à A10, où mon convertisseur mécanique n'en avait traduit qu'un.

using static iXtal26.Cpu._386_common;
using static iXtal26.Cpu.x86;

namespace iXtal26.Cpu;

internal static partial class _386
{
    // pcem: x86_ops_io.h:3-15 — opIN_AL_imm
    private static int opIN_AL_imm(uint32_t fetchdat)
    {
        uint16_t port = (uint8_t)fetchdat; cpu_state.pc++;   // getbytef()
        if (check_io_perm(port)) return 1;
        AL = io.inb(port);
        CLOCK_CYCLES(12);
        PREFETCH_RUN(12, 2, -1, 1, 0, 0, 0, 0);
        // omitted: `if (cpu_state.smi_pending) return 1;` — SMM, 486 et au-dela.
        if (_808x.nmi != 0 && nmi_enable != 0 && _808x.nmi_mask != 0)
                return 1;
        return 0;
    }

    // pcem: x86_ops_io.h:16-28 — opIN_AX_imm
    private static int opIN_AX_imm(uint32_t fetchdat)
    {
        uint16_t port = (uint8_t)fetchdat; cpu_state.pc++;
        if (check_io_perm(port)) return 1;
        if (check_io_perm((uint16_t)(port + 1))) return 1;
        AX = io.inw(port);
        CLOCK_CYCLES(12);
        PREFETCH_RUN(12, 2, -1, 1, 0, 0, 0, 0);
        if (_808x.nmi != 0 && nmi_enable != 0 && _808x.nmi_mask != 0)
                return 1;
        return 0;
    }

    // pcem: x86_ops_io.h:45-59 — opOUT_AL_imm. LE SEUL qui teste le port 0x64.
    private static int opOUT_AL_imm(uint32_t fetchdat)
    {
        uint16_t port = (uint8_t)fetchdat; cpu_state.pc++;
        if (check_io_perm(port)) return 1;
        io.outb(port, AL);
        CLOCK_CYCLES(10);
        PREFETCH_RUN(10, 2, -1, 0, 0, 1, 0, 0);
        if (_808x.nmi != 0 && nmi_enable != 0 && _808x.nmi_mask != 0)
                return 1;
        if (port == 0x64)
                return x86_was_reset;
        return 0;
    }

    // pcem: x86_ops_io.h:60-73 — opOUT_AX_imm. Pas de test du port 0x64.
    private static int opOUT_AX_imm(uint32_t fetchdat)
    {
        uint16_t port = (uint8_t)fetchdat; cpu_state.pc++;
        if (check_io_perm(port)) return 1;
        if (check_io_perm((uint16_t)(port + 1))) return 1;
        io.outw(port, AX);
        CLOCK_CYCLES(10);
        PREFETCH_RUN(10, 2, -1, 0, 0, 1, 0, 0);
        if (_808x.nmi != 0 && nmi_enable != 0 && _808x.nmi_mask != 0)
                return 1;
        return 0;
    }

    // pcem: x86_ops_io.h:96-106 — opIN_AL_DX
    private static int opIN_AL_DX(uint32_t fetchdat)
    {
        if (check_io_perm(DX)) return 1;
        AL = io.inb(DX);
        CLOCK_CYCLES(12);
        PREFETCH_RUN(12, 1, -1, 1, 0, 0, 0, 0);
        if (_808x.nmi != 0 && nmi_enable != 0 && _808x.nmi_mask != 0)
                return 1;
        return 0;
    }

    // pcem: x86_ops_io.h:107-118 — opIN_AX_DX
    private static int opIN_AX_DX(uint32_t fetchdat)
    {
        if (check_io_perm(DX)) return 1;
        if (check_io_perm((uint16_t)(DX + 1))) return 1;
        AX = io.inw(DX);
        CLOCK_CYCLES(12);
        PREFETCH_RUN(12, 1, -1, 1, 0, 0, 0, 0);
        if (_808x.nmi != 0 && nmi_enable != 0 && _808x.nmi_mask != 0)
                return 1;
        return 0;
    }

    // pcem: x86_ops_io.h:131-141 — opOUT_AL_DX. Rend x86_was_reset SANS tester
    // le port : PCem ne s'embarrasse pas d'un test que DX rendrait coûteux.
    private static int opOUT_AL_DX(uint32_t fetchdat)
    {
        if (check_io_perm(DX)) return 1;
        io.outb(DX, AL);
        CLOCK_CYCLES(11);
        PREFETCH_RUN(11, 1, -1, 0, 0, 1, 0, 0);
        if (_808x.nmi != 0 && nmi_enable != 0 && _808x.nmi_mask != 0)
                return 1;
        return x86_was_reset;
    }

    // pcem: x86_ops_io.h:142-155 — opOUT_AX_DX
    private static int opOUT_AX_DX(uint32_t fetchdat)
    {
        if (check_io_perm(DX)) return 1;
        if (check_io_perm((uint16_t)(DX + 1))) return 1;
        io.outw(DX, AX);
        CLOCK_CYCLES(11);
        PREFETCH_RUN(11, 1, -1, 0, 0, 1, 0, 0);
        if (_808x.nmi != 0 && nmi_enable != 0 && _808x.nmi_mask != 0)
                return 1;
        return 0;
    }

    // omitted: opIN_EAX_imm, opOUT_EAX_imm, opIN_EAX_DX, opOUT_EAX_DX — op32
    //   nul sur un 286, et inl/outl n'ont aucun appelant.

    /// <summary>pcem: E4-E7 et EC-EF — relevés sur ops_286[] par gdb.</summary>
    private static void PoserGroupeES()
    {
        ops_286[0xE4] = opIN_AL_imm;
        ops_286[0xE5] = opIN_AX_imm;
        ops_286[0xE6] = opOUT_AL_imm;
        ops_286[0xE7] = opOUT_AX_imm;
        ops_286[0xEC] = opIN_AL_DX;
        ops_286[0xED] = opIN_AX_DX;
        ops_286[0xEE] = opOUT_AL_DX;
        ops_286[0xEF] = opOUT_AX_DX;
    }

    // ---- G2, D2 : les formes 32 bits (_l, _a32) de x86_ops_io.h ----

    // pcem: x86_ops_io.h:110
    private static int opIN_EAX_DX(uint32_t fetchdat)
    {
        if (check_io_perm(DX)) return 1;
        if (check_io_perm((uint16_t)(DX + 1))) return 1;
        if (check_io_perm((uint16_t)(DX + 2))) return 1;
        if (check_io_perm((uint16_t)(DX + 3))) return 1;
        EAX = io.inl(DX);
        CLOCK_CYCLES(12);
        PREFETCH_RUN(12, 1, -1, 0, 1, 0, 0, 0);
        // omitted: `if ((cpu_state.smi_pending) != 0) return 1;` — SMM, 486 et au-dela.
        if (_808x.nmi != 0 && nmi_enable != 0 && _808x.nmi_mask != 0)
                return 1;
        return 0;
    }

    // pcem: x86_ops_io.h:28
    private static int opIN_EAX_imm(uint32_t fetchdat)
    {
        uint16_t port = (uint8_t)fetchdat; cpu_state.pc++;
        if (check_io_perm(port)) return 1;
        if (check_io_perm((uint16_t)(port + 1))) return 1;
        if (check_io_perm((uint16_t)(port + 2))) return 1;
        if (check_io_perm((uint16_t)(port + 3))) return 1;
        EAX = io.inl(port);
        CLOCK_CYCLES(12);
        PREFETCH_RUN(12, 2, -1, 0, 1, 0, 0, 0);
        // omitted: `if ((cpu_state.smi_pending) != 0) return 1;` — SMM, 486 et au-dela.
        if (_808x.nmi != 0 && nmi_enable != 0 && _808x.nmi_mask != 0)
                return 1;
        return 0;
    }

    // pcem: x86_ops_io.h:149
    private static int opOUT_EAX_DX(uint32_t fetchdat)
    {
        if (check_io_perm(DX)) return 1;
        if (check_io_perm((uint16_t)(DX + 1))) return 1;
        if (check_io_perm((uint16_t)(DX + 2))) return 1;
        if (check_io_perm((uint16_t)(DX + 3))) return 1;
        io.outl(DX, EAX);
        PREFETCH_RUN(11, 1, -1, 0, 0, 0, 1, 0);
        // omitted: `if ((cpu_state.smi_pending) != 0) return 1;` — SMM, 486 et au-dela.
        if (_808x.nmi != 0 && nmi_enable != 0 && _808x.nmi_mask != 0)
                return 1;
        return 0;
    }

    // pcem: x86_ops_io.h:71
    private static int opOUT_EAX_imm(uint32_t fetchdat)
    {
        uint16_t port = (uint8_t)fetchdat; cpu_state.pc++;
        if (check_io_perm(port)) return 1;
        if (check_io_perm((uint16_t)(port + 1))) return 1;
        if (check_io_perm((uint16_t)(port + 2))) return 1;
        if (check_io_perm((uint16_t)(port + 3))) return 1;
        io.outl(port, EAX);
        CLOCK_CYCLES(10);
        PREFETCH_RUN(10, 2, -1, 0, 0, 0, 1, 0);
        // omitted: `if ((cpu_state.smi_pending) != 0) return 1;` — SMM, 486 et au-dela.
        if (_808x.nmi != 0 && nmi_enable != 0 && _808x.nmi_mask != 0)
                return 1;
        return 0;
    }

    // pcem: 386_ops.h — les emplacements de ces handlers dans OP_TABLE(386) et (386_0f).
    private static void PoserGroupe_io_386()
    {
        ops_386[0x1E5] = opIN_EAX_imm;
        ops_386[0x1E7] = opOUT_EAX_imm;
        ops_386[0x1ED] = opIN_EAX_DX;
        ops_386[0x1EF] = opOUT_EAX_DX;
        ops_386[0x3E5] = opIN_EAX_imm;
        ops_386[0x3E7] = opOUT_EAX_imm;
        ops_386[0x3ED] = opIN_EAX_DX;
        ops_386[0x3EF] = opOUT_EAX_DX;
    }
}
