// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/includes/private/cpu/x86_ops_string.h  (les 14 handlers
//         `_a16` d'un 286, lignes 3-621)
// SHA256: voir oracle.tsv ; vérifier avec tools/check-oracle.sh
// STATUS: partial — A10 : les 14 emplacements qu'un 286 atteint. Les formes
//         `_a32` et les six `L` (MOVSL, CMPSL, STOSL, LODSL, SCASL, INSL,
//         OUTSL) restent dehors, déclarées au registre des omissions.
//         REP et REPNE (F2, F3) vivent dans x86_ops_rep.h et arrivent avec A10b.
//
// QUATORZE HANDLERS ÉCRITS À LA MAIN, sans une seule macro — et c'est ce qui
// rend le groupe dangereux à transcrire : quatorze corps presque identiques,
// où la faute se cache dans un incrément.
//
// LE MOTIF EST TOUJOURS LE MÊME : vérifier les segments, lire ou écrire,
// avancer SI et/ou DI selon D_FLAG, facturer. Ce qui varie :
//   - QUELS registres avancent. MOVS et CMPS bougent SI ET DI ; STOS et SCAS
//     seulement DI ; LODS seulement SI. INS bouge DI, OUTS bouge SI.
//   - DE COMBIEN : 1 pour les formes octet, 2 pour les formes mot.
//   - QUEL segment. Le SOURCE est cpu_state.ea_seg — donc DS par défaut, mais
//     surchargeable par un préfixe — tandis que la DESTINATION est TOUJOURS ES,
//     jamais surchargeable. C'est l'asymétrie du jeu d'instructions, et elle
//     est visible ici : `cpu_state.ea_seg` d'un côté, `cpu_state.seg_es` de
//     l'autre, littéralement.
//   - LE SENS de setsub. CMPS compare (source, destination) ; SCAS compare
//     (accumulateur, destination). Les deux posent des drapeaux, les autres non.
//
// INS ET OUTS TIRENT LE PORT D'E/S AVEC EUX, et c'est pour cela que
// check_io_perm et checkio entrent à ce jalon (386_common.cs). En mode réel
// check_io_perm ne fait rien — IOPLp y est toujours vrai — mais les quatre
// handlers la portent et l'omettre aurait fait mentir leur structure.

using iXtal26.Memory;
using static iXtal26.Cpu._386_common;
using static iXtal26.Cpu.x86;
using static iXtal26.Cpu.x86_flags;

namespace iXtal26.Cpu;

internal static partial class _386
{
    // pcem: x86_ops_string.h:3 — opMOVSB_a16
    private static int opMOVSB_a16(uint32_t fetchdat)
    {
        uint8_t temp;

        if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        if (SEG_CHECK_WRITE(cpu_state.seg_es!)) return 1;
        temp = readmemb(cpu_state.ea_seg!.@base, SI);
        if (cpu_state.abrt != 0)
                return 1;
        writememb(es, DI, temp);
        if (cpu_state.abrt != 0)
                return 1;
        if ((cpu_state.flags & D_FLAG) != 0)
        {
                DI--;
                SI--;
        }
        else
        {
                DI++;
                SI++;
        }
        CLOCK_CYCLES(7);
        PREFETCH_RUN(7, 1, -1, 1, 0, 1, 0, 0);
        return 0;
    }

    // pcem: x86_ops_string.h:48 — opMOVSW_a16
    private static int opMOVSW_a16(uint32_t fetchdat)
    {
        uint16_t temp;

        if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        if (SEG_CHECK_WRITE(cpu_state.seg_es!)) return 1;
        temp = readmemw(cpu_state.ea_seg!.@base, SI);
        if (cpu_state.abrt != 0)
                return 1;
        writememw(es, DI, temp);
        if (cpu_state.abrt != 0)
                return 1;
        if ((cpu_state.flags & D_FLAG) != 0)
        {
                DI -= 2;
                SI -= 2;
        }
        else
        {
                DI += 2;
                SI += 2;
        }
        CLOCK_CYCLES(7);
        PREFETCH_RUN(7, 1, -1, 1, 0, 1, 0, 0);
        return 0;
    }

