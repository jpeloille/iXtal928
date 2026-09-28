# Plan — l'affichage, du pixel entier au tube cathodique

Tout ce qui suit est de l'**hôte** : la fenêtre, le renderer, des textures superposées.
Rien n'écrit dans `video.Buffer32`. Les oracles (`--boot`, boot-diff, empreintes de
framebuffer) restent aveugles à ce plan, et c'est voulu.

Les choix retenus, écran et son, sont consignés dans
[choix-restitution-ecran-et-son.md](choix-restitution-ecran-et-son.md).

## Fait (26/09/2026)

- **NEC MultiSync 3V (auto derrière une VGA/Trident)** — 14" visibles, 284,5 × 213,4 mm ;
  31-50 kHz, 55-90 Hz respectés (`SdlHost.SignalTiming`, écran « hors plage » sinon).
  Génériques 14/15/17" à 93 % visibles, sans limite de fréquence, et 14" derrière la CGA.
- **Moniteur d'époque** — `SdlHost.ComputeRect` / `ResizeWindow` : toute
  trame remplit la surface 4:3 du tube, à sa taille réelle via `--host-diagonal` ou `--pixel-mm`, filtrage
  linéaire ou net (« Filtrage », `scale_mode`). Fenêtre en `HighPixelDensity`. La fenêtre ne suit plus les modes de
  l'invité. Liseré du linéaire évité : `ClearTextureBorder` noircit le texel qui borde la
  trame à chaque changement de taille.
- **Taille d'image (`--fill`, 90 % par défaut)** — les molettes H-SIZE/V-SIZE : l'image
  couvre 70 à 100 % de la surface du tube, marge noire. Menu : ←/→ par 1 %.
- **Pixels entiers (`--monitor entier`)** — facteur entier, proportions de la trame,
  fenêtre au facteur round(0,42 / MM).
- **`--crt`, lignes de balayage** — `SdlHost.DrawScanlines` : deux texels par ligne émulée
  (clair, sombre à ~38 %) étirés sur l'image. Dessinées seulement à ≥ 2 pixels hôte par
  ligne : sur un écran à 0,234 mm, un 15" fait 897 px de haut, donc rien en 480 lignes et
  plus ; visibles en 350/400 lignes et en CGA.
- **Menu Ctrl+F12 et persistance** — `DisplaySettings` : « Moniteur » et « Lignes CRT »,
  clés `[SDL2]` `monitor`/`crt`/`pixel_mm`, réécrites seulement dans `configs/`.

## Reste à faire, du moins cher au plus cher

### 1. Lignes de balayage fidèles au CGA
Le CGA double ses 200 lignes (`vid_cga.cs:491`, `(ysize << 1) + 16`). `--crt` assombrit
chaque ligne de la trame, donc donne 400 lignes visibles là où le moniteur 5153 en
montrait 200. Il faudrait connaître le doublement : un champ posé par la carte vidéo
à côté de `updatewindowsize`, lu par l'hôte. À ne pas deviner depuis la hauteur.

### 2. Masque de phosphore (dot pitch)
Superposer une grille RVB (fentes ou triades) au pas du tube, 0,28 à 0,39 mm, convertie
en pixels hôte par `--pixel-mm`. Même technique que les lignes : une texture répétée,
aucun shader. N'a de sens qu'à partir de ×4 environ, donc sur un écran 4K.
Réglage : `--crt mask` ou `--dot-pitch MM`.

### 3. Détection de la taille de dalle
SDL3 n'expose pas la taille physique : il faut `--host-diagonal`. L'EDID (octets 21-22,
taille en cm, ou les descripteurs détaillés en mm) la donnerait sans rien demander, via
`/sys/class/drm/*/edid` sous Linux. Hors SDL, donc par plateforme.

### 4. Flou du faisceau, halo, courbure
Il faut des shaders : l'API GPU de SDL3 (`SDL_GPU`) ou un renderer à shaders.
C'est un vrai chantier (pipeline GPU, shaders compilés par plateforme). Effets visés :
- flou horizontal léger (le faisceau n'est pas un point) ;
- halo (bloom) autour des zones claires ;
- courbure du tube et coins arrondis ;
- rémanence du phosphore (mélange avec l'image précédente).

## Portes
Affichage seul : `--menu-check` et un lancement fenêtré suffisent. Pas de boot-diff
tant qu'aucun commit ne touche au cœur ni à `Buffer32`.
