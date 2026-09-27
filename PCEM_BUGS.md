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
*Reproduit* : `Cpu/x86seg.cs`, loadcscall et loadcsjmp, transcrits tels quels ; épinglé par
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

## Portée de ce registre

Ces **quarante-quatre** défauts sont ce que les oracles ont éclairé, **pas le résultat d'un
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

Le dépôt transcrit environ **8 600 des 309 000 lignes** de PCem. Tout ce qui n'a pas été
lu n'a pas été examiné : le cœur 486, le dynarec, les cartes vidéo autres que la CGA, la VGA et les deux Trident, les
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
