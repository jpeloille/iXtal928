// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/includes/private/cpu/x87_ops.h  (les tables a16, lignes 310-1040), sous
//         8087.h:7 — `#define OP_TABLE(name) ops_808x_##name`
// SHA256: voir oracle.tsv ; vérifier avec tools/check-oracle.sh
// STATUS: complete — G4.6 : les huit tables a16 du 8087, générées verbatim ; le 808x n'appelle
//         que les formes a16 (808x.c:3304-3366). Elles nomment les handlers de x87_ops_808x.cs.

namespace iXtal26.Cpu;

internal static partial class _808x
{
    // pcem: x87_ops.h:310-314, OP_TABLE(fpu_d8_a16) sous 8087.h
    internal static readonly OpFn[] ops_808x_fpu_d8_a16 =
    [
        opFADDs_a16, opFMULs_a16, opFCOMs_a16, opFCOMPs_a16, opFSUBs_a16, opFSUBRs_a16, opFDIVs_a16, opFDIVRs_a16,
        opFADDs_a16, opFMULs_a16, opFCOMs_a16, opFCOMPs_a16, opFSUBs_a16, opFSUBRs_a16, opFDIVs_a16, opFDIVRs_a16,
        opFADDs_a16, opFMULs_a16, opFCOMs_a16, opFCOMPs_a16, opFSUBs_a16, opFSUBRs_a16, opFDIVs_a16, opFDIVRs_a16,
        opFADD, opFMUL, opFCOM, opFCOMP, opFSUB, opFSUBR, opFDIV, opFDIVR,
    ];

