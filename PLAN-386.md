# G2 — Le cœur 386 : le plan, sur mesures

> Écrit le 26 septembre 2026, au commit `a0cc3de`. Bloc G2 de `PLAN.md`. Les comptes
> d'emplacements sont faits par script sur les tables `OP_TABLE(…)` de
> `pcem-dev/includes/private/cpu/386_ops.h`, en comparant chaque emplacement de
> `ops_386` à celui de `ops_286`, déjà complet (`PLAN-286.md`). Les tailles sont des
> `wc -l`. Aucune ligne de code n'est encore écrite pour ce bloc.

## Où on en est, à l'emplacement près

| | |
|---|---:|
| `ops_386` : emplacements dont le handler existe déjà (partagé avec `ops_286`) | **574 / 1 024** |
| `ops_386` : emplacements à handler neuf | **450**, pour **300** handlers distincts |
| `ops_386_0f` : emplacements à handler neuf | **318 / 1 024**, pour **193** handlers distincts |
| `ops_REPE` / `ops_REPNE` en 386 | quadrants 1 à 3 à remplir (partagés entre générations, cf. A10) |
| Pagination (`mem.c` : `mmutranslate*`, `flushmmucache*`, `page_lookup`) | **omise** (`Memory/mem.cs`, en-tête) |
| `taskswitch286` (TSS 286 et 386, `x86seg.c:2393-2849`) | `pc.fatal` |
| V86 dans `x86seg.cs` | branches `VM_FLAG` présentes mais jamais exercées |
| Oracle : `h_state` | `eflags` est déjà le **mot haut** d'EFLAGS (`x86.h:113`, VM = `0x0002`) : VM et RF y sont. Manquaient `cr4` et `dr[8]` — **ajoutés en D0.1** |

Les tables `ops_386` et `ops_386_0f` servent **aussi au 486** : `cpu.c` fait
`x86_setopcodes(ops_386, ops_386_0f, …)` pour les deux. Les instructions propres au 486
(`BSWAP`, `CMPXCHG`, `XADD`, `CPUID`, `INVD`, `WBINVD`) sont dans `ops_386_0f`, gardées par
`if (!is486) x86illegal()`. Elles se transcrivent ici avec leur garde et ne s'exercent
qu'en G6.

### Où tombent les 450 emplacements neufs d'`ops_386`

Par quadrant : **0** (o16/a16) **7** · **1** (o32/a16) **141** · **2** (o16/a32) **109** ·
**3** (o32/a32) **193**.

Les sept du quadrant 0 sont exactement ce qu'un 386 change à un 286 en mode 16 bits :
`64`/`65` (préfixes FS/GS), `66`/`67` (préfixes de taille), `9D` `POPF`, `CF` `IRET`,
`F1` `INT1`. Tout le reste est la **même famille d'instructions, en formes `_l` (32 bits
d'opérande) et `_a32` (32 bits d'adresse)**.

## La bonne nouvelle : le silicium est disponible

**`SingleStepTests/80386` existe** (Daniel Balsom, v1.0.0), généré sur un vrai **386EX**
par ArduinoX86 :
- **941 fichiers, 576 Mo**, dans `v1_ex_real_mode/` ;
- **toutes les combinaisons de préfixes** : 236 fichiers `66…`, 321 `67…` (dont `6766…`),
  59 `0F…` ;
- de 100 à 2 500 tests par forme, avec l'état initial et final complet (registres,
  mémoire, exception) ;
- **format binaire `MOO`** (spécification et parseurs dans le dépôt `dbalsom/moo`), et non
  plus JSON comme le corpus 8088. Une liste de révocation (`revocation_list.txt`) est à
  appliquer.

**Ce qu'il ne couvre pas** — à dire dans chaque commit :
1. **Le mode réel seulement.** Les répertoires unreal, protégé et V86 sont annoncés
   « eventually », mais ils sont vides. Le mode protégé 32 bits, la pagination et le V86
   n'ont donc **que PCem pour oracle**, comme le bloc C du 286.
2. **Pas de FPU** : zéro fichier `D8` à `DF`.
3. **Les cycles d'un 386EX, bus 16 bits, SMM compris** : ils ne valent ni pour un 386DX
   ni pour le modèle de temps de PCem. On compare l'état, **pas les cycles**.
4. **Pas d'IF ni de TF**, et pas de file de préchargement.

