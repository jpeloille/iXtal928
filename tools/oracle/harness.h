/* SPDX-FileCopyrightText: 2026 Julien Peloille
 * SPDX-License-Identifier: GPL-2.0-only
 *
 * Contrat entre l'oracle C et le harnais C#.
 *
 * L'oracle compile le VRAI cœur 8088 de PCem (pcem-dev/src/cpu/808x.c) dans une
 * bibliothèque partagée, et expose de quoi le piloter instruction par instruction.
 * C'est la seule source de vérité pour la comptabilité de cycles : SingleStepTests
 * valide l'état architectural, pas le modèle de temps inventé par PCem.
 *
 * Le point clé : harness.c fait `#include "808x.c"`. Sans ça, `memcycs`,
 * `fetchcycles`, `fetchclocks`, `prefetchw`, `prefetchpc`, `prefetchqueue[]`,
 * `nextcyc`, `cycdiff` et `tsc_frac` sont `static` et donc invisibles — et la
 * mitigation du risque n°1 du plan (dérive temporelle silencieuse) n'existe pas.
 */

#ifndef IXTAL26_HARNESS_H
#define IXTAL26_HARNESS_H

#include <stdint.h>

/* 64 depuis C7a, et pas par confort : la branche 286 de taskswitch286 fait 19
 * writememw (sed -n '2639,2850p' x86seg.c | grep -c writememw), et __wrap_writememwl
 * pose DEUX entrees par mot, donc 38. Une porte d'appel 16 bits avec count = 31 — le
 * maximum, x86seg.c:989 — fait 33 PUSHW, donc 66 : ce pire cas reste AU-DELA de 64 et
 * doit etre couvert par la comparaison d'octets de h_read, pas par le journal. C'est
 * dit plutot que pretendu couvert. */
#define H_WLOG_MAX 64
#define H_RAM_SIZE 0x100000u /* 1 Mo — l'espace d'adressage complet du 8088 */

/* x86.h aplatit cpu_state en macros (`#define cycles cpu_state._cycles`, x86.h:122,
 * et la famille cs/CS/cr0/msw). Si cet en-tête est inclus APRÈS x86.h — ce que fait
 * harness_stubs.c — le champ `cycles` de h_state se ferait réécrire en
 * `cpu_state._cycles` et la structure ne compilerait pas.
 *
 * On neutralise donc ces noms ici. `#undef` d'une macro non définie est légal, donc
 * cet en-tête reste utilisable seul. Les fichiers qui ont besoin des macros de PCem
 * incluent x86.h après, ou écrivent cpu_state._cycles explicitement (cf. harness.c). */
#undef cycles
#undef cr0
#undef msw

/* Ordre des segments dans les tableaux ci-dessous. Fixé, partagé avec le C#. */
enum { H_SEG_CS = 0, H_SEG_DS, H_SEG_ES, H_SEG_SS, H_SEG_FS, H_SEG_GS, H_SEG_COUNT };

/* Les quatre descripteurs SYSTÈME. Ils ont le type x86seg comme les six segments,
 * mais ce ne sont pas des segments : gdt et idt n'ont pas de sélecteur, aucun des
 * quatre n'est chargeable par un préfixe, et `ea_seg` ne peut pas les viser. D'où
 * un jeu de tableaux à part plutôt qu'un élargissement de H_SEG_COUNT — sans quoi
 * la boucle de `ea_seg_idx` les balaierait pour rien et l'index cesserait de vouloir
 * dire « lequel des six ». Chez PCem ce sont des globaux (x86.h:171), pas des
 * membres de cpu_state. */
enum { H_SYS_GDT = 0, H_SYS_LDT, H_SYS_IDT, H_SYS_TR, H_SYS_COUNT };

/* Quel cœur le harnais exécute. Chez PCem le choix n'est pas un drapeau à part : c'est
 * `is386 ? exec386 : AT ? exec386 : execx86` (pc.c:478-487). Un 286 est donc « AT sans
 * is386 », et h_set_core() ne fait que mémoriser le choix pour que h_reset() le
 * réapplique — resetx86() branche sur AT (808x.c:680). */
/* H_CORE_386 depuis G2 etape D0.2 : le MEME exec386, mais sur une machine dont la table
 * porte un 386 — cpu_set() pose is386, les temps du 386 et x86_setopcodes(ops_386, …).
 * La machine est l'ami386 (ROM_AMI386SX), la premiere de G3. */
/* H_CORE_486 depuis G6.0 : encore le MEME exec386 — PCem n'a pas de table d'opcodes 486,
 * tout est `if (is486)` dans les handlers du 386 (cpu.c:231) — sur l'ami486 (ROM_AMI486),
 * avec la carte plate de 16 Mo du 386. Seule la machine, donc la table de cpu_set, change. */
/* H_CORE_8086 depuis G1.0 : le MEME execx86 que le 8088, sur l'Olivetti M24 (cpus_8086) —
 * cpu_set() pose is8086. Fuzzeur seulement : h_boot mène une machine 8086 par H_CORE_8088. */
enum { H_CORE_8088 = 0, H_CORE_286 = 1, H_CORE_386 = 2, H_CORE_486 = 3, H_CORE_8086 = 4 };
void h_set_core(int core);
int h_get_core(void);

/* A2.2a — le chemin de FETCH de exec386, exposé pour être diffé AVANT que exec386
 * existe côté C#. fastreadl est un `static inline` de 386_common.h : absent de la .so
 * sans cette réexportation. Il traverse getpccache, le cache de page pccache/pccache2
 * et son arithmétique de biais. Une erreur de page y est parfaitement silencieuse —
 * elle rend des octets plausibles, pris au mauvais endroit. */
uint32_t h_fastreadb(uint32_t a);
uint32_t h_fastreadw(uint32_t a);
uint32_t h_fastreadl(uint32_t a);
uint32_t h_pccache(void);

/* Le modèle de préfetch du 286. `static` dans 386_dynarec.c, exposés par
 * harness_386.c qui le compile dans son unité de traduction. */
int h_prefetch_bytes(void);
int h_prefetch_prefixes(void);
void h_prefetch_reset(void);

/* Remet a zero les DEUX champs du cache descripteur que seg_reset() ne touche pas,
 * limit_raw et checked, sur les six segments. DEVIATION du harnais : PCem ne les
 * remet jamais, n'amorcant qu'une fois par processus. Voir harness.c. */
void h_seg_clear_residue(void);

/* Disposition explicite, pas de padding implicite : le C# marshale ça tel quel.
 * Tout champ ajouté ici doit l'être aussi côté C#, et entrer dans le vecteur
 * diffé — un champ non comparé est un champ où la dérive se cache. */
