// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/includes/private/cpu/x86_ops_pmode.h  (op0F01_common :352-485,
//         op0F01_w/l_a16/a32 :483-502, op0F01_286 :504-508, op0F00_a16/a32 :337-350,
//         opLAR :56-114, opLSL :116-174)
//         et pcem-dev/includes/private/cpu/x86_ops_misc.h  (opCLTS :796-806,
//         opLOADALL :827-881, set_segment_limit, loadall_load_segment et
//         opLOADALL386 :885-974)
// SHA256: voir oracle.tsv ; vérifier avec tools/check-oracle.sh
// STATUS: partial — les six emplacements de ops_286_0f, et depuis G2 D4 les
//         formes 32 bits de 0F 00 à 0F 03 et LOADALL386 (0F 07) de ops_386_0f.
//         Reste omis : INVLPG (486).
//
// LA SECONDE TABLE, ET LA PORTE DU MODE PROTÉGÉ.
//
// ops_286_0f ne porte que SIX handlers sur un 286 — relevés dans la .so par
// gdb, pas supposés. Cinq d'entre eux sont du mode protégé pur. Le sixième,
// op0F01_286, est le plus important de tout le jalon : il porte LGDT, LIDT,
// SGDT, SIDT, SMSW et LMSW.
//
// LMSW EST LA PORTE D'ENTRÉE. Un 286 passe en mode protégé en posant le bit 0
// du mot d'état machine, et LMSW est le seul moyen de le faire — il n'y a pas
// de MOV CR0 avant le 386. Le POST de l'IBM AT le franchit pour dimensionner la
// mémoire au-dessus du méga-octet, puis en RESSORT par la ligne de reset du
// 8042, faute d'autre issue.
//
// ET IL NE PERMET PAS D'EN SORTIR : `if (msw & 1) tempw |= 1`. Une fois le bit
// posé, LMSW ne peut plus l'effacer. C'est le silicium, pas PCem.
//
// TROIS ÉCARTS QUI DÉPENDENT DU PROCESSEUR, et un 286 prend à chaque fois la
// branche la plus ancienne :
//   - SGDT et SIDT forcent les huit bits hauts de la base à UN (`base |=
//     0xff000000`) quand is286. Un 386 les laisse tels quels.
//   - SMSW rend `msw | 0xFFF0` sur un 286, `msw | 0xFF00` sur un 386, `msw` nu
//     sur un 486.
//   - LMSW masque à `0xF` sur un 286, là où un 386 préserve le bit 4.

using static iXtal26.Cpu._386_common;
using static iXtal26.Cpu.x86;
using static iXtal26.Cpu.x86_flags;

namespace iXtal26.Cpu;

internal static partial class _386
{
    /// <summary>pcem: x86_ops_pmode.h:352-485 — op0F01_common(fetchdat, is32,
    /// is286, ea32), appelé avec (0, 1, 0) par op0F01_286.
    ///
    /// DEVIATION: `return 1` depuis le milieu ; la méthode rend `true` pour
    ///   « l'appelant doit rendre 1 ». Les `break` du switch mènent au
    ///   `return cpu_state.abrt` final, comme en C.</summary>
    private static bool op0F01_common(uint32_t rmdat, int is32, int is286, int ea32)
    {
        uint32_t @base;
        uint16_t limit, tempw;

        switch (rmdat & 0x38)
        {
        case 0x00: /*SGDT*/
                if (cpu_mod != 3)
                        if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return true;
                seteaw((uint16_t)gdt.limit);
                @base = gdt.@base;
                if (is286 != 0)
                        @base |= 0xff000000;
                writememl(easeg, cpu_state.eaaddr + 2, @base);
                CLOCK_CYCLES(7);
                PREFETCH_RUN(7, 2, (int)rmdat, 0, 0, 1, 1, ea32);
                break;
        case 0x08: /*SIDT*/
                if (cpu_mod != 3)
                        if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return true;
                seteaw((uint16_t)idt.limit);
                @base = idt.@base;
                if (is286 != 0)
                        @base |= 0xff000000;
                writememl(easeg, cpu_state.eaaddr + 2, @base);
                CLOCK_CYCLES(7);
                PREFETCH_RUN(7, 2, (int)rmdat, 0, 0, 1, 1, ea32);
                break;
        case 0x10: /*LGDT*/
                if ((CPL != 0 || (cpu_state.eflags & VM_FLAG) != 0) && (cr0 & 1) != 0)
                {
                        x86seg_c.x86gpf("", 0);
                        break;
                }
                if (cpu_mod != 3)
                        if (SEG_CHECK_READ(cpu_state.ea_seg!)) return true;
                limit = geteaw();
                @base = readmeml(0, easeg + cpu_state.eaaddr + 2);
                if (cpu_state.abrt != 0)
                        return true;
                gdt.limit = limit;
                gdt.@base = @base;
                if (is32 == 0)
                        gdt.@base &= 0xffffff;
                CLOCK_CYCLES(11);
                PREFETCH_RUN(11, 2, (int)rmdat, 1, 1, 0, 0, ea32);
                break;
        case 0x18: /*LIDT*/
                if ((CPL != 0 || (cpu_state.eflags & VM_FLAG) != 0) && (cr0 & 1) != 0)
                {
                        x86seg_c.x86gpf("", 0);
                        break;
                }
                if (cpu_mod != 3)
                        if (SEG_CHECK_READ(cpu_state.ea_seg!)) return true;
                limit = geteaw();
                @base = readmeml(0, easeg + cpu_state.eaaddr + 2);
                if (cpu_state.abrt != 0)
                        return true;
                idt.limit = limit;
                idt.@base = @base;
                if (is32 == 0)
                        idt.@base &= 0xffffff;
                CLOCK_CYCLES(11);
                PREFETCH_RUN(11, 2, (int)rmdat, 1, 1, 0, 0, ea32);
                break;

        case 0x20: /*SMSW*/
                if (cpu_mod != 3)
                        if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return true;
                if (is486 != 0)
                        seteaw(msw);
                else if (is386 != 0)
                        seteaw((uint16_t)(msw | 0xFF00));
                else
                        seteaw((uint16_t)(msw | 0xFFF0));
                CLOCK_CYCLES(2);
                PREFETCH_RUN(2, 2, (int)rmdat, 0, 0, (cpu_mod == 3) ? 0 : 1, 0, ea32);
                break;
        case 0x30: /*LMSW*/
                if ((CPL != 0 || (cpu_state.eflags & VM_FLAG) != 0) && (msw & 1) != 0)
                {
                        x86seg_c.x86gpf("", 0);
                        break;
                }
                if (cpu_mod != 3)
                        if (SEG_CHECK_READ(cpu_state.ea_seg!)) return true;
                tempw = geteaw();
                if (cpu_state.abrt != 0)
                        return true;
                // ON N'EN SORT PAS : une fois le bit 0 pose, LMSW ne peut plus
                // l'effacer. C'est le silicium ; la seule issue d'un 286 est la
                // ligne de reset du 8042.
                if ((msw & 1) != 0)
                        tempw |= 1;
                if (is386 != 0)
                {
                        tempw &= unchecked((uint16_t)~0x10);
                        tempw |= (uint16_t)(msw & 0x10);
                }
                else
                        tempw &= 0xF;
                msw = tempw;
                if ((msw & 1) != 0)
                        cpu_cur_status |= CPU_STATUS_PMODE;
                else
                        cpu_cur_status &= unchecked((uint16_t)~CPU_STATUS_PMODE);
                // PAS de CLOCK_CYCLES ici — PCem n'en met pas, alors que
                // PREFETCH_RUN recoit 2. Transcrit tel quel.
                PREFETCH_RUN(2, 2, (int)rmdat, 0, 0, (cpu_mod == 3) ? 0 : 1, 0, ea32);
                break;

        // omitted: le cas 0x38 (INVLPG) — garde par `if (is486)`, et sans ce
        //   garde il tombe dans le `default`. Sur un 286 comme sur un 386 c'est donc
        //   un opcode illegal, ce que la branche ci-dessous produit. Il arrive avec
        //   mmu_invalidate, au 486 (G6) et apres la pagination (D6).

        default:
                cpu_state.pc -= 3;
                x86illegal();
                break;
        }
        return false;
    }

