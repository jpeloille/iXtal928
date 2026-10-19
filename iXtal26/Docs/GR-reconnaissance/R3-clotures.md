# GR.3 — Les deux clôtures (G9 et G10) : la reconnaissance

> Le 20 octobre 2026, au commit `d56d161`. Lecture seule : rien n'a été construit ni lancé (ni série, ni
> émulateur). Les images de `os/` ont été lues par un lecteur FAT jetable, hors du dépôt, en lecture seule.
> Niveaux : **vérifié** (relu dans le fichier cité), **déduit** (tiré de ce qui est relu), **inconnu**.

## 0. En bref

- **Fait** : le XTIDE de la M24 et du PC1512 sous boot-diff (G10.2, `bd-m24-xtide-boot`, `bd-pc1512-xtide-boot`) ;
  le POST et le DOS de chaque carte de G9 sous boot-diff ; `--make-nvr` qui écrit 30h pour la MDA et l'Hercules.
- **Partiel** : Windows avec `HERCULES.DRV` et `EGA.DRV` n'a été joué qu'en C# seul (G9.1, G9.2), jamais sous
  boot-diff, et sans pixels côté oracle (GR.0) ; `MSD /S` joué en G13.10a sur l'ami486 seulement ; `--setup-check`
  (36 contrôles) ne regarde aucune carte de G9 ni rien de G10.
- **À faire** : les quatre profils de G9 ; le CD, LPT et la manette dans les profils 486 (aucune clé dans aucun
  `.cfg` de la racine) ; `MODE`, `ECHO >PRN`, QBASIC `STICK`/`STRIG` sous boot-diff, les outils XTIDE (jamais cités) ;
  la marge de `--timer-check` sous lectures CD (la charge n'existe pas) ; l'écoute, qui demande Julien.
- **Changé par G13** : le mode matériel est celui des profils (G13.11) ; boot-diff le refuse ; les témoins se jouent
  donc deux fois en C# seul (PCem, « tout »), et sous boot-diff en mode PCem seulement, comme en G13.10a.
- **Découpe proposée** : neuf étapes (GR.3a à GR.3i), dont trois sans code de l'émulateur. Deux attendent GR.0.
- **Hors de la portée** : aucun défaut de PCem neuf ; cinq constats de l'hôte en annexe ; `486-HDD-C.img` ne
  correspond plus à `os/os.sha256` (écrite le 18/10 à 16 h 18, après la réécriture du 16/10).

## 1. L'état, point par point

### 1.1 Les profils existants

**`iXtal26/Properties/launchSettings.json`** (vérifié) : 18 profils (`grep -c '"commandName"'`). Machines : le 5150
(`--model ibmpc`, et son jumeau `--hardware-mode pcem`), le XT (`ixtal26-xt.cfg`, et `--model ibmxt`), l'ami286
(Trident 8900D), l'ami386dx (Trident 9000B), l'ami486 en six variantes (GD5429 et son jumeau PCem, S3 Trio64, SCSI,
SB 16, AWE32), la M24, le PC1512, l'écran de construction (`--setup`). Aucun profil n'a de MDA, d'Hercules, d'EGA ni
d'ET4000 ; aucun n'a de CD, de périphérique LPT, de manette déclarée, ni de disque XTIDE.

**Les `.cfg` de la racine** (vérifié, relus en entier) : 12 fichiers (`ixtal26.cfg`, `-xt`, `-286`, `-286-install`,
`-386`, `-486`, `-486-s3`, `-486-scsi`, `-486-sb16`, `-486-awe32`, `-m24`, `-pc1512`). `grep -n -E
'cdrom|lpt1_device|joystick' ixtal26*.cfg` ne rend rien. Seul `ixtal26-xt.cfg` est lu par des portes : `bd-xtcfg` et la
boucle `bd-xtdos-$g` (`tools/gates/series.sh:21-24`) — il ne doit pas changer. Les autres profils ne sont lus par aucun
outil du dépôt (`grep` dans `tools/`, vérifié) ; les commentaires de `MaterielCasScsi.cs:39` et `R9Aha.cs:29` les
citent seulement.

**Les jumeaux PCem** : G13.10a a posé deux profils « mode materiel » (5150, ami486) ; G13.11 (VERIFICATION.md § G13.11,
l. 12194) a retourné la règle : le lanceur part du mode matériel, et ces deux profils demandent désormais `pcem`. Un
profil sans option est donc en mode matériel (vérifié : `Launcher.cs`, `ModeMateriel.DefautUtilisateur`). Aucun texte
n'exige un jumeau par machine (voir D1).

**Les CMOS** : `nvrfopen` compose `nvr/.<machine>.nvr` (`nvr/README.md`, l. 3-13) ; deux profils d'une même machine
partagent leur CMOS. Présents aujourd'hui : `nvr/.ami386dx_opti495.nvr`, `nvr/.ami486.nvr` (vérifié, `ls -la nvr/`) ;
pas de `.ami286.nvr`. Les cartes VGA, EGA et ET4000 posent toutes les bits 5-4 de 0x14 à 00 (`NvrImage.cs:290-292`) :
un profil EGA ou ET4000 partage sans conflit le CMOS d'un profil VGA de la même machine (déduit). La MDA ou l'Hercules
sur une machine à CMOS demanderaient 30h, et un CMOS à part.

### 1.2 Les quatre profils de G9 — à faire

PLAN.md:512-513 : XT + Hercules, 286 + EGA, 386DX + ET4000AX, 5150 + MDA (décision du 04/10, qui supplante « aucun
profil ne change » de PLAN-G9.md:142, du 03/10). Ce qui existe déjà pour chacun, sous boot-diff, en mode PCem
(`series.sh:18-23`, `:273-296`, vérifié) :

| Profil | POST | DOS | Banc | Témoin C# seul de G9 |
|---|---|---|---|---|
| 5150 + MDA | `bd-pc-mda` | `bd-pcdos-mda` | — | — |
| XT + Hercules | `bd-xt-hercules` | `bd-xtdos-hercules` | `bd-pc-herc-banc` (5150) | Windows, disque 486 (§ G9.1) |
| ami286 + EGA | `bd-ami286-ega` (POST) | — | `bd-pc-ega-banc` (5150) | Windows, disque 486 (§ G9.2) |
| ami386dx + ET4000AX | `bd-ami386dx-et4000` | — | `bd-ami386dx-et4-banc` | `VGA.DRV`, disque 386 (§ G9.3) |

En mode matériel, les bancs vidéo sont rejoués en C# seul contre leurs attendus (`banc-*-materiel`,
`series.sh:303-313`, G13.9g). Ni `VER` ni `MODE` n'ont été tapés sur ces cartes.

