# G3 — Les machines 386 : le plan, sur reconnaissance

> Écrit le 28 septembre 2026, au commit `6af8022` (G2 fusionné). Bloc G3 de `PLAN.md`.
> Chaque constat cite la ligne de C qui le fonde ; les mesures ont été prises sur cet arbre.

## Où on en est

Le **processeur** 386 est fini et vérifié (G2, D0 à D7). Aucune **machine** 386 ne démarre :

| | C# | oracle |
|---|---|---|
| `m_ami386` (386SX, `model.c:1238-1247`) | dans `models[]`, `init = null`, refus bruyant (`model.cs`) | dans `models[]` (`harness_stubs.c:871`), `h_boot` refuse (`harness.c:1116-1119`) |
| `m_ami386dx` (386DX, `model.c:1340-1349`) | absent ; `cpus_i386DX` non porté (`cpu_tables.cs:116`) | absent |
| chipset Headland (`headland.c`, 503 lignes) | absent | **non lié** (`tools/oracle/Makefile:42-75`) |
| chipset OPTi 495 (`opti495.c`, 302 lignes) | absent | non lié |

**Mesuré sur cet arbre, et c'est le point dur : AUCUN boot-diff de classe AT n'a jamais été
vert.** Les dix-huit de la série de portes sont tous 5150/XT.

| boot-diff | résultat |
|---|---|
| `--model ibmat` | diverge à l'instruction **50**, `IN AL,71h` en `F000:0169` : oracle `0xFF`, C# `0x00` — le **CMOS** (`VERIFICATION.md` § M15/M16 le notait déjà, à 0x10 avant) |
| `--model ami286` | diverge à l'instruction **1 153 990** ; l'état du PIT diffère dès l'instruction **3 860** (`F000:813B`). Le C# a le chipset NEAT (`neat.cs`), l'oracle ne lie pas `neat.c` : les deux côtés ne simulent pas la même machine |
| `--model ami386` | refus de l'oracle : « le chipset Headland n'est pas lié » |

Or la porte de G3 est « boot-diff à l'unité ». Elle suppose donc d'abord de rendre un AT
diffable. D'où une étape zéro.

## Ce que l'ami386 appelle

`model.c:482-485` : `at_headland_init()` = `at_init()` puis `headland_init()`.

- **`at_init`** (`model.c:335-346`) : transcrit côté C# depuis l'AT 286 (`model.cs:166`) ;
  inliné côté oracle (`harness.c:1170-1182`). Rien à ajouter.
- **`headland_init`** (`headland.c:430-503`), pour `ROM_AMI386SX` :
  - ports **0x22/0x23** (index/données, `:445-447`) — PAS le port 92, réservé à l'AMA-932J
    (`:448-451`) ;
  - ports **0x1EC-0x1EF** (EMS, `:457-458`) ;
  - **désactive** `ram_low_mapping`, `ram_mid_mapping`, `ram_high_mapping` (`:463-465`) et
    les remplace par ses propres mappages, dont TOUS les accès passent par
    `get_headland_addr` (`:29-80`) — remappage de la mémoire au-delà de 640 Ko
    (`addr -= 0x60000`, `:77`), EMS sur 32 pages de 16 Ko ;
  - ses gestionnaires `mem_read_headland*` (`:385-428`) ne remplissent **pas** le cache
    `readlookup2` (pas d'`addreadlookup`) : chaque accès RAM passe par le chemin lent ;
  - registre `0x82` : l'ombrage de la ROM F0000 par `mem_set_mem_state` (`:147-151`) ;
    registre `0x87` : `softresetx86()` (`:152-155`) ; CR0/CR4 : ombrage E0000/F0000 et
    `bios_mapping[0..3]` (`:201-233`).
- **CMOS** : `loadnvr` ouvre `ami386.nvr`, masque 127 (`nvr.c:364-367`). **Aucun
  `nvr/default/ami386.nvr` n'existe** (seuls `at.nvr` et `ami286.nvr`) : sans lui, somme de
  contrôle fausse, et le POST s'arrête sur l'erreur CMOS.
- **ROM** : `ami386/ami386.bin`, 64 Ko (`mem_bios.c:350-357`) ; présente dans `roms/`.

Primitives C# requises, vérifiées présentes : `mem_set_mem_state`, `mem_mapping_set_exec`
(`mem.cs:1170`), `mem_mapping_set_addr`, `bios_mapping[8]` (`mem.cs:120`), `ram_*_mapping`,
`mem_a20_alt` / `mem_a20_recalc`, `softresetx86`. **Manquante** : `cpu_set_edx`, appelée par le
port 92 de Headland (`:163`) — branche morte pour l'ami386 (port 92 non posé), transcrite
quand même avec un arrêt nommé.

## Les étapes

### G3.0 — Un AT diffable  ← avant toute ligne de chipset

1. **Le CMOS des deux côtés.** Tracer pourquoi l'oracle lit 0xFF et le C# 0x00 à
   `F000:0169` : `nvrfopen` (`nvr.c:33-54`) cherche `nvr/<config>.<machine>.nvr` puis
   `nvr/default/`. Hypothèse à vérifier : les deux côtés ne résolvent pas le même chemin
   (cwd, `nvr_path`). Critère : `boot-diff --model ibmat` dépasse l'instruction 50.
2. **Le chipset de l'ami286 dans l'oracle** : lier `neat.c` et appeler `neat_init()` comme
   le C# (`model.c:452-455`). Critère : `boot-diff --model ami286` sans écart de PIT à 3 860.
3. Porte : `boot-diff --model ibmat` et `--model ami286` à l'unité (ou le premier écart
   restant, nommé et instruit) ; les dix-huit boot-diffs 5150/XT inchangés à l'unité.

