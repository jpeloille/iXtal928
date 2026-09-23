// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/includes/private/cpu/x86_ops_misc.h  (opNOP, lignes 28-32)
// SHA256: voir oracle.tsv ; vérifier avec tools/check-oracle.sh
// STATUS: partial — A9 : opNOP seul. Le reste de l'en-tête — SETALC, les
//         groupes F6/F7, CBW, CWD, XLAT, LEA, LES, LDS, WAIT, HLT — arrive
//         avec A12.
//
// POURQUOI NOP ARRIVE AVANT SON GROUPE.
//
// Le fuzzeur et core286-check remplissent la RAM de 0x90 des deux côtés, et
// c'est un choix : si le cœur s'échappe, il tombe sur des no-op plutôt que sur
// du hasard. Ce choix ne valait que tant que 0x90 était transcrit — il ne
// l'était pas, et A9 a rendu la fuite POSSIBLE.
//
// LE MÉCANISME, mesuré. Un décalage de compte nul sort par `if (!c) return 0;`
// AVANT tout CLOCK_CYCLES : il consomme ZÉRO cycle. Or la boucle interne de
// exec386 est bornée par `cycdiff < cycle_period`, et h_step286 comme Step286
// posent cycle_period à 1 en rapprochant timer_target de tsc. Avec un cycdiff
// qui reste à zéro, la boucle REPART — et « un pas » exécute deux instructions.
//
// Les deux côtés le font à l'identique, donc la comparaison reste juste ; ce
// qui ne l'était pas, c'est que le C# rencontrait alors un opcode non
// transcrit là où l'oracle exécutait un NOP. La porte « un pas = une
// instruction » de core286-check reste vraie pour tout ce qui consomme au
// moins un cycle, et c'était le cas de tous les groupes jusqu'à A9.

using static iXtal26.Cpu._386_common;
using static iXtal26.Cpu.x86;

namespace iXtal26.Cpu;

internal static partial class _386
{
    // pcem: x86_ops_misc.h:28-32 — opNOP.
    //
    // Il occupe l'emplacement 0x90, qui SERAIT XCHG AX, AX. PCem lui donne un
    // handler à part parce qu'un échange d'un registre avec lui-même n'écrit
    // rien et ne pose aucun drapeau — voir 386_ops_xchg.cs, dont la boucle part
    // de 1 pour lui laisser la place.
    private static int opNOP(uint32_t fetchdat)
    {
        CLOCK_CYCLES(is486 != 0 ? 1 : 3);
        PREFETCH_RUN(3, 1, -1, 0, 0, 0, 0, 0);
        return 0;
    }

    /// <summary>pcem: 90 — relevé sur ops_286[] par gdb.</summary>
    private static void PoserNop()
    {
        ops_286[0x90] = opNOP;
    }
}
