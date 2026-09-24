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
        // LA GARDE MANQUAIT, ET C'ETAIT LE SEUL ENDROIT DU BLOC C QUI SE TROMPAIT
        // SANS LE DIRE.
        //
        // Le C ouvre sur `if (msw & 1 && !(eflags & VM_FLAG))` (x86seg.c:276) et met
        // TOUT le mode protege dedans — 133 lignes vives sur les 176 de la fonction.
        // Le C# entrait directement dans la branche mode reel : en mode protege il
        // chargeait un descripteur plat `seg << 4` et rendait 0, comme si tout allait
        // bien. loadcsjmp, lui, echoue bruyamment depuis toujours.
        //
        // La ROM de l'AT fait `MOV SS, 0x28` juste apres son JMP FAR en mode protege :
        // c'est le premier appelant qui aurait menti, et la divergence se serait
        // manifestee des milliers d'instructions plus loin, sur une pile fausse.
        if ((msw & 1) != 0 && (cpu_state.eflags & VM_FLAG) == 0)
        {
                pc.fatal($"loadseg en mode protege (seg {seg:X4}) : x86seg.c n'est " +
                         "transcrit qu'en mode reel (bloc C du plan)\n");
                return 1;
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

    internal static void pmoderetf(int is32, uint16_t off)
        => pc.fatal($"pmoderetf (is32 {is32}) : x86seg.c n'est transcrit qu'en " +
                    "mode reel (bloc C du plan)\n");

    internal static void pmodeint(int num, int soft)
        => pc.fatal($"pmodeint (num {num:X2}) : x86seg.c n'est transcrit qu'en " +
                    "mode reel (bloc C du plan)\n");

    internal static void pmodeiret(int is32)
        => pc.fatal($"pmodeiret (is32 {is32}) : x86seg.c n'est transcrit qu'en " +
                    "mode reel (bloc C du plan)\n");

    // pcem: x86seg.c — loadcsjmp, la BRANCHE MODE RÉEL seulement (les 19 dernières
    // lignes des 236 de la fonction).
    //
    // La branche mode protégé — 200 lignes qui lisent le descripteur dans la GDT
    // ou la LDT, vérifient CPL/DPL, distinguent segment de code conforme, porte
    // d'appel, TSS — relève du bloc `Ap` du plan et n'est pas transcrite. Elle
    // échoue BRUYAMMENT plutôt que de rendre du silence, même doctrine que la
    // table vide de A2.2b : un JMP far en mode protégé doit nommer ce qui manque,
    // pas charger CS comme si de rien n'était.
    //
    // La branche mode réel, elle, est exactement loadcs ci-dessus plus une
    // facturation de cycles. C'est elle que le vecteur de reset emprunte.
    internal static void loadcsjmp(uint16_t seg, uint32_t old_pc)
    {
        if ((msw & 1) != 0 && (cpu_state.eflags & VM_FLAG) == 0)
        {
                pc.fatal($"loadcsjmp en mode protege (seg {seg:X4}) : x86seg.c n'est " +
                         "transcrit qu'en mode reel (bloc Ap du plan)\n");
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
        cycles -= cpu.timing_jmp_rm;
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
