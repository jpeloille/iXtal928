// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/includes/private/cpu/x86_ops_fpu.h  (lignes 3-33) et
//         pcem-dev/includes/private/cpu/x87_ops.h  (op_nofpu_a16, :278-286)
// SHA256: voir oracle.tsv ; vérifier avec tools/check-oracle.sh
// STATUS: partial — A11 : les neuf emplacements qu'un 286 atteint. Les formes
//         `_a32` restent dehors, et TOUT x87_ops.h au-delà d'op_nofpu_a16 —
//         c'est-à-dire le coprocesseur lui-même — reste hors du jalon.
//
// UN 286 NU N'A PAS DE COPROCESSEUR, ET C'EST CE QUI REND CE GROUPE COURT.
//
// Les huit ESCAPE ne font qu'indexer une table : x86_opcodes_d8_a16 et ses
// sept sœurs. Quand hasfpu est nul — ce que Reset286 pose — cpu_set() les
// dirige TOUTES vers ops_nofpu_a16, 256 copies du même handler. L'instruction
// ne calcule donc rien : elle lève INT 7 si le bit d'émulation est posé, sinon
// elle décode son adresse effective et rend la main.
//
// LE HARNAIS NE POSAIT PAS CES HUIT POINTEURS. Ils sont des globales de cpu.c,
// donc nuls au départ, et opESCAPE_d8_a16 fait
// `x86_opcodes_d8_a16[...](fetchdat)` — le premier D8 aurait fait sauter
// l'oracle sur un déréférencement nul. Même classe de défaut que les vingt
// timing_* à zéro trouvés à A2.0 : lier cpu.c fournit les SYMBOLES, cpu_set()
// pose les VALEURS, et cpu_set() ne tournait pas ici avant M16. Corrigé alors dans
// h_cpu_config_286 (harness_stubs.c) ET dans cpu_config_286 (386.State.cs), deux
// recopies symétriques. Depuis M16 les deux côtés font tourner cpu_set() (Cpu/cpu.cs
// et le vrai de PCem), qui pose les mêmes huit pointeurs, et les recopies ont disparu.
//
// DEUX INDEXATIONS DIFFÉRENTES, et ce n'est pas une coquille : D8 et DC lisent
// `(fetchdat >> 3) & 0x1f`, les six autres `fetchdat & 0xff`. Sans
// coprocesseur la table est uniforme, donc l'écart est inerte — mais le
// transcrire garde la structure vraie pour le jour où elle cessera de l'être.

using static iXtal26.Cpu._386_common;
using static iXtal26.Cpu.x86;

namespace iXtal26.Cpu;

internal static partial class _386
{
    /// <summary>pcem: x87_ops.h:278-286 — op_nofpu_a16, et les tables
    /// ops_nofpu_a16 / ops_nofpu_a32 qui en sont 256 copies.
    ///
    /// `cr0 & 0xc` teste MP et EM ensemble : si l'un des deux est posé, une
    /// instruction coprocesseur lève INT 7 — « coprocesseur non disponible ».
    /// Sinon elle décode son adresse effective pour faire avancer pc du bon
    /// nombre d'octets, et ne fait rien d'autre.</summary>
    private static int op_nofpu_a16(uint32_t fetchdat)
    {
        if ((cr0 & 0xc) != 0)
        {
                x86_int(7);
                return 1;
        }
        else
        {
                if (fetch_ea_16(fetchdat)) return 1;
                return 0;
        }
    }

    internal static readonly OpFn[] ops_nofpu_a16 = BuildNofpu();

    private static OpFn[] BuildNofpu()
    {
        var t = new OpFn[256];
        for (var i = 0; i < 256; i++)
                t[i] = op_nofpu_a16;
        return t;
    }

    // Les huit tables d'échappement, pendant de cpu.c:293-309.
    internal static OpFn[]? x86_opcodes_d8_a16, x86_opcodes_d9_a16, x86_opcodes_da_a16,
                            x86_opcodes_db_a16, x86_opcodes_dc_a16, x86_opcodes_dd_a16,
                            x86_opcodes_de_a16, x86_opcodes_df_a16;

    // pcem: x86_ops_fpu.h:3-24 — les huit opESCAPE_*_a16.
    private static int opESCAPE_d8_a16(uint32_t fetchdat) => x86_opcodes_d8_a16![(fetchdat >> 3) & 0x1f](fetchdat);
    private static int opESCAPE_d9_a16(uint32_t fetchdat) => x86_opcodes_d9_a16![fetchdat & 0xff](fetchdat);
    private static int opESCAPE_da_a16(uint32_t fetchdat) => x86_opcodes_da_a16![fetchdat & 0xff](fetchdat);
    private static int opESCAPE_db_a16(uint32_t fetchdat) => x86_opcodes_db_a16![fetchdat & 0xff](fetchdat);
    private static int opESCAPE_dc_a16(uint32_t fetchdat) => x86_opcodes_dc_a16![(fetchdat >> 3) & 0x1f](fetchdat);
    private static int opESCAPE_dd_a16(uint32_t fetchdat) => x86_opcodes_dd_a16![fetchdat & 0xff](fetchdat);
    private static int opESCAPE_de_a16(uint32_t fetchdat) => x86_opcodes_de_a16![fetchdat & 0xff](fetchdat);
    private static int opESCAPE_df_a16(uint32_t fetchdat) => x86_opcodes_df_a16![fetchdat & 0xff](fetchdat);

