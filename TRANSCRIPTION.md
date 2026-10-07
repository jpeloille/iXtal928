# Transcription PCem → C#

Seul fichier de prose du dépôt. Plafond : 260 lignes, voir R3. On l'amende quand une *règle*
change, jamais pour expliquer une ligne de code. Le projet précédent
(`~/RiderProjects/Retro/IXtal`) s'est arrêté à `NOP` après 1 490 lignes dont ~1 100 de
commentaires de conception : les règles ci-dessous sont les anticorps de ce mode d'échec,
comptables ou greppables, pas déclaratives.

Les **mesures** (résultats de portes, divergences, injections de panne) vont dans
`VERIFICATION.md` : elles grandissent à chaque jalon et n'ont pas à être rationnées.

---

## Règles

- **R1 — budget de commentaires, énuméré.** Un fichier transcrit contient exactement
  quatre sortes de commentaires :
  (a) l'en-tête : deux lignes SPDX + quatre lignes `// ORACLE:` ;
  (b) les commentaires de PCem, *vivants*, verbatim ;
  (c) une ligne de provenance par fonction, `// pcem: src/cpu/808x.c:1271-1301` ;
  (d) `// omitted:`, `// DEVIATION:`, `// pcem bug, reproduced:` / `not reproduced:` (R9) /
  `fixed in hardware mode:` (R10), `// CS0165:` ;
  (e) dans `Floppy/` et `Disc/` seulement, un bloc `// noms:` en en-tête : une ligne par
  identifiant, **sans phrase**, 40 lignes au plus. Deux colonnes — l'identifiant, ce
  qu'il désigne — et **trois dès qu'un nom est modifié** : nom PCem, nom iXtal26, ce
  qu'il désigne. Contrepartie obligatoire du nommage explicite autorisé plus bas — sans
  elle, plus rien ne relie le fichier à `pcem-dev/`.
  Le commentaire de 140 lignes qui explique un choix de conception reste hors budget :
  (e) est une table, pas un exposé. Le code mort commenté de PCem n'est pas reproduit.
- **R2 — parité de lignes.** Le C# vivant d'une région se compare au C vivant
  correspondant (blancs et commentaires exclus des deux côtés). **Le plafond +25 % est
  toujours contraignant** : c'est lui l'anticorps anti-délayage, déclenché ⟹ on supprime,
  on ne négocie pas. Le plancher −25 % ne s'applique qu'aux fichiers `status:
  transcribed` : un `partial` est plus court par construction, chaque ligne absente étant
  couverte par une entrée du registre des omissions. Être plus court sans omission
  déclarée, en revanche, c'est du code oublié — et ça, R6(a) l'attrape.
  **La règle de comptage est une COMMANDE, pas une intention** — quatre conventions
  incompatibles ont cohabité dans une même reconnaissance du bloc C, donnant 195 et 389
  lignes vives pour le même corps :
  `grep -vE '^\s*(//|/\*|\*/|\*|$)' F | grep -vE '^\s*[{}();]+\s*$' | wc -l`.
  Depuis G13, un filtre de plus avant le compte retire les seules gardes du mode matériel
  (R10) : `grep -vE '^\s*(else\s+)?if \(!?materiel\.pb_[0-9]+\)\s*$'`.
  **Les lignes « accolade seule » sont exclues du décompte des deux côtés.** PCem est en
  K&R, le C# du dépôt en Allman : chaque bloc coûte mécaniquement +1 ligne, sans qu'une
  seule instruction ait été ajoutée. Mesuré sur `timer.cs` : 1,27 brut, **1,09** hors
  accolades. Sans cet ajustement R2 déclencherait sur la mise en forme, ce qui le
  rendrait ignorable — et un garde-fou qu'on ignore ne garde plus rien.
- **R3 — un seul fichier de prose, plafonné.** Celui-ci, 260 lignes (240 jusqu'à G13.2,
  relevé pour R10 : décision n° 15 de `PLAN-G13.md`). Un relèvement
  s'inscrit, avec sa raison : un plafond qui bouge sans trace ne plafonne plus (historique :
  `iXtal26/Docs/doctrine-historique.md`). Exemptés, parce que ce sont des **constats** et non
  de la prose de conception : `VERIFICATION.md` (ce que les oracles ont montré) et
  `PCEM_BUGS.md` (les défauts de PCem, `PB-nn`, cités par les marqueurs `// pcem bug, …` du
  code). Les données volumineuses vont dans des fichiers générés (`sst-baseline.tsv`,
  `oracle.tsv`), jamais ici.
