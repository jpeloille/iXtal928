# R1 — La contre-lecture

> Contre-lecture du 4 novembre 2026, au commit `34814ec`, de `R1-interdits.md`. LECTURE SEULE : rien n'a été
> construit, lancé ni mesuré ; seuls `git grep`, `grep`, `sed`, `awk` et des scripts `python3 -I` jetables (hors du
> dépôt) ont tourné, sur le code, sur `il-c0c.txt` (G14.0c), sur les listings du JIT de R sous 10.0.14
> (`/var/tmp/ixtal-g14/ref-R-captures-10.0.14/jit/`) et de G14.4 (`g14-4/jit-C/`), et sur le SDK 10.0.114.
> Classement : **ERREUR** (le rapport se trompe, ou se contredit), **IMPRÉCISION** (le fond tient, le chiffre ou le
> renvoi est à corriger), **REMARQUE** (un manque ou un risque), **CONFIRMÉ** (recompté, égal). Niveaux : vérifié
> (relu), déduit, inconnu. Les listes de fichiers de la frontière : `HOT.txt` jetable, construit comme le § 2, I0.

## 0. En bref

1. Les faits de R1 sur le dépôt tiennent : 156 fichiers et 82 917 lignes, 43 lambdas et leur répartition par
   fichier, 13 `new uint16_t[4]` aux lignes citées, aucun `params`, 128 classes `sealed` et 240 `static`, l'IL
   (5 120 méthodes), 27 gestionnaires exécutés et 88 méthodes de fermeture aux listings de R comme à ceux de G14.4,
   les lignes 74 et 83 du listing de `OP_ARITH`, le contenu du SDK 10.0.114. Tout recompté, égal.
2. Trois erreurs de méthode du banc : (a) le garde-fou « la colonne réel ne s'écarte de la retenue que dans la bande
   de l'A/A » contredit les cas 3, 4 et 6, où le vrai code n'est pas la forme retenue, par construction ; (b) la
   durée « 20 à 40 min » suppose des itérations fixées que la commande donnée ne fixe pas (la même erreur que le
   § 1.14 de `G14-reconnaissance/R0-contre.md`) : 27 à 63 min par passage au job par défaut, calculé ; (c) le
   critère de D5 repose sur l'intervalle de BenchmarkDotNet, que R0-contre a déjà montré trompeur entre processus.
3. Le cas 8 mesure une garde qui n'existe pas à l'exécution : `svga_output` est une `const` à 0
   (`Video/vid_svga.cs:223`), les `pclog($"…")` gardés par elle sont du code mort. Le cas 3 en « interface sur une
   classe `sealed` » est dévirtualisé par le JIT avant toute mesure, si le site d'appel n'est pas polymorphe.
4. La thèse « les délégués des tables de PCem sont la forme retenue » est exacte pour les types `delegate` nommés
   (R4 `:60`, `:126`). Elle ne couvre pas les 43 fermetures ni les `Func<…>` : la liste de R4 est close, et deux
   DEVIATION écrites (`386_ops_arith.cs:33-36`, `386_ops_jump.cs:30-32`) les consignent. D2 doit le dire.
5. Chiffres à corriger, sans effet sur les conclusions : 18 lignes (non 15) au grep `interface|virtual|…` ; 7 lignes
   (non 0) à celui des classes ni `static`, `sealed` ni `abstract` (des commentaires) ; 1 appel LINQ (non 3) dans les
   méthodes d'initialisation des espaces chauds ; 764 (non 756) `new` ; 54 (non 101) lignes à la regex LINQ rendue
   par `…` : la commande n'est pas reproductible telle qu'écrite ; « 4 box » (§ 0) contre 5 (§ 1.5).
6. D1 et D2 sont ouvertes et leurs propositions tiennent. D3 propose plus que PLAN.md ne demande (un outil Roslyn
   à base de référence par membre, huit contrôles négatifs) et passe sous silence deux voies plus petites : le
   vidage d'IL déjà là (`tools/ildump`), et un analyseur local sans NuGet. D4 tient. D5 est à refaire.
7. Bilan : 3 ERREURS, 8 IMPRÉCISIONS, 15 REMARQUES, 25 CONFIRMÉS (voir « Bilan chiffré »). Corrections : fin du texte.

## 1. La frontière des chemins chauds

### 1.1 Le compte — CONFIRMÉ

```
{ ls iXtal26/Cpu/*.cs | grep -v '\.State\.cs'; git ls-files 'iXtal26.SoftFloat/*.cs'
  printf '%s\n' iXtal26/Memory/mem.cs iXtal26/io.cs iXtal26/timer.cs
  ls iXtal26/Ics/Intel8253*.cs iXtal26/Ics/Emu8000*.cs iXtal26/Ics/SierraSC1502x*.cs iXtal26/Ics/TridentTKD8001.cs
  ls iXtal26/Video/*.cs iXtal26/Sound/*.cs; printf '%s\n' iXtal26/Lpt/lpt_dac.cs iXtal26/Lpt/lpt_dss.cs; } > HOT.txt
wc -l < HOT.txt            -> 156
xargs cat < HOT.txt | wc -l -> 82917
```

Détail : `Cpu/*.cs` 55 fichiers dont 2 `*.State.cs` (53 retenus), SoftFloat 53 (`git ls-files`), 3 racines, 7 d'`Ics/`,
`Video/` et `Sound/` sans sous-répertoire (`find -mindepth 1 -type d` : rien), 2 de `Lpt/`. Égal, au fichier et à la
ligne près (vérifié).

### 1.2 Fidèle à PLAN.md:575-576 ? — CONFIRMÉ, avec des manques

