// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/includes/private/cpu/x87_ops_misc.h  (lignes 1-906)
// SHA256: voir oracle.tsv ; vérifier avec tools/check-oracle.sh
// STATUS: partial — G4.4 et G4.5 : tout x87_ops_misc.h sauf FCMOV (:907-926, tables 686).
//         Les huit transcendantes (G4.5) passent par Math.*, dont la parité au bit avec la
//         glibc de l'oracle est mesurée (x87-parity, G4.0) — expressions de PCem comprises. La pile, les constantes, FCHS, FABS, FTST, FXAM,
//         FSTSW, FSTCW, FLDCW, FNINIT, FNCLEX, FDISI, FENI, FSTENV, FLDENV, FSAVE, FRSTOR en
//         16 et 32 bits, mode réel et protégé, FPREM, FPREM1, FSQRT, FRNDINT, FSCALE.
//
// GÉNÉRÉ par règles depuis le C, comme x87_ops_loadstore.cs et x87_ops_arith.cs. Les
// `codegen_set_rounding_mode(...)` sont omis : le dynarec, une souche vide dans l'oracle.
// FSTOR, FSAVE, FLDENV et FSTENV appellent FP_ENTER une SECONDE fois (fpucount compte deux) :
// c'est le C, sans autre effet. Défauts de PCem reproduits (en mode PCem ; corrigés en mode matériel, ceux que
// x87.Materiel.cs nomme) : PB-61 (FNSTSW AX sans TOP), PB-62
// (x87_pc_* et x87_op_* jamais posés ; dispositions de FSAVE et FSTENV incomplètes), PB-63
// (FXAM), PB-64 (FTST), PB-65 (FPREM, FPREM1), PB-66 (FLDLN2), PB-67 (FST registre et
// TAG_UINT64), PB-68 (C2 et les transcendantes).

using static iXtal26.Cpu._386_common;
using static iXtal26.Cpu.x86;

namespace iXtal26.Cpu;

internal static partial class _386
{
    // pcem: x87_ops_misc.h:3-12
    private static int opFDISI(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            cpu_state.pc++;
            if (cpu_c.fpu_type == cpu_c.FPU_8087) {
                    cpu_state.npxc |= FPCW_DISI;
                    CLOCK_CYCLES(x87_timings_c.x87_timings.fdisi_eni);
            } else
                    CLOCK_CYCLES(x87_timings_c.x87_timings.fnop);
            return 0;
    }

    // pcem: x87_ops_misc.h:13-22
    private static int opFENI(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            cpu_state.pc++;
            if (cpu_c.fpu_type == cpu_c.FPU_8087) {
                    cpu_state.npxc &= unchecked((uint16_t)~FPCW_DISI);
                    CLOCK_CYCLES(x87_timings_c.x87_timings.fdisi_eni);
            } else
                    CLOCK_CYCLES(x87_timings_c.x87_timings.fnop);
            return 0;
    }

    // pcem: x87_ops_misc.h:24-32
    private static int opFSTSW_AX(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            cpu_state.pc++;
            // pcem bug, fixed in hardware mode: PB-61 — npxs BRUT : sans les trois bits de TOP que la forme
            //   mémoire (opFSTSW_a16) y compose ; en mode matériel, la table du mode porte le gestionnaire corrigé
            //   (x87.Materiel.cs).
            AX = cpu_state.npxs;
            CLOCK_CYCLES(x87_timings_c.x87_timings.fstcw_sw);
            return 0;
    }

    // pcem: x87_ops_misc.h:34-39
    private static int opFNOP(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            cpu_state.pc++;
            CLOCK_CYCLES(x87_timings_c.x87_timings.fnop);
            return 0;
    }

    // pcem: x87_ops_misc.h:41-47
    private static int opFCLEX(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            cpu_state.pc++;
            cpu_state.npxs &= 0xff00;
            CLOCK_CYCLES(x87_timings_c.x87_timings.fclex);
            return 0;
    }

    // pcem: x87_ops_misc.h:49-64
    private static int opFINIT(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            cpu_state.pc++;
            // pcem bug, fixed in hardware mode: PB-70 — IC (bit 12) n'est lu nulle part : le 8087 et le 287 comparent en
            //   affine ; en mode matériel, x87_compare le lit (x87.Materiel.cs).
            if (cpu_c.fpu_type == cpu_c.FPU_8087)
                    cpu_state.npxc = 0x3ff;
            else
                    cpu_state.npxc = 0x37f;
            // omitted: codegen_set_rounding_mode(...) — le dynarec (souche vide dans l'oracle).
            // pcem bug, reproduced: PB-202 — C3-C0 effacés : le 8087 et le 287 les laissent intacts.
            cpu_state.npxs = 0;
            Array.Clear(cpu_state.tag);
            cpu_state.TOP = 0;
            cpu_state.ismmx = 0;
            CLOCK_CYCLES(x87_timings_c.x87_timings.finit);
            CPU_BLOCK_END();
            return 0;
    }

    // pcem: x87_ops_misc.h:66-74
    private static int opFFREE(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            cpu_state.pc++;
            cpu_state.tag[(cpu_state.TOP + (int)fetchdat) & 7] = x87_c.TAG_EMPTY;
            CLOCK_CYCLES(x87_timings_c.x87_timings.ffree);
            return 0;
    }

    // pcem: x87_ops_misc.h:76-85
    private static int opFST(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            cpu_state.pc++;
            ST((int)(fetchdat & 7)) = ST(0);
            // pcem bug, fixed in hardware mode: PB-67 — le tag est copié, TAG_UINT64 compris, mais pas MM[].q ;
            //   en mode matériel, la table du mode porte le gestionnaire corrigé (x87.Materiel.cs).
            cpu_state.tag[(cpu_state.TOP + (int)fetchdat) & 7] = cpu_state.tag[cpu_state.TOP & 7];
            CLOCK_CYCLES(x87_timings_c.x87_timings.fst);
            return 0;
    }

