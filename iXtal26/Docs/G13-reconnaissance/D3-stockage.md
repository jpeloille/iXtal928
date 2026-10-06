# D3 — Les disques et le stockage : reconnaissance pour le plan de G13

> Lecture du 6 octobre 2026, sur `master` à `bc609ce`. LECTURE SEULE : aucun fichier du dépôt n'a été modifié,
> rien n'a été construit ni lancé. Rien n'est décidé : ce fichier fournit la matière du plan.
>
> Lu d'abord : `PLAN.md` § G13 ; `TRANSCRIPTION.md` § Règles (R1 à R9) ; l'en-tête de `PCEM_BUGS.md` et les
> quarante-huit entrées du domaine. Puis le code : `iXtal26/{Floppy,Disc,Mfm,Ide,Cdrom,Scsi}/`, `pc.cs`,
> `Host/HddImage.cs`, `Host/HardDiskControllers.cs`, `Host/SdlMenu.cs` ; l'outillage : `tools/idecheck/`,
> `tools/atapibanc/`, `tools/ahabanc/`, `tools/gates/series.sh`, `tools/iXtal26.Diff/{R9*,CdImageCheck}.cs`,
> `tools/oracle/harness*.c` ; `VERIFICATION.md` §§ G5, G10.3 à G10.6, G11.0 à G11.2 ; `PLAN-G11.md`.
> Le mécanisme commun (gardes, clé, fichiers propres au mode) est celui que propose la lecture D7 : les
> propositions ci-dessous s'y insèrent sans le présupposer.

**Conventions.** Niveaux du vrai comportement : *documenté* (une source primaire le dit), *déduit*
(raisonnement depuis des sources), *inconnu* (aucune source trouvée). Classement : **(a)** corrigeable et
vérifiable ; **(b)** corrigeable, vérification faible ; **(c)** vrai comportement inconnu — à mesurer sur
matériel réel, laissé reproduit d'ici là ; **(d)** à ne pas corriger. « Chemin chaud » : exécuté par
instruction ou par octet à haute fréquence. Aucune correction du domaine n'y est : les chronomètres de la
disquette (26 à 32 µs, moteur allumé) et de l'AHA (10 µs, carte active) sont tièdes, tout le reste est froid
(par commande ou par écriture de registre).

---

## 1. À savoir d'abord — ce que la lecture a trouvé en chemin

