# Contrôleurs de disques (HDD et FDC) par génération de PC

Ce document détaille les contrôleurs de disque dur (HDD) et de disquette (FDC) utilisés à la **fin de vie commerciale** de chaque génération de processeur, ainsi que les interfaces et standards associés.

---

## 1. Intel 8088 / PC XT (fin de vie ≈ 1989)

### Contrôleur HDD (ST-506/ST-412)
Les disques durs étaient connectés via des **cartes contrôleurs ISA 8-bit** utilisant l'interface **ST-506/ST-412** développée par Seagate. Le contrôleur gérait directement le formatage bas niveau, la géométrie des disques et le codage des données.

| Caractéristique | Détail |
|---|---|
| **Interface** | ST-506 / ST-412 |
| **Bus** | ISA 8-bit |
| **Codage** | MFM (5 Mbit/s, ~17 secteurs/piste), puis RLL en fin de période (7,5 Mbit/s, ~50% plus de capacité) |
| **Capacités typiques** | 10–40 Mo |

#### Contrôleurs dominants
- **Western Digital WD1002-WX1/WX2** : contrôleur MFM pour XT avec ROM BIOS intégrée contenant la table des géométries de disques.
- **Western Digital WD1001** : prédécesseur utilisant le microcontrôleur 8X300.
- **Xebec 1210 / 1410** : contrôleur ST-506 très répandu sur les XT et clones.
- **DTC (Data Technology Corp)** : contrôleurs compatibles ST-506.
- **« Hard cards »** : disque dur monté physiquement sur une carte ISA longue, courant pour gagner de la place dans les boîtiers compacts.

### Contrôleur FDC
- **NEC µPD765** : contrôleur de disquette de l'IBM PC/XT original. Gère les disquettes 5,25" 360 Ko et 1,2 Mo, ainsi que les 3,5" 720 Ko.
- Souvent intégré sur la **même carte ISA multifonction** que le contrôleur HDD, ou directement sur la carte mère des clones XT tardifs.

### Particularités
- Le codage **RLL** (Run Length Limited) apparaît en fin de période pour augmenter la capacité, mais nécessite des disques certifiés RLL. Un disque de 20 Mo MFM peut passer à 30 Mo en RLL.
- Le BIOS du contrôleur contient la table des cylindres/têtes/secteurs (CHS) car les disques n'ont pas d'intelligence embarquée.

---

## 2. Intel 80286 / PC AT (fin de vie ≈ 1991)

### Contrôleur HDD — Époque de transition
C'est la période où plusieurs technologies coexistent : ST-506 (MFM/RLL), ESDI, et les débuts de l'IDE.

#### 2a. MFM/RLL sur bus 16-bit (ST-506)
| Caractéristique | Détail |
|---|---|
| **Interface** | ST-506/ST-412 |
| **Bus** | ISA 16-bit |
| **Codage** | MFM ou RLL |
| **Contrôleurs** | WD1002-WAH, WD1003 |

- **Western Digital WD1002-WAH** : contrôleur MFM/RLL 16-bit ISA, supportant deux disques ST-506.
- **Western Digital WD1003** : évolution compatible AT, ancêtre logique du standard IDE.

#### 2b. ESDI (Enhanced Small Device Interface)
| Caractéristique | Détail |
|---|---|
| **Interface** | ESDI |
| **Câblage** | Identique au ST-506 (34 + 20 broches) mais signaux différents |
| **Débit** | 10–24 Mbit/s |
| **Capacités** | 80–300 Mo |
| **Fabricants** | Western Digital, Adaptec (ACB-4070), DTC, Emulex |

- Apparu en 1986, très répandu sur le **haut de gamme 286/386 début 90s**.
- Le contrôleur ESDI intègre souvent un cache et gère les défauts de manière plus intelligente que le ST-506.
- Les disques ESDI peuvent être formatés en RLL pour des capacités supérieures.

#### 2c. IDE / AT-IDE (début)
| Caractéristique | Détail |
|---|---|
| **Interface** | AT Attachment (ATA) — "Integrated Drive Electronics" |
| **Bus** | ISA 16-bit |
| **Standard** | Créé en 1986 par Compaq et Western Digital |
| **Principe** | Le contrôleur est intégré au disque ; la carte ISA n'est plus qu'une « host adapter » |

