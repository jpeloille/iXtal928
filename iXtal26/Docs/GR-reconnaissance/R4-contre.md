# GR.4 et GR.5 — contre-lecture du rapport R4

Le 20 octobre 2026. Contre-lecture de `R4-dette.md`, en lecture seule du dépôt (`master`, `d56d161`) : rien de
construit, aucune série ni aucun émulateur lancé. Seul `tools/check-oracle.sh` (sans `--fix`, 1,6 s, il ne fait que
lire) a tourné. Chaque fichier:ligne cité par R4 a été rouvert ; les comptes ont été refaits par les commandes notées.

**Niveaux** : *documenté* (le code ou le fichier le dit à la ligne, ou la commande le rend), *déduit*, *inconnu*.
**Marques** : **C** confirmé, **K** corrigé, **A** ajouté.

Verdict : R4 tient. Aucun constat n'est renversé, y compris les deux que la consigne désignait (la phase 2 et
`builtin`). Trois chiffres ou formules sont à corriger (K1 à K3), une précision change le sens d'une décision
(K4, K5), et sept points manquent (A1 à A7). Sur les décisions ouvertes : une seule l'est vraiment (§ 5).

## 1. Les comptes, refaits

- 13 ROM au manifeste, toutes `OK` — commande : `cd roms && sha256sum -c roms.sha256` ; `grep -c ': OK'` ; résultat : 13
  OK, 0 autre ; C
- Le code en charge 35, `im1024font.bin` exclu, 21 manquent — commande : `grep -rhoE
  '(romfopen|rom_present|rom_init|loadfont…)\(…"x.y"'` sur `iXtal26/*.cs`, `sort -u` ; résultat : 32 littéraux ; + 4
  `basicc11.*` bâtis en boucle = 36 ; moins `im1024font.bin` et `at386/at386.bin` (en commentaire,
  `Memory/mem_bios.cs:141`) = 34 ; 34 − 13 = 21 ; C
- `awe32.raw` = `Awe32Sha256` — commande : `sha256sum roms/awe32.raw` contre `BootDiff.cs:275` ; résultat :
  `4e143b94…a710e`, égaux ; C
- `roms/` : 367 fichiers — commande : `find roms -type f | wc -l` ; résultat : 368, dont `roms.sha256` lui-même : 367
  sans lui ; C
- `ide_xt.bin` ≠ `xtide/ide_xt.bin` — commande : `cmp` ; résultat : octet 5 ; C
- Arbre vendoré : 989 fichiers, `26f0727c…` — commande : `find pcem-dev -type f -not -path '*/.idea/*'` ; la boucle de
  `check-oracle.sh` ; résultat : 989 ; `26f0727cc53e` ; C
- PCem : 50 `VIDEO_CARD`, 50 `pcem_add_video(&…)`, dont `v_pgc` sous `#ifdef` — commande : `grep -c '^VIDEO_CARD '
  video.c` ; `grep -c 'pcem_add_video(&'` ; résultat : 50 ; 50 (`video.c:158-160`, `:1336-1338`) ; C
- Le C# en a 10 ; 40 restent, 39 enregistrées — commande : `video.cs:379` ; résultat : 10 entrées ; 50 − 10 = 40 ; 49 −
  10 = 39 ; C
- 294 entrées PB, A 246 / B 26 / C 22 — commande : `awk` sur `^## A.`, `^## B.`, `^## C.`, `^### PB-` ; résultat : 246 /
  26 / 22 = 294 ; C
- *Corrigé* 202, *Non corrigé* 71, ni l'un ni l'autre 21 — commande : `awk` par entrée sur `^\*Corrigé en mode
  matériel\*` et `^\*Non corrigé` ; résultat : 202 (A 195, B 6, C 1) ; 71 (A 51, B 20) ; 21, tous en section C ; aucune
  entrée avec les deux ; C
- Six entrées sans *Effet* — commande : `awk` par entrée sur `^\*Effet\*` ; résultat : PB-64 `:1112`, 66 `:1162`, 97
  `:1833`, 99 `:1866`, 100 `:1953`, 24 `:7143` ; C
