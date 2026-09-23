# Amorcer l'IBM AT 5170 — le plan, sur mesures

> Écrit le 23 septembre 2026, au commit `e8475ba`. Tous les chiffres viennent
> d'une mesure faite ce jour-là, pas d'une estimation : les emplacements sont
> comptés sur `ops_286[]` lu dans la `.so` par gdb, les lignes vives par script.

## Où on en est, à l'emplacement près

| | |
|---|---:|
| Emplacements posés dans `ops_286[]` | **256 / 256** ✅ |
| Mode protégé (`x86seg.cs` vif / `x86seg.c` vif) | **~90 / 2 446** |
| La machine AT, côté C# | **rien** |
| L'oracle sait-il amorcer un AT ? | **non** |

Fait : `mov` (26), `arith` au sens large — `OP_ARITH`, CMP, TEST, groupe
immédiat (56), pile (28). Tout cela est vert au fuzzeur à chaque commit, et les
cinq chiffres du 8088 n'ont pas bougé d'une unité.

## Le point que l'on sous-estime

**L'oracle ne sait pas amorcer un AT.** `h_boot()` (`tools/oracle/harness.c`) est
câblé 8088/XT : il appelle `h_cpu_config_8088()` et monte les périphériques du
5150. Or ce dépôt ne pose pas une ligne sans oracle pour la comparer.

Donc **B2 ci-dessous n'est pas une étape parmi d'autres : c'est celle qui rend
toutes les suivantes vérifiables.** La faire tard reviendrait à écrire le mode
protégé et la machine AT à l'aveugle, puis à chercher la première divergence dans
deux mille lignes neuves d'un coup. C'est exactement ce que le palier (a) a évité
en construisant `boot-diff` avant d'en avoir besoin.

---

## A — La table d'opcodes  ✅ *FAIT, neuf commits de `cba30be` à `9c9ede2`*

**256 emplacements sur 256.** Le cœur exécute n'importe quelle instruction d'un
286 en mode réel, identique à PCem sur 80 000 tirages, avec 317 cas dirigés.

| | | |
|---|---:|---|
| A4 pile | 28 | `e8475ba` |
| A5 sauts, CALL/RET proches | 26 | `58e614e` |
| A6 appel et retour lointains, FF, ENTER/LEAVE | 7 | `2e4ff68` |
| A7 drapeaux | 11 | `47a4c56` |
| A8 INC/DEC et XCHG | 26 | `c0933d9` |
| A9 décalages, et opNOP | 7 | `3f1f500` |
| A10 chaînes | 14 | `fc9bbb0` |
| A11 les 55 derniers, onze en-têtes | 55 | `9c9ede2` |

### Ce que cette partie a appris, et qui vaut pour la suite

**Le fuzzeur a trouvé six défauts, aucun dans un handler.** Les 256 sont
individuellement corrects — relus, diffés jeton par jeton sur leur modèle de
temps, couverts par des cas dirigés. Ce qui cassait était la **plomberie entre
eux** : trois quadrants d'`ops_286` sur quatre laissés vides, les tables
`ops_REPE`/`ops_REPNE` partagées entre générations, deux familles d'instructions
à **coût nul** qui font repartir la boucle interne d'`exec386`, et deux handlers
qui exécutent eux-mêmes l'instruction suivante.

**Trois règles, chacune écrite dans le code là où elle a mordu :**

1. **« Inatteignable » se vérifie sur les APPELANTS, pas sur la fiche
   technique.** Trois fois : `PUSH_L`, `writememl`, les cas FS/GS de
   `MOV r/m, seg`.
2. **Un « complete » ne vaut que si le contrôle énumère TOUTES les catégories de
   définition.** `x86_flags.cs` a menti deux fois — six fonctions, puis deux
   macros.
3. **Lier une unité fournit les SYMBOLES, `cpu_set()` pose les VALEURS — et il ne
   tourne jamais dans le harnais.** Quatre fois : les vingt `timing_*` (A2.0),
   les tables `REPE`/`REPNE`, les huit tables d'échappement FPU.

**Et un quatrième réflexe, d'outillage** : un arrêt fatal du cœur est un
RÉSULTAT, pas un accident. Il doit nommer l'itération, les octets et les
registres — sans quoi trois passages ne montrent que le même message à trois
adresses différentes.

### Ce qui reste de la table : la SECONDE, celle à deux octets

`ops_286_0f` porte **six** handlers sur un 286, relevés dans la `.so` par gdb :
`op0F00_a16`, `op0F01_286`, `opLAR_w_a16`, `opLSL_w_a16`, `opLOADALL`, `opCLTS`.

