# G13 — Corriger les défauts de PCem reproduits : le mode matériel ; le plan, sur reconnaissance

> Écrit le 6 octobre 2026. Bloc G13 de `PLAN.md` (décision utilisateur du 03/10). Feu vert le 06/10, « commence par
> le plan ». **Les décisions de la fin du fichier sont validées par l'utilisateur le 07/10** (« d'accord avec les
> propositions, commence par G13.0 puis attends pour G13.1 »), sauf les n° 3, 6 et 14, à trancher avant G13.2. Chaque constat cite la ligne
> de C qui le fonde, sur `pcem-dev/` tel que vendoré, et chaque vrai comportement sa source. Reconnaissance : six
> lectures par domaine (le processeur, le x87, le stockage, la vidéo, le son, la carte mère) et une sur le mécanisme,
> chacune suivie d'une contre-lecture qui l'a corrigée ; on retient ici les contre-lectures. Les quatorze rapports sont
> dans `iXtal26/Docs/G13-reconnaissance/` : le détail par PB, les sources, les pages.

## Où on en est

- **Le registre** (`PCEM_BUGS.md`, après G12) : 167 défauts. Section A, ce qui fausse un résultat observable : 96
  reproduits, 21 non reproduits. Section B, le comportement indéfini en C : 14 reproduits selon PLAN.md, 12 en réalité
  (PB-07 est neutralisé, PB-09 passe sous R9, ci-dessous), 10 non reproduits, PB-111 sans statut. Section C : 24
  entrées, plus PB-29.
- **Dans le code** : 215 marqueurs `// pcem bug, reproduced:` dans 51 fichiers — 161 de la section A, 12 de la B, 29
  de la C, 13 sans numéro ; 69 sites `not reproduced` (R9 et les bornes).
- **La règle qui les a faits** : R8 — PCem est l'oracle, et un défaut de PCem se reproduit ; R9 en est la seule
  exception. G13 ne touche à aucune des deux : le mode PCem reste celui des portes, le mode matériel s'ajoute à côté.
- **Les références du vrai matériel dans le dépôt** : le corpus SingleStepTests, enregistré sur un 8088 (AMD D8088)
  et un 386 (Intel 386EX, en mode réel). `sst-baseline.tsv` et `sst386-baseline.tsv` disent, forme par forme, où PCem
  s'en écarte ; les vecteurs eux-mêmes ne sont pas sur la machine, seuls leurs manifestes le sont.

## Ce que la reconnaissance a trouvé

### Ce qui n'attend pas G13

Quatre défauts touchent l'hôte ou les fichiers de l'utilisateur dès aujourd'hui, en mode PCem. Ils se corrigent dans
les deux modes, avant tout le reste (G13.0) :
- **PB-09 viole R9.** En mode texte de la CGA, un `OUT` qui pose R1 ≥ 129 (3D8h bit 0 à 1, sur une rangée affichée)
  lève `IndexOutOfRangeException` dans `cga_poll` (`Video/vid_cga.cs:249-250`, `:554-557`). Rien ne la rattrape : le
  processus se termine. PCem, lui, corrompt son tas avec des octets choisis par l'invité. Sortie proposée : un
  `charbuffer` de 512 octets. C'est ce que lit la vraie carte, qui n'a pas de tampon de ligne ; c'est identique à PCem
  là où il survit (R1 = 129 et 130) ; rien ne s'ajoute au chemin chaud. Et `r9-cga`, avec le `r9-m24` qui manque à
  PB-88.
- **PB-17 viole R9.** Une image de disquette dont le BPB annonce au moins 41 secteurs par piste, et au plus 25 000
  octets par piste, fait lever `ArgumentOutOfRangeException` dans `fread` (`Disc/disc_img.cs:86-87`), au démarrage
  comme au menu. Deux voisins non inscrits : une division par zéro (0 secteur par piste, `disc_img.cs:311`) et un
  fichier vide (`disc.cs:134`, une faute de transcription : PCem survit). Aucune des 36 images de `os/` ne déclenche
  l'un des trois.
- **PB-121 corrompt l'image SCSI de l'utilisateur.** Avec le profil `ixtal26-486-scsi.cfg`, un « Reset materiel » du
  menu laisse ouvert le flux d'avant, qui garde en tampon le dernier secteur écrit ; à la sortie, ce tampon est vidé
  en dernier et écrase ce qui a été écrit depuis. Rangé en section C, il a un effet réel depuis l'élargissement de
  G11. R9 ne le couvre pas : décision n° 2.
