# R2 — Le nettoyage, les outils et la porte

Reconnaissance de G15, le 4 novembre 2026, au commit `34814ec` (G14.5). Lecture seule : rien n'a été construit, lancé
ni installé. Les DLL lues sont celles de `/var/tmp/ixtal-g14/g145/` (HEAD `34814ec`, construites le 04/11 à 6 h 27 ;
`iXtal26.dll` `cfd061ed…`), dont l'IL est celui de G14.4 entrée pour entrée (VERIFICATION.md § G14, « G14.5 »).
Niveaux : **vérifié** (relu, ou compté par la commande dite), **déduit** (raisonné sur ce qui est relu), **inconnu**.
Les deux lecteurs de métadonnées dont il est question ci-dessous (`morts.py`, `usings.py`) sont jetables, écrits hors
du dépôt pour cette lecture et non gardés ; leur méthode est dite en § 1.1 et § 1.2, pour qu'on la refasse.

## 0. En bref

- **Code mort** : 191 méthodes et 55 champs qu'aucun IL ne référence, 120 champs écrits jamais lus ; ôtés les `record`
  et les constructeurs servis par réflexion, 129 fonctions : 80 mortes chez PCem aussi, 47 dont l'appelant n'est pas
  transcrit, **2 propres à iXtal**. PB-20 est retrouvé en entier, le contrôle positif du relevé.
- **`using`** : au moins **45** directives certainement inutiles (les trois de § G14.2 en sont), 27 de plus qu'à R.
- **Avertissements** : 0, sous `TreatWarningsAsErrors` partout ; 43 `none` dans `.editorconfig`, 60 pragmas dans 53
  fichiers, dont un superflu (`Flash/rom.cs:12`) ; ni règle de nommage, ni `AnalysisLevel`, ni `global.json`.
- **Outils** : le SDK suffit ; BannedApiAnalyzers est le seul paquet utile (le « signalé »), aucun n'est nécessaire.
- **Porte** : ~4 h 30 de machine par étape qui change l'IL ; la table des noms refuse les membres, à étendre d'abord.
- **Proposé** : N0 (les outils), N1 (code mort et `using`), N2 (avertissements, en fin) ; quatre décisions, D1 à D4.

## 1. L'état, point par point

### 1.1 Le code mort

**La méthode, sans Roslyn** (vérifié sur les DLL de `g145`). Un lecteur des métadonnées (ECMA-335 § II.22-24, en
`python3 -I`, sans paquet, sur le modèle de `g14-4/outils/jetons_chaines.py`) lit les tables de `iXtal26.dll`, décode
le corps de chaque méthode et relève chaque jeton de méthode ou de champ (`call`, `callvirt`, `newobj`, `ldftn`,
`ldvirtftn`, `ldtoken`, `ld*fld*`, `st*fld*`, et les `MethodSpec`), puis ajoute les constructeurs d'attributs, le
point d'entrée, les `MethodImpl`, et les `MemberRef` de `iXtal26.Diff.dll` et `iXtal26.PerfBanc.dll` qui désignent un
type d'iXtal26 (par nom de type et de membre). Il écarte les méthodes virtuelles, les `.cctor`, les types et membres
générés (`<…>`), les `const` et `value__` (pliés, jamais référencés par jeton). Un champ jamais chargé
(`ldfld`, `ldsfld`, `ldflda`, `ldsflda`, `ldtoken`) mais écrit est « écrit jamais lu ».

**Le contrôle positif** : PB-20 (`PCEM_BUGS.md:7781`, « un champ et sept globales morts ») est retrouvé en entier :
`fdc_ready` et `motorspin` sans référence, `abort`, `discmodified`, `discrate`, `defaultwriteprot`, `fdc_indexcount`
et `oldtrack` écrits jamais lus (`Floppy/fdc.cs:44`, `Disc/disc.cs:25-26`, `:97`). PB-19 (`fdc.cs:28`) n'y est pas, et
c'est juste : `byte_written` est lu, il n'est jamais posé à 1 (une mort sémantique, que les jetons ne voient pas).

**Les comptes** (`iXtal26.dll` : 617 types, 5 120 méthodes, 6 143 champs dont 1 198 `const` ; les deux clientes
référencent 324 méthodes et 726 champs) :