- PB-98 : l'EGA en G13.1 — commande : `PCEM_BUGS.md:8026-8027` ; `Video/vid_ega.cs:1305` ; résultat : « *G13.1* : l'EGA
  aussi », marqueur `PB-98` au code ; C
- 17 symboles manquants de `make bench` — commande : `nm -u` des 88 objets C de `build/` contre `nm --defined-only`
  (`LC_ALL=C comm`, voir A4) ; résultat : 17 : `harness.o` 10 (`h_opl_reset`, `h_opl_state`, `h_cd_audio_callback`,
  `h_cd_audio_stop`, `h_cd_boot_raz`, `h_cd_config`, `h_cd_exit`, `h_cd_initpc`, `h_cd_initpc_fin`, `h_cd_resetpchard`),
  `sound_opl.o` 6, `scsi_cd.o` 1 (`cdrom_drive`) ; C (déduit : non construit)
- Dérive `check-oracle` sur `harness.c` et `harness.h` — commande : `./tools/check-oracle.sh` ; `git show 02244e6:… |
  sha256sum` ; résultat : voir A6 ; C
- `UsageText` : trois options absentes — commande : boucle sur les `case "--…"` de `Launcher.cs` contre `UsageText.cs` ;
  résultat : `--frames`, `--make-nvr`, `--menu-check` ; C
- TRANSCRIPTION.md `:200`, `:217`, `:222` ; THIRD_PARTY `:36`, `:37` ; PLAN-G8/G9 sans « est fait » ; PLAN-G9 sans « Les
  risques » ; PLAN.md `:43`, `:47`, `:114`, `:396` ; PLAN-G13 `:901` ; VERIFICATION sans `## GR` (dernière `## G13.13c`
  `:13384`) — commande : relues à la ligne ; résultat : toutes exactes ; C
- `.gitignore` « deux .cfg » (douze) et « seul vectors/sst/MANIFEST » (quatre) — commande : `ls *.cfg | wc -l` ; `git
  ls-files vectors` ; résultat : 12 ; quatre `MANIFEST.sha256` ; C

## 2. Les affirmations clés, rouvertes

### 2.1 `builtin` refusé par `setgfxcard` — C, avec une précision (K4)

`pc.cs:325-345` : la boucle ne cherche que dans `video_cards` (dix noms) ; `name.Length > 0 && c == Length` sort
avec « carte inconnue » et `return false`, **avant** l'appel à `video_get_video_from_internal_name` (`pc.cs:342`),
qui, lui, connaît `"builtin"` (`video.cs:394-399`). `pc.cs:448` en tire un échec de `loadconfig`. *Documenté.*
PCem écrit bien `builtin` (`pc.c:879` -> `video_get_internal_name`, `video.c:387-392` ; l'identifiant -1 passe par
`video_old_to_new`) et l'écran de configuration pose `GFX_BUILTIN` pour les machines à vidéo fixe
(`wx-config.c:117-131`). Le fait que `.cfg` PCem de la M24 ne se charge pas est donc *documenté*, R4 le donnait
*déduit*.

### 2.2 La phase 2 de boot-diff — C, et la gravité se précise (K3)

Relu `BootDiff.cs:1208-1338` contre la phase 1 (`:545-760`) :

