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

`pcem-dev/` est **l'oracle**, pas une dépendance de build de l'émulateur. iXtal26 est une
transcription en C# de PCem : les processeurs du 8088 au 486 et leurs coprocesseurs, les
machines, la vidéo, le stockage, le son et les ports ; l'arbre C reste dans le dépôt pour qu'on
puisse comparer la traduction à l'original. `tools/oracle/` en compile une partie, telle quelle,
en `libixtal26oracle.so`, que les portes de `tools/iXtal26.Diff` pilotent à côté du C#.

**Règle absolue : `pcem-dev/` n'est jamais modifié en place.** Le harnais de l'oracle
(`tools/oracle/harness*.c`, `harness*.cpp`) inclut ou lie les sources de PCem sans les patcher ;
ce qu'il doit observer ou neutraliser passe par ses propres fichiers et par `-Wl,--wrap`
(`tools/oracle/Makefile`, `harness_wrap.c`). Une référence modifiée n'est plus une référence.

`tools/check-oracle.sh` vérifie que les fichiers C cités par `oracle.tsv` n'ont pas
dérivé depuis leur transcription.

### Conséquence de licence

PCem est sous GPL v2.0. Une traduction en C# est une œuvre dérivée au sens de la GPL §2.
**iXtal26 est donc distribué sous GPL v2.0** (`COPYING` à la racine, identifiant SPDX
`GPL-2.0-only` — l'amont dit « GPL v2.0 » sans clause « or later »).

## `roms/`

**Non commité** (voir `.gitignore`). Images de BIOS et de polices IBM, sous copyright ;
PCem n'en distribue aucune et ce dépôt non plus. Seul `roms/roms.sha256` est commité : c'est la
seule trace suivie des ROM, et ce qui permet de vérifier qu'on dispose exactement des bons
fichiers. Le dépôt n'a pas de CI, et rien ne l'appelle d'office : la vérification se lance à la
main.

Le manifeste porte les 34 fichiers que le code lit (`romfopen`, `rom_init`, `rom_present`,
`loadfont`, `awe32.raw` du lanceur) ; `roms/` en contient bien plus, le paquet de ROM de PCem
entier, que le manifeste ne suit pas. Vérification : `cd roms && sha256sum -c roms.sha256`.

- les polices, sur toutes les machines (`mem_bios.c:59-61`) : `mda.rom`, `wy700.rom`, `8x12.bin` ;
- l'IBM PC 5150 : `ibmpc/pc102782.bin`, `ibmpc/basicc11.f6`, `.f8`, `.fa`, `.fc` ;
- l'IBM XT 5160 : `ibmxt/xt.rom`, ou la paire `ibmxt/5000027.u19` et `ibmxt/1501512.u18`
  (`mem_bios.c:169-176`) ;
- l'IBM AT : `ibmat/62x0820.u27`, `ibmat/62x0821.u47` ;
- les AMI : `ami286/amic206.bin`, `ami386/ami386.bin`, `ami386dx/opt495sx.ami`, `ami486/ami486.bin` ;
- l'Olivetti M24 : `olivetti_m24/olivetti_m24_version_1.43_low.bin` et `_high.bin` ;
- l'Amstrad PC1512 : `pc1512/40043.v1`, `40044.v1`, `40078.ic127` ;
- la vidéo : `ibm_vga.bin`, `trident.bin` (TVGA8900D), `tvga9000b/BIOS.BIN`, `et4000.bin`,
  `5429.vbi` (GD5429), `86c764x1.bin` (Trio64), `ibm_6277356_ega_card_u44_27128.bin` (EGA) ;
- les disques : `ibm_xebec_62x0822_1985.bin`, `dtc_cxd21a.bin`, `ide_xt.bin` (XTIDE),
  `adaptec_aha1542c_bios_534201-00.bin` ;
- le son : `awe32.raw`, la ROM de l'EMU8000, contrôlée aussi par `Awe32Sha256`
  (`tools/iXtal26.Diff/BootDiff.cs`).

`roms/ide_xt.bin`, que lit `Ide/xtide.cs`, n'est pas `roms/xtide/ide_xt.bin` (ils diffèrent dès
l'octet 5) : `roms/xtide/` porte les outils XTIDE de GR.3f.

`im1024font.bin` (`mem_bios.c:62`) est absent et sans effet : carte IM1024, non transcrite.
`loadfont()` tolère un fichier manquant (`romfopen` renvoie NULL).
