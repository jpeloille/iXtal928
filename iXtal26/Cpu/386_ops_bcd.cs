// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/includes/private/cpu/x86_ops_bcd.h  (en entier, lignes 3-105)
// SHA256: voir oracle.tsv ; vérifier avec tools/check-oracle.sh
// STATUS: transcribed — les six handlers, tous atteignables sur un 286.
//
// L'ARITHMÉTIQUE DÉCIMALE, ET LE SEUL GROUPE QUI ÉCRIVE LES DRAPEAUX À LA MAIN.
//
// Les six posent A_FLAG et C_FLAG DIRECTEMENT dans cpu_state.flags, sans passer
// par un poseur paresseux — d'où le flags_rebuild() en tête de quatre d'entre
// eux. AAD et AAM s'en passent : ils n'ont pas besoin de l'ancien état, ils
// écrasent tout par setznp16.
//
// DAA ET DAS APPELLENT flags_rebuild DEUX FOIS, et ce n'est pas une maladresse.
// Le premier matérialise l'état d'entrée, qu'ils lisent et modifient. Puis
// setznp8 réinstalle une représentation PARESSEUSE — ce qui périmerait leur
// travail sur C et A. D'où la sauvegarde `tempw`, le second flags_rebuild qui
// effondre setznp8, et le ré-application de tempw par-dessus. Supprimer l'un
// des deux appels donnerait le bon Z et le mauvais C.
//
// LE FABRICANT COMPTE. opAAD et opAAM lisent cpu_manufacturer : sur un
// processeur Intel une base non standard est respectée, ailleurs elle est
// forcée à 10. C'est du vrai comportement documenté (AAD 0x0B existe), pas une
// bizarrerie de PCem. cpu_manufacturer vaut MANU_INTEL par défaut, donc la
// branche prise est celle d'Intel — mais la transcrire garde la structure.
//
// opAAM DIVISE PAR base ET NE TESTE PAS ZÉRO — si, il teste : `if (!base ||
// ...) base = 10;`. Sans ce test un `AAM 0` ferait une division par zéro. Il
// est là, et c'est lui qui rend l'opcode inoffensif.

using static iXtal26.Cpu._386_common;
using static iXtal26.Cpu.x86;
using static iXtal26.Cpu.x86_flags;

namespace iXtal26.Cpu;

internal static partial class _386
{
    // pcem: x86_ops_bcd.h:3-15 — opAAA
    private static int opAAA(uint32_t fetchdat)
    {
        flags_rebuild();
        if ((cpu_state.flags & A_FLAG) != 0 || ((AL & 0xF) > 9))
        {
                AL += 6;
                AH++;
                cpu_state.flags |= (A_FLAG | C_FLAG);
        }
        else
                cpu_state.flags &= unchecked((uint16_t)~(A_FLAG | C_FLAG));
        AL &= 0xF;
        CLOCK_CYCLES(is486 != 0 ? 3 : 4);
        PREFETCH_RUN(is486 != 0 ? 3 : 4, 1, -1, 0, 0, 0, 0, 0);
        return 0;
    }

    // pcem: x86_ops_bcd.h:17-27 — opAAD
    private static int opAAD(uint32_t fetchdat)
    {
        int @base = (uint8_t)fetchdat; cpu_state.pc++;   // getbytef()
        if (cpu.cpu_manufacturer != cpu.MANU_INTEL)
                @base = 10;
        AL = (uint8_t)((AH * @base) + AL);
        AH = 0;
        setznp16(AX);
        CLOCK_CYCLES(is486 != 0 ? 14 : 19);
        PREFETCH_RUN(is486 != 0 ? 14 : 19, 2, -1, 0, 0, 0, 0, 0);
        return 0;
    }