    // pcem: x87_ops_misc.h:87-97
    private static int opFSTP(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            cpu_state.pc++;
            ST((int)(fetchdat & 7)) = ST(0);
            // pcem bug, fixed in hardware mode: PB-67 — le tag est copié, TAG_UINT64 compris, mais pas MM[].q ;
            //   en mode matériel, la table du mode porte le gestionnaire corrigé (x87.Materiel.cs).
            cpu_state.tag[(cpu_state.TOP + (int)fetchdat) & 7] = cpu_state.tag[cpu_state.TOP & 7];
            x87_pop();
            CLOCK_CYCLES(x87_timings_c.x87_timings.fst);
            return 0;
    }

    // pcem: x87_ops_misc.h:99-150
    private static int FSTOR()
    {
            if (FP_ENTER()) return 1;
            // pcem bug, reproduced: PB-62 — les pointeurs d'instruction et d'opérande de l'image ne sont pas relus.
            switch ((cr0 & 1) | (cpu_state.op32 & 0x100)) {
            case 0x000: /*16-bit real mode*/
            case 0x001: /*16-bit protected mode*/
                    cpu_state.npxc = readmemw(easeg, cpu_state.eaaddr);
                    // omitted: codegen_set_rounding_mode(...) — le dynarec (souche vide dans l'oracle).
                    cpu_state.npxs = readmemw(easeg, cpu_state.eaaddr + 2);
                    x87_c.x87_settag(readmemw(easeg, cpu_state.eaaddr + 4));
                    cpu_state.TOP = (cpu_state.npxs >> 11) & 7;
                    cpu_state.eaaddr += 14;
                    break;
            case 0x100: /*32-bit real mode*/
            case 0x101: /*32-bit protected mode*/
                    cpu_state.npxc = readmemw(easeg, cpu_state.eaaddr);
                    // omitted: codegen_set_rounding_mode(...) — le dynarec (souche vide dans l'oracle).
                    cpu_state.npxs = readmemw(easeg, cpu_state.eaaddr + 4);
                    x87_c.x87_settag(readmemw(easeg, cpu_state.eaaddr + 8));
                    cpu_state.TOP = (cpu_state.npxs >> 11) & 7;
                    cpu_state.eaaddr += 28;
                    break;
            }
            x87_ld_frstor(0);
            cpu_state.eaaddr += 10;
            x87_ld_frstor(1);
            cpu_state.eaaddr += 10;
            x87_ld_frstor(2);
            cpu_state.eaaddr += 10;
            x87_ld_frstor(3);
            cpu_state.eaaddr += 10;
            x87_ld_frstor(4);
            cpu_state.eaaddr += 10;
            x87_ld_frstor(5);
            cpu_state.eaaddr += 10;
            x87_ld_frstor(6);
            cpu_state.eaaddr += 10;
            x87_ld_frstor(7);

            cpu_state.ismmx = 0;
            // Horrible hack, but as PCem doesn't keep the FPU stack in 80-bit precision at all times
            // something like this is needed
            if (cpu_state.MM_w4[0] == 0xffff && cpu_state.MM_w4[1] == 0xffff && cpu_state.MM_w4[2] == 0xffff &&
                cpu_state.MM_w4[3] == 0xffff && cpu_state.MM_w4[4] == 0xffff && cpu_state.MM_w4[5] == 0xffff &&
                cpu_state.MM_w4[6] == 0xffff && cpu_state.MM_w4[7] == 0xffff && cpu_state.TOP == 0 &&
                TagsTousValides())
                    cpu_state.ismmx = 1;

            CLOCK_CYCLES(x87_timings_c.x87_timings.frstor);
            return cpu_state.abrt;
    }

    // pcem: x87_ops_misc.h:151-157
    private static int opFSTOR_a16(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            if (fetch_ea_16(fetchdat)) return 1;
            if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
            _ = FSTOR();
            return cpu_state.abrt;
    }

    // pcem: x87_ops_misc.h:158-164
    private static int opFSTOR_a32(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            if (fetch_ea_32(fetchdat)) return 1;
            if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
            _ = FSTOR();
            return cpu_state.abrt;
    }