| Sorte | Nombre | Niveau |
|---|---|---|
| membres de `record` (`Deconstruct`, `op_Inequality`, accesseurs `init` de 7 `record struct`) | 47 | vérifié |
| constructeurs (a) | 15 | vérifié |
| handlers `_a32` du 8087 (b) | 57 | vérifié |
| fonctions mortes chez PCem aussi (c) | 23 | vérifié |
| fonctions dont l'appelant n'est pas transcrit (d) | 47 | vérifié (e) |
| fonctions propres à iXtal (`386_ops_fpu.cs:127`, `vid_svga.cs:228`) | 2 | vérifié |
| **méthodes, en tout** | **191** | |
| champs sans référence (15 d'`ega_t`, 8 vues d'union de l'EMU8000, 3 de `fdd_t`, 2 de PB-20…) | 55 | vérifié |
| champs écrits jamais lus (34 `materiel.pb_NN`, 6 de PB-20, 5 de `HState`…) | 120 | vérifié |

(a) 10 classes d'état servies par `ConditionalWeakTable.GetOrCreateValue`, qui les crée par réflexion ; 4 délégués
jamais instanciés ; `Program`. (b) `Cpu/x87_ops_808x.cs`, généré : PCem compile aussi les tables
`ops_808x_fpu_*_a32` sans jamais les indexer (`x87_ops_808x_tables.cs:7-8`). (c) Aucun appel dans `pcem-dev/src`
hors définition, prototype ou commentaire. (d) PCjr, PS/1, PS/2, Tandy, l'interface wx/qt, le recompilateur.
(e) Relu un par un jusqu'à quatre occurrences dans PCem ; au-delà, déduit.

(Comptes : `python3 -I morts.py iXtal26.dll iXtal26.Diff.dll iXtal26.PerfBanc.dll`, puis le tri des 191 par motif
de nom ; les 72 fonctions de PCem classées par `grep -rnw NOM pcem-dev/src pcem-dev/includes`. La liste est en
annexe B.)

**Mort parce que PCem l'est aussi, transcrit exprès** : la règle est écrite. Une branche morte se transcrit « parce
que la structure du C est ce qui se lit » (`Cpu/808x.cs:72-73`) ; seul le code mort *commenté* de PCem ne l'est pas
(R1 (e), `TRANSCRIPTION.md:30`). Le marquage n'est pas uniforme : PB-20 (`disc.cs:97`, `fdc.cs:44`), `fdd.cs:15`,
`ide.cs:451`, `:868` le disent ; les 15 champs d'`ega_t` (déclarés `vid_ega.h:24-59`, aucun `ega->X` dans
`vid_ega*.c`, vérifié champ par champ), les 23 fonctions et les 57 `_a32` ne le disent pas. **Mort côté iXtal
seulement** : 47 fonctions de module (« les fonctions gardent les noms de PCem : elles sont l'API entre modules »,
`fdc.cs:47`), que PCem appelle d'une machine ou d'une interface que le dépôt n'a pas (par exemple `pit_clock`,
appelé par `pit_irq0_timer_pcjr` et `pit_irq0_ps2`, `pit.c:535`, `:550`) ; et 2 fonctions propres à iXtal, dont
`X87NonTranscrit`, qui en entraîne une troisième : `opX87NonTranscrit` (`386_ops_fpu.cs:121`) n'est appelé que par
elle (une cascade, que le relevé doit répéter jusqu'au point fixe).

**Les limites du relevé** (vérifié) : la réflexion par nom, en 6 sites (`Cpu/386.Materiel.cs:314` `ILLEGAL` ;
`tools/iXtal26.Diff/MaterielCas.cs:514` `memcycs` ; `MaterielCasFrequence.cs:85-87`, les champs de `sound_sb` par
suffixe `_iir_x`/`_iir_y` ; `MaterielCasPit.cs:52`, `:55` ; `tools/perfbanc/GardeBanc.cs:93` `setadc8`) ;
`ConditionalWeakTable` (10 classes, par exemple `Scsi/scsi_aha1540.Materiel.cs:164-193`) ; les cas de
BenchmarkDotNet (les 103 méthodes de `PerfBanc` que rien ne référence) ; `iXtal26.SoftFloat` (6 fonctions sans
référence depuis iXtal26 et iXtal26.Diff, que `tools/softfloat/Banc`, non construit dans `g145`, appelle peut-être :
inconnu). Le harnais : `iXtal26.Diff.dll` a 4 méthodes réellement sans référence, `BootDiff.PasCsharp`
(`BootDiff.cs:1072`) et trois imports `h_*` de `Oracle.cs` (`:167`, `:214`, `h_fastreadb`), qui sont du contrat de
l'ABI (`Oracle.cs:34`).

### 1.2 Les `using`

§ G14.2 en a nommé trois, chacun ôté seul puis la construction (`g14-2/usings-inutiles.log`) : `Mouse/mouse_serial.cs`,
`Ics/Intel8042.cs`, `tools/iXtal26.Diff/MaterielCas.cs` (`VERIFICATION.md:18287-18293`). Ce n'était que les directives
d'un espace quitté, dans les fichiers de l'étape. **Une borne basse sur tout le dépôt** (vérifié par
`python3 -I usings.py`) : une directive dont *aucun* identifiant du fichier (hors commentaires) n'est un nom de type de
l'espace (pour `using N;`) ou un membre du type (pour `using static T;`), lus dans les quatre DLL de `g145`, est
certainement inutile (aucune méthode d'extension, R4).

| | Directives | Certainement inutiles | Au commit R (`ref-R`) |
|---|---|---|---|
| `using iXtal26.…;` | 386 | 41 (25 `Models`, 10 `Memory`, 3 `Diag`, 2 `Sound`, 1 `Keyboard`) | 14 |
| `using static iXtal26.…;` | 410 | 4 | 4 |

Les trois de § G14.2 y sont ; les trois qu'elle disait utiles (`Motorola146818.cs` `Models`, `BootTest.cs`
`Keyboard`, `MaterielCas.cs` `Models`) n'y sont pas. Les 24 `using iXtal26.Models;` de `Video/`, `Sound/`, `Cpu/` et
du harnais apparus depuis R sont, selon toute vraisemblance, ceux que G14.1 a laissés en sortant `pic`, `pit` et
`dma` de `Models` (déduit). Les `using System.…;` n'ont pas été lus (leurs types sont hors de ces DLL) : inconnu.
`ImplicitUsings` est posé dans les 7 projets (`iXtal26.csproj:12`) : `System.Linq` est donc importé partout, chemins
chauds compris ; aucune directive ne double un `using` implicite (`git grep` : 0).

**Pourquoi le compilateur se tait** (vérifié pour l'état, déduit pour le remède) : CS8019 est un diagnostic caché ;
IDE0005 n'est réglé nulle part (`grep -n IDE0005 .editorconfig` : rien) et, selon la documentation du SDK, ne se
signale à la construction qu'avec `GenerateDocumentationFile`, que rien ne pose (`git grep` : 0). Le poser fait
analyser les 3 380 lignes `///` de 103 fichiers d'iXtal26 : CS1591 sur ce qui est public, CS1570-CS1574 sur un XML mal
formé (12 lignes `///` ont un `&` ou un `<` nus, compte `grep`) ; sous `TreatWarningsAsErrors`, autant d'erreurs
possibles (inconnu sans construire). Deux voies sans paquet : l'option dans une construction jetable, ou le Roslyn
que `tools/renommage` charge déjà (`iXtal26.Renommage.csproj:23-34`), avec `DocumentationMode` posé en mémoire, qui
rend CS8019 directive par directive sans toucher un `.csproj` (déduit).

### 1.3 Les avertissements

- **0 avertissement** : la construction Release `--no-incremental` des trois projets de G14.5
  (`g14-5/construction.log`, SDK 10.0.114) : `0 Warning(s)`, trois fois (vérifié).
- **`TreatWarningsAsErrors`** dans les 7 `.csproj` (`iXtal26.csproj:38`, `iXtal26.SoftFloat.csproj:21`,
  `iXtal26.Diff.csproj:15`, `iXtal26.PerfBanc.csproj:13`, `iXtal26.IlDump.csproj:21`, `iXtal26.Renommage.csproj:20`,
  `iXtal26.SoftFloatBanc.csproj:14`) ; **`EnforceCodeStyleInBuild`** dans 2 (`iXtal26.csproj:39`,
  `iXtal26.SoftFloat.csproj:22`) ; aucun `NoWarn`, `WarningsNotAsErrors`, `AnalysisLevel`, `AnalysisMode`,
  `SuppressMessage`, `#nullable` (`git grep` sur `*.cs`, `*.csproj`, `*.props`, `*.targets` : 0) ; ni
  `Directory.Build.props` ni `global.json` (le SDK n'est pas épinglé non plus).
- **Neutralisé par `.editorconfig`** : 43 lignes `severity = none` (`grep -c`), 18 diagnostics, en six sections :
  le code transcrit (`.editorconfig:40-76` : IDE1006, CA1707, CA1708, CS8981, dix règles de mise en forme, CS0649,
  IDE0044, IDE0051, IDE0052), SoftFloat (`:80-91`), `materiel.cs` (`:96-99`), `io`/`timer`/`pc`/`ppi` (`:101-110`),
  `tools/` (`:117-119`, IDE1006 et CA1707).
- **Neutralisé par `#pragma`** : 60 `#pragma warning disable` (1 `restore`, `808x.cs:1136`) dans 53 fichiers
  (`git grep -E "^\s*#pragma warning disable"`), 104 mentions de code : CS8602 ×33, CS8600 ×27, CS8981 ×21,
  CS8604 ×16, CS1717 ×3, CS0162 ×3, CS8618 ×1. Chacun est commenté (la règle, `TRANSCRIPTION.md`, Portée).
- **Les 21 CS8981** compensent le glob : `Cdrom/` (2), `Ide/` (3), `Joystick/` (4), `Lpt/` (1), `Scsi/` (9),
  `rtc.cs` (1) sont hors de `[**/{Cpu,…,Ics}/**.cs]` (`.editorconfig:40`), et `Flash/rom.cs:12`, qui est dans le glob
  depuis : **ce pragma est superflu**, et son commentaire (`rom.cs:9-11`, « Flash/ n'y figure pas ») est périmé. Ni
  `TRANSCRIPTION.md:99-101` (la Portée) ni le glob ne nomment les 22 fichiers transcrits de `Cdrom/`, `Ide/`,
  `Joystick/`, `Lpt/`, `Mfm/`, `Scsi/` ni `rtc.cs` (`grep -l '^// ORACLE: pcem-dev'` : 4, 3, 4, 3, 2, 5 et 1) ; la
  Portée omet aussi `Sound/`, `Devices/`, `Diag/`, `Flash/` et `Mouse/`, que le glob a.
- **Ce que « 0 » ne dit pas** : aucune règle IDE n'est à `warning` (toutes celles que le fichier nomme sont à
  `none`) ; les règles CA sont celles du niveau par défaut du SDK, que rien n'épingle (inconnu sans évaluer le projet).

### 1.4 `.editorconfig` et les analyseurs : ce qui manque

- **PascalCase** : des `dotnet_naming_rule`, `_symbols` et `_style`, et IDE1006 à `warning`. Aujourd'hui : aucune
  règle de nommage (`grep -c dotnet_naming .editorconfig` : 0), IDE1006 à `none` dans 5 sections.
- **`private` explicite** : `dotnet_style_require_accessibility_modifiers` et IDE0040 à `warning`. Aujourd'hui :
  rien ; le nombre de membres sans modificateur est inconnu (`grep` ne distingue pas un membre d'une locale).
- **Les `using`** : IDE0005 à `warning` avec `GenerateDocumentationFile`, ou CS8019 relevé par Roslyn (§ 1.2).
  Aujourd'hui : rien.
- **Les suppressions superflues** : IDE0079. Aujourd'hui : rien, et `rom.cs:12` en est une.
- **Le niveau CA** : `AnalysisLevel` épinglé (`10.0`) et le mode choisi. Aujourd'hui : non posé ; une mise à jour du
  SDK peut ajouter des avertissements, donc des erreurs.
- **Les chemins chauds** : RS0030 (BannedApiAnalyzers) par glob, ou une revue par `grep`. Aujourd'hui : rien.

Les sévérités se règlent par glob, pour tout diagnostic, analyseur tiers compris : c'est ainsi que le fichier
neutralise déjà le code transcrit. Une règle « interdite dans les chemins chauds » se pose donc à `error` dans les
globs de `Cpu/`, `Memory/`, `Video/`, `Sound/` et des minuteries, et à `none` ailleurs (déduit). Le SDK porte les
`globalconfig` des niveaux 5 à 11 (`analyzers/build/config/`) ; au niveau 10, `minimum` met 92 règles CA à `warning`,
`recommended` 145, `all` 280 (`grep -c "severity = warning"`).

### 1.5 Les outils : dans le SDK, ou à installer

Dans le SDK 10.0.114, rien à installer :
- **les analyseurs CA et IDE** (`Sdks/Microsoft.NET.Sdk/analyzers/`, `codestyle/cs/`) : le nommage, `private`, les
  `using`, les suppressions superflues, le niveau CA. **Nécessaire.**
- **`dotnet format`** (`sdk/10.0.114/DotnetTools/dotnet-format`) : appliquer ou vérifier IDE0005 et IDE0040
  (`--verify-no-changes`). Utile.
- **le Roslyn du SDK, par `tools/renommage`** (un lien privé, 5.0.26.52326) : CS8019 exact, et le renommage
  sémantique des membres. Utile.
- **`DOTNET_JitDisasm`** (`listings-jit.sh`) : la preuve du 0 %, et les allocations compilées
  (`CORINFO_HELP_NEW*`, `CORINFO_HELP_BOX`). **Nécessaire.**
- **`MemoryDiagnoser`** (BenchmarkDotNet 0.15.8, installé le 04/10, `tools/perfbanc/Program.cs:31`) : l'allocation
  par opération, déjà un « rouge exact » (`compare_ref.py:37`). **Nécessaire, en place.**

À installer, chacun sur l'accord de Julien (PLAN.md, Annexe) :
- **BannedApiAnalyzers** (NuGet, `PackageReference` en `PrivateAssets="all"`, une restauration en ligne) : RS0030,
  LINQ (`System.Linq.Enumerable`) et toute API listée, par glob. Utile ; c'est le « si possible » de
  PLAN.md:585-587.
- **un analyseur d'allocations** (NuGet ; son entretien est à vérifier au moment voulu) : le boxing, les fermetures,
  `params`, à la compilation. Utile ; les listings et le banc le couvrent déjà.
- **ReSharper Command Line Tools** (`jb inspectcode`, un outil global) : les inspections JetBrains (membres
  inutilisés, `using`), en contre-compte. Absent (`which jb` : rien ; Rider RD-262.10968.170 n'en livre pas
  l'exécutable, déduit). Utile, en contre-lecture.
- **`dotnet-counters` et `dotnet-trace`** (deux outils globaux, absents) : le GC et les allocations d'un amorçage
  entier. Utiles pour la démonstration, pas pour la porte.

Rien n'est nécessaire au-delà du SDK. Les listings donnent déjà les allocations compilées : sur les 30 scénarios de
R (`ref-R-captures-10.0.14/jit/`), 358 méthodes appellent une aide d'allocation du JIT (531 sites, `NEWSFAST` ×310,
`NEWARR_1_VC` ×112, `NEWARR_1_PTR` ×88), presque toutes des initialisations (compte `awk` sur les en-têtes). Un
`grep` des chemins chauds, lui, se trompe : 16 « appels LINQ » relevés dans `Cpu/`, `Video/`, `Sound/` et `Ics/`, dont
15 sont `Math.Min`/`Math.Max` ; le seul vrai est `Video/video.Materiel.cs:59`. C'est l'argument de RS0030.

### 1.6 La porte, ce que chaque preuve prouve

- **Les séries entières** (827 portes, « aucun » et « tout », `compare-series.py` contre `g14r` et `g14r-tout`) :
  le comportement de ce que les portes exécutent, à l'unité, contre l'oracle et contre R. Elles ne disent rien d'un
  code qui ne s'exécute pas (le code mort, par définition) ; elles attrapent en revanche ce que la compilation ne
  voit pas : un nom lu par réflexion (§ 1.1), un `ConditionalWeakTable`, un message qui changerait.
- **Le banc contre R** (`compare-ref.sh`, 65 cas, ABBA, trois passes, Q37) : la vitesse, avec la résolution de
  § G14.0b (`VERIFICATION.md:19554-19561`) : de +0,18 à +2,88 % sur Machine, +0,28 % sur Exec8088, de +1,42 à
  +5,01 % sur Exec286, Exec386 et Exec486, +3,75 % sur Fetch ; RenduCga, Rendu8bpp, Video, Son et SonOplPro jamais
  injectés, lus dans la largeur de l'A/A (0,3 à 0,9 point ; Video 0,9 à 4,8 ; RenduCga 1,2 et 2,1). Le décodage en
  « tout », Fetch, RenduCga, Video et les trois boucles d'Exec386 sont plus grossiers que le 0 % : là, il repose
  sur les listings. L'allocation par opération est jugée à l'octet (« rouge exact »).
- **Les listings du JIT** (`listings-jit.sh compare --noms --dll-ref --dll-autre`, 1 512 méthodes) : le code
  machine, méthode par méthode. *Là où l'IL ne change pas*, la table (et Q39 pour les jetons de chaîne). *Là où il
  change* :
  - ôter une méthode morte : elle n'est jamais compilée, les listings ne bougent pas (attendu 0/0/0, aux jetons de
    chaîne près, que Q39 lit : le tas #US change si une chaîne part) ; ôter un champ d'instance déplace les champs
    suivants (`[rdi+0x…]`) : des méthodes « changées », à lire une à une ; ôter un champ statique peut changer le
    `.cctor` (`scsi_aha1540.Materiel.cs:160-161`, PLAN-G14 n° 4 bis) et, avec lui, les gardes d'initialisation que
    le JIT pose dans les méthodes chaudes (déduit) ;
  - renommer un membre : la table refuse les membres (`tools/noms/g14.txt:29`, `ildump/Program.cs:39`, `:195`,
    `listings-jit.sh:33-34`, `:278`) ; il faut une table des membres, générée par l'outil de renommage (iXtal26 a
    5 120 méthodes et 4 945 champs non `const`), appliquée aux en-têtes et aux appels des listings, et à l'IL ;
  - un `enum` à la place d'un `int` : la signature change dans l'IL et dans l'en-tête du listing
    (`iXtal26.Models.pic:picint(ushort)`), le corps non (le JIT traite un `enum` comme son type sous-jacent) ;
    il faut une normalisation « enum → type sous-jacent » dans `ildump` et `listings-jit.sh`, puis 0/0/0 attendu ; un
    `ToString`, un `Equals(object)` ou un dictionnaire sans comparateur sur l'`enum` mettent une boîte, que le listing
    montre (`CORINFO_HELP_BOX`) et que le banc rougit (allocation) (déduit).
- **L'IL** (`ildump comparer`, 5 738 entrées) : la preuve directe d'un nettoyage, si l'écart attendu est écrit
  d'avance : ôter des `using` donne 0/0/0 sans table ; ôter des méthodes donne exactement ces méthodes disparues ;
  ôter des champs, exactement les entrées de leurs types changées (les champs y sont vidés dans l'ordre,
  `ildump/Program.cs:20-21`). Les analyseurs et `.editorconfig` ne changent pas l'IL (déduit, à prouver par 0/0/0).

### 1.7 La durée d'une porte

| Étape | Série « aucun » | Série « tout » | Banc | Capture du JIT | Machine | En tout |
|---|---|---|---|---|---|---|
| G14.1 (`VERIFICATION.md:17856-17859`) | 1 h 51 | 48 min | 65 min | 3 × 15 min | ~7 h 15 | ~8 h |
| G14.2 (`:18259-18261`) | 1 h 52 | 48 min | 53 min | 15 min | ~4 h | ~4 h 30 |
| G14.3 (`:18741-18743`) | 1 h 54 | 49 min | 65 min | 15 min | ~4 h | ~4 h 40 |
| G14.4 (`:19332-19335`) | 1 h 54 | 49 min | 53 min | 15 min | ~4 h 40 | ~5 h |

(G14.1 compte le réancrage sous 10.0.14 et deux rejeux.) Une étape qui change l'IL coûte donc une demi-journée, sans
série en parallèle ni charge à côté (la règle du dépôt). G14 a fait quatre étapes de ce genre ; le nettoyage de G15 en
demande une (§ 2), et le reste de G15 (les conventions, les `enum`) les siennes : chaque étape de plus coûte ~4 h 30.

### 1.8 La tenue de la référence

- **`ref-R`** (`f15e11f`), `cmp-R`, `ser-R`, `gel-R-10.0.14/`, `ref-R-captures-10.0.14/` et `g14r`/`g14r-tout` doivent
  tenir jusqu'à la fin de G15 (`VERIFICATION.md:19730-19732`, PLAN-G14:190). Vérifié le 04/11 à 6 h 00 (§ G14.5,
  `verif-avant.log`) : HEAD, `git status` vide, les trois DLL de bin/, les 1 894 fichiers du manifeste.
- **Le runtime n'est pas épinglé** (ni apt, ni `global.json`) : le 21/10 (10.0.12 → .13) et le 31/10 (10.0.13 → .14)
  apt a changé .NET sous le dépôt ; le réancrage de § G14.1.0 (`:17468`) a coûté trois essais de la porte R contre R
  (12, 53 et 53 min) et une capture. Il faut savoir ce qu'il ne couvre pas : BenchmarkDotNet **reconstruit** R depuis
  son source avec le SDK du jour (`compare-ref.sh:49-51`) ; un SDK neuf qui ajoute un avertissement CA par défaut
  ferait échouer la construction de R sous `TreatWarningsAsErrors`, et R ne peut pas changer (déduit).
- **Le délai de 1 200 s** (`tools/iXtal26.Diff/Banc.cs:166-169`, `WaitForExit(1_200_000)`) : une horloge de l'hôte
  dans une porte déterministe ; il a frappé `banc-atapibanc*` en G14.0d, G14.1 et G14.4, chaque fois rejouée seule et
  dite à côté du tsv. Une porte de G15 peut le rencontrer encore.

### 1.9 Ce que la clôture de G14 laisse à G15

`VERIFICATION.md:19711-19724`, « ce qui pèse sur G15 », et ce qui en relève selon PLAN.md:565-597 :

- **trois `using` inutiles** (§ G14.2) : **oui**, « nettoyage (… `using` …) » ; ils sont au moins 45 (§ 1.2).
- **`tools/renommage`**, qui lie le Roslyn privé du SDK et ne joue pas une étape seule : **oui, comme outil** ; les
  renommages de G15 s'en serviront, et il faut l'étendre aux membres.
- **le runtime non épinglé, `ref-R` jusqu'à la fin de G15** : **oui, la porte** (« à chaque commit de G15, la mesure
  contre R », PLAN.md:592-593).
- **le délai de 1 200 s de `Banc.cs`** : non comme tâche (PLAN.md n'en dit rien) ; un risque de la porte (§ 3).
- **les 197 renvois `fichier.cs:N` décalés en G14.1** : non ; un risque des renommages, qui recouperont des lignes
  portées au-delà de 120.
- **la Portée de `TRANSCRIPTION.md:99-101`** : **en partie** ; N2 réécrit les globs de `.editorconfig`, et la Portée
  doit dire la même liste (§ 1.3).
- **les renvois inexacts de G14.1-4** : non.

Le reste de « Ce qui reste » n'en relève pas : les défauts candidats de PCem (règle du 18/10), les constats de
G14.0a (`initpc`, la M24 et le PC1512 dans `MachineBanc`, le fichier temporaire de `VideoBanc`), les tsv au-delà de
120, les images `os/386-HDD-C.img` et `os/486-HDD-C.img`, la branche `g14-4-avant-cl` (sauf `ref-R`, `cmp-R` et
`ser-R`, qui restent en place).

## 2. Ce qu'il faut faire, et les étapes proposées

L'ordre dans G15 : le banc des constructions interdites ouvre G15 (PLAN.md:580-584) ; N0 vient ensuite, avant tout
renommage, parce que la porte des renommages en dépend ; N1 avant les renommages (moins de directives et de membres à
renommer et à prouver) ; N2 en dernier, quand le code suit les règles qu'il active (sous `TreatWarningsAsErrors`, une
règle à `warning` ne se pose que sur un code qui la respecte ; les CS8981 tombent d'eux-mêmes quand les conteneurs
prennent un nom PascalCase).

### N0 — Les outils de la porte (aucun changement de l'émulateur)

- **Contenu** :
  1. le relevé des références dans `tools/ildump` (`ildump references DLL [DLL clientes…]`) : la méthode de § 1.1,
     par `System.Reflection.Metadata` comme le reste de l'outil, sans paquet ; ses exclusions écrites (virtuelles,
     générées, `const`) ; la liste des faux positifs connus en entrée (les 6 sites de réflexion, les 10
     `ConditionalWeakTable`, les cas de BenchmarkDotNet), chacun nommé ; la cascade jusqu'au point fixe ;
  2. le relevé exact des `using` : CS8019, par le Roslyn de `tools/renommage` (`DocumentationMode` en mémoire), ou
     IDE0005 dans une construction jetable avec `GenerateDocumentationFile` ; le choix se fait sur le premier essai ;
  3. dans `ildump comparer`, un fichier d'**écart attendu** (les entrées qui doivent disparaître ou changer) : la
     comparaison rend 0 si et seulement si l'écart est exactement celui-là ;
  4. pour les renommages et les `enum` de G15 (s'ils sont planifiés ainsi) : la table des membres et la
     normalisation « enum → sous-jacent » dans `ildump` et `listings-jit.sh`.
- **Portes et preuves** : les témoins de chaque outil, chacun avec son journal ; `ildump` et les listings d'une
  capture contre elle-même : 0 ; l'IL d'iXtal26 inchangé (aucun source de l'émulateur touché) ; lignes ≤ 120. Ni
  série ni banc : ni l'émulateur ni un outil de la série ne change (la règle du dépôt).
- **Contrôles négatifs** : dans une copie jetable, une méthode morte ajoutée sort du relevé, un appel par
  `ConditionalWeakTable` sort comme faux positif connu, un `using` utile ôté fait échouer la construction (CS0246) et
  n'est pas dans le relevé ; un écart attendu faux (une entrée de trop, une de moins) fait rendre 1 ; une table des
  membres qui nomme un membre absent est refusée (retour 2).

### N1 — Le code mort et les `using` (l'IL change)

- **Contenu**, selon la décision D1 (§ 4) ; dans la proposition :
  1. les `using` relevés par N0 (au moins 45), ôtés ;
  2. le code mort propre à iXtal : `X87NonTranscrit` et `opX87NonTranscrit` (`386_ops_fpu.cs:121-132`, et les
     commentaires qui les nomment : `cpu.cs:328`, `x87_ops_tables.cs:12`), le `pclog` de `vid_svga.cs:228` ;
     dans le harnais, `BootDiff.PasCsharp` (les imports `h_*` restent : l'ABI) ;
  3. le pragma superflu de `Flash/rom.cs:12` et son commentaire périmé (`:9-11`) ;
  4. le code mort transcrit reste ; la liste de l'annexe B est consignée dans `VERIFICATION.md` § G15 (ce qui est
     mort, pourquoi, gardé par R6), sans commentaire neuf dans le code (le budget de R1).
- **Portes et preuves** : construction 0 avertissement ; `ildump comparer` contre le parent, sans table, égal à
  l'écart attendu (les méthodes ôtées disparues, rien d'autre) et contre R par la table jusqu'à G14.4 ; `ildump
  references` après : les ôtées n'y sont plus et rien de neuf n'apparaît, hors la cascade dite ; listings du JIT
  contre R : 0/0/0 attendu, les jetons de chaîne lus par Q39 ; séries entières `g15…` et `g15…-tout` contre `g14r`
  et `g14r-tout`, 0 écart ; banc contre R, vert sur 65 cas ; `check-oracle`, `check-doc5150`, R2 recompté sur les
  fichiers transcrits touchés par les `using` ; lignes ≤ 120 ; la vérification d'avant l'étape de § G14.0d (runtime,
  `ref-R`, aucun commit hors G15 sur le pathspec).
- **Contrôles négatifs** : chaque `using` ôté remis seul ne change pas l'IL (échantillon) ; une méthode vivante ôtée
  (témoin jetable) fait échouer la construction ; `ILLEGAL` renommé dans une copie jetable : la construction passe et
  les portes du mode « tout » qui décodent sur le 386 rougissent (le statique `illegal` de `_386_materiel` lève à
  l'initialisation du type, `386.Materiel.cs:29`, `:313-315`, déduit) — la preuve que les séries couvrent la
  réflexion.
- **Durée** : une porte entière, ~4 h 30 de machine (§ 1.7).

### N2 — Les avertissements et `.editorconfig`, en fin de G15 (l'IL ne change pas)

- **Contenu** :
  1. `AnalysisLevel` épinglé (`10.0`) dans les 7 projets, le mode choisi sur une construction jetable (combien
     d'avertissements à `minimum`, `recommended`) ; `EnforceCodeStyleInBuild` dans les projets qui ne l'ont pas ;
  2. les règles que G15 a fait respecter, à `warning` : le nommage (IDE1006 et ses `dotnet_naming_*`), IDE0040,
     IDE0005, IDE0079, dans les globs où elles valent — ou glob par glob, à chaque étape de renommage, si le plan de
     G15 le préfère ;
  3. les suppressions devenues superflues ôtées (les CS8981 si les conteneurs sont renommés), relevées par
     IDE0079 ; les autres pragmas restent, énumérés et commentés ;
  4. le « signalé » de PLAN.md:585-587 : RS0030 à `error` dans les globs des chemins chauds si D3 dit oui, sinon la
     revue par `grep` écrite dans la porte (LINQ, `new`, `params`, `virtual`, `interface`, `abstract`, `=>`) ;
  5. les globs de `.editorconfig` et la Portée de `TRANSCRIPTION.md` alignés sur la même liste (§ 1.3).
- **Portes et preuves** : construction 0 avertissement des 7 projets, `--no-incremental` ; l'IL d'iXtal26,
  d'iXtal26.Diff, de SoftFloat et du banc égal à celui du parent sans table (0/0/0) ; la `.so` non touchée ; le reste
  selon D2.
- **Contrôles négatifs** : dans un fichier témoin d'une copie jetable, un `using` inutile, un membre sans
  modificateur, un nom en snake_case dans un glob converti, un `Enumerable.Select` dans `Cpu/` : chacun fait échouer
  la construction, avec son code ; le même témoin hors du glob passe.

## 3. Les risques

- **Les faux positifs du relevé** : un membre lu par réflexion ou créé par `ConditionalWeakTable` paraît mort ; ôté,
  la construction passe et seule une porte du bon mode rougit. Parade : la liste connue en entrée de N0, les séries
  dans les deux modes. Les renommages de G15 ont le même risque, en pire : `MaterielCasFrequence.cs:87` filtre par
  suffixe, et un suffixe qui ne correspond plus ne lève rien (le témoin cesserait de remettre ses filtres à zéro).
- **La cascade** : ôter une méthode en tue d'autres (`opX87NonTranscrit`) ; le relevé se répète jusqu'au point fixe.
- **Les champs** : ôter un champ d'instance déplace les décalages dans le code machine ; ôter un statique peut changer
  un `.cctor`, donc les gardes d'initialisation du JIT dans du code chaud (déduit). D'où la proposition de D1 : ne
  pas ôter les champs transcrits.
- **`GenerateDocumentationFile`** : 3 380 lignes `///` analysées d'un coup ; sous `TreatWarningsAsErrors`, une
  construction rouge. Parade : la voie Roslyn de N0, ou l'option dans une construction jetable seulement.
- **Le SDK qui bouge** : un SDK neuf peut ajouter des avertissements CA par défaut ; C se protège en épinglant
  `AnalysisLevel` (N2), R ne le peut pas, et BenchmarkDotNet le reconstruit à chaque passe (§ 1.8). Parade : D4.
- **Le délai de 1 200 s** : une porte peut tomber sur l'horloge de l'hôte ; la parade de G14 tient (rejouer seule,
  le dire à côté du tsv, garder la première tentative).
- **La durée** : chaque étape qui change l'IL coûte ~4 h 30 de machine ; un réancrage en coûte ~2 h de plus.
- **`using static` comme `#include`** : `.editorconfig:50` lit `using static … mem;` « comme `#include "mem.h"` » ;
  les 4 `using static` inutiles n'ont plus ce rôle (rien de leur type n'est nommé dans le fichier), mais ôter une
  directive d'un fichier transcrit change son compte R2 (une ligne vive de moins ; aucun plancher n'est menacé à
  une ligne près, déduit).

