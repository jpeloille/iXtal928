# D7 — Le mécanisme du mode matériel (G13) : constats, options, recommandation

> Lecture de reconnaissance du 6 octobre 2026, au commit `bc609ce` (arbre de travail : `iXtal26.csproj`
> modifié, `tools/perfbanc/` non commité). LECTURE SEULE : rien n'a été construit, lancé ni mesuré.
> Les affirmations sur .NET portent un niveau : **documenté** (documentation Microsoft, ou documents de
> conception de dotnet/runtime) ; **déduit** (lu dans le code de dotnet/runtime, branche `release/10.0`,
> le 06/10/2026 : un comportement d'implémentation, pas un contrat) ; **inconnu** (à mesurer).
> Runtime installé : Microsoft.NETCore.App 10.0.12, SDK 10.0.112. Les références sont en annexe A.

---

## 0. L'essentiel

1. **Le dépôt compile sans paliers.** `TieredCompilation=false` dans les trois projets qui font tourner le
   cœur (`iXtal26`, `iXtal26.Diff`, `iXtal26.PerfBanc`). Chaque méthode est compilée UNE fois, optimisée,
   à son premier appel ; rien ne la recompile. La question « un `static readonly` initialisé avant la
   compilation de niveau 1 » devient ici : **initialisé avant L'UNIQUE compilation de chaque méthode
   gardée**. Si oui, RyuJIT plie la garde en constante et n'importe pas la branche morte (déduit). Si non,
   la méthode garde à vie un contrôle d'initialisation et une lecture (déduit) — une perte de vitesse
   silencieuse, jamais une valeur fausse.
2. **Le coût ne se joue que sur une quinzaine de sites.** Sur les 110 défauts des sections A et B, la
   grande majorité des sites sont sur des chemins rares (un opcode précis, une commande de contrôleur,
   une initialisation) : une garde y est gratuite en pratique, quelle que soit sa forme. Les sites chauds
   sont peu nombreux et connus : `clockhardware` du 8088 (PB-03, chaque instruction), la lecture
   d'instruction (PB-51), les préfixes (PB-50), les aides du x87 (PB-48, 55, 56, 59, 60, 62), `cga_poll`
   (PB-04), la lecture linéaire SVGA (PB-80), les boucles d'échantillons de la SB 16 et de l'EMU8000
   (PB-148, 153, 155, 156, 160, 164).
3. **Le marqueur ne se remplace pas encore mécaniquement.** 28 des 110 PB (23 de A, 5 de B) n'ont pas de
   marqueur `// pcem bug, reproduced:` suivi de leur numéro ; PB-01 et PB-03 n'en ont aucun. Un
   recensement site par site est le préalable de G13.
4. **Cinq fichiers du x87 et la table du 386 sont générés** : aucune garde ne s'y écrit à la main.
5. **Recommandation** : deux étages de granularité (un interrupteur pour l'utilisateur, un masque par PB
   pour les outils) ; des `static readonly bool` par PB, figés avant le premier `initpc` ou la première
   remise à zéro d'un harnais, la preuve du 0 % étant le listing du JIT identique avant et après
   (`DOTNET_JitDisasmDiffable`) ; des tables de gestionnaires recopiées pour les fichiers générés ; une
   règle de placement des gardes ; aucune variable d'environnement ; l'oracle intact. Détail en § 8 et § 9.

---

## 1. Constats

### 1.1 Le précédent : `lpt_jeu_hors_service`

- **Ce qu'il est.** `model_c.lpt_jeu_hors_service` (`iXtal26/Models/model.cs:413-418`), un `internal static
  bool` mutable, lu trois fois, à l'initialisation seulement (`:179`, `:426`, `:443`). Posé par
  `iXtal26.Diff` en tête de programme, avant l'aiguillage des commandes : l'option est retirée de `args`,
  une bannière est imprimée (`tools/iXtal26.Diff/Program.cs:131-141`), ou la variable
  `IXTAL26_LPT_JEU_HORS_SERVICE=1` pour une série entière. Son pendant côté oracle :
  `h_set_lpt_jeu_hors_service` (`tools/oracle/harness.c:529`, `:2378-2380`, ABI 44). « Un outil de preuve,
  jamais une machine offerte » (`VERIFICATION.md` § G10.0).
- **Ce qui se transpose** : le geste d'outillage (une option globale traitée avant l'aiguillage, retirée
  des arguments, une bannière qui dit le mode).
- **Ce qui ne se transpose pas** :
  1. il agit des DEUX côtés ; le mode matériel n'a pas de pendant oracle (le PCem vendoré reste intact,
     aucun `h_set_*`, l'ABI ne bouge pas) ;
  2. il est froid (initialisation) ; le mode matériel touche des chemins chauds ;
  3. il n'est jamais offert ; le mode matériel l'est (ligne de commande, `.cfg`, écran de construction) ;
  4. **la variable d'environnement** : `par.sh` lance chaque porte par `dotnet $DLL …` sans filtrer
     l'environnement (`tools/gates/par.sh`, fonctions `run` et `runw`). Une variable « mode matériel »
     exportée par mégarde ferait passer tout le côté C# des portes en mode matériel. À ne pas reproduire.

### 1.2 Les réglages du JIT

| Réglage | Où | Effet |
|---|---|---|
| `<TieredCompilation>false</TieredCompilation>` | `iXtal26/iXtal26.csproj:34` (« −5 % en régime établi », § M5.2) ; `tools/iXtal26.Diff/iXtal26.Diff.csproj:23` ; `tools/perfbanc/iXtal26.PerfBanc.csproj:20`, plus `DOTNET_TieredCompilation=0` dans le job BenchmarkDotNet (`tools/perfbanc/Program.cs`) | confirmé dans `bin/Release/net10.0/iXtal26.runtimeconfig.json` : `"System.Runtime.TieredCompilation": false` |
| `TieredPGO`, `TieredCompilationQuickJit*`, `PublishReadyToRun` | absents | sans objet sans paliers |
| `Directory.Build.props`, `global.json` | absents | rien ne surcharge ces réglages |
| `AllowUnsafeBlocks` | `false` | aucune écriture d'un `readonly` par pointeur |

- **Documenté** : sans paliers, le code est celui du « Tier1 », qui « is equivalent to code that would be
  jitted for a method when tiered compilation is not in use » (dotnet/runtime,
  `docs/design/features/tiered-compilation.md`). La PGO dynamique « works hand-in-hand with tiered
  compilation […] instrumentation that's put in place during tier 0 » (learn.microsoft.com,
  *Compilation config settings*) : elle n'existe pas ici, ce que l'audit du 26/09 notait déjà (P10).
- **Déduit** : sans paliers, aucune recompilation n'a lieu ; une méthode compilée trop tôt le reste.
- Le seul code précompilé du processus est celui du framework (ReadyToRun utilisé « when it's
  available », documenté) ; `iXtal26.dll` est compilé à l'exécution.

### 1.3 Où vivent les défauts, et lesquels sont chauds

**Le compte.** 215 sites `// pcem bug, reproduced:` dans 51 fichiers. Relevé par `grep -A4` (le marqueur,
puis son numéro dans les quatre lignes qui suivent) : **28 des 110 PB des sections A et B n'ont pas de
marqueur canonique numéroté** :

- A : 01, 02, 03, 04, 05, 06, 48, 50, 51, 52, 54, 55, 56, 59, 60, 62, 70, 78, 87, 109, 129, 130, 131 ;
- B : 07, 08, 09, 10, 163.

