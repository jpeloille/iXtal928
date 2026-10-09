# Défauts de PCem

Registre des défauts trouvés **dans PCem lui-même** (`pcem-dev/`), pas dans la
transcription. Un défaut de transcription se corrige ; un défaut de PCem se
**reproduit**, parce que PCem est l'oracle et qu'un oracle qu'on corrige n'est plus un
oracle (règle R8 de `TRANSCRIPTION.md`).

Chaque entrée porte un identifiant stable `PB-nn`, cite le C fautif à la ligne, et
indique où iXtal26 le reproduit. Les sites de reproduction portent le marqueur
`// pcem bug, reproduced:` — `grep -rn "pcem bug" --include=*.cs iXtal26/` les liste.

Depuis G13.2, le mode matériel corrige à côté (R10 de `TRANSCRIPTION.md`) : un défaut qu'il
corrige reste reproduit en mode PCem, ses sites passent à `// pcem bug, fixed in hardware
mode:`, et son entrée reçoit le champ *Corrigé en mode matériel* : où, comment, et ce qui le
prouve. La porte `recensement` tient d'accord les marqueurs, ce champ et la table des
corrections (`iXtal26/Materiel/ModeMateriel.cs`). Le décompte des deux marqueurs dit
l'avancement de G13.

Depuis G13.1, chaque marqueur porte sur sa ligne le numéro de son entrée, et un renvoi à un
autre défaut va sur la ligne suivante. La porte `recensement` le vérifie, et qu'aucun défaut
reproduit des sections A et B ne manque de site, sauf une absence (« *Reproduit* : par
absence »). Chaque entrée des sections A et B porte trois champs, juste avant son statut :
*Source*, le vrai comportement et sa source primaire, avec son niveau (documenté, déduit,
inconnu) ; *Cas qui discrimine*, l'état d'entrée et les deux valeurs, celle de PCem et celle
du matériel ; *G13*, le classement de `PLAN-G13.md` — (a) corrigeable et vérifiable,
(b) corrigeable, vérification faible, (c) vrai comportement inconnu, laissé reproduit,
(d) à ne pas corriger —, ou « hors du mode » pour une entrée déjà corrigée dans les deux
modes. Les sources y sont citées sous un nom court (« 387 PRM », « guide de Creative ») ;
leurs références complètes sont dans les rapports de `iXtal26/Docs/G13-reconnaissance/`,
à leur section des sources.

Ce fichier est un registre de **constats**, comme `VERIFICATION.md` : le plafond de R3
vise la prose de conception, pas les faits mesurés.

Version de référence : PCem v18 tel que vendoré, ancré par empreinte dans
`VENDORED.md`. Les numéros de ligne s'y rapportent.

---

## A. Ce qui fausse un résultat observable

### PB-01 — Le drapeau auxiliaire d'ADC et SBB ignore la retenue entrante

`808x.c:786` `setadc8`, `:817` `setadc16`, `:849` `setsbc8`, `:882` `setsbc16` (les tests d'AF ;
les fonctions commencent en `:778`, `:809`, `:841` et `:873`)

La somme `c = a + b + tempc` sert bien à Z, N, P, C et V. Mais AF est calculé

```c
if (((a & 0xF) + (b & 0xF)) & 0x10)
```

**ligne identique à celle de `setadd8`** (`:766`) : `tempc` est oublié. `0x0F + 0x00`
avec retenue entrante vaut `0x10` et doit poser AF ; PCem calcule `0xF + 0x0 = 0xF` et
ne le pose pas.

*Effet* : AF faux sur ADC/SBB dès que la somme des quartets bas vaut `0xF` et qu'il y a
une retenue entrante. Observable à travers DAA, DAS, AAA et AAS : `09h + 06h` avec CF = 1
donne AF = 0, et le DAA qui suit rend 10h au lieu de 16h. Le 8086 (M24, PC1512) passe par le
même `808x.c`. Le cœur 286/386/486 a un défaut voisin, d'une autre formule (PB-181).
*Trouvé par* : SingleStepTests — ~3 à 4 % de divergence sur les opcodes ADC/SBB,
**toujours sur le seul bit 0x0010**.
*Source* : AF est la retenue (l'emprunt) du bit 3, retenue entrante comprise : documenté (SDM vol. 1,
§ 3.4.3.1) et mesuré (SST 8088 v2, AMD D8088). `(a ^ b ^ résultat) & 0x10` le rend pour ADC comme
pour SBB : 0 écart sur les 131 072 triplets de 8 bits, là où PCem se trompe sur 4 096.
*Cas qui discrimine* : SST 8088, formes `10`–`15` et `18`–`1D`, au manifeste (9 515 à 9 719 sur
10 000 en mode PCem, 10 000 attendus). Banc : CF = 1, AL = 09h, `ADC AL,06h` → AF = 0 (PCem), 1
(8088), puis `DAA` → 10h contre 16h.
*G13* : (a) — le pilote de G13.2 ; chemin tiède, un appel par ADC ou SBB et non par instruction.
*Reproduit* en mode PCem : `Cpu/808x.cs`, `setadc8`, `setadc16`, `setsbc8` et `setsbc16`, marqueurs
`fixed in hardware mode: PB-01`.
*Corrigé en mode matériel* (G13.2, le pilote) : chaque site garde la ligne de PCem et appelle, sous
`if (materiel.pb_01)`, `af_materiel` (`Cpu/808x.Materiel.cs`) : le bit 4 de `a ^ b ^ résultat`. `materiel-cas PB-01`
rend AF = 0 et DAA 10h en mode PCem, AF = 1 et DAA 16h en mode matériel, la correction comptée quatre fois par la
sonde ; la correction coupée (`--attendu materiel` en mode PCem), il rougit. SST en mode matériel : les douze formes
passent entières au 8088 (10 000 sur 10 000 chacune, contre 95,2 à 97,2 % en mode PCem), et au 8086 toutes sauf un
cas de la forme `18`, qui est PB-87 ; aucune autre forme ne bouge (`sst-baseline-materiel.tsv`,
`sst8086-baseline-materiel.tsv`). Le contrôle de fuite contre l'oracle : sur 100 000 instructions des 256 opcodes,
339 divergences, toutes dans son périmètre.

### PB-02 — Les rotations 16 bits par CL écrasent le carry sortant par le carry entrant

`808x.c:3184-3187` `RCL w,CL` (le `case` commence en `:3173`), `:3207-3210` `RCR w,CL` (`:3195`)

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

**Corrigé en G13.1** : l'entrée citait aussi `RCL w,1` (`:2887`) et `RCR w,1` (`:2903`), qui
sont sains ; seules les formes par CL ont le défaut (D1-uc § 3, D1-contre C13).

*Effet* : CF faux après RCL ou RCR mot par CL, environ une fois sur deux pour un compte non
nul ; l'OF de RCL, calculé ensuite depuis ce CF, est faux aussi. Le cœur 386 n'a pas ce défaut.
*Source* : CF reçoit le dernier bit sorti : documenté (386 PRM, page RCL/RCR/ROL/ROR, « RCL shifts
the carry flag into the bottom bit and shifts the top bit into the carry flag », à chaque pas) ;
SST 8088 v2 ne masque aucun drapeau sur `D3.2` et `D3.3` (`metadata.json`).
*Cas qui discrimine* : SST 8088 `D3.2`, `D3.3`, hors manifeste (prédit ~51 % en mode PCem) ; témoins
inchangés `D2.2`, `D2.3`, `D1.2`, `D1.3`. Banc : AX = 8000h, CF = 0, CL = 1, `RCL AX,CL` → CF = 0
(PCem), 1 (8088) ; AX = 0001h, CF = 0, CL = 1, `RCR AX,CL` → CF = 0 (PCem), 1 (8088).
*G13* : (a) — sauter les deux blocs de quatre lignes sous la garde ; vérifiable par SST.
*Reproduit* en mode PCem : `Cpu/808x.cs`, RCL w,CL et RCR w,CL, marqueurs `fixed in hardware mode: PB-02`.
*Corrigé en mode matériel* (G13.3) : sous `if (materiel.pb_02)`, le bloc de quatre lignes qui écrasait le CF est sauté (`rcl_rcr_materiel`, `Cpu/808x.Materiel.cs`, la sonde seule) ; le CF du dernier bit sorti, posé par la boucle, reste. `materiel-cas PB-02` rend la valeur de PCem en mode PCem, celle du matériel en mode matériel, la sonde comptant la correction ; la correction coupée (`--attendu materiel` en mode PCem), il rougit. SST en mode matériel : au 8088, `D3.2` de 2 627 à 5 000 cas sur 5 000, `D3.3` de 2 555 à 5 000 ; au 8086, de 1 118 et 1 112 à 2 000 sur 2 000.

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
budget, ils manquent seulement au TSC. **Le 5150 et l'XT seulement** : seul `xt_init` branche le rafraîchissement
sur la DMA (`model.c:205`) ; sur la M24 et le PC1512, le défaut ne mord que pendant une DMA de périphérique (la
disquette), un effet faible et non mesuré (contre-lecture de G13).
*Trouvé par* : le contrôle de fréquence absolue (`--timer-check`), qui donne 18,205424 Hz
contre 18,206512 attendus, soit −59,79 ppm. Budget d'erreur refermé exactement :
−5,87 ppm de la troncature `cpu_get_speed() / 100` (`pc.c:473` : 47 727 cycles par tranche au lieu de 47 727,28 ;
`TimerCheck.cs:76-81`, `VERIFICATION.md` § M4.6) plus −53,9 ppm d'ici. Cette troncature est un rythme de l'hôte, que
l'invité ne voit pas ; l'imputation à PB-11, depuis la création du registre, était fausse (contre-lecture de G13).
*Attribué par mesure, pas par lecture* : sur 2 M d'instructions, celles **sans** appel à
`timer_process` perdent **0 cycle sur 16,1 M** ; **100,000 %** de la perte est sur celles
qui en ont un, à **1,051 cycle par appel**. Par opcode : `LOOP` 0,80 cycle/exécution
(son `FETCHCLEAR` met `prefetchw` à 0 et désactive la sortie anticipée de
`FETCHCOMPLETE`), `STOSB` 0,13, `MOV` 0,13, `XOR` 0,07.
*Source* : IBM PC TR 6025008 p. 2-3, l'UC à 4,77 MHz, le tiers d'un quartz de 14,31818 MHz ; p. 2-22, le 8253-5
cadencé à 1,19 MHz (le douzième du même quartz). Documenté ; qu'un cycle volé par le rafraîchissement s'écoule aussi
pour le PIT, donc pour le TSC, en est déduit.
*Cas qui discrimine* : `--timer-check` sur le 5150 : PCem, des cycles « consommés mais JAMAIS portés au tsc »
(1,051 par appel de `timer_process`) et, pendant le test mémoire, un Δtsc de 140 457,5 par tranche ; le matériel,
aucun, et un Δtsc de trois fois les cycles consommés (143 181,8 attendus).
*G13* : (a) — le 5150 et l'XT seulement ; avec PB-257, dont les cycles fantômes iraient sinon au TSC.
*Reproduit* en mode PCem : `Cpu/808x.cs`, `clockhardware`, marqueur `fixed in hardware mode: PB-03`. Détail dans `VERIFICATION.md` § M4.6.
*Corrigé en mode matériel* (G13.3, avec PB-257) : sous `if (materiel.pb_03)`, `clockhardware_materiel` appelle timer_process puis porte au TSC les cycles qu'il vient de débiter, par le même compte que clockhardware ; la ligne de PCem, `timer_process()`, reste un appel terminal. `--timer-check` sur le 5150, 300 s : en mode PCem 77 350 cycles consommés jamais portés au TSC (−59,87 ppm au rapport 1) ; en mode matériel aucun, le rapport 1 à −5,88 ppm, la seule troncature de `pc.c:473`, et l'encadrement du rapport 3 contient la fréquence nominale moins cette troncature. `materiel-cas PB-03` rend la valeur de PCem en mode PCem, celle du matériel en mode matériel, la sonde comptant la correction ; la correction coupée (`--attendu materiel` en mode PCem), il rougit. Le cas : cinq secondes à l'invite de BASIC, 1 288 cycles perdus contre 0.

### PB-04 — Le CGA lit le champ `drawcursor` au lieu de la locale

`vid_cga.c:165` et `:198`

`drawcursor` existe en double : une locale (`vid_cga.c:122`), assignée en `:161` et
`:194`, lue en `:171` et `:205` ; et un champ `cga_t.drawcursor` (`vid_cga.h:27`) que
**rien n'écrit jamais**. Ces deux lignes testent `!cga->drawcursor` — le champ — donc
une condition toujours vraie.

*Effet* : la suppression du clignotement s'applique aussi sur la cellule du curseur, là
où la locale l'en aurait exclue. Sur les lignes du curseur, un caractère clignotant (attribut bit 7, 3D8h bit 5)
perd son avant-plan pendant la phase éteinte, et le curseur, une inversion (PB-222), n'y montre qu'un pavé uni.
*Source* : IBM, *Options and Adapters TR*, vol. 2 (1984), CGA, schéma feuille 1 (p. 28) : +ALPHA DOTS = NOR(-CURSOR
BLINK, -CURSOR DLY) OR (+CHG DOTS AND (-BLINK OR NAND(-CURSOR DLY, +ENABLE BLINK, +AT7))) — documenté : sur ses
lignes, la cellule du curseur n'est jamais éteinte par le clignotement, quelle que soit la phase du curseur.
*Cas qui discrimine* : CGA, mode 3, 3D8h = 29h, caractère 41h d'attribut 87h sous le curseur (R10-R11 = 06h-07h), trame
où `cgablink & 8` ≠ 0 (le curseur de PCem allumé) : lignes 6-7 de la cellule, PCem peint un pavé uni cgapal[15] ;
l'exemption `(ma == ca) && con` y laisse le glyphe, inversé (cgapal[8] sur cgapal[15]).
*G13* : (a) — exemption `(ma == ca) && con`, sans `cursoron`, la locale ne suffisant plus avec PB-223.
*Reproduit* : `Video/vid_cga.cs`, `cga_poll`, marqueurs PB-04 (`:272` en 80 colonnes, `:324` en 40).

### PB-05 — `pic.c:369` efface le mauvais bit de `pend`

```c
if (!(pic2.level_sensitive & (1 << c)))
        pic.pend &= ~(1 << c);          /* pic MAÎTRE, indice de l'ESCLAVE */
pic.ins |= (1 << 2);                    /* Cascade IRQ */
```

`c` est l'indice d'IRQ de l'esclave (0-7) ; la ligne l'applique au `pend` du **maître**.
La ligne sœur `:364` vise correctement `pic2.pend`, et `:370-371` visent bien la
cascade 2.

*Effet* : acquitter une IRQ de l'esclave efface une IRQ du maître sans rapport : l'IRQ 8 + c efface l'IRQ c qui
attend (l'IRQ 0 contre l'IRQ 8 de la RTC, l'IRQ 1 contre l'IRQ 9, l'IRQ 5 contre l'IRQ 13…), sauf pour c = 2 :
l'IRQ 10 vise la cascade, que `pic_updatepending` recalcule (déduit, non mesuré). Avec PB-246, un top
d'horloge se perd quand l'IRQ 0 et l'IRQ 8 attendent au même acquittement.
*Source* : 8259A (231468-003) p. 7, « Interrupt Sequence » : à l'INTA, « the highest priority ISR bit is set and the
corresponding IRR bit is reset » ; rien d'autre ne bouge dans l'IRR du maître. Documenté.
*Cas qui discrimine* : PICBANC (à écrire), ibmat, CLI : IRQ 0 et IRQ 8 (la RTC en périodique) en attente, IRR maître
05h et esclave 01h (OCW3 0Ah) ; STI. PCem sert 70h et perd l'IRQ 0 (l'IRR maître relu 00h) ; PB-05 seul corrigé,
70h puis 08h ; avec PB-246, 08h puis 70h.
*G13* : (a) — dans un 8259A selon la fiche, avec PB-246 et PB-247 ; PB-06 et PB-13 y sont absorbés.
*Reproduit* : `Models/pic.cs`, `picinterrupt`, marqueur `fixed in hardware mode: PB-05`.
*Corrigé en mode matériel* (G13.4) : sous `if (materiel.pb_05)`, `picinterrupt` passe la main à `picinterrupt_materiel` (`Models/pic.Materiel.cs`), un acquittement selon la fiche où chaque correction du 8259 ne vaut que si on la demande ; avec PB-05, servir l'esclave ne touche pas à l'IRR du maître, dont la cascade est recalculée par `pic_updatepending`. `materiel-cas PB-05` (AT, l'IRQ 0 masquée mais en attente, l'IRQ 8 : 70h, puis l'IRR du maître) rend 00h en mode PCem, 01h en mode matériel, la sonde comptant la correction ; la correction coupée (`--attendu materiel` en mode PCem), il rougit. PICBANC sur l'IBM AT, PB-05 seul : l'ordre de service « R T » au lieu de « R » ; avec PB-246, « T R ».

### PB-06 — `pic.c:39` écrit `pic.mask2` dans le bloc `pic2`

Entouré de `pic2.icw = 0`, `pic2.mask = 0xFF`, `pic2.pend = pic2.ins = 0`, on lit
`pic.mask2 = 0;`.

*Effet* : `pic2.mask2` n'est jamais réinitialisé par `pic_reset()`. Aucun observable (contre-lecture de G13) : l'ICW1
que le POST envoie à l'esclave remet `mask2` à zéro (`pic.c:214-221`) avant tout démasquage, et la remise à zéro du
seul processeur (le 8042, `softresetx86`) ne passe pas par `pic_reset`.
*Source* : 8259A p. 2, table 1 : aucune broche de remise à zéro ; seul ICW1 initialise (p. 9-10), et l'état à la
mise sous tension n'est pas défini. Documenté.
*Cas qui discrimine* : aucun — seule une sonde du PIC (`pic2.mask2` après un `pic_reset` pendant une IRQ de
l'esclave) le verrait.
*G13* : (d) — sans effet observable, et `pic_reset` modélise une mise sous tension, où toute valeur se vaut ; en C.
*Reproduit* : `Models/pic.cs`, `pic_reset`, marqueur PB-06 (`:72`).

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

*Effet* : la cadence de `disc_poll` est celle du 250 kbps quel que soit `drvden` sous `rate == 1`. Sans conséquence
sur le 5150, dont le contrôleur ne connaît que `rate == 2` (`fdc.c:98`), ni sur l'XT, la M24 et le PC1512. Sur un
AT, en revanche, le B: 1,2 Mo de tous les profils lit une 360 Ko à 300 kbit/s : chaque octet y dure 32 µs au lieu de
26,7, et chaque tour apparent de la piste 20 % de trop ; les données restent justes (reconnaissance de G13).
*Source* : documenté — IBM PC AT Technical Reference (1502494, mars 1984), « Fixed Disk and Diskette Drive
Adapter », § *Data Rates* (« three data rates: 250,000, 300,000 and 500,000 bits per second »), et le listing du
BIOS (« 01: 300 KBS ») : 300 kbit/s, soit 26,67 µs par octet.
*Cas qui discrimine* : ami286, image 360 Ko en B: (lecteur 1,2 Mo), CCR (3F7h) = 01h, READ DATA d'un secteur :
l'écart entre deux appels de `disc_poll` vaut 32 µs chez PCem, 26,67 µs (80/3) sur le matériel — PCem écrit 26 pour
ce débit, l'arrondi est à trancher.
*G13* : (a).
*Reproduit* : `Disc/disc.cs`, `goto case 2` marqué.

### PB-22 — `CMD_FORMAT_TRACK` ne réinitialise pas `sector`, et formate donc à côté

`mfm_xebec.c:369-387`. Les trois autres commandes d'accès au disque commencent par
`xebec->sector = xebec->command[2] & 0x1f` — `CMD_VERIFY_SECTORS` (`:340`),
`CMD_READ_SECTORS` (`:395`), `CMD_WRITE_SECTORS` (`:486`). `CMD_FORMAT_TRACK`, non.

Or `xebec_get_sector` (`:241-269`) fait entrer `sector` dans le calcul d'adresse au même titre que le cylindre et la
tête. Le formatage démarre donc au secteur **laissé par la commande précédente** : il déborde sur la piste suivante
et laisse intact le début de la piste visée. Et si le résidu est ≥ 17, le test de borne de `:260` fait échouer la
commande en `ERR_ILLEGAL_SECTOR_ADDRESS`.

*Effet* : latent sous PC DOS 2.00, qui fait précéder chaque `FORMAT TRACK` d'un accès
laissant `sector` à 0 — le `FORMAT C: /S` de § M12 produit une image octet pour octet
identique des deux côtés. Un pilote qui enchaînerait deux formatages sans accès
intermédiaire, lui, formaterait la mauvaise piste.
*Source* : documenté — IBM Fixed Disk Adapter (Hardware Reference Library, 6361503), *Programming Considerations*,
DCB, p. 7 et p. 10-11 : FORMAT TRACK (classe 0, opcode 06h) porte l'unité et la tête (octet 1), les bits hauts du
cylindre et un secteur à zéro (octet 2), le cylindre (octet 3) : la piste désignée est formatée depuis son début.
*Cas qui discrimine* : XT à Xebec (320h-323h), disque de type 16 initialisé par le BIOS : READ SECTORS d'un secteur
en (c, h, 5) — le secteur courant passe à 6 —, puis FORMAT TRACK (c, h) : PCem formate les secteurs 6 à 16 de (c, h)
et 0 à 5 de (c, h + 1) ; la carte, les 17 secteurs de (c, h), et (c, h + 1) reste intact.
*G13* : (a).
*Reproduit* : `Mfm/mfm_xebec.cs`, l'absence d'affectation est conservée et marquée.

### PB-23 — Le bit d'unité de l'octet de fin est toujours nul

`mfm_xebec.c:293` :

```c
xebec->completion_byte = xebec->drive_sel & 0x20;
```

`drive_sel` vaut **0 ou 1** — il est posé deux lignes plus haut par
`(xebec->command[1] & 0x20) ? 1 : 0`. Le masque `0x20` sur une valeur qui ne dépasse
jamais 1 rend donc **toujours 0**. L'intention était `command[1] & 0x20`.

*Preuve interne* : `CMD_READ_STATUS` fait correctement `data[1] = drive_sel ? 0x20 : 0`
(`:322`) — le même bit, reconstruit proprement, trente lignes plus bas.
*Effet* : l'octet de fin ne signale jamais que la commande visait l'unité D. Sans
conséquence sur un XT à un seul disque, ce qui est la configuration historique.
*Source* : documenté — IBM Fixed Disk Adapter, *Status Register*, p. 3 : bit 1 « an error has occurred », bit 5
« the logical unit number of the drive ».
*Cas qui discrimine* : TEST DRIVE READY (00h) vers l'unité 1 (octet 1 = 20h), unité D présente : octet de fin 00h
chez PCem, 20h sur la carte ; unité D absente : 02h contre 22h.
*G13* : (a) — sans effet pour le BIOS d'IBM, qui ne lit que le bit 1.
*Reproduit* : `Mfm/mfm_xebec.cs`.

### PB-25 — La borne des têtes est testée avec `>` au lieu de `>=`

`mfm_xebec.c:250` et `:255`. Les têtes sont numérotées **depuis 0**, donc `head == hpc` est déjà hors du disque ;
avec `>`, elle passe le filtre et le calcul d'adresse de `:266` vise une piste entière au-delà du cylindre demandé,
**en silence**.

*Preuve interne* : le test des secteurs, dix lignes plus bas (`:260`), écrit bien `>= 17`. Les deux bornes sont dans
la même fonction, écrites dans la même minute, l'une juste et l'autre fausse.
*Effet* : une lecture ou une écriture sur la tête `hpc` atteint des données valides mais **d'ailleurs**.
`mfm_at.c:113` et `:121` portent exactement le même défaut : ancêtre commun.
*Source* : Xebec : IBM Fixed Disk Adapter, p. 6, erreur 21h « Illegal Disk Address » (« an address that is beyond
the maximum range ») — documenté pour le code, déduit pour la tête hors géométrie. AT (WD1003) : une tête absente ne
présente aucun champ ID, d'où IDNF (10h), que `mfm_at` rend pour tout refus d'adresse — déduit.
*Cas qui discrimine* : Xebec : INIT DRIVE PARAMS à n têtes, READ SECTORS à la tête n : PCem lit (c + 1, 0, s), octet
de fin 00h ; la carte, 02h et le sense 21h. AT : SET PARAMETERS à n têtes (n < 16), READ SECTORS (20h) à la tête n :
PCem lit (c + 1, 0, s) sans erreur ; le contrôleur, ERR et l'erreur 10h (IDNF).
*G13* : (a).
*Reproduit* : `Mfm/mfm_xebec.cs` et `Mfm/mfm_at.cs`, `mfm_get_sector`, quatre marqueurs PB-25 — les deux de l'AT
posés en G13.1, qui n'en avaient pas (D3-contre, C6).

### PB-28 — `CMD_DTC_GET_DRIVE_PARAMS` répond une géométrie inventée sur une unité absente

`mfm_xebec.c:651-666`. Quatre commandes touchent à une unité qui peut ne pas exister ; trois testent
`drive->hdd_file.f` et répondent `ERR_NOT_READY` (`:299`, `:305`, `:560` ; `xebec_set_switches`, `:733`, saute aussi
l'unité absente). Celle-ci, non — elle lit directement la géométrie :

```c
xebec->data[2] = drive->hdd_file.hpc - 1;
```

Sur une unité absente, `hpc` vaut 0 et la troncature en `uint8_t` donne **`0xff`** : 256
têtes annoncées.

*Effet* : propre au DTC 5150X, carte qui n'est pas celle du jalon. Le Xebec d'IBM n'a pas
cette commande.
*Source* : inconnu — commande propre au DTC 5150X, aucune documentation trouvée ; ERR_NOT_READY (04h), comme les
commandes qui testent l'unité, est plausible. À mesurer.
*Cas qui discrimine* : DTC 5150X à un seul disque, FBh vers l'unité 1 : PCem rend `00 11 FF 00` (0 cylindre, 17
secteurs, 256 têtes) et l'octet de fin 00h ; le vrai DTC : à mesurer (04h ?). Si sa ROM émet FBh au POST, le tracer
en C# seul dit si l'effet est atteint.
*G13* : (c) — aucune documentation du DTC 5150X : à mesurer sur la carte.
*Reproduit* : `Mfm/mfm_xebec.cs` — le fichier porte les deux cartes, on transcrit les deux.

### PB-39 — Un CALL ou un JMP par porte de tâche lève #GP au lieu de changer de tâche

`x86seg.c:1284-1296` (loadcscall) et `:761-779` (loadcsjmp) :

```c
case 0x100: /*286 Task gate*/
case 0x900: /*386 Task gate*/
        ...
        taskswitch286(seg, segdat, segdat[2] & 0x800);
```

Les types 1 et 9, commentés « Task gate », sont en réalité les **TSS disponibles** (286 et
386) — un CALL ou un JMP directement sur une TSS, que ces deux lignes traitent bien. La vraie
**porte de tâche** est le type 5 (`0x500`) : aucun `case` ne la nomme, et elle tombe dans le
`default` — « Bad CALL special descriptor » puis `x86gpf(NULL, seg & ~3)` (CALL, `:1293-1296`),
ou « Bad JMP CS » puis `x86gpf(NULL, 0)` (JMP, `:775-779`). `pmodeint` (`:1963`) et `pmodeiret`,
eux, suivent bien une porte de tâche ; mais la porte de `pmodeint` accepte une TSS de la LDT et
n'en vérifie pas le type (PB-191) : ce n'est pas un modèle complet de la correction.

Le JMP de tâche efface NT (`:768`) : c'est ce qu'écrit le 386 PRM (§ 7.6, Table 7-2), là où le
SDM (vol. 3A, Table 7-2) le prend dans la nouvelle TSS. Contradiction des manuels, pas un défaut
de PCem (D1-contre K1) : reproduit, sans entrée.

*Effet* : un programme qui change de tâche par `CALL FAR` ou `JMP FAR` sur une porte de
tâche reçoit un #GP. Changer de tâche par INT, par IRET avec NT, ou par CALL/JMP directement
sur la TSS fonctionne.
*Trouvé par* : pm-check --core 386, G2 D5 — le cas « CALL FAR porte de tâche » partait en
#GP des deux côtés ; relecture de loadcscall ensuite.
*Source* : documenté — « a JMP or CALL instruction can refer either to a TSS descriptor or to a task
gate » (386 PRM § 7.5) et les branches TASK-GATE des pages CALL et JMP. Leurs exceptions se
contredisent : #TS (page CALL) ou #GP (page JMP, et SDM) pour DPL, TI, limite, TSS occupée ; #NP commun.
*Cas qui discrimine* : pm-check, cœur 386, CPL 0 : `CALL FAR` vers une porte de tâche (type 5) qui
désigne une TSS 386 disponible → #GP(sélecteur de la porte) (PCem) ; commutation, lien arrière, NT = 1,
TSS occupée (386). `JMP FAR` : #GP(0) contre une commutation sans lien.
*G13* : (a) pour le chemin nominal ; (b) pour les codes d'erreur, que les manuels contredisent (K2).
*Reproduit* : `Cpu/x86seg.cs:722` (loadcscall) et `:2546` (loadcsjmp), marqueurs `PB-39` ; épinglé
par l'attente du cas « CALL FAR porte de tâche -> #GP (PCem : type 5 non géré) ».

### PB-40 — CALL FAR sur une TSS empile l'adresse de retour sur la pile de la NOUVELLE tâche

`x86_ops_call.h:51-96`, la macro `CALL_FAR_l` (et `CALL_FAR_w`, `:3-49`, même forme ; appelées
en `:110`, `:128`, `:199`, `:315`, `:432`, `:550`) :

```c
if (msw & 1)
        loadcscall(new_seg, old_pc);
...
PUSH_L(old_cs);
PUSH_L(old_pc);
```

Les deux empilements viennent APRÈS `loadcscall`, qui a déjà changé de tâche quand la cible
est une TSS : ESP, SS et CS sont ceux de la nouvelle tâche. L'adresse de retour atterrit donc
sur la pile de la tâche appelée — là où un 386 n'empile rien, le lien arrière de la TSS
tenant lieu de retour.

*Effet* : après `CALL FAR` sur une TSS, l'ESP de la nouvelle tâche vaut ESP chargé − 8 (−4 en
16 bits), et deux mots ont été écrits sous sa pile. Mesuré : ESP 0x6FF8 au lieu de 0x7000. Si
cette pile est invalide, l'abandon remet `CS = old_cs` dans l'état de la nouvelle tâche.
*Trouvé par* : pm-check --core 386, G2 D5 — l'attente écrite à la main (0x7000) ne tenait
pas ; relecture de la macro.
*Source* : documenté — un CALL vers une tâche n'empile rien : le lien arrière de la nouvelle TSS reçoit
le sélecteur de l'ancienne, NT est posé, le retour se fait par IRET (386 PRM § 7.6, Table 7-2 ;
branches TASK-GATE et TASK-STATE-SEGMENT de la page CALL).
*Cas qui discrimine* : pm-check « CALL FAR TSS 386 » : ESP de la nouvelle tâche 0x6FF8 et deux mots
écrits sous 0x7000 (PCem) ; ESP 0x7000, rien d'écrit (386).
*G13* : (a) — un drapeau « commutation faite », posé par la voie TSS et par la porte (PB-39).
*Reproduit* : `Cpu/386_ops_call.cs:56` (CALL_FAR_w) et `:121` (CALL_FAR_l), marqueurs `PB-40`,
transcrits tels quels ; épinglé par l'attente du cas « CALL FAR TSS 386 » (ESP 0x6FF8).

### PB-41 — Une TSS 16 bits pose les moitiés hautes des registres généraux à FFFF

`x86seg.c:2817-2824`, branche 16 bits de `taskswitch286` :

```c
EAX = new_eax | 0xFFFF0000;
ECX = new_ecx | 0xFFFF0000;
...
EDI = new_edi | 0xFFFF0000;
```

La branche 32 bits (`:2612-2619`) charge les registres entiers, sans masque. Pour la branche
16 bits, Intel ne garantit pas les moitiés hautes : « When the general-purpose registers are
loaded or saved from a 16-bit TSS, the upper 16 bits of the registers are modified and not
maintained » (SDM vol. 3A, § 7.6). La valeur n'est pas dite. Bochs (`cpu/tasking.cc`) pose
0xFFFF lui aussi, sans dire d'où il le tient. PCem et Bochs mettent de plus FS et GS à nul
(`x86seg.c:2835-2838`).

**Corrigé en G13.1** : l'entrée disait qu'un changement de tâche vers une TSS 286 « ne devrait
toucher que les seize bits bas des registres » ; c'est contraire à Intel (D1-contre C15).

*Effet* : sur un 386, entrer dans une tâche 286 met EAX…EDI à `0xFFFFxxxx`, une valeur
qu'Intel ne fixe pas. Sur un 286, invisible — les moitiés hautes n'y existent pas pour le
programme — mais le vecteur d'état les compare.
*Trouvé par* : relecture pendant la transcription de taskswitch286, G2 D5.
*Source* : inconnu — Intel : moitiés hautes « modified and not maintained » (SDM vol. 3A, § 7.6), sans
valeur ; Bochs : 0xFFFF, sans source. À mesurer sur un 386DX : EAX = 12345678h, JMP vers une TSS 286
dont AX vaut 1111h, puis lire EAX dans la nouvelle tâche.
*Cas qui discrimine* : pm-check « JMP FAR TSS 286 depuis un 386 » : EAX = 0xFFFF1111 (PCem) ; la
valeur du silicium n'est pas connue.
*G13* : (c) — valeur réelle inconnue, et garder les moitiés hautes contredirait Intel : reproduit.
*Reproduit* : `Cpu/x86seg.cs:2303`, taskswitch286, marqueur `PB-41` (l'en-tête de la fonction le
dit) ; épinglé par l'attente du cas « JMP FAR TSS 286 depuis un 386 » (EAX 0xFFFF1111).

### PB-43 — MOV CRx, DRx et TRx décodent le champ `mod` comme une adresse

`x86_ops_mov_ctrl.h:9`, `:43`, `:78`, `:90`, `:105`, `:157`, `:208`, `:220`, `:233`,
`:245`, `:258`, `:269` : chaque handler commence par `fetch_ea_16` ou `fetch_ea_32`, puis
lit `cpu_rm` comme un registre.

Sur un 386 le champ `mod` de ces instructions est **ignoré** : l'opérande est toujours un
registre. Ici, quand `mod ≠ 3`, fetch_ea décode une adresse effective — consomme ses octets
de déplacement (et le SIB en 32 bits) et avance `pc` d'autant.

*Effet* : `0F 20 05 …` (mod 0, rm 5) avance de quatre octets de trop en 32 bits ; les octets
suivants sont sautés. Aucun code réel n'écrit ces formes, mais un octet ModRM quelconque y
mène.
*Trouvé par* : relecture pendant la transcription, G2 D4.
*Source* : « The 2 bits in the mod field are ignored » (SDM vol. 2, MOV vers et depuis les registres
de contrôle, et de débogage) : documenté pour l'IA-32 ; le 386 PRM (page MOV — Move to/from Special
Registers) dit seulement « always 11 ». Pour le 386 et le 486, et pour les TRx : déduit.
*Cas qui discrimine* : banc dirigé, mode réel sur un 386 : `0F 20 06` puis `40 40 F4` → PCem lit un
déplacement de 16 bits (4040h) et saute les deux INC AX ; le 386 fait une instruction de 3 octets
(ESI = CR0) et AX gagne 2. Le fuzzeur `--0f 20…26` en mode matériel ne divergera que sur ces formes.
*G13* : (b) — vrai comportement déduit pour le 386 et le 486 ; aucun corpus (`0F 20`–`0F 26` absents
de SST 386).
*Reproduit* en mode PCem : `Cpu/386_ops_mov_ctrl.cs`, les douze handlers, marqueurs `fixed in hardware mode: PB-43` ;
l'en-tête le dit (« LE CHAMP mod … EST IGNORÉ PAR LE SILICIUM, PAS PAR PCem »). Le fuzzeur `--0f 20…26` le compare
des deux côtés.
*Corrigé en mode matériel* (G13.5) : sous `if (materiel.pb_43)`, chacun des douze handlers lit le ModRM comme un
registre, sans déplacement ni SIB (`modrm_registre_materiel`, `Cpu/386.Materiel.cs`) ; le décodeur de longueur de
PB-50 et PB-51 compte de même trois octets. `materiel-cas PB-43` rend le cas ci-dessus (`0F 20 06`, IP + 3 contre + 5)
et `67 0F 23 85` (IP + 4 contre + 6, la forme de PB-44), et rougit la correction coupée.

### PB-44 — Les formes a32 de MOV DRx,r et MOV TRx,r décodent en 16 bits

`x86_ops_mov_ctrl.h:220` (`opMOV_DRx_r_a32`) et `:269` (`opMOV_TRx_r_a32`) appellent
`fetch_ea_16`, alors que les huit autres formes `_a32` de l'en-tête appellent `fetch_ea_32` ;
le `PREFETCH_RUN` qui suit passe bien `ea32 = 1`.

*Effet* : conjugué à PB-43, un `67 0F 23` ou `67 0F 26` à `mod ≠ 3` décode une adresse
16 bits (pas de SIB, déplacement de 16 bits) au lieu de 32 : `pc` avance d'une autre longueur.
Invisible avec `mod = 3`, la seule forme d'usage.
*Trouvé par* : relecture pendant la transcription, G2 D4.
*Source* : celle de PB-43 — `mod` ignoré, quelle que soit la taille d'adresse : déduit.
*Cas qui discrimine* : `67 0F 23 85` et un déplacement : PCem lit 16 bits (6 octets), une forme a32
corrigée seule en lirait 32 (8 octets), le 386 aucun (4 octets). Invisible pour `mod = 3`.
*G13* : (d) — corrigé seul, il décoderait encore une adresse, d'une autre longueur, aussi fausse ; la
correction de PB-43 le rend inobservable.
*Reproduit* : `Cpu/386_ops_mov_ctrl.cs:313` (MOV DRx,r a32) et `:382` (MOV TRx,r a32), marqueurs
`PB-44`. En mode matériel, la correction de PB-43 (G13.5) le rend inobservable : le mod ignoré, la taille d'adresse ne
décode plus rien.

### PB-45 — IDIV octet étend AX par des zéros au lieu du signe

`808x.c:3614`, groupe F6 /7 :

```c
case 0x38: /*IDIV AL,b*/
        tempws = (int)AX;
```

AX est un `uint16_t` : la conversion en `int` le complète par des zéros. Un dividende négatif
(AX ≥ 0x8000) est donc divisé comme un grand positif. La forme mot (`:3744`, F7 /7) lit
`(DX << 16) | AX` et signe correctement.

*Effet* : quotient et reste faux pour tout dividende négatif sans débordement. Mesuré sur
SingleStepTests/8088, forme `F6.7` (hors ligne de base, vecteurs en `/tmp`) : 1 169 / 9 696
côté oracle ET côté C# (9 696 joués : la sonde écarte les cas qui commencent par REP,
D1-contre K9). Sur les 9 372 cas sans préfixe REP, un modèle Python du C reproduit
exactement les 1 169 : **1 143 échecs viennent du signe perdu** (dividende négatif, pas de
débordement) ; environ 7 000 viennent d'un débordement de quotient que PCem ne détecte pas —
sur le silicium, #DE — comme pour DIV (PB-169) ; 32 cas à diviseur nul échouent,
vraisemblablement sur les drapeaux que l'INT 0 empile (PB-180 ; déduit, D1-contre K6).
`F7.7` n'est pas touché par le signe.
*Trouvé par* : audit du 26/09 (D4), mesuré en G2.
*Source* : le dividende d'IDIV octet est AX, signé : documenté (386 PRM, page IDIV) ; sur le 8088 un
quotient hors capacité, 80h compris, lève l'interruption 0 (pages DIV et IDIV ; § 14.7, point 11), et
un REP devant IDIV inverse le signe du quotient (README SST 8088, mesuré).
*Cas qui discrimine* : SST 8088 `F6.7` (masque 0xF72A). Banc : AX = FFF7h (−9), BL = 02h, `IDIV BL`
→ AL = FBh, AH = 01h (PCem, qui divise 65 527) ; AL = FCh (−4), AH = FFh (−1) sur le 8088.
*G13* : (a) conditionnel — le signe seul gagne 1 143 cas sûrs ; le reste de `F6.7` attend PB-169,
PB-180, PB-177 (REP après un préfixe) et la levée du filtre REP de la sonde.
*Reproduit* en mode PCem : `Cpu/808x.cs`, `tempws = (int)AX` d'IDIV octet, marqueur `fixed in hardware mode: PB-45`.
*Corrigé en mode matériel* (G13.3, avec PB-169) : sous `if (materiel.pb_45)`, `idiv8_materiel` divise AX signé et lève INT 0 sur un quotient hors de −127..127 (PB-169). `materiel-cas PB-45` rend la valeur de PCem en mode PCem, celle du matériel en mode matériel, la sonde comptant la correction ; la correction coupée (`--attendu materiel` en mode PCem), il rougit. SST en mode matériel : avec PB-169 et PB-258, `F6.7` passe entière, de 1 187 à 10 000 cas sur 10 000 au 8088, de 337 à 2 000 sur 2 000 au 8086 (les cas qui commencent par REP compris depuis la suite de G13.3). Sous REP (avec PB-177), le quotient rendu est l'opposé du quotient, le reste inchangé : la règle du README SST, vérifiée sur tous les cas à registre de `F6.7` et `F7.7` ; le cas `materiel-cas-pb177-idiv`.

### PB-48 — Le mode d'arrondi du x87 ne s'applique qu'à FADD avec opérande mémoire

`x87_ops_arith.h:12-16`, dans la seule branche `opFADD##name` du macro `opFPU` :

```c
if ((cpu_state.npxc >> 10) & 3)
        fesetround(rounding_modes[(cpu_state.npxc >> 10) & 3]);
ST(0) += use_var;
if ((cpu_state.npxc >> 10) & 3)
        fesetround(FE_TONEAREST);
```

Les champs RC de `npxc` (bits 10-11) ne sont lus, en arithmétique, que là ; hors de l'arithmétique, seul
`x87_fround` les lit (`x87_ops.h:63`), pour FIST, FISTP et FRNDINT. Les autres branches du même macro —
FSUB, FSUBR, FMUL, FDIV, FDIVR (`:48-112`) — calculent sans `fesetround`, donc au plus près,
et de même les formes registre de FADD (`opFADD`, `opFADDr`, `opFADDP`, `:122-152`) et toutes
celles de FSUB/FMUL/FDIV (`:239-397`). Le macro engendre le défaut pour les quatre types
d'opérande (m32, m64, m16int, m32int, `:114-120`), et `8087.h:86` l'inclut aussi : le 8087 en
hérite. Le stockage FST et FSTP m32 ignore RC lui aussi : `ts.s = (float)ST(0)` (`x87_ops_loadstore.h:527`,
`:539`, `:552`, `:567`) est la conversion de l'hôte, au plus près (contre-lecture de G13, D2-contre K18).

*Effet* : sous un mode d'arrondi dirigé (vers −∞, +∞ ou zéro), FADD mémoire arrondit selon
le mode et toute autre opération au plus près. Sur le silicium, les six opérations suivent
RC. Un logiciel d'arithmétique d'intervalles, ou une bibliothèque qui règle RC pour une
conversion, obtient des bornes fausses d'un ulp.

*Mesuré* : `iXtal26.Diff x87-parity` (G4.0), le VRAI handler exécuté par l'oracle seul —
fpu = 387, cœur 386, `DC /r [disp16]`, ST0 et l'opérande m64 tirés parmi les normaux de
2^−60 à 2^60, npxc = 0x033F | RC << 10 — confronté à l'arrondi dirigé exact (TwoSum/FMA).
`x87-parity 2000000`, 31 250 cas par combinaison opération × mode (dirigé exact / au plus
près) :

| | au plus près | vers −∞ | vers +∞ | vers zéro |
|---|---|---|---|---|
| FADD m64 (`DC /0`) | 31 250 / 0 | 31 250 / 0 | 31 250 / 0 | 31 250 / 0 |
| FSUB m64 (`DC /4`) | 31 250 / 0 | 16 024 / 15 226 | 16 086 / 15 164 | 15 914 / 15 336 |
| FMUL m64 (`DC /1`) | 31 250 / 0 | 15 685 / 15 565 | 15 639 / 15 611 | 15 571 / 15 679 |
| FDIV m64 (`DC /6`) | 31 250 / 0 | 15 601 / 15 649 | 15 571 / 15 679 | 15 637 / 15 613 |

Aucun cas « autre » : chaque résultat est soit l'arrondi dirigé exact, soit l'arrondi au
plus près — ils coïncident quand l'erreur tombe déjà du côté du mode, d'où ~50/50. La même
mesure sur le motif hors du handler (`h_fpu_arith`, mêmes drapeaux -O2 sans
`-frounding-math`) rend 100 % d'arrondi dirigé exact pour les quatre opérations (625 000 cas par combinaison) : GCC honore
bien `fesetround`, le défaut est dans l'endroit où PCem l'appelle.
*Trouvé par* : mesure de parité de G4.0 (`PLAN-G4.md`), confirmée à la ligne de C.
*Source* : IEEE 754-1985 § 4 (toute opération arrondie selon le mode) ; SDM vol. 1 § 8.1.5.3 (RC) et vol. 2, FST/FSTP
(un stockage en m32 ou m64 arrondi selon RC) ; 387 PRM ch. 2 ; Numerics Supplement (1980), « Rounding Control » : le
8087 et le 287 aussi — documenté.
*Cas qui discrimine* : mot de contrôle 0A7Fh (53 bits, vers +∞) : FLD1 ; FDIV m64 = 3,0 ; FSTP m64 → PCem
3FD5555555555555, silicium 3FD5555555555556 ; sous 077Fh (64 bits, vers −∞) : FLD1 ; FDIV m64 = 3,0 ; FST m32 →
PCem 3EAAAAABh, silicium 3EAAAAAAh.
*G13* : (a) — avec le noyau (point de décision n° 10) ; en double, juste pour une opération isolée rangée (K4).
*Reproduit* : `Cpu/x87_ops.cs`, `x87_fadd_dirige` — l'arrondi dirigé de PCem rendu par TwoSum (DEVIATION de G4.3),
appelé par les seuls opFADD mémoire ; les huit blocs FADD mémoire de `Cpu/x87_ops_arith.cs` (générés par
`tools/x87gen/gen43.py`) ; FST et FSTP m32 de `Cpu/x87_ops_loadstore.cs` ; leurs copies de `Cpu/x87_ops_808x.cs` ;
marqueurs PB-48. `x87-cases` : les bords de l'arrondi dirigé de FADD mémoire. (Ce champ disait « pas encore —
G4.3 transcrira… » : G4.3 l'a fait, D2-contre K11.)

### PB-50 — Aucune limite de longueur d'instruction : les préfixes s'enchaînent sans fin

`x86_ops_prefix.h:3-165` (op_seg et ses variantes, `op_66`, `op_67` et leurs formes REPE et
REPNE), `x86_ops_rep.h:741-764` (REPNE, REPE), `x86_ops_misc.h:701-712` (LOCK). Chaque préfixe
compte ses cycles puis aiguille l'octet suivant — `return x86_opcodes[…](fetchdat >> 8)` —
sans compter les octets déjà lus. Le silicium lève #GP(0) quand une instruction dépasse
15 octets sur le 386 (10 sur le 286). PCem n'a pas de compteur.

*Effet* : une suite de préfixes de n'importe quelle longueur forme UNE instruction. Mesuré
(G4.0, `fuzz --core 386 --rounds 1 --instr 3 --op 64`, RAM remplie de 64) : le premier pas
exécute environ 1,7 million de préfixes, 3 428 108 cycles, jusqu'à IP 000F0002 — la fin du Mo
rempli, puisque IP ne boucle pas non plus (PB-51) ; mêmes résultats pour 65, 66, 67, F0, F2,
F3 et `26 64` alternés. Le silicium aurait levé #GP au seizième octet.
*Trouvé par* : l'instruction de PB-49 (préfixes intercalés dans une chaîne de POP SS).
*Source* : documenté — 386 : 15 octets, exception 13 (386 PRM § 14.7, point 6) ; 286 : 10 octets,
exception 13 (errata Intel « 80286 ARPL and Overlength Instructions », 15 octobre 1984) ; 486 : 15,
#GP (déduit, SDM) ; 8088/8086 : aucune limite (§ 14.7, point 6). Le README SST 80386 (10, #6) contredit Intel.
*Cas qui discrimine* : banc, mode réel, 386 : quatorze `26` puis `90` (15 octets) passent des deux
côtés ; quinze `26` puis `90` → NOP exécuté (PCem), INT 0Dh (386). Sur le 286, au-delà de 10 octets ;
le corpus SST 80286 en a (G13.5 : 239 cas de onze octets et plus).
*G13* : (a) — le compte des préfixes dans `Dispatch` suffit au cas pathologique ; l'exactitude au
seuil (préfixes sous la limite, corps au-delà) demande un décodeur de longueur.
*Reproduit* en mode PCem : le C# ne compte pas non plus (`Cpu/386_ops_prefix.cs`, `Dispatch`, marqueur
`fixed in hardware mode: PB-50`). Mais le préfixe de PCem est un appel terminal
que GCC compile en saut (`jmp *%rax`) ; C# ne garantit pas l'appel terminal, et le C# Debug
tombait par StackOverflow sur la recette ci-dessus. Le C# aiguille donc par un trampoline
(`TailCall` / `Dispatch`, `Cpu/386_ops_prefix.cs`, `// DEVIATION:`) : même handler, même
fetchdat, même valeur rendue, sans pile — au bit près l'appel terminal de PCem.
*Corrigé en mode matériel* (G13.5), avec PB-51 (un groupe : le même décodeur) : aux quatre sites qui lisent un opcode
(la boucle d'exec386, et l'ombre de SS dans POP SS et MOV SS, deux formes chacun), sous `if (materiel.pb_51)`, un
décodeur de longueur compte les octets que l'émulateur va consommer — préfixes, opcode, ModRM, SIB, déplacement,
immédiat ; un opcode ILLEGAL s'arrête à lui — et lève #GP(0) au-delà de 15 octets, de 10 sur le 286
(`lire_instruction_materiel`, `Cpu/386.Materiel.cs`). Le chemin court ne décode rien : quinze octets avant la limite,
et moins de quatre préfixes en tête. Mesuré : SST 286, 239 instructions de onze octets et plus lèvent l'exception 13,
sans rien écrire, l'IP du premier préfixe empilé ; à dix octets, 1 313 s'exécutent. SST 386 : 71 instructions de
quinze octets, dont 54 s'exécutent (le README SST est démenti), 6 lèvent #UD (un LOCK refusé) et 11 l'exception 13
d'un décalage a32 au-delà de FFFFh (PB-189, reproduit) ; les 10 de seize et dix-sept octets portent toutes un LOCK
refusé et lèvent #UD, pas l'exception 13 : le refus du LOCK passe avant la longueur, et le décodeur s'arrête sur lui
(`refus`, avec PB-182). Sans cela, 676681.7 (LOCK CMP de seize octets) perdait un cas. `materiel-cas PB-50` rend le
cas ci-dessus sur le 386 et sur le 286 (onze octets), avec leurs témoins (quinze et dix octets), et rougit la
correction coupée. En mode matériel, SST 286 gagne 238 cas, tous ceux qui se jouent (le 239e, C6#1982, est révoqué
par le corpus) : 1 455 332 → 1 455 570, quatorze formes montent (`69`, `81.0` à `.7`, `9A`, `C7`, `EA`, `F7.0`, `F7.1`),
aucune ne descend, et aucun cas qui passait ne tombe (comparaison cas par cas, G13.5b). Au 386, la limite ne change
aucun cas : le corpus n'a aucune instruction de plus de quinze octets sans un LOCK refusé.

### PB-51 — Le fetch d'instruction ne contrôle pas la limite de CS

La boucle d'exec386 (`386.c:153`, lecture d'opcode en `:178`), `fastreadb`, `fastreadw` et
`fastreadl` (`386_common.h:100-149`), puis `getbyte`, `getword` et `getlong` (`:160-173`) lisent
`cs + cpu_state.pc` sans comparer `pc` à `cpu_state.seg_cs.limit` : `grep limit` ne rend rien dans
`386.c`, et dans `386_common.h` que les gardes des données (`CHECK_READ`, `CHECK_WRITE`,
`CHECK_WRITE_REP`, `:74-92`), qu'aucune lecture d'instruction n'appelle. En mode réel, la limite
de CS vaut 0xFFFF : sur le silicium, un fetch au-delà lève #GP (INT 0Dh) — IP ne continue pas
dans le segment suivant.

**Corrigé en G13.1** : l'entrée plaçait la boucle dans `386_dynarec.c` ; l'interpréteur que lie
l'oracle (`tools/oracle/Makefile:77`) et que transcrit `386.cs` est `386.c` (D1-contre K10).

*Effet* : IP franchit 0xFFFF et l'exécution se poursuit linéairement dans la RAM. Mesuré
(G4.0) : après une suite de préfixes commencée en 1000:0xxx, IP vaut 000F0002 — CS:IP
pointe 0x100002, au-delà du Mo. Même famille que les formes E2 de `VERIFICATION.md` § G2,
où PCem ne contrôle pas la limite des adresses effectives en mode réel. En mode protégé, la
lecture du code dit la même chose ; ce n'est pas mesuré. Sur un PC, INT 0Dh est le vecteur de
l'IRQ 5 : le gestionnaire du BIOS revient à l'offset 0, seul un IP de 16 bits étant empilé ; le
vrai PC « semble » donc replier, là où PCem exécute ce qui suit (déduit).
*Trouvé par* : la trace de PB-50.
*Source* : documenté pour le 386 : « On the 80386, the processor raises exception 13 in such a case »
(386 PRM § 14.7, point 8) ; en mode protégé, « Instruction pointer must be within code-segment limit
ELSE #GP(0) » (pages CALL, JMP). Le 286 en mode réel : déduit (OS/2 Museum, M. Necasek, 29/10/2022).
*Cas qui discrimine* : banc, mode réel, 386 : un NOP en CS:FFFFh → PCem exécute l'octet de CS + 10000h ;
le 386 lève INT 0Dh, IP empilé 0000h (OS/2 Museum). Une instruction de 3 octets en FFFEh : #GP aussi.
En mode protégé, CS de limite 0FFFh. Cas SST 286 et 386 en bord de segment : à compter.
*G13* : (a) — chemin chaud : un test par instruction (`pc` contre la limite), ou par octet lu près de
la limite pour l'instruction à cheval ; le coût est à mesurer.
*Reproduit* en mode PCem : `Cpu/386.cs` (la lecture d'opcode d'exec386), `Cpu/386_ops_stack.cs` et
`Cpu/386_ops_mov_seg.cs` (l'ombre de SS), `Cpu/386_common.cs` (getbyte, getword, getlong), marqueurs `fixed in
hardware mode: PB-51` : sans contrôle, comme PCem.
*Corrigé en mode matériel* (G13.5), avec PB-50 : le décodeur de longueur, en tête d'instruction, lève #GP(0) si un
octet de l'instruction passe la limite de CS, avant tout handler (`lire_instruction_materiel`,
`Cpu/386.Materiel.cs`) ; getbyte, getword et getlong n'ont plus rien à contrôler. Sur le 286, 386 et 486 ; pour le
286 en mode réel, la source reste l'OS/2 Museum, son corpus SST n'ayant aucune instruction à cheval sur FFFFh.
Mesuré, SST 386, 284 cas en bord de segment : dans 148, l'instruction passe FFFFh et lève l'exception 13, l'IP de
l'instruction empilé — tous échouaient, tous passent (+148, 1 610 145 → 1 610 293). Dans les 136 autres,
l'instruction finit en FFFFh et c'est le HLT qui clôt le cas, en 10000h, qui lève l'exception : la sonde ne joue
qu'un pas (`Sst386Probe.cs`, en-tête) et ne la voit pas. Un second pas, en essai hors commit, en fait passer 94 ; les
39 restants sont des décalages a32 au-delà de FFFFh (`[ebx+FFFF38CCh]`), PB-189, reproduit ; 3 sauts lointains
passaient déjà. Le banc le joue en deux pas : `materiel-cas PB-51` rend le cas ci-dessus (un NOP en FFFFh, puis #GP, IP
empilé 0000h), MOV AX,1234h à cheval en FFFEh sur le 386 et le 286, et rougit la correction coupée. Le coût, quand on la demande : chaque instruction du cœur passe par `lire_instruction_materiel`,
+17 % pour exec386 sur une boucle MOV, ADD, LOOP (113,4 µs contre 132,2 pour 100 000 cycles), +22 % avec un préfixe
par tour, qui fait lire les quatre octets de tête (138,5 contre 169,5) (`tools/perfbanc/Exec386Banc.cs`) ; en mode
PCem, la garde est pliée et ne coûte rien (M2).

### PB-52 — FBLD n'existe pas : DF /4 est FPU_ILLEGAL

`x87_ops.h:884-920` (et `:923-959` en a32) : les rangées `/4` de `fpu_df_a16` (`:889`, `:898`, `:907` ; en a32
`:928`, `:937`, `:946`) portent `ILLEGAL`, c'est-à-dire `FPU_ILLEGAL_a16` (`:297-302`) — décoder l'adresse effective,
compter `timing_rr`, et rien d'autre. `x87_ops_*.h` ne contient aucun `opFBLD` ; `x87_timings_t`
n'a même pas de champ `fbld` utilisé (il en a un, `x87_timings.h:7`, que personne ne lit).

*Effet* : FBLD (chargement d'un décimal compacté de 10 octets) ne charge rien : la pile x87
ne bouge pas, TOP ne descend pas, et le programme continue avec le registre du dessus
inchangé. Le silicium pousse la valeur décimale. FBSTP, lui, existe (`x87_ops_loadstore.h:141-200`). Un témoin
réel : 86Box v4.2 a corrigé « issues with Lotus 1-2-3 and other applications due to missing FBLD FPU instruction »
(D2-contre A8).
*Trouvé par* : transcription de G4.2, en posant les rangées mémoire de DF.
*Source* : SDM vol. 2, FBLD (dix-huit chiffres et un signe convertis sans erreur d'arrondi, poussés ; −0 gardé ;
chiffres A à F : « undefined ») ; 387 PRM ch. 4 ; Numerics Supplement (1980) — documenté ; la valeur rendue pour un
BCD invalide : inconnue, à mesurer.
*Cas qui discrimine* : FNINIT ; FLDZ ; FBLD m80 = 45 23 01 00 00 00 00 00 00 80 (−12 345) ; FSTP m64 → PCem
0000000000000000 (le zéro de FLDZ, resté au sommet), silicium C0C81C8000000000.
*G13* : (a) — un gestionnaire neuf, avec le noyau (en double, exact jusqu'à 2^53) ; chiffres invalides : (c).
*Reproduit* : `Cpu/x87_ops_tables.cs`, `Table_fpu_df_a16` et `Table_fpu_df_a32`, rangées /4 à `FPU_ILLEGAL_a16`/`_a32`
(générées par `tools/x87gen/gentab.py`), et `Cpu/x87_ops_808x_tables.cs`, `ops_808x_fpu_df_a16` (`gen46.py`) ;
marqueurs PB-52. (Ce champ citait `Cpu/x87_ops.cs`, `TableFpu`, disparue en G4.3 : D2-contre K11.) Le fuzzeur G4.2
(`--x87 mem`) tire DF /4 et le confronte à l'oracle.

### PB-54 — Seul FSTP m64 contrôle la limite du segment ; FST m64 et les autres stockages non

`x87_ops_loadstore.h:454-485` : `opFSTPd_a16` et `_a32` appellent
`CHECK_WRITE(cpu_state.ea_seg, cpu_state.eaaddr, cpu_state.eaaddr + 7)` (`:459`, `:475`) — lève #GP si les huit
octets sortent de la limite. Aucun autre stockage x87 ne le fait : ni `opFSTd` (`:429-452`),
le même stockage sans dépilement, ni FST/FSTP m32, FIST/FISTP, FSTP m80, FBSTP, ni FSAVE, FSTENV,
FSTSW m16 et FSTCW (`x87_ops_misc.h`). Les chargements n'ont pas de `CHECK_READ` non plus : `x87_ops*.h` n'en
contient aucun. Et `SEG_CHECK_WRITE` ne teste que le sélecteur nul (`386_common.h:66-72`), là où `CHECK_WRITE`
teste aussi le droit d'écriture et le segment de code (`:81-86`) : un stockage x87 dans un segment en lecture seule
ne faute pas, hors FSTP m64 (D2-contre A4-i).

*Effet* : à l'offset 0xFFF9 d'un segment de 64 Ko, FSTP m64 lève #GP, FST m64 écrit — au-delà
de la limite, sans faute. Sur le silicium, les deux fautent, mais un opérande à cheval sur la limite, comme celui-ci,
lève l'exception 13 ou l'INT 9 sur le 386, #GP ou l'abandon sur le 486, selon la source ; seul un opérande qui commence
hors de la limite lève #GP partout (D2-contre K20-e, f ; cette entrée disait « les deux lèvent #GP »).
*Trouvé par* : lecture ligne à ligne en G4.2.
*Source* : 287 PRM § 9.6.3 p. 9-10 (INT 9) ; 80386 PRM § 9.8.9 et § 14.7 ; 387 PRM annexe D, item 7 (INT 13 si
l'opérande commence hors limite, INT 9 si un mot suivant en sort) ; 486 : SDM vol. 3 (2016) § 22.18.6.12 contre
vol. 3A ch. 6, i486 PRM § 9.9.9 contre § 25.1 ; SDM vol. 2 (#GP, segment non inscriptible) — documenté, contradictoire.
*Cas qui discrimine* : mode protégé (386 + 387 ou 486), DS de limite 0FFFh : FST m64 [1000h] → PCem écrit huit octets
à base + 1000h, silicium #GP(0) sans écrire ; DS en lecture seule : FST m32 [0] → PCem écrit, silicium #GP(0).
*G13* : (b) — hors limite dès le début, et droits : (a) ; à cheval : (b) ; tous les accès mémoire : avec le noyau.
*Reproduit* : `Cpu/x87_ops_loadstore.cs`, verbatim — seuls les deux `opFSTPd` portent `CHECK_WRITE` ; marqueurs PB-54
sur ces deux lignes et après le `SEG_CHECK_WRITE` de chacun des vingt autres stockages, et (par `gen44.py`) de FSAVE,
FSTENV, FSTSW m16 et FSTCW dans `Cpu/x87_ops_misc.cs`. Aucun dans `Cpu/x87_ops_808x.cs` : `gen46.py` les écarte, le
8087 n'a pas de limite de segment (`CHECK_WRITE` vide, `Cpu/x87_8087.cs`).

### PB-55 — x87_ld80 replie l'exposant modulo 1024 et écrase les dénormaux

`x87_ops.h:86-115`, le chargement d'un réel de 80 bits en double :

```c
int64_t exp64 = (((test.begin & 0x7fff) - BIAS80));
int64_t blah = ((exp64 > 0) ? exp64 : -exp64) & 0x3ff;
int64_t exp64final = ((exp64 > 0) ? blah : -blah) + BIAS64;
…
if (test.eind.ll & 0x400)
        mant64++;
test.eind.ll = (sign << 63) | (exp64final << 52) | mant64;
```

Trois écarts : (1) un exposant hors de la plage du double n'est ni saturé (±∞) ni aplati
(0) mais REPLIÉ par `& 0x3ff` — 2^1024 devient 2^0, 2^2000 devient 2^976 ; (2) un exposant
80 bits nul (dénormal ou pseudo-dénormal) donne un exposant double nul, mantisse conservée :
un dénormal double sans rapport ; (3) l'arrondi ajoute 1 à la mantisse sur le bit 10 et la
recolle par OU : une mantisse pleine qui déborde met 1 dans le bit 0 de l'exposant par OU au
lieu de l'incrémenter. Le bit entier explicite (bit 63) est ignoré : les « unnormals » se
lisent comme des normaux.
*Effet* : FLD m80 et FRSTOR rendent des valeurs fausses hors de la plage du double.
*Trouvé par* : lecture ligne à ligne en G4.2.
*Source* : SDM vol. 2, FLD (un m80 est chargé sans conversion ; ni #IA ni #D pour un m80) ; 387 PRM annexe C
(formats non pris en charge : IE à l'usage) ; Numerics Supplement (1980) tables S-25 et S-26 (8087, 287) — documenté.
*Cas qui discrimine* : FLD m80 (significande 8000000000000000h, exposant 43FFh : 2^1024) ; FSTP m80 → PCem
3FFFh 8000000000000000h (1,0), silicium 43FFh 8000000000000000h, inchangé.
*G13* : (a) — avec le noyau, ou dès « A+ » (un stockage de 80 bits suffit, D2-contre K6) ; en double : partiel.
*Reproduit* : `Cpu/x87_ops.cs`, `x87_ld80`, verbatim, et sa seconde copie de `Cpu/x87_8087.cs` (l'unité du 808x) ;
marqueurs PB-55 ; FLD m80 (`opFLDe_*`) et FRSTOR (`x87_ld_frstor`) l'appellent. L'en-tête de `x87_ops.cs` attribuait
ce défaut à PB-52 : corrigé. Le fuzzeur G4.2 le confronte à l'oracle (DB /5) ; contrôle négatif : sans `& 0x3ff`,
divergence à l'itération 3.

### PB-56 — x87_st80 écrit un double dénormal comme un normal, et 53 bits de précision seulement

`x87_ops.h:117-150`. Pour tout `d != 0` qui n'est ni infini ni NaN, le bit entier explicite
est posé et l'exposant rebiaisé (`exp80final += BIAS80 - BIAS64`) — y compris quand
l'exposant double est nul, c'est-à-dire pour un dénormal : celui-ci s'écrit comme le normal
2^(−1023) × 1,mantisse, faux d'un facteur jusqu'à 2. Et PCem ne garde ST qu'en double
(`x86.h:93`) : les onze bits bas de la mantisse 80 bits sont toujours nuls en écriture, et
perdus en lecture (PB-55). Toute l'arithmétique x87 est à 53 bits, pas à 64.
*Effet* : FSTP m80, FSAVE, FSTENV rendent des octets que le silicium n'écrirait pas pour les
dénormaux ; les calculs en précision étendue perdent onze bits.
*Trouvé par* : lecture ligne à ligne en G4.2 ; la précision était relevée dès PLAN-G4.md.
*Source* : SDM vol. 1 § 8.1.2 (registres de 80 bits) et § 8.1.5.2 (PC : 24, 53 ou 64 bits pour FADD, FSUB(R), FMUL,
FDIV(R), FSQRT) ; SDM vol. 2, FINIT (037Fh, PC = 64 bits) ; 387 PRM ; Numerics Supplement (1980) fig. S-7 — documenté.
*Cas qui discrimine* : FLD m64 = 0000000000000001 (2^−1074) ; FSTP m80 → PCem 3C00h 8000000000000800h, silicium
3BCDh 8000000000000000h ; FNINIT ; FLD1 ; FDIV m64 = 3,0 ; FSTP m80 → PCem 3FFDh AAAAAAAAAAAAA800h, silicium
3FFDh AAAAAAAAAAAAAAABh.
*G13* : (a) — avec le noyau seulement ; en double, le codage des dénormaux, pas la précision (D2-contre K5).
*Reproduit* : `Cpu/x87_ops.cs` et `Cpu/x87_8087.cs`, `x87_st80`, verbatim, marqueurs PB-56 ; ST est un `double[]`
(`Cpu/x86.cs`, hors du domaine du x87, sans marqueur).

### PB-57 — FCOM en forme registre compare avec `==` et `<` : un NaN rend « plus grand »

`x87_ops_arith.h:154-166`, `opFCOM` (D8 D0+i) — la seule comparaison qui n'appelle pas
x87_compare :

```c
if (ST(0) == ST(fetchdat & 7))
        cpu_state.npxs |= C3;
else if (ST(0) < ST(fetchdat & 7))
        cpu_state.npxs |= C0;
```

Avec un NaN, les deux tests sont faux : C3 = C2 = C0 = 0, « ST(0) > ST(i) ». Le silicium, et
FCOMP registre, FCOMPP, FCOM mémoire chez PCem même, rendent « non ordonné » (C3 = C2 = C0 =
1). Et FCOM registre compte `x87_timings.fadd`, FCOMP et FCOMPP aussi, pas `fcom` (`:164`, `:176`, `:193`).
*Effet* : un programme qui teste C2 après FCOM ST(i) sur un NaN le croit comparable.
*Trouvé par* : transcription de G4.3.
*Source* : SDM vol. 2, FCOM/FCOMP/FCOMPP (non ordonné : C3 C2 C0 = 111 ; « C1 Set to 0 » ; #IA pour tout NaN) ; 387 PRM
annexe C ; 287 PRM table 2-6 p. 2-10 et Numerics Supplement (1980), fiche du 8087 table 3 (C3 = C0 = 1, C2 et C1
« X ») — documenté ; le C2 effectif du 8087 et du 287 : inconnu.
*Cas qui discrimine* : ST(0) = 7FF8000000000000, ST(1) = 1,0 ; FCOM ST(1) (D8 D1) ; FSTSW m16 → PCem C3 = C2 = C0 = 0,
IE = 0, silicium (387 et suivants) C3 = C2 = C0 = 1, IE = 1 ; cycles : `fadd` (28 sur le 387) contre `fcom` (24).
*G13* : (a) — G13.6, les PB à peu de gestionnaires (les comparaisons) ; C2 du 8087 et du 287 : (c).
*Reproduit* : `Cpu/x87_ops_arith.cs`, `opFCOM`, marqueur PB-57 (généré par `gen43.py`), et sa copie de
`Cpu/x87_ops_808x.cs`. `x87-cases` le confronte à l'oracle (NaN contre 1) ; contrôle négatif : x87_compare sans son
test de NaN → divergence `npxs` à l'itération 67 du fuzzeur G4.3.

### PB-58 — FCOMPP : −0 contre +0 rend « plus petit », un contournement de détection

`x87_ops_arith.h:180-195`, `opFCOMPP` (DE D9), le contournement en `:186-187` :

```c
if (*(uint64_t *)&ST(0) == ((uint64_t)1 << 63) && *(uint64_t *)&ST(1) == 0)
        cpu_state.npxs |= C0; /*Nasty hack to fix 80387 detection*/
```

ST(0) = −0 et ST(1) = +0 rendent C0, « plus petit » ; le silicium rend C3, « égal ». Le
commentaire dit pourquoi : une routine de détection distingue 287 et 387 par le signe de
l'infini projectif, et PCem, qui n'a pas le mode projectif, force le résultat attendu.
Le contournement ne se déclenche pourtant que sur les motifs exacts (−0, +0) ; la détection classique (FNINIT ; 1/0 ;
FLD ST ; FCHS ; FCOMPP) compare ±∞, et des zéros n'y apparaissent que si 1/0 n'a pas été écrit — ZE démasquée, x87_div
sort sans écrire (PB-59), par exemple avec le `npxc = 0` qu'un `x87_reset` vide laisse. Hypothèse à instruire avant de
corriger (D2-contre A6).
*Effet* : toute comparaison −0 / +0 par FCOMPP, et elle seule, rend « plus petit ».
*Trouvé par* : transcription de G4.3.
*Source* : IEEE 754-1985 § 5.7 (« comparisons shall ignore the sign of zero (so +0 = −0) ») ; SDM vol. 2, FCOM/FCOMP/
FCOMPP (±0 égaux : C3) — documenté.
*Cas qui discrimine* : FLDZ ; FLDZ ; FCHS (ST(0) = −0, ST(1) = +0) ; FCOMPP ; FSTSW m16 → PCem 0100h (C0), silicium
4000h (C3).
*G13* : (a) — G13.6, avec la part « comparaisons » de PB-70, après l'hypothèse A6 instruite.
*Reproduit* : `Cpu/x87_ops_arith.cs`, `opFCOMPP`, marqueur PB-58, et sa copie de `Cpu/x87_ops_808x.cs`. `x87-cases` :
FCOMPP (−0, +0) identique à l'oracle ; sans le contournement, `npxs : oracle 0x0100, C# 0x4000`.

### PB-59 — Seule la division par zéro lève une exception ; démasquée, l'instruction s'évapore

`x87_ops.h:17-30`, la macro x87_div, et `:32`, `x87_checkexceptions() {}` vide. ZE est la
seule exception que PCem modélise : ni IE (∞ − ∞, 0 × ∞, NaN signalants, pile vide ou
pleine), ni DE, ni OE, ni UE, ni PE — aucun de ces bits de npxs n'est jamais posé. Et quand
ZE est démasquée, x87_div fait `picint(1 << 13)` puis `return 1` : le handler sort sans
écrire la destination, sans poser le tag, sans compter ses cycles — et sans poser ES ni B
dans npxs. Sur un XT, `picint(1 << 13)` vise un second PIC qui n'existe pas (le 8087 y passe
par la NMI), G4.6. FLD m32 ou m64 d'un dénormal ne pose pas DE non plus ; démasquée, le silicium empile quand même la
valeur, un cas que l'acheminement traitera à part (D2-contre A4-h).
*Effet* : un programme qui démasque les exceptions n'en voit qu'une, et pour la division par
zéro l'instruction ne compte aucun cycle. Mesuré (contrôle négatif de G4.3, la branche
démasquée neutralisée) : `D8 F5` (FDIV), `cycles consommés : oracle 8, C# 89` — les 8 sont
ceux du décodage.
*Trouvé par* : transcription de G4.3 ; relevé en reconnaissance (PLAN-G4.md).
*Source* : SDM vol. 1 § 8.4 et § 8.5 (les six exceptions, réponses masquées et démasquées ; § 8.5.5, petitesse après
arrondi) ; 387 PRM ch. 3 et annexe C (8087, 287) ; AP-578 § 2.1 à 2.3.1, SDM vol. 3 (2016) § 22.18.6.14 (#MF) —
documenté ; les cycles d'une instruction démasquée : déduit.
*Cas qui discrimine* : FNINIT ; FLD1 ; FCHS ; FSQRT ; FSTSW m16 → PCem 3800h, silicium 3801h (IE) ; IF = 0, FNINIT ;
FLDCW 037Bh ; FLD1 ; FLDZ ; FDIVP ; FNSTSW m16 → PCem 3004h, silicium (387) B084h (ZE, ES, B) ; FLD m64 =
0000000000000001 → DE : PCem 0, silicium 1.
*G13* : (b) — drapeaux masqués : (a), avec le noyau ; ZE et son acheminement : G13.6 ; OE, UE démasquées : (b).
*Reproduit* : `Cpu/x87_ops.cs`, `x87_div`, partagé par les deux instanciations, marqueur PB-59 ; `x87_checkexceptions`,
jamais appelée, n'est pas transcrite. `x87-cases` : 1 / ±0, ZE masquée et démasquée.

### PB-60 — Le NaN propagé suit l'ordre des opérandes que GCC a choisi, handler par handler

PCem calcule en double avec SSE : `addsd` et `mulsd` rendent le PREMIER opérande NaN (rendu
silencieux) quand les deux en sont. L'addition et la multiplication étant commutatives, GCC
place les opérandes à sa guise ; le NaN qui survit dépend donc de l'allocation de registres
de chaque handler, pas du x87. Le silicium, lui, garde le NaN de plus grande mantisse.
Mesuré sur l'oracle (`x87-nan-order`, ST(0) = NaN A, l'autre opérande = NaN B) :

| handler | C | premier opérande SSE (le NaN qui survit) |
|---|---|---|
| opFADD (D8 C0+i) | `ST(0) = ST(0) + ST(i)` | ST(i) |
| opFADDr (DC C0+i) | `ST(i) = ST(i) + ST(0)` | ST(0) |
| opFADDP (DE C0+i) | `ST(i) = ST(i) + ST(0)` | ST(i) |
| opFMUL (D8 C8+i) | `ST(0) = ST(0) * ST(i)` | ST(i) |
| opFMULr, opFMULP | `ST(i) = ST(0) * ST(i)` | ST(0) |
| FADD, FMUL mémoire (au plus près ou dirigé) | `ST(0) += m`, `ST(0) *= m` | la mémoire |

*Effet* : quel NaN sort de NaN + NaN n'est ni celui du silicium ni constant d'un handler à
l'autre ; il changerait avec le compilateur de PCem.
*Trouvé par* : le fuzzeur G4.3 (`DC C5` : `ST[5] : oracle 0x7FF8…, C# 0x7FFF…`), puis le
désassemblage de l'oracle et la mesure.
*Source* : SDM vol. 1 table 4-7, « Rules for Generating QNaNs » (deux NaN : le plus grand significande ; SNaN et QNaN :
le QNaN ; NaN et nombre : le NaN) ; Numerics Supplement (1980), « NANs » (8087 et 287 : le NaN de plus grande valeur
absolue) — documenté ; deux significandes égaux de signes contraires : inconnu.
*Cas qui discrimine* : ST(0) = 7FF8000000000001, ST(5) = 7FF8000000000002 ; DC C5 (FADD ST(5), ST(0)) → PCem ST(5) =
7FF8000000000001, silicium 7FF8000000000002.
*G13* : (a) — avec le noyau ; en double, exact hors des charges de FLD m80 (K4) ; significandes égaux : (c).
*Reproduit* : `Cpu/x87_ops.cs`, `X87AddSd` / `X87MulSd`, la règle SSE avec un ordre explicite, et `x87_fadd_dirige` (la
mémoire en premier) ; vingt-deux sites de `Cpu/x87_ops_arith.cs` (générés par `gen43.py`) et leurs copies de
`Cpu/x87_ops_808x.cs` ; marqueurs PB-60 sur la ligne au-dessus de chaque site, les fins de ligne `// PB-60 : …`
restant. Contrôle négatif : l'ordre de opFADD inversé → divergence à l'itération 46 159.

### PB-61 — FNSTSW AX rend npxs sans TOP

`x87_ops_misc.h:24-32`, `opFSTSW_AX` (DF E0) : `AX = cpu_state.npxs;`. PCem garde TOP à part
(`cpu_state.TOP`) ; les bits 11-13 de npxs ne sont remis à jour que par FSAVE et FSTENV
(`:170`, `:830`). La forme mémoire, `opFSTSW_a16/_a32` (`:369-388`), compose bien
`(npxs & 0xC7FF) | ((TOP & 7) << 11)` ; la forme AX, non.
*Effet* : FNSTSW AX rend un TOP périmé — celui du dernier FSAVE ou FSTENV, ou 0. Un code qui
lit la profondeur de pile par FNSTSW AX, comme beaucoup de détections de coprocesseur, se
trompe dès qu'un push a eu lieu.
*Trouvé par* : transcription de G4.4.
*Source* : SDM vol. 2, FSTSW/FNSTSW (AX reçoit le mot d'état entier, TOP compris) ; 287 PRM (FSTSW AX, « a special 80287
instruction ») — documenté.
*Cas qui discrimine* : FNINIT ; FLD1 ; FLD1 ; FLD1 (TOP = 5) ; FNSTSW AX → PCem AX = 0000h, silicium 2800h ; FSTSW m16
rend 2800h des deux côtés.
*G13* : (a) — G13.6, le premier des PB à peu de gestionnaires (le pilote du x87) ; DF E0 sur le 8087 : PB-200.
*Reproduit* : `Cpu/x87_ops_misc.cs`, `opFSTSW_AX`, marqueur PB-61 (généré par `gen44.py`), et sa copie de
`Cpu/x87_ops_808x.cs`. `x87-cases` : FNSTSW AX et FSTSW m16, TOP 0, 3, 6, npxs 0x3800.

### PB-62 — Le pointeur d'instruction et d'opérande x87 n'est jamais mémorisé

`x87.c:22-23` définit `x87_pc_off`, `x87_op_off`, `x87_pc_seg`, `x87_op_seg` ; aucune ligne de
PCem ne les écrit (`grep` sur `src/` et `includes/`, hors codegen). FSAVE (`x87_ops_misc.h:166-353`)
et FSTENV (`:826-869`) les écrivent donc toujours à zéro, là où le silicium range l'adresse et
l'opcode du dernier ESC, et celle de son opérande. Et les dispositions sont partielles : en
16 bits réel, les mots +8 et +12 (sélecteur de code, opcode ; sélecteur de données) ne sont
pas écrits — l'ancien contenu reste ; en 32 bits réel, ni +16, ni les bits de poids fort de
+12. FSAVE met aussi npxc à 0x37F même pour un 8087, là où FNINIT met 0x3FF. FRSTOR et FLDENV (`:99-150`, `:754-778`)
ne relisent pas ces champs. La disposition se choisit par `cr0 & 1` (`:101`, `:172`, `:758`, `:832`), alors que le
287, qui ne voit pas le mode du 286, suit FSETPM (déduit), dont PCem fait un FNOP (DB E4, `x87_ops.h:589`)
(D2-contre A5).
*Effet* : un gestionnaire d'exception x87 qui lit l'adresse fautive dans l'image FSAVE lit 0 ;
une image FSAVE réécrite puis relue garde des octets de l'image précédente.
*Trouvé par* : transcription de G4.4.
*Source* : SDM vol. 1 § 8.1.8 (dernière instruction non de contrôle) et § 8.1.10 (les quatre dispositions) ; SDM vol. 3
(2016) § 22.18.6.5 et § 22.18.7.16 ; SDM vol. 2, FINIT (le 387 garde les pointeurs, le 486 les efface : D2-contre
K20-d) ; Numerics Supplement (1980) fig. S-9 — documenté ; 287 et FSETPM : déduit ; contrôle sur le 8087 : inconnu.
*Cas qui discrimine* : 386 + 387, mode réel 16 bits : en 1234:0010, FLD m64 [0200h] (DS = 2000h) ; FNSTENV m → PCem
+6 = 0000h, +10 = 0000h, +8 et +12 inchangés ; silicium +6 = 2350h, +8 = 1506h, +10 = 0200h, +12 = 2000h.
*G13* : (a) — 387, 486 ; 287 avec FSETPM ; 8087 : (c) ; touche chaque ESC : avec le noyau, après la décision n° 10.
*Reproduit* : `Cpu/x87.cs` (les quatre globales, déclarées et jamais écrites) ; `Cpu/x87_ops_misc.cs`, FSTOR, FSAVE,
FLDENV, FSTENV (générés par `gen44.py`), et leurs copies de `Cpu/x87_ops_808x.cs` ; la rangée DB E0 de
`Cpu/x87_ops_tables.cs` (FSETPM) ; marqueurs PB-62. `x87-cases` fait l'aller-retour FSAVE / FRSTOR et FSTENV / FLDENV
en 16 et 32 bits, réel et PE, et compare les 112 octets.

### PB-63 — FXAM ne connaît que trois classes

`x87_ops_misc.h:465-481`, `opFXAM` : vide (C3 | C0), zéro (C3), et tout le reste « normal »
(C2) — NaN, infinis et dénormaux compris ; C1 vaut `ST(0) < 0.0`, donc 0 pour −0 et pour un
NaN négatif. Le silicium distingue NaN (C0), infini (C2 | C0), dénormal (C3 | C2) et rend le
signe dans C1 pour toutes les classes.
*Effet* : un code qui teste « infini » ou « NaN » par FXAM ne les voit jamais.
*Trouvé par* : transcription de G4.4.
*Source* : SDM vol. 2, FXAM (C3 C2 C0 : 000 non pris en charge, 001 NaN, 010 normal, 011 infini, 100 zéro, 101 vide,
110 dénormal ; C1 = signe) ; Numerics Supplement (1980) table S-13 (seize codes du 8087 et du 287) ; 387 PRM annexe C
(jamais 1101 ni 1111) — documenté ; le code « vide » que rendent le 8087 et le 287 : inconnu.
*Cas qui discrimine* : FXAM ; FSTSW m16, en C3 C2 C1 C0 : ST(0) = +∞ → PCem 0100, silicium 0101 ; ST(0) =
FFF8000000000000 → PCem 0100, silicium 0011 ; ST(0) = −0 → PCem 1000, silicium 1010.
*G13* : (a) — G13.6, les PB à peu de gestionnaires ; codes « vide » du 8087 et du 287 : (c).
*Reproduit* : `Cpu/x87_ops_misc.cs`, `opFXAM`, marqueur PB-63, et sa copie de `Cpu/x87_ops_808x.cs`. `x87-cases` : FXAM
sur onze classes, tag VALID et EMPTY.

### PB-64 — FTST : un NaN rend « plus grand »

`x87_ops_misc.h:451-463`, `opFTST` : `==` et `<` contre 0.0, comme opFCOM (PB-57). Un NaN rend
C3 = C2 = C0 = 0 au lieu de « non ordonné ».
*Trouvé par* : transcription de G4.4.
*Source* : SDM vol. 2, FTST (non ordonné : C3 C2 C0 = 111, #IA ; « C1 Set to 0 ») ; Numerics Supplement (1980), fiche du
8087 table 3 et table S-27 (8087 et 287 : un NaN, et ∞ en projectif, « not comparable », IE) — documenté.
*Cas qui discrimine* : ST(0) = 7FF8000000000000 ; FTST ; FSTSW m16 → PCem C3 = C2 = C0 = 0, IE = 0, silicium C3 = C2 =
C0 = 1, IE = 1.
*G13* : (a) — G13.6, les PB à peu de gestionnaires (IE avec PB-59, ∞ projectif avec PB-70).
*Reproduit* : `Cpu/x87_ops_misc.cs`, `opFTST`, marqueur PB-64, et sa copie de `Cpu/x87_ops_808x.cs`. `x87-cases` : FTST
sur les classes.

### PB-65 — FPREM tronque en un pas ; FPREM1 est FPREM

`x87_ops_misc.h:634-675` : `temp64 = (int64_t)(ST(0) / ST(1)); ST(0) = ST(0) - ST(1) * temp64;`,
pour les deux. Trois écarts : le quotient passe par un double, donc faux au-delà de 2^53 et
« entier indéfini » (0x8000000000000000) au-delà de 2^63 — le reste devient alors absurde ; la
réduction partielle du silicium (au plus 2^63 par pas, C2 = 1 « incomplet ») n'existe pas, C2
n'est jamais posé ; FPREM1 (le reste IEEE, quotient arrondi au plus près) est identique à FPREM
(tronqué), temps mis à part. Et la division qui donne le quotient est arrondie au plus près avant d'être tronquée
(`:640`) : dès que ST(0)/ST(1) s'arrondit à l'entier supérieur, le reste est faux même pour un petit quotient
(D2-contre K13).
*Effet* : les réductions d'arguments (sin, cos maison) sur de grandes valeurs rendent n'importe
quoi ; une boucle `FPREM ; FNSTSW ; SAHF ; JP` s'arrête toujours au premier tour ; FPREM(1,0 ; 0,1) rend 0.
*Trouvé par* : transcription de G4.4.
*Source* : SDM vol. 2, FPREM et FPREM1 (reste exact ; quotient tronqué ou au pair ; réduction partielle et C2 = 1
au-delà de 63 d'écart d'exposants) ; SDM vol. 3 (2016) § 22.18.2.1 (C0, C1, C3 à 0 après une réduction incomplète sur
387+, intacts sur 8087/287) ; 287 PRM p. 2-8 — documenté ; le N des pas partiels du 387 et du 486 : inconnu.
*Cas qui discrimine* : ST(0) = 1,0, ST(1) = 0,1 (3FB999999999999A) ; FPREM → PCem ST(0) = 0 et C3 C1 C0 = 100 (Q = 10),
silicium 3FB9999999999996 et C3 C1 C0 = 010 (Q = 9) ; ST(0) = 2^100, ST(1) = 3,0 → C2 : PCem 0, silicium 1.
*G13* : (a) — avec le noyau (le reste exact) ; N des pas partiels : (c) ; quotient du 287 : (b).
*Reproduit* : `Cpu/x87_ops_misc.cs`, `opFPREM` et `opFPREM1`, marqueurs PB-65, et leurs copies de
`Cpu/x87_ops_808x.cs` ; la conversion par CvtI64 (cvttsd2si, G4.0). `x87-cases` : FPREM, FPREM1, FSCALE sur neuf couples
de bornes (grands quotients, diviseur nul, infinis, NaN).

### PB-66 — FLDLN2 est d'un ulp trop grand

`x87_ops_misc.h:533-541` : `x87_push_u64(0x3fe62e42fefa39f0ull)`. Le double le plus proche de
ln 2 est 0x3FE62E42FEFA39EF (0,6931471805599453, ce que rend aussi `log(2.0)`) ; PCem pousse
0x…39F0 (0,6931471805599454). Mesuré en décimal à 60 chiffres : écarts 2,3·10⁻¹⁷ et 8,8·10⁻¹⁷.
Les quatre autres constantes (FLDL2T, FLDL2E, FLDPI, FLDLG2) sont les doubles les plus
proches. Aucune ne suit RC (le 387 arrondit ses constantes selon RC). Pour le 8087 et le 287, Intel se contredit :
l'annexe C du 387 PRM (§ C.4, p. C-5) contre quatre textes (387 PRM § 4.7 p. 4-20, i486 PRM § 17.6, Pentium vol. 3
§ 23.3.4, SDM vol. 2) qui leur donnent la valeur au plus près (D2-contre K20-a).
*Trouvé par* : transcription de G4.4, vérifié en décimal.
*Source* : SDM vol. 2, FLD1…FLDZ (constantes de 64 bits arrondies selon RC ; au plus près, celles du 8087 et du 287) ;
387 PRM § 4.7 ; ln 2 calculé exactement (D2 annexe B, recalculé par D2-contre C8) — documenté pour le 387 et le 486 ;
8087 et 287 : au plus près, quatre textes contre un (déduit).
*Cas qui discrimine* : FLDLN2 ; FSTP m64 → PCem 3FE62E42FEFA39F0, silicium (au plus près, vers −∞ ou vers zéro)
3FE62E42FEFA39EF ; FLDLG2 sous RC vers −∞ (077Fh) ; FSTP m64 → PCem 3FD34413509F79FF, silicium 3FD34413509F79FE.
*G13* : (a) pour le 387 et le 486 — G13.6, les PB à peu de gestionnaires ; 8087 et 287 : (b), la valeur au plus près.
*Reproduit* : `Cpu/x87_ops_misc.cs`, `opFLDLN2`, marqueur PB-66, et sa copie de `Cpu/x87_ops_808x.cs`. `x87-cases` : les
sept constantes.

### PB-67 — FST et FSTP registre copient le tag TAG_UINT64, pas l'entier qu'il désigne

`x87_ops_misc.h:76-97`, `opFST` / `opFSTP` (DD D0+i, DD D8+i) : `ST(i) = ST(0)` et le tag copié —
TAG_UINT64 compris —, mais pas `MM[TOP].q`, l'entier exact que ce tag annonce (x87.h:30). FLD
registre (`:390-405`) et FXCH (`:407-427`) le recopient, eux.
*Effet* : après `FILD m64 ; FST ST(1)`, un FISTP m64 de ST(1) écrit le `MM[].q` qui traînait dans
ce registre physique, pas la valeur chargée.
*Trouvé par* : transcription de G4.4.
*Source* : SDM vol. 1 § 8.1.2 et § 8.2 (registres de 80 bits : tout entier de 64 bits y est exact) ; SDM vol. 2, FST
(ST(i) ← ST(0)) — documenté.
*Cas qui discrimine* : FNINIT ; FILD m64 = 5 ; FILD m64 = 2^53 + 1 ; FST ST(1) ; FINCSTP ; FISTP m64 → PCem
0000000000000005, silicium 0020000000000001.
*G13* : (a) — G13.6, les PB à peu de gestionnaires (en double, recopier MM[].q ; avec le noyau, la rustine disparaît).
*Reproduit* : `Cpu/x87_ops_misc.cs`, `opFST` et `opFSTP`, marqueurs PB-67, et leurs copies de `Cpu/x87_ops_808x.cs`.
`x87-cases` : `FST ST1 ; FINCSTP ; FISTP m64`.

### PB-68 — Les transcendantes : la libm de l'hôte, sans bornes ni C2

`x87_ops_misc.h:554-612`, `:688-701`, `:730-752` : F2XM1, FYL2X, FYL2XP1, FPTAN, FPATAN, FSIN, FCOS, FSINCOS appellent
`pow`, `log`, `tan`, `atan2`, `sin`, `cos` de la libm de l'hôte, en double. Trois écarts avec le
silicium : (1) FPTAN, FSIN, FCOS, FSINCOS effacent toujours C2 — la borne |x| < 2^63, au-delà
de laquelle le 387 laisse l'opérande et pose C2 (« réduction incomplète »), n'existe pas : la
libm réduit tout argument ; (2) aucun domaine n'est vérifié — F2XM1 hors de [−1, 1], FYL2X d'un
négatif (NaN par `log`), FPTAN, FSIN, FCOS sur 8087 et 287, qui n'ont ni FSIN, ni FCOS, ni
FSINCOS (le 8087 limite aussi FPTAN à [0, π/4]) — tout est calculé ; (3) la précision est
celle de la libm en double, pas celle du microcode sur 64 bits. Les résultats dépendent donc de
la glibc de l'hôte de PCem (parité mesurée en G4.0 : Math.* de .NET rend les mêmes bits sur
cet hôte). Et `pow(2.0, x) - 1.0`, `log(x + 1.0)` (`:559`, `:582`) perdent toute précision relative pour |x|
petit, la raison d'être de F2XM1 et de FYL2XP1 (D2-contre K14). Les temps de FSIN, FCOS et FSINCOS valent 0 sur le
8087 et le 287 (`x87_timings.c:61-62`, `:135-136`).
*Effet* : une boucle de réduction d'argument pilotée par C2 ne boucle jamais ; un programme qui
teste FSIN pour distinguer 287 et 387 le trouve partout.
*Trouvé par* : transcription de G4.5.
*Source* : 387 PRM table 2-1, ch. 2 et § 4.6.4 p. 4-17 (|x| ≥ 2^63 : C2 = 1, opérande inchangé ; FPTAN pousse 1,0) ;
SDM vol. 2 (domaines de F2XM1, FYL2X, FYL2XP1) ; 287 PRM p. 2-12 et 2-13 (FPTAN en rapport, domaines) ; 387 PRM annexe C
(ni FSIN, ni FCOS, ni FSINCOS sur 8087 et 287) — documenté ; bits exacts du 387 et du 486 : inconnus.
*Cas qui discrimine* : FLD m64 = 2^64 (43F0000000000000) ; FSIN ; FSTSW m16 → PCem C2 = 0, ST(0) = 3F982A353118793D,
silicium (387 et suivants) C2 = 1, ST(0) inchangé ; F2XM1 de 10^−20 → PCem 0, silicium ≈ 6,93·10^−21.
*G13* : (b) — domaines, C2, le 1 de FPTAN : (a) ; précision : (b), sans les noyaux de Bochs (K3) ; bits exacts : (c).
*Reproduit* : `Cpu/x87_ops_misc.cs`, marqueurs PB-68 (FPTAN, FSINCOS, FSIN, FCOS) ; les rangées D9 F8 des tables
(`Cpu/x87_ops_tables.cs`, `Cpu/x87_ops_808x_tables.cs`) et les temps nuls de `Cpu/x87_timings.cs`, marqueurs PB-68 ;
copies de `Cpu/x87_ops_808x.cs`. `x87-cases` : les huit sur dix-neuf bornes × quatre ST(1) ; contrôles négatifs :
FPTAN qui pose C2 → 76 cas divergents.

### PB-69 — Sur un PC ou un XT, l'exception du 8087 se perd : IRQ13 sans second PIC, pas de NMI

`x87_ops.h:17-30`, `x87_div` : une division par zéro non masquée appelle `picint(1 << 13)`,
seule interruption que PCem lève pour le coprocesseur — recompilée telle quelle pour le 8087
(`8087.h:86`). `pic.c:298-311`, `picint` : hors AT (et hors Xi8088), `num > 0xFF` ne tombe dans
aucune branche — la demande est jetée. Sur le silicium, le 8087 du PC et du XT signale par
**NMI** (sortie INT du 8087 vers la logique NMI, masquée par le port A0h), pas par IRQ13.
Le chemin NMI du 8088 existe et ne sert jamais (rien ne pose `nmi = 1` sur le 8088) ; la NMI y est un niveau réarmé
par IRET, quand le silicium la prend sur un front (déduit ; D2-contre A7, C15).
*Effet* : sur un 5150 ou un XT, un FDIV par zéro avec ZE démasqué ne produit rien : ni NMI, ni
IRQ ; seul `npxs` porte ZE, et le handler rend 1, valeur que le 808x ignore
(`808x.c:3304-3366`). Un gestionnaire d'exceptions flottantes (INT 2 chaîné par un runtime
Microsoft ou Borland) n'est jamais appelé.
*Trouvé par* : transcription de G4.6, lecture de `picint`.
*Source* : AP-578 § 2.1 p. 5 (la sortie INT du 8087 va à la NMI du PC) ; IBM BIOS Interface Technical Reference (1987 :
« An 8087 math coprocessor error … drives the NMI ») ; PC XT Technical Reference (port A0h) ; Numerics Supplement
(1980) fig. S-7, p. S-19 (IEM) — documenté ; le rôle du commutateur SW1 : déduit (OS/2 Museum).
*Cas qui discrimine* : XT + 8087, port A0h = 80h, INT 2 accroché : FNINIT ; FLDCW 037Bh (ZE démasquée, IEM = 0) ; FLD1 ;
FLDZ ; FDIVP → PCem : ni NMI ni IRQ (la demande est jetée), silicium : le gestionnaire d'INT 2 s'exécute ; A0h = 00h :
rien des deux côtés.
*G13* : (a) — G13.6, l'acheminement, avec ZE seule ; SW1 : déduit ; la NMI sur front (D2-contre A7).
*Reproduit* : `Models/pic.cs` (`picint`, inchangé) et `Cpu/x87_ops.cs` (`x87_div`, partagé par
les deux instanciations, marqueur PB-69) ; marqueur PB-69 à l'aiguillage des ESC, `Cpu/808x.cs` (`case 0xd8`).

### PB-70 — Le contrôle de l'infini est ignoré : le 287 compare en affine, comme un 387

Le bit 12 du mot de contrôle (IC, infini projectif ou affine) n'est lu nulle part dans
`x87_ops*.h` ni `x87.c` ; `opFINIT` (`x87_ops_misc.h:49-64`, mot de contrôle en `:52-55`) pose 0x037F sur le 287
comme sur le 387. Sur le silicium, le 8087 et le 287 démarrent en projectif (+∞ = −∞) ; `x87_compare`
(`x87_ops.h:189-218`) rend toujours +∞ > −∞.
*Effet* : le test classique de génération (FINIT ; 1/0 ; FCHS ; FCOMPP) conclut au 387. Mesuré
en G4.7 : MSD de Windows 3.1, sur l'AMI 286 avec `fpu = 287`, affiche « 80286/80387 ».
*Trouvé par* : témoin MSD de G4.7, puis lecture du C.
*Source* : Numerics Supplement (1980) p. S-18 et table S-27 p. S-72 (projectif par défaut ; ∞ = ∞ ; ∞ contre un fini,
FTST(∞), ∞ ± ∞, FSQRT(+∞) : IE) ; 287 PRM fig. 1-10 ; 387 PRM annexe C § C.3 et SDM vol. 3 (2016) § 22.18.3 (387 et
486 : affine seul) — documenté ; le 287XL suit le 387 : déduit (Juffa ; fiche 290376 non consultée).
*Cas qui discrimine* : 286 + 287 : FNINIT ; FLD1 ; FLDZ ; FDIVP (+∞) ; FLD ST(0) ; FCHS ; FCOMPP ; FSTSW m16 → PCem
C3 C2 C0 = 001 (−∞ < +∞, « 387 »), silicium 100 (+∞ = −∞ en projectif, « 287 ») ; MSD : « 80287 ».
*G13* : (a) — G13.6, la part « comparaisons » (avec PB-57, 58, 64) ; ∞ ± ∞, √+∞ : avec le noyau ; 287XL : déduit.
*Reproduit* : par la transcription, `Cpu/x87_ops.cs` (`x87_compare`) et `Cpu/x87_ops_misc.cs` (`opFINIT`, généré par
`gen44.py`, et sa copie de `Cpu/x87_ops_808x.cs`), marqueurs PB-70 ; témoin MSD.

### PB-71 — Le canal IDE secondaire lit l'état de l'IRQ 14, pas le sien

`ide.c:138-143`, `ide_irq_update` : le test `(pic2.pend | pic2.ins) & 0x40` vise l'IRQ 14 (bit 6
du PIC esclave) pour LES DEUX canaux ; le secondaire lève et baisse pourtant l'IRQ 15 (0x80).
*Effet* : sur le canal secondaire, une interruption en attente sur l'IRQ 14 (canal primaire)
fait baisser l'IRQ 15, et une IRQ 15 en service ne l'empêche pas d'être relevée.
*Trouvé par* : reconnaissance de G5, lecture du C.
*Source* : documenté — ATA-1 (X3T9.2 791D r4, 17 mars 1993) § 6.3.10, INTRQ ne dépend que de l'unité choisie du
canal ; preuve interne : `ide_irq_raise` et `ide_irq_lower` visent l'IRQ 15 pour le canal secondaire
(`ide.c:118-136`). Le PC87415 de D3-stockage, une puce PCI, est une preuve faible (D3-contre, K12).
*Cas qui discrimine* : un lecteur sur le canal secondaire (`cdrom_channel` = 2) a levé l'IRQ 15 (`irqstat` = 1, nIEN
= 0), une IRQ 14 est en attente : une écriture de 176h efface chez PCem le bit 7 de `pic2.pend` (l'IRQ 15 perdue) ;
le matériel le garde.
*G13* : (a) — à traiter avec PB-216, même fonction.
*Reproduit* : `Ide/ide.cs`, `ide_irq_update`, marqueur PB-71.

### PB-72 — Sélectionner un lecteur pendant un reset perd la tête et le mode LBA de l'écriture

`ide.c:401-420` : une écriture en 0x1F6 qui change de lecteur alors qu'un reset est en cours
termine le reset et REND LA MAIN, avant les lignes `:425-431` qui retiennent la tête, le bit LBA
et les quatre bits hauts de l'adresse LBA.
*Effet* : ces champs gardent leur valeur d'avant — zéro, remis par le reset —, quel que soit
l'octet écrit.
*Trouvé par* : reconnaissance de G5.
*Source* : inconnu — ATA-1 r4 § 7.2.13 (« The host should not access the Command Block Register when BSY=1 ») ne dit
rien de l'unité ; ATA-3 r7b se contredit, « a write to a command block register by the host shall be ignored » (le
registre d'état) contre « the results are indeterminant » (les registres de commande). À mesurer (1993-1994).
*Cas qui discrimine* : SRST posé puis retiré en 3F6h (BSY), puis 1F6h = F5h (unité 1, LBA, tête 5) avant la fin du
chronomètre : PCem choisit l'unité 1, finit le reset sur-le-champ et laisse la tête à 0 et le mode CHS ; le
matériel : inconnu (selon la première lecture d'ATA-3, l'écriture est ignorée et BSY tient jusqu'au bout).
*G13* : (c) — ATA-1 ne définit rien et ATA-3 se contredit : (b) au mieux, par décision.
*Reproduit* : `Ide/ide.cs`, `writeide`, marqueur PB-72.

### PB-73 — READ MULTIPLE et WRITE MULTIPLE sans SET MULTIPLE MODE arrêtent l'émulateur

`ide.c:461-463`, `:487-489` : `blocksize` nul (aucun SET MULTIPLE MODE reçu) → `fatal()`. Le
disque réel rend ABRT.
*Effet* : un pilote qui envoie C4h ou C5h sans avoir fixé la taille de bloc — ou un invité
malveillant — arrête PCem.
*Trouvé par* : reconnaissance de G5.
*G13* : hors du mode — déjà corrigé dans les deux modes (R9) ; rien n'y bascule.
*Non reproduit* (R9, TRANSCRIPTION.md : l'invité ne tue pas l'hôte) : `Ide/ide.cs`, `writeide`,
marqueurs `pcem bug, not reproduced: PB-73` — ABRT (ERR, erreur 04h, IRQ), comme le disque réel.
Les outils n'envoient jamais ce chemin à l'oracle ; la survie est prouvée en C# seul
(`tools/idecheck/`, variante `--survie`, VERIFICATION.md § R9).

### PB-74 — VERIFY ne vérifie qu'un secteur et laisse les registres en place

`ide.c:1000-1012` : le rappel de VERIFY (40h, 41h) rend READY au premier passage, sans
décompter `secount` ni avancer l'adresse.
*Effet* : une vérification de N secteurs est vue comme réussie après un seul, et le registre
de compte rend N au lieu de 0.
*Trouvé par* : reconnaissance de G5.
*Source* : documenté sous ATA-1 — ATA-1 r4 § 9.19 (« the Command Block Registers contain the cylinder, head, and
sector number of the last sector verified » ; en erreur, le secteur fautif et « the number of sectors not yet
verified ») et § 7.2.11 (compte nul en fin normale). ATA-3 r7b § 7.19 ne requiert aucune sortie.
*Cas qui discrimine* : VERIFY (40h) de 5 secteurs depuis C/H/S 0/0/1 : PCem rend READY, compte 5, secteur 1 ;
ATA-1 : compte 0, secteur 5. VERIFY qui franchit la fin du disque : PCem, READY sans erreur ; ATA-1 : ERR, IDNF,
l'adresse du premier secteur fautif et le compte restant.
*G13* : (a), sous ATA-1.
*Reproduit* : `Ide/ide.cs`, `callbackide`, marqueur PB-74.

### PB-75 — Une commande à l'unité 1 absente arrête l'émulateur (Fixed Disk Adapter de l'AT)

`mfm_at.c:193-194` : si l'unité sélectionnée par 0x1F6 n'a pas d'image, l'écriture du registre de
commande appelle `fatal("Command on non-present drive")`.
*Effet* : un utilitaire qui sonde le second disque (FDISK, un diagnostic) sur une machine qui n'en
a qu'un arrête PCem.
*Trouvé par* : inventaire de la règle R9.
*G13* : hors du mode — déjà corrigé dans les deux modes (R9) ; rien n'y bascule.
*Non reproduit* (R9) : `Mfm/mfm_at.cs`, marqueur `pcem bug, not reproduced: PB-75` — la commande
est refusée : ERR, erreur ABRT, IRQ 14.

### PB-76 — READ LONG et WRITE LONG (avec ECC) arrêtent l'émulateur

`mfm_at.c:224-225`, `:237-238` : les commandes 22h-23h et 32h-33h (bit ECC) appellent `fatal()`.
Le WD1003 réel transfère alors 512 octets plus 4 octets d'ECC ; ni PCem ni iXtal26 ne le modélisent.
*Effet* : un utilitaire de bas niveau qui lit les ECC arrête PCem.
*Trouvé par* : inventaire de la règle R9.
*G13* : hors du mode — déjà corrigé dans les deux modes (R9) ; rien n'y bascule.
*Non reproduit* (R9) : `Mfm/mfm_at.cs`, marqueurs `pcem bug, not reproduced: PB-76` — ERR,
erreur ABRT, IRQ 14 : le comportement sûr le plus proche.

### PB-77 — `cpu_features` n'est jamais remis à zéro : un i486 hérite de CR4 et VME d'un iDX4

`cpu.c:483-485` pose `cpu_features = CPU_FEATURE_CR4 | CPU_FEATURE_VME` pour l'iDX4 (puis retombe
dans le cas i486) ; aucune ligne de `cpu_set` ne remet `cpu_features` à zéro (`cpu_CR4_mask`, lui,
l'est, `:316`). Changer de processeur dans la même session garde donc les caractéristiques du
précédent. Au passage, `cpu_CR4_mask = CR4_VME | CR4_PVI | CR4_VME` (`:485`) nomme VME deux fois.
*Effet* : après un iDX4, un i486DX accepte MOV CR4 — que le vrai i486DX refuse —, avec un masque
nul : CR4 reste à zéro. Le harnais rejoue `cpu_set` dans le même processus : l'ordre des entrées
d'un balayage décide de ce qu'elles héritent. Dans iXtal, aucun effet pour l'invité : l'écran de
construction ne propose pas de processeur (`SdlSetup.cs:31`) et `resetpchard` rejoue la même UC.
*Trouvé par* : reconnaissance de G6.
*Source* : « Control register CR4 was introduced in the Pentium processor » (SDM vol. 3B, ch. 22,
p. 22-17) : documenté ; MOV CR4 sur un i486DX est donc #UD (déduit : registre inexistant).
*Cas qui discrimine* : `cpu-config-check`, C# seul : `cpu_set(iDX4)` puis `cpu_set(i486DX)` →
`cpu_features` = CR4 | VME et MOV CR4 accepté (PCem) ; #UD sur un i486DX. Rien que l'invité voie.
*G13* : (d) — aucun effet pour l'invité, l'UC étant fixée par processus : outillage seulement
(D1-contre C20, K12).
*Reproduit* : `Cpu/cpu.cs:476`, `cpu_set`, marqueur `PB-77` ; l'empreinte CPU compare `cpu_features`.

### PB-78 — LOADALL386 s'exécute sur un 486

PCem n'a pas de table d'opcodes 486 : `cpu_set` pose `ops_386` pour tout processeur (`cpu.c:231`),
et `0F 07` y est `opLOADALL386` (`x86_ops_misc.h:930-974`), sans garde `is486`. Le 486 réel n'a
plus de LOADALL : #UD. Les instructions propres au 486 ont, elles, leur garde `!is486` (INVD,
WBINVD, CMPXCHG, XADD : `x86_ops_misc.h:808-825`, `x86_ops_atomic.h`).
*Effet* : un 486 émulé charge l'état entier depuis ES:EDI ; un bloc non préparé y pose un CR0
avec PG et un CR3 quelconque — voir PB-79.
*Trouvé par* : fuzzeur du cœur 486 (G6.1), graine 1.
*Source* : « First of all, the 486 does not have a LOADALL instruction » (R. Collins, *The LOADALL
Instruction*, rcollins.org) : source secondaire ; l'opcode est absent de la carte du 486, d'où #UD
(déduit).
*Cas qui discrimine* : banc dirigé : `0F 07` sur l'ami486, ES:EDI sur un bloc préparé → état chargé
(PCem) ; INT 6 (486). Sur l'ami386, LOADALL inchangé.
*G13* : (b) — vrai comportement déduit, sans mesure ; la garde se pose dans le handler, pas dans la
table générée.
*Reproduit* en mode PCem : par la transcription (la table du 386 est partagée), `Cpu/386_ops_0f.cs`,
opLOADALL386, marqueur `fixed in hardware mode: PB-78`. Le fuzzeur du cœur 486 ne tire plus `0F 07` au hasard
(`Fuzzer.cs`, G6.1) : il ferait tomber l'oracle (PB-79).
*Corrigé en mode matériel* (G13.5) : sous `if (materiel.pb_78)`, sur le 486, opLOADALL386 remet pc sur l'instruction
et lève #UD, le geste d'ILLEGAL (`loadall486_materiel`, `Cpu/386.Materiel.cs`). `materiel-cas PB-78` rend le cas
ci-dessus et son témoin (le 386 charge le bloc dans les deux modes), et rougit la correction coupée. pm-check pose
le décor du 486 par LOADALL386 : en mode matériel, il lui faudra un autre chemin (G13.5c).

### PB-79 — Une table de pages hors RAM fait tomber l'émulateur

`mem.c:216-218` : `mmu_readl` et `mmu_writel` déréférencent `_mem_exec[addr >> 14]`, nul pour
toute adresse sans mémoire. `mmutranslatereal` (`:220-317`) et `mmutranslate_noabrt`
(`:319-345`) y lisent le répertoire en `cr3 & ~0xFFF` et la table en `PDE & ~0xFFF`, sans borne.
*Effet* : un invité qui active la pagination avec un CR3 ou une entrée de répertoire hors de la
mémoire installée arrête PCem (segfault). Mesuré : D4 (MOV CR0 avec PG tiré), puis le fuzzeur du
486 par un LOADALL386 tiré au hasard (PB-78).
*Trouvé par* : G2 D4, puis G6.1.
*G13* : hors du mode — déjà corrigé dans les deux modes (R9) ; rien n'y bascule.
*Non reproduit* (R9) : `Memory/mem.cs`, marqueurs `pcem bug, not reproduced: PB-79` — une
lecture de table hors RAM rend 0xFFFFFFFF (bus ouvert), une écriture y est ignorée. Le fuzzeur
écarte ce chemin côté oracle ; `r9-mmu` (C# seul) prouve la survie sur le 386 et le 486.

### PB-80 — La lecture linéaire en chain4 compact ne charge pas les verrous

`vid_svga.c:1391-1395`, `svga_read_linear` : en chain4 compact (`packed_chain4`) ou en
`fb_only`, la fonction rend l'octet et sort ; la forme par banque, `svga_read`, charge d'abord
les quatre verrous depuis `addr & ~3` (`:1085-1089`).
*Effet* : une écriture en mode 1 (copie des verrous) par la fenêtre linéaire, après une lecture
linéaire, recopie les verrous d'une lecture antérieure, pas ceux de l'octet lu. De même toute écriture qui combine
les verrous : les modes 0 et 2 sous un masque de bits ou une fonction logique, et le mode 3.
*Trouvé par* : diff des deux formes, G7.0.
*Source* : Cirrus Logic, *CL-GD542X Technical Reference Manual* (janvier 1994), § 6.27, p. 6-34 (CR22) : « These
latches are loaded whenever display memory is read by the CPU », sans exception pour le mode compact — documenté ;
les quatre octets de `addr & ~3`, déduit de la forme par banque ; les verrous de huit octets (GRB, p. 9-32), hors cas.
*Cas qui discrimine* : GD5429, SR7 bit 0 = 1, SR4 bit 3 = 1, verrous nuls, VRAM[8h..Bh] = 55h 66h 77h 88h : lecture
d'octet en A000:0008, puis GR5 = 41h (mode d'écriture 1) et écriture d'octet en A000:0010 : VRAM[10h] vaut 00h chez
PCem, 55h sur la GD542x.
*G13* : (a) — documenté ; CR22, que PCem ne modélise pas (`vid_cl5429.c:463-489`), en ferait la sonde en mode matériel.
*Reproduit* : `Video/vid_svga.cs`, `svga_read_linear`, marqueur PB-80 (`:1714`). Atteint par la seule GD5429 : sa
fenêtre linéaire (`gd5429_readb_linear`, `vid_cl5429.c:1276-1283`) et ses lectures de mot et de double mot quand
`fast` est nul (`:749-771`, `:1284-1300`). La VGA d'IBM, la Trio64 et les Trident ne posent ni `packed_chain4` ni
`fb_only` ; l'ET4000 pose `packed_chain4`, mais n'a pas de fenêtre linéaire chez PCem. Les lectures du chemin `fast`,
qui sautent aussi les verrous, l'ET4000 comprise : PB-221.
*G7.1* : la GD5429 porte le même défaut dans sa propre lecture, `gd5429_read_linear`
(`vid_cl5429.c:1183-1187`), et sa forme par banque, `gd5429_read` (`:741-748`), y passe aussi :
chez elle, ni la banque ni la fenêtre linéaire ne chargent les verrous en chain4 compact.
Reproduit, `Video/vid_cl5429.cs`, même marqueur (`:1322`).

### PB-81 — La lecture du motif du blitter de la GD5429 sort de la VRAM

`vid_cl5429.c:1415-1424`, `gd5429_start_blit`, source en motif (`blt.mode & 0xc0` = 0x40) :
l'adresse source est masquée PUIS augmentée de `y_count << 3` + `x_count & 7` (jusqu'à 63 en
8 bpp, 127 en 16 ; le cas 32 bpp, 255, n'est pas atteignable sur la GD5429, dont la profondeur
tient en un bit, `:1710-1713`). Une source programmée dans les derniers octets de la VRAM fait
lire au-delà du tableau `svga->vram`.
*Effet* : comportement indéfini — lecture du tas après la VRAM ; en C#, une exception qui
abattrait l'hôte. Le silicium reboucle sur sa mémoire.
*Trouvé par* : relecture de la transcription, G7.1.
*G13* : hors du mode — déjà corrigé dans les deux modes (R9) ; rien n'y bascule.
*NON reproduit* (R9) : `Video/vid_cl5429.cs`, marqueur `pcem bug, not reproduced: PB-81`,
`DEVIATION` — l'index complet est masqué une seconde fois par `vram_mask`. Identique à PCem
tant que la source ne touche pas le haut de la VRAM ; l'oracle n'y est pas conduit.
`iXtal26.Diff r9-cl5429` (C# seul) : BitBLT en motif, source en 1FFFF8, 8 et 16 bpp — survit ;
sans la garde, `IndexOutOfRangeException` (mesuré).

### PB-82 — Les modes d'écriture 4 et 5 de la GD5429 écrivent après la VRAM

`vid_cl5429.c:851-871` (mode 4) et `:911-931` (mode 5), branches sans « 16 bits étendus » :
huit octets sont écrits de `addr + 0` à `addr + 7` après un seul masquage par `vram_mask`
(`:809`). Avec l'adressage X8 l'adresse est alignée sur 8 ; sans lui (`GRB_WRITEMODE_EXT`
seul), elle ne l'est que sur 4 (`addr <<= 2`, `:804`), voire pas du tout en chain4 (`:795`).
*Effet* : comportement indéfini — jusqu'à sept octets écrits dans le tas après la VRAM ; en
C#, une exception qui abattrait l'hôte.
*Trouvé par* : relecture de la transcription, G7.1.
*G13* : hors du mode — déjà corrigé dans les deux modes (R9) ; rien n'y bascule.
*NON reproduit* (R9) : `Video/vid_cl5429.cs`, marqueurs `pcem bug, not reproduced: PB-82`,
`DEVIATION` — chaque octet des deux branches est masqué par `vram_mask`. Les branches 16 bits
(adresse alignée sur 16) sont inchangées. L'oracle n'y est pas conduit. `r9-cl5429` : modes 4
et 5, GRB = 04, écriture au dernier mot de la VRAM — survit ; sans la garde,
`IndexOutOfRangeException` (mesuré).

### PB-83 — Sans PCI, la S3 lève et baisse son IRQ dans `pci_irq_routing[-1]`

`vid_s3.c:2936` : `s3->card = pci_add(…)` sans condition ; sans PCI, `pci_add` rend -1
(`pci.c:189-190`). Puis `s3_update_irqs` (`vid_s3.c:161-166`), à chaque trame (`s3_vblank_start`)
et à chaque fin de FIFO, appelle `pci_set_irq` / `pci_clear_irq(-1, PCI_INTA)`, qui lisent
`pci_irq_routing[-1]` et, s'il est non nul, ÉCRIVENT `pci_irq_active[-1]` et lèvent une IRQ
calculée sur ce qu'ils ont lu (`pci.c:128-148`).
*Effet* : lecture, et écriture possible, d'un global voisin de l'hôte — selon l'édition de liens.
*Trouvé par* : lecture, G7 (« les défauts déjà relevés », n° 1) ; mesuré à la ligne en G7.3.
*G13* : hors du mode — déjà corrigé dans les deux modes (R9) ; rien n'y bascule.
*NON reproduit* (R9) : sur une carte VLB sans PCI, l'IRQ n'est câblée nulle part. Oracle :
`harness_s3.c` définit `pci_set_irq` / `pci_clear_irq` vides ; C# : `Video/vid_s3.cs`,
`s3_update_irqs`, marqueur `pcem bug, not reproduced: PB-83`. Aucune IRQ, rien d'écrit.

### PB-84 — Le curseur matériel lit hors de la VRAM

S3 : `vid_s3.c:2682-2683`, `s3_hwcursor_draw` indexe `vram[dword_remap(addr) + 0..3]` sans
masque ; l'adresse vient de CR4C/CR4D (jusqu'à 4 Mo) et croît de 16 à 32 octets par ligne.
GD5429 : `vid_cl5429.c:646-697`, l'adresse est masquée à sa pose (`:165-174`), mais en entrelacé
chaque ligne ajoute `line_offset` en plus, et un curseur logé dans les derniers octets de la
VRAM (SR13 = 3F) en sort.
*Effet* : lecture du tas après la VRAM ; en C#, une exception qui abattrait l'hôte.
*Trouvé par* : relecture de la transcription, G7.3.
*G13* : hors du mode — déjà corrigé dans les deux modes (R9) ; rien n'y bascule.
*NON reproduit* (R9) : `Video/vid_s3.cs`, `Video/vid_cl5429.cs`, marqueurs PB-84, DEVIATION —
chaque index est masqué par `vram_mask`. `r9-s3`, `r9-cl5429` : survit ; sans la garde,
`IndexOutOfRangeException` (mesuré).

### PB-85 — Le curseur matériel écrit après la fin de `buffer32`

`vid_s3.c:2687-2689`, `vid_cl5429.c:663-684` : `((uint32_t *)buffer32->line[displine])[offset
+ 32]`, `offset` allant jusqu'à x + 64 (x sur 11 bits). Au-delà de 2 048, l'écriture déborde sur
la ligne suivante — reproduit, c'est le même tableau — et, à la dernière ligne, après la fin de
`buffer32`.
*Effet* : écriture dans le tas ; en C#, une exception.
*Trouvé par* : relecture de la transcription, G7.3.
*G13* : hors du mode — déjà corrigé dans les deux modes (R9) ; rien n'y bascule.
*NON reproduit* (R9) : marqueurs PB-85 dans les deux fichiers, DEVIATION — hors du tableau,
rien n'est écrit. `r9-s3`, `r9-cl5429` : curseur en x = 2040 sur la dernière ligne, survit.

### PB-86 — La pente d'un polygone S3 divise INT_MIN par -1

`vid_s3.c:1651` et `:1669`, `polygon_setup` : `(end_x - start_x) / (end_y - start_y)`, les
abscisses décalées de 20 bits ; `destx_distp` = -2048 depuis x = 0 donne INT_MIN, et une hauteur
de -1 suffit.
*Effet* : SIGFPE, PCem tombe (comme PB-47) ; en C#, `OverflowException`.
*Trouvé par* : relecture de la transcription, G7.3.
*G13* : hors du mode — déjà corrigé dans les deux modes (R9) ; rien n'y bascule.
*NON reproduit* (R9) : `Video/vid_s3.cs`, marqueurs PB-86, DEVIATION — pour un diviseur de -1,
le quotient est pris replié (`-(end_x - start_x)`, INT_MIN pour INT_MIN), identique à PCem
partout ailleurs. `r9-s3` : survit, `poly_dx1` = `poly_dx2` = 80000000 ; sans la garde,
`OverflowException` (mesuré).

### PB-87 — Au repli de l'IP, la lecture d'instruction du 8088 et du 8086 se fait 64 Ko plus loin

`808x.c:145-153`, `FETCH`, file vide : `temp = readmembf(cs + cpu_state.pc)` (`:145`), puis
`prefetchpc = cpu_state.pc = cpu_state.pc + 1` et, sur un 8086 et un `pc` impair,
`prefetchqueue[0] = readmembf(cs + cpu_state.pc)` (`:150`). `cpu_state.pc` est sur 32 bits et
n'est masqué qu'en fin d'instruction (`:3910`) : une instruction qui commence en FFFFh porte `pc` à
0x10000, puis 0x10001, et les deux lectures se font à `cs + 0x10000` et au-delà — 64 Ko après le
début du segment — là où le vrai processeur lit à l'offset 0 (`prefetchpc`, sur 16 bits, vaut
bien 0, puis 1).

**Élargi en G13.1** (D1-contre C14) : la lecture principale (`:145`), sans garde `is8086`, a le
même défaut, sur le 8088 comme sur le 8086 ; la lecture de `:150` n'a lieu que juste après elle et
ne se trompe jamais seule. Les remplissages de `FETCHADD` (`:184`, `:191`) et de `FETCHCOMPLETE`
(`:215`, `:221`) passent par `prefetchpc` et sont justes.

*Effet* : quand une instruction est à cheval sur FFFFh et que la file est vide (après tout saut,
`FETCHCLEAR` la vide, `:242-243`), ses octets d'après FFFFh viennent d'ailleurs ; l'instruction se
décode et se chronomètre autrement. Rare dans du code réel.
*Trouvé par* : le fuzzeur 8086 en flux (G1.0, graine 1, ronde 325 : `FF FF` en FFFF:FFFF puis
FFFF:0001, 13 cycles contre 4). **La transcription de M1 lisait `cs + prefetchpc`** — le geste du
vrai 8086, pas celui de PCem —, et le 8088 n'atteignait jamais cette branche (`is8086`).
*Source* : documenté — « On the 8086, if sequential execution of instructions proceeds past offset
65,535, the processor fetches the next instruction byte from offset 0 of the same segment » (386 PRM
§ 14.7, point 8).
*Cas qui discrimine* : banc C# seul, 8088 et 8086 : `JMP FAR 1000:FFFF` (file vide) sur `B0` (MOV
AL,imm8) en 1FFFFh, 11h en 10000h, 22h en 20000h → AL = 22h (PCem), 11h (8088). SST n'exerce
vraisemblablement pas ce repli (D1-contre C18) ; le fuzzeur 8086 en flux sert de contrôle de fuite.
*G13* : (a), priorité basse — `FETCH` est le site le plus chaud du cœur 8088, et le cas est rare.
*Reproduit* en mode PCem : `Cpu/808x.cs`, `FETCH`, la lecture principale et le préfetch du 8086, marqueurs `fixed in hardware mode: PB-87`.
*Corrigé en mode matériel* (G13.3) : sous `if (materiel.pb_87)`, `fetch_materiel` lit à `cs + (pc & 0xFFFF)`, aux deux sites. `DEVIATION` (R9) : au-delà de 64 Kio dans une même instruction (une chaîne de préfixes qui remplit son segment, que le silicium ne finirait jamais), la lecture redevient celle de PCem. `materiel-cas PB-87` rend la valeur de PCem en mode PCem, celle du matériel en mode matériel, la sonde comptant la correction ; la correction coupée (`--attendu materiel` en mode PCem), il rougit. Le cas : MOV AL,imm8 en 1000:FFFF, AL = 22h (PCem) contre 11h, au 8088 et au 8086. SST au 8086 : les quatre cas à cheval sur FFFFh (formes `18`, `28`, `33`, `D2.3`, un chacune) passent. En mode PCem, la garde est pliée : le code machine d'`execx86` est celui de M0 ; le coût de la correction demandée est mesuré par `tools/perfbanc` (`FetchPb87`, `VERIFICATION.md` § G13.3).

### PB-88 — La M24 recopie jusqu'à 510 octets dans une `charbuffer` de 256

`vid_olivetti_m24.c:414-415`, `m24_poll` : `for (x = 0; x < (crtc[1] << 1); x++)
charbuffer[x] = …`, et `:171-172` la relit jusqu'à `(crtc[1] << 1) + 1`. Le registre R1 du CRTC
n'est pas masqué (`crtcmask[1] = 0xff`, `:41`) : l'index monte à 509 dans un tableau de 256
(`:19`).
*Effet* : PCem écrit 253 octets au-delà, dans les champs qui suivent `charbuffer` dans `m24_t`
(`ctrl`, `base`, `cgamode`… jusqu'au `pc_timer_t` de la carte, pointeurs de rappel et de
chaînage compris) : l'invité peut faire tomber l'émulateur. En C#, une exception.
*Trouvé par* : reconnaissance de G1 (PLAN-G1.md, défaut n° 1).
*G13* : hors du mode — déjà corrigé dans les deux modes (R9) ; rien n'y bascule.
*NON reproduit* (R9) : `Video/vid_olivetti_m24.cs`, marqueurs PB-88, DEVIATION — une écriture
au-delà de 255 est sautée, une lecture rend 0. Identique à PCem tant que R1 ≤ 128 (les modes
du BIOS : 40 et 80 colonnes).

### PB-89 — La bordure de la M24 déborde sur la ligne suivante de `buffer32`

`vid_olivetti_m24.c:154-166`, `:218-231`, `:258-259`, `:279`, `hline` `:287-289` : l'abscisse `c + (crtc[1] << 4) +
8` atteint 4 095 pour une ligne de 2 048 points. Les lignes de `buffer32` sont contiguës
(`wx-sdl2-video.c:59-69`) : l'écriture tombe sur la ligne suivante, jamais hors du tableau —
`displine` reste sous 720.
*Effet* : des points de bordure sur la ligne d'en dessous, avec un R1 hors des modes du BIOS.
*Trouvé par* : reconnaissance de G1 (défaut n° 2) ; la relecture a montré qu'il ne sort pas du
tableau, donc pas de R9 pour la M24.
*Source* : data sheet MC6845 (Motorola) : « Any 8-bit number may be programmed as long as the contents of R0 are
greater than the contents of R1 » — documenté ; au-delà, ce qui dépasse la ligne balayée tombe dans le retour de ligne,
rien de la ligne n ne s'affiche en n + 1 : déduit ; l'image réelle avec R1 > R0, qui dépend du moniteur : inconnue.
*Cas qui discrimine* : M24, 3D8h = 09h (80 colonnes), R1 = FFh, la ligne n + 1 de `Buffer32` remplie d'un motif :
après le rendu de la ligne n, PCem a écrit la bordure en `Buffer32[(n + 1) × 2 048 + 0..7]` ; la carte n'y touche pas.
*G13* : (b) — le comportement réel n'est que déduit : un test borne la ligne, sans image de référence.
*Reproduit* : `Video/vid_olivetti_m24.cs`, marqueur PB-89 (`:196`, le même tableau plat).
*G1.2* : le PC1512 fait de même (`vid_pc1512.c:182-193`, `:317-319`), `displine` revenant à 0
au-delà de 360 (`:326-327`) et de 262 (`:369`) : jamais hors du tableau non plus. Reproduit,
`Video/vid_pc1512.cs` (`:231`, `:430`).
*G13.1* : la CGA, la MDA, l'Hercules et l'EGA aussi. La CGA déborde dès que R1 dépasse 127 (254 en 80 colonnes) : la
bordure (`vid_cga.c:147-153`), les boucles de 40 colonnes et du graphique (`:188-270`), `hline` (`:275`, `:277`) et la
conversion de fin de ligne (`:280-294`). La MDA : `(x * 9) + c` monte à 2 294 quand R1 dépasse 227
(`vid_mda.c:114-130`). L'Hercules : 2 294 en texte (`vid_hercules.c:167-185`), 4 079 en graphique quand R1 dépasse
128 (`:155`). L'EGA borne son texte par `& 2047` (`vid_ega.c:291-320`), pas ses rendus graphiques ni l'écran éteint,
qui suivent CR01 : jusqu'à 4 143 (`:358-381`, `:425-453`), 2 087 (`:497-517`) et 4 639 (`:542-554`). Jamais hors du
tableau : `displine` reste sous 360 (CGA), 500 (MDA, Hercules) et 502 (EGA). Reproduits, marqueurs PB-89 :
`Video/vid_cga.cs:244`, `Video/vid_mda.cs:195`, `Video/vid_hercules.cs:198`, `Video/vid_ega.cs:476`, `:528`, `:593`,
`:676`.

### PB-90 — Le drapeau `DMA_OVER` entre dans l'échantillon ADPCM

`sound_sb_dsp.c:943`, `:984`, `:1019`, `pollsb` : `sbdat2 = sb_8_read_dma(dsp)`, et
`dma_channel_read` rend `octet | DMA_OVER` (0x10000, `dma.h:9`, `dma.c:564`) à l'octet qui fait passer sous zéro le
compte du 8237 — son terme de comptage, et non le dernier octet du bloc du DSP (corrigé en G13). `sbdat2` garde le
drapeau : en ADPCM 4 bits, `sbdat2 >> 4` vaut 0x1000 + quartet, `tempi` sature à 63 (`:925-926`) ; en 2,6 bits,
`sbdat2 >> 5` sature à 39. Les commandes 74h à 77h, 7Dh et 7Fh rangent de même leur premier octet (`:412`, `:423`,
`:432`, `:439`), si le compte du 8237 finit sur lui.
*Effet* : le premier échantillon décodé de l'octet marqué saute de `scaleMap4[63]` = −60 (ou `scaleMap26[39]` = −35)
au lieu de son pas, et la suite du flux garde l'écart, l'ADPCM étant différentiel : un saut à chaque terme du 8237 qui
tombe dans un flux qui continue — à chaque tour du tampon du 8237 en automatique, au milieu du bloc en simple cycle.
Quand le compte du 8237 égale le bloc, l'octet marqué est le dernier lu, que PCem ne décode pas (PB-234) : pas
de saut, un octet perdu. En 2 bits (16h, 17h et 1Fh, `:342`, `:356` ; `pollsb`, `:1019`), le `& 3` (`:999`) le masque.
*Trouvé par* : reconnaissance de G8 (PLAN-G8.md, défaut n° 2) ; précisé en G13 (reconnaissance D5, contre-lecture K11).
*Source* : le terme de comptage sort sur la broche EOP du 8237A, distincte des données (Intel, fiche 8237A, 231466-005,
tableau 1, p. 2 ; T/C, broche B27 du bus, IBM AT TR p. 1-20) ; le DSP lit l'octet seul et le décode (micrologiciel 2.02,
`vector_dma_dac_adpcm4`, `sbv202.asm:445-530`). Documenté ; `DMA_OVER` n'est qu'une convention interne de PCem.
*Cas qui discrimine* : SB 2.0, canal 1 du 8237 en automatique sur 4 octets (80h, 00h, 00h, 00h) ; 75h 07h 00h : au
premier échantillon du 4e octet lu, le terme du 8237, PCem fait passer `sbref` de 80h à 44h et la sortie de 0 à C400h
(−15 360) jusqu'à la fin du bloc ; sans le drapeau, `sbref` reste à 80h et la sortie à 0.
*G13* : (a) — documenté (la fiche du 8237A, le micrologiciel), et le cas se joue en C# seul.
*Reproduit* : `Sound/sound_sb_dsp.cs`, marqueurs PB-90 : les deux lectures ADPCM de `pollsb` (4 et 2,6 bits) et les
lectures de `sbdat2` de 74h/75h, 76h/77h, 7Dh et 7Fh. La lecture 2 bits de `pollsb` porte PB-91 ; celles de 16h/17h et
de 1Fh, sans effet, n'ont pas de marqueur.

### PB-91 — L'ADPCM 2 bits ne finit jamais

`sound_sb_dsp.c:1016-1020` : au contraire de l'ADPCM 4 bits (`:944`) et 2,6 bits (`:985`), la
branche `ADPCM_2` lit l'octet suivant sans décrémenter `sb_8_length`.
*Effet* : les commandes 0x16/0x17 (ADPCM 2 bits, simple) jouent sans fin la mémoire que le DMA
rend, sans IRQ de fin ; 0x1F (automatique) ne recharge jamais. Un programme qui attend l'IRQ
attend toujours.
*Trouvé par* : reconnaissance de G8 (défaut n° 1).
*Source* : guide de Creative, p. 6-7 (PDF 92) : 16h et 17h prennent « the number of bytes to transfer less 1 » ; le DSP
2.02 décrémente le compte à chaque octet lu (`vector_dma_dac_adpcm2`, `sbv202.asm:343-440`, `X01a2`, `X01a4`), puis lève
l'IRQ (`X0159`) ou recharge le bloc de 48h en automatique (`X016e`). Documenté deux fois.
*Cas qui discrimine* : SB 2.0, canal 1 du 8237 en simple cycle sur 4 octets ; 16h 03h 00h : la carte lève l'IRQ 8 bits
après 4 octets pris au 8237 ; PCem jamais (`sb_8_length` reste à 2, `sb_8_enable` à 1). En automatique (48h 03h 00h,
puis 1Fh), la carte recharge `sb_8_autolen` tous les 4 octets ; PCem jamais.
*G13* : (a) — documenté ; l'instant de l'IRQ se combine avec la fin de bloc du 2.02 (PB-234).
*Reproduit* : `Sound/sound_sb_dsp.cs`, `pollsb`, marqueur PB-91.

### PB-92 — L'IRQ 10 de la Sound Blaster se perd sur une machine sans second PIC

`sound_sb.c` propose l'IRQ 10 à la configuration des SB Pro v1 et v2 (`:1220`, `:1242`), sans regarder la machine.
`sb_irq` (`sound_sb_dsp.c:107-114`) appelle `picint(1 << 10)` ; sans `AT`, `picint` (`pic.c:302-308`) n'accepte que
`num <= 0xff` : l'interruption est jetée. C'est aussi ce que fait la carte (refondé en G13) : l'IRQ 10 n'existe que sur
la rallonge de 36 broches de l'AT, qu'un emplacement de PC ou d'XT n'a pas. La reconnaissance de G13 l'appuyait sur DOS
Days, qui dit le contraire (la rallonge de la CT1330 « not wired to anything ») ; stason.org (fiche TULARC) et Wikipédia
la disent reliée à l'IRQ 10 et au DMA 0 (sources secondaires). Si DOS Days avait raison, l'IRQ 10 ne marcherait sur
aucune machine : c'est PCem sur AT qui aurait tort, pas sur un PC.
*Effet* : sur un PC ou un XT réglé à l'IRQ 10, la carte ne signale jamais rien, sans message — comme la vraie carte ;
seule la configuration, qui offre un réglage sans effet sur ces machines, est en cause.
*Trouvé par* : reconnaissance de G8 (défaut n° 3) ; refondé en G13 sur le brochage (contre-lecture K1).
*Source* : IBM, *Technical Reference, Personal Computer AT* (mars 1984), p. 1-21 (I/O Channel, D-Side : D3 = IRQ10,
D8 et D9 = −DACK0 et DRQ0) et p. 9-3 (« Adapters designed to make use of the 36-pin connector are not compatible with
the rest of the IBM Personal Computers »). Documenté pour le bus ; le câblage de la CT1330, sources secondaires.
*Cas qui discrimine* : aucun sur un PC ou un XT, où la carte et PCem perdent l'IRQ 10. Sur un AT, si la CT1330 ne
câblait pas sa rallonge, F2h à l'IRQ 10 lèverait l'IRQ chez PCem et rien sur la carte : à relever sur une CT1330.
*G13* : (d) — fidèle sur un PC et un XT ; la part (c), le câblage de l'IRQ 10 sur la CT1330, ne touche que l'AT. Au
plus, un avertissement de configuration de l'hôte.
*Reproduit* : `Sound/sound_sb_dsp.cs`, `sb_irq`, marqueur PB-92 ; les profils du dépôt prennent l'IRQ 5.
*G12.0* : la SB Pro v1 propose aussi l'IRQ 10 (`sound_sb.c:1220`) ; les SB 1.0, 1.5 et 2.0 ne la proposent pas
(`:1149-1153`, `:1179-1183`).

### PB-94 — La souris PS/2 ne répond pas aux commandes qu'elle ne connaît pas

`mouse_ps2.c:60-146`, `mouse_ps2_write` : le `switch` des commandes ne traite que E6h à E9h, EBh,
F2h à F5h et FFh ; le `default`, qui appelait `fatal()`, est commenté (`:144-145`). Une autre
commande — F6h (valeurs par défaut), EAh (mode flux), F0h (mode distant), EEh (écho), ECh, EDh —
est retenue dans `command` et ne reçoit RIEN, pas même l'accusé FAh que la vraie souris rend à
toute commande. `MOUSE_REMOTE` et `MOUSE_ECHO` (`:12`) ne sont jamais posés.
Élargi en G13.4c (contre-lecture) : ni l'init (`:214-226`) ni le reset FFh (`:134-142`) ne posent les valeurs par
défaut de la souris, 100 points par seconde et 4 points par mm ; un E9h sur une souris neuve rend 00h 00h.
*Effet* : un pilote qui envoie F6h ou F0h attend son accusé jusqu'à son délai, puis conclut à
une souris absente ou en panne.
*Trouvé par* : reconnaissance de PS2 (PLAN-PS2.md, défaut n° 1).
*Source* : Chapweske, « The PS/2 Mouse Interface », 2001, et Brouwer, « The PS/2 mouse » (secondaires) : FAh à toute
commande valide, FEh puis FCh à une invalide ; F6h, F0h, EAh, EEh et ECh. IBM 15F0306 p. 2-97 ne décrit que l'état
posé par INT 15h C2h (désactivée, 100/s, 4 points/mm, 1:1). Documenté (secondaire).
*Cas qui discrimine* : PS2BANC sous `--force-ps2`, F6h par D4h : PCem ne rend rien (le tampon de sortie reste vide
jusqu'au délai du pilote) ; la souris rend FAh, puis E9h donne FAh 00h 02h 64h.
*G13* : (b) — les attendus ne reposent que sur des sources secondaires ; une vraie souris PS/2 les fixerait.
*Reproduit* en mode PCem : `Mouse/mouse_ps2.cs`, `mouse_ps2_write`, marqueur `fixed in hardware mode: PB-94` ; le
banc PS2BANC le montre (F6h → rien, EEh au relevé).
*Corrigé en mode matériel* (G13.4) : sous `if (materiel.pb_94)`, le `default` du switch rend FAh à F6h (les valeurs par
défaut : 100 par seconde, 4 points par mm, échelle 1:1, flux coupé, compteurs à zéro), à EAh (flux), à F0h (distant),
à EEh (écho) et à ECh (fin de l'écho, le mode d'avant), et FEh puis FCh à une commande inconnue ; l'init et le reset
FFh posent les valeurs par défaut, comme F6h ; en tête de
`mouse_ps2_write`, l'écho renvoie tout octet hors ECh et FFh (`Mouse/mouse_ps2.Materiel.cs`). FEh (renvoyer le dernier
paquet) n'est pas modélisé : il reste sans réponse ; ni le refus d'une donnée hors de sa plage après E8h ou F3h.
`materiel-cas PB-94` (E9h sur la souris neuve, FAh 00h 02h 64h ; E8h 03h, F3h 28h, F6h, E9h, de nouveau 02h 64h ;
F0h et un mouvement, aucun paquet ; ECh hors de l'écho ; l'écho, et FFh qui en sort ; EDh, F5h, EDh, EDh : FEh, FAh,
FEh, FCh) rend les valeurs de PCem en mode PCem, celles de la
souris en mode matériel ; la correction coupée, il rougit. PS2BANC en C# seul (`banc-ps2banc-2`, `-3`) : F6h lu FAh.
*PS2.1* : aucune machine du dépôt ne monte la souris PS/2 (pas de `MODEL_PS2`, décision
utilisateur du 03/10) ; le défaut n'est atteint que par la porte de vérification (`--force-ps2`).

### PB-95 — L'octet d'état de la souris PS/2 (E9h) place mal les trois boutons

`mouse_ps2.c:79-84`, commande E9h (état) : `mouse_buttons & 1` pose le bit 0, `& 2` le bit 1, et `& 4` pose `|= 3`.
PCem y reprend la disposition du paquet de flux (`:195-200`) et d'EBh (`:98-103`) — le gauche en bit 0, le droit en
bit 1, le milieu en bit 2 pour une souris à trois boutons —, et code le milieu par les bits 0 et 1. Or l'octet
d'état d'IBM met le gauche en bit 2, le droit en bit 0, et réserve le bit 1. Cette entrée disait que l'état porte le
milieu en bit 2 : c'est faux (contre-lecture de G13, la source ci-dessous).
*Effet* : un pilote qui lit l'état voit le gauche comme le droit, le droit comme le bit réservé (le milieu d'une
souris à trois boutons), et le milieu comme le droit et le milieu ensemble.
*Trouvé par* : reconnaissance de PS2 (défaut n° 2) ; la disposition, par la reconnaissance de G13.
*Source* : IBM 15F0306, INT 15h C2h AL = 06h BH = 00h, « Status byte 1 », p. 2-97 : bit 6 distant, 5 activée, 4
échelle 2:1, 2 gauche, 1 réservé, 0 droit ; de même l'ABIOS (p. 6-118) et le TR du Model 25 (84X0672, p. 5-48).
Documenté ; que l'octet brut d'E9h soit le même est déduit (le BIOS relaie les trois octets d'E9h).
*Cas qui discrimine* : PS2BANC sous `--force-ps2`, souris activée (F4h), E9h : gauche tenu, PCem 21h et IBM 24h ;
droit tenu, 22h et 21h ; milieu tenu sur une souris à deux boutons, 23h et 20h.
*G13* : (a) — avec PB-94 (le bit 6 suit le mode distant) ; le `temp |= 4` de PS2.0 n'en est pas l'attendu.
*Reproduit* en mode PCem : `Mouse/mouse_ps2.cs`, commande E9h, marqueur `fixed in hardware mode: PB-95` ; le banc
PS2BANC, bouton du milieu tenu, le montre à E9h (23h).
*Corrigé en mode matériel* (G13.4) : sous `if (materiel.pb_95)`, l'octet d'état d'IBM (`mouse_ps2_etat_materiel`) :
bit 6 le mode distant, 5 le flux, 4 l'échelle, 2 le gauche, 0 le droit, et le bit 1 au milieu d'une souris à trois
boutons seulement (Chapweske ; réservé chez IBM, comme le paquet le fait du bit 2). `materiel-cas PB-95` (gauche,
droit, milieu sur la souris à deux boutons, milieu sur l'Intellimouse : 21h, 22h, 23h, 23h contre 24h, 21h, 20h, 22h)
rougit la correction coupée. PS2BANC en C# seul, le milieu tenu : 13h et 23h deviennent 10h et 20h (deux boutons), 12h
et 22h (l'Intellimouse).
*PS2.1* : aucune machine du dépôt ne monte la souris PS/2 (pas de `MODEL_PS2`, décision
utilisateur du 03/10) ; le défaut n'est atteint que par la porte de vérification (`--force-ps2`).

### PB-97 — Le 6845 de la MDA n'a pas de masques, et réécrit le curseur d'un BIOS

`vid_mda.c:28`, `:55` : chaque registre du 6845 s'écrit en entier et se relit, là où la vraie
puce rend R0-R13 en écriture seule et masque R4, R6, R7 sur 7 bits. Un logiciel qui relit le
CRTC pour reconnaître la carte voit autre chose que sur le vrai matériel. `:29-34` : si R10 = 6
et R11 = 7, PCem les réécrit en Bh/Ch (« Fix for Generic Turbo XT BIOS ») — un curseur choisi par
le logiciel devient un autre. `:99-100` : en entrelacé, `sc = (sc << 1) & 7` perd les lignes 8 et
au-delà.
*Trouvé par* : reconnaissance de G9 (PLAN-G9.md, défauts n° 3 et 4).
*Source* : data sheet MC6845 (Motorola), table des registres : R4, R6, R7, R10 sur 7 bits, R5, R9, R11 sur 5, R8 sur 6,
R0-R13 non relisibles, l'entrelacé « sync and video » ; IBM PC TR 6025008 (1981), listing du BIOS : SET_CTYPE envoie CX
tel quel à R10-R11 (M16) — documenté ; la valeur que rend un registre en écriture seule : inconnue.
*Cas qui discrimine* : MDA, R10 = 06h puis R11 = 07h : PCem 0Bh-0Ch, le 6845 06h-07h ; R4 = 99h : PCem 99h, le 6845
19h ; R8 = 03h et R9 = 0Dh : PCem ne dessine que les rangées 0, 2, 4 et 6 de la police, en boucle ; le 6845, les
rangées 2k + trame, de 0 à 13 (déduit).
*G13* : (a), (b), (c) — (a) les masques et le Turbo XT ; (b) l'entrelacé ; (c) la valeur relue, inconnue sans mesure.
*Reproduit* : `Video/vid_mda.cs`, marqueurs PB-97 (`:88`, l'écriture et le Turbo XT ; `:182`, l'entrelacé).
*G9.1* : l'Hercules fait de même (`vid_hercules.c:89`, `:54-60`, `:137-138`) ; reproduit,
`Video/vid_hercules.cs` (`:80`, `:186`). Le banc HERCBANC relit les douze registres du mode graphique.
*G13.1* : la table de masques du mode matériel se tire du data sheet, pas de `crtcmask` de la CGA, qui laisse R16-R17
inscriptibles (PB-230). La CGA, la M24 et le PC1512 relisent aussi R0-R13 : PB-230. Le curseur 0607h
tombe à mi-cellule sur une vraie MDA : le BIOS d'IBM le recopie tel quel (VIDEO_PARMS donne 0B0Ch au seul mode 7).

### PB-99 — L'EGA de PCem a des traits de la VGA, et trompe le test de son BIOS

`vid_ega.c` :
- (1) l'attribut 10h bit 7 et l'attribut 14h composent la palette (`:39-42`), et l'attribut 10h
  bit 5 la fenêtre de défilement (`:613`) — des registres de la VGA ;
- (2) CR11 bit 7 protège CR0-CR7 en écriture (`:128`) — idem ;
- (3) tous les registres se relisent (`:152-179`), là où l'EGA est presque toute en écriture seule
  (CR10/CR11 rendent le crayon optique) : un logiciel qui distingue l'EGA de la VGA en relisant un
  registre se trompe ;
- (4) `3DAh` : `stat ^= 0x30` à chaque lecture (`:182`, « Fools IBM EGA video BIOS self-test ») au
  lieu des broches vidéo ;
- (5) le texte n'est redessiné que sur `fullchange` (`:559`) : un curseur, une police (SR3) ou un
  attribut 10h changés n'apparaissent qu'au prochain rafraîchissement complet ;
- (6) la mémoire configurée (64 ou 128 Ko) borne le processeur (`vram_limit`) et le rendu (`vrammask`, `:263-264`,
  `:324`, `:337`, `:1056-1057`), mais les substitutions de rangée de CR17 s'appliquent après le masque (`:344-347`,
  `:405-408`, `:477-480`) : avec 64 Ko, le seul bit 0x10000 (CR17 bit 1 = 0) lit des octets que la carte n'a pas, et
  aucun mode du BIOS d'IBM ne pose ce bit à 0 (déduit). La police du texte, lue sans masque (`:287`), sort de même
  dès que SR3 choisit une table au-delà de la mémoire (relu en G13.1) ;
- (7) CR07 bits 5-7 et CR09 bit 6 lus comme les bits 9 de la VGA (`vtotal`, `dispend`, `vsyncstart`, `split`,
  `:200-219`), et CR0A bit 5 qui éteint le curseur (`:619`) — des registres de la VGA, inscrits en G13.1.
*Trouvé par* : reconnaissance de G9 (PLAN-G9.md, défaut n° 6) ; (6) resserré et (7) ajouté par la reconnaissance de
G13 (D4-contre, § 2 n° 6 ; D4-video, § PB-99).
*Source* : IBM, *Options and Adapters TR*, vol. 2 (1984), EGA : 10h bits 4-7, 07h bits 6-7, 09h et 0Ah bits 5-7, 11h
bits 6-7 « Not Used » ; écriture seule hors 0Ch-0Fh (pp. 30-37, 56-61) ; 3DAh bits 4-5, deux sorties vidéo choisies par
12h (pp. 15-17, 60), que POD14 teste (pp. 113-114) — documenté ; (5), (6) : déduits ; la valeur relue : inconnue.
*Cas qui discrimine* : (1) AC00 = 01h, AC14 = 0Fh, AC10 = 80h : PCem egapal[0] = F1h, l'EGA 01h ; (2) CR11 = 80h puis
CR01 = 27h : PCem garde l'ancien CR01, l'EGA prend 27h ; (7) CR07 = 20h, CR06 = 70h : PCem `vtotal` = 271h, l'EGA 71h.
*G13* : (a), (b), (c) — (a) (1), (2), (5), (7) ; (b) (4), un modèle neuf du faisceau, et (6) ; (c) la valeur de (3).
*Reproduit* : `Video/vid_ega.cs`, marqueurs PB-99 : `:139` (10h bit 7 et 14h), `:237` (CR11), `:264` (la relecture),
`:302` (3DAh), `:322` (les bits 9), `:418` (la police), `:703` (`fullchange`), `:770` (10h bit 5), `:779` (CR0A bit 5),
`:1205` (la mémoire). Le banc EGABANC relit le CRTC et l'attribut 10h, et voit 3DAh basculer ses bits 4-5.

### PB-100 — La Tseng ET4000AX de PCem et son RAMDAC

`vid_et4000.c` :
- `crtc_mask` (`:34-37`) efface CR19-CR2F et CR38-CR3F : c'est fidèle à l'AX, dont la table 4.3-2 du data book n'a
  que les index 00h-18h et 32h-37h — CR3F, dont `et4000_recalctimings` lit le débordement de `htotal` (`:399`), est un
  registre de la W32, et ce point n'est pas un défaut. Mais CR30 et CR31, absents de l'AX, restent inscriptibles et se
  relisent ;
- un CR13 nul vaut 256 (`:397-398`) ;
- la fenêtre linéaire de 128 Ko (`banked_mask = 0x1ffff`, `:72-75`) n'est posée que sur une
  transition non nul → nul de GDC6 ; `svga_init` laisse `banked_mask` à 0. Le masque nul ne se voit pas : `svga_out`
  ne change la carte que si GDC6 bits 2-3 changent, et le BIOS pose un mode avant tout accès ; le data book déconseille
  la carte de 128 Ko en modes étendus (pp. 85, 150-151), dont l'effet sur les segments de 3CDh n'est pas documenté ;
- pas de séquence KEY (03h en 3BFh, puis A0h en 3D8h ou 3B8h, p. 102) : ce qu'elle garde reste ouvert — les écritures
  du CRTC au-delà de 18h hors 33h et 35h (p. 111), CR36-CR37 (pp. 136-137), TS 6 et TS 7 (p. 142), l'ATC 16h (p. 158),
  3CDh avant la première pose (p. 144), et la lecture de l'Input Status 0 bits 5-6 (p. 102) et du bit 7 de 3CAh
  (p. 104). Le retrait du KEY n'est décrit nulle part dans le data book ;
- SR7 se relit avec le bit 2 forcé (`:269-271`). « Set to 1 (always) » (p. 142) décrit un registre en lecture-écriture :
  une consigne de programmation, pas un bit câblé ; PCem ne force pas non plus le bit 4 que la même page donne en
  révision E. La valeur relue est inconnue.
`vid_unk_ramdac.c` : FFh écrit une fois le RAMDAC armé ne touche pas le registre de commande et
tombe dans `svga_out` (le masque des pixels, `:24`) ; chez Sierra, l'écriture va au registre de commande sans
exception, et FFh y pose D4, l'ERPF, avec le code réservé 111 (tables 3 et 10). Le décodage des profondeurs
(`:27-61`) tire 24 ou 32 bits de D5, qui choisit chez Sierra les fronts d'horloge (modes 1a/1b, 3a/3b), et invente un
décodage du code réservé 111 (`:45-58`). Les modes à 4 octets par point existent (3a et 3b, le quatrième octet jeté,
table 2), mais par le registre de repack, l'étendu 10h, que l'ERPF ouvre.
*Trouvé par* : reconnaissance de G9 (PLAN-G9.md, défauts n° 9 et 10) ; CR3F, SR7, le KEY et la Sierra repris par la
reconnaissance de G13 (D4-video, § PB-100 ; D4-contre, § 2 n° 7 à 11).
*Source* : Tseng Labs, data book *ET4000 Graphics Controller* (1990), pp. 85, 102-104, 111, 123, 136-137, 142, 144,
150-151, 158 — documenté, hors le retrait du KEY (inconnu) ; Sierra, data sheet *SC15025/SC15026*, tables 2, 3, 9 et
10, pp. 3-83 à 3-91 — documenté ; la puce exacte de la carte modélisée (« SC1502x ») : inconnue.
*Cas qui discrimine* : CR13 = 00h : PCem `rowoffset` = 256, l'ET4000 0, la même rangée répétée ; sans KEY, CR36 = 5Ah :
PCem l'écrit, l'ET4000 l'ignore ; FFh écrit une fois armé : PCem garde le registre de commande, la Sierra y prend FFh.
*G13* : (b), (c), (d) — (b) CR13, 128 Ko, le KEY posé ; (c) son retrait, SR7, FFh, 24/32 bits, CR30-31 ; (d) CR3F.
*Reproduit* : `Video/vid_et4000.cs`, marqueurs PB-100 : `:48` (le masque), `:72` (le KEY, en écriture), `:95` (la
fenêtre de 128 Ko), `:144` (le KEY, en lecture), `:149` (SR7), `:187` (CR13) ; `Video/vid_unk_ramdac.cs:39` (FFh et
les profondeurs).

### PB-101 — `lpt2_remove_ams` ne retire rien

`lpt.c:166` : `io_removehandler(0x0379, 0x0002, lpt2_read, …, lpt2_write, …)`. Les gestionnaires
de LPT2 sont à 278h (`lpt_init`, `:144`) : aucun n'est à 379h-37Ah, et l'appel, le premier
d'`amstrad_init` (`amstrad.c:144`), ne fait rien. Le PC1512 garde donc le LPT2 de `lpt_init` à
278h, ses registres de données et de contrôle relus. `ams_init` retire LPT1 (`model.c:263`), et
`amstrad.c:149` repose 378h-37Ah sur ses propres gestionnaires.
*Effet* : le PC1512 émulé a un second port parallèle, à 278h, que la machine réelle n'a pas ; un
logiciel qui sonde 278h le trouve. Son BIOS le trouve aussi (contre-lecture de G13, la ROM v1 40043/40044) : il
sonde 3BCh puis 278h par AAh et 55h sur le registre de données (`F000:C8D6-C8FA`), et `lpt2_read` rend `lpt2_dat`
(`lpt.c:130-139`) : la sonde réussit, d'où 0278h en 0040:000A et une imprimante de plus en 0040:0011 (déduit de la
ROM, à mesurer par LPTBANC).
*Trouvé par* : transcription de G10.0.
*Source* : Amstrad, PC1512 Technical Reference Manual, section 1, § 1.3, 1.4 (« 278 - 27F External Printer Port »,
sur une carte d'extension) et 1.10 : un seul port parallèle intégré, en 378h-37Ah. Documenté ; sans carte, 278h ne
répond pas (FFh, la valeur d'un port sans gestionnaire ; déduit).
*Cas qui discrimine* : LPTBANC sur le PC1512 : PCem, 0040:000A = 0278h et 278h relu AAh après OUT 278h,AAh ; le
PC1512, 0040:000A = 0000h et 278h relu FFh.
*G13* : (a) — `lpt2_remove()` (278h) au lieu de `lpt2_remove_ams()` dans `amstrad_init`.
*Reproduit* en mode PCem : `Lpt/lpt.cs` (`lpt2_remove_ams`), `Models/amstrad.cs` (`amstrad_init`), marqueurs `fixed in
hardware mode: PB-101`.
*Corrigé en mode matériel* (G13.4) : sous `if (materiel.pb_101)`, `amstrad_init` retire le LPT2 là où il est, par
`lpt2_remove()` (`Models/amstrad.Materiel.cs`). `materiel-cas PB-101` (lpt_init, amstrad_init, OUT 278h,AAh, IN 278h)
rend AAh en mode PCem, FFh en mode matériel ; la correction coupée, il rougit. Le même `amstrad_init` sert aux
autres Amstrad du dépôt, qui n'ont pas non plus de port en 278h sans carte.

### PB-102 — La MDA et l'Hercules balayent avec un caractère de 8 points

`pit.c:42` : `MDACONST = clock / 2032125.0`, soit un caractère de 16,257 MHz / 8 ;
`mda_recalctimings` (`vid_mda.c:79-80`) et `hercules_recalctimings` (`vid_hercules.c:115-116`)
en multiplient R0 + 1 et R1. La vraie MDA dessine des caractères de 9 points. PCem le sait pour
l'EGA, qui prend `MDACONST * (9.0 / 8.0)` en mode 9 points (`vid_ega.c:231`), mais pas pour la
MDA ni pour l'Hercules. En mode texte (R0 = 61h, 98 caractères par ligne, 370 lignes), la trame
tourne à 20,74 kHz et 56,04 Hz au lieu de 18,43 kHz et 50 Hz. En mode graphique, le 6845 de
l'Hercules compte des unités de 16 points avec le même pas : R0 = 35h donne 37,6 kHz et 102 Hz
(déduit à la lecture, non mesuré).
*Effet* : la MDA et l'Hercules tournent 12,5 % trop vite en mode texte (le retour de trame lu en
3BAh, le clignotement du curseur), deux fois trop vite en graphique ; le moniteur simulé de l'hôte
lit 20,74 kHz (mesuré, VERIFICATION.md, § L'hôte : le moniteur automatique).
*Trouvé par* : la mesure du correctif « hors plage » de l'hôte (plan qualité du 04/10, § 1c).
*Source* : IBM, *Options and Adapters TR*, vol. 2 (1984), MDA : caractère de 9 points, moniteur de 18 kHz (p. 2),
oscillateur de 16,257 MHz (schéma feuille 3, p. 21) ; Hercules, *GB101 Owner's Manual* : 0,5625 µs par caractère en
texte, 1 µs en graphique (p. 10) — documenté ; l'horloge de 16 MHz de l'Hercules, déduite de ces temps.
*Cas qui discrimine* : MDA, mode 7 (R0 = 61h, R4 = 19h, R5 = 06h, R9 = 0Dh) : PCem, ligne de 48,23 µs et trame de
17,84 ms ; IBM, 54,25 µs (18,43 kHz) et 20,07 ms (49,8 Hz). Hercules en graphique (R0 = 35h, R4 = 5Bh, R5 = 02h,
R9 = 03h) : PCem 26,57 µs par ligne, la GB101 54 µs (18,52 kHz, 50,05 Hz).
*G13* : (a) — documenté ; un commutateur propre, `--menu-check` resserré, l'Hercules recalculé aussi sur 3B8h et 3BFh.
*Reproduit* : `Video/vid_mda.cs`, `Video/vid_hercules.cs`, marqueurs PB-102 (`vid_mda.cs:157`, `vid_hercules.cs:159`).

### PB-103 — La CH Flightstick Pro et la TM FCS n'ont pas de haut-gauche

`joystick_ch_flightstick_pro.c:26-33` lit le chapeau par quatre intervalles : `pov > 315 || pov <
45` (en haut), puis `[45, 135)`, `[135, 225)` et `[225, 315)`. 315° exactement — le haut-gauche
d'un chapeau à huit directions, l'angle que `joystick_poll` calcule pour `SDL_HAT_LEFTUP`
(`wx-sdl2-joystick.c:136-142`) — ne tombe dans aucun, et le chapeau se lit au repos.
`joystick_tm_fcs.c:46-54`, mêmes bornes : 315° tombe sur le `return 0` final, qui est le code du
chapeau en bas (`:50-51`).
*Effet* (mesuré, JOYBANC) : poussé en haut à gauche, le chapeau de la CH est lu au repos (FFh, comme
au tour sans chapeau) et celui de la TM en bas (l'axe 3 compté comme à 180°).
*Trouvé par* : reconnaissance de G10.1.
*Source* : Nerdly Pleasures, « Three Flight Simulator Joysticks for DOS », 2014 (secondaire) : la CH code son chapeau
par quatre combinaisons de boutons ; le vrai chapeau n'a que quatre directions (déduit). 315° vient de l'hôte : ce
qu'il donne est une convention, sans vérité matérielle.
*Cas qui discrimine* : JOYBANC, tours 5 et 10, chapeau injecté à 315° : PCem, la CH au repos et la TM à 0 (en bas) ;
« haut » (`>= 315`), les boutons 1 à 4 et −32768 ; « gauche » (le précédent `<= 315`), les boutons 1 et 2 et 16384.
*G13* : (a) — sur une convention à trancher (« haut » ou « gauche ») ; du périphérique, sous l'interrupteur seul.
*Reproduit* en mode PCem : `Joystick/joystick_ch_flightstick_pro.cs`, `Joystick/joystick_tm_fcs.cs`, marqueurs `fixed
in hardware mode: PB-103` ; montré par `bd-pc-joy-ch-banc` et `bd-pc-joy-tm-banc` (tours 5 et 10, le chapeau injecté
à 315°). Contrôle négatif : la borne de la TM corrigée (`<= 315`) fait rougir `bd-pc-joy-tm-banc`.
*Corrigé en mode matériel* (G13.4) : la convention retenue est « haut » — chacune des trois autres diagonales se lit à
la direction suivante dans le sens des aiguilles d'une montre (45° à droite, 135° en bas, 225° à gauche), 315° donc
en haut. Sous `if (materiel.pb_103)`, 315° se lit en haut (`ch_haut_materiel`, `tm_haut_materiel`, dans les
`*.Materiel.cs` des deux manettes). `materiel-cas PB-103` (0°, le témoin ; 315° : la CH F0h contre 00h, la TM 0 contre
-32768) rougit la correction coupée. JOYBANC en C# seul, le chapeau tenu à 315° (`banc-joybanc-ch`, `-tm`) : la CH lit
0Fh au lieu de FFh, la TM compte 01h sur l'axe 3 au lieu de 0Fh. Le choix se renverse sans peine : « gauche » serait
la borne `<= 315` de l'intervalle précédent.

### PB-104 — Sans correspondance explicite, le chapeau se calcule sur un axe contre lui-même

`pc.c:800-803` : le défaut des clés `joystick_N_pov_D_x` et `_y` est `d`, l'indice du chapeau, en X
comme en Y, sans `POV_X` ni `POV_Y` (`plat-joystick.h:42-43`). `joystick_get_axis`
(`wx-sdl2-joystick.c:63-95`) lit alors l'AXE d de la manette de l'hôte, pas son chapeau d, pour X
comme pour Y : le chapeau émulé se calcule sur (axe d, axe d), au mieux une diagonale, et le chapeau
de la manette de l'hôte est ignoré.
*Effet* (mesuré, `iXtal26 --joystick-check`, une manette virtuelle de SDL3) : sans correspondance
du chapeau dans le .cfg, le manche poussé à droite lit 135° (le chapeau étant en haut), à gauche
315°, là où PB-103 tombe. Avec `POV_X|0` et `POV_Y|0` explicites, le chapeau est lu juste (90° à
droite, 315° en haut à gauche). Propre à l'hôte : l'oracle n'a pas de manette hôte, et les portes
injectent l'état de la manette émulée.
*Trouvé par* : reconnaissance de G10.1.
*Source* : sans objet matériel — une correspondance de l'hôte, que l'oracle n'exécute jamais (`pc.c:790` exige une
manette d'hôte) ; la valeur attendue, le chapeau de la manette (`POV_X | d`, `POV_Y | d`), est une convention.
*Cas qui discrimine* : `--joystick-check`, cas 2 (aucune correspondance au .cfg) : PCem, le manche à droite et le
chapeau en haut lisent 135°, le manche à gauche 315° ; avec `POV_X | d` et `POV_Y | d`, 0° et −1.
*G13* : (d) — défaut de l'hôte sans pendant matériel, qui ne protège ni fichier ni hôte : laissé (décision n° 9).
*Reproduit* : `pc.cs`, `load_joysticks`, marqueurs PB-104 (`:674`, l'en-tête ; `:707`, les deux défauts) ; une
valeur explicite du .cfg est lue telle quelle, comme chez PCem. *Correction envisagée avant G13* : par défaut
`POV_X | d` et `POV_Y | d`, le chapeau d de la manette de l'hôte. La contre-lecture de G13 l'a sortie du mode
matériel (aucune vérité matérielle), et la décision n° 9 de `PLAN-G13.md` la laisse dans les deux modes.

### PB-106 — Le mode 2 à 2 336 octets est lu 16 octets trop loin

`cdrom_image.cpp:237-238` (`CanReadPVD`) et `:180-181` (`ReadSector`) : `if (mode2) seek += 24` —
juste pour un secteur brut de 2 352 octets en mode 2 (12 de synchronisation, 4 d'en-tête, 8 de
sous-en-tête), faux pour un secteur de 2 336 octets, qui commence au sous-en-tête : ses données
sont à +8.
*Effet* : une image MODE2/2336 n'est jamais reconnue (`LoadIsoFile` cherche le PVD 16 octets trop
loin) ; une piste MODE2/2336 d'une feuille CUE se lit 16 octets trop loin — son secteur 16 rend le
PVD à partir de son 16e octet. Mesuré par `cdimage-check` (constats, `iso-2336-mode2.bin`,
`formats.cue`).
*Trouvé par* : reconnaissance de G10.3.
*Source* : documenté — SFF-8020i r2.6 (22 janvier 1996), Figure 11 : un secteur XA mode 2 forme 1 porte 12 octets de
synchronisation, 4 d'en-tête et 8 de sous-en-tête avant ses 2 048 octets de données ; sans synchronisation ni
en-tête (2 336 octets), les données sont à +8. Figure non revérifiée en contre-lecture.
*Cas qui discrimine* : `iso-2336-mode2.bin` (isogen) : PCem refuse l'image ; le matériel la lit, et son secteur 16
rend `01 « CD001 » 01`. `formats.cue`, la piste MODE2/2336 : son secteur 16 commence chez PCem au 16e octet du PVD,
sur le matériel à son octet 0.
*G13* : (a).
*Reproduit* : `Cdrom/cdrom_image.cs`, `CanReadPVD` et `ReadSector`, marqueurs PB-106. Contrôles
négatifs : chacun des deux décalages corrigé (+8) rougit la porte.

### PB-107 — Une image de plus de 2 Gio prend une longueur tronquée

`cdrom_image.cpp:63-69` : `getLength` rend `(int)file->tellg()`. Au-delà de 2 Gio, la longueur
devient négative (jusqu'à 4 Gio), puis se réduit modulo 4 Gio ; `LoadIsoFile` en tire la longueur
de la piste (`:217`), et le lead-out avec elle.
*Effet* : une ISO de DVD. Entre 2 et 4 Gio, aucun secteur ne se lit (la piste 1 va de 0 à un
lead-out négatif, `GetTrack` ne la trouve jamais) et la capacité est aberrante : 385 025 secteurs
pour 2 684 354 560 octets, mesuré par `cdimage-check` sur une image creuse (`creuse-2g5.iso`).
Au-delà de 4 Gio, seul le reste modulo 4 Gio est vu : un DVD de 4,7 Go apparaîtrait de 405 Mo
(calculé, non mesuré).
*Trouvé par* : reconnaissance de G10.3.
*Source* : déduit — aucun CD ne dépasse la plage MSF (99:59:74, environ 880 Mo) ; une image de plus de 2 Gio est un
DVD, qu'un lecteur de CD-ROM refuse (« incompatible medium »). Aucune norme ne décrit une image de fichier.
*Cas qui discrimine* : `creuse-2g5.iso` (2 684 354 560 octets) : PCem monte 385 025 secteurs et n'en lit aucun ;
attendu : l'image refusée avec un avertissement, le lecteur vide (comme PB-116) ; une image de 700 Mo, inchangée.
*G13* : (b) — une correction d'hôte plus que de fidélité, sans vrai comportement documenté.
*Reproduit* : `Cdrom/cdrom_image.cs`, `getLength`, marqueur PB-107. Contrôle négatif : la taille
bornée au lieu de tronquée rougit la porte.

### PB-108 — INDEX 00 en 00:00:00 ne compte pas

`cdrom_image.cpp:398-403` : `AddTrack` ne retranche le prégap que si `prestart > 0`. Quand une
piste change de fichier (`:426-436`), son début dans le fichier est `skip * sectorSize` : avec
INDEX 00 en 00:00:00, `skip` vaut 0, et la position d'INDEX 01 dans le fichier est oubliée.
*Effet* : la disposition courante des feuilles à plusieurs fichiers — chaque fichier commence par
son prégap, INDEX 00 00:00:00, puis INDEX 01 — lit chaque piste en avance de la longueur de son
prégap : le secteur de son INDEX 01 rend le premier secteur du fichier. Mesuré par
`cdimage-check` (`multi.cue` : la piste 2, INDEX 01 au secteur 5 de son fichier, rend le
secteur 0). À l'écoute (G10.5), chaque piste jouerait d'abord son prégap.
*Trouvé par* : transcription de G10.3.
*Source* : documenté par le format — CDRWIN User's Guide (Golden Hawk Technology), annexe A, repris par libodraw,
*CUE sheet format* § 5.6 — INDEX 00 marque le prégap, INDEX 01 le début de la piste, positions relatives au FILE ;
la TOC rend INDEX 01 (SFF-8020i § 10.8.19). Format non revérifié en contre-lecture.
*Cas qui discrimine* : `multi.cue`, la piste 2 dans son propre fichier (INDEX 00 en 00:00:00, INDEX 01 au secteur
5) : READ du premier secteur de la piste 2 rend chez PCem le secteur 0 du fichier, sur le matériel le secteur 5.
*G13* : (a).
*Reproduit* : `Cdrom/cdrom_image.cs`, `AddTrack`, marqueur PB-108. Contrôle négatif :
`prestart >= 0` rougit la porte.

### PB-109 — Un échec de lecture colle au fichier

`cdrom_image.cpp:57-61` : `BinaryFile::read` fait `seekg` puis `read`, et rend `!fail()`. Un `read`
qui atteint la fin du fichier pose `eofbit` et `failbit` ; `seekg` n'efface que `eofbit` (C++11),
et sa sentinelle refuse ensuite un flux en échec : toute lecture suivante du fichier échoue, sur
toutes les pistes qui le partagent, jusqu'à sa fermeture.
*Effet* : une feuille dont le dernier secteur est incomplet (`AddTrack` en compte un de plus,
« padding », `:429-430`) : lire ce secteur, ou un prégap au-delà de la fin du fichier, rend le
fichier illisible jusqu'à la réouverture de l'image — mesuré par `cdimage-check` (`multi.cue`, la
piste 3). Et une image coupée sept octets après le début du PVD est montée avec une longueur 0 :
`getLength` rend -1 sous `failbit` (`:66-67`, constat de `tronque-pvd.iso`).
*Trouvé par* : reconnaissance de G10.3 (PLAN-G10.md, « l'échec collant »).
*Source* : déduit — un état de la libstdc++, sans analogue matériel ; un vrai lecteur qui échoue sur un secteur lit
le suivant. Aucune norme ne décrit une image de fichier.
*Cas qui discrimine* : `multi.cue`, la piste 3 (dernier secteur incomplet) : après la lecture de ce secteur, PCem
refuse toute relecture de la piste 3 ; attendu : elle réussit. `tronque-pvd.iso` : PCem monte une longueur 0 ;
attendu : la longueur réelle.
*G13* : (a).
*Reproduit* : `Cdrom/cdrom_image.cs`, l'ifstream modélisé d'après la libstdc++ de gcc 15 (bits d'état et
sentinelles) ; marqueurs PB-109 sur `read` et `getLength`, posés en G13.1 (seul l'en-tête le nommait, D3-contre,
C8). Contrôle négatif : `failbit` effacé par `seekg` rougit la porte.

### PB-110 — Une feuille CUE dont des pistes précèdent tout FILE fait tomber l'émulateur

`cdrom_image.cpp:183` : `ReadSector` appelle `tracks[track].file->read` sans regarder le
fichier, nul pour une piste qui précède tout FILE. `:427` : `AddTrack`, devant une piste d'un
autre fichier que la précédente, appelle `prev.file->getLength()`, nul si la précédente précède
tout FILE. Le parseur n'a pas d'autre chemin vers un fichier nul : `ClearTracks` (`:550-551`) et
la branche FILE en échec (`:358-361`) ne font que `delete`, d'un pointeur nul ou d'un fichier
qu'aucune piste ne tient. Et `cdrom-image.cc:465` : `strcpy(image_path, fn)` dans
`char image_path[1024]`, sans borne.
*Effet* : une feuille dont les pistes n'ont aucun FILE est montée, puis le premier secteur lu
dans l'une d'elles — READ, READ CD brut ou le rappel audio — fait tomber l'hôte ; une piste avant
le premier FILE suivie d'une piste qui en a un le fait tomber pendant `image_open`. Un chemin
d'image de 1 024 octets ou plus déborde `image_path`. La feuille et le chemin sont des données de
l'utilisateur, comme un .cfg.
*Trouvé par* : transcription de G10.3.
*G13* : hors du mode — déjà corrigé dans les deux modes (R9) ; rien n'y bascule.
*Non reproduit* (R9, décision n° 4 de G10.3) : `Cdrom/cdrom_image.cs`, marqueurs `pcem bug, not
reproduced` — la lecture échoue, la feuille fautive est refusée ; `r9-cue` (C# seul) rougit en
nommant l'exception si l'on retire une garde. `image_path` est une chaîne C#. L'oracle ne reçoit
jamais ces feuilles.
*G10.4, la clé `cdrom_path`* (`pc.c:707-711`, `strcpy` dans le même `image_path`) : au-delà de
1 023 octets, le chemin est écarté avec un avertissement et le lecteur reste vide (`pc.cs`), prouvé
par `r9-cdcfg`.

### PB-113 — Le pont ATAPI s'arrête sur dix-neuf fatal(), dont un à la portée de l'invité

`ide_atapi.c` : dix-sept `fatal()` vivants — la sélection (`:102`, `:106`, `:110`) et la machine
d'états du paquet (`:157`, `:159`, `:164`, `:173`, `:205`, `:221`, `:281`, `:296`, `:299`, `:324`,
`:346`, `:362`, `:416`, `:453`) — et deux dans `scsi.c` (`:85`, `:264`). `atapi_command_start`
suppose le bus au repos ; or DEVICE RESET (08h), la reprise ordinaire d'un pilote ATAPI, ne remet
pas le pont à zéro (`ide.c:820-830` n'appelle pas `atapi_reset`), et un PACKET envoyé pendant une
phase en cours trouve le bus occupé.
*Effet* : un pilote qui abandonne un transfert, ou qui reprend par DEVICE RESET puis renvoie un
PACKET, fait tomber l'hôte en `:110` (mesuré par `r9-atapi`, la garde retirée). Les dix-huit autres
sites — `:102` et `:106` dans la sélection, les quatorze de la machine d'états, les deux de `scsi.c`
— vérifient l'accord du pont et de `scsi_cd`, deux codes déterministes : aucune séquence de
l'invité ne les atteint (relecture, VERIFICATION.md § G10.4).
*Trouvé par* : reconnaissance de G10.4.
*G13* : hors du mode — déjà corrigé dans les deux modes (R9) ; rien n'y bascule.
*Non reproduit* (R9, décision n° 14 de PLAN-G10.md) : `Ide/ide_atapi.cs` et `Scsi/scsi.cs`,
marqueurs `pcem bug, not reproduced: PB-113`. La sélection abandonne la transaction en cours
(`scsi_bus_reset`, qui remet aussi le lecteur) et reprend, comme un vrai lecteur ; un second échec
finit la commande en erreur. Les quatorze sites de la machine d'états finissent la commande par
`atapi_abort` (le pont et le bus au repos, ERR et ABRT, phase d'état, IRQ) ; les deux du bus le
remettent au repos, et le pont abandonne à son tour. DEVICE RESET garde l'état du pont de PCem.
Survie : `r9-atapi`, site par site, par un scénario de l'invité ou un état forgé.

### PB-114 — Un PACKET en DMA, sans bus master, appelle un pointeur nul

`ide_atapi.c:473`, `:482` : les états RETRY_READ_DMA et RETRY_WRITE_DMA appellent
`ide_bus_master_read_data` et `ide_bus_master_write_data` sans les tester. Sans bus master (aucune
machine du dépôt n'a de contrôleur PCI), les deux sont nuls, et `:246-248`, `:395-397` mènent à ces
états dès qu'un PACKET porte le bit DMA (registre de fonctions, bit 0).
*Effet* : un pilote qui tente le DMA fait tomber l'hôte au premier transfert.
*Trouvé par* : reconnaissance de G10.4.
*G13* : hors du mode — déjà corrigé dans les deux modes (R9) ; rien n'y bascule.
*Non reproduit* (R9, décision n° 15) : les deux états réarment leur chronomètre, comme si le DMA
n'avait pas eu lieu — l'invité attend, l'émulateur vit (BSY indéfini, comme WIN_READ_DMA côté
disque, `ide.c:884`). `r9-atapi` : le lecteur reste BSY, puis DEVICE RESET le rend.

### PB-115 — Le lecteur de CD s'arrête, ou lit et écrit hors de ses tampons

`scsi_cd.c:992-993` : MECHANISM STATUS de longueur d'allocation nulle, `fatal()`. `:1576-1581` :
`data_out[262 144]` est écrit, hors du tableau, avant le `fatal()` ; un MODE SELECT de longueur 0
(PB-119) y mène. `:1560-1571` : après un remplissage raté (un READ qui atteint la fin du disque)
ou un `bytes_expected` débordé (`cdlen × 2048` ou `× 2352`, `:1057`, `:1149`), `scsi_cd_read`
passe la fin de `data_in` : `data_out`, les champs du struct, puis hors de l'allocation.
`:1186-1187` : READ(12) de 2^31 secteurs ou plus rend `cdlen` négatif, et `readsector` reçoit un
compte négatif (`new[]` démesuré).
*Effet* : chacune fait tomber l'hôte, à la portée de tout invité.
*Trouvé par* : reconnaissance de G10.4.
*G13* : hors du mode — déjà corrigé dans les deux modes (R9) ; rien n'y bascule.
*Non reproduit* (R9, décision n° 16) : MECHANISM STATUS de longueur 0 rend GOOD sans données ;
au-delà de `data_out`, l'octet est compté sans être gardé ; à la fin de `data_in` (262 144
octets), le transfert s'arrête — un dernier octet nul, puis CHECK CONDITION, ILLEGAL REQUEST /
LBA OUT OF RANGE —, et en deçà les octets périmés de PCem sont rendus ; un compte négatif est un
échec de lecture. Marqueurs PB-115, survie `r9-atapi`.

### PB-116 — Une image illisible au démarrage laisse le pilote CD nul

`pc.c:297-301` (initpc) : avec `cdrom_drive = 200` et une image que `fopen` ouvre mais
qu'`image_open` refuse (vide, sans PVD, un répertoire), `atapi` n'est jamais posé et reste NULL
(`ide_atapi.c:26`). Le premier reset d'un canal IDE appelle `atapi->stop()` (`ide.c:796-812`),
même pour un emplacement vide.
*Effet* : avec un contrôleur IDE, l'hôte tombe au premier reset d'un canal — qu'un lecteur de CD
soit monté ou non.
*Trouvé par* : reconnaissance de G10.4.
*G13* : hors du mode — déjà corrigé dans les deux modes (R9) ; rien n'y bascule.
*Non reproduit* (R9, décision n° 17) : `pc.cs`, `cdrom_image_open` — le lecteur reste vide, comme
pour une image absente (`pc.c:302-305`), avec un avertissement. `r9-cdcfg` (vide.iso, sans-pvd.iso,
un répertoire ; sans la garde, NullReferenceException).

### PB-117 — READ CAPACITY rend le nombre de blocs plus un

`scsi_cd.c:1534-1543` rend `atapi->size()`, la capacité de l'image, qu'`image_open` pose à la fin
plus un (`cdrom-image.cc:475`, constat de G10.3). READ CAPACITY attend l'adresse du dernier bloc :
31 pour une image de 32 secteurs ; PCem rend 33.
*Effet* : un pilote qui lit le dernier bloc annoncé reçoit CHECK CONDITION, ILLEGAL REQUEST / LBA
OUT OF RANGE. ATAPIBANC le montre : READ CAPACITY rend 33, et READ(10) du secteur 32 est refusé.
*Trouvé par* : reconnaissance de G10.3 (le moteur), lu chez l'invité en G10.4.
*Source* : documenté — SFF-8020i r2.6, modèle CD-ROM, note sur READ CD-ROM CAPACITY (« returns the logical block
address of the last block prior to the lead-out area ») ; SCSI-2 § 9.2.7 (non revérifié en contre-lecture).
*Cas qui discrimine* : ATAPIBANC, image de 32 secteurs : READ CAPACITY rend 00000021h (33) chez PCem, 0000001Fh (31)
sur le matériel, 2 048 octets par bloc ; READ(10) du LBA 31 : GOOD des deux côtés.
*G13* : (a).
*Reproduit* : `Scsi/scsi_cd.cs`, READ CAPACITY, et `Cdrom/cdrom-image.cs`, `image_open` (la capacité plus un),
marqueurs PB-117. Contrôle négatif : la capacité moins un rougit ATAPIBANC ; la valeur juste est la capacité moins
deux, 31 (D3-contre, C10).

### PB-118 — GET EVENT STATUS NOTIFICATION annonce toujours un nouveau disque

`scsi_cd.c:579-606` (`atapi_event_status`) lit `buffer[4]` et `buffer[5]` avant de les écrire ; or
`scsi_cd_start_command` les a mis à zéro (`:1606`) : « disque présent », « nouveau disque »
(MEC_NEW_MEDIA) et `atapi->load()` à chaque appel. La longueur de l'en-tête est un `uint16_t` de
l'hôte (`:1336`), petit-boutiste là où MMC veut l'ordre réseau ; la longueur d'allocation est lue sur
trois octets, l'octet de contrôle compris (`:1274`).
*Effet* : un pilote qui sonde le lecteur par GESN y voit un disque neuf à chaque appel — même un
lecteur vide —, et lit la longueur 4 comme 1 024. ATAPIBANC, deux appels : `04 00 04 10 02 02 00 00`
les deux fois, lecteur chargé comme vide.
*Trouvé par* : reconnaissance de G10.4 (PLAN-G10.md, « toujours NEW_MEDIA »).
*Source* : SFF-8020i r2.6, Table 37 : 4Ah n'y est pas (documenté), d'où 05/20h en 1996 (déduit). MMC-2
(T10/97-108r0) § 9.1.2 : longueur 0006h en ordre réseau, un événement rapporté une fois, 05/24h si IMMED = 0,
filtrage par classe, sans UNIT ATTENTION (documenté). Référence à choisir (Q5 de D3-stockage).
*Cas qui discrimine* : deux GESN (polled, classe média), disque chargé : PCem `04 00 04 10 02 02 00 00` les deux
fois ; MMC-2 `00 06 04 10 02 02 00 00` puis `00 06 04 10 00 02 00 00` ; lecteur vide : PCem inchangé, MMC-2
`00 06 04 10 00 00 00 00` ; SFF-8020i : CHECK CONDITION, 05/20h.
*G13* : (b) — corrigeable, mais la référence (SFF-8020i ou MMC-2) n'est pas tranchée.
*Reproduit* : `Scsi/scsi_cd.cs`, marqueurs PB-118. Contrôle négatif : la longueur en ordre réseau rougit ATAPIBANC.

### PB-119 — MODE SELECT lit son en-tête de travers et ne finit pas à longueur nulle

`scsi_cd.c:755-757` : `cdrom_mode_select` prend la longueur des données du mode (octets 0 et 1)
pour celle du descripteur de bloc, et compte huit octets d'en-tête même en MODE SELECT(6)
(`prefix_len` est posé, jamais lu). `:1250-1263` : de longueur 0, `bytes_required` vaut 0 et
`scsi_cd_write_complete` ne devient jamais vrai : la phase de données ne finit pas (DRQ, compte
d'octets 0, puis FFFEh), chaque mot écrit en demandant d'autres.
*Effet* : une page envoyée par MODE SELECT est lue au mauvais endroit ; un MODE SELECT vide laisse
le lecteur en phase de données, jusqu'au reset — et mène à PB-115.
*Trouvé par* : reconnaissance de G10.4.
*Source* : documenté — SFF-8020i r2.6 § 10.8.4 (« A parameter list length of zero indicates that no data shall be
transferred » ; une liste qui tronque l'en-tête ou une page : 05/1Ah) ; SCSI-2 Tables 91 et 92 (en-tête(6) de 4
octets, en-tête(10) de 8). MODE SELECT(6) relève de Q5 de D3-stockage.
*Cas qui discrimine* : ATAPIBANC : MODE SELECT(6) de la page audio 0Eh : PCem cherche la page quatre octets trop
loin et refuse (05/24h) ; le matériel l'accepte, et MODE SENSE la relit. MODE SELECT(10) de longueur 0 : PCem reste
en phase de données (DRQ, compte 0 puis FFFEh) ; le matériel rend GOOD sans transfert.
*G13* : (a).
*Reproduit* : `Scsi/scsi_cd.cs`, marqueurs PB-119. ATAPIBANC le montre : MODE SELECT(10) de la page audio est relu à
l'identique par MODE SENSE(10) ; MODE SELECT(6) de la même page prend ses vingt octets, puis est refusé (ILLEGAL
REQUEST, 24h) : la page est cherchée quatre octets trop loin. La longueur 0, par `r9-atapi`.

### PB-120 — La TOC brute n'a ni lead-out ni longueur

`scsi_cd.c:1026-1028` et `cdrom-image.cc:383-425` : READ TOC au format 2 ne rend qu'une entrée par piste — ni les
points A0h, A1h, A2h, ni le lead-out —, et la longueur, `data_in[0..1]`, reste à zéro. Chaque entrée porte en outre
le numéro de piste dans l'octet de session et 0 dans POINT (`cdrom-image.cc:412-415`), là où le format 10b attend la
session, puis la piste en POINT (relevé en G13.1, lu au code).
*Effet* : un pilote qui lit la TOC brute y trouve une longueur nulle et aucune fin de disque.
*Trouvé par* : reconnaissance de G10.3 (le moteur), lu au niveau de la commande en G10.4.
*Source* : documenté — SFF-8020i r2.6 § 10.8.19, format 10b — « the drive will support Q Subcode Point field values
of A0h, A1h, A2h », et « The first TOC entries shall be the A0, A1, A2h pointers » ; Table 131 (le « Disc Type
Byte » : 00h, 10h, 20h) ; la longueur des données renseignée.
*Cas qui discrimine* : READ TOC format 2 de `iso-2048.iso` (une piste de données, 32 secteurs) : PCem rend
`00 00 01 01` et `01 14 00 00 00 00 00 00 00 02 00` ; le matériel `00 2E 01 01`, puis A0h (piste 1, type 00h), A1h
(piste 1), A2h (00:02:32) et `01 14 00 01 00 00 00 00 00 02 00`.
*G13* : (a).
*Reproduit* : `Scsi/scsi_cd.cs` (READ TOC) et `Cdrom/cdrom-image.cs` (`image_readtoc_raw`), marqueurs PB-120 ; le
moteur est comparé des deux côtés par `cdimage-check`. ATAPIBANC ne lit pas la TOC brute (PLAN-G10.md, « Les
risques »).

### PB-122 — Le lead-out de la TOC n'a ni ADR ni contrôle

`dosbox/cdrom_image.cpp:223` (l'ISO) et `:385` (la feuille CUE) : la piste du lead-out reçoit
`attr = 0`, que `image_readtoc` recopie dans l'octet ADR/contrôle de son descripteur
(`cdrom-image.cc:309`). Un vrai lecteur y met ADR 1 et le contrôle de la dernière piste : 14h pour
un disque de données.
*Effet* : READ TOC rend `00 00 AA 00` en tête du descripteur du lead-out, aux formats LBA et MSF.
Seule son adresse sert aux pilotes connus.
*Trouvé par* : les relevés d'ATAPIBANC, G10.4.
*Source* : SFF-8020i r2.6 § 10.8.19 (« The ADR field gives the type of information encoded in the Q sub-channel of
the block where this TOC entry was found ») et Table 131 : le point A2h, entrée Q du lead-in, porte ADR 1 et le
contrôle 4 ou 6 d'un disque de données — ADR 1 documenté, le contrôle de la dernière piste déduit.
*Cas qui discrimine* : ATAPIBANC, READ TOC (format 0, LBA) de `iso-2048.iso` : le descripteur du lead-out vaut
`00 00 AA 00 00 00 00 20` chez PCem, `00 14 AA 00 00 00 00 20` sur le matériel.
*G13* : (a).
*Reproduit* : `Cdrom/cdrom_image.cs`, marqueurs PB-122 ; le moteur est comparé des deux côtés par
`cdimage-check`, la commande par ATAPIBANC.

### PB-123 — L'audio CD en LBA confond l'adresse et la position du lecteur, à 150 secteurs près

`cdrom-image.cc:95-110` : en LBA, `image_playaudio` range l'adresse reçue telle quelle dans
`image_cd_pos` et la fin dans `image_cd_end`, alors que ces deux positions comptent les 150
secteurs de l'amorce (`image_audio_callback` lit `image_cd_pos - 150`, `:36`) ; une position sous
150 est ramenée à 150. `image_seek` (`:138`) range de même une adresse LBA comme une position.
Mais `image_is_track_audio` (`:58-75`), que `scsi_cd.c:1375-1376` consulte avant de jouer, prend
l'adresse LBA sans décalage. Et READ SUB-CHANNEL (`cdrom-image.cc:200-249`) rend des positions
où `GetAudioSub` a ajouté 150 (`dosbox/cdrom_image.cpp:121-122`) : juste pour la position absolue en
MSF, fausse pour la position absolue en LBA et pour la position relative, en LBA comme en MSF.
*Effet* : en LBA, aucune adresse ne joue la bonne piste. L'adresse de READ TOC passe le contrôle mais joue 150
secteurs trop tôt, ou rien : la piste 2 de `mixte.cue` (LBA 42, 30 secteurs) part de 150 et finit à 72 — rien ne
joue. L'adresse plus 150 tombe hors de toute piste et le contrôle la refuse (PLAY AUDIO(12) en 372, pour la piste 3
en 222 : ILLEGAL REQUEST). Seul PLAY AUDIO MSF joue à la bonne position ; ses deux contrôles de piste ont leurs
propres défauts (PB-214, PB-215). READ SUB-CHANNEL rend, huit secteurs après le début de la piste 2,
la position relative 00:02:08 au lieu de 00:00:08, et, après RESUME, en LBA 207 et 165 au lieu de 57 et 15
(`VERIFICATION.md` § G10.5).
*Trouvé par* : reconnaissance de G10.3, au niveau du moteur ; montré par ATAPIAUD en G10.5.
*Source* : documenté — SFF-8020i r2.6 § 10.8.8 (« PLAY AUDIO commands with a starting LBA address of 0000 0000h
shall begin the audio play operation at 00m 02s 00f ») et § 10.8.18, Table 115 (l'adresse absolue, et la relative au
début de la piste). PLAY AUDIO(12) manque à la Table 37 : il relève de Q5 de D3-stockage (D3-contre, K17).
*Cas qui discrimine* : `mixte.cue`, piste 2 audio en LBA 42 (30 secteurs) : PLAY AUDIO(10) depuis 42 : PCem part de
150 et s'arrête à 72, rien ne joue ; le matériel joue depuis 00:02:42. READ SUB-CHANNEL huit secteurs plus loin :
relatif 00:02:08 chez PCem, 00:00:08 ; en LBA, quinze secteurs plus loin : 207 et 165 chez PCem, 57 et 15.
*G13* : (a) pour le LBA et le sous-canal ; PLAY AUDIO(12) selon Q5 de D3-stockage.
*Reproduit* : `Cdrom/cdrom-image.cs` (`image_playaudio`, `image_seek`, le sous-canal en LBA) et
`Cdrom/cdrom_image.cs` (`GetAudioSub`), marqueurs PB-123 ; comparé des deux côtés par `cdimage-check` et par
`bd-ami486-atapi-audio`.

### PB-125 — Le lecteur ZIP écrit et lit hors de ses tampons, et hors de l'image

`scsi_zip.c:209` : `scsi_add_data` écrit `data_in[data_pos_write++]` sans borne ; READ(6) ou READ(10) de plus
de 512 secteurs déborde les 256 Ko de `data_in`, dans `data_out` puis hors de la structure. `:979` :
`scsi_zip_read` lit `data_in` sans borne ; une lecture de longueur nulle ne finit jamais (PB-126), et
l'invité qui insiste lit hors de la structure. `:985-990` : `scsi_zip_write` écrit `data_out[262 144]`
puis `fatal("Exceeded data_out buffer size\n")` ; `:222-224` : `scsi_get_data` lit au-delà puis
`fatal("scsi_get_data beyond buffer limits\n")` — WRITE de plus de 512 secteurs. Et `hdd_file.c:174-212`
: un secteur au-delà de la fin de l'image (offset > sectors) donne un `transfer_sectors` négatif, une
taille énorme pour `fread` et `fwrite`, qui débordent `buf` ; un LBA de 2^31 ou plus, que le ZIP prend de
l'invité sur 32 bits, donne un offset négatif : `fseeko64` échoue et la lecture ou l'écriture part de la
position courante, avec la même taille.
*Effet* : un pilote ou un programme qui envoie un READ, un WRITE ou un secteur hors de l'image fait
tomber l'émulateur, ou écrit n'importe où dans l'image.
*Trouvé par* : reconnaissance de G10.6.
*G13* : hors du mode — déjà corrigé dans les deux modes (R9) ; rien n'y bascule.
*NON reproduit* (R9) : `DEVIATION` dans `Scsi/scsi_zip.cs` — l'octet au-delà d'un tampon est compté,
pas gardé, et se relit nul — et dans `Disc/hdd_file.cs` — hors de l'image ou à un offset négatif, rien
n'est lu ni écrit, le retour est 1. Survie : `r9-zip`, sept essais, chaque garde atteinte.
*Amendé en G11* : sous la glibc, `hdd_file.c` avec un `transfer_sectors` négatif ne plante pas toujours —
`fread` rend EFAULT sans rien lire ; `fwrite` pousse jusqu'à 4 Ko du struct au-delà de la capacité ; un offset
négatif à `transfer_sectors` = 1 lit ou écrit à la position courante du `FILE`. Ces cas dépendent de la libc et
ne se transcrivent pas : la garde reste, et ils sont tenus hors des portes comparées.

### PB-126 — Le lecteur ZIP : la capacité, la fin du disque, l'éjection et les phases sans fin

`scsi_zip.c:607-619` : READ CAPACITY rend le nombre de blocs, 196 608, au lieu du dernier LBA, 196 607.
`hdd_file.c:174-180` : le secteur 196 608, juste après la fin, se lit sans erreur : `transfer_sectors`
vaut 0, rien n'est lu, et `buf` garde le dernier secteur lu. `:885-891` : START STOP UNIT éjecte le disque
quand START et LOEJ valent 0 (« arrêter le moteur ») et le garde quand LOEJ vaut 1 (« éjecter ») — à
rebours de la norme, d'après le commentaire de PCem pour les pilotes Iomega de Windows 9x. `:165-177` :
chaque `resetide` alloue un `scsi_zip_data` neuf, `disc_loaded = 0` : un reset matériel perd le disque.
Une lecture de longueur nulle (READ(10) d'un compte 0) entre en phase de données sans fin
(`scsi_zip_read_complete`, `:993-996`, compare des positions que la première lecture désaccorde) ; MODE
SELECT(6) de longueur 0 de même en phase sortante (`:860-870`, `scsi.c:157-161`).
*Effet* : un pilote qui lit la capacité voit un secteur de trop, et qui le lit reçoit l'ancien
contenu ; une éjection à la norme n'éjecte pas ; un reset matériel vide le lecteur ; une commande de
longueur nulle ne finit pas.
*Trouvé par* : reconnaissance de G10.6 ; montré par ZIPBANC.
*Source* : documenté — INF-8070i r1.2, Table 41 (« the last valid LBA ») ; SCSI-2 § 9.1.2 (hors capacité : 05/21h),
§ 9.2.6 et § 8.2.8 (longueur nulle : ni transfert ni erreur). Le disque gardé au reset, puis 06/29h (Table 61) :
déduit. START STOP UNIT : la norme éjecte sur LoEj 1 / Start 0 (Table 59) ; le ZIP d'Iomega : inconnu.
*Cas qui discrimine* : ZIPBANC, disque de 196 608 secteurs : READ CAPACITY 196 608 chez PCem, 196 607 sur le
matériel ; READ(10) du LBA 196 608 : GOOD et l'ancien tampon, contre 05/21h ; READ(10) de compte 0 : une phase sans
fin, contre GOOD ; reset matériel puis TEST UNIT READY : lecteur vide, contre 06/29h puis GOOD.
*G13* : (a), START STOP UNIT (c) — inconnu sur le lecteur d'Iomega : à mesurer sur un ZIP 100 ATAPI.
*Reproduit* : `Scsi/scsi_zip.cs`, marqueurs PB-126 ; comparé des deux côtés par `bd-ami486-zip-banc`.

### PB-128 — Le disque SCSI écrit et lit hors de ses tampons

`scsi_hd.c:87` : `scsi_add_data` écrit `data_in[data_pos_write++]` sans borne. Un READ(10) de plus de 512
secteurs (jusqu'à 65 535, `:505`) déborde les 256 Ko de `data_in` dans `data_out` : de 513 à 1 024 secteurs, sans
dommage, l'invité relisant les bonnes données (`:717`) ; au-delà de 2 × 256 Ko, le bourrage, `data_pos_read`,
`data_pos_write` (qui s'écrase lui-même), `hdd.f`, le chronomètre, puis le tas. `:717` : `scsi_hd_read` lit
`data_in` sans borne ; une phase DATA IN vide (READ(10) de compte 0, allocation 0, PB-132) fait lire l'AHA
jusqu'à la longueur de son CCB, 16 Mo, au-delà du struct. `:723-728` : `scsi_hd_write` écrit `data_out[262 144]`
(le bourrage) puis `fatal("Exceeded data_out buffer size\n")` ; `:99-102` : `scsi_get_data` de même.
*Effet* : un pilote ASPI qui lit plus de 1 024 secteurs d'un coup, ou une phase vide, fait tomber l'émulateur.
*Trouvé par* : reconnaissance de G11.
*Source* : documenté — SCSI-2 § 9.2.6, READ(10) transfère la longueur demandée (jusqu'à 65 535 blocs) ; l'AHA-1540C
transfère ce que la cible rend, dans la limite du CCB (TR, CCB). Rien de matériel ne borne à 512 Ko.
*Cas qui discrimine* : CCB READ(10) de 2 048 secteurs depuis le LBA 0, disque d'au moins 2 048 secteurs : iXtal (la
garde R9) rend les 1 024 premiers justes, puis des zéros ; PCem écrit hors du struct ; le matériel rend les 2 048
secteurs.
*G13* : à trancher — corrigeable (servir le transfert par morceaux au-delà de 512 Ko), hors de la liste de G13.7 :
le repli R9 du mode matériel est la question Q12 de D3-stockage, ouverte.
*Reproduit jusqu'à 2 × 256 Ko* : `data_in` et `data_out` en un tableau contigu (décision n° 9 de PLAN-G11.md),
marqueur PB-128 sur `io`.
*NON reproduit au-delà* (R9) : l'octet est compté, pas gardé, et se relit nul (`Scsi/scsi_hd.cs`, `:87`, `:717`,
`:101`, `:728`). Survie : `r9-scsihd`.

### PB-129 — Le disque SCSI annonce un secteur de trop, et le lit sans erreur

`scsi_hd.c:426-437` : READ CAPACITY rend `hdd.sectors` au lieu du dernier LBA. `hdd_file.c:173-180` : ce
secteur se lit sans erreur, `transfer_sectors` vaut 0, rien n'est lu, et `buf` — commun à la lecture et à
l'écriture (`:467`, `:521`, `:583`, `:636`) — garde le dernier secteur lu OU ÉCRIT ; statut GOOD. Aucune
commande ne vérifie le LBA (ni READ, ni WRITE, ni VERIFY : pas de 05/21h).
*Effet* : un pilote voit un secteur de plus ; le lire rend un contenu périmé. La traduction de la ROM
(64 × 32) ne l'atteint pas sur une capacité multiple de 2 048.
*Trouvé par* : reconnaissance de G11.
*Source* : documenté — SCSI-2 § 9.2.7 (READ CAPACITY : l'adresse du dernier bloc), § 9.1.2 (hors capacité : CHECK
CONDITION), ASC 21h, LOGICAL BLOCK ADDRESS OUT OF RANGE (§ 8.2.14) — sections non revérifiées en contre-lecture.
*Cas qui discrimine* : disque de n secteurs (40 960, celui d'`aha-format`) : READ CAPACITY rend n chez PCem, n − 1
sur le matériel ; READ(10) du LBA n : GOOD et le dernier secteur lu ou écrit, contre CHECK CONDITION, sense 05/21h.
*G13* : (a) ; l'UNIT ATTENTION de la mise sous tension (§ 7.9) touchera les témoins (D3-contre, A10).
*Reproduit* : `Scsi/scsi_hd.cs`, READ CAPACITY, READ(6), READ(10), WRITE(6) et WRITE(10), marqueurs PB-129 posés en
G13.1 (seul l'en-tête le nommait, D3-contre, C8).

### PB-130 — MODE SENSE du disque SCSI : en-tête, descripteur, bourrage, géométrie fixe

`scsi_hd.c:314-327` : l'en-tête vaut 00 00 08 00, le 08h dans l'octet du paramètre propre au périphérique, la
longueur du descripteur à 0 alors que 8 octets de descripteur suivent (`sectors >> 24` dans l'octet de
densité, un nombre de blocs sur 24 bits). `:410-412` : `for (; len >= 0; len--) add_data_len(0)` décrémente
`len` pendant que `i` monte : min(i0, L) + ⌈(L − i0)/2⌉ octets au lieu de L (L = 255 : 134 octets pour la
page 00h, 146 pour 03h, 04h ou 30h, 170 pour 3Fh). `:329-389` : géométrie fixe (256 secteurs par piste, 4 096
cylindres, 64 têtes), page 30h « PCEM ». PC et DBD ignorés ; une page inconnue rend GOOD.
*Effet* : un utilitaire qui lit les pages de mode lit des zéros pour des pages et une géométrie sans
rapport avec READ CAPACITY.
*Trouvé par* : reconnaissance de G11.
*Source* : documenté — SCSI-2 § 8.3.3, Tables 91 (en-tête(6), longueur du descripteur = 8) et 93 (densité, blocs sur
3 octets, longueur de bloc) ; § 9.3.3 (paramètre propre : WP et DPOFUA) ; § 8.2.10 (DBD, PC) ; une page inconnue :
05/24h. Non revérifié en contre-lecture. La géométrie d'un disque virtuel est un choix.
*Cas qui discrimine* : MODE SENSE(6), page 03h, allocation 255, disque de 40 960 secteurs : PCem rend 146 octets,
en-tête `91 00 08 00` ; le matériel 36 (4 + 8 + 24), en-tête `23 00 00 08` ; DBD = 1 : le descripteur reste chez
PCem ; page 05h : GOOD contre 05/24h.
*G13* : (a) — la géométrie : un choix, pas une inconnue.
*Reproduit* : `Scsi/scsi_hd.cs`, MODE SENSE(6) (l'en-tête, les pages, le bourrage), marqueurs PB-130 posés en G13.1.

### PB-131 — REQUEST SENSE, INQUIRY et les refus du disque SCSI

`scsi_hd.c:148-186` : sense fixe de 18 octets à longueur additionnelle 0 (`:160`), sans bit Valid ; le format
descripteur choisi par `cdb[1]` bit 0 ; la sense n'est effacée que par REQUEST SENSE (`:182`) et persiste
d'une commande à l'autre. `:188-305` : INQUIRY de 96 octets, version 0, longueur additionnelle 0, CmdQue
annoncé, EVPD ignoré. `:107-112`, `:692-708` : tous les refus portent 05/25h (LOGICAL UNIT NOT SUPPORTED), un
code inconnu compris. `:656-675` : FORMAT UNIT, MODE SELECT et VERIFY sont simulés (rien n'est effacé ni lu).
*Effet* : une erreur ancienne se relit plus tard ; un pilote prend un code inconnu pour un problème de LUN ;
un formatage de bas niveau est instantané et garde les données.
*Trouvé par* : reconnaissance de G11.
*Source* : documenté — SCSI-2 § 8.2.14, Table 65 (longueur additionnelle n − 7, soit 10 ; le bit Valid) ; § 7.6
(effacé par la commande suivante) ; § 8.2.5.1, Tables 45 et 48 (version 2, longueur n − 4, CmdQue à 0) ; 05/20h pour
un code inconnu, 05/25h pour un LUN (§ 7.5.3). FORMAT UNIT : le sort des données, propre au fabricant — inconnu.
*Cas qui discrimine* : un code inconnu (0Eh), TEST UNIT READY, REQUEST SENSE : 05/25h chez PCem, 00/00h sur le
matériel (05/20h si REQUEST SENSE suit le code) ; INQUIRY de 96 octets : `00 00 00 02 00 00 00 02` chez PCem,
`00 00 02 02 5B 00 00 00` ; REQUEST SENSE : longueur additionnelle 00h contre 0Ah.
*G13* : (a), FORMAT UNIT (c) — le sort des données est propre au fabricant : laissé simulé.
*Reproduit* : `Scsi/scsi_hd.cs` (`scsi_hd_illegal`, REQUEST SENSE, INQUIRY, VERIFY, MODE SELECT, FORMAT UNIT),
marqueurs PB-131 posés en G13.1.

### PB-132 — Une phase vide fige le bus SCSI, et aucun reset de la carte ne le libère

`scsi_hd.c:150`, `:189`, `:312`, `:505` avec `scsi.c:241` et `scsi_hd.c:731-735` : READ(10) de compte 0, REQUEST
SENSE, INQUIRY ou MODE SENSE(6) d'allocation 0 entrent en DATA IN, et la première lecture rend `read_complete` faux
pour toujours ; MODE SELECT(6) de longueur 0 de même en DATA OUT. L'AHA lit ou écrit jusqu'à la fin du CCB, puis
fait le va-et-vient NEXT_PHASE ↔ READ_DATA à chaque échéance (`scsi_aha1540.c:1832-1910`). Même blocage quand la
cible a plus de données que le CCB. Et les resets de la carte (CTRL_RESET, SRST, BRST, `:276-313`) ne touchent pas
le bus : aucun appel à `scsi_bus_reset` dans `scsi_aha1540.c`. La cible reste BSY ; toute sélection suivante, vers
n'importe quel ID, échoue (`wait_for_bus`, `:1721`) ; si elle était en phase de commande, la CDB suivante va à
l'ANCIEN disque.
*Effet* : la commande ne finit jamais, et tous les disques SCSI disparaissent jusqu'au reset matériel du PC.
*Trouvé par* : reconnaissance de G11.
*Source* : documenté — SCSI-2 §§ 7.2.4, 7.2.6, 8.2.8, 9.2.6 (longueur nulle : aucun transfert, la cible passe au
statut) ; AHA-1540C TR, port de contrôle : HRST reset le bus et la carte ; SCRST, reset « soft », laisse continuer
les opérations ; statut d'hôte 12h (« more data than was allocated »). Un CCB en vol sous SCRST : inconnu.
*Cas qui discrimine* : CCB READ(10) de compte 0 : PCem ne poste aucun MBI et fait la navette ; le matériel rend GOOD
sans données. Cible bloquée à dessein, puis HRST et un CCB vers l'ID 0 : 11h (sélection) chez PCem ; sur le
matériel, le bus libéré, UNIT ATTENTION puis GOOD.
*G13* : (a) côté cible et pour HRST ; le CCB en vol sous SCRST (c) — son sort après le reset du bus est inconnu.
*Reproduit* : `Scsi/scsi_hd.cs`, `Scsi/scsi.cs` et `Scsi/scsi_aha1540.cs`, marqueurs PB-132 ; hors des portes
comparées (les bancs n'y mènent pas), montré par `r9-scsihd`.

### PB-133 — Le LUN du CCB n'atteint jamais le disque

`scsi_aha1540.c:422`, `:440` : le LUN est lu dans le CCB, mais il n'y a ni phase MESSAGE OUT ni message
IDENTIFY ; seul le sense automatique le pose dans la CDB (`:1603`). `scsi_hd.c:131` ne connaît le LUN que
par `cdb[1]` bits 5-7.
*Effet* : un pilote qui ne met le LUN que dans le CCB voit le disque répondre sur ses huit LUN.
*Trouvé par* : reconnaissance de G11.
*Source* : documenté — AHA-1540C TR, CCB octet 1 (le LUN passe par IDENTIFY ; « The LUN field in the SCSI Command
Descriptor Block (CDB) is expected to be zero ») ; SCSI-2 § 7.2.2, § 8.2.5.1 (LUN non supporté : INQUIRY rend 7Fh),
§ 7.5.3 (les autres commandes : 05/25h).
*Cas qui discrimine* : CCB vers l'ID 0, LUN 1 dans le CCB et 0 dans la CDB : INQUIRY rend 00h (un disque) chez PCem,
7Fh sur le matériel ; TEST UNIT READY : GOOD contre CHECK CONDITION, sense 05/25h.
*G13* : (a).
*Reproduit* : `Scsi/scsi_aha1540.cs`, `Scsi/scsi_hd.cs`, marqueurs PB-133 (les deux formes de CCB ; le test du LUN
du disque).

### PB-135 — L'AHA-1542C s'arrête sur une commande de l'invité ; la configuration la fait tomber

`scsi_aha1540.c` : `:530` (CCB d'opcode autre que 0, 2, 3 ou 4), `:679` (les commandes 0Ch, 1Ch, 1Dh,
1Eh, 20h, 21h, 2Ah), `:799`, `:841`, `:883` (commande BIOS 03h, sous-fonctions 02h, 03h, 04h, sur un statut
cible non nul, ou périmé : PB-138), `:980` (sous-fonction 15h sur une cible non disque, ou sur un ID vide après
le sense d'un CCB de mailbox, qui laisse 70h dans `int_buffer`), `:1743` (une CDB plus longue que son
groupe, `scsi.c:21`) : `fatal()`. La configuration : la carte sans sa ROM (`rom_init` rend -1, la fenêtre
lit `bios_rom.rom` nul au POST, `:2083`) ; la carte sur un 8088 ou un 8086, que seul l'écran de PCem filtre
(`wx-config.c:237-247`) — sa ROM s'exécute au balayage (instructions 286), son IRQ haute n'existe pas.
*Effet* : un programme qui parle à la carte, ou un .cfg, arrête ou fait tomber l'émulateur.
*Trouvé par* : reconnaissance de G11.
*G13* : hors du mode — déjà corrigé dans les deux modes (R9) ; rien n'y bascule.
*NON reproduit* (R9, décision n° 5 de PLAN-G11.md) : la commande finit en erreur — `:679` et `:980` en
INVDCMD, `:799`, `:841`, `:883` en code 20h, `:530` au statut d'hôte 16h, `:1743` en suivant la phase de la
cible ; la ROM absente et la règle ISA 16 bits refusent la carte avec un avertissement
(`pc.check_hdd_controller`). Survie : `r9-aha`, `ahacfg`.

### PB-136 — PROGRAM EEPROM (22h) lit au-delà de ses paramètres

`scsi_aha1540.c:1166-1168` : `params[c + 3]` pour `c < params[1]`, jusqu'à 255 : au-delà de `params[64]`,
le struct — `result_pos`, `result_len` (petit-boutien), puis `result[]` jusqu'à `result[185]` (vérifié sur le
code de gcc 15.2 -O2). Seuls 32 octets sont persistés (`:2114`). L'IRQ peut changer avec une interruption en
attente : `set_irq` et IRST visent la nouvelle ligne, l'ancienne reste levée.
*Effet* : une EEPROM programmée avec des octets de l'état interne de la carte ; une interruption collée.
*Trouvé par* : reconnaissance de G11.
*Source* : AHA-1540C TR, *Set EEPROM* (22h) : octet 0 réservé, octet 1 le nombre, octet 2 le décalage, « One to 32
bytes of data » — documenté dans les bornes, mais le TR se contredit (« 31 minus the offset value ») ; au-delà de 32
octets : inconnu. L'ancienne IRQ relâchée quand la configuration change : déduit.
*Cas qui discrimine* : 22h de 4 octets au décalage 8, puis 23h : les mêmes 4 octets des deux côtés ; 22h qui change
l'IRQ avec une interruption en attente : l'ancienne ligne reste levée chez PCem, retombe sur la carte ; 224 octets
depuis 32 (AHABANC) : l'état interne chez PCem, inconnu sur la carte.
*G13* : (b), hors bornes (c) — le TR est ambigu : à mesurer sur une 1542C.
*Reproduit* : `Scsi/scsi_aha1540.cs`, `param_lu` (décision n° 8 de PLAN-G11.md), marqueur PB-136.

### PB-137 — Le sense automatique : inconditionnel, inversé, et écrit au hasard

`scsi_aha1540.c:1535-1622` : un REQUEST SENSE suit CHAQUE commande, même GOOD, sauf si l'octet 3 du CCB
vaut 1 ; son octet de contrôle vaut 14 (`:1610`). Sa destination est inversée (`:1613-1616`) : un CCB de
mailbox l'envoie dans `int_buffer` et jamais dans le CCB ; une commande BIOS 03h l'envoie à
`ccb.addr + 12h + longueur de CDB`, avec `ccb.addr` PÉRIMÉ — 0 à la mise sous tension, soit la table des
vecteurs (INT 07h à 0Ah).
*Effet* : un pilote ne trouve jamais le sense dans son CCB ; une commande BIOS directe peut écraser les
vecteurs de l'horloge et du clavier.
*Trouvé par* : reconnaissance de G11.
*Source* : documenté — AHA-1540C TR, CCB octet 3 — le sense automatique suit un CHECK CONDITION ; 00h : 14 octets,
01h : aucun, 08h à FFh : la longueur ; rangé à la fin du CCB (octets 18 + m à 18 + m + n). L'octet de contrôle de la
CDB à 0 : déduit. La commande 03h, interface du BIOS : inconnu (l'intention lisible est l'inverse de PCem).
*Cas qui discrimine* : un CCB qui finit GOOD : PCem envoie un REQUEST SENSE au disque, le matériel aucun ; un CCB en
CHECK CONDITION, octet 3 = 12h : PCem range le sense dans `int_buffer`, le matériel 18 octets en CCB + 12h + m ;
octet 3 = 01h : aucun sense, des deux côtés.
*G13* : (a) pour la mailbox ; (b) pour 03h, interface privée.
*Reproduit* : `Scsi/scsi_aha1540.cs`, marqueurs PB-137 (l'entrée inconditionnelle, la CDB et sa destination).

### PB-138 — Les commandes BIOS 03h achèvent un CCB périmé, et réussissent à faux

`scsi_aha1540.c:765-1013` : les sous-fonctions 02h, 03h, 04h et 08h ne posent ni `from_mailbox` ni
`current_mbo` (seule 15h remet `from_mailbox` à 0, `:974`). Après un CCB de mailbox, leur achèvement écrit dans
l'ANCIEN CCB (`+0Eh`, `+0Fh`), libère son MBO — un START que le pilote vient d'y poser est perdu — et poste un
MBI. Et sur une cible absente, la sélection ratée (`:1488-1526`) ne touche ni `ccb.status` ni `int_buffer` :
02h, 03h, 04h réussissent si le statut périmé vaut 0 ; 08h et 15h rendent quatre octets périmés.
*Effet* : des achèvements fantômes ; un disque absent « lu » sans erreur.
*Trouvé par* : reconnaissance de G11.
*Source* : AHA-1540C TR : la commande 03h est « reserved for use by the Adaptec host adapter BIOS », ses codes sont
inconnus ; qu'elle n'achève pas un CCB de la mailbox, et qu'une cible absente échoue — déduit (chapitre 6 : « 80h
Time-out. Host adapter or device not responding to BIOS »).
*Cas qui discrimine* : un CCB de mailbox achevé, puis 03h/08h : PCem écrit dans l'ancien CCB (+0Eh, +0Fh), libère
son MBO et poste un MBI ; attendu : l'ancien CCB intact, aucun MBI. 03h/02h vers l'ID 5 vide après un succès : PCem
rend 00h ; attendu : 80h.
*G13* : (b) — attendus déduits : l'interface est privée.
*Reproduit* : `Scsi/scsi_aha1540.cs`, marqueurs PB-138 (la commande 03h, la sélection ratée).

### PB-139 — ABORT efface le mauvais emplacement, et n'interrompt rien

`scsi_aha1540.c:1411` : en mailbox de 4 octets, l'action est effacée en `mba + c * 8` au lieu de `c * 4` :
un START à l'emplacement 2c est perdu, ou, au-delà de `mbc`, un code de MBI. `:1406-1428` : ABORT poste un
MBI « aborted » pour l'adresse donnée, que le CCB soit en cours, fini ou jamais lancé ; l'emplacement reste
ABORT et chaque 02h suivant en reposte un.
*Effet* : deux MBI pour un CCB ; des CCB affamés.
*Trouvé par* : reconnaissance de G11.
*Source* : documenté — AHA-1540C TR, MBO (« After the MBO has been examined … the host adapter sets the MBO command
byte back to zero ») et MBI : 02h « CCB aborted by host », 03h « Aborted CCB not found … It is likely that the CCB
was already presented to the host ».
*Cas qui discrimine* : mailbox de 4 octets, ABORT à l'emplacement 1 pour un CCB déjà achevé : PCem efface l'octet 8
(l'emplacement 2), poste un MBI « aborted » (02h), puis un autre à chaque commande 02h ; la carte efface
l'emplacement 1 et poste un seul MBI 03h.
*G13* : (a) — l'abandon d'un CCB en vol dépend du temps : son attendu est le plus fragile.
*Reproduit* : `Scsi/scsi_aha1540.cs`, marqueur PB-139.

### PB-140 — RETURN SETUP DATA est tronqué à 20 octets

`scsi_aha1540.c:1138-1140` : `result_len = MIN(params[0], 20)`, puis les octets suivants mis à zéro : la
somme A3h C2h et l'adresse de la mailbox BIOS (`:1130-1134`) ne sortent jamais.
*Effet* : un utilitaire lit une configuration incomplète.
*Trouvé par* : reconnaissance de G11.
*Source* : documenté — AHA-1540C TR, *Return Setup Data* (0Dh) — octets 00h à 2Bh (25h le réessai, 26h les
interrupteurs avec en bit 7 « EEPROM read data », 27h-28h la somme, 29h-2Bh la mailbox du BIOS), 2Ch-FFh à zéro ;
« A value of zero is accepted and 256 bytes are returned ».
*Cas qui discrimine* : 0Dh de 44 octets : PCem rend 20 octets puis 24 zéros ; la carte les 44, dont 26h (les
interrupteurs, 0 chez PCem), A3h C2h en 27h-28h et la mailbox du BIOS en 29h-2Bh. 0Dh de 0 : aucun octet chez PCem,
256 sur la carte.
*G13* : (a).
*Reproduit* : `Scsi/scsi_aha1540.cs`, marqueur PB-140.

### PB-141 — Les mailbox : le compte des 02h, le balayage BIOS, le MBI non vérifié

`scsi_aha1540.c:732`, `:1376-1405` : `mbo_req` compte les 02h et chaque balayage reprend à l'emplacement 0 ; ni
HRST, ni SRST, ni 01h ne le remettent à zéro, ni `bios_mbo_req`, ni `bios_mbo_inited`. `:1432-1455` : le
balayage de la mailbox BIOS n'est gardé ni par l'état du CCB ni par STATUS_INIT : il écrase le CCB normal pris
à la même échéance. `:1500-1503`, `:1565-1571`, `:1654-1659` : le MBI est écrit sans vérifier qu'il est
libre. `:717-728` : MAILBOX INIT accepte un compte nul.
*Effet* : après un reset du pilote, des CCB partent sans 02h ; des CCB perdus ; des achèvements écrasés.
*Trouvé par* : reconnaissance de G11.
*Source* : documenté — AHA-1540C TR — 01h : compte nul, INVDCMD et HACC ; 02h : le balayage dure « until all MBO
entries have been serviced » ; MBO : tourniquet depuis « the entry after the last MBO entry that was processed » ;
MBI : un emplacement libre ; HRST et SRST exigent une nouvelle init. La mailbox du BIOS face à la normale : inconnu.
*Cas qui discrimine* : 01h de compte 0 : HACC seul chez PCem, INVDCMD et HACC sur la carte ; deux CCB postés, un
seul 02h : un seul part, contre les deux ; un MBI encore occupé : écrasé, contre l'attente ; HRST puis 02h sans
01h : compté sans rien dire (le CCB part au 01h suivant), contre INVDCMD.
*G13* : (a), la mailbox du BIOS (b) — son rapport à la mailbox normale n'est pas documenté.
*Reproduit* : `Scsi/scsi_aha1540.cs`, marqueurs PB-141 (01h, 02h, les deux balayages, les trois écritures du MBI).

### PB-142 — La machine des commandes est réentrante, et part de zéro

`scsi_aha1540.c:316-326` : `process_cmd` est appelé dans l'OUT de la commande, dans tous les états : en
RESET, il achève l'autotest sur le champ et l'octet est perdu ; en CMD_IN_PROGRESS, l'octet reste dans CDF et
devient une nouvelle commande ; en SEND_RESULT, l'OUT pousse lui-même l'octet suivant. `:2133` : `status`
vaut 0 à la mise sous tension — ni INIT ni IDLE tant qu'il n'y a pas eu de reset.
*Effet* : un pilote qui écrit trop tôt perd un octet ou lance une commande ; la carte non initialisée
répond « prête » à la mailbox.
*Trouvé par* : reconnaissance de G11.
*Source* : documenté pour la mise sous tension — AHA-1540C TR, *Hard Reset Operations* — STST pendant l'autotest,
puis INIT et IDLE. Les octets hors protocole : manuel de l'AHA-1540A/1542A (1989) § 4.2.2, « likely to be
interpreted as invalid, although they may instead cause the execution of valid commands » — inconnu au détail.
*Cas qui discrimine* : lecture du port d'état juste après l'init de la carte : 00h chez PCem ; STST, puis INIT |
IDLE sur la carte. La réentrance (un octet écrit pendant l'autotest ou l'envoi d'un résultat) : à mesurer.
*G13* : (a) pour la mise sous tension ; la réentrance (c) — inconnue état par état : à mesurer sur une 1542C.
*Reproduit* : `Scsi/scsi_aha1540.cs`, marqueurs PB-142 (l'OUT de commande, l'état à l'init).

### PB-143 — Une CDB courte est complétée de zéros

`scsi_aha1540.c:1799-1804` : si le CCB déclare une CDB plus courte que son groupe (`scsi.c:21`), la carte, en
NEXT_PHASE, envoie un zéro par échéance tant que la cible reste en phase de commande. Une longueur 0 exécute
TEST UNIT READY ; un READ(10) déclaré sur 6 octets s'exécute avec un LBA et une longueur tronqués.
*Effet* : une commande différente de celle du pilote s'exécute.
*Trouvé par* : reconnaissance de G11.
*Source* : inconnu. Candidats du TR, déduits et non affirmés pour ce cas : 14h « Target Bus Phase Sequence Failure »
(une phase de commande au-delà de la CDB, avec un reset du bus), 1Ah « Invalid CCB » (une longueur nulle).
*Cas qui discrimine* : à mesurer sur une 1542C : READ(10) déclaré sur 6 octets — PCem complète de zéros et lit avec
un LBA et une longueur tronqués ; déclaré sur 0 octet — PCem exécute TEST UNIT READY ; la carte : 14h ou 1Ah ?
*G13* : (c) — rien ne documente ce cas : à mesurer sur la carte.
*Reproduit* : `Scsi/scsi_aha1540.cs`, marqueur PB-143.

### PB-144 — La 1542C de PCem : traduction, adresses et EEPROM

`scsi_aha1540.c:773`, `:815`, `:857` : la commande BIOS 03h traduit CHS en LBA sans retrancher 1 au secteur (la ROM
v1.01 ne s'en sert pas : elle passe par 82h). `:425-433`, `:1857`, `:1935` : les adresses ne bouclent pas à 24 bits.
`:2101-2104`, `:2155-2157`, `:261-266` : sans EEPROM, ID 0, DMA 0, IRQ 9 ; une IRQ de code 7 vaut 16, et
`picint((uint16_t)(1 << 16))` ne lève rien. `:1703-1711`, `:1052-1074`, `:417` : l'ID de l'hôte n'est pas exclu de
la sélection ; délai de sélection, temps de bus et vitesse sont rangés sans effet ; les bits de sens du CCB sont
rangés sans être lus : le sens vient de la commande, ce qui est conforme à la carte (AHA-1540C TR, CCB octet 1 ;
D3-contre, C11), mais le contrôle de longueur qu'ils arment (statut d'hôte 12h) et « aucun transfert » quand les
deux sont posés manquent.
*Effet* : sans conséquence pour la ROM ; un pilote qui les emploie voit une carte qui ne les tient pas. Le cas
« sans EEPROM » ne survient que sans `nvr/default/aha1542c.nvr`, l'EEPROM de référence de PCem (ID 7, DMA 7, IRQ
10), qu'iXtal livre.
*Trouvé par* : reconnaissance de G11.
*Source* : AHA-1540C TR, CCB octet 1 (le sens vient de la commande ; « If both bits are set … no data transfer ») et
réglages d'usine (ID 7, DMA 5, IRQ 11, 330h, DC000h) — documentés ; le −1 du secteur, les 24 bits, l'ID de l'hôte
exclu — déduits ; l'IRQ de code 7 — inconnu (SCSISelect ne propose que 9, 10, 11, 12, 14 et 15).
*Cas qui discrimine* : CCB READ de 4 secteurs, longueur 512, bit 3 posé : PCem bloque (PB-132), la carte rend 12h ;
sans EEPROM ni référence : ID 0, DMA 0, IRQ 9 chez PCem, ID 7, DMA 5, IRQ 11 ; 03h/02h au secteur 1 : LBA 1 chez
PCem, LBA 0.
*G13* : (b), mixte : longueur et usine (a), CHS et 24 bits (b), IRQ de code 7 (c), temps (d, hors G13).
*Reproduit* : `Scsi/scsi_aha1540.cs`, marqueurs PB-144 posés en G13.1 (seuls des commentaires le nommaient).

### PB-145 — La SB 2.0 sans mélangeur rend l'audio CD presque muet

`sound_sb.c:952` : `sb_2_init` appelle `sb_ct1335_mixer_reset` même quand `mixaddr` vaut 0. Le reset (`:341-347`) met le
registre du CD (08h) à 0, et `:363-369` posent `sound_set_cd_volume(8230 × 164 / 65535, …)`, soit 20 sur 65 535. Sans
mélangeur, l'invité n'a aucun port pour le relever. Avec mélangeur, le CD au minimum après un reset est la valeur que
Creative documente pour les trois mélangeurs, et PCem y est fidèle (corrigé en G13) : CT1335 08h et CT1345 28h,
« Default is 0 ⇒ −46 dB » (guide de Creative, p. 4-5 et 4-9) ; CT1745 36h et 37h, « Default is 0 ⇒ −62 dB » (p. 4-16).
Le CT1345 en donne 81 sur 65 535 (`:413-417`, 28h = 0, le volume que mesure ATAPIAUD en G10.5), le CT1745 12 sur 65 535
(`:557-558`, 36h et 37h = 0, G12.1) : tous deux avec un volume général relevé à 0 dB (PB-237), et 20 et 2 au
général du guide.
*Effet* : sur une machine équipée d'une SB 2.0 sans option CD, l'audio CD (G10.5) est à −70 dB ; avec une
SB 1.0 ou 1.5, qui n'ont pas de mélangeur, il reste à 65 535.
*Trouvé par* : reconnaissance de G12 (contre-lecture des cartes et des mélangeurs) ; le minimum d'un mélangeur rétabli
comme fidèle en G13 (reconnaissance D5, contre-lecture C3).
*Source* : le CT1335 n'équipe que la « Sound Blaster 2.0 CD Interface card » (guide de Creative, p. 4-1 et 4-4, PDF 59
et 62) : documenté. Une SB 2.0 simple n'a rien qui règle un volume CD ; le laisser à 65 535, comme pour la SB 1.0 et la
1.5, est déduit : le volume CD est une grandeur du modèle de PCem, pas de la carte.
*Cas qui discrimine* : SB 2.0, `mixaddr` = 0 : après `sb_2_init`, `sound.cd_vol_l` et `cd_vol_r` valent 20 chez PCem,
65 535 sans mélangeur, comme sur la SB 1.0. Témoin : avec `mixaddr` = 250h, 08h relu 00h et 20 dans les deux modes.
*G13* : (a) — la SB 2.0 sans mélangeur seule, un choix de modèle déduit ; le minimum après le reset d'un mélangeur,
fidèle.
*Reproduit* : `Sound/sound_sb.cs`, `sb_2_init`, marqueur PB-145. La sonde du son compare le volume CD de la carte
(G12.0). Les marqueurs PB-145 du CT1335 et du CT1745 sont retirés en G13 : leur minimum est celui du guide.

### PB-146 — 1Fh, 2Ch, 7Dh et 7Fh prennent les paramètres de la commande précédente

`sound_sb_dsp.c:45-54` : `sb_commands` vaut 0 pour 1Fh, 2Ch, 7Dh et 7Fh, qui s'exécutent donc dès l'octet de
commande, sans paramètre. Mais elles lisent `sb_data[0]` et `sb_data[1]` (`:355`, `:377`, `:431`, `:438`) :
ce sont les octets de la commande précédente. Le DSP réel prend la longueur du bloc dans 48h.
Élargi en G13 : 1Fh, 7Dh et 7Fh sont « with reference byte » ; le DSP lit d'abord un octet de référence, au lancement
seulement. PCem ne le lit pas (`:352-358`, `:428-441`) : le premier octet est décodé comme une donnée, et `sbref` et
`sbstep` gardent leurs valeurs d'avant.
*Effet* : le premier bloc de ces transferts automatiques (ADPCM 2, 4 et 2,6 bits, entrée 8 bits) a une
longueur fausse, d'où une IRQ trop tôt ou trop tard ; les suivants rechargent `sb_8_autolen` (`:1038`). La
SB Pro v2 de G8 l'atteint déjà, comme les SB 1.5, 2.0 et Pro v1. En ADPCM, le flux part d'une référence fausse.
*Trouvé par* : reconnaissance de G12 (contre-lecture du DSP) ; l'octet de référence, reconnaissance de G13 (D5, N1 ;
contre-lecture C7).
*Source* : guide de Creative, p. 6-8 (1Ch : une IRQ par bloc « of size set by command 48h »), p. 6-9, 6-10, 6-18, 6-19
(sans paramètre ; « with reference byte »), p. 6-16 (48h) ; micrologiciel 2.02, `sbv202.asm:1612-1616` et `:1395-1398`
(le compte pris à `dma_blk_len`), `:1659-1672` (la référence) ; 4.05, `v405-8k_e51aff23.asm:2603-2614`, `:2708-2717`.
*Cas qui discrimine* : SB 2.0, 8237 en automatique sur 1000h octets : 48h 7Fh 01h, 40h A5h, puis 7Dh : PCem lève la
première IRQ après 1A6h octets pris au 8237 ; PB-146 seul corrigé, après 180h ; avec l'octet de référence et la fin de
bloc du 2.02 (PB-234), après 181h (contre-lecture K6). Le premier octet : référence sur la carte, donnée chez
PCem.
*G13* : (a) — documenté deux fois ; l'attendu dépend des corrections retenues ensemble et doit le dire.
*Reproduit* : `Sound/sound_sb_dsp.cs`, marqueurs PB-146 (1Fh, 2Ch, 7Dh, 7Fh) ; SBBANC passe 7Dh après 48h 7Fh 01h, ce
qui ne distingue pas les deux comportements (les octets « périmés » y sont la taille du bloc) : il faut intercaler une
commande à paramètre.

### PB-147 — Sur la SB 1.0, D3h met le DMA en pause sans couper le son

`sound_sb_dsp.c:530-531`, `:537-538` : sur un DSP antérieur à la 1.5 (`sb_type < SB15`), D1h (haut-parleur actif) et D3h
(haut-parleur coupé) posent `sb_8_pause = 1` au lieu de toucher le son ; `sb_start_dma` remet la pause à zéro (`:207`).
Sur les DSP 2.00 à 3.02, D1h et D3h règlent `muted` ; sur la SB 16, aucun des deux, comme le 4.xx (guide de Creative,
p. 6-25, note 2). Corrigé en G13 : la pause est écrite par Creative — « On version 1.xx, the DSP will pause the DMA
transfer after executing this command », pour D1h et pour D3h (p. 6-25 et 6-26, PDF 110-111) — et PCem y est fidèle. Le
défaut est l'autre moitié : D1h et D3h relient et coupent aussi la sortie du CNA, sur toutes les versions (« The speaker
here refers to the connection of the digitized sound output to the amplifier input », p. 6-25 ; « Available » coche
1.xx), et sur la SB 1.0 PCem n'y touche pas.
*Effet* : sur la SB 1.0, D3h ne coupe pas le son ; un D1h ou un D3h envoyé après le lancement d'un DMA le suspend
jusqu'à D4h, comme sur la carte.
*Trouvé par* : reconnaissance de G12 (contre-lecture des cartes) ; la pause rétablie comme fidèle en G13 (reconnaissance
D5, contre-lecture C1).
*Source* : guide de Creative, p. 6-25 et 6-26 : la pause du 1.xx et la fonction des deux commandes, documentées ; les
micrologiciels 2.02 et 3.02 commandent la broche de coupure (`sbv202.asm:1827-1866`, `v302_4k_4701c5fc.asm:1413-1430`) :
déduit pour le 1.05, dont aucune image n'est publiée.
*Cas qui discrimine* : SB 1.0, canal 1 du 8237 en simple cycle sur 100h octets à 00h : D3h, 14h FFh 00h, D4h :
`dsp.buffer` reçoit 8000h (−32 768) chez PCem, 0 sur la carte ; D1h, puis de même : non nul des deux côtés. D8h
n'existe pas sur le 1.xx : l'essai ne l'emploie pas.
*G13* : (a) pour la coupure, documentée (déduite pour le 1.05) ; (d) pour la pause, fidèle.
*Reproduit* : `Sound/sound_sb_dsp.cs`, marqueurs PB-147 (D1h, D3h) ; SBBANC envoie D1h après 14h sur la SB 1.0.

### PB-148 — L'enregistrement rend toujours du silence

`sound_sb_dsp.h:63` : `sb_enable_i` n'est écrit nulle part, et seuls `sb_get_buffer_sb16` et
`sb_get_buffer_emu8k` remplissent `record_buffer`, sous cette condition (`sound_sb.c:180`, `:272`). Sur les
SB 1.0 à Pro v2, rien ne le remplit ; `sb_start_dma_i` le vide à chaque entrée (`sound_sb_dsp.c:282`). A0h et
A8h (l'entrée mono ou stéréo de la Pro) ne font rien (`:468-473`, TODO), et leur garde admet la SB 2.0 (DSP 2.01,
`:470`) quand le guide les réserve au 3.xx (G13).
Ce que la carte enregistre est documenté (G13) : le CT1745 mène la MIDI (l'OPL), la ligne, le CD et le micro au
mélangeur d'entrée, qui additionne les interrupteurs fermés de 3Dh et 3Eh ; en mono, le seul mélangeur gauche (« samples
will only be taken from the left input mixer ») ; la commande automatique de gain du micro est active par défaut (43h).
Le CT1345 prend une source, micro, CD ou ligne (0Ch), par un filtre passe-bas de 3,2 ou 8,8 kHz actif par défaut. Les
SB 1.x et 2.0 n'ont que le micro (déduit).
*Effet* : toute entrée DMA ou directe (20h, 24h, 2Ch, 98h, 99h) rend du silence : 80h en 8 bits non signé. C'est juste
pour le micro et la ligne, faute d'entrée de l'hôte ; faux pour l'OPL et le CD, que la SB 16 et la Pro enregistrent.
*Trouvé par* : reconnaissance de G12 (contre-lecture des cartes) ; complété en G13 (reconnaissance D5 ; contre-lecture
K15, A9).
*Source* : guide de Creative, fig. 4-5 (p. 4-14), p. 4-16 (3Dh, 3Eh, le mono), p. 4-17 (3Fh à 43h), p. 4-8 (le 0Ch du
CT1345), p. 6-22 (A0h, A8h : DSP 3.xx). Documenté pour les chemins, les sélecteurs, les coupures et les gains ; les
réponses exactes des filtres et de la commande de gain ne le sont pas.
*Cas qui discrimine* : SB 16, 3Dh = 3Eh = 60h (la MIDI seule), une note de l'OPL tenue, puis C8h 20h FFh 00h (entrée
8 bits stéréo, 256 octets) : tous à 80h chez PCem ; sur la carte, non tous à 80h, et corrélés à `sb.opl.buffer`. Le
micro et la ligne : 80h des deux côtés.
*G13* : (b) — les chemins sont documentés, pas les fonctions de transfert exactes.
*Reproduit* : `Sound/sound_sb_dsp.cs` (`sb_enable_i` ; A0h et A8h), `Sound/sound_sb.cs` (`sb_get_buffer_sbpro` ; les
blocs morts de `sb_get_buffer_sb16` et `sb_get_buffer_emu8k`), marqueurs PB-148.
*G12.1* : la SB 16 aussi : le bloc de `sb_enable_i` (`sound_sb.c:180-197`) ne s'exécute jamais.

### PB-149 — L'octet de mode de B0h à CFh n'est pas masqué

`sound_sb_dsp.c:484`, `:497`, `:510`, `:523` : les commandes 8 et 16 bits du DSP 4.xx passent `sb_data[0]`, l'octet
de mode, tel quel comme format. En 8 bits, 01h à 03h prennent le chemin ADPCM (`:918-1030`) sur un `sbdat2` et un
`sbref` périmés ; toute autre valeur que 00h, 10h, 20h et 30h n'a pas de `case` (`:866-1034`, `:1051-1086`) : la
longueur ne bouge plus, aucune IRQ ne vient. À l'entrée (B8h à BFh, C8h à CFh), `sb_poll_i` n'a de `case` que pour
00h à 30h (`:1118-1165`, `:1180-1233`).
*Effet* : un octet de mode aux bits réservés posés (les bits 0 à 3 ou 6 et 7) fige le transfert, sans fin ni IRQ ; en
sortie 8 bits, 01h et 02h jouent de l'ADPCM 4 et 2,6 bits (deux ou trois échantillons par octet, l'IRQ deux ou trois
fois plus tard), 03h de l'ADPCM 2 bits qui ne finit pas (PB-91).
*Trouvé par* : reconnaissance de G12 (contre-lecture du DSP).
*Source* : guide de Creative, p. 6-23 (PDF 108 : bMode, D7-D6 à 0, D5 stéréo, D4 signé, D3-D0 à 0) et p. 6-24 ;
micrologiciel 4.05, `cmd_dma8` ne teste que les bits 4 et 5 de l'octet (`v405-8k_e51aff23.asm:1031-1039`), `cmd_dma16`
de même (`:1133-1141`) ; 4.13, `v413-8k_e22e9001.asm:1175-1240`. Documenté.
*Cas qui discrimine* : SB 16, canal 1 du 8237 en simple cycle sur 10h octets : C0h 04h 0Fh 00h (le bit 2 réservé posé) :
PCem ne bouge plus (`sb_8_length` reste à 0Fh, aucune IRQ) ; la carte joue 16 octets non signés mono et lève l'IRQ
8 bits (82h bit 0). Et C0h 01h 0Fh 00h : de l'ADPCM 4 bits chez PCem, 16 octets non signés sur la carte.
*G13* : (a).
*Reproduit* : `Sound/sound_sb_dsp.cs`, marqueurs PB-149 (B0h, B8h, C0h, C8h, et le `switch` 16 bits de `pollsb`) ;
SB16BANC passe C0h en mode 01h.

### PB-150 — La fréquence 0 de 41h et 42h fige l'hôte

`sound_sb_dsp.c:393` : `(uint64_t)(TIMER_USEC * (1000000.0f / 0.0f))` vaut 0 en C (mesuré, `-O2` sans `-march`),
`ulong.MaxValue` en .NET. 42h recopie la valeur dans `sblatchi` (`:398`). `pollsb` se réarme alors à la même date
(`timer_advance_u64`, `:858`), et `timer_process` (`timer.c:130-144`) le rappelle sans fin dès que la minuterie de
sortie tourne (un DMA lancé, une pause par 80h) ; de même `sb_poll_i` (`:1114`) quand l'entrée tourne (20h l'arme
pour toujours). Même cause par `sb_dsp_speed_changed` (`:184`, `:189`) quand `sb_timeo` vaut 256. Les
coefficients du FIR deviennent NaN si `sb_freq` n'était pas nul.
*Effet* : un programme qui envoie 41h 00h 00h pendant un transfert fige l'émulateur ; le processeur invité ne
reprend jamais la main.
*Trouvé par* : reconnaissance de G12 (lectures des cartes et du DSP, mesure dans un bac à sable).
*G13* : hors du mode — déjà corrigé dans les deux modes (R9) ; rien n'y bascule.
*NON reproduit* (R9) : la fréquence 0 est ramenée à 1 Hz, le reste de la commande gardé (`Sound/sound_sb_dsp.cs`,
garde `sound_sb_dsp.c:393` ; décision n° 10 de PLAN-G12.md). Aucune porte comparée n'envoie 0. Survie : `r9-sb16`.

### PB-153 — Le CT1745 : 80h, 81h, 82h, 0Ah, 01h, 3Dh et 3Eh, la sélection d'entrée

`sound_sb.c:611-636` : 80h et 81h appliquent les bits dans l'ordre, le dernier posé gagne, et un 0 ne change rien ;
la traduction des requêtes 16 bits vers le canal 8 bits, que PCem documente (`:728-734`), n'existe pas. `:770` : 82h
est rendu dans un `uint8_t`, le `| 0x4000` disparaît, et le bit 2 du MPU (valeur 4) n'est jamais posé. `:601` : 0Ah
(le micro de la Pro) est recopié en 3Ah par `0Ah × 3 + 10`, tronqué à 8 bits ; `:695` le relit par `(3Ah − 10) / 3`,
juste pour 0Ah ≤ 51h, et FDh après un reset. Élargi en G13 : la recopie oublie aussi le masque et le décalage — le
0Ah du CT1745 a 3 bits, 3Ah ses 5 bits en D7:D3, que PCem relit lui-même `>> 3` (`:654`) ; l'échelle du guide donne
`3Ah = ((0Ah & 7) × 3 + 10) << 3`. 01h n'est pas tenu (`:540-546`) et se relit FFh. `:143-148` : la sélection d'entrée
MIDI se lit `a ? out_l : ((0 + b) ? out_r : 0)`, là où le mélangeur d'entrée additionne les interrupteurs fermés. Ni
IRQ ni DMA dans la configuration de la SB 16 et de l'AWE32 (TODO `:1059`, `:1084`) : les défauts de `sb_dsp_init`,
IRQ 7, DMA 1 et DMA 16 bits 5 (`sound_sb_dsp.c:826-828`), contre l'IRQ 5 des cartes d'usine. Élargi en G13 : le reset
pose 3Dh et 3Eh à 55h et 2Bh (`:565-566`), la MIDI reliée, quand le guide donne 15h et 0Bh (ligne, CD et micro).
0Eh (la stéréo de la Pro), que PCem ne branche pas sur le DSP de la SB 16, est absent du CT1745 : PCem y est fidèle
(G13). La SB 16 que vise PCem n'est pas dite : 80h et 81h sont en lecture seule sur une carte PnP, et d'autres cartes
se règlent par cavaliers, DMA haut compris.
*Effet* : un pilote qui lit 82h pour reconnaître son IRQ ne voit jamais le MPU ; un réglage du micro au-delà de 51h
se relit faux ; un jeu réglé par BLASTER=… I5, sans outil Creative qui écrive 80h, n'a pas d'IRQ ; une carte réglée
pour le 16 bits sur le canal 8 bits ne le joue pas.
*Trouvé par* : reconnaissance de G12 (contre-lecture des cartes et des mélangeurs) ; le décalage de 0Ah, 3Dh et 3Eh,
reconnaissance de G13 (D5, N5 et N6 ; contre-lecture C10, C11 et A15).
*Source* : guide de Creative, p. 2-5 (82h : D0 8 bits ou SB-MIDI, D1 16 bits, D2 MPU-401), p. 2-6 et 2-7 (80h, 81h ; la
traduction), p. 4-11 (0Eh), fig. 4-3 et 4-5, p. 4-15 et 4-16 (0Ah, 3Ah, 3Dh, 3Eh), p. 5-5 (l'IRQ 5). Documenté, sauf 80h
et 81h à plusieurs bits ou à 0, 01h et les bits hauts de 82h (inconnus : 86Box, DOSBox-X et MAME divergent).
*Cas qui discrimine* : SB 16 ; après un reset du mélangeur, 3Dh et 3Eh relus 55h et 2Bh chez PCem, 15h et 0Bh sur la
carte ; 0Ah = 07h, puis 3Ah relu 1Fh chez PCem, F8h sur la carte ; 81h = 02h, puis B0h 10h 03h 00h : PCem lit le canal
5, la carte deux octets par échantillon sur le canal 1 ; au démarrage, 80h relu 04h (IRQ 7) chez PCem, 02h (IRQ 5).
*G13* : mixte — (a) pour 82h bits 0-2, la traduction 16→8, la sélection additive, 3Dh et 3Eh, l'IRQ 5 d'usine (décision
n° 13) ; (b) pour 0Ah↔3Ah ; (c) pour 80h et 81h à plusieurs bits ou à 0, 01h, les bits hauts de 82h et le modèle de
carte ; (d) pour 0Eh, fidèle.
*Reproduit* : `Sound/sound_sb.cs`, marqueurs PB-153 (les deux sélections, 01h, 3Dh/3Eh, 0Ah écrit et relu, 80h, 81h,
82h, l'IRQ de `sb_16_init` et de `sb_awe32_init`) ; SB16BANC les relit.

### PB-154 — Le MPU-401 de la SB 16 et de l'AWE32 : 330h fixe, sans IRQ, des ACK en mode UART

`sound_sb.c:1066` (l'AWE32 : `:1091`) : `mpu401_uart_init(&sb->mpu, 0x330, -1, 0)` — l'adresse est fixe et l'IRQ vaut -1
(`sound_mpu401_uart.c:12-13` ne lève donc rien). Seuls FFh et 3Fh sont des commandes (`:21-45`) ; toute autre est
ignorée, sans ACK, comme sur la carte. FFh rend l'ACK FEh même en mode UART (`:26`), contre la note de Roland que le
code cite ; aucune entrée MIDI, la donnée relue reste l'ACK périmé. Élargi en G13 : 3Fh est acquittée en mode UART
aussi (`:36-44`), où la carte ne reconnaît plus que le reset ; et la logique d'IRQ, dormante ici, est l'inverse du
guide : elle lève sur FFh et non au passage en UART, hors Aztech, et la lecture de 3x0h n'efface rien (`:54-62`).
Brancher l'IRQ de la carte sans la réécrire produirait le contraire du guide. 330h est le réglage d'usine (300h par
cavalier).
*Effet* : un test d'IRQ du MPU échoue ; un pilote qui attend l'absence d'ACK après FFh en mode UART lit un FEh ; 3Fh
renvoyée en mode UART laisse un FEh à lire que la carte ne rend pas.
*Trouvé par* : reconnaissance de G12 (contre-lectures) ; 3Fh et la logique d'IRQ, reconnaissance de G13 (contre-lecture
K10 et A7).
*Source* : guide de Creative, p. 2-5 (l'IRQ partagée, acquittée par 3x0h, 82h bit 2), p. 5-5 et 5-7 (en UART, le seul
reset), p. 5-9 et 5-10 (l'IRQ au passage en UART et à l'octet entrant, effacée par la lecture des données) ; Roland,
manuel du MPU-401, § 5.3. Documenté, sauf l'ACK de FFh en mode UART sur la SB 16 et la lecture sans donnée (inconnus).
*Cas qui discrimine* : SB 16 : 3Fh à 331h : la carte lève son IRQ (son bit dans IRR, 82h bit 2 à 1), et la lecture de
330h rend FEh et l'efface ; PCem ne lève rien. Puis 3Fh de nouveau, en UART : la carte n'acquitte pas (331h bit 7 à 1,
rien à lire) ; PCem pose FEh et 331h bit 7 à 0.
*G13* : mixte — (a) pour l'IRQ (levée au passage en UART, effacée par la lecture de 3x0h, 82h bit 2) et pour 3Fh ignorée
en mode UART ; (c) pour l'ACK de FFh en mode UART (le 4.05 n'en écrit pas : il viendrait de la puce d'interface) et la
lecture sans donnée ; 330h, fidèle.
*Reproduit* : `Sound/sound_mpu401_uart.cs` (l'écriture, 3Fh, la lecture) et `Sound/sound_sb.cs` (`sb_16_init`,
`sb_awe32_init`), marqueurs PB-154 ; SB16BANC passe FFh, 3Fh et ACh.
*G12.2* : l'AWE32 monte le même MPU, au même endroit.

### PB-155 — Le FIR de la SB 16 dépasse le gain unité au-delà de 34,7 kHz, et la conversion déborde

`sound_sb_dsp.c:79-105` : `recalc_sb16_filter` coupe à la moitié de la fréquence de lecture, ramenée à 48 kHz ;
au-delà de 48 kHz, la coupure passe au-dessus de la fréquence de Nyquist de la sortie. Σ|coef| dépasse 2,0 à partir
de 34 736 Hz par 41h (2,67 à 65 535 Hz) et de 37 037 Hz par 40h (3,0 à 83 333 Hz, E5h à F5h). Un signal pleine
échelle dont les signes suivent ceux des coefficients déborde alors `(int32_t)(low_fir_sb16(…) × voice)`
(`sound_sb.c:150-151`) : `cvttss2si` rend INT_MIN, même pour un dépassement positif.
Élargi en G13 : la fréquence de PCem est libre — 41h et 42h de 1 à 65 535 Hz (`sound_sb_dsp.c:389-402`), 40h jusqu'à
1 MHz (`:379-388`) —, quand le DSP 4.05 la quantifie : 41h et 42h donnent un registre de 8 bits, `v ≈ 23 × f / 4096`
arrondi (des pas d'environ 178 Hz), FFh dès que l'octet fort atteint B1h, 1Ch sous 13h ; 40h borne la constante à EBh,
puis la traduit par une table. Au-dessus d'environ 45,3 kHz, la part du défaut n'existe donc pas sur la carte ; entre
34,7 et 45,3 kHz, dans les normes, reste le débordement de la conversion. PB-150 (0 ramené à 1 Hz) touche la même
commande.
*Effet* : sur une SB 16 réglée au-delà de 34,7 kHz, des claquements pleins négatifs sur les sons forts et aigus ;
et le repliement au-delà de 48 kHz.
*Trouvé par* : reconnaissance de G12 (contre-lecture, balayage du filtre dans un bac à sable) ; la quantification du
4.05, reconnaissance de G13 (D5, N8 ; contre-lecture K4).
Au-delà de 48 kHz, |H| atteint 2,0002 sur la bande où les copies du filtre se recouvrent ; une salve signée au
hasard n'y suffit pas, un carré presque alterné, oui : rééchantillonné à 48 kHz, il retombe par endroits sur les
signes des coefficients (sortie mesurée sur un modèle du filtre : 1,34 fois 2^31 à 65 535 Hz, 1,5 fois à 83 333 Hz).
*Source* : le FIR est le rééchantillonneur de PCem, sans pendant sur la carte, et un CNA sature (déduit) ; 41h, « Valid
sampling rates range from 5000 to 45 000 Hz inclusive » (guide de Creative, p. 6-15) ; micrologiciel 4.05, `X09a7`
(`v405-8k_e51aff23.asm:1793-1856`) et 40h (`:1688-1692`, `:1754-1784`), vérifiés au binaire ; les Hz, MAME (secondaire).
*Cas qui discrimine* : sur le noyau, `low_fir_sb16_coef` pour 40 960 Hz (Σ|coef| > 2), les 51 échantillons du FIR à
±32 767 aux signes des coefficients, voix à 32 767 : PCem ajoute −21 846 (INT_MIN / 3 >> 15), une conversion saturante
+21 845. 41h FFh FFh : `sb_freq` 65 535 chez PCem, le registre FFh (≈ 45 344 Hz) sur la carte.
*G13* : (a) pour la saturation, un artefact de PCem ; (a) pour le registre de fréquence du 4.05, ses Hz restant
secondaires ; les défauts propres aux 4.04 à 4.12 (la retenue perdue de 41h, contre-lecture A3) restent à trancher.
*Reproduit* : `Sound/sound_sb.cs`, marqueurs PB-155 (`sb_get_buffer_sb16`, `sb_get_buffer_emu8k`), par l'aide de
conversion du C (`Cpu._386.CvtI32`) ; .NET saturerait. `Sound/sound_sb_dsp.cs`, marqueurs PB-155 (40h ; 41h et 42h).
SB16BANC joue ce carré, à 65 535 Hz puis à 83 333 Hz.

### PB-157 — La commande du 8237 haut, l'état des requêtes et les registres relus des deux 8237

`dma.c:389-390` : l'écriture du registre de commande du 8237 haut (D0h) ne fait rien, `dma16_command` reste à 0
(celui du bas est rangé, `:124-126`). `dma_stat_rq` n'est lu que par le PS/2 (`:198-204`) : sur l'ISA, les bits de
requête ne remontent jamais, et cela sur les deux 8237 (`:82-85` et `:344-347` ne rendent que les TC). `dma16_read`
rend, pour DAh — le registre temporaire — et pour les lectures « illégales » D2h à D8h, DCh et DEh, le dernier octet
écrit (`dma16regs`, `:349`) ; `dma_read` fait de même en 09h à 0Ch, 0Eh et 0Fh (`:91`), seul 0Dh rendant 0 (`:87-88`).
*Effet* : un programme qui désactive le 8237 haut par sa commande (bit 2) n'y arrive pas ; DAh se relit avec le
dernier octet écrit au lieu du temporaire, nul sur un AT. Que le masque ne se relise pas en DEh, comme l'écrivait
cette entrée, n'est pas un écart : la lecture de Fh est illégale, et l'AT ne documente DEh qu'en écriture (AT TR
p. 1-14). Les cinq BIOS AT du dépôt réécrivent 00h en 08h et en D0h après leurs master clear (ibmat
`F000:02A6-02AB`) : ranger la commande ne demande pas que le master clear l'efface (PB-251).
*Trouvé par* : reconnaissance de G12 (contre-lecture du DSP et du DMA) ; élargi aux deux 8237 et corrigé par la
reconnaissance de G13.
*Source* : 8237A (231466-005) p. 7, la commande « cleared by Reset or a Master Clear », son bit 2 désactive ; p. 9,
l'état (« Bits 4–7 are set whenever their corresponding channel is requesting service »), le temporaire et la
figure 6 (lectures « Illegal ») ; AT TR p. 1-14. Documenté ; la valeur d'une lecture illégale est inconnue.
*Cas qui discrimine* : ibmat, OUT D0h,04h puis un DMA 16 bits de la SB 16 (canal 5) : PCem le fait ; le 8237A ne
transfère rien jusqu'à OUT D0h,00h. OUT DAh,5Ah (un master clear) puis IN AL,DAh : PCem 5Ah ; le 8237A 00h.
*G13* : (a) la commande et DAh, sans dépendre du master clear ; (c) les lectures illégales et les bits de requête :
inscrits à part en G13.4, PB-259, pour que la part (a) se corrige seule.
*Reproduit* en mode PCem : `Models/dma.cs` (transcrit dès B3), marqueurs `fixed in hardware mode: PB-157` :
`dma16_write` (la commande) et `dma16_read` (DAh) ; SB16BANC relit D0h, DAh et DEh, et la sonde du DMA compare l'état.
*Corrigé en mode matériel* (G13.4) : sous `if (materiel.pb_157)`, la commande du 8237 haut est rangée
(`dma16_command_materiel`), et son bit 2 refuse les transferts des canaux 4 à 7, comme celui du bas ; DAh se lit sur
le temporaire, nul hors du transfert de mémoire à mémoire, que ni PCem ni le mode ne font (`dma_temporaire_materiel`,
`Models/dma.Materiel.cs`). `materiel-cas PB-157` (AT : OUT D0h,04h puis un transfert du canal 5 ; OUT DAh,5Ah puis
IN DAh) rend « fait » et 5Ah en mode PCem, « refusé » et 00h en mode matériel ; la correction coupée, il rougit.
DMABANC, sur l'AT : le 8237 haut désactivé retient la requête du canal 1 (avec PB-253), et DAh rend 00h.

### PB-158 — L'EMU8000 lu et écrit par octet ; WC figé entre deux écritures

`sound_emu8k.c:1408-1409` : `emu8k_inb` rend, pour un port impair, `emu8k_inw(addr & ~1) >> 1` — les bits 1 à 8 du
mot, et non son octet haut. `:1416-1419` : `emu8k_outb` écrit chaque octet comme un mot entier, `val << 8` au port
impair (l'octet bas du registre est perdu, TODO `:1414-1415`), `val` au port pair (l'octet haut mis à zéro). Chaque
octet est un accès complet : les effets de bord d'`emu8k_inw` (SMLD et SMRD avancent, le compteur du pointeur
monte, les bits de la DRAM s'effacent) et d'`emu8k_outw` (un mot téléversé, SMALW avancé) ont lieu à chaque
octet. Et `emu8k_inw` (`:342-716`) n'appelle jamais `emu8k_update`, qui ne tourne qu'aux écritures (`:724`) et à
chaque tampon (`sound_sb.c:221`) : WC (`:651`, avancé à `:2006`), CPF, CVCF et CCCA relus restent figés, puis
sautent d'un tampon (TODO `:647-649`).
Le guide de l'EMU8000 interdit l'accès par octet : ce qu'en fait la puce n'est pas décrit. Sur un bus AT, un octet lu
à l'adresse impaire d'une carte 16 bits rend D15-D8, l'octet haut (déduit). WC, CPF, CVCF et CCCA sont décrits comme
courants : documenté, et non déduit (corrigé en G13).
*Effet* : un programme qui détecte la carte par octets (Impulse Tracker, d'après PCem) lit des valeurs décalées ;
une attente par WC, sans écriture, dure jusqu'au tampon suivant ; un traqueur qui suit CCCA le voit par bonds.
*Trouvé par* : reconnaissance de G12 (contre-lecture de l'EMU8000, D1 à D3) ; précisé en G13 (contre-lecture K7).
*Source* : guide de l'EMU8000, p. 7 (« no byte I/O transactions are allowed »), p. 14 (WC « continuously incrementing at
the sample rate », 65 536 valeurs en 1,486 s), p. 8, 9 et 10-11 (CPF et CVCF « constantly being overwritten with new
data », CCCA l'adresse courante). Documenté pour WC et les registres courants ; inconnu pour les accès par octet.
*Cas qui discrimine* : AWE32, WC (pointeur 3Bh, A22h) lu deux fois à 5 ms de temps invité, sans écriture à l'EMU8000 ni
tampon de son entre les deux : ΔWC = 0 chez PCem, 220 ou 221 sur la carte. A23h lu par octet : les bits 1 à 8 du mot
chez PCem, ses bits 8 à 15 sur la carte (déduit).
*G13* : mixte — (a) pour WC et les registres courants ; (b) pour l'octet impair ; (c) pour l'écriture par octet et les
effets de bord par octet. AWEBANC ne discrimine pas WC : ses deux lectures se suivent à moins d'une microseconde.
*Reproduit* : `Sound/sound_emu8k.cs`, marqueurs PB-158 (`emu8k_inw`, `emu8k_inb`, `emu8k_outb`) ; AWEBANC lit par
octets aux ports pairs et impairs, écrit deux octets par SMLD, et relit WC deux fois sans écriture entre.

### PB-159 — Les bits plein et vide de la DRAM sont invisibles ; SMARW n'est pas masqué

`sound_emu8k.c:1136`, `:1140`, `:1150` : les écritures A22h des canaux 20, 21 et 26 posent `dmareadbit` ou
`dmawritebit` à 8000h, OU-és à la valeur de 32 bits relue (`:621`, `:626`, `:631`, `:636`). Mais READ16 rend, en
A22h, le mot HAUT (`(var) >> 16`, `:213`) : le bit 15 tombe, le drapeau n'apparaît jamais, et sa remise à zéro
agit à vide. `:1152` : `smarw++` sans le masque de 24 bits que gardent SMALW (`:894`), SMALR (`:560`) et SMARR
(`:644`) ; l'écriture, masquée à `:329`, ne déborde pas.
Le guide décrit de plus une attente d'entrée-sortie (I/O WAIT) qui retient l'accès tant que SMLD ou SMRD est plein ou
vide, que PCem ne modélise pas ; combien de temps le drapeau reste posé n'est pas dit : « jusqu'à la première lecture »
est l'approximation de PCem (G13).
*Effet* : un programme qui attend « non plein » passe ; un programme qui attendrait le bit à 1 bouclerait sans fin,
côté invité. Au-delà de FFFFFFh, SMARW relu montre ses bits 24 et suivants.
*Trouvé par* : reconnaissance de G12 (contre-lecture de l'EMU8000, D6 et D7) ; précisé en G13 (contre-lecture K9).
*Source* : guide de l'EMU8000, p. 11-12 (le bit 31 : MT pour SMALR et SMARR, FULL pour SMALW et SMARW ; « bits 30-24
are zero on read » ; l'adresse en 23-0), p. 13-14 (l'attente) et p. 22-23 (la marche à suivre attend ces bits).
Documenté pour le bit et le masque ; inconnu pour la durée du drapeau.
*Cas qui discrimine* : AWE32 : SMALR écrit (pointeur 34h, A20h puis A22h), puis A22h lu : le bit 15 (MT) à 0 chez PCem,
à 1 sur la carte à la première lecture (l'essai ne fige que celle-là). SMARW à FFFFFFh, puis SMRD écrit (pointeur 3Ah) :
A22h relu (pointeur 37h) 0100h chez PCem (le bit 24), les bits 30-24 à 0 sur la carte.
*G13* : (a) pour la visibilité au bit 31 et le masque de 24 bits ; (c) pour la durée du drapeau et l'attente, non
décrites ou non modélisées.
*Reproduit* : `Sound/sound_emu8k.cs`, marqueurs PB-159 (la lecture de A22h, SMARW) ; AWEBANC relit SMALR et SMARW en
A22h après leurs écritures.

### PB-160 — L'EMU8000 de PCem : enveloppes, rustines et approximations

Ce que PCem assume ou rate dans la synthèse (`sound_emu8k.c`) :
- **les enveloppes** : au bout du maintien, celle de modulation passe en RAMP_UP alors qu'elle est au sommet
  (`:1843-1847`) — `:1862` la pose au palier au tic suivant, sans décroissance de hauteur ni de filtre ; relâchée
  pendant le délai, l'attaque ou le maintien, elle prend `env_mod_hertz_to_octave[v >> 9] << 9` (`:1069`) quand
  l'attaque prend `>> 5` et `<< 5` (`:1831`) : un saut au relâchement ; les délais, maintiens et retards de LFO
  sont décomptés sur la valeur programmée (`:1757-1760`, `:1782`, `:1821-1824`, `:1844`, `:1870-1879`), et une
  note redéclenchée sans réécrire ces registres n'en a plus (le « bug on delay » du TODO `:1061`) ; une attaque à
  0 vaut « jamais », la voix se tait (`:1004-1005`, `:1255-1256`, TODO `:1008-1014`) ;
- **les rustines** : l'écriture d'IFATN ignorée sous cinq conditions (`:1326-1331`, pour Impulse Tracker), hwcf3
  forcé à 4 au premier allumage d'un moteur d'enveloppe (`:989-994`, pour Doom), l'octet haut du pointeur tiré
  d'un compteur de 80h à 9Fh (`:710-711`), les pas d'initialisation repérés par `init1[0] = 03FFh` (`:912`, `:957`,
  `:1099`, `:1109`, `:1160`, `:1220`) ;
- **les relectures** (élargi en G13) : HWCF1 à HWCF3 se relisent par une permutation fixe de leurs bits (`:564-572`),
  de source inconnue, là où le guide les dit illisibles (« Due to a VLSI error, this register will not be correctly read
  by the processor ») sans dire ce qu'on lit ;
- **les approximations** : l'interpolation cubique au lieu des trois points de l'AWE, un échantillon plus tard
  (`:290-326`) ; un chorus et une réverbération « workalike » (`:1422`, `:1537`) ; un égaliseur vide
  (`:1587-1589`), les registres d'aigus et de graves sans effet ; la hauteur et la coupure posées à leur cible à
  chaque échantillon (`:1969`, `:1971`), le volume glissant de 400h par échantillon (`:1591-1602`) ;
- **la sortie** : en cubique non filtré, `dat × cvcf_curr_volume` (`:1734`) déborde l'int32 quand |dat| passe
  32 768 (jusqu'à 40 960 par le dépassement de l'interpolateur) et que le volume approche 65 535 — un claquement
  au lieu d'un écrêtage ; le filtre, lui, borne à ±32 767 (`:1699-1703`) ;
- **l'intégration** : la sortie de 44,1 kHz est portée à 48 kHz par répétition, sans interpolation
  (`sound_sb.c:226`) : du repliement.
*Effet* : des timbres autres que ceux de l'AWE32, dits tels par PCem ; des redéclenchements sans délai ; des notes
muettes à l'attaque 0 ; une carte non initialisée qui sonne ; des relectures de HWCF1 à HWCF3 que la puce ne rend pas.
*Trouvé par* : reconnaissance de G12 (contre-lecture de l'EMU8000, D4, D5, D8, D11 à D15) ; les relectures de HWCF1 à
HWCF3, reconnaissance de G13 (contre-lecture A10).
*Source* : guide de l'EMU8000, p. 14 et 21 (HWCF3 effacé au reset, 0004h en fin d'initialisation ; HWCF1 à 3
illisibles), p. 16 (DCYSUSV bit 7 : moteur arrêté, les cibles ne sont plus écrites), p. 17 (« 0x00 being never
attack »), p. 18 (IFATN), p. 7 (le pointeur « random »). Documenté pour hwcf3, l'attaque 0 et le pointeur ; le reste,
déduit ou inconnu.
*Cas qui discrimine* : AWE32 non initialisé (hwcf3 = 0) : une voix allumée (DCYSUSV bit 7 de 1 à 0) sonne chez PCem
(hwcf3 forcé à 4) et se tait sur la carte, vu par la sonde ou la sortie, jamais par HWCF3 relu ; IFATN 40h écrit, moteur
arrêté : PCem pose VTFT, la carte le laisse (déduit). Les autres points : rapport D5 de G13, § 3.
*G13* : mixte — (a) pour la rustine de hwcf3 (décision n° 13) ; (b) pour celle d'IFATN, la fin du maintien, le
relâchement en `>> 9`, la sortie et la répétition à 48 kHz ; (c) pour les délais, les pas d'initialisation,
l'interpolation, les effets, les glissements et HWCF1 à HWCF3 relus ; (d) pour l'attaque 0 et le pointeur.
*Reproduit* : `Sound/sound_emu8k.cs` et `Sound/sound_sb.cs`, marqueurs PB-160 (vingt-trois sites). AWEBANC redéclenche
une note sans réécrire ses enveloppes, remet hwcf3 à 0 avant un allumage, et écrit IFATN sur un canal intact ; il relit
aussi HWCF1 à HWCF3 (`awebanc.S:77-85`), que la puce ne rend pas.

### PB-165 — 08h : la SB 16 de PCem lui attend un paramètre que le DSP 4.05 ne lit pas ; l'AWE32 est juste

`sound_sb_dsp.c:168-171` : `sb_doreset` pose `sb_commands[8] = 1` pour `sb_type == SB16` (la SB 16, DSP 4.05), -1 pour
tout autre type. `sound_sb.c:1073-1095` : `sb_awe32_init` monte le DSP en type SB16 + 1 = 8, la version 4.13
(`sound_sb_dsp.c:57`) — numériquement SADGOLD (`ibm.h:359-360`) ; les tests `>= SB16` le comptent comme une SB 16, ce
seul `== SB16` l'écarte. 08h (la version de l'ASP) rend 18h (`:648-650`) : sur l'AWE32 dès l'octet de commande, son
paramètre supposé étant pris pour une commande ; sur la SB 16, après un paramètre. Renversé en G13 : dans chaque image
d'origine des DSP 4.04 à 4.16, 08h lit le port 82h de son bus X et rend l'octet, sans lire d'octet de l'hôte. L'AWE32 de
PCem est juste ; c'est la SB 16 qui se trompe, en avalant l'octet qui suit 08h. La valeur rendue est celle de ce port,
que fixe le CSP s'il est monté : inconnue sans la puce.
*Effet* : sur la SB 16, l'octet qui suit 08h est pris pour son paramètre au lieu d'être exécuté (08h puis E1h ne rend
pas la version) ; sur les deux cartes, 18h n'est peut-être pas la valeur de la carte.
*Trouvé par* : reconnaissance de G12 (contre-lecture de l'EMU8000, D15) ; renversé en G13 (reconnaissance D5, N7 ;
contre-lecture C2, vérifié au binaire).
*Source* : micrologiciel 4.05, `cmd_csp_version` (`v405-8k_e51aff23.asm:1326-1330` ; routine en 06C5h, `78 82 E2 12 …`,
sans appel de `dsp_data_read`), 4.13 (`v413-8k_e22e9001.asm:1503-1507`), de même de 4.04 à 4.16. Documenté pour le
nombre de paramètres ; la valeur, inconnue (DOSBox-X, mesuré : 10h avec l'ASP, FFh sur une ViBRA 16 sans, secondaire).
*Cas qui discrimine* : SB 16 : 08h, E1h, puis trois lectures de 2xAh, chacune après 2xEh : PCem rend 18h et plus rien ;
la carte rend l'octet de 08h, puis 04h et 05h. AWE32 : 18h, 04h et 0Dh dans les deux modes, le témoin.
*G13* : (a) pour le nombre de paramètres de la SB 16 ; (c) pour la valeur rendue par 08h ; l'AWE32, fidèle, ne change
pas.
*Reproduit* : `Sound/sound_sb_dsp.cs`, marqueurs PB-165 (`sb_doreset` ; la valeur de 08h) ; le marqueur de
`sb_awe32_init` est retiré, le site étant juste. AWEBANC envoie 08h E1h et lit 18h, 04h, 0Dh, le comportement du 4.13 ;
SB16BANC envoie 08h 00h, qui ne distingue pas (00h ne fait rien).

### PB-166 — L'AWE32 sans sa ROM arrête PCem ; une ROM courte laisse de l'indéterminé

`sound.c:102-106` : `sound_card_init` monte la carte sans tester `device_available` ; `emu8k_init` s'arrête alors
sur `fatal("AWE32.RAW not found")` (`sound_emu8k.c:2019`) — dans l'oracle, `fatal()` rend la main, et `fread` sur
NULL (`:2022`) le fait tomber. Une ROM de moins de 1 Mio laisse le reste du tampon de `malloc` (`:2021-2022`) non
initialisé : l'EMU8000 lit de l'indéterminé, différent d'une exécution à l'autre. Seule l'interface de PCem
filtre la carte par sa ROM (`wx-config.c:190-193`).
*Effet* : un .cfg qui demande l'AWE32 sans `roms/awe32.raw` arrête l'émulateur au démarrage.
*Trouvé par* : reconnaissance de G12 (lectures de l'EMU8000).
*G13* : hors du mode — déjà corrigé dans les deux modes (R9) ; rien n'y bascule.
*NON reproduit* (R9) : `check_sndcard` refuse l'AWE32 sans sa ROM, ou avec une ROM d'une autre taille que
1 048 576 octets, avec un avertissement (`pc.cs`, garde `sound_emu8k.c:2019` ; décision n° 5 de PLAN-G12.md).
L'oracle refuse bruyamment de la monter. Survie : `r9-awecfg`.

### PB-121 — Le lecteur de l'amorçage précédent n'est jamais fermé

`ide.c:282-284` : chaque `resetide` appelle `scsi_bus_atapi_init`, qui alloue un
`scsi_cd_data_t` neuf (512 Ko, `scsi_cd.c:621`) sans fermer celui de l'amorçage précédent.
`scsi.c:329-335` : `scsi_bus_close` vide `devices` et `device_data` avant la boucle qui devait les
fermer — rien n'est fermé.
*Effet* : aucun observable pour un lecteur de CD, 512 Ko perdus par amorçage. **Élargi en G11** : pour un
disque SCSI, le `FILE*` reste ouvert avec son tampon stdio ; la libc le vide à `exit()` (et l'oracle à
`h_closepc`, `fflush(NULL)`). Après un reset matériel, le nouveau `FILE*` relit le dernier secteur écrit tel
qu'il était sur le disque, et à la sortie le tampon orphelin, vidé en dernier (`_IO_list_all`), écrase ce qui a
été écrit depuis.
*Trouvé par* : transcription de G10.4 ; reconnaissance de G11.
*Source* : sans objet au matériel : le mécanisme est une fuite de l'hôte, un objet et son tampon par amorçage ; l'effet
sur l'image, le seul observable, est corrigé dans les deux modes (G13.0).
*Cas qui discrimine* : aucun depuis G13.0 ; `reset-scsi-check` garde l'effet corrigé.
*G13* : (d) — hors du mode : la fuite reste reproduite, l'image est protégée dans les deux modes.
*Reproduit* : le mécanisme, `Scsi/scsi.cs`, `scsi_bus_close` (marqueur PB-121) : aucun périphérique n'est fermé, et
l'ancien objet est lâché au ramasse-miettes.
*NON reproduit* pour l'image (hôte, G13.0) : `hdd_file` vide un flux encore ouvert sur la même image avant de la
rouvrir (`Disc/hdd_file.cs`, marqueur PB-121) ; la machine neuve relit l'image à jour, et le vieux tampon, vide,
n'écrase plus rien à la sortie. Le flux n'est pas fermé (une image montée deux fois garde ses deux flux, comme deux
`FILE*`), `scsi_bus_close` reste transcrit tel quel et l'ancien objet est lâché. `reset-scsi-check` le prouve.
Passé de la section C à la section A en G13.0 : l'image de l'utilisateur perdait des écritures, dans l'usage
courant (le profil `ixtal26-486-scsi.cfg`, puis « Reset materiel » au menu). Le registre de `hdd_file`, que
`closepc` vide du plus récent au plus ancien puis ferme (DEVIATION hôte, décision n° 7 de PLAN-G11.md), demeure.

### PB-32 — `pmodeint` : une précédence d'opérateurs annule le code d'erreur

`x86seg.c:1660` (le `pclog` est en `:1659`), dans la branche « vecteur hors des bornes de
l'IDT » :

```c
x86gpf(NULL, (num * 8) + 2 + (soft) ? 0 : 1);
```

`+` lie plus fort que `?:` en C, donc la condition est `((num * 8) + 2 + soft)`. Elle est
**toujours non nulle** — `num * 8 + 2` vaut au minimum 2 — et l'expression rend donc
**toujours 0**. Le code d'erreur voulu, `(num * 8) + 2`, n'est jamais transmis ; les deux
branches du ternaire, 0 et 1, sont là par accident de parenthésage.

Les deux autres sites de la même fonction qui construisent ce code d'erreur l'écrivent
correctement — `x86gpf(NULL, (num * 8) + 2)` aux lignes 1687 et 1692 — ce qui confirme
l'intention.

*Effet* : un `INT n` dont le vecteur dépasse la limite de l'IDT lève bien un #GP, mais avec
un code d'erreur nul au lieu du sélecteur fautif. Un gestionnaire qui lirait le code pour
identifier la cause verrait zéro. Aucun BIOS ni DOS de ce dépôt ne le lit.
Passé de la section C à la section A en G13.1 : le code d'erreur empilé est un résultat observable, et le mode
matériel le corrige (`PLAN-G13.md` § G13.5).
*Source* : documenté — « Interrupt vector must be within IDT table limits, else #GP(vector number *
8+2+EXT) » (386 PRM, page INT) ; les bits IDT et EXT du code (386 PRM § 9.7, « Error Code »).
*Cas qui discrimine* : pm-check : IDT de limite FFh (32 portes), `INT 20h` → #GP(0) (PCem) ;
#GP(0102h) (386). Une interruption matérielle hors de l'IDT : 0 contre n × 8 + 3 (EXT).
*G13* : (a) — une ligne sous la garde, avec EXT (PB-192).
*Reproduit* : `Cpu/x86seg.cs:1119`, marqueur `// pcem bug, reproduced: PB-32` dans `pmodeint`.
Transcrit avec la même précédence, donc le même résultat — le corriger changerait le code
d'erreur d'un côté seulement.

### PB-169 — DIV et IDIV du 8088 ne lèvent INT 0 que pour un diviseur nul

`808x.c:3571` (DIV AL,b, ci-dessous), `:3615` (IDIV AL,b), `:3723` (DIV AX,w), `:3746` (IDIV AX,w) :

```c
if (temp) {
        tempw2 = tempw % temp;
        ...
        AH = tempw2;
        tempw /= temp;
        AL = tempw & 0xFF;
} else {
        printf("DIVb BY 0 %04X:%04X\n", cs >> 4, cpu_state.pc);
        writememw(ss, (SP - 2) & 0xFFFF, cpu_state.flags | 0xF000);
```

Seul le diviseur nul est testé : un quotient qui ne tient pas dans AL (AX) est tronqué. IDIV ne
lève pas non plus l'interruption pour un quotient de 80h (8000h), ni ne tient compte d'un REP
qui le précède : `rep()` relance l'instruction sans le préfixe (PB-177).

*Effet* : DIV et IDIV rendent un quotient tronqué et un reste là où le 8088 lève INT 0. Mesuré :
SST 8088 `F6.6` (4 802 / 10 000, « div byte [ss:bp+di+64h]: AX = 0x0AE6, attendu 0x8ED2 ») et
`F7.6` (4 905 / 10 000) ; environ 7 000 des échecs de `F6.7` (PB-45).
*Trouvé par* : reconnaissance de G13 (D1 § 5.1, D1-contre C12), sur la ligne de base SST 8088.
*Source* : documenté — « Interrupt 0 if the quotient is too big » (386 PRM, pages DIV et IDIV) ; sur
le 8086/8088, un quotient de 80h (8000h) lève aussi l'exception 0 (§ 14.7, point 11) et l'adresse empilée
est celle de l'instruction suivante (§ 14.7, point 2) ; REP devant IDIV inverse le signe (README SST 8088).
*Cas qui discrimine* : SST 8088 `F6.6`, `F7.6` (au manifeste), `F6.7`, `F7.7` (hors). Banc : AX = 1000h,
BL = 02h, `DIV BL` → AX = 0000h, rien d'empilé (PCem) ; INT 0, SP − 6, IP de l'instruction suivante
empilé (8088).
*G13* : (a), avec PB-45 et PB-180 : sans les drapeaux empilés, les cas de débordement
échouent encore sur la pile, que la sonde compare sans masque (D1-contre K6).
*Reproduit* en mode PCem : `Cpu/808x.cs`, DIV et IDIV, octet et mot, marqueurs `fixed in hardware mode: PB-169`.
*Corrigé en mode matériel* (G13.3, avec PB-45) : sous `if (materiel.pb_169)` (IDIV octet : `pb_45`), `div8_materiel`, `idiv8_materiel`, `div16_materiel` et `idiv16_materiel` lèvent INT 0 sur un quotient hors capacité, 80h et 8000h compris pour IDIV, l'IP de l'instruction suivante empilé ; les drapeaux empilés restent ceux d'avant (PB-180, reproduit), sous le SS réel (PB-258). `materiel-cas PB-169` rend la valeur de PCem en mode PCem, celle du matériel en mode matériel, la sonde comptant la correction ; la correction coupée (`--attendu materiel` en mode PCem), il rougit. SST en mode matériel : `F6.6`, `F6.7`, `F7.6` et `F7.7` passent entières : au 8088 de 4 833, 1 187, 4 905 et 2 243 cas à 10 000 chacune ; au 8086 de 1 054, 337, 1 041 et 545 à 2 000 chacune (les cas qui commencent par REP compris depuis la suite de G13.3) (avec PB-45, PB-258 et l'IDIV sous REP de PB-177). Depuis G13.3, la sonde compare les drapeaux qu'une interruption empile sous le masque de la forme, comme `sst386-probe` (`SstProbe.MasquePile`) ; sans cela, chaque cas de débordement échouait sur la pile.

### PB-170 — Le DAA du 8088 suit le pseudo-code d'Intel, là où le silicium compare autrement

`808x.c:1602` (dans `case 0x27`, `:1592-1610`) :

```c
if ((cpu_state.flags & C_FLAG) || (AL > 0x9F)) {
        AL += 0x60;
        cpu_state.flags |= C_FLAG;
}
```

Le second ajustement compare l'AL déjà ajusté à 9Fh. Sur les 1 024 entrées (AL, AF, CF), le
résultat est exactement celui du pseudo-code du SDM, qui compare l'AL d'origine à 99h : PCem est
juste au regard d'Intel. Le 8088 mesuré compare l'AL d'origine à 99h, ou à 9Fh si AF valait 1
en entrée.

*Effet* : pour AL de 9Ah à 9Fh avec AF = 1 et CF = 0 (6 entrées sur 1 024), PCem ajoute 66h et
pose CF (9Eh → 04h), le 8088 n'ajoute que 6 (9Eh → A4h, CF = 0).
*Trouvé par* : reconnaissance de G13 (D1 § 5.1, D1-contre K5), sur la ligne de base SST 8088.
*Source* : mesuré seulement — SST 8088 v2 (AMD D8088), forme `27` ; GloriousCow, commentaire du 24 février
2023 sous l'article de K. Shirriff (righto.com) : « If AF==1, the CPU compares against 0x9F before
adding 60 ». Intel ne documente pas cet écart : le SDM donne le résultat de PCem.
*Cas qui discrimine* : SST 8088 `27`, au manifeste (9 936 / 10 000, « daa: AX = 0x3604, attendu
0x36A4 » ; la règle prédit 0,59 % d'écarts, la mesure 0,64 %). Banc : AL = 9Eh, AF = 1, CF = 0,
`DAA` → AL = 04h, CF = 1 (PCem) ; AL = A4h, CF = 0 (8088).
*G13* : (a) — vérifiable par SST ; la règle vient d'un silicium AMD, que le corpus 8086 (Intel)
peut confirmer (D1-contre A6).
*Reproduit* en mode PCem : `Cpu/808x.cs`, `case 0x27`, marqueurs `fixed in hardware mode: PB-170`.
*Corrigé en mode matériel* (G13.3) : sous `if (materiel.pb_170)`, `daa_materiel` : le seuil de l'AL d'origine, 99h ou 9Fh si AF valait 1 ; AF et CF effacés quand leur pas n'a pas lieu. La règle rend les 10 000 cas du 8088 et les 2 000 du 8086 (Intel), vérifiée d'abord par un modèle hors machine. `materiel-cas PB-170` rend la valeur de PCem en mode PCem, celle du matériel en mode matériel, la sonde comptant la correction ; la correction coupée (`--attendu materiel` en mode PCem), il rougit. SST en mode matériel : `27` entière, 9 936 → 10 000 au 8088, 1 983 → 2 000 au 8086.

### PB-171 — Le DAS du 8088 reprend le CF de l'emprunt d'en bas et compare l'AL déjà ajusté

`808x.c:1665-1683` (`case 0x2F`), le second test en `:1675` :

```c
if ((cpu_state.flags & C_FLAG) || (AL > 0x9F)) {
        AL -= 0x60;
        cpu_state.flags |= C_FLAG;
}
```

L'emprunt de l'ajustement bas pose CF, et le second test le reprend : `AL = 01h, AF = 1, CF = 0`
donne FBh avec CF = 1 après le premier pas, puis 9Bh. Le SDM teste l'AL et le CF d'origine
(`old_AL > 99H or old_CF = 1`) et rend FBh. Le seuil compare de plus l'AL ajusté à 9Fh.

*Effet* : AL faux dans 24 des 1 024 entrées (AL, AF, CF) au regard du SDM, 18 au regard de la
règle mesurée sur le 8088.
*Trouvé par* : reconnaissance de G13 (D1 § 5.1, D1-contre K5), sur la ligne de base SST 8088.
*Source* : documenté pour l'emprunt (SDM vol. 2, page DAS : `old_AL`, `old_CF`) ; pour le seuil, la
règle du 8088 (9Fh si AF = 1, comme DAA) est déduite de la mesure : prédite 1,76 %, mesurée 1,86 %
(SST 8088 v2, forme `2F`).
*Cas qui discrimine* : SST 8088 `2F`, au manifeste (9 814 / 10 000, « das: AX = 0xEA9B, attendu
0xEAFB »). Banc : AL = 01h, AF = 1, CF = 0, `DAS` → AL = 9Bh (PCem) ; FBh (SDM et 8088).
*G13* : (a) — le SDM pour l'emprunt, la règle mesurée pour le seuil ; vérifiable par SST.
*Reproduit* en mode PCem : `Cpu/808x.cs`, `case 0x2F`, marqueurs `fixed in hardware mode: PB-171`.
*Corrigé en mode matériel* (G13.3) : sous `if (materiel.pb_171)`, `das_materiel` : la règle de DAA en soustraction ; l'emprunt du premier pas ne pose pas CF. 10 000 cas sur 10 000 au 8088, 2 000 sur 2 000 au 8086, au modèle hors machine. `materiel-cas PB-171` rend la valeur de PCem en mode PCem, celle du matériel en mode matériel, la sonde comptant la correction ; la correction coupée (`--attendu materiel` en mode PCem), il rougit. SST en mode matériel : `2F` entière, 9 814 → 10 000 au 8088, 1 951 → 2 000 au 8086.

### PB-172 — REP LODSB et REP LODSW ne chargent jamais AL ni AX

`808x.c:1110-1147`, `rep()` : l'octet lu va dans `temp2` (`:1112`), le mot dans `tempw2` (`:1131`) ;
ni l'un ni l'autre n'est copié dans AL ou AX.

*Effet* : après `REP LODSB` ou `REP LODSW`, AL ou AX garde sa valeur ; SI et CX avancent. Un code
qui se sert d'un REP LODS pour lire le dernier élément d'une suite lit faux.
*Trouvé par* : reconnaissance de G13 (D1 § 5.1, D1-contre C9), sur la ligne de base SST 8088.
*Source* : documenté — LODS charge AL (AX) depuis DS:SI à chaque répétition (Intel, page LODS) ;
mesuré, SST 8088 v2, forme `AD`.
*Cas qui discrimine* : SST 8088 `AD`, au manifeste (1 004 / 1 515 joués, « es rep lodsw: AX = 0x94BE,
attendu 0xC65C » ; la sonde écarte les cas qui commencent par REP, D1-contre K9) ; `AC` hors manifeste.
Banc : CX = 1, DS:SI sur 5Ah, AL = 00h, `REP LODSB` → AL = 00h (PCem), 5Ah (8088).
*G13* : (a) — deux affectations sous la garde ; vérifiable par SST.
*Reproduit* en mode PCem : `Cpu/808x.cs`, REP LODSB et REP LODSW, marqueurs `fixed in hardware mode: PB-172`.
*Corrigé en mode matériel* (G13.3) : sous `if (materiel.pb_172)`, `lodsb_materiel` et `lodsw_materiel` chargent AL et AX. `materiel-cas PB-172` rend la valeur de PCem en mode PCem, celle du matériel en mode matériel, la sonde comptant la correction ; la correction coupée (`--attendu materiel` en mode PCem), il rougit. SST en mode matériel : depuis que la sonde joue un REP devant une chaîne jusqu'à sa dernière répétition et n'écarte plus les cas qui commencent par REP (`SstProbe.JouerChaine`, G13.3, suite), `AC` et `AD` passent entières, 2 000 cas sur 2 000 chacune, au 8088 comme au 8086 ; en mode PCem, 1 019 et 1 010 au 8088, 1 066 et 1 068 au 8086. Avant, le corpus attendait toutes les répétitions là où la sonde n'en jouait qu'une : 957 échecs au 8088, 930 au 8086.

### PB-173 — SETMO et SETMOC (D0 à D3 /6) sont traités comme SHL

`808x.c:2798-2799`, `:2920-2921`, `:3068-3069`, `:3219-3220` :

```c
case 0x20:
case 0x30: /*SHL b,1*/
```

Le champ `reg` 6 (`0x30`) est rangé avec 4 (`0x20`, SHL). Sur le 8088, `/6` est SETMO (D0, D1) ou
SETMOC (D2, D3, si CL ≠ 0) : l'opérande devient FFh (FFFFh).

*Effet* : l'opérande est décalé à gauche au lieu de prendre FFh (FFFFh), et les drapeaux suivent
SHL.
*Trouvé par* : reconnaissance de G13 (D1 § 5.1, D1-contre C10), sur la ligne de base SST 8088.
*Source* : mesuré seulement — SST 8088 v2 (AMD D8088) : `metadata.json` marque `D0.6`, `D1.6`, `D2.6`
et `D3.6` « undocumented » (« well-defined and potentially useful behavior, such as SETMO and
SETMOC », README), masque 0xF72A : aucun drapeau arithmétique comparé. Intel ne documente pas ces formes.
*Cas qui discrimine* : SST 8088 `D0.6`, au manifeste (32 / 10 000, « setmo byte [ss:bp-3D75h]:
mem[0xB0443] = 0x54, attendu 0xFF ») ; `D1.6`, `D2.6`, `D3.6` hors manifeste. Banc : AL = 2Ah, `D0 F0`
→ AL = 54h (PCem), FFh (8088) ; SETMOC avec CL = 0 laisse l'opérande des deux côtés.
*G13* : (a) — vérifiable par SST ; comportement pris du silicium AMD, que le corpus 8086 peut
confirmer.
*Reproduit* en mode PCem : `Cpu/808x.cs`, D0 à D3 /6, marqueurs `fixed in hardware mode: PB-173`.
*Corrigé en mode matériel* (G13.3) : sous `if (materiel.pb_173)`, `setmo8_materiel` et `setmo16_materiel` mettent l'opérande à FFh (FFFFh) — SETMOC si CL ≠ 0, que le code de PCem teste déjà — avec les drapeaux que le 8088 laisse, mesurés : l'octet bas vaut 84h dans les 29 691 cas de D0.6 à D3.6 qui ne gardent pas l'opérande (SF et PF). Le temps reste celui de SHL. `materiel-cas PB-173` rend la valeur de PCem en mode PCem, celle du matériel en mode matériel, la sonde comptant la correction ; la correction coupée (`--attendu materiel` en mode PCem), il rougit. SST en mode matériel : `D0.6`, `D1.6`, `D2.6` et `D3.6` entières (de 32, 0, 174 et 155 cas au 8088 ; de 0, 0, 165 et 148 au 8086).

### PB-174 — Les décalages par CL du 8088 n'écrivent jamais OF

`808x.c:3068-3119` (SHL, SHR et SAR b,CL) et `:3219-3271` (w,CL) : aucune de ces lignes ne touche
`V_FLAG`.

*Effet* : OF garde la valeur qu'il avait avant l'instruction ; le 8088 le pose de façon
déterministe, et la valeur héritée est fausse environ une fois sur deux.
*Trouvé par* : reconnaissance de G13 (D1 § 5.1, D1-contre C11), sur la ligne de base SST 8088.
*Source* : Intel le dit indéfini (« OF is undefined for multiple shifts », 386 PRM, page SAL/SAR/SHL/SHR) ;
le 8088 le pose, mesuré (SST 8088 v2 : masque 0xFFEF, seul AF masqué, sur `D2.4`, `D2.5`, `D2.7`, `D3.4`,
`D3.5`, `D3.7`). Règle déduite, à confirmer sur ces formes : celle du dernier pas d'un bit.
*Cas qui discrimine* : SST 8088 `D2.4`, au manifeste (2 648 / 5 000, « shl byte [ds:si+41h], cl: flags
= 0xFC56, attendu 0xF446 », OF 1 contre 0) ; les cinq autres hors manifeste. Banc : OF = 1, AL = 01h,
CL = 2, `SHL AL,CL` → OF = 1 (PCem), 0 (8088).
*G13* : (a) — vérifiable par SST ; la règle exacte se tire des vecteurs.
*Reproduit* en mode PCem : `Cpu/808x.cs`, SHL, SHR et SAR par CL, octet et mot, marqueurs `fixed in hardware mode: PB-174`.
*Corrigé en mode matériel* (G13.3) : sous `if (materiel.pb_174)`, l'OF du dernier pas d'un bit (`of_shl8_materiel` à `of_sar_materiel`) : SHL, le bit de poids fort du résultat XOR CF ; SHR, le bit 6 (14) du résultat ; SAR, 0. Tirée des vecteurs hors machine (D2.4, D2.5, D2.7 : 14 566 cas sur 14 566). `materiel-cas PB-174` rend la valeur de PCem en mode PCem, celle du matériel en mode matériel, la sonde comptant la correction ; la correction coupée (`--attendu materiel` en mode PCem), il rougit. SST en mode matériel : `D2.4`, `D2.5`, `D3.4` et `D3.5` entières au 8088 (de 2 648, 2 635, 2 598 et 2 597 à 5 000) ; `D2.7` et `D3.7` avec PB-176 ; le corpus du 8086 masque cet OF.

### PB-175 — AAM et AAD du 8088 posent SF et ZF d'après AX

`808x.c:3284` (AAM) et `:3291` (AAD) : `setznp16(AX);`, là où les drapeaux suivent AL.

*Effet* : AAD laisse AH à 0, donc ZF est juste et SF toujours nul (faux dès qu'AL ≥ 80h). AAM pose
ZF à faux quand AL = 0 et AH ≠ 0, et SF à faux pour une base atypique (1, ou plus de 80h). PF est
juste : la table de parité ne lit que l'octet bas.
*Trouvé par* : reconnaissance de G13 (D1-contre A1), à la lecture du C.
*Source* : documenté — « The SF, ZF, and PF flags are set according to the resulting binary value in the
AL register » (SDM vol. 2, pages AAD et AAM) ; SST 8088 v2 compare SF, ZF et PF (masque 0xF7EE pour
`D4` et `D5`).
*Cas qui discrimine* : SST 8088 `D4`, `D5` (hors manifeste). Banc : AL = 14h, `AAM 0Ah` → AX = 0200h,
ZF = 0 (PCem), 1 (8088) ; AH = 01h, AL = 78h, `AAD 0Ah` → AL = 82h, SF = 0 (PCem), 1 (8088).
*G13* : (a) — vérifiable par SST ; même défaut dans le cœur 386 (PB-186).
*Reproduit* en mode PCem : `Cpu/808x.cs`, AAM et AAD, marqueurs `fixed in hardware mode: PB-175`.
*Corrigé en mode matériel* (G13.3) : sous `if (materiel.pb_175)`, `znp_al_materiel` pose SF, ZF et PF d'après AL. `materiel-cas PB-175` rend la valeur de PCem en mode PCem, celle du matériel en mode matériel, la sonde comptant la correction ; la correction coupée (`--attendu materiel` en mode PCem), il rougit. SST en mode matériel : `D4` et `D5` entières, de 8 415 et 5 131 cas au 8088 (9 953 et 10 000), de 1 724 et 1 031 au 8086 (1 988 et 2 000).

### PB-176 — SAR par CL du 8088 rend CF nul au-delà de 8 (16) sur un opérande négatif

`808x.c:3104` (SAR b,CL) et `:3258` (SAR w,CL) :

```c
if ((temp >> (c - 1)) & 1)
```

`temp` (`tempw`) est non signé : le décalage remplit de zéros. Pour un compte supérieur à 8 (16),
le bit testé est un zéro, alors que le dernier bit sorti d'un SAR est une copie du signe. Au-delà
de 32, le décalage du C est indéfini ; l'hôte x86 masque le compte à 5 bits, le C# aussi.

*Effet* : CF = 0 au lieu de 1 après `SAR` par CL > 8 (16) d'un opérande négatif ; le résultat,
calculé par la boucle, est juste.
*Trouvé par* : reconnaissance de G13 (D1-contre A2), à la lecture du C.
*Source* : déduit — la définition de SAR (le bit bas va dans CF à chaque pas, « the high-order bit
remains the same », 386 PRM, page SAL/SAR/SHL/SHR) et un 8088 qui ne masque pas le compte (386 PRM
§ 14.7, point 5 : seul le 386 le masque à 5 bits).
*Cas qui discrimine* : SST 8088 `D2.7`, `D3.7` (hors manifeste ; le corpus masque CL à 6 bits).
Banc : AL = 80h, CL = 9, `SAR AL,CL` → AL = FFh des deux côtés, CF = 0 (PCem), 1 (8088).
*G13* : (a) — deux lignes ; vérifiable par SST.
*Reproduit* en mode PCem : `Cpu/808x.cs`, SAR par CL, octet et mot, marqueurs `fixed in hardware mode: PB-176`.
*Corrigé en mode matériel* (G13.3) : sous `if (materiel.pb_176)`, `cf_sar8_materiel` et `cf_sar16_materiel` prennent le dernier bit sorti d'un décalage signé, le signe dès que le compte atteint la largeur. `materiel-cas PB-176` rend la valeur de PCem en mode PCem, celle du matériel en mode matériel, la sonde comptant la correction ; la correction coupée (`--attendu materiel` en mode PCem), il rougit. SST en mode matériel : avec PB-174, `D2.7` et `D3.7` entières : de 1 593 et 1 740 à 5 000 au 8088, de 1 243 et 1 363 à 2 000 au 8086.

### PB-177 — `rep()` du 8088 : 6Eh exécuté comme OUTSB, DS: absent, préfixe perdu avant REP

`808x.c:950-969` (`case 0x6E: /*REP OUTSB*/`), `:915` (`uint16_t ipc = cpu_state.oldpc;`) et
`:1201-1204` :

```c
default:
        cpu_state.pc = ipc + 1;
        cycles -= 20;
        FETCHCLEAR();
```

Trois défauts :
- (i) `F3 6E` envoie CX octets de DS:SI sur le port DX ; sur le 8088 et le 8086, OUTS n'existe
  pas (il naît avec le 186) et 6Eh est l'alias de JLE, que PCem décode ainsi hors REP (`:2010`) ;
- (ii) il n'y a pas de `case 0x3E` : `REP DS: MOVSB` tombe dans le `default`, qui relance à
  `ipc + 1`, et la chaîne ne s'exécute qu'une fois ;
- (iii) `ipc` est le début de toute l'instruction (les préfixes de segment font `goto
  opcodestart` sans toucher `oldpc`) : avec un préfixe de segment AVANT le REP, le `default`
  relance sur le REP, et l'instruction qui suit perd la surcharge — `26 F3 F7 /7` lit dans DS.

*Effet* : (i) des E/S que le 8088 ne fait pas, et un saut omis ; (ii) une copie d'un seul élément ;
(iii) un opérande lu dans le mauvais segment. Les IDIV « seg REP » du corpus en sont touchés (PB-45).
*Trouvé par* : reconnaissance de G13 (D1-contre A3), à la lecture du C.
*Source* : déduit — un préfixe vaut pour l'instruction qui le suit, dans n'importe quel ordre ; la
carte des opcodes du 8086/8088 n'a pas OUTS (386 PRM § 14.7, point 3 : les opcodes indéfinis du 8086
sont, sur le 386, de nouvelles instructions ou l'exception 6).
*Cas qui discrimine* : (iii) SST 8088 `F6.7`, `F7.7` (IDIV précédés d'un préfixe de segment et d'un REP).
Bancs : `F3 3E A4`, CX = 3 → un octet copié, CX = 3 (PCem) ; trois, CX = 0 (8088). `F3 6E` avec CX = 2
→ deux OUT (PCem) ; un JLE, CX intact (8088).
*G13* : (b) — comportement déduit ; seul (iii) se vérifie par SST, une fois levé le filtre REP de la
sonde (D1-contre K9).
*Reproduit* en mode PCem : `Cpu/808x.cs`, rep() (`case 0x6E`, `case 0x08`, `default`) et la répartition de F2 et F3, marqueurs `fixed in hardware mode: PB-177`.
*Corrigé en mode matériel* (G13.3) : sous `if (materiel.pb_177)`, un REP devant autre chose qu'une chaîne n'est qu'un préfixe : il regarde, sans le lire, le premier octet qui suit ses préfixes de segment (`rep_prefixe_materiel`), et devant autre chose qu'une chaîne l'instruction reprend juste après lui, dans la même instruction (`goto opcodestart` d'execx86) : les préfixes placés avant le REP valent ; 6Eh y passe, alias de JLE ; 08h aussi, que PCem relançait de même ; REP DS: pose DS et répète (`rep_ds_materiel`). Le temps (G13.3, suite) : le prix d'un préfixe de segment du cœur, 4 cycles, la file intacte, et non les 20 cycles et la file vidée de PCem (qui, relançant sur le REP quand un préfixe le précède, le facture alors deux fois). Mesuré contre le silicium (`sst-rep-temps`) : le corpus n'a de REP devant autre chose qu'une chaîne que devant IDIV (`F6.7`, `F7.7`) ; dans les 620 cas du 8088 que SST fait partir d'une file vide, comme la sonde, le REP coûte 7,6 cycles au silicium, 7,8 au cœur (27,8 avant la correction) : écart −0,2 ± 2,1, la porte `sst-rep-temps-materiel`. File de départ pleine (675 cas du 8088, les 261 du 8086), le cœur paie 8,4 ± 2,0 et 6,5 ± 3,4 cycles de plus : la sonde part d'une file vide. SST en mode matériel : les 1 295 REP IDIV du 8088 et les 261 du 8086 passent tous, dont les 630 et 141 qui commencent par REP, que la sonde écartait avant la suite de G13.3. `materiel-cas PB-177` rend la valeur de PCem en mode PCem, celle du matériel en mode matériel, la sonde comptant la correction ; la correction coupée (`--attendu materiel` en mode PCem), il rougit. Les cas : REP 6Eh saute ; REP DS: MOVSB copie trois octets ; ES: REP MOV AX,[0] lit dans ES. IDIV ainsi relancé rend l'opposé de son quotient (`rep_idiv_materiel` relit les préfixes de l'instruction ; l'inversion est faite par la correction d'IDIV, PB-45) : sans PB-45, l'IDIV reste celui de PCem. Le cas : ES: REP IDIV BL, AX = 0010h, BL = 2, AL = 08h (PCem) contre F8h.

### PB-178 — LOCK du 8088 est une instruction d'un octet, pas un préfixe

`808x.c:3489-3492` :

```c
case 0xF0: /*LOCK*/
case 0xF1: /*LOCK alias*/
        cycles -= 4;
        break;
```

`break` et non `goto opcodestart` : la fin d'instruction efface un préfixe de segment placé avant
LOCK (`:3924-3928`), et une interruption ou le pas-à-pas peuvent s'intercaler entre LOCK et
l'instruction qu'il devait verrouiller.

*Effet* : `26 F0 A1 00 00` lit DS:0000 au lieu d'ES:0000 ; un IRQ ou un INT 1 peut tomber entre LOCK
et son instruction. Effet limité : un PC à un seul maître de bus se sert peu de LOCK.
*Trouvé par* : reconnaissance de G13 (D1-contre A3), à la lecture du C.
*Source* : déduit — LOCK est un préfixe de l'instruction qui le suit (« the instruction that follows
it », Intel, page LOCK) ; aucun texte lu ici ne dit ce que fait le 8088 d'une interruption entre un
préfixe et son instruction : inconnu pour ce point.
*Cas qui discrimine* : banc : ES ≠ DS, `26 F0 A1 00 00` → AX lu dans DS (PCem) ; dans ES (8088).
*G13* : (b) — comportement déduit, effet limité ; vérification par banc dirigé seulement.
*Reproduit* : `Cpu/808x.cs:3463`, `case 0xF0`, marqueur `PB-178`.

### PB-179 — Un mot à l'offset FFFFh ne replie pas à l'offset 0 du segment

`808x.c:73-80` (`readmemw`) et `:105-111` (`writememw`) : les deux octets d'un mot sont lus ou
écrits en `s + a` et `s + a + 1`, adresses linéaires, par le chemin rapide (`:79`, `:110`) comme
par le lent (`readmemwl(s + a)`, `:77`).

*Effet* : un mot lu ou écrit à l'offset FFFFh d'un segment prend son octet haut en `s + 10000h`,
le premier octet du segment suivant, au lieu de l'offset 0 du même segment ; de même `PUSH` avec
SP = 1. L'autre facette, le repli à 1 Mo d'un mot en FFFFFh, est celle de PB-07 (D1-contre A7).
*Trouvé par* : reconnaissance de G13 (D1 § 3, PB-87 ; D1-contre A7), à la lecture du C.
*Source* : documenté — « On the 8086, memory operands crossing offset 65,535 or 0 wrap around modulo
65,536 » (386 PRM § 14.7, point 7) ; le repli des adresses à 1 Mo du 8086 (§ 14.7, point 18) relève de
PB-07.
*Cas qui discrimine* : banc C# seul (SST évite vraisemblablement ces offsets, D1-contre C18) : DS =
1000h, AAh en 1FFFFh, 11h en 10000h, 22h en 20000h, `MOV AX,[FFFFh]` → AX = 22AAh (PCem), 11AAh (8088).
*G13* : (a) — chemin chaud, chaque accès mot du 808x ; à faire avec PB-07 (D6), une seule condition
de repli dans readmemw et writememw.
*Reproduit* en mode PCem : `Cpu/808x.cs`, readmemw et writememw, marqueurs `fixed in hardware mode: PB-179`.
*Corrigé en mode matériel* (G13.3, avec PB-07) : sous `if (materiel.pb_179)`, un mot à l'offset FFFFh prend son octet haut à l'offset 0 du segment, en lecture comme en écriture (`readmemw_materiel`, `writememw_materiel`). `materiel-cas PB-179` rend la valeur de PCem en mode PCem, celle du matériel en mode matériel, la sonde comptant la correction ; la correction coupée (`--attendu materiel` en mode PCem), il rougit. Le cas : DS = 1000h, MOV AX,[FFFFh] rend 22AAh (PCem) contre 11AAh. Les gardes sont pliées en mode PCem : readmemw et writememw ont le code machine de M0.

### PB-180 — L'INT 0 du 8088 empile les drapeaux d'avant la division

`808x.c:3595`, `:3637`, `:3730`, `:3754` (DIV et IDIV, octet et mot) :

```c
writememw(ss, (SP - 2) & 0xFFFF, cpu_state.flags | 0xF000);
```

*Effet* : les drapeaux empilés par l'erreur de division sont ceux d'avant l'instruction ; le 8088
empile ceux que le microcode de la division a laissés — mesuré sur AAM 0 (octet bas 46h : ZF et
PF posés, SF, AF et CF effacés, PB-46), déduit pour DIV et IDIV (les 32 cas à diviseur nul de
`F6.7`). Le chemin R9 d'AAM 0 (PB-46, C# seul) empile de même. Sans ce point, détecter le
débordement (PB-169) ne gagne presque rien sur `F6.x` et `F7.x` (D1-contre K6).
*Trouvé par* : reconnaissance de G13 (D1-contre K6), sur la mesure de PB-46.
*Source* : règle inconnue — Intel ne documente pas les drapeaux laissés par une division interrompue ;
mesuré au cas par cas (SST 8088, `D4`). À tirer des vecteurs `F6.6`, `F6.7`, `F7.6`, `F7.7` (la pile
de chaque cas), puis à confirmer sur le corpus 8086.
*Cas qui discrimine* : SST 8088 `D4` (AAM 0, 47 cas) : octet bas des drapeaux empilés égal à celui
d'avant (C#, chemin R9) ; 46h (8088). Puis les cas de débordement de `F6.6` à `F7.7`.
*G13* : (c) — la règle est inconnue : à mesurer sur les vecteurs avant toute correction ; reproduit
d'ici là.
*Reproduit* : `Cpu/808x.cs:3554` (DIV b), `:3586` (IDIV b), `:3670` (DIV w), `:3706` (IDIV w),
marqueurs `PB-180`.

### PB-181 — L'AF d'ADC du cœur 286/386/486 manque quand le quartet bas de l'opérande vaut Fh

`x86_flags.h:299-307`, `AF_SET`, branches `FLAGS_ADC8/16/32` :

```c
case FLAGS_ADC8:
        return ((cpu_state.flags_res & 0xf) < (cpu_state.flags_op1 & 0xf)) ||
               ((cpu_state.flags_res & 0xf) == (cpu_state.flags_op1 & 0xf) && cpu_state.flags_op2 == 0xff);
```

Avec une retenue entrante, le quartet bas du résultat égale celui de `op1` quand le quartet bas de
`op2` vaut Fh, et la retenue sort alors du bit 3. La formule exige `op2 == 0xff` (0xffff,
0xffffffff) : c'est celle du CF (`CF_SET`, `:344-352`), juste sur l'opérande entier, recopiée sur le
quartet sans masquer `op2`. Le SBC du même en-tête est juste (`:317-321`). Défaut distinct de
PB-01, d'une autre formule ; le 286 l'a aussi (`ops_286`, `cpu.c:324`, mêmes en-têtes).

*Effet* : AF manque après ADC quand une retenue entre, que le quartet bas de `op2` vaut Fh et que
`op2` n'est pas FFh (FFFFh, FFFFFFFFh) : 3 840 des 131 072 triplets de 8 bits (2,93 %). Des chiffres
BCD valides ne l'exercent pas, à la différence de PB-01.
*Trouvé par* : reconnaissance de G13 (D1 § 5.2, D1-contre C4), sur la ligne de base SST 386.
*Source* : documenté — AF est la retenue du bit 3 (SDM vol. 1, § 3.4.3.1) ; mesuré, SST 386 (386EX),
« l'AF d'ADC (2 370 cas) », famille F1 (`VERIFICATION.md` § G2) ; AF n'est pas un drapeau indéfini d'ADC.
*Cas qui discrimine* : SST 386 `10`–`15`, `6611`, `6613`, `6615`, `80.2`–`83.2`, `6681.2`, `6683.2`.
Banc : CF = 1, AL = 00h, `ADC AL,0Fh` → AF = 0 (PCem), 1 (386).
*G13* : (a) — documenté et mesuré ; vaut pour le 286, le 386 et le 486.
*Reproduit* en mode PCem : `Cpu/x86_flags.cs`, AF_SET, marqueur `fixed in hardware mode: PB-181` ; l'en-tête du
fichier le dit (corrigé en G13.1 : il attribuait au cœur 386 la mesure du 8088).
*Corrigé en mode matériel* (G13.5) : sous `if (materiel.pb_181)`, dans les trois branches ADC, AF est le bit 4 de
op1 ^ op2 ^ résultat (`af_adc_materiel`, `Cpu/386.Materiel.cs`). Le 286 le partage, mesuré. `materiel-cas PB-181`
rend le cas ci-dessus, et rougit la correction coupée. SST en mode matériel : au 386, `10`, `12`, `14`, `15`, `6615`,
`80.2`, `82.2` entières (2 500 cas chacune), `11`, `13`, `81.2`, `83.2`, `6611`, `6613`, `6681.2`, `6683.2` de 2 487 à
2 493 sur 2 500, le reste étant des mots à cheval sur FFFFh (PB-189) ; au 286, `10`, `12`, `14`, `15`, `80.2`, `82.2` entières (5 000 cas chacune, contre 4 849 à 4 875), `11`, `13`,
`81.2`, `83.2` de 4 948 à 4 966 sur 5 000, le reste étant des mots à cheval sur FFFFh.

### PB-182 — LOCK du cœur 286/386/486 ne lève #UD que devant NOP

`x86_ops_misc.h:701-712`, `opLOCK`, le seul refus en `:707` :

```c
ILLEGAL_ON((fetchdat & 0xff) == 0x90);
```

*Effet* : LOCK devant une instruction qui ne se verrouille pas, ou devant la forme registre d'une
instruction verrouillable, s'exécute comme si LOCK n'était pas là ; le 386 et le 486 lèvent #UD
(INT 6). Environ 3 % des cas de presque toutes les formes du corpus 386 portent un LOCK.
*Trouvé par* : reconnaissance de G13 (D1 § 5.2, D1-contre C5), sur la ligne de base SST 386.
*Source* : documenté pour le 386 — « An undefined-opcode exception (interrupt 6) results from using LOCK
before any other instruction » (386 PRM § 14.7, point 9 ; page LOCK : les seules formes « that reference
memory ») ; 486 : déduit (SDM) ; 286 : inconnu, le corpus SST 80286 tranchera.
*Cas qui discrimine* : SST 386, famille E1 (13 860 échecs, 125 formes). Banc : `F0 01 C0` (LOCK ADD
AX,AX, forme registre) → exécuté (PCem) ; INT 6, IP sur le préfixe (386).
*G13* : (a) pour le 386 et le 486, gardé par UC : `opLOCK` sert aussi au 286 (`386_ops.h:10968-10969`),
dont le comportement est inconnu (D1-contre A5).
*Reproduit* en mode PCem : `Cpu/386_ops_prefix.cs`, opLOCK, marqueur `fixed in hardware mode: PB-182`.
*Corrigé en mode matériel* (G13.5) : sous `if (materiel.pb_182)`, sur le 386 et le 486, LOCK lit les préfixes qui le
suivent jusqu'à l'opcode et lève #UD hors de la liste ou devant une forme registre (`lock_materiel`,
`Cpu/386.Materiel.cs`). Le 286, lui, exécute LOCK devant tout, forme registre, CMP et MOV compris : SST 286, une
quarantaine de cas par forme, aucun #UD (G13.5) ; il garde le comportement de PCem. `materiel-cas PB-182` rend le cas
ci-dessus et un témoin (LOCK ADD [BX],AX, exécuté dans les deux modes), et rougit la correction coupée. SST 386 en
mode matériel : les cas LOCK de toutes les formes (`10` entière, 2 500 cas, contre 2 408).

### PB-183 — BT, BTS, BTR et BTC déplacent l'adresse d'un décalage non signé

`x86_ops_bit.h:8`, `:28`, `:48`, `:68` (BT) et `:92`, `:119`, `:146`, `:173` (la macro `opBT` de
BTS, BTR et BTC) :

```c
cpu_state.eaaddr += ((cpu_state.regs[cpu_reg].w / 16) * 2);
```

*Effet* : en forme mémoire, un registre de décalage négatif (8000h à FFFFh en 16 bits, bit 31 posé en
32 bits) adresse en avant, jusqu'à 8 Ko (512 Mo) plus loin, au lieu d'en arrière : CF lit — et BTS,
BTR, BTC écrivent — un autre mot. La forme registre n'est pas touchée.
*Trouvé par* : reconnaissance de G13 (D1 § 5.2, D1-contre K7), à la lecture du C.
*Source* : documenté — « If BitBase is a memory address, BitOffset can range from -2 gigabits to 2
gigabits » (386 PRM § 17.2, Bit(BitBase, BitOffset)) ; SDM vol. 2, Table 3-2. Il n'explique qu'une
partie des écarts de CF mesurés sur BT* (≈ 1 150 à 1 350 cas par forme) : le reste n'est pas lu (K7).
*Cas qui discrimine* : banc, mode réel, 386 : AX = FFFFh, BX = 0100h, `BT [BX],AX` → bit 15 du mot
en DS:20FEh (PCem) ; en DS:00FEh (386). SST 386 `0FA3`, `0FAB`, `0FB3`, `0FBB` : seulement après K7 et K8.
*G13* : (a) — documenté et vérifiable par un banc ; la mesure SST ne départagera qu'une fois le reste
des écarts de CF instruit.
**Élargi en G13.5** : sous une adresse de 16 bits, l'adresse du mot, déplacement compris, replie dans les 64 Ko du
segment, comme toute adresse effective de 16 bits ; PCem la laisse déborder au-delà de FFFFh (mesuré : SST 386,
`bt [ss:bp+di-77h],ax`, AX = 92F2h, l'adresse 0B1Bh − DA2h lue en FD79h par le 386EX, en FFFFFD79h par PCem). Avec
ce repli, les « autres écarts de CF » de K7 disparaissent : il n'en reste aucun.
*Reproduit* en mode PCem : `Cpu/386_ops_bit.cs`, OpBTx (BTS, BTR, BTC) et les quatre BT, marqueurs `fixed in hardware
mode: PB-183`.
*Corrigé en mode matériel* (G13.5) : sous `if (materiel.pb_183)`, après le déplacement de PCem, la différence avec le
décalage signé (2000h de moins en 16 bits, 20000000h en 32 bits), puis le repli sous une adresse de 16 bits
(`bt_decalage_materiel`, `Cpu/386.Materiel.cs`). `materiel-cas PB-183` rend le cas ci-dessus et celui du repli (BX =
0, AX = FFFFh : DS:FFFEh), et rougit la correction coupée. SST 386 en mode matériel : `0FA3`, `0FAB`, `0FB3`, `0FBB`,
`660FA3`, `660FAB`, `660FB3`, `660FBB` entières (2 500 cas chacune, contre 1 056 à 1 952).

### PB-184 — MOVSX r16,r/m16 est un opcode illégal

`386_ops.h:1432` (table `386_0f`, opérande 16 bits et adresse 16 bits, entrée `BF`) et `:1958`
(adresse 32 bits) : `ILLEGAL`. La forme r32 (`66 0F BF`) existe, `opMOVSX_l_w`.

*Effet* : `0F BF` en taille d'opérande 16 bits lève #UD (INT 6) ; le 386 mesuré l'exécute.
*Trouvé par* : reconnaissance de G13 (D1 § 5.2, D1-contre C6), sur la ligne de base SST 386.
*Source* : mesuré seulement — SST 386 (386EX) : `0FBF` à 2,7 %, `670FBF` à 2,3 %, `660FBF` à 96,8 % ;
`80386.csv` la décrit « MOVSX r16/32, r/m16 ». Les manuels d'Intel (386 PRM, SDM) ne listent que la
forme r32 : de ce côté, inconnu.
*Cas qui discrimine* : SST 386 `0FBF`, `670FBF` (famille R). Banc : BX = 8001h, `0F BF C3` (MOVSX
AX,BX) → INT 6 (PCem) ; AX = 8001h (386EX ; déduit : un mot étendu vers 16 bits est une copie).
*G13* : (a) — mesuré ; demande un handler neuf dans une table générée (`386_ops_table386.cs`,
`tools/ops386-table.py`), donc une table du mode posée par `cpu_set` (D1-contre A10).
*Reproduit* en mode PCem : par la table générée, `Cpu/386_ops_table386.cs:717` et `:1069` (ILLEGAL, sans marqueur :
fichier généré) ; marqueur `fixed in hardware mode: PB-184` dans `Cpu/386_ops_movx.cs` (PoserGroupe_movx_0f_386).
*Corrigé en mode matériel* (G13.5) : `cpu_set` pose, sous `if (materiel.pb_184)`, la table 0F du mode, copie de celle
de PCem où 0BFh et 2BFh copient le mot comme MOVZX r16,r/m16 (`table_0f_materiel`, `Cpu/386.Materiel.cs`) ; la table de
PCem reste intacte. `materiel-cas PB-184` rend le cas ci-dessus, et rougit la correction coupée. SST 386 en mode
matériel : `0FBF` 2 487 sur 2 500 (contre 68), `670FBF` 2 082 (contre 57), le reste étant des mots à cheval sur FFFFh
ou au-delà (PB-189).

### PB-185 — AAA et AAS du cœur 286/386/486 ajustent à la façon du 8086

`x86_ops_bcd.h:3-15` (opAAA : `AL += 6; AH++;`, `:6-7`) et `:41-53` (opAAS : `AL -= 6; AH--;`,
`:44-45`). La retenue d'AL + 6 ne passe pas dans AH, ni l'emprunt d'AL − 6.

*Effet* : AAA avec AL ≥ FAh à ajuster : le SDM ajoute 106h à AX (AH + 2), PCem un seul à AH ; AAS avec
AL < 6 : le SDM retire 6 d'AX puis 1 d'AH (AH − 2), PCem un seul.
*Trouvé par* : reconnaissance de G13 (D1 § 5.2, D1-contre C7), sur la ligne de base SST 386.
*Source* : contradiction documentée — SDM vol. 2, AAA (`AX := AX + 106H`) et AAS (`AX := AX – 6; AH :=
AH – 1`) ; 386 PRM, AAA (`AL := (AL + 6) AND 0FH; AH := AH + 1`) et AAS (`AL := AL - 6; …; AH := AH - 1`),
ce que fait PCem. La mesure (386EX) suit le SDM : `37` à 91,6 %, `3F` à 93,2 %, EAX divergent (déduit).
*Cas qui discrimine* : SST 386 `37`, `3F` (famille R). Banc : AX = 00FAh, AF = 1, `AAA` → AX = 0100h
(PCem) ; 0200h (SDM, 386EX). AX = 0102h, AF = 1, `AAS` → AX = 000Ch (PCem) ; FF0Ch (SDM).
*G13* : (a) pour le 386 et le 486, sur la mesure du 386EX, qui tranche entre les manuels ; gardé par
UC : le 286 partage ces handlers, son comportement est inconnu (D1-contre A5).
*Reproduit* en mode PCem : `Cpu/386_ops_bcd.cs`, AAA et AAS, marqueurs `fixed in hardware mode: PB-185`.
*Corrigé en mode matériel* (G13.5) : sous `if (materiel.pb_185)`, après l'ajustement de PCem, la retenue d'AL + 6 (ou
l'emprunt d'AL − 6) passe dans AH (`aaa_materiel`, `aas_materiel`, `Cpu/386.Materiel.cs`). Le 286 suit le SDM lui
aussi, mesuré. `materiel-cas PB-185` rend les cas ci-dessus, et rougit la correction coupée. SST en mode matériel :
`37` et `3F` entières au 386 (2 500 cas chacune, contre 2 290 et 2 331) ; au 286, `37` et `3F` entières aussi (5 000 cas chacune, contre 4 641 et 4 817).

### PB-186 — AAD et AAM du cœur 286/386/486 posent SF et ZF d'après AX

`x86_ops_bcd.h:23` (AAD) et `:35` (AAM) : `setznp16(AX);`, là où les drapeaux suivent AL.

*Effet* : AAD : SF toujours nul (AH vaut 0 ; ZF est juste) ; AAM : ZF faux quand AL = 0 et AH ≠ 0,
SF faux pour une base atypique. `D5` passe à 50,2 % : le profil d'un SF faux une fois sur deux.
*Trouvé par* : reconnaissance de G13 (D1 § 5.2, D1-contre C8), sur la ligne de base SST 386.
*Source* : documenté — « The SF, ZF, and PF flags are set according to the resulting binary value in the
AL register » (SDM vol. 2, pages AAD et AAM) ; `80386.csv` donne `f_umask` 0xF7EE : SF, ZF et PF
comparés.
*Cas qui discrimine* : SST 386 `D5` (50,2 %), `D4` (84,8 %, famille F1). Bancs de PB-175 :
AL = 14h, `AAM 0Ah` → ZF = 0 (PCem), 1 (386) ; AH = 01h, AL = 78h, `AAD 0Ah` → SF = 0 (PCem), 1 (386).
*G13* : (a) — documenté et mesuré ; même défaut que le 8088 (PB-175).
*Reproduit* en mode PCem : `Cpu/386_ops_bcd.cs`, AAD et AAM, marqueurs `fixed in hardware mode: PB-186`.
*Corrigé en mode matériel* (G13.5) : sous `if (materiel.pb_186)`, après le `setznp16(AX)` de PCem, SF, ZF et PF
d'après AL (`aad_aam_materiel`, `Cpu/386.Materiel.cs`). Le 286 le partage, mesuré. `materiel-cas PB-186` rend les cas
ci-dessus, et rougit la correction coupée. SST en mode matériel : `D5` entière au 386 (2 500 cas, contre 1 256) ;
`D4` 2 490 sur 2 500, les dix restants étant AAM 0 (PB-187) ; au 286, `D5` entière (5 000 cas, contre 2 647), `D4` 4 991, les neuf restants étant AAM 0.

### PB-187 — AAM 0 du cœur 286/386/486 prend la base 10 au lieu de lever #DE

`x86_ops_bcd.h:31-32` :

```c
if (!base || cpu_manufacturer != MANU_INTEL)
        base = 10;
```

*Effet* : `D4 00` divise AL par 10 au lieu de lever #DE (INT 0). PB-46 le décrit (`PCEM_BUGS.md`,
section B) sans l'inscrire comme défaut du cœur 386.
*Trouvé par* : reconnaissance de G13 (D1 § 5.3, D1-contre K4), sur la ligne de base SST 386.
*Source* : documenté — « #DE If an immediate value of 0 is used » (SDM vol. 2, page AAM) ; les cas à
immédiat nul de `D4` l'attendent (SST 386, 386EX).
*Cas qui discrimine* : SST 386 `D4` (≈ 1/256 des 2 500 cas). Banc : AL = 2Ah, `AAM 0` → AX = 0402h,
rien d'empilé (PCem) ; #DE, l'adresse de l'AAM empilée (386 : la faute pointe l'instruction, 386 PRM
§ 14.7, point 2).
*G13* : (a) — documenté, vérifiable par SST ; même famille « erreur de division » que PB-46 et
PB-169.
*Reproduit* en mode PCem : `Cpu/386_ops_bcd.cs`, opAAM, marqueur `fixed in hardware mode: PB-187`.
*Corrigé en mode matériel* (G13.5) : sous `if (materiel.pb_187)`, une base nulle lève #DE par `x86_int(0)`, pc remis
sur l'AAM (`aam0_materiel`, `Cpu/386.Materiel.cs`). Le 286 le lève aussi, mesuré. `materiel-cas PB-187` rend le cas
ci-dessus, l'IP empilé compris, et rougit la correction coupée. SST : les cas AAM 0 lèvent #DE au 386 comme au 286,
EAX intact ; mais ils échouent encore, sur les drapeaux. Le silicium laisse SF et ZF à 0 et PF à une valeur que rien
ne documente (au 286, la parité d'AL sur les quatre cas lus ; au 386, aucune règle trouvée sur douze) : inconnu,
non imité (décision n° 14). Dix cas au 386, neuf au 286.

### PB-188 — Le DAS du cœur 286/386/486 s'écarte du SDM

`x86_ops_bcd.h:81-105`, opDAS, le second test en `:92` :

```c
if ((cpu_state.flags & C_FLAG) || (AL > 0x9f)) {
```

Comme celui du 8088 (PB-171), le second test reprend le CF de l'emprunt d'en bas et compare
l'AL déjà ajusté ; le SDM teste l'AL et le CF d'origine. Le DAA du même cœur, identique au SDM,
passe à 100 %.

*Effet* : AL et CF faux dans 24 des 1 024 entrées (AL, AF, CF).
*Trouvé par* : reconnaissance de G13 (D1-contre A4), sur la ligne de base SST 386.
*Source* : documenté — SDM vol. 2, page DAS (`old_AL > 99H or old_CF = 1`) ; mesuré, SST 386 (386EX),
`2F` à 95,6 % (famille R).
*Cas qui discrimine* : SST 386 `2F`. Banc : AL = 01h, AF = 1, CF = 0, `DAS` → AL = 9Bh (PCem) ; FBh
(SDM, 386EX).
*G13* : (a) — documenté et mesuré.
*Reproduit* en mode PCem : `Cpu/386_ops_bcd.cs`, opDAS, marqueur `fixed in hardware mode: PB-188`.
*Corrigé en mode matériel* (G13.5) : sous `if (materiel.pb_188)`, DAS selon le SDM, l'emprunt du premier pas posant
CF (`das_materiel`, `Cpu/386.Materiel.cs`) — à la différence du 8088 (PB-171), où seul le second pas le décide. Le 286
suit le SDM lui aussi, mesuré. `materiel-cas PB-188` rend le cas ci-dessus, et rougit la correction coupée. SST en
mode matériel : `2F` entière au 386 (2 500 cas, contre 2 390) ; au 286, `2F` entière aussi (5 000 cas, contre 4 834).

### PB-189 — En mode réel, la limite des données n'est contrôlée que par les MOV

`386_common.h:58-72` : `SEG_CHECK_READ` et `SEG_CHECK_WRITE` ne testent que le segment nul
(`base == 0xffffffff`), et ce sont les seules gardes de la plupart des handlers. `CHECK_READ`,
`CHECK_WRITE` et `CHECK_WRITE_REP` (`:74-92`), qui comparent l'adresse à `limit_low` et
`limit_high`, ne servent qu'aux MOV de `x86_ops_mov.h` (23 sites), à `x86_ops_misc.h:50`, à six REP
(`x86_ops_rep.h`) et au FSTP m64 (`x87_ops_loadstore.h:459`, `:475`).

*Effet* : en mode réel, un opérande dont l'adresse effective dépasse FFFFh (adressage 32 bits) ou
qui est à cheval sur l'offset FFFFh est lu ou écrit hors du segment, sans #GP ni #SS. Mesuré : SST
386, familles E2 (75 858 échecs, 194 formes `67…`) et E3 (1 838 échecs, 48 formes) ; les MOV a32
(`6788`–`678B`), qui contrôlent, passent à ≈ 94 %, le reste de E2 à ≈ 82 % (D1-contre K10).
*Trouvé par* : reconnaissance de G13 (D1 § 5.2, D1-contre K10), sur la ligne de base SST 386.
*Source* : documenté — exception 13 (segment de données) ou 12 (segment de pile) pour un opérande qui
franchit l'offset 65 535 ou 0 (386 PRM § 14.7, point 7) ; « Interrupt 13 if any part of the operand
would lie outside of the effective address space from 0 to 0FFFFH » (386 PRM, pages d'instruction).
*Cas qui discrimine* : SST 386, familles E2 et E3. Banc, mode réel : BX = FFFFh, `ADD [BX],BX` → mot
lu et écrit en DS:FFFFh et DS:10000h (PCem) ; INT 0Dh (386). EAX = 10000h, `67 01 00` → idem.
*G13* : (a) — documenté et mesuré ; mais un contrôle sur chaque accès mémoire, le chemin le plus chaud
du cœur : son coût est à décider avant G13.5, qui ne le prévoit pas (D1, question 6).
*Reproduit* : `Cpu/386_common.cs:515`, SEG_CHECK_READ et SEG_CHECK_WRITE, marqueur `PB-189`.

### PB-190 — LTR ne contrôle rien

`x86_ops_pmode.h:230-262`, op0F00_common, `case 0x18` :

```c
addr = (sel & ~7) + gdt.base;
...
access |= 2;
writememb(0, addr + 5, access);
...
tr.seg = sel;
```

LTR lit toujours la GDT, garde dans `tr.seg` le bit TI du sélecteur, et ne teste ni le sélecteur
nul, ni la limite de la GDT, ni le type (une TSS disponible), ni la présence : le descripteur lu,
quel qu'il soit, reçoit le bit 1 de son octet d'accès.

*Effet* : LTR charge TR depuis un segment de données, une TSS occupée ou absente, ou d'après un
sélecteur à TI = 1 (lu dans la GDT quand même), et modifie le descripteur ; avec TI = 1, la
commutation suivante cherche le bit occupé dans la LDT (PB-42).
*Trouvé par* : reconnaissance de G13 (D1 § 4, PB-42 ; D1-contre C19), à la lecture du C.
*Source* : documenté — LTR lève « #GP(selector) if the object named by the source selector is not a TSS
or is already busy », « #NP(selector) if the TSS is marked "not present" » (386 PRM, page LTR) ;
« TSS descriptors may reside only in the GDT » (386 PRM § 7.2).
*Cas qui discrimine* : pm-check, CPL 0 : `LTR` sur le sélecteur d'un segment de données → TR chargé,
descripteur modifié (PCem) ; #GP(sélecteur) (386). Idem avec TI = 1, ou une TSS déjà occupée.
*G13* : (a) — documenté ; rend PB-42 inatteignable.
*Reproduit* : `Cpu/386_ops_0f.cs:570`, op0F00_common (LTR), marqueur `PB-190`.

### PB-191 — La voie TSS des CALL, JMP et INT en accepte trop

`x86seg.c:1284-1291` (loadcscall) et `:761-773` (loadcsjmp), après une lecture du descripteur dans
la LDT si `seg & 4` (`:888-894`, `:582-588`) ; la porte de tâche de `pmodeint`, `:1963-1999`, qui lit
la TSS dans la LDT si `seg & 4` (`:1967-1973`) et ne teste que sa présence (`:1990`).

*Effet* : un CALL ou un JMP directement sur une TSS ne contrôle ni le DPL de la TSS contre CPL et RPL,
ni sa présence, ni que son sélecteur désigne la GDT : une TSS de la LDT, d'un privilège insuffisant
ou absente est commutée. La porte de tâche de l'IDT accepte une TSS de la LDT et ne vérifie pas que
le descripteur désigné est une TSS disponible.
*Trouvé par* : reconnaissance de G13 (D1 § 3, PB-39 ; D1-contre A9), à la lecture du C.
*Source* : documenté — 386 PRM, pages CALL et JMP, branches TASK-STATE-SEGMENT (« TSS DPL must be
>= CPL », « >= RPL », « must be present ») et TASK-GATE (« Must specify global in the local/global bit ») ;
page INT, TASK-GATE (« AR byte must specify available TSS »). Exceptions : #TS (CALL, INT), #GP (JMP), #NP.
*Cas qui discrimine* : pm-check, CPL 3 : `CALL FAR` sur une TSS 386 de DPL 0 → commutation (PCem) ;
#TS(sélecteur de la TSS) (386 PRM, page CALL ; #GP selon la page JMP et le SDM). TSS absente :
commutation contre #NP.
*G13* : (a) pour les conditions ; (b) pour les exceptions, que les manuels contredisent (comme PB-39).
*Reproduit* : `Cpu/x86seg.cs:724` (loadcscall), `:2548` (loadcsjmp), `:1418` (pmodeint, porte de
tâche), marqueurs `PB-191`.

### PB-192 — `pmodeint` : EXT jamais posé, et la limite de l'IDT testée sur le premier octet

`x86seg.c:1648` et `:1687` :

```c
if (addr >= idt.limit) {
...
        x86gpf(NULL, (num * 8) + 2);
```

La porte occupe huit octets, de `addr` à `addr + 7`, mais seul `addr` est comparé à la limite. Et le
code d'erreur d'une porte de type nul (`:1687`) n'a jamais le bit EXT, même quand l'événement est
externe (`soft == 0`) ; celui de `:1660` le portait dans son expression voulue (PB-32). Le test de DPL
(`:1692`) ne vaut que pour une interruption logicielle, où EXT est bien nul.

*Effet* : une porte dont les derniers octets dépassent la limite est lue au-delà ; un #GP sur une
porte invalide, pour une interruption matérielle ou une exception, porte n × 8 + 2 au lieu de
n × 8 + 3.
*Trouvé par* : reconnaissance de G13 (D1 § 4, PB-32 ; D1-contre § 5.2), à la lecture du C.
*Source* : documenté — « Interrupt vector must be within IDT table limits, else #GP(vector number *
8+2+EXT) » (386 PRM, page INT) ; le SDM compare `(vector_number « 3) + 7` à la limite (page INT n) ;
« The processor sets the EXT bit if an event external to the program caused the exception » (§ 9.7).
*Cas qui discrimine* : pm-check : limite de l'IDT 00FCh, `INT 1Fh` → porte lue (PCem) ; #GP(00FAh)
(386). Interruption matérielle sur une porte de type nul → #GP(n × 8 + 2) (PCem) ; n × 8 + 3 (386).
*G13* : (a) — documenté ; à faire avec PB-32.
*Reproduit* : `Cpu/x86seg.cs:1095` (le test de limite) et `:1143` (le code d'erreur), pmodeint,
marqueurs `PB-192`.

### PB-193 — LOADALL386 ne contrôle pas le privilège

`x86_ops_misc.h:930-974`, `opLOADALL386` : aucune garde, là où `opLOADALL`, celui du 286, teste
`CPL && (cr0 & 1)` (`:828-831`) avant de lever #GP(0).

*Effet* : en mode protégé, à un niveau de privilège autre que 0, LOADALL386 charge tout l'état (CR0,
EFLAGS, EIP, les registres, les dix caches de descripteur) depuis ES:EDI : un programme de niveau 3
prend la main sur la machine émulée.
*Trouvé par* : reconnaissance de G13 (D1 § 3, PB-78 ; D1-contre C19), à la lecture du C.
*Source* : source secondaire — « Attempting to execute LOADALL at any other privilege level will
generate an exception 13 » (R. Collins, *The LOADALL Instruction*, rcollins.org) ; LOADALL n'est pas
documenté par Intel.
*Cas qui discrimine* : pm-check, cœur 386, CPL 3 : `0F 07`, ES:EDI sur un bloc préparé → état chargé
(PCem) ; #GP(0) (386, selon Collins).
*G13* : (b) — source secondaire seule, sans mesure.
*Reproduit* : `Cpu/386_ops_0f.cs:817`, opLOADALL386, marqueur `PB-193`.

### PB-194 — FXTRACT n'existe pas : D9 F4 est FPU_ILLEGAL

`x87_ops.h:356` (et `:394` en a32), la rangée D9 F0-F7 : `opF2XM1, opFYL2X, opFPTAN, opFPATAN, ILLEGAL, opFPREM1, …` —
D9 F4 décode, compte `timing_rr` et ne fait rien (`FPU_ILLEGAL_a16`, `:297-302`). `x87_ops_*.h` n'a aucun `opFXTRACT` ;
le champ `fxtract` des quatre tables de temps (`x87_timings.c`) n'est lu par personne.
*Effet* : FXTRACT ne sépare rien : la pile ne bouge pas, TOP ne descend pas, ST(0) garde la valeur d'origine ; un
calcul de logarithme ou de mise à l'échelle qui passe par FXTRACT continue avec une pile décalée.
*Trouvé par* : reconnaissance de G13 (D2 § 7, confirmé par D2-contre C7).
*Source* : SDM vol. 2, FXTRACT (l'exposant en ST(1), la significande poussée en ST(0) ; 0 → ZE et ST(1) = −∞) ;
SDM vol. 3 (2016) § 22.18.7.12 et 387 PRM annexe C (8087 et 287 : 0 sans exception) — documenté.
*Cas qui discrimine* : FNINIT ; FLDZ ; FLD m64 = 8,0 (4020000000000000) ; D9 F4 ; FSTP m64 ; FSTP m64 → PCem
4020000000000000 puis 0000000000000000 (le zéro de FLDZ), silicium 3FF0000000000000 (1,0) puis 4008000000000000 (3,0).
*G13* : (a) — un gestionnaire neuf avec ceux du noyau, après le point de décision n° 10 (exact aussi en double).
*Reproduit* : `Cpu/x87_ops_tables.cs` (`Table_fpu_d9_a16` et `_a32`, rangée F0) et `Cpu/x87_ops_808x_tables.cs`
(`ops_808x_fpu_d9_a16`), marqueurs PB-194 (générés par `gentab.py` et `gen46.py`).

### PB-195 — FBSTP tronque au lieu d'arrondir, sans IE ni BCD indéfini

`x87_ops_loadstore.h:141-200` : |ST(0)| est décomposé par `floor(fmod(tempd, 10.0))` puis `tempd /= 10.0`
(`:152-160`, `:182-190`) : les chiffres sont ceux de la partie entière, tronquée, quel que soit RC ; aucune borne :
au-delà de 10^18 − 1, le dix-neuvième chiffre tombe dans l'octet de signe (`:161-164`) ; un infini ou un NaN donne
des chiffres nuls (`fmod` rend NaN, cvttsd2si 80000000h, l'octet 00) ; au-delà de 2^53, les divisions par 10 sont
inexactes et les chiffres faux.
*Effet* : FBSTP de 12,7 écrit 12 ; de 10^18, l'octet de signe 01h ; d'un NaN ou de +∞, un zéro BCD (de −∞, −0) ; jamais
IE.
*Trouvé par* : reconnaissance de G13 (D2 § 7, D2-contre C7).
*Source* : SDM vol. 2, FBSTP (arrondi à l'entier selon RC ; trop grand, ∞, NaN : #IA, et masquée l'indéfini BCD) ; SDM
vol. 1 § 4.7 (l'indéfini BCD compacté, FFFF C000 0000 0000 0000h) — documenté.
*Cas qui discrimine* : FLD m64 = 12,7 (4029666666666666) ; FBSTP m80 → PCem 12 00 00 00 00 00 00 00 00 00, silicium
(au plus près) 13 00 … 00 ; FLD m64 = 10^18 (43ABC16D674EC800) ; FBSTP → PCem 00 × 9 puis 01, silicium 00 × 7, C0 FF
FF et IE.
*G13* : (a) — avec les gestionnaires du noyau, après le point de décision n° 10 ; PB-53 (la globale `tempc`) s'y éteint.
*Reproduit* : `Cpu/x87_ops_loadstore.cs`, `FBSTP_a16` et `FBSTP_a32`, marqueurs PB-195, et leurs copies de
`Cpu/x87_ops_808x.cs`.

### PB-196 — FIST m16 et m32 hors bornes : les bits bas, ni l'indéfini entier ni IE

`x87_ops_loadstore.h:46`, `:60`, `:75`, `:92` : `seteaw((int16_t)temp64)` ; `:282`, `:296`, `:311`, `:328` :
`seteal((int32_t)temp64)` — l'entier de 64 bits de `x87_fround` tronqué ; PCem a commenté son propre contrôle
(`fatal`, `:44-45`, `:280-281`…). FISTP m64 hors bornes tombe juste par hasard : `x87_fround` y rend 8000000000000000h
(cvttsd2si), qui est l'indéfini entier ; IE manque partout (D2-contre A4-k).
*Effet* : FIST m16 de 40 000 écrit 9C40h (−25 536) ; FIST m32 de 3·10^9, B2D05E00h ; un programme qui contrôle ses
conversions par IE ou par l'indéfini ne voit jamais le débordement.
*Trouvé par* : reconnaissance de G13 (D2 § 7, D2-contre C7).
*Source* : SDM vol. 2, FIST/FISTP (trop grand, ∞, NaN : #IA ; masquée, l'indéfini entier, 8000h, 80000000h ou
8000000000000000h) ; Numerics Supplement (1980) — documenté.
*Cas qui discrimine* : FLD m64 = 40 000,0 (40E3880000000000) ; FISTP m16 → PCem 9C40h et IE = 0, silicium 8000h et
IE = 1 ; FLD m64 = 3·10^9 (41E65A0BC0000000) ; FISTP m32 → PCem B2D05E00h, silicium 80000000h et IE.
*G13* : (a) — avec les gestionnaires du noyau, après le point de décision n° 10 ; IE avec PB-59.
*Reproduit* : `Cpu/x87_ops_loadstore.cs`, `opFISTiw_*`, `opFISTPiw_*`, `opFISTil_*`, `opFISTPil_*` (huit gestionnaires),
marqueurs PB-196, et leurs copies de `Cpu/x87_ops_808x.cs`.

### PB-197 — FRNDINT de |x| ≥ 2^63, d'un infini ou d'un NaN rend −2^63

`x87_ops_misc.h:708`, `ST(0) = (double)x87_fround(ST(0));` ; `x87_ops.h:60-82` : `(int64_t)floor(b)` hors des bornes
d'un `int64_t`, compilé en cvttsd2si, vaut 8000000000000000h ; les comparaisons de l'arrondi au plus près rendent alors
`c` (2^63, ±∞) ou `a` (NaN), tous deux −2^63.
*Effet* : FRNDINT de 2^63, de +∞, de −∞ ou d'un NaN rend −9,223372036854775808·10^18 (C3E0000000000000).
*Trouvé par* : reconnaissance de G13 (D2 § 7, D2-contre C7).
*Source* : SDM vol. 2, FRNDINT (arrondi selon RC ; une valeur déjà entière et un infini restent inchangés ; un SNaN :
#IA, un QNaN propagé) — documenté.
*Cas qui discrimine* : FLD m64 = 2^63 (43E0000000000000) ; FRNDINT ; FSTP m64 → PCem C3E0000000000000, silicium
43E0000000000000 ; le même C3E0000000000000 chez PCem pour +∞ (7FF0000000000000) et pour 7FF8000000000000.
*G13* : (a) — avec les gestionnaires du noyau, après le point de décision n° 10 (exact aussi en double).
*Reproduit* : `Cpu/x87_ops_misc.cs`, `opFRNDINT`, marqueur PB-197 (généré par `gen44.py`), et sa copie de
`Cpu/x87_ops_808x.cs` ; la cause, `x87_fround` (`Cpu/x87_ops.cs`, CvtI64), est partagée avec FIST.

### PB-198 — FSCALE avec ST(1) NaN ou +∞ rend 0 ; le domaine du 287 n'est pas contrôlé

`x87_ops_misc.h:722`, `temp64 = (int64_t)ST(1);` : un NaN, un infini ou |ST(1)| ≥ 2^63 donnent −2^63 (cvttsd2si) ;
`:723-724`, `ST(0) * pow(2.0, −9,2·10^18)` vaut alors ±0. Le domaine du 287 (−2^15 ≤ ST(1) < 2^15) n'est pas contrôlé.
*Effet* : FSCALE(1,0 ; NaN) rend 0 au lieu du NaN ; FSCALE(1,0 ; +∞) rend 0 au lieu de +∞ (−∞ rend 0, juste par
hasard).
*Trouvé par* : reconnaissance de G13 (D2 § 7, D2-contre C7) ; le cas de +∞, lu à la ligne de C.
*Source* : SDM vol. 2, FSCALE (ST(1) tronqué vers zéro ; table des résultats : ST(1) NaN → NaN, +∞ → ±∞) ; 287 PRM
(domaine −2^15 ≤ ST(1) < 2^15, « undefined » hors domaine) — documenté ; la valeur hors domaine du 287 : inconnue.
*Cas qui discrimine* : ST(0) = 1,0, ST(1) = 7FF8000000000000 ; FSCALE ; FSTP m64 → PCem 0000000000000000, silicium
7FF8000000000000 ; ST(1) = +∞ → PCem 0000000000000000, silicium 7FF0000000000000.
*G13* : (a) — avec les gestionnaires du noyau ; le domaine du 287 : avec les 16 bits ; hors domaine : (c).
*Reproduit* : `Cpu/x87_ops_misc.cs`, `opFSCALE`, marqueur PB-198 (généré par `gen44.py`), et sa copie de
`Cpu/x87_ops_808x.cs`.

### PB-199 — L'image FSAVE d'un registre TAG_UINT64 : l'entier suivi de 5555h, étiqueté « spécial »

`x87_ops.h:152-161` (`x87_st_fsave` : `MM[reg].q` puis `0x5555`), `:163-175` (`x87_ld_frstor` : relu comme entier si
la marque `0x5555` et TAG_UINT64 s'y trouvent), `x87.c:37-38` (`x87_gettag` : TAG_UINT64 → 10). TAG_UINT64 est la
rustine de FILD m64 (`x87.h:29-30`, `x87_ops_loadstore.h:113-115`).
*Effet* : après FILD m64, FSAVE écrit pour ce registre un entier de 64 bits suivi de 5555h au lieu du réel de 80 bits,
et 10 (« spécial ») dans le mot d'étiquettes ; un débogueur, un gestionnaire d'exceptions ou un changement de tâche
qui lit l'image voit des octets que le silicium n'écrit pas. L'aller-retour FSAVE / FRSTOR, lui, reste cohérent.
*Trouvé par* : reconnaissance de G13 (D2 § 7, D2-contre C7).
*Source* : SDM vol. 1 § 8.1.10 (l'image FSAVE : huit réels de 80 bits) et § 8.1.7 (étiquettes : 00 valide, 01 zéro, 10
spécial) — documenté.
*Cas qui discrimine* : FNINIT ; FILD m64 = 5 ; FNSAVE (16 bits, réel) → le registre en +14 : PCem 05 00 00 00 00 00 00
00 55 55, silicium 00 00 00 00 00 00 00 A0 01 40 (5,0) ; le mot d'étiquettes : PCem BFFFh, silicium 3FFFh.
*G13* : (a) — avec le noyau (la rustine TAG_UINT64 disparaît), ou dès « A+ ».
*Reproduit* : `Cpu/x87_ops.cs` et `Cpu/x87_8087.cs`, `x87_st_fsave` et `x87_ld_frstor` ; `Cpu/x87.cs`, `x87_gettag` ;
marqueurs PB-199.

### PB-200 — FNSTSW AX sur le 8087 écrit AX

`x87_ops.h:916`, la rangée DF E0 de `fpu_df_a16` (`opFSTSW_AX`), que `8087.h:86` recompile pour le 8087 ;
`x87_ops_misc.h:24-32`.
*Effet* : sur un 8088 + 8087, DF E0 écrit le mot d'état dans AX ; un programme qui reconnaît le 287 à ce que FNSTSW AX
change AX croit le trouver.
*Trouvé par* : reconnaissance de G13 (D2 § 7, D2-contre C7 ; relevé aussi par 86Box, ticket #4518).
*Source* : 287 PRM (FSTSW AX, « a special 80287 instruction ») ; Numerics Supplement (1980 : le 8087 n'échange avec
l'UC que par la mémoire) — documenté ; AX inchangé sur le 8088 + 8087 : déduit ; ce que le 8087 fait de DF E0 : inconnu.
*Cas qui discrimine* : 8088 + 8087 : MOV AX, 1234h ; FNINIT ; FNSTSW AX → PCem AX = 0000h, silicium 1234h (déduit).
*G13* : (b) — AX inchangé : déduit ; DF E0 dans le 8087 : (c), sans effet visible ; avec les coprocesseurs de 16 bits.
*Reproduit* : `Cpu/x87_ops_808x_tables.cs`, `ops_808x_fpu_df_a16`, rangée E0, marqueur PB-200 (généré par
`gen46.py`).

### PB-201 — L'adressage du 8087 : les lectures bouclent dans le segment, pas les écritures

`8087.h:19-23` (`readmeml`, `readmemq`) et l'`eaaddr + 8` de `x87_ld80` passent par le `readmemw` du 808x
(`808x.c:73`, offset `uint16_t`) : l'offset est tronqué à 16 bits et boucle dans le segment. Les écritures passent par
`writememw` (`808x.c:105`, offset `uint32_t`) et `writememb_8087` (`8087.h:25`, segment + offset sur 32 bits) : sans
repli. (D2-contre A4-j prêtait aussi le repli à `writememw` : lu à la ligne, seul `readmemw` tronque.)
*Effet* : un opérande qui franchit l'offset FFFFh est lu en partie au début du segment et écrit au-delà : FLD et FST du
même opérande ne voient pas les mêmes octets.
*Trouvé par* : reconnaissance de G13 (D2 § 7 ; D2-contre A4-j).
*Source* : Numerics Supplement (1980), la lecture fictive : l'UC lit le premier mot, le 8087 en relève l'adresse
physique de 20 bits et l'incrémente pour les mots suivants — documenté (D2 § 7).
*Cas qui discrimine* : 8088 + 8087, DS = 1000h, des octets différents en 10000h et en 20000h : FLD m64 [FFFCh] → PCem
lit ses quatre derniers octets en 10000h, silicium en 20000h ; FST m64 [FFFCh] écrit en 20000h des deux côtés.
*G13* : (a) — avec les coprocesseurs de 16 bits : un accès propre au 8087, dans un fichier `*.Materiel.cs`.
*Reproduit* : `Cpu/x87_8087.cs`, `readmemw` (la troncature `(uint16_t)a`), marqueur PB-201.

### PB-202 — FINIT, et FSAVE, effacent C3-C0 sur le 8087 et le 287

`x87_ops_misc.h:57`, `cpu_state.npxs = 0;` dans `opFINIT`, pour tous les types de coprocesseur ; `:346`, la même ligne à
la fin de FSAVE.
*Effet* : sur un 8087 ou un 287, les codes de condition posés avant FINIT ou FSAVE sont perdus.
*Trouvé par* : reconnaissance de G13 (D2 § 7, D2-contre C7 et A8).
*Source* : SDM vol. 3 (2016) § 22.18.2.1 et 387 PRM annexe C § C.3 (après FINIT, C3-C0 intacts sur 8087 et 287) —
documenté ; FSAVE du 8087 : 86Box #4518 (« F(N)INIT and F(N)SAVE both need to simply AND the status word for 0x4700 on
a 8087 », seconde main) ; FSAVE du 287 : déduit.
*Cas qui discrimine* : 8087 ou 287 : FLDZ ; FTST (C3 = 1) ; FNINIT ; FSTSW m16 → PCem 0000h, silicium 4000h.
*G13* : (a) — avec les coprocesseurs de 16 bits (FSAVE du 287 : déduit).
*Reproduit* : `Cpu/x87_ops_misc.cs`, `opFINIT` et `FSAVE`, marqueurs PB-202 (générés par `gen44.py`), et leurs
copies de `Cpu/x87_ops_808x.cs`.

### PB-203 — `x87_reset` est vide : le RESET ne remet pas le coprocesseur dans son état initial

`x87.c:97`, `void x87_reset() {}`, appelée par `resetx86` (`808x.c:697`).
*Effet* : après un reset matériel, l'état x87 est celui d'avant ; à la mise sous tension, le zéro (npxc = 0 : tout
démasqué, PC = 24 bits). Un logiciel qui calcule après un reset sans FNINIT hérite de masques et d'une précision que
le silicium n'aurait pas.
*Trouvé par* : reconnaissance de G13 (D2 § 7, D2-contre C7 ; PLAN-G4.md le notait).
*Source* : 387 PRM annexe C § C.1 (au RESET, l'état de FNINIT, plus IE et ES posés, IM à 0, ERROR# actif) ; Numerics
Supplement (1980) table S-7 p. S-26 et 287 PRM table 3-1 (8087, 287 : l'état de FNINIT, codes de condition
indéterminés) — documenté.
*Cas qui discrimine* : FLDCW 0000h, puis un reset matériel, puis FSTCW m16 et FSTSW m16 sans FNINIT → PCem 0000h et
l'ancien mot d'état ; silicium (387) le mot de FNINIT, IM à 0, et IE et ES posés.
*G13* : (a) — dans le cadre : l'état de reset par type de coprocesseur ; C3-C0 du 8087 et du 287 : (c).
*Reproduit* : `Cpu/x87.cs`, `x87_reset`, vide, marqueur PB-203 ; l'appel est omis dans `Cpu/808x.cs`
(`resetx86`, `// omitted:`, hors du domaine).

### PB-204 — Les ports F0h et F1h de l'AT ne sont pas émulés

Aucun gestionnaire : le seul `io_sethandler(0x00f0, …)` de PCem est celui de la PCjr (`src/floppy/fdc.c:1263-1268`) ;
PCem ne modélise pas non plus le verrou BUSY# de l'AT. PLAN-G4.md, Les risques, le notait déjà.
*Effet* : sur un AT, OUT F0h n'efface aucun verrou et OUT F1h ne réinitialise pas le coprocesseur : un BIOS ou un pilote
qui réinitialise le 287 par F1h garde l'état d'avant.
*Trouvé par* : reconnaissance de G13 (D2 § 7, D2-contre C7).
*Source* : AP-578 § 2.2.1 p. 6 (ERROR# → IRQ13 ; BUSY# verrouillé jusqu'à une écriture au port F0h) ; IBM PC AT
Technical Reference (6280070, 1985 : F0h efface le verrou, F1h réinitialise le coprocesseur ; page non relevée) —
documenté.
*Cas qui discrimine* : AT + 287 : FNINIT ; FLD1 ; OUT F1h, AL ; FSTSW m16 → PCem 3800h (TOP = 7), silicium TOP = 0
(le 287 réinitialisé, C3-C0 indéterminés) ; le verrou (FWAIT bloqué jusqu'à OUT F0h) avec l'acheminement de PB-59.
*G13* : (a) — G13.6, l'acheminement (le verrou de l'AT, F0h et F1h), avec la carte mère (PB-05).
*Reproduit* : par absence ; aucun site dans le domaine du x87 (un gestionnaire de port relèverait de `Models/`).

### PB-205 — Le bit C1 « arrondi vers le haut » n'est jamais posé

Aucun gestionnaire arithmétique, de chargement ou de stockage (`x87_ops_arith.h`, `x87_ops_loadstore.h`,
`x87_ops_misc.h`) ne pose C1 après un arrondi ; PE ne l'est pas non plus (PB-59).
*Effet* : un programme qui lit C1 après une opération inexacte, ou un test de conformité, lit toujours 0.
*Trouvé par* : reconnaissance de G13 (D2 § 7, D2-contre C7).
*Source* : SDM vol. 1 § 8.1.3.2 et § 8.5.6 (C1 = 1 quand le résultat inexact a été arrondi vers le haut) ; 387 PRM
annexe C (propre au 387 et à ses successeurs) — documenté.
*Cas qui discrimine* : FNINIT (64 bits, au plus près) ; FLD1 ; FDIV m64 = 3,0 ; FSTSW m16 → PCem 3800h, silicium (387 et
suivants) 3A20h (C1 et PE : la significande AAAAAAAAAAAAAAABh est arrondie vers le haut).
*G13* : (a) — avec le noyau, qui rend le sens de l'arrondi ; 387 et suivants seulement.
*Reproduit* : par absence, dans tous les gestionnaires ; pas de site propre, pas de marqueur.

### PB-206 — FLD m32 d'un SNaN le rend silencieux sans lever IE

`x87_ops_loadstore.h:499`, `:515`, `x87_push((double)ts.s);` : la conversion de l'hôte (cvtss2sd) rend le NaN
silencieux et ne signale rien au x87.
*Effet* : FLD m32 d'un SNaN ne pose pas IE ; démasquée, aucune exception.
*Trouvé par* : reconnaissance de G13 (D2 § 7, D2-contre C7).
*Source* : SDM vol. 2, FLD (« #IA Source operand is an SNaN ») ; SDM vol. 3 (2016) § 22.18.7.11 (« The 16-bit IA-32 math
coprocessors do not raise an exception when loading a signaling NaN ») — documenté ; la charge rendue silencieuse ou non
par le 8087 et le 287 : inconnu.
*Cas qui discrimine* : FNINIT ; FLD m32 = 7F800001h ; FSTSW m16 → PCem 3800h, silicium (387 et suivants) 3801h (IE) ; la
valeur, 7FF8000020000000 en m64, est la même.
*G13* : (a) — avec le noyau, après le point de décision n° 10 ; 8087 et 287 : sans exception, la valeur : (c).
*Reproduit* : `Cpu/x87_ops_loadstore.cs`, `opFLDs_a16` et `_a32`, marqueurs PB-206, et leurs copies de
`Cpu/x87_ops_808x.cs`.

### PB-207 — FSTENV ne masque pas les exceptions

`x87_ops_misc.h:826-869` : FSTENV range l'environnement et laisse `npxc` intact.
*Effet* : un gestionnaire d'exceptions qui commence par FNSTENV, pour ne pas en lever une seconde, reste démasqué ; avec
ZE démasquée, FNSTENV puis une division par zéro lève l'IRQ13 (PB-59) là où le silicium rend ±∞. Observable dès le
mode PCem.
*Trouvé par* : reconnaissance de G13 (D2-contre A4-a).
*Source* : SDM vol. 2, FSTENV/FNSTENV (« Saves the current FPU operating environment … and then masks all floating-point
exceptions ») — documenté pour le 387 et ses successeurs ; 8087 et 287 : déduit.
*Cas qui discrimine* : FNINIT ; FLDCW 037Bh (ZE démasquée) ; FNSTENV m ; FSTCW m16 → PCem 037Bh, silicium 037Fh ; puis
FLD1 ; FLDZ ; FDIVP → PCem IRQ13 et ST(1) inchangé, silicium +∞ sans interruption.
*G13* : (a) — G13.6, un seul gestionnaire, avec l'acheminement (sa trace visible est l'IRQ13).
*Reproduit* : `Cpu/x87_ops_misc.cs`, `FSTENV`, marqueur PB-207 (généré par `gen44.py`), et sa copie de
`Cpu/x87_ops_808x.cs`.

### PB-208 — Le mot d'étiquettes ment pour les NaN et les infinis ; 10 est relu comme TAG_UINT64

`x87.c:39-42` : `x87_gettag` rend 00 (valide) pour tout registre non vide, non nul et sans TAG_UINT64, NaN et infinis
compris ; `x87.c:56-57` : `x87_settag` relit 10 comme TAG_VALID | TAG_UINT64.
*Effet* : FSTENV et FSAVE étiquettent 00 un NaN ou un infini ; après FLDENV d'une image où un registre est marqué 10,
FISTP m64 de ce registre écrit le `MM[].q` qui y traînait.
*Trouvé par* : reconnaissance de G13 (D2-contre A4-b).
*Source* : SDM vol. 1 § 8.1.7 (« 10 — Special: invalid (NaN, unsupported), infinity, or denormal ») — documenté ; qu'au
rechargement le 387 ne retienne des étiquettes que « vide » ou « non vide » : déduit.
*Cas qui discrimine* : FNINIT ; FLD m64 = +∞ ; FNSTENV m → étiquettes PCem 3FFFh, silicium BFFFh ; FNINIT ; FILD m64 =
5 ; FSTP ST(0) ; FLD1 ; FNSTENV m ; étiquettes forcées à BFFFh ; FLDENV m ; FISTP m64 → PCem 5, silicium 1.
*G13* : (a) — avec le noyau (les étiquettes calculées), ou dès « A+ ».
*Reproduit* : `Cpu/x87.cs`, `x87_gettag` et `x87_settag`, marqueurs PB-208.

### PB-209 — FUCOM, FUCOMP, FUCOMPP et FPREM1 s'exécutent sur le 8087 et le 287, en zéro cycle

Les tables sont les mêmes pour le 287, le 387 et le 486 (`cpu_set` ne lit pas `fpu_type` pour les poser, D2-contre
K16), et `8087.h:86` les recompile pour le 8087 : FUCOM et FUCOMP (`x87_ops.h:758-759`, a32 `:797-798`), FUCOMPP
(`:432`, `:471`), FPREM1 (D9 F5, `:356`, `:394`). Leurs temps valent 0, « /*387+*/ » : `x87_timings.c:54`, `:71`
(8087 : `fprem1`, `fucom`), `:128`, `:145` (287). FSIN, FCOS et FSINCOS relèvent de PB-68.
*Effet* : sur un 8087 ou un 287, ces instructions du 387 calculent comme sur un 387 et ne coûtent aucun cycle ; un
logiciel qui reconnaît le 387 à FUCOM ou à FPREM1 le trouve.
*Trouvé par* : reconnaissance de G13 (D2-contre A4-c).
*Source* : 387 PRM annexe C (FUCOM, FUCOMP, FUCOMPP, FPREM1, FSIN, FCOS, FSINCOS absents du 8087 et du 287) ; Juffa
(« the Intel 8087/80287 do not feature … FUCOM … FPREM1 ») — documenté ; ce que le 8087 et le 287 font de ces octets
(pile, mot d'état, durée) : inconnu, à mesurer sur DD E1, DA E9 et D9 F5.
*Cas qui discrimine* : 287 : ST(0) = 5,0, ST(1) = 3,0 ; D9 F5 → PCem ST(0) = 2,0 (PB-65), en 0 cycle ; silicium :
inconnu — le cas s'écrira sur la mesure.
*G13* : (c) — le vrai comportement est inconnu ; reproduit tant qu'une mesure ne le tranche pas (décision n° 14).
*Reproduit* : `Cpu/x87_ops_tables.cs` (rangées DD E0-EF, DA E8, D9 F0) et `Cpu/x87_ops_808x_tables.cs` ;
`Cpu/x87_timings.cs` (`fprem1` et `fucom` des tables du 8087 et du 287) ; marqueurs PB-209.

### PB-210 — Les alias non documentés, traités à moitié

Exécutés : D9 D8+i (FSTP1, `x87_ops.h:353`, que PCem marque `/*Invalid*/`), DC D0+i et DC D8+i (FCOM2, FCOMP3, par la
table de 32 entrées, `:717`, `:722`). `FPU_ILLEGAL` : DD C8+i (FXCH4, `:755`, `:794`), DE D0+i (FCOMP5, `:835`,
`:874`), DF C0+i (FFREEP), DF C8+i, D0+i, D8+i (FXCH7, FSTP8, FSTP9) (`:912-915`, `:951-954`).
*Effet* : DD C8+i, DE D0+i et DF C0+i à D8+i ne font rien (décodage et `timing_rr`) là où, selon les sources
secondaires, le silicium échange, compare et dépile, libère et dépile, ou range et dépile.
*Trouvé par* : reconnaissance de G13 (D2-contre A4-d).
*Source* : déduit de sources secondaires seulement (D2-contre A4-d) ; aucune source primaire d'Intel relevée — « à
instruire avant toute inscription » : une source primaire, ou DD C9, DE D1, DF C1, DF C9, DF D1 et DF D9 mesurés.
*Cas qui discrimine* : ST(0) = 1,0, ST(1) = 2,0 ; DD C9 ; FSTP m64 → PCem 1,0 (rien n'a bougé), silicium attendu 2,0
(déduit, non mesuré).
*G13* : (c) — reproduit tant que le comportement n'est pas instruit (la contre-lecture voulait l'instruire d'abord).
*Reproduit* : `Cpu/x87_ops_tables.cs` (rangées DD C8, DE D0, DF C0 à D8) et `Cpu/x87_ops_808x_tables.cs`, marqueurs
PB-210 (générés par `gentab.py` et `gen46.py`).

### PB-211 — FRNDINT perd le signe du zéro

`x87_ops_misc.h:708` et `x87_ops.h:60-82` : `x87_fround` rend un `int64_t`, que `(double)` convertit en +0 pour tout
résultat nul : −0, et ]−0,5 ; 0[ au plus près (]−1 ; 0[ vers +∞ ou vers zéro).
*Effet* : FRNDINT de −0 ou de −0,3 rend +0 ; une division qui suit rend +∞ au lieu de −∞.
*Trouvé par* : reconnaissance de G13 (D2-contre A4-e).
*Source* : IEEE 754-1985 § 6.3 (« the sign of the result of the round floating-point number to integral value operation
is the sign of the operand ») ; SDM vol. 2, FRNDINT — documenté.
*Cas qui discrimine* : FLD m64 = −0,3 (BFD3333333333333) ; FRNDINT ; FSTP m64 → PCem 0000000000000000, silicium
8000000000000000 ; de même pour −0 (8000000000000000).
*G13* : (a) — avec les gestionnaires du noyau, après le point de décision n° 10 (exact aussi en double).
*Reproduit* : `Cpu/x87_ops_misc.cs`, `opFRNDINT`, marqueur PB-211 (généré par `gen44.py`), et sa copie de
`Cpu/x87_ops_808x.cs`.

### PB-212 — FLD m64 d'un SNaN le garde signalant, sans IE

`x87_ops_loadstore.h:396-427`, `t.i = geteaq(); … x87_push(t.d);` : les bits sont copiés tels quels (FLD m32, lui, rend
le NaN silencieux : PB-206).
*Effet* : un SNaN chargé par FLD m64 reste signalant dans le registre et ressort tel quel par FSTP m64 ; aucune IE.
*Trouvé par* : reconnaissance de G13 (D2-contre A4-f).
*Source* : SDM vol. 2, FLD (« #IA Source operand is an SNaN » ; masquée, le QNaN) — documenté pour le 387 et ses
successeurs ; 8087 et 287 : sans exception (SDM vol. 3 (2016) § 22.18.7.11), la valeur chargée : inconnue.
*Cas qui discrimine* : FNINIT ; FLD m64 = 7FF0000000000001 ; FSTP m64 ; FSTSW m16 → PCem 7FF0000000000001 et 0000h,
silicium (387 et suivants) 7FF8000000000001 et 0001h (IE).
*G13* : (a) — avec le noyau, après le point de décision n° 10 ; 8087 et 287 : (c).
*Reproduit* : `Cpu/x87_ops_loadstore.cs`, `opFLDd_a16` et `_a32`, marqueurs PB-212, et leurs copies de
`Cpu/x87_ops_808x.cs`.

### PB-213 — Les comparaisons ne remettent pas C1 à zéro

`x87_ops_arith.h:29`, `:42` (FCOM et FCOMP mémoire, les huit instances du macro), `:159` (FCOM), `:173` (FCOMP), `:185`
(FCOMPP), `:201` (FUCOMPP), `:404` (FUCOM), `:415` (FUCOMP) ; `x87_ops_misc.h:456` (FTST) : `npxs &= ~(C0 | C2 | C3)`,
C1 intact. (D2-contre A4-g citait les six premières lignes ; FUCOM, FUCOMP et FTST font de même.)
*Effet* : un C1 posé avant — par FXAM, FPREM, une transcendante — traverse la comparaison.
*Trouvé par* : reconnaissance de G13 (D2-contre A4-g).
*Source* : SDM vol. 2, FCOM/FCOMP/FCOMPP, FUCOM/FUCOMP/FUCOMPP et FTST (« C1 Set to 0 ») — documenté pour le 387 et ses
successeurs ; 8087 et 287 : C1 « X » après une comparaison (287 PRM table 2-6), PCem n'y est pas en défaut.
*Cas qui discrimine* : FNINIT ; FLD m64 = 2,0 ; FLD m64 = −1,0 ; FXAM (C1 = 1) ; FCOM ST(1) ; FSTSW m16 → PCem 3300h (C1
et C0), silicium 3100h (C0).
*G13* : (a) — G13.6, avec les comparaisons (PB-57, 58, 64 et la part « comparaisons » de 70) ; 387 et suivants.
*Reproduit* : `Cpu/x87_ops_arith.cs` (vingt-deux comparaisons, générées par `gen43.py`) et `Cpu/x87_ops_misc.cs`
(`opFTST`, `gen44.py`), et leurs copies de `Cpu/x87_ops_808x.cs` ; marqueurs PB-213.

### PB-214 — PLAY AUDIO MSF cherche la piste sur la position encore compactée

`cdrom-image.cc:83` : `image_playaudio` cherche la piste de `pos` (`GetTrack`) avant de convertir le MSF reçu
(`:91-96`) ; en MSF, `pos` vaut alors `m × 65 536 + s × 256 + f`. Si cette valeur tombe dans une piste de données,
la lecture est annulée (« Can't play data track », `:84-89`), alors que `scsi_cd` a déjà accepté la commande et rend
GOOD (`scsi_cd.c:1375-1381`).
*Effet* : PLAY AUDIO MSF d'une piste audio ne joue rien, sans erreur, quand la valeur compactée tombe dans une piste
de données — toujours au-delà de l'adresse vraie, donc sur un disque où des données suivent l'audio (un CD Extra).
Hors de toute piste, `attr` est indéterminé chez PCem et vaut 0 ici, des deux côtés (PB-111) : la lecture part.
*Trouvé par* : reconnaissance de G13 (D3-contre, A4).
*Source* : documenté — SFF-8020i r2.6 § 10.8.9 (PLAY AUDIO MSF) : la plage demandée est jouée ; une plage qui n'est
pas audio rend CHECK CONDITION, 05/64h « Recommended » (Table 77) ; jamais un succès sans lecture.
*Cas qui discrimine* : une feuille à piste 1 audio (LBA 0 à 2 999) et piste 2 de données (3 000 à 5 999) ; PLAY
AUDIO MSF de 00:20:00 à 00:30:00 (LBA 1 350 à 2 100) : la valeur compactée, 5 120, tombe dans la piste 2 ; PCem rend
GOOD, READ SUB-CHANNEL l'état 13h, rien ne joue ; le matériel joue (11h).
*G13* : (a).
*Reproduit* : `Cdrom/cdrom-image.cs`, `image_playaudio`, marqueur PB-214.

### PB-215 — Le contrôle de piste de PLAY AUDIO MSF juge 150 secteurs trop loin

`cdrom-image.cc:62-66` : `image_is_track_audio` convertit le MSF par `MSF_TO_FRAMES`
(`includes/private/dosbox/cdrom.h:64`), qui ne retranche pas les 150 secteurs de l'amorce ; or `GetTrack` compare
des LBA (`dosbox/cdrom_image.cpp:155-167`), et c'est `GetAudioTrackInfo` qui ajoute 150 pour le MSF (`:108`).
`scsi_cd.c:1375-1378` refuse ou accepte PLAY AUDIO MSF sur ce jugement.
*Effet* : les deux dernières secondes d'une piste sont jugées sur la suivante : 05/64h à tort à la fin de la
dernière piste audio, ou d'une piste audio que suivent des données ; une piste de données acceptée à tort quand de
l'audio la suit — la lecture part alors sur des données.
*Trouvé par* : reconnaissance de G13 (D3-contre, A5).
*Source* : documenté — SFF-8020i r2.6 § 10.8.9 : une plage audio est jouée, une plage de données rend CHECK
CONDITION, 05/64h ; § 10.8.8 : 00:02:00 est le LBA 0, la conversion retranche donc 150.
*Cas qui discrimine* : `mixte.cue` (données, puis audio en LBA 42 et 222, lead-out en 247) : PLAY AUDIO MSF depuis
00:02:00 (LBA 0, des données) : PCem l'accepte, jugé au LBA 150 (piste 2) ; le matériel rend 05/64h. Depuis 00:05:05
(LBA 230, piste 3) : PCem rend 05/64h, jugé au LBA 380, hors des pistes ; le matériel joue.
*G13* : (a).
*Reproduit* : `Cdrom/cdrom-image.cs`, `image_is_track_audio`, marqueur PB-215.

### PB-216 — `ide_irq_update` baisse une IRQ que l'unité choisie tient encore, et la repose après l'EOI

`ide.c:141-142` : la seconde branche d'`ide_irq_update` baisse la ligne dès que l'IRQ 14 est en attente ou en
service au PIC esclave (`(pic2.pend | pic2.ins) & 0x40`), même quand l'unité choisie tient encore son interruption
(`irqstat` à 1, nIEN à 0). `:139-140` : la première, une fois la ligne libre, repose une demande à chaque appel tant
que `irqstat` vaut 1 — `picint` ne connaît pas de front (`pic.c:298-311`). La fonction est appelée à chaque écriture
de 1F6h (`:433`) et de 3F6h (`:601`).
*Effet* : une écriture de 1F6h ou de 3F6h entre la levée de l'IRQ et sa prise en compte la perd ; après l'EOI, tant
que l'état n'est pas lu, une nouvelle écriture en pose une seconde. Baisser la ligne quand l'autre unité est
choisie, ou sans interruption en attente, est juste.
*Trouvé par* : reconnaissance de G13 (D3-stockage, § 1 point 7 ; forme restreinte par D3-contre, K13).
*Source* : documenté — ATA-1 r4 § 6.3.10 : INTRQ est actif tant que l'unité choisie a une interruption en attente et
que nIEN vaut 0 ; il ne retombe que sur RESET-, SRST, une écriture du registre de commande ou une lecture d'état. Le
8259A de l'AT, par front, ne voit pas de front neuf sur une ligne restée haute — déduit.
*Cas qui discrimine* : AT à IDE, unité 0 du primaire, nIEN = 0, IF à 0 ; une commande finie : `irqstat` à 1 et le
bit 6 de `pic2.pend` posé ; écriture de 1F6h = A0h (la même unité) : PCem efface le bit 6, l'IRQ est perdue ; le
matériel le garde, et l'IRQ arrive au STI.
*G13* : (a) — avec PB-71, même fonction.
*Reproduit* : `Ide/ide.cs`, `ide_irq_update`, marqueur PB-216.

### PB-217 — Le sense du Xebec n'a ni le bit « adresse valide » ni l'adresse

`mfm_xebec.c:314-332` (CMD_READ_STATUS) : les quatre octets de sense valent `error`, `drive_sel ? 0x20 : 0`, 0 et 0
(`:321-323`).
*Effet* : après une erreur sur une commande qui visait une adresse du disque, le sense ne dit pas où : l'octet 0 n'a
pas le bit 7, et les octets 1 à 3 sont nuls, hors le bit d'unité. Le BIOS du Fixed Disk Adapter n'en garde que le
type et le code (`AND AL,0FH`, `AND BL,30H`, lignes 1284-1291 de son listing) : seul un programme qui lit le sense
brut voit l'écart.
*Trouvé par* : reconnaissance de G13 (D3-stockage, fiche de PB-25 ; D3-contre, A6).
*Source* : documenté — IBM Fixed Disk Adapter (6361503), *Sense Bytes*, p. 4 : octet 0, le bit 7 « address valid »
quand la commande précédente visait une adresse, puis le type et le code de l'erreur ; octet 1, l'unité (bit 5) et
la tête ; octet 2, les bits hauts du cylindre et le secteur ; octet 3, le bas du cylindre.
*Cas qui discrimine* : XT à Xebec, disque de type 16 initialisé, unité 0 : READ SECTORS au cylindre 123h, tête 2,
secteur 20 (au-delà des 17) : octet de fin 02h des deux côtés ; puis le sense (03h) : `21 00 00 00` chez PCem,
`A1 02 54 23` sur la carte.
*G13* : (a), de faible valeur.
*Reproduit* : `Mfm/mfm_xebec.cs`, CMD_READ_STATUS, marqueur PB-217.

### PB-218 — RETURN EEPROM (23h) ignore son drapeau

`scsi_aha1540.c:1193-1202` : la commande 23h prend trois paramètres (`:639-641`) et rend `params[1]` octets de
l'EEPROM depuis `params[2]` ; `params[0]` n'est jamais lu.
*Effet* : une demande des options par défaut rend la configuration courante. La ROM v1.01 émet deux 23h au POST
(`VERIFICATION.md` § G11.0), avec un drapeau qu'on ne connaît pas : l'effet pour l'invité est inconnu.
*Trouvé par* : reconnaissance de G13 (D3-contre, A8).
*Source* : AHA-1540C TR, *Return EEPROM* (23h), octet 0 : « 1 = Return configured options, 0 = Return default
options » — documenté ; le contenu des options par défaut, les réglages d'usine du même TR (ID 7, DMA 5, IRQ 11) —
déduit.
*Cas qui discrimine* : EEPROM de référence (ID 7, DMA 7, IRQ 10, soit `07 71`) ; 23h, octet 0 = 00h, 2 octets depuis
le décalage 0 : PCem rend `07 71` ; la carte, les options par défaut (`07 52` au codage de PCem — déduit).
*G13* : (b) — le drapeau est documenté, pas le contenu des options par défaut ; l'effet sur la ROM, inconnu.
*Reproduit* : `Scsi/scsi_aha1540.cs`, la commande 23h, marqueur PB-218.

### PB-35 — Lire le DAC juste après `OUT 3C8h,0` indexe `vgapal[-1]`

`vid_svga.c:122-126`, l'écriture de l'index d'ÉCRITURE du DAC :

```c
        case 0x3C8:
                svga->dac_write = val;
                svga->dac_read = val - 1;
                svga->dac_pos = 0;
```

puis `:241-247`, les deux premières lectures de `3C9h` :

```c
                                return svga->vgapal[svga->dac_read].r;
                        return svga->vgapal[svga->dac_read].r & 0x3f;
```

`val = 0` donne `dac_read = -1`, et rien ne le borne avant les cas 0 et 1 — seul le cas 2
masque, `(svga->dac_read + 1) & 255`. `vgapal[-1]` est hors du tableau : dans `svga_t`,
le champ qui le PRÉCÈDE est `uint32_t pallook[512]`, et `RGB` étant aligné sur un octet il
n'y a pas de bourrage entre les deux. La lecture rend donc les octets 1 à 3 de
`pallook[511]`.

*Effet* : les deux premières composantes lues valent ce que contient la fin de
`pallook[]`, pas une couleur du DAC. Pour une VGA c'est **zéro**, et c'est connaissable :
`pallook` n'est écrit qu'aux indices 0-255 (`3C9h` et `svga_set_ramdac_type`), et
`vga_init` a tout effacé. La troisième lecture rend la composante bleue de l'entrée 255. Ce qu'un vrai DAC rend à cet
endroit n'est pas documenté : aucune source ne dit qu'il lirait à l'index de lecture que pose `3C7h`. L'invité lit ces
valeurs dès qu'il atteint le chemin : l'entrée passe de la section C à la section A, avec PB-37 et le même critère, un
effet observable (D4-contre, § 2 n° 13).
*Atteint* : oui, et compté — deux fois dans la campagne graphique de VERIFICATION.md
§ M15, par un programme qui fait `OUT 3C8h,0` puis trois `IN AL,DX` sur `3C9h`. Le BIOS
VGA, lui, ne le fait jamais : 1 536 lectures du DAC, aucune à l'index -1.
*Source* : aucune source primaire lue — FreeVGA, *VGA Color Registers* (secondaire) : le résultat d'un entrelacement des
lectures et des écritures « may produce unexpected results » et dépend du DAC ; la Sierra SC15025/26 a un registre
d'adresse de 8 bits (p. 3-83), sans index -1 ; l'IMS G171 (INMOS, *Graphics Databook*, 1990) reste à lire — inconnu.
*Cas qui discrimine* : VGA, entrée 255 du DAC = 3Fh 3Fh 3Fh, `OUT 3C8h,0` puis trois `IN 3C9h` : PCem rend 00h, 00h,
3Fh ; un vrai DAC, inconnu — à mesurer DAC par DAC (VGA d'IBM, Trio64, GD5429, TKD8001, SC1502x), sous DEBUG, les
entrées 0 et 255 d'abord écrites de valeurs distinctes.
*G13* : (c) — le vrai comportement dépend du DAC et n'est pas documenté ; reproduit jusqu'à une mesure.
*Reproduit* : `Video/vid_svga.cs`, `vgapal_at`, marqueur PB-35 (`:248`). Le
C lit hors du tableau sans broncher, le C# lèverait ; `vgapal_at` rend les octets de
`pallook[511]`, comme la disposition mémoire du C. Le diff de la campagne l'a vérifié : les
registres qui reçoivent ces lectures sont hachés à chaque instruction.

### PB-37 — `svga_render_24bpp_lowres` n'avance jamais son pointeur de sortie

`vid_svga_render.c:707-718`, la branche sans remappage :

```c
                        for (x = 0; x <= svga->hdisp; x++) {
                                ...
                                p[0] = p[1] = dat0 & 0xffffff;
                                p[2] = p[3] = (dat0 >> 24) | ((dat1 & 0xffff) << 8);
                                p[4] = p[5] = (dat1 >> 16) | ((dat2 & 0xff) << 16);
                                p[6] = p[7] = dat2 >> 8;

                                svga->ma += 12;
                        }
```

`p` n'est ni incrémenté ni recalculé : chaque tour réécrit les huit MÊMES pixels, en tête de
ligne. La branche avec remappage (`:720-737`) a le même défaut de `p`, mais avance déjà `x` de 4
(`:720`). Le rendu haute résolution
voisin (`:742-789`) écrit par `*p++` et ne l'a pas. Et `svga->ma` n'est pas masqué en
sortie, contrairement aux cinq autres rendus 15 à 24 bpp — `ma` est masqué à chaque
lecture, donc sans effet sur les adresses.

*Effet* : en 24 bpp basse résolution, seuls les huit premiers pixels de chaque ligne
changent, et ils portent le DERNIER groupe de la ligne ; le reste du tampon garde l'image
précédente. Un effet visible dès que le rendu est atteint : l'entrée passe de la section C à la section A, avec PB-35
et le même critère (D4-contre, § 2 n° 13).
*Atteint* : oui, et compté — 368 908 appels dans la campagne de la 8900D (VERIFICATION.md
§ M19), où le RAMDAC TKD8001 pose `bpp = 24` (`vid_tkd8001_ramdac.c:26-28`) et où
`svga_recalctimings` choisit le rendu basse résolution quand le bit 6 d'AR10 est posé
(`vid_svga.c:341`, `:403-407`). Une atteinte synthétique : le TKD8001 piloté à la main sur le mode 5Dh (§ M19,
étape 7).
*Source* : déduit — une vraie puce affiche toute la ligne, chaque point de 24 bits doublé, l'intention du code (le rendu
haute résolution voisin écrit par `*p++`) ; inconnu — que la 8900D et son TKD8001 offrent vraiment 24 bpp avec AR10
bit 6 (fiches Trident non lues).
*Cas qui discrimine* : SVGA, `bpp` = 24, AR10 bit 6 posé, une ligne dont la VRAM porte des points tous différents : PCem
n'écrit que les huit premiers points de la ligne, avec le dernier groupe ; attendu, chaque point de 24 bits de la VRAM
doublé sur toute la ligne. Le diff attendu porte `#buffer32` et `ma`, que le rendu laisse avant son rechargement.
*G13* : (b) — `x += 4` et `p += 8` sans remappage, `p += 8` avec, dans la fonction (`vid_svga.cs:975` compare `render`).
*Reproduit* : `Video/vid_svga_render.cs`, marqueur PB-37 (`:834`). Confronté
à l'oracle : la campagne est verte au diff (339 586 475 instructions) ET à la sonde, dont le
hachage de `buffer32` porte les pixels que ce rendu écrit.

### PB-220 — Le socle SVGA compte CR00 + 6 caractères par ligne, un de trop

`vid_svga.c:334-335`, `svga_recalctimings` :

```c
        svga->htotal = svga->crtc[0];
        svga->htotal += 6; /*+6 is required for Tyrian*/
```

IBM définit CR00 comme le nombre total de caractères moins 5. Toutes les cartes du socle en héritent : la VGA d'IBM,
les deux Trident, la GD5429, la Trio64 et l'ET4000AX.
*Effet* : une ligne d'un caractère de trop. En mode 3 (CR00 = 5Fh, 9 points, 28,322 MHz), 101 caractères au lieu de
100 : 31,16 kHz et 69,39 Hz au lieu de 31,47 kHz et 70,09 Hz (mesuré, VERIFICATION.md, § L'hôte : le moniteur
automatique). 1 % de trop sur la ligne, que voient la phase de 3DAh, les boucles d'attente et la fréquence verticale.
*Trouvé par* : reconnaissance de G13 (D4-contre, § 3 n° 1).
*Source* : IBM, *PS/2 Hardware Interface Technical Reference — Common Interfaces* (84F9735, octobre 1990), p. 2-56 :
CR00, « the total number of characters minus 5 » — documenté. Ce que compensait le + 6 de Tyrian : inconnu (86Box est
passé à + 5 en gardant le commentaire, un indice).
*Cas qui discrimine* : VGA, mode 3 (CR00 = 5Fh, SR1 bit 0 = 0, horloge de 28,322 MHz) : PCem `htotal` = 101, une ligne
de 32,10 µs ; IBM, 100 et 31,78 µs.
*G13* : (a) — documenté ; `--menu-check` resserré (31 à 32 kHz aujourd'hui), un commutateur propre (boot-diff SVGA).
*Reproduit* : `Video/vid_svga.cs`, `svga_recalctimings`, marqueur PB-220 (`:592`).

### PB-221 — Le chemin rapide des accès en mot ignore le mode d'écriture, la rotation et les verrous

`vid_svga.c:110-111` et `:202-203`, le drapeau `fast` :

```c
        svga->fast = (svga->gdcreg[8] == 0xff && !(svga->gdcreg[3] & 0x18) && !svga->gdcreg[1]) &&
                     ((svga->chain4 && svga->packed_chain4) || svga->fb_only);
```

Il ne teste ni le mode d'écriture (GR5 bits 0-1) ni la rotation (GR3 bits 0-2). Quand il est vrai, `svga_writew` et
`svga_writel` (`:1474-1528`) et leurs formes linéaires (`:1573-1621`) posent les octets du processeur tels quels, là
où deux écritures d'octet (`svga_write`, `:811-1060`) appliqueraient le mode ; `svga_readw` et `svga_readl`
(`:1530-1571`) et leurs formes linéaires (`:1623-1658`) ne chargent aucun verrou, là où `svga_read` les charge
(`:1085-1089`). SR2 n'est pas testé non plus, mais la forme octet du chain4 compact l'ignore aussi (`:828-830`).
*Effet* : sur l'ET4000 (`packed_chain4` vaut 1 en permanence, `vid_et4000.c:496`) et sur la GD5429 en mode compact
(`gd5429_readw`, `gd5429_readl`, `gd5429_writew`, `gd5429_writel`, `vid_cl5429.c:710-771`, et leurs formes linéaires,
`:1251-1300`), un `REP MOVSW` ou `REP STOSW` en mode d'écriture 1, 2 ou 3, ou sous une rotation, écrit autre chose que
deux écritures d'octet ; une lecture de mot laisse les verrous de la lecture d'avant.
*Trouvé par* : reconnaissance de G13 (D4-contre, § 3 n° 2).
*Source* : Cirrus Logic, *CL-GD542X Technical Reference Manual* (janvier 1994), § 6.27, p. 6-34 : « These latches are
loaded whenever display memory is read by the CPU » — documenté pour la GD5429 ; qu'un cycle de mot vaille deux cycles
d'octet, et que l'ET4000 charge ses verrous de même : déduit.
*Cas qui discrimine* : ET4000, mode 13h, GR5 = 41h, GR8 = FFh, VRAM[0..3] = 11h 22h 33h 44h : lecture d'octet en
A000:0000, puis écriture du mot BBAAh en A000:0100 : PCem VRAM[100h..101h] = AAh BBh ; deux écritures d'octet, 11h 22h.
*G13* : (a), (b) — (a) les verrous des lectures, documentés pour la GD542x ; (b) les écritures. Chemin chaud.
*Reproduit* : `Video/vid_svga.cs`, marqueurs PB-221 : le calcul de `fast` dans `svga_out` (`:354`, `:451`) ;
`svga_readw` (`:1896`), `svga_readl` (`:1920`), `svga_readw_linear` (`:2004`), `svga_readl_linear` (`:2026`).

### PB-222 — Le curseur de la CGA et de la MDA inverse la cellule au lieu de forcer ses points

`vid_cga.c:171-177` (80 colonnes) et `:205-214` (40 colonnes) :

```c
                                                        ((uint32_t *)buffer32->line[cga->displine])[(x << 3) + c + 8] =
                                                                cols[...] ^
                                                                0xffffff;
```

et `vid_mda.c:128-131`, `^= mdacols[attr][0][1]`. La CGA inverse l'index de couleur (le `& 0xf` de fin de ligne
ramène `^ 0xffffff` à `^ 15`), la MDA la cellule par l'avant-plan.
*Effet* : sous le curseur, l'invité voit le glyphe en négatif, aux couleurs complémentaires sur la CGA ; la vraie carte
montre un pavé plein, de la couleur d'avant-plan.
*Trouvé par* : reconnaissance de G13 (D4-video, § 4 n° 3 ; D4-contre, § 2 n° 3).
*Source* : IBM, *Options and Adapters TR*, vol. 2 (1984) : CGA, schéma feuille 1 (p. 28), le curseur entre en OU dans
+ALPHA DOTS ; MDA, schéma feuille 5 (p. 23), U3 74LS08 (CURSOR BLINK ∧ +CURSOR DLY) dans le OU U43 74S32 —
documenté : le curseur force les points à 1.
*Cas qui discrimine* : CGA, mode 3, caractère 20h d'attribut 07h sous le curseur, phase allumée : lignes 6-7, PCem
cgapal[15], la carte cgapal[7] ; MDA, caractère DBh d'attribut 07h, rangées 0Bh-0Ch : PCem noir, la carte cgapal[7].
*G13* : (a) — documenté par les deux schémas.
*Reproduit* : `Video/vid_cga.cs`, `cga_poll`, marqueurs PB-222 (`:285` en 80 colonnes, `:337` en 40) ;
`Video/vid_mda.cs`, `mda_poll` (`:220`). L'Hercules, la M24 et le PC1512 dessinent le même XOR ; leur matériel n'a pas
été lu : hors de l'entrée.

### PB-223 — Les caractères de la CGA et le curseur de la MDA clignotent au mauvais rythme

Chez PCem, chaque carte prend le même bit de son compteur de trames pour le curseur et pour les caractères : le bit 3
pour la CGA (`vid_cga.c:165`, `:198`, `:346`), le bit 4 pour la MDA (`vid_mda.c:111`, `:187`), l'Hercules
(`vid_hercules.c:163`, `:246`), la M24 (`vid_olivetti_m24.c:177`, `:353`) et le PC1512 (`vid_pc1512.c:205`, `:366`).
*Effet* : les caractères de la CGA clignotent deux fois trop vite (8 trames sur 16 au lieu de 16 sur 32) ; le curseur
de la MDA, deux fois trop lentement (16 trames sur 32 au lieu de 8 sur 16).
*Trouvé par* : reconnaissance de G13 (D4-video, § 4 n° 4 ; D4-contre, § 2 n° 4).
*Source* : IBM, *Options and Adapters TR*, vol. 2 (1984), CGA, schéma feuille 3 (p. 30), U12 74LS393 : -CURSOR BLINK
est la sortie QD d'un compteur avancé par +V SYNC DLY, -BLINK la sortie QA du second, avancé par QD ; la MDA de même
(feuille 5, p. 23, U28) — documenté : curseur sur 16 trames, caractères sur 32. L'Hercules : inconnu.
*Cas qui discrimine* : CGA, mode 3, 3D8h = 29h, caractère d'attribut 87h : trames entre deux bascules de l'avant-plan,
PCem 8, la carte 16. MDA, mode 7 : trames entre deux bascules du curseur, PCem 16, la carte 8.
*G13* : (a), (c) — (a) la CGA et la MDA, par leurs schémas ; (c) l'Hercules, dont le manuel ne dit pas le rythme.
*Reproduit* : `Video/vid_cga.cs`, `cga_poll`, marqueurs PB-223 (`:275`, `:326`, les caractères) ;
`Video/vid_mda.cs`, `mda_poll` (`:292`, le curseur) ; `Video/vid_hercules.cs`, `hercules_poll` (`:318`, le curseur).
La M24 et le PC1512, dont le matériel n'a pas été lu, restent hors de l'entrée.

### PB-224 — Le bit 3 de 3BAh rend le retour vertical sur la MDA et l'Hercules, pas les points vidéo

`vid_mda.c:135-136` et `:150` : `stat |= 8` à la ligne de synchronisation verticale, `stat &= ~8` seize lignes plus
tard ; `mda_in` le rend en 3BAh (`:57`). L'Hercules de même (`vid_hercules.c:191-192`, `:206`, rendu en `:91`).
*Effet* : un programme qui attend le retour vertical sur le bit 3, par habitude de la CGA, le trouve chez PCem ; sur la
vraie carte, le bit suit les points sous le faisceau, et un écran vide le laisse à 0.
*Trouvé par* : reconnaissance de G13 (D4-video, § 4 n° 1 ; D4-contre, § 1 n° 14 et § 3 n° 3).
*Source* : IBM PC TR 6025008 (1981), pp. 2-42/2-43, et *Options and Adapters TR*, vol. 2, MDA p. 8 : bit 3,
« +Black/White Video » ; schéma feuille 5 (p. 23) : +B & W VIDEO = +ALPHA DOTS ⊕ RVV (U54 74S86) ; le manuel de la
Hercules, *GB101 Owner's Manual*, pp. 12-13 : bit 3, « 1 = dots on » — documenté.
*Cas qui discrimine* : MDA, mode 7, écran d'espaces d'attribut 07h, curseur éteint (R10 = 20h), 3BAh lu à chaque ligne
d'une trame : PCem rend le bit 3 à 1 pendant 16 lignes ; la carte, jamais.
*G13* : (b) — il faut le point sous le faisceau à la lecture, le mécanisme neuf de PB-99 (4).
*Reproduit* : `Video/vid_mda.cs`, `mda_in`, marqueur PB-224 (`:125`) ; `Video/vid_hercules.cs`, `hercules_in`
(`:123`).

### PB-225 — Le bit 7 de 3BAh de l'Hercules a la polarité inversée

`vid_hercules.c:91` : `return (hercules->stat & 0xf) | ((hercules->stat & 8) << 4);` — le bit 7 recopie le bit 3, à 1
pendant les seize lignes du retour vertical (`:191-192`, `:206`).
*Effet* : un programme qui reconnaît l'Hercules, ou attend son retour vertical, par le bit 7 lit l'inverse : PCem rend
0 pendant l'affichage et 1 pendant le retour.
*Trouvé par* : reconnaissance de G13 (D4-video, § 4 n° 2 ; D4-contre, § 1 n° 14) ; PLAN-G9.md, n° 5, l'avait relevé
« à vérifier ».
*Source* : Hercules, *GB101 Owner's Manual*, section 2, pp. 12-13 : 3BAh bit 7, « 0 = vertical retrace … 1 = active
display » — documenté.
*Cas qui discrimine* : Hercules, 3BAh lu pendant l'affichage actif : PCem bit 7 = 0, la GB101 1 ; pendant les lignes
où PCem pose le bit 3 : PCem 1, la GB101 0.
*G13* : (a) — documenté, vérifiable en C# seul.
*Reproduit* : `Video/vid_hercules.cs`, `hercules_in`, marqueur PB-225 (`:125`).

### PB-226 — L'Input Status 1 de l'ET4000 : bit 7 toujours nul, bits 4-5 basculés

`vid_et4000.c:287` : 3DAh tombe dans `svga_in`, qui rend `cgastat` (`vid_svga.c:262-269`) : le bit 7 n'y est jamais
posé, et les bits 4-5 basculent à chaque lecture hors du retour de ligne.
*Effet* : un programme qui attend le retour vertical de l'ET4000 sur le bit 7 ne le voit jamais changer ; les bits 4-5
ne rendent pas la vidéo que choisit AR12.
*Trouvé par* : reconnaissance de G13 (D4-contre, § 3 n° 4).
*Source* : Tseng Labs, data book *ET4000 Graphics Controller* (1990), p. 103 : bit 7, « vertical retrace complement »,
1 hors du retour ; bits 4-5, la rétroaction vidéo choisie par AR12 — documenté. Sur la VGA d'IBM, ces bits sont
« Undefined on Read » (*PS/2 Common Interfaces*, p. 2-45) : l'écart ne vaut que pour l'ET4000.
*Cas qui discrimine* : ET4000, IN 3DAh pendant l'affichage actif : PCem bit 7 = 0, l'ET4000 1 ; pendant le retour
vertical : 0 des deux côtés.
*G13* : (b) — le bit 7 est simple ; les bits 4-5 demandent le point sous le faisceau, comme PB-99 (4).
*Reproduit* : `Video/vid_et4000.cs`, `et4000_in`, marqueur PB-226 (`:168`).

### PB-227 — CR11 bit 7 ne protège pas CR35 sur l'ET4000

`vid_et4000.c:84-87` :

```c
                if ((svga->crtcreg < 7) && (svga->crtc[0x11] & 0x80))
                        return;
                if ((svga->crtcreg == 7) && (svga->crtc[0x11] & 0x80))
                        val = (svga->crtc[7] & ~0x10) | (val & 0x10);
```

Seuls CR0-CR7 sont protégés ; CR35, le débordement vertical de l'ET4000 (les bits 10 de `vblankstart`, `vtotal`,
`dispend`, `vsyncstart` et `split`, `:387-396`), s'écrit toujours.
*Effet* : un programme qui pose CR11 bit 7 pour figer le minutage, puis écrit CR35, le change chez PCem, pas sur
l'ET4000 : les temps verticaux changent.
*Trouvé par* : reconnaissance de G13 (D4-contre, § 3 n° 8).
*Source* : Tseng Labs, data book *ET4000 Graphics Controller* (1990), p. 111, table 4.3-2 : CR35 « protected by bit 7
of CRTC 11 » — documenté.
*Cas qui discrimine* : ET4000, CR35 = 00h, CR11 = 80h, puis OUT 3D4h,35h et OUT 3D5h,02h : PCem CR35 = 02h, soit
`vtotal` + 400h ; l'ET4000 garde 00h.
*G13* : (a) — documenté, vérifiable en C# seul.
*Reproduit* : `Video/vid_et4000.cs`, `et4000_out`, marqueur PB-227 (`:111`).

### PB-228 — L'Input Status 0 de l'EGA rend son bit 7 toujours nul

`vid_ega.c:155-167` : la lecture de 3C2h ne rend que le bit 4, l'interrupteur que choisit `egaswitchread` ; les bits
5-7 sont nuls.
*Effet* : le bit 7, « CRT Interrupt », reste à 0, la valeur du retour vertical : un programme qui l'interroge la lit en
permanence. Distinct de l'IRQ 2, que PCem ne modélise pas (`vid_ega.c` n'appelle aucun `picint`) : une fonction
absente, hors de l'entrée.
*Trouvé par* : reconnaissance de G13 (D4-contre, § 3 n° 5).
*Source* : IBM, *Options and Adapters TR*, vol. 2 (1984), EGA p. 15 : Input Status Register Zero, bit 7, « CRT
Interrupt — A logical 1 indicates video is being displayed … 0 … vertical retrace » — documenté ; son état quand CR11
n'autorise pas l'interruption : inconnu.
*Cas qui discrimine* : EGA, interruption verticale autorisée par CR11 (bits 4-5, pp. 36-37), IN 3C2h pendant l'affichage
actif : PCem bit 7 = 0, l'EGA 1.
*G13* : (b) — documenté quand l'interruption est autorisée, inconnu sinon.
*Reproduit* : `Video/vid_ega.cs`, `ega_in`, marqueur PB-228 (`:273`).

### PB-229 — Le RAMDAC Sierra de l'ET4000 : l'IPF tombe trop tôt ; l'ERPF, ses registres et D3 manquent

`vid_unk_ramdac.c:84-87` :

```c
                if (ramdac->state == 4) {
                        ramdac->state = 0;
                        return ramdac->ctrl;
                }
```

La lecture qui rend le registre de commande désarme le drapeau (l'IPF). Et `unk_ramdac_out` (`:14-76`) ne connaît que
la profondeur : ni l'ERPF (D4 du registre de commande), qui ouvre les registres étendus par 3C7h-3C9h, ni ces
registres (l'identité 09h-0Ch, les masques secondaires 0Dh-0Fh, le repack 10h), ni D3, qui contourne la palette.
*Effet* : des lectures répétées de 3C6h, après les quatre qui arment, rendent le registre de commande puis le masque
des points ; un pilote qui cherche l'identité de la puce (« S », « : », B1h, « A ») par l'ERPF ne la trouve pas.
*Trouvé par* : reconnaissance de G13 (D4-contre, § 3 n° 7).
*Source* : Sierra, data sheet *SC15025/SC15026 HiCOLOR-24*, pp. 3-88/3-89 : l'IPF ne retombe qu'à la mise sous
tension, à une écriture ou à la lecture d'une autre adresse ; tables 9 et 10, l'ERPF et les registres étendus —
documenté ; la puce exacte de la carte modélisée (« SC1502x », `vid_unk_ramdac.c:4-7`) : inconnue.
*Cas qui discrimine* : ET4000, six lectures de 3C6h de suite, sans autre accès : à la sixième, PCem rend le masque des
points, la Sierra encore le registre de commande. L'ERPF : aucun cas tant que la puce n'est pas établie.
*G13* : (b), (c) — (b) l'IPF, documenté ; (c) l'ERPF, ses registres et D3, sur une puce dont l'identité est inconnue.
*Reproduit* : `Video/vid_unk_ramdac.cs`, marqueurs PB-229 : `unk_ramdac_in` (`:116`, l'IPF) et
`unk_ramdac_out` (`:45`, l'ERPF et D3).

### PB-230 — Le 6845 de la CGA, de la M24 et du PC1512 : R16-R17 s'écrivent, R0-R13 et l'index se relisent

`vid_cga.c:16-17`, `crtcmask` : R16 et R17, le crayon optique, à FFh ; `vid_cga.c:62-65` : 3D4h rend l'index, 3D5h
tout registre. La M24 (`vid_olivetti_m24.c:41-42`, `:80-83`) et le PC1512 (`vid_pc1512.c:45-46`, `:100-103`)
recopient la table et la relecture. La MDA et l'Hercules rendent aussi l'index (`vid_mda.c:46-50`,
`vid_hercules.c:80-84`) ; leurs R16-R17 et la relecture de leurs R0-R13 relèvent de PB-97.
*Effet* : un logiciel qui écrit R16-R17 les relit modifiés ; un logiciel qui relit le CRTC ou son index pour
reconnaître la carte voit ce qu'il a écrit.
*Trouvé par* : reconnaissance de G13 (D4-video, § 4 n° 7 ; D4-contre, § 1 n° 14 et § 3 n° 9).
*Source* : data sheet MC6845 (Motorola) : R16-R17 « Write : No », R0-R13 « Read : No » ; *Options and Adapters TR*,
vol. 2 (1984), CGA : le 6845 de Motorola (p. 5), l'index « write-only » (p. 15), R0-R13 « Write Only » (p. 17) —
documenté pour la CGA ; la puce de la M24 et du PC1512 n'a pas été lue ; la valeur relue : inconnue.
*Cas qui discrimine* : CGA, OUT 3D4h,10h, OUT 3D5h,5Ah, IN 3D5h : PCem 5Ah ; le 6845 n'écrit pas R16, qui garde le
verrou du crayon optique. L'index et R0-R13 : aucun cas avant une mesure (DEBUG, `o 3d4 n`, `i 3d5`, `i 3d4`).
*G13* : (a), (c) — (a) R16-R17 de la CGA ; (c) l'index, R0-R13 relus, la M24 et le PC1512 : valeurs inconnues.
*Reproduit* : marqueurs PB-230 : `Video/vid_cga.cs` (`:83`, la table ; `:138`, `cga_in`),
`Video/vid_olivetti_m24.cs` (`:63`, `:110`), `Video/vid_pc1512.cs` (`:70`, `:134`), `Video/vid_mda.cs` (`:117`),
`Video/vid_hercules.cs` (`:115`).

### PB-231 — R10 bits 5-6 : seul « pas de curseur » est traité, pas le clignotement du 6845

`vid_cga.c:343-346` :

```c
                                if ((cga->crtc[10] & 0x60) == 0x20)
                                        cga->cursoron = 0;
                                else
                                        cga->cursoron = cga->cgablink & 8;
```

Les valeurs 10 et 11 des bits 5-6 tombent dans le cas ordinaire : le curseur clignote au seul rythme de la carte. De
même la MDA (`vid_mda.c:184-187`), l'Hercules (`vid_hercules.c:243-246`), la M24 (`vid_olivetti_m24.c:350-353`) et le
PC1512 (`vid_pc1512.c:363-366`).
*Effet* : un logiciel qui pose R10 bits 5-6 à 10 ou 11 voit le clignotement ordinaire ; sur la carte, le 6845 fait
clignoter le curseur lui-même, en plus de la carte.
*Trouvé par* : reconnaissance de G13 (D4-contre, § 3 n° 10).
*Source* : data sheet MC6845 (Motorola), R10 bits 5-6 ; IBM PC TR 6025008 (1981), en-tête de l'INT 10h : « HARDWARE
WILL ALWAYS CAUSE BLINK — SETTING BIT 5 OR 6 WILL CAUSE ERRATIC BLINKING OR NO CURSOR AT ALL » — documenté ; le dessin
qui en résulte : inconnu, à mesurer (R10 = 46h puis 66h, le curseur relevé sur 64 trames).
*Cas qui discrimine* : CGA, R10 = 46h : PCem fait clignoter le curseur comme avec 06h ; le 6845 y ajoute son propre
clignotement — aucun attendu documenté.
*G13* : (c) — le combiné des deux clignotements n'est pas documenté ; reproduit jusqu'à une mesure.
*Reproduit* : marqueurs PB-231 : `Video/vid_cga.cs:499`, `Video/vid_mda.cs:290`, `Video/vid_hercules.cs:316`,
`Video/vid_olivetti_m24.cs:476`, `Video/vid_pc1512.cs:488`.

### PB-232 — En 40 colonnes, la M24 et le PC1512 éteignent le caractère sous le curseur

`vid_olivetti_m24.c:209` et `vid_pc1512.c:237` :

```c
                                                if ((m24->blink & 16) && (attr & 0x80))
                                                        cols[1] = cols[0];
```

En 80 colonnes, les mêmes fichiers exemptent la cellule du curseur (`!drawcursor`, `vid_olivetti_m24.c:177`,
`vid_pc1512.c:205`) ; en 40 colonnes, non. La famille de PB-04, où la CGA lit un champ que rien n'écrit.
*Effet* : en 40 colonnes, un caractère clignotant sous le curseur perd son avant-plan pendant la phase éteinte ; le
curseur, une inversion, n'y montre qu'un pavé uni.
*Trouvé par* : reconnaissance de G13 (D4-video, § 4 n° 5 ; D4-contre, § 1 n° 14).
*Source* : déduit — l'intention de PCem, que ses modes 80 colonnes suivent, et la logique documentée de la CGA d'IBM
(PB-04) ; le matériel de la M24 et du PC1512 n'a pas été lu : inconnu.
*Cas qui discrimine* : M24, 3D8h = 28h (40 colonnes), caractère 41h d'attribut 87h sous le curseur, trame où
`blink & 16` ≠ 0 : sur les lignes du curseur, PCem un pavé uni (le fond inversé) ; avec l'exemption, le glyphe inversé.
*G13* : (b) — l'intention de PCem est claire, le matériel inconnu ; une vérification par image seulement.
*Reproduit* : marqueurs PB-232 : `Video/vid_olivetti_m24.cs:289`, `Video/vid_pc1512.cs:319`.

### PB-233 — La MDA décode 3B0h-3B3h, 3B6h et 3B7h comme son 6845

`vid_mda.c:18-27`, et `:46-55` en lecture : 3B0h, 3B2h et 3B6h prennent l'index du 6845 comme 3B4h ; 3B1h, 3B3h et
3B7h ses registres comme 3B5h.
*Effet* : un logiciel qui écrit dans 3B0h-3B3h, 3B6h ou 3B7h programme le 6845 chez PCem ; sur la vraie MDA, peut-être
rien.
*Trouvé par* : reconnaissance de G13 (D4-video, § 4 n° 10 ; D4-contre, § 2 n° 20).
*Source* : *Options and Adapters TR*, vol. 2 (1984), MDA p. 7 : ces ports « Not Used » — documenté ; « Not Used »
n'est pas « non décodé » : la CGA est décodée en partie (A1, A2 « don't care », CGA p. 15), et PCem est probablement
juste (déduit) ; le décodage réel, à lire aux schémas (MDA, feuilles 1-2) avant de mesurer : inconnu.
*Cas qui discrimine* : aucun tant que le décodage n'est pas lu : OUT 3B0h,0Ah puis OUT 3B1h,00h écrit R10 chez PCem,
peut-être rien sur la MDA.
*G13* : (c), faible — le vrai décodage est inconnu, et PCem probablement fidèle.
*Reproduit* : `Video/vid_mda.cs`, `mda_out`, marqueur PB-233 (`:76`). L'Hercules décode de même
(`vid_hercules.c:44-53`, `:80-89`) ; son manuel ne décrit que 3B4h-3B5h (pp. 8-9) : hors de l'entrée.

### PB-234 — Un bloc ADPCM finit à la lecture de son dernier octet, avant de le jouer

`sound_sb_dsp.c:941-945` (4 bits), `:982-986` (2,6 bits) : dès que les échantillons d'un octet sont sortis, `pollsb`
lit l'octet suivant et décrémente `sb_8_length` ; `:1036-1044` : dès que le compte passe sous zéro, le bloc finit —
l'IRQ, puis l'arrêt ou la recharge. L'octet qui fait passer le compte sous zéro est lu, pas encore décodé.
*Effet* : en simple cycle, le dernier octet d'un bloc ADPCM (deux, trois ou quatre échantillons) n'est jamais joué ; en
automatique, l'IRQ vient un octet avant la fin du bloc joué. Un programme qui enchaîne ses blocs par l'IRQ perd un octet
par bloc.
*Trouvé par* : reconnaissance de G13 (D5, N2 ; contre-lecture C8).
*Source* : micrologiciel 2.02, `vector_dma_dac_adpcm2` et `_adpcm4` (`sbv202.asm:343-440`, `:445-530`) : l'IRQ n'est
levée qu'une fois sorti le dernier échantillon du dernier octet (`r3` revenu à 0, compte nul) ; en automatique, quand il
lit le premier octet du bloc suivant. Documenté pour le 2.02 ; déduit pour les 1.05, 2.00 et 2.01.
*Cas qui discrimine* : SB 2.0, canal 1 du 8237 en simple cycle sur 4 octets (80h, 77h, 77h, 77h) ; 75h 03h 00h : PCem
lève l'IRQ au 4e tic de sortie, à la lecture du 4e octet, après 4 échantillons ; le 2.02 joue les 3 octets de données,
6 échantillons, puis lève l'IRQ.
*G13* : (a) — documenté par le code machine ; l'attendu se combine avec PB-90, PB-91 et PB-146 (contre-lecture K6).
*Reproduit* : `Sound/sound_sb_dsp.cs`, `pollsb`, marqueur PB-234 (la fin du bloc 8 bits).

### PB-235 — En ADPCM, `DMA_NODATA` est décodé et compté

`sound_sb_dsp.c:943`, `:984`, `:1019` : `sbdat2 = sb_8_read_dma(dsp)` range −1 (`DMA_NODATA`, `dma.h:8`, que rend
`dma_channel_read` pour un canal masqué ou hors du mode lecture, `dma.c:503-517`) comme un octet, et `:944`, `:985`
décrémentent le compte ; les chemins PCM, eux, sautent le tic (`:871-872`, « Needed to prevent clicking in Worms »).
*Effet* : quand le 8237 ne sert pas l'octet (canal masqué après son terme en simple cycle, ou programmé trop court), le
bloc se poursuit sur des octets fictifs, −1 décodé, et finit à l'heure avec son IRQ ; le vrai DSP attend l'octet,
occupé, sans lire de commande, jusqu'à ce que le DMA serve ou qu'un reset vienne.
*Trouvé par* : reconnaissance de G13 (D5, N3 ; contre-lecture C9).
*Source* : micrologiciel 2.02 : après sa requête de DMA, le DSP attend l'octet (`X01ac: jnb pin_dav_dsp,X01ac`,
`sbv202.asm:424` ; `X07a7`, `:1570`). Documenté pour le 2.02, déduit pour les autres ; sauter le tic, comme en PCM,
n'est qu'une approximation de cette attente.
*Cas qui discrimine* : SB 2.0, canal 1 du 8237 masqué (0Ah ← 05h) ; 74h 07h 00h : PCem décode −1 et lève l'IRQ au 14e
tic de sortie ; la carte attend le premier octet et ne lève rien tant que le canal reste masqué.
*G13* : (a) pour l'attente — ni octet fictif, ni compte, ni IRQ ; l'état « occupé » du DSP pendant l'attente n'est
qu'approché.
*Reproduit* : `Sound/sound_sb_dsp.cs`, `pollsb`, marqueurs PB-235 (les trois lectures ADPCM).

### PB-236 — Le DSP au démarrage et au reset : ni sortie coupée, ni bloc 07FFh, ni constante 9Ch

`sound_sb_dsp.c:123-157` (`sb_dsp_reset`), qu'appellent le reset par 2x6h (`:721-726`) et `sb_doreset` (`:159-178`) :
`sb_8_autolen = 0xffff` (`:130`), `sb_speaker = 0` (`:137`) ; la constante de temps (`sb_timeo`, `sb_timei`, `sblatcho`,
`sblatchi`, `sb_freq`) et `muted` ne sont pas touchés. Au démarrage, `muted` vaut 0 (le `memset` des `*_init`,
`sound_sb.c:874` et suivants). Le reset est toujours complet : le micrologiciel, lui, restaure l'état antérieur après un
reset reçu en grande vitesse ou en mode MIDI (une signature en RAM, `sbv202.asm:873-887`), que PCem ne connaît pas.
*Effet* : sur les SB 1.5 à Pro v2, la sortie n'est coupée ni au démarrage ni après un reset, et D8h annonce « éteint »
pendant qu'elle joue ; sur les SB 1.5 à 16, un 1Ch ou un 90h sans 48h préalable lève une IRQ tous les 65 536 octets au
lieu de 2 048 ; sur les 2.xx et 3.xx, une constante non renvoyée après un reset garde l'ancienne fréquence au lieu de
10 kHz.
*Trouvé par* : reconnaissance de G13 (D5, N4 ; contre-lecture K2, A4 et N13).
*Source* : guide de Creative, p. 2-2 (le reset « returns it to its default state ») ; micrologiciels 2.02
(`sbv202.asm:855-907` : bloc `:905-906`, constante `:899`), 3.02 (`v302_4k_4701c5fc.asm:561-616`), 4.05
(`v405-8k_e51aff23.asm:781-851`, le bloc en 0432h). Documenté ; la coupure par P2.0 (convention MCS-51) et le 1.05,
déduits.
*Cas qui discrimine* : SB 2.0 : 40h D3h, D1h, un reset (2x6h ← 01h puis 00h), puis 1Ch : PCem garde `sb_timeo` = D3h et
`muted` = 0, et lève l'IRQ après 10000h octets ; la carte joue à 10 kHz (9Ch), sortie coupée (`muted` = 1), et lève
l'IRQ après 800h octets. D8h rend 00h des deux côtés.
*G13* : (a) — l'état documenté au reset (décision n° 13) ; le reset « chaud » attend PB-240 et PB-241.
*Reproduit* : `Sound/sound_sb_dsp.cs`, `sb_dsp_reset`, marqueur PB-236.

### PB-237 — Les mélangeurs relèvent à 0 dB les volumes de reset du guide

`sound_sb.c:346-347` (CT1335 : 0Ah, la voix, à 3 << 1, « changed default from -46dB to 0dB »), `:413-416` (CT1345 :
04h, 22h et 26h à (7 << 5) | (7 << 1), « changed default from -11dB to 0dB »), `:550-556` (CT1745 : 30h à 35h à
31 << 3, « Changed defaults from -14dB to 0dB »). Les `*_init` remettent leur mélangeur à zéro (`:952`, `:986`, `:1015`,
`:1060`, `:1085`) : ce sont aussi les valeurs du démarrage.
*Effet* : après un reset du mélangeur, et dès le démarrage, la voix — et sur la Pro et la 16, le général et la MIDI —
est à 0 dB au lieu de −46, −11 ou −14 dB : le son est plus fort que sur la carte, et le CD, qui suit le général, aussi
(81 au lieu de 20 sur 65 535 pour le CT1345, 12 au lieu de 2 pour le CT1745).
*Trouvé par* : relevé par PLAN-G12 (« Les valeurs de reset sont changées exprès »), jamais numéroté ; reconnaissance de
G13 (D5, N9 ; contre-lecture C10).
*Source* : guide de Creative, p. 4-5 (CT1335 : général et MIDI 4 ⇒ −11 dB, voix 0 ⇒ −46 dB), p. 4-9 (CT1345 : voix,
général et MIDI 4 ⇒ −11 dB), p. 4-15 (CT1745 : 30h à 35h 24 ⇒ −14 dB). Documenté.
*Cas qui discrimine* : 00h écrit à l'index 0 (le reset) : CT1335, 0Ah relu 06h chez PCem, 00h sur la carte ; CT1345, les
champs de 04h, 22h et 26h à 7 chez PCem, à 4 sur la carte, et le volume CD 81 contre 20 ; CT1745, 30h à 35h relus F8h
chez PCem, C0h sur la carte.
*G13* : (a) — l'état documenté au reset (décision n° 13) ; un son plus faible qu'en mode PCem, à dire à l'utilisateur.
*Reproduit* : `Sound/sound_sb.cs`, les branches de reset de `sb_ct1335_mixer_write`, `sb_ct1345_mixer_write` et
`sb_ct1745_mixer_write`, marqueurs PB-237.

### PB-238 — La lecture de 2xEh acquitte aussi l'IRQ 16 bits, et fait retomber la ligne partagée

`sound_sb_dsp.c:797-799` : la lecture de 2xEh appelle `picintc(1 << sb_irqnum)` et efface `sb_irq8` et `sb_irq16`
ensemble. 2xFh (`:809-813`), lui, n'efface que `sb_irq16` et ne baisse la ligne que si `sb_irq8` est nul.
*Effet* : sur la SB 16 et l'AWE32, quand les IRQ 8 et 16 bits sont pendantes ensemble, le gestionnaire qui acquitte la
8 bits par 2xEh efface aussi la 16 bits : 82h ne la montre plus, et la ligne retombe au PIC alors qu'une source la
tient. Le MPU, troisième source de la ligne, n'a pas d'IRQ chez PCem (PB-154).
*Trouvé par* : reconnaissance de G13 (D5, N10 ; contre-lecture C14, K14).
*Source* : guide de Creative, p. 2-5 (PDF 27) : les sources partagent une ligne d'IRQ ; 2xEh n'acquitte que l'IRQ 8 bits
et SB-MIDI, 2xFh la 16 bits, 3x0h le MPU, et 82h les distingue. Documenté pour les acquits séparés ; la ligne, le OU
des sources, est déduite.
*Cas qui discrimine* : SB 16 : F2h puis F3h (82h = 03h), puis une lecture de 2xEh : PCem rend ensuite 82h = 00h et
efface le bit de l'IRQ de la carte dans IRR ; la carte rend 82h = 02h et garde la ligne haute.
*G13* : (a) — les acquits sont documentés ; la ligne partagée se corrige avec l'IRQ du MPU (PB-154).
*Reproduit* : `Sound/sound_sb_dsp.cs`, `sb_read` (2xEh), marqueur PB-238.

### PB-239 — Une commande de DMA reçue pendant un automatique relance sur-le-champ

`sound_sb_dsp.c:201-230` (`sb_start_dma`) : toute commande de sortie (14h, 16h, 17h, 74h à 77h, C0h à C7h…) remplace
sur-le-champ la longueur, le format et le mode du transfert en cours, au milieu du bloc.
*Effet* : un programme qui sort d'un automatique par une commande en simple cycle, comme le prévoit Creative, voit le
bloc en cours coupé net, et l'IRQ suivante vient trop tôt ; sur la SB 16, le dernier bloc prend chez PCem le format de
la commande, quand le 4.05 garde l'ancien.
*Trouvé par* : reconnaissance de G13 (D5, N11 ; contre-lecture C14, K5).
*Source* : guide de Creative, p. 6-8 (1Ch : « The DSP will, at the end of the current block transfer, exit auto-init
mode and process the new DMA mode I/O command ») et p. 3-16 ; micrologiciels 4.05 (`v405-8k_e51aff23.asm:1003-1012`,
l'octet de mode jeté `:1005-1006` ; `:2623-2634`) et 2.02 (`sbv202.asm:1519-1530`, `:1618-1631`). Documenté.
*Cas qui discrimine* : SB 16 (ou 2.0), 48h FFh 00h puis 1Ch (des blocs de 100h), puis, après 10h octets, 14h 0Fh 00h :
PCem lève l'IRQ au 20h-ième octet ; la carte finit le bloc (l'IRQ au 100h-ième), puis joue les 10h octets (au
110h-ième).
*G13* : (a) — documenté ; le 2.02 met aussi en file pendant un simple cycle, le 4.05 pendant un automatique seulement.
*Reproduit* : `Sound/sound_sb_dsp.cs`, `sb_start_dma`, marqueur PB-239.

### PB-240 — La MIDI de la SB n'existe pas : l'octet MIDI devient une commande

`sound_sb_dsp.c:45-54` : 30h à 38h valent −1 dans `sb_commands` ; `:696-697` : 38h (« TODO: AZTECH MIDI-related? ») ne
fait rien, et 30h à 37h n'ont pas de `case`. L'octet qui suit 38h est donc lu comme une commande.
*Effet* : un programme réglé sur « Sound Blaster MIDI » voit ses octets MIDI exécutés : 90h lance une sortie DMA à
grande vitesse (SB 2.0 et plus), 80h une pause du CNA qui avale deux octets, 40h une constante de temps… Après 34h à 37h
(le mode UART), les écritures restent des commandes, et la sortie par un reset n'existe pas ; 30h et 31h (l'entrée)
restent sans effet, faute de source MIDI.
*Trouvé par* : reconnaissance de G13 (contre-lecture A5).
*Source* : guide de Creative, p. 6-14 (PDF 99 : « Send command 38h. Send MIDI data. ») et p. 5-3 ; 34h à 37h, p. 6-12 et
6-13 (DSP 2.00 et plus), dont seul un reset fait sortir. Documenté.
*Cas qui discrimine* : SB 2.0 : 38h puis 90h : PCem lance une sortie DMA (`sb_8_enable` = 1) et n'envoie rien ; la carte
envoie 90h à la sortie MIDI (`midi_write`, l'empreinte MIDI de la sonde) et ne lance rien.
*G13* : (a) pour 38h et le mode UART de sortie (le puits `midi_write` existe, décision n° 8 de PLAN-G12.md) ; (c) pour
l'entrée, faute de source.
*Reproduit* : `Sound/sound_sb_dsp.cs`, la table `sb_commands` et 38h (`sb_exec_command`), marqueurs PB-240.

### PB-241 — En grande vitesse, le DSP prend encore des commandes, et son reset est complet

`sound_sb_dsp.c:448-467` : 90h, 91h, 98h et 99h passent par `sb_start_dma` et `sb_start_dma_i` comme 1Ch et 14h ; le DSP
continue d'exécuter les commandes, et le reset (`:721-726`) le remet à froid (PB-236), le bloc compris.
*Effet* : sur les SB 2.0 à Pro v2, un octet écrit au DSP en grande vitesse est exécuté, quand la carte l'ignore,
occupée ; le reset qui termine la grande vitesse pose le bloc à FFFFh au lieu de garder celui de 48h.
*Trouvé par* : reconnaissance de G13 (contre-lecture A6).
*Source* : guide de Creative, p. 6-20 (PDF 105 : « In high-speed mode, the DSP will not accept any other commands. To
terminate high-speed mode, send a DSP reset command ») ; micrologiciel 2.02, `X0748` (`sbv202.asm:1482-1506`), reset
« chaud » `:873-887`. Documenté pour les 2.01 à 3.xx ; le 4.05 reprend sa boucle de commandes
(`v405-8k_e51aff23.asm:2512-2584`).
*Cas qui discrimine* : SB 2.0 : 48h FFh 0Fh, 90h, puis E1h : PCem rend 02h 01h ; la carte n'exécute pas E1h (rien à
lire, 2xCh bit 7 à 1) ; puis un reset : `sb_8_autolen` passe à FFFFh chez PCem, reste 0FFFh sur la carte.
*G13* : (a) pour les DSP 2.01 à 3.xx ; la SB 16, qui accepte les commandes, ne change pas.
*Reproduit* : `Sound/sound_sb_dsp.cs`, 90h et 98h (`sb_exec_command`), marqueurs PB-241.

### PB-242 — 48h et D8h sans garde de version : la SB 1.0 les accepte

`sound_sb_dsp.c:403-405` (48h) et `:556-558` (D8h) : aucun test de `sb_type`, quand le guide ne les donne qu'à partir
du DSP 2.00 ; sur la SB 1.0 (DSP 1.05), 48h prend ses deux octets et D8h rend l'état du haut-parleur.
*Effet* : sur la SB 1.0, D8h rend 00h ou FFh, et 48h avale deux octets ; si le 1.05 ignorait ces commandes, les deux
octets de 48h y seraient exécutés comme des commandes, et D8h ne rendrait rien.
*Trouvé par* : reconnaissance de G13 (contre-lecture A9).
*Source* : guide de Creative, p. 6-16 (48h) et p. 6-28 (D8h) : « Available » à partir de 2.00. Documenté pour le guide ;
inconnu pour le 1.05, dont aucune image n'est publiée : à mesurer sur une SB 1.0, la réponse de D8h et le sort des deux
octets de 48h.
*Cas qui discrimine* : SB 1.0 : D8h, puis 2xEh lu : bit 7 à 1 chez PCem (une donnée, 00h) ; le 1.05, inconnu.
*G13* : (c) — le comportement du DSP 1.05 n'est pas connu ; reproduit d'ici une mesure.
*Reproduit* : `Sound/sound_sb_dsp.cs`, 48h et D8h (`sb_exec_command`), marqueurs PB-242.

### PB-243 — Avant la SB 16, les ports jumeaux du DSP ne répètent pas

`sound_sb_dsp.c:847-848` : `sb_dsp_setaddr` installe 2x6h-2x7h et 2xAh-2xFh ; `sb_write` (`:720-764`) ne traite que
2x6h et 2xCh, 2x7h et 2xDh ne font rien ; `sb_read` (`:770-815`) rend 0 pour 2x7h, 2xBh et 2xDh, et traite 2xFh en
acquit 16 bits sur toutes les cartes (`:809-813`).
*Effet* : sur les SB 1.0 à Pro v2, un programme qui passe par l'adresse jumelle (2x7h pour 2x6h, 2xBh pour 2xAh, 2xDh
pour 2xCh, 2xFh pour 2xEh) n'a pas la réponse de la carte ; un `out dx,ax` de 0001h en 2x6h achève l'impulsion de reset
sur la carte, pas chez PCem.
*Trouvé par* : reconnaissance de G13 (contre-lecture A11).
*Source* : DOSBox-X, « verified on real hardware » sur une SB 2.0 et une SB Pro 3.1 (`sblaster.cpp:3204-3211`,
`:4454-4466`) : une source secondaire, mesurée ; aucune source primaire ne décrit ce décodage. Déduit.
*Cas qui discrimine* : SB 2.0 : 01h puis 00h écrits en 2x7h, puis 2xEh et 2xAh lus : la carte rend le bit 7 à 1 et AAh,
le reset fait ; PCem, 7Fh et l'octet périmé.
*G13* : (b) — une source secondaire seulement.
*Reproduit* : `Sound/sound_sb_dsp.cs`, `sb_write` et `sb_read`, marqueurs PB-243.

### PB-244 — D1h et D3h agissent sur-le-champ

`sound_sb_dsp.c:529-542` : D1h et D3h changent `muted` (ou la pause, PB-147) dans la commande même ; le DSP reprend
aussitôt les commandes.
*Effet* : sur la carte, D1h et D3h occupent le DSP jusqu'à 112 et 220 ms : une commande envoyée juste après attend ;
chez PCem, elle est servie aussitôt. Faible enjeu.
*Trouvé par* : reconnaissance de G13 (contre-lecture A12).
*Source* : guide de Creative, p. 6-25 (« The DSP takes a maximum of 112 milliseconds », D1h) et p. 6-26 (220 ms, D3h) ;
micrologiciel 2.02, une rampe du CNA (`sbv202.asm:1827-1866`), aucune en 3.02 (`v302_4k_4701c5fc.asm:1413-1430`).
Documenté pour le maximum ; la durée exacte par version se lit au micrologiciel.
*Cas qui discrimine* : SB 2.0 : D1h puis D8h : chez PCem, 2xEh rend FFh (une donnée) dès l'écriture de D8h ; sur la
carte, pas avant la fin de la rampe du 2.02, sous 112 ms.
*G13* : (b) — la durée n'est donnée qu'en maximum, et varie d'une version à l'autre.
*Reproduit* : `Sound/sound_sb_dsp.cs`, D1h et D3h (`sb_exec_command`), marqueurs PB-244.

### PB-245 — La souris de la M24 envoie ses déplacements négatifs en complément à deux, pas en signe et amplitude

`keyboard_olim24.c:247-252`, `mouse_olim24_poll`, en mode souris (commande 12h du clavier) :

```c
if (mouse->x < -127)
        mouse->x = -127;
if (mouse->x > 127)
        mouse->x = 127;
if (mouse->x < -127)
        mouse->x = 0x80 | ((-mouse->x) & 0x7f);
```

La troisième garde suit la borne : `x` vient d'être ramené dans [-127, 127], elle ne peut plus être vraie. De même
pour `y` (`:254-259`). L'octet envoyé après le préfixe FEh (`:261-263`) est la troncature de `x` : le complément à
deux d'un déplacement négatif, jamais le signe et l'amplitude que la conversion visait.
*Effet* : conditionnel. Si le clavier de la M24 code ses déplacements en signe et amplitude, comme la conversion
morte le suppose, un pilote lit un pas de −1 (FFh) comme −127, et la souris saute à chaque petit déplacement
négatif ; s'il les code en complément à deux, il ne reste qu'une branche morte, sans effet.
*Trouvé par* : transcription de G1.1 (deux marqueurs, restés sans entrée) ; instruit par la reconnaissance de G13.
*Source* : inconnu — ni le Service Manual de l'AT&T 6300 ni son System Programmer's Guide n'en parlent ; le MOUSE.DOC
du pilote Logitech de l'AT&T 6300 (cité sur le forum VCF, secondaire) ne dit que le préfixe FEh. À mesurer : une M24
et sa souris, ou le pilote d'origine désassemblé (CBW : complément à deux ; AND 7Fh et TEST 80h : signe et amplitude).
*Cas qui discrimine* : M24, commande 12h au clavier (60h, trois paramètres), un déplacement de −1 en X, 0 en Y : PCem
envoie FEh FFh 00h ; un clavier à signe et amplitude enverrait FEh 81h 00h.
*G13* : (c) — le codage du vrai clavier est inconnu : reproduit tant qu'une mesure ou un désassemblage n'a pas tranché.
*Reproduit* : `Keyboard/keyboard_olim24.cs`, `mouse_olim24_poll`, marqueurs PB-245 (`:287`, `:297`).

### PB-246 — La cascade du 8259 est servie avant l'IRQ 0 et l'IRQ 1

`pic.c:358-359`, `picinterrupt` :

```c
for (c = 0; c < 8; c++) {
        if ((AT || romset == ROM_XI8088) && (temp & (1 << 2))) {
```

Le test de la cascade ne dépend pas de `c` : dès `c = 0`, une demande de l'esclave (IRQ 8 à 15) passe avant celles
du maître en IR0 et en IR1.
*Effet* : sur un AT, une IRQ 8 à 15 est servie avant l'horloge (IRQ 0) et le clavier (IRQ 1) quand elles attendent
ensemble ; avec PB-05, l'IRQ 0 ou l'IRQ 1 qui attendait est perdue (déduit ; fréquence inconnue : sous DOS, la RTC
n'interrompt que pour INT 15h, AH = 83h et 86h).
*Trouvé par* : reconnaissance de G13 (confirmé par la contre-lecture).
*Source* : 8259A p. 15, « Fully Nested Mode » : « IR0 has the highest priority and IR7 the lowest » ; IBM AT TR
1502494 p. 1-10 et 1-11 : IRQ 0, IRQ 1, IRQ 8 à 15 par la cascade, puis IRQ 3 à 7, « in decreasing priority ».
Documenté.
*Cas qui discrimine* : PICBANC (à écrire), ibmat, CLI : l'IRQ 0 (le PIT) et l'IRQ 9 (la SB Pro v2 en IRQ 2, F2h au
DSP) en attente, relues à l'IRR (OCW3 0Ah) ; STI. PCem sert 71h puis 08h ; le 8259A, 08h puis 71h.
*G13* : (a) — avec PB-05 et PB-247, dans un `picinterrupt` selon la fiche.
*Reproduit* : `Models/pic.cs`, `picinterrupt`, marqueur `fixed in hardware mode: PB-246`.
*Corrigé en mode matériel* (G13.4) : sous `if (materiel.pb_246)`, `picinterrupt_materiel` (`Models/pic.Materiel.cs`) ne teste la cascade qu'à son rang, IR2 ; une cascade sans demande servable passe au niveau suivant. `materiel-cas PB-246` (AT, l'IRQ 0 et l'IRQ 9, deux acquittements) rend 71h puis 08h en mode PCem, 08h puis 71h en mode matériel ; la correction coupée, il rougit. PICBANC sur l'IBM AT, PB-246 seul : « T R » au lieu de « R ».

### PB-247 — Le masque de service du 8259 est ignoré

`808x.c:55` (`IRQTEST`) et `:3985` (`takeint`) acceptent une interruption sur `pic.pend & ~pic.mask`, sans `mask2` ;
`pic.c:356` et `:360`, `picinterrupt`, choisissent sur `pend & ~mask`, sans `mask2` non plus.
(a) Sur le 808x, une IRQ de priorité égale ou moindre interrompt un gestionnaire qui a fait STI avant son EOI.
(b) Sur l'AT, le cœur accepte sur `pic_intpending`, qui tient compte de `mask2` (`386.c:249`), mais `picinterrupt`
descend dans l'esclave même quand l'ISR du maître le bloque ; cela n'arrive que par PB-246.
*Effet* : sur le 5150 et l'XT, l'IRQ 1 du clavier s'imbrique dans l'INT 08h du BIOS, qui fait STI en tête
(`F000:FEA5` du BIOS du 27/10/82) et n'envoie l'EOI qu'après INT 1Ch (`F000:FEE5-FEE7`) ; le 8259A l'aurait retenue.
*Trouvé par* : reconnaissance de G13 (confirmé par la contre-lecture, qui en a borné (b)).
*Source* : 8259A p. 15, « While the IS bit is set, all further interrupts of the same or lower priority are
inhibited » ; p. 18, « in the normal nested mode a slave is masked out when its request is in service ». Documenté.
*Cas qui discrimine* : PICBANC (à écrire), 5150 : INT 1Ch détourné, qui fait STI et attend une frappe injectée.
PCem : INT 09h s'exécute dans INT 1Ch, avant l'EOI de l'IRQ 0 ; le 8259A : seulement après cet EOI.
*G13* : (a) — (a) est propre au 808x, dans `IRQTEST`, un chemin chaud ; (b) ne se voit que par PB-246.
*Reproduit* : `Models/pic.cs`, `picinterrupt`, marqueur `fixed in hardware mode: PB-247` ; `Cpu/808x.cs`, `IRQTEST` et
`takeint`, de même.
*Corrigé en mode matériel* (G13.4) : sous `if (materiel.pb_247)`, `picinterrupt_materiel` choisit hors du masque de service (`mask2`), au maître comme à l'esclave ; sur le 8088, `IRQTEST` et `takeint` passent par `irqtest_materiel` et `takeint_materiel` (`Cpu/808x.Materiel.cs`), qui retirent aussi `mask2` : sans cela, une chaîne REP s'arrêterait sans fin pour une IRQ que le 8259A retient. Le 286 et ses successeurs acceptaient déjà sur `pic_intpending`, qui tient compte de `mask2`. `materiel-cas PB-247` (XT, IR0 en service, l'IRQ 1 en attente : l'acquittement, puis le 8088 sous IF = 1) rend 09h et l'entrée dans INT 09h en mode PCem, FFh et le 8088 dans son code en mode matériel ; la correction coupée, il rougit. PICBANC sur l'XT : dans l'INT 08h, IR0 en service, sous STI, une seule entrée au lieu de 14h (avec PB-248, qui garde l'ISR que « no operation » effaçait).

### PB-248 — Les rotations, la priorité et « sans opération » du 8259 font un EOI ; ni poll ni masque spécial

`pic.c:117-145`, `pic_write` : l'OCW2 ne distingue que l'EOI spécifique (`(val & 0xE0) == 0x60`) ; toute autre forme
— les rotations (00h, 80h, A0h, E0h + n), la priorité (C0h + n), « no operation » (40h) — tombe dans la boucle de
l'EOI non spécifique. `:146-153` : l'OCW3 ne lit que RR et RIS ; le poll (bit 2) et le masque spécial (ESMM et SMM,
bits 6-5) sont ignorés. De même pour l'esclave, `pic2_write`, `:224-244`.
*Effet* : un programme qui fait tourner les priorités, ou interroge le 8259 par poll, voit des bits ISR effacés et
lit l'IRR ou l'ISR au lieu du mot de poll. Aucun BIOS du dépôt n'émet ces formes (les OUT immédiats en 20h et A0h :
20h, 60h-67h, 0Ah, 0Bh, 11h et 13h) ; l'usage par les logiciels est inconnu.
*Trouvé par* : reconnaissance de G13 (élargi à l'esclave et à 40h par la contre-lecture).
*Source* : 8259A p. 13-16 : les formats d'OCW2 et d'OCW3 (figure 8 : R, SL, EOI = 010, « no operation »), les
rotations automatique et spécifique, Poll Command, Special Mask Mode. Documenté.
*Cas qui discrimine* : dans l'INT 08h (IR0 en service), OCW2 40h, puis OCW3 0Bh et IN 20h : PCem 00h, l'ISR effacé ;
le 8259A 01h. Poll, une IRQ n en attente : OCW3 0Ch puis IN 20h, le 8259A rend 80h + n ; PCem, l'IRR ou l'ISR.
*G13* : (a) — documenté, vérifiable en C# seul ; aucun BIOS du dépôt pour témoin.
*Reproduit* : `Models/pic.cs`, `pic_write` et `pic2_write`, marqueurs `fixed in hardware mode: PB-248` (l'OCW2 et l'OCW3).
*Corrigé en mode matériel* (G13.4) : sous `if (materiel.pb_248)`, l'OCW2 (`pic_ocw2_materiel`) distingue les huit formes de la figure 8 (fin non spécifique, spécifique, « no operation », rotations en fin automatique et aux fins, priorité posée) ; l'OCW3 (`pic_ocw3_materiel`) ajoute le poll et le masque spécial ; la lecture qui suit un poll rend 80h + le niveau et l'acquitte (`pic_poll_lire_materiel`) ; ICW1 et la mise sous tension remettent IR7 au plus bas, le masque spécial, la rotation et le poll (`Models/pic.Materiel.cs`). L'état que PCem ne tient pas (le niveau le plus bas, le masque spécial, la rotation automatique, le poll en attente) vit dans `pic.Materiel.cs`, par contrôleur ; le masque de service tient compte de la rotation et du masque spécial. `picinterrupt_materiel` parcourt les niveaux dans l'ordre tourné. `materiel-cas PB-248` (XT : 40h puis l'ISR ; le poll ; la priorité C1h ; AT : le masque spécial et la demande vue par le processeur) rend la valeur de PCem en mode PCem, celle de la fiche en mode matériel ; la correction coupée, il rougit. PICBANC : le poll 80h et l'ISR 01h, « no operation » 01h. Le masque de service recalculé à la mise sous tension remet aussi celui de l'esclave, que PCem oublie (PB-06), et la fin spécifique d'IR2 teste le niveau, que PCem compare à `val` (PB-13).

### PB-249 — Le Clear Mask du 8237 (0Eh, DCh) est sans effet

`dma.c:98-161`, `dma_write`, et `:357-425`, `dma16_write` : aucun `case 0xe`. L'écriture en 0Eh (DCh sur le 8237
haut), « Clear Mask Register », est rangée dans `dmaregs` ou `dma16regs` et ne touche pas `dma_m`.
*Effet* : un programme qui démasque les quatre canaux d'un contrôleur par cette commande les laisse masqués : ses
transferts ne partent pas. Les BIOS du dépôt ne l'emploient pas (ils écrivent les masques un par un).
*Trouvé par* : reconnaissance de G13 (confirmé par la contre-lecture).
*Source* : 8237A p. 9, « Clear Mask Register: This command clears the mask bits of all four channels » ; IBM AT TR
p. 1-14, DCh « Clear Mask Register ». Documenté.
*Cas qui discrimine* : OUT 0Fh,0Fh (les quatre masques posés), OUT 0Eh,00h, puis un transfert sur le canal 2 : PCem
`DMA_NODATA` (masqué) ; le 8237A le fait. Sur l'AT, OUT DEh,0Fh puis OUT DCh,00h : de même pour les canaux 4 à 7.
*G13* : (a) — avec PB-157 et les autres voisins du 8237, dans un 8237 selon la fiche.
*Reproduit* en mode PCem : `Models/dma.cs`, `dma_write` et `dma16_write`, marqueurs `fixed in hardware mode: PB-249`.
*Corrigé en mode matériel* (G13.4) : sous `if (materiel.pb_249)`, une écriture en 0Eh ou en DCh efface les quatre
masques du contrôleur (`dma_clear_mask_materiel`, `Models/dma.Materiel.cs`). `materiel-cas PB-249` (OUT 0Fh,0Fh,
OUT 0Eh,00h, un transfert du canal 2 ; OUT DEh,0Fh, OUT DCh,00h, un transfert du canal 5) rend « refusé » deux fois en
mode PCem, « fait » deux fois en mode matériel ; la correction coupée, il rougit. DMABANC : sur l'XT, le
rafraîchissement du canal 0 reprend après Clear Mask (01h au lieu de 00h) ; sur l'AT, Clear Mask au 8237 haut
démasque le canal 4, et la requête du canal 1 passe.

### PB-250 — Le registre de requête du 8237 (09h, D2h) est ignoré

`dma.c:98-161` et `:357-425` : aucun `case 9`. Une requête logicielle (09h ; D2h sur le 8237 haut) est rangée et ne
déclenche rien ; PCem n'a d'ailleurs pas de moteur de DMA qui transfère de lui-même : les périphériques tirent leurs
octets par `dma_channel_read` et `dma_channel_write`.
*Effet* : un transfert lancé par requête logicielle (en mode bloc) n'a jamais lieu. Le POST de l'AT écrit 00h en D2h
(`F000:1402`), sans effet.
*Trouvé par* : reconnaissance de G13 (confirmé par la contre-lecture).
*Source* : 8237A p. 7, « Request Register » : la requête logicielle, en mode bloc seulement, effacée au TC ou par un
EOP externe, et en entier par un Reset ; figure 6 (p. 9), « Write Request Register ». Documenté.
*Cas qui discrimine* : XT, le canal 1 en bloc et en vérification (mode 81h), compte 0003h, démasqué ; OUT 09h,05h. Le
8237A fait quatre cycles : l'état (08h) rend 02h, le compte FFFFh ; PCem rien (état 00h, compte 0003h).
*G13* : (a) — documenté ; demande un moteur de transfert propre au mode matériel.
*Reproduit* en mode PCem : `Models/dma.cs`, `dma_write` et `dma16_write`, marqueurs `fixed in hardware mode: PB-250`.
*Corrigé en mode matériel* (G13.4) : sous `if (materiel.pb_250)`, une écriture en 09h ou en D2h pose ou efface la
requête logicielle d'un canal (`dma_requete_materiel`), et les requêtes en attente se servent avant tout accès aux
registres des deux 8237 (`dma_servir_materiel`, `Models/dma.Materiel.cs`) : non masquables, en mode bloc seulement,
jusqu'au TC (N + 1 transferts, l'adresse avancée ou reculée d'autant), puis l'auto-initialisation ou le masque posé,
le bit TC de l'état et la requête effacée. Un contrôleur désactivé, ou un canal du bas que la cascade ne sert pas
(PB-253), les garde en attente. La vérification et la lecture ne touchent pas la mémoire ; l'écriture non plus : la
donnée est celle d'un bus que personne ne pilote, inconnue. Le transfert de mémoire à mémoire n'est pas modélisé.
`materiel-cas PB-250` (le canal 1 masqué, en bloc et en vérification, compte 0003h, OUT 09h,05h ; le canal 5, compte
0002h, OUT D2h,05h) rend l'état 00h et le compte 0003h, puis 00h, en mode PCem ; 02h et FFFFh, puis 02h, en mode
matériel ; la correction coupée, il rougit. DMABANC, sur l'XT et sur l'AT : 02h et FFFFh.

### PB-251 — Le master clear du 8237 n'efface ni la commande, ni l'état, ni la requête, ni le temporaire

`dma.c:153-156` (0Dh) et `:417-420` (DAh) : le master clear remet la bascule à zéro et pose les quatre masques, rien
d'autre. `dma_command` garde sa valeur (son bit 2 refuse encore tout transfert, `:503-505`), et les bits TC de
`dma_stat` survivent.
*Effet* : un programme qui désactive un 8237 (commande 04h) et compte sur le master clear pour le réactiver le laisse
désactivé. Sans effet sur les BIOS du dépôt : les cinq BIOS AT réécrivent 00h en 08h et en D0h après leurs master
clear (ibmat `F000:02A6-02AB`) ; ce n'est donc pas un préalable de PB-157.
*Trouvé par* : reconnaissance de G13 (jugé non bloquant par la contre-lecture).
*Source* : 8237A p. 9, « Master Clear » : « same effect as the hardware Reset. The Command, Status, Request,
Temporary, and Internal First/Last Flip-Flop registers are cleared and the Mask register is set ». Documenté.
*Cas qui discrimine* : XT, OUT 08h,04h, OUT 0Dh,00h, OUT 0Ah,00h (le rafraîchissement rétabli), puis une lecture de
disquette par INT 13h : PCem la refuse (`DMA_NODATA`, le contrôleur reste désactivé) ; le 8237A la fait. L'état relu
après un TC puis un master clear : PCem garde le bit TC ; le 8237A rend 00h.
*G13* : (a) — documenté ; facultatif pour PB-157, que les BIOS du dépôt ne mettent pas en défaut.
*Reproduit* en mode PCem : `Models/dma.cs`, `dma_write` et `dma16_write`, marqueurs `fixed in hardware mode: PB-251`.
*Corrigé en mode matériel* (G13.4) : sous `if (materiel.pb_251)`, le master clear d'un contrôleur efface aussi sa
commande, ses bits de l'état et ses requêtes (`dma_master_clear_materiel`, `Models/dma.Materiel.cs`) ; le temporaire
reste nul. `materiel-cas PB-251` (le canal 2 au TC, OUT 08h,04h, OUT 0Dh : l'état ; puis le canal 2 démasqué, un
transfert) rend 04h et « refusé » en mode PCem, 00h et « fait » en mode matériel ; la correction coupée, il rougit.
DMABANC : le 8237 bas désactivé puis remis à zéro sert la requête du canal 1 (FFFFh, avec PB-250).

### PB-252 — Au reset, les masques du 8237 restent à zéro

`dma.c:43`, `dma_reset` : `dma_m = 0;` — les huit canaux démasqués. `dma_reset` ne remet pas non plus la commande ni
l'état : `dma_command`, `dma16_command` et `dma_stat` survivent à un reset matériel (`pc.c:179`).
*Effet* : entre le reset et la première programmation, un canal dont le mode convient transfère au lieu d'être
masqué. Sans effet sur les BIOS du dépôt, qui font un master clear, ou posent les masques, avant tout usage ; un
logiciel qui se sert d'un canal sans l'avoir démasqué le trouverait actif.
*Trouvé par* : reconnaissance de G13 (confirmé par la contre-lecture).
*Source* : 8237A p. 2, broche RESET : « clears the Command, Status, Request and Temporary registers… and sets the
Mask register » ; p. 8, « The entire register is also set by a Reset ». Documenté.
*Cas qui discrimine* : en C# seul, `dma_reset`, puis le mode du canal 2 posé en écriture (46h) sans toucher au masque,
et `dma_channel_write(2, …)` : PCem fait le transfert ; le 8237A rend `DMA_NODATA` (le canal masqué).
*G13* : (a) — documenté, vérifiable en C# seul ; aucun BIOS du dépôt pour témoin.
*Reproduit* en mode PCem : `Models/dma.cs`, `dma_reset`, marqueur `fixed in hardware mode: PB-252`.
*Corrigé en mode matériel* (G13.4) : sous `if (materiel.pb_252)`, le reset pose les huit masques et efface les deux
commandes, l'état et les requêtes (`dma_reset_materiel`, `Models/dma.Materiel.cs`). `materiel-cas PB-252` (le canal 2
au TC, `dma_reset` : l'état ; le mode reposé, un transfert) rend 04h et « fait » en mode PCem, 00h et « refusé » en
mode matériel ; la correction coupée, il rougit. Les POST des machines du dépôt n'en changent pas : chaque BIOS
démasque ses canaux avant de s'en servir.

### PB-253 — La cascade du 8237 n'est pas modélisée, et les maîtres de bus passent outre

`dma.c:503-509` et `:571-577`, `dma_channel_read` et `dma_channel_write` : un canal 0 à 3 ne teste que la commande du
8237 bas ; ni le masque ni le mode du canal 4 (la cascade), ni la commande du 8237 haut ne l'arrêtent. Les maîtres de
bus n'y passent pas : la 1542C lit et écrit la mémoire directement (`scsi_aha1540.c:411` et suivantes,
`mem_readb_phys`), sans regarder le masque ni le mode de son canal (7 par défaut) ni la commande du 8237 haut.
*Effet* : un programme qui masque le canal 4, ou désactive le 8237 haut, ne suspend ni les canaux 0 à 3 ni la 1542C.
*Trouvé par* : reconnaissance de G13 (élargi aux maîtres de bus par la contre-lecture).
*Source* : 8237A p. 5-6 (Cascade Mode : DREQ et DACK d'un canal du premier), p. 7-8 (masque, commande) ; IBM AT TR
p. 1-13, « Channel 4 is used to cascade channels 0 through 3 », et p. 1-26 (–MASTER, un maître de bus par un canal
en cascade). Documenté par composition.
*Cas qui discrimine* : ibmat, OUT D4h,04h (le canal 4 masqué), puis une lecture de disquette par INT 13h : PCem la
fait ; l'AT la bloque (le 8237 bas n'obtient pas le bus) jusqu'au démasquage.
*G13* : (a) — documenté par composition ; avec PB-157 (la commande du haut) et PB-251.
*Reproduit* en mode PCem : `Models/dma.cs`, `dma_channel_read` et `dma_channel_write`, marqueurs `fixed in hardware
mode: PB-253`. La 1542C (`Scsi/scsi_aha1540.cs`) est hors du domaine de la carte mère : ses sites sont inscrits à part
en G13.4, PB-260, au domaine du stockage.
*Corrigé en mode matériel* (G13.4) : sous `if (materiel.pb_253)`, sur l'AT, un canal 0 à 3 n'a le bus que si le canal
4 n'est pas masqué, s'il est en mode cascade (C0h) et si le 8237 haut n'est pas désactivé (`dma_cascade_materiel`,
`Models/dma.Materiel.cs`) ; sinon le transfert est refusé, comme par la commande du bas. La commande du haut n'est
rangée qu'avec PB-157. `materiel-cas PB-253` (AT, le canal 4 en cascade : un transfert du canal 2 ; le canal 4 masqué ;
le canal 4 démasqué en mode simple) rend « fait » trois fois en mode PCem, « fait » puis « refusé » deux fois en mode
matériel ; la correction coupée, il rougit. DMABANC, sur l'AT : le canal 4 masqué retient la requête du canal 1.

### PB-254 — Les files du 8042 n'ont pas de garde : au seizième octet en attente, elles paraissent vides

`keyboard_at.c:153-154` (`key_ctrl_queue`), `:233-234` (`key_queue`) et `:240-241` (`mouse_queue`) : chaque ajout
avance `end` modulo 16 sans regarder `start`. Au seizième octet en attente, `end` rejoint `start` : la file paraît
vide, et ce qu'elle tenait est perdu. Seul le poll de la souris PS/2 se borne (`mouse_ps2.c:176`, moins de 13).
*Effet* : vingt codes de clavier que l'invité ne lit pas (l'IRQ 1 masquée, un long CLI) n'en laissent passer que
quatre : le premier, déjà dans le tampon de sortie, puis les trois derniers. Côté souris, les réponses de commandes
enchaînées sans lecture (E9h en rend quatre octets) se perdent de même.
*Trouvé par* : reconnaissance de PS2 (défaut n° 4 de `PLAN-PS2.md`, jamais inscrit) ; reconnaissance de G13, élargi
au clavier et au contrôleur par la contre-lecture.
*Source* : IBM AT TR p. 4-3, « Keyboard Buffer » : seize codes gardés, le dix-septième remplacé par 00h, les suivants
perdus ; PS/2 HITR Common Interfaces (84F9735), « Keyboard/Auxiliary Device Controller », p. 14-15 : le système tient
la ligne « clock » pour retenir l'envoi. Documenté.
*Cas qui discrimine* : ibmat, l'IRQ 1 masquée, vingt codes injectés sans lecture, puis 60h lu à chaque OBF : PCem
rend le 1er, le 18e, le 19e et le 20e ; l'AT, les dix-sept premiers puis le code de débordement, 00h sans traduction
et FFh traduit par le 8042 (le BIOS de l'AT le teste à FFh).
*G13* : (a) — documenté ; vérifiable en C# seul par des frappes injectées, et par PS2BANC sous `--force-ps2`.
*Reproduit* en mode PCem : `Keyboard/keyboard_at.cs`, `keyboard_at_adddata`, `keyboard_at_adddata_keyboard` et
`keyboard_at_adddata_mouse`, marqueurs `fixed in hardware mode: PB-254`.
*Corrigé en mode matériel* (G13.4) : sous `if (materiel.pb_254)` (`Keyboard/keyboard_at.Materiel.cs`), le clavier
garde ses octets dans son tampon, seize puis le code de débordement, les suivants perdus, en octets bruts avant la
traduction ; il en livre un à la file de PCem quand elle est vide, en tête du poll, et l'octet livré compte encore
dans les seize. Le contrôleur et la souris ne perdent rien : ce que leur file ne tient plus attend dans l'ordre (le
8042 retient la souris par la ligne « clock », PS/2 HITR p. 14-15 ; ses propres réponses, déduit), au plus 64 octets,
une borne de l'émulateur (R9). Le reset du clavier (FFh), celui de la souris (FFh), le self-test du contrôleur (AAh)
et `keyboard_at_init` vident ce qui leur revient. `materiel-cas PB-254` (le clavier sans traduction : 01h puis 02h à
14h, PCem 01h 12h 13h 14h, l'AT 01h à 11h puis 00h ; traduit, FFh au débordement ; vingt fois 20h au contrôleur, 4
réponses contre 20 ; cinq E9h à la souris, 4 octets contre 20) rougit la correction coupée.

### PB-255 — ICW1 ne remet pas la lecture du 8259 sur l'IRR ; le reset la met sur l'ISR

`pic.c:106-113` et `:214-221` : l'ICW1 efface le masque, l'ISR et `mask2`, mais ne touche pas `read`. `pic_reset`
pose `pic.read = 1` (`:36`), c'est-à-dire l'ISR (`:161-162`), et ne remet jamais `pic2.read`.
*Effet* : une lecture de 20h (ou de A0h) sans OCW3 après l'initialisation rend l'ISR, ou ce qu'un OCW3 d'avant a
choisi, au lieu de l'IRR. Les lectures de 20h des ROM du dépôt suivent toutes un OCW3 (XT `F000:E036-E044`, AT
`F000:1BD8`, M24) ; le 5150 ne lit jamais 20h ; les lectures de A0h des AMI restent à examiner.
*Trouvé par* : contre-lecture de la reconnaissance de G13.
*Source* : 8259A p. 10, ICW1, point e : « Special Mask Mode is cleared and Status Read is set to IRR » ; p. 17 :
« After initialization the 8259A is set to IRR ». Documenté.
*Cas qui discrimine* : OCW3 0Bh (l'ISR), puis ICW1 à ICW4 du maître, OCW1 FFh, un tic du PIT en attente ; IN 20h :
PCem 00h (l'ISR) ; le 8259A 01h (l'IRR, que l'IMR n'affecte pas).
*G13* : (a) — documenté, vérifiable en C# seul ; l'effet sur les logiciels est inconnu.
*Reproduit* : `Models/pic.cs`, `pic_reset`, `pic_write` et `pic2_write`, marqueurs `fixed in hardware mode: PB-255`.
*Corrigé en mode matériel* (G13.4) : sous `if (materiel.pb_255)`, ICW1 remet la lecture sur l'IRR (`pic_icw1_255_materiel`), et la mise sous tension aussi, aux deux contrôleurs (`pic_reset_255_materiel`, `Models/pic.Materiel.cs`). `materiel-cas PB-255` (XT : OCW3 0Bh, ICW1 à ICW4, OCW1 FFh, une IRQ 0 en attente, IN 20h ; AT : la mise sous tension, IN 20h et IN A0h) rend 00h, puis 00h et 00h en mode PCem, 01h, puis 01h et 01h en mode matériel ; la correction coupée, il rougit. PICBANC : le premier relevé, 01h au lieu de 00h.

### PB-256 — Ni la M24 ni le PC1512 n'ont de rafraîchissement par DMA

`model.c:205` : seul `xt_init` branche la sortie 1 du PIT sur `pit_refresh_timer_xt`, une lecture DMA du canal 0.
`ams_init` (`:259-270`) et `olim24_init` (`:292-300`) ne le font pas : le compteur 1 du PIT n'y commande rien.
*Effet* (déduit) : ces deux UC ne perdent aucun cycle de rafraîchissement et vont plus vite que les vraies (sur le
5150, IBM compte 7 % de la bande passante du bus, PC TR p. 2-8), et l'adresse courante du canal 0 n'avance jamais.
C'est le pendant de PB-03 pour la fidélité du temps.
*Trouvé par* : contre-lecture de la reconnaissance de G13.
*Source* : Amstrad, PC1512 Technical Reference Manual, section 1, § 1.5, 1.5.1 et 1.7.2 : le canal 0, demandé par la
sortie 1 du 8253, toutes les 15,13 µs ; la ROM 1.43 de la M24 pose le canal 0 en 58h et le compteur 1 à 13h, 15,9 µs
(`F000:DC41-DC5A`). Documenté ; le coût d'un rafraîchissement en cycles de ces deux UC ne l'est pas.
*Cas qui discrimine* : PC1512 ou M24 après le POST, l'adresse courante du canal 0 (deux lectures de 00h) relevée à
1 ms d'intervalle : PCem, la même ; la machine, une soixantaine de transferts plus loin.
*G13* : (b) — mécanisme documenté, temps déduit ; il change le temps de deux machines (question n° 2 de D6-contre).
*Reproduit* : `Models/model.cs`, `olim24_init` et `ams_init`, marqueurs PB-256.

### PB-257 — Le cycle de DMA est facturé avant les tests de masque et de mode

`dma.c:511-517` et `:579-585` : sur une machine non AT, `refreshread()` (`FETCHCOMPLETE` et quatre cycles,
`808x.c:82-85`) est appelé avant les tests du masque et du mode : un transfert refusé coûte le cycle d'un transfert
fait.
*Effet* (déduit) : du temps d'UC facturé à tort sur le 5150, l'XT, la M24 et le PC1512, chaque fois que le canal 0 est
masqué ou pas encore programmé, et à chaque tentative refusée d'un périphérique. Corriger PB-03 seul porterait ces
cycles fantômes au TSC : les deux vont ensemble.
*Trouvé par* : contre-lecture de la reconnaissance de G13.
*Source* : 8237A p. 8, « Mask Register » : un masque « disable(s) the incoming DREQ » — pas de requête, donc pas de
cycle de bus. Documenté ; l'effet sur le temps est déduit.
*Cas qui discrimine* : en C# seul, un 5150, le canal 2 masqué, `dma_channel_write(2, …)` : PCem rend `DMA_NODATA` et
débite le cycle (`FETCHCOMPLETE`, `memcycs += 4`) ; le 8237A ne fait aucun cycle.
*G13* : (a) — documenté, vérifiable en C# seul ; à corriger avec PB-03 (G13.3).
*Reproduit* en mode PCem : `Models/dma.cs`, `dma_channel_read` et `dma_channel_write`, marqueurs `fixed in hardware mode: PB-257`.
*Corrigé en mode matériel* (G13.3, avec PB-03) : sous `if (materiel.pb_257)`, `dma_cycle_materiel` (`Models/dma.Materiel.cs`) ne facture le cycle que d'un transfert accepté (canal démasqué, au mode du transfert). `materiel-cas PB-257` rend la valeur de PCem en mode PCem, celle du matériel en mode matériel, la sonde comptant la correction ; la correction coupée (`--attendu materiel` en mode PCem), il rougit. Le cas : un PC, canal 2 masqué, `dma_channel_write` : 4 cycles de bus facturés (PCem) contre 0 ; démasqué en écriture, 4 des deux côtés.

### PB-258 — L'erreur de division empile dans le segment d'un préfixe

`808x.c:3595`, `:3637`, `:3730`, `:3754` (l'INT 0 de DIV et IDIV, octet et mot) : `writememw(ss, (SP - 2) & 0xFFFF, …)`.
Un préfixe de segment pose `ds = ss = …` (`808x.c:1737` et ses voisins) pour l'adressage par BP, et la fin
d'instruction remet `ss` (`:3924-3928`). Les PUSH et les CALL remettent `ss = oldss` avant d'empiler (`if
(cpu_state.ssegs) ss = oldss;`) ; le chemin de l'erreur de division ne le fait pas.

*Effet* : `DS: DIV BL` avec BL = 0 empile les drapeaux, CS et IP dans le segment de DS, à l'offset SP − 6 : la pile
reste intacte, une donnée est écrasée, et l'IRET du gestionnaire dépile ce qui se trouvait sur la vraie pile. En mode
PCem, seul le diviseur nul y mène ; en mode matériel, chaque débordement de quotient aussi (PB-169).
*Trouvé par* : SST en mode matériel, G13.3 — après PB-169, les cas de débordement des formes `F6.6` à `F7.7` qui
portent un préfixe de segment échouaient sur une écriture absente (« mem[0xD6551] = 0x90, attendu 0x46 »).
*Source* : documenté — « When an interrupt service procedure is entered, the flags, CS, and IP are pushed onto the
stack » (8086 Family User's Manual, 9800722-03, chapitre 2, « Interrupt Procedures ») : la pile, SS:SP ; un préfixe
de segment ne vaut que pour l'opérande mémoire de son instruction. Mesuré : SST 8088 v2, les cas préfixés de `F6.6` (« div ah » sous DS:, pile en SS:SP − 6).
*Cas qui discrimine* : `DS: DIV BL`, BL = 0, DS = 3000h, SS:SP = 0000:FFFE : l'IP empilé (0103h) en 3FFF8h (PCem), en
0FFF8h (8088).
*G13* : (a) — vérifiable par SST ; avec PB-45 et PB-169, la même erreur de division.
*Reproduit* en mode PCem : `Cpu/808x.cs`, les quatre chemins de l'erreur de division, marqueurs `fixed in hardware
mode: PB-258`.
*Corrigé en mode matériel* (G13.3, avec PB-45 et PB-169) : `int0_materiel` (`Cpu/808x.Materiel.cs`), le chemin de
l'erreur des corrections de DIV et d'IDIV, remet `ss = oldss` sous un préfixe avant d'empiler. `materiel-cas PB-258`
rend 3FFF8h en mode PCem, 0FFF8h en mode matériel, la sonde comptant la correction ; la correction coupée
(`--attendu materiel` en mode PCem), il rougit. SST en mode matériel : avec lui, `F6.6`, `F6.7`, `F7.6` et `F7.7`
passent entières (PB-169). Le chemin R9 d'AAM 0 (PB-46), du C# propre, a le même geste et le garde dans les deux
modes : le corriger changerait le code machine d'`execx86` en mode PCem, et l'oracle n'y a pas de comportement.

### PB-259 — Le 8237 rend ses lectures illégales avec le dernier octet écrit, et son état sans bits de requête

`dma.c:82-85` et `:344-347` : la lecture de l'état (08h, D0h) ne rend que les bits TC ; ses bits 4 à 7, les requêtes
en cours, restent à 0 (`dma_stat_rq` n'est lu que par le PS/2, `:198-204`). `dma.c:91` et `:349` : les lectures que
la fiche dit « illégales » (09h à 0Ch, 0Eh et 0Fh au 8237 bas ; D2h à D8h, DCh et DEh au haut) rendent le dernier
octet écrit dans le registre (`dmaregs`, `dma16regs`).
*Effet* : un programme qui attend qu'un canal demande (un bit de requête) ne le voit jamais ; une lecture illégale
rend une valeur qu'aucun 8237 ne rend sans doute, mais que personne n'a mesurée.
*Trouvé par* : reconnaissance de G12 (contre-lecture du DSP et du DMA), avec PB-157 ; inscrit à part en G13.4, pour
que la part documentée de PB-157 se corrige seule.
*Source* : 8237A (231466-005) p. 9 : l'état, « Bits 4–7 are set whenever their corresponding channel is requesting
service » ; la figure 6, les lectures « Illegal ». Les bits de requête sont documentés ; la valeur d'une lecture
illégale est inconnue.
*Cas qui discrimine* : OUT 0Ah,05h puis IN AL,0Ah : PCem 05h ; le 8237A, inconnu. Un canal dont la ligne DREQ est
haute, l'état relu : le 8237A pose son bit de requête ; PCem 0.
*G13* : (c) — la valeur d'une lecture illégale est à mesurer sur un 8237A-5 et sur les contrôleurs intégrés des jeux
de puces du dépôt ; les bits de requête demandent une ligne DREQ que PCem n'a pas, et un modèle qui garderait une
requête après chaque transfert arrêterait le POST de l'XT (« HOT TIMER 1 OUTPUT », XT TR p. 5-30), et celui de la
M24, dont la ROM 1.43 fait le même test (`F000:DC5C-DC62`).
*Reproduit* : `Models/dma.cs`, `dma_read` (l'état, les lectures illégales) et `dma16_read` (de même), marqueurs PB-259.

### PB-260 — La 1542C, maître de bus, passe outre son canal de DMA et le 8237

`scsi_aha1540.c:411` et suivantes : la 1542C lit et écrit la mémoire directement (`mem_readb_phys`,
`mem_writeb_phys`) : la mailbox, les CCB, les données, les commandes 03h, 1Ah et 1Bh. Elle ne regarde ni le masque ni
le mode de son canal (7 par défaut), ni la commande du 8237 haut.
*Effet* : un programme qui masque le canal de la 1542C, ou désactive le 8237 haut, ne suspend pas ses transferts.
*Trouvé par* : contre-lecture de la carte mère, reconnaissance de G13, avec PB-253 ; inscrit à part en G13.4, au
domaine du stockage.
*Source* : IBM AT TR p. 1-26 (–MASTER : un maître de bus prend le bus par un canal du 8237 en mode cascade) ; 8237A
p. 5-6 (Cascade Mode). Documenté par composition.
*Cas qui discrimine* : ibmat avec une 1542C sur le canal 7, OUT D4h,07h (le canal 7 masqué), puis une lecture SCSI :
PCem la fait ; l'AT la bloque jusqu'au démasquage.
*G13* : (a) — documenté par composition ; au domaine du stockage (G13.7), avec la cascade de PB-253.
*Reproduit* : `Scsi/scsi_aha1540.cs`, les quatre chemins du maître de bus, marqueurs PB-260.

### PB-261 — La lecture à distance de la souris PS/2 (EBh) ne remet pas ses compteurs à zéro

`mouse_ps2.c:90-109`, commande EBh : le paquet part avec `mouse->x` et `mouse->y`, qui ne reviennent pas à zéro ; seul
le paquet de flux les remet (`:210`). En mode distant, où le poll n'envoie rien, ils continuent d'accumuler.
*Effet* : un pilote en mode distant qui lit la souris deux fois de suite reçoit deux fois le même mouvement : le
pointeur part deux fois plus loin, puis dérive tant que la souris ne bouge pas.
*Trouvé par* : contre-lecture de G13.4c.
*Source* : Chapweske, « The PS/2 Mouse Interface », 2001, « Read Data (EB) » : après le paquet, la souris remet ses
compteurs de mouvement à zéro (secondaire). Documenté (secondaire).
*Cas qui discrimine* : la souris neuve, un mouvement de (5, 3) accumulé, puis EBh deux fois : PCem FAh 00h 05h FDh deux
fois ; la souris, la seconde fois, FAh 00h 00h 00h.
*G13* : (a) — avec PB-94 et PB-95, la souris PS/2.
*Reproduit* en mode PCem : `Mouse/mouse_ps2.cs`, commande EBh, marqueur `fixed in hardware mode: PB-261`.
*Corrigé en mode matériel* (G13.4) : sous `if (materiel.pb_261)`, après le paquet, les compteurs à zéro
(`mouse_ps2_lu_materiel`, `Mouse/mouse_ps2.Materiel.cs`). `materiel-cas PB-261` rend le cas ci-dessus, et rougit la
correction coupée.

### PB-262 — Le décalage immédiat d'un BT, BTS, BTR ou BTC 16 bits n'est pas réduit modulo 16

`x86_ops_bit.h:205` (`opBA_w_a16`) et `:255` (`opBA_w_a32`) : `count = getbyte();`, puis `temp & (1 << count)`,
`temp |= (1 << count)` et ses voisins, sur un mot. Le compte n'est pas réduit modulo 16. (En C, un décalage de 32 ou
plus est indéfini ; GCC sur x86 le réduit modulo 32, et le C# aussi : 20h vise donc le bit 0, juste.) Le bit visé sort
du mot quand `count & 31` vaut 16 ou plus. Les formes 32 bits (`opBA_l_*`) tombent juste, la réduction modulo 32 étant
la leur.

*Effet* : `BT r/m16,imm8` avec un immédiat dont le bit 4 est posé (10h à 1Fh, 30h à 3Fh…) rend CF nul quel que soit le
bit ; BTS, BTR et BTC n'écrivent rien. 128 immédiats sur 256, d'où la moitié des cas de `0FBA.5` et `0FBA.7`.
*Trouvé par* : SST 386 en mode matériel (G13.5), forme `0FBA.7` restée à 51 % après PB-183.
*Source* : documenté — « If the bit base operand specifies a register, the instruction takes the modulo 16, 32, or 64
of the bit offset operand ... The immediate bit offset ... modulo 16 or 32 » (SDM vol. 2, page BT) ; mesuré, SST 386
(386EX), `0FBA.4` à `0FBA.7` (1 262 à 1 900 sur 2 500 chez PCem).
*Cas qui discrimine* : DX = 6D16h, `BTC DX,53h` → DX = 6D16h (PCem) ; 6D1Eh, le bit 3 basculé (386).
*G13* : (a) — documenté et mesuré.
*Reproduit* en mode PCem : `Cpu/386_ops_bit.cs`, `opBA_w_a16` et `opBA_w_a32`, marqueurs `fixed in hardware mode: PB-262`.
*Corrigé en mode matériel* (G13.5) : sous `if (materiel.pb_262)`, le compte réduit modulo 16 (`bt_immediat_materiel`,
`Cpu/386.Materiel.cs`). `materiel-cas PB-262` rend le cas ci-dessus, et rougit la correction coupée. SST en mode
matériel : `0FBA.4` à `0FBA.7` 2 487 sur 2 500 chacune (contre 1 262 à 1 900), le reste étant des mots à cheval
sur FFFFh (PB-189) ; les formes 32 bits (`660FBA.*`) ne bougent que par LOCK (PB-182).

## B. Comportement indéfini en C

### PB-07 — `readmemw` déréférence un `uint16_t*` au-delà de l'allocation

`mem.c:1344` `ram = malloc(mem_size * 1024);` — pas un octet de marge — puis
`808x.c:79` (et `:110`, `writememw`) :

```c
return *(uint16_t *)(readlookup2[(s + a) >> 12] + s + a);
```

Un accès **mot** au sommet de l'espace adressable lit son octet haut **hors
allocation** : du tas adjacent.

*Effet* : valeur non déterministe. Mesuré sur `POP CX` avec `SS = 0xFFFF`, `SP = 0x000F` :
trois exécutions du même cas rendent `0x0E59`, `0x4959`, `0xD859`.
*Trouvé par* : le fuzzer différentiel, ronde 1691 — inatteignable jusqu'à ce que le
remplissage à motif de deux octets débloque les rondes au-delà de la 157.
*Exception à la règle « on reproduit »* : un comportement indéfini n'est pas un
comportement, il n'y a rien dont être le pendant fidèle. `h_pad_ram()` (`tools/oracle/harness.c:562-566`)
donne quatre octets à zéro au `ram` de l'oracle, comme `mem_alloc` (`Memory/mem.cs`, `new byte[mem_size * 1024 + 4]`)
le fait côté C#, et la divergence est consignée au registre des omissions de `TRANSCRIPTION.md`. Ce n'est pas la
seule déviation de l'oracle, contrairement à ce que disait cette entrée : `__wrap_rom_init` (PB-24,
`harness_stubs.c:206`), le `nvrram` de la M24 (`harness.c:2075-2079`), le fil du S3 (`TRANSCRIPTION.md`).
*Source* : IBM PC TR 6025008 p. 2-3, « The processor supports 20 bits of addressing (1 megabyte of storage) » : un mot
en FFFFFh prend son octet haut en 00000h. Documenté ; au-dessus de la RAM, l'octet vient de ce qui est projeté à
l'adresse suivante, ou du bus flottant (FFh dans le modèle de PCem ; inconnu sur un 5150, à mesurer).
*Cas qui discrimine* : POP CX, SS = FFFFh, SP = 000Fh, sur la RAM plate du fuzzeur : CH = 00h (la marge) des deux
côtés ; le 8088, l'octet en 00000h. MOV AX,[000Fh], DS = 9FFFh, un 5150 à 640 Ko : AH = 00h aujourd'hui ; par le
chemin lent, ce que projette A0000h (rien avec une CGA : FFh).
*G13* : (a) le repli à 1 Mo et l'octet haut lu par le chemin lent ; (c) la valeur du bus flottant, inconnue.
*NON reproduit* (neutralisé des deux côtés) : la marge de quatre octets nuls, lue zéro par l'oracle comme par le C#.
Sans marqueur jusqu'à G13.1, il porte depuis les marqueurs `not reproduced: PB-07` de `Memory/mem.cs` (`mem_alloc`,
avec sa `DEVIATION`) et de `Cpu/808x.cs` (`readmemw`, `writememw`), posés par le domaine du processeur.
*Corrigé en mode matériel* (G13.3, avec PB-179) : sous `if (materiel.pb_07)`, un mot dont l'octet bas finit une page (`(s + a) & 0xFFF == 0xFFF`) se lit et s'écrit octet par octet, chacun par sa propre page (`readmemw_materiel`, `writememw_materiel`, `Cpu/808x.Materiel.cs`) : au repli de 1 Mo, l'octet haut vient de 00000h (rammask) ; au sommet de la RAM, de ce que projette l'adresse suivante (FFh sans rien, le bus flottant de PCem : (c), inconnu sur un 5150). Le mode PCem garde la marge nulle de `mem_alloc`. Un défaut neutralisé peut être corrigé en mode matériel : la porte `recensement` l'admet depuis G13.3 (marqueurs `fixed in hardware mode: PB-07` à côté des `not reproduced`). `materiel-cas PB-07` rend la valeur de PCem en mode PCem, celle du matériel en mode matériel, la sonde comptant la correction ; la correction coupée (`--attendu materiel` en mode PCem), il rougit. Le cas : la page FFh dans le cache, POP CX en FFFF:000F rend 0077h (PCem) contre 5A77h.

### PB-08 — `device.c:300` teste la borne après l'accès

```c
while (devices[c] != NULL && c < 256)
        c++;
```

`devices[c]` est évalué **avant** `c < 256`. Quand les 256 fentes sont prises,
`devices[256]` est déréférencé hors tableau.

*Effet* : en C, lecture de la globale voisine ; en C#, exception. Inatteignable : le dépôt enregistre une vingtaine
de périphériques au plus, et l'invité n'en ajoute pas.
*Source* : sans objet matériel — une limite d'une table de l'émulateur (`DEV_MAX`, 256).
*Cas qui discrimine* : aucun — 256 périphériques ne s'enregistrent jamais.
*G13* : (d) — sans effet et sans vérité matérielle ; inverser les deux opérandes serait neutre dans les deux modes.
*Reproduit* : `PluginApi/device.cs`, `pcem_add_device`, marqueur PB-08 (`:327`).

### PB-09 — `charbuffer` du CGA est débordable par le programme invité

`vid_cga.h:31` déclare `uint8_t charbuffer[256]`, mais `vid_cga.c:405` boucle jusqu'à
`cga->crtc[1] << 1`, et `crtcmask[1] = 0xff` laisse `crtc[1]` monter à 255 — donc la
borne à 510. Même borne à la relecture 80 colonnes (`vid_cga.c:157-158`).

*Effet* : un programme invité qui écrit plus de 128 dans le registre 1 du CRTC, en mode texte 80 colonnes,
écrase en silence les champs voisins de `cga_t`, puis, au-delà de 134, le tas. Le BIOS du 5150 pose 40 ou 80.
*G13* : hors du mode — corrigé dans les deux modes en G13.0 (R9, décision n° 2 de PLAN-G13.md).
*NON reproduit* (R9, G13.0) : `charbuffer` fait 512 octets (`Video/vid_cga.cs`, marqueur PB-09) — ce que lit la
carte, qui n'a pas de tampon de ligne, et identique à PCem tant qu'il survit (R1 = 129 et 130). Le C# levait
`IndexOutOfRangeException`, que rien ne rattrapait : un `OUT` de l'invité arrêtait l'émulateur, et les dernières
écritures des images se perdaient. `r9-cga` le prouve (R1 = 129, 200 et 255).

### PB-10 — Double `fclose` dans `loadbios`

`mem_bios.c:549` ferme `f` dans la branche `ROM_IBMPC` sans le remettre à `NULL`. Si
`mem_load_basic("ibmpc")` échoue ensuite, le `break` mène au `if (f) fclose(f);` de
`:1280-1281`, qui referme un `FILE*` déjà fermé.

*Effet* : double libération, uniquement sur le chemin d'échec du chargement de la ROM
BASIC. `Stream.Close()` étant idempotent en .NET, sans conséquence dans iXtal26.
*Source* : sans objet matériel — le chemin d'échec du chargement d'une ROM de l'hôte.
*Cas qui discrimine* : aucun — `Stream.Close()` est idempotent.
*G13* : (d) — sans effet dans iXtal26 et sans vérité matérielle.
*Reproduit* : `Memory/mem_bios.cs`, `loadbios`, marqueur PB-10 (`:284`, numéroté par le domaine du processeur).

### PB-16 — `img_load` éjecte sans remettre `f` à NULL

`disc_img.c:248-249` et `:276-277`

Sur une image de plus de 25 000 octets par piste, ou XDF de géométrie inconnue, `img_load` fait
`fclose(img[drive].f); return;` sans annuler le pointeur. `disc_load` marque pourtant le lecteur plein
(`drive_empty = 0`, `disc.c:81`) et appelle `fdd_disc_changed`. L'`img_seek` sur le `FILE *` fermé n'est pas
atteint, en C non plus : `disc_seek` teste le pointeur de fonction (`disc.c:208`), que `disc_close` et `disc_reset`
ont remis à nul (`disc.c:94-107`, `:182-201`) et qu'`img_load`, sorti tôt, n'a pas reposé. Le comportement indéfini
est le second `fclose`, sur le pointeur pendant, à la prochaine `disc_close` ou au prochain `disc_reset`
(`img_close`, `disc_img.c:323-327`) : chaque reset matériel en fait un (`pc.c:366`), et la glibc peut s'y arrêter
sur un « double free », chez PCem seulement (D3-contre, K11).

*Effet* : comportement indéfini sur toute image rejetée par ces deux branches. Pour l'invité, le lecteur paraît
chargé sans aucune fonction : un changement de disquette, puis « adresse non trouvée » à la lecture
(`disc_notfound`), et READ ID sans réponse jusqu'au délai du BIOS ; le menu Ctrl+F12 annonce « inséré »
(`Host/SdlMenu.cs:974-984`, qui teste `drive_empty`).
*Source* : sans objet matériel — refuser un format d'image n'a pas d'analogue ; l'invité voit ce qu'il verrait d'une
disquette non formatée — déduit.
*Cas qui discrimine* : aucun pour le mode matériel. Pour l'hôte : une image au BPB de 30 × 1 024 octets par piste,
insérée : `drive_empty` vaut 0 et le menu dit « inséré » ; un nettoyage la refuserait avec un avertissement, lecteur
vide.
*G13* : (d) — sans pendant matériel, et ni l'hôte ni les fichiers ne sont en jeu : laissé (décision n° 9).
*Reproduit* : `Disc/disc_img.cs`, les deux sites marqués — le lecteur paraît chargé comme chez PCem ;
`Stream.Close()` est idempotent, le second `fclose` n'a pas de pendant.

### PB-17 — `track_data` fait 20 Ko, une piste XDF ED en demande 23 552

`disc_img.c:9` contre `:347-352`

`track_data[2][20 * 1024]`, mais `img_seek` lit `sectors * sector_size` octets par
face : pour les XDF à densité étendue (`bpb_sectors` 46 ou 48, `:269-273`) c'est
23 552 ou 24 576 octets, soit un débordement de 3 à 4 Ko dans la face suivante puis
dans `img[1]`.

*Effet* : corruption mémoire à la première lecture de piste d'une image de plus de 40 secteurs par piste
(`sector_size` est forcé à 512, `:158`) : une XDF à densité étendue, ou 41 secteurs de 128 octets lus par 512.
*G13* : hors du mode — corrigé dans les deux modes en G13.0 (R9, décision n° 2 de PLAN-G13.md).
*NON reproduit* (R9, G13.0) : `track_data` fait 195 × 512 octets par face (`Disc/disc_img.cs`, marqueur PB-17) ;
une BPB admise annonce au plus 25 000 octets par piste, en secteurs d'au moins 128 octets. Le C# levait
`ArgumentOutOfRangeException` à l'insertion, au démarrage comme au menu ; ces images se lisent désormais entières.
`r9-disquette` le prouve.

### PB-18 — `disc_load` copie `discfns[drive]` sur lui-même

`disc.c:83`, appelé depuis `pc.c:367-368` avec `fn == discfns[drive]`

`strcpy(discfns[drive], fn)` : source et destination sont le même tableau. `strcpy`
est déclaré `restrict` et le chevauchement est indéfini par la norme ; glibc s'en
accommode pour un pointeur identique.

*Effet* : aucun observé.
*Source* : sans objet matériel ; côté C, ISO/IEC 9899:2011 § 7.24.2.3 : un `strcpy` entre objets qui se chevauchent
est indéfini — documenté.
*Cas qui discrimine* : aucun.
*G13* : (d) — sans effet, et rien n'est reproduit en C#.
*NON reproduit* (sans objet en C#) : `Disc/disc.cs`, `discfns[drive] = fn`, une affectation de référence, sans
comportement indéfini ; le résultat est celui de la glibc, la chaîne inchangée. Le marqueur disait « reproduced » :
il dit « not reproduced » depuis G13.1 (D3-stockage, § 4.1).

### PB-21 — `speakval` divise par `pit->l[0]` sans le tester, et le POST y passe

`pit.c:418`, dans `pit_write` :

```c
speakval = (((float)pit->l[2] / (float)pit->l[0]) * 0x4000) - 0x2000;
if (speakval > 0x2000)
        speakval = 0x2000;
```

La ligne est atteinte par **chute de `case`** depuis les trois canaux : toute écriture
aux ports 0x40, 0x41 ou 0x42 la recalcule, même quand `l[0]` n'a pas encore été chargé.
Mesuré sur un amorçage 640 Ko : à la tranche 231, `l[0] = 0` et `l[2] = 65535`, donc
`+inf`. Le `(int)` de PCem rend alors l'**entier indéfini** de `cvttss2si`, 0x80000000 —
et le clamp de la ligne suivante ne le rattrape pas, `INT_MIN` n'étant pas `> 0x2000`.
Les deux commentaires laissés par l'auteur entre le calcul et le plafond (`pit.c:420-421`,
`"Speaker overflow"`) disent qu'il a soupçonné le débordement sans le fermer.

*Effet* : `speakval` sort de sa plage nominale [−0x2000, +0x2000] et vaut `INT_MIN` tant que le canal 0 garde le
compte 0 que le BIOS y charge (18,2 Hz) : chaque écriture au PIT le recalcule avec ce 0, et 0/0 (`l[2]` nul) rend aussi
l'entier indéfini (corrigé en G13 : l'entrée disait « jusqu'à la prochaine écriture au PIT »). Il n'est lu que par
`speaker_update` (`sound_speaker.c:24`) quand `pit.m[2]` vaut 0 ou 4 ; le bip du POST est en mode 3, donc l'audition
n'en dépend pas. Tronqué en `int16_t` à `sound_speaker.c:24`, `INT_MIN` donne 0 — le silence, là où la valeur
nominale aurait donné une tension.

*Source* : Intel, fiche 8254 (231164-005), PDF p. 17 : « The largest possible initial count is 0; this is equivalent to
2^16 for binary counting », convention que PCem applique déjà dans `pit_load` (`pit.c:119`). Documenté pour le compte
0 ; `speakval`, le rapport des comptes des canaux 2 et 0, est le modèle de PCem, sans pendant sur la carte.
*Cas qui discrimine* : canal 0 chargé à 0 (43h ← 36h, 40h ← 00h 00h), canal 2 en mode 0 à FFFFh (43h ← B0h, 42h ← FFh
FFh) : `speakval` vaut INT_MIN chez PCem (sortie 0 en mode 0), 1FFFh avec le compte 0 lu comme 65 536
(`--speaker-check`).
*G13* : (b) — la correction est certaine, mais la valeur audible reste celle du modèle de PCem.
*Reproduit* : `Models/pit.cs`, `pit_write`, marqueur PB-21, avec la garde explicite qu'impose .NET — voir la
`DEVIATION` sur place. C'est le seul endroit du dépôt où une conversion flottant→entier
devait être écrite à la main : .NET **sature** (`(int)float.PositiveInfinity` vaut
`int.MaxValue`, que le clamp ramène alors à 0x2000) là où x86 rend l'entier indéfini.
Trouvé par la sonde `speaker-probe` de M9, au premier tir.

### PB-24 — `rom_init` expose 12 Ko de tas non initialisé à l'invité

`rom.c:60-62` :

```c
rom->rom = malloc(size);
fseek(f, file_offset, SEEK_SET);
fread(rom->rom, size, 1, f);
```

Le retour de `fread` est **ignoré**. Quand le fichier est plus court que `size`, la queue
de l'allocation garde ce que `malloc` a rendu, et `mem_mapping_add` la publie à l'invité.

Les deux appels du Fixed Disk Adapter le font tous les deux :

| Appel | `size` | Fichier | Non initialisé |
|---|---|---|---|
| `mfm_xebec.c:757` | `0x4000` | `ibm_xebec_62x0822_1985.bin`, 4 096 o | **12 288 o** |
| `mfm_xebec.c:793` | `0x4000` | `dtc_cxd21a.bin`, 8 192 o | **8 192 o** |

Le masque passé vaut `0x3fff` : les 16 Ko sont adressables, rien ne replie la lecture sur
la partie chargée.

*Atténué en pratique* : l'en-tête des deux ROMs déclare sa vraie longueur — `55 aa 08`
pour le Xebec (8 × 512 = 4 096) et `55 aa 10` pour le DTC (16 × 512 = 8 192) — donc le
balayage de ROM d'extension du POST ne somme et n'exécute que ce qui est chargé. Il faut
un accès explicite de l'invité au-delà pour voir le tas.
*G13* : hors du mode — non reproduit dans les deux modes (comportement indéfini en C, exception assumée) ; rien n'y bascule.
*NON reproduit — divergence assumée* : `Flash/rom.cs:123` alloue un tableau CLR, donc
**zéro**. Ce n'est pas un comportement dont être le pendant fidèle : c'est de l'UB, et
trois exécutions donnent trois valeurs. Même arbitrage que `h_pad_ram` — un oracle qui
tire aux dés n'est pas un oracle. Voir le registre des omissions de `TRANSCRIPTION.md`.
*G7.1 — un troisième appel, qui mordait.* `vid_vga.c:107` : `rom_init(…, 0x8000, 0x7fff,
0x2000, …)` sur `ibm_vga.bin`, 32 Ko lus À PARTIR de 0x2000 — 24 Ko chargés, **8 Ko de tas**
en C6000-C7FFF. Ici l'en-tête n'atténue rien : le balayage des ROM d'extension du 5150 lit
C600:0000, juste après les 24 Ko déclarés. Le tas y est le plus souvent nul ; une fois, en
série (G7.0, `bd-pcdos-vga`), il valait 8C 50 — divergence à l'instruction 1 078 553.
`MALLOC_PERTURB_=85` la rend déterministe (oracle AAAA, C# 0000). L'oracle enveloppe
désormais `rom_init` (`--wrap`, `harness_stubs.c`) et met à zéro ce que le fichier n'a pas
fourni : les trois appels — Xebec, DTC, VGA — sont déterministes et égaux au C#. Le PCem
vendoré reste intact ; l'arbitrage est inchangé, il est seulement appliqué AUSSI à l'oracle.


### PB-31 — `cga_close` libère un `mem_mapping_t` encore chaîné, et `closepc()` le traverse

Trois lignes, chacune correcte seule, qui composent un `free` suivi d'un déréférencement.

1. `cga_t` porte son mappage **par valeur** : `mem_mapping_t mapping;` (`vid_cga.h:4`), chaîné
   dans la liste globale par `mem_mapping_add(&cga->mapping, 0xb8000, …)` (`vid_cga.c:432`).
2. `cga_close` (`vid_cga.c:441-446`) fait `free(cga->vram); free(cga);` — **sans
   `mem_mapping_remove`**. Le maillon reste donc dans la liste, en pointeur pendant.
3. `mem_mapping_remove` (`mem.c:1158-1175`) parcourt sans garde de fin :

```c
prev = &base_mapping;
dest = prev->next;
while (dest != mapping) { prev = dest; dest = dest->next; }
```

Il n'y a **pas** de `dest != NULL` : un maillon libéré, et la boucle part dans la mémoire
réallouée jusqu'à la faute.

*Atteignable chez PCem, pas seulement ici.* `closepc()` (`pc.c:576-592`) appelle
`device_close_all()` (`:589`), et `closepc()` **est appelé** — `wx-sdl2.c:649`,
`qt-sdl2.c:701`, `pc.c:601`. `device_close_all` (`device.c:34-44`) ferme dans l'ordre
croissant des indices, et `video_init()` (`pc.c:374`) précède `hdd_controller_init()`
(`pc.c:392`) : la CGA est donc libérée **avant** la carte de disque dur, dont le
`xebec_close` appelle `rom_deinit` (`mfm_xebec.c:769`) donc `mem_mapping_remove`
(`rom.c:113`). Quitter PCem sur une machine CGA + Fixed Disk Adapter suit ce chemin.

*Effet* : faute de segmentation à la fermeture. Reproduit dans l'oracle en ajoutant
`device_close_all()` à `h_closepc`, trace obtenue sous gdb :

```
#0 mem_mapping_remove  mem.c:1170      dest = dest->next;
#1 rom_deinit          rom.c:113
#2 xebec_close         mfm_xebec.c:769
#3 device_close_all    device.c:40
```

*G13* : hors du mode — non reproduit dans les deux modes (comportement indéfini en C, exception assumée) ; rien n'y bascule.
*NON reproduit — divergence assumée*, et elle ne se choisit pas : le C# n'a pas de `free`.
`cga_close` n'y libère rien, la liste de mappages reste parcourable, et
`mem_mapping_remove` trouve sa cible. Le côté C# appelle donc `device_close_all()`
fidèlement (`pc.cs`, `closepc`) ; l'oracle, qui est une bibliothèque et non un processus
qui s'arrête, vide ses tampons par `fflush(NULL)` et le dit sur place. Même famille que
`h_pad_ram` et `PB-24` — ce qui diverge est la gestion mémoire manuelle, pas un
comportement émulé. VERIFICATION.md § M13.


### PB-46 — AAM 0 divise par zéro : SIGFPE

`808x.c:3280-3286` :

```c
case 0xD4: /*AAM*/
        tempws = FETCH();
        AH = AL / tempws;
        AL %= tempws;
```

Aucune garde : un octet immédiat nul est une division entière par zéro, comportement indéfini
en C, SIGFPE sur un hôte x86. Le cœur 286/386 ne tombe pas, mais pour une autre raison :
`x86_ops_bcd.h:31-32` remplace une base nulle par 10 — ni plantage, ni INT 0.

*Effet* : un programme invité qui exécute `D4 00` fait **tomber PCem** (mesuré : « Floating
point exception », code 136). Un 8088 lève INT 0 — SingleStepTests/8088, forme `D4`, 47 cas :
SP − 6, IP poussé après l'instruction, AX inchangé.
*Trouvé par* : audit du 26/09 (D3).
*G13* : hors du mode — non reproduit dans les deux modes (comportement indéfini en C, exception assumée) ; rien n'y bascule.
*NON reproduit*, exception assumée comme PB-24 : un oracle qui meurt n'a rien à reproduire.
`Cpu/808x.cs`, marqueur `// pcem bug, not reproduced: PB-46` : garde vers le chemin de l'erreur
de division, celui de DIV par zéro (F6 /6), 83 cycles, marquée `DEVIATION`. Mesuré : le C#
ne tombe plus (il levait `DivideByZeroException`, code 134). Les 47 cas `AAM 0` restent
**non gagnés** : le silicium pousse des drapeaux déjà recalculés (octet bas 0x46 — ZF et PF
posés, SF, AF et CF effacés), le C#, comme le chemin de DIV, les pousse inchangés.

### PB-47 — IDIV divise INT_MIN par -1 : SIGFPE

`808x.c:3743` (F7 /7, 8088), `x86_ops_misc.h:361` et `:473` (F7 /7 en `_w`, 286/386) et
`386_common.c:210-226` (`idivl`, F7 /7 en `_l`) :

```c
tempws = (int)((DX << 16) | AX);
...
tempws2 = tempws / (int)((int16_t)dst);
```

Avec `DX:AX = 0x80000000` et un diviseur `0xFFFF` (-1), le C divise `INT_MIN` par -1 :
comportement indéfini, SIGFPE sur un hôte x86. Même chose en 32 bits dans `idivl` avec
`EDX:EAX = 0x8000000000000000` et -1. Le 286/386 teste pourtant le débordement du quotient,
mais **après** la division, trop tard.

*Effet* : un programme invité fait **tomber PCem**. Le C# levait `OverflowException`, qui
abattait l'hôte (code 134, mesuré sur les trois formes). Le quotient, +2³¹ ou +2⁶³, ne tient
pas dans la destination : le silicium lève #DE.
*Trouvé par* : audit du 26/09 (D2).
*G13* : hors du mode — non reproduit dans les deux modes (comportement indéfini en C, exception assumée) ; rien n'y bascule.
*NON reproduit*, exception assumée comme PB-24 et PB-46 : un oracle qui meurt n'a rien à
reproduire. Marqueurs `// pcem bug, not reproduced: PB-47` : `Cpu/808x.cs` (garde vers le
chemin de la division par zéro, 165 cycles, marquée `DEVIATION`), `Cpu/386_ops_misc.cs` (deux
sites `_w`, `x86_int(0)` comme la branche de débordement) et `Cpu/386_common.cs` (`idivl`).
Mesuré par un pas C# seul : les trois formes prennent INT 0 (IVT[0], SP − 6) ; un IDIV
ordinaire (-100 / -1) est inchangé. Aucun oracle ne départage : l'oracle meurt, et aucun
cas SST ne tombe sur ces valeurs.

### PB-49 — L'ombre de SS est une récursion : une suite de POP SS ou MOV SS épuise la pile

`x86_ops_stack.h:573-624` (`opPOP_SS_w`, `opPOP_SS_l`) et `x86_ops_mov_seg.h:178-190`,
`:221-233` (branche SS de `opMOV_seg_w_a16`, `_a32`). Après le chargement de SS, le handler
exécute l'instruction suivante lui-même — l'ombre d'interruption — en APPELANT son handler :

```c
x86_opcodes[(fetchdat & 0xff) | cpu_state.op32](fetchdat >> 8);
return 1;
```

Ce n'est pas un appel terminal : le désassemblage de l'oracle (-O2) montre `opPOP_SS_w`
appelant `opPOP_SS_l.part.0` (`call`, puis `add $8,%rsp`), qui saute au handler suivant. Une
trame par chargement de SS enchaîné. Les préfixes de segment ont la même forme
(`x86_ops_prefix.h:96-165`) mais écrite `return x86_opcodes[…](…)`, et GCC en fait un vrai
saut (`jmp *%rax`, mesuré sur `opES_w_a16`) : eux ne font pas grandir la pile. Le cœur 386
ne fait pas boucler IP en mode réel : une RAM remplie de 0x17 est une chaîne de la taille de
la RAM. Un préfixe intercalé ne la casse pas : dans `17 65 17 65…` (POP SS, préfixe GS), le
préfixe saute sur le POP SS suivant, qui le rappelle — une trame par paire, et l'oracle tombe
de même (fuzzeur 386 en flux, graine 1, ronde 1139). Tout octet qui enchaîne sur la suivante
(préfixes de segment, 66, 67, F0-F3) forme la chaîne avec POP SS ou MOV SS.

*Effet* : un invité qui enchaîne des chargements de SS fait tomber l'hôte — débordement de
pile, SIGSEGV ou abort selon l'endroit où elle casse. Mesuré : `fuzz --core 286|386 --rounds
1 --instr 1 --op 17` (RAM remplie de 0x17) tombe au premier pas, l'oracle d'abord (la pile
.NET s'arrête dans `Oracle.h_step`) ; des chaînes de N POP SS suivies d'un NOP restent
identiques des deux côtés jusqu'à N = 100 000 au moins, et tombent à N = 200 000 (pile de
8 Mo). Le 8088 n'est pas touché : `808x.c` pose `noint` au lieu de récurser. Sur le
silicium, rien ne tombe ; Intel ne garantit l'inhibition que pour le PREMIER chargement de SS
d'une suite.
*Trouvé par* : le fuzzeur en mode flux sur les 256 opcodes, 286 et 386, dont un balayage
opcode par opcode a isolé 0x17 ; confirmé à la ligne de C et au désassemblage.
*G13* : hors du mode — non reproduit dans les deux modes (comportement indéfini en C, exception assumée) ; rien n'y bascule.
*NON reproduit*, exception assumée comme PB-46 et PB-47 : la chaîne est bornée à
`_386.SS_SHADOW_MAX` = 1 024 chargements enchaînés (`Cpu/386_ops_stack.cs`, profondeur
commune aux quatre sites) ; au-delà, le chargement de SS rend la main sans ombre et exec386
reprend l'instruction suivante. Marqueurs `// pcem bug, not reproduced: PB-49` :
`Cpu/386_ops_stack.cs` (POP SS w et l), `Cpu/386_ops_mov_seg.cs` (MOV SS a16 et a32).
`iXtal26.Diff popss-check` : identique à l'oracle jusqu'à 1 024 chargements (POP SS, MOV SS,
mêlés ; 286 et 386) ; au-delà, écart ATTENDU, là où l'oracle survit encore ; RAM entière
remplie de 0x17, C# seul : 200 pas de 1 025 POP SS, sans plantage ni boucle. Sans la borne,
le même test fait tomber le C#.

### PB-93 — Une valeur de configuration hors liste indexe hors des tableaux (SB, cartes SVGA)

`device.c:94-104` rend telle quelle la valeur d'une section de device du .cfg. L'interface de
PCem n'offre que les valeurs de la liste `selection` (`wx-deviceconfig.cc:49-60`) ; un .cfg
écrit à la main en offre d'autres, et certaines sortent des tableaux. La SB avec `dma = 9` :
`dma_channel_read` / `_write` prennent `&dma[9]` (`dma.c:499`, `:569`, huit canaux) au premier
transfert ; avec `addr = 65534` : `io_sethandler` indexe `port_inb[base + c]` au-delà de FFFFh
(`io.c:45`) dès le montage. Les cartes SVGA avec une clé `memory` hors liste : TVGA8900D 0, 3,
-1 ; GD5429 0, 255, -1 ; Trio64 64, -1 — VRAM de taille nulle, masque incohérent ou taille
négative, lus ou écrits hors du tableau dès l'amorçage du BIOS vidéo.
*Effet* : comportement indéfini en C ; en C#, huit des douze cas vidéo et les deux cas SB
s'arrêtaient sur `IndexOutOfRangeException` ou `OverflowException` (mesuré, G8.3).
*Trouvé par* : la vérification des sections de device, G8.3 (`r9-sbcfg`).
*G13* : hors du mode — non reproduit dans les deux modes ; rien n'y bascule.
*Non reproduit* : `PluginApi/device.cs`, `config_hors_liste` — DEVIATION « valeur de
configuration hors liste → défaut, comme l'interface de PCem l'impose », avec un avertissement
sur la sortie d'erreur (section, clé, valeur rejetée, défaut retenu). Les listes `selection` sont
transcrites pour les sept tables de configuration du dépôt. Prix assumé : une valeur hors liste
mais sans danger (la SB à l'IRQ 3) prend aussi le défaut. L'oracle n'est pas touché ; ces valeurs
restent hors des portes, prouvées en C# seul.
*G10.1, la manette* : `joystick_type` hors des sept types (`pc.c:783`, puis
`joystick_get_max_joysticks`, `:786`, `gameport.c:27` ; `gameport_init_common`, `:132`) —
`joystick_list[7]` est le NULL terminal, au-delà on sort du tableau. Ramené au type 0 avec un
avertissement (`pc.cs`), prouvé par `r9-joycfg` (sans la garde : NullReferenceException pour 7,
IndexOutOfRangeException pour 99, mesuré). Et la section [Joysticks], R9 côté hôte :
`joystick_N_nr` hors de 0 à 8 (`plat_joystick_state[nr - 1]`, `wx-sdl2-joystick.c:122`), une
correspondance d'axe ou de chapeau hors des huit axes sans `POV_X` ni `POV_Y` (`axis[mapping]`,
`:95`), une correspondance de bouton hors des 32 (`b[mapping]`, `:128`) : chacune ramenée à son
défaut, avec un avertissement (`pc.cs`, `load_joysticks`), prouvé par `iXtal26 --joystick-check`
(sans la garde : IndexOutOfRangeException, mesuré). Et plus de huit manettes branchées à l'hôte
débordent `sdl_joy[8]` (`:22-23`, `:34`) : les huit premières seulement (`Host/SdlJoystick.cs`).
*G10.4, le lecteur de CD-ROM* : `cd_speed` hors des dix-huit vitesses (`pc.c:780`) — à 0,
`cur_speed` vaut 0 (`scsi_cd.c:227-228`) et la première lecture divise par lui (`:1081`, `:1170`)
— est ramenée à 24 avec un avertissement, une vitesse sans danger (5, -5) aussi. Un `cd_model`
absent de la table fait lire `cd_models[12]`, un au-delà de la table (`MAX_CD_MODEL` vaut 12 pour
douze entrées, `:492-496`, `:510-514`) : refusé, retour 2, comme `lpt1_device` (la clé et
`--cd-model`). `cdrom_channel` hors de -1 à 3 et `cdrom_drive` ni -1 ni 200 (un lecteur physique,
exclu) sont ramenés à -1, avec un avertissement. Prouvé par `r9-cdcfg` (sans la garde :
DivideByZeroException, IndexOutOfRangeException, NullReferenceException, mesuré).
*G12.0, les SB 1.0, 1.5, 2.0 et Pro v1* : leurs trois tables (`sound_sb.c:1134-1228`) reçoivent leurs listes
`selection` ; `mixaddr` de la 2.0 (0, 250h, 260h) comme `addr` peut indexer au-delà de FFFFh (`io.c:45`). Hors
liste : le défaut, averti, prouvé par `r9-sbcfg` (six essais de plus).
*G12.2, l'AWE32* : `emu_addr` (620h à 680h) et `onboard_ram` (0, 512, 2 048, 8 192 et 28 672 Ko) reçoivent leurs
listes (`sound_sb.c:1304-1323`). Hors liste, `emu_addr` près de FFFFh indexerait au-delà (`io.c:45`), et
`onboard_ram` négatif ferait un `malloc` nul suivi d'un `memset` (`sound_emu8k.c:2046-2047`). Le défaut, averti,
prouvé par `r9-awecfg`.

### PB-151 — L'entrée stéréo lit un élément au-delà de `record_buffer`

`sound_sb_dsp.c:1150`, `:1157`, `:1217`, `:1225` : les formats stéréo d'entrée lisent
`record_buffer[record_pos_read + 1]`, et `record_pos_read` monte jusqu'à FFFEh dans un tableau de FFFFh éléments
(`sound_sb_dsp.h:82`). Ce compteur n'est remis à zéro que par le reset du DSP (`:144`) ; il avance à chaque tic
de l'entrée, le mode direct compris (`:1248-1251`). Atteint sur la SB 16 par C8h-CFh et B8h-BFh en stéréo.
Élargi en G13 : l'écriture aussi — `sound_sb.c:195-196` et `:303-304` rangent `record_buffer[c_record & 0xFFFF]`,
l'indice FFFFh compris, dans le même tableau ; ce bloc ne s'exécute pas (PB-148).
*Effet* : en C, la lecture tombe sur `buffer[0]`, le premier échantillon de SORTIE du bloc en cours (la
disposition est mesurée : `int16_t`, sans bourrage) ; une fois tous les 32 768 couples, l'enregistrement reçoit un
échantillon de la lecture.
*Trouvé par* : reconnaissance de G8 (PLAN-G8.md, défaut n° 6), atteint en G12 ; l'écriture, contre-lecture de G13.
*Source* : le tampon d'enregistrement est une construction de l'émulateur, sans pendant sur la carte : le juste est
l'absence d'alias (déduit, et certain).
*Cas qui discrimine* : SB 16, `record_pos_read` = FFFEh, `record_buffer` à 0, `dsp.buffer[0]` = 1234h : C8h 20h 01h 00h
(entrée 8 bits stéréo non signée, 2 octets) : le second octet écrit par le DMA vaut 92h chez PCem, 80h sans alias.
*G13* : (a) — un anneau de 10000h entrées, lu et écrit.
*Reproduit* : `Sound/sound_sb_dsp.cs`, `record_lu` et ses quatre appels, et `Sound/sound_sb.cs`, `record_ecrit` et ses
deux paires d'appels, marqueurs PB-151 ; une assertion statique de `harness.c` fige la disposition. SB16BANC enregistre
au-delà de FFFEh, une sortie en cours.

### PB-152 — Le registre 3Bh du CT1745 indexe hors de sa table

`sound_sb.c:655` : `speaker = sb_att_2dbstep_5bits[regs[0x3B] * 3 + 22]` lit sans `>> 6` : dès que 3Bh vaut 04h
ou plus, l'indice va jusqu'à 787 dans une table de 32. Le calcul est refait à chaque écriture de donnée du
mélangeur, celle de 3Bh comprise (`:639-669`). `speaker` n'est lu nulle part (TODO `:667`).
*Effet* : en C, une lecture dans `.rodata`, sans conséquence observable ; en C#, une exception à la première
écriture d'un volume de haut-parleur (C0h, la valeur que posent les pilotes).
*Trouvé par* : reconnaissance de G12 (lectures des cartes et du DSP).
*G13* : hors du mode — non reproduit dans les deux modes ; rien n'y bascule.
*NON reproduit* : l'indice borné (`Sound/sound_sb.cs`, marqueur PB-152) ; rien d'observable ne change, `speaker`
reste hors de la sonde. Ce n'est pas un site R9 : PCem ne s'y arrête pas. SB16BANC écrit 3Bh = C0h sous l'oracle.

### PB-156 — `len × sb_freq` déborde l'entier signé

`sound_sb.c:202`, `:325` : `record_pos_write += ((len * sb->dsp.sb_freq) / 48000) * 2`. Avec 40h FFh,
`sb_freq` vaut 1 000 000 et `len` 2 400 : 2,4 × 10^9, au-delà de 2^31.
*Effet* : comportement indéfini, que GCC compile en `imul` qui enveloppe ; sans effet audible, l'enregistrement
étant muet (PB-148).
*Trouvé par* : reconnaissance de G12 (contre-lecture des cartes).
*Source* : de la comptabilité de l'émulateur, sans pendant sur la carte : le juste est l'arithmétique exacte (déduit).
40h FFh (1 MHz) sort des normes (guide de Creative, table 3-2, p. 3-9), et le DSP 4.05 borne la constante à EBh
(`v405-8k_e51aff23.asm:1688-1692`, PB-155) : le cas est inatteignable sur la carte.
*Cas qui discrimine* : SB 16, 40h FFh (`sb_freq` = 1 000 000), `record_pos_write` = 1000h : après un tampon de
2 400 échantillons, PCem pose DB94h (l'ajout vaut −78 956, enveloppé), l'arithmétique exacte 96A0h (+100 000).
*G13* : (a) — sans effet tant que PB-148 n'est pas corrigé, et inatteignable si le bornage de PB-155 l'est.
*Reproduit* : `Sound/sound_sb.cs`, marqueurs PB-156, par l'arithmétique enveloppante du C#
(`CheckForOverflowUnderflow` faux).

### PB-105 — `sw_close` libère la SideWinder sans retirer ses chronomètres

`joystick_sw_pad.c:81-85` : `free(sw)`, alors que ses deux chronomètres (`poll_timer` et
`trigger_timer`, posés par `timer_add`, `:75-76`) restent dans la liste de `timer.c`. Atteignable
par un changement de type de manette à chaud depuis l'interface (`gameport_update_joystick_type`,
`gameport.c:140-148`) : les chronomètres battent alors sur une mémoire libérée.
*Effet* : comportement indéfini en C, hors d'atteinte de l'invité.
*Trouvé par* : transcription de G10.1.
*G13* : hors du mode — non reproduit dans les deux modes ; rien n'y bascule.
*Non reproduit* : `Joystick/joystick_sw_pad.cs`, `sw_close` — sans objet sous GC, l'objet restant
vivant tant que ses chronomètres le tiennent, et iXtal n'a pas de changement de type à chaud. Sans
action.

### PB-96 — La police monochrome lue au-delà de ses 16 lignes

`vid_mda.c:119`, `:122` : `fontdatm[chr][mda->sc]` avec `sc` jusqu'à 31 (`sc &= 31`, `:160`,
`:223`), dans un `fontdatm[2048][16]` (`video.c:921`). La lecture sort de la ligne du caractère et
tombe dans les lignes du caractère suivant — jamais hors du tableau (`chr` ≤ 255).
*Effet* : avec R9 > 15 (des cellules de plus de 16 lignes), le bas d'un caractère montre le haut
du suivant.
*Trouvé par* : reconnaissance de G9 (défaut n° 2).
*Source* : IBM, *Options and Adapters TR*, vol. 2 (1984), MDA : quatre lignes d'adresse de rangée, « RA (4) », vont au
générateur de caractères (p. 3 ; schéma feuille 2, RA0-RA3 du MC6845), un générateur de 8 Ko (p. 1) — documenté ;
rangée = `sc & 15`, les rangées 16-31 répétant 0-15 : déduit ; l'Hercules, dont le schéma n'a pas été lu : par analogie.
*Cas qui discrimine* : MDA, R9 = 1Fh (32 lignes par rangée), caractère 41h : à la ligne 16 de la cellule, PCem dessine
la ligne 0 du caractère 42h (`fontdatm_plat(41h, 16)`), la carte la ligne 0 du 41h (`sc & 15`).
*G13* : (a), (b) — (a) la MDA, par son schéma ; (b) l'Hercules, dont le schéma n'a pas été lu.
*Reproduit* : `Video/vid_mda.cs`, `fontdatm_plat` (`:60`) — l'accès se fait à plat (`chr * 16 + sc`),
l'adresse que le C calcule ; un accès `[chr, sc]` au tableau C# `[2048, 16]` lèverait.
*G9.1* : l'Hercules aussi (`vid_hercules.c:173`, `:176`) ; même accès à plat (`Video/vid_hercules.cs:232`).

### PB-111 — Le moteur d'images de CD lit de l'indéterminé

Des variables automatiques lues sans avoir été écrites, et un tampon du tas copié sans l'avoir
été : `pvd[]` de `CanReadPVD` après une lecture courte (`cdrom_image.cpp:233-242`) ; le tampon
`new[]` de `ReadSectors`, copié entier après un échec (`:136`, `:145`) ; `index` d'une ligne INDEX
sans numéro (`:330-331`) ; `min`, `sec` et `fr` quand `sscanf` s'arrête (`:519-521`) ; `attr`
d'`image_is_track_audio` et d'`image_playaudio` quand `GetTrack` rend -1 (`cdrom-image.cc:69-74`,
`:81-84`) ; les champs du sous-canal hors des pistes (`:207-249`).
*Effet* : comportement indéfini en C, et qui change l'issue. Mesuré par `cdimage-check` sur
l'oracle compilé SANS `-ftrivial-auto-var-init=zero` : dans `ok-ligne511.cue`, `attr`
d'`image_playaudio` hérite de la pile un 14h laissé par un appel précédent — PLAY AUDIO hors de
toute piste y est refusé comme une piste de données (« Can't play data track »), là où le même
appel joue ailleurs ; 31 écarts. Sans le `new[]` enveloppé, sous `MALLOC_PERTURB_=85`, les secteurs
non lus valent AAh ; 158 écarts. Chez PCem, l'issue dépend de ce qu'un appel précédent a laissé.
*Trouvé par* : reconnaissance de G10.3.
*Source* : sans objet matériel — des valeurs indéterminées de C (ISO/IEC 9899:2011 § 6.7.9 ¶ 10 pour les
automatiques ; un `new[]` non initialisé). Les cas qu'elles touchent ont leur entrée : la lecture qui échoue
(PB-109), la piste cherchée sur le MSF compacté (PB-214).
*Cas qui discrimine* : aucun en mode matériel : zéro des deux côtés. Le défaut se montre sur l'oracle compilé sans
`-ftrivial-auto-var-init=zero` (`cdimage-check`, `ok-ligne511.cue` : 31 écarts).
*G13* : (d) — un comportement indéfini, neutralisé des deux côtés : rien à corriger.
*NON reproduit — rendu déterministe, des deux côtés* (décision n° 2 de G10.3) : zéro partout. Le C# initialise ;
l'oracle compile `harness_cdrom.cpp` avec `-ftrivial-auto-var-init=zero` et prend tout `new[]` d'un `calloc`
(`--wrap=_Znam`, que seul cet objet référence, `nm -u`). Conséquence inscrite : PLAY AUDIO en MSF teste la piste sur
la position encore compactée (`cdrom-image.cc:83`) ; hors de toute piste, `attr` vaut 0 et la lecture part. Elle ne
part sur une piste de données que pour le moteur appelé seul (`cdimage-check`) : l'invité ne la voit pas, `scsi_cd`
refusant d'abord une piste de données en 05/64h par `is_track_audio`, qui convertit le MSF (`scsi_cd.c:1375-1378` ;
D3-contre, K4). Marqueurs `not reproduced: PB-111` devant les sept DEVIATION de `Cdrom/cdrom_image.cs` (ReadSectors,
CanReadPVD, l'INDEX, GetCueFrame) et de `Cdrom/cdrom-image.cs` (is_track_audio, playaudio, getcurrentsubchannel),
posés en G13.1.

### PB-124 — Le fil CD lit l'image sans verrou, en même temps que le fil d'émulation

`sound.c:143-197` : le fil CD, réveillé par `sound_poll` (`:221-225`), appelle
`image_audio_callback`, qui lit l'image par l'`ifstream` du moteur (`cdrom-image.cc:36`) et avance
`image_cd_pos` — pendant que le fil d'émulation lit le même `ifstream` pour les commandes ATAPI
(READ, READ TOC, READ SUB-CHANNEL) et réécrit `image_cd_pos` et `image_cd_state` (PLAY, PAUSE,
STOP). Aucun verrou. Il lit aussi les pages de mode de `scsi_cd.c` (`atapi_get_cd_volume`,
`atapi_get_cd_channel`) et les volumes de la carte son pendant qu'ils changent.
*Effet* : une course au sens du C11, donc un comportement indéfini ; en pratique, une lecture de
données pendant la lecture audio peut déplacer la position de l'autre, et la cadence du fil
dépend de l'ordonnanceur de l'hôte : deux exécutions ne rendent pas le même son.
*Trouvé par* : reconnaissance de G10.5.
*G13* : hors du mode — non reproduit dans les deux modes ; rien n'y bascule.
*NON reproduit — divergence assumée* (décision n° 7 de PLAN-G10, `DEVIATION` dans `Sound/sound.cs`
et `tools/oracle/harness.c`) : le corps du fil s'exécute sur-le-champ, à l'échéance de
`sound_poll`, dans le fil d'émulation, des deux côtés.

### PB-161 — La réverbération de l'EMU8000 déborde ses tampons

`sound_emu8k.c:1162-1174` : le quartet haut d'init2 canal 14h fixe `multip = quartet + 18`, et les tailles
`multip × 242` pour la réflexion 5, `(multip + 1) × 242` pour les queues (le canal 16h pour la queue droite quand
la réverbération est liée). L'en-tête prévoit au plus 32 × 242 = 7 744 (`MAX_REFL_SIZE`, `sound_emu8k.h:109-110`) ;
Eh donne 33 × 242 aux queues, Fh 33 × 242 à la réflexion et 34 × 242 aux queues : le « + 1 » des queues est oublié.
*Effet* : en silence, le peigne s'effondre sans tomber ; avec du signal, `emu8k_reverb_comb_work` et
`emu8k_reverb_tail_work` écrivent au-delà de leur tableau, dans les peignes voisins, puis dans `sb_t` et le tas.
*Trouvé par* : reconnaissance de G12 (lectures de l'EMU8000, contre-lecture D10).
*G13* : hors du mode — déjà corrigé dans les deux modes (R9) ; rien n'y bascule.
*NON reproduit* (R9) : des tampons de 34 × 242 = 8 228 entrées, qui tiennent la taille demandée, et une garde aux
quatre affectations (`Sound/sound_emu8k.cs`, gardes `sound_emu8k.c:1164`, `:1165`, `:1167`, `:1173` ; décision n° 15
de PLAN-G12.md). La sonde ne hache que les 7 744 premières entrées. Aucune porte comparée n'y va : AWEBANC s'arrête au
quartet Dh, la dernière taille qui tienne. Survie : `r9-emu8k`, chaque garde atteinte, les entrées au-delà de
7 744 écrites.

### PB-162 — Le chorus droit lit sous son tampon, et prend la fraction du gauche

`sound_emu8k.c:1453-1468` : la position de lecture du canal droit retranche le délai central, le décalage droit
(`hwcf4 & 1FFFFFh` en 1/256 d'échantillon, `:1102-1103`) et le LFO ; un seul repli de 16 384 (`:1457-1465`). Aux
réglages extrêmes (le délai 1FFFh, la profondeur FFh, hwcf4 1FFFFFh), l'indice reste négatif jusqu'à −8 158 :
`chorus_right_buffer[-n]` lit dans `chorus_left_buffer`, qui le précède dans la structure. Et le canal droit
interpole avec la fraction du canal GAUCHE (`:1435`, réemployée à `:1468`).
*Effet* : du signal gauche fuit à droite aux réglages extrêmes ; l'interpolation droite est décalée.
*Trouvé par* : reconnaissance de G12 (contre-lecture de l'EMU8000, D9).
*Source* : le chorus de PCem est un « workalike » (`:1422`) ; le guide de l'EMU8000 ne publie pas le microcode des
effets : l'algorithme réel est inconnu. Deux choses sont sûres (déduit) : une ligne à retard ne lit pas hors
d'elle-même, et chaque voie interpole à sa propre position.
*Cas qui discrimine* : `emu8k_work_chorus` en C# seul, délai central 1FFFh, profondeur FFh, hwcf4 1FFFFFh, une rampe
linéaire en entrée : PCem lit à droite des indices jusqu'à −8 158 et interpole avec la fraction gauche ; attendu : des
indices dans [0, 4000h) et la sortie droite égale à la rampe interpolée au retard droit.
*G13* : (b) — l'effet lui-même est inconnu ; seule sa cohérence se vérifie.
*Reproduit* : `Sound/sound_emu8k.cs`, marqueur PB-162 : les deux tampons en un seul, gauche puis droite — la
contiguïté du C est mesurée (décalages 48 et 65 584) — et la fraction du gauche. AWEBANC pose le chorus extrême ;
`emu8k-kernel-check` le passe sur des états fabriqués.

### PB-163 — Les conversions de la réverbération débordent l'int32

`sound_emu8k.c:1489`, `:1491`, `:1498`, `:1505`, `:1506`, `:1533` : les peignes, les diffuseurs et l'amortisseur
calculent en float (`int × float`) et rendent le résultat à un `int32_t`. Hors bornes, la conversion est un
comportement indéfini ; GCC sur x86-64 émet `cvttss2si`, qui rend INT_MIN, même pour un dépassement positif.
`:1505` calcule de plus `-in`, qui déborde pour INT_MIN.
*Effet* : une réverbération saturée claque à pleine amplitude négative au lieu de saturer.
*Trouvé par* : reconnaissance de G12 (lectures de l'EMU8000).
*Source* : la réverbération de PCem est un « workalike » (`:1537`), l'algorithme réel inconnu ; un processeur de signal
à virgule fixe sature, il ne s'enroule pas (déduit).
*Cas qui discrimine* : `emu8k_reverb_comb_work` sur un état fabriqué, `filterstore` = 2^30, `damp1` = 2,0, `damp2` = 0,
l'écho et l'entrée nuls : `filterstore` devient INT_MIN chez PCem (2^31 hors bornes), 2^31 − 1 en conversion saturante.
*G13* : (b) — l'effet est inconnu ; seule la saturation se vérifie.
*Reproduit* : `Sound/sound_emu8k.cs`, marqueurs PB-163 (le peigne, le diffuseur, l'amortisseur), par l'aide de
conversion du C (`Cpu._386.CvtI32`) ; .NET saturerait. Aucun invité n'y arrive en temps de porte : `emu8k-kernel-check`
compare les sept noyaux sur des états fabriqués, des milliers de sorties à INT_MIN comprises.

### PB-164 — L'attaque de l'enveloppe de modulation lit au-delà de sa table

`sound_emu8k.c:1830-1831` : `value_amp_hz` reçoit l'incrément de l'attaque AVANT d'être borné à 1 << 21 (`:1832-1834`),
et `env_mod_hertz_to_octave[value_amp_hz >> 5]` est lu entre les deux : l'indice dépasse la table de 65 537 entrées
d'au plus `attack_amount >> 5`.
*Effet* : une lecture hors table, aussitôt écrasée par `value_db_oct = 1 << 21` (`:1834`) ; rien d'observable.
*Trouvé par* : reconnaissance de G12 (lectures de l'EMU8000).
*Source* : sans objet : la valeur lue hors table est aussitôt écrasée.
*Cas qui discrimine* : aucun.
*G13* : (d) — sans effet ; le C# borne déjà l'indice, au résultat identique.
*Reproduit* : `Sound/sound_emu8k.cs`, marqueur PB-164 — DEVIATION de forme : l'indice borné à 10000h, le résultat
identique (décision n° 15 de PLAN-G12.md). Ce n'est pas un site R9.

### PB-168 — Une BPB à 0 secteur par piste fait diviser par zéro

`disc_img.c:219` : `img[drive].tracks = bpb_total / (bpb_sides * bpb_sectors)`, dans la branche où la BPB est jugée
valide — `:162` ne teste que les faces et la taille des secteurs. Une BPB qui annonce 0 secteur par piste divise par
zéro.
*Effet* : PCem meurt de SIGFPE à l'insertion de l'image, au démarrage comme au menu ; le C# levait
`DivideByZeroException`.
*Trouvé par* : contre-lecture du stockage, reconnaissance de G13.
*G13* : hors du mode — corrigé dans les deux modes en G13.0 (R9, décision n° 2 de PLAN-G13.md).
*NON reproduit* (R9, G13.0) : une telle BPB est traitée comme une BPB fantaisiste, la géométrie déduite de la taille
du fichier (`Disc/disc_img.cs`, marqueur PB-168, garde `disc_img.c:219`). `r9-disquette` le prouve.

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
cycles, uniforme, invisible sans comparaison à du silicium. Elle ne touche pas la fréquence de
l'INT 8 : les −5,87 ppm que PB-03 lui imputait viennent de `cpu_get_speed() / 100` (`pc.c:473`)
(D1-contre C16, D6-contre C2).
*Source* : le coût en cycles d'une lecture mot du 8088 est mesuré par les traces de SST 8088 v2 ;
le modèle de BIU de PCem est approché, et ces traces ne sont pas comparées (`VERIFICATION.md` § M5.0).
*Cas qui discrimine* : aucun isolément — quelques cycles sur une lecture mot auto-référentielle, que
seule une comparaison de cycles au silicium verrait.
*G13* : (d) — du temps seul : la correction rendrait le modèle cohérent avec lui-même, non avec le
silicium ; elle relève d'un chantier du temps du 8088 contre les traces de SST v2 (décision n° 1).
*Reproduit* : `Cpu/808x.cs:111`, readmemw, marqueur `PB-11`.

### PB-12 — `REP MOVSB` oublie `memcycs = 0` en tête de boucle

`808x.c:970`. Les branches `0xA5` (REP MOVSW, `:996`), `0xA6`, `0xA7`, `0xAA`, `0xAB` remettent
`memcycs` à zéro à chaque itération ; `0xA4` non.

*Effet* : le `FETCHADD` de l'itération consomme un `memcycs` jamais réinitialisé. Dès la troisième
itération, `FETCHADD(17 − memcycs)` reçoit un argument négatif et rend aussitôt : la file ne se
remplit plus pendant un long REP MOVSB. Quelques cycles après l'instruction, aucun effet sur les
données.
*Source* : la file et les cycles d'un REP MOVSB du 8088 sont mesurés par SST 8088 v2 (traces et
file finale), mais ni le modèle de PCem ni la sonde ne les comparent.
*Cas qui discrimine* : aucun isolément — l'état de la file après un REP MOVSB de trois octets ou
plus, que la sonde SST ne compare pas.
*G13* : (d) — du temps seul, comme PB-11, et pour la même raison : à verser au même chantier.
*Reproduit* : `Cpu/808x.cs:797`, rep(), marqueur `PB-12`.

### PB-13 — Branche morte dans l'EOI spécifique du PIC

`pic.c:121` : `if (val == 2 && ...)` dans un bloc gardé par `(val & 0xE0) == 0x60`, donc
`val >= 0x60`. La condition ne peut jamais être vraie. Les sites frères testent `c == 2`.

*Effet* : la remise en attente de la cascade sur EOI spécifique d'IRQ 2 ne se déclenche
jamais. Aucun observable (contre-lecture de G13) : `pic_updatepending()`, appelé juste après, recalcule le bit 2 du
maître avec la même condition ; sur le PC et l'XT, `pic2.pend` reste nul.
*Source* : 8259A p. 15, l'EOI spécifique remet à zéro le bit ISR désigné. Documenté ; la remise en attente de la
cascade vient du niveau de la sortie INT de l'esclave, que `pic_updatepending` rend déjà (déduit).
*Cas qui discrimine* : aucun.
*G13* : (d) — sans effet ; la ligne disparaît dans un 8259A selon la fiche (PB-05).
*Reproduit* : `Models/pic.cs`, l'OCW2 de `pic_write`, marqueur PB-13 (`:166`).

### PB-15 — `fdd_getrpm` : un `switch` après des retours inconditionnels

`fdd.c:143-148` : `if/else` dont toutes les branches retournent, suivi d'un
`switch (fdd[drive].type)` inatteignable — vestige d'une version où le type décidait
seul de la vitesse.

*Effet* : aucun.
*Source* : sans objet — du code mort.
*Cas qui discrimine* : aucun.
*G13* : (d) — sans effet.
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
*Source* : documenté — NEC µPD765 Data Sheet (déc. 1978), WRITE DATA — l'octet est attendu « every 31 µs in the FM
mode, and every 15 µs in the MFM mode » (au débit du 8 pouces ; le double à 250 kbit/s), faute de quoi OR est posé
dans ST1 (bit D4) et la commande se termine.
*Cas qui discrimine* : SPECIFY avec ND = 1, WRITE DATA d'un secteur, l'UC ne fournit pas le deuxième octet à temps :
PCem écrit l'octet périmé et finit en ST1 = 00h ; le contrôleur pose OR, ST1 = 10h. Un écrivain ponctuel ne voit OR
d'aucun côté.
*G13* : (b) — un sens matériel, deux sites (D3-contre, K7) ; aucune machine d'iXtal n'écrit en PIO.
*Reproduit* : `Floppy/fdc.cs`, test conservé tel quel.

### PB-20 — Un champ et sept globales morts dans la couche disquette

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
*Source* : sans objet — des symboles morts (le titre disait « sept » pour les huit lignes du tableau).
*Cas qui discrimine* : aucun.
*G13* : (d) — sans effet.
*Reproduit* : `Floppy/fdc.cs` et `Disc/disc.cs`, définitions conservées, marqueurs PB-20 — le code mort
*commenté* de PCem n'est pas reproduit (R1), mais une variable morte n'est pas un
commentaire.

### PB-26 — Quatre chaînes de `fatal()` fausses par copier-coller dans `mfm_xebec.c`

| Ligne | La chaîne dit | Le code est dans |
|---|---|---|
| `:131` | `Read data STATE_COMPLETION_BYTE` | `case STATE_SEND_DATA` |
| `:133` | `Data write with full data!` | le chemin de **lecture** (`port 0x320` en `in`) |
| `:174` | `Bad write data state - STATE_START_COMMAND` | `case STATE_RECEIVE_COMMAND` |
| `:671` | `CMD_INIT_DRIVE_PARAMS bad state` | `case CMD_DTC_GET_DRIVE_PARAMS` |

*Effet* : aucun sur le comportement — les conditions et les actions sont justes. Mais
`fatal()` tue l'émulateur en imprimant sa chaîne : quand l'un de ces quatre se déclenche,
le message désigne le mauvais endroit.
*Source* : sans objet — des libellés de `fatal()`. Les codes de la carte sont documentés (IBM Fixed Disk Adapter,
p. 6 : 20h « Invalid Command », 21h « Illegal Disk Address ») et serviront au R9 (a) du Xebec.
*Cas qui discrimine* : aucun (un message).
*G13* : (d) — les chaînes tomberont avec le R9 (a) du Xebec, hors G13 (dix-neuf `fatal()` vivants, D3-contre, K19).
*Reproduit* : `Mfm/mfm_xebec.cs`, chaînes conservées telles quelles ; marqueurs PB-26 aux trois sites (`:131` et
`:133` ensemble, `:174` posé en G13.1, `:671`).

### PB-27 — Trois `switch` internes sans `default:` là où six autres appellent `fatal()`

`mfm_xebec.c:314` (`CMD_READ_STATUS`), `:675` (`CMD_DTC_GET_GEOMETRY`) et `:694`
(`CMD_DTC_SET_GEOMETRY`) ouvrent un `switch (xebec->state)` sans branche par défaut. Les
six autres commandes qui en ouvrent un — `:334`, `:389`, `:480`, `:573`, `:594`, `:651` —
terminent toutes par `default: fatal(...)`.

*Effet* : sur un état inattendu, le contrôleur ne fait **rien** : ni octet de fin, ni
chronomètre réarmé, ni IRQ. Il se fige au lieu de s'arrêter bruyamment, ce qui est le
contraire de l'intention affichée par les six autres. Aucun état atteint pendant
l'amorçage, `FDISK` et `FORMAT C: /S` de § M12 n'y mène.
*Source* : déduit — les états sont ceux de la machine de PCem, sans équivalent connu sur le Xebec ; finir la
commande en erreur 20h « Invalid Command » (IBM Fixed Disk Adapter, p. 6), comme R9 le ferait d'un `default:` à
`fatal()`.
*Cas qui discrimine* : état forgé, en C# seul : CMD_READ_STATUS dans l'état RECEIVED_DATA : PCem ne fait rien (ni
octet de fin, ni IRQ) ; attendu : octet de fin 02h, sense 20h.
*G13* : (b), avec le R9 (a) du Xebec — attendu déduit.
*Reproduit* : `Mfm/mfm_xebec.cs`, les trois `switch` restent sans `default:`.

### PB-29 — `ide_fn` déclaré `[4][512]` dans `scsi_ibm.c`, défini `[7][512]` dans `ide.c`

`ide.c:105` définit `char ide_fn[7][512]`. `scsi_ibm.c:21` en re-déclare l'`extern` avec
une borne différente : `extern char ide_fn[4][512];`. Les cinq autres consommateurs — dont
`mfm_xebec.c:26` — écrivent bien `[7][512]`.

*Effet* : aucun à l'exécution, la borne d'un tableau externe n'entrant pas dans l'édition
de liens. Mais un lecteur de `scsi_ibm.c` en déduit quatre disques là où il y en a sept,
et un `-fsanitize=bounds` sur cette unité de traduction signalerait à tort les indices 4
à 6.
*Source* : sans objet.
*Cas qui discrimine* : aucun.
*G13* : (d) — sans effet, rien de transcrit.
*NON reproduit* (sans objet ici) : `scsi_ibm.c` n'est pas transcrit. `Disc/hdd.cs` porte la définition à sept, celle
d'`ide.c` ; marqueur `not reproduced: PB-29`, posé en G13.1.

### PB-30 — Trois symboles morts dans `mfm_xebec.c`

| Symbole | Défini | Occurrences dans l'arbre |
|---|---|---|
| `cfg_spt` | `mfm_xebec.c:41` | 1 — sa déclaration. Ni lu ni écrit |
| `STATE_DUNNO` | `mfm_xebec.c:37` | 1 — sa déclaration dans l'énumération d'états |
| `STAT_DRQ` | `mfm_xebec.c:81` | 1 — son `#define` |

Les deux champs voisins de `cfg_spt` dans la même structure, `cfg_hpc` et `cfg_cyl`, sont
vivants : `CMD_INIT_DRIVE_PARAMS` les remplit. Le nom `STAT_DRQ` vient du jeu de bits du
contrôleur ATA, où il existe ; le Xebec n'a pas ce bit.

*Effet* : aucun.
*Source* : sans objet — des symboles morts.
*Cas qui discrimine* : aucun.
*G13* : (d) — sans effet, déjà omis.
*NON reproduit* (sans objet) : les trois symboles sont omis (`// omitted:`, `Mfm/mfm_xebec.cs`), marqueurs
`not reproduced: PB-30` posés en G13.1. L'entrée disait « conservées » (D3-stockage, § 1 ; D3-contre, C7).

### PB-33 — `savenvr` écrit dans un fichier qu'il n'a pas vérifié avoir ouvert

`nvr.c:780-781`, la queue commune de `savenvr` après son `switch (oldromset)` :

```c
        fwrite(nvrram, 128, 1, f);
        fclose(f);
```

Aucun test de `f`. Or `nvrfopen` rend **explicitement NULL** en écriture dès que le chemin
composé n'est pas ouvrable — `nvr.c:50-52`, avec son propre `pclog("Failed to open file
'%s' for write")`. Un répertoire `nvr/` absent suffit : le `switch` a rendu un `f` nul, et
`fwrite` le déréférence.

Le cas est atteignable sans effort : PCem livre son répertoire `nvr/`, mais rien ne
garantit qu'il survive à une installation manuelle, et le message de `nvrfopen` montre que
l'échec était *attendu* — simplement pas propagé.

*Effet* : plantage à la sortie de l'émulateur, après que la session a tourné normalement.
Le CMOS de la session est perdu, ce qui est la conséquence la moins grave.
*NON reproduit* (hôte, G13.0) : `savenvr` dit que le CMOS n'est pas écrit et rend la main (`Devices/nvr.cs`,
marqueur PB-33). Il passe AVANT `closepc`, le seul endroit qui vide les images : l'exception faisait aussi perdre
les dernières écritures de l'invité. `r9-filet` le prouve. Avant G13.0, **mesuré dans ce dépôt** : le premier
amorçage après l'écriture de `savenvr`, avec un `nvr/` encore absent, a levé exactement là — c'est ce qui a fait
créer le répertoire et son `README.md`.

### PB-34 — Le type de disque 39 de la boîte de configuration ne correspond à aucun BIOS

`wx-config.c:1295-1302`, la table des 46 types que la liste déroulante propose, entrée 39 :

```c
        462, 7,
```

La ROM d'un BIOS AMI 286 (`amic206.bin`, table des paramètres de disque fixe à
`F000:E401`, entrée 39 en `0xE661`) dit **987 × 7**, soit 60 076 800 octets contre les
28 127 232 de PCem — un facteur 2,14 sur le seul nombre de cylindres, les têtes et les 17
secteurs par piste concordant. **Les 45 autres entrées concordent exactement**, secteurs
par piste compris : ce n'est pas un décalage d'indice, c'est une entrée isolée. Le type 38
vaut (987, 3) des deux côtés, et la ROM enchaîne 38 = 987 × 3 puis 39 = 987 × 7 — deux
variantes d'un même modèle de disque, cohérence que le 462 de PCem casse.

Le type 39 est hors de portée du BIOS de l'IBM AT, qui n'a que 23 types : aucun BIOS de ce
dépôt ne le confirme, et celui qui le porte le contredit.

*Effet* : une image créée au type 39 depuis l'interface de PCem a une géométrie qu'aucun
BIOS ne programmera. `CMD_SET_PARAMETERS` transporte les têtes et les secteurs, pas les
cylindres, donc l'incohérence ne se voit qu'au premier accès au-delà du cylindre 462 —
`mfm_get_sector` refuse alors sur « wrong cylinder », après que le formatage a paru
réussir.
*Source* : documenté pour l'AMI 286 — la table des types de sa ROM (`amic206.bin`, F000:E401, entrée 39 en E661h)
dit 987 × 7 ; l'IBM AT n'a que 23 types : le « vrai » type 39 dépend du BIOS.
*Cas qui discrimine* : aucun pour l'invité. Pour l'hôte : la liste des types
(`Host/CommandLine/HardDiskTypeListing.cs`) et la création d'une image de type 39 : 462 × 7 × 17 secteurs chez PCem,
987 × 7 × 17 selon la ROM AMI.
*G13* : (d) — une table de l'hôte, sans pendant chez l'invité ; rien à protéger : laissée (décision n° 9).
*Reproduit* : `Host/HddImage.cs`, la table est recopiée **verbatim**, le 462 compris (marqueur PB-34 devant la ligne
du type 39, posé en G13.1 ; le commentaire n'y renvoyait que par une citation). La corriger serait réécrire la table
d'un oracle ; `PrintHddTypes` cite ce PB à la place.

### PB-36 — Un temps « hors affichage » négatif, converti en entier non signé sans borne

`vid_svga.c:442-451`, la fin de `svga_recalctimings` :

```c
        _dispofftime = disptime - _dispontime;
        ...
        svga->dispofftime = (uint64_t)_dispofftime;
        if (svga->dispofftime < TIMER_USEC)
                svga->dispofftime = TIMER_USEC;
```

`disptime` est `htotal = CR00 + 6`, `_dispontime` est `hdisp = CR01 + 1` : dès que CR01
dépasse CR00 + 5, la différence est négative. Et le cas n'est pas exotique — le BIOS VGA
écrit le CRTC dans l'ordre des index, donc en passant du mode 3 (CR00 = 5Fh, CR01 = 4Fh) à
un mode 40 colonnes il pose CR00 = 2Dh **avant** CR01 : `htotal` 51, `hdisp` 80, et
`svga_recalctimings` tourne entre les deux (`vid_vga.c:46`).

La conversion d'un `double` négatif en `uint64_t` est de l'UB en C. GCC la compile en
`cvttsd2si` (objdump de `build/vid_svga.o`) : le négatif devient `(uint64_t)(int64_t)x`,
une valeur énorme, que le `<` **non signé** de `:450` laisse passer. `timer_advance_u64`
fait alors **reculer** le chronomètre de la carte : `svga_poll` se redéclenche aussitôt,
et la période de ligne reste celle de `htotal` — l'arithmétique modulo 2^64 retombe sur
ses pieds. `vid_cga.c:109-116` porte le même motif (`crtc[1] > crtc[0] + 1`), sans borne
du tout.

*Effet* : aucun à l'écran. Mais la conversion est de l'UB, et un autre compilateur — ou
.NET, voir plus bas — en fait autre chose, ce qui change la phase du balayage, donc les
bits de `3DAh`.
*Source* : sans objet pour l'invité. Le data sheet MC6845 (Motorola) exige R0 > R1 ; au-delà, l'affichage ne s'éteint
jamais dans la ligne (déduit), ce que rend déjà l'arithmétique de PCem : la phase « hors affichage » ne dure rien.
*Cas qui discrimine* : aucun — la période de ligne reste celle de `htotal`, et le C# rend la conversion de GCC.
*G13* : (d) — sans effet ; une borne n'y changerait rien de visible, et `TIMER_USEC` allongerait la ligne de la SVGA.
*Reproduit* : `Video/vid_svga.cs` (`:709`) et `Video/vid_cga.cs` (`:198`), marqueur
`// pcem bug, reproduced: PB-36`, par `unchecked((uint64_t)(int64_t)x)`. Le `(uint64_t)x`
direct n'était PAS fidèle : .NET 9 et au-delà **saturent** à 0 (mesuré sur .NET 10 :
`(ulong)-5.5 = 0`, `(ulong)(long)-5.5 = 0xFFFFFFFFFFFFFFFB`, GCC -O2 rend le second). Trouvé
par la relecture contradictoire de § M15, puis rendu observable : un programme DEBUG qui
pose CR00 = 2Dh sous CR01 = 4Fh et lit `3DAh` 8 192 fois fait rougir le diff d'amorçage à
l'instruction 36 899 042 avec l'ancienne conversion, et le laisse vert — 37 969 642
instructions — avec la nouvelle.

*G9.0* : la MDA fait de même (`vid_mda.c:74-83`) ; reproduit, `Video/vid_mda.cs` (`:159`). L'Hercules
(`vid_hercules.c:110-119`, G9.1) et l'EGA (`vid_ega.c:236-249`, G9.2) aussi ; reproduits (`Video/vid_hercules.cs:161`,
`Video/vid_ega.cs:374`).
*G13.1* : la M24 aussi (`vid_olivetti_m24.c:104-121`), marquée depuis G1.1 (`Video/vid_olivetti_m24.cs:156`) ; le
PC1512 non, ses temps sont des constantes (`vid_pc1512.c:143-155`).

### PB-38 — `svga_render_16bpp_lowres` avance `ma` deux fois

`vid_svga_render.c:620-642` :

```c
                        svga->ma += x << 1;
                } else {
                        ...
                }
                svga->ma += x << 1;
                svga->ma &= svga->vram_display_mask;
```

La branche sans remappage avance déjà `ma` de `x << 1` (`:630`) ; la ligne qui suit la branche
l'avance une seconde fois (`:642`) — et, avec remappage, ajoute `x << 1` à un `ma` déjà avancé de 4
par groupe. Les trois autres rendus 15/16 bpp n'ont que la première. `ma` sert d'adresse de
départ de la ligne suivante quand le rendu n'est pas rappelé sur une ligne répétée ; le
compteur de ligne du CRTC (`svga_poll`, `vid_svga.c:561-579`) le recharge depuis `maback` à chaque ligne
affichée, ce qui borne l'effet.

*Effet* (déduit à la lecture, non mesuré) : invisible tant que `svga_poll` recharge `ma`
depuis `maback` avant chaque ligne ; un `ma` doublé ne servirait qu'au test `changedvram`
d'un appel suivant sur la même ligne.
*Atteint* : **non** par la campagne de § M19 — `16bpp_lowres` y est le seul des six rendus
neufs à zéro passage. Il faudrait `bpp = 16` (TKD8001 de la 8900D) avec le bit 6 d'AR10.
*Source* : sans objet pour l'invité : `ma` est rechargé depuis `maback` à chaque ligne affichée (`vid_svga.c:561-579`).
*Cas qui discrimine* : aucun — `ma` ne diffère qu'échantillonné entre le rendu et son rechargement.
*G13* : (d) — sans effet, même atteint ; à revoir si GR.1 montre que l'ET4000 HiColor passe par ce rendu.
*Reproduit* : `Video/vid_svga_render.cs`, marqueur `// pcem bug, reproduced: PB-38` (`:757`) — reproduction
PAS ENCORE confrontée à l'oracle.

### PB-42 — Le bit « occupé » de la nouvelle TSS est cherché dans la table de l'ancienne

`x86seg.c:2429-2440` et `:2649-2660`, `taskswitch286` :

```c
if (tr.seg & 4)
        tempw = readmemw(ldt.base, (seg & ~7) + 4);
else
        tempw = readmemw(gdt.base, (seg & ~7) + 4);
```

Le descripteur modifié est celui de `seg`, la NOUVELLE TSS, mais la table (LDT ou GDT) est
choisie d'après `tr.seg & 4`, le sélecteur de l'ANCIENNE. Les blocs qui libèrent l'ancienne
(`:2471-2482`, `:2688-2699`) testent, eux, le bon sélecteur.

*Effet* : aucun en pratique — une TSS vit toujours dans la GDT, et TR ne peut désigner que la
GDT, donc `tr.seg & 4` vaut 0 et la GDT est choisie, qui est la bonne. Mais un programme invalide
l'atteint : LTR ne contrôle rien et garde le bit TI dans `tr.seg` (PB-190) ; avec TI = 1, le
bit occupé est cherché dans la LDT.
*Trouvé par* : relecture pendant la transcription de taskswitch286, G2 D5.
*Source* : documenté — « TSS descriptors may reside only in the GDT » (386 PRM § 7.2) ; LTR lève
#GP(sélecteur) si l'objet désigné n'est pas une TSS disponible (386 PRM, page LTR).
*Cas qui discrimine* : aucun pour un programme valide ; par un LTR à TI = 1 puis un changement de
tâche, PCem lit et écrit le bit occupé dans la LDT, là où le 386 a déjà levé #GP au LTR.
*G13* : (d) — nettoyage : le correctif utile est celui de LTR (PB-190), qui rend
`tr.seg & 4` toujours nul.
*Reproduit* : `Cpu/x86seg.cs:1903` (TSS 32 bits) et `:2125` (TSS 16 bits), taskswitch286, marqueurs
`PB-42`.

### PB-53 — FBSTP écrit la globale `tempc` des drapeaux

`x87_ops_loadstore.h:152-164` (et `:182-194` en a32) : `uint8_t tempc` est déclaré DANS la boucle ; après elle,
`tempc = (uint8_t)floor(fmod(tempd, 10.0));` et `tempc |= 0x80;` ne peuvent donc pas viser
cette locale — ils écrivent la globale `int tempc` de `x86_flags.h:3`, celle qu'ADC et SBB
lisent (`x86_flags.h:565-596`). Le C compile parce qu'une globale du même nom est en portée.
Le C# porte deux globales `tempc` (`Cpu/x86_flags.cs`, `Cpu/808x.cs`) là où le C n'en a qu'une (`808x.c:37`,
l'`extern` de `x86_flags.h:3`) : le FBSTP du 8087 écrit celle des drapeaux du 386, que le 808x ne lit pas
(D2-contre A4-l).
*Effet* : aucun observable — ADC et SBB reposent `tempc` avant de le lire. L'octet de signe
écrit en mémoire est juste, lu depuis cette globale.
*Trouvé par* : transcription de G4.2 (la variable de l'écriture finale n'existait pas).
*Source* : sans objet — un défaut du C, pas du matériel : chaque lecteur de `tempc` la repose avant de la lire
(D2-contre C6) — documenté (code).
*Cas qui discrimine* : aucun.
*G13* : (d) — hors du mode : le FBSTP du mode matériel (PB-195) n'emploiera pas la globale.
*Reproduit* : `Cpu/x87_ops_loadstore.cs`, `FBSTP_a16/_a32`, `x86_flags.tempc`, marqueurs
`// pcem bug, reproduced: PB-53`, et leurs copies de `Cpu/x87_ops_808x.cs`.

### PB-98 — L'Hercules ne retire pas sa projection mémoire à la fermeture

`vid_hercules.c:341-346`, `hercules_close` : `free(hercules)` sans `mem_mapping_remove` (la MDA,
`vid_mda.c:282`, la retire). La projection reste dans la liste de `mem.c`, pointant sur une
mémoire libérée — jusqu'au `mem_alloc` de l'amorçage suivant, qui vide la liste
(`mem.c:1373`) ; rien ne la parcourt entre-temps.
*Effet* : aucun observable.
*Trouvé par* : la lecture de G9.1.
*Source* : sans objet : un état de l'hôte, sans pendant matériel.
*Cas qui discrimine* : aucun — rien ne parcourt la liste avant le `mem_alloc` de l'amorçage suivant.
*G13* : (d) — sans effet, sans comportement matériel à viser ; pour G15, ou jamais.
*Reproduit* : `Video/vid_hercules.cs`, marqueur PB-98 (`:434` ; la projection reste, l'objet vivant sous GC).
*G10.2* : le XTIDE aussi. `xtide_close` (`xtide.c:106-110`) fait `free(xtide)` sans `rom_deinit` :
la projection de sa ROM, en C8000, reste dans la liste jusqu'au `mem_alloc` suivant (le Xebec, lui,
la retire, `mfm_xebec.c:766-774`). Reproduit dans `Ide/xtide.cs`, marqueur PB-98.
*G13.1* : l'EGA aussi. `ega_close` (`vid_ega.c:1068-1073`) libère la carte sans retirer ni sa projection ni celle de sa
ROM. Reproduit, `Video/vid_ega.cs`, marqueur PB-98 (`:1226`).

### PB-112 — Des fichiers d'image laissés ouverts

`cdrom_image.cpp:214` : `LoadIsoFile` refuse une image sans libérer son `BinaryFile` ; une feuille
refusée après des FILE laisse ceux de ses pistes, que `LoadIsoFile` oublie par `tracks.clear()`
(`:187`) ; `image_open` remplace `cdrom` sans le libérer (`cdrom-image.cc:467`).
*Effet* : aucun observable — des descripteurs ouverts jusqu'à la fin du processus.
*Trouvé par* : transcription de G10.3.
*Source* : sans objet — des descripteurs de fichier.
*Cas qui discrimine* : aucun.
*G13* : (d) — sans effet observable.
*Reproduit* : les objets sont lâchés et le GC les finalise (marqueurs PB-112, `Cdrom/cdrom_image.cs` et
`Cdrom/cdrom-image.cs`, posés en G13.1) ; `FileShare.ReadWrite | Delete`, sans verrou, comme ifstream : un fichier
encore ouvert n'empêche pas l'effacement, Windows compris.

### PB-127 — Huit fatal() que rien n'atteint dans le lecteur ZIP

`scsi_zip.c:659`, `:663`, `:713`, `:717` testent un retour -1 ou 0x100 de `scsi_add_data`, qui rend
toujours 0 ; `:771`, `:776`, `:832`, `:837` les mêmes de `scsi_get_data`, qui rend un octet.
*Effet* : aucun.
*Trouvé par* : transcription de G10.6.
*Source* : sans objet — du code inatteignable.
*Cas qui discrimine* : aucun.
*G13* : (d) — inatteignable.
*Reproduit* : les huit tests sont transcrits avec leur `fatal()`, `Scsi/scsi_zip.cs`, un marqueur PB-127 par paire,
posés en G13.1 ; l'en-tête du fichier le nomme désormais (D3-contre, A14).

### PB-134 — Vingt-deux fatal() que rien n'atteint dans le SCSI

`scsi_hd.c:476`, `:480`, `:529`, `:533`, `:580`, `:585`, `:633`, `:638` testent un retour -1 ou 0x100 de
`scsi_add_data` ou de `scsi_get_data`, qui rendent 0 ou un octet. `scsi_aha1540.c:1741`, `:1773`, `:1842`,
`:1920`, `:1994`, `:2025` (BSY tombé : il ne tombe qu'après l'ACK du message, que la carte ne fait qu'en
READ_MESSAGE), `:1807`, `:1828`, `:2047` (une phase, un REQ ou un message que `scsi_hd` ne rend pas), `:1016`,
`:1337`, `:1367`, `:1691`, `:2055` (des invariants d'état ; `:1337` n'est atteint, dans l'oracle, qu'après
`:679`, PB-135).
*Effet* : aucun.
*Trouvé par* : reconnaissance de G11.
*Source* : sans objet — du code inatteignable.
*Cas qui discrimine* : aucun.
*G13* : (d) — inatteignable.
*Reproduit* : les sites sont transcrits avec leur `fatal()` ; marqueurs PB-134 posés en G13.1 : quatre dans
`Scsi/scsi_hd.cs` (un par paire de tests), quatorze dans `Scsi/scsi_aha1540.cs`.

### PB-167 — `emu8k_close` ne libère pas le bloc vide

`sound_emu8k.c:2031` alloue le bloc vide (128 Kio) ; `emu8k_close` (`:2234-2237`) libère la ROM et la RAM, pas lui.
*Effet* : une fuite de 128 Kio à chaque fermeture de l'AWE32 ; rien pour l'invité.
*Trouvé par* : reconnaissance de G12 (contre-lecture de l'EMU8000, D18).
*Source* : sans objet : une fuite de l'hôte, sans pendant sur la carte.
*Cas qui discrimine* : aucun.
*G13* : (d) — rien pour l'invité, et sans objet en C#, où aucune mémoire ne se libère à la main.
*Reproduit* : sans objet — `emu8k_t.mem` porte la ROM, le bloc vide et la RAM d'un seul tableau, que le
ramasse-miettes rend avec la carte.

### PB-219 — INQUIRY : une branche « LUN absent » morte, et non conforme

`scsi_hd.c:193-194` : INQUIRY rend `0 | (3 << 5)` (60h) si `cdb[1] & 0xe0`. Mais le test général du LUN (`:131`, où
l'exception d'INQUIRY est commentée) a déjà refusé toute commande d'un LUN non nul, en 05/25h : la branche n'est
jamais prise.
*Effet* : aucun. Elle serait d'ailleurs fausse : un qualificatif 011b exige le type 1Fh, soit 7Fh, et non 60h.
*Trouvé par* : reconnaissance de G13 (D3-contre, K8 et A9).
*Source* : documenté — SCSI-2 § 8.2.5.1 : le qualificatif 011b (aucun périphérique possible sur ce LUN) va avec le
type 1Fh.
*Cas qui discrimine* : aucun, la branche est inatteignable ; un INQUIRY à un LUN non nul rend 05/25h par le test
général (PB-133).
*G13* : (d) — inatteignable, sans effet ; la réponse juste à un LUN absent relève de PB-133.
*Reproduit* : `Scsi/scsi_hd.cs`, INQUIRY, marqueur PB-219.

## Portée de ce registre

Ces **deux cent cinquante-sept** défauts sont ce que les oracles ont éclairé, **pas le résultat d'un
audit systématique de PCem** :

| Trouvé par | Entrées |
|---|---|
| SingleStepTests | PB-01 |
| Fuzzer différentiel | PB-07 |
| Mesure ciblée (fréquence absolue, imputation par opcode) | PB-03 |
| Exécution : l'émulateur s'arrête, ou la machine fait une chose fausse à l'écran | PB-21, PB-31, PB-33 |
| Désassemblage d'une ROM de BIOS, croisé avec une table de PCem | PB-34 |
| Relecture ligne à ligne pendant la transcription | les autres : PB-02, PB-04 à PB-06, PB-08 à PB-20, PB-22 à PB-30, PB-32, PB-35, PB-37, PB-38, PB-41 à PB-44 |
| Relecture contradictoire par agents, puis démonstration au diff | PB-36 |
| pm-check --core 386 : une attente écrite à la main que l'oracle ne tenait pas (G2 D5) | PB-39, PB-40 |
| Audit du code du 26/09, puis mesure (SST 8088, exécution) | PB-45 à PB-47 |
| Mesure de parité du x87 contre le vrai handler, puis lecture du C (G4.0) | PB-48 |
| Fuzzer différentiel en mode flux, balayage par opcode, désassemblage de l'oracle | PB-49 |
| Instruction de PB-49 : trace pas à pas d'un flux de préfixes, build Debug | PB-50, PB-51 |
| Transcription et lecture ligne à ligne de G4.2 (chargements et stockages x87) | PB-52 à PB-56 |
| Transcription de G4.3 (PB-57 à PB-59) ; fuzzeur G4.3 puis désassemblage de l'oracle (PB-60) | PB-57 à PB-60 |
| Transcription de G4.4 (x87_ops_misc.h), vérifiée par x87-cases | PB-61 à PB-67 |
| Transcription de G4.5 (les transcendantes) | PB-68 |
| Transcription de G4.6 (le 8087), lecture de `picint` | PB-69 |
| Témoin MSD de G4.7, puis lecture du C | PB-70 |
| Reconnaissance et transcription de G5 (ide.c) | PB-71 à PB-74 |
| Inventaire de la règle R9 (l'invité ne tue pas l'hôte) | PB-75, PB-76 |
| Reconnaissance et fuzzeur du cœur 486 (G6) | PB-77 à PB-79 |
| Transcription de la fenêtre linéaire SVGA (G7.0) | PB-80 |
| Transcription de la GD5429 (G7.1) | PB-81, PB-82 |
| Transcription de la Trio64 (G7.3) | PB-83 à PB-86 |
| Le fuzzeur 8086 (G1.0) | PB-87 |
| Reconnaissance et transcription de l'Olivetti M24 (G1.1) | PB-88, PB-89 |
| L'Amstrad PC1512 (G1.2) | PB-89 élargi |
| Reconnaissance et transcription de la Sound Blaster Pro v2 (G8.2) | PB-90 à PB-92 |
| La vérification des sections de device du .cfg (G8.3) | PB-93 |
| Reconnaissance et transcription de la souris PS/2 (PS2.0) | PB-94, PB-95 |
| Reconnaissance et transcription de la MDA (G9.0) | PB-96, PB-97, PB-36 élargi |
| Transcription de l'Hercules (G9.1) | PB-98 ; PB-36, PB-96, PB-97 élargis |
| Transcription de l'EGA (G9.2) | PB-99 ; PB-36 élargi |
| Transcription de la Tseng ET4000AX (G9.3) | PB-100 |
| Transcription de LPT1, de la DSS et des Covox (G10.0) | PB-101 |
| La mesure du correctif « hors plage » de l'hôte (plan qualité, § 1c) | PB-102 |
| Reconnaissance et transcription de la manette (G10.1) | PB-103 à PB-105 ; PB-93 élargi |
| Transcription du XTIDE (G10.2) | PB-98 élargi |
| Reconnaissance et transcription du moteur d'images de CD (G10.3) | PB-106 à PB-112 |
| Reconnaissance et transcription de l'ATAPI (G10.4) | PB-113 à PB-122 ; PB-93, PB-110 élargis |
| Reconnaissance de l'audio CD dans la machine (G10.5) | PB-123, PB-124 |
| Reconnaissance et transcription du lecteur ZIP (G10.6) | PB-125 à PB-127 |
| Reconnaissance (lecture puis contre-lecture) et transcription de l'AHA-1542C et de `scsi_hd` (G11) | PB-128 à PB-144 ; PB-121, PB-125 élargis |
| Reconnaissance (lecture puis contre-lecture) et transcription des SB 1.0, 1.5, 2.0 et Pro v1 (G12.0) | PB-145 à PB-148 ; PB-92, PB-93 élargis |
| Reconnaissance (lecture puis contre-lecture) et transcription de la SB 16 (G12.1) | PB-149 à PB-157 ; PB-145, PB-148 élargis |
| Reconnaissance (lecture puis contre-lecture) et transcription de l'AWE32 et de l'EMU8000 (G12.2) | PB-158 à PB-167 ; PB-93, PB-153, PB-154 élargis |
| Contre-lecture du stockage, reconnaissance de G13 (G13.0) | PB-168 |
| Reconnaissance de G13 (six lectures par domaine et leurs contre-lectures), inscrits en G13.1 | PB-169 à PB-257 : le processeur 169 à 193, le x87 194 à 213, le stockage 214 à 219, la vidéo 220 à 233, le son 234 à 244, la carte mère 245 à 257 ; des entrées existantes élargies, chacune le dit |
| SST en mode matériel (G13.3) | PB-258 |
| Le 8237 en mode matériel (G13.4) : les parts laissées reproduites de PB-157 et de PB-253, inscrites à part | PB-259, PB-260 |
| Contre-lecture du 8042 et de la souris PS/2 en mode matériel (G13.4c) | PB-261 ; PB-94 élargi (les valeurs par défaut au reset) |
| SST en mode matériel, le cœur 286/386/486 (G13.5) | PB-262 ; PB-183 élargi (l'adresse de 16 bits replie) |

Le dépôt transcrit environ **8 600 des 309 000 lignes** de PCem. Tout ce qui n'a pas été
lu n'a pas été examiné : le dynarec, les cartes vidéo autres que la CGA, la MDA, l'Hercules, l'EGA, la VGA, les deux Trident, la GD5429, la Trio64 et l'ET4000AX, les
cartes son autres que l'AdLib et les SB 1.0 à 16, le SCSI (hors `scsi.c` et `scsi_cd.c`, lus en G10.4, `scsi_hd.c` et la 1542C, lus en G11) et les images VHD restent hors de ce registre. Le cœur 386, lu en
G2 (D0 à D7), y est entré — mais les écarts de PCem que le corpus SST 386 recense forme par
forme (`sst386-baseline.tsv`, `VERIFICATION.md` § G2) ne sont PAS instruits ici un par un :
ce registre ne garde que ce qui a été lu à la ligne de C.

Deux frontières ont bougé et le disaient mal :

- **Le son n'est plus hors périmètre.** M9 a transcrit le mixeur et le haut-parleur, et y
  a trouvé PB-21 — le seul défaut de ce registre qu'une **exécution** ait révélé, tous les
  autres venant de la relecture ou d'un oracle.
- **Le disque dur non plus.** M12 a transcrit le Fixed Disk Adapter et la couche image, et
  en a rapporté neuf entrées d'un coup, PB-22 à PB-30. C'est le plus fort rendement au
  millier de lignes du dépôt : 707 lignes de C pour neuf défauts, contre 1 834 pour sept à
  M6. Une carte que peu de logiciels exercent est moins relue qu'un cœur d'UC.

Un défaut de PCem se **reproduit**, avec une exception nommée : `PB-24` est de l'UB dont
la valeur change d'une exécution à l'autre, et un oracle qui tire aux dés n'est pas un
oracle. La divergence est assumée et inscrite au registre des omissions de
`TRANSCRIPTION.md`, comme `h_pad_ram` avant elle.

Aucun de ces défauts n'a été remonté en amont ; `VENDORED.md` donne l'URL du projet.
