# G15 — Normaliser le C# ; le plan, sur reconnaissance

> Écrit le 4 novembre 2026, au commit `34814ec` (G14 fait). Bloc G15 de `PLAN.md` (décision utilisateur du 03/10 ;
> feu vert le 04/11 : « feu vert pour G15 »). Reconnaissance en trois lectures, en lecture seule, chacune suivie d'une
> contre-lecture qui l'a corrigée : `iXtal26/Docs/G15-reconnaissance/` (R0 les noms, R1 les constructions interdites,
> R2 le nettoyage, les outils et la porte ; `Rn-contre.md`). On retient ici les contre-lectures. Chaque constat y cite
> sa ligne ; ce plan renvoie aux rapports pour le détail.

## Où on en est

Les onze questions (Q40 à Q50, ci-dessous) sont tranchées par Julien le 04/11 : « ok pour tes propositions » ; chaque
proposition est la décision. G15.0a est faite (le banc des constructions interdites, la frontière des chemins chauds, la
base à R ; VERIFICATION.md § G15.0a). Q48 est rouverte le 05/11 : G15.P, la performance avant les renommages, est
faite (P0, quatre candidats mesurés ; P1, OP_ARITH par des structures, sous la règle du témoin calé, son résidu admis
le 08/11 ; VERIFICATION.md § G15.P). G15.0b est en cours, feu vert du 08/11 : 0b1 (la table des membres), 0b2
(`ildump`), 0b3 (`listings-jit.sh`) et 0b4 (le renommage des membres) sont faites (VERIFICATION.md § G15.0b). La
référence reste celle de G14 : le commit R = `f15e11f`, `/var/tmp/ixtal-g14/ref-R`, les séries `g14r` et `g14r-tout`
(VERIFICATION.md § G14.0d), jusqu'à la fin de G15.

### Ce que les lectures ont trouvé

- **La part des noms est le gros de G15.** iXtal26 déclare 8 976 types et membres (8 898 en comptant une seule fois
  les parties d'une classe partielle) ; environ 7 600 n'ont pas la forme .NET, à 99 % ceux du code transcrit et des
  `.Materiel.cs`, qui l'imitent (sur 900 méthodes du mode matériel, 886). S'y ajoutent, hors de la forme, 300
  paramètres et environ 570 locales d'iXtal26, 16 paramètres et 42 locales de Diff, 10 du banc, 3 fonctions locales et
  des `const` locaux. Les types et les membres de l'hôte, de Diff et du banc sont conformes, hors des exceptions
  voulues (117 P/Invoke `h_*`, les DTO des SST) et de `Host/SdlJoystick.cs`. (R0 § 1.1, R0-contre § 1.1.)
- **Une règle seule ne convertit pas tout.** 19 paires de noms ne diffèrent que par la casse sous la règle cible
  (`CS` et `cs`, `x86.cs:153-160`) ; trois membres prendraient le nom de leur conteneur (CS0542) ; six conteneurs
  commenceraient par un chiffre (`_808x`). Il faut des exceptions écrites. (R0 § 1.1, R0-contre § 1.1.)
- **`private` explicite est déjà fait.** Aucun membre sans modificateur, hors de six constructeurs statiques, que C#
  interdit de qualifier (CS0515). Seule la règle manque : aucun IDE0040 ne l'impose. (R0 § 1.2.)
- **1 046 constantes entières** : 267 viennent d'un `enum` de PCem, 636 d'un `#define`, 143 sont d'iXtal ; 351 servent
  de `case`, 246 de masque. 89 de leurs noms sont aussi dans le harnais, 67 dans son code ; deux seulement passent
  littéralement au P/Invoke (`ROM_IBMAT`, `AtProbe.cs:36` ; `FPU_387`, `X87Cases.cs:287`), le reste passe par des champs
  (`pc.romset`, `cpu_c.fpu_type`, `pc.gfxcard`). Un `enum` change les signatures : sa preuve est une substitution de
  types, pas une table de noms. (R0 § 1.3, R0-contre § 1.3.)
- **Ce qui dépend des noms des membres.** Au moins onze chaînes doivent suivre leur membre, dont deux dans
  l'émulateur : `"ILLEGAL"` (`386.Materiel.cs:313-315`) et `"opFCLEX"` (`x87.Materiel.cs:40`), qui ne casseraient que
  le mode « tout » ; et l'état que le banc compare à R imprime des noms de délégués (`RenduBanc.cs:158-165`,
  `Reference.cs:51`, `:53`, un `[CallerArgumentExpression]`). Environ 500 chaînes nomment le C et ne doivent pas
  bouger (les étiquettes des sondes, les clés de configuration, les clés R9). 1 264 champs de cache `<>O` et 200
  méthodes générées portent un nom de membre. Huit fichiers de `Cpu/` sont écrits par `tools/ops386-table.py` et
  `tools/x87gen/*.py`. (R0 § 1.4, R0-contre § 1.4.)
- **Les outils de la preuve ne savent renommer que des types.** `tools/noms/g14.txt`, `tools/ildump`,
  `listings-jit.sh --noms` et `tools/renommage` refusent ou ignorent les membres (`g14.txt:29`,
  `ildump/Program.cs:39`, `listings-jit.sh:34`). L'API `Workspaces` de Roslyn n'est pas dans `Roslyn/bincore` du
  SDK, mais `DotnetTools/dotnet-format` la porte, à la même version. (R0 § 1.5, R0-contre § 1.5.)
- **Des règles vivantes disent le contraire de G15** : `TRANSCRIPTION.md:100-102`, `:132`, `:154-199`,
  `.editorconfig:30-45` (« `grep -n` des deux côtés »), 18 blocs `// noms:` (« les fonctions gardent les noms de
  PCem », `fdc.cs:47`). `PLAN.md` tranche le but, pas la forme. (R0 § 1.6.)
- **Les constructions interdites, dans les chemins chauds** (156 fichiers, 82 917 lignes, la frontière que R1
  propose) :
  - aucun LINQ à l'exécution (un seul, dans un initialiseur statique, `video.Materiel.cs:59`), mais `ImplicitUsings`
    importe `System.Linq` partout ;
  - aucune interface, aucune méthode virtuelle ; les délégués nommés des tables de PCem sont la forme que R4 retient
    (`TRANSCRIPTION.md:60`, `:126`) ;
  - 43 lambdas qui capturent (41 du cœur 286/386/486, 2 du mode matériel), créées au remplissage des tables, jamais
    à l'exécution ; elles sont des DEVIATION déjà dites (à R, `386_ops_arith.cs:33-36`, `386_ops_jump.cs:30-32`) ;
    l'ADD y fait deux appels indirects de plus par instruction (vu au listing) ; 8 sites `Func<…>` dans `Cpu/` ;
  - 13 `new uint16_t[4]` du mode protégé (`x86seg.cs`), dont un (`pmodeiret`) probablement sur la pile : 12 au tas ;
    5 `box`, tous à l'initialisation ; aucun `params`.
  (R1 § 1, R1-contre.)
