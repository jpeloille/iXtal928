# La ligne de commande : les justifications

`Program.cs` et `Host/CommandLine/` ne portent aucun commentaire : leurs noms disent *ce que*
fait le code. Le *pourquoi* est ici, rangé option par option. Les textes sont ceux des
commentaires d'origine, retirés du code à la réécriture du 26/09/2026.

Où chercher dans le code :

| Sujet | Type |
|---|---|
| Boucle principale, lancement de la fenêtre | `Launcher` |
| État de la ligne de commande quand une machine est lancée | `MachineRunOptions` |
| Surcharges de la machine, partagées par la fenêtre, `--boot` et `--timer-check` | `MachineOverrides` |
| Montage des disquettes et des disques durs | `DriveMounter` |
| Verbes qui rendent la main sans ouvrir de fenêtre | `BootVerb`, `TimerCheckVerb`, `CreateHardDiskVerb`, `CreateFloppyVerb`, `FloppyPutVerb`, `MakeNvrVerb` |

---

## Précédence et sensibilité à l'ordre

**Défauts, puis fichier, puis ligne de commande, dans cet ordre, et AVANT initpc.** C'est la
séquence de pc_main, qui appelle loadconfig() puis initpc() (wx-sdl2.c:451, :459). Sans
`--config` on ne lit aucun fichier : un lancement nu reste un lancement nu, et rend la
machine que VERIFICATION.md décrit.

**Les surcharges valent « non demandé » (`null`) tant que la ligne de commande n'a rien
dit**, pour distinguer « non demandé » de « demandé à 0 ». 0 est un type de lecteur
légitime : « aucun lecteur ».

**`--hdd` et `--hdd-d` sont COLLECTÉS et non appliqués sur place**, comme `--model` et
`--ram`. loadconfig écrase ide_fn[] et hdc[] SANS condition (pc.cs), alors qu'il saute
disc_a quand discfns[] est déjà rempli. La ligne de commande doit l'emporter sur le
fichier, donc elle s'applique après lui.

**`--model` AVANT `--ram`** : c'est le modèle qui porte les bornes mémoire, et le XT est à
64 Ko de granularité là où le 5150 est à 32. Dans l'autre ordre, `--model ibmxt --ram 96`
passerait le contrôle du 5150 puis monterait une machine que PCem ne décrit pas.

**`--ram` est refusé, pas corrigé** : une taille tapée en ligne de commande est explicite.
Avant M10 cette affectation ne passait par AUCUN contrôle — le seul chemin du dépôt qui
pouvait fabriquer un SW2 absurde sans rien dire.

**Le processeur se juge contre la machine FINALE** — `--config`, puis `--model`, puis
`--cpu` — et sort en 2 comme toute option refusée. Pas quand l'écran de construction
s'ouvre : il ramène lui-même l'indice dans la table (SdlSetup.ClampCpu).

**Le budget de turbo vit dans SdlHost** (`SdlHost.DefaultTurboSlices`) : le menu Ctrl+F12
s'en sert aussi pour le réarmer après un reset, et deux 5800 dans l'arbre finiraient par
diverger.

**Le répertoire de ROM se résout dans le lanceur, et pas dans initpc** : c'est l'hôte qui
sait d'où il a été lancé, et initpc ne fait que consommer le chemin qu'on lui tend. Sans
cela, « roms » était interprété depuis le répertoire courant — bin/Debug/net10.0/ sous
Rider, où il n'existe pas — et l'amorçage échouait selon l'endroit d'où on lançait le
binaire. Voir paths.resolve_roms_path.

**Pas de try/catch autour de `host.Run()`** : pc.fatal() lève, et une trace d'exception est
précisément le signal que le cœur est fait pour émettre. L'étouffer ici le perdrait.

**Renoncer n'est pas échouer** : quitter l'écran de construction sort par 0.

## Deux politiques de lecture anticipée

Elles sont distinctes, et il faut les garder ainsi :

- **Les positionnels de `--boot` et de `--timer-check`, et les options qui suivent un verbe,
  s'arrêtent à `--`** (`ArgumentCursor.NextIsPositional`, `NextIsOption`). Jusqu'au
  26/09/2026, les positionnels avalaient l'argument suivant quel qu'il soit :
  `--timer-check --model ibmat`, la forme que citent PLAN-286.md et VERIFICATION.md,
  prenait « --model » pour le répertoire de ROM et sortait en 2. Le test `--` et non `-`
  garde le refus d'un nombre négatif : `--boot roms -5` est toujours rejeté.