    // pcem: x87_ops.h:322-357, OP_TABLE(fpu_d9_a16) sous 8087.h
    internal static readonly OpFn[] ops_808x_fpu_d9_a16 =
    [
        opFLDs_a16, opFLDs_a16, opFLDs_a16, opFLDs_a16, opFLDs_a16, opFLDs_a16, opFLDs_a16, opFLDs_a16,
        FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16,
        opFSTs_a16, opFSTs_a16, opFSTs_a16, opFSTs_a16, opFSTs_a16, opFSTs_a16, opFSTs_a16, opFSTs_a16,
        opFSTPs_a16, opFSTPs_a16, opFSTPs_a16, opFSTPs_a16, opFSTPs_a16, opFSTPs_a16, opFSTPs_a16, opFSTPs_a16,
        opFLDENV_a16, opFLDENV_a16, opFLDENV_a16, opFLDENV_a16, opFLDENV_a16, opFLDENV_a16, opFLDENV_a16, opFLDENV_a16,
        opFLDCW_a16, opFLDCW_a16, opFLDCW_a16, opFLDCW_a16, opFLDCW_a16, opFLDCW_a16, opFLDCW_a16, opFLDCW_a16,
        opFSTENV_a16, opFSTENV_a16, opFSTENV_a16, opFSTENV_a16, opFSTENV_a16, opFSTENV_a16, opFSTENV_a16, opFSTENV_a16,
        opFSTCW_a16, opFSTCW_a16, opFSTCW_a16, opFSTCW_a16, opFSTCW_a16, opFSTCW_a16, opFSTCW_a16, opFSTCW_a16,
        opFLDs_a16, opFLDs_a16, opFLDs_a16, opFLDs_a16, opFLDs_a16, opFLDs_a16, opFLDs_a16, opFLDs_a16,
        FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16,
        opFSTs_a16, opFSTs_a16, opFSTs_a16, opFSTs_a16, opFSTs_a16, opFSTs_a16, opFSTs_a16, opFSTs_a16,
        opFSTPs_a16, opFSTPs_a16, opFSTPs_a16, opFSTPs_a16, opFSTPs_a16, opFSTPs_a16, opFSTPs_a16, opFSTPs_a16,
        opFLDENV_a16, opFLDENV_a16, opFLDENV_a16, opFLDENV_a16, opFLDENV_a16, opFLDENV_a16, opFLDENV_a16, opFLDENV_a16,
        opFLDCW_a16, opFLDCW_a16, opFLDCW_a16, opFLDCW_a16, opFLDCW_a16, opFLDCW_a16, opFLDCW_a16, opFLDCW_a16,
        opFSTENV_a16, opFSTENV_a16, opFSTENV_a16, opFSTENV_a16, opFSTENV_a16, opFSTENV_a16, opFSTENV_a16, opFSTENV_a16,
        opFSTCW_a16, opFSTCW_a16, opFSTCW_a16, opFSTCW_a16, opFSTCW_a16, opFSTCW_a16, opFSTCW_a16, opFSTCW_a16,
        opFLDs_a16, opFLDs_a16, opFLDs_a16, opFLDs_a16, opFLDs_a16, opFLDs_a16, opFLDs_a16, opFLDs_a16,
        FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16,
        opFSTs_a16, opFSTs_a16, opFSTs_a16, opFSTs_a16, opFSTs_a16, opFSTs_a16, opFSTs_a16, opFSTs_a16,
        opFSTPs_a16, opFSTPs_a16, opFSTPs_a16, opFSTPs_a16, opFSTPs_a16, opFSTPs_a16, opFSTPs_a16, opFSTPs_a16,
        opFLDENV_a16, opFLDENV_a16, opFLDENV_a16, opFLDENV_a16, opFLDENV_a16, opFLDENV_a16, opFLDENV_a16, opFLDENV_a16,
        opFLDCW_a16, opFLDCW_a16, opFLDCW_a16, opFLDCW_a16, opFLDCW_a16, opFLDCW_a16, opFLDCW_a16, opFLDCW_a16,
        opFSTENV_a16, opFSTENV_a16, opFSTENV_a16, opFSTENV_a16, opFSTENV_a16, opFSTENV_a16, opFSTENV_a16, opFSTENV_a16,
        opFSTCW_a16, opFSTCW_a16, opFSTCW_a16, opFSTCW_a16, opFSTCW_a16, opFSTCW_a16, opFSTCW_a16, opFSTCW_a16,
        opFLD, opFLD, opFLD, opFLD, opFLD, opFLD, opFLD, opFLD,
        opFXCH, opFXCH, opFXCH, opFXCH, opFXCH, opFXCH, opFXCH, opFXCH,
        opFNOP, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16,
        opFSTP, opFSTP, opFSTP, opFSTP, opFSTP, opFSTP, opFSTP, opFSTP,
        opFCHS, opFABS, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, opFTST, opFXAM, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16,
        opFLD1, opFLDL2T, opFLDL2E, opFLDPI, opFLDEG2, opFLDLN2, opFLDZ, FPU_ILLEGAL_a16,
        // pcem bug, reproduced: PB-194 — D9 F4 (FXTRACT) est ILLEGAL : la pile ne bouge pas.
        // pcem bug, reproduced: PB-209 — FPREM1 (D9 F5), du 387, s'exécute sur le 8087.
        opF2XM1, opFYL2X, opFPTAN, opFPATAN, FPU_ILLEGAL_a16, opFPREM1, opFDECSTP, opFINCSTP,
        // pcem bug, reproduced: PB-68 — FSINCOS, FSIN et FCOS (D9 FB, FE, FF), du 387, s'exécutent sur le 8087.
        opFPREM, opFYL2XP1, opFSQRT, opFSINCOS, opFRNDINT, opFSCALE, opFSIN, opFCOS,
    ];

