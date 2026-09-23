// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/includes/private/cpu/x86_ops_stack.h  (en entier, lignes
//         3-626)
// SHA256: voir oracle.tsv ; vérifier avec tools/check-oracle.sh
// STATUS: partial — A4 : les 30 emplacements de la pile qu'un 286 atteint.
//         Les formes `_l` (PUSH_L_OP, POP_L_OP, PUSHA_l, POPA_l, ENTER_l,
//         LEAVE_l, les `_l` de PUSH_SEG_OPS/POP_SEG_OPS) et les formes `_a32`
//         restent dehors, déclarées au registre des omissions.
//
// LE GROUPE SANS LEQUEL RIEN NE TOURNE.
//
// Un BIOS n'exécute pas trois instructions sans PUSH : chaque appel de
// sous-programme, chaque interruption, chaque sauvegarde de registre y passe.
// C'est le plus gros groupe de la table — 30 emplacements sur 256 — et le
// premier prérequis du chemin critique vers l'amorçage.
//
// TROIS MACROS, et on les transcrit comme telles (même doctrine qu'OP_ARITH) :
//   - PUSH_W_OP / POP_W_OP (:3-10, :19-26), instanciées huit fois chacune sur
//     les huit registres généraux. Leur paramètre est un REGISTRE, et l'ordre
//     x86 des emplacements 50-5F — AX, CX, DX, BX, SP, BP, SI, DI — est
//     exactement celui de cpu_state.regs[]. Une boucle sur l'indice est donc la
//     paramétrisation fidèle, pas un raccourci.
//   - PUSH_SEG_OPS / POP_SEG_OPS (:519-559).
//
// DEUX QUIRKS DE PCem, transcrits tels quels et signalés sur place : le trou de
// POPA et le temp_esp des formes `_w` de POP_SEG_OPS.

using iXtal26.Memory;
using static iXtal26.Cpu._386_common;
using static iXtal26.Cpu.x86;

namespace iXtal26.Cpu;

internal static partial class _386
{
    /// <summary>pcem: x86_ops_stack.h:3-10 — PUSH_W_OP(reg), et :19-26 —
    /// POP_W_OP(reg). Les deux macros, sur les huit registres généraux.</summary>
    private static void PoserPushPopRegistres()
    {
        for (var n = 0; n < 8; n++)
        {
                var reg = n;

                // pcem: x86_ops_stack.h:3-10 — op##PUSH_##reg
                ops_286[0x50 + reg] = fetchdat =>
                {
                        PUSH_W(cpu_state.regs[reg].w);
                        CLOCK_CYCLES(is486 != 0 ? 1 : 2);
                        PREFETCH_RUN(2, 1, -1, 0, 0, 1, 0, 0);
                        return cpu_state.abrt;
                };

                // pcem: x86_ops_stack.h:19-26 — op##POP_##reg
                ops_286[0x58 + reg] = fetchdat =>
                {
                        cpu_state.regs[reg].w = POP_W();
                        CLOCK_CYCLES(is486 != 0 ? 1 : 4);
                        PREFETCH_RUN(4, 1, -1, 1, 0, 0, 0, 0);
                        return cpu_state.abrt;
                };
        }
    }

