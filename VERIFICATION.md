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
`discfns[drive] = fn` (`disc.cs:147`). Le menu n'a donc qu'à insérer ; la disquette en
place au moment du reset est celle sur laquelle le BIOS amorce, sans une ligne de colle.
Aucun interrupteur DIP à ajuster non plus : SW1 rend `0x6D` en dur
(`keyboard_xt.cs:148-159`), qui déclare déjà « un lecteur de disquette » — c'est
`disc_notfound = 1000` (`disc.cs:306-309`) qui renvoie vers BASIC quand il n'y a rien.

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
   DSKCHG (`fdc.cs:878-887`) ;
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
`pc.cs:141-142`) ne porte que `FLAG_HOLE0` et `max_track = 41` (`fdd.cs:92-94`) :
`fdd_can_read_medium` refuse toute image HD ou ED. Offrir 1,44 Mo serait offrir une image
que la machine rejette sans rien dire.

**Pourquoi une image nulle se relit toujours :** les cinq lectures de BPB d'`img_load`
(`disc_img.cs:223-232`) rendent 0, donc `bpb_sides < 1` est vrai et la garde de
`disc_img.cs:242` force la branche de devinette par **taille**. La même garde rend
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

C'est le **premier exercice réel** de `disc_format` (`disc.cs:333`) → `disc_sector_format`
→ `STATE_FORMAT` → `img_writeback` (`disc_img.cs:551`), que § M6 listait comme « non exercé
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
3. ~~Le jalon qui s'en déduit, **M8** : ajouter `h_rawinputkey(int idx, int val)` à
   `harness.c` et `--type` à `boot-diff`…~~ **Fait à § M11**, trois jalons plus tard que
   prévu : M8 est parti sur la configuration, M9 sur le son, M10 sur la deuxième machine.
   La prédiction était juste jusque dans les noms de fonctions ; seule la date était
   fausse.

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
| ~~`model = ibmxt`~~ | **périmé depuis § M10** : la machine existe et démarre. Le contrôle valait « un nom hors table est refusé » ; il est rejoué à § M10 avec `model = ibm5170`, qui lui n'existe pas |
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
| **Vitesse CPU** | **Fermé en § M16** (étapes 3 et 4) : clé `cpu`, vrai `cpu_set()` des deux côtés, budget = rspeed / 100, empreinte CPU confrontée. — Le budget de tranche est DÉRIVÉ côté C# (`pc.cs`, `cpu_get_speed() / 100`) et **littéral** côté C (`harness.c:489`, `4772728 / 100`), plus `bench.c:28` et `BootProfile.cs:76`. Le rendre configurable sans corriger cela ferait tourner les deux côtés à des vitesses différentes **sans aucun diagnostic** |
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
   champs CHRN par `s\.[chrn]\b` a manqué `disc_sector_data[...].n` (`disc_sector.cs:191`
   et `:213`), hors du préfixe `s.`. Le compilateur l'aurait signalé dès le renommage de
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

### L'angle mort de la batterie : elle ne lit que

Les huit épreuves ci-dessus **n'écrivent jamais**. `boot-diff` et `disc-probe` amorcent et
lisent ; le renommage, lui, a touché `fdc_writeprotect`, les blocs de résultat de WRITE et
de FORMAT, `byte_written`, et les usages de `s.cyl` / `s.sector_id` dans `STATE_WRITE_*` et
`STATE_FORMAT`. Une permutation `RES_ST1`/`RES_ST2` y aurait compilé proprement et serait
restée **invisible sur un amorçage en lecture seule**.

D'où la reprise de la procédure de § M7.1, cette fois sans fenêtre : image vierge de
368 640 octets en B:, PC DOS 2.00 en A:, `FORMAT B:` tapé par `--boot … --type`. L'image
produite est une disquette DOS valide, aux mêmes marqueurs qu'à § M7.1 :

| Offset | Octets | Sens |
|---|---|---|
| `0x000` | `eb 2c 90` + `IBM  2.0` | saut court et nom OEM |
| `0x013` | `d0 02` | 720 secteurs = 2 × 40 × 9 |
| `0x015` | `fd` | descripteur de média 360 Ko |
| `0x1FE` | `55 aa` | signature |
| `0x200` | `fd ff ff` | début de FAT |

Histogramme : **366 096 octets de `0xF6`**, le remplissage de FORMAT. § M7.1 en comptait
365 984 : l'écart n'est pas une divergence mais la commande elle-même — les `--type ""`
qui suivent le formatage font répondre Entrée à `Format another (Y/N)?`, donc l'image
subit **plusieurs passes** là où § M7.1 en faisait une seule au clavier. Le nombre exact
dépend du nombre d'Entrées ; les marqueurs de structure, eux, ne bougent pas.

**Deux chemins d'écriture sont exercés, pas un.** Le `0xF6` vient de FORMAT TRACK
(`STATE_FORMAT` → `img_writeback`). Mais `eb 2c 90` en 0x000 et `fd ff ff` en 0x200 ne
sont pas du `0xF6` : DOS a écrit le secteur d'amorçage, les FAT et le répertoire racine
**après** le formatage, par WRITE DATA — donc `case 0x05` de `fdc_write`, son bloc de
résultat, `fdc_getdata` et `STATE_WRITE_FIND_SECTOR`/`STATE_WRITE_SECTOR`. C'est ce qui
rend l'épreuve discriminante pour le renommage : une permutation `RES_R`/`RES_N` dans
l'un des deux blocs de résultat aurait fait mal lire la phase résultat à DOS, et la
disquette ne serait pas structurellement valide.

**Deux caractères manquaient pour y arriver.** La table de frappe du banc (`BootTest.cs`)
ne portait ni `:` ni `/` : `FORMAT B:` arrivait en `FORMAT B` et DOS 2.00 répondait
`Invalid parameter`. Aucune commande DOS ne pouvait désigner un lecteur ni porter un
commutateur — le chemin d'écriture était hors d'atteinte sans fenêtre, et personne ne
l'avait remarqué parce que § M7.1 l'avait exercé **à la main**. Deux `case` dans
`ScancodeFor` et la vérification devient rejouable.

~~Ce que cela prouve reste ce que disait § M7.1 : **une preuve d'usage, pas une preuve de
fidélité.** `harness.h` n'expose toujours aucune injection clavier, donc l'oracle ne peut
pas taper `FORMAT B:`.~~

**Périmé par § M11.** `h_rawinputkey` et `h_kbd_process` existent, l'oracle tape
`FORMAT B:`, et les deux côtés produisent la **même disquette octet pour octet**. Ce
paragraphe est conservé barré plutôt que réécrit : il datait la limite, et savoir combien
de temps elle a tenu vaut mieux que de faire comme si elle n'avait pas existé.

## M9 — Le haut-parleur : le bip du POST, et une sonde pour l'entendre

Point de départ : la machine bipait déjà et personne ne l'écoutait. `pit.cs:543`,
`pit.cs:668-680` et `keyboard_xt.cs:125-130` tenaient `speakval`, `speakon`,
`ppispeakon`, `speaker_gated` et `speaker_enable` à jour depuis M4 — **en écriture
seule**. Le seul consommateur, `speaker_update()`, était un corps vide.

### La contrainte qui commandait tout, et ce qu'elle a montré

`sound_reset()` (`sound.c:260-261`) installe un chronomètre émulé à **48 kHz**. Le
harnais stubait le haut-parleur en entier (`harness_stubs.c:153-157`) et ne liait pas
`sound.c` : brancher le son côté C# seul aurait fait modéliser deux machines
différentes, `n_timer_process` étant un compteur comparé (`harness.h:88`,
`Diag/HState.cs:78`).

L'ordre de grandeur annoncé au plan était faux, et la mesure l'a corrigé deux fois.

1. **`timer_process()` n'est pas rare.** `xt_init` réaffecte le canal 1 à
   `pit_refresh_timer_xt` (`model.c:205`) et le BIOS le programme à 18 ⟹ 216 ticks de
   14,318 MHz, soit ~66 kHz, plus `cga_poll` au rythme ligne. Ajouter 48 kHz n'est pas
   un ×1 300.
2. **Le compteur n'est pas la somme des fréquences** : un seul appel dépile *tous* les
   chronomètres échus. Toute porte calée sur un ratio prédit aurait déclenché à tort.

Et un résultat qu'aucune des deux prédictions n'avait : **avec le chronomètre installé
d'un seul côté, le diff d'amorçage reste vert sur 25 457 269 instructions.** Le
chronomètre à 48 kHz ne perturbe ni la trajectoire architecturale ni le `tsc` du 8088.
Ce vert-là est une mesure, pas un soulagement : il dit que le hachage par instruction de
la phase 1 ne regarde que l'état architectural, et que la parité du son devait donc être
prouvée autrement.

### La doctrine de la vidéo, réappliquée

`sound.c` traîne dix-neuf `SOUND_CARD`, le fil CD, ATAPI et OpenAL. Même arbitrage que
`src/video/video.c` et ses 90 symboles de cartes (`harness_stubs.c`) : **pas lié**.
`harness.c` en reprend le cœur temporel, copié verbatim.

`sound_speaker.c`, lui, est **inclus** — `#include`, pas `SRC`. `speaker_buffer` (`:10`),
`speaker_pos` (`:12`) et `speaker_get_buffer` (`:39`) sont `static` : compilé à part, le
fichier ne montre rien et `h_speaker_probe` n'aurait rien à lire. C'est mot pour mot la
raison pour laquelle `harness.c:40` fait `#include "808x.c"`.

### La sonde, et ce qu'elle a attrapé au premier tir

`h_speaker_probe` / `sound_speaker.Probe()` : neuf champs, même ordre des deux côtés. Le
neuvième est une **empreinte FNV-1a cumulative du son réellement produit**, prise sur
`outbuffer` juste avant `givealbuffer`, donc après le passage des handlers.
`speaker_get_buffer` étant `static` dans un fichier vendoré, on ne peut ni l'intercepter
ni y ajouter une ligne ; et `speaker_buffer` seul ne dirait rien de ce passage.

`speaker-probe` **échoue si l'empreinte est restée à sa graine**. Deux silences
concordants ne prouvent rien — c'est le faux vert de § M4.5 sous une autre forme.

| Portée | Oracle | C# |
|---|---|---|
| `speaker-probe roms 300` | 9 champs, empreinte `CF1968EA83F07483` | identiques |
| `speaker-probe roms 6000` | 9 champs, empreinte `384EC07B1B64CC83` | identiques |

À 6 000 tranches le haut-parleur est **actif** : `speaker_gated = 1`, `speakon = 1`,
`ppispeakon = 1`, `speaker_pos = 815`. À 300 il ne l'est pas — l'empreinte n'y hache que
du silence, et un vert à 300 tranches seul aurait été un vert creux.

**Premier tir, première prise : `speakval` divergeait.** Oracle −2 147 483 648, C# 8 192.
`pit.c:418` divise par `pit->l[0]` sans le tester, et la ligne est atteinte par chute de
`case` depuis les trois canaux. Mesuré : tranche 231 d'un amorçage 640 Ko, `l[0] = 0`,
`l[2] = 65535`, donc `+inf`. Le `(int)` du C rend l'entier indéfini de `cvttss2si`,
`0x80000000`, que le clamp de la ligne suivante ne rattrape pas. **.NET sature** —
`(int)float.PositiveInfinity` vaut `int.MaxValue`, que le clamp ramène à `0x2000`.
`PB-21`, reproduit à `pit.cs:543` avec la garde explicite qu'impose .NET. C'est le seul
endroit du dépôt où une conversion flottant→entier a dû être écrite à la main.

### L'injection de panne qui valide la sonde

`sound_speaker.c:32-33` place `if (!speaker_enable) was_speaker_enable = 0;` **dans** la
boucle par échantillon. C'est un verrou d'un échantillon. Hisser ce test hors de la
boucle — le nettoyage évident — a été fait exprès, puis mesuré :

```
   speaker_gated … speaker_pos, sound_pos_global   identiques
 * sound_hash     oracle 4057391949280496771 | C# 7097314269654152323
```

Huit champs identiques, l'empreinte seule diverge. La sonde regarde bien les
échantillons, et le « ne pas nettoyer » du C est audible.

### Portes

| Épreuve | Résultat |
|---|---|
| `make -C tools/oracle selftest` | vert, 0 échec |
| `boot-diff roms 6000` | vert, **25 457 269** instructions identiques |
| `boot-diff roms 7000 --fda pcdos20b.img` | vert, **26 750 702** instructions identiques — le chiffre de § M6, à l'unité |
| `fuzz --mode stream --rounds 40 --instr 200` | vert, 8 000 instructions, 32 champs |
| `speaker-probe roms 6000` | 9 champs identiques, empreinte non nulle |
| `check-oracle.sh` | 24 transcrits vérifiés, 0 dérive, arbre vendoré OK |
| `dotnet build -c Release` | 0 avertissement |

### Parité R2, accolades seules exclues

| Fichier | C | C# | ratio |
|---|---|---|---|
| `Sound/sound_speaker.cs` | 32 | 35 | 1,09 |
| `Sound/sound.cs` | 65 | 50 | 0,77 |

`sound.cs` est `partial` : le plancher −25 % ne s'y applique pas, et les quinze lignes
absentes sont exactement le fil CD, ATAPI et OpenAL, déclarés `// omitted:` sur place.

### Le coût du chronomètre, mesuré

Protocole de § M5.1 : Release, `taskset -c 0-3`, `--repeat 5`, ordre alterné. « Avant »
mesuré dans un `git worktree` sur `5537b46`, avec son propre oracle construit sans le
chronomètre.

| | C (× temps réel) | C# (× temps réel) | ratio C#/C |
|---|---|---|---|
| Avant M9 | 24,51 (disp. 5,4 %) | 18,90 (disp. 2,4 %) | 1,297 |
| Après M9 | 23,15 (disp. 2,9 %) | 17,59 (disp. 7,1 %) | 1,316 |
| Écart | **−5,5 %** | **−6,9 %** | +1,5 % |

Les deux côtés paient, et presque pareil : le coût est celui du chronomètre, pas d'une
maladresse C#. Le ratio bouge de 1,5 %, dans le bruit. **17,6× le temps réel** laisse
la cible des 100 % tenue avec la même marge qu'avant.

Une première mesure « après » a donné 16,60× avec **62 % de dispersion côté C** : elle a
été jetée et refaite machine au repos. C'est la règle de § M5.1, et elle a servi ici.

### L'étage hôte

`Host/SdlAudio.cs`, pendant de `soundopenal.c`. Mode *push* — flux ouvert avec un
callback **nul**, alimenté depuis le fil d'émulation comme le crochet de blit. Un
callback audio SDL3 tournerait sur un fil séparé et serait le premier thread du projet.

**Contre-pression, et c'est le point d'architecture.** `soundopenal.c:173` ne dépose un
bloc que s'il reste un tampon libre parmi quatre, et **jette** le bloc sinon : l'étage de
sortie de PCem est perdant par conception, le rythme d'écriture étant celui du CPU émulé.
Plafond repris à l'identique, quatre blocs, ~200 ms.

```
--slices 6000 --verbose   audio : 181 blocs déposés, 1000 jetés (2400 échantillons, 50 ms)
```

1 181 blocs produits pour 60 s émulées, 181 déposés. La boucle `--slices` n'attend pas
l'horloge : elle déroule 60 s émulées en ~10 s murales, et les 181 blocs déposés valent
9,05 s d'audio — donc **le périphérique consomme bien à 48 kHz temps réel**, et à 100 %
de vitesse d'émulation production et consommation se rejoignent.

### Le turbo mangeait le bip — trouvé en relisant, corrigé, mesuré

La première version coupait la sortie pendant tout le turbo, et le plan justifiait ce
choix par « le bip arrive à la reprise ». C'était faux, et c'était le genre de phrase
qu'on écrit sans la vérifier : **le bip tombe à la tranche 4769, le turbo par défaut
court jusqu'à 5800.** Le profil de lancement de l'IDE étant `--turbo`, le jalon livrait
donc exactement le silence qu'il devait supprimer.

Fenêtre d'activation, mesurée sur l'oracle : `speaker_enable` non nul **une seule fois**,
tranches **4769 à 4795** — 27 tranches, 0,27 s émulée. Il n'y en a pas d'autre jusqu'à
la tranche 6 000.

Correction : **le bip met fin au turbo.** Le BIOS n'arme le haut-parleur qu'une fois le
test mémoire passé — le bip *est* le signal de fin de POST, donc précisément ce que le
turbo existe pour atteindre. Et c'est correct à n'importe quelle taille RAM, là où une
tranche en dur ne l'aurait été qu'à 640 Ko. Même mécanique que la frappe qui l'arrête
déjà : on pose `_turboStopped`, et la transition existante rend l'horloge neuve et
rouvre la sortie.

```
turbo : 4989 tranches (49.9 s émulées) en 3.64 s mur (x13.7), 1190 images sautées,
        rendu au temps réel par le bip de fin de POST
```

**4989 et non 4769**, et l'écart n'est pas un défaut : c'est `pc.runpc` contre `h_run`,
le fait déjà consigné en § M6. `h_run` remet `cycles` à zéro en tête de tranche,
`pc.runpc` reporte le reliquat comme `pc.c:475` — 4,4 % de tranches en plus pour le même
travail émulé. Les deux côtés voient le même bip, pas à la même tranche.

Ce que le dépôt des blocs dit autour du bip, instrumenté puis retiré :

| Tranche | `enable` | muet | déposés | jetés |
|---|---|---|---|---|
| 4985 | 0 | oui | 0 | 979 |
| 4989 | 2 | **non** | 0 | 980 |
| 4995 | 2 | non | 1 | 980 |
| 5000 | 2 | non | 2 | 980 |
| 5020 | 0 | non | 6 | 980 |
| 5100 | 0 | non | 21 | 980 |

**Le compteur de blocs jetés ne bouge plus d'un seul après la reprise.** Le bip est
déposé en entier, du premier bloc au dernier. Reste au plus un bloc de 50 ms produit
pendant la tranche 4988, avant que la coupure ne se lève : sur 210 ms de bip, inaudible.

### Ce que ce vert ne dit pas

1. **Le taux de perte en régime cadencé n'est pas mesuré globalement.** Il l'est autour
   du bip — zéro bloc jeté sur 21 déposés — mais pas sur une session entière : le bilan
   `--verbose` ne s'imprime qu'à la fermeture de la fenêtre, et aucun mode borné ne
   reste cadencé.
2. **L'oracle est muet.** `givealbuffer` y est vide : ce qui est comparé, ce sont les
   échantillons *avant* la sortie. Que SDL3 les restitue comme OpenAL le ferait n'est
   établi par rien.
3. **Le turbo coupe la sortie, et c'est le bip qui l'arrête.** Deux décisions d'hôte,
   sans pendant chez PCem, donc hors de portée de tout oracle. La seconde couple le
   cadençage à un état de la machine émulée — l'hôte lit `speaker_enable`. C'est le
   premier endroit où il le fait, et ça mérite d'être su.
4. **Aucune carte son.** Le registre `SOUND_CARD` n'est pas transcrit ; `sound_handlers`
   n'a qu'une entrée, et elle vient de `speaker_init`.

## M10 — L'IBM XT 5160 : une deuxième machine, et une table qui cesse d'être une constante

`models[]` n'avait qu'une entrée. **Une table à une seule entrée est une constante**, et
rien n'avait jamais prouvé que le code n'est pas câblé en dur sur le 5150. Le XT 5160 est
le moyen le moins cher de lever ce doute, et le seul disponible sans quitter le 8088.

### Pourquoi le jalon est petit, et pourquoi ce n'est pas une déception

`grep -rn "ROM_IBMXT" pcem-dev/src/` rend **deux lignes** : le chargeur (`mem_bios.c:166`)
et l'entrée de table (`model.c:782`). Rien d'autre, dans tout PCem, ne distingue un XT
d'un PC. Les deux machines partagent le **même `xt_init()`**, et le `xt_init` du C# était
déjà neutre — il omet cassette et gameport, comme celui du harnais.

Le delta de comportement était donc **déjà transcrit** : les trois gardes
`romset == ROM_IBMPC` de `keyboard_xt.c` ont toutes leur branche `else` en C#. Le travail
n'était pas d'écrire une machine, mais de prouver que celle qui dormait démarre.

### Le seul câblage en dur, et il était exactement là où on le cherchait

`model.cs` déclarait `MIN_RAM` / `MAX_RAM` / `RAM_GRANULARITY` en **constantes de classe**,
alors que `MODEL` portait déjà `min_ram` / `max_ram` / `ram_granularity` — **écrits depuis
M8, et lus par personne**. Le XT est à 64 Ko de granularité là où le 5150 est à 32 : c'est
la deuxième machine qui rend ces champs vivants. Vérifié dans les deux sens :

| Entrée | ibmpc | ibmxt |
|---|---|---|
| `mem_size = 96` | accepté (pas de 32) | **refusé** (pas de 64), message nommant la machine |

`DEFAULT_RAM` reste une constante, et c'est délibéré : `pc.cs:94` est un initialiseur de
champ, évalué avant qu'aucun modèle ne soit choisi. Ce sont les 640 Ko du 5150, parce que
toute mesure de ce fichier les suppose. Ce n'est **pas** `max_ram` du modèle : les deux
valent 640 aujourd'hui, et les confondre ferait dériver le défaut avec la première machine
qui monte plus haut.

### Portes

| Épreuve | Résultat |
|---|---|
| `boot-diff roms 6000` (ibmpc) | **25 457 269** — à l'unité, inchangé |
| `boot-diff roms 6000 --config` (ibmxt) | **vert, 23 442 234 instructions** — mais voir ci-dessous : depuis § M12 cette campagne s'écrit `--model ibmxt` |
| `boot-diff roms 7000 --config --fda …b.img` (ibmxt) | **vert, 22 086 920 instructions** |
| `make -C tools/oracle selftest` | vert, 0 échec |
| `speaker-probe roms 6000` (ibmpc) | 9 champs identiques, empreinte `384EC07B1B64CC83` |
| `disc-probe roms 5600 --fda …b.img` (ibmpc) | 20 champs identiques |
| `check-oracle.sh` | 26 transcrits vérifiés, 0 dérive, arbre vendoré OK |
| `dotnet build -c Release` | 0 avertissement |

**Rectification apportée par § M12.** La deuxième ligne mesurait le XT **sans disquette**,
donc sa ROM BASIC, et non ce que son `--config` annonçait : `boot-diff` écrasait alors la
clé `disc_a` du fichier. La commande qui rend 23 442 234 aujourd'hui est
`boot-diff roms 6000 --model ibmxt` — même chiffre à l'unité, sous son vrai nom.
`boot-diff roms 6000 --config ixtal26-xt.cfg` amorce désormais vraiment DOS et rend
**19 511 811**.

**La porte du 5150 passe AVANT qu'on regarde le XT**, à chaque commit. Ajouter une entrée à
`models[]` ne doit rien changer au chemin par défaut ; si ce chiffre bougeait, tous ceux
déjà consignés parleraient d'une autre machine sans le dire.

### Le vert le plus fort du jalon, et pourquoi il n'est pas creux

Un diff vert sur deux machines qui planteraient de la même façon resterait vert. Contrôlé
à part :

```
--boot roms 6500 --model ibmxt --floppy-a …b.img --type "" --type "" --type DIR
  |The IBM Personal Computer DOS
  |Version 2.00 (C)Copyright IBM Corp 1981, 1982, 1983
  |A>DIR
  |COMMAND  COM    17664   3-08-83  12:00p
  …
```

PC DOS 2.00 amorce sur le XT et rend son catalogue. **Le chemin disquette n'est pas mêlé au
BIOS du 5150** — c'est ce que ce jalon existait pour établir, et rien ne l'avait montré
avant.

### Une assertion du plan corrigée par la mesure

Le jalon annonçait réveiller **deux** branches jamais exercées. Un compteur temporaire,
posé puis retiré, a tranché :

| Branche | ibmpc | ibmxt |
|---|---|---|
| Lecture 0x62, branche non-5150 (`keyboard_xt.cs:195-205`) | jamais | **atteinte** — réveillée |
| Lecture 0x60, branche non-5150 (`keyboard_xt.cs:162-166`) | **atteinte** | atteinte |

La seconde n'était pas morte : sa garde est `(romset == ROM_IBMPC || …) && (pb & 0x80)`, et
le 5150 y tombe dès que le bit 7 de `pb` est bas — c'est-à-dire à presque chaque lecture
clavier. Sans ce compteur, § M10 aurait annoncé deux branches réveillées et aurait eu tort
de moitié. Un jalon dont le bénéfice est d'exercer du code mort doit **mesurer** lequel.

### Ce que le XT fait autrement, et qui surprend

**Sa taille mémoire ne passe pas par SW2.** Sur le 5150, le port 0x62 rend
`(mem_size - 64) / 32` en deux demi-octets ; sur le XT cette lecture part dans la branche
`else`, qui ne porte que la configuration vidéo et le FPU. Le BIOS du 5160 détermine sa
mémoire **en balayant** — et rapporte bien 640 Ko en `0040:0013`. Corollaire pour les
mesures : le couplage `mem_size` ↔ durée d'amorçage de § M8 (12 s à 64 Ko, 52 s à 640)
**ne transfère pas** au XT.

Et la question ouverte du plan est levée : **le BIOS du XT démarre sans contrôleur de
disque dur.** `xt_init` n'en ajoute aucun, et rien ne l'attend.

### Ce que ce vert ne dit pas

1. **Deux machines, un seul cœur.** Le banc de vitesse, SST et le fuzzer restent sur le
   5150 : ils testent le 8088, qui est identique. À partir d'ici, **seul le diff d'amorçage
   rejoue les deux machines**.
2. **Le chemin d'écriture du XT n'est pas plus vérifié que celui du 5150.** `harness.h`
   n'expose toujours aucune injection clavier : l'oracle ne peut pas taper `FORMAT B:`, ni
   sur l'une ni sur l'autre. Deux machines, le même angle mort.
3. **La variante deux puces n'a pas été exercée.** `case ROM_IBMXT` a deux chemins —
   `xt.rom` (65 536 o) et la paire `5000027.u19` + `1501512.u18`. Seul le premier a tourné ;
   le second est transcrit et jamais atteint, `xt.rom` étant présent.
4. **`xt050986.0` / `.1` sont dans `roms/ibmxt/` et ne sont référencés par rien** —
   ni par PCem, ni ici.

## M11 — Le chemin d'écriture sous oracle : l'oracle apprend à taper

Le plus gros angle mort du dépôt, nommé comme tel depuis § M7.1 et réaffirmé à § M8.1 :
`FORMAT` et `WRITE DATA` ne s'atteignent qu'en **tapant une commande sous DOS**, et le
harnais ne savait pas taper. Les deux jalons précédents les avaient donc exercés côté C#
seul — « une preuve d'usage, pas une preuve de fidélité ».

### Ce qui manquait tenait en vingt lignes

| Fonction | Ce qu'elle fait |
|---|---|
| `h_rawinputkey` | écrit dans `rawinputkey[]`, le **même** tableau que la pompe SDL remplit côté hôte |
| `h_kbd_process` | `keyboard_poll_host()` (`wx-sdl2-keyboard.c:11-16`, quatre lignes) puis `keyboard_process()`, dans l'ordre de `pc.c:490-491` |
| `h_closepc` | pendant de `pc.closepc()` — les deux `disc_close` qui **vident** les tampons |

`h_runpc` ne les appelle **pas** : l'oracle n'a pas de couche hôte, et les y glisser
changerait toutes les mesures déjà consignées. C'est l'outil de diff qui déclenche, au
même point de la tranche des deux côtés.

`h_closepc` n'est pas un détail de propreté. `img_writeback` écrit dans le `FILE *` sans
`fflush`, et c'est le `fclose` de `img_close` qui pousse : sans lui, comparer deux images
après un FORMAT comparerait un fichier vidé à un fichier qui ne l'est pas — une divergence
entièrement fabriquée par le harnais.

### Une copie par côté, et c'est ce qui manquait pour que tout ait un sens

Les deux cœurs écrivent **pour de vrai** sur l'image montée. Leur donner le même fichier
ferait lire au second ce que le premier vient d'écrire. C'était d'ailleurs un risque
latent depuis M6, où `--fda` passait le même chemin aux deux : inoffensif tant que
l'amorçage n'écrivait pas, mais personne ne l'avait vérifié.

Chaque côté reçoit donc sa copie, et les deux sont **comparées octet par octet** à la fin.
C'est le vrai oracle du chemin d'écriture : le diff d'instructions dit que les deux cœurs
font la même chose, les images disent ce qu'ils ont **produit**.

Et une image identique à celle de départ est **signalée** : deux disquettes intactes se
ressemblent parfaitement et ne prouvent rien. Le contrôle s'est déclenché du premier coup
sur le lecteur A, qui n'est pas écrit — exactement le faux vert qu'il existe pour attraper.

### La porte

```
boot-diff roms 15000 --fda os/pcdos20/pcdos20b.img --fdb vierge-360k.img
          --type-at 5700 --type "" --type "" --type "FORMAT B:" --type ""
```

| Résultat | |
|---|---|
| Trace d'instructions | **vert, 48 783 446 instructions identiques** — la plus longue campagne du dépôt |
| Image A: | identique, et **signalée inchangée** |
| Image B: | identique des deux côtés, **367 370 octets écrits par l'invité** |
| Empreinte des deux images | `sha256 6a0be87ff88cf1b4…`, la même des deux côtés |

La disquette produite est une vraie disquette DOS :

| Offset | Octets | Sens |
|---|---|---|
| `0x000` | `eb 2c 90` + `IBM  2.0` | saut court et nom OEM |
| `0x013` | `d0 02` | 720 secteurs = 2 × 40 × 9 |
| `0x015` | `fd` | descripteur de média 360 Ko |
| `0x1FE` | `55 aa` | signature |
| `0x200` | `fd ff ff` | début de FAT |

**365 984 octets de `0xF6`** — le chiffre de § M7.1, à l'unité, et cette fois obtenu des
deux côtés.

**Deux chemins d'écriture sont comparés, pas un.** Le `0xF6` vient de FORMAT TRACK
(`STATE_FORMAT` → `img_writeback`). Mais `eb 2c 90` en `0x000` et `fd ff ff` en `0x200`
n'en sont pas : DOS les a écrits **après** le formatage, par WRITE DATA — `case 0x05` de
`fdc_write`, son bloc de résultat, `fdc_getdata`, `STATE_WRITE_FIND_SECTOR` et
`STATE_WRITE_SECTOR`.

### Non-régression

| Épreuve | Résultat |
|---|---|
| `boot-diff roms 6000` | **25 457 269** — à l'unité, avec `keyboard_process` désormais appelé des deux côtés à vide |
| `make -C tools/oracle selftest` | vert |
| `dotnet build -c Release` | 0 avertissement |

Le premier chiffre compte plus qu'il n'en a l'air : `keyboard_poll_host` et
`keyboard_process` tournent maintenant à chaque tranche des deux côtés. Sans touche
enfoncée, ils ne coûtent pas un cycle émulé — et c'est **mesuré**, pas supposé.

### Une reproductibilité récupérée au passage

`--boot --type` laissait 200 tranches à l'application après chaque Entrée. Assez pour un
`DIR`, pas pour un FORMAT : **mesuré à 200 tranches, `FORMAT B:` n'écrit que 18 432 des
368 640 octets** et l'écran reste sur « Formatting... ». D'où `--boot --settle N`, et à
4 500 :

```
|Formatting...Format complete
|   362496 bytes total disk space
|   362496 bytes available on disk
```

La recette de § M7.1 et § M8.1 redevient donc rejouable en une commande.

### Ce que ce vert ne dit pas

1. **Un seul scénario d'écriture.** FORMAT d'une disquette vierge en 360 Ko, sous PC DOS
   2.00, sur le 5150. Ni les trois autres tailles, ni le XT, ni une réécriture de fichier
   existante, ni `DISKCOPY`.
2. **La frappe n'est pas la frappe d'un humain.** Le calendrier est arithmétique — quatre
   tranches par état de touche — là où une personne tape à des intervalles quelconques. Ce
   qui est comparé est un scénario déterministe, ce qui est précisément ce qu'un oracle
   demande, et ce qui ne couvre pas les cadences pathologiques.
3. **`keyboard_process` est appelé par l'outil de diff, pas par `h_runpc`.** Les deux
   côtés sont symétriques, mais l'oracle reste une machine sans couche hôte : ce qui est
   prouvé, c'est que les deux réagissent pareil aux mêmes touches aux mêmes tranches.
4. **Le chemin d'écriture hors DMA reste mort** (`PB-19`, `fdc.written` jamais posé à 1) :
   aucun des deux côtés ne l'exerce, et cet accord-là reste un accord vide.

### M11.1 — Le titre mentait

M10 a livré deux machines ; rien ne disait laquelle tournait. `SdlHost.cs` portait

```csharp
private const string WindowTitle = "iXtal26 - IBM PC 5150";
```

**en dur**, et la ligne de `--verbose` annonçait `mem_size` et les lecteurs sans nommer la
machine. Un XT qui tournait affichait donc « IBM PC 5150 » — au seul endroit où l'on
regarde. C'est le même défaut que le chemin pixel de § M4.5 et que le chemin audio de
§ M9 : pas une panne, une absence de voix.

Le titre vient désormais de `models[model].name`, verbatim de PCem. Une **méthode** et non
un champ : un initialiseur s'évaluerait avant `pc.initpc()`, donc avant qu'un modèle soit
choisi.

### Ce qui discrimine vraiment, et c'est mesuré

Un titre qui change prouve qu'une chaîne a changé, pas qu'une machine a changé. Le compte
d'images d'un amorçage de 6 000 tranches, lui, sépare les trois cas :

| Commande | Écran atteint | `frames` |
|---|---|---|
| `--boot roms 6000` | ROM BASIC du 5150 | **1 915** |
| `--boot roms 6000 --model ibmxt` | ROM BASIC du 5160 | **3 366** |
| `--boot roms 6000 --config ixtal26-xt.cfg` | invite de date de PC DOS 2.00 | **3 346** |

Autre repère du même ordre, relevé au passage : le **bip de fin de POST tombe à la
tranche 2 923 sur le XT**, contre 4 989 sur le 5150 (§ M9). Le POST du 5160 est plus
court, et c'est encore le turbo qui le dit — il rend la main au temps réel deux secondes
émulées plus tôt.

Non-régression : `boot-diff roms 6000` → **25 457 269**, à l'unité. Le titre, le
`--verbose` et les messages d'échec sont de l'hôte ; ils ne doivent rien changer au cœur.

### Ce que ce vert ne dit pas

**La chaîne de titre n'a pas été relue depuis le compositeur.** SDL3 tourne ici en Wayland
natif, et `xdotool` — qui est X11 — ne voit aucune fenêtre. Ce qui est vérifié, c'est
l'expression qui la produit : la même que celle de la ligne `--verbose`, qui imprime bien
`machine : [8088] IBM XT` sous les deux formes de sélection.

## M12 — Le disque dur du XT : FDISK, FORMAT C: /S, et trois trous dans le harnais

Le 5160 est le premier IBM PC livré avec un disque fixe dans sa configuration standard :
c'est ce que veut dire le « XT ». M10 émulait la machine sans sa raison d'être.

Le contrôleur n'est pas sur la carte mère. C'est l'**IBM Fixed Disk Adapter**, de
conception Xebec, une carte avec sa propre ROM d'extension en 0xC8000 qui apporte l'INT 13h
du disque fixe — ce qui explique le constat de § M10, « le BIOS du XT démarre sans
contrôleur de disque dur ». Elle prend l'IRQ 5 et le canal DMA 3, que personne d'autre
n'occupait dans ce dépôt.

### Ce qui a été transcrit, et la parité

| Fichier C# | Oracle | Lignes vives C# / C | |
|---|---|---|---|
| `Mfm/mfm_xebec.cs` | `src/mfm/mfm_xebec.c` | **566 / 572** | ratio 0,99 — le fichier porte **deux** cartes, `mfm_xebec_device` et `dtc_5150x_device`, qui partagent `xebec_close`, `xebec_read`, `xebec_write` et `xebec_callback`. Le DTC vient gratuitement ; seul le Xebec est câblé |
| `Disc/hdd_file.cs` | `src/hdd/hdd_file.c` | **110 / 196** | branche `HDD_IMG_RAW` seule. Les 86 lignes d'écart sont exactement les branches VHD et ramdisk : 63 pour les trois blocs `:12-24`, `:42-64` et `:96-147`, le reste dans les quatre fonctions d'E/S |
| `Disc/hdd.cs` | `src/hdd/hdd.c` | **8 / 129** | les deux globales seules, `hdc[7]` et `hdd_controller_name[16]`. Le reste est le registre `HDD_CONTROLLER` et ses huit accesseurs, omis — même arbitrage que `SOUND_CARD` (§ M9) et `VIDEO_CARD` |

**`hdd.c` n'était pas entièrement écartable**, et c'est le piège que la reconnaissance a
trouvé : `ibm.h:366-372` ne porte que l'`extern` de `hdc[7]`, la définition est dans
`hdd.c`. Un registre de cartes qu'on écarte peut abriter les seules définitions de
l'arbre ; il faut lire avant d'écarter.

**`ide_fn[7][512]` est défini dans `ide/ide.c`**, le fichier qu'on ne veut surtout pas
lier — 2 032 lignes de contrôleur IDE pour une machine qui n'en a pas. PCem lui-même ne
l'inclut jamais pour ça : ses six consommateurs re-déclarent l'`extern` localement, y
compris `mfm_xebec.c:26`. Il vit donc dans `Disc/hdd.cs`, à côté de la géométrie qu'il
complète, et il **doit** porter la même valeur des deux côtés du diff : sans quoi
`xebec_set_switches` calcule deux `switches` différents et le diff rougit au premier
`in 0x322`.

**`hdd_file.c` traînait 3 996 lignes de C tiers** — `minivhd/` (3 723) et `ramdisk/`
(273), dont 1 797 (`cwalk.c`, `libxml2_encoding.c`) ne servent qu'aux chemins parents
UTF-16 des VHD différentiels. Les deux prédicats d'aiguillage, `mvhd_file_is_vhd` (par
contenu) et `is_ramdisk_file` (par extension), sont rendus **faux** : deux stubs contre
3 996 lignes. Côté oracle, dix-sept stubs d'édition de liens dans `harness_stubs.c`, que
`-Wl,--no-undefined` a énumérés exactement comme il l'avait fait pour les 90 symboles de
`video.c`.

