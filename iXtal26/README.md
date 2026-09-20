# iXtal26

Émulateur d'**IBM PC 5150** (8088 à 4,77 MHz, CGA, contrôleur de disquettes), transcrit
de PCem v18 en C#.

Le cœur est vérifié bit à bit contre le C d'origine, gardé dans `pcem-dev/` : les deux
exécutent le POST du 5150 puis la ROM BASIC sur **25 457 269 instructions identiques**,
et l'amorçage de PC DOS 2.00 depuis une disquette sur **26 750 702**, registres, segments,
drapeaux et modèle de temps compris. Voir `../VERIFICATION.md`.

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

Le titre de la fenêtre affiche la vitesse en pourcentage du temps réel, sur une
fenêtre glissante — 100 % signifie que les 4,77 MHz sont tenus.

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
```

`--headless` n'est pas un raccourci de test : c'est la ligne architecturale qui garantit
que le cœur ne dépend pas du front-end. `--slices` est l'interrupteur de déterminisme —
sans cadencement horloge murale, deux exécutions donnent le même nombre de cycles.

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
| `BootTest.cs`         | Amorçage console : BDA, écran texte CGA, état du framebuffer   |

Tout le reste (`Cpu/`, `Memory/`, `Models/`, `Video/`, `Keyboard/`, `Floppy/`, `Disc/`,
`pc.cs`, `io.cs`, `timer.cs`, `ppi.cs`) est du code **transcrit** : identifiants et commentaires anglais de
PCem conservés, une ligne `// pcem:` par fonction. Les règles sont dans
`../TRANSCRIPTION.md`, la correspondance fichier à fichier dans `../oracle.tsv`.

## ROMs

Matériel IBM sous copyright : non distribué, `.gitignore`d. Attendu dans `roms/` :
`ibmpc/pc102782.bin` (BIOS 8 Ko) et, pour la ROM BASIC, `ibmpc/basicc11.f6/.f8/.fa/.fc`.
`mda.rom` fournit la police 8×8 du CGA, que `loadbios` charge inconditionnellement.
Les empreintes attendues sont dans `../roms/roms.sha256`.

## Images de disquette

Logiciels sous copyright (PC DOS…) : non distribués, `.gitignore`d comme les ROMs. Seul
`../os/os.sha256` est versionné ; il ancre les mesures de VERIFICATION.md § M6, faites
sur `os/pcdos20/pcdos20b.img` (PC DOS 2.00, disquette système, 180 Ko simple face).

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
