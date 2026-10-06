# D2 — Contre-lecture : le x87 en mode matériel (G13)

> Contre-lecture adverse de `D2-x87.md`, le 6 octobre 2026, au commit `bc609ce` (arbre de travail :
> `iXtal26.csproj` modifié ; `tools/perfbanc/` et `iXtal26/Docs/arbre-classes-cpu.html` non suivis). LECTURE
> SEULE sur le dépôt : rien n'a été construit, lancé ni mesuré dans l'émulateur. Deux calculs hors dépôt, en
> Python sur des rationnels exacts (les constantes à 64 et 53 bits ; la fréquence du double arrondi), dans le
> répertoire de travail de la session (`work/consts.py`, `work/dblround.py`).
> Lu : TRANSCRIPTION.md § Règles (R1, R2, R4, R8, R9), PLAN.md § G4 et § G13, PLAN-G4.md, VERIFICATION.md
> § G4.0 à § G4.7, PCEM_BUGS.md (PB-05, PB-48, PB-52 à PB-70, PB-53), D7 (§ 1.4, 4.3, 6, 8, 9), le C de
> `pcem-dev/` et le C# que D2 cite, puis les sources extérieures de l'annexe.
> Niveaux, comme D2 : **documenté** (source primaire, ou le code lui-même), **déduit**, **inconnu**.
> « D2 § n » renvoie au rapport contre-lu ; [A1]…[A20] à son annexe A ; [C1]…[C23] à l'annexe de ce fichier.

---

## 0. Le verdict

1. **Le fond tient.** PCem garde la pile en `double` ; les vingt entrées, leurs lignes de C et de C#, les neuf
   PB sans marqueur canonique, l'erreur de `x87_ops.cs:15`, le classement (d) de PB-53, les treize voisins (à
   des nuances près) et les constantes de l'annexe B sont exacts. La grille documentaire (387 PRM annexe C,
   287 PRM, Numerics Supplement, SDM, AP-578) est la bonne.
2. **Ce qui change la décision.**
   - **Les tailles.** La voie A est sous-estimée d'un facteur 2,5 à 5 (≈ 6 000 à 8 000 lignes de C#, pas 1 500
     à 2 500) ; la voie B de 25 à 100 % (≈ 10 000 à 20 000). À dessin égal, B coûte ≈ 2 à 3,5 fois A, pas
     3 à 7 fois (§ 2, K1-K2).
   - **La licence.** Les noyaux transcendants et FPREM de Bochs, que B4 transcrirait via 86Box, portent la
     licence SoftFloat-2b à clause d'indemnité, que la FSF tient pour incompatible avec la GPLv2 ; QEMU a
     quitté 2b pour 2a pour cette raison. Le dépôt est GPL-2.0-only : B4 telle qu'écrite est bloquée (K3).
   - **Stockage contre arithmétique.** PB-55 n'exige pas un cœur de 80 bits, mais un STOCKAGE de 80 bits ;
     la distinction ouvre une voie intermédiaire que D2 n'examine pas (K6, § 3 A1).
   - **« PB-48 exact en double »** ne vaut que sous PC = 53. Sous le mot de FNINIT (037Fh, PC = 64), une
     expression de deux opérations rangée en m64 diffère du silicium au dernier bit dans ~25 % des tirages
     (simulation) : c'est PB-56, et c'est le cas ordinaire (K4, K5).
   - **Le domaine d'accord** est faisable, mais pas en comparant `npxs` tel quel : le silicium pose PE (et C1)
     à presque chaque opération inexacte, PCem jamais (K15).
   - **MCPDIAG n'est pas un témoin du « bit Intel »** : son test des transcendantes échoue aussi sur du
     silicium (K9).
   - **Les deux contradictions d'Intel existent, mais ne pèsent pas pareil** : pour les constantes du
     8087/287, quatre textes d'Intel (dont le 387 PRM lui-même, § 4.7) contre la seule annexe C ; pour FPATAN,
     le SDM vol. 2 donne raison au 287 PRM. Et trois affirmations de D2 tombent à la relecture : FINIT efface
     les pointeurs « sur 387+ » (pas sur le 387), le 486 lève #GP sur un opérande à cheval (Intel se
     contredit), le mot de FINIT du 8087 « 03FFh » (non écrit) (K20).
   - **Sept défauts voisins de plus** (et cinq précisions sur des défauts connus), dont un observable EN MODE
     PCEM dès aujourd'hui : FSTENV ne masque pas les exceptions (§ 3 A4).
3. **Recommandation** (§ 5) : la voie B, mais pas telle qu'écrite — le cadre X0, une A1 réduite aux PB qui ne
   touchent qu'une poignée de gestionnaires, l'acheminement des exceptions avec ZE seule, puis B1 (le noyau
   extF80, hors machine, vérifié par TestFloat) en éclaireur mesuré ; les transcendantes d'abord en double
   (« B allégée »), des noyaux de 64 bits seulement depuis une source compatible ou écrits. Pas de voie A
   complète : A2 et A4 seraient jetées par B.

---

## 1. Confirmé

- **C1. La représentation.** `cpu_state.ST` est un `double` (`pcem-dev/includes/private/cpu/x86.h:93` ;
  C# `iXtal26/Cpu/x86.cs:114`) ; le 80 bits n'existe qu'à `x87_ld80` / `x87_st80` (`x87_ops.h:86-150`) et
  dans l'image FSAVE de TAG_UINT64 (`:152-175`). PLAN-G4.md:18-22 le disait. — documenté (code).
- **C2. Les lignes de C des vingt entrées**, relues une à une : `x87_ops.h:17-30` (x87_div), `:32`, `:60-83`,
  `:86-115`, `:117-150`, `:189-217`, `:297-308`, `:884-893` et `:923-932` (DF /4) ; `x87_ops_arith.h:12-16`,
  `:154-166`, `:180-193` ; `x87_ops_loadstore.h:36-98`, `:141-200`, `:429-485` ; `x87_ops_misc.h:24-32`,
  `:49-62`, `:76-97`, `:99-150`, `:166-353`, `:451-481`, `:483-552`, `:554-612`, `:634-675`, `:688-752`,
  `:754-778`, `:826-869` ; `x87.c:22-23`, `:97` ; `8087.h:12-35`, `:86`. — documenté (code).
- **C3. Les temps** de PB-57 (fadd contre fcom : 85/45 sur 8087 et 287, 28/24 sur 387, 14/4 sur 486) et de
  FBLD (300, 300, 270, 86) : `x87_timings.c:8, 15, 11` (8087), `:82, 89, 85` (287), `:155, 162, 158` (387),
  `:228, 235, 231` (486). — documenté (code).
- **C4. Les sites C# et les marqueurs** que D2 cite : `x87_ops_arith.cs:1139` (PB-57), `:1167` (PB-58) ;
  `x87_ops_misc.cs:58` (61), `:119, 131` (67), `:509` (64), `:523` (63), `:592` (66), `:652, 756, 795, 809`
  (68), `:697, 719` (65) ; leurs doubles dans `x87_ops_808x.cs` (1708, 1736, 1987, 2048, 2060, 2438, 2452,
  2521, 2581, 2626, 2648, 2685, 2724, 2738) ; PB-69 `808x.cs:3229` ; PB-53 `x87_ops_loadstore.cs:178, 211` et
  `x87_ops_808x.cs:178, 211` ; les deux `CHECK_WRITE` de PB-54 à `x87_ops_loadstore.cs:479, 496`. — documenté
  (`grep`).
- **C5. Les neuf PB sans marqueur canonique** : 48, 52, 54, 55, 56, 59, 60, 62, 70 (`grep -rn "pcem bug"
  --include=*.cs iXtal26/` n'en rend aucun site) ; `x87_ops.cs:12-15` décrit le repli d'exposant de
  `x87_ld80` sous « (PB-52) » : c'est PB-55. — documenté.
- **C6. PB-53 en (d).** La globale `tempc` (`808x.c:37`, déclarée `extern` par `x86_flags.h:3`) n'est lue que
  par `setadc*`/`setsbc*` (`x86_flags.h:562-598`) et par les ADC/SBB du 808x (`808x.c:779-874`, `:1409-1512`,
  `:2056-2190`) ; chaque chemin l'écrit avant de la lire (`x86_ops_arith.h:5-6`, et `:676`, `:684` pour le
  groupe 80-83 ; `808x.c:1249` à `opcodestart`) ; les drapeaux paresseux d'ADC/SBC ne lisent que
  `flags_op1/op2/res` (`x86_flags.h:209-233`, `:299-319`, `:344-361`) ; RCL/RCR octet l'écrivent avant de la
  lire (`x86_ops_shift.h:35, 57`) ; `tools/oracle/harness.c` ne la capture pas. Aucun chemin observable. —
  documenté (code). Une nuance en § 3 A4-l.
- **C7. Les voisins de D2 § 7 existent dans le C** : FXTRACT (D9 F4 `ILLEGAL`, `x87_ops.h:356` et `:394`) ;
  FBSTP qui tronque (`floor(fmod(...))` sur |ST(0)|, ni borne ni NaN, `x87_ops_loadstore.h:150-164`) ; FIST m16
  et m32 par `(int16_t)`, `(int32_t)` d'un int64 (`:46, 60, 75, 92` ; `:282, 296, 311, 328`) ; FRNDINT
  `(double)x87_fround` (`x87_ops_misc.h:703-714`) ; FSCALE `(int64_t)ST(1)` (`:716-728`) ; l'image FSAVE de
  TAG_UINT64 et son étiquette 10 (`x87_ops.h:152-161`, `x87.c:37-38`) ; FNSTSW AX dans la table que `8087.h:86`
  recompile (`x87_ops.h:916`) ; l'offset de 16 bits du 8087 (`8087.h:19-35`) ; FINIT qui pose `npxs = 0`
  (`x87_ops_misc.h:57`) ; `x87_reset` vide (`x87.c:97`, déjà noté par PLAN-G4.md:148) ; aucun port F0h/F1h (le seul `io_sethandler(0x00f0, …)` est
  `fdc_add_pcjr`, `fdc.c:1263-1268`) ; C1 jamais posé en « arrondi vers le haut » ; FLD m32 d'un SNaN rendu
  silencieux par la conversion (`x87_ops_loadstore.h:487-501`). — documenté (code). Leurs vrais comportements
  sont confirmés par les manuels : FXTRACT (SDM § 22.18.7.12, et vol. 2 : « an exponent value of –∞ is stored in
  register ST(1) »), FINIT et C3-C0 (§ 22.18.2.1), le RESET du 387 (387 PRM § C.1), FLD d'un SNaN (§ 22.18.7.11 :
  « The 16-bit IA-32 math coprocessors do not raise an exception when loading a signaling NaN »), FNSTSW AX
  propre au 287 (relevé aussi par 86Box, #4518 : « DF E0 which is STSW AX, added on 287 ») [C3, C14].
  Compléments en § 2 et § 3.
- **C8. Les constantes de l'annexe B.1**, recalculées en rationnels exacts (Machin pour π, `Decimal.ln` à 120
  chiffres) : les 64 bits tronqués, le bit suivant, les quatre arrondis à 64 et à 53 bits et la valeur de PCem
  sont exacts ; le test QEMU `fldcst` attend les mêmes (L2T …8AFE en N/D/Z, …8AFF en U ; L2E …F0BC en N/U,
  …F0BB en D/Z) [C10]. Aucune des cinq constantes n'est près d'un milieu à 53 bits (fraction résiduelle 0,09 à
  0,95) : en voie A, la valeur RANGÉE en m64 égale celle du silicium dans les quatre modes. — déduit (calcul).
