# R0 — La référence de performance figée, avant G14

> Reconnaissance du 23 octobre 2026, au commit `1d49a7f`. LECTURE SEULE : rien n'a été construit, lancé ni mesuré ;
> les chiffres cités sont ceux que le dépôt consigne, ou que les rapports de BenchmarkDotNet laissés sous `bin/`
> portent. Niveaux : **vérifié** (relu dans le fichier ou la sortie d'une commande), **déduit** (raisonné sur ce qui
> est relu), **inconnu**. Les commandes de comptage sont à l'annexe B.

## 0. En bref

1. `tools/perfbanc/` a six bancs, 52 cas ; tous écrits en G13 (07/10-18/10) pour M1 : le coût du mode matériel.
2. Aucun ne mesure une machine entière : boucles de 100 000 cycles, IF = 0, minuteries hors jeu, PIC jamais lu.
3. PLAN.md § G15 : le 8088 et le 386 partiels ; le 286 absent ; le 486, un seul gestionnaire ; le son et la vidéo
   partiels (ni CGA, ni rendu graphique, ni OPL joué) ; les instructions par seconde, absentes du banc.
4. Les chiffres consignés ne servent pas de juge : l'hôte a changé de processeur depuis M5.1 ; .NET est passé de
   10.0.12 à 10.0.13 par `unattended-upgrade` le 21/10 ; le bruit d'un processus à l'autre atteint 2,5 à 5 %.
5. Le protocole proposé : un worktree au commit figé R, gardé construit, remesuré côte à côte avec le candidat,
   R C R C, sur les cœurs P, adresses non randomisées ; le juge est l'écart, pas un chiffre du journal.
6. Le critère : rouge si les deux tours le voient, intervalles à 99,9 % disjoints ET écart > s, s étant le bruit
   mesuré en A/A pour chaque cas (au moins 1 %) ; et à l'octet près : allocations, états imprimés, empreintes.
7. Les listings du JIT restent la preuve directe du 0 % ; mais `listings-jit.sh compare` indexe par nom : sans
   table des noms, renommer `pic`, `pit` et `dma` seuls ferait 42 disparues, 42 apparues, 39 changées.
8. Le banc reste exigé par la porte de G14 ; il voit ce que les listings masquent (données statiques, méthodes
   qu'aucun scénario ne compile, allocations), à sa résolution s.
9. Six étapes R0.1-R0.6 : le choix des cas, les bancs manquants, l'outil de comparaison, le bruit A/A, les
   contrôles négatifs (régressions injectées de ~3 % et ~10 %, une allocation, un no-op), puis le gel.
10. Cinq décisions ouvertes : les machines, les modes, le seuil s, le jeu de cas et sa durée, la forme du gel.

## 1. L'état, point par point

### 1.1 Les six bancs de `tools/perfbanc/`

Communs à tous (vérifié) : BenchmarkDotNet 0.15.8 (`iXtal26.PerfBanc.csproj:30`), une référence au projet
`iXtal26` (`:25`), ses internes ouverts par `InternalsVisibleTo` (`iXtal26/iXtal26.csproj:73`). Un job mutateur pose
`DOTNET_TieredCompilation=0` (`Program.cs:30`), en plus de `<TieredCompilation>false` (`csproj:20`) ;
`MemoryDiagnoser` ; les rapports sous `bin/Release/net10.0/BenchmarkDotNet.Artifacts` (`Program.cs:32`). Le job est
celui par défaut : nombre d'itérations choisi par BenchmarkDotNet, un lancement, aucune affinité (le CSV des rapports
porte `Affinity=11111111111111111111`, les vingt processeurs logiques ; vérifié). Chaque cas (méthode, job, valeur de
`[Params]`) tourne dans son processus, et le mode matériel se fige une fois par processus (commentaires de chaque
banc ; documenté dans `G13-reconnaissance/D7-contre.md:150-152`).

Aucune porte ne construit le banc (`VERIFICATION.md:12988`, vérifié). Le projet n'est pas dans `iXtal26.sln`, qui ne
contient que `iXtal26` et `iXtal26.SoftFloat` (`iXtal26.sln:3,5`, vérifié).

**GardeBanc** (`GardeBanc.cs`, G13.2, `dc03cf6`, 07/10 ; décision n° 6 de PLAN-G13, `PLAN-G13.md:920-921`).
- Ce qu'il appelle : quatre copies de `setadc8` (`_808x`) par un délégué, l'équivalence vérifiée par réflexion
  contre le vrai `setadc8` sur 65 536 couples et deux retenues (`GardeBanc.cs:91-112`).