- **`--turbo`, `--make-nvr`, `--create-hdd`, `--create-floppy` et `--floppy-put` s'arrêtent au
  premier `-`** (`ArgumentCursor.NextIsValue`). Un nombre ne peut pas commencer par un
  tiret : ce test distingue « --turbo 3000 » de « --turbo --verbose » sans consommer
  l'option suivante. Sans lui, « --create-hdd 1 -v » avalerait -v comme chemin et créerait
  un fichier nommé « -v ». Aucun type ni géométrie valide ne commence par un tiret.

## Options du mode fenêtre

### `--config CHEMIN`

Le fichier de configuration machine, comme PCem (pc.c:211-226). Les options machine le
surchargent.

Un chemin introuvable est **refusé et non ignoré** : un chemin mal tapé donnerait tous les
défauts en silence, donc une machine autre que celle demandée. C'est la même politique que
pour les disquettes et que le contrôle de répertoire de ROM d'initpc. Le chemin se résout
comme `--rom-path` et `--floppy-a` : tel quel depuis le répertoire courant, sinon en
remontant depuis le binaire. Sans cela « --config ixtal26.cfg » marcherait depuis la racine
du dépôt et pas depuis Rider, qui lance depuis bin/Debug/net10.0 : l'échec dépendrait de
l'endroit d'où on lance.

### `--floppy-a CHEMIN`, `--floppy-b CHEMIN`

Image .img montée dans le lecteur avant l'amorçage. C'est le pendant de
« --load_drive_a » de PCem (pc.c:227-233), qui remplit discfns[] AVANT initpc pour que
resetpchard la charge (pc.c:367). Le BIOS du 5150 amorce alors dessus au lieu de basculer
sur la ROM BASIC.

La remontée vit dans paths.resolve_file_path, pour qu'il n'existe qu'UNE politique de
résolution de fichier : la clé disc_a d'un fichier de configuration doit se comporter
exactement comme `--floppy-a`, sans quoi la même image marche par un chemin et pas par
l'autre. Une image absente est refusée ICI, avec son chemin. disc_load, lui, se tairait et
laisserait le lecteur vide : le BIOS irait sur BASIC et rien ne dirait pourquoi.

### `--hdd CHEMIN`, `--hdd-d CHEMIN`

Monte un DISQUE DUR qui existe déjà. Avant M13, il n'y avait aucun chemin pour cela :
`--floppy-a` existait, et le disque dur n'était atteignable que par `--config`.

La géométrie n'est pas demandée : elle se DÉDUIT de la taille du fichier, par la branche
MFM de check_hd_type, que PCem applique au même endroit, après son sélecteur de fichiers
(wx-config.c:2085).

Trois écarts avec le montage d'une disquette, tous imposés par le matériel :

1. La géométrie d'une disquette se déduit de la taille PAR LE CŒUR, dans img_load
   (disc_img.cs). Celle d'un disque dur vient de la CONFIGURATION — hdd_load_ext pose
   spt/hpc/tracks depuis ses paramètres. C'est donc ici qu'il faut la calculer, et c'est
   check_hd_type qui le fait chez PCem.
2. Une taille qui ne correspond à aucun type est REFUSÉE. Le repli 63/16 du C
   (wx-config.c:1355-1357) existe pour les contrôleurs IDE, que ce dépôt n'a pas. Les deux
   cartes transcrites câblent 17 secteurs dans leur ADRESSAGE (xebec_get_sector), donc un
   disque à 63 secteurs serait annoncé sans être adressable. Mieux vaut le dire que
   produire une machine qui diverge au POST.
3. Le contrôleur de la machine est posé si rien n'en a nommé : `mfm_at` sur un AT,
   `mfm_xebec` ailleurs (`HardDiskControllers.DefaultForCurrentMachine`). Sans
   contrôleur, une image montée n'est vue par personne. Sur l'AT, `mfm_at` prend la
   géométrie dans le CMOS : `nvr/default/at.nvr` déclare le type 0, donc le disque
   n'apparaît qu'avec un CMOS fabriqué par `--make-nvr`. Mesuré le 26/09/2026, le Xebec
   ne faisait pas mieux sur l'`ibmat` (0 disque au BDA 0040:0075, dans les deux cas).

