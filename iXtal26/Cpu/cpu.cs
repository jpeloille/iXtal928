// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/cpu/cpu.c + includes/private/cpu/cpu.h
// STATUS: partial — cpu_set() réduit aux familles que les tables du dépôt portent :
//         8088, 286 (M16), 386SX (G2, D0.2 — table ops_386 et temps) ;
//         cpu_update_waitstates() entier ; cpu_get_speed() et cpu_set_turbo() ; depuis
//         G4.1, fpu_get_type, fpu_get_internal_name, les tables d'échappement du x87
//         (provisoires, voir 386_ops_fpu.cs) et le choix de la table de temps. Le 486 et
//         au-delà, dynarec, MSR, Cyrix sont omis, bloc par bloc, sur place.
//
// L'oracle lie cpu.c (tools/oracle/Makefile) et fait tourner le VRAI cpu_set() de
// PCem depuis M16 : ce fichier est donc vérifié contre lui par l'empreinte CPU de
// tools/iXtal26.Diff (cpu-config-check), et non contre une recopie de ses règles.

using iXtal26.Models;

namespace iXtal26.Cpu;

internal static partial class cpu_c
{
    // pcem: cpu.h:9-50 — les types de CPU que nomment les tables du dépôt ET les
    // expressions verbatim de cpu_set() (is486, cpu_iscyrix, cpu_16bitbus) et de
    // cpu_update_waitstates().
    /*808x class CPUs*/
    internal const int CPU_8088 = 0;
    internal const int CPU_8086 = 1;

    /*286 class CPUs*/
    internal const int CPU_286 = 2;

    /*386 class CPUs*/
    internal const int CPU_386SX = 3;
    internal const int CPU_386DX = 4;
    internal const int CPU_486SLC = 5;
    internal const int CPU_486DLC = 6;

    /*486 class CPUs*/
    internal const int CPU_i486SX = 7;
    internal const int CPU_Cx486S = 9;
    internal const int CPU_i486DX = 10;  // G6.1
    internal const int CPU_Cx486DX = 12;
    internal const int CPU_iDX4 = 13;    // G6.1
    internal const int CPU_Cx5x86 = 14;

    /*586 class CPUs*/
    internal const int CPU_PENTIUM = 17; // G6.1 — les Pentium OverDrive de cpus_i486
    internal const int CPU_Cx6x86 = 19;
    internal const int CPU_Cx6x86MX = 20;
    internal const int CPU_Cx6x86L = 21;
    internal const int CPU_CxGX1 = 22;
    // omitted: les treize autres types (CPU_WINCHIP … CPU_CYRIX_III, cpu.h:24-50, hors ceux ci-dessus) —
    //   aucune table du dépôt ne les porte, et aucune expression transcrite ne les nomme.

    // pcem: cpu.h:6 et cpu.h:52 — le FABRICANT, et il n'est pas decoratif :
    // opAAD et opAAM s'en servent pour decider si la base d'un AAD/AAM non
    // standard est respectee (Intel) ou forcee a 10 (les autres). Nul par
    // defaut, donc MANU_INTEL, et c'est ce que cpu_set() pose pour les deux familles.
    internal const int MANU_INTEL = 0;

    // pcem: cpu.h:72 — enum { FPU_NONE, FPU_8087, FPU_287, FPU_287XL, FPU_387, FPU_BUILTIN }.
    internal const int FPU_NONE = 0;
    internal const int FPU_8087 = 1;
    internal const int FPU_287 = 2;
    internal const int FPU_287XL = 3;
    internal const int FPU_387 = 4;
    internal const int FPU_BUILTIN = 5;

    // pcem: cpu.c:11 — posé par loadconfig depuis la clé `fpu` (pc.c:655-656, pc.cs depuis
    // G4.1) : `none` par défaut, donc FPU_NONE et hasfpu nul.
    internal static int fpu_type;

    // pcem: cpu.c:128-138
    internal static int fpu_get_type(int model, int manu, int cpu, string internal_name)
    {
        var cpu_s = model_c.models[model].cpu[manu].cpus![cpu];
        var fpus = cpu_s.fpus!;
        var fpu_type = fpus[0].type;
        var c = 0;

        while (fpus[c].internal_name != null)
        {
                if (internal_name == fpus[c].internal_name)
                        fpu_type = fpus[c].type;
                c++;
        }

        return fpu_type;
    }