- **R4 — zéro abstraction.** Pas d'`interface`, `abstract class`, générique, LINQ,
  `record`, `async`, DI, méthode d'extension. Le C du palier (a) n'en contient aucun.
  Les sept C#-ismes autorisés sont une liste close :
  `delegate` · `class` (structs dont l'adresse est prise) · propriété `ref` (`#define`) ·
  `[StructLayout(Explicit)]` (unions) · alias `global using` (stdint) ·
  `[MethodImpl(AggressiveInlining)]` (macros-fonctions) · `ref` locale d'alias en
  prologue de `execx86` — et là seulement : contournement d'une limite de RyuJIT,
  mesuré, VERIFICATION.md § M5.
- **R5 — cadence de build : 300 lignes.** Jamais plus de 300 lignes neuves sans
  `dotnet build` **et** exécution du vérificateur du jalon courant.
- **R6 — « fini » par fichier**, quatre conditions : chaque ligne C a une contrepartie ou
  un `// omitted:` ; le harnais différentiel est vert ; R2 tient ; **zéro `TODO`** — un
  chemin non implémenté est `fatal("not implemented: 808x.c:1234")`.
- **R7 — un commit par région transcrite**, message
  `transcribe: src/cpu/808x.c:1271-1400 (opcodes 00-1F)`. `git log --oneline` *est* le
  rapport de couverture.
- **R8 — verbes interdits** jusqu'à M4 dans un commit ou un commentaire : *improve,
  optimise, clean up, simplify, refactor*. Si le C est faux, on transcrit la fausseté et
  on la marque `// pcem bug, reproduced:` ; le mode matériel la corrige à côté, sans la
  retirer (R10).
- **R9 — l'invité ne tue pas l'hôte** (1er octobre 2026, décision de Julien). Un défaut de PCem
  par lequel le code invité arrête, plante ou fige l'émulateur (`fatal()`, SIGFPE, SIGSEGV,
  récursion sans borne) n'est PAS reproduit : le comportement du matériel s'il est connu, le
  plus sûr sinon, `// pcem bug, not reproduced: PB-nn` + `// DEVIATION:`, et l'entrée PB le dit.
  Identité stricte partout où l'oracle survit ; l'oracle vendoré n'est jamais touché — les
  outils l'écartent de ces chemins, un test ciblé en C# seul prouve la survie. Hors R9 : les
  arrêts « non transcrit » d'iXtal et les défauts sans arrêt de l'hôte, qui restent reproduits.
- **R10 — le mode matériel** (G13, décision de Julien du 7 octobre 2026). Un défaut de PCem
  reproduit (`PB-nn`, sections A et B de `PCEM_BUGS.md`) se corrige À CÔTÉ, jamais à la place :
  la ligne fausse reste transcrite, et le mode PCem — le défaut, celui des portes — l'exécute.
  La correction est gardée par un champ figé avant le premier cœur, lu directement par une
  garde seule sur sa ligne (`if (materiel.pb_nn)`), que le JIT plie : le mode PCem ne coûte
  rien, les listings du JIT le prouvent (`tools/listings-jit.sh`). Le site prend `// pcem bug,
  fixed in hardware mode: PB-nn` ; l'entrée PB nomme la source, le cas qui discrimine et la
  panne qui le rougit, dans son champ *Corrigé en mode matériel*. Le code propre au mode va
  dans un `*.Materiel.cs` (`STATUS: materiel`) : son marqueur tient lieu de provenance (R1 c),
  R2 et R6 ne s'y appliquent pas, son vérificateur est le cas qui discrimine, en C# seul. Dans
  un fichier généré, la garde passe par le générateur. L'oracle n'a pas de mode matériel : un
  outil qui le compare refuse le mode (retour 2), hors le contrôle de fuite déclaré. Le code
  du mode reste déterministe, sans fonction de l'hôte à résultat variable, sans arrêt (R9).

### Portée

Le code **transcrit** (`Cpu/`, `Memory/`, `Models/`, `Keyboard/`, `Video/`, `PluginApi/`,
`Floppy/`, `Disc/`, `io.cs`, `timer.cs`, `pc.cs`, `ppi.cs`) garde les identifiants et commentaires anglais de
PCem ; `.editorconfig` y neutralise les règles de style qui réécriraient une ligne.

Le code **hôte**, neuf (`Host/`, `Materiel/`, `Program.cs`), suit les conventions C#
normales et reste commenté en français ; un `*.Materiel.cs` rangé dans un répertoire
transcrit suit R10. `TreatWarningsAsErrors` est plein partout ; sur les fichiers
transcrits, les avertissements du compilateur se neutralisent par `#pragma warning
disable` **énumérés et commentés**, jamais en bloc.

Exception : `Program.cs` et `Host/CommandLine/` n'ont aucun commentaire hors de leur
en-tête. Leurs noms disent ce que fait le code, et le pourquoi est dans
`iXtal26/Docs/ligne-de-commande.md`.

---

## Conventions de transcription

| Construction C | Rendu C# |
|---|---|
| Globales d'un fichier | `static` sur `internal static partial class` du même nom ; ailleurs `using static` |
| `#define cycles cpu_state._cycles` | propriété `ref` : `static ref int cycles => ref cpu_state._cycles;` |
| Union `x86reg` | `[StructLayout(LayoutKind.Explicit)]`, `l`@0 `w`@0 `b.l`@0 `b.h`@1 |
| Struct dont l'adresse est prise et stockée | **classe** (`x86seg`, `cpu_state_t`, `pc_timer_t`, `cga_t`) |
| Struct seulement copiée par valeur | **struct** (`x86reg`) |
| `readlookup2[]` de `uintptr_t` biaisés | `int[]` d'offsets dans `byte[] ram` ; `ram[readlookup2[a>>12] + a]` |
| Pointeurs de fonction | types `delegate` nommés, tableaux dimensionnés comme en C |
| `void *p` | `object` + cast explicite dans le handler |
| `(void *)f` passé à `timer_add`, `f` sans paramètre | `f(object? p)` ; les appels directs passent `null` (`fdc_callback`) |
| `uint8_t *p` dans le tampon d'un autre module (`sector_t.data`) | le tampon **et** l'offset : `uint8_t[] data; int data_off;` |
| `uint8_t`… | alias `global using` (voir `GlobalUsings.cs`) |
| `goto opcodestart` | `goto opcodestart` |
| Nommage | `snake_case` de PCem **verbatim** ; `Floppy/` et `Disc/` exceptés, voir ci-dessous |
| Fichiers | miroir du chemin C : `src/cpu/808x.c` → `Cpu/808x.cs` |

