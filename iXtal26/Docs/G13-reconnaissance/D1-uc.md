# G13 — Reconnaissance D1 : le processeur (8088, 8086, 286, 386, 486 ; hors x87)

> Lecture du 6 octobre 2026, au commit `bc609ce`. LECTURE SEULE : rien n'a été construit, lancé ni
> mesuré dans le dépôt ; les vecteurs SST ne sont pas sur la machine (seuls les deux manifestes y sont).
> Les numéros de ligne C se rapportent au PCem vendoré (`pcem-dev/`), ceux du C# à l'arbre de `bc609ce`.
> Niveaux pour le vrai matériel : **documenté** (une source primaire le dit — documentation Intel, ou
> mesure publiée sur silicium : corpus SingleStepTests), **déduit** (raisonnement depuis des sources),
> **inconnu** (aucune source trouvée). Le mécanisme de l'interrupteur (forme, stockage, coût du JIT, règles)
> est l'objet de la lecture D7 : on n'en reprend ici que ce qui touche le processeur. Sources en annexe.

---

## 0. L'essentiel

1. **Dix-sept entrées lues** (13 de la section A, 4 de la section C). Classement proposé :
   **(a) corrigeable et vérifiable : 10** (PB-01, 02, 45, 87, 39, 40, 50, 51, 77 ; PB-32 en section C) ;
   **(b) corrigeable, vérification faible : 2** (PB-43, PB-78) ; **(c) vrai comportement inconnu : 1**
   (PB-41) ; **(d) à ne pas corriger en G13 : 4** (PB-44, absorbé par PB-43 ; PB-11 et PB-12, du temps
   seul ; PB-42, nettoyage dont le vrai correctif est ailleurs, dans LTR).
2. **Seul PB-01 est relié aux lignes de base SST.** Ses douze formes (`10`–`15`, `18`–`1D`) font 4 320
   échecs sur 120 000 cas du 8088. PB-02 (`D3.2`, `D3.3`) et PB-45 (`F6.7`) ont des formes SST qui ne sont
   pas au manifeste ; tout le reste du domaine (mode protégé, MOV CRx, longueur, limite de CS, 486) n'a
   aucune forme SST : le corpus 386 est en mode réel et ne contient ni `0F 20`–`0F 26`, ni `0F 07`.