- **Le nettoyage** :
  - le code mort : au moins 191 méthodes et 55 champs qu'aucun IL ne référence. Ôtés les membres générés et les
    constructeurs servis par réflexion, 79 fonctions sont mortes chez PCem aussi (22, et les 57 handlers `_a32` du
    8087), 48 le sont parce que leur appelant n'est pas transcrit (PCjr, PS/1, PS/2, Tandy, wx et qt, le
    recompilateur), **une seule est propre à iXtal** : `X87NonTranscrit` (`386_ops_fpu.cs:127`), avec sa cascade
    `opX87NonTranscrit`, que `tools/x87gen/gentab.py:42`, `:74` émet encore. Le relevé par l'IL voit mort ce qui n'est
    appelé que sous une constante fausse (le `pclog` de `vid_svga.cs:228`, derrière `svga_output`) : il n'est pas mort ;
  - au moins 45 `using` certainement inutiles, les trois de § G14.2 compris ; le compte exact demande CS8019 ou
    IDE0005 ;
  - 0 avertissement, `TreatWarningsAsErrors` dans les 7 projets ; 43 règles à `none` dans `.editorconfig`, 60
    `#pragma` dans 53 fichiers, dont un superflu (`Flash/rom.cs:12`) ; ni règle de nommage, ni `AnalysisLevel`, ni
    `global.json` ; 22 fichiers transcrits hors du glob de `.editorconfig:40` et de la Portée de `TRANSCRIPTION.md`.
  (R2 § 1, R2-contre.)
- **Les outils** : le SDK 10.0.114 suffit (les analyseurs CA et IDE, `dotnet format`, Roslyn, `DOTNET_JitDisasm`,
  `MemoryDiagnoser` déjà actif). Aucun paquet n'est nécessaire ; BannedApiAnalyzers est le seul utile (le
  « signalé », LINQ seul), absent du cache local. ClrHeapAllocationAnalyzer, `jb inspectcode`, `dotnet-counters` et
  `dotnet-trace` n'apportent rien qu'une porte de G15 demande. (R2 § 1.5, R1 § 1.8.)
- **La porte** : une étape qui change l'IL coûte de 4 h à 4 h 40 de machine (les deux séries, 1 h 51 à 1 h 54 et 48 à
  49 min ; le banc, 53 à 65 min ; la capture du JIT, 15 min ; VERIFICATION.md § G14.2-G14.4 ; G14.1, avec son
  réancrage et deux rejeux, en a pris environ 7 h 15). Ôter une méthode
  renumérote les lambdas et les fermetures de son type (`<Run>b__0_0`, `<>c__DisplayClassN_M`), dans l'IL comme dans
  les listings : les outils doivent le normaliser. (R2 § 1.6-1.7, R2-contre.)

## Le principe

Celui de G14, étendu : un nom change, rien d'autre ; une construction se retire, rien d'autre. La preuve en est faite
à chaque commit qui change l'émulateur :
- l'IL identique, par la table des noms (types et membres), ou égal à l'écart attendu écrit d'avance ;
- le code machine identique de même ;
- toutes les séries identiques à l'unité, dans les deux modes, contre `g14r` et `g14r-tout` ;
- le banc vert contre le binaire figé R.

Les `enum` viennent après les noms, avec leur propre preuve. Les constructions interdites ne s'introduisent pas, et le
banc d'ouverture chiffre ce qu'elles coûtent.

## Les étapes

L'ordre : G15.0a, G15.P (la performance, ajoutée le 05/11), 0b, 0c (le banc, les outils, les règles), G15.1 (le
nettoyage), G15.2 à G15.8 (les membres, une famille à la fois), G15.9 (les paramètres et les locales), G15.10 (les
enums), G15.11 (la clôture). G15.0a ne
dépend d'aucune question ; G15.0b, de Q40 pour sa table et de Q45 pour ce que l'outil journalise ; G15.0c et la suite,
de Q40 à Q50 selon l'étape (dit à chacune).

### G15.0a — Le banc des constructions interdites (l'ouverture) ✅

`PLAN.md` le veut « en ouverture de G15 ». Neuf cas dans `iXtal26.PerfBanc`, hors des 65 cas de la référence des 0 %
(`compare-ref.sh` ne les nomme pas, comme GardeBanc et Is486Banc) ; iXtal26 n'est pas touché. Chaque cas mesure la
construction interdite contre sa forme retenue, sur un vrai chemin d'iXtal, en quatre colonnes : le vrai code (appelé
comme le produit l'appelle), la copie retenue, la copie interdite, et une seconde copie retenue (l'A/A, le placement).
L'équivalence des copies et du vrai code est vérifiée avant la mesure.

