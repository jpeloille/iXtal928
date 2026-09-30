// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/includes/private/cpu/x87_ops.h  (les tables, lignes 310-1040)
// SHA256: voir oracle.tsv ; vérifier avec tools/check-oracle.sh
// STATUS: partial — G4.3 : les seize tables d'échappement non 686 (D8 à DF, a16 et a32),
//         GÉNÉRÉES verbatim depuis le C, emplacement par emplacement, `ILLEGAL` résolu selon
//         le #define en cours (FPU_ILLEGAL_a16 ou _a32). Les tables `_686_` (FCMOV, FCOMI) et
//         nofpu sont hors de portée ou ailleurs (386_ops_fpu.cs).
//
// Les handlers que les tables nomment et qui ne sont pas encore transcrits sont posés en
// fin de fichier sous LEUR NOM, en souches qui s'arrêtent bruyamment (opX87NonTranscrit) :
// G4.4 (x87_ops_misc.h hors transcendantes) et G4.5 (les transcendantes) les remplaceront un
// par un, et les tables, elles, ne bougeront plus.

namespace iXtal26.Cpu;

internal static partial class _386
{
    // pcem: x87_ops.h:310-314
    private static OpFn[] Table_fpu_d8_a16() =>
    [
        opFADDs_a16, opFMULs_a16, opFCOMs_a16, opFCOMPs_a16, opFSUBs_a16, opFSUBRs_a16, opFDIVs_a16, opFDIVRs_a16,
        opFADDs_a16, opFMULs_a16, opFCOMs_a16, opFCOMPs_a16, opFSUBs_a16, opFSUBRs_a16, opFDIVs_a16, opFDIVRs_a16,
        opFADDs_a16, opFMULs_a16, opFCOMs_a16, opFCOMPs_a16, opFSUBs_a16, opFSUBRs_a16, opFDIVs_a16, opFDIVRs_a16,
        opFADD, opFMUL, opFCOM, opFCOMP, opFSUB, opFSUBR, opFDIV, opFDIVR,
    ];

    // pcem: x87_ops.h:315-319
    private static OpFn[] Table_fpu_d8_a32() =>
    [
        opFADDs_a32, opFMULs_a32, opFCOMs_a32, opFCOMPs_a32, opFSUBs_a32, opFSUBRs_a32, opFDIVs_a32, opFDIVRs_a32,
        opFADDs_a32, opFMULs_a32, opFCOMs_a32, opFCOMPs_a32, opFSUBs_a32, opFSUBRs_a32, opFDIVs_a32, opFDIVRs_a32,
        opFADDs_a32, opFMULs_a32, opFCOMs_a32, opFCOMPs_a32, opFSUBs_a32, opFSUBRs_a32, opFDIVs_a32, opFDIVRs_a32,
        opFADD, opFMUL, opFCOM, opFCOMP, opFSUB, opFSUBR, opFDIV, opFDIVR,
    ];

