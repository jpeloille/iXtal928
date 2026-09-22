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

#define H_WLOG_MAX 16
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
enum { H_CORE_8088 = 0, H_CORE_286 = 1 };
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

/* Exécute exactement une instruction et rend le nombre de cycles consommés.
 * Le budget est réarmé à l'intérieur : execx86() boucle tant que cycles > 0,
 * donc on l'appelle avec de quoi faire une seule instruction. */
int h_step(void);

/* Exécute jusqu'à épuisement d'un budget de cycs cycles — la forme qu'emploie
 * runpc() (execx86(cpu_get_speed() / 100), soit 47 727 cycles par tranche de
 * 10 ms sur un XT). Sert à vérifier que h_step() ne fausse pas la comptabilité,
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
#define H_ABI_VERSION 12
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
