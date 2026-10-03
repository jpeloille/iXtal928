# G9 — Vidéo, complément : MDA, Hercules, EGA, Tseng ET4000AX ; le plan, sur reconnaissance

> Écrit le 3 octobre 2026, après PS2. Bloc G9 de `PLAN.md` (décision utilisateur du 03/10, feu
> vert du même jour). Chaque constat cite la ligne de C qui le fonde, sur `pcem-dev/` tel que
> vendoré. Les décisions à trancher sont en fin de fichier ; rien ne s'écrit avant leur validation.

## Où on en est

Le dépôt a la CGA (M5), la VGA (M15), les deux Trident (M19), la GD5429 et la Trio64 (G7), le
socle SVGA (`vid_svga.c`, `vid_svga_render.c`) et le registre `VIDEO_CARD` réduit
(`Video/video.cs:289-347`, six cartes). `video_is_mda`, `video_is_cga`, `video_is_ega_vga` sont
transcrits (`video.cs:450-470`) et lus par les interrupteurs du 5150/XT
(`keyboard_xt.cs:147-149`, `:196-198`), le port d'entrée du 8042 (`keyboard_at.cs:785-787`, qui
dit aujourd'hui « video_is_mda() rend 0 dans ce dépôt ») et l'Amstrad. La police MDA
(`fontdatm`, `mda.rom`) est déjà chargée (`video.cs:500-560`). L'oracle monte ses cartes par
`h_video_cards[]` (`harness_stubs.c:511-517`) et lit les structures privées en incluant le `.c`
(`harness_tvga.c`, `harness_m24.c`). Le CMOS fabriqué (`--make-nvr`, `Host/NvrImage.cs:285`)
ne connaît que « CGA 80 » ou « carte à ROM » (00) : pas le monochrome (30h).

| | lignes | à transcrire | ROM | réutilise |
|---|---:|---:|---|---|
| `vid_mda.c` (+ `.h`) | 303 | ≈ 280 | aucune (police `mda.rom`) | `fontdatm`, `cgapal` |
| `vid_hercules.c` | 365 | ≈ 300, 85 % commun avec la MDA | aucune | idem |
| `vid_ega.c` (+ `.h`) | 1 123 | ≈ 1 000 | `ibm_6277356_ega_card_u44_27128.bin` (16 Ko) | `edatlookup` |
| `vid_et4000.c`, `et4000_device` seul | 630 | ≈ 215 | `et4000.bin` (32 Ko) | le socle SVGA entier |
| `vid_unk_ramdac.c` | 96 | ≈ 70 | — | — |

Le registre : `v_mda` (`video.c:140`, MDA, ISA 8/16/32), `v_hercules` (`:120`), `v_ega` (`:119`,
SPECIAL), `v_et4000ax` (`:189-190`, ISA 3/3/6 5/5/10).

**Hors G9** (`PLAN.md`) : les variantes coréennes de l'ET4000 (`et4000k_device`, Kasan,
`vid_et4000.c:22-31`, `:102-383`, `:432-483`, `:501-582`, `:614-630`), et toutes les cartes de la
liste d'exclusion. Ce PCem n'a pas d'ATI EGA Wonder dans `vid_ega.c` (seul `ega_device`, `:1121`) ;
`vid_pc1640.c` réutilise `ega_t`, la machine est absente.

## Ce que chaque carte fait, et ce qu'il faudra vérifier

- **MDA** (`mda_device`, `:303`) : 6845 aux ports 3B0-3BF (`:242`), 4 Ko de VRAM vus en B0000
  sur 32 Ko (`addr & 0xfff`, `:65-71`), texte 80×25 en 720×350 (caractères de 9 points),
  chronomètre `mda_poll` (`:274`). Configuration `display_type` : vert, ambre, blanc (défaut
  blanc, `:293-301`).
- **Hercules** (`hercules_device`, `:364`) : la MDA plus le graphique 720×348 en deux pages de
  32 Ko (3BF bit 1 ouvre B8000, `:66-72` ; VRAM 64 Ko, `& 0xffff`). Même configuration.