Trois corollaires non négociables :

1. **`regs` est un champ tableau `x86reg[8]` nu.** Jamais une propriété, jamais `List<T>`,
   jamais `readonly`. À travers un getter de propriété on mute une *copie* et l'écriture
   disparaît sans diagnostic. Corollaire général : **pas de `readonly struct`, pas de
   paramètre `in`, pas de propriété à valeur struct** dans l'état mutable.
2. **Les globales passées hors-bande restent des globales** : `mem_logical_addr`
   (`mem.c:53` → lu par `mem_read_ram` `:828`), `mmu_perm`, et `tempc` (capturé à
   `opcodestart`, `808x.c:1248`, consommé par `setadc8`/`setsbc8`). En faire des
   paramètres est l'amélioration évidente, et c'est exactement ce qui fait cesser à
   l'oracle d'être un oracle.
3. **Dans les helpers de flags (`808x.c:748-886`), tout intermédiaire est `int` ou
   `uint`, jamais `byte`/`ushort`.** `setsub8` fait `((a & 0xF) - (b & 0xF)) & 0x10` : en
   C c'est de l'arithmétique `int` qui donne `-1` puis `0x10`, donc AF posé. Typer
   l'intermédiaire en `byte` « pour coller aux types C » fait disparaître l'emprunt
   silencieusement, et AF n'est observable qu'à travers DAA/DAS/AAA/AAS.
   Corollaire : **ne jamais déplier une affectation composée** — C# insère le cast dans
   `AL += temp` (759 occurrences dans `808x.c`) mais rejette `AL = AL + temp`.

### Nommage explicite — `Floppy/` et `Disc/` seulement

Le vocabulaire du 765 (`stat`, `res`, `pnum`, `tc`, `discint`, `paramstogo`) ne se décode
pas sans la fiche technique, et l'opacité est autant dans les littéraux que dans les noms.
Deux déviations y sont permises, **et elles vont ensemble** : renommer un identifiant
(`stat` → `msr`) ; nommer une valeur de registre matériel (`0x90` → `MSR_RQM|MSR_CB`), ce
qui réécrit une expression de PCem et pas seulement une étiquette.

Trois conditions, toutes vérifiables : le bloc `// noms:` de R1(e) donne la correspondance
pour **chaque** nom modifié ; le commit ne contient que le renommage ; la batterie
d'oracles est verte au même commit, aux mêmes chiffres qu'avant. Le renommage est neutre
pour le comportement — c'est l'oracle qui le prouve, pas la relecture. Hors de ces deux
répertoires, la règle reste le verbatim : `808x.cs` dit en en-tête pourquoi.

### Collisions de mots-clés C#

Scan mécanique des en-têtes du palier (a), puis de `fdc.c` à M6 et de `vid_svga.h` à M15. **Cinq entrées :**

| C | C# | Où |
|---|---|---|
| `base` | `@base` | `x86seg.base` (`x86.h:38`), `mem_mapping_t.base` (`mem.h`) |
| `checked` | `@checked` | `x86seg.checked` (`x86.h:44`) |
| `params` | `@params` | `FDC.params` (`fdc.c:33`) |
| `lock` | `@lock` | `FDC.lock` (`fdc.c:39`) |
| `override` | `@override` | `svga_t.override` (`vid_svga.h:139`) |

Le préfixe `@` est choisi pour que `grep -n base` retrouve encore la ligne.

Collisions conteneur/membre : `x86seg.c` et le typedef `x86seg` ; `fdc.c` et son instance
`static FDC fdc` ; `fdd.c` et `fdd[2]` ; `model.c` et `int model` (M8) ; `cpu.c` et
`int cpu` (M16). Le conteneur prend `_c` (`x86seg_c`, `fdc_c`, `fdd_c`, `model_c`, `cpu_c`),
le membre garde son nom. `pit.c`/`PIT`, `timer.c`/`pc_timer_t`, `mem.c`/`mem_mapping_t`,
`vid_cga.c`/`cga_t`, `device.c`/`device_t` sont tous distincts.
`808x.c` → classe `_808x` (un identifiant C# ne peut pas commencer par un chiffre ; le
nom de fichier, si).

---

## Registre des omissions

Ce qui n'est **pas** transcrit, et pourquoi. Toute nouvelle entrée se justifie ici.