- **Toute exception non rattrapée perd les dernières écritures.** `closepc` est le seul endroit qui vide les tampons
  des images (`pc.cs:1306-1320`). PB-09 et PB-17 le sautent ; PB-33 aussi, puisque `savenvr` passe avant `closepc` et
  lève si `nvr/` manque. Un filet de l'hôte (vider les images sur toute sortie anormale) relèverait de G16, mais il
  protège les fichiers de l'utilisateur : décision n° 2.

### Le registre n'est pas prêt

- **Les marqueurs.** 29 PB des sections A et B n'ont pas leur numéro sur la ligne de leur marqueur ; sept n'en ont
  nulle part (PB-01, 03, 07, 51, 54, 56, 70). Deux marqueurs de `Keyboard/keyboard_olim24.cs` (`:287`, `:298`) n'ont
  aucune entrée au registre : l'octet de souris de la M24. Les numéros de ligne des champs *Reproduit* ont dérivé : le
  recensement se fait par marqueur, jamais par ligne.
- **Des entrées fausses ou renversées**, sources relues :
  - PB-147 : la pause du DMA d'un DSP 1.xx est écrite par Creative ; le vrai défaut est que D3h ne coupe pas le son.
  - PB-165 est renversé : 08h ne prend aucun paramètre sur les DSP 4.04 à 4.16 (vérifié au binaire). L'AWE32 de PCem
    est juste ; c'est sa SB 16 qui a tort.
  - PB-145 : le CD au minimum après un reset est la valeur documentée des trois mélangeurs ; reste la SB 2.0 sans
    mélangeur.
  - PB-92 : sans doute fidèle (l'IRQ 10 n'existe pas sur un emplacement 8 bits), mais la source citée dit le
    contraire ; à refonder.
  - PB-95 : le vrai octet d'état de la souris PS/2 met le bouton gauche en bit 2 et le droit en bit 0 (IBM 15F0306,
    l'ABIOS, le TR du Model 25) ; PCem place mal les trois boutons.
  - PB-03 : les −5,87 ppm du `--timer-check` viennent de la troncature `cpu_get_speed() / 100` de `pc.c:473`, un
    rythme de l'hôte que l'invité ne voit pas, et non de PB-11.
  - PB-07 n'est pas reproduit mais neutralisé : un zéro des deux côtés, sans marqueur.
  - PB-41 : la correction que l'entrée suggère contredit Intel (des moitiés hautes « modifiées et non maintenues »).
  - PB-87 est plus large : la lecture principale de `FETCH` (`808x.c:145`) a le même défaut, sur le 8088 aussi.
  - PB-100 : CR3F n'existe pas sur l'ET4000AX ; la Sierra SC15025/26 a bien des modes à 4 octets par point.
  - PB-144 : le sens du transfert ignoré est conforme à la carte (AHA-1540C TR) ; ce qui manque est le contrôle de
    longueur.
  - D'autres, de détail (PB-16, 30, 35, 36, 37, 99) : dans les rapports.
- **Des défauts voisins non inscrits**, vérifiés par les contre-lectures :
  - *le processeur* — sur le 8088 : DAA et DAS (la règle observée sur silicium, pas le pseudo-code du SDM), REP LODSW
    (AL et AX jamais chargés), SETMO, l'OF de SHL par CL, le débordement de DIV, les SF et ZF d'AAM et d'AAD, le CF de
    SAR par CL, trois défauts de `rep()`. Sur le 386 : l'AF d'ADC, LOCK sans #UD, BT* et son décalage non signé, MOVSX
    r16,r/m16 traité en illégal, AAA et AAS à la façon du 8086, le SF d'AAD, DAS. Les 742 formes du 386 qui s'écartent
    du corpus ne sont pas 742 défauts : la sonde n'applique pas le masque des drapeaux indéfinis que le corpus fournit
    (`f_umask`) ;
  - *le x87* — une vingtaine, dont FXTRACT absent, FBSTP qui tronque au lieu d'arrondir, FIST hors bornes, et FSTENV
    qui ne masque pas les exceptions, visible dès le mode PCem ;
  - *le stockage* — `ide_irq_update` qui fait retomber une IRQ que l'unité tient encore, le sense du Xebec sans
    adresse, deux défauts de PLAY AUDIO MSF ;
  - *la vidéo* — `htotal = CR00 + 6` au lieu de « moins 5 » chez IBM : toutes les cartes SVGA balayent à 31,16 kHz
    au lieu de 31,47 ; le chemin rapide des accès en mot, qui ignore les verrous, le mode d'écriture et le masque ; le
    curseur plein et les rythmes de clignotement de la CGA et de la MDA (schémas IBM) ; des bits de 3BAh, de l'ET4000
    et de l'EGA ;
  - *le son* — une vingtaine : l'octet de référence de l'ADPCM, la fin d'un bloc ADPCM, l'état du DSP au reset, les
    volumes de reset relevés, la MIDI de la SB absente, la grande vitesse, le MPU, 3Dh, 3Eh et 0Ah du CT1745 ;
  - *la carte mère* — la cascade du 8259 servie avant IRQ0 et IRQ1, le masque de service ignoré, OCW2 et OCW3 ; le
    8237 (Clear Mask, registre de requête, master clear, masques au reset, la cascade) ; les files du 8042 ; ICW1 ;
    pas de rafraîchissement par DMA sur la M24 et le PC1512 ; le coût du cycle DMA.

