// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/cpu/cpu_tables.c  (cpus_8088 :31-39, cpus_286 :68-77, cpus_ibmat :84-88,
//         cpus_i386SX :109-115)
//         + cpu_tables.c:25-29 (les cinq tables FPU, G4.1)
//         + includes/public/pcem/cpu.h:6-27 (FPU, CPU)
// STATUS: partial — cinq tables de CPU sur trente-trois, celles que les machines du
//         dépôt référencent (model.c:777, :782, :989, :1109, :1241, :1341), et les cinq
//         tables FPU.

namespace iXtal26.Cpu;

// pcem: cpu.h:6-10 — un coprocesseur proposé : son nom, son nom interne (la valeur de la
// clé `fpu`), son type FPU_*. La sentinelle `{NULL, NULL, 0}` clôt chaque table.
// DEVIATION: une CLASSE, comme CPU : les tables en gardent la référence (cpu_s->fpus).
internal sealed class FPU
{
    internal readonly string? name;
    internal readonly string? internal_name;
    internal readonly int type;

    internal FPU(string? name, string? internal_name, int type)
    {
        this.name = name;
        this.internal_name = internal_name;
        this.type = type;
    }
}

// pcem: cpu.h:12-27
// DEVIATION: une CLASSE et non une struct — cpu_s en garde l'adresse (cpu.c:177), et la
//   convention du dépôt réserve la struct C# à ce qui n'est jamais pris par adresse.
internal sealed class CPU
{
    internal readonly string name;
    internal readonly int cpu_type;
    // G4.1 — la liste des coprocesseurs proposés, lue par fpu_get_type (cpu.c:128-139).
    // Null pour les sentinelles, comme le `0` de `{"", -1, 0, …}`.
    internal readonly FPU[]? fpus;
    internal readonly int speed;
    internal readonly int rspeed;
    internal readonly int multi;
    internal readonly int pci_speed;
    internal readonly uint32_t edx_reset;
    internal readonly uint32_t cpuid_model;
    internal readonly uint16_t cyrix_id;
    internal readonly int cpu_flags;
    internal readonly int mem_read_cycles, mem_write_cycles;
    internal readonly int cache_read_cycles, cache_write_cycles;
    internal readonly int atclk_div;

    // L'ordre des paramètres est celui des champs du C, pour que chaque ligne de table
    // se relise contre cpu_tables.c colonne par colonne. Les champs au-delà de `multi`
    // ont un défaut nul : c'est ce que l'initialiseur incomplet de la sentinelle C,
    // `{"", -1, 0, 0, 0, 0}`, leur donne.
    internal CPU(string name, int cpu_type, FPU[]? fpus, int speed, int rspeed, int multi, int pci_speed = 0,
                 uint32_t edx_reset = 0, uint32_t cpuid_model = 0, uint16_t cyrix_id = 0, int cpu_flags = 0,
                 int mem_read_cycles = 0, int mem_write_cycles = 0, int cache_read_cycles = 0,
                 int cache_write_cycles = 0, int atclk_div = 0)
    {
        this.name = name;
        this.cpu_type = cpu_type;
        this.fpus = fpus;
        this.speed = speed;
        this.rspeed = rspeed;
        this.multi = multi;
        this.pci_speed = pci_speed;
        this.edx_reset = edx_reset;
        this.cpuid_model = cpuid_model;
        this.cyrix_id = cyrix_id;
        this.cpu_flags = cpu_flags;
        this.mem_read_cycles = mem_read_cycles;
        this.mem_write_cycles = mem_write_cycles;
        this.cache_read_cycles = cache_read_cycles;
        this.cache_write_cycles = cache_write_cycles;
        this.atclk_div = atclk_div;
    }
}

internal static class cpu_tables
{
    // pcem: cpu_tables.c:25-29 — G4.1.
    internal static readonly FPU[] fpus_none = {new("None", "none", FPU_NONE), new(null, null, 0)};
    internal static readonly FPU[] fpus_8088 = {new("None", "none", FPU_NONE), new("8087", "8087", FPU_8087), new(null, null, 0)};
    internal static readonly FPU[] fpus_80286 = {new("None", "none", FPU_NONE), new("287", "287", FPU_287), new("287XL", "287xl", FPU_287XL), new(null, null, 0)};
    internal static readonly FPU[] fpus_80386 = {new("None", "none", FPU_NONE), new("387", "387", FPU_387), new(null, null, 0)};
    internal static readonly FPU[] fpus_builtin = {new("Built-in", "builtin", FPU_BUILTIN), new(null, null, 0)};

    // pcem: cpu_tables.c:31-39
    // Les sentinelles `{"", -1, …}` sont gardées VERBATIM : cpu_set() tombe dessus pour un
    // indice hors table (cpu_type -1, donc le `default: fatal` de cpu.c:1128), et les
    // comptes de l'interface (wx-config.c:953-959) les parcourent jusqu'à elles.
    internal static readonly CPU[] cpus_8088 =
    {
            /*8088 standard*/
            new("8088/4.77", CPU_8088, fpus_8088, 0, 4772728, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1),
            new("8088/7.16", CPU_8088, fpus_8088, 1, 14318184 / 2, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1),
            new("8088/8", CPU_8088, fpus_8088, 1, 8000000, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1),
            new("8088/10", CPU_8088, fpus_8088, 2, 10000000, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1),
            new("8088/12", CPU_8088, fpus_8088, 3, 12000000, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1),
            new("8088/16", CPU_8088, fpus_8088, 4, 16000000, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1),
            new("", -1, null, 0, 0, 0),
    };