    // pcem: x86_ops_string.h:138 — opCMPSB_a16
    private static int opCMPSB_a16(uint32_t fetchdat)
    {
        uint8_t src, dst;

        if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        if (SEG_CHECK_READ(cpu_state.seg_es!)) return 1;
        src = readmemb(cpu_state.ea_seg!.@base, SI);
        dst = readmemb(es, DI);
        if (cpu_state.abrt != 0)
                return 1;
        setsub8(src, dst);
        if ((cpu_state.flags & D_FLAG) != 0)
        {
                DI--;
                SI--;
        }
        else
        {
                DI++;
                SI++;
        }
        CLOCK_CYCLES(is486 != 0 ? 8 : 10);
        PREFETCH_RUN(is486 != 0 ? 8 : 10, 1, -1, 2, 0, 0, 0, 0);
        return 0;
    }

    // pcem: x86_ops_string.h:181 — opCMPSW_a16
    private static int opCMPSW_a16(uint32_t fetchdat)
    {
        uint16_t src, dst;

        if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        if (SEG_CHECK_READ(cpu_state.seg_es!)) return 1;
        src = readmemw(cpu_state.ea_seg!.@base, SI);
        dst = readmemw(es, DI);
        if (cpu_state.abrt != 0)
                return 1;
        setsub16(src, dst);
        if ((cpu_state.flags & D_FLAG) != 0)
        {
                DI -= 2;
                SI -= 2;
        }
        else
        {
                DI += 2;
                SI += 2;
        }
        CLOCK_CYCLES(is486 != 0 ? 8 : 10);
        PREFETCH_RUN(is486 != 0 ? 8 : 10, 1, -1, 2, 0, 0, 0, 0);
        return 0;
    }

    // pcem: x86_ops_string.h:267 — opSTOSB_a16
    private static int opSTOSB_a16(uint32_t fetchdat)
    {
        if (SEG_CHECK_WRITE(cpu_state.seg_es!)) return 1;
        writememb(es, DI, AL);
        if (cpu_state.abrt != 0)
                return 1;
        if ((cpu_state.flags & D_FLAG) != 0)
                DI--;
        else
                DI++;
        CLOCK_CYCLES(4);
        PREFETCH_RUN(4, 1, -1, 0, 0, 1, 0, 0);
        return 0;
    }

    // pcem: x86_ops_string.h:294 — opSTOSW_a16
    private static int opSTOSW_a16(uint32_t fetchdat)
    {
        if (SEG_CHECK_WRITE(cpu_state.seg_es!)) return 1;
        writememw(es, DI, AX);
        if (cpu_state.abrt != 0)
                return 1;
        if ((cpu_state.flags & D_FLAG) != 0)
                DI -= 2;
        else
                DI += 2;
        CLOCK_CYCLES(4);
        PREFETCH_RUN(4, 1, -1, 0, 0, 1, 0, 0);
        return 0;
    }

    // pcem: x86_ops_string.h:348 — opLODSB_a16
    private static int opLODSB_a16(uint32_t fetchdat)
    {
        uint8_t temp;

        if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        temp = readmemb(cpu_state.ea_seg!.@base, SI);
        if (cpu_state.abrt != 0)
                return 1;
        AL = temp;
        if ((cpu_state.flags & D_FLAG) != 0)
                SI--;
        else
                SI++;
        CLOCK_CYCLES(5);
        PREFETCH_RUN(5, 1, -1, 1, 0, 0, 0, 0);
        return 0;
    }

    // pcem: x86_ops_string.h:381 — opLODSW_a16
    private static int opLODSW_a16(uint32_t fetchdat)
    {
        uint16_t temp;

        if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        temp = readmemw(cpu_state.ea_seg!.@base, SI);
        if (cpu_state.abrt != 0)
                return 1;
        AX = temp;
        if ((cpu_state.flags & D_FLAG) != 0)
                SI -= 2;
        else
                SI += 2;
        CLOCK_CYCLES(5);
        PREFETCH_RUN(5, 1, -1, 1, 0, 0, 0, 0);
        return 0;
    }

    // pcem: x86_ops_string.h:447 — opSCASB_a16
    private static int opSCASB_a16(uint32_t fetchdat)
    {
        uint8_t temp;

        if (SEG_CHECK_READ(cpu_state.seg_es!)) return 1;
        temp = readmemb(es, DI);
        if (cpu_state.abrt != 0)
                return 1;
        setsub8(AL, temp);
        if ((cpu_state.flags & D_FLAG) != 0)
                DI--;
        else
                DI++;
        CLOCK_CYCLES(7);
        PREFETCH_RUN(7, 1, -1, 1, 0, 0, 0, 0);
        return 0;
    }

