# R2 — Ce qui dépend des noms, et la preuve que seuls des noms changent

*Reconnaissance de G14, le 23 octobre 2026, au commit 1d49a7f. Lecture seule : rien n'a été construit ni joué.*
*Niveaux : **vérifié** (relu ou compté), **déduit** (suit de ce qui est relu), **inconnu**.*

Les modules lus : `Models/pic.cs`, `pit.cs`, `dma.cs`, `serial.cs`, `ali1429.cs`, `neat.cs`, `headland.cs`,
`opti495.cs` ; `ppi.cs`, `rtc.cs` ; `Devices/nvr.cs` ; `Keyboard/keyboard_at.cs` (le 8042) ; `Sound/sound_opl.cs`,
`dbopl.cs`, `sound_dbopl.cs`, `sound_emu8k.cs`, `sound_mpu401_uart.cs` ; `Video/vid_unk_ramdac.cs`,
`vid_tkd8001_ramdac.cs` ; les mélangeurs (`sb_ct1335_mixer_t`, `sb_ct1345_mixer_t`, `sb_ct1745_mixer_t`, dans
`Sound/sound_sb.cs:38-73`) ; le CRTC. Les comptes viennent de scripts jetables écrits hors du dépôt (Python `-I`,
dans le répertoire de travail de la séance) ; leur expression régulière est donnée avec chaque chiffre.

## 0. En bref

1. Ni le harnais ni les sondes ne désignent une classe, un fichier ou un namespace C# par son nom : leurs chaînes
   (`h_state`, `Fuzzer.Compare`, `PitFields`, `VgaProbe.Fields`, les clés `R9.Garde`) nomment le **C** (vérifié).
2. Les sorties comparées des portes n'en contiennent aucun : 0 `.cs` et 0 `iXtal26.<x>` dans les 1 653 journaux
   des deux séries de GR.1i2, ni dans les `.tsv`, les `sst*-baseline*.tsv` et les attendus des bancs (vérifié).
3. La réflexion par chaîne existe (cinq sites), mais aucune ne vise un module de puce (vérifié).
4. Ce qui dépend vraiment des noms : `using static` et les références qualifiées (le compilateur les suit), les
   globs du `.editorconfig` par répertoire et par fichier, la colonne `csharp_path` d'`oracle.tsv` (32 lignes),
   et les listings du JIT, clés et corps (154 méthodes des modules et 50 autres, sur 1 507).
5. Un remplacement textuel est dangereux ici : `PIT pit` en paramètre (15 dans `pit.cs`), `serial`, `nvr`, et des
   chaînes PCem (`ami386dx_opti495.nvr`, `"dma"`, `"serial1"`, `r9-emu8k`, `sound_emu8k.c:1164`).
6. La solution Rider ne contient ni `tools/iXtal26.Diff` ni `tools/perfbanc` : un refactor Rider n'y touche pas.
7. Preuves : le diff inverse (précédent 9530e21) et `listings-jit.sh` existent ; la comparaison des listings exige
   une table des noms qu'elle n'a pas ; aucune comparaison d'IL n'existe ; aucun comparateur de journaux n'est commité.
8. Les captures du JIT de G13 sont sous .NET 10.0.12 ; la machine a 10.0.13 : une référence est à refaire.
9. Docs : 51 renvois vivants dans PCEM_BUGS, 11 dans `Docs/ibm5150/` (contrôlés par `check-doc5150.sh`), 3 dans
   TRANSCRIPTION ; 37 dans VERIFICATION et 18 dans les plans clos, de l'historique ; aucune règle écrite pour lui.
10. Décisions ouvertes : la portée (conteneurs seuls ?), les suffixes `pic_`/`pit_`, le CRTC, le harnais, l'outil.

## 1. L'état, point par point

### 1.1 Les modules et ce qui porte leur nom

Chaque module est un conteneur `internal static (partial) class` au nom du fichier C, dans le namespace de son
répertoire (TRANSCRIPTION.md:118 et :132 ; les 23 namespaces suivent les répertoires, `grep -rn "^namespace"`).