    // pcem: x87_ops_misc.h:166-353
    private static int FSAVE()
    {
            if (FP_ENTER()) return 1;
            cpu_state.npxs = (uint16_t)((cpu_state.npxs & ~(7 << 11)) | ((cpu_state.TOP & 7) << 11));

            // pcem bug, reproduced: PB-62 — x87_pc_* et x87_op_* valent toujours 0, et des champs de l'image ne sont
            //   pas écrits (16 bits réel : +8, +12 ; 32 bits réel : +16) ; disposition choisie par CR0.PE, pas FSETPM.
            switch ((cr0 & 1) | (cpu_state.op32 & 0x100)) {
            case 0x000: /*16-bit real mode*/
                    writememw(easeg, cpu_state.eaaddr, cpu_state.npxc);
                    writememw(easeg, cpu_state.eaaddr + 2, cpu_state.npxs);
                    writememw(easeg, cpu_state.eaaddr + 4, x87_c.x87_gettag());
                    writememw(easeg, cpu_state.eaaddr + 6, (uint16_t)x87_c.x87_pc_off);
                    writememw(easeg, cpu_state.eaaddr + 10, (uint16_t)x87_c.x87_op_off);
                    cpu_state.eaaddr += 14;
                    if (cpu_state.ismmx != 0) {
                            x87_stmmx(cpu_state.MM[0]);
                            cpu_state.eaaddr += 10;
                            x87_stmmx(cpu_state.MM[1]);
                            cpu_state.eaaddr += 10;
                            x87_stmmx(cpu_state.MM[2]);
                            cpu_state.eaaddr += 10;
                            x87_stmmx(cpu_state.MM[3]);
                            cpu_state.eaaddr += 10;
                            x87_stmmx(cpu_state.MM[4]);
                            cpu_state.eaaddr += 10;
                            x87_stmmx(cpu_state.MM[5]);
                            cpu_state.eaaddr += 10;
                            x87_stmmx(cpu_state.MM[6]);
                            cpu_state.eaaddr += 10;
                            x87_stmmx(cpu_state.MM[7]);
                    } else {
                            x87_st_fsave(0);
                            cpu_state.eaaddr += 10;
                            x87_st_fsave(1);
                            cpu_state.eaaddr += 10;
                            x87_st_fsave(2);
                            cpu_state.eaaddr += 10;
                            x87_st_fsave(3);
                            cpu_state.eaaddr += 10;
                            x87_st_fsave(4);
                            cpu_state.eaaddr += 10;
                            x87_st_fsave(5);
                            cpu_state.eaaddr += 10;
                            x87_st_fsave(6);
                            cpu_state.eaaddr += 10;
                            x87_st_fsave(7);
                    }
                    break;
            case 0x001: /*16-bit protected mode*/
                    writememw(easeg, cpu_state.eaaddr, cpu_state.npxc);
                    writememw(easeg, cpu_state.eaaddr + 2, cpu_state.npxs);
                    writememw(easeg, cpu_state.eaaddr + 4, x87_c.x87_gettag());
                    writememw(easeg, cpu_state.eaaddr + 6, (uint16_t)x87_c.x87_pc_off);
                    writememw(easeg, cpu_state.eaaddr + 8, (uint16_t)x87_c.x87_pc_seg);
                    writememw(easeg, cpu_state.eaaddr + 10, (uint16_t)x87_c.x87_op_off);
                    writememw(easeg, cpu_state.eaaddr + 12, (uint16_t)x87_c.x87_op_seg);
                    cpu_state.eaaddr += 14;
                    if (cpu_state.ismmx != 0) {
                            x87_stmmx(cpu_state.MM[0]);
                            cpu_state.eaaddr += 10;
                            x87_stmmx(cpu_state.MM[1]);
                            cpu_state.eaaddr += 10;
                            x87_stmmx(cpu_state.MM[2]);
                            cpu_state.eaaddr += 10;
                            x87_stmmx(cpu_state.MM[3]);
                            cpu_state.eaaddr += 10;
                            x87_stmmx(cpu_state.MM[4]);
                            cpu_state.eaaddr += 10;
                            x87_stmmx(cpu_state.MM[5]);
                            cpu_state.eaaddr += 10;
                            x87_stmmx(cpu_state.MM[6]);
                            cpu_state.eaaddr += 10;
                            x87_stmmx(cpu_state.MM[7]);
                    } else {
                            x87_st_fsave(0);
                            cpu_state.eaaddr += 10;
                            x87_st_fsave(1);
                            cpu_state.eaaddr += 10;
                            x87_st_fsave(2);
                            cpu_state.eaaddr += 10;
                            x87_st_fsave(3);
                            cpu_state.eaaddr += 10;
                            x87_st_fsave(4);
                            cpu_state.eaaddr += 10;
                            x87_st_fsave(5);
                            cpu_state.eaaddr += 10;
                            x87_st_fsave(6);
                            cpu_state.eaaddr += 10;
                            x87_st_fsave(7);
                    }
                    break;
            case 0x100: /*32-bit real mode*/
                    writememw(easeg, cpu_state.eaaddr, cpu_state.npxc);
                    writememw(easeg, cpu_state.eaaddr + 4, cpu_state.npxs);
                    writememw(easeg, cpu_state.eaaddr + 8, x87_c.x87_gettag());
                    writememw(easeg, cpu_state.eaaddr + 12, (uint16_t)x87_c.x87_pc_off);
                    writememw(easeg, cpu_state.eaaddr + 20, (uint16_t)x87_c.x87_op_off);
                    writememl(easeg, cpu_state.eaaddr + 24, (x87_c.x87_op_off >> 16) << 12);
                    cpu_state.eaaddr += 28;
                    if (cpu_state.ismmx != 0) {
                            x87_stmmx(cpu_state.MM[0]);
                            cpu_state.eaaddr += 10;
                            x87_stmmx(cpu_state.MM[1]);
                            cpu_state.eaaddr += 10;
                            x87_stmmx(cpu_state.MM[2]);
                            cpu_state.eaaddr += 10;
                            x87_stmmx(cpu_state.MM[3]);
                            cpu_state.eaaddr += 10;
                            x87_stmmx(cpu_state.MM[4]);
                            cpu_state.eaaddr += 10;
                            x87_stmmx(cpu_state.MM[5]);
                            cpu_state.eaaddr += 10;
                            x87_stmmx(cpu_state.MM[6]);
                            cpu_state.eaaddr += 10;
                            x87_stmmx(cpu_state.MM[7]);
                    } else {
                            x87_st_fsave(0);
                            cpu_state.eaaddr += 10;
                            x87_st_fsave(1);
                            cpu_state.eaaddr += 10;
                            x87_st_fsave(2);
                            cpu_state.eaaddr += 10;
                            x87_st_fsave(3);
                            cpu_state.eaaddr += 10;
                            x87_st_fsave(4);
                            cpu_state.eaaddr += 10;
                            x87_st_fsave(5);
                            cpu_state.eaaddr += 10;
                            x87_st_fsave(6);
                            cpu_state.eaaddr += 10;
                            x87_st_fsave(7);
                    }
                    break;
            case 0x101: /*32-bit protected mode*/
                    writememw(easeg, cpu_state.eaaddr, cpu_state.npxc);
                    writememw(easeg, cpu_state.eaaddr + 4, cpu_state.npxs);
                    writememw(easeg, cpu_state.eaaddr + 8, x87_c.x87_gettag());
                    writememl(easeg, cpu_state.eaaddr + 12, x87_c.x87_pc_off);
                    writememl(easeg, cpu_state.eaaddr + 16, (uint32_t)x87_c.x87_pc_seg);
                    writememl(easeg, cpu_state.eaaddr + 20, x87_c.x87_op_off);
                    writememl(easeg, cpu_state.eaaddr + 24, (uint32_t)x87_c.x87_op_seg);
                    cpu_state.eaaddr += 28;
                    if (cpu_state.ismmx != 0) {
                            x87_stmmx(cpu_state.MM[0]);
                            cpu_state.eaaddr += 10;
                            x87_stmmx(cpu_state.MM[1]);
                            cpu_state.eaaddr += 10;
                            x87_stmmx(cpu_state.MM[2]);
                            cpu_state.eaaddr += 10;
                            x87_stmmx(cpu_state.MM[3]);
                            cpu_state.eaaddr += 10;
                            x87_stmmx(cpu_state.MM[4]);
                            cpu_state.eaaddr += 10;
                            x87_stmmx(cpu_state.MM[5]);
                            cpu_state.eaaddr += 10;
                            x87_stmmx(cpu_state.MM[6]);
                            cpu_state.eaaddr += 10;
                            x87_stmmx(cpu_state.MM[7]);
                    } else {
                            x87_st_fsave(0);
                            cpu_state.eaaddr += 10;
                            x87_st_fsave(1);
                            cpu_state.eaaddr += 10;
                            x87_st_fsave(2);
                            cpu_state.eaaddr += 10;
                            x87_st_fsave(3);
                            cpu_state.eaaddr += 10;
                            x87_st_fsave(4);
                            cpu_state.eaaddr += 10;
                            x87_st_fsave(5);
                            cpu_state.eaaddr += 10;
                            x87_st_fsave(6);
                            cpu_state.eaaddr += 10;
                            x87_st_fsave(7);
                    }
                    break;
            }

            // pcem bug, reproduced: PB-62 — 0x37F même pour le 8087, où FNINIT pose 0x3FF.
            cpu_state.npxc = 0x37F;
            // omitted: codegen_set_rounding_mode(...) — le dynarec (souche vide dans l'oracle).
            // pcem bug, reproduced: PB-202 — C3-C0 effacés : le 8087 les laisse intacts.
            cpu_state.npxs = 0;
            Array.Clear(cpu_state.tag);
            cpu_state.TOP = 0;
            cpu_state.ismmx = 0;

            CLOCK_CYCLES(x87_timings_c.x87_timings.fsave);
            return cpu_state.abrt;
    }