- Charge : 1 000 appels par invocation (`:79`), `OperationsPerInvoke` ; le mode PCem, figé par `_808x.Reset()`.
- Pourquoi : M1, le coût d'une garde figée, modifiable, ou figée trop tard.
- Chiffres (`VERIFICATION.md:7359-7365` ; rapport `GardePb01-report-github.md`, 07/10 22:07) : 4,918 ns
  (± 0,0088, demi-intervalle à 99,9 %), 4,920 (± 0,0041), 4,830 (± 0,0053, rapport 0,98), 4,779 (± 0,0024, 0,97).

**Is486Banc** (`Is486Banc.cs`, `Cmpsb386` et `Cmpsb486`, commité en G13.2, `dc03cf6` ; écrit avant, avec
BenchmarkDotNet installé le 04/10, `csproj:29`).
- Ce qu'il appelle : `ops_386[0xA6]`, le vrai `opCMPSB_a16`, contre une copie où `is486 != 0 ? 8 : 10` devient la
  constante (`:106`, `:168-218`) ; la machine posée par `Reset386` (i386SX/33) ou `Reset486` (i486DX2/66).
- Charge : 1 000 appels du gestionnaire par invocation (`:75`), mode réel, DS = ES = 2000h.
- Chiffres : **aucun consigné** (vérifié : aucune mention dans VERIFICATION.md ; `D7-mecanisme.md:169` le disait).

**FetchBanc** (`FetchBanc.cs`, G13.3, `8556e3a`, 08/10).
- Ce qu'il appelle : `_808x.Reset`, `_808x.SetRegs`, `_808x.Run(100 000)` (`:56`, `:80-89`), donc `execx86`.
- Charge : MOV AX,1234h ; ADD BX,AX ; LOOP, en 0000:0100 sur la RAM plate du harnais (`:58`) ; FLAGS = F002h
  (`:86`), donc IF = 0. Trois cas : « aucun », « PB-87 », « tout ». Le nombre d'instructions n'est pas imprimé
  (inconnu ; le banc imprime BX, CX, IP).
- Chiffres (`VERIFICATION.md:7499-7503` ; rapports du 08/10 20:04) : 114,8 µs (± 2,29, écart type 2,14) ;
  125,0 (± 2,47, σ 2,64) ; 127,6 (± 2,53, σ 5,33 ; ses itérations vont de 120,9 à 133,7 µs, journal
  `BenchmarkRun-20261008-200356.log:139-143`). La marge à 99,9 % y est de 2 % de la moyenne.

**Exec386Banc** (`Exec386Banc.cs`, G13.5b, `f3d5288`, et G13.5d, `7886f80`, 10/10).
- Ce qu'il appelle : `_386.Reset386`, `_386.exec386(100 000)` (`:86`, `:117-128`) ; l'échéance posée à la main,
  `timer.timer_target` (`:125`), sans quoi exec386 ne sort plus.
- Machine : l'ami386 de `Reset386` ; `FuzzCpu` n'est pas posé, donc l'indice 0 de `cpus_i386SX`, l'i386SX/16
  (`cpu_tables.cs:155` ; déduit, un processus par cas).
- Charge : trois boucles (`:88-90`) : sans préfixe, avec ES:, à deux accès mémoire ; FLAGS = F002h (`:123`), IF = 0.
  Six cas : « aucun » contre PB-51 (deux boucles), « aucun » contre PB-189.
- Chiffres (`VERIFICATION.md:8135-8138`, `:8411-8415` ; rapports du 10/10) : 113,4 (± 0,43) contre 132,2 (± 0,13) ;
  138,5 (± 0,22) contre 169,5 (± 0,32) ; 156,2 (± 0,83) contre 154,3 (± 0,33). Marges de 0,1 à 0,5 %.

**VideoBanc** (`VideoBanc.cs`, G13.9g, `bf7db3f`, 15/10).
- Ce qu'il appelle : `pc.loadconfig`, `pc.initpc` sans BIOS, les ports de la carte, puis le rappel de la minuterie
  de la carte joué à la main, deux par ligne, et `io.inb(0x3DA)` (`:154-170`).
- Machines : l'XT et l'EGA, l'ami486 et l'ET4000AX (`:109-110`), texte de 80 colonnes ; 0 ou 16 lectures de 3DAh.
  Huit cas : « aucun » contre PB-99 (EGA), « aucun » contre PB-226 (ET4000).
- Chiffres (`VERIFICATION.md:11823-11832`, µs par trame) : 24,8 / 190,7 ; 52,9 / 309,8 ; 29,8 / 32,1 ;
  84,8 / 151,9. Dispersion **non consignée** ; les journaux étaient sous `/tmp/a9`, vidé au redémarrage du 20/10
  (`uptime -s` : 2026-10-20 06:31:51 ; inconnu).

**SonBanc** (`SonBanc.cs`, G13.8g3a, `1eda949`, 18/10 ; « PERFBANC SonBanc »).
- Ce qu'il appelle : l'ami486 monté sans BIOS (`:226-259`), la carte et le 8237 par leurs ports, puis le temps
  avancé d'échéance en échéance, `timer.timer_process`, jusqu'à ce que `sound_poll` ait rendu un tampon (`:263-287`).