typedef struct h_state {
        /* --- état architectural ------------------------------------------ */
        uint32_t regs[8];             /* .l de chaque x86reg : AX CX DX BX SP BP SI DI */
        uint32_t seg_base[H_SEG_COUNT];
        uint16_t seg_sel[H_SEG_COUNT];

        /* LE CACHE DESCRIPTEUR de chaque segment, ajouté au jalon 286.
         *
         * Les deux côtés le portaient déjà — x86seg a huit champs chez PCem
         * (x86.h:37-45) comme en C# (x86.cs) — mais le vecteur n'en comparait que
         * DEUX, `base` et `seg`. Sur un 8088 cela suffisait : `msw & 1` vaut toujours
         * zéro, loadseg ne pose que la base, et les six autres champs ne bougent
         * jamais. En mode protégé ce sont eux qui portent tout — limite, droits
         * d'accès, granularité — et une divergence y serait restée invisible.
         *
         * Ils entrent AVANT d'être nécessaires, et c'est délibéré : le 8088 les
         * compare dès maintenant à des valeurs qu'il ne fait pas varier, ce qui
         * vérifie le câblage des deux côtés pendant qu'il est encore trivial. */
        uint32_t seg_limit[H_SEG_COUNT];
        uint32_t seg_limit_raw[H_SEG_COUNT];
        uint32_t seg_limit_low[H_SEG_COUNT];
        uint32_t seg_limit_high[H_SEG_COUNT];
        int32_t  seg_checked[H_SEG_COUNT];
        uint8_t  seg_access[H_SEG_COUNT];
        uint8_t  seg_access2[H_SEG_COUNT];

        /* LES QUATRE DESCRIPTEURS SYSTÈME et les registres de contrôle, ajoutés au
         * jalon 286 juste après le cache descripteur, et pour la même raison : sur un
         * 8088 ils sont CONSTANTS, donc le câblage se vérifie pendant qu'il est encore
         * trivial et le contrôle négatif coûte trois lignes. Les laisser pour plus tard
         * ferait câbler exec386 contre un vecteur aveugle aux registres exacts que
         * l'amorçage du mode protégé écrit — LGDT, LIDT, LLDT, LTR, LMSW n'écrivent
         * QUE là-dedans. Une divergence y resterait invisible jusqu'à ce qu'elle
         * ressorte, bien plus loin, en adresse fausse.
         *
         * Les neuf champs de x86seg sont repris pour les quatre, alors que PCem n'en
         * écrit que six (base, limit, limit_raw, access, access2, seg — mesuré sur
         * x86seg.c et loadall_load_segment, x86_ops_misc.h:896-912). Les trois autres
         * sont repris quand même : le cas qu'on veut attraper est justement le C# qui
         * ÉCRIT un champ que le C laisse tranquille. */
        uint32_t sys_base[H_SYS_COUNT];
        uint32_t sys_limit[H_SYS_COUNT];
        uint32_t sys_limit_raw[H_SYS_COUNT];
        uint32_t sys_limit_low[H_SYS_COUNT];
        uint32_t sys_limit_high[H_SYS_COUNT];
        int32_t  sys_checked[H_SYS_COUNT];
        uint16_t sys_sel[H_SYS_COUNT];
        uint8_t  sys_access[H_SYS_COUNT];
        uint8_t  sys_access2[H_SYS_COUNT];

        /* cr0 est cpu_state.CR0.l ; msw en est les 16 bits bas, donc pas de champ
         * séparé. cr4 est omis : 486 et au-delà, aucun chemin 286 ne le lit.
         * cpl_override n'est pas un registre mais l'interrupteur qui fait sauter les
         * contrôles de privilège pendant un chargement de descripteur ; le comparer,
         * c'est attraper un C# qui oublierait de le remettre à zéro. */
        uint32_t cr0;
        uint32_t cr2;
        uint32_t cr3;
        uint32_t use32;
        int32_t  stack32;
        int32_t  cpl_override;

        /* cr4 ET LES HUIT REGISTRES DE DEBOGAGE, ajoutes en G2 etape D0.1, avant le
         * premier handler 386 qui les ecrit : MOV CRx et MOV DRx (x86_ops_mov_ctrl.h:
         * 134, :187, :209, :221). Meme doctrine que le cache descripteur : ils entrent
         * CONSTANTS, a zero des deux cotes, et le cablage se verifie pendant qu'il est
         * trivial. cr4 est remis par resetx86 (808x.c:677) ; dr[] ne l'est par RIEN,
         * PCem n'amorcant qu'une fois par processus — h_seg_clear_residue le remet,
         * comme cr2 et cr3 ; ClearSegResidue cote C#.
         * Les registres de test TR6/TR7 n'ont pas de stockage chez PCem : MOV TRx ne
         * fait que journaliser (x86_ops_mov_ctrl.h:227-275). Rien a ajouter.
         *
         * `eflags` n'a PAS a s'elargir : c'est deja le MOT HAUT d'EFLAGS
         * (x86.h:113, VM_FLAG = 0x0002 « In EFLAGS »), donc RF et VM y sont. */
        uint32_t cr4;
        uint32_t dr[8];

        /* LES DRAPEAUX PARESSEUX (x86.h:65-68), ajoutés en A2.1.
         *
         * exec386 ne matérialise pas `flags` : il retient l'OPÉRATION (flags_op) et ses
         * opérandes, et ne reconstruit les six bits arithmétiques qu'au moment où
         * quelqu'un les lit — flags_rebuild(), x86_flags.h:420. Entre deux
         * reconstructions, `cpu_state.flags` est donc PÉRIMÉ.
         *
         * D'où le piège : comparer `flags` seul, c'est comparer un champ mort DES DEUX
         * CÔTÉS. L'accord vide parfait.
         *
         * On COMPARE ces quatre champs plutôt que d'appeler flags_rebuild() avant chaque
         * capture, et le départage n'est pas la mutation — flags_rebuild est gardée et
         * idempotente — mais la COUVERTURE. L'appel rendrait flags_op égal à
         * FLAGS_UNKNOWN à toutes les captures PAR CONSTRUCTION : la représentation
         * paresseuse ne serait jamais comparée, et un cœur qui calcule flags_res
         * autrement tout en matérialisant les mêmes six bits passerait. Ici la
         * divergence échoue à l'instruction qui l'a causée.
         *
         * Inertes sur un 8088 — 808x.c ne contient aucune occurrence de flags_op, et les
         * six appels de x86seg.c sont en mode protégé ou SMM. Ils entrent donc AVANT
         * d'être nécessaires, comme le cache descripteur avant eux. */
        int32_t  flags_op;
        uint32_t flags_res;
        uint32_t flags_op1;
        uint32_t flags_op2;

        /* LE MODÈLE DE PRÉFETCH DU 286 (386_dynarec.c:152-153), ajouté en A2.2d.
         *
         * Pendants exacts, pour le cœur 286, de memcycs / fetchcycles /
         * prefetchqueue plus bas : de l'état de TEMPS entre instructions, que rien
         * d'architectural ne révèle. Le vecteur les comparait pour le 8088 depuis M1
         * et pas pour le 286 — un trou, et il a mordu : un cas dirigé rendait
         * « cycles consommés : oracle 24, C# 20 » sans qu'aucun champ ne dise
         * pourquoi. Le fuzzeur ne pouvait pas le voir, les deux côtés dérivant
         * ensemble tant qu'ils exécutent la même suite. */
        int32_t  prefetch_bytes;
        int32_t  prefetch_prefixes;

        uint16_t flags;
        uint16_t eflags;
        uint16_t prefetchpc;
        uint32_t pc;
        uint32_t oldpc;
        uint32_t eaaddr;
        int32_t ea_seg_idx;           /* lequel des 6 segments ea_seg vise ; -1 sinon */
        int32_t ssegs;
        int32_t abrt;

        /* --- comptabilité de cycles (les `static` de 808x.c) -------------- */
        int32_t cycles;
        uint64_t tsc;
        uint64_t tsc_frac;
        int32_t memcycs;
        int32_t fetchcycles;
        int32_t fetchclocks;
        int32_t nextcyc;
        int32_t cycdiff;
        int32_t current_diff;
        int32_t prefetchw;
        uint8_t prefetchqueue[6];
        uint8_t _pad[2];

        /* --- état d'interruption ----------------------------------------- */
        int32_t noint;
        int32_t inhlt;
        int32_t takeint;

        /* --- compteurs d'appels des stubs --------------------------------
         * Mitigation du risque n°3 : le cas dangereux n'est pas le désaccord
         * (bruyant) mais l'accord vide — les deux côtés stubent à la même
         * constante et une divergence dans le chemin qui la consomme est
         * masquée. « Aucun côté n'a jamais appelé inb » se lit comme un accord.
         * Ces compteurs font partie du vecteur diffé, donc zéro-des-deux-côtés
         * sur un stub que le test devait exercer FAIT ÉCHOUER la passe. */
        uint64_t n_inb;
        uint64_t n_outb;
        uint64_t n_picint;
        uint64_t n_picinterrupt;
        uint64_t n_timer_process;
        uint64_t n_readmembl;
        uint64_t n_writemembl;
        uint64_t n_readmemwl;
        uint64_t n_writememwl;
        uint64_t n_fatal;

        /* --- les sept globaux du MODE PROTEGE, depuis le bloc C etape C7a -----
         *
         * Aucun n'etait dans ce vecteur, et les sept sont ecrits par les fonctions
         * de x86seg.c. Places ICI et non parmi les registres : ce sont des globaux
         * de x86seg.c et de 808x.c, pas des champs de cpu_state.
         *
         * Le lien les fournit deja — nm -D les rend tous en B — donc aucune unite
         * a ajouter : la regle 3 joue a l'endroit pour une fois. */
        uint32_t abrt_error;   /* x86seg.c:27 — ecrit par les cinq leveurs (:138-158),
                                  lu par x86_doabrt (:119-131) pour empiler le code
                                  d'erreur. x86_doabrt SAUTE cet empilement quand
                                  `abrt || x86_was_reset`, donc sur faute imbriquee il
                                  ne laisse AUCUNE trace en RAM. Et h_state.abrt est un
                                  accord vide : 386.c:205 le remet a 0 AVANT l'appel. */
        int32_t intgatesize;   /* x86seg.c:32 — ecrit par pmodeint seul (:1702), lu par
                                  x86_doabrt seul (:117). Decide entre writememw et
                                  writememl, donc le NOMBRE d'octets empiles. */
        int32_t cgate16;       /* x86seg.c:28 — loadcsjmp :671, loadcscall :988. */
        int32_t cgate32;       /* x86seg.c:28 — loadcsjmp :670, loadcscall :987. Paire
                                  REDONDANTE par construction (cgate16 = !cgate32) :
                                  comparer les deux attrape une transcription qui n'en
                                  poserait qu'un. cgate32 decide PUSH_L vs PUSH_W dans
                                  la macro appelante (x86_ops_call.h:21, :69). */
        int32_t optype;        /* x86.h:250-254 — le SEUL des sept a porter de l'etat
                                  ENTRE instructions : `grep -c 'optype = 0' x86seg.c`
                                  rend 0, et x86seg.c:765 (JMP) comme :1995 (OPTYPE_INT)
                                  ne se defont jamais. taskswitch286 le lit dix fois. */
        int32_t oldcpl;        /* 808x.c:35 — x86_doabrt:79 fait
                                  `seg_cs.access = oldcpl << 5`, donc un oldcpl divergent
                                  change le CPL apres TOUTE faute. Et sa garde
                                  `CPL == 3 && oldcpl != 3` deplace un flushmmucache_cr3,
                                  donc des cycles, sur une instruction ulterieure. */
        uint16_t cur_status;   /* cpu_cur_status (x86.h:185). Ecrit par set_stack32,
                                  set_use32, do_seg_load, LMSW et LOADALL ; lu par RIEN
                                  que l'oracle execute — src/codegen/ est hors de SRC.
                                  Retenu QUAND MEME, et c'est la doctrine de b932df7 :
                                  « le cas qu'on veut attraper est le C# qui ECRIT un
                                  champ que le C laisse tranquille ». */
        uint16_t _pad2[3];

        /* --- LE x87, depuis G4.0 (ABI 27) -----------------------------------
         *
         * Ils entrent AVANT le premier handler x87, et CONSTANTS tant que hasfpu est nul
         * — op_nofpu n'en touche aucun (x87_ops.h:278-295). Même doctrine que cr4 et dr[]
         * en G2 D0.1 : le câblage se vérifie pendant qu'il est trivial.
         *
         * ST[] en BITS BRUTS et non en double : un NaN n'est pas égal à lui-même, et une
         * comparaison de doubles verrait deux NaN de charges différentes, ou 0 et -0,
         * comme égaux ou différents à contresens. PCem garde ST en double (x86.h:93) : un
         * uint64 le porte au bit près.
         *
         * MM[].q et MM_w4[] sont de l'état x87, pas seulement MMX : FILD/FISTP 64 bits y
         * gardent l'entier exact (TAG_UINT64, x87.h:30), et FSAVE/FRSTOR les lisent
         * (x87_ops.h:152-176). x87_pc_* et x87_op_* sont des globales de x87.c:22-23.
         * fpu_type et hasfpu sont comparés : c'est h_set_fpu qui les pose, des deux côtés,
         * et plus un zéro implicite. */
        uint64_t fpu_st[8];
        uint64_t fpu_mm[8];
        uint16_t fpu_mm_w4[8];
        uint8_t  fpu_tag[8];
        int32_t  fpu_top;
        uint16_t npxs;
        uint16_t npxc;
        uint32_t x87_pc_off;
        uint32_t x87_op_off;
        uint16_t x87_pc_seg;
        uint16_t x87_op_seg;
        int32_t  ismmx;
        int32_t  fpu_type;
        int32_t  hasfpu;

        /* --- divers ------------------------------------------------------- */
        uint64_t ins;                 /* instructions exécutées depuis h_reset */
} h_state;

