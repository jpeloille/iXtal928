# G13 — domaine D6 : la carte mère et les périphériques

Reconnaissance en lecture seule pour le plan de G13, le 6 octobre 2026, au commit `bc609ce`.
Rien n'est décidé : ce document fournit la matière du plan.

**Périmètre** — quatorze entrées de `PCEM_BUGS.md` : section A, PB-03, PB-05, PB-06, PB-157,
PB-94, PB-95, PB-101, PB-103, PB-104 ; section B, PB-07, PB-08, PB-10 ; section C, PB-13, PB-33.

**Lu** — `PLAN.md` § G13 ; `TRANSCRIPTION.md` § Règles ; `PCEM_BUGS.md` (en-tête, les quatorze
entrées, « Portée de ce registre ») ; le C de PCem (`pcem-dev/src/…`) et le C# qui le reproduit ;
`VERIFICATION.md` § M4.6, § M16, § PS2.0, § G10.0 ; `PLAN-PS2.md`, `PLAN-G10.md` ; l'oracle
(`tools/oracle/harness.h`, `harness.c`, `harness_dma.c`) ; les bancs `tools/sb16banc`,
`tools/ps2banc`, `tools/joybanc`, `tools/lptbanc` ; `tools/gates/series.sh`. Aucun fichier du dépôt
n'a été modifié ; rien n'a été construit ni lancé.

**Niveaux** — *documenté* : une source primaire le dit (fiche Intel, Technical Reference d'IBM ou
d'Amstrad) ; *documenté (secondaire)* : seulement une source secondaire ; *déduit* : raisonnement
sur les sources ou sur le code ; *inconnu* : aucune source trouvée, il faut mesurer. Sources,
pages et URL au § 10.

---

## 1. L'essentiel

- **Comptes.** (a) corrigeable et vérifiable : 7 — PB-03, PB-05, PB-07, PB-95, PB-101, PB-103,
  PB-104. Mixte (a)/(c) : 1 — PB-157 (la commande et le registre temporaire sont documentés ; les
  relectures « illégales » DCh/DEh et les bits de requête ne le sont pas). (b) : 1 — PB-94 (attendus
  tirés de sources secondaires seulement). (d) : 5 — PB-06, PB-08, PB-10, PB-13 sans effet ni
  contrepartie matérielle ; PB-33 est un défaut de l'hôte, à corriger plutôt dans les deux modes.
- **Le PIC et le DMA ne se corrigent pas entrée par entrée.** PB-05 a deux voisins non inscrits :
  N1, la cascade servie avant IRQ0 et IRQ1, et N2, le masque de service ignoré. Ensemble, ils
  font perdre un top d'horloge chaque fois que l'IRQ 0 et l'IRQ 8 attendent en même temps. Pour le
  8237, ranger la commande du contrôleur haut (PB-157) sans que le « master clear » efface le
  registre de commande (N6, non inscrit) peut laisser ce contrôleur désactivé. Le POST de
  l'IBM AT écrit 04h en 08h et D0h, puis envoie un master clear à chacun des deux 8237.
- **Trois entrées du registre sont fausses ou incomplètes.**
  - PB-95 : dans l'octet d'état d'E9h, le bouton gauche est en bit 2 et le droit en bit 0 (IBM,
    *PS/2 and PC BIOS Interface Technical Reference*, p. 2-96/2-97 et 6-117). PCem place donc mal
    les trois boutons, pas seulement celui du milieu.
  - PB-03 : les −5,87 ppm qui ferment le budget viennent de la troncature
    `cpu_get_speed() / 100` (`pc.c:473`, VERIFICATION.md § M4.6), pas de PB-11.
  - PB-07 : il n'est ni reproduit ni corrigé, mais neutralisé des deux côtés par un zéro, sans
    marqueur. `PLAN.md` le compte à tort parmi les « reproduits » de la section B.
- **Préalable mécanique.** La conversion en `fixed in hardware mode: PB-nn` se fera par grep.
  Elle ne trouvera pas sept sites :
  - PB-03 et PB-07 n'ont aucun marqueur ;
  - PB-05, PB-06, PB-13 (`pic.cs`), PB-08 (`device.cs`) et PB-10 (`mem_bios.cs`) ont un marqueur
    sans identifiant.
- **Chemins chauds.** Trois corrections touchent un chemin chaud :
  - PB-03, dans `clockhardware`, appelé une fois par instruction sur 8088 et 8086 ;
  - PB-07, à chaque accès mot du 808x ;
  - N2, dans le test d'acceptation des IRQ du 808x.

  Le reste se trouve dans des gestionnaires d'E/S ou au chargement.
- **Vérification sans oracle.** Le mode matériel ne se vérifie que par des bancs dirigés en C#
  seul, dont les attendus viennent de la documentation.
  - Existent déjà et servent : `--timer-check`, `--joystick-check`, `r9-joycfg`.
  - À écrire : PICBANC et DMABANC.
  - À doter d'un second jeu d'attendus : SB16BANC, PS2BANC, JOYBANC, LPTBANC.

  Il faut aussi un exécuteur de bancs en C# seul (frappes injectées, relevé comparé à un fichier
  d'attendus). C'est une infrastructure commune à tout G13.

---

## 2. Tableau récapitulatif

