# G11 — Le SCSI : l'Adaptec AHA-1542C et ses disques ; le plan, sur reconnaissance

> **G11 est fait** (5 octobre 2026) : G11.0 à G11.3, VERIFICATION.md §§ G11.0 à G11.3 ; PB-128 à PB-144.

> Écrit le 5 octobre 2026. Bloc G11 de `PLAN.md` (décision utilisateur du 03/10). Feu vert le
> 05/10, « en totale autonomie » pour G11 et G12 : les décisions de la fin de fichier sont prises
> sous ce mandat, chacune datée. Chaque constat cite la ligne de C qui le fonde, sur `pcem-dev/` tel
> que vendoré. Reconnaissance : trois lectures (le disque, la carte, l'intégration), chacune suivie
> d'une contre-lecture qui l'a corrigée. On retient ici les contre-lectures.

## Où on en est

- **Le bus** : `Scsi/scsi.cs` porte le bus de G10.4, celui du pont ATAPI (`scsi_bus_update`,
  `scsi_bus_read`, `scsi_bus_kick`, `scsi_bus_reset`). Le bus des cartes SCSI (`scsi_bus_init`,
  `scsi_bus_close`, `scsi.c:304-336`) est omis (`scsi.cs:504`). Le chemin non ATAPI (`cmd_len`
  par groupe, `scsi.c:21`, `:96`) est transcrit, mais aucune porte ne l'a jamais exercé.
- **Le disque** : rien. `scsi_hd.c` (788 lignes) n'est qu'une souche dans l'oracle
  (`harness_stubs.c:819`).
- **La carte** : rien. `scsi_aha1540.c` (2 299 lignes) porte deux cartes, la 1542C et la
  BusLogic BT-545S. La ROM `adaptec_aha1542c_bios_534201-00.bin` (32 Ko, deux banques de 16 Ko)
  est dans `roms/`. L'EEPROM de référence de PCem (`pcem-dev/nvr/aha1542c.nvr` : ID 7, DMA 7,
  IRQ 10) n'a pas de copie dans `nvr/default/`.
- **Les lecteurs** : `hdc[7]` et `ide_fn[7]` existent (`Disc/hdd.cs:36`, `:61`), mais `loadconfig`
  ne lit que quatre lecteurs (`pc.cs`, omission de `hdg_*` à `hdi_*`, `pc.c:749-774`). BootDiff a
  `NHd = 4`.
- **La règle ISA 16 bits** n'existe nulle part. Le seul filtre est celui de l'écran
  (`Host/HardDiskControllers.cs`, `RequiresAtMachine`, qui vient de `wx-config.c:237-242`) ;
  `loadconfig`, `--hdd-controller` et l'oracle montent n'importe quelle carte sur n'importe quelle
  machine.

## Ce qu'il y a à transcrire