    // pcem: x86_ops_pmode.h:504-508 — op0F01_286
    private static int op0F01_286(uint32_t fetchdat)
    {
        if (fetch_ea_16(fetchdat)) return 1;

        if (op0F01_common(fetchdat, 0, 1, 0)) return 1;
        return cpu_state.abrt;
    }

    // pcem: x86_ops_misc.h:796-806 — opCLTS.
    //
    // ATTEIGNABLE EN MODE REEL : sa garde est `(CPL || VM) && (cr0 & 1)`, et
    // cr0 & 1 est nul hors mode protege. Il efface donc simplement le bit 3 de
    // cr0 — le drapeau « changement de tache » du coprocesseur.
    private static int opCLTS(uint32_t fetchdat)
    {
        if ((CPL != 0 || (cpu_state.eflags & VM_FLAG) != 0) && (cr0 & 1) != 0)
        {
                x86seg_c.x86gpf("", 0);
                return 1;
        }
        cr0 &= unchecked((uint32_t)~8);
        CLOCK_CYCLES(5);
        PREFETCH_RUN(5, 1, -1, 0, 0, 0, 0, 0);
        return 0;
    }

    // pcem: x86_ops_misc.h:827-881 — opLOADALL.
    //
    // L'OPCODE NON DOCUMENTE DU 286, et le plus brutal de la table : il recharge
    // L'ETAT ENTIER du processeur — registres, segments avec leurs caches
    // descripteurs, GDT, LDT, IDT, TR — depuis un bloc fixe a l'adresse
    // physique 0x800. Cinquante et une lectures de seize bits.
    //
    // IL EXISTE PARCE QU'UN 286 NE SAIT PAS SORTIR DU MODE PROTEGE. Le BIOS de
    // l'AT s'en sert apres un reset pour restaurer l'etat qu'il avait sauvegarde
    // avant d'y entrer. C'est l'autre moitie du mecanisme dont LMSW est
    // l'entree, et ni l'un ni l'autre n'a de sens sans le 8042.
    //
    // ATTEIGNABLE EN MODE REEL : sa garde est `CPL && (cr0 & 1)`.
    private static int opLOADALL(uint32_t fetchdat)
    {
        if (CPL != 0 && (cr0 & 1) != 0)
        {
                x86seg_c.x86gpf("", 0);
                return 1;
        }
        msw = (uint16_t)((msw & 1) | readmemw(0, 0x806));
        cpu_state.flags = (uint16_t)((readmemw(0, 0x818) & 0xffd5) | 2);
        flags_extract();
        tr.seg = readmemw(0, 0x816);
        cpu_state.pc = readmemw(0, 0x81A);
        ldt.seg = readmemw(0, 0x81C);
        DS = readmemw(0, 0x81E);
        SS = readmemw(0, 0x820);
        CS = readmemw(0, 0x822);
        ES = readmemw(0, 0x824);
        DI = readmemw(0, 0x826);
        SI = readmemw(0, 0x828);
        BP = readmemw(0, 0x82A);
        SP = readmemw(0, 0x82C);
        BX = readmemw(0, 0x82E);
        DX = readmemw(0, 0x830);
        CX = readmemw(0, 0x832);
        AX = readmemw(0, 0x834);
        es = (uint32_t)(readmemw(0, 0x836) | (readmemb(0, 0x838) << 16));
        cpu_state.seg_es.access = readmemb(0, 0x839);
        cpu_state.seg_es.limit = readmemw(0, 0x83A);
        cs = (uint32_t)(readmemw(0, 0x83C) | (readmemb(0, 0x83E) << 16));
        cpu_state.seg_cs.access = readmemb(0, 0x83F);
        cpu_state.seg_cs.limit = readmemw(0, 0x840);
        ss = (uint32_t)(readmemw(0, 0x842) | (readmemb(0, 0x844) << 16));
        cpu_state.seg_ss.access = readmemb(0, 0x845);
        cpu_state.seg_ss.limit = readmemw(0, 0x846);
        if (cpu_state.seg_ss.@base == 0 && cpu_state.seg_ss.limit_low == 0 &&
            cpu_state.seg_ss.limit_high == 0xffffffff)
                cpu_cur_status &= unchecked((uint16_t)~CPU_STATUS_NOTFLATSS);
        else
                cpu_cur_status |= CPU_STATUS_NOTFLATSS;
        ds = (uint32_t)(readmemw(0, 0x848) | (readmemb(0, 0x84A) << 16));
        cpu_state.seg_ds.access = readmemb(0, 0x84B);
        cpu_state.seg_ds.limit = readmemw(0, 0x84C);
        if (cpu_state.seg_ds.@base == 0 && cpu_state.seg_ds.limit_low == 0 &&
            cpu_state.seg_ds.limit_high == 0xffffffff)
                cpu_cur_status &= unchecked((uint16_t)~CPU_STATUS_NOTFLATDS);
        else
                cpu_cur_status |= CPU_STATUS_NOTFLATDS;
        gdt.@base = (uint32_t)(readmemw(0, 0x84E) | (readmemb(0, 0x850) << 16));
        gdt.limit = readmemw(0, 0x852);
        ldt.@base = (uint32_t)(readmemw(0, 0x854) | (readmemb(0, 0x856) << 16));
        ldt.access = readmemb(0, 0x857);
        ldt.limit = readmemw(0, 0x858);
        idt.@base = (uint32_t)(readmemw(0, 0x85A) | (readmemb(0, 0x85C) << 16));
        idt.limit = readmemw(0, 0x85E);
        tr.@base = (uint32_t)(readmemw(0, 0x860) | (readmemb(0, 0x862) << 16));
        tr.access = readmemb(0, 0x863);
        tr.limit = readmemw(0, 0x864);
        CLOCK_CYCLES(195);
        PREFETCH_RUN(195, 1, -1, 51, 0, 0, 0, 0);
        return 0;
    }


