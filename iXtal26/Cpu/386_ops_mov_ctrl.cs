// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/includes/private/cpu/x86_ops_mov_ctrl.h  (l'en-tête entier)
// SHA256: voir oracle.tsv ; vérifier avec tools/check-oracle.sh
// STATUS: transcribed — G2, D4 : MOV CRx, MOV DRx, MOV TRx, lecture et écriture,
//         en formes a16 et a32. Douze handlers, vingt-quatre emplacements de
//         ops_386_0f (0F 20 à 0F 26, sans 0F 25).
//
// LES REGISTRES SYSTÈME DU 386. CR0 prolonge le mot d'état machine du 286 (MSW en
// est la moitié basse, même stockage) ; CR2 garde l'adresse de la dernière faute de
// page, CR3 la base du répertoire de pages ; DR0 à DR7 sont les registres de
// débogage ; TR6 et TR7, les registres de test du TLB.
//
// LE CHAMP mod DU ModRM EST IGNORÉ PAR LE SILICIUM, PAS PAR PCem. Un vrai 386 prend
// toujours `rm` pour un registre. Ici fetch_ea_16/32 décode une adresse effective
// quand mod ≠ 3 — elle consomme ses octets de déplacement et avance pc — puis le
// handler lit cpu_rm comme un registre. Transcrit tel quel.
//
// LA GARDE EST `(CPL || VM) && (cr0 & 1)` : en mode réel elle est toujours fausse,
// donc tous ces handlers y sont légaux. La branche #GP n'est pas atteinte par le
// fuzzeur en mode réel ; elle relève de pm-check (D5).
//
// TR6/TR7 SONT DES REGISTRES FANTÔMES : la lecture rend zéro, l'écriture ne garde
// rien. PCem ne modélise pas le TLB par leur biais.

using static iXtal26.Cpu._386_common;
using static iXtal26.Cpu.x86;

namespace iXtal26.Cpu;

internal static partial class _386
{
    // pcem: x86_ops_mov_ctrl.h:3-36
    //
    // CR1 N'EXISTE PAS, ET CR4 SEULEMENT AU 486. Le `case 4` sans CR4 TOMBE dans
    // le `default` (goto default ici) : opcode invalide, pc ramené à oldpc. Le
    // handler poursuit ensuite jusqu'à CLOCK_CYCLES et rend 0, comme en C.
    private static int opMOV_r_CRx_a16(uint32_t fetchdat)
    {
        if ((CPL != 0 || (cpu_state.eflags & VM_FLAG) != 0) && (cr0 & 1) != 0)
        {
                // omitted: pclog("Can't load from CRx") — sortie pure.
                x86seg_c.x86gpf(null!, 0);
                return 1;
        }
        // pcem bug, reproduced: PB-43 — mod ≠ 3 est décodé en adresse ; le 386 l'ignore.
        if (fetch_ea_16(fetchdat)) return 1;
        switch (cpu_reg)
        {
        case 0:
                cpu_state.regs[cpu_rm].l = cr0;
                if (is486 != 0)
                        cpu_state.regs[cpu_rm].l |= 0x10; /*ET hardwired on 486*/
                break;
        case 2:
                cpu_state.regs[cpu_rm].l = cr2;
                break;
        case 3:
                cpu_state.regs[cpu_rm].l = cr3;
                break;
        case 4:
                if (cpu_c.cpu_has_feature(cpu_c.CPU_FEATURE_CR4) != 0)
                {
                        cpu_state.regs[cpu_rm].l = cr4;
                        break;
                }
                goto default;
        default:
                // omitted: pclog("Bad read of CR%i %i") — sortie pure.
                cpu_state.pc = cpu_state.oldpc;
                x86illegal();
                break;
        }
        CLOCK_CYCLES(6);
        PREFETCH_RUN(6, 2, (int)fetchdat, 0, 0, 0, 0, 0);
        return 0;
    }

