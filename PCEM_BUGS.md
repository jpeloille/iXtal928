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

### PB-22 — `CMD_FORMAT_TRACK` ne réinitialise pas `sector`, et formate donc à côté

`mfm_xebec.c:369-387`. Les trois autres commandes d'accès au disque commencent par
`xebec->sector = xebec->command[2] & 0x1f` — `CMD_VERIFY_SECTORS` (`:340`),
`CMD_READ_SECTORS` (`:395`), `CMD_WRITE_SECTORS` (`:486`). `CMD_FORMAT_TRACK`, non.

Or `xebec_get_sector` (`:236-270`) fait entrer `sector` dans le calcul d'adresse au même
titre que le cylindre et la tête. Le formatage démarre donc au secteur **laissé par la
commande précédente** : il déborde sur la piste suivante et laisse intact le début de la
piste visée. Et si le résidu est ≥ 17, le test de borne de `:260` fait échouer la commande
en `ERR_ILLEGAL_SECTOR_ADDRESS`.

*Effet* : latent sous PC DOS 2.00, qui fait précéder chaque `FORMAT TRACK` d'un accès
laissant `sector` à 0 — le `FORMAT C: /S` de § M12 produit une image octet pour octet
identique des deux côtés. Un pilote qui enchaînerait deux formatages sans accès
intermédiaire, lui, formaterait la mauvaise piste.
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
*Reproduit* : `Mfm/mfm_xebec.cs`.

### PB-25 — La borne des têtes est testée avec `>` au lieu de `>=`

`mfm_xebec.c:250` et `:255`. Les têtes sont numérotées **depuis 0**, donc `head == hpc`
est déjà hors du disque ; avec `>`, elle passe le filtre et le calcul d'adresse de `:265`
vise une piste entière au-delà du cylindre demandé, **en silence**.

*Preuve interne* : le test des secteurs, cinq lignes plus bas (`:260`), écrit bien
`>= 17`. Les deux bornes sont dans la même fonction, écrites dans la même minute, l'une
juste et l'autre fausse.
*Effet* : une lecture ou une écriture sur la tête `hpc` atteint des données valides mais
**d'ailleurs**. `mfm_at.c:113` porte exactement le même défaut : ancêtre commun.
*Reproduit* : `Mfm/mfm_xebec.cs`.

### PB-28 — `CMD_DTC_GET_DRIVE_PARAMS` répond une géométrie inventée sur une unité absente

`mfm_xebec.c:651-666`. Quatre commandes touchent à une unité qui peut ne pas exister ;
trois testent `drive->hdd_file.f` et répondent `ERR_NOT_READY` (`:299`, `:305`, `:560`,
`:733`). Celle-ci, non — elle lit directement la géométrie :

```c
xebec->data[2] = drive->hdd_file.hpc - 1;
```

Sur une unité absente, `hpc` vaut 0 et la troncature en `uint8_t` donne **`0xff`** : 256
têtes annoncées.

*Effet* : propre au DTC 5150X, carte qui n'est pas celle du jalon. Le Xebec d'IBM n'a pas
cette commande.
*Reproduit* : `Mfm/mfm_xebec.cs` — le fichier porte les deux cartes, on transcrit les deux.


---

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
`default` — « Bad CALL special descriptor » puis `x86gpf(NULL, seg & ~3)` (CALL), ou
« Bad JMP CS » puis `x86gpf(NULL, 0)` (JMP). `pmodeint` (`:1963`) et `pmodeiret`, eux,
suivent bien une porte de tâche.

*Effet* : un programme qui change de tâche par `CALL FAR` ou `JMP FAR` sur une porte de
tâche reçoit un #GP. Changer de tâche par INT, par IRET avec NT, ou par CALL/JMP directement
sur la TSS fonctionne.
*Trouvé par* : pm-check --core 386, G2 D5 — le cas « CALL FAR porte de tâche » partait en
#GP des deux côtés ; relecture de loadcscall ensuite.
*Reproduit* : `Cpu/x86seg.cs`, loadcscall et loadcsjmp, marqueur `PB-39` ; épinglé par
l'attente du cas « CALL FAR porte de tâche -> #GP (PCem : type 5 non géré) ».

### PB-40 — CALL FAR sur une TSS empile l'adresse de retour sur la pile de la NOUVELLE tâche

`x86_ops_call.h:51-76`, la macro `CALL_FAR_l` (et `CALL_FAR_w`, même forme) :

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
16 bits), et deux mots ont été écrits sous sa pile. Mesuré : ESP 0x6FF8 au lieu de 0x7000.
*Trouvé par* : pm-check --core 386, G2 D5 — l'attente écrite à la main (0x7000) ne tenait
pas ; relecture de la macro.
*Reproduit* : `Cpu/386_ops_call.cs`, CALL_FAR_l / CALL_FAR_w, transcrits tels quels ;
épinglé par l'attente du cas « CALL FAR TSS 386 » (ESP 0x6FF8).

### PB-41 — Une TSS 16 bits pose les moitiés hautes des registres généraux à FFFF

`x86seg.c:2817-2824`, branche 16 bits de `taskswitch286` :

```c
EAX = new_eax | 0xFFFF0000;
ECX = new_ecx | 0xFFFF0000;
...
EDI = new_edi | 0xFFFF0000;
```

Un changement de tâche vers une TSS 286 ne devrait toucher que les seize bits bas des
registres ; PCem force les seize hauts à 1. La branche 32 bits (`:2612-2619`) charge les
registres entiers, sans masque.

*Effet* : sur un 386, entrer dans une tâche 286 met EAX…EDI à `0xFFFFxxxx`. Sur un 286,
invisible — les moitiés hautes n'y existent pas pour le programme — mais le vecteur d'état
les compare.
*Trouvé par* : relecture pendant la transcription de taskswitch286, G2 D5.
*Reproduit* : `Cpu/x86seg.cs`, taskswitch286, commentaire `verbatim` ; épinglé par l'attente
du cas « JMP FAR TSS 286 depuis un 386 » (EAX 0xFFFF1111).

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
*Reproduit* : `Cpu/386_ops_mov_ctrl.cs`, en-tête (« LE CHAMP mod … EST IGNORÉ PAR LE
SILICIUM, PAS PAR PCem »). Le fuzzeur `--0f 20…26` le compare des deux côtés.

### PB-44 — Les formes a32 de MOV DRx,r et MOV TRx,r décodent en 16 bits

`x86_ops_mov_ctrl.h:220` (`opMOV_DRx_r_a32`) et `:269` (`opMOV_TRx_r_a32`) appellent
`fetch_ea_16`, alors que les huit autres formes `_a32` de l'en-tête appellent `fetch_ea_32` ;
le `PREFETCH_RUN` qui suit passe bien `ea32 = 1`.

