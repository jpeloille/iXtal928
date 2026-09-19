# Journal de vérification

Le registre des **mesures**. TRANSCRIPTION.md porte les règles et les conventions — un
document borné, qui ne bouge que quand une règle change. Celui-ci porte ce qu'on a
constaté : résultats de portes, divergences, injections de panne. Il grandit à chaque
jalon, et c'est normal.

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

---

## Résultats des portes M0

Mesurés le 2026-09-19, .NET 10.0.112, x86-64 Linux.

| Porte | Résultat |
|---|---|
| **G1** propriété `ref` | **Verte.** `cycles -= 3;`, passage `ref`, `tsc += 0x1_0000_0000UL` mutent bien le champ référencé. |
| **G2** switch géant + `goto` | **Verte.** **180 106 octets d'IL** pour 256 cas / ~13 900 lignes ; JIT propre en Debug, Release, et sous `DOTNET_TieredCompilation=0` (pas de bailout). Le switch de `808x.c` fait ~2 600 lignes, soit ~1/5 : **marge 5×**, la contingence « découper par quartet » est inutile. |
| **G3** union explicite | **Verte.** Aliasing `l`/`w`/`b.h`/`b.l`, écriture de demi-octet préservant le reste, `getr8`/`setr8` indexés avec le pliage haut/bas sur le bit 2, et absence de mutation à travers une copie de struct. |
| **G4** SDL3-CS sous net10 | **Verte.** Restaure et compile ; `dotnet run -- --frames 5` ouvre une fenêtre, rend 5 images, sort 0. |

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