| Module | Conteneur | `using static` | Réf. cœur (fich.) | Réf. Diff (fich.) | `.Materiel.cs` | oracle.tsv |
|---|---|---|---|---|---|---|
| `Models/pic.cs` | `pic` (+ `PIC`) | 13 | 39 (20) | 62 (2) | oui | 2 |
| `Models/pit.cs` | `pit` (+ `PIT`, `PIT_nr`) | 4 | 352 (28) | 6 (1) | oui | 1 |
| `Models/dma.cs` | `dma` (+ `dma_t`) | 4 | 7 (6) | 32 (1) | oui | 3 |
| `ppi.cs` | `ppi_c` (+ `PPI`) | 4 | 5 (5) | 0 | non | 1 |
| `rtc.cs` | `rtc`, `rtc_h` | 2 | 7 (1) + 2 (2) | 0 | non | 1 |
| `Devices/nvr.cs` | `nvr` | 0 | 14 (1) | 0 | non | 1 |
| `Keyboard/keyboard_at.cs` | `keyboard_at` (+ `keyboard_at_t`) | 2 | 3 (3) | 24 (1) | oui | 2 |
| `Models/serial.cs` | `serial` (+ `SERIAL`) | 0 | 100 (4) | 0 | non | 2 |
| `ali1429`, `neat`, `headland`, `opti495` | même nom | 0 | 1 (1) chacun | `neat` 1 | non | 1 chacun |
| `Sound/sound_opl.cs` | `sound_opl` (+ `opl_t`) | 3 | 3 (3) | 0 | non | 1 |
| `Sound/dbopl.cs` | `DBOPL` | 0 | 3 (1) | 7 (1) | non | 3 |
| `Sound/sound_dbopl.cs` | `sound_dbopl` | 1 | 1 (1) | 0 | non | 1 |
| `Sound/sound_emu8k.cs` | `sound_emu8k` (+ `emu8k_*`) | 0 | 6 (3) | 42 (4) | oui | 3 |
| `Sound/sound_mpu401_uart.cs` | `sound_mpu401_uart` | 1 | 5 (2) | 2 (1) | oui | 3 |
| `Video/vid_unk_ramdac.cs` | `vid_unk_ramdac` | 1 | 1 (1) | 0 | oui | 2 |
| `Video/vid_tkd8001_ramdac.cs` | `vid_tkd8001_ramdac` | 1 | 1 (1) | 0 | non | 2 |

Comptes (vérifiés) : `using static` = lignes `^\s*using static <ns>.<classe>;` dans `iXtal26/`, `tools/`,
`iXtal26.SoftFloat/` ; « réf. » = `(?<![\w.])<classe>\.(?!(c|cs|h|cc|cpp)\b)[A-Za-z_]` ou un `using static`, sur
le code dont les commentaires `//` et les chaînes sont retirés, fichier de la classe compris ; `perfbanc` : 0 pour
tous. `oracle.tsv` : `grep -cP '^iXtal26/<chemin>(\.Materiel)?\.cs\t'`, 32 lignes en tout.

- **Le CRTC n'est pas un module** (vérifié) : `crtc[]` vit dans l'état de chaque carte (`grep -rln 'crtc\['
  iXtal26/Video` rend 20 fichiers) ; le seul nom propre est `Masque6845`, classe imbriquée de
  `Video/vid_mda.Materiel.cs:38`. Les mélangeurs non plus : trois classes dans `sound_sb.cs`, sans conteneur.
- **Les collisions conteneur/membre** ont produit des noms qui n'ont que cette raison (vérifié) : `pic_`
  (`pic.cs:35`), `pit_` (`pit.cs:79-81`, CS0542), `keyboard_at_` (`keyboard_at.cs:120-122`), et le conteneur
  `ppi_c` (`ppi.cs:23`, l'instance s'appelle `ppi`). TRANSCRIPTION.md:181-185 dit « le conteneur prend `_c`, le
  membre garde son nom » ; `pic`, `pit` et `keyboard_at` ont fait l'inverse.
- **Les paramètres homonymes** (vérifié) : 15 déclarations `PIT pit` dans `pit.cs` (le `struct PIT *pit` du C,
  `pit.cs:131`, `:181`…), une dans `pit.Materiel.cs` ; `SERIAL serial` dans `serial.cs` (7) et `mouse_serial.cs`
  (3) ; des locales `nvr` (`nvr.cs`, 6). Dans ces méthodes, `pit.l[0]` est le paramètre ; ailleurs, `pit.pit_` est
  la classe. Commande : `grep -rcP '\b\w+\s+(pic|pit|dma|serial|nvr|rtc|keyboard_at)\s*[,)=;]'`.
- **Les sept partielles du mode matériel** (`pic`, `pit`, `dma`, `keyboard_at`, `vid_unk_ramdac`, `sound_emu8k`,
  `sound_mpu401_uart`) s'attachent par namespace et nom : `namespace iXtal26.Models;` puis
  `internal static partial class pic` (`pic.Materiel.cs:15-17`, de même pour les six autres). Aucune n'a de champ
  statique initialisé au niveau de la classe (vérifié : leurs états sont dans des classes imbriquées, `Etat8259`,
  `Etat8042`), et `pic.Materiel.cs:19-23` dit pourquoi : un initialiseur de plus change le constructeur statique et
  le code du JIT. Un renommage qui oublierait une moitié laisserait deux classes ; leurs méthodes privées étant
  appelées par les gardes de l'autre moitié, le build tomberait (déduit).
- **Le gel de `materiel.cs`** ne dépend d'aucun nom de module : les gardes lisent `materiel.pb_nn`
  (`materiel.cs:7-13`), `RunClassConstructor(typeof(materiel).TypeHandle)` est suivi par le compilateur
  (`ModeMateriel.cs:348`), et les domaines de `ModeMateriel.Corrections` sont des mots (`"video"`, `"son"`), pas
  des classes (`ModeMateriel.cs:41-105`) (vérifié).
- **InternalsVisibleTo** ouvre trois assemblées par leur nom (`iXtal26.csproj:70-73`), aucun type (vérifié).
  `GlobalUsings.cs` ne porte que les alias stdint ; aucun autre alias `using X = …` (vérifié par
  `grep -rnP '^\s*(global )?using \w+ = '`).

