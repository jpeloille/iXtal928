# G10 — Le 486 complet : LPT1, la manette, le XTIDE, le CD-ROM et le ZIP ; le plan, sur reconnaissance

> Écrit le 4 octobre 2026, après G9. Bloc G10 de `PLAN.md` (décision utilisateur du 03/10, feu vert
> du 04/10). Chaque constat cite la ligne de C qui le fonde, sur `pcem-dev/` tel que vendoré. Les
> décisions sont en fin de fichier, toutes validées le 04/10. Remis d'aplomb le même jour selon le
> plan qualité du 04/10 (§ 1a) : décisions datées, deux erreurs corrigées, le port jeu passé de G10.1
> à G10.0 (un seul recompte du POST), le registre R9 recompté à la ligne, « Les risques ».

## Où on en est

- **LPT1** : `Lpt/lpt.cs` n'a que `lpt1_write/read` sans périphérique, pour l'Amstrad.
  `common_init` omet `lpt_init()` (`model.c:195`, `model.cs:417`), qui pose DEUX ports, LPT1 en
  378h et LPT2 en 278h (`lpt.c:142-145`) ; `resetpchard` omet `lpt1_device_init()` (`pc.c:353`,
  `:376` ; `pc.cs:770`), des deux côtés : **aucune machine n'a de port parallèle aujourd'hui**.
- **La manette** : `Joystick/gameport.cs` porte `gameport.c` et `joystick_standard.c` (G1, pour la
  M24 et le PC1512, sans manette branchée). `xt_init` et `at_init` omettent `gameport_device`
  (`model.c:208`, `:344` ; `model.cs:179`, `:432`), et `runpc` omet `joystick_poll()`
  (`pc.c:493`, `pc.cs:970`).
- **Le XTIDE** : rien. `xtide.c` (121 lignes, la variante XT seule) repose sur l'`ide.c` de G5 et la
  ROM `ide_xt.bin`, présente.
- **Le CD-ROM et le ZIP** : `ide.cs` omet tous les crochets ATAPI (`ide.c:78`, `:282-298`, `:311`,
  `:567-574`, `:643-708`, `:735`, `:794-812`, `:1050-1118`) et `pc.cs:450-460` refuse
  `cdrom_channel`/`zip_channel` ≥ 0 ; l'oracle force les deux à -1 (`harness.c:1477-1481`).

## Ce qu'il y a à transcrire

