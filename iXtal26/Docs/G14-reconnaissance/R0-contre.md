# R0 — La contre-lecture

> Contre-lecture du 23 octobre 2026, au commit `1d49a7f`, de `R0-reference.md`. LECTURE SEULE : rien n'a été
> construit, lancé ni mesuré. Chaque constat de R0 a été relu dans le dépôt ; les comptes ont été refaits avec leurs
> commandes. Classement : **exact**, **imprécis** (ce qui est juste est dit), **faux** (la preuve est donnée).
> Niveaux : vérifié (relu), déduit, inconnu. Les commandes sont à l'annexe B.

## 0. En bref

1. Les faits de R0 sur le dépôt tiennent : six bancs, 52 cas, IF = 0 (`FetchBanc.cs:86`, `Exec386Banc.cs:123`),
   Is486Banc sans chiffre, 1 507 méthodes, 42 de `pic`/`pit`/`dma`, 39 qui les nomment : tout recompté, égal.
2. Trois erreurs de fait : « la lecture littérale » de D1 oublie deux modèles de la table (`ibmat`, `ami386`, soit
   neuf modèles, non sept) ; « le même code, deux états » (156,1 puis 156,2 µs) compare deux versions du code ;
   `ami486 … sept variantes` est sept profils, donc six variantes.
3. Le protocole côte à côte est faisable tel quel avec BenchmarkDotNet 0.15.8 : tous les drapeaux existent. Mais le
   comparateur ne peut pas tout lire dans le JSON : les états imprimés par `Preparer` ne sont que dans le journal, et
   l'affinité du JSON est celle du job (vingt bits à 1), jamais celle de `taskset`.
4. Le critère chiffré ne tient pas la règle de PLAN.md tel qu'il est écrit : l'intervalle de BenchmarkDotNet
   suppose des itérations indépendantes, ce que le bruit entre processus (2,5 à 5 %) dément ; la condition
   « intervalles disjoints » est donc trompeuse, et c'est le seuil s qui décide. Le plancher fixe de 1 % relâche la
   règle là où le bruit est le plus bas (0,1 à 0,5 % sur Exec386). Une analyse sur les moyennes de lancement règle
   les deux défauts.