### Les sources existent, presque partout

Primaires : les manuels Intel (8086 à 486, 8087 à 387, SDM, AP-578) ; les fiches 8237A, 8259A, 8254, 8042, MC6845,
µPD765 ; les Technical Reference d'IBM (PC, XT, AT, Options and Adapters, PS/2) ; ATA-1, ATA-3, SFF-8020i, MMC-2,
SCSI-2, l'AHA-1540C Technical Reference ; le guide de programmation de Creative, le guide de l'EMU8000, le manuel du
MPU-401 ; les data books de Tseng, Cirrus et Sierra. Deux sources « de code » s'y ajoutent : les micrologiciels des
DSP 2.02 à 4.16, désassemblés et réassemblables à l'identique, et les octets des ROM du dépôt, désassemblés
(décision n° 8). Une vingtaine de points restent inconnus faute de source (les relectures illégales du 8237, les
moitiés hautes d'une TSS de 16 bits, les bits exacts des transcendantes, le bus flottant…) : ils restent reproduits.

## Le principe (décision n° 1)

- Le mode PCem reste le défaut, et celui des portes ; l'oracle n'est pas touché (ni `h_set_*`, ni ABI).
- Le mode matériel corrige À CÔTÉ : la ligne fausse reste transcrite et vivante en mode PCem. R8 ne change pas.
- Il n'ajoute aucun arrêt (R9) et reste déterministe.
- Un PB n'entre au mode matériel qu'avec sa source, son cas qui discrimine (la valeur de PCem d'un côté, celle de la
  documentation de l'autre) et sa panne injectée. Un comportement inconnu reste reproduit, et l'entrée PB dit ce qu'il
  faudrait mesurer.
- Le mode matériel n'est pas un « temps matériel » : le modèle de temps reste celui, approché, de PCem, hors les PB de
  temps explicitement corrigés.

## Le mécanisme (décisions n° 3 à 5)

- **Deux étages.** Un champ par PB, dans une classe centrale qui ne contient rien d'autre ; pour l'utilisateur, un seul
  interrupteur ; les domaines comme alias de listes de PB, pour la ligne de commande et les outils. Une table donne à
  chaque PB son domaine et ses groupes, les corrections qui ne valent qu'ensemble (PB-55 et 56 ; PB-45 et le
  débordement de DIV…).
- **Des `static readonly bool` figés.** Un constructeur statique explicite ; un gel unique, en tête d'`initpc` (une
  ligne `// DEVIATION:`), dans les cinq remises à zéro des harnais (`808x.State.cs`, `386.State.cs`) et en tête
  d'`iXtal26.Diff`. Le dépôt compile sans paliers (`TieredCompilation=false`) : chaque méthode est compilée une seule
  fois, après le gel, et en Release le JIT plie chaque garde en constante. Le mode PCem ne coûte rien, et le listing
  du JIT le prouve. Un changement de demande après le gel est refusé bruyamment ; une clé absente ne demande rien. Les
  sites atteints AVANT le gel (PB-104 dans `loadconfig`, les autres à inventorier) lisent la demande, jamais le champ
  figé. `Unsafe.AsRef` est interdit (une règle greppable). Des champs, jamais un tableau : un `static readonly bool[]`
  ne se plie pas.
- **Des tables recopiées** là où PCem aiguille par table. Le x87 et la table du 386 sont des fichiers GÉNÉRÉS
  (`tools/x87gen`, `ops386-table.py`), où aucune garde ne s'écrit à la main : `cpu_set` pose les tables du mode. Le
  8087 indexe ses tables en dur dans `execx86` : ses entrées se remplacent en place au montage, ou par une DEVIATION.