#ifdef __cplusplus
extern "C" {
#endif

/* Remet le cœur ET le harnais à l'état post-reset d'un XT 8088 :
 * CS=0xFFFF, pc=0, rammask=0xFFFFF, AT=0, is386/is486/hasfpu=0, is8086=0,
 * tables znp/mod1 reconstruites, file de préfetch vidée, compteurs à zéro. */
void h_reset(void);

/* Écrit len octets à l'adresse physique addr dans la RAM de l'oracle. */
void h_load(uint32_t addr, const uint8_t *buf, uint32_t len);

/* Lit len octets depuis la RAM de l'oracle. */
void h_read(uint32_t addr, uint8_t *buf, uint32_t len);

/* Remplit toute la RAM d'une valeur. SingleStepTests impose 0x90 : « all bytes
 * fetched after the initial instruction bytes are set to 0x90 ». Laisser des
 * zéros change tout opérande lu hors des octets listés dans initial.ram — un
 * diviseur, une source de chaîne — et fabrique des divergences qui ne sont ni
 * celles de PCem ni celles du silicium, mais celles du harnais. */
void h_fill_ram(uint8_t value);
void h_fill_ram2(uint8_t a, uint8_t b);

/* Positionne CS:IP (et la base de segment correspondante) avant exécution. */
void h_set_cs_ip(uint16_t cs_sel, uint16_t ip);

/* Ordre du vecteur de registres de h_setregs / h_getregs. Calqué sur l'objet
 * "regs" de SingleStepTests pour que le chargement d'un cas soit direct. */
enum {
        H_R_AX = 0, H_R_BX, H_R_CX, H_R_DX, H_R_CS, H_R_SS, H_R_DS, H_R_ES,
        H_R_SP, H_R_BP, H_R_SI, H_R_DI, H_R_IP, H_R_FLAGS, H_R_COUNT
};

/* Charge d'un coup les 14 registres architecturaux. Les segments passent par
 * loadcs/loadseg, donc leur base est recalculée (sel << 4 en mode réel), et la
 * file de préfetch est vidée — ce qui est le bon comportement pour un cas SST :
 * on sème la RAM à cs:ip et on ignore le champ "queue", redondant avec elle. */
void h_setregs(const uint16_t r[H_R_COUNT]);

/* Relit les 14 mêmes registres. */
void h_getregs(uint16_t r[H_R_COUNT]);

/* G2, D0.4 — CE QUE h_setregs NE POSE PAS SUR UN 386, à appeler APRÈS lui : les moitiés
 * hautes des huit registres généraux (ordre de cpu_state.regs : EAX ECX EDX EBX ESP EBP
 * ESI EDI), le mot haut d'EFLAGS (VM, RF — x86.h:113) et les sélecteurs de FS et GS,
 * chargés par loadseg comme les quatre autres. (Pas `gs` : x86.h:143 en fait une macro.) Le fuzzeur du cœur 386 les tire au
 * hasard ; les laisser à zéro rendrait invisible un handler 16 bits qui écrase la
 * moitié haute. */
void h_setregs386(const uint16_t hi[8], uint16_t eflags, uint16_t fs_sel, uint16_t gs_sel);

/* G2, D0.5 — les registres système qu'un cas SingleStepTests/80386 pose (RG32 : cr0, cr3,
 * dr6, dr7). cr0 est cpu_state.CR0.l, msw en est le mot bas (x86.h). */
void h_setsys386(uint32_t cr0_val, uint32_t cr3_val, uint32_t dr6, uint32_t dr7);

/* G2, D6 — page-check : un appel de mmutranslatereal dans un contexte posé (harness.c),
 * et mmu_perm, hors de h_state. */
uint32_t h_mmutranslate(uint32_t addr, int rw, int cpl, int cpl_ovr, int abrt_in);
int h_mmu_perm(void);

/* G2, D0.5 — flags_rebuild(), pour la sonde SST 386 (harness_386.c). */
void h_flags_rebuild(void);

/* Exécute exactement une instruction et rend le nombre de cycles consommés.
 * Le budget est réarmé à l'intérieur : execx86() boucle tant que cycles > 0,
 * donc on l'appelle avec de quoi faire une seule instruction. */
int h_step(void);

/* Un pas AVEC le geste de la boucle tracee de h_runpc, donc SANS toucher timer_target.
 * La phase 2 du boot-diff doit compter les memes iterations que la phase 1, faute de
 * quoi elle ne rejoue pas la meme execution. Voir harness.c. */
int h_step_trace(void);

/* La borne du journal d'ecritures, pour que le C# la CONFRONTE au lieu de la recopier.
 * Les deux 16 etaient ecrits en dur et jamais compares : monter un seul des deux donnait
 * un vert tronque au lieu d'une erreur. C'est un changement d'ABI PAR LE COMPORTEMENT,
 * qu'un .so perime ne signalerait pas — d'ou l'accesseur. */
int h_wlog_max(void);

/* Retire tsc du hachage de trace, pour separer une divergence de TEMPS d'une divergence
 * FONCTIONNELLE. Drapeau de diagnostic : a poser des deux cotes, et un boot-diff sans
 * tsc ne remplace pas un boot-diff complet. Voir harness.c. */
void h_set_trace_notsc(int on);

/* Exécute jusqu'à épuisement d'un budget de cycs cycles — la forme qu'emploie
 * runpc() (execx86(cpu_get_speed() / 100), soit 47 727 cycles par tranche de
 * 10 ms sur un 8088 à 4,77 MHz ; voir h_slice_budget). Sert à vérifier que h_step() ne fausse pas la comptabilité,
 * et servira au diff plein régime. Rend les cycles réellement consommés. */
int h_run(int cycs);

/* Recopie l'état complet, y compris les `static` de 808x.c. */
void h_getstate(h_state *out);

/* sha256-like : somme de contrôle rapide de la RAM, pour diffé rer sans
 * transférer 1 Mo par instruction. */
uint64_t h_ram_hash(void);

/* --- amorçage machine complète (IBM PC 5150) -----------------------------
 * Miroir exact de pc.initpc() côté C#. Rend 1 si le BIOS a pu être chargé.
 * romspath est le répertoire des ROMs, p.ex. "roms". */
int h_boot(const char *romspath);

/* Exécute une tranche de 10 ms, comme runpc(). */
void h_runpc(void);

/* Trace d'amorçage : hachage d'une ligne d'état par instruction, écrit dans un
 * fichier. C'est la forme « phase 1 » du diff — 8 octets par instruction au lieu
 * de la ligne formatée, donc utilisable sur des millions d'instructions. */
int h_trace_open(const char *path);
void h_trace_close(void);
/* Le premier errno d'une écriture refusée de la trace (fwrite, ou le vidage de fclose), 0 si
 * tout est passé ; à lire après h_trace_close(). Ajouté le 04/10 (ABI 45). */
int h_trace_errno(void);
/* L'accélération du 4 octobre (ABI 46). h_trace_hash_value : le hachage d'un état donné, plié
 * (ref = 0) ou par l'ancien MIX octet par octet (ref = 1), pour trace-hash-check. h_raz_fin : à la
 * sortie du processus, un dernier vidage des tables de traduction par l'anneau et leur balayage ;
 * rend le nombre d'écarts, -1 sans remise courte. h_mem_size et h_ram_cmp : la RAM comparée octet
 * par octet (-1 égale ; -2 si n dépasse la RAM de l'oracle ; sinon le premier décalage différent,
 * et l'octet de l'oracle à ce décalage dans *octet). */
uint64_t h_trace_hash_value(int ref, uint16_t sel_cs, uint32_t pc, const uint16_t *regs, uint16_t sel_ds,
                            uint16_t sel_es, uint16_t sel_ss, uint16_t flags, uint64_t t, int notsc);
int32_t h_raz_fin(void);
int h_mem_size(void);
int64_t h_ram_cmp(const uint8_t *autre, uint32_t n, uint8_t *octet);

/* Sonde PIT — 19 champs du canal t, dans l'ordre de Models.pit.Probe() côté C#.
 * `pit` est une globale de pit.c et le harnais est lié avec : on lit l'arbre
 * vendoré, on ne l'instrumente pas. */
#define H_PIT_PROBE_N 19
void h_pit_probe(int t, uint64_t *out);

/* Sonde haut-parleur (M9) — 9 champs, dans l'ordre de
 * Sound.sound_speaker.Probe() côté C#. Le dernier est une empreinte cumulative
 * du son produit : c'est la seule voix du chemin audio dans le diff, et elle ne
 * vaut que si elle a bougé (voir h_speaker_probe dans harness.c). */
#define H_SPEAKER_PROBE_N 9
void h_speaker_probe(uint64_t *out);

/* --- disquette (M6) ---------------------------------------------------------
 * Pendant de discfns[] (fdd.c:6) : pc.c le remplit depuis argv AVANT initpc, et
 * resetpchard le consomme (pc.c:367-368). À appeler avant h_boot. NULL ou "" :
 * lecteur vide. Les types de lecteur sont posés par h_boot (5,25" DD, comme
 * pc.cs) et ne se configurent pas. */
void h_set_discfn(int drive, const char *fn);

/* --- configuration machine (M8) ---------------------------------------------
 * Taille RAM en Ko, pendant de mem_size (mem.c:102) que loadconfig pose AVANT
 * initpc chez PCem (pc.c:694). À appeler avant h_boot ; sans appel, h_boot garde
 * 640, la valeur qu'il portait en dur et que toutes les mesures de
 * VERIFICATION.md supposent.
 *
 * C'est le PATRON de tout paramètre machine configurable : l'outil de diff lit la
 * configuration UNE fois, puis pousse le même scalaire des deux côtés — ici, et
 * dans mem.mem_size côté C#. Laisser chaque côté relire le fichier ouvrirait la
 * porte à deux résolutions de chemin divergentes, soit exactement la panne que la
 * comparaison différentielle existe pour attraper. */
void h_set_mem_size(int kb);

/* Type de lecteur, pendant de fdd_set_type (fdd.c:176) que loadconfig pose AVANT
 * initpc (pc.c:776-777). Sans appel, h_boot garde 1 — 5,25" DD, le lecteur du
 * 5150. Le type gouverne max_track et les drapeaux de densité (fdd.c:38-64),
 * donc fdd_seek et fdd_can_read_medium : un type divergent fait diverger la
 * recherche de piste avant toute lecture de secteur. */
void h_set_drive_type(int drive, int type);

/* Pendant de bpb_disable (disc_img.c:22). Force img_load à deviner la géométrie
 * depuis la taille du fichier au lieu de lire le BPB. */
void h_set_bpb_disable(int v);

/* --- machine (M10) ----------------------------------------------------------
 * Le romset, valeur de l'énumération ROM_* (ibm.h:164+) : 0 = IBM PC 5150,
 * 1 = IBM XT 5160. À appeler AVANT h_boot — c'est loadbios() qui le lit
 * (mem_bios.c:63), et il tourne à l'intérieur.
 *
 * C'est un SCALAIRE et non un nom de machine, délibérément : le harnais ne lie
 * ni pc.c ni model.c, donc il n'a pas de models[] à indexer ni de
 * model_getromset() à appeler. Côté C# le romset DÉRIVE du nom via loadconfig ;
 * l'outil de diff pousse ici la valeur déjà résolue. Même doctrine que
 * h_set_mem_size : une seule lecture de la configuration, deux poussées. */
void h_set_romset(int r);
/* G3.0 — les chemins du CMOS (nvr.c:33-54), identiques a ceux du C#. */
void h_set_nvr_paths(const char *nvr, const char *nvr_default);

/* --- processeur (M16) -------------------------------------------------------
 * Le fabricant et l'INDICE dans la table de CPU de la machine, pendant des clés
 * `cpu_manufacturer` et `cpu` (pc.c:653-654). À appeler AVANT h_boot, comme le romset :
 * h_boot fait tourner le vrai cpu_set() de PCem avec eux, et refuse (rend 0) un indice
 * hors de la table ou une table qui contredit le coeur de h_set_core. */
void h_set_cpu(int manu, int n);

/* Le budget d'une tranche, `cpu_get_speed() / 100` (pc.c:473) — celui que h_runpc
 * emploie, par la même fonction. */
int h_slice_budget(void);

/* L'EMPREINTE CPU — H_CPU_FP_N champs dans l'ordre de CpuFingerprint.Csharp() côté C# :
 * tout ce que cpu_set() et setpitclock() posent et que le temps de l'invité lit. À
 * prendre juste après h_boot. Les champs au-delà du dernier nommé valent zéro. */
#define H_CPU_FP_N 48
void h_cpu_fingerprint(uint64_t *out);

/* --- x87 (G4.0) -------------------------------------------------------------
 * Le coprocesseur, valeur de l'énumération FPU_* (cpu.h:72) : 0 = aucun, 1 = 8087,
 * 2 = 287, 3 = 287XL, 4 = 387, 5 = intégré. Pendant de la clé `fpu` (pc.c:655-656).
 * À appeler AVANT h_reset ou h_boot : cpu_set() en tire hasfpu (cpu.c:184) et choisit
 * les tables d'échappement (:276-300). Sans appel : 0. PCem ne le pose jamais hors de
 * loadconfig — c'est ce geste explicite qui remplace le zéro implicite. */
void h_set_fpu(int type);

/* Pose l'état x87 (pour le fuzzeur) : ST en bits bruts, MM[].q, MM_w4, tags, TOP, mot
 * d'état et mot de contrôle. x87_pc_* et x87_op_* restent tels que h_reset les a mis. */
void h_setfpu(const uint64_t st[8], const uint64_t mm[8], const uint16_t mm_w4[8],
              const uint8_t tag[8], int top, uint16_t npxs, uint16_t npxc);

/* Sondes de PARITÉ de G4.0 — hors du cœur, compilées avec les MÊMES drapeaux que lui.
 *
 * h_libm : la fonction n de la libm, ou l'une des expressions de x87_ops_misc.h
 * verbatim (H_LIBM_*). Rend les bits du résultat.
 * h_conv : les conversions que x87_ops_*.h écrivent en C, sur un double : (int64_t),
 * (uint64_t), (int32_t), (int16_t). Rend les bits du résultat, étendus à 64.
 * h_fpu_arith : une opération sous fesetround, dans une unité compilée comme 386.c
 * (-O2, sans -frounding-math) — la question de G4.0 est de savoir si GCC honore le mode.
 * Ce n'est PAS le chemin de l'oracle : celui-là se mesure par h_step, sur le vrai
 * handler (voir X87Parity.cs). */
enum { H_LIBM_SIN, H_LIBM_COS, H_LIBM_TAN, H_LIBM_ATAN2, H_LIBM_LOG, H_LIBM_POW, H_LIBM_SQRT,
       H_LIBM_FMOD, H_LIBM_FLOOR, H_LIBM_CEIL, H_LIBM_F2XM1, H_LIBM_FYL2X, H_LIBM_FYL2XP1,
       H_LIBM_FSCALE, H_LIBM_COUNT };
uint64_t h_libm(int n, uint64_t a, uint64_t b);
enum { H_CONV_I64, H_CONV_U64, H_CONV_I32, H_CONV_I16, H_CONV_COUNT };
uint64_t h_conv(int n, uint64_t a);
uint64_t h_fpu_arith(int op, int mode, uint64_t a, uint64_t b);

/* --- vidéo (M15) ------------------------------------------------------------
 * La carte, valeur de l'énumération GFX_* (ibm.h:274-318) : 0 = CGA, 13 = VGA,
 * 4 = Trident 8900D, 42 = Trident 9000B (M19).
 * À appeler AVANT h_boot, qui fait le device_add. Sans appel : CGA, le défaut de
 * PCem quand la clé gfxcard est absente (pc.c:660-664). */
void h_set_gfxcard(int g);

/* Sonde VGA — H_VGA_PROBE_N champs dans l'ordre de Video.vid_svga.Probe() côté C#.
 * Tout à zéro sans carte svga. Hachages FNV-1a pour les tableaux (VRAM, registres,
 * palettes, buffer32), valeurs brutes pour les scalaires. */
#define H_VGA_PROBE_N 163
/* G8.1 — la sonde du son : 9 du haut-parleur, 6 par OPL (×2) ; G8.2 : +20 du DSP et du mélangeur. */
#define H_SOUND_PROBE_N 41  /* 64 à M15 ; +11 champs svga et +11 de la tvga_t à M19 ; +16 de la GD5429 à G7.1 ; +20 de la Trio64 à G7.3 ; +21 de la M24 à G1.1 ; +20 du PC1512 à G1.2 */
void h_vga_probe(uint64_t *out);

/* M21 — injecte un mouvement de souris (mickeys x, y, z, boutons), pendant de mouse_poll. */
void h_mouse_poll(int x, int y, int z, int b);

/* G10.1 — le type de manette, avant h_boot ; l'état de la manette n (0 à 3) : plat_joystick_nr
 * (0 : débranchée), axes 0 à 2, les 32 boutons en masque, chapeau 0 (-1 : au repos). */
void h_set_joystick_type(int t);
void h_joy_set(int n, int nr, int x, int y, int z, uint32_t boutons, int pov);

/* G10.3 — le moteur d'images de CD, appelé hors de toute machine (harness_cdrom.cpp, porte
 * cdimage-check). h_cd_reset remet à zéro l'état du pilote et pose atapi à NULL ; h_cd_fin le rend
 * au harnais. h_cd_call appelle l'entrée op de la table atapi (rangs de ide_atapi.h:8-26) sur
 * buf + off. h_cd_state : 12 scalaires et 10 entiers par piste, image_path et mcn. */
void h_cd_reset(void);
void h_cd_fin(void);
int h_cd_open(const char *path);
void h_cd_close(void);
void h_cd_null_open(void);
void h_cd_set_drive(int drive, int old);
int64_t h_cd_call(int op, int64_t a, int64_t b, int64_t c, int64_t d, uint8_t *buf, int off);
void h_cd_audio_callback(int16_t *out, int len);
int h_cd_state(int64_t *v, int max, char *path, int pathmax, char *mcn, int mcnmax);

/* G10.4 — le lecteur de CD-ROM de la machine, posé avant h_boot comme loadconfig le pose (pc.c:702-711,
 * :780-781) : cdrom_drive (-1 ou CDROM_IMAGE), cdrom_channel (-1 à 3), cdrom_path (moins de 1 024 octets),
 * cd_speed (l'une des dix-huit vitesses), cd_model (un nom de configuration de la table) — le C# a validé
 * chacun. h_ide_type : le type de l'unité d (0 rien, 1 disque, 2 CD) ; h_cd_driver : le pilote posé
 * (0 aucun, 1 le lecteur vide, 2 l'image). */
void h_set_cdrom(int drive, int channel, const char *path, int speed, const char *model);
void h_set_zip(int channel, const char *path);   /* G10.6 */
int h_ide_type(int d);
int h_cd_driver(void);

/* La VRAM de la carte svga, NULL sans carte. Lue sans passer par svga_read. */
uint8_t *h_vga_vram(void);

/* --- clavier (M11) ----------------------------------------------------------
 * De quoi TAPER dans l'oracle, et donc de quoi mettre le chemin d'écriture du
 * contrôleur de disquettes sous comparaison. Sans ces deux-là, FORMAT et WRITE
 * DATA ne s'exerçaient que côté C# (§ M7.1, § M8.1) : une preuve d'usage, pas
 * une preuve de fidélité.
 *
 * h_rawinputkey écrit dans rawinputkey[] — le MÊME tableau que la pompe SDL
 * remplit côté hôte. h_kbd_process fait keyboard_poll_host() puis
 * keyboard_process(), dans l'ordre où runpc() les appelle (pc.c:490-491).
 *
 * h_runpc ne les appelle PAS : l'oracle n'a pas de couche hôte, et les y glisser
 * changerait toutes les mesures déjà consignées. C'est l'appelant qui déclenche,
 * au même point de la tranche des deux côtés. */
void h_rawinputkey(int idx, int val);
void h_kbd_process(void);

/* Pendant de pc.closepc() : les deux disc_close qui VIDENT les tampons d'écriture
 * sur les images. Sans lui, comparer deux images après un FORMAT comparerait un
 * fichier poussé à un fichier qui ne l'est pas. */
void h_closepc(void);

/* --- disque dur (M12) -------------------------------------------------------
 * Geometrie et image d'un disque, et le nom INTERNE de la carte. À appeler AVANT
 * h_boot : xebec_init lit les deux par hdd_load dès sa construction.
 *
 * `drive` est une LETTRE DE LECTEUR DOS — 0 = C:, 1 = D: — et non un numéro de
 * contrôleur. Le Fixed Disk Adapter n'en gère que deux.
 *
 * La géométrie n'est pas libre : xebec_set_switches (mfm_xebec.c:725) exige
 * 17 secteurs par piste et l'un des quatre couples (306,4) (612,4) (615,4)
 * (306,8), faute de quoi l'unité est présentée comme type 0 et le POST diverge. */
void h_set_hdd(int drive, const char *fn, int spt, int hpc, int tracks);
/* Le nom INTERNE : "mfm_xebec", "dtc5150x", "mfm_at" depuis G5.0, ou rien. */
void h_set_hdd_controller(const char *name);

/* Sonde disquette — H_DISC_PROBE_N globales de disc.c et fdc.c, dans l'ordre de
 * Floppy.fdc_c.Probe() côté C#. L'instance `fdc` est static dans fdc.c, donc hors
 * de portée sans l'inclure ; les globales suffisent à nommer le champ divergent. */
#define H_DISC_PROBE_N 20
void h_disc_probe(uint64_t *out);

/* Journal d'ecritures de la derniere instruction. Voir harness_stubs.c. */
void h_wlog_reset(void);
int h_wlog_count(void);
uint32_t h_wlog_get_addr(int i);
uint8_t h_wlog_get_val(int i);

/* Pointeur direct sur la RAM de l'oracle (1 Mo), pour les cas où le C# doit
 * la comparer en entier. */
uint8_t *h_ram(void);

/* Version du contrat. Incrémentée dès que h_state change de forme, pour qu'un
 * .so périmé échoue bruyamment au lieu de marshaler du charabia. */
/* 2 depuis M8 : les setters de configuration (h_set_mem_size, h_set_drive_type,
 * h_set_bpb_disable) s'ajoutent au contrat. Un .so bâti avant ne les exporte pas,
 * et le C# doit le dire au lieu de tomber sur un symbole absent. */
/* 6 depuis M12 : h_set_hdd et h_set_hdd_controller s'ajoutent au contrat. */
/* 7 depuis le jalon 286 : h_state porte le cache descripteur des six segments —
 * limit, limit_raw, limit_low, limit_high, access, access2, checked. Le vecteur
 * change de TAILLE, donc un .so périmé marshalerait du charabia en silence. */
/* 8 depuis le jalon 286, deuxième moitié : les quatre descripteurs système (gdt,
 * ldt, idt, tr) et cr0, cr2, cr3, use32, stack32, cpl_override. Même raison, même
 * moment — LGDT, LIDT, LLDT, LTR et LMSW n'écrivent que là. */
/* 9 depuis A2.0 : h_set_core / h_get_core s'ajoutent au contrat. h_state ne change pas
 * de forme, mais un .so bâti avant ne les exporte pas et le C# doit le dire. */
/* 11 depuis A2.2a : h_fastreadl et h_pccache s'ajoutent au contrat — le chemin de
 * fetch de exec386, expose pour etre diffe avant que exec386 existe cote C#. */
/* 10 depuis A2.1 : h_state porte les quatre drapeaux paresseux — flags_op, flags_res,
 * flags_op1, flags_op2. Le vecteur change de TAILLE. */
/* 12 depuis A2.2d : h_state porte prefetch_bytes et prefetch_prefixes, et
 * h_prefetch_reset s'ajoute au contrat. */
/* 13 depuis le bloc C : h_seg_clear_residue s'ajoute au contrat. Le vecteur ne
 * change PAS de taille — c'est une fonction, pas un champ. */
/* 14 depuis le bloc C etape 6a : h_step_trace s'ajoute au contrat, pour que les deux
 * phases du boot-diff empruntent le MEME pas. Le vecteur ne change pas de taille. */
/* 15 depuis C7a : sept champs du mode protege entrent dans h_state — abrt_error,
 * intgatesize, cgate16, cgate32, optype, oldcpl, cur_status — et h_wlog_max
 * s'ajoute au contrat. Le vecteur change de TAILLE, contrairement aux trois bumps
 * precedents. */
/* 16 : h_set_trace_notsc s'ajoute au contrat. Le vecteur ne change pas de taille. */
/* 17 depuis M15 : h_set_gfxcard, h_vga_probe et h_vga_vram s'ajoutent au contrat, et
 * l'oracle lie vid_vga.c, vid_svga.c et vid_svga_render.c. Le vecteur ne change pas
 * de taille. */
/* 18 depuis M16 : h_set_cpu, h_slice_budget et h_cpu_fingerprint s'ajoutent au
 * contrat, et h_boot fait tourner le vrai cpu_set(). Le vecteur ne change pas de
 * taille. */
/* 19 depuis M19 : les Trident. h_vga_probe passe de 64 à 86 champs, la temporisation
 * suit la carte (video_speed = -1) et video_is_* lisent ses drapeaux. Le vecteur
 * h_state ne change pas de taille. */
/* 20 depuis M21 : h_mouse_poll s'ajoute au contrat ; COM1, COM2 et la souris serie
 * Microsoft entrent dans l'oracle. Le vecteur ne change pas de taille. */
/* 21 depuis G2 etape D0.1 : cr4 et dr[8] entrent dans h_state. Le vecteur change de
 * TAILLE. */
/* 22 depuis G2 etape D0.2 : h_set_core accepte H_CORE_386. Le vecteur ne change pas de
 * taille. */
/* 23 depuis G2 etape D0.4 : h_setregs386 s'ajoute au contrat. */
/* 24 depuis G2 etape D0.5 : h_setsys386 et h_flags_rebuild s'ajoutent au contrat. */
/* 25 depuis G2 etape D6 : h_mmutranslate et h_mmu_perm (page-check). h_state ne change pas. */
/* 26 depuis G3.0 : h_set_nvr_paths. h_state ne change pas. */
/* 27 depuis G4.0 : l'état x87 entre dans h_state (le vecteur change de TAILLE) ;
 * h_set_fpu, h_setfpu et les sondes de parité h_libm, h_conv, h_fpu_arith s'ajoutent. */
/* 28 depuis G5.0 : h_set_hdd_controller("mfm_at") monte le Fixed Disk Adapter de l'AT
 * (mfm_at.c, enfin lié) — changement PAR LE COMPORTEMENT : un .so périmé accepterait le nom
 * et ne monterait rien. h_state ne change pas. */
/* 29 depuis G6.0 : h_set_core accepte H_CORE_486 (l'ami486, cpus_i486 seule). */
/* 30 depuis G7.1 : la GD5429 (gfxcard 19), et la sonde VGA passe de 86 à 102 champs. */
/* 31 depuis G7.3 : la Trio64 Phoenix (gfxcard 22), et la sonde VGA passe de 102 à 122 champs. */
/* 32 depuis G1.0 : h_set_core accepte H_CORE_8086 (l'Olivetti M24, cpus_8086). */
/* 33 depuis G1.1 : l'Olivetti M24 s'amorce (romset 9), et la sonde VGA passe de 122 à 143 champs (21 de la M24, champ 0 = 2). */
/* 34 depuis G1.2 : l'Amstrad PC1512 s'amorce (romset 11), et la sonde VGA passe de 143 à 163 champs (20 du PC1512, champ 0 = 3). */
/* 35 depuis G8.0 : h_opl_tables — DBOPL compilé en C++ (harness_dbopl.cpp). */
/* 36 depuis G8.1 : h_set_sndcard, h_opl_reset, h_sound_probe (H_SOUND_PROBE_N champs). */
/* 37 depuis G8.2 : la SB Pro v2 (sndcard sbprov2), la sonde du son passe de 21 à 41 champs. */
/* 38 depuis G8.3 : h_clear_device_config, h_set_device_config (les sections de device du .cfg). */
/* 39 depuis PS2.0 : h_set_mouse_type, h_mouse_probe (la souris PS/2) ; h_mouse_poll pose mouse_buttons. */
/* 40 depuis G9.0 : la MDA (gfxcard GFX_MDA) et sa sonde (champ 0 = 4, places de la M24). */
/* 41 depuis G9.1 : l'Hercules (GFX_HERCULES) et sa sonde (champ 0 = 5). */
/* 42 depuis G9.2 : l'EGA (GFX_EGA) et sa sonde (champ 0 = 6). */
/* 43 depuis G9.3 : la Tseng ET4000AX (GFX_ET4000), sondée comme les svga. */
/* 44 depuis G10.0 : LPT1/LPT2 posés (lpt_init), le port jeu sur xt_init/at_init, h_set_lpt1_device,
 * h_set_lpt_jeu_hors_service (l'interrupteur de preuve). */
/* 45 depuis le 04/10 (outils) : h_trace_errno, l'écriture refusée de la trace. */
/* 46 depuis le 04/10 (l'accélération) : h_trace_hash_value, h_raz_fin, h_mem_size, h_ram_cmp. */
/* 47 depuis G10.1 : h_set_joystick_type, h_joy_set (la manette). */
/* 48 depuis G10.2 : le XTIDE (hdd_controller « xtide », xtide.c lié). */
/* 49 depuis G10.3 : le moteur d'images de CD (harness_cdrom.cpp) — h_cd_reset, h_cd_fin, h_cd_open,
 * h_cd_close, h_cd_null_open, h_cd_set_drive, h_cd_call, h_cd_audio_callback, h_cd_state. */
/* 50 depuis G10.4 : l'ATAPI (ide_atapi.c, scsi.c, scsi_cd.c liés ; ide.c inclus par harness_ide.c) —
 * h_set_cdrom, h_ide_type, h_cd_driver ; h_boot pose le pilote CD (pc.c:291-313, :411-433). */
/* 51 depuis G10.5 : l'audio CD dans la machine — le corps du fil CD à l'échéance de sound_poll,
 * h_cd_sound_probe (H_CD_SOUND_PROBE_N champs), h_cd_audio_stop ; h_boot remet le fil à zéro. */
/* 52 depuis G10.6 : le lecteur ZIP (scsi_zip.c lié) — h_set_zip ; h_boot charge l'image, h_closepc l'éjecte. */
#define H_ABI_VERSION 53
#define H_CD_SOUND_PROBE_N 6
/* 53 (G11.0) : l'Adaptec AHA-1542C et ses disques SCSI (harness_aha.c inclut scsi_aha1540.c et scsi_hd.c) —
 * h_aha_probe (H_AHA_PROBE_N champs) ; h_boot monte « aha1542c ». */
#define H_AHA_PROBE_N 140
void h_aha_probe(uint64_t *o);
void h_cd_sound_probe(uint64_t *out);
uint32_t h_abi_version(void);

/* sizeof(h_state) tel que le compilateur C l'a disposé. Le C# l'assène contre son
 * propre Marshal.SizeOf au démarrage : un décalage de champ entre les deux côtés
 * ne produirait pas d'erreur, il produirait des comparaisons silencieusement
 * fausses — exactement le genre de panne que ce projet existe pour éviter. */
uint32_t h_state_size(void);

#ifdef __cplusplus
}
#endif

#endif /* IXTAL26_HARNESS_H */
