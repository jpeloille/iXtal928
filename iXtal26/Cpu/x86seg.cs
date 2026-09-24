// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/cpu/x86seg.c  (plages dans oracle.tsv)
// SHA256: voir oracle.tsv ; vérifier avec tools/check-oracle.sh
// STATUS: partial — les branches MODE RÉEL, plus LES FONDATIONS DU MODE PROTÉGÉ
//         depuis le bloc C étape 4 : les cinq leveurs d'exception, x86abort,
//         set_stack32, set_use32, do_seg_load, do_seg_v86_init, check_seg_valid,
//         PUSHW / PUSHL / POPW / POPL. Restent à écrire : loadseg et loadcsjmp en
//         mode protégé (étape 5), pmodeint et pmodeiret (6), pmoderetf et
//         loadcscall (7), taskswitch286 (8). Voir PLAN-286.md.
//
// x86seg.c est partagé entre le cœur 8088 et le cœur 386 : 808x.c l'appelle pour
// loadcs/loadseg, et sur un XT `msw & 1` vaut toujours 0, donc seules les
// branches `else` étaient atteintes au palier (a).
//
// TROIS FONCTIONS PRÉCÈDENT LEURS APPELANTS, et c'est voulu : do_seg_load,
// do_seg_v86_init et check_seg_valid n'ont encore personne qui les appelle — ce sera
// l'étape 5. Elles sont écrites d'abord parce que TOUT le reste du bloc C en dépend,
// et que l'alternative était un commit de deux mille lignes. Le compilateur ne s'en
// plaint pas (C# n'avertit pas sur une méthode privée non appelée), donc c'est écrit
// ici plutôt que laissé à deviner.
//
// Le nom de conteneur est x86seg_c : le fichier x86seg.c et le typedef x86seg se
// disputent le nom, et c'est le type qui le garde (cf. TRANSCRIPTION.md).

using static iXtal26.Cpu._386_common;
using static iXtal26.Cpu.x86;

namespace iXtal26.Cpu;

internal static partial class x86seg_c
{
    // pcem: x86seg.c:50-65
    private static void seg_reset(x86seg s)
    {
        s.access = (0 << 5) | 2;
        s.access2 = 0;
        s.limit = 0xFFFF;
        s.limit_low = 0;
        s.limit_high = 0xffff;
        if (s == cpu_state.seg_cs)
        {
                // TODO - When the PC is reset, initialization of the CS descriptor must be like the annotated line below.
                // s->base = AT ? (cpu_16bitbus ? 0xFF0000 : 0xFFFF0000) : 0xFFFF0;
                s.@base = AT != 0 ? 0xF0000u : 0xFFFF0u;
                s.seg = AT != 0 ? (uint16_t)0xF000 : (uint16_t)0xFFFF;
        }
        else
        {
                s.@base = 0;
                s.seg = 0;
        }
    }

    // pcem: x86seg.c:67-74
    internal static void x86seg_reset()
    {
        seg_reset(cpu_state.seg_cs);
        seg_reset(cpu_state.seg_ds);
        seg_reset(cpu_state.seg_es);
        seg_reset(cpu_state.seg_fs);
        seg_reset(cpu_state.seg_gs);
        seg_reset(cpu_state.seg_ss);
    }

    // pcem: x86seg.c:410-448 — branche `else` (mode réel) de loadseg().
    // omitted: toute la branche mode protégé (msw & 1), ~350 lignes.
    internal static int loadseg(uint16_t seg, x86seg s)
    {
        // pcem: x86seg.c:276-417 — LE MODE PROTEGE, et la ROM de l'AT y entre par
        // `MOV SS, 0x28` juste apres son JMP FAR. Cette branche etait un fatal() ;
        // c'est le premier morceau du bloc C qui devient un vrai chemin.
        if ((msw & 1) != 0 && (cpu_state.eflags & VM_FLAG) == 0)
        {
                // omitted: `#define DPL ((segdat[2] >> 13) & 3)` (x86seg.c:448) — une
                //   macro sur une variable LOCALE, definie APRES loadseg et donc hors de
                //   sa portee. Cette branche-ci ecrit `dpl` en local et le lit trois
                //   fois ; c'est loadcs et loadcsjmp qui utilisent la macro, et elles y
                //   ont leur propre fonction locale.
                var segdat = new uint16_t[4];

                // LE SELECTEUR NUL N'EST PAS UNE ERREUR, SAUF POUR SS. Charger 0 dans DS,
                // ES, FS ou GS est legal et documente : ca rend le segment inutilisable
                // sans lever quoi que ce soit, et la faute viendra au premier acces. Pour
                // SS c'est un #SS immediat, une pile sans segment n'ayant pas de sens.
                //
                // ET `base = -1` EST LA SENTINELLE, pas une adresse. 0xFFFFFFFF est ce que
                // SEG_CHECK_READ et SEG_CHECK_WRITE testent (386_common.cs) pour dire
                // « segment vide ». Le `(uint32_t)` est donc obligatoire et pas cosmetique.
                if ((seg & ~3) == 0)
                {
                        if (s == cpu_state.seg_ss)
                        {
                                // omitted: pclog("SS selector = NULL!") — sortie pure.
                                x86ss(null!, 0);
                                return 1;
                        }
                        s.seg = 0;
                        s.access = 0;
                        s.@base = unchecked((uint32_t)(-1));
                        if (s == cpu_state.seg_ds)
                                cpu_cur_status |= CPU_STATUS_NOTFLATDS;
                        return 0;
                }

                // LE BIT 2 CHOISIT LA TABLE, et la limite se compare AVANT d'ajouter la
                // base — un descripteur hors table est un #GP, pas une lecture sauvage.
                uint32_t addr = (uint32_t)(seg & ~7);
                if ((seg & 4) != 0)
                {
                        if (addr >= ldt.limit)
                        {
                                // omitted: pclog("Bigger than LDT limit...") — sortie pure.
                                x86gpf(null!, (uint16_t)(seg & ~3));
                                return 1;
                        }
                        addr += ldt.@base;
                }
                else
                {
                        if (addr >= gdt.limit)
                        {
                                // omitted: pclog("Bigger than GDT limit...") — sortie pure.
                                x86gpf(null!, (uint16_t)(seg & ~3));
                                return 1;
                        }
                        addr += gdt.@base;
                }

                // cpl_override = 1 AUTOUR DE LA LECTURE, ET C'EST INDISPENSABLE. Le
                // processeur lit la table des descripteurs avec ses propres privileges,
                // pas ceux du programme : sans ce drapeau, readmemw appliquerait la
                // verification de segment et un programme au CPL 3 ne pourrait pas
                // charger un selecteur.
                cpl_override = 1;
                segdat[0] = readmemw(0, addr);
                segdat[1] = readmemw(0, addr + 2);
                segdat[2] = readmemw(0, addr + 4);
                segdat[3] = readmemw(0, addr + 6);
                cpl_override = 0;
                if (cpu_state.abrt != 0)
                        return 1;
                int dpl = (segdat[2] >> 13) & 3;

                if (s == cpu_state.seg_ss)
                {
                        // LE TEST DU SELECTEUR NUL EST REFAIT, et c'est du code mort chez
                        // PCem : la garde du haut a deja rendu 1 pour SS dans ce cas. Porte
                        // tel quel — le corriger serait reecrire l'oracle, pas le
                        // transcrire.
                        if ((seg & ~3) == 0)
                        {
                                // omitted: pclog("Load SS null selector") — sortie pure.
                                x86gpf(null!, (uint16_t)(seg & ~3));
                                return 1;
                        }
                        // SS EXIGE L'EGALITE STRICTE DES TROIS PRIVILEGES, la ou un segment
                        // de donnees se contente d'inegalites : RPL == CPL == DPL. Une pile
                        // dont le privilege differe de celui du code qui l'utilise n'a pas
                        // de sens, et c'est ce qui rend le changement de pile des portes
                        // d'appel si contraint.
                        if ((seg & 3) != CPL || dpl != CPL)
                        {
                                // omitted: pclog("Invalid SS permiss") — sortie pure.
                                x86gpf(null!, (uint16_t)(seg & ~3));
                                return 1;
                        }
                        switch ((segdat[2] >> 8) & 0x1F)
                        {
                        case 0x12:
                        case 0x13:
                        case 0x16:
                        case 0x17: /*r/w*/
                                break;
                        default:
                                // omitted: pclog("Invalid SS type") — sortie pure.
                                x86gpf(null!, (uint16_t)(seg & ~3));
                                return 1;
                        }
                        // SS ABSENT DONNE #SS, PAS #NP, la ou tous les autres segments
                        // donnent #NP quelques lignes plus bas. La distinction est dans le
                        // silicium et le gestionnaire s'en sert.
                        if ((segdat[2] & 0x8000) == 0)
                        {
                                // omitted: pclog("Load SS not present!") — sortie pure.
                                x86ss(null!, (uint16_t)(seg & ~3));
                                return 1;
                        }
                        set_stack32((segdat[3] & 0x40) != 0 ? 1 : 0);
                }
                else if (s != cpu_state.seg_cs)
                {
                        // omitted: les deux `if (output) pclog(...)` (x86seg.c:361-364) —
                        //   sorties pures sous un drapeau de mise au point.
                        switch ((segdat[2] >> 8) & 0x1F)
                        {
                        case 0x10:
                        case 0x11:
                        case 0x12:
                        case 0x13: /*Data segments*/
                        case 0x14:
                        case 0x15:
                        case 0x16:
                        case 0x17:
                        case 0x1A:
                        case 0x1B: /*Readable non-conforming code*/
                                if ((seg & 3) > dpl || (CPL) > dpl)
                                {
                                        // omitted: pclog("Data seg fail...") — sortie pure.
                                        x86gpf(null!, (uint16_t)(seg & ~3));
                                        return 1;
                                }
                                break;
                        case 0x1E:
                        case 0x1F: /*Readable conforming code*/
                                break;
                        default:
                                // omitted: pclog("Invalid segment type...") — sortie pure.
                                x86gpf(null!, (uint16_t)(seg & ~3));
                                return 1;
                        }
                }

                if ((segdat[2] & 0x8000) == 0)
                {
                        x86np("Load data seg not present", (uint16_t)(seg & 0xfffc));
                        return 1;
                }
                s.seg = seg;
                do_seg_load(s, segdat);

                // LE BIT D'ACCES EST ECRIT DANS LA TABLE, ET POUR TOUS LES SEGMENTS.
                //
                // CS_ACCESSED et SEL_ACCESSED sont TOUS DEUX definis (x86seg.c:17, :21),
                // donc le `#ifndef CS_ACCESSED` qui enveloppait ce bloc d'un
                // `if (s != &_cs)` N'EST PAS compile : CS le recoit aussi. Verifie par grep,
                // pas suppose — c'est le genre de #ifdef qu'on lit a l'envers.
                //
                // ET C'EST UNE ECRITURE EN MEMOIRE QU'AUCUN COMPTEUR NE SURVEILLE : les
                // quatre compteurs memoire de h_state sont remplis mais jamais compares
                // (PLAN-286.md). Son seul filet est le hachage de RAM.
                cpl_override = 1;
                writememw(0, addr + 4, (uint16_t)(segdat[2] | 0x100)); /*Set accessed bit*/
                cpl_override = 0;

                s.@checked = 0;
                if (s == cpu_state.seg_ds)
                        codegen_flat_ds = 0;
                if (s == cpu_state.seg_ss)
                        codegen_flat_ss = 0;

                if (s == cpu_state.seg_ds)
                {
                        if (s.@base == 0 && s.limit_low == 0 && s.limit_high == 0xffffffff)
                                cpu_cur_status &= unchecked((uint16_t)~CPU_STATUS_NOTFLATDS);
                        else
                                cpu_cur_status |= CPU_STATUS_NOTFLATDS;
                }
                if (s == cpu_state.seg_ss)
                {
                        if (s.@base == 0 && s.limit_low == 0 && s.limit_high == 0xffffffff)
                                cpu_cur_status &= unchecked((uint16_t)~CPU_STATUS_NOTFLATSS);
                        else
                                cpu_cur_status |= CPU_STATUS_NOTFLATSS;
                }

                return cpu_state.abrt;
        }

        s.access = (3 << 5) | 2;
        s.access2 = 0;
        s.@base = (uint32_t)(seg << 4);
        s.seg = seg;
        s.@checked = 1;
        if (s == cpu_state.seg_ds)
                codegen_flat_ds = 0;
        if (s == cpu_state.seg_ss)
                codegen_flat_ss = 0;
        // omitted: `if (s == seg_ss && (eflags & VM_FLAG)) set_stack32(0);`
        //          VM_FLAG est inatteignable sur un XT (mode virtuel 8086 = 386+).

        if (s == cpu_state.seg_ds)
        {
                if (s.@base == 0 && s.limit_low == 0 && s.limit_high == 0xffffffff)
                        cpu_cur_status &= unchecked((uint16_t)~CPU_STATUS_NOTFLATDS);
                else
                        cpu_cur_status |= CPU_STATUS_NOTFLATDS;
        }
        if (s == cpu_state.seg_ss)
        {
                if (s.@base == 0 && s.limit_low == 0 && s.limit_high == 0xffffffff)
                        cpu_cur_status &= unchecked((uint16_t)~CPU_STATUS_NOTFLATSS);
                else
                        cpu_cur_status |= CPU_STATUS_NOTFLATSS;
        }

        return cpu_state.abrt;
    }