- **La règle de placement des gardes.** La garde lit le champ directement, sans locale ni propriété ; elle se tient au
  niveau le plus froid possible (par tampon, par ligne, par commande ; jamais par échantillon ni par pixel) ; jamais
  dans une méthode que le JIT inline aujourd'hui sans `AggressiveInlining` ; jamais dans un fichier généré. Dans
  `execx86` et `exec386`, seulement une garde et un appel vers un fichier `*.Materiel.cs`, sans aucune locale : ces
  méthodes sont près du budget de locales de l'inliner (922), que `VERIFICATION.md` documente.
- **La forme.** Une clé `hardware_mode = 0|1` (le nom : décision n° 3) ; `--hardware-mode LISTE`, valeur obligatoire
  (`tout`, des domaines, des `PB-nn`) ; une ligne à l'écran de construction ; « materiel » dans la barre de titre et
  les bannières de `--boot`, `--timer-check` et `--verbose`. Aucune variable d'environnement : `par.sh` les transmet
  toutes aux portes. Il refusera toute `IXTAL26_*` hors d'une liste blanche, et toute `DOTNET_Jit*`, `DOTNET_Tiered*`
  ou `DOTNET_ReadyToRun`.
- **Les outils.** Une liste d'acceptation, commande par commande : les 48 d'`iXtal26.Diff` et les vérifications
  d'`iXtal26`. boot-diff, les fuzzeurs et toute comparaison à l'oracle refusent le mode (retour 2), hors le contrôle de
  fuite déclaré. `sst-probe` et `sst386-probe` l'acceptent sous `--target csharp`, avec leur ligne de base à part
  (`sst-baseline-materiel.tsv`). Les `r9-*` se relisent un à un : trois n'atteignent leur garde que par un défaut de
  PCem (`r9-atapi` par PB-119, `r9-zip` par PB-126, `r9-aha` par PB-132) et demandent un autre scénario en mode
  matériel. `--joystick-check` affirme le comportement reproduit de PB-104 : un verdict par mode.
- **Les règles.** R10 (le mode matériel), et les amendements de R1 (c, d), de R2 (la commande de décompte retire les
  seules lignes de garde, par un motif ancré), de R6 (un `*.Materiel.cs` n'a pas d'oracle), de R8 et de la Portée ;
  l'en-tête de `PCEM_BUGS.md` ; le statut `materiel` d'`oracle.tsv`. `TRANSCRIPTION.md` est à 218 lignes sur 240 :
  il faudra un relèvement inscrit de R3 (décision n° 15). R10 ne nomme pas la classe, que G14 et G15 renommeront.

## La vérification

- **Le mode PCem n'a pas bougé.** À chaque étape qui change l'émulateur : la série entière, verdict par verdict,
  contre la précédente ; les listings du JIT (`DOTNET_JitDisasm=*`, Release, par scénario), identiques à la référence
  M0 prise avant G13, chaque méthode gardée présente dans au moins un scénario ; `ops-count` identique ; SST qui rend
  `sst-baseline.tsv` à l'identique.
- **Le mode matériel est juste.** Pour chaque PB, un cas en C# seul qui rend la valeur de PCem en mode PCem et celle
  de la documentation en mode matériel, la phrase de la source à côté de l'attendu, et sa panne injectée (la
  correction coupée par le masque) qui le rougit. SST en mode matériel : les formes visées montent, aucune autre ne
  bouge. Les bancs dirigés (SBBANC, SB16BANC, AWEBANC, AHABANC, ATAPIBANC, ZIPBANC, X87BANC…) en C# seul, avec un
  second jeu d'attendus sourcés, par un exécuteur de bancs en C# seul (frappes injectées, relevé comparé à un fichier
  d'attendus).
- **Le contrôle de fuite.** Le fuzzeur et boot-diff, sous une option dédiée, avec un seul PB corrigé : toute
  divergence avec l'oracle doit tomber dans le périmètre déclaré, et au moins une doit s'y produire.
- **Le déterminisme.** Chaque scénario du mode matériel joué deux fois, aux mêmes empreintes.
- **Une sonde du mode.** Un compteur par PB (un `int[]`), incrémenté dans le bloc matériel : quelles corrections un
  scénario exerce, et la couverture par PB.
- **Le coût.** M0, les listings d'avant G13 ; M1, le coût d'une garde au banc `tools/perfbanc`, sur le patron
  d'`Is486Banc` ; M2, les listings après chaque étape ; M3 et M4, `bench` et `--timer-check` (chaque machine garde
  une marge supérieure à 1 en mode matériel).