### 1.2 Le harnais et les sondes : des noms du C, pas du C#

- `h_state` (`tools/oracle/harness.h:95-330`) et son miroir `HState` (`iXtal26/Diag/HState.cs:46`) ont les
  champs des globales du C (`n_picint`, `memcycs`…) ; le C# le remplit par des accès typés
  (`Cpu/808x.State.cs:591-593`), et `Oracle.CheckAbi` n'assène que la taille (vérifié).
- Les étiquettes comparées sont des chaînes du C : `Fuzzer.cs:1342-1360` (`Chk("n_picint", …)`),
  `BootDiff.cs:1779` (`PitFields`), `BootDiff.cs:2090` (les champs du 765), `VgaProbe.cs:26-60` (chemins de
  `svga_t`, `ramdac.state`…), les sondes du son (`BootDiff.cs:1813-1825`, `"opl0.addr"`, `"mixer.regs"`) et du DMA
  (`BootDiff.cs:1898`, `dma[c].ab|ac`). La règle est déjà écrite : `Floppy/fdc.cs:48-49` et la leçon 2 de
  VERIFICATION.md:1550-1554 (« les renommer par cohérence aurait fait mentir la sonde »). Elles ne changent pas.
- Les sondes C# s'appellent par le compilateur : `iXtal26.Models.pit.Probe(t, cs)` (`BootDiff.cs:1795`, `:2217`),
  les `Probe` d'EMU8000 et du DMA de même (vérifié par lecture des sites cités par `oracle.tsv`, lignes `host`).
- Les liaisons natives (`Oracle.cs:156…`, 98 `DllImport`) nomment des symboles C `h_*` ; les clés des gardes R9
  sont des fichiers C (`Diag.R9.Garde("sound_emu8k.c:1164")`, `sound_emu8k.cs:1141` ; 58 gardes, toutes `*.c`,
  `grep -rhoP 'R9.Garde\("[^"]*"\)'`) (vérifié).
- Le hachage de trace (`BootDiff.cs:913-932`, FNV-1a) porte sur des valeurs, jamais sur des noms (vérifié).

### 1.3 Les sorties des portes

- `tsv` de série : 0 `.cs` dans `gr1i2.tsv` et `gr1i2-tout.tsv` (828 lignes chacun) ; un seul nom de module,
  « DBOPL », littéral du message d'`OplTablesCheck.cs:45`, qui ne se calcule pas du type (vérifié, `grep -cw`).
- Journaux par porte (`par.sh:110-112`, stdout et stderr) : dans les 826 journaux de `gr1i2` et les 827 de
  `gr1i2-tout` (`/tmp/a9/series/`), 0 `.cs`, 0 `iXtal26.<x>` ; les mots `dma`, `nvr`, `serial`, `emu8k` qui y
  paraissent sont des clés de configuration, des répertoires, des fichiers C et des noms de porte (vérifié :
  `grep -cw`, puis lecture des lignes). Les messages d'erreur citent `e.GetType().Name`, des exceptions du
  système ; seule une pile (`Fuzzer.cs:509`) citerait une classe et un fichier C#, sur un rouge seulement.
- `sst*-baseline*.tsv` : aucun `.cs` ; la ligne d'en-tête nomme l'outil `iXtal26.Diff`. `oracle.tsv` : ancré sur
  les chemins C# en première colonne, sur les lignes **C** (`c_lines`), jamais sur des lignes C# (vérifié).
- Attendus des bancs (`tools/gates/bancs/*.attendus`, 32) : deux commentaires citent `vid_mda.cs:306` et
  `vid_hercules.cs:360`, hors des modules visés ; aucun autre nom C# (vérifié, `grep -lE '\.cs\b|iXtal26\.[A-Z]'`).
- **Le bruit des journaux** : sur 805 journaux comparés entre `gr2d1` et `gr1i2` (le nom de série et les PID
  normalisés par `sed`), 14 diffèrent ; causes lues : le temps de l'hôte (`materiel-cas-pb03`, « temps hôte de la
  fenêtre »), les PID tronqués (`materiel-mode`), la ligne d'isogen (`r9-cue`, `r9-atapi`…), et les vrais
  changements de GR.1i2 (vérifié). VERIFICATION.md:6481-6486 consigne déjà ce genre d'écarts à la main. Aucun
  comparateur de journaux n'est commité : les scripts de série vivent hors du dépôt
  (`/var/tmp/ixtal-g13/a9-scripts/serie.sh`) (vérifié).

### 1.4 La réflexion, les chaînes et les clés dans iXtal26/ et ses outils

- **Réflexion** (vérifié, `grep -rn 'BindingFlags\|GetField(\|GetMethod('`) : `Cpu/386.Materiel.cs:314`
  (`_386.ILLEGAL`), `MaterielCasPit.cs:52-55` (`sound_speaker.speaker_pos`, `speaker_buffer`),
  `MaterielCasFrequence.cs:85` (champs de `sound_sb`), `MaterielCas.cs:513` (`_808x.memcycs`),
  `perfbanc/GardeBanc.cs:93` (`_808x.setadc8`). Le type y est un `typeof`, suivi ; le **membre** est une chaîne,
  non suivie. Aucun ne vise un module de puce.
