// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/cpu/x86seg.c  (lignes 50-74, 419-448, 452-566)
// SHA256: voir oracle.tsv ; vérifier avec tools/check-oracle.sh
// STATUS: partial — branches MODE RÉEL uniquement (~80 lignes sur 3 187).
//
// x86seg.c est partagé entre le cœur 8088 et le cœur 386 : 808x.c l'appelle pour
// loadcs/loadseg, et sur un XT `msw & 1` vaut toujours 0, donc seules les
// branches `else` sont atteintes. Tout le mode protégé — descripteurs, portes,
// TSS, gates — est hors palier (a) et constitue son propre lot de travail.
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
}
