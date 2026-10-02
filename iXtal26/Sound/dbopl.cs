// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/dosbox/dbopl.cpp + includes/private/dosbox/dbopl.h
// STATUS: partial — G8.0 : les constantes et les tables statiques de DBOPL, et InitTables en mode
//         WAVE_TABLEMUL (dbopl.h:46), SAUF ChanOffsetTable et OpOffsetTable (des offsets
//         d'octets tirés de la disposition mémoire du C++, qui deviennent des indices en G8.1).
//         Operator, Channel et Chip entrent en G8.1. Les branches WAVE_HANDLER et
//         WAVE_TABLELOG (ExpTable, SinTable, WaveForm0-7) sont omises : ce mode ne les compile pas.

// CS8981 : `DBOPL` est le nom de l'espace de noms C++ ; les tables gardent leurs noms.
namespace iXtal26.Sound;

internal static partial class DBOPL
{
    // pcem: dbopl.cpp:43-45 (PI, :44)
    private const double PI = 3.14159265358979323846;

    // pcem: dbopl.cpp:50 — 52
    internal const int TREMOLO_TABLE = 52;

    // pcem: dbopl.cpp:78-89 — ENV_BITS 9 en WAVE_TABLEMUL.
    private const int ENV_BITS = 9;
    private const int ENV_EXTRA = ENV_BITS - 9;

    // pcem: dbopl.cpp:94
    private const int MUL_SH = 16;

    // pcem: dbopl.cpp:102-108
    private static readonly uint8_t[] KslCreateTable =
    {
            //0 will always be be lower than 7 * 8
            64, 32, 24, 19,
            16, 12, 11, 10,
            8, 6, 5, 4,
            3, 2, 1, 0,
    };

    // pcem: dbopl.cpp:151 — Bit16s WaveTable[8 * 512] (WAVE_TABLEMUL).
    internal static readonly int16_t[] WaveTable = new int16_t[8 * 512];

    // pcem: dbopl.cpp:172 — Bit16u MulTable[384] (WAVE_TABLEMUL).
    internal static readonly uint16_t[] MulTable = new uint16_t[384];

    // pcem: dbopl.cpp:175-176
    internal static readonly uint8_t[] KslTable = new uint8_t[8 * 16];
    internal static readonly uint8_t[] TremoloTable = new uint8_t[TREMOLO_TABLE];

    // pcem: dbopl.cpp:1310
    private static bool doneTables = false;

    // pcem: dbopl.cpp:1311-1464 (WAVE_TABLEMUL)
    // omitted: les blocs WAVE_HANDLER / WAVE_TABLELOG (:1314-1330, :1349-1360) — hors de ce mode.
    // omitted (G8.1): ChanOffsetTable et OpOffsetTable (:1393-1428) — des offsets d'octets
    //   (reinterpret_cast d'un Chip* nul) ; le C# indexera ses canaux et opérateurs.
    internal static void InitTables()
    {
        if (doneTables)
                return;
        doneTables = true;
        //Multiplication based tables
        for (int i = 0; i < 384; i++)
        {
                int s = i * 8;
                //TODO maybe keep some of the precision errors of the original table?
                double val = (0.5 + (Math.Pow(2.0, -1.0 + (255 - s) * (1.0 / 256))) * (1 << MUL_SH));
                MulTable[i] = (uint16_t)(val);
        }

        //Sine Wave Base
        for (int i = 0; i < 512; i++)
        {
                WaveTable[0x0200 + i] = (int16_t)(Math.Sin((i + 0.5) * (PI / 512.0)) * 4084);
                WaveTable[0x0000 + i] = (int16_t)(-WaveTable[0x200 + i]);
        }
        //Exponential wave
        for (int i = 0; i < 256; i++)
        {
                WaveTable[0x700 + i] = (int16_t)(0.5 + (Math.Pow(2.0, -1.0 + (255 - i * 8) * (1.0 / 256))) * 4085);
                WaveTable[0x6ff - i] = (int16_t)(-WaveTable[0x700 + i]);
        }

        for (int i = 0; i < 256; i++)
        {
                //Fill silence gaps
                WaveTable[0x400 + i] = WaveTable[0];
                WaveTable[0x500 + i] = WaveTable[0];
                WaveTable[0x900 + i] = WaveTable[0];
                WaveTable[0xc00 + i] = WaveTable[0];
                WaveTable[0xd00 + i] = WaveTable[0];
                //Replicate sines in other pieces
                WaveTable[0x800 + i] = WaveTable[0x200 + i];
                //double speed sines
                WaveTable[0xa00 + i] = WaveTable[0x200 + i * 2];
                WaveTable[0xb00 + i] = WaveTable[0x000 + i * 2];
                WaveTable[0xe00 + i] = WaveTable[0x200 + i * 2];
                WaveTable[0xf00 + i] = WaveTable[0x200 + i * 2];
        }

        //Create the ksl table
        for (int oct = 0; oct < 8; oct++)
        {
                int @base = oct * 8;
                for (int i = 0; i < 16; i++)
                {
                        int val = @base - KslCreateTable[i];
                        if (val < 0)
                                val = 0;
                        //*4 for the final range to match attenuation range
                        KslTable[oct * 16 + i] = (uint8_t)(val * 4);
                }
        }
        //Create the Tremolo table, just increase and decrease a triangle wave
        for (uint8_t i = 0; i < TREMOLO_TABLE / 2; i++)
        {
                uint8_t val = (uint8_t)(i << ENV_EXTRA);
                TremoloTable[i] = val;
                TremoloTable[TREMOLO_TABLE - 1 - i] = val;
        }
    }
}