- **Une section G13 dans `series.sh`.** Le recensement (chaque PB a ses sites, chaque site son numéro, aucune
  configuration de porte ne porte la clé, les `.cfg.in` compris) ; les refus ; SST dans les deux modes (une porte qui
  ne trouve pas ses vecteurs est rouge, jamais muette) ; les cas par domaine ; le déterminisme ; les contrôles de
  fuite.

## Les étapes

Une série entière par étape qui change l'émulateur, une seule à la fois ; un sous-ensemble ciblé sinon ; un commit par
étape dès sa série verte. Estimation : une vingtaine de séries entières pour tout G13.

### G13.0 — Ce qui n'attend pas : R9 et les fichiers de l'utilisateur, dans les deux modes  ✅ *fait, VERIFICATION.md § G13.0*

- PB-09 : le `charbuffer` de 512 octets ; `r9-cga` (R1 = 129, 200 et 255 ; 3D8h = 01h puis 29h) et `r9-m24`.
- PB-17 et ses deux voisins : la lecture bornée (99 840 octets par piste au plus) ou l'image refusée avec un
  avertissement ; la division par zéro ; le fichier vide (une faute de transcription) ; un `r9-*` par cas.
- PB-121 : fermer et vider le flux d'avant au reset matériel (décision n° 2).
- Le filet de l'hôte et PB-33 : vider les images sur toute sortie anormale ; `savenvr` qui ne lève plus
  (décision n° 2).
- Le registre suit : PB-09 et PB-17 en « NON reproduit (R9) », PB-121 en section A, les voisins inscrits.
- **Vérification** : les `r9-*` neufs, chaque garde atteinte et tout survivant ; un essai du reset SCSI en C# seul
  (une copie, un reset, une recopie, la sortie : l'image garde la seconde copie) ; la série entière, identique à g126
  hors les portes neuves (aucune porte n'atteint ces chemins).

### G13.1 — Le registre et le recensement (aucun comportement ne change)

- Une seule méthode de recensement : le numéro `PB-nn` sur la ligne du marqueur. Les 29 PB sans numéro le reçoivent,
  les sept sans marqueur en reçoivent un ; dans les fichiers générés, par leurs générateurs, rejoués à l'octet près.
- Les entrées fausses corrigées, sources citées ; PLAN.md § G13 recompté.
- Les défauts voisins vérifiés inscrits (des numéros neufs, décision n° 1), chacun lu à la ligne.
- Chaque entrée des sections A et B reçoit sa source, son cas qui discrimine et sa panne : c'est la matière des
  rapports de `iXtal26/Docs/G13-reconnaissance/`.
- **Vérification** : un sous-ensemble ciblé (seuls des commentaires changent : construction sans avertissement, R2,
  `check-oracle`, les générateurs rejoués) ; la porte du recensement.

### G13.2 — Le mécanisme, et un PB pilote

- M0, avant toute garde. Puis la classe, le gel, les refus, la clé, l'option, l'écran (`--setup-check`), la barre de
  titre, la liste d'acceptation, la défense de `par.sh`, la sonde du mode, l'exécuteur de bancs en C# seul, R10 et
  ses amendements.
- Les vecteurs SST (décision n° 5) : les formes manquantes du 8088 (D1.2, D1.3, D3.2, D3.3, F6.7…), le masque des
  drapeaux indéfinis appliqué par `sst386-probe`, le corpus du 8086 ; le lecteur du corpus du 286 réparé (`Moo.cs`
  lève sur `REGS`).
- Le pilote : PB-01, l'AF d'ADC et de SBB du 8088, le seul PB que SST discrimine aujourd'hui (douze formes). Il
  éprouve la chaîne entière : la garde, le gel, le listing inchangé, SST en mode matériel, la panne injectée, le
  contrôle de fuite.
- **Vérification** : la série entière identique ; M1 et M2 ; les refus prouvés (boot-diff en mode matériel rend 2) ;
  `sst-probe` en mode matériel, les douze formes de PB-01 à 10 000 sur 10 000, aucune autre ne bougeant.

### G13.3 — Le 8088 et le 8086

- PB-02 ; PB-45 avec le débordement de DIV et d'IDIV ; PB-87 élargi au fetch principal (priorité basse : chemin
  chaud, effet rare) ; PB-03 et PB-07 sur le 5150 et l'XT (ils sont dans `808x.cs` et se mesurent avec les outils de
  l'UC) ; les familles inscrites en G13.1 (DAA et DAS, REP LODS, SETMO, l'OF de SHL, AAM et AAD, SAR, `rep()`).
