# Configurations PC à la fin de vie des processeurs Intel x86 (1989–1994)

Ce document recense les configurations matérielles typiques et haut de gamme que l'on pouvait trouver sur le marché à la **fin du cycle commercial** de chaque génération de processeur Intel, du bon marché aux stations de travail.

---

## 1. Intel 8088 (fin de vie commerciale ≈ 1989)

En 1989, le 8088 n'équipait plus que les PC « budget » et les derniers clones XT. Sa limite restait le bus de données 8 bits et les 640 Ko adressables en mode réel sous DOS.

| | **Entrée de gamme** | **Milieu de gamme** | **Haut de gamme / fin de vie** |
|---|---|---|---|
| **CPU** | 8088 @ 4,77–10 MHz | 8088 @ 10 MHz (mode turbo) | 8088 @ 10 MHz |
| **RAM** | 512–640 Ko | 640 Ko | 640 Ko (maximum utilisable sous DOS) |
| **Disque dur** | Aucun ou 20 Mo | 20–30 Mo (MFM) | 30–40 Mo (contrôleur RLL) |
| **Lecteurs** | 1 floppy 5,25" 360 Ko | 1 floppy 5,25" 1,2 Mo | 1 ou 2 floppies 5,25" / 3,5" |
| **Graphique** | MDA / CGA / Hercules | CGA / Hercules | EGA (rare) |
| **Système d'exploitation** | DOS 3.x | DOS 3.3 / 4.01 | DOS 4.01 |

### Exemple concret
Le clone *Swan XT10* (1989) proposait un 8088 à 10 MHz avec 640 Ko de RAM et un disque dur de 32 Mo en option, pour un prix situé entre 550 et 800 $ [^14^]. À cette époque, 30 Mo restait le gros standard pour un XT ; au-delà, le BIOS avait du mal à adresser l'espace sans overlay logiciel.

---

## 2. Intel 80286 (fin de vie commerciale ≈ 1991)

Intel a discontinué le 80286 en 1991 [^4^]. Le processeur dominait encore le milieu de gamme en 1989–1990. Le passage au mode protégé commençait à être exploité par Windows 3.0 et OS/2.

| | **Entrée de gamme** | **Milieu de gamme** | **Haut de gamme / fin de vie** |
|---|---|---|---|
| **CPU** | 286 @ 10–12 MHz | 286 @ 12–16 MHz | 286 @ 16–20 MHz (25 MHz très rare) |
| **RAM** | 1 Mo | 1–2 Mo | 4 Mo (8 Mo exceptionnel, 16 Mo quasi inexistant) |
| **Disque dur** | 20–40 Mo (IDE ou MFM) | 40–80 Mo IDE | 110–210 Mo IDE |
| **Lecteurs** | 1 floppy 3,5" 1,44 Mo | 1 ou 2 floppies 3,5" / 5,25" | 1,44 Mo + 1,2 Mo |
| **Graphique** | EGA | VGA 16/256 couleurs | Super VGA (800×600) |
| **Système d'exploitation** | DOS 3.x / 4.01 | DOS 4.01 / 5.0 + Windows 3.0 | DOS 5.0 + Windows 3.1 / OS/2 1.21 |

### Exemple concret
L'*AST Bravo 286/16* (1989) embarquait un 80286 à 16 MHz, 1 Mo de RAM (extensible théoriquement à 16 Mo), et proposait des disques durs allant de 40 Mo jusqu'à **210 Mo** en option [^8^]. Sur le marché des composants, des cartes mères haut de gamme fin 1991 supportaient jusqu'à 4 Mo de mémoire vive rapide (voire 16 Mo avec des barrettes SIMM/SIPP très coûteuses) [^16^].

---

## 3. Intel 80386 SX (fin de vie commerciale ≈ 1992–1993)

Le 386SX fut le « 286 amélioré » : architecture 32 bits en interne, mais bus de données 16 bits et limite physique de 16 Mo de RAM. Il a dominé le milieu de gamme jusqu'à l'arrivée massive des 486 DX2.

| | **Entrée de gamme** | **Milieu de gamme** | **Haut de gamme / fin de vie** |
|---|---|---|---|
| **CPU** | 386SX @ 16–20 MHz | 386SX @ 25 MHz | 386SX @ 25 MHz |
| **RAM** | 1 Mo | 2–4 Mo | 4–8 Mo (16 Mo théorique mais rare) |
| **Disque dur** | 40 Mo | 80–120 Mo | 120–200 Mo |
| **Lecteurs** | 1 floppy 3,5" 1,44 Mo | 1,44 Mo + 1,2 Mo | 1,44 Mo + 1,2 Mo |
| **Graphique** | VGA | VGA / SVGA (Trident) | SVGA 512 Ko VRAM |
| **Système d'exploitation** | DOS 5.0 + Windows 3.1 | DOS 6.0 + Windows 3.1 | DOS 6.2 + Windows 3.11 |