    // pcem: x86_ops_fpu.h:27-33 — opWAIT.
    //
    // IL N'A PAS DE PREFETCH_RUN, et c'est le seul handler de toute la table
    // dans ce cas. PCem l'écrit ainsi ; le modèle de préfetch ne voit donc pas
    // passer un WAIT. Transcrit tel quel — l'ajouter changerait prefetch_bytes,
    // qui est dans le vecteur comparé depuis A2.2d.
    private static int opWAIT(uint32_t fetchdat)
    {
        if ((cr0 & 0xa) == 0xa)
        {
                x86_int(7);
                return 1;
        }
        CLOCK_CYCLES(4);
        return 0;
    }

    // omitted: les huit opESCAPE_*_a32 — op32 nul sur un 286.
    // omitted: tout x87_ops.h au-dela d'op_nofpu_a16 — le coprocesseur lui-meme,
    //   ses quatre-vingts handlers et sa pile de registres. Un 287 est un
    //   composant OPTIONNEL de l'AT 5170 ; Reset286 pose hasfpu = 0.

    /// <summary>pcem: 9B et D8-DF — relevés sur ops_286[] par gdb.</summary>
    private static void PoserGroupeFPU()
    {
        ops_286[0x9B] = opWAIT;
        ops_286[0xD8] = opESCAPE_d8_a16;
        ops_286[0xD9] = opESCAPE_d9_a16;
        ops_286[0xDA] = opESCAPE_da_a16;
        ops_286[0xDB] = opESCAPE_db_a16;
        ops_286[0xDC] = opESCAPE_dc_a16;
        ops_286[0xDD] = opESCAPE_dd_a16;
        ops_286[0xDE] = opESCAPE_de_a16;
        ops_286[0xDF] = opESCAPE_df_a16;
    }

    // ---- G2, D2 : les formes 32 bits (_l, _a32) de x86_ops_fpu.h ----

    // pcem: x87_ops.h:287-295 — op_nofpu_a32, et ops_nofpu_a32 qui en est 256 copies.
    private static int op_nofpu_a32(uint32_t fetchdat)
    {
        if ((cr0 & 0xc) != 0)
        {
                x86_int(7);
                return 1;
        }
        else
        {
                if (fetch_ea_32(fetchdat)) return 1;
                return 0;
        }
    }

    internal static readonly OpFn[] ops_nofpu_a32 = BuildNofpu32();

    private static OpFn[] BuildNofpu32()
    {
        var t = new OpFn[256];
        for (var i = 0; i < 256; i++)
                t[i] = op_nofpu_a32;
        return t;
    }

    internal static OpFn[]? x86_opcodes_d8_a32, x86_opcodes_d9_a32, x86_opcodes_da_a32,
                            x86_opcodes_db_a32, x86_opcodes_dc_a32, x86_opcodes_dd_a32,
                            x86_opcodes_de_a32, x86_opcodes_df_a32;

    // pcem: x86_ops_fpu.h:4-25 — les huit opESCAPE_*_a32.
    private static int opESCAPE_d8_a32(uint32_t fetchdat) => x86_opcodes_d8_a32![(fetchdat >> 3) & 0x1f](fetchdat);
    private static int opESCAPE_d9_a32(uint32_t fetchdat) => x86_opcodes_d9_a32![fetchdat & 0xff](fetchdat);
    private static int opESCAPE_da_a32(uint32_t fetchdat) => x86_opcodes_da_a32![fetchdat & 0xff](fetchdat);
    private static int opESCAPE_db_a32(uint32_t fetchdat) => x86_opcodes_db_a32![fetchdat & 0xff](fetchdat);
    private static int opESCAPE_dc_a32(uint32_t fetchdat) => x86_opcodes_dc_a32![(fetchdat >> 3) & 0x1f](fetchdat);
    private static int opESCAPE_dd_a32(uint32_t fetchdat) => x86_opcodes_dd_a32![fetchdat & 0xff](fetchdat);
    private static int opESCAPE_de_a32(uint32_t fetchdat) => x86_opcodes_de_a32![fetchdat & 0xff](fetchdat);
    private static int opESCAPE_df_a32(uint32_t fetchdat) => x86_opcodes_df_a32![fetchdat & 0xff](fetchdat);

    // pcem: 386_ops.h — les emplacements de ces handlers dans OP_TABLE(386) et (386_0f).
    private static void PoserGroupe_fpu_386()
    {
        ops_386[0x2D8] = opESCAPE_d8_a32;
        ops_386[0x2D9] = opESCAPE_d9_a32;
        ops_386[0x2DA] = opESCAPE_da_a32;
        ops_386[0x2DB] = opESCAPE_db_a32;
        ops_386[0x2DC] = opESCAPE_dc_a32;
        ops_386[0x2DD] = opESCAPE_dd_a32;
        ops_386[0x2DE] = opESCAPE_de_a32;
        ops_386[0x2DF] = opESCAPE_df_a32;
        ops_386[0x3D8] = opESCAPE_d8_a32;
        ops_386[0x3D9] = opESCAPE_d9_a32;
        ops_386[0x3DA] = opESCAPE_da_a32;
        ops_386[0x3DB] = opESCAPE_db_a32;
        ops_386[0x3DC] = opESCAPE_dc_a32;
        ops_386[0x3DD] = opESCAPE_dd_a32;
        ops_386[0x3DE] = opESCAPE_de_a32;
        ops_386[0x3DF] = opESCAPE_df_a32;
    }
}