3. **Les écarts mesurés sans PB dominent.** 8088 : 7 des 19 formes déviantes (DAA, DAS, REP LODSW,
   SETMO, l'OF de SHL par CL, le débordement de DIV) n'ont aucune entrée. Pour le REP LODS, la cause est
   lue : `rep()` charge l'octet ou le mot dans `temp2`/`tempw2` et n'écrit jamais AL ni AX
   (`808x.c:1112`, `:1131`). 386 : **aucune des 742 formes déviantes** n'est couverte par un PB. Relus à la
   ligne : l'AF d'ADC du cœur 386 (`x86_flags.h:299-307`, un défaut DISTINCT de PB-01 ; le SBB y est
   juste), LOCK sans #UD, le BT* à décalage non signé, MOVSX r16,r/m16 traité en ILLEGAL, AAA/AAS à la
   8086, les SF/ZF d'AAD/AAM calculés sur AX.
4. **PB-41 ne doit pas être « corrigé » comme l'entrée le suggère.** Le SDM d'Intel dit que les moitiés
   hautes sont « modifiées et non maintenues » au chargement d'une TSS 16 bits. Bochs pose FFFF, comme
   PCem. La valeur exacte est inconnue.
5. **PB-87 est plus large que son entrée.** La lecture principale de `FETCH` (`808x.c:145`, sur le 8088
   comme le 8086) lit elle aussi `cs + cpu_state.pc` non masqué au repli de l'IP. Ce site est le plus
   chaud du cœur 8088.
6. **Les marqueurs ne se remplacent pas mécaniquement.** PB-01 et PB-51 n'ont aucun marqueur ; PB-02,
   PB-11 et PB-12 en ont un sans numéro ; PB-50, PB-78 et PB-87 n'ont leur numéro qu'en commentaire ;
   les lignes « Reproduit » de PB-01 et PB-02 ont dérivé ou sont fausses (§ 2.1).
7. **Deux corpus matériels non utilisés existent** : SingleStepTests **8086** (Intel P80C86A-2, 2 000
   cas par forme) et **80286** (Harris N80C286-12, 326 formes, mode réel ; son README annonce des
   instructions que les préfixes portent au-delà de 10 octets, donc vraisemblablement des cas pour
   PB-50). Ils seraient l'oracle matériel du 8086 (M24, PC1512) et du 286 (AT, ami286).
8. **Ordre proposé** : préparer sans changer de comportement (U0) ; puis le 8088/8086 vérifiable par
   SST (U1) ; le fetch au repli (U2, chemin chaud) ; le décodage et la longueur (U3) ; le fetch borné
   du 286/386/486 (U4, chemin chaud) ; le mode protégé (U5, documentation seule).

---

## 1. Tableau récapitulatif

Classement : (a) corrigeable et vérifiable ; (b) corrigeable, vérification faible ; (c) vrai
comportement inconnu, laissé reproduit ; (d) à ne pas corriger (en G13).

| PB | Titre court | Effet observable par l'invité | Classement | Chemin chaud | Vérification du mode matériel |
|---|---|---|---|---|---|
| 01 | AF d'ADC/SBB sans la retenue entrante (8088/8086) | AF faux ~3 % ; une addition BCD multi-octets (ADC puis DAA) rend un chiffre faux | (a) | tiède (chaque ADC/SBB) | SST 8088 `10`–`15`, `18`–`1D` → 10 000/10 000 ; idem SST 8086 |
| 02 | RCL/RCR mot par CL : CF écrasé par la retenue entrée | CF (et OF qui en dépend) faux | (a) | non | SST 8088 `D3.2`, `D3.3` (hors manifeste) |
| 45 | IDIV octet : AX étendu par des zéros | quotient et reste faux pour tout dividende négatif | (a), groupé avec le débordement de DIV/IDIV (non inscrit) | non | SST 8088 `F6.7` : 1 169 → 2 312 réussis prédits (signe seul), ~100 % avec le débordement |
| 87 | Préfetch 8086 au repli d'IP (et lecture principale, 8088 compris) | octet lu 64 Ko plus loin quand une instruction franchit FFFFh | (a) | **oui** (`FETCH`) | banc dirigé C# seul (valeur documentée) ; cas SST qui franchissent FFFFh, s'il y en a |
| 39 | CALL/JMP par porte de tâche → #GP | changement de tâche par porte refusé | (a) chemin nominal ; (b) codes d'erreur | non | pm-check, attentes tirées de la documentation |
| 40 | CALL sur une TSS empile le retour sur la nouvelle pile | ESP de la nouvelle tâche −8 (−4), deux mots écrits | (a) | non | pm-check : ESP 0x7000 au lieu de 0x6FF8 |
| 41 | TSS 16 bits : moitiés hautes à FFFF | EAX…EDI = FFFFxxxx | **(c)** — la correction proposée par l'entrée contredit Intel | non | mesure sur un 386 réel |
| 43 | MOV CRx/DRx/TRx : `mod` décodé en adresse | longueur d'instruction fausse si mod ≠ 3 | (b) | non | banc dirigé (valeur du SDM) |
| 44 | a32 de MOV DRx/TRx en `fetch_ea_16` | idem, autre longueur | (d) seul ; absorbé par PB-43 | non | — |
| 50 | Pas de limite de longueur d'instruction | une suite de préfixes forme une instruction sans fin | (a) | tiède (préfixes) | SST 80286 (cas > 10 octets) ; banc dirigé 386/486 |
| 51 | Pas de contrôle de la limite de CS au fetch | l'exécution franchit la limite au lieu de #GP | (a) | **oui** (fetch) | banc dirigé ; cas SST 286/386 en bord de segment, s'il y en a |
| 77 | `cpu_features` jamais remis à zéro | aucun en usage d'iXtal (UC fixée par processus) | (a), valeur faible | non | `cpu-config-check` C# seul, dans les deux ordres |
| 78 | LOADALL386 s'exécute sur un 486 | le 486 charge tout l'état au lieu de #UD | (b) | non | banc dirigé (#UD déduit) |
| 11 (C) | `readmemw` compare un offset à une adresse linéaire | quelques cycles par lecture mot | (d) en G13 | (tiède) | aucune isolée |
| 12 (C) | REP MOVSB sans `memcycs = 0` | file de préfetch et quelques cycles après REP MOVSB | (d) en G13 | non | aucune isolée |
| 32 (C) | `pmodeint` : code d'erreur toujours nul | #GP « hors IDT » avec code 0 au lieu de n×8+2 (+1) | (a) | non | pm-check : un gestionnaire lit le code |
| 42 (C) | Bit occupé cherché dans la table de l'ancienne TSS | aucun pour un programme valide | (d) nettoyage ; le correctif utile est LTR | non | pm-check : LTR à TI=1 → #GP |

---

## 2. Constats transverses

### 2.1 Les marqueurs et les références

| PB | État du marqueur dans le C# | Référence du registre |
|---|---|---|
| 01 | **aucun** (`808x.cs:580`, `:617`, `:655`, `:694`, les quatre tests d'AF) | cite `:554, 591, 629, 667` : dérivé |
| 02 | « `pcem bug, reproduced:` » **sans numéro** (`808x.cs:3070`, `:3098`) | cite aussi `808x.c:2887` et `:2903` (RCL/RCR w,**1**) : **ces deux sites sont sains à la lecture**, seules les formes `,CL` (`:3184-3187`, `:3207-3210`) portent le défaut |
| 11, 12 | sans numéro (`808x.cs:111`, `:775`) | cite `:105`, `:758` : dérivé |
| 50 | numéro dans un commentaire de bloc (`386_ops_prefix.cs:38`) | — |
| 51 | **aucun** (le registre : « rien d'imposé ») | — |
| 78 | aucun sur `opLOADALL386` (`386_ops_0f.cs:810`) ; numéro cité dans `mem.cs:188` (PB-79) | — |
| 87 | « (PB-87) » en fin de commentaire `// pcem:` (`808x.cs:182`) | — |
| 39, 40, 41, 43, 44, 45, 77, 32, 42 | canonique, numéroté | à jour |

**Deux textes du dépôt à corriger, et un texte extérieur à lire avec prudence :**
- l'en-tête de `Cpu/x86_flags.cs:35-40` (« NE PAS CORRIGER AF_SET… les formes ADC et SBB ») attribue au
  cœur 386 le défaut du 8088, mesuré sur `sst-baseline.tsv`. Dans le cœur 386, le **SBB est juste** et
  l'**ADC a son propre défaut**, d'une autre formule (§ 5.2) ;
- le registre, à PB-03 (`PCEM_BUGS.md:91`), impute « −5,87 ppm » à PB-11. `VERIFICATION.md` § M4.6
  l'attribue à la division entière `cpu_get_speed() / 100` (`pc.c:473`). L'arithmétique tranche :
  4 772 728 / 100 = 47 727,28, tronqué à 47 727, soit −5,87 ppm. **PB-11 n'est pour rien dans la
  fréquence de l'INT 8** (la lecture D7, question 7, a repris l'imputation fautive) ;
- le README du corpus SST 80386 écrit que l'excès de préfixes au-delà de « 10 octets » lève
  « l'interruption #6 », ce qui contredit Intel (15 octets, exception 13 sur le 386). La phrase est
  vraisemblablement recopiée du README 80286 (déduit). Seuls les cas du corpus, s'il en contient,
  trancheront.

### 2.2 Ce que SST couvre, et ne couvre pas

- **8088** : le manifeste (`vectors/sst/MANIFEST.sha256`) porte **84 formes sur 324** (et
  `metadata.json`). Hors manifeste mais utiles au domaine : `D3.2`, `D3.3` (PB-02), `F6.7`, `F7.7`
  (PB-45 et le débordement), `AC` (REP LODSB), `D2.5`, `D2.7`, `D3.4`, `D3.5`, `D3.7` (l'OF par CL),
  `D1.6`, `D2.6`, `D3.6` (SETMO/SETMOC). Les masques de drapeaux indéfinis de `metadata.json` disent que
  `D3.2` et `D3.3` n'en ont **aucun** : CF et OF y sont comparés. `F6.7`/`F7.7` ont le masque `0xF72A`.
- **Le franchissement de FFFFh n'est pas exercé** (déduit). Sur les 817 998 cas de la ligne de base,
  une IP uniforme donnerait une vingtaine d'instructions à cheval sur FFFFh et une dizaine d'opérandes
  mot à l'offset FFFFh. Or aucun échec de ce type n'apparaît, alors que PCem, qui lit ces octets 64 Ko plus loin (§ PB-87), les raterait presque tous.
  Le générateur les évite donc vraisemblablement : SST ne vérifiera ni PB-87 ni le repli des données
  mot du 8088.
- **386** (`v1_ex_real_mode`, 386EX) : 941 formes, **mode réel seulement**. Rien pour le mode protégé,
  les tâches, MOV CRx, LOADALL, le 486. La puce est un 386EX (cœur statique, bus 16 bits) : ses drapeaux
  indéfinis ne sont pas forcément ceux d'un 386DX (inconnu).
- **Deux corpus non exploités** : **8086** (Intel P80C86A-2, 2 000 cas par forme, file de préfetch
  initiale pleine, IDIV avec REP dans 10 % des cas) et **80286** (Harris N80C286-12, 326 formes, mode
  réel, 1 000 à 5 000 cas par forme). Le README 80286 dit que les préfixes de segment tirés au hasard
  peuvent porter l'instruction au-delà de 10 octets, ce qui lève l'interruption 13, et qu'un opérande
  16 bits à l'offset FFFFh lève une exception : le corpus contient donc vraisemblablement des cas pour
  PB-50 et pour le contrôle de limite (à compter). `Moo.cs` lit déjà le format MOO.

### 2.3 Ce que « l'état sondé » veut dire ici

En mode PCem, aucune correction ne change rien, par construction. En mode matériel, chaque correction
change des champs que `h_state` compare : FLAGS (PB-01, 02) ; AX (PB-45) ; pc, cycles, `prefetchqueue`,
`prefetchw`, `prefetchpc`, `fetchcycles` (PB-87) ; CS, EIP, TR, ESP, la mémoire des TSS et des piles
(PB-39, 40) ; EAX…EDI (PB-41) ; pc et `prefetch_bytes` (PB-43, 44) ; la voie d'exception (PB-50, 51,
78) ; `cpu_features` dans l'empreinte UC (PB-77). Conséquence utile : un diff à l'oracle **en mode
matériel** doit diverger sur les seules formes visées. C'est un contrôle de localisation (D7 § 6.3,
« contrôle de fuite »).

---

## 3. Section A — par PB

### PB-01 — L'AF d'ADC et de SBB ignore la retenue entrante (8088 et 8086)

1. **Défaut.** `setadc8/16` et `setsbc8/16` calculent AF comme ADD/SUB, sans `tempc`. **Effet** : AF
   faux quand la somme (ou la différence) des quartets bas vaut exactement 0Fh avec une retenue
   entrante. Une addition BCD multi-octets (`ADC` puis `DAA`) rend alors un chiffre faux : 09h + 06h +
   CF=1 donne AF=0 au lieu de 1, et DAA rend 10h au lieu de 16h.
2. **Sites.** C : `808x.c:786` (setadc8), `:817` (setadc16), `:849` (setsbc8), `:882` (setsbc16).
   C# : `Cpu/808x.cs:580`, `:617`, `:655`, `:694`, **sans marqueur**. Le 8086 (M24, PC1512) passe par le
   même `808x.c`.
3. **Vrai comportement.** AF = retenue (ou emprunt) du bit 3 de l'opération complète, retenue entrante
   comprise. **Documenté** par la mesure SST 8088 (un AMD D8088 : les douze premiers échecs ne
   diffèrent que du bit 0x0010). La formule `((a ^ b ^ résultat) & 0x10)` donne le bon AF pour ADC comme
   pour SBB. Vérifié exhaustivement sur 8 bits (a, b, retenue) : 0 écart. Les formules de PCem se
   trompent sur 4 096 des 131 072 triplets (3,12 %) pour ADC comme pour SBB.
4. **Correction.** Remplacer le test d'AF des quatre aides par la formule XOR, sous la garde : ~4
   lignes changées, ~8 avec les gardes, plus les marqueurs. **Chemin** : tiède, un appel par ADC ou SBB,
   pas par instruction. **Temps** : aucun. **État sondé** : FLAGS.
5. **Vérification.** SST 8088 en mode matériel : `10`–`15`, `18`–`1D` → 10 000/10 000 chacune (ligne
   de base PCem : 9 515 à 9 719). Toutes les autres formes inchangées. Le même contrôle sur le corpus 8086
   s'il est récupéré.
6. **Classement : (a).**
7. **Dépendances.** Indépendant de DAA/DAS (ses échecs SST portent sur AL avec un AF donné, § 5.1).
   PB-53 (x87, FBSTP écrit `tempc`) n'interfère pas : ADC/SBB reposent `tempc` avant de le lire. Le cœur
   286/386/486 a un défaut **frère, non inscrit** (§ 5.2).

### PB-02 — RCL et RCR mot par CL : la retenue sortie est écrasée par la retenue entrée

1. **Défaut.** Après la boucle, `if (templ)` (RCL) et `if (tempw2)` (RCR) reposent CF sur la retenue
   **entrée** dans la dernière itération. **Effet** : CF faux environ une fois sur deux pour un compte ≥ 1 ;
   OF, calculé ensuite depuis ce CF, faux aussi pour un compte de 1.
2. **Sites.** C : `808x.c:3184-3187` (RCL w,CL), `:3207-3210` (RCR w,CL). Les sites `:2887` et `:2903`
   que cite le registre (formes `,1`) sont **sains** à la lecture. Le pendant octet RCR b,CL porte ces
   lignes commentées (`:3059-3060`). C# : `808x.cs:3074-3077` et `:3102-3105` (marqueurs sans numéro à
   `:3070`, `:3098`).