    // pcem: x87_ops.h:322-357
    private static OpFn[] Table_fpu_d9_a16() =>
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
        opF2XM1, opFYL2X, opFPTAN, opFPATAN, FPU_ILLEGAL_a16, opFPREM1, opFDECSTP, opFINCSTP,
        opFPREM, opFYL2XP1, opFSQRT, opFSINCOS, opFRNDINT, opFSCALE, opFSIN, opFCOS,
    ];

    // pcem: x87_ops.h:360-395
    private static OpFn[] Table_fpu_d9_a32() =>
    [
        opFLDs_a32, opFLDs_a32, opFLDs_a32, opFLDs_a32, opFLDs_a32, opFLDs_a32, opFLDs_a32, opFLDs_a32,
        FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32,
        opFSTs_a32, opFSTs_a32, opFSTs_a32, opFSTs_a32, opFSTs_a32, opFSTs_a32, opFSTs_a32, opFSTs_a32,
        opFSTPs_a32, opFSTPs_a32, opFSTPs_a32, opFSTPs_a32, opFSTPs_a32, opFSTPs_a32, opFSTPs_a32, opFSTPs_a32,
        opFLDENV_a32, opFLDENV_a32, opFLDENV_a32, opFLDENV_a32, opFLDENV_a32, opFLDENV_a32, opFLDENV_a32, opFLDENV_a32,
        opFLDCW_a32, opFLDCW_a32, opFLDCW_a32, opFLDCW_a32, opFLDCW_a32, opFLDCW_a32, opFLDCW_a32, opFLDCW_a32,
        opFSTENV_a32, opFSTENV_a32, opFSTENV_a32, opFSTENV_a32, opFSTENV_a32, opFSTENV_a32, opFSTENV_a32, opFSTENV_a32,
        opFSTCW_a32, opFSTCW_a32, opFSTCW_a32, opFSTCW_a32, opFSTCW_a32, opFSTCW_a32, opFSTCW_a32, opFSTCW_a32,
        opFLDs_a32, opFLDs_a32, opFLDs_a32, opFLDs_a32, opFLDs_a32, opFLDs_a32, opFLDs_a32, opFLDs_a32,
        FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32,
        opFSTs_a32, opFSTs_a32, opFSTs_a32, opFSTs_a32, opFSTs_a32, opFSTs_a32, opFSTs_a32, opFSTs_a32,
        opFSTPs_a32, opFSTPs_a32, opFSTPs_a32, opFSTPs_a32, opFSTPs_a32, opFSTPs_a32, opFSTPs_a32, opFSTPs_a32,
        opFLDENV_a32, opFLDENV_a32, opFLDENV_a32, opFLDENV_a32, opFLDENV_a32, opFLDENV_a32, opFLDENV_a32, opFLDENV_a32,
        opFLDCW_a32, opFLDCW_a32, opFLDCW_a32, opFLDCW_a32, opFLDCW_a32, opFLDCW_a32, opFLDCW_a32, opFLDCW_a32,
        opFSTENV_a32, opFSTENV_a32, opFSTENV_a32, opFSTENV_a32, opFSTENV_a32, opFSTENV_a32, opFSTENV_a32, opFSTENV_a32,
        opFSTCW_a32, opFSTCW_a32, opFSTCW_a32, opFSTCW_a32, opFSTCW_a32, opFSTCW_a32, opFSTCW_a32, opFSTCW_a32,
        opFLDs_a32, opFLDs_a32, opFLDs_a32, opFLDs_a32, opFLDs_a32, opFLDs_a32, opFLDs_a32, opFLDs_a32,
        FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32,
        opFSTs_a32, opFSTs_a32, opFSTs_a32, opFSTs_a32, opFSTs_a32, opFSTs_a32, opFSTs_a32, opFSTs_a32,
        opFSTPs_a32, opFSTPs_a32, opFSTPs_a32, opFSTPs_a32, opFSTPs_a32, opFSTPs_a32, opFSTPs_a32, opFSTPs_a32,
        opFLDENV_a32, opFLDENV_a32, opFLDENV_a32, opFLDENV_a32, opFLDENV_a32, opFLDENV_a32, opFLDENV_a32, opFLDENV_a32,
        opFLDCW_a32, opFLDCW_a32, opFLDCW_a32, opFLDCW_a32, opFLDCW_a32, opFLDCW_a32, opFLDCW_a32, opFLDCW_a32,
        opFSTENV_a32, opFSTENV_a32, opFSTENV_a32, opFSTENV_a32, opFSTENV_a32, opFSTENV_a32, opFSTENV_a32, opFSTENV_a32,
        opFSTCW_a32, opFSTCW_a32, opFSTCW_a32, opFSTCW_a32, opFSTCW_a32, opFSTCW_a32, opFSTCW_a32, opFSTCW_a32,
        opFLD, opFLD, opFLD, opFLD, opFLD, opFLD, opFLD, opFLD,
        opFXCH, opFXCH, opFXCH, opFXCH, opFXCH, opFXCH, opFXCH, opFXCH,
        opFNOP, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32,
        opFSTP, opFSTP, opFSTP, opFSTP, opFSTP, opFSTP, opFSTP, opFSTP,
        opFCHS, opFABS, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, opFTST, opFXAM, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32,
        opFLD1, opFLDL2T, opFLDL2E, opFLDPI, opFLDEG2, opFLDLN2, opFLDZ, FPU_ILLEGAL_a32,
        opF2XM1, opFYL2X, opFPTAN, opFPATAN, FPU_ILLEGAL_a32, opFPREM1, opFDECSTP, opFINCSTP,
        opFPREM, opFYL2XP1, opFSQRT, opFSINCOS, opFRNDINT, opFSCALE, opFSIN, opFCOS,
    ];

    // pcem: x87_ops.h:399-435
    private static OpFn[] Table_fpu_da_a16() =>
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
        FPU_ILLEGAL_a16, opFUCOMPP, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16,
        FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16,
        FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16,
    ];

    // pcem: x87_ops.h:438-474
    private static OpFn[] Table_fpu_da_a32() =>
    [
        opFADDil_a32, opFADDil_a32, opFADDil_a32, opFADDil_a32, opFADDil_a32, opFADDil_a32, opFADDil_a32, opFADDil_a32,
        opFMULil_a32, opFMULil_a32, opFMULil_a32, opFMULil_a32, opFMULil_a32, opFMULil_a32, opFMULil_a32, opFMULil_a32,
        opFCOMil_a32, opFCOMil_a32, opFCOMil_a32, opFCOMil_a32, opFCOMil_a32, opFCOMil_a32, opFCOMil_a32, opFCOMil_a32,
        opFCOMPil_a32, opFCOMPil_a32, opFCOMPil_a32, opFCOMPil_a32, opFCOMPil_a32, opFCOMPil_a32, opFCOMPil_a32, opFCOMPil_a32,
        opFSUBil_a32, opFSUBil_a32, opFSUBil_a32, opFSUBil_a32, opFSUBil_a32, opFSUBil_a32, opFSUBil_a32, opFSUBil_a32,
        opFSUBRil_a32, opFSUBRil_a32, opFSUBRil_a32, opFSUBRil_a32, opFSUBRil_a32, opFSUBRil_a32, opFSUBRil_a32, opFSUBRil_a32,
        opFDIVil_a32, opFDIVil_a32, opFDIVil_a32, opFDIVil_a32, opFDIVil_a32, opFDIVil_a32, opFDIVil_a32, opFDIVil_a32,
        opFDIVRil_a32, opFDIVRil_a32, opFDIVRil_a32, opFDIVRil_a32, opFDIVRil_a32, opFDIVRil_a32, opFDIVRil_a32, opFDIVRil_a32,
        opFADDil_a32, opFADDil_a32, opFADDil_a32, opFADDil_a32, opFADDil_a32, opFADDil_a32, opFADDil_a32, opFADDil_a32,
        opFMULil_a32, opFMULil_a32, opFMULil_a32, opFMULil_a32, opFMULil_a32, opFMULil_a32, opFMULil_a32, opFMULil_a32,
        opFCOMil_a32, opFCOMil_a32, opFCOMil_a32, opFCOMil_a32, opFCOMil_a32, opFCOMil_a32, opFCOMil_a32, opFCOMil_a32,
        opFCOMPil_a32, opFCOMPil_a32, opFCOMPil_a32, opFCOMPil_a32, opFCOMPil_a32, opFCOMPil_a32, opFCOMPil_a32, opFCOMPil_a32,
        opFSUBil_a32, opFSUBil_a32, opFSUBil_a32, opFSUBil_a32, opFSUBil_a32, opFSUBil_a32, opFSUBil_a32, opFSUBil_a32,
        opFSUBRil_a32, opFSUBRil_a32, opFSUBRil_a32, opFSUBRil_a32, opFSUBRil_a32, opFSUBRil_a32, opFSUBRil_a32, opFSUBRil_a32,
        opFDIVil_a32, opFDIVil_a32, opFDIVil_a32, opFDIVil_a32, opFDIVil_a32, opFDIVil_a32, opFDIVil_a32, opFDIVil_a32,
        opFDIVRil_a32, opFDIVRil_a32, opFDIVRil_a32, opFDIVRil_a32, opFDIVRil_a32, opFDIVRil_a32, opFDIVRil_a32, opFDIVRil_a32,
        opFADDil_a32, opFADDil_a32, opFADDil_a32, opFADDil_a32, opFADDil_a32, opFADDil_a32, opFADDil_a32, opFADDil_a32,
        opFMULil_a32, opFMULil_a32, opFMULil_a32, opFMULil_a32, opFMULil_a32, opFMULil_a32, opFMULil_a32, opFMULil_a32,
        opFCOMil_a32, opFCOMil_a32, opFCOMil_a32, opFCOMil_a32, opFCOMil_a32, opFCOMil_a32, opFCOMil_a32, opFCOMil_a32,
        opFCOMPil_a32, opFCOMPil_a32, opFCOMPil_a32, opFCOMPil_a32, opFCOMPil_a32, opFCOMPil_a32, opFCOMPil_a32, opFCOMPil_a32,
        opFSUBil_a32, opFSUBil_a32, opFSUBil_a32, opFSUBil_a32, opFSUBil_a32, opFSUBil_a32, opFSUBil_a32, opFSUBil_a32,
        opFSUBRil_a32, opFSUBRil_a32, opFSUBRil_a32, opFSUBRil_a32, opFSUBRil_a32, opFSUBRil_a32, opFSUBRil_a32, opFSUBRil_a32,
        opFDIVil_a32, opFDIVil_a32, opFDIVil_a32, opFDIVil_a32, opFDIVil_a32, opFDIVil_a32, opFDIVil_a32, opFDIVil_a32,
        opFDIVRil_a32, opFDIVRil_a32, opFDIVRil_a32, opFDIVRil_a32, opFDIVRil_a32, opFDIVRil_a32, opFDIVRil_a32, opFDIVRil_a32,
        FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32,
        FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32,
        FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32,
        FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32,
        FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32,
        FPU_ILLEGAL_a32, opFUCOMPP, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32,
        FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32,
        FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32,
    ];

    // pcem: x87_ops.h:557-593
    private static OpFn[] Table_fpu_db_a16() =>
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

    // pcem: x87_ops.h:596-632
    private static OpFn[] Table_fpu_db_a32() =>
    [
        opFILDil_a32, opFILDil_a32, opFILDil_a32, opFILDil_a32, opFILDil_a32, opFILDil_a32, opFILDil_a32, opFILDil_a32,
        FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32,
        opFISTil_a32, opFISTil_a32, opFISTil_a32, opFISTil_a32, opFISTil_a32, opFISTil_a32, opFISTil_a32, opFISTil_a32,
        opFISTPil_a32, opFISTPil_a32, opFISTPil_a32, opFISTPil_a32, opFISTPil_a32, opFISTPil_a32, opFISTPil_a32, opFISTPil_a32,
        FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32,
        opFLDe_a32, opFLDe_a32, opFLDe_a32, opFLDe_a32, opFLDe_a32, opFLDe_a32, opFLDe_a32, opFLDe_a32,
        FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32,
        opFSTPe_a32, opFSTPe_a32, opFSTPe_a32, opFSTPe_a32, opFSTPe_a32, opFSTPe_a32, opFSTPe_a32, opFSTPe_a32,
        opFILDil_a32, opFILDil_a32, opFILDil_a32, opFILDil_a32, opFILDil_a32, opFILDil_a32, opFILDil_a32, opFILDil_a32,
        FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32,
        opFISTil_a32, opFISTil_a32, opFISTil_a32, opFISTil_a32, opFISTil_a32, opFISTil_a32, opFISTil_a32, opFISTil_a32,
        opFISTPil_a32, opFISTPil_a32, opFISTPil_a32, opFISTPil_a32, opFISTPil_a32, opFISTPil_a32, opFISTPil_a32, opFISTPil_a32,
        FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32,
        opFLDe_a32, opFLDe_a32, opFLDe_a32, opFLDe_a32, opFLDe_a32, opFLDe_a32, opFLDe_a32, opFLDe_a32,
        FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32,
        opFSTPe_a32, opFSTPe_a32, opFSTPe_a32, opFSTPe_a32, opFSTPe_a32, opFSTPe_a32, opFSTPe_a32, opFSTPe_a32,
        opFILDil_a32, opFILDil_a32, opFILDil_a32, opFILDil_a32, opFILDil_a32, opFILDil_a32, opFILDil_a32, opFILDil_a32,
        FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32,
        opFISTil_a32, opFISTil_a32, opFISTil_a32, opFISTil_a32, opFISTil_a32, opFISTil_a32, opFISTil_a32, opFISTil_a32,
        opFISTPil_a32, opFISTPil_a32, opFISTPil_a32, opFISTPil_a32, opFISTPil_a32, opFISTPil_a32, opFISTPil_a32, opFISTPil_a32,
        FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32,
        opFLDe_a32, opFLDe_a32, opFLDe_a32, opFLDe_a32, opFLDe_a32, opFLDe_a32, opFLDe_a32, opFLDe_a32,
        FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32,
        opFSTPe_a32, opFSTPe_a32, opFSTPe_a32, opFSTPe_a32, opFSTPe_a32, opFSTPe_a32, opFSTPe_a32, opFSTPe_a32,
        FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32,
        FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32,
        FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32,
        FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32,
        opFENI, opFDISI, opFCLEX, opFINIT, opFNOP, opFNOP, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32,
        FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32,
        FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32,
        FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32,
    ];

    // pcem: x87_ops.h:714-718
    private static OpFn[] Table_fpu_dc_a16() =>
    [
        opFADDd_a16, opFMULd_a16, opFCOMd_a16, opFCOMPd_a16, opFSUBd_a16, opFSUBRd_a16, opFDIVd_a16, opFDIVRd_a16,
        opFADDd_a16, opFMULd_a16, opFCOMd_a16, opFCOMPd_a16, opFSUBd_a16, opFSUBRd_a16, opFDIVd_a16, opFDIVRd_a16,
        opFADDd_a16, opFMULd_a16, opFCOMd_a16, opFCOMPd_a16, opFSUBd_a16, opFSUBRd_a16, opFDIVd_a16, opFDIVRd_a16,
        opFADDr, opFMULr, opFCOM, opFCOMP, opFSUBRr, opFSUBr, opFDIVRr, opFDIVr,
    ];

    // pcem: x87_ops.h:719-723
    private static OpFn[] Table_fpu_dc_a32() =>
    [
        opFADDd_a32, opFMULd_a32, opFCOMd_a32, opFCOMPd_a32, opFSUBd_a32, opFSUBRd_a32, opFDIVd_a32, opFDIVRd_a32,
        opFADDd_a32, opFMULd_a32, opFCOMd_a32, opFCOMPd_a32, opFSUBd_a32, opFSUBRd_a32, opFDIVd_a32, opFDIVRd_a32,
        opFADDd_a32, opFMULd_a32, opFCOMd_a32, opFCOMPd_a32, opFSUBd_a32, opFSUBRd_a32, opFDIVd_a32, opFDIVRd_a32,
        opFADDr, opFMULr, opFCOM, opFCOMP, opFSUBRr, opFSUBr, opFDIVRr, opFDIVr,
    ];

    // pcem: x87_ops.h:726-762
    private static OpFn[] Table_fpu_dd_a16() =>
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
        FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16,
        opFST, opFST, opFST, opFST, opFST, opFST, opFST, opFST,
        opFSTP, opFSTP, opFSTP, opFSTP, opFSTP, opFSTP, opFSTP, opFSTP,
        opFUCOM, opFUCOM, opFUCOM, opFUCOM, opFUCOM, opFUCOM, opFUCOM, opFUCOM,
        opFUCOMP, opFUCOMP, opFUCOMP, opFUCOMP, opFUCOMP, opFUCOMP, opFUCOMP, opFUCOMP,
        FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16,
        FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16,
    ];

    // pcem: x87_ops.h:765-801
    private static OpFn[] Table_fpu_dd_a32() =>
    [
        opFLDd_a32, opFLDd_a32, opFLDd_a32, opFLDd_a32, opFLDd_a32, opFLDd_a32, opFLDd_a32, opFLDd_a32,
        FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32,
        opFSTd_a32, opFSTd_a32, opFSTd_a32, opFSTd_a32, opFSTd_a32, opFSTd_a32, opFSTd_a32, opFSTd_a32,
        opFSTPd_a32, opFSTPd_a32, opFSTPd_a32, opFSTPd_a32, opFSTPd_a32, opFSTPd_a32, opFSTPd_a32, opFSTPd_a32,
        opFSTOR_a32, opFSTOR_a32, opFSTOR_a32, opFSTOR_a32, opFSTOR_a32, opFSTOR_a32, opFSTOR_a32, opFSTOR_a32,
        FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32,
        opFSAVE_a32, opFSAVE_a32, opFSAVE_a32, opFSAVE_a32, opFSAVE_a32, opFSAVE_a32, opFSAVE_a32, opFSAVE_a32,
        opFSTSW_a32, opFSTSW_a32, opFSTSW_a32, opFSTSW_a32, opFSTSW_a32, opFSTSW_a32, opFSTSW_a32, opFSTSW_a32,
        opFLDd_a32, opFLDd_a32, opFLDd_a32, opFLDd_a32, opFLDd_a32, opFLDd_a32, opFLDd_a32, opFLDd_a32,
        FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32,
        opFSTd_a32, opFSTd_a32, opFSTd_a32, opFSTd_a32, opFSTd_a32, opFSTd_a32, opFSTd_a32, opFSTd_a32,
        opFSTPd_a32, opFSTPd_a32, opFSTPd_a32, opFSTPd_a32, opFSTPd_a32, opFSTPd_a32, opFSTPd_a32, opFSTPd_a32,
        opFSTOR_a32, opFSTOR_a32, opFSTOR_a32, opFSTOR_a32, opFSTOR_a32, opFSTOR_a32, opFSTOR_a32, opFSTOR_a32,
        FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32,
        opFSAVE_a32, opFSAVE_a32, opFSAVE_a32, opFSAVE_a32, opFSAVE_a32, opFSAVE_a32, opFSAVE_a32, opFSAVE_a32,
        opFSTSW_a32, opFSTSW_a32, opFSTSW_a32, opFSTSW_a32, opFSTSW_a32, opFSTSW_a32, opFSTSW_a32, opFSTSW_a32,
        opFLDd_a32, opFLDd_a32, opFLDd_a32, opFLDd_a32, opFLDd_a32, opFLDd_a32, opFLDd_a32, opFLDd_a32,
        FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32,
        opFSTd_a32, opFSTd_a32, opFSTd_a32, opFSTd_a32, opFSTd_a32, opFSTd_a32, opFSTd_a32, opFSTd_a32,
        opFSTPd_a32, opFSTPd_a32, opFSTPd_a32, opFSTPd_a32, opFSTPd_a32, opFSTPd_a32, opFSTPd_a32, opFSTPd_a32,
        opFSTOR_a32, opFSTOR_a32, opFSTOR_a32, opFSTOR_a32, opFSTOR_a32, opFSTOR_a32, opFSTOR_a32, opFSTOR_a32,
        FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32,
        opFSAVE_a32, opFSAVE_a32, opFSAVE_a32, opFSAVE_a32, opFSAVE_a32, opFSAVE_a32, opFSAVE_a32, opFSAVE_a32,
        opFSTSW_a32, opFSTSW_a32, opFSTSW_a32, opFSTSW_a32, opFSTSW_a32, opFSTSW_a32, opFSTSW_a32, opFSTSW_a32,
        opFFREE, opFFREE, opFFREE, opFFREE, opFFREE, opFFREE, opFFREE, opFFREE,
        FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32,
        opFST, opFST, opFST, opFST, opFST, opFST, opFST, opFST,
        opFSTP, opFSTP, opFSTP, opFSTP, opFSTP, opFSTP, opFSTP, opFSTP,
        opFUCOM, opFUCOM, opFUCOM, opFUCOM, opFUCOM, opFUCOM, opFUCOM, opFUCOM,
        opFUCOMP, opFUCOMP, opFUCOMP, opFUCOMP, opFUCOMP, opFUCOMP, opFUCOMP, opFUCOMP,
        FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32,
        FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32,
    ];

    // pcem: x87_ops.h:805-841
    private static OpFn[] Table_fpu_de_a16() =>
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
        FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16,
        FPU_ILLEGAL_a16, opFCOMPP, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16,
        opFSUBRP, opFSUBRP, opFSUBRP, opFSUBRP, opFSUBRP, opFSUBRP, opFSUBRP, opFSUBRP,
        opFSUBP, opFSUBP, opFSUBP, opFSUBP, opFSUBP, opFSUBP, opFSUBP, opFSUBP,
        opFDIVRP, opFDIVRP, opFDIVRP, opFDIVRP, opFDIVRP, opFDIVRP, opFDIVRP, opFDIVRP,
        opFDIVP, opFDIVP, opFDIVP, opFDIVP, opFDIVP, opFDIVP, opFDIVP, opFDIVP,
    ];

    // pcem: x87_ops.h:844-880
    private static OpFn[] Table_fpu_de_a32() =>
    [
        opFADDiw_a32, opFADDiw_a32, opFADDiw_a32, opFADDiw_a32, opFADDiw_a32, opFADDiw_a32, opFADDiw_a32, opFADDiw_a32,
        opFMULiw_a32, opFMULiw_a32, opFMULiw_a32, opFMULiw_a32, opFMULiw_a32, opFMULiw_a32, opFMULiw_a32, opFMULiw_a32,
        opFCOMiw_a32, opFCOMiw_a32, opFCOMiw_a32, opFCOMiw_a32, opFCOMiw_a32, opFCOMiw_a32, opFCOMiw_a32, opFCOMiw_a32,
        opFCOMPiw_a32, opFCOMPiw_a32, opFCOMPiw_a32, opFCOMPiw_a32, opFCOMPiw_a32, opFCOMPiw_a32, opFCOMPiw_a32, opFCOMPiw_a32,
        opFSUBiw_a32, opFSUBiw_a32, opFSUBiw_a32, opFSUBiw_a32, opFSUBiw_a32, opFSUBiw_a32, opFSUBiw_a32, opFSUBiw_a32,
        opFSUBRiw_a32, opFSUBRiw_a32, opFSUBRiw_a32, opFSUBRiw_a32, opFSUBRiw_a32, opFSUBRiw_a32, opFSUBRiw_a32, opFSUBRiw_a32,
        opFDIViw_a32, opFDIViw_a32, opFDIViw_a32, opFDIViw_a32, opFDIViw_a32, opFDIViw_a32, opFDIViw_a32, opFDIViw_a32,
        opFDIVRiw_a32, opFDIVRiw_a32, opFDIVRiw_a32, opFDIVRiw_a32, opFDIVRiw_a32, opFDIVRiw_a32, opFDIVRiw_a32, opFDIVRiw_a32,
        opFADDiw_a32, opFADDiw_a32, opFADDiw_a32, opFADDiw_a32, opFADDiw_a32, opFADDiw_a32, opFADDiw_a32, opFADDiw_a32,
        opFMULiw_a32, opFMULiw_a32, opFMULiw_a32, opFMULiw_a32, opFMULiw_a32, opFMULiw_a32, opFMULiw_a32, opFMULiw_a32,
        opFCOMiw_a32, opFCOMiw_a32, opFCOMiw_a32, opFCOMiw_a32, opFCOMiw_a32, opFCOMiw_a32, opFCOMiw_a32, opFCOMiw_a32,
        opFCOMPiw_a32, opFCOMPiw_a32, opFCOMPiw_a32, opFCOMPiw_a32, opFCOMPiw_a32, opFCOMPiw_a32, opFCOMPiw_a32, opFCOMPiw_a32,
        opFSUBiw_a32, opFSUBiw_a32, opFSUBiw_a32, opFSUBiw_a32, opFSUBiw_a32, opFSUBiw_a32, opFSUBiw_a32, opFSUBiw_a32,
        opFSUBRiw_a32, opFSUBRiw_a32, opFSUBRiw_a32, opFSUBRiw_a32, opFSUBRiw_a32, opFSUBRiw_a32, opFSUBRiw_a32, opFSUBRiw_a32,
        opFDIViw_a32, opFDIViw_a32, opFDIViw_a32, opFDIViw_a32, opFDIViw_a32, opFDIViw_a32, opFDIViw_a32, opFDIViw_a32,
        opFDIVRiw_a32, opFDIVRiw_a32, opFDIVRiw_a32, opFDIVRiw_a32, opFDIVRiw_a32, opFDIVRiw_a32, opFDIVRiw_a32, opFDIVRiw_a32,
        opFADDiw_a32, opFADDiw_a32, opFADDiw_a32, opFADDiw_a32, opFADDiw_a32, opFADDiw_a32, opFADDiw_a32, opFADDiw_a32,
        opFMULiw_a32, opFMULiw_a32, opFMULiw_a32, opFMULiw_a32, opFMULiw_a32, opFMULiw_a32, opFMULiw_a32, opFMULiw_a32,
        opFCOMiw_a32, opFCOMiw_a32, opFCOMiw_a32, opFCOMiw_a32, opFCOMiw_a32, opFCOMiw_a32, opFCOMiw_a32, opFCOMiw_a32,
        opFCOMPiw_a32, opFCOMPiw_a32, opFCOMPiw_a32, opFCOMPiw_a32, opFCOMPiw_a32, opFCOMPiw_a32, opFCOMPiw_a32, opFCOMPiw_a32,
        opFSUBiw_a32, opFSUBiw_a32, opFSUBiw_a32, opFSUBiw_a32, opFSUBiw_a32, opFSUBiw_a32, opFSUBiw_a32, opFSUBiw_a32,
        opFSUBRiw_a32, opFSUBRiw_a32, opFSUBRiw_a32, opFSUBRiw_a32, opFSUBRiw_a32, opFSUBRiw_a32, opFSUBRiw_a32, opFSUBRiw_a32,
        opFDIViw_a32, opFDIViw_a32, opFDIViw_a32, opFDIViw_a32, opFDIViw_a32, opFDIViw_a32, opFDIViw_a32, opFDIViw_a32,
        opFDIVRiw_a32, opFDIVRiw_a32, opFDIVRiw_a32, opFDIVRiw_a32, opFDIVRiw_a32, opFDIVRiw_a32, opFDIVRiw_a32, opFDIVRiw_a32,
        opFADDP, opFADDP, opFADDP, opFADDP, opFADDP, opFADDP, opFADDP, opFADDP,
        opFMULP, opFMULP, opFMULP, opFMULP, opFMULP, opFMULP, opFMULP, opFMULP,
        FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32,
        FPU_ILLEGAL_a32, opFCOMPP, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32,
        opFSUBRP, opFSUBRP, opFSUBRP, opFSUBRP, opFSUBRP, opFSUBRP, opFSUBRP, opFSUBRP,
        opFSUBP, opFSUBP, opFSUBP, opFSUBP, opFSUBP, opFSUBP, opFSUBP, opFSUBP,
        opFDIVRP, opFDIVRP, opFDIVRP, opFDIVRP, opFDIVRP, opFDIVRP, opFDIVRP, opFDIVRP,
        opFDIVP, opFDIVP, opFDIVP, opFDIVP, opFDIVP, opFDIVP, opFDIVP, opFDIVP,
    ];

    // pcem: x87_ops.h:884-920
    private static OpFn[] Table_fpu_df_a16() =>
    [
        opFILDiw_a16, opFILDiw_a16, opFILDiw_a16, opFILDiw_a16, opFILDiw_a16, opFILDiw_a16, opFILDiw_a16, opFILDiw_a16,
        FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16,
        opFISTiw_a16, opFISTiw_a16, opFISTiw_a16, opFISTiw_a16, opFISTiw_a16, opFISTiw_a16, opFISTiw_a16, opFISTiw_a16,
        opFISTPiw_a16, opFISTPiw_a16, opFISTPiw_a16, opFISTPiw_a16, opFISTPiw_a16, opFISTPiw_a16, opFISTPiw_a16, opFISTPiw_a16,
        FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16,
        opFILDiq_a16, opFILDiq_a16, opFILDiq_a16, opFILDiq_a16, opFILDiq_a16, opFILDiq_a16, opFILDiq_a16, opFILDiq_a16,
        FBSTP_a16, FBSTP_a16, FBSTP_a16, FBSTP_a16, FBSTP_a16, FBSTP_a16, FBSTP_a16, FBSTP_a16,
        FISTPiq_a16, FISTPiq_a16, FISTPiq_a16, FISTPiq_a16, FISTPiq_a16, FISTPiq_a16, FISTPiq_a16, FISTPiq_a16,
        opFILDiw_a16, opFILDiw_a16, opFILDiw_a16, opFILDiw_a16, opFILDiw_a16, opFILDiw_a16, opFILDiw_a16, opFILDiw_a16,
        FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16,
        opFISTiw_a16, opFISTiw_a16, opFISTiw_a16, opFISTiw_a16, opFISTiw_a16, opFISTiw_a16, opFISTiw_a16, opFISTiw_a16,
        opFISTPiw_a16, opFISTPiw_a16, opFISTPiw_a16, opFISTPiw_a16, opFISTPiw_a16, opFISTPiw_a16, opFISTPiw_a16, opFISTPiw_a16,
        FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16,
        opFILDiq_a16, opFILDiq_a16, opFILDiq_a16, opFILDiq_a16, opFILDiq_a16, opFILDiq_a16, opFILDiq_a16, opFILDiq_a16,
        FBSTP_a16, FBSTP_a16, FBSTP_a16, FBSTP_a16, FBSTP_a16, FBSTP_a16, FBSTP_a16, FBSTP_a16,
        FISTPiq_a16, FISTPiq_a16, FISTPiq_a16, FISTPiq_a16, FISTPiq_a16, FISTPiq_a16, FISTPiq_a16, FISTPiq_a16,
        opFILDiw_a16, opFILDiw_a16, opFILDiw_a16, opFILDiw_a16, opFILDiw_a16, opFILDiw_a16, opFILDiw_a16, opFILDiw_a16,
        FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16,
        opFISTiw_a16, opFISTiw_a16, opFISTiw_a16, opFISTiw_a16, opFISTiw_a16, opFISTiw_a16, opFISTiw_a16, opFISTiw_a16,
        opFISTPiw_a16, opFISTPiw_a16, opFISTPiw_a16, opFISTPiw_a16, opFISTPiw_a16, opFISTPiw_a16, opFISTPiw_a16, opFISTPiw_a16,
        FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16,
        opFILDiq_a16, opFILDiq_a16, opFILDiq_a16, opFILDiq_a16, opFILDiq_a16, opFILDiq_a16, opFILDiq_a16, opFILDiq_a16,
        FBSTP_a16, FBSTP_a16, FBSTP_a16, FBSTP_a16, FBSTP_a16, FBSTP_a16, FBSTP_a16, FBSTP_a16,
        FISTPiq_a16, FISTPiq_a16, FISTPiq_a16, FISTPiq_a16, FISTPiq_a16, FISTPiq_a16, FISTPiq_a16, FISTPiq_a16,
        FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16,
        FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16,
        FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16,
        FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16,
        opFSTSW_AX, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16,
        FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16,
        FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16,
        FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16, FPU_ILLEGAL_a16,
    ];

    // pcem: x87_ops.h:923-959
    private static OpFn[] Table_fpu_df_a32() =>
    [
        opFILDiw_a32, opFILDiw_a32, opFILDiw_a32, opFILDiw_a32, opFILDiw_a32, opFILDiw_a32, opFILDiw_a32, opFILDiw_a32,
        FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32,
        opFISTiw_a32, opFISTiw_a32, opFISTiw_a32, opFISTiw_a32, opFISTiw_a32, opFISTiw_a32, opFISTiw_a32, opFISTiw_a32,
        opFISTPiw_a32, opFISTPiw_a32, opFISTPiw_a32, opFISTPiw_a32, opFISTPiw_a32, opFISTPiw_a32, opFISTPiw_a32, opFISTPiw_a32,
        FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32,
        opFILDiq_a32, opFILDiq_a32, opFILDiq_a32, opFILDiq_a32, opFILDiq_a32, opFILDiq_a32, opFILDiq_a32, opFILDiq_a32,
        FBSTP_a32, FBSTP_a32, FBSTP_a32, FBSTP_a32, FBSTP_a32, FBSTP_a32, FBSTP_a32, FBSTP_a32,
        FISTPiq_a32, FISTPiq_a32, FISTPiq_a32, FISTPiq_a32, FISTPiq_a32, FISTPiq_a32, FISTPiq_a32, FISTPiq_a32,
        opFILDiw_a32, opFILDiw_a32, opFILDiw_a32, opFILDiw_a32, opFILDiw_a32, opFILDiw_a32, opFILDiw_a32, opFILDiw_a32,
        FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32,
        opFISTiw_a32, opFISTiw_a32, opFISTiw_a32, opFISTiw_a32, opFISTiw_a32, opFISTiw_a32, opFISTiw_a32, opFISTiw_a32,
        opFISTPiw_a32, opFISTPiw_a32, opFISTPiw_a32, opFISTPiw_a32, opFISTPiw_a32, opFISTPiw_a32, opFISTPiw_a32, opFISTPiw_a32,
        FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32,
        opFILDiq_a32, opFILDiq_a32, opFILDiq_a32, opFILDiq_a32, opFILDiq_a32, opFILDiq_a32, opFILDiq_a32, opFILDiq_a32,
        FBSTP_a32, FBSTP_a32, FBSTP_a32, FBSTP_a32, FBSTP_a32, FBSTP_a32, FBSTP_a32, FBSTP_a32,
        FISTPiq_a32, FISTPiq_a32, FISTPiq_a32, FISTPiq_a32, FISTPiq_a32, FISTPiq_a32, FISTPiq_a32, FISTPiq_a32,
        opFILDiw_a32, opFILDiw_a32, opFILDiw_a32, opFILDiw_a32, opFILDiw_a32, opFILDiw_a32, opFILDiw_a32, opFILDiw_a32,
        FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32,
        opFISTiw_a32, opFISTiw_a32, opFISTiw_a32, opFISTiw_a32, opFISTiw_a32, opFISTiw_a32, opFISTiw_a32, opFISTiw_a32,
        opFISTPiw_a32, opFISTPiw_a32, opFISTPiw_a32, opFISTPiw_a32, opFISTPiw_a32, opFISTPiw_a32, opFISTPiw_a32, opFISTPiw_a32,
        FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32,
        opFILDiq_a32, opFILDiq_a32, opFILDiq_a32, opFILDiq_a32, opFILDiq_a32, opFILDiq_a32, opFILDiq_a32, opFILDiq_a32,
        FBSTP_a32, FBSTP_a32, FBSTP_a32, FBSTP_a32, FBSTP_a32, FBSTP_a32, FBSTP_a32, FBSTP_a32,
        FISTPiq_a32, FISTPiq_a32, FISTPiq_a32, FISTPiq_a32, FISTPiq_a32, FISTPiq_a32, FISTPiq_a32, FISTPiq_a32,
        FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32,
        FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32,
        FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32,
        FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32,
        opFSTSW_AX, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32,
        FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32,
        FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32,
        FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32, FPU_ILLEGAL_a32,
    ];

    // ---- Souches : nommés par les tables, pas encore transcrits (G4.4, G4.5) ----
    private static int opF2XM1(uint32_t fetchdat) => opX87NonTranscrit(fetchdat);
    private static int opFYL2X(uint32_t fetchdat) => opX87NonTranscrit(fetchdat);
    private static int opFPTAN(uint32_t fetchdat) => opX87NonTranscrit(fetchdat);
    private static int opFPATAN(uint32_t fetchdat) => opX87NonTranscrit(fetchdat);
    private static int opFYL2XP1(uint32_t fetchdat) => opX87NonTranscrit(fetchdat);
    private static int opFSINCOS(uint32_t fetchdat) => opX87NonTranscrit(fetchdat);
    private static int opFSIN(uint32_t fetchdat) => opX87NonTranscrit(fetchdat);
    private static int opFCOS(uint32_t fetchdat) => opX87NonTranscrit(fetchdat);
}