    // pcem: x86seg.c:452-566 — branche `else` (mode réel) de loadcs().
    // omitted: toute la branche mode protégé, ~90 lignes.
    internal static void loadcs(uint16_t seg)
    {
        // Meme defaut que loadseg ci-dessus, meme correction : le C garde son mode
        // protege derriere `if (msw & 1 && !(eflags & VM_FLAG))` (x86seg.c:457), et
        // le C# n'avait pas la garde.
        //
        // SES DEUX APPELANTS SONT PARTICULIERS, et c'est pour ca que le defaut n'a
        // jamais mordu : x86_doabrt (x86seg.c:110) et taskswitch286 (:2541, sous
        // `eflags & VM_FLAG`, donc jamais sur un 286). Mesure : `grep -n
        // '[^a-z_]loadcs(' x86seg.c` rend exactement ces deux sites.
        if ((msw & 1) != 0 && (cpu_state.eflags & VM_FLAG) == 0)
        {
                pc.fatal($"loadcs en mode protege (seg {seg:X4}) : x86seg.c n'est " +
                         "transcrit qu'en mode reel (bloc C du plan)\n");
                return;
        }

        cpu_state.seg_cs.@base = (uint32_t)(seg << 4);
        cpu_state.seg_cs.limit = 0xFFFF;
        cpu_state.seg_cs.limit_low = 0;
        cpu_state.seg_cs.limit_high = 0xffff;
        CS = seg;
        if ((cpu_state.eflags & VM_FLAG) != 0)
                cpu_state.seg_cs.access = (3 << 5) | 2;
        else
                cpu_state.seg_cs.access = (0 << 5) | 2;
        if (CPL == 3 && oldcpl != 3)
                Memory.mem.flushmmucache_cr3();
        oldcpl = CPL;
    }

    // LA QUEUE DE x86seg.c — 273 LIGNES VIVES QU'AUCUN 286 N'ATTEINT, et ce n'est
    // pas la fiche technique qui le dit mais les TABLES, lues dans la .so.
    //
    // omitted: sysenter (x86seg.c:2851-2881) et sysexit (:2883-2911) — 0F 34 et
    //   0F 35, Pentium II.
    // omitted: x86_smi_trigger (:2913), smi_write_descriptor_cache (:2915-2919),
    //   smi_load_descriptor_cache (:2920-2937), smi_load_smi_selector (:2939-2947),
    //   x86_smi_enter (:2987-3094), x86_smi_leave (:3096-3187) — le mode de gestion
    //   systeme. Ses appelants sont models/piix.c, vt82c586b.c, piix_pm.c et sio.c,
    //   dont AUCUN n'est lie a l'oracle. 386.cs:339 porte deja l'omission du test
    //   `if (smi_pending)` cote boucle.
    // omitted: cyrix_write_seg_descriptor (:2949-2954) et cyrix_load_seg_descriptor
    //   (:2956-2985) — les opcodes SMM propres a Cyrix (SVDC, RSDC, SVLDT...).
    // omitted: stimes, dtimes, btimes (:22-24) — trois compteurs de mise au point,
    //   lus seulement par un pclog commente (:89).
    // omitted: breaknullsegs (:30) — `#define breaknullsegs 0`, garde morte chez PCem.
    // omitted: le prototype taskswitch386 (:35) — ORPHELIN chez PCem lui-meme : sa
    //   seule autre occurrence de tout l'arbre est le commentaire :772, et il n'a
    //   AUCUNE definition.
    //
    // MESURE, et c'est la table qui tranche, pas le processeur : les pointeurs de
    // ops_286 et ops_286_0f lus dans les RELOCATIONS de libixtal26oracle.so, croises
    // avec la table de symboles complete (les handlers sont `static`, donc invisibles
    // a nm -D et a dladdr — c'est ce qui a fait echouer les deux premieres tentatives).
    //   ops_286    : 251 handlers distincts, AUCUN de SMM / sysenter / Cyrix
    //   ops_286_0f :   7 handlers distincts — op0F00_a16, op0F01_286, opLAR_w_a16,
    //                  opLSL_w_a16, opLOADALL, opCLTS, et ILLEGAL
    // Le second compte confirme a l'identique ce que faaf0fb avait lu au gdb.

    // LES TROIS AUTRES PORTES DU MODE PROTÉGÉ, déclarées ici et non transcrites.
    //
    // loadcscall (354 lignes vives), pmoderetf (248) et pmodeiret (300) sont
    // appelées par A6 — CALL far, RETF, IRET — mais UNIQUEMENT quand msw & 1.
    // En mode réel leurs appelants prennent l'autre branche, écrite sur place.
    //
    // Elles échouent BRUYAMMENT plutôt que de rendre du silence. C'est la même
    // décision qu'à A2.2b pour la table vide, et elle vaut pour la même raison :
    // un RETF qui rendrait sans rien faire laisserait le cœur avancer sur du
    // vide, et la divergence se manifesterait des milliers d'instructions plus
    // loin. Ici, le premier passage en mode protégé nomme ce qui manque.
    //
    // Leur transcription est le bloc `C` du plan (PLAN-286.md) : ~1 950 lignes
    // vives sur les 2 446 de x86seg.c.

    internal static void loadcscall(uint16_t seg, uint32_t old_pc)
        => pc.fatal($"loadcscall (seg {seg:X4}) : x86seg.c n'est transcrit qu'en " +
                    "mode reel (bloc C du plan)\n");