## 4. Les décisions ouvertes

**D1 — Le périmètre du « code mort » dans le code transcrit.** PLAN.md:569-571 dit « nettoyage (code mort …) » sans
le borner ; TRANSCRIPTION.md demande que chaque ligne C ait sa contrepartie ou un `// omitted:` (R6), transcrit les
branches mortes (`808x.cs:72-73`) et reproduit PB-20.
*Proposition* : ôter le code mort propre à iXtal (2 fonctions et leur cascade, 1 dans le harnais) et les `using` ;
garder le code mort transcrit (80 fonctions et handlers morts chez PCem aussi, 47 dont l'appelant n'est pas transcrit,
les 55 champs) et le consigner. *Pourquoi* : une méthode jamais appelée n'est jamais compilée, elle ne coûte rien à
l'exécution (les listings le montrent) ; l'ôter coûterait un `// omitted:` et une entrée du registre par fonction,
un recompte R2, et casserait la symétrie `grep` avec `pcem-dev/` ; les 47 sont l'API de leur module, qu'une machine
future (PCjr, PS/1) rappellerait ; ôter des champs change le code machine (§ 3).

**D2 — La porte d'un commit dont l'IL ne change pas** (N2, et N1 s'il était coupé en deux). PLAN.md:596 et
:592-593 demandent les séries et le banc « à chaque commit » ; G14.5 ne les a pas joués, son IL étant celui de son
parent entrée pour entrée (`VERIFICATION.md`, § G14.5, « Ni série entière ni banc »).
*Proposition* : quand `ildump comparer` contre le parent, sans table, rend 0/0/0 pour les quatre assemblages et que
la `.so` n'a pas bougé, la porte est la construction sans avertissement, l'IL et les témoins des règles, comme en
G14.5 ; sinon, la porte entière. *Pourquoi* : les séries et le banc exécuteraient le binaire du parent, à l'IL près ;
c'est la lecture que G14.5 a faite de la même règle, et elle épargne ~4 h 30 par commit. N1 garde ses `using` et son
code mort dans un seul commit, à la porte entière : la question ne s'y pose pas.

**D3 — BannedApiAnalyzers pour le « signalé ».** PLAN.md:585-587 : « si possible, un analyseur … ; sinon, une revue
par `grep` » ; l'annexe (PLAN.md:632-633, à HEAD) veut l'accord de Julien au moment de l'installation.
*Proposition* : l'installer en N2, sur accord, dans `iXtal26.csproj` seul, `PrivateAssets="all"`, RS0030 à `error`
dans les globs chauds et `none` ailleurs ; l'IL prouvé inchangé (0/0/0) ; les autres paquets et outils globaux non
installés (les listings, `MemoryDiagnoser` et `grep` les remplacent). *Pourquoi* : c'est le seul « possible » de la
règle, il lit les symboles et non le texte (le `grep` des chemins chauds se trompe 15 fois sur 16, § 1.5), et il ne
change rien au binaire. Il ne voit ni `virtual` ni `interface` déclarés : ceux-là restent au `grep`.

**D4 — Tenir le SDK et le runtime pendant G15.** Le réancrage est décidé (PLAN-G14, la vérification) ; épingler ne
l'est pas, et le réancrage ne couvre pas un SDK qui rendrait R inconstructible (§ 1.8).
*Proposition* : que Julien pose `apt-mark hold` sur les paquets `dotnet-*` et `aspnetcore-*` du début à la fin de
G15 (une action sur sa machine, hors du dépôt) ; sinon, la vérification d'avant chaque étape (runtime et SDK lus,
`/var/log/apt/history.log`) et le réancrage de § G14.1.0 s'ils ont bougé. *Pourquoi* : deux mises à jour en dix jours
(21/10, 31/10) ; un réancrage coûte ~2 h de machine, et un SDK qui casserait R arrêterait G15.

