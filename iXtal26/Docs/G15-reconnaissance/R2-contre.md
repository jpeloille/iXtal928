# R2 — Contre-lecture

*G15, reconnaissance, le 4 novembre 2026, au commit 34814ec. Lecture seule : rien construit, rien joué, rien installé.*
*Cible : `R2-nettoyage.md` (476 lignes). Niveaux : **CONFIRMÉ**, **IMPRÉCISION** (ce qui est juste), **REMARQUE**,*
***ERREUR** (la preuve ou la conclusion tombe). Les comptes ont été refaits (git grep, sed, python3 -I jetable hors du*
*dépôt, DLL de `/var/tmp/ixtal-g14/g145/`, journaux de VERIFICATION.md).*

## 0. En bref

1. Le fond tient : les comptes de `using`, d'avertissements, de pragmas, de `.editorconfig`, de durées, de réflexion et
   de SDK sont exacts ou à une unité près. Le relevé du code mort est bon sur l'échantillon (35 noms, aucun vivant).
2. **ERREUR 1** : le `pclog` de `vid_svga.cs:228` n'est pas du code mort à ôter. Il a dix appels dans le fichier,
   tous derrière `if (svga_output)` (constante 0) : l'IL n'en garde aucun, donc le relevé le croit mort. L'ôter casse
   la construction (CS0103) ou oblige à omettre des appels transcrits (R6). « 2 propres à iXtal » devient 1 (+ la
   cascade), et N1.2 perd une de ses trois lignes.