    // pcem: cpu.c:140-152
    internal static string? fpu_get_internal_name(int model, int manu, int cpu, int type)
    {
        var cpu_s = model_c.models[model].cpu[manu].cpus![cpu];
        var fpus = cpu_s.fpus!;
        var c = 0;

        while (fpus[c].internal_name != null)
        {
                if (fpus[c].type == type)
                        return fpus[c].internal_name;
                c++;
        }

        return fpus[0].internal_name;
    }

    // omitted: fpu_get_name_from_index et fpu_get_type_from_index (cpu.c:154-170) — la liste de l'interface wx/qt.

    // pcem: cpu.c:14-15
    private static int cpu_turbo_speed, cpu_nonturbo_speed;
    private static int cpu_turbo = 1;

    // pcem: cpu.c:82
    // DEVIATION: `int cpu = 3` chez PCem, qui passe TOUJOURS par loadconfig avant
    //   cpu_set() — et le défaut de la clé y est 0 (pc.c:654). Ici plusieurs points
    //   d'entrée n'ont pas de fichier de configuration (--boot, --timer-check, les
    //   outils Diff) : le 3 atteindrait cpu_set() et démarrerait un 8088/10 ou un
    //   286/12 en silence. Même arbitrage que DEFAULT_RAM (model.cs).
    internal static int cpu = 0, cpu_manufacturer = 0;

    // pcem: cpu.c:83, :168
    internal static CPU? cpu_s;

    // pcem: cpu.h:111 — lu par les branches DIV et IDIV des groupes F6 et F7,
    // qui posent les drapeaux AUTREMENT sur un Cyrix. Nul sur un 286 ; la
    // branche `!cpu_iscyrix` est donc toujours prise, et la porter garde la
    // structure de PCem lisible.
    internal static int cpu_iscyrix;

    // pcem: cpu.c:87 — posé par cpu_set() : rspeed / multi.
    internal static int cpu_busspeed;

    // pcem: cpu.h — lu par les handlers REP pour choisir leur budget de cycles
    // par appel : `(is386 && cpu_use_dynarec) ? 1000 : 100`. Nul ici, et is386
    // l'est aussi sur un 286, donc le budget est toujours 100.
    internal static int cpu_use_dynarec;

    // pcem: cpu.c:114, pose a 314 et 315. Un acces mal aligne coute
    // timing_misaligned cycles — ZERO sur un 286, la valeur que cpu_set() donne
    // avant le switch ; seul le Pentium (cpu.c:447) le porte a 3. Portes parce
    // que readmemll et writememll les lisent, pas parce qu'ils mordent ici.
    internal static int timing_misaligned;
    internal static int cpu_cyrix_alignment;

    // pcem: cpu.c:95-96 — l'override d'états d'attente (clé `cpu_waitstates`, pc.c:658,
    // non lue ici) et les deux caches, que rien dans l'arbre porté n'active. Tous à
    // zéro : cpu_update_waitstates() prend donc la branche « memory timings ».
    internal static int cpu_waitstates;
    internal static int cpu_cache_int_enabled, cpu_cache_ext_enabled;

    // pcem: cpu.c:17 — posé par cpu_set() : atclk_div de l'entrée. Le coût d'un accès
    // au bus ISA, en cycles CPU, que ISA_CYCLES(x) multiplie.
    internal static int isa_cycles;

    // pcem: cpu.c:20 — présence d'un coprocesseur, posée par cpu_set() depuis fpu_type.
    // Le PPI du 5150 la rapporte au BIOS via les interrupteurs DIP (port 0x62).
    internal static int hasfpu = 0;

    // pcem: cpu.h:121, :127-128 et cpu.c:12 — la seule caractéristique que lit l'arbre
    // porté : MOV CRx (G2, D4) n'accepte CR4 que si elle est présente. cpu_set() ne pose
    // cpu_features qu'au 486 et au-delà (cpu.c:484 et suivantes) ; sur un 386 elle reste
    // à zéro, et CR4 est un registre inexistant.
    internal const int CPU_FEATURE_CR4 = 1 << 3;
    internal const int CPU_FEATURE_VME = 1 << 4; // pcem: cpu.h:122 — G6.1, l'iDX4

