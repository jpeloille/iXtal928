// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/includes/private/cpu/x86_ops_pmode.h  (op0F01_common :352-485,
//         op0F01_286 :504-508, op0F00_a16 :337-342, opLAR :56-113, opLSL :116-172)
//         et pcem-dev/includes/private/cpu/x86_ops_misc.h  (opCLTS :796-806,
//         opLOADALL :827-881)
// SHA256: voir oracle.tsv ; vérifier avec tools/check-oracle.sh
// STATUS: partial — les six emplacements de ops_286_0f, complets pour ce qu'un
//         286 EN MODE RÉEL atteint. Les corps de op0F00_common, opLAR et opLSL
//         ne sont pas transcrits : leur première instruction est NOTRM, donc en
//         mode réel ils lèvent INT 6 et s'arrêtent là. Ils arriveront avec le
//         bloc C.
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
        //   garde il tombe dans le `default`. Sur un 286 c'est donc un opcode
        //   illegal, ce que la branche ci-dessous produit.

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


    // pcem: x86_ops_pmode.h:56-113 — opLAR(w_a16, fetch_ea_16, 0, 0), la seule forme que
    // ops_286_0f référence (relevé dans la .so, voir x86seg.cs). M20 : Windows 3.x en
    // mode standard valide ses sélecteurs avec. Vérifié par pm-check.
    // omitted: les formes w_a32, l_a16, l_a32 (x86_ops_pmode.h:113) — 386.
    //
    // LE VERDICT EST DANS ZF, ET LAR NE FAUTE PAS SUR UN MAUVAIS SÉLECTEUR : il efface ZF
    // et rend. Le type n'est refusé que pour 0, 8, A et D (réservés), et le privilège
    // n'est vérifié que hors code conforme.
    private static int opLAR_w_a16(uint32_t fetchdat)
    {
        int valid;
        uint16_t sel, desc = 0;

        if (NOTRM()) return 1;
        fetch_ea_16(fetchdat);
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
                cpu_state.regs[cpu_reg].w =
                        (uint16_t)(readmemw(0, (uint32_t)(((sel & 4) != 0 ? ldt.@base : gdt.@base) + (sel & ~7) + 4)) & 0xff00);
                cpl_override = 0;
        }
        CLOCK_CYCLES(11);
        PREFETCH_RUN(11, 2, (int)rmdat, 2, 0, 0, 0, 0);
        return cpu_state.abrt;
    }

    // pcem: x86_ops_pmode.h:116-172 — opLSL(w_a16, fetch_ea_16, 0, 0), même relevé.
    // omitted: les formes w_a32, l_a16, l_a32 (x86_ops_pmode.h:171-172) — 386.
    //
    // PLUS PERMISSIF QUE LAR SUR LES TYPES : il refuse toute porte (`(desc & 0x1400) ==
    // 0x400`) et les types 0 et A, mais accepte un TSS ou une LDT — dont la limite a un
    // sens. Et sa variable s'appelle `rpl` là où elle lit un DPL : le nom est de PCem.
    private static int opLSL_w_a16(uint32_t fetchdat)
    {
        int valid;
        uint16_t sel, desc = 0;

        if (NOTRM()) return 1;
        fetch_ea_16(fetchdat);
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
                cpu_state.regs[cpu_reg].w = readmemw(0, (uint32_t)(((sel & 4) != 0 ? ldt.@base : gdt.@base) + (sel & ~7)));
                cpl_override = 0;
        }
        CLOCK_CYCLES(10);
        PREFETCH_RUN(10, 2, (int)rmdat, 4, 0, 0, 0, 0);
        return cpu_state.abrt;
    }

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
    private static int op0F00_common(uint32_t fetchdat, int ea32)
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
        fetch_ea_16(fetchdat);
        return op0F00_common(fetchdat, 0);
    }

    // omitted: opLOADALL386 (x86_ops_misc.h:882-...) — c'est la forme 386, et
    //   la table donne opLOADALL a un 286. Verifie dans la .so.

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
