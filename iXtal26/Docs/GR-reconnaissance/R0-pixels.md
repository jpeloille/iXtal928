# GR.0 — Les pixels, pour toutes les cartes non SVGA : la reconnaissance

Lecture du 20 octobre 2026, au commit `d56d161`, en lecture seule. Texte de référence : PLAN.md:492-496 (GR.0),
PLAN.md:532-533 (la porte de GR), PLAN.md:33-35 (« Ce qu'un bloc livre », n° 3 et 4), PLAN.md:478-483 (« Ce qui ne
doit pas bouger »), PLAN-G9.md:137-157 (décisions), PLAN-G13.md:930-931 (décision n° 12).
Niveaux : **vérifié** (relu dans le code), **déduit** (raisonné sur le code relu), **inconnu** (à mesurer).

## 0. En bref

1. Rien de GR.0 n'est fait ; rien n'est caduc. G13 n'a touché aux images qu'en C# seul (décision n° 12).
2. L'oracle a encore `loadfont` et `cgapal_rebuild` vides (`tools/oracle/harness_stubs.c:465-466`, `:616`) :
   son image CGA, MDA, Hercules, M24 et PC1512 est entièrement noire ; celle de l'EGA, elle, est déjà juste.
3. Le C# charge les vraies polices et la vraie palette ; ses 113 valeurs de `cgapal_rebuild` sont celles de PCem.
4. Aucune sonde non SVGA ne hache `buffer32` ; la CGA n'a pas de sonde du tout (genre 0, comparaison sautée).
5. Les champs de la MDA, de l'Hercules et de l'EGA portent les noms de la M24 et du PC1512 ; le message vert
   annonce 163 champs pour toutes les cartes, quel que soit le nombre de champs remplis.
6. Les portes existantes ne finissent en graphique que pour le PC1512 (`bd-pc1512-plan`) : EGABANC et HERCBANC
   reviennent au texte, et ni le graphique de la CGA ni le 640 × 400 de la M24 ne sont atteints sous l'oracle.
7. Le mode matériel reste hors de l'oracle (boot-diff le refuse) ; ses images n'ont que les cas en C# seul.
8. Trois étapes proposées : GR.0a (l'oracle et ses tables), GR.0b (les sondes et les six pannes), GR.0c (les bancs
   qui finissent dans le mode testé, si la décision ouverte n° 1 le retient).
9. Coût : environ 88 boot-diffs non SVGA, 1 470 s cumulées dans g13b, 3 % de la série ; un hachage de 16 Mo par côté.
10. Trois décisions ouvertes : les modes graphiques, les images en mode matériel, les clés sans pendant chez PCem.

## 1. L'état, point par point

### 1.1 « L'oracle reçoit les vraies polices et la vraie palette CGA » — à faire

**Vérifié.** L'oracle ne lie pas `video.c` (`harness_stubs.c:356-358`) et en fournit les globales :

- `cgapal[16]` et `cgapal_rebuild` vide (`harness_stubs.c:465-466`) ; `initvideo` l'appelle (`:452`), les cartes aussi
  (`vid_cga.c:436`, `vid_mda.c:252`, `vid_hercules.c:316`, `vid_pc1512.c:449`) : `cgapal` reste à zéro ;
- `fontdat[2048][8]`, `fontdatm[2048][16]` définis à zéro (`harness_stubs.c:467-470`) ; `loadfont` vide (`:616`).
  `mem_bios.c` est lié tel quel (`tools/oracle/Makefile`, liste `SRC`) : ses quatre appels de tête
  (`mem_bios.c:59-62`) et celui du PC1512 (`:85`) tombent dans ce vide ;
- le chemin composite (`cga_comp_init`, `update_cga16_color`, `Composite_Process`) est vide (`harness_stubs.c:476-480`).

