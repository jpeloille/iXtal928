# La carte fille IDE des 386 et 486 — le plan, sur recherche documentaire

> Écrit le 20 octobre 2026. Demande de Julien, le 20/10 : « émuler une vraie carte IDE pour mes 386 et 486 », une carte
> répandue en 1992-1994, « elle doit pouvoir avoir 2 floppy et 2 hdd » ; puis : « regarde ce que tu trouves comme doc
> technique sur les 3 cartes proposées », bitsavers.org en tête. Le but : « coller à un vrai PC de cette époque ». Et la
> contrainte : « mon émulation devra avoir deux modes : la carte réellement émulée ou la version sans cette carte
> lorsque cette dernière ne fait pas sens ou remet en cause le fonctionnement d'un PC qui était mieux émulé sans ».
> Trois recherches sur le web, une par carte, en lecture seule : rien n'est téléchargé, rien dans l'émulateur ne change.
> Ce qui suit n'a été lu qu'en ligne ; l'étape suivante le relit sur pièces.

## Les trois candidates

Les 386 et 486 clonés de 1992-1994 prenaient presque tous une carte « multi-I/O » sans marque : l'IDE, les disquettes,
deux COM, un LPT, la manette. Elle se reconnaît à sa puce. Trois candidates, toutes à 2 lecteurs de disquettes et
2 disques (un canal IDE, maître et esclave) :

| Carte | La doc primaire de la puce | Les cavaliers | Verdict |
|---|---|---|---|
| **Multi-I/O ISA 16 bits à SMC FDC37C652** | **Complète** : la fiche SMC FDC37C651/652, Rev. 9/9/92, 90 pages, lue | Dans la fiche : le 652 lit ses cavaliers au RESET (p. 72-79) ; aucun manuel d'une carte ISA 16 bits à 652 et IDE trouvé | **La seule documentée de bout en bout** |
| **Goldstar Prime 2 / Prime 2C** | **Aucune.** « Prime 2 » et « Prime 2C » sont les puces elles-mêmes, des Super I/O Goldstar, sans fiche ; le GM82C765B, une autre puce, n'est sur aucune de ces cartes | TH99 (20334, 21437), le wiki de Vogons, Viric — qui se contredisent en partie | Les registres des disquettes ne sont écrits nulle part |
| **DTC 2278D/E** (VLB) | **Aucune pour l'IDE** : la puce est un Atronics IDE-2015PL (marqué « DTC2278D+ »), sans fiche ; la Super I/O, une Winbond W83757 (2278D) ou W83787F (2278E) | TH99 pour COM, LPT et IRQ seulement ; rien sur l'IDE | Les registres IDE ne se lisent que dans le pilote Linux `dtc2278.c` (0B0h/0B4h, C3h, A0h, 20h, sans explication) |

Mes premières hypothèses, corrigées par la recherche : la Prime 2C ne porte pas de GM82C765B ; la puce IDE de la DTC
n'est pas une Winbond ; le pilote Linux a quitté `drivers/ide/legacy/` pour `drivers/ide/` vers la 2.6.28, avant que
`drivers/ide` ne sorte du noyau (vers la 5.14, à vérifier).

## Ce que dit la fiche du FDC37C651/652

- **Les disquettes** : un cœur CMOS 765B, pas un 82077 — ni FIFO, ni CCR à part. DOR en 3F2h (écriture), MSR en 3F4h,
  les données en 3F5h, DSR en 3F7h en écriture (500, 300, 250 kbit/s ; 1 Mbit/s avec un quartz de 48 MHz et CR3.2,
  tableau 6), DIR en 3F7h en lecture.
- **3F7h en lecture, partagé** : la puce ne mène que DB7, le changement de disquette ; DB6-DB0 restent en haute
  impédance, le disque IDE les donne. La broche IDED7 tamponne le bit 7 entre le câble IDE et l'hôte (1F0h-1F7h,
  3F6h, 3F7h en écriture) et porte DSKCHG à la lecture de 3F7h.