    // pcem: x86_ops_pmode.h:56-114 — la macro opLAR(name, fetch_ea, is32, ea32), et ses
    // quatre instances. M20 : Windows 3.x en mode standard valide ses sélecteurs avec la
    // forme w_a16, la seule que ops_286_0f référence. G2, D4 : les trois autres. Vérifié
    // par pm-check pour la w_a16 ; les formes 32 bits n'atteignent que NOTRM en mode réel.
    //
    // LE VERDICT EST DANS ZF, ET LAR NE FAUTE PAS SUR UN MAUVAIS SÉLECTEUR : il efface ZF
    // et rend. Le type n'est refusé que pour 0, 8, A et D (réservés), et le privilège
    // n'est vérifié que hors code conforme.
    //
    // LA FORME 32 BITS LIT UN MOT DOUBLE ET GARDE 0xFFFF00 : les octets 5 et 6 du
    // descripteur, droits d'accès ET quartet haut de la limite avec G, D/B et AVL. La forme
    // 16 bits s'arrête à l'octet d'accès.
    //
    // DEVIATION: la macro devient une méthode ; `fetch_ea` se choisit par `ea32`, ce que
    // font les quatre instances.
    private static int opLAR(uint32_t fetchdat, int is32, int ea32)
    {
        int valid;
        uint16_t sel, desc = 0;

        if (NOTRM()) return 1;
        if (ea32 != 0 ? fetch_ea_32(fetchdat) : fetch_ea_16(fetchdat)) return 1;
        if (cpu_mod != 3)
                if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;

        sel = geteaw();
        if (cpu_state.abrt != 0)
                return 1;

        flags_rebuild();
        if ((sel & 0xfffc) == 0)
        {
                cpu_state.flags &= unchecked((uint16_t)~Z_FLAG);
                return 0;
        } /*Null selector*/
        valid = (sel & ~7) < ((sel & 4) != 0 ? ldt.limit : gdt.limit) ? 1 : 0;
        if (valid != 0)
        {
                cpl_override = 1;
                desc = readmemw(0, (uint32_t)(((sel & 4) != 0 ? ldt.@base : gdt.@base) + (sel & ~7) + 4));
                cpl_override = 0;
                if (cpu_state.abrt != 0)
                        return 1;
        }
        cpu_state.flags &= unchecked((uint16_t)~Z_FLAG);
        if ((desc & 0x1f00) == 0x000)
                valid = 0;
        if ((desc & 0x1f00) == 0x800)
                valid = 0;
        if ((desc & 0x1f00) == 0xa00)
                valid = 0;
        if ((desc & 0x1f00) == 0xd00)
                valid = 0;
        if ((desc & 0x1c00) < 0x1c00) /*Exclude conforming code segments*/
        {
                int dpl = (desc >> 13) & 3;
                if (dpl < CPL || dpl < (sel & 3))
                        valid = 0;
        }
        if (valid != 0)
        {
                cpu_state.flags |= Z_FLAG;
                cpl_override = 1;
                if (is32 != 0)
                        cpu_state.regs[cpu_reg].l =
                                readmeml(0, (uint32_t)(((sel & 4) != 0 ? ldt.@base : gdt.@base) + (sel & ~7) + 4)) & 0xffff00;
                else
                        cpu_state.regs[cpu_reg].w =
                                (uint16_t)(readmemw(0, (uint32_t)(((sel & 4) != 0 ? ldt.@base : gdt.@base) + (sel & ~7) + 4)) & 0xff00);
                cpl_override = 0;
        }
        CLOCK_CYCLES(11);
        PREFETCH_RUN(11, 2, (int)fetchdat, 2, 0, 0, 0, ea32);
        return cpu_state.abrt;
    }