    // pcem: x86_ops_string.h:480 — opSCASW_a16
    private static int opSCASW_a16(uint32_t fetchdat)
    {
        uint16_t temp;

        if (SEG_CHECK_READ(cpu_state.seg_es!)) return 1;
        temp = readmemw(es, DI);
        if (cpu_state.abrt != 0)
                return 1;
        setsub16(AX, temp);
        if ((cpu_state.flags & D_FLAG) != 0)
                DI -= 2;
        else
                DI += 2;
        CLOCK_CYCLES(7);
        PREFETCH_RUN(7, 1, -1, 1, 0, 0, 0, 0);
        return 0;
    }

    // pcem: x86_ops_string.h:546 — opINSB_a16
    private static int opINSB_a16(uint32_t fetchdat)
    {
        uint8_t temp;

        if (SEG_CHECK_WRITE(cpu_state.seg_es!)) return 1;
        if (check_io_perm(DX)) return 1;
        temp = io.inb(DX);
        writememb(es, DI, temp);
        if (cpu_state.abrt != 0)
                return 1;
        if ((cpu_state.flags & D_FLAG) != 0)
                DI--;
        else
                DI++;
        CLOCK_CYCLES(15);
        PREFETCH_RUN(15, 1, -1, 1, 0, 1, 0, 0);
        return 0;
    }

    // pcem: x86_ops_string.h:581 — opINSW_a16
    private static int opINSW_a16(uint32_t fetchdat)
    {
        uint16_t temp;

        if (SEG_CHECK_WRITE(cpu_state.seg_es!)) return 1;
        if (check_io_perm(DX)) return 1;
        if (check_io_perm((uint16_t)(DX + 1))) return 1;
        temp = io.inw(DX);
        writememw(es, DI, temp);
        if (cpu_state.abrt != 0)
                return 1;
        if ((cpu_state.flags & D_FLAG) != 0)
                DI -= 2;
        else
                DI += 2;
        CLOCK_CYCLES(15);
        PREFETCH_RUN(15, 1, -1, 1, 0, 1, 0, 0);
        return 0;
    }

    // pcem: x86_ops_string.h:659 — opOUTSB_a16
    private static int opOUTSB_a16(uint32_t fetchdat)
    {
        uint8_t temp;

        if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        temp = readmemb(cpu_state.ea_seg!.@base, SI);
        if (cpu_state.abrt != 0)
                return 1;
        if (check_io_perm(DX)) return 1;
        if ((cpu_state.flags & D_FLAG) != 0)
                SI--;
        else
                SI++;
        io.outb(DX, temp);
        CLOCK_CYCLES(14);
        PREFETCH_RUN(14, 1, -1, 1, 0, 1, 0, 0);
        return 0;
    }

    // pcem: x86_ops_string.h:694 — opOUTSW_a16
    private static int opOUTSW_a16(uint32_t fetchdat)
    {
        uint16_t temp;

        if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        temp = readmemw(cpu_state.ea_seg!.@base, SI);
        if (cpu_state.abrt != 0)
                return 1;
        if (check_io_perm(DX)) return 1;
        if (check_io_perm((uint16_t)(DX + 1))) return 1;
        if ((cpu_state.flags & D_FLAG) != 0)
                SI -= 2;
        else
                SI += 2;
        io.outw(DX, temp);
        CLOCK_CYCLES(14);
        PREFETCH_RUN(14, 1, -1, 1, 0, 1, 0, 0);
        return 0;
    }
    // omitted: les 14 formes `_a32` et les sept formes `L` (MOVSL, CMPSL,
    //   STOSL, LODSL, SCASL, INSL, OUTSL) avec leurs variantes — op32 nul sur
    //   un 286.

    /// <summary>pcem: 6C-6F et A4-AF — relevés sur ops_286[] par gdb.
    /// A8 et A9 sont TEST AL/AX,imm (A3d) et non des opérations de chaîne : la
    /// bande A4-AF n'est pas continue.</summary>
    private static void PoserGroupeChaines()
    {
        ops_286[0x6C] = opINSB_a16;
        ops_286[0x6D] = opINSW_a16;
        ops_286[0x6E] = opOUTSB_a16;
        ops_286[0x6F] = opOUTSW_a16;
        ops_286[0xA4] = opMOVSB_a16;
        ops_286[0xA5] = opMOVSW_a16;
        ops_286[0xA6] = opCMPSB_a16;
        ops_286[0xA7] = opCMPSW_a16;
        ops_286[0xAA] = opSTOSB_a16;
        ops_286[0xAB] = opSTOSW_a16;
        ops_286[0xAC] = opLODSB_a16;
        ops_286[0xAD] = opLODSW_a16;
        ops_286[0xAE] = opSCASB_a16;
        ops_286[0xAF] = opSCASW_a16;
    }