- **EGA** (`ega_device`, `:1121`) : ROM en C0000 (`rom_init`, fenêtre de 32 Ko, `:1040`, octets
  renversés `:1042-1051`) ; ports 3A0-3DF, 3Bx et 3Dx échangés en monochrome (`:26`, `:147`) ;
  A0000 sur 128 Ko remappé par GDC6 (`:100-113`) ; configuration `memory` 64/128/256 Ko (défaut
  256) et `monitor_type` (défaut 9, l'ECD ; 10 = monochrome) (`:1099-1119`).
- **ET4000AX** (`et4000_device`, `:610-612`) : le socle SVGA, 1 Mo fixe (`:493`), ROM 32 Ko
  (`:489`), `packed_chain4` (`:496`), banques (`:42-100`), horloges (`:406-419`), et le RAMDAC
  `unk_ramdac` (`vid_unk_ramdac.c`, un SC1502x HiColor) aux ports 3C6-3C9 (`vid_et4000.c:54-59`).

## Les défauts de PCem relevés (à lire à la ligne, puis inscrire)

1. **Le temps « hors affichage » négatif** : `mda_recalctimings` (`vid_mda.c:74-83`), celui
   d'Hercules (`:110-119`) et d'EGA (`:236-249`) convertissent `disptime − dispontime` en
   `uint64_t` sans borne quand R1 > R0 + 1. C'est **PB-36**, déjà reproduit pour la CGA : le
   chronomètre recule après la phase « off », la phase « on » le fait repartir, l'arithmétique
   modulo 2^64 retombe sur la période de ligne — **pas d'arrêt de l'hôte**, donc pas de R9. À
   reproduire (`(uint64_t)(int64_t)x`), PB-36 élargi.
2. **La police lue au-delà de 16 lignes** : `fontdatm[chr][sc]` avec `sc` jusqu'à 31 (`&= 31`,
   `vid_mda.c:160`, `:223` ; lectures `:119`, `:122` ; Hercules `:173`, `:176`) dans un tableau
   `[2048][16]` (`video.c:921`) : la lecture tombe dans le caractère suivant, jamais hors du
   tableau. En C#, `fontdatm` est `[2048, 16]` : l'accès doit se faire **à plat**
   (`chr * 16 + sc`) pour reproduire sans exception. À reproduire.
3. **Le 6845 sans masques** (MDA `:28`, `:55` ; Hercules `:89`) : tous les registres s'écrivent
   en entier et se relisent ; la vraie puce rend R0-R13 en écriture seule. Et le « Turbo-XT
   cursor hack » (`:29-34`, Hercules `:55-60`) réécrit R10/R11. À reproduire.
4. **L'entrelacé** : `sc = (sc << 1) & 7` (`vid_mda.c:99-100`) perd les lignes 8 et au-delà.
5. **Hercules, 3BA bit 7** (`:91`) : 1 pendant le retour vertical ; à vérifier contre la
   documentation (polarité inverse sur le vrai HGC ?), sans changer PCem.