## Annexe A — Hors de la portée

Trouvé en route, non proposé (règle du 18/10) :
- **Des allocations compilées dans du code qui tourne** (les listings de R) : `x86seg_c.pmodeint`, `pmodeiret`,
  `pmoderetf`, `taskswitch286` (`NEWARR_1_VC`), `vid_svga.svga_poll` et `sound_sb_dsp.sb_dsp_setaddr` (`NEWSFAST`),
  les `*_recalctimings` de la SVGA ; leur chemin (froid ou chaud) n'est pas établi.
- **LINQ dans un initialiseur** : `Video/video.Materiel.cs:59` (`Enumerable.Range(…).Select(…).ToArray()`, un
  `static readonly`, donc une fois).
- **Deux `record` dans des fichiers soumis à R4** : `Ide/ide.cs:145` (`IDE_HDD_EMU`) et
  `Sound/sound_sb.Materiel.cs:378` (`Selection`) ; R4 interdit `record` dans le code transcrit.
- **Deux commentaires périmés de `iXtal26.csproj`** : `:52-64` (« EN COURS DE RÉCONCILIATION — exclus de la
  compilation », sans aucun `Compile Remove`) et `:71` (`InternalsVisibleTo iXtal26.Sst`, un assemblage qui n'existe
  pas, « à trancher », `:76-79`).