    // pcem: cpu.h:137-138 — drapeaux des tables (dynarec seulement : aucun lecteur ici).
    internal const int CPU_SUPPORTS_DYNAREC = 1;
    internal const int CPU_REQUIRES_DYNAREC = 2;

    // pcem: cpu.c:68-69 — les bits d'EDX de CPUID que lisent les 486.
    private const uint32_t CPUID_FPU = 1 << 0;
    private const uint32_t CPUID_VME = 1 << 1;

    // pcem: cpu.c — G6.1. cpu_multi : le multiplicateur du 486 (DX2, DX4) ; has_vlb : le bus
    // local VESA, que la carte VLB de G7 lira (vid_cl5429.c:420).
    internal static int cpu_multi;
    internal static int has_vlb;
    internal static uint32_t cpu_features;
    internal static int cpu_has_feature(int feature) => (int)(cpu_features & (uint32_t)feature);

    // pcem: cpu.c:2050-2063 — le bit turbo du port 0x61 sur les clones XT, et le
    // cpu_set_turbo(1) de fin de resetpchard (pc.c:439). keyboard_xt.cs ne l'appelle que
    // pour GENXT, DTKXT, AMIXT et PXXT, jamais atteints ici ; celui de resetpchard est
    // inerte, cpu_set() venant de poser cpu_turbo à 1.
    internal static void cpu_set_turbo(int turbo)
    {
        if (cpu_turbo != turbo)
        {
                cpu_turbo = turbo;

                cpu_s = model_c.models[model_c.model].cpu[cpu_manufacturer].cpus![cpu];
                if (cpu_s.cpu_type >= CPU_286)
                {
                        if (cpu_turbo != 0)
                                pit.setpitclock(cpu_turbo_speed);
                        else
                                pit.setpitclock(cpu_nonturbo_speed);
                }
                else
                        pit.setpitclock(14318184.0f);
        }
    }

    // omitted: cpu_get_turbo() et cpu_set_nonturbo_divider() (cpu.c:2065, :2073-2080) —
    //   leur seul appelant hors de cpu.c est scat.c (:905, :1083), que le dépôt ne porte
    //   pas.

    // pcem: cpu.c:2067-2071
    //
    // C'EST LE BUDGET DE CHAQUE TRANCHE (pc.c:473), et jusqu'à M16 il valait 4 772 728
    // en dur, sur TOUTES les machines et des deux côtés : un 286 recevait les cycles d'un
    // 8088 pendant que son PIT comptait à 6 MHz, et son temps s'écoulait à 79,5 % du
    // temps réel (VERIFICATION.md § M16). Avant le premier cpu_set(), il rend 0 : tout
    // lecteur doit donc venir après initpc — c'est vérifié pour chacun (§ M16, étape 4).
    internal static int cpu_get_speed()
    {
        if (cpu_turbo != 0)
                return cpu_turbo_speed;
        return cpu_nonturbo_speed;
    }

    // pcem: cpu.h — le modèle de temps de PRÉFETCH de l'interpréteur, posé par
    // cpu_update_waitstates() (cpu.c:2010-2047). Ajoutés en A2.2a parce que
    // getpccache les ÉCRIT : c'est lui qui bascule entre le coût d'une ROM et celui
    // de la RAM, à chaque changement de page d'instruction.
    //
    // À ZÉRO pour un 8088 — mem_read_cycles vaut 0 dans cpus_8088 — et c'est le
    // comportement juste : 808x.c porte son propre modèle de préfetch dans ses
    // statiques (fetchcycles, prefetchqueue), et ne lit aucun de ces symboles. Ils
    // ne mordent que sur le 286, où PREFETCH_RUN est gardé par
    // `if (cpu_prefetch_cycles)` (386_dynarec.c:210).
    internal static int cpu_prefetch_cycles;
    internal static int cpu_mem_prefetch_cycles;
    internal static int cpu_rom_prefetch_cycles;
    internal static int cpu_prefetch_width;
    internal static int cpu_cycles_read, cpu_cycles_read_l;
    internal static int cpu_cycles_write, cpu_cycles_write_l;

