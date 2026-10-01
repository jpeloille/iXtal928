# G6 — Le 486 et l'ultime machine : le plan, sur reconnaissance

> Écrit le 1er octobre 2026, après G5 (IDE). Bloc G6 de `PLAN.md`.
> Chaque constat cite la ligne de C qui le fonde, sur `pcem-dev/` tel que vendoré.
> Les décisions à trancher sont en fin de fichier ; rien ne s'écrit avant leur validation.

## Où on en est

**PCem n'a pas de table d'opcodes 486.** `cpu_set` pose `ops_386` / `ops_386_0f` pour tout
processeur (`cpu.c:231`) ; les cas i486 (`cpu.c:486-517`) ne font que régler les `timing_*`.
Tout ce qui est propre au 486 est un `if (is486)` DANS les handlers partagés — et le cœur 386
de G2 les a transcrits : 294 occurrences d'`is486` dans `iXtal26/Cpu`. CMPXCHG, XADD
(`386_ops_atomic.cs`), BSWAP, INVD, WBINVD, CPUID (`386_ops_misc.cs:1003-1050`), CR0.CD et
`cpu_update_waitstates`, CR4, `timing_misaligned` (`mem.cs:756-956`), le CR0 de reset
(`1 << 30`, `808x.cs:1033`) sont là. **G6 est surtout de la configuration, pas des handlers.**

| | C# | oracle |
|---|---|---|
| Handlers `is486` | transcrits (G2) | compilés (`386.c` verbatim) |
| `cpus_i486`, `cpus_Am486`, `cpus_Cx486` | absents (`cpu_tables.cs:152`) | présents |
| `cpu_set`, cas i486 / Am486 / Cx486 / iDX4 | omis (`cpu.cs:452`) | présents |
| Constantes `CPU_i486DX`, `CPU_Am486*`, `CPU_iDX4` | absentes (`cpu.cs:39-50`) | présentes |
| INVLPG (`0F 01 /7`), `mmu_invalidate` | omis (`386_ops_0f.cs:166-169`, `mem.cs:304`) | présents |
| `CPUID = cpuid_model`, `cpu_multi`, `has_vlb`, `cpu_CR4_mask` | omis (`cpu.cs:254`, `:267`, `:347`) | présents |
| `ali1429.c` (66 lignes) | absent | **non lié** (Makefile) |
| Machine `ami486` | absente | absente du harnais (`h_model_*`) |
| ROM `roms/ami486/ami486.bin` | présente (64 Ko) | — |
| x87 intégré (`FPU_BUILTIN`, `x87_timings_486`) | transcrit (G4) | présent |
| IDE (`MODEL_HAS_IDE`) | transcrit (G5) | lié (G5.0) |

**Machine finale** : `ami486` (`model.c:1412-1421`) — ALi 1429, `MODEL_AT | MODEL_HAS_IDE`,
1 à 256 Mo, `at_ali1429_init` = `at_init` + `ali1429_init` (`model.c:502-505`) — avec
**i486DX2/66** (`cpu_tables.c:179-180`) : `CPU_i486DX`, `fpus_builtin`, 66 666 666 Hz,
multiplicateur 2, bus 33 333 333, `edx_reset` 0x430, `cpuid_model` 0 (pas de CPUID : #UD),
mémoire 12/12, cache 6/6.

## Les étapes

### G6.0 — L'outillage : un cœur « 486 » pour le fuzzeur  ✅ *fait avec G6.1, VERIFICATION.md § G6.0–G6.1*

Le harnais ne connaît que `H_CORE_286` et `H_CORE_386` (`harness.c:59-61`), et `h_reset`
fige `ROM_AMI386SX` pour le 386 (`:505`). Il faut `H_CORE_486` (machine `ami486`, `cpu_set`
sur l'entrée choisie), `--core 486` et `--cpu` sur `cpus_i486` dans `iXtal26.Diff`, le
`Reset486` côté C#, et le vecteur de configuration CPU élargi (`CPUID`, `cpu_features`,
`cpu_CR4_mask`, `cpu_multi`, `has_vlb`, `cr0` de reset, `cpu_cache_int_enabled`). ABI + 1.
**Porte** : toutes les séries existantes identiques ; le cœur 486 refuse bruyamment tant que
G6.1 n'a pas transcrit ses tables (pas de vert muet).

### G6.1 — Les tables et `cpu_set`  ✅ *fait, VERIFICATION.md § G6.0–G6.1 (INVLPG compris)*

`cpus_i486` seule (décision n° 1) ; les constantes ; les cas i486 / iDX4 de `cpu_set`
(`cpu.c:483-517`), `CPUID`, `cpu_multi`, `has_vlb`, `cpu_CR4_mask` ; `cpu_CPUID` pour l'iDX4,
seul à avoir un `cpuid_model`. **Porte** : `cpu-config-check` élargi à chaque entrée 486 et à son `fpus_builtin`,
empreintes identiques ; fuzzeur `--core 486` single et flux sur les 256 opcodes et les `0F`
propres au 486 (08, 09, 01/7, A2, B0/B1, C0/C1, C8-CF), graines 1 et 7 ; x87 `--x87 all` avec le
x87 intégré ; pm-fuzz et pm-check en 486.

### G6.2 — INVLPG et le reste du mode protégé 486

`0F 01 /7` (`x86_ops_pmode.h:447-458`), `mmu_invalidate` (`mem.c:349-352`), CR0.WP, la
lecture de CR0 et SMSW (ET). **Porte** : page-check et pm-check en 486, contrôles négatifs.

### G6.3 — L'ami486 : `ali1429.c` et la machine

`ali1429.c` (ports 0x22/0x23, ombrage C0000-FFFFF par blocs de 32 Ko, registres 0x13/0x14),
`ali1429_reset()` aux trois points de `pc.c` (`:191`, `:317`, `:403` — omis aujourd'hui,
`pc.cs:852`), `m_ami486`, les crochets `ROM_AMI486` de `nvr.c:368`, `:655` et
`mem_bios.c:569-576`, le CMOS de référence et `--make-nvr`. L'oracle lie `ali1429.c`, gagne
`h_model_ami486` et l'appel d'`ali1429_init` dans `h_boot`. **Porte** : boot-diff ami486 +
i486DX2/66 jusqu'au bout du POST, puis `--timer-check --model ami486` (la cadence, comme en
M16), `cpu-config-check` sur l'ami486.