    // pcem: x87_ops.h:399-435, OP_TABLE(fpu_da_a16) sous 8087.h
    internal static readonly OpFn[] ops_808x_fpu_da_a16 =
    [
        opFADDil_a16, opFADDil_a16, opFADDil_a16, opFADDil_a16, opFADDil_a16, opFADDil_a16, opFADDil_a16, opFADDil_a16,
        opFMULil_a16, opFMULil_a16, opFMULil_a16, opFMULil_a16, opFMULil_a16, opFMULil_a16, opFMULil_a16, opFMULil_a16,
        opFCOMil_a16, opFCOMil_a16, opFCOMil_a16, opFCOMil_a16, opFCOMil_a16, opFCOMil_a16, opFCOMil_a16, opFCOMil_a16,
        opFCOMPil_a16, opFCOMPil_a16, opFCOMPil_a16, opFCOMPil_a16, opFCOMPil_a16, opFCOMPil_a16, opFCOMPil_a16, opFCOMPil_a16,
        opFSUBil_a16, opFSUBil_a16, opFSUBil_a16, opFSUBil_a16, opFSUBil_a16, opFSUBil_a16, opFSUBil_a16, opFSUBil_a16,
        opFSUBRil_a16, opFSUBRil_a16, opFSUBRil_a16, opFSUBRil_a16, opFSUBRil_a16, opFSUBRil_a16, opFSUBRil_a16, opFSUBRil_a16,
        opFDIVil_a16, opFDIVil_a16, opFDIVil_a16, opFDIVil_a16, opFDIVil_a16, opFDIVil_a16, opFDIVil_a16, opFDIVil_a16,
        opFDIVRil_a16, opFDIVRil_a16, opFDIVRil_a16, opFDIVRil_a16, opFDIVRil_a16, opFDIVRil_a16, opFDIVRil_a16, opFDIVRil_a16,
        opFADDil_a16, opFADDil_a16, opFADDil_a16, opFADDil_a16, opFADDil_a16, opFADDil_a16, opFADDil_a16, opFADDil_a16,
        opFMULil_a16, opFMULil_a16, opFMULil_a16, opFMULil_a16, opFMULil_a16, opFMULil_a16, opFMULil_a16, opFMULil_a16,
        opFCOMil_a16, opFCOMil_a16, opFCOMil_a16, opFCOMil_a16, opFCOMil_a16, opFCOMil_a16, opFCOMil_a16, opFCOMil_a16,
        opFCOMPil_a16, opFCOMPil_a16, opFCOMPil_a16, opFCOMPil_a16, opFCOMPil_a16, opFCOMPil_a16, opFCOMPil_a16, opFCOMPil_a16,
        opFSUBil_a16, opFSUBil_a16, opFSUBil_a16, opFSUBil_a16, opFSUBil_a16, opFSUBil_a16, opFSUBil_a16, opFSUBil_a16,
        opFSUBRil_a16, opFSUBRil_a16, opFSUBRil_a16, opFSUBRil_a16, opFSUBRil_a16, opFSUBRil_a16, opFSUBRil_a16, opFSUBRil_a16,
        opFDIVil_a16, opFDIVil_a16, opFDIVil_a16, opFDIVil_a16, opFDIVil_a16, opFDIVil_a16, opFDIVil_a16, opFDIVil_a16,
        opFDIVRil_a16, opFDIVRil_a16, opFDIVRil_a16, opFDIVRil_a16, opFDIVRil_a16, opFDIVRil_a16, opFDIVRil_a16, opFDIVRil_a16,
        opFADDil_a16, opFADDil_a16, opFADDil_a16, opFADDil_a16, opFADDil_a16, opFADDil_a16, opFADDil_a16, opFADDil_a16,
        opFMULil_a16, opFMULil_a16, opFMULil_a16, opFMULil_a16, opFMULil_a16, opFMULil_a16, opFMULil_a16, opFMULil_a16,
        opFCOMil_a16, opFCOMil_a16, opFCOMil_a16, opFCOMil_a16, opFCOMil_a16, opFCOMil_a16, opFCOMil_a16, opFCOMil_a16,
        opFCOMPil_a16, opFCOMPil_a16, opFCOMPil_a16, opFCOMPil_a16, opFCOMPil_a16, opFCOMPil_a16, opFCOMPil_a16, opFCOMPil_a16,
        opFSUBil_a16, opFSUBil_a16, opFSUBil_a16, opFSUBil_a16, opFSUBil_a16, opFSUBil_a16, opFSUBil_a16, opFSUBil_a16,
        opFSUBRil_a16, opFSUBRil_a16, opFSUBRil_a16, opFSUBRil_a16, opFSUBRil_a16, opFSUBRil_a16, opFSUBRil_a16, opFSUBRil_a16,
        opFDIVil_a16, opFDIVil_a16, opFDIVil_a16, opFDIVil_a16, opFDIVil_a16, opFDIVil_a16, opFDIVil_a16, opFDIVil_a16,
        opFDIVRil_a16, opFDIVRil_a16, opFDIVRil_a16, opFDIVRil_a16, opFDIVRil_a16, opFDIVRil_a16, opFDIVRil_a16, opFDIVRil_a16,
        FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16,
        FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16,
        FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16,
        FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16,
        FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16,
        // pcem bug, reproduced: PB-209 — FUCOMPP (DA E9), du 387, s'exécute sur le 8087.
        FPU_ILLEGAL_a16, opFUCOMPP, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16,
        FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16,
        FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16,
    ];

