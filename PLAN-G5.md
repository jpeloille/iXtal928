# G5 — L'IDE : le plan, sur reconnaissance

> Écrit le 30 septembre 2026, après G4 (x87). Bloc G5 de `PLAN.md`.
> Chaque constat cite la ligne de C qui le fonde, sur `pcem-dev/` tel que vendoré.
> Les décisions à trancher sont en fin de fichier ; rien ne s'écrit avant leur validation.

## Où on en est

| | C# | oracle |
|---|---|---|
| Contrôleurs de disque dur | `mfm_xebec`, `dtc5150x`, `mfm_at` (`Host/HardDiskControllers.cs`) ; IDE « omitted » | `mfm_xebec.c` et `hdd_file.c` seuls (`tools/oracle/Makefile`) ; **ni `mfm_at.c` ni `ide.c`** |
| Aiguillage | `pc.cs:707-713`, trois `device_add` ; `resetide()` omis (`pc.cs:717`, pour `pc.c:395`) | `harness.c:1321-1327` : `mfm_xebec` / `dtc5150x` codés en dur |
| Disques | `Disc/hdd.cs` porte déjà `hdc[7]` et `ide_fn[7]` ; `hdd_file.cs` RAW seul | `harness_stubs.c:682-689` : `hdc`, `ide_fn` en souches |
| Machines | ami286, ami386, ami386dx : `MODEL_HAS_IDE` dans PCem (`model.c:990`, `:1240`, `:1344`) ; ami486 aussi (`:1418`) | les trois premières bootent sous oracle (neat.c lié) |

**Le constat qui cadre tout : aucun disque AT n'a jamais été vérifié contre l'oracle.** L'oracle
n'a pas `mfm_at.c` (VERIFICATION.md, « l'oracle n'a ni neat.c ni mfm_at.c » — neat.c y est
entré depuis) : les profils 286 et 386, qui tournent sur `mfm_at` type 46, n'ont que le cœur
C# pour témoin de leur disque. G5 est l'occasion de fermer ce trou en même temps que d'ouvrir
l'IDE.

`MODEL_HAS_IDE` ne câble rien : seule l'interface le lit (`wx-config.c:223`, `:924`), pour
proposer « ide » par défaut. Le vrai chemin est la clé `hdd_controller = ide` → 
`hdd_controller_init` (`pc.c:392`, `hdd.c:128-140`) → `device_add(&ide_device)` ; les disques
sont chargés par `resetide()` (`pc.c:290`, `:395`, `ide.c:265`).

## Ce qu'il faut transcrire

