# R1 — Les constructions interdites et leur démonstration

Lecture de reconnaissance de G15, au commit `34814ec` (4 novembre 2026). Lecture seule : rien n'a été construit,
lancé ni installé. Les sources : `PLAN.md` § G15 (`:565-597`) et l'annexe des outils (`:643-660` dans la copie de
travail, `:630-647` à HEAD), `TRANSCRIPTION.md` (R4, `:57-63` ; la table des conventions, `:126`), le code
d'`iXtal26/` et d'`iXtal26.SoftFloat/`, le banc `tools/perfbanc/`, et ce que G14 a laissé sous `/var/tmp/ixtal-g14/` :
le vidage d'IL de G14.0c (`g14-0c/il-c0c.txt`, 5 120 méthodes) et les listings du JIT de R sous 10.0.14
(`ref-R-captures-10.0.14/jit/`, 30 scénarios) et de G14.4 (`g14-4/jit-C/`, 30 scénarios).

Niveaux : **vérifié** (relu dans le texte, l'IL ou un listing), **déduit** (tiré de ce qui est relu, sans mesure),
**inconnu**.

## 0. En bref

- Frontière : une liste de fichiers par glob (156 fichiers, 82 917 lignes) ; ni le répertoire ni un attribut (§ 1.1).
- LINQ : aucun appel sur un chemin chaud, mais `ImplicitUsings` importe `System.Linq` partout (§ 1.2).
- Interfaces et `virtual` : aucune ; les délégués des tables de PCem sont la forme retenue (R4), pas un interdit.
- Fermetures : l'écart réel, 43 gestionnaires du cœur 286/386/486, 27 exécutés dans les captures de R ; l'ADD y fait
  deux appels indirects de plus par instruction, vu au listing (§ 1.4).
- Allocations : 13 `new uint16_t[4]` du mode protégé (`x86seg.cs`) ; boxing : 4 `box`, à l'initialisation (§ 1.5).
- Le banc : 9 cas, quatre colonnes (vrai, retenue, interdite, A/A), hors des 65 cas, 20 à 40 min, rien à installer.
- Le signalé : un outil sur le Roslyn du SDK (comme `tools/renommage`), le grep, les listings du JIT ; pas de NuGet.
- Trois étapes (I0 la frontière, I1 le banc, I2 le signalement) ; cinq décisions ouvertes (§ 4).

## 1. L'état, point par point

### 1.1 La carte des chemins chauds

PLAN.md:575-576 nomme « cœurs UC et x87, mémoire, aiguillage des opcodes, minuteries, rendu vidéo, mixage du son ».
Leur place dans le code (vérifié sur le texte ; les volumes par `wc -l`) :

- **Cœurs UC** : `Cpu/*.cs` sauf `808x.State.cs` et `386.State.cs` (les portes du harnais) ; `execx86`
  (`808x.cs`, le `switch` de `:1298`), `exec386` (`386.cs`).
- **x87** : `Cpu/x87*.cs` et `iXtal26.SoftFloat/**`, que le noyau de 80 bits du mode matériel appelle.
- **Mémoire** : `Memory/mem.cs`, `readmembl` (`:569`) et les mappages ; `readmemb` est dans
  `Cpu/386_common.cs:163` et `Cpu/808x.cs:92`.
- **Aiguillage des opcodes** : `Cpu/386.cs` (`Dispatch(x86_opcodes![…])`, `:387`), `386_ops_table386.cs`,
  `386_ops_prefix.cs` (`Dispatch`, `:63`), `808x.cs`.
- **Minuteries** : `timer.cs` (`timer_process`, `:207`), `Ics/Intel8253*.cs` (le PIT).
- **Rendu vidéo** : `Video/**` (`svga_poll`, `vid_svga.cs:731`, les `*_poll`), `Ics/SierraSC1502x*.cs`,
  `Ics/TridentTKD8001.cs` (les RAMDAC).
- **Mixage du son** : `Sound/**`, `Ics/Emu8000*.cs`, `Lpt/lpt_dac.cs`, `Lpt/lpt_dss.cs` ; `sound_poll`
  (`sound.cs:130`) appelle chaque `get_buffer` (`:152`), ceux du DAC et du DSS compris (`lpt_dac.cs:126`,
  `lpt_dss.cs:140`).

Deux fichiers sont à la frontière : `io.cs`, l'aiguillage des ports (`inb`, `outb`, `io.cs:177`, `:198`), que
chaque `IN`/`OUT` traverse, pendant des mappages de la mémoire ; et `pc.cs`, dont `runpc` (`:1494`) ne tourne
que 100 fois par seconde (`tools/perfbanc/Is486Banc.cs:8`) et dont le reste est la configuration. Je propose
`io.cs` dedans, `pc.cs` dehors (§ 4, D1).

**Hors des chemins chauds** (vérifié par la lecture de ce qu'ils contiennent) : `Host/**` (SDL, menus, ligne de
commande), `Program.cs`, `BootTest.cs`, `TimerCheck.cs`, `pc.cs`, `PluginApi/**` (configuration, `device_t`),
`Materiel/**` (le gel du mode ; les champs `materiel.pb_nn` y sont seulement déclarés), `Diag/**` (compteurs et
portes du harnais), `Memory/mem_bios.cs` (lecture des ROM), `Models/**`, `Keyboard/**`, `Mouse/**`, `Joystick/**`,
`Floppy/**`, `Disc/**`, `Cdrom/**`, `Scsi/**`, `Ide/**`, `Mfm/**`, `Flash/**`, `Devices/**`, `Lpt/lpt.cs`, les
autres puces d'`Ics/` (8042, 8237, 8259A, MC146818, 8250, les trois jeux de puces), et `tools/**`.

Le PIC (`Ics/Intel8259A.cs`) et le DMA (`Ics/Intel8237.cs`) sont appelés à chaque IRQ et à chaque octet de DMA du
son ; PLAN.md ne les nomme pas. Je les laisse dehors (D1) : la porte des 0 % les traverse de toute façon (les
cas Son et Machine de `compare-ref.sh:97-106` montent des cartes à DMA et des IRQ : déduit).

**La frontière applicable.** Trois formes possibles, aucune parfaite :

- *par répertoire* : trop large. `Cpu/808x.State.cs` est une porte du harnais (`GetState` alloue deux tableaux par
  appel, `:507`, `:524`), `Memory/` contient `mem_bios.cs`, `Ics/` mêle le PIT (chaud) et le NEAT (froid), `Lpt/`
  mêle le DAC (le son) et le port ; les fichiers chauds eux-mêmes portent leur initialisation et leurs tables de
  configuration (les `device_config_t` de `vid_cga.cs:719-730`, `sound_sb.cs:1638-1754`) ;
- *par attribut* (un `[CheminChaud]` par méthode) : précis, mais ajoute des lignes au code transcrit (les renvois
  `fichier.cs:N` et le compte de R2 bougent) ;
- *par fichier* (une liste de globs) : ce que `.editorconfig` sait déjà faire (`.editorconfig:40`,
  `[**/{Cpu,…,Ics}/**.cs]`), ce qu'un outil et un grep lisent pareil. Le grain fin (l'initialisation dans un
  fichier chaud) se règle par la base de référence (§ 2, I2), pas par la frontière.