Les six familles de PLAN.md (cœurs UC et x87, mémoire, aiguillage des opcodes, minuteries, rendu vidéo, mixage du
son) sont toutes représentées ; rien de la liste n'est oublié. Aucun fichier de la liste n'est étranger à une
famille, mais la maille est celle du fichier : `Cpu/cpu_tables.cs` (53 lignes `new` hors `static`/`readonly`,
des tables), `Sound/sound_sb.cs` (87), des `device_config_t` dans `Video/` sont de l'initialisation. R1 le dit
(§ 1.1, « le grain fin se règle par la base »). Les trois fichiers à la frontière sont bien posés.

**REMARQUE 1 — `ppi.cs` et `rtc.cs` ne sont classés nulle part.** La liste « hors des chemins chauds » (R1 § 1.1)
ne les nomme pas, et la liste d'I0 non plus. `ppi.cs` (34 lignes, `wc -l`) porte le port 61h, la porte du PIT 2 et le
haut-parleur ; `rtc.cs` (357 lignes) est le calendrier, froid. Il faut les écrire dans l'une ou l'autre liste, pour
que « chaque fichier d'`iXtal26/` est classé » soit une porte vraie.

**REMARQUE 2 — la glob `iXtal26.SoftFloat/**/*.cs` d'I0 ramasse `obj/` et `bin/`.** `find iXtal26.SoftFloat -name
'*.cs'` rend, hors `obj` et `bin`, 53 fichiers, comme `git ls-files` ; `obj/Release/net10.0/` et `obj/Debug/` portent
chacun un `iXtal26.SoftFloat.GlobalUsings.g.cs` (vérifié). Une glob lue sur le disque dit 156 aujourd'hui seulement
si l'outil lit `git ls-files` ou exclut `obj` et `bin` : à écrire dans I0.

**REMARQUE 3 — `.editorconfig:40` ne couvre pas la liste.** Sa section est
`[**/{Cpu,Memory,Models,Keyboard,Video,PluginApi,Diag,Devices,Sound,Flash,Floppy,Disc,Mouse,Ics}/**.cs]` : ni `io.cs`
ni `timer.cs` (racine), ni `Lpt/`. R1 le relève en annexe pour `Lpt`, mais pas pour les deux fichiers racine de
sa propre liste ; « recopié dans une section de `.editorconfig` » demande donc des sections par fichier.

**REMARQUE 4 — PLAN.md:585-586 dit « dans les dossiers des chemins chauds ».** R1 propose une liste de fichiers,
parce que le dossier ne tient plus (le PIT est dans `Ics/` depuis G14). La raison est bonne ; c'est pourtant un écart
au texte de Julien, et D1 doit le nommer comme tel (§ 4).

**REMARQUE 5 — l'IL de G14.0c ne couvre pas SoftFloat.** `il-c0c.txt` ne dumpe que `iXtal26.dll` (les 1 104 lignes qui
nomment `iXtal26.SoftFloat` sont des appels). Pour la frontière « SoftFloat dedans », les affirmations « aucun LINQ,
aucune interface, aucun `box` » tiennent sur le texte : `git ls-files '*.cs' | xargs grep -nE
'\bnew\b|\binterface\b|\bvirtual\b|\bparams\b|\$"|\bdelegate\b'` dans `iXtal26.SoftFloat` ne rend aucune ligne de
code (les `=>` y sont des corps d'expression). À dire dans R1 ; le vidage d'IL de G14.5 couvre SoftFloat (90 entrées,
`VERIFICATION.md:19726`).

## 2. Les constats chiffrés, un par un

### 2.1 LINQ

- CONFIRMÉ : `ImplicitUsings` (`iXtal26.csproj:12`) engendre `global using System.Linq;`
  (`obj/Release/net10.0/iXtal26.GlobalUsings.g.cs:5`, aussi `System.Net.Http`, `System.Threading.Tasks`).
- CONFIRMÉ : aucun appel LINQ dans un chemin chaud. Preuve indépendante, sur l'IL :
  `grep 'System.Linq.Enumerable::'` par méthode de `il-c0c.txt` rend 14 méthodes, toutes dans `BootTest`, `Diag.R9`,
  `Host.*`, `ModeMateriel`, `TimerCheck`, et `iXtal26.Video.video+Faisceau::.cctor` (le seul de la liste chaude :
  `video.Materiel.cs:59`, `Enumerable.Range(0, 512).Select(…).ToArray()`, un initialiseur statique).
- **IMPRÉCISION 1 — « 3 dans l'initialisation » (R1 § 1.2).** Dans les espaces cités (`Cpu`, `Memory`, `Video`,
  `Sound`, `Models`, `Devices`, `timer`, `io`, `pc`), le script rend **1** méthode (`video+Faisceau::.cctor`).
  `ModeMateriel` en compte 3 (`get_Description`, `get_ListeDemandee`, `get_Tout`, + `Lire`), mais n'est pas dans
  cette liste. Dire 1, ou nommer les trois.
- **IMPRÉCISION 2 — « 101 lignes » (R1 § 1.2) n'est pas reproductible.** La commande donnée contient `…` dans
  l'alternance. Avec `Select|Where|Any|All|First|FirstOrDefault|Last|ToArray|ToList|Sum|Count|OrderBy|Max|Min|
  Average|Skip|Take|Contains|Distinct|Concat|Zip|Aggregate|Cast|OfType|Reverse|SequenceEqual|ToDictionary|GroupBy|
  Single|ElementAt|Append|Prepend|Except|Union|Intersect|ToHashSet|Order`, hors `Host/` et `obj/` : 54 lignes. Le
  fond (presque toutes `Math.Min`/`Max`, `string.Contains`, `StringBuilder.Append`) tient ; écrire la regex entière.
- REMARQUE 6 : `ModeMateriel.Tout` (`Materiel/ModeMateriel.cs:298`) est une propriété à corps LINQ, évaluée à chaque
  lecture. Ses appelants (`Description`, `Demander…`) sont tous du titre de fenêtre, du lanceur ou du `--setup-check`
  (`grep -rn 'ModeMateriel.Description'`) : froid, mais c'est le seul LINQ du dépôt qui se relit par propriété.

### 2.2 Interfaces et `virtual`

- CONFIRMÉ : `grep -rnE '\bclass\s+\w+' iXtal26 | grep -cE '\bsealed\b'` rend 128, `static` 240.
- CONFIRMÉ : un seul `abstract` (`Cdrom/cdrom_image.cs:83`, `TrackFile`, trois `override` : `:114`, `:195`, `:208`) ;
  `IDisposable` seulement (`SdlAudio.cs:22`, `SdlHost.cs:18`, `cdrom_image.cs:55, 83, 871`).
- **IMPRÉCISION 3 — « 0 ligne » au grep des classes ni `static`, `sealed` ni `abstract`.** La commande rend 7
  lignes : `Scsi/scsi_cd.cs:1434, 1441` et `Cpu/cpu.cs:26, 30, 33, 39, 47` (le mot « class » dans des commentaires).
  Aucune déclaration ; la conclusion est juste.
- **IMPRÉCISION 4 — « 15 lignes, toutes fausses ».** Le grep sur la liste chaude, hors lignes qui commencent par
  `//`, rend **18** : les 15 de R1 (8 dans des `/* */`, 7 sur `svga.@override`) et 3 commentaires de plus
  (`Cpu/cpu.cs:592`, `Video/vid_et4000.cs:120`, `Video/vid_tvga.cs:131`, « override mask », « Waitstates override »).
  Toutes fausses, comme dit.
- CONFIRMÉ : les `callvirt` des espaces chauds hors `Invoke` : 38 cibles distinctes, dont `Stream::*`, `TextWriter::*`,
  `StringBuilder::Append` et les méthodes d'instance de `DBOPL+Operator`/`Channel` (classes `sealed`). Aucune cible
  d'interface de collection sur le chemin d'un `foreach`. À noter, hors liste de R1 : `EqualityComparer<Int32>::Equals`
  (8) vient de `longueur_materiel` et d'`Equals` de `record struct` (`sound_sb+Selection`, `ide+IDE_HDD_EMU`), pas
  d'une interface au sens de PLAN.md.
- CONFIRMÉ : `foreach` seulement sur tableaux ou `ReadOnlySpan` (relu : `Emu8000.cs:2276`, `sound_sb.cs:1430`,
  `vid_svga.cs:2483` sont des `ReadOnlySpan<uint8_t>`).

### 2.3 Fermetures

- CONFIRMÉ : `grep -rnE --include=*.cs 'fetchdat\s*=>' iXtal26/Cpu | wc -l` = 43 ; par fichier : `386_ops_arith` 13,
  `386_ops_stack` 8, `386_ops_jump` 6, `386_ops_inc_dec` 4, `386_ops_rep` 3, `386_ops_prefix` 2, `386_ops_xchg` 2,
  `x87.Materiel` 2, un chacun pour `bit`, `shift`, `mov_seg`. Aucune hors `Cpu/`.
- CONFIRMÉ : l'IL les voit tous comme capturants : 43 méthodes `<>c__DisplayClass…b__n(UInt32) : Int32`, dont 41 dans
  `iXtal26.Cpu._386` (25 classes de capture) et 2 dans `_x87_materiel` ; aucune dans `Memory`, `Video`, `Sound`, `Ics`,
  `timer`, `io`. Les autres méthodes de capture des espaces chauds sont des fonctions locales de `x86seg_c`
  (`DPL`…), `_386_materiel` (`octet`, `borne`) et `sound_emu8k` (`lit`) : par référence, sans `newobj`.
- **Créées par appel ou en boucle ?** CONFIRMÉ : jamais par appel du chemin d'exécution. Les `newobj
  <>c__DisplayClass` sont dans les seules fabriques (`OP_ARITH`, `OP_ARITH_386`, `Loop`, `PushSeg`, `PopSeg`, `OpBTx`,
  `OpSHxD`, `Op0F`, `OpLsel`, `Poser*`), toutes appelées au remplissage des tables ; les « délégués dans une boucle »
  sont les boucles de pose (`386_ops_jump.cs:79-87`, `:109-118`, `386_ops_inc_dec.cs`, `386_ops_stack.cs`,
  `386_ops_xchg.cs`), jouées une fois. Le mode matériel (`x87.Materiel.cs:58-85`, `partout`/`sur`) enveloppe au gel.
- CONFIRMÉ, aux listings : 27 gestionnaires distincts compilés dans les 30 scénarios de R
  (`grep -h '^; Assembly listing for method iXtal26.Cpu._386+<>c__DisplayClass.*(uint):int:this' *.txt | sort -u |
  wc -l`), 27 dans G14.4 ; `grep -h '^; Assembly listing' *.txt | sort -u | grep -cE '<>c|>g__|b__'` : 88 dans R et
  dans G14.4. Par fabrique : `OP_ARITH` 6, `OP_ARITH_386` 2, `PoserIncDecRegistres` 2, `PoserPile386` 2,
  `PoserPushPopRegistres` 2, un pour chacune des onze autres.
- CONFIRMÉ : `<>c__DisplayClass70_0:<OP_ARITH>b__0` de `ami486-dx2.txt` : `call [rax+0x18]…ArithOp8:Invoke` à la
  ligne 74 de la méthode, `…ArithFlags8:Invoke` à la ligne 83 (awk sur le listing).
- **IMPRÉCISION 5 — « 43 gestionnaires du cœur 286/386/486 » (R1 § 0).** 41 sont du cœur ; les 2 autres sont
  `x87.Materiel.cs:146` (`efface_b`) et `:218` (`attend`), du mode matériel. Les « 27 exécutés » comptent les 41
  seuls (motif `_386+`). Écrire « 41 + 2 ».
- REMARQUE 7 : `attend` (`x87.Materiel.cs:218`) enveloppe, d'après `partout`/PB-204 (`:50-100`, « par position »),
  les gestionnaires x87 de toutes les tables sous « tout », pas seulement WAIT ; R1 dit « à chaque FCLEX et à chaque
  instruction d'attente ». Sous le mode par défaut de l'utilisateur, chaque instruction x87 paie donc une fermeture
  et un délégué de plus (déduit du texte ; à vérifier au listing d'un scénario x87 en « tout »).
- REMARQUE 8 — le périmètre de « fermeture » : R1 ne compte que les `fetchdat =>`. Les lambdas non capturantes
  (201 autres formes `=>` dans `Cpu/` par `grep -rnE`, `() => ES`, `op8`, `setflags8`, `cond`) sont des
  méthodes de `<>c`, sans allocation ; elles ne sont pas des fermetures au sens de PLAN.md (« lambdas qui capturent »).
  C'est bien posé dans R1 ; à dire en une phrase pour qu'un lecteur ne recompte pas 244.

### 2.4 Allocations

- CONFIRMÉ : `new uint16_t[4]` : 13 dans `Cpu/x86seg.cs`, aux lignes 80, 378-379, 790-791, 1086-1088, 1498-1500,
  1904, 2371 (`grep -n 'new uint16_t\[4\]'`) ; 1 de plus dans `386.Materiel.cs:546` (R1 la range au mode matériel).
- CONFIRMÉ : les listings de R portent 1, 2, 2, 3, 2, 1, 1 `NEWARR` pour `loadseg`, `loadcscall`, `pmoderetf`,
  `pmodeint`, `pmodeiret`, `taskswitch286`, `loadcsjmp` (script `python3 -I` sur les 30 fichiers `.txt`).
- REMARQUE 9 — « la troisième de `pmodeiret` n'y paraît pas : non élucidé ». Explication probable, déduite :
  `segs` (`x86seg.cs:1500`) n'est que indexé (`:1606-1620`), jamais passé à une méthode ; il ne s'échappe pas, et le
  JIT de .NET 10 alloue sur la pile un petit tableau qui ne s'échappe pas. Les deux autres tableaux de la méthode
  (`segdat`, `segdat2`) vont à `do_seg_load(x86seg, ushort[])` et s'échappent. Conséquences : (i) il y a au plus 12
  allocations sur le tas par exécution, non 13 ; (ii) la « forme retenue » du cas 6 (`Span` de `stackalloc`) n'est pas
  un remplacement local : `taskswitch286(ushort, ushort[], int)` et `do_seg_load` reçoivent des `ushort[]` ; la
  correction de l'audit P8 change des signatures. À écrire dans le cas 6 et dans D2.
- **IMPRÉCISION 6 — « 756 lignes de `new` ».** `xargs grep -nE '\bnew\b' < HOT.txt | sed -E 's#//.*##' | grep -cE
  '\bnew\b'` rend 764 (791 sans ôter les commentaires). Le fond tient (tables et `*_init`).
- CONFIRMÉ : 36 interpolations `$"` dans la liste chaude (`xargs grep -c '\$"'`, somme) ; aucun `params`
  (`grep -nE '\bparams\b'`, hors commentaires : rien).
- **IMPRÉCISION 7 — « 4 `box` » (§ 0) contre « 5 `box` » (§ 1.5).** `grep -c ': box ' il-c0c.txt` : 5 ; par méthode :
  `<>z__ReadOnlyArray…get_Item` (compilateur), `ide_init`, `ams1512_init`, `headland_init`, `machine_named_entries…
  get_Current`. « 4 à l'initialisation » est vrai si l'on ôte celle du compilateur ; la `get_Current` n'est pas une
  initialisation mais une énumération de configuration, froide. Écrire 5 partout, et dire lesquelles.
- CONFIRMÉ : `svga_poll`, 5 `NEWSFAST` aux lignes 369, 398, 427, 465, 503 du listing, le premier derrière
  `test rdi, rdi / jne` (lignes 366-367) : le cache de délégué. `vid_ega.Materiel.cs`, `point_materiel`, idem
  (`ldsfld …<>O::<6>__rendu_faisceau / brtrue`).
- REMARQUE 10 — **`svga_output` est une constante.** `Video/vid_svga.cs:223` : `private const int svga_output = 0;`
  (et `:15-16`, le pragma CS0162 qui l'avoue). Les `if (svga_output != 0) pclog($"…")` de `:1100-1101`, `:1137-1138`
  sont supprimés à la compilation : ils ne sont pas « gardés », ils n'existent pas à l'exécution. R1 § 1.5 les range
  parmi les interpolations « gardées » ; le cas 8 (§ 2.3 ci-dessous) ne mesure donc rien du produit.

### 2.5 `params`, instances, délégués

- CONFIRMÉ : conteneurs statiques, `cpu_state` (`Cpu/386_common.cs:23`), `pic_`/`pic2`/`pit_`, DBOPL 41 méthodes
  d'instance (regex sur `Sound/dbopl.cs`, 42 à R1 : à un près selon la regex), `dbopl.cs:12-17` pour les
  pointeurs de membre remplacés.
- CONFIRMÉ : les chiffres de `statique-instance-et-ecart-oracle.md` (§ 2, `:46-48`, +3 %, 4 à 5 % ; § 4, `:103`,
  922 locales) et de l'audit (§ 3, `:181-182`, « 2,05 ns contre 1,73 ns » ; P2 `:53`, P8 `:138`, P10 `:159`).

## 3. Les délégués : la thèse de TRANSCRIPTION.md:60 et :126

CONFIRMÉ pour ce qu'elle dit : `TRANSCRIPTION.md:60` liste `delegate` parmi « les sept C#-ismes autorisés, liste
close » (R4, `:57`), et `:126` rend les pointeurs de fonction par « types `delegate` nommés, tableaux dimensionnés
comme en C ». Les sept délégués cités (`timer_callback_t` `timer.cs:14`, `mem_read_b_fn` `Memory/mem.cs:27-32`,
`inb_fn` `io.cs:22-28`, `device_init_fn` `PluginApi/device.cs:29-41`, `OpFn` `Cpu/386.cs:36`, `sound_get_buffer_t`
`Sound/sound.cs:22`, `svga_render_fn` `Video/vid_svga.cs:39-42`) existent aux lignes dites.

**REMARQUE 11 — ce que R4 ne dit pas, et que R1 n'oppose pas à sa thèse.**
- R4 (`:57-58`) interdit déjà « `interface`, `abstract class`, générique, LINQ, `record`, `async`, DI, méthode
  d'extension » : les constructions de PLAN.md sont, pour trois d'entre elles, déjà fermées par la règle de
  transcription. Les écarts réels sont donc des entorses à R4 : `Cdrom/cdrom_image.cs:83` (`abstract`, hors liste),
  et surtout les fermetures, qui ne figurent pas dans la liste des sept.
- Les `Func<…>` ne sont ni des « types `delegate` nommés » ni autorisés : `386_ops_bit.cs:22, 77`,
  `386_ops_shift.cs:595, 610`, `386_ops_stack.cs:296, 582`, `x87.Materiel.cs:58, 78` (`grep -rnE '\b(Func|Action)<'
  iXtal26/Cpu`) : 8 sites dans les fichiers chauds, génériques, qui capturent via les fabriques.
- Les fermetures sont des écarts consignés : `386_ops_arith.cs:33-36` (« DEVIATION: `operation` et `setflags` … des
  délégués ») et `386_ops_jump.cs:30-32` (« la condition devient un délégué rendu par ConditionDe() »), et l'audit P2
  propose leur retrait « fidèle au C ».
Conséquence pour D2 : admettre les 43 fermetures « en base de référence » revient à reconduire une DEVIATION déjà
écrite, pas à constater une pratique ; la proposition tient, mais doit le dire. La thèse de R1 reste juste :
les délégués nommés des tables sont la forme retenue, et PLAN.md:578-579 ne les range pas dans la liste.

## 4. Le banc de démonstration (I1)

### 4.1 Ce qui tient — CONFIRMÉ

`InternalsVisibleTo` (`iXtal26.csproj:73`) ; `Program.cs:29-31` (job `DOTNET_TieredCompilation=0`, `MemoryDiagnoser`) ;
`GardeBanc.cs:91-112` (équivalence sur les 65 536 couples, `typeof(_808x).GetMethod(…)` à `:93`) ;
`MemoireBanc.cs:61-70` (`Reference.Monter`) ; `compare-ref.sh:48` (53 à 56 min), `:76-109` (les groupes, GardeBanc et
Is486Banc « hors de la référence ») ; `Is486Banc.cs:8` (« 100 fois par seconde »). `ptrace_scope` = 1
(`/proc/sys/kernel/yama/ptrace_scope`) ; `iced`, `gee.external.capstone`, `microsoft.diagnostics.runtime` dans
`~/.nuget/packages` (vérifié). Les neuf cas couvrent toutes les constructions de PLAN.md:575-579 et les quatre
exemples de PLAN.md:580-584 (cas 1, 2, 3, 6).

### 4.2 Chaque cas mesure-t-il « la construction interdite contre sa forme retenue, sur un vrai chemin » ?

**ERREUR 1 — le garde-fou d'I1 contredit trois de ses cas.** « La colonne “réel” ne s'écarte de la retenue que dans
la bande de l'A/A ; sinon le cas ne vaut rien et se reprend » (R1 § 2, I1, « Portes et preuves »). Or R1 dit, juste
avant, que dans les cas 4 et 6 « le code d'iXtal **est** la forme interdite » (réel = interdite, retenue = copie
corrigée), et au cas 3 que le vrai est « une fermeture qui appelle `op8` et `setflags8` » (ni l'expression en
ligne, ni l'interface). Pour ces trois cas, l'écart réel/retenue est le résultat attendu ; la règle le déclarerait
nul. La porte doit se dire par cas : cas 1, 2, 5, 7, 8, 9 (le vrai est la forme retenue) : réel ≈ retenue ; cas 3, 4,
6 : réel ≈ la colonne qu'il incarne (délégué, fermeture, allocation), dans la bande de l'A/A de cette colonne.

**ERREUR 3 — la durée.** La commande de R1 est `… --filter '*Interdit*' --launchCount 3` : ni
`--warmupCount`, ni `--iterationCount`, ni `--invocationCount`. Le « 13 s par cas et par passage » vient de
`compare-ref.sh:48`, qui les fixe (`compare-ref.sh:281-283`, `--warmupCount "$echauffements" --iterationCount
"$iterations"`). Au job par défaut, `G14-reconnaissance/R0-contre.md:168-176` a relevé 15 à 35 s par lancement
(`Run time` des journaux) : 36 colonnes × 3 lancements × (15 à 35 s) = 27 à 63 min par passage (calculé), 54 à
126 min pour deux, hors construction. Fixer les itérations dans la commande, ou corriger le chiffre. Le cas où
toutes les colonnes d'un cas partagent une classe construit aussi, par classe, un projet généré qui recompile
`iXtal26` (R0-contre § 1.14, « une construction par passage ») : à compter.

**ERREUR 2 — D5 reprend le critère que R0-contre a réfuté.** « L'intervalle du rapport interdit/retenue
exclut 1 et l'écart dépasse celui de l'A/A » : BenchmarkDotNet lance chaque méthode dans son propre processus
(`--launchCount 3`) ; l'intervalle de confiance suppose des itérations indépendantes, que le bruit entre processus
(2,5 à 5 %, `R0-contre.md:20-24, :264`) dément. Les colonnes « réel », « retenue », « interdite » et « A/A » sont des
processus différents : seul l'écart à l'A/A, mesuré entre ses propres lancements, porte le bruit ; l'intervalle
d'un rapport, non. Pour les cas attendus gros (LINQ, `params`, boxing, allocation : des multiples), aucun
risque ; pour le cas 1 (la mesure du 26/09 : 0 % pour la SVGA, +3 % pour `cpu_state` non `readonly`) c'est précisément
l'ordre de grandeur du bruit. D5 doit fixer un seuil sur les moyennes de lancement, comme R0-contre l'a proposé
pour G14, ou admettre que les cas à moins de 5 % se concluent « non mesurable ».

Cas par cas :

1. **`readmemb` statique contre instance** (`Cpu/386_common.cs:163-169`, vérifié) — faisable, c'est l'exemple de
   PLAN.md. Le « réel » appelle un `internal` : direct. Les trois variantes d'instance sont des copies ; le code de
   `Memory/mem.cs` (`readmembl`, `:569`) n'est pas dans la copie. Résultat attendu dans le bruit (cf. D5).
2. **`for` contre LINQ** sur `speaker_get_buffer` — le `for` est à `Sound/sound_speaker.cs:70-71` (R1 : `:69-70`,
   une ligne plus haut ; `private`, donc réflexion + délégué, comme dit). LINQ y alloue ; l'écart sera gros et sans
   ambiguïté. Valable, mais c'est le cas le moins instructif : aucune équivalence à discuter.
3. **Appel direct contre interface ou `virtual`** — **REMARQUE 12 — le site d'appel doit être polymorphe.** « Une
   interface `IAluOp` sur une classe `sealed` » est dévirtualisée statiquement par le JIT si le type est connu au
   site (R1 § 3 le dit en risque, sans en tirer la consigne). Pour mesurer ce que la table des opcodes subirait, la
   copie doit appeler un élément d'un tableau d'au moins trois implémentations (comme `ops_286[i]`), sans
   `readonly`/`sealed` utile au JIT. R1 le renvoie au verdict (D5, « le JIT dévirtualise ») : c'est bâtir un cas
   que l'on sait perdu. De plus la « retenue » est « l'expression en ligne » : comparer un appel à une expression
   mélange le coût de l'indirection et celui de l'inlining ; PLAN.md dit « appel direct », qui est un appel de
   méthode statique.
4. **Fermeture contre méthode nommée** (`386_ops_jump.cs:79-87`) — bien posé ; voir l'ERREUR 1 pour sa porte.
5. **Délégué créé dans une boucle** — l'« affectation de rendu » de `svga_recalctimings` est un groupe de méthodes
   statique mis en cache : la forme interdite (« une lambda qui capture `svga` ») n'existe pas dans le produit. Ce
   n'est pas « un vrai chemin » mais un chemin du produit avec une construction ajoutée. Acceptable, à dire.
6. **Sans allocation contre avec** (`loadseg`, `Cpu/x86seg.cs:80`) — le « réel » exige un mode protégé monté : un GDT
   et un descripteur lisibles, ce que `Reference.Monter` ne pose pas pour l'instant (`MemoireBanc.cs:64-70` monte
   une boucle en `0000:0100`) ; coût d'écriture non nul, à compter dans l'estimation. Voir la REMARQUE 9 pour la
   forme retenue.
7. **Boxing** — `outb` (`io.cs:198`) reçoit `object priv` ; la forme interdite (un entier en boîte) n'est pas dans
   le produit, et seule une copie la porte. Même observation qu'au cas 5.
8. **`string`** — **le cas ne mesure rien du produit.** `svga_output` est une `const` (REMARQUE 10) : la colonne
   « réel » est une méthode sans `pclog`, la « retenue » aussi ; seule la colonne interdite (l'interpolation sans
   garde) coûte. Un vrai chemin chaud gardé par un champ non constant existe-t-il ? Sans lui, le cas se réduit à un
   micro-banc d'interpolation. À remplacer, ou à dire.
9. **`params`** (`prefetch_run`, `Cpu/386.cs:245`, huit `int`, `private static`) — faisable et net.

**REMARQUE 13 — l'invocation des copies.** `GardeBanc`/`Is486Banc` appellent leurs copies « par le même délégué »
(`GardeBanc.cs:8-16`). Si I1 fait de même, une indirection commune à toutes les colonnes noie le cas 3 (appel
direct contre interface). La boucle de mesure doit appeler chaque copie directement (`OperationsPerInvoke` dans une
boucle du banc), ce que R1 n'écrit pas. `OperationsPerInvoke` n'est d'ailleurs employé que par `Is486Banc.cs:44-67`
(R1 : « comme les bancs de G13 »).

**REMARQUE 14 — le mode.** Les gestionnaires `attend`/`efface_b` et les `ConditionalWeakTable` sont du mode matériel,
celui de l'utilisateur depuis G13.11. R1 ne dit pas dans quel mode les cas « réels » tournent (« aucun » comme
l'oracle, ou « tout » comme le produit). Pour les cas 3 et 4, c'est la différence entre un ADD nu et un ADD derrière
`attend`.

**Ce que vaut une copie** (R1 § 1.7) : exact, et suffisant s'il se tient aux ordres de grandeur. Placement 2 à 3 %
(`R0-contre.md:264`). Un cas dont l'écart attendu est inférieur à 5 % ne se conclut pas par une copie.

**Faisabilité de la partie « sans installation »** : CONFIRMÉ. BenchmarkDotNet 0.15.8 et ses dépendances de
désassemblage sont dans le cache ; `DOTNET_JitDisasm` est intégré ; le projet `iXtal26.PerfBanc` accepte de nouvelles
classes sans toucher `iXtal26` ; `compare-ref.sh` ne joue que `GROUPES_REF` (`:79-109`).

## 5. Les analyseurs

### 5.1 Le contenu du SDK 10.0.114 — CONFIRMÉ

`/usr/lib/dotnet/sdk/10.0.114/Sdks/Microsoft.NET.Sdk/analyzers/` : `Microsoft.CodeAnalysis.NetAnalyzers.dll`,
`…CSharp.NetAnalyzers.dll`, `…VisualBasic.NetAnalyzers.dll`. Identifiants cherchés en UTF-8 et UTF-16 : CA1859, CA1822,
CA1852, CA1851, CA1826, CA1827, CA1829, CA1860, CA1861, CA1825, CA2014 présents (331 identifiants `CA` au total) ;
`codestyle/cs/Microsoft.CodeAnalysis.CSharp.CodeStyle.dll` : `IDE0320`, `IDE0062`, et les classes
`MakeAnonymousFunctionStatic…`, `MakeLocalFunctionStatic…`. Roslyn du SDK : `/usr/lib/dotnet/sdk/10.0.114/Roslyn/
bincore/Microsoft.CodeAnalysis.dll` et `…CSharp.dll` (`tools/renommage/iXtal26.Renommage.csproj` les lie par
`HintPath`, sans paquet). `ls ~/.nuget/packages | grep -iE 'banned|heap|perfsens|meziantou|roslynator'` : rien ; sont
présents `microsoft.codeanalysis.common`, `.csharp` (4.14.0), `.analyzers` (3.11.0), `benchmarkdotnet` (0.15.8).
`/usr/lib/dotnet/library-packs` : deux paquets (`ILCompiler`, `ILLink.Tasks`), aucun analyseur. Ce qui demande NuGet :
BannedApiAnalyzers et ClrHeapAllocationAnalyzer, comme dit.

REMARQUE 15 — **ce que R1 écarte sans l'examiner.**
- Le vidage d'IL de `tools/ildump`, déjà dans la porte de G14, est un « relevé d'occurrences » gratuit : la table
  des noms de G15 (comme `tools/noms/g14.txt`) l'aligne sur R, et « 0 changée, 0 disparue, 0 apparue » dit déjà qu'il
  n'y a aucune fermeture, aucun `box`, aucun appel LINQ, aucun `callvirt` neufs (les constructions de PLAN.md sont
  toutes visibles à l'IL, sauf `string` et `virtual` déclaré mais non appelé). Il couvre les relevés 1 et 3 de R1
  pour une fraction du coût, sans outil neuf. R1 ne l'emploie que dans l'étape I1.
- Un analyseur **local**, sans NuGet : une bibliothèque compilée contre le Roslyn du SDK (comme `renommage`),
  référencée par `<Analyzer Include="…">` et bornée par `.editorconfig`, qui relève en construction. C'est « un
  analyseur » au sens littéral de PLAN.md:585, aux dossiers (ou fichiers) voulus. Le piège de
  `TreatWarningsAsErrors` (`iXtal26.csproj:38`) vaut ; la voie est à peser, non à écarter par silence.

CONFIRMÉ : `CS8820` (lambda `static` qui capture) est bien l'erreur du compilateur, déduit comme dit.

## 6. Les décisions ouvertes D1 à D5

- **D1 — la frontière.** Ouverte : PLAN.md nomme des familles, pas des fichiers. La proposition tient (`io.cs` dedans,
  SoftFloat dedans, `pc.cs` dehors : `runpc` 100 Hz, `pc.cs:1494-1530` ne contient que des appels, vérifié). À ajouter :
  `ppi.cs`, `rtc.cs` (REMARQUE 1), et l'écart au texte « dossiers » de PLAN.md:585-586 (REMARQUE 4). Le PIC et le DMA
  dehors sont défendables ; « la porte des 0 % les traverse » est déduit, et l'est bien.
- **D2 — les 43 fermetures et les allocations existantes.** Ouverte (PLAN.md ne dit pas ce que devient l'existant), et
  la proposition tient (admettre en base, ne rien changer, chiffrer au banc). À compléter : ce sont des DEVIATION
  consignées de R4 (§ 3), les `Func<…>` en font partie, et la correction de P8 touche des signatures (REMARQUE 9) :
  « retirer » est donc un chantier plus gros que R1 ne le laisse voir, ce qui renforce le renvoi hors de G15.
- **D3 — l'outil.** Ouverte sur le choix de l'outil (les paquets demandent l'accord de Julien : annexe de PLAN.md),
  mais la proposition élargit G15. PLAN.md:585-587 : « si possible, un analyseur ou une règle `.editorconfig` […] ;
  sinon, une revue par `grep` ». Un outil Roslyn à analyse de flot, six familles de règles, un TSV par occurrence
  keyé par (fichier, membre, règle, rang), huit contrôles négatifs et une base de référence maintenue à chaque
  renommage de G15, c'est l'étape la plus lourde du bloc, non requise par le texte. Proposer d'abord le grep et l'IL
  (REMARQUE 15), qui répondent à « sinon grep » avec une preuve meilleure que celle de G14, puis seulement,
  si Julien le veut, l'outil. Les négatifs par règle d'I2 gardent leur valeur pour le grep et l'IL.
- **D4 — la cadence du banc.** Ouverte, proposition tenable. Réserve : « à chaque commit de G14 et de G15, la
  mesure contre R » est la lettre de PLAN.md ; l'exemption d'un commit d'outillage se fonde sur le précédent
  de Q39 (`5643228`), qui portait sur la porte du JIT, non sur le banc : la dire comme un choix, avec l'IL égal
  à R pour preuve, comme G14.5 l'a fait pour les commentaires.
- **D5 — le verdict.** À refaire (ERREUR 2). La règle de
  fond de D5 (écrire « pas de coût mesurable » quand c'est le cas,
  la règle de Julien restant) est bonne ; son critère chiffré ne l'est pas.

Rien dans D1 à D5 n'est déjà tranché par PLAN.md ; D3 est la seule qui sorte de son texte.

## 7. Ce que le rapport omet

- Les `Func<…>` et le caractère « hors liste » des fermetures dans R4 (§ 3).
- La constance de `svga_output` (cas 8, § 1.5 de R1).
- Le mode (« aucun » ou « tout ») des cas du banc ; l'invocation directe des copies (REMARQUES 13, 14).
- Le polymorphisme du site d'appel pour l'interface (REMARQUE 12).
- Le coût de mise en place d'un mode protégé pour le cas 6, et le changement de signatures qu'il suppose.
- L'IL de SoftFloat n'est pas dans `il-c0c.txt` (REMARQUE 5).
- `ppi.cs`, `rtc.cs` non classés ; les sections de `.editorconfig` absentes pour les fichiers racine (REMARQUES 1, 3).
- La voie « IL comme relevé » et l'analyseur local (REMARQUE 15).
- Les éléments de l'annexe de PLAN.md hors analyseurs (ReSharper CLT, dotnet-counters, dotnet-trace) ne sont pas
  traités ; ils relèvent peut-être d'une autre lecture, mais le lecteur de R1 ne le sait pas.
- Le runtime est épinglé à 10.0.14 (`ref-R-captures-10.0.14`) mais le banc de démonstration n'est pas dans la
  référence ; une mise à jour d'apt entre son ouverture et sa clôture changerait ses chiffres absolus (R1 § 3 le dit).

## Bilan chiffré

| Classe | Nombre | Détail |
|---|---|---|
| ERREUR | 3 | 1 garde-fou d'I1 ; 2 critère de D5 ; 3 durée du banc (R0-contre § 1.14 déjà jugé) |
| IMPRÉCISION | 8 | 1 LINQ « 3 » (1) ; 2 regex « 101 » (54) ; 3 « 0 ligne » (7) ; 4 « 15 » (18) ; 5 « 43 du cœur » |
|  |  | (41 + 2) ; 6 « 756 » (764) ; 7 « 4 box » (5) ; 8 `sound_speaker.cs:69-70` (le `for` est à `:70-71`) |
| REMARQUE | 15 | numérotées 1 à 15 (frontière 1 à 5, LINQ 6, x87 7, fermetures 8, pile 9, `const` 10, |
|  |  | R4 11, banc 12 à 14, analyseurs 15) |
| CONFIRMÉ | 25 | 156 fichiers ; 82 917 lignes ; 128 `sealed` ; 240 `static` ; `abstract` unique ; `IDisposable` |
|  |  | seul ; 43 lambdas et leur répartition ; 43 méthodes de capture à l'IL ; fabriques seules ; |
|  |  | 27 gestionnaires (R et G14.4) ; 88 méthodes de fermeture ; lignes 74 et 83 ; 13 `new uint16_t[4]` ; |
|  |  | 1-2-2-3-2-1-1 `NEWARR` ; 36 `$"` ; 0 `params` ; 5 `box` ; cache de `svga_poll` ; ImplicitUsings ; |
|  |  | `foreach` ; délégués aux lignes dites ; R4 `:60`, `:126` ; audit et note statique/instance ; |
|  |  | ancres du banc ; SDK 10.0.114 ; absence de NuGet ; Roslyn du SDK |

## Corrections à porter au plan

1. **I1, la porte par cas** : réel ≈ retenue pour les cas 1, 2, 5, 7, 8, 9 ; réel ≈ la colonne qu'il incarne pour les
   cas 3, 4, 6. Retirer la phrase générale.
2. **I1, la commande** : fixer `--warmupCount`, `--iterationCount`, `--invocationCount` comme `compare-ref.sh:281-283`,
   ou chiffrer 27 à 63 min par passage ; compter la construction par classe.
3. **I1, cas 3** : site d'appel polymorphe (tableau d'au moins trois implémentations), « appel direct » = appel de
   méthode statique, copies appelées directement et non par un délégué commun.
4. **I1, cas 6** : dire que la forme retenue change des signatures (`do_seg_load`, `taskswitch286`) et que `segs` de
   `pmodeiret` est probablement alloué sur la pile par le JIT (12 allocations au tas, non 13) ; compter la mise en
   place d'un mode protégé.
5. **I1, cas 8** : `svga_output` est constante ; choisir un vrai chemin gardé par un champ, ou dire que le cas est
   un micro-banc d'interpolation.
6. **I1, le mode** des cas « réel » : écrire « aucun » ou « tout » par cas.
7. **D5** : un seuil sur les moyennes de lancement, pas sur l'intervalle de BenchmarkDotNet ; « non mesurable »
   sous 5 %.
8. **D2** : nommer les 43 fermetures comme DEVIATION de R4 (`386_ops_arith.cs:33-36`, `386_ops_jump.cs:30-32`), y
   ajouter les 8 sites `Func<…>`.
9. **D3** : proposer d'abord le grep et l'IL (`tools/ildump` + table de G15), puis l'outil Roslyn seulement sur le
   mot de Julien ; mentionner l'analyseur local sans NuGet.
10. **I0** : classer `ppi.cs` et `rtc.cs` ; exclure `obj` et `bin` de la glob SoftFloat (ou lire `git ls-files`) ;
    prévoir des sections `.editorconfig` pour `io.cs` et `timer.cs`.
11. **D1** : nommer l'écart au texte « dossiers » de PLAN.md:585-586.
12. **Chiffres de R1** : § 0 « 43 gestionnaires du cœur » → « 41 + 2 du mode matériel » ; « 4 box » → 5 ; « 3 » LINQ à
    l'init → 1 (`video+Faisceau::.cctor`) ; « 15 » lignes → 18 ; « 0 ligne » → 7 (des commentaires) ; « 756 » → 764 ;
    « 101 » → regex complète (54) ; `sound_speaker.cs:69-70` → `:70-71`.
13. **R1 § 1.2** : dire que l'IL de G14.0c ne couvre pas SoftFloat (texte relu, aucun interdit).
14. **R1 § 1.4** : sous « tout », `attend` enveloppe toutes les instructions x87, pas seulement WAIT (à vérifier).
