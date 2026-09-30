# iXtal26 — la feuille de route, du 8088 au 486 DX2-66

> Écrit le 26 septembre 2026, au commit `f1f45da` (après M21). Les tailles sont des
> `wc -l` sur `pcem-dev/`, les modèles et les tables CPU sont relevés dans `model.c` et
> `cpu_tables.c` le même jour. Le détail du jalon 286 est dans `PLAN-286.md`.

## Le but, et sa borne

Réunir les composants qui permettent de recréer n'importe quel PC des années 80 et 90 bâti
sur un **8088, 8086, 80286, 80386 (SX/DX) ou 80486 (SX/DX)**. La dernière machine de la
série est un **486 DX2-66 sur carte VLB**. **Pas de Pentium, pas de Windows 95** : tout ce
que PCem porte au-delà (`CPU_SUPPORTS_DYNAREC`, codegen, PCI, i430…) reste hors du dépôt.

La doctrine ne change pas d'une génération à l'autre : l'oracle d'abord, la transcription
ensuite, chaque commit passe la séquence de portes (`check-oracle.sh` à zéro dérive,
boot-diffs à l'unité, `selftest`, zéro avertissement, fuzzeur). Le cœur reste
l'**interpréteur** `exec386` : le dynarec de PCem n'est pas transcrit, et les entrées
`CPU_SUPPORTS_DYNAREC` des tables ne changent rien à l'exécution.

## Où on en est — 1er octobre 2026, après G4

```
8088 ✅ ── 286 ✅ ── G2 cœur 386 ✅ ── G3 ✅ ── G4 x87 ✅ ──▶ [ICI] G5 IDE ── G6 DX2-66 ── G7 ── G8   (G1 8086 ☐)
```

- **Derrière** : les générations 8088 et 286, jusqu'à M21 ; **G2**, le cœur 386 (D0 à D7,
  `PLAN-386.md`, fusionné en `d7e9f5b`) ; **G3**, l'ami386 (Headland) et l'ami386dx
  (OPTi 495), boot-diffs verts et Windows 3.1 en mode 386 étendu (`PLAN-G3.md`, `ab00595`) ;
  les défauts D1 à D6 de l'audit du 26/09 (`iXtal26/Docs/audit-performance-2026-09-26.md`).