    // pcem: x86_ops_pmode.h:114 — les quatre instances de opLAR.
    private static int opLAR_w_a16(uint32_t fetchdat) => opLAR(fetchdat, 0, 0);
    private static int opLAR_w_a32(uint32_t fetchdat) => opLAR(fetchdat, 0, 1);
    private static int opLAR_l_a16(uint32_t fetchdat) => opLAR(fetchdat, 1, 0);
    private static int opLAR_l_a32(uint32_t fetchdat) => opLAR(fetchdat, 1, 1);

    // pcem: x86_ops_pmode.h:116-174 — la macro opLSL, même traitement que opLAR.
    //
    // PLUS PERMISSIF QUE LAR SUR LES TYPES : il refuse toute porte (`(desc & 0x1400) ==
    // 0x400`) et les types 0 et A, mais accepte un TSS ou une LDT — dont la limite a un
    // sens. Et sa variable s'appelle `rpl` là où elle lit un DPL : le nom est de PCem.
    //
    // LA FORME 32 BITS REND LA LIMITE EN OCTETS, granularité appliquée : les vingt bits
    // bruts, décalés de 12 et complétés par 0xFFF quand G est posé. La forme 16 bits
    // rend le mot bas brut, sans G.
    private static int opLSL(uint32_t fetchdat, int is32, int ea32)
    {
        int valid;
        uint16_t sel, desc = 0;

        if (NOTRM()) return 1;
        if (ea32 != 0 ? fetch_ea_32(fetchdat) : fetch_ea_16(fetchdat)) return 1;
        if (cpu_mod != 3)
                if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;

        sel = geteaw();
        if (cpu_state.abrt != 0)
                return 1;
        flags_rebuild();
        cpu_state.flags &= unchecked((uint16_t)~Z_FLAG);
        if ((sel & 0xfffc) == 0)
                return 0; /*Null selector*/
        valid = (sel & ~7) < ((sel & 4) != 0 ? ldt.limit : gdt.limit) ? 1 : 0;
        if (valid != 0)
        {
                cpl_override = 1;
                desc = readmemw(0, (uint32_t)(((sel & 4) != 0 ? ldt.@base : gdt.@base) + (sel & ~7) + 4));
                cpl_override = 0;
                if (cpu_state.abrt != 0)
                        return 1;
        }
        if ((desc & 0x1400) == 0x400)
                valid = 0; /*Interrupt or trap or call gate*/
        if ((desc & 0x1f00) == 0x000)
                valid = 0; /*Invalid*/
        if ((desc & 0x1f00) == 0xa00)
                valid = 0;             /*Invalid*/
        if ((desc & 0x1c00) != 0x1c00) /*Exclude conforming code segments*/
        {
                int rpl = (desc >> 13) & 3;
                if (rpl < CPL || rpl < (sel & 3))
                        valid = 0;
        }
        if (valid != 0)
        {
                cpu_state.flags |= Z_FLAG;
                cpl_override = 1;
                if (is32 != 0)
                {
                        cpu_state.regs[cpu_reg].l = readmemw(0, (uint32_t)(((sel & 4) != 0 ? ldt.@base : gdt.@base) + (sel & ~7)));
                        cpu_state.regs[cpu_reg].l |=
                                (uint32_t)((readmemb(0, (uint32_t)(((sel & 4) != 0 ? ldt.@base : gdt.@base) + (sel & ~7) + 6)) & 0xF) << 16);
                        if ((readmemb(0, (uint32_t)(((sel & 4) != 0 ? ldt.@base : gdt.@base) + (sel & ~7) + 6)) & 0x80) != 0)
                        {
                                cpu_state.regs[cpu_reg].l <<= 12;
                                cpu_state.regs[cpu_reg].l |= 0xFFF;
                        }
                }
                else
                        cpu_state.regs[cpu_reg].w = readmemw(0, (uint32_t)(((sel & 4) != 0 ? ldt.@base : gdt.@base) + (sel & ~7)));
                cpl_override = 0;
        }
        CLOCK_CYCLES(10);
        PREFETCH_RUN(10, 2, (int)fetchdat, 4, 0, 0, 0, ea32);
        return cpu_state.abrt;
    }

    // pcem: x86_ops_pmode.h:173-174 — les quatre instances de opLSL.
    private static int opLSL_w_a16(uint32_t fetchdat) => opLSL(fetchdat, 0, 0);
    private static int opLSL_w_a32(uint32_t fetchdat) => opLSL(fetchdat, 0, 1);
    private static int opLSL_l_a16(uint32_t fetchdat) => opLSL(fetchdat, 1, 0);
    private static int opLSL_l_a32(uint32_t fetchdat) => opLSL(fetchdat, 1, 1);