3. **Vrai comportement.** CF reçoit le dernier bit sorti de la rotation : **documenté** (la définition
   de RCL/RCR ; SST 8088 ne masque aucun drapeau pour `D3.2`/`D3.3`, la mesure tranchera). OF pour un
   compte > 1 : indéfini selon Intel, déterministe sur le 8088. PCem le calcule en fin de boucle par la
   formule à un bit, ce qui, le CF corrigé, coïncide avec un calcul fait à chaque itération (**déduit**).
4. **Correction.** Sauter les deux blocs de quatre lignes en mode matériel : ~4 lignes de garde.
   **Chemin** : non. **Temps** : aucun. **État** : FLAGS.
5. **Vérification.** SST 8088 `D3.2`, `D3.3` (5 000 cas chacune, à récupérer). Prédit : ~51 % en mode
   PCem (1/64 de comptes nuls plus une demi-chance), ~100 % en mode matériel. `D2.2`/`D2.3` (octet) en
   témoins inchangés.
6. **Classement : (a).**
7. **Dépendances** : aucune. Le cœur 386 n'a pas ce défaut : `D1.2`, `D1.3`, `D3.2`, `D3.3` y échouent
   au seul taux du LOCK illégal (famille E1).

### PB-45 — IDIV octet étend AX par des zéros

1. **Défaut.** `tempws = (int)AX` sur un `uint16_t`. **Effet** : quotient et reste faux pour tout
   dividende négatif (`AX ≥ 8000h`). Un `CBW ; IDIV r8` de compilateur sur des `char` négatifs rend
   n'importe quoi : l'effet est le plus « logiciel » du domaine.
2. **Sites.** C : `808x.c:3614`. C# : `808x.cs:3511` (marqueur `:3508`). Le cœur 386 signe
   correctement et détecte le débordement (`x86_ops_misc.h:130-149`) : c'est le modèle de la correction.
3. **Vrai comportement.** Dividende signé dans AX : **documenté** (Intel, définition d'IDIV). Le reste du
   comportement du 8088 sur cette forme est documenté aussi :
   - quotient hors de capacité → interruption 0 (386 PRM, DIV/IDIV, « Interrupt 0 if the quotient is
     too big ») ;
   - **sur le 8086/8088, un quotient de 80h (ou 8000h) lève aussi l'exception 0** (386 PRM § 14.7, item 11) ;
   - l'adresse empilée est celle de l'instruction **suivante** (README SST 8088) ;
   - un préfixe REP devant IDIV **inverse le signe du quotient** (README SST 8088 et 8086, mesuré).
4. **Correction.** Le signe seul : 1 ligne. Mais sans la détection du débordement, le mode matériel
   serait faux autrement : à grouper avec un **nouveau PB, « DIV/IDIV : débordement de quotient non
   détecté »** (§ 5.1 ; `808x.c:3569-3611`, `:3613-3650`, `:3720`, `:3743`). Ensemble : une aide
   « erreur de division » factorisant la voie d'INT 0 existante (~12 lignes), quatre tests de débordement
   (~8), le cas 80h/8000h (~2), la négation sous REP (~10, il faut savoir qu'un REP précède : `rep()`
   aiguille les non-chaînes vers `ipc + 1`). En tout **~35 lignes**. **Chemin** : non. **Temps** : la voie
   d'INT 0 garde les cycles de PCem. **État** : AX, FLAGS, pile, CS:IP.
5. **Vérification.** SST 8088 `F6.7` (hors manifeste, mesurée en G2 : 1 169 réussis sur 9 696 joués).
   Sur les 9 372 cas sans REP, prédit **2 312** avec le signe seul (1 143 échecs viennent du signe), puis
   ~100 % avec le débordement. Résidu attendu : les 32 cas à diviseur nul « non instruits ». Hypothèse :
   les FLAGS empilés par l'exception, que le masque des drapeaux finaux ne couvre pas en mémoire
   (inconnu). Également `F7.7`, `F6.6`, `F7.6` pour le débordement.
6. **Classement : (a)**, à condition d'inscrire et de corriger le débordement dans la même étape.
7. **Dépendances.** Le débordement de DIV (non inscrit, `F6.6`/`F7.6`). PB-47 (IDIV mot INT_MIN/−1,
   non reproduit, R9) : en mode matériel, il devient un simple débordement, donc #DE. PB-46 (AAM 0, R9) :
   même famille « erreur de division ».

### PB-87 — Au repli de l'IP, le préfetch du 8086 lit 64 Ko plus loin

1. **Défaut.** `cpu_state.pc` n'est masqué qu'en fin d'instruction (`808x.c:3910`). Une instruction qui
   commence en FFFFh porte `pc` à 10000h. La lecture d'un octet suivant, file vide, se fait alors à
   `cs + 0x10000`. **Effet** : l'octet décodé vient d'ailleurs ; le temps aussi (13 cycles contre 4 dans
   le cas du fuzzeur).
2. **Sites.** C : `808x.c:150` (le préfetch du 8086, l'entrée). **Élargissement, déduit à la lecture** :
   `808x.c:145`, la lecture principale de `FETCH` file vide, a le même défaut, **sur le 8088 comme sur
   le 8086**. Un `JMP` vers une instruction de deux octets ou plus en FFFFh lit son deuxième octet à
   `cs + 0x10000`. Les remplissages de `FETCHADD` (`:184`, `:191`) et de `FETCHCOMPLETE` (`:215`, `:221`) passent par
   `prefetchpc`, 16 bits, et sont justes. C# : `808x.cs:183` (le préfetch) et `:174` (la lecture
   principale) ; masque de fin `808x.cs:3763`.
3. **Vrai comportement.** **Documenté** : « On the 8086, if sequential execution of instructions
   proceeds past offset 65,535, the processor fetches the next instruction byte from offset 0 of the
   same segment » (386 PRM § 14.7, item 8).
4. **Correction.** `cs + (cpu_state.pc & 0xFFFF)` aux deux lectures : 2 lignes plus gardes. **Chemin :
   OUI**, `FETCH` est le site le plus chaud du cœur 8088, appelé à chaque octet d'instruction, en
   `AggressiveInlining`. Avec le stockage « figé » de D7, la garde se plie ; sinon, un masque par
   variable statique (`pc & masque`, FFFFFFFFh en mode PCem) coûte un ET par octet. À mesurer (D7 § 4.4,
   `tools/perfbanc`). **Temps** : oui, au repli seulement (un autre octet décodé). **État** : la file, pc,
   les cycles.
5. **Vérification.** Banc dirigé C# seul, attentes documentées : une instruction de 2 à 4 octets posée
   en FFFFh après un saut (file vide), sur le 8088 et le 8086 ; l'octet lu doit être celui de l'offset 0.
   SST ne l'exerce vraisemblablement pas (§ 2.2) ; compter les cas du corpus 8086 qui franchissent FFFFh.
6. **Classement : (a)**, chemin chaud.
7. **Dépendances.** PB-51 (même famille : le 8086 replie, le 286 et suivants lèvent #GP). Un voisin non
   inscrit : le **mot de données à l'offset FFFFh** sur le 8088. `readmemw`/`writememw` lisent `s+a` et
   `s+a+1` en linéaire (`808x.c:73-80`, `:105-111`), là où le 8086 replie à l'offset 0 (386 PRM § 14.7,
   item 7 : documenté). Ce site est chaud aussi.

### PB-39 — CALL ou JMP par une porte de tâche lève #GP

1. **Défaut.** Les `case 0x100` et `0x900` (« Task gate ») sont les **TSS disponibles** ; la porte de
   tâche (type 5, `0x500`) tombe dans le `default` : #GP. **Effet** : un changement de tâche par
   `CALL FAR` ou `JMP FAR` sur une porte de tâche échoue en #GP ; par INT, par IRET/NT ou par TSS
   directe, il réussit.
2. **Sites.** C : `x86seg.c:761-773` (loadcsjmp ; `default` `:775-779`), `:1284-1291` (loadcscall ;
   `default` `:1293-1296`). Le modèle de la correction est le `case 0x500` de `pmodeint`
   (`:1963-1997`). C# : `x86seg.cs:722` (loadcscall), `:2537` (loadcsjmp).
3. **Vrai comportement.** **Documenté** : « a JMP or CALL instruction can refer either to a TSS
   descriptor or to a task gate » (386 PRM § 7.5), et les pseudo-codes TASK-GATE de CALL et de JMP.
   **Contradiction documentée** pour l'exception d'un DPL insuffisant : #TS(gate selector) dans la page
   CALL, #GP(gate selector) dans la page JMP du même manuel (le SDM écrit #GP). Les autres vérifications
   (TSS dans la GDT, dans la limite de la GDT, disponible, présente) sont communes aux deux pages.
4. **Correction.** Un `case 0x500` dans les deux fonctions, appelant une aide commune. L'aide vérifie le
   DPL de la porte, sa présence, le sélecteur de TSS (TI=0, dans la limite), le type (1 ou 9) et la
   présence, puis `taskswitch286`. Environ **40 lignes** d'aide (fichier `*.Materiel.cs` selon D7) et
   2×3 lignes de `case`. **Chemin** : non. **Temps** : PCem ne compte aucun cycle de commutation sur la
   voie TSS (seulement le CALL/JMP de base) ; la porte ferait de même. Les temps documentés (« ts » des
   tables de CALL/JMP du 386 PRM) relèvent d'une décision séparée. **État** : CS, EIP, TR, registres,
   TSS en mémoire, NT, bits occupés.
5. **Vérification.** pm-check : le cas « CALL FAR porte de tâche → #GP (PCem : type 5 non géré) »
   reçoit en mode matériel l'attente documentée (commutation vers la TSS n° 2, lien arrière, NT=1, bits
   occupés). Ajouter « JMP FAR porte de tâche » (sans imbrication, NT inchangé) et deux cas d'erreur.
   Aucun oracle matériel : SST est en mode réel.
