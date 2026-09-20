# Défauts de PCem

Registre des défauts trouvés **dans PCem lui-même** (`pcem-dev/`), pas dans la
transcription. Un défaut de transcription se corrige ; un défaut de PCem se
**reproduit**, parce que PCem est l'oracle et qu'un oracle qu'on corrige n'est plus un
oracle (règle R8 de `TRANSCRIPTION.md`).

Chaque entrée porte un identifiant stable `PB-nn`, cite le C fautif à la ligne, et
indique où iXtal26 le reproduit. Les sites de reproduction portent tous le marqueur
`// pcem bug, reproduced:` — `grep -rn "pcem bug" --include=*.cs iXtal26/` les liste.

Ce fichier est un registre de **constats**, comme `VERIFICATION.md` : le plafond de
200 lignes de R3 vise la prose de conception, pas les faits mesurés.

Version de référence : PCem v18 tel que vendoré, ancré par empreinte dans
`VENDORED.md`. Les numéros de ligne s'y rapportent.

---

## A. Ce qui fausse un résultat observable

### PB-01 — Le drapeau auxiliaire d'ADC et SBB ignore la retenue entrante

`808x.c:778` `setadc8`, `:809` `setadc16`, `:841` `setsbc8`, `:873` `setsbc16`

La somme `c = a + b + tempc` sert bien à Z, N, P, C et V. Mais AF est calculé

```c
if (((a & 0xF) + (b & 0xF)) & 0x10)
```

**ligne identique à celle de `setadd8`** (`:758`) : `tempc` est oublié. `0x0F + 0x00`
avec retenue entrante vaut `0x10` et doit poser AF ; PCem calcule `0xF + 0x0 = 0xF` et
ne le pose pas.

*Effet* : AF faux sur ADC/SBB dès que la somme des quartets bas vaut `0xF` et qu'il y a
une retenue entrante. Observable à travers DAA, DAS, AAA et AAS.
*Trouvé par* : SingleStepTests — ~3 à 4 % de divergence sur les opcodes ADC/SBB,
**toujours sur le seul bit 0x0010**.
*Reproduit* : `Cpu/808x.cs:554, 591, 629, 667`.

### PB-02 — Les rotations 16 bits écrasent le carry sortant par le carry entrant

`808x.c:2887` `RCL w,1`, `:2903` `RCR w,1`, `:3173` `RCL w,CL`, `:3195` `RCR w,CL`

Dans la boucle, `templ` (resp. `tempw2`) retient le carry **avant** rotation. Après la
boucle :

```c
if (templ)  cpu_state.flags |= C_FLAG;
else        cpu_state.flags &= ~C_FLAG;
```

ce qui restaure le carry **entré** dans la dernière itération, écrasant celui qui en est
sorti et que la boucle avait correctement posé.

Le pendant octet `RCR b,CL` porte **exactement ces quatre lignes commentées**
(`808x.c:3059-3060`) : l'auteur les a identifiées comme fausses et retirées là, sans le
faire dans les variantes 16 bits.

*Effet* : CF faux après toute rotation avec retenue sur opérande mot.
*Reproduit* : `Cpu/808x.cs:2989` et `:3017`.

### PB-03 — `clockhardware()` perd les cycles de rafraîchissement DRAM

`808x.c:893-904`

```c
static void clockhardware() {
        int diff = cycdiff - cycles - current_diff;
        current_diff += diff;
        tsc_frac += (uint64_t)diff * xt_cpu_multi;
        tsc += (tsc_frac >> 32);
        ...
        timer_process();          /* APRÈS la prise du diff */
}
```

`timer_process()` redescend par `pit_refresh_timer_xt` → `dma_channel_read(0)` →
`refreshread()` → `FETCHCOMPLETE()`, qui fait `cycles -= (4 - (fetchcycles & 3))`. Ces
cycles sont débités **après** que `diff` a été pris, et l'instruction suivante refait
`cycdiff = cycles` (`:1239`) sur la valeur déjà amputée : ils ne figurent dans aucun
`diff` et n'arrivent jamais au TSC.

