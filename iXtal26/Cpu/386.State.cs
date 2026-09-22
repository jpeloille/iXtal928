// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: tools/oracle/harness.c  (h_set_core / h_reset / h_step286)
// STATUS: host — porte de diagnostic, pas de code PCem transcrit.
//         R2 (parité de lignes) ne s'applique pas.
//
// LE PILOTE DU CŒUR 286, pendant exact de celui du harnais C.
//
// Il doit l'être au geste près : c'est la seule façon que les deux côtés partent
// du même état et avancent du même pas. Toute asymétrie ici se lirait comme une
// divergence du cœur.

using iXtal26.Memory;
using static iXtal26.Cpu._386_common;
using static iXtal26.Cpu.x86;

namespace iXtal26.Cpu;

internal static partial class _386
{
    /// <summary>Ce que cpu_set() poserait pour cpus_286[0], le « 286/6 » de l'AT
    /// 5170. Pendant de h_cpu_config_286() (tools/oracle/harness_stubs.c), repris
    /// de cpu.c:323-353 — la branche `case CPU_286:`, inatteignable ici comme
    /// là-bas parce qu'elle déréférence models[].</summary>
    internal static void cpu_config_286()
    {
        x86_setopcodes(ops_286, ops_286_0f);

        cpu.cpu_busspeed = 6000000;
        cpu.isa_cycles = 1;

        // cpu.c:189 — et resetx86() en tire rammask. Un 286 a 24 lignes d'adresse.
        cpu_16bitbus = 1;

        // cpu.c:2036-2047, branche « memory timings » : mem_read_cycles =
        // mem_write_cycles = 2 (cpu_tables.c:70), et (cpu_16bitbus ? 2 : 1) vaut 2.
        cpu.cpu_prefetch_width = 2;
        cpu.cpu_prefetch_cycles = 2;
        cpu.cpu_cycles_read = 2;
        cpu.cpu_cycles_read_l = 4;
        cpu.cpu_cycles_write = 2;
        cpu.cpu_cycles_write_l = 4;
        cpu.cpu_mem_prefetch_cycles = 2;
        cpu.cpu_rom_prefetch_cycles = 2;
    }

    /// <summary>Pendant de h_reset() avec h_core == H_CORE_286.</summary>
    internal static void Reset286()
    {
        _808x.FlatMap286();

        // AT = 1 : c'est LUI qui aiguille vers exec386 chez PCem (pc.c:484), et
        // resetx86() branche dessus pour le vecteur de reset et rammask.
        AT = 1;
        is386 = 0;
        is486 = 0;
        _808x.is8086 = 0;
        cpu.hasfpu = 0;
        AMSTRAD = TANDY = PCI = MCA = 0;

        cpu_config_286();

        timer.tsc = 0;
        timer.timer_target = 0x7FFFFFFF;

        _808x.resetx86();
    }

    /// <summary>Une instruction exactement. Pendant de h_step286().
    ///
    /// LE PAS-À-PAS DU 286 N'EST PAS CELUI DU 8088, et la différence est
    /// structurelle : la boucle interne de exec386 est bornée par
    /// `cycdiff &lt; cycle_period`, que le budget `cycles` ne borne pas. On rapproche
    /// donc la borne — timer_target posé à tsc rend cycle_period == 1.
    ///
    /// DEVIATION assumée, identique des deux côtés : un timer_process() par pas, là
    /// où le 8088 n'en déclenche aucun. timer_target est restauré pour que l'état
    /// comparé ne porte pas la trace du mécanisme.</summary>
    internal static int Step286()
    {
        var savedTarget = timer.timer_target;

        cycles = 1;
        timer.timer_target = (uint32_t)timer.tsc;
        exec386(0);
        timer.timer_target = savedTarget;

        return 1 - cycles;
    }
}