3. **ERREUR 2** : la porte de N1 (« listings du JIT 0/0/0 attendu », « l'IL : exactement ces méthodes disparues »)
   ignore la numérotation des lambdas et des fermetures, qui dépend du rang de la méthode dans son type : ôter une
   méthode morte placée avant une méthode à lambda renomme `<…>b__N_M` et `<>c__DisplayClassN_M` dans l'IL et dans les
   listings. La table de G14 ne le couvre pas, et le dépôt le sait (`386.Materiel.cs:26-28`).
4. Neuf imprécisions (§ 1) : `gs` classé « mort chez PCem » alors que PCem l'emploie (`808x.c:635`) ; « 531 sites »
   sont 531 couples méthode × aide ; « écrits jamais lus » inclut les champs que seul un initialiseur écrit ; etc.
5. Omissions (§ 3) : la renumérotation ci-dessus ; les 150 renvois `fichier.cs:N` que N1 décale (aucun recalage prévu) ;
   le générateur `tools/x87gen/gentab.py`, qui émet encore `opX87NonTranscrit` ; les `globalconfig` de style du SDK
   (`analysislevelstyle_*`) ; IDE0051/IDE0052 à `none`, qui sont le contrôle Roslyn du code mort privé.
6. Décisions : **D4 sort de G15** (action sur la machine de Julien ; le réancrage est décidé par PLAN-G14) ; **D2 est
   déjà tranchée** par la règle des étapes (PLAN-G13:188, PLAN-G14:176) sauf pour la clause du banc ; D1 et D3 restent
   ouvertes, mais D1 doit dire qu'elle borne le « code mort » de PLAN.md:570 à presque rien.
7. Bilan : 2 erreurs, 9 imprécisions, 8 remarques, 31 constats confirmés (§ 5).

## 1. Constats, un par un

### 1.1 Le code mort

**ERREUR — `pclog` de `vid_svga.cs:228` (R2 § 0, § 1.1, N1.2, D1).**
Preuve : `git grep -nw pclog -- iXtal26/Video/vid_svga.cs` rend, hors définition et commentaires, dix appels (`:1101`,
`:1138`, `:1452`, `:1488`, `:1843`, `:1850`, `:1881`, `:1888`, `:1978`, `:2013`). `vid_svga.cs:223` :
`private const int svga_output = 0;` ; `:15-19` : pragma CS0162 « les `pclog` gardés par `if (svga_output)` ». Une
branche gardée par une constante fausse n'émet aucun IL : le relevé par jetons voit zéro référence. Mais la source en a
dix, qui transcrivent `vid_svga.c` (R6). Le même cas existe dans `video.cs:66`, `mem_bios.cs:23`, `Flash/rom.cs:35`
(ces trois sont appelés pour de bon, `rom.cs:135`, `:166`, `:170`) : le shim n'est pas une dette d'iXtal.
Conséquences : (a) N1.2 ne doit pas l'ôter ; (b) le relevé est biaisé par construction pour tout ce qui n'est appelé
que sous une constante ou un `#if` (`PluginApi/device.cs:195`, `#if RELEASE_BUILD`) : à dire dans la liste des limites
de § 1.1 et à vérifier par un `git grep -w` de chaque candidat, avant tout retrait. `X87NonTranscrit` reste, lui,
vraiment mort (`git grep -nw X87NonTranscrit -- iXtal26 tools` : la définition `386_ops_fpu.cs:127` seule), avec
`opX87NonTranscrit` en cascade.

**IMPRÉCISION — `gs` classé « mort chez PCem aussi » (Annexe B, classe c).**
Preuve : `git grep -nw gs -- pcem-dev/src` rend `808x.c:635`, `printf("GS : base=%06X …", gs, …)` dans `dumpregs`, que
le C# n'a pas transcrit (`x87.cs:91` omet les dumps). `gs` a donc un appelant non transcrit : classe (d). Partage
corrigé : 22 + 48 (le total 129 ne bouge pas). Les 13 autres fonctions de la classe (c) testées sont bien mortes :
`cdrom_null_audio_callback`, `null_close`, `geteaw2`, `disc_stop`, `fdc_get_rwc`, `fdd_setswap`, `clearpic`,
`serial2_set_has_fifo`, `getstat`, `joystick_get_axis_name`, `lpt2_init`, `device_get_config_string`, `cpu_readport`,
`ppi_reset` : une définition et un prototype dans `pcem-dev/src` et `includes`, rien dans le reste de `pcem-dev/`
(`git grep -nw NOM -- pcem-dev ':!pcem-dev/src' ':!pcem-dev/includes'` : 0 pour toutes). `geteaw2` : les trois
« appels » de `808x.c:2629`, `:3855`, `:3878` sont des commentaires en fin de ligne.

**CONFIRMÉ — classe (d), 28 fonctions testées** (`x87_reset`, `flags_res_valid`, `rom_init_interleaved`, `fdc_remove`,
`fdc_set_ps1`, `keyboard_at_init_ps2`, `pit_clock`, `pit_set_using_timer`, `serial1_set`, `ide_set_bus_master`,
`gameport_update_joystick_type`, `keyboard_tandy_init`, `lpt1_init`, `mem_read_romext`, `mouse_emu_close`,
`config_free_section`, `paths_init`, `scsi_bus_match`, `cd_get_speed`, `sound_init`, `cga_init`, `mda_setcol`,
`svga_set_override`, `vga_disable`, `closevideo`, `timer_set_p`, `video_card_getdevice`, `fdc_remove`) : un seul
résultat de `git grep -nw` côté C# (la définition ; `x87_reset` et `sound_init` : un commentaire du harnais), des
appelants PCem dans `models/ps1.c`, `ps2_mca.c`, `hdd/hdd_esdi.c`, `qt-ui/qt-config.c:388` (`cd_get_speed`), etc.
Aucun de ces appelants n'est dans `iXtal26/Models/` (`ali1429`, `amstrad`, `model`, `nmi`, `olivetti_m24`).

**CONFIRMÉ — les 57 handlers `_a32` du 8087.** `git grep -cE "static int \w+_a32\(" -- iXtal26/Cpu/x87_ops_808x.cs` :
56 ; plus `FPU_ILLEGAL_a32` (`x87_8087.cs:233`, classe `_808x`) : 57. **IMPRÉCISION** : le fichier n'en porte que 56
(le 57e est dans `x87_8087.cs`), et la citation `x87_ops_808x_tables.cs:7-8` dit que le 808x n'appelle que les formes
a16 ; ce que R2 affirme (PCem compile les tables a32 sans les indexer) est vrai mais se lit à `x87_ops.h:315`
(`OP_TABLE(fpu_d8_a32)`) et `808x.c:3308` (seul `_a16` indexé).

**IMPRÉCISION — « 191 », un plancher.** Le relevé n'est pas fait jusqu'au point fixe (R2 le dit de la cascade
d'`opX87NonTranscrit`, mais § 0 donne 191 sans réserve) : `opX87NonTranscrit` n'y est pas, car `X87NonTranscrit` le
référence par `ldftn` (`386_ops_fpu.cs:130`). Les 191 sont donc au moins 192 une fois la cascade faite. Même effet, en
sens inverse, des tables de délégués : une entrée de table garde vivant un handler que l'opcode n'atteint jamais ; la
réflexion, elle, rend vivants des membres qu'aucun IL ne nomme. Le relevé borne, il ne mesure pas.

**IMPRÉCISION — « 120 champs écrits jamais lus ».** Preuve : `git grep -nw discrate -- iXtal26 tools` : une seule
occurrence, `fdc.cs:205`, `internal static int[] discrate = new int[2];` ; de même `discmodified` (`:204`),
`defaultwriteprot` (`disc.cs:98`, `= 0`), `fdc_indexcount` (`:108`, `= 52`), `oldtrack` (`:304`). PB-20
(`PCEM_BUGS.md:7781-7790`) dit `discmodified` et `discrate` « jamais écrits non plus ». Ils comptent « écrits » parce
que
l'initialiseur statique est un `stsfld` du `.cctor`. Idem pour les 34 `materiel.pb_NN` (`static readonly bool`,
`materiel.cs:22`). La rubrique mêle donc « posé par un initialiseur » et « écrit à l'exécution » ; le compte en deux
colonnes se refait d'un coup avec l'IL (écrit hors `.cctor`/constructeur).

**CONFIRMÉ — les champs sans référence** : les 15 d'`ega_t` (`vid_ega.h:24-59`) n'ont aucun `ega.X` dans `vid_ega*.cs`
(`git grep -nE
"\.(chain4|clock|firstline_draw|hdisp_on|lastline_draw|linecountff|linedbl|lowres|ma_latch|rowcount)\b"` :
0 hors `vid_svga`/`s3`/`tvga`) ; les hits de `fast`, `htotal`, `interlace`, `oddeven`, `hdisp_time` appartiennent à
`svga_t` et `cga_t`. PB-20 : `fdc_ready`, `motorspin` sans référence (une occurrence), `abort` écrit une fois
(`fdc.cs:218`), jamais lu. PB-19 absent : juste.

**CONFIRMÉ — les limites** : les six sites de réflexion (`386.Materiel.cs:314`, `MaterielCas.cs:514`,
`MaterielCasFrequence.cs:85-87`, `MaterielCasPit.cs:52` et `:55`, `GardeBanc.cs:93`) sont les seuls (`git grep` de
`GetField|GetMethod|GetProperty|GetFields|Activator|nameof|DynamicInvoke` : rien d'autre dans `iXtal26/` ni `tools/`,
hors JSON et Roslyn). Les 10 classes de `ConditionalWeakTable` : 12 tables, dont 2 de `StrongBox<bool>` (BCL) :
`Adresses`, `Mailboxes`, `Transferts`, `Cavaliers`, `Extensions` ×2, `Etats` ×4 = 10. Les imports `h_set_cs_ip`
(`Oracle.cs:167`), `h_get_core` (`:214`), `h_fastreadb` (`:219`) et `BootDiff.PasCsharp` (`BootDiff.cs:1072`) n'ont
qu'une occurrence dans `tools/iXtal26.Diff`. Projets clients d'iXtal26 : Diff et PerfBanc seuls
(`git grep ProjectReference -- '*.csproj'`) ; `iXtal26.Sst` (`iXtal26.csproj:71`) n'existe pas.

**REMARQUE — PB-20 comme contrôle positif.** Il vaut pour la lecture des jetons, pas pour la couverture : ses huit
symboles sont tous « sans lecture » ; aucun n'est une méthode. Le contrôle des méthodes manque (voir l'erreur 1 : un
second contrôle positif aurait été `pclog`, tombé du mauvais côté).