    // pcem: x87_ops.h:557-593, OP_TABLE(fpu_db_a16) sous 8087.h
    internal static readonly OpFn[] ops_808x_fpu_db_a16 =
    [
        opFILDil_a16, opFILDil_a16, opFILDil_a16, opFILDil_a16, opFILDil_a16, opFILDil_a16, opFILDil_a16, opFILDil_a16,
        FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16,
        opFISTil_a16, opFISTil_a16, opFISTil_a16, opFISTil_a16, opFISTil_a16, opFISTil_a16, opFISTil_a16, opFISTil_a16,
        opFISTPil_a16, opFISTPil_a16, opFISTPil_a16, opFISTPil_a16, opFISTPil_a16, opFISTPil_a16, opFISTPil_a16, opFISTPil_a16,
        FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16,
        opFLDe_a16, opFLDe_a16, opFLDe_a16, opFLDe_a16, opFLDe_a16, opFLDe_a16, opFLDe_a16, opFLDe_a16,
        FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16,
        opFSTPe_a16, opFSTPe_a16, opFSTPe_a16, opFSTPe_a16, opFSTPe_a16, opFSTPe_a16, opFSTPe_a16, opFSTPe_a16,
        opFILDil_a16, opFILDil_a16, opFILDil_a16, opFILDil_a16, opFILDil_a16, opFILDil_a16, opFILDil_a16, opFILDil_a16,
        FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16,
        opFISTil_a16, opFISTil_a16, opFISTil_a16, opFISTil_a16, opFISTil_a16, opFISTil_a16, opFISTil_a16, opFISTil_a16,
        opFISTPil_a16, opFISTPil_a16, opFISTPil_a16, opFISTPil_a16, opFISTPil_a16, opFISTPil_a16, opFISTPil_a16, opFISTPil_a16,
        FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16,
        opFLDe_a16, opFLDe_a16, opFLDe_a16, opFLDe_a16, opFLDe_a16, opFLDe_a16, opFLDe_a16, opFLDe_a16,
        FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16,
        opFSTPe_a16, opFSTPe_a16, opFSTPe_a16, opFSTPe_a16, opFSTPe_a16, opFSTPe_a16, opFSTPe_a16, opFSTPe_a16,
        opFILDil_a16, opFILDil_a16, opFILDil_a16, opFILDil_a16, opFILDil_a16, opFILDil_a16, opFILDil_a16, opFILDil_a16,
        FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16,
        opFISTil_a16, opFISTil_a16, opFISTil_a16, opFISTil_a16, opFISTil_a16, opFISTil_a16, opFISTil_a16, opFISTil_a16,
        opFISTPil_a16, opFISTPil_a16, opFISTPil_a16, opFISTPil_a16, opFISTPil_a16, opFISTPil_a16, opFISTPil_a16, opFISTPil_a16,
        FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16,
        opFLDe_a16, opFLDe_a16, opFLDe_a16, opFLDe_a16, opFLDe_a16, opFLDe_a16, opFLDe_a16, opFLDe_a16,
        FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16,
        opFSTPe_a16, opFSTPe_a16, opFSTPe_a16, opFSTPe_a16, opFSTPe_a16, opFSTPe_a16, opFSTPe_a16, opFSTPe_a16,
        FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16,
        FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16,
        FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16,
        FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16,
        opFENI, opFDISI, opFCLEX, opFINIT, opFNOP, opFNOP, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16,
        FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16,
        FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16,
        FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16,
    ];

    // pcem: x87_ops.h:714-718, OP_TABLE(fpu_dc_a16) sous 8087.h
    internal static readonly OpFn[] ops_808x_fpu_dc_a16 =
    [
        opFADDd_a16, opFMULd_a16, opFCOMd_a16, opFCOMPd_a16, opFSUBd_a16, opFSUBRd_a16, opFDIVd_a16, opFDIVRd_a16,
        opFADDd_a16, opFMULd_a16, opFCOMd_a16, opFCOMPd_a16, opFSUBd_a16, opFSUBRd_a16, opFDIVd_a16, opFDIVRd_a16,
        opFADDd_a16, opFMULd_a16, opFCOMd_a16, opFCOMPd_a16, opFSUBd_a16, opFSUBRd_a16, opFDIVd_a16, opFDIVRd_a16,
        opFADDr, opFMULr, opFCOM, opFCOMP, opFSUBRr, opFSUBr, opFDIVRr, opFDIVr,
    ];