- **C9. Ce que G4 a mesuré** (D2 § 2) : x87-parity (`fesetround` exact à 100 % hors gestionnaire ; FADD seul
  dans le vrai gestionnaire), x87-cases 2 649 cas, X87BANC 297/98 tics, QBASIC sans ESC, SST sans x87 :
  VERIFICATION.md § G4.0 à § G4.7 (l. 3919-4420) ; l'angle mort déclaré : PLAN-G4.md:123-124. — documenté.
- **C10. La base du mécanisme** : `cpu_set` pose les seize tables `ops_fpu_*` (`cpu.cs:321-358`) ; les huit ESC
  du 8088 indexent `ops_808x_fpu_*` (`808x.cs:3225-3289`, C `808x.c:3304-3366`) ; les générateurs rendent leurs
  fichiers à l'octet (`tools/x87gen/README.md`). — documenté.
- **C11. Les sources Intel que la contre-lecture a relues disent ce que D2 leur fait dire** : SDM vol. 2, FLD
  (« #IA Source operand is an SNaN. Does not occur if … FLD m80fp », « #D … Does not occur if the source operand
  is in double extended-precision format ») [C5] ; FCOM (« C1 Set to 0 », #IA pour tout NaN) [C6] ; les
  constantes (arrondies selon RC, « #P … not generated », et « When the RC field is set to round-to-nearest, the
  FPU produces the same constants that is produced by the Intel 8087 and Intel 287 math coprocessors ») [C7] ;
  80386 PRM § 9.8.9 (INT 9 sur la « middle portion », mode protégé) et § 14.7 (exception 13, ou 12 pour SS,
  quand un opérande franchit 0FFFFh) [C8]. — documenté. Les citations du 387 PRM, du 287 PRM, du Numerics
  Supplement et d'AP-578 : § 2, K20.
- **C12. Les références de vérification existent**, avec leurs chiffres : TestFloat 3e (extF80,
  `-precision32/64/80`, six modes, cinq drapeaux ; licence BSD-3 de l'Université de Californie) [C9] ; les
  treize tests QEMU nommés par D2 [C10] ; IEEETEST (Cyrix, « a port of FPTEST » de Coonen ; la disquette Cyrix
  3.3 est sur archive.org) et les chiffres de Juffa, à l'unité (31 235/0 ; 23 412/7 823 et les quinze lignes
  de l'annexe B.2) [C11] ; PCTRL/RCTRL (les sept sorties, exactes ; sources Turbo Pascal 6.0 en annexe A de
  Juffa ; « The 8087 and 287 pass the RCTRL and PCTRL tests ») [C11] ; le 287XL « contain the internals of a 387
  coprocessor » [C11] ; MCPDIAG (« Intel Math CoProcessor Advanced Diagnostics », sur archive.org) [C12] ;
  l'option « Softfloat FPU » de 86Box (v4.0 du 26/08/2023 ; 8087 et SoftFloat 3e en v4.2) et le ticket #4518
  [C13, C14]. — documenté. Leur PORTÉE est corrigée en K9.
- **C13. Les comptes de 86Box** : 616 + 887 + 545 + 130 + 1 408 + 137 + 450 + 40 = 4 213 lignes, exacts au
  commit 8b7cc386 du 06/10/2026 ; les quatre recoupements de la voie `double` (NMI du 8087 sous IEM dans
  `x87_div` ; `new_ne` → #MF du 486 ; FXAM par `fpclassify` ; FNSTSW AX avec TOP) existent [C14]. — documenté.
  Dates et portée : K9.
- **C14. Les autres références existent** : Shirriff 2020 et 2026 (FPTAN en rapport Y/X, CORDIC + Padé) et
  `a-mcego/granite` sous GPL-3.0 [C17] ; DOSBox-X PR #6612 « Unify FPU cores and improve x87 accuracy »,
  27/09/2026, fermée [C18] ; MAME `i8087.cpp` (BSD-3) dresse lui-même sa liste de manques (pointeurs, FPREM,
  exceptions) [C19]. — documenté.
- **C15. PB-69** : le chemin NMI du 8088 existe et ne sert jamais (`808x.cs:3793-3806` ; rien ne pose `nmi = 1`
  sur le 8088) ; le masque A0h est transcrit (`Models/nmi.cs:15`) ; hors AT, `picint` jette `1 << 13`
  (`Models/pic.cs:341-357`). — documenté (code).
- **C16. PB-05** : l'acquittement de l'IRQ13 (esclave, indice 5) efface le bit 5 du maître (PCEM_BUGS.md:112-125) :
  le couplage que D2 signale pour l'acheminement de PB-59 est réel. — documenté.
- **C17. R4** (TRANSCRIPTION.md:52-59) interdit génériques et interfaces : le `double` et le 80 bits ne peuvent
  pas partager leurs gestionnaires (D2 § 9.6), c'est exact. R4 permet en revanche `delegate` : une instanciation
  unique pour le 386 et le 8088 (accès mémoire par `delegate`) est permise, ce que D2 n'envisage pas (K1, K2). —
  documenté.

---

## 2. Corrigé

- **K1. La taille de la voie A** (D2 § 4.2 : « 1 500 à 2 500, en majorité générées »). Les fichiers que ces
  variantes recopieraient pèsent déjà : `x87_ops_arith.cs` 1 384 lignes (88 gestionnaires), `x87_ops_misc.cs`
  980 (55 fonctions), `x87_ops_loadstore.cs` 593 (34), et `x87_ops_808x.cs` 2 909 (les 177 recopiés :
  1 360 + 570 + 956). PB-48 touche tous les gestionnaires arithmétiques, PB-54 tous les gestionnaires mémoire,
  PB-59 (pile vide ou pleine, IE) presque tous, dans les deux instanciations. Sous l'option C, avec des fichiers
  générés intacts et des variantes générées gestionnaire par gestionnaire (ce que D2 décrit : « variantes de
  gen43/gen44 »), la voie A recopie la quasi-totalité des gestionnaires : ≈ 5 000 à 6 000 lignes générées, plus
  ≈ 1 000 à 1 500 lignes d'aides écrites (arrondi dirigé de −, ×, ÷, √ ; réponses masquées ; acheminement ;
  pointeurs ; NaN ; projectif ; FPREM ; BCD ; bornes) : **≈ 6 000 à 8 000 lignes de C#**. Avec une
  instanciation unique (accès mémoire par `delegate`, que R4 permet), ≈ 3 500 à 4 500. Un aiguillage par ESC
  qui contrôlerait la pile avant d'appeler le gestionnaire de PCem réduirait encore le compte, mais ce n'est
  pas le dessin de D2. — déduit (`wc -l`).
- **K2. La taille de la voie B** (D2 : 8 000 à 10 000, « plus que tout G4 (≈ 3 750 lignes de C) »).
  (i) L'unité : G4 a produit 7 708 lignes de C# (`wc -l iXtal26/Cpu/x87*.cs`) pour 3 652 lignes de C ; mettre
  du C# en face de C grossit l'écart. (ii) Le noyau : le sous-ensemble de SoftFloat 3e qu'il faut (60 fichiers :
  add, sub, mul, div, sqrt, rem, roundToInt, comparaisons, conversions i32/i64/f32/f64, et leurs aides de 128
  bits) fait 5 120 lignes brutes, plus 882 pour la spécialisation `8086`, soit 2 390 lignes vives au compte de
  R2 (en-têtes de licence et commentaires ôtés) [C9, compté sur l'arbre de Berkeley] : avec les ajouts propres
  au x87 (FPREM partiel, FXTRACT, FSCALE, BCD, i16, formats non canoniques), les 3 000 à 4 500 lignes de D2
  sont plausibles. (iii) Les gestionnaires, eux, sont sous-estimés : ceux de 86Box font 4 213 lignes, plus
  `x87.c` (553) et `x87.h` (246) que D2 ne compte pas, avec une macro `sf_FPU` instanciée 8 fois — 191
  gestionnaires (177 pour un 286/386, ≈ 112 pour le 8087) [C14]. Au rapport mesuré en G4 (3 460 lignes de C,
  macros comprises, pour 4 317 de C# dans l'instanciation du 386 : ×1,25), ≈ 6 300 à 8 000 lignes de C# pour
  une instanciation, pas 3 500 à 4 500. (iv) Les transcendantes et FPREM de Bochs font ≈ 1 860 lignes [C14],
  mais sont bloquées (K3). Somme : **≈ 10 000 à 14 000 lignes** avec une instanciation partagée, **≈ 14 000 à
  20 000** avec une seconde instanciation `_808x` générée comme en G4. — déduit. À dessin égal (une ou deux
  instanciations des deux côtés), B coûte donc ≈ 2 à 3,5 fois A (K1), pas 3 à 7 fois comme le disent les chiffres
  de D2 ; et les deux estimations de D2 sont basses.
- **K3. La licence des extensions de Bochs** (D2 § 9.7 : « licence de SoftFloat 2 avec clause d'indemnité … à
  inscrire dans `THIRD_PARTY_NOTICES.md` »). Insuffisant. Dix fichiers de `src/cpu/softfloat3e/` de 86Box
  (`fsincos.cc`, `fpatan.cc`, `fyl2x.cc`, `f2xm1.cc`, `fprem.cc`, `poly.cc`, `fpu_constant.h`,
  `softfloat-helpers.h`, `softfloat-specialize.c/.h`) portent la licence « Release 2b » : « USE OF THIS SOFTWARE
  IS RESTRICTED TO PERSONS AND ORGANIZATIONS WHO … EFFECTIVELY INDEMNIFY JOHN HAUSER AND THE INTERNATIONAL
  COMPUTER SCIENCE INSTITUTE … » [C14] ; la copie de MAME (`3rdparty/softfloat3/bochs_ext/fsincos.c`) et
  Bochs lui-même aussi [C19]. La FSF juge cette licence incompatible avec la GPLv2 (« the FSF says the license
  is incompatible with GPLv2 », qemu-devel, 11/04/2013) et QEMU a rebasé son SoftFloat de 2b sur 2a, puis
  relicencié, pour cette raison (« relicense QEMU softfloat from 2b to 2a », 11/2014 ; en-tête de
  `fpu/softfloat.c` : « derived from release 2a ») [C20]. Le dépôt est GPL-2.0-only : transcrire ces noyaux
  (B4) ou la spécialisation de Bochs, c'est importer ce problème. Le cœur SoftFloat 3e d'Hauser (BSD-3) et SA
  spécialisation `8086` sont compatibles [C9] ; `fpu_trans.h` et `poly.h` sont LGPL-2+, compatibles. —
  documenté (en-têtes) ; l'avis de la FSF : documenté de seconde main.
- **K4. « PB-48 et PB-60 se corrigent exactement en double »** (D2 § 0.1). À restreindre.
  PB-48 : l'arrondi dirigé d'une opération sur deux doubles se calcule exactement en double (TwoSum, FMA), mais
  c'est l'arrondi À 53 BITS. Le silicium arrondit à la précision de PC : 64 bits après FNINIT (037Fh). Le
  registre diffère donc dès que PC = 64. La valeur RANGÉE d'une opération isolée sous un mode dirigé coïncide
  (deux arrondis de même sens se composent : RD₅₃(RD₆₄(x)) = RD₅₃(x)) ; pas celle d'une chaîne. La référence
  `fesetround` d'x87-parity valide la sémantique double, pas le silicium. Et le reste par FMA (`FMA(−r, b, a)`,
  `FMA(−r, r, a)`) n'est exact que hors de la zone dénormale : il faut y traiter les bords, comme
  `x87_fadd_dirige` traite débordement et zéro (`x87_ops.cs:333-360`). — déduit.
  PB-60 : exact en double, sauf pour les NaN chargés par FLD m80 dont les 11 bits bas de charge diffèrent
  (perdus à la conversion) ; « tous, en voie A » vaut pour les NaN nés en double. — déduit.
- **K5. Ce que coûtent les 53 bits, en pratique** (absent de D2 § 4). Simulation sur rationnels exacts, 20 000
  tirages (a, b uniformes dans [1, 2[, c dans [−2, 2[) : un produit seul rangé en m64 ne diffère du chemin à 64
  bits que dans 0,01 % des cas ; mais a·b + c et a·b / c, évalués en registres à 64 bits puis rangés en m64,
  diffèrent du calcul en double dans **28 % et 26 %** des tirages. Sous le mot de FNINIT, la voie A s'écarte du
  silicium au dernier bit d'une expression ordinaire sur quatre, sans compter la plage d'exposant (un produit
  intermédiaire au-delà de 1,8·10³⁰⁸ reste fini sur le silicium). — déduit (simulation, pas de silicium).
- **K6. « PB-55 et PB-56 exigent un cœur de 80 bits »** (D2 § 0.1). PB-55 n'exige que le STOCKAGE de 80 bits :
  FLD m80 → FSTP m80, FRSTOR → FSAVE, les classes de FXAM des formats chargés, FBLD → FBSTP au-delà de 2⁵³ ne
  calculent rien. Exigent l'ARITHMÉTIQUE de 80 bits : PB-56 (précision, plage), les seuils OE/UE/PE de PB-59
  pour un résultat en registre, le registre de PB-48 sous PC = 64, la précision de PB-68. — déduit. Voie
  intermédiaire en § 3 A1.
- **K7. PB-59 en voie A** (D2 § 4.1 : « DE, OE, UE, PE : aux seuils du double »). DE n'a pas de seuil : il se
  lève sur un opérande dénormal DANS SON FORMAT SOURCE — m32 ou m64, détectable exactement ; en registre, un
  dénormal étendu (< 2⁻¹⁶³⁸²) est inatteignable en double [C5]. DE est exact en voie A ; OE, UE et PE ne le sont
  pas pour un résultat en registre, et la réponse démasquée d'OE/UE (exposant recalé de ±24 576) n'y est pas
  représentable. — déduit.
- **K8. Le bilan des PB fermés** (D2 § 4.2 : « 17 sur 19 » en A, « 19 sur 19 » en B). A : 14 fermés (avec les
  réserves de K4 pour PB-48, 52, 60, 66), 3 en partie (54, 59, 68), 2 pour l'essentiel ouverts (55 et 56 : seuls
  la conversion correctement arrondie et le codage des dénormaux du `double` y sont corrigés). B : 15 fermés, 4
  en partie (54 et 59 pour l'acheminement, 65 pour N, 68 pour les bits), les sous-cas (c) restant. Classer PB-55 « (b) en
  voie A » est une erreur de catégorie : la conversion correctement arrondie se vérifie parfaitement (TestFloat
  `extF80_to_f64`) ; elle est INCOMPLÈTE, pas faiblement vérifiée. — déduit.
- **K9. La portée réelle des références** (D2 § 0.4, § 3.1, § 3.3, § 8.4).
  - TestFloat 3e : la précision 32/64/80 ne vaut que pour +, −, ×, ÷, √ ; `extF80_rem` est le reste IEEE
    (FPREM1 complet), pas FPREM ; aucune transcendante ; ni i16 ni BCD (ui32, ui64, i32, i64 seulement) ; le
    contrôle bit à bit des NaN (`-checkNaNs`) est une option de `testfloat_ver`, pas de `testfloat_gen` ; et
    « SoftFloat's functions are not guaranteed to operate as expected when inputs of type extFloat80_t are
    non-canonical » : unnormaux, pseudo-NaN, pseudo-infinis, pseudo-dénormaux (PB-55, le 287) n'y ont aucune
    couverture [C9]. B1 (D2 § 8.4) promet TestFloat pour « i16 … BCD » : ces deux-là demandent nos cas. —
    documenté.
  - QEMU : `test-i386-fprem.c` n'a AUCUNE valeur attendue (« Run this on real hardware, then under QEMU, and diff
    the outputs » ; la comparaison est désactivée sur master) ; les tables de `f2xm1`, `fpatan`, `fyl2x`,
    `fyl2xp1` sont des encadrements {bas, haut} d'arrondi fidèle, « randomly generated », pas des relevés de
    silicium (« I haven't investigated how accurate hardware is ») ; seul `fprem` a un en-tête de licence, les
    douze autres relèvent du « GPL v2 or later » par défaut du fichier LICENSE de QEMU [C10]. D2 § 3.1
    (« valeurs en dur ») : vrai pour fldcst, fxam, fscale, fxtract, fbstp, pseudo-denormal, snan-convert,
    fp-exceptions ; pas pour les cinq autres. — documenté.
  - IEEETEST sur le 287 : la table de 7 823 échecs est UNE table pour « Intel 80287 run with a 80386 CPU and
    Intel 8087 », et Juffa en donne la cause : « the Intel 8087/80287 do not feature the IEEE-754 compliant
    comparison (FUCOM) and remainder (FPREM1) instructions … so IEEETEST uses the non-compliant FCOM and FPREM
    instructions on these processors » [C11]. Les comptes mêlent donc le chemin 287 du programme (sa détection,
    donc PB-70, et les instructions du 387 que PCem exécute sur le 287, § 3 A4-c), FCOM/FPREM et l'arithmétique ;
    et Juffa ne donne que des comptes par opération, pas la liste des cas échoués : des comptes égaux ne
    prouveraient pas des résultats égaux. C'est une vérification faible (b), pas « la seule référence de silicium
    chiffrée pour le 287 » qui porterait B5 (D2 § 0.4, § 8.4). — documenté (la table) ; déduit (la portée).
  - MCPDIAG : son test des transcendantes échoue sur DOSBox-X, sur 86Box avec SoftFloat 2 et 3e, « and even a
    case on real hardware » (#4518), et sur un Penryn nu (dosbox-staging #2418) ; #4518 a été fermé
    « CANTFIX - not our bug » pour les transcendantes, la seule correction (cb275dd6a3) portant sur FINIT et
    FSAVE du 8087 [C12, C14]. D2 § 0.4 (« le bit exact du microcode Intel n'est atteint par aucune implantation
    publique ») et § 3.3 (« passe sur le silicium Intel ») sont à corriger : MCPDIAG juge les résultats d'une
    génération (le 387 ou le 487), pas « le bit Intel » en général ; il n'est pas un oracle. — documenté (seconde
    main).
  - La borne d'erreur : le même document de Juffa dit « Intel's published error limit for the 80387 is 2**-62 »
    et ailleurs « Intel's stated error bound of 3 ULPs » [C11] ; D2 n'en cite qu'une. — documenté.
  - 86Box : FNSTSW AX avec TOP n'existe que sur master, depuis le 25/09/2026 (ffcf9d9963) ; toutes les versions
    publiées jusqu'à 6.0 rendaient `AX = npxs`. FXAM par `fpclassify` date du 16/03/2025 (v5.0), avec C1 resté
    `ST(0) < 0.0`. L'option Softfloat n'est forcée par aucun processeur, seulement par deux machines
    (« PS/2 model 70 type 4 », « Quadtel 286 clone », « due to strict BIOS tests ») [C14]. — documenté.
  - Licences des témoins : IEEETEST et TESTVECS (© UC Berkeley 1985, Cyrix 1990), MCPDIAG (© Intel, ACG) sont
    propriétaires ; les sources de PCTRL, RCTRL, DENORMTS sont imprimées par Juffa sans licence [C11, C12] :
    hors dépôt seulement, comme les images de `os/`. D2 § 3.3 le dit pour les binaires ; il faut le dire aussi
    pour les sources de Juffa (les ressaisir dans le dépôt pose une question de droit d'auteur). — documenté.
- **K10. Les marqueurs** (D2 § 7 : « les sites du x87 sont dans des fichiers GÉNÉRÉS : les marqueurs s'y
  ajoutent par les générateurs »). En partie faux. `x87_ops_loadstore.cs` a été « transcrit à la main, par
  règles, et n'a pas de générateur » (`tools/x87gen/README.md`) — PB-54 y vit —, mais `gen46.py:7` le RECOPIE
  dans `x87_ops_808x.cs` : toute retouche impose de rejouer gen46. PB-55, 56, 59, 60 et 70 vivent dans
  `x87_ops.cs`, écrit à la main, avec une seconde copie de `x87_ld80`, `x87_st80` et `FPU_ILLEGAL` dans
  `x87_8087.cs:115, 141, 214` (à la main) ; PB-62 a ses déclarations dans `x87.cs:40-41`. Les sites générés sont
  ceux de PB-48 et PB-60 (gen43), PB-62 et le FINIT de PB-70 (gen44), PB-52 (les tables, gentab et gen46). P0
  mêle donc des retouches à la main, des générateurs retouchés et un rejeu. Et le marqueur `pcem bug, fixed in
  hardware mode` (PLAN.md § G13) n'est pas dans la liste close de R1 (d) (TRANSCRIPTION.md:21) : l'amendement que
  D7 § 9 prévoit est un préalable de P0. — documenté.
- **K11. Le registre est périmé, et D2 ne le dit pas.** PB-48 : « *Reproduit* : pas encore — G4.3 transcrira… »
  (`PCEM_BUGS.md:393`), alors que G4.3 l'a fait (`x87_ops.cs:322-360`). PB-52 : « `Cpu/x87_ops.cs`, `TableFpu` »
  (`PCEM_BUGS.md:444`), alors que TableFpu a disparu en G4.3 (VERIFICATION.md:4160 ; `x87_ops.cs:384-385`) : le
  site est `x87_ops_tables.cs:427-500` et `x87_ops_808x_tables.cs:218`. À corriger en P0. — documenté.
- **K12. La porte de P0** (D2 § 8.1 : « série entière identique à l'unité »). P0 ne change que des commentaires
  et le registre. La règle du 04/10 (une série entière seulement quand l'émulateur change) demande un
  sous-ensemble ciblé : construction sans avertissement, rejeu des quatre générateurs (`git status` vide),
  recensement des marqueurs — sauf si P0 est fondu dans l'étape du mécanisme (D7 § 9, G13.0). — déduit (règle).
- **K13. PB-65 est plus large que son entrée.** Le quotient passe par une division ARRONDIE au plus près, puis
  tronquée (`x87_ops_misc.h:640`) : dès que ST(0)/ST(1) s'arrondit à l'entier supérieur, le reste est faux même
  pour un petit quotient. FPREM(1,0 ; 0,1) rend 0 chez PCem (1,0/0,1 = 10,0 en double), 0,09999999999999995 sur
  le silicium (= `fmod`). L'entrée PB-65 (« faux au-delà de 2⁵³ ») et D2 § 5 sont à compléter. — déduit (calcul).
- **K14. PB-68 en voie A** (D2 : « précision : celle de la libm »). Optimiste : `pow(2.0, x) − 1.0` et
  `log(x + 1.0)` (`x87_ops_misc.h:559`, `:582`) perdent toute précision relative pour |x| petit — la raison d'être
  de F2XM1 et FYL2XP1. La voie A doit écrire expm1 et log1p : la BCL ne les fournit pas (dotnet/runtime,
  `Double.cs:634` `ExpM1(x) => Math.Exp(x) - 1`, `:640` `Exp2M1(x) => Math.Pow(2, x) - 1`, `:911`
  `LogP1(x) => Math.Log(x + 1)`) [C21]. — documenté (code).
- **K15. Le domaine d'accord** (D2 § 0.7, § 4.3, § 8.2). Faisable, à cinq conditions que D2 ne pose pas :
  (i) le silicium pose PE (et C1 sur 387+) à presque chaque produit ou quotient inexact, PCem jamais : la
  comparaison de `npxs` doit exclure PE et C1, sinon le domaine se réduit aux opérations exactes — « ni
  dépassement ni exception » ne peut pas inclure l'inexact ; (ii) l'état en 80 bits doit être ramené au double
  (exact dans le domaine) et TAG_UINT64, `MM[].q` neutralisés ; (iii) le fuzzeur tire des états spéciaux
  (`--fpu-state` : NaN, infinis, dénormaux, UINT64, `npxc` entier, VERIFICATION.md § G4.0) et ses chaînes sortent
  du domaine (débordement, dénormal, √ d'un négatif) : il faut un prédicat par pas et l'arrêt, ou la
  resynchronisation, à la sortie ; (iv) exclure les cycles des gestionnaires dont le mode matériel corrige le
  temps (FCOM registre, PB-57 ; FBLD, PB-52), et FRNDINT (le signe du zéro, § 3 A4-e) ; (v) laisser hors domaine
  `x87_pc_*` et les images FSAVE/FSTENV (PB-62). — déduit.
- **K16. La granularité « une table recopiée par PB »** (D2 § 4.2). Vraie seulement pour les PB à gestionnaires
  disjoints (52, 57, 58, 61, 63, 64, 65, 66, 67, 68). PB-48, 54, 59, 60, 62, 70 touchent les mêmes gestionnaires :
  le masque par PB y devient des drapeaux lus DANS les gestionnaires matériels (pliés par l'option B de D7). En
  voie B, la granularité par PB n'a de sens que pour le contrôle de fuite : rendre « réglable » un PB logique
  obligerait à réécrire le défaut de PCem en 80 bits. Et les tables de PCem sont les mêmes pour 287, 387 et
  486 (`cpu.cs:321-340` ne lit pas `fpu_type`) : le mode matériel veut des tables par type, ou des tests de
  `fpu_type` dans les gestionnaires, comme FINIT, FENI et FDISI en font déjà (`x87_ops_misc.h:6, 16, 53`). —
  déduit.
- **K17. La pose des tables** (D2 § 4.3). Les huit `ops_808x_fpu_*` sont des `static readonly OpFn[]`
  (`x87_ops_808x_tables.cs:218` pour DF) dont le CONTENU se modifie : on peut y recopier des entrées au montage
  sans toucher `808x.cs` — mais l'objet de PCem est alors altéré dans ce processus, et l'égalité de références
  de D7 § 6.1-3 ne voit rien : il faut une porte sur les contenus. Côté 386, `cpu_set` est rappelé à chaque
  `resetpchard` (`pc.cs:1193`) : la pose vit dans `cpu.cs` ou juste après cet appel, deux fichiers transcrits
  (un écart froid, une fois par démarrage). — déduit.
- **K18. FST m32 qui ignore RC.** D2 § 5 (PB-48) le renvoie « (voisin, § 7) », mais la table du § 7 ne l'a pas.
  À y ajouter, ou à rattacher à PB-48, dont l'entrée dit « Les champs RC … ne sont lus que là »
  (`x87_ops_loadstore.h:520-531`, `ts.s = (float)ST(0)`). — documenté (code).
- **K19. Détails.** « ≈ 116 gestionnaires arithmétiques » : 66 par instanciation (48 mémoire, 18 registre), plus
  FSQRT, soit 134 avec le 808x. `softfloat3e/` de 86Box : 234 fichiers et 1 077 123 octets au 06/10/2026 (D2 :
  232, ≈ 992 Ko) [C14]. [A7] § 14.7 pose l'exception 13/12 pour tout opérande qui franchit 0FFFFh, sans nommer les
  ESC : pour le coprocesseur, c'est déduit [C8]. 86Box a son option depuis la v4.0, pas « avant la v4.2 »
  seulement (exact, mais imprécis) [C14].
- **K20. Les citations Intel, relues dans les manuels** [C3] (387 PRM 231917-001 ; 287 PRM 210498-005 ;
  Numerics Supplement 1980 ; i486 PRM 1990 ; Pentium vol. 3, 1995 ; SDM vol. 3B de 2016 et de 2022 ; AP-578) :
  - a. **Les constantes.** Les deux citations de D2 § 3.2 sont exactes (387 PRM annexe C § C.4, ligne « FLD
    constant », p. C-5 ; SDM vol. 3B § 22.18.7.13, p. 22-13, même numéro en 2016 et en 2022) et s'excluent bien
    pour FLDPI, FLDLN2, FLDLG2 et FLDL2E — pas pour FLDL2T, où elles s'accordent. Mais ce n'est pas une source
    contre une : le 387 PRM se contredit lui-même (§ 4.7, p. 4-20 : « When the rounding control is set to round
    to nearest on the 80387, the 80387 produces the same constant that is produced by the 80287 ») ; le i486 PRM
    dit de même (§ 17.6, p. 17-7) tout en recopiant l'annexe C (§ 25.2, p. 25-9) ; le manuel du Pentium de 1995
    (vol. 3 § 23.3.4) et le SDM (vol. 3, et vol. 2, page FLD1…FLDZ [C7]) aussi. La ligne de l'annexe C est
    l'isolée, réécrite par Intel entre 1990 et 1995. Le 8087 et le 287 rendent donc, selon quatre textes contre
    un, la valeur AU PLUS PRÈS (π = …C235) — déduit ; le silicium reste à mesurer, et l'article de Shirriff sur la
    ROM ne donne pas les motifs hexadécimaux [C16], contrairement à ce que suggère D2 § 3.2. PB-66 sur 8087/287
    passe de (c) à (b).
  - b. **FPATAN.** Contradiction confirmée, et D2 a raison sur le fond plus fortement qu'il ne le dit : le SDM
    vol. 2 (page FPATAN, « IA-32 Architecture Compatibility » : « 0 ≤ |ST(1)| < |ST(0)| < +∞ ») se range avec le
    287 PRM et le Numerics Supplement ; seuls l'annexe C, le SDM vol. 3 § 22.18.7.9 et leurs copies (i486 PRM
    § 25.2, Pentium § 23.3.4) inversent — le § 22.18.7.9 dit même « This difference has impact on existing
    software » là où l'annexe C disait « None ». Pages : 287 PRM p. 2-13, pas 2-12 ; le Numerics Supplement
    (p. S-37) imprime « 0 < Y < X < ∞ », strict. — documenté.
  - c. **PB-65.** « C2 excepté » n'est pas déduit, il est écrit : SDM § 22.18.2.1, « After an incomplete
    FPREM/FPREM1 instruction, the C0, C1, and C3 flags are set to 0 on the 32-bit x87 FPUs. After the same
    operation on a 16-bit IA-32 math coprocessor, these flags are left intact. » — documenté.
  - d. **PB-62.** D2 § 5 propose d'« effacer à FNINIT/FSAVE (387+) » les pointeurs. Faux pour le 387 : SDM
    vol. 2, FINIT, « In the Intel387 math coprocessor, the FINIT/FNINIT instruction does not clear the
    instruction and data pointers » ; le 486 et ses successeurs les effacent (« Both the instruction and data
    pointers are cleared ») [C15]. La règle se pose par type de coprocesseur ; pour FSAVE, qui réinitialise
    « comme FNINIT », le 387 les garde aussi (déduit). — documenté.
  - e. **PB-54, le 486.** D2 : « #GP(0) ou #SS(0), jamais d'INT 9 (documenté [A4] § 22.18.6.12-13) ». Le
    § 22.18.6.12 dit que, là où le 387 lèverait INT 9, le 486 « simply abort[s] the instruction » ; le SDM vol. 3A
    ch. 6 (« Interrupt 9 ») dit au contraire que la condition est « detected with a general protection exception
    (#GP) » ; le i486 PRM se contredit de même (§ 9.9.9, p. 9-17, contre § 25.1, p. 25-2) ; #SS n'apparaît que
    dans les pages d'instructions du vol. 2. Opérande qui COMMENCE hors limite : (a). Opérande À CHEVAL sur la
    limite : (b), Intel se contredit.
  - f. **PB-54, le 386.** La « middle portion » que D2 dit déduite est écrite dans le 387 PRM, annexe D item 7 :
    « Interrupt 9 will occur if the second or subsequent words of a floating-point operand fall outside a
    segment's size. Interrupt 13 will occur if the starting address of a numeric operand falls outside a
    segment's size » ; mais la fiche du 387 (231920, table 2.6) en donne une autre description (un opérande
    « wrapped around an addressing limit » ; « never allowing numeric data to start within 108 bytes of the end
    of a segment »). En mode réel, l'exception 13 de l'80386 PRM § 14.7 ne nomme pas les ESC [C8], et l'annexe D
    ne se limite pas au mode protégé : le « (a) pour le 386 réel » de D2 devient (b).
  - g. **Le mot de contrôle du 8087 après FINIT.** Le Numerics Supplement (table S-7, p. S-26) donne les champs
    (IC = 0 projectif, RC = 00, PC = 11, IEM = 1, masques 111111), pas « 03FFh » (D2 § 5, PB-69 : « documenté
    [A3] fig. S-7 ») : le bit 6 est réservé, d'où 03BFh ou 03FFh, que FNSTCW rend visible ; le 287 PRM marque les
    bits 6-7 réservés tout en listant IEM = 1 (table 3-1), et 037Fh, la valeur de PCem pour le 287, n'est écrit
    par Intel que pour le 387 (387 PRM § 4.8.1). IEM = 1 : documenté ; la valeur exacte du mot : inconnu.
  - h. **Les codes de condition.** Au RESET du 8087 et du 287 : « ???? (Indeterminate) » (table S-7 ; 287 PRM
    table 3-1) ; après FINIT : intacts (SDM § 22.18.2.1 ; 387 PRM § C.3) ; au RESET du 387 : IE et ES posés, IM à
    0, ERROR# actif (387 PRM § C.1). Les voisins « FINIT efface C3-C0 » et « x87_reset vide » de D2 sont
    documentés ; 86Box a corrigé le premier pour le 8087 (§ 3 A8).
  - i. **AP-578.** Les citations de D2 tiennent (§ 2.1, p. 5 ; § 2.2.1, p. 6 ; § 2.2.2 ; § 2.3.1, pp. 7-8, listes
    « deferred » et « immediate » ; NE = 1 et l'interruption 16 aux § 4.1-4.2, p. 27). À ajouter pour les
    programmes invités de PB-59 : « Windows 95 and 3.1 use interrupt 5DH instead of 75H » (note 2) — sous
    Windows 3.1 en mode étendu, un témoin d'acheminement accroche 5Dh, pas 75h.
  - j. **Les numéros du SDM.** Ceux de D2 (22.18.7.9, .13, .15, .16) sont ceux de l'édition de juin 2016
    (325384-059US) ; dans celle de 2022 (253669-077US), FXAM devient 22.18.7.14 et FSAVE/FSTENV 22.18.7.15
    (FSETPM passé en 22.18.9). D2 le signale en général ; l'édition est à figer dans la référence [A4].
  - Non revérifiés par la contre-lecture : [A2] § 9.6.3 (INT 9 du 286) et les tables S-25 à S-27 du Numerics
    Supplement, citées par D2 pour les unnormaux et le projectif.

---

## 3. Ajouté

- **A1. Une voie intermédiaire, « A+ » : le `double` corrigé, plus l'image exacte de 80 bits portée par
  l'étiquette** — la rustine TAG_UINT64 de PCem, généralisée. PCem range déjà l'image brute dans `MM[].q` et
  `MM_w4` à FRSTOR (`x87_ops.h:166-167`). Une étiquette « image exacte », posée par FLD m80, FRSTOR, FBLD,
  FILD m64 ; copiée par FLD ST(i), FXCH, FST ST(i) ; signe basculé exactement par FCHS et FABS ; lue par FSTP
  m80, FSAVE, FBSTP, FIST(P), FCOM, FTST, FXAM ; effacée par toute arithmétique. Elle ferme la part « mouvement
  de données » de PB-55 (aller-retour m80 et FSAVE/FRSTOR exacts), FBLD → FBSTP sur dix-huit chiffres, les
  charges de NaN de 63 bits (PB-60), les classes de FXAM des formats chargés (PB-63) ; elle laisse PB-56
  (précision et plage des calculs, K5). Ordre de grandeur : 200 à 400 lignes dans les gestionnaires matériels.
  C'est la réponse à « un 80 bits limité aux opérations où il change un résultat observable » : 80 bits pour
  stocker, `double` pour calculer. — déduit.
- **A2. La voie « B allégée »** : le cœur de 80 bits pour +, −, ×, ÷, √, reste, arrondi à l'entier,
  comparaisons et conversions (SoftFloat 3e d'Hauser, BSD-3, avec sa spécialisation `8086`, pas celle de
  Bochs) ; les transcendantes calculées en `double` (`Math.*`, expm1/log1p écrits) sur l'argument arrondi à 53
  bits, avec les domaines, C2, la réduction et le `1` de FPTAN exacts. Elle évite le blocage de K3 et ≈ 1 500 à
  2 000 lignes ; les transcendantes y sont à ~1 ulp de `double`, pas à 64 bits — Intel non plus n'est pas « au
  bit » d'une génération à l'autre ([A4] § 22.18.8, et K9). Des noyaux de 64 bits peuvent suivre, d'une source
  compatible ou écrits. — déduit.
- **A3. Une voie non examinée, et à écarter en connaissance de cause : l'x87 de l'HÔTE**, par une bibliothèque
  native — comme PCem pour `x87_compare` (`x87_ops.h:189-205`). Le dépôt dépend déjà de l'x86-64 (`CvtI64` :
  `Sse2.X64`, `x87_ops.cs:153`). Exacte pour +, −, ×, ÷, √ sous PC et RC, presque gratuite en lignes ; mais un
  P/Invoke par instruction, du code natif à construire, des transcendantes et un FPREM partiel propres au
  processeur hôte (non déterministes d'une machine à l'autre), rien pour le projectif ni les unnormaux du 287 ;
  et le dépôt a jusqu'ici évité tout P/Invoke (PLAN-G4, décisions n° 3 et 4 : celui de la libm n'était qu'un
  repli, non retenu). — déduit.
- **A4. Des défauts voisins que D2 n'a pas relevés** (tous lus dans le C) :
  - a. **FSTENV ne masque pas les exceptions** après la sauvegarde (`x87_ops_misc.h:826-869` : `npxc` intact) ;
    SDM vol. 2 : « Saves the current FPU operating environment … and then masks all floating-point exceptions »
    [C4]. **Observable en mode PCem** : ZE démasquée, FNSTENV puis FDIV par zéro lève l'IRQ13 (PB-59) là où le
    silicium rend ±∞. — documenté (387+) ; 8087/287 : déduit.
  - b. **Le mot d'étiquettes ment pour les NaN et les infinis** : `x87_gettag` rend 00 (valide) pour eux
    (`x87.c:39-42`) et 10 (spécial) pour TAG_UINT64 ; le SDM réserve 10 aux NaN, infinis, dénormaux et formats
    non pris en charge (vol. 1, mot d'étiquettes). Et `x87_settag` relit 10 comme TAG_UINT64 (`x87.c:56-57`) :
    après FLDENV d'une image marquée 10, FISTP m64 écrit un `MM[].q` périmé. — documenté (code) ; le vrai
    comportement : documenté (SDM vol. 1 § 8.1.7 : « 10 — Special: invalid (NaN, unsupported), infinity, or
    denormal ») [C23].
  - c. **Les instructions du 387 s'exécutent sur le 8087 et le 287, en zéro cycle** : FUCOM, FUCOMP
    (`x87_ops.h:758-759`), FUCOMPP (`:432`), FPREM1 (D9 F5), FSIN, FCOS, FSINCOS ; leurs temps valent 0,
    « /*387+*/ » (`x87_timings.c:54, 61-62, 71` pour le 8087 ; `:128, 135-136, 145` pour le 287). PB-68 ne
    couvre que FSIN/FCOS/FSINCOS ; FUCOM* et l'existence de FPREM1 n'ont pas d'entrée. — documenté (code) ; ce que
    le 287 fait de ces octets : inconnu.
  - d. **Les alias non documentés, traités à moitié** : D9 D8+i (FSTP1) et DC D0+i, D8+i (FCOM2, FCOMP3, par la
    table de 32 entrées, `x87_ops.h:714-718`) s'exécutent ; DD C8+i (FXCH4), DE D0+i (FCOMP5), DF C0+i (FFREEP),
    DF C8+i, D0+i, D8+i (FXCH7, FSTP8, FSTP9) sont `FPU_ILLEGAL` (`:755`, `:835`, `:912-915`). Ce que fait le
    silicium : déduit de sources secondaires, à instruire avant toute inscription.
  - e. **FRNDINT perd le signe du zéro** : −0 et ]−0,5 ; 0[ au plus près (et ]−1 ; 0[ vers +∞ ou vers zéro)
    rendent +0 (`(double)x87_fround`, `x87_ops.h:60-82`) ; IEEE 754-1985 § 6.3 : « the sign of the result of the
    round floating-point number to integral value operation is the sign of the operand » [C22]. — documenté.
  - f. **FLD m64 d'un SNaN le garde signalant** (copie des bits, `x87_ops_loadstore.h:396-411`), FLD m32 le
    rend silencieux (`:487-501`) ; le silicium (387+) : #IA, puis le QNaN, pour les deux [C5]. — documenté.
  - g. **Les comparaisons ne remettent pas C1 à zéro** (`x87_ops_arith.h:29, 42, 159, 173, 185, 201` :
    `~(C0 | C2 | C3)`) ; SDM, FCOM : « C1 Set to 0 » [C6] ; un C1 posé par FXAM ou FPREM traverse. —
    documenté.
  - h. **FLD m32/m64 d'un dénormal ne pose pas DE** ; le SDM précise que, DE démasquée, la valeur est quand même
    empilée [C5] — un cas de PB-59 que l'acheminement doit traiter à part. — documenté.
  - i. **PB-54 oublie les droits** : `SEG_CHECK_WRITE` ne teste que le sélecteur nul (`386_common.h:66-72`),
    `CHECK_WRITE` teste aussi l'écriture permise et le segment de code (`:81-86`) : un stockage x87 dans un
    segment en lecture seule ne faute pas (hors FSTP m64). — documenté (code).
  - j. **L'adressage du 8087 est incohérent dans PCem même** : FBSTP écrit ses octets par `writememb_8087`
    (segment + offset sur 32 bits, sans repli, `8087.h:25`), tous les mots par le `readmemw`/`writememw` du
    808x, qui replient. — documenté (code).
  - k. **FISTP m64 hors bornes tombe juste par hasard** : `x87_fround` rend 0x8000000000000000 (cvttsd2si,
    mesuré en G4.0), qui EST l'indéfini entier ; seuls m16 et m32 sont faux (et IE manque partout). — documenté.
  - l. **Le C# porte deux globales `tempc`** (`x86_flags.cs:67`, `808x.cs:39`) là où le C n'en a qu'une
    (`808x.c:37`, l'`extern` de `x86_flags.h:3`) : le FBSTP du 8087 écrit celle des drapeaux du 386, que le 808x
    ne lit pas. Sans effet (C6), mais l'entrée PB-53 devrait le dire. — documenté (code).
- **A5. PB-62 sur le 287 dépend de FSETPM.** Le 287 ne voit pas le mode du 286 : la disposition de ses images
  suit l'état posé par FSETPM (DB E4), pas CR0.PE ; PCem choisit par `cr0 & 1` (`x87_ops_misc.h:101`, `:170`) et
  fait de FSETPM un FNOP (`x87_ops.h:589`). Un programme protégé qui omet FSETPM (ou un retour en mode réel sans
  FINIT) lit sur le silicium l'autre disposition. — déduit ; la phrase du 287 PRM sur FSETPM reste à relever (le
  SDM de 2022 range FSETPM parmi les instructions obsolètes, § 22.18.9).
- **A6. PB-58 : une hypothèse à instruire avant de corriger.** Le contournement ne se déclenche que sur les
  motifs exacts (−0, +0). La détection classique (FNINIT ; 1/0 ; FLD ST ; FCHS ; FCOMPP) donne ±∞, pas des
  zéros ; des zéros n'y apparaissent que si 1/0 n'a pas été écrit — ZE démasquée, `x87_div` sort sans écrire
  (PB-59), par exemple avec `npxc = 0` laissé par `x87_reset` vide (VERIFICATION.md § G4.4). Si c'est cela, PB-58
  corrigé SANS PB-59 et sans le reset change une détection : jouer MSD, Windows et une détection sans FNINIT,
  avec et sans le contournement. — déduit (hypothèse).
- **A7. PB-69 : la NMI du 8088 est sur front**, le chemin de PCem est un niveau réarmé par IRET
  (`808x.cs:3793-3806` ; `nmi_enable = 1` à `:2614`). Si la correction pose `nmi = 1` sans le retirer au service,
  un gestionnaire qui revient par IRET sans FNCLEX boucle ; pas le silicium. — déduit.
- **A8. Des témoins que D2 n'a pas.** 86Box v4.2 : « Fixed issues with Lotus 1-2-3 and other applications due
  to missing FBLD FPU instruction » [C14] — un témoin réel de PB-52, et un logiciel du quotidien. Les BIOS
  « stricts » (PS/2 70 type 4, Quadtel 286) qui exigent le Softfloat de 86Box (pas dans nos machines). Et #4518
  confirme un voisin de D2 : sur le 8087, FINIT et FSAVE gardent C3-C0 (« F(N)INIT and F(N)SAVE both need to
  simply AND the status word for 0x4700 on a 8087 ») [C14]. — documenté (seconde main).
- **A9. B1 en éclaireur.** Le noyau extF80 se transcrit et se vérifie contre TestFloat HORS de la machine, sans
  toucher l'émulateur ni ses portes : c'est la mesure du vrai coût (lignes, ns par opération sur X87BANC et un
  Whetstone) avant de s'engager sur B2-B6. D2 le place en tête de B ; il faut en faire le point de décision. Si
  R10 (D7) étend R2 aux sources autres que PCem, le plafond de +25 % sur les 2 390 lignes vives du sous-ensemble
  de SoftFloat 3e (K2) borne le noyau à ≈ 3 000 lignes vives de C#, un critère de sortie tout prêt ; et SoftFloat
  vendoré à côté de `pcem-dev/` (`VENDORED.md`) devient un second oracle compilable, comme le premier. — déduit.
- **A10. Les licences, en une table.**

| Source | Licence | Pour un dépôt GPL-2.0-only |
|---|---|---|
| SoftFloat 3e, TestFloat 3e (Hauser) | BSD-3 (Université de Californie) [C9] | compatible ; vendorable |
| Extensions de Bochs (fsincos, fpatan, fyl2x, f2xm1, fprem, poly, specialize, helpers) | SoftFloat-2b, clause d'indemnité [C14, C19] | **incompatible selon la FSF** [C20] : ni recopie ni transcription |
| `fpu_trans.h`, `poly.h` de Bochs | LGPL-2.0-or-later | compatible |
| 86Box | GPL-2.0-or-later [C14] | compatible, sauf ses fichiers 2b |
| MAME `i8087.cpp` | BSD-3 ; ses `bochs_ext` en 2b [C19] | idem |
| Tests x87 de QEMU | GPL-2.0-or-later (en-tête de fprem ; LICENSE pour les autres) [C10] | compatible |
| `granite/tools/8087mc` | GPL-3.0 [C17] ; le microcode lui-même est l'œuvre d'Intel | lecture seulement |
| IEEETEST, TESTVECS, MCPDIAG | propriétaires [C11, C12] | hors dépôt |
| PCTRL, RCTRL, DENORMTS (Juffa) | aucune licence écrite [C11] | hors dépôt, ou réécrits |

---

## 4. Le classement par PB, après contre-lecture

(a) corrigeable et vérifiable ; (b) vérification faible ; (c) vrai comportement inconnu ; (d) à ne pas
corriger ; « partiel » : la correction n'atteint pas tout le défaut (résidu déclaré, distinct de (b)).

| PB | D2 | Contre-lecture, voie A | Contre-lecture, voie B | Ce qui change |
|---|---|---|---|---|
| 48 | (a) | (a) sous PC = 53 ; sous PC = 64, (a) pour la valeur rangée d'une opération isolée, registre faux (PB-56) | (a) — TestFloat | K4, K5 |
| 52 | (a) ; BCD invalide (c) | (a) ≤ 2⁵³, au-delà avec A+ ; (c) chiffres invalides | (a) ; (c) chiffres invalides | A1, A8 (Lotus 1-2-3) |
| 54 | (b) ; (a) 486 et 386 réel | opérande qui commence hors limite (a) ; à cheval (b), Intel se contredit (486, 386 réel) ; droits d'écriture (a) | idem | K20-e, f ; A4-i |
| 55 | (a) B ; (b) A | partiel, vérifiable (a) ; exact avec A+ pour les valeurs non recalculées | (a) ; formats non canoniques hors TestFloat (b) | K6, K8, K9 |
| 56 | (a) B ; A : (i) seulement | (i) (a) ; (ii) non corrigeable (~25 % des expressions, K5) | (a) | K5 |
| 57 | (a) ; C2 8087/287 (c) | (a) ; (c) | (a) ; (c) | — |
| 58 | (a) | (a), avec PB-70, APRÈS l'hypothèse A6 instruite | idem | A6 |
| 59 | (b) | drapeaux IE, ZE, DE et pile (a) ; OE, UE, PE non ; acheminement (b) | drapeaux (a) (TestFloat) ; démasquées OE/UE (b) ; acheminement (b) | K7 |
| 60 | (a) ; égalité (c) | (a) sauf charges m80 (exact avec A+) ; (c) | (a) ; (c) | K4 |
| 61 | (a) ; DF E0 du 8087 (c) | (a) ; (c) sans effet visible | idem | — |
| 62 | (a) ; 8087 (c) | (a) 387/486, effacement par FINIT selon le type ; 287 (a) avec FSETPM ; 8087 (c) | idem | K20-d, A5 |
| 63 | (a) ; vides 8087/287 (c) | (a) pour les classes atteignables ; (c) | (a) ; (c) | — |
| 64 | (a) | (a) | (a) | — |
| 65 | (a) ; N et quotient du 287 (c) | (a) réduction complète (et petits quotients, K13), « C2 excepté » documenté ; (c) N ; quotient du 287 (b) | idem | K13, K20-c |
| 66 | (a) 387/486 ; 8087/287 (c) | (a) valeur rangée (C8), registre à 53 bits ; 8087/287 (b) : au plus près, quatre textes d'Intel contre un | (a) ; 8087/287 (b), idem | K20-a |
| 67 | (a) | (a) | (a), la rustine disparaît | — |
| 68 | (b) | domaines, C2, `1`, IE/ZE (a) ; précision (b) avec expm1/log1p écrits | domaines (a) ; précision (b) ; noyaux de Bochs bloqués (K3) | K3, K14 |
| 69 | (a) | (a) ; SW1 déduit ; front contre niveau (A7) | idem | A7 |
| 70 | (a) ; 287XL déduit | (a) ; 287XL déduit (290376 non consultée) | idem | — |
| 53 | (d) | (d) | (d) | A4-l |

Comptes : en voie B, la contre-lecture retrouve pour l'essentiel ceux de D2 — (a) 16, (b) 3 (54, 59, 68),
(d) 1 —, avec plus de sous-cas. En voie A, ils ne tiennent pas : 14 fermés, 3 partiels, 2 pour l'essentiel
ouverts (55, 56).

---

## 5. Recommandation : la voie B, préparée autrement

**Pourquoi pas A seule.** Son seul avantage était le prix, et il fond (K1 : 6 000 à 8 000 lignes, contre 1 500
à 2 500 annoncées). Elle laisse le défaut le plus visible du domaine : sous le mot de FNINIT, une expression
ordinaire sur quatre diffère du silicium au dernier bit (K5), la plage intermédiaire déborde à 10³⁰⁸,
l'aller-retour m80, Comp et le BCD de dix-huit chiffres restent faux. Et ce qu'elle « corrige » (PB-48, 60, 66)
ne l'est qu'au regard d'une machine à 53 bits (K4) ; sa vérification n'a pas d'oracle extérieur.

**Pourquoi B.** Elle a l'oracle que la doctrine réclame (« l'oracle d'abord ») : TestFloat et SoftFloat 3e,
sous licence compatible, en face du noyau ; le domaine d'accord (K15) en face de PCem. Elle coûte ≈ 2 à 3,5
fois A à dessin égal, pas 3 à 7.

**Comment.**
1. **P0** (registre, marqueurs, K10, K11, K18) en sous-ensemble ciblé (K12).
2. **X0**, le cadre de D2 § 8.2, avec la porte de contenu du 8088 (K17) et des tables par type (K16).
3. **A1 réduite** aux PB qui ne touchent qu'une poignée de gestionnaires : 61 (FNSTSW AX), 57 (FCOM registre),
   58 avec la part « comparaisons » de 70 (après A6), 64 (FTST), 63 (FXAM), 66 (les constantes), 67 (FST
   registre). Peu de lignes, visibles (MSD « 80287 », FNSTSW AX, FXAM), et leurs cas `x87hw-cases` survivent à B.
   Pas 52, 54, 62, ni la part arithmétique de 70 (∞ ± ∞, √+∞ en projectif) : ils touchent tous les
   gestionnaires mémoire ou arithmétiques, que B réécrit.
4. **L'acheminement** (PB-69, l'IRQ13 et le verrou de l'AT, F0h/F1h, FERR#/#MF du 486) avec ZE seule : il ne
   dépend pas de la représentation et se garde en B.
5. **B1 hors machine, point de décision** (A9) : SoftFloat 3e et sa spécialisation `8086`, TestFloat, la
   mesure en ns. Si B1 déçoit, repli sur **A+** (A1) : 80 bits pour stocker, `double` pour calculer.
6. **B2, B3, B5** comme D2 § 8.4, en une instanciation partagée si Julien l'accepte (K1, K2).
7. **B4 remplacée** : transcendantes en `double` (A2) ; des noyaux de 64 bits seulement d'une source compatible
   GPL-2.0-only, ou écrits — jamais depuis les fichiers 2b de Bochs (K3).

À trancher par Julien, en plus de D2 § 10 : la licence (K3) ; une ou deux instanciations (K1, K2) ; A+ comme
point d'arrêt possible (A1) ; la place de FSTENV (A4-a), défaut observable dès le mode PCem.

---

## Annexe — Sources de la contre-lecture

- [C1] Le dépôt, au commit `bc609ce` : les fichiers et lignes cités dans le texte (`pcem-dev/`, `iXtal26/`,
  `tools/`, PCEM_BUGS.md, VERIFICATION.md, PLAN.md, PLAN-G4.md, TRANSCRIPTION.md).
- [C2] Calculs hors dépôt : `work/consts.py` (constantes, 120 chiffres décimaux), `work/dblround.py` (double
  arrondi, 20 000 tirages, rationnels exacts), dans le répertoire de travail de la session.
- [C3] Les manuels Intel relus (texte intégral et PDF) : 80387 PRM 231917-001
  <https://archive.org/download/bitsavers_intel80386ammersReferenceManual1987_14896856/231917-001_80387_Programmers_Reference_Manual_1987_djvu.txt> ;
  80286/80287 PRM 210498-005
  <https://archive.org/download/bitsavers_intel80286287ProgrammersReferenceManual1987_27505703/210498-005_80286_and_80287_Programmers_Reference_Manual_1987_djvu.txt> ;
  Numerics Supplement <https://archive.org/download/8086familyusersm00inte/8086familyusersm00inte_djvu.txt> ;
  i486 PRM 1990 <http://bitsavers.trailing-edge.com/components/intel/80486/i486_Processor_Programmers_Reference_Manual_1990.pdf> ;
  Pentium Family Developer's Manual vol. 3 (241430-004, 1995)
  <http://bitsavers.trailing-edge.com/components/intel/pentium/241430-004_Pentium_Processor_Family_Developers_Manual_Volume_3_Jul95.pdf> ;
  SDM vol. 3B de 2016 (miroir) <https://xem.github.io/minix86/manual/intel-x86-and-64-manual-vol3/o_fe12b1e2a880e0ce-1011.html>
  et de 2022 (253669-077US) ; AP-578 <https://www.ardent-tool.com/CPU/docs/Intel/IA/243291-002.pdf>
- [C4] SDM vol. 2, FSTENV/FNSTENV : <https://www.felixcloutier.com/x86/fstenv:fnstenv>
- [C5] SDM vol. 2, FLD : <https://www.felixcloutier.com/x86/fld>
- [C6] SDM vol. 2, FCOM/FCOMP/FCOMPP : <https://www.felixcloutier.com/x86/fcom:fcomp:fcompp>
- [C7] SDM vol. 2, FLD1…FLDZ : <https://www.felixcloutier.com/x86/fld1:fldl2t:fldl2e:fldpi:fldlg2:fldln2:fldz>
- [C8] 80386 PRM § 9.8.9 et § 14.7 : <https://flint.cs.yale.edu/cs422/readings/i386/s09_08.htm>,
  <https://flint.cs.yale.edu/cs422/readings/i386/s14_07.htm>
- [C9] TestFloat 3e et SoftFloat 3e : <http://www.jhauser.us/arithmetic/TestFloat-3/doc/testfloat_gen.html>,
  `testfloat_ver.html`, `TestFloat-general.html`, <http://www.jhauser.us/arithmetic/SoftFloat-3/doc/SoftFloat.html>,
  `SoftFloat-source.html` ; `source/8086/s_propagateNaNExtF80UI.c` de
  <https://github.com/ucb-bar/berkeley-softfloat-3>
- [C10] QEMU `tests/tcg/i386/` : <https://github.com/qemu/qemu/tree/master/tests/tcg/i386> (fldcst, fprem,
  f2xm1…) ; <https://raw.githubusercontent.com/qemu/qemu/master/LICENSE> ; commits eca30647fc07, 5eebc49d2d0a,
  1f18a1e6ab83, 80b4008c805e
- [C11] N. Juffa, v1.5 <https://www.vogonswiki.com/images/2/21/Coproc.txt> et v1.6
  <https://mikro.naprvyraz.sk/docs/Coding/Hardware/COPRO16A.TXT> ; disquette Cyrix
  <https://archive.org/details/cyrixdiagnosticdisk>
- [C12] MCPDIAG : <https://archive.org/details/msdos_MCPDIAG_shareware> ;
  <https://github.com/dosbox-staging/dosbox-staging/issues/2418> ;
  <https://github.com/joncampbell123/dosbox-x/issues/38>
- [C13] 86Box, notes de version : <https://86box.net/2023/08/26/86box-v4-0.html>,
  <https://86box.net/2024/07/26/86box-v4-2.html>
- [C14] 86Box, source au commit 8b7cc386 (06/10/2026) : `src/cpu/x87_ops_sf*.h`, `x87.c`, `x87.h`, `x87_ops.h`,
  `x87_ops_misc.h`, `softfloat3e/` ; `src/config.c:652-654` ; ticket <https://github.com/86Box/86Box/issues/4518>
  et commit cb275dd6a3 ; README.md:81 (GPL-2.0-or-later)
- [C15] SDM vol. 2, FINIT/FNINIT : <https://www.felixcloutier.com/x86/finit:fninit>
- [C16] K. Shirriff, « Extracting ROM constants from the 8087 math coprocessor's die » (2020) :
  <http://www.righto.com/2020/05/extracting-rom-constants-from-8087-math.html>
- [C17] K. Shirriff, <https://www.righto.com/2026/09/8087-tangent-cordic.html> ; licence de
  <https://github.com/a-mcego/granite> (API GitHub : GPL-3.0)
- [C18] DOSBox-X PR #6612 (API GitHub) : <https://github.com/joncampbell123/dosbox-x/pull/6612>
- [C19] MAME `src/devices/machine/i8087.cpp` et `3rdparty/softfloat3/bochs_ext/fsincos.c` :
  <https://github.com/mamedev/mame> ; Bochs, `cpu/fpu/fsincos.cc` (même en-tête 2b)
- [C20] qemu-devel, « SoftFloat licensing in Linux kernel » (avril 2013) :
  <https://qemu-devel.nongnu.narkive.com/VLUQCihu/softfloat-licensing-in-linux-kernel> ; « [PATCH 0/6] relicense
  QEMU softfloat from 2b to 2a » (11/2014) : <https://lists.gnu.org/archive/html/qemu-devel/2014-11/msg03611.html> ;
  en-tête de <https://github.com/qemu/qemu/blob/master/fpu/softfloat.c>
- [C21] dotnet/runtime, `src/libraries/System.Private.CoreLib/src/System/Double.cs` (ExpM1, Exp2M1, LogP1) :
  <https://github.com/dotnet/runtime>
- [C22] IEEE Std 754-1985, § 6.3 « The Sign Bit » (« These rules shall apply even when operands or results are
  zero or infinite ») : <https://www.ime.unicamp.br/~biloti/download/ieee_754-1985.pdf>
- [C23] SDM vol. 1 § 8.1.7, le mot d'étiquettes (miroir) :
  <https://xem.github.io/minix86/manual/intel-x86-and-64-manual-vol1/o_7281d5ea06a5b67a-198.html>