5. MachineBanc ne prouve pas ce que R0 lui fait prouver : le PIC et le PIT n'y sont touchés qu'à ~18 Hz si le
   code invité est celui d'un amorçage sans disque (déduit) ; rien dans R0 ne dit quelle part du temps de chaque cas
   passe par les modules que G14 renomme (inconnu, et c'est la mesure qui manque).
6. L'estimation de durée est trop basse d'un facteur 1,4 à 2,6 (§ 1.14) ; la forme R C R C par passe entière
   répartit mal la dérive : un entrelacement par classe de banc est meilleur.
7. D1 est mal posée (neuf modèles) ; D2 et D5 sont ouvertes ; D3 est ouverte mais autrement : la question n'est pas
   « quel s » mais « comment compter le bruit sans tolérance non autorisée » ; D4 est à moitié tranchée par PLAN.md
   (la liste des chemins chauds) et son « aucun sous-ensemble » est plus dur que le plan.
8. Deux bancs sont sans objet pour G14 : GardeBanc et Is486Banc mesurent des copies définies dans le banc, jamais le
   code que G14 renomme (8 des 52 cas). Les garder dans la référence ne coûte rien mais ne prouve rien.
9. Manquent : la part de temps des modules renommés par cas, le moyen de savoir quelle DLL tourne vraiment, le
   chemin des états imprimés, la perte de la capture des listings par la mise à jour de .NET, le choix de ce qu'est
   une « machine » pour les IPS.
10. À corriger avant le plan : § 4 (quinze points).

## 1. Constat par constat

### 1.1 Les six bancs, 52 cas — exact (imprécision de l'annexe B)

Recomptés : `grep -c '\[Benchmark' tools/perfbanc/*.cs` rend Exec386 6, Fetch 3, Garde 4, Is486 4, Son 6, Video 4
(27 méthodes). Avec les `[Params]` : Garde 4 ; Is486 2 classes × 2 = 4 ; Fetch 3 ; Exec386 3 classes × 2 = 6 ;
Video 4 classes × `Lectures` (0, 16) = 8 ; Son 4 + 4 + 4 + 3 + 3 + 3 × 3 = 27. Total 4 + 4 + 3 + 6 + 8 + 27 = **52**
(vérifié). Imprécis : le n-uplet « (4, 3, 6, 4, 4, 6) » de l'annexe B n'est pas dans l'ordre de la commande
(alphabétique : 6, 3, 4, 4, 6, 4) ; la somme, elle, est juste.

### 1.2 Communs aux bancs — exact

BenchmarkDotNet 0.15.8 (`iXtal26.PerfBanc.csproj:30`), référence au projet (`:25`), `InternalsVisibleTo`
(`iXtal26/iXtal26.csproj:73`), `TieredCompilation` à `false` (`csproj:20`), job mutateur (`Program.cs:30`),
artefacts (`:32`), `MemoryDiagnoser`. Le CSV porte `Affinity=11111111111111111111` (vingt bits) ; les rapports
disent « i5-14600KF, 20 logical and 14 physical cores », « .NET 10.0.12 », SDK 10.0.112 (vérifié). Le projet n'est pas
dans `iXtal26.sln` (`grep 'Project(' iXtal26.sln` : deux projets). `VERIFICATION.md:12988` : « aucune porte ne
construit PERFBANC » (vérifié).

Imprécis : « le job par défaut : nombre d'itérations choisi par BenchmarkDotNet, un lancement » est juste, mais
le fait utile pour le protocole n'est pas dit : chaque `Preparer` affiche une ligne `// Banc (mode, boucle) : après un
appel, BX = …` qui n'arrive que dans le journal `BenchmarkRun-*.log` (par exemple `…052912.log:28`), pas dans les
rapports. Voir § 1.8.

### 1.3 GardeBanc — exact, mais sans objet pour G14

Chiffres relus dans `GardePb01-report-github.md` : 4,918 (± 0,0088), 4,920 (± 0,0041), 4,830 (± 0,0053),
4,779 (± 0,0024) ns, rapports 1,00 / 0,98 / 0,97 : égaux à R0. `GardeBanc.cs:79` (`Appels = 1000` à la ligne 79),
`:91-112` (équivalence par réflexion, `GetMethod("setadc8")` à `:93`) : exact. Ce qu'R0 ne dit pas : les quatre
cas mesurent des copies de `setadc8` écrites dans le banc, appelées par un délégué. Aucun code d'iXtal26 que G14
renomme n'est exécuté en cours de mesure (hors `_808x.Reset`). Même chose pour Is486Banc (copie `CmpsbSans10`,
`Is486Banc.cs:168`). Voir § 3 (D4).

### 1.4 Is486Banc — exact

Pas de chiffre consigné : `grep -n 'Is486\|Cmpsb' VERIFICATION.md` ne rend rien ; `D7-mecanisme.md:169` « Aucun résultat
n'est consigné » (vérifié). Premier commit `dc03cf6` (07/10, `git log --follow`), `Appels = 1000` (`:75`), le
handler pris dans `ops_386[0xA6]` (`:106`), `Reset386`/`Reset486` (`:93-95`) : exact. Aucun rapport `Cmpsb*` n'existe
sous `bin/…/results/` (vérifié).

### 1.5 FetchBanc — exact

`Cycles = 100_000` (`:56`), `Code` à `:58`, `FLAGS = 0xF002` (`:86`) : le bit 9 (IF = 0x200) est à zéro (vérifié).
Chiffres du rapport : 114,8 (± 2,29), 125,0 (± 2,47), 127,6 (± 2,53) µs ; écart type 2,14 / 2,64 / 5,33 ; 56 B alloués
(un `new ushort[R.COUNT]` par appel, `FetchBanc.cs:80`) ; itérations de 120,9 à 133,7 µs dans le journal
(`BenchmarkRun-20261008-200356.log:139-143`). Tout égal. « Le nombre d'instructions n'est pas imprimé » : exact.

### 1.6 Exec386Banc — exact, un point imprécis

`Reset386` (`:98`), `timer.timer_target` (`:125`), `FLAGS = 0xF002` (`:123`), les trois codes (`:88-90`) : exact.
Chiffres relus : 113,4 (± 0,43), 132,2 (± 0,13), 138,5 (± 0,22), 169,5 (± 0,32), 156,2 (± 0,83), 154,3 (± 0,33) :
égaux ; marges de 0,1 à 0,5 % : 0,38 / 0,10 / 0,16 / 0,19 / 0,53 / 0,21 % (calculées). L'i386SX/16 : `FuzzCpu` vaut
0 (`386.State.cs:25`), `cpus_i386SX[0]` est « i386SX/16 » (`cpu_tables.cs:155`) ; exact (déduit). Conséquence que R0
ne tire pas : la machine mesurée est un 386SX à bus de 16 bits, alors que les profils de la racine utilisent
`ami386dx` (cpu = 3, i386DX/33) ; la boucle ne représente pas le chemin du profil (voir § 2).

### 1.7 VideoBanc, SonBanc — exact

VideoBanc : `Depot()` aux lignes 92-98, la config à `:108-110`, la trame à `:154-170` ; 8 cas ; chiffres de
`VERIFICATION.md:11823-11832` : 24,8 / 190,7 ; 52,9 / 309,8 ; 29,8 / 32,1 ; 84,8 / 151,9 (vérifié). SonBanc : 27 cas,
aucun `Baseline` (`grep -n Baseline tools/perfbanc/SonBanc.cs` : vide) ; 207,3 à 949,4 µs
(`VERIFICATION.md:13005-13019`) ; bruit « de 10 à 20 µs, une fois 63 µs » : exact. Imprécis : le « 2,5 à 5 % » de R0
(§ 1.4) est une dérivation (10/400, 20/400) et non une dispersion consignée ; sur l'ADPCM (207 µs) le même 20 µs
fait 10 %. Le « 13 % » est 63/488 (calculé), pas un chiffre du journal.

### 1.8 Ce que R0 attend du JSON — imprécis

R0 § 2.1 : le comparateur « vérifie runtime, SDK, environnement, affinité, allocations, états et empreintes » à partir
des JSON. Relu :
- l'affinité du JSON (et du CSV) est la propriété `Affinity` du job, `11111111111111111111` même sans épinglage ;
  `taskset` restreint le processus de l'extérieur sans la modifier : le contrôle « l'affinité diffère » est vide ;
  la vérification utile est `taskset -p`/`/proc/PID/status` pris par le script, et `setarch -R` (une personnalité de
  processus) n'a aucune trace dans le JSON (déduit) ;
- l'état imprimé par `Preparer` n'est que dans le journal (`grep '// Exec386Banc' BenchmarkRun-…052912.log` : lignes
  28, 196, 362, 525). Le comparateur doit lire les journaux ; et ces états sont faibles : trois registres (BX, CX, IP).
  Pour la vidéo et le son c'est une somme, pour la machine entière une empreinte (instructions, cycles) à créer ;
- l'allocation exacte est dans le JSON (`Memory.BytesAllocatedPerOperation`), et elle est utilisable : 56 B constants
  dans tous les cas Fetch et Exec386 (rapports relus).

### 1.9 Le « mode tout » n'est mesuré que par FetchMateriel — exact

Relu : les cas d'Exec386 prennent PB-51 ou PB-189, VideoBanc un PB, SonBanc des PB et le domaine « son ». Seul
`FetchPb87`/`FetchMateriel` prend « tout » (`FetchBanc.cs:36-52`). Exact.

### 1.10 Le PIC jamais lu — exact ; les « minuteries » — imprécis

`Cpu/386.cs:478-480` : le test d'interruption commence par `(flags & I_FLAG) != 0 &&` avant `Models.pic.…` ; `808x.cs:
766-768` de même : à IF = 0, le PIC n'est pas lu (vérifié). Imprécis : « Seul SonBanc traverse le 8237 et les minuteries
»
confond `timer.cs` (le planificateur d'événements de l'hôte, que SonBanc et VideoBanc avancent à la main) avec le
PIT 8253 (`Models/pit.cs`, dont les rappels `pit_timer_over` ne s'arment que lorsque l'invité programme les compteurs,
`pit.cs:232-269`). SonBanc programme le 8237 et la SB16 par leurs ports ; rien dans `SonBanc.cs` n'écrit le port 40h-43h
(vérifié par `grep -n 'io\.out[bwl]\?(0x4[0-3]' SonBanc.cs` : aucun). Le PIT n'est donc touché par aucun banc existant
(déduit). Les
bancs ne nomment aucun des modules de G14 (`grep -n 'pic\.\|pit\.\|dma\.' tools/perfbanc/*.cs` : vide) ; ils nomment
`vid_ega.Probe`, `vid_svga.svga_get_pri`, `sound_sb.sb_pri`, `sb_dsp_t`, `sound`, `timer`, `Models.model_c` : si
G14 en renomme un, le banc du candidat est à suivre à la main, comme R0 le dit.

### 1.11 Les autres mesures de vitesse — exact, une cause expliquée

`Bench.cs:76-78` (le 8088 par construction), M5.1 (`VERIFICATION.md:780-788`, Core Ultra 7 258V, `:770`), GR.4d
(`:15887-15894`, « n'est pas une référence de performance (G14 la posera) »), `TimerCheck.cs:408-420`, `:504-508`,
`SdlHost.cs:1324`, audit du 26/09 (`audit-performance-2026-09-26.md:15, :33`) : tout égal.
Imprécis : R0 § 1.4 donne la marge de l'ami486 (3,88 le 10/10, 8,99 le 21/10) pour une « cause inconnue ». Le dépôt
l'éclaire : G13.5b dit « ami486 (DX2/66, 33 MIPS invités) » (`VERIFICATION.md:8148-8150`), GR.3h compte 667 000 618
instructions pour la fenêtre `ram` (`:15458`), soit ~11 MIPS sur 60 s (si la fenêtre est de 60 s ; déduit). La
marge dépend du nombre d'instructions que l'invité exécute dans la fenêtre : elle n'est comparable qu'à code invité
égal. C'est l'argument qui fait des instructions par seconde de l'hôte (capacité ÷ CPI) l'unité à retenir pour
MachineBanc, plutôt que la marge.

### 1.12 L'hôte, le runtime — exact

`lscpu -e` : cœurs 0-11 à 5 300 MHz (six cœurs P, deux fils), 12-19 à 4 000 MHz ; gouverneur `powersave` ;
`dotnet --list-runtimes` : 10.0.13 seul ; `--list-sdks` : 10.0.113 seul ; `/var/log/apt/history.log` : mise à jour par
`unattended-upgrade` le 2026-10-21 06:18:11 ; `20auto-upgrades` : `Unattended-Upgrade "1"` (tout vérifié). Imprécis :
« processeurs 0-11 … deux fils par cœur » donne 12 fils sur six cœurs ; `taskset -c 0-11` laisse donc le noyau
placer deux fils sur un même cœur (voir § 3, protocole).

### 1.13 BenchmarkDotNet 0.15.8 — exact

Les options `statisticalTest`, `launchCount`, `warmupCount`, `iterationCount`, `affinity`, `envVars`, `exporters`,
`runtimes`, `coreRun`, `disasm`, `disasmDiff`, `join`, `invocationCount`, `unrollFactor`, `toolchain`, `noForcedGCs`
existent dans `BenchmarkDotNet.dll` (`strings -n 4 … | grep -E '^(…)$'`). `MsBuildArgument` :
`BenchmarkDotNet.xml:1352`.
Les constructions passent `--nodeReuse:false /p:UseSharedCompilation=false /p:Deterministic=true` (chaîne trouvée) :
pas de serveur de compilation qui ronge les cœurs entre les cas, contrairement à ce qu'on pourrait craindre. Le JSON
« full » porte les mesures une à une avec leur `LaunchIndex` (chaîne `Measurement_LaunchIndex`) : c'est la matière du
critère proposé au § 3. Imprécis : R0 écrit `--exporters json` ; les exporteurs existants sont `json`, `briefjson`,
`fulljson` (chaînes trouvées) ; seul `fulljson` est sûr de porter les mesures brutes (déduit ; à essayer).

### 1.14 La durée — faux (trop bas)

R0 : « 15 à 35 s chacun au job par défaut … ~45 cas à trois lancements … 25 à 30 min par passage ». Relu :
`Run time` des journaux : 35,19 / 15,86 / 23,01 s (08/10), 15,76 / 24,61 / 18,58 / 15,24 s (10/10), 17,68 / 17,1 /
17,01 / 18,41 s. Ces durées sont celles d'un lancement. Trois lancements : 45 × (15 à 35) × 3 = 34 à 79 min par
passage (calculé), soit de 2 h 15 à 5 h 15 pour R C R C, hors construction (une construction par passage). Seule
une itération fixée (`--warmupCount`, `--iterationCount`, `--invocationCount`) ramène la durée à l'ordre annoncé ;
R0 ne la fixe pas. Le chiffre de R0 n'est pas déductible de ses prémisses.

### 1.15 Les listings : 1 507, 42, 39 — exact

Recomptés avec un script jetable qui reprend l'expression régulière de `listings-jit.sh:181-199` sur
`/var/tmp/ixtal-g13/M2-13b/*.txt` (30 scénarios, 30 fichiers `.txt` + `runtime.txt`) :

| Compte | Résultat |
|---|---|
| méthodes distinctes | **1 507** |
| noms commençant par `iXtal26.Models.(pic\|pit\|dma):` | **42** (pit 17, pic 13, dma 12) |
| autres, dont le corps nomme `iXtal26.Models.(pic\|pit\|dma)` | **39** (autre expression : 39 aussi) |

Donc « 42 disparues, 42 apparues, 39 changées » est exact **pour ces trois seuls modules** (vérifié). Deux réserves :
- la capture de M2-13b a été faite sous .NET **10.0.12** (`runtime.txt`) ; le runtime actuel est 10.0.13 : cette capture
  n'est plus comparable à un candidat construit aujourd'hui. R0.6 recapture, mais § 1.6 de R0 le présente comme un
  acquis (« la dernière capture de G13 »). La table des noms s'applique à la capture de R refaite sous 10.0.13 ;
- 42/39 est un plancher : G14 renomme « et ainsi de suite » (le CRTC, le 8250, le 8255, le RTC…) ; seuls trois
  modules sont comptés. R0 écrit « renommer `pic`, `pit` et `dma` seuls », ce qui est correct.
Imprécis : « les adresses sont masquées » : `VERIFICATION.md:7347-7349` dit que la forme d'adressage des champs
statiques (relative ou absolue) variait entre deux exécutions et que `setarch -R` l'a fixée ; elle reste donc
visible, elle n'est pas masquée.

### 1.16 Les références de lignes de PLAN.md — exact

`PLAN.md:557` (le banc de G15 contre sa référence), `:586-592` (la référence figée, « jamais contre le commit
précédent », « statistiquement significative »), `:635` (`DOTNET_JitDisasm`) : exact.

### 1.17 Divers de l'annexe A — exact

`iXtal26.csproj:27` renvoie à « VERIFICATION.md § M5.2 » : les titres sont M5.0, M5.1, M5.3 (`grep -n '^## M5'`).
`.gitignore:12` `roms/*` (vérifié). « sept variantes de l'ami486 » (§ 1.3 de R0) : imprécis, il y a **sept profils**
`ixtal26-486*.cfg` (`ls | wc -l` : 7), donc six variantes de plus que le profil `ixtal26-486.cfg`.

### 1.18 « Le même code, deux états » (§ 1.4) — faux

R0 : « Exec386Memoire en mode PCem, 156,1 puis 156,2 µs, 0,1 % ». `VERIFICATION.md:8412-8415` : les 156,1 µs sont ceux
d'une première version « un appel de méthode par contrôle », jugée trop chère, et les 156,2 ceux de la version gardée
qui inline le chemin court. Ce sont deux versions du code, pas un A/A. Leur égalité montre que le mode PCem ne
coûte rien dans les deux, pas le bruit d'un processus à l'autre (c'est une mesure de l'effet de la garde). Le seul A/A
du dépôt, c'est la dispersion interne d'une exécution.

## 2. Ce qui manque

1. **La part de temps des modules renommés, cas par cas.** R0 conclut que `MachineBanc` « couvre » le PIC et le PIT
   (§ 1.3, R0.5) sans l'avoir établi. Un amorçage sans disque s'arrête sur la boucle d'attente du BIOS : le PIT n'y
   arme que le canal 0 (IRQ 0, ~18 Hz) et, sur l'XT, le rafraîchissement ; le PIC n'est interrogé qu'à ces IRQ
   (déduit). Une injection de 3 % ou 10 % « dans le rappel du PIT » est invisible si le rappel pèse 0,01 % du temps.
   Il manque une mesure : pour chaque cas, le temps par fonction (échantillonnage hors BenchmarkDotNet, ou compteur
   d'appels par module), de façon à dire quels cas voient quels modules. Sans elle, R0.5 teste la sensibilité de la
   mesure au coût injecté, pas la sensibilité du banc aux modules renommés.
2. **Ce qu'est le code invité de `MachineBanc`.** « Une seconde émulée par `pc.runpc` sur le chemin de `--timer-check` »
   :
   charge `repos` (BIOS en attente) ? `ram` ? ni l'une ni l'autre n'est un programme représentatif (un jeu, Windows
   3.1, 8088 MPH). Les IPS d'une boucle d'attente disent la vitesse du moteur, pas celle des chemins que G15 risque
   de ralentir. R0 ne tranche pas et ne le range pas en décision.
3. **L'état entre deux invocations.** Un `MachineBanc` qui rejoue « une seconde » à chaque invocation continue la
   même machine (le BIOS sort de son POST, le temps invité avance) : la charge n'est pas stationnaire, et l'empreinte
   (instructions, cycles) dépend du nombre d'invocations que BenchmarkDotNet a choisi (pilote). Il faut un nombre
   d'invocations et d'itérations fixé (`--invocationCount`, `--iterationCount`, `--warmupCount`), ou une machine
   remontée à
   chaque itération (`[IterationSetup]`, qui coûte l'amorçage et dont BenchmarkDotNet déconseille l'usage). La
   répétabilité de `pc.initpc` dans un même processus est inconnue : les classes sont statiques (le mode « figé
   au chargement », G13.2). Aucun banc existant n'appelle `initpc` deux fois.
4. **Quelle DLL tourne.** R0 consigne le sha256 de `iXtal26.dll` du worktree, mais BenchmarkDotNet génère un projet par
   cas et lance les processus depuis ce répertoire généré (`Depot()` le suppose, `VideoBanc.cs:90-91`). Le fichier
   hashé n'est pas forcément celui qui s'exécute. À vérifier (inconnu) : `--keepFiles` ou le hash de la DLL copiée dans
   le répertoire généré, par le script.
5. **Les chemins incorporés.** Une construction déterministe incorpore le chemin du PDB dans la DLL : deux worktrees
   de mêmes sources à des chemins différents ne donnent pas le même sha256 (déduit ; aucun `PathMap` dans les
   `.csproj` : `grep -i pathmap` vide). R0 écrit (§ 2.3) « `iXtal26.dll` identique au sha256 avant et après chaque
   étape » : vrai dans un même arbre ; faux d'un arbre à l'autre, et R0.4 compare deux arbres. L'égalité qui compte
   entre deux arbres est celle du code IL ou machine (les listings), pas celle du fichier.
6. **L'épinglage du runtime.** Le comparateur « refuse » si le runtime change, mais `unattended-upgrade` peut le
   changer au milieu d'une passe de deux heures. R0 le range en « faux vert » sans parade. Le moyen qui ne touche pas
   au système : une copie du runtime et du SDK sous `/var/tmp` (`DOTNET_ROOT`) pour toutes les mesures de G14 et de
   G15 ; `apt-mark hold` est la parade système, qui est à Julien (modifier la maintenance de sa machine).
7. **L'épinglage fin.** `taskset -c 0-11` laisse deux fils d'un même cœur au planificateur. Un fil par cœur
   (`0,2,4,6,8,10`) retire le bruit du jumeau SMT (déduit). La fréquence (gouverneur `powersave`, pas de
   `no_turbo`), la température, un navigateur ouvert : seuls des A/A les voient ; ils sont dans R0.4.
8. **La part du banc qui ne passe par aucun module renommé.** GardeBanc et Is486Banc (8 cas) ne mesurent que leurs
   copies ; le reste ne nomme ni `pic`, ni `pit`, ni `dma` (§ 1.10). La référence « figée » de R0 couvre donc
   déjà ce que PLAN.md nomme (décodage, mémoire, vidéo, son, IPS) mais pas ce que G14 renomme.
9. **La machine mesurée n'est pas celle du profil.** Exec386Banc tourne en i386SX/16 (§ 1.6) ; les profils utilisent le
   386DX/33 et le DX2/66. Une perte propre au DX ne se verrait pas.
10. **Les 8086** (`olivetti_m24`, `pc1512` avec `cpus_8086`, `cpus_pc1512`) : R0 les ajoute aux machines de D1 mais
    aucun
    banc de décodage ne les couvre, `execx86` est partagé avec le 8088 (déduit : `FetchBanc` ne pose que
    `_808x.Reset`). L'IPS des deux profils passe par `MachineBanc` seul.
11. **La détection de l'écart de placement.** R0 dit que « le placement pèse plus que le travail » (GardeBanc, 2 à 3 %).
    Cela a deux sens : du bruit entre processus (aléatoire, balayé par `setarch -R`) et un effet déterministe d'un
    changement de disposition de classes. Le renommage de G14 ne change pas le code machine ; G15 (PascalCase,
    `private`) non plus ; mais le déplacement de champs entre classes, que PLAN.md n'exclut pas pour les puces
    en `Ics/`, peut le faire. R0 ne dit pas quel est le seuil où un écart de placement déterministe est « une
    régression » au sens du plan.
12. **Le coût de l'attente du lancement.** Le banc de G15 doit tourner « à chaque commit de G14 et de G15 » : le nombre
    de commits est inconnu (R0 le dit) ; il faut le dire à Julien avant le plan, parce qu'à 2 à 5 heures par commit,
    la durée fait la décision de découpage de G14.

### Ce qui élargit G14 au-delà de PLAN.md (§ G14 : 542-560, § G15 : 562-593)

- **R0.4 (A/A) et R0.5 (régressions injectées, répétition du renommage)** ne sont pas dans PLAN.md : le plan demande
  la référence figée, la mesure contre elle à chaque commit, et la correction de toute régression significative.
  Valider le protocole est raisonnable, mais c'est un ajout : il demande le feu vert de Julien ou doit tenir en peu
  d'effort (un A/A suffit à fixer le seuil ; les injections sont facultatives).
- **Les bancs neufs de R0.2** (Exec286/486, 8088 à ModRM mémoire, accès lent, CGA, GD5429, Trident, OPL joué,
  MachineBanc) : tous dans la liste de PLAN.md:586-589 (« décodage 8088, 286, 386, 486 ; accès mémoire ; rendu vidéo ;
  son … IPS de chaque machine »), donc dans la portée ; mais « Trident » et « GD5429 en graphique 8 bpp » sont un
  choix de R0, pas du plan.
- **Un script de comparaison dans `tools/perfbanc/`** : outillage nouveau, comme `compare` de `listings-jit.sh`.
  Python est déjà utilisé en jetable ; l'annexe de PLAN.md ne le nomme pas. À dire à Julien.
- **Le runtime privé sous `/var/tmp`** (§ 2.6) : nouveau, hors plan.
- **Le « jamais sous 1 % »** : une tolérance, que PLAN.md (« 0 % de perte ») n'a pas décidée. Voir D3.
- **Le worktree de référence gardé jusqu'à la fin de G15** : prévu par PLAN.md (« référence d'origine »), mais il engage
  le disque de `/var/tmp` et l'immobilité de la machine de mesure sur des semaines.

## 3. Le protocole et les décisions ouvertes, revus

### 3.1 Le protocole côte à côte est-il faisable avec BenchmarkDotNet 0.15.8 ? — oui, avec quatre corrections

Faisable : deux arbres, chacun `dotnet build -c Release`, chacun lancé par `dotnet
tools/perfbanc/bin/…/iXtal26.PerfBanc.dll
--filter … --launchCount 3 …` sous `setarch $(uname -m) -R taskset -c …`. L'épinglage et la personnalité (ASLR coupé)
sont hérités par les processus enfants de BenchmarkDotNet, qui ne pose une affinité que si le job la demande (le
job d'iXtal26 ne la demande pas : `Program.cs`). Les options existent (§ 1.13). Pas besoin d'un job à deux
projets (`MsBuildArgument`), le repli de R0 : écarté à raison.

Corrections :
1. **Entrelacer par classe, pas par passe.** R C R C par passes entières (2 à 5 h) laisse R et C à une heure d'écart :
   dérive thermique et de fréquence mal répartie, et R est mesuré en premier sur une machine froide. Mieux : pour
   chaque classe de banc, R puis C puis C puis R (ABBA), chacune avec `--filter '*Classe*'`. Le coût est la
   construction du projet généré à chaque appel ; elle dépend du banc (inconnu, à mesurer en R0.3).
2. **Lire le journal.** Le comparateur lit le JSON (full) pour les mesures, allocations et environnement, et le
   journal pour l'état imprimé (§ 1.8). L'affinité et la personnalité sont lues par le script lui-même (§ 1.8).
3. **Fixer l'itération.** `--warmupCount`, `--iterationCount` (et `--invocationCount` pour MachineBanc, § 2.3) donnent
   une durée prévisible et des empreintes comparables ; sinon le pilote change le nombre d'invocations d'un lancement à
   l'autre et la durée suit (§ 1.14).
4. **Un fil par cœur P** (§ 2.7) et le runtime privé (§ 2.6).

Le plus simple qui tienne la règle « contre la référence d'origine, jamais contre le commit précédent » est celui de R0
(un arbre gardé, remesuré), moins l'étape « chiffres de R0.4 comme juge » : les chiffres consignés ne servent à rien
d'autre
que d'alerter sur la dérive de l'hôte.

### 3.2 Le critère chiffré tient-il « toute régression statistiquement significative » ? — non, tel qu'écrit

Le critère de R0 : rouge si, aux deux tours, [l'intervalle à 99,9 % de C est entièrement au-dessus de celui de R]
ET [moyenne(C) − moyenne(R) > s], s ≥ 1 %.

- **L'intervalle de BenchmarkDotNet est trop étroit.** Il est calculé sur toutes les itérations des lancements, comme
  si elles étaient indépendantes. Mais la variance dominante est entre processus (10 à 20 µs sur 400 pour le son, 2,5 à
  5 % ; R0 § 1.4), et l'intervalle ne la compte pas : les demi-intervalles vus sont de 0,1 à 0,5 % (Exec386) et de
  0,0 % à 2 % (Fetch, Son). « Disjoints » est donc presque toujours vrai pour un écart de quelques dixièmes de
  pour cent entre deux processus, et faux seulement pour les cas déjà bruyants. Dans les cas calmes, la condition
  est sans effet ; dans les cas bruyants, elle est plus sévère que s. La règle réelle est donc max(séparation des
  intervalles, s), non pas ce que R0 annonce.
- **Le plancher de 1 % relâche la règle là où le banc est le plus fin.** Exec386 mesure à ±0,1-0,5 % ; une perte
  de 0,7 % y serait « significative » pour PLAN.md et « verte » pour R0. Or ce sont les boucles du décodage, le premier
  chemin chaud de G15. Un plancher a une valeur fixe arbitraire ; la règle de PLAN.md n'en admet pas.
- **Trois A/A donnent un mauvais s.** Le maximum de trois écarts est un estimateur faible de la queue du bruit ;
  avec ~90 cas par commit (D2 : deux modes) sur des dizaines de commits, quelques milliers de verdicts : il faut une
  probabilité de faux rouge de l'ordre de 1/10 000 par verdict pour rester sous quelques pour cent d'alertes
  par campagne. Un maximum de trois tirages ne l'établit pas (déduit).
- **Troisième tour non défini.** « Un seul tour rouge : un troisième tour tranche » : la règle de ce tour (deux sur
  trois ?
  les trois tours poolés ?) n'est pas écrite ; elle fait le taux de faux rouges.

**Proposition.** Prendre pour unité d'analyse la **moyenne de chaque lancement** (le JSON full porte `LaunchIndex`) :
avec trois lancements sur deux tours, six valeurs par côté et par cas. L'écart est la différence des médianes ; son
intervalle à 99 % est celui d'un test non paramétrique ou d'un bootstrap sur ces six valeurs, la variance entre
processus
y étant comptée. Rouge si la borne basse de l'intervalle de C/R − 1 dépasse zéro. Aucun plancher fixe n'est
nécessaire : un cas calme se verra à 0,5 %, un cas bruyant à 3 à 5 %. Le A/A de R0.4 sert alors à **vérifier** le taux
de
faux rouges (dix A/A suffisent à le voir), non à fixer s. Les cas où l'intervalle est plus large que l'écart
exigé par le plan (le son à ±3 %) sont rapportés tels quels : le banc ne prouve pas le 0 % là, les listings le font,
ce que R0 dit déjà. Si Julien veut tout de même une tolérance, c'est D3, et la décision est la sienne.

### 3.3 Les cinq décisions ouvertes

| | Ouverte par PLAN.md ? | Ma position |
|---|---|---|
| D1 | oui, mais mal posée | Les modèles de `models[]` sont neuf, non sept (voir ci-dessous) |
| D2 | oui | d'accord avec R0 : « aucun » et « tout » |
| D3 | oui, autrement | voir § 3.2 : le comptage du bruit, pas une tolérance |
| D4 | à moitié | la liste des chemins chauds est tranchée ; « aucun sous-ensemble » est un ajout |
| D5 | oui | d'accord avec R0 : le binaire juge, les chiffres alertent |

**D1 — « chaque machine ».** R0 : « les sept modèles des profils de la racine … la lecture littérale ». Faux :
`Models/model.cs` enregistre **neuf** modèles (`grep -n 'internal_name = ' model.cs` : `ibmpc`, `ibmxt`, `ami286`,
`ibmat`, `ami386`, `ami386dx`, `ami486`, `pc1512`, `olivetti_m24`). `ibmat` (portes `ibmat-287`, `ibmat-ide`) et
`ami386` (SX,
`Reset386`) sont des machines au sens de PLAN.md et n'ont pas de profil à la racine. La lecture littérale de « chaque
machine » est les neuf ; la lecture « profils » en est sept. Le plan ne dit pas laquelle. Proposition : les neuf,
puisque PLAN.md dit « chaque » ; l'AT d'IBM (287) est le seul à passer par `ibmat` ; le surcoût est de
deux cas par mode. Si Julien veut moins, c'est à lui de dire qu'une machine sans profil n'en est pas une.

**D2 — Quel mode.** Ouverte : PLAN.md (03/10) précède G13.11 (16/10). Accord avec R0. Réserve : « aucun » et « tout »
doublent le jeu ; comme G14 est un renommage, le mode ne change pas le code qui bouge. Je garderais les deux (c'est la
lecture sûre) et je consignerais le surcoût au plan.

**D3 — Le seuil.** PLAN.md : « 0 % de perte », « statistiquement significative (intervalle de confiance) ». Déjà
tranchée
pour la tolérance : il n'y en a pas. Ouverte pour la manière de compter le bruit (§ 3.2). R0 la pose comme « quel s » :
c'est une tolérance que le plan ne donne pas. Si on la pose, elle est à Julien, avec la table des bruits devant lui ;
sinon, la proposition du § 3.2 tient la règle sans la poser.

**D4 — Le jeu de cas.** PLAN.md nomme les chemins chauds, pas leurs cas ni la fréquence : « à chaque commit ». R0 : « le
même
jeu complet, aucun sous-ensemble ». C'est plus fort que le plan, et ruineux (2 à 5 h par commit, § 1.14). Deux parades
conformes au plan : le jeu complet à chaque commit mais un seul passage R C quand tous les cas sont verts, un second
tour seulement sur les cas douteux (séquentiel, § 3.2) ; ou les cas qui passent par les modules renommés, plus le jeu
complet au dernier commit de chaque étape. La seconde est un relâchement ; c'est à Julien. Dans tous les cas, retirer
GardeBanc et Is486Banc de la référence (§ 1.3) : 8 cas sans objet.

**D5 — La forme du gel.** Ouverte : « figée » ne dit ni chiffre ni binaire. Accord : le binaire remesuré juge.
Ajouter : le runtime est partie de la référence (§ 2.6) ; sans copie privée, un binaire figé sur un runtime qui bouge
n'est pas figé.

### 3.4 Décisions que R0 range en proposition et que PLAN.md tranche déjà

- « Les listings restent la preuve directe du 0 % » : `PLAN.md:635` (« C'est la preuve directe du 0 % »).
- « le banc reste exigé » : `PLAN.md:556-557`.
- « contre la référence d'origine, jamais contre le commit précédent » : `PLAN.md:589-590`.
Ces trois points ne sont pas des décisions : R0 les traite bien comme des acquis.

## 4. Ce qu'il faut corriger dans R0 avant le plan

1. **D1 et § 1.3** : neuf modèles (`ibmat`, `ami386` en plus), non sept ; « lecture littérale » est faux.
2. **§ 1.4, « Le même code, deux états »** : 156,1 et 156,2 µs sont deux versions du code (`VERIFICATION.md:8412-8415`),
   à retirer
   des mesures de bruit.
3. **§ 1.3, « ami486 et ses sept variantes »** : sept profils (six variantes).
4. **§ 2.1, durée** : 34 à 79 min par passage au lieu de 25 à 30 min, sauf itération fixée ; à fixer et à dire.
5. **§ 2.1, comparateur** : lire le journal pour les états imprimés, lire l'affinité et la personnalité hors du JSON ;
   l'affinité du JSON ne dit rien de `taskset`.
6. **§ 2.1, critère** : réécrire sur les moyennes de lancement (§ 3.2), ou dire que la condition « intervalles disjoints
   »
   est décorative ; supprimer le plancher fixe de 1 % ou le renvoyer à Julien comme une tolérance ; décrire le
   troisième tour.
7. **§ 2.1, ordre** : R C R C par classe de banc, non par passe entière.
8. **§ 2.1, épinglage** : un fil par cœur P ; le motif de « deux cœurs physiques » (`VERIFICATION.md:909-911`) était
   le fil de compilation Tier1 ; avec `TieredCompilation=0` (le banc) il ne vaut plus, et un seul cœur est admis
   (« ou un seul cœur seulement une fois ΔJIT = 0 », `:911`).
9. **§ 1.3 et R0.5, MachineBanc et le PIC/PIT** : l'affirmation est à retirer tant que la part de temps des modules
   n'est pas mesurée ; ajouter cette mesure à R0.1 ou R0.2, et dire le code invité de MachineBanc.
10. **§ 1.3, minuteries** : `timer.cs` n'est pas le PIT ; aucun banc existant ne touche le 8253.
11. **§ 1.6, listings** : la capture M2-13b est sous .NET 10.0.12 ; elle est périmée ; le plancher de 42/39 ne vaut que
    pour
    trois modules.
12. **§ 2.3 et R0.4, sha256** : l'égalité de sha256 vaut dans un arbre, pas entre arbres (chemin du PDB) ; entre arbres
    c'est l'égalité des listings.
13. **§ 3, risques** : ajouter la mise à jour de .NET en cours de passe et sa parade (runtime privé ou `apt-mark hold`,
    à Julien) ;
    ajouter l'état de la machine entre deux invocations de MachineBanc et la répétabilité de `pc.initpc`.
14. **D3, D4** : D3 devient « comment compter le bruit » (§ 3.2) ; D4 n'est pas « tout, ou rien » : dire que
    GardeBanc/Is486Banc sortent de la référence et que le sous-ensemble est une décision de Julien.
15. **Annexe B** : l'ordre du n-uplet des compteurs ; `--exporters fulljson` ; la commande du compte des listings dit
    aussi la forme du second compte (`\b(pic|pit|dma)[:.]`).

Mineurs : « s » désigne le seuil et « 3 s » (§ 2.1, dérive) se lit comme trois secondes ; dire « trois fois le seuil ».
`Program.cs:13-15` cité pour le mutateur est un commentaire (la ligne de code est `:30`).

## Annexe A — Hors de la portée

Trouvé en route, non proposé :
- `Exec386Banc` ne pose pas `FuzzCpu` : l'i386SX/16 par défaut est un choix implicite (R0 le note en annexe A).
- Le projet `PerfBanc` n'est pas dans `iXtal26.sln` : Rider ne le renomme pas avec la solution (R0 le note).
- `GardeBanc.cs:93` lit `setadc8` par réflexion et par son nom : dépend des noms de G15 (PascalCase) plus que de G14.
- Les allocations de 56 B par `Boucle` (`new ushort[R.COUNT]`) dans FetchBanc et Exec386Banc sont dans la mesure et
  constantes ; elles ne gênent pas la comparaison, mais ce n'est pas du « zéro allocation ».
- `ibmat` et `ami386` n'ont ni profil à la racine ni cas dans PERFBANC.

## Annexe B — Les commandes

- Les cas : `cd tools/perfbanc; grep -c '\[Benchmark' *.cs` (Exec386 6, Fetch 3, Garde 4, Is486 4, Son 6, Video 4 ;
  Global 0) ; `grep -n '\[Params\|\[Benchmark\|Baseline' *.cs` ; le calcul du § 1.1.
- L'IF : `sed -n 86p FetchBanc.cs` ; `sed -n 123p Exec386Banc.cs` (`0xF002`).
- Les rapports : `tail -6 bin/Release/net10.0/BenchmarkDotNet.Artifacts/results/iXtal26.PerfBanc.*-report-github.md` ;
  les journaux : `grep -E 'Run time|Global total' BenchmarkRun-*.log` ; `grep -n '// Exec386Banc'
  BenchmarkRun-20261010-052912.log`.
- Les listings : un script `python3 -I` jetable (hors dépôt), même expression régulière que `listings-jit.sh:181-199`,
  sur `/var/tmp/ixtal-g13/M2-13b` : 1 507 ; 42 (`^iXtal26\.Models\.(pic|pit|dma):`) ; 39 (corps nommant
  `iXtal26.Models.(pic|pit|dma)`) ; `cat runtime.txt` (10.0.12).
- L'hôte : `lscpu -e=CPU,CORE,MAXMHZ`, `dotnet --list-runtimes`, `dotnet --list-sdks`,
  `grep -n -B3 'dotnet-runtime-10.0:amd64 (10.0.12' /var/log/apt/history.log`.
- Les modèles : `grep -n 'internal_name = ' iXtal26/Models/model.cs` ; `ls ixtal26-486*.cfg | wc -l`.
- BenchmarkDotNet : `strings -n 4 …/benchmarkdotnet/0.15.8/lib/net8.0/BenchmarkDotNet.dll | grep -E '^(launchCount|…)$'`
  ;
  `strings -el … | grep -i 'Deterministic\|nodeReuse'` ; `strings … | grep -ix 'fulljson\|json\|briefjson'`.
- L'ASLR : `sed -n 7345,7350p VERIFICATION.md`. Le plan : `grep -n 'banc de performance de G15\|statistiquement'
  PLAN.md`.