    // pcem: cpu.h — le modèle de temps par CLASSE d'instruction, posé par cpu_set()
    // (cpu.c:323-353 pour le 286). Tous à ZÉRO pour un 8088, qui porte son temps
    // dans 808x.c et n'en lit aucun — 0 occurrence, mesuré.
    //
    // Les vingt de mode protégé sont là AVANT d'être lus, délibérément : à zéro des
    // deux côtés ils auraient formé un accord vide à la première instruction de mode
    // protégé, les deux cœurs d'accord sur un temps faux.
    internal static int timing_rr, timing_rm, timing_mr, timing_mm;
    internal static int timing_rml, timing_mrl, timing_mml;
    internal static int timing_bt, timing_bnt;
    internal static int timing_int, timing_int_rm, timing_int_v86;
    internal static int timing_int_pm, timing_int_pm_outer;
    internal static int timing_iret_rm, timing_iret_v86;
    internal static int timing_iret_pm, timing_iret_pm_outer;
    internal static int timing_call_rm, timing_call_pm;
    internal static int timing_call_pm_gate, timing_call_pm_gate_inner;
    internal static int timing_retf_rm, timing_retf_pm, timing_retf_pm_outer;
    internal static int timing_jmp_rm, timing_jmp_pm, timing_jmp_pm_gate;

