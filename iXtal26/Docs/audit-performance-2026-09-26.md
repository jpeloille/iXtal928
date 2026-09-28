# Audit du code d'iXtal26 : cœurs 8088, 286 et 386/486

> Audit du 26 septembre 2026, au commit `047abd9`. En lecture seule : aucun fichier de code
> n'a été modifié ni commité.

## Méthode

- Quatre audits parallèles : le cœur 8088, le cœur 286/386, la mémoire et les E/S, puis la
  vidéo et l'hôte.
- Lecture de l'IL de la DLL Release (`ilspycmd -il`).
- Relecture de la trace de profil du 26/09 : AT 286, Windows 3.1, Trident,
  `/tmp/ixbench-run/A.speedscope.json`.
- Chaque point classé haut a été revérifié dans le code.

**Aucun gain n'a été mesuré.** Ce sont des gains attendus, à valider un levier à la fois,
au banc, avec l'accord de Julien. Chaque levier doit passer la séquence de portes
(fuzzeur pour les cycles, journal d'écritures, boot-diffs, empreintes du framebuffer),
avec une mesure entre deux leviers.

## 1. Défauts à traiter avant toute optimisation

| # | Où | Défaut | Correctif |
|---|---|---|---|
| D1 | `Memory/mem.cs:1103-1110` | `wlog_n` est incrémenté à chaque écriture lente, `--boot` compris, et n'est remis à zéro que par le harnais (`wlog_reset`). Après 2³¹ écritures, soit de l'ordre de 30 à 60 min de VGA sous Windows, il devient négatif. Le test `< WLOG_MAX` passe alors, et l'accès `wlog_addr[-2147483648]` lève une `IndexOutOfRangeException`. **L'émulateur plante.** | Ne compter que sous `WLOG_MAX`, ou passer en `long`. Vérifier d'abord ce que `h_state` compare. |
| D2 | `Cpu/808x.cs:3547` et `Cpu/386_ops_misc.cs:389-391` | `IDIV` mot avec `DX:AX = 0x80000000` et diviseur `0xFFFF` : `int.MinValue / -1` lève `OverflowException` en .NET, même en `unchecked`. Un programme invité fait donc tomber l'hôte, alors qu'un vrai CPU lève #DE. PCem meurt aussi (SIGFPE). Le même piège existe en 32 bits (`long.MinValue / -1`) au 386. | Garde explicite vers le chemin #DE, marquée `// pcem bug, not reproduced:` et consignée dans `PCEM_BUGS.md`. |
| D3 | `Cpu/808x.cs:3175` | `AAM 0` sur le 8088 lève `DivideByZeroException`. Le chemin 286 (`386_ops_bcd.cs:74`) est protégé par `if (!base)`. | Même traitement que D2. |
| D4 | `Cpu/808x.cs:3448` | `IDIV` octet : `tempws = (int)AX` étend AX par des zéros au lieu du signe. Fidèle au C (`808x.c:3614`), mais ni marqué `// pcem bug, reproduced:` ni consigné dans `PCEM_BUGS.md`. | Marquer et consigner. Il pourrait expliquer des échecs SST sur F6.7. |
| D5 | `Memory/mem.cs:468`, `:511` | `readlookup2[..] + addr` fait un calcul en `long`, sans le repli modulo 2³² des autres sites `unchecked(.. + (int)addr)`. Latent aujourd'hui ; lèvera une `IndexOutOfRangeException` avec la pagination du 386 (adresses ≥ 0x80000000). | Aligner sur la forme `unchecked`. |
| D6 | `Host/SdlHost.cs:848`, `:855-882` | La borne est `w > Stride` au lieu de `x + w > Stride`. `ConsumeBlit` remonte les lignes `0..h` alors que seules `[y1,y2)` ont été écrites : des lignes d'une image précédente, ou une bande noire, peuvent apparaître. | Voir P7. |

## 2. Performance, classée d'après le profil 286 sous Windows 3.1

Répartition mesurée : `exec386` 40 %, `prefetch_run` 10 %, timers 7,4 %,
`readmemw`/`writememw` 4,6 %, égalité de délégués 2,5 %.

### P1. Égalité de délégués dans `svga_poll`, 2,5 % mesurés

`Video/vid_svga.cs:967-969`

- **Mécanisme :** `svga.render == svga_render_8bpp_lowres || …` compare cinq délégués vers
  des méthodes statiques différentes. Ils partagent le même shuffle thunk, donc
  `Delegate.Equals` descend dans `InternalEqualMethodHandles`, un appel natif. La trace
  attribue 100 % de ce coût à cette ligne. Les 12 autres `op_Equality` de l'assembly sont
  dans `io_removehandler`, chemin froid.
- **Correctif :** un champ `static readonly svga_render_fn` canonique par rendu, utilisé à
  **tous** les sites d'affectation, puis `ReferenceEquals`. `// DEVIATION:` requise.
  Contrôle : `grep -rn "\.render = svga_render_"` ne doit plus rien renvoyer.
- **Piège :** un simple `ReferenceEquals` sans ces champs canoniques casserait en silence.
  Le cache Roslyn des groupes de méthodes existe une fois par classe : `vid_tvga` et
  `vid_svga` ont déjà deux instances distinctes de `svga_render_8bpp_highres`. Une future
  Cirrus ou S3 affectant un `*_lowres` désactiverait la division de `video_res_x`.

### P2. Handlers 286 bâtis en fermetures qui appellent d'autres délégués

`Cpu/386_ops_arith.cs:100-240`, `Cpu/386_ops_jump.cs:83`, INC/DEC, PUSH/POP reg

- **Mécanisme :**
  - chaque ADD/SUB/AND… fait trois appels indirects (la fermeture, `op8`, `setflags8`)
    plus le test `gettempc` capturé ;
  - chaque Jcc appelle `cond()` ;
  - le site intérieur voit plusieurs cibles différentes, et sans tiering il n'y a pas de
    dévirtualisation gardée : rien n'est inliné ;
  - le 386 multipliera ce motif (formes `_a32` et 32 bits).
- **Correctif fidèle au C :** des méthodes nommées, une par opcode, comme la macro C les
  engendre (`opADD_b_rmw_a16`, `opJO`…). Plus proche du C que les fermetures, et cela
  retire la DEVIATION de `jump.cs:30`, au prix de plus de lignes (à vérifier contre le
  plafond R2).
- **Alternative plus compacte :** un générique contraint par `static abstract`. Hors de R4,
  donc à arbitrer.
- Probablement le levier le plus important de `exec386` avant G2.

### P3. `prefetch_run`, 10 %

`Cpu/386.cs:185-247`

- **Mécanisme :** `prefetch_bytes` et `cycles` sont un statique mutable et une propriété
  `ref` vers un champ d'objet. Ils sont relus et réécrits en mémoire à chaque tour des deux
  `while`.
- **Correctif :** travailler sur des locales, réécrites une fois en fin de méthode.
  Sémantique identique ; du C# ordinaire, rien qui sorte de R4.
- **Option :** remplacer les deux boucles par une division (`n = ceil(-pb / width)`).
  `// DEVIATION:`, vérifiée par les cycles du fuzzeur.

### P4. Accès mémoire octet par octet

`Cpu/386_common.cs:115-119, 142-215, 345` ; `Cpu/808x.cs:113-136` ;
`Memory/mem.cs:469, 512, 570, 644-721`

- **Mécanisme :** 2 à 4 chargements, chacun avec son contrôle de bornes, et
  `readlookup2[addr>>12]` relu deux ou trois fois. `mem.ram` et `readlookup2` ne sont pas
  `readonly` (`mem.cs:136, 167`), donc rechargés à chaque accès. `fastreadl` passe par ce
  chemin **à chaque instruction**. Côté 8088, l'IL montre aussi des index calculés en
  `long` (`int + uint32`) dans `readmemw`, `writememw`, `readmembf`.
- **Correctif :**
  - une seule lecture de la table en locale ;
  - puis `BinaryPrimitives.ReadUInt16LittleEndian(new ReadOnlySpan<byte>(ram, i, 2))`, ou
    la variante 32 bits : un seul contrôle, une seule instruction de chargement, et le
    résultat reste indépendant de l'hôte, donc la DEVIATION de `386_common.cs:111` garde
    sa justification.
- `Span` et `BinaryPrimitives` sont hors de la liste fermée R4 : accord requis.
- Le cœur 8088 en profite aussi (91 sites dans `execx86`).

### P5. Banc de registres

`Cpu/x86.cs:58`

- **Mécanisme :** `readonly x86reg[] regs` coûte un déréférencement de plus et un contrôle
  de bornes, même pour `regs[0]` (AX), parce que le JIT ne connaît pas la longueur d'un
  tableau porté par un champ d'instance.
- **Correctif :** `[InlineArray(8)] struct x86regs { x86reg _e; }` dans `cpu_state_t`. Les
  indices constants perdent leur contrôle, et `& 7` le supprime pour les indices
  variables. Plus proche du `x86reg regs[8]` du C, mais hors R4 : accord requis.

### P6. Tableaux multidimensionnels

- **Sites :** `mod1add[2,8]` (`Cpu/808x.cs:302`, deux appels par adresse effective
  mémoire, 8088 et 286), `edatlookup[4,4]` (`Video/video.cs:139`, rendu VGA 16 couleurs),
  `svga_rotate[8,256]` (`Video/vid_svga.cs:230`), les tables de `io.cs:33-41`.
- **Mécanisme :** un tableau `[,]` échappe à l'élimination des contrôles de bornes.
- **Correctif :** aplatir, ce qui reprend le précédent `fontdat` de M5.1. Ajouter aussi
  `readonly` aux tables d'`io.cs`, sans DEVIATION.

### P7. Vidéo et hôte

- `cga_poll` (`Video/vid_cga.cs:270-384`) et `hline` (`Video/video.cs:834-841`) : passer
  par `Buffer32.AsSpan(ligne, n)` et `Fill`, avec un contrôle de bornes par caractère au
  lieu d'un par pixel.
- Rendus SVGA 4 bpp (`Video/vid_svga_render.cs:404-505`) : une seule lecture
  `ReadUInt32LittleEndian` de la VRAM (idem `vram_l`, `:497-499`), et les champs hissés en
  locales avant la boucle de `remap_func`.
- `pclog($"…")` interpolé dans `svga_write`/`writew`/`writel` (`vid_svga.cs:1091, 1128,
  1486, 1493, 1519, 1526`) : même gardé, il alourdit le prologue. Le sortir dans un
  assistant `[MethodImpl(NoInlining)]`.
- `ConsumeBlit` (`Host/SdlHost.cs:855-882`) fait deux copies (`Buffer32` → `_screen` →
  texture). Remonter directement les lignes `[y1,y2)` depuis `Buffer32`, ce qui corrige
  aussi D6. Code hôte libre.

### P8. Allocations en mode protégé

`Cpu/x86seg.cs:81, 377, 771, 1067, 1464, 1856`

- `new uint16_t[4]` est alloué à chaque chargement de segment, CALL/RET far, IRET et
  `pmodeint`. Chaud sous Windows 3.x, plus encore au 386.
- **Correctif :** `Span<uint16_t> segdat = stackalloc uint16_t[4];`, sans `unsafe`, plus
  fidèle au tableau local du C.

### P9. Divers, faibles

- `cpu_state.ea_seg = seg_ds` à chaque instruction (`Cpu/386.cs:306`) pose une barrière
  d'écriture GC. À vérifier au `JitDisasm` avant d'envisager un index.
- Hisser `x86_opcodes` en locale dans `exec386`. Sûr : le seul appel à `x86_setopcodes`
  est dans `cpu_set` (`Cpu/cpu.cs:277`), jamais en cours de tranche.
- `<ConcurrentGarbageCollection>false</ConcurrentGarbageCollection>` : gratuit, le
  programme n'alloue quasiment rien.
- `GetState` (`Cpu/808x.State.cs:208, 225`) alloue deux tableaux par instruction dans le
  diff et le fuzzeur ; `RamHash` (`:322-326`) fait un contrôle par octet. Outillage
  seulement.

### P10. `TieredCompilation=false`, à re-mesurer

- Désactiver le tiering désactive aussi la PGO dynamique, donc la dévirtualisation gardée
  des délégués (timers, `remap_func`, gestionnaires mémoire).
- Le −5 % de M5.2 a été mesuré sur le 8088, dont le cœur est un switch sans délégués. Le
  286 dépend beaucoup des délégués.
- Une mesure sur la charge Windows 3.1, **après** P2, tranchera.

### Timers, 7,4 %

`timer_enable` (`timer.cs:101-155`) insère dans une liste chaînée triée à chaque
réarmement (`pit_over`, `svga_poll`, `sound_poll` à 48 kHz). L'ordre des timers à échéance
égale compte pour l'oracle : il faut une conception et un oracle, pas une retouche.

## 3. Écarté, parce que déjà mesuré ou refusé

- Instancier `cpu_state` ou lui retirer `readonly` : +3 % mesuré, à ne pas faire.
- Statique contre instance pour la SVGA : aucun écart mesurable.
- Pointeurs de fonction `delegate*` : ils exigent `unsafe`, que l'engagement du csproj
  exclut.
- Découper `execx86` et le levier B : refusés à M5.1. Le point de blocage reste le même :
  au-delà de 922 locales, aucun helper n'est inliné (67 `fetchea`, 51 `FETCH`…).
- Le coût du thunk des délégués statiques à l'aiguillage : environ 0,3 ns par instruction
  au micro-banc (2,05 ns contre 1,73 ns), trop peu pour justifier un changement seul.
- Les `switch (rmdat & 0x38)` compilés en arbres de comparaisons (12 sites de `808x.cs`) :
  gain probablement marginal pour une réécriture lourde.
- Le nommage en minuscules et avec soulignés dans les dossiers transcrits : voulu par la
  règle verbatim.

## 4. Conventions .NET, côté hôte

Rien de bloquant :

- les poignées SDL sont des `IntPtr` avec un `Dispose` manuel, acceptable pour des
  ressources qui vivent autant que le processus ;
- l'identifiant `"com.example.ixtal26"` est resté à sa valeur fictive
  (`Host/SdlHost.cs:262`) ;
- `savenvr()` et `closepc()` s'exécutent dans `Dispose` même si `Init` a échoué
  (`Host/SdlHost.cs:1013-1014`). Sans effet aujourd'hui, mais fragile : un drapeau posé
  après `initpc` les garderait ;
- `SdlAudio.GiveBuffer` compte les blocs muets du turbo comme `BlocksDropped` : un
  compteur séparé distinguerait contre-pression et turbo.

## 5. Ordre proposé

1. D1 et D2 : ce sont des plantages de l'hôte.
2. P1 : 2,5 % mesurés, peu de code.
3. P3 : du C# ordinaire, rien qui sorte de R4.
4. P2 : avant d'écrire les formes 32 bits de G2.
5. Les leviers hors R4 (P4, P5), soumis à arbitrage un par un.
