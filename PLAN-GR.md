# GR — La reprise de G9 et G10, en profondeur ; le plan, sur reconnaissance

> Écrit le 20 octobre 2026, au commit `d56d161` (G13 close). Bloc GR de `PLAN.md` (décision utilisateur du 04/10 ;
> feu vert le 20/10 : « ok, on va lancer la phase GR du plan »). Reconnaissance en cinq lectures, en lecture seule,
> chacune suivie d'une contre-lecture qui l'a corrigée : `iXtal26/Docs/GR-reconnaissance/` (R0 les pixels, R1 la
> profondeur de G9, R2 celle de G10, R3 les clôtures, R4 la dette et les documents ; `Rn-contre.md`). On retient ici
> les contre-lectures. Chaque constat y cite sa ligne ; ce plan renvoie aux rapports pour le détail.

## Où on en est

GR a été décidé le 04/10, avant G11, G12 et G13. Depuis, G13 a ajouté le mode matériel, le défaut de l'utilisateur
depuis G13.11 ; boot-diff le refuse (il compare à l'oracle, qui est PCem), les bancs en C# seul se jouent dans les deux
modes contre des attendus. G13.9 a corrigé la vidéo en C# seul, « les images viendront avec GR.0 » (PLAN-G13.md,
décision n° 12) ; G13.10a a posé des profils et des témoins, G13.12a les cavaliers de l'EGA et de la CGA, G13.13b les
clés de COM et de LPT.

Le constat des cinq lectures : **aucun point de GR n'est fait, aucun n'est caduc.** Deux sont partiels : la sonde de
l'ET4000 (75 champs de `svga_t`, pas `et4000_t`), et le XTIDE de la M24 et du PC1512 sous boot-diff (fait en G10.2).
Ce que G13 a changé : plusieurs bancs montrent un PB dans un mode et ne vérifient plus l'autre ; les bancs du stockage
n'ont jamais eu leurs attendus du mode matériel (PLAN-G13.md:448-450, non tenu) ; les clés neuves de G13.12 et G13.13
(`font`, `io_base`) n'ont pas de pendant chez PCem.

### Ce que les lectures ont trouvé

- **L'oracle n'a pas d'image hors de la VGA.** `loadfont` et `cgapal_rebuild` y sont des souches vides
  (`harness_stubs.c:466`, `:616`) : l'image de l'oracle est noire pour la CGA, la MDA, l'Hercules, la M24 et le PC1512 ;
  celle de l'EGA est juste (`pallook16/64` ne lisent ni `cgapal` ni `fontdat`). Aucune sonde non SVGA ne hache
  `buffer32` ; la CGA n'a pas de sonde (genre 0, `BootDiff.cs:1015`). Les 113 valeurs de `cgapal_rebuild` du C# sont
  celles de PCem (R0, comparées par script). La VRAM de la CGA est allouée sans effacement (`vid_cga.c:427`) : sans
  effacement au montage, une sonde d'image serait intermittente (R0-contre K3).
- **Les modes graphiques ne sont pas atteints sous l'oracle.** Seul `bd-pc1512-plan` finit en graphique ; EGABANC et
  HERCBANC reviennent au texte ; ni le graphique de la CGA ni le 640 × 400 de la M24 ne passent sous l'oracle.
- **L'ET4000.** Sa ROM (`et4000.bin`, V8.01X) offre le 1024 × 768 (mode 38h) et le HiColor par INT 10h 10F0h-10F2h,
  sans VESA (R1, désassemblage). Sa détection du RAMDAC (10F1h) dépend de PB-100 (c) et de PB-229 : type 4 en mode
  PCem, type 2 en mode matériel (déduit d'une trace, à mesurer d'abord).
- **Les citations `// pcem:` de G9** : 70, dont 17 aux bornes fausses, et une plage fausse dans le DEVIATION
  d'`ega_write` (R1 § 1.3).
