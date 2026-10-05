# iXtal26 — la feuille de route, du 8088 au 486 DX2-66

> Écrit le 26 septembre 2026, au commit `f1f45da` (après M21). Les tailles sont des
> `wc -l` sur `pcem-dev/`, les modèles et les tables CPU sont relevés dans `model.c` et
> `cpu_tables.c` le même jour. Le détail du jalon 286 est dans `PLAN-286.md`. Remis à jour le
> 5 octobre 2026, au commit `34268db` (G10 fait), relu contre `git log` et `VERIFICATION.md`.

## Le but, et sa borne

Réunir les composants qui permettent de recréer n'importe quel PC des années 80 et 90 bâti
sur un **8088, 8086, 80286, 80386 (SX/DX) ou 80486 (SX/DX)**. La dernière machine de la
série est un **486 DX2-66 sur carte VLB**. **Pas de Pentium** — OverDrive compris : Intel seul,
8088 → 486 DX4 (décision utilisateur du 03/10) —, **pas de Windows 95** : tout ce que PCem porte
au-delà (`CPU_SUPPORTS_DYNAREC`, codegen, PCI, i430…) reste hors du dépôt.

La doctrine ne change pas d'une génération à l'autre : l'oracle d'abord, la transcription
ensuite, chaque commit passe la séquence de portes (`check-oracle.sh` à zéro dérive,
boot-diffs à l'unité, `selftest`, zéro avertissement, fuzzeur). Le cœur reste
l'**interpréteur** `exec386` : le dynarec de PCem n'est pas transcrit, et les entrées
`CPU_SUPPORTS_DYNAREC` des tables ne changent rien à l'exécution.

## Ce qu'un bloc livre  *(règle du 04/10)*

La référence est ce que G1 à G8 ont livré ; G9 et G10 en sont restés en deçà, et le bloc GR les y
ramène. À partir de G11, chaque bloc livre :

1. **Un plan complet** : « Où on en est », les étapes, les défauts relevés, « La vérification »,
   « Les risques », les décisions marquées *(validé)*, puis l'en-tête « Gx est fait ».
2. **Une clôture, « les machines et les témoins ; Gx fait »** : les profils `.cfg` et
   `launchSettings.json` ; les contrôles de `--setup-check` ; un tableau de témoins tirés de vrais
   logiciels (POST, `VER`, `MSD /S`, `MODE`, CHKDSK, Windows avec le pilote) ; le comportement
   surprenant rejoué sous boot-diff ; la preuve que les originaux sont intacts.
3. **Une sonde qui voit ce que le diff ne voit pas** (VRAM, palettes, pixels, état d'un
   périphérique), et la preuve qu'elle mord : une panne injectée la rougit elle-même.
4. **Une couverture établie**, comme pour la vidéo en M15, M19 et G7 : des bancs qui finissent
   dans le mode testé, une couverture comptée ou tracée, une relecture contradictoire des chemins
   non exercés.
5. **Des configurations validées** : une valeur hors liste est ramenée au défaut ou refusée, avec
   un avertissement, et une porte `r9-*` le prouve.

G11 à G13 seront planifiés avec leur clôture : la dette ne grossit plus.

## Où on en est — 6 octobre 2026, G11 fait ; G12 en cours

```
8088 ✅ ── 286 ✅ ── G2 ✅ ── G3 ✅ ── G4 ✅ ── G5 ✅ ── G6 ✅ ── G7 ✅ ── G1 ✅ ── G8 ✅ ── PS2 ✅ ── G9 ✅
     ── G10 ✅ ── G11 ✅ ──▶ [ICI] G12 ── G13 ── GR ── G14 ── G15 ── G16 ── G17
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
- **G5, fait** : `PLAN-G5.md`, G5.0 à G5.3. L'oracle lit enfin un disque AT (`mfm_at.c`,
  `ide.c` liés) ; l'IDE disque dur transcrit, deux canaux, vérifié par boot-diffs d'écriture
  (FDISK, FORMAT) et un ide-check dirigé ; témoins CHKDSK, Windows 3.1, DOS 5 installé sur un
  disque IDE vierge. Règle R9 (l'invité ne tue pas l'hôte), PB-71 à PB-76. Séries parallèles.
- **G6, fait** : `PLAN-G6.md`, G6.0 à G6.4. **La machine finale tourne** : ami486 (ALi 1429)
  + i486DX2/66, x87 intégré, IDE ; Intel seul (décision du 01/10). Cœur 486 au fuzzeur, boot-diffs
  POST complet et écriture IDE, cadence 66,663 MHz ; témoins MSD, Windows 3.1 en 386 étendu,
  X87BANC. PB-77 à PB-79 (PB-79 sous R9). Profil Rider ami486.
- **G7, fait** : `PLAN-G7.md`, G7.0 à G7.4. Le socle SVGA (fenêtre linéaire, rendu 32 bpp),
  la Cirrus Logic GD5429 et son blitter (banc BLTBANC), la S3 Trio64 Phoenix, dont
  l'accélérateur est rendu synchrone des deux côtés (DEVIATION, décision du 01/10) ; profils et
  témoins Windows. PB-80 à PB-86.
- **G1, fait** : `PLAN-G1.md`, G1.0 à G1.3. Les tables 8086 et leur fuzzeur, l'Olivetti M24 et
  l'Amstrad PC1512, les deux machines (décision du 01/10), avec leur vidéo et leurs souris ;
  profils. PB-87 à PB-89.
- **G8, fait** : `PLAN-G8.md`, G8.0 à G8.3. L'OPL par DBOPL, l'AdLib, et la Sound Blaster Pro v2
  (DSP SBPRO2, mélangeur CT1345) ; les sections de device du `.cfg` ; profils et témoins.
  PB-90 à PB-93.
- **PS2, transcrite, non offerte** : `PLAN-PS2.md`, PS2.0 et PS2.1. La souris PS/2 et
  l'Intellimouse par le 8042, vérifiées, puis refusées : aucun BIOS du dépôt ne les gère
  (décision du 03/10). PB-94, PB-95.
- **G9, fait** : `PLAN-G9.md`, G9.0 à G9.3. La MDA, l'Hercules, l'EGA et la Tseng ET4000AX, sous
  boot-diff ; témoins Windows 3.11 en Hercules, en EGA et, sur l'ET4000, en VGA. PB-96 à PB-100.
  La profondeur qui manque est renvoyée au bloc GR, ci-dessous.
- **G10, fait le 5 octobre 2026** : `PLAN-G10.md`, feu vert du 04/10. G10.0 :
  LPT1, LPT2 et le port jeu sur toutes les machines XT et AT, la Sound Source et les Covox sur
  LPT1 (PB-101). G10.1 : la manette, ses sept types et l'hôte SDL3 (PB-103 à PB-105).
  G10.2 : le XTIDE en version XT ; le 5150, le XT, la M24 et le PC1512 amorcent un disque dur.
  G10.3 : le moteur d'images CD entier, ISO et CUE/BIN, prouvé hors machine (PB-106 à PB-112).
  G10.4 : l'ATAPI, le CD-ROM sur l'IDE, prouvé par le banc ATAPIBANC (PB-113 à PB-122).
  G10.5 : l'audio CD dans la machine, le fil CD de PCem appelé en synchrone, prouvé par ATAPIAUD
  et la sonde du CD (PB-123, PB-124).
  G10.6 : le lecteur ZIP 100 sur l'IDE, prouvé par ZIPBANC, l'image comparée des deux côtés
  (PB-125 à PB-127).
  Sans la clôture de « Ce qu'un bloc livre » (profils, témoins, couverture comptée) : elle revient au
  bloc GR (GR.2, GR.3), avec ce que chaque étape a laissé (`PLAN-G10.md`, « Les risques »).
- **G11, fait le 5 octobre 2026** : `PLAN-G11.md`, feu vert du 05/10 (« en totale autonomie »).
  L'Adaptec AHA-1542C et ses disques SCSI, sous l'oracle et sous une sonde de 140 champs : le POST
  de sa ROM, FDISK et FORMAT, DOS 5 amorcé sur l'ami486, l'ami386dx et l'ami286 ; le banc AHABANC
  (un programme qui parle à la carte comme un pilote ASPI) ; r9-aha et r9-scsihd. La règle ISA
  16 bits est écrite : une carte DEVICE_AT n'est pas montée sur un 8088 ou un 8086. Livré avec sa
  clôture : le profil `ixtal26-486-scsi.cfg`, `--setup-check`, les témoins (VER, MEM, CHKDSK, MSD).
  PB-128 à PB-144.
- **G12 à G17** attendent chacun un feu vert (G12 l'a reçu le 05/10, avec G11) ; **GR**, la reprise
  de G9 et G10, vient après G13.

## Les générations

| Génération | État | Machine cible | Ce qui manque |
|---|---|---|---|
| 8088 | ✅ | IBM PC 5150, XT 5160 | — |
| 8086 | ✅ | Olivetti M24, Amstrad PC1512 | — |
| 80286 | ✅ | IBM AT 5170, ami286 (NEAT) | reliquat : `PLAN-286.md` § « Tâches à couvrir » |
| 80386SX | ✅ | `ami386` (Headland) | — (387 : G4 ✅) |
| 80386DX | ✅ | `ami386dx` (OPTi 495) | — (387 : G4 ✅) |
| 80486SX/DX/DX2 | ✅ | **`ami486` (ALi 1429) + `i486DX2/66`** | — (Intel seul ; Am486, Cx486 omis) |

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

### G1 — Le 8086  ✅ *(`PLAN-G1.md`, fait le 2 octobre 2026)*

Porter `cpus_8086` (`cpu_tables.c`) et une machine. Dans `roms/`, on trouve
`olivetti_m24` et `pc1512`, mais pas la ROM du Compaq Deskpro 8086 : le choix est donc entre
l'**Olivetti M24** et l'**Amstrad PC1512**. Chacune apporte son propre matériel (vidéo M24,
vidéo et souris Amstrad), à inventorier avant de choisir.
**Oracle** : boot-diff, comme le 5150. **Tranché** : les deux machines (décision du 01/10).

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

### G5 — L'IDE  ✅ *(`PLAN-G5.md`, fait le 1er octobre 2026)*

`ide/ide.c`, 1 222 lignes. `ami486` le requiert (`MODEL_HAS_IDE`). C'est aussi la fin
des trois plafonds du disque MFM (type 46).
**Oracle** : le chemin d'écriture de M11 et M12 (FDISK et FORMAT C: sous oracle, une copie
d'image par côté, comparée octet par octet).

### G6 — Le 486, et l'ultime machine  ✅ *(`PLAN-G6.md`, fait le 1er octobre 2026)*

- les chemins `is486` : cycles, `BSWAP`, `CMPXCHG`, `XADD`, `INVLPG`, bits de CR0 et de
  CR4 propres au 486 ;
- **`ali1429.c`**, 66 lignes, via `at_ali1429_init` ;
- les tables `cpus_i486`, `cpus_Am486` et `cpus_Cx486` avec le multiplicateur. Le DX2-66
  existe en trois variantes : `i486DX2/66`, `Am486DX2/66` et `Cx486DX2/66` (bus à
  33 MHz, multiplicateur 2) ;
- la cadence : `--timer-check --model ami486`, comme en M16 pour le 286.

**Machine finale** : **ami486 + i486DX2/66**, x87 intégré, IDE, VLB.

### G7 — La vidéo  ✅ *(`PLAN-G7.md`, fait le 2 octobre 2026)*

1. **Cirrus Logic GD5429** : `vid_cl5429.c` (2 201 lignes, qui porte aussi les GD5402 à
   GD5434), avec la branche VLB (`has_vlb`). ROM `5429.vbi` présente.
2. **S3** : `vid_s3.c` (3 142 lignes). **PCem n'a ni 86C805 ni 86C928**, seulement
   Vision864 (Paradise Bahamas 64), Trio32 et Trio64. Le plus proche d'un 486 VLB est le
   **Vision864**, mais sa ROM `bahamas64.bin` n'est pas dans `roms/`. Les ROM Trio64 y sont
   (`s3_764.bin`, `86c764x1.bin`). **Tranché** : la Trio64 (décision du 01/10), son accélérateur
   rendu synchrone des deux côtés (DEVIATION).

**Oracle** : sonde VGA de fin de boot-diff, comme pour M15 et M19.

### G8 — Le son  ✅ *(`PLAN-G8.md`, fait le 2 octobre 2026)*

**La carte : la Sound Blaster Pro v2** (décision utilisateur du 02/10) — `sb_pro_v2_init`
(`sound_sb.c:998`), `sb_pro_v2_device` (`:1345`). Elle porte :

- l'**OPL3** (`opl3_init`, `sound_opl.c:130`), 18 voix en stéréo, aux ports 2x0-2x3, 2x8-2x9
  et 388h-38Bh (`sound_sb.c:1017-1019`) : l'AdLib est couverte par la même puce ;
- le **DSP SBPRO2** (`sb_dsp_init(…, SBPRO2, …)`, `sound_sb_dsp.c`), 8 bits stéréo ;
- le **mélangeur CT1345** (`sb_ct1345_mixer_*`, ports 2x4-2x5, `:1020`).

Configuration (`sb_pro_v2_config`, `:1230`) : adresse 220h ou 240h (défaut 220h), IRQ 2, 5, 7
ou 10 (défaut 7), DMA 8 bits 1 ou 3 (défaut 1), émulateur OPL `opl_emu` (défaut DBOPL, ou
NukedOPL). **Tranché** : DBOPL (G8.1).

L'**AdLib seule** (OPL2, `adlib_device`) est maintenue si elle ne coûte qu'un device de plus.
Ni AdLib Gold ni EMU8K en G8 ; les Sound Blaster 1.x, 2.0, Pro v1, 16 et AWE32 (EMU8K) sont
renvoyées à G12 (décision utilisateur du 03/10).

**Réserves.** L'OPL est du C++ — `sound_dbopl.cc` lie `src/dosbox/dbopl.cpp` et
`src/dosbox/nukedopl.cpp` — : il faut vérifier comment l'oracle le lie avant d'écrire. Le
diff d'instructions est aveugle au son : il faudra une sonde d'échantillons, comme en M9.

### G9 — Vidéo, complément  ✅ *(`PLAN-G9.md`, fait le 4 octobre 2026)*

Dans cet ordre :

1. **MDA** : `vid_mda.c` (303 lignes) — vérifiée sur le 5150 et le XT.
2. **Hercules** : `vid_hercules.c` (365) — vérifiée sur le 5150 et le XT.
3. **EGA** : `vid_ega.c` (1 123), ROM `ibm_6277356_ega_card_u44_27128.bin` — vérifiée sur
   286 et 386.
4. **Tseng ET4000AX** : `vid_et4000.c` (630), `et4000_device`, ROM `et4000.bin` — vérifiée
   jusqu'à Windows en 256 couleurs.

**Ce qui a été tenu.** Les quatre cartes sous boot-diff sur leurs machines ; un banc dirigé
pour l'Hercules, l'EGA et l'ET4000. Windows 3.11 tourne en Hercules (720×348) et en EGA
(640×350). Sur l'ET4000, le 256 couleurs est vu par le banc ET4BANC, mais Windows tourne en VGA :
la ROM `et4000.bin` ne rend pas les services VESA (mesuré, VERIFICATION.md § G9.3). C'est une
limite dite, pas un défaut. Ce qui manque en profondeur (les pixels des cartes non SVGA, les
bancs qui finissent dans le mode testé, la clôture) est renvoyé au bloc GR.

**Hors G9** : InColor, PGC, Plantronics, Sigma, Wyse, Compaq CGA, les variantes coréennes,
Paradise, Cirrus 5428/5430/5434, Trio32/9FX, et tout ce qui est PCI (Voodoo, Banshee,
Millennium, Mach64).

### G10 — Le 486 complet : ce qui manque à la machine du quotidien  ✅ *(`PLAN-G10.md`, fait le 5 octobre 2026 ; sa clôture revient à GR)*

**G10 est fait** (G10.0 à G10.6) : LPT1, LPT2 et le port jeu sur toutes les machines XT et AT,
comme PCem, en un seul recompte du POST ; sur LPT1, la Disney Sound Source et les Covox
(VERIFICATION.md § G10.0) ; la manette, ses sept types, la section [Joysticks] et `joystick_poll`
par SDL3 (§ G10.1) ; le XTIDE en version XT, sur lequel le 5150, le XT, la M24 et le PC1512
amorcent un disque dur (§ G10.2) ; le moteur d'images CD entier, ISO et CUE/BIN, pistes de données
et audio, prouvé des deux côtés hors machine sur les images d'isogen, PB-106 à PB-112 (§ G10.3) ;
l'ATAPI, le pont, le bus et le lecteur de CD-ROM sur l'IDE, ses cinq clés, et le banc ATAPIBANC
qui lui parle par les ports sous DOS, PB-113 à PB-122 (§ G10.4) ; l'audio CD dans la machine, le
fil CD de `sound.c` à l'échéance de `sound_poll`, la page audio et le volume CD de la carte, la
voie hôte à 44,1 kHz, prouvé par ATAPIAUD et la sonde du CD, PB-123 et PB-124 (§ G10.5) ; le lecteur
ZIP 100 sur l'IDE, `zip_channel` et `zip_path` (DEVIATION), prouvé par ZIPBANC et `r9-zip`, PB-125
à PB-127 (§ G10.6). La clôture (profils, témoins, couverture) revient au bloc GR.

- **(a) CD-ROM et ZIP en ATAPI sur l'IDE** : `ide_atapi.c` (500), `scsi.c` (352),
  `scsi_cd.c` (1 707), `scsi_zip.c` (1 111), `cdrom-image.cc` (500, du C++ comme DBOPL), et le
  bloc cdrom de `pc.c`, omis aujourd'hui (`pc.cs`). **Images seulement** (ISO, CUE/BIN) : le
  lecteur physique de l'hôte (`cdrom-ioctl-*`) est omis, pour le déterminisme. Le plus gros
  morceau ; reconnaissance à faire.
- **(b) La manette sur les AT** : `gameport_device` sur `at_init` (fait en G10.0, sur `xt_init`
  aussi), les types de manette (`joystick/*.c`, les sept) et `joystick_poll` vers l'hôte — fait en
  G10.1.
- **(c) Le port parallèle LPT1** : `lpt.c` (172), `lpt1_device_init` — fait en G10.0, LPT2 et la
  Sound Source et les Covox compris.
- **(d) Le XTIDE, version XT seulement** : `xtide.c` (121, sur l'`ide.c` de G5),
  `xtide_device`, ROM `ide_xt.bin` — un disque dur amorçable pour les 8088/8086, en
  particulier la M24 et le PC1512, où le Xebec n'amorce pas (constat de G1). Les variantes
  XTIDE AT et PS/1 sont exclues. **Fait en G10.2.** Le constat de G1 portait sur une image
  vierge : `8088-HDD-C.img` n'a ni partition ni système ; le Xebec n'a pas été réessayé sur un
  disque amorçable.
- **La souris PS/2** (`PLAN-PS2.md`) : transcrite et vérifiée ; non offerte, aucune machine
  transcrite n'a de BIOS PS/2 (décision utilisateur du 03/10) ; des machines PCem à BIOS PS/2 ont
  leur ROM dans `roms/` (p. ex. PB410A), non transcrites.
- **Exclus** : les Pentium OverDrive — Intel seul, 8088 → 486 DX4 ; pas de Pentium (décision
  utilisateur du 03/10). Présents dans `cpus_i486`, ils restent sautés par `cpu-config-check` et
  refusés bruyamment si on les choisit.

### G11 — SCSI  ✅ *(`PLAN-G11.md`, fait le 5 octobre 2026)*

Une seule carte, l'**Adaptec AHA-1542C** : `scsi_aha1540.c` (2 299), ROM
`adaptec_aha1542c_bios_534201-00.bin`, avec `scsi_hd.c` (788, les disques SCSI). Le CD-ROM et le
ZIP en SCSI réutilisent `scsi_cd.c` et `scsi_zip.c` de G10. **Exclus** : BusLogic, IBM SCSI,
Longshine, Rancho, Trantor (53C400), l'ESDI et le XTIDE autre que XT.
La carte est ISA 16 bits : elle est refusée avec avertissement sur les machines 8088/8086. La règle
est écrite en G11 (`pc.check_hdd_controller`, décision n° 3 de `PLAN-G11.md`), pour toutes les cartes
de disque DEVICE_AT ; G12 l'étend aux cartes son.

### G12 — Les autres Sound Blaster (ISA)  *(décision utilisateur du 03/10 ; feu vert du 05/10, en cours : `PLAN-G12.md`)*

SB 1.0, SB 1.5, SB 2.0, SB Pro v1, SB 16 et SB AWE32 (`sound_sb.c`, `sound_sb_dsp.c`, déjà en
partie transcrits en G8).

- **AWE32** = EMU8K (`sound_emu8k.c`, 2 237, ROM `awe32.raw`) : c'est la levée de l'exclusion
  « ni EMU8K » de G8.
- **SB 16** : DMA 16 bits et mélangeur CT1745 ; le MPU-401 UART (`sound_mpu401_uart.c`) s'il
  le tire.
- **Exclues** : SB MCV et SB Pro MCV (`DEVICE_MCA` — aucune machine MCA dans iXtal) ; l'AdLib
  Gold reste exclue.
- La SB 16 et l'AWE32 sont des cartes ISA 16 bits : elles doivent être refusées avec
  avertissement sur les machines 8088/8086, par la règle écrite en G11.

### G13 — Corriger les défauts de PCem reproduits  *(décision utilisateur du 03/10 ; attend un feu vert)*

Pour qu'iXtal soit fidèle au vrai matériel. Comptes relevés dans `PCEM_BUGS.md` le 04/10,
après G10.0, mis à jour le 05/10 après G11 :

1. **Section A, les 78 PB reproduits**, par groupe : UC (01, 02, 39, 40, 41, 43, 44, 45, 50, 51,
   78, 87), 486 (77), x87 (48, 52, 54 à 70), carte mère (03, 05, 06), vidéo (04, 80, 89, 97, 99,
   100, 102), disques (14, 22, 23, 25, 28, 71, 72, 74), son (90, 91, 92), souris (94, 95 —
   PS2.0), ports (101 — G10.0), manette (103 ; 104, de l'hôte, sa correction proposée pour le mode
   matériel — G10.1), images CD (106, 107, 108, 109 — G10.3 ; 122 — G10.4 ; 123 — G10.5), lecteur
   de CD (117 à 120 — G10.4), lecteur ZIP (126 — G10.6), SCSI (129 à 133, 136 à 144 — G11).
2. **Section B, les 9 PB reproduits** : 07, 08, 09, 10, 16, 17, 18, 21, 96.
3. **Section C, 23 entrées sans effet observable** : nettoyage seulement, sans changement de
   comportement (dont 134, G11).

Les non-reproduits (24, 31, 46, 47, 49, 73, 75, 76, 79, 81 à 86, 88, 93, 124, 125, 128, 135) sont déjà
réglés.

**Principe, à confirmer au plan de G13** : chaque correction se fait derrière une option « mode
matériel », désactivée par défaut, et le marqueur `pcem bug, reproduced` devient `pcem bug, fixed
in hardware mode: PB-nn`. Le mode PCem reste celui des portes : l'oracle et toutes les séries
restent intacts. Le mode matériel se vérifie contre la documentation Intel, le corpus SST (8088 et
386 réels) et des bancs dirigés.

**Ordre proposé** : l'UC et le x87 d'abord (32 PB, le 486 compris), puis les disques, la vidéo,
le son, la carte mère, la souris et les ports, puis la section B, puis la section C.

### Transverse, au fil de l'eau

- La **souris PS/2** (`mouse_ps2`, `PLAN-PS2.md`) : transcrite et vérifiée ; non offerte, aucune
  machine transcrite n'a de BIOS PS/2 (décision utilisateur du 03/10) ; des machines PCem à BIOS
  PS/2 ont leur ROM dans `roms/` (p. ex. PB410A), non transcrites.
- Le reliquat du 286 : `PLAN-286.md` § « Tâches à couvrir ». `taskswitch286` y est
  rattaché à G2.
- **Hors plan, relevé en G4** : le harnais de `pm-fuzz` laisse fuir une IRQ d'une itération
  à la suivante (l'IRQ13 d'un x87 démasqué, vue comme « BANC FAUX »). Contournée en G4.4 —
  `npxc = 0x037F` posé à chaque cas sous `--fpu` —, pas corrigée : l'état du PIC n'est pas
  remis à zéro entre deux cas.
- **Hors plan, règle R9** (TRANSCRIPTION.md) : (a) les dix-sept `fatal()` de protocole de
  `mfm_xebec.c` (état inattendu, commande inconnue, « no DMA ») — l'invité peut arrêter
  l'émulateur ; à traiter selon R9 après lecture de la documentation Xebec / IBM (l'octet d'état
  d'erreur de la carte). (b) Les accès `geteal`, `geteaq`, `seteal`, `seteaq` du 8087 en mod = 3
  (`8087.h:37-58`, `fatal`) : prouver qu'aucune table ne les atteint, ou appliquer R9.

## L'ordre

```
G1  8086                     ✅ l'Olivetti M24 et l'Amstrad PC1512
G2  cœur 386                 ✅
G3  ami386, ami386dx         ✅
G4  x87                      ✅
G5  IDE                      ✅
G6  486 + ami486 + DX2-66    ✅ l'ultime machine
G7  Cirrus 5429, S3 Trio64   ✅
G8  son                      ✅
PS2 souris PS/2              ✅ transcrite, non offerte (03/10)
G9  MDA, Hercules, EGA, ET4000   ✅
G10 LPT1 et port jeu, manette, XTIDE (XT), CD-ROM/ZIP ATAPI   ✅ le 486 complet (clôture : GR)
G11 SCSI : AHA-1542C                    ┐
G12 les autres Sound Blaster, AWE32     │ décisions utilisateur du 03/10
G13 défauts de PCem, « mode matériel »  │ (GR : du 04/10) ;
GR  reprise de G9 et G10, en profondeur │ chacun attend un feu vert
G14 nommer les puces (Ics/)             │
G15 normaliser le C#, 0 % de perte      │
G16 robustesse et confort               │
G17 usage avancé, publication           ┘
```

G4 et G5 ne dépendent pas de G2 : on peut les intercaler si le cœur 386 s'enlise. Le x87
se vérifie au fuzzeur en mode réel, l'IDE sur un AT 286 déjà vert.

## Ce que je ne sais pas encore

- ~~**Si un corpus SingleStepTests 80386 existe.**~~ **Oui**, pour le mode réel seulement.
  Le mode protégé, la pagination et le V86 n'ont que PCem pour oracle (`PLAN-386.md`).
- ~~**Le coût réel de G2.**~~ G2 est fait (`PLAN-386.md`, D0 à D7).
- ~~**Le S3 exact**~~ : la Trio64 (G7, décision du 01/10).
- ~~**La machine 8086**~~ : les deux, la M24 et le PC1512 (G1, décision du 01/10).
- ~~**Le cache interne du 486**~~ : lu en G6 ; PCem n'en modélise que des temps, pas des
  fonctions (`cpu_cache_int_enabled`, `cpu_update_waitstates`) — `PLAN-G6.md`, « Les risques ».

## Ce qui ne doit pas bouger

Les chiffres de `PLAN-286.md` § « Ce qui ne doit pas bouger » : boot-diffs 8088 en CGA,
VGA et 8900D, sonde VGA 163/163, à l'unité, à chaque commit, pour toutes les générations.
Rebasés en G10.0, parce que le POST trouve désormais LPT1, LPT2 et le port jeu. L'interrupteur
« LPT et port jeu hors service » (`--lpt-jeu-hors-service`) rend les anciens chiffres à l'unité.

## GR — La reprise de G9 et G10, en profondeur  *(décision utilisateur du 04/10 ; attend un feu vert)*

Après G13 et avant la référence de performance figée de G14. D'ici là, G9 et G10 restent en
l'état. GR les porte au niveau de « Ce qu'un bloc livre » ; chaque étape de G10 y ajoute ce
qu'elle laisse (`PLAN-G10.md`, « Les risques »).

- **GR.0 — Les pixels, pour toutes les cartes non SVGA** : CGA, MDA, Hercules, EGA, M24, PC1512
  (décision du 04/10). L'oracle reçoit les vraies polices et la vraie palette CGA : `loadfont` et
  `cgapal_rebuild` recopiés verbatim de `video.c:930` et `:1162` dans le harnais, le PCem vendoré
  restant intact. Les sondes de ces cartes hachent l'image (`buffer32`), et `pallook` pour l'EGA ;
  leurs champs sont nommés dans `VgaProbe.Fields`, et le message vert compte les champs remplis.
  Une panne injectée par carte rougit un champ de sonde.
- **GR.1 — La profondeur de G9.** L'EGA à 64 Ko et sur moniteur mono (l'échange 3Bx/3Dx) sous
  boot-diff, `display_type` en ambre. Les bancs : MDABANC (le 6845, l'entrelacé, le registre 9
  au-delà de 15 pour PB-96, le correctif « Turbo XT » de PB-97) ; EGABANC étendu à AND et OR ;
  ET4BANC qui finit en 256 couleurs, puis un banc qui finit en HiColor ; pour PB-100, le FFh armé
  et les 32 bits du RAMDAC ; le 1024×768 si la ROM l'offre. L'état `et4000_t` sondé, le
  boot-diff DOS de l'ET4000 sur l'ami486, sa ROM essayée sur 8088 en oracle seul. Couverture
  comptée des cinq fichiers et relecture contradictoire, citations `// pcem:` comprises.
- **GR.2 — La profondeur de G10.** ATAPIBANC piloté par une table : la trentaine de commandes de
  `scsi_cd.c`, leurs cas limites, et le compte de chaque commande atteinte. ZIPBANC de 3 à
  23 commandes. Une sonde de l'état ATAPI et ZIP, et de la file de la DSS et du DAC. Une campagne
  de CUE générés et malformés pour `cdimage-check` ; un fichier connu lu de bout en bout dans l'ISO
  générée ; ATAPIBANC aussi sur l'ami286 et l'ami386dx. Couverture comptée et relecture
  contradictoire.
- **GR.3 — Les deux clôtures.** Les quatre profils de G9 (XT + Hercules, 286 + EGA, 386DX +
  ET4000AX, 5150 + MDA, décision du 04/10), puis le CD, LPT et la manette sur les profils 486,
  chacun avec `launchSettings.json`. `--setup-check` étendu, l'écran SETUP compris ; `--make-nvr`
  pour chaque carte. Les témoins : POST, `VER`, `MSD /S` (la vidéo, « LPT Ports », « Game
  Adapter »), `MODE`, QBASIC `STICK` et `STRIG` avec `--joy-at` sous boot-diff, `ECHO >PRN`, les
  outils XTIDE de `roms/xtide/`, le XTIDE de la M24 et du PC1512 sous boot-diff, Windows sous
  boot-diff, pixels compris, avec `HERCULES.DRV` et `EGA.DRV`. L'écoute du Covox, de la DSS et
  de l'audio CD par l'utilisateur ; la marge de `--timer-check` avec les lectures CD.
- **GR.4 — La dette partagée avec G1, G7 et G8.** La M24 et le PC1512 ignorent `gfxcard` sans le
  dire, et l'écran SETUP leur offre toutes les cartes. `VENDORED.md`, `roms/roms.sha256` et
  `THIRD_PARTY_NOTICES.md:36` à mettre à jour avant G17 (les ROM de G7 et de G9, l'OPL de
  DOSBox). Les registres : TRANSCRIPTION.md (la ligne `VIDEO_CARD`, `mouse_ps2.c`) ;
  PCEM_BUGS.md (le champ *Effet* de PB-97, 99 et 100, PB-98 étendu à l'EGA, les totaux). Les
  en-têtes et commentaires périmés (`video.cs:5-8` et `:67-73`, `device.cs:132-133`, les listes
  de cartes de `UsageText` et du README). L'outil : le vidage `--boot` de l'EGA ; la phase 2 de
  boot-diff, qui ne rejoue ni `--type` ni `--fdb` et accuse alors le hachage de trace à tort
  (G10.0).
- **GR.5 — Les documents.** VERIFICATION.md § GR, avec ses tableaux et une partie « ce que ce
  vert ne dit pas » ; l'en-tête « est fait » de PLAN-G8, G9 et G10, et « Les risques » pour
  PLAN-G9.

**Porte** : toutes les portes vidéo vertes, pixels compris, et une panne qui rougit chaque sonde ;
les tableaux de couverture ; les constats de la relecture corrigés ; les témoins.

## G14 — Nommer les puces  *(décision utilisateur du 03/10 ; attend un feu vert)*

Après GR, seulement quand tout ce qui précède est vert : plus aucun fichier PCem ne reste à
transcrire.

Les modules qui modélisent une puce prennent le nom de la puce, dans un répertoire `Ics/` :
`pic` → `Intel8259A`, `pit` → `Intel8253`, `dma` → `Intel8237`, le CRTC → `Motorola6845`,
et ainsi de suite. Les cartes (CGA, FDC, VGA…) restent des *devices*.

- **Pourquoi pas avant** : pendant les transcriptions, les noms verbatim de PCem évitent de
  traduire chaque appel (`picint`, `dma_channel_read`…) dans chaque nouveau fichier.
- **Comment** : renommage mécanique (refactor Rider), table `// noms:` en tête de chaque
  fichier, comme dans `Floppy/` et `Disc/`. Vérifier d'abord si le harnais ou les sondes
  (`h_state`, sst-probe, boot-diff) désignent des champs par leur nom.
- **Porte** : toutes les séries identiques à l'unité (boot-diffs, SST, fuzzeur) — seuls des
  noms changent ; et le banc de performance de G15 contre sa référence d'origine.
- **Ce qui ne change pas** : les classes restent statiques. Le passage en instances est
  **abandonné** (décision utilisateur du 03/10, voir G15) — il n'est plus « à mesurer
  séparément ».

## G15 — Normaliser le C#  *(décision utilisateur du 03/10 ; attend un feu vert)*

Après G14.

- **Contenu** : les conventions .NET (PascalCase, `private` explicite, `enum` au lieu des
  constantes `int`), `.editorconfig` et analyseurs, nettoyage (code mort, `using`,
  avertissements). **Les commentaires restent en français** (décision utilisateur).
- **Conservés** : les marqueurs `// pcem: fichier.c:ligne` et `// pcem bug … PB-nn`.
- **« Ce qui coûte, on ne le fait pas, et on démontre que ça coûte »** (décision utilisateur
  du 03/10) :
  1. **Interdit dans les chemins chauds** (cœurs UC et x87, mémoire, aiguillage des opcodes,
     minuteries, rendu vidéo, mixage du son) : les classes statiques passées en instances ;
     LINQ, les fermetures (lambdas qui capturent), les allocations dans les boucles (`new`,
     boxing, `string`, `params`) ; les interfaces et les méthodes virtuelles. Le « levier
     instances » est donc abandonné.
  2. **Démontré** : en ouverture de G15, un banc BenchmarkDotNet versionné (`tools/perfbanc/`)
     mesure chaque construction interdite contre sa forme retenue, sur un vrai chemin d'iXtal
     (`readmemb` statique contre instance, boucle `for` contre LINQ, appel direct contre
     interface ou `virtual`, sans allocation contre avec). Les chiffres sont consignés dans
     VERIFICATION.md : c'est la justification écrite de la règle.
  3. **Signalé** : si possible, un analyseur ou une règle `.editorconfig` relève ces
     constructions dans les dossiers des chemins chauds ; sinon, une revue par `grep` dans la
     porte de G15.
- **Performance : 0 % de perte** (décision utilisateur — les pertes se cumulent) :
  - avant G14, une **référence figée** : un banc BenchmarkDotNet sur les chemins chauds (décodage
    8088, 286, 386, 486 ; accès mémoire ; rendu vidéo ; son) et les instructions par seconde de
    chaque machine ;
  - à chaque commit de G14 et de G15, la mesure se fait contre **cette référence d'origine**,
    jamais contre le commit précédent, pour que rien ne se cumule ;
  - toute régression statistiquement significative (intervalle de confiance de BenchmarkDotNet)
    se corrige ou s'annule avant le commit.
- **Porte** : toutes les séries identiques à l'unité, plus le banc de performance.

## G16 — Robustesse et confort  *(décision utilisateur du 03/10 ; attend un feu vert)*

- Les défauts hors plan qui gênent l'usage : la triple faute (`386_common.cs:675`, arrêt « non
  transcrit »), l'erreur 104 de l'AT, le Flush des images disque (`img_writeback`).
- Un lanceur et une configuration graphiques : créer une machine, choisir les cartes, créer un
  disque dur vierge de la taille choisie.
- Changer de disquette, de CD ou de ZIP pendant que la machine tourne, depuis un menu.
- Le clavier AZERTY bien traduit ; la capture et le relâchement de la souris.
- Le plein écran, la mise à l'échelle et la correction du rapport largeur/hauteur (CGA/EGA
  640×200).
- Une copie de référence **figée** des disques de l'utilisateur pour les portes (hors `/tmp`,
  empreinte versionnée), pour que ses usages ne fassent plus dériver `g5w`.

Le reste du hors plan — les `fatal()` du Xebec (R9), le 8087 en mod=3, la fuite d'IRQ de
`pm-fuzz`, les sondes, le reliquat du 286, la dette de documentation — peut y être rattaché ou
rester hors plan.

## G17 — Usage avancé  *(décision utilisateur du 03/10 ; attend un feu vert)*

- La sortie MIDI vers l'hôte (FluidSynth et une banque de sons, MT-32 émulé), pour le MPU-401 de
  G12.
- Un son sans craquements : latence réglable, synchronisation sous charge.
- Les sauvegardes d'état (ni PCem ni 86Box ne les ont ; le déterminisme les facilite).
- Les images VHD et les formats de disquette courants.
- La vitesse mesurée sur une machine modeste (pas de dynarec prévu pour le 486).
- Des tests de stabilité de longue durée.
- La publication : versions Windows, Linux et macOS, installateur, documentation utilisateur en
  anglais, et la licence (GPL v2 comme PCem ; les ROM ne sont pas redistribuées).

**Options, non décidées** (à trancher par l'utilisateur au plan de G17) : le réseau NE2000 avec
accès à l'hôte, l'impression LPT vers un fichier ou un PDF, un débogueur intégré.

## Annexe — Les outils de G14/G15  *(décision utilisateur du 03/10)*

Des outils gratuits, en ligne de commande. **Rien n'est installé avant G15**, et chaque
installation demandera l'accord de l'utilisateur au moment venu.

| Outil | Usage |
|---|---|
| **BenchmarkDotNet** (NuGet) | La référence de performance figée avant G14 et les comparaisons à chaque commit (intervalles de confiance) ; la démonstration du coût des constructions interdites. |
| **`DOTNET_JitDisasm`** (intégré à .NET, rien à installer) | Vérifier le code machine produit : inlining, aucun appel virtuel, code identique après renommage. C'est la preuve directe du 0 %. |
| **Microsoft.CodeAnalysis.BannedApiAnalyzers** (NuGet) | Interdire à la compilation LINQ et les API bannies dans les dossiers des chemins chauds. |
| **Un analyseur d'allocations**, par exemple ClrHeapAllocationAnalyzer (NuGet) | Les allocations cachées : boxing, fermetures, `params`. |
| **ReSharper Command Line Tools** (`jb inspectcode`, gratuit) | Les inspections JetBrains sans interface, dans la porte de G15. |
| **dotnet-counters** et **dotnet-trace** (outils .NET gratuits) | Le GC et les allocations pendant un amorçage complet. |

Note : dotTrace et dotMemory (JetBrains, payants, parfois intégrés à Rider) servent à
l'exploration manuelle par l'utilisateur ; ils ne sont pas requis par les portes de G15.