6. **EGA : des traits de la VGA** (attribut 10h bit 7 et 14h, `:39-42` ; CR11 bit 7, `:128` ;
   CR10 bit 5, `:619` ; tous les registres relisibles, `:152-179`) ; le « `stat ^= 0x30` » du
   test du BIOS (`:182`) ; un rafraîchissement du texte seulement sur `fullchange` (`:559`) ;
   `egaswitchread` statique jamais remis à zéro (`:19`) — à remettre à l'amorçage des deux côtés
   (déviation de l'oracle, comme `opl[]` en G8). À reproduire, sauf la statique.
7. **EGA, la fenêtre de ROM** : un fichier de 16 Ko dans une fenêtre de 32 Ko (`:1040`,
   `rom.c:60-62`) : C4000-C7FFF est du tas. L'oracle a déjà `__wrap_rom_init`, qui efface la
   queue (G7.1, PB-24) ; le C# rend des zéros : identique.
8. **La VRAM non initialisée** (`malloc` sans `memset` : MDA `:239`, Hercules `:308`, EGA `:937` ;
   l'ET4000 passe par `svga_init`, déjà traité) : effacée à l'amorçage côté oracle, comme pour la
   M24 et le socle SVGA ; le C# a des tableaux nuls.
9. **ET4000** : `banked_mask` laissé à 0 par `svga_init` jusqu'au premier choix de mode
   (`:72-75`) ; `crtc_mask` qui efface 38h-3Fh, CR3F compris (`:34-37`, donc `:399` mort) ; CR13
   nul forcé à 256 (`:397-398`) ; pas de séquence KEY (registres étendus toujours ouverts). À
   reproduire.
10. **`unk_ramdac`** : l'écriture de FFh une fois armé saute le registre de commande et tombe
    dans `svga_out` (`:24`) ; le décodage des profondeurs comprend 32 bits, que le SC1502x n'a
    pas (`:27-61`). À reproduire.

**R9** : aucun `fatal()` atteignable, aucun accès hors tableau que l'invité commande (VRAM
masquée, `charaddr` ≤ 0x37FFE dans 0x40000, écritures de `buffer32` dans le bloc contigu, comme
PB-89). La police (n° 2) est le seul piège, et il est propre au C#.

## Les étapes

### G9.0 — La MDA  ✅ *fait, VERIFICATION.md § G9.0*

`vid_mda.c` des deux côtés : `Video/vid_mda.cs` ; l'oracle inclut `vid_mda.c` (`harness_mda.c`)
pour la sonde (registres, VRAM 4 Ko, état du balayage) ; `v_mda` dans les registres ; le
monochrome (30h) dans `--make-nvr` ; `keyboard_at.cs:785` cesse de dire « rend 0 ». **Portes** :
boot-diffs 5150 et XT, POST et DOS (la boucle `for g in …` de la série gagne `mda`), l'IBM AT
avec la MDA ; un contrôle négatif. La correction de la phrase sur les ROM PS/2 (VERIFICATION
§ PS2.1, PLAN-PS2.md) entre dans ce commit.

### G9.1 — L'Hercules  ✅ *fait, VERIFICATION.md § G9.1*

`vid_hercules.c` (le code commun à la MDA reste deux fichiers, comme chez PCem). **Portes** :
boot-diffs 5150/XT ; un banc HERCBANC (.COM saisi dans DEBUG) qui passe en graphique (3BF, 3B8,
le 6845 du mode 720×348), écrit les deux pages et relit 3BA — sonde comparée. **Témoin** :
Windows 3.11 et `HERCULES.DRV` (disquette 2) sur un AT avec l'Hercules, par le SETUP DOS dans
une copie `/tmp` d'un disque de l'utilisateur.

### G9.2 — L'EGA  ✅ *fait, VERIFICATION.md § G9.2*

`vid_ega.c` (la ROM, 64/128/256 Ko, les moniteurs) ; sonde EGA (registres, palette, VRAM, trame).
**Portes** : boot-diffs 5150, XT, IBM AT, ami286, ami386 ; le POST, le DOS, un banc des modes
graphiques (0Dh, 0Eh, 10h ; modes d'écriture 0-2, plans, rotation) ; un contrôle négatif.
**Témoins** : Windows 3.11 en `EGA.DRV` (disquette 2) sur un 286/386 ; Yeager Air Combat
(`os/yeager.img`) s'il tourne en EGA.

### G9.3 — La Tseng ET4000AX et son RAMDAC  ✅ *fait, VERIFICATION.md § G9.3*

`vid_et4000.c` (`et4000_device` seul) et `vid_unk_ramdac.c` ; sonde SVGA + champs ET4000 (banques,
CR3x, RAMDAC). **Portes** : boot-diffs ami386dx/ami486 (POST, DOS) ; un banc des modes 256
couleurs de la ROM (2Eh 640×480, 30h 800×600, par INT 10h) et des banques ; R9 si la lecture en
découvre. Les boot-diffs sur l'**ami286** et l'**ami386dx** (des machines sans VLB : c'est l'intérêt
d'une carte ISA), et l'ami486. **Témoin** : Windows 3.11 en 256 couleurs par `SVGA256.DRV` —
décision n° 3.

## La vérification

- **Diff d'instructions** : ce que le BIOS et les programmes lisent des cartes (6845, 3BA/3DA,
  registres EGA et ET4000, ROM).
- **Sondes de fin de boot-diff** : MDA/Hercules (comme la M24), EGA (nouvelle), SVGA (ET4000).
- **Bancs dirigés** sous boot-diff, comme SBBANC et PS2BANC.

## Les décisions à trancher

1. **L'ordre** : MDA → Hercules → EGA → ET4000 (`PLAN.md`) *(validé)*.
2. **Les machines des portes** : MDA et Hercules sur le 5150, le XT et l'IBM AT ; l'EGA sur le
   5150, le XT, l'IBM AT, l'ami286, l'ami386 ; l'ET4000, carte ISA, sur l'ami286 et l'ami386dx
   (sans VLB), et l'ami486. Aucun profil ne change : les cartes se choisissent par `gfxcard`.
   *(Validé le 03/10, l'ET4000 corrigée par l'orchestrateur.)*
3. **Le témoin Windows 256 couleurs de l'ET4000** : Windows 3.11 porte les pilotes génériques
   `SVGA256.DR_` et `SUPERVGA.DR_` (disquette 2), qui passent par le BIOS VESA. *(Validé le 03/10,
   corrigé par l'orchestrateur : ma première lecture de la disquette les avait manqués.)* On
   mesure d'abord si `et4000.bin` rend les services VESA (INT 10h 4F00h, 4F01h, 4F02h) ; s'il les
   rend, le témoin est SVGA256 sur une copie `/tmp` ; sinon, c'est un constat (l'ET4000AX
   d'époque passait par un TSR VESA) : banc 256 couleurs par les modes Tseng natifs (2Eh…),
   Windows en VGA, limite dite. Pas de disquette à demander.
4. **Les états à remettre à zéro à l'amorçage** (VRAM des trois cartes, `egaswitchread`) :
   des deux côtés, déviation de l'oracle comme `svga_init` et la VRAM de la M24 *(validé)*.
5. **La configuration** : les défauts de PCem (MDA/Hercules blanc ; EGA 256 Ko, ECD) ; les
   autres valeurs restent accessibles par la section du device (G8.3), validées contre leurs
   listes *(validé)*.
6. **Ce fichier** : `PLAN-G9.md`.
