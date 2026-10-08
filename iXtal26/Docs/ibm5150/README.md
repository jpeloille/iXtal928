# Le PC 5150, des fiches techniques au code

> Ouvert le 7 octobre 2026. Ce dossier explique chaque composant du cœur de l'IBM PC 5150 émulé. Pour chacun,
> il part des sources primaires (les fiches d'Intel, la Technical Reference d'IBM), puis décrit le code C# qui en
> découle.

Le cœur, c'est ce qui tourne à chaque démarrage d'un 5150 réglé par défaut : un 8088 à 4,77 MHz, 640 Ko de mémoire, une
CGA, deux lecteurs 5,25" DD, sans carte son ni disque dur. Il compte 42 fichiers et 25 367 lignes de C#, recensés le
7 octobre 2026 ; la liste est à la fin de ce fichier. Les options (le 8087, les autres cartes vidéo, les disques durs,
les cartes son) ne sont pas couvertes ici.

Chaque document du dépôt a son rôle :
- `PCEM_BUGS.md` recense les défauts de PCem ;
- `VERIFICATION.md` garde les preuves ;
- `pourquoi-on-corrige.md` donne la raison de chaque correction ;
- ce dossier explique ce que fait la machine réelle et comment le code le reproduit.

## Comment lire un chapitre

Un chapitre s'ouvre sur son titre, la mention « Lu sur » suivie d'un commit (l'arbre sur lequel ses renvois au code ont
été vérifiés), puis la liste de ses sources. Il se découpe ensuite en cinq parties :

1. **Le composant** : ce que dit la fiche (registres, modes, protocole).
2. **Dans le 5150** : ce que dit la Technical Reference (ports, IRQ, DMA, horloge, câblage), et ce qu'en fait le BIOS.
3. **Le code** : les fichiers et les fonctions, lus pas à pas, avec l'original de PCem en regard.
4. **Les écarts** : les PB du registre et leur état dans le mode matériel de G13. Ce qu'aucune source ne documente est
   reproduit tel quel.
5. **Pour l'observer** : les portes, les outils et les bancs DEBUG qui le font voir.

Chaque affirmation indique son niveau de preuve, comme dans les rapports de G13 :
- *documenté* : une source primaire le dit ;
- *documenté (secondaire)* : seule une source secondaire le dit ;
- *mesuré* : une porte, un outil ou une somme l'a montré ;
- *déduit* : on le tire d'un raisonnement sur les sources ou sur le code ;
- *inconnu* : aucune source n'en parle, il faudrait mesurer.

## Les sources

Les PDF sont rangés dans `sources/`, hors de git. `tools/fetch-sources.sh` les télécharge, et avec `--verify` contrôle
leurs sommes ; `sources/MANIFEST.tsv` donne leurs URL. Les numéros de page ci-dessous sont ceux du PDF.

| Id | Source | Où trouver |
|---|---|---|
| I1 | Intel, *The 8086 Family User's Manual*, 9800722-03, octobre 1979 | ch. 2, les UC 8086 et 8088 : p. 24, la page imprimée 2-n étant la page n + 23 du PDF ; table 2-20 (adresse effective) : p. 74 ; ch. 4, le matériel : p. 240 |
| I2 | Intel, *Component Data Catalog*, 1981 | 8237 : p. 432 ; 8259A : p. 464 ; 8088 : p. 528 ; 8284A : p. 593 ; 8288 : p. 606 ; 8253 : p. 684 ; 8255A : p. 710 |
| T1 | IBM, *Personal Computer Technical Reference*, 6322507, avril 1984, édition révisée | section 1, la carte mère : p. 20 (= 1-1) ; plan des E/S : p. 43 (1-24) ; section 4, le clavier : p. 80 (4-1) ; section 5, le BIOS : p. 94 (5-1), et son listing dès la p. 122 (5-29) ; section 6, le jeu d'instructions : p. 208 ; index : p. 300 |
| T2 | IBM, *Personal Computer Technical Reference*, 6025008, août 1981 | l'édition d'origine : la carte mère de 16 à 64 Ko, le BIOS du 24/04/81 |
| T3 | IBM, *Technical Reference Options and Adapters*, volume 1, avril 1984 | le lecteur 5,25" : p. 229 ; l'extension mémoire de 64/256 Ko : p. 401 |
| T4 | IBM, *Technical Reference Options and Adapters*, volume 2, avril 1984 | la CGA : p. 38 ; l'adaptateur imprimante : p. 252 ; l'adaptateur de disquettes : p. 266 ; l'adaptateur série : p. 450 ; l'adaptateur manette : p. 678 |
| N1 | NEC, fiche du µPD765, décembre 1978 | 20 pages |
| M1 | Motorola, fiche du MC6845 | 23 pages |
| S1 | National Semiconductor, *Microcommunications Elements Databook*, 1987 | l'INS8250A et le NS16450 : p. 164 |
| S2 | National Semiconductor, note d'application AN-493, avril 1989 | ce qui distingue l'INS8250 d'origine de l'INS8250A |
| R1 | Les octets de `roms/ibmpc/` et de `roms/mda.rom`, désassemblés (`objdump -D -b binary -m i8086`) | ce que fait réellement le BIOS 10/27/82, à l'adresse près |

Le listing de T1 est celui du BIOS 10/27/82, le même que `roms/ibmpc/pc102782.bin`. T2 donne celui du 24/04/81 : il
renseigne sur l'histoire de la machine, pas sur le code.

