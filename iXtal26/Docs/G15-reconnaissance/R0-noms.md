# R0 — Les noms : ce que les conventions .NET toucheraient, ce qui en dépend, et la preuve

*Reconnaissance de G15, le 4 novembre 2026, au commit `34814ec`. Lecture seule : rien n'a été construit ni joué.*
*Niveaux : **vérifié** (relu ou compté), **déduit** (suit de ce qui est relu), **inconnu**.*

Les comptes viennent de scripts jetables en python3 (`-I`, bibliothèque standard), écrits hors du dépôt et gardés
dans `/var/tmp/ixtal-g15/R0-noms/` avec leurs sorties (annexe A). Deux lectures indépendantes, recoupées :
- **les sources** (`inv.py`) : un lexeur (commentaires, chaînes et caractères retirés), puis un analyseur à
  contextes (espace de noms, corps de type, corps de membre) qui relève chaque déclaration, son genre, son
  conteneur, ses modificateurs ; sur `git ls-files 'iXtal26/*.cs'`, `'tools/iXtal26.Diff/*.cs'`,
  `'tools/perfbanc/*.cs'` ;
- **les métadonnées** (`md.py`) : les tables ECMA-335 des DLL déjà construites au commit `34814ec`
  (`/var/tmp/ixtal-g14/g145/…/bin/Release/net10.0/`, la version `1.0.0+34814ecb…` lue dans chaque DLL), et les
  tables `LocalScope`/`LocalVariable` de leurs PDB portables, lues dans le PE sans paquet, comme
  `tools/listings-jit.sh` lit le tas `#US`.

