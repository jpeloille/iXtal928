# PS2 — La souris PS/2 ; le plan, sur reconnaissance

> Écrit le 3 octobre 2026, après G8. Hors plan, ligne « Transverse » de `PLAN.md` (« la souris
> PS/2 (`mouse_ps2`), attendue sur les 386 et 486 : elle passe par le 8042 déjà transcrit »).
> Feu vert de l'utilisateur du 03/10, relayé par l'orchestrateur. Chaque constat cite la ligne
> de C qui le fonde, sur `pcem-dev/` tel que vendoré. Rien ne s'écrit avant la validation des
> décisions de fin de fichier.

## Où on en est

**Le 8042 est prêt.** `Keyboard/keyboard_at.cs` porte déjà tout le côté contrôleur de la souris :
la file `mouse_queue` (16 octets, `keyboard_at.c:81-85`), `keyboard_at_adddata_mouse`, l'IRQ 12
(`picint(0x1000)`), les commandes A7h/A8h/A9h, D3h, D4h (« write to mouse », qui appelle
`mouse_write` s'il est posé et met `mouse_scan` à 1), le bit 5 de l'octet de commande, et
`keyboard_at_set_mouse`. Il ne manque que le périphérique au bout du fil : `mouse_ps2.c`
(248 lignes). `mouse_scan` vit aujourd'hui dans `keyboard_at.cs` (DEVIATION, son fichier n'étant
pas transcrit) et dans `harness_stubs.c` (`int mouse_scan;`).

**Le registre.** `Mouse/mouse.cs` garde les indices de PCem (`mouse.c:9-15`) ; les places 2
(« 2-button mouse (PS/2) ») et 3 (« Microsoft Intellimouse (PS/2) ») sont nulles. `pc.cs:489-495`
lit `mouse_type` et retombe sur 0 pour une souris non transcrite. L'oracle n'amorce que la souris
série (`harness.c:1364-1368`) ; `h_mouse_poll` existe (M21) mais **aucune porte n'injecte de
mouvement** : le chemin « hôte → souris → invité » n'a jamais été comparé.

**Le chemin hôte** est générique : `Host/SdlMouse.cs` remplit `mouse_x/y/z/buttons`
(`wx-sdl2-mouse.c:16-36`), `pc.cs` appelle `mouse_poll` (`pc.c:112-119`) — la souris PS/2 n'a
rien à y ajouter.

## Le constat qui conditionne tout : aucune machine du dépôt n'a `MODEL_PS2` chez PCem

L'interface de PCem n'offre une souris PS/2 qu'aux modèles marqués `MODEL_PS2`
(`wx-config.c:64`). Or ni `ami286` (`model.c:986-990`), ni `ibmat` (`:1106-1110`), ni `ami386`
(`:1238-1242`), ni `ami386dx` (`:1340-1344`), ni `ami486` (`:1412-1416`) ne le sont. « Attendue
sur les 386 et 486 » (PLAN.md) ne tient donc pas chez PCem tel qu'on s'en sert — c'est la règle
de G8.3 (« comme l'interface de PCem l'impose »).

Ce que le matériel émulé en dit : les trois BIOS AMI 386/486 (`ami386/ami386.bin`,
`ami386dx/opt495sx.ami`, `ami486/ami486.bin`) portent la chaîne de setup « Mouse Support
Option » ; celui de l'AMI 286 ne l'a pas. Le 8042 de PCem, lui, répond à D4h sur toutes les
machines AT. Une souris PS/2 sur ces trois machines est donc plausible matériellement, mais c'est
un écart à PCem (décision n° 1). À mesurer avant tout : le BIOS rend-il les services INT 15h C2h
(souris BIOS), dont dépendent `MOUSE.COM` et le `MOUSE.DRV` de Windows ? Option de setup à
activer dans une copie /tmp du CMOS.

## Ce que `mouse_ps2.c` fait (et les défauts relevés, à lire à la ligne puis inscrire)

`mouse_ps2_write` (`:36-161`) : commandes E6h, E7h, E8h (+ donnée), E9h (état, 3 octets), EBh
(lecture à distance), F2h (identifiant : 00h, ou 03h en mode Intellimouse), F3h (+ donnée), F4h,
F5h, FFh (reset : vide la file, AAh 00h) ; le « knock » Intellimouse F3 C8 F3 64 F3 50 (`:149-160`).
`mouse_ps2_poll` (`:163-212`) : paquet de 3 (ou 4) octets si `mouse_scan`, mode flux, `MOUSE_ENABLE`
et place dans la file (`< 13`), déplacements bornés à [-256, 255].

1. **Commandes sans réponse.** Toute commande hors de la liste — F6h (valeurs par défaut), EAh
   (mode flux), F0h (mode distant), EEh (écho), ECh, EDh — ne reçoit ni ACK ni rien (`:144-145`,
   le `default` commenté) ; un pilote qui attend FAh attend jusqu'à son délai. `MOUSE_REMOTE` et
   `MOUSE_ECHO` ne sont donc jamais posés. À reproduire.
2. **E9h, bouton du milieu** : `temp |= 3` (`:83-84`) au lieu de 4 — le bit du bouton droit et
   du gauche, pas celui du milieu. À reproduire.