### Exemples concrets
Un PC assemblé fin d'époque tournait souvent autour d'un **386SX-25 MHz avec 4 Mo de RAM et un disque dur de 120 Mo** [^20^]. À l'inverse, des machines grand public comme l'*Amstrad Mega PC 386SX* (1993) restaient très basiques avec seulement 1 Mo de RAM et 40 Mo de disque [^19^], tandis qu'une configuration « luxe » montait à 8 Mo pour faire tourner confortablement Windows 3.1.

---

## 4. Intel 80386 DX (fin de vie grand public ≈ 1993–1994)

Le vrai 386 32 bits. À la fin de son cycle, il restait la référence pour l'utilisateur avancé avant que le 486 ne devienne abordable. Les versions AMD à 40 MHz ont prolongé son existence sur le marché.

| | **Entrée de gamme** | **Milieu de gamme** | **Haut de gamme / fin de vie** |
|---|---|---|---|
| **CPU** | 386DX @ 20–25 MHz | 386DX @ 33 MHz (Intel) | 386DX @ 33–40 MHz (AMD Am386DX-40) |
| **RAM** | 2–4 Mo | 4 Mo | 8–16 Mo (32 Mo sur cartes spéciales) |
| **Disque dur** | 60–100 Mo | 120–170 Mo | 250–500 Mo |
| **Lecteurs** | 1,44 Mo + 1,2 Mo | 1,44 Mo + CD-ROM 2x | 1,44 Mo + CD-ROM 2–4x |
| **Graphique** | VGA / SVGA | SVGA (Trident 9000i) | SVGA haut de gamme (ET-4000, 1 Mo VRAM) |
| **Système d'exploitation** | DOS 6.0 + Windows 3.1 | DOS 6.2 + Windows 3.11 | DOS 6.22 + Windows 3.11 / Windows 95 (limite) |

### Exemples concrets
Un *Slimline 386DX* de début 1993, haut de gamme pour l'époque, utilisait un **AMD Am386DX-40**, **8 Mo de RAM** (SIMM 30 broches), une carte graphique Trident 512 Ko et un disque dur **Western Digital Caviar de 170 Mo** [^15^]. Dans la communauté retro, des configurations de fin d'époque montaient fréquemment à **16 Mo de RAM** avec des disques de 500 Mo et un coprocesseur 80387 [^17^]. Microsoft a même supporté le 386DX pour Windows 95, mais uniquement à 33–40 MHz avec **au moins 8 Mo de RAM** [^10^].

---

## Synthèse comparative

| Processeur | Fin de vie | **Config « gros » RAM** | **Config « gros » disque** | Limite théorique RAM |
|---|---|---|---|---|
| **Intel 8088** | ~1989 | 640 Ko (plafond absolu DOS) | 30–40 Mo | 1 Mo |
| **Intel 80286** | ~1991 | 4 Mo (16 Mo très rare) | 110–210 Mo | 16 Mo |
| **Intel 80386 SX** | ~1993 | 4–8 Mo | 120–200 Mo | 16 Mo |
| **Intel 80386 DX** | ~1994 | 8–16 Mo (32 Mo possible) | 250–500 Mo | 4 Go |

### Observations
- Le **8088** restait bloqué sous le mégaoctet utilisable, rendant toute évolution au-delà symbolique sous DOS classique.
- Le **286** et le **386 SX** partageaient la même barre des **16 Mo** théoriques, dictée par le bus d'adresse 24 bits.
- Le **386 DX** fut le premier à casser cette limite de manière significative pour le grand public, ouvrant la voie aux configurations professionnelles avec 8 à 16 Mo de RAM et des disques de plusieurs centaines de mégaoctets.
- L'évolution se ressentait surtout à la capacité disque et à la quantité de RAM utilisable par les systèmes d'exploitation graphiques naissants (Windows 3.x, OS/2).

---

## Sources

[^4^]: Intel discontinues the 80286 in 1991 — Wikipédia / sources techniques.
[^8^]: AST Bravo 286/16 specifications — 16 MHz, RAM 1 Mo extensible, HDD 40–210 Mo.
[^10^]: Microsoft Windows 95 system requirements — support du 386DX à 33–40 MHz avec 8 Mo de RAM minimum.
[^14^]: Swan XT10 clone PC (1989) — 8088 @ 10 MHz, 640 Ko RAM, HDD 32 Mo.
[^15^]: Slimline 386DX (1993) — AMD Am386DX-40, 8 Mo RAM, HDD Western Digital Caviar 170 Mo.
[^16^]: 286 high-end motherboards late 1991 — support 4 Mo+ RAM via SIMM/SIPP.
[^17^]: Retro community high-end 386DX builds — 16 Mo RAM, 500 Mo HDD, 80387 coprocessor.
[^19^]: Amstrad Mega PC 386SX (1993) — 1 Mo RAM, 40 Mo HDD.
[^20^]: Typical late-era 386SX-25 custom build — 4 Mo RAM, 120 Mo HDD.