- **L'IDE** : du décodage seul — IDEENLO, IDEENHI, HDCS0 (1F0h-1F7h), HDCS1 (3F6h/3F7h), IOCS16 en entrée. Aucun
  registre propre, pas de DMA (il faut de la logique externe : AN 4.14). Un mode XT existe aussi (320h-323h, HDACK).
- **Le reste** : deux UART 16450 sans FIFO (le 16550 n'arrive qu'avec les 665/666) ; le port parallèle,
  bidirectionnel sur le seul 651 ; GAMECS en 201h ; COM3 et COM4 en 338h et 238h, et non en 3E8h et 2E8h. Le 652 sort
  PSPIRQ et SSPIRQ, que des cavaliers mènent à IRQ 3 ou 4.
- **La configuration** : le 651 (carte mère) par logiciel — 55h deux fois en 3F0h, l'index en 3F0h, la valeur en
  3F1h, CR0 à CR3, AAh pour sortir ; le 652 (carte fille) par cavaliers, lus sur le front descendant de RESET, sur des
  broches partagées avec IDEENLO et IDEENHI (d'où les résistances de 27 kΩ).
- **Les incohérences de la fiche** : HDCS1 couvre 3F5h-3F7h dans la description de broche, 3F6h/3F7h dans le
  tableau 1 ; la configuration du port série par IDEENLO et IDEENHI dite « Primary » mais décrite comme choisissant le
  port secondaire ; « 16540 » pour 16450.

## La recommandation

**La carte multi-I/O ISA 16 bits à SMC FDC37C652.** C'est la seule où chaque comportement émulé citera une source
primaire (décision n° 8). La Prime 2C et la DTC 2278 reposeraient sur des indices : des tableaux de TH99, un pilote
Linux.

## La performance : un risque négligeable

- **Le chemin chaud ne change pas** : le transfert disque passe par le port de données 1F0h (REP INSW, 256 mots par
  secteur), dans `Ide/ide.cs`. La partie IDE du 652 n'est que du décodage.
- **Les cavaliers se lisent au démarrage**, comme les clés de G13.12b et G13.13b, toutes froides.
- **Les disquettes en 765B en font moins** que le 765/82077 de PCem (`Floppy/fdc.cs`) : pas de FIFO, pas de TDR
  étendu en 3F3h ; 62 Ko/s au plus, cadencés par des minuteries.
- **3F7h ne se lit qu'au contrôle du changement de disquette** par le BIOS. Les 16450 sont plus simples que les 16550.
- **Le seul vrai coût possible** : compter le temps du bus ISA à chaque accès (états d'attente, cycles de 8 ou de 16
  bits). Ce serait une décision à part, mesurée à part.

## Les deux modes

- **Sans la carte : l'état actuel, intact.** `hdd_controller = ide` (`Ide/ide.cs`, `hdd_controller_current_is_ide`)
  et les disquettes que pose `common_init` (`Models/model.cs`, `fdc_add`). C'est le choix des machines où la carte n'a
  pas de sens, ou de celles qui tournaient mieux sans elle.
- **Avec la carte** : une valeur neuve de la même clé `hdd_controller` (par exemple `smc652`), déjà dans le menu de
  `Host/SdlSetup.cs`. Choisie, elle remplacerait aussi les disquettes de la carte mère, et peut-être COM et LPT.
- **À trancher dans le plan de conception** : l'articulation avec les clés serial1/2 et parallel1/2 de G13.13b ; les
  machines qui ont déjà une Super I/O sur leur carte mère.

## L'étape suivante (sur feu vert ; l'émulateur ne change pas)

1. Télécharger les sources primaires hors de git, dans `iXtal26/Docs/cartes-ide/sources/` (une règle au
   `.gitignore`) : la fiche du FDC37C651/652, l'AN 4.14 de SMSC, et pour comparaison celles du GM82C765B et de la
   W83757, avec les fiches TH99 utiles.
2. Lire la fiche du 652 sur place (`pdftotext`) ; relever ce que l'émulateur fait déjà, en mode matériel, des
   disquettes, de l'IDE et de 3F7h ; en tirer une note des écarts, sans rien corriger.
3. Ce qui en sort — les écarts, les clés des cavaliers, la conception des deux modes — fera un plan à part, soumis à
   Julien.

**La vérification** : chaque PDF s'ouvre sous `pdftotext`, son titre et sa révision sont ceux du tableau des sources ;
`git status` ne montre rien de `sources/`.

## Sources

« Lu » : ouvert et lu par la recherche ; « non ouvert » : vu dans un résultat de recherche seulement.

| Carte | Document | URL | État |
|---|---|---|---|
| SMC | Fiche FDC37C651/652, « Super I/O Floppy Disk Controllers », SMC, ©1992, Rev. 9/9/92, 90 p. | http://www.bitsavers.org/components/standardMicrosystems/_dataSheets/SMC_37C651.pdf | Lu, primaire |
| SMC | La même fiche | https://theretroweb.com/chip/documentation/fdc37c651-652-6419cea6afbea780722193.pdf | Lu, primaire |
| SMC | Fiche FDC37C665IR/666IR (le nom du fichier trompe) | http://www.bitsavers.org/components/standardMicrosystems/_dataSheets/SMC_37C655.pdf | Page 1 seule, primaire |
| SMC | Fiche FDC37C665GT | http://www.bitsavers.org/components/standardMicrosystems/_SMSC/_dataSheets/FDC37C665GT_SuperIO.pdf | Non ouvert, primaire |
| SMC | AN 4.14, « FDC37C6XX DMA IDE Support », SMSC, 1997, 2 p. | http://www.bitsavers.org/components/standardMicrosystems/_SMSC/appnote/AN4.14_FDC37C6XX_DMA_IDE_Support.pdf | Lu, primaire |
| SMC | Manuel Wiseland MP666/MP654/MP652 (cavaliers J1-J18 ; le MP652 sans IDE) | https://theretroweb.com/expansioncard/documentation/scanned-20251110-0935-6914f3b55d434902496043.pdf | Lu, secondaire |
| SMC | Wiseland SPG-660/540 (8 bits, FDC37C652) | https://theretroweb.com/expansioncards/s/wiseland-spg-660-540 | Lu |
| SMC | Arnos VL-ADI2 (VLB, FDC37C652 et Appian ADI2) | https://theretroweb.com/expansioncards/s/arnos-aic-hk-vl-adi2 ; https://theretroweb.com/expansioncard/documentation/aic-vlb-io-user-guide-67e069016fc35343198773.pdf | Non ouvert |
| SMC | Octek VL-COMBO 3.2 | https://theretroweb.com/expansioncards/s/octek-vl-combo-3.2 | Non ouvert |
| SMC | TH99, « IDE/FDC Multi I-O » (puce non nommée) | https://th99.classic-computing.de/c/U-Z/20726.htm | Lu, secondaire |
| SMC | Data books SMC (bitsavers s'arrête à 1988) | http://www.bitsavers.org/components/standardMicrosystems/_dataBooks/ | Rien de 1989-1994 |
| Goldstar | Fiche GM82C765B, « Floppy Disk Subsystem Controller », 36 p. | https://gendev.spritesmind.net/datasheet/GM82C765B.pdf | Illisible en ligne (extraits seuls : pdf.jiepei.com/gm82c765-9163103.html), primaire |
| Goldstar | Fiche WD37C65C, WD, novembre 1991 (comparaison) | https://bitsavers.mirrorservice.org/components/westernDigital/_dataBooks/1992_SystemLogic_Imaging_Storage/25_WD37C65C.pdf | Non ouvert, primaire |
| Goldstar | TH99, Prime 2 (J1-J12) | https://th99.classic-computing.de/c/E-H/20334.htm ; https://stason.org/TULARC/pc/hard-disk-floppy-controllers/E-H/GOLDSTAR-ELECTRONICS-INTERNATIONAL-INC-Two-IDE-AT-32.html | Lu, secondaire |
| Goldstar | TH99, Prime 2C MKIII | https://th99.classic-computing.de/c/U-Z/21437.htm | Lu, secondaire |
| Goldstar | The Retro Web, Prime 2, Prime 2C MKIII, la version VLB (MIOVLU6-AA) | https://theretroweb.com/expansioncards/s/goldstar-gold-star-prime-2 ; https://theretroweb.com/expansioncards/s/goldstar-gold-star-prime-2c-mkiii ; https://theretroweb.com/expansioncards/6784 | Lu |
| Goldstar | Wiki de Vogons, Prime 2C MKIII (dit TH99 faux sur primaire/secondaire) | http://www.vogonswiki.com/index.php/GoldStar_Prime_2C_MKIII | Lu |
| Goldstar | Viric, relevé de cavaliers d'une « IDE PLUS-V3 » | https://viric.name/oldcomps/iocard-gprime2.html | Lu |
| Goldstar | FCC JGBPRIME2C (7 mars 1994, sans pièce jointe) | https://fccid.io/JGBPRIME2C | Lu |
| DTC | Pilote Linux `dtc2278.c` (v0.02, 10/02/1996) | https://www.kernel.org/pub/linux/kernel/people/marcelo/linux-2.4/drivers/ide/dtc2278.c ; https://elixir.bootlin.com/linux/v2.6.12.2/source/drivers/ide/legacy/dtc2278.c | Lu, secondaire |
| DTC | Fiche W83759A, Rev. A1, mai 1995, 41 p. (comparaison ; pas la puce de la carte) | https://bitsavers.mirrorservice.org/components/winbond/W83759_199505.pdf | Lu p. 1-24, primaire |
| DTC | Fiche W83757 | https://theretroweb.com/chip/documentation/w83757-6454bd5a47d28732390949.pdf | Lu p. 1-3, primaire |
| DTC | Fiches W83787F (avril 1995) et W83758F (nov. 1994) | https://theretroweb.com/chip/documentation/w837x7f-66e4962245176354447704.pdf ; …/w83758f-datasheet-ocred-6a92543e177be472142418.pdf | Non ouvert, primaire |
| DTC | TH99 : 2278E 400516, 2278E 400518, 2278EB, 2278S, 2278VL | https://th99.classic-computing.de/c/C-D/20836.htm ; https://hwmuseum.pp.ua/th99/c/C-D/21091.htm ; …/20837.htm ; …/20778.htm ; …/20779.htm | Lu, secondaire |
| DTC | stason, la même fiche 2278E | https://stason.org/TULARC/pc/hard-disk-floppy-controllers/C-D/DATA-TECHNOLOGY-CORPORATION-Two-IDE-AT-or-EIDE-int-288.html | Non ouvert |
| DTC | The Retro Web : 2278D, 2278D 400544, 2278D 400537, 2278E 400518, 2278S/D 9001 | https://theretroweb.com/expansioncards/s/data-technology-corporation-dtc2278d (et les pages voisines) | Lu, secondaire |
| DTC | Pilotes DTC et Atronics | https://archive.org/details/dtc2278vesaeidecontrollercard ; https://files.retropc.se/hardware/HDD/DTC/index.html | Lu, secondaire |
| DTC | Annonce Recycled Goods (puces DTC2278D+, W83758E, Rev A) | https://recycledgoods.com/dtc-ide-local-bus-multi-i-o-vlb-controller-card-2278d-dtc2278d/ | Lu, commercial |

**Introuvables** : la fiche des puces Prime 2 et Prime 2C ; un manuel ou un schéma Goldstar ; la fiche de l'Atronics
IDE-2015PL et le manuel DTC 2278D/E ; les fiches FDC37C661/662/663 ; un manuel de carte ISA 16 bits à 652 et IDE ; les
fils de Vogons et de Higher Intellect (403). 86Box n'a ni DTC2278 ni Atronics (`hdc_ide_w83769f.c` est une autre puce) ;
MAME n'a pas été regardé.