    // pcem: x86seg.c:1320-1624 — LE RETF DU MODE PROTEGE.
    //
    // ECRIT PARCE QUE LE BIOS DE L'AMI 286 EN A BESOIN, et c'est un vrai amorcage qui
    // l'a montre : `--boot --model ami286` tombait sur ce stub a la tranche 0, apres
    // 8 691 instructions. Le BIOS de l'IBM AT ne l'atteignait jamais. Deux BIOS
    // empruntent donc des chemins differents du mode protege, ce qui est tout l'argument
    // d'une seconde machine.
    //
    // `off` EST LE NOMBRE D'OCTETS A DEPILER EN PLUS — la forme `RETF imm16`. Et il est
    // ajoute DEUX FOIS dans le chemin « niveau exterieur » : une fois avant les tests,
    // une fois a la toute fin. Ce n'est pas une erreur de PCem mais la mecanique du
    // silicium : le premier ajout depile les parametres de l'ANCIENNE pile, le second
    // ceux de la NOUVELLE. Les omettre ou les confondre laisse une pile decalee.
    internal static void pmoderetf(int is32, uint16_t off)
    {
        uint32_t newpc;
        uint32_t newsp;
        uint32_t addr, oaddr;
        var segdat = new uint16_t[4];
        var segdat2 = new uint16_t[4];
        uint16_t seg, newss;
        uint32_t oldsp = ESP;
        int DPL() => (segdat[2] >> 13) & 3;
        int DPL2() => (segdat2[2] >> 13) & 3;

        if (is32 != 0)
        {
                newpc = POPL();
                seg = (uint16_t)POPL();
                if (cpu_state.abrt != 0)
                        return;
        }
        else
        {
                newpc = POPW();
                seg = POPW();
                if (cpu_state.abrt != 0)
                        return;
        }
        // UN RETF NE PEUT QUE DESCENDRE, comme un IRET : `(seg & 3) < CPL` est un #GP.
        if ((seg & 3) < CPL)
        {
                ESP = oldsp;
                x86gpf(null!, (uint16_t)(seg & ~3));
                return;
        }
        // NOTER L'ABSENCE DE `ESP = oldsp` ICI, la ou les quatorze autres sorties de la
        // fonction le font. C'est dans le C, et ca se recopie : un RETF vers un selecteur
        // nul laisse la pile depilee. Asymetrie de PCem, pas de la transcription.
        if ((seg & ~3) == 0)
        {
                x86gpf(null!, 0);
                return;
        }
        addr = (uint32_t)(seg & ~7);
        if ((seg & 4) != 0)
        {
                if (addr >= ldt.limit) { x86gpf(null!, (uint16_t)(seg & ~3)); return; }
                addr += ldt.@base;
        }
        else
        {
                if (addr >= gdt.limit) { x86gpf(null!, (uint16_t)(seg & ~3)); return; }
                addr += gdt.@base;
        }
        cpl_override = 1;
        segdat[0] = readmemw(0, addr);
        segdat[1] = readmemw(0, addr + 2);
        segdat[2] = readmemw(0, addr + 4);
        segdat[3] = readmemw(0, addr + 6);
        cpl_override = 0;
        if (cpu_state.abrt != 0) { ESP = oldsp; return; }
        oaddr = addr;

        // LE PREMIER DES DEUX AJOUTS DE `off`, et il a lieu AVANT les verifications de
        // type. Une faute apres cette ligne laisse donc la pile DEJA avancee de off — et
        // les `ESP = oldsp` qui suivent la remettent bien en arriere, celle-la comprise.
        if (stack32 != 0)
                ESP += off;
        else
                SP += (uint16_t)off;

        if (CPL == (seg & 3))
        {
                /*Retour au MEME niveau*/
                switch (segdat[2] & 0x1F00)
                {
                case 0x1800:
                case 0x1900:
                case 0x1A00:
                case 0x1B00: /*Non-conforming*/
                        // AU MEME NIVEAU c'est CPL qui est teste contre DPL ; au niveau
                        // EXTERIEUR c'est le RPL. La distinction est dans le C et elle est
                        // juste : au meme niveau le RPL EST le CPL.
                        if (CPL != DPL())
                        {
                                ESP = oldsp;
                                x86gpf(null!, (uint16_t)(seg & ~3));
                                return;
                        }
                        break;
                case 0x1C00:
                case 0x1D00:
                case 0x1E00:
                case 0x1F00: /*Conforming*/
                        if (CPL < DPL())
                        {
                                ESP = oldsp;
                                x86gpf(null!, (uint16_t)(seg & ~3));
                                return;
                        }
                        break;
                default:
                        // Et ici PAS de `ESP = oldsp`, alors que la branche exterieure en a
                        // un au meme endroit. Encore une asymetrie du C, portee telle quelle.
                        x86gpf(null!, (uint16_t)(seg & ~3));
                        return;
                }
                if ((segdat[2] & 0x8000) == 0)
                {
                        ESP = oldsp;
                        x86np("RETF CS not present\n", (uint16_t)(seg & 0xfffc));
                        return;
                }

                cpl_override = 1;
                writememw(0, addr + 4, (uint16_t)(segdat[2] | 0x100)); /*Set accessed bit*/
                cpl_override = 0;

                cpu_state.pc = newpc;
                /*Conforming segments don't change CPL, so CPL = RPL*/
                if ((segdat[2] & 0x400) != 0)
                        segdat[2] = (uint16_t)((segdat[2] & ~(3 << (5 + 8))) | ((seg & 3) << (5 + 8)));
                CS = seg;
                do_seg_load(cpu_state.seg_cs, segdat);
                cpu_state.seg_cs.access = (uint8_t)((cpu_state.seg_cs.access & ~(3 << 5)) | ((CS & 3) << 5));
                if (CPL == 3 && oldcpl != 3)
                        Memory.mem.flushmmucache_cr3();
                oldcpl = CPL;
                set_use32(segdat[3] & 0x40);

                cycles -= cpu_c.timing_retf_pm;
        }
        else
        {
                /*Retour a un niveau EXTERIEUR : SS et SP sont depiles en plus*/
                switch (segdat[2] & 0x1F00)
                {
                case 0x1800:
                case 0x1900:
                case 0x1A00:
                case 0x1B00: /*Non-conforming*/
                        if ((seg & 3) != DPL())
                        {
                                ESP = oldsp;
                                x86gpf(null!, (uint16_t)(seg & ~3));
                                return;
                        }
                        break;
                case 0x1C00:
                case 0x1D00:
                case 0x1E00:
                case 0x1F00: /*Conforming*/
                        if ((seg & 3) < DPL())
                        {
                                ESP = oldsp;
                                x86gpf(null!, (uint16_t)(seg & ~3));
                                return;
                        }
                        break;
                default:
                        ESP = oldsp;
                        x86gpf(null!, (uint16_t)(seg & ~3));
                        return;
                }
                if ((segdat[2] & 0x8000) == 0)
                {
                        ESP = oldsp;
                        x86np("RETF CS not present\n", (uint16_t)(seg & 0xfffc));
                        return;
                }
                if (is32 != 0)
                {
                        newsp = POPL();
                        newss = (uint16_t)POPL();
                        if (cpu_state.abrt != 0)
                                return;
                }
                else
                {
                        newsp = POPW();
                        newss = POPW();
                        if (cpu_state.abrt != 0)
                                return;
                }
                if ((newss & ~3) == 0)
                {
                        ESP = oldsp;
                        x86gpf(null!, (uint16_t)(newss & ~3));
                        return;
                }
                addr = (uint32_t)(newss & ~7);
                if ((newss & 4) != 0)
                {
                        if (addr >= ldt.limit) { ESP = oldsp; x86gpf(null!, (uint16_t)(newss & ~3)); return; }
                        addr += ldt.@base;
                }
                else
                {
                        if (addr >= gdt.limit) { ESP = oldsp; x86gpf(null!, (uint16_t)(newss & ~3)); return; }
                        addr += gdt.@base;
                }
                cpl_override = 1;
                segdat2[0] = readmemw(0, addr);
                segdat2[1] = readmemw(0, addr + 2);
                segdat2[2] = readmemw(0, addr + 4);
                segdat2[3] = readmemw(0, addr + 6);
                cpl_override = 0;
                if (cpu_state.abrt != 0) { ESP = oldsp; return; }

                // LES TROIS MEMES TESTS QUE pmodeiret, DANS UN ORDRE DIFFERENT. Ici le
                // type vient AVANT le DPL ; dans pmodeiret c'est l'inverse. Sans
                // consequence — les trois doivent passer — mais l'ordre se recopie.
                // Et comme dans pmodeiret, PCem a mis en commentaire
                // `((newss & 3) != DPL) || (DPL2 != DPL)` au profit de
                // `(newss & 3) != (seg & 3)` : le RPL de la pile contre celui du CODE.
                if ((newss & 3) != (seg & 3))
                {
                        ESP = oldsp;
                        x86gpf(null!, (uint16_t)(newss & ~3));
                        return;
                }
                if ((segdat2[2] & 0x1A00) != 0x1200)
                {
                        ESP = oldsp;
                        x86gpf(null!, (uint16_t)(newss & ~3));
                        return;
                }
                if ((segdat2[2] & 0x8000) == 0)
                {
                        ESP = oldsp;
                        x86np("RETF loading SS not present\n", (uint16_t)(newss & 0xfffc));
                        return;
                }
                if (DPL2() != (seg & 3))
                {
                        ESP = oldsp;
                        x86gpf(null!, (uint16_t)(newss & ~3));
                        return;
                }
                SS = newss;
                set_stack32((segdat2[3] & 0x40) != 0 ? 1 : 0);
                if (stack32 != 0)
                        ESP = newsp;
                else
                        SP = (uint16_t)newsp;
                do_seg_load(cpu_state.seg_ss, segdat2);

                // DEUX BITS D'ACCES, et le second par `oaddr` — l'adresse du descripteur
                // de CODE, sauvegardee avant que addr ne serve a la pile.
                cpl_override = 1;
                writememw(0, addr + 4, (uint16_t)(segdat2[2] | 0x100)); /*Set accessed bit*/
                writememw(0, oaddr + 4, (uint16_t)(segdat[2] | 0x100)); /*Set accessed bit*/
                cpl_override = 0;

                /*Conforming segments don't change CPL, so CPL = RPL*/
                if ((segdat[2] & 0x400) != 0)
                        segdat[2] = (uint16_t)((segdat[2] & ~(3 << (5 + 8))) | ((seg & 3) << (5 + 8)));

                cpu_state.pc = newpc;
                CS = seg;
                do_seg_load(cpu_state.seg_cs, segdat);
                // NOTER L'ABSENCE de la reecriture de seg_cs.access que la branche « meme
                // niveau » fait juste apres do_seg_load. Elle n'est pas ici, et c'est dans
                // le C : le masque sur segdat[2] juste au-dessus en tient lieu, mais
                // SEULEMENT pour un segment conforme. Asymetrie portee telle quelle.
                if (CPL == 3 && oldcpl != 3)
                        Memory.mem.flushmmucache_cr3();
                oldcpl = CPL;
                set_use32(segdat[3] & 0x40);

                // LE SECOND AJOUT DE `off` : les parametres de la NOUVELLE pile.
                if (stack32 != 0)
                        ESP += off;
                else
                        SP += (uint16_t)off;

                check_seg_valid(cpu_state.seg_ds);
                check_seg_valid(cpu_state.seg_es);
                check_seg_valid(cpu_state.seg_fs);
                check_seg_valid(cpu_state.seg_gs);
                cycles -= cpu_c.timing_retf_pm_outer;
        }
    }

    // pcem: x86seg.c:32 — intgatesize. SEUL pmodeint l'ecrit, et x86_doabrt le lit pour
    // decider s'il empile le code d'erreur sur 16 ou 32 bits. A zero il prendrait la
    // branche 32 bits, la MAUVAISE sur un 286 — c'est pour ca que les deux arrivent
    // dans le meme commit.
    internal static int intgatesize;