    // ---- G2, D2 : les formes 32 bits (_l, _a32) de x86_ops_string.h ----

    // pcem: x86_ops_string.h:159
    private static int opCMPSB_a32(uint32_t fetchdat)
    {
        uint8_t src, dst;

        if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        if (SEG_CHECK_READ(cpu_state.seg_es)) return 1;
        src = readmemb(cpu_state.ea_seg!.@base, ESI);
        dst = readmemb(es, EDI);
        if (cpu_state.abrt != 0)
                return 1;
        setsub8(src, dst);
        if ((cpu_state.flags & D_FLAG) != 0) {
                EDI--;
                ESI--;
        } else {
                EDI++;
                ESI++;
        }
        CLOCK_CYCLES((is486 != 0) ? 8 : 10);
        PREFETCH_RUN((is486 != 0) ? 8 : 10, 1, -1, 2, 0, 0, 0, 1);
        return 0;
    }

    // pcem: x86_ops_string.h:224
    private static int opCMPSL_a16(uint32_t fetchdat)
    {
        uint32_t src, dst;

        if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        if (SEG_CHECK_READ(cpu_state.seg_es)) return 1;
        src = readmeml(cpu_state.ea_seg!.@base, SI);
        dst = readmeml(es, DI);
        if (cpu_state.abrt != 0)
                return 1;
        setsub32(src, dst);
        if ((cpu_state.flags & D_FLAG) != 0) {
                DI -= 4;
                SI -= 4;
        } else {
                DI += 4;
                SI += 4;
        }
        CLOCK_CYCLES((is486 != 0) ? 8 : 10);
        PREFETCH_RUN((is486 != 0) ? 8 : 10, 1, -1, 0, 2, 0, 0, 0);
        return 0;
    }

    // pcem: x86_ops_string.h:245
    private static int opCMPSL_a32(uint32_t fetchdat)
    {
        uint32_t src, dst;

        if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        if (SEG_CHECK_READ(cpu_state.seg_es)) return 1;
        src = readmeml(cpu_state.ea_seg!.@base, ESI);
        dst = readmeml(es, EDI);
        if (cpu_state.abrt != 0)
                return 1;
        setsub32(src, dst);
        if ((cpu_state.flags & D_FLAG) != 0) {
                EDI -= 4;
                ESI -= 4;
        } else {
                EDI += 4;
                ESI += 4;
        }
        CLOCK_CYCLES((is486 != 0) ? 8 : 10);
        PREFETCH_RUN((is486 != 0) ? 8 : 10, 1, -1, 0, 2, 0, 0, 1);
        return 0;
    }

    // pcem: x86_ops_string.h:202
    private static int opCMPSW_a32(uint32_t fetchdat)
    {
        uint16_t src, dst;

        if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        if (SEG_CHECK_READ(cpu_state.seg_es)) return 1;
        src = readmemw(cpu_state.ea_seg!.@base, ESI);
        dst = readmemw(es, EDI);
        if (cpu_state.abrt != 0)
                return 1;
        setsub16(src, dst);
        if ((cpu_state.flags & D_FLAG) != 0) {
                EDI -= 2;
                ESI -= 2;
        } else {
                EDI += 2;
                ESI += 2;
        }
        CLOCK_CYCLES((is486 != 0) ? 8 : 10);
        PREFETCH_RUN((is486 != 0) ? 8 : 10, 1, -1, 2, 0, 0, 0, 1);
        return 0;
    }

    // pcem: x86_ops_string.h:563
    private static int opINSB_a32(uint32_t fetchdat)
    {
        uint8_t temp;

        if (SEG_CHECK_WRITE(cpu_state.seg_es)) return 1;
        check_io_perm(DX);
        temp = io.inb(DX);
        writememb(es, EDI, temp);
        if (cpu_state.abrt != 0)
                return 1;
        if ((cpu_state.flags & D_FLAG) != 0)
                EDI--;
        else
                EDI++;
        CLOCK_CYCLES(15);
        PREFETCH_RUN(15, 1, -1, 1, 0, 1, 0, 1);
        return 0;
    }