- **Le délai de 1 200 s** de `Banc.cs:166-169`, une horloge dans une porte déterministe.
- **Quatre délégués jamais instanciés** (`Disc/disc.cs:50`, `Ide/ide.cs:188-189`, `Video/vid_svga.cs:41`) : leurs
  champs restent `null`, comme chez PCem quand aucun appelant n'est transcrit (déduit).

## Annexe B — Les listes

**Les 23 fonctions mortes chez PCem aussi** (aucun appel dans `pcem-dev/src`, `grep -rnw`) :
`cdrom_null_audio_callback`, `cdrom_null_audio_stop`, `null_close` (`Cdrom/cdrom-null.cs:18`, `:21`, `:71`) ; `geteaw2`
(`Cpu/808x.cs:466`, appels commentés `808x.c:2629`, `:3855`, `:3878`) ; `gs` (`Cpu/x86.cs:163`, le `#define` de
`x86.h:143`) ; `disc_get_bitcell_period`, `disc_stop` (`Disc/disc.cs:218`, `:355`) ; `fdc_reset_fifo_buf`,
`fdc_get_rwc`, `fdc_get_boot_drive`, `fdc_update_boot_drive`, `fdc_update_drvrate` (`Floppy/fdc.cs:226`, `:313`, `:317`,
`:319`, `:325`) ; `fdd_setswap` (`Floppy/fdd.cs:216`) ; `clearpic` (`Ics/Intel8259A.cs:396`) ; `serial2_set_has_fifo`
(`Ics/National8250.cs:355`) ; `getstat` (`Ide/ide.cs:202`) ; `joystick_get_axis_name`, `_button_name`, `_pov_name`
(`Joystick/gameport.cs:391`, `:394`, `:397`) ; `lpt2_init` (`Lpt/lpt.cs:243`) ; `device_get_config_string`
(`PluginApi/device.cs:344`, une API de greffon) ; `cpu_readport` (`io.cs:184`) ; `ppi_reset` (`ppi.cs:29`). Et les 57
handlers `_a32` de `Cpu/x87_ops_808x.cs`.