- `nameof` : 0 occurrence ; `typeof` hors réflexion : `materiel`, des types d'outillage (vérifié).
- **Chaînes qui ressemblent à des noms de module** et ne doivent pas bouger (vérifié, lecture) :
  `NvrImage.cs:261` (`"ami386dx_opti495.nvr"`, nom de fichier de PCem), `sound_sb.cs:1205…1734` (la clé `"dma"`),
  `model.Materiel.cs:47-48` et `R9PortsCfg.cs:29-50` (`"serial1"`), `ModeOutils.cs:29` (la porte `"r9-emu8k"`),
  les clés R9 (`"sound_emu8k.c:…"`), `Video/vid_s3.cs:1393` (`"vid_s3: sdac_ramdac_in…"`), `mouse_serial.cs:111`.
- **Clés de configuration** : les sections et clés des `.cfg` et de `SdlSetup` sont les noms internes de PCem
  (`"sb16"`, `"aha1542c"`, `"mfm_xebec"`), lus sans réflexion (vérifié par la recherche ci-dessus ; déduit pour
  l'absence de tout lien type → clé). Les commandes de `Program.cs` (`emu8k-kernel-check`, `opl-tables-check`,
  `nvr-check`) nomment les puces ou le C, pas les classes.
- **`.editorconfig`** (vérifié) : la neutralisation des règles de style du code transcrit tient à une liste de
  **répertoires**, `[**/{Cpu,Memory,Models,Keyboard,Video,PluginApi,Diag,Devices,Sound,Flash,Floppy,Disc,Mouse}/**.cs]`
  (`.editorconfig:40`), et à des **noms de fichier**, `[**/{io,timer,pc,ppi}.cs]` (`:97`). Un `Ics/` absent de la
  liste perd CS0649 et CS8981 à `none` : avec `TreatWarningsAsErrors` (`iXtal26.csproj:38-39`), le build tombe
  (déduit). Le commentaire de CS8981 (`:47-53`) cite `pit, pic, dma` comme conteneurs en minuscules.
- `.editorconfig:90-91` annonce : « G14 et G15 les renommeront avec le reste » (la classe `materiel` et ses
  `pb_nn`). PLAN.md § G14 ne le dit pas (voir l'annexe).

### 1.5 Les outils qui citent des chemins C# ou des noms de méthodes

- **`tools/listings-jit.sh`** (vérifié) : `compare` découpe les listings sur l'en-tête « `; Assembly listing for
  method NOM` » (`:188`) et compare **par nom qualifié** (`:203`). Le nom est `iXtal26.Models.pic:picint(ushort)` ;
  les corps citent aussi les appels (`call [iXtal26.Models.pic:picint(ushort)]`) et les types des signatures ; les
  adresses sont masquées (`JitDisasmDiffable`, `:169`), les chaînes littérales non. Sur la capture `M2-13b`
  (30 scénarios, 1 507 méthodes distinctes), 154 méthodes appartiennent aux types des modules visés et 50 autres en
  citent un dans leur corps ou leur signature (script jetable sur la découpe de `:181-196`, motif
  `<ns>.<type>(?=[:+\],)( ]|$)`). Renommés, ces 154 sortiraient « disparues » et « apparues », les 50 « changées » :
  `compare` rendrait 1 sans rien prouver. Il faut une table des noms (§ 2).
- Les scénarios ne compilent ni `neat` ni `headland` (0 en-tête) ; `ppi_c` une méthode : les listings ne prouvent
  rien pour ce qu'ils ne compilent pas (vérifié).
- Toutes les captures de G13 (`/var/tmp/ixtal-g13/M0-860f2b1`, `M2-*`) sont sous `Microsoft.NETCore.App 10.0.12`
  (`runtime.txt`) ; la machine a 10.0.13 (`/usr/lib/dotnet/shared/Microsoft.NETCore.App/`) (vérifié). Le script
  le dit : une capture se compare au même runtime (`:15-16`).
- **`tools/check-oracle.sh`** : signale `ABSENT C#` quand un `csharp_path` n'existe plus (`:49-53`) ; ne voit pas un
  fichier C# sans ligne : 39 fichiers d'`iXtal26/` n'en ont pas, dont `pit.Materiel.cs`, `sound_emu8k.Materiel.cs`,
  `sound_mpu401_uart.Materiel.cs`, `vid_unk_ramdac.Materiel.cs` (vérifié, boucle `find` × `grep "^$f\t"`). Il
  hache aussi les fichiers du harnais (22 lignes `tools/oracle/…`) : en éditer un commentaire le fait dériver.
- **Le harnais** cite deux fichiers C# visés en commentaire : `harness_stubs.c:172` (`keyboard_at.cs`) et
  `harness.c:2092` (`nvr.cs`) (vérifié).