*Effet* : l'horloge de la machine invitée retarde par rapport à son propre compte de
cycles. **Dépendant de la charge** : 0,006 % à l'invite BASIC, **1,90 %** pendant le test
mémoire du POST. La durée d'amorçage n'est PAS affectée — ces cycles sont bien débités du
budget, ils manquent seulement au TSC.
*Trouvé par* : le contrôle de fréquence absolue (`--timer-check`), qui donne 18,205424 Hz
contre 18,206512 attendus, soit −59,79 ppm. Budget d'erreur refermé exactement :
−5,87 ppm de PB-11 plus −53,9 ppm d'ici.
*Attribué par mesure, pas par lecture* : sur 2 M d'instructions, celles **sans** appel à
`timer_process` perdent **0 cycle sur 16,1 M** ; **100,000 %** de la perte est sur celles
qui en ont un, à **1,051 cycle par appel**. Par opcode : `LOOP` 0,80 cycle/exécution
(son `FETCHCLEAR` met `prefetchw` à 0 et désactive la sortie anticipée de
`FETCHCOMPLETE`), `STOSB` 0,13, `MOV` 0,13, `XOR` 0,07.
*Reproduit* : `Cpu/808x.cs:270`. Détail dans `VERIFICATION.md` § M4.6.

### PB-04 — Le CGA lit le champ `drawcursor` au lieu de la locale

`vid_cga.c:165` et `:198`

`drawcursor` existe en double : une locale (`vid_cga.c:122`), assignée en `:161` et
`:194`, lue en `:171` et `:205` ; et un champ `cga_t.drawcursor` (`vid_cga.h:27`) que
**rien n'écrit jamais**. Ces deux lignes testent `!cga->drawcursor` — le champ — donc
une condition toujours vraie.

*Effet* : la suppression du clignotement s'applique aussi sur la cellule du curseur, là
où la locale l'en aurait exclue.
*Reproduit* : `Video/vid_cga.cs:249`.

### PB-05 — `pic.c:369` efface le mauvais bit de `pend`

```c
if (!(pic2.level_sensitive & (1 << c)))
        pic.pend &= ~(1 << c);          /* pic MAÎTRE, indice de l'ESCLAVE */
pic.ins |= (1 << 2);                    /* Cascade IRQ */
```

`c` est l'indice d'IRQ de l'esclave (0-7) ; la ligne l'applique au `pend` du **maître**.
La ligne sœur `:365` vise correctement `pic2.pend`, et `:370-371` visent bien la
cascade 2.

*Effet* : acquitter une IRQ de l'esclave efface une IRQ du maître sans rapport.
*Reproduit* : `Models/pic.cs:433`.

### PB-06 — `pic.c:39` écrit `pic.mask2` dans le bloc `pic2`

Entouré de `pic2.icw = 0`, `pic2.mask = 0xFF`, `pic2.pend = pic2.ins = 0`, on lit
`pic.mask2 = 0;`.

*Effet* : `pic2.mask2` n'est jamais réinitialisé par `pic_reset()`.
*Reproduit* : `Models/pic.cs:70`.

### PB-14 — `disc_set_rate` : le cas 1 retombe dans le cas 2

`disc.c:160-175`

```c
case 1:
        switch (drvden) { case 0: disc_period = 26; break; ... }
case 2: /*Double density*/
        disc_period = 32;
        break;
```

Pas de `break` après le `switch` interne : pour `rate == 1` (300 kbps, lecteur 1,2 Mo
lisant une 360 Ko), `disc_period` est posé à 26, 16 ou 4 puis **écrasé par 32**. Les cas
0, 2 et 3 sont bien terminés ; seul celui-ci ne l'est pas.

*Effet* : la cadence de `disc_poll` est celle du 250 kbps quel que soit `drvden` sous
`rate == 1`. Sans conséquence sur le 5150, dont le contrôleur ne connaît que `rate == 2`
(`fdc.c:98`).
*Reproduit* : `Disc/disc.cs`, `goto case 2` marqué.

---

## B. Comportement indéfini en C

### PB-07 — `readmemw` déréférence un `uint16_t*` au-delà de l'allocation

`mem.c:1344` `ram = malloc(mem_size * 1024);` — pas un octet de marge — puis
`808x.c:78` :

```c
return *(uint16_t *)(readlookup2[(s + a) >> 12] + s + a);
```

Un accès **mot** au sommet de l'espace adressable lit son octet haut **hors
allocation** : du tas adjacent.

*Effet* : valeur non déterministe. Mesuré sur `POP CX` avec `SS = 0xFFFF`, `SP = 0x000F` :
trois exécutions du même cas rendent `0x0E59`, `0x4959`, `0xD859`.
*Trouvé par* : le fuzzer différentiel, ronde 1691 — inatteignable jusqu'à ce que le
remplissage à motif de deux octets débloque les rondes au-delà de la 157.
*Seule exception à la règle « on reproduit »* : un comportement indéfini n'est pas un
comportement, il n'y a rien dont être le pendant fidèle. `tools/oracle/harness.c:99`
`h_pad_ram()` donne quatre octets à zéro au `ram` de l'oracle, comme
`Memory/mem.cs:706` le fait côté C#. **C'est la seule déviation assumée de l'oracle dans
tout le dépôt**, et elle est consignée au registre des omissions de `TRANSCRIPTION.md`.

