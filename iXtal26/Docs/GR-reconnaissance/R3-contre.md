# GR.3 — La contre-lecture du rapport R3

Le 20 octobre 2026, au commit `d56d161`. Contre-lecture de `R3-clotures.md`, en lecture seule du dépôt : rien de
construit, rien de lancé (ni série, ni émulateur). Les fichiers:lignes cités ont été relus ; chaque compte a été
refait par la commande notée entre parenthèses. Les images de `os/` ont été lues sans lecteur FAT (aucun `mtools`
sur la machine) : la recherche des noms de répertoire FAT (`grep -a -c 'QBASIC  EXE' image`, noms en 8+3 majuscules),
en lecture seule. Elle prouve la présence d'une entrée de répertoire, pas son emplacement.

**Niveaux** : *documenté* (un texte du dépôt ou une source le dit), *déduit*, *inconnu*. **Marques** : C = confirmé,
K = corrigé, A = ajouté. Les décisions D1 à D11 sont jugées au § 4 contre PLAN.md § GR (04/10), PLAN-G9.md, PLAN-G10.md,
PLAN-G13.md et VERIFICATION.md.

## 0. En bref

- **Le rapport tient.** Tous les comptes se refont (18 profils, 12 `.cfg`, 36 contrôles, les trois débits) ; le
  XTIDE de la M24 et du PC1512 est bien sous boot-diff depuis G10.2 ; l'empreinte de `486-HDD-C.img` diverge bien.
- **Quatre corrections (K)** : le compte de `grep -c` de `--setup-check` (38, non 36 : deux définitions) ; les
  profils du 5150 (cinq, non deux) ; deux références de lignes (`NvrImage.cs`, `BootTest.cs`) ; `pcdos20/` partagé
  entre deux images.
- **Une lacune de fond (A)** : G11.3 a posé le précédent d'un `MSD /S` sous boot-diff sur l'ami486, en 54 s
  (`bd-ami486-aha-temoins`). Le rapport traite MSD sous boot-diff comme un pari de coût (§ 1.9, GR.3d) ; c'est un
  précédent, et le coût 0,33 Minstr/s n'est pas le seul.
- **Autres ajouts (A)** : le lanceur a `--gfxcard` ; `ixtal26-286.cfg` écrit sur `os/286-HDD-C.img` ; le témoin
  Yeager de G9.2 n'est pas repris ; l'enregistrement de l'écran SETUP omet toutes les clés de G13.12 et G13.13 ;
  « le comportement surprenant rejoué sous boot-diff » est mal délimité dans GR.3d.
- **Décisions** : sur onze, aucune n'est ouverte à Julien au sens de la règle du 18/10. D10 relève de Q20 (tranchée
  le 16/10) ; les dix autres sont tranchées par une décision écrite ou relèvent de la mise en œuvre.

## 1. L'état, point par point

### 1.1 Les comptes refaits

- 18 profils dans `launchSettings.json` — commande : `grep -c '"commandName"' iXtal26/Properties/launchSettings.json` ;
  résultat : 18 — **C**
- 12 `.cfg` à la racine — commande : `ls *.cfg | wc -l` ; résultat : 12 — **C**
- aucune clé `cdrom`, `lpt1_device`, `joystick` — commande : `grep -n -E 'cdrom|lpt1_device|joystick' ixtal26*.cfg` ;
  résultat : rien (code 1) — **C**
- 36 contrôles de `--setup-check` — commande : `grep -n 'Check(' iXtal26/Host/SdlSetup.cs` ; résultat : 38 lignes, dont
  `SelfCheck(` (1169) et la définition `void Check(` (1174) : **36 appels** (1189 à 1459) — **C** pour 36, **K** pour la
  méthode
- 36 verts dans VERIFICATION — commande : `grep -n '36' VERIFICATION.md` ; résultat : l. 7405, 11862, 12221 — **C**
- 0,33 Minstr/s sur l'ami486 — commande : 183 849 253 / 554 s ; résultat : 0,3319 — **C**
- 1,37 sur l'ami286 — commande : 172 227 813 / 126 s ; résultat : 1,3669 — **C**
- 3,5 sur le XT — commande : 55 822 590 / 16 s ; résultat : 3,489 — **C**
- la plus longue porte : 1 742 s — commande : `awk` sur la colonne 3 de `g13b.tsv` (765 lignes) ; résultat :
  `bd-ami486-zip-banc`, 1 742 — **C**
- `bd-m24-xtide-boot` : 54 001 687 en 12 s — commande : `grep` dans `g13b.tsv` ; résultat : **C**
- 154 fichiers dans `os/` — commande : `find os -type f | wc -l` ; résultat : 154 (153 images et fichiers, plus
  `os.sha256`) — **C**

**Où est `g13b.tsv`** : `/var/tmp/ixtal-g13/series/g13b.tsv`, 765 lignes (`wc -l`). **C**. Le répertoire garde aussi
`g13a.tsv`, `g13b-tout.tsv`, `g12*.tsv` ; les traces sont doublées dans /var/tmp/ixtal-g13.