| Fichier | Lignes | Ce qui entre |
|---|---|---|
| `ide/ide.c` | 1 222 | le contrôleur ATA, deux canaux (0x1F0/0x3F6 IRQ 14, 0x170/0x376 IRQ 15), ≈ 1 000 lignes utiles |
| `ide.h` | 67 | `IDE`, constantes |
| `mfm_at.c` (porte d'oracle seulement) | — | déjà transcrit en C# ; à LIER à l'oracle |

**Hors périmètre**, comme décidé pour G4 et sauf avis contraire : `ide_atapi.c` (500, CD-ROM et
ZIP par SCSI-sur-ATAPI), `ide_sff8038i.c` (189, bus master PCI), `xtide.c` (121, cartes XT).
Les appels `atapi_*`, `WIN_PACKETCMD`, `WIN_PIDENTIFY` deviennent `// omitted:` ;
`WIN_READ_DMA` / `WIN_WRITE_DMA` (`:872-905`, `:951-977`) sont transcrits tels quels — sans
bus master, PCem laisse le disque BUSY, et on le reproduit.

**Piège** : `cdrom_channel` vaut 2 par défaut (`ide.c:87`, `pc.c:703`) — le maître du canal
secondaire devient un CD-ROM ATAPI (`:282-284`). Sans ATAPI, il faut `cdrom_channel = -1` des
deux côtés, marqué `// DEVIATION:` côté C# et posé par le harnais côté oracle.

## Les étapes

### G5.0 — L'oracle d'abord : `mfm_at.c` et `ide.c` liés

`mfm_at.c` et `ide.c` dans le Makefile ; souches retirées (`ide_fn`) ou ajoutées (`atapi`,
`scsi_bus_atapi_init`, `scsi_cd`, `scsi_zip`, `hdd_controller_current_is_ide` —
`-Wl,--no-undefined` les exigera) ; `h_set_hdd_controller` accepte `mfm_at` et `ide`. ABI + 1.
**Porte** : selftest, et le **premier boot-diff d'un disque AT** : ami286 + `mfm_at` type 46,
FDISK puis FORMAT C: /S sous oracle (méthode de M11/M12 : une copie d'image par côté,
comparées octet par octet après `h_closepc`). Toutes les séries de G4 identiques.

### G5.1 — `ide.c`, disque dur seul

Transcription de `ide.c` et `ide.h`, `resetide`, `ide_pri_enable` / `ide_sec_enable`,
`ide_init`, les timers ; `HardDiskControllers.cs` gagne « ide » ; `pc.cs` le branche ; le
SETUP (`SdlSetup.cs`), `DriveMounter.cs` et `MachineOverrides.cs` le proposent.
**Porte** : boot-diff ami286 + `ide` + image vierge de type 46 : FDISK, FORMAT C: /S,
réamorçage sur C: — images identiques octet par octet, instructions identiques ; idem ami386dx ;
le fuzzeur et toutes les séries existantes inchangés (l'IDE n'est posé nulle part par défaut).

### G5.2 — Deux disques, deux canaux, et ce qui reste

C: et D: sur le canal primaire, puis un disque sur le secondaire (IRQ 15 — voir PB ci-dessous) ;
READ/WRITE MULTIPLE, SET MULTIPLE, IDENTIFY, VERIFY, FORMAT, SPECIFY, les erreurs (secteur
hors disque, disque absent). **Porte** : un `ide-check` dirigé (commandes ATA écrites dans les
ports, comme `x87-cases` pour le coprocesseur), plus un boot-diff à deux disques.

### G5.3 — Les machines et les témoins

`hdd_controller = ide` pour l'ami486 (G6) par défaut ; profils Rider inchangés (MFM) sauf
décision contraire. Témoins `--boot` : DOS 5 installé sur un disque IDE vierge, `CHKDSK`,
Windows 3.1 depuis C:. Les images de l'utilisateur ne sont touchées qu'en copie.

## Les défauts de PCem déjà relevés (à lire à la ligne, puis inscrire)

1. `ide_irq_update` (`:139-142`) teste le bit 0x40 de pic2 (IRQ 14) pour LES DEUX canaux.
2. Le rappel de reset (`:796-812`) appelle `atapi->stop()` même pour un lecteur `IDE_NONE` :
   pointeur nul si `atapi` n'est pas posé — ce qui sera le cas sans ATAPI.
3. READ/WRITE MULTIPLE avec `blocksize == 0` (`:462`, `:488`) : `fatal()`, l'invité peut
   arrêter l'émulateur.
4. `WIN_FORMAT` (`:1018`) formate depuis `ide_get_sector()`, qui retranche 1 au secteur : un
   secteur à 0 vise l'octet −512 de la piste.
5. Sélection de lecteur pendant un reset (`:404-419`) : tête et LBA de la même écriture perdus.
6. VERIFY (`:1000-1012`) n'avance ni les registres ni `secount`.
7. IDENTIFY (`:176-181`) borne les cylindres à 16 383 sans recalculer têtes et secteurs, et lit
   `hdc[]` plutôt que `hdd_file`.
8. Hors `ide.c` : `wx-config.c:223` inverse le défaut (« none » pour une machine IDE).
Déjà inscrit : PB-29 (`ide_fn[4]` contre `[7]`).

## Les risques

1. **L'oracle n'a jamais lu un disque AT.** G5.0 est une porte en soi : si `mfm_at` diverge,
   c'est un défaut de transcription de M13 à corriger avant toute ligne d'IDE.
2. **Les timers.** `IDE_TIME = 10 * TIMER_USEC` ; l'ordre de `timer_add` fixe l'ordre des
   rappels — même piège qu'au contrôleur de disquettes.
3. **Le point 2 ci-dessus** : un défaut de PCem qui plante l'oracle. Le reproduire
   fidèlement, c'est planter C# aussi ; à trancher (décision n° 3).
4. **La taille des images.** 156 Mo par copie et par côté ; les boot-diffs d'écriture en
   feront deux par passe.

## Les décisions à trancher

1. **Périmètre** : ATA disque dur seul, ATAPI / SFF-8038i / XT-IDE hors G5 ? *(proposé : oui)*
2. **G5.0 avant l'IDE** : lier `mfm_at.c` à l'oracle et vérifier enfin le disque des profils
   286/386 ? *(proposé : oui — c'est le seul disque AT en usage)*
3. **`atapi->stop()` sur pointeur nul** : reproduire le plantage (R8), ou le contourner en
   DEVIATION quand `cdrom_channel = -1` ? *(proposé : lire d'abord si le chemin est atteint
   sans ATAPI ; s'il l'est, le reproduire côté oracle rend l'oracle inutilisable — DEVIATION
   des deux côtés, inscrite)*
4. **Profils Rider** : restent en `mfm_at`, ou passent en `ide` sur l'ami286 / ami386dx ?
   *(proposé : inchangés ; l'IDE devient le défaut de l'ami486 en G6)*
5. **Ce fichier** : `PLAN-G5.md`.