- Charge : un tampon de 2 400 tics à 48 kHz (50 ms). Six classes, 27 cas : SB 16 en sortie et en entrée, Pro v2,
  ADPCM de la SB 2.0, AWE32 à 30 voix, AWE32 et WC lu 0, 16 ou 64 fois ; chacun en « aucun », sous ses PB, et sous
  le domaine « son » ; aucune ligne de base (`Baseline`), donc aucun rapport calculé par le banc.
- Chiffres (`VERIFICATION.md:13005-13019`, µs par tampon, deux lancements) : de 207,3 (ADPCM) à 949,4 (AWE32,
  « son ») ; dans un processus l'écart type reste sous 2 µs ; d'un processus à l'autre le même cas bouge de 10 à
  20 µs, une fois 63 µs (`:13007-13008`).

### 1.2 Les autres mesures de vitesse du dépôt

- **`iXtal26.Diff bench`** (`tools/iXtal26.Diff/Bench.cs`, M5.1) : le cœur C# contre l'oracle C sur la même
  trajectoire, **8088 seul par construction** (`Bench.cs:76-78`) ; 6 000 tranches, médiane et minimum, ordre
  alterné, trajectoire vérifiée. M5.1 : 3 514 ms pour 60 s émulées, 24 944 866 instructions
  (`VERIFICATION.md:780-788`), sur un Core Ultra 7 258V, .NET 10.0.12 (`:770-772`).
- **`make -C tools/oracle bench`** (PCem compilé comme PCem, 8088) : GR.4d, 901,5 ms, × 66,56 le temps réel,
  dispersion 0,4 % (`VERIFICATION.md:15887-15888`) ; et : « Le chiffre de `bench` n'est pas une référence de
  performance (G14 la posera) » (`:15894`).
- **`--timer-check`** (`iXtal26/TimerCheck.cs`) : son bloc 7, seul non déterministe, donne la marge, la capacité
  (MHz de cycles émulés par seconde hôte) et les MIPS invités (`TimerCheck.cs:408-417`), et une ligne `EMPREINTE`
  déterministe (`:419`). Charges `ram`, `x87`, `cd`, réservées au 286 et plus en mode réel (`:504-508`). Chiffres :
  ami286 33,3 / 32,1, ami386 25,2 / 22,0, ami486 3,88 / 3,33 (G13.5b, `VERIFICATION.md:8148-8150`) ; x87 : 61,6 /
  47,6, 52,4 / 27,4, 42,3 / 7,4 (`:9803-9809`) ; GR.3h sur un ami486 avec IDE, CD et SB Pro : repos 5,94 / 4,87,
  ram 8,99 / 7,22, cd 7,77 / 6,40 (`:15456-15460`). « Ce sont des mesures de l'hôte, non déterministes ».
- **Le titre de la fenêtre** : `invite NN % - MHz - marge xN` (`Host/SdlHost.cs:1279`, `:1324`) ; plafonné à 100 %
  par le frein (`VERIFICATION.md:775-777`).
- **L'audit du 26/09** (`iXtal26/Docs/audit-performance-2026-09-26.md`) : le profil du 286 sous Windows 3.1,
  `exec386` 40 %, `prefetch_run` 10 %, minuteries 7,4 %, `readmemw`/`writememw` 4,6 %, égalité de délégués de
  `svga_poll` 2,5 % (§ 2) ; « Aucun gain n'a été mesuré ». M5.1 : sur le 5150, `cga_poll` fait 36,7 % du temps C#
  (`VERIFICATION.md:819`).

Aucune de ces mesures n'est une référence figée : aucune n'est répétée contre un état gardé, et aucune ne couvre
toutes les machines (vérifié pour chacune).

### 1.3 L'écart avec PLAN.md § G15 (`PLAN.md:586-592`)

| Exigé | État | Ce qui manque |
|---|---|---|
| décodage 8088 | partiel | FetchBanc : trois opcodes, ni ModRM mémoire, ni préfixe, ni chaîne |
| décodage 286 | absent | aucun banc ne pose `Reset286` (`386.State.cs:33`), qui existe |
| décodage 386 | partiel | trois boucles de trois instructions, i386SX/16, mode réel, IF = 0 |
| décodage 486 | partiel | un gestionnaire (CMPSB) par délégué ; pas la boucle d'exec386 sur `Reset486` |
| accès mémoire | partiel | la boucle à deux accès du 386, RAM rapide ; ni le 8088, ni le chemin lent |
| rendu vidéo | partiel | EGA et ET4000 en texte ; ni CGA, ni mode graphique SVGA, ni GD5429, Trident, S3 |
| son | partiel | SB, Pro, AWE32 ; l'OPL ne joue aucune voix ; jamais le mode « tout » |
| IPS de chaque machine | absent du banc | voir ci-dessous |