- Les premiers disques IDE apparaissent sur 286/386 fin 80s, mais restent minoritaires face au MFM jusqu'en 1990.
- L'IDE simplifie considérablement l'installation : plus de tables CHS complexes dans le BIOS du contrôleur.

### Contrôleur FDC
- **Intel 82072A** : contrôleur FDC de l'IBM PC/AT, compatible µPD765 avec commandes étendues.
- **Western Digital WD37C65/A/B** : « superchip » CMOS très populaire intégrant le FDC, le séparateur de données (PLL), la précompensation, la génération d'horloge et l'interface de lecteur. Compatible PC/AT et BIOS.
- Le FDC est souvent une puce séparée sur carte mère ou intégrée au **chipset** (ex : UMC, OPTi).

---

## 3. Intel 80386 SX (fin de vie ≈ 1992–1993)

### Contrôleur HDD — IDE devient majoritaire
Le 386SX a un bus de données externe 16 bits, ce qui le rend compatible natif avec les contrôleurs ISA 16-bit et l'IDE standard.

| Caractéristique | Détail |
|---|---|
| **Interface dominante** | IDE (ATA-1) |
| **Bus** | ISA 16-bit |
| **Capacités typiques** | 40–200 Mo |
| **Alternative haut de gamme** | SCSI-2 |

#### IDE standard (ATA-1)
- Disques IDE de 40–120 Mo branchés directement sur une carte mère équipée d'un **connecteur IDE intégré** (via chipset VLSI, SiS, OPTi) ou sur une carte ISA « multi I/O ».
- Les chipsets intègrent désormais l'interface IDE directement, libérant un slot ISA.
- Le BIOS des cartes mères 386SX commence à inclure le support LBA (Linear Block Addressing) pour dépasser les limitations CHS.

#### SCSI (haut de gamme et stations de travail)
- **Adaptec AHA-1542CF** : contrôleur SCSI-2 ISA 16-bit très populaire. DMA bus-master, cache onboard, BIOS ROM. Débit jusqu'à 5–10 Mo/s selon configuration.
- Permet de chaîner disques durs, CD-ROM et scanners sur un même bus.
- Plus cher que l'IDE, réservé aux configurations professionnelles.

#### ESDI
- Encore présent sur certaines stations de travail héritées, mais en **déclin rapide** face à IDE et SCSI.

### Contrôleur FDC
- **Western Digital WD37C65B** ou **Intel 82077A** : gestion des disquettes 1,44 Mo et parfois 2,88 Mo (ED — Extra Density).
- Apparition des premiers circuits **Super I/O** qui regroupent FDC, ports série (UART), port parallèle et parfois IDE sur une seule puce (ex : SMC FDC37C65).
- Le FDC devient rarement une puce indépendante ; il migre vers des circuits intégrés multifonctions.

---

## 4. Intel 80386 DX (fin de vie ≈ 1993–1994)

### Contrôleur HDD — EIDE et VLB
Le 386DX a un bus de données **32 bits**, ce qui autorise des interfaces plus rapides et l'utilisation du **VESA Local Bus (VLB)** pour les contrôleurs haute performance.

| Caractéristique | Détail |
|---|---|
| **Interface grand public** | IDE / EIDE (ATA-2 / Fast ATA) |
| **Interface haut de gamme** | SCSI-2 (ISA ou VLB) |
| **Bus d'extension** | ISA 16-bit, EISA 32-bit, VLB 32-bit |
| **Capacités typiques** | 120–500 Mo |

#### IDE / EIDE (Enhanced IDE)
- **EIDE** apparaît en 1994. Supporte les modes PIO 3/4, le transfert DMA multiword, et dépasse la barrière des 504 Mo grâce au **LBA** (Logical Block Addressing).
- Le contrôleur est **100 % intégré au chipset** de la carte mère (ex : chipsets Intel 420 series, ou équivalents VLSI/OPTi/SiS pour 386DX).
- Les cartes mères 386DX tardives proposent souvent **2 canaux IDE** (Primary/Secondary) permettant jusqu'à 4 disques.
- Certaines cartes « multi I/O » avec BIOS ROM propriétaire (ex : Promise) permettent d'ajouter des disques IDE sur anciennes cartes mères.