*Effet* : conjugué à PB-43, un `67 0F 23` ou `67 0F 26` à `mod ≠ 3` décode une adresse
16 bits (pas de SIB, déplacement de 16 bits) au lieu de 32 : `pc` avance d'une autre longueur.
Invisible avec `mod = 3`, la seule forme d'usage.
*Trouvé par* : relecture pendant la transcription, G2 D4.
*Reproduit* : `Cpu/386_ops_mov_ctrl.cs`, commentaires `verbatim` des deux handlers.

### PB-45 — IDIV octet étend AX par des zéros au lieu du signe

`808x.c:3614`, groupe F6 /7 :

```c
case 0x38: /*IDIV AL,b*/
        tempws = (int)AX;
```

AX est un `uint16_t` : la conversion en `int` le complète par des zéros. Un dividende négatif
(AX ≥ 0x8000) est donc divisé comme un grand positif. La forme mot (`:3743`, F7 /7) lit
`(DX << 16) | AX` et signe correctement.

*Effet* : quotient et reste faux pour tout dividende négatif sans débordement. Mesuré sur
SingleStepTests/8088, forme `F6.7` (hors ligne de base, vecteurs en `/tmp`) : 1 169 / 9 696
côté oracle ET côté C#. Sur les 9 372 cas sans préfixe REP, un modèle Python du C reproduit
exactement les 1 169 : **1 143 échecs viennent du signe perdu** (dividende négatif, pas de
débordement) ; environ 7 000 viennent d'un débordement de quotient que PCem ne détecte pas —
sur le silicium, #DE — comme pour DIV (famille déjà recensée en § M5.0 de `VERIFICATION.md`,
sans entrée PB) ; 32 cas à diviseur nul échouent pour une cause non instruite. `F7.7` n'est
pas touché par le signe.
*Trouvé par* : audit du 26/09 (D4), mesuré en G2.
*Reproduit* : `Cpu/808x.cs`, marqueur `// pcem bug, reproduced: PB-45` sur `tempws = (int)AX`.

### PB-48 — Le mode d'arrondi du x87 ne s'applique qu'à FADD avec opérande mémoire

`x87_ops_arith.h:12-16`, dans la seule branche `opFADD##name` du macro `opFPU` :

```c
if ((cpu_state.npxc >> 10) & 3)
        fesetround(rounding_modes[(cpu_state.npxc >> 10) & 3]);
ST(0) += use_var;
if ((cpu_state.npxc >> 10) & 3)
        fesetround(FE_TONEAREST);
```

Les champs RC de `npxc` (bits 10-11) ne sont lus que là. Les autres branches du même macro —
FSUB, FSUBR, FMUL, FDIV, FDIVR (`:48-112`) — calculent sans `fesetround`, donc au plus près,
et de même les formes registre de FADD (`opFADD`, `opFADDr`, `opFADDP`, `:122-152`) et toutes
celles de FSUB/FMUL/FDIV (`:239-397`). Le macro engendre le défaut pour les quatre types
d'opérande (m32, m64, m16int, m32int, `:114-120`), et `8087.h:86` l'inclut aussi : le 8087 en
hérite.

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
*Reproduit* : pas encore — G4.3 transcrira l'arrondi dirigé sur les seuls FADD mémoire, au
plus près partout ailleurs (décision n° 3 de `PLAN-G4.md`).

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
*Reproduit* : le C# ne compte pas non plus. Mais le préfixe de PCem est un appel terminal
que GCC compile en saut (`jmp *%rax`) ; C# ne garantit pas l'appel terminal, et le C# Debug
tombait par StackOverflow sur la recette ci-dessus. Le C# aiguille donc par un trampoline
(`TailCall` / `Dispatch`, `Cpu/386_ops_prefix.cs`, `// DEVIATION:`) : même handler, même
fetchdat, même valeur rendue, sans pile — au bit près l'appel terminal de PCem.

### PB-51 — Le fetch d'instruction ne contrôle pas la limite de CS

La boucle d'exec386 (`386_dynarec.c`, l'interpréteur) et `fastreadl`/`getpccache`
(`386_common.h:100-145`) lisent `cs + cpu_state.pc` sans comparer `pc` à
`cpu_state.seg_cs.limit` : `grep limit` ne rend, dans ces deux fichiers, que des
commentaires. En mode réel, la limite de CS vaut 0xFFFF : sur le silicium, un fetch au-delà
lève #GP (INT 0Dh) — IP ne continue pas dans le segment suivant.

*Effet* : IP franchit 0xFFFF et l'exécution se poursuit linéairement dans la RAM. Mesuré
(G4.0) : après une suite de préfixes commencée en 1000:0xxx, IP vaut 000F0002 — CS:IP
pointe 0x100002, au-delà du Mo. Même famille que les formes E2 de `VERIFICATION.md` § G2,
où PCem ne contrôle pas la limite des adresses effectives en mode réel. En mode protégé, la
lecture du code dit la même chose ; ce n'est pas mesuré.
*Trouvé par* : la trace de PB-50.
*Reproduit* : le C# transcrit la boucle et `fastreadl` sans contrôle, comme PCem ; rien
d'imposé.

### PB-52 — FBLD n'existe pas : DF /4 est FPU_ILLEGAL

`x87_ops.h:884-893` (et `:923-932` en a32) : la rangée `/4` de `fpu_df_a16` porte
`ILLEGAL`, c'est-à-dire `FPU_ILLEGAL_a16` (`:297-302`) — décoder l'adresse effective, compter
`timing_rr`, et rien d'autre. `x87_ops_*.h` ne contient aucun `opFBLD` ; `x87_timings_t`
n'a même pas de champ `fbld` utilisé (il en a un, `x87_timings.h:7`, que personne ne lit).