**Oracle** : le boot-diff lui-même. C'est de l'outillage et du harnais — `h_boot` peut
devoir changer (ABI).

✅ *G3.0 fait, et les deux causes étaient celles-là :*
- *le CMOS : `nvr_path` et `nvr_default_path` étaient deux chaînes VIDES côté oracle,
  définies par `harness_stubs.c:736-737` et jamais remplies. `nvrfopen` ne trouvait aucun
  fichier, `loadnvr` prenait la branche « pas de fichier », 0xFF partout ; le C# lisait
  `nvr/default/at.nvr`. Corrigé par `h_set_nvr_paths` (ABI 26), poussé une fois dans le
  constructeur statique d'`Oracle` avec la résolution d'`initpc` (`pc.cs:538-543`).
  **`boot-diff --model ibmat` : vert, 4 723 826 instructions** — le premier AT diffable ;*
- *le chipset : `neat.c` lié à l'oracle et `neat_init()` appelé après l'`at_init` inliné
  pour `ROM_AMI286`. **`boot-diff --model ami286` : vert, 5 207 508 instructions.***

### G3.1 — L'ami386 : Headland

1. **L'oracle d'abord** : lier `headland.c`, lever le refus de `h_boot`, appeler
   `headland_init()` après l'`at_init` inliné quand `romset == ROM_AMI386SX`.
2. **Le CMOS de l'ami386** : produire `nvr/default/ami386.nvr` par le même geste que celui de
   l'ami286 (`--make-nvr`, somme de contrôle AMI mesurée dans `nvr/README.md`), après avoir
   vérifié dans `ami386.bin` que le BIOS AMI 386 range et somme son CMOS de la même façon.
3. **Le C#** : `headland.cs` (503 lignes, verbatim), `at_headland_init`, `m_ami386.init`.
4. Porte : `boot-diff --model ami386` à l'unité ; puis `--boot --model ami386` jusqu'à
   l'invite DOS ; puis Windows 3.1 en mode 386 étendu — le test d'intégration de G2 (V86,
   pagination, commutation de tâches).

*Fait (G3.1, étapes 1 et 3) : `headland.c` lié à l'oracle, `headland.cs` relu contre le C
(aucun écart de comportement ; citations de lignes corrigées, entrée `oracle.tsv` ajoutée),
`at_headland_init`, la ROM `ami386/ami386.bin`. **`boot-diff --model ami386` : vert,
42 177 361 instructions à 640 Ko et 42 160 293 à 4 Mo (30 s)**. Le BIOS laisse le remap
éteint (`headland_regs_cr[0] = 4` à l'init, jamais réécrit) ; la mémoire étendue passe
par les gestionnaires Headland, ~1,6 M d'accès au-delà du Mo en 30 s. Contrôles négatifs :
lecture faussée au-delà de 3 Mo → divergence à 358 514 ; remap allumé à l'init → 358 588.
Reste l'étape 2 (le CMOS `ami386.nvr`) et la porte `--boot` : le POST s'arrête sur
« CMOS system options not set ».*

*Fait ensuite : le CMOS (`c47658a`), la référence de PCem et `loadnvr` qui l'ignorait côté
C#. `--boot` (ATTENTION : `--config` APRÈS `--boot`, sinon il est ignoré et c'est le 5150
qui démarre — le « 131 » de sa cassette le trahit) : DOS 2.00 jusqu'à `A>` ; puis, sur une
copie du disque 286 de Julien et un CMOS `--make-nvr` (4 Mo, Trident 8900D, type 46),
HIMEM + SMARTDRV jusqu'à `C:\>`.*

