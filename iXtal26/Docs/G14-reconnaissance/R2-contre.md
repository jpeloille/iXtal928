# R2 — Contre-lecture

*G14, reconnaissance, le 23 octobre 2026, au commit 1d49a7f. Lecture seule : rien construit, rien joué.*
*Cible : `R2-noms.md` (376 lignes). Niveaux : **exact**, **imprécis** (ce qui est juste), **faux** (la preuve).*
*Les comptes ont été refaits ; les scripts jetables vivent hors du dépôt (Python `-I`, shell).*

## 0. En bref

1. R2 tient dans l'ensemble : aucun faux de fond. Les thèses 1 à 6 sont **exactes** (relues et recomptées).
2. Cinq imprécisions de chiffres, sans effet sur les conclusions : 826 journaux (c'est 827), 14 journaux bruités
   (13 avec un autre normaliseur), 22 lignes `tools/oracle/` d'`oracle.tsv` (23), « 15 déclarations `PIT pit` »
   (14 paramètres et locales, plus le champ `PIT_nr.pit`), « 1 653 » journaux (1 654).
3. Oublis : `ixtal26-286.cfg:18` cite `neat.cs` ; la décision sur les **types d'état** (`PIT`, `PIC`, `PPI`,
   `SERIAL`, `dma_t`, `PIT_nr`) et sur les **deux conteneurs de `rtc.cs`** (`rtc_h`, `rtc`) n'est dans aucune
   décision ; l'**espace de noms** des types déplacés (`iXtal26` pour `ppi`/`rtc`, `iXtal26.Models`…) ; les
   frères de `pit_` (`keyboard_xt_`, `keyboard_amstrad_`, `keyboard_olim24_`) qui resteraient suffixés.
4. La réflexion par chaîne : exact, cinq sites (six avec `GetFields` de `MaterielCasFrequence.cs:85`, qui lit des
   *noms* de champs de `sound_sb`, pas de module visé). `GardeBanc.cs:93` vise `_808x`, pas G14, mais G15.
5. `.editorconfig` : exact, avec une réserve — des répertoires transcrits (`Ide/`, `Mfm/`, `Scsi/`, `Cdrom/`,
   `Lpt/`, `Joystick/`) sont **absents** du glob de `:40` et la construction passe : « `Ics/` oublié fait tomber
   le build » est une hypothèse (non démontrée), pas un fait. Les noms PascalCase ôtent d'ailleurs CS8981.
6. `check-oracle.sh` : exact. Seul le contrôle d'existence de `csharp_path` compte (32 lignes, recomptées).
7. La preuve (table + IL + normaliseur) est suffisante ; une plus simple existe pour l'IL : clé par nom
   qualifié trié, donc l'ordre de compilation (« inconnu ») cesse d'être un risque.