    // pcem: x87_ops_misc.h:354-360
    private static int opFSAVE_a16(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            if (fetch_ea_16(fetchdat)) return 1;
            if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
            // pcem bug, reproduced: PB-54 — pas de CHECK_WRITE : la limite de l'opérande n'est pas contrôlée.
            _ = FSAVE();
            return cpu_state.abrt;
    }

    // pcem: x87_ops_misc.h:361-367
    private static int opFSAVE_a32(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            if (fetch_ea_32(fetchdat)) return 1;
            if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
            // pcem bug, reproduced: PB-54 — pas de CHECK_WRITE : la limite de l'opérande n'est pas contrôlée.
            _ = FSAVE();
            return cpu_state.abrt;
    }

    // pcem: x87_ops_misc.h:369-378
    private static int opFSTSW_a16(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            if (fetch_ea_16(fetchdat)) return 1;
            if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
            // pcem bug, reproduced: PB-54 — pas de CHECK_WRITE : la limite de l'opérande n'est pas contrôlée.
            seteaw((uint16_t)((cpu_state.npxs & 0xC7FF) | ((cpu_state.TOP & 7) << 11)));
            CLOCK_CYCLES(x87_timings_c.x87_timings.fstcw_sw);
            return cpu_state.abrt;
    }

    // pcem: x87_ops_misc.h:379-388
    private static int opFSTSW_a32(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            if (fetch_ea_32(fetchdat)) return 1;
            if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
            // pcem bug, reproduced: PB-54 — pas de CHECK_WRITE : la limite de l'opérande n'est pas contrôlée.
            seteaw((uint16_t)((cpu_state.npxs & 0xC7FF) | ((cpu_state.TOP & 7) << 11)));
            CLOCK_CYCLES(x87_timings_c.x87_timings.fstcw_sw);
            return cpu_state.abrt;
    }

    // pcem: x87_ops_misc.h:390-405
    private static int opFLD(uint32_t fetchdat)
    {
            int old_tag;
            uint64_t old_i64;

            if (FP_ENTER()) return 1;
            cpu_state.pc++;
            old_tag = cpu_state.tag[(cpu_state.TOP + (int)fetchdat) & 7];
            old_i64 = cpu_state.MM[(cpu_state.TOP + (int)fetchdat) & 7].q;
            x87_push(ST((int)(fetchdat & 7)));
            cpu_state.tag[cpu_state.TOP & 7] = (uint8_t)old_tag;
            cpu_state.MM[cpu_state.TOP & 7].q = old_i64;
            CLOCK_CYCLES(x87_timings_c.x87_timings.fld);
            return 0;
    }

