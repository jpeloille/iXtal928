# iXtal26

Émulateur d'**IBM PC 5150** et d'**IBM XT 5160** (8088 à 4,77 MHz, CGA, contrôleur de
disquettes, haut-parleur, et le Fixed Disk Adapter du XT), transcrit de PCem v18 en C#.

Le cœur est vérifié bit à bit contre le C d'origine, gardé dans `pcem-dev/` : les deux
exécutent le POST du 5150 puis la ROM BASIC sur **25 457 269 instructions identiques**,
et l'amorçage de PC DOS 2.00 depuis une disquette sur **26 750 702**, registres, segments,
drapeaux et modèle de temps compris. Le XT 5160 a ses deux campagnes à lui, vertes
également : **23 442 234** sur sa ROM BASIC et **22 086 920** sous DOS. Voir
`../VERIFICATION.md`.

## Lancer

```bash
dotnet run                       # fenêtre, ROMs dans « roms », tourne jusqu'à fermeture
```

Sans disquette, le BIOS bascule sur l'INT 18h et la machine s'arrête sur l'invite de
l'IBM Cassette BASIC C1.10. Le clavier est branché ; la croix ferme la fenêtre (Échap
appartient à la machine émulée).

```bash
dotnet run -- --floppy-a os/pcdos20/pcdos20b.img   # amorce sur l'image du lecteur A:
```

`--floppy-a IMG` (et `--floppy-b`) monte une image brute `.img` — 160, 180, 320 ou 360 Ko
sur les lecteurs 5,25" double densité du 5150 — **en lecture-écriture** : ce que DOS y
écrit est écrit pour de vrai. Une image absente est refusée avec son chemin.

Le titre de la fenêtre affiche, sur une fenêtre glissante, trois chiffres tirés du tsc
de la machine : `invite NN %`, la vitesse à laquelle le temps de l'invité s'écoule
rapportée au temps réel ; `X MHz`, les cycles CPU émulés par seconde murale ; et
`marge xM`, ce que l'hôte tiendrait sans le frein. Sur le 5150, 100 % et 4,77 MHz vont
ensemble. `--timer-check` mesure les mêmes grandeurs au compteur de tops du BIOS, sur
n'importe quelle machine (`--model`, `--config`).

**Le haut-parleur est branché** : le 5150 bipe en fin de POST, et `BEEP` ou
`PLAY "CDEFGAB"` sonnent sous BASIC. C'est le canal 2 du PIT et le port 0x61, mixés à
48 kHz par le pendant de `sound.c` et déposés dans SDL3. Une sortie audio absente n'est
pas une panne : la machine tourne muette et le dit. Voir `../VERIFICATION.md` § M9.

Le son se coupe pendant le turbo — à quatorze fois le temps réel il ne resterait qu'un
hachis — mais **le bip met fin au turbo** : le BIOS n'arme le haut-parleur qu'une fois
le test mémoire passé, donc `--turbo` rend la main au temps réel juste avant de biper,
quelle que soit la taille mémoire. Le turbo saute l'attente, pas le bip.

L'amorçage dure **51,7 s** jusqu'à l'invite BASIC, dont 46 s de test mémoire : c'est la
durée authentique du 5150 à 640 Ko (VERIFICATION.md § M4.6 et § M6). Pour ne pas la
regarder passer :

```bash
dotnet run -- --turbo            # amorce à ~x14, puis rend la machine au temps réel
```

Le turbo ne touche ni au CPU émulé ni à son horloge : mêmes instructions, mêmes cycles,
même écran — seule change la vitesse à laquelle l'hôte déroule tout cela, et le nombre
d'images réellement présentées pendant la phase. Une frappe y met fin.

Lancements non interactifs :