6. **Classement : (a)** pour le chemin nominal ; **(b)** pour les codes d'erreur (documentation
   contradictoire).
7. **Dépendances.** PB-40 : un CALL par porte devra lui aussi sauter les empilements. Voisins non
   inscrits (§ 5.3) : la voie TSS directe ne contrôle ni le DPL ni la présence de la TSS
   (`x86seg.c:1284-1291`) ; un JMP de tâche **efface** NT (`:768`) là où Intel le laisse inchangé
   (386 PRM § 7.6).

### PB-40 — CALL FAR sur une TSS empile l'adresse de retour sur la pile de la nouvelle tâche

1. **Défaut.** `CALL_FAR_w/_l` empilent CS et IP **après** `loadcscall`, qui a déjà changé de tâche.
   **Effet** : ESP de la nouvelle tâche = ESP chargé − 8 (−4 en 16 bits), deux mots écrits sous sa pile
   (mesuré : 0x6FF8 au lieu de 0x7000). Si cette pile est invalide, l'abandon remet `CS = old_cs` dans
   l'état de la nouvelle tâche.
2. **Sites.** C : `x86_ops_call.h:3-49` (CALL_FAR_w), `:51-96` (CALL_FAR_l), appelés `:110`, `:128`,
   `:199`, `:315`, `:432`, `:550`. C# : `386_ops_call.cs:56`, `:121`.
3. **Vrai comportement.** **Documenté** : rien n'est empilé. Le lien arrière de la nouvelle TSS reçoit
   le sélecteur de l'ancienne et NT est posé ; le retour se fait par IRET (386 PRM § 7.6). Les
   pseudo-codes TASK-GATE et TASK-STATE-SEGMENT de CALL n'empilent rien.
4. **Correction.** Un drapeau « commutation faite » posé par la voie TSS (et par la porte, PB-39) de
   `loadcscall`, testé dans les deux macros avant les empilements : ~10 lignes. Les `PREFETCH_RUN` des
   appelants comptent encore deux écritures : à ajuster (temps, quelques cycles). **Chemin** : non (voie
   du mode protégé de CALL FAR seulement). **État** : ESP, mémoire de la pile.
5. **Vérification.** pm-check « CALL FAR TSS 386 » : ESP 0x7000 en mode matériel (attente actuelle
   0x6FF8 en mode PCem).
6. **Classement : (a).**
7. **Dépendances** : PB-39.

### PB-41 — Une TSS 16 bits pose les moitiés hautes des registres à FFFF

1. **Défaut allégué.** `taskswitch286`, branche 16 bits : `EAX = new_eax | 0xFFFF0000` (… EDI).
   **Effet** : après une entrée dans une tâche 286 sur un 386, EAX…EDI valent FFFFxxxx.
2. **Sites.** C : `x86seg.c:2817-2824`. C# : `x86seg.cs:2294`.
3. **Vrai comportement : inconnu.** **Documenté** que les moitiés hautes ne sont **pas** conservées :
   « When the general-purpose registers are loaded or saved from a 16-bit TSS, the upper 16 bits of the
   registers are modified and not maintained » (SDM vol. 3, § 7.6 « 16-Bit Task-State Segment »). La
   valeur n'est pas dite. Bochs (`cpu/tasking.cc`) pose 0xFFFF, comme PCem, sans dire d'où il le tient.
   L'entrée PB-41 (« ne devrait toucher que les seize bits bas ») contredit Intel.
4. **Correction : aucune en G13.** La correction que suggère l'entrée (garder les moitiés hautes) serait
   fausse au regard d'Intel. À mesurer : sur un 386DX réel, un JMP vers une TSS 286 avec EAX = 12345678h
   avant, puis lecture d'EAX dans la nouvelle tâche.
5. **Vérification** : la mesure ci-dessus ; l'attente de pm-check (EAX 0xFFFF1111) reste.
6. **Classement : (c)**, laissé reproduit. Il faudrait **amender l'entrée** : « conforme à Bochs ; Intel :
   modifiés ; valeur exacte inconnue ».
7. **Dépendances** : aucune.

### PB-43 — MOV CRx, DRx et TRx décodent le champ `mod` comme une adresse

1. **Défaut.** Chaque handler commence par `fetch_ea_16/32`. Pour mod ≠ 3, il consomme un déplacement
   (et un SIB en 32 bits). **Effet** : longueur d'instruction fausse (`0F 20 05 …` avance de 4 octets de
   trop en 32 bits) ; jamais pour un code réel.
2. **Sites.** C : `x86_ops_mov_ctrl.h:9`, `43`, `78`, `90`, `105`, `157`, `208`, `220`, `233`, `245`,
   `258`, `269`. C# : `386_ops_mov_ctrl.cs:47`, `89`, `134`, `151`, `180`, `239`, `296`, `315`, `332`,
   `349`, `366`, `383`.
3. **Vrai comportement.** **Documenté pour l'architecture IA-32** : « The 2 bits in the mod field are
   ignored » (SDM vol. 2, MOV vers/depuis les registres de contrôle, et de débogage). Le 386 PRM
   (page « MOV — Move to/from Special Registers ») dit seulement « The two bits in the [mod] field are
   always 11 », ce qui est une règle d'encodage. Pour le 386 et le 486 précisément, **déduit**. Les TRx
   (absents du SDM) : déduit par analogie.
4. **Correction.** Une aide « ModRM registre » (cpu_reg, cpu_rm sans déplacement, ~6 lignes) appelée à
   la place de `fetch_ea_*` sous la garde dans les 12 handlers (~12 à 24 lignes). Passer `rmdat | 0xC0` à
   `PREFETCH_RUN`, qui compte sinon les octets de déplacement (`386_dynarec.c:155-203`). **Chemin** : non.
   **Temps** : oui, légèrement (le compte de préfetch). **État** : pc, `prefetch_bytes`.
5. **Vérification.** Banc dirigé C# seul : `0F 20 05` puis un NOP en 32 bits, `0F 20 06` en 16 bits ;
   l'instruction doit faire 3 octets, avec CR0 dans EBP (resp. ESI). Pas de corpus matériel
   (`0F 20`–`0F 26` absents de SST 386). Le fuzzeur `--0f 20…26` en mode matériel divergera de l'oracle
   sur ces seules formes (localisation).
6. **Classement : (b).**
7. **Dépendances** : absorbe PB-44.

### PB-44 — Les formes a32 de MOV DRx,r et MOV TRx,r décodent en 16 bits

1. **Défaut.** `opMOV_DRx_r_a32` et `opMOV_TRx_r_a32` appellent `fetch_ea_16`. **Effet** : avec mod ≠ 3,
   une autre longueur d'instruction.