    // pcem: cpu_tables.c:68-77
    internal static readonly CPU[] cpus_286 =
    {
            /*286*/
            new("286/6", CPU_286, fpus_80286, 0, 6000000, 1, 0, 0, 0, 0, 0, 2, 2, 2, 2, 1),
            new("286/8", CPU_286, fpus_80286, 1, 8000000, 1, 0, 0, 0, 0, 0, 2, 2, 2, 2, 1),
            new("286/10", CPU_286, fpus_80286, 2, 10000000, 1, 0, 0, 0, 0, 0, 2, 2, 2, 2, 1),
            new("286/12", CPU_286, fpus_80286, 3, 12000000, 1, 0, 0, 0, 0, 0, 3, 3, 3, 3, 2),
            new("286/16", CPU_286, fpus_80286, 4, 16000000, 1, 0, 0, 0, 0, 0, 3, 3, 3, 3, 2),
            new("286/20", CPU_286, fpus_80286, 5, 20000000, 1, 0, 0, 0, 0, 0, 4, 4, 4, 4, 3),
            new("286/25", CPU_286, fpus_80286, 6, 25000000, 1, 0, 0, 0, 0, 0, 4, 4, 4, 4, 3),
            new("", -1, null, 0, 0, 0),
    };

    // pcem: cpu_tables.c:84-88
    internal static readonly CPU[] cpus_ibmat =
    {
            /*286*/
            new("286/6", CPU_286, fpus_80286, 0, 6000000, 1, 0, 0, 0, 0, 0, 3, 3, 3, 3, 1),
            new("286/8", CPU_286, fpus_80286, 0, 8000000, 1, 0, 0, 0, 0, 0, 3, 3, 3, 3, 1),
            new("", -1, null, 0, 0, 0),
    };

    // pcem: cpu_tables.c:109-115 — G2, D0.2 : la table Intel de l'ami386, seule machine
    // 386 du dépôt, que le fuzzeur du cœur 386 fait tourner par cpu_set().
    internal static readonly CPU[] cpus_i386SX =
    {
            /*i386SX*/
            new("i386SX/16", CPU_386SX, fpus_80386, 0, 16000000, 1, 0, 0x2308, 0, 0, 0, 3, 3, 3, 3, 2),
            new("i386SX/20", CPU_386SX, fpus_80386, 1, 20000000, 1, 0, 0x2308, 0, 0, 0, 4, 4, 3, 3, 3),
            new("i386SX/25", CPU_386SX, fpus_80386, 2, 25000000, 1, 0, 0x2308, 0, 0, 0, 4, 4, 3, 3, 3),
            new("i386SX/33", CPU_386SX, fpus_80386, 3, 33333333, 1, 0, 0x2308, 0, 0, 0, 6, 6, 3, 3, 4),
            new("", -1, null, 0, 0, 0),
    };

    // pcem: cpu_tables.c:117-123 — G3.2 : la table Intel de l'ami386dx.
    internal static readonly CPU[] cpus_i386DX =
    {
            /*i386DX*/
            new("i386DX/16", CPU_386DX, fpus_80386, 0, 16000000, 1, 0, 0x0308, 0, 0, 0, 3, 3, 3, 3, 2),
            new("i386DX/20", CPU_386DX, fpus_80386, 1, 20000000, 1, 0, 0x0308, 0, 0, 0, 4, 4, 3, 3, 3),
            new("i386DX/25", CPU_386DX, fpus_80386, 2, 25000000, 1, 0, 0x0308, 0, 0, 0, 4, 4, 3, 3, 3),
            new("i386DX/33", CPU_386DX, fpus_80386, 3, 33333333, 1, 0, 0x0308, 0, 0, 0, 6, 6, 3, 3, 4),
            new("", -1, null, 0, 0, 0),
    };

    // omitted: les vingt-huit autres tables — cpus_pcjr, cpus_europc, cpus_8086, cpus_pc1512
    //   (cpu_tables.c:41-67), cpus_super286tr (:79-83), cpus_ibmxt286 à cpus_ps2_m30_286
    //   (:90-107), et de cpus_acer à cpus_VIA_100MHz (:125-667). Aucune machine du
    //   dépôt ne les référence ; cpus_Am386SX et cpus_486SLC, que m_ami386 propose
    //   aussi, sont omis avec elle (model.cs).
    private const int CPU_8088 = cpu_c.CPU_8088;
    private const int CPU_286 = cpu_c.CPU_286;
    private const int CPU_386SX = cpu_c.CPU_386SX;
    private const int CPU_386DX = cpu_c.CPU_386DX;
    private const int FPU_NONE = cpu_c.FPU_NONE;
    private const int FPU_8087 = cpu_c.FPU_8087;
    private const int FPU_287 = cpu_c.FPU_287;
    private const int FPU_287XL = cpu_c.FPU_287XL;
    private const int FPU_387 = cpu_c.FPU_387;
    private const int FPU_BUILTIN = cpu_c.FPU_BUILTIN;
}
