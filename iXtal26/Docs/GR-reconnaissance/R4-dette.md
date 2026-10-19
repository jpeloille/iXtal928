# GR.4 et GR.5 — La dette partagée et les documents : l'état au 20 octobre 2026

Lecture de reconnaissance, au commit `d56d161` (G13 close). Lecture seule : rien n'a été construit ni lancé ; les
empreintes et les comptes sont tirés de `sha256sum`, `grep`, `awk` et `nm` sur les objets déjà présents. Niveaux :
**vérifié** (relu à la ligne, ou recompté), **déduit**, **inconnu**.

## 0. En bref

1. M24 et PC1512 : rien n'a bougé depuis G1. `gfxcard` est lu, ignoré par `video_init`, sans un mot ; l'écran SETUP
   offre les dix cartes ; et la valeur que PCem écrit lui-même pour ces machines, `builtin`, est **refusée**.
2. `roms/roms.sha256` ancre 13 des 34 ROM que le code charge : 21 manquent, dont `awe32.raw`. `VENDORED.md` décrit
   le seul 5150/XT, et des fichiers qui n'existent pas (`patches/`, `pcem-ref-build/`).
3. `THIRD_PARTY_NOTICES.md:36` dit « No » pour DOSBox, alors que `dbopl.cpp` (G8) et `cdrom_image.cpp` (G10.3) sont
   transcrits ; l'Ayumi « hors cible » et le seul corpus 8088 sont périmés aussi.
4. TRANSCRIPTION.md : la ligne 200 (`VIDEO_CARD`) ignore les quatre cartes de G9 ; la ligne 222 dit `mouse_ps2.c`
   « non transcrit » (il l'est depuis PS2.0) ; la ligne 217 dit « un seul » contrôleur câblé (six).
5. PCEM_BUGS.md : PB-97, 99 et 100 n'ont toujours pas de champ *Effet* (PB-64, 66 et 24 non plus) ; PB-98 couvre
   l'EGA depuis G13.1 (fait) ; « deux cent cinquante-sept » pour 294 entrées, et la table s'arrête à PB-262.
6. Les commentaires cités sont tous encore périmés, et dix autres sites s'y ajoutent ; `UsageText` et le README
   décrivent 2 machines sur 9 et 4 cartes sur 10.
7. Le vidage `--boot` de l'EGA passe par `ega_read` (cycles, verrous) et lit toujours B800 : faux en mono, et il
   modifie la machine qu'il observe. Les bancs EGA en dépendent (`egabanc.attendus`).
8. La phase 2 de boot-diff ne rejoue ni frappe, ni souris, ni manette, et **repart d'images sales et partagées** :
   A: est l'image SOURCE, ouverte par les deux côtés ; C: est la copie du C#, donnée aussi à l'oracle.
9. `make bench` ne se lie toujours pas (déduit) : 17 symboles des objets C++ manquent au lien C seul (`h_opl_reset`…).
10. GR.5 : `§ GR` absent ; l'en-tête « est fait » manque à PLAN-G8 et PLAN-G9 (PLAN-G10 l'a depuis `5372ad2`) ;
    PLAN-G9 n'a pas de « Les risques ». Hors liste : PLAN.md dit encore « G13 en clôture », PLAN-G13 n'a pas son
    en-tête, et `check-oracle` dérive sur `harness.c` et `harness.h` depuis G13.5a.

## 1. L'état, point par point

### 1.1 La M24 et le PC1512 face à `gfxcard` — à faire, en entier

- **La lecture** (vérifié) : `pc.cs:448` appelle `setgfxcard` sur la clé, sans regarder la machine ; `setgfxcard`
  (`pc.cs:325-345`) ne connaît que les dix entrées du registre. Il n'y a pas de `check_gfxcard` : les contrôles de
  configuration sont `check_cpu` (`:357`), `check_mem_size` (`:402`), `check_hdd_controller` (`:1059`),
  `check_sndcard` (`:1120`).
- **L'effet** (vérifié) : `video_init` (`Video/video.cs:503-523`) monte `pc1512_device` ou `m24_device` par le romset
  et sort ; `video_is_*` (`:484`, `:491`, `:498`) et `video_card_getdevice` (`:407-418`) passent le romset avant la
  carte. Une clé `gfxcard = vga` sur la M24 est donc acceptée puis ignorée, sans avertissement.
- **Le défaut en plus, nouveau** (déduit du code, non exécuté) : PCem enregistre pour ces machines
  `gfxcard = builtin`. Sa boîte de configuration n'y offre que « Built-in video », grisé (`wx-config.c:117-124`) ;
  `video_card_getid("Built-in video")` rend `GFX_BUILTIN` (`video.c:359-360`), écrit `builtin` par
  `video_get_internal_name` (`pc.c:879`, `video.c:387-392`). iXtal refuse ce nom : la boucle de `setgfxcard` ne le
  trouve pas, et le message « carte inconnue » sort. Un `.cfg` de PCem pour la M24 ne se charge donc pas.