    // pcem: cpu.c:170-353, 1128-1130
    internal static void cpu_set()
    {
        if (model_c.models[model_c.model].cpu[cpu_manufacturer].cpus == null)
        {
                /*CPU is invalid, set to default*/
                cpu_manufacturer = 0;
                cpu = 0;
        }

        cpu_s = model_c.models[model_c.model].cpu[cpu_manufacturer].cpus![cpu];

        x86.CPUID = (int)cpu_s.cpuid_model;
        // omitted: cpuspeed (cpu.c:180) — n'est lu que par saveconfig (pc.c:882).
        _808x.is8086 = (cpu_s.cpu_type > CPU_8088) ? 1 : 0;
        x86.is386 = (cpu_s.cpu_type >= CPU_386SX) ? 1 : 0;
        x86.is486 = (cpu_s.cpu_type >= CPU_i486SX) || (cpu_s.cpu_type == CPU_486SLC || cpu_s.cpu_type == CPU_486DLC) ? 1 : 0;
        hasfpu = (fpu_type != FPU_NONE) ? 1 : 0;

        cpu_iscyrix = (cpu_s.cpu_type == CPU_486SLC || cpu_s.cpu_type == CPU_486DLC || cpu_s.cpu_type == CPU_Cx486S ||
                       cpu_s.cpu_type == CPU_Cx486DX || cpu_s.cpu_type == CPU_Cx5x86 || cpu_s.cpu_type == CPU_Cx6x86 ||
                       cpu_s.cpu_type == CPU_Cx6x86MX || cpu_s.cpu_type == CPU_Cx6x86L || cpu_s.cpu_type == CPU_CxGX1) ? 1 : 0;
        x86.cpu_16bitbus = (cpu_s.cpu_type == CPU_286 || cpu_s.cpu_type == CPU_386SX || cpu_s.cpu_type == CPU_486SLC) ? 1 : 0;
        if (cpu_s.multi != 0)
                cpu_busspeed = cpu_s.rspeed / cpu_s.multi;
        cpu_multi = cpu_s.multi;
        // omitted: ccr0 à ccr6 (cpu.c:193) — les registres de configuration Cyrix (Intel seul).
        has_vlb = (cpu_s.cpu_type >= CPU_i486SX) && (cpu_s.cpu_type <= CPU_Cx5x86) ? 1 : 0;

        cpu_turbo_speed = cpu_s.rspeed;
        if (cpu_s.cpu_type < CPU_286)
                cpu_nonturbo_speed = 4772728;
        else if (cpu_s.rspeed < 8000000)
                cpu_nonturbo_speed = cpu_s.rspeed;
        else
                cpu_nonturbo_speed = 8000000;
        cpu_turbo = 1;

        cpu_update_waitstates();

        isa_cycles = cpu_s.atclk_div;

        if (cpu_s.rspeed <= 8000000)
                cpu_rom_prefetch_cycles = cpu_mem_prefetch_cycles;
        else
                cpu_rom_prefetch_cycles = cpu_s.rspeed / 1000000;

        // omitted: pci_nonburst_time / pci_burst_time (cpu.c:214-220) — pas de bus PCI.
        // omitted: les pclog (cpu.c:221, :228-229) — sorties de diagnostic seulement.
        // omitted: io_sethandler / io_removehandler(0x0022, cyrix_*) (cpu.c:223-226) —
        //   cpu_iscyrix vaut 0 sur les deux familles, et io_init() vient de vider la
        //   table (pc.c:362) : le removehandler ne retire rien.
        // pcem: cpu.c:231 — G2, D0.2. INCONDITIONNEL chez PCem, et la branche CPU_286
        // ci-dessous le REMPLACE par la table du 286 : c'est ce geste, et non un test
        // is386, qui donne au 386 et au 486 leur table. Sur un 8088 elle est posée sans
        // être lue — execx86 n'aiguille pas par elle.
        _386.x86_setopcodes(_386.ops_386, _386.ops_386_0f);
        // omitted: x86_opcodes_REPE / _REPNE / _3DNOW (cpu.c:232-237) — les deux REP sont
        //   câblées une fois pour toutes (386_ops_rep.cs:915), 3DNOW est de l'ère K6.
        // omitted: les seize tables dynarec (cpu.c:239-273) et codegen_timing_set (:274)
        //   — src/codegen/ n'est pas porté, cpu_use_dynarec vaut 0.

        if (hasfpu != 0)
        {
                // G4.1 — les tables sont posées ; leurs handlers s'arrêtent bruyamment
                // jusqu'à G4.5 (386_ops_fpu.cs, opX87NonTranscrit).
                _386.x86_opcodes_d8_a16 = _386.ops_fpu_d8_a16;
                _386.x86_opcodes_d8_a32 = _386.ops_fpu_d8_a32;
                _386.x86_opcodes_d9_a16 = _386.ops_fpu_d9_a16;
                _386.x86_opcodes_d9_a32 = _386.ops_fpu_d9_a32;
                _386.x86_opcodes_da_a16 = _386.ops_fpu_da_a16;
                _386.x86_opcodes_da_a32 = _386.ops_fpu_da_a32;
                _386.x86_opcodes_db_a16 = _386.ops_fpu_db_a16;
                _386.x86_opcodes_db_a32 = _386.ops_fpu_db_a32;
                _386.x86_opcodes_dc_a16 = _386.ops_fpu_dc_a16;
                _386.x86_opcodes_dc_a32 = _386.ops_fpu_dc_a32;
                _386.x86_opcodes_dd_a16 = _386.ops_fpu_dd_a16;
                _386.x86_opcodes_dd_a32 = _386.ops_fpu_dd_a32;
                _386.x86_opcodes_de_a16 = _386.ops_fpu_de_a16;
                _386.x86_opcodes_de_a32 = _386.ops_fpu_de_a32;
                _386.x86_opcodes_df_a16 = _386.ops_fpu_df_a16;
                _386.x86_opcodes_df_a32 = _386.ops_fpu_df_a32;
        }
        else
        {
                _386.x86_opcodes_d8_a16 = _386.ops_nofpu_a16;
                _386.x86_opcodes_d9_a16 = _386.ops_nofpu_a16;
                _386.x86_opcodes_da_a16 = _386.ops_nofpu_a16;
                _386.x86_opcodes_db_a16 = _386.ops_nofpu_a16;
                _386.x86_opcodes_dc_a16 = _386.ops_nofpu_a16;
                _386.x86_opcodes_dd_a16 = _386.ops_nofpu_a16;
                _386.x86_opcodes_de_a16 = _386.ops_nofpu_a16;
                _386.x86_opcodes_df_a16 = _386.ops_nofpu_a16;
                _386.x86_opcodes_d8_a32 = _386.ops_nofpu_a32;
                _386.x86_opcodes_d9_a32 = _386.ops_nofpu_a32;
                _386.x86_opcodes_da_a32 = _386.ops_nofpu_a32;
                _386.x86_opcodes_db_a32 = _386.ops_nofpu_a32;
                _386.x86_opcodes_dc_a32 = _386.ops_nofpu_a32;
                _386.x86_opcodes_dd_a32 = _386.ops_nofpu_a32;
                _386.x86_opcodes_de_a32 = _386.ops_nofpu_a32;
                _386.x86_opcodes_df_a32 = _386.ops_nofpu_a32;
        }

        // omitted: memset(&msr, 0, sizeof(msr)) (cpu.c:312) — les MSR du Pentium.
        timing_misaligned = 0;
        cpu_cyrix_alignment = 0;
        x86.cpu_CR4_mask = 0;

        switch (cpu_s.cpu_type)
        {
        case CPU_8088:
        case CPU_8086:
                break;

        case CPU_286:
                _386.x86_setopcodes(_386.ops_286, _386.ops_286_0f);
                timing_rr = 2;     /*register dest - register src*/
                timing_rm = 7;     /*register dest - memory src*/
                timing_mr = 7;     /*memory dest   - register src*/
                timing_mm = 7;     /*memory dest   - memory src*/
                timing_rml = 9;    /*register dest - memory src long*/
                timing_mrl = 11;   /*memory dest   - register src long*/
                timing_mml = 11;   /*memory dest   - memory src*/
                timing_bt = 7 - 3; /*branch taken*/
                timing_bnt = 3;    /*branch not taken*/
                timing_int = 0;
                timing_int_rm = 23;
                timing_int_v86 = 0;
                timing_int_pm = 40;
                timing_int_pm_outer = 78;
                timing_iret_rm = 17;
                timing_iret_v86 = 0;
                timing_iret_pm = 31;
                timing_iret_pm_outer = 55;
                timing_call_rm = 13;
                timing_call_pm = 26;
                timing_call_pm_gate = 52;
                timing_call_pm_gate_inner = 82;
                timing_retf_rm = 15;
                timing_retf_pm = 25;
                timing_retf_pm_outer = 55;
                timing_jmp_rm = 11;
                timing_jmp_pm = 23;
                timing_jmp_pm_gate = 38;
                break;

        // pcem: cpu.c:355-384 — G2, D0.2 : la table de l'ami386.
        case CPU_386SX:
                timing_rr = 2;     /*register dest - register src*/
                timing_rm = 6;     /*register dest - memory src*/
                timing_mr = 7;     /*memory dest   - register src*/
                timing_mm = 6;     /*memory dest   - memory src*/
                timing_rml = 8;    /*register dest - memory src long*/
                timing_mrl = 11;   /*memory dest   - register src long*/
                timing_mml = 10;   /*memory dest   - memory src*/
                timing_bt = 7 - 3; /*branch taken*/
                timing_bnt = 3;    /*branch not taken*/
                timing_int = 0;
                timing_int_rm = 37;
                timing_int_v86 = 59;
                timing_int_pm = 99;
                timing_int_pm_outer = 119;
                timing_iret_rm = 22;
                timing_iret_v86 = 60;
                timing_iret_pm = 38;
                timing_iret_pm_outer = 82;
                timing_call_rm = 17;
                timing_call_pm = 34;
                timing_call_pm_gate = 52;
                timing_call_pm_gate_inner = 86;
                timing_retf_rm = 18;
                timing_retf_pm = 32;
                timing_retf_pm_outer = 68;
                timing_jmp_rm = 12;
                timing_jmp_pm = 27;
                timing_jmp_pm_gate = 45;
                break;

        // pcem: cpu.c:386-415 — G3.2 : la table de l'ami386dx. Ne diffère du 386SX que par
        // les accès 32 bits (rml, mrl, mml), que son bus de 32 bits fait en un cycle.
        case CPU_386DX:
                timing_rr = 2;     /*register dest - register src*/
                timing_rm = 6;     /*register dest - memory src*/
                timing_mr = 7;     /*memory dest   - register src*/
                timing_mm = 6;     /*memory dest   - memory src*/
                timing_rml = 6;    /*register dest - memory src long*/
                timing_mrl = 7;    /*memory dest   - register src long*/
                timing_mml = 6;    /*memory dest   - memory src*/
                timing_bt = 7 - 3; /*branch taken*/
                timing_bnt = 3;    /*branch not taken*/
                timing_int = 0;
                timing_int_rm = 37;
                timing_int_v86 = 59;
                timing_int_pm = 99;
                timing_int_pm_outer = 119;
                timing_iret_rm = 22;
                timing_iret_v86 = 60;
                timing_iret_pm = 38;
                timing_iret_pm_outer = 82;
                timing_call_rm = 17;
                timing_call_pm = 34;
                timing_call_pm_gate = 52;
                timing_call_pm_gate_inner = 86;
                timing_retf_rm = 18;
                timing_retf_pm = 32;
                timing_retf_pm_outer = 68;
                timing_jmp_rm = 12;
                timing_jmp_pm = 27;
                timing_jmp_pm_gate = 45;
                break;

        // omitted: CPU_486SLC, CPU_486DLC (cpu.c:417-482) — cpus_486SLC / cpus_486DLC, que
        //   l'ami386 et l'ami386dx proposent, sont omises avec leurs fabricants (model.cs).

        // pcem: cpu.c:483-485 — l'iDX4 pose CR4/VME puis RETOMBE dans les i486 (pas de
        // break). cpu_features n'est remis à zéro nulle part dans cpu_set : un i486 choisi
        // après un iDX4 garde CR4 et VME.
        // pcem bug, reproduced: PB-77
        case CPU_iDX4:
                cpu_features = CPU_FEATURE_CR4 | CPU_FEATURE_VME;
                x86.cpu_CR4_mask = x86.CR4_VME | x86.CR4_PVI | x86.CR4_VME;
                goto case CPU_i486SX;
        // pcem: cpu.c:486-517
        case CPU_i486SX:
        case CPU_i486DX:
                timing_rr = 1; /*register dest - register src*/
                timing_rm = 2; /*register dest - memory src*/
                timing_mr = 3; /*memory dest   - register src*/
                timing_mm = 3;
                timing_rml = 2; /*register dest - memory src long*/
                timing_mrl = 3; /*memory dest   - register src long*/
                timing_mml = 3;
                timing_bt = 3 - 1; /*branch taken*/
                timing_bnt = 1;    /*branch not taken*/
                timing_int = 4;
                timing_int_rm = 26;
                timing_int_v86 = 82;
                timing_int_pm = 44;
                timing_int_pm_outer = 71;
                timing_iret_rm = 15;
                timing_iret_v86 = 36; /*unknown*/
                timing_iret_pm = 20;
                timing_iret_pm_outer = 36;
                timing_call_rm = 18;
                timing_call_pm = 20;
                timing_call_pm_gate = 35;
                timing_call_pm_gate_inner = 69;
                timing_retf_rm = 13;
                timing_retf_pm = 17;
                timing_retf_pm_outer = 35;
                timing_jmp_rm = 17;
                timing_jmp_pm = 19;
                timing_jmp_pm_gate = 32;
                timing_misaligned = 3;
                break;

        // omitted: CPU_Am486SX, CPU_Am486DX (cpu.c:519-551) — Intel seul (décision de Julien,
        //   1er octobre 2026, PLAN-G6.md n° 1) : cpus_Am486 n'est pas transcrite.
        // omitted: CPU_Cx486S à CPU_CYRIX_III (cpu.c:553-1126) — Intel seul (décision n° 1) pour
        //   les Cyrix, et des 586 et au-delà pour les autres. Les Pentium OverDrive de
        //   cpus_i486 tombent dans le default : arrêt « non transcrit », pas de vert muet.

        default:
                pc.fatal($"cpu_set : unknown CPU type {cpu_s.cpu_type}\n");
                break;
        }

        // pcem: cpu.c:1132-1152 — G4.1.
        switch (fpu_type)
        {
        case FPU_NONE:
                break;

        case FPU_8087:
                x87_timings_c.x87_timings = x87_timings_c.x87_timings_8087;
                break;

        case FPU_287:
                x87_timings_c.x87_timings = x87_timings_c.x87_timings_287;
                break;

        case FPU_287XL:
        case FPU_387:
                x87_timings_c.x87_timings = x87_timings_c.x87_timings_387;
                break;

        default:
                x87_timings_c.x87_timings = x87_timings_c.x87_timings_486;
                break;
        }
    }

