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
    /// <summary>L'indice, dans la table de la machine, du processeur que Reset286() ou
    /// Reset386() configure — le `--cpu N` de `fuzz --core 286|386`. Pendant de
    /// h_set_cpu() côté oracle ; 0 par défaut (286/6, i386SX/16).</summary>
    internal static int FuzzCpu;

    /// <summary>Le coprocesseur (FPU_*, cpu.h:72) que Reset(), Reset286() et Reset386()
    /// donnent à cpu_set() — pendant de h_set_fpu() côté oracle (G4.0). FPU_NONE par
    /// défaut.</summary>
    internal static int FuzzFpu;

    /// <summary>Pendant de h_reset() avec h_core == H_CORE_286.</summary>
    internal static void Reset286() => ResetExec386("ami286");

    /// <summary>Pendant de h_reset() avec h_core == H_CORE_386 (G2, D0.2) : le MÊME
    /// exec386, sur l'ami386 — cpu_set() y lit cpus_i386SX, pose is386 et la table
    /// ops_386. `FuzzCpu` est alors l'indice dans cpus_i386SX.</summary>
    internal static void Reset386() => ResetExec386("ami386");

    /// <summary>Pendant de h_reset() avec h_core == H_CORE_486 (G6.0) : toujours exec386 —
    /// PCem n'a pas de table d'opcodes 486 —, sur l'ami486, carte plate de 16 Mo comme le
    /// 386. `FuzzCpu` est l'indice dans cpus_i486.</summary>
    internal static void Reset486()
    {
        if (Models.model_c.model_get_model_from_internal_name("ami486") < 0)
                pc.fatal("not implemented: l'ami486 et cpus_i486 entrent en G6.1 (PLAN-G6.md)\n");
        ResetExec386("ami486");
    }

    /// <summary>La table de CPU de l'ami486 (cpus_i486), ou une table vide sans elle.</summary>
    internal static CPU[] Table486()
    {
        int m = Models.model_c.model_get_model_from_internal_name("ami486");
        return m < 0 ? [new CPU("", -1, null, 0, 0, 0)]
                     : Models.model_c.models[m].cpu[0].cpus!;
    }

    /// <summary>Le corps commun : seule la machine distingue le 286 du 386, comme
    /// h_reset() côté oracle, qui ne choisit que `model` selon h_core.</summary>
    private static void ResetExec386(string machine)
    {
        // G13 — un cœur démarre ici sans initpc : le mode matériel se fige avant lui (ModeMateriel).
        ModeMateriel.Figer();
        if (machine is "ami386" or "ami486")
                _808x.FlatMap386();
        else
                _808x.FlatMap286();

        // LES COMPTEURS ET L'ÉTAT DE TEMPS, avant resetx86 comme h_reset les remet.
        //
        // h_reset() les remet à zéro QUEL QUE SOIT le cœur sélectionné : ce ne sont
        // pas des affaires du 8088, ce sont celles du harnais. Les omettre ici ferait
        // partir le C# avec ce que la campagne précédente a laissé, pendant que
        // l'oracle repart de zéro — et dix des champs comparés (n_readmembl et les
        // neuf autres compteurs de stubs) divergeraient dès la première instruction.
        // La panne se lirait comme un défaut de handler. Elle n'en serait pas un.
        _808x.ResetCounters();
        _808x.ClearFpuResidue();

        // AT = 1 : c'est LUI qui aiguille vers exec386 chez PCem (pc.c:484), et
        // resetx86() branche dessus pour le vecteur de reset et rammask.
        AT = 1;
        is386 = 0;
        is486 = 0;
        _808x.is8086 = 0;
        cpu_c.hasfpu = 0;
        cpu_c.fpu_type = FuzzFpu; // G4.0 — pendant de h_set_fpu
        AMSTRAD = TANDY = PCI = MCA = 0;

        // LE VRAI cpu_set(), sur la table de la machine (M16, étape 6) — pendant exact de
        // h_reset() côté oracle. Il remplace cpu_config_286(), qui en recopiait les valeurs
        // pour cpus_286[0] : les mêmes à l'indice 0, et `fuzz --core 286 --cpu N` exerce
        // désormais les autres entrées. resetx86() lit la table par cpu_update_waitstates().
        Models.model_c.model = Models.model_c.model_get_model_from_internal_name(machine);
        cpu_c.cpu_manufacturer = 0;
        cpu_c.cpu = FuzzCpu;
        cpu_c.cpu_set();

        // Posé aussi côté oracle sans condition de cœur (harness.c:331). exec386 fait
        // `tsc += ins_cycles` sans multiplicateur, donc c'est probablement inerte ici —
        // mais « probablement inerte » est exactement ce qu'on disait des vingt
        // timing_* à zéro. Les deux côtés portent la même valeur, point.
        _808x.xt_cpu_multi = (uint64_t)((14318184.0 * (double)(1UL << 32)) / 4772728.0);

        timer.tsc = 0;
        timer.timer_target = 0x7FFFFFFF;

        // Même raison que ResetTimingState pour le 8088.
        prefetch_reset();

        _808x.resetx86();
        _808x.ResetTimingState();
        _808x.FigerGarde(); // l'accélération du 4 octobre : la garde de la carte de 1 Mo, après resetx86
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
    /// <summary>Pendant de h_seg_clear_residue(). DEVIATION du harnais, pas du cœur.
    ///
    /// limit_raw et checked sont les DEUX SEULS champs de x86seg (x86.h:37-45) que
    /// seg_reset() ne remet pas — relevé en énumérant la struct contre son corps, pas
    /// en attendant que le diff les sorte un par un. PCem ne les remet jamais, et ça ne
    /// lui coûte rien parce qu'il n'amorce qu'une fois par processus ; le diff amorce
    /// deux fois. Voir harness.c pour le raisonnement complet.
    ///
    /// SANS APPELANT AUJOURD'HUI HORS DE BootDiff, et il faut qu'il en reste ainsi :
    /// appelé depuis le chemin produit, il effacerait un champ que le mode protégé
    /// vient d'écrire.</summary>
    internal static void ClearSegResidue()
    {
        cpu_state.seg_cs.limit_raw = 0;
        cpu_state.seg_cs.@checked = 0;
        cpu_state.seg_ds.limit_raw = 0;
        cpu_state.seg_ds.@checked = 0;
        cpu_state.seg_es.limit_raw = 0;
        cpu_state.seg_es.@checked = 0;
        cpu_state.seg_fs.limit_raw = 0;
        cpu_state.seg_fs.@checked = 0;
        cpu_state.seg_gs.limit_raw = 0;
        cpu_state.seg_gs.@checked = 0;
        cpu_state.seg_ss.limit_raw = 0;
        cpu_state.seg_ss.@checked = 0;

        // gdt, ldt ET tr : resetx86() n'en pose AUCUN champ — il ne cite que idt. Leur
        // valeur d'un amorçage propre est donc zéro PARTOUT, mesuré à A1b où les trois
        // s'accordaient à zéro là où idt.limit portait 0xFFFF. Et ce sont des x86seg
        // comme les autres, donc ils ont limit_raw et checked aussi : c'est
        // LDT.limit_raw qui a sorti ce cas, après que gdt.base et ldt.base aient été
        // traités. Vider champ par champ invitait à en oublier un — on vide tout.
        ClearAll(gdt);
        ClearAll(ldt);
        ClearAll(tr);

        // idt, lui, EST initialisé par resetx86 : base = 0 et limit = 0xFFFF sur un
        // 286. On ne touche donc que ses deux champs restants — écraser sa limite
        // serait effacer une valeur juste.
        idt.limit_raw = 0;
        idt.@checked = 0;

        // ET LE RESTE DE h_state QUE resetx86() NE CITE PAS.
        //
        // Énuméré sur la struct h_state champ par champ, en cochant ceux que resetx86,
        // x86seg_reset, ResetTimingState, ResetCounters ou prefetch_reset posent déjà.
        // C'était la seule façon d'arrêter de les découvrir un par un : limit_raw, puis
        // checked, puis gdt.base, puis ldt.limit_raw, puis flags_op — cinq tours de
        // diff pour cinq champs de la MÊME famille.
        //
        // Pour flags_op, zéro EST la valeur juste et pas seulement la valeur neuve :
        // c'est FLAGS_UNKNOWN, « aucun drapeau paresseux en attente ».
        cr2 = 0;
        cr3 = 0;
        Array.Clear(dr);
        cpl_override = 0;
        cpu_state.flags_op = 0;
        cpu_state.flags_res = 0;
        cpu_state.flags_op1 = 0;
        cpu_state.flags_op2 = 0;
        cpu_state.oldpc = 0;
        cpu_state.eaaddr = 0;
        cpu_state.ssegs = 0;
        cpu_state.abrt = 0;

        // LES SIX GLOBALES DU MODE PROTÉGÉ, pendant de h_seg_clear_residue (harness.c,
        // C7a). Elles manquaient ici : invisible tant que seul boot-diff l'appelait, sur
        // des machines qui n'entrent jamais en mode protégé. pm-fuzz (G2 D5) enchaîne les
        // itérations dans un processus : mesuré, `intgatesize : oracle 0, C# 32` au pas 0
        // de l'itération qui suit un pmodeint.
        abrt_error = 0;
        x86seg_c.intgatesize = 0;
        cgate16 = 0;
        optype = 0;
        oldcpl = 0;
        cpu_cur_status = 0;

        _808x.ClearFpuResidue(); // G4.0 — pendant de l'appel dans h_seg_clear_residue
    }

    /// <summary>Les neuf champs d'un x86seg à zéro. Pour les descripteurs que
    /// resetx86() n'initialise pas du tout.</summary>
    private static void ClearAll(x86seg s)
    {
        s.@base = 0;
        s.limit = 0;
        s.limit_raw = 0;
        s.access = 0;
        s.access2 = 0;
        s.seg = 0;
        s.limit_low = 0;
        s.limit_high = 0;
        s.@checked = 0;
    }

    /// <summary>Une instruction, SANS toucher timer_target. Pendant de la boucle tracée
    /// de h_runpc (harness.c), et PAS de h_step286.
    ///
    /// LA DIFFÉRENCE EST LE TEMPS QUE VOIT L'INVITÉ. Step286 pose timer_target à tsc
    /// pour forcer cycle_period à 1, ce qui déclenche un timer_process() par pas — une
    /// DEVIATION assumée, nécessaire au pas-à-pas où un pas doit valoir exactement une
    /// instruction. La boucle tracée de h_runpc, elle, ne le fait pas : elle veut
    /// l'amorçage tel qu'il se déroule.
    ///
    /// LES DEUX PHASES DU boot-diff N'EMPRUNTENT DONC PAS LE MÊME PAS, et c'est ce qui
    /// a produit une incohérence lisible : la phase 1 annonçait une divergence à
    /// l'instruction 2 que la phase 2 ne retrouvait pas — « les états concordent à
    /// l'index signalé ». Ce n'était pas le cœur mais deux steppers différents.</summary>
    internal static int Step286Trace()
    {
        cycles = 1;
        exec386(0);
        _808x.ins_count++;
        return 1 - cycles;
    }

    internal static int Step286()
    {
        var savedTarget = timer.timer_target;

        cycles = 1;
        timer.timer_target = (uint32_t)timer.tsc;
        exec386(0);
        timer.timer_target = savedTarget;

        // Le pendant de `h_ins_count++` dans h_step286. Ce compteur EST dans le
        // vecteur comparé : l'oublier fait rougir le fuzzeur dès la première
        // instruction, sur un champ qui n'a rien à voir avec le handler testé.
        _808x.ins_count++;

        return 1 - cycles;
    }
}