| | lignes | atteint |
|---|---:|---|
| `scsi/scsi_hd.c` | 788 | tout |
| `scsi/scsi_aha1540.c` | 2 299 | la 1542C seule : sans la BT-545S ni `MB_FORMAT_8` (décision n° 1) |
| `scsi/scsi.c:304-336` | 33 | `scsi_bus_init`, `scsi_bus_close` (PB-121 reproduit) |
| `mem.c:773-791` | 19 | `mem_readw_phys`, `mem_readl_phys` (les écritures w et l ne servent qu'à `MB_FORMAT_8`) |
| `pc.c:749-774` | 26 | `hdg_*`, `hdh_*`, `hdi_*` |
| `hdd.c:159` | 1 | l'entrée `aha1542c` du registre |

`MB_FORMAT_8` n'est posé que par la commande 0x81 (`scsi_aha1540.c:1255-1266`), refusée sur la
1542C (`:650-655`). Toutes ses branches sont donc mortes sur cette carte, y compris les trois
`fatal("MBO_IS_BIOS MB_FORMAT_8")` (`:1507`, `:1575`, `:1663`).

## Les défauts relevés (à lire à la ligne, puis inscrire)

**R9 — l'invité ou la configuration arrête l'hôte** (non reproduits ; `// pcem bug, not reproduced`).
Rappel : le `fatal()` de l'oracle rend la main (`harness_stubs.c:776-784`) et la suite y est
inexploitable (après `:679`, `:1337` à chaque échéance, et la carte est morte). Les tests R9 sont
donc en C# seul, et chaque porte exige `h_n_fatal == 0`.

1. **La carte, atteignable par l'invité.**
   - `:530` : CCB d'opcode inconnu.
   - `:679` : les sept commandes {0Ch, 1Ch, 1Dh, 1Eh, 20h, 21h, 2Ah}.
   - `:799`, `:841`, `:883` : la commande BIOS 03h, sous-fonctions 02h/03h/04h, quand le statut
     cible est non nul (CHECK CONDITION, ou statut périmé après une sélection ratée).
   - `:980` : sous-fonction 15h quand `int_buffer[0] & 1Fh` est non nul (cible non disque, ou ID
     vide après le sense automatique d'un CCB de mailbox, qui laisse 70h).
   - `:1743` : CDB plus longue que la longueur de son groupe (`scsi.c:21`).
2. **Le disque, atteignable par l'invité.**
   - `scsi_hd.c:87`, `:717` : `data_in` déborde. En C, sans plantage jusqu'à 2 × 256 Ko : le
     débordement tombe dans `data_out`. Au-delà, les champs de contrôle sont écrasés et le tas
     est corrompu.
   - `:723-728`, `:99-102` : `data_out` au-delà de 256 Ko, puis `fatal`.
3. **La configuration.**
   - La ROM absente : `rom_init` rend -1 et laisse `rom` à NULL, le POST lit la fenêtre, SIGSEGV
     (`:2083`).
   - La carte sur un 8088 ou un 8086 : sa ROM s'exécute au balayage du POST (instructions 286,
     `WBINVD` en 0F62h), et `picint` ignore l'IRQ ≥ 8 hors AT.

**Inatteignables avec `scsi_hd`** (transcrits avec leur `fatal`, comme PB-127) :
- la carte : `:1016`, `:1337`, `:1367`, `:1691`, `:1741`, `:1773`, `:1807`, `:1828`, `:1842`,
  `:1920`, `:1994`, `:2025`, `:2047`, `:2055`. BSY ne tombe qu'après l'ACK du message
  (`scsi.c:190-199`), et `scsi_hd` ne rend que quatre phases, toutes traitées ;
- le disque : `scsi_hd.c:476`, `:480`, `:529`, `:533`, `:580`, `:585`, `:633`, `:638`.

**Défauts observables, reproduits** (à numéroter à l'inscription, à partir de PB-128) :
- **La commande 22h (PROGRAM EEPROM) n'est pas bornée.** `params[c+3]` lit jusqu'à l'indice 257,
  au-delà de `params[64]`, dans `result_pos`, `result_len`, puis `result[]` : l'oracle survit, la
  disposition est donc modélisée. Seuls 32 octets sont persistés. L'ancienne IRQ reste levée
  quand l'IRQ change avec une interruption en attente.
- **Le sense automatique.**
  - Il a lieu après CHAQUE commande, sauf si l'octet 3 du CCB vaut 1, et son octet de contrôle
    vaut 14.
  - Sa destination est inversée (`:1613-1616`). Un CCB de mailbox l'envoie dans `int_buffer`.
    Une commande BIOS l'envoie à `ccb.addr + 12h + longueur`, et `ccb.addr` est périmé : 0 à la
    mise sous tension, c'est-à-dire la table des vecteurs.
- **`from_mailbox`, `current_mbo` et `ccb.status` périmés** pour les sous-fonctions 02h, 03h,
  04h et 08h de 03h. Effets : la fausse complétion de l'ancien CCB, un MBO libéré, et un faux
  succès sur cible absente.
- **ABORT.** En mailbox de 4 octets, il écrit en `mba + c*8` (`:1411`), et il n'interrompt rien
  (deux MBI pour un CCB).
- **RETURN SETUP DATA** est tronqué à 20 octets, le reste mis à zéro (`:1138-1140`).
- **`mbo_req`.**
  - Il compte les 02h, et ni HRST, ni SRST, ni 01h ne le remettent à zéro.
  - Le balayage de la mailbox BIOS n'est gardé par rien : le CCB normal de la même échéance est
    écrasé.
  - Le MBI est écrit sans vérifier qu'il est libre.
  - MAILBOX INIT accepte un compte nul.
- **La machine d'états.**
  - `process_cmd` est réentrant depuis l'OUT, dans tous les états.
  - `status` vaut 0 à la mise sous tension.
  - Une CDB courte est complétée de zéros, un octet par échéance.
  - Le CHS vers LBA ne retranche pas 1 au secteur (`:773`).
  - Les adresses ne bouclent pas à 24 bits.
  - Les resets de la carte ne remettent pas le bus SCSI à zéro : une transaction interrompue le
    fige jusqu'au reset matériel du PC.
- **Les phases de longueur nulle** (READ(10) de compte 0, allocation 0, MODE SELECT de 0) et la
  cible plus longue que le CCB : le va-et-vient NEXT_PHASE ↔ READ_DATA sans fin, et tout le bus
  perdu.
- **Le disque, ses réponses.**
  - READ CAPACITY rend `sectors`, et non `sectors - 1`.
  - Un LBA hors capacité se lit sans erreur, et rend le dernier secteur lu ou ÉCRIT (`buf`
    commun).
  - MODE SENSE : en-tête faux (08h à l'octet 2), descripteur mal formé, bourrage à
    min(i0, L) + ⌈(L − i0)/2⌉, géométrie fixe, PC et DBD ignorés.
  - REQUEST SENSE : longueur additionnelle 0 ; le sense persiste.
  - INQUIRY : version 0, longueur 0, CmdQue annoncé, EVPD ignoré.
  - 25h pour tous les refus.
  - FORMAT, MODE SELECT et VERIFY sont simulés.
- **Le LUN du CCB n'est jamais transmis** (pas d'IDENTIFY) : le disque répond sur ses huit LUN.
- **`scsi_bus_close` ne ferme rien** (PB-121, à élargir). Pour un disque, le tampon stdio du
  dernier secteur écrit reste en vol jusqu'à `exit()` (PCem) ou `fflush(NULL)` (l'oracle). Après
  un reset matériel, le tampon orphelin est vidé en DERNIER.
- **PB-125 à amender** : sous glibc, `hdd_file.c` avec `transfer < 0` ne plante pas toujours.
  Il écrit 4 Ko hors capacité, ou fait l'E/S à la position courante.

## Les étapes

### G11.0 — Le socle, le disque et la carte, sous l'oracle  ✅ *fait, VERIFICATION.md § G11.0*

Sans image d'abord : la carte seule, puis la carte et le disque vierge.
- **La règle ISA 16 bits et la ROM absente.** Un contrôle unique, `pc.check_hdd_controller()`,
  rangé à côté de `check_cpu` et appelé aux mêmes points. Il porte sur la machine FINALE, AVANT
  les trois poussées vers l'oracle (`BootDiff`, Phase2, SpeakerProbe). Le registre gagne le
  champ `Rom` ; pour les cartes de disque, `RequiresAtMachine` (DEVICE_AT) est le bus de 16 bits :
  `mfm_at`, `ide` et `aha1542c`. *(À l'exécution : `check_cpu` appelle `check_hdd_controller` ;
  boot-diff gagne `--hdd-controller`.)*
- **Les sept lecteurs.**
  - `loadconfig` lit `hdg_*` à `hdi_*`.
  - BootDiff passe à `NHd = 7`, et `CompareImages` compare G: à I:.
  - `runw` relève `Image [C-I]`.
- **Le vidage hôte des flux disque** (décision n° 7) : un registre fort des `FileStream` de
  `hdd_file`, vidé du plus récent au plus ancien à `closepc`, avec FileShare ReadWrite et
  Delete.
- **Les autres pièces de socle** :
  - `mem_readw_phys` et `mem_readl_phys` ;
  - `nvrfopen` passe en `internal` ;
  - `nvr/default/aha1542c.nvr` est livré.
- **Les transcriptions.**
  - `Scsi/scsi_hd.cs`.
  - `scsi_bus_init` et `scsi_bus_close`.
  - `Scsi/scsi_aha1540.cs`, avec la disposition params/result modélisée.
  - Les gardes R9 de la liste.
- **L'oracle** :
  - `harness_aha.c` inclut `scsi_aha1540.c` (le patron de `harness_ide.c`) et porte
    `h_aha_probe` ;
  - `scsi_hd.c` est inclus lui aussi (sa struct est lue par la sonde) et la souche est retirée ;
  - `h_boot` reçoit la branche `aha1542c` ;
  - ABI 53.
- **La sonde de la carte** (`--expect-aha`) couvre les états, les mailbox, le CCB, la CDB,
  l'EEPROM, la RAM d'ombre, `int_buffer` et le bus. Preuve qu'elle mord : une panne C# seule qui
  la rougit sans rougir la trace.
- **Les mesures M1 à M8**, d'abord en C# seul :
  - les commandes que la ROM v1.01 émet ;
  - si 22h l'est ;
  - les ID que 15h interroge, et dans quel ordre par rapport au sense ;
  - la base du secteur ;
  - `ccb.addr` au premier 02h BIOS ;
  - si le message de conflit de bus s'affiche.
- **Porte `bd-ami486-aha-post`** : la bannière de la ROM, sans disque puis avec un disque vierge.

### G11.1 — Les disques : formatage et amorçage  ✅ *fait, VERIFICATION.md § G11.1*

- **L'image** : `scsic.img`, 64 têtes × 32 secteurs × 20 cylindres (20 Mio), à la taille EXACTE
  (`truncate`).
  - Recette g5w en C# seul : FDISK et FORMAT C: /S depuis la disquette DOS 5, DEBUG, MEM et
    CHKDSK décompressés par EXPAND (la disquette 3 en B:), puis l'empreinte est figée.
  - Le « System will now restart » de FDISK est un reset à chaud : la carte n'est pas remise à
    zéro. C'est fidèle, et la sonde le voit.
- **Portes.**
  - `bd-ami486-aha-format` : l'image C: comparée octet par octet ; elle n'est verte qu'avec le
    vidage de `closepc`.
  - `bd-{ami486,ami386dx,ami286}-aha-boot` : DOS amorcé par l'INT 13h de la ROM de la carte.
  - `bd-ami486-aha-c8` : la ROM en C8000h et les ports en 330h.
- **Contrôle négatif** : le vidage retiré de `closepc` rougit `-format`.

### G11.2 — Le banc AHABANC, `r9-aha`, `r9-scsihd`  ✅ *fait, VERIFICATION.md § G11.2*

> **Amendé à l'exécution (05/10)** : SCSIBANC est fondu dans AHABANC. Un seul programme, en assembleur
> (`tools/ahabanc/ahabanc.S`, GNU as), mène les commandes d'hôte, les CCB des deux disques et les commandes
> BIOS. `ahacfg` est fondu dans `r9-aha`, dont il est l'onzième essai.

- **AHABANC** (DEBUG tapé, comme ZIPBANC) : les commandes d'hôte, puis les CCB, dans un ORDRE
  imposé.
  - D'abord un CCB de mailbox dans un tampon du banc, pour que `ccb.addr` y pointe.
  - Ensuite seulement 03h, puis ABORT, la CDB courte, 22h de 255 octets (montré des deux côtés,
    l'oracle survivant), RETURN SETUP DATA de 44, et le sense relu dans le CCB.
  - JAMAIS une commande qui mène à un `fatal` ou à un SIGSEGV de l'oracle.
- **SCSIBANC.**
  - INQUIRY (dont EVPD) ;
  - MODE SENSE aux allocations 4, 36 et 255, et la page 3Fh ;
  - REQUEST SENSE persistant ;
  - le LUN fantôme ;
  - READ CAPACITY ;
  - le LBA de capacité relu après un WRITE (le motif) ;
  - READ(10) de 600 secteurs (le régime contigu : décision n° 9) ;
  - les règles de CCB : DATA IN au moins égal à la cible, DATA OUT égal, jamais de phase nulle
    dans une porte comparée.
- **`r9-aha` et `r9-scsihd`**, en C# seul, sur le patron de `R9Zip.cs`, chaque essai sur une
  machine neuve. Chaque site est atteint, sans exception ; une garde retirée donne l'exception
  nommée.
- **`ahacfg`** (r9 de configuration) :
  - la carte sur un 8088 et sur un 8086, par le .cfg, par `--model` puis par
    `--hdd-controller` : refus averti, aucune carte ;
  - la ROM absente ;
  - `cdrom_channel` et `zip_channel` sous la carte ;
  - `addr` et `bios_addr` hors liste ;
  - `hdg_fn` sans géométrie.

### G11.3 — La clôture : les machines et les témoins ; G11 fait  ✅ *fait, VERIFICATION.md § G11.3*

- **Les profils** : `ixtal26-486-scsi.cfg` et sa ligne dans `launchSettings.json`.
- **`--setup-check`** contrôle la carte et la règle 16 bits.
- **L'écran de construction** propose la carte sur un AT seulement (les clés de E: à I: d'un .cfg
  sont gardées à l'enregistrement ; l'écran ne montre que C: et D:).
- **Les témoins** : POST de la ROM (bannière, « Press Ctrl-A »), FDISK et FORMAT, EXPAND, `VER`,
  `MEM`, `CHKDSK C:`, `MSD /S` (le disque), AHABANC sous DEBUG, et le reset à chaud du redémarrage
  de FDISK. *(À l'exécution : KeyScript ne tape pas Ctrl-Alt-Suppr ; le redémarrage de FDISK en
  tient lieu.)*
- **Les originaux intacts** : `os.sha256`, `g5w.sha256` (ajouts seulement).
- **La série complète.**

## La vérification

- Chaque porte boot-diff exige `h_n_fatal == 0` et `R9.Atteints` vide, la sonde AHA identique, et
  les images C: à I: identiques.
- Une série complète par étape qui change l'émulateur (G11.0), les autres sous `PORTES=` ciblé.
  Une série complète finale pour le bilan.
- `check-oracle` sans dérive, et `make -C tools/oracle selftest`.
- Les contrôles négatifs, chacun construit à part et chacun rouge :
  - la sonde (un octet d'EEPROM, `from_mailbox` forcé) ;
  - le vidage de `closepc` retiré ;
  - la complétion par des zéros de la CDB courte supprimée ;
  - `mbo_req` remis à zéro par HRST ;
  - les gardes retirées (dans `r9-*`).

## Les décisions

1. **La 1542C seule.** La BusLogic BT-545S et `MB_FORMAT_8` sont omis (`// omitted:`, avec le
   renvoi à `:650-655`). L'oracle lie le .c entier. *(validé sous mandat, 05/10)*
2. **Ni CD ni ZIP sur le bus SCSI.**
   - Sous `aha1542c`, `cdrom_channel` et `zip_channel` sont ramenés à -1 avec un avertissement
     (DEVIATION de configuration). C'est la portée de l'utilisateur, « AHA-1542C + scsi_hd ».
   - Deux raisons en plus. Le ZIP SCSI déréférence `atapi_dev` NULL au premier READ ou WRITE
     (`scsi_zip.c:642`, `:697`, `:759`, `:819`). La 15h de la ROM sur un CD part en `fatal`
     (`:980`).
   - `scsi_bus_init` reste verbatim : il monte un CD ou un ZIP si le canal le désigne, et la
     configuration ne le désigne jamais. *(validé sous mandat, 05/10)*
3. **La règle ISA 16 bits.**
   - Pour les cartes de disque, DEVICE_AT et le bus de 16 bits coïncident (`RequiresAtMachine`).
     Les cartes son de G12 auront leur propre drapeau : la SB 16 et l'AWE32 ont des drapeaux à 0
     (`sound_sb.c:1349-1352`).
   - Le test se fait contre l'absence de `MODEL_AT`, sur la machine finale : avertissement et
     aucune carte.
   - Elle vaut pour `mfm_at`, `ide` et `aha1542c`, et pour les cartes son 16 bits de G12. C'est
     une règle hôte : PCem ne l'applique qu'à l'écran.
   - À dire : un .cfg existant qui monterait `mfm_at` ou `ide` sur un XT change de comportement
     (aucun profil ni porte ne le fait). *(validé sous mandat, 05/10)*
4. **La ROM absente** est refusée au même point, avec le même effet (R9 de configuration).
   *(validé sous mandat, 05/10)*
5. **Les traitements R9 de la carte**, sur le principe « la commande finit en erreur » :
   - `:679` suit le chemin `invalid` (INVDCMD, HACC).
   - `:530` : statut d'hôte 16h, puis la complétion avec erreur du chemin `:1490-1525`, sans
     transaction sur le bus.
   - `:799`, `:841`, `:883` : `data_in = 20h`, DF et HACC. Une écriture ratée n'est jamais
     annoncée réussie.
   - `:980` : INVDCMD et HACC.
   - `:1743` : `scsi_state = NEXT_PHASE`, la carte suit la phase de la cible ; les octets de CDB
     en trop ne partent pas.
   *(validé sous mandat, 05/10)*
6. **Les traitements R9 du disque** :
   - au-delà de 2 × 256 Ko pour `data_in` (`:87`, `:717`), l'octet est compté, non gardé, et se
     relit nul ;
   - au-delà de 256 Ko pour `data_out` (`:728`, `:101`), même chose.
   La garde de `hdd_file` (PB-125) est gardée telle quelle, amendée, et ses cas sont tenus hors
   des portes comparées. *(validé sous mandat, 05/10)*
7. **`scsi_bus_close`** est reproduit en no-op (PB-121). En compensation, une DEVIATION hôte, le
   pendant exact du `fflush(NULL)` d'`h_closepc` et de l'`exit()` du C : `hdd_file` tient un
   registre fort de ses flux, que `closepc` vide (du plus récent au plus ancien) puis ferme. Sans
   elle, aucune porte à disque SCSI n'est verte. *(validé sous mandat, 05/10)*
8. **PB-136 (22h)** est reproduit par un accesseur de recouvrement : `params[0..63]`, puis
   `result_pos` et `result_len` en petit-boutien, puis `result[]`. L'oracle survit ; la règle
   est l'identité stricte. *(validé sous mandat, 05/10)*
9. **Les tampons de `scsi_hd`** sont un seul tableau de 2 × 256 Ko : `data_in`, puis `data_out`.
   Cela reproduit le régime du C de 513 à 1 024 secteurs, où PCem ne plante pas, et le READ de
   600 secteurs devient comparable à l'oracle. `scsi_zip` n'est pas touché (noté pour G13).
   *(validé sous mandat, 05/10)*
10. **L'EEPROM.**
    - `nvr/default/aha1542c.nvr` est la copie de celle de PCem.
    - Le fichier écrit est `nvr/.aha1542c.nvr`, PARTAGÉ par toutes les configurations
      (`config_name` vide).
    - Les portes prouvent au compteur qu'elles n'émettent pas 22h, sauf AHABANC.
    *(validé sous mandat, 05/10 ; amendé à l'exécution : AHABANC programme les octets 32 à 255,
    les 32 premiers, les seuls persistés, restent ceux de la référence ; le fichier que l'oracle
    écrit est donc celui que le C# relit, et aucun répertoire `nvr` par côté n'est nécessaire)*
11. **Les canaux** : de -1 à 3 sous `ide` et `xtide`, -1 seulement sous `aha1542c`
    (décision n° 2). Sinon -1, avec un avertissement. *(validé sous mandat, 05/10)*
12. **Les clés `hdg_*` à `hdi_*`** sont toujours lues ; avertir si elles sont posées sans
    contrôleur SCSI. Avertir aussi d'un disque SCSI sans géométrie, ou dont la capacité déborde.
    *(validé sous mandat, 05/10)*
13. **Les machines** : `ami486` (porte principale, bancs, profil), `ami386dx` et `ami286`
    (amorçage). Les quatre machines sans `MODEL_AT` refusent la carte. *(validé sous mandat,
    05/10)*
14. **Ce fichier** est commité avec G11.0. *(validé sous mandat, 05/10)*

## Les risques

Ce que G11 laisse :

- **La couverture de la carte.** La 1542C accepte vingt-cinq commandes d'hôte. AHABANC en passe
  dix-neuf ; la ROM en passe quatre autres à chaque porte (24h, 25h, 29h, 82h). Restent 26h et 27h,
  les bancs de la ROM : SCSISelect (Ctrl-A au POST) seul s'en sert, avec la RAM d'ombre, et aucune
  porte ne les exerce, KeyScript ne tapant pas Ctrl-A.
- **Les resets.** Aucun reset de la carte (HRST, SRST, BRST) n'est passé sous l'oracle : la
  réentrance de PB-142 en RESET et `mbo_req` (PB-141) qui survit à HRST ne sont pas montrés.
  Un reset en pleine transaction fige le bus (PB-132) : les bancs n'y vont pas, `r9-scsihd` le
  montre en C# seul.
- **La traduction CHS** de 03h (PB-144) n'est montrée que sur le secteur 1 du cylindre 0 ; les
  disques de plus de 1 Go (la traduction 255 × 63 de la ROM) ne sont pas essayés.
- **Les disques.** Un seul disque de DOS (20 Mio) et un disque vierge. Ni Windows 3.1 sur SCSI, ni
  pilote ASPI réel (ASPI4DOS.SYS n'est pas sur les disquettes de l'utilisateur). AHABANC en tient
  lieu.
- **Le reset matériel** (le menu de l'hôte) avec un disque SCSI : le tampon orphelin de PB-121 est
  reproduit, et `closepc` vide le registre dans l'ordre de la glibc. Aucune porte ne fait de reset
  matériel au milieu d'une session.
- **L'écran de construction** ne montre que C: et D: ; les ID 2 à 6 se posent dans le .cfg.
- **`scsi_zip.c`** garde ses gardes à 256 Ko (G10.6), là où `scsi_hd` reproduit désormais le régime
  de 256 à 512 Ko (décision n° 9) : la même correction attend G13.