    // pcem: x87_ops_misc.h:407-427
    private static int opFXCH(uint32_t fetchdat)
    {
            double td;
            uint8_t old_tag;
            uint64_t old_i64;
            if (FP_ENTER()) return 1;
            cpu_state.pc++;
            td = ST(0);
            ST(0) = ST((int)(fetchdat & 7));
            ST((int)(fetchdat & 7)) = td;
            old_tag = cpu_state.tag[cpu_state.TOP & 7];
            cpu_state.tag[cpu_state.TOP & 7] = cpu_state.tag[(cpu_state.TOP + (int)fetchdat) & 7];
            cpu_state.tag[(cpu_state.TOP + (int)fetchdat) & 7] = old_tag;
            old_i64 = cpu_state.MM[cpu_state.TOP & 7].q;
            cpu_state.MM[cpu_state.TOP & 7].q = cpu_state.MM[(cpu_state.TOP + (int)fetchdat) & 7].q;
            cpu_state.MM[(cpu_state.TOP + (int)fetchdat) & 7].q = old_i64;

            CLOCK_CYCLES(x87_timings_c.x87_timings.fxch);
            return 0;
    }

    // pcem: x87_ops_misc.h:429-438
    private static int opFCHS(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            cpu_state.pc++;
            ST(0) = -ST(0);
            cpu_state.tag[cpu_state.TOP & 7] = x87_c.TAG_VALID;
            CLOCK_CYCLES(x87_timings_c.x87_timings.fchs);
            return 0;
    }

    // pcem: x87_ops_misc.h:440-449
    private static int opFABS(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            cpu_state.pc++;
            ST(0) = Math.Abs(ST(0));
            cpu_state.tag[cpu_state.TOP & 7] = x87_c.TAG_VALID;
            CLOCK_CYCLES(x87_timings_c.x87_timings.fabs);
            return 0;
    }

    // pcem: x87_ops_misc.h:451-463
    private static int opFTST(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            cpu_state.pc++;
            // pcem bug, reproduced: PB-213 — C1 n'est pas remis à zéro (387 et suivants : C1 = 0).
            cpu_state.npxs &= unchecked((uint16_t)~(x87_c.C0 | x87_c.C2 | x87_c.C3));
            // pcem bug, fixed in hardware mode: PB-64 — un NaN rend « plus grand », pas « non ordonné » ; en
            //   mode matériel, la table du mode porte le gestionnaire corrigé (x87.Materiel.cs).
            if (ST(0) == 0.0)
                    cpu_state.npxs |= x87_c.C3;
            else if (ST(0) < 0.0)
                    cpu_state.npxs |= x87_c.C0;
            CLOCK_CYCLES(x87_timings_c.x87_timings.ftst);
            return 0;
    }

    // pcem: x87_ops_misc.h:465-481
    private static int opFXAM(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            cpu_state.pc++;
            // pcem bug, fixed in hardware mode: PB-63 — trois classes seulement : vide, zéro, « normal » ; en
            //   mode matériel, la table du mode porte le gestionnaire corrigé (x87.Materiel.cs).
            cpu_state.npxs &= unchecked((uint16_t)~(x87_c.C0 | x87_c.C1 | x87_c.C2 | x87_c.C3));
            if (cpu_state.tag[cpu_state.TOP & 7] == x87_c.TAG_EMPTY)
                    cpu_state.npxs |= x87_c.C0 | x87_c.C3;
            else if (ST(0) == 0.0)
                    cpu_state.npxs |= x87_c.C3;
            else
                    cpu_state.npxs |= x87_c.C2;
            if (ST(0) < 0.0)
                    cpu_state.npxs |= x87_c.C1;
            CLOCK_CYCLES(x87_timings_c.x87_timings.fxam);
            return 0;
    }

    // pcem: x87_ops_misc.h:483-491
    private static int opFLD1(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            cpu_state.pc++;
            x87_push(1.0);
            CLOCK_CYCLES(x87_timings_c.x87_timings.fld_z1);
            return 0;
    }

    // pcem: x87_ops_misc.h:493-501
    private static int opFLDL2T(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            cpu_state.pc++;
            x87_push(3.3219280948873623);
            CLOCK_CYCLES(x87_timings_c.x87_timings.fld_const);
            return 0;
    }

    // pcem: x87_ops_misc.h:503-511
    private static int opFLDL2E(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            cpu_state.pc++;
            x87_push(1.4426950408889634);
            CLOCK_CYCLES(x87_timings_c.x87_timings.fld_const);
            return 0;
    }

    // pcem: x87_ops_misc.h:513-521
    private static int opFLDPI(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            cpu_state.pc++;
            x87_push(3.141592653589793);
            CLOCK_CYCLES(x87_timings_c.x87_timings.fld_const);
            return 0;
    }

    // pcem: x87_ops_misc.h:523-531
    private static int opFLDEG2(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            cpu_state.pc++;
            x87_push(0.3010299956639812);
            CLOCK_CYCLES(x87_timings_c.x87_timings.fld_const);
            return 0;
    }

    // pcem: x87_ops_misc.h:533-541
    private static int opFLDLN2(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            cpu_state.pc++;
            // pcem bug, fixed in hardware mode: PB-66 — ln 2 d'un ulp au-dessus du double le plus proche ;
            //   en mode matériel, la table du mode porte les constantes corrigées (x87.Materiel.cs).
            x87_push_u64(0x3fe62e42fefa39f0UL);
            CLOCK_CYCLES(x87_timings_c.x87_timings.fld_const);
            return 0;
    }

    // pcem: x87_ops_misc.h:543-552
    private static int opFLDZ(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            cpu_state.pc++;
            x87_push(0.0);
            cpu_state.tag[cpu_state.TOP & 7] = x87_c.TAG_VALID;
            CLOCK_CYCLES(x87_timings_c.x87_timings.fld_z1);
            return 0;
    }

    // pcem: x87_ops_misc.h:554-563
    private static int opF2XM1(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            cpu_state.pc++;
            ST(0) = Math.Pow(2.0, ST(0)) - 1.0;
            cpu_state.tag[cpu_state.TOP & 7] = x87_c.TAG_VALID;
            CLOCK_CYCLES(x87_timings_c.x87_timings.f2xm1);
            return 0;
    }