    // pcem: x86_ops_mov_ctrl.h:37-70
    private static int opMOV_r_CRx_a32(uint32_t fetchdat)
    {
        if ((CPL != 0 || (cpu_state.eflags & VM_FLAG) != 0) && (cr0 & 1) != 0)
        {
                // omitted: pclog("Can't load from CRx") — sortie pure.
                x86seg_c.x86gpf(null!, 0);
                return 1;
        }
        // pcem bug, reproduced: PB-43 — mod ≠ 3 est décodé en adresse ; le 386 l'ignore.
        if (fetch_ea_32(fetchdat)) return 1;
        switch (cpu_reg)
        {
        case 0:
                cpu_state.regs[cpu_rm].l = cr0;
                if (is486 != 0)
                        cpu_state.regs[cpu_rm].l |= 0x10; /*ET hardwired on 486*/
                break;
        case 2:
                cpu_state.regs[cpu_rm].l = cr2;
                break;
        case 3:
                cpu_state.regs[cpu_rm].l = cr3;
                break;
        case 4:
                if (cpu_c.cpu_has_feature(cpu_c.CPU_FEATURE_CR4) != 0)
                {
                        cpu_state.regs[cpu_rm].l = cr4;
                        break;
                }
                goto default;
        default:
                // omitted: pclog("Bad read of CR%i %i") — sortie pure.
                cpu_state.pc = cpu_state.oldpc;
                x86illegal();
                break;
        }
        CLOCK_CYCLES(6);
        PREFETCH_RUN(6, 2, (int)fetchdat, 0, 0, 0, 0, 1);
        return 0;
    }

    // pcem: x86_ops_mov_ctrl.h:72-83
    //
    // DR4 ET DR5 SE LISENT COMME LES AUTRES : PCem indexe dr[cpu_reg] sur les huit,
    // sans l'alias DR4→DR6 / DR5→DR7 du silicium.
    private static int opMOV_r_DRx_a16(uint32_t fetchdat)
    {
        if ((CPL != 0 || (cpu_state.eflags & VM_FLAG) != 0) && (cr0 & 1) != 0)
        {
                // omitted: pclog("Can't load from DRx") — sortie pure.
                x86seg_c.x86gpf(null!, 0);
                return 1;
        }
        // pcem bug, reproduced: PB-43 — mod ≠ 3 est décodé en adresse ; le 386 l'ignore.
        if (fetch_ea_16(fetchdat)) return 1;
        cpu_state.regs[cpu_rm].l = dr[cpu_reg];
        CLOCK_CYCLES(6);
        PREFETCH_RUN(6, 2, (int)fetchdat, 0, 0, 0, 0, 0);
        return 0;
    }

    // pcem: x86_ops_mov_ctrl.h:84-95
    private static int opMOV_r_DRx_a32(uint32_t fetchdat)
    {
        if ((CPL != 0 || (cpu_state.eflags & VM_FLAG) != 0) && (cr0 & 1) != 0)
        {
                // omitted: pclog("Can't load from DRx") — sortie pure.
                x86seg_c.x86gpf(null!, 0);
                return 1;
        }
        // pcem bug, reproduced: PB-43 — mod ≠ 3 est décodé en adresse ; le 386 l'ignore.
        if (fetch_ea_32(fetchdat)) return 1;
        cpu_state.regs[cpu_rm].l = dr[cpu_reg];
        CLOCK_CYCLES(6);
        PREFETCH_RUN(6, 2, (int)fetchdat, 0, 0, 0, 0, 1);
        return 0;
    }