    // pcem: x86_ops_stack.h:71-97 — opPUSHA_w
    private static int opPUSHA_w(uint32_t fetchdat)
    {
        if (stack32 != 0)
        {
                writememw(ss, ESP - 2, AX);
                writememw(ss, ESP - 4, CX);
                writememw(ss, ESP - 6, DX);
                writememw(ss, ESP - 8, BX);
                writememw(ss, ESP - 10, SP);
                writememw(ss, ESP - 12, BP);
                writememw(ss, ESP - 14, SI);
                writememw(ss, ESP - 16, DI);
                if (cpu_state.abrt == 0)
                        ESP -= 16;
        }
        else
        {
                writememw(ss, (uint32_t)((SP - 2) & 0xFFFF), AX);
                writememw(ss, (uint32_t)((SP - 4) & 0xFFFF), CX);
                writememw(ss, (uint32_t)((SP - 6) & 0xFFFF), DX);
                writememw(ss, (uint32_t)((SP - 8) & 0xFFFF), BX);
                writememw(ss, (uint32_t)((SP - 10) & 0xFFFF), SP);
                writememw(ss, (uint32_t)((SP - 12) & 0xFFFF), BP);
                writememw(ss, (uint32_t)((SP - 14) & 0xFFFF), SI);
                writememw(ss, (uint32_t)((SP - 16) & 0xFFFF), DI);
                if (cpu_state.abrt == 0)
                        SP -= 16;
        }
        CLOCK_CYCLES(is486 != 0 ? 11 : 18);
        PREFETCH_RUN(18, 1, -1, 0, 0, 8, 0, 0);
        return cpu_state.abrt;
    }

    // pcem: x86_ops_stack.h:128-179 — opPOPA_w.
    //
    // LE TROU EST DANS PCem, PAS DANS CE PORT. PUSHA écrit HUIT mots ; POPA n'en
    // relit que SEPT — l'offset +6, celui où PUSHA avait mis SP, n'est jamais lu.
    // C'est conforme au 286 (POPA jette la valeur de SP empilée) mais PCem le fait
    // en SAUTANT la lecture, là où le silicium la fait et la jette. La différence
    // est observable : un POPA à cheval sur une limite de segment n'abandonne pas
    // au même endroit. On transcrit PCem — c'est lui l'oracle.
    private static int opPOPA_w(uint32_t fetchdat)
    {
        if (stack32 != 0)
        {
                DI = readmemw(ss, ESP);
                if (cpu_state.abrt != 0)
                        return 1;
                SI = readmemw(ss, ESP + 2);
                if (cpu_state.abrt != 0)
                        return 1;
                BP = readmemw(ss, ESP + 4);
                if (cpu_state.abrt != 0)
                        return 1;
                BX = readmemw(ss, ESP + 8);
                if (cpu_state.abrt != 0)
                        return 1;
                DX = readmemw(ss, ESP + 10);
                if (cpu_state.abrt != 0)
                        return 1;
                CX = readmemw(ss, ESP + 12);
                if (cpu_state.abrt != 0)
                        return 1;
                AX = readmemw(ss, ESP + 14);
                if (cpu_state.abrt != 0)
                        return 1;
                ESP += 16;
        }
        else
        {
                DI = readmemw(ss, (uint32_t)(SP & 0xFFFF));
                if (cpu_state.abrt != 0)
                        return 1;
                SI = readmemw(ss, (uint32_t)((SP + 2) & 0xFFFF));
                if (cpu_state.abrt != 0)
                        return 1;
                BP = readmemw(ss, (uint32_t)((SP + 4) & 0xFFFF));
                if (cpu_state.abrt != 0)
                        return 1;
                BX = readmemw(ss, (uint32_t)((SP + 8) & 0xFFFF));
                if (cpu_state.abrt != 0)
                        return 1;
                DX = readmemw(ss, (uint32_t)((SP + 10) & 0xFFFF));
                if (cpu_state.abrt != 0)
                        return 1;
                CX = readmemw(ss, (uint32_t)((SP + 12) & 0xFFFF));
                if (cpu_state.abrt != 0)
                        return 1;
                AX = readmemw(ss, (uint32_t)((SP + 14) & 0xFFFF));
                if (cpu_state.abrt != 0)
                        return 1;
                SP += 16;
        }
        CLOCK_CYCLES(is486 != 0 ? 9 : 24);
        PREFETCH_RUN(24, 1, -1, 7, 0, 0, 0, 0);
        return 0;
    }

    // pcem: x86_ops_stack.h:233-239 — opPUSH_imm_w
    private static int opPUSH_imm_w(uint32_t fetchdat)
    {
        uint16_t val = (uint16_t)fetchdat; cpu_state.pc += 2;   // getwordf()
        PUSH_W(val);
        CLOCK_CYCLES(2);
        PREFETCH_RUN(2, 3, -1, 0, 0, 1, 0, 0);
        return cpu_state.abrt;
    }

