# Transcription PCem → C#

Seul fichier de prose du dépôt. Plafond : 200 lignes. On l'amende quand une *règle*
change, jamais pour expliquer une ligne de code. Le projet précédent
(`~/RiderProjects/Retro/IXtal`) s'est arrêté à `NOP` après 1 490 lignes dont ~1 100 de
commentaires de conception : les règles ci-dessous sont les anticorps de ce mode d'échec,
comptables ou greppables, pas déclaratives.
---

## Règles

- **R1 — budget de commentaires, énuméré.** Un fichier transcrit contient exactement
  quatre sortes de commentaires :
  (a) l'en-tête : deux lignes SPDX + quatre lignes `// ORACLE:` ;
  (b) les commentaires de PCem, *vivants*, verbatim ;
  (c) une ligne de provenance par fonction, `// pcem: src/cpu/808x.c:1271-1301` ;
  (d) `// omitted:`, `// DEVIATION:`, `// pcem bug, reproduced:`, `// CS0165:`.
  Le commentaire de 140 lignes qui explique un choix de conception relève d'une
  catégorie (e) qui n'existe pas. Le code mort commenté de PCem n'est pas reproduit.
- **R2 — parité de lignes.** Le C# vivant d'une région se compare au C vivant
  correspondant (blancs et commentaires exclus des deux côtés). **Le plafond +25 % est
  toujours contraignant** : c'est lui l'anticorps anti-délayage, déclenché ⟹ on supprime,
  on ne négocie pas. Le plancher −25 % ne s'applique qu'aux fichiers `status:
  transcribed` : un `partial` est plus court par construction, chaque ligne absente étant
  couverte par une entrée du registre des omissions. Être plus court sans omission
  déclarée, en revanche, c'est du code oublié — et ça, R6(a) l'attrape.