2. **Sites.** C : `x86_ops_mov_ctrl.h:220`, `:269`. C# : `386_ops_mov_ctrl.cs:313`, `:382`.
3. **Vrai comportement** : celui de PB-43 (mod ignoré, quelle que soit la taille d'adresse) : déduit.
4. **Correction** : rien de propre. Corriger PB-44 seul (`fetch_ea_32`) décoderait encore une adresse,
   juste d'une autre longueur, aussi faux qu'avant. La correction de PB-43 rend PB-44 inobservable.
5. **Vérification** : les bancs de PB-43 avec le préfixe 67.
6. **Classement : (d)** en tant que tel ; résolu par PB-43.
7. **Dépendances** : PB-43.

### PB-50 — Aucune limite de longueur d'instruction

1. **Défaut.** Chaque préfixe aiguille l'octet suivant sans compter. **Effet** : une suite de préfixes
   de n'importe quelle longueur forme une instruction (mesuré : ~1,7 million de préfixes, 3,4 M cycles).
2. **Sites.** C : `x86_ops_prefix.h:3-165` (op_seg et ses variantes, op_66, op_67, et leurs formes
   REPE/REPNE), `x86_ops_rep.h:741-764` (REPNE, REPE), `x86_ops_misc.h:701-712` (LOCK). C# : le
   trampoline `TailCall`/`Dispatch` (`386_ops_prefix.cs:54`, `:62`), `PrefixeSegment` (`:86`), `op_66`
   (`:104`), `op_67` (`:118`), `opLOCK` (`:186`), REPNE/REPE (`386_ops_rep.cs`, après `:71`).
3. **Vrai comportement.** **Documenté** :
   - 386 : 15 octets, exception 13 (386 PRM § 14.7, item 6 : « Exception 13 occurs if the limit on
     instruction length is violated. The 8086/8088 has no instruction length limit ») ;
   - 286 : 10 octets, exception 13 (errata Intel « 80286 ARPL and Overlength Instructions » du
     15 octobre 1984 ; README SST 80286) ;
   - 486 : 15 octets, #GP (déduit, SDM) ;
   - 8088/8086 : aucune limite (correct chez PCem).
   La contradiction du README SST 80386 (10 octets, #6) est notée au § 2.1.
4. **Correction.** Deux niveaux :
   - (i) **le compte des préfixes** dans la boucle `while (r == TAIL)` de `Dispatch`. C'est le seul lieu
     où passent tous les préfixes du C# (une DEVIATION du port, utile ici) : si `pc − oldpc` dépasse la
     limite (10 ou 15), #GP(0). ~8 lignes. Ce niveau suffit au cas pathologique de PB-50 ;
   - (ii) **l'exactitude au seuil**. Les préfixes seuls peuvent rester sous la limite et le corps la
     franchir : 13 préfixes puis 4 octets font 17 octets. Sans préfixes redondants, aucune instruction ne
     la dépasse (déduit : au plus 4 préfixes distincts et 11 octets de corps, soit 15, sur le 386 ; au
     plus 8 octets sur le 286). Il suffit donc d'un **décodeur de longueur** (~80 à 120 lignes, piloté
     par tables) appelé seulement au-delà de N préfixes : coût nul ailleurs.
   **Chemin** : tiède (le trampoline ne tourne que sur les préfixes). **Temps** : non (voie d'exception).
   **État** : la voie d'exception.
5. **Vérification.** Le corpus **SST 80286**, dont le README annonce des cas au-delà de 10 octets
   (interruption 13) : s'ils y sont, la forme exacte du seuil est mesurée sur silicium. Banc dirigé 386/486 :
   14 préfixes + NOP (15 octets) passe, 15 préfixes + NOP lève #GP. Le corpus 386, s'il contient de tels
   cas, tranche entre 13 et 6.
6. **Classement : (a).**
7. **Dépendances.** PB-49 (non reproduit : l'ombre de SS bornée, où des préfixes s'intercalent).
   PB-51 (la mesure de PB-50 finissait hors du Mo). E1, le LOCK illégal (§ 5.2) : même trampoline.

### PB-51 — La lecture d'instruction ne contrôle pas la limite de CS

1. **Défaut.** La boucle d'`exec386` et `fastreadb/w/l` lisent `cs + pc` sans comparer `pc` à la
   limite. **Effet** : en mode réel, IP franchit FFFFh et l'exécution continue dans les 64 Ko suivants.
   Un vrai 286/386/486 lève #GP, soit INT 0Dh en mode réel. Sur un PC, c'est le vecteur de l'IRQ 5 : le
   gestionnaire du BIOS fait un IRET vers l'offset 0, puisque seul un IP 16 bits est empilé. Le vrai PC
   « semble » donc replier, là où PCem exécute ce qui suit : l'effet est observable.
2. **Sites.** C : `386.c:178` (la lecture d'opcode), `386_common.h:100-175` (`fastreadb/w/l`,
   `getbyte/word/long`). Les octets d'adresse lus par `fetch_ea` passent par les mêmes lectures. C# :
   `386.cs:369`, `386_common.cs:52-110` et `:560-577`. **Aucun marqueur.**
3. **Vrai comportement.** **Documenté** pour le 386 : « On the 80386, the processor raises exception 13
   in such a case » (386 PRM § 14.7, item 8) ; en mode protégé, #GP(0) si l'instruction sort de la
   limite (pseudo-codes de JMP/CALL : « Instruction pointer must be within code-segment limit »). Pour
   le 286 en mode réel : **déduit** (OS/2 Museum, M. Necasek, 29 oct. 2022 : « Segment protection
   prevents accesses that wrap around the end of a segment, for both data and instructions »).
4. **Correction.** Deux niveaux :
   - (L1) un test par instruction dans la boucle, `pc > limite de CS` avant l'opcode : #GP(0). Il couvre
     « l'exécution séquentielle au-delà de FFFFh », pas l'instruction à cheval ;
   - (L2) l'exactitude : un test sur chaque octet lu (`getbyte/word/long`, les incréments de `fetch_ea`).
     Pour éviter le coût, un test par instruction « `pc` à moins de 15 octets de la limite » qui bascule
     dans une voie contrôlée.
   ~5 lignes (L1) ; ~20 à 30 lignes (L2). **Chemin : OUI**, chaque instruction. D7 propose la boucle
   dupliquée (`exec386_materiel`) ou le stockage figé. **Temps** : non. **État** : la voie d'exception.
5. **Vérification.** Banc dirigé C# seul, en mode réel : un NOP en CS:FFFFh. Attendu : INT 0Dh, IP
   empilé 0000h (selon OS/2 Museum) ; une instruction de 3 octets en FFFEh, #GP. En mode protégé : CS de
   limite 0FFFh, exécution jusqu'à la limite. Cas SST 286/386 en bord de segment : inconnu, à compter
   dans les vecteurs.
6. **Classement : (a)**, chemin chaud.
7. **Dépendances.** PB-87 (le pendant 8086), PB-50. Les familles E2/E3 (§ 5.2), le même contrôle pour
   les données : un chantier bien plus gros, sur le chemin mémoire.

### PB-77 — `cpu_features` n'est jamais remis à zéro (486)

1. **Défaut.** L'iDX4 pose `CPU_FEATURE_CR4 | CPU_FEATURE_VME` et rien ne l'efface. **Effet** : après un
   iDX4 dans le même processus, un i486DX accepte MOV CR4. **Dans iXtal, aucun effet pour l'invité** :
   l'écran de construction n'a pas de ligne de processeur, et l'UC est fixée par processus. Seuls les
   outils qui rejouent `cpu_set` sont touchés (`CpuConfigCheck.cs:40` fixe l'ordre pour ça).
2. **Sites.** C : `cpu.c:483-485` (et `:316`, où `cpu_CR4_mask` est, lui, remis à zéro). C# :
   `cpu.cs:476-480`.
3. **Vrai comportement.** **Documenté** : « Control register CR4 was introduced in the Pentium
   processor » (SDM vol. 3B, ch. 22 « Architecture Compatibility », p. 22-17). Un i486DX n'en a pas, et
   MOV CR4 y est #UD (**déduit** : registre de contrôle inexistant). Le masque de l'iDX4 nomme VME deux
   fois (`cpu.c:485`) : l'intention exacte est inconnue, et le masque (VME, PVI) est plausible.
4. **Correction.** `cpu_features = 0` en tête du `switch` de `cpu_set`, sous la garde : 1 à 3 lignes.
   **Chemin** : non (initialisation). **Temps** : non. **État** : `cpu_features` dans l'empreinte UC.
5. **Vérification.** `cpu-config-check` en mode matériel, **C# seul** (l'oracle garde son héritage) : les
   empreintes de chaque entrée identiques dans les deux ordres. Un banc : MOV CR4 sur i486DX → INT 6.
6. **Classement : (a)**, valeur faible.
7. **Dépendances** : aucune.

### PB-78 — LOADALL386 s'exécute sur un 486

1. **Défaut.** Une seule table 0F pour 386 et 486 ; `0F 07` y est `opLOADALL386`, sans garde. Les
   instructions propres au 486 ont, elles, leur garde `!is486` (INVD, WBINVD, CMPXCHG, XADD :
   `x86_ops_misc.h:808-823`, `x86_ops_atomic.h:3-9`, `:199-205`). **Effet** : un 486 émulé charge l'état
   entier depuis ES:EDI au lieu de #UD.
2. **Sites.** C : `cpu.c:231` (`x86_setopcodes(ops_386, …)` pour tous), `386_ops.h:1246` (l'entrée
   `0F 07`), `x86_ops_misc.h:930-974`. C# : `386_ops_0f.cs:810` (handler), `:879` (table), **sans
   marqueur**.
3. **Vrai comportement.** « First of all, the 486 does not have a LOADALL instruction » (R. Collins,
   *The LOADALL Instruction*, rcollins.org : source secondaire). #UD : **déduit** (opcode absent de la
   carte du 486). Une source non sourcée (Wikipédia) dit le LOADALL du 386 exécutable sur
   486 « en SMM seulement ». Hors ICE et SMM, l'issue reste #UD (déduit).
4. **Correction.** En tête d'`opLOADALL386` : `is486` → `pc = oldpc`, `x86illegal()`. ~4 lignes. Se pose
   dans le handler (fichier écrit à la main), pas dans la table générée. **Chemin** : non. **Temps** :
   non. **État** : la voie d'exception.
5. **Vérification.** Banc dirigé C# seul : 0F 07 sur l'ami486 → INT 6 ; sur l'ami386 → LOADALL
   inchangé. Le fuzzeur du 486 en mode matériel pourrait réintégrer `0F 07`, qu'il écarte aujourd'hui à
   cause de PB-79.
6. **Classement : (b)** (vrai comportement déduit, sans mesure).
7. **Dépendances.** PB-79 (non reproduit, R9) : la porte d'entrée par LOADALL disparaît. Voisin non
   inscrit : `opLOADALL386` n'a **aucun contrôle de privilège**, alors qu'en mode protégé, à un autre
   niveau de privilège que 0, le 386 lève l'exception 13 (Collins, page LOADALL de rcollins.org).

---

## 4. Section C — par PB

### PB-11 — `readmemw` compare un offset 16 bits à une adresse linéaire

- **Défaut et effet.** `808x.c:74-75` ; C# `808x.cs:111-115`. `memcycs` est facturé sur toute lecture
  mot, y compris une lecture « auto-référentielle » que `readmemb` épargnerait. L'effet porte sur
  quelques cycles, sur des cas rares. **Il ne touche pas la fréquence de l'INT 8** (§ 2.1).
- **Vrai comportement.** Le coût en cycles d'une lecture mot du 8088 : **documenté** par les traces de
  cycles de SST v2. Mais le modèle de BIU de PCem est approché, et ces traces ne sont pas comparées
  (`VERIFICATION.md` § M5.0).
- **Une correction en mode matériel a-t-elle un sens ?** Non, isolément : elle rendrait le modèle de PCem
  cohérent avec lui-même, pas avec le silicium, et aucune mesure ne départagerait. Elle n'en aurait que
  dans un chantier « temps du 8088 contre les traces SST v2 », hors G13. **Classement : (d)** en G13.
  Ce n'est pas un « nettoyage sans effet » : l'effet existe, en temps.

### PB-12 — REP MOVSB oublie `memcycs = 0` en tête de boucle

- **Défaut et effet.** `808x.c:970-987` (comparer `:996`, REP MOVSW) ; C# `808x.cs:775`. Dès la
  troisième itération, `FETCHADD(17 − memcycs)` reçoit un argument négatif et rend aussitôt. La file ne
  se remplit plus pendant un long REP MOVSB : quelques cycles après l'instruction, aucun effet sur les
  données.
- **Vrai comportement** : l'état de la file et les cycles pendant un REP MOVSB du 8088 sont mesurés par
  SST v2 (traces et file finale), mais non comparés.
- **Sens d'une correction** : même réponse que PB-11. **Classement : (d)** en G13 ; à verser au même
  chantier de temps.

### PB-32 — `pmodeint` : la précédence annule le code d'erreur

- **Défaut et effet.** `x86seg.c:1659` ; C# `x86seg.cs:1115`. `(num * 8) + 2 + (soft) ? 0 : 1` vaut
  toujours 0. Un INT n au-delà de la limite de l'IDT lève #GP avec un code nul au lieu de n×8+2
  (+1, le bit EXT, pour un événement externe). L'effet est observable par un gestionnaire qui lit le
  code ; aucun BIOS ni DOS du dépôt ne le fait.
- **Vrai comportement : documenté.** « The processor sets the I-bit (IDT-bit) if the index portion of
  the error code refers to a gate descriptor in the IDT » ; « The processor sets the EXT bit if an event
  external to the program caused the exception » (386 PRM § 9.7 « Error Code »). Les deux autres sites
  de la fonction écrivent bien `(num * 8) + 2` (`:1687`, `:1692`).
- **Sens d'une correction : oui**, une ligne sous la garde. **Classement : (a).** Vérification : un cas
  pm-check avec une IDT courte, INT 20h, un gestionnaire de #GP qui lit son code → 0102h.
- **Voisins non inscrits** (§ 5.3) : EXT n'est jamais posé, même pour une interruption matérielle (`:1687`,
  `:1692`) ; le test de limite est `addr >= idt.limit` (`:1648`) au lieu de `addr + 7 > limite`.

### PB-42 — Le bit occupé de la nouvelle TSS est cherché dans la table de l'ancienne

- **Défaut et effet.** `x86seg.c:2429-2440`, `:2649-2660` ; C# `x86seg.cs:1894`, `:2116`. Aucun effet
  pour un programme valide : une TSS ne vit que dans la GDT (« TSS descriptors may reside only in the
  GDT. An attempt to identify a TSS with a selector that has TI=1 … results in an exception », 386 PRM
  § 7.2 : documenté).
- **Mais il est atteignable** par un programme invalide, parce que **LTR ne contrôle rien**
  (`x86_ops_pmode.h:230-262`) : il lit toujours la GDT, garde `tr.seg = sel` avec son bit TI, et ne teste
  ni le type, ni le bit occupé, ni la présence, ni la limite. Le vrai 386 lève #GP(sélecteur) ou
  #NP(sélecteur) (386 PRM, page LTR : documenté). Avec TI=1, `tr.seg & 4` désigne la LDT, et le bit
  occupé est cherché dans la mauvaise table.
- **Sens d'une correction.** Corriger PB-42 seul est du **nettoyage** : `seg & 4` vaut toujours 0 une
  fois LTR et la voie TSS contrôlés. Le correctif utile est celui de LTR (non inscrit, ~25 lignes), avec
  le contrôle « TSS dans la GDT » de la voie CALL/JMP. **Classement : (d)** pour PB-42 lui-même.

---

## 5. Les formes divergentes qu'aucun PB ne couvre

### 5.1 Le 8088 — `sst-baseline.tsv` (84 formes ; 19 déviantes)

| Forme | Réussite (PCem) | Défaut | Lu dans le C | PB |
|---|---|---|---|---|
| `10`–`15`, `18`–`1D` | 9 515 à 9 719 / 10 000 | AF d'ADC/SBB | `808x.c:786`, `:817`, `:849`, `:882` | **PB-01** |
| `27` DAA | 9 936 | AL faux (« AX = 0x3604, attendu 0x36A4 ») | le seuil compare l'AL **après** l'ajustement bas à 9Fh (`808x.c:1602`) ; le cas [20] est compatible avec un 8088 qui compare l'AL d'origine (déduit, à instruire) | aucun |
| `2F` DAS | 9 814 | idem | `808x.c:1675` | aucun |
| `AD` REP LODSW | 1 004 / 1 515 | AX jamais chargé | `808x.c:1131` : `tempw2 = readmemw(ds, SI)`, jamais copié dans AX ; REP LODSB idem (`:1112`, `temp2`) | aucun |
| `D0.6` SETMO | 32 | l'opérande devrait valoir FFh | PCem traite `/6` comme SHL (`808x.c:2798-2799`, et `D1`/`D2`/`D3` `/6`) ; SETMO/SETMOC sont « undocumented » dans `metadata.json` | aucun |
| `D2.4` SHL b,CL | 2 648 / 5 000 | OF | aucune ligne de SHL/SHR/SAR par CL ne touche `V_FLAG` (`808x.c:3068-3120`, `:3219-3270`) ; SST ne masque que AF | aucun |
| `F6.6`, `F7.6` DIV | 4 802 ; 4 905 | quotient tronqué au lieu d'INT 0 | `808x.c:3571` (et `F7` `:3720`) ne teste que le diviseur nul | aucun |

**Prédites par lecture, hors manifeste (à mesurer)** : `D3.2`, `D3.3` (PB-02) ; `F6.7` (PB-45 et
débordement) ; `F7.7` (débordement ; le cas 8000h) ; `AC` (REP LODSB) ; `D2.5`, `D2.7`, `D3.4`,
`D3.5`, `D3.7` (OF par CL) ; `D1.6`, `D2.6`, `D3.6` (SETMO/SETMOC). Les IDIV précédés de REP (négation
du quotient, README SST). Et deux défauts de repli que SST n'exerce pas (§ 2.2) : la lecture principale
au repli de l'IP (PB-87 élargi) et le mot de données à l'offset FFFFh.

**Proposition** : inscrire ces familles comme PB avant de corriger quoi que ce soit (U0) — le
débordement de DIV/IDIV (avec le 80h/8000h et la négation sous REP), DAA/DAS, REP LODS, SETMO/SETMOC,
l'OF des décalages par CL, le repli des mots à FFFFh. Toutes sauf le dernier sont vérifiables par SST.

### 5.2 Le 386 — `sst386-baseline.tsv` (941 formes ; 742 déviantes ; 284 449 échecs)

**Aucune de ces 742 formes n'est couverte par un PB du domaine.** PB-01 ne concerne que `808x.c` ;
PB-50 et PB-51 ne correspondent à aucune forme identifiable du corpus. Familles (`VERIFICATION.md` § G2,
classement par le premier champ divergent) :

| Famille | Échecs | Formes | Ce qui diverge | Lu à la ligne ici |
|---|---:|---:|---|---|
| E2 limite, adressage 32 bits en mode réel | 75 858 | 194 | une adresse au-delà de FFFFh doit lever #GP/#SS | **non** ; documenté (386 PRM, chaque page : « Interrupt 13 if any part of the operand would lie outside of the effective address space from 0 to 0FFFFH ») ; PCem n'a dans les handlers que `SEG_CHECK_READ` (segment nul), pas `CHECK_READ` |
| E1 LOCK illégal | 13 860 | 125 | #UD attendu | **oui** : `opLOCK` ne refuse que `LOCK NOP` (`x86_ops_misc.h:707`) ; documenté (386 PRM, page LOCK, et § 14.7 item 9) |
| E3 accès à cheval sur FFFFh | 1 838 | 48 | #GP attendu | non ; documenté (386 PRM § 14.7 item 7) |
| F1 drapeaux arithmétiques | 104 650 | 81 | en majorité des drapeaux **indéfinis** (SHLD/SHRD, BT*, BSF/BSR, IMUL) | **oui, pour les définis** : AF d'ADC (`x86_flags.h:299-307`) ; CF de BT* : décalage non signé (`x86_ops_bit.h:8`, `regs[].w / 16` au lieu d'un décalage signé, documenté 386 PRM) ; SF/ZF d'AAD/AAM calculés par `setznp16(AX)` au lieu d'AL (`x86_ops_bcd.h:17-39`), d'où `D5` à 50,2 % |
| F2 EFLAGS bits 18-31 | 3 228 | 2 | `669D` POPFD, `66CF` IRETD, à 0 % | non |
| R registres | 79 187 | 285 | ECX sous a32/REP, ESP (POPAD, ENTER), EAX (MUL/IMUL)… | **en partie** : `0FBF` (2,7 %) et `670FBF` (2,3 %) = MOVSX r16,r/m16, que PCem traite en ILLEGAL (`386_ops.h:1432`) ; `37`/`3F` AAA/AAS à la 8086 (`AL += 6; AH++`, `x86_ops_bcd.h:3-15`) là où le SDM écrit `AX := AX + 106H` |
| M mémoire | 5 828 | 7 | `669C` PUSHFD (0 %) ; DIV/IDIV : les FLAGS empilés par #DE | non |