    // pcem: x86_ops_mov_ctrl.h:97-148
    //
    // ÉCRIRE CR0 EST LA SORTIE DOCUMENTÉE DU MODE PROTÉGÉ, celle que LMSW refuse
    // (x86_ops_pmode.h:445) et qui manquait au 286. Le bit PE se pose ou s'efface
    // ici, et cpu_cur_status le suit.
    //
    // flushmmucache() SEULEMENT SI PE OU PG CHANGE (masque 0x80000001), mais TOUJOURS
    // pour CR3. mmu_perm revient à 4 dès que la pagination est coupée.
    //
    // cpu_16bitbus FORCE ET À 1 SUR UN 386SX : le bit dit « coprocesseur 387 », et
    // PCem le câble d'après la largeur du bus. Un 386DX le laisse tel qu'écrit.
    private static int opMOV_CRx_r_a16(uint32_t fetchdat)
    {
        uint32_t old_cr0 = cr0;

        if ((CPL != 0 || (cpu_state.eflags & VM_FLAG) != 0) && (cr0 & 1) != 0)
        {
                // omitted: pclog("Can't load CRx") — sortie pure.
                x86seg_c.x86gpf(null!, 0);
                return 1;
        }
        // pcem bug, reproduced: PB-43 — mod ≠ 3 est décodé en adresse ; le 386 l'ignore.
        if (fetch_ea_16(fetchdat)) return 1;
        switch (cpu_reg)
        {
        case 0:
                if (((cpu_state.regs[cpu_rm].l ^ cr0) & 0x80000001) != 0)
                        Memory.mem.flushmmucache();
                cr0 = cpu_state.regs[cpu_rm].l;
                if (cpu_16bitbus != 0)
                        cr0 |= 0x10;
                if ((cr0 & 0x80000000) == 0)
                        Memory.mem.mmu_perm = 4;
                if (is486 != 0 && (cr0 & (1 << 30)) == 0)
                        cpu_c.cpu_cache_int_enabled = 1;
                else
                        cpu_c.cpu_cache_int_enabled = 0;
                if (is486 != 0 && ((cr0 ^ old_cr0) & (1 << 30)) != 0)
                        cpu_c.cpu_update_waitstates();
                if ((cr0 & 1) != 0)
                        cpu_cur_status |= CPU_STATUS_PMODE;
                else
                        cpu_cur_status &= unchecked((uint16_t)~CPU_STATUS_PMODE);
                break;
        case 2:
                cr2 = cpu_state.regs[cpu_rm].l;
                break;
        case 3:
                cr3 = cpu_state.regs[cpu_rm].l;
                Memory.mem.flushmmucache();
                break;
        case 4:
                if (cpu_c.cpu_has_feature(cpu_c.CPU_FEATURE_CR4) != 0)
                {
                        cr4 = (uint32_t)(cpu_state.regs[cpu_rm].l & cpu_CR4_mask);
                        break;
                }
                goto default;
        default:
                // omitted: pclog("Bad load CR%i") — sortie pure.
                cpu_state.pc = cpu_state.oldpc;
                x86illegal();
                break;
        }
        CLOCK_CYCLES(10);
        PREFETCH_RUN(10, 2, (int)fetchdat, 0, 0, 0, 0, 0);
        return 0;
    }