### 1.3 Le CD, LPT et la manette sur les profils 486 — à faire

Rien dans les profils (§ 1.1). Les clés existent et sont gardées : `cdrom_channel`, `cdrom_drive`, `cdrom_path`,
`cd_speed`, `cd_model` (PLAN-G10.md, décision n° 17 ; `r9-cdcfg`), `lpt1_device` (`pc.cs:275-287`, un nom inconnu
refusé avec la liste), `joystick_type` et `[Joysticks]` (`r9-joycfg`). La décision n° 10 de PLAN-G10 dit que
« c'est le profil ou le `.cfg` qui pose le lecteur » ; la n° 21, que « les profils attendent GR.3 ». Le lanceur n'a ni
`--lpt1` ni `--sndcard` ni option du CD : seul boot-diff les a (`tools/iXtal26.Diff/Program.cs:573-578`) ; les profils
passent donc par des `.cfg` (vérifié, `Launcher.cs:36-120`).

Aucune image de CD dans `os/` (vérifié, `find os -type f`, 154 fichiers). Le BIOS de l'ami486 ne touche jamais le canal
secondaire (§ G10.4) : un lecteur posé ne change rien au démarrage de l'utilisateur, mais il ne sert qu'à un programme
qui parle ATAPI aux ports (ATAPIBANC, ATAPIAUD), faute de pilote DOS (décision n° 5).

La manette : sans `[Joysticks]`, `joystick_0_nr` vaut 0 et la manette de l'hôte n'est pas branchée (`pc.cs`,
`load_joysticks` ; déduit). Il n'y a pas d'écran de réglage (PLAN-G10.md, « Les risques », G10.1).

### 1.4 `--setup-check` et l'écran de construction — partiel

`SdlSetup.SelfCheck` (`Host/SdlSetup.cs:1169-1469`) : 36 sites d'appel de `Check` (compté par `grep -c`), 36
contrôles verts selon VERIFICATION (§ G13.10a, l. 11862 ; § G13.11, l. 12221). Ils couvrent la mémoire, les cinq
cartes VGA (VGA, 9000B, 8900D, GD5429, Trio64), les contrôleurs (MFM, Xebec, AHA), les cartes son (G12.3), la ligne
« Mode » (G13.11) et la navigation. **Aucun contrôle** sur la CGA, la MDA, l'Hercules, l'EGA ou l'ET4000, ni sur les
clés de G10.

L'écran : les lignes sont Modèle, Mémoire, Vidéo, Son, A:, B:, Contrôleur, C:, D:, Mode, Charger, Enregistrer,
Démarrer (`SdlSetup.cs:47-50`). La liste vidéo est celle du registre, filtrée par la seule présence de la ROM
(`BuildVideoList`, `:315-338`) : la MDA, l'Hercules et la CGA n'ont pas de fonction `available` et sont toujours
proposées ; l'EGA demande `ibm_6277356_ega_card_u44_27128.bin` (`vid_ega.cs:1300`), l'ET4000 `et4000.bin`
(`vid_et4000.cs:287`). L'enregistrement (`:858-907`) écrit `joystick_type`, `mouse_type`, `[Joysticks]`, les cinq clés
du CD, `zip_channel`, `zip_path`, `hardware_mode` ; il n'écrit pas `lpt1_device`, que PCem écrit (`pc.c:965`). Effet
déduit : nul, la clé chargée restant dans la configuration en mémoire jusqu'à l'enregistrement (`config_load` vide la
tête, `PluginApi/config.cs:139-146`). L'en-tête de l'écran est périmé (`:31-37` : « deux cartes y sont transcrites, la
CGA et la VGA » ; « omitted : … LPT, souris, joystick » alors que la souris et la manette sont enregistrées).