Trois cas différents :
- *aucun marqueur* : PB-01 (`setadc8`, `setadc16`, `setsbc8`, `setsbc16`, `Cpu/808x.cs:571-696` ;
  `PCEM_BUGS.md` cite encore `:554, :591, :629, :667`) ; PB-03 (`clockhardware`, `Cpu/808x.cs:280-292` ;
  le registre cite `:270`) ;
- *un marqueur sans numéro* (antérieur à la convention) : PB-02 (`808x.cs:3070`, `:3098`), PB-04
  (`vid_cga.cs:259`), PB-09 (`:548`), PB-05 et PB-06 (`pic.cs:433`, `:70`), PB-08 (`device.cs:327`), PB-10
  (`mem_bios.cs:284`) ;
- *le numéro dans un en-tête ou en fin de ligne* : PB-48 (vingt mentions), PB-60
  (`// PB-60 : la mémoire en premier`), PB-50 (`386_ops_prefix.cs:38`), PB-52, 55, 59, 62 (en-têtes du
  x87), PB-78 (`mem.cs:188`), PB-87 (`808x.cs:182`), PB-109, 129, 130 (en-têtes), PB-163
  (`sound_emu8k.cs:1427`).

Les numéros de ligne des champs *Reproduit* de `PCEM_BUGS.md` ont dérivé : le recensement doit se faire
par marqueur, jamais par ligne.

**La fréquence d'exécution des sites** (lecture des fonctions qui les portent) :

| Fréquence | Sites |
|---|---|
| Chaque instruction ou chaque lecture d'instruction | PB-03 (`clockhardware`, 8088), PB-51 (lecture d'instruction hors limite de CS), PB-50 (trampoline des préfixes), PB-87 (préchargement du 8086), PB-07 (B, `readmemw`) |
| Chaque instruction x87 | PB-62 (FIP/FDP, s'ils sont mémorisés), les aides de `x87_ops.cs` : PB-48, 55, 56, 59, 60 |
| Chaque caractère, ligne ou accès vidéo | PB-04 (`cga_poll`), PB-80 (`svga_read_linear`, `gd5429_read_linear`), PB-89 (`m24_poll`, `pc1512_poll`), PB-96, 97, 99 (`*_poll`) |
| Chaque échantillon | PB-148, 153, 155, 156 (`sb_get_buffer_sb16`, `_emu8k`), PB-160, 164 (`emu8k_update`, réverbération), PB-90, 91, 149 (`pollsb`) |
| Un opcode précis | PB-01 (ADC, SBB), PB-02 (RCL et RCR mot), PB-45 (IDIV octet), PB-69 (ESC sur PC et XT), PB-52 à 68 (un gestionnaire x87 chacun), PB-39 à 44 (commutation de tâche, MOV CRx) |
| Une commande, un registre | les disques (Xebec, IDE), le SCSI, le CD, le ZIP, le DMA haut (PB-157), le PIC (PB-05, 06), les mélangeurs, la souris, la manette |
| L'initialisation | PB-77 (`cpu_set`), PB-104 (`loadconfig`), PB-145 (en partie), PB-165 |

Conséquence : **la forme de la garde ne compte que pour une quinzaine de sites**. Partout ailleurs, la
garde ne s'exécute que lorsque le chemin fautif s'exécute.

### 1.4 Les fichiers générés

- **Le x87** (`tools/x87gen/README.md`) : `x87_ops_arith.cs` (`gen43.py`, depuis `x87_ops_arith.h`),
  `x87_ops_misc.cs` (`gen44.py`), `x87_ops_tables.cs` (`gentab.py`), `x87_ops_808x.cs` et
  `x87_ops_808x_tables.cs` (`gen46.py`, qui recopie `x87_ops_loadstore.cs`, `x87_ops_arith.cs` et
  `x87_ops_misc.cs` dans `_808x`). « Les rejouer doit rendre les fichiers commités à l'octet près » ;
  `x87_ops_808x.cs:14` : « Ne pas éditer à la main ». **30 des 215 sites sont dans ces fichiers**
  (16 + 12 + 2), et chaque correction du x87 existe deux fois : instanciation `_386` et instanciation
  `_808x` (le 8087).
- **La table du 386** : `Cpu/386_ops_table386.cs`, « GÉNÉRÉ par tools/ops386-table.py --emit ; ne pas
  retoucher à la main ».
- Les générateurs savent déjà porter des écarts : `x87_ops_arith.cs:13` « et trois écarts nommés ».

### 1.5 La configuration et les chemins de lancement

- `pc.loadconfig` (`pc.cs:424` et suivantes) lit les clés de la section générale ; les sections de
  device passent par `device_get_config_*`. `config.c` n'est pas lié dans l'oracle
  (`PluginApi/config.cs:10-15`) : **une clé de `.cfg` est invisible de l'oracle**.
- **boot-diff lit le `.cfg` lui-même**, côté C#, par `pc.loadconfig` (`tools/iXtal26.Diff/BootDiff.cs:427`),
  puis pousse chaque scalaire à l'oracle (`:592-620`) et les sections de device (`:345-350`). Une clé
  « mode matériel » dans un `.cfg` mettrait le côté C# en mode matériel sans que rien ne le dise.
- **Des portes lisent les profils de l'utilisateur** : `bd-xtcfg` et les `bd-xtdos-*` prennent
  `ixtal26-xt.cfg` à la racine (`tools/gates/series.sh`). La clé dans ce profil rougirait ces portes.
- La précédence : défauts, puis fichier, puis ligne de commande, avant `initpc`
  (`iXtal26/Docs/ligne-de-commande.md`, « Précédence ») ; `MachineOverrides` est partagé par la fenêtre,
  `--boot` et `--timer-check`.
- L'écran de construction « ne monte rien » (`Host/SdlSetup.cs`, en-tête) : il remplit ce que
  `loadconfig` remplit, puis `initpc` monte. Ses lignes : `Item` (`:47-50`) ; l'enregistrement dans
  `configs/` : `SaveMachine` (`:809-895`) ; son auto-contrôle : `SelfCheck` (`:1151`), hors `series.sh`.
- La barre de titre est en ASCII pur, délibérément (`Host/SdlHost.cs:30-38`) : « materiel », sans accent.
- **Les points où un cœur démarre** : `pc.initpc` (SdlHost, `--boot`, `--timer-check`, `SdlMenu.SelfCheck`,
  boot-diff, les `r9-*`) ; mais aussi, SANS `initpc`, les remises à zéro des harnais (`Cpu/808x.State.cs`,
  `Cpu/386.State.cs` : `Reset386`, `Reset486`), dont se servent `Fuzzer`, `PageCheck`, `PmCheck386`,
  `PopSsCheck`, `Sst386Probe`, `X87Cases` et le banc `Is486Banc`.

### 1.6 Les portes et l'outillage

- `par.sh` fait tourner chaque porte `run` dans un bac à sable où `nvr/` et `os/` sont des COPIES de
  `nvr/default/` et `os/pcdos20/` : aucune porte ne lit le CMOS de session de l'utilisateur.
- `iXtal26.Diff` a quarante-cinq commandes. La plupart comparent à l'oracle. En C# seul : les quatorze
  `r9-*`, `config-check`, `fdc-trace`, `sst-probe` et `sst386-probe` sous `--target csharp`, une partie de
  `popss-check`.
- **Le corpus SST** : `sst-baseline.tsv` (84 formes du 8088) et `sst386-baseline.tsv` (941 formes du 386,
  mode réel) disent ce que PCem passe. Les VECTEURS ne sont pas sur la machine (seuls les manifestes,
  `.gitignore`), et `sst-probe` n'est pas dans `series.sh`. § M5.1 : le C# reproduit la ligne de base,
  160 592 / 166 998 cas, en 10 min 20 s avec `--limit 2000`.
- **`tools/perfbanc/`** (non commité) : BenchmarkDotNet 0.15.8, un job mutateur
  `DOTNET_TieredCompilation=0`, et `Is486Banc.cs`, qui est EXACTEMENT la mesure dont G13 a besoin :
  « ce que coûte un test de famille dans la boucle ». Le vrai `opCMPSB_a16`, qui lit `is486` (un
  `internal static int` mutable, `Cpu/x86.cs:347`), contre une copie où le test devient une constante,
  les deux par le délégué `OpFn`, l'équivalence vérifiée avant de mesurer. Aucun résultat n'est consigné.

### 1.7 Les règles et leurs plafonds

- `TRANSCRIPTION.md` : 218 lignes pour un plafond de 240 (R3) : **22 lignes** pour tout ce que G13 y
  ajoute.
- R1(d) est une liste close : il faut un marqueur neuf. L'en-tête de `PCEM_BUGS.md` (lignes 8-10) dit que
  « les sites de reproduction portent tous le marqueur `// pcem bug, reproduced:` » : à amender.
- `oracle.tsv` a une colonne `status` (`transcribed | deviated | partial | host`) ; `check-oracle.sh`
  ignore toute ligne dont `c_path` vaut `-` sans valider le statut : un statut `materiel` ne lui coûte
  rien.

---

## 2. La granularité (question 1)

| Granularité | Pour, dans ce code | Contre, dans ce code |
|---|---|---|
| **Une option globale** | une clé, une ligne d'écran, un profil par machine ; un seul champ à figer | un défaut du mode matériel ne se localise pas : rien ne permet de couper une correction pour voir si elle est en cause ; aucun contrôle de fuite par PB (§ 6.3) |
| **Une par domaine** (processeur, x87, stockage, vidéo, son, carte mère) | suit l'ordre des étapes de G13 et les lectures par domaine ; permettrait à l'utilisateur d'arbitrer (le processeur exact, le son de PCem) | les frontières sont floues : PB-03 (processeur ou carte mère : le rafraîchissement du PIT), PB-69 (x87 ou carte mère : IRQ13 et NMI), PB-92 (son ou carte mère : l'IRQ 10 sans second PIC), PB-123 et PB-145 (CD ou son), PB-157 (DMA haut, carte mère ou son) ; les domaines ne coïncident pas avec les classes statiques (le x87 est dans `Cpu/`, le DMA dans `Models/`) : le champ ne peut pas vivre « dans » la classe du domaine |
| **Une par PB** | colle aux marqueurs et au registre ; permet la bissection et le contrôle de fuite ; une garde `materiel.pb_45` se retrouve par `grep` | 110 interrupteurs : inutilisables à l'écran et dans un `.cfg` ; 2^110 combinaisons, dont on ne teste que « aucun », « tous » et « chacun seul » |
| **Une clé par carte, dans sa section de device** (`device_config_t`, `[Sound Blaster 16]`) | le mécanisme de PCem pour les réglages d'une carte | ni le processeur, ni le x87, ni la carte mère ne sont des `device_t` ; ajouter une entrée aux tableaux `*_config` est une DEVIATION sur des données transcrites ; la valeur est lue à l'initialisation dans un champ d'instance, qu'aucun JIT ne plie ; l'écran de construction n'édite pas ces sections |