    // pcem: x87_ops_misc.h:565-575
    private static int opFYL2X(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            cpu_state.pc++;
            ST(1) = ST(1) * (Math.Log(ST(0)) / Math.Log(2.0));
            cpu_state.tag[(cpu_state.TOP + 1) & 7] = x87_c.TAG_VALID;
            x87_pop();
            CLOCK_CYCLES(x87_timings_c.x87_timings.fyl2x);
            return 0;
    }

    // pcem: x87_ops_misc.h:577-587
    private static int opFYL2XP1(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            cpu_state.pc++;
            ST(1) = ST(1) * (Math.Log(ST(0) + 1.0) / Math.Log(2.0));
            cpu_state.tag[(cpu_state.TOP + 1) & 7] = x87_c.TAG_VALID;
            x87_pop();
            CLOCK_CYCLES(x87_timings_c.x87_timings.fyl2xp1);
            return 0;
    }

    // pcem: x87_ops_misc.h:589-600
    private static int opFPTAN(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            cpu_state.pc++;
            ST(0) = Math.Tan(ST(0));
            cpu_state.tag[cpu_state.TOP & 7] = x87_c.TAG_VALID;
            x87_push(1.0);
            // pcem bug, reproduced: PB-68 — C2 toujours effacé : pas de borne |x| < 2^63, la libm
            //   réduit tout argument, et « réduction incomplète » n'est jamais signalée.
            cpu_state.npxs &= unchecked((uint16_t)~x87_c.C2);
            CLOCK_CYCLES(x87_timings_c.x87_timings.fptan);
            return 0;
    }

    // pcem: x87_ops_misc.h:602-612
    private static int opFPATAN(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            cpu_state.pc++;
            ST(1) = Math.Atan2(ST(1), ST(0));
            cpu_state.tag[(cpu_state.TOP + 1) & 7] = x87_c.TAG_VALID;
            x87_pop();
            CLOCK_CYCLES(x87_timings_c.x87_timings.fpatan);
            return 0;
    }

    // pcem: x87_ops_misc.h:614-622
    private static int opFDECSTP(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            cpu_state.pc++;
            cpu_state.TOP--;
            CLOCK_CYCLES(x87_timings_c.x87_timings.fincdecstp);
            return 0;
    }

    // pcem: x87_ops_misc.h:624-632
    private static int opFINCSTP(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            cpu_state.pc++;
            cpu_state.TOP++;
            CLOCK_CYCLES(x87_timings_c.x87_timings.fincdecstp);
            return 0;
    }

    // pcem: x87_ops_misc.h:634-654
    private static int opFPREM(uint32_t fetchdat)
    {
            int64_t temp64;
            if (FP_ENTER()) return 1;
            cpu_state.pc++;
            // pcem bug, reproduced: PB-65 — un quotient tronqué d'un coup, C2 jamais posé, et
            //   FPREM1 identique à FPREM.
            temp64 = CvtI64(ST(0) / ST(1));
            ST(0) = ST(0) - (ST(1) * (double)temp64);
            cpu_state.tag[cpu_state.TOP & 7] = x87_c.TAG_VALID;
            cpu_state.npxs &= unchecked((uint16_t)~(x87_c.C0 | x87_c.C1 | x87_c.C2 | x87_c.C3));
            if ((temp64 & 4) != 0)
                    cpu_state.npxs |= x87_c.C0;
            if ((temp64 & 2) != 0)
                    cpu_state.npxs |= x87_c.C3;
            if ((temp64 & 1) != 0)
                    cpu_state.npxs |= x87_c.C1;
            CLOCK_CYCLES(x87_timings_c.x87_timings.fprem);
            return 0;
    }

    // pcem: x87_ops_misc.h:655-675
    private static int opFPREM1(uint32_t fetchdat)
    {
            int64_t temp64;
            if (FP_ENTER()) return 1;
            cpu_state.pc++;
            // pcem bug, reproduced: PB-65 — un quotient tronqué d'un coup, C2 jamais posé, et
            //   FPREM1 identique à FPREM.
            temp64 = CvtI64(ST(0) / ST(1));
            ST(0) = ST(0) - (ST(1) * (double)temp64);
            cpu_state.tag[cpu_state.TOP & 7] = x87_c.TAG_VALID;
            cpu_state.npxs &= unchecked((uint16_t)~(x87_c.C0 | x87_c.C1 | x87_c.C2 | x87_c.C3));
            if ((temp64 & 4) != 0)
                    cpu_state.npxs |= x87_c.C0;
            if ((temp64 & 2) != 0)
                    cpu_state.npxs |= x87_c.C3;
            if ((temp64 & 1) != 0)
                    cpu_state.npxs |= x87_c.C1;
            CLOCK_CYCLES(x87_timings_c.x87_timings.fprem1);
            return 0;
    }

    // pcem: x87_ops_misc.h:677-686
    private static int opFSQRT(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            cpu_state.pc++;
            ST(0) = Math.Sqrt(ST(0));
            cpu_state.tag[cpu_state.TOP & 7] = x87_c.TAG_VALID;
            CLOCK_CYCLES(x87_timings_c.x87_timings.fsqrt);
            return 0;
    }

    // pcem: x87_ops_misc.h:688-701
    private static int opFSINCOS(uint32_t fetchdat)
    {
            double td;
            if (FP_ENTER()) return 1;
            cpu_state.pc++;
            td = ST(0);
            ST(0) = Math.Sin(td);
            cpu_state.tag[cpu_state.TOP & 7] = x87_c.TAG_VALID;
            x87_push(Math.Cos(td));
            // pcem bug, reproduced: PB-68 — C2 toujours effacé : pas de borne |x| < 2^63, la libm
            //   réduit tout argument, et « réduction incomplète » n'est jamais signalée.
            cpu_state.npxs &= unchecked((uint16_t)~x87_c.C2);
            CLOCK_CYCLES(x87_timings_c.x87_timings.fsincos);
            return 0;
    }