### PB-08 — `device.c:300` teste la borne après l'accès

```c
while (devices[c] != NULL && c < 256)
        c++;
```

`devices[c]` est évalué **avant** `c < 256`. Quand les 256 fentes sont prises,
`devices[256]` est déréférencé hors tableau.

*Effet* : en C, lecture de la globale voisine ; en C#, exception. Inatteignable au
palier (a), qui enregistre moins de dix devices.
*Reproduit* : `PluginApi/device.cs:268`.

### PB-09 — `charbuffer` du CGA est débordable par le programme invité

`vid_cga.h:31` déclare `uint8_t charbuffer[256]`, mais `vid_cga.c:405` boucle jusqu'à
`cga->crtc[1] << 1`, et `crtcmask[1] = 0xff` laisse `crtc[1]` monter à 255 — donc la
borne à 510. Même borne à la relecture 80 colonnes (`vid_cga.c:157-158`).

*Effet* : un programme invité qui écrit plus de 128 dans le registre 1 du CRTC écrase en
silence les champs voisins de `cga_t`. Le BIOS du 5150 pose 40 ou 80.
*Reproduit* : `Video/vid_cga.cs:538`.

### PB-10 — Double `fclose` dans `loadbios`

`mem_bios.c:549` ferme `f` dans la branche `ROM_IBMPC` sans le remettre à `NULL`. Si
`mem_load_basic("ibmpc")` échoue ensuite, le `break` mène au `if (f) fclose(f);` de
`:1280-1281`, qui referme un `FILE*` déjà fermé.

*Effet* : double libération, uniquement sur le chemin d'échec du chargement de la ROM
BASIC. `Stream.Close()` étant idempotent en .NET, sans conséquence dans iXtal26.
*Reproduit* : `Memory/mem_bios.cs:115`.

### PB-16 — `img_load` éjecte sans remettre `f` à NULL

`disc_img.c:248-249` et `:276-277`

Sur une image de plus de 25 000 octets par piste, ou XDF de géométrie inconnue,
`img_load` fait `fclose(img[drive].f); return;` sans annuler le pointeur. `disc_load`
marque pourtant le lecteur plein (`drive_empty = 0`, `disc.c:83`) et appelle
`fdd_disc_changed` → `img_seek`, dont le seul garde est `if (!img[drive].f)` : `fseek`
et `fread` sur un `FILE *` fermé, puis un second `fclose` à la prochaine `disc_close`.

*Effet* : comportement indéfini sur toute image rejetée par ces deux branches.
*Reproduit* : `Disc/disc_img.cs`, les deux sites marqués — `Stream.Close()` est
idempotent, `Seek` sur un flux fermé lève.

### PB-17 — `track_data` fait 20 Ko, une piste XDF ED en demande 23 552

`disc_img.c:9` contre `:347-352`

`track_data[2][20 * 1024]`, mais `img_seek` lit `sectors * sector_size` octets par
face : pour les XDF à densité étendue (`bpb_sectors` 46 ou 48, `:269-273`) c'est
23 552 ou 24 576 octets, soit un débordement de 3 à 4 Ko dans la face suivante puis
dans `img[1]`.

*Effet* : corruption mémoire à la première lecture de piste d'une image XDF ED.
*Reproduit* : non reproductible — `fread` sur un `Span` borné lève en C#. Marqué
`Disc/disc_img.cs`, `img_seek`.

### PB-18 — `disc_load` copie `discfns[drive]` sur lui-même

`disc.c:85`, appelé depuis `pc.c:367-368` avec `fn == discfns[drive]`

`strcpy(discfns[drive], fn)` : source et destination sont le même tableau. `strcpy`
est déclaré `restrict` et le chevauchement est indéfini par la norme ; glibc s'en
accommode pour un pointeur identique.

*Effet* : aucun observé.
*Reproduit* : `Disc/disc.cs`, `discfns[drive] = fn` — une affectation de référence.

---

## C. Incohérences sans conséquence observable

### PB-11 — `readmemw` compare un offset 16 bits à une adresse linéaire 20 bits

`808x.c:74` :

```c
static uint16_t readmemw(uint32_t s, uint16_t a) {
        if (a != (cs + cpu_state.pc))
                memcycs += (8 >> is8086);
```

`a` est l'offset dans le segment ; `cs + cpu_state.pc` est une adresse linéaire, `cs`
étant déjà la base décalée. `readmemb` (`:57`) compare correctement deux adresses
linéaires.

*Effet* : la garde est vraie en pratique toujours, donc `memcycs` est facturé même sur une
lecture mot auto-référentielle, là où `readmemb` l'aurait sautée. Différence de quelques
cycles, uniforme, invisible sans comparaison à du silicium.
*Reproduit* : `Cpu/808x.cs:105`.