    // pcem: x87_ops.h:726-762, OP_TABLE(fpu_dd_a16) sous 8087.h
    internal static readonly OpFn[] ops_808x_fpu_dd_a16 =
    [
        opFLDd_a16, opFLDd_a16, opFLDd_a16, opFLDd_a16, opFLDd_a16, opFLDd_a16, opFLDd_a16, opFLDd_a16,
        FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16,
        opFSTd_a16, opFSTd_a16, opFSTd_a16, opFSTd_a16, opFSTd_a16, opFSTd_a16, opFSTd_a16, opFSTd_a16,
        opFSTPd_a16, opFSTPd_a16, opFSTPd_a16, opFSTPd_a16, opFSTPd_a16, opFSTPd_a16, opFSTPd_a16, opFSTPd_a16,
        opFSTOR_a16, opFSTOR_a16, opFSTOR_a16, opFSTOR_a16, opFSTOR_a16, opFSTOR_a16, opFSTOR_a16, opFSTOR_a16,
        FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16,
        opFSAVE_a16, opFSAVE_a16, opFSAVE_a16, opFSAVE_a16, opFSAVE_a16, opFSAVE_a16, opFSAVE_a16, opFSAVE_a16,
        opFSTSW_a16, opFSTSW_a16, opFSTSW_a16, opFSTSW_a16, opFSTSW_a16, opFSTSW_a16, opFSTSW_a16, opFSTSW_a16,
        opFLDd_a16, opFLDd_a16, opFLDd_a16, opFLDd_a16, opFLDd_a16, opFLDd_a16, opFLDd_a16, opFLDd_a16,
        FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16,
        opFSTd_a16, opFSTd_a16, opFSTd_a16, opFSTd_a16, opFSTd_a16, opFSTd_a16, opFSTd_a16, opFSTd_a16,
        opFSTPd_a16, opFSTPd_a16, opFSTPd_a16, opFSTPd_a16, opFSTPd_a16, opFSTPd_a16, opFSTPd_a16, opFSTPd_a16,
        opFSTOR_a16, opFSTOR_a16, opFSTOR_a16, opFSTOR_a16, opFSTOR_a16, opFSTOR_a16, opFSTOR_a16, opFSTOR_a16,
        FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16,
        opFSAVE_a16, opFSAVE_a16, opFSAVE_a16, opFSAVE_a16, opFSAVE_a16, opFSAVE_a16, opFSAVE_a16, opFSAVE_a16,
        opFSTSW_a16, opFSTSW_a16, opFSTSW_a16, opFSTSW_a16, opFSTSW_a16, opFSTSW_a16, opFSTSW_a16, opFSTSW_a16,
        opFLDd_a16, opFLDd_a16, opFLDd_a16, opFLDd_a16, opFLDd_a16, opFLDd_a16, opFLDd_a16, opFLDd_a16,
        FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16,
        opFSTd_a16, opFSTd_a16, opFSTd_a16, opFSTd_a16, opFSTd_a16, opFSTd_a16, opFSTd_a16, opFSTd_a16,
        opFSTPd_a16, opFSTPd_a16, opFSTPd_a16, opFSTPd_a16, opFSTPd_a16, opFSTPd_a16, opFSTPd_a16, opFSTPd_a16,
        opFSTOR_a16, opFSTOR_a16, opFSTOR_a16, opFSTOR_a16, opFSTOR_a16, opFSTOR_a16, opFSTOR_a16, opFSTOR_a16,
        FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16,
        opFSAVE_a16, opFSAVE_a16, opFSAVE_a16, opFSAVE_a16, opFSAVE_a16, opFSAVE_a16, opFSAVE_a16, opFSAVE_a16,
        opFSTSW_a16, opFSTSW_a16, opFSTSW_a16, opFSTSW_a16, opFSTSW_a16, opFSTSW_a16, opFSTSW_a16, opFSTSW_a16,
        opFFREE, opFFREE, opFFREE, opFFREE, opFFREE, opFFREE, opFFREE, opFFREE,
        // pcem bug, reproduced: PB-210 — DD C8-CF (FXCH4, alias non documenté de FXCH) : ILLEGAL.
        FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16,
        opFST, opFST, opFST, opFST, opFST, opFST, opFST, opFST,
        opFSTP, opFSTP, opFSTP, opFSTP, opFSTP, opFSTP, opFSTP, opFSTP,
        // pcem bug, reproduced: PB-209 — FUCOM et FUCOMP (DD E0-EF), du 387, s'exécutent sur le 8087.
        opFUCOM, opFUCOM, opFUCOM, opFUCOM, opFUCOM, opFUCOM, opFUCOM, opFUCOM,
        opFUCOMP, opFUCOMP, opFUCOMP, opFUCOMP, opFUCOMP, opFUCOMP, opFUCOMP, opFUCOMP,
        FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16,
        FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16,
    ];

