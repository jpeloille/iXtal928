# Code tiers vendoré

## `pcem-dev/`

| | |
|---|---|
| Projet | PCem — émulateur PC, par Sarah Walker et contributeurs |
| Version | **v18** (`CHANGELOG.md` ligne 1 : « # PCem v18 ») |
| Amont | <https://github.com/sarah-walker-pcem/pcem> · <https://pcem-emulator.co.uk/> |
| Licence | **GPL v2.0** (`pcem-dev/COPYING`, et `pcem-dev/README.md` ligne 8 : « PCem is licensed under GPL v2.0 ») |
| Récupéré le | 2026-09-17 (mtime de l'arbre) |
| Taille | 16 Mo, 355 fichiers `.c`, ~309 000 lignes C/C++/H |

### Ancrage

`pcem-dev/` **n'est pas un checkout git** — pas de `.git`, c'est un export de sources.
L'ancrage ne peut donc pas être un SHA de commit ; c'est un hash de contenu :

```
sha256 de l'arbre : 26f0727cc53e565a5363b705b3d887db9d35d984448d77abb784fe71f9d853b7
989 fichiers
```

recalculable par (c'est ce que fait `tools/check-oracle.sh`) :

```bash
find pcem-dev -type f -not -path 'pcem-dev/.idea/*' -print0 \
  | sort -z | xargs -0 sha256sum | sha256sum
```

`pcem-dev/.idea/` est exclu : bruit d'IDE laissé dans l'export amont, sans rapport avec
les sources.

**Piège de commit, à connaître :** `pcem-dev/.gitignore:46` contient la règle `x86/`,
destinée à des répertoires de build amont, mais qui attrape aussi les vrais sources
`src/codegen/x86/` et `src/codegen/arm64/`. L'arbre vendoré a donc été ajouté avec
`git add -f`. Ces backends de dynarec ne sont pas transcrits, mais l'arbre doit rester
**complet** pour que le hash ci-dessus soit vérifiable.

### Rôle dans ce dépôt

`pcem-dev/` est **l'oracle**, pas une dépendance de build. iXtal26 est une transcription
en C# de son cœur 8088/XT ; l'arbre C reste dans le dépôt pour qu'on puisse diffé rer la
traduction contre l'original, et pour compiler `pcem-ref` — un binaire de référence
instrumenté qui sert de source de vérité aux harnais de test.

**Règle absolue : `pcem-dev/` n'est jamais modifié en place.** L'instrumentation de trace
est livrée comme patch (`patches/000N-*.patch`) appliqué à une copie de travail
(`pcem-ref-build/`, `.gitignored`). Une référence modifiée n'est plus une référence.

`tools/check-oracle.sh` vérifie que les fichiers C cités par `oracle.tsv` n'ont pas
dérivé depuis leur transcription.

### Conséquence de licence

PCem est sous GPL v2.0. Une traduction en C# est une œuvre dérivée au sens de la GPL §2.
**iXtal26 est donc distribué sous GPL v2.0** (`COPYING` à la racine, identifiant SPDX
`GPL-2.0-only` — l'amont dit « GPL v2.0 » sans clause « or later »).

## `roms/`

**Non commité** (voir `.gitignore`). Images de BIOS et de polices IBM, sous copyright ;
PCem n'en distribue aucune et ce dépôt non plus. Seul `roms/roms.sha256` est commité, pour
que la CI puisse vérifier qu'elle dispose exactement des bons fichiers, et signaler un
`SKIPPED` explicite plutôt qu'un échec quand ils sont absents.

Contenu attendu pour la cible IBM PC/XT :

| Fichier | Taille | Rôle |
|---|---|---|
| `ibmxt/xt.rom` | 65 536 | BIOS 5160 du 05/09/86. Vecteur de reset `EA 5B E0 00 F0` à 0xFFFF0. Chemin nominal de `loadbios()`, `mem_bios.c:179` |
| `ibmxt/5000027.u19` + `ibmxt/1501512.u18` | 32 768 ×2 | Paire alternative, repli si `xt.rom` absent (`mem_bios.c:169-176`) |
| `mda.rom` | 8 192 | Police. Chargée **inconditionnellement** par `loadbios()` (`mem_bios.c:59`) et lue par le rendu texte CGA via `fontdat[chr + fontbase]` |
| `wy700.rom`, `8x12.bin` | 16 384 / 4 096 | Polices chargées inconditionnellement aussi (`mem_bios.c:60-61`), inutiles au XT mais évitent un chemin d'erreur |

`im1024font.bin` (`mem_bios.c:62`) est absent et sans effet : carte IM1024, hors cible.
`loadfont()` tolère un fichier manquant (`romfopen` renvoie NULL).