**L'AF d'ADC du cœur 386 est un défaut distinct de PB-01.** La formule est
`(res & 0xF) < (op1 & 0xF) || (res & 0xF) == (op1 & 0xF) && op2 == 0xFF`. Elle se trompe quand le quartet
bas de `op2` vaut Fh, avec une retenue entrante et `op2 ≠ FFh` (resp. FFFFh, FFFFFFFFh) : 3 840 des
131 072 triplets de 8 bits (2,93 %). Son SBB est juste (0 écart). Formes `10`–`15`, `6611`, `6613`,
`6615`, `80.2`–`83.2`, `6681.2`, `6683.2` ; touche le 286 aussi (même cœur). Les chiffres BCD valides
(≤ 9) ne l'exercent pas, contrairement à PB-01.

**Ce qui n'est pas un défaut à corriger**, ou pas sans décision : les drapeaux **indéfinis** de F1
(Intel n'en promet rien ; le 386EX les pose à sa façon, peut-être autrement qu'un 386DX : inconnu) ;
E2/E3, défauts réels mais dont la correction met un contrôle de limite sur **chaque accès mémoire**
(chemin chaud, gros chantier).

### 5.3 Lus mais hors de portée de SST (mode protégé), non inscrits

- **LTR** sans aucun contrôle (`x86_ops_pmode.h:230-262`) : voir PB-42.
- **CALL/JMP sur une TSS** : ni DPL ni présence contrôlés (`x86seg.c:1284-1291`, `:761-773`).
- **JMP de tâche** : NT effacé (`x86seg.c:768`), là où Intel le laisse inchangé (386 PRM § 7.6).
- **EXT** jamais posé pour un événement externe (`x86seg.c:1659`, `:1687`, `:1692`) ; limite de l'IDT
  testée par `addr >= idt.limit` (`:1648`).
