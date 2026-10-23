# G14, reconnaissance — R1-contre : la contre-lecture de l'inventaire des puces

Contre-lecture du 23 octobre 2026, au commit `1d49a7f`, de `R1-puces.md`. Lecture seule : rien construit, rien lancé ;
les comptes viennent de `grep`, `git grep`, `awk` et de scripts Python jetables (`python3 -I`) hors du dépôt, dans le
dossier de travail de la séance. Classement de chaque constat : **exact**, **imprécis** (ce qui est juste suit) ou
**faux** (la preuve suit). Niveaux : vérifié (relu), déduit, inconnu.

## 0. En bref

1. R1 est solide : sur une cinquantaine de constats relus, aucun n'est faux dans sa conclusion ; les chiffres
   principaux (226 fichiers, 217 citations, 69 fichiers, 168 appels, 108 méthodes, 5 574 mentions) se reproduisent.
2. Le script de l'annexe B sous-compte : il ne relève que les membres `static`, pas les `const` (qui sont statiques
   par nature). Avec eux : iXtal26/ 235 sites (et non 233), iXtal26.Diff 193 sites dans 12 fichiers (et non 174
   dans 11) ; `dma.DMA_NODATA` (9 sites) et les constantes d'`sound_emu8k` (12) manquaient.
3. `ppi_c` est retenu à tort, ou du moins en contradiction avec R1 : le module est 19 lignes (`pa`, `pb`, `s2`,
   `ppi_reset`) et R1 écrit lui-même que « le 8255 n'est pas modélisé en tant que tel ». À sortir des quatorze, ou
   à poser comme question.