    /// <summary>pcem: x86_ops_pmode.h:176-336 — op0F00_common : SLDT, STR, LLDT, LTR,
    /// VERR, VERW, aiguillés par le champ `reg` du ModRM.
    ///
    /// LE POST DE L'IBM AT EN A BESOIN, et c'est une vraie exécution qui l'a montré, pas
    /// une lecture : `--boot roms 6000 --model ibmat` tombait sur le fatal() de
    /// NotrmPuisEchec à la tranche 600, après 4 799 914 instructions, alors que le
    /// boot-diff n'y arrivait jamais.
    ///
    /// LLDT ET LTR LISENT LE DESCRIPTEUR OCTET PAR OCTET, pas par mots de seize bits
    /// comme do_seg_load. Ce n'est pas une coquette de PCem : la base d'un descripteur
    /// système est éparpillée sur les octets 2-3, 4 et 7, et la limite sur 0-1 plus le
    /// quartet bas de l'octet 6. Lire par mots obligerait à masquer davantage.
    ///
    /// ET LTR ÉCRIT DANS LA TABLE, LLDT NON. `access |= 2` puis `writememb(0, addr + 5,
    /// access)` : charger le registre de tâche marque le TSS OCCUPÉ, pour qu'une seconde
    /// commutation vers la même tâche soit refusée. C'est le seul effet de bord mémoire
    /// des six, et il tombe sous le même angle mort que le bit d'accès — aucun compteur
    /// ne le surveille.
    ///
    /// VERR ET VERW RENDENT LEUR VERDICT DANS ZF, ET NE FAUTENT JAMAIS. C'est leur raison
    /// d'être : demander « puis-je lire ce sélecteur ? » sans risquer le #GP que la
    /// lecture provoquerait. D'où `cpl_override` autour de la lecture du descripteur, et
    /// le retour précoce sur sélecteur nul — ZF déjà effacé, donc « non ».
    ///
    /// LEUR DIFFÉRENCE EST PLUS FINE QU'ELLE N'EN A L'AIR : VERR accepte un code
    /// conforme SANS vérifier le privilège — `(desc & 0xC00) != 0xC00` exclut ce cas du
    /// test — et refuse un code non lisible. VERW refuse TOUT code et exige une donnée
    /// inscriptible. Échanger les deux donnerait un émulateur qui marche presque.</summary>
    // LE PARAMÈTRE S'APPELLE rmdat, ET C'EST LA CORRECTION DE M20. Chez PCem, x86.h:197
    // fait `#define fetchdat rmdat` : dans op0F00_common(uint32_t fetchdat, ...), `rmdat`
    // désigne donc le PARAMÈTRE — les octets qui suivent `0F 00`. Nommé `fetchdat` ici,
    // le corps lisait la GLOBALE rmdat, que seule la boucle d'exec386 pose : pour tout
    // opcode à un octet les deux coïncident, mais derrière l'échappement 0F la globale
    // commence un octet trop tôt, sur le `00`. Les six instructions du groupe
    // s'exécutaient toutes en SLDT. Trouvé par Windows 3.11 : DOSX fait `LLDT AX` puis
    // `LTR SI`, TR restait nul, et sa première porte d'appel vers l'anneau 0 lisait une
    // « TSS » dans du texte. L'AT ne l'avait jamais montré : son boot-diff diverge à
    // l'instruction 50, sur le CMOS. pm-check le vérifie depuis.
    private static int op0F00_common(uint32_t rmdat, int ea32)
    {
        int dpl, valid, granularity;
        uint32_t addr, @base, limit;
        uint16_t desc, sel;
        uint8_t access, access2;

        switch (rmdat & 0x38)
        {
        case 0x00: /*SLDT*/
                if (cpu_mod != 3)
                        if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
                seteaw(ldt.seg);
                CLOCK_CYCLES(4);
                PREFETCH_RUN(4, 2, (int)rmdat, 0, 0, (cpu_mod == 3) ? 0 : 1, 0, ea32);
                break;
        case 0x08: /*STR*/
                if (cpu_mod != 3)
                        if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
                seteaw(tr.seg);
                CLOCK_CYCLES(4);
                PREFETCH_RUN(4, 2, (int)rmdat, 0, 0, (cpu_mod == 3) ? 0 : 1, 0, ea32);
                break;
        case 0x10: /*LLDT*/
                // `(CPL || VM_FLAG) && (cr0 & 1)` : en mode REEL la garde est fausse, donc
                // LLDT y est legal — c'est NOTRM en amont qui l'y interdit, pas ceci.
                if ((CPL != 0 || (cpu_state.eflags & VM_FLAG) != 0) && (cr0 & 1) != 0)
                {
                        // omitted: pclog("Invalid LLDT!") — sortie pure.
                        x86seg_c.x86gpf(null!, 0);
                        return 1;
                }
                if (cpu_mod != 3)
                        if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
                sel = geteaw();
                if (cpu_state.abrt != 0)
                        return 1;
                addr = (uint32_t)(sel & ~7) + gdt.@base;
                limit = (uint32_t)(readmemw(0, addr) + ((readmemb(0, addr + 6) & 0xf) << 16));
                @base = (uint32_t)(readmemw(0, addr + 2) | (readmemb(0, addr + 4) << 16)
                                   | (readmemb(0, addr + 7) << 24));
                access = readmemb(0, addr + 5);
                access2 = readmemb(0, addr + 6);
                granularity = readmemb(0, addr + 6) & 0x80;
                if (cpu_state.abrt != 0)
                        return 1;
                ldt.limit = limit;
                ldt.limit_raw = limit;
                ldt.access = access;
                ldt.access2 = access2;
                if (granularity != 0)
                {
                        ldt.limit <<= 12;
                        ldt.limit |= 0xfff;
                }
                ldt.@base = @base;
                ldt.seg = sel;
                CLOCK_CYCLES(20);
                PREFETCH_RUN(20, 2, (int)rmdat, (cpu_mod == 3) ? 0 : 1, 2, 0, 0, ea32);
                break;
        case 0x18: /*LTR*/
                // NOTER LE `break` ET NON `return 1` de LLDT. La difference est dans le C
                // et elle est visible : apres x86gpf, LLDT rend 1 et LTR tombe en bas de
                // la fonction, qui rend `cpu_state.abrt`. Les deux valent 1 puisque x86gpf
                // vient de le poser — mais la forme se recopie, pas le raisonnement.
                if ((CPL != 0 || (cpu_state.eflags & VM_FLAG) != 0) && (cr0 & 1) != 0)
                {
                        // omitted: pclog("Invalid LTR!") — sortie pure.
                        x86seg_c.x86gpf(null!, 0);
                        break;
                }
                if (cpu_mod != 3)
                        if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
                sel = geteaw();
                if (cpu_state.abrt != 0)
                        return 1;
                addr = (uint32_t)(sel & ~7) + gdt.@base;
                limit = (uint32_t)(readmemw(0, addr) + ((readmemb(0, addr + 6) & 0xf) << 16));
                @base = (uint32_t)(readmemw(0, addr + 2) | (readmemb(0, addr + 4) << 16)
                                   | (readmemb(0, addr + 7) << 24));
                access = readmemb(0, addr + 5);
                access2 = readmemb(0, addr + 6);
                granularity = readmemb(0, addr + 6) & 0x80;
                if (cpu_state.abrt != 0)
                        return 1;
                // LE TSS EST MARQUE OCCUPE, ET C'EST UNE ECRITURE EN MEMOIRE. Bit 1 de
                // l'octet d'acces. Aucun compteur ne la surveille — meme angle mort que le
                // bit d'accede des descripteurs de segment.
                access |= 2;
                writememb(0, addr + 5, access);
                if (cpu_state.abrt != 0)
                        return 1;
                tr.seg = sel;
                tr.limit = limit;
                tr.limit_raw = limit;
                tr.access = access;
                tr.access2 = access2;
                if (granularity != 0)
                {
                        tr.limit <<= 12;
                        tr.limit |= 0xFFF;
                }
                tr.@base = @base;
                CLOCK_CYCLES(20);
                PREFETCH_RUN(20, 2, (int)rmdat, (cpu_mod == 3) ? 0 : 1, 2, 0, 0, ea32);
                break;
        case 0x20: /*VERR*/
                if (cpu_mod != 3)
                        if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
                sel = geteaw();
                if (cpu_state.abrt != 0)
                        return 1;
                flags_rebuild();
                cpu_state.flags &= unchecked((uint16_t)~Z_FLAG);
                if ((sel & 0xfffc) == 0)
                        return 0; /*Null selector*/
                cpl_override = 1;
                valid = (sel & ~7) < (int)(((sel & 4) != 0) ? ldt.limit : gdt.limit) ? 1 : 0;
                desc = readmemw(0, (((sel & 4) != 0) ? ldt.@base : gdt.@base) + (uint32_t)(sel & ~7) + 4);
                cpl_override = 0;
                if (cpu_state.abrt != 0)
                        return 1;
                if ((desc & 0x1000) == 0)
                        valid = 0;
                if ((desc & 0xC00) != 0xC00) /*Exclude conforming code segments*/
                {
                        dpl = (desc >> 13) & 3; /*Check permissions*/
                        if (dpl < CPL || dpl < (sel & 3))
                                valid = 0;
                }
                if ((desc & 0x0800) != 0 && (desc & 0x0200) == 0)
                        valid = 0; /*Non-readable code*/
                if (valid != 0)
                        cpu_state.flags |= Z_FLAG;
                CLOCK_CYCLES(20);
                PREFETCH_RUN(20, 2, (int)rmdat, (cpu_mod == 3) ? 1 : 2, 0, 0, 0, ea32);
                break;
        case 0x28: /*VERW*/
                if (cpu_mod != 3)
                        if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
                sel = geteaw();
                if (cpu_state.abrt != 0)
                        return 1;
                flags_rebuild();
                cpu_state.flags &= unchecked((uint16_t)~Z_FLAG);
                if ((sel & 0xfffc) == 0)
                        return 0; /*Null selector*/
                cpl_override = 1;
                valid = (sel & ~7) < (int)(((sel & 4) != 0) ? ldt.limit : gdt.limit) ? 1 : 0;
                desc = readmemw(0, (((sel & 4) != 0) ? ldt.@base : gdt.@base) + (uint32_t)(sel & ~7) + 4);
                cpl_override = 0;
                if (cpu_state.abrt != 0)
                        return 1;
                if ((desc & 0x1000) == 0)
                        valid = 0;
                dpl = (desc >> 13) & 3; /*Check permissions*/
                if (dpl < CPL || dpl < (sel & 3))
                        valid = 0;
                if ((desc & 0x0800) != 0)
                        valid = 0; /*Code*/
                if ((desc & 0x0200) == 0)
                        valid = 0; /*Read-only data*/
                if (valid != 0)
                        cpu_state.flags |= Z_FLAG;
                CLOCK_CYCLES(20);
                PREFETCH_RUN(20, 2, (int)rmdat, (cpu_mod == 3) ? 1 : 2, 0, 0, 0, ea32);
                break;

        default:
                // omitted: pclog("Bad 0F 00 opcode %02X") — sortie pure.
                // `pc -= 3` AVANT x86illegal : l'INT 6 doit pointer sur l'instruction
                // fautive, et pc a deja avance de trois octets — 0F, 00 et le ModRM.
                cpu_state.pc -= 3;
                _386_common.x86illegal();
                break;
        }
        return cpu_state.abrt;
    }