**Les 47 fonctions dont l'appelant n'est pas transcrit** : `x87_reset` (`Cpu/x87.cs:98` ; l'appel de `808x.c:697`
est omis, `808x.cs:1179`), `flags_res_valid` (`Cpu/x86_flags.cs:377`, le recompilateur), `rom_init_interleaved`
(`Flash/rom.cs:156`), `fdc_update_is_nsc`, `fdc_datacrcerror`, `fdc_headercrcerror`, `fdc_add_pcjr`, `fdc_remove`,
`fdc_3f1_enable`, `fdc_set_ps1` (`Floppy/fdc.cs:309`, `:1306`, `:1323`, `:1459`, `:1468`, `:1486`, `:1488`),
`keyboard_at_init_ps2` (`Ics/Intel8042.cs:898`), `pit_clock`, `pit_set_using_timer` (`Ics/Intel8253.cs:650`, `:664`),
`serial1_set`, `serial1_set_has_fifo`, `serial2_set` (`Ics/National8250.cs:324`, `:332`, `:347`),
`ide_set_bus_master`, `ide_reset_devices` (`Ide/ide.cs:1355`, `:1365`), `gameport_update_joystick_type`
(`Joystick/gameport.cs:491`), `keyboard_tandy_init` (`Keyboard/keyboard_xt.cs:257`), `lpt_device_has_config`,
`lpt_get_device`, `lpt1_init` (`Lpt/lpt.cs:143`, `:151`, `:228`), `mem_read_romext`, `…w`, `…l`
(`Memory/mem.cs:1081-1084`), `mouse_emu_close` (`Mouse/mouse.cs:59`), `config_free_section`, `get_filename`
(`PluginApi/config.cs:115`, `:425`), `device_force_redraw`, `device_add_status_info` (`PluginApi/device.cs:226`,
`:243`), `paths_init` (`PluginApi/paths.cs:240`), `scsi_bus_match` (`Scsi/scsi.cs:483`), `cd_get_speed`,
`cd_get_model_interfaces`, `cd_get_model_speed` (`Scsi/scsi_cd.cs:341`, `:452`, `:455`), `sound_init`
(`Sound/sound.cs:102`), `cga_init` (`Video/vid_cga.cs:639`), `mda_setcol` (`Video/vid_mda.cs:446`),
`svga_set_override`, `svga_set_ramdac_type` (`Video/vid_svga.cs:242`, `:530`), `vga_disable`, `vga_enable`
(`Video/vid_vga.cs:104`, `:114`), `video_card_getdevice`, `closevideo` (`Video/video.cs:408`, `:696`),
`timer_get_ts_int`, `timer_set_p` (`timer.cs:327`, `:369`). Au-delà de quatre occurrences dans PCem, l'appelant n'a
pas été relu un par un (déduit).