C'est la situation du 8088 en M5.0 : PCem contre le silicium, avec des **formes
déviantes à recenser par famille**, et non à « corriger ». La règle de `PLAN-286.md`
vaut encore : **transcrire PCem, même quand il a tort**, et consigner l'écart dans
`PCEM_BUGS.md` et `VERIFICATION.md`.

> En passant : `SingleStepTests/80286` et `SingleStepTests/8086` existent aussi. Le
> premier n'a jamais été passé contre le cœur 286 ; ce serait un filet pour le reliquat
> du 286. Le second servira à G1.

## Le point que l'on sous-estime

**Le fuzzeur et le corpus SST ne voient ni les écritures en table ni la pagination.**
L'angle mort de `PLAN-286.md` § « Ce que le bloc C n'a PAS comme oracle » s'agrandit :
- les bits *accédé* et *modifié* des entrées de page s'écrivent en RAM, pendant une
  traduction, et **aucun compteur ne les compare** ;
- le cache de traduction (`readlookup2`/`writelookup2`), réécrit à chaque changement de
  CR3, est **le chemin chaud** de toute la machine. Une erreur y reste muette jusqu'au
  premier Windows en mode 386 étendu.

D'où la règle de ce plan : **la pagination n'entre qu'avec une sonde dédiée**, sur le
modèle de `pm-check`. Des tables de pages fabriquées en RAM, puis le résultat de
`mmutranslatereal` comparé des deux côtés, adresse par adresse, avec les bits A et D
relus en RAM après chaque accès.

---

## Les étapes

L'ordre est celui du 286 : **l'oracle d'abord**, puis le 32 bits là où le fuzzeur et le
silicium voient tout (le mode réel), puis ce qu'ils ne voient plus.

### D0 — L'outillage  ← **à faire avant la première ligne de cœur**

