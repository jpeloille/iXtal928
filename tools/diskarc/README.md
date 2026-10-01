# Les arcs d'écriture sur disque dur (G5)

Des lignes KeyScript, une par ligne, à passer en `--type` au boot-diff (ou à `--boot`) :

    mapfile -t L < tools/diskarc/fdisk-format-d.keys; A=(); for l in "${L[@]}"; do A+=(--type "$l"); done
    iXtal26.Diff boot-diff roms 60000 --config CFG --type-at 60000 --type-settle 1500 "${A[@]}"

`fdisk-format-d.keys` : FDISK sur le second disque (partition primaire, taille maximale), sortie
par deux Échap SANS Entrée (suffixe « ^ », KeyScript), une touche pour redémarrer, douze lignes
vides « ^ » d'attente, `FORMAT D: /S`, `Y`, vingt attentes, l'étiquette vide, `DIR D:`. Il faut
C: amorçable (DOS 5, sans `KEYB FR` : KeyScript tape en QWERTY) et D: vierge de type 46, avec le
CMOS qui déclare les deux (`--make-nvr`). Les lignes 7 et 8 portent un octet Échap (0x1B).
Voir VERIFICATION.md § G5.0.

`dos5-install.keys` : l'installation de MS-DOS 5 depuis ses trois disquettes (`os/Dos 5.0/`,
720 Ko) sur un disque dur VIERGE, pour `--boot` seul (les lignes `@wait` et `@A:` sont des
commandes de BootTest, pas des frappes). Disquette 1 en A: au démarrage (`disc_a`), réglage
`--settle 500`. Le SETUP partitionne, redémarre, formate, copie, demande les disquettes 2 et 3,
puis on éjecte et la machine amorce sur C: jusqu'au DOS Shell. Les chemins `@A:` visent des
COPIES dans /tmp/g5w — ne jamais pointer les images de `os/`, que le lecteur ouvre en écriture.
Voir VERIFICATION.md § G5.3.