    // pcem: x86_ops_bcd.h:29-39 — opAAM. Le `!base` protège de la division
    // par zéro, et c'est lui qui rend `AAM 0` inoffensif.
    private static int opAAM(uint32_t fetchdat)
    {
        int @base = (uint8_t)fetchdat; cpu_state.pc++;
        if (@base == 0 || cpu.cpu_manufacturer != cpu.MANU_INTEL)
                @base = 10;
        AH = (uint8_t)(AL / @base);
        AL %= (uint8_t)@base;
        setznp16(AX);
        CLOCK_CYCLES(is486 != 0 ? 15 : 17);
        PREFETCH_RUN(is486 != 0 ? 15 : 17, 2, -1, 0, 0, 0, 0, 0);
        return 0;
    }

    // pcem: x86_ops_bcd.h:41-53 — opAAS
    private static int opAAS(uint32_t fetchdat)
    {
        flags_rebuild();
        if ((cpu_state.flags & A_FLAG) != 0 || ((AL & 0xF) > 9))
        {
                AL -= 6;
                AH--;
                cpu_state.flags |= (A_FLAG | C_FLAG);
        }
        else
                cpu_state.flags &= unchecked((uint16_t)~(A_FLAG | C_FLAG));
        AL &= 0xF;
        CLOCK_CYCLES(is486 != 0 ? 3 : 4);
        PREFETCH_RUN(is486 != 0 ? 3 : 4, 1, -1, 0, 0, 0, 0, 0);
        return 0;
    }

    // pcem: x86_ops_bcd.h:55-79 — opDAA. Voir l'en-tête pour les DEUX
    // flags_rebuild.
    private static int opDAA(uint32_t fetchdat)
    {
        uint16_t tempw;

        flags_rebuild();
        if ((cpu_state.flags & A_FLAG) != 0 || ((AL & 0xf) > 9))
        {
                int tempi = ((uint16_t)AL) + 6;
                AL += 6;
                cpu_state.flags |= A_FLAG;
                if ((tempi & 0x100) != 0)
                        cpu_state.flags |= C_FLAG;
        }
        if ((cpu_state.flags & C_FLAG) != 0 || (AL > 0x9f))
        {
                AL += 0x60;
                cpu_state.flags |= C_FLAG;
        }

        tempw = (uint16_t)(cpu_state.flags & (C_FLAG | A_FLAG));
        setznp8(AL);
        flags_rebuild();
        cpu_state.flags |= tempw;
        CLOCK_CYCLES(4);
        PREFETCH_RUN(4, 1, -1, 0, 0, 0, 0, 0);

        return 0;
    }

    // pcem: x86_ops_bcd.h:81-105 — opDAS
    private static int opDAS(uint32_t fetchdat)
    {
        uint16_t tempw;

        flags_rebuild();
        if ((cpu_state.flags & A_FLAG) != 0 || ((AL & 0xf) > 9))
        {
                int tempi = ((uint16_t)AL) - 6;
                AL -= 6;
                cpu_state.flags |= A_FLAG;
                if ((tempi & 0x100) != 0)
                        cpu_state.flags |= C_FLAG;
        }
        if ((cpu_state.flags & C_FLAG) != 0 || (AL > 0x9f))
        {
                AL -= 0x60;
                cpu_state.flags |= C_FLAG;
        }

        tempw = (uint16_t)(cpu_state.flags & (C_FLAG | A_FLAG));
        setznp8(AL);
        flags_rebuild();
        cpu_state.flags |= tempw;
        CLOCK_CYCLES(4);
        PREFETCH_RUN(4, 1, -1, 0, 0, 0, 0, 0);

        return 0;
    }

    /// <summary>pcem: 27, 2F, 37, 3F, D4, D5 — relevés sur ops_286[] par gdb.
    /// L'ordre de la table n'est pas celui du fichier C : DAA, DAS, AAA, AAS
    /// occupent 27, 2F, 37, 3F — un par bloc de huit, dans les trous que la
    /// bande ALU laisse.</summary>
    private static void PoserGroupeBCD()
    {
        ops_286[0x27] = opDAA;
        ops_286[0x2F] = opDAS;
        ops_286[0x37] = opAAA;
        ops_286[0x3F] = opAAS;
        ops_286[0xD4] = opAAM;
        ops_286[0xD5] = opAAD;
    }
}