**Les 45 `using` certainement inutiles** :
- `using iXtal26.Models;` (25) : `Cpu/386_ops_misc.cs:32`, `Cpu/808x.Materiel.cs:13`, `Ics/Intel8042.cs:50`,
  `Mouse/mouse_serial.cs:9`, `Sound/sound_opl.cs:15`, `sound_sb_dsp.cs:24`, `sound_speaker.cs:8`,
  `sound_speaker.Materiel.cs:11`, `Video/vid_cga.cs:24`, `vid_cl5429.cs:21`, `vid_ega.cs:21`, `vid_et4000.cs:21`,
  `vid_hercules.cs:22`, `vid_hercules.Materiel.cs:17`, `vid_mda.cs:22`, `vid_mda.Materiel.cs:17`,
  `vid_olivetti_m24.cs:21`, `vid_pc1512.cs:29`, `vid_s3.cs:28`, `vid_svga.cs:23`, `vid_tvga.cs:15`, `video.cs:12`,
  et dans `tools/iXtal26.Diff/` `CpuFingerprint.cs:16`, `MaterielCasFront.cs:12`, `MaterielCasLigne.cs:12` ;
- `using iXtal26.Memory;` (10) : `Cpu/386.State.cs:14`, `386.cs:29`, `386_ops_atomic.cs:8`, `386_ops_bit.cs:8`,
  `386_ops_bitscan.cs:8`, `386_ops_movx.cs:8`, `386_ops_stack.cs:32`, `386_ops_string.cs:34`,
  `tools/iXtal26.Diff/Bench.cs:36`, `BootProfile.cs:25` ;