*Effet* : FBLD (chargement d'un décimal compacté de 10 octets) ne charge rien : la pile x87
ne bouge pas, TOP ne descend pas, et le programme continue avec le registre du dessus
inchangé. Le silicium pousse la valeur décimale. FBSTP, lui, existe (`:141-200`).
*Trouvé par* : transcription de G4.2, en posant les rangées mémoire de DF.
*Reproduit* : `Cpu/x87_ops.cs`, `TableFpu`, rangée `/4` de DF à `FPU_ILLEGAL` ; commentaire
PB-52. Le fuzzeur G4.2 (`--x87 mem`) tire DF /4 et le confronte à l'oracle.

### PB-54 — Seul FSTP m64 contrôle la limite du segment ; FST m64 et les autres stockages non

`x87_ops_loadstore.h:454-485` : `opFSTPd_a16` et `_a32` appellent
`CHECK_WRITE(cpu_state.ea_seg, cpu_state.eaaddr, cpu_state.eaaddr + 7)` — lève #GP si les huit
octets sortent de la limite. Aucun autre stockage x87 ne le fait : ni `opFSTd` (`:429-452`),
le même stockage sans dépilement, ni FST/FSTP m32, FIST/FISTP, FSTP m80, FBSTP.

*Effet* : à l'offset 0xFFF9 d'un segment de 64 Ko, FSTP m64 lève #GP, FST m64 écrit — au-delà
de la limite, sans faute. Sur le silicium, les deux lèvent #GP.
*Trouvé par* : lecture ligne à ligne en G4.2.
*Reproduit* : `Cpu/x87_ops_loadstore.cs`, verbatim — seuls les deux `opFSTPd` portent
`CHECK_WRITE`.

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
*Reproduit* : `Cpu/x87_ops.cs`, `x87_ld80`, verbatim. Le fuzzeur G4.2 le confronte à l'oracle
(DB /5) ; contrôle négatif : sans `& 0x3ff`, divergence à l'itération 3.

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
*Reproduit* : `Cpu/x87_ops.cs`, `x87_st80`, verbatim ; ST est un `double[]` (`x86.cs`).

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
1). Et FCOM registre compte `x87_timings.fadd`, FCOMP et FCOMPP aussi, pas `fcom`.
*Effet* : un programme qui teste C2 après FCOM ST(i) sur un NaN le croit comparable.
*Trouvé par* : transcription de G4.3.
*Reproduit* : `Cpu/x87_ops_arith.cs`, `opFCOM`, marqueur PB-57. `x87-cases` le confronte à
l'oracle (NaN contre 1) ; contrôle négatif : x87_compare sans son test de NaN → divergence
`npxs` à l'itération 67 du fuzzeur G4.3.

### PB-58 — FCOMPP : −0 contre +0 rend « plus petit », un contournement de détection

`x87_ops_arith.h:180-195`, `opFCOMPP` (DE D9) :

```c
if (*(uint64_t *)&ST(0) == ((uint64_t)1 << 63) && *(uint64_t *)&ST(1) == 0)
        cpu_state.npxs |= C0; /*Nasty hack to fix 80387 detection*/
```

ST(0) = −0 et ST(1) = +0 rendent C0, « plus petit » ; le silicium rend C3, « égal ». Le
commentaire dit pourquoi : une routine de détection distingue 287 et 387 par le signe de
l'infini projectif, et PCem, qui n'a pas le mode projectif, force le résultat attendu.
*Effet* : toute comparaison −0 / +0 par FCOMPP, et elle seule, rend « plus petit ».
*Trouvé par* : transcription de G4.3.
*Reproduit* : `Cpu/x87_ops_arith.cs`, `opFCOMPP`, marqueur PB-58. `x87-cases` : FCOMPP (−0,
+0) identique à l'oracle ; sans le contournement, `npxs : oracle 0x0100, C# 0x4000`.

### PB-59 — Seule la division par zéro lève une exception ; démasquée, l'instruction s'évapore

`x87_ops.h:17-30`, la macro x87_div, et `:32`, `x87_checkexceptions() {}` vide. ZE est la
seule exception que PCem modélise : ni IE (∞ − ∞, 0 × ∞, NaN signalants, pile vide ou
pleine), ni DE, ni OE, ni UE, ni PE — aucun de ces bits de npxs n'est jamais posé. Et quand
ZE est démasquée, x87_div fait `picint(1 << 13)` puis `return 1` : le handler sort sans
écrire la destination, sans poser le tag, sans compter ses cycles — et sans poser ES ni B
dans npxs. Sur un XT, `picint(1 << 13)` vise un second PIC qui n'existe pas (le 8087 y passe
par la NMI), G4.6.
*Effet* : un programme qui démasque les exceptions n'en voit qu'une, et pour la division par
zéro l'instruction ne compte aucun cycle. Mesuré (contrôle négatif de G4.3, la branche
démasquée neutralisée) : `D8 F5` (FDIV), `cycles consommés : oracle 8, C# 89` — les 8 sont
ceux du décodage.
*Trouvé par* : transcription de G4.3 ; relevé en reconnaissance (PLAN-G4.md).
*Reproduit* : `Cpu/x87_ops.cs`, `x87_div`. `x87-cases` : 1 / ±0, ZE masquée et démasquée.

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
*Reproduit* : `Cpu/x87_ops.cs`, `X87AddSd` / `X87MulSd`, la règle SSE avec un ordre explicite,
et vingt-deux sites de `Cpu/x87_ops_arith.cs` marqués PB-60. Contrôle négatif : l'ordre de
opFADD inversé → divergence à l'itération 46 159.

### PB-61 — FNSTSW AX rend npxs sans TOP

`x87_ops_misc.h:24-32`, `opFSTSW_AX` (DF E0) : `AX = cpu_state.npxs;`. PCem garde TOP à part
(`cpu_state.TOP`) ; les bits 11-13 de npxs ne sont remis à jour que par FSAVE et FSTENV
(`:168`, `:828`). La forme mémoire, `opFSTSW_a16/_a32` (`:369-388`), compose bien
`(npxs & 0xC7FF) | ((TOP & 7) << 11)` ; la forme AX, non.
*Effet* : FNSTSW AX rend un TOP périmé — celui du dernier FSAVE ou FSTENV, ou 0. Un code qui
lit la profondeur de pile par FNSTSW AX, comme beaucoup de détections de coprocesseur, se
trompe dès qu'un push a eu lieu.
*Trouvé par* : transcription de G4.4.
*Reproduit* : `Cpu/x87_ops_misc.cs`, marqueur PB-61. `x87-cases` : FNSTSW AX et FSTSW m16,
TOP 0, 3, 6, npxs 0x3800.

### PB-62 — Le pointeur d'instruction et d'opérande x87 n'est jamais mémorisé

`x87.c:22-23` définit `x87_pc_off`, `x87_op_off`, `x87_pc_seg`, `x87_op_seg` ; aucune ligne de
PCem ne les écrit (`grep` sur `src/` et `includes/`, hors codegen). FSAVE (`x87_ops_misc.h:166-353`)
et FSTENV (`:826-869`) les écrivent donc toujours à zéro, là où le silicium range l'adresse et
l'opcode du dernier ESC, et celle de son opérande. Et les dispositions sont partielles : en
16 bits réel, les mots +8 et +12 (sélecteur de code, opcode ; sélecteur de données) ne sont
pas écrits — l'ancien contenu reste ; en 32 bits réel, ni +16, ni les bits de poids fort de
+12. FSAVE met aussi npxc à 0x37F même pour un 8087, là où FNINIT met 0x3FF.
*Effet* : un gestionnaire d'exception x87 qui lit l'adresse fautive dans l'image FSAVE lit 0 ;
une image FSAVE réécrite puis relue garde des octets de l'image précédente.
*Trouvé par* : transcription de G4.4.
*Reproduit* : `Cpu/x87_ops_misc.cs`, verbatim ; `x87-cases` fait l'aller-retour FSAVE / FRSTOR
et FSTENV / FLDENV en 16 et 32 bits, réel et PE, et compare les 112 octets.