    // pcem: x86_ops_string.h:618
    private static int opINSL_a16(uint32_t fetchdat)
    {
        uint32_t temp;

        if (SEG_CHECK_WRITE(cpu_state.seg_es)) return 1;
        check_io_perm(DX);
        check_io_perm((uint16_t)(DX + 1));
        check_io_perm((uint16_t)(DX + 2));
        check_io_perm((uint16_t)(DX + 3));
        temp = io.inl(DX);
        writememl(es, DI, temp);
        if (cpu_state.abrt != 0)
                return 1;
        if ((cpu_state.flags & D_FLAG) != 0)
                DI -= 4;
        else
                DI += 4;
        CLOCK_CYCLES(15);
        PREFETCH_RUN(15, 1, -1, 0, 1, 0, 1, 0);
        return 0;
    }

    // pcem: x86_ops_string.h:638
    private static int opINSL_a32(uint32_t fetchdat)
    {
        uint32_t temp;

        if (SEG_CHECK_WRITE(cpu_state.seg_es)) return 1;
        check_io_perm(DX);
        check_io_perm((uint16_t)(DX + 1));
        check_io_perm((uint16_t)(DX + 2));
        check_io_perm((uint16_t)(DX + 3));
        temp = io.inl(DX);
        writememl(es, EDI, temp);
        if (cpu_state.abrt != 0)
                return 1;
        if ((cpu_state.flags & D_FLAG) != 0)
                EDI -= 4;
        else
                EDI += 4;
        CLOCK_CYCLES(15);
        PREFETCH_RUN(15, 1, -1, 0, 1, 0, 1, 1);
        return 0;
    }

    // pcem: x86_ops_string.h:599
    private static int opINSW_a32(uint32_t fetchdat)
    {
        uint16_t temp;

        if (SEG_CHECK_WRITE(cpu_state.seg_es)) return 1;
        check_io_perm(DX);
        check_io_perm((uint16_t)(DX + 1));
        temp = io.inw(DX);
        writememw(es, EDI, temp);
        if (cpu_state.abrt != 0)
                return 1;
        if ((cpu_state.flags & D_FLAG) != 0)
                EDI -= 2;
        else
                EDI += 2;
        CLOCK_CYCLES(15);
        PREFETCH_RUN(15, 1, -1, 1, 0, 1, 0, 1);
        return 0;
    }

    // pcem: x86_ops_string.h:364
    private static int opLODSB_a32(uint32_t fetchdat)
    {
        uint8_t temp;

        if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        temp = readmemb(cpu_state.ea_seg!.@base, ESI);
        if (cpu_state.abrt != 0)
                return 1;
        AL = temp;
        if ((cpu_state.flags & D_FLAG) != 0)
                ESI--;
        else
                ESI++;
        CLOCK_CYCLES(5);
        PREFETCH_RUN(5, 1, -1, 1, 0, 0, 0, 1);
        return 0;
    }

    // pcem: x86_ops_string.h:414
    private static int opLODSL_a16(uint32_t fetchdat)
    {
        uint32_t temp;

        if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        temp = readmeml(cpu_state.ea_seg!.@base, SI);
        if (cpu_state.abrt != 0)
                return 1;
        EAX = temp;
        if ((cpu_state.flags & D_FLAG) != 0)
                SI -= 4;
        else
                SI += 4;
        CLOCK_CYCLES(5);
        PREFETCH_RUN(5, 1, -1, 0, 1, 0, 0, 0);
        return 0;
    }

    // pcem: x86_ops_string.h:430
    private static int opLODSL_a32(uint32_t fetchdat)
    {
        uint32_t temp;

        if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        temp = readmeml(cpu_state.ea_seg!.@base, ESI);
        if (cpu_state.abrt != 0)
                return 1;
        EAX = temp;
        if ((cpu_state.flags & D_FLAG) != 0)
                ESI -= 4;
        else
                ESI += 4;
        CLOCK_CYCLES(5);
        PREFETCH_RUN(5, 1, -1, 0, 1, 0, 0, 1);
        return 0;
    }

    // pcem: x86_ops_string.h:397
    private static int opLODSW_a32(uint32_t fetchdat)
    {
        uint16_t temp;

        if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        temp = readmemw(cpu_state.ea_seg!.@base, ESI);
        if (cpu_state.abrt != 0)
                return 1;
        AX = temp;
        if ((cpu_state.flags & D_FLAG) != 0)
                ESI -= 2;
        else
                ESI += 2;
        CLOCK_CYCLES(5);
        PREFETCH_RUN(5, 1, -1, 1, 0, 0, 0, 1);
        return 0;
    }