    // pcem: x86_ops_stack.h:250-260 — opPUSH_imm_bw. L'immédiat est un OCTET
    // étendu en signe, et c'est toute la raison d'être de l'opcode.
    private static int opPUSH_imm_bw(uint32_t fetchdat)
    {
        uint16_t tempw = (uint8_t)fetchdat; cpu_state.pc++;     // getbytef()

        if ((tempw & 0x80) != 0)
                tempw |= 0xFF00;
        PUSH_W(tempw);

        CLOCK_CYCLES(2);
        PREFETCH_RUN(2, 2, -1, 0, 0, 1, 0, 0);
        return cpu_state.abrt;
    }

    // pcem: x86_ops_stack.h:273-297 — opPOPW_a16.
    //
    // LE DÉPILEMENT A LIEU AVANT LE CALCUL DE L'ADRESSE EFFECTIVE, et ça se voit :
    // `POP [BP+SI]` avec un BP qui vient d'être dépilé lit l'ANCIEN BP. Si l'écriture
    // abandonne, le pointeur de pile est remis en arrière à la main.
    private static int opPOPW_a16(uint32_t fetchdat)
    {
        uint16_t temp;

        temp = POP_W();
        if (cpu_state.abrt != 0)
                return 1;

        if (fetch_ea_16(fetchdat)) return 1;
        if (cpu_mod != 3)
                if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
        seteaw(temp);
        if (cpu_state.abrt != 0)
        {
                if (stack32 != 0)
                        ESP -= 2;
                else
                        SP -= 2;
        }

        if (is486 != 0)
                CLOCK_CYCLES((cpu_mod == 3) ? 1 : 6);
        else
                CLOCK_CYCLES((cpu_mod == 3) ? 4 : 5);
        PREFETCH_RUN((cpu_mod == 3) ? 4 : 5, 2, (int)fetchdat, 1, 0, (cpu_mod == 3) ? 0 : 1, 0, 0);
        return cpu_state.abrt;
    }

    // ----------------------------------------------------- les segments

    /// <summary>pcem: x86_ops_stack.h:519-531 — PUSH_SEG_OPS(seg), forme `_w`.
    /// Le paramètre de la macro est un SÉLECTEUR.</summary>
    private static OpFn PushSeg(Func<uint16_t> seg) => fetchdat =>
    {
        PUSH_W(seg());
        CLOCK_CYCLES(2);
        PREFETCH_RUN(2, 1, -1, 0, 0, 1, 0, 0);
        return cpu_state.abrt;
    };

    /// <summary>pcem: x86_ops_stack.h:533-545 — POP_SEG_OPS(seg, realseg),
    /// forme `_w`.
    ///
    /// `temp_esp` SAUVEGARDE ESP ET NON SP, y compris dans la forme 16 bits.
    /// Sur un 286 stack32 est nul, donc POP_W n'a touché que la moitié basse et
    /// restaurer les 32 bits revient au même — mais ça cesserait d'être vrai au
    /// 386. C'est PCem qui l'écrit ainsi ; on ne le « répare » pas.</summary>
    private static OpFn PopSeg(x86seg realseg) => fetchdat =>
    {
        uint16_t temp_seg;
        uint32_t temp_esp = ESP;
        temp_seg = POP_W();
        if (cpu_state.abrt != 0)
                return 1;
        x86seg_c.loadseg(temp_seg, realseg);
        if (cpu_state.abrt != 0)
                ESP = temp_esp;
        CLOCK_CYCLES(is486 != 0 ? 3 : 7);
        PREFETCH_RUN(is486 != 0 ? 3 : 7, 1, -1, 0, 0, 1, 0, 0);
        return cpu_state.abrt;
    };