- **Vérification** : SST 8088 et 8086 en mode matériel ; `--timer-check` (PB-03 : aucun cycle hors du compteur) ; le
  coût de `FETCH` (M1) ; une série.

### G13.4 — La carte mère

- Les ports et l'hôte (PB-103, PB-101 ; PB-104 et PB-33 selon la décision n° 9) ; le 8237 selon la fiche (PB-157 et
  ses voisins inscrits) ; le 8259A selon la fiche (PB-05, avec la cascade et le masque de service ; PB-06 et PB-13
  absorbés) ; la souris PS/2 (PB-94, PB-95).
- Avant le x87 et le son, parce que leurs IRQ (IRQ13, la ligne partagée de la SB 16) passent par le PIC.
- **Vérification** : PICBANC et DMABANC (à écrire, aux attendus de la fiche et de l'AT TR, dans les deux modes) ; les
  POST de toutes les machines ; `--joystick-check` ; PS2BANC ; deux ou trois séries.

### G13.5 — Le 286, le 386 et le 486

- Le décodage et la longueur (PB-43 avec PB-44, PB-50, PB-78 ; LOCK → #UD, gardé par UC, puisque le 286 partage
  `opLOCK` ; MOVSX r16) ; le fetch borné (PB-51 : chaque lecture d'instruction, en mode matériel) ; le mode protégé
  selon la documentation (PB-39, PB-40, PB-32 ; LTR contrôlé) ; les familles du 386 inscrites (l'AF d'ADC, BT*, AAA et
  AAS, AAD, DAS). PB-41 et le NT d'un JMP de tâche, où les manuels se contredisent, restent reproduits.
- **Vérification** : SST 386 (masque appliqué) et 286 en mode matériel ; `pm-check` avec une table d'attentes du mode
  matériel tirée de la documentation ; le coût de PB-51 ; deux séries.

### G13.6 — Le x87

- Le cadre : des tables du mode posées par `cpu_set`, d'abord copies conformes (le fuzzeur doit rester vert) ; le
  harnais `x87hw-cases`, en C# seul.
- Les PB qui ne touchent qu'une poignée de gestionnaires : PB-61 (FNSTSW AX), 57, 58 et la part « comparaisons » de
  70, 64, 63, 66, 67. Et l'acheminement des exceptions (PB-69 ; IRQ13 et le verrou de l'AT, F0h et F1h ; FERR# et #MF
  du 486), avec ZE seule.
- Puis un point de décision (n° 10) : un noyau de 80 bits (SoftFloat 3e, licence BSD-3, vérifié par TestFloat-3e),
  d'abord hors machine, et mesuré. S'il tient, les gestionnaires du 387 et du 486, les exceptions démasquées et les
  coprocesseurs de 16 bits suivent : 10 000 à 20 000 lignes, plus que G4 entier. Sinon, un repli : 80 bits pour
  stocker, `double` pour calculer. Jamais les noyaux transcendants de Bochs : leur licence (SoftFloat 2b) est
  incompatible avec la GPL-2.0-only.
- **Vérification** : `x87hw-cases`, aux attentes du 387 PRM (annexe C), du 287 PRM, du Numerics Supplement et de
  l'AP-578 ; le domaine d'accord contre l'oracle (opérandes normaux, PC à 53 bits, arrondi au plus près, PE et C1
  exclus) ; MSD « 80287 » sur un 286 avec 287 ; une série par sous-étape.

### G13.7 — Le stockage

- La disquette et le MFM (PB-14, 22, 23, 25 ; PB-19 sur ses deux sites ; les `fatal()` de protocole du Xebec, enfin
  traitables par la documentation d'IBM, sous R9) ; l'IDE (PB-71, 74, et `ide_irq_update`) ; le moteur d'images CD
  (PB-106, 108, 109, 122) ; l'ATAPI et l'audio (PB-117, 119, 120, 123 et ses voisins) ; le ZIP (PB-126 hors START STOP
  UNIT) ; le disque SCSI (PB-129 à 131, 132 côté cible) ; l'AHA-1542C (PB-132 côté carte, 133, 137, 139 à 142, 144 ;
  136 et 138 dans leurs bornes).
