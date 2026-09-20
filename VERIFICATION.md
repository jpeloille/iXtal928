# Journal de vérification

Le registre des **mesures**. TRANSCRIPTION.md porte les règles et les conventions — un
document borné, qui ne bouge que quand une règle change. Celui-ci porte ce qu'on a
constaté : résultats de portes, divergences, injections de panne. Il grandit à chaque
jalon, et c'est normal.

Les défauts trouvés dans **PCem lui-même** ont leur propre registre, `PCEM_BUGS.md`,
qui leur donne des identifiants stables `PB-nn`.

La séparation n'est pas cosmétique : le plafond de 200 lignes de TRANSCRIPTION.md est
l'anticorps contre la prose de conception qui a tué le projet précédent. Une mesure n'est
pas de la prose, et elle ne doit pas être rationnée.

---

## Où PCem s'écarte du silicium

Chiffres dans **`sst-baseline.tsv`**, généré par
`iXtal26.Diff sst-probe --baseline` : c'est de la donnée, pas de la prose, et elle grandira
à chaque forme sondée. **Le critère n'est pas « 100 % de SST » mais « le C# reproduit la
colonne `passe` à l'identique ».**

Sondé sur un échantillon *volontairement adverse* (2 000 cas × 19 formes, les opcodes les
plus retors) : **100 % sur tout ce qui est ordinaire** — ADD, MOV, NOP, MOVSB, CALL, INC,
MUL, SHL 1, AAA/AAS. Les masques de `metadata.json` sont indispensables : `37` passe de
117/2000 à 2000/2000 une fois appliqué.

Cinq écarts réels, à transcrire tels quels et à marquer `// pcem bug, reproduced:` —
`F6.6`/`F7.6` DIV (**débordement de quotient non détecté** : vérifié à la main sur
`F6.6[2]`, AX=36562 ÷ 12 = 3046 > 255, le 8088 lève INT 0, PCem tronque à 0xE6 et
poursuit) · `D0.6` SETMO (opcode non documenté, non implémenté) · `AD` REP LODSW ·
`D2.4` SHL par CL (un seul bit : **OF**) · `27`/`2F` DAA/DAS (AL, ~1–2 % des cas).

Performance relevée : 38 000 cas en 61 s, dominés par `h_reset()` qui remet à `-1` les 16 Mo
de `readlookup2`/`writelookup2` à chaque cas — ~85 min pour le corpus complet contre 2 min
visées. Le harnais devra réinitialiser ces tables paresseusement.

> **Constat de M0, à lire avec § M5.0.** Ce paragraphe est exact *pour les données de M0* :
> `git show 60cf953:sst-baseline.tsv` porte bien 19 formes, et les cinq écarts qu'il
> énumère sont les cinq que ces 19 formes révèlent. Ce qui a changé depuis :
> (1) M1.2 a ajouté les douze formes ADC/SBB `10`–`1D`, qui forment une **sixième** famille
> — aujourd'hui `PB-01` — sans que cette prose soit reprise ; les défauts de PCem ont
> depuis leur registre propre, `PCEM_BUGS.md`. (2) La ligne de base couvre les 84 formes
> depuis M5.0, contre 67 entre M1.2 et M5.0. (3) La réinitialisation paresseuse de
> `h_reset()` n'a jamais eu à être faite : à 2 000 cas par forme, les 84 formes passent
> sans qu'on y touche.

---

## Résultats des portes M0

Mesurés le 2026-09-19, .NET 10.0.112, x86-64 Linux.

| Porte | Résultat |
|---|---|
| **G1** propriété `ref` | **Verte.** `cycles -= 3;`, passage `ref`, `tsc += 0x1_0000_0000UL` mutent bien le champ référencé. |
| **G2** switch géant + `goto` | **Verte.** **180 106 octets d'IL** pour 256 cas / ~13 900 lignes ; JIT propre en Debug, Release, et sous `DOTNET_TieredCompilation=0` (pas de bailout). Le switch de `808x.c` fait ~2 600 lignes, soit ~1/5 : **marge 5×**, la contingence « découper par quartet » est inutile. |
| **G3** union explicite | **Verte.** Aliasing `l`/`w`/`b.h`/`b.l`, écriture de demi-octet préservant le reste, `getr8`/`setr8` indexés avec le pliage haut/bas sur le bit 2, et absence de mutation à travers une copie de struct. |
| **G4** SDL3-CS sous net10 | **Verte.** Restaure et compile ; ouvre une fenêtre, rend des images, sort 0. La commande d'origine était `dotnet run -- --frames 5` ; `--frames` a été retiré à M4.3 (une tranche vaut 10 ms émulées, pas une image — voir Program.cs). Rejouer avec `dotnet run -- --slices 400`. |

### Le harnais attrape-t-il vraiment ? (M1.0b)

Un fuzzer vert ne prouve rien tant qu'on n'a pas vérifié qu'il sait rougir. Trois
pannes injectées dans `808x.cs`, puis révoquées — **les trois sorties sur la première
instruction**, en nommant le bon champ :