    // pcem: x86_ops_string.h:25
    private static int opMOVSB_a32(uint32_t fetchdat)
    {
        uint8_t temp;

        if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        if (SEG_CHECK_WRITE(cpu_state.seg_es)) return 1;
        temp = readmemb(cpu_state.ea_seg!.@base, ESI);
        if (cpu_state.abrt != 0)
                return 1;
        writememb(es, EDI, temp);
        if (cpu_state.abrt != 0)
                return 1;
        if ((cpu_state.flags & D_FLAG) != 0) {
                EDI--;
                ESI--;
        } else {
                EDI++;
                ESI++;
        }
        CLOCK_CYCLES(7);
        PREFETCH_RUN(7, 1, -1, 1, 0, 1, 0, 1);
        return 0;
    }

    // pcem: x86_ops_string.h:93
    private static int opMOVSL_a16(uint32_t fetchdat)
    {
        uint32_t temp;

        if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        if (SEG_CHECK_WRITE(cpu_state.seg_es)) return 1;
        temp = readmeml(cpu_state.ea_seg!.@base, SI);
        if (cpu_state.abrt != 0)
                return 1;
        writememl(es, DI, temp);
        if (cpu_state.abrt != 0)
                return 1;
        if ((cpu_state.flags & D_FLAG) != 0) {
                DI -= 4;
                SI -= 4;
        } else {
                DI += 4;
                SI += 4;
        }
        CLOCK_CYCLES(7);
        PREFETCH_RUN(7, 1, -1, 0, 1, 0, 1, 0);
        return 0;
    }

    // pcem: x86_ops_string.h:115
    private static int opMOVSL_a32(uint32_t fetchdat)
    {
        uint32_t temp;

        if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        if (SEG_CHECK_WRITE(cpu_state.seg_es)) return 1;
        temp = readmeml(cpu_state.ea_seg!.@base, ESI);
        if (cpu_state.abrt != 0)
                return 1;
        writememl(es, EDI, temp);
        if (cpu_state.abrt != 0)
                return 1;
        if ((cpu_state.flags & D_FLAG) != 0) {
                EDI -= 4;
                ESI -= 4;
        } else {
                EDI += 4;
                ESI += 4;
        }
        CLOCK_CYCLES(7);
        PREFETCH_RUN(7, 1, -1, 0, 1, 0, 1, 1);
        return 0;
    }

    // pcem: x86_ops_string.h:70
    private static int opMOVSW_a32(uint32_t fetchdat)
    {
        uint16_t temp;

        if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        if (SEG_CHECK_WRITE(cpu_state.seg_es)) return 1;
        temp = readmemw(cpu_state.ea_seg!.@base, ESI);
        if (cpu_state.abrt != 0)
                return 1;
        writememw(es, EDI, temp);
        if (cpu_state.abrt != 0)
                return 1;
        if ((cpu_state.flags & D_FLAG) != 0) {
                EDI -= 2;
                ESI -= 2;
        } else {
                EDI += 2;
                ESI += 2;
        }
        CLOCK_CYCLES(7);
        PREFETCH_RUN(7, 1, -1, 1, 0, 1, 0, 1);
        return 0;
    }

    // pcem: x86_ops_string.h:676
    private static int opOUTSB_a32(uint32_t fetchdat)
    {
        uint8_t temp;

        if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        temp = readmemb(cpu_state.ea_seg!.@base, ESI);
        if (cpu_state.abrt != 0)
                return 1;
        check_io_perm(DX);
        if ((cpu_state.flags & D_FLAG) != 0)
                ESI--;
        else
                ESI++;
        io.outb(DX, temp);
        CLOCK_CYCLES(14);
        PREFETCH_RUN(14, 1, -1, 1, 0, 1, 0, 1);
        return 0;
    }

    // pcem: x86_ops_string.h:731
    private static int opOUTSL_a16(uint32_t fetchdat)
    {
        uint32_t temp;

        if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        temp = readmeml(cpu_state.ea_seg!.@base, SI);
        if (cpu_state.abrt != 0)
                return 1;
        check_io_perm(DX);
        check_io_perm((uint16_t)(DX + 1));
        check_io_perm((uint16_t)(DX + 2));
        check_io_perm((uint16_t)(DX + 3));
        if ((cpu_state.flags & D_FLAG) != 0)
                SI -= 4;
        else
                SI += 4;
        io.outl((uint16_t)EDX, temp); // verbatim : PCem passe EDX, tronqué au port 16 bits
        CLOCK_CYCLES(14);
        PREFETCH_RUN(14, 1, -1, 0, 1, 0, 1, 0);
        return 0;
    }