- **Les commentaires du code** citent les fichiers visés 29 fois dans 24 fichiers d'`iXtal26/` (2 avec ligne), 3
  fois dans `tools/iXtal26.Diff` (vérifié, motif `(?<![\w.])(?:[\w./]*/)?(pic|pit|…)(\.Materiel)?\.cs(?!\w)`).
- **`tools/gates/`** : `series.sh` et `par.sh` ne citent aucun chemin C# hors un commentaire (`series.sh:1019`) ;
  les noms de porte (`picbanc`, `r9-emu8k`) ne sont liés à aucune classe (vérifié).
- **`Recensement.cs:60`** lit tous les `iXtal26/**/*.cs` par glob et ne cite un fichier que sur un rouge ; vert, sa
  ligne est un compte (`gr1i2.tsv`, « 294 défauts, 1281 marqueurs ») (vérifié).
- **Les générateurs** (`tools/x87gen/*.py`, `ops386-table.py`) écrivent des chemins fixes de `Cpu/`, hors des
  modules visés (vérifié).
- **`tools/perfbanc`** : aucune référence aux modules visés ; ses bancs appellent `sound`, `sound_sb`, `vid_ega`,
  `vid_svga`, les cœurs ; les noms des bancs sont ceux de leurs classes à eux (vérifié). La réflexion de
  `GardeBanc.cs:93` vise `_808x`.
- **La couverture de GR** : le réécrivain (`/tmp/a9/cov/rewriter/Program.cs`, copie dans
  `/var/tmp/ixtal-g13/a9-scripts/cov/`) est **syntaxique** (`CSharpSyntaxTree.ParseText`, sans modèle sémantique),
  sur `Microsoft.CodeAnalysis.CSharp` 4.14.0 ; sa carte cite des noms de fichier (`carte.tsv` : `vid_mda.cs 58
  méthode mdacol`). Il n'est pas dans le dépôt (vérifié).
- **`check-doc5150.sh`** contrôle les renvois `chemin.cs#symbole` de `Docs/ibm5150/` (`:5-18`) : un fichier visé
  renommé y rougit (déduit de la règle ; 11 renvois aux modules visés, § 1.6).

### 1.6 Les documents

Comptes (vérifiés) : motif `(?<![\w.])(?:[\w./]*/)?(pic|pit|dma|ppi|rtc|nvr|keyboard_at|serial|ali1429|neat|
headland|opti495|sound_opl|dbopl|sound_dbopl|sound_emu8k|vid_unk_ramdac|vid_tkd8001_ramdac|sound_mpu401_uart)
(\.Materiel)?\.cs(?!\w)`, `grep -oP … | wc -l` ; « avec ligne » : le même suivi de `:\d`.

| Document | Renvois (avec ligne) | Nature | Par module |
|---|---|---|---|
| `PCEM_BUGS.md` | 51 (0) | vivant : *Reproduit* | pic 13, dma 16, emu8k 13 ; pit, kbd, ramdac, mpu 2 ; nvr 1 |
| `Docs/ibm5150/` | 11 (0) | vivant, contrôlé par `check-doc5150.sh` | dma 4, pit 3, pic, ppi, serial, nvr 1 |
| `TRANSCRIPTION.md` | 3 (1) | règles et omissions, vivants | ppi `:100`, kbd `:222`, `nvr.cs:78-103` `:223` |
| `iXtal26/README.md` | 1 (0) | vivant | ppi `:347` |
| `THIRD_PARTY_NOTICES.md` | 2 (0) | notice, vivante | dbopl, sound_dbopl `:50` |
| `oracle.tsv` | 32 lignes | manifeste, vivant, contrôlé par `check-oracle.sh` | — |
| `VERIFICATION.md` | 37 (5) | journal daté, historique | pit 7, dma 6, kbd 6, ramdac 6, pic 3… |
| `PLAN-*.md` clos | 18 (4) | historique (286, G1, G3, G9, G12, PS2) ; `PLAN.md` : 0 | — |
| `Docs/G13-`, `GR-reconnaissance/` | 45 + 11 | rapports datés, historique | — |
| `pourquoi-on-corrige.md` | 0 | ne cite aucun fichier C# | — |

Ce qu'en dit le dépôt (vérifié) :
- Aucune règle écrite ne dit comment traiter l'historique. La pratique de VERIFICATION.md est d'annoter, pas de
  récrire : « ~~`model = ibmxt`~~ **périmé depuis § M10** » (`:1445`), « **Périmé par § M11.** » (`:1645`).
- Deux règles contre les numéros de ligne C# : `check-doc5150.sh:15-18` (« un numéro périmé se lit comme s'il était
  juste ») et `Recensement.cs:8` (« jamais un numéro de ligne, qui dérive »). Un cas réel : `fdc.cs:48` et
  VERIFICATION.md:1550 citent `BootDiff.cs:264`, où les étiquettes ne sont plus ; elles sont à `BootDiff.cs:2090`.
- TRANSCRIPTION.md fixe ce que G14 contredit : R1(e) réserve `// noms:` à `Floppy/` et `Disc/` (`:23-29`) ; la
  convention « globales d'un fichier → classe du même nom » (`:118`) ; « Fichiers : miroir du chemin C » (`:132`) ;
  le nommage explicite « `Floppy/` et `Disc/` seulement » (`:153-165`) ; la liste de la portée (`:98-101`). Le
  plafond de R3 est 260 lignes (`:48`) ; le fichier en a 237 (`wc -l`) : 23 de marge.