4. Deux attributions sont plus pauvres que R1 ne le dit : `serial` (le dépôt écrit « le 8250 » dans `oracle.tsv:211`,
   PCem parle de 16450 sans FIFO dans `f82c710_upc.c:287`) ; `nvr` (il porte aussi `nvrfopen`, utilisé par l'EEPROM
   de l'AHA-1542C, `scsi_aha1540.cs:2076`).
5. Les références de `headland` (« HT18 ») et de `neat` (« Chips & Technologies NEAT ») ne viennent que des en-têtes
   d'iXtal26 : ni PCem, ni `sources/`. « vérifié (iXtal26) » est circulaire ; c'est inconnu.
6. R1 oublie une règle qu'`Ics/` viole : `TRANSCRIPTION.md:132`, « Fichiers : miroir du chemin C ».
7. La simulation des lignes de plus de 120 colonnes ne regarde que les lignes qui le deviennent ; or le contrôle
   (`git diff -U0 | grep '^+'`) compte aussi les lignes déjà longues que le renommage touche : `pc.cs:1479` (136),
   `TimerCheck.cs:576` (121), `EmuTablesCheck.cs:53-61`, `MaterielCas.cs:3682`.
8. Le risque 2 (glob de `.editorconfig`, construction qui échoue) a une réponse dans le dépôt : six répertoires de
   code transcrit n'y sont pas et construisent ; `Ics` dans le glob est utile, pas nécessaire.
9. Décisions : D1, D2, D3, D5, D6 sont vraiment ouvertes ; D4 est tranchée par la convention du dépôt ; D7 n'est pas
   une décision pour Julien. Il manque une question : le sort de `ppi` (et, dans D5, des cœurs UC, à poser nommément).
10. Les six étapes sont justes et commitables ; deux ajouts : l'exécution du renommage (outil et option) et la
    `.so` comparée sans débogage si le harnais bouge.

## 1. Constat par constat

### 1.1 Les comptes de départ (R1 § 1.1)

- 226 fichiers `.cs` ; 37 dans `Host/`, 55 dans `Cpu/`, 44 `*.Materiel.cs` : **exact**.
  `find iXtal26 -name '*.cs' -not -path '*/bin/*' -not -path '*/obj/*' | wc -l` = 226 ; par répertoire 37 et 55 ;
  avec `-name '*.Materiel.cs'` : 44.
- `Ics/` absent : **exact** (`ls iXtal26`).
- Répertoire = namespace, sauf `Materiel/`, `GlobalUsings.cs`, `Program.cs` : **exact**. Une boucle `find` +
  `grep -m1 '^namespace'` comparée au répertoire ne sort que ces quatre fichiers.
- La règle de nommage (`rtc.cs:28-31`, `TRANSCRIPTION.md:131`) : **exact**, relue.
- Le tableau § 1.2 classe les 226 fichiers : **imprécis**. `Cdrom/` (6 fichiers : `cdrom-image*.cs`,
  `cdrom-ioctl.cs`, `cdrom-null.cs`, `cdrom_image*.cs`) n'y figure pas ; aucune puce dedans, mais la liste n'est pas
  close.

### 1.2 La table des modules et les puces (R1 § 1.2, 1.3)

Les en-têtes et lignes cités ont été relus : `pit.cs:5`, `pit.Materiel.cs:9`, `dma.cs:5-7`, `dma.Materiel.cs:9`,
`dma.cs:452`, `pic.cs:18`, `:35`, `:103`, `pic.Materiel.cs:9`, `keyboard_at.cs:5`, `:9`, `:542-545`, `ppi.cs:17-21`,
`rtc.cs:10`, `808x.cs:46`, `cpu.cs:30-41`, `cpu_tables.cs:84-88`, `nvr.c:802`, `rtc.c:1-2`, `opti495.c:1`,
`opti495.c:53-57`, `dbopl.cpp:20`, `sound_sb.cs:38/50/73` et `:647/731/877`, `vid_unk_ramdac.c:4-7`,
`vid_unk_ramdac.Materiel.cs:8`, `vid_tkd8001_ramdac.c:1`, `sound_emu8k.c:564` : **exacts**. Le décompte « 20 types
dans `sound_emu8k.cs` » est exact (21 `class`/`struct` dont le conteneur). Les 17 fichiers de nature P sont bien 17.
Les 14 + `rtc` + `dbopl` + `sound_dbopl` : exact.

Les constats qui appellent une réserve :

**`ppi_c` / `Intel8255` — retenu à tort (écart avec R1 lui-même).** `iXtal26/ppi.cs` fait 19 lignes de code : la
classe `PPI` (`s2`, `pa`, `pb`, `ppi.cs:17-21`), l'instance `ppi` et `ppi_reset`. Aucun registre de commande, aucune
logique de groupe, aucun port : les ports 60h-62h sont dans `keyboard_xt.cs` (`io_sethandler(0x0060, 0x0004, …)`,
`:244`, `:259`), `keyboard_amstrad.cs`, `keyboard_olim24.cs` et `keyboard_at.cs:545`. R1 le dit en § 1.3 b
(« Le 8255 n'est pas modélisé en tant que tel : deux verrous ») et le garde pourtant parmi les « quatorze modules qui
modélisent une puce » (§ 0 point 2, § 1.5). PLAN.md:547 : « les modules qui **modélisent** une puce ». Donner
`Intel8255` à trois champs affirme un modèle qui n'existe pas ; pire, `ppi.pb` est écrit par `keyboard_at` (le 8042
écrit « le port B du PPI »). Niveau : vérifié. Voir D5.

**`serial` — « un 16550, déduit » : imprécis.** (i) R1 écrit « Ni PCem ni iXtal26 ne nomment la puce » (§ 1.3 a) ;
or `oracle.tsv:211` note « M21 : le 8250 de COM1 et COM2 », et `Docs/ibm5150/README.md:52-53` (S1, S2) traite
l'INS8250 pour `Docs/ibm5150/` ; (ii) PCem nomme le 16450 sans FIFO : `f82c710_upc.c:287` (« serial ports must be 16450
without fifos »), avec `serial1_set_has_fifo(0)` (`:280`). Le module est donc une famille réglée par `has_fifo`
(16450 sans FIFO, 16550 avec) ; `common_init` ne l'appelle qu'avec `has_fifo = 1` (`model.cs:441-442`, comme
`model.c:198-199`), de sorte que, **dans ce qui est transcrit**, c'est un 16550 ; mais la machine de référence de
G14 est le 5150, dont l'UART d'origine est un INS8250 (fiche S1, `sources/`). Le nom `National16550` est défendable,
non « déduit seul » ; il faut le dire dans le bloc `// noms:` (R1 le dit au risque 7, pas au § 1.3 a).
`PLAN-CARTE-IDE.md:37` parle de « deux UART 16450 sans FIFO » pour la carte fille : une troisième lecture.

**`nvr` — « nvr = MC146818 » : imprécis.** Exact pour les ports 70h-71h, l'horloge, la CMOS. Mais `Devices/nvr.cs`
est aussi le service de fichiers de toutes les NVRAM : `nvrfopen` (nvr.c:48-50, `paths.cs:38-39`) est appelé par
`Scsi/scsi_aha1540.cs:2076` et `:2089` et par `scsi_aha1540.Materiel.cs:47` pour l'EEPROM de l'AHA-1542C
(`oracle.tsv:35` : « G11.0 : nvrfopen internal »), et `loadnvr`/`savenvr` portent le nom de fichier par machine
(un `switch (romset)`, `nvr.cs:6-11`), dont `pc1512.nvr`. `Motorola146818` serait donc le nom d'un fichier dont une
partie n'est pas la puce. À mettre dans le bloc `// noms:` (comme R1 le prévoit pour `rtc`) ; ne change pas la
proposition.