| | lignes | atteint |
|---|---:|---|
| `lpt/lpt.c` (+ `lpt_dac.c` 98, `lpt_dss.c` 89) | 172 | `lpt_init`, `lpt1_device_init/close`, le registre (none, DSS, DAC, DAC stéréo) |
| `joystick/*.c` (CH Flightstick Pro 74, SideWinder pad 205, TM FCS 74) | 353 | les types de manette ; `joystick_poll` vers l'hôte |
| `ide/xtide.c` | 121 | `xtide_device` seul ; `xtide_at` et `xtide_ps1` exclus (PLAN.md) |
| `ide/ide_atapi.c` | 500 | tout (le DMA n'a pas de contrôleur bus master sur ces machines) |
| `scsi/scsi.c` | 352 | `bus_update`, `read`, `kick`, `atapi_init`, `reset` |
| `scsi/scsi_cd.c` | 1 707 | tout (dont la table des modèles, 251-455) |
| `scsi/scsi_zip.c` | 1 111 | tout sauf les branches non-ATAPI |
| `cdrom/cdrom-image.cc` + `dosbox/cdrom_image.cpp` + `cdrom.h` | 500 + 557 + 172 | ISO et CUE/BIN, pistes de données et audio — **C++** |
| `cdrom/cdrom-null.c` | 79 | le lecteur sans disque |
| crochets d'`ide.c`, bloc CD de `pc.c`, chemin CD de `sound.c` | ≈ 190 | |

Soit ≈ 4 400 lignes atteintes pour le CD/ZIP, ≈ 650 pour le reste. Les lecteurs physiques de
l'hôte (`cdrom-ioctl-*`) sont exclus (déterminisme, PLAN.md).

## Les défauts relevés (à lire à la ligne, puis inscrire)

**R9 — l'invité arrête ou fait tomber l'hôte** (non reproduits ; `// pcem bug, not reproduced`).
Recompté à la ligne le 04/10 (`grep -n 'fatal('`, commentaires exclus) : **31 `fatal()` vivants**
dans les quatre fichiers, plus deux arrêts provoqués par la configuration.
1. Le pont ATAPI : **17** `fatal()` vivants dans `ide_atapi.c` (18 occurrences, `:208` en
   commentaire) : `:102`, `:106`, `:110`, `:157`, `:159`, `:164`, `:173`, `:205`, `:221`, `:281`,
   `:296`, `:299`, `:324`, `:346`, `:362`, `:416`, `:453` — atteignables par un pilote qui envoie un
   PACKET pendant une phase en cours, ou qui lâche le bus. Et **2** dans `scsi.c` (`:85`, `:264`).
2. `scsi_cd.c` : **2** vivants (`:858` et `:871` en commentaire) — MECHANISM STATUS de longueur 0
   (`:992-993`) ; `data_out` dépassé (`:1581`).
3. PACKET avec le bit DMA sans bus master : appel par pointeur nul (`ide_atapi.c:396-397`, `:473`,
   `:482`) — le cas de toutes les machines du dépôt.
4. ZIP : **10** `fatal()` dans `scsi_zip.c` (`:224`, `:659`, `:663`, `:713`, `:717`, `:771`, `:776`,
   `:832`, `:837`, `:990`) ; READ(10) de plus de 512 secteurs, débordement de `data_in[256K]` (`scsi_zip.c:703-727`) ;
   transfert de longueur nulle, `read_complete` jamais vrai et lecture hors de la structure
   (`scsi.c:241`, `scsi_zip.c:996`) ; WRITE de plus de 512 secteurs, `fatal()` (`:989-990`) ;
   WRITE au-delà du secteur 196 607, `transfer_sectors` négatif vers `fwrite`
   (`hdd_file.c:206-212`).
5. CD : READ dont le remplissage échoue en route (LBA proche de la fin), lecture au-delà de
   `data_pos_write` (`scsi_cd.c:1565`) ; `cdlen * 2048` / `* 2352` qui débordent (`:1057`, `:1149`).
6. **Par la configuration** (traitement de PB-93 : hors liste → défaut, avec un avertissement) :
   `cd_speed = 0` pose `cur_speed` à 0 (`scsi_cd.c:227-228`), puis divise par lui (`:1081`,
   `:1170`) ; `joystick_type` hors borne indexe hors de `joystick_list` (`gameport.c:27`, `:132`).

Chaque site reçoit son traitement, R9 ou PB-93, et un **test de survie en C# seul** qui rougit si
l'on retire sa garde : `r9-atapi` (G10.4), `r9-zip` (G10.6), `r9-cdcfg` (G10.4, `cd_speed`), et le
`joystick_type` hors borne en G10.1.

**Observables, à reproduire** : READ CAPACITY rend la fin + 1 (`cdrom-image.cc:475`) ; GET EVENT
STATUS toujours « NEW_MEDIA » (`scsi_cd.c:582-598`) ; MODE SELECT (`:756`) ; le format de READ TOC
pris dans `cdb[9] >> 6` (`:1013`), la TOC brute sans lead-out ; `playaudio` et `image_seek` décalés
de 150 secteurs (`cdrom-image.cc:83`, `:138`) ; l'échec collant d'un `ifstream` (`failbit`) ;
READ CD brut sur piste cuite qui laisse des données périmées.

**Comportement indéfini, sans arrêt** : `ReadSectors` qui copie un tampon non initialisé après un
échec (`cdrom_image.cpp:145`) ; `image_getcurrentsubchannel` qui écrit des champs non initialisés
(`cdrom-image.cc:208-249`). Déterministes côté C# (zéros), à effacer côté oracle.

**État statique qui traverse les amorçages** : `old_cdrom_drive`, `image_changed`,
`cdrom_capacity`, `image_cd_*`, `cd_data`, `zip_data`, `mode_pages_in`, `page_flags`, l'état
`atapi` des `ide_drives[]`. Remis à zéro à l'amorçage des deux côtés (comme `opl[]`, G8).

**Le non-déterminisme** : l'audio CD tourne dans un fil (`sound_cd_thread`, `sound.c:143-199`)
qui avance `image_cd_pos` et partage l'`ifstream` avec les lectures ATAPI. Décision n° 7.

## Les étapes

### G10.0 — LPT1, LPT2 et le port jeu : le seul recompte du POST  ✅ *fait, VERIFICATION.md § G10.0*

`lpt_init` dans `common_init` (LPT1 en 378h, LPT2 en 278h), `lpt1_device_init/close` dans
`resetpchard`/`closepc`, le registre `lpt_devices` (none, DSS, DAC, DAC stéréo — `lpt_dac.c`,
`lpt_dss.c`, de petits DAC qui alimentent le mixeur, vus par la sonde du son) ; et
`gameport_device` sur `xt_init` et `at_init` (`model.c:208`, `:344`), sans manette branchée
(`joystick_type` 0) — décision de l'utilisateur du 04/10 : **un seul recompte du POST**, LPT et port
jeu ensemble. **Le POST de toutes les machines change** : la série recompte presque toutes les
portes. **La preuve** : un interrupteur d'outil « LPT et port jeu hors service », des deux côtés
(`--lpt-jeu-hors-service`, ou `IXTAL26_LPT_JEU_HORS_SERVICE=1` pour la série entière), qui rend
EXACTEMENT les comptes de g93 ; « Ce qui ne doit pas bouger » (`PLAN-286.md`, `PLAN.md`) rebasé
avec elle, comme à M21. **Une porte doit pouvoir échouer** : `--lpt1` et la clé `lpt1_device`
refusent un nom inconnu (retour 2), comme `--sndcard` ; PCem le prendrait pour « aucun » et la
comparaison du son serait coupée. **Porte** : un banc LPTBANC (écritures de données et de contrôle,
lectures d'état, la file de la Sound Source remplie ; le Covox et la Sound Source sous la sonde du
son), avec chacun des trois périphériques ; contrôles négatifs (DSS faussée, nom faux).

### G10.1 — La manette  ✅ *fait, VERIFICATION.md § G10.1*

Les types de manette (CH Flightstick Pro, SideWinder, TM FCS en plus des quatre standard),
`joystick_poll` vers l'hôte (SDL) ; dans iXtal26.Diff, `--joystick-type N` et
`--joy-at TRANCHE:x,y,boutons` injectés des deux côtés, comme la souris de PS2.0. Le port jeu
lui-même est entré en G10.0. La clé `joystick_type` hors borne indexe hors de `joystick_list`
(`gameport.c:27`, `:132`) : ramenée au défaut avec un avertissement (traitement de PB-93), et
`--joystick-type` refuse un type inconnu (retour 2). **Porte** : un banc JOYBANC qui chronomètre les
axes par 201h et lit les boutons, mouvements injectés ; contrôle négatif.

### G10.2 — Le XTIDE (XT)

`xtide_device` sur l'`ide.c` de G5, ROM `ide_xt.bin` en C8000 ; `hdd_controller = xtide`.
**Porte** : boot-diffs 5150 et XT amorçant un disque dur par le XTIDE (copie de
`os/8088-HDD-C.img` dans `$WORK`, recette `g5w`). **Témoin** : la M24 et le PC1512 amorcent enfin
un disque dur (le Xebec ne le faisait pas, constat de G1).

### G10.3 — Le moteur d'images CD, ISO

`cdrom_image.cpp` (BinaryFile, `LoadIsoFile`, `CanReadPVD`, pistes, `ReadSector(s)`),
`cdrom-image.cc` côté données, `cdrom-null.c`. L'oracle compile `harness_cdrom.cpp` (en C++, comme
DBOPL) qui les inclut. **Un générateur d'images** versionné, `tools/isogen/` (ISO 9660 minimal,
déterministe, en Python ; aucun outil n'est installé sur l'hôte) : ISO à 2048, 2352 et mode 2.
**Porte** : `cdimage-check`, le moteur appelé des deux côtés sur ces images (TOC, capacité,
secteurs).