    // pcem: cpu.c:2010-2048
    internal static void cpu_update_waitstates()
    {
        cpu_s = model_c.models[model_c.model].cpu[cpu_manufacturer].cpus![cpu];

        if (x86.is486 != 0)
                cpu_prefetch_width = 16;
        else
                cpu_prefetch_width = x86.cpu_16bitbus != 0 ? 2 : 4;

        if (cpu_cache_int_enabled != 0)
        {
                /* Disable prefetch emulation */
                cpu_prefetch_cycles = 0;
        }
        else if (cpu_waitstates != 0 && (cpu_s.cpu_type >= CPU_286 && cpu_s.cpu_type <= CPU_386DX))
        {
                /* Waitstates override */
                cpu_prefetch_cycles = cpu_waitstates + 1;
                cpu_cycles_read = cpu_waitstates + 1;
                cpu_cycles_read_l = (x86.cpu_16bitbus != 0 ? 2 : 1) * (cpu_waitstates + 1);
                cpu_cycles_write = cpu_waitstates + 1;
                cpu_cycles_write_l = (x86.cpu_16bitbus != 0 ? 2 : 1) * (cpu_waitstates + 1);
        }
        else if (cpu_cache_ext_enabled != 0)
        {
                /* Use cache timings */
                cpu_prefetch_cycles = cpu_s.cache_read_cycles;
                cpu_cycles_read = cpu_s.cache_read_cycles;
                cpu_cycles_read_l = (x86.cpu_16bitbus != 0 ? 2 : 1) * cpu_s.cache_read_cycles;
                cpu_cycles_write = cpu_s.cache_write_cycles;
                cpu_cycles_write_l = (x86.cpu_16bitbus != 0 ? 2 : 1) * cpu_s.cache_write_cycles;
        }
        else
        {
                /* Use memory timings */
                cpu_prefetch_cycles = cpu_s.mem_read_cycles;
                cpu_cycles_read = cpu_s.mem_read_cycles;
                cpu_cycles_read_l = (x86.cpu_16bitbus != 0 ? 2 : 1) * cpu_s.mem_read_cycles;
                cpu_cycles_write = cpu_s.mem_write_cycles;
                cpu_cycles_write_l = (x86.cpu_16bitbus != 0 ? 2 : 1) * cpu_s.mem_write_cycles;
        }
        if (x86.is486 != 0)
                cpu_prefetch_cycles = (cpu_prefetch_cycles * 11) / 16;
        cpu_mem_prefetch_cycles = cpu_prefetch_cycles;
        if (cpu_s.rspeed <= 8000000)
                cpu_rom_prefetch_cycles = cpu_mem_prefetch_cycles;
    }