    // pcem: x86_ops_mov_ctrl.h:149-200
    private static int opMOV_CRx_r_a32(uint32_t fetchdat)
    {
        uint32_t old_cr0 = cr0;

        if ((CPL != 0 || (cpu_state.eflags & VM_FLAG) != 0) && (cr0 & 1) != 0)
        {
                // omitted: pclog("Can't load CRx") — sortie pure.
                x86seg_c.x86gpf(null!, 0);
                return 1;
        }
        // pcem bug, reproduced: PB-43 — mod ≠ 3 est décodé en adresse ; le 386 l'ignore.
        if (fetch_ea_32(fetchdat)) return 1;
        switch (cpu_reg)
        {
        case 0:
                if (((cpu_state.regs[cpu_rm].l ^ cr0) & 0x80000001) != 0)
                        Memory.mem.flushmmucache();
                cr0 = cpu_state.regs[cpu_rm].l;
                if (cpu_16bitbus != 0)
                        cr0 |= 0x10;
                if ((cr0 & 0x80000000) == 0)
                        Memory.mem.mmu_perm = 4;
                if (is486 != 0 && (cr0 & (1 << 30)) == 0)
                        cpu_c.cpu_cache_int_enabled = 1;
                else
                        cpu_c.cpu_cache_int_enabled = 0;
                if (is486 != 0 && ((cr0 ^ old_cr0) & (1 << 30)) != 0)
                        cpu_c.cpu_update_waitstates();
                if ((cr0 & 1) != 0)
                        cpu_cur_status |= CPU_STATUS_PMODE;
                else
                        cpu_cur_status &= unchecked((uint16_t)~CPU_STATUS_PMODE);
                break;
        case 2:
                cr2 = cpu_state.regs[cpu_rm].l;
                break;
        case 3:
                cr3 = cpu_state.regs[cpu_rm].l;
                Memory.mem.flushmmucache();
                break;
        case 4:
                if (cpu_c.cpu_has_feature(cpu_c.CPU_FEATURE_CR4) != 0)
                {
                        cr4 = (uint32_t)(cpu_state.regs[cpu_rm].l & cpu_CR4_mask);
                        break;
                }
                goto default;
        default:
                // omitted: pclog("Bad load CR%i") — sortie pure.
                cpu_state.pc = cpu_state.oldpc;
                x86illegal();
                break;
        }
        CLOCK_CYCLES(10);
        PREFETCH_RUN(10, 2, (int)fetchdat, 0, 0, 0, 0, 1);
        return 0;
    }

    // pcem: x86_ops_mov_ctrl.h:202-213
    private static int opMOV_DRx_r_a16(uint32_t fetchdat)
    {
        if ((CPL != 0 || (cpu_state.eflags & VM_FLAG) != 0) && (cr0 & 1) != 0)
        {
                // omitted: pclog("Can't load DRx") — sortie pure.
                x86seg_c.x86gpf(null!, 0);
                return 1;
        }
        // pcem bug, reproduced: PB-43 — mod ≠ 3 est décodé en adresse ; le 386 l'ignore.
        if (fetch_ea_16(fetchdat)) return 1;
        dr[cpu_reg] = cpu_state.regs[cpu_rm].l;
        CLOCK_CYCLES(6);
        PREFETCH_RUN(6, 2, (int)fetchdat, 0, 0, 0, 0, 0);
        return 0;
    }

    // pcem: x86_ops_mov_ctrl.h:214-225
    private static int opMOV_DRx_r_a32(uint32_t fetchdat)
    {
        if ((CPL != 0 || (cpu_state.eflags & VM_FLAG) != 0) && (cr0 & 1) != 0)
        {
                // omitted: pclog("Can't load DRx") — sortie pure.
                x86seg_c.x86gpf(null!, 0);
                return 1;
        }
        // pcem bug, reproduced: PB-44 — la forme a32 décode en fetch_ea_16 (:220), mais
        //   passe ea32 = 1 à PREFETCH_RUN. Muet quand mod = 3, la forme d'usage.
        // pcem bug, reproduced: PB-43 — mod ≠ 3 est décodé en adresse ; le 386 l'ignore.
        if (fetch_ea_16(fetchdat)) return 1;
        dr[cpu_reg] = cpu_state.regs[cpu_rm].l;
        CLOCK_CYCLES(6);
        PREFETCH_RUN(6, 2, (int)fetchdat, 0, 0, 0, 0, 1);
        return 0;
    }

    // pcem: x86_ops_mov_ctrl.h:227-238
    private static int opMOV_r_TRx_a16(uint32_t fetchdat)
    {
        if ((CPL != 0 || (cpu_state.eflags & VM_FLAG) != 0) && (cr0 & 1) != 0)
        {
                // omitted: pclog("Can't load from TRx") — sortie pure.
                x86seg_c.x86gpf(null!, 0);
                return 1;
        }
        // pcem bug, reproduced: PB-43 — mod ≠ 3 est décodé en adresse ; le 386 l'ignore.
        if (fetch_ea_16(fetchdat)) return 1;
        cpu_state.regs[cpu_rm].l = 0;
        CLOCK_CYCLES(6);
        PREFETCH_RUN(6, 2, (int)fetchdat, 0, 0, 0, 0, 0);
        return 0;
    }