- **Ouvert, sans bloquer G** : le reliquat du 286, `PLAN-286.md` § « Tâches à couvrir » —
  les tâches 3 (`taskswitch286`, G2 D5) et 4 (le CMOS de l'AT, G3.0) y sont faites, pas
  encore cochées.
- **G4, fait** : `PLAN-G4.md`, G4.0 à G4.7 (VERIFICATION.md § G4.0 à § G4.7). Le x87 de
  PCem entier — 8087, 287, 287XL, 387 —, transcendantes comprises, vérifié au fuzzeur bit pour
  bit ; `fpu = 287` sur le profil 286, `fpu = 387` sur le profil 386DX ; témoins MSD,
  Windows 3.1 (Calculatrice) et un banc x87 à nous (`tools/x87banc/`). En chemin, PB-48 à
  PB-70, le trampoline des préfixes et la borne de POP SS.
- **G5, plan proposé** : `PLAN-G5.md`, à valider. G1 (8086) reste petit et indépendant.

## Les générations

| Génération | État | Machine cible | Ce qui manque |
|---|---|---|---|
| 8088 | ✅ | IBM PC 5150, XT 5160 | — |
| 8086 | ☐ | Olivetti M24 ou Amstrad PC1512 | `cpus_8086`, la machine |
| 80286 | ✅ | IBM AT 5170, ami286 (NEAT) | reliquat : `PLAN-286.md` § « Tâches à couvrir » |
| 80386SX | ✅ | `ami386` (Headland) | — (387 : G4 ✅) |
| 80386DX | ✅ | `ami386dx` (OPTi 495) | — (387 : G4 ✅) |
| 80486SX/DX/DX2 | ☐ | **`ami486` (ALi 1429) + `i486DX2/66`** | IDE, chemins `is486`, le chipset (x87 : G4 ✅) |

## Ce qui existe déjà, et qui servira

- **`is8086`** est géré par `Cpu/808x.cs` : file de préchargement de 6 octets, chargement
  par mots. Seules la table et la machine manquent.
- **`exec386`** est en place (`Cpu/386.cs`), mais seules `ops_286` et `ops_286_0f` sont
  remplies. `x86_flags.cs` est complet, formes 32 bits comprises.
- **`x86seg.cs`** : le mode réel et le mode protégé 286, sauf `taskswitch286`.
- **Le cœur SVGA** (`vid_svga.cs`, `vid_svga_render.cs`) : le Cirrus et le S3 s'y
  branchent comme les Trident.
- **`dma16`, `pic2`, `nvr`, `keyboard_at`** : tout le socle AT, commun aux 386 et 486.

## Les blocs, dans l'ordre de dépendance

### G1 — Le 8086  *(petit)*

Porter `cpus_8086` (`cpu_tables.c`) et une machine. Dans `roms/`, on trouve
`olivetti_m24` et `pc1512`, mais pas la ROM du Compaq Deskpro 8086 : le choix est donc entre
l'**Olivetti M24** et l'**Amstrad PC1512**. Chacune apporte son propre matériel (vidéo M24,
vidéo et souris Amstrad), à inventorier avant de choisir.
**Oracle** : boot-diff, comme le 5150.

### G2 — Le cœur 386  *(le plus gros bloc du projet)*

> **Plan détaillé, sur mesures : `PLAN-386.md`** (étapes D0 à D7). Il existe un corpus
> SingleStepTests 80386 sur silicium, en mode réel seulement.

- `ops_386` et `ops_386_0f` ;
- les préfixes `66`/`67` et toutes les formes `_a32` et 32 bits des handlers déjà posés ;
- les groupes propres au 386 : bit, bitscan, movx, set, mov_ctrl ;
- les registres de contrôle et de débogage ;
- le mode V86 ;
- `taskswitch286` et `taskswitch386` ;
- **la pagination** : `mmutranslate`, `pages[]`, `page_lookup`, aujourd'hui omis
  (`Memory/mem.cs`, en-tête).

**Oracles** : le fuzzeur, étendu au 32 bits puis à un état paginé (GDT, CR0, CR3 et tables
de pages fabriqués en RAM, sur le modèle de ce qui a été fait pour le mode protégé 286).
**Vérifié** : `SingleStepTests/80386` couvre le mode réel, préfixes `66`/`67` compris,
sur un 386EX, au format `MOO` (voir `PLAN-386.md`).

**L'angle mort** de `PLAN-286.md` § « Ce que le bloc C n'a PAS comme oracle » vaut
encore plus ici. Les écritures mémoire (bit accédé, bit modifié des entrées de page) ne
sont couvertes par aucun compteur. Elles se transcrivent à l'œil, et le commit doit le
dire.

**Découpage** : sur le modèle de A4 à A11, un en-tête C par commit. Le 32 bits d'abord, en
mode réel, où le fuzzeur voit tout ; le mode protégé 32 bits ensuite, la pagination en
dernier.

### G3 — Les machines 386

1. **`ami386`**, 386SX : `at_headland_init` → `headland.c` (503 lignes).
2. **`ami386dx`**, 386DX : `at_opti495_init` → `opti495.c` (302 lignes).

Les deux ROM sont dans `roms/`.
**Porte** : boot-diff à l'unité, puis `--boot` jusqu'à l'invite DOS. Windows 3.1 en mode
386 étendu est le test d'intégration de G2 : V86, pagination, commutation de tâches.

### G4 — Le x87  ✅ *(`PLAN-G4.md`, fait le 1er octobre 2026)*

`x87.c` (97), `x87_timings.c` (297), `x87_ops.h` (1 116), `x87_ops_arith.h` (452),
`x87_ops_loadstore.h` (576), `x87_ops_misc.h` (927) : **≈ 3 460 lignes**. Indispensable
au 486DX, dont le coprocesseur est intégré (`fpus_builtin`). Il ouvre aussi le 287 et le
387, et règle la tâche `hasfpu` de `PLAN-286.md`.

**Piège connu** : la conversion flottant → entier. `(uint64_t)` d'un double négatif ne
rend pas la même chose sous GCC et sous .NET 9+ ; transcrire `(uint64_t)(int64_t)x`.
Le fuzzeur doit comparer les registres x87 bit pour bit, NaN et dénormaux compris.

### G5 — L'IDE  *(plan proposé : `PLAN-G5.md`)*

`ide/ide.c`, 1 222 lignes. `ami486` le requiert (`MODEL_HAS_IDE`). C'est aussi la fin
des trois plafonds du disque MFM (type 46).
**Oracle** : le chemin d'écriture de M11 et M12 (FDISK et FORMAT C: sous oracle, une copie
d'image par côté, comparée octet par octet).

### G6 — Le 486, et l'ultime machine

- les chemins `is486` : cycles, `BSWAP`, `CMPXCHG`, `XADD`, `INVLPG`, bits de CR0 et de
  CR4 propres au 486 ;
- **`ali1429.c`**, 66 lignes, via `at_ali1429_init` ;
- les tables `cpus_i486`, `cpus_Am486` et `cpus_Cx486` avec le multiplicateur. Le DX2-66
  existe en trois variantes : `i486DX2/66`, `Am486DX2/66` et `Cx486DX2/66` (bus à
  33 MHz, multiplicateur 2) ;
- la cadence : `--timer-check --model ami486`, comme en M16 pour le 286.

**Machine finale** : **ami486 + i486DX2/66**, x87 intégré, IDE, VLB.

### G7 — La vidéo

1. **Cirrus Logic GD5429** : `vid_cl5429.c` (2 201 lignes, qui porte aussi les GD5402 à
   GD5434), avec la branche VLB (`has_vlb`). ROM `5429.vbi` présente.
2. **S3** : `vid_s3.c` (3 142 lignes). **PCem n'a ni 86C805 ni 86C928**, seulement
   Vision864 (Paradise Bahamas 64), Trio32 et Trio64. Le plus proche d'un 486 VLB est le
   **Vision864**, mais sa ROM `bahamas64.bin` n'est pas dans `roms/`. Les ROM Trio64 y sont
   (`s3_764.bin`, `86c764x1.bin`). **À trancher par Julien** au moment du bloc.

**Oracle** : sonde VGA de fin de boot-diff, comme pour M15 et M19.

### G8 — Le son  *(en dernier)*

AdLib (OPL) puis Sound Blaster (`sound_sb.c`, `sound_sb_dsp.c`). **Réserve** :
`sound_dbopl.cc` est du C++, il faut vérifier comment l'oracle le lie avant d'écrire. Le
diff d'instructions est aveugle au son : il faudra une sonde d'échantillons, comme en M9.

### Transverse, au fil de l'eau

- La **souris PS/2** (`mouse_ps2`), attendue sur les 386 et 486 : elle passe par le 8042
  déjà transcrit.
- Le reliquat du 286 : `PLAN-286.md` § « Tâches à couvrir ». `taskswitch286` y est
  rattaché à G2.

## L'ordre

```
G1  8086          ← petit, indépendant ; peut aussi venir plus tard
G2  cœur 386      ← le verrou de tout ce qui suit
G3  ami386, ami386dx
G4  x87
G5  IDE
G6  486 + ami486 + DX2-66   ← l'ultime machine
G7  Cirrus 5429, S3
G8  son
```

G4 et G5 ne dépendent pas de G2 : on peut les intercaler si le cœur 386 s'enlise. Le x87
se vérifie au fuzzeur en mode réel, l'IDE sur un AT 286 déjà vert.

## Ce que je ne sais pas encore

- ~~**Si un corpus SingleStepTests 80386 existe.**~~ **Oui**, pour le mode réel seulement.
  Le mode protégé, la pagination et le V86 n'ont que PCem pour oracle (`PLAN-386.md`).
- **Le coût réel de G2.** Nombre de handlers et de lignes vives à mesurer par script,
  comme pour `PLAN-286.md`, avant d'annoncer une taille.
- **Le S3 exact** : Vision864 sans ROM aujourd'hui, ou Trio64.
- **La machine 8086** : M24 ou PC1512, selon le matériel propre qu'elles tirent.
- **Le cache interne du 486** : ce que PCem en modélise en mode interprété, à lire
  avant G6.

## Ce qui ne doit pas bouger

Les chiffres de `PLAN-286.md` § « Ce qui ne doit pas bouger » : boot-diffs 8088 en CGA,
VGA et 8900D, sonde VGA 86/86, à l'unité, à chaque commit, pour toutes les générations.

## Après G8 — nommer les puces

Seulement quand tout ce qui précède est vert : plus aucun fichier PCem ne reste à transcrire.

Les modules qui modélisent une puce prennent le nom de la puce, dans un répertoire `Ics/` :
`pic` → `Intel8259A`, `pit` → `Intel8253`, `dma` → `Intel8237`, le CRTC → `Motorola6845`,
et ainsi de suite. Les cartes (CGA, FDC, VGA…) restent des *devices*.

- **Pourquoi pas avant** : pendant les transcriptions, les noms verbatim de PCem évitent de
  traduire chaque appel (`picint`, `dma_channel_read`…) dans chaque nouveau fichier.
- **Comment** : renommage mécanique (refactor Rider), table `// noms:` en tête de chaque
  fichier, comme dans `Floppy/` et `Disc/`. Vérifier d'abord si le harnais ou les sondes
  (`h_state`, sst-probe, boot-diff) désignent des champs par leur nom.
- **Portes** : boot-diffs, SST et fuzzeur identiques à l'unité — seuls des noms changent.
- **Ce qui ne change pas** : les classes restent statiques. Passer en instances est un
  autre levier, à mesurer séparément.