L'avertissement « le Fixed Disk Adapter n'accepte pas C x H » ne concerne que le Xebec. Il
ne sort plus sous `dtc5150x` ni sous `mfm_at`, qui acceptent toutes les géométries de la
table.

Un type imposé l'emporte, mais seulement s'il décrit BIEN ce fichier. Accepter
« --hdd-type 16 » sur une image de 10 Mo monterait un disque deux fois trop grand, dont la
moitié n'existe pas.

La taille ne suffit pas toujours à nommer un type, et il faut le DIRE : sept tailles de la
table en désignent plusieurs. Pour l'une d'elles, les deux candidats ont une géométrie
DIFFÉRENTE et sont tous deux acceptés par le Fixed Disk Adapter : 21 307 392 octets, c'est
le type 13 (306 x 8) ou le type 16 (612 x 4). Se tromper garde la bonne capacité et change
l'adressage CHS, donc le système de fichiers se lit de travers sans qu'aucune erreur
n'apparaisse.

Avant le montage, il faut aussi savoir si la carte accepte cette géométrie. Sans cet
avertissement, une image de taille valide mais de géométrie inconnue de la carte donne un
POST qui diverge, et xebec_set_switches se contente d'un warning() que personne ne lit.

### `--hdd-controller NOM`

Choisit le contrôleur de disque dur parmi ceux de la machine : `mfm_xebec` et `dtc5150x`
partout, et `mfm_at` sur les seules machines AT. C'est le filtre DEVICE_AT/MODEL_AT de
PCem (wx-config.c:237-242), et la liste est celle de l'écran de construction
(`Host/HardDiskControllers.cs`). Un nom inconnu, ou absent de la machine, est refusé en
listant ceux qui existent. L'option s'applique après `--config` et `--model`, comme
`--gfxcard`, et l'emporte sur la clé `hdd_controller`.

### `--hdd-type N`, `--hdd-d-type N`

Tranchent une taille ambiguë. PCem n'a pas besoin de cela : son hd_file montre la
géométrie déduite dans un dialogue et laisse la corriger avant de l'appliquer
(wx-config.c:2085-2088). Une ligne de commande n'a pas ce dialogue.

### `--cpu N`

L'INDICE du processeur dans la table de la machine (M16). 5 désigne un 286/20 sur
l'ami286, un 8088/16 sur le 5150, et n'existe pas sur l'ibmat (cpus_ibmat). L'indice est
collecté, appliqué APRÈS `--model`, et jugé contre la machine finale par pc.check_cpu.

### `--slices N`

Zéro est refusé, pour que « `SliceLimit` renseigné » signifie exactement « --slices a été
donné » : c'est ce test, et lui seul, qui autorise `--headless`.

`--headless` sans `--slices` est vérifié après la boucle, pour que l'ordre des deux options
n'ait pas d'importance. Sans borne, une exécution sans fenêtre n'a plus rien pour
l'arrêter : ni Quit SDL, ni Échap.

### `--turbo [N]`

N'attend pas l'horloge murale pendant les N premières tranches, puis rend la machine au
temps réel. La trajectoire ÉMULÉE est rigoureusement la même : mêmes instructions, mêmes
cycles, même écran. Seule change la vitesse à laquelle l'hôte la déroule. Le test mémoire
de 640 Ko dure 46 s, et c'est authentique (§ M4.6) ; le regarder passer est un choix, pas
une obligation.