Ce que « l'écran SETUP compris » doit couvrir n'est pas écrit (voir D9). La M24 et le PC1512 à qui l'écran offre toutes
les cartes relèvent de GR.4 (PLAN.md:521-522).

### 1.5 `--make-nvr` pour chaque carte — partiel

`Host/NvrImage.cs:229` lit `gfxcard` (défaut `cga`) ; `:290-292` : `cga` → 20h, `mda` ou `hercules` → 30h, toute autre
valeur → 00h (carte à ROM). Fait en G9.0 et G9.1 (VERIFICATION.md § G9.0, l. 5424 ; § G9.1, l. 5499). Ce qui manque :
- **aucune porte** ne joue `--make-nvr` carte par carte : la recette g5w l'appelle huit fois, toutes sur des cartes
  VGA (`g5w-recipe.sh:67-74`), et `bd-ibmat-mda` et `bd-ibmat-hercules` tournent avec le CMOS de référence `at.nvr`
  (0x14 = 41h, bits 5-4 à 00 ; `nvr/README.md`), pas avec 30h (déduit : `run` ne copie aucun CMOS, `series.sh:2-5`) ;
- un nom de carte inconnu passe pour une carte à ROM (00h), sans un mot, alors que `loadconfig` le refuse
  (`pc.cs:448`) — vérifié par lecture, non joué ;
- le refus des machines sans CMOS dit « les deux machines à 8088 n'en portent pas » (`NvrImage.cs:266-268`) : le
  registre compte aussi la M24 et le PC1512 (8086), et `nvr/default/pc1512.nvr` existe. Le PC1512 relève de GR.4.

### 1.6 Les témoins

| Témoin (PLAN.md:514-519) | État | Où |
|---|---|---|
| POST | fait pour chaque carte, mode PCem | `series.sh:18-23`, `:273-296` |
| `VER` | fait sur les cartes VGA (XT, AT, ami286, ami386dx, ami486) | § G13.10a, l. 11870-11878 |
| `MSD /S` — la vidéo | ami486 + GD5429, deux modes, C# seul | § G13.10a ; script ci-dessous |
| `MSD /S` — « LPT Ports », « Game Adapter » | inconnu : non relevé | — |
| `MODE` | à faire (aucune mention dans VERIFICATION) | — |
| QBASIC `STICK`/`STRIG` + `--joy-at` sous boot-diff | à faire ; JOYBANC couvre 201h | `series.sh:337-347` |
| `ECHO >PRN` | à faire ; LPTBANC couvre 378h-37Ah | `series.sh:316-326` |
| outils XTIDE de `roms/xtide/` | à faire, jamais cités dans le dépôt | `grep` vide |
| XTIDE de la M24 et du PC1512 sous boot-diff | **fait** (G10.2) | `series.sh:360-362` ; § G10.2 |
| Windows sous boot-diff, `HERCULES.DRV`, `EGA.DRV`, pixels | partiel : C# seul, sans pixels oracle | § G9.1, § G9.2 |
| écoute du Covox, de la DSS, de l'audio CD | à faire, Julien | PLAN-G10.md, risques |
| marge de `--timer-check` avec les lectures CD | à faire | `TimerCheckVerb.cs:45-49` |

Détails :
- **Le XTIDE de la M24 et du PC1512** : `bd-m24-xtide-boot` et `bd-pc1512-xtide-boot` amorcent `c8088dos.img`,
  tapent `VER`, `DIR`, `MD`, `COPY`, `DIR` ; dans `g13b.tsv`, `bd-m24-xtide-boot` : 54 001 687 instructions en 12 s
  (vérifié, `/var/tmp/ixtal-g13/series/g13b.tsv`). Ce point de GR.3 est clos.
- **Le script des témoins de G13.10a** : `/tmp/a9/temoins-10a.sh` n'existe plus ; sa copie est
  `/var/tmp/ixtal-g13/a9-scripts/temoins-10a.sh` (vérifiée) : un bac à sable par machine, `os/` lié image par image,
  `--make-nvr` refait, `--boot … --hardware-mode aucun` puis `tout`, `@shot` pour Windows.
- **QBASIC** a déjà été tapé, en C# seul (§ G4.7, l. 4354) ; jamais sous boot-diff.
- **Les outils XTIDE** (vérifié, `ls -la roms/xtide/`) : `biosdrvs.com` (3 187 octets), `xtidecfg.com` (14 462),
  `serdrive.exe` (40 448, le serveur de disque série, un programme de l'hôte Windows, hors de propos), et les ROM
  `ide_*.bin`. Aucun disque de l'utilisateur ne les porte : il faut une disquette fabriquée (`--create-floppy`,
  `--floppy-put`, `Launcher.cs:45-49`).
- **Windows sous boot-diff** : aucune porte ne tape `WIN` (`grep` dans `series.sh`, vérifié). Les pixels de la MDA, de
  l'Hercules et de l'EGA côté oracle sont l'objet de GR.0 ; `VgaProbe.Fields` a `#buffer32` pour le socle SVGA
  seulement (`tools/iXtal26.Diff/VgaProbe.cs:28-31`).