- **Le stockage.** `scsi_cd.c` a 30 commandes : 16 atteintes sous l'oracle, 8 en C# seul, 6 jamais (1Bh, 1Eh, 2Bh, 44h,
  ADh, BBh). `scsi_zip.c` en a 25 : 19, 1, 5 (FORMAT, SEEK(6), IOMEGA EJECT, PREVENT, WRITE AND VERIFY). G11 n'ouvre
  aucun autre chemin (sous l'AHA, les canaux du CD et du ZIP sont ramenés à -1). Aucune sonde ne voit l'état ATAPI, CD,
  ZIP, DSS ou Covox. `cdimage-check` joue 34 cas fixes. ZIPBANC est la plus longue porte (1 742 s) ; le temps d'un banc
  suit le nombre de lignes tapées (7 à 8 s par ligne), et 96 des 190 lignes d'octets de ZIPBANC sont des suites
  arithmétiques qu'une boucle du `.COM` remplacerait (environ 800 s par série, R2-contre A4).
- **Les clôtures.** Aucun des quatre profils de G9 n'existe ; aucun profil 486 ne porte le CD, LPT ou la manette.
  `--setup-check` (36 contrôles) ne touche ni aux cartes de G9 ni aux clés de G10 ; l'enregistrement de l'écran omet
  les clés de G13.12 et G13.13. Windows avec `HERCULES.DRV` et `EGA.DRV` n'a tourné qu'en C# seul. Les disques 286, 386
  et 486 portent QBASIC, MODE, MSHERC et MSD ; aucun pilote de CD DOS, aucune image de CD. Le précédent
  `bd-ami486-aha-temoins` (G11.3) joue `MSD /S` sous boot-diff en 54 s.
- **La dette.**
  - La M24 et le PC1512 ignorent `gfxcard` sans le dire, et l'écran SETUP leur offre les dix cartes. `builtin`, la
    valeur que PCem écrit pour elles, est refusée par `setgfxcard` : un `.cfg` de PCem pour la M24 ne se charge pas
    (R4-contre, documenté).
  - Les manifestes et les notices :
    - `roms/roms.sha256` couvre 13 des 34 ROM chargées ;
    - `VENDORED.md` ne décrit que le 5150 et l'XT ;
    - `THIRD_PARTY_NOTICES.md` dit « No » pour DOSBox (`dbopl.cpp`, `cdrom_image.cpp`) et omet `nukedopl.cpp`.
  - Les registres :
    - TRANSCRIPTION.md oublie les quatre cartes de G9 et dit `mouse_ps2.c` non transcrit ;
    - PCEM_BUGS.md : six entrées sans *Effet* (PB-24, 64, 66, 97, 99, 100), « deux cent cinquante-sept » pour 294,
      la table « Trouvé par » arrêtée à PB-262.
  - L'outil :
    - `check-oracle` signale une dérive sur `harness.c` et `harness.h` depuis `cb69404`, jamais réancrée ;
    - le vidage `--boot` de l'EGA lit à travers la carte (`ega_read` : des cycles, les verrous), et toujours en B800 ;
    - la phase 2 de boot-diff ne rejoue ni la frappe, ni la souris, ni la manette, et repart d'images déjà écrites
      (C: du C# donnée à l'oracle). Elle n'est atteinte qu'après une divergence : grave pour le diagnostic, jamais pour
      une porte verte ;
    - `make bench` ne se lie plus (17 symboles des objets C++, déduit).
  - Le statut des plans : PLAN-G8 et PLAN-G9 n'ont pas d'en-tête « est fait », et PLAN-G9 n'a pas de « Les risques ».
    PLAN.md dit encore « G13 en clôture ».

## Le principe

GR ne corrige aucun défaut de PCem : il vérifie. Le mode PCem reste identique à l'unité à chaque étape. Ce qui change
l'émulateur, ce sont des méthodes de sonde (froides) et des corrections d'hôte (configuration, écran). Une
transcription fausse que les pixels révéleraient est corrigée dans l'étape qui la trouve, avec son explication (R0,
risque n° 1). Un défaut de PCem neuf est inscrit en annexe de l'étape, sans correction (règle du 18/10). Le mode
matériel n'a pas d'oracle : il est vérifié par ses cas en C# seul et par les bancs en C# seul dans les deux modes.

## Les étapes

L'ordre suit les dépendances. L'image de l'oracle vient d'abord (GR.0), parce que GR.1 en dépend. Les outils de
diagnostic (la phase 2, le vidage de l'EGA) précèdent les bancs qui réécrivent leurs attendus. La configuration de
l'hôte précède les profils, et les profils précèdent les témoins. La couverture et la relecture se font sur des bancs
finis, puis viennent les registres et la clôture. Chaque étape est commitée et poussée dès sa série verte, avec le
préfixe `gr:`. Une étape qui change l'émulateur joue la série entière dans les deux modes ; les autres jouent un
sous-ensemble ciblé (moins de 15 min).

### GR.0a — L'oracle reçoit les polices et la palette  ✅ *fait, VERIFICATION.md § GR.0a*

- **Le contenu** :
  - dans `harness_stubs.c`, `loadfont` recopié verbatim de `video.c:930-1047` et `cgapal_rebuild` de
    `video.c:1162-1298`, avec `fontdatw`, `fontdat8x12` et `fontdat12x18` définis. Les formats PC200, T3100e et
    SIGMA400 compilent sans être atteints (R0-contre K1) ; le composite reste vide, et le commentaire le dit ;
  - un export de l'ABI, de 57 à 58 ;
  - la porte hors machine `video-tables-check` : pour chaque romset, `fontdat` et `fontdatm` comparés octet par octet,
    puis `cgapal` pour les `display_type` et les deux contrastes. Elle est déclarée `pcem_seul` à `ModeOutils` et à
    `series.sh` ;
  - `oracle.tsv` réancré pour `harness.c` et `harness.h`, les deux seules dérives vues d'abord.
- **Les preuves** :
  - la porte est verte, et la même porte contre l'oracle de `d56d161` est rouge ;
  - les comptes d'instructions des boot-diffs non SVGA ne bougent pas : la police et la palette ne touchent aucun état
    du processeur.
- **Les contrôles négatifs** : la police fine et la police épaisse échangées ; une valeur de l'ambre faussée.
- **La série** : un sous-ensemble ciblé (`abi`, la porte neuve, les 74 boot-diffs non SVGA, environ 1 280 s).

### GR.0b — Les sondes hachent l'image ; la sonde de la CGA ; les champs de l'ET4000  ✅ *fait, VERIFICATION.md § GR.0b*

- **Le contenu** :
  - la MDA, l'Hercules, l'EGA, la M24 et le PC1512 remplissent `#buffer32`, avec les mêmes octets que la VGA
    (`harness.c:2264`). L'EGA remplit aussi `#pallook`, dont le hachage change quand 3C2h bascule entre `pallook16` et
    `pallook64` ;
  - `#mdacols` et `#cgapal` sont ajoutés, pour qu'une palette fausse se voie même sur un attribut absent de l'écran ;
  - la CGA reçoit une sonde de 22 champs (genre 7). Sa VRAM est effacée au montage côté oracle, une déviation de
    l'oracle par analogie avec la décision n° 4 de PLAN-G9 ;
  - l'ET4000 reçoit `banking`, `ramdac.state` et `ramdac.ctrl` (R1, GR.1a, regroupé ici : la même ABI, la même série).
    La décision n° 3 dit pourquoi. `vid_et4000.c` sort de la liste liée du Makefile pour être inclus dans un harnais ;
  - `VgaProbe` nomme les champs par genre et compte les champs remplis. La SVGA garde « 163 champs » (PLAN.md:480) ;
  - boot-diff refuse `font` = 1, `io_base` = 200h et `display_type = composite` (code 2), qui n'ont pas de pendant
    chez PCem (décision n° 4).
- **Les preuves** :
  - tous les boot-diffs non SVGA sont verts, images comprises, aux mêmes comptes ;
  - une fois, hors porte, un écran par carte, comparé entre le `--boot` du C# et l'image de l'oracle.
- **Les contrôles négatifs** : une panne par carte, côté C#, que la sonde rougit sur l'image seule (le tableau de R0
  § 2) :

  | Carte | Panne | Porte |
  |---|---|---|
  | CGA | la rangée de la police | `bd-pcdos-cga` |
  | MDA | la palette de l'attribut 7 | `bd-pcdos-mda` |
  | Hercules | la palette de l'attribut 7 | `bd-pcdos-hercules` |
  | EGA | `pallook64` | `bd-pcdos-ega` |
  | M24 | l'index de `fontdatm` | `bd-m24-dir` |
  | PC1512 | les plans 1 et 2 échangés | `bd-pc1512-plan` |
  | ET4000 | `ramdac.ctrl` faussé après la dernière lecture de l'invité | ET4BANC |

  S'y ajoutent `loadfont` rendu à la souche dans l'oracle, qui fait rougir tous les textes, et le bit 7 de 3C2h
  inversé. Une panne que l'invité relit rougit le diff avant la sonde et ne prouve rien (R1-contre K6).
- **La série** : entière, dans les deux modes (des méthodes de sonde dans l'émulateur). Attendus contre g13b : les
  messages des portes vidéo, `abi`, aucun compte d'instructions ; M2 limité aux méthodes de sonde.
- **Le risque** : le premier vert n'est pas acquis. Plusieurs chemins de points n'ont jamais été comparés hors de la
  VGA : la police lue à plat, PB-96, le `charbuffer` de 512 de PB-09, la neuvième colonne de la MDA. Un rouge sur le
  seul champ `revision|composite|snow_enabled` de la CGA, avec `crtc[1] > 128`, relève de PB-09.

### GR.4b — La phase 2 de boot-diff rejoue l'exécution  ✅ *fait, VERIFICATION.md § GR.4b*

- **Le contenu** :
  - la phase 2 reçoit le calendrier de frappe, les injections (souris, manette, `--fdb`) et les fins de tranche de la
    phase 1 ;
  - elle repart de copies fraîches de A:, de B:, des disques et du ZIP, une par côté ;
  - si le budget d'une tranche s'écarte d'un côté à l'autre avant l'index, elle le dit et renvoie à `--lockstep` ;
  - son message final ne parle du hachage de trace que si elle a rejoué la même exécution.
- **Les preuves** :
  - le contrôle négatif de la DSS de G10.0, rejoué : la phase 2 nomme la lecture de 379h (VERIFICATION.md:5654-5660) ;
  - une divergence injectée pendant une frappe, nommée ;
  - les images source inchangées (`sha256sum`).
- **Les contrôles négatifs** : sans le rejeu des touches, retour à « les états concordent » ; avec A: prise à la
  source, son empreinte change.
- **La série** : outillage seul. Les portes vertes ne passent jamais par la phase 2 : un sous-ensemble ciblé.

### GR.4c — Le vidage `--boot` de l'EGA, sans passer par la carte  ✅ *fait, VERIFICATION.md § GR.4c*

- **Le contenu** : `DumpEgaTextScreen` lit la VRAM brute, plan 0. Le segment suit le moniteur (B000 ou B800, d'après
  `0040:0063`), et les colonnes, les lignes et la page viennent de la BDA. La MDA et l'Hercules ne coûtent rien à la
  lecture ; la CGA garde son chemin, et « Ce que ce vert ne dit pas » le note (décision n° 5).
- **Les preuves** :
  - le témoin de G13.12a (l'EGA en mono) se lit sans `@shot` ;
  - une EGA en 43 lignes ;
  - `banc-egabanc` et `-materiel` : chaque octet de `egabanc.attendus` qui change est expliqué. Les lectures de 3DAh
    peuvent se déplacer, puisque le vidage ne coûte plus de cycles.
- **Les contrôles négatifs** :
  - le segment forcé à B800 : le témoin mono rend un écran vide ;
  - une lecture par la carte : `egareads` bouge.
- **La série** : le sous-ensemble des `banc-*` de la vidéo.

### GR.4a — La M24 et le PC1512 : la vidéo intégrée  ✅ *fait, VERIFICATION.md § GR.4a*

- **Le contenu** :
  - `builtin` est accepté sans mot sur la M24 et le PC1512, et refusé ailleurs. Une autre carte est ignorée sur ces
    deux machines, avec un avertissement (`check_gfxcard`, comme `check_sndcard`) ;
  - `gfxcard` interne reste à la CGA, pour que le mode PCem et l'oracle ne bougent pas (décision n° 6) ;
  - l'écran SETUP affiche « Built-in video », non sélectionnable, et enregistre `builtin` ;
  - `NvrImage` refuse un `gfxcard` inconnu.
- **Les preuves** :
  - `r9-gfxfixe`, en C# seul : sans clé, `builtin`, `vga`, `ega` et un nom inconnu, sur les deux machines ;
  - `--setup-check` étendu ;
  - le témoin `--boot` de la M24 avec `gfxcard = vga`.
- **Les contrôles négatifs** : l'avertissement retiré ; le filtre de l'écran retiré ; `builtin` refusé.
- **La série** : `pc.cs` change, la série entière ; attendu : identique à l'unité, plus une porte.

### GR.0c — Les bancs vidéo qui finissent dans le mode testé (avec GR.1e)  ✅ *fait, VERIFICATION.md § GR.0c*

GR.0c et GR.1e touchent aux mêmes générateurs : ils font une seule étape, après la sonde de l'image (R0-contre A2).

- **Le contenu** :
  - EGABANC gagne AND et OR (GR3 = 08h et 10h, chacun sur un verrou connu), imprime ses relevés, attend une touche et
    finit en 10h ;
  - HERCBANC finit sur la page 1 du graphique ;
  - un banc de la CGA, CGABANC (modes 4, 5, 6 et 1, la palette de 3D9h, l'intensité, la bordure), qui finit en
    graphique ;
  - le 640 × 400 de la M24 : le mode du BIOS qui le pose est à mesurer d'abord.
- **Les preuves** :
  - les portes recomptées ou neuves sont vertes, image comprise ;
  - les attendus du C# seul sont réécrits, chaque octet expliqué ;
  - `@vram`, et une empreinte de ce que `@shot` écrit (`@image`), dans les deux modes.
- **Les contrôles négatifs** : AND et OR échangés dans `ega_logic` (le diff) ; les pannes graphiques du tableau de
  GR.0b (la sonde seule).
- **La série** : outillage seul, un sous-ensemble (les portes touchées).

### GR.1 — La profondeur de G9

- **GR.1b — L'ET4000 sous DOS sur l'ami486, et sa ROM sur le 8088 en oracle seul** ✅ *fait, § GR.1b* :
  - `bd-ami486-et4000-dos` ;
  - la mesure de la ROM sur le 8088 dite dans VERIFICATION, sans porte si elle ne tourne pas ;
  - le contrôle négatif : le bit 2 de SR7 non forcé.
- **GR.1c — ET4BANC jusqu'au 1024 × 768** ✅ *fait, § GR.1c* (la table de C000:7200 ; un second jeu de CRTC,
  choisi par le bit 5 de `40:0089h`, est à relever) :
  - les relevés en texte, une touche, puis le mode 38h, les douze banques de 64 Ko remplies, CR34, CR35 et 3CDh relus,
    et la sortie dans ce mode ;
  - `bd-ami386dx-et4-banc` est recompté, et les deux attendus du C# seul sont réécrits ;
  - le contrôle négatif : `rowoffset` de 38h faussé.
- **GR.1d — Le banc HiColor, ET4HBANC** ✅ *fait, § GR.1d ; le second contrôle négatif, resté vert, y est remplacé* :
  - la détection du RAMDAC (10F1h) est mesurée d'abord, dans les deux modes ;
  - puis le FFh armé, la commande et le masque relus (PB-100 (c)), les codes 32 bits du décodeur (40h, C5h), 60h,
    10F0h, et la sortie en HiColor. 10F0h refuse le 1024 × 768 (BL = 38h) : pas de HiColor dans ce mode ;
  - trois portes ;
  - les contrôles négatifs : le FFh armé pris pour une commande ; 40h décodé en 24 bits.
- **GR.1f — Les clés sous boot-diff** ✅ *fait, § GR.1f* :
  - l'EGA de 64 Ko et l'EGA sur moniteur mono (3Bx sous le diff), sur le 5150 et l'AT ; la MDA et l'Hercules en ambre ;
    EGABANC sur 64 Ko ;
  - `r9-videocfg` étendu à `memory` et à `display_type` hors liste ;
  - les contrôles négatifs : le `^ 0x60` retiré ; l'ambre faussé ; `memory` lu à 128 Ko.
- **GR.1g — MDABANC** ✅ *fait, § GR.1g* :
  - R0 à R17 écrits et relus, le curseur par INT 10h puis R10 et R11 relus (le Turbo XT de PB-97), R4 = 99h, 3BAh
    échantillonné (PB-102), 3B8h, un écran d'attributs, puis l'état final avec R9 = 13h (l'entrelacé, PB-96) ;
  - `bd-pc-mda-banc`, plus le banc en C# seul dans les deux modes, avec l'empreinte de l'image ;
  - les contrôles négatifs : le Turbo XT retiré ; `& 15` dans l'entrelacé ; `fontdatm` lu `sc & 15`.

### GR.2 — La profondeur de G10

- **GR.2a — Les sondes de l'ATAPI, du CD, du ZIP, de la DSS et du Covox** :
  - le pont ATAPI se sonde déjà par `harness_ide.c`. `scsi_cd_data_t`, `scsi_zip_data` et les fichiers LPT demandent
    des harnais inclusifs, sur le patron de `harness_aha.c` ;
  - les pendants en C# ; l'ABI 59 ;
  - un contrôle négatif par sonde : une faute que le diff ne voit pas ;
  - la série entière, dans les deux modes.
- **GR.2b — ATAPIBANC tiré d'une table** :
  - les 30 commandes et leurs cas limites, sans les sites R9 ;
  - DATA.BIN et README.TXT lus de bout en bout contre les sommes d'isogen ;
  - sous boot-diff : sur l'ami486, avec le lecteur chargé et avec le lecteur vide ; sur l'ami286 et l'ami386dx, avec le
    lecteur chargé (`ami486-atapi.cfg.in` recopié) ;
  - en C# seul, dans les deux modes, contre des attendus. Ceux du mode PCem viennent de l'oracle ; ceux du mode
    matériel sont sourcés à SFF-8020i et MMC-2. C'est la dette de G13.7 (PLAN-G13.md:448-450) ;
  - la frappe est compactée par des boucles dans le `.COM`.
- **GR.2c — L'audio du CD** : ATAPIAUD sur la même table (SEEK, PLAY AUDIO sur deux pistes, lecture de données pendant
  la lecture audio, READ SUB-CHANNEL, START STOP et l'éjection, PREVENT), sous boot-diff et en C# seul dans les deux
  modes.
- **GR.2d — ZIPBANC complet, et l'esclave** :
  - les cinq commandes manquantes ;
  - une image en lecture seule ;
  - le ZIP en esclave du CD ;
  - FORMAT dans une porte à part (100 Mo par côté, sous `ESPACE_MIN_GO`) ;
  - les suites arithmétiques de ZIPBANC passées en boucles d'abord (environ 800 s de moins par série) ;
  - les attendus des deux modes en C# seul ; AHABANC de même.
- **GR.2e — La campagne de CUE** :
  - un générateur déterministe dans `isogen.py`, avec des feuilles valides combinées et des feuilles malformées par
    mutation, que `cdimage-check` joue des deux côtés ;
  - le FILE qui désigne un répertoire inscrit en DEVIATION (PLAN-G13, décision n° 9) ;
  - le contrôle négatif : une extraction d'entier qui ne sature plus.

### GR.3 — Les deux clôtures

- **GR.3a — Les quatre profils de G9** :
  - 5150 + MDA, XT + Hercules (disquette seule), 286 + EGA, 386DX + ET4000AX, en mode matériel (décision n° 17 de
    G13) ;
  - les profils à disque écrivent sur des images propres au profil, pas sur `os/` ;
  - les en-têtes disent les limites de Windows ;
  - `bd-cfg-5150-mda` et `bd-cfg-xt-hercules` ; les profils à disque en C# seul, dans les deux modes.
- **GR.3b — Le CD, LPT et la manette dans les profils 486** :
  - le lecteur au canal 2, vide, et la manette, dans `ixtal26-486.cfg` et `-486-s3.cfg` ;
  - LPT1 reste à `none` (PLAN-G10, décision n° 3) ;
  - deux profils courts pour l'écoute, la DSS et le Covox ;
  - `bd-cfg-486-cd`.
- **GR.3c — `--setup-check` et `--make-nvr`** :
  - chaque carte de G9 et la CGA proposée, et posée sur un XT et sur un AT ;
  - un aller-retour Charger → Enregistrer de toutes les clés de G10, G12.3, G13.12 et G13.13. L'enregistrement de
    l'écran est corrigé pour elles ;
  - `nvr-check` : l'octet 14h attendu par carte ;
  - `bd-ibmat-mda-cmos` et `bd-ami286-hercules-cmos` ;
  - le message de `--make-nvr` corrigé.
- **GR.3d — Les témoins DOS** :
  - `VER`, `MODE`, `MSD /S` (la vidéo, « LPT Ports », « Game Adapter ») et `ECHO >PRN` avec `none`, `dss` et
    `lpt_dac`, dans le bac à sable de G13.10a, dans les deux modes ;
  - `MSD /S` sous boot-diff sur l'ami486, sur le patron de G11.3 ;
  - le comportement surprenant sous boot-diff : `bd-pc1512-lpt` (PB-101) ; PB-99 l'est déjà (`bd-pc-ega-banc`).
- **GR.3e — QBASIC `STICK` et `STRIG` sous boot-diff** : sur l'ami286, avec des états injectés par `--joy-at`, puis en
  C# seul dans les deux modes.
- **GR.3f — Les outils XTIDE** : une disquette fabriquée par la recette, avec `BIOSDRVS` sur le XT, la M24 et le
  PC1512, et `XTIDECFG` ouvert puis quitté sur le XT ; l'image C: inchangée.
- **GR.3g — Windows sous boot-diff, avec `EGA.DRV` et `HERCULES.DRV`** :
  - les disques préparés par la recette depuis `c286.img` et la disquette 2 ;
  - le coût mesuré d'abord en C# seul, jusqu'au Gestionnaire de programmes ;
  - `bd-ami286-win-ega` et `bd-ami286-win-herc`, en mode standard, pixels compris.
- **GR.3h — La marge de `--timer-check` sous lectures CD** : `--charge cd` (PLAY AUDIO, puis des READ(10)), mesurée dans
  les deux modes sur l'ami486.
- **GR.3i — L'écoute, par Julien** : une disquette d'écoute (une gamme en DSS, en Covox mono et stéréo, et ATAPIAUD) et
  une feuille CUE d'écoute d'isogen ; sa réponse est inscrite telle quelle.

### GR.1h, GR.2f — La couverture comptée ; GR.1i — la relecture contradictoire

- La méthode de M15 s'applique, sur une copie jetable : un compteur par méthode, par `case` et par branche, dans les
  deux modes. Pour la vidéo, les cinq fichiers de G9 et leurs `.Materiel.cs` (décision n° 7). Pour le stockage,
  `ide_atapi`, `scsi`, `scsi_cd`, `scsi_zip`, le moteur d'images et `lpt*`.
- Les tableaux : ce qui est exercé, ce qui n'est jamais atteint, et pourquoi.
- La relecture contradictoire porte sur cinq tranches de la vidéo et sur le stockage, chaque tranche lue puis réfutée.
  Les 21 bornes fausses de G9 sont corrigées : 17 `// pcem:`, la plage du DEVIATION d'`ega_write` et trois renvois
  en ligne de `vid_ega.cs` (R1-contre K4). Cette relecture ne dépend d'aucune porte et peut avancer plus tôt.
- La série : des commentaires seuls, donc un sous-ensemble.

### GR.4d, GR.4e — `make bench`, les registres, les licences, les commentaires

- **GR.4d** : `make bench` se lie de nouveau (les objets C++, `--wrap=_Znam`), et la `.so` ne change pas.
- **GR.4e** :
  - les manifestes et les notices : `roms/roms.sha256` (34 lignes), `VENDORED.md`, `THIRD_PARTY_NOTICES.md` (DOSBox
    « Yes », `nukedopl.cpp`, l'Ayumi, les quatre corpus) ;
  - TRANSCRIPTION.md ;
  - PCEM_BUGS.md : le champ *Effet* des six entrées, les totaux, la table « Trouvé par », et le compte des lignes retiré
    ou refait avec sa méthode ;
  - les commentaires et en-têtes périmés de R4 § 1.5 et R4-contre A2 ; `UsageText` et le README.
  - Les preuves : `sha256sum -c` ; `check-oracle` sans dérive ; `recensement` ; la construction sans avertissement.

### GR.5 — Les documents, et la clôture de GR

- PLAN-G8, G9 et G10 : l'en-tête « est fait », et « Les risques » de PLAN-G9.
- PLAN-G13 : l'en-tête « G13 est fait ».
- PLAN.md : « Où on en est », et la ligne 320 sur l'ET4000 (Windows tourne en VGA).
- VERIFICATION.md § GR :
  - les portes vidéo (carte, machine, mode, champs de sonde, panne et champ rougi) ;
  - la couverture ;
  - les constats de la relecture et leur sort ;
  - les témoins ;
  - la dette, point par point ;
  - « Ce que ce vert ne dit pas » : le mode matériel sans oracle ; l'image de fin, hachage de 16 Mo et non « l'écran » ;
    le vidage de la CGA par la carte ; `SVGA256.DRV` et l'ET4000 sans VESA ; Yeager ; aucun pilote de CD DOS ; aucune
    manette réelle ; l'écoute subjective.
- `os.sha256` (Q35) et `g5w.sha256` : la preuve que les originaux sont intacts.

## La vérification

- Le diff d'instructions, à l'unité, pour tout ce qui passe sous boot-diff, en mode PCem.
- Les sondes de fin de porte :
  - l'image de chaque carte non SVGA (GR.0b) ;
  - l'ET4000 (GR.0b) ;
  - l'ATAPI, le CD, le ZIP, la DSS et le Covox (GR.2a).
  Chacune a sa panne injectée qui la rougit.
- Les bancs en C# seul, dans les deux modes, contre des attendus ; chaque octet propre au mode matériel est expliqué
  par son PB.
- La couverture comptée, et la relecture contradictoire.
- Les témoins tirés de vrais logiciels, et la preuve que les originaux sont intacts.

## Les décisions

Le principe de GR, sa portée et sa porte sont décidés (PLAN.md § GR, 04/10). Les décisions ci-dessous découlent des
décisions déjà prises ; la règle du 18/10 n'en laisse qu'une question à Julien, Q35.

1. **L'ordre** : celui des étapes ci-dessus. GR.0 vient d'abord, parce que GR.1f et GR.1g n'ont de sens qu'avec
   l'image. Les outils de diagnostic précèdent les bancs, et la configuration précède les profils. *(De PLAN.md § GR
   et de R0-contre A2.)*
2. **Les images en mode matériel** : aucune comparaison, faute d'oracle. Une empreinte de l'image vient en plus dans les
   attendus des bancs vidéo en C# seul. *(PLAN-G13, décision n° 12, et G13.9g.)*
3. **La sonde de l'ET4000 dans GR.0b** : la même ABI et la même série que les sondes de l'image, au lieu d'une étape de
   plus. *(Choix de mise en œuvre.)*
4. **Les clés sans pendant chez PCem** (`font`, `io_base`, le composite) : boot-diff les refuse, comme le mode
   matériel. *(PLAN-G13, décisions n° 14 et 17 : elles sont au défaut de PCem.)*
5. **Le vidage brut se limite à l'EGA**, la seule que GR.4 nomme ; la MDA et l'Hercules ne coûtent rien à la lecture ;
   la CGA est dite dans les limites. *(Règle du 18/10.)*
6. **`builtin`** : accepté sur la M24 et le PC1512, refusé ailleurs. Une autre carte y est ignorée, avec un
   avertissement. `gfxcard` interne reste à la CGA. *(Règle n° 5 de « Ce qu'un bloc livre », PLAN.md:38-39 :
   `builtin` est la valeur juste de ces deux machines ; ailleurs, PCem lirait `video_cards[-1]`.)*
7. **La couverture compte le mode matériel** : les `.Materiel.cs` et les deux modes, un tableau par mode, comptés
   par les portes `banc-*-materiel` et `materiel-cas` (boot-diff refuse ce mode). *(« Ce qu'un bloc livre », n° 4 :
   « dans le mode testé » ; PLAN-G13, décision n° 17 : c'est le mode de l'utilisateur.)*
8. **Les bancs du stockage reçoivent leurs attendus du mode matériel** dans GR.2 : la dette de G13.7. *(PLAN-G13.md:165
   et :448-450.)*
9. **Les profils neufs sont en mode matériel**, sans jumeau PCem : le mode PCem de ces machines est couvert par les
   portes. Le XT + Hercules est à disquette seule. Les profils à disque ne touchent pas `os/`. *(PLAN-G13, n° 17 ;
   PLAN-G10, n° 8 ; Q20.)*
10. **Aucune ligne d'écran neuve** pour LPT, la manette et le CD : des contrôles seulement, et les lignes neuves en
    G16. *(PLAN-G10, n° 21 ; PLAN.md, G16.)*
11. **Les disquettes de bancs, d'outils et d'écoute** sont fabriquées par la recette g5w ou par un script, et rien n'est
    ajouté à `os/`. *(PLAN-G10, n° 5 et n° 20.)*
12. **Le commit** : le préfixe `gr:` pour chaque étape, `os:` pour `os.sha256`, `plan:` pour ce fichier.

**La question à Julien** :

- **Q35 (GR.5)** — `os/486-HDD-C.img` a été écrite le 18/10 à 16 h 18. Elle ne concorde plus avec `os/os.sha256`, récrit
  le 16/10 sur Q20. Aucune porte ni aucun script de G13.13 ne l'a touchée : les portes travaillent sur des copies, et
  les scripts de 13a ne lançaient que la M24 et le PC1512. Est-ce ta séance ? *Proposé : si oui, récrire l'empreinte,
  comme pour Q20, dans un commit `os:` à part.* Sans réponse, rien ne bloque avant GR.5.

## Les risques

- **Le premier vert des pixels** (GR.0b) : un rouge est une transcription à corriger dans l'étape, en mode PCem, avec
  son explication. Ce n'est pas un défaut de PCem.
- **La durée de la série** : les bancs neufs ajoutent du temps (une table ATAPI sur trois machines, ZIPBANC complet,
  MDABANC, CGABANC, ET4HBANC, Windows). Les boucles dans les `.COM` en compensent une part. On mesure à chaque étape.
- **Les attendus réécrits** (GR.4c, GR.0c, GR.1c) : chaque octet changé est nommé. Un écart non expliqué est un rouge.
- **La détection du RAMDAC** (GR.1d) n'est que déduite. Le banc HiColor suit le chemin mesuré. Aucun des deux types
  n'est celui d'une Sierra réelle, qui reste inconnue (PLAN-G13, décision n° 14).
- **Les sites R9 du stockage** (PB-113 à 115, 125, 127) arrêtent l'oracle : la table d'ATAPIBANC les exclut.
- **FORMAT du ZIP** écrit 100 Mo par côté sous le quota de `/tmp` (l'incident du 4 octobre) : il passe dans une porte à
  part, avec la garde d'espace.
- **Les harnais inclusifs** (GR.2a) : l'oracle reste sans dérive, et la série identique (le précédent de G11.0).
- **La phase 2 rejouée** suppose que le budget compté par le pas égale celui de `h_runpc`. Sinon, elle renvoie à
  `--lockstep` au lieu de se tromper.
- **La licence de DOSBox** (« GPL v2 or later » en amont, GPL-2.0-only chez nous) : compatible, par déduction ; le texte
  public est à relire avant G17.