    // pcem: x86_ops_string.h:751
    private static int opOUTSL_a32(uint32_t fetchdat)
    {
        uint32_t temp;

        if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        temp = readmeml(cpu_state.ea_seg!.@base, ESI);
        if (cpu_state.abrt != 0)
                return 1;
        check_io_perm(DX);
        check_io_perm((uint16_t)(DX + 1));
        check_io_perm((uint16_t)(DX + 2));
        check_io_perm((uint16_t)(DX + 3));
        if ((cpu_state.flags & D_FLAG) != 0)
                ESI -= 4;
        else
                ESI += 4;
        io.outl((uint16_t)EDX, temp); // verbatim : PCem passe EDX, tronqué au port 16 bits
        CLOCK_CYCLES(14);
        PREFETCH_RUN(14, 1, -1, 0, 1, 0, 1, 1);
        return 0;
    }

    // pcem: x86_ops_string.h:712
    private static int opOUTSW_a32(uint32_t fetchdat)
    {
        uint16_t temp;

        if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        temp = readmemw(cpu_state.ea_seg!.@base, ESI);
        if (cpu_state.abrt != 0)
                return 1;
        check_io_perm(DX);
        check_io_perm((uint16_t)(DX + 1));
        if ((cpu_state.flags & D_FLAG) != 0)
                ESI -= 2;
        else
                ESI += 2;
        io.outw(DX, temp);
        CLOCK_CYCLES(14);
        PREFETCH_RUN(14, 1, -1, 1, 0, 1, 0, 1);
        return 0;
    }

    // pcem: x86_ops_string.h:463
    private static int opSCASB_a32(uint32_t fetchdat)
    {
        uint8_t temp;

        if (SEG_CHECK_READ(cpu_state.seg_es)) return 1;
        temp = readmemb(es, EDI);
        if (cpu_state.abrt != 0)
                return 1;
        setsub8(AL, temp);
        if ((cpu_state.flags & D_FLAG) != 0)
                EDI--;
        else
                EDI++;
        CLOCK_CYCLES(7);
        PREFETCH_RUN(7, 1, -1, 1, 0, 0, 0, 1);
        return 0;
    }

    // pcem: x86_ops_string.h:513
    private static int opSCASL_a16(uint32_t fetchdat)
    {
        uint32_t temp;

        if (SEG_CHECK_READ(cpu_state.seg_es)) return 1;
        temp = readmeml(es, DI);
        if (cpu_state.abrt != 0)
                return 1;
        setsub32(EAX, temp);
        if ((cpu_state.flags & D_FLAG) != 0)
                DI -= 4;
        else
                DI += 4;
        CLOCK_CYCLES(7);
        PREFETCH_RUN(7, 1, -1, 0, 1, 0, 0, 0);
        return 0;
    }

    // pcem: x86_ops_string.h:529
    private static int opSCASL_a32(uint32_t fetchdat)
    {
        uint32_t temp;

        if (SEG_CHECK_READ(cpu_state.seg_es)) return 1;
        temp = readmeml(es, EDI);
        if (cpu_state.abrt != 0)
                return 1;
        setsub32(EAX, temp);
        if ((cpu_state.flags & D_FLAG) != 0)
                EDI -= 4;
        else
                EDI += 4;
        CLOCK_CYCLES(7);
        PREFETCH_RUN(7, 1, -1, 0, 1, 0, 0, 1);
        return 0;
    }

    // pcem: x86_ops_string.h:496
    private static int opSCASW_a32(uint32_t fetchdat)
    {
        uint16_t temp;

        if (SEG_CHECK_READ(cpu_state.seg_es)) return 1;
        temp = readmemw(es, EDI);
        if (cpu_state.abrt != 0)
                return 1;
        setsub16(AX, temp);
        if ((cpu_state.flags & D_FLAG) != 0)
                EDI -= 2;
        else
                EDI += 2;
        CLOCK_CYCLES(7);
        PREFETCH_RUN(7, 1, -1, 1, 0, 0, 0, 1);
        return 0;
    }