- Restent reproduits faute de vérité : PB-28, 72 et 143, START STOP UNIT, FORMAT UNIT, la réentrance de PB-142.
- **Vérification** : des cas `hw-*` en C# seul, sur le patron des `r9-*`, chaque attendu sourcé à sa norme ; les
  bancs (ATAPIBANC, ZIPBANC, AHABANC) en C# seul, avec leurs attendus du mode matériel ; des témoins inter-modes (les
  mêmes images formatées dans les deux modes, identiques) ; trois ou quatre séries.

### G13.8 — Le son

- Le DSP 8 bits et l'ADPCM (PB-90, 91 ; 146 et l'octet de référence ; la coupure de PB-147 ; 149 ; l'état du DSP au
  reset ; 08h de la SB 16) ; le CT1745, le MPU-401 et la ligne d'IRQ partagée (PB-153, 154 ; 145 pour la 2.0 sans
  mélangeur) ; l'enregistrement (PB-148, 151, 156) ; l'EMU8000 (PB-158 à 160, 162, 163) ; la clôture (PB-21, 155).
- **Vérification** : des cas en C# seul aux valeurs du guide de Creative et des micrologiciels, chacun avec sa page ;
  SBBANC, SB16BANC et AWEBANC en C# seul (le scénario de PB-146 corrigé : une commande à paramètre intercalée) ; quatre
  ou cinq séries.

### G13.9 — La vidéo

- Le socle SVGA (PB-80 et le chemin rapide, PB-37, `htotal`) ; la MDA et l'Hercules (PB-102, 97, 96, le curseur
  plein, les rythmes de clignotement, 3BAh) ; l'EGA (PB-99, hors la valeur des registres en écriture seule) ;
  l'ET4000 (les parts documentées de PB-100) ; la CGA, la M24 et le PC1512 (PB-04, PB-89).
- PB-35 et la valeur des registres en écriture seule restent reproduits, avec leur protocole de mesure.
- **Vérification** : des cas en C# seul (les périodes de balayage contre IBM et Hercules, les masques contre la fiche
  du 6845, les rangées du curseur) ; les bancs (EGABANC, ET4BANC, BLTBANC, HERCBANC) en C# seul ; les images quand
  GR.0 les aura ; `--menu-check` resserré ; deux séries.

### G13.10 — La clôture

- Les profils du mode matériel (le 486 de référence, le 5150) dans `launchSettings.json` ; `--setup-check` ; les
  témoins du mode matériel par machine (POST, `VER`, `MSD /S`, Windows sur l'ami486) ; la couverture comptée (la
  sonde du mode) ; `PCEM_BUGS.md` (un champ *Corrigé en mode matériel* par entrée) ; `VERIFICATION.md` ; PLAN.md ;
  les originaux intacts.
- **Vérification** : la série entière dans les deux modes ; les listings contre M0.

## Les décisions (proposées ; à trancher)

1. **Le principe et la portée.** Le principe ci-dessus. La portée : les PB des sections A et B, plus les défauts
   voisins que les contre-lectures ont vérifiés, inscrits d'abord (G13.1). Les drapeaux indéfinis du 386 et les temps
   (PB-11, PB-12, les temps de commutation de tâche) restent hors du mode. *Validé le 07/10.*
2. **G13.0 tout de suite, dans les deux modes.** PB-09 et PB-17 sous R9, puisque la règle existe ; PB-121 et le filet
   de l'hôte (vider les images sur toute sortie anormale, `savenvr` sans exception), qui protègent les fichiers de
   l'utilisateur sans relever de R9. *Validé le 07/10 : les quatre.*
3. **Le nom** : `hardware_mode` et `--hardware-mode`, l'anglais des clés de PCem et de `zip_path` ; ou `materiel`.
   *Ouvert, à trancher avant G13.2.*
4. **Le mécanisme** : des `static readonly` figés et des tables recopiées, un interrupteur pour l'utilisateur et un
   masque par PB pour les outils ; la clé dans le `.cfg`, que boot-diff refuse. *Validé le 07/10.*
5. **Les données extérieures** : récupérer les vecteurs SST (les formes manquantes du 8088, le corpus du 386, celui du
   8086 ; celui du 286 après réparation du lecteur). Des données de test publiques, sans installation. *Validé le 07/10.*
6. **Le banc `tools/perfbanc/`**, non commité, avec la ligne `InternalsVisibleTo` d'`iXtal26.csproj` : le commiter en
   G13.2 pour M1. *Ouvert, à trancher avant G13.2.*
7. **La règle du 0 %** : elle vaut pour le mode PCem pendant G13, prouvée par les listings contre M0 ; le mode
   matériel garde une marge supérieure à 1 au `--timer-check`. *Validé le 07/10.*