    // pcem: x86_ops_mov_ctrl.h:239-250
    private static int opMOV_r_TRx_a32(uint32_t fetchdat)
    {
        if ((CPL != 0 || (cpu_state.eflags & VM_FLAG) != 0) && (cr0 & 1) != 0)
        {
                // omitted: pclog("Can't load from TRx") — sortie pure.
                x86seg_c.x86gpf(null!, 0);
                return 1;
        }
        // pcem bug, reproduced: PB-43 — mod ≠ 3 est décodé en adresse ; le 386 l'ignore.
        if (fetch_ea_32(fetchdat)) return 1;
        cpu_state.regs[cpu_rm].l = 0;
        CLOCK_CYCLES(6);
        PREFETCH_RUN(6, 2, (int)fetchdat, 0, 0, 0, 0, 1);
        return 0;
    }

    // pcem: x86_ops_mov_ctrl.h:252-262
    private static int opMOV_TRx_r_a16(uint32_t fetchdat)
    {
        if ((CPL != 0 || (cpu_state.eflags & VM_FLAG) != 0) && (cr0 & 1) != 0)
        {
                // omitted: pclog("Can't load TRx") — sortie pure.
                x86seg_c.x86gpf(null!, 0);
                return 1;
        }
        // pcem bug, reproduced: PB-43 — mod ≠ 3 est décodé en adresse ; le 386 l'ignore.
        if (fetch_ea_16(fetchdat)) return 1;
        CLOCK_CYCLES(6);
        PREFETCH_RUN(6, 2, (int)fetchdat, 0, 0, 0, 0, 0);
        return 0;
    }

    // pcem: x86_ops_mov_ctrl.h:263-273
    private static int opMOV_TRx_r_a32(uint32_t fetchdat)
    {
        if ((CPL != 0 || (cpu_state.eflags & VM_FLAG) != 0) && (cr0 & 1) != 0)
        {
                // omitted: pclog("Can't load TRx") — sortie pure.
                x86seg_c.x86gpf(null!, 0);
                return 1;
        }
        // pcem bug, reproduced: PB-44 — fetch_ea_16 dans la forme a32 (:269), comme MOV DRx,r.
        // pcem bug, reproduced: PB-43 — mod ≠ 3 est décodé en adresse ; le 386 l'ignore.
        if (fetch_ea_16(fetchdat)) return 1;
        CLOCK_CYCLES(6);
        PREFETCH_RUN(6, 2, (int)fetchdat, 0, 0, 0, 0, 1);
        return 0;
    }

    /// <summary>pcem: 386_ops.h — OP_TABLE(386_0f), emplacements 20 à 26 : a16 dans
    /// les quadrants 0 et 1, a32 dans 2 et 3. 0F 25 est ILLEGAL dans la table de
    /// PCem, et déjà posé par la part partagée.</summary>
    private static void PoserGroupe_mov_ctrl_0f_386()
    {
        for (var q = 0; q < 4; q++)
        {
                var a32 = q >= 2;
                var b = q << 8;
                ops_386_0f[b | 0x20] = a32 ? opMOV_r_CRx_a32 : opMOV_r_CRx_a16;
                ops_386_0f[b | 0x21] = a32 ? opMOV_r_DRx_a32 : opMOV_r_DRx_a16;
                ops_386_0f[b | 0x22] = a32 ? opMOV_CRx_r_a32 : opMOV_CRx_r_a16;
                ops_386_0f[b | 0x23] = a32 ? opMOV_DRx_r_a32 : opMOV_DRx_r_a16;
                ops_386_0f[b | 0x24] = a32 ? opMOV_r_TRx_a32 : opMOV_r_TRx_a16;
                ops_386_0f[b | 0x26] = a32 ? opMOV_TRx_r_a32 : opMOV_TRx_r_a16;
        }
    }
}