- **LOADALL386** sans contrôle de privilège (`x86_ops_misc.h:930-974`).
- **AAM 0** dans le cœur 386 : base remplacée par 10 (`x86_ops_bcd.h:31-32`) au lieu de #DE (même famille
  que PB-46, côté 386).

---

## 6. Proposition d'étapes (le domaine UC)

Le principe de la série tient à chaque étape : le mode PCem doit rester **identique au bit près**, et
c'est la série entière qui le prouve (le code de l'émulateur change à chaque étape, même gardé). Le mode
matériel se vérifie à part, contre le silicium (SST) ou la documentation (bancs dirigés).

**U0 — Préparer, sans changer aucun comportement.**
- Marqueurs canoniques numérotés sur tous les sites du domaine (§ 2.1), références du registre remises
  à jour (PB-01, 02, 11, 12), l'en-tête de `x86_flags.cs`, l'imputation de −5,87 ppm, l'entrée PB-41
  amendée.
- Inscrire les familles mesurées sans PB (§ 5.1, et pour le 386 au moins l'AF d'ADC, LOCK, BT* signé,
  MOVSX r16, AAA/AAS, AAD/AAM, LTR) : chacune lue à la ligne, comme les autres entrées.
- Les vecteurs (une décision, car c'est l'extérieur) : les formes 8088 manquantes (§ 2.2), les corpus
  **8086** et **80286** ; deux sondes (`sst8086-probe`, `sst286-probe`, sur le patron de `Sst386Probe`
  et `Moo.cs`) ; des lignes de base PCem étendues (re-baser est une décision : M5.0).
- L'interrupteur et l'outillage du mode (D7), **sans aucune correction** ; la preuve du coût nul en mode
  PCem (listings du JIT ou perfbanc), en particulier sur `FETCH`, `setadc*`, la boucle d'`exec386`.
- *Vérif.* : série entière identique ; `check-oracle.sh` sans dérive ; nouvelles lignes de base PCem
  égales oracle = C#.

**U1 — Le 8088/8086, états d'instruction vérifiables par SST** (~50 lignes pour les PB, ~60 de plus pour les familles inscrites) : PB-01, PB-02,
PB-45 avec le débordement de DIV/IDIV ; puis, si inscrits, REP LODS, l'OF par CL, DAA/DAS,
SETMO/SETMOC.
- *Vérif.* : `sst-baseline-materiel.tsv`, où les formes visées montent à 100 % (ou à un résidu expliqué :
  `F6.7`, les 32 cas à diviseur nul) et où **aucune autre forme ne bouge** ; sst-diff en mode matériel
  contre l'oracle, qui doit diverger sur les seules formes visées ; une panne injectée (une correction
  retirée) fait redescendre sa forme ; série entière en mode PCem.

**U2 — Le fetch au repli, 8088/8086** (~10 lignes) : PB-87 élargi (et le mot à FFFFh si inscrit).
- *Vérif.* : banc dirigé C# seul (attentes documentées, 386 PRM § 14.7, items 7 et 8) ; mesure de coût
  sur `FETCH` et `readmemw` ; cas SST 8086 qui franchissent FFFFh, s'il y en a.

**U3 — Le décodage et la longueur, 286/386/486** (~40 lignes, plus ~100 si le décodeur de longueur est
retenu) : PB-43 (avec PB-44), PB-50, PB-78, PB-77 ; et, si inscrits, LOCK → #UD et MOVSX r16.
- *Vérif.* : SST 80286 (cas au-delà de 10 octets) ; SST 386 (`E1` vers 100 % si LOCK est corrigé ;
  `0FBF`) ; bancs dirigés 386/486 (15/16 octets, `0F 20 05`, `0F 07` sur 486) ; `cpu-config-check`
  C# seul dans les deux ordres.

**U4 — Le fetch borné, 286/386/486** (L1 ~5 lignes ; L2 ~25) : PB-51.
- *Vérif.* : banc dirigé (NOP en FFFFh → INT 0Dh avec IP 0000h ; mode protégé, limite de CS) ; mesure de
  coût (chaque instruction) ; cas SST 286/386 en bord de segment, s'il y en a. E2/E3 (les données) restent
  hors de l'étape sauf décision : ce sont des contrôles sur chaque accès mémoire.