- `using iXtal26.Diag;` (3) : `Cpu/808x.cs:28`, `Ics/Intel8259A.Materiel.cs:13`, `tools/iXtal26.Diff/PageCheck.cs:31` ;
- `using iXtal26.Sound;` (2) : `tools/iXtal26.Diff/EmuKernelCheck.cs:14`, `EmuTablesCheck.cs:14` ;
- `using iXtal26.Keyboard;` (1) : `tools/iXtal26.Diff/MaterielCas.cs:34` ;
- `using static` (4) : `Cpu/386_ops_movx.cs:11` (`x86_flags`), `Cpu/x87_ops_arith.cs:22` (`x86`),
  `Scsi/scsi_aha1540.Materiel.cs:19` (`Intel8259A`), `Video/vid_olivetti_m24.cs:24` (`PluginApi.device`).

**Les champs** : sans référence (55) : 15 d'`ega_t` (`chain4`, `clock`, `fast`, `firstline_draw`, `hdisp_on`,
`hdisp_time`, `htotal`, `interlace`, `lastline_draw`, `linecountff`, `linedbl`, `lowres`, `ma_latch`, `oddeven`,
`rowcount`), 8 vues d'union de l'EMU8000, 3 de `fdd_t`, `disc.fdc_ready` et `.motorspin`, 5 du DMA (dont 3 de
`dma_ps2_t`), 4 de `svga_hwcursor_t`, 3 de `gd5429_t`, 3 de `svga_t`, et 12 isolés. Écrits jamais lus (120) : 34
`materiel.pb_NN` (le noyau x87, posé en bloc : leurs gardes ne sont pas lues par champ ; R10 et le recensement
dépendent de leur forme, `.editorconfig:93-95`), 6 de PB-20, 27 dans `Video/` et `Sound/`, 6 de `x86` (dont
`codegen_*`), 5 de `CPU`, 5 de `HState` (le vecteur d'état que le harnais compare), et le reste isolé.