#### SCSI haut de gamme
- **Adaptec AHA-1542CF** : reste la référence ISA 16-bit, très utilisé sur les 386DX.
- **Adaptec AHA-2842A** : version **VLB (VESA Local Bus)** pour 386DX/486 offrant des débits supérieurs à 6 Mo/s, bien au-dessus des capacités du bus ISA (max ~6 Mo/s théorique, souvent 2–3 Mo/s en pratique).
- Le VLB exploite la largeur 32 bits du bus processeur du 386DX, ce qui est impossible sur un 386SX.

### Contrôleur FDC
- **Intégration totale au Super I/O** : puces comme le **SMC FDC37C65**, **FDC37C66**, **Winbond W83877F** regroupent FDC, UARTs (ports série), port parallèle, manette de jeu et parfois infrarouge.
- Le FDC n'est pratiquement plus une puce indépendante mais un bloc dans le circuit Super I/O du chipset.
- Support des formats : 720 Ko, 1,44 Mo, et 2,88 Mo (ED) sur les machines les plus récentes.

### Particularités du 386DX
- Contrairement au 386SX, le **386DX autorise le VLB**, ce qui permet d'atteindre des débits disque bien supérieurs (jusqu'à 40 Mo/s théoriques sur VLB, ~20–25 Mo/s en pratique).
- Les chipsets pour 386DX (ex : OPTi 386WB, SiS 310/320, UMC 480) intègrent souvent le contrôleur IDE et le FDC directement, réduisant le nombre de composants discrets.

---

## Tableau récapitulatif de l'évolution

| Processeur | Fin de vie | **HDD : Interface** | **HDD : Contrôleur typique** | **FDC : Puce typique** | **FDC : Emplacement** |
|---|---|---|---|---|---|
| **Intel 8088** | ~1989 | ST-506 (MFM/RLL) | Carte ISA 8-bit : WD1002, Xebec 1210, WD1001 | NEC µPD765 | Carte mère ou carte multifonction ISA |
| **Intel 80286** | ~1991 | ST-506 / ESDI / IDE début | Carte ISA 16-bit : WD1003, ESDI, AT-IDE | Intel 82072A / WD37C65 | Carte mère ou chipset |
| **Intel 80386 SX** | ~1993 | IDE (ATA-1) / SCSI-2 | Carte mère (IDE intégré) ou AHA-1542CF (SCSI) | WD37C65B / Intel 82077A | Carte mère / Super I/O début |
| **Intel 80386 DX** | ~1994 | EIDE (ATA-2) / SCSI-2 VLB | Chipset carte mère (EIDE) ou AHA-2842A (VLB SCSI) | Super I/O (SMC, Winbond) | Intégré chipset |

---

## Évolutions clés à retenir

1. **ST-506 → IDE** : Le grand basculement s'opère entre 1988 et 1991. Le contrôleur sort de la carte ISA pour rejoindre le disque (IDE), puis la carte mère. L'installation devient « plug and play » comparativement au formatage bas niveau MFM.

2. **ISA → VLB** : Le 386DX est le premier processeur grand public à exploiter pleinement le bus 32 bits via VLB pour les contrôleurs SCSI haut de gamme. Le 386SX, limité à 16 bits externe, ne peut pas utiliser VLB.

3. **FDC autonome → Super I/O** : Le contrôleur de disquette passe d'une puce dédiée (NEC µPD765) à un bloc intégré dans un circuit multifonctions, libérant des emplacements et réduisant les coûts de fabrication.

4. **MFM/RLL → EIDE** : L'augmentation des capacités (dépassant 500 Mo) et l'arrivée du CD-ROM rendent l'EIDE indispensable en 1994. Les modes DMA et le LBA éliminent les goulets d'étranglement du PIO mode 0.