### PB-63 — FXAM ne connaît que trois classes

`x87_ops_misc.h:465-481`, `opFXAM` : vide (C3 | C0), zéro (C3), et tout le reste « normal »
(C2) — NaN, infinis et dénormaux compris ; C1 vaut `ST(0) < 0.0`, donc 0 pour −0 et pour un
NaN négatif. Le silicium distingue NaN (C0), infini (C2 | C0), dénormal (C3 | C2) et rend le
signe dans C1 pour toutes les classes.
*Effet* : un code qui teste « infini » ou « NaN » par FXAM ne les voit jamais.
*Trouvé par* : transcription de G4.4.
*Reproduit* : `Cpu/x87_ops_misc.cs`, marqueur PB-63. `x87-cases` : FXAM sur onze classes, tag
VALID et EMPTY.

### PB-64 — FTST : un NaN rend « plus grand »

`x87_ops_misc.h:451-463`, `opFTST` : `==` et `<` contre 0.0, comme opFCOM (PB-57). Un NaN rend
C3 = C2 = C0 = 0 au lieu de « non ordonné ».
*Trouvé par* : transcription de G4.4.
*Reproduit* : `Cpu/x87_ops_misc.cs`, marqueur PB-64. `x87-cases` : FTST sur les classes.

### PB-65 — FPREM tronque en un pas ; FPREM1 est FPREM

`x87_ops_misc.h:634-675` : `temp64 = (int64_t)(ST(0) / ST(1)); ST(0) = ST(0) - ST(1) * temp64;`,
pour les deux. Trois écarts : le quotient passe par un double, donc faux au-delà de 2^53 et
« entier indéfini » (0x8000000000000000) au-delà de 2^63 — le reste devient alors absurde ; la
réduction partielle du silicium (au plus 2^63 par pas, C2 = 1 « incomplet ») n'existe pas, C2
n'est jamais posé ; FPREM1 (le reste IEEE, quotient arrondi au plus près) est identique à FPREM
(tronqué), temps mis à part.
*Effet* : les réductions d'arguments (sin, cos maison) sur de grandes valeurs rendent n'importe
quoi ; une boucle `FPREM ; FNSTSW ; SAHF ; JP` s'arrête toujours au premier tour.
*Trouvé par* : transcription de G4.4.
*Reproduit* : `Cpu/x87_ops_misc.cs`, marqueur PB-65, la conversion par CvtI64 (cvttsd2si, G4.0).
`x87-cases` : FPREM, FPREM1, FSCALE sur neuf couples de bornes (grands quotients, diviseur
nul, infinis, NaN).

### PB-66 — FLDLN2 est d'un ulp trop grand

`x87_ops_misc.h:533-541` : `x87_push_u64(0x3fe62e42fefa39f0ull)`. Le double le plus proche de
ln 2 est 0x3FE62E42FEFA39EF (0,6931471805599453, ce que rend aussi `log(2.0)`) ; PCem pousse
0x…39F0 (0,6931471805599454). Mesuré en décimal à 60 chiffres : écarts 2,3·10⁻¹⁷ et 8,8·10⁻¹⁷.
Les quatre autres constantes (FLDL2T, FLDL2E, FLDPI, FLDLG2) sont les doubles les plus
proches. Aucune ne suit RC (le 387 arrondit ses constantes selon RC).
*Trouvé par* : transcription de G4.4, vérifié en décimal.
*Reproduit* : `Cpu/x87_ops_misc.cs`, marqueur PB-66. `x87-cases` : les sept constantes.

### PB-67 — FST et FSTP registre copient le tag TAG_UINT64, pas l'entier qu'il désigne

`x87_ops_misc.h:76-97`, `opFST` / `opFSTP` (DD D0+i, DD D8+i) : `ST(i) = ST(0)` et le tag copié —
TAG_UINT64 compris —, mais pas `MM[TOP].q`, l'entier exact que ce tag annonce (x87.h:30). FLD
registre (`:390-405`) et FXCH (`:407-427`) le recopient, eux.
*Effet* : après `FILD m64 ; FST ST(1)`, un FISTP m64 de ST(1) écrit le `MM[].q` qui traînait dans
ce registre physique, pas la valeur chargée.
*Trouvé par* : transcription de G4.4.
*Reproduit* : `Cpu/x87_ops_misc.cs`, marqueur PB-67. `x87-cases` : `FST ST1 ; FINCSTP ; FISTP m64`.

### PB-68 — Les transcendantes : la libm de l'hôte, sans bornes ni C2

`x87_ops_misc.h:554-753` : F2XM1, FYL2X, FYL2XP1, FPTAN, FPATAN, FSIN, FCOS, FSINCOS appellent
`pow`, `log`, `tan`, `atan2`, `sin`, `cos` de la libm de l'hôte, en double. Trois écarts avec le
silicium : (1) FPTAN, FSIN, FCOS, FSINCOS effacent toujours C2 — la borne |x| < 2^63, au-delà
de laquelle le 387 laisse l'opérande et pose C2 (« réduction incomplète »), n'existe pas : la
libm réduit tout argument ; (2) aucun domaine n'est vérifié — F2XM1 hors de [−1, 1], FYL2X d'un
négatif (NaN par `log`), FPTAN, FSIN, FCOS sur 8087 et 287, qui n'ont ni FSIN, ni FCOS, ni
FSINCOS (le 8087 limite aussi FPTAN à [0, π/4]) — tout est calculé ; (3) la précision est
celle de la libm en double, pas celle du microcode sur 64 bits. Les résultats dépendent donc de
la glibc de l'hôte de PCem (parité mesurée en G4.0 : Math.* de .NET rend les mêmes bits sur
cet hôte).
*Effet* : une boucle de réduction d'argument pilotée par C2 ne boucle jamais ; un programme qui
teste FSIN pour distinguer 287 et 387 le trouve partout.
*Trouvé par* : transcription de G4.5.
*Reproduit* : `Cpu/x87_ops_misc.cs`, marqueurs PB-68 ; `x87-cases` : les huit sur dix-neuf
bornes × quatre ST(1) ; contrôles négatifs : FPTAN qui pose C2 → 76 cas divergents.

### PB-69 — Sur un PC ou un XT, l'exception du 8087 se perd : IRQ13 sans second PIC, pas de NMI