**K — le compte de `grep -c`.** Le rapport écrit « 36 sites d'appel de `Check` (compté par `grep -c`) ». `grep -c
'Check('` rend 38 ; 36 est le compte des appels une fois les deux définitions retirées. La borne basse est solide :
l'appel de la ligne 1389 (sans `awe32.raw`) s'exécute toujours ; celui de 1425 est dans un `if` dont le corps ne manque
pas en usage normal (déduit, sans lancer l'outil).

**K — les profils du 5150.** Le rapport (§ 1.1) liste « le 5150 (`--model ibmpc`, et son jumeau) » et trouve 15
profils sur 18. Il y en a cinq pour le 5150 : lignes 9 (`ixtal26.cfg --turbo`), 13 (`--model ibmpc`), 17 (jumeau
PCem), 21 (`--ram 64`, « demarrage court »), 25 (`--turbo --verbose`, « diagnostic »). Deux profils XT (29, 33), un
286 (37), un 386 (41), six 486 (45 à 65), M24 (69), PC1512 (73), `--setup` (5) : 5 + 2 + 1 + 1 + 6 + 1 + 1 + 1 = 18.
Les titres du « XT » : « IBM XT 5160 » est le `.cfg` avec la disquette PC-DOS 2.00, « ROM BASIC » est `--model
ibmxt` sans disquette. Conséquence pour GR.3a : le « 5150 + MDA » est un profil de plus à côté de cinq, pas à
côté de deux.

### 1.2 Le XTIDE de la M24 et du PC1512 sous boot-diff — **fait, C**

- `tools/gates/series.sh:360-362` : la boucle `for m in ibmpc:pc ibmxt:xt olivetti_m24:m24 pc1512:pc1512` lance
  `bd-${m#*:}-xtide-boot`. **A** : elle compte quatre portes, pas deux ; `bd-pc-xtide-boot` (9 s) et
  `bd-xt-xtide-boot` (10 s) sont aussi dans `g13b.tsv`. Le rapport ne cite que la M24 et le PC1512, qui sont l'objet du
  point de PLAN.md:517-518.
- `bd-pc1512-xtide-boot` : 62 116 423 instructions, 17 s, « 14 682 octets écrits, identique des deux côtés »
  (`g13b.tsv`). `bd-m24-xtide-boot` : même nombre d'octets écrits, 54 001 687 instructions, 12 s. Ces portes se
  rejouent donc à chaque série.
- Origine : commit `ba76d5a` (04/10, « G10.2 — le XTIDE (XT) : le 5150, le XT, la M24 et le PC1512 amorcent un disque
  dur »), VERIFICATION § G10.2 (l. 6077) ; PLAN-G10.md:136 le dit (« Témoin : la M24 et le PC1512 amorcent enfin un
  disque dur »). **Documenté.** Le point est clos ; il manque à la clôture de GR.3 le seul tableau de GR.5.
- Limite que le rapport ne dit pas (**A**) : la porte boot-diff amorce `c8088dos.img`, fabriqué par la recette g5w à
  partir de `bd-xt-xtide-format` (PLAN-G10.md:288-291). Les outils de `roms/xtide/` ne sont pas cités : confirmé
  (`grep -rIl -i 'biosdrvs\|xtidecfg'` ne trouve que `R3-clotures.md`).

### 1.3 Windows avec `HERCULES.DRV` et `EGA.DRV` : C# seul — **C**

- VERIFICATION § G9.1 (l. 5492 et suivantes) : « Le témoin Windows (C# seul, `--boot`, copie `/tmp` du disque 486 et
  des disquettes d'installation, `KEYB FR` retiré, CMOS copié) : le SETUP DOS de Windows 3.11, affichage « Hercules
  Monochrome » (…) ; puis `WIN` : Program Manager en 720×348 sur l'Hercules (capture). » § G9.2 (l. 5560) : « (C#
  seul, `--boot` sur copies — disque 486, disquettes 1, 2, 3, 5) : (…) Program Manager en 640×350, en mode 386
  étendu (`WIN`) comme en mode standard (`WIN /S`). » **Documenté** : les deux témoins sont en C# seul.
- **K** : le rapport (§ 1.2, tableau) écrit « Windows, disque 486 (§ G9.1) » pour le XT + Hercules et « (§ G9.2) »
  pour l'ami286 + EGA : exact, mais la machine de ces témoins n'est pas dite. G9.1 dit « sur un AT avec
  l'Hercules » (PLAN-G9.md:110), G9.2 « sur un 286/386 » (PLAN-G9.md:118). Aucun de ces textes ne dit que ce fut
  l'ami286 ;
  le rapport, qui propose Windows sur l'ami286 en mode standard (D8), ne peut donc pas s'appuyer sur G9.2 pour affirmer
  que `WIN /S` y tient. **Inconnu** : la machine de l'essai. Le disque est celui du 486.
- **A — Yeager Air Combat.** PLAN-G9.md:118 prévoit « Yeager Air Combat (`os/yeager.img`) s'il tourne en EGA ».
  VERIFICATION
  l. 5562-5564 : « essayé, non concluant — le programme passe en 40 colonnes puis attend ». `os/yeager.img` figure dans
  le
  § 1.8 du rapport, mais comme un fichier de la liste ; ce reliquat de G9.2 n'est ni dans le tableau des témoins ni
  dans l'une des étapes. G12.3 (l. 7120-7124) l'a joué sur la Pro v2 : il pose une question de sécurité tirée du
  manuel que Julien n'a pas fourni. À inscrire dans « ce que ce vert ne dit pas », sans étape.
- **A — `SVGA256.DRV`.** G9.3 (l. 5590) a mesuré que `et4000.bin` ne rend pas les services VESA ; Windows y tourne
  en `VGA.DRV`. Le rapport (§ 1.2) le dit (« `VGA.DRV`, disque 386 »), mais n'en tire pas la limite pour la clôture :
  le témoin Windows de l'ET4000 en 256 couleurs n'existera pas.

### 1.4 Les logiciels des disques de `os/` — **C**, avec une réserve de méthode

Le rapport écrit « relevé par un lecteur FAT en lecture seule ». Il n'a pas dit comment ; sans `mtools` ici, la
contre-lecture a refait le relevé par `LC_ALL=C grep -a -c` des noms 8+3 dans chaque image (lecture seule). Résultat
(nombre de lignes de l'image où le nom apparaît ; 0 = absent) :

| Nom | 286-HDD-C | 386-HDD-C | 486-HDD-C |
|---|---|---|---|
| `QBASIC  EXE` | 1 | 2 | 2 |
| `MODE    COM` | 3 | 3 | 3 |
| `MSHERC  COM` | 1 | 2 | 2 |
| `MSD     EXE` | 1 | 1 | 3 |
| `PRINTER SYS` | 1 | 1 | 1 |
| `VGA     DRV` | 6 | 2 | 1 |
| `S3TRIO  DRV` | 0 | 0 | 8 |
| `HERCULESDRV`, `EGA     DRV` | 0 | 0 | 0 |
| `SBPRO` (répertoire) | 0 | 30 | 40 |
| `MSCDEX  EXE`, `OAKCDROMSYS`, `VIDE-CDDSYS`, `SHSUCDX COM` | 0 | 0 | 0 |

- **C** : QBASIC, MODE, MSHERC, MSD et PRINTER.SYS sont sur les trois disques ; `HERCULES.DRV` et `EGA.DRV` n'y sont
  pas ; `S3TRIO.DRV` seulement sur le 486 ; `\SBPRO` seulement sur le 386 et le 486. Le rapport est exact.
- **Les emplacements** (`\DOS\`, `\WINDOWS\`, `\WINDOWS\SYSTEM`) ne se déduisent pas du `grep` : *déduit* pour les
  chemins, G13.10a les confirme pour MSD (`temoins-10a.sh` tape `DIR C:\WINDOWS\MSD.*` puis `C:\WINDOWS\MSD /S`).
- **Pas de pilote CD DOS** (**C**, étendu) : ni `MSCDEX.EXE` ni `OAKCDROM.SYS` ni `VIDE-CDD.SYS` ni `SHSUCDX.COM` en
  entrée de répertoire sur les trois disques, ni (`grep -a -r -i -E 'MSCDEX|OAKCDROM|VIDE-CDD|SHSUCDX|SBPCD|MTMCDAI|
  GSCDROM' os/`) sur aucune disquette, en dehors de textes : `DOS50/DISK5/README.TXT` et la disquette 1 de Windows
  3.11 parlent de MSCDEX, et **A** : les disques 386 et 486 portent une chaîne « Sound Blaster CD Player … MSCDEX
  not loaded. Program aborted » (le lecteur de CD de l'utilitaire Creative de `\SBPRO`). Ce programme existe donc
  sur les disques du 386 et du 486 et sa sortie, sans MSCDEX, est un refus : un témoin possible de l'absence de
  pilote, sans intérêt pour la vérification. La décision n° 5 de PLAN-G10 (aucun pilote ATAPI DOS) tient.
- **PC-DOS 2.00** (**K**) : le rapport écrit « `os/pcdos20/` : `MODE.COM`, `BASICA.COM`, `PRINT.COM`, `DEBUG.COM` ».
  `pcdos20b.img` (boot) porte `MODE`, `BASICA`, `BASIC`, `PRINT` ; `pcdos20s.img` (supplément) porte `DEBUG`. Les
  deux images sont à passer (la porte `bd-pc-lpt-*-banc` met la seconde en B:, `series.sh:~320`). Le rapport ajoute
  que `BASICA.COM` « a aussi `STICK` et `STRIG` » : aucune des deux images ne contient ces mots (`grep -a -c -E
  'STICK|STRIG'` : 0) ; `BASICA.COM` est un lanceur du BASIC en ROM (`roms/ibmpc/basicc11.f6`…), les instructions
  y sont. **Déduit**, pas vérifié par texte.
- **MS-DOS 5.0, disquettes** : `Dos 5.0/Disk01.img` porte `MODE.CO_`, la disquette 2 `QBASIC.EX_` et `MSHERC.CO_`, la 3
  `EXPAND.EXE`. **C**.

### 1.5 La divergence de `os/486-HDD-C.img` — **C**, avec la date

```
sha256sum os/486-HDD-C.img          5bef743eafa16060960d99df9d822bd0197a9d15985fbbe7cdc4b9e26a14d448
os/os.sha256 (ligne du 486)         7c325071166c8f135677ae4d7c877e489d6565e0f3dae8ef277cb89545e9c7e9
stat os/486-HDD-C.img  (mtime)      2026-10-18 16:18:27 +1100
stat nvr/.ami486.nvr   (mtime)      2026-10-18 16:18:31 +1100
stat os/os.sha256      (mtime)      2026-10-16 19:03:06 +1100  (commit 4e8bde8, 19:03:11)
```

- **C** pour le désaccord. `286-HDD-C.img` (2d42e8ff…), `386-HDD-C.img` (c3d938cc…) et `8088-HDD-C.img` (4c13cc8d…)
  concordent avec `os.sha256`.
- **K — la phrase du § 0.** « `os.sha256` (écrite le 18/10 à 16 h 18, après la réécriture du 16/10) » se lit comme si le
  fichier d'empreintes avait été écrit à 16 h 18. C'est l'image qui l'a été ; `os.sha256` date du 16/10 à 19 h 03.
- **A — la cause**. L'image (16 h 18 min 27 s) et le CMOS de la machine (16 h 18 min 31 s) ont quatre secondes d'écart :
  la fermeture d'une machine lancée par l'utilisateur (le disque se vide, puis `savenvr`). Les portes, les témoins de
  10a et de 13a-13c travaillent sur des copies (VERIFICATION l. 11888-11891). **Déduit**, avec une certitude
  raisonnable : une séance du lanceur sur le profil ami486. Les commits voisins sont à 13 h 53 (`cf6f2f5`) et 18 h 17
  (`56e2401`) ; aucun journal de la série ne touche `os/`.
- **A — ce que G13 a déjà dit sur ce cas.** PLAN-G13.md:819-822, Q20 : « `386-HDD-C.img` (modifiée le 10/10) et
  `486-HDD-C.img` (le 11/10) ne concordent plus (…) Récrire les deux empreintes ? *Proposé : les récrire si ces
  écritures sont les vôtres.* **Tranchée le 16/10 : oui** — récrites (4e8bde8), l'état actuel devient la référence. »
  Même cas, même fichier : voir D10.
- **A — `os/` est ignoré par git** (`git check-ignore -v os/486-HDD-C.img` : `.gitignore:18:os/*`) ; seul `os/os.sha256`
  est suivi (`git ls-files os`). La seule trace de l'image est donc cette ligne : une ligne réécrite sans la preuve
  d'une séance de l'utilisateur ôterait au prochain contrôle sa raison d'être.

### 1.6 Boot-diff refuse le mode matériel — **C**

- Par la clé : `tools/iXtal26.Diff/BootDiff.cs:426-433` — si `pc.loadconfig` refuse et que `hardware_mode` n'est ni 0 ni
  `pcem` ni `aucun`, message « demande le mode matériel ; l'oracle est PCem, refusé », retour 2. **Documenté** :
  PLAN-G13.md décision n° 4 (« la clé dans le `.cfg`, que boot-diff refuse »), VERIFICATION § G13.11 (l. 12214,
  quinze contrôles de `materiel-mode`).
- Par la ligne de commande : `Program.cs:149-165` et `ModeOutils.cs:24-50` — `boot-diff` n'est pas dans la liste
  `Toujours` ni dans les cas conditionnels ; `Accepte` rend faux, raison « la commande compare le C# à l'oracle, qui
  est PCem ». **C**.
- **A — un piège pour GR.3a/b.** L'écran de construction « écrit toujours la clé (0 ou 1) » (VERIFICATION l. 12214) :
  un `.cfg` enregistré par l'écran après un passage en mode matériel porte `hardware_mode = 1`, que boot-diff refuse. Le
  rapport dit « un profil ne doit pas la porter » ; il n'en tire pas que les profils neufs doivent être écrits à la
  main (ou enregistrés en mode PCem), ni que `ixtal26-xt.cfg`, qui n'a pas cette clé (**C**, relu en entier), doit le
  rester.

### 1.7 `--lockstep` — **C**

`Program.cs:44, 63-65, 633-634` : `--lockstep N [--lockstep-from S]` ; `BootDiff.cs:1108-1117` (M19) : « les deux
côtés avancent ENSEMBLE, tranche par tranche, avec la même frappe ». `BootDiff.cs:1206-1210` : la phase 2 ne rejoue
ni `--type` ni `--fdb` (le rapport cite `:1108-1113` et `:1208` : K, c'est la documentation de `LockstepEvery` et
`Phase2`, dont le corps est en 1206-1210). Une divergence d'une campagne tapée se lit donc par `--lockstep`.
**Documenté** : PLAN.md:530-532 (GR.4) et PLAN-G10.md:182 (risque de G10.0).

### 1.8 Les profils, les CMOS, `--make-nvr`

- **C** `iXtal26/Host/NvrImage.cs:229` (`gfxcard`, défaut `cga`), `:292` (`displayBits`). **K** : le message « les
  deux machines à 8088 n'en portent pas » est en `:270`, non `:266-268`.
- **C** : `NvrImage.Make` appelle `config.config_load` (`:223`) et non `pc.loadconfig`, donc un `gfxcard` inconnu
  ne passe pas par le refus de `pc.cs:448` (`setgfxcard`) : le défaut est exact, lu par le texte.
- **C** : `nvr/default/` porte `aha1542c`, `ami286`, `ami386`, `ami386dx_opti495`, `ami486`, `at`, `pc1512`. L'octet
  0x14 (`xxd -s 0x14`) : `ami286` 45h, `ami386dx_opti495` 0Dh, `ami386` 4Dh, `ami486` 4Fh, `at` 41h, `pc1512` 65h ; la
  CMOS de l'utilisateur `nvr/.ami486.nvr` 47h et `.ami386dx_opti495.nvr` 0Fh. Bits 5-4 : 00 pour toutes sauf `pc1512`
  (10, CGA 80 colonnes). Le rapport (§ 1.1) déduit « EGA ou ET4000 partagent sans conflit le CMOS d'un profil VGA »
  de `NvrImage.cs:290-292` : **C**, et vérifié sur les fichiers.
- **A** : `nvr/.ami286.nvr` n'existe pas (le rapport le dit) ; un profil `ami286` lit donc `nvr/default/ami286.nvr`
  en lecture seule (`nvr/README.md:3-5`) tant que la machine n'a pas écrit son CMOS ; le profil `ixtal26-286.cfg` n'est
  pas réglé sur un CMOS fait par `--make-nvr`. Pour un profil Hercules sur l'ami286 (D8), le premier lancement lit un
  CMOS 45h avec bits 5-4 à 00 : le POST peut afficher « CMOS display type mismatch » (**inconnu**, le rapport l'a dit).
- **A — `ixtal26-286.cfg` écrit sur l'original.** Le profil lit `hdc_fn = os/286-HDD-C.img` et `hdd_fn =
  os/286-HDD-D.img` ; `ixtal26-386.cfg` et `-486.cfg` de même sur leurs disques. Les nouveaux profils de GR.3a
  (286 + EGA, 386 + ET4000) qui en copient le disque écrivent donc dans `os/` à chaque séance, comme celui du 486
  le fait depuis le 18/10 (§ 1.5). Le rapport le range sous « les disques de l'utilisateur bougent » (§ 3) et propose
  de passer par une copie pour les portes : juste, mais il n'en tire pas que chaque profil neuf aggrave la dérive de
  `os.sha256`.
- **A** : le lanceur a `--gfxcard` (`Host/CommandLine/Launcher.cs:98`, `MachineOverrides`), et `--hdd-controller`
  (`:83`), `--floppy-a/b` (`:68`), `--zip` (`:~95`). Le rapport dit seulement qu'il n'a ni `--lpt1`, ni `--sndcard`, ni
  option du CD (**C**). Les profils MDA, Hercules et EGA du 5150 et du XT peuvent donc être de simples lignes de
  `launchSettings.json` (`--model ibmpc --gfxcard mda`) ; la convention du dépôt (« les profils `.cfg` et
  `launchSettings.json`», PLAN.md:27) reste le `.cfg`. Rien de bloquant ; à choisir sans question.
- **A — `ixtal26-xt.cfg` et `ixtal26.cfg`** n'ont ni gfxcard ni hdd : `disc_a = os/pcdos20/pcdos20b.img` seule. Le
  « XT + Hercules » est donc, de fait, `ixtal26-xt.cfg` plus `gfxcard = hercules`.

### 1.9 Le coût de boot-diff : le précédent oublié — **A**, le principal manque

- Le rapport (§ 1.9) conclut du compte 0,33 Minstr/s que « Windows en 386 étendu sur l'ami486 (…) dépasserait l'heure »
  et
  que `MSD` sous boot-diff n'est possible « sur l'ami286 seulement si son coût le permet ».
- Or `tools/gates/series.sh:429-431` (G11.3) : `bd-ami486-aha-temoins` amorce DOS 5 du disque SCSI, tape `VER`,
  `MEM`, `CHKDSK C:`, puis `B:MSD /S` (disquette 3 de Windows 3.11 en B:), le tout sous boot-diff :
  31 112 309 instructions en 54 s (`g13b.tsv`), soit 0,58 Minstr/s. **MSD /S sous boot-diff sur l'ami486 existe, depuis
  G11.3** (VERIFICATION l. 6860-6866 : « Les témoins, sous l'oracle »). Le rapport cite G11.3 (§ 1.7) comme la forme
  d'une clôture mais pas ce précédent.
- L'écart avec 0,33 vient de la nature de la porte (les 183 millions du DOS sur le GD5429 sont dominés par les
  écritures disque et la VGA ; 31 millions pour l'AHA). *Déduit* : le coût d'un `MSD /S` seul est de l'ordre de
  quelques dizaines de secondes sur l'ami486. La décision D7 (« l'ami286, quatre fois plus rapide ») est plus
  prudente qu'il ne faut pour MSD ; elle reste juste pour QBASIC et Windows.
- Conséquence sur GR.3d : `MSD /S` sous boot-diff sur l'ami486 (disque IDE, GD5429) est le témoin de la vidéo de
  « Ce qu'un bloc livre » 2 ; il suffit de reprendre la forme de `bd-ami486-aha-temoins` (copie de `win3-3.img`,
  `fatpatch.py` pour `KEYB FR`). **Inconnu** : si l'écran de MSD (« LPT Ports », « Game Adapter ») est identique
  aux deux côtés — le diff d'instructions le dira.

### 1.10 Ce que « Ce qu'un bloc livre » point 2 demandait et que le rapport laisse

PLAN.md:22-35, point 2 : « les profils (…) ; les contrôles de `--setup-check` ; un tableau de témoins (…) ; le
comportement surprenant rejoué sous boot-diff ; la preuve que les originaux sont intacts ».

- **Le comportement surprenant rejoué sous boot-diff** : le rapport en parle dans § 1.7 (« la forme d'une clôture ») et
  dans GR.3d, qui rejoue PB-101 et PB-99 « dans les deux modes » **en C# seul** (`MODE`, DEBUG, `MSD`). Ce n'est pas du
  boot-diff. Ce que G12.3 appelle ainsi (VERIFICATION l. 7132-7135) : des portes qui exercent le défaut des deux côtés
  de l'oracle, sous le mode PCem, chaque série, avec un contrôle négatif. Pour G9 et G10 le candidat est :
  - **PB-99** (les traits de la VGA dans l'EGA) : le contrôle négatif de G9.2 (`^ 0x20` de 3DAh) rougit déjà
    `bd-pc-ega-banc` (VERIFICATION § G9.2, l. 5558) ; **déjà rejoué**.
  - **PB-101** (LPT2 du PC1512, `PCEM_BUGS.md:2018-2042`, cas qui discrimine : « LPTBANC sur le PC1512 ») : le banc
    existe ; `grep 'pc1512' series.sh` ne montre aucune porte `bd-pc1512-lpt` : **à faire** (A). L'ordre de PLAN-G10 le
    permet : LPTBANC saisi dans DEBUG sur le PC1512, sonde du son.
  - **PB-103** (la CH et la TM sans haut-gauche) : `bd-pc-joy-ch-banc` et `-tm-banc`, déjà dans `g13b.tsv`.
  - **PB-104, PB-105, PB-124** : hors du mode matériel (`PCEM_BUGS.md`, champs *Non corrigé*), reproduits des deux
    côtés ; rien à rejouer.
  Le rapport n'a pas fait ce tri : GR.3d dit « PB-101 lu par `MODE` ou DEBUG », et `MODE` de PC-DOS 2.00 ne montre
  pas LPT2 (déduit : le texte de `MODE.COM` n'a été lu ni ici ni là) ; DEBUG (`D 40:8`) le montre.
- **La preuve que les originaux sont intacts** : le rapport la remet à « la clôture (avec GR.5) » et à `os.sha256`
  (§ 2, dernier paragraphe). La forme de G12.3 (VERIFICATION l. 7135-7138) est : `os/os.sha256` (153 images,
  aucune n'a changé), plus `g5w.sha256` (22 empreintes à l'identique, une ligne ajoutée). Le rapport l'a, sauf que
  son § 1.5 établit qu'elle ne passe plus : **A** — une étape GR.3 qui dit « les originaux avant et après » (GR.3a) ne
  peut le dire que contre l'état du 18/10, d'où D10.

### 1.11 Autres vérifications, sans écart

- `launchSettings.json` : les deux jumeaux PCem (lignes 17, 49) demandent `pcem` ; les autres sont sans option, donc
  en mode matériel : **C** (G13.11, VERIFICATION l. 12204-12206, `ModeMateriel.DefautUtilisateur`,
  `ModeMateriel.cs:292`).
- `SdlSetup.cs:47-50` (les lignes de l'écran), `:315-338` (`BuildVideoList`), `:858-907` (l'enregistrement), `:31-37`
  (l'en-tête périmé : « deux cartes y sont transcrites, la CGA et la VGA ») : **C**.
- `pc.cs:275-287` (`setlpt1device`, refus avec la liste) : **C**. `pc.c:965` écrit `lpt1_device` dans PCem : **C**.
- `TimerCheck.cs:411` (`enum Charge { Repos, Ram, X87 }`), `TimerCheckVerb.cs:45-49` (`--charge`), `:455-457` du
  message « n'a pas de coprocesseur » : **C** (la ligne de refus est en `TimerCheck.cs:455-458`).
- `BootTest.cs` : le fichier est `iXtal26/BootTest.cs`, non `Host/` ; `@manette` en `:199-210`, `@shot` `:117-122`,
  `@vram` `:125-139`, `@son` `:141-150`, `@wait` `:212` : **C** (K pour le chemin).
- `roms/xtide/` : `biosdrvs.com` 3 187 octets, `xtidecfg.com` 14 462, `serdrive.exe` 40 448 : **C**.
- `tools/isogen/isogen.py:163-164`, `:292-296` : audio de 1 à 30 secteurs de 588 trames (2 352 octets) ; le plus long
  piste (30 secteurs) dure 0,40 s : **C**.
- `lptbanc.py --com` (`:17`, `:289`), `atapibanc.py --com` (`:31`, `:507`), `--audio` (`:40`, `:43`) : **C**.
- **A — l'écran SETUP omet plus que `lpt1_device`.** Le rapport (§ 1.4, annexe 3) ne relève que `lpt1_device`.
  `grep -E 'serial1|parallel1|monitor_type|io_base|drive0_type|"primary"|"addr"' iXtal26/Host/SdlSetup.cs` ne rend
  rien : les clés de G13.12a (EGA, CGA), G13.12b (Xebec, AT, IDE), G13.13b (`serial1`, `serial2`, `parallel1`,
  `parallel2`) ne sont pas réécrites par l'écran non plus. Même effet déduit que pour `lpt1_device` (la configuration
  chargée reste en mémoire jusqu'à l'enregistrement), mais l'aller-retour Charger → Enregistrer de GR.3c devrait les
  couvrir toutes, pas seulement les cinq clés du CD et `lpt1_device`. **Déduit.**

## 2. Les étapes proposées — ce que la contre-lecture change

L'ordre et la découpe GR.3a à GR.3i sont bons. Les changements :

- **GR.3a** — ajouter : le profil du 5150 + MDA se range à côté de cinq profils de 5150 ; les profils à disque
  (286 + EGA, 386 + ET4000) écrivent sur `os/` (§ 1.8) : les écrire sur des chemins de disque propres au profil
  (comme `ixtal26-486-scsi.cfg` le fait avec son disque créé par `--create-hdd`, VERIFICATION § G11.3) évite de
  doubler la dérive. **Choix de mise en œuvre.**
- **GR.3c** — l'aller-retour Charger → Enregistrer couvre toutes les clés de G10, G12.3, G13.12, G13.13 (§ 1.11), pas
  seulement `lpt1_device`, `joystick_type`, `[Joysticks]` et le CD.
- **GR.3d** — (i) `MSD /S` sous boot-diff sur l'ami486 : un précédent (`bd-ami486-aha-temoins`, § 1.9), à copier ;
  (ii) le comportement surprenant sous boot-diff : une porte `bd-pc1512-lpt` (LPTBANC sur le PC1512 sous boot-diff,
  mode PCem) reprend PB-101 ; PB-99 est déjà rejoué (§ 1.10).
- **GR.3g** — Windows sous boot-diff : le coût est *inconnu* (aucune porte ne tape `WIN`) ; Windows 3.11 en mode
  standard n'est pas établi comme tenant sur l'ami286 (§ 1.3). Une mesure en C# seul (instructions jusqu'à Program
  Manager) précède la porte.
- **Clôture** — les deux limites que GR.5 doit inscrire en plus : `SVGA256.DRV` (ET4000 en 256 couleurs sous Windows)
  et Yeager (G9.2, G12.3).

## 3. Les risques — ajouts

- **Le profil est un fichier de l'utilisateur** : chaque `.cfg` neuf pointe sur un disque de `os/` ; la dérive de
  `os.sha256` n'a que cette cause (§ 1.5, § 1.8).
- **Le mode matériel dans un profil enregistré par l'écran** (§ 1.6) : `hardware_mode = 1` sort la porte `bd-cfg-*`
  en code 2. Un profil neuf ne doit jamais être produit par l'écran sans relire la clé.
- **MSD sous boot-diff** : la divergence se localise par `--lockstep` ; le coût d'un `MSD /S` est celui de G11.3
  (§ 1.9), à mesurer en C# seul d'abord (comme le fait GR.3d pour les profils).

## 4. Les onze décisions D1 à D11, jugées

**Règle de Julien (18/10)** : une question à Julien seulement si ses décisions la laissent ouverte ; les choix de
mise en œuvre se font sans question. Verdict : **aucune des onze n'est une question à Julien.** Pour chacune, ce
qui la tranche :

- D1 jumeaux PCem — ouverte ? : non ; ce qui tranche : PLAN-G13.md décision n° 17 (16/10) et § G13.11 : « les profils et
  le lanceur le posent (le mode matériel) par défaut ; le mode PCem se demande ». Un profil neuf est donc au mode
  matériel sans option ; le jumeau PCem n'est pas exigé (les deux de G13.10a sont des témoins de 10a, VERIFICATION l.
  11846-11852). Choix : aucun jumeau.
- D2 disque du XT + Hercules — ouverte ? : non ; ce qui tranche : PLAN-G10.md:288-291 (« Aucun profil n'a de disque dur
  XTIDE ») est un constat ; PLAN-G10 n° 8 (« jamais sur une image de l'utilisateur ») et Q20 (écrire `os/` : non, sauf
  séance de l'utilisateur) tranchent : disquette seule. Choix de mise en œuvre.
- D3 Windows du 286 + EGA — ouverte ? : non ; ce qui tranche : PLAN-G9.md n° 3 (« limite dite ») et la forme de G9.3
  (VERIFICATION l. 5590-5591 : Windows en `VGA.DRV`, limite dite) : l'en-tête du profil le dit.
- D4 où vont le CD, LPT, manette — ouverte ? : non ; ce qui tranche : PLAN.md:512-514 (« sur les profils 486 ») ;
  PLAN-G10 n° 3 (LPT1 : `none` par défaut), n° 4 (manettes), n° 10 (« c'est le profil ou le `.cfg` qui pose le lecteur
  »). LPT1 à `none` dans le profil de référence : la décision n° 3. Les deux profils d'écoute sont un choix de forme.
- D5 disquette des outils — ouverte ? : non ; ce qui tranche : PLAN-G10 n° 5 (« rien à demander à l'utilisateur »), n°
  20 (isogen et la recette g5w fabriquent, `g5w.sha256` gagne la ligne) : fabriquée par la recette, rien dans `os/`.
- D6 source de l'audio CD — ouverte ? : non ; ce qui tranche : PLAN-G10 n° 5 et n° 20 : isogen fabrique les images de
  test ; une feuille d'écoute est un fichier de plus. L'écoute reste à Julien (PLAN.md:519) mais la demander n'ouvre
  rien.
- D7 machine de QBASIC — ouverte ? : non ; ce qui tranche : Choix technique (§ 1.9). Le précédent
  `bd-ami486-aha-temoins` montre que l'ami486 tient ; on garde l'ami286 pour QBASIC par prudence.
- D8 machine de Windows — ouverte ? : non ; ce qui tranche : Choix technique, à mesurer (§ 2, GR.3g) ; PLAN-G13 n° 12 («
  les images viendront avec GR.0 ») place la porte après GR.0.
- D9 lignes de l'écran — ouverte ? : non ; ce qui tranche : PLAN-G10 n° 21 : « enregistre les cinq clés sans ligne à
  l'écran ; les profils attendent GR.3 » ; PLAN-G10.md, « Les risques », G10.1 : « pas d'écran de réglage des manettes
  ». Les lignes neuves sont G16 (PLAN.md:592-593). Aucune ligne en GR.3.
- D10 `os.sha256` — ouverte ? : non, en l'état ; ce qui tranche : Q20 (PLAN-G13.md:819-822, tranchée le 16/10 : « l'état
  actuel devient la référence »), même fichier, même cause (§ 1.5). Appliquer Q20 : une ligne réécrite dans un commit
  `os:` à part, qui nomme les deux empreintes et la date. La réserve de Q20 (« si ces écritures sont les vôtres ») est
  levée par l'écart de quatre secondes entre l'image et le CMOS (déduit, § 1.5).
- D11 série des étapes de l'hôte — ouverte ? : non ; ce qui tranche : la règle du 08/10 (« série entière : seulement si
  l'émulateur change ») : sous-ensemble ciblé.

**La seule chose qui reste à Julien** n'est pas une question : l'écoute (GR.3i), prévue par PLAN.md:519. Si D10 était
portée comme question, elle serait déjà répondue.

## Annexe — Candidats, hors de la portée

**Défauts de PCem neufs** : aucun trouvé.

**Constats de l'hôte** (non des PB) :
1. `iXtal26/Host/SdlSetup.cs:858-907` : l'enregistrement omet toutes les clés de G13.12/13 (§ 1.11) ; ajouté à celui
   du rapport (`lpt1_device`).
2. `tools/gates/series.sh:360-362` : `bd-pc-xtide-boot` et `bd-xt-xtide-boot` ne sont pas dans le rapport ; sans
   conséquence.
3. Le rapport dépasse 120 caractères sur 41 lignes (`awk 'length>120' R3-clotures.md`).