    // pcem: x86_ops_pmode.h:337-342
    private static int op0F00_a16(uint32_t fetchdat)
    {
        if (NOTRM()) return 1;
        if (fetch_ea_16(fetchdat)) return 1;
        return op0F00_common(fetchdat, 0);
    }

    // ---- G2, D4 : les formes 32 bits de 0F 00 et 0F 01, et LOADALL386 ----

    // pcem: x86_ops_pmode.h:344-350
    private static int op0F00_a32(uint32_t fetchdat)
    {
        if (NOTRM()) return 1;
        if (fetch_ea_32(fetchdat)) return 1;
        return op0F00_common(fetchdat, 1);
    }

    // pcem: x86_ops_pmode.h:483-502 — les quatre formes 386 de op0F01_common.
    //
    // is286 NUL : SGDT et SIDT rendent la base telle quelle, sans forcer l'octet haut
    // à 0xFF. is32 garde les 32 bits de base chargés par LGDT/LIDT ; sans lui, 24.
    // L'écriture de SGDT/SIDT, elle, fait toujours quatre octets, quelle que soit la
    // taille d'opérande — `writememl` sans condition (x86_ops_pmode.h:364).
    private static int op0F01_w_a16(uint32_t fetchdat)
    {
        if (fetch_ea_16(fetchdat)) return 1;

        if (op0F01_common(fetchdat, 0, 0, 0)) return 1;
        return cpu_state.abrt;
    }