`x87_ops.h:17-30`, `x87_div` : une division par zéro non masquée appelle `picint(1 << 13)`,
seule interruption que PCem lève pour le coprocesseur — recompilée telle quelle pour le 8087
(`8087.h:86`). `pic.c:298-311`, `picint` : hors AT (et hors Xi8088), `num > 0xFF` ne tombe dans
aucune branche — la demande est jetée. Sur le silicium, le 8087 du PC et du XT signale par
**NMI** (sortie INT du 8087 vers la logique NMI, masquée par le port A0h), pas par IRQ13.
*Effet* : sur un 5150 ou un XT, un FDIV par zéro avec ZE démasqué ne produit rien : ni NMI, ni
IRQ ; seul `npxs` porte ZE, et le handler rend 1, valeur que le 808x ignore
(`808x.c:3304-3366`). Un gestionnaire d'exceptions flottantes (INT 2 chaîné par un runtime
Microsoft ou Borland) n'est jamais appelé.
*Trouvé par* : transcription de G4.6, lecture de `picint`.
*Reproduit* : `Models/pic.cs` (`picint`, inchangé) et `Cpu/x87_ops.cs` (`x87_div`, partagé par
les deux instanciations) ; marqueur PB-69 à l'aiguillage des ESC, `Cpu/808x.cs`.

### PB-70 — Le contrôle de l'infini est ignoré : le 287 compare en affine, comme un 387

Le bit 12 du mot de contrôle (IC, infini projectif ou affine) n'est lu nulle part dans
`x87_ops*.h` ni `x87.c` ; `opFINIT` (`x87_ops_misc.h:49-60`) pose 0x037F sur le 287 comme sur le
387. Sur le silicium, le 8087 et le 287 démarrent en projectif (+∞ = −∞) ; `x87_compare`
(`x87_ops.h`) rend toujours +∞ > −∞.
*Effet* : le test classique de génération (FINIT ; 1/0 ; FCHS ; FCOMPP) conclut au 387. Mesuré
en G4.7 : MSD de Windows 3.1, sur l'AMI 286 avec `fpu = 287`, affiche « 80286/80387 ».
*Trouvé par* : témoin MSD de G4.7, puis lecture du C.
*Reproduit* : par la transcription, `Cpu/x87_ops_misc.cs` (opFINIT) et `Cpu/x87_ops.cs`
(x87_compare), inchangés ; témoin MSD.

### PB-71 — Le canal IDE secondaire lit l'état de l'IRQ 14, pas le sien

`ide.c:138-143`, `ide_irq_update` : le test `(pic2.pend | pic2.ins) & 0x40` vise l'IRQ 14 (bit 6
du PIC esclave) pour LES DEUX canaux ; le secondaire lève et baisse pourtant l'IRQ 15 (0x80).
*Effet* : sur le canal secondaire, une interruption en attente sur l'IRQ 14 (canal primaire)
fait baisser l'IRQ 15, et une IRQ 15 en service ne l'empêche pas d'être relevée.
*Trouvé par* : reconnaissance de G5, lecture du C.
*Reproduit* : `Ide/ide.cs`, `ide_irq_update`, marqueur PB-71.

### PB-72 — Sélectionner un lecteur pendant un reset perd la tête et le mode LBA de l'écriture

`ide.c:401-420` : une écriture en 0x1F6 qui change de lecteur alors qu'un reset est en cours
termine le reset et REND LA MAIN, avant les lignes `:425-431` qui retiennent la tête, le bit LBA
et les quatre bits hauts de l'adresse LBA.
*Effet* : ces champs gardent leur valeur d'avant — zéro, remis par le reset —, quel que soit
l'octet écrit.
*Trouvé par* : reconnaissance de G5.
*Reproduit* : `Ide/ide.cs`, `writeide`, marqueur PB-72.

### PB-73 — READ MULTIPLE et WRITE MULTIPLE sans SET MULTIPLE MODE arrêtent l'émulateur

`ide.c:461-463`, `:487-489` : `blocksize` nul (aucun SET MULTIPLE MODE reçu) → `fatal()`. Le
disque réel rend ABRT.
*Effet* : un pilote qui envoie C4h ou C5h sans avoir fixé la taille de bloc — ou un invité
malveillant — arrête PCem.
*Trouvé par* : reconnaissance de G5.
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
*Reproduit* : `Ide/ide.cs`, `callbackide`, marqueur PB-74.

### PB-75 — Une commande à l'unité 1 absente arrête l'émulateur (Fixed Disk Adapter de l'AT)

`mfm_at.c:193-194` : si l'unité sélectionnée par 0x1F6 n'a pas d'image, l'écriture du registre de
commande appelle `fatal("Command on non-present drive")`.
*Effet* : un utilitaire qui sonde le second disque (FDISK, un diagnostic) sur une machine qui n'en
a qu'un arrête PCem.
*Trouvé par* : inventaire de la règle R9.
*Non reproduit* (R9) : `Mfm/mfm_at.cs`, marqueur `pcem bug, not reproduced: PB-75` — la commande
est refusée : ERR, erreur ABRT, IRQ 14.

### PB-76 — READ LONG et WRITE LONG (avec ECC) arrêtent l'émulateur

`mfm_at.c:224-225`, `:237-238` : les commandes 22h-23h et 32h-33h (bit ECC) appellent `fatal()`.
Le WD1003 réel transfère alors 512 octets plus 4 octets d'ECC ; ni PCem ni iXtal26 ne le modélisent.
*Effet* : un utilitaire de bas niveau qui lit les ECC arrête PCem.
*Trouvé par* : inventaire de la règle R9.
*Non reproduit* (R9) : `Mfm/mfm_at.cs`, marqueurs `pcem bug, not reproduced: PB-76` — ERR,
erreur ABRT, IRQ 14 : le comportement sûr le plus proche.

### PB-77 — `cpu_features` n'est jamais remis à zéro : un i486 hérite de CR4 et VME d'un iDX4