    // pcem: x86seg.c:1626-2007 — L'INTERRUPTION EN MODE PROTEGE.
    //
    // TROIS PORTES POSSIBLES dans l'IDT, et le type le dit : interruption (0x600,
    // 0xE00), piege (0x700, 0xF00), ou tache (0x500). Les deux premieres ne different
    // que par UN bit — le 0x100 — et ce bit decide si I_FLAG est efface : une porte
    // d'INTERRUPTION masque les interruptions en entrant, une porte de PIEGE non.
    //
    // ET LE CAS LE PLUS LONG EST UN CHANGEMENT DE PILE. Quand DPL2 < CPL, l'interruption
    // monte en privilege : elle doit prendre la pile du niveau cible, lue dans le TSS,
    // et empiler l'ANCIENNE paire SS:SP en plus de flags, CS et pc. C'est la moitie de
    // la fonction.
    internal static void pmodeint(int num, int soft)
    {
        var segdat = new uint16_t[4];
        var segdat2 = new uint16_t[4];
        var segdat3 = new uint16_t[4];
        uint32_t addr, oaddr;
        uint16_t newss;
        uint32_t oldss, oldsp;
        int type;
        uint32_t newsp;
        uint16_t seg = 0;
        int new_cpl;
        int DPL() => (segdat[2] >> 13) & 3;
        int DPL2() => (segdat2[2] >> 13) & 3;
        int DPL3() => (segdat3[2] >> 13) & 3;

        // UNE INTERRUPTION LOGICIELLE EN V86 EXIGE IOPL == 3. Branche morte sur un 286 ;
        // VM_FLAG y est inatteignable.
        if ((cpu_state.eflags & VM_FLAG) != 0 && IOPL != 3 && soft != 0)
        {
                // omitted: les deux pclog("V86 banned int") — sorties pures.
                x86gpf(null!, 0);
                return;
        }
        addr = (uint32_t)(num << 3);
        if (addr >= idt.limit)
        {
                // LA TRIPLE FAUTE EST UN RESET, ET C'EST LE SILICIUM. Si le vecteur 8
                // (#DF, double faute) est lui-meme hors de l'IDT, le processeur ne peut
                // plus rien signaler : il se reinitialise. C'est la SEULE facon dont un
                // 286 sort du mode protege sans passer par le 8042.
                if (num == 8)
                {
                        // omitted: pclog("Triple fault!") — sortie pure.
                        Cpu._808x.softresetx86();
                        // omitted: cpu_set_edx() — l'oracle l'interpose A VIDE
                        //   (__wrap_cpu_set_edx, harness_stubs.c:278), voir 808x.cs.
                }
                else if (num == 0xD)
                {
                        // omitted: pclog("Double fault!") — sortie pure.
                        pmodeint(8, 0);
                }
                else
                {
                        // pcem bug, reproduced: PB-32 — precedence d'operateurs. Le C ecrit
                        //   `(num * 8) + 2 + (soft) ? 0 : 1`, et `+` lie plus fort que `?:` :
                        //   la condition est `((num*8) + 2 + soft)`, toujours non nulle, donc
                        //   le code d'erreur vaut TOUJOURS 0. Le `(num*8)+2` voulu n'est
                        //   jamais transmis. Reproduit tel quel — PCem est l'oracle.
                        x86gpf(null!, (uint16_t)((((num * 8) + 2 + soft) != 0) ? 0 : 1));
                }
                return;
        }
        addr += idt.@base;

        // NOTER LES PREMIERS ARGUMENTS : 0, 2, 4, 6 et non 0, 0, 0, 0. readmemw additionne
        // ses deux arguments, donc c'est bien addr+2, addr+4, addr+6 — mais le C le note
        // AUTREMENT ici que partout ailleurs dans le fichier, et la forme se garde.
        cpl_override = 1;
        segdat[0] = readmemw(0, addr);
        segdat[1] = readmemw(2, addr);
        segdat[2] = readmemw(4, addr);
        segdat[3] = readmemw(6, addr);
        cpl_override = 0;
        if (cpu_state.abrt != 0)
                return;
        oaddr = addr;

        if ((segdat[2] & 0x1F00) == 0)
        {
                // x86gpf_expected ET PAS x86gpf : le commentaire de PCem le dit — ca se
                // declenche sur TOUTES les interruptions V86 d'EMM386, et le marquer
                // « attendu » evite au recompilateur d'invalider ses blocs.
                if ((cpu_state.eflags & VM_FLAG) != 0)
                        x86gpf_expected(null!, (uint16_t)((num * 8) + 2));
                else
                        x86gpf(null!, (uint16_t)((num * 8) + 2));
                return;
        }
        // `&& soft` EST LA DIFFERENCE ENTRE UN INT ET UNE FAUTE. Un INT n exige que la
        // porte soit au moins aussi privilegiee que l'appelant ; une exception MATERIELLE
        // n'a pas a le verifier, le processeur n'etant pas un programme.
        if (DPL() < CPL && soft != 0)
        {
                x86gpf(null!, (uint16_t)((num * 8) + 2));
                return;
        }
        type = segdat[2] & 0x1F00;
        switch (type)
        {
        case 0x600:
        case 0x700:
        case 0xE00:
        case 0xF00: /*Interrupt and trap gates*/
                intgatesize = (type >= 0x800) ? 32 : 16;
                if ((segdat[2] & 0x8000) == 0)
                {
                        x86np("Int gate not present\n", (uint16_t)((num << 3) | 2));
                        return;
                }
                seg = segdat[1];
                new_cpl = seg & 3;

                addr = (uint32_t)(seg & ~7);
                if ((seg & 4) != 0)
                {
                        if (addr >= ldt.limit) { x86gpf(null!, (uint16_t)(seg & ~3)); return; }
                        addr += ldt.@base;
                }
                else
                {
                        if (addr >= gdt.limit) { x86gpf(null!, (uint16_t)(seg & ~3)); return; }
                        addr += gdt.@base;
                }
                // omitted: le bloc commente x86seg.c:1730-1735 — un test `(seg&3) < CPL`
                //   que PCem a desactive ; le desactiver EST son comportement.
                cpl_override = 1;
                segdat2[0] = readmemw(0, addr);
                segdat2[1] = readmemw(0, addr + 2);
                segdat2[2] = readmemw(0, addr + 4);
                segdat2[3] = readmemw(0, addr + 6);
                cpl_override = 0;
                if (cpu_state.abrt != 0)
                        return;
                oaddr = addr;

                if (DPL2() > CPL) { x86gpf(null!, (uint16_t)(seg & ~3)); return; }

                switch (segdat2[2] & 0x1F00)
                {
                case 0x1800:
                case 0x1900:
                case 0x1A00:
                case 0x1B00: /*Non-conforming*/
                        if (DPL2() < CPL)
                        {
                                if ((segdat2[2] & 0x8000) == 0)
                                {
                                        x86np("Int gate CS not present\n", (uint16_t)(segdat[1] & 0xfffc));
                                        return;
                                }
                                if ((cpu_state.eflags & VM_FLAG) != 0 && DPL2() != 0)
                                {
                                        x86gpf(null!, (uint16_t)(segdat[1] & 0xFFFC));
                                        return;
                                }
                                /*Load new stack*/
                                // LA NOUVELLE PILE EST DANS LE TSS, ET SA DISPOSITION DEPEND
                                // DU TYPE DE TSS. `tr.access & 8` distingue un TSS de 386
                                // (entrees de huit octets, SP sur 32 bits) d'un TSS de 286
                                // (quatre octets, SP sur 16). Un 286 prend la seconde
                                // branche, mais les deux sont la.
                                oldss = SS;
                                oldsp = ESP;
                                cpl_override = 1;
                                if ((tr.access & 8) != 0)
                                {
                                        addr = (uint32_t)(4 + tr.@base + (DPL2() * 8));
                                        newss = readmemw(0, addr + 4);
                                        newsp = readmeml(0, addr);
                                }
                                else
                                {
                                        addr = (uint32_t)(2 + tr.@base + (DPL2() * 4));
                                        newss = readmemw(0, addr + 2);
                                        newsp = readmemw(0, addr);
                                }
                                cpl_override = 0;
                                if ((newss & ~3) == 0)
                                {
                                        x86ss(null!, (uint16_t)(newss & ~3));
                                        return;
                                }
                                addr = (uint32_t)(newss & ~7);
                                if ((newss & 4) != 0)
                                {
                                        if (addr >= ldt.limit) { x86ss(null!, (uint16_t)(newss & ~3)); return; }
                                        addr += ldt.@base;
                                }
                                else
                                {
                                        if (addr >= gdt.limit) { x86ss(null!, (uint16_t)(newss & ~3)); return; }
                                        addr += gdt.@base;
                                }
                                cpl_override = 1;
                                segdat3[0] = readmemw(0, addr);
                                segdat3[1] = readmemw(0, addr + 2);
                                segdat3[2] = readmemw(0, addr + 4);
                                segdat3[3] = readmemw(0, addr + 6);
                                cpl_override = 0;
                                if (cpu_state.abrt != 0)
                                        return;
                                if (((newss & 3) != DPL2()) || (DPL3() != DPL2()))
                                {
                                        x86ss(null!, (uint16_t)(newss & ~3));
                                        return;
                                }
                                if ((segdat3[2] & 0x1A00) != 0x1200)
                                {
                                        x86ss(null!, (uint16_t)(newss & ~3));
                                        return;
                                }
                                if ((segdat3[2] & 0x8000) == 0)
                                {
                                        x86np("Int gate loading SS not present\n", (uint16_t)(newss & 0xfffc));
                                        return;
                                }
                                SS = newss;
                                set_stack32((segdat3[3] & 0x40) != 0 ? 1 : 0);
                                if (stack32 != 0)
                                        ESP = newsp;
                                else
                                        SP = (uint16_t)newsp;
                                do_seg_load(cpu_state.seg_ss, segdat3);

                                cpl_override = 1;
                                writememw(0, addr + 4, (uint16_t)(segdat3[2] | 0x100)); /*Set accessed bit*/
                                cpl_override = 0;

                                // cpl_override = 1 AUTOUR DES EMPILEMENTS, et ce n'est pas la
                                // meme raison qu'autour des lectures de descripteur : ici la
                                // pile appartient au niveau CIBLE, plus privilegie que celui
                                // qu'on quitte. Sans le drapeau, le PUSH serait refuse.
                                cpl_override = 1;
                                if (type >= 0x800)
                                {
                                        if ((cpu_state.eflags & VM_FLAG) != 0)
                                        {
                                                PUSHL(GS);
                                                PUSHL(FS);
                                                PUSHL(DS);
                                                PUSHL(ES);
                                                if (cpu_state.abrt != 0)
                                                        return;
                                                loadseg(0, cpu_state.seg_ds);
                                                loadseg(0, cpu_state.seg_es);
                                                loadseg(0, cpu_state.seg_fs);
                                                loadseg(0, cpu_state.seg_gs);
                                        }
                                        PUSHL(oldss);
                                        PUSHL(oldsp);
                                        PUSHL((uint32_t)(cpu_state.flags | (cpu_state.eflags << 16)));
                                        PUSHL(CS);
                                        PUSHL(cpu_state.pc);
                                        if (cpu_state.abrt != 0)
                                                return;
                                }
                                else
                                {
                                        PUSHW((uint16_t)oldss);
                                        PUSHW((uint16_t)oldsp);
                                        PUSHW(cpu_state.flags);
                                        PUSHW(CS);
                                        PUSHW((uint16_t)cpu_state.pc);
                                        if (cpu_state.abrt != 0)
                                                return;
                                }
                                cpl_override = 0;
                                cpu_state.seg_cs.access = 0;
                                cycles -= cpu_c.timing_int_pm_outer - cpu_c.timing_int_pm;
                                break;
                        }
                        else if (DPL2() != CPL)
                        {
                                x86gpf(null!, (uint16_t)(seg & ~3));
                                return;
                        }
                        // LA CHUTE EST VOULUE : si DPL2 == CPL, un code non conforme se
                        // traite exactement comme un conforme — pas de changement de pile.
                        // C# interdit la chute implicite, d'ou goto case.
                        goto case 0x1C00;
                case 0x1C00:
                case 0x1D00:
                case 0x1E00:
                case 0x1F00: /*Conforming*/
                        if ((segdat2[2] & 0x8000) == 0)
                        {
                                x86np("Int gate CS not present\n", (uint16_t)(segdat[1] & 0xfffc));
                                return;
                        }
                        if ((cpu_state.eflags & VM_FLAG) != 0 && DPL2() < CPL)
                        {
                                x86gpf(null!, (uint16_t)(seg & ~3));
                                return;
                        }
                        // `type > 0x800` ICI, MAIS `type >= 0x800` POUR intgatesize ET POUR
                        // le bloc ci-dessus. La difference porte sur le type 0x800 exactement,
                        // qui n'est pas une porte valide — donc inerte, et conservee telle
                        // quelle parce que c'est ce que le C ecrit.
                        if (type > 0x800)
                        {
                                PUSHL((uint32_t)(cpu_state.flags | (cpu_state.eflags << 16)));
                                PUSHL(CS);
                                PUSHL(cpu_state.pc);
                                if (cpu_state.abrt != 0)
                                        return;
                        }
                        else
                        {
                                PUSHW(cpu_state.flags);
                                PUSHW(CS);
                                PUSHW((uint16_t)cpu_state.pc);
                                if (cpu_state.abrt != 0)
                                        return;
                        }
                        new_cpl = CS & 3;
                        break;
                default:
                        x86gpf(null!, (uint16_t)(seg & ~3));
                        return;
                }
                do_seg_load(cpu_state.seg_cs, segdat2);
                CS = (uint16_t)((seg & ~3) | new_cpl);
                cpu_state.seg_cs.access = (uint8_t)((cpu_state.seg_cs.access & ~(3 << 5)) | (new_cpl << 5));
                if (CPL == 3 && oldcpl != 3)
                        Memory.mem.flushmmucache_cr3();
                oldcpl = CPL;
                // LE POINT D'ENTREE VIENT DE segdat, PAS DE segdat2 : l'offset est dans la
                // PORTE, le segment dans le descripteur de code. Les confondre donnerait un
                // saut a l'adresse du descripteur.
                if (type > 0x800)
                        cpu_state.pc = (uint32_t)(segdat[0] | (segdat[3] << 16));
                else
                        cpu_state.pc = segdat[0];
                set_use32(segdat2[3] & 0x40);

                cpl_override = 1;
                writememw(0, oaddr + 4, (uint16_t)(segdat2[2] | 0x100)); /*Set accessed bit*/
                cpl_override = 0;

                cpu_state.eflags &= unchecked((uint16_t)~VM_FLAG);
                cpu_cur_status &= unchecked((uint16_t)~CPU_STATUS_V86);
                // LE BIT 0x100 SEPARE LA PORTE D'INTERRUPTION DE LA PORTE DE PIEGE, et
                // c'est tout ce qui les distingue : absent, I_FLAG est efface.
                if ((type & 0x100) == 0)
                        cpu_state.flags &= unchecked((uint16_t)~I_FLAG);
                cpu_state.flags &= unchecked((uint16_t)~(T_FLAG | NT_FLAG));
                cycles -= cpu_c.timing_int_pm;
                break;

        case 0x500: /*Task gate*/
                seg = segdat[1];
                addr = (uint32_t)(seg & ~7);
                if ((seg & 4) != 0)
                {
                        if (addr >= ldt.limit) { x86gpf(null!, (uint16_t)(seg & ~3)); return; }
                        addr += ldt.@base;
                }
                else
                {
                        if (addr >= gdt.limit) { x86gpf(null!, (uint16_t)(seg & ~3)); return; }
                        addr += gdt.@base;
                }
                cpl_override = 1;
                segdat2[0] = readmemw(0, addr);
                segdat2[1] = readmemw(0, addr + 2);
                segdat2[2] = readmemw(0, addr + 4);
                segdat2[3] = readmemw(0, addr + 6);
                cpl_override = 0;
                if (cpu_state.abrt != 0)
                        return;
                if ((segdat2[2] & 0x8000) == 0)
                {
                        x86np("Int task gate not present\n", (uint16_t)(segdat[1] & 0xfffc));
                        return;
                }
                optype = OPTYPE_INT;
                cpl_override = 1;
                taskswitch286(seg, segdat2, segdat2[2] & 0x800);
                cpl_override = 0;
                break;

        default:
                x86gpf(null!, (uint16_t)(seg & ~3));
                return;
        }
    }