    private static int op0F01_w_a32(uint32_t fetchdat)
    {
        if (fetch_ea_32(fetchdat)) return 1;

        if (op0F01_common(fetchdat, 0, 0, 1)) return 1;
        return cpu_state.abrt;
    }

    private static int op0F01_l_a16(uint32_t fetchdat)
    {
        if (fetch_ea_16(fetchdat)) return 1;

        if (op0F01_common(fetchdat, 1, 0, 0)) return 1;
        return cpu_state.abrt;
    }

    private static int op0F01_l_a32(uint32_t fetchdat)
    {
        if (fetch_ea_32(fetchdat)) return 1;

        if (op0F01_common(fetchdat, 1, 0, 1)) return 1;
        return cpu_state.abrt;
    }

    // pcem: x86_ops_misc.h:885-894 — set_segment_limit.
    //
    // LE TEST D'EXPANSION VERS LE BAS N'EST VRAI QUE POUR UN SEGMENT DE DONNÉES dont le
    // bit E est posé : `(access & 0x18) == 0x10` exige S = 1 et un type donnée. Tout le
    // reste — code, système, données ordinaires — prend la branche [0, limit].
    private static void set_segment_limit(x86seg s, uint8_t segdat3)
    {
        if ((s.access & 0x18) != 0x10 || (s.access & (1 << 2)) == 0) /*expand-down*/
        {
                s.limit_high = s.limit;
                s.limit_low = 0;
        }
        else
        {
                s.limit_high = (segdat3 & 0x40) != 0 ? 0xffffffff : 0xffff;
                s.limit_low = s.limit + 1;
        }
    }

    // pcem: x86_ops_misc.h:896-928 — loadall_load_segment.
    //
    // UN DESCRIPTEUR DE DOUZE OCTETS, et non le format de la GDT : un mot double
    // d'attributs (accès en 15:8, segdat3 en 23:16), puis la base et la limite en clair,
    // déjà en octets. Rien n'est relu dans une table.
    //
    // use32 ET stack32 SONT REPOSÉS À CHAQUE APPEL, pas seulement pour CS et SS :
    // cpu_cur_status est recalculé des deux globales sur les dix segments. Le résultat
    // final est le même, le coût est de PCem.
    //
    // DEVIATION: `s == &cpu_state.seg_cs` devient ReferenceEquals — x86seg est une
    //   classe, la comparaison d'adresses se dit ainsi.
    private static void loadall_load_segment(uint32_t addr, x86seg s)
    {
        uint32_t attrib = readmeml(0, addr);
        uint32_t segdat3 = (attrib >> 16) & 0xff;
        s.access = (uint8_t)((attrib >> 8) & 0xff);
        s.@base = readmeml(0, addr + 4);
        s.limit = readmeml(0, addr + 8);

        if (ReferenceEquals(s, cpu_state.seg_cs))
                use32 = (segdat3 & 0x40) != 0 ? 0x300u : 0;
        if (ReferenceEquals(s, cpu_state.seg_ss))
                stack32 = (segdat3 & 0x40) != 0 ? 1 : 0;

        cpu_cur_status &= unchecked((uint16_t)~(CPU_STATUS_USE32 | CPU_STATUS_STACK32));
        if (use32 != 0)
                cpu_cur_status |= CPU_STATUS_USE32;
        if (stack32 != 0)
                cpu_cur_status |= CPU_STATUS_STACK32;

        set_segment_limit(s, (uint8_t)segdat3);

        if (ReferenceEquals(s, cpu_state.seg_ds))
        {
                if (s.@base == 0 && s.limit_low == 0 && s.limit_high == 0xffffffff)
                        cpu_cur_status &= unchecked((uint16_t)~CPU_STATUS_NOTFLATDS);
                else
                        cpu_cur_status |= CPU_STATUS_NOTFLATDS;
        }
        if (ReferenceEquals(s, cpu_state.seg_ss))
        {
                if (s.@base == 0 && s.limit_low == 0 && s.limit_high == 0xffffffff)
                        cpu_cur_status &= unchecked((uint16_t)~CPU_STATUS_NOTFLATSS);
                else
                        cpu_cur_status |= CPU_STATUS_NOTFLATSS;
        }
    }