**U5 — Le mode protégé** (~70 lignes) : PB-39, PB-40, PB-32 ; LTR contrôlé (rend PB-42 inatteignable,
puis nettoyage) ; si inscrits, le NT des JMP de tâche, EXT. PB-41 laissé reproduit (c).
- *Vérif.* : pm-check avec une **table d'attentes du mode matériel** tirée de la documentation
  (commutation par porte, ESP 0x7000, code d'erreur 0102h, LTR à TI=1 → #GP), à côté de la table PCem
  actuelle ; aucune attente là où la documentation se contredit (#TS ou #GP), sauf décision.

**Hors G13 (proposé)** : PB-11 et PB-12 (le temps du 8088, à traiter contre les traces de cycles de SST
v2, ensemble) ; les drapeaux indéfinis du 386 ; les familles R et M non instruites ; les temps de
commutation de tâche documentés.

---

## 7. Les risques

1. **Des corrections partielles pires que l'existant.** PB-45 sans le débordement donne un IDIV qui ne
   ressemble ni à PCem ni au 8088 ; PB-44 sans PB-43 de même. D'où les groupes ci-dessus.
2. **Les chemins chauds** : `FETCH` (PB-87, le mot à FFFFh), la boucle et les lectures d'`exec386`
   (PB-51), et dans une moindre mesure `setadc*` et le trampoline. Le coût nul en mode PCem dépend du
   mécanisme retenu (D7) ; le coût en mode matériel de PB-51 L2 et d'E2/E3 est inconnu et peut être réel.
3. **Quel silicium fait foi ?** SST 8088 = AMD D8088 ; 8086 = Intel P80C86A-2 ; 286 = Harris
   N80C286-12 ; 386 = Intel 386EX (cœur statique, bus 16 bits). Aucun ne garantit qu'un comportement non
   documenté (drapeaux indéfinis, SETMO, l'OF des décalages multiples) est celui de l'Intel 8088 ou du
   386DX qu'émulent les machines.
4. **Une documentation qui se contredit** : 386 PRM contre SDM (AAA ; #TS ou #GP pour une porte de
   tâche) ; README SST 80386 contre Intel (10 octets/#6 contre 15/#13). Pour le mode protégé, aucun
   oracle matériel n'existe dans le dépôt.
5. **L'oracle mal employé** : un diff à l'oracle en mode matériel rougit par construction. Les outils
   doivent le refuser, sauf en contrôle de localisation déclaré (D7).
6. **Le temps** : PB-87, PB-40 et PB-43 changent des cycles en mode matériel ; le modèle de temps reste
   celui, approché, de PCem. Un « mode matériel » n'est donc pas un « temps matériel ».
7. **La dette de texte** : le registre et les commentaires portent déjà quatre inexactitudes (les sites
   de PB-02, l'en-tête de `x86_flags.cs`, l'imputation de −5,87 ppm, la prémisse de PB-41). Les corriger
   d'abord évite d'en hériter dans les marqueurs du mode.
8. **Le volume** : les familles non inscrites du 386 pèsent bien plus que les 13 PB du domaine. Les
   instruire toutes tient d'un bloc à part.

---

## 8. Les questions de principe à poser à Julien

1. **Le périmètre** : les seuls PB inscrits du domaine, ou aussi les écarts mesurés par SST sans entrée
   (8088 : débordement de DIV/IDIV, DAA/DAS, REP LODS, SETMO, l'OF par CL ; 386 : AF d'ADC, LOCK, BT*,
   MOVSX r16, AAA/AAS, AAD/AAM) — qu'il faudrait d'abord inscrire comme PB ?
2. **Les vecteurs** : récupérer les formes 8088 manquantes (voire les 324, ~726 Mo), les corpus **8086**
   et **80286**, et re-baser les lignes de base PCem sur ces formes nouvelles ?
3. **La référence matérielle** des comportements non documentés : le silicium mesuré par SST (AMD 8088,
   386EX…), ou rien d'autre que la documentation Intel ? Les drapeaux **indéfinis** sont-ils à copier ?
4. **PB-41** : amender l'entrée (Intel : moitiés hautes « modifiées, non maintenues » ; Bochs : FFFF) et
   le laisser reproduit, faute de mesure sur un 386 réel ?
5. **Le temps** : le mode matériel corrige-t-il des cycles (PB-11, PB-12, les temps de commutation de
   tâche), ou seulement des états, le temps étant renvoyé à un chantier contre les traces SST v2 ?
6. **Le coût toléré** en mode matériel pour PB-51 (L1 seul, ou L2 exact) et, si on les prend, pour E2/E3
   (un contrôle de limite sur chaque accès mémoire en mode réel) ?
7. **Les exceptions mal documentées** : pour la porte de tâche à DPL insuffisant (#TS selon la page
   CALL, #GP selon JMP et le SDM), retenir le SDM ?
8. **LOADALL386 sur 486** : retenir #UD (déduit ; Collins : « the 486 does not have a LOADALL
   instruction »), faute de mesure ?
9. **PB-77**, sans effet pour l'invité d'iXtal : le corriger quand même dans le mode, ou le classer
   « outils seulement » ?
10. **L'élargissement de PB-87** (la lecture principale du 8088) et le repli des mots à FFFFh : les
    verser à PB-87, ou en faire des entrées propres ?

---

## Annexe — Sources

**Intel**
- *Intel 80386 Programmer's Reference Manual* (1986), édition HTML :
  - § 14.7 « Differences From 8086 », items 6, 7, 8, 9 et 11 —
    https://pdos.csail.mit.edu/6.828/2008/readings/i386/s14_07.htm
  - § 7.2 (descripteur de TSS) — https://pdos.csail.mit.edu/6.828/2018/readings/i386/s07_02.htm
  - § 7.5 (commutation de tâche) — https://pdos.csail.mit.edu/6.828/2018/readings/i386/s07_05.htm
  - § 7.6 (chaînage des tâches) — https://pdos.csail.mit.edu/6.828/2018/readings/i386/s07_06.htm
  - § 9.7 « Error Code » — https://pdos.csail.mit.edu/6.828/2018/readings/i386/s09_07.htm
  - pages d'instructions CALL, JMP, LTR, LOCK, DIV, MOV (registres spéciaux) —
    https://pdos.csail.mit.edu/6.828/2018/readings/i386/CALL.htm, `JMP.htm`, `LTR.htm`, `LOCK.htm`,
    `DIV.htm`, `MOVRS.htm`
- *Intel 64 and IA-32 Architectures SDM* :
  - vol. 2, MOV vers/depuis les registres de contrôle et de débogage (« The 2 bits in the mod field are
    ignored ») — https://www.felixcloutier.com/x86/mov-1, https://www.felixcloutier.com/x86/mov-2
  - vol. 2, AAA (`AX := AX + 106H`) — https://www.felixcloutier.com/x86/aaa
  - vol. 3, § 7.6 « 16-Bit Task-State Segment » (p. 7-16 de l'édition rendue) —
    https://xem.github.io/minix86/manual/intel-x86-and-64-manual-vol3/o_fe12b1e2a880e0ce-254.html
  - vol. 3B, ch. 22 « Architecture Compatibility », p. 22-17 (CR4 introduit avec le Pentium) —
    https://xem.github.io/minix86/manual/intel-x86-and-64-manual-vol3/o_fe12b1e2a880e0ce-1021.html
- Errata Intel « 80286 ARPL and Overlength Instructions », 15 octobre 1984 —
  https://www.pcjs.org/documents/manuals/intel/80286/extra_prefixes/

**Mesures sur silicium (SingleStepTests)**
- 8088 v2 (AMD D8088 de 1982) : README et `v2/metadata.json` — https://github.com/SingleStepTests/8088
- 8086 (Intel P80C86A-2) — https://github.com/SingleStepTests/8086
- 80286 (Harris N80C286-12, mode réel) — https://github.com/SingleStepTests/80286
- 80386 (Intel 386EX, mode réel) — https://github.com/SingleStepTests/80386

**Autres**
- M. Necasek, « Does (E)IP Wrap Around in 16-bit Segments? », OS/2 Museum, 29 octobre 2022 —
  https://www.os2museum.com/wp/does-eip-wrap-around-in-16-bit-segments/
- Bochs, `bochs/cpu/tasking.cc` (TSS 16 bits entrante : moitiés hautes à 0xFFFF) —
  https://github.com/bochs-emu/Bochs/blob/master/bochs/cpu/tasking.cc
- R. R. Collins, *The LOADALL Instruction* — https://www.rcollins.org/articles/loadall/tspec_a3_doc.html
  (et https://www.rcollins.org/secrets/opcodes/LOADALL.html)
- Wikipédia, « LOADALL » (affirmation non sourcée sur le 486 en SMM) — https://en.wikipedia.org/wiki/LOADALL

**Dans le dépôt** : `PCEM_BUGS.md` (PB-01, 02, 03, 11, 12, 32, 39 à 45, 50, 51, 77, 78, 87) ;
`VERIFICATION.md` § « Où PCem s'écarte du silicium », § M4.6, § M5.0, § G2, § G1.0 ; `sst-baseline.tsv` ;
`sst386-baseline.tsv` ; `vectors/sst/MANIFEST.sha256`, `vectors/sst386/MANIFEST.sha256` ;
`tools/fetch-sst.sh`, `tools/fetch-sst386.sh` ; `tools/iXtal26.Diff/SstProbe.cs`, `PmCheck386.cs`,
`CpuConfigCheck.cs` ; `tools/perfbanc/` (non suivi) ; la lecture D7 (le mécanisme).