### 1.2 Les `using`

**CONFIRMÉ — 45 certainement inutiles.** Échantillon de 21 refait par un autre lecteur (source, hors IL : le nom
d'un type de l'espace n'apparaît nulle part dans le fichier hors commentaires) : 17 directives `using iXtal26.X;`
(`386_ops_misc.cs:32`, `Intel8042.cs:50`, `mouse_serial.cs:9`, `sound_opl.cs:15`, `vid_cga.cs:24`, `video.cs:12`,
`vid_s3.cs:28`, `CpuFingerprint.cs:16`, `386.cs:29`, `386_ops_stack.cs:32`, `Bench.cs:36`, `BootProfile.cs:25`,
`808x.cs:28`, `Intel8259A.Materiel.cs:13`, `PageCheck.cs:31`, `EmuKernelCheck.cs:14`, `MaterielCas.cs:34`) : 17 sur 17
sans aucun type de l'espace. Témoin positif du lecteur : 10 fichiers qui ont `using iXtal26.Models;` en emploient un
type. Les 4 `using static` (`386_ops_movx.cs:11`, `x87_ops_arith.cs:22`, `scsi_aha1540.Materiel.cs:19`,
`vid_olivetti_m24.cs:24`) : aucun des 34 membres de `x86_flags`, des 70 de `x86` ne figure dans le fichier concerné
(`x87_ops_arith.cs` ne nomme que `ST` et `cpu_state`, de `x87_ops.cs` et `386_common.cs:23`). Le compte global : 386
`using iXtal26.…;` (`git grep -hE "^\s*using iXtal26[A-Za-z0-9_.]*;"`) et 410 `using static` : exacts.
Aucun `using System` doublant un implicite (`git grep` : 0). Il n'y a pas d'extension (`git grep -E "\((this) [A-Z]"`
: 0).

**IMPRÉCISION — « 14 au commit R »** : le même lecteur sur `/var/tmp/ixtal-g14/ref-R` en trouve 13. Écart d'une
unité entre deux méthodes (jetons contre texte) ; le « 27 de plus qu'à R » devient 28. Sans effet sur la décision.

**CONFIRMÉ — la thèse IDE0005 / `GenerateDocumentationFile`.** Les trois propositions du R2 sont cohérentes : le
SDK porte `codestyle/cs/build/config/analysislevelstyle_recommended.globalconfig:11` (`IDE0005 = warning`) et un
diagnostic nommé `EnableGenerateDocumentationFile` (`analysislevelstyle_none.globalconfig:10-11`), signe que la règle
exige le fichier de documentation à la construction. Non rejoué (pas de construction) : documenté, déduit. Les
3 380 lignes `///` dans 103 fichiers sont exactes (`git grep -hE '^\s*///' -- 'iXtal26/*.cs' | wc -l`), les 12 lignes
à `&` ou `<` nus aussi (`grep -E '&[^a-z#]|<[^/a-zA-Z!]'`).
**REMARQUE** : CS1591 ne vise que le visible de l'extérieur. `git grep -cE '^\s*public '` donne 166 membres dans 26
fichiers d'`iXtal26/` : le plafond du risque est connu, il n'est pas « inconnu » (la plupart sont dans des types
`internal`, donc hors CS1591).

### 1.3 Les avertissements

**CONFIRMÉ** : `TreatWarningsAsErrors` dans 7 `.csproj` (`git grep -c`) ; `EnforceCodeStyleInBuild` dans 2
(`iXtal26.csproj:39`, `iXtal26.SoftFloat.csproj:22`) ; 0 `NoWarn|WarningsNotAsErrors|SuppressMessage|#nullable|
AnalysisLevel|AnalysisMode` ; ni `Directory.Build.props` ni `global.json`. 43 `severity = none`
(`grep -c`), 18 diagnostics distincts, aucune règle à `warning`/`error`, 0 `dotnet_naming`. 60 `#pragma warning
disable` ancrés dans 53 fichiers (`git grep -nE "^\s*#pragma warning disable"`), 104 codes : CS8602 ×33, CS8600 ×27,
CS8981 ×21, CS8604 ×16, CS1717 ×3, CS0162 ×3, CS8618 ×1. `808x.cs:1136` : le seul `restore`.
`g14-5/construction.log` : 3 fois `0 Warning(s)`. Le glob (`.editorconfig:40`) contient `Flash` : `rom.cs:12` est
superflu et son commentaire (`:9-11`) périmé. Les 22 fichiers transcrits hors du glob : `grep -l '^// ORACLE: pcem-dev'`
dans `Cdrom` (4), `Ide` (3), `Joystick` (4), `Lpt` (3), `Mfm` (2), `Scsi` (5) et `rtc.cs` (1) = 22. Les 21 CS8981 :
`Cdrom` 2, `Ide` 3, `Joystick` 4, `Lpt` 1, `Scsi` 9, `rtc.cs` 1, `Flash` 1. La Portée de `TRANSCRIPTION.md:99-101`
omet bien `Sound`, `Devices`, `Diag`, `Flash`, `Mouse`.

**IMPRÉCISION** : « 43 `none` … en six sections » (R2 § 1.3) : la liste qui suit en donne cinq, et le fichier cinq
(`:40`, `:80`, `:96`, `:101`, `:117`).
**IMPRÉCISION** : le journal cité, `g14-5/construction.log`, termine sur `iXtal26.dll` `721e592d…`, alors que la DLL
lue (en-tête de R2) est `cfd061ed…` (`sha256sum` : c'est bien celle de `g145/`). Deux constructions du même source
(la révision du commit s'inscrit dans la version) ; le « 0 avertissement » vaut pour les deux, mais le rapport devrait
le dire. Et seuls trois projets sur sept y figurent : le « 0 » des quatre autres est déduit de `TreatWarningsAsErrors`
et de leur construction dans la série, pas de ce journal.

**REMARQUE** : IDE0051 (membre privé inutilisé) et IDE0052 (jamais lu) sont à `none` pour le code transcrit
(`.editorconfig:75-76`) : ce sont les règles Roslyn du code mort *privé*. Les passer à `warning` sur une copie jetable
donnerait au relevé un second témoin, indépendant des jetons, qui aurait attrapé l'erreur 1 (le shim `pclog` est
utilisé en source) et vérifié les `private static` de N1 (`geteaw2`, `getstat`…). R2 les range dans la liste des
règles neutralisées sans en tirer cet usage.

### 1.4 `.editorconfig` et analyseurs

**CONFIRMÉ** : `analysislevel_10_minimum/recommended/all.globalconfig` : 92, 145 et 280 `severity = warning`
(`grep -c`) ; `default` : 0 (les règles actives par défaut ne passent pas par ce fichier). Niveaux 5 à 11 présents.
Les 16 « appels LINQ » du `grep` des chemins chauds : 15 `Math.Min/Max` et `video.Materiel.cs:59` (`Enumerable.Range`),
refait mot pour mot sur `Cpu/`, `Video/`, `Sound/`, `Ics/`.
**OMISSION** (§ 3) : le SDK porte aussi les `globalconfig` de **style** (`Sdks/Microsoft.NET.Sdk/codestyle/cs/build/
config/analysislevelstyle_{none,default,minimum,recommended,all}.globalconfig`) : `recommended` met IDE0005 (`:11`)
et IDE0040 (`:86`) à `warning`, `all` aussi IDE1006 (`:317`). C'est la voie sans liste à la main (`AnalysisLevelStyle`).

### 1.5 Les outils

**CONFIRMÉ** : SDK `10.0.114` seul (`dotnet --list-sdks`) ; `Microsoft.CodeAnalysis.NetAnalyzers.dll` et
`CSharp.CodeStyle.dll` présents ; `DotnetTools/dotnet-format` présent ; Roslyn `5.0.26.52326`
(`strings Microsoft.CodeAnalysis.CSharp.dll`), `Microsoft.CodeAnalysis.dll` et `CSharp.dll` sans `Workspaces`.
`jb`, `dotnet-counters`, `dotnet-trace` absents (`which`). **BannedApiAnalyzers n'est pas dans le SDK** : aucun RS0030
dans `NetAnalyzers.dll` (`strings -e l | grep -c RS0030` : 0) ; `~/.nuget/packages` ne le contient pas (il a
`microsoft.codeanalysis.analyzers`, qui est autre chose) : une restauration en ligne, comme dit. Ce qu'il demande, et
que
R2 ne dit pas : un fichier `BannedSymbols.txt` déclaré en `AdditionalFiles`, **unique pour le projet** (la sévérité
se règle par glob, la liste non) ; une version épinglée. **REMARQUE** : le paquet bannit des API par symbole, pas des
constructions (`new`, `params`, `virtual`, fermetures) : R2 le dit pour `virtual`/`interface`, pas pour `new`.
**IMPRÉCISION — « 531 sites »** (§ 1.5) : `awk` sur `ref-R-captures-10.0.14/jit/*.txt`, clé (méthode, aide) : 531
couples (NEWSFAST 310, NEWARR_1_VC 112, NEWARR_1_PTR 88, plus MAYBEFROZEN 8, NEWFAST 7, NEW_MDARR 6, que R2 omet),
358 méthodes (compte exact). Les sites eux-mêmes, clé (méthode, rang de l'appel) : 8 294 NEWSFAST, 3 063 NEWARR_1_VC.
Le texte doit dire « couples » ; la conclusion (« presque toutes des initialisations ») reste non vérifiée.
**REMARQUE** : l'annexe de PLAN.md (`:645-660`) range ReSharper CLT « dans la porte de G15 » et `dotnet-counters/trace`
pour « un amorçage complet » ; R2 les met en « utile, pas pour la porte » et D3 propose de n'installer que
BannedApiAnalyzers. C'est une proposition à présenter comme telle (chaque installation a son accord), pas comme
l'état des choses ; PLAN.md ne retire rien.

### 1.6 La porte

**CONFIRMÉ — durées** (table de § 1.7) : `VERIFICATION.md:17856-17859` (≈ 8 h, ≈ 7 h 15 de machine ; `g141` 1 h 51,
`g141-tout` 48 min, banc 65 min), `:18259-18261` (≈ 4 h 30 ; 1 h 52, 48 min, 53 min), `:18741-18743` (≈ 4 h 40 ;
1 h 54, 49 min, 65 min), `:19332-19335` (≈ 5 h ; 1 h 54, 49 min, 53 min, rejeu de 19 min). La somme de la machine
(15 + 2 + 53 + 112 + 48 min pour G14.2) tombe sur 3 h 50 : les « ~4 h » sont justes. Le délai de 1 200 s :
`Banc.cs:166-169` exact ; dit frappé en G14.0d, G14.1, G14.4 (`VERIFICATION.md:19713-19715` : « trois fois »).
**IMPRÉCISION** : la résolution du banc citée (`:19554-19561`) omet `MemoireLente` (+1,32 %), qui figure à la source.

**CONFIRMÉ — la table refuse les membres.** `tools/noms/g14.txt:28-29` (« Les membres … ne sont jamais dans la table …
Une entrée qui désigne un membre est refusée ») ; `tools/ildump/Program.cs:35-42` (`FormatException` « désigne un
membre … la table ne renomme que des types ») ; `tools/listings-jit.sh:30-36` (le même refus). Le texte se borne à
des types (borne `(?<![\w.])`), les en-têtes et les appels portent la signature (`call
[iXtal26.Models.pic:picint(ushort)]`
dans `ami386dx-387.txt`) : la normalisation des `enum` doit donc agir aussi sur les sites d'appel, pas seulement sur
l'en-tête. **IMPRÉCISION** : R2 écrit « l'en-tête du listing » ; les appels sont aussi touchés.

**CONFIRMÉ — « BenchmarkDotNet reconstruit R depuis son source avec le SDK du jour ».** `compare-ref.sh:25-29` et
`:49-51` (« BenchmarkDotNet reconstruit le banc et iXtal26 dans un répertoire qu'il génère … les DLL lancées ne sont pas
celles de l'arbre ») ; `VERIFICATION.md:17511-17513` (« reconstruit le banc depuis le source avec le SDK du jour »)
et `:17515-17520` : sous 10.0.114, le gel de 10.0.14 est celui de 10.0.13 « à l'octet », « à la normalisation près »
(`efb54e43…`, `23d2f857…`, `c856ed99…`). **REMARQUE sur le risque** : l'expérience du 31/10 va contre l'inquiétude de
R2 pour un *correctif* (SDK .113 → .114 : mêmes DLL de R) ; le danger de « nouveaux avertissements CA qui font échouer
R » est réel pour une *bande de fonctionnalités* (un SDK 10.0.2xx), pas pour un correctif. À écrire ainsi, sans quoi
D4 paraît plus pressant qu'il n'est.

**ERREUR — « listings du JIT contre R : 0/0/0 attendu » pour N1** (voir aussi § 0.3). Preuve : les listings de R
portent des noms de méthodes générées à numéro : `iXtal26.BootTest+<>c:<Run>b__0_0(int)`,
`iXtal26.Cpu._386+<>c:<.cctor>b__8_0():bool` (`grep -h "^; Assembly listing" jit/*.txt | grep -c "<>c\|b__"` : 1 547
en-têtes sur les 31 captures). Le premier numéro est le rang de la méthode dans son type, donc ôter une méthode morte
placée avant une méthode à lambda (ou à fermeture) change ces noms, dans l'IL (`ildump`, les types
`<>c__DisplayClassN_M`) comme dans les listings. Le dépôt l'écrit déjà (`386.Materiel.cs:26-28` : « des membres de plus
… décaleraient les numéros que le compilateur donne à ses lambdas et à ses classes de fermeture, et les listings du JIT
changeraient de noms »). La table de G14 ne renomme que des types nommés. N1 (retrait de méthodes) et tout autre
retrait ou réordonnancement de membres de G15 ont ce défaut. À corriger : une normalisation des noms générés (numéros
effacés, ou numérotation relue sur le parent) dans `ildump comparer` et `listings-jit.sh`, dans N0 ; sinon l'écart
attendu est faux pour toute classe qui contient une lambda.

**CONFIRMÉ** : le contrôle négatif `ILLEGAL` (`386.Materiel.cs:312-315`, `typeof(_386).GetMethod("ILLEGAL", …) ??
throw`,
dans un `static readonly` de `_386_materiel`, ligne 29 ouvre la classe) est bien conçu.

### 1.7 Les décisions

**D1 — ouverte, mais à reformuler.** PLAN.md:570 dit « nettoyage (code mort, `using`, avertissements) », décision
de Julien. La proposition de R2 en garde : les `using`, 1 fonction (`X87NonTranscrit`, la seconde, `pclog`, étant
fausse), `BootDiff.PasCsharp`, un pragma. Elle ne **précise** pas le « code mort » de Julien, elle le **réduit** : tout
le code mort *transcrit* (80 + 47, 55 champs) reste. C'est défendable par R6 (`TRANSCRIPTION.md:67`) et R1 (e)
(`:30`), mais la question à Julien doit le dire en ces termes : « le code mort de PLAN.md:570 est-il celui d'iXtal
seul ? »
L'argument « les 47 sont l'API d'une machine future » est exact à `fdc.cs:47` ; celui des listings (« une méthode non
appelée n'est jamais compilée ») l'est aussi (`TieredCompilation` à `false`, `iXtal26.csproj:34`), mais il ne vaut pas
pour
les champs (voir 1.1).
**D2 — déjà tranchée, sauf le banc.** La règle des étapes est écrite : `PLAN-G13.md:188` (« une série entière par étape
qui change l'émulateur … un sous-ensemble ciblé sinon ») et `PLAN-G14.md:176`, appliquée en G14.5
(`VERIFICATION.md:19678`). Ce que PLAN.md:592-596 (« à chaque commit … le banc ») ajoute est la seule vraie question :
la mesure contre R pour un commit d'IL identique. À garder, en une ligne, et à retirer pour les séries.
**D3 — ouverte** (l'annexe `PLAN.md:645-652` la prévoit, l'accord se demande au moment venu). Voir la remarque de 1.5
sur
les quatre autres outils.
**D4 — à sortir de G15.** `apt-mark hold` est une action sur la maintenance de la machine de Julien, hors dépôt et hors
PLAN.md ; PLAN-G14 a déjà décidé ce qui se fait quand .NET bouge (la vérification d'avant étape et le réancrage, §
G14.1.0,
`VERIFICATION.md:17466`). Le rapport de G14 avait écrit la même chose (`G14-reconnaissance/R0-contre.md:248-251`) en
nommant aussi la parade qui ne touche pas au système : une copie du runtime et du SDK sous `/var/tmp` (`DOTNET_ROOT`).
Une « étape » ou une décision qui demande à Julien de modifier ses mises à jour élargit G15 ; la place est en annexe
« Hors de la portée », une ligne, ou dans les risques avec la vérification d'avant étape comme seule parade.
**Élargissements de PLAN.md** : N0 point 3 (le fichier d'écart attendu) et 4 (table des membres, normalisation des
enum) servent les renommages et les `enum` que PLAN.md:568-569 nomme : dans la portée. N2.1 (`AnalysisLevel` épinglé)
l'est aussi (« analyseurs »), à condition que ce soit dit comme tel. D4 est le seul hors portée.

## 2. Échantillon du code mort (tableau)

- (c) `cdrom_null_audio_callback`, `null_close`, `geteaw2`, `disc_stop`, `fdc_get_rwc`, `fdd_setswap`, `clearpic` :
  C# une occurrence ; PCem définition et prototype. CONFIRMÉ.
- (c) `serial2_set_has_fifo`, `getstat`, `joystick_get_axis_name`, `lpt2_init`, `device_get_config_string`,
  `cpu_readport`, `ppi_reset` : idem ; `pcem-dev` hors `src` et `includes` : 0. CONFIRMÉ.
- (c) `gs` : `808x.c:635` l'emploie (`dumpregs`). Mal classé, c'est une (d).
- (d) les 28 de 1.1 : un appelant PCem hors des machines transcrites. CONFIRMÉ.
- (b) 56 + `FPU_ILLEGAL_a32` : aucune table a32 dans `x87_ops_808x_tables.cs`. CONFIRMÉ.
- propre : `X87NonTranscrit`, définition seule ; `opX87NonTranscrit` en cascade. CONFIRMÉ.
- propre : `pclog` de `vid_svga.cs:228`, dix appels sous `svga_output` (const 0). **ERREUR**.
- champs : `discrate`, `discmodified`, `defaultwriteprot`, `fdc_indexcount`, `oldtrack` : une occurrence, écrits
  par un initialiseur. IMPRÉCISION.
- champs : `fdc_ready`, `motorspin`, `abort`, 15 de `ega_t` : une occurrence, ou écrit une fois et jamais lu.
  CONFIRMÉ.

Au total 35 noms de méthode ou de champ tirés des trois classes (a-b, c-d, propre), 15 champs de plus ; les tables
d'opcodes (`ldftn` de la table) ne faussent pas le compte vers le haut (un handler en table est vivant par jeton) mais
le
faussent vers le bas, comme dit en 1.1 ; la réflexion est limitée aux six sites, qui visent `ILLEGAL`, `memcycs`,
`setadc8`, `speaker_pos`, `speaker_buffer` et les `_iir_x/_iir_y` de `sound_sb`, aucun de la liste des 191.

## 3. Ce que le rapport omet

1. **La renumérotation des lambdas et des fermetures** (erreur 2) : à ajouter à N0 (normalisation) et aux risques.
2. **Le décalage des renvois `fichier.cs:N`.** N1 ôte des lignes (45 `using`, des méthodes) de fichiers que le code et
   la documentation citent par numéro de ligne : `git grep -hoE "\b[A-Za-z0-9_.]+\.cs:[0-9]+"` donne 150 occurrences
   dans 56 fichiers de code, plus `PCEM_BUGS.md` (`vid_svga.cs:976` par exemple) ; G14.5 a recalé 24 renvois à la main
   (commit `34814ec`). Ni N1 ni § 3 ne prévoit leur recalage ni son contrôle (`check-doc5150.sh` ne couvre que
   Docs/ibm5150). Les `using` en tête de fichier décalent *tout* le fichier.
3. **Le générateur `tools/x87gen/gentab.py`** : lignes 42 et 74, il émet `=> opX87NonTranscrit(fetchdat)` et le
   commentaire de `x87_ops_tables.cs:12` ; ôter la fonction rend le générateur périmé. Un renvoi à nettoyer dans N1.2
   (R2 cite `cpu.cs:328` et `x87_ops_tables.cs:12`, pas le script).
4. **`AnalysisLevelStyle`** et les `globalconfig` de style du SDK (1.4) : la façon la plus courte d'activer IDE0005 et
   IDE0040, puis de les régler par glob.
5. **IDE0051/IDE0052** comme second relevé du code mort privé (1.3).
6. **`dotnet format style --diagnostics IDE0005 --verify-no-changes`** : R2 range `dotnet-format` dans « utile » mais
   ne l'ajoute pas aux « deux voies sans paquet » de § 1.2 ; c'est la troisième (elle ne passe pas par
   `GenerateDocumentationFile`). À essayer sur une copie jetable, avec l'accord du dépôt sur les constructions.
7. **Le fichier `BannedSymbols.txt`** unique, et sa version (1.5).
8. **La parade `DOTNET_ROOT`** déjà proposée en G14 (D4).
9. **Le plafond de CS1591** : 166 membres `public` (1.2).
10. Une phrase sur `ImplicitUsings` : IDE0005 y verrait les `using System.…` redondants ; il y en a 0 (`git grep` des
    directives `System`, `System.IO`, `System.Linq`, … : 0), donc rien à ôter, mais la vérification manque au texte.

## 4. Les remarques (hors constats chiffrés)

- La proposition « N2 en dernier » et « N0 avant tout renommage » est cohérente avec PLAN.md (le banc d'ouverture avant
  N0) ; seule la dépendance de N1 à la normalisation des noms générés (erreur 2) la complique.
- Les 22 fichiers transcrits hors glob : l'ajout de `Cdrom|Ide|Joystick|Lpt|Mfm|Scsi` au glob ferait tomber 20 des 21
  pragmas CS8981 (hors `Flash`), mais lèverait IDE0011, IDE0045/46, IDE0055, CS0649… sur ces fichiers pas encore
  passés par la même épreuve que `Ics/` (commentaire `.editorconfig:46-58`) : à essayer en copie jetable avant N2.
- `iXtal26.csproj:52-64` et `:71` (commentaires périmés) sont bien en annexe A, hors de la portée ; correct.

## 5. Bilan chiffré

- **ERREUR : 2.** `pclog` (1.1) ; porte de N1 sans renumérotation des noms générés (1.6).
- **IMPRÉCISION : 9.** `gs` (c vers d) ; 57e handler et citation `tables.cs:7-8` ; « 191 » plancher ; « écrits jamais
  lus » ; « 14 au commit R » (13) ; « six sections » (cinq) ; journal `721e592…` et DLL `cfd061ed…` ; « 531 sites » ;
  résolution du banc sans `MemoireLente` (avec l'« en-tête » des enum, qui touche aussi les appels).
- **REMARQUE : 8.** PB-20 contrôle positif des champs seulement ; CS1591 borné par 166 ; IDE0051/52 ; BannedApi par
  symbole ; annexe de PLAN et les quatre autres outils ; risque du SDK par bande de fonctionnalités ; 0 avertissement
  des quatre autres projets déduit ; glob et pragmas CS8981.
- **CONFIRMÉ : 31.** Classes (c) ×14 et (d) ×28 ; 45 `using` (17 + 4 relus) ; 386 et 410 directives ; IDE0005 ;
  3 380 `///` ; `TreatWarningsAsErrors` ×7 ; 43 `none` ; 60 pragmas ; 22 fichiers hors glob ; `rom.cs:12` ; SDK
  10.0.114 ; RS0030 hors SDK ; 6 sites de réflexion ; 10 `ConditionalWeakTable` ; durées G14.1-4 ; délai 1 200 s ;
  refus des membres ; reconstruction de R ; 92, 145 et 280 ; 16 « appels LINQ » ; les imports `h_*`.

Comptes refaits : méthodes testées 14 (c) + 28 (d) + 57 (b) + 1 propre + `pclog` = 100 noms ; champs 15 + 8 ; `using` 21
sur 45 ; pragmas 60/53/104 ; globs 22/21 ; sites de réflexion 6 ; durées 4 sur 4.

## 6. Corrections à porter au plan

1. **Retirer `pclog` (`vid_svga.cs:228`) de N1.2**, de § 0 et de D1 ; « 2 propres à iXtal » → « 1 propre
   (`X87NonTranscrit`),
   plus sa cascade `opX87NonTranscrit` ». Ajouter à la liste des limites du relevé : les appels sous une constante
   fausse
   ou un `#if`. Chaque candidat : un `git grep -w` avant retrait.
2. **Ajouter à N0 la normalisation des noms générés** (`b__N_M`, `<>c__DisplayClassN_M`) dans `ildump comparer` et
   `listings-jit.sh`, ou la relecture de la numérotation sur le parent ; rectifier « 0/0/0 attendu » de N1 en
   conséquence (§ 1.6) et le contrôle négatif : un témoin qui ôte une méthode avant une lambda.
3. **Reclasser `gs`** : (d), pas (c) ; 22 + 48 ; écrire « 56 + `FPU_ILLEGAL_a32` (`x87_8087.cs:233`) = 57 ».
4. **« 191 » devient un plancher** (« au moins 192 avec la cascade »), et les 120 champs se séparent en « écrits hors
   initialiseur » et « posés par un initialiseur » (un `stsfld` du `.cctor` n'est pas une écriture d'exécution).
5. **Ajouter à N1 le recalage des renvois `fichier.cs:N`** (150 occurrences dans 56 fichiers, `PCEM_BUGS.md` compris)
   et son
   contrôle, ou décider qu'ils se lisent « à une ligne près » comme en G14 ; y joindre `gentab.py:42, :74`.
6. **D2** : fermer la partie séries (règle de PLAN-G13:188 / PLAN-G14:176) ; ne garder que la clause du banc de
   PLAN.md:592-596. **D4** : sortir des décisions de G15 (annexe « Hors de la portée » ou risque avec sa parade de
   vérification) ; y noter `DOTNET_ROOT`. **D1** : la poser comme « le code mort de PLAN.md:570 est-il celui d'iXtal
   seul ? ». **D3** : séparer l'accord de BannedApiAnalyzers de celui des quatre autres outils de l'annexe.
7. **Compléter § 1.4 et N2** : les `globalconfig` de style du SDK (`AnalysisLevelStyle`), IDE0051/IDE0052 à `warning`
   sur
   une copie jetable comme second relevé, `dotnet format style --diagnostics IDE0005` comme troisième voie ; le fichier
   `BannedSymbols.txt` ; le plafond de CS1591 (166).
8. **Petites rectifications** : « 531 couples méthode × aide » ; « cinq sections » ; la résolution du banc avec
   `MemoireLente` ; les enum touchent aussi les appels des listings ; le journal et la DLL (`721e592…` / `cfd061ed…`) ;
   « 13 ou 14 » `using` au commit R ; le risque du SDK dit par bande de fonctionnalités, la preuve du 31/10 à l'appui.