Je propose la liste de fichiers, écrite une fois dans un fichier lu par l'outil, le grep et `.editorconfig`
(§ 4, D1). Compte de la liste proposée : 156 fichiers, 82 917 lignes
(`HOT="$(ls iXtal26/Cpu/*.cs | grep -v '\.State\.cs') … iXtal26/Lpt/lpt_dss.cs"; echo "$HOT" | wc -w`,
`cat $HOT | wc -l` ; la liste complète en tête du § 2, I0).

Remarque (vérifié) : la Portée de `TRANSCRIPTION.md:98-101` ne liste ni `Sound/` ni `Devices/` (déjà relevé,
`VERIFICATION.md:19723`) ; la frontière des chemins chauds ne peut pas s'appuyer sur elle.

### 1.2 LINQ

- **`using System.Linq` n'apparaît nulle part**, et pourtant LINQ est importé dans chaque fichier :
  `iXtal26.csproj:12` (`<ImplicitUsings>enable</ImplicitUsings>`) engendre `global using System.Linq;`
  (`obj/Release/net10.0/iXtal26.GlobalUsings.g.cs:5`). Vérifié. Une règle « pas de `using System.Linq` » ne
  relèverait rien ; il faut regarder les appels.
- Appels hors de `Host/` (`grep -rnE --include=*.cs '\.(Select|Where|Any|…|Average)\(' iXtal26 | grep -v '^obj/'`,
  101 lignes, presque toutes des `Math.Min`/`Math.Max` ou `string.Contains`, relues une à une). Les vrais :
  - `Video/video.Materiel.cs:59` : `Enumerable.Range(0, 512).Select(…).ToArray()`, un initialiseur `static
    readonly`, joué une fois. Seul cas dans les fichiers chauds. Pas chaud (vérifié : initialiseur de champ).
  - `Diag/R9.cs:25` (le résumé de R9, à la sortie), `BootTest.cs:32, 204, 229`, `TimerCheck.cs:210, 235-240,
    426-427`, `Materiel/ModeMateriel.cs:289-409` (le gel du mode, au démarrage), `pc.cs:260` (`Array.FindIndex`,
    la configuration) : hors des chemins chauds.
- L'IL le confirme : aucun appel à `System.Linq.Enumerable::` dans une méthode des espaces `Cpu`, `Memory`,
  `Video`, `Sound`, `Models`, `Devices`, `timer`, `io`, `pc` hors de l'initialisation ; 3 dans l'initialisation
  (script jetable sur `il-c0c.txt`, motif `System\.Linq\.Enumerable::`, méthodes classées par leur nom). Vérifié
  sur l'IL de G14.0c ; l'IL de HEAD lui est égal par la table (`g14-5/il/iXtal26-avec.txt` : « 0 changées,
  0 disparues, 0 apparues » sur 5 738 entrées, contre R) — déduit pour G14.0c contre R, même nombre de méthodes.

### 1.3 Interfaces et méthodes virtuelles

- **Déclarations** : dans tout `iXtal26/`, 128 classes `sealed`, 240 `static`, et une seule classe ni l'une ni
  l'autre : `Cdrom/cdrom_image.cs:83`, `internal abstract class TrackFile : IDisposable`, avec trois `override`
  (`:114`, `:195`, `:208`) — le CD, hors des chemins chauds (`grep -rnE --include=*.cs '\bclass\s+[A-Za-z_0-9]+'
  iXtal26 | grep -vE '\b(static|sealed|abstract)\b'` : 0 ligne ; `… | grep -cE '\bsealed\b'` : 128 ; `static` :
  240). L'IL le confirme : aucun type marqué `Interface` ; les seuls `Abstract` non `Sealed` sont `TrackFile`
  (en-têtes `== type` de `il-c0c.txt`). Vérifié.
- **Interfaces implémentées** : `IDisposable` seulement (`Host/SdlAudio.cs:22`, `Host/SdlHost.cs:18`,
  `Cdrom/cdrom_image.cs:55, 83, 871`). Hors des chemins chauds.