| Panne injectée | Détectée |
|---|---|
| `cycles -= 8` → `-= 9` (coût de l'opcode indéfini) | `cycles consommés : oracle 16, C# 17` |
| `fetchcycles = 4` → `= 5` (rechargement de la file) | `cycles consommés : oracle 16, C# 15` |
| suppression de `fetchclocks += …` dans `FETCH` | `fetchcycles : oracle 12, C# 16` |

Passe verte de référence : **200 000 instructions** (2 000 rondes x 100), 32 champs
comparés, zéro divergence, 17 s. Le hachage de la RAM se fait en fin de ronde et non par
instruction — 1 Mo par côté et par instruction représentait des centaines de Go et rendait
la passe interminable ; `--ram-per-instr` le rétablit pour localiser une divergence.

**Point aveugle mesuré, à lever en M1.1.** La quatrième panne — supprimer la garde
`if (a != (cs + cpu_state.pc))` sur `memcycs += 4` — **n'est PAS détectée** : 900
instructions vertes. `0xCE` ne passe que par `readmembf`, qui n'a ni garde ni
comptabilisation ; `readmemb` n'est jamais atteinte. C'est exactement l'accord vide que
les compteurs de stubs traquent ailleurs. **Le premier opcode à opérande mémoire de M1.1
doit rejouer cette injection avant d'être déclaré fini.**

L'échafaudage (`M0Gates.cs`, `M0Gates.Generated.cs`, le drapeau `--gates`) est supprimé
une fois ces résultats consignés. Il est regénérable depuis l'historique git si une
régression de runtime remet G2 en question.

---

## M4.0 — Diff de traces d'amorçage : oracle contre C#, depuis le reset

Le fuzzer compare une instruction sur un état fabriqué ; SST compare une instruction
contre du silicium. Ni l'un ni l'autre ne fait tourner une **machine**. Le troisième
oracle est le diff de traces : les deux cœurs amorcent la même ROM d'IBM PC 5150 et on
compare instruction par instruction.

Deux phases, comme prévu au plan. Phase 1 : un hachage FNV-1a de 8 octets par instruction
(CS, pc, les huit registres, DS/ES/SS, flags, tsc) — assez léger pour tracer 700 000
instructions des deux côtés. Phase 2 : rejeu en pas à pas jusqu'à l'index fautif, avec le
vecteur d'état complet des 32 champs.

**Ce que la phase 2 a dû apprendre à ne pas comparer.** Les deux premières exécutions se
sont arrêtées sur `n_inb` puis sur `ins` — des compteurs de diagnostic, absents du
hachage de phase 1, donc *incapables* d'être la divergence cherchée. Ils divergeaient
parce que `h_boot()` et `initpc()` ne les remettaient pas à zéro au même moment, et le
diff s'arrêtait sur son propre bruit avant d'atteindre la vraie panne. Corrigé des deux
côtés (`h_stub_counters_reset` dans `h_boot`, `_808x.ResetDiagState()` symétrique) et
`CompareStates(counters: false)` pour le boot.

### Ce que le diff a trouvé : le PIT ne tournait pas du tout

Première divergence architecturale à l'instruction **24 664**, `F000:E0E8`, `IN AL, 0x41` :
oracle `0x00`, iXtal26 `0xFB`. Une sonde ajoutée sur les trois canaux du PIT (`h_pit_probe`
lit la globale `pit` de `pit.c` — le C n'est pas instrumenté, il est lu) a reporté le
premier écart **24 598 instructions plus tôt**, à l'instruction 66, `F000:E0C1`,
`OUT 0x61, 0xFC` : `pit.count[2]`, oracle 0, C# 65535.

État d'entrée identique des deux côtés. Les deux exécutent la même branche de
`pit_set_gate_no_timer`, et `pit.timer[2].ts` finit identique. Seul `timer_enable()`
diverge — parce que le C commence par `if (!timer_valid(timer)) return;` et que le C#
n'avait pas ce test.

Deux défauts distincts, tous deux confirmés par la source :

1. **`timer.cs` omettait la couche de validité** — `magic`, `all_timers[]`, `num_timers`,
   `timer_valid()`. Je l'avais classée « détection d'use-after-free, inatteignable sous
   GC ». C'est faux : `timer_reset()` remet `magic` à zéro sur **tous** les chronomètres
   enregistrés (`timer.c:162`), et `timer_valid` les rend alors inertes jusqu'au prochain
   `timer_add()`. C'est du contrôle de flux vivant, pas du durcissement.

2. **`pc_reset()` appelait `timer_reset()`** — que `pc.c:178` porte **en commentaire**.
   Combiné au défaut n°1, il invalidait tous les chronomètres que `model_init()` venait
   d'enregistrer : la machine tournait sans PIT, donc sans IRQ0 et sans rafraîchissement
   DRAM. Symétriquement, `setpitclock(14318184.0)` appartient à `pc_reset()`
   (`pc.c:184-187`), donc **après** `pit_init()`, et non à `initpc()` où je l'avais mis.

Le second défaut seul faisait disparaître le symptôme. Le premier est une lacune de
transcription : sans lui, la divergence serait revenue plus loin, au prochain
`resetpchard()`.

### Mesure

| | |
|---|---|
| Amorçage 20 tranches (0,2 s émulée) | **88 459 instructions, identiques** |
| Amorçage 200 tranches (2 s émulée) | **714 879 instructions, identiques** |
| Champs comparés par instruction (phase 2) | 32, plus 19 champs de sonde PIT par canal |

Le nombre d'instructions par tranche a changé avec le correctif (87 611 → 88 459 sur 20
tranches) : c'est la signature du PIT qui se met enfin à tourner.

**Ce que cette mesure ne prouve pas.** Le fuzzer n'a jamais pu attraper ce défaut et ne le
pourra jamais : il passe par `FlatMap()`/`Reset()`, qui n'appelle `timer_add` qu'une fois
par processus et jamais `timer_reset()` après enregistrement. SST ne touche pas aux
chronomètres. Un fuzzer vert après ce correctif ne dit donc rien sur ce correctif — seule
la longueur du préfixe d'amorçage identique le mesure.

### Un angle mort du fuzzer, trouvé en voulant l'utiliser

La passe de non-régression de ce correctif n'a pas rendu de verdict : 40 minutes à
100 % de processeur, tuée par son propre délai. Le débit réel est pourtant de
2 700 instructions/s — 120 000 auraient dû prendre 45 s.

Bissection : la ronde **157** ne termine jamais. Le mode flux remplit **toute** la RAM
d'un seul opcode ; quand cet opcode est l'un des **quatre préfixes de segment**
(`0x26`, `0x2E`, `0x36`, `0x3E`), le flux est un préfixe infini et aucune instruction
n'est jamais retirée. `goto opcodestart` (`808x.c:1589/1664/1739/1798`) saute **dans**
le corps de la boucle : la condition `while (cycles > 0)` n'est jamais réévaluée.

Ce n'est pas un défaut de transcription. Les deux cœurs bouclent, d'accord entre eux, et
c'est aussi ce que ferait un 8088 réel — les préfixes bloquent les interruptions, la
séquence ne retire rien. C'est le **remplissage** du fuzzer qui est dégénéré, et la
conséquence est pire qu'un faux positif : avec 256 opcodes et 1 200 rondes, une
vingtaine de rondes sont vouées à bloquer, donc **le mode flux n'a jamais exercé un seul
préfixe de segment** — il s'arrêtait dessus. Le mode simple les couvrait
(`Fuzzer.cs:107-115`), pas le mode flux.

Corrigé par un remplissage à motif de deux octets, `h_fill_ram2` / `mem.fill_ram2` :
préfixe, opcode réel, préfixe, opcode réel. Le chemin de préfixe est exercé et la ronde
termine.

### Et derrière l'angle mort : PCem lit hors de son allocation

Le mode flux, enfin capable de dépasser la ronde 157, a trouvé une divergence à la
ronde **1691** : `POP CX` avec `SS = 0xFFFF`, `SP = 0x000F`. Oracle `CX = 0x0E59`,
iXtal26 `0x0059`.

Le mot lu est à cheval sur `0xFFFFF` / `0x100000`. `mem_alloc()` alloue `ram` à
**exactement** `mem_size` Ko (`mem.c:1344`), et `readmemw` (`808x.c:78`) déréférence un
`uint16_t *` à `readlookup2[..] + s + a` : l'octet haut est lu **un octet au-delà de
l'allocation**. Trois exécutions du même cas rendent `0x0E59`, `0x4959`, `0xD859` — du
tas adjacent.

Il n'y a rien là dont iXtal26 puisse être le pendant fidèle. `mem_alloc()` côté C#
alloue déjà quatre octets de marge (`mem.cs:706`), donc l'octet haut y vaut zéro, de
façon déterministe.

**Première tentative, et pourquoi elle était fausse.** J'ai d'abord écrit un test
général : avant de conclure au défaut de transcription, rejouer la ronde deux fois sur
le seul oracle et vérifier qu'il s'accorde avec lui-même. L'idée est bonne, le test ne
l'est pas — **il n'a pas déclenché**. Dans un même processus l'allocateur rend le même
bloc et l'octet voisin garde sa valeur ; la variation n'apparaît qu'entre processus. Un
test qui rate le cas pour lequel il a été écrit ne mérite pas de rester comme
rassurance : il a été retiré.

**Retenu.** `h_pad_ram()` (`tools/oracle/harness.c`) réalloue `ram` avec quatre octets à
zéro après chaque `mem_alloc()`, et rebase `pages[].mem` — qui pointe dans `ram`
(`mem.c:1357`) et par lequel passe toute écriture (`mem_write_ramb_page`, `mem.c:877`).
L'arbre vendoré n'est pas modifié ; seul l'état global de `mem.c` est réécrit après
coup, ce que le harnais fait déjà pour la carte mémoire plate.

C'est une **déviation assumée de l'oracle**, la première du dépôt : l'oracle n'est plus
PCem exactement. Elle se justifie du fait que le comportement remplacé n'est pas un
comportement — c'est de l'UB — et qu'un oracle qui tire aux dés n'est pas un oracle. À
noter pour plus tard : un 8088 réel, avec ses vingt lignes d'adresse, ferait reboucler
`0x100000` sur `0x00000`. Ni PCem ni iXtal26 ne le font. C'est à SST de trancher, pas à
un diff de transcription.

| | |
|---|---|
| Fuzzer, mode flux, 256 opcodes | **400 000 instructions, zéro divergence**, 47 s |
| Amorçage 200 tranches, après padding | **714 879 instructions, identiques** |

### M4.1 — L'IBM PC 5150 démarre sur Cassette BASIC

Soixante secondes émulées, 24 073 823 instructions, `--boot roms 6000` :

```
--- écran texte CGA (B800:0000, 80x25) ---
  |The IBM Personal Computer Basic
  |Version C1.10 Copyright IBM Corp 1981
  |62940 Bytes free
  |Ok
  [20 ligne(s) vide(s)]
  |1LIST   2RUN    3LOAD"  4SAVE"  5CONT   6,"LPT1 7TRON   8TROFF  9KEY    0SCREEN
```

Le POST va au bout : mot d'équipement `0040:0010 = 006D` (CGA 80x25, une disquette),
taille mémoire `0040:0013 = 640 Ko` après le test à motifs qui occupe les tranches 150
à 5400. Faute d'unité amorçable, le BIOS bascule sur l'INT 18h et la ROM BASIC prend la
main ; elle est à son invite, dans la boucle d'attente clavier de l'INT 16h
(`F000:E84D`, qui lit `0040:001A`/`001C`).

Le vidage écran lit `0xB8000` **à travers la carte mémoire** (`mem_readb_phys`), pas le
tableau `ram` : c'est le chemin que prendrait un vrai accès, et il vérifie donc au
passage que la fenêtre CGA est bien mappée.

### M4.2 — L'oracle n'avait pas de carte vidéo

Le diff poussé jusqu'à l'invite BASIC s'arrête à l'instruction **801 677**,
`F000:F173`, `REP STOSW` de 16 Ko vers `B800:0000` — l'effacement de l'écran, avec
`DX = 0x03D4` (l'index du CRTC). Oracle **81 924** cycles, iXtal26 **147 462**.

Ce n'est pas un écart de transcription. L'oracle ne liait **aucun** fichier de `src/video/` :
son `0xB8000` est de la RAM ordinaire, tandis que celui d'iXtal26 traverse la carte CGA et
ses états d'attente. Les deux harnais modélisaient des machines différentes, et c'est
iXtal26 qui était le plus complet des deux.

**Un piège de l'édition de liens, au passage.** J'ai d'abord ajouté `src/video/video.c` et
le `.so` s'est lié « proprement ». Il ne l'était pas : `-shared` tolère les symboles non
résolus, et le `.so` portait **quatre-vingt-dix** trous — tout le registre `VIDEO_CARD`.
Seul le lien de `selftest`, qui est un exécutable, l'a révélé. Un appel à l'un de ces
symboles aurait sauté à l'adresse nulle des mois plus tard. Le Makefile porte désormais
`-Wl,--no-undefined` : la classe entière de faute échoue maintenant à la construction.

Retenu : lier `vid_cga.c` seul, et fournir les globales et frontières hôte de `video.c` dans
`harness_stubs.c`, en miroir de `Video/video.cs`. La section distingue explicitement deux
niveaux — ce qui porte du **temps** (`buffer32` et sa géométrie 2048x2048, `hline` :
`cga_poll` y écrit à chaque balayage, un pointeur nul planterait) et ce qui ne porte que des
**pixels** (`cgapal`, `fontdat`, chemin composite), stubé à zéro et à reprendre avant
l'oracle de framebuffer.

### Mesure d'ensemble, palier (a)

| Oracle | Portée | Résultat |
|---|---|---|
| Diff différentiel, mode flux, 256 opcodes | transcription | **300 000 à 400 000 instructions, zéro divergence** |
| Diff de traces d'amorçage, 6 000 tranches | la machine | **24 944 866 instructions identiques**, du vecteur de reset à l'invite BASIC |
| Autotest de l'oracle | h_step ≡ h_run | vert, 25 contrôles |

Les deux cœurs exécutent le POST complet de l'IBM PC 5150, le test mémoire de 640 Ko, la
bascule INT 18h et la ROM BASIC **sans un seul bit d'écart** sur les 32 champs — registres,
segments, drapeaux, et tout le modèle de temps : `cycles`, `tsc`, `tsc_frac`, `memcycs`,
`fetchcycles`, `fetchclocks`, `nextcyc`, `cycdiff`, `prefetchw`, `prefetchpc`, la file de
préfetch.

### Ce que le vidage texte NE prouvait pas

Le vidage de `0xB8000` lit la VRAM **à travers la carte mémoire**. C'est un chemin
entièrement distinct de `cga_poll`, qui dessine dans `Buffer32`. Des caractères justes en
VRAM ne disent donc rien sur le rendu — et jusqu'ici ce second chemin n'avait **jamais**
été exécuté par aucun test.

Mesuré (`--boot roms 6000`) :

```
framebuffer : 4194304 pixels, 46576 non nuls, dernière ligne touchée 237
  xsize=656 ysize=200 res=80x25 frames=1742
```

`cga_poll` tourne et dessine : 656x200 est la géométrie CGA 80x25 exacte (640 + 16 de
bordure). `frames=1742` pour 60 s émulées, là où 59,92 Hz en donnerait ~3 595 : `frames++`
est gardé par `if (cga->crtc[7])` (`vid_cga.c:368`), donc rien n'est compté tant que le BIOS
n'a pas programmé le registre 7 du CRTC. Pas d'anomalie.

**Ce qui reste invérifiable pour l'instant** : le contenu des pixels. `cgapal` et `fontdat`
sont stubés à zéro côté oracle, donc son framebuffer est noir. L'oracle de framebuffer du
plan (§ Vérification 3) exige d'abord un `cgapal_rebuild` et un `loadfont` réels dans le
harnais.

## M4.3 — L'hôte SDL3, et la police que personne ne regardait

La machine tournait depuis M4.1 sans jamais s'afficher : `Game.cs`, l'échafaudage de démo
du gabarit de projet, était resté le point d'entrée. `Host/SdlHost.cs` (fenêtre, texture,
accumulateur horloge murale, remontée du blit), `Host/SdlKeyboard.cs` (105 entrées de
`SDLScancodeToSystemScancode`) et une table d'arguments refaite le remplacent.

### Le défaut que la fenêtre a révélé en trente secondes

**La police CGA était intégralement corrompue.** Un seul glyphe se répétait sur toute la
grille 80x25, espaces compris.

`mem_bios.c:59-62` appelle `loadfont` quatre fois de suite, sans condition : `mda.rom`
(FONT_MDA), `wy700.rom` (FONT_WY700), `8x12.bin` (FONT_MDSI), `im1024font.bin`
(FONT_IM1024). Dans le C, ces formats écrivent chacun dans **leur** table — `fontdatw`,
`fontdat8x12` — et `default:` n'est accolé qu'à `case FONT_CGA` (`video.c:975-976`).

J'avais omis ces tables, ce qui est légitime, **et leurs étiquettes de `case` avec elles**,
ce qui ne l'est pas : les sept formats tombaient alors dans `default` = FONT_CGA. Donc
`mda.rom` remplissait `fontdat` correctement, puis `wy700.rom` (16 384 o = 2048 x 8)
l'écrasait *intégralement*, puis `8x12.bin` (4 096 o) écrasait les 512 premiers caractères
et laissait `0xFF` sur le reste — `FileStream.ReadByte()` rend −1 en fin de fichier, que le
cast en `uint8_t` transforme en `0xFF`.

Le plus instructif : **mon propre commentaire d'omission énonçait déjà la conséquence** —
« ces sept formats tombent maintenant dans `default`, c'est-à-dire FONT_CGA ». Le registre
disait ce qui allait se passer, et personne, moi compris, ne l'a lu comme un défaut.

| | avant | après |
|---|---|---|
| Pixels non nuls, écran BASIC (656x200) | 46 576 (35,5 %) | **3 997 (3,0 %)** |

**Troisième angle mort, disjoint des deux déjà consignés.** La police ne touche aucun état
CPU : les 24 944 866 instructions du diff d'amorçage restent identiques, et le vidage texte
de `BootTest` montre la bannière BASIC juste — parce que la VRAM *est* juste. Seul le chemin
pixel était faux, et il n'a eu de lecteur qu'à l'ouverture de la fenêtre. Le chiffre de
46 576 consigné en M4.2 mesurait donc une police corrompue ; sa chute est un progrès.

### Le drapeau de blit perdait des images

`video_blit_memtoscreen` posait `blit_pending`, que l'hôte consultait une fois par tranche
de 10 ms. Un drapeau n'est pas une file : deux blits dans la même tranche n'en faisaient
qu'un. Mesuré sur un amorçage nu, **2 images perdues sur 486**, au transitoire de
reprogrammation du CRTC.

PCem ne peut pas en perdre : `video_blit_memtoscreen` appelle `video_wait_for_blit()`
(`video.c:1150-1153`), qui **bloque** le fil d'émulation tant que `blit_data.busy` vaut 1.
Le crochet `video_blit_memtoscreen_func`, déclaré mais jamais appelé, est maintenant invoqué
**synchroniquement** : quand il rend la main, le rectangle est remonté. Mono-thread, c'est
exactement la propriété du handshake d'origine.

### Autres écarts corrigés, tous relevés en relecture croisée

| Constat | Correction |
|---|---|
| Fenêtre au contenu indéfini pendant les ~3,5 s précédant le premier blit | `Render()` dès `Init()` |
| VSync forcée à 1, présentation sur le fil d'émulation | VSync à 0 — `video_vsync = 0` est le défaut de PCem (`wx-sdl2-video.c:30`). Forcée, `RenderPresent` bloquait au milieu de `drawits` et le retard partait dans `if (drawits > 50) drawits = 0`, sans diagnostic |
| Indicateur de vitesse en moyenne **cumulée** | Fenêtre **glissante** — `onesec()` (`pc.c:168-174`) remet `framecount` à zéro chaque seconde réelle. Cumulée, une chute à 50 % après dix minutes se serait lue « 97 % » |
| `w` non borné par la texture | Borné. `xsize` vient du registre 1 du CRTC : un invité y écrivant `0xFF` en 40 colonnes donne 4096, le double de la texture |
| `Run()` rendait 0 même si aucune image n'atteignait la texture | Rend 1 |
| `--boot roms 60O` exécutait 20 tranches et sortait 0 | Rend 2 avec un message |
| « l'écart est le nombre d'images perdues » | Faux : `video_frames` est incrémenté avant le `if (h <= 0) return` |

### Mesure

| | |
|---|---|
| Fenêtre X11, amorçage complet | **IBM Cassette BASIC C1.10 lisible à l'écran, 100 % du temps réel** |
| Blits, 400 tranches | 16 émis, 15 consommés, 15 téléversés, **0 en échec**, 0 perdu |
| Diff d'amorçage, 200 tranches | 714 879 instructions identiques |
| Fuzzer, 256 opcodes | 300 000 instructions, zéro divergence |
| `check-oracle.sh` | 16 vérifiés, 0 dérive, arbre vendoré OK |

**Non exercé** : le clavier. Aucune frappe n'a encore été envoyée à la machine — le POST du
5150 n'en attend aucune, donc rien dans ces exécutions ne traverse `keyboard_poll_host()`.
La table et `Reset()` ne sont validés que par lecture et compilation.

### M4.4 — Le clavier, vérifié sans gestionnaire de fenêtres

M4.3 laissait le chemin clavier non exercé. Les tentatives d'injection par `xdotool` dans
la fenêtre ont toutes échoué, et le compteur ajouté pour l'occasion dit pourquoi :

```
évènements : 26 reçus de SDL, dernier WindowExposed
clavier    : 0 KeyDown reçus de SDL, 0 mappés ; dernier scancode Unknown -> -1
```

Le pompage d'évènements fonctionne — 26 évènements de fenêtre arrivent — mais aucune
touche : la fenêtre n'a jamais obtenu le focus clavier sous ce compositeur. Ça ne
départage pas « mon code est faux » de « mon injection n'arrive pas », et un gestionnaire
de fenêtres n'est pas un oracle.

D'où `--boot roms N --type "TEXTE"` : l'injection écrit dans `keyboard.rawinputkey[]` —
**exactement** le tableau que `Host/SdlKeyboard` remplit — et passe par la même
`MapScancode`. Seule la livraison SDL est court-circuitée ; tout le reste est exercé pour
de vrai.

```
--- frappe de « PRINT 6*7 » puis Entrée ---
  |Ok
  |PRINT 6*7
  | 42
  |Ok
```

`keyboard_poll_host` → `keyboard_process` → `scancode_xt` → `keyboard_xt` → IRQ 1 →
INT 9 du BIOS → tampon clavier de la BDA → INT 16h → l'analyseur de BASIC, qui évalue et
imprime. Déterministe, reproductible, sans fenêtre.

**Ce que la première tentative a appris.** Elle rendait `print 687` au lieu de `PRINT 6*7` :
Maj était sans effet. `keyboard_process` balaie `pcem_key[]` de l'indice 0 à 271 et émet
dans cet ordre — appuyer Maj (0x2A) et « 8 » (0x09) dans la MÊME passe envoie le scancode
de « 8 » avant celui de Maj, et l'INT 9 lit un « 8 ». Un humain n'a jamais ce problème : il
appuie sur Maj un scrutin plus tôt. Défaut du harnais, pas de l'émulateur — mais il fallait
le mesurer pour le savoir, et c'est consigné au site de correction.

### M4.5 — L'amorçage dépendait du répertoire d'où l'on lançait

Lancé depuis Rider, le binaire rendait 1 sur `Failed to load ROM!`. Les ROMs étaient
pourtant là, intactes, et la même commande depuis la racine du dépôt amorçait jusqu'à
BASIC. **L'échec dépendait de l'endroit d'où on lançait, pas de ce qu'on lançait** — la
pire classe de bogue à diagnostiquer, parce que la première chose qu'on fait pour le
reproduire, `cd` à la racine, est précisément ce qui le masque.

La chaîne, du symptôme à la cause :

| Maillon | Ce qui se passait |
|---|---|
| Rider (`WORKING_DIRECTORY` vide) | répertoire courant = `bin/Debug/net10.0/` |
| `Program.cs` | `romsPath = "roms"`, chemin **relatif**, jamais ancré |
| `set_roms_paths` (`paths.c:62-88`) | `dir_exists("roms/")` faux → chemin **écarté en silence**, `num_roms_paths = 0` |
| `romfopen` (`rom.c:10-24`) | boucle `for (i = 0; i < num_roms_paths; ++i)` → **zéro itération**, rend NULL |
| `loadbios` | accuse les ROMs, alors que le répertoire entier manquait |

Le dépôt contenait déjà la réponse, jamais appelée : `paths_init()` (`paths.c:190-216`)
remonte depuis l'emplacement du binaire jusqu'au premier répertoire contenant `roms/`.
`grep` confirme zéro appelant — écrite, commentée, morte.

**La résolution est faite dans `Program.cs`, pas dans `initpc`.** C'est l'hôte qui sait
d'où il a été lancé ; `initpc` ne fait que consommer le chemin qu'on lui tend. La placer
dans `initpc` l'aurait aussi imposée à `BootDiff`, qui doit garder le chemin **tel quel**
(voir plus bas).

`resolve_roms_path` essaie, dans cet ordre : le répertoire courant s'il contient déjà le
chemin — une exécution depuis la racine, un chemin absolu et un `--rom-path` qui tombe
juste gardent exactement leur comportement — puis la remontée depuis le binaire, puis le
chemin inchangé, pour que le message cite ce que l'utilisateur a tapé.

**Deux fautes, deux messages.** « le répertoire n'existe pas » et « le répertoire existe
mais pas les fichiers » se confondaient en un seul `Failed to load ROM!`, qui envoyait
chercher des fichiers dans un dossier absent. `initpc` teste désormais `num_roms_paths`
avant `loadbios`, et `loadbios` cite le chemin **résolu** (`paths.roms_paths`), pas la
chaîne d'origine.

**Le piège de la chaîne vide, relevé en relecture.** `Path.Combine(d, "")` rend `d`, qui
existe toujours : la remontée s'arrêtait à sa première itération et `--rom-path ""`
recevait « Impossible de charger le BIOS depuis `bin/Debug/net10.0/` » — un chemin que
l'utilisateur n'a jamais écrit. La chaîne vide sort maintenant avant la boucle.

**Ce que la remontée ne protège pas.** `AppContext.BaseDirectory` est le **premier**
candidat testé : un `roms/` posé à côté du binaire gagne sur celui du dépôt. C'est correct
une fois déployé, et c'est un piège en développement — un `roms/` partiel copié à la main
dans `bin/Debug/net10.0/` charge le BIOS, amorce vert, et laisse `mda.rom` absent. Or
`loadfont` (`video.cs:216-225`) **retourne en silence** quand `romfopen` rend NULL : c'est
le défaut de M4.3 à l'identique, et aucun oracle ne l'attrape, la police ne touchant aucun
état CPU. PCem imprime pourtant la trace (`pclog("loadfont %i %s %p")`, `video.c:934`,
marquée `omitted:`) : **la rétablir rendrait ce cas audible.** Non fait ici.

**`tools/iXtal26.Diff` garde son `"roms"` relatif, délibérément.** `BootDiff` passe la même
chaîne à `Oracle.h_boot()` (`harness.c:395`, côté C) et à `pc.initpc()` (côté C#). La
résoudre d'un seul côté ferait lire deux répertoires différents aux deux moitiés du
différentiel — exactement ce qu'un oracle différentiel ne doit jamais faire.

### Mesure

| | |
|---|---|
| `--boot roms 20`, depuis la racine / `bin/Debug/net10.0/` / `/tmp` | **sorties identiques aux trois**, 74 126 instructions |
| `--boot roms 6000` depuis `/tmp` | **24 073 823 instructions** — le chiffre de M4.1, obtenu hors du dépôt |
| Écran, même exécution | `IBM Personal Computer Basic C1.10`, `62940 Bytes free`, `Ok` |
| Framebuffer, même exécution | 3 997 pixels non nuls, 1 742 images — **les polices sont chargées** |
| `--rom-path /nexistepas` | « Aucun répertoire de ROM utilisable », rend 1 |
| `--rom-path /tmp/roms-vide` | « Impossible de charger le BIOS depuis `/tmp/roms-vide/` », rend 1 |
| `--rom-path ""` | « Aucun répertoire de ROM utilisable », rend 1 |

Les 3 997 pixels non nuls sont la vérification qui compte : un code de sortie 0 se serait
contenté d'un `roms/` partiel. Ils prouvent que le `roms/` résolu est l'entier.

### M4.5 — Un écran noir muet

Écran noir en fenêtre, avec les seuls pavés gris de la ligne de touches de fonction
visibles. Signature exacte d'un `fontdat` **entièrement nul** : le fond des cellules en
vidéo inverse continue d'être peint, mais aucun pixel de glyphe n'est tracé. La VRAM reste
juste, `--boot` montre la bannière, et rien ne dit qu'il manque une police.

C'est le prix d'une omission que j'avais classée « sortie pure » : `pclog("loadfont %i %s
%p")` (`video.c:934`). Elle est rétablie, en message d'échec :

```
loadfont : im1024font.bin introuvable — police non chargée.
```

et `--boot` rapporte désormais l'état de la table :

```
police : 3082 octets non nuls sur 16384 dans fontdat
```

avec un avertissement explicite si le compte est nul. Un écran noir doit se diagnostiquer
en une commande, pas en une heure passée à soupçonner le rendu.

C'est le même angle mort que la police corrompue de M4.3, sous une autre forme : ni le diff
d'amorçage ni le fuzzer ne regardent le chemin pixel, et le chemin pixel n'avait aucune
voix. Maintenant il en a une.

## M4.6 — Le temps d'amorçage est-il celui d'un vrai 5150 ?

Question posée après avoir trouvé l'amorçage long. Trois angles indépendants, puis une
confrontation qui en corrige deux.

### 1. Contrôle de fréquence absolue — enfin fait

Le plan le réclamait depuis M3 ; il ne l'avait jamais été. Nul besoin d'un programme
assemblé à la main : le BIOS programme déjà le canal 0 au diviseur 65536, démasque
l'IRQ 0, et son INT 8 incrémente `0040:006C`. Fenêtre de 3 000 s émulées, **alignée sur un
front du compteur aux deux bouts** pour que le compte de tops soit exact :

| | |
|---|---|
| tops observés | 54 617 sur 3 000,04 s émulées |
| fréquence mesurée | **18,205424 Hz** |
| attendue (1 193 182 / 65 536) | 18,206512 Hz |
| écart | **−59,79 ppm ± 3,3** |

Le plan demandait quatre chiffres significatifs, soit ~275 ppm : franchi avec 4,5× de marge.
Une fenêtre de 30 s ne l'aurait pas permis (±333 ppm) — vérifié empiriquement.

Le budget d'erreur **se ferme exactement** :

| terme | valeur | origine |
|---|---|---|
| division entière `cpu_get_speed() / 100` | −5,87 ppm | `pc.c:473`, c'est PCem |
| cycles consommés jamais portés au TSC | −53,9 ppm | voir ci-dessous |
| **somme** | **−59,8 ppm** | = l'écart mesuré |

Mis HORS DE CAUSE par la mesure : `xt_cpu_multi` vaut exactement 3 × 2³², `PITCONST`
exactement 12 × 2³², et le rapport tops/TSC tombe à −0,04 ppm (786 432,034 contre
65 536 × 12 = 786 432). Le domaine d'horloge est juste.

### 2. Un défaut de comptabilité dans PCem, localisé à 100 %

`clockhardware()` (`808x.c:893-904`) prend son `diff`, banque `tsc`, **puis** appelle
`timer_process()` — qui redescend par `pit_refresh_timer_xt` → `dma_channel_read(0)` →
`refreshread()` → `FETCHCOMPLETE()`, lequel fait `cycles -= (4 - (fetchcycles & 3))`.
Ces cycles de **rafraîchissement DRAM** sont donc débités APRÈS la prise du diff, et
l'instruction suivante refait `cycdiff = cycles` sur la valeur déjà amputée : ils ne
figurent dans aucun diff.

Mesuré, pas déduit : sur 2 M d'instructions, celles **sans** appel à `timer_process`
perdent **0 cycle sur 16,1 M** ; **100,000 %** de la perte est sur celles qui en ont un,
à **1,051 cycle par appel**.

**La perte dépend de la charge**, et c'est ce qui réconciliait deux mesures d'apparence
contradictoire :

| | TSC par tranche | écart |
|---|---|---|
| attendu | 143 181,8 | — |
| à l'invite BASIC | 143 173,3 | −0,006 % |
| pendant le test mémoire | 140 457,5 | **−1,90 %** |

Imputation par opcode : `LOOP` 0,80 cycle/exécution, `STOSB` 0,13, `MOV` 0,13, `XOR` 0,07 —
soit 1,13 sur 63,6, 1,78 %. `LOOP` domine parce que son `FETCHCLEAR` met `prefetchw` à 0
et désactive la sortie anticipée de `FETCHCOMPLETE`.

**Ce défaut ne change PAS la durée d'amorçage** : ces cycles sont bel et bien débités du
budget, ils manquent seulement au TSC. Il retarde l'horloge de l'INVITÉ de 1,9 % pendant
un travail mémoire intensif. C'est exactement la dérive systématique que le diff par
instruction ne peut pas voir, puisqu'elle est identique des deux côtés.

### 3. Le coût du test mémoire, prédit puis mesuré

Le BIOS teste **41 blocs de 16 Kio** (640 Ko de RAM + 16 Ko de VRAM CGA), un remplissage
`REP STOSB` puis **cinq passes** de motifs AA → 55 → FF → 01 → 00. Confirmé à l'unité près
par un profil par adresse linéaire : **3 358 720 itérations = 41 × 16384 × 5**.

| | prédit depuis la ROM et `808x.c` | mesuré | écart |
|---|---|---|---|
| coût de la boucle E02E | 63,51 cycles/itération | 63,598 | +0,14 % |
| test mémoire complet | 46,25 s | **46,18 s** | **−0,16 %** |

### 4. Confrontation au matériel réel — et la limite

Une seule mesure au gabarit exact a été trouvée : IBM 5150, 640 Ko, CGA, BIOS 27/10/82,
départ à froid → curseur sous `Ok` en **52 s**. Plancher analytique indépendant : 33,6 s
pour le seul test mémoire, ce qui exclut toute valeur très inférieure.

Nous sommes à **57,30 s** émulées (tranche 5729). **Mais 6,63 s — 11,6 % — sont de la
pure temporisation disquette** : `Models/model.cs:31` porte `// omitted: fdc_add()` là où
PCem l'appelle (`model.c:194`), pendant que les interrupteurs DIP déclarent deux lecteurs
(`0040:0010 = 0x006D`). Le BIOS tourne alors dans le `WAIT_INT` de `F000:EF3A`
(`TESTB $80, ds:0x3E` / `JNE` / `LOOP` avec CX = 0, BL = 2) jusqu'à échéance complète,
cinq fois — 655 360 exécutions, tranches 4920 à 5695.

Hors ce poste : **≈ 50,7 s contre 52 s réelles**, rampe d'alimentation et vrai cycle
moteur compris.

### Verdict

Le temps **émulé** est juste : horloge exacte à 60 ppm près, modèle de coût confirmé à
0,16 % par une mesure indépendante, durée compatible avec la seule mesure de matériel
réel disponible. La lenteur de l'amorçage est **authentique** — le test mémoire du 5150
est linéaire en RAM et 640 Ko est le maximum.

Trois réserves, à ne pas taire :
1. **6,6 s de trop**, dues à notre propre omission du contrôleur de disquettes.
2. La référence matérielle est **une seule mesure au chronomètre**, ±2 à 3 s, sur une
   configuration qui n'est pas la nôtre. « Compatible » est le mot juste, pas « conforme ».
3. L'accord à 0,16 % confirme l'**arithmétique** de PCem, pas la conformité de son modèle
   de BIU à un vrai 8088 : `FETCHCLEAR` qui jette la file à chaque `LOOP` pris, le plafond
   de 16 sur `fetchcycles` — rien ici n'arbitre ces choix contre une table publiée.

Et à ne pas confondre avec tout ce qui précède : « la machine HÔTE tient-elle le temps
réel ? » est une autre question, lue à 99-100 % dans le titre de la fenêtre, et qui
n'entre dans aucun des chiffres ci-dessus.

---

## M5.0 — Le corpus SST complet contre le cœur C#

Mesuré le 2026-09-20, .NET 10.0.112, x86-64 Linux. Oracle : `libixtal26oracle.so`, ABI 1.

La sonde SST de M0 était une **porte**, pas une vérification : elle mesurait si PCem suit
le silicium d'assez près pour mériter d'être transcrit. Elle ne visait que l'oracle C, sur
19 formes choisies pour être retorses. Le harnais qu'elle devait décider n'avait jamais
été passé au cœur C#, et `sst-baseline.tsv` n'avait pas bougé depuis M1.2 — 67 formes,
alors que `vectors/sst/v2/` en porte 84 et que le jeu d'instructions est complet depuis
M1.4.

Les deux cibles ont donc été passées sur **les 84 formes**, 2 000 cas chacune : 168 000
cas présentés, 166 998 joués après le filtre de préfixes.

### Le critère est tenu octet pour octet

Le plan ne demande pas « 100 % de SST » : il demande que le C# **reproduise la colonne
`passe` de l'oracle à l'identique**. Les deux lignes de base, corps hors en-tête de
provenance, ont le même condensé :

```
d994e8f7d46d8ed9cd3cf43f4f09028345937ce753dc5fbb89547ff70f929f60   oracle C
d994e8f7d46d8ed9cd3cf43f4f09028345937ce753dc5fbb89547ff70f929f60   cœur C#
```

160 592 / 166 998 des deux côtés, colonne `premier_echec` comprise — même cas en premier
échec, mêmes valeurs observées et attendues, sur chacune des 84 formes.

### Et la vérification forte : l'état complet, pas la colonne

L'égalité des compteurs est nécessaire, pas suffisante : deux cœurs peuvent échouer le
même **nombre** de cas sans échouer les **mêmes**, et deux états faux différents comptent
pareil. `sst-diff` rejoue chaque cas sur les deux cœurs et confronte les **32 champs
d'état plus les cycles consommés**, via `Fuzzer.CompareStates`.

| | |
|---|---|
| formes | **84 / 84** |
| cas rejoués sur les deux cœurs | **168 000** |
| divergences | **0** |

Le C# ne reproduit donc pas le score de PCem, il reproduit son **état bit à bit** — y
compris les 19 formes où PCem s'écarte du silicium, faux drapeaux compris.

### Les 19 formes déviantes, par famille

Toutes échouées identiquement des deux côtés. Chiffres sur 2 000 cas, sauf `AD` (1 515
après filtre).

| Famille | Formes | Réussite | Défaut |
|---|---|---|---|
| ADC / SBB, drapeau AF | `10`–`15`, `18`–`1D` | 1 905 à 1 953 | **PB-01** |
| DAA / DAS | `27`, `2F` | 1 989 / 1 960 | **sans entrée PB** — voir ci-dessous |
| REP LODSW | `AD` | 1 004 | — |
| SETMO | `D0.6` | 5 | opcode non documenté, non implémenté |
| SHL par CL, drapeau OF | `D2.4` | 1 063 | — |
| DIV, débordement de quotient | `F6.6`, `F7.6` | 955 / 982 | — |

**Les tirets de la colonne « Défaut » sont des trous, pas des acquittements.** Cinq des six
familles n'ont **aucune entrée `PB-nn`** : DAA/DAS, REP LODSW, SETMO, SHL par CL et le
débordement de quotient de DIV. Le § Portée de `PCEM_BUGS.md` n'impute qu'une seule entrée
à SingleStepTests, PB-01 ; l'oracle SST en a en réalité éclairé six. Ces cinq-là sont
décrites en prose dans le constat de M0 ci-dessus, mais elles ne sont pas instruites à la
ligne de C, pas identifiées, et donc pas citables depuis un marqueur
`// pcem bug, reproduced:`.

**PB-01 chiffré.** Le registre annonce « ~3 à 4 % de divergence sur ADC/SBB, toujours sur
le seul bit 0x0010 ». Sur douze formes × 2 000 cas : de 1 905/2000 (`19`, SBB word, 4,75 %)
à 1 953/2000 (`1D`, 2,35 %), et **les douze premiers échecs portent tous
`diff masqué 0x0010`**. Aucune exception.

**DAA/DAS n'est pas un effet de PB-01, et n'a pas d'entrée.** PB-01 fausse AF sur ADC et
SBB, et le registre le note « observable à travers DAA, DAS, AAA et AAS ». Mais un cas SST
de `27` est autonome : AF lui est **donné** par l'état initial, aucun ADC ne le précède.
Et les échecs ne portent pas sur un drapeau — ils portent sur **AL** : `daa` donne
`AX = 0x3604` contre `0x36A4` attendu, `das` donne `0xEA9B` contre `0xEAFB`. C'est le
résultat de DAA/DAS lui-même qui est faux, indépendamment de PB-01. À instruire et à
verser à `PCEM_BUGS.md` — 11 et 40 cas sur 2 000, jamais examinés à la ligne.

**Dix-huit formes entrent dans la ligne de base** — `40 43 48 4B` (INC/DEC), `50 53 58 5B`
(PUSH/POP), `70 72 74 75 76 78 7A 7C 7E 7F` (Jcc). Elles passent 2000/2000 des deux côtés.

### Deux défauts du harnais, trouvés par la campagne elle-même

1. **`RunCaseCsharp` omettait le suffixe `(diff masqué 0x…)`** qu'émet la branche oracle.
   Aucun cœur ne divergeait, mais un `diff` des deux lignes de base signalait **13 fausses
   différences** — ce qui défait très exactement le critère « reproduire ce fichier à
   l'identique ». Un garde-fou qui crie sur sa propre mise en forme ne garde plus rien.
   Corrigé, `SstProbe.cs` ; les deux fichiers sont identiques depuis.

2. **`IsUnimplementedPrefix` est périmé.** Il écarte encore `F2`/`F3` au motif qu'ils
   « attendent `rep()` », alors que `808x.cs:3277-3281` les traite depuis M1.4. Mesuré :
   **1 002 cas jamais joués** (0,60 %), tous sur `A4` et `AD`. Le filtre ne teste que
   `bytes[0]`, d'où l'incohérence visible sur `AD`, dont le premier échec est un
   `26 F3 AD` qui passe au travers.

   **`sst-diff` tranche la question** : il n'applique pas ce filtre, et a donc joué `A4` et
   `AD` sur **2 000 cas chacun** là où la sonde n'en joue que 1 483 et 1 515. Les deux
   reviennent d'accord. Les 1 002 cas masqués ne cachent aucune divergence — retirer le
   filtre ne fera qu'ajouter de la couverture sur les opérations de chaîne. Non retiré
   ici : ça change les dénominateurs de `sst-baseline.tsv`, et re-baser est une décision,
   pas une conséquence.

### Ce que ce vert ne dit pas

- **Il ne couvre pas la déviation M5.1** des alias `ref` de prologue dans `execx86`. La
  bibliothèque mesurée a été construite à 13:56:10 ; `808x.cs` a été modifié à 16:32:32 ;
  aucune construction entre les deux. Toutes les mesures ci-dessus portent sur le cœur
  **d'avant**. Le contrôle structurel passe — `cpu_state` est `static readonly` d'instance
  unique (`386_common.cs:19`), `regs` un `readonly x86reg[8]` jamais réalloué (`x86.cs:58`),
  `seg_cs`/`seg_ds`/… des `readonly x86seg` jamais réassignées (`x86.cs:75-76`), donc aucun
  alias ne peut se périmer et une écriture faite par une fonction appelée vise la même
  adresse. Mais structurellement sûr n'est pas mesuré, et c'est précisément ce que SST sait
  adjuger. **À relancer contre M5.1.**
- **Il ne couvre pas 79,5 % du corpus.** 168 000 cas sur les **819 000** que portent les 84
  fichiers. L'échantillon est le même qu'à M0 par forme, pas le corpus entier.
- **Il ne dit rien du diff d'amorçage ni du fuzzer**, et **rien du chemin pixel** — SST ne
  fait pas tourner une machine. Les trois oracles attrapent des classes disjointes.
- **Il ne valide pas le modèle de BIU.** SST v2 porte des traces de cycles par broche ;
  `RunCase` ne lit que `initial`/`final`. Les champs `cycles`, `queue` et `hash` sont
  désérialisés et ignorés, comme depuis M0.

Preuves : `sst-baseline.tsv` (84 formes) pour la ligne de base ; le reste est régénérable
par `iXtal26.Diff sst-probe --limit 2000 [--target csharp]` et `sst-diff <forme> 2000`.

---

## M5.1 — Vitesse hôte : « aussi rapide que du C ? », mesuré

Mesuré le 2026-09-20, .NET 10.0.112 (runtime 10.0.12), gcc, Core Ultra 7 258V — 4 cœurs P
(cpu 0-3, 4,8 GHz) + 4 cœurs LPE (cpu 4-7, 3,7 GHz), gouverneur `powersave`, Rider actif
en fond. Tout est épinglé `taskset -c 0-3`, minimum ou médiane de 3.

Le dépôt ne contenait **aucune mesure de vitesse hôte** : zéro `Stopwatch` dans tout le C#,
`speed-check` mesure le temps *émulé*, et le « 100 % » du titre SDL ne peut pas dépasser
100 par construction (le frein `drawits` attend l'horloge murale : 15× de marge et 1,05×
s'affichent pareil). Le `.csproj` affirmait « 50 à 100× de marge ». La question n'avait
jamais été posée à la machine.

### La réponse

| Cœur | 60 s émulées (6 000 × `h_run`/`Run(47727)`, 24 944 866 instr.) | × temps réel | ratio |
|---|---|---|---|
| C, oracle instrumenté (`libixtal26oracle.so` : `-O2 -fPIC`, `--wrap`, sans LTO) | **2 525 ms** | 23,8 | 1 |
| C, **production** (`tools/oracle/build/bench` : `-O2 -flto -march=x86-64-v2`, sans `--wrap`) | **1 659 ms** | 36,2 | 0,66 |
| C#, avant M5.1 (HEAD `e75784e`) | 3 936 ms | 15,2 | 1,56 |
| C#, **M5.1** (prologue + `cols` + `fontdat`) | **3 514 ms** | 17,1 | **1,39** ; 2,12 contre la production |

Trajectoire vérifiée en fin de banc : Δins, Σcycles (296 826 600), vecteur d'état (32
champs) et hachage FNV de la RAM **identiques** entre les trois cœurs — sinon le ratio
n'est pas imprimé. Commandes :
`iXtal26.Diff bench roms 6000 --repeat 3 --warmup 600` · `make -C tools/oracle bench` puis
`tools/oracle/build/bench roms 6000 3 600`.

L'oracle instrumenté est **50 % plus lent que PCem compilé comme PCem** : `--wrap` sur les
accès lents, `-fPIC` sans `-fvisibility=hidden` (281 relocations GOT), pas de LTO. La
comparaison « à instrumentation égale » est 1,39 ; contre le C livré, 2,12 — et le C#
porte encore ses compteurs et son journal d'écritures en ligne (`mem.cs:271-386`).

### Où va l'écart : les compteurs matériels tranchent

`perf stat`, 6 000 tranches `--boot` (démarrage .NET compris) :

| | cycles | instructions | IPC | branch-misses | L1i misses |
|---|---|---|---|---|---|
| C (.so) | 3,93 G | 21,7 G | 5,53 | 9,6 M | 2,8 M |
| C#, prologue | 6,56 G | 36,5 G | 5,57 | 16,7 M | 24,3 M |
| C#, prologue + `cols` + `fontdat` | 6,25 G | 34,9 G | 5,59 | 16,6 M | — |

**Même IPC.** Le C# n'est pas ralenti par les caches ni la prédiction : il **exécute plus
d'instructions hôte** — 1 517 par instruction 8088 contre 873. C'est du code généré et de
l'instrumentation, rien d'autre.

`perf record` par fonction (`DOTNET_PerfMapEnabled=1 DOTNET_EnableWriteXorExecute=0` — sans
le second, le code JIT vit dans un `memfd:doublemapper` que perf ne symbolise pas) :

| Fonction | C (.so) | C#, prologue | C#, M5.1 |
|---|---|---|---|
| `cga_poll` | 43,9 % · 1,12 s | 41,5 % · 1,78 s | 36,7 % · 1,55 s |
| `execx86` | 12,6 % · 0,32 s | 7,6 % · **0,32 s** | 8,3 % · 0,35 s |
| `FETCHADD` + `FETCH` | 14,1 % · 0,36 s | 7,7 % · 0,33 s | 8,8 % · 0,37 s |
| `readmembl` + `mem_read_bios` + `readmembf` (+ `__wrap`, PLT) | 8,4 % · 0,21 s | 6,9 % · 0,30 s | 9,2 % · 0,39 s |
| `clockhardware` | 2,6 % · 0,066 s | 1,5 % · 0,066 s | 2,4 % · 0,10 s |
| timers (`timer_enable/process/remove_head`) | 4,5 % · 0,12 s | 3,5 % · 0,15 s | 3,6 % · 0,15 s |
| JIT (`libclrjit`) | — | 8,7 % · 0,37 s | ≈ idem |
| runtime (`libcoreclr`, barrières d'écriture, `__tls_get_addr`, thunks de délégués) | — | 8,9 % · 0,38 s | ≈ 6 % |

**Après le prologue, le cœur CPU proprement dit est à parité avec le C** (`execx86`,
`FETCH*`, `clockhardware`). L'écart vit dans `cga_poll` — le premier poste des DEUX côtés,
44 % du temps du C —, dans la chauffe du JIT, et dans le runtime.

### Ce qu'on a appris de RyuJIT, et vérifié à la source (`dotnet/runtime`, `release/10.0`)

- `execx86` (26 270 octets d'IL, 9 825 instructions, 1 209 blocs, 357 `case`) est compilée
  en Tier1 — **pas en MinOpts** —, mais **sans un seul inlining** : 1 507 `call` dans le
  listing, dont 898 vers les accesseurs d'une ligne qui transcrivent `#define cycles`,
  `AL`, `cpu_mod`… ; 51 `FETCH()` marqués `AggressiveInlining`, 51 appels. Porte :
  `lvaHaveManyLocals(0.9f)` (`fginline.cpp:1133`) — dès 922 locales
  (0,9 × `JitMaxLocalsToTrack` = 0x400), `CALLSITE_TOO_MANY_LOCALS`, fatal, **hors de la
  policy, donc aveugle à `AggressiveInlining`**. L'importation crée un temporaire par `dup`
  de byref (chaque `x -= n` sur une propriété `ref`) : le compte est dépassé avant le
  premier candidat. Preuve différentielle : `clockhardware`, `geteab`, `rep` inlinent les
  mêmes accesseurs sans un `call`.
- **Contre-épreuve** : `DOTNET_JitMaxLocalsToTrack=0x4000 DOTNET_TieredCompilation=0`
  réinline (1 508 → 946 appels, trame 0x948 → 0x4A8) pour **5 % de chrono**. Les appels
  étaient un défaut réel, pas *le* défaut. En mode tiered, le même bouton retarde le Tier1
  de plusieurs secondes : inutilisable.
- Le **prologue de `ref` locales** (`808x.cs:1063-1080`, 7e entrée de R4) : 898 → 26
  appels d'accesseurs, 636 `call` au total, trame 2 376 → 312 octets, code 31 → 24 Ko.
  Gain ≈ 5 %, risque nul : alias purs sur des champs `readonly` jamais remplacés.
- Les cinq seuils MinOpts (60 000 octets, 20 000 instr., 2 000 blocs, 2 000 locales,
  8 000 réf. ; `compiler.cpp:3710`) sont comptés sur l'IL **avant** importation, donc
  mesurables sur la DLL : `ilspycmd -il -t iXtal26.Cpu._808x iXtal26/bin/Release/net10.0/iXtal26.dll`.
  `execx86` est à **60 % du seuil de blocs**. Le palier (b) 8086 doit rejouer ce compte
  (garde-fou G2 bis) : franchir 2 000 blocs débraye tout, silencieusement — aucun oracle
  ne le verrait, seul `DOTNET_JitDisasmSummary=1` (« MinOpts ») le dit.
- **Ce que G2 ne prouvait pas.** Sa méthode de 180 106 octets d'IL (VERIFICATION.md § M0)
  était nécessairement en MinOpts ; « pas de bailout » voulait dire « ça compile », pas
  « c'est optimisé ». `JitDisasmSummary` ne montre pas l'absence d'inlining ; `JitDisasm`
  oui.
- Réglages runtime (`TieredPGO`, `TieredCompilation`, `TC_QuickJitForLoops`, OSR, R2R,
  `JitInlineBudget`) : aucun effet mesurable. `AggressiveOptimization` n'élève aucune porte.

### Les deux leviers vidéo (M5.1, code transcrit, `// DEVIATION:`)

| | Forme | Vérification |
|---|---|---|
| `cols` | `new uint32_t[4]` par appel de `cga_poll` (31 400/s émulée) → tableau statique réutilisé, comme le `uint32_t cols[4]` de pile du C. Chaque chemin écrit ses éléments avant de les lire. | `--boot roms 6000` : `ins 24073823`, écran BASIC, 3 997 pixels, **empreinte FNV du framebuffer identique** (`D8C027E793FF9677`) |
| `fontdat` | `uint8_t[2048, 8]` → `uint8_t[2048 * 8]` indexé `(c << 3) \| d` : un tableau `[,]` échappe à l'analyse de plages de RyuJIT (`rangecheck.cpp` ne connaît que `GT_ARR_LENGTH`). | idem — l'empreinte est le seul juge du chemin pixel, ajoutée à `--boot` pour l'occasion |

### Oracles, sur le cœur M5.1

| | |
|---|---|
| Diff d'amorçage, 6 000 tranches | **24 944 866 instructions identiques** |
| Fuzzer, mode flux, 256 opcodes, 1 500 × 200 | **300 000 instructions, zéro divergence** |
| `bench`, trajectoire (Δins, Σcycles, 32 champs, RAM) | identique C / C#, 6 mesures reproductibles |
| Framebuffer, `--boot roms 6000` | empreinte identique avant/après |
| SST, sonde (84 formes, `--limit 2000`) | **160 592 / 166 998**, condensé du corps identique à `sst-baseline.tsv` |
| SST, `sst-diff` (84 formes × 2 000 cas) | **168 000 cas rejoués, zéro divergence** |

#### SST relancé contre le cœur M5.1

M5.0 mesurait le cœur **d'avant** le prologue de `ref` locales : la `.so` datait de
13:56:10, `808x.cs` de 16:32:32, sans construction entre les deux. La campagne est donc
rejouée, et **les deux côtés sont reconstruits avant de mesurer** — `libixtal26oracle.so`
à 16:59:05, `iXtal26.dll` à 16:59:33, contre un `808x.cs` figé à 16:32:32. C'est la
précaution qui manquait, pas le résultat.

| | |
|---|---|
| Sonde, `sst-probe --limit 2000 --target csharp` | 160 592 / 166 998 (96,16 %), **10 min 20 s** |
| Condensé du corps (`tail -n +4 … \| sha256sum`) | `d994e8f7d46d8ed9cd3cf43f4f09028345937ce753dc5fbb89547ff70f929f60` des deux côtés ; `diff` vide, colonne `premier_echec` comprise |
| `sst-diff <forme> 2000`, 84 invocations | **84 formes, 0 en divergence**, 22 min 32 s |

Le prologue de `ref` locales ne déplace **aucun** cas : ni le score, ni l'identité des cas
échoués, ni les 32 champs d'état, ni les cycles. L'hypothèse « l'alias vaut toute la
boucle » est désormais mesurée, pas seulement raisonnée à partir de `static readonly`.

**Ce que ce vert ne dit pas** : `--limit 2000` reproduit *délibérément* l'échantillon de
M5.0, sans quoi le condensé ne serait plus comparable. 168 000 cas sur les 819 000 que
portent les 84 fichiers — « 84/84 » est une reproduction, pas une couverture de corpus.

### Deux défauts trouvés en mesurant

1. **`h_ram_hash()` et `_808x.RamHash()` lisaient 1 Mo** (`H_RAM_SIZE`/`RAM_SIZE`) sur une
   machine amorcée à 640 Ko + 4 : segfault côté C, `IndexOutOfRange` côté C#. Jamais
   appelées après un amorçage jusqu'ici (le fuzzer travaille sur la carte plate de 1 Mo).
   Bornées à `mem_size` Ko des deux côtés.
2. **Épingler un processus .NET sur UN cœur fausse la mesure** : `taskset -c 3` donne
   6,9 s au lieu de 4,2 — le fil de compilation Tier1 partage le cœur avec l'émulation.
   Les quatre cœurs P, ou un seul cœur seulement une fois ΔJIT = 0.

### Ce que ce chiffre ne dit pas, et ce qui reste

- **`cga_poll` reste 1,4× le C** (1,55 s contre 1,12 s) : bornes sur ~31 M d'accès à
  `Buffer32` par seconde émulée, `hline`, `cgapal`. Ce n'est pas le siège d'un écart
  singulier — le C recharge sept valeurs par pixel à cause de `-fno-strict-aliasing` —
  mais c'est le premier poste des deux côtés.
- **Le chemin lent mémoire** (`readmembl` par octet de code ROM : `mem_read_bios`
  n'appelle jamais `addreadlookup`, donc chaque octet de F000/F600 le traverse, des deux
  côtés) coûte 0,39 s contre 0,21 s. Le delta C# : `obs_depth`, la région `try/finally`,
  deux bornes, un délégué non dévirtualisé. Levier B du plan, non fait : le miroir exact
  de `-Wl,--wrap` (enveloppe + `__real_readmembl`) rendrait `obs_depth` et `try/finally`
  inutiles.
- **La chauffe du JIT** (0,37 s) est un coût fixe, pas de régime : il disparaît d'une
  mesure de régime stationnaire, pas d'un `--boot`.
- **Découper `execx86`** reste le seul levier pour les ~310 sites `static inline` du C
  (`FETCH`, `FETCHCLEAR`, `geteab/w`, `seteab/w`, `setznp8/16`, `getword`) que le JIT ne
  peut pas inliner tant que la méthode dépasse la porte. Refusé à M5.1 ; à chiffrer.
- **Pour la cible 386** : `386.c:194` dispatche par table de pointeurs de fonction, une
  fonction par opcode. Le monolithe qui bloque RyuJIT est propre au 8088.

La marge de l'hôte se lit maintenant dans le titre de la fenêtre (`marge x17,1`, à côté
du pourcentage), d'un `Stopwatch` autour du seul `pc.runpc()`.

---

## M5.3 — Le turbo d'amorçage : 57 s de POST en 4,2 s, sans toucher à la machine

Mesuré le 2026-09-20, même machine que § M5.1 (Core Ultra 7 258V, `taskset -c 0-3`,
build Release), **mais pas sur une machine au repos** : une campagne SST tournait dans
une autre session jusqu'à 17 h 55, et l'IDE faisait tourner un build Debug ensuite. Ces
chiffres sont donc des **majorants** — la charge ne peut qu'avoir ralenti les trois
configurations, et elle les a ralenties ensemble, à une mesure près par ligne. Le rapport
entre elles, lui, tient : c'est lui qui porte la conclusion, pas la milliseconde. Une
reprise au repos est due, avec le protocole complet de § M5.1 (minimum de 3, Rider fermé).

La question « peut-on accélérer le POST ? » recouvre trois choses que les chiffres
séparent nettement. Aller jusqu'à l'invite BASIC (6 000 tranches, 60 s émulées) :

| | temps mur | facteur |
|---|---|---|
| fenêtre, frein sur horloge murale (comportement d'origine) | **≈ 57 s** | 1 |
| fenêtre, frein retiré, toutes les images présentées (`--slices 6000`) | 9,5 à 10,3 s | ~6 |
| fenêtre, **`--turbo`** (frein retiré + présentation bridée) | **4,24 s** pour 5 800 tranches | **13,7** |
| headless, frein retiré — plancher, aucun chemin de blit | 3,93 s | 14,6 |
| si le cœur C# égalait le C de production (§ M5.1) | ≈ 1,7 s | 34 |

**Le frein vaut 6×, la présentation encore 2×, et toute l'optimisation du cœur transcrit
vaut au mieux 2× par-dessus.** C'est ce rapport qui a décidé de l'ordre des travaux :
l'axe hôte d'abord, le code transcrit ensuite.

### Ce que le turbo change, et ce qu'il ne change pas

`--turbo [N]` (défaut 5 800 tranches ; l'invite BASIC tombait alors à la tranche 5 729,
§ M4.6 — M6 l'a ramenée à 5 167, et le défaut couvre aussi l'invite de PC DOS à 5 520)
n'attend pas l'horloge murale pendant les N premières tranches, puis rend la machine au
temps réel. Une frappe y met fin — à partir de là, quelqu'un regarde.

La trajectoire émulée est **rigoureusement inchangée** : `--boot roms 6000` rend toujours
`ins = 24073823`, `0040:0010 = 006D`, 640 Ko, l'écran `The IBM Personal Computer Basic` /
`Ok`, 3 997 pixels et l'empreinte `D8C027E793FF9677`. Le turbo vit entièrement dans
`Host/SdlHost.cs` et `Program.cs` : aucun `// DEVIATION:`, aucun oracle exposé.

Ce qui est sauté est **hôte** : pendant le turbo, le CGA émet des centaines d'images par
seconde murale ; on n'en présente qu'une toutes les 16 ms (1 428 sautées sur ~1 690 au
dernier relevé). `video_blit_complete()` reste **inconditionnel** — c'est lui qui rend la
propriété du handshake de PCem, et le CGA redessinant toutes ses lignes à chaque image,
une image sautée n'est jamais une ligne perdue.

### Le piège du réarmement, et une attribution fausse corrigée par la mesure

1. **`drawits` doit repartir de zéro à la sortie du turbo.** Sans cela l'accumulateur
   encaisse d'un coup les ~50 s de retard, et la branche `if (drawits > 50) drawits = 0`
   (wx-sdl2.c:176-179) les jette **sans rien dire** : une demi-seconde d'émulation en
   accéléré, invisible. On repart donc d'une horloge neuve, compteurs de titre compris.
2. **`--slices N` reste hors du turbo**, et c'est un contrat : ce mode ne cadence rien du
   début à la fin, et c'est ce qui rend deux exécutions identiques. `--turbo` avec
   `--slices` est **refusé**, pas ignoré — une option sans effet est indiscernable d'une
   option qui ne marche pas.
3. **L'attribution des 4 s d'écart entre temps mur et temps CPU était fausse.** On les
   croyait dans `RenderPresent` (attente du compositeur). Chronométré des deux côtés :
   **recopie + `UpdateTexture` 2 686 ms contre `RenderPresent` 1 190 ms** sur 1 741
   images. Le garde saute donc les deux, et pas seulement la présentation. Ces deux
   chiffres sont imprimés par `--verbose` (`chemin blit:`), que le banc de § M5.1 ne
   pouvait pas produire : headless, il n'installe aucun crochet de blit.

### Vérifications

| | |
|---|---|
| Trajectoire, `--boot roms 6000` | `ins 24073823`, empreinte `D8C027E793FF9677`, écran BASIC — **identiques à § M5.1** |
| Le frein reprend après le turbo | 12 s d'horloge murale après `--turbo 400` : **2,07 s user + 0,48 s système** (turbo permanent en aurait coûté 12) |
| Phase turbo, par le programme lui-même | `turbo : 5800 tranches (58 s émulées) en 4.24 s mur (x13.7), 1428 images sautées` |
| Table d'options | `--turbo 0`, `--turbo abc`, `--turbo --slices` : code 2 et message explicite ; `--turbo --headless` retombe sur la règle de `--headless` |

Ce que le turbo ne fait PAS : il ne rend pas le cœur plus rapide. Les 4,24 s restent
2,5× le C de production sur la même trajectoire, et le POST dure toujours 57 s pour la
machine émulée. Restent donc, dans l'ordre : le miroir de `--wrap` côté C# (levier B,
§ M5.2), les bornes de `cga_poll`, puis le contrôleur de disquettes — 6,63 s de
temporisation à vide dans le POST (§ M4.6), que **l'oracle C partage** (aucun source
`floppy/` ni `disc/` dans `tools/oracle/Makefile`) : ce jalon-là doit déplacer les deux
côtés à la fois, sous peine de transformer les 24 944 866 instructions identiques en
divergence à la tranche ~4920.

---

## M6 — Le lecteur de disquette : PC DOS 2.00 amorce, et l'oracle est d'accord

Le 2026-09-20. § M5.3 posait la condition : ce jalon devait **déplacer les deux côtés à
la fois**, sous peine de transformer les 24 944 866 instructions identiques en divergence
à la tranche ~4920. C'est ce qui a été fait : cinq fichiers transcrits (`floppy/fdc.c`,
`floppy/fdd.c`, `disc/disc.c`, `disc/disc_img.c`, `disc/disc_sector.c`, plus
`get_extension` de `config.c`) et les mêmes cinq fichiers C liés dans
`tools/oracle/Makefile`, avec trois stubs (`readflash`, `isa_cycles`, le chargeur FDI).
Le câblage suit `pc.c` à la ligne des deux côtés : `fdc_init/disc_init/img_init` dans
`initpc` (`:279-282`), `fdc_init/disc_reset/disc_load` dans `resetpchard` (`:365-368`),
`fdc_add` dans `common_init` (`model.c:194`), `fdc_reset` dans `pc_reset` (`:180`).

Un choix de configuration, identique des deux côtés et qui ne vient pas de PCem : le
type de lecteur. PCem le lit dans son fichier de configuration (`pc.c:776`, défaut 7 =
3,5" ED). Le 5150 a des 5,25" double densité : **type 1** (`fdd.c:44-46`), posé dans
`pc.cs` et dans `h_boot`.

### Mesures

| Oracle | Commande | Résultat |
|---|---|---|
| Diff d'amorçage, sans disquette | `boot-diff roms 6000` | **25 457 269 instructions identiques** (24 944 866 avant M6) |
| Diff d'amorçage, disquette système | `boot-diff roms 7000 --fda os/pcdos20/pcdos20b.img` | **26 750 702 instructions identiques** — POST, INT 19h, secteur d'amorçage, IBMBIO, IBMDOS, COMMAND, invite de date |
| Diff d'amorçage, disquette non système | `boot-diff roms 6500 --fda os/pcdos20/pcdos20s.img` | **27 494 583 identiques** — le secteur d'amorçage lit le répertoire et affiche `Non-System disk or disk error` |
| Sonde disquette | `disc-probe roms 5300 / 5310 / 5400`, `5600 --fda …b.img` | 20 champs identiques à chaque fois |
| Fuzzer, mode flux, 256 opcodes | `fuzz --mode stream --rounds 8000 --op 00 … --op FF` | 400 000 instructions, zéro divergence (il ne voit pas la disquette : dit pour mémoire) |
| Autotest de l'oracle | `make -C tools/oracle selftest` | vert |
| Manifeste | `tools/check-oracle.sh` | 22 vérifiés, 0 dérive, arbre vendoré intact |
| Images | `sha256sum os/pcdos20/*.img` | inchangées après toutes les passes (ouvertes en lecture-écriture) |

Et la machine, vue de la console — `--boot roms 6500 --floppy-a os/pcdos20/pcdos20b.img
--type "" --type "" --type DIR` :

```
  |Current date is Tue  1-01-1980
  |Enter new date:
  |Current time is  0:00:13.89
  |Enter new time:
  |The IBM Personal Computer DOS
  |Version 2.00 (C)Copyright IBM Corp 1981, 1982, 1983
  |A>DIR
  | Volume in drive A has no label
  | Directory of  A:\
  |COMMAND  COM    17664   3-08-83  12:00p
  |ANSI     SYS     1664   3-08-83  12:00p
  ...
```

Sans disquette, `--boot roms 6000` rend toujours l'écran BASIC, `ins = 24586052`
(24 073 823 avant), `0040:0010 = 006D`, 640 Ko, empreinte `6EF1F53C27AB77AF` (la phase
du curseur diffère, BASIC ayant démarré 5,7 s plus tôt).

### La réserve n° 1 de § M4.6 : rétrécie, et la prédiction corrigée de 1,0 s

§ M4.6 imputait **6,63 s** de l'amorçage à l'absence du contrôleur (tranches 4920 à 5695
dans le `WAIT_INT` de `F000:EF3A`) et en déduisait, par soustraction, « ≈ 50,7 s hors ce
poste ».

**Événement mesuré : l'invite BASIC `Ok` COMPLÈTE**, le même que celui de § M4.6 — et pas
la bannière, qui commence à s'écrire une quinzaine de tranches plus tôt. Bissection sur la
ligne `Ok` seule, `--boot roms N` : 5166 non, 5168 oui.

| | tranche | temps émulé |
|---|---|---|
| avant M6, sans contrôleur (§ M4.6) | 5 729 | 57,29 s |
| après M6, contrôleur présent | **5 167** | **51,67 s** |
| gagné | 562 | **5,62 s** |

La prédiction de § M4.6 était donc **optimiste de 1,0 s** : retirer la temporisation ne
rend pas les 6,63 s, parce qu'un contrôleur PRÉSENT travaille — recalibrage (2048 µs),
seek (1024 µs), démarrage moteur, lecture du secteur d'amorçage — là où un contrôleur
absent ne faisait qu'expirer. **5,62 s de temporisation retirées, ≈ 1,0 s de travail réel
ajouté.** Le « ≈ 50,7 s » de § M4.6 se lit donc comme un plancher, pas comme une
prédiction : il soustrayait un poste au lieu de le remplacer.

Contre les **52 s** chronométrées sur un vrai 5150 (§ M4.6 § 4, mesure unique, ±2 à 3 s),
51,67 s est un meilleur accord que 57,29 s — mais la réserve n° 2 de § M4.6 tient
intégralement : une seule mesure au chronomètre sur une configuration qui n'est pas la
nôtre ne fait pas une conformité.

Avec la disquette système, l'invite de date de PC DOS 2.00 tombe **entre les tranches
5 516 et 5 520** (bissection sur `Enter new date`), soit ≈ 55,2 s émulées.

### Parité R2, accolades seules exclues

| Fichier | C | C# | ratio |
|---|---|---|---|
| `Floppy/fdd.cs` | 113 | 123 | 1,09 |
| `Floppy/fdc.cs` | 963 | 991 | 1,03 |
| `Disc/disc.cs` | 174 | 194 | 1,11 |
| `Disc/disc_img.cs` | 320 | 324 | 1,01 |
| `Disc/disc_sector.cs` | 264 | 273 | 1,03 |

### Deux choses apprises en vérifiant

1. **`h_runpc` et `pc.runpc` ne suivent pas la même trajectoire.** `h_runpc` remet
   `cycles` à zéro en tête de tranche ; `pc.runpc`, comme `pc.c:475`, laisse `execx86`
   faire `cycles += cycs` et reporte le reliquat négatif. La première version de
   `disc-probe` comparait l'un à l'autre et montrait `motoron` à 1 côté C# contre 0 côté
   oracle à la tranche 5300 : pas une divergence du contrôleur, la même coupure de
   moteur vue un reliquat plus tôt. Le diff d'amorçage (`h_step`/`Step`) et le banc
   (`h_run`/`Run`) sont symétriques et n'ont jamais eu ce problème ; `disc-probe` passe
   désormais par `h_run`/`Run`. À retenir avant d'écrire un nouvel outil de comparaison.
2. **Le POST affiche `131`** — code du test de bouclage cassette — avant que BASIC ou DOS
   n'écrive. C'était déjà le cas (BASIC efface l'écran, DOS non) : `model.cs` omet
   `cassette_device` et l'oracle stube `cassette_input` à 0, donc les deux côtés le font
   à l'identique. Un vrai 5150 sans magnétophone ne l'affiche pas — c'est le port qui
   est testé, pas l'appareil. À transcrire (`devices/cassette.c`) pour un POST propre.

### Défauts de PCem relevés en transcrivant

PB-14 (`disc_set_rate` : le cas 1 retombe dans le cas 2), PB-15 (`fdd_getrpm` : `switch`
inatteignable), PB-16 (`img_load` éjecte sans annuler `f`), PB-17 (`track_data` de 20 Ko
débordé par une piste XDF ED), PB-18 (`strcpy` de `discfns` sur lui-même). Registre :
`PCEM_BUGS.md`.

### Ce que ce vert ne dit pas

- **Rien n'a été écrit sur la disquette.** L'amorçage de DOS 2.00 et `DIR` ne font que
  lire ; les états `STATE_WRITE_*` et `STATE_FORMAT` de `disc_sector.c`, `img_writeback`
  et `fdc_getdata` sont transcrits mais **aucun oracle ne les a exercés**. Le prochain
  jalon est un `COPY` ou un `FORMAT` sous DOS, des deux côtés, avec comparaison de
  l'image résultante.
- Le lecteur B:, les images double face (320/360 Ko), le FIFO du 82077 (le BIOS du 5150
  ne le configure pas) et les formats XDF ne sont couverts par aucune passe.
- Le fuzzer est structurellement aveugle à tout ceci : il n'appelle ni `h_boot` ni
  `disc_load`.


---

## M7 — Le menu Ctrl+F12 : insérer une disquette, réinitialiser, amorcer dessus

Le 2026-09-20. Fonctionnalité d'**hôte** : `Host/SdlMenu.cs` (neuf), plus deux
transcriptions dans `pc.cs` (`resetpc_cad`, `pc.c:344-351` ; `closepc` réduit,
`pc.c:576-592`). L'ORACLE du menu est `wx-sdl2.c:725-770`, le gestionnaire de commandes
du menu wxWidgets de PCem : seule l'enveloppe wxWidgets disparaît, le contenu des
commandes est celui de PCem (`disc_close` puis `disc_load` pour changer, `disc_close`
seul pour éjecter, `resetpchard` pour le reset matériel).

**Le cœur n'a rien eu à apprendre.** `resetpchard()` appelle déjà `disc_load(0,
discfns[0])` (`pc.cs:169`, fidèle à `pc.c:367`) et `disc_load` pose lui-même
`discfns[drive] = fn` (`disc.cs:127`). Le menu n'a donc qu'à insérer ; la disquette en
place au moment du reset est celle sur laquelle le BIOS amorce, sans une ligne de colle.
Aucun interrupteur DIP à ajuster non plus : SW1 rend `0x6D` en dur
(`keyboard_xt.cs:148-159`), qui déclare déjà « un lecteur de disquette » — c'est
`disc_notfound = 1000` (`disc.cs:286-289`) qui renvoie vers BASIC quand il n'y a rien.

### Ce que la lecture de `timer.cs` a réglé avant d'écrire le code

`resetpchard()` n'avait **jamais tourné deux fois** dans ce dépôt : son unique appelant
était la dernière ligne d'`initpc()`. Le menu le rend répétable, et chaque `pc_timer_t`
est une instance statique unique que `disc_reset`, `fdc_init`, `pit_init` et
`keyboard_xt_init` repassent à `timer_add`. La question était donc : deux resets
inscrivent-ils deux fois le même chronomètre ? Non — `timer_reset()` (`timer.cs:237-260`)
pose `timer_head = null` **et** `num_timers = 0`, donc vide le registre au lieu de se
contenter d'invalider les `magic` ; `timer_add` (`:265-289`) ré-inscrit depuis l'indice 0.
`resetpchard()` est idempotent quant aux chronomètres **parce que** `timer_reset()` est sa
première instruction. Et `mem_alloc()` fait `ram = new byte[...]` (`mem.cs:726`), un
tableau neuf à zéro : un reset matériel est un démarrage authentiquement à froid,
`0040:0072` ne porte pas 0x1234, le test mémoire de 46 s se déroule en entier. C'est ce
qui rend l'entrée « Reset materiel + turbo » utile et pas cosmétique.

### Non-régression — l'oracle n'a pas bougé d'une instruction

| Porte | Commande | Résultat |
|---|---|---|
| Diff d'amorçage sur disquette | `boot-diff roms 7000 --fda os/pcdos20/pcdos20b.img` | **26 750 702 instructions identiques** — le chiffre de § M6, inchangé |
| Amorçage DOS + DIR | `--boot roms 6500 --floppy-a …b.img --type "" --type "" --type DIR` | invite `A>` et catalogue complet |
| Déterminisme | `--slices 6000 --headless --verbose`, deux passes | 6000 tranches, 1915 blits, à l'identique |
| Turbo de lancement | `--turbo --verbose` | 5800 tranches en 9,34 s mur (x6,2) — la condition devenue relative à `_turboBase` (0 au lancement) est le test d'origine |
| Fenêtre + `closepc` | `--slices 2000 --verbose --floppy-a …b.img` | code 0, 486 images téléversées, `menu : 0 ouvertures` |
| Empreintes des images | `sha256sum -c os/os.sha256` | inchangées après toutes les passes, insertions comprises |

Le compilateur est resté à **0 avertissement** avec `TreatWarningsAsErrors` plein.

### Fonctionnel — observé à l'écran

Session fenêtrée sous XWayland, copies d'écran à l'appui :

- le menu s'ouvre en surimpression au-dessus de BASIC et **met la machine en pause** ; il
  est dessiné sur le renderer, entre `RenderTexture` et `RenderPresent`, et ne touche
  jamais `video.Buffer32` — c'est ce qui laisse `boot-diff` et les empreintes de
  framebuffer de § M5.1 strictement inchangés ;
- l'écran de choix liste les deux images de `os/` avec leur taille (`pcdos20b.img 180 Ko`,
  `pcdos20s.img 180 Ko`) et l'entrée `Parcourir...` ;
- **une image insérée par le menu est bien amorcée par un reset déclenché par le menu** :
  l'écran est passé de BASIC au secteur d'amorçage d'une disquette, `Non-System disk or
  disk error / Replace and strike any key when ready` — donc `pcdos20s.img`, précisément
  le comportement que § M6 attribue à cette image. Le `131` au-dessus est le code POST de
  bouclage cassette, préexistant et connu (§ M6), pas un symptôme neuf.

  **Ce qui n'est PAS établi, et la raison :** quelles entrées de menu ont produit cette
  séquence, ni dans quel ordre. La session était pilotée à la fois par XTEST et par
  l'utilisateur à son clavier, et les deux flux sont indiscernables dans le journal ; la
  dernière image sélectionnée à l'écran était `pcdos20b.img`, or c'est `pcdos20s.img` qui
  a amorcé. Le fait observé est donc le lien insertion → reset → amorçage, pas le chemin
  exact qui y mène. À refaire proprement quand un scénario scriptable sera possible.

**Un défaut trouvé à l'écran et corrigé** : les glyphes de `RenderDebugText` remplissent
leur cellule de 8 px de haut. Empiler les lignes tous les 8 px ne laisse aucun blanc entre
elles et la liste devient un pâté où les jambages touchent les hampes de la ligne suivante.
Hauteur de ligne portée à 10 px (`SdlMenu.Row`), plus des marges intérieures de 4 × 3 px.
Illisible au premier rendu, net au second ; rien qu'une relecture du code n'aurait montré.

**Un second défaut, trouvé en relisant** : le résultat du sélecteur natif n'était récolté
que depuis `HandleEvent`. Or le rappel arrive quand le système veut, souvent sans qu'aucun
évènement SDL ne suive : le fichier choisi serait resté en attente jusqu'à la frappe
suivante — ce qui se lit exactement comme « le sélecteur n'a rien fait ». D'où `Poll()`,
appelé à chaque tour de la boucle de pause.

**Fait d'outillage, coûteux à redécouvrir** : SDL3 **ignore les évènements clavier
synthétiques `XSendEvent`**, donc `xdotool key --window` ne l'atteint pas ; seul XTEST
passe, et il exige le focus clavier. Or GNOME sous XWayland refuse `xdotool
windowactivate` (prévention du vol de focus). Un scénario de menu ne se scripte donc pas
sur ce bureau sans un serveur X dédié (Xvfb/Xephyr, absents de cette machine).

### Reste ouvert — non mesuré, à faire à la main

Dans la fenêtre, `Ctrl+F12` puis les flèches. **Non vérifiés à l'exécution :**

1. le **réarmement du turbo** par « Reset materiel + turbo » (entrée n° 6). Les quatre
   affectations sont relues, la condition relative est prouvée au lancement, mais aucune
   seconde ligne `turbo :` n'a été observée ;
2. **« Parcourir... »** — jamais déclenché. Cette machine a `xdg-desktop-portal-gnome` et
   `zenity`, donc le sélecteur devrait s'ouvrir ; s'il échoue, le menu doit afficher le
   texte de `SDL.GetError()` et non rester muet ;
3. **Ctrl+Alt+Suppr** (`resetpc_cad`), redémarrage à chaud sans test mémoire ;
4. **éjection** puis reset → retour à BASIC ;
5. **changement de disquette à chaud sous DOS** puis `DIR` : premier exercice réel de
   DSKCHG (`fdc.cs:790-797`) ;
6. **`closepc()`** : écrire sous DOS sur une **copie** d'image, fermer la fenêtre, et
   vérifier que l'empreinte a changé. C'est la seule chose qui vide les tampons
   d'`img_writeback`, qui n'appelle aucun `Flush()` ;
7. **cinq resets d'affilée**, puis une minute d'horloge murale contre une vraie montre à
   l'invite DOS, et `--timer-check roms 300` toujours à 18,2065 Hz. La lecture de
   `timer.cs` dit que c'est propre ; la mesure resterait à le prouver.

---

## M7.1 — Créer une disquette vierge, et le premier formatage réel

Le 2026-09-20, dans la foulée de § M7. Entrée de menu **« Creer une disquette vierge... »**,
ORACLE `wx-createdisc.cc:22-29, 62-73` (`IDM_DISC_CREATE`). Tout est dans `Host/SdlMenu.cs` :
aucun fichier transcrit n'a bougé.

La création tient en dix lignes chez PCem et autant ici : un tampon de 512 octets à zéro,
écrit `nr_sectors` fois. **Pas de BPB, pas de FAT, pas de signature `0xAA55`** — le fichier
est strictement N × 512 octets nuls. L'octet est `0x00` et non `0xF6` : le `0xF6` est celui
de l'invité, passé en `params[4]` de la commande FORMAT TRACK du FDC, et il n'apparaît
qu'une fois la disquette formatée depuis la machine émulée.

**Quatre tailles, pas les neuf de PCem.** `drive_types[1]` (5,25" DD, le lecteur du 5150,
`pc.cs:141-142`) ne porte que `FLAG_HOLE0` et `max_track = 41` (`fdd.cs:76-78`) :
`fdd_can_read_medium` refuse toute image HD ou ED. Offrir 1,44 Mo serait offrir une image
que la machine rejette sans rien dire.

**Pourquoi une image nulle se relit toujours :** les cinq lectures de BPB d'`img_load`
(`disc_img.cs:211-220`) rendent 0, donc `bpb_sides < 1` est vrai et la garde de
`disc_img.cs:230` force la branche de devinette par **taille**. La même garde rend
inatteignable la division `0/0` de la branche BPB. La taille du fichier est donc le seul
déterminant de la géométrie — et c'est pourquoi le menu écrit noir sur blanc que **la taille
choisie EST le format que l'image acceptera**.

### Non-régression

| Porte | Résultat |
|---|---|
| `boot-diff roms 7000 --fda os/pcdos20/pcdos20b.img` | **26 750 702 instructions identiques**, inchangé depuis § M6 |
| `--slices 6000 --headless --verbose`, deux passes | 6000 tranches, 1915 blits, à l'identique |
| `sha256sum -c os/os.sha256` | les deux images d'origine intactes |
| Compilation | 0 avertissement, `TreatWarningsAsErrors` plein |

### L'image produite, à la création

`vierge-360k.img`, format « 360 Ko, 9 sect. × 40 pistes × 2 faces » :

| Contrôle | Commande | Résultat |
|---|---|---|
| Taille | `ls -l` | **368 640 octets** = 720 × 512, exactement `2 * 40 * 9` secteurs |
| Contenu | `tr -d '\0' < … \| wc -c` | **0** — pas un seul octet non nul |
| Réellement alloué | `du --block-size=1` | **368 640** et non 0 : l'écriture est faite secteur par secteur, pas par `SetLength`, donc le fichier n'est **pas sparse** |

`cmp … /dev/zero` a été écarté : `/dev/zero` étant infini, `cmp` s'arrête sur l'EOF de
l'image, **sort non nul**, et n'aurait prouvé que la concordance d'un préfixe.

### Le formatage sous DOS — premier exercice réel du chemin d'écriture

`FORMAT B:` sur l'image vierge, sous PC DOS 2.00, **aboutit** :

```
Formatting...Format complete
   362496 bytes total disk space
   362496 bytes available on disk
```

Et l'image sur disque est une disquette DOS structurellement valide :

| Offset | Octets | Sens |
|---|---|---|
| `0x000` | `eb 2c 90` | saut court + NOP, entrée de secteur d'amorçage |
| `0x003` | `49 42 4d 20 20 32 2e 30` | nom OEM « IBM  2.0 » |
| `0x00B` | `00 02` | 512 octets par secteur |
| `0x013` | `d0 02` | **720 secteurs au total** — exactement `2 * 40 * 9` |
| `0x015` | `fd` | descripteur de média 0xFD = 360 Ko, 9 secteurs, 2 faces |
| `0x018` | `09 00` | 9 secteurs par piste |
| `0x01A` | `02 00` | 2 têtes |
| `0x1FE` | `55 aa` | signature |
| `0x200` | `fd ff ff` | début de FAT : descripteur + marqueur de fin |

Histogramme du fichier entier : **365 984 octets de `0xF6`**, le remplissage de FORMAT —
soit la confirmation directe que le `0xF6` vient de l'invité et non de la création.

C'est le **premier exercice réel** de `disc_format` (`disc.cs:313`) → `disc_sector_format`
→ `STATE_FORMAT` → `img_writeback` (`disc_img.cs:539`), que § M6 listait comme « non exercé
par aucun oracle ». Les écritures atteignent bien le fichier : `img_writeback` n'appelle
aucun `Flush()`, et le contenu est lisible sur disque.

**Et ce n'est PAS comparé à l'oracle.** Vérifié dans l'arbre : `harness.h` n'expose aucune
injection clavier (ni `h_type`, ni `rawinputkey`) et `boot-diff` n'accepte que `--fda`
(`tools/iXtal26.Diff/Program.cs:122-150`). Il n'existe aujourd'hui aucun moyen de faire
taper `FORMAT B:` au côté C. **Un formatage réussi est une preuve d'usage, pas une preuve de
fidélité** : il montre que le chemin d'écriture produit une disquette que DOS relit, il ne
montre pas qu'il fait les mêmes cycles que PCem.

### Amorçer sur une disquette vierge : observé, non expliqué

Reset matériel avec l'image vierge en A:, avant tout formatage : l'écran rend le code POST
`131` et un curseur, **et rien d'autre**. Pas de bascule BASIC.

C'est cohérent avec le code et ce n'est pas le cas de § M7 : là le lecteur était *vide*,
`drives[].readsector` valait `null` et `disc_notfound = 1000` renvoyait vers l'INT 18h. Ici
l'image est **chargée**, `drive_empty[0] == 0`, les sept délégués sont câblés et `img_seek`
enregistre neuf secteurs valides par piste : le secteur 0 se lit parfaitement — 512 octets
nuls — et le BIOS saute dedans. `00 00` se décode `ADD [BX+SI], AL`, répété, puis
l'exécution part dans une RAM elle aussi à zéro.

**Non établi :** si le BIOS du 27/10/82 teste la signature `0xAA55` avant de sauter, et où
le processeur finit. La session était pilotée à la fois par XTEST et par l'utilisateur au
clavier, donc la séquence exacte n'est pas attribuable — seul l'écran l'est. PCem fait la
même chose par construction ; ce n'est pas un défaut d'iXtal26.

### Reste ouvert

1. **Les trois autres tailles** (160/180/320 Ko) : formule commune (`NrSectors * 512`)
   vérifiée sur 360 Ko, mais les fichiers 163 840 / 184 320 / 327 680 octets n'ont pas été
   produits ni mesurés.
2. **`FreeName`** — le suffixe `-2`, `-3`… contre l'écrasement n'a pas été exercé.
3. Le jalon qui s'en déduit, **M8** : ajouter `h_rawinputkey(int idx, int val)` à
   `harness.c` et `--type` à `boot-diff`, sur le modèle de `BootTest.TypeAndDump`
   (`BootTest.cs:62-126`). Les deux côtés taperaient la même chose aux mêmes tranches, et
   le chemin d'écriture — aujourd'hui le plus gros angle mort du dépôt — deviendrait
   diffable.

---

## M8 — La configuration machine, et la première en tandem avec l'oracle

Le 2026-09-20. `plugin-api/config.c` transcrit (`PluginApi/config.cs`), `loadconfig`
réduit à dix clés (`pc.cs`), la table `MODEL` à une entrée (`Models/model.cs`), et
**trois setters neufs au contrat de l'oracle** : `h_set_mem_size`, `h_set_drive_type`,
`h_set_bpb_disable`. `H_ABI_VERSION` passe de 1 à **2**.

### Ce que « configurable » veut dire ici, et pourquoi ce n'est pas « lire un fichier »

iXtal26 ne vaut que par la comparaison au C. **Un paramètre n'est donc configurable que
s'il est réglable à l'identique des deux côtés** — sinon `boot-diff` compare deux machines
différentes et appelle cela une divergence de cœur. Avant M8 un seul l'était, `discfns[]`,
et `h_boot` portait le reste en dur : `mem_size = 640` (`harness.c:410`),
`fdd_set_type(0/1, 1)` (:428-429).

La mécanique retenue, qui est celle de `discfns` généralisée : **l'outil de diff lit le
fichier UNE fois**, puis pousse chaque scalaire des deux côtés — `h_set_*` avant `h_boot`,
la globale C# avant `initpc`. Il ne laisse jamais chaque côté relire le fichier : deux
résolutions de chemin indépendantes peuvent trouver deux fichiers différents, et le
harnais ne saurait pas distinguer cela d'une divergence de cœur.

**C'est aussi la raison du fichier plutôt que d'une UI.** Une UI de lancement serait la
seule source de vérité que l'oracle C ne peut pas lire. Un fichier est relu par l'outil, et
citable ici — « cette mesure a été faite avec cette configuration ».

### Les défauts ne sont pas ceux de PCem, et c'est la règle

PCem par défaut : `mem_size = 4096`, `drive_*_type = 7` (3,5" ED). Ici chaque défaut
**reproduit exactement la machine d'avant M8**, parce que ce fichier est plein de mesures
qui la supposent (§ M4.1 « 640 Ko », § M4.6 « 51,7 s », § M6 « 26 750 702 »). Un défaut qui
dérive ferait cesser en silence toutes ces mesures d'être reproductibles. Sans `--config`,
aucun fichier n'est lu.

### Portes

| Porte | Commande | Résultat |
|---|---|---|
| **Défauts inchangés** | `boot-diff roms 7000 --fda …b.img` | **26 750 702 instructions identiques** — le chiffre de § M6, à l'unité |
| **Configuration en tandem, 64 Ko** | `boot-diff roms 3000 --config` (`mem_size = 64`) | **11 645 075 identiques** |
| **En tandem, 256 Ko + aucun lecteur** | `--config` (`mem_size = 256`, `drive_*_type = 0`) | **16 318 838 identiques** |
| **`disc_a` depuis le fichier** | `--config` (`disc_a = …b.img`) | **1 069 757 identiques** |
| DOS + DIR, défauts | `--boot 6500 --floppy-a … --type DIR` | catalogue rendu |
| Déterminisme | `--slices 6000 --headless`, deux passes | identiques |
| Empreintes des images | `sha256sum -c os/os.sha256` | inchangées |

La deuxième ligne est le vrai livrable : elle prouve que la configuration se propage **des
deux côtés à la fois**. Sans elle, le reste ne vaudrait rien.

### Le POST est linéaire en RAM — mesuré, et c'est le bénéfice visible

Première tranche où l'invite BASIC est à l'écran, par bissection sur une échelle fixe
(donc des **majorants**, pas des valeurs exactes) :

| RAM | Invite BASIC | Temps émulé |
|---|---|---|
| 64 Ko | ≤ tranche 1200 | **≤ 12,0 s** |
| 128 Ko | ≤ tranche 1800 | ≤ 18,0 s |
| 256 Ko | ≤ tranche 2400 | ≤ 24,0 s |
| 640 Ko | ≤ tranche 5200 | ≤ 52,0 s |

Cohérent avec les 51,7 s de § M4.6 et avec « le test mémoire est linéaire en RAM ». Un
5150 à 64 Ko démarre donc en douze secondes **sans turbo** — c'est la machine d'époque, à
sa vitesse d'époque, avec la RAM que la plupart avaient vraiment.

### Contrôles de robustesse

| Entrée | Comportement |
|---|---|
| `mem_size = 100` | refusé (hors pas de 32), message nommé, repli sur 640 |
| `mem_size = 0x100` | **256** — la sémantique `%i` de `sscanf` est transcrite, préfixe hexa compris |
| `model = ibmxt` | refusé, avec la liste des machines connues |
| `--config` absent du disque | refusé avant tout amorçage |
| fichier + `--ram 128` | **128** — la ligne de commande surcharge le fichier |

### Politique de bug inversée, et pourquoi

`plugin-api/config.c` **n'est pas lié dans l'oracle** (`tools/oracle/Makefile` ;
`config_get_int` y est un stub qui rend le défaut). Il n'a donc aucun pendant exécutable,
et ses défauts sont **corrigés** au lieu d'être reproduits, chacun marqué `// DEVIATION:` :
dernière ligne perdue quand le fichier ne finit pas par un saut de ligne (`config.c:145`),
tabulations non reconnues comme blanc (`:151`), `sscanf` sans test de retour qui rend une
valeur indéterminée au lieu du défaut (`:293, :313`), `config_free` qui laisse un pointeur
pendant (`:71-92`). C'est l'inverse de la politique de `disc.c` (PB-14/15/18), et la raison
tient en une ligne : là-bas l'oracle exécute le bug, ici il n'existe pas.

Même logique pour `model_get_model_from_internal_name` (`model.c:168-178`), qui rend
**l'indice 0 en silence** sur un nom inconnu — donc une AMI XT clone au lieu de la machine
demandée. Ici : refus nommé. Mesurer une machine pour une autre est exactement ce que ce
dépôt existe pour empêcher.

### La moitié écriture du moteur, et sa porte

`config_set_*` et `config_save` n'ont aucun appelant tant que le menu n'édite pas la
configuration — et un chemin mort est indiscernable d'un chemin cassé. `config.c` n'étant
pas lié dans l'oracle, il n'existe rien à quoi le comparer : d'où une porte d'aller-retour,
`iXtal26.Diff config-check`, qui écrit un fichier avec les setters, le relit avec le
parseur et compare. **Onze contrôles, tous verts**, dont trois que le C échouerait :

| Contrôle | Ce qu'il attrape |
|---|---|
| clé réécrite (`mem_size` 256 puis 512) | une seconde entrée au lieu d'une mutation en place — `find_entry` rendrait toujours la première et `config_save` écrirait les deux |
| `[CGA] addr = 0x220` → relu **544** | `config_set_int` écrit en décimal (`sprintf "%i"`), `config_get_int` relit les deux graphies |
| `[GL3] input_scale = 1.5` → `1.500000` | le `%f` à six décimales, et le point décimal quelle que soit la locale |
| section et clé absentes → défaut | le chemin par lequel toute la configuration par périphérique tient sans fichier |
| **ligne indentée par tabulation** | `config.c:151` ne saute que les espaces : le C casse le nom de clé |
| **valeur non numérique → défaut** | `config.c:293` ne teste pas le retour de `sscanf` et rend une valeur indéterminée |
| **dernière ligne sans saut final** | `config.c:145` fait `fgets` puis `feof` : le C perd la ligne |

Le fichier produit confirme aussi la forme : section racine anonyme émise **sans**
en-tête, et une ligne blanche avant chaque `[section]`.

### Reste ouvert

| Paramètre | Bloqué par |
|---|---|
| **Vitesse CPU** | Le budget de tranche est DÉRIVÉ côté C# (`pc.cs`, `cpu_get_speed() / 100`) et **littéral** côté C (`harness.c:489`, `4772728 / 100`), plus `bench.c:28` et `BootProfile.cs:76`. Le rendre configurable sans corriger cela ferait tourner les deux côtés à des vitesses différentes **sans aucun diagnostic** |
| **Carte vidéo** | Une seule carte transcrite ; quatre éditions en tandem pour un choix à une valeur |
| **`hasfpu`** | Les deux côtés dépendent d'un zéro IMPLICITE, jamais d'une affectation. Le rendre réglable exige d'ajouter l'affectation explicite des deux côtés, sinon un seul change le bit 1 de SW1 |
| **`video_speed`** | `video_updatetiming` est un no-op côté C alors que `video.cs` calcule vraiment — réglage à sens unique tant que ce n'est pas tranché |
| **Une deuxième machine** | La table est livrée, les machines non. Un XT 5160 demande son BIOS, ses périphériques et son propre passage au vert |

---

## M8.1 — `Floppy/` et `Disc/` lisibles, sans perdre l'oracle

Le 2026-09-21. Point de départ : les noms de PCem dans `fdc.cs` ne se décodent pas sans
la fiche du 765, et l'opacité est autant dans les **littéraux** — 42 affectations de
`fdc.stat` en hexadécimal nu — que dans les identifiants. La contrainte : la revue ligne
à ligne contre `pcem-dev/` est la seule vérification *statique* du projet, et un nom qui
change la casse.

D'où l'ordre, qui est tout l'intérêt du jalon : **la table des noms d'abord, le renommage
ensuite**. Tant que les noms sont ceux de PCem, la table est un glossaire de confort ;
dès qu'un nom change, elle devient le seul chemin de retour vers `pcem-dev/`, et R1(e) la
rend obligatoire.

### Le repère, pris avant la première modification

`tools/oracle/harness.c` et les trois fichiers de `tools/iXtal26.Diff/` avaient été
commités entre § M6 et ici. Les chiffres de § M6 ne pouvaient donc pas être *supposés*
valides : ils ont été **remesurés sur `3ed82cc`** avant de toucher à quoi que ce soit.

| Épreuve | Repère | § M6 |
|---|---|---|
| `boot-diff roms 6000` | 25 457 269 | identique |
| `boot-diff roms 7000 --fda …b.img` | 26 750 702 | identique |
| `boot-diff roms 6500 --fda …s.img` | 27 494 583 | identique |
| `disc-probe 5300 / 5310 / 5400 / 5600` | 20 champs identiques | identique |

Les trois se reproduisent à l'unité. C'est ce repère — pas § M6 — qui a servi de
référence ensuite : sans lui, une dérive préexistante se serait lue comme une faute de
renommage, ou l'inverse.

### Ce qui a été fait, et ce que chaque étape a coûté

| Étape | Effet | R2 |
|---|---|---|
| Constantes nommées (`MSR_*`, `ST0_*`, `ST1_OR`, `ST3_*`, `RES_*`, `EXEC_*`) | 42 affectations + formes masquées deviennent lisibles | +29 lignes, 1 041/961 = **1,08** |
| Tables `// noms:` des cinq fichiers | 36 + 16 + 13 + 12 + 9 lignes, cap R1(e) = 40 | commentaires, nul |
| Renommage `fdc.cs` | 20 identifiants, 263 sites | **inchangé** — renommage pur |
| Renommage `disc_sector.cs`, `disc.cs` | 5 identifiants (CHRN, `forced_writeprot`) | inchangé |
| `fdc-trace` | outillage, hors fichiers transcrits | sans objet |

À chaque commit des deux étapes de renommage, la batterie complète : **tous les chiffres
identiques au repère, à l'unité**, images inchangées, `check-oracle` 22 vérifiés 0 dérive.
Le renommage étant neutre pour le comportement, tout écart aurait été une faute de frappe
et rien d'autre — c'est ce qui rend une étape de 263 sites sûre malgré sa taille.

### Trois choses apprises

1. **`sed` ne voit pas ce que le compilateur voit, et inversement.** Le renommage des
   champs CHRN par `s\.[chrn]\b` a manqué `disc_sector_data[...].n` (`disc_sector.cs:187`
   et `:209`), hors du préfixe `s.`. Le compilateur l'aurait signalé dès le renommage de
   la déclaration ; un `grep` ciblé l'a trouvé avant. C'est l'argument pour le renommage
   sémantique plutôt que textuel — pas la théorie, un cas.
2. **Les couplages par chaîne ne sont pas typés.** `tools/iXtal26.Diff/BootDiff.cs:264`
   porte les 20 étiquettes de la sonde sous forme de chaînes. Elles désignent les globales
   **du C** (`harness.c:559` : `extern int discint, lastbyte, paramstogo, bit_rate`) et
   gardent donc les noms PCem. Les renommer « par cohérence » aurait fait mentir la sonde
   sur ce qu'elle compare, sans le moindre diagnostic.
3. **Nommer à moitié est pire que ne pas nommer.** `ST0_NR` n'a l'air décoratif que
   jusqu'au moment où le `0x68` de `fdc_callback` reste littéral au milieu de ses voisins
   nommés. Et `Sense drive status` rend **ST3**, dont la disposition n'est celle d'aucun
   des trois autres registres : appliquer les `ST0_*` à son `0x28` aurait produit du code
   faux-lisible, ce qui est le pire des deux mondes.

### Deux défauts de PCem trouvés en inventoriant les noms

Une table qui dit ce qu'un champ désigne bute sur ceux qui ne désignent plus rien.

- **PB-19** — `fdc.written` est déclaré, remis à zéro deux fois, testé une fois, et
  **jamais posé à 1** dans tout l'arbre vendoré : la détection d'écrasement en écriture
  hors DMA est morte, là où son symétrique en lecture (`fdc.data_ready`) fonctionne. Le
  chemin d'écriture n'étant pas exercé par l'amorçage de PC DOS 2.00 (§ M6), rien ne
  l'avait mis en évidence.
- **PB-20** — sept champs et globales morts : `abort`, `discmodified[]`, `discrate[]`,
  `motorspin`, `fdc_ready`, `fdc_indexcount`, `defaultwriteprot`, `oldtrack[]`. Deux ont
  leur `extern` **commenté** dans `disc.h`, et `oldtrack` n'est lu que par un bloc
  `ddnoise_seek` commenté : ils ont eu des lecteurs, retirés sans que les définitions
  suivent.

### La doctrine a bougé trois fois, et la troisième était une correction

R3 : plafond de `TRANSCRIPTION.md` porté de 200 à 220 lignes (214 aujourd'hui), le
relèvement inscrit dans la règle. R1 : catégorie **(e)**, la table `// noms:`, bornée à
40 lignes et sans phrase. Table des conventions : ligne « Nommage » exceptée pour
`Floppy/` et `Disc/`, **pour les deux classes de déviation** — renommer un identifiant et
nommer une valeur de registre, la seconde réécrivant une expression de PCem et pas
seulement une étiquette.

La troisième modification a corrigé la première : R1(e) disait « deux colonnes », ce qui
ne tient plus dès qu'un nom change — il en faut trois, l'ancien, le nouveau, le sens. Une
règle écrite la veille, invalidée par sa première application réelle.

### Ce que `fdc-trace` montre

`fdc-trace roms 5600 --fda os/pcdos20/pcdos20b.img` : **734 transitions pour 23 093 051
instructions**, et la séquence d'amorçage se lit sans la fiche technique — reset,
Specify, Recalibrate, Seek, puis les `Read data` alternant « cherche secteur » et « lit
secteur ». Il échantillonne depuis l'outillage après chaque `Step()`, comme `disc-probe` :
aucun point d'accroche dans le cœur, aucune ligne vivante ajoutée, rien qui tourne pendant
les passes de comparaison. L'oracle n'y participe pas — son instance `fdc` est `static`
dans `fdc.c`, invisible depuis `harness.c`.