- **L'écran SETUP** (vérifié) : `BuildVideoList` (`Host/SdlSetup.cs:315-337`) n'écarte une carte que si sa ROM manque
  (`video_card_available`) ; aucun regard sur `MODEL_GFX_FIXED` (`Models/model.cs:86`, posé sur `m_pc1512` `:354` et
  `m_olivetti_m24` `:369`). Le changement de machine appelle `ClampCpu`, `ClampController`, `ClampSound`
  (`:561-563`), pas de pendant pour la vidéo. `--setup-check` (`:1208-1279`) ne teste la vidéo que sur la machine
  courante, jamais une machine à vidéo fixe.
- **Les profils** (vérifié) : `ixtal26-m24.cfg` et `ixtal26-pc1512.cfg` n'ont pas de clé `gfxcard` ; le défaut y est
  donc la CGA, silencieuse. G13.12a (les cavaliers de l'EGA et de la CGA) et G13.13b (COM/LPT) n'y ont pas touché.
- **L'oracle** : `h_set_gfxcard(pc.gfxcard)` (`BootDiff.cs`, `harness.c:2224`) ; `harness.c:1914-1922` suit le
  romset comme le C#. Poser `GFX_BUILTIN` (-1) côté C# enverrait -1 à `h_video_cards[]` (déduit : un indice -1 en C).

### 1.2 `VENDORED.md`, `roms/roms.sha256`, `THIRD_PARTY_NOTICES.md` — à faire

**`roms/roms.sha256`** (vérifié ; script jetable : les noms des `romfopen`, `rom_init`, `rom_present`, `loadfont`
du C# relevés par `grep`, puis `sha256sum` et recherche dans le manifeste). 13 lignes, toutes `OK` à
`sha256sum -c`, toutes utilisées. Le code en charge 35 ; `im1024font.bin` est absent par choix (`mem_bios.cs:92`,
VENDORED.md). Il manque **21** ROM présentes dans `roms/` :

- cartes mères (G3-G6, l'AT) : `ibmat/62x0820.u27`, `ibmat/62x0821.u47`, `ami286/amic206.bin`, `ami386/ami386.bin`,
  `ami386dx/opt495sx.ami`, `ami486/ami486.bin` ;
- G1 : `pc1512/40043.v1`, `pc1512/40044.v1`, `pc1512/40078.ic127`,
  `olivetti_m24/olivetti_m24_version_1.43_low.bin`, `olivetti_m24/olivetti_m24_version_1.43_high.bin` ;
- vidéo (M15, G7, G9) : `ibm_vga.bin`, `5429.vbi`, `86c764x1.bin`, `et4000.bin`,
  `ibm_6277356_ega_card_u44_27128.bin` ;
- disques (M12, G10.2, G11) : `ibm_xebec_62x0822_1985.bin`, `dtc_cxd21a.bin`, `ide_xt.bin`,
  `adaptec_aha1542c_bios_534201-00.bin` ;
- son (G12.2) : `awe32.raw` (`4e143b94…a710e`, égal à `Awe32Sha256`, `tools/iXtal26.Diff/BootDiff.cs:275`).

`roms/` compte 367 fichiers (`find -type f`) : le paquet de ROM de PCem entier ; le manifeste ne doit porter que ce
que le code lit. Piège : `roms/ide_xt.bin` (celle que lit `Ide/xtide.cs:111`) diffère de `roms/xtide/ide_xt.bin`
(`cmp`, octet 5) ; `roms/xtide/` porte les outils XTIDE de GR.3.

**`VENDORED.md`** (vérifié). L'empreinte de l'arbre tient : recalculée, `26f0727c…53b7`, 989 fichiers. Périmé :
« transcription en C# de son cœur 8088/XT » (§ Rôle), « Contenu attendu pour la cible IBM PC/XT » (le tableau des
ROM, quatre lignes), `patches/000N-*.patch` et `pcem-ref-build/` — ni l'un ni l'autre n'existe ; l'oracle est
`tools/oracle/libixtal26oracle.so` (le harnais inclut le `.c`, sans patch).

**`THIRD_PARTY_NOTICES.md`** (vérifié ; dernière mise à jour `595033c`, le 11/10, pour SoftFloat).
- `:36` DOSBox « **No.** » : faux depuis G8.1 (`Sound/dbopl.cs`, ORACLE `pcem-dev/src/dosbox/dbopl.cpp`) et G10.3
  (`Cdrom/cdrom_image.cs`, ORACLE `dosbox/cdrom_image.cpp`). Seul `vid_cga_comp.c` reste non transcrit. Les deux
  sources amont portent « Copyright (C) 2002-2010 / 2002-2015 The DOSBox Team », GPL v2 « or (at your option) any
  later version » ; `pcem-dev/NOTICE:4-…` liste l'équipe. Les tables ADPCM de `sound_sb_dsp.c`, « borrowed from
  DOSBox » (`Sound/sound_sb_dsp.cs:162`), sont du code de PCem qui le dit.
- `:37` Ayumi : « Sound is out of scope for the XT target » — périmé (le son est transcrit) ; la réponse « No »
  reste juste. `:42-44` « beyond the IBM PC/XT target » : périmé.
- `:103-114` SingleStepTests : le seul corpus 8088 `v2` ; le dépôt en a quatre (`vectors/sst`, `sst8086`, `sst286`,
  `sst386`, quatre `MANIFEST.sha256` commités, quatre `tools/fetch-sst*.sh`).

### 1.3 TRANSCRIPTION.md — à faire

- `:200` (vérifié) : « les 44 autres `VIDEO_CARD` » et « le registre … réduit à `v_cga` et `v_vga` … Depuis M19 …
  G7.1 … G7.3 … G1.1 et G1.2 ». Manquent `v_mda`, `v_hercules`, `v_ega`, `v_et4000ax` (G9.0-G9.3). PCem définit 50
  `VIDEO_CARD` (`grep -c "^VIDEO_CARD " video.c`), en enregistre 49 hors PGC (`video.c:1301-1356`, 50 appels dont un
  sous `USE_EXPERIMENTAL_PGC`) ; le C# en a 10 (`video.cs:379`) : il en reste **40** définies, 39 enregistrées.
- `:222` (vérifié) : « `mouse_scan` … appartient à `mouse_ps2.c:10`, non transcrit, et vit dans `keyboard_at.cs` ».
  Faux depuis PS2.0 : `Mouse/mouse_ps2.cs:48` le définit, `keyboard_at.cs:155` le dit, `oracle.tsv:216` l'ancre.
- `:217`, hors liste (vérifié) : « Seize contrôleurs … dont un seul est câblé » ; six le sont (`pc.cs:1275-1290` :
  `mfm_xebec`, `dtc5150x`, `mfm_at`, `ide`, `xtide`, `aha1542c`).

### 1.4 PCEM_BUGS.md — partiel

- **Le champ *Effet*** (vérifié ; `awk` : pour chaque `### PB-`, la présence de `*Effet*` avant l'entrée suivante).
  294 entrées, six sans *Effet* : **PB-97** (`:1833`), **PB-99** (`:1866`), **PB-100** (`:1953`), et hors liste
  PB-64 (`:1112`), PB-66 (`:1162`), PB-24 (`:7143`, qui a *Atténué en pratique*). Les trois de GR.4 restent à faire.
- **PB-98 étendu à l'EGA** : **fait** en G13.1 (`:8026-8027`, `ega_close`, `vid_ega.c:1068-1073`, marqueur
  `Video/vid_ega.cs`) ; le XTIDE l'était en G10.2.
- **Les totaux** (vérifié ; `grep -c "^### PB-"` = 294 ; numéros PB-01 à PB-294 contigus, aucun doublon ; par
  section A 246, B 26, C 22 ; *Corrigé en mode matériel* 202, *Non corrigé* 71, ni l'un ni l'autre 21, comptés par
  entrée — les deux premiers égaux au message de `d56d161`). Périmé dans « Portée de ce registre » (`:8096-8178`) :
  « deux cent cinquante-sept » (`:8098`) ; la table « Trouvé par » s'arrête à PB-262 (`:8148`) : 32 entrées,
  PB-263 à PB-294, sans ligne ; « les cartes son autres que l'AdLib et les SB 1.0 à 16 » (`:8152`) oublie l'AWE32
  (G12.2) ; « environ 8 600 des 309 000 lignes » (`:8150`) est loin du compte — la somme des plages de `oracle.tsv`
  donne 62 800 lignes sur 148 fichiers C (doublons compris, en-têtes comprises : un ordre de grandeur, déduit ; la
  méthode du chiffre d'origine est inconnue).

### 1.5 Les en-têtes et commentaires — à faire

Tous relus (vérifié), tous encore périmés :
- `Video/video.cs:5-8` (cité) : le registre « réduit à v_cga et v_vga », « les 62 autres cartes » ; il a 10
  entrées (`video.cs:379`), il en reste 40 ;
- `Video/video.cs:67-73` (cité) : « les 48 VIDEO_CARD … autres que v_cga et v_vga », « réduit à ses deux entrées »,
  « aucune des deux cartes n'a de dialogue » ;
- `PluginApi/device.cs:127-133` (cité `:132-133`, la ligne 133 porte la phrase) : `sound_cards[]`, `lpt_devices[]`,
  `SOUND_CARD`, `LPT_DEVICE` dits omis — ils sont transcrits (`Sound/sound.cs:190`, `Lpt/lpt.cs:48`, `:65`) ; et
  `VIDEO_CARD` « réduits à la CGA et à la VGA ».

Hors de la liste de GR.4, du même ordre :
- `PluginApi/device.cs:142-144` : `model_get_config_*` « arrivent avec le modèle xt » (le XT est là depuis M10) ;
- `Video/video.cs:474-476` et `:507-510` : « les quatre romsets du dépôt » (neuf machines) ;
- `pc.cs:241-243` : `setgfxcard` choisit « "cga" ou "vga" » (dix noms) ;
- `pc.cs:1256-1260` et `Disc/hdd.cs:63-70` : `VIDEO_CARD` « réduit à deux entrées », « une seule carte câblée » ;
- `tools/oracle/harness.c:1906-1909`, `harness_stubs.c:505`, `:619-626` : « quatre romsets », « v_cga et v_vga » ;
- `tools/iXtal26.Diff/AtProbe.cs:156` : « les quatre romsets ».

`UsageText` est `iXtal26/Host/CommandLine/UsageText.cs` (vérifié) : `:27-28` les clés de `--config` (sept), `:29`
`--model` « ibmpc ou ibmxt », `:31-33` `--gfxcard` quatre cartes (cga, vga, tvga8900d, tvga9000b), `:35-37` les
tables de `--cpu` de quatre machines, `:20` « émule l'IBM PC 5150 ». Le registre a 10 cartes (`video.cs:379`),
`models[]` 9 machines (`Models/model.cs:386`). Trois options du lanceur n'y figurent pas (`--make-nvr`,
`--menu-check`, `--frames` ; `grep` des `case "--…"` de `Launcher.cs` contre le texte), à juger.
Le README (`iXtal26/README.md`, vérifié) : `:3` « IBM PC 5150 et IBM XT 5160 … CGA » ; `:120-124` « Deux
machines » ; `:126-129` « Quatre cartes vidéo » ; `:347-352` § ROMs, le seul 5150, et le renvoi au manifeste.

### 1.6 Le vidage `--boot` de l'EGA — à faire

Ce que fait `--boot` (vérifié, `iXtal26/BootTest.cs:368-399`) : `DumpTextScreen` prend la carte SVGA si elle existe
et lit sa VRAM **brute** (`DumpVgaTextScreen`, `:411-441` : plans 0/1, colonnes, lignes et page tirées de la BDA,
`0040:004A`, `:004E`, `:0084`). Sinon, `video_is_mda()` choisit B000 ou B800 et le vidage lit 80 × 25 cellules par
`mem_readb_phys`, à travers la carte.

Pour l'EGA, qui n'est pas dans le socle SVGA et dont `video_is_mda()` vaut 0 :
- la lecture passe par `ega_read` (`Video/vid_ega.cs`, en tête) : `cycles -= video_timing_read_b`,
  `pc.cycles_lost`, `egareads++`, et les quatre verrous `la..ld` réécrits. Le vidage **change la machine** qu'il
  observe, au milieu d'une exécution (`TypeAndDump`, `:218`, `:232`, après chaque ligne tapée) ; c'est exactement
  ce que `DumpVgaTextScreen` évite pour la VGA (son commentaire `:401-404`). La CGA a le même travers, plus doux
  (`cga_read`, `vid_cga.cs:170-182` : `cycles -= 4`, la neige) ;
- le segment est toujours B800 : une EGA sur moniteur monochrome (le réglage 11 de G13.12a) range le texte en B000,
  et le vidage rend un écran vide (déduit). Le témoin de G13.12a l'a contourné par `@shot` (VERIFICATION.md
  `:13165-13168`, « l'écran en B000h, lu par `@shot` ») ;
- 80 × 25 fixes : ni 40 colonnes, ni les 43 lignes de l'EGA, ni la page active ;
- un `readmode` de 1 laissé par un programme ferait lire la comparaison de couleurs au lieu des caractères (déduit).

`@vram` (G13.9g, `BootTest.cs:125-139`) hache bien la VRAM de l'EGA, mais ne l'affiche pas. Dans boot-diff, le
vidage vient après la comparaison des sondes (`BootDiff.cs:847-853`) : le verdict n'en dépend pas ; mais après
`closepc`, l'EGA est déjà fermée et sa projection, restée (PB-98), répond encore (déduit).

**Ce qui en dépend** (vérifié) : les portes `banc-egabanc` et `banc-egabanc-m` (`tools/gates/series.sh:300-305`)
lisent l'écran de `--boot` contre `tools/gates/bancs/egabanc.attendus`, qui porte des lectures de 3DAh sensibles au
temps (« le dernier 3DAh, lu une fois, 01h (le bit 0, l'instant de la lecture) »).

### 1.7 La phase 2 de boot-diff — à faire, et plus large qu'écrit

Les lignes citées par PLAN-G10.md (`BootDiff.cs:490`, `:744`) ont glissé : la phase 2 est `Phase2`
(`tools/iXtal26.Diff/BootDiff.cs:1208-1338`), appelée en `:888`, signature `(romsPath, index, discA)` ; son message
est en `:1336-1337`. Relu (vérifié) :
- **aucune frappe** : le calendrier `script` (construit `:529-542`) n'est pas passé ; ni `h_rawinputkey`, ni
  `h_kbd_process`, ni `keyboard_poll_host`/`keyboard_process` aux fins de tranche ;
- **ni souris ni manette** : `SourisOracle`/`ManetteOracle` (appelées par tranche en phase 1 et dans `Lockstep`,
  `:1160-1170`) n'y sont pas ; `PousserManettes` remet seulement l'état initial (`:110-113`) ;
- **pas de tranches** : `h_step_trace` et `PasCsharpTrace` avancent instruction par instruction, sans budget ; même
  sans frappe, le travail de fin de tranche du clavier manque (son effet sans touche : inconnu) ;
- **les images** : `h_set_discfn(0, discA)` et `discfns[0] = discA` (`:1210`, `:1263`) donnent aux deux côtés
  l'image **source** A:, la même pour les deux — ce que les copies par côté (`:545-566`) interdisent, et l'image de
  l'utilisateur écrite si l'invité écrit avant l'index (déduit). B: n'est pas reposé : chaque côté garde la copie
  écrite par sa phase 1. Les disques durs : `h_set_hdd(hd, ide_fn[hd], …)` (`:1236-1238`) prend `ide_fn`, que la
  phase 1 a posé sur les copies **du C#** (`:748-751`) : les deux côtés ouvrent le même fichier, déjà écrit. Le ZIP :
  chaque côté sa copie de phase 1 (`ZipOracle`, `:145`, `:595`).
- Le pas commun `--lockstep` (M19, `:1117-1206`) rejoue la frappe, la souris et la manette tranche par tranche, et
  nomme la ligne tapée : c'est l'outil qui marche, la phase 2 ne le dit pas.

Le budget de la tranche est le même des deux côtés : `h_runpc` tracé décompte `1 - _cycles` par instruction jusqu'à
`h_slice_budget()` (`harness.c:2138-2165`), et `h_step_trace` rend les cycles de l'instruction. Une phase 2 qui
décompte le même budget peut donc rejouer les fins de tranche aux mêmes instructions (déduit, à prouver).

### 1.8 `make bench` — à faire

`tools/oracle/Makefile:210` `BENCH_SRC := $(filter-out harness_wrap.c,$(SRC))` : du C seul ; `CXXSRC`
(`harness_dbopl.cpp`, `harness_cdrom.cpp`, `:184`) n'entre que dans la `.so` ; `:181` et `:190` disent encore
« bench, qui ne lit ni l'OPL ni le DSP, reste en C seul ». `bench.c` n'appelle que `h_boot`, `h_getstate`,
`h_ram_hash`, `h_run` (tous dans `harness.h`) — mais `harness.c` et `sound_opl.c` référencent des symboles définis
dans les seuls objets C++. Comptés sur les objets de `tools/oracle/build/` (`nm -u` des 87 objets C contre
`nm --defined-only` des deux objets C++ ; objets du 09/10, la liste d'aujourd'hui est déduite) : **17** — dans
`harness.o`, `h_opl_reset`, `h_opl_state`, `h_cd_audio_callback`, `h_cd_audio_stop`, `h_cd_boot_raz`, `h_cd_config`,
`h_cd_exit`, `h_cd_initpc`, `h_cd_initpc_fin`, `h_cd_resetpchard` ; dans `sound_opl.o`, `opl_init`, `opl_read`,
`opl_write`, `opl2_update`, `opl3_update`, `opl_timer_over` ; dans `scsi_cd.o`, `cdrom_drive`. Le lien
(`-Wl,--no-undefined`, `:242`) échoue donc (déduit ; non construit). PLAN-G10.md `:291-293` ne citait que le
premier.

### 1.9 GR.5 — les documents

- **VERIFICATION.md § GR** : absent (vérifié : aucun `## GR` ; la dernière section est `## G13.13c`, `:13384`).
- **En-têtes « est fait »** (vérifié, `grep` de la ligne `> **Gx est fait**`) : PLAN-G10 l'a (`:3-6`, posé par
  `5372ad2` le 05/10, avec le renvoi de la clôture à GR) — **fait** ; PLAN-G8 non (ses quatre étapes sont marquées
  ✅, `:49`, `:56`, `:65`, `:73` ; commits `5ad3a8f`, `a0f0b71`, `59500a5`, `1394107`, plus `12b01b0`) ; PLAN-G9 non
  (✅ en `:96`, `:105`, `:113`, `:121` ; `66690cd`, `f963e04`, `af06fcb`, `fc87801`). Hors liste : PLAN-G13 non
  (G13 close à `d56d161`), et sa section s'intitule encore « Les décisions (proposées ; à trancher) » (`:901`) ;
  PLAN-G3 et PLAN-PS2 n'en ont pas non plus.
- **« Les risques » de PLAN-G9** : absent (vérifié : sections `:7`, `:36`, `:52`, `:94`, `:130`, `:137`). PLAN-G8 en
  a un (`:102-109`), qui ne dit pas `make bench`.
- **PLAN.md** (hors liste, vérifié) : le titre `:43` « 16 octobre 2026, G12 fait ; G13 en clôture », la frise `:47`
  « [ICI] G13 », la puce `:114` « **G13, en cours** » (dont la fin, `:173-176`, dit pourtant « G13 est close »), le
  titre du bloc `:396` « en cours ». `:41` « G11 à G13 seront planifiés avec leur clôture ».
- **`check-oracle`** (hors liste, vérifié par recalcul : la boucle de `tools/check-oracle.sh` refaite en `awk` et
  `sha256sum`) : deux dérives, `tools/oracle/harness.c` (`oracle.tsv:15`, `:129`, ancré sur l'état de `02244e6`,
  G12.2) et `harness.h` (`:39`), changés par `cb69404` (G13.5a, 10/10) et jamais réancrés ; aucune section de G13
  n'en rend compte (aucun `check-oracle` après la ligne 9 000 de VERIFICATION.md). `check-oracle` n'est pas dans
  `series.sh`.
- **`.gitignore`** (hors liste) : « Les deux .cfg de la racine » (il y en a douze), « Seul vectors/sst/MANIFEST »
  (quatre).

## 2. Ce qui reste, et les étapes proposées

Six étapes. Les trois premières changent l'outillage ou l'émulateur, les trois dernières des documents seulement.

### GR.4a — Les machines à vidéo intégrée

- **Contenu** : `check_gfxcard` dans `pc.cs`, appelé comme `check_sndcard` : sur une machine `MODEL_GFX_FIXED`, une
  clé ou une option `gfxcard` autre que `builtin` est ignorée **avec un avertissement** (« la M24 a sa vidéo
  intégrée ; gfxcard = « vga » ignoré ») ; `setgfxcard` accepte `builtin` (le nom que PCem écrit) sur ces machines,
  le refuse ailleurs. `gfxcard` garde sa valeur interne (la CGA, ce que PCem porte quand la clé manque), pour que
  `h_set_gfxcard` et les portes ne bougent pas. L'écran SETUP : la ligne « Video » montre « Built-in video », non
  sélectionnable, sur ces deux machines (comme `wx-config.c:117-124`) ; `config_save` y écrit `builtin`. Les
  commentaires `pc.cs:241-243` dans la même étape.
- **Portes et preuves** : une porte `r9-gfxfixe` (C# seul) : M24 et PC1512, sans clé, `builtin`, `vga`, `ega`,
  un nom inconnu ; l'avertissement attendu, la machine amorcée avec sa vidéo ; `--setup-check` étendu (la M24
  choisie : la liste vidéo réduite à « Built-in video » ; retour au XT : les dix cartes). Témoin : `--boot` de la M24
  avec `gfxcard = vga`, l'écran de la M24.
- **Contrôles négatifs** : l'avertissement retiré, la porte rougit ; `BuildVideoList` sans le filtre, le
  `--setup-check` rougit ; `builtin` refusé, la porte rougit.
- **La série** : `pc.cs` change, donc la série entière (la règle) ; attendu : identique à l'unité, une porte de plus.

### GR.4b — La phase 2 de boot-diff

- **Contenu** : `Phase2` reçoit le calendrier de frappe, les copies et les injections de la phase 1 ; elle repart
  de copies **fraîches**, une par côté, de A:, B:, des disques et du ZIP (refaites depuis les sources, comme
  `:561-595`) ; elle décompte le budget de la tranche des deux côtés (`cpu_get_speed() / 100`, les cycles rendus
  par le pas) et fait, à chaque fin de tranche, ce que fait la phase 1 : les touches de la tranche suivante, le
  clavier, `SourisOracle`/`SourisCsharp`, `ManetteOracle`/`ManetteCsharp`. Si un écart de tranche entre les deux
  côtés apparaît avant l'index, le dire et renvoyer à `--lockstep`. Le message final `:1336-1337` ne parle du
  hachage que si la phase 2 a bien rejoué la même exécution.
- **Portes et preuves** : le contrôle négatif de G10.0 rejoué (la file de la DSS pleine à 15,
  `dss_read_status`) : la phase 2 doit nommer la lecture de 379h en 0600:0175, AL = 40h contre 00h, à l'instruction
  132 103 671 (VERIFICATION.md `:5654-5660`) ; une divergence injectée pendant une frappe sur `bd-pcdos-ega` (une
  campagne tapée) nommée de même ; une écriture disquette injectée (FORMAT) : l'image source inchangée après la
  phase 2 (`sha256sum` avant et après).
- **Contrôles négatifs** : sans le rejeu des touches, la première preuve retombe sur « les états concordent » ; avec
  A: source au lieu d'une copie, l'empreinte de la source change.
- **La série** : outillage seul ; sous-ensemble ciblé (les portes tapées vidéo et LPT, < 15 min). Les portes vertes
  ne passent jamais par la phase 2 : rien ne doit bouger.

### GR.4c — Le vidage `--boot` de l'EGA

- **Contenu** : `DumpEgaTextScreen(ega_t)`, comme `DumpVgaTextScreen` : la VRAM **brute** (`vid_ega.Probe`), le
  caractère au plan 0 en `vram[(o & ~1) << 2]` (le chemin `chain2_read` de `ega_read`), le segment par le moniteur
  (B000 en mono, B800 en couleur, lu dans `0040:0063`), colonnes, lignes et page dans la BDA (`:004A`, `:0084`,
  `:004E`). Rien n'est lu à travers la carte : ni cycles, ni verrous. Pour la CGA, la MDA et l'Hercules, la même
  lecture brute est possible ; voir la décision n° 3.
- **Portes et preuves** : le témoin de G13.12a rejoué (l'EGA en mono, `D 40:87`) : le texte de DOS lu en B000
  sans `@shot` ; une EGA en 43 lignes (`MODE CO80,43` n'existe pas sous DOS 2 : un `INT 10h AX=1112h` par DEBUG) ;
  `banc-egabanc` et `-m` dans les deux modes.
- **Contrôles négatifs** : le segment forcé à B800, le témoin mono rend un écran vide ; la lecture faite par
  `mem_readb_phys`, les compteurs `egareads` d'avant et d'après le vidage diffèrent (un `Debug.Assert` jetable).
- **La série** : `BootTest.cs` est le chemin de `--boot`, que lisent les bancs : les portes `banc-*` de la vidéo
  en C# seul (`series.sh:300-313`). Si `egabanc.attendus` change (le vidage ne coûte plus de cycles entre les
  lignes), chaque octet changé est expliqué ; le reste de la série n'est pas touché (boot-diff ne vide qu'après
  les sondes).

### GR.4d — `make bench`

- **Contenu** : `bench` lie les deux objets C++ (compilés avec `BENCH_FLAGS`, liés par `$(CXX)`, comme la `.so`) ;
  `-Wl,--wrap=_Znam` et `-ftrivial-auto-var-init=zero` sur `harness_cdrom` comme pour la `.so` ; les commentaires
  `Makefile:179-181`, `:190` corrigés.
- **Portes et preuves** : `make bench` se lie, `build/bench ../../roms 6000 3 600` tourne et rend son empreinte de
  RAM ; le chiffre consigné à côté de VERIFICATION.md `:785-793` (1 659 ms, M5.1), sur la même machine, pour mémoire.
- **Contrôles négatifs** : `harness_dbopl.cpp` retiré de la liste, le lien échoue sur `h_opl_reset`.
- **La série** : la `.so` ne change pas (vérifié par son empreinte avant et après) ; aucune porte.

### GR.4e — Les registres, les licences, les commentaires

- **Contenu** : `roms/roms.sha256` (les 21 lignes, triées comme les 13) ; `VENDORED.md` (le rôle, l'oracle tel qu'il
  est, la table des ROM par machine et par carte, ou un renvoi au manifeste) ; `THIRD_PARTY_NOTICES.md` (DOSBox
  « Yes » pour `dbopl.cpp` et `cdrom_image.cpp`, avec la notice reproduite de `pcem-dev/NOTICE` ; l'Ayumi, la cible,
  les quatre corpus, la date) ; TRANSCRIPTION.md `:200`, `:217`, `:222` ; PCEM_BUGS.md (le champ *Effet* de PB-97,
  99, 100 — et de 64, 66, 24 —, « deux cent quatre-vingt-quatorze », 32 lignes de table PB-263 à PB-294, l'AWE32 dans
  la portée, le compte des lignes refait ou retiré) ; les commentaires du § 1.5, harnais compris ; `UsageText` et le
  README (machines, cartes, ROM) ; `oracle.tsv` réancré pour `harness.c` et `harness.h`.
- **Portes et preuves** : `sha256sum -c roms/roms.sha256` (34 OK) ; un `grep` du manifeste contre les noms du code
  (aucun manque, hors `im1024font.bin`) ; `Awe32Sha256` égal à la ligne du manifeste ; la porte `recensement` verte
  (le champ *Effet* ajouté n'y change rien, déduit) ; `check-oracle` 0 dérive ; la construction sans avertissement.
- **Contrôles négatifs** : une ligne du manifeste altérée, `sha256sum -c` rougit ; `oracle.tsv` non réancré,
  `check-oracle` rougit (c'est l'état d'aujourd'hui).
- **La série** : commentaires et documents ; le harnais change d'empreinte mais pas de code (à montrer : `.so`
  identique, ou l'ABI inchangée) ; pas de série entière, la construction et `check-oracle` suffisent.

### GR.5 — Les documents

- **Contenu** : l'en-tête « est fait » de PLAN-G8 et PLAN-G9 (dates, commits, § de VERIFICATION, PB), sur le modèle
  de PLAN-G10 `:3-6` : « Sans la clôture de « Ce qu'un bloc livre » … revient au bloc GR » ; « Les risques » de
  PLAN-G9, tiré de PLAN.md GR.1 et de l'état que relèvent les lectures de GR.0 et GR.1 (EGA à 64 Ko et mono hors
  boot-diff, `display_type` ambre, MDABANC, EGABANC sans AND/OR, ET4BANC, le FFh et les 32 bits du RAMDAC,
  `et4000_t` non sondé, le boot-diff DOS de l'ET4000 sur l'ami486, les pixels de GR.0, le vidage de GR.4c), chacun
  avec ce que G13.9 et G13.12a en ont fait ; « Les risques » de PLAN-G8 complété de `make bench` ; PLAN.md
  « Où on en est » (G13 fait, [ICI] GR) et le titre de son bloc G13 ; l'en-tête « G13 est fait » de PLAN-G13 et le
  titre de ses décisions. VERIFICATION.md § GR, à la fin du bloc : une sous-section par étape (GR.0 à GR.5), et
  ses tableaux — les portes vidéo (carte × machine × mode, champs de sonde remplis, la panne injectée et le champ
  qu'elle rougit), la couverture comptée par fichier, les constats de la relecture et leur sort, les témoins, la
  dette de GR.4 point par point avec son commit — puis « Ce que ce vert ne dit pas » (au moins : le mode matériel
  n'a pas d'oracle, les bancs en C# seul ; la phase 2 ne prouve rien sans `--lockstep` sur une campagne tapée
  avant GR.4b ; les ROM non distribuées ; les écoutes laissées à Julien).
- **Portes et preuves** : relecture ; aucun code.
- **La série** : aucune.

## 3. Les risques

1. **Le vidage de l'EGA change les bancs.** Les lectures par `ega_read` coûtent des cycles entre les lignes tapées ;
   les retirer peut déplacer d'une fraction de trame les lectures de 3DAh d'EGABANC et changer
   `egabanc.attendus`. Chaque octet changé doit s'expliquer, dans les deux modes ; si la CGA et la MDA passent aussi
   en lecture brute, les attendus de leurs bancs (HERCBANC, PICBANC, SBBANC… sous `--boot` en CGA) peuvent bouger
   de même.
2. **La phase 2 rejouée tranche par tranche** suppose que le budget compté par le pas égale celui de `h_runpc`
   tracé ; un écart d'un cycle déplacerait une fin de tranche. La preuve de GR.4b le mesure ; sinon, la phase 2
   renvoie à `--lockstep` au lieu de se tromper.
3. **`builtin` sur une machine à carte** : chez PCem, `video_get_video_from_internal_name("builtin")` rend
   `GFX_BUILTIN` sur n'importe quelle machine (`video.c:394-407`), puis `video_cards[-1]` ; le refuser ailleurs que
   sur la M24 et le PC1512 est une déviation de configuration, à inscrire.
4. **La licence de DOSBox** : « GPL v2 or later » en amont, `GPL-2.0-only` pour iXtal ; compatible (déduit), mais
   c'est un texte public qui engage : à faire relire par Julien avant G17.
5. **Le manifeste des ROM** n'est vérifié par aucune porte (`sha256sum -c` à la main) ; une ROM changée sans lui ne
   se verrait qu'aux portes de sa machine.
6. **Le réancrage de `oracle.tsv`** : `--fix` réécrit toutes les empreintes ; ne le lancer qu'après avoir vu que les
   deux seules dérives sont `harness.c` et `harness.h` (le commentaire du script le dit pour `pcem-dev/`).

## 4. Les décisions ouvertes

Celles que PLAN.md § GR (04/10), PLAN-G9, PLAN-G10, PLAN-G13 « Les décisions » et VERIFICATION laissent ouvertes.

1. **La valeur `builtin`.** « Ce qu'un bloc livre » (n° 5) tranche le principe — une valeur hors liste ramenée au
   défaut ou refusée, avec un avertissement —, pas le cas de la valeur que PCem écrit lui-même. *Proposé* : `builtin`
   accepté en silence sur la M24 et le PC1512, refusé ailleurs ; toute autre carte ignorée avec un avertissement sur
   ces deux machines ; `gfxcard` interne inchangé (la CGA), pour que le mode PCem et l'oracle ne bougent pas.
2. **La phase 2 : rejouer ou se récuser.** *Proposé* : rejouer (GR.4b), avec le repli « écart de tranche : utilisez
   `--lockstep` » ; la récusation seule (« campagne tapée : phase 2 non rejouée ») est le minimum si la preuve de
   GR.4b échoue.
3. **Le vidage brut pour la CGA, la MDA et l'Hercules.** GR.4 ne nomme que l'EGA. *Proposé* : l'EGA seule en GR.4c ;
   les trois autres gardent leur chemin, et « Ce que ce vert ne dit pas » le note (un vidage qui coûte 4 cycles par
   lecture en CGA) ; les étendre attend un feu vert, parce qu'il peut changer les attendus de bancs hors vidéo.
4. **Le champ *Effet* de PB-64, 66 et 24**, hors de la liste de GR.4. *Proposé* : dans la même étape (GR.4e), le
   registre devient uniforme et la porte `recensement` pourrait l'exiger.
5. **Le compte des lignes transcrites** de PCEM_BUGS.md (`:8150`). *Proposé* : le retirer, ou le remplacer par une
   méthode écrite (la somme des plages de `oracle.tsv`, sans doublons) ; la méthode du 8 600 n'est pas consignée.

## Annexe — Candidats, hors de la portée

Aucun défaut neuf de PCem. Deux défauts de l'outillage d'iXtal, trouvés en lisant, tous deux dans la portée de
GR.4 : les images de la phase 2 (§ 1.7, A: source partagée, C: la copie du C# donnée à l'oracle) et la dérive de
`check-oracle` (§ 1.9). Un écart de configuration d'iXtal par rapport à PCem : `builtin` refusé (§ 1.1). Le même
montage d'images que la phase 2 (A: source, `ide_fn` du C# pour les deux côtés) se retrouve dans les verbes de sonde
`SpeakerProbe` (`BootDiff.cs:1644-1705`) et la sonde du disque (`:1773-1831`) ; amorçages courts, sans écriture
attendue (déduit) : à revoir avec GR.4b, sans plus.