3. **E9h ne lit pas la mise à l'échelle comme le vrai protocole** (`flags` porte `MOUSE_ENABLE`
   0x20 et `MOUSE_SCALE` 0x10 ; le vrai octet d'état met « remote » en bit 6, l'activation en
   bit 5, l'échelle en bit 4 — conforme ici) : à vérifier à la lecture, rien à inscrire si
   conforme.
4. **La file du 8042 sans garde de débordement** : `keyboard_at_adddata_mouse` écrit sans
   tester la place (`keyboard_at.c`, déjà transcrit) ; au-delà de 16 octets non lus, `end`
   rattrape `start` et la file paraît vide. `mouse_ps2_poll` se garde (`< 13`), pas les réponses
   de `mouse_ps2_write` (E9h en pousse 4). Déjà reproduit côté 8042 ; à noter.
5. **R9** : aucun tableau indexé par l'invité dans `mouse_ps2.c` (`last_data[6]` décalé à indices
   fixes), aucun `fatal()` (les deux sont commentés). Rien à écarter, a priori.
6. `ROM_PC5086` (`upc_set_mouse`, `:224-225`) : machine absente, branche omise et marquée.

## Les étapes

### PS2.0 — `mouse_ps2.c`, des deux côtés ; l'injection de la souris dans le boot-diff  ✅ *fait, VERIFICATION.md § PS2.0*

Transcription de `mouse_ps2.c` (`Mouse/mouse_ps2.cs`), les places 2 et 3 du registre ;
`mouse_scan` rejoint son fichier (la DEVIATION de `keyboard_at.cs` tombe). Oracle : `mouse_ps2.c`
lié, `int mouse_scan;` retiré des souches ; `h_set_mouse_type` et un `h_mouse_poll` qui appelle la
souris montée ; une sonde de la souris (état de `mouse_ps2_t`, `mouse_queue`, `mouse_scan`) en fin
de boot-diff. iXtal26.Diff : `--mouse-type N` et `--mouse-at TRANCHE:dx,dy,dz,b` (répétable),
injecté aux mêmes tranches des deux côtés. **Porte** : boot-diff ami486 + souris PS/2 jusqu'au DOS ;
un banc dirigé PS2BANC (.COM saisi dans DEBUG, comme SBBANC) qui parle au 8042 directement :
A8h, D4h FFh (AAh 00h), F2h, E8h/F3h, E9h, F4h puis des paquets nés de mouvements injectés, EBh,
F5h, le knock Intellimouse (type 3 : F2h → 03h, paquets à 4 octets), une commande sans réponse
(défaut n° 1), A7h — sous boot-diff, sonde de la souris comparée ; contrôle négatif.

### PS2.1 — Les machines et les témoins

La souris sur les profils retenus (décision n° 1) ; INT 15h C2h mesuré ; témoins `--boot` dans
`/tmp` : `C:\UTILS\MOUSE.COM` (pilote Microsoft, sur le disque 486) qui doit dire « PS/2 », puis
Windows 3.11 et son `MOUSE.DRV` (« Microsoft, or IBM PS/2 ») avec des mouvements injectés par une
commande de script `@souris dx,dy,b` — le pointeur bouge sur la capture. Contrôle négatif : la
souris série à sa place, `MOUSE.COM` ne trouve pas de souris PS/2.

## La vérification

- **Diff d'instructions** : tout ce que le logiciel lit du 8042 (port 60h/64h, IRQ 12).
- **Sonde de la souris** : l'état interne, que l'invité ne lit pas tout.
- **L'injection** : la première porte du dépôt qui fasse bouger une souris des deux côtés.

## Les décisions à trancher

1. **Les machines.** *(proposé : la souris PS/2 offerte aux trois AMI 386/486 — ami386, ami386dx,
   ami486 —, DEVIATION « MODEL_PS2 ajouté là où le BIOS porte l'option de souris », écart assumé à
   l'interface de PCem ; pas l'AMI 286 ni l'IBM AT, dont le BIOS ne la gère pas. Alternative : la
   règle de G8.3 à la lettre — refuser une souris PS/2 sans `MODEL_PS2`, avertir et retomber sur
   la série —, et le bloc se réduit à transcrire un périphérique qu'aucune machine du dépôt ne
   peut monter.)*
2. **L'Intellimouse** (place 3, 3 boutons et molette) : transcrite avec la 2 boutons — même
   fichier, une dizaine de lignes *(proposé : oui)*.
3. **Les profils** : `mouse_type = 2` sur `ixtal26-486.cfg`, `-486-s3.cfg`, `-386.cfg`, à la
   place de la souris série *(proposé : oui si le BIOS rend INT 15h C2h, mesuré en PS2.1 ; sinon
   la série reste et la PS/2 est offerte sans être posée)*. L'option « Mouse Support Option » du
   CMOS : réglée par l'utilisateur dans son setup — rien n'est écrit dans `nvr/`.
4. **Un `mouse_type` refusé** (PS/2 sur une machine sans la décision n° 1) : avertir et retomber
   sur la série, comme `pc.cs:491-495` le fait déjà pour une souris non transcrite *(proposé)*.
5. **Ce fichier** : `PLAN-PS2.md`.