1. **PB-17 fait tomber l'hôte aujourd'hui.** Le C déborde `track_data` ; le C# (`Disc/disc_img.cs:86-87`)
   prend `buf.AsSpan(0, 23 552)` sur un tableau de 20 480 : `ArgumentOutOfRangeException`, que rien ne
   rattrape sur ce chemin (les `catch` de l'hôte n'entourent que l'ouverture des fichiers), dès l'INSERTION
   d'une image XDF ED — au démarrage (`pc.cs:1197`) comme au menu Ctrl+F12 (`Host/SdlMenu.cs:972`) — `disc_load` →
   `fdd_disc_changed` → `img_seek`, que `img_load` vient de poser. Même chute pour toute image dont le BPB
   annonce de 20 481 à 25 000 octets par piste (42 × 512, 24 × 1 024…). C'est une donnée de l'utilisateur qui
   arrête l'émulateur : le précédent de PB-110 (décision n° 4 de G10.3) la range sous R9, indépendamment de G13.
2. **PB-121 est rangé en section C, mais il a un effet observable** depuis son élargissement de G11 : après un
   reset matériel (le menu de l'hôte) avec un disque SCSI, le nouveau flux relit le dernier secteur écrit tel
   qu'il était sur le disque, et à la sortie le tampon orphelin, vidé en dernier (`Disc/hdd_file.cs:89-103`,
   l'ordre de la glibc), réécrit l'ancien contenu par-dessus. C'est l'IMAGE DE L'UTILISATEUR qui en garde la
   trace, en mode PCem comme demain.
3. **PB-25 a un site non marqué** : `Mfm/mfm_at.cs:186` et `:190` reproduisent le défaut du Fixed Disk Adapter
   de l'AT (`mfm_at.c:113`, `:121`, que l'entrée cite) sans marqueur `// pcem bug, reproduced: PB-25`.
4. **PB-30 : le registre ne suit plus le code.** L'entrée dit « les trois définitions sont conservées » ; le C#
   les omet (`Mfm/mfm_xebec.cs:39`, `:98`, `:109`, `// omitted:`).
5. **PB-16 n'a pas, dans iXtal, l'effet que décrit l'entrée.** `disc_close` et `disc_reset` remettent
   `drives[drive].seek` à nul avant tout chargement (`Disc/disc.cs:159-171`, `:262-282`) : le chemin
   `fdd_disc_changed → img_seek` sur un flux fermé n'est jamais atteint, et `Stream.Close()` est idempotent.
   L'effet réel : le lecteur paraît chargé sans aucune fonction ; l'invité voit un changement de disquette, puis
   « adresse non trouvée » (comme une disquette non formatée) ; le menu Ctrl+F12 annonce « inséré »
   (`Host/SdlMenu.cs:972-983` teste `drive_empty`, resté à 0).
6. **Sites sans marqueur** : PB-109 (l'ifstream modélisé, seul l'en-tête de `Cdrom/cdrom_image.cs:13-18` le
   nomme), PB-127 (huit sites, `Scsi/scsi_zip.cs:764-960`), PB-134 (en-têtes seulement), PB-129 à PB-131 (en-tête
   de `Scsi/scsi_hd.cs:14` ; seul `:148` nomme PB-130).
7. **Un défaut voisin, non inscrit** : `ide_irq_update` (`ide.c:138-143`, `Ide/ide.cs:228-234`) BAISSE la ligne
   quand l'IRQ est déjà en attente au PIC, même si l'unité choisie la tient toujours (`irqstat` = 1,
   nIEN = 0) : la première branche ne relève que si rien n'est en attente, la seconde baisse dès que quelque
   chose l'est. Sur le vrai bus, INTRQ reste haute tant que l'interruption est pendante (ATA-1, § 6.3.10), et
   la demande du 8259 reste posée. Une écriture de 1F6h ou de 3F6h entre la levée et la prise en compte d'une
   IRQ la perd. À instruire avec PB-71 (une nouvelle entrée ?).
8. **PLAY AUDIO MSF teste la piste sur la position encore compactée** (en-tête de `Cdrom/cdrom-image.cs:13-17`,
   conséquence de PB-111) : la lecture part même sur une piste de données. SFF-8020i § 10.8.8 exige ILLEGAL
   REQUEST, ILLEGAL MODE FOR THIS TRACK (05/64h). Hors registre ; à joindre à PB-123.
9. **Le « sens du transfert ignoré » de PB-144 est conforme à la carte** : « For an Initiator CCB, the direction
   of data transfer is established by the SCSI command being executed independent of the value of bits 3 and
   4 » (AHA-1540C Technical Reference, CCB, octet 1). Ce qui manque, c'est le contrôle de longueur que ces bits
   arment (statut d'hôte 12h).
10. **SFF-8020i r2.6 (1996) n'a ni GET EVENT STATUS NOTIFICATION (4Ah) ni MODE SELECT/SENSE(6)** (Table 37) :
    pour PB-118 et PB-119, le « vrai lecteur » dépend de la génération qu'on prend pour référence (question Q5).
11. **Deux reliquats déjà promis à G13 dans le domaine** : `PLAN-G11.md` (décision n° 9 et « Les risques ») —
    les tampons de `scsi_zip` restent gardés à 256 Ko là où `scsi_hd` reproduit le régime de 256 à 512 Ko,
    « la même correction attend G13 » (c'est de la fidélité au mode PCem, PB-125) ; et `PLAN.md`, « Hors plan,
    R9 (a) » — les dix-sept `fatal()` de protocole de `mfm_xebec.c`, que la documentation IBM trouvée ici permet
    enfin de traiter (§ 4.2, PB-26 et PB-27).

---

## 2. Les sources

| Document | Où | Ce qu'il établit ici |
|---|---|---|
| IBM PC AT Technical Reference, 1502494, mars 1984 — « Fixed Disk and Diskette Drive Adapter », § *Data Rates* ; listing du BIOS (« 00: 500 KBS / 01: 300 KBS / 10: 250 KBS ») ; table des IRQ (IRQ 15 « Reserved ») | https://archive.org/details/bitsavers_ibmpcat150ferenceMar84_26847525 | PB-14 |
| NEC µPD765 Data Sheet, déc. 1978 — WRITE DATA (« must occur every 31 µs in the FM mode, and every 15 µs in the MFM mode … sets the OR (Over Run) flag in Status Register 1 … and terminates the Write Data Command ») ; ST1 bit D4 | https://archive.org/details/bitsavers_necdatasheec78_1042541 | PB-19 |
| IBM Fixed Disk Adapter, Hardware Reference Library 6361503 — *Programming Considerations* : Status Register (octet de fin), Sense Bytes, Disk Controller Error Tables, DCB, commandes de classe 0 | https://www.minuszerodegrees.net/oa/OA%20-%20IBM%20Fixed%20Disk%20Adapter.pdf | PB-22, 23, 25, 26, 27 |
| ATA-1, brouillon X3T9.2 791D révision 4, 17 mars 1993 — § 6.3.10 INTRQ, § 7.2.13 registre d'état (BSY), § 9.19 *Read verify sector(s)* | http://www.os2museum.com/files/docs/ata/ata-r4-utf8.txt (liste : http://www.os2museum.com/wp/historical-ata-standard-drafts/) | PB-71, 72, 74 |
| ATA-3, brouillon X3T13 2008D révision 7b, 27 janvier 1997 — § 4.2.10 INTRQ, registre d'état (BSY : « a write to a command block register by the host shall be ignored by the device »), § 7.19 READ VERIFY SECTOR(S) | https://www.scs.stanford.edu/23wi-cs212/pintos/specs/ata-3-std.pdf | PB-72, 74 |
| National Semiconductor PC87415, fiche technique — « IRQ14 mirrors CH1-INT … IRQ15 mirrors CH2-INT if Legacy mode is enabled » | http://ftp.parisc-linux.org/docs/chips/PC87415.pdf | PB-71 |
| SFF-8020i *ATA Packet Interface for CD-ROMs*, révision 2.6 proposée, 22 janvier 1996 — Table 37 (commandes), Figure 11 (formats de secteur), note du modèle CD-ROM (capacité), § 10.8.4 MODE SELECT, Table 58, § 10.8.8 PLAY AUDIO, § 10.8.14 READ CD-ROM CAPACITY, § 10.8.18 READ SUB-CHANNEL, § 10.8.19 READ TOC (formats 00b à 10b, Table 131) | https://www.jmargolin.com/project/SFF8020i.pdf | PB-106, 117 à 120, 122, 123 |
| MMC-2, T10/97-108r0 (clause 9 de SFF-8090 r0.9), 1997 — § 9.1.2 GET EVENT STATUS NOTIFICATION | https://www.t10.org/ftp/t10/document.97/97-108r0.pdf | PB-118 |
| INF-8070i *ATAPI Removable Rewritable Specification*, révision 1.2, 4 novembre 1998 — READ CAPACITY (Table 41, « Last Logical Block Address »), START/STOP UNIT (Tables 58 à 60) | https://www.isdaman.com/alsos/hardware/hdc/INF-8070.PDF | PB-126 |
| SCSI-2, X3.131-1994 (brouillon X3T9.2/86-109 rév. 10L, version HTML) — §§ 6.2.2, 6.6.7, 7.2.2, 7.2.4, 7.2.6, 7.5.3, 7.6, 8.2.5.1, 8.2.8, 8.2.14, 8.3.3, 9.1.2, 9.2.6, 9.2.7, 9.3.3 | https://www.staff.uni-mainz.de/tacke/scsi/SCSI2-06.html (et `-07`, `-08`, `-09`) | PB-126, 129 à 133 |
| Adaptec *AHA-1540C Series Technical Reference Manual*, 510397-00A, avril 1993 — réglages d'usine, ports (HRST, SRST, IRST, SCRST ; STST, INIT, IDLE), *Hard Reset* et *SCSI Reset Operations*, commandes 01h, 02h, 03h, 0Dh, 22h, 23h, 82h, MBO, MBI, CCB, statuts d'hôte | http://bitsavers.informatik.uni-stuttgart.de/pdf/adaptec/pc/AHA-1540A/510397-00A_AHA-1540C_Series_ISA_SCSI_Technical_Reference_Manual_199304.pdf | PB-132, 133, 136 à 144 |
| Adaptec *AHA-1540A/1542A User's Manual*, 1989 — § 4.2.2 (le port de commande, les octets en trop) | https://archive.org/details/bitsavers_adaptecAdaersManual1989_8298811 | PB-142 |
| Format CUE : CDRWIN User's Guide, annexe A (Golden Hawk Technology), l'origine ; repris par libodraw, *CUE sheet format*, § 5.6 INDEX | https://github.com/libyal/libodraw/blob/main/documentation/CUE%20sheet%20format.asciidoc | PB-108 |
| A. Brouwer, *Zip drives: size and geometry* (un disque ZIP 100 : 100 663 296 octets, 196 608 secteurs) | https://aeb.win.tue.nl/linux/zip/zip-1.html | PB-126 |
| ROM AMI 286 `amic206.bin` (table des types à F000:E401), désassemblée en M12 | `roms/`, `PCEM_BUGS.md` PB-34 | PB-34 |

Non trouvés : une documentation du DTC 5150X (PB-28) ; une spécification de l'interface ATAPI du ZIP par
Iomega (PB-126, START STOP UNIT) ; le texte d'ECMA-130 (le serveur n'a pas répondu) — la Figure 11 de
SFF-8020i en tient lieu pour le format des secteurs.

---

## 3. Tableau récapitulatif

| PB | Titre court | Effet pour l'invité | Classe | Chaud | Vérification proposée |
|---|---|---|---|---|---|
| **A** | | | | | |
| 14 | `disc_set_rate` : 300 kbit/s cadencé à 250 | une 360 Ko dans un 1,2 Mo (le B: des AT) : 32 µs par octet au lieu de 26,7 | a | non | test C# seul : écart entre octets à CCR = 01 |
| 22 | FORMAT TRACK (Xebec) garde `sector` | formatage décalé vers la piste suivante | a | non | test C# seul Xebec : FORMAT après un READ |
| 23 | bit d'unité de l'octet de fin nul | aucun connu (le BIOS teste le bit 1) | a | non | test C# seul : octet de fin de l'unité 1 |
| 25 | têtes : `>` au lieu de `>=` (Xebec et AT) | la tête `hpc` lit/écrit une autre piste | a | non | test C# seul : 21h (Xebec), IDNF (AT) |
| 28 | FBh du DTC sur unité absente : 256 têtes | non mesuré (POST du DTC à un disque) | c | non | à mesurer sur un DTC 5150X |
| 71 | canal secondaire : test de l'IRQ 14 | IRQ 15 perdues ou doublées sous double activité | a | non | test C# seul : IRQ 15 avec IRQ 14 en attente |
| 72 | sélection pendant le reset | changement d'unité, reset écourté, tête/LBA perdues | c | non | selon Q6 : scénario d'IDECHK en C# seul |
| 74 | VERIFY d'un seul secteur | vérification fausse, registres non avancés | a | non | IDECHK / test C# seul : compte 0, dernier secteur |
| 106 | MODE2/2336 lu à +24 | image 2336 refusée ; piste 2336 décalée de 16 octets | a | non (tiède) | `cdimage-check` C# seul, images d'isogen |
| 107 | image de plus de 2 Gio | rien ne se lit, capacité aberrante | b | non | C# seul : `creuse-2g5.iso` refusée, avertie |
| 108 | INDEX 00 en 00:00:00 ignoré | pistes lues en avance de leur prégap | a | non | `cdimage-check` C# seul, `multi.cue` |
| 109 | échec de lecture collant | fichier illisible jusqu'à la réouverture | a | non | `cdimage-check` C# seul, `multi.cue` p. 3 |
| 117 | READ CAPACITY : blocs + 1 | dernier bloc annoncé refusé | a | non | ATAPIBANC C# seul : 0000001Fh |
| 118 | GESN : toujours « nouveau disque » | média neuf à chaque appel, longueur fausse | b | non | ATAPIBANC C# seul, attendus selon Q5 |
| 119 | MODE SELECT : en-tête et longueur 0 | page lue au mauvais endroit ; phase sans fin | a | non | ATAPIBANC C# seul ; longueur 0 → GOOD |
| 120 | TOC brute sans A0/A1/A2 ni longueur | longueur nulle, pas de fin de disque | a | non | nouveau cas d'ATAPIBANC, C# seul |
| 122 | lead-out sans ADR ni contrôle | `00 00 AA 00` en TOC | a | non | ATAPIBANC C# seul : `00 14 AA 00` |
| 123 | audio en LBA décalé de 150 | la piste demandée ne joue pas ; sous-canal faux | a | non | ATAPIAUD C# seul, valeurs justes et son |
| 126 | ZIP : capacité, fin, éjection, reset, longueur 0 | secteur fantôme, éjection inversée, disque perdu, blocage | a (START STOP : c) | non | ZIPBANC C# seul ; START STOP à mesurer |
| 129 | disque SCSI : capacité + 1, lu sans erreur | secteur fantôme au contenu périmé | a | non | `hw-aha` : n − 1 ; 05/21h |
| 130 | MODE SENSE : en-tête, descripteur, bourrage | pages fausses, géométrie sans rapport | a | non | `hw-aha` : en-tête, DBD, pages |
| 131 | sense, INQUIRY, refus | erreur ancienne relue ; code inconnu pris pour un LUN | a (FORMAT : c) | non | `hw-aha` : sense 18 o effacé, INQUIRY v2 |
| 132 | phase vide fige le bus ; resets sans effet | commande sans fin, plus aucun disque | a | non | `hw-scsihd`/`hw-aha` : GOOD, HRST libère |
| 133 | LUN du CCB jamais transmis | le disque répond sur ses huit LUN | a | non | `hw-aha` : LUN 1 → 7Fh, 05/25h |
| 136 | 22h lit au-delà de ses paramètres | EEPROM programmée d'octets internes ; IRQ collée | b (hors bornes : c) | non | `hw-aha` dans les bornes ; hors bornes à mesurer |
| 137 | sense automatique inconditionnel, inversé | le pilote ne trouve jamais le sense ; vecteurs écrasés | a (03h : b) | non | `hw-aha` : sense dans le CCB après CHECK |
| 138 | 03h achève un CCB périmé | achèvements fantômes ; disque absent « lu » | b | non | `hw-aha`, attendus déduits |
| 139 | ABORT : mauvais emplacement, n'interrompt rien | deux MBI pour un CCB, CCB affamés | a | non | `hw-aha` : MBO effacé, MBI 03h |
| 140 | RETURN SETUP DATA tronqué à 20 | configuration incomplète | a | non | `hw-aha` : 44 octets ; 0 → 256 |
| 141 | mailbox : compte des 02h, balayage, MBI | CCB partis sans 02h, perdus, écrasés | a (mailbox BIOS : b) | non (tiède) | `hw-aha` : INVDCMD, tourniquet, MBI libre |
| 142 | machine des commandes réentrante, statut 0 | « prête » sans reset ; octets perdus | a (réentrance : c) | non | `hw-aha` : INIT et IDLE à la mise sous tension |
| 143 | CDB courte complétée de zéros | une autre commande s'exécute | c | non | à mesurer sur une 1542C |
| 144 | traduction, adresses, EEPROM, réglages | sans effet pour la ROM ; config par défaut fausse | b (mixte) | non | `hw-aha` : 12h, défauts ; le reste faible |
| **B** | | | | | |
| 16 | `img_load` laisse `f` | disquette « insérée » mais illisible ; menu trompeur | d (hôte : nettoyage) | non | C# seul : image refusée → lecteur vide |
| 17 | `track_data` de 20 Ko | **l'hôte tombe** à l'insertion d'une XDF ED | a (R9 d'abord) | non | C# seul : image XDF ED fabriquée |
| 18 | `strcpy` sur lui-même | aucun ; le C# est exact | d | non | — |
| **C** | | | | | |
| 15 | `switch` mort de `fdd_getrpm` | aucun | d | non | — |
| 19 | `fdc.written` jamais posé | pas d'OR en écriture hors DMA | b | non | C# seul : PIO lent → ST1 = 10h |
| 20 | sept symboles morts (disquette) | aucun | d | non | — |
| 26 | chaînes de `fatal()` fausses (Xebec) | aucun (message) ; tombe avec R9 (a) | d | non | — |
| 27 | trois `switch` sans `default:` (Xebec) | le contrôleur se fige | b (avec R9 (a)) | non | état forgé, C# seul |
| 29 | `ide_fn[4]` dans `scsi_ibm.c` | sans objet (non transcrit) | d | — | — |
| 30 | trois symboles morts (Xebec) | aucun ; déjà omis en C# | d | — | — |
| 34 | type 39 : 462 × 7 au lieu de 987 × 7 | table de l'hôte, pas de l'invité | d (hôte : Q2) | — | — |
| 112 | fichiers d'image laissés ouverts | aucun | d | — | — |
| 121 | lecteur de l'amorçage précédent jamais fermé | **image SCSI corrompue** après un reset matériel | a | non | C# seul : écrire, reset, relire, sortir |
| 127 | huit `fatal()` inatteignables (ZIP) | aucun | d | — | — |
| 134 | vingt-deux `fatal()` inatteignables (SCSI) | aucun | d | — | — |

**Comptes** (48 entrées) : **(a) 27** — 25 de la section A, PB-17 (sous R9), PB-121 ; **(b) 7** — PB-107, 118,
136, 138, 144, 19, 27 ; **(c) 3** — PB-28, 72, 143 ; **(d) 11** — PB-16, 18, 15, 20, 26, 29, 30, 34, 112, 127,
134. Classés (a) avec une part (c) : PB-126 (START STOP UNIT), PB-131 (FORMAT UNIT), PB-142 (la réentrance) ;
avec une part (b) : PB-137 (la commande 03h), PB-141 (la mailbox du BIOS). PB-136 est (b) avec une part (c).

---

## 4. Les fiches

Chaque fiche : 1. le défaut et l'effet ; 2. les sites (C de PCem, puis C#) ; 3. le vrai comportement, son niveau
et sa source ; 4. la correction (où, quoi, taille, chemin chaud, temps visibles, état sondé par l'oracle,
fichiers de l'utilisateur) ; 5. la vérification ; 6. le classement ; 7. les dépendances. « Mode PCem inchangé »
va de soi partout : chaque correction est gardée ; la série entière, côté PCem, en reste la preuve.

### 4.1 La disquette

#### PB-14 — `disc_set_rate` : le cas 1 retombe dans le cas 2
1. Sans `break`, `rate == 1` (300 kbit/s) finit à `disc_period = 32` (250 kbit/s). Effet : sur un AT, une
   disquette 360 Ko dans un lecteur 1,2 Mo — le B: de tous les profils AT (`drive_b_type = 2`), que le BIOS
   lit à 300 kbit/s — défile à 32 µs par octet au lieu de 26,7 : chaque octet, et chaque tour apparent de la
   piste, dure 20 % de trop. Les données restent justes (le contrôle des cellules de bit passe par `fdc_update_rate`, juste).
   PC, XT, M24, PC1512 : `rate` vaut toujours 2, non concernés. Les branches `drvden` 1 et 2 sont mortes :
   `fdc_update_drvrate` n'a d'appelant ni en C ni en C# (`Floppy/fdc.cs:324`).
2. C : `disc/disc.c:156-180` (`:160-175`), appelé par `floppy/fdc.c:268-272`. C# : `Disc/disc.cs:254`
   (`goto case 2`), appelé par `Floppy/fdc.cs:421`.
3. 300 000 bit/s, 26,67 µs par octet MFM — **documenté** : IBM PC AT Technical Reference, « Fixed Disk and
   Diskette Drive Adapter », § Data Rates (« three data rates: 250,000, 300,000 and 500,000 bits per
   second ») ; listing du BIOS, « 01: 300 KBS ».
4. `case 1` se termine en mode matériel. 26 µs (la valeur que PCem écrit pour ce débit) ou 80/3 µs exacts
   (`TIMER_USEC` est en virgule fixe 32.32) : détail à trancher. 2 à 4 lignes. Froid (écriture de 3F7h) ; la
   période du chronomètre change : **temps visibles, oui** (cadence et rotation du B:). Sondé : `h_disc_probe`
   lit le reste de `disc_poll_timer` — seulement en mode PCem. Fichiers : aucun.
5. Test C# seul (`hw-fdc`) : ami286, image 360 Ko en B:, CCR = 01, READ DATA d'un secteur ; l'écart entre deux
   appels de `disc_poll` vaut 26 (ou 26,67) µs. Contrôle négatif : sans la garde, 32 µs.
6. **(a).**
7. Aucune.

#### PB-19 (C) — `fdc.written` n'est jamais posé à 1
1. En écriture hors DMA, `fdc_getdata` ne détecte jamais que l'UC n'a pas fourni l'octet à temps ; la lecture,
   elle, le détecte (`data_ready`).
2. C : `floppy/fdc.c:46`, `:554`, `:1186`, `:1219`. C# : `Floppy/fdc.cs:94` (`byte_written`), `:705`,
   `:1362-1366`, `:1405`.
3. **Documenté** : NEC µPD765 Data Sheet, WRITE DATA — passé le délai (15 µs en MFM au débit du 8 pouces, le
   double à 250 kbit/s), le contrôleur pose OR dans ST1 et termine la commande ; ST1 D4 : « If the FDC is not
   serviced by the main-systems during data transfers, within a certain time interval, this flag is set ».
4. Une correction a un sens matériel : `byte_written = 1` quand le contrôleur prend un octet en PIO (la remise
   à 0 à l'écriture de 3F5h existe déjà) — 2 à 3 lignes, froid, aucun temps changé.
5. Faible : test C# seul — SPECIFY avec ND = 1, WRITE DATA, l'outil n'écrit pas le deuxième octet : ST1 = 10h.
   Aucune machine d'iXtal n'écrit en PIO (le BIOS passe par le DMA, aucun PCjr) : bénéfice limité aux logiciels
   exotiques ; risque de faux OR si un invité lent tient tout juste le délai dans le temps émulé.
6. **(b)** — section C, mais pas du simple nettoyage.
7. Aucune.

#### PB-15, PB-20 (C) — code et symboles morts
Nettoyage sans effet, aucun sens matériel : le `switch` inatteignable de `fdd_getrpm` et son
`#pragma CS0162` (`Floppy/fdd.cs:23-27`, `:205-212` ; C `fdd.c:143-148`) ; les sept symboles morts conservés
(`Floppy/fdc.cs:90`, `:203-204`, `:215-217` ; `Disc/disc.cs:97-107`, `:293`). **(d).**

#### PB-16 (B) — `img_load` éjecte sans remettre `f` à nul
1. Voir § 1, point 5 : dans iXtal, le lecteur paraît chargé sans fonctions ; l'invité voit une disquette
   illisible, le menu dit « inséré ».
2. C : `disc/disc_img.c:248-249`, `:276-277` ; `disc.c:83`. C# : `Disc/disc_img.cs:344`, `:377`.
3. Sans objet : refuser un format d'image n'a pas d'analogue matériel ; ce que voit l'invité ressemble à une
   disquette non formatée — **déduit**.
4. Pas de correction de fidélité. Nettoyage d'hôte possible (Q2) : `f = null`, lecteur rendu vide, message
   « refusé » — 3 lignes ; aucune porte ne charge de telle image.
5. Test C# seul : une image au BPB de 30 × 1 024 octets par piste ; `drive_empty == 1` après `disc_load`.
6. **(d)** pour le mode matériel ; correction d'hôte selon Q2.
7. PB-17 (même fonction).

#### PB-17 (B) — `track_data` fait 20 Ko, une piste XDF ED en demande 23 552
1. Voir § 1, point 1 : **l'hôte tombe** à l'insertion.
2. C : `disc/disc_img.c:9`, `:347-352`. C# : `Disc/disc_img.cs:38` (taille), `:86-87` (`fread`),
   `:454-466` (marqueur).
3. Un lecteur ED (2,88 Mo, 1 Mbit/s) lit une disquette XDF ED avec le pilote XDF d'OS/2 ou de PC DOS 7 —
   **déduit**.
4. Tampon de piste à la taille maximale que `img_load` admet (25 000 octets ; 32 Ko) — 1 ligne. Froid. La
   correction diverge de l'oracle (UB), mais aucune porte ne charge d'image XDF ED : elle peut valoir dans les
   deux modes, au titre de R9 (Q3). Fichiers : l'image n'est écrite que si l'invité l'écrit (XDF est forcé en
   lecture seule, `disc_img.cs`, `xdf_type != 0`).
5. Test C# seul sur le patron de `r9-cue` : une image XDF ED fabriquée (BPB 46 × 512, deux faces) insérée →
   aucune exception, la piste 0 se lit. Contrôle négatif : 20 Ko rendent l'exception.
6. **(a)**, d'abord comme correction R9.
7. PB-16.

#### PB-18 (B) — `disc_load` copie `discfns[drive]` sur lui-même
Aucun effet en C (glibc) ; en C#, `discfns[drive] = fn` (`Disc/disc.cs:146`) est exact : rien n'est reproduit.
Seul le marqueur ment ; il devient « sans objet en C# ». **(d).**

### 4.2 Les cartes MFM : Xebec (IBM Fixed Disk Adapter), DTC 5150X, Fixed Disk Adapter de l'AT

#### PB-22 — `CMD_FORMAT_TRACK` ne réinitialise pas `sector`
1. Le formatage démarre au secteur laissé par la commande précédente : il déborde sur la piste suivante et
   laisse le début de la piste visée ; un résidu ≥ 17 fait échouer la commande (21h). Latent sous PC DOS 2.00.
2. C : `mfm/mfm_xebec.c:369-387` (le calcul : `:236-270`). C# : `Mfm/mfm_xebec.cs:446-470` (marqueur `:452`).
3. **Documenté** : IBM Fixed Disk Adapter, table des DCB — Format Track (classe 0, opcode 06) : octet 1 =
   unité et tête, octet 2 = deux bits hauts du cylindre et bits 0 à 5 À ZÉRO, octet 3 = cylindre, octet 4 =
   entrelacement : la commande formate la piste désignée, depuis son début.
4. `xebec.sector = 0` en tête de la commande — 1 ligne, froid. **Fichiers de l'utilisateur** : le contenu de
   l'image après un formatage, ce que la correction vise.
5. Test C# seul (`hw-mfm`, un XT à Xebec conduit par 320h-323h comme `R9Aha` conduit la 1542C) : READ du secteur
   5, puis FORMAT TRACK (c, h) ; les dix-sept secteurs de (c, h) reçoivent le motif de `hdd_format_sectors`,
   (c, h + 1) reste intact.
6. **(a).**
7. Aucune.

#### PB-23 — le bit d'unité de l'octet de fin est toujours nul
1. `completion_byte = drive_sel & 0x20` sur une valeur 0 ou 1. Effet : aucun connu ; le BIOS d'IBM ne lit que
   le bit 1 (erreur).
2. C : `mfm_xebec.c:293`. C# : `Mfm/mfm_xebec.cs:357-363`.
3. **Documenté** : IBM Fixed Disk Adapter, « Status Register » : bit 1 « an error has occurred », bit 5 « the
   logical unit number of the drive ».
4. `completion_byte = command[1] & 0x20` — 1 ligne, froid.
5. Test C# seul : TEST DRIVE READY sur l'unité 1 → octet de fin 20h (22h avec erreur).
6. **(a).**
7. Aucune.

#### PB-25 — la borne des têtes testée avec `>` au lieu de `>=`
1. Une lecture ou une écriture à la tête `hpc` vise en silence la tête 0 du cylindre suivant.
2. C : `mfm_xebec.c:250`, `:255` ; `mfm_at.c:113`, `:121`. C# : `Mfm/mfm_xebec.cs:295-311` (marqué) ;
   `Mfm/mfm_at.cs:186`, `:190` (**non marqué**, § 1 point 3).
3. Xebec : « Illegal Disk Address. The controller detected an address that is beyond the maximum range »
   (erreur de type 2, code 1, soit 21h) — **documenté** pour le code, **déduit** pour son emploi sur une tête
   hors géométrie (la géométrie vient de la commande 0Ch). Fixed Disk Adapter de l'AT (WD1003) : la tête
   absente ne présente aucun champ ID, d'où IDNF (10h), que `mfm_at` rend déjà pour tout refus d'adresse —
   **déduit**.
4. `>=` aux quatre tests — 4 lignes, froid. **Fichiers** : en mode PCem, une écriture à la tête `hpc` atteint
   une autre piste de l'image ; en mode matériel elle est refusée.
5. Test C# seul : Xebec, INIT DRIVE PARAMS à n têtes, READ à la tête n sur l'unité 0 → octet de fin 02h, sense
   21h ; AT, SET PARAMETERS à n têtes, READ à la tête n → ERR et IDNF.
6. **(a).**
7. Un écart voisin, non inscrit : le TR fait porter au premier octet du sense le bit « Address Valid » (bit 7,
   posé quand la commande précédente visait une adresse) et aux octets 1 à 3 l'adresse fautive ; PCem rend
   `21 00 00 00` (`mfm_xebec.c:314-328`). Sur la vraie carte : `A1h` suivi de la tête, du secteur et du cylindre
   — **documenté** (« Sense Bytes »). À joindre à l'étape MFM si on l'inscrit.

#### PB-28 — `CMD_DTC_GET_DRIVE_PARAMS` (FBh) invente 256 têtes sur une unité absente
1. Propre au DTC 5150X, qu'iXtal propose (`Host/HardDiskControllers.cs:39`). Effet non mesuré : on ne sait pas
   si la ROM du DTC sonde l'unité 1 par FBh au POST.
2. C : `mfm_xebec.c:651-666`. C# : `Mfm/mfm_xebec.cs:763-793` (marqueur `:774`).
3. **Inconnu** : commande propre au DTC, aucune documentation trouvée. Plausible : ERR_NOT_READY (04h), comme
   les trois autres commandes qui touchent une unité absente.
4. Laissé reproduit. Si la mesure confirme 04h : 2 lignes.
5. À mesurer sur un DTC 5150X à un seul disque (réponse à FBh pour l'unité 1). Une mesure d'appoint, en C# seul :
   tracer si la ROM du DTC émet FBh sur l'unité 1 au POST, ce qui dit si l'effet est atteint.
6. **(c).**
7. Aucune.

#### PB-26 et PB-27 (C) — les `fatal()` du Xebec ; trois `switch` sans `default:`
- **PB-26**, quatre chaînes de `fatal()` fausses (C `mfm_xebec.c:131`, `:133`, `:174`, `:671` ; C#
  `Mfm/mfm_xebec.cs:164`, `:788`) : nettoyage sans effet. Mais ces `fatal()` sont VIVANTS dans iXtal : `pc.fatal`
  lève `InvalidOperationException` (`pc.cs:164-169`), et `PLAN.md` les range sous « Hors plan, R9 (a) », en
  attente de la documentation. Elle est trouvée : l'IBM Fixed Disk Adapter définit « Invalid Command » (type 2,
  code 0, soit 20h) et « Illegal Disk Address » (21h). Traiter R9 (a) dans la même étape que PB-22, 23, 25
  (dans les deux modes, R9 n'étant pas le mode matériel) fait tomber les chaînes avec les `fatal()`. **(d).**
- **PB-27**, trois `switch` sans `default:` (C `:314`, `:675`, `:694` ; C# `Mfm/mfm_xebec.cs:386`, `:797`,
  `:818`) : sur un état inattendu, le contrôleur se fige. Les états sont ceux de la machine de PCem ; le vrai
  Xebec n'a pas d'équivalent connu. Finir la commande en erreur (20h), comme R9 le ferait des `default:` qui
  appellent `fatal()` — **déduit**. Vérification : état forgé, C# seul. **(b)**, avec R9 (a).

#### PB-29, PB-30, PB-34 (C)
- **PB-29** : `scsi_ibm.c` n'est pas transcrit ; `Disc/hdd.cs` porte `[7]`. Sans objet. **(d).**
- **PB-30** : déjà fait — le C# omet les trois symboles (`Mfm/mfm_xebec.cs:39`, `:98`, `:109`) ; corriger
  l'entrée du registre. **(d).**
- **PB-34** : le type 39 (462 × 7 chez PCem, 987 × 7 dans la ROM AMI 286) est une table de l'HÔTE
  (`Host/HddImage.cs:67`, `Host/CommandLine/HardDiskTypeListing.cs:24`), pas un comportement de l'invité ;
  le « vrai » type 39 dépend du BIOS (l'AT d'IBM n'a que 23 types). Pas de sens pour le mode matériel. Une
  correction d'hôte est possible (prendre la table de la ROM de la machine) sans effet sur l'oracle (les portes
  créent des disques de type 46) : question Q2. **(d).**

### 4.3 L'IDE

#### PB-71 — le canal secondaire lit l'état de l'IRQ 14, pas le sien
1. `(pic2.pend | pic2.ins) & 0x40` pour les deux canaux. Effet : avec un lecteur sur le secondaire
   (`cdrom_channel` ou `zip_channel` à 2 ou 3), une IRQ 14 en attente fait baisser l'IRQ 15, et une IRQ 15 en
   service ne l'empêche pas d'être relevée : IRQ perdues ou doublées quand les deux canaux travaillent.
2. C : `ide/ide.c:138-143`. C# : `Ide/ide.cs:225-234` ; appelé à `:559` (1F6h) et `:696` (3F6h).
3. **Documenté** : chaque canal a sa ligne — PC87415 : « IRQ14 mirrors CH1-INT … IRQ15 mirrors CH2-INT if
   Legacy mode is enabled » ; ATA-1 § 6.3.10 : INTRQ ne dépend que de l'unité choisie du canal.
4. Masque `board ? 0x80 : 0x40` — 2 lignes, froid, aucun temps changé.
5. Test C# seul (`hw-ide`, ports conduits comme dans `r9-atapi`) : IRQ 14 en attente au primaire, unité
   secondaire avec `irqstat` = 1, écriture de 176h → bit 7 de `pic2.pend` levé.
6. **(a).**
7. Le défaut voisin non inscrit (§ 1, point 7) touche la même fonction : à décider ensemble.

#### PB-72 — sélectionner un lecteur pendant un reset
1. Une écriture de 1F6h pendant le reset logiciel (BSY) change d'unité, termine le reset sur-le-champ et rend
   la main avant de retenir tête, LBA et bits hauts.
2. C : `ide.c:401-420` (et `:586-597`, qui arme le reset). C# : `Ide/ide.cs:524-547` (marqueur `:528`).
   Banc : `tools/idecheck/idecheck.py:241`.
3. ATA-1 r4 § 7.2.13 : « The host should not access the Command Block Register when BSY=1 » — comportement de
   l'unité **non défini**. ATA-3 r7b, registre d'état : « When the BSY bit is equal to one, a write to a
   command block register by the host shall be ignored by the device » — **documenté** pour ATA-3 :
   l'écriture serait ignorée entière, ni changement d'unité ni fin anticipée, et tête et LBA restent aux
   valeurs par défaut du reset (0). La perte de tête et de LBA de PCem est donc conforme à ATA-3 ; la sélection
   et la fin anticipée ne le sont pas. Un disque de 1993 (ATA-1) : **inconnu**.
4. Si la règle d'ATA-3 est retenue (Q6) : sous BSY de reset, ignorer toute écriture du bloc de commande —
   ~6 lignes, froid ; la durée du reset redevient celle du chronomètre (temps visibles : à peine).
5. Avec ATA-3 : le scénario d'IDECHK en C# seul — l'unité 0 reste choisie, BSY jusqu'à la fin du chronomètre,
   tête 0. Sans décision : à mesurer sur un disque IDE d'époque.
6. **(c)**, (a) si Q6 adopte ATA-3.
7. Aucune.

#### PB-74 — VERIFY ne vérifie qu'un secteur et laisse les registres en place
1. VERIFY (40h, 41h) rend READY au premier rappel, sans décompter ni avancer ; aucune erreur au-delà du disque.
2. C : `ide.c:1000-1012`. C# : `Ide/ide.cs:1108-1124` (marqueur `:1116`). Banc : `idecheck.py:217`.
3. **Documenté** : ATA-1 r4 § 9.19 — « Upon command completion, the Command Block Registers contain the
   cylinder, head, and sector number of the last sector verified. If an error occurs, the verify terminates at
   the sector where the error occurs … The Sector Count Register shall contain the number of sectors not yet
   verified. » ATA-3 § 7.19 : « identical to the READ SECTOR(S) command, except that the DRQ bit is never set ».
   Compte à 0 en fin normale : **déduit**.
4. Une boucle sur le compte (contrôle d'adresse, `ide_next_sector` entre deux secteurs, IDNF hors du disque avec
   le compte restant), puis READY et IRQ — 15 à 25 lignes, froid. Option : un délai par secteur (temps
   visibles ; ATA ne fixe pas la durée).
5. IDECHK en C# seul ou `hw-ide` : VERIFY de 5 secteurs depuis (0, 0, 1) → compte 0, secteur 5 ; VERIFY qui
   franchit la fin → ERR, IDNF, adresse du premier secteur fautif, compte restant.
6. **(a).**
7. La garde de `hdd_file` (PB-125) pour le hors-disque.

### 4.4 Le moteur d'images de CD

#### PB-106 — le mode 2 à 2 336 octets est lu 16 octets trop loin
1. `+24` vaut pour un secteur brut de 2 352 octets, pas pour 2 336 (qui commence au sous-en-tête). Effet :
   image MODE2/2336 jamais reconnue ; piste MODE2/2336 d'une feuille décalée de 16 octets.
2. C : `dosbox/cdrom_image.cpp:180-181`, `:237-238`. C# : `Cdrom/cdrom_image.cs:331-334`, `:412-415`. Outils :
   `tools/isogen/isogen.py:324`, `tools/iXtal26.Diff/CdImageCheck.cs:367-372`.
3. **Documenté** : SFF-8020i Figure 11 — XA Mode 2 Form 1 : synchronisation 12, en-tête 4, sous-en-tête 8,
   données 2 048 ; sans synchronisation ni en-tête, les données sont à +8.
4. `+8` pour 2 336, `+24` pour 2 352 brut — 2 sites, 2 lignes. Tiède (par secteur lu). Lecture seule.
5. `cdimage-check` en C# seul (attendus écrits) sur `iso-2336-mode2.bin` et `formats.cue` : PVD reconnu,
   secteur 16 = 01 « CD001 » 01. Les deux contrôles négatifs de G10.3 (VERIFICATION.md, l. 6252-6253) sont
   exactement cette correction.
6. **(a).**
7. Aucune.

#### PB-107 — une image de plus de 2 Gio prend une longueur tronquée
1. `(int)tellg()`. Effet : de 2 à 4 Gio, rien ne se lit et la capacité est aberrante ; au-delà, vue modulo 4 Gio.
2. C : `cdrom_image.cpp:63-69`, `:217`. C# : `Cdrom/cdrom_image.cs:198-209` (marqueur `:200`).
3. Aucun CD ne dépasse la plage MSF (99:59:74, environ 880 Mo) : une image de plus de 2 Gio est un DVD, qu'un
   lecteur de CD-ROM ne lit pas (« incompatible medium ») — **déduit**.
4. Longueur sur 64 bits, puis refus de l'image au-delà de la plage MSF, avec un avertissement, lecteur vide
   (comme PB-116) — ~6 lignes, froid. C'est une correction d'hôte plus que de fidélité (Q2).
5. C# seul : `creuse-2g5.iso` d'isogen refusée et avertie ; une image de 700 Mo inchangée.
6. **(b).**
7. Aucune.

#### PB-108 — INDEX 00 en 00:00:00 ne compte pas
1. `AddTrack` ne retranche le prégap que si `prestart > 0`. Effet : une feuille à plusieurs fichiers, chacun
   ouvrant sur son prégap, lit chaque piste en avance de son prégap ; à l'écoute, le prégap joue.
2. C : `cdrom_image.cpp:398-403`, `:426-436`. C# : `Cdrom/cdrom_image.cs:597-600`.
3. **Documenté** par le format : CDRWIN User's Guide, annexe A, repris par libodraw § 5.6 — INDEX 00 marque le
   prégap, INDEX 01 le début de la piste, positions relatives au FILE. Le lecteur rend dans la TOC la position
   d'INDEX 01 (SFF-8020i § 10.8.19).
4. `prestart >= 0` — 1 ligne (le contrôle négatif de G10.3), froid. Les débuts de piste changent pour ces
   feuilles : c'est l'objet.
5. `cdimage-check` C# seul, `multi.cue` : la piste 2 (INDEX 01 au secteur 5 de son fichier) rend le secteur 5 ;
   ATAPIAUD en C# seul, le prégap ne joue plus.
6. **(a).**
7. PB-123 (l'audio).

#### PB-109 — un échec de lecture colle au fichier
1. `seekg` n'efface pas `failbit` : après une lecture au-delà de la fin, le fichier ne se lit plus, sur toutes
   ses pistes, jusqu'à sa réouverture ; une image coupée est montée de longueur 0.
2. C : `cdrom_image.cpp:57-61`, `:66-67`. C# : `Cdrom/cdrom_image.cs:13-18` (en-tête), `:191-196` (`read`),
   `:199-209` (`getLength`) — **pas de marqueur de site**.
3. Sans analogue matériel (un état de la libstdc++) ; un vrai lecteur qui échoue sur un secteur lit le suivant
   — **déduit**.
4. Effacer l'état d'échec avant chaque `seekg` — 2 lignes, froid.
5. `cdimage-check` C# seul : `multi.cue`, la piste 3 relue après l'échec ; `tronque-pvd.iso`, longueur réelle.
6. **(a).**
7. Aucune.

#### PB-122 — le lead-out de la TOC n'a ni ADR ni contrôle
1. `attr = 0` sur la piste AAh. Effet : READ TOC rend `00 00 AA 00` en tête du descripteur du lead-out ;
   seule son adresse sert aux pilotes connus.
2. C : `cdrom_image.cpp:223`, `:385` ; `cdrom-image.cc:309`. C# : `Cdrom/cdrom_image.cs:392`, `:580`.
3. SFF-8020i § 10.8.19 : « The ADR field gives the type of information encoded in the Q sub-channel of the
   block where this TOC entry was found » ; Table 131 : le point A2h (début du lead-out) est une entrée Q du
   lead-in, ADR 1, contrôle 4/6 sur un disque de données. ADR = 1 **documenté** ; contrôle = celui de la
   dernière piste **déduit**.
4. `attr` du lead-out = celui de la dernière piste (14h pour une ISO) — 2 lignes, froid.
5. ATAPIBANC en C# seul : `00 14 AA 00`.
6. **(a).**
7. Aucune.

#### PB-112 (C) — des fichiers d'image laissés ouverts
Aucun effet observable (le GC finalise ; `FileShare.ReadWrite | Delete`, sans verrou). C# :
`Cdrom/cdrom-image.cs:575`, `Cdrom/cdrom_image.cs:32`, `:384`. Nettoyage sans effet. **(d).**

### 4.5 Le lecteur de CD ATAPI et l'audio

Note de référence : PCem présente douze modèles — « PCemCD », puis des lecteurs réels de l'Aztech 4x au Kenwood
72x — avec un seul jeu de commandes (`scsi_cd.c:250-460`). Le « vrai lecteur » est donc une norme : SFF-8020i
r2.6 pour la génération des profils (CR-587-B, 1996-1997), MMC-2 pour les commandes qu'elle n'a pas (Q5).

#### PB-117 — READ CAPACITY rend le nombre de blocs plus un
1. 33 pour une image de 32 secteurs (ATAPIBANC) ; un pilote qui lit le dernier bloc annoncé reçoit LBA OUT OF
   RANGE.
2. C : `scsi/scsi_cd.c:1534-1543` ; `cdrom/cdrom-image.cc:475` (`cdrom_capacity` = début du lead-out + 1).
   C# : `Scsi/scsi_cd.cs:1643-1658` (marqueur `:1646`) ; `tools/atapibanc/atapibanc.py:437`.
3. **Documenté** : SFF-8020i, modèle CD-ROM : « The READ CD-ROM CAPACITY command returns the logical block
   address of the last block prior to the lead-out area » ; SCSI-2 § 9.2.7.
4. Rendre `size() − 2` (début du lead-out moins un) — 1 ligne ; `size()` n'a pas d'autre utilisateur. Froid. Le
   contrôle négatif de G10.4 (« sans le + 1 », 32) n'était pas la bonne valeur : c'est 31.
5. ATAPIBANC en C# seul : `0000001Fh`, 2 048.
6. **(a).**
7. Aucune.

#### PB-118 — GET EVENT STATUS NOTIFICATION annonce toujours un nouveau disque
1. `buffer[4]` et `[5]` lus après leur remise à zéro : « nouveau disque » et `atapi->load()` à chaque appel,
   même lecteur vide ; longueur en petit-boutiste ; longueur d'allocation lue sur trois octets.
2. C : `scsi_cd.c:579-606`, `:1274`, `:1336`, `:1606`. C# : `Scsi/scsi_cd.cs:577-610`, `:1370-1450`
   (marqueurs `:1373`, `:1426`) ; `atapibanc.py:451`.
3. SFF-8020i r2.6, Table 37 : 4Ah n'y figure pas — **documenté** pour l'absence ; un lecteur de 1996 rend donc
   05/20h — **déduit**. MMC-2 (97-108r0) § 9.1.2 : la longueur compte « the number of bytes data following the
   data length field » (6), en ordre réseau ; un événement est rapporté une fois, puis 0 — **documenté**.
4. Selon Q5 : (i) refus 05/20h ; ou (ii) MMC-2 : `00 06 04 10`, NewMedia une fois après chargement puis NoChg,
   « média présent » réel, allocation sur deux octets, sans effet de bord — ~25 lignes, froid.
5. ATAPIBANC en C# seul, deux appels : (ii) `00 06 04 10 02 02 00 00` puis `00 06 04 10 00 02 00 00` ; lecteur
   vide, `00 06 04 10 00 00 00 00`.
6. **(b)** : corrigeable, mais la référence n'est pas tranchée.
7. Aucune.

#### PB-119 — MODE SELECT lit son en-tête de travers et ne finit pas à longueur nulle
1. La longueur des données du mode prise pour celle du descripteur ; huit octets d'en-tête même en MODE
   SELECT(6) (la page cherchée 4 octets trop loin, refusée 24h dans ATAPIBANC) ; à longueur 0, phase de données
   sans fin (et PB-115).
2. C : `scsi_cd.c:755-757`, `:1250-1263`. C# : `Scsi/scsi_cd.cs:786`, `:1338-1369` (marqueur `:1342`), `:1707`.
3. **Documenté** : SFF-8020i § 10.8.4 — « A parameter list length of zero indicates that no data shall be
   transferred. This condition shall not be considered as an error » ; Table 58 : en-tête de 8 octets dont la
   longueur est réservée en MODE SELECT ; SCSI-2 Tables 91 et 92 : en-tête(6) de 4 octets (longueur du
   descripteur à l'octet 3), en-tête(10) de 8 (octets 6-7). MODE SELECT(6) n'est pas dans la Table 37 de
   SFF-8020i : son existence sur ATAPI relève de Q5.
4. Longueur d'en-tête selon l'opcode, longueur de descripteur lue à sa place, longueur 0 → GOOD sans phase de
   données — ~12 lignes, froid.
5. ATAPIBANC en C# seul : MODE SELECT(6) de la page 0Eh accepté, relu par MODE SENSE ; MODE SELECT(10) de
   longueur 0 → GOOD immédiat.
6. **(a).**
7. PB-115 (garde R9 de `data_out`) ; Q5.

#### PB-120 — la TOC brute n'a ni lead-out ni longueur
1. READ TOC format 2 : une entrée par piste, ni A0h, A1h, A2h, ni longueur.
2. C : `scsi_cd.c:1026-1028`, `cdrom-image.cc:383-425`. C# : `Scsi/scsi_cd.cs:1083`,
   `Cdrom/cdrom-image.cs:470-517`.
3. **Documenté** : SFF-8020i § 10.8.19, format 10b obligatoire — « the drive will support Q Subcode Point field
   values of A0h, A1h, A2h » ; Table 131 (formats Q du lead-in) ; longueur des données de la TOC renseignée.
4. Points A0 (première piste, type de disque), A1 (dernière piste), A2 (lead-out en MSF), longueur — ~25 lignes,
   froid. Le type de disque (00h, 20h pour XA) : **déduit** de la piste.
5. Un cas ajouté à ATAPIBANC, en C# seul : valeurs calculées de la TOC de `iso-2048.iso` et de `mixte.cue`.
6. **(a).**
7. PB-122 (même contrôle du lead-out).

#### PB-123 — l'audio en LBA confond l'adresse et la position du lecteur
1. En LBA, PLAY AUDIO range l'adresse telle quelle dans une position qui compte les 150 secteurs de l'amorce ;
   SEEK de même ; READ SUB-CHANNEL ajoute 150 au relatif et au LBA absolu. Effet : aucune adresse LBA ne joue la
   bonne piste (`mixte.cue`, piste 2 en LBA 42 : rien ne joue) ; sous-canal faux (00:02:08 pour 00:00:08 ; 207
   et 165 pour 57 et 15).
2. C : `cdrom/cdrom-image.cc:58-75`, `:95-110`, `:138`, `:200-249` ; `dosbox/cdrom_image.cpp:121-122`. C# :
   `Cdrom/cdrom-image.cs:124-169` (marqueur `:160`), `:197-205` (`:202`), `:272-334` ;
   `Cdrom/cdrom_image.cs:267` (`GetAudioSub`) ; `atapibanc.py:376`.
3. **Documenté** : SFF-8020i § 10.8.8 — « PLAY AUDIO commands with a starting LBA address of 0000 0000h shall
   begin the audio play operation at 00m 02s 00f » ; § 10.8.18, Table 115 : adresse absolue, et adresse
   relative au début de la piste.
4. +150 aux positions reçues en LBA (PLAY AUDIO(10)/(12), SEEK) ; sous-canal : LBA absolu et relatif sans +150 —
   ~8 lignes, froid (le rappel audio n'est pas touché). Y joindre PLAY AUDIO MSF (§ 1, point 8) : conversion
   avant le test de piste, refus 05/64h d'une piste de données — ~4 lignes.
5. ATAPIAUD en C# seul : les valeurs justes que donne l'entrée ; `--expect-cd-son` sur la bonne piste.
6. **(a).**
7. PB-108 (positions des pistes d'une feuille).

### 4.6 Le lecteur ZIP

#### PB-126 — la capacité, la fin du disque, l'éjection, le reset et les phases sans fin
1. READ CAPACITY rend 196 608 au lieu de 196 607 ; le secteur 196 608 se lit sans erreur (le tampon d'avant) ;
   START STOP UNIT éjecte sur START = LOEJ = 0 et garde le disque sur LOEJ = 1 ; chaque `resetide` alloue un
   lecteur neuf et perd le disque ; READ(10) de compte 0 et MODE SELECT(6) de longueur 0 ne finissent pas.
2. C : `scsi/scsi_zip.c:607-619`, `:885-891`, `:165-177`, `:993-996`, `:860-870` ; `hdd/hdd_file.c:174-180` ;
   `scsi.c:157-161`. C# : `Scsi/scsi_zip.cs:710` (capacité), `:752`, `:810` (hors disque), `:983` (MODE SELECT
   0), `:1012` (START STOP), `:220-250` (init au reset), `:1113` (lecture de longueur 0) ; `pc.cs:976-981`
   (`zip_load` après le dernier `resetide`) ; `atapibanc.py:401-423`.
3. - Capacité : **documenté** — INF-8070i Table 41 (« The Last Logical Block Address field holds the last valid
     LBA ») ; SCSI-2 § 9.2.7 ; 196 608 secteurs pour un disque de 100 Mo (Brouwer).
   - Hors disque : **documenté** — SCSI-2 § 9.1.2 : « If a command is issued that requests access to a logical
     block not within the capacity of the medium, the command is terminated with CHECK CONDITION » ; 05/21h.
   - Longueur nulle : **documenté** — SCSI-2 § 9.2.6 (READ(10)), § 8.2.8 (MODE SELECT) ; INF-8070i (« this
     condition shall not be considered an error »).
   - Reset : le disque reste ; UNIT ATTENTION 06/29h ensuite (INF-8070i, Table 61) — **déduit**.
   - START STOP UNIT : la norme dit LOEJ 1 / START 0 = éjecter, 0 / 0 = arrêter (INF-8070i Table 59,
     **documenté**). Mais le commentaire de PCem (`scsi_zip.c:886-887`) dit que les pilotes Iomega de
     Windows 9x envoient 0 / 0 pour éjecter : le lecteur Iomega réel éjecte donc probablement sur 0 / 0, et sur
     1 / 0 on ne sait pas — **inconnu**.
4. Capacité − 1 (1 ligne) ; contrôle d'adresse en READ, WRITE, VERIFY → 05/21h (~10 lignes) ; longueurs nulles
   → GOOD (~8 lignes) ; le disque recollé au lecteur neuf après `resetide`, avec UNIT ATTENTION (~8 lignes).
   START STOP laissé reproduit. Froid. Fichiers : l'image ZIP ne reçoit plus d'écriture hors disque (déjà vrai
   par la garde R9 de PB-125).
5. ZIPBANC en C# seul : READ CAPACITY 196 607 ; READ(10) du secteur 196 608 → 05/21h ; READ(10) de compte 0 →
   GOOD ; test C# seul : reset matériel puis TEST UNIT READY → UNIT ATTENTION, puis GOOD, disque présent. Les
   contrôles négatifs de G10.6 (VERIFICATION.md, G10.6) font déjà la moitié du chemin.
6. **(a)**, START STOP UNIT **(c)** — à mesurer sur un ZIP 100 ATAPI réel (éjecte-t-il sur 0 / 0 ? sur 1 / 0 ?).
7. PB-125 (les gardes), et le reliquat de `PLAN-G11.md` (§ 1, point 11).

#### PB-127 (C) — huit `fatal()` que rien n'atteint
`Scsi/scsi_zip.cs:764`, `:769`, `:822`, `:827`, `:887`, `:893`, `:954`, `:960` ; aucun marqueur de site.
Nettoyage sans effet. **(d).**

### 4.7 Le disque SCSI (`scsi_hd`)

Note de référence : le disque de PCem n'est aucun modèle (INQUIRY « PCem ») : son « vrai comportement » est
celui d'un disque SCSI-2 conforme à X3.131-1994.

#### PB-129 — le disque annonce un secteur de trop, et le lit sans erreur
1. READ CAPACITY rend `sectors` ; ce secteur se lit GOOD avec le contenu du dernier secteur lu OU ÉCRIT ; aucun
   LBA n'est vérifié (ni READ, ni WRITE, ni VERIFY).
2. C : `scsi_hd.c:426-437` ; `hdd_file.c:173-180`. C# : `Scsi/scsi_hd.cs:474-485`, READ `:487-598`, WRITE
   `:599-714`, VERIFY `:715-718`.
3. **Documenté** : SCSI-2 § 9.1.2 (adresse du dernier bloc [n − 1], CHECK CONDITION au-delà), § 9.2.7 ; ASC 21h
   LOGICAL BLOCK ADDRESS OUT OF RANGE (§ 8.2.14.3).
4. Capacité − 1 ; contrôle d'adresse dans les cinq commandes → 05/21h — ~15 lignes, froid. La ROM traduit en
   64 × 32 depuis la capacité : la géométrie BIOS ne change que si `sectors` ≡ 2 047 (mod 2 048) — un disque déjà
   partitionné en mode PCem garde sa géométrie, sauf ce cas.
5. `hw-aha` (C# seul, sur le patron de `r9-aha`) : READ CAPACITY ; READ du LBA n → CHECK CONDITION, sense
   05/21h. Dans AHABANC, le cas « le LBA de la capacité » change d'attendu.
6. **(a).**
7. PB-131 (le sense), PB-137 (où il arrive).

#### PB-130 — MODE SENSE : en-tête, descripteur, bourrage, géométrie fixe
1. En-tête `00 00 08 00` (08h dans le paramètre propre au périphérique, longueur de descripteur 0 alors que
   8 octets suivent, mal formés) ; bourrage à min(i0, L) + ⌈(L − i0)/2⌉ octets ; géométrie fixe
   (256 × 4 096 × 64), page 30h « PCEM » ; PC et DBD ignorés ; page inconnue → GOOD.
2. C : `scsi_hd.c:314-412`. C# : `Scsi/scsi_hd.cs:147-154` (`add_data_len`), `:353-472`.
3. **Documenté** : SCSI-2 § 8.3.3, Table 91 (en-tête(6) : longueur, type de support, paramètre propre,
   longueur du descripteur = 8), Table 93 (descripteur : densité, nombre de blocs sur 3 octets, réservé,
   longueur de bloc) ; § 9.3.3 : le paramètre propre d'un accès direct n'a que WP (bit 7) et DPOFUA (bit 4) ;
   § 8.2.10 (DBD, PC) ; page inconnue → 05/24h. La géométrie (pages 03h, 04h) d'un disque virtuel : toute
   géométrie cohérente avec la capacité convient — choix à faire (l'image, ou 64 × 32 comme la ROM).
4. Réécriture de MODE SENSE(6) — 60 à 100 lignes, froid. Le budget R2 de `scsi_hd.cs` (≈ 150 lignes libres sous
   +25 %) ne suffit pas avec PB-131 : fichier propre au mode (Q9).
5. `hw-aha` : en-tête et descripteur exacts ; DBD = 1 → pas de descripteur ; L = 255 → les octets de la page,
   pas un bourrage ; page 05h → 05/24h ; PC = 01 → masque des valeurs modifiables.
6. **(a)** (la géométrie : un choix, pas une inconnue).
7. PB-129 (capacité).

#### PB-131 — REQUEST SENSE, INQUIRY et les refus
1. Sense fixe de 18 octets à longueur additionnelle 0, sans bit Valid, format descripteur sur `cdb[1]` bit 0,
   sense effacé seulement par REQUEST SENSE ; INQUIRY de 96 octets, version 0, longueur additionnelle 0, CmdQue
   annoncé, EVPD ignoré ; tous les refus en 05/25h, code inconnu compris ; FORMAT UNIT, MODE SELECT, VERIFY
   simulés.
2. C : `scsi_hd.c:107-112`, `:148-305`, `:656-708`. C# : `Scsi/scsi_hd.cs:138-145`, `:186-228`, `:229-347`,
   `:715-733`, `:768-772`.
3. **Documenté** : SCSI-2 § 8.2.14, Table 65 (longueur additionnelle n − 7, soit 10 ; « Targets shall
   implement the valid bit ») ; § 7.6 (la condition contingente est effacée par toute commande suivante) ;
   § 8.2.5.1, Tables 45 et 48 (version 2, longueur additionnelle n − 4, CmdQue = 0 sans files étiquetées) ;
   05/20h INVALID COMMAND OPERATION CODE pour un code inconnu, 05/25h pour un LUN (§ 7.5.3) ; le format
   descripteur du sense n'existe pas en SCSI-2 : `cdb[1]` bit 0 y est réservé, et § 7.1.1 admet le refus
   (ILLEGAL REQUEST) comme l'interprétation d'une extension future — celle de SPC-3, que PCem suit : à garder.
   FORMAT UNIT : ce qu'il advient des données sans motif d'initialisation est propre au fabricant —
   **inconnu**.
4. Sense fixe complet, effacé à la commande suivante ; INQUIRY conforme ; 05/20h pour un code inconnu ; EVPD →
   05/24h (ou la page 00h) — ~40 lignes, froid. FORMAT UNIT laissé simulé.
5. `hw-aha` : le scénario d'AHABANC « code inconnu, TEST UNIT READY, REQUEST SENSE » rend 00/00h (et 05/20h si
   REQUEST SENSE suit immédiatement) ; INQUIRY de 36 octets : `00 00 02 02 1F`… (5Ch pour 96 octets) ; CmdQue
   nul.
6. **(a)**, FORMAT UNIT **(c)**.
7. PB-137 (le sense automatique le prend chaque fois), PB-133 (LUN).

#### PB-132 — une phase vide fige le bus, et aucun reset de la carte ne le libère
1. READ(10) de compte 0, REQUEST SENSE, INQUIRY ou MODE SENSE(6) d'allocation 0, MODE SELECT(6) de longueur 0 :
   la cible entre en phase de données et n'en sort plus ; la carte fait la navette NEXT_PHASE ↔ READ_DATA ; même
   blocage si la cible a plus de données que le CCB ; HRST, SRST, BRST ne touchent pas le bus : la cible reste
   BSY, tous les disques disparaissent jusqu'au reset du PC.
2. C : `scsi_hd.c:150`, `:189`, `:312`, `:505` ; `scsi.c:241`, `:731-735` ; `scsi_aha1540.c:276-313`,
   `:1832-1910`. C# : `Scsi/scsi_hd.cs` (mêmes commandes) ; `Scsi/scsi.cs` ; `Scsi/scsi_aha1540.cs:284-319`
   (marqueur `:287`), `:1518-1879` ; outil `tools/iXtal26.Diff/R9Aha.cs:166`.
3. **Documenté** : SCSI-2 §§ 7.2.4, 7.2.6, 8.2.8, 9.2.6 (une longueur nulle n'est pas une erreur, aucun
   transfert) ; AHA-1540C TR, port de contrôle : HRST — « A Reset Condition is generated on the SCSI bus » ;
   SCRST — « causes a SCSI Bus Reset to be generated … raises the RST line … for the designated 25 microsecond
   period » ; SRST — « A Reset Condition is not generated on the SCSI bus », mais « clears all on-going SCSI and
   host adapter commands » ; statut d'hôte 12h « The target attempted to transfer more data than was allocated »
   ; SCSI-2 § 6.2.2 (le reset libère le bus). Le sort exact de la cible après un SRST : **inconnu**.
4. Côté cible : longueur nulle → STATUS directement (~10 lignes). Côté carte : HRST et SCRST appellent
   `scsi_bus_reset` (~4 lignes) ; débordement de la cible → octets jetés ou bourrés, CCB achevé en 12h quand les
   bits de sens sont posés (~20 lignes). Côté disque, après le reset : UNIT ATTENTION 06/29h (~5 lignes).
   Tiède (l'échéance de 10 µs de la carte). **Sondé** : `h_aha_probe` lit le bus et chaque disque — rien de
   nouveau n'y entre.
5. `hw-scsihd` : READ(10) de compte 0 → GOOD immédiat, puis un CCB vers un autre ID réussit ; `hw-aha` : bus
   bloqué à dessein, HRST, puis CCB normal ; READ de 4 secteurs sur un CCB de 1 secteur → 12h.
6. **(a).**
7. PB-142 (états du reset), PB-141 (mailbox après reset), PB-125/PB-128 (tampons).

#### PB-133 — le LUN du CCB n'atteint jamais le disque
1. Ni MESSAGE OUT ni IDENTIFY : le disque ne connaît le LUN que par `cdb[1]`. Effet : un pilote qui ne met le
   LUN que dans le CCB (l'usage SCSI-2) voit le disque répondre sur ses huit LUN — un balayage ASPI y trouverait
   huit disques.
2. C : `scsi_aha1540.c:422`, `:440`, `:1603` ; `scsi_hd.c:131`. C# : `Scsi/scsi_aha1540.cs:411-416` (marqueur
   `:415`) ; `Scsi/scsi_hd.cs:165-172`, `:229-236`.
3. **Documenté** : AHA-1540C TR, CCB octet 1 — « If the target accepts an Identify message out, the value in
   bits 2, 1, and 0 is provided in the LUN field of the message byte. The LUN field in the SCSI Command
   Descriptor Block (CDB) is expected to be zero » ; SCSI-2 § 7.2.2 — « The target shall ignore the logical unit
   number specified within the command descriptor block if an IDENTIFY message was received » ; § 8.2.5.1 :
   LUN non supporté → INQUIRY rend 7Fh ; § 7.5.3 : les autres commandes → 05/25h.
4. Sans modéliser la phase MESSAGE OUT : le bus porte, en mode matériel, le LUN de la sélection, que `scsi_hd`
   prend à la place de `cdb[1]` — ~20 lignes (bus, carte, disque), froid. Le nouveau champ reste HORS de
   `h_aha_probe` (sinon l'ABI et le compte de champs changent).
5. `hw-aha` : CCB vers le LUN 1, CDB à LUN 0 → INQUIRY 7Fh ; TEST UNIT READY → CHECK CONDITION 05/25h.
6. **(a).**
7. PB-131 (INQUIRY sur LUN absent : 60h aujourd'hui, 7Fh attendu).

#### PB-121 (C) — le lecteur de l'amorçage précédent n'est jamais fermé
1. `scsi_bus_close` vide ses tableaux avant la boucle qui devait fermer. Pour un CD : 512 Ko perdus par
   amorçage. Pour un disque SCSI (élargi en G11) : après un reset matériel, le flux d'avant reste ouvert avec son
   tampon ; le nouveau relit le dernier secteur écrit tel qu'il était sur le disque ; à la sortie, `closepc` vide
   du plus récent au plus ancien, et l'ancien contenu réécrit l'image (§ 1, point 2).
2. C : `ide.c:282-284`, `scsi.c:329-335`, `scsi_cd.c:621`. C# : `Scsi/scsi.cs:532` (marqueur),
   `Disc/hdd_file.cs:66-103`, `pc.cs:1341-1345`, `Scsi/scsi_hd.cs:96`.
3. Un disque est un disque : après un reset, il relit ce qu'on y a écrit — **déduit**.
4. En mode matériel, `scsi_bus_close` vide et ferme les flux des disques avant la réouverture — ~6 lignes, froid.
   **Fichiers de l'utilisateur** : c'est l'image SCSI qu'on protège. Le registre de flux et son ordre restent
   indispensables au mode PCem (sans eux, `bd-ami486-aha-format` rougit, VERIFICATION.md § G11.0). Q2 :
   corriger aussi en mode PCem, comme R9 protège l'hôte ?
5. Test C# seul : écrire un secteur par un CCB, reset matériel, relire (contenu neuf attendu), sortir, relire
   l'image (contenu neuf attendu). Contrôle négatif : le mode PCem rend l'ancien contenu.
6. **(a)** — section C, mais avec un effet.
7. Aucune.

#### PB-134 (C) — vingt-deux `fatal()` que rien n'atteint
`Scsi/scsi_hd.cs:525`, `:530`, `:579`, `:584`, `:635`, `:641`, `:692`, `:698` et quatorze sites de
`Scsi/scsi_aha1540.cs` ; en-têtes seulement. Nettoyage sans effet. **(d).**

### 4.8 L'Adaptec AHA-1542C

Note de référence : l'*AHA-1540C Series Technical Reference Manual* (1993) décrit l'interface de la carte
émulée ; la commande 03h et la 82h y sont « reserved for use by the Adaptec host adapter BIOS » : leur
comportement hors de l'usage du BIOS n'est pas documenté. Le fichier `nvr/.aha1542c.nvr` est partagé par toutes
les configurations (décision n° 10 de G11) : une EEPROM écrite en mode matériel est relue en mode PCem.

#### PB-136 — PROGRAM EEPROM (22h) lit au-delà de ses paramètres
1. `params[c + 3]` pour `c < params[1]` (jusqu'à 255) : au-delà de `params[64]`, l'état interne ; seuls
   32 octets persistés ; l'IRQ peut changer avec une interruption pendante, l'ancienne ligne reste levée.
2. C : `scsi_aha1540.c:1166-1168`, `:2114`, `:261-266`. C# : `Scsi/scsi_aha1540.cs:256-267` (`param_lu`),
   `:1110-1125` (marqueur `:1114`).
3. **Documenté** : AHA-1540C TR, *Set EEPROM (22h)* — octet 0 réservé (zéro), octet 1 nombre d'octets, octet 2
   décalage, « One to 32 bytes of data to be written to the EEPROM ». Au-delà de 32, ou décalage + nombre > 32 :
   **inconnu** (refus INVDCMD ? troncature ?). L'IRQ : la carte met à jour « the configuration registers » en
   même temps que l'EEPROM — l'ancienne ligne est relâchée — **déduit**.
4. Dans les bornes : copier exactement les octets reçus (~6 lignes), relâcher l'ancienne IRQ (~3 lignes). Hors
   bornes : laissé reproduit. Froid. **Fichiers** : `nvr/.aha1542c.nvr`.
5. `hw-aha` : 22h de 4 octets au décalage 8, relus par 23h ; l'IRQ changée avec une interruption pendante →
   l'ancienne ligne retombe. Le cas d'AHABANC (224 octets depuis 32) : à mesurer.
6. **(b)**, hors bornes **(c)**.
7. PB-144 (l'IRQ de code 7).

#### PB-137 — le sense automatique : inconditionnel, inversé, et écrit au hasard
1. Un REQUEST SENSE suit CHAQUE commande, même GOOD (octet de contrôle 14) ; un CCB de mailbox l'envoie dans
   `int_buffer` et jamais dans le CCB ; une commande 03h l'envoie à `ccb.addr + 12h + longueur`, `ccb.addr`
   périmé — 0 à la mise sous tension, la table des vecteurs.
2. C : `scsi_aha1540.c:1535-1622`. C# : `Scsi/scsi_aha1540.cs:1434-1460` (marqueur `:1436`).
3. **Documenté** : AHA-1540C TR, CCB octet 3 — le sense automatique répond à « Check Condition status » ;
   « A value of 00h indicates that an allocation length of 14 bytes is to be used … A value of 01h requests that
   no automatic Request Sense be executed … Values from 08h to FFh are valid allocation lengths … reserved at
   the end of the CCB » (octets 18 + m à 18 + m + n) ; la CDB générée prend cette longueur. L'octet de contrôle
   à 0 : **déduit**. Pour 03h : **inconnu** (interface privée) ; l'intention lisible est l'inverse de ce que fait
   PCem (le BIOS lit `int_buffer`) — **déduit**.
4. Sense seulement après CHECK CONDITION (statut 02h) ; dans le CCB pour la mailbox ; dans `int_buffer` pour 03h ;
   contrôle 0 — ~12 lignes, froid. Plus d'écriture dans la table des vecteurs.
5. `hw-aha` : CCB qui finit GOOD → aucun REQUEST SENSE émis (compteur de commandes du disque) ; CCB en CHECK
   CONDITION, octet 3 = 12h → 18 octets de sense en CCB + 18 + m ; octet 3 = 00h → 14 octets ; octet 3 = 01h →
   aucun sense.
6. **(a)** pour la mailbox ; **(b)** pour 03h.
7. PB-138 (`from_mailbox`), PB-131 (le contenu du sense).

#### PB-138 — les commandes BIOS 03h achèvent un CCB périmé, et réussissent à faux
1. Les sous-fonctions 02h, 03h, 04h, 08h ne posent ni `from_mailbox` ni `current_mbo` : après un CCB de mailbox,
   leur achèvement écrit dans l'ANCIEN CCB, libère son MBO, poste un MBI ; une cible absente laisse le statut
   périmé (succès à faux).
2. C : `scsi_aha1540.c:765-1013`, `:1488-1526`. C# : `Scsi/scsi_aha1540.cs:681-995` (marqueur `:682`).
3. Interface privée (« reserved for use by the Adaptec host adapter BIOS ») : **inconnu** pour les codes. Qu'une
   commande du BIOS n'achève pas un CCB de la mailbox, et qu'une cible absente ne réussisse pas : **déduit** (les
   codes que le BIOS en tire, chapitre 6 du TR : « 80h Time-out. Host adapter or device not responding to BIOS »).
4. `from_mailbox = 0` et pas de MBO pour 03h ; une sélection ratée pose un statut d'échec (expiration) —
   ~8 lignes, froid.
5. `hw-aha` : CCB de mailbox, puis 03h/08h → l'ancien CCB intact, aucun MBI ; 02h sur l'ID 5 vide → échec.
   Attendus déduits : vérification faible.
6. **(b).**
7. PB-137, PB-141.

#### PB-139 — ABORT efface le mauvais emplacement, et n'interrompt rien
1. En mailbox de 4 octets, l'action est effacée en `mba + c * 8` ; ABORT poste « aborted » quel que soit l'état du
   CCB ; l'emplacement reste ABORT et chaque 02h en reposte un.
2. C : `scsi_aha1540.c:1406-1428` (`:1411`). C# : `Scsi/scsi_aha1540.cs:1255-1275` (marqueur `:1262`).
3. **Documenté** : AHA-1540C TR, MBO — « After the MBO has been examined … the host adapter sets the MBO command
   byte back to zero » ; MBI — 02h « CCB aborted by host », 03h « Aborted CCB not found … It is likely that the
   CCB was already presented to the host before the Abort CCB MBO entry was completed ».
4. `c * 4` ; 02h si le CCB est le courant (interrompu : bus remis au repos), 03h sinon — ~15 lignes, froid.
5. `hw-aha` : ABORT d'un CCB déjà achevé → MBO effacé à son emplacement, MBI 03h, une seule fois ; ABORT d'un CCB
   en cours (READ long) → 02h.
6. **(a)** (l'interruption d'un CCB en vol dépend du temps : son attendu est le plus fragile).
7. PB-141 (le balayage), PB-132 (le bus).

#### PB-140 — RETURN SETUP DATA est tronqué à 20 octets
1. `result_len = MIN(params[0], 20)`, la suite à zéro : la somme, l'adresse de la mailbox du BIOS ne sortent jamais.
2. C : `scsi_aha1540.c:1130-1140`. C# : `Scsi/scsi_aha1540.cs:1061-1094` (marqueur `:1090`).
3. **Documenté** : AHA-1540C TR, *Return Setup Data (0Dh)* — octets 00h à 2Bh définis (dont 25h, 26h les
   interrupteurs, 27h-28h la somme du micrologiciel, 29h-2Bh l'adresse de la mailbox du BIOS), 2Ch-FFh à zéro ;
   « A value of zero is accepted and 256 bytes are returned ».
4. Longueur demandée (0 → 256), contenu jusqu'à 2Bh — ~4 lignes ; l'octet 26h (interrupteurs) : rendre ceux de
   la carte (PCem rend 0) — 1 ligne. Froid.
5. `hw-aha` : 0Dh de 44 octets → les 44 ; 0Dh de 0 → 256 octets.
6. **(a).**
7. Aucune.

#### PB-141 — les mailbox : le compte des 02h, le balayage, le MBI non vérifié
1. `mbo_req` compte les 02h, chaque balayage repart de l'emplacement 0, ni HRST ni SRST ni 01h ne le remettent à
   zéro ; le balayage de la mailbox du BIOS n'est gardé par rien ; le MBI est écrit sans vérifier qu'il est
   libre ; MAILBOX INIT accepte un compte nul.
2. C : `scsi_aha1540.c:717-732`, `:1376-1455`, `:1500-1503`, `:1565-1571`, `:1654-1659`. C# :
   `Scsi/scsi_aha1540.cs:664-680`, `:1227-1310` (marqueur `:1278`), écritures du MBI (`:503-506` et les
   achèvements).
3. **Documenté** : AHA-1540C TR — 01h : « If the Mailbox Count is zero, the INVDCMD and HACC bits are set » ;
   02h : « begin to scan for active MBO entries. Once scanning has been started, it continues until all MBO
   entries have been serviced » ; MBO : « The host adapter is always searching for new MBO entries in a
   round-robin order, beginning with the entry after the last MBO entry that was processed » ; MBI : « the host
   adapter scans the first byte of an MBI entry to find a free mailbox » ; HRST et SRST imposent une nouvelle
   initialisation (INIT). La mailbox du BIOS (25h) face à la mailbox normale : **inconnu**.
4. 01h à compte nul → INVDCMD ; 02h arme un balayage complet (plus de compte) ; tourniquet depuis l'emplacement
   suivant ; MBI libre exigé, sinon l'achèvement attend l'échéance suivante ; resets → état de mailbox à zéro —
   30 à 50 lignes. Tiède (le balayage tourne à l'échéance de la carte).
5. `hw-aha` : 01h compte 0 → INVDCMD ; deux CCB postés, un seul 02h → les deux partent ; MBI occupé → pas
   d'écrasement ; HRST puis CCB sans 01h → INVDCMD au 02h.
6. **(a)**, la mailbox du BIOS **(b)**.
7. PB-142 (états), PB-139.

#### PB-142 — la machine des commandes est réentrante, et part de zéro
1. `process_cmd` est appelé dans l'OUT, dans tous les états : en RESET l'octet est perdu et l'autotest finit sur
   le champ ; en CMD_IN_PROGRESS l'octet devient une nouvelle commande ; en SEND_RESULT l'OUT pousse lui-même
   l'octet suivant. `status` vaut 0 à la mise sous tension.
2. C : `scsi_aha1540.c:316-326`, `:2133`. C# : `Scsi/scsi_aha1540.cs:320-331` (marqueur `:328`), `:1955-1998`.
3. Mise sous tension : **documenté** — AHA-1540C TR, *Hard Reset Operations* : « Resets may be generated by
   powering down … Regardless of the source … STST … After the Hard Reset process is complete … raising the
   initialization bit » (et IDLE, *Control Port*, HRST). Les octets écrits hors protocole : le manuel de la
   1540A (§ 4.2.2) prévient que des octets en trop « are likely to be interpreted as invalid, although they may
   instead cause the execution of valid commands » — **inconnu** au détail, état par état.
4. Mise sous tension : STST le temps de l'autotest, puis INIT | IDLE — ~4 lignes, froid. La réentrance :
   laissée reproduite.
5. `hw-aha` : lecture du port d'état juste après l'init → STST, puis INIT et IDLE. La réentrance : à mesurer
   sur une 1542C (écrire pendant l'autotest, pendant l'envoi d'un résultat).
6. **(a)** pour la mise sous tension, **(c)** pour la réentrance.
7. PB-141.

#### PB-143 — une CDB courte est complétée de zéros
1. Une CDB plus courte que son groupe : la carte envoie un zéro par échéance tant que la cible reste en phase de
   commande ; longueur 0 → TEST UNIT READY ; un READ(10) déclaré sur 6 octets s'exécute tronqué.
2. C : `scsi_aha1540.c:1799-1804`. C# : `Scsi/scsi_aha1540.cs:1628`.
3. **Inconnu**. Candidats : « 14h Target Bus Phase Sequence Failure … The host adapter generates a SCSI reset
   condition » (une phase de commande au-delà de la CDB), ou « 1Ah Invalid CCB … A CCB parameter was invalid »
   (longueur 0) — **déduits**, non affirmés par le TR pour ce cas.
4. Laissé reproduit.
5. À mesurer sur une 1542C : CCB de READ(10) déclaré sur 6 et 9 octets, puis sur 0.
6. **(c).**
7. PB-132 (le reset du bus).

#### PB-144 — la 1542C de PCem : traduction, adresses et EEPROM
1. 03h traduit CHS en LBA sans retrancher 1 au secteur ; les adresses ne bouclent pas à 24 bits ; sans EEPROM :
   ID 0, DMA 0, IRQ 9 ; une IRQ de code 7 vaut 16 et `picint((uint16_t)(1 << 16))` ne lève rien ; l'ID de l'hôte
   n'est pas exclu de la sélection ; délai de sélection, temps de bus et vitesse sans effet ; le sens du
   transfert ignoré.
2. C : `scsi_aha1540.c:773`, `:815`, `:857`, `:425-433`, `:1857`, `:1935`, `:2101-2104`, `:2155-2157`,
   `:261-266`, `:1703-1711`, `:1052-1074`, `:417`. C# : `Scsi/scsi_aha1540.cs:269-274`, `:683`, `:1012-1036`,
   `:1923-1938`, sélection `:1518-…`.
3. - Sens du transfert : PCem est **conforme** (§ 1, point 9) ; le contrôle de longueur des bits 3 et 4 (12h) et
     « both bits set → no data transfer » manquent — **documenté** (CCB octet 1).
   - Défauts d'usine : ID 7, DMA 5, IRQ 11, port 330h, BIOS DC000h — **documenté** (tableau des réglages
     d'usine du TR). iXtal livre l'EEPROM de référence de PCem (`nvr/default/aha1542c.nvr` : ID 7, DMA 7,
     IRQ 10) : le cas « sans EEPROM » ne survient que sans ce fichier.
   - CHS : secteurs numérotés depuis 1 — **déduit** ; la ROM v1.01 ne s'en sert pas (elle passe par 82h).
   - 24 bits : le bus ISA n'a que 24 lignes d'adresse — **déduit** ; sans effet sous 16 Mo.
   - L'ID de l'hôte : on ne se sélectionne pas soi-même → expiration (11h) — **déduit**.
   - IRQ de code 7 : **inconnu** (SCSISelect ne propose que 9, 10, 11, 12, 14, 15).
   - Délai de sélection, temps de bus, vitesse : n'agissent que sur le temps.
4. Contrôle de longueur (12h) ~10 lignes ; défauts d'usine en mode matériel (Q8) ~4 lignes ; −1 du CHS 1 ligne ;
   bouclage à 24 bits ~4 lignes ; ID de l'hôte → 11h ~3 lignes. Les temps : hors G13 (Q11). IRQ 7 : laissée.
   Froid. **Fichiers** : les défauts d'usine ne toucheraient `nvr/` qu'à la première écriture.
5. `hw-aha` : CCB READ de 4 secteurs, longueur de 512, bit 3 posé → 12h ; sans EEPROM ni référence → ID 7, DMA 5,
   IRQ 11 ; 03h/02h au secteur 1 → LBA 0. Le reste : faible.
6. **(b)** (mixte : a, b, c et d selon le point).
7. PB-136 (IRQ), PB-132 (débordement).

---

## 5. Proposition d'étapes

Les étapes du domaine viennent après le socle commun de G13 (l'interrupteur, ses gardes, la garde de
`boot-diff` qui refuse le mode matériel, le patron des tests C# seuls) et dans l'ordre de `PLAN.md` (l'UC et le
x87 d'abord). Chaque étape change l'émulateur : **série entière** côté PCem à chaque fois (elle doit rester
identique, journal par journal, à la précédente), plus ses tests `hw-*` en C# seul, ajoutés à
`tools/gates/series.sh` (quelques secondes chacun), un commit par étape après sa série verte. Chaque test `hw-*`
porte, à côté de chaque attendu, la phrase de la norme qui le fonde, et son contrôle négatif : le comportement de
PCem doit le rougir.

| Étape | Contenu | Vérification | Taille (C#) |
|---|---|---|---|
| **ST.0 — Ce qui n'attend pas le mode** | PB-17 sous R9 (Q3) ; les marqueurs manquants (PB-25 dans `mfm_at.cs`, PB-109, PB-127, PB-134, PB-129 à 131) ; l'entrée PB-30 corrigée ; PB-18 « sans objet » ; inscrire (ou non) le défaut voisin d'`ide_irq_update` | test C# seul de l'image XDF ED ; série entière (PB-17 change l'émulateur), sinon sous-ensemble ciblé | ~5 + test ~60 |
| **ST.1 — Disquette et MFM** | PB-14, PB-22, PB-23, PB-25 (deux cartes) ; PB-19 si retenu ; R9 (a) du Xebec joint (Q4 : PB-26, PB-27) | `hw-fdc`, `hw-mfm` ; témoin inter-modes : `FORMAT C: /S` du Xebec en C# seul dans les deux modes, images identiques (PC DOS 2.00 laisse `sector` à 0) | ~25 (+ R9 (a) ~40) ; tests ~300 |
| **ST.2 — IDE** | PB-71, PB-74 ; PB-72 si Q6 ; le défaut voisin s'il est inscrit | `hw-ide` (le scénario d'IDECHK conduit en C#) ; témoin inter-modes : `bd-ami486-ide-ecriture` en C# seul dans les deux modes, images identiques | ~30 ; tests ~250 |
| **ST.3 — Moteur d'images CD** | PB-106, PB-108, PB-109, PB-122 ; PB-107 selon Q2 | `cdimage-check` en mode attendu (C# seul) sur les images d'isogen | ~15 ; tests ~150 |
| **ST.4 — Lecteur ATAPI et audio** | PB-117, PB-119, PB-120, PB-123 (+ PLAY AUDIO MSF) ; PB-118 selon Q5 | `hw-atapi` (sur le patron de `r9-atapi`) ; ATAPIBANC et ATAPIAUD lancés en C# seul, relevés attendus écrits depuis SFF-8020i | ~70 ; tests ~350 |
| **ST.5 — ZIP** | PB-126 hors START STOP UNIT | `hw-zip` ; ZIPBANC en C# seul | ~30 ; tests ~150 |
| **ST.6 — Disque SCSI** | PB-129, PB-130, PB-131 (hors FORMAT), PB-132 côté cible, PB-121 | `hw-scsihd` ; témoins inter-modes : `aha-format`, `aha-boot` en C# seul dans les deux modes (la ROM passe par READ CAPACITY et le sense automatique, mais sur des disques de 40 960 secteurs, multiple de 2 048, et sans erreur : images identiques attendues ; la trace, elle, différera) | ~170 (fichier propre au mode, Q9) ; tests ~300 |
| **ST.7 — AHA-1542C** | PB-132 côté carte, PB-133, PB-137, PB-139, PB-140, PB-141, PB-142 (mise sous tension), PB-144 (longueur, défauts selon Q8, CHS, 24 bits, ID de l'hôte), PB-136 et PB-138 dans leurs bornes | `hw-aha` (sur le patron de `r9-aha`) ; AHABANC en C# seul, attendus du TR | ~200 ; tests ~450 |
| **ST.8 — Section C et registre** | PB-15, PB-20 (nettoyage) ; PB-34 selon Q2 ; `PCEM_BUGS.md` : une ligne « *Corrigé en mode matériel* » par entrée, la liste « à mesurer » ; `VERIFICATION.md` | série entière si PB-15 et PB-20 sont retirés (le code de l'émulateur change, même mort) ; sinon sous-ensemble ciblé | ~10 |

Restent reproduits en mode matériel, faute de vérité : PB-28, PB-72 (sauf Q6), PB-143, START STOP UNIT du ZIP,
FORMAT UNIT du disque SCSI, PB-136 hors bornes, la réentrance de PB-142, l'IRQ de code 7 de PB-144.

**L'ordre** : ST.0 d'abord (un plantage de l'hôte sur une donnée de l'utilisateur n'attend pas G13) ; puis du
plus simple au plus gros, chacun isolable : ST.1 et ST.2 (quelques lignes, documentation sûre), ST.3 et ST.4 (le
CD, une seule norme), ST.5, puis ST.6 avant ST.7 (la carte s'appuie sur le comportement du disque : sense,
longueurs nulles, LUN).

---

## 6. Les risques

1. **Le mode PCem doit rester identique.** Chaque garde ajoute une branche dans des fichiers que l'oracle
   compare instruction par instruction (`boot-diff`), par sondes (`h_disc_probe`, `h_aha_probe`, l'état du
   moteur CD de `cdimage-check`) et par images de disque comparées. La série entière, par étape, en est la seule
   preuve.
2. **Les sondes et l'ABI.** Un champ propre au mode matériel (le LUN de la sélection, PB-133 ; l'état de
   l'événement GESN, PB-118 ; le disque gardé du ZIP, PB-126) ne doit entrer dans aucune sonde : `h_aha_probe`
   compte ses champs (`H_AHA_PROBE_N`) des deux côtés.
3. **Les fichiers partagés entre les modes.** `nvr/.aha1542c.nvr` est commun à toutes les configurations
   (décision n° 10 de G11) : une EEPROM écrite en mode matériel (PB-136, PB-144) sera relue en mode PCem. Les
   images de disque, de ZIP passent d'un mode à l'autre : un disque formaté en mode matériel (PB-22, PB-25)
   n'aura pas le contenu que le mode PCem aurait produit. Rien de faux, mais des comparaisons inter-modes à
   faire sur des copies.
4. **Le mode PCem corrompt déjà une image de l'utilisateur** (PB-121 : reset matériel avec un disque SCSI) et
   **l'hôte tombe** sur une image XDF ED (PB-17) : deux défauts que la règle « on reproduit » garde en mode
   PCem, sauf décision (Q2, Q3).
5. **Les références.** Le disque SCSI de PCem n'est aucun modèle ; les douze modèles de lecteur de CD partagent
   un jeu de commandes ; le ZIP est un « IOMEGA ZIP 100 ATAPI » sans documentation d'interface trouvée. Le « vrai
   comportement » y est une norme choisie (Q1), qu'il faut écrire au plan.
6. **Huit points inconnus**, laissés reproduits : PB-28, PB-72, PB-143, START STOP UNIT du ZIP, FORMAT UNIT,
   PB-136 hors bornes, la réentrance de PB-142, l'IRQ de code 7. Les mesurer suppose un DTC 5150X, un disque IDE
   de 1993-1994, un ZIP 100 ATAPI, une AHA-1542C.
7. **Le temps n'est pas corrigé.** Le mode matériel corrige des résultats ; les durées (VERIFY de N secteurs,
   délai de sélection, temps de bus) restent celles de PCem, sauf PB-14.
8. **Les budgets des règles.** R2 (+25 %) : `scsi_hd.cs` n'a que ~150 lignes libres pour ~170 à ajouter ;
   `Cdrom/cdrom_image.cs` est déjà à 1,65 (`deviated`). R1(d) ne connaît pas le nouveau marqueur. Les fichiers
   propres au mode (Q9) règlent les deux.
9. **La vérification sans oracle.** Les attendus sont écrits à la main depuis les normes : un contresens de
   lecture passerait. Parade : la phrase de la norme à côté de chaque attendu, le contrôle négatif (PCem rougit
   chaque test), et les témoins inter-modes sur les chemins que les corrections ne touchent pas.
10. **Les gardes R9 ne sont pas le matériel.** En mode matériel, un READ de plus de 512 secteurs (PB-125,
    PB-128) rend encore des zéros au-delà des tampons : le vrai lecteur transfère tout. Faire mieux suppose de
    servir les transferts par morceaux — hors de cette liste, à décider (Q12).

---

## 7. Les questions de principe

- **Q1 — La référence du « vrai matériel », appareil par appareil.** Proposition : ATA-1 (X3T9.2 791D r4) pour
  l'IDE, avec ATA-3 là où ATA-1 se tait (Q6) ; l'*IBM Fixed Disk Adapter* pour le Xebec ; l'*IBM PC AT Technical
  Reference* et le µPD765 pour la disquette ; SFF-8020i r2.6 pour le lecteur de CD (et MMC-2 pour ce qu'elle n'a
  pas, Q5) ; INF-8070i pour le ZIP ; SCSI-2 (X3.131-1994) pour le disque SCSI ; l'*AHA-1540C Technical
  Reference* (1993) pour la carte.
- **Q2 — Les corrections d'hôte sans analogue matériel** (PB-16 : refus d'image ; PB-107 : image de plus de
  2 Gio ; PB-109 : l'échec collant ; PB-121 : le tampon orphelin ; PB-34 : la table des types) : en mode
  matériel seulement, ou dans les deux modes, sur le modèle de R9, quand aucune porte ne compare ces chemins ?
  PB-121 protège une image de l'utilisateur.
- **Q3 — PB-17** : le traiter tout de suite sous R9, dans les deux modes (un tampon de 32 Ko), hors de G13 ?
- **Q4 — R9 (a) du Xebec** (les dix-sept `fatal()` de protocole, `PLAN.md` « Hors plan ») : le joindre à
  l'étape MFM, puisque la documentation IBM qui le débloque est la même ?
- **Q5 — Les commandes que la norme d'époque n'a pas** (GET EVENT STATUS NOTIFICATION, MODE SELECT/SENSE(6) sur
  ATAPI) : les refuser comme un lecteur SFF-8020i de 1996, ou les corriger selon MMC-2 comme un lecteur de 1998 ?
  Les modèles de PCem vont d'un 4x (vers 1995) à un 72x (vers 1999), sous un seul jeu de commandes.
- **Q6 — PB-72** : adopter la règle d'ATA-3 (« une écriture du bloc de commande sous BSY est ignorée ») comme
  comportement matériel, ou attendre une mesure ?
- **Q7 — Les inconnus** : d'accord pour les laisser reproduits en mode matériel, marqueur inchangé, et les
  inscrire au registre comme « à mesurer » ? Y a-t-il du matériel réel disponible (DTC 5150X, disque IDE
  d'époque, ZIP 100 ATAPI, AHA-1542C) ?
- **Q8 — L'AHA en mode matériel** : les réglages d'usine du TR (ID 7, DMA 5, IRQ 11) à la place de l'EEPROM de
  référence de PCem (ID 7, DMA 7, IRQ 10) quand aucune EEPROM n'est écrite ? Et un fichier `nvr` distinct par
  mode, ou partagé ?
- **Q9 — Où vit le code du mode matériel** dans les fichiers transcrits : en ligne, sous garde (R2 menacé dans
  `scsi_hd.cs`), ou dans des fichiers propres `*.Materiel.cs` hors R2, comme le propose la lecture du mécanisme
  (D7) ? R1(d) est à amender pour le marqueur `// pcem bug, fixed in hardware mode: PB-nn`.
- **Q10 — La granularité** : un interrupteur pour l'utilisateur et une garde par PB (D7) conviennent au
  stockage ; le mode se fige-t-il au démarrage (une bascule à chaud laisserait des machines d'états
  incohérentes) ?
- **Q11 — Les durées** (VERIFY, délais de sélection, temps de bus de l'AHA) : hors de G13 ?
- **Q12 — Les R9 « non reproduits » du domaine** (PB-113 à 116, 125, 128, 135) : leur repli sûr reste-t-il le
  comportement du mode matériel, ou celui-ci doit-il faire mieux (transferts complets au-delà de 256 Ko et de
  512 Ko ; les commandes 0Ch, 1Ch, 1Dh, 20h, 21h que le TR documente, au lieu d'INVDCMD) ? Et le reliquat de
  `PLAN-G11.md` (les tampons du ZIP au régime de `scsi_hd`, fidélité du mode PCem) entre-t-il dans G13 ?