- **A:** (`:1210`, `:1263`) : `h_set_discfn(0, discA)` et `discfns[0] = discA`, `discA` étant la **source**
  (`:888`). Les deux côtés ouvrent le même fichier ; l'invité qui écrit sur A: avant l'index (FORMAT, WRITE DATA)
  écrit sur l'image de l'utilisateur, et le second côté relit ce que le premier vient d'écrire. A: part toutefois
  **propre** (la phase 1 n'a écrit que dans ses copies) : R4 écrit « repart d'images sales », ce qui n'est vrai
  que de B: et des disques.
- **B:** n'est pas reposé : l'oracle garde `oracleB` et le C# `csharpB` (`:564`, `:566`), chacun tel que sa phase 1
  l'a laissé, donc **sales et différents** l'un de l'autre.
- **C: à I:** (`:1236-1238`) : `h_set_hdd(hd, Disc.hdd_c.ide_fn[hd], …)`, or `ide_fn[hd]` vaut `csharpHd[hd]` depuis
  `:751`. L'oracle reçoit donc la copie **du C#**, déjà écrite par la phase 1, et les deux côtés l'ouvrent. Phase 1,
  elle, donnait `oracleHd[hd]` (`:628`). *Documenté.*
- **ZIP** : `pc.cfg_zip_path = csharpZip` (`:752`) reste posé pour la phase 2 ; l'oracle garde `ZipOracle`
  (`:595`) : chaque côté sa copie de phase 1, sale.
- Ni `script` (`:529-542`, non passé), ni `SourisOracle`, ni `ManetteOracle`, ni budget de tranche : *documenté*
  (aucun appel dans `:1208-1338`). R4 dit vrai.

**Gravité (K3)** : « grave » vaut pour la fiabilité du diagnostic, pas pour les portes. `Phase2` n'est atteinte que
par `:888`, après une divergence de la phase 1 ; une porte verte n'y passe jamais (`series.sh` n'attend que des
sorties vertes de `boot-diff`). Ce qui est abîmé : le message final de `:1336-1337` (« le hachage porte sur moins de
champs ») sur une campagne tapée, et l'image source de A: sur un invité qui écrit. R4 le dit presque (§ 1.7), pas
tout à fait (le titre « repart d'images sales et partagées » prête à croire que les portes sont touchées).

### 2.3 Le vidage `--boot` de l'EGA — C

- `BootTest.cs:368-399` : `svga_get_pri()` null pour l'EGA, `video_is_mda()` vaut 0 (`video.cs:484-486`), segment
  B800 fixe, 80 × 25 par `mem_readb_phys`. *Documenté.*
- `ega_read` (`vid_ega.cs:1088-1130`) : `egareads++`, `cycles -= video_timing_read_b`, `pc.cycles_lost`, les quatre
  verrous `la..ld` réécrits (`:1114-1117`). Le vidage change donc la machine. *Documenté.*
- Appelé après chaque ligne tapée (`BootTest.cs:232`) et par `@wait` (`:218`) : R4 exact.
- `@vram` hache la VRAM de l'EGA (`BootTest.cs:128-131`, `Video.vid_ega.Probe?.vram`) ; le témoin de G13.12a passe
  par `@shot` (`VERIFICATION.md:13165-13168`). Exact.
- Les portes `banc-egabanc` et `-m` : `series.sh:305-306`. Exact.

**K5 : ce que R4 ne dit pas du reste de la décision n° 3.** `mda_read` (`vid_mda.cs:152-157`) et `hercules_read`
(`vid_hercules.cs:169-174`) ne font **que** `egareads++` : pas de cycles, pas de verrou. Le vidage de la MDA et de
l'Hercules est donc déjà innocent. Seule la CGA (`vid_cga.cs:170-182` : `cycles -= 4`, la neige et `charbuffer`)
est concernée, plus la M24 et le PC1512, dont les lectures de B800 ne sont pas relues ici (*inconnu*). La décision
n° 3 se réduit à « la CGA aussi, ou non », et le risque n° 1 de R4 (« HERCBANC, PICBANC… peuvent bouger ») ne vaut
que pour les bancs `--boot` en CGA.

### 2.4 La dérive de `check-oracle` — C (A6)

### 2.5 PB-98 étendu à l'EGA — C

`PCEM_BUGS.md:8026-8027` (« *G13.1* : l'EGA aussi ») ; `Video/vid_ega.cs:1305` (marqueur, `ega_close` sans
`mem_mapping_remove` ni `rom_deinit`) ; `vid_ega.c:1068-1073` pour le C (relu par R4, non rouvert ici : *déduit*
pour moi). Le titre de PB-98 (`:8010`) reste « L'Hercules ne retire pas… » : le XTIDE et l'EGA y sont en notes de
bas d'entrée. Cosmétique, hors liste.

## 3. Corrections

- **K1. « 62 800 lignes sur 148 fichiers C » (R4 § 1.4).** `oracle.tsv` compte **182** fichiers sous `pcem-dev/`
  (`awk -F'\t' '$2 ~ /^pcem-dev/'`, `sort -u`) : 100 `.c`, 2 `.cc`, 2 `.cpp` et **78 `.h`**. La somme des plages
  (colonne
  4) refaite en `python3 -I` donne bien 62 800 (60 609 lignes distinctes), mais en mêlant les en-têtes ; sur les seuls
  sources C/C++ : **45 090** lignes, 104 fichiers. Les sources C/C++ de `pcem-dev/src` font 238 641 lignes
  (`find … -name '*.c' -o -name '*.cc' -o -name '*.cpp' \| xargs cat \| wc -l`), pas 309 000. R4 avait raison de
  traiter le chiffre comme un ordre de grandeur ; le « 148 » est faux.
- **K2. THIRD_PARTY `:36`.** R4 dit « seul `vid_cga_comp.c` reste non transcrit » : exact pour DOSBox, mais la ligne
  `src/dosbox/` couvre aussi `nukedopl.cpp` (A1).
- **K3.** La gravité de la phase 2 (voir 2.2).
- **K4. `builtin` : le C# a déjà tout le câblage de -1.** `video.cs:384`, `:396`, `:424-425`, `:440-441`,
  `:449-461` (`GFX_BUILTIN` dans `video_card_available`, `getname`, `old_to_new`, `new_to_old`,
  `get_internal_name`) ; `video.cs:209` et `:481` disent que `gfxcard` « peut valoir GFX_BUILTIN » ;
  `Models/amstrad.cs:185` le teste. La transcription attend ce -1 ; c'est `setgfxcard` seul qui le refuse. R4
  propose de ne jamais le poser (« `gfxcard` interne inchangé, la CGA ») au motif que l'oracle recevrait -1.
  *Documenté* : le motif est exact, mais plus précis que R4 ne le dit : `tools/oracle/harness_stubs.c:633-635`
  (`video_is_mda`, `_cga`, `_ega_vga`) n'a **aucun** test de romset et appelle `h_video_card(gfxcard)`, qui fait
  `fatal("… absente de h_video_cards[]")` (`harness_stubs.c:544`). Côté C#, tout passe déjà par le romset d'abord
  (`video.cs:484-500`, `:507-521`). Deux montages, donc, à dire dans la décision n° 1 : (a) garder -1 comme PCem et
  pousser 0 à l'oracle pour ces deux machines (les appels `Oracle.h_set_gfxcard(pc.gfxcard)` : `BootDiff.cs:619`,
  `:1228`, `:1665`, `:1791`, `CpuConfigCheck.cs:95`, `SvgaLinearCheck.cs:47`) ; (b) la proposition de R4. (b) est
  plus sûr pour le mode PCem ; (a) est ce que fait PCem (`config_save` y écrit `builtin` de lui-même).