### La géométrie n'est pas libre

`xebec_set_switches` (`mfm_xebec.c:716-747`) n'accepte que **17 secteurs par piste** et
quatre couples (cylindres, têtes) : (306,4), (612,4), (615,4), (306,8). Hors de là,
`warning()` et les interrupteurs restent à zéro — le disque est annoncé en type 0 quelle
que soit sa taille, et le POST diverge. Rien ne le refuse : c'est un avertissement.

Le disque du jalon est donc **306 × 4 × 17 = 20 808 secteurs = 10 653 696 octets**, le
10 Mo historique du XT.

### Trois trous dans le harnais, tous trouvés en cherchant pourquoi rien ne s'écrivait

Le symptôme était : « Image C: identique des deux côtés — mais INCHANGÉE depuis le
départ ». Le garde-fou de § M11 faisait son travail, campagne après campagne, sans qu'on
sache pourquoi. Un vidage de l'écran texte a donné la réponse en une ligne : **« Missing
operating system »**. La machine amorçait sur le disque dur, pas sur la disquette.

**1. `boot-diff` écrasait la disquette du fichier de configuration.** `BootDiff.Run` faisait
`discfns[0] = csharpA ?? ""` sans condition : `--fda` absent, la clé `disc_a` que
`loadconfig` venait de poser était remplacée par une chaîne vide. Les deux côtés étant
amputés pareil, **le diff restait vert** — il comparait deux machines également sourdes.
Introduit à M11 avec les copies par côté, invisible jusqu'ici parce que toutes les
campagnes disquette passaient `--fda` explicitement.

**2. La porte des images ne pouvait pas échouer.** Le verdict était

```csharp
return CompareImages(…, "A:") & CompareImages(…, "B:") & CompareImages(…, "C:");
```

`CompareImages` rend 1 en cas de divergence et 0 sinon, **y compris quand rien n'est
monté**. Un `&` entre trois codes ne vaut donc 1 que si toutes les images divergent **en
même temps**, et un lecteur vide suffisait à masquer les autres. Le texte imprimé, lui, a
toujours été juste : la porte des images de M11 était lue **à l'œil**, jamais par le code
de sortie. C'est `|` qui la rend exécutable — bitwise et non `||`, pour que les quatre
lignes s'impriment même après la première divergence.

Vérifié par **contrôle négatif** : un octet retourné dans l'image du côté C# juste avant la
comparaison donne `Image C: : 1 octet(s) divergent(s), le premier en 0x1BE — oracle 00,
C# FF` et **code de sortie 1** ; la même campagne sans sabotage sort 0. Le crochet a été
retiré.

**3. `ide_fn[1]` partait tel quel à l'oracle** pendant que le C# lisait le même fichier :
le défaut même qu'on venait de fermer pour A:, B: et C:, en attente d'un second disque.
Inerte tant que D: est vide — c'est l'origine du « Cannot open file '' » bénin — refermé
au passage, avec une quatrième ligne de comparaison.

### Une porte perdue, et récupérée

Le correctif (1) déplace un chiffre de régression, et il faut le dire : **`boot-diff
roms 6000 --config ixtal26-xt.cfg` mesurait en fait le XT SANS disquette**, donc sa ROM
BASIC. Le correctif rend la clé au fichier, et cette campagne-là n'avait alors plus aucune
commande pour l'exprimer — `boot-diff` n'avait pas de `--model`.

Il en a un maintenant, et la preuve est arithmétique : `boot-diff roms 6000 --model ibmxt`
rend **23 442 234**, l'ancien chiffre à l'unité. La porte n'a pas bougé, elle a changé de
nom.

### Les deux chemins neufs, comptés

IRQ 5 et canal DMA 3 n'avaient jamais tourné. Compteur temporaire, posé puis retiré, sur
l'arc complet depuis un disque vierge :

| Étape | IRQ 5 | DMA canal 3 |
|---|---|---|
| Amorçage seul, carte montée | 7 | 512 o lus, 512 o écrits |
| + `FDISK`, création de la partition | 9 | 1 024 o lus, 1 024 o écrits |
| + redémarrage et `FORMAT C: /S` | **1 303** | **72 192 o lus, 42 496 o écrits** |

Les 41 472 octets que DOS annonce à l'écran — « 41472 bytes used by system » — sont
exactement ce que le canal DMA a écrit entre les deux dernières lignes. Le compte du
chemin d'écriture et le compte de l'invité tombent juste l'un sur l'autre.

**Et les deux chemins d'écriture ne se confondent pas.** `FORMAT TRACK` passe par
`hdd_format_sectors`, qui n'emprunte **pas** le DMA ; `WRITE DATA` passe par
`hdd_write_sectors` et le canal 3. Le formatage écrit des zéros sur des zéros et ne
change donc pas un octet de l'image : ce qui reste visible dans la comparaison, ce sont
les 33 744 octets du système de fichiers.

### Les portes

| Épreuve | Résultat |
|---|---|
| `boot-diff roms 6000` | **25 457 269** — 5150, ROM BASIC, inchangé |
| `boot-diff roms 7000 --fda …pcdos20b.img` | **26 750 702** — 5150, DOS, inchangé |
| `boot-diff roms 6000 --model ibmxt` | **23 442 234** — XT, ROM BASIC (ex-porte 3, renommée) |
| `boot-diff roms 6000 --config ixtal26-xt.cfg` | **19 511 811** — XT, DOS : chiffre NEUF, le correctif (1) l'a corrigé |
| `boot-diff roms 7000 --config ixtal26-xt.cfg --fda …` | **22 086 920** — XT, DOS, inchangé |
| Campagne M11, `FORMAT B:` sur disquette | **48 783 446** instructions, **367 370 octets** — à l'unité, et désormais sous une porte exécutable |
| Arc `FDISK` + `FORMAT C: /S`, disque à zéro | **98 945 755** instructions, **33 982 octets** |
| Amorçage depuis C:, sans disquette | **25 941 449** instructions |
| `make -C tools/oracle selftest` | vert, 0 échec |
| `speaker-probe roms 6000` · `disc-probe roms 6000 --fda …` | 9 et 20 champs identiques |
| `check-oracle.sh` · `dotnet build -c Release` | 0 dérive, arbre vendoré OK · 0 avertissement |

### La campagne du disque dur, et c'est elle la porte du jalon

L'arc entier, depuis une image de 10 653 696 octets à **zéro**, en une seule campagne :

```
--create-hdd 1 /tmp/xt-arc-hdd.img          # depuis § M12.1 ; avant, un
                                            # « head -c 10653696 /dev/zero », qui
                                            # donne le meme fichier a l'octet
# /tmp/xt-arc.cfg = ixtal26-xt.cfg avec les quatre cles disque decommentees
#                   et hdc_fn = /tmp/xt-arc-hdd.img
boot-diff roms 6000 --config /tmp/xt-arc.cfg --type-at 6000 --type-settle 3000
          --type "" --type "" --type FDISK --type 1 --type "" --type ""
          --type "" --type "" --type "FORMAT C: /S" --type ""
```

Les six premières lignes répondent aux invites de date et d'heure, ouvrent `FDISK`,
choisissent « Create DOS Partition », acceptent le disque entier et laissent la machine
**redémarrer d'elle-même** ; les quatre dernières répondent aux invites du nouvel
amorçage et formatent.

| Résultat | |
|---|---|
| Trace d'instructions | **vert, 98 945 755 instructions identiques** — deux fois la campagne de M11, la plus longue du dépôt |
| Image C: | identique des deux côtés, **33 982 octets écrits par l'invité** |
| Empreinte des deux images | `sha256 3aa1e5ff75e31ec5…`, la même des deux côtés |
| Écran | « Format complete / System transferred / 10 550 784 bytes available on disk » |

**33 982 = 33 744 + 238**, et les deux termes ont été mesurés séparément : 33 744 pour un
`FORMAT C: /S` seul sur une image déjà partitionnée, 238 pour un `FDISK` seul sur une
image vierge. Les deux commandes n'écrivent pas au même endroit et leurs comptes
s'additionnent exactement.

Le disque produit est un vrai disque fixe DOS :

| Offset | Octets | Sens |
|---|---|---|
| `0x1EE` | `80 00 02 00 01 03 51 30 01 00 00 00 03 51 00 00` | entrée de partition : amorçable, type 01 (FAT12), LBA 1, 20 739 secteurs |
| `0x1FE` | `55 aa` | signature du MBR |
| `0x200` | `eb 2c 90` + `IBM  2.0` | secteur d'amorçage de la partition |
| `0x215` | `f8` | descripteur de média **disque fixe** — `fd` était celui de la 360 Ko de § M11 |

L'entrée est dans le **quatrième** emplacement de la table, pas le premier : c'est ce que
fait le FDISK de PC DOS 2.00, et les deux côtés le font pareil.

### Et la machine amorce sur C:, sous oracle aussi

Le disque produit par la campagne précédente, remonté seul — **aucune disquette**, la clé
`disc_a` retirée de la configuration (`/tmp/xt-bootc.cfg`, pointant une copie de l'image
formatée) :

```
boot-diff roms 6000 --config /tmp/xt-bootc.cfg --type-at 6000 --type-settle 800
          --type "" --type "" --type DIR
```

**Vert, 25 941 449 instructions identiques**, écran à `C>` et `DIR` qui liste
`COMMAND COM 17664` et `10543104 bytes free`. C'est le troisième terme de la portée du
jalon, et il exerce ce que les deux autres n'exercent pas : le **chemin de lecture** du
contrôleur pris par l'INT 19h du BIOS, puis la relecture du système de fichiers qu'une
autre campagne a écrit. L'image y est forcément inchangée — un `DIR` n'écrit rien — et
l'outil le signale, comme il doit.

### Une image montée en lecture-écriture n'est vierge qu'une fois

Constat fait en rejouant la campagne de M11 pour vérifier la porte redevenue exécutable :
elle a rendu **16 306 octets écrits** au lieu des 367 370 consignés. Ni le cœur ni le
harnais n'avaient bougé — la trace était à **48 783 446**, à l'unité. C'est
`os/vierge-360k.img` qui n'était plus vierge : une campagne antérieure y avait écrit un
système de fichiers complet, 349 713 octets de `0xF6` compris. `CompareImages` compte les
octets qui ont changé **depuis le départ**, et le départ avait dérivé.

Rejouée depuis une image réellement à zéro : **367 370 octets**, le chiffre de M11 à
l'unité.

Le garde-fou « INCHANGÉE depuis le départ » n'attrape que le cas total. Un départ
**partiellement** pré-écrit passe au travers et donne un compte plus petit sans rien
signaler. Toute mesure de chemin d'écriture doit donc partir d'une image fraîche, et `os/`
n'est pas versionné : la recette doit le dire, pas le supposer.

### Neuf défauts de PCem, `PB-22` à `PB-30`

C'est le plus fort rendement au millier de lignes du dépôt : 707 lignes de C pour neuf
entrées, contre 1 834 pour sept à M6. Une carte que peu de logiciels exercent est moins
relue qu'un cœur d'UC.

| | |
|---|---|
| `PB-22` | `CMD_FORMAT_TRACK` ne réinitialise pas `sector` là où les trois autres commandes d'accès le font |
| `PB-23` | `completion_byte = drive_sel & 0x20` sur une valeur qui vaut 0 ou 1 : toujours nul |
| `PB-24` | `rom_init` ignore le retour de `fread` : **12 288 octets de tas** publiés à l'invité. **Non reproduit**, divergence assumée |
| `PB-25` | borne des têtes testée avec `>` au lieu de `>=`, cinq lignes au-dessus d'un `>=` correct |
| `PB-26` | quatre chaînes de `fatal()` fausses par copier-coller |
| `PB-27` | trois `switch` internes sans `default:` là où six autres appellent `fatal()` |
| `PB-28` | `CMD_DTC_GET_DRIVE_PARAMS` répond 256 têtes sur une unité absente |
| `PB-29` | `ide_fn` déclaré `[4][512]` dans `scsi_ibm.c`, défini `[7][512]` dans `ide.c` |
| `PB-30` | trois symboles morts : `cfg_spt`, `STATE_DUNNO`, `STAT_DRQ` |

`PB-22` mérite une note, parce que c'est le seul que la campagne aurait pu faire rougir et
qui n'a rien fait : PC DOS 2.00 fait précéder chaque `FORMAT TRACK` d'un accès qui laisse
`sector` à 0. L'image est donc identique des deux côtés **parce que les deux reproduisent
le défaut**, pas parce qu'il est inoffensif. Un pilote qui enchaînerait deux formatages
formaterait la mauvaise piste.

`PB-24` est la seule divergence assumée du jalon, et pour la raison de `h_pad_ram` : de
l'UB dont la valeur change d'une exécution à l'autre, et un oracle qui tire aux dés n'est
pas un oracle. Atténuée en pratique — l'en-tête des deux ROMs déclare sa vraie longueur
(`55 aa 08` pour le Xebec, `55 aa 10` pour le DTC) et borne le balayage du POST.

### Ce que ce vert ne dit pas

1. **Un seul contrôleur, une seule géométrie.** Le Xebec d'IBM en (306, 4, 17). Le DTC
   5150X est transcrit ligne à ligne mais **n'a jamais été monté** : aucune des trois
   commandes qui lui sont propres n'a tourné, ni sous oracle ni ailleurs. Les trois autres
   géométries admises non plus.
2. **Un seul système, une seule version.** PC DOS 2.00. Ni DOS 3, ni un pilote qui
   parlerait directement au contrôleur.
3. **Le DMA en défaut n'est pas exercé.** Les boucles `DMA_NODATA` de `mfm_xebec.c`
   suspendent en conservant `data_pos` et laissent un chronomètre de 2 000 µs reprendre.
   Rien dans cette campagne ne les a fait suspendre : le canal 3 a toujours répondu.
4. **Dix-neuf `fatal()` vivants n'ont pas été atteints.** C'est une bonne nouvelle et pas
   une preuve : un `in`/`out` sur 0x320 dans un état inattendu tue l'émulateur des deux
   côtés, et seul un logiciel qui s'y risque le montrerait.
5. **`PB-23`, `PB-25`, `PB-27` et `PB-28` n'ont aucun pendant exécuté.** Ils sont
   transcrits et marqués ; la campagne ne passe pas dessus. Ce qui est vérifié, c'est que
   les deux côtés font la même chose sur le chemin que DOS emprunte.
6. **L'arc s'arrête au formatage.** Il n'écrit ni ne réécrit de fichier après coup — pas
   de `COPY` vers C:, pas de réécriture d'un fichier existant — et ne teste pas un second
   disque en D:. La relecture après remontage, elle, l'est : c'est la campagne
   d'amorçage sur C: ci-dessus.


## M12.1 — Fabriquer le disque, avec la table de types du BIOS

§ M12 a livré le Fixed Disk Adapter. Mais **fabriquer le disque restait manuel et
piégeux** : les recettes de § M12 passaient par un `head -c 10653696 /dev/zero`, taille
calculée à la main, et `ixtal26-xt.cfg` portait un tableau de géométries à recopier sans se
tromper. Trois pièges, tous silencieux :

1. **La géométrie n'est pas libre.** `xebec_set_switches` exige 17 secteurs par piste et
   l'un de quatre couples (cylindres, têtes). Hors de là, la carte se contente d'un
   `warning()`, laisse ses interrupteurs à zéro — le disque est alors annoncé en **type 0**,
   pas absent — et le POST diverge. **Rien ne le refuse.**
2. **17 est câblé dans l'ADRESSAGE**, pas seulement dans la validation :
   `xebec_get_sector` teste `sector >= 17` et calcule
   `addr = ((cylinder × heads) + head) × 17 + sector`. La clé `hdc_sectors` n'a donc
   qu'une valeur juste, et rien ne le dit à l'exécution.
3. **Une image créée implicitement fait zéro octet.** `hdd_load_ext` la crée vide et ne la
   pré-alloue jamais ; `hdd.sectors` vient de la configuration, pas du fichier. Or
   `hdd_read_sectors` ignore la fin de fichier — pendant du `fread` dont PCem ignore le
   retour. Lire un secteur jamais écrit rend **le contenu résiduel de `xebec.sector_buf`**,
   pas des zéros. Les deux côtés du diff font la même chose : ce n'est pas un défaut de
   fidélité, c'est un défaut de **reproductibilité**, et il rend le garde-fou « INCHANGÉE
   depuis le départ » de `BootDiff` aveugle à son propre point de départ.

### L'oracle n'était pas là où on le cherchait

`wx-createdisc.cc` — l'oracle de « Creer une disquette vierge... » depuis § M7.1 — ne fait
que des **disquettes**, neuf tailles de 160 Ko à 100 Mo, et n'a aucune notion de géométrie.
Le disque dur de PCem passe ailleurs : par le bouton « New… » de la page *Hard disc* de la
boîte de configuration, donc par `hdnew_dlgproc` (`wx-config.c:1550-1839`). Ce jalon est
donc un **portage**, du même statut que `Host/SdlMenu.cs`, pas une invention — et il a
fallu chercher pour le savoir. Ce qui est transcrit :

| | Citation |
|---|---|
| `create_drive_raw()` — la création | `wx-config.c:1427-1440` |
| `hd_types[46]` — la table de types du BIOS | `:1295-1302` |
| Libellé d'un type et calcul de taille | `:1580-1586` |
| Les quatre validations, messages **verbatim** | `:1607-1633` |
| Le message de fin | `:1682-1683` |
| Géométrie → numéro de type | `:1722-1731` |

`create_drive_raw` est **structurellement identique** à `wx-createdisc.cc:62-73`, donc à
`SdlMenu.CreateBlank` : un tampon de 512 octets nuls, écrit `cyl × hpc × spt` fois. Et rien
n'est posé dans le secteur 0 — ni MBR, ni table de partition, ni signature `55AA`. PCem le
dit lui-même, c'est son message de fin : *« Drive created, remember to partition and format
the new drive. »*