    // pcem: x87_ops_misc.h:703-714
    private static int opFRNDINT(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            cpu_state.pc++;
            // pcem bug, reproduced: PB-197 — |x| >= 2^63, ∞ ou NaN rendent -2^63, pas la valeur elle-même.
            // pcem bug, reproduced: PB-211 — un résultat nul perd son signe : -0 et ]-0,5 ; 0[ rendent +0.
            ST(0) = (double)x87_fround(ST(0));
            cpu_state.tag[cpu_state.TOP & 7] = x87_c.TAG_VALID;
            CLOCK_CYCLES(x87_timings_c.x87_timings.frndint);
            return 0;
    }

    // pcem: x87_ops_misc.h:716-728
    private static int opFSCALE(uint32_t fetchdat)
    {
            int64_t temp64;
            if (FP_ENTER()) return 1;
            cpu_state.pc++;
            // pcem bug, reproduced: PB-198 — ST(1) NaN ou infini : (int64_t) rend -2^63, et ST(0) devient ±0.
            temp64 = CvtI64(ST(1));
            if (ST(0) != 0.0)
                    ST(0) = ST(0) * Math.Pow(2.0, (double)temp64);
            cpu_state.tag[cpu_state.TOP & 7] = x87_c.TAG_VALID;
            CLOCK_CYCLES(x87_timings_c.x87_timings.fscale);
            return 0;
    }

    // pcem: x87_ops_misc.h:730-740
    private static int opFSIN(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            cpu_state.pc++;
            ST(0) = Math.Sin(ST(0));
            cpu_state.tag[cpu_state.TOP & 7] = x87_c.TAG_VALID;
            // pcem bug, reproduced: PB-68 — C2 toujours effacé : pas de borne |x| < 2^63, la libm
            //   réduit tout argument, et « réduction incomplète » n'est jamais signalée.
            cpu_state.npxs &= unchecked((uint16_t)~x87_c.C2);
            CLOCK_CYCLES(x87_timings_c.x87_timings.fsin_cos);
            return 0;
    }

    // pcem: x87_ops_misc.h:742-752
    private static int opFCOS(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            cpu_state.pc++;
            ST(0) = Math.Cos(ST(0));
            cpu_state.tag[cpu_state.TOP & 7] = x87_c.TAG_VALID;
            // pcem bug, reproduced: PB-68 — C2 toujours effacé : pas de borne |x| < 2^63, la libm
            //   réduit tout argument, et « réduction incomplète » n'est jamais signalée.
            cpu_state.npxs &= unchecked((uint16_t)~x87_c.C2);
            CLOCK_CYCLES(x87_timings_c.x87_timings.fsin_cos);
            return 0;
    }

    // pcem: x87_ops_misc.h:754-778
    private static int FLDENV()
    {
            if (FP_ENTER()) return 1;
            // pcem bug, reproduced: PB-62 — les pointeurs d'instruction et d'opérande de l'image ne sont pas relus.
            switch ((cr0 & 1) | (cpu_state.op32 & 0x100)) {
            case 0x000: /*16-bit real mode*/
            case 0x001: /*16-bit protected mode*/
                    cpu_state.npxc = readmemw(easeg, cpu_state.eaaddr);
                    // omitted: codegen_set_rounding_mode(...) — le dynarec (souche vide dans l'oracle).
                    cpu_state.npxs = readmemw(easeg, cpu_state.eaaddr + 2);
                    x87_c.x87_settag(readmemw(easeg, cpu_state.eaaddr + 4));
                    cpu_state.TOP = (cpu_state.npxs >> 11) & 7;
                    break;
            case 0x100: /*32-bit real mode*/
            case 0x101: /*32-bit protected mode*/
                    cpu_state.npxc = readmemw(easeg, cpu_state.eaaddr);
                    // omitted: codegen_set_rounding_mode(...) — le dynarec (souche vide dans l'oracle).
                    cpu_state.npxs = readmemw(easeg, cpu_state.eaaddr + 4);
                    x87_c.x87_settag(readmemw(easeg, cpu_state.eaaddr + 8));
                    cpu_state.TOP = (cpu_state.npxs >> 11) & 7;
                    break;
            }
            CLOCK_CYCLES(x87_timings_c.x87_timings.fldenv);
            return cpu_state.abrt;
    }

    // pcem: x87_ops_misc.h:780-786
    private static int opFLDENV_a16(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            if (fetch_ea_16(fetchdat)) return 1;
            if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
            _ = FLDENV();
            return cpu_state.abrt;
    }

    // pcem: x87_ops_misc.h:787-793
    private static int opFLDENV_a32(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            if (fetch_ea_32(fetchdat)) return 1;
            if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
            _ = FLDENV();
            return cpu_state.abrt;
    }

    // pcem: x87_ops_misc.h:795-809
    private static int opFLDCW_a16(uint32_t fetchdat)
    {
            uint16_t tempw;
            if (FP_ENTER()) return 1;
            if (fetch_ea_16(fetchdat)) return 1;
            if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
            tempw = geteaw();
            if (cpu_state.abrt != 0)
                    return 1;
            cpu_state.npxc = tempw;
            // omitted: codegen_set_rounding_mode(...) — le dynarec (souche vide dans l'oracle).
            CLOCK_CYCLES(x87_timings_c.x87_timings.fldcw);
            return 0;
    }