### G10.4 — L'ATAPI : le CD-ROM sur l'IDE

`scsi.c`, `ide_atapi.c`, les crochets d'`ide.c`, `scsi_cd.c` ; les clés `cdrom_drive`,
`cdrom_path`, `cdrom_channel`, `cd_speed`, `cd_model` ; le refus de `pc.cs:450-460` levé. R9 n° 1,
2, 3, 5. **Porte** : boot-diffs ami486 avec un lecteur CD (canal secondaire, `cdrom_channel = 2`)
vide et chargé ; un banc ATAPIBANC qui parle ATAPI directement aux ports (IDENTIFY PACKET,
INQUIRY, TEST UNIT READY, READ CAPACITY, READ(10) du PVD, READ TOC, MODE SENSE, REQUEST SENSE).
**Témoin** : décision n° 5.

### G10.5 — CUE/BIN et l'audio CD

`LoadCueSheet` et les pistes multiples ; `playaudio`, pause, sous-canal ; le chemin CD de
`sound.c` (volume et canal ATAPI, `cd_vol` de la carte son). **Porte** : `cdimage-check` sur un
CUE/BIN généré (une piste de données, deux pistes audio) ; ATAPIBANC étendu (PLAY AUDIO, READ
SUBCHANNEL, PAUSE) ; l'empreinte des échantillons CD dans la sonde du son.