8. Décisions : D1 et D4 sont déjà tranchées par le dépôt (TRANSCRIPTION.md, PLAN.md) — à fermer ; D2 se règle
   par la règle écrite `TRANSCRIPTION.md:181-185`, mais élargit les noms touchés ; D3 : je confirme R2 ;
   D5, D6, D7 : ouvertes. Une D8 manque (types d'état, `rtc`, espaces de noms).
9. Faisabilité de l'outil : Rider `rename_refactoring` n'a ni option commentaires/chaînes ni analyse de
   conflit en `preview` (schéma de l'outil) ; Roslyn : le SDK embarque `Microsoft.CodeAnalysis.CSharp.dll`
   (`/usr/lib/dotnet/sdk/10.0.113/`), pas `Workspaces` : R2 a raison sur l'absence de `Renamer`.
10. À corriger dans R2 : § 4 de ce rapport.

## 1. Constat par constat

### 1.1 Les thèses du § 0 de R2

1. **Exact.** Ni harnais ni sondes ne désignent une classe C# par son nom. `grep` de `GetType(`, `Type.GetType`,
   `Activator`, `Assembly.`, `nameof`, `CallerFilePath` sur `iXtal26/` et `tools/` : seuls `e.GetType().Name`
   (exceptions), `typeof(X).Assembly.Location` (`PmCheck.cs:85`, `PmCheck386.cs:122`) et `typeof(pc).Assembly`
   (`Bench.cs:63`). Aucune chaîne ne contient un nom de conteneur visé ; les chaînes de sonde (`"n_picint"`,
   `PitFields`, `DiscFields`) sont les noms du C.
2. **Exact, chiffre imprécis.** 0 `.cs` dans les journaux : `cat /tmp/a9/series/gr1i2-*.txt | grep -c '\.cs\b'`
   rend 0 (et 0 pour `iXtal26\.(Models|Sound|Keyboard|Devices|Video)`). Mais les journaux sont 827 par série
   (`ls gr1i2-*.txt | grep -v '^gr1i2-tout-' | wc -l`), 1 654 en tout, non 826 et 1 653. `gr1i2.tsv` a 828
   lignes dont l'en-tête : 827 portes.
3. **Exact.** Cinq sites de réflexion, aucun sur un module de puce : `386.Materiel.cs:313-314` (`_386.ILLEGAL`),
   `MaterielCasPit.cs:51-55` (`sound_speaker`), `MaterielCasFrequence.cs:85` (`sound_sb`, filtre sur le suffixe
   `_iir_x` ou `_iir_y`), `MaterielCas.cs:513` (`_808x.memcycs`), `GardeBanc.cs:93` (`_808x.setadc8`).
4. **Exact.** Dépendances réelles (`using static`, qualifiées, globs, `csharp_path`, listings) : §§ 1.2 à 1.5.
5. **Exact.** Dangers du remplacement textuel : § 1.6.
6. **Exact.** `grep '^Project' iXtal26.sln` : `iXtal26` et `iXtal26.SoftFloat`. Un cinquième projet, hors solution
   aussi, existe : `tools/softfloat/Banc/iXtal26.SoftFloatBanc.csproj` (sans lien avec les modules visés ;
   déduit).
7. **Exact.** `tools/listings-jit.sh:188-203` (clé = nom qualifié) ; aucun script commité d'IL ni de
   comparaison de journaux dans `tools/`.
8. **Exact.** `/usr/lib/dotnet/shared/Microsoft.NETCore.App/` ne contient que `10.0.13` ; SDK `10.0.113`.
9. **Exact.** Motif de R2 sur `git ls-files` (hors `pcem-dev/` et `vectors/`) : `PCEM_BUGS.md` 51 ;
   `Docs/ibm5150/` 6+4+1 = 11 ; `TRANSCRIPTION.md` 3 ; `VERIFICATION.md` 37 ; plans clos : `PLAN-G12` 4,
   `PLAN-286` 4, `PLAN-PS2` 3, `PLAN-G3` 3, `PLAN-G9` 2, `PLAN-G1` 2 = 18 ; `Docs/G13-` 45 ; `Docs/GR-` 11 ;
   `THIRD_PARTY_NOTICES.md` 2 ; `iXtal26/README.md` 1.
10. **Imprécis.** Les décisions ouvertes : § 3.

### 1.2 Le tableau des modules (§ 1.1 de R2)

- `using static` : recompté `grep -rhE '^\s*using static iXtal26\.<ns>\.<cl>;' iXtal26 tools iXtal26.SoftFloat` :
  pic 13, pit 4, dma 4, ppi_c 4, rtc_h 2, keyboard_at 2, sound_opl 3, sound_dbopl 1, sound_mpu401_uart 1,
  vid_unk_ramdac 1, vid_tkd8001_ramdac 1 ; 0 pour serial, nvr, DBOPL, sound_emu8k, neat, headland. **Exact.**
  Note : la colonne « rtc 2 » compte `rtc_h`, pas `rtc` (0). Une ligne `using static iXtal26.ppi_c;` porte un
  commentaire de fin de ligne (`// le conteneur s'appelle ppi_c : …`) : un remplacement doit l'emporter aussi.
- `oracle.tsv` : `grep -cP '^iXtal26/<chemin>(\.Materiel)?\.cs\t'` sur la liste de R2 : **32**, et la ventilation
  par module de R2 est exacte (pic 2, pit 1, dma 3, ppi 1, rtc 1, nvr 1, kbd 2, serial 2, 4 chipsets 1 chacun,
  opl 1, dbopl 3, sound_dbopl 1, emu8k 3, mpu 3, unk 2, tkd 2). Les `.Materiel.cs` de `pit`, `emu8k`, `mpu401`,
  `unk_ramdac` n'ont pas de ligne (0 chacun) : **exact**. 39 fichiers `.cs` d'`iXtal26/` sans ligne :
  **exact** (`find` × `grep "^$f\t"`).
- « 22 lignes `tools/oracle/…` » : `awk -F'\t' '$2 ~ /^tools\/oracle\//' oracle.tsv \| wc -l` = **23**.
  **Imprécis** (un cas de plus ; sans conséquence).
- 23 espaces de noms : `grep -rn '^namespace'` rend 23 espaces distincts, dont `iXtal26` et
  `iXtal26.Host.CommandLine`. **Exact.**
- Les sept partielles : exactes. J'ai ajouté une vérification : aucune des sept n'a **aucun champ statique**,
  initialisé ou non (`grep` des déclarations `static … ;|=` hors méthodes : 0). Le risque « ordre des champs d'une
  partielle selon l'ordre de compilation » est donc nul pour ces sept ; R2 ne disait que « initialisé ».
- « 15 déclarations `PIT pit` dans `pit.cs` » : `grep -n '\bPIT pit\b' pit.cs` rend 16 lignes = 11 paramètres
  (`:131,181,188,211,221,286,346,358,635,649,663`) + 3 locales (`:444,570,628`) + le champ `PIT_nr.pit` (`:38`) +
  un commentaire (`:79`). **Imprécis** : 14 paramètres ou locales ; le 15e est un membre, qui ne se confond pas
  avec un paramètre mais qu'une renomination de `pit_` → `pit` croisera (`pit.cs:729`).
- `SERIAL serial` : 7 (`serial.cs`) + 3 (`mouse_serial.cs`) : **exact**. Locales `nvr` : 6 : **exact**.

### 1.3 `.editorconfig` et construction

- `.editorconfig:40`, `:97`, `:90-91` : **exact** (relus). `:97` liste `{io,timer,pc,ppi}` ; `rtc.cs`, qui est
  à la racine comme `ppi.cs`, n'y est **pas** : il s'en tire par `#pragma warning disable CS8981` (`rtc.cs:32`).
  Pas une erreur de R2, un détail à retenir quand `rtc` ira dans `Ics/`.
- **Réserve** : six répertoires du code transcrit (`Cdrom`, `Host`, `Ide`, `Joystick`, `Lpt`, `Mfm`, `Scsi`) ne
  sont pas dans le glob de `:40` et la construction est verte (`ide.cs:22-24`, `mfm_xebec.cs:23` : pragmas
  seulement). La conséquence d'un `Ics/` hors glob dépend des diagnostics que ses fichiers déclenchent
  (CS0649 en particulier, `:70`). R2 écrit « le build tombe (déduit) » ; c'est **plausible, non démontré**, et
  facile à lever (une construction d'essai après le premier `git mv`). Le glob reste à étendre : la raison est la
  doctrine (les mêmes règles que le code transcrit), non le risque d'échec seul.
- Avec les noms en PascalCase, **CS8981 ne frappe plus les conteneurs renommés** : le commentaire `:47-53`
  (« pit, pic, dma… ») devient faux à mesure que G14 avance. R2 le dit à moitié (« cite `pit, pic, dma` ») ;
  il faut l'inscrire dans la doctrine G14.2 : mettre ce commentaire à jour est une obligation, pas une option.
- `iXtal26.csproj:38-39` (`TreatWarningsAsErrors`, `EnforceCodeStyleInBuild`) et `:70-73` (trois
  `InternalsVisibleTo`) : **exacts**. Le `Compile` est implicite (glob SDK) : un déplacement dans `Ics/` n'exige
  aucune édition du projet (**vérifié** : `grep -n 'Compile' iXtal26.csproj` ne rend rien).

### 1.4 `oracle.tsv` et `check-oracle.sh`

Exact sur tout : `check-oracle.sh:49-53` (`ABSENT C#`), `:55-58` (le `-` compte comme hôte), `:60-63` (`ABSENT C`),
la comparaison de hachage sur `c_path`. Deux compléments :
- `csharp_path` n'est **pas** haché : déplacer un fichier sans mettre la ligne à jour donne `ABSENT C#` (retour 1),
  mais renommer un fichier **sans** ligne (les 39) ne produit aucun signal. Les 7 `.Materiel.cs` visés dont 3 ont
  une ligne (`pic`, `dma`, `keyboard_at`) et 4 n'en ont pas : le contrôle négatif de R2 « un `csharp_path` laissé
  rend `ABSENT C#` » ne vaut que pour 3 d'entre elles.
- Le harnais haché : toucher `tools/oracle/*.c` change le hachage de la ligne qui le cite ; D4 est donc fondée.

### 1.5 Les listings du JIT

`listings-jit.sh:188` (en-tête), `:203` (comparaison par nom), `:169` (adresses masquées) : relus, **exacts**.
Le script comparé est bien sans table. Le constat « 154 + 50 » n'a pas été refait (capture `M2-13b`, script de
R2 non commité) : **non vérifié par moi** ; l'ordre de grandeur est plausible. La conclusion — `compare` rendrait 1
sans rien prouver — est exacte par lecture du code (`seuls_a`, `seuls_b`).

### 1.6 Les dangers d'un remplacement textuel

- `PIT pit` : exact (§ 1.2). J'ai vérifié D2 mécaniquement : script jetable qui découpe `iXtal26/` et `tools/`
  par méthode et signale toute méthode qui déclare `pit`/`pic`/`keyboard_at` **et** emploie `pit_`/`pic_`/
  `keyboard_at_` : **0 cas**. Contrôle positif du même script : il repère bien les 14 méthodes de `pit.cs` et
  celle de `pit.Materiel.cs` qui déclarent `pit`. Donc la renomination `pit_` → `pit` ne rebranche rien
  (limite : le script ne voit pas les paramètres de lambdas ; l'IL tranche).
- Chaînes de PCem : exact pour celles citées. Aucune des cinq réflexions n'en est une.
- **Oubli** : les noms qualifiés `iXtal26.Models.pit.Probe(...)` (`BootDiff.cs:1795`, `:2217`) et 62 références à
  `pic` dans `Diff` sont du code (le compilateur les suit), mais un remplacement textuel devrait aussi traiter
  les espaces de noms ; voir D8.

### 1.7 Les sorties des portes

- 805 journaux comparés `gr2d1`/`gr1i2` : 805 paires, **exact**. Écarts : avec mon normaliseur (le nom de série
  et toute suite de trois chiffres ou plus), **13** et non 14 ; la différence vient des PID (`materiel-mode`
  s'égalise). Les 13 sont : `bd-ami486-aha-banc`, `cdimage-check`, `couverture-materiel`, `materiel-cas-pb03`
  (+ `-materiel`), `materiel-cas-pb123` (+ `-materiel`), `materiel-cas-pb215` (+ `-materiel`), `r9-atapi`,
  `r9-cdcfg`, `r9-cue`, `recensement`. **Imprécis** sur le chiffre, exact sur l'idée : le nombre dépend du
  normaliseur ; c'est un argument pour le **commiter** (R2 le propose) et pour fixer la liste du bruit au repère
  G14.0 par deux séries du même commit.
- Les `.tsv` de série portent la durée de chaque porte (`rc=0  6s`) : « identique à l'unité » ne peut pas valoir
  pour le `.tsv` brut. R2 ne le dit pas ; le normaliseur doit couvrir la colonne de durée.
- `Recensement.cs` lit `iXtal26/**/*.cs` en `AllDirectories` (`:54`) et ne dépend d'aucun chemin : **exact**. Le
  motif des marqueurs (`pcem bug, (not reproduced|reproduced|fixed in hardware mode)`) ne peut pas être déclenché
  par une table `// noms:`, sauf si une ligne de table recopiait ces mots : à interdire dans la doctrine.

### 1.8 Les outils et le harnais

- `harness_stubs.c:172` (`keyboard_at.cs`) et `harness.c:2092` (`nvr.cs`) : **exacts** (lignes relues).
  `Oracle.cs:345` cite `Models/dma.cs ProbeState` ; `AtProbe.cs:159` cite `keyboard_at.cs` : exact, 3 renvois
  dans `Diff` (comptés).
- `check-doc5150.sh` : non relu par moi dans le détail ; les 11 renvois de `ibm5150/` sont comptés. **Déduit.**
- `tools/gates/*.sh`, `tools/*banc` (`picbanc.py`, `dmabanc.py`…) : aucun chemin C# (grep des noms de fichiers
  visés, hors commentaires) : **exact**.
- Réécrivain de couverture hors dépôt : non revérifié (hors dépôt, `/tmp/a9/cov/`).

### 1.9 La mécanique du renommage

- `rename_refactoring` : le schéma de l'outil dit que `preview` « ne fait pas d'analyse de conflit » (« un
  aperçu propre peut échouer à l'application »), que `affects` vient de l'index des références (donc `nameof`,
  `cref`), et que l'outil n'a **aucun paramètre** pour les commentaires et les chaînes : son traitement est
  inconnu, comme R2 l'écrit. Il ne renomme ni fichier ni espace de noms ; `move_type_to_namespace` existe.
  Ajout : `symbolName` est un nom dans le fichier (« TypeName ») : pour `pit`, qui est à la fois la classe et un
  paramètre d'autres méthodes, le risque d'**ambiguïté** est réel (la réponse a un champ `ambiguous`).
- Roslyn : `microsoft.codeanalysis.csharp` et `.common` 4.14.0 dans `~/.nuget/packages`, pas `Workspaces` ni
  `Build.Locator` : **exact**. Complément : le SDK embarque `Microsoft.CodeAnalysis.dll` et `.CSharp.dll`
  (`/usr/lib/dotnet/sdk/10.0.113/`) : un outil jetable peut s'y référer sans rien télécharger. Un renommeur
  sémantique à la main (compilation de `iXtal26` avec SDL3-CS et SoftFloat, `GetSymbolInfo` sur chaque
  `IdentifierName`) est faisable mais c'est un vrai outil à écrire et à tester ; ne pas le sous-estimer.
- `9530e21` (précédent `cpu` → `cpu_c`) : cité, non rouvert par moi (déduit).
- `System.Reflection.Metadata.dll` dans `Microsoft.NETCore.App/10.0.13` : **exact**.

## 2. Ce qui manque

1. **`ixtal26-286.cfg:18`** cite `neat.cs` (« rien : neat.cs n'appelle jamais cpu_set() »), commentaire dans un
   fichier de configuration lu par les portes. Absent du § 1.6 de R2. Les `.cfg` ne sont pas hachés
   (`grep` des `.sha256` : aucun) ; `Recensement` ne lit d'eux que la clé `hardware_mode`. Peu grave, mais à
   mettre dans la liste des renvois vivants (le motif de R2 est restreint aux `.md`, `.cs` et sources).
2. **Types d'état et conteneurs multiples** : `pit.cs` porte `PIT`, `PIT_nr` et `pit` ; `pic.cs` `PIC` et `pic` ;
   `ppi.cs` `PPI` et `ppi_c` ; `rtc.cs` `rtc_h` **et** `rtc` ; `dma.cs` `dma_t` et `dma` ; `serial.cs` `SERIAL`
   et `serial`. D1 parle des « conteneurs seuls » sans dire ce que deviennent les types d'état ni lequel des
   deux conteneurs de `rtc.cs` reçoit le nom de la puce. Une renomination ne tranche pas seule.
3. **Espaces de noms** : `ppi` et `rtc` sont dans `iXtal26` (racine), les autres dans `iXtal26.Models`,
   `.Keyboard`, `.Sound`, `.Video`, `.Devices`. Les mettre dans `iXtal26.Ics` oblige des `using` là où le
   parent suffisait (les types `PPI`, `PIT`… sont résolus par le parent `iXtal26` dans `iXtal26.Floppy` : après
   le déplacement, non). R2 dit « le renommage du conteneur et de son espace » sans le mettre en décision.
4. **Les frères de `pit_`** : `keyboard_xt_` (`keyboard_xt.cs:56`), `keyboard_amstrad_` (`:46`),
   `keyboard_olim24_` (`:59`) ont la même cause (CS0542) et ne sont pas des puces. D2 les laisse suffixés ; les
   commentaires de `keyboard_at.cs:121` et `keyboard_xt.cs:55` les citent ensemble. Si D2 est suivie, la
   famille se dépareille : à dire, non à traiter (hors PLAN.md).
5. **Les volumes de D2** : `pic_` 106 occurrences de mot, `keyboard_at_` 141, `pit_` 50 (`grep -rnw`) dans
   `iXtal26/` et `tools/`. R2 n'en donne aucun.
6. **`rtc.cs` et le glob de `:97`** : voir § 1.3.
7. **La colonne de durée des `.tsv`** : voir § 1.7.
8. **L'identité de la `.so`** : R2 pose « `sha256` de la `.so` inchangée » ; GR.4e a déjà constaté que la `.so`
   est « égale hors débogage » (commit 328fe16). La preuve est la comparaison hors débogage, pas le `sha256`
   brut : la formule de R2 serait fausse si la construction embarquait des chemins. Ici la `.so` ne change pas
   si `tools/oracle/` n'est pas touché (D4), donc le `sha256` suffit ; à écrire tel quel.
9. **Documents vivants hors `.md`** : `iXtal26/Properties/launchSettings.json` et `Host/CommandLine/
   MakeNvrVerb.cs` citent `ixtal26-286.cfg`, pas un module visé : sans objet (vérifié par `grep`).

## 3. Les décisions ouvertes, revues

- **D1 — la portée (conteneurs seuls ou aussi les membres).** *Déjà tranchée* par le dépôt : PLAN.md § G14 dit
  « Les modules… prennent le nom de la puce » (le module, pas ses fonctions) et le précédent `Floppy/` écrit
  « Les FONCTIONS gardent toutes les noms de PCem : elles sont l'API entre modules » (`fdc.cs:47`). La seule
  vraie question est celle des **types d'état** (D8 ci-dessous). À fermer en « conteneurs, fichiers, espaces de
  noms, répertoires ; fonctions et champs inchangés ».
- **D2 — rendre leur nom de PCem à `pic_`, `pit_`, `keyboard_at_`.** Ouverte, mais à moitié tranchée par une
  règle écrite : `TRANSCRIPTION.md:181-185` (« le conteneur prend `_c`, le membre garde son nom ») que
  ces trois membres contredisent, et par la logique de D1 (les membres gardent le nom de PCem). L'effet est
  mécaniquement sûr (0 méthode à paramètre homonyme qui lise le suffixé, § 1.6) mais il touche ~300
  occurrences, ce qui en fait une étape à part. **Ma proposition** : la faire, en commit séparé après le
  conteneur, comme R2 ; l'inscrire comme *optionnelle* (elle ne figure pas dans PLAN.md : « seuls des noms
  changent » reste vrai, mais « Ce qu'on nomme » est le conteneur), et ne pas y mêler `keyboard_xt_` & co.
  Divergence avec R2 : **la présenter comme ouverte par PLAN.md est excessif**, c'est un choix de périmètre.
  Si Julien préfère le minimum, on garde les suffixes (qui ne gênent plus rien) : le coût de ne pas le faire est
  nul pour la porte.
- **D3 — le CRTC.** J'**approuve** R2. Preuves : `crtc[]` est dans 21 fichiers de `Video/` (`grep -rln
  'crtc\[' iXtal26/Video` = 21, R2 écrit 20 : **imprécis**), le seul nom propre est `Masque6845`
  (`vid_mda.Materiel.cs:38`), et `pcem-dev/src/video` n'a pas de fichier `6845`/`crtc` (`ls`). Il n'y a rien à
  renommer : l'extraire est un changement de code, hors G14 (« seuls des noms changent »). PLAN.md écrit la
  phrase au futur (« le CRTC → `Motorola6845` »), dans une liste de noms. Proposition : noter dans la table
  `// noms:` des cartes que leur `crtc` est un Motorola 6845, et demander à Julien d'**infirmer** la lecture
  (il a écrit la ligne) — une seule question fermée.
- **D4 — le harnais.** *Déjà tranchée* par PLAN.md (« Porte : toutes les séries identiques… seuls des noms
  changent ») et par le dépôt (`tools/oracle/` est l'oracle, `check-oracle.sh` en hache vingt-trois fichiers).
  Les deux mentions sont des commentaires. À fermer : ne pas toucher.
- **D5 — l'historique.** Ouverte (aucune règle écrite) ; la proposition de R2 est la pratique du dépôt
  (`VERIFICATION.md:1445`, `:1645`) : bonne. J'ajoute qu'il faut la **règle** (une ligne dans TRANSCRIPTION.md,
  dans la marge de R3 : 260 − 237 = 23 lignes) plutôt que « une ligne en tête de VERIFICATION.md » seule.
- **D6 — l'outil.** PLAN.md fixe « refactor Rider » : D6 n'est ouverte que pour les outils (`Diff`, `perfbanc`)
  hors solution. Une piste que R2 ne cite pas : charger `Diff` et `perfbanc` dans une solution **provisoire,
  non commitée** (un `.sln` hors dépôt) pour que Rider les voie. À essayer en `preview` avant tout choix.
  Comme `preview` ne fait pas d'analyse de conflit, l'IL par la table est le juge, pas l'outil.
- **D7 — le grain.** Ouverte. Cinq familles est raisonnable ; la règle « série entière quand l'émulateur change »
  (mémoire du dépôt) s'applique, puisque la DLL change. L'argument « l'IL rend la série confirmatoire » est
  exact pour le code, **pas** pour les journaux (bruit, durées) : garder les deux séries à chaque famille,
  c'est ce que R2 propose ; je suis d'accord.
- **D8 (manquante) — les types d'état, les conteneurs doubles, les espaces de noms.** Elle est ouverte parce
  que PLAN.md n'en dit rien. Proposition : (a) les types d'état (`PIT`, `PIT_nr`, `PIC`, `PPI`, `SERIAL`, `dma_t`)
  **ne bougent pas** (ils sont des « structs » de PCem, `ibm.h`, nommés comme en C ; les renommer est du
  G15) ; (b) pour `rtc.cs`, un seul conteneur prend le nom de la puce et `rtc_h` suit comme `Intel8259A`-et-ses-
  constantes (`rtc_h` = constantes du `.h`) — à décider avec la même règle que pour `fdc_c` ; (c) l'espace de
  noms suit le répertoire (`iXtal26.Ics`) comme les 23 autres, conformément à `TRANSCRIPTION.md:132`.

## 4. Ce qu'il faut corriger dans R2 avant le plan

1. § 1.3 : 827 journaux par série (1 654 en tout) ; « 14 » devient « 13 à 14 selon le normaliseur », avec la
   phrase « le nombre dépend du normaliseur ». Ajouter que les `.tsv` portent une durée.
2. § 1.1 : « 15 déclarations » → « 14 paramètres ou locales, plus le champ `PIT_nr.pit` (`pit.cs:38`) ».
3. § 1.5 : « 22 lignes `tools/oracle/…` » → 23 (colonne `c_path`) ; CRTC : 21 fichiers, non 20.
4. § 1.6 : ajouter `ixtal26-286.cfg:18` aux renvois vivants (commentaire).
5. § 1.4 : tempérer « le build tombe » (déduit) en citant les répertoires transcrits absents du glob ; dire
   que CS8981 ne frappe plus les conteneurs PascalCase et que le commentaire `:47-53` est à réécrire.
6. § 1.7 : ôter « inconnu » sur l'ordre de compilation en prescrivant un vidage d'IL **clé par nom qualifié
   trié** ; relever que les sept partielles n'ont aucun champ statique.
7. § 4 : fermer D1 et D4 (déjà tranchées), écrire D2 comme choix de périmètre optionnel, garder D3, D5, D6, D7,
   et ajouter D8.
8. Annexe : ajouter les frères de `pit_` (`keyboard_xt_`, `keyboard_amstrad_`, `keyboard_olim24_`) ; les volumes
   (`pic_` 106, `keyboard_at_` 141, `pit_` 50).
9. Le § 2 (étapes) : ajouter à G14.1 une étape « essai à blanc » (`preview` de Rider sur un module sans
   `.Materiel.cs`, par exemple `opti495`, puis l'IL) avant de toucher les familles réelles.

Chemin : `/home/jpeloille/RiderProjects/iXtal928/iXtal26/Docs/G14-reconnaissance/R2-contre.md`