Le mode « tout », celui de l'utilisateur depuis G13.11 (`VERIFICATION.md:12196-12197`), n'est mesuré que par
`FetchMateriel` (vérifié : les autres cas prennent « aucun », un PB ou un domaine).

Ce qui fausserait le jugement de G14 : les boucles de FetchBanc et d'Exec386Banc tournent IF = 0 (`FetchBanc.cs:86`,
`Exec386Banc.cs:123`) ; le PIC n'y est donc jamais lu (`Cpu/386.cs:480`, `Cpu/808x.cs:768`, déduit) et aucune
minuterie n'échoit dans la fenêtre d'Exec386Banc (`:125`). Or `pic`, `pit` et `dma` (`Models/pic.cs`, `pit.cs`,
`dma.cs`) sont les premiers modules que PLAN.md renomme. Seul SonBanc traverse le 8237 et les minuteries.

**Les instructions par seconde.** Ce qui existe : la capacité et la marge de `--timer-check` (toutes machines AT, et
le 8088 au repos), `iXtal26.Diff bench` (le 5150 seul), le titre. Ce qui conviendrait : un banc `MachineBanc`, la
machine entière par `pc.runpc` (le PIC, le PIT, la vidéo et le son à leurs rythmes), une seconde émulée par
invocation, sur le chemin de `--timer-check` (la configuration de porte, le CMOS fabriqué, sans disque), avec son
empreinte (instructions, cycles) imprimée, égale entre la référence et le candidat. Les machines des profils de la
racine (vérifié, `grep '^model'`) : `ibmpc` (5150-mda), `ibmxt` (xt, xt-hercules), `olivetti_m24`, `pc1512`,
`ami286` (286, 286-ega, 286-install), `ami386dx` (386, 386-et4000), `ami486` (486 et ses sept variantes). Sept
modèles : voir la décision D1.

### 1.4 Le bruit déjà mesuré, et l'hôte qui dérive

- **D'un processus à l'autre** : 2,5 à 5 % sur le son, une fois 13 % (`VERIFICATION.md:13007-13008`). Dans un
  processus : de 0,1 % (Exec386) à 2 % (Fetch) de demi-intervalle.
- **Le placement du code** : une copie qui lit vraiment un champ sort 2 à 3 % plus vite que la copie sans garde
  (`VERIFICATION.md:7362-7365`) ; à cette échelle, le placement pèse plus que le travail.
- **Le même code, deux états** : Exec386Memoire en mode PCem, 156,1 puis 156,2 µs (`VERIFICATION.md:8412-8415`),
  0,1 %.
- **TieredCompilation** : la dispersion du banc de M5 tombait de 6-9 % à 2-3 % sans paliers
  (`iXtal26/iXtal26.csproj:27-32`).
- **Les adresses** : deux exécutions du même binaire donnaient deux listings, selon l'adresse des champs statiques ;
  d'où `setarch -R` dans `listings-jit.sh` (`VERIFICATION.md:7347-7349`). Les processus de BenchmarkDotNet tournent
  sans lui (vérifié : `Program.cs` ne le pose pas) : une cause probable du bruit entre processus (déduit).
- **L'hôte** : M5.1 sur un Core Ultra 7 258V (`VERIFICATION.md:770`) ; les rapports du banc, du 07 au 10/10, sur un
  Intel Core i5-14600KF (en-têtes des `*-report-github.md`). Aujourd'hui (`lscpu -e`) : six cœurs P, processeurs
  0-11 à 5,3 GHz, deux fils par cœur ; huit cœurs E, 12-19 à 4,0 GHz ; gouverneur `powersave`. Un processus non
  épinglé peut tomber sur un cœur E (déduit).
- **.NET** : les rapports portent .NET 10.0.12, SDK 10.0.112 ; aujourd'hui 10.0.13 et 10.0.113 seuls
  (`dotnet --list-runtimes`), posés par `unattended-upgrade` le 2026-10-21 à 06:18:11 (`/var/log/apt/history.log`) ;
  `APT::Periodic::Unattended-Upgrade "1"` (`/etc/apt/apt.conf.d/20auto-upgrades`). VERIFICATION.md ne le consigne
  pas (vérifié : aucune mention de 10.0.13).
- **Un chiffre consigné ne se compare pas seul** : l'ami486 sous `--charge ram` valait 3,88 le 10/10 sur la
  configuration de porte, sans son (`VERIFICATION.md:8150`), et 8,99 le 21/10 sur une configuration plus chargée
  (`:15458`). La cause est inconnue.

### 1.5 Ce que BenchmarkDotNet 0.15.8 offre (vérifié dans le paquet, `~/.nuget/packages/benchmarkdotnet/0.15.8`)