`cpu.c:483-485` pose `cpu_features = CPU_FEATURE_CR4 | CPU_FEATURE_VME` pour l'iDX4 (puis retombe
dans le cas i486) ; aucune ligne de `cpu_set` ne remet `cpu_features` à zéro (`cpu_CR4_mask`, lui,
l'est, `:316`). Changer de processeur dans la même session garde donc les caractéristiques du
précédent. Au passage, `cpu_CR4_mask = CR4_VME | CR4_PVI | CR4_VME` (`:485`) nomme VME deux fois.
*Effet* : après un iDX4, un i486DX accepte MOV CR4 — que le vrai i486DX refuse —, avec un masque
nul : CR4 reste à zéro. Le harnais rejoue `cpu_set` dans le même processus : l'ordre des entrées
d'un balayage décide de ce qu'elles héritent.
*Trouvé par* : reconnaissance de G6.
*Reproduit* : `Cpu/cpu.cs`, `cpu_set`, marqueur PB-77 ; l'empreinte CPU compare `cpu_features`.

### PB-78 — LOADALL386 s'exécute sur un 486

PCem n'a pas de table d'opcodes 486 : `cpu_set` pose `ops_386` pour tout processeur (`cpu.c:231`),
et `0F 07` y est `opLOADALL386` (`x86_ops_misc.h:931-973`), sans garde `is486`. Le 486 réel n'a
plus de LOADALL : #UD.
*Effet* : un 486 émulé charge l'état entier depuis ES:EDI ; un bloc non préparé y pose un CR0
avec PG et un CR3 quelconque — voir PB-79.
*Trouvé par* : fuzzeur du cœur 486 (G6.1), graine 1.
*Reproduit* : par la transcription (la table du 386 est partagée). Le fuzzeur du cœur 486 ne
tire plus `0F 07` au hasard (`Fuzzer.cs`, G6.1) : il ferait tomber l'oracle (PB-79).

### PB-79 — Une table de pages hors RAM fait tomber l'émulateur

`mem.c:216-218` : `mmu_readl` et `mmu_writel` déréférencent `_mem_exec[addr >> 14]`, nul pour
toute adresse sans mémoire. `mmutranslatereal` (`:220-317`) et `mmutranslate_noabrt`
(`:319-345`) y lisent le répertoire en `cr3 & ~0xFFF` et la table en `PDE & ~0xFFF`, sans borne.
*Effet* : un invité qui active la pagination avec un CR3 ou une entrée de répertoire hors de la
mémoire installée arrête PCem (segfault). Mesuré : D4 (MOV CR0 avec PG tiré), puis le fuzzeur du
486 par un LOADALL386 tiré au hasard (PB-78).
*Trouvé par* : G2 D4, puis G6.1.
*Non reproduit* (R9) : `Memory/mem.cs`, marqueurs `pcem bug, not reproduced: PB-79` — une
lecture de table hors RAM rend 0xFFFFFFFF (bus ouvert), une écriture y est ignorée. Le fuzzeur
écarte ce chemin côté oracle ; `r9-mmu` (C# seul) prouve la survie sur le 386 et le 486.

### PB-80 — La lecture linéaire en chain4 compact ne charge pas les verrous

`vid_svga.c:1391-1395`, `svga_read_linear` : en chain4 compact (`packed_chain4`) ou en
`fb_only`, la fonction rend l'octet et sort ; la forme par banque, `svga_read`, charge d'abord
les quatre verrous depuis `addr & ~3` (`:1085-1089`).
*Effet* : une écriture en mode 1 (copie des verrous) par la fenêtre linéaire, après une lecture
linéaire, recopie les verrous d'une lecture antérieure, pas ceux de l'octet lu.
*Trouvé par* : diff des deux formes, G7.0.
*Reproduit* : `Video/vid_svga.cs`, `svga_read_linear`, marqueur PB-80. Atteint par les cartes
de G7 (la VGA d'IBM ne pose ni `packed_chain4` ni `fb_only`).
*G7.1* : la GD5429 porte le même défaut dans sa propre lecture, `gd5429_read_linear`
(`vid_cl5429.c:1183-1187`), et sa forme par banque, `gd5429_read` (`:741-748`), y passe aussi :
chez elle, ni la banque ni la fenêtre linéaire ne chargent les verrous en chain4 compact.
Reproduit, `Video/vid_cl5429.cs`, même marqueur.

### PB-81 — La lecture du motif du blitter de la GD5429 sort de la VRAM

`vid_cl5429.c:1415-1424`, `gd5429_start_blit`, source en motif (`blt.mode & 0xc0` = 0x40) :
l'adresse source est masquée PUIS augmentée de `y_count << 3` + `x_count & 7` (jusqu'à 63 en
8 bpp, 127 en 16 ; le cas 32 bpp, 255, n'est pas atteignable sur la GD5429, dont la profondeur
tient en un bit, `:1710-1713`). Une source programmée dans les derniers octets de la VRAM fait
lire au-delà du tableau `svga->vram`.
*Effet* : comportement indéfini — lecture du tas après la VRAM ; en C#, une exception qui
abattrait l'hôte. Le silicium reboucle sur sa mémoire.
*Trouvé par* : relecture de la transcription, G7.1.
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
*NON reproduit* (R9) : marqueurs PB-85 dans les deux fichiers, DEVIATION — hors du tableau,
rien n'est écrit. `r9-s3`, `r9-cl5429` : curseur en x = 2040 sur la dernière ligne, survit.

### PB-86 — La pente d'un polygone S3 divise INT_MIN par -1

`vid_s3.c:1651` et `:1669`, `polygon_setup` : `(end_x - start_x) / (end_y - start_y)`, les
abscisses décalées de 20 bits ; `destx_distp` = -2048 depuis x = 0 donne INT_MIN, et une hauteur
de -1 suffit.
*Effet* : SIGFPE, PCem tombe (comme PB-47) ; en C#, `OverflowException`.
*Trouvé par* : relecture de la transcription, G7.3.
*NON reproduit* (R9) : `Video/vid_s3.cs`, marqueurs PB-86, DEVIATION — pour un diviseur de -1,
le quotient est pris replié (`-(end_x - start_x)`, INT_MIN pour INT_MIN), identique à PCem
partout ailleurs. `r9-s3` : survit, `poly_dx1` = `poly_dx2` = 80000000 ; sans la garde,
`OverflowException` (mesuré).

### PB-87 — Au repli de l'IP, le préfetch du 8086 lit 64 Ko plus loin

`808x.c:146-153`, `FETCH`, file vide : `prefetchpc = cpu_state.pc = cpu_state.pc + 1`, puis, sur
un 8086 et un `pc` impair, `prefetchqueue[0] = readmembf(cs + cpu_state.pc)`. `cpu_state.pc` est
sur 32 bits et n'est masqué qu'en fin d'instruction (`:3910`) : une instruction qui commence en
FFFFh et lit un second octet porte `pc` à 0x10001, et le préfetch lit `cs + 0x10001` — 64 Ko au-delà
du segment — là où le vrai 8086 lit `cs + 1` (`prefetchpc`, sur 16 bits, vaut bien 1).
*Effet* : au repli de l'IP, l'octet mis en file vient d'ailleurs ; sur un remplissage uniforme,
l'instruction suivante se décode et se chronomètre autrement.
*Trouvé par* : le fuzzeur 8086 en flux (G1.0, graine 1, ronde 325 : `FF FF` en FFFF:FFFF puis
FFFF:0001, 13 cycles contre 4). **La transcription de M1 lisait `cs + prefetchpc`** — le geste du
vrai 8086, pas celui de PCem —, et le 8088 n'atteignait jamais cette branche (`is8086`).
*Reproduit* : `Cpu/808x.cs`, `FETCH`, `cs + cpu_state.pc`, marqueur PB-87.

### PB-88 — La M24 recopie jusqu'à 510 octets dans une `charbuffer` de 256

`vid_olivetti_m24.c:414-415`, `m24_poll` : `for (x = 0; x < (crtc[1] << 1); x++)
charbuffer[x] = …`, et `:171-172` la relit jusqu'à `(crtc[1] << 1) + 1`. Le registre R1 du CRTC
n'est pas masqué (`crtcmask[1] = 0xff`, `:41`) : l'index monte à 509 dans un tableau de 256
(`:19`).
*Effet* : PCem écrit 253 octets au-delà, dans les champs qui suivent `charbuffer` dans `m24_t`
(`ctrl`, `base`, `cgamode`… jusqu'au `pc_timer_t` de la carte, pointeurs de rappel et de
chaînage compris) : l'invité peut faire tomber l'émulateur. En C#, une exception.
*Trouvé par* : reconnaissance de G1 (PLAN-G1.md, défaut n° 1).
*NON reproduit* (R9) : `Video/vid_olivetti_m24.cs`, marqueurs PB-88, DEVIATION — une écriture
au-delà de 255 est sautée, une lecture rend 0. Identique à PCem tant que R1 ≤ 128 (les modes
du BIOS : 40 et 80 colonnes).

### PB-89 — La bordure de la M24 déborde sur la ligne suivante de `buffer32`

`vid_olivetti_m24.c:154-166`, `:218-231`, `:258-259`, `:279` : l'abscisse `c + (crtc[1] << 4) +
8` atteint 4 095 pour une ligne de 2 048 points. Les lignes de `buffer32` sont contiguës
(`wx-sdl2-video.c:59-69`) : l'écriture tombe sur la ligne suivante, jamais hors du tableau —
`displine` reste sous 720.
*Effet* : des points de bordure sur la ligne d'en dessous, avec un R1 hors des modes du BIOS.
*Trouvé par* : reconnaissance de G1 (défaut n° 2) ; la relecture a montré qu'il ne sort pas du
tableau, donc pas de R9 pour la M24.
*Reproduit* : `Video/vid_olivetti_m24.cs`, marqueur PB-89 (le même tableau plat).
*G1.2* : le PC1512 fait de même (`vid_pc1512.c:182-193`, `:317-319`), `displine` revenant à 0
au-delà de 360 (`:326-327`) et de 262 (`:369`) : jamais hors du tableau non plus. Reproduit,
`Video/vid_pc1512.cs`.

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
Les deux commentaires laissés par l'auteur au-dessus (`pit.c:420-421`,
`"Speaker overflow"`) disent qu'il a soupçonné le débordement sans le fermer.

*Effet* : `speakval` sort de sa plage nominale [−0x2000, +0x2000] et vaut `INT_MIN`
jusqu'à la prochaine écriture au PIT. Il n'est lu que par `speaker_update`
(`sound_speaker.c:24`) quand `pit.m[2]` vaut 0 ou 4 ; le bip du POST est en mode 3, donc
l'audition n'en dépend pas. Tronqué en `int16_t` à `sound_speaker.c:35`, `INT_MIN`
donnerait 0 — le silence, là où la valeur nominale aurait donné une tension.

*Reproduit* : `Models/pit.cs:543`, avec la garde explicite qu'impose .NET — voir la
`DEVIATION` sur place. C'est le seul endroit du dépôt où une conversion flottant→entier
devait être écrite à la main : .NET **sature** (`(int)float.PositiveInfinity` vaut
`int.MaxValue`, que le clamp ramène alors à 0x2000) là où x86 rend l'entier indéfini.
Trouvé par la sonde `speaker-probe` de M9, au premier tir.

---

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
*NON reproduit*, exception assumée comme PB-46 et PB-47 : la chaîne est bornée à
`_386.SS_SHADOW_MAX` = 1 024 chargements enchaînés (`Cpu/386_ops_stack.cs`, profondeur
commune aux quatre sites) ; au-delà, le chargement de SS rend la main sans ombre et exec386
reprend l'instruction suivante. Marqueurs `// pcem bug, not reproduced: PB-49` :
`Cpu/386_ops_stack.cs` (POP SS w et l), `Cpu/386_ops_mov_seg.cs` (MOV SS a16 et a32).
`iXtal26.Diff popss-check` : identique à l'oracle jusqu'à 1 024 chargements (POP SS, MOV SS,
mêlés ; 286 et 386) ; au-delà, écart ATTENDU, là où l'oracle survit encore ; RAM entière
remplie de 0x17, C# seul : 200 pas de 1 025 POP SS, sans plantage ni boucle. Sans la borne,
le même test fait tomber le C#.

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
*Reproduit* : `Mfm/mfm_xebec.cs`, chaînes conservées telles quelles.

### PB-27 — Trois `switch` internes sans `default:` là où six autres appellent `fatal()`

`mfm_xebec.c:314` (`CMD_READ_STATUS`), `:675` (`CMD_DTC_GET_GEOMETRY`) et `:694`
(`CMD_DTC_SET_GEOMETRY`) ouvrent un `switch (xebec->state)` sans branche par défaut. Les
six autres commandes qui en ouvrent un — `:334`, `:389`, `:480`, `:573`, `:594`, `:651` —
terminent toutes par `default: fatal(...)`.

*Effet* : sur un état inattendu, le contrôleur ne fait **rien** : ni octet de fin, ni
chronomètre réarmé, ni IRQ. Il se fige au lieu de s'arrêter bruyamment, ce qui est le
contraire de l'intention affichée par les six autres. Aucun état atteint pendant
l'amorçage, `FDISK` et `FORMAT C: /S` de § M12 n'y mène.
*Reproduit* : `Mfm/mfm_xebec.cs`, les trois `switch` restent sans `default:`.

### PB-29 — `ide_fn` déclaré `[4][512]` dans `scsi_ibm.c`, défini `[7][512]` dans `ide.c`

`ide.c:105` définit `char ide_fn[7][512]`. `scsi_ibm.c:21` en re-déclare l'`extern` avec
une borne différente : `extern char ide_fn[4][512];`. Les cinq autres consommateurs — dont
`mfm_xebec.c:26` — écrivent bien `[7][512]`.

*Effet* : aucun à l'exécution, la borne d'un tableau externe n'entrant pas dans l'édition
de liens. Mais un lecteur de `scsi_ibm.c` en déduit quatre disques là où il y en a sept,
et un `-fsanitize=bounds` sur cette unité de traduction signalerait à tort les indices 4
à 6.
*Sans objet ici* : `scsi_ibm.c` n'est pas transcrit. `Disc/hdd.cs` porte la définition à
sept, celle d'`ide.c`.

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
*Reproduit* : `Mfm/mfm_xebec.cs`, les trois définitions sont conservées — une constante
morte n'est pas un commentaire (même arbitrage que PB-20).


### PB-32 — `pmodeint` : une précédence d'opérateurs annule le code d'erreur

`x86seg.c:1659`, dans la branche « vecteur hors des bornes de l'IDT » :

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
*Reproduit* : `Cpu/x86seg.cs`, marqueur `// pcem bug, reproduced: PB-32` dans `pmodeint`.
Transcrit avec la même précédence, donc le même résultat — le corriger changerait le code
d'erreur d'un côté seulement.


### PB-33 — `savenvr` écrit dans un fichier qu'il n'a pas vérifié avoir ouvert

`nvr.c:770-772`, la queue commune de `savenvr` après son `switch (oldromset)` :

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
*Reproduit* : `Devices/nvr.cs`, marqueur `// pcem bug, reproduced: PB-33`. Le
`NullReferenceException` de C# est le pendant du déréférencement de NULL du C. **Mesuré
dans ce dépôt** : le premier amorçage après l'écriture de `savenvr`, avec un `nvr/`
encore absent, a levé exactement là — c'est ce qui a fait créer le répertoire et son
`README.md`.

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
*Reproduit* : `Host/HddImage.cs`, la table est recopiée **verbatim**, le 462 compris. La
corriger serait réécrire la table d'un oracle ; `PrintHddTypes` cite ce PB à la place.


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
`vga_init` a tout effacé. Un vrai DAC lirait à l'index de LECTURE, que `3C7h` pose.
*Atteint* : oui, et compté — deux fois dans la campagne graphique de VERIFICATION.md
§ M15, par un programme qui fait `OUT 3C8h,0` puis trois `IN AL,DX` sur `3C9h`. Le BIOS
VGA, lui, ne le fait jamais : 1 536 lectures du DAC, aucune à l'index -1.
*Reproduit* : `Video/vid_svga.cs`, `vgapal_at`, marqueur `// pcem bug, reproduced:`. Le
C lit hors du tableau sans broncher, le C# lèverait ; `vgapal_at` rend les octets de
`pallook[511]`, comme la disposition mémoire du C. Le diff de la campagne l'a vérifié : les
registres qui reçoivent ces lectures sont hachés à chaque instruction.

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
*Reproduit* : `Video/vid_svga.cs` et `Video/vid_cga.cs`, marqueur
`// pcem bug, reproduced: PB-36`, par `unchecked((uint64_t)(int64_t)x)`. Le `(uint64_t)x`
direct n'était PAS fidèle : .NET 9 et au-delà **saturent** à 0 (mesuré sur .NET 10 :
`(ulong)-5.5 = 0`, `(ulong)(long)-5.5 = 0xFFFFFFFFFFFFFFFB`, GCC -O2 rend le second). Trouvé
par la relecture contradictoire de § M15, puis rendu observable : un programme DEBUG qui
pose CR00 = 2Dh sous CR01 = 4Fh et lit `3DAh` 8 192 fois fait rougir le diff d'amorçage à
l'instruction 36 899 042 avec l'ancienne conversion, et le laisse vert — 37 969 642
instructions — avec la nouvelle.

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
ligne. La branche avec remappage (`:720-738`) a le même défaut. Le rendu haute résolution
voisin (`:742-789`) écrit par `*p++` et ne l'a pas. Et `svga->ma` n'est pas masqué en
sortie, contrairement aux cinq autres rendus 15 à 24 bpp — `ma` est masqué à chaque
lecture, donc sans effet sur les adresses.

*Effet* : en 24 bpp basse résolution, seuls les huit premiers pixels de chaque ligne
changent, et ils portent le DERNIER groupe de la ligne ; le reste du tampon garde l'image
précédente.
*Atteint* : oui, et compté — 368 908 appels dans la campagne de la 8900D (VERIFICATION.md
§ M19), où le RAMDAC TKD8001 pose `bpp = 24` (`vid_tkd8001_ramdac.c:26-28`) et où
`svga_recalctimings` choisit le rendu basse résolution quand le bit 6 d'AR10 est posé
(`vid_svga.c:341`, `:403-407`).
*Reproduit* : `Video/vid_svga_render.cs`, marqueur `// pcem bug, reproduced: PB-37`. Confronté
à l'oracle : la campagne est verte au diff (339 586 475 instructions) ET à la sonde, dont le
hachage de `buffer32` porte les pixels que ce rendu écrit.

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

La branche sans remappage avance déjà `ma` de `x << 1` ; la ligne qui suit la branche
l'avance une seconde fois — et, avec remappage, ajoute `x << 1` à un `ma` déjà avancé de 4
par groupe. Les trois autres rendus 15/16 bpp n'ont que la première. `ma` sert d'adresse de
départ de la ligne suivante quand le rendu n'est pas rappelé sur une ligne répétée ; le
compteur de ligne du CRTC (`svga_poll`, `vid_svga.c:564-578`) le recharge depuis `maback` à chaque ligne
affichée, ce qui borne l'effet.

*Effet* (déduit à la lecture, non mesuré) : invisible tant que `svga_poll` recharge `ma`
depuis `maback` avant chaque ligne ; un `ma` doublé ne servirait qu'au test `changedvram`
d'un appel suivant sur la même ligne.
*Atteint* : **non** par la campagne de § M19 — `16bpp_lowres` y est le seul des six rendus
neufs à zéro passage. Il faudrait `bpp = 16` (TKD8001 de la 8900D) avec le bit 6 d'AR10.
*Reproduit* : `Video/vid_svga_render.cs`, marqueur `// pcem bug, reproduced: PB-38` — reproduction
PAS ENCORE confrontée à l'oracle.

---

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
GDT, donc `tr.seg & 4` vaut 0 et la GDT est choisie, qui est la bonne.
*Trouvé par* : relecture pendant la transcription de taskswitch286, G2 D5.
*Reproduit* : `Cpu/x86seg.cs`, taskswitch286, commentaires `verbatim`.

### PB-53 — FBSTP écrit la globale `tempc` des drapeaux

`x87_ops_loadstore.h:152-164` : `uint8_t tempc` est déclaré DANS la boucle ; après elle,
`tempc = (uint8_t)floor(fmod(tempd, 10.0));` et `tempc |= 0x80;` ne peuvent donc pas viser
cette locale — ils écrivent la globale `int tempc` de `x86_flags.h:3`, celle qu'ADC et SBB
lisent (`x86_flags.h:577`). Le C compile parce qu'une globale du même nom est en portée.
*Effet* : aucun observable — ADC et SBB reposent `tempc` avant de le lire. L'octet de signe
écrit en mémoire est juste, lu depuis cette globale.
*Trouvé par* : transcription de G4.2 (la variable de l'écriture finale n'existait pas).
*Reproduit* : `Cpu/x87_ops_loadstore.cs`, `FBSTP_a16/_a32`, `x86_flags.tempc`, marqueur
`// pcem bug, reproduced: PB-53`.

## Portée de ce registre

Ces **soixante-huit** défauts sont ce que les oracles ont éclairé, **pas le résultat d'un
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

Le dépôt transcrit environ **8 600 des 309 000 lignes** de PCem. Tout ce qui n'a pas été
lu n'a pas été examiné : le dynarec, les cartes vidéo autres que la CGA, la VGA, les deux Trident, la GD5429 et la Trio64, les
cartes son, l'IDE, le SCSI et les images VHD restent hors de ce registre. Le cœur 386, lu en
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