- **K5.** La décision n° 3 est plus étroite (voir 2.3).

## 4. Ce que R4 a manqué

- **A1. NukedOPL.** `pcem-dev/src/dosbox/nukedopl.cpp` est le troisième fichier de ce répertoire. Il n'est ni dans
  `oracle.tsv` (2 lignes `pcem-dev/src/dosbox`), ni dans `THIRD_PARTY_NOTICES.md` (aucune occurrence de « nuked »,
  `grep -i`), ni dans `pcem-dev/NOTICE`. Non transcrit : `Sound/sound_dbopl.cs:7`, `:29`, `:56`, `:116` l'omettent
  (`opl_emu` figé à `OPL_DBOPL`). Sa licence n'est pas dans l'en-tête (*inconnu*). La ligne « DOSBox … `src/dosbox/`
  — OPL » de `:36` l'enveloppe à tort ; GR.4e doit ajouter une ligne « Nuked OPL3 — No ».
- **A2. Sites de commentaires périmés en plus des dix de R4** (`grep -n 'VIDEO_CARD\|quatre romsets\|deux cartes'`) :
  `Host/SdlSetup.cs:307` (« aucune des deux cartes ni des quatre… »), `Disc/hdd.cs:7`, `PluginApi/device.cs:9`,
  `tools/oracle/harness_stubs.c:356`, `Video/video.cs:510` (R4 donne `:507-510`, ce qui le couvre). À joindre à
  l'étape GR.4e ; le harnais compris, donc réancrage (A6).