R1 compte comme source documentée pour sa puce (décision n° 8 de PLAN-G13). Les autres émulateurs (86Box, MAME,
DOSBox-X) ne servent que d'indices.

## Les conventions de renvoi

- **Une source** : `[T1 p. 1-24]` pour une page imprimée ; `[T1 p. 5-29, l. 345]` pour une ligne de listing ;
  `[R1 F000:E5BC]` pour un octet de ROM. Le tableau ci-dessus donne la page du PDF qui correspond.
- **Le code C#** : `<chemin>.cs#<symbole>`, chemin pris sous `iXtal26/`, par exemple `Models/pit.cs#pit_write`. Jamais de
  numéro de ligne : G13 fait bouger le code, et un numéro périmé se lit comme s'il était juste.
- **PCem** : `pcem-dev/src/models/pit.c:37-59`. L'arbre vendoré ne bouge pas.
- **Le registre** : `PB-nn`, numéroté comme dans `PCEM_BUGS.md`.

`tools/check-doc5150.sh` contrôle tous ces renvois. Avec `--couverture`, il vérifie en plus que chacun des 42 fichiers
est cité par au moins un chapitre.

## Les chapitres

Ils suivent le matériel, en partant du 8088.

| N° | Chapitre | État |
|---|---|---|
| 00 | La machine : la carte mère, les plans de la mémoire et des E/S, les IRQ, le DMA, les horloges | à écrire |
| 01 | Le 8088 : l'architecture, la file d'attente, les cycles de bus, le reset | écrit, contre-lu |
| 02 | Le 8088 : les instructions, avec la table des 256 opcodes et des groupes | écrit, contre-lu |
| 03 | Le 8088 : les interruptions et le pas à pas | à écrire |
| 04 | L'horloge : le 8284A, et le temps de l'émulateur | à écrire |
| 05 | Le bus : le 8288, le canal d'E/S, le décodage des ports | à écrire |
| 06 | La mémoire : la RAM, la parité, la ROM, le plan mémoire | à écrire |
| 07 | Le 8259A, contrôleur d'interruptions | à écrire |
| 08 | Le 8253, la minuterie | à écrire |
| 09 | Le 8237A, le DMA, et les registres de page | à écrire |
| 10 | Le 8255A et les interrupteurs SW1 et SW2 | à écrire |
| 11 | Le clavier | à écrire |
| 12 | Le haut-parleur | à écrire |
| 13 | La cassette | à écrire |
| 14 | La NMI | à écrire |
| 15 | La disquette : le µPD765, l'adaptateur, les lecteurs | à écrire |
| 16 | La CGA : le MC6845, les modes, la police | à écrire |
| 17 | Les ports série : l'INS8250, et la souris | à écrire |
| 18 | Le port parallèle | à écrire |
| 19 | Le port manette | à écrire |
| 20 | Le BIOS et le BASIC : ce que le POST attend de chaque puce | à écrire |
| 21 | L'assemblage : du lancement à la tranche de 10 ms | à écrire |

Les écarts trouvés en écrivant et absents du registre sont consignés dans [constats.md](constats.md).

## Les 42 fichiers

Chaque fichier du cœur, avec les chapitres qui l'expliquent :

- `Program.cs` : 21
- `pc.cs` : 04, 21
- `io.cs` : 05
- `timer.cs` : 04, 21
- `ppi.cs` : 10
- `GlobalUsings.cs` : 21
- `Models/model.cs` : 00, 21
- `Models/pic.cs` : 03, 07
- `Models/pit.cs` : 04, 08, 12
- `Models/dma.cs` : 09
- `Models/nmi.cs` : 14
- `Models/serial.cs` : 17
- `Cpu/808x.cs` : 01, 02, 03, 14
- `Cpu/x86.cs` : 01
- `Cpu/x86seg.cs` : 01
- `Cpu/cpu.cs` : 01, 04
- `Cpu/cpu_tables.cs` : 04
- `Cpu/386_common.cs` : 01
- `Memory/mem.cs` : 05, 06
- `Memory/mem_bios.cs` : 06, 20
- `Flash/rom.cs` : 06
- `Keyboard/keyboard_xt.cs` : 10, 11, 12, 13
- `Keyboard/keyboard.cs` : 11
- `Devices/cassette.cs` : 13
- `Floppy/fdc.cs` : 15
- `Floppy/fdd.cs` : 15
- `Disc/disc.cs` : 15
- `Disc/disc_img.cs` : 15
- `Disc/disc_sector.cs` : 15
- `Video/video.cs` : 16
- `Video/vid_cga.cs` : 16
- `Sound/sound_speaker.cs` : 12
- `Sound/sound.cs` : 12
- `Lpt/lpt.cs` : 18
- `Joystick/gameport.cs` : 19
- `Mouse/mouse.cs` : 17
- `Mouse/mouse_serial.cs` : 17
- `PluginApi/device.cs` : 05, 21
- `PluginApi/config.cs` : 21
- `PluginApi/paths.cs` : 21
- `Diag/Counters.cs` : 21
- `Diag/R9.cs` : 21

Le chapitre 21 nomme aussi les fichiers qui s'exécutent sur un 5150 sans y servir : le reset de l'ALi 1429, le CMOS
absent, la pile CD et IDE à vide.