L'en-tête de section le dit déjà (`harness_stubs.c:367-371`) : « Stubé à zéro … À REPRENDRE avant l'oracle de
framebuffer ». **Pourquoi l'image n'est pas comparable** (déduit, du code relu) :

| Carte | Ce qui colore un point chez PCem | Dans l'oracle aujourd'hui |
|---|---|---|
| CGA | indice 4 bits puis `cgapal[...]` en fin de ligne (`vid_cga.c:290-293`) ; police `fontdat` (`:174`) | tout à 0 |
| MDA | `mdacols`, rempli de `cgapal` à l'init (`vid_mda.c:255-272`) ; police `fontdatm` (`:119`) | tout à 0 |
| Hercules | `mdacols` (`vid_hercules.c:319-336`) ; le graphique `cgapal[0x7]` (`:156`) | tout à 0 |
| M24 | `cgapal` sans `cgapal_rebuild` propre (celui d'`initvideo`) ; police `fontdatm` (`:186`) | tout à 0 |
| PC1512 | `cgapal` (`vid_pc1512.c:180-315`) ; police `fontdat` de `40078.ic127` (`mem_bios.c:85`) | tout à 0 |
| EGA | `pallook16`/`pallook64` (`vid_ega.c:16`, bâties à l'init, `:940-993`) ; la police au plan 2 | **juste** |

L'EGA ne lit ni `cgapal` ni `fontdat` (aucune occurrence, `grep` sur `vid_ega.c`) : son image est comparable dès
aujourd'hui, seule la sonde manque. VERIFICATION.md § M4.3 (ligne 313) rappelle le précédent : une police fausse en C#
qu'« aucun oracle ne pouvait attraper ».

**Ce que charge le C#** (vérifié). `video.cs:531-532` (`fontdat` à plat depuis M5.1, `fontdatm` en `[,]`) ;
`loadfont` (`video.cs:538-624`) : FONT_MDA et FONT_CGA transcrits, les sept autres formats en `case` vides, ce qui
laisse `fontdat` et `fontdatm` dans l'état où PCem les laisse (les formats de PCem écrivent dans `fontdatw`,
`fontdat8x12`, `fontdat12x18`, jamais dans `fontdat`) ; appelée par `mem_bios.cs:89-92` et `:256`, comme
`mem_bios.c:59-62` et `:85`. `cgapal_rebuild` (`video.cs:764` et suivantes) : **113 affectations, valeur pour valeur
identiques** à `video.c:1162-1298` (comptées par un script jetable qui extrait les `cgapal[i] = makecol(r, g, b)` des
deux fichiers et compare les listes). `makecol` identique (`video.h:20`, `video.cs` « pcem: video.h:20 »).

**Pour la recopie** (vérifié) : `romfopen` est lié (`rom.c`) ; `roms/mda.rom` (8 192 o, exactement les 4 × 256 × 8 de
FONT_MDA), `roms/wy700.rom`, `roms/8x12.bin`, `roms/pc1512/40078.ic127` (8 192 o) sont présents,
`roms/im1024font.bin` absent (le `return` de `video.c:935-937`). La recopie verbatim de `video.c:930-1047` exige de
définir `fontdatw[512][32]`, `fontdat8x12[256][16]`, `fontdat12x18[256][36]` (`video.c:922-924`), que le harnais n'a
pas. Le fichier de 8 Ko du PC1512 lu en FONT_CGA (2 048 × 8) laisse 0xFF au-delà du caractère 1 023 des deux côtés
(`getc` rend EOF, `ReadByte` −1, tous deux tronqués en 0xFF) ; `fontbase` ne dépasse pas 768 (`vid_pc1512.c:443`).

### 1.2 « Les sondes de ces cartes hachent l'image (`buffer32`) » — à faire

**La référence, la VGA** (vérifié) : `h_vga_probe` hache `buffer32` entier, 2 048 × 2 048 × 4 octets
(`harness.c:2264`), le C# les mêmes octets (`vid_svga.cs`, `Probe`, `MemoryMarshal.AsBytes(Buffer32)`), champ 3
`#buffer32` ; prise en fin de course, au même point de l'histoire des deux côtés (`BootDiff.cs:678-682` pour
l'oracle, `:802-803` pour le C#, avant `pc.closepc`). M15 a prouvé que le champ mord (VERIFICATION.md:3087-3095 :
F2, la 9ᵉ colonne des cellules, rougit `#buffer32` seul). `initvideo` remet `buffer32` à zéro des deux côtés
(`harness_stubs.c:425`, `video.cs` `Array.Clear(Buffer32)`), ce qui rend les deux histoires comparables.