**`op0F01_286` est le plus important de tout le jalon** : il porte `LGDT`,
`LIDT`, `SGDT`, `SIDT`, `SMSW` et `LMSW` — la porte par laquelle un 286 *entre*
en mode protégé, et celle que le POST de l'AT franchit pour dimensionner la
mémoire. Deux écarts déjà repérés : `SGDT`/`SIDT` forcent les huit bits hauts
de la base à 1 sur un 286 (`base |= 0xff000000`), et `SMSW` masque
différemment selon le processeur (`msw | 0xFFF0` sur un 286).

**`0x0F` est exclu du fuzz pour cette raison nommée**, et le handler d'échec
distingue désormais les deux tables dans son message.

## ~~A — Finir la table d'opcodes : 146 emplacements~~ *(historique)*

Par en-tête C, mesuré. L'ordre est celui de la dépendance, pas de la taille.

| # | Groupe | Empl. | En-tête C | Pourquoi là |
|---|---|---:|---|---|
| **A5** | `jump` — Jcc ×16, JMP ×3, LOOP, JCXZ | 26 | `x86_ops_jump.h` | **Le verrou.** Sans saut conditionnel, aucun BIOS ne boucle. Lit `CF_SET`/`ZF_SET`… : premier vrai client des drapeaux paresseux en lecture |
| **A6** | `call` / `ret` + ENTER/LEAVE | 7 | `x86_ops_call.h`, `_ret.h`, `_stack.h` | Avec A4 et A5, un sous-programme devient exécutable |
| **A7** | `flag` — PUSHF/POPF, CLI/STI, CLC/STC/CMC, CLD/STD, LAHF/SAHF | 11 | `x86_ops_flag.h` | PUSHF force `flags_rebuild` : le premier consommateur en volume |
| **A8** | `inc_dec` (17), `xchg` (9) | 26 | `x86_ops_inc_dec.h`, `_xchg.h` | Mécanique, sans surprise |
| **A9** | `shift` (6 slots, macros) | 6 | `x86_ops_shift.h` | `tempc` traverse les rotations : cas dirigés à deux instructions obligatoires |
| **A10** | `string` (14) + `rep` (2) | 16 | `x86_ops_string.h`, `_rep.h` | `REP` boucle **dans** le handler ; modèle de temps propre |
| **A11** | `io` (8) + `int` (3) | 11 | `x86_ops_io.h`, `_int.h` | INT/IRET : la porte d'entrée du BIOS |
| **A12** | `misc` (10), `mul` (2), `bcd` (6), `mov_seg` (3), `mov` (2), `pmode` (1) | 24 | divers | Le reste |
| **A13** | `fpu` (9) — ESCAPE | 9 | `x86_ops_fpu.h` | Un AT sans 287 les traite en illégaux ; à vérifier avant d'écrire |

**Cadence observée** : A3a→A3d ont posé 82 emplacements en une session, portes
comprises. A4 en a posé 28. Le reste est du même tissu, **sauf A9, A10 et A11**,
qui portent une vraie difficulté (boucles internes, modèle de temps, vecteurs).

**Ce qui ne change pas d'un commit à l'autre** : les trois angles morts établis
à A3 valent pour chacun de ces groupes —

1. le fuzzeur en mode simple ne voit rien de ce qu'une instruction laisse à la
   suivante ⟹ cas dirigés à deux instructions (critique pour A5, A7, A9) ;
2. il vérifie les *poseurs* de drapeaux, pas les *lecteurs* ⟹ cas-piège par
   espèce, avec un jeu de valeurs qui déborde en signé ;
3. sur un 286 `timing_rm == timing_mr == 7` et `is486 == 0` ⟹ diff mécanique de
   jetons sur `CLOCK_CYCLES`/`PREFETCH_RUN`.

Le troisième mérite d'être **outillé** plutôt que refait à la main à chaque
groupe : un script qui lit les plages dans les commentaires `// pcem:` et
compare, donc auto-entretenu. À faire dès A5 — il a déjà trouvé six citations
fausses sur le seul `arith`.

---

## B — L'oracle et la machine AT

### B1 — `nvr.cs` et `keyboard_at.cs`

| | C | présent en C# |
|---|---:|---|
| `keyboard_at.c` — le 8042 | 858 | **non** |
| `nvr.c` — MC146818, CMOS + RTC | 802 | **non** |
| `dma.c` — `dma16_init` | — | **oui** |
| `pic.c` — `pic2_init` | — | **oui** |