- Options de la ligne de commande présentes (chaînes de `BenchmarkDotNet.dll`) : `--statisticalTest` (seuil,
  test de Mann-Whitney, `StatisticalTestColumn`), `--launchCount`, `--warmupCount`, `--iterationCount`,
  `--affinity`, `--envVars`, `--exporters`, `--runtimes`, `--coreRun`, `--disasm`, `--disasmDiff`, `--join`.
- L'intervalle : la colonne `Error` est « Half of 99.9% confidence interval » (légende des journaux) ;
  `ConfidenceInterval = [...] (CI 99.9%)` par cas (`BenchmarkRun-20261008-200356.log:202`).
- `--runtimes` et `--coreRun` changent le **runtime**, pas l'application : ils compareraient 10.0.12 et 10.0.13,
  pas deux états d'iXtal26 ; un seul runtime partagé est installé.
- `MsBuildArgument` (doc XML du paquet, `BenchmarkDotNet.xml:1352-1356`) : un job peut construire le projet avec
  ses propres propriétés MSBuild ; deux jobs dans un même lancement, l'un ligne de base, donneraient `Ratio` et le
  test statistique natifs. Il faudrait que le csproj du banc référence, sous une propriété, la DLL et les sources de
  la référence (déduit ; non essayé).
- `WithNuGet` compare deux versions d'un paquet : il faudrait empaqueter iXtal26 (écarté : lourd).
- `StatisticalTestColumn` et `Ratio` n'existent qu'entre cas d'un même lancement, avec une ligne de base.
- Les constructions des projets générés passent `/p:Deterministic=true` (chaîne de la DLL) : même SDK, même octets
  (déduit).

### 1.6 Les listings du JIT et un renommage

`tools/listings-jit.sh` (30 scénarios, `:83-114`) capture `DOTNET_JitDisasm='*'` avec `JitDisasmDiffable` sous
`setarch -R` ; `compare` indexe les méthodes **par leur nom** (`:178-219`). Les adresses sont masquées
(`0xD1FFAB1E`, `[(reloc)]`), mais les cibles d'appel portent le nom complet du type
(`call [iXtal26.Models.pic:picinterrupt():byte]`, capture `/var/tmp/ixtal-g13/M2-13b`). La dernière capture de G13
compte 1 507 méthodes ; 42 sont de `pic`, `pit` ou `dma`, et 39 autres les nomment dans leur code (annexe B).
Après un renommage, `compare` brut dirait 42 disparues, 42 apparues, 39 changées. Il faut une table des noms
appliquée à la capture de référence avant la comparaison : « identiques à la renommée près » devient alors
« identiques ».

Ce que les listings ne voient pas (déduit) : l'adresse et la disposition des champs statiques (masquées) ; les
méthodes qu'aucun scénario ne compile ; les allocations et le GC. Un déplacement de champs entre classes, ou un
ordre de chargement des types changé, ne se lirait qu'au banc. À l'inverse, à listings identiques, le banc ne peut
voir qu'un écart de données ou de placement, sous la forme d'un écart de quelques pour cent au plus (déduit des
2 à 3 % de GardeBanc).

## 2. Ce qu'il faut faire

### 2.1 Le protocole proposé

**Figer** : R est le commit qui clôt les étapes R0 (le banc complet et l'outil), avant tout renommage. Un worktree
détaché au commit R, `/var/tmp/ixtal-g14/ref-R` (hors de `/tmp`), construit une fois en Release ; les empreintes
sha256 de `iXtal26.dll`, `iXtal26.SoftFloat.dll` et `iXtal26.PerfBanc.dll` consignées. `roms/` y est lié (les ROM sont
ignorées par git, `.gitignore:12`, et VideoBanc et SonBanc cherchent `roms/` au-dessus du banc,
`VideoBanc.cs:92-98`) ; `roms.sha256` vérifié avant chaque mesure. Ce worktree ne bouge plus jusqu'à la fin de G15.

**Mesurer** : à chaque commit candidat C de G14 et de G15, les deux bancs (celui de R dans son worktree, celui de C
dans le sien : chacun compile contre ses propres noms) tournent côte à côte, le même jour, sur le même runtime :
- l'ordre R C R C, deux tours ; chaque passage `--launchCount 3`, un job fixé (échauffement et itérations
  constants), `--exporters json` ;