**Les cartes non SVGA** (vérifié) : `h_vga_probe` délègue quand `svga_get_pri()` est nul (`harness.c:2248-2254`) ;
aucune de ces sondes ne touche `buffer32` :

| Carte | Oracle | C# | Genre | Places | Champs | Image |
|---|---|---|---:|---|---:|---|
| M24 | `harness_m24.c` (`h_m24_probe`) | `vid_svga.cs:2351` | 2 | 122-142 | 21 | non |
| PC1512 | `harness_pc1512.c` | `vid_svga.cs:2384` | 3 | 143-162 | 20 | non |
| MDA | `harness_mda.c:47-74` | `vid_svga.cs:2322` | 4 | 122-138 | 17 | non |
| Hercules | `harness_hercules.c:48-73` | `vid_svga.cs:2293` | 5 | 122-138 | 17 | non |
| EGA | `harness_ega.c:59-101` | `vid_svga.cs:2249` | 6 | 122-151 | 30 | non |
| CGA | — | — | 0 | — | 0 | non |

Les commentaires des quatre harnais le disent : « la sonde ne lit pas les pixels » (`harness_mda.c:6-7`,
`harness_hercules.c:6-7`, `harness_m24.c:51`, `harness_pc1512.c` « Pas de framebuffer »).

**La CGA n'a aucune sonde** (vérifié) : `h_vga_probe` rend le genre 0, et `CompareVga` sort sans rien comparer
(`BootDiff.cs:1015-1016`). Les boot-diffs en CGA ne comparent que le diff d'instructions. Le `cga_t` est public
(`includes/private/video/vid_cga.h:3-36`) ; la carte se monte par `device_add(&cga_device)` (`harness.c:1972`).
Sa VRAM est allouée sans effacement (`vid_cga.c:427`) — sans conséquence sur le diff d'instructions (aucune lecture
avant écriture, sinon la série sous `MALLOC_PERTURB_=85` serait rouge, déduit), mais visible dans l'image.

### 1.3 « et `pallook` pour l'EGA » — à faire

`h_ega_probe` hache `egapal` (`harness_ega.c:78`), pas `ega->pallook` (vérifié). Le pointeur désigne `pallook16` ou
`pallook64` selon `vres` (`vid_ega.c:51`) ; le C# a les mêmes tables (`vid_ega.cs:110`, `:167`). Le champ 9
`#pallook` de la disposition SVGA (`VgaProbe.cs:30-31`) est libre pour les genres non SVGA.

### 1.4 « leurs champs sont nommés dans `VgaProbe.Fields` » — partiel

`VgaProbe.Fields` (`VgaProbe.cs:27-67`, 163 noms) nomme les places 122-142 pour la M24 et 143-162 pour le PC1512
(vérifié). La MDA, l'Hercules et l'EGA écrivent aux mêmes places (« aux places de la M24 », `harness_mda.c:46`,
`harness_ega.c:57`) : une divergence de la VRAM de la MDA (place 124) s'imprimerait « `#m24.vram` », une de l'EGA en
place 145 « `#pc1512.vram` » (déduit de `BootDiff.cs:1019-1024`, qui lit `VgaProbe.Fields[f]` sans regarder le genre).
La CGA n'a aucun nom.