**`rtc` et le MC146818 (§ 1.3 b, D6) : imprécis.** R1 : « PCem nomme la puce deux fois, et pas la même ». `rtc.c:1-2`
dit bien DS12C887, mais `rtc.c:6` en donne aussi la fiche du **MC146818A** (`MC146818A_RTC_1984.pdf`) et
`rtc.h:68` dit « the MC146818A » ; le DS12C887 est un MC146818 compatible. Le troisième argument de D6 (« PCem
attribue `rtc.c` à une autre puce ») est donc faible ; les deux autres (initialisation des statiques, suffixe
inventé) tiennent. Annexe A.6 de R1 est à nuancer de même.

**`headland` « HT18 » : « vérifié (iXtal26) » est circulaire.** La seule occurrence de HT18 dans tout le dépôt hors
`pcem-dev/` est `headland.cs:10` (`git grep -n -i HT18 -- . ':!pcem-dev'`) ; PCem n'a que « headland » (`headland.c`,
`model.c:482`) et `sources/` n'a aucune fiche. Ce qui est vérifié est le commentaire de l'auteur, pas la puce.
Même chose pour `neat` : PCem dit « the chipset used in the AMI 286 clone model » (`neat.c:1`), et
« Chips & Technologies NEAT » est l'ajout du commentaire (`neat.cs:9-11`). Niveau : inconnu pour la référence,
vérifié pour le nom du fichier PCem. R1 le dit pour `neat` et `ali1429` (« inconnue »), pas pour `headland`.

**`opti495`** : exact (R1 le note : le commentaire décrit le 82C493, `opti495.c:53-57`).

**Cohérence de la règle de D3.** « La référence porte déjà le fabricant » justifie `Emu8000` (pas de `Creative` ou
`Emu`) ; la même raison s'applique à `TKD8001` (TKD = Trident) qui devient pourtant `TridentTKD8001`. Une règle ou
l'autre : voir D3.

### 1.3 Le 6845 (R1 § 1.4)

**Exact.** Aucun module CRTC : `grep -rn 'class .*6845\|class .*[Cc]rtc' iXtal26 --include=*.cs` ne rend que
`Masque6845` (`vid_mda.Materiel.cs:38`). Cinq cartes portent chacune `crtc[32]` + `crtcreg` (`vid_cga.cs:38-39`,
`vid_mda.cs:33-34`, `vid_hercules.cs:34-35`, `vid_pc1512.cs:43-44`, `vid_olivetti_m24.cs:34-35`) ; trois copies du
masque (`vid_cga.c:16`, `vid_pc1512.c:45`, `vid_olivetti_m24.c:41` et leurs `.cs` `:85`, `:72`, `:65`) ; la MDA et
l'Hercules n'en ont pas. EGA et SVGA ont leur CRTC (`vid_ega.cs:41-42`, `vid_svga.cs:64-65`, 128 registres).
`Lignes6845` à `SdlHost.cs:929` ; `LigneBornee` à `video.Materiel.cs:28`. `Masque6845.largeur` n'a qu'un lecteur
hors de la MDA : `vid_hercules.Materiel.cs:44`.

Imprécisions mineures : `cga_poll` finit à la ligne 631 et `mda_poll` à 377 (R1 : 633 et 379) ; l'EGA est nommé
parmi les « cartes à 6845 » à `video.Materiel.cs:6-7` (R1 : `:4-5`, qui est la phrase « cartes à 6845 ») — annexe A.3
correcte sur le fond.

**Conséquence pour « le CRTC → Motorola6845 »** (PLAN.md:548) : bien vue par R1. Une précision : de la liste
de quatre noms que Julien donne, c'est la seule dont le module n'existe pas ; le texte de PLAN.md est donc à
amender par lui quelle que soit la voie (D2). Le constat de R1 est le bon, la proposition (a) aussi.

### 1.4 `pit`, le 8254 et le nom `Intel8253`