    // pcem: x86seg.c:2009-2391 — L'IRET DU MODE PROTEGE, et c'est par lui que le POST
    // de l'AT ressort apres avoir dimensionne la memoire haute.
    //
    // TROIS SORTIES POSSIBLES, et l'ordre des tests les separe : un changement de tache
    // si NT est pose, un retour au MEME niveau, ou un retour a un niveau MOINS
    // privilegie qui depile en plus SS et SP.
    //
    // `ESP = oldsp` PARTOUT OU IL ECHOUE, et c'est la moitie du travail de cette
    // fonction. Un IRET depile trois ou cinq mots ; si la verification echoue au
    // milieu, la pile doit revenir a son etat d'avant, sans quoi le gestionnaire
    // d'exception rejouerait l'instruction sur une pile a demi consommee. PCem le
    // repete a chacun de ses quinze points de sortie.
    internal static void pmodeiret(int is32)
    {
        uint32_t newsp;
        uint16_t newss;
        uint32_t tempflags, flagmask;
        uint32_t newpc;
        var segdat = new uint16_t[4];
        var segdat2 = new uint16_t[4];
        var segs = new uint16_t[4];
        uint16_t seg = 0;
        uint32_t addr, oaddr;
        uint32_t oldsp = ESP;
        int DPL() => (segdat[2] >> 13) & 3;
        int DPL2() => (segdat2[2] >> 13) & 3;

        // LA BRANCHE V86 EST MORTE SUR UN 286, gardee par is386 qui vaut 0 pour les
        // trois machines du depot. Transcrite quand meme : is386 est une VARIABLE et
        // non une constante de compilation, et la fidelite de structure est la seule
        // defense d'un bloc que nul oracle independant ne verifie.
        if (is386 != 0 && (cpu_state.eflags & VM_FLAG) != 0)
        {
                if (IOPL != 3)
                {
                        // omitted: pclog("V86 IRET! IOPL!=3") — sortie pure.
                        x86gpf(null!, 0);
                        return;
                }
                if (is32 != 0)
                {
                        newpc = POPL();
                        seg = (uint16_t)POPL();
                        tempflags = POPL();
                        if (cpu_state.abrt != 0)
                                return;
                }
                else
                {
                        newpc = POPW();
                        seg = POPW();
                        tempflags = POPW();
                        if (cpu_state.abrt != 0)
                                return;
                }
                cpu_state.pc = newpc;
                cpu_state.seg_cs.@base = (uint32_t)(seg << 4);
                cpu_state.seg_cs.limit = 0xFFFF;
                cpu_state.seg_cs.limit_low = 0;
                cpu_state.seg_cs.limit_high = 0xffff;
                CS = seg;
                cpu_state.flags = (uint16_t)((uint32_t)(cpu_state.flags & 0x3000) | (tempflags & 0xCFD5) | 2u);
                cycles -= cpu_c.timing_iret_rm;
                return;
        }

        // NT POSE VEUT DIRE « CETTE TACHE A ETE APPELEE PAR UNE AUTRE », et l'IRET est
        // alors un RETOUR DE TACHE, pas un retour d'interruption : le selecteur de la
        // tache precedente est dans les deux premiers octets du TSS courant. C'est
        // pourquoi taskswitch286 est un appelant de pmodeiret et non l'inverse.
        //
        // NOTER L'ORDRE DES ARGUMENTS : `readmemw(tr.base, 0)`, la base en premier et
        // l'offset a zero. Le reste de la fonction fait l'inverse, `readmemw(0, addr)`.
        // Les deux sont corrects — readmemw additionne ses deux arguments — mais
        // l'asymetrie est dans le C et se recopie telle quelle.
        if ((cpu_state.flags & NT_FLAG) != 0)
        {
                seg = readmemw(tr.@base, 0);
                addr = (uint32_t)(seg & ~7);
                if ((seg & 4) != 0)
                {
                        // omitted: pclog("TS LDT ... IRET") — sortie pure.
                        x86ts(null!, (uint16_t)(seg & ~3));
                        return;
                }
                else
                {
                        if (addr >= gdt.limit)
                        {
                                x86ts(null!, (uint16_t)(seg & ~3));
                                return;
                        }
                        addr += gdt.@base;
                }
                cpl_override = 1;
                segdat[0] = readmemw(0, addr);
                segdat[1] = readmemw(0, addr + 2);
                segdat[2] = readmemw(0, addr + 4);
                segdat[3] = readmemw(0, addr + 6);
                taskswitch286(seg, segdat, segdat[2] & 0x800);
                cpl_override = 0;
                return;
        }

        // LE MASQUE DE DRAPEAUX EST UN MECANISME DE PRIVILEGE. Un programme moins
        // privilegie ne doit pas pouvoir se donner IOPL ni toucher NT : les deux bits
        // sont retires du masque selon CPL, donc l'IRET les laisse tels quels au lieu
        // de les charger depuis la pile. C'est le seul endroit de x86seg.c ou un
        // drapeau depile est IGNORE plutot que refuse.
        flagmask = 0xFFFF;
        if (CPL != 0)
                flagmask &= unchecked((uint32_t)~0x3000);
        if (IOPL < CPL)
                flagmask &= unchecked((uint32_t)~0x200);

        if (is32 != 0)
        {
                newpc = POPL();
                seg = (uint16_t)POPL();
                tempflags = POPL();
                if (cpu_state.abrt != 0) { ESP = oldsp; return; }
                // Seconde branche morte sur un 286 : IRETD vers le mode V86, sous is386.
                if (is386 != 0 && ((tempflags >> 16) & VM_FLAG) != 0)
                {
                        newsp = POPL();
                        newss = (uint16_t)POPL();
                        segs[0] = (uint16_t)POPL();
                        segs[1] = (uint16_t)POPL();
                        segs[2] = (uint16_t)POPL();
                        segs[3] = (uint16_t)POPL();
                        if (cpu_state.abrt != 0) { ESP = oldsp; return; }
                        cpu_state.eflags = (uint16_t)(tempflags >> 16);
                        cpu_cur_status |= CPU_STATUS_V86;
                        loadseg(segs[0], cpu_state.seg_es);
                        do_seg_v86_init(cpu_state.seg_es);
                        loadseg(segs[1], cpu_state.seg_ds);
                        do_seg_v86_init(cpu_state.seg_ds);
                        cpu_cur_status |= CPU_STATUS_NOTFLATDS;
                        loadseg(segs[2], cpu_state.seg_fs);
                        do_seg_v86_init(cpu_state.seg_fs);
                        loadseg(segs[3], cpu_state.seg_gs);
                        do_seg_v86_init(cpu_state.seg_gs);

                        cpu_state.pc = newpc & 0xffff;
                        cpu_state.seg_cs.@base = (uint32_t)(seg << 4);
                        cpu_state.seg_cs.limit = 0xFFFF;
                        cpu_state.seg_cs.limit_low = 0;
                        cpu_state.seg_cs.limit_high = 0xffff;
                        CS = seg;
                        cpu_state.seg_cs.access = (3 << 5) | 2;
                        if (CPL == 3 && oldcpl != 3)
                                Memory.mem.flushmmucache_cr3();
                        oldcpl = CPL;

                        ESP = newsp;
                        loadseg(newss, cpu_state.seg_ss);
                        do_seg_v86_init(cpu_state.seg_ss);
                        cpu_cur_status |= CPU_STATUS_NOTFLATSS;
                        use32 = 0;
                        cpu_cur_status &= unchecked((uint16_t)~CPU_STATUS_USE32);
                        cpu_state.flags = (uint16_t)((tempflags & 0xFFD5) | 2);
                        cycles -= cpu_c.timing_iret_v86;
                        return;
                }
        }
        else
        {
                newpc = POPW();
                seg = POPW();
                tempflags = POPW();
                if (cpu_state.abrt != 0) { ESP = oldsp; return; }
        }

        if ((seg & ~3) == 0)
        {
                // omitted: pclog("IRET CS=0") — sortie pure.
                ESP = oldsp;
                x86gpf(null!, 0);
                return;
        }

        addr = (uint32_t)(seg & ~7);
        if ((seg & 4) != 0)
        {
                if (addr >= ldt.limit) { ESP = oldsp; x86gpf(null!, (uint16_t)(seg & ~3)); return; }
                addr += ldt.@base;
        }
        else
        {
                if (addr >= gdt.limit) { ESP = oldsp; x86gpf(null!, (uint16_t)(seg & ~3)); return; }
                addr += gdt.@base;
        }
        // UN IRET NE PEUT QUE DESCENDRE EN PRIVILEGE, JAMAIS MONTER. `(seg & 3) < CPL`
        // est un #GP : c'est ce test, et lui seul, qui empeche un programme au CPL 3 de
        // revenir au CPL 0 en forgeant une pile.
        if ((seg & 3) < CPL)
        {
                ESP = oldsp;
                x86gpf(null!, (uint16_t)(seg & ~3));
                return;
        }
        cpl_override = 1;
        segdat[0] = readmemw(0, addr);
        segdat[1] = readmemw(0, addr + 2);
        segdat[2] = readmemw(0, addr + 4);
        segdat[3] = readmemw(0, addr + 6);
        cpl_override = 0;
        if (cpu_state.abrt != 0) { ESP = oldsp; return; }

        switch (segdat[2] & 0x1F00)
        {
        case 0x1800:
        case 0x1900:
        case 0x1A00:
        case 0x1B00: /*Non-conforming code*/
                // NON CONFORME EXIGE L'EGALITE, conforme se contente de `>=`. La
                // difference tient au fait qu'un segment conforme s'execute au privilege
                // de l'appelant : y revenir avec un RPL plus grand est legal.
                if ((seg & 3) != DPL())
                {
                        ESP = oldsp;
                        x86gpf(null!, (uint16_t)(seg & ~3));
                        return;
                }
                break;
        case 0x1C00:
        case 0x1D00:
        case 0x1E00:
        case 0x1F00: /*Conforming code*/
                if ((seg & 3) < DPL())
                {
                        ESP = oldsp;
                        x86gpf(null!, (uint16_t)(seg & ~3));
                        return;
                }
                break;
        default:
                // omitted: pclog("IRET CS != code seg") — sortie pure.
                ESP = oldsp;
                x86gpf(null!, (uint16_t)(seg & ~3));
                return;
        }
        if ((segdat[2] & 0x8000) == 0)
        {
                ESP = oldsp;
                x86np("IRET CS not present\n", (uint16_t)(seg & 0xfffc));
                return;
        }

        if ((seg & 3) == CPL)
        {
                /*Même niveau*/
                CS = seg;
                do_seg_load(cpu_state.seg_cs, segdat);
                cpu_state.seg_cs.access = (uint8_t)((cpu_state.seg_cs.access & ~(3 << 5)) | ((CS & 3) << 5));
                if (CPL == 3 && oldcpl != 3)
                        Memory.mem.flushmmucache_cr3();
                oldcpl = CPL;
                set_use32(segdat[3] & 0x40);

                cpl_override = 1;
                writememw(0, addr + 4, (uint16_t)(segdat[2] | 0x100)); /*Set accessed bit*/
                cpl_override = 0;
                cycles -= cpu_c.timing_iret_pm;
        }
        else /*Return to outer level*/
        {
                oaddr = addr;
                if (is32 != 0)
                {
                        newsp = POPL();
                        newss = (uint16_t)POPL();
                        if (cpu_state.abrt != 0) { ESP = oldsp; return; }
                }
                else
                {
                        newsp = POPW();
                        newss = POPW();
                        if (cpu_state.abrt != 0) { ESP = oldsp; return; }
                }

                if ((newss & ~3) == 0)
                {
                        ESP = oldsp;
                        x86gpf(null!, (uint16_t)(newss & ~3));
                        return;
                }
                addr = (uint32_t)(newss & ~7);
                if ((newss & 4) != 0)
                {
                        if (addr >= ldt.limit) { ESP = oldsp; x86gpf(null!, (uint16_t)(newss & ~3)); return; }
                        addr += ldt.@base;
                }
                else
                {
                        if (addr >= gdt.limit) { ESP = oldsp; x86gpf(null!, (uint16_t)(newss & ~3)); return; }
                        addr += gdt.@base;
                }
                cpl_override = 1;
                segdat2[0] = readmemw(0, addr);
                segdat2[1] = readmemw(0, addr + 2);
                segdat2[2] = readmemw(0, addr + 4);
                segdat2[3] = readmemw(0, addr + 6);
                cpl_override = 0;
                if (cpu_state.abrt != 0) { ESP = oldsp; return; }

                // TROIS TESTS SUR LE NOUVEAU SS, ET LE PREMIER N'EST PAS CELUI QU'ON
                // CROIT. PCem a mis en commentaire `((newss & 3) != DPL) || (DPL2 != DPL)`
                // et le remplace par `(newss & 3) != (seg & 3)` : le RPL de la pile doit
                // egaler celui du CODE vers lequel on retourne, pas le DPL du descripteur
                // de code. Les deux se confondent presque toujours, et « presque » est la
                // raison pour laquelle l'ancienne ligne est encore visible dans le C.
                if ((newss & 3) != (seg & 3))
                {
                        ESP = oldsp;
                        x86gpf(null!, (uint16_t)(newss & ~3));
                        return;
                }
                if ((segdat2[2] & 0x1A00) != 0x1200)
                {
                        ESP = oldsp;
                        x86gpf(null!, (uint16_t)(newss & ~3));
                        return;
                }
                if (DPL2() != (seg & 3))
                {
                        ESP = oldsp;
                        x86gpf(null!, (uint16_t)(newss & ~3));
                        return;
                }
                if ((segdat2[2] & 0x8000) == 0)
                {
                        ESP = oldsp;
                        x86np("IRET loading SS not present\n", (uint16_t)(newss & 0xfffc));
                        return;
                }
                SS = newss;
                set_stack32((segdat2[3] & 0x40) != 0 ? 1 : 0);
                if (stack32 != 0)
                        ESP = newsp;
                else
                        SP = (uint16_t)newsp;
                do_seg_load(cpu_state.seg_ss, segdat2);

                // DEUX BITS D'ACCES ECRITS, celui de SS et celui de CS, et le second
                // utilise `oaddr` — l'adresse du descripteur de CODE, sauvegardee avant
                // que addr ne soit reaffecte au descripteur de pile. Perdre oaddr
                // marquerait le mauvais descripteur.
                cpl_override = 1;
                writememw(0, addr + 4, (uint16_t)(segdat2[2] | 0x100)); /*Set accessed bit*/
                writememw(0, oaddr + 4, (uint16_t)(segdat[2] | 0x100)); /*Set accessed bit*/
                cpl_override = 0;

                /*Conforming segments don't change CPL, so CPL = RPL*/
                if ((segdat[2] & 0x400) != 0)
                        segdat[2] = (uint16_t)((segdat[2] & ~(3 << (5 + 8))) | ((seg & 3) << (5 + 8)));

                CS = seg;
                do_seg_load(cpu_state.seg_cs, segdat);
                cpu_state.seg_cs.access = (uint8_t)((cpu_state.seg_cs.access & ~(3 << 5)) | ((CS & 3) << 5));
                if (CPL == 3 && oldcpl != 3)
                        Memory.mem.flushmmucache_cr3();
                oldcpl = CPL;
                set_use32(segdat[3] & 0x40);

                // ET LES QUATRE AUTRES SEGMENTS SONT REVALIDES, parce que CPL vient de
                // MONTER : un selecteur charge au CPL 0 peut etre illegal au CPL 3.
                // check_seg_valid les vide plutot que de lever — voir sa transcription.
                check_seg_valid(cpu_state.seg_ds);
                check_seg_valid(cpu_state.seg_es);
                check_seg_valid(cpu_state.seg_fs);
                check_seg_valid(cpu_state.seg_gs);
                cycles -= cpu_c.timing_iret_pm_outer;
        }
        cpu_state.pc = newpc;
        cpu_state.flags = (uint16_t)((cpu_state.flags & ~flagmask) | (tempflags & flagmask & 0xFFD5) | 2);
        if (is32 != 0)
                cpu_state.eflags = (uint16_t)(tempflags >> 16);
    }