`--turbo` avec `--slices` est **refusé plutôt qu'ignoré** : `--slices N` ne cadence RIEN,
de la première tranche à la dernière (c'est ce qui rend deux exécutions identiques).
Accepter `--turbo` à côté donnerait une option sans effet, indiscernable d'une option qui
ne marche pas.

### `--monitor`, `--host-diagonal`, `--pixel-mm` et `--crt`

Quatre options d'**affichage seul** : elles agissent sur la fenêtre et le renderer, jamais sur
`video.Buffer32`. `--boot`, boot-diff et les empreintes de framebuffer n'en voient rien.

`--monitor` simule un moniteur d'époque : toute trame, 640×480 comme 1024×768 ou le texte
720×400, remplit la même surface 4:3, celle du tube, diagonale visible décomposée en 3-4-5.

- `nec3v` : NEC MultiSync 3V (JC-1535VMA, 1994), 15" nominal, 14" visibles, soit
  284,5 × 213,4 mm, 31-50 kHz, 55-90 Hz (crtdatabase.com). Il **respecte ses capacités** :
  un signal hors plage donne un écran noir et le message du moniteur. La CGA (15,7 kHz) et
  l'EGA (21,8 kHz) en sont exclues, le 1024×768 à 60 Hz (48,4 kHz) passe. Les fréquences
  viennent du timer de la carte (`dispontime + dispofftime`, `vtotal`) ; la CGA, sans accès
  statique, est tenue pour 15,70 kHz / 59,92 Hz. PCem ajoute 6 au CRTC 0 au lieu de 5
  (`vid_svga.cs:589`) : la VGA mesure 31,16 kHz au lieu de 31,47, encore dans la plage.
- `14`, `15`, `17` : génériques, diagonale × 0,93 visible (le ratio NEC, clé
  `visible_fraction`), acceptent tout signal.
- `auto` (défaut) : le 3V derrière une VGA ou une Trident, un générique 14" derrière la
  CGA, que le 3V ne synchronise pas.
- `entier` : pas de moniteur, pixels entiers. Filtrage linéaire par défaut (`scale_mode = 1`, la clé et le sens
de PCem), plus proche voisin au choix. La fenêtre ne suit plus les changements de mode de
l'invité, comme un CRT. `--monitor entier` revient aux pixels entiers : facteur
round(0,42 / MM), proportions de la trame, plus proche voisin.

La taille d'un pixel de la dalle hôte : SDL3 connaît la résolution native
(`GetDesktopDisplayMode`) mais pas la taille physique — `GetDisplayContentScale` est le
facteur de l'OS (125 %, Retina), pas un pas de pixel. On la dit donc : `--host-diagonal 27`
(pas = diagonale / √(largeur² + hauteur²), 0,2335 mm pour un 27" en 2560 × 1440), ou
directement `--pixel-mm`, qui l'emporte. Sans l'un ni l'autre : 0,2331. Au-delà de 2 mm,
la valeur est refusée : c'est une faute de frappe, pas un écran. La fenêtre est réduite si
elle dépasse le bureau.

La fenêtre est créée en `HighPixelDensity` : sous HiDPI (Wayland fractionnaire, Retina) le
renderer reçoit les pixels de la dalle. Sans ce drapeau, le compositeur agrandissait par
dessus notre propre mise à l'échelle.

`--crt` assombrit la moitié basse de chaque ligne émulée (~38 % de noir), à condition
d'avoir au moins deux pixels hôte par ligne : en dessous ce ne serait que du moiré, et le
menu affiche « trop fines ici ».

**Menu et persistance.** Ctrl+F12 porte « Moniteur » (Entrée ou ←/→), « Lignes CRT » et
« Filtrage ». Les réglages vivent dans la section `[SDL2]` du .cfg machine (`monitor`, `crt`,
`scale_mode`, `visible_fraction`, `pixel_mm`, `host_diagonal` ; `monitor` vaut `auto`,
`nec3v`, `14`, `15`, `17` ou `entier`), lus après `--config` ou après l'écran de construction. Précédence : défauts,
puis config, puis ligne de commande. Ils ne sont **réécrits que dans `configs/`** :
`config_save` réémet l'arbre sans ses commentaires, et un `ixtal26.cfg` documenté à la main
les perdrait. Hors de `configs/`, le menu dit « pour cette session ».

### `--verbose`

Sans compteur de blits, une exécution muette ne distingue pas « le CGA a balayé et l'hôte
a téléversé » de « rien n'est jamais arrivé à la texture ». Dans les deux cas la fenêtre
est noire et le code de sortie vaut 0.

La ligne de diagnostic nomme la MACHINE en tête, comme BootDiff le fait depuis M10 :
depuis qu'il y en a deux, une ligne qui ne la nomme pas laisse croire qu'il n'y en a
qu'une.

### `--setup` et l'écran de construction

C'est le pendant du Configuration Manager de PCem, que `config_override` saute quand
`--config` est passé (wx-sdl2.c:481-488). Ici, l'écran s'ouvre tout seul sur un lancement
NU, et `--setup` le force.

L'ÉCRAN S'OUVRE SUR UN LANCEMENT NU, et seulement là. Dès qu'un argument décrit la
machine, on la monte telle qu'il l'a dite : toutes les recettes de VERIFICATION.md et les
profils de Rider gardent leur comportement au cycle près. `--setup` passe outre.

`--gfxcard` et `--hdd-controller` seuls ne comptent pas comme une machine choisie : c'est
l'utilisateur qui choisit la carte graphique et le contrôleur de sa machine. L'écran
s'ouvre donc avec ce choix déjà sélectionné, et le reste à composer. Décision de Julien,
26/09/2026.

`--slices` et `--headless` ne le voient jamais non plus. Le premier existe pour que deux
exécutions traversent les mêmes états, et un écran qui attend une touche n'a pas sa place
dans ce contrat. C'est le même arbitrage que pour le menu Ctrl+F12, inerte sous `--slices`
depuis M7.

### `--frames`

`--frames` n'est pas devenu un synonyme de `--slices` : il a été retiré. Une image CGA dure
1/60 s et une tranche pc.runpc() 10 ms émulées, donc « --frames 60 » aurait voulu dire
600 ms au lieu de 60 images : un compteur qui ment sans prévenir. Le diagnostic reste
explicite parce que README.md cite encore l'option.

## Verbes

### `--boot [CHEMIN] [N]`

Conservé tel quel : VERIFICATION.md invoque « --boot roms 6000 ». Ses deux positionnels
lui sont propres et ignorent `--rom-path`, ce qui garde la commande de vérification
indépendante du reste de la table.

Les deux positionnels sont facultatifs et s'arrêtent à `--` : `--boot --model ibmxt` amorce
sur `roms`, en 20 tranches.

Un nombre mal tapé ne doit PAS retomber sur le défaut. « --boot roms 60O » (lettre O)
exécutait 20 tranches, imprimait un écran vide et sortait 0 : indiscernable d'un cœur qui
n'affiche rien. Et le « if (slices != 20) i++ » d'origine ne consommait même pas
l'argument quand il valait justement 20.

`--type TEXTE` tape la chaîne dans la machine après l'amorçage et revide l'écran. C'est la
seule vérification du chemin clavier qui ne dépende pas d'un gestionnaire de fenêtres.
L'option est répétable : DOS demande la date puis l'heure avant de rendre son invite, donc
« --type "" --type "" --type DIR ».

`--settle N` fixe le nombre de tranches laissées à l'application après chaque Entrée. Le
défaut suffit à un DIR ; un FORMAT 360 Ko en demande ~4 000.

`--model`, `--gfxcard`, `--cpu`, `--hdd`, `--hdd-type` et `--hdd-controller` sont COLLECTÉS, pas appliqués
dans la boucle. Là, les options prennent effet dans l'ordre écrit, et `--config` poserait
alors le modèle après `--model`. La précédence doit être la même qu'en mode fenêtre —
défauts, puis `--config`, puis la ligne de commande — quel que soit l'ordre de frappe.
`--cpu` ne vaut que dans la table de la machine FINALE. `--hdd` doit s'appliquer APRÈS
`--config`, qui écrase ide_fn[] et hdc[] sans condition.

La configuration se lit DANS la boucle du verbe, et pas dans la boucle principale :
`--boot` rend la main avant d'y arriver. `--floppy-a` et `--config` y agissent donc dans
l'ordre écrit.

Le processeur est jugé dans le verbe, contre la machine finale, pour sortir en 2 comme
toute option refusée. initpc le jugerait aussi, mais rendrait 1, le code d'un amorçage raté.

### `--timer-check [CHEMIN] [SECONDES]`

Contrôle de fréquence ABSOLUE. Il a deux positionnels, comme `--boot` et pour la même
raison : la commande doit rester citable telle quelle dans un rapport, sans dépendre du
reste de la table d'options.

300 s émulées par défaut. À 18,2 Hz cela fait ~5 460 tops, et l'alignement sur les fronts
ramène l'incertitude à ±33 ppm : assez pour affirmer quatre chiffres significatifs. Une
seconde n'en donnerait que 18, soit 5,5 % de quantification, et ne prouverait rien.

Les options de la MACHINE suivent le patron de `--boot`, et pour les mêmes raisons. Sans
elles, l'outil ne mesurait que le 5150, le modèle par défaut, et rendait la main avant que
la boucle principale ait appliqué quoi que ce soit.

`--charge` règle la charge de la fenêtre : « repos » (défaut, ce que la machine faisait)
ou « ram », une boucle en RAM, celle qui décide de la marge de l'hôte. `--boot-slices`
fixe les tranches d'amorçage avant la fenêtre : 6 000 amènent le 5150 à l'invite BASIC,
et une machine au POST plus long peut en demander plus.

### `--create-hdd [TYPE|CYL,TETES,SECT] [CHEMIN]`

FABRIQUE une image de disque dur vierge, puis sort. C'est la PREMIÈRE commande du dépôt
qui produit un fichier : `--boot` et `--timer-check` racontent, celle-ci écrit.

Elle existe parce que la géométrie n'est pas libre et que rien, à l'exécution, ne le dit.
Le Fixed Disk Adapter n'accepte que 17 secteurs par piste et quatre couples (cylindres,
têtes). Hors de là, il se contente d'un warning(), annonce le disque en type 0 et laisse le
POST diverger. Voir Host/HddImage.cs.

Sans argument, on liste et on sort SANS RIEN CRÉER. Un défaut implicite qui fabriquerait
10 Mo parce qu'on a tapé la commande pour voir serait exactement le genre de surprise que
ce dépôt refuse ailleurs. Lister est le service le plus utile de cette commande : la table
est longue, ses doublons ne se voient pas, et son entrée 15 est un piège. Le type 15 est
RÉSERVÉ dans la table de l'IBM AT, et PCem le laisse dans sa liste déroulante, où il
affiche « size=0MB ». On le montre aussi, parce que le cacher décalerait les numéros, mais
on dit ce qu'il est.

La saisie libre CYL,TETES,SECT est celle du dialogue de PCem (wx-config.c:1613-1621). Elle
passe par la ligne de commande et pas par le menu : saisir trois nombres dans une
surimpression SDL demanderait un éditeur de texte que le menu n'a pas, puisqu'il n'a
qu'une liste et le sélecteur de fichiers du système.

Sans CHEMIN, le fichier va au premier nom libre dans os/, le MÊME os/ que le menu
Ctrl+F12, par le même ImagesRoot : deux résolutions, ce sont deux répertoires qui finissent
par différer, et une image invisible dans la liste du menu. os/ est dans .gitignore, donc
absent d'un clone neuf, d'où la création du répertoire.

### `--create-floppy [FORMAT] [CHEMIN]`

FABRIQUE une disquette FORMATÉE, puis sort. Sa voisine `--create-hdd`, comme le « Creer
une disquette vierge » du menu, produit un fichier de zéros que DOS ne sait pas lire tant
que FORMAT n'est pas passé DANS la machine. Celle-ci pose le système de fichiers depuis
l'hôte : c'est la moitié qui manquait pour y faire entrer un fichier du disque Linux. Voir
Host/FatImage.cs. Elle a la même forme que `--create-hdd`, et pour les mêmes raisons.

### `--floppy-put IMAGE FICHIER...`

DÉPOSE des fichiers de l'hôte dans une image, puis sort : c'est l'autre moitié. L'image se
résout comme `--floppy-a` et `--config`.

### `--make-nvr CONFIG SORTIE [--force]`

Fabrique le CMOS d'une machine à partir de SON fichier de configuration, et de nulle part
ailleurs : les deux ne peuvent donc pas se contredire. Voir Host/NvrImage.cs, et
nvr/README.md pour la disposition mesurée.

Pourquoi un verbe plutôt que le SETUP ? Le SETUP en ROM d'un BIOS AMI ferait le même
travail, mais le piloter demande d'envoyer Suppr, les flèches et F10 à l'aveugle. Et
l'IBM AT n'a pas de SETUP en ROM du tout : le sien est sur la disquette de diagnostics, que
ce dépôt n'a pas.

### `--setup-check`, `--menu-check`, `--fat-check`

Ce sont des auto-contrôles. Les deux premiers existent parce que ce code n'a aucun autre
moyen d'être exécuté : rien ici ne donne le focus à une fenêtre SDL. `--fat-check` existe
parce qu'un système de fichiers faux ne se voit pas à l'œil : l'image a la bonne taille et
le bon nombre de secteurs, et DOS la lit de travers en silence. Il porte aussi l'invariant
de géométrie, qui ne peut se mesurer qu'en chargeant vraiment une image par les deux
branches d'img_load.

## Sorties console en un bloc

L'aide, la liste des types de disque et celle des formats de disquette s'écrivent en un
seul appel (`StandardOutput.WriteAtOnce`). `Console.Out` vide son tampon à chaque
`WriteLine` : l'aide coûtait 125 appels `write(2)` et en coûte 5 depuis la réécriture
(`strace -c`). Les fins de ligne passent par `ReplaceLineEndings(Environment.NewLine)`
pour rester identiques sous Windows.