### 1.5 « le message vert compte les champs remplis » — à faire

`CompareVga` imprime `VgaProbe.Fields.Length`, soit 163, pour chaque genre (`BootDiff.cs:1033-1050`, vérifié) : « Sonde
MDA : 163 champs identiques » alors que la MDA en remplit 18 (le genre et 17). PLAN.md:480 fige « sonde VGA 163/163 »
parmi ce qui ne doit pas bouger : le compte des champs remplis ne vaut donc que pour les genres non SVGA (déduit).

### 1.6 « Une panne injectée par carte rougit un champ de sonde » — à faire

Les contrôles négatifs de G1.1, G1.2, G9.0-G9.2 portaient sur l'état que le processeur relit (3BAh, 3DAh, le plan 2 du
PC1512) ou sur un registre sondé (`ctrl` de la M24) (VERIFICATION.md:5024-5025, :5081-5082, :5440-5441, :5516-5517,
:5558-5559) ; aucun sur l'image.

### 1.7 Les modes, et les portes qui les atteignent

Les portes vidéo non SVGA (`tools/gates/series.sh:18-23`, `:158-168`, `:273-289`, `:360-362`), comptées par un script
jetable sur `series.sh` (carte par `--gfxcard`, par la clé `gfxcard` du `.cfg`, ou CGA par défaut) :

| Carte | Portes | Modes atteints sous l'oracle (déduit des bancs relus) | Non atteints |
|---|---:|---|---|
| CGA | ≈ 60 (a) | texte 80 × 25 du POST et de DOS ; BASIC | 40 col. (inconnu), 320 × 200, 640 × 200, 3D9h |
| MDA | 5 | texte 80 × 25 | souligné, inverse, clignotant (inconnu) |
| Hercules | 6 | texte ; 720 × 348 par HERCBANC, qui **repasse au texte** (`hercbanc.py:206`) | l'image graphique |
| EGA | 8 | texte ; 0Dh, 0Eh, 10h, 04h par EGABANC, qui **revient au texte** (`:237`) | l'image graphique |
| M24 | 4 | texte du POST, DOS, `DIR` | 640 × 400 (`ctrl & 1`, `vid_olivetti_m24.c:265`), 320 × 200 |
| PC1512 | 5 | texte ; le mode plan 640 × 200 × 16, **où le banc finit** (`pc1512banc.py:172-185`) | — |

(a) Les 8088 et les AT sans carte, les bancs du son, de LPT, de la manette, PIC et DMA sur le 5150, le XT ou l'AT ;
les boucles de `series.sh` développées. Toutes cartes : environ 88 portes.

Le composite est hors d'atteinte des deux côtés : omis en C# (`vid_cga.cs:484-487`), vide dans l'oracle. Avec
`display_type = composite`, l'oracle laisserait les indices bruts dans `buffer32` (`vid_cga.c:285-289`, les stubs ne
font rien) et le C# des couleurs RGB : rouge assuré (déduit).

Principe déjà décidé : « des bancs qui finissent dans le mode testé » (PLAN.md:34-35), renvoyé à GR (PLAN.md:327-328).
GR.1 nomme MDABANC, EGABANC étendu à AND et OR, ET4BANC qui finit en 256 couleurs (PLAN.md:497-501) ; aucun banc de
la CGA ni de la M24 n'est nommé nulle part.

### 1.8 Le mode matériel