*Windows 3.1 en mode 386 étendu, C# seul : le Windows du disque avait été installé sur le 286
(ni `WIN386.EXE` ni pilotes `*.386`) et démarrait en mode standard. SETUP rejoué sans fenêtre
sur l'ami386 (`IBM_Windows_3-11`, mise à jour Express de `C:\WINDOWS`) :*

```
--boot roms 40000 --config <ami386 4 Mo tvga8900d mfm_at type 46> --settle 3000
  --type "@A:disk01.img" --type "q.setup" --type "" ×4 --type "@wait 20000"
  --type "@A:disk02.img" --type "" --type "@wait 30000"
  --type "@A:disk03.img" --type "" --type "@wait 30000"
  --type "Julien" --type "@wait 5000" --type "" --type "@wait 20000"
  puis pour k = 3..7 : --type "@A:disk0k.img" --type "" --type "@wait 40000"
  --type "@wait 30000" --type "@A:" --type "zin" --type "@wait 30000"
```

*(le dernier Entrée de la boucle choisit « Reboot » ; AZERTY : `q.setup` = `a:setup`, `zin`
= `win`). Le Gestionnaire de programmes s'ouvre avec **CR0 = 8000FF11** (PG et PE) sur
60 000 tranches, dont ~9 % s'achèvent en V86 : pagination et V86, donc le mode 386 étendu.
Mesuré par une instrumentation jetable de `BootTest.cs`, retirée. Au passage, le SETUP a
fait tomber l'hôte sur le défaut D1 de l'audit (`wlog_n` enroulé) : corrigé à part.*

### G3.2 — L'ami386dx : OPTi 495

1. `cpus_i386DX` (`cpu_tables.c`, table Intel seule, comme l'ami386), `m_ami386dx` des deux
   côtés, ROM `ami386dx`.
2. `opti495.c` (302 lignes) : oracle puis C#.
3. Porte : la même que G3.1.

*Fait (G3.2) : `opti495.c` (302 lignes, dont 43 de code) lié à l'oracle et transcrit,
`cpus_i386DX` et le `case CPU_386DX` de `cpu_set` (seuls `rml`, `mrl`, `mml` diffèrent du
386SX : bus 32 bits), `m_ami386dx`, sa ROM `ami386dx/opt495sx.ami`, son CMOS de référence
(`pcem-dev/nvr/ami386dx_opti495.nvr`, somme 0x0461, même règle), `loadnvr`/`savenvr`.
Le piège d'unités de PCem est devenu vivant : RAM en Mo pour un AT à granularité < 128
(`pc.c:695-700`) — `pc.ram_bounds_kb`, utilisé par la configuration, `--ram` et le SETUP.*

*Un défaut du HARNAIS, trouvé ici : `h_pad_ram` réallouait `ram` mais ne rebasait que
`ram_low_mapping` ; `ram_mid_mapping` et `ram_high_mapping` pointaient dans le bloc libéré
dès 768 Ko. Invisible sur l'ami386 (Headland les remplace), SIGSEGV sur l'ami386dx à 4 Mo
dès que l'OPTi ombre F0000 (premier fetch en F000:3350). Rebasés comme dans `mem_alloc`.*

*`boot-diff` ami386dx à 4 Mo : vert, 43 268 682 instructions (30 s) ; avec DOS, 28 886 794
(20 s). Contrôles négatifs : ombrage F0000 inversé → divergence à l'instruction 1 860 ;
`timing_rml` du 386DX à 8 → empreinte CPU divergente. `--boot` : l'écran AMI annonce
« 80386DX », « 64KB CACHE MEMORY », DOS jusqu'à `A>`. Windows 3.1 installé de même (la
séquence de G3.1, mais le DX va plus vite : éjecter A: AVANT l'Entrée qui choisit
« Reboot », après la disquette 5) : CR0.PG sur 60 000 tranches, ~10 % en V86 — mode 386
étendu.*

## Ce que je ne sais pas encore

- ~~**La cause exacte de l'écart CMOS de l'AT**~~ : **vérifiée** en G3.0 (`19eddda`), c'était bien le chemin — l'oracle composait `nvrfopen` sur deux chaînes vides.
- **Si le BIOS AMI 386 range son CMOS comme l'AMI 286** (somme 0x10-0x2D en 16 bits) : à lire
  dans `ami386.bin` avant de fabriquer `ami386.nvr`.
- **La taille mémoire à retenir** pour l'ami386 : `get_headland_addr` et `headland_mem_conf_cr0`
  dépendent de `mem_size` (`:21-27`, `:77`) ; la configuration par défaut du C# donne 640 Ko.
  Il faudra au moins une configuration > 1 Mo pour que les mappages mid et high existent.
- **Ce que la vitesse hôte y perd** : les mappages Headland court-circuitent `readlookup2`. À
  mesurer sur l'ami386, pas sur le 8088 (qui ne passe pas par eux).