| Omis | Citation | Raison |
|---|---|---|
| ~~`magic` / `TIMER_MAGIC` / `all_timers[256]` / `timer_valid()`~~ — **entrée révoquée, transcrite depuis** | `timer.h:19`, `timer.c:16-21` | Le motif inscrit ici (« use-after-free, inatteignable sous GC ») était faux. `timer_reset()` remet `magic` à zéro sur tous les chronomètres enregistrés (`timer.c:162`) et `timer_valid` les rend alors inertes : c'est du contrôle de flux vivant. Coût de l'erreur : la machine tournait sans PIT. Voir VERIFICATION.md § M4.0. Seules restent omises les branches de récupération de liste corrompue (`timer.c:49-65, 117-122, 133-137`), leurs `pclog` étant des sorties pures et le nœud corrompu inatteignable sous GC. |
| **Déviation de l'ORACLE** (pas d'iXtal26) : `h_pad_ram()` réalloue le `ram` de PCem avec 4 octets à zéro | `mem.c:1344`, `808x.c:79` et `:110` | PCem alloue `ram` à exactement `mem_size` Ko puis lit un `uint16_t` au sommet de l'espace : un octet **hors allocation**, donc du tas. Trois exécutions, trois valeurs. Pas un comportement, de l'UB — il n'y a rien dont être le pendant fidèle, et un oracle qui tire aux dés n'est pas un oracle. `mem_alloc` (`Memory/mem.cs`) a la même marge ; PB-07, marqueurs `not reproduced`. VERIFICATION.md § M4.0. |
| `video_recalctimings` | déclaré `video.h:64` | Mort : jamais assigné, jamais appelé ; seule référence dans le bloc commenté `pit.c:54-55`. |
| Ce qu'une VGA n'atteint pas dans le socle SVGA : les rendus `null`, `text_80_ksc5601`, `ABGR8888`, `RGBA8888`, les visionneuses ; `ps1vga_init` ; les 44 autres `VIDEO_CARD` | `vid_svga_render.c:8-12, 161-281, 856-916` ; `vid_vga.c:120-136` ; `video.c:69-193` | `vid_svga.c` est le socle GÉNÉRIQUE des SVGA de PCem, et la VGA d'IBM n'en emprunte qu'une partie — qui se MESURE : `vga_init` pose `bpp = 8` et rien dans `vid_vga.c` ni `vid_svga.c` ne le change ; `fast` exige `packed_chain4` ou `fb_only`, que seuls d'autres pilotes posent ; aucune fenêtre linéaire n'est enregistrée. Compteurs de passage sur la campagne graphique de § M15 : sept des neuf rendus que `svga_recalctimings` peut choisir, les quatre modes d'écriture. Les rendus que `vid_svga.c` NOMME restent, en `fatal()`, pour que ses comparaisons de pointeurs (`:384-415`, `:694-696`) restent verbatim. Depuis M19 le RAMDAC TKD8001 de la Trident 8900D pose `bpp` à 15, 16 ou 24 : ces six rendus et `video_15to32`/`video_16to32` sont transcrits, des deux côtés. Depuis G7.0, la fenêtre linéaire (`svga_*_linear`, `vid_svga.c:1137-1658`) et les rendus 32 bpp le sont aussi, pour les cartes de G7 (VERIFICATION.md § G7.0). Le registre `VIDEO_CARD`, lui, est transcrit, réduit à `v_cga` et `v_vga` : avec une carte il était une constante. Depuis M19 il porte aussi `v_tvga8900d` et `v_tvga9000b`, depuis G7.1 `v_cl_gd5429` (`vid_cl5429.c`, la GD5429 seule ; MCA, PCI et les dix autres cartes de la famille omis), depuis G7.3 `v_px_trio64` (`vid_s3.c`, la Trio64 Phoenix seule ; Vision864, 9FX, Trio32 et le SDAC qu'elles tirent omis — l'oracle lie `vid_sdac_ramdac.c`, que la Trio64 n'appelle pas), depuis G1.1 et G1.2 la vidéo par le romset de l'Olivetti M24 et de l'Amstrad PC1512 (`video.c:197`, `:246-247`, `:264-265`, `:433`, `:470`, `:512`, `:614-615`, `:629-630`, `:775-777`, `:800-802`), et `video_updatetiming` sa branche `video_speed == -1`, dont seul le `switch (romset)` reste omis (`video.c:607-718`, branches non prises). VERIFICATION.md § M15, § M19. |
| La manette sur les machines existantes (`device_add(&gameport_device)` de `xt_init` et `at_init`, `model.c:205`, `:347`) | `model.c` | **G1.1** : `gameport.c` et `joystick_standard.c` sont transcrits (`Joystick/gameport.cs`), sans manette branchée (décision n° 2 de PLAN-G1.md), pour l'Olivetti M24 et l'Amstrad PC1512 dont les inits l'ajoutent. Les machines déjà au dépôt — 5150, XT, AT, AMI — l'omettent toujours, des deux côtés : l'y ajouter changerait le port 201h de toutes les mesures consignées, hors du périmètre de G1. Trois manettes (`ch_flightstick_pro`, `sw_pad`, `tm_fcs`) sont omises de `joystick_list`. **G1.2** : de `lpt.c`, seuls `lpt1_read` et `lpt1_write` (`Lpt/lpt.cs`), qu'`amstrad.c` appelle pour 378h-37Ah, sans périphérique branché (`lpt1_device` nul), recopiés à l'identique dans l'oracle ; `lpt_init`, `lpt1_remove` et `lpt2_remove_ams` (des retraits de gestionnaires jamais posés) restent omis ou souchés, des deux côtés. **G10.0 : levée.** `lpt.c`, `lpt_dac.c` et `lpt_dss.c` transcrits (`Lpt/`) et liés dans l'oracle ; `lpt_init` dans `common_init` et le port jeu dans `xt_init`/`at_init`, des deux côtés, sur toutes les machines ; seules restent omises les trois manettes, jusqu'à G10.1. **G10.1 : levée.** Les trois manettes transcrites (`Joystick/joystick_ch_flightstick_pro.cs`, `joystick_sw_pad.cs`, `joystick_tm_fcs.cs`), `joystick_list` à sept types, liées dans l'oracle ; `joystick_type` et la section [Joysticks] lus (`pc.c:783-805`) ; `joystick_poll` par l'hôte (`Host/SdlJoystick.cs`, SDL3). |
| Le THREAD de l'accélérateur S3 (`fifo_thread`, `thread_create`, `thread_set_event`, l'attente sur `fifo_not_full_event`) | `vid_s3.c:840-913` | **Décision utilisateur du 01/10 (G7, n° 3)** : un oracle à thread n'est pas déterministe. L'accélérateur est rendu SYNCHRONE DES DEUX CÔTÉS : côté oracle, `harness_s3.c` souche `thread_create` et enveloppe (`--wrap`) `thread_set_event`, qui vide la FIFO par la boucle de `fifo_thread` recopiée — le PCem vendoré reste intact ; côté C#, la même chose. DEVIATION, des deux côtés. **Effet observable** : la carte n'est jamais « occupée » (bit d'occupation du moteur toujours à zéro à la relecture) et la FIFO ne se remplit jamais. PLAN-G7.md, décision n° 3. |
| `old_fp_control`, `new_fp_control`, `trunc_fp_control` | `x86.h:100-106` | Sauvegarde du mot de contrôle x87 de l'hôte, sous `#if defined __i386__`. Sans objet en .NET. |
| Les vues `l`/`w`/`b` de l'union `MMX_REG` | `x86.h:46-56` | MMX, hors de portée. `ST[8]`, `TOP`, `tag[8]`, `npxs`, `npxc`, `MM_w4`, `ismmx` et `MM[].q` sont entrés en G4.0 comme **stockage** (le x87 lit `.q` pour FILD/FISTP 64 et FSAVE/FRSTOR), comparés par `h_state` ; aucun handler ne les écrit avant G4.2. |
| `smi_pending`, `smbase`, `op32`, `cpu_recomp_ins` | `x86.h:85, 116` | SMM / 32 bits / dynarec. `cr0` est conservé : `mem.c:429, 448` testent `cr0 >> 31`, toujours 0 sur XT. |
| `_mem_exec[]`, `getpccache`, `page_lookup`, `pages[]`, `mmutranslatereal` | `mem.c:17-18, 28, 431-438` | Chemins 386/dynarec/pagination. `808x.c` n'y touche pas (0 occurrence). Attention : `mem_write_ram*` passe `&pages[addr>>12]` — supprimer `pages` **réécrit** le chemin d'écriture, marquer `// DEVIATION:`. |
| Chemin composite CGA (`Composite_Process`, `cga->composite`) | `vid_cga.c:~380` | Inutile au palier (a), et contient un vrai bug de PCem (écriture octet via lecture dword). L'éviter esquive la question de politique de bug. |
| `hline`, `create_bitmap`, `destroy_bitmap`, `screen` comme remontées cœur→hôte | déclarés `video.h`, définis `wx-sdl2-video.c:15,49,57,59` | Inversion de dépendance. `hline` (appelé `vid_cga.c:275`) devient un `Array.Fill` sur le buffer plat. |
| `src/wx-ui/`, `src/qt-ui/`, `src/codegen/`, `src/dosbox/` (hors `dbopl.cpp`, G8, et `cdrom_image.cpp`, G10.3), `thread-pthread.c` | — | Remplacés par l'hôte SDL3 mono-thread. **Trois exceptions**, toutes portées avec leur oracle, le contenu des commandes étant celui de PCem et seule l'enveloppe wxWidgets disparaissant : les gestionnaires de menu disquette et reset (`wx-sdl2.c:725-770`) et la création d'image de disquette (`wx-createdisc.cc:22-29, 62-73`) dans `Host/SdlMenu.cs` ; la création d'image de **disque dur** (`wx-config.c:1427-1440` create_drive_raw, `:1295-1302` hd_types[46], `:1580-1586`, `:1607-1633`, `:1682-1683`, `:1722-1731`) dans `Host/HddImage.cs` ; le **Configuration Manager** et la composition d'une machine (`wx-config_sel.c` en entier, `wx-config.c:658-675`, `:742-753`, `:773-792`, `:2038-2120`, et `:139-161` pour la carte vidéo depuis M15) dans `Host/SdlSetup.cs`, dont restent omis « Parcourir... » — du code à rendez-vous entre deux fils, déjà porté une fois dans `SdlMenu` — et tout ce que la boîte de PCem règle et que ces machines n'ont pas : CPU, FPU, dynarec, waitstates, carte son, CD-ROM, ZIP, LPT, souris, joystick, réseau — les cinq clés du CD-ROM sont enregistrées depuis G10.4, `zip_channel` et `zip_path` depuis G10.6, sans ligne à l'écran. De `hdnew_dlgproc` restent omis : les trois formats VHD de `hd_format` 1/2/3 (`:1645-1668`, couverts par l'omission `minivhd/`), la saisie par taille en Mo (`:1736-1754`) qui force 63/16, et la branche NON-MFM de `check_hd_type` (`:1358-1380`), heuristique à seuils pour l'IDE et le SCSI, dont ce dépôt n'a aucun contrôleur. Sa branche MFM (`:1340-1357`), elle, est transcrite depuis M13 : l'omission disait « il sert le sélecteur de fichiers du dialogue, que ce dépôt n'a pas », et `--hdd` est ce sélecteur. VERIFICATION.md § M12.1 et § M13. |
| `disc_fdi.c`, `fdi2raw.c`, l'entrée `"FDI"` de `loaders[]` et `fdi_init()` | `disc.c:53`, `pc.c:281` | Format de flux FDI : 448 + 2 700 lignes pour un format que ni le 5150 ni DOS ne produisent. L'oracle stube `fdi_load`/`fdi_close` à vide (`harness_stubs.c`). |
| `fdc37c665.c`, `fdc37c93x.c` | `src/floppy/` | Super I/O de cartes 486/Pentium, hors cible ; leurs accesseurs `fdc_update_*` restent transcrits, `fdc_init` les appelle. |
| Les DEUX tiers de configuration (`pcem.cfg` global + `configs/<nom>.cfg` machine), `add_config_callback`, `config_dump`, `config_new` | `config.c:7-11, 46-69, 221-224, 458` ; `pc.c:613` | Le tiers GLOBAL (`pcem.cfg`) seul : deux tiers ne paient rien avec une seule machine, et trois clés y existent en double chez PCem (`vid_resize`, `video_fullscreen_*`), la valeur machine écrasant la globale par accident d'ordre. Le tiers MACHINE, lui, n'est plus omis depuis M13 : `configs/<nom>.cfg` est écrit et relu par l'écran de `--setup`, ce qui donne enfin un appelant aux six `config_set_*` et à `config_save`, morts et pourtant vérifiés par `config-check` depuis M8. **Les défauts de `config.c` sont CORRIGÉS et non reproduits** — il n'est pas lié dans l'oracle (`tools/oracle/Makefile`), donc aucun pendant exécutable ne les exécute. VERIFICATION.md § M8. |
| `pclog(...)` dans les fichiers de M6 | `disc.c`, `disc_img.c`, `fdc.c:975` | Sorties pures, marquées `// omitted:` sur place. |
| Le registre `SOUND_CARD` complet ; le fil CD comme fil | `sound.c:31-105` ; `:143-148`, `:207-208` | Dix-neuf cartes son sur un 5150 de 1981. `sound.c` n'est donc **pas lié** à l'oracle, même arbitrage que `video.c` et ses 90 symboles de cartes : `harness.c` en reprend le cœur temporel, copié verbatim, et c'est ce qui porte le chronomètre à 48 kHz des deux côtés. VERIFICATION.md § M9. G8.1 a pris trois entrées du registre (none, adlib, sbprov2), G12.0 quatre de plus (sb, sb1.5, sb2.0, sbprov1), G12.1 et G12.2 les deux dernières de l'ISA (sb16, sbawe32) ; les cartes MCA (adlib_mca, sbmcv, sbpromcv), l'AdLib Gold et le CMS restent omis (PLAN-G12.md, décision n° 1). G10.5 a pris le corps du fil CD (`:149-195`), appelé à l'échéance de `sound_poll` des deux côtés : le fil lui-même, son attente et son événement, restent omis (décision n° 7 de PLAN-G10, PB-124). |
| `gated` | `sound_speaker.c:5` | Mort chez PCem aussi : sa seule référence est le `printf` commenté `:20`. `speakval`, `speakon` et `ppispeakon`, déclarés sur la même ligne, sont vivants et transcrits. |
| `speaker_mute` du PCjr, `sn76489`, `pssj` | `keyboard_pcjr.c:129-132`, `model.c:228` | Le 5150 n'a que le haut-parleur du canal 2 du PIT : `m_ibmpc` a `device = NULL` (`model.c:777`) et `xt_init` n'ajoute aucun périphérique sonore. `speaker_mute` **est** transcrit — c'est `speaker_init` qui le pose — mais rien ne le met à 1. |
| Le registre `HDD_CONTROLLER` : `hdd_controllers[]`, ses seize entrées et ses huit accesseurs | `hdd.c:28-182` | Seize contrôleurs de disque dur, dont un seul est câblé. Même arbitrage que `SOUND_CARD` (§ M9) — et que `VIDEO_CARD` jusqu'à M15, où la VGA l'a fait transcrire, réduit à deux entrées : un registre de cartes enfichables ne porte pas de temps. `hdd_controller_init` se réduit à son unique effet, un `device_add`, écrit sur place dans `pc.resetpchard()`. Mais `hdd.c` n'est **pas** entièrement écartable : il définit `hdc[7]` et `hdd_controller_name[16]`, seules définitions de l'arbre, transcrites dans `Disc/hdd.cs`. VERIFICATION.md § M12. |
| `src/ide/` (2 032 lignes) et `src/scsi/` (8 180 lignes) | — | Contrôleurs IDE/ATAPI et SCSI, et les cartes qui les portent. Aucune machine du dépôt n'en a : le 5150 n'a pas de disque dur, le 5160 a le Fixed Disk Adapter, qui est MFM. **Exception :** `ide_fn[7][512]`, défini `ide.c:105`, est transcrit dans `Disc/hdd.cs` — `mfm_xebec.c:26` le re-déclare `extern`, comme cinq autres consommateurs, et sans lui la carte ne sait pas quel fichier ouvrir.  **G5 et G10.2 :** `ide.c` (le disque dur, sans ATAPI) et `xtide.c` (la version XT, `Ide/xtide.cs`) transcrits ; restent omis `xtide_at`, `xtide_ps1`, `ide_atapi.c` et le SCSI. **G10.3 :** le type `ATAPI`, la table du pilote de CD, et le pointeur `atapi` (`ide_atapi.h:7-29`, `ide_atapi.c:26`, `Ide/ide_atapi.cs`). **G10.4 :** le pont (`ide_atapi.c` entier), le bus que parle le pont (`scsi.c`, `Scsi/scsi.cs`) et le lecteur de CD-ROM (`scsi_cd.c`, `Scsi/scsi_cd.cs`), les crochets d'`ide.c` ; **G10.6 :** le lecteur ZIP (`scsi_zip.c` entier, `Scsi/scsi_zip.cs`). **G11.0 :** le bus des cartes SCSI (`scsi_bus_init`, `scsi_bus_close`), le disque (`scsi_hd.c` entier, `Scsi/scsi_hd.cs`) et l'Adaptec AHA-1542C (`scsi_aha1540.c`, `Scsi/scsi_aha1540.cs`) ; restent omis la BusLogic BT-545S et `MB_FORMAT_8` (mort sur la 1542C), `scsi_53c400.c`, `scsi_ibm.c`, `hdd_esdi.c`, `xtide_at` et `xtide_ps1` (exclus, PLAN.md). |
| Le lecteur de CD-ROM physique de l'hôte : `cdrom-ioctl.c`, `cdrom-ioctl-linux.c`, `cdrom-ioctl-osx.c`, `cdrom-ioctl-dummy.c` | `pc.c:292-311`, `:322-333`, `:413-433` | Un lecteur physique n'est pas déterministe : des images seulement (PLAN.md, bloc G10). Restent ses deux globales, `cdrom_drive` et `old_cdrom_drive` (`cdrom-ioctl-linux.c:23-24`), dans `Cdrom/cdrom-ioctl.cs`. Le moteur d'images (`cdrom_image.cpp`, `cdrom-image.cc`) et le lecteur sans disque (`cdrom-null.c`) sont transcrits en G10.3. G10.4 : la clé `cdrom_drive` ne reçoit que -1 et 200 (l'image), défaut -1 là où PCem met 0, le lecteur physique ; `cdrom_device_path` est omise. |
| `src/hdd/minivhd/` (3 723 lignes) et `src/hdd/ramdisk/` (273 lignes) ; les branches `HDD_IMG_VHD` et `HDD_IMG_RAW_RAM` de `hdd_file.c` | `hdd_file.c:12-24, 42-64, 96-147` et les quatre fonctions d'E/S | Images VHD différentielles et disques en RAM. **1 797 des 3 723 lignes** (`cwalk.c`, `libxml2_encoding.c`) ne servent qu'aux chemins parents UTF-16 des VHD différentiels — un 5160 avec une image brute n'en emprunte pas une ligne. Les deux prédicats d'aiguillage, `mvhd_file_is_vhd` (par contenu) et `is_ramdisk_file` (par extension), sont rendus **faux** : deux stubs contre 3 996 lignes. Côté oracle, dix-sept stubs d'édition de liens dans `harness_stubs.c`. |
| La queue de `x86seg.c` : `sysenter`, `sysexit`, les six fonctions SMM, les deux Cyrix, `stimes`/`dtimes`/`btimes`, `breaknullsegs`, le prototype `taskswitch386` | `x86seg.c:22-24, 30, 35, 2851-3187` | 273 lignes vives, 11 % du fichier, **et la TABLE le dit, pas la fiche du 286** : les pointeurs de `ops_286` et `ops_286_0f` lus dans les RELOCATIONS de la `.so` puis croisés avec la table de symboles complète — les handlers sont `static`, donc invisibles à `nm -D` et à `dladdr`, ce qui a fait échouer deux tentatives avant celle-là — donnent 251 handlers distincts pour la première, dont **aucun** de SMM, sysenter ou Cyrix, et 7 pour la seconde, à l'identique de ce que `faaf0fb` avait lu au gdb. Reste `x86_smi_trigger`, seul point d'entrée hors table : ses appelants sont `models/piix.c`, `vt82c586b.c`, `piix_pm.c` et `sio.c`, dont **aucun n'est lié** à l'oracle. `taskswitch386` est orphelin chez PCem lui-même — aucune définition dans tout l'arbre. |
| Les branches par `romset` de `keyboard_at.c` : T3100E, Xi8088, GRID1520, SPC6000A, Endeavor, Zappa, Itautec, GA686BX | `keyboard_at.c:173-217, 242-243, 301-304, 471-476, 482-486, 583-585, 611-670, 673-679, 692-693, 783-788` | Le 8042 est transcrit en ENTIER — A20 et la ligne de reset comprises, qui sont l'essentiel du bloc B1. Ce qui reste est de l'aiguillage vers huit machines que ce dépôt n'a pas : quarante-cinq lignes de `t3100e_notify_set` pour la touche « Fn » d'un Toshiba, le port B dupliqué en 0x63 du Xi8088, le rétroéclairage du GRID1520, le `STAT_IFULL` immédiat qu'attendent le T3100e et le SPC-6000A. **Ce ne sont pas des branches mortes mais des branches NON PRISES**, et la différence se mesure : `romset` vaut `ROM_IBMAT` des deux côtés — l'oracle le reçoit par `h_set_romset` — donc chaque garde est fausse à l'identique. Les `else` de 0xC0 et 0xCA *sont* les chemins de l'AT, et ils sont transcrits. `mouse_scan` est une **déviation** et non une omission : il appartient à `mouse_ps2.c:10`, non transcrit, et vit dans `keyboard_at.cs` parce que `keyboard_at.h` le déclare `extern` et que le 8042 est son seul lecteur — l'oracle fait le même geste, `harness_stubs.c:686`. |
| `config_name`, les trente autres `case` des `switch (romset)` de `loadnvr`/`savenvr`, `time_internal_sync` | `nvr.c:238-523, 546-783` ; `rtc.c:224-243` ; `pc.c:217-219` | Le MC146818 est transcrit en entier, `nvrfopen` comprise (`nvr.cs:78-103`), et les **deux** branches de `loadnvr` — avec fichier et sans. Ce qui reste des deux `switch` — 546 des 802 lignes — est un nom de fichier par machine pour une trentaine de machines absentes ; seuls `ROM_IBMAT` et `ROM_AMI286` y sont, ce dernier avec son `nvrmask = 127`. `config_name` est déclaré mais **jamais affecté** (`config.cs:54`) là où PCem le pose depuis le nom du fichier de configuration : le CMOS s'appelle donc `.at.nvr` et `.ami286.nvr`, deux fichiers **cachés**, et `--config` ne le change pas. **L'oracle appelle bien `loadnvr`** (`harness.c:1112`), mais ses trois chemins sont des globales de `.bss` jamais affectées (`harness_stubs.c:683-685`) : il compose `./.ami286.nvr`, qui n'existe pas, et prend donc toujours la branche sans fichier là où le C# lit le vrai fichier — **une divergence d'état du CMOS avant la première instruction, qu'aucun `boot-diff` ne peut traverser**. `nvr_dosave` reste posé sans lecteur, fidèlement : son seul lecteur chez PCem est la boucle de trames de l'interface (`wx-sdl2.c:181`), omise ci-dessous. `time_internal_sync` lit l'horloge de l'HÔTE : aucun oracle ne peut la comparer, et `enable_sync` vaut 0. |
| Le champ `FILE *f` de `PcemHDC` | `ibm.h:366-372` | Mort dans tout l'arbre vendoré : `hdd_file_t` porte son propre descripteur, et aucune ligne n'écrit `hdc[d].f`. |
| **Divergence assumée** (pas une omission) : `rom_init` alloue un tableau CLR, donc à zéro, là où `malloc` rend du tas | `rom.c:60-62`, appelé `mfm_xebec.c:757` et `:793` | PCem alloue `size` puis ignore le retour de `fread` : une ROM de 4 096 octets dans une allocation de 16 384 laisse **12 288 octets de tas** lisibles par l'invité (`PB-24`). De l'UB, pas un comportement — trois exécutions, trois valeurs. Même arbitrage que `h_pad_ram` ci-dessus. Atténué par l'en-tête de ROM, qui déclare sa vraie longueur et borne le balayage du POST. Depuis G7.1 l'oracle enveloppe `rom_init` et met la queue à zéro (`harness_stubs.c`) : la VGA d'IBM (`vid_vga.c:107`, 8 Ko de tas en C6000-C7FFF, que le balayage du 5150 LIT) avait fait rougir un boot-diff. Les deux côtés sont à zéro. |

**À ne PAS omettre malgré les apparences :** `readlookup2`/`writelookup2`/`addreadlookup`
/`addwritelookup` (portent du temps, `cycles -= 9` à `mem.c:378`) · `mem_logical_addr`
(paramètre vivant) · la file de préfetch `808x.c:117-260` (c'est *l'émulation*, pas une
optimisation) · `nextcyc` (porté d'une itération de boucle à l'autre).

---

## Faits vérifiés qui contredisent l'intuition

Déplacés le 1er octobre 2026 dans `iXtal26/Docs/faits-verifies.md` — des constats, pas des
règles (R3).