| PB | Titre court | Effet observable par l'invité | Classement | Chemin chaud | Vérification du mode matériel |
|---|---|---|---|---|---|
| PB-03 | `clockhardware` : cycles débités après la prise du `diff`, jamais portés au TSC | Le temps de l'invité retarde sur son UC : −0,006 % à l'invite, −1,90 % au test mémoire (mesuré). Concerne le 5150, l'XT, la M24 et le PC1512 | (a) | **oui** (par instruction ; correctif logeable dans la branche rare) | `--timer-check` : 0 cycle hors TSC, Δtsc = 3 × 47 727 par tranche en moyenne |
| PB-05 | En acquittant l'IRQ c de l'esclave, `picinterrupt` efface le bit c du maître | Perte d'une IRQ du maître (IRQ0 contre IRQ8, IRQ1 contre IRQ9, IRQ6 contre IRQ14, IRQ7 contre IRQ15…), déduit | (a) | non ; **oui** si N2 est joint (acceptation du 808x) | PICBANC à écrire, attendus de la fiche 8259A et de l'AT TR p. 1-10 |
| PB-06 | `pic_reset` remet `pic.mask2` à la place de `pic2.mask2` | Aucun : l'ICW1 du POST remet `mask2` à zéro | (d) nettoyage | non | Une sonde du PIC seulement |
| PB-157 | 8237 haut : commande non rangée, bits de requête absents, DAh, DCh et DEh relus = dernier octet écrit | Le 8237 haut ne se désactive pas ; DAh ≠ 00h ; DEh ne rend pas le masque | (a) commande et temporaire ; (c) DCh/DEh et bits de requête | non (gestionnaires d'E/S) ; tiède avec N8 | DMABANC ou SB16BANC étendu, attendus de la fiche 8237A et de l'AT TR |
| PB-94 | Souris PS/2 muette aux commandes qu'elle ne connaît pas | Un pilote attend FAh (F6h, F0h, EAh, EEh…) jusqu'au délai ; atteint seulement par `--force-ps2` | (b) | non | PS2BANC, attendus Chapweske et Brouwer (secondaires) et IBM BIOS TR |
| PB-95 | E9h : boutons mal placés dans l'octet d'état | Bouton du milieu lu « droit + milieu » ; aussi, gauche lu « droit » et droit lu « milieu » ; seulement par `--force-ps2` | (a) | non | PS2BANC E9h, attendus IBM 15F0306 |
| PB-101 | `lpt2_remove_ams` ne retire rien : le PC1512 garde un LPT2 en 278h | Port 278h présent, LPT2 au BDA (déduit, non mesuré) | (a) | non | LPTBANC sur le PC1512 : BDA et lecture de 278h |
| PB-103 | Chapeau CH et TM : 315° tombe hors des quatre intervalles | Haut-gauche lu au repos (CH) ou en bas (TM), mesuré | (a), sur une convention | non | JOYBANC, tours 5 et 10 |
| PB-104 | Hôte : sans correspondance explicite, chapeau calculé sur (axe d, axe d) | Le chapeau de l'hôte est ignoré (mesuré, `--joystick-check`) | (a), hôte | non | `--joystick-check`, cas 2 |
| PB-07 | `readmemw`/`writememw` du 808x : l'octet haut d'un mot en fin de page sort du tableau | Aujourd'hui 0 des deux côtés (neutralisé) ; le matériel replie à 1 Mo, ou lit ce qui suit la RAM | (a) pour le repli à 1 Mo ; bus flottant déduit | **oui** (chaque accès mot du 808x) | Test C# dirigé, et le corpus SST 8088 s'il contient de tels cas |
| PB-08 | Borne testée après l'accès dans `pcem_add_device` | Aucun (une vingtaine de périphériques au plus) | (d) | non | — |
| PB-10 | Double `fclose` dans `loadbios` | Aucun (`Close` .NET idempotent) | (d) | non | — |
| PB-13 | Branche morte de l'EOI spécifique (`val == 2`) | Aucun (`pic_updatepending` recalcule) | (d) nettoyage | non | — |
| PB-33 | `savenvr` écrit sans tester le fichier | Aucun pour l'invité ; l'hôte tombe à la sortie si `nvr/` manque | (d) pour le mode matériel ; correction d'hôte proposée dans les deux modes | non | Test C# seul, répertoire temporaire absent |

---

## 3. Constats transverses

### 3.1 Les marqueurs et les renvois du registre

La conversion `reproduced` → `fixed in hardware mode: PB-nn` se fera par grep. Les sites
suivants doivent d'abord porter leur identifiant.

| PB | Site cité par `PCEM_BUGS.md` | Site réel (6/10) | Marqueur |
|---|---|---|---|
| PB-03 | `Cpu/808x.cs:270` | `Cpu/808x.cs:280-291` (`clockhardware`) | **absent** |
| PB-05 | `Models/pic.cs:433` | `:433` | sans identifiant (« pic.c:368-369 efface le bit ») |
| PB-06 | `Models/pic.cs:70` | `:70` | sans identifiant |
| PB-13 | `Models/pic.cs:163` | `:163` | sans identifiant |
| PB-07 | `Memory/mem.cs:706` | `Memory/mem.cs:1414-1417` (`new byte[mem_size * 1024 + 4]`) | **absent** (commentaire sans PB) |
| PB-08 | `PluginApi/device.cs:268` | `:327` | sans identifiant |
| PB-10 | `Memory/mem_bios.cs:115` | `:284` | sans identifiant |
| PB-157 | `Models/dma.cs` | `:312`, `:360` | correct |
| PB-94, PB-95 | `Mouse/mouse_ps2.cs` | `:90`, `:117` | correct |
| PB-101 | `Lpt/lpt.cs`, `Models/amstrad.cs` | `lpt.cs:258`, `amstrad.cs:175` | correct |
| PB-103 | deux fichiers | `joystick_ch_flightstick_pro.cs:26`, `joystick_tm_fcs.cs:47` | correct |
| PB-104 | `pc.cs` | `pc.cs:674` (dans un commentaire `///`) | correct |
| PB-33 | `Devices/nvr.cs` | `:510` | correct |

R1(d) de `TRANSCRIPTION.md` n'énumère que `reproduced:` et `not reproduced:`. La nouvelle forme
devra y entrer : c'est un changement de règle (218 lignes sur un plafond de 240).

### 3.2 Les entrées à amender avant G13

1. **PB-03.**
   - « −5,87 ppm de PB-11 » est faux. VERIFICATION.md § M4.6 impute ces 5,87 ppm à la division
     entière `cpu_get_speed() / 100` (`pc.c:473`), un défaut non inscrit (N10).
   - Site : remplacer `:270` par `:280-291` et ajouter le marqueur.
2. **PB-07.**
   - Ajouter une ligne de statut : « neutralisé des deux côtés » (`h_pad_ram`,
     `tools/oracle/harness.c:562` ; `mem.cs:1417`).
   - « C'est la seule déviation assumée de l'oracle » n'est plus vrai : il y a aussi
     `__wrap_rom_init` (PB-24), le `loadnvr` de G1.1 et le fil du S3.
   - `PLAN.md` le range parmi les « reproduits » de la section B. Même remarque, hors domaine,
     pour PB-17 (« non reproductible »).
3. **PB-95.**
   - L'entrée dit que l'octet d'état porte le milieu en bit 2. C'est faux : bit 2 = gauche,
     bit 1 = milieu (réservé sur une souris à deux boutons), bit 0 = droit, bit 6 = mode distant
     (IBM 15F0306).
   - PCem place les trois boutons ailleurs : l'effet est plus large que celui écrit.
   - Le contrôle négatif de PS2.0 (`temp |= 4`) ne vaut donc pas attendu du mode matériel.
4. **PB-157.**
   - `:345` devient `:349`.
   - Les bits de requête manquent sur **les deux** 8237, pas seulement sur le haut (`dma.c:83-85`
     ne rend que les TC du bas).
   - Y joindre N4 à N8 s'ils sont inscrits.
5. **PB-06.** Il est classé en A, mais aucun effet n'est observable : à reclasser en C ?
6. **PB-101.** L'effet reste « déduit, non mesuré ». Il se mesure en mode PCem, sous l'oracle,
   avec LPTBANC sur le PC1512, avant toute correction.

### 3.3 Défauts voisins, non inscrits, relevés en lisant

Ils ne sont pas dans la liste de G13, mais une correction cohérente des PB du domaine les
rencontre. Ils sont à inscrire (PB-168 et suivants) si Julien le décide (question Q1).

| N | Défaut | C de PCem (C#) | Vrai comportement | Lien |
|---|---|---|---|---|
| N1 | `picinterrupt` teste la cascade dès `c = 0` : les IRQ 8 à 15 passent avant IRQ0 et IRQ1 | `pic.c:359` (`pic.cs:421`) | IR0 > IR1 > IR2 (IRQ 8-15) > IR3 … IR7. **Documenté** : 8259A p. 15, IBM AT TR p. 1-10/1-11 | PB-05 |
| N2 | Masque de service (`mask2`) ignoré. (a) Le 808x accepte une IRQ sur `pic.pend & ~pic.mask` : une IRQ moins prioritaire interrompt un gestionnaire qui a fait STI avant son EOI (le 5150 fait STI en tête d'INT 08h). (b) Sur l'AT, `picinterrupt` descend dans l'esclave même quand l'ISR 2 du maître le bloque | `808x.c:55`, `:3985`, `pic.c:356-359` (`808x.cs:700`, `:3826`) | « While the IS bit is set, all further interrupts of the same or lower priority are inhibited ». **Documenté** : 8259A p. 15 | PB-05 |
| N3 | OCW2 : rotations et « set priority » traitées comme un EOI non spécifique. OCW3 : le « poll » et le masque spécial ignorés | `pic.c:126-152` | **Documenté** : 8259A p. 13-16. Usage par les logiciels du dépôt : **inconnu** | PB-05, PB-13 |
| N4 | « Clear Mask Register » (0Eh, DCh) sans effet : aucun `case 0xe` | `dma.c:94-162`, `:352-426` | Efface les quatre masques. **Documenté** : 8237A p. 9 (Figure 6) ; AT TR p. 1-14 (« ODC Clear Mask Register ») | PB-157 |
| N5 | Registre de requête (09h, D2h) ignoré : pas de requête logicielle | idem | **Documenté** : 8237A p. 7, en mode bloc. Le POST de l'AT écrit 00h en D2h (sans effet) | PB-157 |
| N6 | Le master clear (0Dh, DAh) n'efface ni la commande, ni l'état, ni la requête, ni le temporaire | `dma.c:153-156`, `:417-420` (`dma.cs:235`, `:389`) | Les efface et pose les masques. **Documenté** : 8237A p. 9 | **PB-157 (lien fort)** |
| N7 | `dma_reset` laisse les masques à 0 | `dma.c:43` (`dma.cs:121`) | Le Reset pose tout le registre de masque. **Documenté** : 8237A p. 8 | PB-157 |
| N8 | Cascade non modélisée : un canal 4 masqué ou un 8237 haut désactivé ne bloquent pas les canaux 0 à 3 | `dma.c:498-512`, `:568-581` | « Channel 4 is used to cascade channels 0 through 3 » : **documenté** (AT TR p. 1-13). L'effet de blocage est **déduit** | PB-157 |
| N9 | La file souris du 8042 (16 octets) n'a pas de garde : au-delà, elle paraît vide. Relevé en PS2 (défaut n° 4 de `PLAN-PS2.md`), jamais inscrit | `keyboard_at.c:239-244` (`keyboard_at.cs:309-315`) | Contrôle de flux par la ligne « clock » : le périphérique attend. **Documenté** : PS/2 HITR Common Interfaces, « Auxiliary Device and System Timings », p. 14-15 du chapitre | PB-94 |
| N10 | `runpc` tronque `cpu_get_speed() / 100` : −5,87 ppm de temps invité face à l'hôte | `pc.c:473` | Rythme de l'hôte, inobservable par l'invité (**déduit**) | PB-03 |
| N11 | (domaine UC) Un mot à l'offset FFFFh : le chemin rapide lit le second octet en `seg + 10000h` | `808x.c:73-80` | À instruire par le domaine UC (corpus SST, documentation Intel) | PB-07, PB-87 |

---

## 4. Section A — les PB reproduits

### PB-03 — `clockhardware()` perd les cycles de rafraîchissement DRAM

**1. Défaut et effet.**
- `clockhardware()` prend `diff`, le porte au TSC, puis appelle `timer_process()`.
- Les cycles que celui-ci débite n'entrent dans aucun `diff`, puisque l'instruction suivante
  repart de `cycdiff = cycles`. Ce sont :
  - le `FETCHCOMPLETE()` de `refreshread()`, appelé par la DMA de rafraîchissement
    (`pit_refresh_timer_xt` → `dma_channel_read(0)`) ;
  - de même pour toute DMA d'une machine non AT : `dma_channel_read/write` appellent
    `refreshread()`.
- Effet : le temps de l'invité (PIT, INT 08h, heure du BDA, chronomètres des périphériques, tous
  sur le TSC) retarde sur le compte de cycles de son processeur. Mesuré (VERIFICATION.md § M4.6) :
  −0,006 % à l'invite BASIC, −1,90 % pendant le test mémoire, −53,9 ppm en moyenne sur 3 000 s.
- Un programme qui chronomètre une boucle mémoire au PIT croit l'UC plus rapide d'autant
  (**déduit**).
- Machines concernées : celles du cœur 808x (5150, XT, M24, PC1512). Le 286 et au-delà n'ont
  aucun manque : 0 cycle perdu sur 1 431 905 470, VERIFICATION.md § M16.

**2. Sites.**
- PCem :
  - `pcem-dev/src/cpu/808x.c:893-904` (`clockhardware`) ;
  - `:1239-1240` (remise de `cycdiff` et `current_diff`) ;
  - `:3932-3939` (fin d'instruction) ;
  - `:82-85` (`refreshread`) ;
  - `pcem-dev/src/models/pit.c:553-556` ;
  - `pcem-dev/src/models/dma.c:511-512` et `:579-580` (`if (!AT) refreshread();`).
- C# :
  - `iXtal26/Cpu/808x.cs:280-291` (sans marqueur), `:1171-1172`, `:3778`, `:123-127` ;
  - `iXtal26/Models/dma.cs:502-503`, `:586-587` ;
  - `iXtal26/Models/pit.cs:692`.

**3. Vrai comportement.**
- **Documenté** : sur le 5150, l'UC (4,77 MHz) et le 8253-5 (1,19318 MHz) tirent leur horloge du
  même quartz de 14,31818 MHz, divisé par 3 et par 12 (IBM PC TR 6025008, « System Board »,
  p. 2-3 ; signaux OSC et CLK du canal d'E/S ; entrée du 8253-5 à 1,19318 MHz). Le rapport UC/PIT
  est fixe : un cycle volé par la DMA de rafraîchissement s'écoule aussi pour le PIT.
- **Documenté** : le rafraîchissement est une lecture DMA factice du canal 0, demandée par un
  compteur du 8253 toutes les 72 horloges, environ 15 µs (p. 2-3/2-4).
- La TR se contredit sur la durée du cycle : « four clocks » p. 2-3, « five clocks » p. 2-4. Ce
  coût n'est pas l'objet de PB-03.

**4. Correction en mode matériel.**
- Après `timer_process()`, reprendre `cycdiff - cycles - current_diff` et le porter au TSC (les
  quatre lignes de tête).
- Logée **dans la branche** `if (TIMER_VAL_LESS_THAN_VAL(...))`, elle ne coûte que lorsqu'un
  chronomètre échoit : le test du mode y est rare.
- Environ 6 à 8 lignes.
- Temps visibles : **oui**. L'horloge de l'invité regagne jusqu'à 1,9 % sur l'UC pendant un
  travail mémoire.
- État sondé : `tsc`, `tsc_frac`, `current_diff` dans `h_state` (`tools/oracle/harness.h`,
  `iXtal26/Diag/HState.cs:133`). Le mode matériel est hors de portée de l'oracle par
  construction.

**5. Vérification.**
- `--timer-check` en mode matériel, sur le 5150, l'XT, la M24 et le PC1512 :
  - « cycles jamais portés au tsc » à 0 (le rapport le compte déjà, § M16) ;
  - Δtsc par tranche égal à 3 × 47 727 en moyenne, à l'invite comme pendant le test mémoire ;
  - écart de fréquence réduit aux seuls −5,87 ppm de N10, contre l'attendu documenté de
    1 193 182 / 65 536 Hz.
- C'est l'instrument qui a trouvé le défaut : vérification forte.

**6. Classement.** (a).

**7. Dépendances.**
- N10 (`pc.c:473`, mal attribué à PB-11 dans l'entrée).
- PB-11 et PB-12 (même cœur, section C, domaine UC) sont indépendants.
- Marqueur à poser avant tout. Même fichier que le domaine UC : à coordonner (§ 7).

### PB-05 — `pic.c:369` efface le mauvais bit de `pend`

**1. Défaut et effet.**
- En acquittant l'IRQ `8 + c` de l'esclave, `picinterrupt` fait `pic.pend &= ~(1 << c)` sur le
  **maître**, au lieu du bit 2 de la cascade (que `pic_updatepending` recalcule de toute façon).
- Une demande du maître qui attend à la même position est perdue :

  | IRQ du maître perdue | IRQ de l'esclave acquittée | Exemple dans le dépôt |
  |---|---|---|
  | IRQ0 (horloge) | IRQ 8 (RTC) | — |
  | IRQ1 (clavier) | IRQ 9 (l'IRQ 2 redirigée) | carte son réglée en IRQ 2 |
  | IRQ3 (COM2) | IRQ 11 | AHA-1542C |
  | IRQ4 (COM1, la souris série) | IRQ 12 | — |
  | IRQ6 (disquette) | IRQ 14 (IDE primaire) | — |
  | IRQ7 (LPT1) | IRQ 15 (IDE secondaire) | IRQ 7 = la SB 16 de `ixtal26-486-sb16.cfg` ; IDE secondaire = le CD-ROM au défaut de PCem, `cdrom_channel = 2` |

- Effets **déduits, non mesurés** : un top d'horloge perdu ; un clavier figé jusqu'à une lecture
  de 60h ; une disquette en délai dépassé ; une IRQ de la SB perdue pendant une lecture de CD.
- La perte exige que la demande du maître attende au moment de l'acquittement. N1 rend ce cas
  fréquent pour IRQ0 et IRQ1, puisque la cascade passe avant elles.

**2. Sites.**
- PCem : `pcem-dev/src/models/pic.c:368-369`, dans `picinterrupt` (`:355-394`).
- C# : `iXtal26/Models/pic.cs:433-439` (marqueur sans identifiant).

**3. Vrai comportement.**
- **Documenté** : à l'INTA, « the highest priority ISR bit is set and the corresponding IRR bit
  is reset » (Intel 8259A, 231468-003, « Interrupt Sequence », p. 7). Rien d'autre ne bouge dans
  l'IRR du maître.
- **Documenté** : en cascade, le maître ne remet que l'IRR de l'entrée de l'esclave (p. 19,
  « Cascade Mode »).
- **Documenté** : ordre des priorités de l'AT — IRQ0, IRQ1, IRQ 8 à 15 par la cascade, puis IRQ3
  à 7 (IBM AT TR 1502494, « System Interrupts », p. 1-10/1-11).

**4. Correction en mode matériel.**
- Correction minimale : ne plus effacer ce bit, ou effacer le bit 2. 2 à 3 lignes.
- Seule, elle laisse N1 et N2. La correction cohérente comprend deux parties, environ 35 lignes :
  - un `picinterrupt` « selon la fiche » : la demande la plus prioritaire hors IMR et hors
    `mask2`, sur le maître ; si c'est IR2, la même règle dans l'esclave ;
  - sur le 808x, l'acceptation sur `pic_intpending` (qui tient déjà compte de `mask2`) au lieu de
    `pic.pend & ~pic.mask`.
- Chemin chaud :
  - `picinterrupt` : non, il tourne une fois par interruption ;
  - l'acceptation du 808x (`takeint` ; `IRQTEST` à chaque itération des REP) : oui. Le coût ne
    change pas (une lecture de champ contre une autre), seul s'ajoute le test du mode.
- Temps visibles : **oui** (ordre des interruptions, plus de perte).
- État sondé : aucune sonde du PIC n'existe. `h_state` ne porte que `n_picint` et
  `n_picinterrupt`. Tout le flot d'instructions dépend de l'ordre des IRQ.

**5. Vérification.**
- Un banc dirigé **PICBANC**, à écrire : en C# seul en mode matériel, et sous l'oracle en mode PCem
  pour montrer la reproduction. Trois scénarios :
  1. AT : CLI, attendre que l'IRQ0 (PIT) et l'IRQ8 (RTC périodique, registre B bit 6, transcrit)
     soient toutes deux dans l'IRR (lecture IRR par OCW3), puis STI. Attendu : IRQ0 puis IRQ8,
     aucune perte. PCem : IRQ8 d'abord, IRQ0 perdue.
  2. AT : l'IRQ1 forcée par la commande D2h du 8042 contre l'IRQ 9 forcée par la commande F2h du
     DSP d'une SB réglée en IRQ 2 (les deux sont transcrites).
  3. 5150 : une frappe injectée pendant l'INT 08h. Attendu : l'IRQ1 n'est servie qu'après l'EOI
     (fiche, p. 15). PCem : elle est imbriquée.
- Attendus : fiche 8259A et AT TR.
- Plus une **sonde du PIC**, une vingtaine de champs. `pic` et `pic2` sont des globales non
  statiques de PCem, lisibles par le harnais. En mode PCem, sous l'oracle, elle rendrait enfin
  PB-06 visible.

**6. Classement.** (a).

**7. Dépendances.** N1, N2, N3 ; PB-06 et PB-13 (même fichier, absorbés par une réécriture) ;
PB-69 et PB-92 (l'IRQ 13 et l'IRQ 10 perdues sans second PIC : autres domaines, même `picint`).

### PB-06 — `pic.c:39` écrit `pic.mask2` dans le bloc `pic2`

**1. Défaut et effet.**
- `pic_reset()` remet le `mask2` du maître à la place de celui de l'esclave : `pic2.mask2` survit
  à une remise à zéro.
- Effet : **aucun** d'observable. Après `pc_reset`, `pic2.mask` vaut FFh et `pic2.pend` 0. Le
  POST envoie ICW1 à l'esclave, qui remet `mask2` à zéro (`pic.c:214-221`), avant de démasquer
  quoi que ce soit.
- La remise à zéro du seul processeur (8042, `softresetx86`) ne passe pas par `pic_reset`.

**2. Sites.** PCem : `pic.c:39` (dans `:30-44`). C# : `Models/pic.cs:70-72`.

**3. Vrai comportement.**
- **Documenté** : le 8259A n'a pas de broche de remise à zéro (fiche Intel, Table 1, p. 2).
  Seul ICW1 l'initialise : front réarmé, IMR effacé… (p. 9-10). Son état à la mise sous tension
  n'est pas défini.
- **Déduit** : `pic_reset` modélise une mise sous tension, où toute valeur est aussi « vraie »
  qu'une autre.

**4. Correction.** `pic2.mask2 = 0` en mode matériel (1 ligne), ou rien. Ni chemin chaud, ni
temps visible. Aucune sonde ne la voit.

**5. Vérification.** Seulement par la sonde du PIC proposée en PB-05 : remise à zéro pendant une
IRQ de l'esclave, puis relevé de `pic2.mask2`.

**6. Classement.** (d) — nettoyage sans effet. Classé en A au registre, il serait mieux en C.

**7. Dépendances.** Le groupe du PIC (PB-05, PB-13, N1, N2).

### PB-157 — Le 8237 haut : la commande, l'état des requêtes, les registres relus

**1. Défaut et effet.**
- (a) L'écriture de la commande du 8237 haut (D0h) est ignorée : `dma16_command` reste à 0, et
  le bit 2 (désactivation du contrôleur) n'agit pas sur les canaux 5 à 7. Celle du bas est
  rangée.
- (b) Les bits 4 à 7 de l'état (« requête en cours ») ne remontent jamais sur l'ISA, **pour les
  deux 8237** : `dma_stat_rq` n'est lu que par le PS/2.
- (c) `dma16_read` rend, pour DAh, DCh et DEh, le dernier octet écrit.
- Effets :
  - un programme qui désactive le 8237 haut n'y parvient pas ;
  - DAh se relit avec la dernière valeur écrite, au lieu du registre temporaire ;
  - un programme qui lirait les bits de requête (pour trouver un canal DMA en le masquant, par
    exemple) ne les voit jamais monter. **Déduit** ; aucun programme témoin identifié.

**2. Sites.**
- PCem :
  - `dma.c:389-390` (commande haute ignorée) ; `:124-126` (la basse, rangée) ;
  - `:83-85` et `:344-347` (état : TC seuls) ; `:198-204` (requêtes, PS/2 seul) ;
  - `:349` (`return dma16regs[...]`, cité `:345`) ;
  - `:549` et `:618` (`dma_stat_rq` posé).
- C# : `Models/dma.cs:311-313` et `:359-361`.
- Oracle :
  - `tools/oracle/harness_dma.c` (la sonde du DMA, 29 champs, dont `dma16_command`,
    `dma_stat_rq` et l'empreinte de `dma16regs`) ;
  - `tools/sb16banc/sb16banc.S:276-278` (relit D0h, DAh, DEh).

**3. Vrai comportement.**
- **Documenté**, fiche Intel 8237A, 231466-005, septembre 1993 :
  - le registre de commande est « cleared by Reset or a Master Clear instruction », et son bit 2
    désactive le contrôleur (p. 7) ;
  - registre d'état : bits 0-3 = TC, effacés à chaque lecture ; bits 4-7 « set whenever their
    corresponding channel is requesting service » (p. 8-9) ;
  - registre temporaire : le dernier octet d'un transfert mémoire-mémoire, effacé par Reset
    (p. 9) ;
  - Figure 6 (p. 9) : la lecture des registres 9h, Ah, Bh, Ch, Eh et Fh est « Illegal ».
- **Documenté**, côté AT (IBM AT TR 1502494, « Programming the 16-Bit DMA Channels », p. 1-14) :

  | Port | Lecture | Écriture |
  |---|---|---|
  | D0h | état | commande |
  | D2h | — | requête |
  | D4h | — | masque simple |
  | D6h | — | mode |
  | D8h | — | bascule |
  | DAh | registre temporaire | master clear |
  | DCh | — | effacement des masques |
  | DEh | — | écriture de tous les masques |

- **Documenté** (listing du BIOS de l'AT, module TEST1) : le POST écrit 04h en 08h et en D0h
  (« DISABLE DMA CONTROLLER 1 / 2 », point de contrôle 4), puis envoie un master clear à chacun
  (`OUT DMA+0DH` ; `OUT DMA1+0DH*2`, test 07).
- **Déduit** : le registre temporaire du 8237 haut vaut 0 sur un AT. Le mémoire-mémoire passe par
  les canaux 0 et 1 du contrôleur, c'est-à-dire 4 (la cascade) et 5.
- **Déduit** : PCem n'a pas de ligne DREQ (un périphérique tire un octet quand il le veut). Une
  « requête en cours » n'y existe que si un transfert demandé est refusé (canal masqué ou
  contrôleur désactivé).
- **Documenté**, contrainte : le POST de l'XT lit l'état et s'arrête si le bit de requête du
  canal 0 est posé à ce moment (« IS TIMER REQUEST THERE? (IT SHOULDN'T BE) … HALT SYS. (HOT
  TIMER 1 OUTPUT) », IBM XT TR 6361459, listing du BIOS, p. 5-30). Un modèle qui garderait une
  requête après chaque transfert, comme le chemin PS/2 de PCem, arrêterait ce POST.
- **Inconnu** :
  - ce que rend une lecture « Illegal » (DCh, DEh) d'un 8237A-5 dans un AT ;
  - ce que rendent les contrôleurs intégrés des chipsets du dépôt (NEAT, Headland, OPTi, ALi :
    82C206 ou équivalent).

  À mesurer.

**4. Correction en mode matériel.**
- (a) Ranger `dma16_command` : 1 ligne, **avec** N6 (le master clear efface la commande, l'état,
  la requête et le temporaire : environ 4 lignes par contrôleur).
- (b) Lire DAh = 00h : 2 lignes.
- (c) DCh et DEh : inconnu. Garder le comportement de PCem en le marquant, ou adopter FFh : à
  décider (Q5).
- (d) Bits de requête, optionnels (environ 15 lignes dans `dma_channel_read/write`, tiède) :
  - posés par un transfert refusé ;
  - effacés par le service, le démasquage, un master clear ou un reset.
- À y joindre : N4 (effacement des masques), N7 (masques posés au reset), N8 (la cascade).
- Total : 25 à 45 lignes. Chemin chaud : non, sauf (d) et N8, appelés une fois par octet DMA.
- Temps visibles : non, sauf quand un logiciel désactive ou masque.
- État sondé : la sonde du DMA (`dma16_command`, `dma_stat`, `dma_stat_rq`, `dma16regs`,
  `dma_m`). Elle reste celle du mode PCem.

**5. Vérification.**
- Un banc **DMABANC** (ou SB16BANC étendu), en C# seul :

  | Geste | Attendu |
  |---|---|
  | 04h en D0h, puis un DMA 16 bits de la SB 16 | aucun mot transféré (compteur inchangé, pas d'IRQ) |
  | master clear en DAh | commande effacée, le DMA repart |
  | lecture de DAh | 00h |
  | écriture en DCh | masques 5-7 effacés |
  | lectures d'état | TC, puis 0 |

- Attendus : la fiche Intel et l'AT TR.
- Témoins en mode matériel : POST, DOS et Windows 3.1 sur ibmat, ami286, ami386, ami386dx et
  ami486 (leurs BIOS programment ces registres) ; le POST du 5150 et de l'XT si (d) est fait.

**6. Classement.** (a) pour la commande (avec N6) et le registre temporaire ; (c) pour DCh/DEh et
pour les bits de requête : la sémantique est documentée, la modélisation sans DREQ est un choix.

**7. Dépendances.**
- N4 à N8.
- SB16BANC et la sonde du DMA : un second jeu d'attendus.
- PB-90 et PB-147 (son : ils lisent le retour de `dma_channel_read`).
- Le listing du POST de l'AT : la relecture faite ici est OCR et partielle. Il faut savoir s'il
  réécrit 00h en D0h, ce qui rendrait N6 non bloquant pour le contrôleur haut.

### PB-94 — La souris PS/2 ne répond pas aux commandes qu'elle ne connaît pas

**1. Défaut et effet.**
- `mouse_ps2_write` ne traite que E6h-E9h, EBh, F2h-F5h et FFh. Toute autre commande (F6h, EAh,
  F0h, EEh, ECh, FEh…) ne reçoit rien, pas même FAh.
- Les modes distant et écho ne sont jamais posés.
- Les données hors plage de E8h et F3h sont acceptées.
- Effet : un pilote qui envoie F6h ou F0h attend son FAh jusqu'au délai, puis conclut à une
  souris absente.
- Atteint seulement par la porte `--force-ps2` : aucune machine ne monte la souris PS/2
  (décision du 03/10).

**2. Sites.**
- PCem : `mouse_ps2.c:36-161` (le `default` commenté à `:144-145`) ; `:12`.
- C# : `Mouse/mouse_ps2.cs:90-92`.
- Banc et série : `tools/ps2banc/ps2banc.py:211` ; `tools/gates/series.sh:250-265`.

**3. Vrai comportement.**
- **Documenté (secondaire)** — Chapweske 2001 ; Brouwer, « The PS/2 mouse » :
  - toute commande ou donnée valide est acquittée par FAh ; une invalide reçoit FEh, la suivante
    si elle l'est encore FCh ;
  - F6h : 100 rapports/s, 4 points/mm, 1:1, rapport désactivé (Brouwer ajoute le mode flux) ;
  - F0h : mode distant, plus de paquet spontané, EBh les lit ;
  - EAh : mode flux ;
  - EEh : écho, tout octet renvoyé sauf ECh et FFh ; ECh en sort ;
  - FEh : renvoi du dernier paquet.
- **Documenté** (primaire, IBM 15F0306, INT 15h C2h, AL = 05h et 01h, p. 2-96) : l'état après
  initialisation est désactivé, 100 rapports/s, 4 points/mm, 1:1.
- Une description des commandes de la souris par IBM elle-même n'a pas été trouvée : la
  *Hardware Interface TR* (1988 et 1990) ne décrit que le contrôleur.

**4. Correction en mode matériel.**
- Les commandes manquantes ; le dernier paquet retenu pour FEh ; le refus FEh/FCh.
- Le mode distant est déjà honoré par `mouse_ps2_poll` (`mouse->mode == MOUSE_STREAM`).
- Environ 40 à 60 lignes. Pas de chemin chaud.
- Temps visibles : oui, un pilote ne tombe plus en délai.
- État sondé : la sonde de la souris (9 champs, PS2.0), dont `mode`, `flags` et `command`.

**5. Vérification.**
- PS2BANC en C# seul sous `--force-ps2`, avec un second jeu d'attendus :

  | Commande | Attendu |
  |---|---|
  | F6h | FAh, puis E9h → `00h 02h 64h` |
  | F0h | FAh, plus de paquet spontané ; EBh en rend un |
  | EEh | écho de chaque octet |
  | commande invalide | FEh, puis FCh |

- Les attendus reposent sur des sources secondaires. Une mesure sur une vraie souris PS/2 (tout PC
  à port PS/2) les fixerait.

**6. Classement.** (b).

**7. Dépendances.** PB-95 (le bit 6 d'E9h suit le mode distant) ; N9 (E9h pousse 4 octets dans
la file, l'écho davantage) ; la décision du 03/10.

### PB-95 — L'état de la souris PS/2 code le bouton du milieu comme gauche et droit

**1. Défaut et effet.**
- E9h code le milieu par `temp |= 3`. **Constat de cette relecture** : le vrai octet d'état met
  le gauche en bit 2, le droit en bit 0 et le milieu en bit 1 ; PCem met le gauche en bit 0 et
  le droit en bit 1.
- Effet : un pilote qui lit l'état voit le gauche comme « droit », le droit comme « milieu » (ou
  bit réservé), le milieu comme « droit + milieu ».
- Seulement par `--force-ps2`.

**2. Sites.**
- PCem : `mouse_ps2.c:76-89` (le milieu à `:83-84`).
- C# : `Mouse/mouse_ps2.cs:110-125` (marqueur `:117`).
- PS2BANC relit E9h, bouton du milieu tenu : 23h (VERIFICATION.md § PS2.0).

**3. Vrai comportement.**
- **Documenté** (primaire) : IBM *PS/2 and PC BIOS Interface Technical Reference* 15F0306,
  INT 15h AH = C2h, AL = 06h, BH = 00h « Return status », octet d'état 1 :

  | Bit | Sens |
  |---|---|
  | 6 | mode distant |
  | 5 | activée |
  | 4 | échelle 2:1 |
  | 2 | bouton gauche |
  | 1 | réservé |
  | 0 | bouton droit |

  Source : p. 2-96/2-97. Même disposition pour l'ABIOS, fonction 03h « Read Device Parameters »
  (p. 6-117).
- **Documenté (secondaire)**, Chapweske 2001 : le milieu en bit 1 sur une souris à trois boutons.

**4. Correction en mode matériel.**
- Gauche → bit 2, droit → bit 0.
- Milieu → bit 1 si la souris a trois boutons, comme le paquet qui teste `MOUSE_TYPE_3BUTTON`.
- Distant → bit 6, avec PB-94.
- Environ 6 lignes. Ni chemin chaud, ni temps. La sonde de la souris n'est pas touchée (l'octet
  est calculé à la volée), le flot d'instructions l'est.

**5. Vérification.** PS2BANC E9h en C# seul, attendus IBM :

| Cas | Attendu |
|---|---|
| souris à deux boutons, activée, milieu tenu | 20h |
| Intellimouse, milieu tenu | 22h |
| gauche | 24h |
| droit | 21h |

**6. Classement.** (a).

**7. Dépendances.** PB-94.

### PB-101 — `lpt2_remove_ams` ne retire rien

**1. Défaut et effet.**
- `lpt2_remove_ams` retire des gestionnaires en 379h-37Ah, où LPT2 n'est pas : le PC1512 garde le
  LPT2 que `lpt_init` pose en 278h.
- Effet (**déduit, non mesuré**) : un second port parallèle, en 278h, que la machine n'a pas.
  Un BIOS qui balaie 278h l'inscrit au BDA. Le BIOS du 5150 balaie 3BCh, 378h et 278h (IBM PC TR,
  listing du BIOS, annexe A, vers la p. A-15) ; ce que fait celui du PC1512 est **inconnu**.

**2. Sites.**
- PCem : `lpt.c:166`, `:142-145` ; `amstrad.c:143-144`.
- C# : `Lpt/lpt.cs:257-260` ; `Models/amstrad.cs:172-175`.

**3. Vrai comportement.**
- **Documenté** : le PC1512 n'a qu'un port parallèle intégré, en 378h-37Ah, et 379h porte aussi
  les liens LK1-LK3 ; 278h-27Fh est réservé à un « External Printer Port » sur une carte
  d'extension (Amstrad *PC1512 Technical Reference Manual*, Section 1, § 1.3, § 1.4, § 1.10).
- **Déduit** : sans carte, rien ne répond en 278h. La lecture rend FFh, valeur que l'émulateur
  rend pour un port sans gestionnaire.

**4. Correction en mode matériel.** `amstrad_init` appelle `lpt2_remove()` (qui retire 278h) au
lieu de `lpt2_remove_ams()` : environ 3 lignes. Ni chemin chaud, ni temps, ni sonde.

**5. Vérification.**
- D'abord en mode PCem sous l'oracle : LPTBANC sur le PC1512 (BDA en 0040:0008, relecture de
  278h). C'est la mesure qui manque à l'entrée.
- Puis en C# seul, en mode matériel : BDA sans 0278h ; 278h et 27Ah relus FFh.
- Témoins : `MODE`, MSD « LPT Ports ».

**6. Classement.** (a).

**7. Dépendances.** Aucune parmi les PB. La question générale de la fidélité de configuration
(LPT1 et LPT2 sur toutes les machines) est posée en Q3.

### PB-103 — La CH Flightstick Pro et la TM FCS n'ont pas de haut-gauche

**1. Défaut et effet.**
- Les deux manettes lisent le chapeau par quatre intervalles qui laissent 315° hors de tous.
- Effet (mesuré, JOYBANC, tours 5 et 10) : la CH lit le haut-gauche au repos, la TM le lit en
  bas.

**2. Sites.**
- PCem : `joystick_ch_flightstick_pro.c:26-33`, `joystick_tm_fcs.c:46-54`.
- C# : `Joystick/joystick_ch_flightstick_pro.cs:26-27`, et le test `> 315` à `:44` ;
  `Joystick/joystick_tm_fcs.cs:47-48` et `:65`.
- Portes : `bd-pc-joy-ch-banc`, `bd-pc-joy-tm-banc` (`tools/gates/series.sh:307-327`) ;
  `--joystick-check` (`Host/SdlJoystick.cs:337`).

**3. Vrai comportement.**
- **Documenté (secondaire)**, Nerdly Pleasures 2014 : la CH code son chapeau par combinaisons de
  boutons — haut 1+2+3+4, droite 1+2+4, bas 1+2+3, gauche 1+2 —, comme PCem. Le codage n'a que
  quatre valeurs : le chapeau réel est à quatre directions (**déduit**), une diagonale n'existe
  pas.
- La TM code le sien par une résistance sur l'axe Y2. L'ordre haut, droite, bas, gauche de PCem
  est celui du pilote Linux `analog.c` ; Nerdly Pleasures donne un autre ordre. Les sources se
  contredisent, mais c'est hors du sujet de PB-103.
- Ce que rend 315° est une **convention de l'hôte**, sans vérité matérielle.

**4. Correction en mode matériel.**
- `>= 315` au lieu de `> 315` dans les deux fichiers : 315° rejoint « haut », comme 45° rejoint
  « droite », 135° « bas » et 225° « gauche » (la borne basse incluse partout).
- 2 lignes et le test du mode. Ni chemin chaud, ni temps, ni sonde de l'oracle.

**5. Vérification.** JOYBANC en C# seul, mode matériel, tours 5 et 10 : la CH lit « haut »
(boutons 1 à 4), la TM lit l'axe 3 à −32768.

**6. Classement.** (a), sur une convention à valider (Q6).

**7. Dépendances.** PB-104 : une fois PB-104 corrigé, 315° arrive chaque fois qu'on pousse le
chapeau de l'hôte en haut à gauche. Les deux vont ensemble.

### PB-104 — Sans correspondance explicite, le chapeau se calcule sur un axe contre lui-même

**1. Défaut (de l'hôte).** Sans correspondance explicite, le chapeau d émulé se calcule sur
(axe d, axe d) de la manette de l'hôte, et le chapeau de l'hôte est ignoré. Mesuré
(`--joystick-check`) : manche à droite → 135°, à gauche → 315°.

**2. Sites.**
- PCem : `pc.c:800-803` ; `wx-sdl2-joystick.c:63-95`.
- C# : `pc.cs:674-676` (marqueur) ; `:707-710` (les deux défauts `d`) ; `:719` (le repli de
  `joystick_mapping`) ; `Host/SdlJoystick.cs:343-352`.

**3. Vrai comportement.** Sans objet matériel. La correction notée au registre (`PCEM_BUGS.md`,
PB-104 ; `PLAN.md` § G13 ; `PLAN-G10.md`) est `POV_X | d` et `POV_Y | d`.

**4. Correction en mode matériel.** Ces défauts dans `load_joysticks` et dans le repli R9 de
`joystick_mapping` : environ 4 lignes, au chargement seulement. Une configuration explicite reste
lue telle quelle.

**5. Vérification.**
- `--joystick-check`, cas 2 en mode matériel : correspondances (POV_X|0, POV_Y|0) ; manche à
  droite et chapeau en haut → 0° ; manche à gauche et chapeau au repos → −1.
- `r9-joycfg` pour le repli. C# seul par nature : l'oracle n'a pas de manette hôte.

**6. Classement.** (a), correction de l'hôte (Q3).

**7. Dépendances.** PB-103 ; PB-93 (les replis R9 des correspondances, même fonction).

---

## 5. Section B

### PB-07 — `readmemw` déréférence un `uint16_t*` au-delà de l'allocation

**Statut (à vérifier, demandé) : ni reproduit, ni corrigé — neutralisé.**
- `h_pad_ram()` (`tools/oracle/harness.c:562`) donne quatre octets nuls à l'oracle, et
  `mem_alloc()` en C# aussi (`Memory/mem.cs:1417`, cité `:706`).
- L'entrée n'a pas de ligne « Reproduit » ; le C# n'a pas de marqueur.

**1. Défaut et effet.**
- Les chemins rapides de `readmemw` et de `writememw` du 808x lisent ou écrivent les deux octets
  d'un mot d'un seul tenant, sans tester qu'il franchit la fin de sa page. Au sommet de la RAM,
  l'octet haut sort du tableau. Chez PCem, c'est du tas : 0E59h, 4959h, D859h en trois
  exécutions. Aujourd'hui, 0 des deux côtés.
- Cas atteignables :
  - (i) le haut de la RAM conventionnelle, par un mot en 9FFFFh à 640 Ko ou au sommet d'une RAM
    plus petite, sur le 5150, l'XT, la M24 et le PC1512 ;
  - (ii) le repli à 1 Mo, par un mot en FFFFFh, seulement si la page FFh est en RAM : la RAM
    plate du fuzzeur. Sur une vraie configuration elle est en ROM, et le chemin lent replie déjà
    juste.

**2. Sites.**
- PCem : `808x.c:73-80` (`readmemw`), `:105-111` (`writememw`) ; `mem.c:1344` (malloc sans
  marge) ; `mem.c:484-522` (`readmemwl`) et `:524` (`writememwl`) : le chemin lent coupe le mot
  à la page et masque par `rammask`.
- C# : `Cpu/808x.cs:109-120`, `:138-148` ; `Memory/mem.cs:1414-1417`.

**3. Vrai comportement.**
- **Documenté** : le 8088 a 20 bits d'adresse (« supports 20 bits of addressing (1 megabyte of
  storage) », IBM PC TR, p. 2-3). L'octet haut d'un mot en FFFFFh se lit en 00000h.
- **Déduit** : au-dessus de la RAM, l'octet haut vient de ce qui est projeté à l'adresse
  suivante (la mémoire d'une EGA ou d'une VGA en A0000h), ou de rien. Le bus flottant rend FFh
  dans le modèle de PCem et d'iXtal.
- **Inconnu** : la valeur réelle du bus flottant d'un 5150, à mesurer (FFh attendu).

**4. Correction en mode matériel.**
- Envoyer au chemin lent (`readmemwl`/`writememwl`) tout mot dont l'octet bas est le dernier de
  sa page de 4 Ko : une condition de plus dans les deux fonctions, environ 4 à 6 lignes.
- **Chemin chaud** : chaque accès mot du 808x. Le test du mode doit coûter zéro (§ 8).
- Temps : la facturation de `memcycs` précède le choix du chemin et ne change pas. À vérifier :
  `timing_misaligned` sur le 808x.
- État sondé : les registres de `h_state`.

**5. Vérification.**
- Test dirigé en C# seul :
  - POP CX avec SS = FFFFh et SP = 000Fh sur la RAM plate du fuzzeur → CH = `ram[0]` ;
  - MOV AX,[000Fh] avec DS = 9FFFh sur un 5150 à CGA → AH = FFh.
- Le corpus SST 8088 (silicium réel), en mode matériel, s'il contient des accès à cheval sur
  FFFFFh.

**6. Classement.** (a) pour le repli à 1 Mo ; la valeur du bus flottant est déduite.

**7. Dépendances.** Domaine UC : même fonction que PB-11 (section C), voisin de PB-87 et de N11.
À traiter avec le 808x (§ 7).

### PB-08 — `device.c:300` teste la borne après l'accès

1. **Défaut et effet.** `pcem_add_device` lit `devices[c]` avant de tester `c < 256`. Avec 256
   périphériques, il lit hors du tableau ; en C#, une exception remplace le `fatal` « too many
   devices ». Effet : **aucun**, le dépôt enregistre au plus une vingtaine de périphériques et
   l'invité n'en ajoute pas.
2. **Sites.** PCem : `plugin-api/device.c:300-301`. C# : `PluginApi/device.cs:327-331` (marqueur
   sans identifiant).
3. **Vrai comportement.** Sans objet matériel : c'est une limite d'une table de l'émulateur.
4. **Correction.** Aucune en mode matériel. Si l'on y touche : inverser les deux opérandes,
   1 ligne, neutre pour toutes les portes, dans les deux modes.
5. **Vérification.** —
6. **Classement.** (d).
7. **Dépendances.** —

### PB-10 — Double `fclose` dans `loadbios`

1. **Défaut et effet.** `loadbios` referme le fichier de la ROM du 5150 déjà fermé quand
   `mem_load_basic` échoue. Effet : **aucun**, `Close` est idempotent en .NET, et ce n'est que le
   chemin d'échec du BASIC.
2. **Sites.** PCem : `memory/mem_bios.c:549`, `:1280-1281`. C# : `Memory/mem_bios.cs:284-288`
   (marqueur sans identifiant).
3. **Vrai comportement.** Sans objet matériel.
4. **Correction.** Aucune.
5. **Vérification.** —
6. **Classement.** (d).
7. **Dépendances.** —

---

## 6. Section C

### PB-13 — Branche morte dans l'EOI spécifique du PIC

1. **Défaut et effet.** `val == 2` est testé dans un bloc où `val >= 60h` : la branche est morte.
   Effet : **aucun**.
   - Sur l'AT, `pic_updatepending()`, appelé juste après, recalcule le bit 2 du maître avec la
     même condition.
   - Sur le PC et l'XT, `pic2.pend` reste nul : `picint` refuse les IRQ au-delà de 7 hors AT,
     et `picintlevel` n'a pour appelant transcrit que le port série, en IRQ 3 ou 4.
2. **Sites.** PCem : `pic.c:121`. C# : `Models/pic.cs:159-168` (marqueur sans identifiant).
3. **Vrai comportement.** **Documenté** : l'EOI spécifique remet à zéro le bit ISR désigné
   (8259A, p. 15). **Déduit** : la remise en attente de la cascade vient du niveau de la sortie
   INT de l'esclave, que `pic_updatepending` rend déjà.
4. **Correction.** Une correction en mode matériel n'a pas de sens propre : la ligne disparaît
   dans la réécriture du PIC (PB-05). Sinon, `(val & 7) == 2`, 1 ligne, sans effet.
5. **Vérification.** —
6. **Classement.** (d) — nettoyage sans effet.
7. **Dépendances.** Le groupe du PIC.

### PB-33 — `savenvr` écrit dans un fichier qu'il n'a pas vérifié avoir ouvert

1. **Défaut et effet.** `savenvr` écrit sans vérifier `nvrfopen`, qui rend NULL en écriture si
   le chemin n'est pas ouvrable (un `nvr/` absent suffit). L'hôte tombe à la sortie
   (NullReferenceException) et le CMOS de la session est perdu. Mesuré dans ce dépôt. Aucun effet
   pour l'invité.
2. **Sites.**
   - PCem : `devices/nvr.c:770-772`, et `:50-52` pour le NULL.
   - C# : `Devices/nvr.cs:510-516`, appelé par `BootTest.cs:64` et `Host/SdlHost.cs:1356`.
   - L'oracle n'appelle jamais `savenvr` : il appelle seulement `loadnvr` (`harness.c:2080`).
3. **Vrai comportement.**
   - **Documenté** : le CMOS de l'AT est la RAM d'un MC146818 tenue par une pile. Sa perte se
     signale par le bit VRB du registre D (IBM AT TR, « RT/CMOS RAM », p. 1-45 à 1-48).
   - **Déduit** : l'équivalent matériel d'un CMOS non sauvegardé est une pile morte. Le plantage,
     lui, n'en a aucun.
4. **Correction.** En mode matériel : sans objet. Proposée **dans les deux modes**, comme
   correction de l'hôte : si `f` est nul, un avertissement qui nomme le chemin, et rien d'écrit
   (environ 4 lignes). Aucune porte n'y passe.
5. **Vérification.** Test en C# seul : `nvr_path` pointé vers un répertoire temporaire absent →
   avertissement, code de sortie 0, aucune exception. Jamais dans le `nvr/` du dépôt.
6. **Classement.** (d) pour le mode matériel ; correction de l'hôte à décider (Q2).
7. **Dépendances.** `config_name` jamais affecté (registre des omissions de `TRANSCRIPTION.md`).

---

## 7. Proposition d'étapes pour le domaine

Préalables communs à G13, que le pilote planifie :
- l'interrupteur « mode matériel » : clé de `.cfg`, option, écran de réglage, `--setup-check`,
  porte `r9-*` des valeurs hors liste ;
- un exécuteur de bancs en C# seul, avec frappes injectées et relevé comparé à un fichier
  d'attendus.

Chaque étape ci-dessous change l'émulateur. Elle demande donc une série entière en mode PCem, qui
doit rester identique à la précédente (preuve d'inertie), plus ses portes du mode matériel et un
contrôle négatif : une panne injectée dans le code du mode matériel doit rougir son banc.

| Étape | Contenu | Vérification | Série |
|---|---|---|---|
| **D6.0 — Préalable, sans changement de comportement** | Les marqueurs du § 3.1 (identifiants ; PB-03 ; PB-07 en `DEVIATION`) ; les amendements du § 3.2 ; l'inscription de N1 à N11 si Q1 le décide ; R1(d) amendée | Build, zéro avertissement, grep des marqueurs. Sous-ensemble ciblé de moins de 15 min : commentaires et documents seulement | non |
| **D6.1 — L'hôte et les ports** | PB-104, PB-103, PB-101 ; PB-33 dans les deux modes si Q2 | `--joystick-check` et JOYBANC (attendus du mode matériel) ; LPTBANC sur le PC1512, d'abord mesuré en mode PCem, puis en mode matériel ; test de PB-33 | une |
| **D6.2 — Le 8237 selon la fiche** | PB-157 avec N4, N6, N7, N8 ; bits de requête selon Q5 | DMABANC ou SB16BANC étendu ; témoins POST, DOS et Windows sur les cinq AT ; POST du 5150 et de l'XT si les bits de requête entrent | une |
| **D6.3 — Le 8259A selon la fiche** | PB-05 avec N1, N2 (N3 selon Q1) ; PB-06 et PB-13 absorbés ; une sonde du PIC sous l'oracle en mode PCem | PICBANC sur l'AT et sur le 5150 ; sonde du PIC (panne injectée en mode PCem) ; témoins sur toutes les machines | une |
| **D6.4 — Le temps et la mémoire du 808x** | PB-03, PB-07, à coordonner avec le domaine UC (même fichier, PB-11, N11) ; ou faits dans l'étape UC | `--timer-check` sur le 5150, l'XT, la M24 et le PC1512 ; test C# de PB-07 ; SST 8088 en mode matériel ; mesure de performance (`tools/perfbanc`) en mode PCem (aucune régression) et en mode matériel | une |
| **D6.5 — La souris PS/2** | PB-94, PB-95 — ou reportés (Q8) | PS2BANC sous `--force-ps2`, attendus IBM et sources secondaires | avec D6.4 |
| **Clôture** | Témoins du mode matériel par machine (POST, `VER`, `MSD /S`, Windows sur l'ami486) ; preuve que le mode PCem est intact ; entrées de `VERIFICATION.md` ; marqueurs convertis | — | — |

**Pourquoi cet ordre.**
- L'hôte et les ports d'abord. Ce sont des corrections de quelques lignes, sans chemin chaud :
  elles éprouvent l'interrupteur et l'exécuteur en C# seul à faible risque.
- Cela inverse l'ordre de `PLAN.md` (« la carte mère, la souris et les ports ») ; on peut garder
  ce dernier sans dommage.
- Le DMA vient avant le PIC parce que ses effets sont plus étroits. Le PIC change l'ordre de
  toutes les interruptions.
- PB-03 et PB-07 sont dans `808x.cs`, avec PB-11, et se mesurent avec les outils de l'UC (SST,
  banc de performance). Les faire dans l'étape UC épargne une série.
- Au plus quatre séries entières pour le domaine (D6.1 à D6.4, D6.5 jointe à D6.4), jamais deux
  en parallèle.

---

## 8. Risques

1. **Corriger isolément peut aggraver.**
   - PB-157 sans N6 peut laisser le 8237 haut désactivé après le POST de l'AT, et casser le DMA
     16 bits de la SB 16 sur ibmat. Cela dépend de ce que le BIOS réécrit ensuite en D0h, à
     relire dans le listing complet.
   - PB-05 sans N1 et N2 laisse le PIC faux autrement.
   - Un modèle naïf des bits de requête arrête le POST de l'XT (« HOT TIMER 1 OUTPUT »).
2. **Les BIOS en mode matériel.**
   - Masques posés au reset (N7) et cascade (N8) : l'IBM AT démasque le canal 4 dans `ROM_SCAN`
     (listing, « SET DMA MASK AND REQUEST REGISTERS »).
   - Les BIOS AMI sont des ROM sans listing. Seuls des témoins en mode matériel, machine par
     machine, montreront qu'ils passent.
3. **L'ordre des interruptions change** en mode matériel (D6.3). Des périphériques transcrits qui
   « marchaient » sous l'ordre de PCem peuvent révéler un défaut à eux : il faut des témoins
   longs (DOS, Windows, son, CD) en mode matériel.
4. **Coût sur les chemins chauds du 808x** (`clockhardware`, `readmemw`, `IRQTEST`).
   - Piste : un `static readonly bool` lu une fois la configuration chargée. Le JIT le plie en
     constante au palier 1, le coût devient nul en mode PCem, mais le mode est alors fixé pour
     tout le processus (Q4).
   - À mesurer avant la référence de performance de G14.
5. **Pas d'oracle pour le mode matériel.** Une erreur dans le code du mode matériel ne sera vue
   que par ses bancs : d'où les attendus documentés, la panne injectée par étape et la preuve
   d'inertie du mode PCem.
6. **Des sources incomplètes.**
   - Lectures « illégales » du 8237 et contrôleurs intégrés des chipsets : inconnu.
   - Commandes de la souris PS/2 : sources secondaires seulement.
   - Ordre du chapeau de la TM : sources contradictoires, hors PB-103.
   - Le listing du BIOS de l'AT n'a été lu ici que par OCR.
7. **Maintenance.** SB16BANC, JOYBANC, PS2BANC et LPTBANC portent désormais deux jeux d'attendus.
   La sonde du DMA et celle de la souris ne servent qu'en mode PCem.
8. **Effort sans usage.** La souris PS/2 n'est offerte par aucune machine : PB-94 et PB-95
   corrigés ne changent rien pour l'utilisateur.

---

## 9. Questions de principe pour Julien

1. **Les défauts non inscrits** (N1 à N11) : les inscrire (PB-168 et suivants) et faire entrer
   dans G13 ceux qu'un 8259A et un 8237A cohérents exigent (N1, N2, N4, N6, N7, N8) ? Ou s'en
   tenir à la liste inscrite ? N3, N5, N9 et N11 resteraient à part.
2. **Les entrées sans contrepartie matérielle** (PB-06, PB-08, PB-10, PB-13) : les laisser
   « reproduites » pour toujours, ou les reclasser en C ? Et PB-33 : le corriger **dans les deux
   modes**, comme correction de l'hôte dans l'esprit de R9, puisque l'oracle ne passe jamais par
   `savenvr` ?
3. **La portée du mode matériel** : les seules corrections de PB, ou aussi la fidélité de
   configuration ? Par exemple, LPT1 et LPT2 sur toutes les machines, alors que le 5150, l'XT et
   l'AT n'ont pas de port parallèle sur la carte mère et que le PC1512 n'en a qu'un. Et les
   corrections de l'hôte (PB-103, PB-104) : sous le même interrupteur, ou sous un interrupteur
   « hôte » séparé ?
4. **L'interrupteur** : un seul et global, ou un par domaine (utile pour isoler une régression) ?
   Choisi au lancement seulement (coût nul sur les chemins chauds), ou changeable à chaud ?
5. **Les comportements inconnus** (lectures DCh/DEh, bits de requête dans un modèle sans DREQ,
   l'IRQ 7 « fantôme » de la fiche p. 7) : garder en mode matériel le comportement de PCem en le
   marquant, ou adopter une convention plausible ? Julien a-t-il accès à du matériel réel pour
   mesurer : un 5150 ou un XT, un AT 5170, un 486 à chipset intégré, un PC1512, une souris PS/2 ?
6. **PB-103** : 315° vers « haut » (la symétrie des bornes) ou vers « gauche » ?
7. **R1 et R2** : la forme `// pcem bug, fixed in hardware mode: PB-nn` entre dans R1(d), ce qui
   amende `TRANSCRIPTION.md`. Le code du mode matériel compte-t-il dans le plafond de +25 % de
   R2, ou a-t-il son propre budget ?
8. **La souris PS/2** : corriger PB-94 et PB-95 maintenant, vérifiés par `--force-ps2` seulement,
   ou les reporter jusqu'à une machine PS/2 transcrite ?
9. **PB-03 et PB-07** : dans l'étape de l'UC (même fichier `808x.cs`, mêmes outils SST et
   performance), ou dans celle de la carte mère ?

---

## 10. Sources

Les textes intégraux ont été lus hors du dépôt. Les pages sont celles imprimées dans le document.

1. Intel, *8259A Programmable Interrupt Controller (8259A/8259A-2)*, Order Number 231468-003,
   décembre 1988 — https://pdos.csail.mit.edu/6.828/2014/readings/hardware/8259A.pdf
   - p. 2 : Table 1, Pin Description, sans broche de remise à zéro.
   - p. 7 : Interrupt Sequence ; IRQ 7 en l'absence de demande.
   - p. 9-10 : ICW1.
   - p. 13-14 : formats des OCW.
   - p. 15 : Fully Nested Mode, End of Interrupt, Automatic Rotation.
   - p. 16 : Specific Rotation, Poll Command, Special Mask Mode.
   - p. 17 : Edge and Level Triggered.
   - p. 19 : Cascade Mode.
2. Intel, *8237A High Performance Programmable DMA Controller (8237A-5)*, Order Number
   231466-005, septembre 1993 — https://pdos.csail.mit.edu/6.828/2014/readings/hardware/8237A.pdf
   - p. 7 : Command Register, Request Register.
   - p. 8 : Mask Register, Status Register.
   - p. 9 : bits 4-7 de l'état, Temporary Register, Master Clear, Clear Mask Register, Figure 6
     « Software Command Codes » (lectures « Illegal »).
3. IBM, *Personal Computer Technical Reference*, 6025008, août 1981 —
   https://bitsavers.org/pdf/ibm/pc/pc/6025008_PC_Technical_Reference_Aug81.pdf
   (texte : archive.org, `bitsavers_ibmpcpc602renceAug81_17295874`)
   - « System Board », p. 2-3 et 2-4 : 4,77 MHz = 14,31818 / 3 ; 20 bits d'adresse ;
     rafraîchissement par DMA 0.
   - Entrée du 8253-5 à 1,19318 MHz.
   - I/O Address Map, p. 2-23/2-24 : 378-37F parallel printer port, 278-27F réservé.
   - Listing du BIOS, annexe A, vers la p. A-15 : ports imprimante 3BCh, 378h, 278h.
4. IBM, *Personal Computer XT Technical Reference*, 6361459, avril 1984 —
   https://bitsavers.org/pdf/ibm/pc/xt/6361459_PC_XT_Technical_Reference_Apr84.pdf
   - Listing du BIOS, p. 5-30 : « GET DMA STATUS / IS TIMER REQUEST THERE? (IT SHOULDN'T BE) /
     HALT SYS. (HOT TIMER 1 OUTPUT) ».
5. IBM, *Personal Computer AT Technical Reference*, 1502494, mars 1984 —
   https://bitsavers.org/pdf/ibm/pc/at/1502494_PC_AT_Technical_Reference_Mar84.pdf
   - « System Interrupts », p. 1-10/1-11 : priorités.
   - « DMA Channels », p. 1-13 : canal 4 en cascade.
   - « Programming the 16-Bit DMA Channels », p. 1-14 : ports C0h-DEh.
   - « RT/CMOS RAM », p. 1-45 à 1-48 : MC146818, VRB.
   - Listing du BIOS, module TEST1, points de contrôle 04 à 07 : désactivation des deux 8237,
     master clear en 0Dh et en DAh.
   - `ROM_SCAN` : « SET DMA MASK AND REQUEST REGISTERS ».
6. IBM, *IBM Personal System/2 and Personal Computer BIOS Interface Technical Reference*,
   15F0306, mai 1988 —
   https://bitsavers.org/pdf/ibm/pc/ps2/15F0306_PS2_and_PC_BIOS_Interface_Technical_Reference_May88.pdf
   - INT 15h AH = C2h : AL = 01h et 05h, état par défaut ; AL = 06h, BH = 00h, octet d'état
     (p. 2-96/2-97).
   - ABIOS, fonction 03h « Read Device Parameters » (p. 6-117).
7. IBM, *Personal System/2 Hardware Interface Technical Reference — Common Interfaces*, 84F9735,
   octobre 1990 —
   https://bitsavers.org/pdf/ibm/pc/ps2/84F9735_PS2_Hardware_Interface_Technical_Reference_Common_Interfaces_Oct90.pdf
   - « Keyboard/Auxiliary Device Controller », « Auxiliary Device and System Timings »,
     p. 14-15 du chapitre : inhibition par la ligne « clock ».
   - Consultés sans résultat pour les commandes de la souris : ce volume et la *Hardware
     Interface Technical Reference* de mai 1988.
8. Amstrad, *PC1512 Technical Reference Manual*, Section 1 « Hardware », § 1.3 (Main board I/O
   channels), § 1.4 (Expansion bus I/O channels : « 278 - 27F External Printer Port »), § 1.10
   (Parallel printer port) — transcription de John Elliott :
   https://www.seasip.info/AmstradXT/1512tech/section1.html
9. A. Chapweske, *The PS/2 Mouse Interface*, 2001 — source secondaire —
   https://isdaman.com/alsos/hardware/mouse/ps2interface.htm
10. A. Brouwer, *Keyboard scancodes*, « The PS/2 mouse » — source secondaire —
    https://web.stanford.edu/class/cs140/projects/pintos/specs/kbd/scancodes-12.html
11. *Three Flight Simulator Joysticks for DOS*, Nerdly Pleasures, 11 octobre 2014 — source
    secondaire — http://nerdlypleasures.blogspot.com/2014/10/three-flight-simulator-joysticks-for-dos.html
12. Linux, `drivers/input/joystick/analog.c`, `analog_decode` (le chapeau FCS) — source
    secondaire — https://github.com/torvalds/linux/blob/master/drivers/input/joystick/analog.c

Non consultés, faute d'objet dans la liste du domaine : les protocoles série de Microsoft et de
Logitech (aucun PB inscrit sur la souris série), l'IBM Game Control Adapter (PB-103 et PB-104 ne
touchent pas l'adaptateur) et les fiches des chipsets NEAT, Headland, OPTi et ALi (aucun PB
inscrit).