    /// <summary>pcem: x86seg.c:2393-2748 — LE CHANGEMENT DE TACHE, etape 8 du bloc C.
    ///
    /// Dernier de la sequence, et pour une raison mesuree : ses seuls appelants sont les
    /// portes de TACHE — type 0x100 et 0x900 dans loadcsjmp et loadcscall, et le bit
    /// correspondant de l'IDT dans pmodeint. Le POST de l'IBM AT n'en emprunte aucune :
    /// pas de descripteur de type 9 dans sa GDT, pas de porte de type 5 dans son IDT.
    /// Echoue bruyamment en attendant, meme doctrine que ses voisines.</summary>
    internal static void taskswitch286(uint16_t seg, uint16_t[] segdat, int is32)
        => pc.fatal($"taskswitch286 (seg {seg:X4}, is32 {is32}) : x86seg.c n'est pas " +
                    "encore transcrit jusque-la (bloc C etape 8)\n");

    // pcem: x86seg.c — loadcsjmp, la BRANCHE MODE RÉEL seulement (les 19 dernières
    // lignes des 236 de la fonction).
    //
    // CE COMMENTAIRE DISAIT « n'est pas transcrite. Elle échoue BRUYAMMENT » ET
    // C'ÉTAIT PÉRIMÉ depuis dcb8960 : la branche mode protégé est juste en dessous,
    // avec son descripteur lu dans la GDT ou la LDT, ses tests CPL/DPL, son segment
    // conforme, sa porte d'appel et sa porte de tâche. Seule celle-ci appelle encore
    // un stub, taskswitch286, et elle le fait en le nommant.
    //
    // Relevé par une reconnaissance en lecture seule, pas par une porte : aucun test
    // ne vérifie qu'un commentaire dit vrai. C'est la quatrième ligne de statut de
    // cette session à se révéler fausse à la première mesure.
    //
    // La branche mode réel, elle, est exactement loadcs ci-dessus plus une
    // facturation de cycles. C'est elle que le vecteur de reset emprunte.
    internal static void loadcsjmp(uint16_t seg, uint32_t old_pc)
    {
        if ((msw & 1) != 0 && (cpu_state.eflags & VM_FLAG) == 0)
        {
                var segdat = new uint16_t[4];
                // pcem: x86seg.c:448 — `#define DPL ((segdat[2] >> 13) & 3)`, macro sur
                // une locale. Fonction locale ici, meme portee, et elle SUIT segdat quand
                // le code le recharge depuis la porte d'appel — c'est voulu chez PCem et
                // c'est le piege de cette fonction : DPL ne designe pas toujours le meme
                // descripteur.
                int DPL() => (segdat[2] >> 13) & 3;

                if ((seg & ~3) == 0)
                {
                        // omitted: pclog("Trying to load CS with NULL selector!...") — pure.
                        x86gpf(null!, 0);
                        return;
                }
                uint32_t addr = (uint32_t)(seg & ~7);
                if ((seg & 4) != 0)
                {
                        if (addr >= ldt.limit) { x86gpf(null!, (uint16_t)(seg & ~3)); return; }
                        addr += ldt.@base;
                }
                else
                {
                        if (addr >= gdt.limit) { x86gpf(null!, (uint16_t)(seg & ~3)); return; }
                        addr += gdt.@base;
                }
                cpl_override = 1;
                segdat[0] = readmemw(0, addr);
                segdat[1] = readmemw(0, addr + 2);
                segdat[2] = readmemw(0, addr + 4);
                segdat[3] = readmemw(0, addr + 6);
                cpl_override = 0;
                if (cpu_state.abrt != 0)
                        return;
                // omitted: `if (output) pclog(...)` (x86seg.c:609-610) — sortie pure.

                // LE BIT 12 SEPARE LES DEUX MONDES : segment de code ordinaire d'un cote,
                // descripteur SYSTEME de l'autre — porte d'appel ou porte de tache. C'est
                // le seul embranchement de cette fonction qui compte vraiment.
                if ((segdat[2] & 0x1000) != 0) /*Normal code segment*/
                {
                        // LE SEGMENT CONFORME NE VERIFIE RIEN, et c'est toute sa raison
                        // d'etre : il s'execute au privilege de l'APPELANT, donc y sauter
                        // depuis un CPL plus bas est legal. Pour un segment NON conforme,
                        // PCem exige `RPL <= CPL` ET `CPL == DPL` — l'egalite, pas une
                        // inegalite : un JMP ne change PAS de niveau de privilege, c'est
                        // l'affaire de loadcscall et de ses portes.
                        if ((segdat[2] & 0x400) == 0) /*Not conforming*/
                        {
                                if ((seg & 3) > CPL) { x86gpf(null!, (uint16_t)(seg & ~3)); return; }
                                if (CPL != DPL()) { x86gpf(null!, (uint16_t)(seg & ~3)); return; }
                        }
                        if (CPL < DPL()) { x86gpf(null!, (uint16_t)(seg & ~3)); return; }
                        if ((segdat[2] & 0x8000) == 0)
                        {
                                x86np("Load CS JMP not present\n", (uint16_t)(seg & 0xfffc));
                                return;
                        }
                        set_use32(segdat[3] & 0x40);

                        // CS_ACCESSED est defini (x86seg.c:17), donc ce bloc EST compile.
                        cpl_override = 1;
                        writememw(0, addr + 4, (uint16_t)(segdat[2] | 0x100)); /*Set accessed bit*/
                        cpl_override = 0;

                        // LE RPL DU SELECTEUR CHARGE DEVIENT LE CPL COURANT, et le DPL du
                        // cache est REECRIT pour correspondre. Les deux lignes vont
                        // ensemble : `CS = (seg & ~3) | CPL` force les deux bits bas, et le
                        // masque sur segdat[2] remplace le champ DPL par CPL avant que
                        // do_seg_load ne le range dans access. Sans la seconde, le cache
                        // porterait le DPL de la table et CPL se lirait faux a l'acces
                        // suivant.
                        CS = (uint16_t)((seg & ~3) | CPL);
                        segdat[2] = (uint16_t)((segdat[2] & ~(3 << (5 + 8))) | (CPL << (5 + 8)));

                        do_seg_load(cpu_state.seg_cs, segdat);
                        if (CPL == 3 && oldcpl != 3)
                                Memory.mem.flushmmucache_cr3();
                        oldcpl = CPL;
                        // omitted: le bloc commente x86seg.c:644-653 — PCem y avait inline
                        //   ce que set_use32 fait, et l'a remplace par l'appel ci-dessus.
                        cycles -= cpu_c.timing_jmp_pm;
                }
                else /*System segment*/
                {
                        if ((segdat[2] & 0x8000) == 0)
                        {
                                x86np("Load CS JMP system selector not present\n", (uint16_t)(seg & 0xfffc));
                                return;
                        }
                        uint16_t type = (uint16_t)(segdat[2] & 0xF00);
                        uint32_t newpc = segdat[0];
                        // LE BIT 11 DU TYPE DIT « 32 BITS », et c'est lui qui decide si le
                        // point d'entree de la porte fait 16 ou 32 bits. Sur un 286 les
                        // types 0xC00 et 0x900 n'existent pas dans une table valide, mais
                        // PCem ne les garde pas : un descripteur forge les atteindrait.
                        if ((type & 0x800) != 0)
                                newpc |= (uint32_t)(segdat[3] << 16);
                        switch (type)
                        {
                        case 0x400: /*Call gate*/
                        case 0xC00:
                                cgate32 = (type & 0x800);
                                cgate16 = cgate32 == 0 ? 1 : 0;
                                cpu_state.oldpc = cpu_state.pc;
                                if ((DPL() < CPL) || (DPL() < (seg & 3)))
                                {
                                        x86gpf(null!, (uint16_t)(seg & ~3));
                                        return;
                                }
                                // Le test de presence est REFAIT ici alors que la garde du
                                // haut vient de le faire sur le meme segdat : code mort chez
                                // PCem, porte tel quel.
                                if ((segdat[2] & 0x8000) == 0)
                                {
                                        x86np("Load CS JMP call gate not present\n", (uint16_t)(seg & 0xfffc));
                                        return;
                                }
                                uint16_t seg2 = segdat[1];

                                if ((seg2 & ~3) == 0)
                                {
                                        // omitted: pclog("...NULL selector! lcsjmpcg") — pure.
                                        x86gpf(null!, 0);
                                        return;
                                }
                                addr = (uint32_t)(seg2 & ~7);
                                if ((seg2 & 4) != 0)
                                {
                                        if (addr >= ldt.limit) { x86gpf(null!, (uint16_t)(seg2 & ~3)); return; }
                                        addr += ldt.@base;
                                }
                                else
                                {
                                        if (addr >= gdt.limit) { x86gpf(null!, (uint16_t)(seg2 & ~3)); return; }
                                        addr += gdt.@base;
                                }
                                // SEGDAT EST RECHARGE, DONC DPL() CHANGE DE SENS. A partir
                                // d'ici la macro designe le descripteur CIBLE de la porte, et
                                // plus la porte elle-meme. C'est le piege de cette fonction.
                                cpl_override = 1;
                                segdat[0] = readmemw(0, addr);
                                segdat[1] = readmemw(0, addr + 2);
                                segdat[2] = readmemw(0, addr + 4);
                                segdat[3] = readmemw(0, addr + 6);
                                cpl_override = 0;
                                if (cpu_state.abrt != 0)
                                        return;

                                if (DPL() > CPL) { x86gpf(null!, (uint16_t)(seg2 & ~3)); return; }
                                if ((segdat[2] & 0x8000) == 0)
                                {
                                        x86np("Load CS JMP from call gate not present\n", (uint16_t)(seg2 & 0xfffc));
                                        return;
                                }

                                switch (segdat[2] & 0x1F00)
                                {
                                case 0x1800:
                                case 0x1900:
                                case 0x1A00:
                                case 0x1B00: /*Non-conforming code*/
                                        if (DPL() > CPL)
                                        {
                                                // omitted: pclog("Call gate DPL > CPL") — pure.
                                                x86gpf(null!, (uint16_t)(seg2 & ~3));
                                                return;
                                        }
                                        // LE C TOMBE DANS LE CAS SUIVANT, et ce n'est pas un
                                        // oubli de `break` : un code non conforme dont le DPL
                                        // passe le test se charge exactement comme un
                                        // conforme. C# interdit la chute implicite, d'ou le
                                        // `goto case` — meme flot, syntaxe differente.
                                        goto case 0x1C00;
                                case 0x1C00:
                                case 0x1D00:
                                case 0x1E00:
                                case 0x1F00: /*Conforming*/
                                        CS = seg2;
                                        do_seg_load(cpu_state.seg_cs, segdat);
                                        if (CPL == 3 && oldcpl != 3)
                                                Memory.mem.flushmmucache_cr3();
                                        oldcpl = CPL;
                                        set_use32(segdat[3] & 0x40);
                                        cpu_state.pc = newpc;

                                        cpl_override = 1;
                                        writememw(0, addr + 4, (uint16_t)(segdat[2] | 0x100)); /*Set accessed bit*/
                                        cpl_override = 0;
                                        break;

                                default:
                                        // omitted: pclog("JMP Call gate bad segment type") — pure.
                                        x86gpf(null!, (uint16_t)(seg2 & ~3));
                                        return;
                                }
                                cycles -= cpu_c.timing_jmp_pm_gate;
                                break;

                        case 0x100: /*286 Task gate*/
                        case 0x900: /*386 Task gate*/
                                // LE PC REVIENT A old_pc, ET C'EST POUR CA QUE loadcsjmp LE
                                // RECOIT. Un changement de tache sauvegarde l'etat courant
                                // dans l'ancien TSS ; il doit y ranger l'adresse de
                                // l'instruction JMP elle-meme, pas celle qui la suit.
                                cpu_state.pc = old_pc;
                                optype = JMP;
                                cpl_override = 1;
                                taskswitch286(seg, segdat, segdat[2] & 0x800);
                                cpu_state.flags &= unchecked((uint16_t)~NT_FLAG);
                                cpl_override = 0;
                                // omitted: le `case 0xB00` commente et l'appel commente a
                                //   taskswitch386 (x86seg.c:770-772) — ce dernier n'a AUCUNE
                                //   definition dans tout l'arbre, voir le registre.
                                return;

                        default:
                                // omitted: pclog("Bad JMP CS ... special descriptor ...") — pure.
                                x86gpf(null!, 0);
                                return;
                        }
                }
                return;
        }

        cpu_state.seg_cs.@base = (uint32_t)(seg << 4);
        cpu_state.seg_cs.limit = 0xFFFF;
        cpu_state.seg_cs.limit_low = 0;
        cpu_state.seg_cs.limit_high = 0xffff;
        CS = seg;
        if ((cpu_state.eflags & VM_FLAG) != 0)
                cpu_state.seg_cs.access = (3 << 5) | 2;
        else
                cpu_state.seg_cs.access = (0 << 5) | 2;
        if (CPL == 3 && oldcpl != 3)
                Memory.mem.flushmmucache_cr3();
        oldcpl = CPL;
        cycles -= cpu_c.timing_jmp_rm;
    }