### PB-12 — `REP MOVSB` oublie `memcycs = 0` en tête de boucle

`808x.c:970`. Les branches `0xA5` (REP MOVSW), `0xA6`, `0xA7`, `0xAA`, `0xAB` remettent
`memcycs` à zéro à chaque itération ; `0xA4` non.

*Effet* : le `FETCHADD` de l'itération consomme un `memcycs` jamais réinitialisé.
*Reproduit* : `Cpu/808x.cs:758`.

### PB-13 — Branche morte dans l'EOI spécifique du PIC

`pic.c:121` : `if (val == 2 && ...)` dans un bloc gardé par `(val & 0xE0) == 0x60`, donc
`val >= 0x60`. La condition ne peut jamais être vraie. Les sites frères testent `c == 2`.

*Effet* : la remise en attente de la cascade sur EOI spécifique d'IRQ 2 ne se déclenche
jamais.
*Reproduit* : `Models/pic.cs:163`.

### PB-15 — `fdd_getrpm` : un `switch` après des retours inconditionnels

`fdd.c:143-148` : `if/else` dont toutes les branches retournent, suivi d'un
`switch (fdd[drive].type)` inatteignable — vestige d'une version où le type décidait
seul de la vitesse.

*Effet* : aucun.
*Reproduit* : `Floppy/fdd.cs`, sous `#pragma warning disable CS0162` — C# fait du code
mort une erreur.

### PB-19 — `fdc.written` n'est jamais posé à 1 : la détection d'écrasement en écriture est morte

`fdc.c:46` le déclare, `:554` et `:1219` le remettent à zéro, `:1186` le teste — et aucune
ligne de `pcem-dev/` ne lui donne jamais la valeur 1. Le champ frère `fdc.data_ready`, lui,
est bien posé (`:1064`, `:1072`) et arme la détection symétrique en lecture.

*Effet* : en écriture hors DMA, `fdc_getdata` ne peut pas détecter que l'UC n'a pas fourni
l'octet à temps ; `fdc_overrun` n'est jamais appelé par ce chemin. Le chemin d'écriture
n'est pas exercé par l'amorçage de PC DOS 2.00 (VERIFICATION.md § M6), donc rien ne l'a
mis en évidence à l'exécution.
*Reproduit* : `Floppy/fdc.cs`, test conservé tel quel.

### PB-20 — Sept champs et globales morts dans la couche disquette

Aucun n'est lu nulle part dans l'arbre vendoré :

| Symbole | Défini | Remarque |
|---|---|---|
| `fdc.abort` | `fdc.c:97` | écrit une fois dans `fdc_reset` |
| `discmodified[2]` | `fdc.c:87` | jamais écrit non plus |
| `discrate[2]` | `fdc.c:88` | idem |
| `motorspin` | `disc.c:30` | — |
| `fdc_ready` | `disc.c:25` | son `extern` est **commenté** (`disc.h:48`) |
| `fdc_indexcount` | `disc.c:33` | `extern` commenté aussi (`disc.h:49`), initialisé à 52 |
| `defaultwriteprot` | `disc.c:23` | — |
| `oldtrack[2]` | `disc.c:205` | seul usage : le bloc `ddnoise_seek` commenté, `:210-213` |

*Effet* : aucun. Deux `extern` commentés et un usage commenté disent que ces symboles ont
eu des lecteurs, retirés sans que les définitions suivent.
*Reproduit* : `Floppy/fdc.cs` et `Disc/disc.cs`, définitions conservées — le code mort
*commenté* de PCem n'est pas reproduit (R1), mais une variable morte n'est pas un
commentaire.

---

## Portée de ce registre

Ces dix-huit défauts sont ce que les trois oracles ont éclairé, **pas le résultat d'un
audit systématique de PCem** :

| Trouvé par | Entrées |
|---|---|
| SingleStepTests | PB-01 |
| Fuzzer différentiel | PB-07 |
| Mesure ciblée (fréquence absolue, imputation par opcode) | PB-03 |
| Relecture ligne à ligne pendant la transcription | PB-02, PB-04, PB-05, PB-06, PB-08, PB-09, PB-10, PB-11, PB-12, PB-13, PB-14 à PB-20 |

Le palier (a) et M6 ne transcrivent que ~7 200 des 309 000 lignes de PCem. Tout ce qui n'a pas été
lu n'a pas été examiné, et les cœurs 286/386/486, le dynarec, les autres cartes vidéo et
tout le son sont hors de ce registre.

Aucun de ces défauts n'a été remonté en amont ; `VENDORED.md` donne l'URL du projet.