Bonne nouvelle mesurée : `dma16_init` et `pic2_init` sont **déjà transcrits**,
bien que le XT ne s'en serve pas. Il reste deux fichiers.

Le 8042 n'est pas un clavier : il tient **la porte A20 et la ligne de reset**.
Le POST de l'AT s'en sert pour sortir du mode protégé — il n'y a pas d'autre
sortie sur un 286. Le MC146818 est lu avant tout le reste, et le POST s'arrête
sur une somme de contrôle fausse.

`ibm_at_init` (`model.c:335-351`) donne la liste exacte : `AT = 1`,
`common_init`, `mem_add_bios`, `pit_refresh_timer_at`, `dma16_init`,
`keyboard_at_init`, `nvr_device`, `pic2_init`, `gameport_device`,
`nmi_mask = 0`, puis `mem_remap_top_384k`.

### B2 — L'oracle amorce un AT  ← **à faire tôt**

Lier `keyboard_at.c` et `nvr.c` au harnais, ouvrir `h_boot` au modèle AT,
étendre `h_set_romset` à `ROM_IBMAT`. C'est ce qui rend B3, B4 et C
vérifiables ; sans lui, tout ce qui suit s'écrit à l'aveugle.

**Porte** : l'oracle seul atteint un point connu du POST de l'AT, et
`boot-diff --model ibmat` devient lançable — même s'il diverge à la première
instruction, il *nomme* où.

### B3 — La machine AT en C#

Le modèle, la carte mémoire, `mem_remap_top_384k`. Petit, une fois B1 fait.

---

## C — Le mode protégé

`x86seg.c` fait **2 446 lignes vives en 38 fonctions**. Les six qui comptent :

| Fonction | Lignes vives |
|---|---:|
| `taskswitch286` | 356 |
| `loadcscall` | 354 |
| `pmodeint` | 303 |
| `pmodeiret` | 300 |
| `pmoderetf` | 248 |
| `loadcsjmp` | 179 |
| `loadseg` (le reste à compléter) | 136 |

Plus `check_seg_valid` (32), `do_seg_load` (26), `x86_doabrt` (43).
**Hors périmètre 286** : `x86_smi_enter`/`leave` (170), `sysenter`/`sysexit`
(47), `cyrix_load_seg_descriptor` (24).

Soit **~1 950 lignes vives** pour le 286. C'est le plus gros bloc du jalon, et
le seul dont **le fuzzeur est le seul oracle** : pas de SingleStepTests en mode
protégé, pas de `boot-diff` tant que B2 n'est pas fait. À dire dans le commit,
pas à laisser deviner.

Prérequis d'outillage : apprendre au fuzzeur à fabriquer un état protégé valide
— GDT en RAM, `gdt.base/limit`, `cr0 |= 1`, rechargement des six sélecteurs.
5 à 8 entrées d'ABI neuves.

---

## L'ordre que je recommande

```
A5  jump          ← le verrou : rien ne boucle sans lui
A6  call/ret      ← un sous-programme devient exécutable
B2  oracle AT     ← rend tout le reste vérifiable. NE PAS REPOUSSER
A7  flag
A8  inc_dec/xchg
A9  shift
A10 string/rep
A11 io/int
A12 le reste
B1  nvr + keyboard_at
B3  machine AT C#
C   mode protégé
```

**B2 est placé en troisième position à dessein.** C'est la seule étape dont
l'absence rend les autres invérifiables, et elle ne dépend d'aucune des
précédentes — seulement du harnais.

## Ce que je ne sais pas encore

- **Combien d'instructions le POST de l'AT exécute avant d'entrer en mode
  protégé.** B2 le mesurera, et ce chiffre déciderait peut-être de remonter C
  dans l'ordre. Aujourd'hui je l'ignore, et je ne veux pas planifier dessus.
- **Si le 287 est nécessaire.** A13 le tranchera en lisant ce que `ops_286` met
  aux huit emplacements ESCAPE.
- **Le coût réel de C.** 1 950 lignes vives est une taille, pas une durée : le
  mode protégé est la partie la moins mécanique du jalon.

## Ce qui ne doit pas bouger

Les cinq chiffres du 8088, à l'unité, à chaque commit :

```
25 457 269 · 26 750 702 · 23 442 234 · 19 511 811 · 22 086 920
```

plus `check-oracle.sh` à zéro dérive, `selftest`, `--setup-check`, zéro
avertissement, et le fuzzeur 8088 sur ses 60 champs.