8. **Les sources** : les micrologiciels désassemblés des DSP et les octets des ROM du dépôt comptent comme
   documentés pour leur puce ; les autres émulateurs (86Box, DOSBox-X, Bochs, MAME) ne sont que des indices.
   *Validé le 07/10.*
9. **Les défauts de l'hôte sans pendant matériel** (PB-104, 33, 16, 34) : une seule règle, corrigés dans les deux
   modes s'ils protègent les fichiers ou l'hôte, laissés sinon. *Validé le 07/10.*
10. **Le x87** : le cadre, les PB à peu de gestionnaires et l'acheminement dans G13 ; puis un point de décision sur le
    noyau de 80 bits, mesuré hors machine ; le reste du x87 (les gestionnaires du 387 et du 486, les exceptions
    démasquées, le 8087 et le 287) en sous-bloc s'il tient. *Validé le 07/10.*
11. **L'ordre des domaines** : celui des étapes ci-dessus — le 8088 avec PB-03 et PB-07, puis la carte mère avant le
    x87 et le son, dont les IRQ passent par le PIC — au lieu de celui de PLAN.md (l'UC et le x87, les disques, la
    vidéo, le son, la carte mère). *Validé le 07/10.*
12. **La vidéo** dans G13, vérifiée en C# seul ; les images viendront avec GR.0. GR reste après G13, comme dans
    PLAN.md. *Validé le 07/10.*
13. **L'état au reset en mode matériel** : l'état documenté (le haut-parleur coupé, les volumes de reset de Creative,
    l'EMU8000 muet tant qu'un pilote ne l'a pas initialisé), et non celui que laissent les utilitaires. *Validé le 07/10.*
14. **Les comportements inconnus** restent reproduits. As-tu du matériel réel pour mesurer (8087, 287, 387, un 386,
    une SB, un 8237…) ? Une mesure trancherait une vingtaine de points. *Ouvert.*
15. **`TRANSCRIPTION.md`** : un relèvement inscrit de R3, de 240 à 260 lignes, pour R10 et ses amendements. *Validé le 07/10.*
16. **Les rapports de reconnaissance** : commités avec ce plan, dans `iXtal26/Docs/G13-reconnaissance/`. *Validé le 07/10.*

## Les risques

- **L'ampleur.** G13 est le plus gros bloc du plan : une vingtaine de séries entières, et le x87 de 80 bits pèse à
  lui seul plus que G4. Le registre lui-même grossit d'une soixantaine de défauts voisins.
- **La fuite dans le mode PCem** : une aide partagée retouchée, un état statique commun (`sb_commands`, les tables),
  un générateur qui ne se rejoue plus à l'octet près. La série, les listings et le recensement la cherchent.
- **Le JIT.** Le pliage des `static readonly` est un comportement d'implémentation, pas un contrat. Il n'existe ni en
  Debug (un lancement Debug de Rider paie chaque garde), ni en ReadyToRun (G17). Les listings le surveillent ; une
  garde mal pliée coûte de la vitesse, jamais une valeur.
- **Les sources qui se contredisent** : le 386 PRM et le SDM sur AAA, #TS ou #GP, le NT d'un JMP de tâche ; ATA-3 ;
  le 486 sur l'abandon ou #GP d'une instruction x87. Faute de silicium, ces points restent reproduits. (Les constantes
  du 8087 et du 287 ne le sont plus : quatre textes d'Intel contre la seule annexe C du 387 PRM, on retient la valeur
  au plus près.)
- **Les témoins rares.** Ni logiciel SB 16 ni AWE32, ni IEEETEST ni MCPDIAG : le mode matériel du son et du x87 n'a
  d'abord pour témoins que nos bancs.
- **Les licences.** SoftFloat 3e et TestFloat (BSD-3) sont compatibles ; les extensions de Bochs (SoftFloat 2b) ne le
  sont pas ; les manuels lus ne sont pas recopiés dans le dépôt.
- **Le coût du mode matériel lui-même** : PB-51 ajoute un contrôle à chaque lecture d'instruction, et un x87 de 80
  bits sera plus lent. Mesuré (M4), jamais supposé.
- **Les fichiers entre deux modes** : une image formatée en mode PCem peut porter la trace d'un défaut (le Xebec, le
  disque SCSI d'un secteur de trop, la capacité du ZIP), et l'EEPROM de l'AHA peut s'écrire autrement. À documenter
  pour l'utilisateur.