### 1.7 La mécanique du renommage, et ce qui existe pour la preuve

**Les outils de renommage** :
- **Rider** : la solution `iXtal26.sln` ne contient que `iXtal26` et `iXtal26.SoftFloat` (vérifié, `grep
  '^Project'`) ; Rider est ouvert dessus et son serveur MCP répond (vérifié : `get_solution_projects` rend ces deux
  projets). Il expose `rename_refactoring` (avec `preview`) et `move_type_to_namespace` ; non essayés (lecture
  seule). Ses renommages n'atteindraient pas `tools/iXtal26.Diff` (62 références à `pic`, 42 à `sound_emu8k`…) ni
  `tools/perfbanc` : le compilateur les trouverait, à corriger ailleurs (déduit). Son traitement des commentaires
  et des chaînes est inconnu.
- **Roslyn** : `Microsoft.CodeAnalysis.CSharp` et `.Common` 4.14.0 sont dans le cache NuGet (vérifié,
  `~/.nuget/packages`), pas `Microsoft.CodeAnalysis.Workspaces` ni `Microsoft.Build.Locator` : pas de `Renamer`
  prêt. Un script peut monter une `CSharpCompilation` (sources et références du SDK) et renommer les nœuds dont le
  **symbole** est la classe visée, jamais une chaîne ni un commentaire ; c'est le patron du réécrivain de GR plus un
  modèle sémantique (déduit).
- **`git mv` + remplacement textuel** : le précédent est 9530e21 (`cpu` → `cpu_c`, 21 fichiers). Ici il buterait
  sur les paramètres `PIT pit` (§ 1.1), les chaînes de PCem (§ 1.4) et les commentaires qui citent `pit.c` à
  distinguer de `pit.cs` ; VERIFICATION.md:1543-1549 le dit déjà (« `sed` ne voit pas ce que le compilateur voit »).

**Les preuves, ce qui existe** :
- **Sources, le diff inverse** : 9530e21 l'a fait (« les 220 lignes retirées et ajoutées sont identiques une fois
  `cpu_c.` ramené à `cpu.` ») ; M8.1 aussi (VERIFICATION.md:1528-1541). Aucun script commité : la méthode est
  manuelle (vérifié, `git show -s 9530e21`).
- **IL** : rien. `ilspycmd -il` a servi en § M5.1 (VERIFICATION.md:853) mais n'est pas installé (`which`, `dotnet
  tool list -g`, `~/.nuget/packages` : vérifié). `System.Reflection.Metadata` est dans le runtime
  (`/usr/lib/dotnet/shared/Microsoft.NETCore.App/10.0.13/`), sans rien installer (vérifié). L'ordre des fichiers
  dans la compilation, donc celui des `TypeDef` et des jetons, peut bouger quand des fichiers changent de
  répertoire : inconnu (l'ordre du glob `Compile` n'est pas documenté dans le dépôt). Une comparaison d'octets
  bruts échouerait alors sur les jetons ; il faut les résoudre en noms.
- **JIT** : `listings-jit.sh capture/compare`, sans table des noms (§ 1.5).
- **Séries** : `par.sh` et la comparaison journal par journal, faite à la main (§ 1.3).
- **La performance** : `tools/perfbanc` ne nomme aucun module visé : un renommage ne change ni ses bancs ni leurs
  noms (vérifié) ; la référence figée de G15 est l'affaire d'une autre lecture.

## 2. Ce qu'il faut faire, et les étapes proposées

La table des noms est le pivot : **une seule** table, `ancien namespace.type → nouveau namespace.type` et
`ancien chemin → nouveau chemin`, versionnée, sert aux quatre preuves et aux tables `// noms:` (déduit).

**G14.0 — Le repère, au commit de départ, sous le runtime du jour.**
- Contenu : la capture des listings (`listings-jit.sh capture`, 30 scénarios) sous 10.0.13 ; le vidage normalisé
  de l'IL de `iXtal26.dll` (G14.1) ; les deux séries (mode PCem, `MATERIEL=tout`) ; `sha256sum` de
  `libixtal26oracle.so` ; les comptes de § 1.1.
- Preuve : deux captures du même commit rendent `compare` à 0 ; deux vidages d'IL identiques ; deux séries du même
  commit, comparées par le normaliseur, donnent la liste du bruit (temps hôte, PID, isogen).
- Contrôle négatif : une capture d'un commit qui change une ligne vivante rend `compare` à 1.

**G14.1 — L'outillage de la preuve, sans rien installer.**
- Contenu : `listings-jit.sh compare REF AUTRE [TABLE]`, qui réécrit les noms de REF par la table, en en-tête et en
  corps, aux bornes de `<ns>.<type>` (`:`, `+`, `]`, `,`, `)`) avant de comparer ; un vidage d'IL par
  `System.Reflection.Metadata` (une commande d'`iXtal26.Diff` ou un script hors du cœur) : par méthode, la
  signature, les drapeaux, les locales, les blocs d'exception et l'IL décodé, jetons résolus en noms ; par type, les
  champs dans l'ordre, la disposition, les attributs ; un normaliseur de journaux (nom de série, PID, lignes de
  temps hôte) pour « identiques à l'unité ».
