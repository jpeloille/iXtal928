// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/cpu/cpu_tables.c  (cpus_8088 :31-39, cpus_286 :68-77, cpus_ibmat :84-88)
//         + includes/public/pcem/cpu.h:12-27 (CPU)
// STATUS: partial — trois tables sur trente-trois, celles que les quatre machines du
//         dépôt référencent (model.c:777, :782, :989, :1109). FPU omis.

namespace iXtal26.Cpu;

// pcem: cpu.h:12-27
// DEVIATION: une CLASSE et non une struct — cpu_s en garde l'adresse (cpu.c:177), et la
//   convention du dépôt réserve la struct C# à ce qui n'est jamais pris par adresse.
// omitted: `const FPU *fpus` — la liste des coprocesseurs proposés. La clé `fpu`
//   (pc.c:655-656) n'est pas lue et fpu_type reste FPU_NONE : aucune machine du dépôt
//   n'a de 287 (VERIFICATION.md § M8, « hasfpu »).
internal sealed class CPU
{
    internal readonly string name;
    internal readonly int cpu_type;
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
    internal CPU(string name, int cpu_type, int speed, int rspeed, int multi, int pci_speed = 0,
                 uint32_t edx_reset = 0, uint32_t cpuid_model = 0, uint16_t cyrix_id = 0, int cpu_flags = 0,
                 int mem_read_cycles = 0, int mem_write_cycles = 0, int cache_read_cycles = 0,
                 int cache_write_cycles = 0, int atclk_div = 0)
    {
        this.name = name;
        this.cpu_type = cpu_type;
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
    // pcem: cpu_tables.c:31-39
    // Les sentinelles `{"", -1, …}` sont gardées VERBATIM : cpu_set() tombe dessus pour un
    // indice hors table (cpu_type -1, donc le `default: fatal` de cpu.c:1128), et les
    // comptes de l'interface (wx-config.c:953-959) les parcourent jusqu'à elles.
    internal static readonly CPU[] cpus_8088 =
    {
            /*8088 standard*/
            new("8088/4.77", CPU_8088, 0, 4772728, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1),
            new("8088/7.16", CPU_8088, 1, 14318184 / 2, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1),
            new("8088/8", CPU_8088, 1, 8000000, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1),
            new("8088/10", CPU_8088, 2, 10000000, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1),
            new("8088/12", CPU_8088, 3, 12000000, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1),
            new("8088/16", CPU_8088, 4, 16000000, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1),
            new("", -1, 0, 0, 0),
    };

    // pcem: cpu_tables.c:68-77
    internal static readonly CPU[] cpus_286 =
    {
            /*286*/
            new("286/6", CPU_286, 0, 6000000, 1, 0, 0, 0, 0, 0, 2, 2, 2, 2, 1),
            new("286/8", CPU_286, 1, 8000000, 1, 0, 0, 0, 0, 0, 2, 2, 2, 2, 1),
            new("286/10", CPU_286, 2, 10000000, 1, 0, 0, 0, 0, 0, 2, 2, 2, 2, 1),
            new("286/12", CPU_286, 3, 12000000, 1, 0, 0, 0, 0, 0, 3, 3, 3, 3, 2),
            new("286/16", CPU_286, 4, 16000000, 1, 0, 0, 0, 0, 0, 3, 3, 3, 3, 2),
            new("286/20", CPU_286, 5, 20000000, 1, 0, 0, 0, 0, 0, 4, 4, 4, 4, 3),
            new("286/25", CPU_286, 6, 25000000, 1, 0, 0, 0, 0, 0, 4, 4, 4, 4, 3),
            new("", -1, 0, 0, 0),
    };

    // pcem: cpu_tables.c:84-88
    internal static readonly CPU[] cpus_ibmat =
    {
            /*286*/
            new("286/6", CPU_286, 0, 6000000, 1, 0, 0, 0, 0, 0, 3, 3, 3, 3, 1),
            new("286/8", CPU_286, 0, 8000000, 1, 0, 0, 0, 0, 0, 3, 3, 3, 3, 1),
            new("", -1, 0, 0, 0),
    };

    // omitted: les trente autres tables — cpus_pcjr, cpus_europc, cpus_8086, cpus_pc1512
    //   (cpu_tables.c:41-67), cpus_super286tr (:79-83), et de cpus_ibmxt286 à
    //   cpus_VIA_100MHz (:90-667). Aucune machine du dépôt ne les référence.
    // omitted: les cinq tables FPU (cpu_tables.c:25-29), avec le champ `fpus`.

    private const int CPU_8088 = cpu_c.CPU_8088;
    private const int CPU_286 = cpu_c.CPU_286;
}