| # | Quoi | Pourquoi |
|---|---|---|
| D0.1 ✅ | `cr4` et `dr[8]` entrent dans `h_state` ; ABI 21 | `MOV CRx`/`MOV DRx` les écrivent. **Correction** : la première version de ce plan voulait élargir `eflags` à 32 bits — faux, c'est déjà le mot haut d'EFLAGS chez PCem. TR6/TR7 n'ont pas de stockage (`MOV TRx` ne fait que journaliser) |
| D0.2 ✅ | Porter `cpus_i386SX` (`cpu_tables.c`), `m_ami386` des deux côtés (init absente, **refus bruyant**), et `cpu_set()` sur un 386 : `x86_setopcodes(ops_386)` inconditionnel, temps du 386SX. `ops_386`/`ops_386_0f` remplies de leur part partagée par le fichier **généré** `386_ops_table386.cs` (`tools/ops386-table.py`). `cpus_i386DX` viendra avec l'ami386dx (G3) | Pose `is386`, les `timing_*` et `x86_setopcodes(ops_386…)`. Règle A3 : lier ne suffit pas, **`cpu_set()` pose les valeurs** |
| D0.3 ✅ | `Oracle.Core386` / `H_CORE_386`, un seul prédicat « exec386 » de chaque côté (`h_exec386`, `Oracle.Exec386`, `Oracle.CoreForModel`) ; fait avec D0.2 | Un site oublié renvoyait le 386 vers execx86 en silence |
| D0.4 | Le fuzzeur en `Core386` : préfixes `66`/`67` tirés, registres 32 bits aléatoires, tables tirées dans les quatre quadrants | `Fuzzer.cs` exclut aujourd'hui `66`/`67` « jusqu'au 386 » |
| D0.5 | `sst386-probe` : lecteur `MOO` en C# (dans `tools/iXtal26.Diff`), liste de révocation, `--baseline` comme `sst-probe` ; `tools/fetch-sst.sh` étendu au dépôt 80386 (`.gitignore`) | Le seul oracle silicium du bloc |
| D0.6 ✅ | `tools/ops386-table.py` (ce que PCem attend) et `iXtal26.Diff ops-count` (ce que la table C# vivante porte) — fait avec D0.2 | Le compteur « N / 1 024 » de chaque commit, comme pour A |

**Porte de D0** : les boot-diffs 8088 et 286 inchangés à l'unité ; `sst386-probe` tourne
et rend un rouge **nommé** sur chaque fichier (aucun handler 32 bits n'existe encore) ;
le fuzzeur `Core386` rend 100 % vert quand il est restreint au quadrant 0 sans `66`/`67`.

### D1 — Le décodage 32 bits

`x86_ops_prefix.h` (168) : `op_66`, `op_67`, les préfixes FS et GS. Dans `386_common.h`
(280) et `x86.h` (333) : `fetch_ea_32` (SIB, `disp32`), `geteal`/`seteal`, `getr32`,
`fastreadl`. **Rien ne s'exécute encore en 32 bits**, mais un `66 90` ou un `67 8B 00`
décode, et le fuzzeur le voit.

### D2 — Les familles déjà connues, en formes `_l` et `_a32`

Un en-tête par commit, dans l'ordre de A4 à A11. Chaque handler 16 bits est déjà relu,
donc la différence est mécanique, **sauf** là où la largeur change la sémantique :

| # | En-tête (lignes) | Point dur |
|---|---|---|
| D2.1 | `x86_ops_mov.h` (732), `x86_ops_mov_seg.h` (491) | `MOV Sreg` en `_l` ; `opMOV_seg` |
| D2.2 | `x86_ops_arith.h` (914) | `OP_ARITH` en 32 bits ; AF et CF sur `SBB` 32 |
| D2.3 | `x86_ops_stack.h` (626) | `PUSHAD`/`POPAD`, `stack32` contre `op32` : quatre combinaisons |
| D2.4 | `x86_ops_jump.h` (351), `x86_ops_call.h` (603), `x86_ops_ret.h` (230) | `JECXZ`, `LOOP` sur ECX sous `67` ; `CALL`/`RET` 32 bits |
| D2.5 | `x86_ops_flag.h` (253) | `PUSHFD`/`POPFD` : VM et RF (d'où D0.1) |
| D2.6 | `x86_ops_inc_dec.h` (99), `x86_ops_xchg.h` (232) | mécanique |
| D2.7 | `x86_ops_shift.h` (716) | rotations 32 bits, **plus `SHLD`/`SHRD`** |
| D2.8 | `x86_ops_string.h` (772), `x86_ops_rep.h` (766) | ESI/EDI/ECX sous `67` ; les quadrants 1 à 3 de `ops_REPE`/`ops_REPNE` |
| D2.9 | `x86_ops_io.h` (163), `x86_ops_int.h` (120) | `IN`/`OUT` 32 bits, `INT1` (`F1`) |
| D2.10 | `x86_ops_mul.h` (317), `x86_ops_misc.h` (1 041), `x86_ops_bcd.h` (107) | `IMUL` 32, `DIV` 64/32, `CWDE`/`CDQ`, `BOUND` 32 |

**Porte de chaque D2.x** : fuzzeur `Core386` vert sur les familles posées ; `sst386-probe`
vert sur leurs fichiers, **aux formes déviantes recensées près** ; boot-diffs 8088 et 286
à l'unité.

### D3 — Les groupes propres au 386

| # | En-tête (lignes) | Contenu |
|---|---|---|
| D3.1 | `x86_ops_movx.h` (227) | `MOVZX`, `MOVSX` |
| D3.2 | `x86_ops_set.h` (25), `Jcc` longs (`0F 80`–`8F`, dans `x86_ops_jump.h`) | `SETcc` ; sauts `rel16`/`rel32` |
| D3.3 | `x86_ops_bit.h` (398), `x86_ops_bitscan.h` (166) | `BT`/`BTS`/`BTR`/`BTC`, groupe `0F BA` ; `BSF`/`BSR`. **Piège** : l'adresse de bit hors de l'opérande en mémoire |
| D3.4 | `LFS`/`LGS`/`LSS`, `PUSH`/`POP FS`/`GS`, `IMUL r, r/m` (`0F AF`) | dans `x86_ops_misc.h` / `x86_ops_stack.h` / `x86_ops_mul.h` |
| D3.5 | `x86_ops_atomic.h` (322) et les 486 de `x86_ops_misc.h` | `CMPXCHG`, `XADD`, `BSWAP`, `CPUID`, `INVD`/`WBINVD`, **avec leur garde `is486`** ; vérifiés illégaux sur un 386, exercés en G6 |

**Jalon visible à la fin de D3** : le jeu d'instructions 386 complet en mode réel. Le
corpus `v1_ex_real_mode` doit être vert au recensement des déviations près.

### D4 — Les registres système

`x86_ops_mov_ctrl.h` (275) : `MOV CRx`/`DRx`/`TRx`. Les formes `_l` de `0F 00` et de
`0F 01`. `LOADALL386` (`0F 07`). `x86_ops_pmode.h` (519), ses formes 32 bits.
**Oracle** : le fuzzeur. Le corpus SST ne teste pas l'entrée en mode protégé.

### D5 — Le mode protégé 32 bits

- les branches `is32`/`use32`/`stack32` de `loadcscall`, `pmodeint`, `pmodeiret` et
  `pmoderetf`, **à recompter** contre `x86seg.c` avant d'écrire ;
- **`taskswitch286`** (`x86seg.c:2393-2849`), qui traite les TSS 286 **et** 386.

**Oracle** : `pm-check`, étendu au 386 (GDT et TSS 32 bits fabriquées par LOADALL386),
plus le fuzzeur en état protégé. **Pas de silicium.** Les écritures en table se
transcrivent à l'œil, et le commit le dit.

### D6 — La pagination

`mem.c` : `flushmmucache`, `flushmmucache_nopc`, `flushmmucache_cr3` (93-188),
`mem_flush_write_page`, `mmutranslatereal`, `mmutranslate_noabrt` (189-353), les branches
paginées d'`addreadlookup`/`addwritelookup` et de `readmem*l`/`writemem*l`, `page_lookup`.
**≈ 400 lignes**, sur le chemin chaud du 8088.

**Oracle** : la sonde dédiée décrite plus haut (`page-check`) ; et, **mesure obligatoire**,
la vitesse hôte du 8088 avant et après, puisque `readmembl` y passe.

### D7 — Le mode virtuel 8086

Les branches `VM_FLAG` d'`x86seg.cs` (déjà écrites, jamais exercées), et `IOPL` sur
`CLI`/`STI`/`PUSHF`/`POPF`/`INT`/`IRET`. **Oracle** : `pm-check` en V86.

---

## Porte de sortie de G2

Le cœur 386 est « fait » quand :
1. `ops_386` et `ops_386_0f` sont à **1 024 / 1 024** au script de D0.6 ;
2. `v1_ex_real_mode` est vert, avec des **déviations recensées** dans `VERIFICATION.md`
   et une ligne de base `sst386-baseline.tsv` ;
3. le fuzzeur `Core386` est vert dans les quatre quadrants, en mode réel et protégé ;
4. `pm-check` et `page-check` sont verts ;
5. les chiffres de `PLAN-286.md` § « Ce qui ne doit pas bouger » n'ont pas bougé d'une
   unité, et la vitesse hôte du 8088 est mesurée (M5.1).

La **preuve d'intégration** vient ensuite, avec G3 (`ami386`) : DOS, puis un gestionnaire
de mémoire (EMM386), puis Windows 3.1 en mode 386 étendu, qui exerce D5, D6 et D7 à la
fois.

## L'ordre

```
D0  outillage        ← oracle d'abord. NE PAS REPOUSSER
D1  décodage 32 bits
D2  familles connues, _l et _a32   (dix commits)
D3  groupes 386 seuls              ← jalon : jeu d'instructions complet en mode réel
D4  registres système
D5  mode protégé 32 bits + taskswitch286
D6  pagination                     ← sonde dédiée avant le code
D7  V86
```

## Ce que je ne sais pas encore

- **La lecture de `MOO` en C#** : le format est « simple et chunké », mais je ne l'ai pas
  lu. À lire avant D0.5 ; `moo2json.py` est le repli si le parseur coûte trop cher.
- **Combien de formes dévieront** entre PCem et le 386EX. Pour le 8088, il y en avait 19.
  Les drapeaux indéfinis (`BSF` à zéro, `SHLD` au-delà de 16, `MUL`/`DIV`) sont les
  suspects.
- **Si le 386EX diffère d'un 386DX** sur autre chose que les cycles et le SMM (limites de
  segment, `LOADALL` compris). À vérifier dans le README et les issues du dépôt.
- **Les lignes vives réelles de D5**, par branche `is32` : à mesurer par script, comme
  `PLAN-286.md` l'a fait pour `x86seg.c`.
- **Si le modèle de préfetch 386** (`prefetch_bytes`, `harness_386.c`) change avec `66`
  et `67` — le vecteur d'état le compare déjà.

## Ce qui ne doit pas bouger

Les chiffres de `PLAN-286.md` § « Ce qui ne doit pas bouger », à l'unité, à chaque
commit ; `check-oracle.sh` à zéro dérive ; `sst-probe --baseline` 8088 inchangé ;
`core286-check` et `pm-check` 286 verts ; zéro avertissement.