    // pcem: x86seg.c:135-139 — LA LEVÉE D'EXCEPTION, réduite à ce qu'elle est :
    // poser la cause et le code d'erreur. C'est exec386 qui, voyant `abrt` non nul,
    // appellera x86_doabrt. Le message ne sert qu'au pclog de PCem, omis ici.
    internal static void x86gpf(string s, uint16_t error)
    {
        cpu_state.abrt = (int8_t)ABRT_GPF;
        abrt_error = error;
    }

    // pcem: x86seg.c:140-144 — x86gpf_expected.
    //
    // CE N'EST PAS x86gpf. Il pose `ABRT_GPF | ABRT_EXPECTED`, donc le BIT HAUT
    // en plus — celui-là même qui explique qu'ABRT_MASK vaille 0x7F et non 7
    // (voir x86.cs). Le recompilateur s'en sert pour distinguer une faute
    // attendue, que l'émulation provoque exprès, d'une faute subie. exec386 ne
    // lit que `abrt & ABRT_MASK`, donc la différence est inerte ici — mais la
    // perdre ferait que cpu_state.abrt, qui EST dans le vecteur comparé,
    // divergerait de l'oracle.
    internal static void x86gpf_expected(string s, uint16_t error)
    {
        cpu_state.abrt = (int8_t)(ABRT_GPF | ABRT_EXPECTED);
        abrt_error = error;
    }

    // pcem: x86seg.c:145-149
    internal static void x86ss(string s, uint16_t error)
    {
        cpu_state.abrt = (int8_t)ABRT_SS;
        abrt_error = error;
    }

    // pcem: x86seg.c:150-154 — DÉFAUT DE TSS. Levé par taskswitch286 et par les portes
    // de tâche ; il n'était pas transcrit parce qu'aucun chemin de mode réel ne le lève.
    internal static void x86ts(string s, uint16_t error)
    {
        cpu_state.abrt = (int8_t)ABRT_TS;
        abrt_error = error;
    }

    // pcem: x86seg.c:155-159 — SEGMENT ABSENT, bit `present` du descripteur à zéro.
    internal static void x86np(string s, uint16_t error)
    {
        cpu_state.abrt = (int8_t)ABRT_NP;
        abrt_error = error;
    }