```bash
dotnet run -- --slices 6000            # 60 s émulées, sans cadencement, puis sortie
dotnet run -- --headless --slices 6000 # idem, sans initialiser la moindre vidéo SDL
dotnet run -- --boot roms 6000 # amorce et vide l'écran texte CGA sur la console
dotnet run -- --boot roms 6500 --floppy-a os/pcdos20/pcdos20b.img --type "" --type "" --type DIR
                               # amorce DOS, répond aux invites de date et d'heure, tape DIR
dotnet run -- --boot roms 5700 --floppy-a os/pcdos20/pcdos20b.img --floppy-b vierge.img \
              --settle 4500 --type "" --type "" --type "FORMAT B:" --type ""
                               # formate une disquette vierge en B:
```

`--settle N` est le nombre de tranches laissées à l'application après chaque Entrée. Le
défaut, 200, suffit à un `DIR` ; un `FORMAT` d'une 360 Ko en demande ~4 500, faute de quoi
l'écran reste sur « Formatting... » et l'image n'est écrite qu'au dixième.

`--headless` n'est pas un raccourci de test : c'est la ligne architecturale qui garantit
que le cœur ne dépend pas du front-end. `--slices` est l'interrupteur de déterminisme —
sans cadencement horloge murale, deux exécutions donnent le même nombre de cycles.

## Construire la machine avant de la lancer

Un lancement **sans argument** ouvre un écran de construction : on choisit la machine, la
mémoire, les disquettes des lecteurs A: et B:, le contrôleur de disque dur et les images
de C: et D: — ou on en crée une sur place, qui est affectée dans la foulée. Puis
« Demarrer ».

```bash
dotnet run                  # l'écran s'ouvre
dotnet run -- --setup       # le force, même si d'autres options ont parlé
```

Il sait aussi **charger une machine enregistrée** — les `.cfg` de `configs/` et les deux
exemples de la racine — et **enregistrer** celle qu'on vient de composer, dans
`configs/machine.cfg`, `machine-2.cfg`… Ce répertoire n'est pas versionné : ces fichiers
pointent des images qui ne le sont pas non plus.

L'enregistrement va **toujours** dans `configs/`, jamais par-dessus `ixtal26.cfg` ou
`ixtal26-xt.cfg` : `config_save` réécrit le fichier depuis l'arbre en mémoire, et cet
arbre ne porte pas les commentaires — les deux exemples y perdraient toute leur
documentation.

**L'écran ne monte rien.** Il remplit exactement ce que `--config` remplit, puis rend la
main ; c'est `initpc` qui monte. Un seul chemin de montage, donc pas un second à tenir en
accord avec le premier.

Et il ne s'ouvre **jamais** quand un argument a décrit la machine — `--config`, `--model`,
`--ram`, `--floppy-a`, `--hdd`… — ni sous `--slices`, `--headless` ou `--boot`. Toutes les
commandes de ce fichier et de `../VERIFICATION.md` gardent donc leur comportement au cycle
près.

## Configurer la machine

```bash
dotnet run -- --config ixtal26.cfg      # la machine décrite par le fichier
dotnet run -- --model ibmxt             # l'IBM XT 5160 au lieu du 5150
dotnet run -- --ram 64                  # 64 Ko : l'invite BASIC en 12 s au lieu de 52
```

Deux machines : `ibmpc` (IBM PC 5150, le défaut) et `ibmxt` (IBM XT 5160). Elles partagent
tout — même 8088, même init, même CGA — sauf le BIOS, le port cassette et la granularité
RAM : **32 Ko par pas sur le 5150, 64 sur le XT**. `mem_size = 96` est donc accepté sur
l'un et refusé sur l'autre. Les deux amorcent la ROM BASIC et PC DOS 2.00 ; les deux sont
vertes au diff contre le C de PCem (`../VERIFICATION.md` § M10).