### G10.6 — Le ZIP

`scsi_zip.c` sur le `hdd_file.cs` existant ; R9 n° 4. **Porte** : boot-diff ami486 avec un ZIP
(image de 100 663 296 octets, fabriquée dans `$WORK`) ; un banc ZIPBANC (READ/WRITE(10), les
erreurs, l'éjection) ; l'image comparée des deux côtés après écriture.

## Les décisions

1. **L'ordre** : LPT1 et port jeu → manette → XTIDE → images CD → ATAPI CD → CUE/BIN et audio → ZIP,
   les petits morceaux d'abord. *(validé sous mandat, 04/10)*
2. **Le port jeu et LPT1 sur toutes les machines XT et AT**, comme PCem (`model.c:195`, `:208`,
   `:344`) : c'est le matériel de ces machines. Un seul recompte du POST, en G10.0, prouvé par
   l'interrupteur « LPT et port jeu hors service ». *(validé par l'utilisateur, 04/10)*
3. **Les périphériques LPT** : none (défaut), DSS, Covox, Covox stéréo, ceux du registre de PCem ;
   l'impression vers un fichier reste en option de G17. *(validé par l'utilisateur, 04/10)*
4. **Les manettes** : les quatre types standard plus CH Flightstick Pro, SideWinder, TM FCS (353
   lignes). *(validé sous mandat, 04/10)*
5. **Le témoin CD-ROM** : ATAPIBANC seul, qui parle ATAPI directement aux ports ; pas de témoin
   DOS, rien à demander à l'utilisateur. La limite est dite, comme en G9.3 : aucun pilote ATAPI DOS
   (OAKCDROM.SYS, VIDE-CDD.SYS) ni MSCDEX / SHSUCDX sur ses disques. *(validé par l'utilisateur,
   04/10)*
6. **L'image ZIP** : PCem n'a pas de clé de configuration (elle ne se charge que par l'interface,
   `wx-sdl2.c:776`) et la perd à chaque réinitialisation matérielle (`scsi_zip.c:165-177`)
   : une clé `zip_path` et une option `--zip IMG`, DEVIATION ; la perte au reset reproduite.
   *(validé sous mandat, 04/10)*
7. **L'audio CD** : le fil de PCem n'est pas déterministe : le rappel `image_audio_callback` est
   appelé de façon synchrone à l'échéance de `sound_poll`, DEVIATION inscrite, des deux côtés ; la
   sortie reste la voie hôte séparée de PCem, `givealbuffer_cd`, hachée par la sonde du son.
   *(validé sous mandat, 04/10)*
8. **Les ZIP et CD en écriture** : les portes travaillent sur des copies (`$WORK`), jamais sur une
   image de l'utilisateur. *(acquis, 07bdb5b)*