- **R3 — un seul fichier de prose.** Celui-ci.
- **R4 — zéro abstraction.** Pas d'`interface`, `abstract class`, générique, LINQ,
  `record`, `async`, DI, méthode d'extension. Le C du palier (a) n'en contient aucun.
  Les six C#-ismes autorisés sont une liste close :
  `delegate` · `class` (structs dont l'adresse est prise) · propriété `ref` (`#define`) ·
  `[StructLayout(Explicit)]` (unions) · alias `global using` (stdint) ·
  `[MethodImpl(AggressiveInlining)]` (macros-fonctions).
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
  on la marque `// pcem bug, reproduced:`.

### Portée

Le code **transcrit** (`Cpu/`, `Memory/`, `Models/`, `Keyboard/`, `Video/`, `PluginApi/`,
`io.cs`, `timer.cs`, `pc.cs`, `ppi.cs`) garde les identifiants et commentaires anglais de
PCem ; `.editorconfig` y neutralise les règles de style qui réécriraient une ligne.

Le code **hôte**, neuf (`Host/`, `Program.cs`), suit les conventions C# normales et reste
commenté en français. `TreatWarningsAsErrors` est plein partout ; sur les fichiers
transcrits, les avertissements du compilateur se neutralisent par `#pragma warning
disable` **énumérés et commentés**, jamais en bloc.

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
| `uint8_t`… | alias `global using` (voir `GlobalUsings.cs`) |
| `goto opcodestart` | `goto opcodestart` |
| Nommage | `snake_case` de PCem **verbatim** |
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

### Collisions de mots-clés C#

Scan mécanique des en-têtes du palier (a). **Deux entrées, c'est tout :**

| C | C# | Où |
|---|---|---|
| `base` | `@base` | `x86seg.base` (`x86.h:38`), `mem_mapping_t.base` (`mem.h`) |
| `checked` | `@checked` | `x86seg.checked` (`x86.h:44`) |

Le préfixe `@` est choisi pour que `grep -n base` retrouve encore la ligne.

Une collision conteneur/type : le fichier `x86seg.c` et le typedef `x86seg`. La classe
conteneur devient `x86seg_c`, le type garde `x86seg`. Seul cas — `pit.c`/`PIT`,
`timer.c`/`pc_timer_t`, `mem.c`/`mem_mapping_t`, `vid_cga.c`/`cga_t`, `device.c`/`device_t`
sont tous distincts. `808x.c` → classe `_808x` (un identifiant C# ne peut pas commencer
par un chiffre ; le nom de fichier, si).

---

## Registre des omissions

Ce qui n'est **pas** transcrit, et pourquoi. Toute nouvelle entrée se justifie ici.

| Omis | Citation | Raison |
|---|---|---|
| `magic` / `TIMER_MAGIC` / `all_timers[256]` / `timer_valid()` et les branches de récupération de liste | `timer.h:19`, `timer.c:16-21, 49-65` | Détection d'use-after-free sur une struct libérée. Inatteignable sous GC. |
| `video_recalctimings` | déclaré `video.h:64` | Mort : jamais assigné, jamais appelé ; seule référence dans le bloc commenté `pit.c:54-55`. |
| `old_fp_control`, `new_fp_control`, `trunc_fp_control` | `x86.h:100-106` | Sauvegarde du mot de contrôle x87 de l'hôte, sous `#if defined __i386__`. Sans objet en .NET. |
| `MMX_REG MM[8]`, `ST[8]`, `TOP`, `tag[8]`, `npxs`, `npxc` | `x86.h:59, 93-99` | État 8087/MMX. **Champs absents, pas stubés** — un `ST[8]` stubé invite à une demi-implémentation. |
| `smi_pending`, `smbase`, `op32`, `cpu_recomp_ins` | `x86.h:85, 116` | SMM / 32 bits / dynarec. `cr0` est conservé : `mem.c:429, 448` testent `cr0 >> 31`, toujours 0 sur XT. |
| `_mem_exec[]`, `getpccache`, `page_lookup`, `pages[]`, `mmutranslatereal` | `mem.c:17-18, 28, 431-438` | Chemins 386/dynarec/pagination. `808x.c` n'y touche pas (0 occurrence). Attention : `mem_write_ram*` passe `&pages[addr>>12]` — supprimer `pages` **réécrit** le chemin d'écriture, marquer `// DEVIATION:`. |
| Chemin composite CGA (`Composite_Process`, `cga->composite`) | `vid_cga.c:~380` | Inutile au palier (a), et contient un vrai bug de PCem (écriture octet via lecture dword). L'éviter esquive la question de politique de bug. |
| `hline`, `create_bitmap`, `destroy_bitmap`, `screen` comme remontées cœur→hôte | déclarés `video.h`, définis `wx-sdl2-video.c:15,49,57,59` | Inversion de dépendance. `hline` (appelé `vid_cga.c:275`) devient un `Array.Fill` sur le buffer plat. |
| `src/wx-ui/`, `src/qt-ui/`, `src/codegen/`, `src/dosbox/`, `thread-pthread.c` | — | Remplacés par l'hôte SDL3 mono-thread. |

**À ne PAS omettre malgré les apparences :** `readlookup2`/`writelookup2`/`addreadlookup`
/`addwritelookup` (portent du temps, `cycles -= 9` à `mem.c:378`) · `mem_logical_addr`
(paramètre vivant) · la file de préfetch `808x.c:117-260` (c'est *l'émulation*, pas une
optimisation) · `nextcyc` (porté d'une itération de boucle à l'autre).

---

## Faits vérifiés qui contredisent l'intuition

Chacun a été vérifié dans l'arbre, pas déduit.

1. **`808x.c` n'utilise ni `_mem_exec` ni `getpccache`** (`grep -c` → 0). Il passe par
   `readlookup2`/`writelookup2`, granularité **4 Ko**, sentinelle `-1`. Et
   `addreadlookup` facture **`cycles -= 9`** (`mem.c:378`).
2. **`memcycs` n'est pas uniforme.** Lecture : `if (a != (cs + cpu_state.pc)) memcycs += 4;`
   (`:62`). `readmembf`, la variante de préfetch, ne facture **rien** (`:69-75`). Écriture :
   inconditionnel (`:98`). Mots : `+= (8 >> is8086)`, même garde.
3. **`execx86` n'est pas borné par `timer_target`** (contrairement à `exec386`, `386.c:163`).
   C'est `cycles += cycs; while (cycles > 0)` (`:1222`), avec `clockhardware()` appelé par
   instruction (`:3939`) et six fois dans `rep()`. TSC en virgule fixe 32:32 :
   `tsc_frac += (uint64_t)diff * xt_cpu_multi` (`:893-904`).
4. **`setpitclock` écrit onze globales**, pas neuf (`pit.c:37-59`), puis diffuse
   `video_updatetiming()` et `device_speed_changed()`. C'est le domaine d'horloge partagé,
   et c'est ce qui interdit un refactor « struct d'instance ».
5. **Les décalages par CL sont des boucles par comptage** (`:2984, 3138`), pas des
   décalages larges — donc pas de divergence C-UB / masquage C#. Ça reviendra au palier (b).
6. **La sémantique multi-thread de PCem sous Linux n'existe pas.** `thread_reset_event`
   est vide (`thread-pthread.c:46`), `thread_wait_event` n'a pas de boucle de prédicat.
   Le handshake de `video.c:1132-1144` dégénère en attente active sur un `int` non
   atomique. Il n'y a pas de fidélité à préserver : iXtal26 est mono-thread.

---

## Résultats des portes M0

Mesurés le 2026-09-19, .NET 10.0.112, x86-64 Linux.

| Porte | Résultat |
|---|---|
| **G1** propriété `ref` | **Verte.** `cycles -= 3;`, `cycles = cycles - 7;`, passage `ref`, et `tsc += 0x1_0000_0000UL` mutent bien le champ référencé. |
| **G2** switch géant + `goto` | **Verte.** **180 106 octets d'IL** pour 256 cas / ~13 900 lignes ; JIT propre en Debug, Release, et sous `DOTNET_TieredCompilation=0` (pas de bailout). Le switch de `808x.c` fait ~2 600 lignes, soit ~1/5 : **marge 5×**, la contingence « découper par quartet » est inutile. |
| **G3** union explicite | **Verte.** Aliasing `l`/`w`/`b.h`/`b.l`, écriture de demi-octet préservant le reste, `getr8`/`setr8` indexés avec le pliage haut/bas sur le bit 2, et absence de mutation à travers une copie de struct. |
| **G4** SDL3-CS sous net10 | **Verte.** `SDL3-CS 3.4.14.1` restaure et compile sous `net10.0` ; `dotnet run -- --frames 5` ouvre une fenêtre, rend 5 images, sort 0. |

L'échafaudage (`M0Gates.cs`, `M0Gates.Generated.cs`, le drapeau `--gates`) est supprimé
une fois ces résultats consignés. Il est regénérable depuis l'historique git si une
régression de runtime remet G2 en question.

---

## Où PCem s'écarte du silicium

Chiffres dans **`sst-baseline.tsv`**, généré par
`iXtal26.Diff sst-probe --baseline` : c'est de la donnée, pas de la prose, et elle grandira
à chaque forme sondée. **Le critère n'est pas « 100 % de SST » mais « le C# reproduit la
colonne `passe` à l'identique ».**

Sondé sur un échantillon *volontairement adverse* (2 000 cas × 19 formes, les opcodes les
plus retors) : **100 % sur tout ce qui est ordinaire** — ADD, MOV, NOP, MOVSB, CALL, INC,
MUL, SHL 1, AAA/AAS. Les masques de `metadata.json` sont indispensables : `37` passe de
117/2000 à 2000/2000 une fois appliqué.

Cinq écarts réels, à transcrire tels quels et à marquer `// pcem bug, reproduced:` —
`F6.6`/`F7.6` DIV (**débordement de quotient non détecté** : vérifié à la main sur
`F6.6[2]`, AX=36562 ÷ 12 = 3046 > 255, le 8088 lève INT 0, PCem tronque à 0xE6 et
poursuit) · `D0.6` SETMO (opcode non documenté, non implémenté) · `AD` REP LODSW ·
`D2.4` SHL par CL (un seul bit : **OF**) · `27`/`2F` DAA/DAS (AL, ~1–2 % des cas).

Performance relevée : 38 000 cas en 61 s, dominés par `h_reset()` qui remet à `-1` les 16 Mo
de `readlookup2`/`writelookup2` à chaque cas — ~85 min pour le corpus complet contre 2 min
visées. Le harnais devra réinitialiser ces tables paresseusement.