- **A3. Un défaut de PCem que l'annexe aurait dû porter.** R4 écrit « Aucun défaut neuf de PCem », mais son risque
  n° 3 en décrit un : `builtin` lu sur une machine **à carte** donne `gfxcard = -1` (`pc.c:660-664`,
  `video.c:394-407`) puis `video_cards[video_old_to_new(-1)]` = `video_cards[-1]`, une lecture hors tableau, à
  `video_init` (`video.c:915`), `video_card_available` (`:219`) et `video_card_getdevice` (`:337`). Seule la boîte
  de configuration l'évite (`wx-config.c:136-137`, qui ramène à 0 *dans l'interface*) ; un `.cfg` lu à la main n'a
  pas ce garde-fou. Candidat (section B, comportement indéfini, effet : un plantage ou un device lu au hasard),
  hors de la portée de GR.4, sans le proposer à corriger. Le C# lève une exception au même endroit (`video.cs:209`,
  commentaire) : l'écart est de forme seulement.
- **A4. `make bench` : les objets examinés ne sont pas ceux du lien.** Le lien de `bench` prend `build/lto/*.o`
  (`Makefile:210-213`, `-flto`), dossier **absent** ; R4 a compté sur `build/*.o`, ceux de la `.so`, datés du
  08-09/10. Même source, donc mêmes symboles, mais deux réserves à écrire : (1) les objets précèdent `cb69404`
  (10/10), qui a touché `harness.c` ; (2) sans `lto`, `nm` ne lit pas le code intermédiaire d'un objet `-flto`.
  Recompté ici : 95 symboles non définis par les objets C hors `__real_*` et libc ; 17 sont définis par
  `harness_dbopl.o` et `harness_cdrom.o` (10 + 6 + 1) ; les 78 autres sont libc, `__real_*` (`--wrap`) ou
  `_GLOBAL_OFFSET_TABLE_`. Le chiffre 17 tient comme *déduit*, pas comme *documenté*.
- **A5. Aucun automate ne vérifie le manifeste, et il n'y a pas de CI.** `.gitignore:11-14` dit « pour que la CI
  vérifie ce qu'elle a » ; `.github/` n'existe pas, et rien dans `tools/` ni `series.sh` n'appelle `sha256sum -c`
  (`grep -rn 'roms.sha256\|check-oracle' --include=*.sh --include=*.yml`) : seulement `check-oracle.sh` lui-même.
  `roms/*` étant ignoré (`git ls-files roms` = 1 fichier), le manifeste est la seule trace suivie des ROM. R4
  l'écrit en risque n° 5 ; il faut y ajouter que ni `check-oracle` ni `roms.sha256` n'est dans `series.sh`.
- **A6. La dérive, détail.** `./tools/check-oracle.sh` rend « 212 transcrit(s) vérifié(s), 41 fichier(s) hôte
  ignoré(s), 3 dérive(s), 0 absent(s), arbre vendoré OK » : trois **lignes** du manifeste (`oracle.tsv:15` et
  `:129` pour `harness.c`, `:39` pour `harness.h`), deux fichiers. Les empreintes du manifeste (`5e2001e3…`,
  `31380e23…`) sont exactement celles de `harness.c` et `harness.h` à `02244e6` (`git show 02244e6:… | sha256sum`),
  les réelles (`2637cf98…`, `ec788a4b…`) celles de `cb69404` ; `git log` n'a pas d'autre commit sur ces deux
  fichiers. La **convention du projet** est de réancrer dans l'étape qui touche le harnais (`VERIFICATION.md:6581`
  « harnais réancré », `:6656` idem) : `cb69404` l'a rompue. Conséquence pour GR.4e : une seule des trois lignes
  est à relire sur le fond (le diff de `cb69404` sur `harness.c`, ABI comprise) avant `--fix`, qui réécrit tout.