Omis de `hdnew_dlgproc`, et inscrit au registre : les trois formats VHD (`:1645-1668`,
couverts par l'omission `minivhd/` de § M12), la saisie par taille en Mo (`:1736-1754`) qui
force 63/16, et `check_hd_type` (`:1340-1382`) qui déduit un type de la **taille** d'un
fichier existant — il sert le sélecteur de fichiers du dialogue, que ce dépôt n'a pas.

### Ce que la table révèle, et qui interdit d'écrire le marquage à la main

| Fait | |
|---|---|
| 46 entrées `(cylindres, têtes)`, 17 secteurs **implicites** | comparées une à une au C : **identiques** |
| L'entrée 14 vaut `(0, 0)` | c'est le **type 15, réservé** dans la table de l'IBM AT. PCem le laisse dans sa liste déroulante, où il affiche « size=0MB » |
| **SIX** des 46 types sont compatibles Xebec, pas quatre | la table a des doublons : `(306,4)` est aux types **01 et 23**, `(615,4)` aux types **02 et 06** |

Le marquage se **calcule** donc contre `xebec_hd_types`, passée `internal` pour l'occasion.
Une liste de quatre écrite à la main serait fausse dès la première relecture ; et recopier
les quatre couples les ferait dériver du fichier qui les fait respecter, après quoi la
carte refuserait **en silence** une géométrie que l'utilitaire aurait proposée.

Mesuré : les six types marqués sont bien 01, 02, 06, 13, 16, 23.

### Quatre déviations, toutes marquées sur place

1. **`CreateNew` là où PCem ouvre en `"wb"`** et écrase donc en silence. Il peut se le
   permettre : son chemin vient d'un sélecteur de fichiers dont le système demande
   confirmation. Ici il vient d'un argument ou d'un nom calculé, et rien ne redemanderait.
2. **Un cinquième refus, que PCem n'a pas.** Ses trois bornes sont des **plafonds** ; le
   plancher n'existe pas, et le type 15 de sa propre table le traverse sans un mot pour
   produire un fichier de zéro octet — exactement ce que ce jalon existe pour éviter.
3. **Un libellé court au menu.** Celui de PCem fait jusqu'à 46 caractères ; la boîte en a
   46 **en tout**, marque et marge comprises, et rien ne coupe une ligne trop longue. Mesuré
   après compression : libellé le plus long **39** caractères, ligne statique la plus longue
   **45**, écran de **20** lignes. `--create-hdd`, lui, imprime le libellé verbatim.
4. **L'image n'est pas montée**, là où `CreateBlank` insère la disquette dans A:. La
   disquette peut l'être parce que DOS attend qu'on l'insère pendant qu'il tourne ; un
   disque dur, non — la carte lit `ide_fn[]` au `device_add` de `resetpchard`, et sa
   géométrie vient du fichier de configuration, qui reste maître. PCem ne le monte pas non
   plus.

Un cinquième écart n'en est pas un : le calcul de taille est en 64 bits, ce qui est le
pendant des **deux** calculs du C — `int` pour les 46 types, où il ne peut pas déborder, et
`uint64_t` pour la saisie libre, où il déborderait.

### Le bloc de clés imprimé dépendait du répertoire de lancement

Défaut trouvé en relisant la promesse faite à l'utilisateur — « les quatre clés, prêtes à
coller » — depuis un second répertoire. `ImagesRoot` rend `os` **relatif** quand le
répertoire courant est la racine du dépôt, et un chemin **absolu** quand on lance depuis
`bin/Release/net10.0`, ce que fait Rider et ce pour quoi son repli existe. La clé imprimée
suivait, et donnait alors une configuration qui marche sur cette machine et sur aucune
autre.

`HddImage.ConfigPath` rend donc `os/NOM` dès que l'image est dans `os/`, et le chemin tel
quel sinon — un chemin explicitement demandé reste celui qu'on a demandé. La forme relative
est celle que `resolve_file_path` retrouve de partout : il essaie le répertoire courant,
puis remonte depuis le binaire.

**La leçon est de méthode** : une commande mesurée depuis un seul répertoire n'est pas
mesurée. C'est la même famille que le titre de fenêtre de § M11.1 et que la clé `disc_a`
écrasée de § M12 — une sortie juste là où on l'a regardée.

Durci au passage : les deux positionnels testaient `StartsWith("--")` là où `--turbo`
(`Program.cs:266`) teste `StartsWith('-')`. `--create-hdd 1 -v` créait donc un fichier
nommé « -v ».

### Les portes

| Épreuve | Résultat |
|---|---|
| Table comparée au C, entrée par entrée | **46 / 46 identiques** |
| Types marqués compatibles Xebec | **01, 02, 06, 13, 16, 23** — six, et calculés |
| Type 01 : taille | **10 653 696 octets** = 306 × 4 × 17 × 512, à l'unité |
| Type 01 : contenu | **tous les octets nuls** |
| Type 01 : allocation | **non sparse** — `du` et `du --apparent-size` égaux, 10 653 696 des deux côtés, mesuré dans `os/` donc **sur ext4** et non sur le tmpfs de `/tmp`, où ces deux nombres s'accordent pour des raisons qui ne se généralisent pas |
| Type 01 contre `head -c 10653696 /dev/zero` | **`cmp` silencieux** : le même fichier à l'octet |
| Clé `hdc_fn` imprimée | **`os/vierge-hdd-type01.img` depuis les DEUX répertoires** — la racine du dépôt et `bin/Release/net10.0`, celui d'où Rider lance |
| Neuf refus | type 15, type 0, type 47, `abc`, `306,4` incomplet, 64 secteurs, 17 têtes, 265265 cylindres, 0 cylindre → **code 2** ; fichier existant → **code 1** |
| `boot-diff roms 6000` | **25 457 269** à chacun des trois commits |
| `check-oracle.sh` | 30 transcrits vérifiés, 0 dérive, arbre vendoré OK |
| `dotnet build -c Release` | 0 avertissement |

**Et la porte de fond : l'image sert vraiment.** L'arc complet de § M12 rejoué à
l'identique, mais depuis une image fabriquée par `--create-hdd 1` au lieu du `head -c` :

| | |
|---|---|
| Trace d'instructions | **vert, 98 945 755 instructions identiques** |
| Image C: | identique des deux côtés, **33 982 octets écrits par l'invité** |

**Les deux chiffres de § M12, à l'unité.** C'était le seul verdict capable de dire que la
fabrique produit le même point de départ que la recette qu'elle remplace — un chiffre
différent aurait signifié le contraire, sans qu'aucune autre épreuve ne le voie.

### Une question du plan, tranchée par la mesure

Le plan notait « à vérifier, pas à supposer » que PC DOS 2.00 sache formater les types
au-delà de ~16 Mo : FAT12 plafonne à 4 085 clusters, et 41 616 secteurs n'y tiennent qu'avec
16 secteurs par cluster. Mesuré sur le **type 16** (612 × 4 × 17 = 21 307 392 octets),
`FDISK` puis `FORMAT C: /S` :

```
|Formatting...Format complete
|System transferred
| 21225472 bytes total disk space
|    41472 bytes used by system
| 21184000 bytes available on disk
```

DOS 2.00 s'en sort seul. L'inquiétude était sans objet, et c'est mesuré et non déduit.

### Ce que ce vert ne dit pas

1. **« Le menu et la ligne de commande produisent le même octet » n'est pas MESURÉ.** Le
   piloter demanderait une fenêtre focalisée, et SDL3 tourne ici en Wayland natif — même
   limite qu'au titre de fenêtre de § M11.1. Ce qui est vérifié est structurel, par
   relecture et par `grep` : les deux appelants lisent la géométrie dans
   `HddImage.hd_types[type-1]` et `HddImage.TypeSectorsPerTrack`, appellent le même
   `HddImage.Create`, et **aucun ne porte de géométrie en dur**. Pour un type donné, ils
   passent donc des arguments identiques à la même fabrique — mais c'est un argument de
   code, pas une empreinte comparée.
2. **L'entrée de menu n'a jamais été affichée.** Ses largeurs sont calculées et vérifiées
   contre `Cols`, pas vues à l'écran. Même angle mort que le titre de fenêtre.
3. **Deux types sur quarante-six ont été créés**, le 01 et le 16, plus une géométrie libre.
   Les quarante-trois autres ne sont validés que par le calcul de leur taille.
4. **Aucun contrôleur ne sait adresser autre chose que 17 secteurs par piste.** La saisie
   libre accepte jusqu'à 63 comme chez PCem, et la commande dit alors que la carte refusera
   — mais ce chemin n'a pas d'émulation derrière lui dans ce dépôt, et n'en aura qu'avec un
   contrôleur IDE.
5. **`hdd_controller = mfm_xebec` est écrit en dur dans le bloc de clés imprimé.** La carte
   `dtc5150x` est transcrite et accepterait toute géométrie ; le bloc ne la propose pas.


## M13 — Construire la machine avant de la lancer

Parti d'un symptôme : « j'ai un FDISK mais une fois la machine relancée, elle ne voit pas
l'image ». La cause immédiate n'était pas un défaut — après `FDISK` le disque est
partitionné mais sans système de fichiers, et il reste `FORMAT C: /S`. Mais la question
posée, *« comment la monter avant le lancement »*, a découvert trois manques et deux
défauts, dont un de perte de données.

### La perte d'écritures était totale, pas latente

`pc.closepc()` ne fermait que les deux disquettes. Son commentaire d'omission disait
« `device_close_all()` n'a rien à fermer que le processus ne rende de lui-même » : vrai
quand la phrase a été écrite, **rendu faux par § M12** sans que personne n'y revienne,
puisque le Fixed Disk Adapter tient depuis un `FileStream` tamponné.

Un `FDISK` écrit **un** secteur de 512 octets. Il tient entièrement dans le tampon de
4 Ko et n'en sort jamais.

| | Octets non nuls dans l'image, après sortie du processus |
|---|---|
| Avant correctif | **0** — alors que l'écran affichait la partition écrite |
| Après | **238** — entrée de partition en `0x1EE`, signature `55 aa` |

Les campagnes de § M12 y échappaient **par le volume** — un `FORMAT` pousse 10 Mo, donc
tout sauf le dernier tampon partiel — et non par construction. `BootTest.Run` ne fermait
pas non plus, alors que le README donne `--settle 4500 --type "FORMAT B:"` comme recette.

**Leçon générale** : un commentaire qui justifie une omission par l'état du dépôt périme
quand le dépôt change, et rien ne le signale.

### `PB-31`, trouvé en corrigeant l'oracle symétriquement

Et il fallait le corriger symétriquement : ne vider qu'un côté ferait diverger les images
sur le tampon non vidé de l'autre — la divergence fabriquée par le harnais que § M11
décrit. Le pendant fidèle a été écrit, puis **retiré après plantage** :

```
#0 mem_mapping_remove  mem.c:1170   dest = dest->next;
#1 rom_deinit          rom.c:113
#2 xebec_close         mfm_xebec.c:769
#3 device_close_all    device.c:40
```

`cga_close` fait `free(cga)` **sans** `mem_mapping_remove`, alors que `cga_t` porte son
`mem_mapping_t` par valeur et que `cga_standalone_init` l'a chaîné. Le maillon reste dans
la liste en pointeur pendant, et `mem_mapping_remove` le traverse avec une boucle
`while (dest != mapping)` **sans garde de fin de liste**. Atteignable chez PCem :
`closepc()` est bien appelé (`wx-sdl2.c:649`), et `video_init` précède
`hdd_controller_init`.

Non reproduit, et la divergence ne se choisit pas : le C# n'a pas de `free`. Le côté C#
appelle donc `device_close_all()` fidèlement ; l'oracle, qui est une **bibliothèque** et
non un processus qui s'arrête, vide par `fflush(NULL)`.

### Deux `boot-diff` concurrents fabriquent un rouge

Le chemin de la trace oracle était fixe, celui des copies d'images aussi. Une boucle de
régression lancée pendant une campagne à disque dur a rendu :

```
roms 6000                     PREMIÈRE DIVERGENCE à l'instruction 21
roms 7000 --fda …b.img        ÉCART DE LONGUEUR : oracle 35 622 286, C# 26 750 702
```

**35 622 286 est le compte de l'autre campagne.** Rejouées en série, les deux portes
rendent leur chiffre exact. Le numéro de processus entre dans les deux noms, et la trace
— 8 octets par instruction, 792 Mo pour l'arc de § M12 — est supprimée dès qu'elle est en
mémoire.

C'est la même famille que le partage d'image fermé à § M11 entre les deux **côtés** d'un
diff ; ici c'était entre deux **exécutions**.

### `--hdd`, et ce que la mesure a corrigé dans le plan

Il n'y avait aucun chemin pour monter un disque dur sans écrire un fichier de
configuration. `--hdd IMG` le fait, géométrie déduite de la taille par la branche MFM de
`check_hd_type` — que § M12.1 avait omise en écrivant « il sert le sélecteur de fichiers
du dialogue, que ce dépôt n'a pas ». L'omission tombe avec la phrase.

Le plan affirmait que l'aller-retour taille → type serait l'identité sur les 46 types.
**Faux, et mesuré** : `check_hd_type` compare des **tailles**, pas des géométries. Sept
tailles en désignent plusieurs, quatre recouvrent des géométries différentes :

| Taille | Candidats |
|---|---|
| 10 653 696 | type 01 (306×4), type 23 (306×4), type 34 (612×2) |
| **21 307 392** | **type 13 (306×8), type 16 (612×4)** — les deux admis par le Xebec |
| 21 411 840 | type 02 (615×4), type 06 (615×4), type 10 (820×3) |
| 42 823 680 | type 37 (615×8), type 40 (820×6) |

PCem s'en sort parce que `hd_file` montre la géométrie déduite dans `HdSizeDlg` et laisse
la corriger. Une option n'a pas ce dialogue, d'où un avertissement **restreint aux
géométries que la carte accepte** — sans quoi monter le 10 Mo standard du XT avertirait à
chaque fois pour un type 34 que le Xebec ne connaît pas — et `--hdd-type N` pour trancher.

Se tromper entre 13 et 16 garde la bonne capacité et **décale l'adressage CHS** : le
système de fichiers se lit de travers sans qu'aucune erreur n'apparaisse.

### L'écran de construction

`Host/SdlSetup.cs`. L'oracle n'est pas une invention : PCem ouvre son **Configuration
Manager** avant de démarrer — `pc_main` tourne, puis wxWidgets démarre et `wx_load_config`
appelle `config_selection_open` avant `start_emulation`, sauf si `--config` a parlé
(`config_override`, `wx-sdl2.c:481-488`). Ses machines sont des `configs/<nom>.cfg`,
listées par un simple glob.

**L'écran ne monte rien**, et c'est sa propriété centrale. Il remplit exactement ce que
`pc.loadconfig` remplit — `pc.setmodel`, `cfg_mem_size`, `cfg_drive_type[]`,
`cfg_hdd_controller`, `discfns[]`, `hdc[]`, `ide_fn[]` — puis rend la main. C'est `initpc`
puis `resetpchard` qui montent, comme pour `--config`. Un seul chemin de montage.

**L'ordre d'initialisation s'inverse, et seulement sur ce chemin.** `SdlHost.Init`
appelait `pc.initpc` **avant** `SDL.Init` et la fenêtre — ce qui fait qu'une ROM absente
est signalée sans qu'une fenêtre ait clignoté. Cet ordre est conservé tel quel sur le
chemin direct ; l'écran, lui, a besoin d'un renderer pour se dessiner et choisit la
machine que `initpc` va monter, donc il l'inverse. La fenêtre s'ouvre à la taille de repli
— `video_width` vaut zéro avant le premier balayage CGA — et `SyncWindowSize`, qui existe
déjà, la recale après.

Il donne enfin un appelant aux six `config_set_*` et à `config_save`, transcrits et
vérifiés par `config-check` depuis § M8, et **morts depuis** — `ConfigCheck.cs` le disait :
« n'a aucun appelant tant que le menu n'édite pas la configuration ».

### Ce qu'une sonde a trouvé, et que l'œil n'aurait pas vu

L'écran ne se pilote pas ici : SDL3 tourne en Wayland natif et rien ne donne le focus à
une fenêtre. On a donc mesuré ses lignes par une sonde temporaire, posée puis retirée —
et elle a rendu **deux défauts réels** qu'une relecture n'aurait pas donnés :

1. **La fenêtre de défilement n'était pas recalée à l'ouverture.** Ouvrir la liste mémoire
   d'un 5150 à 640 Ko plaçait le curseur en 19ᵉ position et affichait les dix premières :
   `selected` valait **−1**, aucune ligne n'était en vidéo inverse, et l'écran paraissait
   n'avoir rien de sélectionné.
2. **Les `.cfg` de la racine n'étaient pas trouvés.** Lancé depuis la racine,
   `resolve_roms_path` rend « roms » tel quel, dont `GetDirectoryName` rend `""` ;
   `Directory.GetFiles("")` lève, l'exception était avalée, et la liste sortait vide alors
   qu'`ixtal26.cfg` et `ixtal26-xt.cfg` sont là.

Et deux autres sont tombés en exerçant l'enregistrement, tous deux dans `ConfigPath`
(§ M12.1), qu'un seul appelant ne pouvait pas montrer :

3. **Il levait sur un chemin vide** — `Path.GetFullPath("")` — et une machine a toujours
   un lecteur B vide à enregistrer. `--create-hdd` n'a jamais de chemin vide.
4. **Il ne relativisait que ce qui est DIRECTEMENT dans `os/`.** L'image de PC DOS vit
   dans `os/pcdos20/` : une machine enregistrée depuis Rider aurait porté son chemin
   absolu. Vérifié depuis les deux répertoires, la même ligne sort :
   `disc_a = os/pcdos20/pcdos20b.img`.

### Les portes

**L'inertie d'abord, et c'est la plus importante.** Rien de ce jalon ne doit toucher une
machine que la ligne de commande décrit :

| Épreuve | Résultat |
|---|---|
| `boot-diff roms 6000` | **25 457 269** |
| `boot-diff roms 7000 --fda …b.img` | **26 750 702** |
| `boot-diff roms 6000 --model ibmxt` | **23 442 234** |
| `boot-diff roms 6000 --config ixtal26-xt.cfg` | **19 511 811** |
| `boot-diff roms 7000 --config ixtal26-xt.cfg --fda …` | **22 086 920** |

Et l'écran ne s'ouvre pour aucune recette existante : `--boot`, `--slices`, `--headless`,
`--config`, `--model` ont tous été exécutés et ont tous rendu la main sans attendre une
touche.

**La porte de fond : la machine que l'écran produit est verte au diff.** Composée par le
code de l'écran — XT, 512 Ko, PC DOS en A:, un disque dur créé sur place et affecté à C: —
puis enregistrée, puis relue :

| | |
|---|---|
| `--config configs/machine.cfg --verbose` | `[8088] IBM XT, mem_size = 512 Ko` |
| `--boot --config configs/machine.cfg` | amorce DOS, `DIR` liste 23 fichiers |
| **`boot-diff roms 6000 --config configs/machine.cfg`** | **vert, 19 049 926 instructions** |

C'est ce dernier qui compte : il dit que l'écran produit une configuration que **l'oracle
sait lire aussi**, donc une machine réelle et pas seulement un fichier plausible.

Le fichier écrit, vérifié ligne à ligne, porte les seize clés de `pc.loadconfig` avec des
chemins relatifs des deux côtés :

```ini
model = ibmxt
mem_size = 512
disc_a = os/pcdos20/pcdos20b.img
hdd_controller = mfm_xebec
hdc_sectors = 17
hdc_heads = 4
hdc_cylinders = 306
hdc_fn = os/vierge-hdd-type01.img
```

**Et un auto-contrôle, parce que ce code n'a aucun autre moyen d'être exécuté.** SDL3
tourne ici en Wayland natif et rien ne donne le focus à une fenêtre : sans `--setup-check`,
la navigation au clavier et les bascules d'écran n'auraient jamais tourné une seule fois.
C'est le motif de `config-check` pour la moitié écriture du moteur de configuration, et
c'est le même ici — ce dépôt traite un chemin mort comme un chemin cassé.

Huit contrôles, tous verts. Deux méritent d'être nommés :

- **la revisite d'une liste après changement de modèle.** Ouvrir la liste mémoire d'un
  5150 à 640 Ko met le curseur en 19ᵉ position et le haut de fenêtre à 9 ; passer au XT
  et rouvrir donne 10 entrées, index 9, **haut 0** — `Activate` remet le haut à zéro
  avant de reconstruire, donc aucune fenêtre périmée ne survit. Vérifié parce que la
  question a été posée, pas parce qu'on soupçonnait le contraire.
- **les deux sorties de l'écran principal** : « Demarrer » rend `(termine, démarre)` =
  `(vrai, vrai)`, Échap rend `(vrai, faux)`. C'est la différence entre une machine qui
  démarre et un processus qui sort sans rien faire, et aucune autre porte ne la voit.

Contrôle négatif fait : inverser la sortie d'Échap donne `[ECHEC]` et **code de sortie 1**;
le contrôle sain sort 0.

**Reste** : `make -C tools/oracle selftest` vert, `check-oracle.sh` sans dérive,
`config-check` vert, `--setup-check` vert, `dotnet build -c Release` sans avertissement,
`TRANSCRIPTION.md` à 223 lignes sous le plafond de 225.

### Un profil de Rider a dû changer de forme pour ne pas changer de sens

Le profil « machine de reference, sans turbo » ne passait **aucun** argument — ce qui,
depuis ce jalon, ouvre l'écran. Il passe maintenant `--model ibmpc`, un **no-op
volontaire** : `ibmpc` est déjà le défaut, et le nommer dit que la machine est choisie.
Vérifié : `machine : [8088] IBM PC, mem_size = 640 Ko, lecteurs 1/1 (défauts)`, comme
avant.

Un septième profil, « construire la machine », porte `--setup` et vient en tête.

### Ce que ce vert ne dit pas

1. **L'écran n'a jamais été VU.** Ses largeurs sont mesurées contre `Cols` — la plus
   longue ligne fait 44 caractères sur 46 — sa hauteur comptée en lignes, ses transitions
   d'état exercées par une sonde. Son apparence, non. Même angle mort que le titre de
   fenêtre de § M11.1 et l'entrée de menu de § M12.1.
2. **Aucune touche n'a jamais été livrée à l'écran PAR SDL.** `--setup-check` appelle
   `HandleMain` directement, avec des `SDL.Scancode` fabriqués. Restent sans aucune
   exécution : `Run()` — la boucle d'évènements elle-même —, le répartiteur `Handle`, et
   `HandleList`, donc la navigation DANS une liste et son défilement. Ce qui est vérifié,
   c'est que les fonctions font ce qu'on croit quand on les appelle ; pas que SDL les
   appelle.
3. **« Parcourir... » n'existe pas dans cet écran.** Il liste `os/`. Un chemin quelconque
   passe par `--floppy-a` ou `--hdd`.
4. **Une seule machine a été composée et enregistrée.** Le chargement d'une machine
   enregistrée est exercé par la liste, pas par un aller-retour complet depuis l'écran.
5. **`config_save` détruit les commentaires**, parce que le parseur saute les `#` et que
   `entry_t` n'a pas de champ pour eux. C'est pourquoi l'enregistrement va toujours dans
   `configs/` et jamais par-dessus les deux exemples de la racine — mais rien n'empêche un
   `--config configs/x.cfg` suivi d'un enregistrement d'aplatir un fichier que
   l'utilisateur aurait commenté à la main.


## M14 — Fabriquer une disquette formatée, et y déposer des fichiers de l'hôte

Parti d'une question : *« je voudrais pouvoir créer une disquette et y déposer des
fichiers venant de mon disque dur Linux »*. Le dépôt s'arrêtait juste avant. Il savait
fabriquer une image de N × 512 octets **nuls** — `SdlMenu.CreateBlank`, pendant de
`wx-createdisc.cc:62-73` — et rien de plus : pas de BPB, pas de FAT, pas de signature.
La seule route vers une disquette utilisable passait par `FORMAT` **dans** la machine
émulée, ce qui ne fait entrer aucun octet venu de l'hôte.

Et aucun contournement n'existait sur cette machine : `mtools` n'est pas installé, et
`mkfs.fat` seul sait formater une image mais pas y copier de fichier.

### Les quatre BPB ne sont pas devinés : ils sont lus dans FORMAT.COM

C'est le point qui décide si tout le reste tient. Écrire un système de fichiers de
mémoire produit une image que DOS lit de travers **en silence** — la taille est bonne, le
nombre de secteurs est bon, et le contenu est faux.

`os/pcdos20/pcdos20b.img` porte `FORMAT.COM`. À l'offset **0xC7FA** de l'image, quatre
enregistrements de 18 octets, immédiatement suivis de `"Formatting...$"` :

```
01 01 00 02 40 00 40 01 fe 01 00 08 00 01 00 00 00 00   160 Ko
02 01 00 02 70 00 80 02 ff 01 00 08 00 02 00 00 00 00   320 Ko
01 01 00 02 40 00 68 01 fc 02 00 09 00 01 00 00 00 00   180 Ko
02 01 00 02 70 00 d0 02 fd 02 00 09 00 02 00 00 00 00   360 Ko
```

C'est le BPB privé de son mot `bps`, soit les offsets `0x0D`-`0x1D` du secteur d'amorce.
Les deux **derniers** reproduisent octet pour octet les secteurs 0 de `pcdos20b.img`
(180 Ko) et `vierge-360k.img` (360 Ko), deux disquettes réellement formatées par DOS 2.00
que le dépôt porte déjà. Les deux premiers sont donc du même niveau de preuve — et c'est
ce qui rend le 160 Ko et le 320 Ko non conjecturaux, aucune de ces deux tailles n'existant
sur disque ici.

### Le BIOS du 5150 ne lit AUCUNE signature sur disquette

Désassemblé dans `roms/ibmpc/pc102782.bin`, l'INT 19h en `0xE701` :

```
e712: b8 01 02    mov ax,0201      ; lire 1 secteur
e715: 2b d2 / 8e c2 / bb 00 7c     ; ES:BX = 0000:7C00
e71f: cd 13
e722: 73 c0       jae e6e4
e6e4: ea 00 7c 00 00                jmp 0000:7C00     ; INCONDITIONNEL
```

La retenue de l'INT 13h est la **seule** condition. Aucun `cmp` sur `0xAA55` — les deux
occurrences de cette valeur dans la ROM appartiennent au balayage des ROM d'extension.
Conséquence contraignante : un secteur 0 nul s'exécuterait en `add [bx+si],al` et partirait
dans le décor. **Le talon d'amorce est obligatoire, pas décoratif.** Il est écrit à la
main — 36 octets — et non recopié du chargeur d'IBM, que `.gitignore` refuse de versionner.
Il a été **assemblé puis re-désassemblé** avant d'être figé, et l'adresse du message est
recalculée depuis la longueur du talon plutôt qu'écrite en dur.

### L'invariant de géométrie, mesuré et non supposé

Une image vierge fait rendre 0 aux cinq lectures de BPB d'`img_load`, donc la garde de
`disc_img.cs:242` force la branche de devinette par **taille**. Les images de `FatImage`
portent un BPB valide : elles prennent l'**autre** branche (`:309-392`). Les deux doivent
rendre la même géométrie, sinon c'est l'image qui est fausse. `bpb_disable`, posé à 0 puis
à 1 sur le **même fichier**, compare exactement les deux chemins — d'où `disc_img.Probe`,
sonde de diagnostic sur le modèle de `fdc_c.Probe`, `img[]` étant privé.

| | par BPB | par taille |
|---|---|---|
| 160 Ko | 8 × 40 × 1, secteur 512, trou 0, xdf 0 | identique |
| 180 Ko | 9 × 40 × 1, secteur 512, trou 0, xdf 0 | identique |
| 320 Ko | 8 × 40 × 2, secteur 512, trou 0, xdf 0 | identique |
| 360 Ko | 9 × 40 × 2, secteur 512, trou 0, xdf 0 | identique |

Le 320 Ko est le seul dont la branche taille ne *calcule* pas `sides` et hérite du défaut
de `:237` — c'est la ligne fragile, et c'est celle que la mesure couvre.

### L'empaquetage FAT12 : trois contrôles, et pourquoi le troisième ne suffit pas

`--fat-check`. La FAT1 de `os/vierge-360k.img` est la chaîne de `BASIC.COM` et couvre les
**deux** alignements de l'empaquetage 12 bits sur toute sa longueur :

```
fd ff ff 03 40 00 05 60 00 07 80 00 09 a0 00 0b c0 00 0d e0 00 0f 00 01 11 f0 ff
```

1. **Valeurs absolues** — entrée 0 = `0xFFD`, entrée 1 = `0xFFF`, entrées 2..16 = `n+1`,
   entrée 17 = `0xFFF`. C'est ce contrôle qui épingle la branche **impaire** seule :
   l'entrée 3 est impaire et vaut 4, ce qu'aucune inversion des deux branches ne contrefait.
2. **Écriture depuis zéro** — la chaîne 2→17 écrite dans une FAT vierge rend les 27 mêmes
   octets.
3. **Aller-retour** — 354 entrées écrites puis relues, et les deux entrées réservées
   intactes.

Le troisième seul ne prouverait rien : il exerce lecture et écriture **composées**, et
deux défauts inverses l'un de l'autre s'y annulent.

### Le marqueur de fin de répertoire, et les quatre racines qu'on rencontre

C'est l'oubli le plus discret du lot : `FORMAT` remplit la racine de `0xF6`, qui n'est ni
`0x00` ni `0xE5`, donc un `DIR` y lirait une entrée nommée `0xF6F6F6…`. DOS, lui, estampe
un `0x00` sur l'octet 0 de la première entrée jamais utilisée — visible en `0xA20` de
`vierge-360k.img`.

L'invariant : **après un dépôt, exactement un `0x00`, et après toutes les entrées
utilisées.** Une image sortie de `Create` n'en présente qu'une disposition ; le disque en
présente quatre, parce que `FORMAT` ne pose aucun marqueur, que `DEL` laisse des `0xE5`, et
qu'une entrée peut traîner **après** le marqueur. Les quatre sont exercées, avec le cas
tordu — l'entrée orpheline en 1 alors que l'entrée 0 porte le marqueur — qui range bien le
nouveau `0x00` en 2, donc après l'orpheline.

### Une sonde hôte dans un fichier à part

`disc_img.Probe` n'a **aucun pendant dans l'oracle**. `fdc_c.Probe` en a un —
`h_disc_probe()`, `harness.c:853` — et c'est ce qui l'autorise à vivre dans un fichier
transcrit. Invoquer ce précédent pour celle-ci serait confondre deux catégories : ce
serait du code hôte non déclaré au milieu d'une région gouvernée par R1 et R2. Elle vit
donc dans `Disc/disc_img_probe.cs`, `STATUS: host`, seconde partie du `partial` — la
frontière de fichier dit la catégorie sans qu'on ait à croire un commentaire.

### Ce que DOS 2.00 en dit, pour les quatre formats

Fabrication, dépôt de deux fichiers venus de `/tmp`, puis `DIR B:` et `TYPE B:LISEZMOI.TXT` :

| format | ce que DOS annonce | octets libres annoncés par l'outil |
|---|---|---|
| 160 Ko | les deux fichiers, 43 et 3000 octets, dates et heures exactes | 156 672 — **identique** |
| 180 Ko | idem | 176 128 — **identique** |
| 320 Ko | idem | 318 464 — **identique** |
| 360 Ko | idem, plus un fichier de taille nulle | 358 400 — **identique** |

`TYPE` rend le contenu exact du fichier Linux. Un lecteur FAT12 **indépendant** (écrit en
Python, donc sans partage de code avec `FatImage`) extrait les trois fichiers d'une 360 Ko :
`md5` identiques aux sources, `FAT1 == FAT2`. `fsck.fat 4.2` relit le secteur d'amorce et
retrouve toute la géométrie.

### Les deux sens, et l'aller-retour complet

- **Le talon exécuté.** Amorcé sur une disquette fabriquée ici : le BIOS charge le secteur,
  saute dedans, et l'écran affiche `Disquette non systeme / Remplacer et frapper une touche`.
  Puis INT 19h, qui relit la même disquette — **boucle infinie voulue**, identique à celle
  du vrai chargeur d'IBM.
- **DOS qui écrit.** `COPY B:LISEZMOI.TXT B:COPIE.TXT` sous DOS 2.00 : `1 File(s) copied`,
  cluster 3 alloué, `FAT1 == FAT2` après coup, secteur d'amorce intact (`IXTAL1.0`, `55AA`),
  et le `md5` de la copie égal à celui du fichier Linux d'origine.
- **L'outil qui reprend.** `--floppy-put` sur cette même image après DOS : le dépôt passe,
  et un doublon de l'entrée que **DOS** a écrite est refusé en la nommant.

### Les refus, et l'image inchangée

Tous exercés, chacun nommant la règle violée : radical de 9 caractères (**refusé, jamais
tronqué** — tronquer fabrique un doublon silencieux), nom de périphérique DOS, espace dans
le nom, source inexistante, répertoire, place insuffisante (« 391 clusters demandés, il en
reste 350 »), doublon, image non formatée (« la fabriquer avec `--create-floppy` »), format
inconnu, fichier déjà existant. Après les dix, `cmp` déclare l'image **identique octet pour
octet** à ce qu'elle était avant : la phase 1 n'écrit rien.

Le refus qui compte le plus est celui d'une image **montée** : `img_seek` tient une piste
en cache qu'`img_writeback` réécrit, et au-dessus DOS garde sa propre copie de la FAT et de
la racine. Éjecter, écrire dans son dos, réinsérer — et DOS réécrit sa FAT périmée. Le menu
Ctrl+F12 **n'éjecte donc jamais** à la place de l'utilisateur.

### Ce que ce vert ne dit pas

1. **Le menu Ctrl+F12 n'a pas été vu.** `--menu-check` exerce ses chemins clavier — les deux
   entrées neuves, le bouclage de la liste des formats, le titre en mode cible, Échap — mais
   s'arrête **net devant le sélecteur de fichiers**, qui exige une vraie fenêtre. Les deux
   temps du dépôt par le menu (choisir l'image, puis la source) ne sont couverts par aucune
   exécution. Même angle mort que § M13.
2. **`--menu-check` a d'abord trouvé une assertion fausse, pas un défaut.** « Haut depuis le
   dernier format boucle sur le premier » était faux : `Haut` décrémente. Le contrôle a
   corrigé celui qui l'écrivait.
3. **Aucun `FORMAT` réel n'a été relancé** pour confirmer que FORMAT applique bien
   l'enregistrement 1 au simple face 8 secteurs et le 2 au double face. La question reste
   ouverte sur la **provenance** ; elle est sans objet sur la **validité**, DOS 2.00 lisant
   les quatre images fabriquées ici.
4. **Le répertoire racine seul.** Pas de sous-répertoires, et un répertoire donné en source
   est refusé. Le répertoire racine d'un FAT12 ne s'agrandit pas — 64 ou 112 entrées.
5. **Aucune disquette système.** Rendre l'image amorçable reste le travail de `SYS` sous DOS.
6. **Écriture seule.** Extraire un fichier de l'image vers Linux n'existe pas encore ; le
   parcours de FAT est écrit, la commande ne l'est pas.
7. **Une seule taille de cluster par format.** Le remplissage du dernier cluster est
   vérifié par la relecture, pas par une inspection de ce qui traîne au-delà de la taille
   déclarée.

---

## M15 — La VGA d'IBM : trois fichiers de PCem, et un oracle qui voit enfin les pixels

Parti de la demande *« ajoute le nécessaire pour avoir du VGA »*. PCem porte la VGA d'IBM en
trois fichiers : `vid_vga.c` (170 lignes), la carte elle-même, et le socle générique de toutes
ses SVGA, `vid_svga.c` (1 682) et `vid_svga_render.c` (916). La ROM est au dépôt :
`roms/ibm_vga.bin`, 32 Ko dont PCem mappe 24 Ko en C0000 à partir de l'offset 0x2000 — et
l'en-tête déclare exactement 48 × 512 octets. *Corrigé en G7.1 :* « donc le balayage des ROM
ne lit rien au-delà » était FAUX. L'allocation fait 32 Ko, les 8 derniers (C6000-C7FFF) sont
du tas, et le balayage du 5150 lit C600:0000, le bloc qui suit les 24 Ko déclarés. PB-24
élargi, § G7.0 (le rouge) et § G7.1 (le correctif de l'oracle).

### L'ordre : l'oracle d'abord (`4f56525`)

Même ordre que B2 pour l'AT : la `.so` apprend la VGA avant qu'une ligne de C# ne s'écrive.
`vga-probe` amorce l'**oracle seul** et lit l'écran dans la VRAM brute :

| Machine | Écran de l'oracle, VGA |
|---|---|
| `ibmat` | `00640 KB OK` / `161-System Options Not Set-(Run SETUP)` |
| `ami286` | `286-BIOS (C)1990 …` / `CMOS display type mismatch` / `Press <F1>` |
| `ibmxt` | Cassette BASIC C1.10, `62940 Bytes free`, `Ok` |
| `ibmpc` | idem |

`hdisp` 720, 400 lignes, cellules de 9 points partout : le BIOS VGA a posé le mode 3. La
question « le 5150 et le XT balaient-ils C0000 ? » est tranchée par la mesure : oui. PCem
propose d'ailleurs la VGA sur les quatre machines — aucun des filtres de `wx-config.c:145`
(PCI, MCA) ne la retire.

### Le piège du jalon était un stub vide

`video_updatetiming` était un no-op dans l'oracle, et les six `video_timing_*` n'y existaient
pas : `vid_cga.c` ne les lit pas. `vid_svga.c` les facture à **chaque** accès —
`cycles -= video_timing_write_b` (`:818`), `-= video_timing_read_b` (`:1068`). Laissées à
zéro pendant que `video.cs` les calcule, chaque octet de VRAM aurait coûté 8 cycles d'un
côté et 0 de l'autre : une dérive de `tsc` sans un registre de différence. Rendue réelle,
réduite comme `video.cs` la réduit. **Les cinq chiffres du 8088 n'ont pas bougé d'une unité
avec la VGA liée**, ce qui prouve que l'oracle reste neutre pour la CGA.

### Deux déviations de l'oracle

- `svga_init` alloue VRAM et `changedvram` par `malloc` sans les effacer (`vid_svga.c:772`,
  `:777`). La première VRAM du processus vient de `mmap`, donc nulle ; mais `vga_close` la
  libère, glibc relève son seuil de mmap dynamique, et la VRAM de la **phase 2** du
  boot-diff vient du tas avec les octets de la phase 1. `memset` après `device_add` — même
  arbitrage que `h_pad_ram` et PB-24.
- `initvideo` remet `buffer32` à zéro, comme `video.cs` (`Array.Clear`). Sans cela la
  comparaison de framebuffer comparerait deux histoires.

### Ce qui a été transcrit, et la parité

| Fichier | C vivant | C# vivant | Rapport |
|---|---:|---:|---:|
| `vid_svga.cs` ← `vid_svga.c` (sans `*_linear`) + `vid_svga.h` | 1 119 | 1 207 | 1,078 (1,006 hors sonde) |
| `vid_svga_render.cs` ← rendus transcrits + `vid_svga_render_remap.h` | 351 | 385 | 1,096 |
| `vid_vga.cs` ← `vid_vga.c` (sans `ps1vga`) | 99 | 97 | 0,979 |

Tout ce qu'une VGA **appelle** est transcrit, chemins inertes compris — le chemin `fast` de
`svga_writew`/`readw`, le curseur matériel et le recouvrement de `svga_poll`, l'entrelacé :
moins cher que d'en justifier l'omission. Omis, au registre : la fenêtre linéaire, les rendus
qu'aucune ligne de `vid_svga.c` ni `vid_vga.c` ne choisit pour une VGA, `ps1vga_init`. Les
rendus 15 à 32 bpp que `vid_svga.c` **nomme** restent, en `fatal()`.

**Le registre `VIDEO_CARD` cesse d'être une constante.** Avec la seule CGA, `video_is_cga()`
rendait 1 en dur des deux côtés ; `video_init` faisait `device_add(cga_device)` sans le
registre. Il est transcrit, réduit à `v_cga` et `v_vga` : `video_init`, `video_is_*`,
`video_old_to_new`, `video_get_video_from_internal_name`. La leçon de § M10, une seconde fois.

**Un ordre d'évaluation vérifié sur le binaire.** `svga_readw` fait
`svga_read(addr) | (svga_read(addr + 1) << 8)` : deux appels à effets de bord (verrous
`la..ld`, cycles) dont le C ne fixe pas l'ordre. C# évalue de gauche à droite ; `objdump` de
`build/vid_svga.o` montre GCC appeler aussi `addr` puis `addr + 1`. Noté sur place.

### La sonde VGA : l'oracle de ce que le diff d'instructions ne voit pas

Le diff compare ce que le CPU relit — une `IN 3DA`, une lecture de VRAM — et rien de ce qu'il
ne relit pas : la palette du DAC, la police du plan 2, les pixels. `h_vga_probe` et
`vid_svga.Probe` rendent 64 champs — VRAM, `changedvram`, `buffer32`, registres CRTC,
séquenceur, GDC et attributs, `vgapal`, `pallook` et `egapal` hachés en FNV-1a, et les
scalaires bruts. `boot-diff` les compare en fin de course dès qu'une VGA est montée.

Le harnais sait-il rougir ? Quatre pannes injectées puis révoquées, XT, BASIC :

| Panne | Diff d'instructions | Sonde VGA |
|---|---|---|
| F1 : `svga_write` facture un cycle de plus | **rouge**, instruction 1 062 816, `tsc` | — |
| F2 : 9ᵉ colonne des cellules de texte, `fg` au lieu de `bg` | vert, 23 360 025 | **rouge**, `#buffer32` seul |
| F3 : DAC 6 → 8 bits en `× 3` au lieu de `× 4` | vert | **rouge**, `#pallook` et `#buffer32` |
| F4 : `attrregs[0x14] & 0x8` au lieu de `& 0xc` | vert | vert — **inerte, mesuré** : `attrregs[0x14]` vaut 0 |
| F4b : `attrregs[c] & 0x1f` au lieu de `& 0x3f` | vert | **rouge**, `#egapal` seul |

F2, F3 et F4b sont exactement ce que le diff d'instructions ne peut pas voir.

### Les campagnes

| Campagne | Instructions identiques | Sonde VGA |
|---|---:|---|
| 5150, ROM BASIC, `--gfxcard vga` | 25 266 173 | 64/64, 4 542 trames |
| 5150, PC DOS 2.00 | 26 534 951 | 64/64, 5 222 trames |
| XT, ROM BASIC | 23 360 025 | 64/64, 4 478 trames |
| XT, PC DOS 2.00 | 22 029 326 | 64/64, 5 148 trames |
| **XT, DOS + DEBUG, modes 13h, 12h, 0Dh, 4, 1, 3** | **353 694 491** | **64/64, 88 809 trames** |

La dernière tape 125 lignes en 1 740 évènements de clavier, de la tranche 7 000 à la
tranche 138 960 : `B:DEBUG`, un programme qui passe en 13h et remplit l'écran
octet par octet, un qui pilote le GDC à la main en 12h — modes d'écriture 2 et 3, XOR, AND
et OR, rotation, set/reset, mode de lecture 1, et une lecture du DAC juste après
`OUT 3C8h,0` —, puis chaque mode du BIOS avec défilement.

### La couverture, comptée et non supposée

Compteurs de passage dans une copie jetable du C#, sur la même campagne :

| | Exercé |
|---|---|
| Rendus | blank, texte 80 (6,7 M lignes), texte 40, 2 bpp basse rés., 4 bpp basse et haute rés., 8 bpp basse rés. — **7 sur 9** |
| Écriture | mode 0 en `chain2`, `chain4` et plans ; mode 0 + OR + set/reset ; mode 1 (3,2 M) ; mode 2, mode 2 + XOR ; mode 3 + AND + rotation |
| Lecture | mode 0 en `chain2`, `chain4` et plans ; mode 1 |
| DAC | 1 539 lectures, dont 2 à l'index -1 (PB-35) |

**Jamais atteints** : `svga_render_2bpp_highres` et `svga_render_8bpp_highres`, qu'aucun
mode du BIOS ne choisit ; le chemin `fast` (il exige `packed_chain4` ou `fb_only`, qu'aucune
ligne de la VGA ne pose) ; curseur matériel, recouvrement, entrelacé. Pour ceux-là le seul
filet est la relecture ci-dessous.

### Deux défauts d'outillage, trouvés en chemin

1. **`boot-diff` plafonnait à ~268 M d'instructions.** Il chargeait la trace de l'oracle par
   `File.ReadAllBytes` (2 Go au plus) et l'indexait par `n * 8` en `int`. La campagne
   graphique l'a dépassé — un 8088 qui fait défiler le mode 13h octet par octet. La trace se
   lit désormais en flux ; les chiffres de non-régression sont inchangés.
2. **L'écran de fin s'imprimait sous l'en-tête « CGA ».** `pc.closepc()` ferme la carte,
   `svga_pri` redevient nul, et l'affichage retombait sur `mem_readb_phys` — donc sur
   `svga_read`, qui touche les verrous. La carte est retenue avant `closepc`, et l'écran
   VGA se lit dans la VRAM brute (`BootTest.DumpVgaTextScreen`).

Et un troisième, laissé tel quel : `KeyScript` ne sait pas taper `[` ni `]`. La campagne
contourne par `es:` puis `lodsb`.

### La relecture contradictoire, et le seul défaut qu'elle a trouvé

Le diff prouve l'équivalence sur les chemins exercés ; pour les autres, la relecture. Cinq
relecteurs sur des tranches disjointes — registres et `recalctimings`, `poll` et accès
larges, `svga_write`/`svga_read`, les rendus, la colle (`vid_vga.c`, le registre de
`video.c`, `pc.c`, le harnais) —, puis un sceptique par tranche chargé de **réfuter** chaque
constat. Seize constats, quinze confirmés, un rejeté (déjà corrigé entre-temps).

Quatorze sont des citations ou des commentaires faux : des plages `// pcem:` décalées de
trois à huit lignes dans `video.cs` et `keyboard_at.cs`, un décompte faux (« soixante-quatre
cartes » : `video.c` en définit cinquante, en enregistre quarante-neuf), un `default:` que
le `switch` de `video_init` n'a pas, et un commentaire qui prétendait que seul `vga_init`
pose `bpp = 8` — `svga_init` le fait d'abord. Tous corrigés. Aucun ne touchait le
comportement, et c'est le constat : **le diff ne voit pas les commentaires, et c'est la
relecture seule qui les tient honnêtes.**

Le quinzième est sémantique, et c'est PB-36 : `(uint64_t)` d'un `double` négatif dans
`svga_recalctimings`, que GCC rend `(uint64_t)(int64_t)x` et que .NET 10 **sature** à 0.
Le transitoire qui l'atteint — CR00 réécrit avant CR01 en passant en 40 colonnes — avait
traversé la campagne graphique sans rougir : aucune trame n'était tombée dans la fenêtre.
Rendu observable par un programme DEBUG qui la tient ouverte (CR00 = 2Dh sous CR01 = 4Fh,
8 192 lectures de `3DAh`) :

| | Diff |
|---|---|
| ancienne conversion `(uint64_t)x` | **rouge**, instruction 36 899 042 |
| `unchecked((uint64_t)(int64_t)x)` | **vert**, 37 969 642 instructions, sonde 64/64 |

`vid_cga.c:115-116` porte le même motif depuis M4, corrigé de la même façon ; les cinq
portes du 8088 le couvrent. Et un défaut de l'OUTIL au passage : la phase 2 de `boot-diff`
ne rejoue pas le script de frappe, donc elle ne peut pas localiser une divergence d'une
campagne tapée — elle a rendu ici « les états concordent », sur une machine restée à
l'invite de date. Laissé tel quel, noté.

### L'IBM AT

`--boot --model ibmat --gfxcard vga --floppy-a pcdos20b.img --type $'\x01'` amorce PC DOS
2.00 en VGA : invites de date et d'heure en 80 × 25. Le `104-System Board Error` du POST est
le défaut connu de l'AT côté C#, sans rapport avec la carte.

`boot-diff --model ibmat --gfxcard vga` diverge à l'instruction 7 : `IN AL,71h`, oracle
`0xFF`, C# `0x10` — le **CMOS**. Même site et mêmes valeurs en CGA (instruction 40) : c'est
la divergence que `TRANSCRIPTION.md` décrit, le C# lisant `nvr/.at.nvr` là où l'oracle prend
la branche sans fichier. Aucun `boot-diff` d'AT ne peut aujourd'hui atteindre la VGA.

Ce qui se mesure quand même, sur le 286 : les entrées du chemin de temps. Lues dans la
`.so` après un amorçage AT en VGA, `video_timing_read_b/w/l` et `write_b/w/l` valent
**8 / 16 / 32**, avec `isa_cycles = 1` et `cpu_16bitbus = 1` — donc `read_l = read_w × 2`
a bien joué. Le C# les tire de la même formule, `isa_cycles` et `cpu_16bitbus` étant posés
à 1 en dur (`386.State.cs:45`, `:48`). Et la machine de § « 286 complet », `ami286` à
4 096 Ko, termine son POST en VGA : bannière AMI tracée par le BIOS VGA, `04096 KB OK`, et
« CMOS display type mismatch » disparu — le CMOS de référence déclare une EGA/VGA.

### La fenêtre, enfin

Rien de ce qui précède ne passe par l'hôte : `--boot` et `boot-diff` s'arrêtent à
`Buffer32`. Le vrai hôte SDL, XT, VGA, 6 200 tranches, `--verbose` : **4 497 blits émis,
4 497 consommés, 4 497 téléversés, 0 en échec**, dernier blit `x=32 y=0 y1=0 y2=400 w=720
h=400`, fenêtre recalée à 720 × 400. La texture de 512 lignes tient les 480 du mode 12h ;
ce mode-là n'a été vu que dans `Buffer32`, l'hôte n'ayant pas de frappe scriptée.

### Ce que ce vert ne dit pas

1. **L'AT n'est pas sous oracle pour la VGA.** Tout le vert ci-dessus est sur 8088. Le cœur
   286 accède à la VRAM par le même `svga_read`/`svga_write`, avec un bus de 16 bits : ses
   ENTRÉES de temps sont mesurées égales, son exécution n'a été vue qu'en `--boot`.
2. **Deux rendus, le chemin `fast`, le curseur et l'entrelacé** n'ont que la relecture.
3. **L'hôte SDL n'est pas sous oracle** : `Buffer32` l'est, la remontée seulement comptée
   (ci-dessus), et en mode texte seulement.
4. **256 Ko de VRAM seulement**, la valeur de `vga_init` ; une SVGA en voudrait plus, et
   aucune n'est transcrite.

---

## M16 — La cadence du 286 : la mesurer, puis la régler

Parti de la question *« comment mesurer la cadence de la machine émulée ? je voudrais
vérifier que mon 80286 tourne entre 20 et 25 MHz »*. La réponse de la lecture du code
précède toute mesure : **il ne le pouvait pas**. `cpu_get_speed()` rendait 4 772 728 en
dur (`pc.cs`, `Cpu/cpu.cs`) — le budget du 8088 — alors que `pc_reset` pose
`setpitclock(6 000 000)` pour l'AT. L'oracle fait exactement pareil (`__wrap_cpu_get_speed`,
`h_runpc`, `harness.c:1121`), assumé au commit `9dff06f` « pour la symétrie » : **aucune
porte ne pouvait voir l'écart**, puisque les deux côtés l'ont. PCem, lui, rend rspeed
(`cpu.c:196-203`, `:2067-2071`) : 60 000 cycles par tranche pour un 286/6.

### Trois chiffres, qu'il ne faut pas confondre

| Chiffre | Définition | Ce qui le mesure |
|---|---|---|
| **Cadence vue par l'invité** | cycles consommés par seconde du compteur de tops BDA | `--timer-check`, rapport 5 |
| **Cadence fournie** | cycles consommés par seconde MURALE | titre de la fenêtre, `X MHz` |
| **Vitesse du temps invité** | secondes de BDA par seconde contractuelle (100 tranches) | `--timer-check`, rapport 6 ; titre, `invite NN %` |

Un banc d'essai EXÉCUTÉ DANS L'INVITÉ — une boucle chronométrée au PIT, `TIMER` en BASICA —
ne peut pas trancher : le PIT, le RTC et le CPU sont dans le même domaine d'horloge, la
mesure s'annule et rend l'horloge de `setpitclock` par construction. Seule une comparaison
avec les tranches, ou avec une montre, fait apparaître l'écart. Sans aucun code : `TIME`
sous DOS, soixante secondes au chronomètre, `TIME` à nouveau — l'AT n'en compte que 47,7.

### Étape 1 : l'instrument (code hôte seul)

`--timer-check` était le contrôle de fréquence absolue du 5150 : constantes du 8088 en dur,
et une branche qui rendait la main **avant** que `--config` et `--model` soient appliqués —
il ne pouvait mesurer que la machine par défaut. Il prend désormais `--model`, `--config`,
`--gfxcard`, `--floppy-a/b` et `--boot-slices`, lit le budget, l'horloge et le nombre de tsc
par cycle **après** `initpc`, et ajoute trois rapports : fréquence vue par l'invité (5),
temps invité sur temps contractuel (6), et un bloc hôte non déterministe (7). Le titre de la
fenêtre compte le temps de l'invité dans son tsc, tranche par tranche autour de `runpc()`,
au lieu de supposer 10 ms par tranche : `invite NN % - X MHz - marge xM`.

**Inertie sur le 5150** : toutes les lignes déterministes identiques à la sortie du binaire
de référence (`ebd4aef`) — 5 462 tops, Δtsc, cycles, instructions, rapports 1 à 4
(−59,89 ppm). Les nouveaux rapports disent ce que § « Contrôle de fréquence » établissait
déjà : 4,773 MHz vus par l'invité (+52,3 ppm, le manque de comptabilité du 8088), rapport
de temps 0,999942.

**Sur les deux 286**, avec `--timer-check roms 300 --model ibmat` puis `--model ami286`,
prédictions écrites avant la mesure :

| | Prédit | ibmat | ami286 |
|---|---|---|---|
| 1. Δtsc par s contractuelle | 4 772 700 contre 6 000 000 | 4 772 700,05 (−204 550 ppm) | 4 772 700,03 (−204 550 ppm) |
| 2. tops par s de tsc | 18,2065 (tautologique) | 18,206509 (−0,18 ppm) | 18,206509 (−0,18 ppm) |
| 3. tops par s contractuelle | ≈ 14,482 | **14,482368** | **14,482368** |
| 5. fréquence vue par l'invité | 6,000 MHz | **6 000 001,1 Hz** (+0,18 ppm) | **6 000 001,1 Hz** (+0,18 ppm) |
| 6. temps invité / contractuel | 0,7955 | **0,795450** | **0,795450** |
| cycles jamais portés au tsc | 0 | 0 sur 1 431 905 470 | 0 sur 1 431 905 462 |
| tsc par top | 329 552,4 | 329 552,467 | 329 552,465 |

Le 286 n'a **aucun** manque de comptabilité : `exec386` ajoute au tsc les cycles de chaque
instruction, E/S comprises (`386.cs`, `timer.tsc += ins_cycles`), là où le 8088 en perd
53,9 ppm. Les deux machines donnent la même fenêtre de 4 345 tops ; seul le CPI les
distingue (4,70 contre 6,47), c'est-à-dire ce que leurs BIOS exécutent.

**Conclusion de l'étape 1** : le 286 d'iXtal26 tourne, VU PAR L'INVITÉ, à 6,000 MHz — et
l'horloge murale ne lui fournit que 4,773 MHz : son temps s'écoule à 79,545 % du temps réel,
et le « 100 % » que le titre affichait sur l'AT valait en réalité 80 %. Les chiffres de
l'hôte (rapport 7) ne sont pas consignés ici : une autre session compilait pendant la
mesure, et le protocole de § M5.1 exige une machine au repos.

### Étape 3 : cpu_set() à sa place, chiffres gelés

La sélection de CPU de PCem est portée, réduite aux deux familles que les tables du dépôt
portent : `struct CPU` et les tables `cpus_8088`, `cpus_286`, `cpus_ibmat`
(`Cpu/cpu_tables.cs`), `cpu_set()` réduit et `cpu_update_waitstates()` entier
(`Cpu/cpu.cs`), le membre `cpu[5]` de `MODEL`, les clés `cpu_manufacturer` et `cpu`, et
`--cpu N`. `cpu_set()` tourne dans `resetpchard` AVANT `mem_alloc`, comme `pc.c:363` —
ce dépôt posait la configuration du 286 dans `at_init`, donc après. `setpitclock` lit la
rspeed de l'entrée choisie. Un indice hors de la table est REFUSÉ en listant ce qui
existe (`check_cpu`, DEVIATION : PCem lit hors du tableau).

**Côté oracle, le VRAI `cpu_set()` de PCem tourne** dans `h_boot` : `models[]` peuplé
pour les quatre romsets, `model = romset`, `cpu` et `cpu_manufacturer` poussés par
`h_set_cpu` et écrits explicitement (`cpu.c:82` les initialise à 3). `h_reset` — fuzz,
SST, selftest, core286-check — garde `h_cpu_config_*`. ABI 18.

Deux provisoires, marqués `DEVIATION: TEMPORAIRE` des deux côtés, pour que cette étape ne
déplace aucun chiffre : `cpu_get_speed()` rend encore 4 772 728 partout, et l'ibmat
pointe encore sur `cpus_286` au lieu de `cpus_ibmat`.

**L'empreinte CPU** (`h_cpu_fingerprint`, `CpuFingerprint.cs`) : 37 grandeurs — vitesse,
budget de tranche, cycles mémoire, préfetch, `isa_cycles`, domaine d'horloge, temps
vidéo, `mem_size` —, prises juste après l'amorçage des deux côtés et confrontées champ
par champ. `boot-diff` la vérifie avant la première instruction ; c'est elle qui ferme le
piège « un budget asymétrique ne se voit pas dans la trace d'un AT ».

**`cpu-config-check`** amorce chaque entrée de chaque table des deux côtés :

| Machine | Entrées | Résultat |
|---|---|---|
| ibmpc, ibmxt | 8088/4.77 à 8088/16 | 12 identiques |
| ibmat (T-ibmat), ami286 | 286/6 à 286/25 | 14 identiques |
| indice hors table | 6, 6, 7, 7 | 4 refus concordants |

À 286/20 et 286/25, les deux côtés donnent `busspeed` 2e7 / 2,5e7, `isa_cycles` 3,
lecture/écriture 4/8, préfetch mémoire 4, préfetch ROM **20 / 25**, temps vidéo
24/48/96. Le préfetch identique prouve au passage que `--wrap` ne capture pas l'appel
interne de `cpu_set` à `cpu_update_waitstates` — sinon il serait nul côté oracle.

**Tout est resté en place**, au chiffre près : les cinq boot-diff (25 457 269 /
26 750 702 / 23 442 234 / 19 511 811 / 22 086 920), fuzz 8088 et fuzz 286 (255 opcodes),
core286-check et SST (160 592 / 166 998, forme par forme) identiques aux sorties de
`ebd4aef`, selftest, `--setup-check`. Contre le binaire de `9530e21`, depuis un même
répertoire et un même instantané de CMOS : `--timer-check` sur les quatre machines,
`--boot` 1 500 tranches sur ibmat et ami286, `vga-probe` sur les quatre machines —
identiques. `at-probe` ne diffère que par deux adresses, et parce qu'il constate
désormais `cpus` peuplé. `boot-diff --model ibmat` vérifie l'empreinte, puis diverge
toujours à l'instruction 40, sur le CMOS (§ M15).

**Relecture contradictoire avant commit** (trois relecteurs : fidélité, symétrie,
régressions). Rien de bloquant ; ce qu'elle a trouvé, et ce qui en a été fait :

- **`AT` n'était jamais remis à 0**, d'aucun côté : PCem le fait dans `model_init`
  (`model.c:688`, `AMSTRAD = AT = PCI = TANDY = MCA = 0`), omis sans marque. Un 8088
  amorcé après un AT dans le même processus partait en mode AT — des deux côtés, donc
  en vert. Le vert de `cpu-config-check` ne tenait qu'à l'ordre de son balayage.
  Transcrit des deux côtés ; `cpu-config-check --inverse` balaye maintenant les AT
  d'abord, et doit rester vert.
- **Deux omissions avaient perdu leur motif.** `cpu_update_waitstates()` aux resets et
  `cpu_set_edx()` après un reset se justifiaient par « `cpu[0].cpus` est NUL », ce que
  cette étape rend faux. Ce sont désormais des `DEVIATION:` déclarées : TEMPORAIRE (étape
  6) pour la première — au-delà de 8 MHz le préfetch garde le coût de la ROM après un
  reset —, un levier à part pour la seconde — PCem pose `EDX = edx_reset` (0) au retour
  du mode protégé, ce dépôt garde DX, des deux côtés.
- **L'aide et trois commentaires décrivaient `cpus_ibmat`** alors que l'ibmat, sous
  T-ibmat, accepte les sept indices de l'ami286. Marqués.
- **Un indice hors table sortait en 1**, le code d'un amorçage raté, là où `--model` ou
  `--ram` refusés sortent en 2. `check_cpu` est appelé avant `initpc` dans les trois
  chemins du programme. L'écran de construction ramène l'indice aussi au CHARGEMENT
  d'une machine, et ajoute son message à celui de la mémoire au lieu de l'écraser.
- Citations corrigées (`cpu.h`, `cpu.c`, plages de `oracle.tsv`), commentaires périmés de
  `386_ops_fpu.cs`, `mem.cs`, `AtProbe.cs` et `BootDiff.cs` réécrits.

### Étape 4 : le levier A — le budget suit rspeed

Une ligne de chaque côté : `cpu_get_speed()` et `cpu_set_turbo()` deviennent verbatim
(`cpu.c:2050-2071`), et l'oracle retire les deux enveloppes (`Makefile`, `WRAP_BOTH`).
`h_runpc` et `runpc` lisent désormais le budget de PCem — `cpu_turbo ? cpu_turbo_speed :
cpu_nonturbo_speed`, que `cpu_set()` pose — et `resetpchard` finit par le `cpu_set_turbo(1)`
de `pc.c:439`, inerte.

**Prédictions écrites avant la mesure, et tenues :**

| Contrôle | Prédit | Mesuré |
|---|---|---|
| `--timer-check --model ibmat`, rapport 1 | 6 000 000 /s | **6 000 000,000** |
| rapport 3 | 18,2065 Hz ± 33 ppm | 18,206966 (+24,9 ppm) |
| rapport 5, fréquence vue par l'invité | 6,000 MHz | **5 999 850,7 Hz** (−24,9 ppm) |
| rapport 6, temps invité / contractuel | 1,0000 | **1,000025** |
| cycles jamais portés au tsc | 0 | 0 sur 1 800 300 000 |
| même chose, `--model ami286` | idem | 6 000 000,07 ; 18,206966 ; 5 999 850,8 ; 1,000025 ; 0 |
| `--boot roms 1000 --model ibmat` | entre les comptes à 1 257 et 1 258 tranches de l'ancien budget : [9 931 862 ; 9 938 230] | **9 932 822**, tsc 60 000 003 |
| `--boot roms 1000 --model ami286` | [8 091 512 ; 8 097 243] | **8 092 369**, tsc 59 999 825 |
| `boot-diff roms 6000 --cpu 3` (8088/10, 100 000 cycles) | vert, compte ≠ 25 457 269 | **vert, 52 936 819** |
| `cpu-config-check` (et `--inverse`) | budget = rspeed / 100 | 47 727 … 160 000 (8088), 60 000 … 250 000 (286), vert |
| ibmpc, ibmxt | strictement inchangés | les cinq boot-diff, `--timer-check`, `vga-probe` identiques |

`boot-diff --cpu 3` est LA porte de ce levier : c'est le seul diff qui va jusqu'au contrôle
de longueur final avec un budget changé — l'AT diverge avant, sur le CMOS — et il exerce un
`xt_cpu_multi` non entier (14 318 184 / 10 000 000). Un budget asymétrique y sortirait en
écart de longueur, et l'empreinte CPU l'aurait nommé avant.

Le bilan `--verbose` de l'ami286 dit désormais « 1 seconde invitée par tranche de 10 ms » :
**sous le frein, le 286 tient le temps réel à 6 MHz**. Porte, au chiffre près : build
Release 0 avertissement, build Diff, make oracle + selftest + bench, check-oracle 0 dérive,
abi 18, fuzz 8088 et 286, core286-check et SST identiques, les cinq boot-diff, `--setup-check`.
Ce qui bouge, et devait bouger : les comptes `--boot` et les écrans `vga-probe` de l'AT
(atteints plus tôt), les tranches des scripts `--type-at` de l'AT (à diviser par 1,257).

### Étape 5 : le levier B — l'IBM AT rendu à cpus_ibmat

`m_ibmat` pointe sur `cpus_ibmat` (`model.c:1109`) des deux côtés : 286/6 et 286/8, à trois
cycles de lecture et d'écriture mémoire. L'AT de ce dépôt avait, depuis B2, les deux
cycles de `cpus_286[0]` — l'entrée de l'ami286 —, symétriquement, donc sans qu'aucune
porte ne le voie. Levier accepté par l'utilisateur sur les chiffres de l'étape 4.

| Contrôle | Prédit | Mesuré |
|---|---|---|
| `cpu-config-check` (et `--inverse`) | 21 identiques : ibmat 286/6 et 286/8 à lecture/écriture 3/6, préfetch mémoire et ROM 3 ; `cpu = 2` refusé | **21 identiques, 4 refus** |
| `--timer-check --model ibmat` (300 s) | rapports 1, 3, 5, 6 inchangés ; CPI en hausse | rapport 6 **0,999992** ; **277 837 045** instructions sur la fenêtre contre 383 287 727 ; CPI 6,48 |
| `--boot roms 1000 --model ibmat` | moins que 9 932 822 instructions, tsc ≈ 60 000 000 | **8 170 432**, tsc 60 000 034 |
| `boot-diff --model ibmat` | empreinte identique ; divergence **toujours à l'instruction 40** | empreinte identique (mem 3, 3/6) ; divergence à l'instruction **50** |
| ami286, ibmpc, ibmxt, fuzz, core286-check, SST | strictement inchangés | identiques |

**Une prédiction manquée, et pourquoi.** La divergence de l'ibmat est la même — `IN AL,71h`
en `F000:0169`, l'oracle lit `0xFF` (CMOS sans fichier), le C# `0x10` (`nvr/.at.nvr`, sha256
inchangé) — mais le chemin qui y mène compte dix instructions de plus. Seul le temps a
changé, donc ce chemin en dépend : une attente avant la lecture du CMOS, dont le nombre de
tours suit la durée des instructions. Le mécanisme exact n'a pas été tracé. Les deux côtés
restent identiques jusque-là, instruction par instruction.

Bougent comme prévu : `at-probe` (oracle seul, ROM_IBMAT), les écrans `vga-probe` et les
comptes `--boot` de l'ibmat. Portes, au chiffre près : build Release 0 avertissement, build
Diff, make oracle + selftest, check-oracle 0 dérive, abi 18, fuzz 8088 et 286, core286-check
et SST identiques, les cinq boot-diff, `boot-diff --cpu 3` à 52 936 819, `--setup-check`.

### Étape 6 : les resets passent par la table

`resetx86` et `softresetx86` appellent `cpu_update_waitstates()` (`808x.c:675-676`,
`:719-720`), des deux côtés : l'oracle retire sa dernière enveloppe de cette famille, et
`h_reset` comme `_808x.Reset()` / `_386.Reset286()` font tourner `cpu_set()` sur la machine
que leur cœur désigne (5150, ou ami286 à l'entrée poussée). `h_cpu_config_286/_8088` et
`cpu_config_286` — les recopies à la main de M0 à M16 — disparaissent. Au-delà de 8 MHz,
le préfetch retombe désormais au coût de la RAM à chaque reset, comme chez PCem ; c'est le
reset que le 8042 déclenche pour sortir du mode protégé. Levier accepté par l'utilisateur.

**Prédiction : aucune porte ne bouge à 8 MHz et moins.** Tenue, contre le binaire de
l'étape 5 : les cinq boot-diff, `boot-diff --cpu 3` (52 936 819), fuzz 8088 et 286,
core286-check, SST (forme par forme), `cpu-config-check` et `--inverse` (21, 4 refus),
`--timer-check` et `--boot` sur les quatre machines, `vga-probe` sur les quatre — tous
identiques ; `at-probe` ne diffère que par deux adresses.

**Gagné : le fuzz du 286 aux vitesses hautes.** `fuzz --core 286 --cpu N` pousse l'entrée
aux deux côtés, et le fuzzeur confronte désormais l'empreinte CPU après son premier reset —
un `--cpu` qui n'atteindrait qu'un côté fuzzerait deux machines. Verts sur 255 opcodes, 67
champs, cycles et tsc compris : `--cpu 5` (286/20 : lecture/écriture 4/8, isa 3, préfetch
ROM 20) et `--cpu 6` (286/25). C'est le premier témoin DIFFÉRENTIEL du temps d'exécution à
ces vitesses — mémoire et préfetch en RAM ; le préfetch en ROM, lui, n'est atteint que par
un amorçage, donc par aucun diff tant que NEAT manque à l'oracle.

### Étape 7 : l'ami286 en 286/20 et 286/25 — mesuré

Aucun code transcrit : des mesures, et une charge ajoutée à l'outil. Au repos, un BIOS
attend dans une boucle en ROM, où chaque mot coûte rspeed / 1e6 cycles au-delà de 8 MHz :
très peu d'instructions par seconde invitée, un hôte peu chargé, une marge flatteuse.
`--timer-check --charge ram` écrit donc en 0000:0600 une boucle sur registres
(`MOV CX,FFFF / DEC CX / JNZ / JMP`) et y fait sauter le 286, en mode réel, IF = 1 : l'INT 8
continue de compter les tops. C'est cette charge qui décide de la faisabilité.

Protocole : `--timer-check roms 300 --model ami286 --cpu N --charge ram`, `taskset -c 0-3`,
trois passages, CMOS figé (répertoire jetable). **Machine PAS au repos** — Rider ouvert,
charge moyenne 2,5 à 3,4 : les marges ci-dessous sont des MINORANTS.

| | 286/20 (`cpu = 5`) | 286/25 (`cpu = 6`) | Prédit |
|---|---|---|---|
| budget par tranche | 200 000 | 250 000 | idem |
| fréquence vue par l'invité (rapport 5) | **20 000 168,9 Hz** (+8,4 ppm) | **25 000 211,0 Hz** (+8,4 ppm) | 20,000 / 25,000 MHz ± 60 ppm |
| temps invité / contractuel (rapport 6) | 0,999992 | 0,999992 | 1,0000 |
| cycles jamais portés au tsc | 0 | 0 | 0 |
| trois passages | EMPREINTE identique au chiffre près | idem | — |
| temps hôte de la fenêtre de 300 s, min / max | 12,091 / 12,548 s | 13,845 / 14,354 s | — |
| **marge** (s invitées par s hôte) | **×24,8** | **×21,7** | > 1,1 |
| MIPS invité, CPI (charge ram) | 2,35 ; 8,5 | 2,94 ; 8,5 | — |

Au repos (60 s), la même machine fait 0,67 MIPS à 286/20, CPI 29,9 — le coût de la ROM ;
sous la charge ram, 2,35 MIPS. La marge tombe de ×34 à ×24 : c'est bien la charge qui
décide, et elle laisse plus de vingt fois le temps réel.

**Et sur la machine de l'utilisateur**, pas seulement l'ami286 par défaut (CGA, 640 Ko) :
`ixtal26-286.cfg` sans son disque — ami286, **4 096 Ko, VGA**, lecteurs 5 et 2 —, avec son
CMOS copié, même protocole. L'amorçage finit en mode réel (la charge ram le vérifie).

| | 286/20 | 286/25 |
|---|---|---|
| fréquence vue par l'invité | 20 000 168,8 Hz | 25 000 620,8 Hz (+24,8 ppm) |
| temps invité / contractuel | 0,999992 | 0,999975 |
| temps hôte de 300 s, min / max | 9,085 / 9,118 s | 10,854 / 10,991 s |
| **marge** | **×33,0** | **×27,4** |

Plus large qu'en CGA : sur cette machine, `cga_poll` coûte davantage à l'hôte que la VGA
(§ M5.1 en faisait déjà le premier poste). Ni l'une ni l'autre marge ne compte le coût de la
présentation SDL, que le titre de la fenêtre ne compte pas non plus.

### Conclusion

La question était : « mon 80286 tourne-t-il entre 20 et 25 MHz ? ». Au départ, **non** :
6,000 MHz vus par l'invité, 4,773 MHz fournis, son temps à 79,5 % du temps réel — le budget
du 8088, des deux côtés, invisible à toute porte. Désormais, sur l'ami286, `cpu = 5` donne
un 286 que l'invité mesure à **20,000 MHz** et `cpu = 6` à **25,000 MHz**, tenant le temps
réel avec une marge d'au moins ×21 sur cet hôte — ×27 sur la machine VGA à 4 Mo de
l'utilisateur. Ce qui le garantit :

- **la configuration** est vérifiée contre le VRAI `cpu_set()` de PCem, entrée par entrée
  (`cpu-config-check`, 21 configurations, dans les deux ordres) ;
- **le temps d'exécution** à 20 et 25 MHz est vérifié en différentiel par le fuzz du 286
  (`--cpu 5`, `--cpu 6`, 255 opcodes, cycles et tsc compris), empreinte confrontée ;
- **le budget** est confronté à chaque boot-diff par l'empreinte CPU, et sa porte
  discriminante est `boot-diff --cpu 3` (8088/10, vert à 52 936 819) ;
- **la cadence** est mesurée par `--timer-check` et affichée par le titre de la fenêtre.

**Ce que ce vert ne dit pas.** L'ami286 n'a aucun boot-diff : l'oracle n'a ni `neat.c` ni
`mfm_at.c`. Le préfetch en ROM à 20/25 cycles n'est donc exercé par aucun diff — seulement
par l'amorçage, et symétriquement. Restent aussi une DEVIATION déclarée (`cpu_set_edx` : DX
gardé au reset, là où PCem pose 0) et la vérification à l'œil du titre de la fenêtre sous le
frein (`invite 100 % - 20.00 MHz`), que seul un lancement fenêtré peut montrer.

## M17 — Un 286 complet : le CMOS fabriqué, et la machine qui démarre sur son disque

Parti d'une demande simple — *« fabrique un CMOS pour AMI 286 »* — après le constat de
`8a125cf` : le CMOS survivait à l'extinction, mais le POST se plaignait toujours de deux
choses, « CMOS memory size mismatch » et « CMOS display type mismatch ». Le SETUP en ROM
les corrigerait ; le piloter demande d'envoyer Suppr, les flèches et F10 à l'aveugle.

### La machine, telle que son propre POST l'imprime

```
  Main Processor     : 80286            Base Memory Size   : 640 KB
  Numeric Processor  : None             Ext. Memory Size   : 3072 KB
  Floppy Drive A:    : 1.44 MB, 3 "     Hard Disk C: Type  : 46
  Floppy Drive B:    : 1.2 MB, 5 "      Hard Disk D: Type  : None
  Display Type       : VGA or EGA       Serial Port(s)     : None
  ROM-BIOS Date      : 10/15/90         Parallel Port(s)   : None
```

Aucune plainte CMOS, aucun 161, aucun 162.

### La somme de contrôle, établie par trois voies

Elle n'est écrite nulle part dans PCem : elle vit dans les BIOS.

> `somme = Σ octets[0x10 … 0x2D]` sur 16 bits · `[0x2E]` poids **fort**, `[0x2F]` poids
> faible · **une somme nulle est rejetée**.

| voie | où |
|---|---|
| Les fichiers livrés | `default/ami286.nvr` → 0x0AB6 · `default/at.nvr` → 0x00E5, tous deux concordants |
| IBM AT, vérification | `62x0820`+`62x0821` entrelacés, `0x06fe-0x0727` |
| AMI 286, **écriture** | `amic206.bin:0xacd0-0xad11` — le SETUP lui-même |

La troisième est la plus utile : `--make-nvr` ne fait rien d'autre que ce qu'elle fait.

### Trois choses que la mesure a corrigées

**1. `0x30/0x31` est en kilo-octets.** Ma lecture du désassemblage en faisait des unités
de 64 Ko. Un amorçage à 4 096 Ko laisse `00 0C`, soit 3 072 exactement — `mem_size −
1024`, pas son quotient. Le test de `0x8e85` est donc « déclaré ≠ trouvé », et c'est ce
qui explique la plainte des fichiers de PCem : `ami286.nvr` déclare 1 024 Ko d'étendue, et
sur une machine qui n'en a pas le POST réécrit `0x30/0x31` à zéro.

**2. Les deux sites de test de l'affichage s'excluent.** `0x9716` ne s'applique que si le
vecteur d'INT 10h a quitté F000 — carte à ROM d'extension — et **exige** alors `00` ;
`0x97dd` vaut pour une vidéo de carte mère et **refuse** `00`. Une VGA se déclare donc
`00`, qui est déjà la valeur livrée par PCem ; une CGA doit dire `20`.

**3. Un désaccord CMOS/lecteur n'est pas cosmétique.** Déclarer A: en 1,44 Mo pendant que
PCem monte un 1,2 Mo fait programmer le contrôleur pour de la haute densité 3,5" : la
disquette de 180 Ko devient illisible, « DISKETTE BOOT FAILURE », lecteur bon et image
intacte. D'où un CMOS par configuration.

### Le point fixe

La preuve la plus forte disponible ici, faute d'oracle : la région de configuration
`0x10-0x3F` est **identique octet pour octet** avant et après l'amorçage, et `0x0E` reste
à zéro. Le BIOS n'a rien trouvé à corriger. Vérifié sur trois amorçages successifs, dont
un avec frappe.

### Trois plafonds successifs sur le disque dur, chacun mesuré

| étape | taille | ce qui plafonne |
|---|---:|---|
| Le fichier image | **152,4 Mio** | type 46 de la table du BIOS, 1224 × 15 × 17, lu dans `amic206.bin:0xE6D1` |
| Ce que l'INT 13h expose | **127,5 Mio** | `AH=08h` fait `sub ax,2` puis écrête les cylindres à 0x3FF (`0xA331-0xA33C`) |
| Ce que FDISK partitionne | **127,5 Mio** | 261 119 secteurs, fin CHS `1023/14/17` — il prend tout ce que le BIOS décrit |
| Ce que PC DOS 2.00 formate | **31,5 Mio** | 64 511 secteurs, 16 secteurs par cluster, 4 031 clusters — juste sous la limite des 4 085 du FAT12 |

Les deux cents derniers cylindres du disque ne sont donc atteignables par rien.

### L'amorçage depuis C:

```
C>DIR
 Volume in drive C has no label
 Directory of  C:\
COMMAND  COM    17664   3-08-83  12:00p
        1 File(s)  32923648 bytes free
```

Chaîne complète : `--make-nvr` de la configuration d'installation, amorçage sur la
disquette, `FDISK`, création de partition, redémarrage automatique, `FORMAT C: /S`, puis
`--make-nvr` de la configuration finale et amorçage sur le disque.

### Ce qui n'a PAS d'oracle, et pourquoi

L'oracle ne lit pas le même CMOS : ses `nvr_path`, `nvr_default_path` et `config_name`
sont trois globales de `.bss` jamais affectées (`harness_stubs.c:683-685`), donc il prend
toujours la branche « pas de fichier ». **Aucun `boot-diff` de classe AT n'est possible** ;
la campagne VGA de M15 l'a constaté indépendamment. La vérification est donc côté C#
seul — assumée, pas subie —, comme les écritures disque de M6.

### Le risque nommé qui ne s'est pas réalisé

Compter 3 Mo de mémoire haute fait passer le POST en mode protégé et en revenir par
l'octet `0x0F` du CMOS et un reset du 8042 : le territoire du 104 non résolu, avec
`loadcscall` et `taskswitch286` encore en `fatal()`. Aucun n'est tombé.

### Un écart observé une fois, non reproduit

Un amorçage a lu `0x10 = 0x22` alors que `--make-nvr` venait d'écrire `0x42` dans le même
enchaînement de commandes. Deux reproductions ultérieures, avec et sans frappe, ont rendu
`0x42` à l'aller comme au retour. **La cause n'est pas connue et n'est pas inventée ici.**

## M19 — Les deux Trident, 9000B puis 8900D, sous oracle sur le 5150 et le XT

`vid_tvga.c` (417 lignes) et `vid_tkd8001_ramdac.c` (62) transcrits en entier ; la 9000B
enregistrée d'abord, la 8900D une fois ses rendus 15/16/24 bpp écrits. Branche `m19-tvga`,
worktree dédié. Portes réduites par étape (boot-diffs et sonde) à la demande de Julien ;
SST et fuzz au dernier commit.

### Étape 1 : la temporisation par carte

La branche `video_speed == -1` de `video_updatetiming` (`video.c:599-735`) est celle que
PCem prend réellement (`pc.c:665`). Transcrite des deux côtés, elle ne change **rien** pour
la CGA et la VGA — prédit et mesuré à l'unité sur les dix boot-diffs de référence. Pour
les Trident, lu sur l'oracle à l'étape 2 : bus 8 bits, 8900D lecture 8/8/12 écriture 3/3/6,
9000B 7/7/12 ; bus 16 bits (`ibmat`), `_l = 2 × _w`. Le C# rend les mêmes (empreinte CPU
confrontée, « vidéo 7/7/12 »).

### Étape 2 : l'oracle d'abord — les BIOS tournent sur un 8088

`vga-probe --gfxcard`, oracle seul, 6 000 tranches, sur `ibmpc` et `ibmxt` :

| Carte | INT 10h en fin de course | Mode posé | Invite |
|---|---|---|---|
| 9000B (`D3.0`, 11/12/91) | C000:14C8 | 3, hdisp 720, 449 lignes | BASIC `Ok` |
| 8900D (`C4.3`, 07/12/93) | C000:1398 | 3, hdisp 720, 449 lignes | BASIC `Ok` |

Les deux points d'entrée sont ceux lus dans les ROM avant tout amorçage : la ROM a tourné
et rendu la main. Aucun opcode 186+ sur les chemins d'init et d'INT 10h (désassemblage
récursif), confirmé par l'exécution. `video_is_ega_vga` de l'oracle lit désormais les
drapeaux de la carte : `gfxcard == GFX_VGA` aurait fait diverger le PPI du XT avant la
première instruction.

### Étapes 3 et 7 : les boot-diffs des deux cartes, verts du premier coup

| Machine | 9000B | 8900D |
|---|---:|---:|
| 5150, ROM BASIC | 25 252 602 | 25 261 150 |
| 5150, PC DOS 2.00 | 26 914 975 | 26 920 563 |
| XT, ROM BASIC | 23 330 506 | 23 346 138 |
| XT, PC DOS 2.00 | 22 411 492 | 22 419 149 |

Sonde 86/86 partout (64 champs de M15, onze génériques — banques, `bpp`, masques,
`rowoffset`, `ma_latch`, entrelacé, `clock` en bits —, onze de la `tvga_t` et du RAMDAC).
Les dix nombres de référence (CGA et VGA) inchangés à chaque étape.

### Étape 4 : la campagne DEBUG de la 9000B, et le défaut qu'elle a trouvé HORS de la Trident

201 lignes tapées sous DEBUG (XT, PC DOS 2.00, B: = `pcdos20s.img`) : pokes directs en mode
texte (SR0B lu et écrit, GDC0F, 3D8/3D9 ouverts et fermés, SR0E et GDC0E en new mode,
SR0D/SR0E/SR0C en old mode, 3DB), puis un programme en 100 lancé pour douze modes
(`r ax` avant chaque `g`) : 5Dh, 5Ch, 5Eh, 62h, 5Bh, 5Fh, 50h, 53h, 55h, 57h, 5Ah, 3. Il
remplit chaque banque par SR0E, copie une banque dans une autre banques séparées, passe
GDC6 à la carte 0, pose l'entrelacé, puis oldctrl2 bit 4 et un recalcul provoqué par CR13.
Aucune paire index/donnée n'est tapée en mode graphique ; aucun caractère non mappé.

**Premier passage : rouge à l'instruction 229 190 925**, rejoué SEUL au même indice. La
phase 2 ne rejoue pas `--type` et rendait « les états concordent » — inutilisable. D'où
`boot-diff --lockstep N` : les deux côtés au pas commun, tranche par tranche, frappe
comprise, état CPU à chaque tranche, sonde VGA toutes les N. Il a nommé l'écart :

```
PREMIER ÉCART en fin de tranche 87160, pendant la ligne tapée 174 « g » (mode 50h)
  CPU : cycles : oracle -327686, C# -786436
```

14 cycles de plus par mot écrit côté C# dans le `rep stosw` vers A000, en mode texte :
deux octets à 7 cycles, la temporisation de la carte. **La cause est dans `Memory/mem.cs`,
pas dans la Trident** : `mem_mapping_set_addr` omettait `mapping->enable = 0` avant de
recalculer l'ancienne plage, et `= 1` avant la nouvelle (`mem.c:1194`, `:1198`). Sans eux,
le recalcul de l'ANCIENNE plage retrouvait la carte encore active et la re-mappait : après
un passage de 128 Ko en A0000 à 32 Ko en B8000, A0000-B7FFF restait routé vers la SVGA côté
C#, sans mappage côté PCem. Présent depuis M2 (`62ae24f`), invisible tant qu'aucune
campagne n'écrivait en A000 après un retour en mode texte. Corrigé : **258 890 009
instructions identiques, sonde 86/86, 68 137 trames**, et les dix boot-diffs de référence
ainsi que les huit Trident inchangés.

### Étape 7 : la campagne de la 8900D

La même, étendue à 271 lignes : six modes hi-color du BIOS (74h, 75h, 76h, 6Bh, 6Ch, 7Eh),
puis le TKD8001 piloté à la main — quatre lectures de 3C6 sans toucher 3C7-3C9, écriture de
E0, A0 puis C0 (16, 15, 24 bpp) sur le mode 5Dh, et un recalcul provoqué par CR13, sans
lequel le rendu ne change pas. **Verte du premier coup : 339 586 475 instructions, sonde
86/86, 154 069 trames**, empreinte « vidéo 8/8/12 » (lecture) des deux côtés.

### Les injections de panne (copies jetables, oracle intact)

Campagne courte sous DEBUG, sans relecture des registres touchés :

| Faute plantée | Diff d'instructions | Sonde |
|---|---|---|
| (témoin, 9000B) | vert, 35 912 517 | 86/86 |
| F1 — GDC0E perd le bit 3 de `tvga_3d9` | vert | **rouge** : `read_bank`, `tvga.3d9` |
| F2 — `read_bank` suit toujours `write_bank` | rouge à 33 446 278 | — |
| F3 — `oldctrl2` jamais stocké en old mode | rouge à 1 161 740 (POST) | — |
| F4 — fréquence de clksel 2 fausse (44,0 au lieu de 44,9 MHz) | vert | **rouge** : `clock(bits)` et 14 champs de balayage |
| F5 — le TKD8001 ne compte plus les lectures de 3C6 (8900D) | rouge à 1 174 898 (POST) | — |

F4 n'était pas vu au premier essai : en mode 3, clksel vaut 1, qui n'a pas de cas dans la
table de la Trident. Un `o 3c2 6b` (clksel 2) l'a rendu observable.

### La couverture, comptée (copie jetable, C# seul, même campagne)

| | 9000B | 8900D |
|---|---|---|
| banques d'écriture | 0 à 7 | 0 à 15 |
| lecture séparée de l'écriture | w1/r3, w0/r5, w7/r3… | w15/r3, w15/r0… |
| `banked_mask` 0x1ffff | 12 | 21 |
| entrelacé / oldctrl2 bit 4 | 33 / 27 | 61 / 379 |
| clksel atteints | 0-7, C, E | 0-7, C, E, F |
| 3DB écrit | 1 | 25 (par le BIOS) |
| TKD8001 : bpp 15 / 24 / 16 | — | 4 / 4 / 4 |
| rendus | text_80, blank, 4bpp_highres, 8bpp_lowres, 8bpp_highres | + 15bpp_lowres/highres, 16bpp_highres, 24bpp_lowres/highres |

**Jamais atteints** : `16bpp_lowres` (le chemin de PB-38), `2bpp_*`, `32bpp_*`, le chemin
`fast`, le curseur matériel.

### Ce que la campagne a montré de PCem

`high_res_256` se déclenche en mode texte sur la 9000B : 33 recalculs avec le rendu
`text_80` actif, que `tvga_recalctimings` remplace alors par `svga_render_8bpp_highres`
(`vid_tvga.c:320-330`, sans garde de mode graphique). Transcrit tel quel ; non encore
inscrit au registre des défauts faute d'avoir regardé l'écran produit.

### Le 286, en `--boot` seulement (C# seul, répertoire jetable)

Le correctif de `mem_mapping_set_addr` touche toute machine SVGA, y compris l'ami286 que rien
ne diffe. `ixtal26-286.cfg` tel que Julien l'a (`cpu = 4`, VGA, 4 Mo, disque type 46),
copies du CMOS et du disque dans `/tmp`, binaire de l'étape 7, 20 000 tranches : POST sans
plainte, HIMEM installé, `C:\>`. Deux amorçages : `0x0E = 00`, région `0x10-0x3F`
identique. Puis la même machine avec `--gfxcard tvga9000b` et `tvga8900d` : même écran de
configuration AMI (« Display Type : VGA or EGA »), même `C:\>`, CMOS inchangé — l'octet
d'affichage reste `00`, la ROM étant en C000. Aucun `nvr` à régénérer. La fenêtre à
2 048 lignes (étape 5) n'a pas été regardée : c'est à Julien de le faire.

## M20 — Le mode protégé du 286 pour Windows 3.11 en mode standard

L'installation de Windows 3.11 sur l'ami286 de Julien s'arrêtait à la 3e disquette, quand
SETUP quitte DOS pour lancer Windows en mode standard (DOSX, KRNL286) :
`fatal: loadcscall (seg 00CB)`. Le bloc C avait laissé en `fatal()` ce que le POST et DOS
n'atteignaient pas.

### L'oracle : `pm-check`, l'état de mode protégé construit par LOADALL

L'ami286 n'a pas de boot-diff et le fuzzeur ne part qu'en mode réel ; mais l'oracle lie
`x86seg.c` en entier. `pm-check` écrit les mêmes tables des deux côtés (GDT de 26 entrées,
LDT, IDT de 32 portes, deux TSS 286, piles), entre en mode protégé par `LOADALL` (0F 05)
depuis la table en 0x800, puis exécute pas à pas : LOADALL, l'instruction testée, trois
NOP. Après chaque pas : état complet, journal d'écritures, hachage de la RAM entière.
**Un processus par cas** : ni `h_reset`, ni `Reset286`, ni `LOADALL` ne remettent tout —
mesuré, un CS de limite 0xFF chargé par un cas restait dans `limit_raw` des suivants côté
oracle, et un `fatal()` au milieu de `CALL_FAR_w` laissait `optype = CALL` côté C#.

| Étape | Commit | pm-check (58 cas) |
|---|---|---|
| 0 — le banc, avant tout C# | `6d4c01f` | 13 verts (le bloc C, rétroactivement), 0 rouge, 44 arrêts nommés |
| 1 — `loadcscall` (`x86seg.c:864-1318`) | `3790167` | 34 verts, 0 rouge |
| 2 — `LAR`, `LSL` (`x86_ops_pmode.h:56-172`) | `2fe4d08` | 54 verts, 0 rouge |
| 3 — double faute (`386.c:206-216`) | `3642778` | 56 verts, 0 rouge, 2 arrêts (`taskswitch286`) |

Les cas `loadcscall`, tous verts du premier coup : même privilège, conforme depuis CPL0 et
CPL3, `FF /3`, GDT index 25 (le `00CB` de Windows : TI = 0, donc la GDT — le banc l'avait
d'abord cru dans la LDT et rendait un #GP, juste, des deux côtés), LDT, porte d'appel même
privilège, porte CPL3 → CPL0 avec bascule de pile par la TSS et copie de 0, 1, 5 et 31
paramètres, #GP (nul, hors GDT, données, DPL, limite), #NP, #TS.

### Ce que PCem fait, et que le banc montre sans le juger

Identique des deux côtés, donc transcrit, mais pas le 286 réel :
- un `CALL FAR` vers un segment de DONNÉES le charge comme CS (`0060:0004`) ;
- un `CALL FAR` vers une vraie porte de tâche (type 5) rend #GP : `loadcscall` ne traite
  comme tâche que les types 1 et 9 — ceux d'un TSS ;
- ni la limite du segment de code ni celle de la nouvelle pile ne sont vérifiées à l'appel ;
- une faute depuis CPL3 vers une porte DPL0 est livrée sans bascule de pile, CS = `000B` ;
- `LAR`/`LSL` sur sélecteur nul rendent avant `CLOCK_CYCLES` : coût nul, le pas avale
  l'instruction suivante.

### Le second arrêt : le groupe `0F 00` s'exécutait tout entier en SLDT

Julien a relancé l'installation : `x86abort : Bigger than GDT limit 2052 011F CSC` — que
PCem aurait fait aussi (`x86abort` = `exit`). Une porte CPL3 → 0 lisait sa pile interne dans
une « TSS » en `00C000` : du texte (« EXPANDED MEMORY MANAGER »). TR n'avait jamais été
chargé.

Pour ne plus dépendre d'allers-retours, `--boot` a appris à dérouler une installation :
`@A:image` change la disquette comme le menu Ctrl+F12, `@A:` l'éjecte, `@wait N` laisse
passer N tranches. L'installation, rejouée sur une COPIE du disque de Julien (clavier
AZERTY : `q.setup` pour `a:setup`, `zin` pour `win`), a reproduit l'arrêt. Un journal
jetable a montré DOSX exécutant `0F 00 D0` (`LLDT AX`) puis `0F 00 DE` (`LTR SI`) —
décodés /2 et /3 par `cpu_reg`, mais exécutés comme `SLDT`.

Cause : chez PCem, `x86.h:197` fait `#define fetchdat rmdat`, donc dans `op0F00_common`
`rmdat` est le PARAMÈTRE. Le C# nommait ce paramètre `fetchdat` et lisait la GLOBALE
`rmdat`, que seule la boucle d'`exec386` pose : derrière l'échappement `0F` elle commence un
octet trop tôt. Invisible depuis le jalon 286 : le boot-diff de l'AT diverge à
l'instruction 50 (CMOS), et `pm-check` n'avait aucun cas `0F 00`. Dix cas ajoutés AVANT la
correction : 9 rouges, puis verts. **66 verts, 0 rouge.**

### Windows 3.1, installé et démarré

Même installation rejouée de bout en bout, C# seul, depuis une copie : sept disquettes,
nom, « Windows 3.1 is now set up », disquette éjectée, redémarrage, `WIN` — le
Gestionnaire de programmes s'ouvre en 640 × 480, mode standard, sur l'ami286 (286/16,
4 Mo, Trident 8900D). Aucun `fatal()` de bout en bout.

### Ce qui reste

`taskswitch286` (`x86seg.c:2393-2849`, 356 lignes) : atteint par `CALL`/`JMP` vers une
TSS, pas transcrit — Windows 3.1 en mode standard ne l'a pas atteint.

## M21 — COM1, COM2 et la souris série Microsoft

`serial.c`, `mouse_serial.c` et `mouse.c` (registre réduit à la souris Microsoft,
`mouse_type = 0`) transcrits, oracle d'abord (ABI 20, `h_mouse_poll`). Les boot-diffs
changent de chiffres — le POST sonde COM1 et COM2 — et restent verts des deux côtés,
sonde VGA 86/86 comprise (chiffres dans le message de `adfc7a9`). Test ciblé sous oracle,
XT + DEBUG : BDA `0040:0000` = `03F8 02F8` ; RTS levé → LSR `61`, `3F8` rend `4D` (« M »),
puis LSR `60` — vert, 30 450 207 instructions. Windows 3.1 réinstallé sur une copie du
disque de Julien (ami286, Trident 8900D) : SETUP détecte la souris, le curseur apparaît
dans le Gestionnaire de programmes. Le déplacement réel ne se vérifie qu'à la main, dans
la fenêtre : un clic capture la souris, Ctrl+Fin la libère.

## G2 — Le cœur 386 : les écarts SST 386 entre PCem et le silicium

Corpus `SingleStepTests/80386`, `v1_ex_real_mode`, relevé sur un **386EX** (mode réel
seulement, cycles non comparés — voir `PLAN-386.md`). Ligne de base :
`sst386-baseline.tsv`, colonne `passe` de l'**oracle** (PCem), régénérée sur la carte de
16 Mo en D2. Le critère n'est pas 100 % : c'est que le C# reproduise cette colonne à
l'identique. **Il la reproduit sur les 941 formes** depuis D3, et à chaque série complète
de D4 à D7 (`667071f`).

| | |
|---|---:|
| formes | **941** |
| formes à 100 % | 199 |
| formes déviantes | **742** |
| cas joués / réussis | 1 758 699 / 1 474 250 (**83,83 %**) |
| échecs | **284 449** |

### Méthode du classement

Pour chaque cas échoué, le **premier champ divergent** dans l'ordre de comparaison de
`Sst386Probe` (cr0, cr3, EAX…EBP, ESP, CS…SS, EIP, EFLAGS, DR6, DR7, puis la RAM). Relevé
le 28/09 par une instrumentation **temporaire** de la sonde (non commitée, retirée) :
les 284 449 échecs recensés égalent exactement 1 758 699 − 1 474 250. Le premier échec de
chaque forme se rejoue avec l'outil commité : `iXtal26.Diff sst386-probe --op XX`.

Deux signatures suffisent à classer l'essentiel :
- **ESP obtenu = ESP attendu + 6** : le silicium a empilé FLAGS, CS et IP — une interruption
  de mode réel — et PCem non. C'est une **exception que PCem ne lève pas**. 91 556 cas.
- **Seul EFLAGS diffère, sur les six drapeaux arithmétiques.** 104 650 cas. En majorité
  des drapeaux qu'Intel documente comme **indéfinis** — AF et OF de SHLD/SHRD (compte > 1),
  OF de BT/BTS/BTR/BTC, tout sauf ZF de BSF/BSR, PF/ZF/SF d'IMUL — et que le 386EX pose
  autrement que PCem. **Mais pas tous** : le CF de BT* (≈ 1 150 à 1 350 cas par forme), l'AF
  d'ADC (2 370 cas) et le SF/ZF d'AAD/AAM sont **définis**. Ceux-là sont de vrais écarts de
  PCem, à instruire ; l'AF d'ADC rappelle PB-01 (la retenue entrante oubliée dans AF, sur
  le 8088) — non vérifié dans le C du cœur 386.

### Les familles

| Famille | Échecs | Formes dont c'est la cause dominante | Ce qui diverge |
|---|---:|---:|---|
| **E2** exception : limite de segment, adressage 32 bits | 75 858 | 194 | formes `67…` : une adresse effective 32 bits au-delà de 0xFFFF lève #GP/#SS en mode réel sur le 386 ; PCem ne teste pas la limite |
| **E1** exception : `LOCK` illégal | 13 860 | 125 | `LOCK` sur une instruction non verrouillable ou à opérande registre : #UD (INT 6) sur le silicium, ignoré par PCem |
| **E3** exception : accès à cheval sur 0xFFFF | 1 838 | 48 | un mot ou double mot à l'offset 0xFFFF (0xFFFD) : #GP sur le silicium — ex. `add [ds:bx],bx`, BX = FFFF |
| **F1** EFLAGS : drapeaux arithmétiques | 104 650 | 81 | SHLD/SHRD (AF, AF+OF), BT/BTS/BTR/BTC (OF, CF), BSF/BSR (tous), IMUL (PF, ZF, SF), ADC (AF), AAD/AAM (SF, ZF) |
| **F2** EFLAGS : bits 18-31 | 3 228 | 2 | IRETD et POPFD : bits hauts d'EFLAGS |
| **R** registres | 79 187 | 285 | ECX sous `67`/`rep`/`repne` (a32), ESP sur `66`/POPAD/ENTER, EAX sur MUL/IMUL, BOUND et `lock` sur CS, et d'autres ; ESP hors de la signature +6 |
| **M** mémoire | 5 828 | 7 | BTC/BTS en mémoire, PUSHFD, DIV/IDIV |

La signature E (ESP + 6) est mesurée ; la **cause** attribuée à E1, E2 et E3 est
**déduite** des instructions en échec (`lock …`, formes `67…` à adresse au-delà de 0xFFFF,
accès à l'offset 0xFFFF), non lue dans le C. Les sept familles somment exactement les
284 449 échecs.

Les sous-familles R et M ne sont **pas instruites** : le premier champ divergent y nomme le
registre, pas la cause. Elles sont le prochain chantier de ce recensement, forme par forme,
à la ligne de C.

### Ce que ces écarts sont, et ne sont pas

Ce sont des **écarts de PCem**, reproduits par le C# comme le veut la règle R8 — pas des
défauts de la transcription. Ils ne sont **pas** inscrits un par un dans `PCEM_BUGS.md`, qui
ne garde que ce qui a été lu à la ligne de C ; l'absence de contrôle de limite en mode réel
(E2, E3) et l'indifférence au `LOCK` illégal (E1) sont mesurées ici, pas encore instruites
dans `386_common.h` et `386.c`.

### Les formes déviantes, par famille dominante

Taux de réussite de l'oracle (et du C#) par forme, d'après `sst386-baseline.tsv`.

**E1 exception : LOCK illégal (#UD sur le silicium)** — 125 forme(s) : `08` 99.5 %, `0A` 97.3 %, `0F06` 97.0 %, `0F90` 96.2 %, `0F91` 96.2 %, `0F92` 96.4 %, `0F93` 96.2 %, `0F94` 96.2 %, `0F95` 96.2 %, `0F96` 96.4 %, `0F97` 96.2 %, `0F98` 96.2 %, `0F99` 96.2 %, `0F9A` 96.4 %, `0F9B` 96.4 %, `0F9C` 96.2 %, `0F9D` 96.6 %, `0F9E` 96.4 %, `0F9F` 96.2 %, `38` 97.3 %, `39` 96.9 %, `3A` 97.2 %, `3B` 96.9 %, `6639` 96.8 %, `663B` 96.8 %, `6681.7` 96.7 %, `6683.7` 96.7 %, `6685` 96.7 %, `6689` 97.3 %, `66A3` 97.6 %, `66C1.0` 96.7 %, `66C1.1` 96.7 %, `66C1.2` 96.7 %, `66C1.3` 96.7 %, `66C1.4` 96.7 %, `66C1.5` 96.7 %, `66C1.6` 96.7 %, `66C1.7` 96.7 %, `66C7` 97.1 %, `66D1.0` 96.8 %, `66D1.1` 96.8 %, `66D1.2` 96.8 %, `66D1.3` 96.8 %, `66D1.4` 96.7 %, `66D1.5` 96.7 %, `66D1.6` 96.7 %, `66D1.7` 96.7 %, `66D3.0` 96.8 %, `66D3.1` 96.8 %, `66D3.2` 96.8 %, `66D3.3` 96.8 %, `66D3.4` 96.8 %, `66D3.5` 96.8 %, `66D3.6` 96.8 %, `66D3.7` 96.8 %, `66F7.0` 96.8 %, `66F7.1` 96.8 %, `6766A3` 98.7 %, `67A2` 98.1 %, `67A3` 97.9 %, `80.7` 97.2 %, `81.7` 96.8 %, `82.7` 97.2 %, `83.7` 96.8 %, `84` 97.2 %, `85` 96.8 %, `88` 97.4 %, `89` 97.4 %, `A2` 97.6 %, `A3` 97.6 %, `C0.0` 97.3 %, `C0.1` 97.3 %, `C0.2` 97.3 %, `C0.3` 97.3 %, `C0.4` 97.3 %, `C0.5` 97.3 %, `C0.6` 97.3 %, `C0.7` 97.3 %, `C1.0` 96.8 %, `C1.1` 96.8 %, `C1.2` 96.8 %, `C1.3` 96.8 %, `C1.4` 96.8 %, `C1.5` 96.8 %, `C1.6` 96.8 %, `C1.7` 96.8 %, `C6` 97.7 %, `C7` 97.2 %, `CE` 98.6 %, `D0.0` 97.3 %, `D0.1` 97.3 %, `D0.2` 97.3 %, `D0.3` 97.3 %, `D0.4` 97.4 %, `D0.5` 97.4 %, `D0.6` 97.4 %, `D0.7` 97.4 %, `D1.0` 96.9 %, `D1.1` 96.9 %, `D1.2` 96.9 %, `D1.3` 96.9 %, `D1.4` 96.8 %, `D1.5` 96.8 %, `D1.6` 96.8 %, `D1.7` 96.8 %, `D2.0` 97.4 %, `D2.1` 97.4 %, `D2.2` 97.4 %, `D2.3` 97.4 %, `D2.6` 97.4 %, `D2.7` 97.4 %, `D3.0` 96.9 %, `D3.1` 96.9 %, `D3.2` 96.9 %, `D3.3` 96.9 %, `D3.4` 96.9 %, `D3.5` 96.9 %, `D3.6` 96.9 %, `D3.7` 96.9 %, `F6.0` 97.3 %, `F6.1` 97.3 %, `F7.0` 96.9 %, `F7.1` 96.9 %, `FF.4` 96.8 %, `FF.5` 96.2 %

**E2 exception : limite de segment, adressage 32 bits** — 194 forme(s) : `6700` 83.1 %, `6701` 82.2 %, `6703` 80.8 %, `6708` 83.1 %, `6709` 82.2 %, `670A` 81.9 %, `670B` 81.0 %, `670F90` 81.4 %, `670F91` 81.2 %, `670F92` 82.0 %, `670F93` 81.8 %, `670F94` 81.0 %, `670F95` 81.0 %, `670F96` 81.2 %, `670F97` 81.2 %, `670F98` 81.2 %, `670F99` 81.2 %, `670F9A` 81.6 %, `670F9B` 81.4 %, `670F9C` 81.8 %, `670F9D` 82.0 %, `670F9E` 82.0 %, `670F9F` 81.6 %, `6710` 80.2 %, `6711` 78.4 %, `6718` 83.0 %, `6719` 82.1 %, `6720` 82.9 %, `6721` 82.1 %, `6728` 82.9 %, `6729` 82.2 %, `672B` 80.8 %, `6730` 82.9 %, `6731` 82.1 %, `6733` 80.7 %, `6738` 81.6 %, `6739` 80.9 %, `673A` 81.6 %, `673B` 80.7 %, `676601` 82.0 %, `676603` 80.8 %, `676609` 82.2 %, `67660B` 80.8 %, `676611` 77.2 %, `676619` 82.1 %, `676621` 82.1 %, `676629` 82.1 %, `67662B` 80.7 %, `676631` 81.9 %, `676633` 80.6 %, `676639` 80.8 %, `67663B` 80.7 %, `676681.0` 82.6 %, `676681.1` 82.6 %, `676681.2` 78.8 %, `676681.3` 82.6 %, `676681.4` 82.6 %, `676681.5` 82.6 %, `676681.6` 82.6 %, `676681.7` 81.2 %, `676683.0` 82.4 %, `676683.1` 82.4 %, `676683.2` 80.2 %, `676683.3` 82.4 %, `676683.4` 82.3 %, `676683.5` 82.4 %, `676683.6` 82.3 %, `676683.7` 80.9 %, `676685` 81.4 %, `67668C` 82.1 %, `67668E` 80.1 %, `6766C1.0` 81.7 %, `6766C1.1` 81.6 %, `6766C1.2` 81.6 %, `6766C1.3` 81.6 %, `6766C1.4` 81.6 %, `6766C1.5` 81.6 %, `6766C1.6` 81.6 %, `6766C1.7` 81.6 %, `6766C7` 82.9 %, `6766D1.0` 81.6 %, `6766D1.1` 81.6 %, `6766D1.2` 81.6 %, `6766D1.3` 81.6 %, `6766D1.4` 81.5 %, `6766D1.5` 81.5 %, `6766D1.6` 81.5 %, `6766D1.7` 81.5 %, `6766D3.0` 81.7 %, `6766D3.1` 81.6 %, `6766D3.2` 81.6 %, `6766D3.3` 81.6 %, `6766D3.4` 81.6 %, `6766D3.5` 81.6 %, `6766D3.6` 81.6 %, `6766D3.7` 81.7 %, `6766F7.0` 82.2 %, `6766F7.1` 82.0 %, `6766F7.2` 83.0 %, `6766F7.3` 83.0 %, `6780.0` 83.3 %, `6780.1` 83.4 %, `6780.2` 80.9 %, `6780.3` 83.3 %, `6780.4` 83.3 %, `6780.5` 83.2 %, `6780.6` 83.2 %, `6780.7` 82.0 %, `6781.0` 82.5 %, `6781.1` 82.6 %, `6781.2` 79.4 %, `6781.3` 82.5 %, `6781.4` 82.5 %, `6781.5` 82.5 %, `6781.6` 82.5 %, `6781.7` 81.1 %, `6782.0` 83.3 %, `6782.1` 83.4 %, `6782.2` 80.9 %, `6782.3` 83.4 %, `6782.4` 83.2 %, `6782.5` 83.2 %, `6782.6` 83.2 %, `6782.7` 81.8 %, `6783.0` 82.5 %, `6783.1` 82.6 %, `6783.2` 79.7 %, `6783.3` 82.5 %, `6783.4` 82.4 %, `6783.5` 82.4 %, `6783.6` 82.4 %, `6783.7` 81.2 %, `6784` 82.2 %, `6785` 81.5 %, `678C` 82.1 %, `678E` 80.1 %, `67C0.0` 82.6 %, `67C0.1` 82.6 %, `67C0.2` 82.7 %, `67C0.3` 82.8 %, `67C0.4` 82.7 %, `67C0.5` 82.6 %, `67C0.6` 82.6 %, `67C0.7` 82.7 %, `67C1.0` 81.8 %, `67C1.1` 81.7 %, `67C1.2` 81.7 %, `67C1.3` 81.7 %, `67C1.4` 81.7 %, `67C1.5` 81.7 %, `67C1.6` 81.7 %, `67C1.7` 81.7 %, `67C6` 83.1 %, `67C7` 82.8 %, `67D0.0` 82.6 %, `67D0.1` 82.6 %, `67D0.2` 82.6 %, `67D0.3` 82.6 %, `67D0.4` 82.5 %, `67D0.5` 82.5 %, `67D0.6` 82.6 %, `67D0.7` 82.5 %, `67D1.0` 81.6 %, `67D1.1` 81.6 %, `67D1.2` 81.6 %, `67D1.3` 81.6 %, `67D1.4` 81.6 %, `67D1.5` 81.6 %, `67D1.6` 81.6 %, `67D1.7` 81.6 %, `67D2.0` 82.6 %, `67D2.1` 82.7 %, `67D2.2` 82.6 %, `67D2.3` 82.6 %, `67D2.4` 80.3 %, `67D2.5` 79.8 %, `67D2.6` 82.7 %, `67D2.7` 82.6 %, `67D3.0` 81.8 %, `67D3.1` 81.7 %, `67D3.2` 81.8 %, `67D3.3` 81.7 %, `67D3.4` 81.7 %, `67D3.5` 81.8 %, `67D3.6` 81.7 %, `67D3.7` 81.8 %, `67F6.0` 83.0 %, `67F6.1` 82.8 %, `67F6.2` 84.0 %, `67F6.3` 84.0 %, `67F7.0` 82.0 %, `67F7.1` 82.0 %, `67F7.2` 83.1 %, `67F7.3` 83.1 %

**E3 exception : limite de segment, accès à cheval sur 0xFFFF** — 48 forme(s) : `01` 99.1 %, `09` 99.1 %, `19` 99.2 %, `21` 99.1 %, `29` 99.1 %, `31` 99.1 %, `6601` 99.0 %, `6609` 99.0 %, `6619` 99.1 %, `6621` 99.0 %, `6629` 99.0 %, `6631` 99.0 %, `6681.0` 99.0 %, `6681.1` 99.0 %, `6681.3` 99.0 %, `6681.4` 99.0 %, `6681.5` 99.0 %, `6681.6` 99.0 %, `6683.0` 99.1 %, `6683.1` 99.1 %, `6683.3` 99.1 %, `6683.4` 99.1 %, `6683.5` 99.1 %, `6683.6` 99.1 %, `668C` 97.7 %, `668E` 97.3 %, `66EA` 100.0 %, `66F7.2` 98.9 %, `66F7.3` 98.9 %, `81.0` 99.2 %, `81.1` 99.2 %, `81.3` 99.2 %, `81.4` 99.2 %, `81.5` 99.2 %, `81.6` 99.2 %, `83.0` 99.2 %, `83.1` 99.2 %, `83.3` 99.2 %, `83.4` 99.2 %, `83.5` 99.2 %, `83.6` 99.2 %, `8C` 97.7 %, `8E` 97.3 %, `EA` 100.0 %, `F7.2` 99.0 %, `F7.3` 99.0 %, `FF.0` 98.9 %, `FF.1` 98.9 %

**F1 EFLAGS : drapeaux arithmétiques** — 81 forme(s) : `0FA3` 39.6 %, `0FA4` 3.6 %, `0FA5` 7.1 %, `0FAB` 32.6 %, `0FAC` 3.6 %, `0FAD` 7.2 %, `0FAF` 5.9 %, `0FB3` 37.7 %, `0FBA.4` 36.8 %, `0FBA.5` 25.0 %, `0FBA.6` 37.6 %, `0FBA.7` 24.2 %, `0FBB` 31.4 %, `0FBC` 2.5 %, `0FBD` 3.9 %, `10` 96.3 %, `11` 94.6 %, `12` 94.6 %, `13` 93.1 %, `14` 96.8 %, `15` 96.0 %, `660FA3` 35.0 %, `660FA4` 3.6 %, `660FA5` 7.1 %, `660FAB` 22.5 %, `660FAC` 3.6 %, `660FAD` 7.2 %, `660FAF` 5.6 %, `660FB3` 35.1 %, `660FBA.4` 48.9 %, `660FBA.5` 49.1 %, `660FBA.6` 48.4 %, `660FBA.7` 49.0 %, `660FBB` 21.3 %, `660FBC` 2.9 %, `660FBD` 3.6 %, `6611` 93.0 %, `6613` 92.5 %, `6615` 95.3 %, `6681.2` 94.4 %, `6683.2` 96.2 %, `670FA3` 34.2 %, `670FA4` 3.1 %, `670FA5` 5.9 %, `670FAB` 29.5 %, `670FAC` 2.9 %, `670FAD` 6.0 %, `670FAF` 5.6 %, `670FB3` 32.1 %, `670FBA.4` 31.6 %, `670FBA.5` 21.9 %, `670FBA.6` 32.2 %, `670FBA.7` 19.8 %, `670FBB` 27.3 %, `670FBC` 2.2 %, `670FBD` 2.6 %, `6713` 77.9 %, `67660FA3` 40.6 %, `67660FA4` 3.1 %, `67660FA5` 5.9 %, `67660FAB` 41.4 %, `67660FAC` 2.9 %, `67660FAD` 6.0 %, `67660FAF` 5.0 %, `67660FB3` 40.6 %, `67660FBA.4` 41.7 %, `67660FBA.5` 42.4 %, `67660FBA.6` 41.8 %, `67660FBA.7` 40.0 %, `67660FBB` 41.6 %, `67660FBC` 2.2 %, `67660FBD` 2.7 %, `676613` 77.3 %, `80.2` 96.5 %, `81.2` 95.4 %, `82.2` 96.5 %, `83.2` 95.8 %, `D2.4` 94.1 %, `D2.5` 94.4 %, `D4` 84.8 %, `D5` 50.2 %

**F2 EFLAGS : bits 18-31** — 2 forme(s) : `669D` 0.0 %, `66CF` 0.0 %

**M mémoire** — 7 forme(s) : `669C` 0.0 %, `66F7.6` 96.8 %, `66F7.7` 95.7 %, `F6.6` 97.0 %, `F6.7` 95.8 %, `F7.6` 96.4 %, `F7.7` 95.7 %

**R registres** — 285 forme(s) : `00` 99.5 %, `02` 97.4 %, `03` 97.0 %, `06` 96.6 %, `07` 96.3 %, `0B` 96.9 %, `0E` 96.7 %, `0FA0` 97.7 %, `0FA1` 97.2 %, `0FA8` 97.6 %, `0FA9` 97.3 %, `0FB2` 97.1 %, `0FB4` 97.0 %, `0FB5` 97.0 %, `0FB6` 97.3 %, `0FB7` 96.8 %, `0FBE` 97.3 %, `0FBF` 2.7 %, `16` 96.7 %, `17` 96.4 %, `18` 99.5 %, `1A` 97.4 %, `1B` 97.1 %, `1E` 96.6 %, `1F` 96.4 %, `20` 99.5 %, `22` 97.3 %, `23` 96.9 %, `28` 99.5 %, `2A` 97.3 %, `2B` 97.0 %, `2F` 95.6 %, `30` 99.5 %, `32` 97.2 %, `33` 96.9 %, `37` 91.6 %, `3F` 93.2 %, `50` 97.0 %, `51` 97.0 %, `52` 96.8 %, `53` 96.8 %, `54` 96.7 %, `55` 96.7 %, `56` 96.6 %, `57` 96.7 %, `58` 96.5 %, `59` 96.5 %, `5A` 96.4 %, `5B` 96.4 %, `5C` 96.3 %, `5D` 96.5 %, `5E` 96.5 %, `5F` 96.5 %, `60` 97.4 %, `61` 96.6 %, `62` 87.2 %, `6603` 96.9 %, `6606` 96.6 %, `6607` 96.3 %, `660B` 96.8 %, `660E` 96.7 %, `660F80` 99.8 %, `660F82` 99.8 %, `660F84` 99.8 %, `660F86` 99.8 %, `660F88` 99.8 %, `660F8A` 99.8 %, `660F8D` 99.8 %, `660F8E` 99.8 %, `660FA0` 97.7 %, `660FA1` 97.2 %, `660FA8` 97.6 %, `660FA9` 97.3 %, `660FB2` 97.0 %, `660FB4` 97.0 %, `660FB5` 97.0 %, `660FB6` 97.3 %, `660FB7` 96.8 %, `660FBE` 97.3 %, `660FBF` 96.8 %, `6616` 96.7 %, `6617` 96.4 %, `661B` 97.0 %, `661E` 96.6 %, `661F` 96.4 %, `6623` 96.8 %, `662B` 96.8 %, `6633` 96.8 %, `6650` 97.0 %, `6651` 97.0 %, `6652` 96.8 %, `6653` 96.8 %, `6654` 96.7 %, `6655` 96.7 %, `6656` 96.6 %, `6657` 96.7 %, `6658` 93.0 %, `6659` 92.9 %, `665A` 93.0 %, `665B` 93.0 %, `665C` 92.7 %, `665D` 92.8 %, `665E` 92.9 %, `665F` 92.9 %, `6660` 97.1 %, `6661` 4.5 %, `6662` 87.6 %, `6668` 96.6 %, `6669` 96.9 %, `666A` 96.6 %, `666B` 97.0 %, `666D` 70.8 %, `666F` 71.0 %, `6687` 99.4 %, `668B` 97.4 %, `668D` 97.7 %, `668F` 89.4 %, `669A` 97.3 %, `66A1` 97.8 %, `66A5` 73.8 %, `66A7` 78.8 %, `66AB` 77.8 %, `66AD` 76.7 %, `66AF` 84.0 %, `66C2` 87.8 %, `66C3` 88.0 %, `66C4` 96.8 %, `66C5` 96.8 %, `66C8` 1.3 %, `66C9` 7.9 %, `66CA` 88.8 %, `66CB` 88.2 %, `66E5` 99.0 %, `66F7.4` 96.8 %, `66F7.5` 96.8 %, `6702` 81.7 %, `670FB2` 76.8 %, `670FB4` 76.8 %, `670FB5` 76.8 %, `670FB6` 81.7 %, `670FB7` 80.9 %, `670FBE` 81.8 %, `670FBF` 2.3 %, `6712` 79.1 %, `671A` 81.6 %, `671B` 80.9 %, `6722` 82.0 %, `6723` 81.0 %, `672A` 81.6 %, `6732` 81.5 %, `6762` 47.9 %, `67660FB2` 76.8 %, `67660FB4` 76.7 %, `67660FB5` 76.8 %, `67660FB6` 81.7 %, `67660FB7` 80.9 %, `67660FBE` 81.8 %, `67660FBF` 81.0 %, `67661B` 80.9 %, `676623` 80.8 %, `676662` 50.2 %, `676669` 81.8 %, `67666B` 81.6 %, `67666D` 72.7 %, `67666F` 73.3 %, `676687` 81.1 %, `676689` 93.9 %, `67668B` 94.0 %, `67668D` 97.1 %, `67668F` 76.6 %, `6766A1` 99.2 %, `6766A5` 82.2 %, `6766A7` 87.4 %, `6766AB` 78.9 %, `6766AD` 77.8 %, `6766AF` 84.5 %, `6766C4` 76.7 %, `6766C5` 76.8 %, `6766F7.4` 81.8 %, `6766F7.5` 81.8 %, `6766F7.6` 87.9 %, `6766F7.7` 85.2 %, `6769` 81.7 %, `676B` 81.8 %, `676C` 76.2 %, `676D` 72.8 %, `676E` 76.1 %, `676F` 73.2 %, `6786` 82.1 %, `6787` 81.1 %, `6788` 94.0 %, `6789` 93.9 %, `678A` 94.1 %, `678B` 94.0 %, `678D` 97.1 %, `678F` 79.6 %, `67A0` 97.9 %, `67A1` 98.1 %, `67A4` 82.0 %, `67A5` 82.1 %, `67A6` 87.4 %, `67A7` 87.4 %, `67AA` 82.3 %, `67AB` 78.9 %, `67AC` 80.9 %, `67AD` 77.9 %, `67AE` 89.0 %, `67AF` 84.6 %, `67C4` 76.7 %, `67C5` 76.8 %, `67D7` 97.8 %, `67F6.4` 82.7 %, `67F6.5` 82.7 %, `67F6.6` 87.6 %, `67F6.7` 83.8 %, `67F7.4` 81.9 %, `67F7.5` 81.9 %, `67F7.6` 87.5 %, `67F7.7` 82.8 %, `68` 96.6 %, `69` 97.0 %, `6A` 96.6 %, `6B` 97.0 %, `6C` 74.0 %, `6D` 70.8 %, `6E` 73.8 %, `6F` 70.7 %, `80.0` 99.6 %, `80.1` 99.6 %, `80.3` 99.6 %, `80.4` 99.6 %, `80.5` 99.6 %, `80.6` 99.6 %, `82.0` 99.6 %, `82.1` 99.6 %, `82.3` 99.6 %, `82.4` 99.6 %, `82.5` 99.6 %, `82.6` 99.6 %, `86` 99.5 %, `87` 99.5 %, `8A` 97.4 %, `8B` 97.4 %, `8D` 97.7 %, `8F` 93.3 %, `9A` 97.3 %, `9C` 97.9 %, `9D` 97.2 %, `A0` 97.5 %, `A1` 97.8 %, `A4` 80.6 %, `A5` 74.0 %, `A6` 86.9 %, `A7` 79.0 %, `AA` 80.0 %, `AB` 77.8 %, `AC` 80.0 %, `AD` 76.8 %, `AE` 88.1 %, `AF` 84.2 %, `C2` 96.7 %, `C3` 96.8 %, `C4` 96.8 %, `C5` 96.8 %, `C8` 12.4 %, `C9` 93.7 %, `CA` 93.2 %, `CB` 93.2 %, `CC` 98.0 %, `CD` 97.5 %, `CF` 97.4 %, `D6` 99.0 %, `D7` 97.4 %, `E5` 99.8 %, `F6.2` 99.4 %, `F6.3` 99.4 %, `F6.4` 97.3 %, `F6.5` 97.3 %, `F7.4` 96.9 %, `F7.5` 96.9 %, `FE.0` 99.4 %, `FE.1` 99.4 %, `FF.2` 96.8 %, `FF.3` 96.2 %, `FF.6` 97.0 %

## G4.0 — L'outillage du x87, avant toute ligne de x87

Le 29/09, sur l'arbre 8444530. Plan : `PLAN-G4.md` § G4.0.

**ABI 27.** `h_state` porte l'état du x87 — ST en bits bruts, `MM[].q`, `MM_w4`, tags, TOP,
`npxs`, `npxc`, `x87_pc/op_*`, `ismmx`, `fpu_type`, `hasfpu` : 616 → 800 octets, sans padding
implicite (`ins` en 792). `h_set_fpu` écrit `fpu_type` avant `cpu_set()` dans `h_reset` et
`h_boot` ; `FuzzFpu` et `BootDiff` en sont les pendants C#. `h_fpu_clear_residue` /
`ClearFpuResidue` : DEVIATION du harnais, `x87_reset()` est vide (`x87.c:97`). Côté C#, l'état
entre dans `cpu_state` comme stockage ; aucun handler ne l'écrit. Comparés : 69 → 83 formes
de champ. Contrôles négatifs, retirés : `SetFpu` omis côté C# → `ST[0]` diverge à
l'itération 0 ; TOP faussé à la capture → `TOP` à l'itération 0.

**Le fuzzeur 8088 et AAM 0.** Le tir des 256 opcodes (`fuzz --mode single --iter 100000`,
graine 1) faisait mourir l'oracle de SIGFPE, arbre d'avant G4.0 compris : AAM 0, PB-46, dans
le processus du diff. Sonde par opcode, 30 000 itérations : D4 tombe sur le 8088 seul ; D4
sur 286 et 386, F6 sur 8088 et 286, verts. Correctif, accord de Julien : sur le cœur 8088,
l'immédiat nul de D4 devient 1 (en tête, ou derrière un préfixe enchaîné). **Les recettes
`--seed` 8088 qui tiraient `D4 00` changent** ; aucune autre, la suite du générateur ne
bougeant pas. Le mode flux n'est pas touché (l'immédiat y est l'opcode de remplissage).

**`--fpu-state`.** Un état x87 tiré et posé des deux côtés à chaque itération — pile vide,
pleine ou partielle ; ST spéciaux, entiers ou bruts ; tags `VALID|UINT64` ; `npxc` entier —
par un générateur À PART (graine ^ sel) : sans l'option, les tirages sont ceux d'avant.

**Les portes**, même série sur le binaire figé de 8444530 (avec le seul correctif D4 pour les
fuzzeurs) et sur G4.0 :

| porte | 8444530 | G4.0 |
|---|---|---|
| build | 0 avertissement | 0 avertissement ; `harness*.c` sans avertissement |
| `selftest` ; `check-oracle` | — | vert ; 0 dérive (empreintes du harnais mises à jour) |
| ABI | 26, 616 octets | 27, 800 octets |
| ops-count | 2048 / 2048 | 2048 / 2048 |
| boot-diff roms 6000 / `--model ibmxt` / `--cpu 3` | 25 457 272 / 23 442 235 / 52 936 825 | identiques |
| boot-diff ibmat / ami286 / ami386 (3000) | 4 723 826 / 5 207 508 / 4 368 893 | identiques |
| boot-diff ami386 4 Mo / ami386dx 4 Mo | 4 352 635 / 4 437 159 ¹ | identiques |
| boot-diff DOS, XT cfg, ami386dx + DOS ; sondes VGA en vga, 8900D, 9000B, ibmat vga | verts, 86/86 | identiques, trame pour trame |
| fuzz 8088 100 000, 286 80 000, 386 80 000 graines 1 et 7, 386 `--0f` 40 000 | verts | sorties identiques octet pour octet |
| fuzz 8088 flux 1 500 × 200 | 300 000, 69 champs | 300 000, 83 champs |
| fuzz `--fpu-state` 8088 100 000 / 286 80 000 / 386 80 000 / 8088 flux 300 000 | — | verts |
| page-check ; pm-fuzz 20 000 ; core286-check ; pm-check ; pm-check 386 | verts ; 68/0/0 ; 106/0/0 | identiques |
| cpu-config-check ; config-check | 21 + 4 ; vert | identiques |

¹ Remesuré le 3 octobre 2026 en porte isolée (CMOS de `nvr/default/`), au commit d774bc2 :
4 437 104, d'abord consigné, venait d'un lancement qui lisait le CMOS de session de l'utilisateur
(voir § G1.0). Instructions identiques des deux côtés dans les deux cas.

Hors portes, préexistant : le fuzzeur 386 en mode FLUX sur les 256 opcodes tombe (SIGSEGV,
rc=139, 8444530 compris), le 286 en flux aussi (abort, rc=134). Non instruit.

**Les trois mesures** (`x87-parity`, contre la .so) :

- **libm** — 10⁷ tirages par fonction, bits bruts, petite et grande plages : sin, cos, tan,
  atan2, log, pow, sqrt, fmod, floor, ceil, et F2XM1, FYL2X, FYL2XP1, FSCALE verbatim :
  **0 écart**, charges de NaN comprises. `Math.*` suffit (décision n° 4).
- **conversions** — 10⁷ : le cast .NET s'écarte 1 186 960 fois en `(int64_t)`, 4 728 939 en
  `(uint64_t)`, 1 630 456 en `(int32_t)`, 1 628 159 en `(int16_t)` ; les aides cvttsd2si de
  `X87Parity.cs`, **0**. `(uint64_t)` : GCC teste `d >= 2^63`, un NaN prend la branche
  directe et rend 0x8000000000000000 — le premier modèle, en `<`, se trompait sur lui.
- **fesetround** — hors du handler (`h_fpu_arith`, -O2 sans `-frounding-math`), 625 000 cas
  par combinaison : **100 % d'arrondi dirigé exact**, GCC honore le mode. Dans le VRAI
  handler (oracle seul, 387, `DC /r m64`), 31 250 cas par combinaison : **FADD seul suit
  RC** ; FSUB, FMUL, FDIV restent au plus près, ~50/50 avec l'exact. PB-48.

SingleStepTests n'a pas de corpus x87 : vingt-deux dépôts, aucun, et aucune forme D8-DF
dans `sst-baseline.tsv` ni `sst386-baseline.tsv`.

## PB-49 — L'ombre de SS, bornée ; le fuzzeur en flux du 286 et du 386 enfin vert

Le 29/09, sur d774bc2. Le fuzzeur en flux sur les 256 opcodes tombait sur le 286 (abort,
rc=134) et le 386 (SIGSEGV, rc=139), arbre d'avant G4.0 compris.

**La cause** (PB-49) : POP SS et MOV SS exécutent la suivante en appelant son handler, sans
appel terminal (`call opPOP_SS_l.part.0` puis `add $8,%rsp` au désassemblage de l'oracle) ;
une RAM remplie de 0x17 est une récursion de la taille de la RAM, IP ne bouclant pas en mode
réel. Balayage des 256 opcodes seuls en flux, 4 × 200 : seul 0x17 tombe, 286 et 386 ; le
8088 non (`noint`). Recette : `fuzz --core 286|386 --rounds 1 --instr 1 --op 17`. Après le
premier correctif, le 386 tombait encore à la ronde 1139 de la graine 1 : `17 65 17 65…`, le
préfixe GS saute sur le POP SS suivant, qui le rappelle.

**Mesuré** : des chaînes de N POP SS puis un NOP, oracle et C# sans borne, restent identiques
jusqu'à N = 100 000 au moins et tombent à N = 200 000 (pile de 8 Mo). L'hôte n'a qu'un fil.

**Les correctifs**, accord de Julien :
- le C# borne la chaîne à `SS_SHADOW_MAX` = 1 024 chargements enchaînés, profondeur commune
  aux quatre sites (POP SS w/l, MOV SS a16/a32), `// pcem bug, not reproduced: PB-49` ;
- `Fuzzer.Run`, flux, cœurs exec386 : derrière un remplissage 0x17, l'opcode intercalé ne
  peut être ni un octet qui enchaîne (`EnchaineSurLaSuivante`) ni 8E (`8E 17` = MOV SS,[BX]) ;
  derrière un préfixe de segment, ni 0x17. Le 8088 n'est pas touché.

**`popss-check`**, nouvelle porte : POP SS, MOV SS,AX, les deux mêlés, POP SS + préfixe GS,
sur 286 et 386, N = 1, 2, 100, 1 023, 1 024 : **identique à l'oracle** ; N = 1 025 et 4 096 :
sans plantage, écart attendu là où la chaîne recourt aux cycles (POP SS : le pas s'arrête au
1 025ᵉ ; MOV SS ne compte aucun cycle, `x86_ops_mov_seg.h:190` rendant avant CLOCK_CYCLES, et
la boucle d'exec386 reprend la chaîne ITÉRATIVEMENT dans le même pas — identique à l'oracle) ;
RAM entière remplie de 0x17, C# seul : 200 pas de 1 025 POP SS, sans plantage ni boucle.
Contrôle négatif, retiré : sans la borne, ce dernier cas fait tomber le C#.

**Les portes**, comparées aux journaux de d774bc2 : 25 boot-diffs, fuzzeurs 8088/286/386
single et flux 8088, `--0f`, `--fpu-state`, page-check, pm-fuzz, core286-check, pm-check 286
et 386, cpu-config-check, config-check — **43 journaux sur 43 identiques hors durées**.
Nouveaux verts : **fuzz 286 en flux** et **fuzz 386 en flux**, 1 500 × 200, 256 opcodes,
300 000 instructions chacun ; `popss-check`. Build 0 avertissement, selftest, check-oracle 0
dérive.

**Reste ouvert, instruit, non corrigé** : une RAM remplie de préfixes (64, 66, 67, F0, F2,
F3, ou `26 64`) forme sur le 386 une seule « instruction » d'environ 1,7 million de préfixes
(3 428 108 cycles au premier pas, IP 000F0002 : la fin du Mo rempli). Oracle et C# en
Release identiques, sans plantage ni boucle — parce que les deux font du préfixe un saut
terminal : GCC (`jmp *%rax`) et RyuJIT, qui n'y est pas tenu. **En Debug, le C# tombe**
(StackOverflow dans `PrefixeSegment`, `fuzz --core 386 --rounds 1 --instr 3 --op 64`). Le
silicium lève #GP au-delà de 15 octets par instruction (10 sur le 286) et au-delà de la
limite de CS en mode réel : PCem ne fait ni l'un ni l'autre.

## PB-50, PB-51 — Le trampoline des préfixes ; le C# Debug ne tombe plus

Le 29/09, sur 6c91331. Instruit à la suite de PB-49 : une RAM remplie de préfixes (64, 65, 66,
67, F0, F2, F3, ou `26 64` alternés) forme sur le 386 UNE instruction d'environ 1,7 million
de préfixes — 3 428 108 cycles au premier pas, IP 000F0002. Pas de boucle sans fin : IP ne
boucle pas en mode réel (PB-51), la chaîne s'arrête à la fin du Mo rempli. PCem ne limite pas
la longueur d'une instruction (PB-50 ; le silicium lève #GP au seizième octet, onzième sur le
286). Oracle et C# Release identiques — les deux font du préfixe un saut, GCC par
`jmp *%rax`, RyuJIT parce qu'il le veut bien. **Le C# Debug tombait** : StackOverflow dans
`PrefixeSegment`, `fuzz --core 386 --rounds 1 --instr 3 --op 64`.

**Le correctif**, accord de Julien : un trampoline (`Cpu/386_ops_prefix.cs`, `// DEVIATION:`).
Les préfixes — op_seg et ses formes REPE/REPNE, 66, 67, `PrefixeTaille`, REPE, REPNE,
LOCK — rendent `TailCall(handler, fetchdat)` au lieu d'appeler ; `Dispatch` appelle le handler
rangé tant qu'on lui rend TAIL, aux trois sites qui aiguillent depuis l'extérieur d'un préfixe :
la boucle d'exec386, POP SS (w, l) et MOV SS (a16, a32). Même handler, même fetchdat, même
valeur rendue, rien d'exécuté entre les deux : l'appel terminal de PCem, sans pile. Les
échappements 0F et x87 restent des appels directs — un cran, leurs handlers n'enchaînent pas.

**Les portes**, comparées aux journaux de 6c91331 et de la série précédente : 25 boot-diffs,
fuzzeurs 8088/286/386 single, flux 8088, 286 et 386 sur les 256 opcodes, `--0f`,
`--fpu-state` (single et flux 8088), page-check, pm-fuzz, core286-check, pm-check 286 et 386,
cpu-config-check, config-check, `popss-check` — **47 journaux sur 47 identiques hors
durées**. Build 0 avertissement, selftest, check-oracle 0 dérive. **En Debug**, verts : les
flux de 64, 65, 66, 67, F0, F2, F3, `26 64`, `2E 66` sur le 386, de 26 et F3 sur le 286, et le
flux 386 complet, 1 500 × 200, 256 opcodes, 300 000 instructions.

## G4.1 — L'état et la plomberie du x87

Le 29/09, sur c6e0095. Plan : `PLAN-G4.md` § G4.1. Aucun handler x87 réel : c'est G4.2.

**Transcrit.** `Cpu/x87_timings.cs` : le struct `x87_timings_t` (72 int, Sequential) et les
quatre tables 8087, 287, 387, 486, verbatim (`(a + b) / 2` en division entière). `Cpu/x87.cs` :
`x87_pc_*` (déplacées de x86.cs), `x87_gettag`, `x87_settag`, `x87_reset` (vide chez PCem),
les constantes C0-C3, TAG_*, X87_ROUNDING_*. `Cpu/cpu_tables.cs` : le struct FPU, les cinq
tables `fpus_*`, et le champ `fpus` rendu à CPU, à sa place dans l'ordre du C. `Cpu/cpu.cs` :
FPU_* au complet, `fpu_get_type`, `fpu_get_internal_name`, la branche `hasfpu` de cpu_set
(les seize tables `ops_fpu_*`) et le `switch (fpu_type)` des temps (cpu.c:1132-1152).

**Provisoire, et bruyant.** Les seize tables `ops_fpu_*` existent, chacun de leurs emplacements
s'arrête (`opX87NonTranscrit`) jusqu'à G4.5 ; l'ESC du 8088 s'arrête quand un 8087 est déclaré
(G4.6). Un coprocesseur déclaré ne se comporte donc jamais comme « pas de coprocesseur » en
silence.

**La clé `fpu`.** Lue par `loadconfig` (pc.c:655, défaut « none »), résolue par `initpc`
contre la machine et le CPU FINAUX, après `check_cpu` (DEVIATION : `--model` et `--cpu`
s'appliquent après le fichier) ; écrite par le SETUP (pc.c:875). BootDiff la résout de même
et la pousse par `h_set_fpu`.

**L'empreinte CPU** gagne deux champs, des deux côtés : `fpu_type` et la FNV des 72 champs de
`x87_timings`. `cpu-config-check` balaye désormais, pour chaque entrée de CPU, chacun des
coprocesseurs de sa liste, et l'ami386 et l'ami386dx en plus des quatre machines d'avant :
**67 configurations identiques, 6 refus concordants** (21 + 4 avant) — 8087 sur le 5150 et le
XT, 287 et 287XL sur l'IBM AT et l'ami286, 387 sur l'ami386 et l'ami386dx. Contrôle négatif,
retiré : la table du 387 donnée au 287 côté C# → « FNV des 72 champs de x87_timings » diverge
sur chaque entrée 287.

**Les portes**, comparées aux journaux de c6e0095 : 25 boot-diffs, fuzzeurs 8088/286/386
single, flux 8088, 286 et 386 sur 256 opcodes, `--0f`, `--fpu-state`, page-check, pm-fuzz,
core286-check, pm-check 286 et 386, config-check, popss-check — **46 journaux sur 46
identiques hors durées** ; cpu-config-check vert, élargi. Build 0 avertissement, selftest,
check-oracle 0 dérive (112 fichiers : x87.cs et x87_timings.cs entrent au manifeste).

## G4.2 — Les chargements et les stockages du x87

Le 29/09, sur 17c241f. Plan : `PLAN-G4.md` § G4.2.

**Transcrit.** `Cpu/x87_ops_loadstore.cs` : les trente-quatre handlers de
`x87_ops_loadstore.h`, a16 et a32, transcrits PAR RÈGLES (FP_ENTER, fetch_ea, gardes en
`if (…) return 1;` ; conversions par cvttsd2si ; `fplog` omis). `Cpu/x87_ops.cs` : ST,
FP_ENTER, x87_push, x87_pop, x87_fround, x87_ld80, x87_st80, FPU_ILLEGAL, les unions x87_ts /
x87_td, les aides CvtI64 / CvtU64 / CvtI32 (DEVIATION, mesurées en G4.0), et `TableFpu`, qui
pose les rangées mémoire de chargement, de stockage et ILLEGAL de D9, DB, DD, DF. `readmemq`,
`writememq`, `geteaq`, `seteaq` (386_common.h:16-40, :204, :222), `readmemql` et `writememql`
(mem.c:670-761, sans enveloppe comme dans l'oracle), `fpucount`.

**Une erreur de transcription, trouvée par le fuzzeur et corrigée.** FPU_ILLEGAL passait la
globale `x86.rmdat` à PREFETCH_RUN ; x86.h:197 fait `#define fetchdat rmdat`, et le `rmdat` du
handler est son PARAMÈTRE. Divergence `prefetch_bytes : oracle 0, C# 1` sur `66 DB 65 92`.

**La porte** : `fuzz --fpu 387|287 --x87 mem --fpu-state` — le coprocesseur des deux côtés,
un ModRM mémoire dont le `reg` désigne une rangée transcrite (dérivé des bits tirés, sans
consommer le générateur), aussi derrière 66 et 67 pour les formes a32, et les seize octets à
l'adresse effective comparés à chaque pas (writememql n'est journalisé d'aucun côté).

| porte | résultat |
|---|---|
| 386, 387, D9/DB/DD/DF + 66/67, graine 1, 80 000 | vert, zéro divergence |
| 386, 387, idem, graine 7, 80 000 | vert, zéro divergence |
| 286, 287, D9/DB/DD/DF, 80 000 | vert, zéro divergence — les temps du 287 confrontés |

Contrôles négatifs, retirés : le cast .NET au lieu de cvttsd2si dans x87_fround → divergence
à l'itération 143 (`DF 5C`, FISTP m16) ; sans le `& 0x3ff` de x87_ld80 → itération 3 (`DB 2D`,
FLD m80) ; le signe 0x40 au lieu de 0x80 dans FBSTP → itération 17 (`DF 35`) ; seteaq
faussé d'un bit → itération 5, `RAM à l'EA 103E65` (`DD 12`, FST m64). Le `(byte)` .NET au
lieu de CvtI32 dans FBSTP ne mord pas : sur `floor(fmod(x, 10))`, NaN et ±∞ rendent 0 des
deux façons.

**Les défauts de PCem rencontrés**, reproduits : PB-52 (FBLD n'existe pas, DF /4 est
FPU_ILLEGAL), PB-53 (FBSTP écrit la globale `tempc`), PB-54 (seul FSTP m64 contrôle la
limite), PB-55 (x87_ld80 replie l'exposant, écrase les dénormaux), PB-56 (x87_st80 et les
dénormaux ; 53 bits de précision).

**Non couvert** : le mode flux avec coprocesseur (le remplissage donne des ModRM de mode
registre, G4.3 et G4.4) ; le mode protégé (pm-fuzz ne tire pas d'ESC).

**Les portes**, comparées aux journaux de 17c241f : **47 journaux sur 47 identiques hors
durées** (25 boot-diffs fpu=none, fuzzeurs single et flux 8088/286/386, `--0f`,
`--fpu-state`, page-check, pm-fuzz, core286-check, pm-check 286 et 386, cpu-config-check,
config-check, popss-check). Build 0 avertissement, selftest, check-oracle 0 dérive (114).

## G4.3 — L'arithmétique du x87

Le 30/09, sur 9023016. Plan : `PLAN-G4.md` § G4.3.

**Compté avant d'écrire.** `gcc -E` de l'unité 386_dynarec.c de l'oracle (harness_386.c) :
x87_ops_arith.h engendre **92 handlers** — 64 formes mémoire (la macro opFPU : FADD, FCOM,
FCOMP, FDIV, FDIVR, FMUL, FSUB, FSUBR × s, d, iw, il × a16, a32) et 28 formes registre — et
**16 appels fesetround**, les huit opFADD mémoire × 2 : PB-48, à la ligne. FCOMI, FCOMIP,
FUCOMI, FUCOMIP ne sont que dans les tables `_686_` : 88 handlers transcrits.

**Transcrit, par génération depuis le C.** `Cpu/x87_ops_arith.cs` : la macro expansée pour
ses huit instanciations, puis les formes registre ; x87_div devient `if (x87_div(ref …))
return 1;`. `Cpu/x87_ops_tables.cs` : les seize tables non 686 de x87_ops.h (:310-1040),
emplacement par emplacement, `ILLEGAL` résolu selon le #define ; 51 handlers encore absents
(FLDENV, FSAVE, FXCH, FLD1, les transcendantes…) y sont des souches à leur nom (G4.4, G4.5).
Le TableFpu de G4.2 disparaît. `Cpu/x87_ops.cs` gagne x87_div, x87_compare / x87_ucompare
(DEVIATION : l'asm `fcompp` hôte de PCem, rendu par sa sémantique) et x87_fadd_dirige
(DEVIATION : l'arrondi dirigé des seuls FADD mémoire, TwoSum, débordement, zéro exact).

**Le NaN qui survit** (PB-60). Le fuzzeur a divergé sur `DC C5`, deux NaN en entrée :
`ST[5] : oracle 0x7FF8000000000000, C# 0x7FFFFFFFFFFFFFFF`. addsd et mulsd rendent le premier
opérande NaN ; GCC a choisi l'ordre handler par handler. `x87-nan-order` le mesure sur l'oracle
(table dans PB-60) ; X87AddSd / X87MulSd imposent la règle SSE dans l'ordre mesuré.

**Les portes.**

| porte | résultat |
|---|---|
| 386 + 387, D8/DA/DC/DE mémoire et registre + 66/67, graines 1 et 7, 80 000 | verts |
| 286 + 287, D8/DA/DC/DE, 80 000 | vert, temps du 287 confrontés |
| 386 + 387, flux D8/DA/DC/DE (ModRM de mode registre), 1 500 × 200 | vert, 300 000 |
| `x87-cases` : FADD m64 et m32 sur toutes les paires de 18 bornes × 4 modes, FCOMPP / FCOM / FUCOMPP sur les zéros signés et NaN, FDIV par ±0 masquée et démasquée | vert, 1 825 cas |
| portes de G4.2 (D9/DB/DD/DF mémoire, 387 et 287) | vertes, identiques |

Contrôles négatifs, retirés : l'arrondi dirigé appliqué AUSSI à FMUL mémoire → divergence à
l'itération 22 (`DA 4E`) ; x87_compare sans test de NaN → itération 67 (`npxs`) ; l'ordre
PB-60 d'opFADD inversé → itération 46 159 ; la branche démasquée de x87_div neutralisée →
itération 104 (`cycles : oracle 8, C# 89`) ; sans le contournement de FCOMPP → `x87-cases`,
`npxs : oracle 0x0100, C# 0x4000` ; sans la règle du zéro exact vers le bas → 15 cas.

**Les défauts de PCem** : PB-57 (FCOM registre et les NaN), PB-58 (FCOMPP, −0 contre +0),
PB-59 (ZE seule ; démasquée, l'instruction s'évapore), PB-60 (le NaN propagé).

**La série**, comparée aux journaux de 9023016 : **50 journaux sur 50 identiques hors
durées** (25 boot-diffs fpu=none, fuzzeurs existants, portes de G4.2, checks, popss-check).
Build 0 avertissement, selftest, check-oracle 0 dérive (116).

## G4.4 — Le reste du x87, transcendantes exceptées

Le 30/09, sur a5be5be. Plan : `PLAN-G4.md` § G4.4.

**Transcrit, par génération depuis le C.** `Cpu/x87_ops_misc.cs` : quarante-sept fonctions de
x87_ops_misc.h — pile (FLD, FXCH, FFREE, FST, FSTP registre), constantes, FCHS, FABS, FTST,
FXAM, FSTSW (mémoire et AX), FSTCW, FLDCW, FNINIT, FNCLEX, FDISI, FENI, FSTENV, FLDENV,
FSAVE, FRSTOR (16 et 32 bits, réel et protégé, TAG_UINT64, MM_w4, le « Horrible hack » d'ismmx),
FPREM, FPREM1, FSQRT, FRNDINT, FSCALE, FDECSTP, FINCSTP. `codegen_set_rounding_mode` omis (le
dynarec, souche vide dans l'oracle). `Cpu/x87_ops.cs` : x87_push_u64, x87_st_fsave,
x87_ld_frstor (conversion NON signée de MM[].q, corrigée avant tout test), x87_stmmx. Les huit
transcendantes restent des souches (G4.5).

**Un défaut du harnais, trouvé ici.** pm-fuzz avec ESC tombait en « BANC FAUX » à l'itération 15
(graine 1) : un FDIV par zéro à l'itération 13, ZE démasquée (npxc vaut 0 après le reset),
levait IRQ13 (PB-59), que ni h_reset ni Reset386 ne retirent du PIC ; LOADALL386 posait IF à
l'itération suivante et l'interruption partait. Correctif étroit : avec `--fpu`, pm-fuzz pose
npxc = 0x037F (FNINIT) à chaque cas, des deux côtés. La fuite d'IRQ d'une itération à l'autre
reste, pour les autres fuzzeurs, un défaut du harnais noté, pas corrigé.

**Les portes.**

| porte | résultat |
|---|---|
| 386 + 387, `--x87 g44` D9/DB/DD/DF mémoire et registre + 66/67, graines 1 et 7, 80 000 | verts |
| 286 + 287, idem, 80 000 | vert |
| 386 + 387, flux D9/DB/DD/DF, 1 500 × 200 | vert, 300 000 |
| pm-fuzz `--fpu 387`, les huit ESC, CR0.EM/TS une fois sur quatre (#NM), 20 000 | vert, cinq départs |
| `x87-cases` : + FXAM/FTST/FCHS/FABS sur onze classes, FRNDINT × 4 modes, FPREM/FPREM1/FSCALE aux bornes, FNSTSW AX et FSTSW m16 (TOP 0, 3, 6), les sept constantes, FNINIT, FNCLEX, FFREE, FST/FSTP registre, FSAVE/FRSTOR et FSTENV/FLDENV et FSTCW/FLDCW en 16 et 32 bits, réel et PE, #NM par EM, TS, EM+TS sur les huit tables | vert, 2 039 cas |

`--x87 g44` écarte les huit transcendantes de D9 (F0-F3, F9, FB, FE, FF) par une table fixe ;
la comparaison à l'EA couvre 112 octets (l'image FSAVE dépasse les 64 entrées du journal).

Contrôles négatifs, retirés : FNSTSW AX qui compose TOP → `regs[0]`, TOP 0 et 3 ; FXAM qui
reconnaît l'infini → `npxs` sur ±∞ ; FSAVE 16 bits réel qui écrit +8 → `[DS:0108]` ;
FP_ENTER qui ignore TS → les cycles de #NM ; FST registre qui recopie MM[].q → `MM[1].q`.

**Les défauts de PCem** : PB-61 (FNSTSW AX sans TOP), PB-62 (x87_pc_* jamais posés,
dispositions de FSAVE et FSTENV partielles), PB-63 (FXAM à trois classes), PB-64 (FTST et les
NaN), PB-65 (FPREM tronqué, FPREM1 = FPREM), PB-66 (FLDLN2 d'un ulp), PB-67 (FST registre et
TAG_UINT64).

**La série**, comparée aux journaux de a5be5be : tous identiques hors durées, sauf
`x87-cases`, élargi (1 825 → 2 039 cas). Build 0 avertissement, selftest, check-oracle 0
dérive (117).

## G4.5 — Les transcendantes

Le 30/09, sur f847fee. Plan : `PLAN-G4.md` § G4.5.

**Transcrit.** F2XM1, FYL2X, FYL2XP1, FPTAN, FPATAN, FSIN, FCOS, FSINCOS (x87_ops_misc.h:554-753),
générés depuis le C comme le reste de x87_ops_misc.cs ; `pow`, `log`, `tan`, `atan2`, `sin`, `cos`
deviennent Math.*, dont la parité au bit avec la glibc de l'oracle est mesurée depuis G4.0
(10⁷ tirages par fonction, expressions verbatim comprises). Décision n° 4 : pas de P/Invoke.
Les seize tables n'ont plus AUCUNE souche : 175 handlers distincts, tous transcrits.

**Les portes.** Fuzzeur `--x87 all` (D8 à DF sans filtre, EA sur 112 octets) : 386 + 387 +
66/67 graines 1 et 7, 286 + 287, 80 000 chacun, verts ; flux 386 + 387 D8-DF, 300 000, vert ;
pm-fuzz `--fpu 387` sur les huit ESC, transcendantes comprises, 20 000, vert. `x87-cases` +
les huit sur dix-neuf bornes (0, ±0,5, ±1, 2, ±π, ±10²⁰, 9,3·10¹⁸, 10³⁰⁰, ±∞, NaN, dénormaux,
−3) × quatre ST(1) : 2 647 cas, verts.

Contrôles négatifs, retirés : FSINCOS dans l'ordre inverse → 64 cas ; FPTAN qui pose C2 → 76 ;
FYL2X par Math.Log2 → 2 cas, au dernier bit (`0x…DDF8` contre `0x…DDF7`) — la formule
`log(x) / log(2)` de PCem n'est PAS log2 ; F2XM1 par `exp(x ln 2) − 1` → 8 cas.

**Le défaut de PCem** : PB-68 (la libm de l'hôte, sans borne 2^63 ni C2, et sur 8087 et 287
qui n'ont ni FSIN ni FCOS).

**La série**, comparée aux journaux de f847fee : tous identiques hors durées sauf x87-cases,
élargi (2 039 → 2 647). Build 0 avertissement, selftest, check-oracle 0 dérive (117).

## G4.6 — Le 8087

Le 30/09, sur ea6c332. Plan : `PLAN-G4.md` § G4.6.

**Transcrit.** 8087.h ne porte aucune instruction : c'est un jeu de macros qui fait recompiler
TOUT x87_ops.h dans l'unité du 808x (`8087.h:86`). Même geste ici, en deux parties.
`Cpu/x87_8087.cs` porte le contexte : FP_ENTER réduit à `fpucount++` (X8087, pas de #NM),
fetch_ea / SEG_CHECK / CHECK_WRITE / PREFETCH_RUN vides, CLOCK_CYCLES sur `cycles`, readmeml /
readmemq / writememl / writememq par le readmemw / writememw du 808x — bus 8 bits, memcycs,
offset de 16 bits qui boucle dans le segment —, writememb_8087, geteal / geteaq / seteal /
seteaq qui font fatal() en mode registre, et les aides mémoire de x87_ops.h (x87_ld80,
x87_st80, x87_st_fsave, x87_ld_frstor, x87_stmmx, FPU_ILLEGAL) réinstanciées.
`Cpu/x87_ops_808x.cs` est GÉNÉRÉ : le corps des trois fichiers de handlers du 386 recopié
dans `_808x`, où chaque primitive de contexte se résout d'abord — comme le C, où 8087.h les
redéfinit avant l'#include ; les aides sans mémoire (pile, conversions, arrondi dirigé,
comparaisons, x87_div) viennent de `_386`, rendues `internal`. Vérifié à la liste : aucun
appel de la copie ne se résout vers une primitive mémoire de `_386_common`.
`Cpu/x87_ops_808x_tables.cs` : les huit tables a16 sous `OP_TABLE = ops_808x_` (8087.h:7),
générées verbatim. Les huit ESC de `808x.c:3304-3366` appellent la table et restituent
`pc` (tronqué à 16 bits, comme le `uint16_t save_pc` du C). SW1 du XT (keyboard_xt.c:161,
:199) était transcrit depuis G4.1.

**Un trou de l'outil, trouvé par un contrôle négatif et bouché.** writememq faussé (le mot
haut pris à `>> 40`) laissait le fuzzeur 8088 VERT : le 808x ne pose pas `cpu_state.ea_seg`,
donc `CmpEa` ne comparait rien, et ses écritures rapides (writelookup2) échappent au journal.
`CmpEa` prend désormais, sur le 8088, la base `easeg` du côté C# (`eaaddr` est déjà comparé).
Même contrôle après : divergence à l'itération 41 (`DF BF`, FISTP m64).

**Les portes.**

| porte | résultat |
|---|---|
| fuzz 8088 + 8087, `--x87 all`, D8-DF, graine 1, 100 000 | vert, zéro divergence |
| idem, graine 7, 100 000 | vert, zéro divergence |
| flux 8088 + 8087, D8-DF, 300 000 | vert, 83 champs |
| flux 8088 + 8087, 255 opcodes (D4 exclu, PB-46), `--fpu-state`, 300 000 | vert |
| boot-diff ibmxt + 8087, ROM seule (6 000) et PC DOS 2.00 (7 000) | vert |
| boot-diff ibmpc + 8087, ROM seule (6 000) et PC DOS 2.00 (7 000) | vert |

Les quatre boot-diffs rendent le MÊME nombre d'instructions que sans coprocesseur (23 442 235,
22 086 862, 25 457 272, 26 750 652) : ni le BIOS du 5150 et du 5160 ni PC DOS 2.00 n'exécutent
d'ESC ; ils ne voient du 8087 que le bit de SW1. Sonde, retirée : `fpu_type = 1` résolu des
deux côtés. Le 8087 n'est donc exercé que par le fuzzeur — les témoins sont en G4.7.

Contrôles négatifs, retirés : CLOCK_CYCLES vide → divergence à l'itération 0 ; writememq
faussé → itération 41 (après le correctif de CmpEa) ; readmemw qui permute un bit d'adresse
→ itération 7 516 (`DD 05`, FLD m64). Un `pc++` avant la restitution ne mord pas, et c'est
attendu : la restitution l'efface — les handlers du 8087 ne touchent pas `pc`.

**Le défaut de PCem** : PB-69 (hors AT, `picint(1 << 13)` est jeté : l'exception du 8087 ne
produit ni IRQ ni NMI).

**La série**, sur un instantané construit avant toute ligne de G4.7 et comparée aux journaux
de ea6c332 : **65 journaux sur 65 identiques hors durées**, et les huit portes nouvelles
vertes (quatre boot-diffs 8087, fuzz 8088 + 8087 graines 1 et 7, deux flux). Build 0
avertissement, selftest, check-oracle 0 dérive (120 : x87_8087.cs, x87_ops_808x.cs et
x87_ops_808x_tables.cs entrent au manifeste).

## G4.7 — Les machines et les témoins

Le 30/09, sur le commit de G4.6. Plan : `PLAN-G4.md` § G4.7.

**Les machines.** `fpu = 287` dans `ixtal26-286.cfg` (profil Rider « 286, Trident 8900D »),
`fpu = 387` dans `ixtal26-386.cfg` (profil « 386DX, Trident 9000B ») — décision n° 1 ; `none`
partout ailleurs, lignes de base inchangées. `--make-nvr` pose le bit 1 de l'octet 0x14 quand
la clé en déclare un. Mesuré : les deux BIOS AMI détectent le coprocesseur SEULS — avec un
CMOS dont le bit 1 est à zéro (celui de session de l'utilisateur, ou la référence de PCem), le
POST affiche « Numeric Processor : Present » sans plainte. Le bit est donc informatif ici, et
les CMOS de session existants n'ont pas à être refaits.

**Les boot-diffs**, tous verts : ibmat en 287 et en 287XL, ami286 en 287 (3 000) ; ami386 et
ami386dx en 387 (3 000), ami386dx en 387 avec PC DOS 2.00 (2 000) et jusqu'au bout du POST
(25 000 tranches, 36 077 215 instructions). L'ibmat (4 723 848 contre 4 723 826) et l'ami286
(5 207 135 contre 5 207 508) changent de trajectoire : leur BIOS sonde le 287 et bifurque. Les
deux 386 gardent le même compte : la sonde s'exécute à nombre d'instructions égal, ESC compris.

**Les témoins**, iXtal26 seul (`--boot`), sur des COPIES des disques de l'utilisateur
(`os/286-HDD-C.img`, `os/386-HDD-C.img`) dont l'AUTOEXEC.BAT est réécrit sans `KEYB FR` —
KeyScript tape en QWERTY — et un binaire, un `nvr/` et des CMOS isolés dans /tmp : ni les
disques ni les CMOS de session ne sont touchés. `--boot` gagne pour cela `@shot FICHIER`
(l'écran en PPM), les touches `@` et `=`, et une ligne `x87 : fpucount = N` après chaque
frappe quand un coprocesseur est déclaré.

| témoin | 286/16 + 287 | 386DX/33 + 387 | sans FPU (les deux) |
|---|---|---|---|
| POST AMI | « Numeric Processor : Present » | idem | « None » |
| INT 11h, 0040:0010 | 0463 (bit 1) | 0463 | 0461 |
| MSD /S (Windows 3.1) | « 80286/80387 » — PB-70 | « 80386/80387 » | « 80286 », « 80386 » |
| QBASIC `PRINT SIN(1), ATN(1)*4, SQR(2)` | .841471 3.141593 1.414214 | idem | idem |
| Windows 3.1, Calculatrice, `2 sqrt` | 1.414213562373 ; fpucount 184 → 2 917 | 1.414213562373 ; 5 → 487 | 1.414213562373 (WIN87EM émule) |
| X87BANC.COM | **297 tics** | **98 tics** | « PAS DE FPU » |

MSD exécute vingt instructions x87 pour sa détection (fpucount 2 → 22) ; la Calculatrice passe
par WIN87EM jusqu'au coprocesseur. **QBASIC n'exerce PAS le x87** : fpucount ne bouge pas
d'une unité pendant un programme de calcul, avec ou sans 387. Désassemblé (QBASIC.EXE de
DOS 5, offset 0x2BD63) : `mov byte [4],0` pose à zéro le drapeau « 8087 présent » de la
bibliothèque, et chaque FNINIT est gardé par `cmp byte [4],0 / je`. QBASIC 1.1 calcule donc
toujours par son émulateur ; ses résultats justes ne prouvent rien du coprocesseur, et le
banc QBASIC prévu (décision n° 6, option A) ne pouvait montrer aucun écart : 11,37 s avec et
sans 387, S identique au dernier chiffre. D'où la décision n° 6 révisée.

**Le banc.** `tools/x87banc/x87banc.py` assemble X87BANC.COM (168 octets, listing annoté,
désassemblage vérifié par objdump) et écrit `x87banc.keys` : `DEBUG`, onze lignes `E`, `N
B:X87BANC.COM`, `R CX`, `A8`, `W`, `Q`. La machine fabrique elle-même son .COM sur une
disquette 360 Ko insérée en B: (`@B:`), puis `B:X87BANC`. Détection par SMSW (CR0.EM) puis
FNINIT / FNSTSW ; 4 × 65 535 tours de FLD m64, FPTAN, FDIVP, FSQRT, FMUL m64, FSTP m64 —
communs au 287 et au 387, 0,5 dans le domaine du FPTAN du 287 ; chronomètre INT 1Ah. Rejoué :
sortie identique à l'octet près. Le 386DX/33 + 387 est trois fois plus rapide que le 286/16 +
287 (98 contre 297 tics) ; sans coprocesseur, « PAS DE FPU » et retour propre à DOS.

**Le défaut de PCem** : PB-70 (le bit IC n'est jamais lu : le 287 compare l'infini en affine).
Reproduit : `x87-cases` + deux cas (FNINIT ; 1/0 ; FLD ST ; FCHS ; FCOMPP ; FNSTSW, en 287 et
en 387) — l'oracle rend C0 sans C3 sur les deux. Contrôle négatif, retiré : un x87_compare
projectif sur le 287 → `npxs : oracle 0x0104, C# 0x4004`.

**La série**, sur un instantané de G4.7 et comparée aux journaux de 3e76e9f : **72 journaux
sur 72 identiques hors durées**, sauf `x87-cases`, élargi (2 647 → 2 649, les deux cas de
PB-70) ; sept boot-diffs nouveaux, verts (ibmat 287 et 287XL, ami286 287, ami386 387, ami386dx
387 seul, avec PC DOS 2.00, et jusqu'au bout du POST). `config-check` inchangé avec la clé
`fpu` dans les deux configurations. Build 0 avertissement, selftest, check-oracle 0 dérive (120).

## G5.0 — L'oracle lit enfin un disque AT : mfm_at.c et ide.c liés

Le 1er octobre 2026, sur 226b3d8. Plan : `PLAN-G5.md` § G5.0.

**Le trou.** Depuis M13, les profils 286 et 386 tournent sur `mfm_at` (type 46, 156 Mo) sans
qu'aucun boot-diff ait jamais vu ce disque : l'oracle ne liait pas `mfm_at.c`, et
`h_set_hdd_controller("mfm_at")` n'y montait rien, en silence.

**L'oracle.** `mfm_at.c` et `ide.c` entrent au Makefile ; `harness.c` monte `mfm_at` et `ide`
(celle-ci avec `cdrom_channel = zip_channel = -1`, la configuration PCem « Hard drive » sur les
quatre lecteurs, wx-config.c:891) et appelle `resetide()` à la place de pc.c:395. ABI 28 :
changement par le comportement. `harness_stubs.c` rend `hdd_controller_current_is_ide`
(« ide » seul, des cartes liées), le pilote CD de l'hôte `atapi` sous sa forme nulle, et des
`atapi_*` / `scsi_bus_atapi_init` qui s'arrêtent bruyamment — ATAPI est hors G5.

**La décision n° 3 tranchée à la lecture.** Les `atapi->stop()` du rappel de reset
(ide.c:796-812) sont atteints sans ATAPI — pour tout lecteur absent —, mais `atapi` n'est
JAMAIS nul chez PCem : pc.c:293 appelle toujours `cdrom_null_open`, qui pose un pilote dont
`stop` est vide (cdrom-null.c:19, :50-51). Ce n'est pas un défaut de PCem, et il n'y a rien à
contourner : l'oracle reçoit ce même pilote nul, le C# n'a rien à transcrire. Arbre vendoré
intact (check-oracle 0 dérive).

**L'outil.** KeyScript gagne le suffixe « ^ » — une ligne tapée SANS Entrée —, commun à
`--boot` et au boot-diff : sortir de FDISK exige un Échap seul, l'Entrée qui suivait choisissant
aussitôt l'option par défaut du menu d'arrivée.

**Les portes**, dans un répertoire isolé (/tmp/g5w : CMOS type 46 fabriqué par `--make-nvr`,
copie du disque 286 de l'utilisateur sans `KEYB FR`, image vierge de 156 Mo) :

| boot-diff ami286 + mfm_at | instructions identiques | disque |
|---|---|---|
| C: 286 amorcé, `MD G5`, `COPY AUTOEXEC.BAT G5`, `DIR G5` | 172 276 548 | C: identique, 2 552 octets écrits |
| C: + D: vierge : FDISK (disque 2, partition primaire, sortie), redémarrage, `FORMAT D: /S`, `DIR D:` | 234 087 348 | D: identique, 101 776 octets écrits |

Premier disque AT jamais comparé à l'oracle : vert du premier coup. L'arc a d'abord été
calibré en C# seul (`--boot`, 7 s), puis transposé — une tranche de boot-diff vaut environ un
cinquième de tranche `--boot`.

L'arc est versionné : `tools/diskarc/fdisk-format-d.keys`. Rejoué deux fois sur l'instantané de
la série : 233 566 322 instructions les deux fois (une passe manuelle antérieure, sur un CMOS de
session différent, en comptait 234 087 348 — les deux côtés restent identiques à chaque passe).

**La série**, comparée aux journaux de d6ca254 : **79 journaux sur 79 identiques hors durées**
(la ligne d'ABI passe de 27 à 28), et les deux portes nouvelles vertes. Selftest vert,
check-oracle 0 dérive (harness.c, harness.h, harness_stubs.c : empreintes rafraîchies après
édition délibérée).

## G5.1 — L'IDE, disque dur seul

Le 1er octobre 2026, sur 72ec467. Plan : `PLAN-G5.md` § G5.1.

**Transcrit.** `Ide/ide.cs` : `ide.c` en entier pour le disque dur ATA — IDENTIFY, READ et
WRITE (simples et MULTIPLE), VERIFY, FORMAT, SPECIFY, SEEK, RECALIBRATE, DIAGNOSTICS, CHECK
POWER MODE, SET MULTIPLE MODE, le reset logiciel par 0x3F6, les deux canaux (0x1F0/0x3F6
IRQ 14, 0x170/0x376 IRQ 15), les quatre lecteurs, l'adressage LBA. Omis, bruyamment : ATAPI
(`fatal` si un disque dur reçoit WIN_PACKETCMD, comme l'oracle) ; le bus master n'est pas
branché (pointeurs nuls, DMA sans effet — chez PCem aussi sur toute machine ISA).
`IDE.buffer` est rendu en octets, comme dans mfm_at.cs. DEVIATION : `cdrom_channel` vaut -1
(ATAPI hors G5), et `loadconfig` refuse une clé `cdrom_channel` ou `zip_channel` positive.
`pc.cs` monte « ide » et appelle `resetide()` (pc.c:395) ; `loadconfig` lit désormais aussi
hde_ et hdf_ (E:, F:, canal secondaire). `HardDiskControllers` propose « ide » sur un AT — le
SETUP et `--hdd-controller` le montrent d'office.

**Les défauts de PCem** : PB-71 (le secondaire teste l'IRQ 14), PB-72 (sélection pendant un
reset), PB-73 (READ/WRITE MULTIPLE sans taille de bloc : `fatal`), PB-74 (VERIFY d'un seul
secteur).

**Les portes.**

| boot-diff, contrôleur `ide` | instructions identiques | disque |
|---|---|---|
| ami286, C: 286, MD / COPY / DIR | 172 283 037 | C: identique, 2 552 octets écrits |
| ami286, C: + D: vierge (esclave primaire), FDISK + FORMAT D: /S | 233 559 631 | D: identique, 101 776 octets |
| ami386dx, C: 386, MD / COPY / DIR | 181 109 300 | C: identique, 7 589 octets |

Le BIOS AMI pilote l'IDE comme le MFM : même type 46 au CMOS, même INT 13h. Contrôles
négatifs, retirés : IDE_TIME à 11 µs → divergence à l'instruction 2 220 883 ; le passage de
tête de `ide_next_sector` décalé d'un cran → vert sur MD/COPY (aucune écriture ne franchit de
tête), rouge sur l'arc FORMAT (instruction 162 734 306). Auto-tests de l'hôte (`--setup-check`,
`--menu-check`, `--fat-check`) verts.

**La série**, sur un instantané de G5.1 et comparée aux journaux de 72ec467 : **81 journaux
sur 81 identiques hors durées**, et les trois portes IDE ci-dessus vertes. Un incident de
harnais, de mon fait : un nettoyage de /tmp pendant la passe (`find -mmin +20`, alors que
`File.Copy` conserve la date de la source) a supprimé la copie de disquette de
`bd-pcdos-cga` en cours ; l'amorçage était vert, CompareImages est tombé sur le fichier
absent (rc = 134). Rejouée seule sur le même instantané : verte, journal identique à celui
de G4.7. Règle depuis : aucun nettoyage de /tmp pendant une série. Selftest, check-oracle
0 dérive (122 : `Ide/ide.cs` entre au manifeste).

## G5.2 — Les deux canaux, dirigés : l'ide-check

Le 1er octobre 2026, sur 5df27b5. Plan : `PLAN-G5.md` § G5.2.

**L'outil.** Le harnais n'expose pas les ports : les commandes ATA sont donc écrites par
L'INVITÉ. `tools/idecheck/idecheck.py` assemble IDECHK.COM (637 octets, désassemblage vérifié) —
un interprète d'une table de 353 octets : écrire un registre, attendre BUSY puis DRQ, lire ou
écrire N mots, relever les sept registres, choisir le canal, écrire 0x3F6/0x376. Il est saisi
dans DEBUG par KeyScript (`idecheck.keys`), écrit sur C:, lancé sous boot-diff. `BootDiff`
copie désormais E: et F: par côté, les pousse à l'oracle et compare leurs images — sans quoi
le C# écrivait dans l'image source et l'oracle ne voyait pas le disque.

**Ce qu'il tire.** Canal primaire, C: en lecture seule : IDENTIFY, SET MULTIPLE MODE 4, READ
MULTIPLE LBA 0 × 8, READ CHS qui change de tête, VERIFY × 5 (PB-74 : le compte reste 5), CHECK
POWER MODE, SETIDLE, une commande inconnue, SET FEATURES (ABRT tous trois), l'esclave absent
(état 00, commande ignorée). Canal secondaire, E: vierge (IRQ 15) : IDENTIFY, WRITE LBA 100 × 2
et relecture, SET MULTIPLE MODE 2, WRITE MULTIPLE LBA 200 × 4 et READ MULTIPLE en relecture,
FORMAT d'une piste, SPECIFY, DIAGNOSTICS, RECALIBRATE, le reset logiciel par 0x376, la sélection
pendant un reset (PB-72), une commande avec nIEN, puis la réactivation — qui passe par
`ide_irq_update` et son masque d'IRQ 14 (PB-71). READ/WRITE MULTIPLE sans taille de bloc ne sont
pas tirés : PCem s'y arrête (PB-73), des deux côtés.

**La porte** : boot-diff ami286 + `ide`, C: 286 et E: vierge — **189 962 423 instructions
identiques**, C: identique (889 octets écrits : IDECHK.COM), E: identique (3 064 octets).
Contrôle négatif, retiré : un VERIFY qui remet `secount` à zéro → divergence à l'instruction
185 819 674.

**La série**, sur un instantané de G5.2 et comparée aux journaux de 5df27b5 : **84 journaux
sur 84 identiques hors durées** (`bd-pcdos-cga` comparé à celui de 72ec467, le journal de
5df27b5 étant celui de l'incident), et l'ide-check nouveau, vert. Selftest, check-oracle 0 dérive.

## R9 — L'invité ne tue pas l'hôte : PB-73, PB-75, PB-76

Le 1er octobre 2026, sur 4a15131. Règle de Julien, inscrite dans TRANSCRIPTION.md (R9).

**L'inventaire** des 74 défauts du registre et des `fatal()` de PCem transcrits. Déjà non
reproduits : PB-46, PB-47 (SIGFPE), PB-49 (récursion de POP SS). Restent reproduits, hors R9 :
PB-50 (1,7 million de préfixes dans un pas, mais l'hôte survit et le pas se termine), PB-26/27
(chaînes fausses, gel du Xebec sans arrêt de l'hôte), PB-31/33 (atteints par l'hôte, pas par
l'invité). Traités ici : PB-73 (IDE, READ/WRITE MULTIPLE sans taille de bloc → ABRT, comme le
disque réel), PB-75 (mfm_at, commande à l'unité 1 absente → ERR, ABRT, IRQ 14), PB-76 (mfm_at,
READ LONG / WRITE LONG → ABRT ; le vrai transfert avec ECC n'est modélisé ni ici ni chez PCem).
Reportés au hors plan de PLAN.md : les dix-sept `fatal()` de protocole du Xebec, et les accès
mémoire du 8087 en mod = 3.

**Le test de survie**, en C# seul (`--boot`), puisque l'oracle s'arrête sur ces chemins et que
les séries ne les lui envoient jamais : `tools/idecheck/idecheck.py --survie` assemble SURVIE.COM
(386 octets) — C4h et C5h sans SET MULTIPLE MODE, une commande à l'unité 1 absente, 22h, 32h —
saisi dans DEBUG et lancé sur l'ami286, contrôleur `ide` puis `mfm_at`. Les deux terminent :

| relevé (état, erreur) | ide | mfm_at |
|---|---|---|
| READ MULTIPLE sans bloc | 51 04 (ABRT) | 51 04 (commande inconnue) |
| WRITE MULTIPLE sans bloc | 51 04 | 51 04 |
| commande à l'unité 1 absente | 00 FF (IDE_NONE, ignorée) | 01 04 (PB-75) |
| READ LONG | 51 04 (inconnue) | 51 04 (PB-76) |
| WRITE LONG | 51 04 | 51 04 |

Contrôle : le même test sur un binaire d'avant R9 (G4.7) meurt — « iXtal26 FATAL: Command on
non-present drive », rc 134.

**La série** — la première EN PARALLÈLE (dix portes à la fois, les plus longues d'abord, chaque
porte disque dans son propre répertoire ; le .tsv reconstruit dans l'ordre canonique) : 62
minutes contre 192 en séquentiel, et **85 journaux sur 85 identiques** à ceux de 4a15131 hors
durées, mêmes codes de retour dans le même ordre. Aucune porte n'envoie les chemins de R9 à
l'oracle. Selftest, check-oracle 0 dérive.

## G5.3 — Les témoins de l'IDE

Le 1er octobre 2026. Plan : `PLAN-G5.md` § G5.3. iXtal26 seul (`--boot`), sur des copies
(/tmp/g5w), contrôleur `ide`, CMOS type 46 de `--make-nvr`.

| témoin | résultat |
|---|---|
| ami286, disque 286 de l'utilisateur, `CHKDSK C:` | 32 980 992 octets, 623 fichiers, aucune erreur |
| ami286, `WIN` | Windows 3.1, Gestionnaire de programmes |
| ami386dx, disque 386, `CHKDSK C:` puis `WIN` | idem : CHKDSK sans erreur, Gestionnaire de programmes (mode de Windows non relevé) |
| ami286, disque VIERGE de 156 Mo + disquettes MS-DOS 5 | SETUP partitionne, redémarre, formate, copie les trois disquettes ; la machine amorce ensuite sur C: jusqu'au DOS Shell |

La séquence de l'installation est versionnée : `tools/diskarc/dos5-install.keys`.
Les profils Rider restent en `mfm_at` (décision n° 4) ; « ide » par défaut sur l'ami486 est
reporté à G6, où la machine entre.

Aucun code ne change à G5.3 (une séquence de frappe et des documents) : l'état est celui de
2053035, dont la série est verte. Les témoins ont tourné sur le binaire de G5.2 ; R9 ne touche
aucun chemin qu'ils empruntent.

## Outillage des séries — `tools/gates/`, la recette de /tmp/g5w

Le 1er octobre 2026, sur 5909349. La liste des portes (`series.sh`, ordre canonique, sans les
anciennes gardes « depuis quelle étape »), le lanceur parallèle (`par.sh`, `REPO` et `WORK`
paramétrés, plus rien de `/tmp` en dur) et les configurations des portes (`cfg/`) entrent au
dépôt, avec la RECETTE de l'espace des portes disque (`g5w-recipe.sh`, `fatpatch.py`) et ses
empreintes (`g5w.sha256`) — pas les images, qui restent dans `os/`.

**Une référence refaite.** L'ancien /tmp/g5w n'était pas reproductible : sa copie de C:
descendait d'une copie que les témoins Windows de G4.7 avaient écrite. La recette repart de
`os/286-HDD-C.img` et `os/386-HDD-C.img` ; deux fabrications donnent les mêmes empreintes. La
série de preuve (parallèle, 62 minutes) : **81 journaux identiques** à ceux de 2053035 (aux
chemins des configurations près, `/tmp/g4/` devenu `tools/gates/cfg/`), mêmes codes de retour
dans le même ordre ; les quatre portes qui amorcent C: restent vertes avec un autre contenu
écrit — MD/COPY 2 527 octets au lieu de 2 552 (ami286, mfm_at et ide), ide-check 885 au lieu
de 889, ami386dx 181 109 674 instructions au lieu de 181 109 300 et 7 920 octets au lieu de
7 589. Ce sont désormais les références.

TRANSCRIPTION.md revient sous son plafond (215 lignes pour 240) : l'historique de R3 va dans
`iXtal26/Docs/doctrine-historique.md`, les « Faits vérifiés » dans
`iXtal26/Docs/faits-verifies.md`, inchangés ; aucune règle ne quitte le fichier.

## G6.0–G6.1 — Le cœur 486 : H_CORE_486, cpus_i486, cpu_set ; INVLPG

Le 1er octobre 2026, sur 1bf071c. Plan : `PLAN-G6.md` § G6.0, § G6.1 (et INVLPG, avancé de
§ G6.2). **Un seul commit pour deux étapes**, et c'est une nécessité : le cœur 486 du fuzzeur
(G6.0) n'a pas d'existence sans `cpus_i486` et la machine `ami486` (G6.1) — PCem n'ayant pas de
table d'opcodes 486, « le cœur 486 » n'est qu'exec386 sur une autre table de CPU. Décision n° 1 :
**Intel seul** (décision de Julien du 01/10) — ni `cpus_Am486` ni `cpus_Cx486`, marqués
`// omitted:` à leur place dans cpu.c et cpu_tables.c.

**L'oracle** : `H_CORE_486` (ABI 29), la carte plate de 16 Mo du 386, `h_model_ami486` (table
Intel seule, init refusée par h_boot jusqu'à G6.3). **Le C#** : `cpus_i486` (treize 486 et deux
Pentium OverDrive, exclus — décision utilisateur du 03/10), les cas i486 / iDX4 de `cpu_set`
(iDX4 retombant dans i486, comme le C), `CPUID`, `cpu_multi`, `has_vlb`, `cpu_CR4_mask`, `cpu_CPUID` de l'i486DX et de l'iDX4, la machine
`m_ami486` (init `at_ali1429_init` : arrêt « non transcrit » jusqu'à G6.3), INVLPG
(`0F 01 /7`, x86_ops_pmode.h:463-472) et `mmu_invalidate`. **L'outil** : `--core 486`,
`Reset486`, l'empreinte CPU élargie de cinq champs (CPUID, cpu_features, cpu_CR4_mask, cpu_multi,
has_vlb).

**Un défaut de transcription, trouvé par le fuzzeur et corrigé** : `readmemwl` et `writememwl`
omettaient `cycles -= timing_misaligned` (« nul sur un 8088 », vrai jusqu'au 386) — 3 cycles par
accès mal aligné sur un 486. Divergence `01 47 12` (ADD m16 à DS:A143), oracle 29 cycles, C# 23.

**Deux chutes de l'oracle, et pourquoi le fuzzeur évite désormais `0F 07` sur le 486** : PCem
exécute LOADALL386 sur un 486 (table partagée, PB-78) ; tiré au hasard, il charge un CR0 avec PG
et un CR3 quelconque, et la traduction de page déréférence `_mem_exec` hors RAM — segfault
(graine 1, itération 2 588 ; puis `DB E7 0F 07`, un ESC à coût nul qui fait exécuter l'octet
suivant). Le fuzzeur du cœur 486 remplace tout `0F 07` de son tampon par `0F 06` ; celui du 386
garde son tirage. Le défaut de fond — une table de pages hors RAM fait tomber PCem — relève de
R9 : PB-79, en G6.2.

**Les portes.** Les treize entrées Intel ont une empreinte identique des deux côtés, cinq
champs 486 compris ; les deux Pentium OverDrive s'arrêtent (cpu_set) — exclus, décision
utilisateur du 03/10. Fuzzeur `--core 486`, tous verts : 256 opcodes graines 1 et 7 (i486SX/16), i486DX2/66 et iDX4/100
(80 000 chacun), flux i486SX et DX2 (300 000), les `0F` propres au 486 (08, 09, 01, A2, B0/B1,
C0/C1, C8-CF ; 40 000), l'iDX4 sur CPUID et MOV CRx, le x87 intégré du DX2 (`--x87 all`,
single et flux). Contrôle négatif, retiré : `timing_bt` du 486 faussé → « CONFIGURATION CPU
DIVERGENTE : FNV des 28 timing_* » dès le reset.

**Les défauts de PCem** : PB-77 (`cpu_features` jamais remis à zéro), PB-78 (LOADALL386 sur 486).
`cpu-config-check` n'entre l'ami486 qu'en G6.3 : il AMORCE chaque machine, et l'ami486 n'a pas
encore son chipset.

**La série** (parallèle, 78 minutes), comparée aux journaux de 1bf071c : **85 journaux sur 85
identiques hors durées** (la ligne d'ABI passe de 28 à 29), et les dix portes 486 nouvelles
vertes. Selftest, check-oracle 0 dérive.

## G6.2 — Le mode protégé du 486 ; PB-79 sous R9

Le 1er octobre 2026, sur b28b93e. Plan : `PLAN-G6.md` § G6.2 (INVLPG est entré avec G6.1).

**PB-79, non reproduit (R9).** `mmu_readl` / `mmu_writel` de PCem déréférencent `_mem_exec`
hors RAM : un invité qui pagine sur une table inexistante arrête l'émulateur. Le C# s'y arrêtait
par `fatal` ; il rend désormais 0xFFFFFFFF en lecture (bus ouvert) et ignore l'écriture — sur le
386 comme sur le 486, qui partagent mem.cs. Ces deux fonctions n'ont pas d'autre appelant que la
traduction de page. `r9-mmu` (C# seul) : pagination active, CR3 à FFFFF000, 7F000000 et
01000000, un MOV qui lit puis écrit — les six cas survivent ; contrôle : le mem.cs de b28b93e
s'arrête sur les six (« mmu_readl hors de _mem_exec »).

**Les portes du mode protégé, en 486** (`--core 486` ajouté à page-check, pm-check, pm-fuzz) :
page-check 200 000 traductions, pm-check 106 cas sur 106, pm-fuzz 20 000 (cinq départs, V86
compris), pm-fuzz x87 sur les huit ESC 20 000 — tous verts.

**La série** (parallèle, 80 minutes), comparée aux journaux de b28b93e : **95 journaux sur 95
identiques hors durées**, et les cinq portes nouvelles vertes (page-check, pm-check, pm-fuzz et
pm-fuzz x87 en 486 ; `r9-mmu`). Selftest, check-oracle 0 dérive.

## G6.3 — L'ami486 : ALi 1429, BIOS, CMOS ; la cadence du DX2/66

Le 1er octobre 2026, sur c673ca6. Plan : `PLAN-G6.md` § G6.3.

**Transcrit.** `Models/ali1429.cs` (ali1429.c entier : ports 0x22/0x23, ombrage de C0000h-FFFFFh
par blocs de 32 Ko selon les registres 0x13 et 0x14) ; `at_ali1429_init` ; les trois
`ali1429_reset` de pc.c (:191, :317, :403), sur toute machine comme le C ; le chargement de
`ami486/ami486.bin` (mem_bios.c:569-576) ; les cas `ROM_AMI486` de loadnvr / savenvr ; le CMOS
de référence de PCem, `nvr/default/ami486.nvr`, et `--make-nvr` pour l'ami486. L'oracle lie
`ali1429.c`, appelle `ali1429_init` dans h_boot et les trois resets aux points miroirs.

**Les portes.**
- boot-diff ami486 + i486DX2/66 : 3 000 tranches, 5 433 745 instructions ; jusqu'au POST
  complet, 40 000 tranches, **73 097 655 instructions identiques**, sonde VGA identique.
- `cpu-config-check` avec l'ami486 en dernier (PB-77 : l'ordre décide de ce que `cpu_features`
  hérite) : **80 configurations identiques**, 7 refus concordants ; les deux Pentium OverDrive
  de cpus_i486 sont sautés et le disent (exclus, décision utilisateur du 03/10 ; cpu_set les arrête).
- `--timer-check 60` sur l'ami486 DX2/66, CMOS fabriqué : **66,663 MHz invités, −58,69 ppm**
  (l'écart du 286 de M16) ; rapport invité / contractuel 1,000058 ; l'hôte tient le DX2/66 avec
  une marge de 2,9 (28,3 MIPS invités).
- Témoin `--boot` : « AMIBIOS (C)1993 », « Main Processor : 486DX or 487SX », « Numeric
  Processor : Present », 3 072 Ko étendus. Sans CMOS fait pour lui (la référence de PCem), le POST
  s'arrête sur « CMOS memory size mismatch » : c'est ce CMOS-là qui le dit, pas le C#.

**La série** (parallèle, 82 minutes), comparée aux journaux de c673ca6 : **99 journaux sur 100
identiques hors durées** — `cpu-config-check` élargi à l'ami486 (67 → 80 identiques, deux
entrées sautées) — et les deux boot-diffs de l'ami486 nouveaux, verts. Selftest, check-oracle
0 dérive (123 : `Models/ali1429.cs` entre au manifeste).

## G6.4 — La machine complète et les témoins ; G6 fait

Le 1er octobre 2026, sur a293168. Plan : `PLAN-G6.md` § G6.4.

**La machine.** L'IDE devient le contrôleur par défaut de l'ami486 (`HardDiskControllers`,
décision n° 4 de PLAN-G5.md) ; les autres AT gardent `mfm_at`. Profil Rider « iXtal26 (486DX2/66,
IDE, Trident 9000B, sans turbo) » sur `ixtal26-486.cfg` (décision n° 3) — i486DX2/66, `fpu =
builtin`, 4 Mo, IDE type 46 sur `os/486-HDD-C.img`. Ce fichier-là et le CMOS de session ne sont
PAS fabriqués par le dépôt : la configuration dit comment (copier un disque, `--make-nvr`). Sur
une copie, le profil amorce jusqu'à `C:\>`.

**Les portes nouvelles** (recette : `c486.nvr`, empreintes refaites) : boot-diff ami486 DX2/66
+ IDE, MD / COPY / DIR sur le disque 386 — **183 802 720 instructions identiques**, C: identique
(7 920 octets écrits) — et l'ide-check sur les deux canaux de l'ami486.

**Les témoins** (`--boot`, copies dans /tmp/g6w, CMOS fabriqué) :

| témoin | ami486 + i486DX2/66, IDE |
|---|---|
| POST AMI 1993 | « 486DX or 487SX », « Numeric Processor : Present », 3 072 Ko étendus |
| MSD /S | « Computer: American Megatrend, 486DX » |
| CHKDSK C: | 32 980 992 octets, sans erreur |
| Windows 3.1, `WIN /3` (386 étendu forcé) | Gestionnaire de programmes |
| X87BANC.COM | **31 tics** — contre 98 (386DX/33 + 387) et 297 (286/16 + 287) |
| `--timer-check 60` | 66,663 MHz, −58,69 ppm, marge 2,9 en temps réel |

La porte ide-check de l'ami486 : **202 649 589 instructions identiques**, C: et E: identiques
(3 064 octets écrits sur E:).

**La série** (parallèle, 80 minutes), comparée aux journaux de a293168 : **101 journaux sur 101
identiques hors durées**, et les deux portes IDE de l'ami486 nouvelles, vertes. Selftest,
check-oracle 0 dérive.

## G7.0 — Le socle SVGA : la fenêtre linéaire, le rendu 32 bpp

Le 1er octobre 2026, sur b069c1f. Plan : `PLAN-G7.md` § G7.0 (feu vert pour G7.0 à G7.2 ; G7.3,
l'accélérateur S3, attend la décision de l'utilisateur).

**Transcrit.** `svga_write_linear` et `svga_read_linear` (vid_svga.c:1137-1441) — le corps du
switch d'écriture est celui de `svga_write` à la ligne près (vérifié par diff), seule la tête
change : pas de banque, pas de `addr &= ~3` en chain4 ; la lecture calcule ses verrous depuis
l'adresse non bancaire et, en chain4 compact, ne les charge pas (PB-80) — et leurs formes 16 et
32 bits (`:1573-1658`). Les corps des rendus 32 bpp (vid_svga_render.c:791-854), jusqu'ici
`fatal()`. Aucune carte du dépôt n'installe encore de fenêtre linéaire.

**La porte : `svga-linear-check`**, un banc dirigé qui appelle les six accès linéaires DES DEUX
CÔTÉS (P/Invoke sur les fonctions de la .so) sur une VGA du 5150 amorcée à l'identique — au pas
commun du boot-diff : l'oracle trace, le C# avance instruction par instruction ; une tranche de
`pc.runpc()` n'est PAS le pendant d'une de `h_runpc` (mesuré : la sonde divergeait sur `vc`).
50 000 opérations tirées : écritures et lectures 8/16/32 bits à des adresses de 512 Ko,
entremêlées d'écritures aux registres GDC 0-8 et séquenceur 2 et 4 ; chaque valeur lue est
comparée, et la sonde VGA (VRAM, verrous, registres) toutes les 1 000 opérations. **Vert.**
Contrôle négatif, retiré : le plan de lecture du chain4 décalé d'un cran → divergence à
l'opération 134 (`svga_read_linear(224F9)`, oracle 07, C# 00). Limite : la VGA n'a ni
`packed_chain4` ni `fb_only`, donc ni les chemins « fast » ni PB-80 ; la GD5429 (G7.1) les
atteindra. Le rendu 32 bpp attend la S3.

**La série** (instantané figé, 10 travaux, 106 portes, comparée à g64 par nom, durées ôtées) :
105 identiques à g64, plus `svga-linear-check` verte. **Un rouge, expliqué et antérieur à
G7.0** : `bd-pcdos-vga` (5150 + VGA, PC-DOS 2.0) diverge à l'instruction 1 078 553, F000:E36A —
l'oracle lit 508C en C600:0000, le C# 0000. Rejouée seule deux fois, puis dix fois en
parallèle : verte. Cause : `vid_vga.c:107` charge `ibm_vga.bin` à partir de 0x2000 dans une
allocation de 32 Ko — 24 Ko lus, 8 Ko de tas en C6000-C7FFF (`rom.c:60-62`, classe PB-24) — et
le balayage des ROM d'extension du 5150 lit C600:0000 (DX = C600). `MALLOC_PERTURB_=85` rend la
divergence déterministe sur l'instantané G7.0 (même instruction, oracle AAAA). G7.0 ne touche
ni l'oracle, ni `rom.c`, ni `vid_vga.c` : le défaut date de M15. Correction en G7.1 (une
enveloppe de `rom_init` dans l'oracle, PB-24 élargi). Commit sur ce constat, accord du pair :
on ne rejoue pas une série aux dés.

## G7.1 — La Cirrus Logic GD5429 ; la ROM VGA de l'oracle ; PB-81, PB-82

Le 1er octobre 2026, sur 62c401e. Plan : `PLAN-G7.md` § G7.1 (décision n° 1 : la GD5429 seule).

**Transcrit.** `vid_cl5429.c` pour `gd5429_device` (`Video/vid_cl5429.cs`) : `gd5429_t`, les
ports (séquenceur et GDC étendus, DAC caché par 3C6), les deux banques (GR9/GRA), les fenêtres
— banque, linéaire, MMIO en B8000 —, `gd5429_recalctimings`, le curseur matériel, les modes
d'écriture étendus 4 et 5, les verrous de 8 octets, l'adressage X8, le blitter, `cl_init` ;
`mem_mapping_set_handler` (`mem.c:1177-1190`). Omis : MCA, PCI, les dix autres cartes de la
famille. Le registre `VIDEO_CARD` gagne `v_cl_gd5429` (`video.c:99-100`, temps VLB 4/4/8,
10/10/20), `pc.GFX_CL_GD5429` = 19, et l'écran de construction la propose (deux contrôles de
`--setup-check`, vingt et un en tout).

**L'oracle.** `harness_cl5429.c` inclut `vid_cl5429.c` (la `gd5429_t` est privée), sur le patron
de `harness_tvga.c` ; `h_boot` monte `gd5429_device` ; souches fatales `pci_add`,
`pci_add_specific`, `mca_add`. **La sonde VGA passe de 86 à 102 champs** : seize de la carte —
type, banques, masques, DAC caché, `lfb_base`, `mmio_vram_overlap`, SR10/SR11, verrous
étendus — et le curseur matériel. ABI 30. Contrôle négatif, retiré : `type` faussé d'une unité
côté C# → « gd5429.type oracle 3 | C# 4 », la sonde voit bien la carte.

**Les boot-diffs** (`--gfxcard cl_gd5429`) :

| Machine | Tranches | Instructions | Sonde |
|---|---:|---:|---|
| ami486, i486DX2/66 (VLB) | 3 000 | 5 462 861 | 102 identiques |
| ami486, POST complet | 40 000 | 73 130 516 | 102 identiques |
| ami386dx (ISA, `has_vlb` = 0) | 3 000 | 4 980 606 ¹ | 102 identiques |
| ami486, DOS sur IDE, `VER` et `DIR` | 100 000 | 183 847 196 | 102 identiques |

¹ Remesuré le 3 octobre 2026 en porte isolée (CMOS de `nvr/default/`), au commit ee101d5 :
5 285 528, d'abord consigné, venait d'un lancement qui lisait le CMOS de session de l'utilisateur
(voir § G1.0). Instructions identiques des deux côtés dans les deux cas.

**PB-80, le jumeau.** `gd5429_read_linear` sort sans charger les verrous en chain4 compact
(`vid_cl5429.c:1183-1187`), et la forme par banque y passe : reproduit, marqué.

**R9 : PB-81 et PB-82, non reproduits.** La lecture du motif du blitter ajoute jusqu'à 127
octets APRÈS le masque de la source (`:1415-1424`) ; les modes 4 et 5 sans X8 écrivent jusqu'à
`addr + 7` sur une adresse alignée sur 4 (`:851-871`, `:911-931`). Hors du tableau `vram` :
lecture ou écriture du tas en C, exception en C#. Index masqués par `vram_mask`, DEVIATION.
**`r9-cl5429`** (C# seul) : BitBLT en motif, source 1FFFF8, 8 et 16 bpp ; modes 4 et 5,
GRB = 04, écriture au dernier mot de la VRAM — quatre survies. Contrôle négatif, retiré : sans
les gardes, quatre `IndexOutOfRangeException`.

**La ROM VGA de l'oracle** (le rouge de § G7.0). `__wrap_rom_init` (`harness_stubs.c`,
`WRAP_BOTH`) met à zéro ce que le fichier ROM n'a pas fourni : la VGA d'IBM (8 Ko en
C6000-C7FFF), le Xebec et le DTC de PB-24. Avant : `MALLOC_PERTURB_=85` fait diverger
`bd-pcdos-vga` à l'instruction 1 078 553 (oracle AAAA) ; après : vert. PB-24 élargi, note de
M15 corrigée.

**La série**, sur un instantané de G7.1, **sous `MALLOC_PERTURB_=85`** (chaque `malloc` de
l'oracle rendu plein d'un motif non nul : une dépendance au tas y devient un rouge certain),
comparée à g70 par nom, durées ôtées : 110 portes, **toutes vertes**. Les 105 anciennes sont
identiques à g70, à deux écarts attendus près : l'ABI (29 → 30) et la sonde (« 86 champs » →
« 102 champs »). `bd-pcdos-vga`, rouge en g70, est verte. Les cinq nouvelles portes sont vertes
(les quatre boot-diffs du tableau, `r9-cl5429`). Aucune autre dépendance au tas : la ROM VGA
était la seule. Build 0 avertissement, selftest, check-oracle 0 dérive.

## G7.2 — Le blitter de la GD5429 : le banc dirigé BLTBANC

Le 1er octobre 2026, sur ee101d5. Plan : `PLAN-G7.md` § G7.2.

Le blitter (`gd5429_start_blit`, `vid_cl5429.c:1302-1599`) et son MMIO (`:1600-1805`) sont
transcrits depuis G7.1, mais aucun BIOS ne les atteint : il faut un programme qui les programme.
**`tools/bltbanc/bltbanc.py`** assemble BLTBANC.COM (1 101 octets, listing annoté, désassemblage
vérifié par objdump), un interprète sur le patron d'IDECHK : écrire un registre, appeler
l'INT 10h, remplir la VRAM par une banque, programmer un BitBLT (dix-sept valeurs GR20-GR32,
puis GR31 = 02), alimenter un BitBLT « source système » par des mots en A000, attendre la fin
(GR31 bit 0), relire la VRAM en somme tournante, écrire en MMIO (B8000), relire ou modifier un
registre. Saisi dans DEBUG par KeyScript, écrit sur C:, lancé ; porte `bd-ami486-gd5429-blt`.

Le script, en 640 × 480 × 256 (mode 5Fh du BIOS Cirrus), pas de 640 : copie avant et arrière ;
motif 8 × 8 ; expansion de couleur, transparente ; motif en expansion ; les seize ROP du switch
(`:1466-1526`) ; le masque de début de ligne (GR2F) ; 16 bpp (copie, motif, expansion) ;
source système, copie et expansion ; un BitBLT entier programmé en MMIO (SR17 bit 2) ; puis
six sommes de la VRAM touchée. La sonde de fin compare la VRAM entière, les registres de la
carte et le framebuffer en 256 couleurs.

**Résultat** : vert, 261 046 800 instructions, sonde 102 champs identiques ; 1 460 octets écrits
sur C: (le .COM). **Le banc fait ce qu'il dit** : une trace temporaire de `gd5429_start_blit`
côté C# compte 29 BitBLT — mode 00 dix-neuf fois (seize ROP, la copie, le masque, le MMIO),
01, 04, 10, 40, 50, 80, 84, 88, 90, C0 une fois chacun. **Contrôle négatif**, retiré avec la
trace : la ROP 0x0D (`dst = src`) faussée d'un bit côté C# → divergence à l'instruction
256 679 589, dans les relectures.

Limite : la sonde ne lit pas l'état interne du blitter (`blt.*`) ; elle en voit les effets
(VRAM) et le CPU en lit le statut (GR31). Le témoin Windows reste en VGA (décision n° 4 : aucun
pilote Cirrus sur les images) : le banc fait foi.

**La série**, sur un instantané de G7.2, sous `MALLOC_PERTURB_=85`, comparée à g71 par nom,
durées ôtées : 111 portes, toutes vertes ; les 110 journaux anciens identiques, la nouvelle
`bd-ami486-gd5429-blt` verte (261 046 800 instructions, 1 460 octets écrits sur C:).

## G7.3 — La S3 Trio64 Phoenix, accélérateur synchrone ; PB-83 à PB-86

Le 2 octobre 2026, sur 80e8906. Plan : `PLAN-G7.md` § G7.3 ; décisions n° 2 (la Trio64 Phoenix,
`86c764x1.bin`) et n° 3 (l'accélérateur synchrone des deux côtés, utilisateur, 01/10).

**Transcrit.** `vid_s3.c` pour `s3_phoenix_trio64_device` (`Video/vid_s3.cs`) : `s3_t`, les
ports et le CRTC étendu (déverrouillage CR38/CR39, banques CR35/CR51/CR6A), `s3_recalctimings`,
`s3_updatemapping` (banque, fenêtre linéaire, MMIO en A0000), l'horloge de la Trio64, le
curseur matériel, les registres de l'accélérateur par port et par MMIO (`packed_mmio`), et
l'accélérateur 2D entier (`s3_accel_start` : lignes, rectangles, BitBLT, motif, polygones).
Omis : Vision864, 9FX, Trio32, le SDAC (que la Trio64 n'appelle pas). `v_px_trio64` au
registre (`video.c:164-166`, temps VLB 3/2/4, 25/25/40), `pc.GFX_PHOENIX_TRIO64` = 22, proposée
par l'écran de construction (vingt-trois contrôles de `--setup-check`).

**L'accélérateur synchrone** (DEVIATION des deux côtés). PCem le fait tourner dans un thread
(`fifo_thread`, `vid_s3.c:840-887`), nourri par `s3_queue`. Côté oracle, `harness_s3.c` inclut
`vid_s3.c` et DÉFINIT les souches de `thread.h` — ni la bibliothèque de threads ni `pci.c`
ne sont liées, il n'y a pas de `__real` à envelopper — : `thread_set_event(wake_fifo_thread)`
vide la FIFO sur-le-champ par le corps recopié de la boucle de `fifo_thread`. Côté C#,
`s3_fifo_drain`, le même corps. Effet observable : la carte n'est jamais vue « occupée » et la
FIFO ne se remplit jamais — `s3_queue` réveille à moins de 8 entrées (`:911-912`), donc à
chaque entrée. `timer_read` (statistiques de l'hôte) est omis.

**PB-83, mesuré** : sans PCI, `pci_add` rend -1 (`pci.c:189-190`), et `s3_update_irqs`
(`vid_s3.c:161-166`) appelle `pci_set_irq` / `pci_clear_irq(-1)` à chaque trame : lecture de
`pci_irq_routing[-1]` et, s'il est non nul, écriture de `pci_irq_active[-1]`. R9 : aucune IRQ,
rien d'écrit, des deux côtés (souches vides de l'oracle, branches vides du C#). La souche
`pci_add` de l'oracle rend -1 sans PCI, comme le vrai.

**PB-84 à PB-86, R9**, relevés à la relecture du brouillon : le curseur lu hors VRAM (S3 : adresse
non masquée ; GD5429 : en entrelacé), le curseur écrit après `buffer32` à la dernière ligne (les
deux cartes), la pente d'un polygone INT_MIN / -1 (SIGFPE chez PCem). **`r9-s3`** et
**`r9-cl5429`** élargi : survit ; contrôle négatif, gardes retirées : quatre arrêts
(`IndexOutOfRangeException` ×3, `OverflowException`).

**L'oracle.** `h_s3_probe` : **la sonde passe de 102 à 122 champs** (vingt de la `s3_t` : puce,
identifiants, banque, `ma_ext`, largeur, bpp, fenêtre linéaire, statuts, commande, positions,
couleurs, masques, mélanges, compteurs internes, indices de la FIFO, `blitter_busy` et
`force_busy`, couleurs du curseur). ABI 31. Lié : `vid_sdac_ramdac.c` (référencé, non appelé) ;
`timer_read` souché en arrêt bruyant.

**Les boot-diffs** (`--gfxcard px_trio64`) :

| Machine | Tranches | Instructions | Sonde |
|---|---:|---:|---|
| ami486, i486DX2/66 (VLB) | 3 000 | 5 443 326 | 122 identiques |
| ami486, POST complet | 40 000 | 73 108 330 | 122 identiques |
| ami386dx (ISA, `has_vlb` = 0) | 3 000 | 4 989 579 ¹ | 122 identiques |
| ami486, DOS sur IDE, `VER` et `DIR` | 100 000 | 183 816 380 | 122 identiques |

¹ Remesuré le 3 octobre 2026 en porte isolée (CMOS de `nvr/default/`), au commit f727e2c :
5 272 247, d'abord consigné, venait d'un lancement qui lisait le CMOS de session de l'utilisateur
(voir § G1.0). Instructions identiques des deux côtés dans les deux cas.

**Le banc S3BANC** (`tools/s3banc/s3banc.py`, 1 493 octets, sur le patron de BLTBANC), porte
`bd-ami486-trio64-accel` : VESA 101h, registres déverrouillés, CR40 ; rectangles sous les seize
mélanges ; BitBLT avant et arrière ; motif 8 × 8 ; lignes radiales dans les huit directions ;
données du CPU par E2E8 en couleur et en expansion monochrome ; sélection par la mémoire
d'affichage ; ciseaux ; un rectangle en MMIO (CR53) ; relevés du statut et sommes de VRAM.
**Vert** : 313 940 329 instructions, sonde 122 champs identiques. **Le banc fait ce qu'il dit** :
une trace temporaire de `s3_accel_start` compte 34 commandes — les 31 du script, toutes
présentes, et trois 40B3 du BIOS au changement de mode. **Contrôle négatif**, retiré avec la
trace : le mélange 7 (`dest = src`) faussé d'un bit → divergence à l'instruction 309 530 138.

**La série**, sur un instantané de G7.3, sous `MALLOC_PERTURB_=85`, comparée à g72 par nom,
durées ôtées : 117 portes, **toutes vertes**. Les 111 anciennes identiques à g72, aux écarts
attendus près — ABI 30 → 31, « 102 champs » → « 122 champs », `r9-cl5429` élargi au curseur ;
les six nouvelles vertes (les quatre boot-diffs du tableau, le banc, `r9-s3`). Build 0
avertissement, selftest, check-oracle 0 dérive (129), `--setup-check` vingt-trois contrôles.

## G7.4 — Les machines et les témoins ; G7 fait

Le 2 octobre 2026, sur f727e2c. Plan : `PLAN-G7.md` § G7.4, décisions n° 4 et n° 5.

**Les profils** (décision n° 5). `ixtal26-486.cfg` passe de la Trident 9000B à la Cirrus GD5429 :
profil Rider « iXtal26 (486DX2/66, IDE, Cirrus GD5429, sans turbo) ». Nouveau
`ixtal26-486-s3.cfg`, la même machine avec la Trio64 Phoenix : profil « iXtal26 (486DX2/66, IDE,
S3 Trio64, sans turbo) ». Même disque, même CMOS : `--make-nvr` fabrique un CMOS identique pour
les deux cartes et identique à celui de G6 (l'octet d'équipement ne voit que « VGA ») — le
`nvr/.ami486.nvr` de session reste valable. `--setup-check` : vingt-trois contrôles.

**KeyScript** gagne trois touches pour le SETUP DOS de Windows : `\` (disposition US), Haut et
Bas, écrites `\x18` et `\x19` (les flèches de la page de code 437). Aucune touche existante ne
change.

**Les témoins** (`--boot`, C# seul, dans /tmp/g7w : binaire isolé, copie du disque 386 sans
`KEYB FR`, CMOS fabriqué ; copies des sept disquettes IBM Windows 3.11) :

| témoin | ami486 + GD5429 | ami486 + Trio64 |
|---|---|---|
| BIOS de la carte au POST | (passé avant la tranche 250) | « Phoenix S3 TRIO64 Enhanced VGA BIOS. Version 1.5-07 EDO » |
| POST AMI | « 486DX or 487SX », « Display Type : VGA/PGA/EGA » | idem |
| `VER` | MS-DOS Version 5.00 | idem |
| `MSD /S` | « Video: VGA, Cirrus » | « Video: VGA, Phoenix » |
| `MODE CO40`, `MODE CO80` | 40 colonnes, puis 80 | idem |
| Windows 3.1, `VGA.DRV` | Gestionnaire de programmes, 640 × 480 | idem |
| Windows 3.1, **Super VGA 640 × 480 × 256** (`SVGA256.DRV`, VESA) | **Gestionnaire de programmes** | **« An error occured while trying to initialize the video adapter »** |

**Décision n° 4, corrigée.** Sa prémisse — « aucun pilote Cirrus ou S3 sur les images, seul
VGA.DRV » — était fausse : la disquette 2 de l'IBM Windows 3.11 (`os/IBM_Windows_3-11/disk02.img`)
porte `SUPERVGA.DR_` et `SVGA256.DR_`, les pilotes Super VGA génériques par le BIOS VESA. Sur une
COPIE du disque, le SETUP DOS de Windows (`CD WINDOWS`, `SETUP`, sept fois Haut, Entrée, trente
fois Haut, sept fois Bas : « Super VGA 640x480 256 colors ») installe `SVGA256.DRV` depuis la
disquette 2, puis les polices des disquettes 3 et 5, et écrit `display.drv=svga256.drv`. Le SETUP
avertit d'un `SETUP.INF` de version différente de son `SETUP.EXE` : l'état du disque de
l'utilisateur, pas un défaut.

**Sous boot-diff** (90 000 tranches, `WIN` tapé à 60 000, disque Super VGA) :
- GD5429 : **164 276 782 instructions identiques**, sonde 122 champs identiques — framebuffer en
  256 couleurs compris.
- Trio64 : **164 899 485 instructions identiques**, sonde identique, et l'ORACLE affiche le même
  message : l'échec de SVGA256 sur la Trio64 Phoenix est le comportement de PCem, reproduit à
  l'identique. Consigné comme constat, non corrigé (hors périmètre ; à instruire si l'on veut
  un jour un pilote VESA sur la S3 : le BIOS Phoenix ou le VESA de PCem).

Les originaux ne sont pas touchés : `sha256sum -c os/os.sha256` vert (les deux images PC-DOS
2.0 qu'il couvre) ; les disquettes Windows gardent leur date de 2020, `386-HDD-C.img` celle du
29/09, et la copie de la disquette 2 est restée identique à l'original après le SETUP.

**La série**, sur un instantané de G7.4, sous `MALLOC_PERTURB_=85` : 117 portes, toutes vertes,
les 117 journaux identiques à g73 (durées ôtées). Les trois touches de KeyScript ne changent
aucune frappe existante.

## G1.0 — Les tables 8086 et le fuzzeur 8086 ; PB-87

Le 2 octobre 2026, sur 038bc41. Plan : `PLAN-G1.md` § G1.0 (validé sous mandat).

**Le cœur 8086 était déjà transcrit** (M1) : PCem n'en a pas de distinct, `cpu_set` pose
`is8086` et `808x.c` branche dessus (file de 6 octets, mots sans pénalité). Manquaient les
tables : `cpus_8086` (`cpu_tables.c:53-61`) et `cpus_pc1512` (`:63-66`), et les deux machines
dans la table des modèles (`m_pc1512`, `m_olivetti_m24`, `model.c:885-894`, `:948-957`), sans
init jusqu'à G1.1 et G1.2 — refus bruyant des deux côtés (`initpc`, et un refus explicite dans
`h_boot`). Les remises à zéro d'`is8086` du harnais précèdent le vrai `cpu_set`, qui le repose :
elles n'écrasent rien.

**Le fuzzeur 8086** : `--core 8086` (ABI 32), le MÊME `execx86` sur l'Olivetti M24 —
`h_reset` et `_808x.Reset8086()` font tourner `cpu_set` sur `cpus_8086[--cpu]`, `xt_cpu_multi`
suit la vitesse de l'entrée. Les comparaisons numériques de cœur de l'outil (`core >=
Core386`, qui aurait donné au 8086 — valeur 4 — l'état d'un 386) deviennent des prédicats
(`Is386Class`, `Exec386`). Contrôle négatif, retiré : la file du 8086 ramenée à 5 octets côté
C# → divergence à la première instruction en 8086, 8088 vert.

**PB-87, trouvé par le fuzzeur 8086 en flux** (graine 1, ronde 325) : au repli de l'IP, le
préfetch du 8086 lit `cs + cpu_state.pc` (`808x.c:150`), `pc` valant alors 0x10001, non masqué.
La transcription de M1 lisait `cs + prefetchpc` — le geste du vrai 8086 —, branche que le 8088
n'atteint pas. Localisé par une trace temporaire de l'état du préfetch des deux côtés
(`prefetchw`, `fetchcycles`, `fetchclocks`, `prefetchpc`), retirée : l'oracle remplissait la file
de cinq octets et comptait 13 cycles, le C# un octet et 4. Corrigé, reproduit ; le flux 8086
complet (1 500 × 200) est vert. La première série de G1.0, lancée avant le correctif, a été
arrêtée et relancée sur un instantané corrigé.

**Les portes** : `cpu-config-check --inverse` (le balayage dans l'autre ordre, risque n° 1) ;
fuzzeur 8086 single (graines 1 et 7, et 8086/16 graine 3), flux, x87 8087 single et flux.

**La série**, sur un instantané de G1.0 construit dans un worktree de HEAD (pour exclure G1.1,
déjà en cours dans le dépôt), sous `MALLOC_PERTURB_=85` : 124 portes, **toutes vertes** ; les
sept nouvelles vertes. Comparée à g74 : identique, sauf l'ABI (31 → 32) et les sept boot-diffs
de l'ami386dx (+53 à +548 instructions, identiques des deux côtés). **Cause, mesurée** : dans le
dépôt, ces portes lisent le CMOS de session `nvr/.ami386dx_opti495.nvr` de l'utilisateur ; le
worktree ne l'a pas et retombe sur `nvr/default/`. L'instantané de G7.4 rejoué depuis le worktree
donne le même compte que G1.0 (4 437 159 pour `bd-ami386dx-4m`) : l'environnement, pas le code.

## G1.1 — L'Olivetti M24 ; PB-88, PB-89

Le 2 octobre 2026, sur 2a8a49f. Plan : `PLAN-G1.md` § G1.1.

**Transcrit.** `olivetti_m24.c` (ports 66h/67h), `keyboard_olim24.c` (le clavier et la souris
de la M24, par la file du clavier ; files sans contrôle de débordement et bornes mortes de la
souris reproduites), `vid_olivetti_m24.c` (la CGA 640 × 400 de la M24, 32 Ko, police MDA),
`gameport.c` et `joystick_standard.c` (sans manette branchée, décision n° 2). Intégration :
`olim24_init` (`model.c:292-300` : pas de rafraîchissement mémoire, clavier propre, CMOS, ports,
NMI, manette), le cas `ROM_OLIM24` de `loadbios` (`mem_bios.c:234-245`), la vidéo par le romset
(`video_card_getdevice`, `video_init`, `video_updatetiming` — `timing_m24` —, `video_is_*` :
gfxcard peut valoir `GFX_BUILTIN` sur une machine à vidéo fixe, le romset passe donc avant la
lecture de la carte), le CMOS sans cas dans `loadnvr`.

**L'oracle.** `harness_m24.c` inclut `vid_olivetti_m24.c` ; `h_boot` suit `olim24_init` et monte
`m24_device` par le romset ; `video_updatetiming` prend les temps de la M24 et du PC1512 par le
romset ; `fontdatm` défini (pixels seulement). Deux déviations de l'oracle, comme `svga_init` :
la VRAM de la M24 effacée (défaut 3), `nvrram` mis à zéro avant `loadnvr` pour la M24 (défaut 6c,
le C# fait de même). **La sonde passe à 143 champs** (ABI 33) : vingt et un de la `m24_t`
(CRTC, VRAM, `charbuffer`, registres, compteurs, temps), le champ 0 valant 2 ; contrôle négatif
(`ctrl` faussé d'une unité côté C#) mordant.

**PB-88, R9** : `charbuffer[256]` recopié jusqu'à l'index 509 (R1 non masqué) — PCem écrase les
champs qui suivent, `pc_timer_t` compris ; le C# saute l'écriture, rend 0 à la lecture.
**PB-89, reproduit** : la bordure déborde sur la ligne suivante de `buffer32` — jamais hors du
tableau (`displine` < 720) : le défaut n° 2 de PLAN-G1.md n'est pas un R9 pour la M24, les gardes
du brouillon ont été retirées et le code est verbatim.

**Les boot-diffs** (`--model olivetti_m24`, 8086/7,16) :

| Arc | Tranches | Instructions | Sonde M24 |
|---|---:|---:|---|
| POST | 3 000 | 16 463 895 | 143 identiques |
| PC-DOS 2.00 en disquette | 7 000 | 35 343 172 | 143 identiques |
| date, heure, `DIR` au clavier de la M24 | 9 000 | 45 217 532 | 143 identiques |

`cpu-config-check` : la M24 entre au balayage après les deux 8088, six vitesses × {sans, 8087},
identiques dans les deux ordres (`is8086` est dans l'empreinte). Témoin `--boot` (sur copie) :
« Resident Diagnostics Rev 1.43 », « CPU (i8086) Pass », « 640 kb RAM Pass », « RT Clock Pass »,
PC-DOS 2.00, `DIR`.

**La série**, sous `MALLOC_PERTURB_=85`, avec les portes isolées des CMOS de session (081b197),
comparée à g10b : 127 portes, **toutes vertes**. Les 124 anciennes identiques, aux écarts attendus
près — ABI 32 → 33, « 122 champs » → « 143 champs », `cpu-config-check` dans les deux ordres
élargi à la M24 (80 → 92 configurations identiques, 7 → 8 refus concordants) ; les trois
boot-diffs de la M24 nouveaux, verts. Build 0 avertissement, selftest, check-oracle 0 dérive (138).

## G1.2 — L'Amstrad PC1512

Le 2 octobre 2026, sur 870e7ac. Plan : `PLAN-G1.md` § G1.2.

**Transcrit.** `amstrad.c` pour le PC1512 (ports 66h — le reset logiciel —, 78h/7Ah de la souris,
378h-37Ah, DEADh ; `ams1512_device` et sa langue, défaut 7, décision n° 4), `keyboard_amstrad.c`
(le PPI de l'Amstrad, 60h-65h, file sans contrôle reproduite), `vid_pc1512.c` entier (la CGA du
PC1512 et son mode plan 640 × 200 × 16 : 3DDh masque d'écriture, 3DEh plan lu, 3DFh bordure,
64 Ko). De `lpt.c`, `lpt1_read`/`lpt1_write` sans périphérique (`Lpt/lpt.cs`), recopiées dans
l'oracle. Intégration : le champ `device` de MODEL et sa pose par `model_init`
(`model.c:692-693`), `ams_init` (`:259-270`), le cas `ROM_PC1512` de `loadbios` (ROM entrelacée et
police `40078.ic127`), la vidéo par le romset (`timing_pc1512` à zéro : le pilote facture
`cycles -= 12` lui-même), `pc1512.nvr` dans `loadnvr`/`savenvr`.

**Le CMOS de référence** : `--make-nvr` ne sait amender que la disposition des AT ; le PC1512 a
la sienne. `nvr/default/pc1512.nvr` est le CMOS que PCem livre (`pcem-dev/nvr/pc1512.nvr`),
copié tel quel.

**L'oracle** : `harness_pc1512.c` inclut `vid_pc1512.c` (sonde, VRAM effacée — déviation de
l'oracle) ; `h_boot` suit `ams_init` puis ajoute `ams1512_device`, monte `pc1512_device` par le
romset. **La sonde passe à 163 champs** (ABI 34) : vingt de la `pc1512_t` (CRTC, VRAM des quatre
plans, registres du mode plan, compteurs, temps), le champ 0 valant 3.

**Les boot-diffs** (`--model pc1512`, 8086/8) : POST, 3 000 tranches, 21 702 355 instructions,
sonde identique. **Le banc P1512** (`tools/pc1512banc/pc1512banc.py`, 340 octets, instructions
8086 seules), saisi dans DEBUG depuis la disquette supplémentaire de PC-DOS 2.00 en B: : mode 6,
plans effacés, un motif par plan, deux masques combinés (05h, 0Ah), bordure, relecture des
quatre plans et du plan masqué (3DEh à 07h → 3), trois lectures de 3DAh (le bit 0 bascule) —
**210 736 512 instructions identiques**, sonde identique, B: identique (517 octets écrits).
Contrôle négatif, retiré : l'écriture du plan 2 faussée d'un bit côté C# → divergence dès
l'instruction 49 873, en plein POST — le BIOS teste sa VRAM en mode plan.

**Témoin** (`--boot`, copies dans /tmp/g1w) : « AMSTRAD PC 640k (V1) », « (c)1986 AMSTRAD Consumer
Electronics plc », PC-DOS 2.00, `DIR` ; le banc en 16 couleurs, bordure magenta. **Constat** :
l'écran d'accueil date « 17 December 19F7 » — identique à l'oracle (VRAM comprise) : le CMOS de
référence de PCem, lu tel quel.

**La série**, sous `MALLOC_PERTURB_=85`, portes isolées, comparée à g11 : 130 portes, **toutes
vertes**. Les 126 anciennes identiques, aux écarts attendus près — ABI 33 → 34, « 143 champs » →
« 163 champs », `cpu-config-check` dans les deux ordres élargi au PC1512 (92 → 94 configurations,
8 → 9 refus) ; les quatre portes du PC1512 nouvelles, vertes (POST, PC-DOS 2.00, clavier, banc
du mode plan). Build 0 avertissement, selftest, check-oracle 0 dérive (146), `--setup-check`.

## G1.3 — Les machines et les témoins ; G1 fait

Le 2 octobre 2026, sur 8851e43. Plan : `PLAN-G1.md` § G1.3, décisions n° 3, n° 5 et n° 6.

**Les profils** (décision n° 5) : `ixtal26-m24.cfg` (8086/7,16, 640 Ko, deux 360 Ko, PC-DOS 2.00
en A:) et `ixtal26-pc1512.cfg` (8086/8, de même), profils Rider « Olivetti M24 » et « Amstrad
PC1512 ». Pas de CMOS à fabriquer : la M24 n'en lit aucun, le PC1512 part de
`nvr/default/pc1512.nvr`.

**Les souris** (décision n° 3) : la clé `mouse_type` est lue (`pc.c:784`) et écrite (`pc.c:932`,
par l'écran de construction) ; `mouse_list` garde les indices de PCem — 0 la série Microsoft,
4 l'Amstrad, 5 la M24 —, les places 1 à 3 (Mouse Systems, PS/2) restent nulles. Un indice sans
souris transcrite, qui ferait déréférencer NULL à PCem, est refusé et ramené à 0, avec un
message (DEVIATION). Les profils posent 5 et 4. **Limite** : le chemin de l'hôte (`mouse_poll`
depuis SDL) est celui de la souris série (M21) ; aucun témoin sans fenêtre n'en exerce le
mouvement — `--boot` n'a pas d'entrée souris. Les portes gardent `mouse_type` = 0, comme
l'oracle.

**Les témoins** (`--boot`, copies dans /tmp/g1w) : les deux profils amorcent PC-DOS 2.00
(`VER`), souris de la machine montée ; `mouse_type = 2` est refusé. **Le disque dur XT**
(décision n° 6, témoin seulement) : `8088-HDD-C.img` (306 × 4 × 17, une des quatre géométries du
Xebec) avec `mfm_xebec` — la M24 voit la ROM (« Optional ROM at C800:0000 »), mais ni elle ni le
PC1512 n'amorcent sur le disque, et PC-DOS 2.00 en disquette ne voit pas C:. **Constat, pas un
défaut de transcription** : sous boot-diff, 26 927 452 (M24) et 37 865 560 (PC1512) instructions
identiques, sondes identiques — l'oracle fait de même. Comportement de PCem, consigné.

Les originaux ne sont pas touchés : `sha256sum -c os/os.sha256` vert.

**La série**, sous `MALLOC_PERTURB_=85`, portes isolées, comparée à g12 : 130 portes, **toutes
vertes**, journaux identiques (durées ôtées) — les portes gardent `mouse_type` = 0.

## G8.0 — L'outillage du son : le C++ dans l'oracle, les tables de DBOPL

Le 2 octobre 2026. Plan : `PLAN-G8.md` § G8.0, validé sous mandat (décisions n° 2 à n° 5) ; la
carte, la Sound Blaster Pro v2, décision utilisateur du 02/10 ; l'OPL, DBOPL, décision de
l'orchestrateur sous mandat.

**Le C++ dans l'oracle.** Le Makefile compile désormais du C++ (`g++`, mêmes `-I`, mêmes
drapeaux) et lie la `.so` par `$(CXX)` ; `harness_dbopl.cpp` inclut `src/dosbox/dbopl.cpp`, dont
les tables sont `static` au fichier, et rend `MulTable`, `WaveTable`, `KslTable`, `TremoloTable`
(`h_opl_tables`, ABI 35). `bench`, qui ne lit ni l'OPL ni le DSP, reste en C seul.

**La porte `opl-tables-check`.** `InitTables` (`dbopl.cpp:1311-1464`, mode `WAVE_TABLEMUL`)
calcule `MulTable` et `WaveTable` par `pow(2.0, …)` et `sin(…)` de la libm de l'hôte : glibc
côté oracle, .NET côté C# (`Sound/dbopl.cs`, les tables et `InitTables`). **Les quatre tables
sont identiques, 4 660 entrées** : la décision n° 3 (une table figée tirée de glibc en cas
d'écart) n'a pas lieu de s'appliquer — mesuré d'abord, comme convenu. Contrôle négatif, retiré :
le coefficient du sinus faussé (4084 → 4083) → 1 476 entrées divergentes.

**La série**, sur un instantané de G8.0 construit dans un worktree de HEAD (oracle reconstruit de
zéro : le Makefile ne suit pas les en-têtes), sous `MALLOC_PERTURB_=85` : 131 portes, **toutes
vertes**, identiques à g13 hors ABI (34 → 35) ; `opl-tables-check` nouvelle, verte.

## G8.1 — L'OPL par DBOPL et l'AdLib

Le 2 octobre 2026. Plan : `PLAN-G8.md` § G8.1.

**Transcrit.** DBOPL entier en mode `WAVE_TABLEMUL` (`Sound/dbopl.cs` : Operator, Channel, Chip,
`Setup`) — les pointeurs de fonctions membres deviennent des énumérations, `ChanOffsetTable` et
`OpOffsetTable` (des offsets d'octets tirés de la disposition du C++) des indices, les sorties
des couples (tableau, décalage) : DEVIATIONS inscrites, équivalence des tables d'offsets
démontrée registre par registre ; `sound_dbopl.cc` (l'`opl[2]`, NukedOPL omis — `opl_emu` figé à
DBOPL), `sound_opl.c` (ports et minuteries ; chaque lecture coûte `isa_timing * 8` cycles),
`sound_adlib.c` (l'AdLib, OPL2 sur deux puces, décision n° 2). Le registre `SOUND_CARD` réduit
(`none`, `adlib`), la clé `sndcard` (`pc.c:666-670`), `sound_card_init` après `speaker_init`
(`pc.c:383`). Avant intégration, le brouillon de DBOPL a été confronté au vrai `dbopl.cpp` compilé
à part : 20 000 itérations d'écritures aléatoires de registres et de générations, OPL2 et OPL3,
deux graines, hachages d'échantillons identiques.

**L'oracle.** `sound_opl.c` et `sound_adlib.c` liés ; `harness_dbopl.cpp` inclut aussi
`sound_dbopl.cc` ; quatre souches fatales pour NukedOPL, inatteignables. `h_boot` monte la carte
nommée par `h_set_sndcard` après `speaker_init`. **Déviation de l'oracle (défaut n° 4)** : `opl[]`
est statique et traverse les amorçages d'un processus — `h_opl_reset` reconstruit la puce et
remet le reste à zéro à chaque amorçage ; le C# fait de même. ABI 36.

**La sonde du son en fin de boot-diff** (`h_sound_probe`, 21 champs, comparée dès qu'une carte son
est montée) : les neuf champs du haut-parleur, `sound_hash` compris — l'empreinte FNV de TOUS les
échantillons mixés (M9) —, et l'état des deux OPL (adresse, état, masque, contrôle, périodes). Une
empreinte restée à sa graine est un échec : deux silences ne prouvent rien.

**Les boot-diffs** : 5150 + AdLib, 13 122 609 instructions ; ami486 + AdLib, 5 433 745 ¹ ; sondes
du son identiques. **Le banc OPLBANC** (`tools/oplbanc/oplbanc.py`, 587 octets, instructions 8086
seules), saisi dans DEBUG sur le 5150 : la détection AdLib (minuterie 1 à FFh, état relu, minuterie 2,
masque, remise à zéro de l'IRQ), un instrument et neuf notes tenues, le mode rythme et ses cinq
percussions, relâchement — **152 918 358 instructions identiques, sonde du son identique**. Les
relevés, lus en C# seul : 06h, C6h, 06h, A6h — la détection d'une AdLib réussit. **Contrôle
négatif**, retiré : l'échantillon d'un opérateur faussé d'une unité côté C# → les instructions
restent identiques (le CPU ne lit pas les échantillons) et `sound_hash` diverge : la sonde voit ce
que le diff d'instructions ne voit pas.

¹ Remesuré le 3 octobre 2026 en porte isolée (CMOS de `nvr/default/`), au commit a0f0b71 :
5 140 843, d'abord consigné, venait d'un lancement qui lisait le CMOS de session de l'utilisateur
(voir § G1.0). Instructions identiques des deux côtés dans les deux cas.

**La série**, sous `MALLOC_PERTURB_=85`, oracle reconstruit de zéro, comparée à g80 : 134 portes,
**toutes vertes**, identiques à g80 hors ABI (35 → 36) ; les trois nouvelles vertes
(`bd-pc-adlib`, `bd-ami486-adlib`, `bd-pc-adlib-banc`). Build 0 avertissement, selftest,
check-oracle 0 dérive (152).

## G8.2 — Le DSP SBPRO2, le CT1345 et la Sound Blaster Pro v2

Le 2 octobre 2026. Plan : `PLAN-G8.md` § G8.2.

**Transcrit.** `sound_sb_dsp.c` tel que la SB Pro v2 l'atteint (`sb_type == SBPRO2`, DMA 8 bits :
reset, commandes, ports, `pollsb`, `sb_poll_i`, sortie directe, ADPCM 4, 2,6 et 2 bits ; le 16 bits,
le SB16/AWE et l'Aztech omis, chaque garde `sb_type >= SB16` laissée telle quelle), `sound_sb.c`
réduit à la SB Pro v2 (`sb_get_buffer_sbpro`, le mélangeur CT1345, `sb_pro_v2_init`, sa
configuration et son device) et le filtre `sb_iir` de `filters.h` en `float`, son état statique
traversant les amorçages comme en C (décision n° 4). `sound_set_cd_volume` des deux côtés ; le
registre `SOUND_CARD` gagne `sbprov2`. Trois défauts reproduits et inscrits : PB-90 (`DMA_OVER`
dans l'échantillon ADPCM), PB-91 (l'ADPCM 2 bits ne finit jamais), PB-92 (l'IRQ 10 perdue sans
second PIC).

**L'oracle.** `sound_sb.c` et `sound_sb_dsp.c` liés ; souches fatales pour l'EMU8000 et le MPU-401
(inatteignables sur la SB Pro v2), `GAMEBLASTER` à 0. La sonde du son passe à 41 champs : aux 21 de
G8.1 s'ajoutent les 20 du DSP et du mélangeur (état du DSP, longueurs et positions DMA, IRQ,
stéréo, registres du CT1345, échantillons courants). ABI 37.

**Les boot-diffs** : 5150 + SB Pro v2, 13 122 609 instructions ; ami486 + SB Pro v2, 5 433 745 (CMOS de nvr/default/, porte isolée) ;
sondes du son identiques. **Le banc SBBANC** (`tools/sbbanc/sbbanc.py`, 963 octets, instructions
8086 seules), saisi dans DEBUG sur le 5150 : reset et version du DSP, haut-parleur (D1h, D3h), 64
échantillons en sortie directe (10h), constante de temps (40h), DMA simple (14h) et automatique
(48h, 1Ch), pause, reprise et sortie (D0h, D4h, DAh), stéréo et relecture du mélangeur, filtre de
sortie coupé (0Eh = 20h) puis remis par le reset du mélangeur, ADPCM 4 bits (75h), l'OPL3 de la
carte (mode OPL3, notes dans les deux banques) — **220 778 728 instructions identiques, sonde du
son identique** (le filtre `sb_iir`, actif après le reset, a donc rendu les mêmes `float` des deux
côtés). Les relevés, lus en C# seul : reset AAh, version 3.02, mélangeur FFh, DDh, 02h, 00h,
volume maître EEh après le reset. **Contrôle négatif**, retiré : un échantillon DMA 8 bits mono
faussé côté C# (`^ 0x81` au lieu de `^ 0x80`) → instructions identiques, `sound_hash` seul
divergent (1 champ sur 41).

**La série**, sous `MALLOC_PERTURB_=85`, oracle reconstruit de zéro, comparée à g81 : 138 portes,
**toutes vertes**, identiques à g81 hors ABI (36 → 37) et hors la ligne de sonde des deux portes
AdLib, passée de 21 à 41 champs avec la même empreinte d'échantillons ; les trois nouvelles vertes
(`bd-pc-sbpro`, `bd-ami486-sbpro`, `bd-pc-sbpro-banc`). Build 0 avertissement, selftest,
check-oracle 0 dérive (157).

## G8.3 — Les sections de device, la SB Pro v2 sur les profils, les témoins

Le 2 octobre 2026. Plan : `PLAN-G8.md` § G8.3 ; décisions de l'orchestrateur du 02/10 (option a,
puis option i).

**Les sections de device du .cfg.** L'IRQ 5 de la décision n° 5 n'avait pas de chemin :
`device_get_config_int/string` rendaient toujours le défaut, des deux côtés. Elles lisent
maintenant la section qui porte le nom du device, comme PCem (`device.c:94-116`) : en C# par
`PluginApi/config.cs` (la DEVIATION de `device.cs` tombe) ; dans l'oracle, où `config.c` n'est
pas lié, par une table que remplit `h_set_device_config` (ABI 38) — iXtal26.Diff y recopie, avant
chaque amorçage, les sections du `--config` chargé. Les listes `selection` (`devices.h:22-25`)
sont transcrites pour les sept tables de configuration du dépôt, et **une valeur hors liste prend
le défaut, avec un avertissement** (section, clé, valeur rejetée, défaut retenu) — DEVIATION
« comme l'interface de PCem l'impose » : son dialogue n'offre que la liste, et une valeur écrite
à la main peut indexer hors des tableaux (PB-93 : la SB à DMA 9 ou à FFFEh ; huit valeurs de
`memory` sur douze essayées pour la TVGA8900D, la GD5429 et la Trio64 arrêtaient l'hôte). L'oracle
n'est pas touché ; ces valeurs restent hors des portes.

**Les portes.** `bd-ami486-sbpro-irq5` : l'ami486 + SB Pro v2 avec la section des profils
(`addr = 544`, `irq = 5`, `dma = 1`), et `--expect-sb 220,5,1`, qui exige ces valeurs lues dans la
sonde du son DES DEUX CÔTÉS (l'adresse y entre, champ `sbe2|sbe2count|sb_addr`) : vert.
`bd-ami486-sbpro` exige désormais le défaut, 220h, IRQ 7, DMA 1. **Contrôles négatifs**, retirés :
la section absente sous `--expect-sb 220,5,1` → « attendu 220h, IRQ 5, DMA 1 », rouge ; la section
donnée au C# seul (oracle privé de `h_set_device_config`) → instructions identiques, sonde
divergente sur `data_stat|irqnum` (IRQ 7 contre 5). `r9-sbcfg`, en C# seul, 19 essais : clé
inconnue et section d'un device absent ignorées ; 240h/IRQ 10/DMA 3 (dans la liste) pris ; IRQ 3
→ 7 averti ; non numérique → défaut, `0x3` lu 3 (`%i`) ; DMA 9, IRQ 99, base FFFEh → défauts
avertis ; `opl_emu = 1` → DBOPL (NukedOPL omis, dit sur la sortie d'erreur) ; les douze valeurs
`memory` hors liste → défaut averti (deux avertissements pour la Trio64, qui lit la clé deux
fois), puis 400 tranches d'amorçage — tout survit ; sans la validation, dix essais s'arrêtaient
(mesuré).

**Les profils.** `ixtal26-486.cfg`, `ixtal26-486-s3.cfg`, `ixtal26-386.cfg` (ami386dx) :
`sndcard = sbprov2` et la section `[Sound Blaster Pro v2]` à 220h, IRQ 5, DMA 1 ; côté DOS,
`SET BLASTER=A220 I5 D1 T4`. Aucune porte ne lit ces fichiers.

**Le témoin Windows** (C# seul, `--boot`, dans `/tmp` : copie du disque 486 de l'utilisateur,
profil 486-s3, `KEYB FR` retiré, CMOS copié ; rien n'est écrit dans `os/` ni `nvr/`). Le pilote
Sound Blaster 1.5 de Windows 3.11 (`SNDBLST2.DRV`, disquette 4 ; `VSBD.386`, disquette 5) est
décompressé par `EXPAND` dans l'invité ; `SYSTEM.INI` de la copie reçoit `wave=sndblst2.drv`,
`device=vsbd.386` et `[sndblst.drv] port=220 int=5`. Une commande de script `@son` (BootTest)
imprime l'état de la carte. **À l'IRQ 5** : Program Manager sans message, et le DSP laissé par le
son de démarrage (CHIMES.WAV) — DMA 8 bits automatique de 2 048 octets, constante de temps D3h
(22 kHz), haut-parleur allumé, pause (D0h) en fin de son. **Contrôle négatif**, la carte à
l'IRQ 7 et Windows réglé sur 5 : le pilote envoie F2h (le test d'interruption), l'IRQ reste en
attente, rien n'est joué, et Windows affiche « Sound Blaster — A configuration or hardware
problem has occurred ». **Témoin audible** : non consigné — la session n'a pas d'oreille ; à
écouter par l'utilisateur (ce même son de démarrage, pilote installé, profil 486-s3).

**La série**, sous `MALLOC_PERTURB_=85`, oracle reconstruit de zéro, comparée à g82 : 140 portes,
**toutes vertes** ; identique à g82 hors ABI (37 → 38) et hors la nouvelle ligne « Sound Blaster
des deux côtés : 220h, IRQ 7, DMA 1 » des trois portes SB (instructions et empreintes inchangées) ;
les deux nouvelles vertes (`bd-ami486-sbpro-irq5`, 5 433 745 instructions, IRQ 5 lue des deux
côtés ; `r9-sbcfg`). Rien ne bouge pour les cartes et profils existants. Build 0 avertissement,
selftest, check-oracle 0 dérive (157).

## G8 — Correctifs du 3 octobre : des chiffres remesurés, un avertissement dédoublonné

**Les chiffres.** Quatre décomptes consignés venaient d'un lancement qui lisait le CMOS de session
de l'utilisateur (`nvr/.ami386dx_opti495.nvr`, `nvr/.ami486.nvr`). Chacun a été remesuré au commit
qui l'avait consigné, dans un worktree dont `nvr/` ne contient que `nvr/default/` : § G4.0
(4 437 104 → 4 437 159), § G7.1 (5 285 528 → 4 980 606), § G7.3 (5 272 247 → 4 989 579), § G8.1
(5 140 843 → 5 433 745) — chacun corrigé avec une note. Les autres décomptes ami486 de G6 et G7
concordent avec les portes isolées ; le 181 109 300 de § G5 vient d'une porte disque à CMOS copié,
déjà isolée, et § G5 le remplace lui-même.

**L'avertissement.** La Trio64 lit `memory` deux fois (`vid_s3.c:2881`, `:3008`) : l'avertissement
« hors liste » n'est plus dit qu'une fois par clé et par initialisation du device. La lecture ne
change pas (mêmes appels, même valeur rendue) ; `r9-sbcfg` attend désormais un avertissement.

**La série**, sous `MALLOC_PERTURB_=85`, oracle reconstruit de zéro, comparée à g83 : 140 portes,
toutes vertes, journaux identiques sauf celui de `r9-sbcfg` (un avertissement Trio64 au lieu de
deux).

## PS2.0 — La souris PS/2 par le 8042 ; PB-94, PB-95

Le 3 octobre 2026. Plan : `PLAN-PS2.md` § PS2.0 (validé par l'orchestrateur le 03/10).

**Transcrit.** `mouse_ps2.c` entier (`Mouse/mouse_ps2.cs`) : la souris PS/2 à deux boutons et
l'Intellimouse (places 2 et 3 de `mouse_list`, aux indices de PCem), la branche du PC5086 omise.
`mouse_scan` rejoint son fichier (la DEVIATION de `keyboard_at.cs` tombe). **DEVIATION :
`MODEL_PS2` ajouté aux trois AMI 386/486** (ami386, ami386dx, ami486), dont le BIOS porte la
« Mouse Support Option » ; chez PCem le drapeau ne sert qu'à l'interface (`wx-config.c:64`), donc
l'oracle monte la même souris sans écart. Sur une machine sans `MODEL_PS2` (ami286, ibmat…), une
souris PS/2 est refusée avec un avertissement et la souris série prend sa place
(`pc.mouse_type_selon_machine`). Deux défauts reproduits : PB-94 (aucune réponse aux commandes
inconnues, F6h…), PB-95 (E9h code le bouton du milieu en 3).

**L'oracle.** `mouse.c` et `mouse_msystems.c` liés : la souris est celle de `mouse_type`
(`h_set_mouse_type`), montée par `mouse_emu_init` comme `pc.c:373` ; `harness_ps2.c` inclut
`mouse_ps2.c` (structure privée) et retient la souris montée par un renommage le temps de
l'inclusion de `keyboard_at_set_mouse` — le texte vendoré n'est pas touché ; souche fatale
`upc_set_mouse`. `h_mouse_poll` pose `mouse_buttons` puis appelle `mouse_poll`. **La sonde de la
souris** (9 champs : `mouse_scan`, la file du 8042, l'état de `mouse_ps2_t`, le knock) est
comparée en fin de boot-diff dès qu'une souris PS/2 est montée. ABI 39.

**L'injection.** `--mouse-type N` et `--mouse-at TRANCHE:dx,dy,dz,b` (répétable) : le mouvement
est injecté EN FIN de tranche, des deux côtés, là où `runpc` appellerait `pollmouse`. C'est la
première porte du dépôt qui fasse bouger une souris des deux côtés (`h_mouse_poll` existait
depuis M21 sans appelant).

**Le banc PS2BANC** (`tools/ps2banc/ps2banc.py`, 524 octets, instructions 8086 seules), saisi
dans DEBUG sur l'ami386dx (CMOS `f386.nvr`, sans disque dur), clavier coupé et IRQ 12 masquée :
il parle au 8042 directement et relève, pour chaque lecture, l'état puis la donnée (EEh si rien
ne vient). Sous boot-diff, mouvements injectés, bouton du milieu tenu dès la tranche 45 560 :
- **souris à 2 boutons** : 72 011 910 instructions identiques, sonde identique. Relevés : test du
  port 00h ; reset FAh AAh 00h ; identifiant 00h ; résolution et cadence acquittées ; E9h
  10h 02h 64h (échelle 2:1) ; **F6h sans réponse (PB-94)** ; flux activé, paquets
  08h 03h 02h, puis le gros mouvement 28h 28h E7h ; EBh ; **E9h 23h (PB-95 : activée + bouton du
  milieu codé 3)** ; le knock ignoré (identifiant 00h).
- **Intellimouse** : 72 012 638 instructions identiques, sonde identique. Après le knock,
  identifiant **03h**, paquets à 4 octets (0Ch 03h 02h 00h : bouton du milieu en bit 2), EBh 04h.
**Contrôle négatif**, retiré : PB-95 « corrigé » côté C# seul (`temp |= 4`) → première divergence
à l'instruction 67 871 907.

**Les autres portes.** `bd-ami486-ps2` (3 000 tranches) et `bd-ami486-ps2-post` (POST complet) :
la souris montée, sonde identique ; `bd-ami286-ps2-refus` : refus averti, la série à sa place,
vert, compte de la porte `bd-ami286`.

**La série**, sous `MALLOC_PERTURB_=85`, oracle reconstruit de zéro, comparée à g84 : 145 portes,
**toutes vertes** ; les cinq nouvelles vertes (`bd-ami486-ps2`, `bd-ami486-ps2-post` — 73 097 655
instructions, le compte du POST sans souris : le BIOS ne touche pas la souris au POST avec le CMOS
de référence —, `bd-ami286-ps2-refus`, `bd-ami386dx-ps2-banc-2` et `-3`). Écarts à g84 : l'ABI
(38 → 39) ; le message des Pentium OverDrive dans `cpu-config-check` et `-inverse` (commit doc
2af7e87) ; et **sept portes disque, cause : disque de l'utilisateur modifié, 03/10 09:12**.
`os/386-HDD-C.img` a changé ; la recette `g5w` en tire un autre `c386.img` (empreinte réécrite,
`g5w.sha256`), que les sept portes amorcent (`ami386dx-ide.cfg`, `ami486-ide.cfg`,
`ami486-ide-e.cfg`). Toutes vertes, instructions identiques des deux côtés ; les comptes :

| Porte | g84 | g85 | Octets écrits sur C: |
|---|---:|---:|---|
| `bd-ami386dx-ide-ecriture` | 181 109 674 | 181 109 525 | 7920 → 8149 |
| `bd-ami486-ide-ecriture` | 183 802 720 | 183 803 652 | 7920 → 8149 |
| `bd-ami486-ide-check` | 202 649 589 | 202 651 525 | 1021 → 1031 |
| `bd-ami486-gd5429-dos` | 183 847 196 | 183 848 146 | — |
| `bd-ami486-gd5429-blt` | 261 046 800 | 261 046 649 | 1460 → 1526 |
| `bd-ami486-trio64-dos` | 183 816 380 | 183 815 808 | — |
| `bd-ami486-trio64-accel` | 313 940 329 | 313 942 154 | 1511 → 1535 |

**Preuve de la cause** : les sept portes rejouées avec l'instantané d'AVANT PS2.0 (celui de g84,
ABI 38) sur le nouveau `c386.img` rendent des journaux identiques octet pour octet à ceux de g85 —
c'est le disque, pas le code. Build 0 avertissement, selftest, check-oracle 0 dérive (159).