    // pcem: x86_ops_string.h:280
    private static int opSTOSB_a32(uint32_t fetchdat)
    {
        if (SEG_CHECK_WRITE(cpu_state.seg_es)) return 1;
        writememb(es, EDI, AL);
        if (cpu_state.abrt != 0)
                return 1;
        if ((cpu_state.flags & D_FLAG) != 0)
                EDI--;
        else
                EDI++;
        CLOCK_CYCLES(4);
        PREFETCH_RUN(4, 1, -1, 0, 0, 1, 0, 1);
        return 0;
    }

    // pcem: x86_ops_string.h:321
    private static int opSTOSL_a16(uint32_t fetchdat)
    {
        if (SEG_CHECK_WRITE(cpu_state.seg_es)) return 1;
        writememl(es, DI, EAX);
        if (cpu_state.abrt != 0)
                return 1;
        if ((cpu_state.flags & D_FLAG) != 0)
                DI -= 4;
        else
                DI += 4;
        CLOCK_CYCLES(4);
        PREFETCH_RUN(4, 1, -1, 0, 0, 0, 1, 0);
        return 0;
    }

    // pcem: x86_ops_string.h:334
    private static int opSTOSL_a32(uint32_t fetchdat)
    {
        if (SEG_CHECK_WRITE(cpu_state.seg_es)) return 1;
        writememl(es, EDI, EAX);
        if (cpu_state.abrt != 0)
                return 1;
        if ((cpu_state.flags & D_FLAG) != 0)
                EDI -= 4;
        else
                EDI += 4;
        CLOCK_CYCLES(4);
        PREFETCH_RUN(4, 1, -1, 0, 0, 0, 1, 1);
        return 0;
    }

    // pcem: x86_ops_string.h:307
    private static int opSTOSW_a32(uint32_t fetchdat)
    {
        if (SEG_CHECK_WRITE(cpu_state.seg_es)) return 1;
        writememw(es, EDI, AX);
        if (cpu_state.abrt != 0)
                return 1;
        if ((cpu_state.flags & D_FLAG) != 0)
                EDI -= 2;
        else
                EDI += 2;
        CLOCK_CYCLES(4);
        PREFETCH_RUN(4, 1, -1, 0, 0, 1, 0, 1);
        return 0;
    }

    // pcem: 386_ops.h — les emplacements de ces handlers dans OP_TABLE(386) et (386_0f).
    private static void PoserGroupe_string_386()
    {
        ops_386[0x16D] = opINSL_a16;
        ops_386[0x16F] = opOUTSL_a16;
        ops_386[0x1A5] = opMOVSL_a16;
        ops_386[0x1A7] = opCMPSL_a16;
        ops_386[0x1AB] = opSTOSL_a16;
        ops_386[0x1AD] = opLODSL_a16;
        ops_386[0x1AF] = opSCASL_a16;
        ops_386[0x26C] = opINSB_a32;
        ops_386[0x26D] = opINSW_a32;
        ops_386[0x26E] = opOUTSB_a32;
        ops_386[0x26F] = opOUTSW_a32;
        ops_386[0x2A4] = opMOVSB_a32;
        ops_386[0x2A5] = opMOVSW_a32;
        ops_386[0x2A6] = opCMPSB_a32;
        ops_386[0x2A7] = opCMPSW_a32;
        ops_386[0x2AA] = opSTOSB_a32;
        ops_386[0x2AB] = opSTOSW_a32;
        ops_386[0x2AC] = opLODSB_a32;
        ops_386[0x2AD] = opLODSW_a32;
        ops_386[0x2AE] = opSCASB_a32;
        ops_386[0x2AF] = opSCASW_a32;
        ops_386[0x36C] = opINSB_a32;
        ops_386[0x36D] = opINSL_a32;
        ops_386[0x36E] = opOUTSB_a32;
        ops_386[0x36F] = opOUTSL_a32;
        ops_386[0x3A4] = opMOVSB_a32;
        ops_386[0x3A5] = opMOVSL_a32;
        ops_386[0x3A6] = opCMPSB_a32;
        ops_386[0x3A7] = opCMPSL_a32;
        ops_386[0x3AA] = opSTOSB_a32;
        ops_386[0x3AB] = opSTOSL_a32;
        ops_386[0x3AC] = opLODSB_a32;
        ops_386[0x3AD] = opLODSL_a32;
        ops_386[0x3AE] = opSCASB_a32;
        ops_386[0x3AF] = opSCASL_a32;
    }
}