- Preuve : sur le repère, `compare` avec une table vide rend 0 ; IL identique à lui-même.
- Contrôles négatifs : une table fausse d'une entrée fait apparaître ses méthodes ; un `+1` dans `pic_read`
  rougit l'IL et le listing de `pic_read` seuls ; un nom de série laissé dans un journal le rougit.

**G14.2 — La doctrine, sans code.**
- Contenu : TRANSCRIPTION.md (R1(e) étendue à `Ics/`, la ligne « Fichiers » et « Globales d'un fichier », la
  portée, les collisions), dans la marge de R3 ; `.editorconfig` : `Ics` dans le glob de `:40` (et `ppi` hors de
  `:97` quand `ppi.cs` part).
- Preuve : le build sans avertissement ; l'IL identique au repère (aucun code ne bouge).

**G14.3 et suivantes — Un module, ou une famille, par commit.**
- Contenu : `git mv` vers `Ics/` ; le renommage du conteneur et de son namespace par le compilateur (§ 4, D6) ; la
  table `// noms:` en tête ; `oracle.tsv` (`csharp_path`) ; les renvois **vivants** (§ 1.6) ; `.Materiel.cs` avec
  sa classe.
- Preuves, à chaque commit : build Release et `iXtal26.Diff` sans avertissement ; le diff inverse vide hors des
  tables `// noms:`, des en-têtes et des renvois de documents ; l'IL identique au repère **par la table** ;
  `listings-jit.sh compare` à 0 par la table ; `check-oracle` 0 dérive 0 absent ; `recensement` ;
  `check-doc5150.sh` ; les séries entières identiques au repère après normalisation ; `sha256` de la `.so`
  inchangée.
- Contrôles négatifs : le renommage d'une partielle oublié fait tomber le build ; un `csharp_path` laissé rend
  `ABSENT C#` ; un renvoi `Models/pit.cs#pit_write` laissé rougit `check-doc5150.sh` ; une entrée de table manquante
  rougit la comparaison d'IL.

## 3. Les risques

1. **Le remplacement textuel qui compile et rebranche** : `pit.l[0]` sur le paramètre `pit` réécrit en
   `Intel8253.l[0]` ne compile pas aujourd'hui, mais un homonyme qui aurait le même membre lierait autre chose sans
   diagnostic. Seule la comparaison d'IL par la table l'attrape partout ; les listings ne couvrent que 1 507
   méthodes, et ni `neat` ni `headland`.
2. **Les chaînes et les commentaires de PCem touchés** : `ami386dx_opti495.nvr`, `"dma"`, `"serial1"`, `r9-emu8k`,
   `sound_emu8k.c:…`, `pit.c:20` dans les marqueurs `// pcem:`. Un remplacement qui ne distingue pas `pit.c` de
   `pit.cs` casse le lien vers `pcem-dev/` sans qu'aucune porte ne le voie (déduit ; `check-oracle` lit le manifeste,
   pas les marqueurs).
3. **Les outils hors de la solution** : un refactor Rider laisse `tools/iXtal26.Diff` (et `perfbanc`) ; le build
   les signale, mais une correction à la main y réintroduit le risque 1.
4. **Le runtime** : un `apt` qui passe de 10.0.13 à 10.0.14 en cours de G14 invalide le repère des listings
   (c'est arrivé entre les captures de G13 et aujourd'hui, § 1.5). Le contrôle : `runtime.txt` égal à chaque capture.
5. **Le bruit des journaux** pris pour un écart, ou l'inverse : sans normaliseur commité, « identiques à l'unité »
   repose sur une lecture humaine des 14 sortes d'écarts de § 1.3.
6. **L'ordre de compilation** (inconnu) : s'il change l'ordre des types, un vidage d'IL naïf rougit à tort, et
   l'ordre d'initialisation des partielles pourrait bouger s'il y avait des initialiseurs (il n'y en a pas, § 1.1).
7. **Le `.editorconfig`** : `Ics/` oublié fait tomber le build (CS0649, CS8981 en erreur) ; s'il est
   ajouté sans `IDE0055`, une mise en forme de Rider réécrirait les lignes transcrites.
8. **Les renvois `fichier.cs:ligne` qui mentent** : déjà le cas (`BootDiff.cs:264`) ; G14 en multiplie l'occasion
   dans l'historique.

## 4. Les décisions ouvertes

**D1 — La portée des noms : les conteneurs seuls, ou aussi les membres ?** PLAN.md dit « les modules prennent le
nom de la puce » et justifie l'attente par les appels (`picint`, `dma_channel_read`) ; il ne dit pas si ces appels
changent. *Proposition* : les conteneurs seuls — classe, fichier, namespace, répertoire —, les fonctions gardent les
noms de PCem, comme `fdc.cs:47` l'a écrit (« elles sont l'API entre modules ») et parce que G15 fera le PascalCase
des membres : renommer `picint` deux fois coûterait deux fois la preuve. La table `// noms:` dit alors
`pic.c → Ics/Intel8259A.cs`, `pic → Intel8259A`, et les collisions de D2.