| Cas | Le vrai chemin | L'interdit mesuré |
|---|---|---|
| 1 | `readmemb` (`Cpu/386_common.cs:163-169`), sur la RAM d'une machine montée | les mêmes champs, par une instance |
| 2 | `speaker_get_buffer` (`Sound/sound_speaker.cs:70-71`) | `Enumerable.Range` et `Select` |
| 3 | l'ADD d'`OP_ARITH` (à R, `386_ops_arith.cs:103-131`) | une interface et un `override`, à un site polymorphe |
| 4 | le Jcc (`386_ops_jump.cs:79-87`), une fermeture | une méthode nommée (le vrai est l'interdit) |
| 5 | une affectation de rendu de `svga_recalctimings` | une lambda qui capture, créée à chaque appel |
| 6 | le descripteur de `loadseg` (`x86seg.cs:80`, `new uint16_t[4]`) | `stackalloc` (le vrai est l'interdit) |
| 7 | l'aiguillage de `outb_corps` (`io.cs:187-193`) | `priv` mis en boîte à chaque appel |
| 8 | une interpolation de chaîne gardée par un champ, sur un vrai chemin | la même, sans garde |
| 9 | `prefetch_run` (`Cpu/386.cs:245`) | la même signature en `params int[]` |

- **La forme de chaque cas** (R1-contre) : au cas 3, le site d'appel est polymorphe (au moins trois implémentations),
  sinon le JIT dévirtualise, et « l'appel direct » est un appel de méthode statique ; aux cas 3, 4 et 6, le vrai code
  est la forme interdite, ou un délégué : sa colonne se compare à celle qu'il incarne ; ailleurs, à la retenue. Le cas
  8 ne prend pas `svga_output`, une constante (`vid_svga.cs:223`) : un vrai chemin gardé par un champ, ou le cas est
  dit micro-banc d'interpolation. Au cas 6, la forme retenue changerait des signatures (`do_seg_load`,
  `taskswitch286`) : la copie seule la porte, et le cas compte la mise en place d'un mode protégé. Le mode de la
  machine (« aucun » ou « tout ») est écrit par cas.
- **Le lancement** : comme `compare-ref.sh`, sous `setarch -R taskset -c 4,6,8,10`, `--launchCount 3`, l'échauffement,
  les itérations et les invocations fixés (`compare-ref.sh:281-283`) ; sans les fixer, 27 à 63 min par passage.
- **Le verdict** (décision n° 6) : sur les moyennes de lancement, comme la référence, pas sur l'intervalle de
  BenchmarkDotNet entre colonnes, qui sont des processus distincts (`G14-reconnaissance/R0-contre.md`).
- **Portes et preuves** : la construction sans avertissement ; `git diff -- iXtal26 iXtal26.SoftFloat` vide ; l'IL
  d'iXtal26 égal à celui de R par la table de G14 ; l'IL des types du banc qui existaient égal, seuls des types neufs ;
  les chiffres dans VERIFICATION.md § G15.0a : par cas, le rapport, sa dispersion, les octets alloués
  (`MemoryDiagnoser`), l'extrait du code machine qui l'explique (`DOTNET_JitDisasm`), la commande. Ni série ni passe de
  la référence : ni l'émulateur ni la série ne changent (décision n° 5, Q50).
- **La base des constructions interdites à R** : le relevé de la décision n° 7 (le `grep` et l'IL) joué sur R, écrit
  une fois, avec les 43 fermetures, les 8 `Func<…>` et les 13 `new uint16_t[4]` nommés un à un (Q48) ; chaque étape
  qui suit s'y compare.
- **Contrôles négatifs** : une copie falsifiée (un drapeau faux dans la copie interdite du cas 3) fait échouer
  l'équivalence ; la copie retenue du cas 6 qui alloue fait paraître des octets ; le cas 2 interdit montre des octets,
  la retenue 0.
- **Fait le 04/11** (VERIFICATION.md § G15.0a). Ce qui s'écarte de ce qui précède, et pourquoi :
  - au cas 8, la retenue est la garde pliée du vrai (`svga_output` est une constante), et la garde par un champ une
    colonne de plus ; au cas 1, l'instance en deux colonnes, l'appel gardé comme dans le produit et l'appel inliné par
    le JIT ; au cas 3, Delegue, la forme du vrai ;
  - le seuil s'élargit au placement du vrai quand son code machine est celui de sa copie (six cas sur neuf ; il ne
    joue qu'aux cas 1, 3 et 8) : l'A/A, deux copies du même assemblage, ne voit pas l'écart entre deux assemblages (de
    12 à 27 % à code égal, selon la construction) ;
  - la base compte 14 `new uint16_t[4]` : les 13 de `x86seg.cs` et celui de `porte_tache_materiel` ; et l'IL s'y écrit
    en comptes par méthode et par instruction, le détail (1,9 Mo) se régénérant de R.

### G15.P — La performance avant les renommages ✅

Ajoutée le 05/11, Q48 rouverte (« vu tous les coups, on a rien changé ? »). Elle vient avant G15.0b, pour que G15.1 à
G15.8 renomment le code final, et que leurs outils n'aient pas à normaliser des fermetures appelées à disparaître.
- **P0, mesurer avant de changer** : quatre candidats, chacun seul dans un worktree, rien de commité. Ce sont le Jcc
  sans fermeture, OP_ARITH par des structures, le descripteur sur la pile et `readmemb` inliné ; chacun passe les trois
  passes du banc contre R. Aucun n'est vert contre R. La perte d'OP_ARITH sur le 286 est un effet de placement :
  montré par un témoin de calage, R plus quatre appels vides dans le banc, qui perd autant ou plus sur le 286
  sans rien changer d'iXtal26. Julien garde OP_ARITH seul (« garde OP_ARITH, règle du témoin calé », 05/11).
- **P1, OP_ARITH** : `OP_ARITH<TAlu>` et `OP_ARITH_386<TAlu>` sur `IAluArith` (des membres `static abstract`, une
  structure par famille, la forme retenue du cas 3 de G15.0a). Ses portes sont complètes :
  - les séries entières dans les deux modes, 0 écart sur 827 portes chacune ;
  - le banc contre le témoin calé : 61 verts et 4 rouges retenus (+0,13 à +0,36 %, sur le pc1512, l'olivetti_m24 et
    l'ibmat), sur des machines dont trois n'exécutent pas le code changé ;
  - l'IL relevé, tout le changement dans OP_ARITH ;
  - la construction sans avertissement, les lignes ≤ 120.

  Trois témoins de plus (le dernier sans banc, faute de reproduire l'empreinte de types) et les compteurs du
  processeur n'ont pas trouvé la cause des rouges : ils ont écarté le tas et vu le front-end différer. Julien admet
  le résidu (« admets le résidu, commite OP_ARITH », 08/11).
- **Fait le 08/11** (VERIFICATION.md § G15.P). Ce qui s'écarte de ce qui précède :
  - le cas 3 du banc des interdits a désormais pour vrai la forme retenue ; `interdits.py` admet le registre de
    l'argument (une lambda de classe cache contre une copie statique) ;
  - les passes 2 et 3 montrent deux cas verts par la règle plus lents, l'ibmxt « aucun » (+0,8 %) et l'ibmpc
    « aucun » (+0,4 %), du même résidu.
  - Jcc, le descripteur et `readmemb` ne sont pas retenus : Q48 reste, pour eux, ce que la proposition du 04/11
    disait.

### G15.0b — Les outils de la preuve des membres

Rien de l'émulateur ne change. Feu vert du 08/11. Six sous-étapes, chacune commitée avec ses portes (la DLL
d'iXtal26 identique au sha256, ses essais et ses contrôles négatifs ; ni série ni banc) :
- **0b1, la table des membres** ✅ : la grammaire étendue, la lecture étape par étape, la règle de Q40, le
  générateur et la vérification (`tools/renommage generer`, `verifier`) ;
- **0b2, `ildump`** ✅ : la table des membres appliquée, les noms générés normalisés, l'écart attendu,
  `references`, l'option des paramètres ;
- **0b3, `listings-jit.sh compare`** ✅ : la table des membres, les noms générés, la liste close ;
- **0b4, `tools/renommage`** ✅ : les membres et la vérification de liaison ;
- **0b5, les générateurs et les petits outils** : les générateurs de `Cpu/` régénérés à HEAD et sensibles à la table
  (venus de 0b4 : ils transcrivent par du texte, il leur faut une traduction des identifiants qu'ils écrivent) ; les
  relevés (les `using`, le code mort privé, les constructions interdites) ; `compare-ref.sh` ; `check-doc5150.sh` ;
- **0b6, la répétition générale** sur `Ics/Intel8259A.cs` et ses appelants, les contrôles négatifs d'ensemble, la
  clôture de l'étape.

Ce qui suit est le contenu de l'étape entière.
- **La table des membres** : générée par la règle de Q40, avec ses exceptions écrites et ses refus (une collision,
  un membre absent, un nouveau égal à son conteneur) ; la liste close des littéraux qui suivent leur membre (les onze
  chaînes, les états attendus de `Reference.cs`), lue par `ildump` comme par `listings-jit.sh` ; la grammaire de
  `tools/noms/` étendue aux champs, méthodes, constantes, paramètres.
- **`ildump`** : la table des membres appliquée, puis le vidage retrié ; la normalisation des noms générés
  (`<>O`, `b__N_M`, `<>c__DisplayClassN_M`) ; un fichier d'**écart attendu** (les entrées qui doivent disparaître ou
  changer : la comparaison rend 0 si et seulement si l'écart est exactement celui-là) ; le relevé des références
  (`ildump references`), avec ses faux positifs connus nommés (les six sites de réflexion, les dix
  `ConditionalWeakTable`, les cas de BenchmarkDotNet, les appels sous une constante fausse) ; l'option des paramètres.
- **`listings-jit.sh compare`** : la table des membres, les noms générés normalisés, la liste close.
- **`tools/renommage`** : les membres, par l'API `Renamer` de Roslyn (celle de `DotnetTools/dotnet-format`, sans
  paquet) ou par sa syntaxe étendue, avec une vérification de liaison (un masquage, une autre surcharge par deux
  `using static` : refusés). *Fait en 0b4 par la syntaxe étendue ; le masquage est éprouvé, les deux `using static`
  n'ont pas d'essai propre (VERIFICATION.md § G15.0b4).*
- **Les générateurs de `Cpu/`** renommés avec les fichiers qu'ils écrivent, `gen46.py`, qui relit et retouche ceux de
  `gen43` et `gen44`, compris. *En 0b5, avec leur porte « régénéré = commité ».*
- **Le relevé exact des `using`** : CS8019 par le Roslyn de `tools/renommage`, IDE0005 dans une construction
  jetable (le `globalconfig` de style du SDK, `AnalysisLevelStyle`, la règle sans liste à la main), ou `dotnet format
  style --diagnostics IDE0005`, au premier essai ; IDE0051 et IDE0052 à `warning` dans une copie jetable, en second
  relevé du code mort privé.
- **Le relevé des constructions interdites** (décision n° 7) : le `grep` sur la liste des chemins chauds, et le
  relevé d'IL d'`ildump` (les `newobj` de fermeture, les `box`, les `callvirt` d'interface, les appels à
  `System.Linq`), comparés à la base écrite à R (G15.0a).
- **Les générateurs de `Cpu/`** : régénérés à HEAD, avant tout renommage, les huit fichiers comparés à l'octet aux
  fichiers commités (jamais joué jusqu'ici).
- **`compare-ref.sh`** : l'état imprimé normalisé par la table ; **`check-doc5150.sh:68`** : le symbole cherché hors
  des commentaires (son `grep -qw` trouve aujourd'hui un nom resté dans un commentaire de PCem).
- **Portes et preuves** : `iXtal26.dll` identique au sha256, construit avant le commit (la DLL porte son commit dans
  sa version) ; `ildump` et `compare` d'une capture contre elle-même :
  0 ; la répétition générale dans une copie jetable : les membres d'`Ics/Intel8259A.cs` et leurs appelants renommés par
  l'outil, l'IL et les listings identiques avec la table, rouges sans elle, et un sous-ensemble ciblé de la série
  identique ; `check-doc5150` juste. Ni série entière ni banc.
- **Contrôles négatifs** : une table avec une collision (`CS` et `cs` vers `Cs`), un membre absent, un nouveau égal
  au conteneur : refusés ; dans une copie, un champ renommé vers le nom d'une locale qui le masque : refusé par la
  liaison, écart d'IL ; `"opFCLEX"` laissé à l'ancien nom : refusé par la liste ; une méthode ôtée avant une lambda :
  l'écart attendu tient, le nom généré normalisé ; un écart attendu faux (une entrée de trop, une de moins) : retour 1 ;
  un renvoi dont l'ancien nom ne reste qu'en commentaire : `check-doc5150` rouge ; une construction interdite glissée
  dans une copie, dans un fichier de la liste chaude, sort du relevé ; la même dans `Host/`, non.

### G15.0c — Les règles

Selon Q40 à Q46.
- `TRANSCRIPTION.md`, dans son plafond de 260 lignes (il en a 249 : les ajouts se paient de reformulations, ou le
  plafond se relève, comme en G13, décision n° 15) : R1(e) (la correspondance, Q41), la Portée (les 22 fichiers
  hors d'elle), la table des conventions, le nommage explicite, les collisions, R2 et R10 selon Q43 ;
  la dérogation de Q45 sur les lignes longues, si elle est retenue ; `.editorconfig` : les règles `dotnet_naming_*`
  de la forme .NET, IDE0040 à `warning` dès cette étape (0 site à corriger), les exceptions voulues (les P/Invoke, les
  DTO, ce que Q43 décidera) ; les extinctions d'IDE1006 retirées répertoire par répertoire, à l'étape qui le renomme ;
  les globs alignés sur la Portée, `io.cs` et `timer.cs` compris.
- **Portes** : la construction sans avertissement ; la DLL identique au sha256, construite avant le commit
  (`.editorconfig` ne règle que des diagnostics). **Contrôles négatifs** : dans une copie, un membre sans modificateur,
  et un nom en `snake_case` dans un répertoire dont l'extinction est retirée, font échouer la construction.

### G15.1 — Le nettoyage

Selon Q47.
- Les `using` relevés par G15.0b ; le code mort propre à iXtal (`X87NonTranscrit`, sa cascade, `gentab.py:42`, `:74`,
  les commentaires qui les nomment ; dans Diff, `BootDiff.PasCsharp`, `BootDiff.cs:1072`) ; le pragma superflu de
  `Flash/rom.cs:12` et son commentaire périmé ; chaque candidat passé par un `git grep -w` avant retrait. Le code mort
  transcrit selon Q47.
- Les renvois `fichier.cs:N` que les lignes ôtées décalent (150 dans 56 fichiers, `PCEM_BUGS.md` compris) sont
  recalés, chacun vérifié sur le texte de la ligne citée, comme en G14.5.
- **Portes et preuves** : `ildump comparer` contre le parent égal à l'écart attendu, et contre R par la table de G14 ;
  les listings contre R, l'écart attendu ; les séries entières dans les deux modes ; le banc contre R ; `check-oracle`,
  `check-doc5150` ; R2 recompté sur les fichiers transcrits touchés ; le relevé des constructions interdites égal à la
  base de R. **Contrôles négatifs** : une méthode vivante ôtée
  dans une copie fait échouer la construction ; `"ILLEGAL"` renommé dans une copie : les portes du mode « tout » qui
  décodent sur le 386 rougissent (la preuve que les séries couvrent la réflexion).

### G15.2 à G15.8 — Les membres, une famille à la fois

Selon Q40 à Q46. Une famille est un ensemble de répertoires avec leurs `.Materiel.cs` (une classe partielle ne se
renomme qu'entière) ; son commit touche tous ses appelants (iXtal26, Diff, le banc), comme G14.
- G15.2 l'hôte et les restes des outils (`SdlJoystick.cs`, `HddImage.cs`, deux méthodes de Diff) : la répétition du
  circuit entier, petite ;
- G15.3 `Ics/`, `Floppy/`, `Disc/` (~1 000 noms, leurs blocs `// noms:` récrits) ;
- G15.4 `Video/` (~1 120) ;
- G15.5 `Sound/`, `Joystick/`, `Lpt/`, `Mouse/`, `Keyboard/` (~1 130) ;
- G15.6 `Cdrom/`, `Ide/`, `Scsi/`, `Mfm/` (~1 280) ;
- G15.7 `Memory/`, `Models/`, `PluginApi/`, `Diag/`, `Devices/`, `Flash/`, la racine, `Materiel/` (~950) ;
- G15.8 `Cpu/` (~2 280), le chemin le plus chaud, en dernier, avec ses huit fichiers générés et leurs générateurs ;
  `iXtal26.SoftFloat/` selon Q46.

**Portes et preuves** : la construction sans avertissement ; l'IL d'iXtal26, de Diff et du banc identique par la table
(0 changée, 0 disparue, 0 apparue, hors de la liste close) ; l'IL de SoftFloat de même en G15.8 ; les
listings par la table et par les jetons : 0/0/0 ; les séries entières dans les deux modes contre `g14r` et
`g14r-tout` ; le banc vert contre R ; `check-oracle` sans dérive, `check-doc5150` juste, la `.so` inchangée ; le nombre
de lignes vives de chaque fichier transcrit inchangé (R2) ; les générateurs régénèrent les fichiers commités à l'octet
(G15.8) ; `grep` des anciens noms hors commentaires et chaînes : 0, hors de la liste ; les lignes longues selon Q45 ;
le relevé des constructions interdites égal à la base de R.
G15.2, l'hôte, porte un sous-ensemble ciblé de la série au lieu de la série entière (l'hôte n'est pas sous l'oracle ;
le banc reste dû).
**Contrôles négatifs** : `ildump` et `compare` sans la table des membres sortent exactement les membres renommés ;
`git diff` de `BootDiff.cs`, `VgaProbe.cs`, `Fuzzer.cs` limité aux sites qualifiés (aucune étiquette changée) ; un
générateur relancé avec une table tronquée produit un écart.

### G15.9 — Les paramètres et les locales

Les paramètres, les locales, les fonctions locales et les `const` locaux hors de la forme `camelCase`. L'IL des
locales doit être identique **sans** table (leurs noms ne sont que dans le PDB), celui des paramètres identique par
l'option d'`ildump` : si `ildump` et les listings rendent 0/0/0, un sous-ensemble ciblé de la série suffit, et le banc
n'est pas rejoué (décision n° 5, Q50) ; le relevé des constructions interdites égal à la base de R. **Contrôle
négatif** : une locale renommée vers le nom d'un champ qu'elle masque, dans
une copie : refusée par la liaison, écart d'IL.

### G15.10 — Les enums

Selon Q44. Un enum se déclare dans le fichier qu'il remplace ; sa valeur égale celle du C, membre par membre. Une
seule passe (la même preuve pour toutes les familles).
- **Portes et preuves** : l'IL identique par la table et la substitution de types (`ildump` : l'enum ramené à son
  type sous-jacent dans les signatures, les conversions explicites relevées) ; les listings de même (que le JIT y nomme
  l'enum est à montrer d'abord dans une copie jetable) ; les séries entières dans les deux modes (une valeur qui
  s'écarterait du C rougirait les boot-diffs et les portes qui passent par `h_set_*`) ; le banc vert contre R ; le
  relevé des constructions interdites égal à la base de R.
- **Contrôles négatifs** : dans une copie, un membre d'enum de valeur changée : écart d'IL et série rouge ; un champ
  d'enum imprimé par interpolation dans une sonde : série rouge (le nom au lieu du nombre) ; la substitution d'un type
  absent de la table : refusée.

### G15.11 — La clôture

- `AnalysisLevel` épinglé dans les 7 projets ; `EnforceCodeStyleInBuild` ; les règles que G15 a fait respecter à
  `warning` (le nommage, IDE0040, IDE0005, IDE0079) dans leurs globs ; le « signalé » des constructions interdites,
  selon Q49 (décision n° 7). L'IL ne change pas. **Portes** : la construction sans avertissement, la DLL identique au
  sha256 avant le commit. **Contrôles négatifs** : dans une copie, un nom hors de la forme, un `using` inutile et une
  construction interdite, chacun dans un fichier de la liste chaude, font échouer la construction ou le relevé.
- `VERIFICATION.md` § G15 : la table des noms en tête (le lien avec l'historique, PLAN-G14 décision n° 10), les
  commits, les séries, l'IL et les listings avec et sans la table, les passes du banc, ce que coûtent les
  constructions interdites, ce qui reste. `PLAN.md` dit « G15 est fait » et met [ICI] avant G16.

## La vérification

- **L'IL et le code machine** : identiques, par la table des noms de G14 et celle des membres, à chaque commit de
  G15.2 à G15.9 ; égaux à l'écart attendu en G15.1 ; égaux par la substitution de types en G15.10.
- **Les séries** : entières, dans les deux modes, à chaque commit qui change l'émulateur, contre `g14r` et
  `g14r-tout` après normalisation (`compare-series.py`) ; un sous-ensemble ciblé quand l'IL d'iXtal26 est identique à
  celui de son parent (décision n° 5).
- **Le banc** : contre le binaire figé R, jamais contre le commit précédent, selon le protocole de § G14.0b (la
  troisième passe de Q37) ; sa résolution, celle de § G14.0b ; là où elle est plus grossière que le 0 % (le décodage en
  « tout », Fetch, RenduCga, Video, trois boucles d'Exec), le 0 % repose sur les listings.
- **La règle du témoin calé** (05/11, G15.P) : une étape qui change le code des chemins chauds se mesure contre un
  témoin, le commit de base plus un calage hors dépôt, qui met le code jitté au même placement modulo 64 que le
  candidat. Le placement se vérifie sur les 65 cas par la carte des méthodes jittées. Son résidu est ce que la règle
  ne tient pas, les précodes et les stubs du runtime : quelques dixièmes de pour cent, écrits à l'étape.
- **Le runtime** : une mise à jour d'apt de .NET entre deux étapes demande le réancrage de VERIFICATION.md § G14.1 ;
  `ref-R` ne bouge pas. Un SDK neuf reconstruit dans le job de BenchmarkDotNet le banc de R depuis son source : le gel
  du lancé (`--lancees-r`) le vérifie à chaque passage, et le passage de 10.0.113 à 10.0.114 a rendu les mêmes DLL
  (§ G14.1).
- **Rien d'autre ne change** : avant chaque étape, la vérification de § G14.0d (aucun commit hors `g15:` depuis R sur
  ce que la série et le banc lisent, hors les commits `g14:`).
- **L'oracle** : sa `.so` inchangée ; G15 ne touche pas `tools/oracle/`, sauf un commentaire qui nommerait un membre
  renommé (corrigé ligne pour ligne, la `.so` reconstruite égale, comme en G14.5).

## Les décisions

Le principe de G15, sa portée et sa porte sont décidés (`PLAN.md` § G15 et l'annexe, 03/10 ; PLAN-G14, décision
n° 1). Les décisions ci-dessous en découlent ; ce qu'elles laissent ouvert est posé à Julien (Q40 à Q50). Les
décisions 5 à 7 sont des choix du plan dans ce cadre, dits comme tels : Julien peut les défaire.

1. **L'ordre** : le banc d'ouverture, les outils, les règles, le nettoyage, les membres par famille, les paramètres et
   les locales, les enums, la clôture. *(Le nettoyage avant les renommages : moins de membres à renommer et à
   prouver ; les enums après les noms : les mêmes lignes ne se touchent pas deux fois ; `Cpu/` en dernier : le chemin
   le plus chaud, quand l'outil a fait ses preuves sur six familles.)*
2. **`private` explicite** : rien à écrire ; IDE0040 l'impose dès G15.0c. *(R0 § 1.2.)*
3. **Ce qui garde sa forme, sans question** : les étiquettes des sondes, les clés de configuration, les clés R9 et les
   noms de fichiers de PCem (`fdc.cs:48-49` pour les étiquettes) ; les P/Invoke et les DTO (`.editorconfig:112-119`) ;
   les marqueurs `// pcem:` et `// pcem bug … PB-nn` (`PLAN.md:572`). Les registres vivants suivent (`PCEM_BUGS.md`,
   `Docs/ibm5150/`) ; les journaux datés restent tels qu'écrits (PLAN-G14, décision n° 10).
4. **L'outil du renommage** : le script (`tools/renommage`, étendu), dans un worktree ; la préversion de Rider en
   contre-compte, là où elle voit (Q38). *(Rider ne voit ni Diff ni le banc, ni les chaînes liées.)*
5. **Un commit dont l'IL d'iXtal26 est identique à celui de son parent** (0/0/0 sans table : G15.9, une règle de
   `.editorconfig`, un commentaire) porte un sous-ensemble ciblé de la série et pas de passe du banc : son code machine
   est celui de son parent. *(La règle du dépôt, « la série entière quand l'émulateur change » ; le précédent de
   G14.5. `PLAN.md:592-593` veut la mesure « à chaque commit de G14 et de G15 » : l'écart à la lettre est Q50.)*
6. **Le banc d'ouverture** est hors de la référence des 0 %, joué une fois, rejoué si une étape touche une
   construction de sa liste. Son verdict : « ça coûte » quand l'écart des moyennes de lancement entre la colonne
   interdite et la retenue dépasse celui de l'A/A et la résolution de § G14.0b ; sinon le cas est écrit « pas de coût
   mesurable sur ce chemin », avec son code machine, et la règle reste : elle est la décision de Julien, le banc
   l'éclaire sans la défaire. *(R1 D4 et D5, R1-contre.)*
7. **La frontière des chemins chauds** est une liste de fichiers, écrite une fois (`tools/noms/` ou à côté), lue par la
   revue et par `.editorconfig` : `PLAN.md` dit « les dossiers des chemins chauds », mais `Ics/` et `Cpu/` mêlent du
   chaud et du froid. Dedans : `Cpu/` sans les `*.State.cs`, `iXtal26.SoftFloat/` (ses sources suivies),
   `Memory/mem.cs`, `io.cs`, `timer.cs`, `Intel8253`, `Emu8000` et les deux RAMDAC, `Video/`, `Sound/`, `lpt_dac` et
   `lpt_dss` ; `ppi.cs` et `rtc.cs` classés à l'écriture. Le « signalé » est, sans paquet : un `grep` des
   constructions interdites sur cette liste, et le relevé d'IL d'`ildump` (les `newobj` de fermeture, les `box`, les
   `callvirt` d'interface, les appels à `System.Linq`), contre une base écrite à R, dans la porte de chaque étape. Un
   analyseur dans la construction demande un paquet : Q49. *(PLAN.md : « si possible, un analyseur ou une règle
   `.editorconfig` ; sinon, une revue par `grep` dans la porte de G15 » ; R1-contre, D1 et D3.)*
8. **Un commit par étape**, chacun avec sa porte ; une famille qui rougit est refaite seule (PLAN-G14, décision
   n° 11). Le préfixe `g15:` pour chaque étape, `plan:` pour ce fichier.

**Les questions à Julien** :

- **Q40 (G15.0c, G15.2-G15.8) — La règle de conversion.** `PLAN.md` dit « PascalCase » sans dire comment l'on convertit
  `CS` et `cs`, `_808x`, `pc_timer_t`, `opADD_b_rmw`, `AL`, `IRQ`, ni le style des champs `internal` (2 663 des 2 970
  champs hors de la forme) et des `private static`.
  *Proposé : les conventions du .NET, appliquées par un générateur : PascalCase pour les types, les méthodes, les champs
  `internal` et les constantes ; camelCase pour les champs privés, les paramètres et les locales ; un sigle de deux
  lettres en majuscules (`CS`, `AL`, `IO`), au-delà une majuscule (`Fdc`, `Pit`) ; un champ privé sans préfixe `_`
  (`speakerPos`) ; les 15 champs `private static readonly` de Diff et du banc, déjà en PascalCase, restent ainsi ; en
  option, le suffixe `_t` d'un type d'état tombe quand rien ne collisionne (un renommage de plus que le PascalCase :
  `pic_t` deviendrait sinon `PicT`) ; un conteneur qui commencerait par un chiffre prend le nom de sa famille (`_808x` →
  `Cpu808x`) ; chaque collision (les 19 paires, les trois CS0542) reçoit une exception écrite avec sa raison. C'est
  cette liste, quelques dizaines de lignes, que tu valides, pas les 6 500 noms.*
- **Q41 (G15.0c) — Où vit la correspondance PCem ↔ iXtal26.** R1(e) l'exige (« sans elle, plus rien ne relie le
  fichier à `pcem-dev/` », `TRANSCRIPTION.md:26-27`), mais borne un bloc `// noms:` à 40 lignes ; 64 fichiers ont plus
  de 40 noms à changer.
  *Proposé : la table générée de `tools/noms/` est la correspondance ; les blocs `// noms:` disent la règle, les noms
  modifiés à la main et le fichier C ; aucune ligne `// pcem:` ajoutée par fonction. (L'autre voie, le nom C sur une
  ligne `// pcem:` de chaque fonction renommée, garde le `grep -n` des deux côtés, mais coûte jusqu'à ~3 300 lignes de
  commentaire, décale ~292 renvois `fichier.cs:N` et laisse l'ancien nom dans un commentaire, où `check-doc5150` le
  trouverait.)*
- **Q42 (G15.2-G15.8) — Les fichiers.** Les conteneurs prennent la forme .NET ; leurs fichiers suivent-ils
  (`vid_cga.cs` → `VidCga.cs`) ?
  *Proposé : non ; les fichiers restent le miroir du chemin C (`TRANSCRIPTION.md:133`) : `oracle.tsv` (259 lignes),
  `PCEM_BUGS.md` (614 chemins), `check-doc5150`, les `.cfg` et les renvois `fichier.cs:N` les nomment. G14 a su les
  déplacer, outillé, mais un déplacement réordonne le tas #US (Q39) pour aucun gain de lecture ; le bloc d'en-tête dit
  le nom du conteneur.*
- **Q43 (G15.0c, G15.7) — `materiel` et `materiel.pb_nn`.** La forme sert la règle de compte de R2
  (`TRANSCRIPTION.md:43`), la garde de R10 (`:88`), 787 gardes dans 76 fichiers et 183 mentions de `PCEM_BUGS.md`, et
  reflète le marqueur `PB-nn`.
  *Proposé : la garder, en exception écrite de la règle de nommage, comme les P/Invoke et les DTO : elle est un marqueur
  autant qu'un nom. C'est une dérogation à PLAN-G14, décision n° 1 (« chaque membre en PascalCase »).*
- **Q44 (G15.10) — Quels `const` deviennent des `enum`, et quel type portent les champs.**
  *Proposé : (a) les familles d'un `enum` de PCem (267 constantes) et les familles de `#define` qui sont des ensembles
  d'états ou de commandes lus en `case` (`COMMAND_*`, `GPCMD_*`, `WIN_*`, `CMD_*`) deviennent des `enum` ; les champs
  qui ne tiennent que ces valeurs prennent leur type, hors de ceux que le harnais lit en `int` (`pc.romset`,
  `cpu_c.fpu_type`, `pc.gfxcard`, castés au P/Invoke) ; (b) les familles de bits (les masques de registre) restent des
  constantes, en PascalCase : la valeur d'un registre n'est pas l'une de ses valeurs nommées, et chaque lecture de port
  demanderait un cast si les champs prenaient ce type ; (c) les constantes isolées (tailles, adresses) restent des
  constantes. Deux temps : quelles constantes deviennent des `enum`, puis quels champs prennent leur type. Les trois
  s'écartent de la lettre de `PLAN.md` (« `enum` au lieu des constantes `int` ») : le (a) va plus loin (il retype des
  champs, des paramètres, des retours), le (b) et le (c) moins ; c'est à toi de dire ce qu'elle couvre. Les familles
  sont comptées par préfixe (35 familles et 8 isolées) : un `enum` de PCem coupé en deux par son préfixe se recoud à
  l'écriture.*
- **Q45 (G15.1-G15.9) — Les lignes de plus de 120 caractères qu'un renommage touche.** iXtal26 en a 1 844 et Diff
  1 362 ; un renommage en toucherait environ 1 163 et 810, dont 898 et 786 resteraient au-delà de 120 (une simulation
  de R0-contre). La règle compte toute ligne touchée comme neuve ; les recouper toutes changerait R2 et la mise en forme
  de plus de mille lignes du transcrit.
  *Proposé : une ligne que le renommage seul touche sans l'allonger garde sa coupe, et l'outil le journalise ; toute
  ligne allongée, ou neuve, tient en 120. C'est une dérogation à la règle des 120, d'où la question.*
- **Q46 (G15.8) — SoftFloat.** `iXtal26.SoftFloat/` (59 types, 75 méthodes en `snake_case`) a le pacte du transcrit
  (`.editorconfig:78-79`).
  *Proposé : l'inclure, en dernière famille, avec ses propres portes (son banc `tools/softfloat/Banc`, le fuzzeur
  x87).*
- **Q47 (G15.1) — Le code mort.** `PLAN.md` dit « nettoyage (code mort, …) ». Une seule fonction d'iXtal26 est morte de
  son fait (et `BootDiff.PasCsharp` dans Diff) ; 127 le sont parce que PCem les a mortes aussi ou que leur appelant
  n'est pas transcrit.
  *Proposé : n'ôter que le code mort propre à iXtal ; le code mort transcrit reste (R6, le miroir du C), consigné dans
  VERIFICATION.md § G15.1 avec sa raison ; une méthode jamais appelée n'est jamais compilée par le JIT. C'est réduire
  « code mort » à presque rien, d'où la question.*
- **Q48 (G15.1) — Les fermetures et les allocations qui existent déjà.** `PLAN.md` dit « ce qui coûte, on ne le fait
  pas » ; il ne dit pas ce que devient ce que le code fait déjà : les 43 fermetures (des DEVIATION de R4 déjà dites),
  les 8 `Func<…>` de `Cpu/` et les 13 `new uint16_t[4]` de `x86seg.cs`.
  *Proposé : les admettre en base de référence, nommées une à une dans VERIFICATION.md, et ne rien y changer en G15 :
  les retirer change le code machine des chemins les plus chauds et des signatures (`do_seg_load`, `taskswitch286`) ;
  c'est un levier de performance, pas une convention. Le banc d'ouverture chiffre leur coût (cas 4 et 6) ; ce qu'on en
  fait ensuite est hors de G15.*
  *Rouverte le 05/11 : G15.P. Les fermetures d'OP_ARITH sont ôtées ; les autres restent en base, comme proposé.*
- **Q49 (G15.11) — Les outils à installer.** L'annexe de `PLAN.md` en nomme cinq, chacun sur ton accord.
  *Proposé : aucun. Le SDK suffit à toutes les portes de G15 (les analyseurs CA et IDE, Roslyn, `dotnet format`,
  `DOTNET_JitDisasm`, `MemoryDiagnoser`) ; le « signalé » se fait par le `grep` et l'IL (décision n° 7).
  BannedApiAnalyzers (NuGet) n'ajouterait que LINQ, dont les chemins chauds n'ont aucun appel ;
  ClrHeapAllocationAnalyzer ignore les boucles ; `jb inspectcode`, `dotnet-counters` et `dotnet-trace` n'apportent
  rien qu'une porte demande. Si tu veux un analyseur dans la construction, BannedApiAnalyzers, pour LINQ, borné à la
  liste des chemins chauds, est le plus petit pas ; un analyseur écrit ici, sur le Roslyn du SDK, en serait un autre,
  sans paquet.*
- **Q50 (G15.0a-G15.0c, G15.9, G15.11) — Le banc à chaque commit.** `PLAN.md:592-593` veut la mesure « à chaque commit
  de G14 et de G15 ». Cinq étapes ne changent pas le code de l'émulateur, ou n'en changent que des noms de locales :
  G15.0a, 0b, 0c, G15.9 et G15.11.
  *Proposé : la passe du banc n'y est pas jouée quand l'IL d'iXtal26 est identique à celui de son parent (0/0/0 sans
  table, et la DLL au sha256 quand elle peut l'être) : le code machine est alors celui du parent (décision n° 5, le
  précédent de G14.5). Toute étape qui change l'IL a sa passe.*

## Les risques

- **La durée.** Huit étapes portent une série entière dans les deux modes et une passe du banc (G15.1, G15.3 à G15.8,
  G15.10), de 4 h à 4 h 40 de machine chacune : de 32 à 37 h, plus G15.0a (1 à 2 h) et les sous-ensembles.
  Rien ne tourne à côté.
- **Un changement de sens sans erreur de compilation** : un renommage qui masque, ou qui fait choisir une autre
  surcharge par deux `using static`. La vérification de liaison le refuse, l'IL le montre.
- **Les couplages par chaîne** : onze sites doivent suivre, dont `"opFCLEX"` et `"ILLEGAL"`, qui ne casseraient que le
  mode « tout » ; ~500 ne doivent pas suivre. La liste close les sépare ; les séries dans les deux modes le jugent.
- **Les noms générés** : 1 264 champs `<>O`, 200 méthodes, la numérotation des lambdas et des fermetures ; un vidage
  qui ne les normaliserait pas sortirait des centaines d'écarts sans cause.
- **Les générateurs** : huit fichiers de `Cpu/` ; un renommage qui ne passe pas par eux serait défait à la prochaine
  génération. G15.8 les régénère et compare à l'octet.
- **Les enums** : une valeur réordonnée casse la parité avec le C par `h_set_*` ; un enum imprimé change la sortie
  d'une série. Les séries le jugent.
- **L'hôte qui dérive** sur plus de trente heures de machine : le réancrage de § G14.1, le gel du lancé, le refus d'une
  passe où le runtime change.
- **Le banc moins fin que la règle** sur les cas bruyants : c'est dit cas par cas, et le 0 % y repose sur les listings.
- **Le délai de 1 200 s de `Banc.cs:166-169`** : une horloge de l'hôte dans une porte déterministe ; il a frappé trois
  fois en G14, chaque fois rejoué seul et dit.

## Annexe — Hors de la portée

Trouvé par les lectures, non proposé (règle du 18/10) :
- deux `record` dans des fichiers soumis à R4 (`Ide/ide.cs:145`, `Sound/sound_sb.Materiel.cs:378`) ;
- des commentaires périmés d'`iXtal26.csproj` (`:52-64`, `:71`, un `InternalsVisibleTo` vers `iXtal26.Sst`, qui
  n'existe pas) ;
- `ImplicitUsings`, qui importe aussi `System.Net.Http` et `System.Threading.Tasks` ;
- quatre délégués jamais instanciés, le `BigInteger` d'`arith_seize`, les `ConditionalWeakTable` du mode matériel ;
- des allocations compilées dans `pmodeint` et `svga_poll` (R2, annexe A), l'égalité de délégués de `svga_poll` ;
- sous « tout », `attend` enveloppe peut-être toutes les instructions x87, et pas seulement WAIT (à vérifier) ;
- la parade `DOTNET_ROOT` contre une mise à jour du runtime (G14-reconnaissance/R0-contre.md:248-251), et l'épinglage
  du runtime par apt, une action sur la machine de Julien, que G15 ne fait pas ;
- le délai de 1 200 s de `Banc.cs:166-169`, déjà dit en G14 ;
- trouvé par G15.0a : le `readmemb` statique n'est pas inliné, dans le banc ni dans le produit, sa copie d'instance
  l'est (−20,7 % sur ce chemin) ; une interpolation gardée par un champ coûte par sa présence, le prologue mettant ses
  locales à zéro (+14,75 % sur `svga_write`, à peine au-dessus du seuil élargi de +13,47 %) ;
- trouvé par G15.P :
  - `ildump comparer` ne normalise pas la numérotation des fermetures et des lambdas ;
  - les précodes et les stubs du runtime n'ont pas de témoin ;
  - la comparaison de listings d'`interdits.py` ne connaît que deux écarts admis, les décalages et le registre de
    l'argument.