**Exact, et plus fort que R1 ne le dit.** `pit.c:328` (`if ((val & 0xC0) == 0xC0)`) n'est gardé par aucune
machine ; `pit.cs:449` le transcrit ; `grep -n 'AT\|romset' pcem-dev/src/models/pit.c` : seulement la ligne 1. Le PC
et le XT portent un 8253-5 (`PCEM_BUGS.md:150-153`, IBM PC TR p. 2-22). Complément : `pit.Materiel.cs:9` cite la
fiche **du 8254** d'Intel (231164-005) : le dépôt a donc déjà choisi, pour le mode matériel, de traiter le module
comme un 8254 ; `oracle.tsv:153` (« le 8253/8254 du 5150 ») et l'en-tête `pit.cs:5` datent d'avant le
rafraîchissement AT (`pit_refresh_timer_at`, transcrit depuis B3). Le nom décidé `Intel8253` dit moins que le code ;
c'est exact que ce n'est pas une décision ouverte (Julien a nommé le 8253), mais l'en-tête et le bloc `// noms:` de
`Intel8253` doivent dire « 8253-5 du 5150, 8254 des AT » : à ajouter à G14.1.

### 1.5 Les comptes (R1 § 1.9, annexe B)

Commande : le script de l'annexe B extrait tel quel (hors les lignes de clôture Markdown), `python3 -I compte-g14.py
DEPOT <les seize classes>`.

- 69 fichiers et 233 sites (iXtal26/) ; 11 fichiers et 174 sites (Diff) : **exact pour le script, mais sous-compte**
  (voir plus bas). Le script rend bien 69/233 et 11/174.
- Le détail par classe (`pic` 28/56, `pit` 30/98, `dma` 10/11, …) : **exact**, tout est reproduit.
- Les trois décidés, 51 fichiers/165 sites et 7 fichiers/113 sites : **exact** (56+98+11 ; 80+9+24).
- perfbanc et SoftFloat sans site : **exact** (script, et `grep -nw` sur `tools/perfbanc/*.cs`).
- 217 citations de chemins : **exact**. `git grep -h -o -E "…(\.Materiel)?\.cs" -- . ':!pcem-dev' | wc -l` = 217 ; la
  ventilation (48, 31, 24, 33, 8, 9, 3) est reproduite.
- 24 lignes d'`oracle.tsv`, dont 6 pour les trois décidés : **exact** (`pic` 2, `pit` 1, `dma` 3).
- `picint` 27 lignes dans 18 fichiers et 20 dans tools ; `dma_channel_read` 10/6 ; `dma_channel_write` 8/5 et 22 :
  **exact** (`grep -rnw --include=*.cs`, commentaires exclus).
- `picintc` 21 lignes dans 14 fichiers : **imprécis**. 21 lignes, mais **13** fichiers en code (14 en comptant un
  commentaire : `ide.Materiel.cs:44`).
- `pic_` 106, `pit_` 50, `dma_` 70 : **exact** (`grep -rnw --include=*.cs … iXtal26 tools | wc -l`).

**Le sous-compte du script.** Son relevé des membres statiques (`MEMBRE`) exige le mot `static` ; les `const`
(statiques par définition) en sont exclus, tout comme `new` et `PCJR`, relevés à tort. Preuve : `dma.DMA_NODATA` est
`internal const int` (`dma.cs:44`) et n'est pas dans l'ensemble relevé de `dma`. Avec `(?:static|const)` :

- `dma` : Diff 24 → 33 sites ; `sound_emu8k` : iXtal26 6 → 8, Diff 33 → 43 (un cinquième fichier) ;
- bilan : **iXtal26 233 → 235 sites (69 fichiers)** ; **Diff 174 → 193 sites, 11 → 12 fichiers**.

Aucune autre classe ne change (`diff` des sorties). Les types imbriqués (`sound_emu8k.<type>`) n'ont pas de site
qualifié dans le code. Le renommage mécanique de Rider ne dépend pas du script ; mais R1 présente ces comptes comme
la mesure du travail, et la ligne « Les seize, union » est à corriger. Les limites que R1 annonce (chaîne contenant
`//`, déclaration sur plusieurs lignes) : non observées.

**Les lignes de plus de 120 colonnes (§ 1.9, fin).** La simulation de R1 (14 lignes, dont 8 de vrais sites) est
reproduite : 11 lignes d'iXtal26 (`808x.cs:768`, `:4091`, `808x.Materiel.cs:444`, `:448`, `nvr.cs:264`, `pit.cs:280`,
`:340`, `:436`, `serial.cs:86`, `:91`, `:96`) + 3 de `MaterielCas.cs`, soit 14 ; les longueurs avant substitution
sont 110 et 109 pour `808x.cs` (R1 : 111 et 110, probablement une erreur de décompte d'une colonne). Mais elle est
**incomplète** : le contrôle du dépôt est `git diff -U0 | grep '^+'` (mémoire « Lignes ≤ 120 : bloquant ») et bloque
toute ligne ajoutée de plus de 120 caractères, y compris celles qui l'étaient déjà. Lignes déjà longues touchées par
le renommage (code, hors variables locales) : `pc.cs:1479` (`Models.pit.setpitclock(…)`, 136), `TimerCheck.cs:576`
(121), `tools/iXtal26.Diff/EmuTablesCheck.cs:53`, `:54-57`, `:60-61` (126 à 159, `sound_emu8k.…`),
`MaterielCas.cs:3682` (121). Ces lignes devront être recoupées dans le commit, ce que la parité de lignes de R2 ne
voit pas pour `tools/`. À ajouter à R1 (risque 5).

### 1.6 La convention `// noms:` (R1 § 1.6)

**Exact** : les cinq blocs et leurs bornes (`fdc.cs:10-49`, `fdd.cs:9-23`, `disc.cs:10-28`, `disc_img.cs:9-19`,
`disc_sector.cs:9-25` : la ligne suivante est vide dans chaque cas) ; le format de `TRANSCRIPTION.md:23-29` ; « Les
FONCTIONS gardent toutes les noms de PCem » (`fdc.cs:47`, la faute est celle de l'original) ; les étiquettes de
`BootDiff.cs:1898`, `:2091` et la phrase `fdc.cs:48-49` ; `wc -l TRANSCRIPTION.md` = 237 pour un plafond de 260 (R3).

**Imprécis / manquant** :

- L'exemple de bloc pour `Intel8259A` met la ligne `pic.c Intel8259A` en première colonne : dans `Floppy/` le
  conteneur renommé reçoit le suffixe `_c` (« `fdc_c` = le conteneur du fichier, suffixe `_c` parce que PCem n'a pas
  de classe et que `fdc` est pris par l'instance », `fdc.cs:45-46`). En G14 le conteneur prend le nom de la puce et
  la globale `pic_` n'a plus de raison d'être ; l'exemple est cohérent avec D1, pas avec le précédent. À signaler.
- **Omission** : `TRANSCRIPTION.md:132`, « Fichiers | miroir du chemin C : `src/cpu/808x.c` → `Cpu/808x.cs` ». Les
  puces de `src/models/` (`pic`, `pit`, `dma`, `serial`, les 4 jeux), de `src/devices/` (`nvr`), de `src/keyboard/`
  et de `src/sound/`, `src/video/` quitteraient leur miroir pour `Ics/`. R1 prévoit d'amender `:23`, `:131` et
  `:153-165` ; `:132` manque. La règle est déjà écornée (`Sound/dbopl.cs` ← `src/dosbox/dbopl.cpp`), mais l'écorner
  n'est pas l'abroger : l'amendement est à écrire, avec son coût dans le plafond de 260 (23 lignes de marge).
- R1 propose le bloc dans le fichier transcrit seulement (D7). Les `*.Materiel.cs` portent `ORACLE: (aucun)` : accord.

### 1.7 La portée selon PLAN.md (R1 § 1.7)

Les numéros de ligne de PLAN.md (`:547-549`, `:548`, `:549`, `:551-552`, `:553-554`, `:556-557`, `:566-567`, `:569`,
`:586-588`, `:589`) : tous exacts. Deux compléments : (1) l'annexe de PLAN.md (« Les outils de G14/G15 ») nomme
`DOTNET_JitDisasm` comme « la preuve directe du 0 % » : « code identique après renommage » ; `listings-jit.sh compare`
est donc l'outil attendu, la table de correspondance de G14.0 en est l'adaptation, pas un élargissement ;
(2) `Docs/G13-reconnaissance/D7-contre.md:322` écrit « G14 et G15 renommeront `materiel`, `pb_nn` et les
`*.Materiel.cs` ». R1 (annexe A.4) le range en G15 ; c'est le bon rangement (PLAN.md ne vise que les puces), mais
la divergence entre deux documents du dépôt mérite une ligne.

### 1.8 Ce que le harnais et les sondes désignent par nom (R1 § 1.8)

**Exact.** `HState.cs:146-147`, `BootDiff.cs:1898` et `:2091`, `MaterielCasPit.cs:52`, `:55`, `MaterielCas.cs:513`,
`GardeBanc.cs:93`, `386.Materiel.cs:314`, `MaterielCasFrequence.cs:85`, `MaterielCasLigne.cs:20` relus ;
`git grep` des `Type.GetType("`, `Assembly.GetType` : aucun ; les 17 usages de réflexion sont les siens ; aucun
`CallerFilePath`. Les références de séries (`/var/tmp/ixtal-g13/series/*.tsv`) et `tools/gates/` ne contiennent
aucun nom de classe candidate ; aucun fichier non `.cs` suivi ne cite `iXtal26.Models.<classe>` hors
`Docs/GR-reconnaissance/R1-contre.md`. `check-oracle.sh` : le message « ABSENT C# » est à la ligne 51 (R1 : 50-51).
`listings-jit.sh` : 168 lignes `call … iXtal26.Models.pic:picint(ushort)`, 108 méthodes, 5 574 mentions, relus sur
`/var/tmp/ixtal-g13/M2-13b` ; le découpage par en-tête de méthode (`:178-217`) est bien celui décrit.

### 1.9 Les risques (R1 § 3)

- Risque 2 (glob de `.editorconfig`, construction qui échoue) — **imprécis, répondable**. Le glob (`.editorconfig:40`)
  ne couvre ni `Ide/`, ni `Scsi/`, ni `Mfm/`, ni `Lpt/`, ni `Joystick/`, ni `Cdrom/` : tous du code transcrit à
  membres `snake_case`, qui construisent avec `TreatWarningsAsErrors` et `EnforceCodeStyleInBuild`
  (`iXtal26.csproj:38-39`), avec pour seule suppression un `#pragma warning disable CS8981` par fichier (`ide.cs:24`,
  `scsi_hd.cs:18`, `lpt.cs:20`). IDE1006 n'est donc pas un échec de construction par défaut. L'ajout de `Ics` au glob
  reste bon (cohérence, et `ppi` en sort de `:97`), mais ce n'est pas un piège. Niveau : déduit du précédent.
- Risque 4 (fusionner `nvr` et `rtc`) : exact (déduit). Complément, déduit : l'ordre de compilation des fichiers d'une
  classe partielle est conservé (`X.Materiel.cs` < `X.cs` en ordre ordinal, avant comme après).
- Risque 9 (historique `git mv`) : plausible, déduit ; les `*.Materiel.cs` les plus courts sont à surveiller
  (la similarité tombe sous 50 % si l'en-tête et les `using` pèsent trop).
- **Absent** : le moyen d'exécuter « le refactor Rider ». Les outils `rename_refactoring` et `move_type_to_namespace`
  existent dans la session ; sinon, un script à partir de la liste des sites de l'annexe B. R1 ne dit ni l'un ni
  l'autre ni l'option (pas de renommage dans les commentaires et les chaînes), qui est le risque 1.
- La porte « la `.so` de l'oracle inchangée (sha256) » (G14.1) : imprécise. `VERIFICATION.md:15947-15948` montre que
  deux constructions d'un même code diffèrent par `-g` (numéros de ligne) et `.note.gnu.build-id` ; l'empreinte ne se
  compare qu'**après retrait du débogage**. Et le plus simple est de ne pas toucher `tools/oracle/*.c` (leurs mentions
  des classes sont des commentaires : `harness.h:458`).

## 2. Ce qui manque, et ce qui élargit G14

**Manque (à ajouter à R1).**

1. `TRANSCRIPTION.md:132` (voir § 1.6).
2. Les lignes déjà longues touchées par le renommage (§ 1.5).
3. `Cdrom/` dans le tableau (aucune puce).
4. Le moyen d'exécution du renommage et l'option commentaires/chaînes (§ 1.9).
5. Le sort de `ppi_c` (§ 1.2) et la nature de `nvr` (service de fichiers NVRAM).
6. Les cœurs UC à nommer dans D5 : `_808x` (8088, 8086) et `_386` (286, 386SX/DX, 486) sont les modules les plus
   proches d'une puce, ainsi que les tables 8087/287/387 (`cpu_tables.cs:84-88`). R1 les exclut (« plusieurs puces
   chacun », vérifié) en une ligne ; la raison est bonne, mais « et ainsi de suite » (PLAN.md:549) ne les exclut pas, et
   renommer `_386` toucherait 28 fichiers `386_ops_*.cs` du chemin chaud : c'est à Julien de le dire.
7. La nuance du MC146818A dans `rtc.c` (§ 1.2).
8. L'identité d'`Intel8253` comme 8253-5/8254 dans les en-têtes (§ 1.4).

**Puces examinées et correctement écartées par R1** (vérifié) : `sound_opl`/`dbopl`/`sound_dbopl` (YM3812 et
YMF262 dans un moteur, `dbopl.cpp:20`) — l'argument « comme `softfloat` » est toutefois faible (`iXtal26.SoftFloat` est
un projet à part, réécrit ; `dbopl` est un fichier transcrit de `src/dosbox/`) ; les mélangeurs CT1335/1345/1745
(en ligne dans `sound_sb.cs`, `grep -i ct1335` : seulement `sound_sb.c` et `sound_azt2316a.c` côté PCem) ; le DSP ;
le haut-parleur ; le port jeu (`grep -rn -i '558\|555'` : rien) ; `nmi` ; le FDC (rangé parmi les cartes par
PLAN.md:549) ; l'IDE, l'XTIDE, le Xebec, l'AHA (des cartes, au sens de PLAN.md) ; le MPU-401 ; `rtc_tc8521` et les
super-E/S (non transcrits). Aucune puce n'est oubliée au sens de PLAN.md.

**Ce qui élargirait G14 au-delà de PLAN.md.** Rien de nouveau dans les étapes de R1 ; à garder sous contrôle :
`ChipsCS8221` et `AliM1429` « si la fiche le confirme » (G14.4) font de G14 une recherche documentaire — aucune fiche
n'est dans `sources/` ; D2 (b) (module neuf) est hors de la porte et R1 le dit ; la variante de D1 (rendre `pic_` →
`pic`) est un renommage de membre que G15 reprendra (`Pic`), donc du travail double.

## 3. Les décisions ouvertes, revues

- **D1, la portée (module seul ou membres) : vraiment ouverte, mais elle penche.** Accord avec « le module seul ».
  PLAN.md:551-552 (« traduire chaque appel (`picint`…) ») et le précédent de `Floppy/` (des tables de champs) tirent
  de l'autre côté ; le PascalCase des membres en G15 (`PLAN.md:566`) et la porte « seuls des noms changent »
  arbitrent. À poser à Julien en une phrase, sans la variante `pic_` → `pic`.
- **D2, le 6845 : ouverte.** Accord avec (a). La phrase de PLAN.md:548 repose sur un module qui n'existe pas ;
  Julien doit l'amender.
- **D3, la forme des noms : ouverte en partie.** Fixes, par la forme des exemples : `Intel8042`, `Motorola146818`.
  Ouverts : `Emu8000` contre `TridentTKD8001` (une seule règle pour un fabricant déjà dans la référence : `EMU8000`
  chez E-mu, `TKD8001` chez Trident), `Intel8255` (si `ppi` reste), `National16550` contre un nom de famille,
  `SierraSC1502x` contre `SierraSC15025`. Proposition : « fabricant + référence imprimée », sans omettre le
  fabricant quand la référence l'abrège (`TridentTKD8001`, `EmuEMU8000` ou `CreativeEMU8000`), à poser ensemble.
- **D4, le namespace : non ouverte.** Tranchée par la convention : 222 des 226 fichiers ont namespace = répertoire
  (exceptions : `Materiel/`, `GlobalUsings.cs`, `Program.cs`). `iXtal26.Ics` : une ligne de G14.0, pas une question.
- **D5, les puces au-delà des trois : ouverte.** À reformuler : (i) `ppi_c` hors liste, ou posé en question ;
  (ii) les cœurs UC et x87 : nommément « non retenus », pour confirmation ; (iii) l'OPL : accord, exclu ;
  (iv) `neat`, `ali1429` et `headland` : leurs noms de puce ne sont pas dans le dépôt — « les laisser à G15 » les
  ferait passer en `Neat` et `Ali1429`, jamais en nom de puce ; dire cette conséquence à Julien.
- **D6, `nvr` et `rtc` : ouverte.** Accord (`rtc` reste), avec l'argument du MC146818A de `rtc.c:6` corrigé et la
  mention du service `nvrfopen`.
- **D7, les documents datés et le bloc des partiels : non ouverte.** Pratique constante du dépôt (les journaux datés
  ne sont pas réécrits, `VERIFICATION.md` donne la table) ; le bloc dans le seul fichier transcrit découle de
  `TRANSCRIPTION.md:23-29` (les partiels n'ont pas d'`ORACLE`). À écrire dans l'étape, pas à demander.

**Question manquante** à ajouter : « `ppi.cs` (19 lignes, un état de trois champs) prend-il le nom `Intel8255` bien que
le 8255 ne soit pas modélisé ? » Proposition : non, il reste à la racine avec `io`, `pc`, `timer` (D6 pour `rtc` suit la
même logique).

Les étapes G14.0 à G14.5 : la découpe est juste (outils/règles ; trois puces décidées ; carte mère ; cartes ;
jeux de puces ; clôture), chacune commitable avec sa série. À ajouter : G14.0 écrit l'amendement de
`TRANSCRIPTION.md:132`, la liste des lignes à recouper d'avance, le choix de l'outil de renommage ; G14.2 sans
`ppi_c` si la réponse ci-dessus est non ; G14.1 dit « 8253-5 / 8254 » dans l'en-tête de `Intel8253`. Le préalable
(la référence de performance figée de PLAN.md:586-588) reste hors de G14.

## 4. Ce qu'il faut corriger dans R1 avant le plan

1. § 1.9 et annexe B : compter aussi les `const` (regex `\b(?:static|const)\b`) ; chiffres corrigés : iXtal26 235
   sites (69 fichiers), Diff 193 sites (12 fichiers) ; `dma` Diff 33, `sound_emu8k` iXtal26 8 et Diff 43.
2. § 1.9 : `picintc` dans 13 fichiers (21 lignes) ; les colonnes de `808x.cs:768`/`:4091` sont 110 et 109.
3. § 1.9 et risque 5 : ajouter les lignes déjà longues touchées (`pc.cs:1479`, `TimerCheck.cs:576`,
   `EmuTablesCheck.cs:53-61`, `MaterielCas.cs:3682`) ; le contrôle est celui du diff entier.
4. § 0 point 2, § 1.5, D5 : retirer `ppi_c` des quatorze ou le poser en question (le module ne modélise pas le 8255).
5. § 1.3 a, `serial` : citer `oracle.tsv:211` (8250) et `f82c710_upc.c:287` (16450) ; le nom `National16550` couvre la
   famille réglée par `has_fifo`.
6. § 1.2, `nvr` : ajouter `nvrfopen`/`loadnvr` (AHA-1542C, `pc1512.nvr`) aux fonctions du module.
7. § 1.3 b, D6, annexe A.6 : `rtc.c:6` et `rtc.h:68` citent le MC146818A.
8. § 1.3 d : `headland` « HT18 » et `neat` « Chips & Technologies NEAT » : niveau inconnu (une source : l'en-tête
   d'iXtal26).
9. § 1.6 et G14.0 : ajouter `TRANSCRIPTION.md:132` (« miroir du chemin C ») ; dire que l'exemple de bloc diverge du
   précédent `_c` de `Floppy/`.
10. Risque 2 : le ramener à « déduit : non bloquant » par le précédent de `Ide/`, `Scsi/`, `Mfm/`, `Lpt/`,
    `Joystick/`, `Cdrom/` ; garder `Ics` au glob.
11. G14.1 : la `.so` se compare sans débogage (`VERIFICATION.md:15947-15948`), ou le harnais n'est pas touché ;
    nommer l'outil et l'option du renommage.
12. § 1.2 : ajouter `Cdrom/` au tableau ; § 1.3 f : `case 0x63` — le `grep` rend aussi `keyboard_at.cs:354` et
    `:579` (ports 60h-63h du 8042) ; pour le XT, `io_sethandler(0x0060, 0x0004, …)` (`keyboard_xt.cs:244`, `:259`)
    route 63h vers une fonction sans `case` : la phrase de R1 est juste sur le XT, fausse comme sortie de `grep`.
13. § 1.5, D3 : appliquer la même règle de fabricant à `Emu8000` et `TridentTKD8001`.
14. D4 et D7 : retirer de la liste des décisions de Julien ; D1 : retirer la variante `pic_` → `pic`.
15. Annexe A.4 : citer `D7-contre.md:322`, qui range le renommage de `materiel` en G14 et G15.

## 5. Ce que cette contre-lecture ne dit pas

Rien n'a été construit : que les noms proposés compilent sans collision (aucun identifiant existant du même nom :
`git grep -w` des quatorze noms et de `Ics` : seulement `PLAN.md` et deux notes) est établi, que le refactor passe sans
erreur ne l'est pas. Le contenu des fiches de `sources/` n'a pas été relu ; l'identité réelle des puces `headland`,
`neat`, `ali1429` reste inconnue. La référence de performance figée (`R0-reference.md`) n'est pas examinée ici.