9. **Ce fichier** : `PLAN-G10.md`, commité avec G10.0. *(validé, 04/10)*
10. **Le canal par défaut du CD** : PCem le met à 2 (`pc.c:703`), iXtal à -1 (`Ide/ide.cs:140`). On
    garde -1, la DEVIATION existante : aucun CD par défaut, c'est le profil ou le `.cfg` qui pose le
    lecteur (le canal 2 percuterait l'E: des portes ide-check et changerait le POST de tout ami486).
    *(validé par l'utilisateur, 04/10)*

## Les risques

Ce que G10 laisse, et qui est renvoyé au **bloc GR** (la reprise de G9 et G10, en fin de plan,
`PLAN.md`) ; chaque étape y ajoute ce qu'elle laisse :

- **G10.0** : l'état de la file de la DSS et du DAC n'est pas dans une sonde (le diff
  d'instructions voit l'état lu par 379h, la sonde du son les échantillons). Une faute du seuil de
  la file de la DSS n'est vue **que par le diff d'instructions** : l'écart dure une vidange à
  7 kHz (≈ 143 µs) pendant les lectures de LPTBANC, puis les relevés se rejoignent ; ni l'écran ni
  la sonde du son ne le voient (VERIFICATION.md § G10.0). Aucun témoin réel (`MSD /S` « LPT
  Ports » et « Game Adapter », `ECHO >PRN`), aucune écoute du Covox ni de la DSS ; LPT et port jeu
  absents des profils.
- **L'outil (GR.4)** : la phase 2 de boot-diff ne rejoue ni `--type` ni `--fdb` (`BootDiff.cs:490`,
  `:744` ; § M15). Sur une campagne tapée, elle rejoue donc une autre exécution, et son message
  (« les états concordent… vérifier TraceHash contre h_trace_note ») accuse à tort le hachage de
  trace. Vu en G10.0 sur le contrôle négatif de la DSS, dont la vraie divergence est une lecture de
  379h, la file à 15 (VERIFICATION.md § G10.0).
- **L'incident du 4 octobre** : la série de G10.0 a dépassé le quota de `/tmp` (tmpfs `usrquota`),
  et les portes touchées ont été rejouées sur disque (VERIFICATION.md § G10.0) ; l'outillage qui
  l'évite fait un commit « outils: » à part, avant G10.1.
- **Le risque du recompte** : une porte qui changerait pour une autre raison que LPT et le port jeu
  serait masquée par le recompte ; l'interrupteur la démasque (il doit rendre g93 à l'identique).
- **G10.1** : le banc JOYBANC compte ses tours au calendrier des injections (un état par tour,
  tranche 18 840 + 55 × tour) : si son temps change, un tour peut lire l'état voisin — la porte
  reste juste, les deux côtés recevant les mêmes états, mais la démonstration de PB-103 à l'écran
  se décale. La manette de l'hôte (`Host/SdlJoystick.cs`) n'a pas d'oracle : elle est vérifiée par
  `--joystick-check` sur une manette virtuelle de SDL3, sans matériel réel. Aucun témoin réel (un
  jeu DOS, un test de manette). Pas d'écran de réglage des manettes : la section [Joysticks] du
  .cfg seulement. PB-104 reste reproduit jusqu'au mode matériel de G13.
- **L'outil (GR.4)** : `make bench`, le banc C de l'oracle, ne se lie plus depuis G8 :
  `h_opl_reset` y manque (`harness.c`, `h_boot`), `harness_dbopl.cpp` étant hors de `BENCH_SRC`
  (`tools/oracle/Makefile`). Constaté le 04/10, identique à `08d0d2f`.
- **ATAPI et ZIP** (G10.4, G10.6) : les bancs couvrent une dizaine de commandes sur une trentaine ;
  pas de sonde de l'état ATAPI ; un seul témoin, ATAPIBANC, faute de pilote DOS (décision n° 5).
- **L'audio CD** (G10.5) : le rappel synchrone n'est pas le fil de PCem ; la cadence avec des lectures
  CD sur le fil d'émulation n'est pas mesurée.
