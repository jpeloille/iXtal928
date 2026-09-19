# iXtal26

Application SDL3 en C# : fenêtre, renderer, boucle de jeu avec delta-time, clavier et compteur FPS.

## Lancer

```bash
dotnet run
```

Flèches = pousser le carré, Échap ou la croix = quitter.

Pour un lancement non interactif (test, CI) :

```bash
dotnet run -- --frames 60       # 60 images puis sortie avec le code 0
```

## Fichiers

| Fichier      | Rôle                                                              |
|--------------|-------------------------------------------------------------------|
| `Program.cs` | Point d'entrée, arguments de ligne de commande                     |
| `Game.cs`    | Fenêtre, boucle, `Update(dt)` pour la logique, `Render()` pour l'affichage |

La fenêtre fait 1280 × 720 au démarrage et est redimensionnable ; `Update` lit la taille courante
via `SDL.GetRenderOutputSize`, donc le rebond reste correct après un redimensionnement.

## SDL3 : d'où viennent les binaires

- `SDL3-CS` : les bindings managés (API idiomatique — `SDL.Init`, `SDL.CreateWindowAndRenderer`…).
- `SDL3-CS.Linux` / `.Windows` / `.MacOS` : les bibliothèques **natives**, livrées par NuGet.
  Rien à installer sur la machine : elles sont copiées dans
  `bin/Debug/net9.0/runtimes/<rid>/native/` et chargées automatiquement.

On peut ne garder que la ligne de son OS dans le `.csproj` pour alléger `bin/` ; les trois sont
nécessaires pour publier vers une autre plateforme (`dotnet publish -r win-x64`, etc.).

**Dépannage (Linux)** — si le chargement de `libSDL3.so` échoue (dépendances Wayland/X11 du système) :

```bash
sudo apt install libsdl3-dev     # installe SDL3 + le symlink libSDL3.so
```

puis supprimer les `PackageReference` `SDL3-CS.*` natifs du `.csproj` : le SDL3 du système prend le
relais. Attention, sans le paquet `-dev` le système n'expose que `libSDL3.so.0`, que .NET ne sait pas
résoudre — c'est justement pourquoi les natifs NuGet sont le choix par défaut.

## Aller plus loin

Images, polices, audio : ajouter le package correspondant à son OS.

```bash
dotnet add package SDL3-CS.Linux.Image     # SDL_image
dotnet add package SDL3-CS.Linux.TTF       # SDL_ttf
dotnet add package SDL3-CS.Linux.Mixer     # SDL_mixer
```

Documentation des bindings : <https://github.com/edwardgushchin/SDL3-CS> ·
documentation SDL3 : <https://wiki.libsdl.org/SDL3/>