    // DEVIATION: x86abort (x86seg.c:40-48) fait error(), dumpregs(), pclog_end() puis
    //   exit(-1). Les trois premiers sont des sorties pures, non transcrites dans ce
    //   dépôt ; le quatrième est un arrêt du processus. On le rend par pc.fatal(), qui
    //   lève — même geste que partout ailleurs ici, et l'appelant ne reprend pas la main
    //   dans les deux cas. Ses appelants sont dans les chemins de mode protégé les plus
    //   profonds, là où PCem lui-même renonce.
    internal static void x86abort(string format)
        => pc.fatal($"x86abort : {format}\n");

    // pcem: x86seg.c:161-167 — LA TAILLE DE LA PILE, et elle est DOUBLE.
    //
    // `stack32` gouverne si les push/pop utilisent ESP ou SP, et le MÊME fait est
    // recopié dans un bit de cpu_cur_status. Ce n'est pas une redondance gratuite : le
    // recompilateur teste cpu_cur_status en un seul ET pour décider s'il peut réutiliser
    // un bloc compilé. Les deux doivent bouger ENSEMBLE, et c'est pourquoi PCem passe
    // par cette fonction plutôt que d'écrire stack32 directement.
    internal static void set_stack32(int s)
    {
        stack32 = s;
        if (stack32 != 0)
                cpu_cur_status |= CPU_STATUS_STACK32;
        else
                cpu_cur_status &= unchecked((uint16_t)~CPU_STATUS_STACK32);
    }

    // pcem: x86seg.c:169-177 — LA TAILLE D'OPÉRANDE PAR DÉFAUT DU SEGMENT DE CODE.
    //
    // 0x300 ET PAS 1, et ce n'est pas un drapeau booléen : `use32` sert d'INDEX dans
    // ops_286 / ops_386, `(opcode | cpu_state.op32) & 0x3ff`, et op32 vaut use32. Les
    // bits 8 et 9 sélectionnent le quadrant de la table — d'où les quatre copies
    // identiques de ops_286 (A11). Sur un 286 use32 reste nul, donc seul le premier
    // quadrant est atteint ; la fonction est transcrite parce que c'est le mode protégé
    // qui l'appelle, et que sa valeur EST comparée.
    internal static void set_use32(int u)
    {
        if (u != 0)
        {
                use32 = 0x300;
                cpu_cur_status |= CPU_STATUS_USE32;
        }
        else
        {
                use32 = 0;
                cpu_cur_status &= unchecked((uint16_t)~CPU_STATUS_USE32);
        }
    }

    // pcem: x86seg.c:179-213 — LE CHARGEMENT D'UN DESCRIPTEUR, cœur de tout le bloc C.
    //
    // Les quatre mots que la GDT ou la LDT rend deviennent les neuf champs du cache. Ce
    // qui se lit mal et compte beaucoup :
    //
    // limit_raw GARDE LA VALEUR BRUTE, limit peut être MULTIPLIÉE PAR 4096. Le bit 7 de
    // segdat[3] est la granularité : posé, la limite est en PAGES et non en octets, d'où
    // `(limit << 12) | 0xFFF`. limit_raw est ce qu'on relira pour SGDT ou pour réécrire
    // le descripteur — c'est pourquoi les deux champs existent, et pourquoi limit_raw
    // n'est écrit QUE par ici, par LLDT et par LTR.
    //
    // is386 GARDE LES HUIT BITS HAUTS DE LA BASE. Sur un 286 la base fait 24 bits : le
    // quatrième mot n'en porte pas l'octet de poids fort. La branche est transcrite et
    // morte ici — is386 vaut 0 pour les trois machines du dépôt.
    //
    // LE SEGMENT À EXPANSION VERS LE BAS INVERSE LES DEUX BORNES. Le test
    // `(segdat[2] & 0x1800) != 0x1000 || !(segdat[2] & (1 << 10))` isole le seul cas qui
    // le fait : un segment de DONNÉES dont le bit 10 est posé. Alors limit_low devient
    // `limit + 1` et limit_high le maximum — une pile qui croît vers le bas et dont
    // l'accès légal est AU-DESSUS de la limite. Se tromper ici rend des segments qui
    // marchent presque.
    private static void do_seg_load(x86seg s, uint16_t[] segdat)
    {
        s.limit = (uint32_t)(segdat[0] | ((segdat[3] & 0xF) << 16));
        s.limit_raw = s.limit;
        if ((segdat[3] & 0x80) != 0)
                s.limit = (s.limit << 12) | 0xFFF;
        s.@base = (uint32_t)(segdat[1] | ((segdat[2] & 0xFF) << 16));
        if (is386 != 0)
                s.@base |= (uint32_t)((segdat[3] >> 8) << 24);
        s.access = (uint8_t)(segdat[2] >> 8);
        s.access2 = (uint8_t)(segdat[3] & 0xf0);

        if ((segdat[2] & 0x1800) != 0x1000 || (segdat[2] & (1 << 10)) == 0) /*expand-down*/
        {
                s.limit_high = s.limit;
                s.limit_low = 0;
        }
        else
        {
                s.limit_high = (segdat[3] & 0x40) != 0 ? 0xffffffff : 0xffff;
                s.limit_low = s.limit + 1;
        }

        if (s == cpu_state.seg_ds)
        {
                if (s.@base == 0 && s.limit_low == 0 && s.limit_high == 0xffffffff)
                        cpu_cur_status &= unchecked((uint16_t)~CPU_STATUS_NOTFLATDS);
                else
                        cpu_cur_status |= CPU_STATUS_NOTFLATDS;
        }
        if (s == cpu_state.seg_ss)
        {
                if (s.@base == 0 && s.limit_low == 0 && s.limit_high == 0xffffffff)
                        cpu_cur_status &= unchecked((uint16_t)~CPU_STATUS_NOTFLATSS);
                else
                        cpu_cur_status |= CPU_STATUS_NOTFLATSS;
        }
    }

    // pcem: x86seg.c:215-221 — l'état d'un segment en mode virtuel 8086 : DPL 3, limite
    // de 64 Ko. Transcrite parce que pmodeiret et taskswitch286 l'appellent ; le mode
    // V86 lui-même est inatteignable sur un 286, VM_FLAG étant un drapeau de EFLAGS que
    // seuls un IRET 32 bits ou un changement de tâche peuvent poser.
    private static void do_seg_v86_init(x86seg s)
    {
        s.access = (3 << 5) | 2;
        s.access2 = 0;
        s.limit = 0xffff;
        s.limit_low = 0;
        s.limit_high = 0xffff;
    }

    // pcem: x86seg.c:223-269 — LA REVALIDATION D'UN SÉLECTEUR, et son effet est de
    // VIDER le segment, pas de lever une exception.
    //
    // Appelée après un changement de niveau de privilège : un sélecteur parfaitement
    // chargé peut devenir illégal parce que CPL a monté. PCem ne fait alors PAS de
    // faute — il fait `loadseg(0, s)`, ce qui charge le sélecteur nul. La faute viendra
    // plus tard, au premier accès, et c'est le comportement du silicium.
    //
    // LE BIT 2 DU SÉLECTEUR CHOISIT LA TABLE : posé, c'est la LDT ; sinon la GDT. Et la
    // comparaison est `(seg & ~7) >= limit`, donc sur l'OFFSET du descripteur, les trois
    // bits bas étant le sélecteur de table et le RPL.
    //
    // LE CODE CONFORME EST LA SEULE EXCEPTION AU TEST DE PRIVILÈGE. Les cas 0x1E et
    // 0x1F passent sans rien vérifier : un segment de code conforme s'exécute au
    // privilège de l'APPELANT, donc y accéder depuis un CPL plus bas est légal par
    // construction. Tout le reste exige `(seg & 3) <= dpl && CPL <= dpl`.
    private static void check_seg_valid(x86seg s)
    {
        int dpl = (s.access >> 5) & 3;
        int valid = 1;

        if ((s.seg & 4) != 0)
        {
                if ((s.seg & ~7) >= ldt.limit)
                        valid = 0;
        }
        else
        {
                if ((s.seg & ~7) >= gdt.limit)
                        valid = 0;
        }

        switch (s.access & 0x1f)
        {
        case 0x10:
        case 0x11:
        case 0x12:
        case 0x13: /*Data segments*/
        case 0x14:
        case 0x15:
        case 0x16:
        case 0x17:
        case 0x1A:
        case 0x1B: /*Readable non-conforming code*/
                if ((s.seg & 3) > dpl || (CPL) > dpl)
                {
                        valid = 0;
                        break;
                }
                break;

        case 0x1E:
        case 0x1F: /*Readable conforming code*/
                break;

        default:
                valid = 0;
                break;
        }

        if (valid == 0)
                loadseg(0, s);
    }

    // pcem: x86seg.c:804-862 — LES QUATRE ACCÈS À LA PILE DU MODE PROTÉGÉ.
    //
    // CE NE SONT PAS LES MACROS PUSH_W / POP_W de x86_ops_call.h, qui portent le même
    // nom à un tiret près et vivent ailleurs. Celles-ci sont des FONCTIONS de x86seg.c,
    // appelées par loadcscall, pmoderetf, pmodeint et pmodeiret.
    //
    // CHACUNE TESTE abrt APRÈS L'ACCÈS ET AVANT DE BOUGER LE POINTEUR. Si l'écriture
    // faute — pile trop courte, segment absent — ESP ne doit PAS avoir avancé : le
    // gestionnaire d'exception rejouera l'instruction, et une pile déjà décrémentée la
    // ferait écrire deux fois. C'est la partie facile à perdre en transcrivant.
    internal static void PUSHW(uint16_t v)
    {
        if (stack32 != 0)
        {
                writememw(ss, ESP - 2, v);
                if (cpu_state.abrt != 0)
                        return;
                ESP -= 2;
        }
        else
        {
                writememw(ss, (uint32_t)((SP - 2) & 0xFFFF), v);
                if (cpu_state.abrt != 0)
                        return;
                SP -= 2;
        }
    }

    internal static void PUSHL(uint32_t v)
    {
        if (stack32 != 0)
        {
                writememl(ss, ESP - 4, v);
                if (cpu_state.abrt != 0)
                        return;
                ESP -= 4;
        }
        else
        {
                writememl(ss, (uint32_t)((SP - 4) & 0xFFFF), v);
                if (cpu_state.abrt != 0)
                        return;
                SP -= 4;
        }
    }

    internal static uint16_t POPW()
    {
        uint16_t tempw;
        if (stack32 != 0)
        {
                tempw = readmemw(ss, ESP);
                if (cpu_state.abrt != 0)
                        return 0;
                ESP += 2;
        }
        else
        {
                tempw = readmemw(ss, SP);
                if (cpu_state.abrt != 0)
                        return 0;
                SP += 2;
        }
        return tempw;
    }

    internal static uint32_t POPL()
    {
        uint32_t templ;
        if (stack32 != 0)
        {
                templ = readmeml(ss, ESP);
                if (cpu_state.abrt != 0)
                        return 0;
                ESP += 4;
        }
        else
        {
                templ = readmeml(ss, SP);
                if (cpu_state.abrt != 0)
                        return 0;
                SP += 4;
        }
        return templ;
    }
}