    // pcem: x86_ops_stack.h:573-598 — opPOP_SS_w.
    //
    // CELUI-LÀ N'EST PAS UNE INSTANCE DE LA MACRO, et c'est le seul opcode de tout
    // le groupe qui EXÉCUTE L'INSTRUCTION SUIVANTE lui-même. C'est l'ombre
    // d'interruption : après un chargement de SS, le 286 inhibe les interruptions
    // le temps de l'instruction d'après, pour qu'un MOV SP,... puisse suivre sans
    // qu'une interruption ne survienne sur une pile à moitié chargée. PCem
    // l'obtient en allant chercher et en aiguillant l'opcode suivant sur place,
    // puis en rendant 1 — ce qui fait sortir exec386 de sa boucle interne.
    private static int opPOP_SS_w(uint32_t fetchdat)
    {
        uint16_t temp_seg;
        uint32_t temp_esp = ESP;
        temp_seg = POP_W();
        if (cpu_state.abrt != 0)
                return 1;
        x86seg_c.loadseg(temp_seg, cpu_state.seg_ss);
        if (cpu_state.abrt != 0)
        {
                ESP = temp_esp;
                return 1;
        }
        CLOCK_CYCLES(is486 != 0 ? 3 : 7);
        PREFETCH_RUN(is486 != 0 ? 3 : 7, 1, -1, 0, 0, 1, 0, 0);

        cpu_state.oldpc = cpu_state.pc;
        cpu_state.op32 = use32;
        cpu_state.ssegs = 0;
        cpu_state.ea_seg = cpu_state.seg_ds;
        fetchdat = fastreadl(cs + cpu_state.pc);
        cpu_state.pc++;
        if (cpu_state.abrt != 0)
                return 1;
        ops_286[(fetchdat & 0xff) | cpu_state.op32](fetchdat >> 8);

        return 1;
    }

    // omitted: toutes les formes `_l` et `_a32` de cet en-tête — PUSH_L_OP,
    //   POP_L_OP, opPUSHA_l, opPOPA_l, opPUSH_imm_l, opPUSH_imm_bl, opPOPW_a32,
    //   opPOPL_a16/a32, opENTER_l, opLEAVE_l, les `_l` de PUSH_SEG_OPS et
    //   POP_SEG_OPS, opPOP_SS_l — inatteignables tant qu'op32 est nul.
    // omitted: opPUSH_FS/GS et opPOP_FS/GS — le 286 n'a ni FS ni GS, et la table
    //   ne leur donne aucun emplacement.
    // omitted: opENTER_w (:375-429) et opLEAVE_w (:486-501) — leurs emplacements
    //   C8 et C9 relèvent du groupe `call`, qui n'est pas encore transcrit ; les
    //   poser seuls laisserait un handler sans voisin pour l'exercer.

    /// <summary>Emplacements relevés sur ops_286[] de la .so par gdb.
    ///
    /// L'ordre de 50-5F est l'ordre X86 des registres — AX, CX, DX, BX, SP, BP,
    /// SI, DI — et non l'ordre alphabétique des invocations dans le fichier C,
    /// qui liste AX, BX, CX, DX. Se fier au texte aurait interverti BX et CX.</summary>
    private static void PoserGroupePile()
    {
        PoserPushPopRegistres();

        ops_286[0x60] = opPUSHA_w;
        ops_286[0x61] = opPOPA_w;
        ops_286[0x68] = opPUSH_imm_w;
        ops_286[0x6A] = opPUSH_imm_bw;
        ops_286[0x8F] = opPOPW_a16;

        ops_286[0x06] = PushSeg(() => ES);
        ops_286[0x0E] = PushSeg(() => CS);
        ops_286[0x16] = PushSeg(() => SS);
        ops_286[0x1E] = PushSeg(() => DS);

        ops_286[0x07] = PopSeg(cpu_state.seg_es);
        ops_286[0x1F] = PopSeg(cpu_state.seg_ds);
        ops_286[0x17] = opPOP_SS_w;
    }
}