    // pcem: x86_ops_misc.h:930-974 — opLOADALL386.
    //
    // LE LOADALL DU 386 N'EST PAS CELUI DU 286 : autre opcode (0F 07 et non 0F 05), et
    // le bloc n'est plus à l'adresse fixe 0x800 mais en ES:EDI — 0xCC octets. Il
    // recharge CR0, EFLAGS, EIP, les huit registres généraux, DR6 et DR7, les six
    // sélecteurs et, par loadall_load_segment, les dix caches de descripteur.
    //
    // AUCUNE GARDE DE PRIVILÈGE, là où opLOADALL teste `CPL && (cr0 & 1)`. Et CR0 est
    // écrit directement : ni flushmmucache, ni mise à jour de CPU_STATUS_PMODE — le
    // bit PE chargé ne se voit dans cpu_cur_status qu'au prochain chargement qui le
    // recalcule. Transcrit tel quel.
    //
    // PAS DE PREFETCH_RUN, seulement CLOCK_CYCLES(350).
    private static int opLOADALL386(uint32_t fetchdat)
    {
        uint32_t la_addr = es + EDI;

        cr0 = readmeml(0, la_addr);
        cpu_state.flags = readmemw(0, la_addr + 4);
        cpu_state.eflags = readmemw(0, la_addr + 6);
        flags_extract();
        cpu_state.pc = readmeml(0, la_addr + 8);
        EDI = readmeml(0, la_addr + 0xC);
        ESI = readmeml(0, la_addr + 0x10);
        EBP = readmeml(0, la_addr + 0x14);
        ESP = readmeml(0, la_addr + 0x18);
        EBX = readmeml(0, la_addr + 0x1C);
        EDX = readmeml(0, la_addr + 0x20);
        ECX = readmeml(0, la_addr + 0x24);
        EAX = readmeml(0, la_addr + 0x28);
        dr[6] = readmeml(0, la_addr + 0x2C);
        dr[7] = readmeml(0, la_addr + 0x30);
        tr.seg = readmemw(0, la_addr + 0x34);
        ldt.seg = readmemw(0, la_addr + 0x38);
        GS = readmemw(0, la_addr + 0x3C);
        FS = readmemw(0, la_addr + 0x40);
        DS = readmemw(0, la_addr + 0x44);
        SS = readmemw(0, la_addr + 0x48);
        CS = readmemw(0, la_addr + 0x4C);
        ES = readmemw(0, la_addr + 0x50);

        loadall_load_segment(la_addr + 0x54, tr);
        loadall_load_segment(la_addr + 0x60, idt);
        loadall_load_segment(la_addr + 0x6c, gdt);
        loadall_load_segment(la_addr + 0x78, ldt);
        loadall_load_segment(la_addr + 0x84, cpu_state.seg_gs);
        loadall_load_segment(la_addr + 0x90, cpu_state.seg_fs);
        loadall_load_segment(la_addr + 0x9c, cpu_state.seg_ds);
        loadall_load_segment(la_addr + 0xa8, cpu_state.seg_ss);
        loadall_load_segment(la_addr + 0xb4, cpu_state.seg_cs);
        loadall_load_segment(la_addr + 0xc0, cpu_state.seg_es);

        if (CPL == 3 && oldcpl != 3)
                Memory.mem.flushmmucache_cr3();
        oldcpl = CPL;

        CLOCK_CYCLES(350);
        return 0;
    }

    /// <summary>pcem: 386_ops.h — OP_TABLE(386_0f), emplacements 00 à 03 et 07 des
    /// quatre quadrants. 0F 00 n'a qu'une forme par taille d'adresse ; 0F 01, LAR et
    /// LSL en ont quatre. 0F 07 est LOADALL386 partout.</summary>
    private static void PoserGroupe_pmode_0f_386()
    {
        ops_386_0f[0x200] = op0F00_a32;
        ops_386_0f[0x300] = op0F00_a32;

        ops_386_0f[0x001] = op0F01_w_a16;
        ops_386_0f[0x101] = op0F01_l_a16;
        ops_386_0f[0x201] = op0F01_w_a32;
        ops_386_0f[0x301] = op0F01_l_a32;

        ops_386_0f[0x102] = opLAR_l_a16;
        ops_386_0f[0x202] = opLAR_w_a32;
        ops_386_0f[0x302] = opLAR_l_a32;

        ops_386_0f[0x103] = opLSL_l_a16;
        ops_386_0f[0x203] = opLSL_w_a32;
        ops_386_0f[0x303] = opLSL_l_a32;

        for (var q = 0; q < 4; q++)
                ops_386_0f[(q << 8) | 0x07] = opLOADALL386;
    }

    /// <summary>pcem: les six entrées non-ILLEGAL de ops_286_0f, relevées par
    /// gdb : 00, 01, 02, 03, 05, 06. Tout le reste de la table est ILLEGAL.</summary>
    private static void PoserTable0F()
    {
        for (var i = 0; i < 1024; i++)
                ops_286_0f[i] = ILLEGAL;

        ops_286_0f[0x00] = op0F00_a16;
        ops_286_0f[0x01] = op0F01_286;
        ops_286_0f[0x02] = opLAR_w_a16;
        ops_286_0f[0x03] = opLSL_w_a16;
        ops_286_0f[0x05] = opLOADALL;
        ops_286_0f[0x06] = opCLTS;
    }
}