**Proposition : deux étages.** Un champ par PB dans une classe centrale (`materiel.pb_45`), qu'aucun
utilisateur ne voit ; un interrupteur unique pour l'utilisateur (clé, écran, profils) ; les noms de
domaine comme ALIAS de listes de PB, pour la ligne de commande, les outils, la documentation et le
découpage des étapes. Une table attribue chaque PB à un seul domaine, cas frontières compris.
`launchSettings.json` reçoit un ou deux profils (le 486 de référence, peut-être le 5150), jamais une
combinaison.

---

## 3. La forme (question 2)

### 3.1 La clé du `.cfg`

- **Nom** : `hardware_mode = 0 | 1`, section générale, en anglais comme les clés de PCem et comme
  `zip_path`, la clé propre à iXtal. À trancher contre un nom français (`materiel`).
- **Lecture** : `pc.loadconfig`, après `model` ; une valeur hors de {0, 1} est ramenée à 0 avec un
  avertissement, et une porte `r9-*` le prouve (« Ce qu'un bloc livre », point 5). **Écriture** :
  `SdlSetup.SaveMachine`. PCem ignore les clés qu'il ne connaît pas : un `.cfg` d'iXtal reste lisible par
  PCem.
- **Le prix** : la clé voyage avec la machine, et boot-diff lit les `.cfg`. Il faut donc que boot-diff la
  REFUSE explicitement (retour 2, « mode matériel demandé par le fichier : l'oracle est PCem »), et que
  les profils lus par des portes ne la portent jamais. Le mode matériel s'offre par un profil de
  lancement (`--config ixtal26-486.cfg --hardware-mode`) plutôt que par un `.cfg` de plus.
- **Variante** : pas de clé du tout, la ligne de commande seule. Plus sûr pour les portes ; la machine
  enregistrée par l'écran perd alors son mode.

### 3.2 La ligne de commande

- `--hardware-mode [LISTE]` : sans valeur, tout ; LISTE = des domaines ou des `PB-nn`, séparés par des
  virgules (la bissection) ; `--hardware-mode aucun` force le mode PCem par-dessus un `.cfg`. Un nom ou
  un numéro inconnu sort en 2.
- Sa place : `Launcher.ApplyArgument` et `MachineOverrides` (donc aussi `--boot` et `--timer-check`),
  `UsageText`, et le pourquoi dans `iXtal26/Docs/ligne-de-commande.md`, puisque `Host/CommandLine/` ne
  porte aucun commentaire (TRANSCRIPTION.md, Portée).
- Le même nom partout, dans `iXtal26` et dans `iXtal26.Diff`, pour qu'un `grep` les trouve ensemble.
- **Aucune variable d'environnement** (§ 1.1). Une série « en mode matériel » s'écrit en lignes
  explicites de `series.sh`. En défense, `par.sh` peut refuser de partir si une variable `IXTAL26_*`
  inconnue est posée.

### 3.3 L'écran de construction et `--setup-check`

- Une ligne `Mode : PCem` / `Mode : materiel` ; Entrée bascule ; `SaveMachine` écrit la clé ;
  `loadconfig` la relit au chargement d'une machine.
- `--setup-check` : la ligne existe et vaut PCem par défaut ; Entrée bascule et rebascule ; enregistrer
  puis recharger rend le mode ; l'écran n'a pas figé le mode (§ 3.5).
- La barre de titre : `iXtal26 - <machine> - materiel - invite …` quand le mode est actif, inchangée
  sinon. `--verbose`, `--boot` et `--timer-check` impriment le mode en tête.

### 3.4 Les outils

| Commande | Mode matériel | Raison |
|---|---|---|
| boot-diff, fuzz, pm-fuzz, page-check, pm-check, core286-check, fetch-probe, at-probe, cpu-config-check, sst-diff, x87-cases, x87-nan-order, x87-parity, opl-tables-check, sb16-filter-check, emu8k-tables-check, emu8k-kernel-check, cdimage-check, svga-linear-check, disc-probe, speaker-probe, vga-probe, trace-hash-check, bench… | **refusé, retour 2** | l'oracle est PCem ; un vert ou un rouge n'y dirait rien |
| `sst-probe`, `sst386-probe` | **accepté sous `--target csharp` seulement** | SST est du silicium : c'est l'oracle naturel du mode matériel pour le 8088 et le 386 en mode réel |
| les quatorze `r9-*`, `config-check` | **accepté** | C# seul ; la survie doit tenir dans les deux modes (R9) |
| `fuzz` en *contrôle de fuite* (§ 6.3) | accepté sous une option dédiée, par PB | la divergence y est la mesure, et son périmètre le verdict |

- **Une liste d'acceptation, pas une liste de refus** : chaque commande déclare si elle accepte le mode ;
  toute commande neuve le refuse par défaut.
- **sst-probe** : la ligne de base du mode matériel est un fichier GÉNÉRÉ à part
  (`sst-baseline-materiel.tsv`), jamais `sst-baseline.tsv`, qui est « ce que l'oracle (donc PCem) fait
  réellement passer » (`SstProbe.cs`) : `--baseline` refuse d'écrire ce dernier en mode matériel. Le
  verdict : pour chaque forme, passe(matériel) ≥ passe(PCem), et égal à la ligne de base du mode
  matériel ; strictement plus grand sur les formes visées (PB-01 : ADC et SBB ; PB-02 : RCL et RCR mot ;
  PB-45 : IDIV octet).
- **L'oracle n'est pas touché** : ni `h_set_*`, ni changement d'ABI, `check-oracle.sh` sans dérive.

### 3.5 Où se fige le mode

Le mode se fige UNE fois, avant le premier démarrage d'un cœur, en trois endroits idempotents :
1. au début de `pc.initpc` (la fenêtre, l'écran de construction, `--boot`, `--timer-check`, les auto-contrôles) ;
2. dans les remises à zéro des harnais (`808x.State.cs`, `386.State.cs`), qui démarrent un cœur sans `initpc` ;
3. en tête de `iXtal26.Diff/Program.cs`, après la lecture de l'option et avant l'aiguillage des commandes.

Toute demande de mode APRÈS le gel est refusée bruyamment (« le mode se choisit au lancement ») : un gel
prématuré, qui figerait en silence le mode PCem, devient une erreur visible. L'écran affiche la DEMANDE,
jamais la valeur figée.

---

## 4. Le coût (question 3)

### 4.1 Ce que fait .NET 10

| # | Fait | Niveau | Source |
|---|---|---|---|
| 1 | Sans paliers, chaque méthode est compilée une fois, optimisée, au premier appel | documenté | `tiered-compilation.md` (le Tier1 « equivalent to code […] when tiered compilation is not in use ») ; learn, *Compilation config settings* |
| 2 | La lecture d'un champ `static readonly` (InitOnly) entier ou booléen devient une constante si l'optimisation est active ET si la classe propriétaire est déjà initialisée à la compilation | déduit | `importer.cpp` : `impImportStaticReadOnlyField` (`opts.OptimizationEnabled()`, puis `getStaticFieldContent`) ; `jitinterface.cpp` : `CEEInfo::getStaticFieldContent` (`IsClassInitedOrPreinited() && IsFdInitOnly(...)`) |
| 3 | Le JIT ne lance pas le constructeur statique à la compilation : classe non initialisée, il émet un appel d'initialisation et une vraie lecture | déduit | `jitinterface.cpp` : `CEEInfo::initClass` (rend `CORINFO_INITCLASS_USE_HELPER`) ; `importer.cpp` : `impInitClass` |
| 4 | « Pré-initialisée » veut dire « initialisable sans exécuter de code de l'utilisateur » : un initialiseur qui lit la demande n'en est pas | déduit | `methodtable.h` : commentaire de `IsClassInitedOrPreinited` |
| 5 | Une branche dont la condition est constante est pliée à l'importation ; le bloc mort n'est pas importé | déduit | `importer.cpp`, `CEE_BRTRUE`/`CEE_BRFALSE` puis `COND_JUMP` (« The conditional jump becomes an unconditional jump », `Metrics.ImporterBranchFold`) |
| 6 | Un `static bool` mutable d'une classe non générique initialisée : adresse constante dans le code, donc une lecture, un test et un saut à chaque évaluation | déduit (codegen exact : inconnu) | `jitinterface.cpp` : accesseur `CORINFO_FIELD_STATIC_ADDRESS`, `IAT_VALUE`, et `CORINFO_FLG_FIELD_INITCLASS` si la classe n'est pas initialisée |
| 7 | Les seuils qui font basculer une méthode en MinOpts (60 000 octets d'IL, 20 000 instructions, 2 000 blocs, 2 000 locales, 8 000 références) se comptent sur l'IL AVANT importation, donc avant tout pliage ; en MinOpts, aucun `static readonly` n'est plié | déduit | `compiler.cpp` : `compSetOptimizationLevel` après `fgFindBasicBlocks` ; `compiler.h` : `DEFAULT_MIN_OPTS_*` ; `impImportStaticReadOnlyField` sort si l'optimisation est coupée. Déjà constaté par le dépôt : `VERIFICATION.md` § M5.1 |
| 8 | Le pré-examen de l'inliner ne tient pour constantes que `ldnull` à `ldc.r8` (et `ldstr`, `ldtoken`, `sizeof`) : un `ldsfld` de `static readonly` y compte comme du code, et la garde grossit l'estimation de taille de l'appelé | déduit | `fgbasic.cpp` : `PushConstant` sur `CEE_LDNULL`…`CEE_LDC_R8` ; `CALLSITE_FOLDABLE_BRANCH` seulement si l'opérande est constant ; seuils `ALWAYS_INLINE_SIZE` = 16 (`inline.h`) et `DEFAULT_MAX_INLINE_SIZE` = 100 (`compiler.h`) ; `AggressiveInlining` passe outre |
| 9 | `FieldInfo.SetValue` sur un champ statique InitOnly lève une exception : une valeur figée ne peut être réécrite | documenté | learn, `FieldInfo.SetValue`, Remarques |
| 10 | `RuntimeHelpers.RunClassConstructor` « ensures that the type initializer […] has been run » ; lire un champ de la classe suffit aussi | documenté | learn, `RuntimeHelpers.RunClassConstructor` |
| 11 | `DOTNET_JitDisasm`, `JitDisasmSummary`, `JitDisasmDiffable` (pointeurs remplacés, listings comparables au texte près) et `JitStdOutFile` existent dans le runtime livré | documenté | dotnet/runtime, `docs/design/coreclr/jit/viewing-jit-dumps.md` |
| 12 | Le code d'une image ReadyToRun ou NativeAOT est compilé sans connaître la valeur figée : la garde y redevient une lecture | déduit | conséquence de 2 et 3 ; à reconsidérer si G17 publie en ReadyToRun |
| 13 | Le coût en cycles d'une lecture en L1 et d'un saut bien prédit sur le processeur de Julien (Core Ultra 7 258V, § M5.1) ; l'identité octet pour octet du code après pliage | inconnu | à mesurer (§ 4.4) |
| 14 | BenchmarkDotNet lance-t-il chaque valeur d'un `[Params]` dans son propre processus (donc un gel par valeur) ? | inconnu | à vérifier avant d'écrire le banc |

### 4.2 Ce que cela veut dire dans ce code

- **Avec un `static readonly` gelé à temps, le mode PCem est gratuit** (2, 5) : la garde disparaît avant la
  génération du code. Le gel « à temps » est réaliste, car aucune méthode chaude n'est compilée avant
  `initpc` ou la remise à zéro d'un harnais. Le défaut d'ordre se paie en vitesse, jamais en justesse
  (3) : la valeur lue est la bonne, simplement lue. Il se DÉTECTE au listing (11).
- **Un `static bool` mutable coûte une lecture, un test et un saut par évaluation** (6) : le même coût que
  les tests de `is486` déjà présents dans les gestionnaires, que `Is486Banc` sait mesurer.
- **L'IL grossit dans les deux cas** (7) : `execx86` est « à 60 % du seuil de blocs » (§ M5.1). Une garde
  ajoute deux blocs au moins ; les sites du 8088 (PB-01, 02, 03, 45, 69, 87) n'en ajoutent que quelques
  dizaines. Marge large, mais à recompter à chaque étape qui touche `execx86`.
- **L'inliner peut changer d'avis en mode PCem** (8) : une garde ajoutée dans une petite méthode qu'il
  inline aujourd'hui sans `AggressiveInlining` peut la faire passer au-dessus du seuil. D'où la règle de
  placement du § 4.3.
- **Les fichiers générés** (§ 1.4) n'admettent aucune garde écrite à la main.

### 4.3 La règle de placement des gardes (indépendante de la forme)

1. **Au niveau le plus froid** : jamais dans une boucle par échantillon, par pixel ou par octet si la garde
   peut se tenir au-dessus. Une garde par tampon (`sb_get_buffer_sb16`), par ligne (`*_poll`) ou par
   commande, pas par élément.
2. **Jamais dans une méthode que le JIT inline aujourd'hui sans `AggressiveInlining`** : la garde va chez
   l'appelant, ou le listing prouve que rien n'a bougé.
3. **Jamais dans un fichier généré** : un écart nommé du générateur, dont le rejeu reste à l'octet près,
   ou une table recopiée dont on remplace l'entrée (option C, § 8).
4. **Le code propre au mode matériel est une aide PURE** quand elle sert les deux instanciations du x87 :
   le calcul (classe FXAM, reste partiel, NaN, arrondi), sans lecture de mémoire ni de code, partageable
   entre `_386` et `_808x`.

### 4.4 Ce qu'il faudrait mesurer, et comment (sans le lancer ici)

- **M0 — la référence d'avant G13**, prise avant toute garde : les listings du JIT des méthodes chaudes
  en mode PCem (`DOTNET_TieredCompilation=0 DOTNET_JitDisasmDiffable=1
  DOTNET_JitDisasm="execx86 clockhardware setadc8 exec386 opF6_a16 x87_ld80 cga_poll svga_read_linear sb_get_buffer_sb16 emu8k_update …"
  DOTNET_JitStdOutFile=…`, sur `--boot` et sur un fuzzeur) ; `DOTNET_JitDisasmSummary=1` (aucune méthode
  en MinOpts) ; les comptes d'IL d'`execx86` et d'`exec386` (`ilspycmd -il`, la méthode de § M5.1).
- **M1 — le coût d'une garde, au banc** (le patron d'`Is486Banc`) : pour quatre sites représentatifs
  (un gestionnaire du 386 par la table, une aide du x87, une boucle d'échantillons, un rendu vidéo),
  trois formes, sans garde, `static bool`, `static readonly bool` figé dans `[GlobalSetup]`, par le même
  délégué, l'équivalence vérifiée avant de mesurer. Verdict : les intervalles de confiance de
  BenchmarkDotNet.
- **M2 — la preuve directe du mode PCem** : après chaque étape, les listings `Diffable` des mêmes méthodes
  comparés à M0. Vides : 0 %, sans statistique (c'est « la preuve directe du 0 % » de l'annexe de
  PLAN.md). Non vides : M1 sur ces méthodes-là.
- **M3 — la machine entière** : `iXtal26.Diff bench --side csharp` (le 8088), `--timer-check` (cadence et
  marge) sur le 5150, l'ami286, l'ami386dx et l'ami486, avant et après, passes alternées, sur les quatre
  cœurs P (un seul cœur fausse la mesure, § M5.1).
- **M4 — le prix du mode matériel lui-même** (PB-51 ajoute un contrôle de limite à chaque lecture
  d'instruction) : M3 sous le mode. Critère proposé : chaque machine garde une marge > 1 au
  `--timer-check`. Le 0 % ne s'y applique pas.

**Une décision à prendre** : la règle « 0 % de perte » de G15 se mesure contre une référence figée
AVANT G14, donc APRÈS G13. Sans autre règle, le coût de G13 entrerait dans la référence sans être vu. Il
faut soit étendre le 0 % au mode PCem pendant G13, contre M0, soit l'accepter et l'écrire.

---

## 5. Les règles (question 4)

- **R1** : (d) reçoit `// pcem bug, fixed in hardware mode: PB-nn`. Le marqueur REMPLACE `reproduced` sur
  le site corrigé ; il dit « reproduit en mode PCem, corrigé en mode matériel ». Le compte des
  `reproduced` des sections A et B décroît à chaque étape, celui des `fixed in hardware mode` croît :
  l'avancement de G13 se lit au `grep`. Le même marqueur ouvre chaque fonction de `*.Materiel.cs` et
  tient lieu de provenance : la référence documentaire vit dans l'entrée PB, pas dans le code. Aucune
  autre sorte de commentaire n'est nécessaire.
- **R2, la règle de décompte** : la commande de R2 reçoit un filtre de plus,
  `… | grep -v 'materiel\.' | wc -l`. Les lignes de garde sortent du décompte ; tout autre code du mode
  matériel écrit DANS la région compte, et le plafond de +25 % le renvoie dans un fichier
  `*.Materiel.cs` (`STATUS: materiel`, hors R2, sur le patron de `808x.State.cs`, « R2 ne s'applique
  pas ») dès qu'il dépasse quelques lignes. Un contrôle mécanique complète le compte : `git diff -w` d'une
  étape G13 sur un fichier transcrit ne retire ni ne modifie AUCUNE ligne vivante, hors marqueurs (un
  `if (!materiel.pb_02) { … }` qui enveloppe des lignes de PCem ne fait que les réindenter).
- **R4** : des `bool` et des sauts ; des tables de délégués recopiées (`delegate` est dans la liste
  close) ; des classes partielles (déjà la convention). Ni interface, ni stratégie, ni générique (la
  spécialisation d'un générique sur une `struct` serait gratuite, mais R4 l'interdit), ni
  `#if` (écarté au § 8).
- **R8** : « Si le C est faux, on transcrit la fausseté » reste vrai en G13 : la fausseté reste
  transcrite et vivante en mode PCem ; la correction s'ajoute à côté.
- **R9** : le mode matériel n'ajoute aucun arrêt ; une correction partielle retombe sur le comportement de
  PCem, jamais sur un `fatal()`. Les sites `not reproduced` de R9 sont déjà le comportement le plus sûr
  dans les deux modes ; G13 n'y touche pas. Le modèle de R9 est celui de G13 : les outils écartent
  l'oracle, un test en C# seul prouve.

**Proposition de R10**, au style du fichier (une dizaine de lignes ; avec les deux lignes d'amendement de
R1 et de R8, le fichier passe de 218 à environ 230 lignes, sous le plafond de 240) :

> - **R10 — le mode matériel** (G13, *à valider*). Un défaut de PCem reproduit (`PB-nn`, sections A et B
>   de `PCEM_BUGS.md`) se corrige À CÔTÉ, jamais à la place : la ligne fausse reste transcrite, et le mode
>   PCem — le défaut, celui des portes — l'exécute ; la correction est gardée par `materiel.pb_nn`, figé
>   avant le premier `initpc` et immuable ensuite. Le site prend `// pcem bug, fixed in hardware mode:
>   PB-nn` ; l'entrée PB nomme la documentation, le cas qui discrimine et la panne qui le rougit. Le code
>   propre au mode va dans `*.Materiel.cs` (`STATUS: materiel`, hors R2) ; la région transcrite ne reçoit
>   que les gardes, que R2 ne compte pas (`| grep -v 'materiel\.'`), et ce qui tient sous son plafond.
>   Dans un fichier généré, la garde est un écart du générateur, ou la table est recopiée. L'oracle n'a pas de mode
>   matériel : tout outil qui le compare refuse le mode (retour 2). Le mode matériel reste déterministe,
>   n'appelle aucune fonction de l'hôte à résultat variable et n'ajoute aucun arrêt (R9).

« Aucune fonction de l'hôte à résultat variable » vise les transcendantes (PB-68) : `Math.*` suit la libm
de l'hôte, qui change d'un système à l'autre ; une correction du mode matériel ne peut pas en dépendre.

---

## 6. La vérification (question 5)

### 6.1 Prouver que le mode PCem n'a pas bougé

1. **La série complète, journal par journal**, durées retirées (`tools/gates/README.md`), contre la
   dernière série d'avant l'étape : verdicts, comptes d'instructions, sondes, empreintes, images comparées.
   G13 change l'émulateur : une série entière par étape, une seule à la fois.
2. **M2** : les listings du JIT identiques (option B), ou M1 et M3 (option A).
3. **`ops-count`** identique ; avec l'option C, `x86_opcodes` est le MÊME objet qu'`ops_386` en mode PCem
   (égalité de références, contrôlée par une porte).
4. **SST** : `sst-probe --target csharp --limit 2000` rend `sst-baseline.tsv` à l'identique (le condensé
   de § M5.1), à chaque étape qui touche le processeur. Il faut les vecteurs (§ 10).
5. **Le recensement** : une porte compare les PB des sections A et B à leurs marqueurs (chaque PB a au
   moins un site ; chaque site a son numéro ; un PB marqué `fixed in hardware mode` a une entrée qui le
   dit) ; elle vérifie aussi qu'aucun `.cfg` de `tools/gates/cfg/` ni aucun profil lu par une porte ne
   porte la clé.

### 6.2 Tester le mode matériel

- **Le cas qui discrimine** : pour chaque PB, un état d'entrée qui rend la valeur de PCem en mode PCem et
  celle de la documentation en mode matériel. Les deux résultats sont écrits dans la porte. Il prouve
  d'un même geste que la correction est juste et qu'elle est atteinte : « une correction qu'aucun cas
  n'atteint ne prouve rien », comme une survie R9 qui n'est pas passée par sa garde. Sa panne injectée
  (la garde inversée, construite à part) le rougit : « les contrôles négatifs de chaque étape, chacun
  construit à part et chacun rouge » (PLAN-G12, La vérification).
- **SST** sous `--target csharp --hardware-mode`, pour le 8088 et le 386 en mode réel (§ 3.4).
- **Les bancs dirigés en C# seul** (SBBANC, SB16BANC, AWEBANC, AHABANC, ATAPIBANC, ZIPBANC, X87BANC…), par
  `--boot` et `--type` en mode matériel, leurs sorties comparées aux valeurs de la documentation,
  committées avec leur référence.
- **Les `r9-*` dans les deux modes.**
- **Une sonde du mode** (point 3 de « Ce qu'un bloc livre ») : un compteur par PB, un tableau d'entiers
  incrémenté DANS le bloc matériel (gratuit en mode PCem, où le bloc est mort), lu par les outils en C#
  seul. Il dit quelles corrections un scénario a exercées, et donne la couverture par PB (point 4).
- **Le déterminisme** : deux exécutions du même scénario en mode matériel rendent les mêmes empreintes
  (`--boot` imprime `ins`, `tsc`, l'empreinte FNV-1a du framebuffer et la sonde du son) ; une porte les
  joue deux fois.
- **Des témoins** : `--timer-check`, où la correction de PB-03 doit rapprocher l'INT 8 de 18,2065 Hz (les
  −5,87 ppm qui resteront sont ceux de PB-11, § 10) ; MSD, Windows, et ce que les lectures par domaine
  proposent.

### 6.3 Le contrôle de fuite, que permet la granularité par PB

Le fuzzeur et boot-diff restent des outils de mode PCem, mais une option dédiée en fait des détecteurs
de fuite :
- `fuzz --hardware-mode PB-45 --fuite F6.7` : le côté C# corrige PB-45 seul ; toute divergence avec
  l'oracle doit tomber dans le périmètre déclaré (« FUITE » sinon), et au moins une doit s'y produire
  (« NON ATTEINT » sinon). Une table PB → formes d'opcode suffit. Il prouve que la correction ne touche
  rien d'autre, ce que SST ne voit pas hors de ses formes, et il vaut pour le 286, le 386 protégé, le 486
  et le x87, que SST ne couvre pas ;
- boot-diff, plus lourd : vert si aucune divergence ne précède le premier passage par la correction (le
  compteur de la sonde, avec son rang d'instruction).

### 6.4 Garder les deux modes vérifiés dans le temps

Une section G13 dans `series.sh` : le recensement (§ 6.1-5) ; les refus (une commande d'outil qui
vérifie que boot-diff et le fuzzeur refusent le mode, et rend 0 quand ils refusent) ; `sst-materiel`
pour le 8088 et le 386 (les vecteurs exigés : une porte qui les trouve absents est ROUGE, jamais muette) ;
les cas discriminants par domaine ; les `r9-*` en mode matériel ; le déterminisme ; les contrôles de
fuite du processeur et du x87. Dès lors, « toutes les séries identiques à l'unité » de G14 et G15 vaut
pour les deux modes, et la référence de performance de G15 se prend dans les deux.

---

## 7. La persistance et l'usage (question 6)

- **Le CMOS** : le mode n'y est pas, et ne doit pas y être (l'invité ne le voit pas). Un fichier
  `nvr/.MACHINE.nvr` par machine, partagé par les deux modes : une machine, une pile. Les portes n'en
  souffrent pas (bac à sable, § 1.6). Une exception à documenter : l'EEPROM de l'AHA-1542C
  (`nvr/.aha1542c.nvr`, 32 octets persistés, PB-136) peut s'écrire différemment selon le mode, puis se
  relire dans l'autre.
- **Les images disque** : partagées, sans marque de mode. Une image porte parfois la trace d'un défaut :
  le Xebec formate à côté (PB-22, latent sous PC DOS 2.00), le disque SCSI annonce un secteur de trop
  (PB-129), le ZIP a sa capacité (PB-126). À dire dans la documentation de l'utilisateur. Les images de la
  recette des portes, fabriquées par émulation en C# seul (`xtide-format.keys`, `aha-format.keys`), se
  font toujours en mode PCem : sans variable d'environnement rien ne peut l'y mettre, et `g5w.sha256`
  rougirait.
- **Les profils** : `launchSettings.json` reçoit un profil « mode materiel » pour une ou deux machines,
  par `--hardware-mode` ; pas de `.cfg` de plus (§ 3.1).
- **Ce que voit l'utilisateur** : la ligne de l'écran de construction, la barre de titre, la bannière de
  `--boot`, de `--timer-check` et de `--verbose`. Le mode PCem reste le défaut (PLAN.md) ; le défaut
  offert au public sera une question de G17.
- **La documentation** : `UsageText` et `ligne-de-commande.md` ; une section « Le mode matériel » dans
  `iXtal26/README.md` (ce qu'il corrige, ce qu'il coûte, les images entre deux modes) ; `PCEM_BUGS.md`
  (l'en-tête des lignes 8-10, un champ *Corrigé en mode matériel* par entrée : sites, référence, cas
  discriminant, effet mesuré ; le tableau de « Portée ») ; `TRANSCRIPTION.md` (R1, R2, R8, R10) ;
  `VERIFICATION.md` § G13 ; `PLAN-G13.md` ; `oracle.tsv` (statut `materiel` des `*.Materiel.cs`).

---

## 8. Les options de mécanisme

Les trois options partagent : la granularité à deux étages (§ 2), la forme (§ 3), la règle de placement
(§ 4.3), les règles (§ 5), la vérification (§ 6). Elles diffèrent par le stockage du drapeau et le geste
aux sites.

### Option A — « L'interrupteur » : un `static bool` par PB, lu à chaque passage

Le précédent `lpt_jeu_hors_service`, par PB. Gardes en ligne ; code long dans `*.Materiel.cs`.
- **Mécanisme** : environ 350 lignes (la classe et sa table des domaines, la ligne de commande, la clé,
  l'écran, l'outillage, le recensement), avant toute correction.
- **Mode PCem** : une lecture, un test et un saut par passage sur chaque garde. Nul en pratique sur les
  chemins rares ; MESURABLE sur la quinzaine de sites chauds. Pour PB-03 (dans `clockhardware`, qu'`execx86`
  appelle à chaque instruction), la garde par instruction est inévitable : dupliquer `execx86` (plus de
  2 600 lignes) est exclu. Pour PB-51, on peut dupliquer la boucle d'`exec386` (`exec386_materiel`,
  choisie cent fois par seconde dans `runpc`) : une centaine de lignes de plus, à maintenir et à renommer
  en G14.
- **Preuve du 0 %** : statistique seulement (M1, M3), à chaque étape ; le code machine change forcément.
- **Pour** : aucune dépendance au JIT ; le mode peut changer d'un amorçage à l'autre dans un même
  processus ; indifférent à ReadyToRun et à NativeAOT (G17).
- **Contre** : une perte possible, petite et cumulée, que la règle du 0 % devra chiffrer site par site.

### Option B — « Le mode figé » : un `static readonly bool` par PB, figé avant le premier cœur

`materiel_demande` (mutable : la ligne de commande, le `.cfg`, l'écran, l'outil) ; `materiel` (cent dix
`static readonly bool pb_nn`, copiés de la demande par un initialiseur trivial, qui ne lève jamais) ;
`materiel.figer()` aux trois endroits du § 3.5 ; refus de toute demande postérieure.
- **Mécanisme** : environ 450 lignes (les cent dix champs sont mécaniques).
- **Mode PCem** : rien, si chaque méthode gardée est compilée après le gel et n'est pas en MinOpts (déduit,
  § 4.1, 2 à 5). Les gardes de PB-03 et de PB-51 disparaissent du code : ni duplication ni coût.
- **Preuve du 0 %** : DIRECTE, les listings identiques (M2), en quelques minutes par étape.
- **Pour** : le 0 % démontrable par le code machine ; la même granularité que A pour les outils.
- **Contre** :
  1. il repose sur un comportement d'implémentation de RyuJIT, pas sur un contrat. La porte des listings
     le surveille, et un écart se paie en vitesse seulement ;
  2. un ordre fragile : une méthode chaude compilée avant le gel garde à vie sa lecture et son contrôle
     d'initialisation (sans paliers, aucune recompilation). Le refus des demandes tardives et les
     listings le rendent visible ;
  3. l'IL grossit tout de même (seuils de MinOpts, inliner : § 4.2), d'où la règle de placement ;
  4. ReadyToRun ou NativeAOT en G17 ramènerait chaque garde au coût de A : à remesurer ce jour-là ;
  5. un processus, un mode : un test qui compare les deux modes est deux processus, ce que les portes
     sont déjà. Le banc BenchmarkDotNet doit geler dans `[GlobalSetup]`, un processus par mode (§ 4.1, 14).

### Option C — « Les tables du mode » : des gestionnaires de remplacement, posés au montage

Là où PCem aiguille déjà par table, la correction d'un gestionnaire entier ne se garde pas : on recopie la
table et on remplace son entrée au montage, comme `cpu_set` choisit `ops_286` ou `ops_386`
(`Cpu/cpu.cs:315`, `:374`) et les tables du x87 (`:325-359`). Les variantes vivent dans `*.Materiel.cs` et
appellent les aides de PCem pour tout ce qu'elles ne corrigent pas.
- **Où elle s'applique** : les gestionnaires du x87 (les fichiers générés : la table recopiée, jamais le
  fichier), les opcodes du 286, du 386 et du 486 (`ops_386` est généré), les gestionnaires d'E/S posés par
  `io_sethandler` quand une carte entière change de comportement.
- **Où elle ne s'applique pas** : le `switch` d'`execx86` (le 8088 n'a pas de table), les aides
  (`setadc8`, `x87_ld80`, `clockhardware`, la lecture d'instruction), les boucles d'échantillons et de
  pixels.
- **Coût** : nul par aiguillage, dans les deux modes, quel que soit le stockage du drapeau, qui n'est lu
  qu'au montage. Le prix est la duplication : une variante entière pour une ligne fausse, et deux copies à
  tenir d'accord si l'on corrige plus tard une faute de TRANSCRIPTION dans le gestionnaire de PCem.
- **Ce n'est pas une option autonome** : un complément de A ou de B.

### Écartées

| Forme | Pourquoi |
|---|---|
| `#if MATERIEL`, ou un `const bool` | le coût nul est certain, mais il faut deux binaires, deux constructions par série, et l'utilisateur ne choisit plus au lancement ; avec un `const`, le code mort lève CS0162, une erreur sous `TreatWarningsAsErrors` |
| un générique spécialisé sur une `struct` | gratuit, mais R4 interdit génériques et interfaces |
| une clé par carte dans sa section de device | § 2 : ni le processeur ni la carte mère ne sont des devices ; champ d'instance non pliable ; DEVIATION sur des tables transcrites |
| une variable d'environnement | § 1.1 : elle traverse `par.sh` et ferait basculer des portes en silence |
| `AppContext` et les « feature switches » | appel de méthode, non plié par le JIT (ils ne se plient qu'au découpage de NativeAOT) ; ne se marie pas au `.cfg` ; rien de plus que B |

### Les coûts, côte à côte

| | A | B | B + C |
|---|---|---|---|
| Lignes du mécanisme (avant les corrections) | ≈ 350 | ≈ 450 | ≈ 480 |
| Mode PCem, sites rares | ≈ 0 | 0 | 0 |
| Mode PCem, PB-03 et PB-51 (chaque instruction) | une lecture et un saut par instruction (PB-03 sans échappatoire ; PB-51, ou la boucle d'`exec386` dupliquée) | 0 (vérifié au listing) | 0 |
| Mode PCem, gestionnaires d'opcode et du x87 | une lecture et un saut par exécution de l'opcode | 0 | 0, sans toucher les fichiers générés |
| Preuve du 0 % à chaque étape | bancs statistiques (dizaines de minutes) | listings (minutes) ; bancs si un listing change | idem B, plus l'égalité des tables |
| Dépendance au JIT | aucune | pliage des `static readonly` (déduit) | idem B |
| Fichiers générés | gardes interdites : C ou les générateurs | idem | C les couvre |
| ReadyToRun / NativeAOT (G17) | indifférent | retombe au coût de A | idem B, sauf les tables |
| Deux modes dans un processus | possible | non | non |

---

## 9. Recommandation

**B + C, avec la règle de placement, et deux étages de granularité.**

- **Pourquoi B plutôt que A** : la doctrine du dépôt est « mesuré, pas supposé », et la règle du 0 %
  réclame une preuve. B est la seule forme où le mode PCem se prouve inchangé par le code machine
  lui-même, sans statistique : c'est la preuve que l'annexe de PLAN.md attend de `DOTNET_JitDisasm`. Ses
  deux risques (l'ordre du gel, le comportement non contractuel du JIT) se paient en vitesse et non en
  justesse, et la même porte des listings les détecte. A reste le repli, site par site, partout où un
  listing montrerait que le pliage n'a pas eu lieu, et pour tout G17 qui passerait à ReadyToRun.
- **Pourquoi C en plus** : les trente sites du x87 sont dans des fichiers générés, et la table du 386
  aussi ; C les corrige sans toucher aux générateurs ni à leur rejeu à l'octet près.
- **G13.0, le mécanisme seul, avant toute correction** :
  1. le recensement des 110 PB, avec un marqueur canonique numéroté par site, et sa porte ;
  2. la référence M0 (listings, résumés, comptes d'IL) et le banc M1, sur le patron d'`Is486Banc` ;
  3. la classe `materiel`, le gel, les refus, la clé, l'option, l'écran, la barre de titre, la liste
     d'acceptation d'`iXtal26.Diff`, la ligne de base SST du mode matériel ;
  4. R10 et les amendements de R1, R2, R8 ; l'en-tête de `PCEM_BUGS.md` ;
  5. un PB pilote de bout en bout, choisi pour exercer les deux natures de site : PB-45 (un cas rare dans
     le `switch` du 8088, discriminé par SST) ou PB-01 (une aide appelée par tout ADC et SBB). Le pilote
     éprouve le mécanisme avant que cent neuf autres PB s'y engagent.
- **Puis l'ordre de PLAN.md** (le processeur et le x87, le stockage, la vidéo, le son, la carte mère,
  la section B), une étape par domaine. Une étape par PB coûterait une série entière par PB, soit plus de
  cent heures de machine ; par domaine, une quinzaine de séries.

---

## 10. Ce qu'il faut demander à Julien

1. **La granularité** : un interrupteur pour l'utilisateur, un masque par PB pour les outils ?
2. **Le nom** : `--hardware-mode` et `hardware_mode`, ou `--materiel` et `materiel` ? (un seul nom partout)
3. **La clé du `.cfg`** : oui (et boot-diff la refuse), ou la ligne de commande seule ?
4. **Le stockage** : B, figé au lancement, recommandé ; ou A ?
5. **La règle du 0 %** : s'applique-t-elle au mode PCem PENDANT G13, contre la référence M0 ? Et le mode
   matériel : la seule exigence d'une marge > 1 au `--timer-check` ?
6. **Les vecteurs SST** (l'extérieur) : récupérer le sous-ensemble des manifestes du 8088 (84 formes ;
   726 Mo pour le corpus entier) et du 386 (941 formes). Sans eux, ni la reproduction de la ligne de base
   ni le mode matériel du processeur ne se vérifient contre le silicium.
7. **La section C** : hors du mode (nettoyage, seulement si la série est identique), ou dans le mode pour
   les entrées qui changent un compte de cycles ? PB-11 et PB-12 sont « invisibles sans comparaison à du
   silicium » mais pas sans effet : PB-03 impute −5,87 ppm de l'INT 8 à PB-11. Ce que la section C
   contient de mesurable relève, en toute rigueur, du mode matériel.
8. **Les témoins extérieurs** du processeur (par exemple une suite de tests du 386 en ROM, à récupérer),
   ou nos seuls bancs ?
9. **Le défaut offert au public**, en G17 : PCem ou matériel ?

---

## Annexe A — Références

**Dépôt** (commit `bc609ce`) : `iXtal26/iXtal26.csproj:34` ; `tools/iXtal26.Diff/iXtal26.Diff.csproj:23` ;
`tools/perfbanc/iXtal26.PerfBanc.csproj:20`, `Program.cs`, `Is486Banc.cs` ;
`iXtal26/Models/model.cs:179, 413-418, 426, 443` ; `tools/iXtal26.Diff/Program.cs:131-141` ;
`tools/oracle/harness.c:529, 2378-2380` ; `iXtal26/pc.cs:424` (`loadconfig`) ;
`iXtal26/PluginApi/config.cs:10-15` ; `tools/iXtal26.Diff/BootDiff.cs:345-350, 427, 592-620` ;
`iXtal26/Host/SdlSetup.cs:47-50, 809-895, 1151` ; `iXtal26/Host/SdlHost.cs:30-38, 214-255` ;
`iXtal26/Cpu/808x.cs:182, 280-292, 571-696, 3070, 3098, 3508` ; `iXtal26/Cpu/386.cs:342-380` ;
`iXtal26/Cpu/cpu.cs:315, 325-359, 374` ; `iXtal26/Cpu/x86.cs:347` ; `tools/x87gen/README.md` ;
`iXtal26/Cpu/x87_ops_808x.cs:9-14` ; `iXtal26/Cpu/386_ops_table386.cs:6-7` ;
`tools/gates/par.sh`, `series.sh`, `README.md` ; `VERIFICATION.md` § M5.1 et § G10.0 ;
`iXtal26/Docs/audit-performance-2026-09-26.md` (P10) ; `TRANSCRIPTION.md` (R1 à R9, R3 : 240 lignes) ;
`PCEM_BUGS.md`, lignes 8-10 et « Portée ».

**dotnet/runtime, branche `release/10.0`, lue le 06/10/2026** (numéros de ligne de ce jour) :
- `src/coreclr/jit/importer.cpp` : `impImportStaticReadOnlyField` (≈ 3802-3836) ; son appel sur
  `CORINFO_FIELD_STATIC_ADDRESS` avec `CORINFO_FLG_FIELD_FINAL` (≈ 9483-9493) ; `impInitClass`
  (≈ 3769-3800) ; le pliage de `CEE_BRTRUE`/`CEE_BRFALSE` (≈ 7705-7811).
- `src/coreclr/vm/jitinterface.cpp` : `CEEInfo::getStaticFieldContent` (≈ 12123-12151) ; `CEEInfo::initClass`
  (≈ 3776-3925) ; l'accesseur des champs statiques (≈ 1506-1517).
- `src/coreclr/vm/methodtable.h` : `IsClassInitedOrPreinited` (≈ 1179-1181).
- `src/coreclr/jit/compiler.cpp` : `compSetOptimizationLevel` (≈ 3558, seuils ≈ 3714-3716 ; appel ≈ 7030,
  après `fgFindBasicBlocks`) ; `src/coreclr/jit/compiler.h` : `DEFAULT_MIN_OPTS_*` (≈ 9913-9917),
  `DEFAULT_MAX_INLINE_SIZE` = 100 (≈ 11253).
- `src/coreclr/jit/fgbasic.cpp` : constantes du pré-examen (≈ 937-943), branche pliable (≈ 1870-1883) ;
  `src/coreclr/jit/inline.h` : `ALWAYS_INLINE_SIZE` = 16 (≈ 1096).
- `docs/design/features/tiered-compilation.md` (définition du Tier1) ;
  `docs/design/coreclr/jit/viewing-jit-dumps.md` (options de désassemblage du runtime livré).

**learn.microsoft.com** : *Compilation config settings* (`/dotnet/core/runtime-config/compilation`) ;
`FieldInfo.SetValue` (`/dotnet/api/system.reflection.fieldinfo.setvalue`, Remarques) ;
`RuntimeHelpers.RunClassConstructor` (`/dotnet/api/system.runtime.compilerservices.runtimehelpers.runclassconstructor`).
