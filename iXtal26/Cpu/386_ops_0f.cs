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

    /// <summary>pcem: x86_ops_pmode.h:337-342 — op0F00_a16, et les macros opLAR
    /// (:56-113) et opLSL (:116-172).
    ///
    /// LES TROIS COMMENCENT PAR NOTRM, et c'est tout ce qu'ils font en mode
    /// réel : la macro lève INT 6 — opcode invalide — si `msw & 1` est nul.
    /// Leurs corps sont donc INATTEIGNABLES hors mode protégé, et ce n'est pas
    /// une omission par largeur mais par MODE. Ils échouent bruyamment après la
    /// garde, comme loadcsjmp et ses voisines dans x86seg.cs.</summary>
    private static OpFn NotrmPuisEchec(string nom) => fetchdat =>
    {
        if (NOTRM()) return 1;
        pc.fatal($"{nom} en mode protege : x86_ops_pmode.h n'est transcrit " +
                 "qu'en mode reel (bloc C du plan)\n");
        return 0;
    };

    // omitted: op0F00_common (x86_ops_pmode.h:176-336) — SLDT, STR, LLDT, LTR,
    //   VERR, VERW. Inatteignable hors mode protege ; bloc C.
    // omitted: opLOADALL386 (x86_ops_misc.h:882-...) — c'est la forme 386, et
    //   la table donne opLOADALL a un 286. Verifie dans la .so.

    /// <summary>pcem: les six entrées non-ILLEGAL de ops_286_0f, relevées par
    /// gdb : 00, 01, 02, 03, 05, 06. Tout le reste de la table est ILLEGAL.</summary>
    private static void PoserTable0F()
    {
        for (var i = 0; i < 1024; i++)
                ops_286_0f[i] = ILLEGAL;

        ops_286_0f[0x00] = NotrmPuisEchec("0F 00 (SLDT/STR/LLDT/LTR/VERR/VERW)");
        ops_286_0f[0x01] = op0F01_286;
        ops_286_0f[0x02] = NotrmPuisEchec("LAR");
        ops_286_0f[0x03] = NotrmPuisEchec("LSL");
        ops_286_0f[0x05] = opLOADALL;
        ops_286_0f[0x06] = opCLTS;
    }
}