- **Appels virtuels** dans les espaces chauds (les `callvirt` de l'IL qui ne sont pas des `Invoke` de délégué) :
  `TextWriter::WriteLine` et `Stream::*` (journaux, fichiers), `StringBuilder::Append` (`*_add_status_info`), et les
  méthodes d'instance de DBOPL (`DBOPL+Operator::GetSample`…). Ces dernières sont des `callvirt` sur une classe
  `sealed` : le JIT en fait un appel direct (déduit : C# émet `callvirt` pour toute méthode d'instance). Pas
  d'appel par interface sur un chemin chaud (vérifié dans l'IL : `awk` sur `il-c0c.txt`, cible de chaque
  `callvirt`).
- Le grep sur les fichiers chauds (`interface|virtual|abstract|override`, hors commentaires de ligne) rend 15
  lignes, **toutes fausses** : 8 dans des commentaires `/* … */` (`sound_sb.cs:1255-1446`, `vid_s3.cs:3223, 3226`)
  et 7 sur le champ de PCem `svga.@override` (`vid_svga.cs:190-931`). Vérifié. C'est la limite du grep.

**Les délégués des tables de PCem** (`timer_callback_t`, `timer.cs:14` ; `mem_read_b_fn`…, `Memory/mem.cs:27-32` ;
`inb_fn`…, `io.cs:22-28` ; `device_init_fn`…, `PluginApi/device.cs:29-41` ; `OpFn`, `Cpu/386.cs:36` ;
`sound_get_buffer_t`, `Sound/sound.cs:22` ; `svga_render_fn`, `Video/vid_svga.cs:39-42`) : **la forme retenue**,
pas une construction interdite. R4 les autorise nommément (`TRANSCRIPTION.md:60`, « `delegate` », premier des sept
C#-ismes) et la table des conventions en fait le rendu des pointeurs de fonction (`:126`) ; PLAN.md:578-579 ne les
range pas dans la liste. Un appel par délégué n'est pas inliné et passe par un thunk pour une méthode statique
(audit : « environ 0,3 ns par instruction au micro-banc (2,05 ns contre 1,73 ns) »,
`Docs/audit-performance-2026-09-26.md`, § 3). Le banc peut le chiffrer à titre d'information (§ 2, I1, cas 3) ;
ce n'est pas une règle à signaler.

### 1.4 Fermetures et délégués créés dans une boucle

C'est la seule construction interdite qui tourne réellement à chaque instruction.

- **Le code** : 43 lambdas `fetchdat => { … }` dans 11 fichiers de `Cpu/` (`grep -rnE --include=*.cs
  'fetchdat\s*=>' iXtal26/Cpu | wc -l`) : `386_ops_arith.cs` 13, `386_ops_stack.cs` 8, `386_ops_jump.cs` 6,
  `386_ops_inc_dec.cs` 4, `386_ops_rep.cs` 3, `386_ops_prefix.cs` 2, `386_ops_xchg.cs` 2, `x87.Materiel.cs` 2,
  `386_ops_bit.cs`, `386_ops_shift.cs`, `386_ops_mov_seg.cs` 1 chacun. Elles capturent les paramètres de leur
  fabrique : `OP_ARITH` (`386_ops_arith.cs:96`, ses délégués `op8`, `setflags8`, le booléen `gettempc`),
  `PushSeg(Func<uint16_t> seg)` (`386_ops_stack.cs:296`, appelé avec `() => ES`…, `:511-514`, `:558-576`),
  `OpBTx(Func<…> operation, …)` (`386_ops_bit.cs:22`), `OpSHxD` (`386_ops_shift.cs:595`), `Loop(CondFn? extra)`
  (`386_ops_jump.cs:195`, `:370-371`), `PrefixeSegment`, `Op0F`, `PrefixeRep`, `PrefixeSegmentRep`,
  `PrefixeTaille`, `OpLsel`, `PopSeg`. **Délégués créés dans une boucle** : les Jcc, `for (var n = 0; n < 16; n++)
  { var cond = conditions[n]; ops_286[0x70 + n] = fetchdat => { … if (cond()) … } }`
  (`386_ops_jump.cs:79-87`, et `:109-118` pour la forme longue), INC/DEC et PUSH/POP par registre
  (`386_ops_inc_dec.cs:45, 55`, `386_ops_stack.cs:52, 61`), XCHG (`386_ops_xchg.cs:93`). Vérifié.
- **Pas d'allocation par instruction** : les fermetures sont construites une fois, au remplissage de la table
  (`386_ops_arith.cs:38-39`, « LES DÉLÉGUÉS SONT CONSTRUITS UNE FOIS »). Vérifié dans l'IL : les `newobj` des
  classes `<>c__DisplayClass` sont dans les fabriques (`OP_ARITH`, `Loop`, `PushSeg`…), appelées par les `Poser*`.
- **Le coût est l'appel** : chaque ADD/SUB/AND… fait trois appels indirects (le gestionnaire, `op8`, `setflags8`),
  chaque Jcc un `cond()` (audit P2, `audit-performance-2026-09-26.md`, § 2). Vérifié au listing de R :
  `<>c__DisplayClass70_0:<OP_ARITH>b__0` dans `ami486-dx2.txt` porte `call [rax+0x18]…ArithOp8:Invoke` et
  `call [rax+0x18]…ArithFlags8:Invoke` (lignes 74 et 83 du listing de la méthode, `awk` sur le fichier).
- **Combien ont tourné** : l'IL de G14.0c a 25 classes de fermeture dans `_386` et 41 corps de gestionnaire
  (`grep '^== méthode iXtal26.Cpu._386+<>c__DisplayClass' il-c0c.txt | sed … | sort -u | wc -l` : 25 ; `grep -c
  '…DisplayClass.*b__[0-9]*(UInt32) : Int32'` : 41). Dans les 30 captures de R, 27 gestionnaires distincts ont été
  compilés, donc exécutés au moins une fois (`TieredCompilation=false` compile à la première exécution ;
  `grep -h '^; Assembly listing for method iXtal26.Cpu._386+<>c__DisplayClass.*(uint):int:this' *.txt | sort -u |
  wc -l`). Même compte dans les captures de G14.4 (27 ; 88 méthodes de fermeture ou de lambda en tout, dans R et
  dans G14.4). Vérifié pour « exécuté » ; « chaud » est déduit (ADD, Jcc, PUSH/POP sont parmi les plus fréquents).
- **Fermetures de fonctions locales, sans tas** : `x86seg.cs` (`DPL`, `DPL2`… dans `loadcscall`, `pmodeint`,
  `pmodeiret`, `pmoderetf`, `taskswitch286`, `loadcsjmp`), `386.Materiel.cs` (`longueur_materiel`, ses `octet` et
  `borne`), `Emu8000.Materiel.cs` (`chorus_materiel`, sa `lit`) : leur classe de capture passe par référence
  (`<>c__DisplayClass7_0&` dans l'IL), une structure sur la pile. Pas d'allocation (vérifié dans l'IL : aucun
  `newobj` de ces classes), mais un appel non inliné possible (inconnu).
- **Le mode matériel** : `x87.Materiel.cs:146` (`efface_b`) et `:218` (`attend`) enveloppent un gestionnaire x87
  par une fermeture, sous PB-59 et le verrou du coprocesseur (vérifié). Joué à chaque FCLEX et à chaque instruction
  d'attente sous « tout » (déduit).
- Hors des chemins chauds : `Mfm/mfm_at.Materiel.cs:58-64` et `Video/vid_ega.Materiel.cs:290` (des lambdas passées
  à `io_sethandler`, à l'initialisation ; le second est dans un fichier chaud mais à l'initialisation),
  `Scsi/scsi_cd.Materiel.cs:236`, `PluginApi/device.cs:327`, `pc.cs:260`.

### 1.5 Allocations dans les boucles

- **Le comptage** : `new` sur les fichiers chauds, 756 lignes de code (grep, commentaires de ligne ôtés). Presque
  tout est froid : les tableaux statiques, les `device_config_t` (plus de 100 lignes dans `Video/` et `Sound/`), les
  `*_init`. Le tri a été fait sur l'IL (script jetable sur `il-c0c.txt` : `newobj` d'un type référence, `newarr`,
  par méthode, les méthodes d'initialisation écartées par leur nom), puis sur les listings du JIT (les aides
  `CORINFO_HELP_NEWARR_1_VC`, `NEWSFAST`… par méthode compilée dans les 30 captures).
- **Ce qui alloue à chaque appel sur un chemin chaud** (vérifié au listing de R) : `x86seg.cs`, 13 `new
  uint16_t[4]` (`:80`, `:378-379`, `:790-791`, `:1086-1088`, `:1498-1500`, `:1904`, `:2371`) dans `loadseg`,
  `loadcscall`, `pmoderetf`, `pmodeint`, `pmodeiret`, `taskswitch286`, `loadcsjmp` ; les listings portent 1, 2, 2, 3,
  2, 1, 1 appels à `NEWARR_1_VC` (la troisième de `pmodeiret` n'y paraît pas : non élucidé). Seulement en mode
  protégé (`:73`, la garde de `loadseg`), donc sous Windows 3.x et au POST des AT ; c'est l'audit P8 (32 octets par
  tableau, déduit).
- Le mode matériel : `386.Materiel.cs:546` (`porte_tache_materiel`, une porte de tâche : rare) ;
  `x87_noyau.Materiel.cs:602` (`BigInteger`, dans `arith_seize`, `:619-666`, sous PB-59, seulement sur un non normal
  ou un dénormal : rare).
- **Les délégués mis en cache ne sont pas des allocations par appel** : `svga_poll` porte 5 `NEWSFAST` au listing
  (`ami486-gd5429.txt`), chacun derrière un test du champ de cache (`test rdi, rdi` / `jne`, lignes 365-369 du
  listing de la méthode) : la conversion d'un groupe de méthodes statique, allouée une fois (C# 11 et plus). De même
  `svga_recalctimings`, `sb_dsp_setaddr`, les `poll_materiel`. Un compteur naïf des `NEWSFAST` les compterait
  (vérifié sur le listing).
- **Chaînes** : 36 interpolations `$"` dans les fichiers chauds. Celles des chemins chauds sont gardées
  (`vid_svga.cs:1100-1101`, `:1137-1138` : `if (svga_output != 0) pclog($"…")`) ou dans des branches d'erreur
  (`mem.cs:1164`, `386_common.cs` dans `x86_int`, `x86seg.cs:556, 566`) : aucune n'est construite sur le chemin
  ordinaire (vérifié sur le texte).
- **`params`** : 0 dans les fichiers chauds (grep `\bparams\b`). Vérifié.
- **Boxing** : 5 instructions `box` dans tout l'IL (`grep -c ': box ' il-c0c.txt`) : une du compilateur
  (`<>z__ReadOnlyArray`1`), une dans `config.machine_named_entries` (froid), trois dans `ide_init`,
  `ams1512_init`, `headland_init` (l'initialisation). Aucune sur un chemin chaud. Les `object p` des gestionnaires
  ne reçoivent que des classes ; les interpolations passent par `DefaultInterpolatedStringHandler`, sans boxing.
  Vérifié.
- `foreach` : seulement sur des tableaux ou des `ReadOnlySpan` (`grep -nE '\bforeach\b'` sur les fichiers chauds) ;
  aucun énumérateur d'interface. Vérifié.

### 1.6 Instances au lieu de statiques

- Les conteneurs sont des classes statiques (`Cpu/` 56, `Video/` 31, `Ics/` 22, `Sound/` 18, `Memory/` 2 ;
  `grep -rhE '^\s*(internal|public|private)?\s*static\s+(partial\s+)?class\s'` par répertoire). L'état chaud est
  statique, ou un objet tenu par un `static readonly` : `cpu_state` (`Cpu/386_common.cs:23`), `pic_`, `pic2`
  (`Ics/Intel8259A.cs:44`), `pit_` (`Ics/Intel8253.cs:96`). Vérifié.
- Les objets reçus en `object p` (`svga_t`, `cga_t`, `PIT`…) sont les structures de PCem passées par `void *` :
  la transcription de PCem, pas un passage en instance (`TRANSCRIPTION.md:127`).
- Méthodes d'instance dans les fichiers chauds : DBOPL, 42 (`Sound/dbopl.cs`, transcription du C++ de DOSBox,
  classes `sealed`, ses pointeurs de membre déjà remplacés par des `switch`, `dbopl.cs:12-17`) ; et hors des
  chemins chauds, `National8250.cs:53` (`Clear`), `808x.State.cs:219, 239` (le harnais). Vérifié.
- Ce qui a été mesuré le 26/09 (`Docs/statique-instance-et-ecart-oracle.md`, § 2) : la SVGA par un `static
  readonly` contre `(svga_t)p`, aucun écart ; `cpu_state` sans `readonly`, +3 % au total, le cœur 4 à 5 %. Un objet
  reçu en paramètre ou par `this` n'a pas été mesuré (même note, § 2, dernier point).

### 1.7 Le banc existant, et ce que vaut une copie

- `tools/perfbanc/` : BenchmarkDotNet 0.15.8 (`iXtal26.PerfBanc.csproj`), un job mutateur qui pose
  `DOTNET_TieredCompilation=0` et `MemoryDiagnoser` sur tous les cas (`Program.cs:29-31`) ; l'accès aux internes
  par `InternalsVisibleTo` (`iXtal26.csproj:73`) ; le comparateur `compare-ref.sh` (R contre C, ABBA, 65 cas,
  53 à 56 min, `compare-ref.sh:48`) ne joue que ses groupes nommés (`:79-109`) : un cas neuf n'y entre pas.
- `GardeBanc` et `Is486Banc` mesurent des copies écrites dans le banc, appelées par un délégué ; l'équivalence est
  vérifiée avant la mesure (`GardeBanc.cs:91-112`, sur les 65 536 couples ; `Is486Banc.cs`, un appel de chaque forme
  depuis le même état). R0-contre de G14 l'a dit : « Aucun code d'iXtal26 que G14 renomme n'est exécuté en cours de
  mesure » (`Docs/G14-reconnaissance/R0-contre.md:62-68`), et l'écart de placement entre copies est de 2 à 3 %
  (`R0-contre.md:264`).
- **Ce que vaut une copie** (déduit) : le même JIT, les mêmes réglages, l'inlining entre assemblages permis ; mais un
  autre placement du code, et un autre ordre d'initialisation des classes (`GardeBanc.cs:13-14` : le même
  `static readonly` coûte ou ne coûte pas selon que sa classe est initialisée avant la compilation). Une copie mesure
  la construction, pas le chiffre d'iXtal. D'où la colonne « le vrai code » du § 2.
- **Ce que BenchmarkDotNet mesure** : la moyenne et son intervalle de confiance à 99,9 % (`Error`), l'écart type, le
  rapport à la ligne de base (`Ratio`) ; avec `MemoryDiagnoser`, les octets alloués par opération et les collectes
  par 1 000 opérations (déjà actif). `DisassemblyDiagnoser` rend le code machine des méthodes mesurées et de leurs
  appelés : ses dépendances (`iced`, `microsoft.diagnostics.runtime`, `gee.external.capstone`) sont déjà dans
  `~/.nuget/packages` (vérifié), rien à installer ; qu'il marche ici (Linux, `ptrace_scope` = 1, vérifié) est
  inconnu. Le repli est connu : `DOTNET_JitDisasm` et `DOTNET_JitStdOutFile` posés sur le job, comme le fait déjà
  `Program.cs:30` pour le palier (`PLAN.md`, l'annexe : « intégré à .NET, rien à installer »).

### 1.8 Les analyseurs disponibles

Vérifié dans le SDK 10.0.114 (`/usr/lib/dotnet/sdk/10.0.114/Sdks/Microsoft.NET.Sdk/`, les identifiants cherchés en
UTF-8 et UTF-16 dans les DLL par un script jetable) :

- **Les règles CA, dans le SDK** (`analyzers/Microsoft.CodeAnalysis.NetAnalyzers.dll`) : CA1859 (des types
  concrets au lieu d'interfaces), CA1822 (un membre qui peut être statique), CA1852 (sceller), CA1851 (les
  énumérations multiples), CA1826, CA1827, CA1829, CA1860 (des formes de LINQ, pas LINQ), CA1861 (les tableaux
  constants en argument), CA1825, CA2014 (`stackalloc` en boucle). Aucune n'interdit une construction de la liste ;
  elles en relèvent des cas.
- **Les règles IDE, dans le SDK** (`codestyle/cs/*.dll`) : `MakeAnonymousFunctionStaticDiagnosticAnalyzer` et
  `MakeLocalFunctionStaticDiagnosticAnalyzer` (IDE0320 et IDE0062 ; l'identifiant est déduit de son voisinage dans
  la DLL). Elles signalent une lambda qui **pourrait** être `static`, pas une qui capture.
- **Le Roslyn du SDK** (`Roslyn/bincore/Microsoft.CodeAnalysis*.dll`) : tout, par un outil du dépôt ;
  `tools/renommage` le lie déjà sans paquet (`tools/renommage/iXtal26.Renommage.csproj`, Q38).
- **Microsoft.CodeAnalysis.BannedApiAnalyzers, NuGet** : LINQ et toute API nommée dans `BannedSymbols.txt`
  (RS0030) ; ni les fermetures, ni les allocations, ni `virtual`.
- **ClrHeapAllocationAnalyzer, NuGet** : le boxing, les fermetures, `params`, `new`, les concaténations (ses règles
  HAA, d'après sa documentation, non relue ici) ; partout, sans notion de boucle.

Aucun des deux paquets n'est dans `~/.nuget/packages` (vérifié : `ls ~/.nuget/packages | grep -iE 'banned|heapalloc'`
ne rend rien). Aucune règle du SDK ne relève une lambda qui capture ; le compilateur, lui, refuse une capture dans une
lambda marquée `static` (déduit : CS8820).

Borner aux chemins chauds : `.editorconfig` accepte des sections par glob et y règle la sévérité de toute règle
(`dotnet_diagnostic.ID.severity`) ; `BannedSymbols.txt` vaut pour tout le projet, mais la sévérité de RS0030 se
borne par glob (déduit). Le piège : `TreatWarningsAsErrors` est plein (`iXtal26.csproj:38`) et
`EnforceCodeStyleInBuild` actif (`:39`) ; une règle levée en avertissement dans les fichiers chauds casse la
construction sur chaque occurrence existante, et la taire demande un `#pragma` ou une suppression dans le code
transcrit (des lignes neuves, des renvois qui bougent).

## 2. Ce qu'il faut faire, et les étapes proposées

### I0 — La frontière, écrite une fois

**Contenu.** Un fichier, par exemple `tools/interdits/chemins-chauds.txt`, une glob par ligne, lu par l'outil
d'I2, par le grep de la porte et recopié dans une section de `.editorconfig` si une règle y est un jour levée :

```
iXtal26/Cpu/*.cs            moins  iXtal26/Cpu/*.State.cs
iXtal26.SoftFloat/**/*.cs
iXtal26/Memory/mem.cs
iXtal26/io.cs
iXtal26/timer.cs
iXtal26/Ics/Intel8253*.cs
iXtal26/Ics/Emu8000*.cs  iXtal26/Ics/SierraSC1502x*.cs  iXtal26/Ics/TridentTKD8001.cs
iXtal26/Video/*.cs
iXtal26/Sound/*.cs
iXtal26/Lpt/lpt_dac.cs  iXtal26/Lpt/lpt_dss.cs
```

**Porte.** Les globs résolues donnent 156 fichiers à R ; chaque fichier de la liste existe ; aucun fichier
d'`iXtal26/` ne change. Un renommage de G15 qui déplace un fichier met la liste à jour dans le même commit.

**Contrôles négatifs.** Une glob qui ne résout rien est une erreur de l'outil (retour 2) ; un fichier de `Host/`
glissé dans la liste est refusé (la liste ne contient que des chemins sous les familles de PLAN.md).

### I1 — Le banc de démonstration (l'ouverture de G15)

**Contenu.** Des classes neuves dans `tools/perfbanc/` (un fichier `InterditsBanc.cs`, ou un par construction),
dans l'assemblage `iXtal26.PerfBanc` : son `InternalsVisibleTo` existe, iXtal26 n'est pas touché. Chaque cas a
quatre colonnes :

- **Réel** : le vrai code d'iXtal26, appelé comme le produit l'appelle (un `internal` directement ; un `private` par
  réflexion puis un délégué, comme `GardeBanc.cs:93`, quand le produit l'appelle lui-même par délégué) ;
- **Retenue** (`Baseline = true`) : une copie à l'identique dans le banc ;
- **Interdite** : la même copie, la seule construction interdite changée ;
- **A/A** : une seconde copie de la forme retenue, pour mesurer le placement.

L'équivalence des trois copies et du vrai code est vérifiée dans le `[GlobalSetup]` avant la mesure, comme
`GardeBanc.VerifierEquivalence` ; la machine est montée par `Reference.Monter` quand le cas lit l'état d'une vraie
machine (`MemoireBanc.cs:64-70`). `OperationsPerInvoke = 1000`, comme les bancs de G13.

Les cas, chacun « le vrai chemin » puis « la forme interdite » :

1. **Statique contre instance.** `readmemb`, `Cpu/386_common.cs:163-169` (`mem.readlookup2`, `mem.ram`), sur la
   RAM d'une machine montée. Interdit : les mêmes champs dans une classe `sealed`, lus par `this`, par un
   paramètre, et par un `static` non `readonly` (la variante C du 26/09).
2. **`for` contre LINQ.** `speaker_get_buffer`, `Sound/sound_speaker.cs:69-70`
   (`buffer[c] += speaker_buffer[c >> 1]`). Interdit : `Enumerable.Range(0, len * 2)` et `Select`, recopié dans
   `buffer`.
3. **Appel direct contre interface ou `virtual`.** L'ADD de `OP_ARITH`, `ops_286[0x00]`,
   `386_ops_arith.cs:103-131` ; le vrai est une fermeture qui appelle `op8` et `setflags8`. Trois copies :
   l'expression en ligne (la macro de PCem développée, la retenue), une interface `IAluOp` sur une classe `sealed`,
   une classe abstraite et son `override` ; le délégué (le vrai) en information.
4. **Fermeture contre méthode nommée.** Le Jcc, `ops_286[0x74]`, `386_ops_jump.cs:79-87` (`cond()` capturé).
   Inverse du cas 3 : le vrai est l'interdit, la copie nommée (un `opJE`) est la retenue.
5. **Délégué créé dans une boucle.** Une affectation de rendu de `svga_recalctimings` (un groupe de méthodes, mis
   en cache). Interdit : une lambda qui capture `svga`, créée à chaque appel.
6. **Sans allocation contre avec.** La lecture du descripteur de `loadseg`, `Cpu/x86seg.cs:80`
   (`new uint16_t[4]`). Inverse : le vrai alloue ; la copie retenue lit dans un `Span<uint16_t>` de `stackalloc`.
7. **Boxing.** L'aiguillage de `outb`, `io.cs:198`, vers un gestionnaire dont `priv` est une classe. Interdit : le
   même, `priv` un entier mis en boîte à chaque appel.
8. **`string`.** `svga_write`, `vid_svga.cs:1100-1101`, `pclog` gardé par `svga_output == 0`. Interdit :
   l'interpolation faite sans garde, écrite dans `TextWriter.Null`.
9. **`params`.** `prefetch_run`, `Cpu/386.cs:245`, huit `int`. Interdit : la même signature en `params int[]`.

Les cas 4 et 6 sont ceux où le code d'iXtal **est** la forme interdite (§ 1.4, § 1.5) : le banc chiffre ce
qu'elle coûte déjà, sans rien y changer.

**Ce qui est mesuré.** Le temps par appel et son intervalle, le rapport à la forme retenue, les octets alloués
(`MemoryDiagnoser`, déjà actif). Le code machine des colonnes par `DisassemblyDiagnoser` s'il marche ici, sinon par
`DOTNET_JitDisasm` sur le job (§ 1.7) : il dit pourquoi (un `call [rax+…]`, un `NEWARR`, un appel non inliné).

**Le lancement.** Comme `compare-ref.sh` : `setarch $(uname -m) -R taskset -c 4,6,8,10 dotnet
tools/perfbanc/bin/Release/net10.0/iXtal26.PerfBanc.dll --filter '*Interdit*' --launchCount 3`. Durée : 9 cas,
environ 36 colonnes ; à 13 s par cas et par passage (65 cas en 53 à 56 min pour quatre passages,
`compare-ref.sh:48`), 8 min par passage ; deux passages, une vingtaine de minutes, au plus 40 (déduit).

**Portes et preuves.**

- iXtal26 ne change pas : `git diff --stat -- iXtal26 iXtal26.SoftFloat` vide ; l'IL d'iXtal26 égal à celui de R
  par la table (`tools/ildump`, 0 changée, 0 disparue, 0 apparue).
- Le banc de la référence ne change pas : l'IL des types de `iXtal26.PerfBanc` qui existaient est égal, seuls des
  types neufs apparaissent (le vidage d'IL du banc, comme `il/banc-attributs.log` en G14) ; `compare-ref.sh` ne
  nomme aucun cas neuf (`GROUPES_REF`, `compare-ref.sh:79-109`).
- Chaque cas : l'équivalence passe ; la colonne A/A ne s'écarte pas de la retenue (intervalles qui se recouvrent) ;
  la colonne « réel » ne s'écarte de la retenue que dans la bande de l'A/A ; sinon le cas ne vaut rien et se
  reprend.
- Les chiffres dans `VERIFICATION.md`, une section « G15 — ce que coûtent les constructions interdites » : un
  tableau par cas, le rapport, l'intervalle, les octets, l'extrait du code machine qui l'explique, la commande.

**Contrôles négatifs** (un journal chacun) : une copie falsifiée (un drapeau faux dans la copie interdite du cas 3)
fait échouer le `[GlobalSetup]` ; la colonne interdite du cas 6 inversée (la retenue qui alloue) fait paraître des
octets dans la colonne retenue ; le cas 2 interdit montre des octets alloués, la retenue 0 ; le cas 3 « interface »
montre un appel indirect au code machine, la retenue aucun — ou, si le JIT dévirtualise, le banc le dit et le cas
le note (§ 4, D5).

### I2 — Le signalement dans la porte de G15

**Contenu.** Trois relevés, du plus précis au plus grossier, aucun paquet à installer.

1. **Un outil Roslyn du SDK**, `tools/interdits/`, bâti comme `tools/renommage` (le compilateur de
   `Roslyn/bincore`, sans paquet, hors de `iXtal26.sln`) : il compile iXtal26 en mémoire et parcourt les fichiers
   d'I0 par l'API d'opérations. Règles : LINQ (une cible dans `System.Linq.Enumerable` ou `Queryable`, une requête) ;
   fermeture (une lambda, une méthode anonyme ou une fonction locale avec des variables capturées, par l'analyse de
   flot ; la capture par structure distinguée de la capture sur le tas) ; délégué créé dans une boucle (une création
   de délégué sous une boucle) ; allocation (une création d'objet d'un type référence, un tableau, une interpolation
   ou une concaténation, un tableau `params`, une conversion de boxing), classée « en boucle », « initialiseur »,
   « ailleurs » ; interface et `virtual` (une déclaration `interface`, `abstract`, `virtual`, `override` ; un appel
   dont la cible est virtuelle ou d'interface) ; instance (une classe statique de R qui ne l'est plus, une méthode
   d'instance neuve). Sortie : un TSV, une ligne par occurrence (fichier, membre englobant, règle, rang dans le
   membre, ligne, extrait).
2. **Le grep**, comme repli et comme contre-épreuve : les motifs du § 1 (`\.(Select|Where|…)\(`, `Enumerable\.`,
   les lambdas, `\bnew\b`, `interface|virtual|abstract|override`, `\bparams\b`, `\$"`), comptés par fichier d'I0,
   comparés aux comptes de R. Il voit moins (§ 1.3 : 15 fausses alertes sur 15) mais ne dépend d'aucun outil.
3. **Les listings du JIT** que chaque étape capture déjà (`tools/listings-jit.sh capture`, 30 scénarios) : les
   méthodes de fermeture compilées (88 à R) et les aides d'allocation des méthodes chaudes compilées, le motif du
   cache de délégué écarté (§ 1.5). C'est la seule vue de ce qui a tourné.

**La base de référence.** Les relevés 1 et 3 à R sont la base (les 43 fermetures, les 13 allocations du mode
protégé, les initialiseurs) ; la porte de chaque commit de G15 exige 0 occurrence neuve. La clé est (fichier,
membre, règle, rang), pas la ligne : les lignes bougent. Les renommages de G15 passent par sa table des noms, comme
G14 l'a fait pour l'IL (`tools/noms/g14.txt`).

**Portes et preuves.** L'outil rejoué deux fois sur R rend la même base, à l'octet ; à chaque commit de G15 : 0
neuve aux relevés 1 et 3, le grep sans hausse ; les journaux à côté du verdict.

**Contrôles négatifs**, dans un worktree jetable, une insertion par règle et un journal chacun : un `.Select(` sans
aucun `using` dans `Cpu/x86.cs` (le piège d'`ImplicitUsings`) ; une lambda qui capture dans `timer.cs` ; un
`new int[4]` dans une boucle de `Video/vid_cga.cs` ; une boîte (`object o = val;`) dans `Sound/sound.cs` ; un appel
`params` dans `Memory/mem.cs` ; une interface et un `virtual` dans `Ics/Intel8253.cs` ; le `static` ôté d'une classe
de `Cpu/` ; un `$"…"` en boucle dans `Sound/sound_speaker.cs`. Chacun relevé (retour 1, la bonne règle, le bon
fichier). Les mêmes insertions dans `Host/` : aucune relevée (la frontière tient). Une lambda `static` et une
fonction locale qui capture par structure : pas d'allocation relevée. L'arbre propre : retour 0.

## 3. Les risques

- **Une copie ne mesure pas iXtal** (§ 1.7). Parade : la colonne « réel » et l'A/A ; un cas dont l'A/A dépasse
  l'écart mesuré ne prouve rien et le dit.
- **Le JIT peut effacer la différence** : une interface sur une classe `sealed` connue, un délégué constant ; sans
  paliers, la dévirtualisation gardée par PGO est absente (audit P10), mais la dévirtualisation statique reste
  (déduit). Le banc doit tourner sans paliers, comme le produit (`Program.cs:29-30`), sinon il mesure un autre JIT.
- **Le banc change `iXtal26.PerfBanc`** : le placement de ses boucles existantes peut bouger, et `compare-ref.sh`
  compare le banc de C à celui de R. G14 a déjà changé ce banc à chaque étape (des `BenchmarkAttribute`,
  `VERIFICATION.md:19508`) ; la porte d'I1 vérifie l'IL des types existants.
- **Les faux positifs** : des centaines de `new` dans les fichiers chauds sont des tables et de l'initialisation
  (§ 1.5) ; des délégués mis en cache ressemblent à des allocations au listing ; sans base de référence par membre,
  la porte serait ignorée. Les faux négatifs : le grep ne voit ni une capture ni un boxing implicite ; les listings
  ne voient que ce qui a tourné dans les 30 scénarios.
- **Le Roslyn du SDK n'est pas une API publiée** (`VERIFICATION.md:19715-19716`) : une mise à jour du SDK peut casser
  l'outil, comme `tools/renommage`.
- **La liste de PLAN.md n'est pas tout ce qui coûte** : une propriété à la place d'un champ dans `execx86` (au-delà
  de 922 locales, plus rien n'est inliné, `statique-instance-et-ecart-oracle.md` § 4), un `readonly` retiré (la
  variante C, +3 %) ne sont pas dans la liste. Le PascalCase et les `enum` de G15 y touchent ; seule la porte des 0 %
  contre R les voit.
- **Le runtime n'est pas épinglé** (`VERIFICATION.md:19717-19718`) : une mise à jour d'apt pendant le banc change le
  JIT des deux colonnes à la fois (le rapport tient, les valeurs absolues non).

## 4. Les décisions ouvertes

**D1 — La frontière, fichier par fichier.** PLAN.md:575-576 nomme des familles, pas des fichiers ; `io.cs`, le PIC,
le DMA, `pc.cs` et SoftFloat ne sont pas tranchés. *Proposition* : la liste d'I0 — `io.cs` dedans (chaque `IN`/`OUT`
y passe, c'est l'aiguillage des ports, le pendant des mappages de la mémoire), SoftFloat dedans (le x87 du mode
matériel, celui de l'utilisateur depuis G13.11), `pc.cs` dehors (`runpc` à 100 Hz, le reste la configuration), le
PIC et le DMA dehors (PLAN.md ne les nomme pas, la porte des 0 % les traverse par les cas Son et Machine, déduit).

**D2 — Les fermetures et les allocations qui existent déjà.** PLAN.md dit « ce qui coûte, on ne le fait pas » ; il
ne dit pas ce que devient ce que le code fait déjà : les 43 fermetures du cœur 286/386/486 (§ 1.4) et les 13
allocations de `x86seg.cs` (§ 1.5). *Proposition* : les admettre en base de référence, nommées une à une dans
`VERIFICATION.md`, et ne rien y changer en G15 : les retirer change le code machine des chemins les plus chauds,
c'est un levier de performance (l'audit P2 et P8), pas une convention. Le banc d'I1 les chiffre (cas 4 et 6) ; la
suite est hors de la portée (annexe).

**D3 — L'outil du signalement.** PLAN.md:585-587 veut « si possible, un analyseur ou une règle `.editorconfig` » ;
l'annexe propose deux paquets NuGet, à installer avec l'accord de Julien. *Proposition* : l'outil Roslyn du SDK
d'I2, sans paquet (le précédent de `tools/renommage`, Q38), avec le grep et les listings du JIT. C'est un analyseur
au sens de Roslyn, joué dans la porte et non dans la construction : il couvre les six constructions, quand
BannedApiAnalyzers ne couvre que LINQ et ClrHeapAllocationAnalyzer ignore les boucles ; et il ne demande ni
`#pragma` ni suppression dans le code transcrit, que `TreatWarningsAsErrors` imposerait à une règle levée dans la
construction. Aucune règle de `.editorconfig` n'est levée. Si Julien préfère un analyseur dans la construction,
BannedApiAnalyzers pour LINQ seul, borné par glob à la liste d'I0, est le plus petit pas ; il demande son accord.

**D4 — La place et la cadence du banc.** PLAN.md:580-581 veut le banc « en ouverture de G15 », versionné dans
`tools/perfbanc/` ; il ne dit pas s'il entre dans la référence des 0 %. *Proposition* : dans `iXtal26.PerfBanc`,
hors de `GROUPES_REF`, comme GardeBanc et Is486Banc (`compare-ref.sh:76`) ; joué une fois à l'ouverture, ses
chiffres dans `VERIFICATION.md`, et rejoué seulement si une étape de G15 touche à une construction de la liste.
Un commit qui ne change que le banc ou l'outillage, l'IL d'iXtal26 égal à R par la table, n'appelle pas
`compare-ref.sh` : il le dit dans son message, comme le commit outillé de Q39 (`5643228`).

**D5 — Le verdict d'un cas.** PLAN.md veut qu'on « démontre que ça coûte » ; il ne dit pas quoi écrire si un cas ne
montre rien (le JIT dévirtualise, l'allocation se perd dans le bruit). *Proposition* : « ça coûte » quand
l'intervalle du rapport interdit/retenue exclut 1 et que l'écart dépasse celui de l'A/A ; sinon le cas est écrit tel
quel dans `VERIFICATION.md`, « pas de coût mesurable sur ce chemin », avec son code machine, et la règle reste : elle
est la décision de Julien, le banc l'éclaire sans la défaire. Un tel cas est signalé à Julien dans le compte rendu
de l'étape.

## Annexe — Hors de la portée

Trouvé en route, non proposé :

- L'égalité de délégués de `svga_poll` (`Video/vid_svga.cs:977-979`), 2,5 % mesurés au 26/09 (audit P1), toujours là.
- Les fermetures du cœur 286/386/486 (§ 1.4, audit P2) et les `new uint16_t[4]` du mode protégé (§ 1.5, audit P8) :
  des leviers de performance, à l'accord de Julien (D2).
- `BigInteger` dans `arith_seize` (`x87_noyau.Materiel.cs:602`, `:637-702`) : une allocation possible au-delà de
  32 bits (déduit), sur un chemin rare.
- Les `ConditionalWeakTable` du mode matériel (`GetOrCreateValue` dans `crtc_ecrit_materiel` et
  `crtc_relu_materiel` de la MDA et de l'Hercules, `vid_et4000` `Cle`, `sound_sb_dsp` `etat_materiel`) : une
  recherche par accès au CRTC ou au DSP, non mesurée.
- `private readonly record struct Selection` (`Sound/sound_sb.Materiel.cs:378`) : R4 écarte `record` du code
  transcrit ; le fichier est du mode matériel (R10).
- `ImplicitUsings` importe aussi `System.Net.Http` et `System.Threading.Tasks` dans le code transcrit
  (`obj/Release/net10.0/iXtal26.GlobalUsings.g.cs`) : à voir avec le nettoyage des `using` de G15.
- La section du code transcrit de `.editorconfig:40` ne nomme ni `Cdrom`, ni `Scsi`, ni `Ide`, ni `Mfm`, ni
  `Joystick`, ni `Lpt`.
- `GetState` (`Cpu/808x.State.cs:507`, `:524`) alloue deux tableaux par appel dans le harnais (audit P9).