    // pcem: x87_ops.h:805-841, OP_TABLE(fpu_de_a16) sous 8087.h
    internal static readonly OpFn[] ops_808x_fpu_de_a16 =
    [
        opFADDiw_a16, opFADDiw_a16, opFADDiw_a16, opFADDiw_a16, opFADDiw_a16, opFADDiw_a16, opFADDiw_a16, opFADDiw_a16,
        opFMULiw_a16, opFMULiw_a16, opFMULiw_a16, opFMULiw_a16, opFMULiw_a16, opFMULiw_a16, opFMULiw_a16, opFMULiw_a16,
        opFCOMiw_a16, opFCOMiw_a16, opFCOMiw_a16, opFCOMiw_a16, opFCOMiw_a16, opFCOMiw_a16, opFCOMiw_a16, opFCOMiw_a16,
        opFCOMPiw_a16, opFCOMPiw_a16, opFCOMPiw_a16, opFCOMPiw_a16, opFCOMPiw_a16, opFCOMPiw_a16, opFCOMPiw_a16, opFCOMPiw_a16,
        opFSUBiw_a16, opFSUBiw_a16, opFSUBiw_a16, opFSUBiw_a16, opFSUBiw_a16, opFSUBiw_a16, opFSUBiw_a16, opFSUBiw_a16,
        opFSUBRiw_a16, opFSUBRiw_a16, opFSUBRiw_a16, opFSUBRiw_a16, opFSUBRiw_a16, opFSUBRiw_a16, opFSUBRiw_a16, opFSUBRiw_a16,
        opFDIViw_a16, opFDIViw_a16, opFDIViw_a16, opFDIViw_a16, opFDIViw_a16, opFDIViw_a16, opFDIViw_a16, opFDIViw_a16,
        opFDIVRiw_a16, opFDIVRiw_a16, opFDIVRiw_a16, opFDIVRiw_a16, opFDIVRiw_a16, opFDIVRiw_a16, opFDIVRiw_a16, opFDIVRiw_a16,
        opFADDiw_a16, opFADDiw_a16, opFADDiw_a16, opFADDiw_a16, opFADDiw_a16, opFADDiw_a16, opFADDiw_a16, opFADDiw_a16,
        opFMULiw_a16, opFMULiw_a16, opFMULiw_a16, opFMULiw_a16, opFMULiw_a16, opFMULiw_a16, opFMULiw_a16, opFMULiw_a16,
        opFCOMiw_a16, opFCOMiw_a16, opFCOMiw_a16, opFCOMiw_a16, opFCOMiw_a16, opFCOMiw_a16, opFCOMiw_a16, opFCOMiw_a16,
        opFCOMPiw_a16, opFCOMPiw_a16, opFCOMPiw_a16, opFCOMPiw_a16, opFCOMPiw_a16, opFCOMPiw_a16, opFCOMPiw_a16, opFCOMPiw_a16,
        opFSUBiw_a16, opFSUBiw_a16, opFSUBiw_a16, opFSUBiw_a16, opFSUBiw_a16, opFSUBiw_a16, opFSUBiw_a16, opFSUBiw_a16,
        opFSUBRiw_a16, opFSUBRiw_a16, opFSUBRiw_a16, opFSUBRiw_a16, opFSUBRiw_a16, opFSUBRiw_a16, opFSUBRiw_a16, opFSUBRiw_a16,
        opFDIViw_a16, opFDIViw_a16, opFDIViw_a16, opFDIViw_a16, opFDIViw_a16, opFDIViw_a16, opFDIViw_a16, opFDIViw_a16,
        opFDIVRiw_a16, opFDIVRiw_a16, opFDIVRiw_a16, opFDIVRiw_a16, opFDIVRiw_a16, opFDIVRiw_a16, opFDIVRiw_a16, opFDIVRiw_a16,
        opFADDiw_a16, opFADDiw_a16, opFADDiw_a16, opFADDiw_a16, opFADDiw_a16, opFADDiw_a16, opFADDiw_a16, opFADDiw_a16,
        opFMULiw_a16, opFMULiw_a16, opFMULiw_a16, opFMULiw_a16, opFMULiw_a16, opFMULiw_a16, opFMULiw_a16, opFMULiw_a16,
        opFCOMiw_a16, opFCOMiw_a16, opFCOMiw_a16, opFCOMiw_a16, opFCOMiw_a16, opFCOMiw_a16, opFCOMiw_a16, opFCOMiw_a16,
        opFCOMPiw_a16, opFCOMPiw_a16, opFCOMPiw_a16, opFCOMPiw_a16, opFCOMPiw_a16, opFCOMPiw_a16, opFCOMPiw_a16, opFCOMPiw_a16,
        opFSUBiw_a16, opFSUBiw_a16, opFSUBiw_a16, opFSUBiw_a16, opFSUBiw_a16, opFSUBiw_a16, opFSUBiw_a16, opFSUBiw_a16,
        opFSUBRiw_a16, opFSUBRiw_a16, opFSUBRiw_a16, opFSUBRiw_a16, opFSUBRiw_a16, opFSUBRiw_a16, opFSUBRiw_a16, opFSUBRiw_a16,
        opFDIViw_a16, opFDIViw_a16, opFDIViw_a16, opFDIViw_a16, opFDIViw_a16, opFDIViw_a16, opFDIViw_a16, opFDIViw_a16,
        opFDIVRiw_a16, opFDIVRiw_a16, opFDIVRiw_a16, opFDIVRiw_a16, opFDIVRiw_a16, opFDIVRiw_a16, opFDIVRiw_a16, opFDIVRiw_a16,
        opFADDP, opFADDP, opFADDP, opFADDP, opFADDP, opFADDP, opFADDP, opFADDP,
        opFMULP, opFMULP, opFMULP, opFMULP, opFMULP, opFMULP, opFMULP, opFMULP,
        // pcem bug, reproduced: PB-210 — DE D0-D7 (FCOMP5, alias non documenté de FCOMP) : ILLEGAL.
        FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16,
        FPU_ILLEGAL_a16, opFCOMPP, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16,
        opFSUBRP, opFSUBRP, opFSUBRP, opFSUBRP, opFSUBRP, opFSUBRP, opFSUBRP, opFSUBRP,
        opFSUBP, opFSUBP, opFSUBP, opFSUBP, opFSUBP, opFSUBP, opFSUBP, opFSUBP,
        opFDIVRP, opFDIVRP, opFDIVRP, opFDIVRP, opFDIVRP, opFDIVRP, opFDIVRP, opFDIVRP,
        opFDIVP, opFDIVP, opFDIVP, opFDIVP, opFDIVP, opFDIVP, opFDIVP, opFDIVP,
    ];