- **L'écoute** : rien sur les disques de l'utilisateur ne joue sur un Covox ou une DSS (déduit du listage, § 1.8 ;
  ISHAR inconnu). LPTBANC n'écrit qu'une rampe de 256 échantillons. Les pistes audio d'isogen sont des motifs de 1 à
  30 secteurs (`tools/isogen/isogen.py:163-164`, `:292-296`), moins d'une demi-seconde chacune.
- **`--timer-check`** : trois charges, `repos`, `ram`, `x87` (`TimerCheck.cs:411`). Aucune ne lit le CD ; G10.5
  l'a laissé dit (« la cadence avec des lectures CD sur le fil d'émulation n'est pas mesurée », PLAN-G10.md, risques).

### 1.7 Ce que G11, G12 et G13 ont posé

- **La forme d'une clôture** : G11.3 (§ l. 6835) et G12.3 (§ l. 7077) — le profil et son en-tête (les étapes avant
  le premier lancement), la ligne de `launchSettings.json`, les contrôles ajoutés à `--setup-check`, un tableau de
  témoins, le comportement surprenant rejoué sous boot-diff, `os.sha256` et `g5w.sha256`, la série.
- **G13.10a** : les témoins en C# seul, dans les deux modes, dans un bac à sable ; les originaux contrôlés avant et
  après.
- **G13.11** : les verbes outils (`--boot`, `--timer-check`, `--setup-check`, `--menu-check`) partent du mode PCem
  (§ l. 12206) : un témoin du mode matériel doit passer `--hardware-mode tout` ; boot-diff refuse une clé
  `hardware_mode` non nulle (`BootDiff.cs`, `Run`, après `loadconfig`) : un profil ne doit pas la porter.
- **G13.8g3b** : des témoins hors du dépôt (`/var/tmp/ixtal-g13/temoins-8g/`), sans porte ; le même partage de rôles
  (les cas et les bancs vérifient les valeurs, les témoins que le logiciel tourne).
- **G13.12a, G13.13b** : les cavaliers de l'EGA (`monitor_type`, `io_base`) et de la CGA (`font`), les clés `serial1`,
  `serial2`, `parallel1`, `parallel2` ; `r9-videocfg` et `r9-portscfg` les gardent. `MODE` et `MSD` en sont des
  témoins naturels (un port retiré disparaît de la BDA).