    // pcem: cpu.c:1155-1184 — cpu_CPUID, les 486 d'Intel. Lu par opCPUID seulement si
    // CPUID != 0 : l'iDX4 (cpuid_model).
    internal static void cpu_CPUID()
    {
        switch (model_c.models[model_c.model].cpu[cpu_manufacturer].cpus![cpu].cpu_type)
        {
        case CPU_i486DX:
                if (x86.EAX == 0)
                {
                        x86.EAX = 0x00000001;
                        x86.EBX = 0x756e6547;
                        x86.EDX = 0x49656e69;
                        x86.ECX = 0x6c65746e;
                }
                else if (x86.EAX == 1)
                {
                        x86.EAX = (uint32_t)x86.CPUID;
                        x86.EBX = x86.ECX = 0;
                        x86.EDX = CPUID_FPU; /*FPU*/
                }
                else
                        x86.EAX = x86.EBX = x86.ECX = x86.EDX = 0;
                break;

        case CPU_iDX4:
                if (x86.EAX == 0)
                {
                        x86.EAX = 0x00000001;
                        x86.EBX = 0x756e6547;
                        x86.EDX = 0x49656e69;
                        x86.ECX = 0x6c65746e;
                }
                else if (x86.EAX == 1)
                {
                        x86.EAX = (uint32_t)x86.CPUID;
                        x86.EBX = x86.ECX = 0;
                        x86.EDX = CPUID_FPU | CPUID_VME;
                }
                else
                        x86.EAX = x86.EBX = x86.ECX = x86.EDX = 0;
                break;

        // omitted: CPU_Am486SX, CPU_Am486DX (cpu.c:1185-1211) — Intel seul (décision n° 1).
        // omitted: CPU_WINCHIP à CPU_CYRIX_III (cpu.c:1212-1736) — 586 et au-delà, dont le
        //   Pentium OverDrive de cpus_i486, que cpu_set arrête déjà.
        default:
                pc.fatal("not implemented: cpu.c:1211 — cpu_CPUID au-delà du 486\n");
                break;
        }
    }
}