- **A7. `Host/NvrImage.cs:229-292`** lit `gfxcard` avec le défaut « cga » et en tire les bits d'affichage du CMOS
  (`gfx == "cga" ? … : gfx is "mda" or "hercules" ? … : DisplayOwnRom`). Une clé `builtin` donnerait `DisplayOwnRom`.
  Sans conséquence : la M24 et le PC1512 n'ont pas de CMOS d'AT (*déduit*, `--make-nvr` ne vise que les AT) ; à
  noter dans `r9-gfxfixe` pour que personne ne l'étende.

Vérifié sans rien à dire : les citations `pc.cs:357/402/1059/1120`, `video.cs:503-523`, `SdlSetup.cs:315-337` et
`:561-563`, `model.cs:86/354/369/386`, `wx-config.c:117-124`, `harness.c:1914-1922`, `:2138-2165`, `:2224`,
`pc.cs:1275-1290`, `mouse_ps2.cs:48`, `keyboard_at.cs:155`, `oracle.tsv:216`, `PLAN-G10.md:3-6`, `:289-293`, les
plages de `PLAN-G8/G9`, `UsageText.cs:20/27-37`, `README.md:3/120-129`, `TRANSCRIPTION.md:200/217/222`.

## 5. Les décisions ouvertes, jugées

Règle de Julien (18/10) : une question seulement si ses décisions la laissent ouverte.

1. **`builtin`.** *Vraiment ouverte, et la seule.* PLAN.md `:38-39` (n° 5) tranche « hors liste : ramené ou refusé,
   avec un avertissement » ; il ne dit pas si le nom que PCem écrit est une valeur hors liste. La proposition de R4
   (accepté en silence sur ces deux machines, avertissement pour toute autre carte) est la bonne. À reformuler avec
   K4 : le choix de fond est « -1 comme PCem + 0 poussé à l'oracle » ou « CGA interne ». *Proposé* : la seconde, la
   moins risquée pour le mode PCem ; le -1 reste atteignable plus tard sans changer le format du fichier.
2. **Phase 2 : rejouer ou se récuser.** *Pas une décision.* PLAN.md `:516-518` (GR.4) range « la phase 2, qui ne
   rejoue ni `--type` ni `--fdb` » dans la dette à corriger : rejouer est le mandat. Le repli sur `--lockstep`, si
   la preuve de budget échoue, est une conséquence de l'étape et se constate, il ne se demande pas.
3. **Vidage brut pour la CGA, la MDA, l'Hercules.** *Pas une question à Julien.* GR.4 nomme l'EGA seule ; « ne pas
   fuir en avant » donne la réponse (l'EGA seule). Et K5 la vide pour la MDA et l'Hercules, déjà innocents. Reste
   une ligne dans « Ce que ce vert ne dit pas » pour la CGA.
4. **Le champ *Effet* de PB-64, 66, 24.** *Pas ouverte.* GR.4 ne nomme que 97, 99, 100, mais l'uniformité d'un
   registre dont le recensement (`Recensement.cs`) exige déjà *Corrigé* ou *Non corrigé* est de la même veine que
   « les totaux ». À faire en GR.4e sans demander ; **ne pas** faire exiger *Effet* par `recensement` : c'est un
   changement de porte, donc une étape à part (déduit : `Recensement.cs` ne teste pas *Effet* aujourd'hui,
   `grep -n Effet` vide).
5. **Le compte de lignes (`:8150`).** *Pas ouverte* : « les totaux » est dans GR.4. Retirer le chiffre (K1) ou le
   remplacer par la somme sans doublons des sources C/C++ seules (45 086 distinctes + méthode écrite).

Une question à ajouter, que R4 laisse implicite : **GR.4e réancre `oracle.tsv` après un harnais dont les
commentaires changent.** C'est mécanique (A6, risque n° 6 de R4), pas une décision.

## 6. Ce qui reste inconnu

- Les 17 symboles sur les objets d'aujourd'hui (A4) : il faudrait construire.
- La licence de `nukedopl.cpp` (A1) : l'en-tête n'en porte pas.
- La lecture de B800 par la M24 et le PC1512 (2.3), sans cycles ou avec.
- Le sort de `egabanc.attendus` si le vidage ne coûte plus de cycles : seule la série le dira (R4 risque n° 1).