    // pcem: x87_ops_misc.h:810-824
    private static int opFLDCW_a32(uint32_t fetchdat)
    {
            uint16_t tempw;
            if (FP_ENTER()) return 1;
            if (fetch_ea_32(fetchdat)) return 1;
            if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
            tempw = geteaw();
            if (cpu_state.abrt != 0)
                    return 1;
            cpu_state.npxc = tempw;
            // omitted: codegen_set_rounding_mode(...) — le dynarec (souche vide dans l'oracle).
            CLOCK_CYCLES(x87_timings_c.x87_timings.fldcw);
            return 0;
    }

    // pcem: x87_ops_misc.h:826-869
    private static int FSTENV()
    {
            if (FP_ENTER()) return 1;
            cpu_state.npxs = (uint16_t)((cpu_state.npxs & ~(7 << 11)) | ((cpu_state.TOP & 7) << 11));

            // pcem bug, reproduced: PB-62 — x87_pc_* et x87_op_* valent toujours 0, champs non écrits comme FSAVE ;
            //   disposition choisie par CR0.PE, pas FSETPM.
            switch ((cr0 & 1) | (cpu_state.op32 & 0x100)) {
            case 0x000: /*16-bit real mode*/
                    writememw(easeg, cpu_state.eaaddr, cpu_state.npxc);
                    writememw(easeg, cpu_state.eaaddr + 2, cpu_state.npxs);
                    writememw(easeg, cpu_state.eaaddr + 4, x87_c.x87_gettag());
                    writememw(easeg, cpu_state.eaaddr + 6, (uint16_t)x87_c.x87_pc_off);
                    writememw(easeg, cpu_state.eaaddr + 10, (uint16_t)x87_c.x87_op_off);
                    break;
            case 0x001: /*16-bit protected mode*/
                    writememw(easeg, cpu_state.eaaddr, cpu_state.npxc);
                    writememw(easeg, cpu_state.eaaddr + 2, cpu_state.npxs);
                    writememw(easeg, cpu_state.eaaddr + 4, x87_c.x87_gettag());
                    writememw(easeg, cpu_state.eaaddr + 6, (uint16_t)x87_c.x87_pc_off);
                    writememw(easeg, cpu_state.eaaddr + 8, (uint16_t)x87_c.x87_pc_seg);
                    writememw(easeg, cpu_state.eaaddr + 10, (uint16_t)x87_c.x87_op_off);
                    writememw(easeg, cpu_state.eaaddr + 12, (uint16_t)x87_c.x87_op_seg);
                    break;
            case 0x100: /*32-bit real mode*/
                    writememw(easeg, cpu_state.eaaddr, cpu_state.npxc);
                    writememw(easeg, cpu_state.eaaddr + 4, cpu_state.npxs);
                    writememw(easeg, cpu_state.eaaddr + 8, x87_c.x87_gettag());
                    writememw(easeg, cpu_state.eaaddr + 12, (uint16_t)x87_c.x87_pc_off);
                    writememw(easeg, cpu_state.eaaddr + 20, (uint16_t)x87_c.x87_op_off);
                    writememl(easeg, cpu_state.eaaddr + 24, (x87_c.x87_op_off >> 16) << 12);
                    break;
            case 0x101: /*32-bit protected mode*/
                    writememw(easeg, cpu_state.eaaddr, cpu_state.npxc);
                    writememw(easeg, cpu_state.eaaddr + 4, cpu_state.npxs);
                    writememw(easeg, cpu_state.eaaddr + 8, x87_c.x87_gettag());
                    writememl(easeg, cpu_state.eaaddr + 12, x87_c.x87_pc_off);
                    writememl(easeg, cpu_state.eaaddr + 16, (uint32_t)x87_c.x87_pc_seg);
                    writememl(easeg, cpu_state.eaaddr + 20, x87_c.x87_op_off);
                    writememl(easeg, cpu_state.eaaddr + 24, (uint32_t)x87_c.x87_op_seg);
                    break;
            }
            // pcem bug, reproduced: PB-207 — npxc intact : FSTENV ne masque pas les exceptions.
            CLOCK_CYCLES(x87_timings_c.x87_timings.fstenv);
            return cpu_state.abrt;
    }

    // pcem: x87_ops_misc.h:871-877
    private static int opFSTENV_a16(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            if (fetch_ea_16(fetchdat)) return 1;
            if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
            // pcem bug, reproduced: PB-54 — pas de CHECK_WRITE : la limite de l'opérande n'est pas contrôlée.
            _ = FSTENV();
            return cpu_state.abrt;
    }

    // pcem: x87_ops_misc.h:878-884
    private static int opFSTENV_a32(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            if (fetch_ea_32(fetchdat)) return 1;
            if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
            // pcem bug, reproduced: PB-54 — pas de CHECK_WRITE : la limite de l'opérande n'est pas contrôlée.
            _ = FSTENV();
            return cpu_state.abrt;
    }

    // pcem: x87_ops_misc.h:886-895
    private static int opFSTCW_a16(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            if (fetch_ea_16(fetchdat)) return 1;
            if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
            // pcem bug, reproduced: PB-54 — pas de CHECK_WRITE : la limite de l'opérande n'est pas contrôlée.
            seteaw(cpu_state.npxc);
            CLOCK_CYCLES(x87_timings_c.x87_timings.fstcw_sw);
            return cpu_state.abrt;
    }

    // pcem: x87_ops_misc.h:896-905
    private static int opFSTCW_a32(uint32_t fetchdat)
    {
            if (FP_ENTER()) return 1;
            if (fetch_ea_32(fetchdat)) return 1;
            if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
            // pcem bug, reproduced: PB-54 — pas de CHECK_WRITE : la limite de l'opérande n'est pas contrôlée.
            seteaw(cpu_state.npxc);
            CLOCK_CYCLES(x87_timings_c.x87_timings.fstcw_sw);
            return cpu_state.abrt;
    }
}