Le recoupement (`cmp.py`) : les **champs** (constantes et membres d'enum compris) sont égaux à l'unité dans les trois
assemblages (4 461, 411, 69) ; les **méthodes** aussi, une fois retirées des métadonnées celles que le compilateur
écrit pour les `record` (42, 27 et 6 : `ToString`, `Equals`, `PrintMembers`…) ; les **paramètres** aussi, à ceux des
fonctions locales près. Les **locales** sont une heuristique : 5 346 lues dans les sources, 5 165 nommées dans le PDB
de Release (une borne basse : le compilateur optimisé n'y garde pas toutes les locales).

## 0. En bref

1. iXtal26 déclare 8 976 types et membres ; 7 608 n'ont pas la forme .NET, et ce sont, à 99 %, ceux du code transcrit et
   des `.Materiel.cs`, qui l'imitent. L'hôte et les deux outils sont conformes, hors des exceptions voulues.
2. `private` explicite est déjà fait : aucun membre sans modificateur, hors des six constructeurs statiques (vérifié).
3. 1 046 constantes entières : 267 viennent d'un `enum` de PCem (43 familles), 636 d'un `#define` ; 98 noms sont aussi
   dans le harnais, 112 sont lues par les outils, 351 servent de `case`, 246 de masque.
4. Une chaîne doit suivre un membre en neuf sites, dont deux dans l'émulateur (`"ILLEGAL"`, `"opFCLEX"`) et un dans
   l'état que le banc compare à R ; environ 500 chaînes nomment le C et ne doivent pas bouger.
5. La table de G14 refuse les membres, et les trois outils de la preuve ne savent renommer que des types.
6. Un `enum` à la place d'un `const int` change les signatures : sa preuve est une substitution de types, pas de noms.
7. `TRANSCRIPTION.md`, `.editorconfig` et 18 blocs `// noms:` disent le contraire de G15 ; `PLAN.md` tranche le but
   et non la forme : la règle de conversion, le lieu de la correspondance, `materiel.pb_nn`, les fichiers, les enums.
8. Étapes proposées : les outils, les règles, sept familles de membres, les paramètres et les locales, puis les enums.

## 1. L'état, point par point

### 1.1 L'inventaire des noms

La forme visée est la forme .NET usuelle : PascalCase pour les types, les méthodes, les propriétés, les constantes, les
membres d'enum et les champs non privés ; `camelCase` ou `_camelCase` pour les champs privés ; `camelCase` pour les
paramètres et les locales (`conforme.py`). « Code transcrit » : les répertoires de `TRANSCRIPTION.md:100-101`, plus
`Diag/`, `Devices/`, `Sound/`, `Flash/`, `Mouse/` (le glob de `.editorconfig:40`) et `Cdrom/`, `Ide/`, `Joystick/`,
`Lpt/`, `Mfm/`, `Scsi/`, `rtc.cs` ; « matériel » : les `*.Materiel.cs` et `Materiel/` ; « hôte » : `Host/`,
`Program.cs`, `BootTest.cs`, `TimerCheck.cs`, `GlobalUsings.cs`.

**iXtal26/** (vérifié, `conforme.py inv-ixtal.tsv ixtal`) — chaque case : déclarés / hors de la forme .NET.

| Genre | transcrit | matériel | hôte | total |
|---|---|---|---|---|
| types (classes, structs, enums, délégués, records) | 387 / 330 | 86 / 39 | 57 / 2 | 530 / 371 |
| champs | 2 757 / 2 623 | 345 / 316 | 161 / 31 | 3 263 / 2 970 |
| constantes | 955 / 919 | 39 / 15 | 82 / 9 | 1 076 / 943 |
| membres d'enum | 51 / 24 | 10 / 0 | 61 / 0 | 122 / 24 |
| méthodes | 2 653 / 2 386 | 900 / 886 | 303 / 7 | 3 856 / 3 279 |
| propriétés | 56 / 21 | 5 / 0 | 68 / 0 | 129 / 21 |
| paramètres | 3 737 / 270 | 1 156 / 12 | 466 / 18 | 5 359 / 300 |
| locales (heuristique) | 3 326 / 519 | 1 120 / 51 | 900 / 3 | 5 346 / 573 |

- **Les noms de PCem.** Un nom est « de PCem » s'il est un identifiant des 821 fichiers C de `pcem-dev/` (30 251
  identifiants, commentaires et chaînes retirés, `classer.py`). Dans le transcrit : 2 533 champs sur 2 757, 2 330
  méthodes sur 2 653, 904 constantes sur 955, 3 677 paramètres sur 3 737 (vérifié). Les formes : 1 412 champs et 1 219
  méthodes en `snake_case`, 1 185 champs et 237 méthodes en minuscules d'un mot, 924 constantes en majuscules (les
  `#define`), 686 méthodes mixtes (`opADD_b_rmw`) ; 236 méthodes seulement sont déjà en PascalCase.
- **Le code du mode matériel imite le transcrit** : 886 de ses 900 méthodes sont hors de la forme (`longueur_materiel`,
  `poser_8087_materiel`), 253 seulement portent un nom de PCem (vérifié).
- **L'hôte est conforme** (vérifié, lecture des 70 noms relevés) : la règle simple compte à tort 17 paramètres
  positionnels de `record struct`, en PascalCase comme il se doit, et 15 champs `private static readonly` en
  PascalCase ; restent `Host/SdlJoystick.cs`, qui garde les noms de la plate-forme de PCem (`plat_joystick_t`,
  `joystick_init`, `nr_axes`, `SDL_HAT_UP` : 2 types, 7 méthodes, 15 champs, 8 constantes, 2 paramètres et locales),
  `HddImage.cs:44` et `:77` (`MAX_CYLINDERS`, `hd_types`) et deux locales de `SdlMenu.cs`.
- **Par répertoire** (vérifié, `classer.py`, types et membres hors de la forme PascalCase, champs privés compris) :
  `Cpu/` 1 686 + 592 du mode matériel ; `Video/` 1 030 + 95 ; `Scsi/` 638 + 90 ; `Ics/` 588 + 63 ; `Sound/` 545 + 152 ;
  la racine 233 ; `Ide/` 228 + 12 ; `Materiel/` 205 ; `Floppy/` 180 + 2 ; `Disc/` 174 + 2 ; `Memory/` 147 ; `Mfm/`
  144 + 17 ; `Cdrom/` 142 + 13 ; `Diag/` 127 ; `Keyboard/` 126 ; `Joystick/` 123 + 4 ; `PluginApi/` 119 ; `Lpt/` 100 ;
  `Models/` 93 + 11 ; `Mouse/` 71 + 9 ; `Flash/` 16 ; `Devices/` 3. 197 fichiers en ont ; la médiane est de 23 par
  fichier, 64 fichiers en ont plus de 40 (`Ics/Emu8000.cs` 258, `Materiel/materiel.cs` 203, `Scsi/scsi_aha1540.cs`
  197, `Floppy/fdc.cs` 144).

**tools/iXtal26.Diff/** et **tools/perfbanc/** (vérifié, même script) :

| Genre | iXtal26.Diff | perfbanc |
|---|---|---|
| types | 110 / 0 | 47 / 0 |
| champs | 205 / 74 | 59 / 7 |
| constantes | 183 / 16 | 10 / 0 |
| méthodes | 958 / 119 | 137 / 0 |
| propriétés | 34 / 26 | — |
| paramètres | 2 062 / 16 | 106 / 10 |
| locales | 3 365 / 42 | 142 / 0 |

Ce qui reste hors de la forme y est voulu (vérifié, lecture) : 117 des 119 méthodes de Diff sont les `extern` des
`DllImport` (`Oracle.cs:156…`), qui portent le nom du symbole C `h_*` ; les 26 propriétés sont les DTO de
SingleStepTests (`SstProbe.cs:51-64` : `ax`, `bx`…), que `.editorconfig:112-119` exempte déjà ; les 74 champs sont des
`private static readonly` en PascalCase (`BootDiff.cs:149` `ZipOracle`), conformes ou non selon la règle qu'on écrira ;
dans le banc, `GardeBanc.cs:60` et `:67` copient `materiel.pb_01`, et les paramètres hors de la forme sont des `_` et
les paramètres positionnels d'un `record`. Les deux outils lisent iXtal26 bien plus qu'ils ne déclarent : 654 membres
distincts d'iXtal26 nommés qualifiés en 3 092 sites dans Diff, 84 en 265 sites dans le banc, sans compter les
`using static` (5 et 6 directives) (vérifié, motif `Conteneur.membre` sur les jetons, annexe A).

**L'ampleur.** 6 507 noms distincts de types et de membres ne sont pas en PascalCase dans les trois assemblages. Leurs
occurrences hors commentaires et chaînes touchent au plus 54 108 des 93 187 lignes de code d'iXtal26, 8 787 des 23 079
de Diff, 569 des 1 604 du banc (une borne haute : un paramètre homonyme d'un membre est compté). Le code compte 398
`using static iXtal26.…` dans 134 fichiers : la plupart des appels entre modules sont des noms simples (vérifié).

**Les collisions** d'une conversion mécanique (`collisions.py` : chaque mot du `snake_case` capitalisé, un mot tout en
majuscules ramené à une majuscule ; vérifié) : dans iXtal26, 30 paires de noms d'un même conteneur tombent sur le même
nom, et ce sont des distinctions du C par la casse ou le soulignement : `CS`/`cs`, `DS`/`ds`, `ES`, `SS`, `GS`
(`Cpu/x86.cs:153-160` : le sélecteur et la base, que le commentaire de PCem distingue, `:151-152`),
`PREFETCH_RUN`/`prefetch_run` (`Cpu/386.cs:314` et `:245`), `IDE`/`ide` (deux types), `_dma_read`/`dma_read`,
`SCANCODE_SET_1`/`scancode_set1`, `ZERO`/`zero`, `TREMOLO_TABLE`/`TremoloTable`, `KeyOn`/`keyOn`… Trois membres
prendraient le nom de leur conteneur (CS0542 : `keyboard_amstrad_`, `keyboard_olim24_`, `keyboard_xt_`, que
`keyboard_amstrad.cs:44-46` suffixe déjà pour cette raison), et six conteneurs commencent par un chiffre une fois le
`_` ôté (`_808x`, `_386`, `_386_common`…). La conversion ne peut donc pas être une règle seule : il lui faut des
exceptions écrites.

**Hors du périmètre demandé, pour mémoire** : `iXtal26.SoftFloat/` (53 fichiers, le même pacte que le transcrit,
`.editorconfig:78-79`) a 59 types, 75 méthodes, 49 champs et constantes, tous en `snake_case` (vérifié, même script).

### 1.2 `private` explicite

- **Aucun membre sans modificateur d'accès** dans les trois assemblages, hors de six constructeurs statiques
  (`Cpu/386.cs:89`, `Disc/disc_img.cs:68`, `Lpt/lpt.cs:284`, `Materiel/materiel.cs:638`, `Oracle.cs:98`,
  `GardeBanc.cs:69`), que C# interdit de qualifier (CS0515) (vérifié : `inv.py`, colonne de l'accès, sur 8 878, 1 491
  et 254 déclarations ; contre-épreuve par `grep` des déclarations à quatre espaces qui commencent par `static`,
  `const`, un type… : les seules sont ces constructeurs). Les types de premier niveau aussi : tous qualifiés.
- **La règle existe déjà, en commentaire** : « le mapping linkage C → accessibilité C# (static → private, non-static →
  internal) » (`iXtal26.csproj:67-69`). Elle n'est imposée par aucun analyseur : `.editorconfig` n'a ni
  `dotnet_style_require_accessibility_modifiers` ni IDE0040 (vérifié, `grep`). L'analyseur est livré avec le SDK
  (`sdk/10.0.114/Sdks/Microsoft.NET.Sdk/codestyle/cs/Microsoft.CodeAnalysis.CSharp.CodeStyle.dll`, `find`), et
  `EnforceCodeStyleInBuild` est posé dans `iXtal26.csproj:39`, pas dans les deux projets d'outils (vérifié).
- **Effet sur l'IL** : aucun. Un membre de classe sans modificateur est privé ; `private` écrit produit le même drapeau
  de métadonnées (déduit, la règle du langage). Il n'y a d'ailleurs rien à écrire.

### 1.3 Les constantes entières, candidates à un `enum`

Le relevé (`consts.py`, vérifié) : 1 076 `const` dans iXtal26, dont 1 046 entières (`int` 753, `uint8_t` 218,
`uint16_t` 37, `uint32_t` 31, `uint` 4, `uint64_t` 2, `char` 1) ; les 30 autres sont 15 `string` et 15 `double`.

- **D'où elles viennent** : 267 portent le nom d'un membre d'un `enum` C de PCem (toutes dans le transcrit), 636 celui
  d'un `#define` (632 dans le transcrit), 143 sont propres à iXtal (53 dans le transcrit, 33 dans le mode matériel, 57
  dans l'hôte). Commande : les noms des `enum { … }` et des `#define NOM` (sans parenthèse) de `pcem-dev/src` et
  `pcem-dev/includes` (1 404 et 3 218 noms).
- **Groupées** par fichier et par préfixe (`groupes.tsv`) : 132 familles de deux constantes ou plus (847 constantes) et
  199 isolées (les tailles, les adresses, `MAX_*`). La nature d'une famille, lue sur les valeurs : 48 familles de bits
  (223 constantes, une puissance de deux chacune : `STAT_*` des claviers et du MFM de l'AT, `FLAG_*` de `fdd.cs`,
  `BUS_*` de `scsi.cs`, `INPUT_*` de la SB), 24 suites de 0 à n (161 : `FLAGS_*`, les états `STATE_*`), 34 de valeurs
  nommées (291 : `COMMAND_*` de l'AHA, `GPCMD_*`, `ROM_*`, `RTC_*`), 26 dont la valeur est une expression (172).
- **Les 43 familles d'un `enum` de PCem** sont les candidates naturelles. Les plus grandes : `FLAGS_*` (37,
  `Cpu/x86_flags.cs:57…`, l'opération des drapeaux paresseux, sur le chemin chaud), `ROM_*` (36, `pc.cs`), `RTC_*`
  (32, `rtc.cs`), `STATE_*` (12, `disc_sector.cs`), `GFX_*` (11), `SCSI_STATE_*` (10), `FIFO_*` (7, `vid_s3.cs`),
  `ABRT_*` (7), `ENV_*` (7, `Emu8000.cs`). Trois `enum` de PCem sont déjà des `enum` C# : `hdd_img_type`
  (`hdd_file.cs:28`), `fontformat_t` (`video.cs:49`), `SynthMode` (`dbopl.cs:178`) ; `Diag/HState.cs:26-40` en ajoute
  trois de l'outillage (`R`, `Seg`, `Sys`), lus en index par `(int)R.BX` (`DecodageBanc.cs:130`).
- **Les usages** (les jetons voisins de chaque occurrence, hors déclaration) : 964 constantes sont lues, 82 jamais ; 351
  en `case`, 246 dans un masque (`&`, `|`, `^`, `~`), 106 entre crochets (index ou taille de tableau, `GPCMD_*` 28 sur
  28, `RTC_*` 11), 2 à la fois index et masque.
- **Ce qui passe par le harnais** : 98 noms de constantes sont aussi des identifiants de `tools/oracle/` (`GFX_*` 10,
  `FIFO_*` 10, `ROM_*` 9, `MODEL_*` 7, `CPU_*` 7, `VIDEO_*` 6, `DISPLAY_*` 5) ; leur valeur traverse le P/Invoke en
  `int`, et elle doit égaler celle du C : `Oracle.h_set_romset(ROM_IBMAT)` (`AtProbe.cs:36`),
  `Oracle.h_set_fpu(FPU_387)` (`X87Cases.cs:287`) (vérifié). 112 constantes sont lues par Diff ou le banc
  (`InternalsVisibleTo`).

### 1.4 Ce qui dépend des noms des membres

**La réflexion et les chaînes qui doivent suivre un membre** (vérifié, `git grep` de `BindingFlags`, `GetField(`,
`GetMethod(`, `Method.Name`, `nameof`, `Caller*`). Chacune casserait ou mentirait sans diagnostic du compilateur :

- `Cpu/386.Materiel.cs:313-315`, `GetMethod("ILLEGAL")` : l'initialiseur lève, la machine ne démarre plus ;
- `Cpu/x87.Materiel.cs:40`, `h.Method.Name == "opFCLEX"` : PB-59, FNCLEX n'efface plus B, en mode « tout » seul ;
- `tools/iXtal26.Diff/MaterielCas.cs:514`, `GetField("memcycs")!` : une exception, la porte rougit ;
- `MaterielCasPit.cs:52` et `:55`, `"speaker_pos"`, `"speaker_buffer"` : de même ;
- `MaterielCasFrequence.cs:85-87`, les suffixes `"_iir_x"` et `"_iir_y"` : les filtres ne sont plus remis à zéro, et le
  témoin dépend des cas d'avant, sans erreur ;
- `OpsCount.cs:32`, `"opNonTranscrit"` : le décompte d'`ops-count` devient faux, sans erreur ;
- `tools/perfbanc/GardeBanc.cs:93`, `GetMethod("setadc8")!` : `VerifierEquivalence` lève ;
- `Is486Banc.cs:107-108`, `"opCMPSB_a16"` : le banc lève ;
- `RenduBanc.cs:158-159` et `:164-165`, `"svga_render_8bpp_highres"`, et `{rendu}` dans l'état imprimé : le banc lève ;
  et l'état comparé à R diffère, un rouge exact de `compare-ref.sh` (PLAN-G14 § G14.0b).

Le dernier est le seul qui touche la preuve du banc : R, figé, imprimera toujours l'ancien nom (`Reference.cs:224-225`,
`Reference.Nom` rend `d.Method.Name`). `nameof` ne règle pas tout : il exige un membre accessible, et `memcycs`,
`speaker_pos`, `setadc8` sont privés, lus par réflexion pour cette raison (déduit, la règle de `nameof`).

**Les chaînes qui nomment le C et ne doivent pas bouger** (vérifié, `chaînes égales à un nom de membre`, jetons de
chaîne comparés aux noms de l'inventaire) :
- 413 littéraux de Diff égalent un nom de membre d'iXtal26 : `BootDiff.cs` 121, `VgaProbe.cs` 83 (`VgaProbe.cs:28-68`,
  les champs de `h_vga_probe` dans l'ordre de `harness.c`), `Fuzzer.cs` 51 (`Chk("n_picint", …)`), `CpuFingerprint.cs`
  36 ; 384 autres en contiennent un. Ce sont les étiquettes des sondes : elles désignent les globales **du C**, et la
  règle est écrite (`Floppy/fdc.cs:48-49` ; VERIFICATION.md:1550-1554 : « les renommer par cohérence aurait fait
  mentir la sonde ») ;
- 74 littéraux d'iXtal26 : surtout des clés de configuration de PCem (`pc.cs` 15 : `"model"`, `"mem_size"`,
  `"gfxcard"` ; `sound_sb.cs` 19 ; `SdlSetup.cs` 14), lues dans les 58 `.cfg` du dépôt et dans ceux de l'utilisateur ;
  plus les clés R9 (`R9.Garde("sound_emu8k.c:1164")`), des noms de fichier (`ami386dx_opti495.nvr`), et les libellés
  de `ModeMateriel.Corrections` (`ModeMateriel.cs:41-…` : « le compte 0 lu 65 536 dans speakval »), imprimés ;
- `h_state` et son miroir `Diag/HState.cs` : des champs au nom du C, rangés en `LayoutKind.Sequential` ; le marshalling
  suit l'ordre, pas le nom, et `Oracle.CheckAbi` ne vérifie que la taille (R2-noms de G14 § 1.2). Un renommage de ces
  champs ne casse rien ; l'ordre, si.

**Les noms que le compilateur dérive d'un membre** (vérifié, `md.py` sur iXtal26.dll) : 1 264 champs des caches de
délégués `<>O` portent le nom de la méthode visée (`iXtal26.Video.vid_cga/<>O` : `<0>__cga_poll`, `<1>__cga_read`) ;
201 méthodes générées portent celui de la méthode qui les contient (`<PressKey>g__Step|2_0`, les lambdas
`<…>b__…`). Le vidage d'IL les nomme ; G14 n'y a jamais eu affaire (ces noms portent des membres, pas des types).

**Les documents et les registres** (vérifié, `grep`) :
- `PCEM_BUGS.md` : 241 champs *Reproduit* et 208 *Corrigé en mode matériel*, qui nomment le site C# par le fichier
  et le nom de PCem (« `Cpu/808x.cs`, `setadc8`, `setadc16`… », PB-01) ; 614 chemins `.cs`, 24 renvois `fichier.cs:N`,
  183 mentions de `materiel.pb_nn` ; les 1 184 identifiants en `snake_case` entre accents graves (716 distincts) sont
  surtout des fonctions C. Les marqueurs du code (`// pcem bug, …`, 1 280 lignes dans 111 fichiers) ne portent qu'un
  numéro : la porte `recensement` (`Recensement.cs:68-82`) ne lit aucun nom de membre ;
- `TRANSCRIPTION.md` : R1(e) (`:23-28`), la règle de compte de R2 (`:41-43`, `materiel\.pb_[0-9]+`), R10 (`:88`, la
  garde `if (materiel.pb_nn)`), la portée (`:100-102` : le transcrit « garde les identifiants … de PCem »), la table des
  conventions (`:121`, `:132` : « `snake_case` de PCem **verbatim** » ; `:133` : « miroir du chemin C »), les
  corollaires qui citent `regs`, `mem_logical_addr`, `tempc` (`:137-152`), le nommage explicite (`:154-174`), les
  collisions de mots-clés et de conteneurs (`:176-199`). Le fichier a 249 lignes, sous un plafond de 260 (R3, `:49`) ;
- `.editorconfig` : la raison d'être du transcrit, « `grep -n <symbole>` renvoie des lignes correspondantes des deux
  côtés » (`:30-33`) ; IDE1006 et CA1707 éteints sur le transcrit (`:40-45`), sur SoftFloat (`:80-82`), sur
  `materiel.cs` (`:96-99`, avec la raison : la forme `pb_nn` sert à R2 et au recensement, `:93-95`), sur `io`, `timer`,
  `pc`, `ppi` (`:101-104`), sur les outils (`:112-119`). Aucune règle `dotnet_naming_*` n'existe (0, `grep -c`) : ces
  extinctions ne font rien aujourd'hui, et G15 les rendra actives en écrivant les règles ;
- les 22 blocs `// noms:` (Disc 3, Floppy 2, Ics 12, Video 5) : 18 disent que les fonctions « gardent les noms de
  PCem » (`fdc.cs:47`, les 12 d'`Ics/`, les 5 de `Video/` : `Intel8259A.cs:16`, `vid_cga.cs:11`…). R1(e) les borne à
  40 lignes (`TRANSCRIPTION.md:24`) ;
- `Docs/ibm5150/` : 82 renvois `chemin.cs#symbole` (72 distincts : `execx86`, `cycdiff`, `memcycs`, `pb_01`…), que
  `check-doc5150.sh` contrôle par `grep -qw -- "$sym" "$f"` (`:67`), sur tout le fichier, commentaires compris. Un
  symbole renommé dont l'ancien nom reste dans un commentaire de PCem (R1(b), verbatim) passerait ce contrôle à tort
  (déduit) ;
- les commentaires du harnais qui nomment un membre C# : `sound.sound_cd_raz` (`harness.c:318`), `_808x.FigerGarde`
  (`:861`), `_386.Reset286()` (`:961`), `FauteGarde` (`:784`), les `Probe` des sondes (20 lignes). Les corriger
  reconstruit la `.so` (G14.5 l'a fait à l'octet égal, aucun numéro de ligne ne bougeant) ;
- `oracle.tsv` est ancré sur les chemins (`csharp_path`) et les lignes C : un renommage de membres ne le touche pas ;
  ses notes nomment le C (« cpu_state et nmi_enable »). Les `.cfg` citent des fichiers et des lignes
  (`ixtal26-286.cfg:11` cite `cpu_tables.cs:86`), 7 renvois.

**Les générateurs** (vérifié, en-têtes) : huit fichiers C# sont produits par des scripts qui écrivent les noms de PCem :
`Cpu/386_ops_table386.cs` (`tools/ops386-table.py`, qui lit les tables de PCem « par leur NOM »), `x87_ops_arith.cs`,
`x87_ops_misc.cs`, `x87_ops_tables.cs`, `x87_ops_808x.cs`, `x87_ops_808x_tables.cs` (`tools/x87gen/gen43.py`,
`gen44.py`, `gentab.py`, `gen46.py`), `x87_noyau_386.Materiel.cs` et `x87_noyau_808x.Materiel.cs` (`genmat.py`). Un
renommage du seul C# serait défait à la prochaine génération.

**Ce qui ne dépend pas des noms** (vérifié) : `InternalsVisibleTo` nomme des assemblages (`iXtal26.csproj:70-73`) ;
`PluginApi/` est la transcription interne de l'API de plugin de PCem, sans chargement d'assemblage (0 `Assembly.Load`,
`LoadFrom`, `AssemblyLoadContext`) : aucune ABI extérieure ; la ligne de commande (`Host/CommandLine/`) est déjà en
PascalCase et ses options sont des chaînes ; le gel de `materiel` passe par `typeof` (`ModeMateriel.cs:348`) ; les DTO
de SST se lisent sans casse (`SstProbe.cs:98`, `PropertyNameCaseInsensitive = true`) ; les `[Params]` du banc sont déjà
en PascalCase (`DecodageBanc.cs:34`…), et les noms de ses cas (les méthodes du banc, toutes conformes) ne changent pas.

### 1.5 La preuve « seuls des noms changent », étendue aux membres

**Ce que les outils de G14 savent faire** (vérifié, lecture) :
- `tools/noms/g14.txt` : des types de premier niveau et des fichiers ; « Une entrée qui désigne un membre est refusée »
  (`g14.txt:30-31`) ; `TableDesNoms.cs:20` lit des noms qualifiés, et `ildump` comme `listings-jit.sh` refusent ceux
  qui désignent un membre ;
- `tools/ildump` : clé par nom qualifié, triée ; la table s'applique aux noms de types, dans les clés et les corps
  (`Program.cs:13-16`) ; un membre de la table est refusé (`:39`, `:195`) ; le vidage compare les champs « DANS L'ORDRE
  des métadonnées » avec nom, signature et constante (`:21`), les paramètres avec leur nom (`:24`), les opérandes de
  l'IL résolus en noms (`Type::membre : signature`, `:377-390`) ;
- `listings-jit.sh compare --noms` : chaque nom de type ancien, borné, devient le nouveau ; « les membres gardent leur
  nom » ; refus si une entrée désigne un membre (`:269-305`). Dans les listings, les membres paraissent dans les
  en-têtes et dans les opérandes des `call` et `tail` (`call [iXtal26.Cpu.x86:get_AX():byref]`), jamais un nom de
  champ : les accès aux champs sont des décalages et des adresses masquées (vérifié sur `ami386dx-387.txt` de
  `ref-R-captures-10.0.14`, 3 517 `call` et 141 `tail` sont les seules lignes qui nomment iXtal26) ;
- `tools/renommage` : ne touche qu'un nom que le modèle sémantique lie à un **type** de la table (`Program.cs:164`,
  `if (s is not INamedTypeSymbol n) return null;`) ; ni commentaire ni chaîne ; recompile en mémoire et refuse toute
  erreur ou tout avertissement de plus (`:29-36`). Il charge Roslyn du SDK (`Roslyn/bincore`), sans l'API
  `Workspaces` : pas de `Renamer` (vérifié, `find` : `Microsoft.CodeAnalysis.Workspaces.dll` n'existe que sous
  `sdk/10.0.114/DotnetTools/dotnet-format/`).

**Ce qu'il leur faudrait pour les membres** (déduit) :
- une table des membres, portée par le type qui déclare (`Type::membre`), qui couvre les surcharges d'un nom, les
  accesseurs d'une propriété (`get_AX`), les noms dérivés par le compilateur (`<>O.<k>__nom`, `<nom>g__…`, `<nom>b__…`),
  les constantes et les membres d'enum ; et qui refuse une collision (deux anciens vers un nouveau dans une même portée,
  un nouveau égal au nom du conteneur, un nouveau qui existe déjà). Avec ~6 500 noms, elle ne s'écrit pas à la main :
  une règle et une liste d'exceptions la génèrent, de façon déterministe ;
- `ildump` : la table des membres appliquée aux clés, aux champs, aux opérandes ; pour les paramètres, soit des entrées,
  soit une option qui compare position, type, drapeaux et constante sans le nom ; une liste close de littéraux qui
  changent avec leur membre (les neuf sites de § 1.4), chacun vérifié ancien → nouveau, tout autre `ldstr` changé
  restant un écart ;
- `listings-jit.sh` : `Type:membre(` dans les en-têtes et les opérandes ; le refus « un ancien encore dans AUTRE » porté
  par le type, puisqu'un même nom de membre peut rester dans un autre type ;
- `tools/renommage` : les symboles de membres (`IFieldSymbol`, `IMethodSymbol` y compris les groupes de méthodes des
  tables de handlers, `IPropertySymbol`, les membres d'enum, les paramètres et les locales dans leur portée), et une
  **vérification de liaison** : après la réécriture, chaque nom touché se lie au même symbole qu'avant, et chaque nom
  non touché aussi. Sans elle, un champ renommé vers le nom d'une locale, ou deux surcharges importées par deux
  `using static`, changeraient le sens sans erreur de compilation ; l'IL le verrait, l'outil doit le refuser avant ;
- `compare-ref.sh` : l'état imprimé normalisé par la table (le `{rendu}` de `RenduBanc.cs:165`), ou un libellé fixe
  dans le banc ;
- `check-doc5150.sh` : chercher le symbole dans le code sans ses commentaires, pour qu'un renvoi périmé échoue ;
- les générateurs : la table appliquée à leurs noms, et une porte « régénéré = commité ».

**L'effet sur l'IL** (déduit, sauf mention) :
- `private` explicite : aucun (§ 1.2), et aucun site ;
- **un renommage de membre** : le tas `#Strings` change, pas les opcodes. L'ordre des lignes des tables suit l'ordre de
  déclaration, que le renommage ne touche pas : le vidage, qui compare les champs dans cet ordre, reste comparable par
  la table. Le tas `#US` ne change que si un littéral change : `nameof` n'a aucun site dans iXtal26 et un seul dans le
  banc (`Reference.cs:224`, `[CallerArgumentExpression(nameof(d))]`) (vérifié) ; les neuf littéraux de § 1.4 changent,
  et chacun décale les jetons `#US` des chaînes suivantes : l'écart de Q39, que `--dll-ref/--dll-autre` lit déjà
  (`listings-jit.sh:39-51`). Les noms des locales ne sont que dans le PDB : leur renommage laisse l'IL **identique sans
  table** (déduit : `ildump` ne vide des locales que leur signature, des types, `Program.cs:711-712`, vérifié). Les
  paramètres sont dans la table `Param` : le vidage les montre ;
- **un `enum` à la place d'un `const int`** : les constantes disparaissent de leur classe, un type apparaît (ses champs
  littéraux et `value__`). Là où la valeur reste lue en `int` (`(int)E.X`), l'opcode est le même `ldc.i4`. Dès qu'un
  champ, un paramètre ou un retour prend le type de l'enum, sa signature devient `valuetype E` : le champ, chaque
  `ldfld`/`stfld` qui le vise, chaque `MemberRef` de Diff, chaque signature de locales changent dans le vidage, sans
  qu'un opcode change. Le `switch` reste le même `switch` IL ; un index de tableau demande `(int)`, qui ne coûte rien
  en IL ; une interpolation (`$"{e}"`) passe par `AppendFormatted<E>` et imprime le **nom** au lieu du nombre ;
  `ToString`, `Equals(object)`, une clé de dictionnaire changent d'instanciation. Pour le JIT, un enum est son type
  sous-jacent : le code machine des opérations est le même ; les en-têtes et les opérandes des listings nomment les
  types des signatures (`ApplyArgument(iXtal26.Host.CommandLine.ArgumentCursor,…)`, vérifié), et y nommeraient l'enum
  (déduit ; inconnu tant qu'une copie jetable ne l'a pas montré). La preuve d'un enum est donc une **substitution de
  types** (E ↔ son sous-jacent, et `const X` ↔ `E.X` de même valeur), pas une table de noms ;
- **ce que G14 a appris et qui vaut ici** : l'ordre de compilation suit l'ordre des fichiers, et déplacer un fichier
  réordonne le tas `#US` (Q39, PLAN-G14.md:378-384) : un enum se déclare dans le fichier des constantes qu'il remplace ;
  le vidage du banc porte `[CallerFilePath]` et `[CallerLineNumber]` de `BenchmarkAttribute`
  (VERIFICATION.md:16731-16734, :17725-17728) : une ligne ajoutée en tête d'un fichier du banc change des entrées sans
  changer le code ; un initialiseur statique de plus change le constructeur statique et le code du JIT
  (`Ics/Intel8259A.Materiel.cs:19-24`, la raison de ses classes imbriquées) : une conversion ne doit pas en ajouter.

### 1.6 La tension entre « les fonctions gardent les noms de PCem » et « chaque membre en PascalCase »

**Ce que `PLAN.md` tranche** (vérifié) :
- le contenu de G15, décidé par Julien le 03/10 : « les conventions .NET (PascalCase, `private` explicite, `enum` au
  lieu des constantes `int`) » (`PLAN.md:569-570`) ; les commentaires restent en français (`:571`) ; les marqueurs
  `// pcem: fichier.c:ligne` et `// pcem bug … PB-nn` sont conservés (`:572`) ;
- la raison des noms verbatim était la transcription : « pendant les transcriptions, les noms verbatim de PCem évitent
  de traduire chaque appel (`picint`, `dma_channel_read`…) dans chaque nouveau fichier » (`PLAN.md:554-555`) ; plus
  aucun fichier ne reste à transcrire (`:546-547`) ;
- PLAN-G14, décision n° 1 : « G15 renommera chaque membre en PascalCase (`PLAN.md:569-570`) : les renommer en G14
  doublerait le travail et la preuve » (`PLAN-G14.md:298-300`).

**Ce qui le contredit, écrit avant** (vérifié) : `TRANSCRIPTION.md:100-102` et `:132` (le verbatim), `:173-174` (« Hors
de ces trois répertoires, la règle reste le verbatim : `808x.cs` dit en en-tête pourquoi » ; `808x.cs:17-19` : « on
transcrit les dix variables avec les noms de PCem, on ne consolide rien ») ; `.editorconfig:30-33` (le `grep -n` des
deux côtés, « la seule raison d'être de la transcription ») ; les blocs `// noms:` (« elles sont l'API entre
modules », `fdc.cs:47`). Ces textes sont des règles vivantes : G15 les amende, comme G14.0c a amendé R1(e) et le nommage
explicite pour `Ics/`.

**Ce que le renommage coûte à la lecture contre le C, et ce qui le compense** (déduit) :
- `grep -n setadc8` ne trouvera plus la fonction C# ; il trouvera le marqueur seulement si le marqueur porte le nom ;
- les `// pcem:` restent (2 982 lignes dans 143 fichiers, vérifié) : ils donnent le fichier et les lignes du C de chaque
  fonction, pas son nom ;
- les étiquettes des sondes et des portes restent au nom du C (§ 1.4) : le diff par instruction compare toujours les
  « dix variables » de `808x.cs:17-19` sous leurs noms de PCem, ce qui garde l'argument de ce commentaire ;
- les commentaires de PCem, verbatim (R1(b)), continuent de nommer les fonctions du C.

**Ce qui reste ouvert** : la règle de conversion et ses exceptions ; le lieu de la correspondance PCem ↔ iXtal26 (R1(e)
borne un bloc à 40 lignes, et 64 fichiers ont plus de 40 noms à changer) ; la forme des marqueurs `// pcem:` (les
garder tels quels, ou y ajouter le nom C) ; `materiel.pb_nn` ; les noms de fichiers ; la portée des enums ; le sort des
lignes de plus de 120 caractères qu'un renommage touche ; SoftFloat. Section 4.

## 2. Ce qu'il faut faire, et les étapes proposées

Le principe, celui de G14 étendu : un nom change, rien d'autre ; la preuve à chaque commit, de quatre façons (l'IL
identique par la table, le code machine identique par la table, les séries identiques à l'unité, le banc égal à R).
Les enums viennent après les noms, avec leur propre preuve (la substitution de types). L'ordre ne dépend d'aucune
question, sauf N1 (la règle de conversion, Q1) et E1 (Q5). Les numéros sont provisoires.

### G15.N0 — Les outils de la preuve des membres

- La table des membres (`tools/noms/`), son générateur (la règle, les exceptions écrites), sa grammaire, ses refus
  (§ 1.5). La liste close des littéraux liés à un membre (§ 1.4), dans la table.
- `ildump`, `listings-jit.sh compare --noms`, `tools/renommage` étendus aux membres ; la vérification de liaison ;
  l'option des paramètres d'`ildump`.
- `compare-ref.sh` : l'état imprimé normalisé par la table ; `check-doc5150.sh` : le symbole hors des commentaires.
- **Portes** : `iXtal26.dll` identique au sha256 (seuls les outils changent) ; `ildump` et `compare` d'une capture
  contre elle-même : 0 ; la répétition générale dans une copie jetable : les membres d'un module (`Ics/Intel8259A.cs`,
  32 noms, et ses appelants) renommés par l'outil, l'IL et les listings identiques avec la table, rouges sans elle, et
  le sous-ensemble ciblé de la série identique ; `check-doc5150` juste sur l'arbre.
- **Contrôles négatifs** : une table avec une collision (`CS` et `cs` vers `Cs`), un membre inexistant, un nouveau égal
  au conteneur : refusés ; dans une copie, un champ renommé vers le nom d'une locale qui le masque : refusé par la
  liaison, et sorti en écart par `ildump` ; une étiquette de `VgaProbe.Fields` renommée : écart d'IL hors de la liste et
  série rouge ; `"opFCLEX"` laissé à l'ancien nom : refusé par la liste, et `materiel-cas PB-59` rouge en « tout » ; un
  renvoi `chemin.cs#symbole` dont l'ancien nom ne reste qu'en commentaire : `check-doc5150` rouge.

### G15.N1 — Les règles

- `TRANSCRIPTION.md` amendé dans son plafond (249 lignes sur 260) : R1(e), la portée, la table des conventions, le
  nommage explicite, les collisions ; R2 et R10 selon Q4. `.editorconfig` : les règles `dotnet_naming_*` de la forme
  .NET, l'IDE0040 (`private` explicite, 0 site à corriger), les exceptions voulues (P/Invoke, DTO, et ce que Q4
  décidera) ; les extinctions d'IDE1006 retirées répertoire par répertoire, à l'étape qui le renomme.
- **Portes** : construction sans avertissement ; la DLL identique au sha256 (`.editorconfig` ne règle que des
  diagnostics). **Contrôles négatifs** : dans une copie, un membre sans modificateur et un nom en `snake_case` dans un
  répertoire dont l'extinction est retirée font échouer la construction.

### G15.N2 à N8 — Les membres, une famille à la fois

Une famille est un ensemble de répertoires avec ses `.Materiel.cs` (une classe partielle ne se renomme qu'entière), et
le commit touche tous ses appelants (iXtal26, Diff, banc), comme G14. Proposé, par taille (noms hors de la forme,
§ 1.1) : N2 l'hôte et les restes des outils, petit, la répétition du circuit entier (`SdlJoystick.cs`, `HddImage.cs`,
les deux méthodes de Diff hors P/Invoke) ; N3 `Ics/`, `Floppy/`, `Disc/` (~1 000, leurs blocs `// noms:` récrits) ;
N4 `Video/` (~1 120) ; N5 `Sound/`, `Joystick/`, `Lpt/`, `Mouse/`, `Keyboard/` (~1 130) ; N6 `Cdrom/`, `Ide/`, `Scsi/`,
`Mfm/` (~1 280) ; N7 `Memory/`, `Models/`, `PluginApi/`, `Diag/`, `Devices/`, `Flash/`, la racine, `Materiel/`
(~950, selon Q4) ; N8 `Cpu/` (~2 280, les huit fichiers générés et leurs générateurs), le chemin chaud, en dernier.
- **Portes** : construction sans avertissement ; l'IL d'iXtal26, de Diff et du banc identique par la table (0 changée,
  0 disparue, 0 apparue, hors des littéraux de la liste ; la limite connue du banc, `[CallerFilePath]`) ; les listings
  par la table et par les jetons (`--dll-ref/--dll-autre`) : 0/0/0 ; les séries entières dans les deux modes identiques
  à `g14r` et `g14r-tout` ; le banc vert contre R ; `check-oracle.sh` sans dérive, `check-doc5150.sh` juste, la `.so`
  inchangée ; le nombre de lignes vives de chaque fichier inchangé (R2 ne bouge pas) ; les générateurs régénèrent les
  fichiers commités à l'octet (N8) ; `grep` des anciens noms hors commentaires et chaînes : 0, hors de la liste ; les
  lignes longues selon Q6.
- **Contrôles négatifs** : `ildump` et `compare` sans la table des membres sortent exactement les membres renommés en
  disparus et apparus ; `git diff` de `BootDiff.cs`, `VgaProbe.cs`, `Fuzzer.cs` limité aux sites qualifiés (aucune
  étiquette changée) ; un générateur relancé avec une table tronquée produit un écart.

### G15.N9 — Les paramètres et les locales

- Les 300 paramètres et ~570 locales d'iXtal26 hors de la forme `camelCase` (§ 1.1), 16 et 42 dans Diff, 10 dans le
  banc. Une étape à part : sa preuve est plus forte, l'IL des locales devant être **identique sans table** (leurs noms
  ne sont que dans le PDB), celui des paramètres identique par l'option d'`ildump`.
- **Contrôle négatif** : une locale renommée vers le nom d'un champ qu'elle masque, dans une copie : refusée par la
  liaison, écart d'IL.

### G15.E1 et E2 — Les enums

- E1 : les familles retenues par Q5, en commençant par les 43 familles des `enum` de PCem ; E2 : le reste de ce que Q5
  retient. Un enum se déclare dans le fichier qu'il remplace (Q39) ; sa valeur égale celle du C, membre par membre.
- **Portes** : l'IL identique par la table des noms et la substitution de types ; les listings de même ; les séries
  entières dans les deux modes (une valeur de C# qui s'écarterait du C rougirait les boot-diffs et les portes qui
  passent par `h_set_*`) ; le banc vert contre R.
- **Contrôles négatifs** : dans une copie, un membre d'enum de valeur changée : écart d'IL (la constante) et série
  rouge ; un champ d'enum imprimé par interpolation dans une sonde : série rouge (le nom au lieu du nombre) ; la
  substitution appliquée à un type qui n'est pas dans la table : refusée.

### Le coût

Chaque étape qui change l'émulateur porte la série entière dans les deux modes et une passe du banc, de plusieurs
heures chacune (PLAN-G14.md:388-389). Sept familles, N9, E1 et E2 font dix passes, contre quatre en G14. Regrouper des
familles réduit le nombre de passes et grossit la reprise d'une famille qui rougit (PLAN-G14, décision n° 11).

## 3. Les risques

- **Un changement de sens sans erreur de compilation** : un renommage qui masque (une locale, un paramètre) ou qui fait
  choisir une autre surcharge par deux `using static`. La vérification de liaison le refuse, l'IL le montre (§ 1.5).
- **Les couplages par chaîne** : neuf sites doivent suivre (§ 1.4), dont `"opFCLEX"`, qui ne casserait que le mode
  « tout » ; ~500 chaînes ne doivent pas suivre. Un outil textuel ou « rechercher dans les chaînes » les confondrait.
- **Les noms générés** : 1 264 champs `<>O` et 201 méthodes portent un nom de membre ; un vidage qui ne les traduirait
  pas sortirait des centaines d'écarts sans cause.
- **Les générateurs** : huit fichiers ; un renommage qui ne passe pas par eux est défait à la prochaine génération.
- **La conversion qui confond** : 30 paires du C distinguées par la casse (§ 1.1), trois CS0542, six noms qui
  commenceraient par un chiffre.
- **La porte de la doc qui passe à tort** : `check-doc5150.sh:67` trouve un symbole dans un commentaire.
- **Les lignes longues** : 1 844 lignes de plus de 120 caractères dans iXtal26 et 1 362 dans Diff aujourd'hui ; un
  renommage en touche au plus 1 018 et 518, dont 757 et 501 resteraient au-delà (borne haute, simulée par
  `collisions.py`, la conversion simple). Les recouper changerait le nombre de lignes vives (R2) et la mise en forme que
  `.editorconfig:59-69` protège.
- **Un enum imprimé** dans une sonde ou un journal change la sortie d'une série (le nom au lieu du nombre) ; une valeur
  réordonnée casse la parité avec le C par `h_set_*`.
- **La durée** des dix passes, et l'hôte qui dérive entre elles (le réancrage de PLAN-G14, la vérification).
- **Rider** : sa préversion de renommage ne voit ni Diff ni le banc (`iXtal26.sln`, R2-noms de G14 § 1.5), et ne sait
  pas les chaînes liées ; le script applique, Rider compte, comme Q38.

## 4. Les décisions ouvertes

Seulement ce que `PLAN.md` (§ G15, 03/10) et PLAN-G14 (décision n° 1) laissent ouvert.

- **Q1 — La forme d'un nom de PCem en .NET.** `PLAN.md` dit « PascalCase » sans dire comment l'on convertit `CS` et
  `cs`, `_808x`, `pc_timer_t`, `opADD_b_rmw`, `AL`, `IRQ`. *Proposé* : les règles de nommage du .NET (PascalCase, un
  sigle de deux lettres tout en majuscules, `CS`, `AL`, `IO` ; au-delà, une majuscule, `Fdc`, `Pit`), appliquées par un
  générateur ; le suffixe `_t` d'un type d'état tombe quand rien ne collisionne ; un conteneur qui commencerait par un
  chiffre prend le nom de sa famille (`_808x` → un nom à fixer, comme `Cpu808x`) ; chaque collision (les 30 paires, les
  trois CS0542) reçoit une exception écrite avec sa raison, et c'est cette liste, quelques dizaines de lignes, que tu
  valides, pas les 6 500 noms.
- **Q2 — Où vit la correspondance PCem ↔ iXtal26.** R1(e) l'exige « sans elle, plus rien ne relie le fichier à
  `pcem-dev/` » (`TRANSCRIPTION.md:26-27`), mais borne un bloc à 40 lignes et le réserve à `Floppy/`, `Disc/` et
  `Ics/` ; 64 fichiers ont plus de 40 noms à changer. *Proposé* : la table générée de `tools/noms/` est la
  correspondance (R3 range déjà les données volumineuses dans des fichiers générés, `:55-56`) ; les blocs `// noms:`
  gardent leur rôle (les noms modifiés à la main, les valeurs nommées, le fichier C) et disent la règle ; et chaque
  fonction renommée porte son nom C sur une ligne `// pcem:`, celle qui la précède, ou une ligne neuve de la forme R1(c)
  là où un marqueur de région en couvre plusieurs (`setadc8`, `Cpu/808x.cs:616`, est sous `// pcem: 808x.c:758-819`,
  `:591` ; le fichier a 23 marqueurs pour 42 méthodes de premier niveau, `grep -c`). Le marqueur est conservé, comme
  `PLAN.md:572` le veut, et `grep -n setadc8` retrouve la fonction des deux côtés, la propriété que
  `.editorconfig:30-33` défend. Ce sont des commentaires : l'IL n'en dépend pas, R2 non plus.
- **Q3 — Les fichiers.** Les types prennent la forme .NET, conteneurs compris ; leurs fichiers suivent-ils
  (`vid_cga.cs` → `VidCga.cs`) ? *Proposé* : non. Les fichiers restent le miroir du chemin C (`TRANSCRIPTION.md:133`),
  parce que `oracle.tsv` (259 lignes), `PCEM_BUGS.md` (614 chemins), `check-doc5150`, les `.cfg` et les renvois
  `fichier.cs:N` les nomment, et qu'un déplacement réordonne le tas `#US` (Q39) ; un conteneur et son fichier portent
  alors deux noms, et le bloc d'en-tête le dit.
- **Q4 — `materiel` et `materiel.pb_nn`.** G14 ne les a pas renommés, « PLAN.md § G14 ne nomme que les puces »
  (`.editorconfig:93-95`). La forme sert la règle de compte de R2 (`TRANSCRIPTION.md:43`), la garde de R10 (`:88`), 787
  gardes dans 76 fichiers et 183 mentions de `PCEM_BUGS.md`, et elle reflète le marqueur `PB-nn`. *Proposé* : la
  garder, en exception écrite de la règle de nommage, comme les P/Invoke et les DTO : elle est un marqueur autant qu'un
  nom, et la renommer changerait deux règles et un registre pour aucun gain de lecture.
- **Q5 — Quels `const` deviennent des `enum`, et quel type portent les champs.** *Proposé* : (a) les 43 familles d'un
  `enum` de PCem (267 constantes) et les familles de `#define` qui sont des ensembles d'états ou de commandes lus en
  `case` (`COMMAND_*`, `GPCMD_*`, `WIN_*`, `CMD_*`) deviennent des `enum`, et les champs qui ne tiennent que ces valeurs
  prennent leur type, hors de ceux que le harnais lit en `int` (casté au P/Invoke) ; (b) les 48 familles de bits restent
  des constantes, en PascalCase : la valeur d'un registre n'est pas l'une de ses valeurs nommées, chaque lecture et
  chaque écriture de port demanderait un cast, et `[Flags]` serait un C#-isme hors de la liste close de R4
  (`TRANSCRIPTION.md:59-64`) ; (c) les 199 isolées (tailles, adresses) restent des constantes. Le (b) s'écarte de la
  lettre de `PLAN.md:569-570` : c'est à toi de dire si « `enum` au lieu des constantes `int` » les couvre.
- **Q6 — Les lignes de plus de 120 caractères qu'un renommage touche.** La règle les compte avant chaque commit ; G14 a
  recoupé celles qu'il touchait (sept dans `EmuTablesCheck.cs`, commit `e5d51e0`). Ici, au plus 757 lignes d'iXtal26 et
  501 de Diff resteraient au-delà de 120 après le renommage, qui n'en allonge aucune dans la simulation (§ 3).
  *Proposé* : une ligne que le renommage seul n'allonge pas garde sa coupe ; toute ligne allongée, ou neuve, tient en
  120 ; l'outil le journalise, comme `tools/renommage` le fait déjà (`Program.cs:34-36`). Le recoupage changerait R2 et
  la mise en forme de plus de mille lignes du transcrit.
- **Q7 — SoftFloat.** `PLAN.md` dit « Normaliser le C# » ; `iXtal26.SoftFloat/` (59 types, 75 méthodes, tous en
  `snake_case`) a le pacte du transcrit (`.editorconfig:78-79`). *Proposé* : l'inclure, en dernière famille, avec ses
  propres portes (son banc `tools/softfloat/Banc`, le fuzzeur x87) ; sinon, une exception écrite.

Ne sont pas ouvertes, et le plan les suit sans question : `private` explicite (rien à écrire, l'IDE0040 seulement) ; les
étiquettes des sondes, les clés de configuration, les clés R9 et les noms de fichiers de PCem gardent leur forme
(`fdc.cs:48-49`, VERIFICATION.md:1550-1554) ; les P/Invoke et les DTO gardent les noms du C et du JSON
(`.editorconfig:112-119`) ; les paramètres et les locales suivent la forme .NET, qui en fait partie ; les registres
vivants suivent (`PCEM_BUGS.md`, `Docs/ibm5150/`), les journaux datés restent tels qu'écrits (PLAN-G14, décision
n° 10).

## Annexe A — Comment on a compté

Dans `/var/tmp/ixtal-g15/R0-noms/` (hors du dépôt) :
- `inv.py FICHIERS…` → `inv-<assemblage>.tsv` : une ligne par déclaration (fichier, ligne, genre, nom, accès explicite,
  modificateurs, conteneur, type). Les chaînes interpolées, verbatim et brutes sont retirées avec leurs trous ; les
  directives `#` sont sautées (une seule réelle, `PluginApi/device.cs:195`) ; `Program.cs` (une instruction de premier
  niveau) n'a pas de déclaration ;
- `md.py DLL [PDB]` → `md-<assemblage>.tsv` : les tables `TypeDef`, `Field`, `MethodDef`, `Param`, `NestedClass`,
  `Constant`, et `LocalScope`/`LocalVariable` du PDB (dont les index de `MethodDef` se dimensionnent par le flux
  `#Pdb`) ;
- `cmp.py` : le recoupement par (conteneur, nom), les méthodes `SpecialName` (accesseurs, constructeurs) et `Invoke`
  des délégués retirées ;
- `conforme.py` : la forme .NET par genre (§ 1.1) ; `classer.py` : les formes, les noms de PCem, les répertoires ;
  `collisions.py` : la conversion simple et ses collisions ; `consts.py` → `consts.tsv`, puis les familles
  (`groupes.tsv`).
Les `grep` sont dans le texte, à côté de chaque chiffre.

## Annexe B — Hors de la portée

Trouvé en route, sans proposition :
- `Ide/ide.cs:145` déclare un `record struct` dans le code transcrit, que R4 exclut (`TRANSCRIPTION.md:57-58`) ;
- `iXtal26.csproj:71` ouvre ses internes à `iXtal26.Sst`, un projet qui n'existe pas ;
- 82 constantes entières ne sont jamais lues (`consts.tsv`, colonne des usages à 0) ;
- `iXtal26.dll` a 47 champs de classes de fermeture (`<>c__DisplayClass`) et 68 de `<>c` dans les types de `Cpu/` (leurs
  sites ne sont pas lus) ; un listing de R montre `call [iXtal26.Cpu.x86:get_AX():byref]` dans `opMOV_AX_imm`, non
  inliné ;
- `.editorconfig:40` ne couvre ni `Cdrom/`, ni `Ide/`, ni `Joystick/`, ni `Lpt/`, ni `Mfm/`, ni `Scsi/` ;
  `EnforceCodeStyleInBuild` n'est posé que dans `iXtal26.csproj`.