- **Les défauts corrigés en mode matériel** qui touchent ces profils : PB-96, 97, 99, 100, 102, 222 à 229, 233, 294
  (vidéo), PB-101 (le LPT2 du PC1512), PB-103 (la manette), PB-123 (l'audio CD) ; PB-104, 105, 124 restent hors du
  mode (PCEM_BUGS.md, champs *Corrigé* et *Non corrigé*, vérifiés). Un témoin peut donc différer entre les modes ; ce
  n'est pas une faute, il faut le dire.

### 1.8 Les logiciels disponibles dans `os/`

Relevé par un lecteur FAT en lecture seule (vérifié) :
- **Les trois disques durs** (`286-HDD-C.img`, `386-HDD-C.img`, `486-HDD-C.img`) : MS-DOS 5.00 avec `\DOS\MODE.COM`,
  `\DOS\QBASIC.EXE`, `\DOS\MSHERC.COM` (le pilote Hercules de QBASIC), `\DOS\PRINTER.SYS`, `\WINDOWS\MSD.EXE` ; Windows
  3.1 réglé en `VGA.DRV` (`\WINDOWS\SYSTEM`, aucun `HERCULES.DRV` ni `EGA.DRV`) ; le 486 a en plus `S3TRIO.DRV`, le 386
  et le 486 `\SBPRO`. Aucun pilote de CD (OAKCDROM, VIDE-CDD, MSCDEX, SHSUCDX). `286-HDD-D.img` est vide.
- **PC-DOS 2.00** (`os/pcdos20/`) : `MODE.COM`, `BASICA.COM` (qui a aussi `STICK` et `STRIG`), `PRINT.COM`, `DEBUG.COM`.
- **MS-DOS 5.0** : `os/Dos 5.0/Disk01-03.img` (720 Ko ; `QBASIC.EX_`, `MSHERC.CO_`, `MODE.CO_`, `EXPAND.EXE`) et
  `os/DOS50/` (une image et cinq répertoires de fichiers de l'hôte).
- **Windows 3.11** : `os/Windows_3-11/` (six images) et `os/IBM_Windows_3-11/` (sept), dont la disquette 2 aux pilotes
  d'affichage, que G9.1 et G9.2 ont utilisée.
- **Le Sound Blaster Pro 2** : deux images (TEST-SBP, VPLAY…), `os/yeager.img`, `os/8088-HDD-C.img` (vierge, 10 Mo).

### 1.9 Ce qu'un témoin sous boot-diff peut et ne peut pas

- **Peut** : taper (`--type`, `--type-at`, `--type-settle`), insérer A: et B: (`--fda`, `--fdb`), poser la carte vidéo,
  la carte son, le périphérique LPT, le modèle de CD, la manette et ses états (`--gfxcard`, `--sndcard`, `--lpt1`,
  `--cd-model`, `--joystick-type`, `--joy-at` ; `Program.cs:573-621`, `BootDiff.cs:414-419`), exiger un état
  (`--expect-cd`, `--expect-sb`), comparer les images écrites et les sondes de fin.
- **Ne peut pas** : le mode matériel (refusé) ; le son de l'hôte (aucune oreille) ; une divergence d'une campagne tapée
  n'est pas localisée par la phase 2, qui ne rejoue ni `--type` ni `--fdb` (`BootDiff.cs:1108-1113`, `:1208` ; GR.4) :
  il faut `--lockstep N` (M19). Les pixels des cartes non SVGA attendent GR.0.
- **Le coût** (relevé dans `g13b.tsv`) : 0,33 million d'instructions par seconde sur l'ami486
  (`bd-ami486-gd5429-dos`, 183 849 253 en 554 s), 1,37 sur l'ami286 (`bd-ami286-mfm-ecriture`, 172 227 813 en 126 s),
  3,5 sur le XT (`bd-xt-xtide-format`, 55 822 590 en 16 s). La plus longue porte, `bd-ami486-zip-banc`, 1 742 s.
- **`--boot`** n'a pas `--joy-at` mais `@manette x,y,b,z,c` (`BootTest.cs:199-210`), ni `--lpt1` (passer par
  `--config`) ; il a `@shot`, `@vram`, `@son`, `@wait` (`BootTest.cs:120-215`).

### 1.10 Les originaux

`os/os.sha256` a été réécrit le 16/10 (`4e8bde8`, Q20). Depuis, `486-HDD-C.img` a été écrite le 18/10 à 16 h 18, avec
`nvr/.ami486.nvr` à 16 h 18 (une session de l'utilisateur, déduit) : son SHA-256 (`5bef743e…`) n'est plus celui du
fichier (`7c325071…`). `286-HDD-C.img` et `386-HDD-C.img` concordent (vérifié, `sha256sum`). Voir D10.

## 2. Ce qui reste, et les étapes proposées

L'ordre : les profils d'abord (rien n'en dépend), puis les outils de l'hôte, puis les témoins, Windows après GR.0,
l'écoute en dernier. Les scripts de témoins hors des portes vivent dans `/var/tmp/ixtal-gr/` (la règle du 18/10 sur
`/tmp`). Aucune étape ne change l'émulateur, sauf mention : la série entière n'est pas due ; un sous-ensemble ciblé
suffit (règle « Série entière : seulement si l'émulateur change »).

### GR.3a — Les quatre profils de G9

- **Contenu** : `ixtal26-5150-mda.cfg`, `ixtal26-xt-hercules.cfg`, `ixtal26-286-ega.cfg`, `ixtal26-386-et4000.cfg`,
  courts, qui renvoient aux en-têtes de `ixtal26.cfg`, `-xt`, `-286`, `-386` ; quatre lignes dans
  `launchSettings.json`, en mode matériel (D1). Le 286 et le 386 gardent disque, CMOS et carte son de leur profil
  VGA ; l'en-tête dit la limite de Windows (D3). Le XT selon D2.
- **Portes et preuves** : deux portes boot-diff sur les profils à disquette, lus tels quels comme `bd-xtcfg` :
  `bd-cfg-5150-mda` et `bd-cfg-xt-hercules` (POST, `VER`, `MODE`, `DIR`). Les deux profils à disque, en C# seul et
  dans les deux modes (le bac à sable de G13.10a) : `VER`, `MODE`, `MSD /S` (§ GR.3d). Les originaux avant et après.
- **Contrôles négatifs** : `gfxcard` du profil remplacé par `cga` dans une copie : la sonde mono ou EGA de la porte
  disparaît ou change (rouge) ; une clé `hardware_mode = 1` ajoutée à une copie : boot-diff refuse (2).
- **La série** : deux portes neuves ; rien d'autre ne bouge.

### GR.3b — Le CD, LPT et la manette dans les profils 486

- **Contenu** : selon D4, les clés du lecteur (canal 2, lecteur d'images vide ou image de D6, `cd_model` explicite),
  `joystick_type = 0` et `[Joysticks]` (la manette 1 de l'hôte, axes 0-1, boutons 0-1), `lpt1_device` ; leur
  explication dans l'en-tête (le BIOS ne voit pas le CD ; aucune ligne à l'écran ; le Covox ou la DSS sur 378h).
- **Portes et preuves** : `--boot --config` de chaque profil touché, deux modes : le POST identique à celui d'avant
  (aucun recompte : LPT et le port jeu sont déjà là depuis G10.0) ; `--expect-cd` n'existe que dans boot-diff, donc une
  porte `bd-cfg-486-cd` sur une copie `.cfg.in` (le disque de l'utilisateur change, D10) : le lecteur présent des deux
  côtés.
- **Contrôles négatifs** : la clé du canal retirée de la copie : `--expect-cd` rougit ; `lpt1_device` mal orthographié :
  le refus (2) avec la liste.
- **La série** : une porte neuve.

### GR.3c — `--setup-check` étendu et `--make-nvr` pour chaque carte

- **Contenu** (code de l'hôte) : dans `SelfCheck`, pour chaque carte de G9 et la CGA, « proposée » (selon sa ROM) et
  « la choisir pose `gfxcard` », sur une machine XT et une machine AT ; un aller-retour Charger → Enregistrer
  d'un `.cfg` qui porte `lpt1_device`, `joystick_type`, `[Joysticks]`, les clés du CD, les sections de l'EGA et de
  la SB : chaque clé relue identique (cela tranche le constat de § 1.4 par une preuve plutôt qu'une déduction).
  Dans `NvrImage.Make` : un `gfxcard` inconnu refusé (2), comme `loadconfig` ; le message des machines sans CMOS
  corrigé (la M24 et le PC1512 sont des 8086). Un contrôle `nvr-check` (ou des cas dans `config-check`) : pour
  chaque carte du registre, l'octet 0x14 attendu (20h, 30h, 00h) et la somme non nulle ; puis deux POST sous
  boot-diff avec un CMOS à 30h : `bd-ibmat-mda-cmos` et `bd-ami286-hercules-cmos` (CMOS de la recette g5w), sans
  message d'affichage au POST.
- **Contrôles négatifs** : l'Hercules ramenée à 00h dans `NvrImage` : `nvr-check` rougit, et le POST du 286 Hercules
  affiche « CMOS display type mismatch » (attendu, à mesurer) ; une carte retirée de la liste de l'écran :
  `--setup-check` rougit ; `lpt1_device` non relu à l'aller-retour (la ligne de configuration effacée avant
  l'enregistrement, dans une copie) : rouge.
- **La série** : `--setup-check` change de compte ; la recette g5w gagne deux CMOS et deux lignes de `g5w.sha256`,
  « l'ajout seulement » (la règle de G10.2) ; deux portes. M2 sur les méthodes de l'hôte touchées (des jetons de
  chaînes, comme en G13.11).

### GR.3d — Les témoins DOS par machine : `VER`, `MODE`, `MSD /S`, `ECHO >PRN`

- **Contenu** : le bac à sable de G13.10a, étendu aux quatre profils de GR.3a et aux profils 486 de GR.3b, deux modes.
  `MODE` (PC-DOS 2.00 sur le 5150 et le XT ; `MODE CON`, `MODE LPT1` et `MODE /STATUS` sous DOS 5) ; `MSD /S` sur les
  machines AT, la sortie copiée dans un fichier puis `TYPE` (la recette de 10a), relevées : la ligne vidéo, « LPT
  Ports », « Game Adapter » ; `ECHO A >PRN` avec `lpt1_device` à `none`, `dss` et `lpt_dac` (ce que dit DOS est le
  témoin, « Write fault » compris). Sous boot-diff, en mode PCem : `bd-pc-lpt-dss-prn` (5150, PC-DOS 2.00, `ECHO >PRN`
  puis `@son`), court ; `MSD` sous boot-diff sur l'ami286 seulement si son coût le permet (§ 1.9).
- **Le comportement surprenant** : PB-101 (le PC1512 garde son LPT2 en mode PCem) lu par `MODE` ou DEBUG dans les deux
  modes ; PB-99 (l'EGA de PCem passe pour une VGA à son BIOS) : ce que `MSD` en dit dans chaque mode.
- **Contrôles négatifs** : `parallel2 = 0` (G13.13b) : LPT2 disparaît de `MODE` et de `MSD` ; `joystick_type` hors
  borne : ramené au défaut (déjà `r9-joycfg`) — le témoin seul ne prouve rien, il est relu.
- **La série** : une porte neuve au plus ; le reste hors des portes.

### GR.3e — QBASIC `STICK` et `STRIG` sous boot-diff

- **Contenu** : sur l'ami286 (D7), `c286.img` de la recette (QBASIC présent, `KEYB FR` retiré), un programme tapé
  (`COPY CON` ou l'éditeur de QBASIC) qui imprime `STICK(0)`, `STICK(1)`, `STRIG(1)`, `STRIG(5)` dans une boucle
  bornée ; trois ou quatre états injectés par `--joy-at`, dont des boutons ; puis le même script en C# seul, deux
  modes, avec `@manette`.
- **Portes et preuves** : `bd-ami286-qbasic-stick` ; les valeurs relues à l'écran, différentes d'un état à l'autre.
- **Contrôles négatifs** : un `--joy-at` d'un côté seulement (une faute du harnais dans une copie) : divergence ;
  `--joystick-type 1` (quatre axes) : l'écran change.
- **La série** : une porte, quelques minutes (§ 1.9).

### GR.3f — Les outils XTIDE

- **Contenu** : la recette g5w fabrique `xtide-outils.img` (360 Ko, `--create-floppy` puis `--floppy-put` de
  `biosdrvs.com` et `xtidecfg.com`), une ligne de `g5w.sha256`. `BIOSDRVS` lancé depuis B: sur le XT, la M24 et le
  PC1512 qui amorcent `c8088dos.img` ; `XTIDECFG` ouvert puis quitté (Échap) sur le XT seulement, sans rien écrire.
- **Portes et preuves** : `bd-{xt,m24,pc1512}-xtide-outils` ; l'image C: inchangée des deux côtés.
- **Contrôles négatifs** : l'octet haut du verrou faussé (le contrôle de G10.2) : rouge ; `BIOSDRVS` sur une copie
  sans XTIDE : il ne liste pas le disque (un témoin, pas une porte).
- **La série** : trois portes.

### GR.3g — Windows sous boot-diff, `HERCULES.DRV` et `EGA.DRV` (après GR.0)

- **Contenu** : deux disques préparés par la recette, en C# seul, depuis `c286.img` : le SETUP DOS de Windows, affichage
  « Hercules » ou « EGA », la disquette 2 en B: (la recette de G9.1 et G9.2, rejouée par un script de touches) ;
  `c286-herc.img` égal octet par octet à l'image C: qu'une porte d'installation écrirait, comme `c8088dos.img`
  (§ G10.2), si la durée le permet, sinon empreinte seule. Puis `WIN` sous boot-diff sur l'ami286 (mode standard,
  D8) : `bd-ami286-win-ega`, `bd-ami286-win-herc` (CMOS 30h de GR.3c), la sonde de GR.0 qui hache l'image.
- **Portes et preuves** : les deux portes, pixels compris ; en C# seul, deux modes, l'image `@shot` comparée.
- **Contrôles négatifs** : la panne injectée de GR.0 pour chaque carte ; `EGA.DRV` sur l'Hercules : Windows ne démarre
  pas (constat).
- **La série** : deux portes longues (déduit : quelques minutes chacune à 1,4 million d'instructions par seconde).

### GR.3h — La marge de `--timer-check` sous lectures CD

- **Contenu** (code de l'hôte) : `--charge cd`, une boucle en RAM comme `x87` (`TimerCheck.cs:417-475`) qui lance une
  lecture audio (PLAY AUDIO MSF) puis répète des READ(10) en PIO sur le lecteur du canal 2, l'image de la recette ou
  celle de D6 ; la marge mesurée dans les deux modes sur l'ami486, à côté de `repos` et `ram`.
- **Preuves** : la marge (décision n° 7 de PLAN-G13 : supérieure à 1 en mode matériel), le compte de secteurs lus et
  d'échantillons CD, la ligne `EMPREINTE`.
- **Contrôles négatifs** : `--charge cd` sans lecteur : refus (2), comme `x87` sans coprocesseur (`:455-457`).
- **La série** : aucune porte ; M2 de `TimerCheck` (hors des portes).

### GR.3i — L'écoute, par Julien

- **Contenu** : `LPTPLAY.COM` (comme `lptbanc.py --com`) qui joue sur 378h une gamme reconnaissable, au rythme du PIT,
  en DSS (la file, 7 kHz) et en Covox (mono, puis la stéréo par 37Ah) ; une piste audio de D6 ; ATAPIAUD en `.COM`
  (`atapibanc.py --audio --com`). Une disquette fabriquée par une recette (D5), et un mode d'emploi court dans l'en-tête
  des profils de GR.3b. Julien lance la fenêtre et écoute ; le compte rendu est le sien.
- **Preuves** : sa réponse, inscrite telle quelle ; en face, `@son` en C# seul dans les deux modes.
- **La série** : aucune.

### La clôture (avec GR.5)

Le tableau des témoins de GR.3 dans VERIFICATION.md § GR ; `os.sha256` et `g5w.sha256` ; ce que ce vert ne dit pas
(aucun pilote de CD DOS, aucune manette réelle, l'écoute subjective).

## 3. Les risques

- **Les disques de l'utilisateur bougent** : `486-HDD-C.img` a encore changé (§ 1.10). Toute porte doit passer par une
  copie de la recette (`c286`, `c386`), jamais par un profil qui pointe vers `os/`, sauf pour les disquettes.
- **Le coût de boot-diff** : Windows en 386 étendu sur l'ami486 (déduit : plusieurs centaines de millions
  d'instructions à 0,33 million par seconde) dépasserait l'heure ; d'où l'ami286 en mode standard (D8). Le quota de
  `/tmp` (l'incident du 04/10) : les traces de ces portes sur disque (`TMPDIR` de la série).
- **La phase 2 ne localise pas** une campagne tapée (GR.4) : un rouge de GR.3d-g se lit par `--lockstep`.
- **Le témoin du mode matériel diffère** de celui du mode PCem (§ 1.7) : la cadence (PB-102), les palettes (PB-99,
  PB-294), le curseur (PB-222, 223). Comparer les écrans après les frappes, pas l'empreinte au relevé (la leçon de 10a).
- **QBASIC et `STICK`** : la valeur dépend de la vitesse du processeur (une boucle chronométrée) ; elle est la même des
  deux côtés, mais change entre l'ami286 et l'ami486 ; les états de `--joy-at` se calent sur les tranches, comme JOYBANC
  (PLAN-G10.md, risques, G10.1).
- **MSD sur le 5150 et le XT** : la version de Windows 3.1 demande peut-être un DOS plus récent que PC-DOS 2.00
  (inconnu) ; `MODE` et DEBUG y suffisent.
- **`ECHO >PRN`** sans périphérique : le BIOS peut attendre longtemps la fin de son délai (inconnu) ; borner la tranche.
- **Le partage du CMOS** : un profil Hercules ou MDA sur une machine à CMOS écraserait celui du profil VGA voisin ;
  aucun des quatre profils de GR.3a ne le fait, les portes de GR.3c ont leurs CMOS dans le WORK.

## 4. Les décisions ouvertes

Seules celles que PLAN.md § GR, PLAN-G9, PLAN-G10, PLAN-G13 (« Les décisions ») et VERIFICATION laissent ouvertes.

- **D1 — Les jumeaux PCem.** G13.11 ne dit que le mode par défaut. *Proposé* : pas de jumeau pour les profils neufs ; le
  mode PCem de ces machines est couvert par les portes boot-diff.
- **D2 — Le disque du XT + Hercules.** Aucun profil n'a de XTIDE (PLAN-G10.md, risques, G10.2) ; `os/8088-HDD-C.img` est
  vierge. *Proposé* : disquette seule pour le profil ; le XTIDE reste celui des portes (`c8088dos.img`) et de GR.3f.
  Poser le XTIDE sur `os/8088-HDD-C.img` ferait écrire l'utilisateur dans `os/` (la question de Q20).
- **D3 — Windows du 286 + EGA.** Le Windows du disque 286 est réglé pour la VGA (§ 1.8). *Proposé* : l'en-tête du
  profil le dit (« `WIN` demande d'abord le SETUP DOS de Windows, sur une copie ») ; aucun second disque dans `os/`.
- **D4 — Où vont le CD, LPT et la manette.** PLAN.md dit « les profils 486 ». *Proposé* : `ixtal26-486.cfg` et
  `-486-s3.cfg` reçoivent le lecteur (canal 2, vide) et la manette ; LPT1 reste `none` dans le profil de référence, et
  deux profils courts, `ixtal26-486-dss.cfg` et `ixtal26-486-covox.cfg`, servent l'écoute (le lanceur n'a pas `--lpt1`).
- **D5 — La disquette des outils et de l'écoute.** *Proposé* : fabriquée par la recette dans le WORK pour les portes, et
  par un script dans `/var/tmp/ixtal-gr/` pour la fenêtre de Julien, passée par `--floppy-b` ; rien d'ajouté à `os/`.
- **D6 — La source de l'audio CD.** *Proposé* : isogen gagne une feuille d'écoute (deux pistes : 440 Hz à gauche puis
  880 Hz à droite, dix secondes chacune), sans rien demander ; un vrai CD de Julien, s'il en a une image, en plus.
- **D7 — La machine de QBASIC sous boot-diff.** *Proposé* : l'ami286 (`c286.img`, quatre fois plus rapide que l'ami486
  sous boot-diff, § 1.9) ; l'ami486 en C# seul.
- **D8 — La machine de Windows sous boot-diff.** *Proposé* : l'ami286 en mode standard, l'EGA et l'Hercules (CMOS 30h,
  GR.3c) ; le 386 étendu reste en C# seul.
- **D9 — Les lignes de l'écran pour LPT, la manette et le CD.** L'en-tête de l'écran renvoie à « GR.3, G16 »
  (`SdlSetup.cs:36-37`) ; G16 porte « un lanceur et une configuration graphiques » (PLAN.md:592-593). *Proposé* : en
  GR.3, des contrôles seulement (les cartes de G9, l'aller-retour des clés) ; les lignes neuves en G16.
- **D10 — `os.sha256` et le disque 486.** *Question pour Julien* (comme Q20) : l'état du 18/10 devient-il la référence ?
  *Proposé* : oui, une ligne réécrite dans un commit `os:` à part, avant la clôture.
- **D11 — La série des étapes de l'hôte** (GR.3c, GR.3h). *Proposé* : un sous-ensemble ciblé (`config-check`, les portes
  neuves, celles qui lisent un CMOS de `--make-nvr`, la recette g5w rejouée) et M2 des méthodes touchées ; pas de série
  entière, l'émulateur ne changeant pas.

## Annexe — Candidats, hors de la portée

**Défauts de PCem neufs** : aucun.

**Constats de l'hôte d'iXtal** (non des PB ; relus, non joués) :
1. `NvrImage.cs:229`, `:292` : `gfxcard` inconnu traité comme une carte à ROM (00h), sans avertissement.
2. `NvrImage.cs:266-268` : « les deux machines à 8088 » ; le registre a aussi la M24 et le PC1512 (8086), et
   `nvr/default/pc1512.nvr` existe (GR.4).
3. `SdlSetup.cs:858-907` : `lpt1_device` n'est pas écrit (`pc.c:965` l'écrit) ; sans effet déduit (§ 1.4).
4. `SdlSetup.cs:31-37` : l'en-tête périmé (les cartes, les périphériques omis) ; à joindre aux en-têtes de GR.4.
5. `os/486-HDD-C.img` différente de `os/os.sha256` depuis le 18/10 (D10).