Quatre cartes vidéo, par `--gfxcard` ou la clé `gfxcard` : `cga` (le défaut), `vga` (VGA
d'IBM, `ibm_vga.bin`), `tvga9000b` (Trident 9000B, 512 Ko, `tvga9000b/BIOS.BIN`) et
`tvga8900d` (Trident 8900D, 1 Mo, `trident.bin`, jusqu'à 24 bits par pixel). Toutes sont
vertes au diff contre PCem sur le 5150 et le XT (`../VERIFICATION.md` § M15 et § M19).

Format `.cfg` de PCem : `clé = valeur`, sections `[entre crochets]`, `#` en commentaire.

```ini
model = ibmpc           # ou ibmxt ; --model l'emporte sur cette clé
mem_size = 640          # bornes DE LA MACHINE ; 0x100 marche aussi
drive_a_type = 1        # 0 aucun, 1 = 5,25" DD (le lecteur du 5150)
drive_b_type = 1
disc_a = os/pcdos20/pcdos20b.img
bpb_disable = 0
```

### Depuis l'IDE

`Properties/launchSettings.json` porte sept profils de lancement, que Rider et
`dotnet run --launch-profile` lisent tous les deux. Le premier est le défaut :

| Profil | Arguments |
|---|---|
| `iXtal26 (construire la machine)` | `--setup` |
| `iXtal26` | `--config ixtal26.cfg --turbo` |
| `iXtal26 (machine de reference, sans turbo)` | `--model ibmpc` |
| `iXtal26 (64 Ko, demarrage court)` | `--config ixtal26.cfg --ram 64` |
| `iXtal26 (diagnostic)` | `--config ixtal26.cfg --turbo --verbose` |
| `iXtal26 (IBM XT 5160)` | `--config ixtal26-xt.cfg --turbo` |
| `iXtal26 (IBM XT 5160, ROM BASIC)` | `--model ibmxt --turbo` |

`--model ibmpc` dans le profil de référence est un **no-op volontaire** : `ibmpc` est
déjà le défaut, et le nommer explicitement dit à l'émulateur que la machine est choisie,
donc que l'écran de construction n'a pas à s'ouvrir. Sans lui, ce profil montrerait
l'écran au lieu d'amorcer la machine que `../VERIFICATION.md` décrit.

Les deux derniers montrent les **deux façons** de choisir une machine. Le second n'a pas
de `--config` : sans fichier, aucune disquette n'est montée, donc le XT tombe sur **sa**
ROM BASIC — celle contenue dans `xt.rom`, là où le 5150 charge quatre fichiers
`basicc11.*`.

**La barre de titre nomme la machine** (`iXtal26 - [8088] IBM XT`), et `--verbose`
l'imprime en tête. Avant M11.1 le titre portait « IBM PC 5150 » en dur : un XT qui
tournait affichait 5150, ce qui est le seul endroit où l'on regarde.

**Un profil ne s'applique qu'à un lancement SANS arguments.** Dès qu'on passe `-- …`,
`dotnet run` remplace les arguments du profil par les vôtres : toutes les commandes de ce
fichier et de `../VERIFICATION.md` gardent donc exactement leur comportement.

Les chemins de `--config`, `--floppy-a` et de la clé `disc_a` sont résolus d'abord
relativement au répertoire courant, sinon en remontant depuis le binaire. C'est ce qui
fait qu'un lancement depuis Rider — qui part de `bin/Debug/net10.0/` — trouve les mêmes
fichiers qu'un lancement depuis la racine du dépôt.

**Précédence : défauts, puis `--config`, puis `--ram` / `--drive-a` / `--drive-b`.** Sans
`--config` aucun fichier n'est lu — la machine est alors celle que décrit
`../VERIFICATION.md` : 640 Ko, deux lecteurs 5,25" DD, CGA. Ce défaut n'est pas un
réglage : c'est ce qui garde reproductibles toutes les mesures déjà consignées.

Sur le **5150**, la taille mémoire *est* l'interrupteur SW2 que lit le POST, et le test
mémoire est linéaire : 64 Ko amorce en 12 s, 640 Ko en 52 s (§ M8). Un 5150 à 64 Ko démarre
donc vite sans turbo — c'est la machine d'époque avec la RAM que la plupart avaient.

Sur le **XT**, non : la lecture du port 0x62 ne porte pas la taille mémoire, et le BIOS du
5160 la détermine en balayant. Le couplage `mem_size` ↔ durée d'amorçage ne s'y transfère
pas (§ M10).

Chaque paramètre configurable est réglable **à l'identique côté oracle C**, par un
`h_set_*` appelé avant `h_boot` : `boot-diff --config FICHIER` compare donc la machine
demandée, pas une autre. C'est la raison d'être du fichier plutôt que d'une interface —
une UI serait la seule source de vérité que l'oracle ne peut pas lire.

## Le disque dur du XT

C'est ce que veut dire le « XT » : le 5160 est le premier IBM PC livré avec un disque fixe
dans sa configuration standard. Le contrôleur n'est pas sur la carte mère — c'est l'**IBM
Fixed Disk Adapter**, de conception Xebec, une carte avec sa propre ROM d'extension en
0xC8000 qui apporte l'INT 13h du disque fixe. Le BIOS du 5160 n'en contient pas une ligne.

**Le plus court pour monter un disque** est l'option, qui ne demande aucun fichier de
configuration :

```bash
dotnet run -- --model ibmxt --hdd os/mon-disque.img      # C:
dotnet run -- --model ibmxt --hdd IMG --hdd-d AUTRE.img  # et D:
```

La **géométrie se déduit de la taille du fichier** — c'est `check_hd_type`, que PCem
applique au même endroit, après son sélecteur de fichiers — et la carte `mfm_xebec` est
posée si aucune configuration n'en a nommé. Une taille qui ne correspond à aucun des
46 types du BIOS est **refusée** plutôt que repliée sur 63 secteurs, que nos deux cartes
ne savent pas adresser.

Un cas mérite l'attention : **21 307 392 octets, c'est le type 13 (306 × 8) *ou* le type 16
(612 × 4)**, et le Fixed Disk Adapter accepte les deux. La taille ne tranche pas. L'option
le dit et retient le type 13, comme PCem ; `--hdd-type 16` impose l'autre. Se tromper garde
la bonne capacité et décale l'adressage CHS, donc le système de fichiers se lit de travers
sans qu'aucune erreur n'apparaisse.

Pour une machine qu'on relance souvent, les clés d'un fichier de configuration restent plus
pratiques. Elles ne sont **pas** actives par défaut : la ROM de la carte change la
trajectoire du POST, et `os/` n'est pas versionné. Quatre clés l'allument, toutes
documentées dans `../ixtal26-xt.cfg` :

```ini
hdd_controller = mfm_xebec   # ou dtc5150x, la carte DTC 5150X, transcrite aussi
hdc_sectors = 17             # la carte n'accepte QUE 17
hdc_heads = 4
hdc_cylinders = 306          # 306x4x17 = 10 653 696 o, le 10 Mo historique
hdc_fn = os/xt-10mo.img      # cree s'il manque, et non pre-alloue
```

**La géométrie n'est pas libre** : `xebec_set_switches` n'admet que 17 secteurs par piste
et quatre couples (cylindres, têtes) — (306,4), (612,4), (615,4) et (306,8). Hors de là, la
carte se contente d'un avertissement, annonce le disque en type 0 et le POST diverge.

Il n'y a donc pas à calculer une taille à la main :

```bash
dotnet run -- --create-hdd          # liste les 46 types de disque du BIOS
dotnet run -- --create-hdd 1        # cree os/vierge-hdd-type01.img, le 10 Mo du XT
dotnet run -- --create-hdd 1 CHEMIN # ou le fichier de votre choix
```

L'image est faite de **zéros, à la taille exacte de la géométrie**, et la commande imprime
les quatre clés à coller. Le listing marque d'une étoile les géométries que la carte
accepte — **six** des quarante-six types, la table du BIOS ayant des doublons. `TYPE` peut
aussi s'écrire `CYL,TETES,SECT` pour la saisie libre, bornée comme chez PCem. Un fichier
existant est refusé, jamais écrasé. La même chose est au menu Ctrl+F12, sous
« Creer un disque dur vierge... ».

Sans cela, pointer `hdc_fn` vers un chemin libre marche quand même — `hdd_load_ext` crée le
fichier — mais il fait alors **zéro octet** et n'est jamais pré-alloué : lire un secteur
jamais écrit rend le contenu résiduel du tampon de la carte, pas des zéros. C'est
reproductible, donc le diff reste vert, mais le point de départ n'est plus connu.

Et les préfixes de clés sont des **lettres de lecteur DOS**, pas des numéros de
contrôleur : `hdc_` = C:, `hdd_` = D:, jusqu'à `hdi_` = I:. La clé `hdd_controller`
ci-dessus n'a rien à voir avec les `hdd_` de géométrie — collision de préfixe héritée de
PCem.

Le disque créé est vierge au sens fort — tous ses secteurs existent et valent zéro. Il se
prépare ensuite comme en 1983, sous PC DOS 2.00 :

```
FDISK          -> 1 (Create DOS Partition), puis Entree ; la machine redemarre
FORMAT C: /S   -> « Format complete / System transferred »
```

après quoi la machine amorce sur C:, disquette retirée. **Cet arc entier est vert au diff
contre le C de PCem** — 98 945 755 instructions identiques pour le partitionnement et le
formatage, 25 941 449 pour l'amorçage sur C: qui suit, et l'image de disque comparée octet
par octet entre les deux côtés, pas seulement la trace d'instructions
(`../VERIFICATION.md` § M12).

## Le menu, Ctrl+F12

Dans la fenêtre, **Ctrl+F12** ouvre un menu en surimpression et met la machine en pause :
insérer une image dans A: ou B: (liste des `.img`/`.ima`/`.360`/`.xdf` trouvés sous `os/`,
plus un « Parcourir... » qui ouvre le sélecteur du système), éjecter, réinitialiser. Ce sont
les commandes que PCem porte dans son menu wxWidgets (`wx-sdl2.c:725-770`).

**« Creer une disquette vierge... »** fabrique une image neuve dans `os/` et l'insère dans
A: dans la foulée. Quatre tailles, les seules que le lecteur 5,25" DD du 5150 sache lire :
160, 180, 320 et 360 Ko. Le nom est automatique (`vierge-360k.img`, puis `-2`, `-3`…) :
jamais d'écrasement.

Attention à deux choses. **La taille choisie est le format que l'image acceptera** —
`img_load` fige la géométrie sur la taille du fichier, et `img_writeback` calcule ensuite
ses offsets dessus. Et **un `.img` ne peut pas représenter un support non formaté** : le
format *est* la suite des données de secteurs, sans marques d'adresse. Ce que vous obtenez
est l'équivalent d'une disquette formatée bas niveau et logiquement vide — tous les secteurs
existent et se lisent, leur contenu est nul. Il faut passer `FORMAT` sous DOS pour lui
donner son BPB et sa FAT ; c'est à ce moment, et seulement là, que les `0xF6` apparaissent,
écrits par l'invité. C'est exactement ce que fait PCem.

**« Creer une disquette formatee... »** fait l'autre moitié : elle pose un système de
fichiers FAT12 complet — secteur d'amorce, BPB, deux FAT, répertoire racine — et l'image
est utilisable telle quelle, sans passer `FORMAT`. Mêmes quatre tailles. Contrairement à
la précédente, **elle n'est pas insérée dans A:** : la suite naturelle est d'y déposer un
fichier, et cela est refusé sur une image montée (voir ci-dessous).

**« Deposer un fichier de l'hote... »** demande d'abord l'image, puis ouvre le sélecteur
du système pour choisir n'importe quel fichier du disque Linux, et l'écrit dans la racine
de la disquette. Si l'image est montée dans A: ou B:, le dépôt est **refusé** et non
contourné : `img_seek` tient une piste entière en cache qu'`img_writeback` réécrit, et
au-dessus DOS garde sa propre copie de la FAT et du répertoire. Éjecter, écrire dans son
dos, réinsérer — et DOS réécrit sa FAT périmée, effaçant l'entrée qu'on vient de poser.
« Ejecter A: » est deux lignes plus haut.

L'insertion est immédiate et ne demande aucun reset : DOS voit le changement par la ligne
DSKCHG. Le reset matériel, lui, relit `discfns[]` — donc la disquette en place au moment du
reset est celle sur laquelle le BIOS amorce. Deux resets sont offerts parce qu'ils ne
coûtent pas la même chose : le reset matériel repart à froid, avec les 46 s de test mémoire
(d'où la variante « + turbo », qui les déroule en quelques secondes murales), là où
Ctrl+Alt+Suppr est un redémarrage à chaud que le BIOS expédie sans retester la mémoire.

Le menu est inerte sous `--slices`, pour la même raison que `--turbo` y est refusé : ce
mode existe pour que deux exécutions traversent les mêmes états.

`dotnet run -- --help` donne la table complète.

## Fichiers

| Fichier               | Rôle                                                          |
|-----------------------|---------------------------------------------------------------|
| `Program.cs`          | Point d'entrée, table d'arguments                             |
| `Host/SdlHost.cs`     | Fenêtre, texture, accumulateur horloge murale, remontée du blit |
| `Host/SdlKeyboard.cs` | Scancodes SDL → PC/XT jeu 1, vers `keyboard.rawinputkey`       |
| `Host/SdlMenu.cs`     | Menu Ctrl+F12 : disquettes et reset (`wx-sdl2.c:725-770`)      |
| `Host/SdlSetup.cs`    | Écran de construction de machine (`wx-config_sel.c`)           |
| `Host/HddImage.cs`    | Images de disque dur : les 46 types du BIOS, créer, deviner    |
| `Host/FatImage.cs`    | Disquettes FAT12 : formater, déposer un fichier de l'hôte      |
| `BootTest.cs`         | Amorçage console : BDA, écran texte CGA, état du framebuffer   |

Tout le reste (`Cpu/`, `Memory/`, `Models/`, `Video/`, `Keyboard/`, `Floppy/`, `Disc/`, `Mfm/`, `Sound/`,
`pc.cs`, `io.cs`, `timer.cs`, `ppi.cs`) est du code **transcrit** : identifiants et commentaires anglais de
PCem conservés, une ligne `// pcem:` par fonction. Les règles sont dans
`../TRANSCRIPTION.md`, la correspondance fichier à fichier dans `../oracle.tsv`.

## ROMs

Matériel IBM sous copyright : non distribué, `.gitignore`d. Attendu dans `roms/` :
`ibmpc/pc102782.bin` (BIOS 8 Ko) et, pour la ROM BASIC, `ibmpc/basicc11.f6/.f8/.fa/.fc`.
`mda.rom` fournit la police 8×8 du CGA, que `loadbios` charge inconditionnellement.
Les empreintes attendues sont dans `../roms/roms.sha256`.

## Faire entrer un fichier du disque Linux

En ligne de commande, deux étapes, qui n'allument pas la machine :

```
dotnet run -- --create-floppy                       # liste les quatre formats, ne crée rien
dotnet run -- --create-floppy 360k os/travail.img   # une disquette FAT12 vide, prête à l'emploi
dotnet run -- --floppy-put os/travail.img ~/PROG.COM ~/LISEZ.TXT
dotnet run -- --boot roms 6500 --floppy-a os/pcdos20/pcdos20b.img --floppy-b os/travail.img \
              --type "" --type "" --type "DIR B:"
```

La différence avec « Creer une disquette vierge » tient en une phrase : celle-là écrit des
zéros que DOS ne sait pas lire tant que `FORMAT` n'est pas passé **dans** la machine ;
`--create-floppy` pose le système de fichiers depuis l'hôte. Les quatre BPB ne sont pas
reconstruits de mémoire — ils sont **lus dans le `FORMAT.COM` de PC DOS 2.00**, à l'offset
0xC7FA de `os/pcdos20/pcdos20b.img`, et les deux que le dépôt peut recouper reproduisent
octet pour octet les disquettes qu'il porte (VERIFICATION.md § M14).

Les noms doivent tenir en **8.3**. Un nom trop long est **refusé, jamais tronqué** :
tronquer `rapport-annuel.txt` et `rapport-mensuel.txt` en `RAPPORT.TXT` fabriquerait un
doublon silencieux. Sont refusés de la même façon les caractères hors du jeu DOS, les noms
de périphériques (`CON`, `PRN`, `LPT1`…), les répertoires, un horodatage hors 1980-2107, un
doublon, une racine pleine et le manque de place — chacun avec un message qui **nomme la
règle**. Aucun octet n'est écrit avant que tous les fichiers de la ligne soient acceptés.

La disquette produite est **non système**. Amorcer dessus affiche `Disquette non systeme`
et attend une touche, en boucle — comme le vrai chargeur d'IBM. Pour la rendre amorçable,
`SYS B:` depuis DOS.

`--fat-check` et `--menu-check` sont les auto-contrôles : l'empaquetage FAT12 contre la FAT
d'une disquette réellement formatée par DOS 2.00, le secteur d'amorce, la conversion de
noms, le refus d'image montée, l'invariant de géométrie entre les deux branches
d'`img_load`, et les chemins clavier du menu.

## Images de disquette et de disque dur

Logiciels sous copyright (PC DOS…) : non distribués, `.gitignore`d comme les ROMs. Seul
`../os/os.sha256` est versionné ; il ancre les mesures de VERIFICATION.md § M6, faites
sur `os/pcdos20/pcdos20b.img` (PC DOS 2.00, disquette système, 180 Ko simple face).

Les images de disque dur non plus ne sont pas versionnées. **Elles dérivent** : une image
montée en lecture-écriture est écrite pour de vrai, et une campagne qui la reformate part
de ce que la précédente y a laissé. C'est vrai des disquettes aussi — le `vierge-360k.img`
de `os/` n'est vierge qu'une fois. Toute mesure d'un chemin d'écriture doit donc repartir
d'une image fraîche, sans quoi elle compte les octets qui ont changé depuis la fois d'avant
et non depuis le vide (VERIFICATION.md § M12).

## SDL3 : d'où viennent les binaires

- `SDL3-CS` : les bindings managés (API idiomatique — `SDL.Init`, `SDL.CreateWindowAndRenderer`…).
- `SDL3-CS.Linux` / `.Windows` / `.MacOS` : les bibliothèques **natives**, livrées par NuGet.
  Rien à installer sur la machine : elles sont copiées dans
  `bin/Debug/net10.0/runtimes/<rid>/native/` et chargées automatiquement.

On peut ne garder que la ligne de son OS dans le `.csproj` pour alléger `bin/` ; les trois sont
nécessaires pour publier vers une autre plateforme (`dotnet publish -r win-x64`, etc.).

**Dépannage (Linux)** — si le chargement de `libSDL3.so` échoue (dépendances Wayland/X11 du système) :

```bash
sudo apt install libsdl3-dev     # installe SDL3 + le symlink libSDL3.so
```

puis supprimer les `PackageReference` `SDL3-CS.*` natifs du `.csproj` : le SDL3 du système prend le
relais. Attention, sans le paquet `-dev` le système n'expose que `libSDL3.so.0`, que .NET ne sait pas
résoudre — c'est justement pourquoi les natifs NuGet sont le choix par défaut.

Documentation des bindings : <https://github.com/edwardgushchin/SDL3-CS> ·
documentation SDL3 : <https://wiki.libsdl.org/SDL3/>