    // pcem: x87_ops.h:884-920, OP_TABLE(fpu_df_a16) sous 8087.h
    internal static readonly OpFn[] ops_808x_fpu_df_a16 =
    [
        opFILDiw_a16, opFILDiw_a16, opFILDiw_a16, opFILDiw_a16, opFILDiw_a16, opFILDiw_a16, opFILDiw_a16, opFILDiw_a16,
        FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16,
        opFISTiw_a16, opFISTiw_a16, opFISTiw_a16, opFISTiw_a16, opFISTiw_a16, opFISTiw_a16, opFISTiw_a16, opFISTiw_a16,
        opFISTPiw_a16, opFISTPiw_a16, opFISTPiw_a16, opFISTPiw_a16, opFISTPiw_a16, opFISTPiw_a16, opFISTPiw_a16, opFISTPiw_a16,
        // pcem bug, reproduced: PB-52 — DF /4 (FBLD m80bcd) est ILLEGAL : rien n'est chargé ni poussé.
        FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16,
        opFILDiq_a16, opFILDiq_a16, opFILDiq_a16, opFILDiq_a16, opFILDiq_a16, opFILDiq_a16, opFILDiq_a16, opFILDiq_a16,
        FBSTP_a16, FBSTP_a16, FBSTP_a16, FBSTP_a16, FBSTP_a16, FBSTP_a16, FBSTP_a16, FBSTP_a16,
        FISTPiq_a16, FISTPiq_a16, FISTPiq_a16, FISTPiq_a16, FISTPiq_a16, FISTPiq_a16, FISTPiq_a16, FISTPiq_a16,
        opFILDiw_a16, opFILDiw_a16, opFILDiw_a16, opFILDiw_a16, opFILDiw_a16, opFILDiw_a16, opFILDiw_a16, opFILDiw_a16,
        FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16,
        opFISTiw_a16, opFISTiw_a16, opFISTiw_a16, opFISTiw_a16, opFISTiw_a16, opFISTiw_a16, opFISTiw_a16, opFISTiw_a16,
        opFISTPiw_a16, opFISTPiw_a16, opFISTPiw_a16, opFISTPiw_a16, opFISTPiw_a16, opFISTPiw_a16, opFISTPiw_a16, opFISTPiw_a16,
        // pcem bug, reproduced: PB-52 — DF /4 (FBLD m80bcd) est ILLEGAL : rien n'est chargé ni poussé.
        FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16,
        opFILDiq_a16, opFILDiq_a16, opFILDiq_a16, opFILDiq_a16, opFILDiq_a16, opFILDiq_a16, opFILDiq_a16, opFILDiq_a16,
        FBSTP_a16, FBSTP_a16, FBSTP_a16, FBSTP_a16, FBSTP_a16, FBSTP_a16, FBSTP_a16, FBSTP_a16,
        FISTPiq_a16, FISTPiq_a16, FISTPiq_a16, FISTPiq_a16, FISTPiq_a16, FISTPiq_a16, FISTPiq_a16, FISTPiq_a16,
        opFILDiw_a16, opFILDiw_a16, opFILDiw_a16, opFILDiw_a16, opFILDiw_a16, opFILDiw_a16, opFILDiw_a16, opFILDiw_a16,
        FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16,
        opFISTiw_a16, opFISTiw_a16, opFISTiw_a16, opFISTiw_a16, opFISTiw_a16, opFISTiw_a16, opFISTiw_a16, opFISTiw_a16,
        opFISTPiw_a16, opFISTPiw_a16, opFISTPiw_a16, opFISTPiw_a16, opFISTPiw_a16, opFISTPiw_a16, opFISTPiw_a16, opFISTPiw_a16,
        // pcem bug, reproduced: PB-52 — DF /4 (FBLD m80bcd) est ILLEGAL : rien n'est chargé ni poussé.
        FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16,
        opFILDiq_a16, opFILDiq_a16, opFILDiq_a16, opFILDiq_a16, opFILDiq_a16, opFILDiq_a16, opFILDiq_a16, opFILDiq_a16,
        FBSTP_a16, FBSTP_a16, FBSTP_a16, FBSTP_a16, FBSTP_a16, FBSTP_a16, FBSTP_a16, FBSTP_a16,
        FISTPiq_a16, FISTPiq_a16, FISTPiq_a16, FISTPiq_a16, FISTPiq_a16, FISTPiq_a16, FISTPiq_a16, FISTPiq_a16,
        // pcem bug, reproduced: PB-210 — DF C0-DF (FFREEP et les alias FXCH7, FSTP8, FSTP9) : ILLEGAL.
        FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16,
        FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16,
        FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16,
        FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16,
        // pcem bug, reproduced: PB-200 — DF E0 (FNSTSW AX, une instruction du 287) écrit AX sur le 8087.
        opFSTSW_AX, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16,
        FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16,
        FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16,
        FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16,
    ];
}