**D2 — Les suffixes de collision (`pic_`, `pit_`, `keyboard_at_`, et `ppi_c`).** Leur seule raison est CS0542
(`pit.cs:79-81`), qui disparaît avec le conteneur renommé. *Proposition* : rendre aux instances leur nom de PCem
(`pic_` → `pic`, `pit_` → `pit`, `keyboard_at_` → `keyboard_at`) dans un commit à part, après celui du conteneur,
pour qu'un commit n'ait qu'une table. C'est le retour au verbatim (TRANSCRIPTION.md:181-185 voulait déjà que le
membre garde son nom), et la lecture de `pit.cs` montre qu'aucune méthode à paramètre `pit` ne lit `pit_` (les
lectures sont à `:708-762`, dans `pit_speaker_timer`, `pit_init` et `Probe`) ; la comparaison d'IL le vérifiera.

**D3 — Le CRTC.** PLAN.md cite « le CRTC → `Motorola6845` », mais il n'y a pas de module : son état et son code
sont dans chaque carte (§ 1.1). En faire un module serait une extraction, où « seuls des noms changent » ne tient
plus (l'IL bouge). *Proposition* : G14 ne l'extrait pas ; le nom de la puce vit déjà dans `Masque6845` et dans les
tables `// noms:` des cartes, si Julien les veut ; l'extraction, si elle est voulue, est une décision à part.

**D4 — Le harnais.** `harness.c:2092` et `harness_stubs.c:172` citent `nvr.cs` et `keyboard_at.cs`, et
`check-oracle.sh` hache ces fichiers. *Proposition* : ne pas toucher `tools/oracle/` pendant G14, pour que la
`.so` garde la même empreinte d'un bout à l'autre — c'est la moitié « oracle » de la preuve ; les deux mentions
s'inscrivent dans la table `// noms:` des fichiers renommés (l'ancien nom y figure de toute façon), et se
corrigent plus tard, avec un `--fix` dit.

**D5 — L'historique.** *Proposition* : récrire les registres vivants (PCEM_BUGS, TRANSCRIPTION, README,
`Docs/ibm5150/`, notices, `oracle.tsv`) ; ne pas récrire VERIFICATION.md, les plans clos ni les rapports de
reconnaissance, qui disent l'état d'un commit daté — la pratique du dépôt (`VERIFICATION.md:1445`, `:1645`) est
d'annoter, et la table `// noms:` de chaque fichier rend le chemin de l'ancien nom au nouveau. Une ligne en tête de
la section G14 de VERIFICATION.md le dit, une fois.

**D6 — L'outil.** PLAN.md dit « refactor Rider » ; il est atteignable par son serveur MCP, mais ne couvre pas
les outils et ne dit pas ce qu'il fait des commentaires. *Proposition* : Rider (`rename_refactoring` en `preview`
d'abord, dont `affects` se confronte aux comptes de § 1.1) pour le cœur, le compilateur pour les outils, et le
diff inverse puis l'IL comme juges ; si la préversion touche une chaîne ou un commentaire de PCem, un script Roslyn
sémantique (§ 1.7) à sa place. Dans les deux cas, ni `sed` global ni « rechercher dans les commentaires ».

**D7 — Le grain des commits, et les séries.** La règle du dépôt joue la série entière à chaque changement de
l'émulateur ; la DLL change à chaque renommage. *Proposition* : cinq commits par famille (le 8259, le 8253, le 8237
et le 8255 ; le MC146818 et le 8042 et le 8250 ; les quatre chipsets ; l'OPL, l'EMU8000 et le MPU-401 ; les deux
RAMDAC), chacun avec l'IL et les listings par la table, et les deux séries entières : la preuve d'IL rend la série
confirmatoire, et cinq séries plutôt que vingt.

## Annexe — Hors de la portée

Trouvé en route, non proposé :
- 39 fichiers C# sans ligne dans `oracle.tsv`, dont onze partielles `.Materiel.cs` de `Video/` et six de `Sound/`.
- `fdc.cs:48` et VERIFICATION.md:1550 renvoient à `BootDiff.cs:264` ; les étiquettes sont à `BootDiff.cs:2090`.
- TRANSCRIPTION.md:181-185 (« le membre garde son nom ») et les membres `pic_`, `pit_`, `keyboard_at_` se
  contredisent depuis M1.
- `.editorconfig:90-91` annonce le renommage de `materiel` et des `pb_nn` « par G14 et G15 », que PLAN.md ne
  prévoit pas pour G14 ; la règle de compte de R2 (TRANSCRIPTION.md, le filtre des gardes) et `recensement` dépendent
  de cette forme textuelle.
- Le réécrivain de la couverture de GR et les scripts de série vivent hors du dépôt, en partie dans `/tmp`.
- `iXtal26.csproj:71` ouvre ses internes à `iXtal26.Sst`, une assemblée qui n'existe pas (le commentaire de
  `:74-80` le sait).