**Vérifié** : boot-diff refuse le mode matériel (`BootDiff.cs:428-435`) ; la série « tout » le marque `pcem_seul`.
Les corrections de G13.9 qui changent l'image en mode matériel : PB-04 (le curseur de la CGA), PB-89 (la ligne
bornée), PB-96, PB-97, PB-102 (le caractère de 9 points), PB-222 et PB-223 (le curseur), PB-99 (l'EGA), PB-224 et
PB-228 (le faisceau), PB-294 (les moniteurs de l'EGA) — PLAN-G13.md:705-740, VERIFICATION.md § G13.9a à § G13.9g.
Elles sont vérifiées par des cas en C# seul qui lisent de vrais points dans `Buffer32`, avec les vraies polices :
`MaterielCasMono.cs:167`, `MaterielCasCga.cs:53`, `MaterielCasBordure.cs:24`, `MaterielCasEga.cs:125-201`,
`MaterielCasFaisceau.cs:108-113`, `R9VideoCfg.cs:103-106`. Aucun oracle ne peut juger ces images : PCem dessine
autrement par construction. La décision n° 12 (« les images viendront avec GR.0 ») ne dit pas si GR.0 doit en
donner une en mode matériel : décision ouverte n° 2.

**Deux clés de G13.12a valent dans les deux modes et n'ont pas de pendant chez PCem** (vérifié) : `font` de la CGA
(`vid_cga.cs:656-659`, `fontbase = 256`) et `io_base` de l'EGA (0x200) ; VERIFICATION.md:13126-13129. L'oracle lit la
section du device (`h_set_device_config`, `harness_stubs.c:713`) mais `cga_standalone_init` ne connaît pas `font` :
sous boot-diff avec `font = 1`, l'oracle dessinerait le double trait, le C# le simple — l'image rougirait sans défaut
de transcription (déduit). Aucune porte ne pose ces clés aujourd'hui. Décision ouverte n° 3.

### 1.9 Le déterminisme

- **L'instant** : la fin de course, comme la VGA (`BootDiff.cs:678-682`, `:802-803`). `buffer32` y mêle la trame en
  cours (lignes au-dessus de `displine`) et la précédente ; identique des deux côtés tant que les temps le sont, ce
  que garantit le diff d'instructions vert (déduit).
- **Le clignotement** (curseur, attribut 80h) dépend du compte de trames ; ses compteurs sont déjà sondés (`cursoron`,
  `blink`, `cgablink`) : identiques si le diff l'est.
- **Les lignes jamais redessinées** (après un changement de mode, la bordure de PB-89 sur la ligne suivante, le texte
  de l'EGA redessiné seulement sur `fullchange`, PB-99) gardent leur histoire : comparables seulement si les deux
  côtés partent de zéro. C'est le cas pour `buffer32` (§ 1.2) ; **pas pour la VRAM de la CGA** (`vid_cga.c:427`), que
  la série remplit d'octets 0xAA côté oracle (`MALLOC_PERTURB_=85`, déduit) avant que le BIOS l'efface : à effacer au
  montage, comme les VRAM de la MDA, de l'Hercules, de l'EGA (PLAN-G9.md:151-152, décision n° 4) et de la M24.
- **La CGA écrit des indices puis convertit en fin de ligne** (`vid_cga.c:290-293`) : la ligne en cours au moment de
  la sonde contient des indices, des deux côtés.
- **`charbuffer`** : 256 octets chez PCem, 512 en C# (PB-09, `vid_cga.cs:63-68`) — la sonde n'en hache que 256.
- **La phase 2** du boot-diff (qui ne rejoue ni `--type` ni `--fdb`, GR.4) vient après la sonde : sans effet.

## 2. Ce qui reste, et les étapes proposées

### GR.0a — L'oracle reçoit les polices et la palette

**Contenu.**
- `harness_stubs.c` : `loadfont` recopié verbatim de `video.c:930-1047` et `cgapal_rebuild` de `video.c:1162-1298`,
  avec `fontdatw`, `fontdat8x12`, `fontdat12x18` définis (`video.c:922-924`) ; l'en-tête (b) (`:367-371`) et le
  commentaire de `fontdatm` (`:468-469`) récrits ; le chemin composite reste vide, et le dit. Le PCem vendoré intact.
- Un export de l'ABI (58) qui rend `fontdat`, `fontdatm` et `cgapal` (ou leurs empreintes).
- Une porte hors machine, `video-tables-check`, sur le patron d'`opl-tables-check` et `emu-tables-check` : pour chaque
  romset du dépôt, `loadbios` des deux côtés puis `fontdat` et `fontdatm` comparés octet par octet ; puis
  `cgapal_rebuild` pour les sept `display_type` × deux contrastes, comparé.

**Preuves.** La porte verte ; le contrôle avant/après : la même porte contre l'oracle de `d56d161` est rouge (tables
nulles). Les comptes d'instructions des boot-diffs non SVGA inchangés à l'unité (la police et la palette ne touchent
aucun état du processeur, VERIFICATION.md § M4.3) — c'est « Ce qui ne doit pas bouger » (PLAN.md:478-483).

**Contrôles négatifs** (côté C#, retirés) : FONT_MDA qui inverse les polices fine et épaisse ; une valeur de
`cgapal_rebuild` (l'ambre, contraste) faussée d'une unité ; chacun rougit `video-tables-check`.

**La série.** L'émulateur ne change pas : le sous-ensemble ciblé — `abi`, la porte neuve, les boot-diffs non SVGA —
contre g13b. Environ 88 portes, 1 470 s cumulées dans g13b (temps de la colonne 3 de
`/var/tmp/ixtal-g13/series/g13b.tsv`, sommés pour les portes classées non SVGA ; 47 104 s pour les 764 portes),
soit 3 %.

### GR.0b — Les sondes hachent l'image ; six pannes

**Contenu.**
- MDA, Hercules, EGA, M24, PC1512 : le champ 3 (`#buffer32`, la place de la VGA) rempli par chaque genre, mêmes
  octets que `harness.c:2264` ; l'EGA remplit aussi le champ 9 (`#pallook`, 256 × 4 octets en petit-boutiste, comme
  `harness.c:2257-2259`). Proposé en plus, à peu de frais : `#mdacols` (MDA, Hercules ; 4 Ko) et `#cgapal` (CGA, M24,
  PC1512 ; 64 o), pour qu'une palette fausse se voie même sur un attribut absent de l'écran — à la place d'un champ
  libre de chaque genre.
- La CGA, genre 7 : `h_cga_attach` (le `cga_t` retrouvé dans `devices[]` après `harness.c:1972`, sa VRAM de 16 Ko
  effacée — déviation de l'oracle, par analogie avec la décision n° 4 de PLAN-G9) et `h_cga_probe`, aux places 122 et
  suivantes (la CGA ne se monte jamais avec la M24 ni le PC1512) : `#crtc`, `crtcreg`, `#vram`,
  `cgamode|cgacol|cgastat`, `fontbase`, `linepos`, `displine`, `sc`, `vc`, `cgadispon`, `con|coff`,
  `cursoron|cgablink`, `vsynctime|vadj`, `ma|maback`, `oddeven`, `dispontime`, `dispofftime`, `timer`,
  `firstline|lastline`, `drawcursor`, `#charbuffer` (256 o), `revision|composite|snow_enabled` — 22 champs. Côté C#,
  `ProbeCga` dans `vid_svga.cs`, la carte prise par `PluginApi.device.device_priv` ou par un `Probe` statique comme
  `vid_mda.Probe` (`vid_mda.cs:474`).
- `VgaProbe` : un tableau de noms par genre (SVGA, M24, PC1512, MDA, Hercules, EGA, CGA), `CompareVga` nomme le champ
  divergent selon `o[0]` et compte les champs remplis : « Sonde MDA : 20 champs identiques — …, image comprise ». La
  SVGA garde « 163 champs » (PLAN.md:480).

**Preuves.** Tous les boot-diffs non SVGA verts, images comprises, aux mêmes comptes d'instructions. L'image vue :
`--boot` du C# et vidage de l'image de l'oracle (un `h_buffer32` pour lire les points) sur un écran de chaque carte,
comparés hors porte, une fois.

**Contrôles négatifs** (côté C#, chacun retiré ; attendu : diff d'instructions vert, la sonde rouge sur l'image seule) :

| Carte | Panne proposée | Porte qui doit rougir | Champ |
|---|---|---|---|
| Carte | Panne proposée | Porte | Champ |
|---|---|---|---|
| CGA | rangée de police `(cga.sc + 1) & 7`, 80 colonnes (`vid_cga.cs:319`) | `bd-pcdos-cga` | `#buffer32` |
| MDA | `mdacol(c, 0, 1) = cgapal[0xf]` au lieu de `[0x7]` (`vid_mda.cs:413`) | `bd-pcdos-mda` | `#buffer32` (b) |
| Hercules | la même (`vid_hercules.cs:448`) ; en graphique, `cgapal[0xf]` (`:243`) | `bd-pcdos-hercules` | idem |
| EGA | `pallook64` : un poids secondaire 0x55 → 0x54 (`vid_ega.cs:1218` sqq.) | `bd-pcdos-ega` | `#pallook` (c) |
| M24 | `lineff ^ 1` dans l'index de `fontdatm` (`vid_olivetti_m24.cs:261`) | `bd-m24-dir` | `#buffer32` |
| PC1512 | les plans 1 et 2 échangés dans la couleur (`vid_pc1512.cs:418`) | `bd-pc1512-plan` | `#buffer32` |

(b) et `#mdacols` s'il est retenu ; (c) et `#buffer32`. La panne graphique de l'Hercules attend GR.0c.

Et une panne côté oracle : `loadfont` revenu au stub, tous les textes rouges — la preuve que GR.0a porte.

**La série.** Les fonctions de sonde vivent dans l'assemblage de l'émulateur (`vid_svga.cs:2078-2405`) : l'émulateur
change (méthodes froides) ; la série entière dans les deux modes, comme à chaque sonde neuve de G1 et G9. Attendu
contre g13b : les messages des ≈ 88 portes non SVGA (« N champs identiques », la CGA qui gagne une ligne), l'ABI ;
aucun compte d'instructions ; M2 : seules les méthodes de sonde changent.

### GR.0c — Les bancs qui finissent dans le mode testé (selon la décision ouverte n° 1)

**Contenu proposé.** Une variante de chaque banc qui s'arrête avant le retour au texte, générée par le même `.py`
(les `.keys` actuels intacts, leurs portes inchangées) : EGABANC qui finit en 10h (les 64 couleurs de `pallook64`,
350 lignes), HERCBANC qui finit sur la page 1 du graphique. Un banc neuf de la CGA (modes 4, 5, 6 et 1 par INT 10h, un
motif, la palette de 3D9h, l'intensité, la bordure ; il finit en 4 ou en 6) et, sur la M24, le 640 × 400 (`ctrl` bit 0,
3DEh ; le mode du BIOS qui le pose est inconnu, à mesurer). Chacun saisi dans DEBUG sur PC-DOS 2.00, sous boot-diff.

**Preuves.** Chaque porte neuve verte, image comprise ; sa panne au chemin graphique (le tableau de GR.0b, colonne
graphique) rouge sur `#buffer32` seul.

**La série.** Outillage seul : le sous-ensemble (les portes neuves, environ 40 à 60 s chacune d'après
`bd-pc-ega-banc` et `bd-pc-herc-banc` dans g13b).

## 3. Les risques

1. **Le premier vert n'est pas acquis.** Les chemins de points n'ont jamais été comparés hors de la VGA : police à plat
   (M5.1, `video.cs:526-531`), lecture à plat de PB-96, `charbuffer` de 512 (PB-09), la 9ᵉ colonne de la MDA. Un rouge
   en GR.0b est alors un défaut de transcription en mode PCem, à corriger dans l'étape (l'émulateur change).
2. **La VRAM de la CGA non effacée** (`vid_cga.c:427`) : sans l'effacement au montage, `#buffer32` rougit sous
   `MALLOC_PERTURB_` sans défaut ; l'effacement ne doit pas bouger un compte (aucune lecture avant écriture, déduit,
   à prouver par la série).
3. **Le composite** : rouge assuré s'il était demandé (§ 1.7) ; hors de la portée, à refuser sous boot-diff.
4. **Les clés `font` et `io_base`** sous boot-diff (§ 1.8).
5. **Le sens de l'image de fin** : un hachage de 16 Mo, pas « l'écran » — il mêle deux trames et les restes des modes
   précédents ; à écrire dans « ce que ce vert ne dit pas » (GR.5). Le texte de l'EGA hors `fullchange` (PB-99) est
   comparé tel que PCem le garde, pas tel qu'une EGA l'afficherait.
6. **Le coût** : FNV octet par octet sur 16 Mo, une fois par côté et par porte ; sous `--lockstep N`, à chaque
   relevé, comme la VGA aujourd'hui.
7. **Les comptes figés** : la sonde VGA « 163/163 » ne doit pas bouger (PLAN.md:480) ; le compte des champs remplis
   ne s'applique qu'aux genres 2 à 7.

## 4. Les décisions ouvertes

1. **Les modes graphiques sous l'oracle.** Le principe (PLAN.md:34-35) veut des bancs qui finissent dans le mode
   testé ; GR.0 ne nomme aucun banc, GR.1 ni la CGA ni la M24. *Proposé :* GR.0c tel que ci-dessus — les variantes
   d'EGABANC (10h) et d'HERCBANC (graphique), un banc de la CGA, le 640 × 400 de la M24 ; le PC1512 est déjà couvert.
   L'autre voie, sans banc neuf : une chaîne des trames, l'empreinte de chaque rectangle remonté, prise aux deux
   frontières hôte qui existent déjà (`video_blit_memtoscreen` vide de l'oracle, `harness_stubs.c:485-487` ; le
   crochet `video_blit_memtoscreen_func` du C#, `video.cs:280`, appelé `:759`) — chaque mode traversé par une porte
   serait comparé, sans toucher à PCem ni à l'émulateur, au prix d'un hachage par trame (inconnu, à mesurer).
2. **Les images en mode matériel.** *Proposé :* aucune comparaison (pas d'oracle) ; une empreinte `@image` (les
   `xsize × ysize` points en haut à gauche de `Buffer32`, comme `@shot`, `BootTest.cs:117-120`) ajoutée aux attendus
   d'EGABANC et d'HERCBANC dans les deux modes, en C# seul, sur le patron de `@vram` (`BootTest.cs:125-128`) : un
   témoin de non-régression, que les cas de G13.9 justifient point par point.
3. **Les clés sans pendant chez PCem sous boot-diff** (`font` ≠ 0 de la CGA, `io_base` = 0x200 de l'EGA,
   `display_type = composite`). *Proposé :* boot-diff les refuse, code 2, avec un message, comme le mode matériel
   (`BootDiff.cs:428-435`).

## Annexe — Candidats, hors de la portée

- **La M24 ne reconstruit pas sa palette** (`vid_olivetti_m24.c`, aucun `cgapal_rebuild`, vérifié) : elle hérite de
  `cgapal` tel que l'a laissé `initvideo` ou la carte précédente. Chez PCem, `initvideo` ne tourne que dans `initpc`
  (`pc.c:261`) : une M24 choisie après une CGA en tube vert, sans relancer PCem, serait verte (déduit, non mesuré).
  Le C# rappelle `initvideo` à chaque `initpc` : sans effet dans iXtal.
- **Le chemin composite écrit des octets là où il lit des mots** (`vid_cga.c:287` : `buffer32->line[...][c]` indexe
  des octets) — relevé par le commentaire de `vid_cga.cs:486-487` ; le chemin est omis des deux côtés.