- sous `setarch $(uname -m) -R taskset -c <cœurs P>` (deux cœurs physiques au moins : un seul cœur fausse la
  mesure, `VERIFICATION.md:909-911`) ; la machine au repos : aucune série, aucune construction, Rider fermé (règle de
  § M5.1, `VERIFICATION.md:947`, et de la mémoire du dépôt : rien de lourd à côté d'une série) ;
- un comparateur (python3 jetable, rien à installer) lit les JSON des deux côtés.

**Le critère chiffré**, par cas :
- **refus** (ni vert ni rouge, à refaire) si le runtime, le SDK, les variables d'environnement du job ou
  l'affinité diffèrent entre les deux côtés, ou si `dotnet --list-runtimes` change entre le début et la fin ;
- **rouge exact** si l'allocation par opération diffère d'un octet, si l'état imprimé par `Preparer` diffère, ou si
  l'empreinte d'IPS (instructions, cycles) diffère : ce sont des faits, sans bruit ;
- **rouge statistique** si, dans les deux tours, l'intervalle à 99,9 % de C est entièrement au-dessus de celui de R
  ET la moyenne de C dépasse celle de R de plus de s ; s est le bruit A/A du cas (étape R0.4), au moins 1 % ;
- un seul tour rouge : un troisième tour, décidé d'avance, tranche ; jamais plus ;
- **vert** sinon. Un rouge se corrige ou s'annule avant le commit (`PLAN.md:591-592`), listings identiques ou non.

**Les chiffres consignés** servent de journal, pas de juge : la table de R0.4 (hôte, processeur, runtime, moyennes,
intervalles) va dans VERIFICATION.md ; si R remesuré s'en écarte de plus de 3 s, l'hôte a dérivé, et c'est dit ; le
verdict reste celui du côte à côte, qui ne cumule rien puisque R ne change jamais.

**Pourquoi pas la variante à un seul lancement** (deux jobs par `MsBuildArgument`) : elle donnerait le test de
BenchmarkDotNet en colonne, mais demande un csproj qui bascule de sources et de DLL, non essayé ; le côte à côte en
deux arbres ne demande rien de plus que ce que le banc fait déjà. Elle reste un repli si le comparateur ne suffit pas.

**Durée** (déduit) : les cas ont pris 15 à 35 s chacun au job par défaut (journaux du 08 et du 10/10) ; un jeu de
~45 cas à trois lancements fait de l'ordre de 25 à 30 min par passage, deux heures pour R C R C.

### 2.2 Le banc est-il nécessaire quand les listings sont identiques ?

Pour les méthodes compilées par les scénarios, des listings identiques (à la table des noms près) sont la preuve
directe du 0 % : même code machine, même taille, donc même placement sous `setarch -R` (déduit). Le banc n'y ajoute
qu'une résolution de s. Mais il reste nécessaire pour trois choses que les listings ne voient pas (§ 1.6) : les
données statiques, les méthodes hors scénario (rendus SVGA, voix de l'AWE32, Trident), les allocations. Et PLAN.md
le demande : la porte de G14 nomme « le banc de performance de G15 contre sa référence d'origine »
(`PLAN.md:556-557`), l'annexe nomme `DOTNET_JitDisasm` pour « code identique après renommage » (`PLAN.md:635`).
Les deux restent ; leur désaccord (listings identiques, banc rouge) se traite comme un rouge.

### 2.3 Les étapes proposées

Aucune ne change l'émulateur : la preuve en est `iXtal26.dll` identique au sha256 avant et après chaque étape ;
aucune série entière, un sous-ensemble ciblé seulement si un outil partagé change (règle du dépôt).

**R0.1 — Le jeu de cas.** Le choix écrit, cas par cas, de ce qui constitue la référence : les cas existants gardés
(en « aucun » et en « tout », pas chaque PB), les cas neufs de R0.2, les machines et les modes (D1, D2), la liste
des filtres `--filter`. Portes et preuves : la liste dans le plan de G14, validée par Julien. Contrôle négatif :
aucun (un document).

**R0.2 — Les bancs manquants.** Sur le patron des bancs existants (un `Preparer` qui pose le mode avant le gel,
vérifie et imprime son état) :
- `Exec286` et `Exec486` : les trois boucles d'Exec386Banc sous `Reset286` et `Reset486` ;
- une boucle 8088 à ModRM mémoire, préfixe et chaîne courte ;
- un accès lent : lecture en ROM, écriture en mémoire vidéo par le chemin des gestionnaires ;
- la vidéo : la CGA en texte (`cga_poll`, premier poste de M5.1) ; un mode graphique 8 bpp sur la GD5429 et la
  Trident (les cartes des profils 486 et 286) ;
- le son : l'OPL de la Pro v2 avec des voix jouées ;
- `MachineBanc` : une seconde émulée par `pc.runpc`, par modèle et par mode, l'empreinte imprimée.
Portes et preuves : la construction sans avertissement ; `--job dry` sur tous les cas (rapide) ; l'état imprimé de
chaque cas égal d'un mode à l'autre là où le mode n'y change rien ; `iXtal26.dll` inchangée. Contrôles négatifs :
une équivalence cassée exprès (une copie qui diverge) doit faire refuser le `Preparer` ; `MachineBanc` sans le mode
posé doit le dire (comme le contrôle de `--menu-check`, `VERIFICATION.md:11819-11821`).

**R0.3 — L'outil de comparaison.** Un script (`tools/perfbanc/`) qui joue R C R C sous `setarch -R` et `taskset`,
exporte en JSON, vérifie runtime, SDK, environnement, affinité, allocations, états et empreintes, puis applique le
critère de § 2.1, et écrit son rapport sous `/var/tmp/ixtal-g14/perf/`. Portes et preuves : il tourne sur deux
arbres au même commit. Contrôles négatifs, sur des JSON modifiés à la main : un runtime différent, la variable
`DOTNET_TieredCompilation` absente d'un côté, une allocation de 8 octets de plus, un état imprimé différent :
chacun refusé ou rouge, avec sa raison.

**R0.4 — Le bruit (A/A).** R contre une copie de lui-même (un second worktree au même commit), trois fois, à trois
moments de la journée ; puis R contre un no-op (un commentaire changé) : listings identiques, banc vert. Le plus
grand écart A/A de chaque cas fixe son s (D3). Portes et preuves : la table des s consignée, avec l'hôte et le
runtime. Contrôle négatif : le même A/A sans `setarch -R` et sans `taskset` ; s'il est plus bruyant, la mesure de
ces deux réglages est faite ; s'il ne l'est pas, ils restent par prudence (déduit, à dire).

**R0.5 — Les régressions injectées.** Dans des copies jetables : un coût non pliable dans la boucle d'exec386 et
d'execx86, dosé à ~3 % et ~10 % ; le même dans le rappel du PIT et dans `picinterrupt` ; une allocation dans
`sound_poll`. Attendus : rouges là où le chemin passe, et sur `MachineBanc` pour le PIT et le PIC (la preuve que
l'IPS couvre ce que les boucles ne voient pas) ; verts ailleurs ; l'allocation rouge exact. La plus petite injection
vue rouge est la résolution réelle du protocole, consignée. Une répétition générale aussi : un renommage d'essai
(`pic` → `Intel8259A`) dans une copie, listings comparés avec la table des noms (identiques), banc vert.

**R0.6 — Le gel.** Le commit R (les bancs, l'outil, la table des s) ; le worktree de référence construit, ses
empreintes consignées ; la capture des listings au commit R (`listings-jit.sh capture`, gardée sous `/var/tmp`) ;
la table de R0.4 dans VERIFICATION.md. Portes et preuves : R remesuré contre lui-même, vert. Contrôle négatif :
l'outil lancé contre un worktree à un autre commit que R doit le dire (l'empreinte de la DLL de référence vérifiée à
chaque lancement).

## 3. Les risques

**Faux rouges.**
- Le bruit entre processus (2,5 à 5 %) plus grand que la résolution voulue : un test sans seuil verrait rouge des
  écarts de placement ; d'où s mesuré, et deux tours.
- Un processus sur un cœur E (4,0 contre 5,3 GHz) : d'où `taskset` sur les cœurs P.
- Une série, une construction, Rider qui indexe, à côté ; la fréquence et la température sur deux heures : d'où
  R C R C, qui répartit la dérive des deux côtés.
- Les disques de l'utilisateur, réécrits par ses séances (`a93c658`) : `MachineBanc` sans disque, comme la
  configuration de porte.

**Faux verts.**
- Un banc qui ne passe pas par le module renommé : les boucles à IF = 0 ne voient ni le PIC ni le PIT ;
  `MachineBanc` est là pour ça, et R0.5 le prouve.
- Des chiffres comparés à travers une mise à jour de .NET (10.0.12 → 10.0.13 le 21/10, sans prévenir) : seul le côte
  à côte, sur le même runtime, l'évite ; le comparateur refuse un runtime qui change.
- Une référence reconstruite avec un autre SDK : ses octets changent ; d'où les binaires gardés et leur sha256.
- `--job dry` ou un job sans la variable : le job mutateur la garde (`Program.cs:13-15`), et le comparateur vérifie
  l'environnement dans le JSON.
- Un seuil s trop large qui laisserait passer une perte réelle : d'où l'injection de ~3 % de R0.5, qui doit être
  vue.

**Autres.**
- La durée (de l'ordre de deux heures par commit) : à mettre en regard du nombre de commits de G14 et G15, inconnu.
- Le banc n'est pas dans `iXtal26.sln` : un renommage par Rider dans la solution ne le met pas à jour ; le banc du
  candidat devra suivre les noms à la main (vérifié pour la solution ; ce que G14 renomme et que le banc nomme dépend
  de la liste des puces). GardeBanc lit `setadc8` par réflexion, par son nom (`GardeBanc.cs:93`).
- Le worktree de référence sous `/var/tmp` survit aux redémarrages ; sous `/tmp`, il serait perdu (mémoire du dépôt).

## 4. Les décisions ouvertes

**D1 — « chaque machine » : lesquelles ?** PLAN.md ne les nomme pas. Proposition : les sept modèles des profils de
la racine (`ibmpc`, `ibmxt`, `olivetti_m24`, `pc1512`, `ami286`, `ami386dx`, `ami486`), chacun avec la carte vidéo et
la carte son de son profil principal, sans disque. C'est la lecture littérale, et le surcoût est d'environ
quatorze cas.

**D2 — Quel mode ?** PLAN.md (03/10) précède G13.11 (16/10), où « tout » devient le mode de l'utilisateur et
« aucun » celui des portes. Proposition : les deux, pour chaque cas ; une perte en « tout » est une perte pour
l'utilisateur, une perte en « aucun » ralentit les portes et l'oracle.

**D3 — Le seuil s.** PLAN.md dit « statistiquement significative (intervalle de confiance) », sans seuil ; or le
bruit mesuré dépasse 0 %. Proposition : s par cas, le plus grand écart A/A de R0.4 multiplié par 1,5, jamais sous
1 % ; le 0 % strict reste prouvé par les listings là où ils s'appliquent. À défaut, un s unique à fixer par Julien
au vu de la table de R0.4.

**D4 — Le jeu de cas et sa durée.** Les 52 cas d'aujourd'hui mesurent surtout chaque PB (27 pour le son).
Proposition : la référence ne garde que « aucun » et « tout » de chaque banc, plus les cas neufs ; le banc de G13
reste tel quel pour son usage (M1). Le même jeu complet à chaque commit, comme PLAN.md le dit ; aucun sous-ensemble.

**D5 — La forme du gel.** PLAN.md dit « une référence figée » sans dire si c'est un chiffre ou un binaire.
Proposition : les deux, avec des rôles distincts : le binaire de R, gardé et remesuré côte à côte, juge ; les
chiffres de R0.4, consignés, ne servent qu'à voir l'hôte dériver.

## Annexe A — Hors de la portée

Trouvé en route, non proposé :
- `iXtal26/iXtal26.csproj:27` renvoie à « VERIFICATION.md § M5.2 », qui n'existe pas (`grep '^## M5'` : M5.0, M5.1,
  M5.3).
- Is486Banc n'a jamais de chiffre consigné.
- Les commentaires d'Exec386Banc disent « l'ami386 » sans dire l'i386SX/16 de l'indice 0.
- Le `--job dry` des 10/10 a levé l'avertissement `MinIterationTime` (itérations de ~0,3 ms) ; le job par défaut ne
  l'a pas levé.
- FetchMateriel : un écart type de 4 % dans un processus (120,9 à 133,7 µs), plus large que ses voisins.
- La marge de l'ami486 sous `--charge ram`, 3,88 le 10/10 et 8,99 le 21/10, sur deux configurations : l'écart n'est
  pas expliqué.
- Les journaux de VideoBanc et SonBanc, perdus avec `/tmp/a9` au redémarrage du 20/10.

## Annexe B — Les commandes

- Les cas : `grep -c '\[Benchmark' tools/perfbanc/*.cs` (4, 3, 6, 4, 4, 6) et `grep -n '\[Params'` ; 4 + 4 + 3 + 6 +
  4 × 2 + (4 + 4 + 4 + 3 + 3 + 3 × 3) = 52.
- L'histoire : `git log --follow --format='%h %ad %s' --date=short -- tools/perfbanc/<fichier>`.
- Les chiffres et intervalles : `tools/perfbanc/bin/Release/net10.0/BenchmarkDotNet.Artifacts/results/*-github.md`,
  `grep 'ConfidenceInterval\|Run time' BenchmarkDotNet.Artifacts/*.log`.
- L'hôte : `lscpu -e=CPU,CORE,MAXMHZ`, `cat /sys/devices/system/cpu/cpu0/cpufreq/scaling_governor`,
  `dotnet --list-runtimes`, `dotnet --list-sdks`, `grep -B3 'dotnet-runtime-10.0:amd64 (10.0.12'
  /var/log/apt/history.log`.
- BenchmarkDotNet : `strings -n 4 BenchmarkDotNet.dll | grep -E '^(statisticalTest|coreRun|runtimes|affinity|…)$'`,
  `strings -el` pour les messages, `grep -n MsBuildArgument BenchmarkDotNet.xml`.
- Les listings : un script python3 `-I` jetable sur `/var/tmp/ixtal-g13/M2-13b/*.txt`, qui découpe par
  `; Assembly listing for method` comme `listings-jit.sh` (`:181-199`), compte 1 507 méthodes, 42 dont le nom
  commence par `iXtal26.Models.(pic|pit|dma)`, 39 autres dont le corps les nomme.
- Les machines : `grep -E '^(model|cpu|gfxcard|sndcard|mem_size) *=' ixtal26-*.cfg`.