### G6.4 — La machine complète et les témoins

`hdd_controller = ide` par défaut sur l'ami486 (décision n° 4 de G5) ; un profil Rider
« ami486, DX2/66 » (décision n° 3) ; boot-diff avec disque IDE (écriture, FDISK + FORMAT,
ide-check) ; témoins `--boot` : DOS 5, MSD (« 80486 »), Windows 3.1 en mode 386 étendu,
X87BANC.COM (x87 intégré contre 387). **Porte** : séries complètes, témoins consignés.

## Les défauts de PCem déjà relevés (à lire à la ligne, puis inscrire)

1. `cpu_features` n'est jamais remis à zéro par `cpu_set` : après une entrée iDX4 ou Pentium,
   un i486 garde CR4 / VME — et le harnais rejoue `cpu_set` dans le même processus.
2. `cpu_CR4_mask = CR4_VME | CR4_PVI | CR4_VME` (`cpu.c:485`) : VME en double, sans effet.
3. CR0 de reset `1 << 30` (`808x.c:671`, `:715`) au lieu de 0x60000010 ; ET n'est rendu qu'à
   MOV r32,CR0, pas à SMSW ; les écritures de CR0 ne sont pas masquées.
4. INVLPG : base `ds` au lieu du segment effectif (sans effet, tout est vidé), pas de #UD en
   mod = 3, pas de retour après SEG_CHECK_READ.
5. CR0.WP honoré même sur un 386 ; changer WP seul ne vide pas le cache de la MMU.
6. CMPXCHG n'écrit pas la destination quand la comparaison échoue (le 486 écrit toujours).
7. Exécution depuis la ROM : `cpu_prefetch_cycles = cpu_rom_prefetch_cycles` même cache
   interne actif (`mem.c:431-435`) — une particularité de temps à reproduire.
8. INVD / WBINVD sur un non-486 : `x86illegal()` sans restaurer `pc` (`x86_ops_misc.h:808-825`).

## La vérification

- **Oracle** : PCem seul. Aucun corpus SingleStepTests 486 n'existe (seuls 8088 et 80386
  mode réel, `tools/fetch-sst*.sh`).
- **Fuzzeur** : `--core 486`, cycles compris (`timing_*` du 486, `timing_misaligned`,
  `cpu_prefetch_width` 16).
- **Boot-diffs** : ami486 + DX2/66, POST complet, DOS, écriture IDE.
- **Cadence** : `--timer-check`, le multiplicateur 2 et le bus à 33 MHz (PIT).

## Les risques

1. **La cadence.** Premier processeur à multiplicateur : `cpu_busspeed` ≠ `cpuclock`, le PIT
   suit le bus (`pit.c:46`). Mal réglé, le POST compte faux sans diverger au diff.
2. **Le cache.** `cpu_cache_int_enabled`, `cpu_update_waitstates` (×11/16), le préfetch ROM
   (point 7) : des temps, pas des fonctions — seul le fuzzeur à cycles les voit.
3. **Le point 1** : un état qui fuit d'un `cpu_set` à l'autre fausserait `cpu-config-check`
   selon l'ordre des entrées. À reproduire (R8), et à ordonner.
4. **Le mode 386 étendu de Windows** sur 486 : V86 + pagination + INVLPG ; le témoin décisif.

## Les décisions à trancher

1. **Les processeurs** : **Intel seul, décision de l'utilisateur du 01/10** — `cpus_i486` (SX,
   DX, DX2, DX4/iDX4) et ses cas de `cpu_set` ; ni `cpus_Am486` ni `cpus_Cx486`, marqués
   `// omitted:` à leur place.
2. **Le cœur du fuzzeur** : `H_CORE_486` distinct, sur l'ami486 *(proposé : oui, G6.0)*.
3. **Le profil Rider** : un nouveau profil « ami486, i486DX2/66, IDE, Trident 9000B », les
   deux profils actuels inchangés ? *(proposé : oui ; la carte VLB viendra en G7)*
4. **`cpu_features` qui fuit (point 1)** : le reproduire tel quel (R8) et fixer l'ordre des
   entrées dans les outils ? *(proposé : oui)*
5. **Ce fichier** : `PLAN-G6.md`.
