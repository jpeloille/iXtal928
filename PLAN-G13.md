# G13 — Corriger les défauts de PCem reproduits : le mode matériel ; le plan, sur reconnaissance

> Écrit le 6 octobre 2026. Bloc G13 de `PLAN.md` (décision utilisateur du 03/10). Feu vert le 06/10, « commence par
> le plan ». **Les décisions de la fin du fichier sont validées par l'utilisateur le 07/10** (« d'accord avec les
> propositions, commence par G13.0 puis attends pour G13.1 ») ; les n° 3, 6 et 14 le sont aussi, le même jour. Chaque constat cite la ligne
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

### G13.1 — Le registre et le recensement (aucun comportement ne change)  ✅ *fait, VERIFICATION.md § G13.1*

- Une seule méthode de recensement : le numéro `PB-nn` sur la ligne du marqueur. Les 29 PB sans numéro le reçoivent,
  les sept sans marqueur en reçoivent un ; dans les fichiers générés, par leurs générateurs, rejoués à l'octet près.
- Les entrées fausses corrigées, sources citées ; PLAN.md § G13 recompté.
- Les défauts voisins vérifiés inscrits (des numéros neufs, décision n° 1), chacun lu à la ligne.
- Chaque entrée des sections A et B reçoit sa source, son cas qui discrimine et sa panne : c'est la matière des
  rapports de `iXtal26/Docs/G13-reconnaissance/`.
- **Vérification** : un sous-ensemble ciblé (seuls des commentaires changent : construction sans avertissement, R2,
  `check-oracle`, les générateurs rejoués) ; la porte du recensement.

### G13.2 — Le mécanisme, et un PB pilote  ✅ *fait, VERIFICATION.md § G13.2*

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

### G13.3 — Le 8088 et le 8086  ✅ *fait, VERIFICATION.md § G13.3 ; suite le 09/10, § G13.3, suite*

- PB-02 ; PB-45 avec le débordement de DIV et d'IDIV ; PB-87 élargi au fetch principal (priorité basse : chemin
  chaud, effet rare) ; PB-03 et PB-07 sur le 5150 et l'XT (ils sont dans `808x.cs` et se mesurent avec les outils de
  l'UC) ; les familles inscrites en G13.1 (DAA et DAS, REP LODS, SETMO, l'OF de SHL, AAM et AAD, SAR, `rep()`).
- **Vérification** : SST 8088 et 8086 en mode matériel ; `--timer-check` (PB-03 : aucun cycle hors du compteur) ; le
  coût de `FETCH` (M1) ; une série.
- **Corrigé** (09/10, décision de Julien, « on corrige d'abord ») : le temps d'un REP devant autre chose qu'une
  chaîne. Le mode matériel gardait le prix de PCem, 20 cycles et la file vidée, 24,5 ± 1,5 cycles de trop devant IDIV
  au 8088 ; il facture maintenant le prix d'un préfixe, la file intacte : écart −0,2 ± 2,1 cycles contre le silicium,
  file de départ vide (`sst-rep-temps`, porte `sst-rep-temps-materiel`).

### G13.4 — La carte mère  ✅ *(09/10 : trois sous-étapes, chacune sa série et son commit)*

- Les ports et l'hôte (PB-103, PB-101 ; PB-104 et PB-33 selon la décision n° 9) ; le 8237 selon la fiche (PB-157 et
  ses voisins inscrits) ; le 8259A selon la fiche (PB-05, avec la cascade et le masque de service ; PB-06 et PB-13
  absorbés) ; la souris PS/2 (PB-94, PB-95).
- Avant le x87 et le son, parce que leurs IRQ (IRQ13, la ligne partagée de la SB 16) passent par le PIC.
- **Vérification** : PICBANC et DMABANC (à écrire, aux attendus de la fiche et de l'AT TR, dans les deux modes) ; les
  POST de toutes les machines ; `--joystick-check` ; PS2BANC ; deux ou trois séries.
- **G13.4a, le 8259** ✅ *(09/10, VERIFICATION.md § G13.4a)* : PB-05, PB-246, PB-247, PB-248, PB-255 corrigés en
  mode matériel (PB-06 et PB-13 absorbés) ; PICBANC, XT et AT, dans les deux modes ; série g4a1, 307 portes vertes.
- **G13.4b, le 8237** ✅ *(09/10, VERIFICATION.md § G13.4b)* : PB-157, PB-249, PB-250, PB-251, PB-252, PB-253
  corrigés en mode matériel (PB-259 et PB-260 inscrits, la part qui reste reproduite) ; DMABANC, XT et AT, dans les
  deux modes ; série g4b1, 325 portes vertes.
- **G13.4c, le 8042, la souris PS/2 et les ports** ✅ *(09/10, VERIFICATION.md § G13.4c)* : PB-254, PB-94, PB-95,
  PB-261 (inscrit), PB-101, PB-103 corrigés en mode matériel (PB-104 reproduit selon la décision n° 9, PB-33 fait en
  G13.0 ; PB-256 laissé reproduit, question à Julien) ; PS2BANC et JOYBANC dans les deux modes, `--joystick-check` ;
  série g4c1, 345 portes vertes.

### G13.5 — Le 286, le 386 et le 486  ✅ *fait : quatre sous-étapes, chacune sa série et son commit*

- Avant les corrections : la carte plate de 16 Mo du cœur 286, des deux côtés (`h_flat16`, `FlatMap286`) ; 17 % du
  corpus SST du 286 tombe aujourd'hui hors de sa carte de 1 Mo (VERIFICATION.md § G13.2).
- Le décodage et la longueur (PB-43 avec PB-44, PB-50, PB-78 ; LOCK → #UD, gardé par UC, puisque le 286 partage
  `opLOCK` ; MOVSX r16) ; le fetch borné (PB-51 : chaque lecture d'instruction, en mode matériel) ; le mode protégé
  selon la documentation (PB-39, PB-40, PB-32 ; LTR contrôlé) ; les familles du 386 inscrites (l'AF d'ADC, BT*, AAA et
  AAS, AAD, DAS). PB-41 et le NT d'un JMP de tâche, où les manuels se contredisent, restent reproduits.
- **Vérification** : SST 386 (masque appliqué) et 286 en mode matériel ; `pm-check` avec une table d'attentes du mode
  matériel tirée de la documentation ; le coût de PB-51 ; deux séries.
- **G13.5a, la carte de 16 Mo du 286 et les familles que SST mesure** ✅ *(10/10, VERIFICATION.md § G13.5a)* : l'AF d'ADC
  (PB-181), LOCK (PB-182), BT* (PB-183, PB-262), MOVSX r16 (PB-184), AAA et AAS (PB-185), AAD et AAM (PB-186), AAM 0
  (PB-187), DAS (PB-188) ; SST 386 et 286 en mode matériel, en entier puis en portes de 150 cas par forme ; le scénario
  `ami386dx-dos` des listings.
- **G13.5b, le décodage, la longueur et le fetch borné** ✅ *(10/10, VERIFICATION.md § G13.5b)* : PB-43 avec PB-44,
  PB-50 et PB-51 en un groupe (un décodeur de longueur, son coût au banc `Exec386Banc`), PB-78 ; le scénario
  `ami386dx-ctrl` des listings.
- **G13.5c, le mode protégé** ✅ *(10/10, VERIFICATION.md § G13.5c)* : PB-32 avec PB-192, PB-39, PB-40, PB-190 (LTR),
  PB-191, PB-193 (le privilège de LOADALL386, dont le cas se joue à CPL 3, dans pm-check), PB-263 (inscrit : l'IP d'un
  CALL de tâche) ; les exceptions du manuel de chaque processeur (#TS par CALL et INT sur le 386 et le 486, #GP sur le
  286 ; décision de Julien du 10/10) ; `pm-check --target csharp` et ses tables du mode matériel, au 286, au 386 et au
  486, qui pose le décor du 486 autrement que par LOADALL386 (PB-78 corrigé) ; le scénario `ami386dx-pm` des listings.
  Le NT d'un JMP de tâche suit déjà le 386 PRM. PB-41 reste reproduit (aucun manuel ne donne la valeur) ; PB-189 (la
  limite des données en mode réel) suit, en G13.5d.
- **G13.5d, la limite des données** ✅ *(10/10, VERIFICATION.md § G13.5d)* : PB-189 (en mode réel, un opérande au-delà de FFFFh ou à cheval sur FFFFh lève
  #GP, ou #SS pour la pile), demandé par Julien le 10/10 ; un contrôle sur chaque accès mémoire, le chemin le plus chaud
  du cœur : son coût mesuré (M1) en mode matériel, nul en mode PCem (M2) ; SST 386, familles E2 et E3.

### G13.6 — Le x87  *(6a, 6b et 6c faits ; point de décision n° 10 : étape 1 faite, en attente)*

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
- **Point n° 10, étape 1, la réécriture et la mesure** ✅ *(11/10, VERIFICATION.md § G13, point de décision n° 10)* :
  SoftFloat 3e réécrit en C# (alors `tools/softfloat/` ; la bibliothèque est dans `iXtal26.SoftFloat/` depuis N1), le
  sous-ensemble 80 bits en variante 8086, hors de l'émulateur ; TestFloat 3e au bit près, niveaux 1 et 2 (9,6
  milliards de cas, aucun écart) ; FADD à 10,7 ns. **Intégration décidée par Julien le 11/10** (« feu vert pour les
  deux » : le sous-bloc, et C1 noté par l'arrondi) ; le plan découpé, § G13.6, le noyau, validé le même jour.
- **G13.6a, le cadre et les PB à peu de gestionnaires** ✅ *(10/10, VERIFICATION.md § G13.6a)* : les tables du mode,
  copies de celles de PCem posées par `cpu_set` (`Cpu/x87.Materiel.cs`), le 8087 en place (`Cpu/808x.Materiel.cs`) ;
  PB-61 (le pilote), PB-57, PB-58, PB-64, PB-70 (un groupe : les comparaisons ; l'hypothèse A6 instruite), PB-63,
  PB-66, PB-67 ; les cas dans `materiel-cas` (le harnais `x87hw-cases` prévu y est fondu) ; le scénario `ami386dx-x87`
  des listings. IE n'est pas posé (avec le noyau, PB-59).
- **G13.6b, les deux PB de gestionnaires restants** ✅ *(10/10, VERIFICATION.md § G13.6b)* : PB-213 (les comparaisons
  remettent C1 à zéro : 287XL, 387, 486), oublié de 6a, et PB-207 (FSTENV masque les exceptions), par enveloppes posées
  dans les tables du mode, chaque gestionnaire repéré par son nom.
- **G13.6c, l'acheminement** ✅ *(10/10, VERIFICATION.md § G13.6c)* : PB-59, PB-69 et PB-204 en un groupe, ZE seule.
  ES et B posés sur le front de l'exception ; le 8087 lève la NMI que le port A0h masque (les deux `nmi_mask` de la
  transcription reliés en mode matériel), prise sur un front ; le 287 et le 387, IRQ13 et le verrou de l'AT, que F0h
  et F1h effacent (F1h remet aussi le coprocesseur à zéro) ; le 486, FERR# vers IRQ13, #MF si NE, sinon l'arrêt
  jusqu'à IGNNE#. Chaque instruction qui attend est enveloppée dans les tables du mode (toutes sauf FNCLEX, FNINIT,
  FSETPM, FNSTCW, FNSTSW, FNSAVE et FNSTENV), WAIT par une garde. Les cycles de l'instruction démasquée restent ceux
  de PCem (inconnus ; avec le noyau). Le scénario `ami386dx-x87div` des listings.
- **Vérification** : `x87hw-cases`, aux attentes du 387 PRM (annexe C), du 287 PRM, du Numerics Supplement et de
  l'AP-578 ; le domaine d'accord contre l'oracle (opérandes normaux, PC à 53 bits, arrondi au plus près, PE et C1
  exclus) ; MSD « 80287 » sur un 286 avec 287 ; une série par sous-étape.

### G13.6, le noyau — le sous-bloc du point n° 10  *(proposé et validé le 11/10)*

*Validé par Julien le 11/10 (« feu vert pour les six »)* : (1) la bibliothèque dans `iXtal26.SoftFloat/`, projet frère ;
(2) l'écart C1 à chaque site d'arrondi ; (3) l'arrondi à exposant non borné pour OE et UE démasquées, en N5 ; (4) un
seul groupe pour le noyau, qui n'entre dans « tout » qu'à la fin de N7, et les gestionnaires en `double` de 6a et 6b
retirés en N8 ; (5) le débordement de pile du x87 inscrit au registre en N1 ; (6) le fuzzeur en mode matériel sur le
domaine d'accord, exception déclarée, dès N4.

Le découpage suit la voie B de la reconnaissance (D2 § 8.4, corrigée par D2-contre § 5) : B1 est fait (le point n° 10,
étape 1) ; B2 se coupe en trois ; B4 n'emprunte rien à l'hôte. Chaque étape a ses cas en C# seul, ses contrôles
négatifs, sa contre-lecture, sa série entière (l'émulateur change) et son commit ; le mode PCem reste identique à M0
(M2), à chaque étape.

**Ce qui commande le découpage.** Le noyau change la représentation : en mode matériel, la pile devient huit registres
de 80 bits, à côté des `double` de PCem, que le mode PCem garde. Un gestionnaire du noyau ne peut pas cohabiter avec
un gestionnaire de PCem dans la même machine : le premier écrit des registres que le second ne lit pas. Le noyau
n'entre donc au mode, pour l'utilisateur, qu'une fois complet ; d'ici là, il ne se demande que par sa liste de PB,
pour les cas et les portes. Et R10 tranche une question que D2-contre (A2) laissait ouverte : le code du mode est
déterministe, « sans fonction de l'hôte à résultat variable » ; les transcendantes ne peuvent pas passer par
`Math.Sin` et ses voisins, elles s'écrivent sur le noyau.

- **N1 — Le noyau dans le produit** ✅ *(11/10, VERIFICATION.md § G13.6, le noyau, N1)* (aucun comportement ne
  change). La bibliothèque quitte `tools/` pour
  `iXtal26.SoftFloat/`, un projet frère d'`iXtal26` qui le référence (comme le banc) ; sa licence reste dans son
  répertoire. L'écart C1 (validé) : SoftFloat note s'il a arrondi vers le haut, dans une globale à côté de
  `softfloat_exceptionFlags`, à chaque site d'arrondi — `softfloat_roundPackToExtF80`, `…ToF32`, `…ToF64`,
  `softfloat_roundToI32`, `…ToI64`, `extF80_roundToInt` (FST m32 et m64, FIST et FRNDINT posent C1 comme FADD) ;
  chaque site le dit en tête de fichier. Le banc vérifie C1 sur tous les cas de TestFloat contre sa définition (le
  résultat diffère de celui qu'arrondit vers zéro) ; TestFloat repasse en entier. Le registre : le débordement de
  pile du x87 (empiler sur un registre plein, lire un registre vide : IE, et SF et C1 sur le 387 et le 486), que
  PCem ignore et qu'aucune entrée ne porte, inscrit avec ses marqueurs. ≈ 300 lignes.
- **N2 — L'état de 80 bits et le générateur** ✅ *(11/10, VERIFICATION.md § G13.6, le noyau, N2)* (aucun
  comportement ne change pour l'utilisateur ; PB-203 et PB-264 corrigés en chantier). L'état du mode (huit registres
  `extFloat80_t`, le mot d'étiquettes de l'architecture, FIP, FCS, FOP, FDP, FDS) ; l'état de reset par type
  (PB-203) ; une couche sémantique commune aux cœurs (`x87_noyau.Materiel.cs` : la classe de chaque opérande —
  normal, dénormal, non normal, pseudo-dénormal, pseudo-infini, NaN —, la pile et son débordement, la réponse
  masquée de chaque exception, les indicateurs et C1 vers `npxs`, PC et RC vers SoftFloat) ; un générateur
  (`tools/x87gen/`) qui rend, d'une seule description, les gestionnaires du noyau pour le 286, le 386 et le 486
  (a16, a32) et pour le 8087 (a16, l'adressage de PB-201), et les tables du mode — une instanciation partagée à la
  source (D2-contre K1-K2), deux à la sortie, sans générique ni interface (R4). Le groupe du noyau (`Groupes`) réunit
  tous les PB du x87 qu'il touche. ≈ 1 000 à 1 500 lignes, dont la moitié générée.
- **N3 — Les mouvements de données et le contrôle.** Coupé en deux : **N3a** ✅ *(11/10, VERIFICATION.md § G13.6, le
  noyau, N3a)*, les chargements, les stockages, les constantes et les opérations de registre (PB-52, 55, 56, 195,
  196, 206, 212) ; **N3b** ✅ *(11/10, VERIFICATION.md § G13.6, le noyau, N3b)*, l'environnement, les pointeurs, les
  étiquettes et les contrôles de limite (PB-54, 62, 199, 208 ; PB-207, corrigé en 6b, que le noyau reprend, et PB-63,
  66, 67 de même, n'entrent au groupe qu'en N7). FLD, FST, FSTP m32, m64 et m80 ; FILD, FIST, FISTP m16, m32,
  m64 ; FBLD, FBSTP ; les constantes ; FXCH, FST et FSTP registre, FFREE, FINCSTP, FDECSTP, FCHS, FABS, FXAM ;
  FLDCW, FSTCW, FSTSW, FCLEX, FINIT, FLDENV, FSTENV, FSAVE, FRSTOR avec les pointeurs ; chaque accès mémoire contrôlé
  (le chemin de PB-189). PB-52, 54, 55, 56 (le stockage), 62, 63, 66, 67, 195, 196, 199, 203, 206, 207, 208, 212 ;
  les exceptions masquées. ≈ 1 500 lignes.
- **N4 — L'arithmétique, les comparaisons, l'arrondi.** Coupé en deux : **N4a** ✅ *(11/10, VERIFICATION.md § G13.6, le
  noyau, N4a)*, les quatre opérations, FSQRT, les comparaisons et le domaine d'accord (PB-48, 60 ; la précision de
  PB-56, les drapeaux masqués de PB-59 ; les comparaisons de PB-57, 58, 64, 70 et 213 reprises, au groupe en N7) ;
  **N4b** ✅ *(11/10, VERIFICATION.md § G13.6, le noyau, N4b)*, FRNDINT, FSCALE, FXTRACT, FPREM et FPREM1 (PB-65, 194,
  197, 198, 211, et PB-265, neuf, que le domaine d'accord a trouvé). Le contenu d'ensemble : FADD, FSUB(R), FMUL, FDIV(R) en mémoire (m32, m64, m16
  entier, m32 entier) et en registre ; FSQRT ; FCOM, FCOMP, FCOMPP, FUCOM*, FICOM, FTST (le projectif du 287 compris) ;
  FRNDINT, FSCALE, FXTRACT ; FPREM et FPREM1 par réductions partielles (C2, et le quotient dans C0, C3, C1). PB-48,
  56 (la précision), 57, 58, 59 (les drapeaux masqués), 60, 64, 65, 70, 194, 197, 198, 211, 213. Après N4, le
  **domaine d'accord** : sous 027Fh, sur des normaux du `double`, le noyau rend au bit près le `double` de PCem — le
  fuzzeur, restreint à ce domaine, contre l'oracle (une exception déclarée au refus des fuzzeurs, comme le contrôle
  de fuite). ≈ 1 500 lignes.
- **N5 — Les exceptions démasquées** ✅ *(11/10, VERIFICATION.md § G13.6, le noyau, N5)*. IE, DE, OE, UE et PE par
  l'acheminement de G13.6c (aujourd'hui ZE seule) :
  l'instruction qui faute avant de calculer laisse sa destination ; OE et UE démasquées rangent le résultat à
  l'exposant décalé (±24 576), ce que SoftFloat ne fait pas : un second écart, un arrondi à exposant non borné,
  annoncé comme C1. PB-59 entier. ≈ 500 lignes.
- **N6 — Les transcendantes** ✅ *(12/10, VERIFICATION.md § G13.6, le noyau, N6)*. F2XM1, FYL2X, FYL2XP1, FPTAN,
  FPATAN, FSIN, FCOS, FSINCOS, écrites sur le noyau :
  réduction par le π de 66 bits que décrit le SDM (vol. 1, § 8.3.8 ; pour le 387 et le 486 : déduit), polynômes en virgule fixe de 128 bits, un seul arrondi final ;
  les domaines (ceux, étroits, du 8087 et du 287), C2 au-delà de 2^63, le 1 de FPTAN. Les attendus calculés hors
  machine en précision arbitraire (`decimal` de Python, sans installation ; un outil commité). Visé : ≤ 1 ulp sur
  le 387 et le 486, (b) ; le bit d'Intel reste (c) (décision n° 14). PB-68. ≈ 1 500 à 2 000 lignes. *Fait* : la
  réduction exacte, en entiers, par le Pi de 66 bits (§ 8.3.8) ; des séries de Taylor sur un réel de travail de 128
  bits (`x87_transcendantes.Materiel.cs`), un seul arrondi ; la référence `tools/x87ref/transcendantes.py` et le banc
  `x87-transc`, au bit près ; C2, le 1 de FPTAN, les spéciaux des tables du SDM ; hors des domaines, la valeur
  mathématique (un choix). Les domaines étroits du 8087 et du 287, FPTAN en rapport, et FSIN, FCOS et FSINCOS, absents de
  ces deux-là (PB-266, détaché de PB-68), passent à N7.
- **N7 — Les coprocesseurs de 16 bits** ✅ *(12/10, en trois : N7a, N7b, N7c)*. Le 8087 et le 287 : l'infini projectif dans l'arithmétique, les non normaux
  et les dénormaux chargés en non normaux, FXAM à seize codes, FINIT et FSAVE qui gardent C3-C0 (PB-202), FNSTSW AX
  absent du 8087 (PB-200), l'adressage du 8087 (PB-201), le quotient du 287, FSETPM et la disposition des images du
  287, IEM, FENI, FDISI ; venus de N6, FSIN, FCOS et FSINCOS absents (PB-266), les domaines étroits des transcendantes
  et FPTAN en rapport. ≈ 1 000 lignes. **Le noyau entre alors au mode** (« tout » et le domaine `x87`). Coupé en
  trois (12/10), sur la liste de l'annexe C du 387 PRM (« Compatibility between the 80387 and the 80287/8087 ») :
  - **N7a — Le 8087 et le 287, hors des non normaux** ✅ *(12/10, VERIFICATION.md § G13.6, le noyau, N7a)*. FINIT, FSAVE et le RESET gardent C3-C0 (PB-202, annexe C §
    C.3) ; DF E0 sur le 8087, AX inchangé (PB-200) ; l'adressage du 8087, l'adresse physique incrémentée pour chaque
    mot (PB-201) ; FENI et FDISI (IEM du 8087, sans effet ailleurs), et les entrées que PCem exécute en FNOP ; IE pour
    tout NaN, le NaN de plus grand significande ; le dépassement masqué (l'infini au plus près et vers zéro, OE
    seulement si la réponse est l'infini), la petitesse sur le résultat vrai, OE et UE démasquées vers la pile sans
    arrondi ni PE ; DE démasquée avant toute autre ; ni DE dans les transcendantes et FXTRACT, ni examen de leurs
    opérandes (287 PRM, « operands not checked ») ; DE pour FLD m80 ; FXTRACT d'un zéro (deux zéros) et d'un infini
    (IE), FSCALE par un infini (IE) ; FPTAN sans C2, l'opérande gardé en ST(1) après la faute de pile ; C2 de FPREM
    invalide (les bits de son quotient, sur lesquels les sources se contredisent, restent ceux de PCem) ; les étiquettes du pseudo-zéro et du non normal, et leurs codes de FXAM (les
    seize codes, hors de l'arithmétique des non normaux). Les domaines étroits et le
    rapport de FPTAN : hors domaine, « undefined » ; le noyau garde la valeur mathématique (un choix, (c)). PB-209 et
    PB-266 restent reproduits (décision n° 14).
  - **N7b — Les non normaux et l'infini projectif** ✅ *(12/10, VERIFICATION.md § G13.6, le noyau, N7b)*. Les tables 1-8, 1-9, 1-10, 1-11, 1-12 et 1-17 du 287 PRM : le
    dénormal chargé en non normal (FLD m32, m64), converti en non normal dans un calcul ; l'arithmétique des non
    normaux (la normalisation de l'opérande le plus grand pour l'addition, un produit non normal, un dividende non
    normal) ; IE pour un diviseur, une racine, un modulo, un FIST ou un FBSTP dénormal ou non normal ; FST m32 et m64
    d'un non normal ; le pseudo-zéro ; les comparaisons, FTST et FRNDINT qui normalisent d'abord (DE pour un non normal
    comparé) ; FXTRACT d'un dénormal et d'un non normal ; FST m32 et m64 d'un dénormal ; l'infini projectif dans
    l'addition, la soustraction et FSQRT. Les étiquettes auxquelles le 8087 et le 287 se fient au lieu du contenu
    (annexe C § C.3) : ce qu'ils font d'un contenu contredit n'est pas décrit, (c).
  - **N7c — L'entrée au mode** ✅ *(12/10, VERIFICATION.md § G13.6, le noyau, N7c)*. `EnChantier` vidé ; les groupes du noyau et des comparaisons réunis, PB-57, 58, 61,
    63, 64, 66, 67, 70, 207 et 213 repris par le noyau ; les entrées illégales de PCem (PB-210) aux tables du noyau ;
    chaque lecteur de `cpu_state.ST` et `tag` (le débogueur, l'état sauvé) relu ; « tout » et le domaine `x87`.
- **N8 — La clôture** ✅ *(12/10, en deux : N8a, N8b ; le noyau est clos)*. Les gestionnaires en `double` de G13.6a et 6b, que le noyau remplace, quittent le dépôt (leurs
  cas restent, rejoués par le noyau) ; la couverture comptée (gestionnaires × classes × RC × PC) ; PCEM_BUGS ; la
  marge du `--timer-check` en mode matériel sur un invité saturé de flottant (le 486, un Whetstone), supérieure à 1
  (décision n° 7) ; X87BANC aux mêmes temps (les cycles restent ceux de PCem). Coupé en deux (12/10) :
  - **N8a — Le retrait** ✅ *(12/10, VERIFICATION.md § G13.6, le noyau, N8a)*. Les gestionnaires en `double` de PB-57,
    58, 61, 63, 64, 66, 67, 70, 207 et 213 (x87.Materiel.cs, 808x.Materiel.cs), leurs poses dans cpu_set et la garde de
    PB-70 dans `x87_compare` ; l'acheminement (PB-59, 69, 204), qui vaut sans le noyau, reste.
  - **N8b — Les mesures** ✅ *(12/10, VERIFICATION.md § G13.6, le noyau, N8b)*. La couverture comptée, la marge du `--timer-check` sous un invité saturé de flottant, X87BANC,
    PCEM_BUGS.

**La vérification, en plus du patron de chaque étape.** TestFloat rejoué à travers les gestionnaires (le mot de
contrôle vers SoftFloat, les indicateurs vers `npxs`) ; le domaine d'accord contre l'oracle dès N4 ; les vecteurs
publics des tests x87 de QEMU (GPL-2.0-or-later, des données, hors dépôt comme SST) ; MSD « 80287 » et les détections
387 à chaque étape qui touche les comparaisons (D2, risque 10). Environ 8 000 à 10 000 lignes en tout, générées
comprises.

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

#### G13.7, le plan détaillé — *proposé et validé le 12/10 (« Je valide les recommandations »)*

**Q12, tranchée par Julien le 12/10** (« je veux que l'on fasse ce que le vrai matériel fait ») : en mode matériel, le
comportement documenté du matériel au lieu du repli de R9 — les transferts complets du CD (PB-115), du ZIP (PB-125) et
du disque SCSI (PB-128), READ LONG et WRITE LONG du WD1003 (PB-76), les commandes 0Ch, 1Ch, 1Dh, 20h et 21h de
l'AHA-1542C (PB-135) ; le repli reste où il est déjà le comportement du matériel (PB-73, 113), où ce comportement est
inconnu (PB-75, 114), et pour les fichiers de l'hôte (PB-110, 116, la configuration de PB-135). Le reliquat de
PLAN-G11.md (les tampons du ZIP au régime de `scsi_hd`, la fidélité du mode PCem) entre dans G13.7.

Cinq étapes, chacune avec ses cas `hw-*` en C# seul (chaque attendu sourcé à sa norme), ses contrôles négatifs, sa
contre-lecture, M2, sa série entière et son commit :
- **G13.7a — La disquette et le MFM** ✅ *(12/10, VERIFICATION.md § G13.7a ; PB-76 reste au repli, l'ECC n'étant pas documenté au bit près ; l'unité absente du Xebec ajoutée à PB-267 par la contre-lecture)*. PB-14 (le débit de 300 kbit/s), PB-19 (la surcharge d'une écriture en PIO, deux
  sites), PB-22 (FORMAT TRACK du Xebec part du secteur 0), PB-23 (le bit d'unité de l'octet de fin), PB-25 (la borne
  des têtes), PB-217 (le sense du Xebec et son adresse) ; les dix-sept `fatal()` de protocole du Xebec, sous R9, dans
  les deux modes, par l'*IBM Fixed Disk Adapter* (Q4 de D3-stockage) ; PB-76, READ LONG et WRITE LONG, 512 octets et
  les 4 de l'ECC — si le code ECC du WD1003 n'est pas documenté au bit près, PB-76 reste au repli (dit). Restent
  reproduits : PB-28 (le DTC 5150X, inconnu), PB-75 (l'unité 1 absente, inconnu).
- **G13.7b — L'IDE** ✅ *(12/10, VERIFICATION.md § G13.7b ; SRST et RESET- effacent l'interruption en attente, ajoutés par la contre-lecture)*. PB-71 et PB-216 (l'IRQ du canal secondaire, et `ide_irq_update` qui baisse une ligne encore
  tenue : une seule fonction), PB-74 (VERIFY, tous les secteurs et les registres de fin, ATA-1). PB-73 est déjà le
  comportement du disque ; PB-72 : Q6.
- **G13.7c — Le CD** ✅ *(13/10, en quatre : 7c1 à 7c4)*. Le moteur d'images (PB-106, le mode 2 à 2 336 octets ; PB-108, INDEX 00 ; PB-109, l'échec
  collant ; PB-122, le lead-out de la TOC) ; l'ATAPI et l'audio (PB-117, READ CAPACITY ; PB-119, MODE SELECT ; PB-120,
  la TOC brute ; PB-123, l'audio en LBA ; PB-214 et 215, PLAY AUDIO MSF) ; PB-115, les transferts complets. Le jeu de
  commandes : Q5. Restent au repli : PB-113 (déjà celui du lecteur), PB-114 (inconnu), PB-110 et 116 (les fichiers).
  Coupée le 12/10, comme N7 (en trois, puis en quatre en G13.7c2) :
  - **G13.7c1 — Le moteur d'images** ✅ *(12/10, VERIFICATION.md § G13.7c1)*. PB-106, PB-108 (étendu : la piste qui ouvre son
    fichier commence à son INDEX 01, quel que soit son INDEX 00), PB-109, PB-122 ; en C# seul, hors machine ; deux
    scénarios de M2 (ami486-cd-cue, un PACKET READ(10) sous DEBUG ; ami486-cd-iso).
  - **G13.7c2 — Les commandes du lecteur** ✅ *(12/10, VERIFICATION.md § G13.7c2)*. PB-117, PB-119 (et deux défauts du même
    sous-programme, au texte de SFF-8020i § 10.8.4 : 26h au lieu de 24h, la liste vérifiée avant d'appliquer ; 1Ah pour
    une liste tronquée), PB-120 ; deux défauts neufs, de la contre-lecture : PB-268 (un READ qui sort du disque, refusé
    d'emblée) et PB-269 (MECHANISM STATUS) ; PB-115 examiné : rien n'y bascule (les transferts dans le disque déjà
    complets, ceux qui en sortent étant PB-268). Le texte de SFF-8020i r2.6 obtenu : les références des étapes du CD
    relues.
  - **G13.7c3 — Le jeu de commandes selon l'année du modèle (Q5)** ✅ *(12/10, VERIFICATION.md § G13.7c3)*. MMC-2 (r11a,
    août 1999) obtenue. PB-270 (neuf) : sur ATAPI, READ(6), MODE SELECT(6) et MODE SENSE(6) refusés (ni SFF-8020i
    Table 37, ni MMC-2 Table B.2 — la longueur 0 de READ(6) n'a donc plus d'objet sur ATAPI), GESN, PLAY AUDIO(12) et
    READ DISC INFORMATION refusés par les modèles SFF-8020i ; MMC-2 pour le 40x, le 48x, le 72x et le générique de
    PCem (années documentées pour quatre modèles, déduites de la vitesse pour les autres) ; PB-118, GESN selon MMC-2,
    en groupe avec PB-270 ; PB-271 (neuf), READ CD MSF, obligatoire, servi.
  - **G13.7c4 — L'audio** ✅ *(13/10, VERIFICATION.md § G13.7c4)*. PB-123 (l'adresse LBA de PLAY AUDIO et de SEEK, et READ
    SUB-CHANNEL : la position du lecteur à 150 secteurs près), PB-214 (la piste de PLAY AUDIO MSF cherchée à son LBA),
    PB-215 (son contrôle de piste) ; deux défauts neufs, de la contre-lecture : PB-272 (l'adresse de PLAY AUDIO(12) sur
    quatre octets) et PB-273 (une plage qui change de type, 05/63h) ; en C# seul, au niveau du pilote d'images et sur
    le banc de r9-atapi.
- **G13.7d — Le ZIP et le disque SCSI.** PB-125 (les transferts complets, une adresse hors du disque en CHECK
  CONDITION, 05/21h), PB-126 (la capacité, la fin du disque, les phases à longueur nulle ; START STOP UNIT reste
  reproduit, inconnu sur le lecteur d'Iomega), PB-128 (les transferts complets, jusqu'à 65 535 blocs), PB-129 (la
  capacité, le bloc de trop), PB-130 (MODE SENSE), PB-131 (REQUEST SENSE, INQUIRY, les refus ; FORMAT UNIT reste simulé),
  PB-132 côté cible (la phase vide) ; le reliquat de G11 (les tampons du ZIP au régime de `scsi_hd`, en mode PCem :
  vérifié contre l'oracle, ZIPBANC).
  Coupée en deux le 13/10 :
  - **G13.7d1 — Le ZIP** ✅ *(13/10, VERIFICATION.md § G13.7d1)*. Le reliquat de G11 en mode PCem (les tampons en un tableau de
    2 × 256 Ko, comme `scsi_hd`, vérifié par ZIPBANC contre l'oracle) ; PB-125 (les transferts complets) et PB-126 (la
    capacité, la fin du disque, les longueurs nulles, le disque gardé au reset) en mode matériel ; START STOP UNIT
    détaché en PB-274, reproduit (inconnu sur le lecteur d'Iomega).
  - **G13.7d2 — Le disque SCSI**, coupée en deux :
    - **G13.7d2a** ✅ *(13/10, VERIFICATION.md § G13.7d2a)*. PB-128 (les transferts complets), PB-129 (le dernier bloc, la
      plage), PB-131 (REQUEST SENSE, INQUIRY, les refus), PB-132 côté cible (les longueurs nulles) ; VERIFY, MODE SELECT
      et FORMAT UNIT simulés détachés en PB-275 (reproduit, inconnu) ; le côté carte de PB-132 détaché en PB-276 (7e).
    - **G13.7d2b** ✅ *(13/10, VERIFICATION.md § G13.7d2b)*. PB-130 (MODE SENSE : l'en-tête, le descripteur, le bourrage, la géométrie).
- **G13.7e — L'AHA-1542C.** PB-276 (l'ancien PB-132 côté carte : HRST, la navette, le statut 12h), PB-133 (le LUN par IDENTIFY), PB-135 (les cinq commandes du TR),
  PB-137 (le sense automatique), PB-139 (ABORT), PB-140 (RETURN SETUP DATA entier), PB-141 (les mailbox), PB-144 (la
  longueur et les réglages d'usine ; la traduction CHS et les 24 bits, déduits), PB-136 et 138 dans leurs bornes,
  PB-218 (le drapeau de RETURN EEPROM). Restent reproduits : la réentrance de PB-142, PB-143, le CCB en vol sous
  SCRST, l'IRQ de code 7 (inconnus). Les réglages d'usine : Q8.
  PB-135 est déjà corrigé dans les deux modes (R9) : rien n'y bascule. Coupée en quatre le 13/10 :
  - **G13.7e1a — La configuration** ✅ *(13/10, VERIFICATION.md § G13.7e1a)*. PB-144 côté usine (Q8 : sans EEPROM écrite, l'image de référence avec ID 7, DMA 5,
    IRQ 11 ; un fichier `nvr` propre au mode matériel, sans repli sur `nvr/default/`), PB-218 (23h, le drapeau 0 rend
    les options d'usine), PB-140 (0Dh entier : 44 octets utiles, les interrupteurs en 26h, 0 pour 256), PB-277 (22h
    qui change l'IRQ relâche l'ancienne ligne, détaché de PB-136 ; PB-136 hors bornes reste reproduit). PB-278 à
    PB-280 détachés de PB-144.
  - **G13.7e1b — Les mailbox** ✅ *(13/10, VERIFICATION.md § G13.7e1b)*. PB-141 (01h de compte nul refusé, le balayage des 02h, le MBI libre attendu, HRST et
    SRST exigent une nouvelle init), PB-139 (ABORT : l'emplacement en `c * 4`, un seul MBI, 03h si le CCB est déjà
    rendu).
  - **G13.7e2 — Le côté SCSI.** Coupée en deux le 13/10 :
    - **G13.7e2a — Les données et le bus** ✅ *(13/10, VERIFICATION.md § G13.7e2a)*. PB-276 (HRST et SCRST remettent le bus au repos ; au bout du CCB, le
      surplus de la cible purgé et le CCB achevé, au lieu de la navette), PB-278 (les bits de sens arment le contrôle
      de longueur, 12h ; les deux posés, aucun transfert ; les données à 24 bits, l'ID de l'hôte exclu). Détachés,
      reproduits : PB-281 (le CCB en vol sous SRST ou SCRST, inconnu), PB-282 (le CCB lui-même à cheval sur 16 Mo).
    - **G13.7e2b — Le LUN et le sense** ✅ *(13/10, VERIFICATION.md § G13.7e2b)*. PB-133 (le LUN du CCB jusqu'à la cible, l'équivalent d'IDENTIFY ; le disque
      à un LUN absent selon SCSI-2 : INQUIRY 7Fh, REQUEST SENSE 05/25h, le reste CHECK CONDITION), PB-137 (le sense
      automatique sur CHECK CONDITION seul, rangé dans le CCB en 12h + m, octet de contrôle 0 ; celui de la commande
      BIOS dans le tampon interne ; 14h inatteignable). Détaché, reproduit : PB-283 (le CD et le ZIP SCSI à un LUN
      absent).
  - **G13.7e3 — La commande BIOS 03h** ✅ *(13/10, VERIFICATION.md § G13.7e3)*. PB-138 (ni CCB périmé, ni succès sur une cible absente : 80h), PB-279 (le
    secteur CHS moins 1) ; attendus déduits, l'interface est privée. PB-283 reclassé (c) à l'étude, et sorti de
    l'étape : sa forme SCSI est inatteignable (ni CD ni ZIP sur le bus de l'AHA, décision n° 2 de PLAN-G11.md), sa
    forme ATAPI inconnue (SFF-8020i réserve l'octet 1 du paquet) ; reproduit dans les deux modes.
  - **G13.7e4 — La mise sous tension de l'AHA-1542C et son DMA** ✅ *(17/10, VERIFICATION.md § G13.7e4 ; décidée
    par Julien le 16/10 : « on part pour 7e4 »)*. PB-260 (la carte, maître du bus, passe outre son canal de DMA et le
    8237 : la vraie carte passe par son canal, en cascade) et PB-142 (la machine des commandes, la mise sous tension).
    Une étude d'abord (ce que disent le TR d'Adaptec et le 8237 ; ce que PCem fait), puis sa série. Après G13.11.
    L'étude (`/var/tmp/ixtal-g13/etude-7e4.md`) : la ROM de la carte met son canal en cascade avant tout accès à la
    mémoire (0x3FE) ; la suspension se fait à l'échéance de 10 µs, en tête de `process_ccb`, `process_scsi`, de 1Ah, 1Bh
    et des états de 03h qui écrivent ; la mise sous tension joue le HRST de PCem ; PB-142 disait 00h, PCem relit 10h.
    **Q28, posée le 16/10** — un canal démasqué mais hors cascade (mode simple, bloc ou demande) : le 8237 y pilote
    lui-même le bus, en conflit avec la carte (fiche, Cascade Mode) ; l'issue réelle est inconnue. (A) la condition de
    l'AT TR : la carte attend, comme PB-253 pour le canal 4 ; (B) reproduire PCem, seuls le masque et la désactivation
    suspendant. *Proposé : (A) ; codé ainsi en attendant la réponse, une ligne à retirer pour (B).* Faite : la série
    verte dans les deux modes, et le démarrage témoin (DOS 5 depuis le disque SCSI, sous « tout » comme en mode PCem).

**Les questions de D3-stockage, tranchées par Julien le 12/10 sur ces propositions** :
- **Q5 (G13.7c)** — les commandes que la norme du lecteur de 1996 n'a pas (GET EVENT STATUS NOTIFICATION, MODE
  SELECT/SENSE(6)) : les refuser comme un lecteur SFF-8020i, ou les servir selon MMC-2 comme un lecteur de 1998 ? Les
  modèles de PCem vont d'un 4x (vers 1995) à un 72x (vers 1999). *Validé : selon l'année du modèle choisi.*
- **Q6 (G13.7b)** — PB-72, une écriture pendant un reset : ATA-1 n'en dit rien et ATA-3 se contredit. Proposé : le
  laisser reproduit (inconnu, décision n° 14). *Validé.*
- **Q8 (G13.7e)** — sans EEPROM écrite, les réglages d'usine du TR (ID 7, DMA 5, IRQ 11) au lieu de ceux de PCem (ID 7,
  DMA 7, IRQ 10) ; et un fichier `nvr` de la carte par mode, ou partagé ? Proposé : les réglages d'usine, et un
  fichier par mode (une EEPROM écrite dans un mode ne doit pas changer l'autre). *Validé.*

### G13.8 — Le son

- Le DSP 8 bits et l'ADPCM (PB-90, 91 ; 146 et l'octet de référence ; la coupure de PB-147 ; 149 ; l'état du DSP au
  reset ; 08h de la SB 16) ; le CT1745, le MPU-401 et la ligne d'IRQ partagée (PB-153, 154 ; 145 pour la 2.0 sans
  mélangeur) ; l'enregistrement (PB-148, 151, 156) ; l'EMU8000 (PB-158 à 160, 162, 163) ; la clôture (PB-21, 155).
- **Vérification** : des cas en C# seul aux valeurs du guide de Creative et des micrologiciels, chacun avec sa page ;
  SBBANC, SB16BANC et AWEBANC en C# seul (le scénario de PB-146 corrigé : une commande à paramètre intercalée) ; une
  série par sous-étape.
  Coupée en sept le 13/10 (l'étude, en lecture seule, de trente entrées du son) ; une série par sous-étape :
  - **G13.8a — L'état du DSP** ✅ *(13/10, VERIFICATION.md § G13.8a)*. PB-236 (le reset : le bloc 07FFh, la constante 9Ch, la sortie coupée, au démarrage
    aussi), PB-241 (la grande vitesse : aucune commande sur les DSP 2.01 à 3.xx ; le reset « chaud » qui garde le bloc,
    jusqu'au 4.13 ; 2xCh, inconnu, reste celui de PCem), PB-240 (38h et l'octet MIDI, le mode UART de 34h à 37h dont un reset fait sortir ; l'entrée reste
    reproduite, faute de source), PB-165 (08h de la SB 16 sans paramètre ; sa valeur, inconnue, reste 18h), PB-147
    (D1h et D3h de la SB 1.0 relient et coupent la sortie). Le banc des cas du son (`MaterielCasSon.cs`) : une machine
    par situation, la carte par ses ports, le 8237 par les siens, le tic de sortie joué à la main.
  - **G13.8b — L'ADPCM** ✅ *(13/10, VERIFICATION.md § G13.8b)*. PB-90, PB-91, PB-234, PB-235, PB-146 : les attendus se composent (contre-lecture K6). À
    l'étude (13/10), les décodeurs des quatre micrologiciels relus : PB-284, neuf (les tables du 2 et du 2,6 bits, le
    troisième échantillon du 2,6 bits) ; PB-234 élargi (le premier octet d'un bloc, sans référence et à la recharge).
    Le vecteur du DSP tic par tic sous PB-234, le tic de PCem refait sous les autres ; quatre attendus par scénario.
  - **G13.8c — Le DSP 4.xx et la fréquence.** PB-149, PB-239, PB-155 (Q15). Coupée en deux le 13/10 : **8c1** ✅
    *(13/10, VERIFICATION.md § G13.8c1)*, PB-149 (l'octet de mode de B0h à CFh) et PB-239 (la commande mise en file :
    2.0x et 3.xx pendant tout transfert, 4.xx pendant un automatique), qui ne dépendent pas de Q15 ; **8c2** ✅
    *(17/10, VERIFICATION.md § G13.8c2)*, PB-155, après la réponse à Q15 (tranchée le 16/10) : l'étude
    (`/var/tmp/ixtal-g13/etude-8c2.md`) ; le registre de 8 bits du micrologiciel, la retenue perdue et la table du 4.05
    sur la SB 16 (la carte réelle, Q15), le 4.13 sur l'AWE32 ; les Hz de MAME ; la conversion du FIR saturante, une
    garde par tampon ; le registre au reset, 38h, sous PB-236 ; PB-156 reclassé (d), sans objet en mode matériel ;
    r9-sb16 du mode PCem seul ; PB-288, neuf (le registre ne s'applique qu'au départ d'un transfert), inscrit ;
    **8c3** ✅ *(17/10, VERIFICATION.md § G13.8c3)*, PB-288 : pendant un transfert, 40h, 41h et 42h d'un DSP 4.xx
    sont gardées ; le codec reçoit le registre en tête des commandes qui appellent `X0a26` (les départs, D4h, D6h) et
    au reset.
  - **G13.8d — Les mélangeurs, le MPU-401 et la ligne partagée.** PB-237, PB-145, PB-153, PB-154, PB-238 (Q14). Coupée
    en deux le 13/10 : **8d1** ✅ *(13/10, VERIFICATION.md § G13.8d1)*, les mélangeurs — PB-237 (les volumes du reset),
    PB-145 (la SB 2.0 sans mélangeur), et de PB-153, 3Dh et 3Eh au reset et la traduction du 16 bits vers le canal 8
    bits (81h), qui ne dépendent d'aucune question ; **8d2** ✅ *(13/10, VERIFICATION.md § G13.8d2)*, la ligne
    partagée — PB-154 (l'IRQ du MPU, 82h bit 2, 3Fh en mode UART) et PB-238 (2xEh, 2xFh et le reset du DSP), sans
    question ; **8d3**, de PB-153, l'IRQ 5 d'usine, après la réponse à Q14 — devenue les cavaliers (Q22 à Q27) :
    **8d3a** ✅ *(17/10, VERIFICATION.md § G13.8d3a)*, la SB 16 et l'AWE32 — `irq`, `dma`, `dma16` et `mpu_addr`
    dans les deux modes, l'IRQ 5 d'usine et « Use Low » sous PB-153, PB-289, neuf (l'EMU8000 à la base + 400h) ;
    **8d3b** ✅ *(17/10, VERIFICATION.md § G13.8d3b)*, les SB 1.x, 2.0 et Pro, selon les manuels de Q24 — PB-290,
    neuf (le DMA 1 seul des 1.x et 2.0, l'IRQ 5 d'usine de la 2.0), PB-291, neuf (le DMA 0 de la Pro), la liste du
    mode lue par `device_get_config_int_liste` ; **8d3c** ✅ *(17/10, VERIFICATION.md § G13.8d3c)*, l'AHA-1542C —
    PB-292, neuf (le port d'usine, 330h, en mode matériel ; l'avertissement du MPU de `pc.cs` le suit), SW5 en clé
    `floppy` dans les deux modes (sur Off d'usine, déduit, contre la proposition « désactivé » du relevé, soumis à
    Julien : l'octet 26h de 0Dh, PB-140, rend 01h) ;
    **8d4** ✅ *(14/10, VERIFICATION.md
    § G13.8d4)*, ouverte le 14/10 à la demande de Julien, le front de la ligne — PB-285, neuf (une IRQ du DSP levée sur
    la ligne déjà haute ne repose pas la demande au PIC), les gardes en tête de chaque état qui lève, `sb_irq`, inliné
    dans `pollsb`, laissé sans garde. La sélection additive de PB-153 passe en
    8e : elle ne sert que l'enregistrement, et se lit par échantillon (la garde au niveau du tampon).
  - **G13.8e — L'enregistrement.** PB-148 (Q13), PB-151, PB-156, la sélection additive de PB-153 ; après 8d.
    Attend Q13 : sans PB-148, l'enregistrement reste muet, PB-151 et PB-156 n'ont aucun effet audible, et PB-156 devient
    inatteignable si PB-155 est borné (Q15) ; l'étude les joint alors à 8c2. Q13 tranchée le 16/10 (PB-156 sans objet
    depuis 8c2) ; l'étude (`/var/tmp/ixtal-g13/etude-8e.md`) la coupe en deux le 17/10 : **8e1** ✅ *(17/10,
    VERIFICATION.md § G13.8e1)*, la SB 16 et l'AWE32 enregistrent la MIDI — PB-148 (l'anneau du CAN rempli par les
    copies des tampons, la lecture un tampon derrière l'écriture, 20h sans régler l'entrée), PB-151 (l'anneau sans
    alias), de PB-153 la sélection additive et 0Ah↔3Ah ; **8e2** ✅ *(17/10, VERIFICATION.md § G13.8e2)*, le CD (un
    pont entre son flux et celui de la carte : 36h, 37h, 3Dh et 3Eh sur la SB 16 et l'AWE32, 0Ch sur la Pro) et la SB
    Pro (sa copie du tampon ; A0h et A8h du 3.xx, une voie par tic ; le reset en mono).
  - **G13.8f — L'EMU8000** ✅ *(14/10, VERIFICATION.md § G13.8f)*, sans question : PB-158 (a) (WC et les registres
    courants avancent à la lecture), PB-159 (a) (le bit 31 et le masque de 24 bits des adresses de la DRAM, en A22h),
    PB-160 (a) (HWCF3 sans la rustine : l'AWE32 non initialisée se tait). Faite avant 8e, que bloquent Q13 et Q15.
    Restent PB-162 et PB-163 (Q13), avec l'octet impair de PB-158 : **G13.8f2** ✅ *(16/10, VERIFICATION.md
    § G13.8f2)*, ouverte le 16/10 (Q13 tranchée) —
    l'octet impair de PB-158 (l'octet haut), PB-162 (le chorus droit dans sa ligne, à sa fraction), PB-163 (la
    réverbération sature), leurs gardes en tête du travail d'un tampon ; les moteurs joués seuls dans leurs cas.
  - **G13.8g — La clôture du son.** PB-21 ; SBBANC, SB16BANC et AWEBANC en C# seul dans les deux modes ; PERFBANC ;
    les témoins (Q16). Coupée en trois le 17/10, sur l'étude (`/var/tmp/ixtal-g13/etude-8g.md`, hors du dépôt) :
    **8g1** ✅ *(17/10, VERIFICATION.md § G13.8g1)*, le compte 0 du 8254 dans le haut-parleur — PB-21 (`speakval`, le
    compte 0 lu 2^16 aux deux comptes), PB-293, neuf (la sortie de `speaker_update` hors des modes 0 et 4, le carré de
    18,2 Hz) ; **8g2**, les bancs en C# seul, coupée en deux : **8g2a** ✅ *(17/10, VERIFICATION.md § G13.8g2a)*, les
    six SBBANC (le scénario de PB-146 corrigé : 40h A5h intercalée, le compte du 8237 relu à la première IRQ) ;
    **8g2b** ✅ *(17/10, VERIFICATION.md § G13.8g2b)*, SB16BANC et AWEBANC (ils impriment leurs relevés ; les bancs de
    l'ami486 posent `irq = 7` et l'AHA en 334h ; l'avertissement de `pc.check_sndcard` attend le mode figé, que sa garde
    de PB-292 figeait en PCem au chargement d'une machine à l'AHA et à la SB 16 ou l'AWE32, depuis 8d3c) ; **8g3**,
    coupée en trois : **8g3a** ✅ *(18/10, VERIFICATION.md § G13.8g3a)*, PERFBANC, `SonBanc` (le coût d'un tampon de
    50 ms : au plus 0,15 ms, 0,3 % ; PB-148 avec une entrée, PB-162 et PB-163 le portent) ; **8g3b**, les témoins
    (Q16) ✅ *(18/10, VERIFICATION.md § G13.8g3b ; hors du dépôt : VPLAY, l'ADPCM 4 bits, VREC mono et stéréo,
    sur la Pro v2 installée par émulation ; VEDIT2 non joué, ses menus attendent Alt ou la souris)* ; **8g3c** ✅
    *(18/10, VERIFICATION.md § G13.8g3c)*, 10b et 10c rejouées (200 corrections couvertes ; la série de 8g2b ; M0,
    chaque écart rattaché). Le son est clos.
  Restent reproduits : PB-242 (c), PB-92 (d), les parts (c) de 153, 154, 158, 159, 160, 165 et 240 ; PB-243 et PB-244
  (b), laissés par Q13. Hors du mode : PB-150, 152, 161, 166 (R9), 93 ; (d) : PB-164, PB-167.

**Les questions du son (G13.8), posées à Julien le 13/10 ; 8a n'en dépend pas** :
- **Q13 (8d, 8e, 8f, 8g)** — les parts (b) : corriger celles dont la correction est certaine et se vérifie par
  cohérence — PB-21 (le compte 0 lu 65 536), PB-148 (les chemins documentés de l'enregistrement, sans modèle des
  filtres), PB-162 (le chorus droit dans son tampon), PB-163 (la réverbération saturée), 0Ah↔3Ah de PB-153 (à l'échelle
  du guide), l'octet impair de PB-158 (le bus AT) — et laisser reproduits, en le disant, PB-243 (une source secondaire
  seulement), PB-244 (une durée maximale seulement) et les parts (b) de PB-160 (des modèles de PCem ; les corriger
  serait en inventer un autre) ? *Proposé.* **Tranchée le 16/10 : oui** — l'EMU8000 (PB-162, PB-163, l'octet impair
  de PB-158) en G13.8f2 ; PB-148 et 0Ah↔3Ah avec l'enregistrement (8e) ; PB-21 avec la clôture (8g).
- **Q14 (8d)** — l'IRQ 5 d'usine de la SB 16 et de l'AWE32 (décision n° 13) contre les profils réglés à l'IRQ 7
  (`SET BLASTER … I7`) et le témoin Windows 3.11 de G12.3 : l'usine sans clé neuve, dite dans le profil et la bannière
  du mode (comme Q8) ; un réglage propre viendrait en G16 s'il manque. Autre choix : une clé `sb16_irq` du mode. *Proposé :
  l'usine.* **Tranchée le 16/10** (la réponse de Julien, donnée pour « Q15 », porte sur celle-ci) : l'utilisateur
  choisit les IRQ, les DMA et les autres cavaliers des cartes de l'époque, dans la configuration de sa machine, avant
  de la lancer (précisé le 16/10). 8d3 devient les cavaliers : d'abord leur relevé, carte par carte (ce que la carte
  réelle permettait, ce qu'iXtal expose), proposé à Julien.
- **Q15 (8c)** — PB-155 : le registre de fréquence de 8 bits du 4.05 (documenté par le micrologiciel), avec les Hz de
  MAME (secondaire, dit dans l'entrée), et la conversion saturante ; ou la saturation seule, la fréquence de PCem
  gardée (part reclassée (c)). *Proposé : le registre et la saturation.* **Tranchée le 16/10 : le comportement réel de
  la SB 16** — le registre et la quantification du 4.05 (son micrologiciel, vérifié au binaire) ; les Hz de MAME faute
  de mesure, dits secondaires ; la saturation (le FIR est le rééchantillonneur de PCem, la carte ne claque pas). 8c2,
  avec PB-156.
- **Q16 (8g)** — les témoins : le logiciel de la disquette SB Pro 2 (VPLAY, VEDIT2 pour l'ADPCM, VREC) installé sur une
  copie de disque dans /var/tmp, hors du dépôt, sans porte de série, comme les amorçages de 7e. *Proposé : oui.*
  **Tranchée le 16/10 : oui.**

**Les questions des cavaliers (8d3, Q14), posées à Julien le 16/10** — le relevé, carte par carte, dans
`/var/tmp/ixtal-g13/releve-cavaliers-8d3.md` (hors du dépôt) ; en mode PCem, ni table ni lecture ne change :
- **Q22** — quelle SB 16 : une carte à cavaliers (CT1740 : 220h à 280h, IRQ 2, 5, 7, 10, DMA 0, 1, 3, DMA haut 5, 6, 7,
  MPU 330h ou 300h) ? *Proposé : les cavaliers posent l'état à l'allumage, 80h et 81h écrits restent ceux de PCem.*
  **Tranchée le 16/10 : oui**, pour la SB 16 (DSP 4.05, celui des premières cartes à cavaliers) et l'AWE32 (DSP 4.13,
  monté sans PnP par PCem).
- **Q23** — l'IRQ 5 d'usine par défaut, les deux profils du dépôt (`ixtal26-486-sb16.cfg`, `-awe32.cfg`) posant
  `irq = 7`, que leurs témoins attendent ? *Proposé : oui.* **Tranchée le 16/10 : oui.**
- **Q24** — les listes de DMA que seules des sources secondaires corrigent (le DMA 0 de la Pro, le DMA 3 des 1.x et
  2.0) ? *Proposé : laissées à PCem tant qu'une source primaire manque.* **Tranchée le 16/10 : oui**, et chercher les
  manuels d'installation de Creative, qui trancheraient sur pièce. **Trouvés le 16/10**
  (`/var/tmp/ixtal-g13/manuels-creative/RAPPORT.md`, hors du dépôt ; archive.org) : la 1.5 (URM 1989-1990, p. 63-64)
  et la 2.0 (User's Guide, 4e éd., 1994, p. 1-6, fig. B-7) n'ont que le DMA 1, « not selectable » — le DMA 3 de PCem
  est faux ; leurs IRQ 2, 3, 5, 7 ; l'usine IRQ 7 (1.5) et IRQ 5 (2.0, la CT1350B, DSP 2.01). La Pro (URM SBP-0002,
  Getting Started CSBPP-002, p. 7-8) : DMA 0, 1 ou 3 (le 0 manque chez PCem), IRQ 2, 5, 7, 10, l'usine 220h, IRQ 7,
  DMA 1. La SB 16 du User's Guide 1992-1994 règle l'IRQ et les DMA par logiciel (DIAGNOSE, 80h et 81h), ses seuls
  cavaliers l'adresse et le MPU ; l'usine 220h, IRQ 5, DMA 1, DMA 16 bits 5, MPU 330h (p. B-2). Pour 8d3b : deux PB
  neufs à inscrire (le DMA 3 des 1.x et 2.0 ; le DMA 0 de la Pro), et l'IRQ 5 d'usine de la 2.0.
- **Q25** — le périmètre : le son (SB 16 et AWE32, l'EMU8000 en BLASTER + 400h ; puis la Pro, les 1.x et 2.0) et
  l'AHA (8d3a à c) ; l'EGA (SW1-SW4, P3) et le Xebec plus tard ; COM et LPT avec la carte mère ? *Proposé : oui.*
  **Tranchée le 16/10 : oui** — 8d3a (SB 16, AWE32), 8d3b (Pro, 1.x, 2.0, selon les manuels de Q24), 8d3c (AHA,
  SW5) ; puis, une étape après le son : l'EGA (SW1-SW4, P3), la police de la CGA (P3), le type de disque du Xebec, les
  adresses de l'IDE AT et de l'XTIDE ; COM et LPT avec la carte mère (PB-256).
- **Q26** — une clé du mode matériel posée en mode PCem : ignorée, avec un avertissement ? Les cavaliers à l'écran de
  construction maintenant, ou en G16 ? *Proposé : l'avertissement ; l'écran en G16.* **Tranchée le 16/10, autrement**
  (voir la décision n° 17) : les cavaliers valent dans les deux modes ; seul le défaut change (celui de PCem en mode
  PCem, l'usine en mode matériel), et sans clé, le mode PCem reste celui de PCem. L'écran, en G16.
- **Q27** — l'AHA-1542C à 334h chez PCem, 330h d'usine : un PB neuf, corrigé (avec la SB 16 ou l'AWE32, le MPU en
  330h entre alors en conflit, comme sur la carte : un avertissement le dit) ? *Proposé : oui.* **Tranchée le 16/10 :
  oui** — 330h par défaut en mode matériel, 334h en mode PCem ; l'avertissement de `pc.cs` suit le port réel du MPU ;
  `ixtal26-486-scsi.cfg` pose déjà 334h.

### G13.9 — La vidéo

- Le socle SVGA (PB-80 et le chemin rapide, PB-37, `htotal`) ; la MDA et l'Hercules (PB-102, 97, 96, le curseur
  plein, les rythmes de clignotement, 3BAh) ; l'EGA (PB-99, hors la valeur des registres en écriture seule) ;
  l'ET4000 (les parts documentées de PB-100) ; la CGA, la M24 et le PC1512 (PB-04, PB-89).
- PB-35 et la valeur des registres en écriture seule restent reproduits, avec leur protocole de mesure.
- **Vérification** : des cas en C# seul (les périodes de balayage contre IBM et Hercules, les masques contre la fiche
  du 6845, les rangées du curseur) ; les bancs (EGABANC, ET4BANC, BLTBANC, HERCBANC) en C# seul ; les images quand
  GR.0 les aura ; `--menu-check` resserré ; deux séries.
  Coupée en six le 14/10, sur l'étude, en lecture seule, de quarante-deux entrées : vingt-cinq de la vidéo, avec
  D4-video et D4-contre. Une série par sous-étape :
  - **G13.9a — Le socle SVGA** ✅ *(14/10, VERIFICATION.md § G13.9a)*, sans question. PB-220 (CR00 + 5, d'IBM). PB-80 : la lecture en chain4 compact charge
    les verrous, dans le socle et la GD5429 ; les verrous de huit octets restent ceux de PCem, hors de la source. La
    part (a) de PB-221 : les lectures rapides de mot et de double mot chargent les verrous de leur dernier octet. La
    part (b) de PB-221, les écritures, et PB-37 attendent Q17.
  - **G13.9a2 — Le socle SVGA, les parts (b)** ✅ *(14/10, VERIFICATION.md § G13.9a2)*, après la réponse à Q17 : la part (b) de PB-221 (le chemin rapide des
    écritures en mode d'écriture 0 et sans rotation seulement : un mot écrit vaut deux octets) et PB-37 (toute la ligne
    de 24 bits, chaque point doublé). Après 9b.
  - **G13.9b — La MDA et l'Hercules** ✅ *(14/10, VERIFICATION.md § G13.9b)*. Sans question : PB-102 (le caractère de 9 points), la part (a) de PB-97 (les
    masques, le Turbo XT), la MDA de PB-96, la MDA de PB-222 et de PB-223 (le curseur plein, son rythme), et PB-225.
    PB-224 va à 9f (Q18) ; l'entrelacé de PB-97 et l'Hercules de PB-96 restent reproduits (Q17).
  - **G13.9c — La CGA, la M24 et le PC1512** ✅ *(14/10, VERIFICATION.md § G13.9c)*. PB-04, la CGA de PB-222 et de
    PB-223, la part (a) de PB-230 (R16-R17) ; la M24 et le PC1512 n'ont que des parts inconnues (la relecture, leur
    puce non lue) ou hors de l'entrée (leur curseur). PB-232 reste reproduit (Q17).
  - **G13.9c2 — Les rendus : la bordure, PB-89, et la mémoire de l'EGA, PB-99 (6)** ✅ *(15/10, VERIFICATION.md
    § G13.9c2)* (Q17), après 9d : tous les rendus qui suivent R1 ou CR01, sur la CGA, la MDA, l'Hercules, l'EGA, la M24
    et le PC1512, la ligne bornée à elle-même ; les rendus de l'EGA, les substitutions de CR17 et la police sous
    vrammask. Les vingt sites de PB-89 tiennent en une garde par carte, à l'init : le balayage du mode matériel posé sur
    la minuterie de la carte, qui garde les deux lignes suivantes de buffer32 autour du balayage de PCem quand R1 peut
    sortir de la ligne. PB-99 (6) : l'adresse de début de ligne masquée, CR17 bit 1 tenu à 1 le temps d'une ligne
    graphique sur 64 Ko, les tables de SR3 masquées.
  - **G13.9d — L'EGA** ✅ *(14/10, VERIFICATION.md § G13.9d)*. PB-99 (1), (2), (5) et (7), avec CR0A bit 5 et
    l'attribut 10h bit 5. PB-99 (6), la mémoire de 64 Ko qui ne voit pas le bit 0x10000 (déduit, Q17), va à 9c2 avec les
    rendus qu'elle touche ; PB-99 (4) et PB-228 vont à 9f (Q18).
  - **G13.9e — L'ET4000 et son RAMDAC** ✅ *(14/10, VERIFICATION.md § G13.9e)*. PB-227 (CR35 sous CR11), CR13 de
    PB-100 (nul vaut 0), l'IPF de PB-229 (Q17). La fenêtre de 128 Ko de PB-100 : sans effet visible (le BIOS pose un mode
    avant tout accès), laissée. PB-226 va à 9f (Q18).
  - **G13.9e2 — La séquence KEY de l'ET4000** ✅ *(15/10, VERIFICATION.md § G13.9e2)* (PB-100 (b)) : sa pose (03h en
    3BFh, puis A0h en 3D8h ou 3B8h) et sa portée (data book Tseng, pp. 102-104, 111, 136-137, 142, 144, 158) ; son
    retrait, que le data book ne décrit pas, par la séquence du BIOS de la carte (01h en 3BFh, puis 29h ; sa routine en
    1050h, trouvée à l'étude). Restent PCem, mesurés aux amorçages : l'état du KEY à la mise sous tension (le BIOS écrit
    les registres gardés avant sa première pose, puis les relit) et le reset synchrone qui refermerait 3CDh (à la
    lettre, après chaque changement de mode du BIOS).
  - **G13.9f — Le faisceau** ✅ *(15/10, VERIFICATION.md § G13.9f)* (Q18), après 9e : le point sous le faisceau à
    l'instant de la lecture, tiré du temps de la ligne et de la mémoire d'affichage ; PB-99 (4) (3DAh de l'EGA, POD14
    sans bip pour critère), PB-224 (3BAh bit 3 de la MDA et de l'Hercules), PB-226 (les bits 4-5 et 7 de l'Input Status
    1 de l'ET4000), PB-228 (l'Input Status 0 de l'EGA, son bit 7 quand l'interruption est autorisée ; le reste, inconnu,
    reproduit). Le point : la ligne que la carte rend, sa palette remplacée par l'identité (les sorties de l'attribut),
    une fois par phase de la ligne ; à la part écoulée de la phase, lue à la minuterie de la carte ; la bordure et le
    blanc hors de l'affichage. POD14 rejoué a trouvé PB-99 (8) : 3C1h, où le BIOS d'IBM écrit la donnée de l'attribut,
    que PCem ignore. Restent la bascule de PCem sous 12h = 3Fh sur l'EGA (« Not Used ») et sous les trois choix de 12h
    que le data book de l'ET4000 contredit.
  - **G13.9g — La clôture de la vidéo** ✅ *(15/10, VERIFICATION.md § G13.9g)*. EGABANC, ET4BANC, BLTBANC et
    HERCBANC en C# seul dans les deux modes, contre leurs attendus (`tools/gates/bancs/`) ; BLTBANC, resté en
    graphique, par l'empreinte de la mémoire vidéo (`@vram`, neuf) ; `--menu-check` resserré, avec les bornes du mode
    matériel (il ne prenait pas le mode : le lanceur le pose désormais avant le contrôle) ; PERFBANC, le coût d'une
    trame (`VideoBanc`). L'explication d'un écart d'ET4BANC donnée en 9f, fausse, corrigée (PB-220, et non PB-100).
  Restent reproduits, les parts (c) : PB-35, la relecture de PB-97, PB-99 (3), l'état du KEY à la mise sous tension,
  le reset synchrone et SR7 de PB-100, l'Hercules de PB-223, l'ERPF et D3 de PB-229, l'index et R0-R13 de PB-230,
  PB-231, PB-233 ; et PB-245, avec le clavier de la M24. Hors du mode : PB-09, 81 à 86, 88 (R9) et 93 ; les parts (d) : PB-36, 38, 98, CR3F de PB-100.
  PB-256 va avec la carte mère.

**Les questions de la vidéo (G13.9), posées à Julien le 14/10 et tranchées le même jour ; 9a n'en dépendait pas** :
- **Q17 (9a à 9e)** — les parts (b). Je propose de corriger celles qui suivent la documentation sans modèle neuf. La
  part (b) de PB-221 : un mot écrit vaut deux octets, donc le chemin rapide n'est pris qu'en mode d'écriture 0, sans
  rotation. PB-37 : toute la ligne de 24 bits, chaque point doublé. PB-89 : la bordure bornée à sa ligne. CR13 de
  PB-100 : 0 vaut 0. L'IPF de PB-229. Je laisse reproduits, en le disant, ce qui n'a que l'intention de PCem pour
  source : PB-232, l'Hercules de PB-96, l'entrelacé de PB-97, la fenêtre de 128 Ko et le KEY posé de PB-100. *Proposé.*
- **Q18 (9b, 9d, 9e)** — le faisceau. PB-99 (4), PB-224, PB-226 (bits 4-5) et PB-228 demandent un mécanisme neuf : le
  point sous le faisceau à l'instant de la lecture, tiré du temps de la ligne et de la mémoire d'affichage. POD14, le
  test du BIOS de l'EGA d'IBM, en est le critère, sans bip. Le faire, dans une sous-étape à part après 9e, ou laisser
  ces bits reproduits ? *Proposé : le faire, à part.*
- **Réponse de Julien (14/10)** : « fais ce qui est conforme au matériel. donc en sous étape après 9e ». Q17 : corriger
  ce que la documentation du matériel établit ; ce qui n'a que l'intention de PCem pour source reste reproduit, et on le
  dit. Q18 : le faisceau, une sous-étape après 9e (9f) ; la clôture devient 9g.

### G13.10 — La clôture

- Les profils du mode matériel (le 486 de référence, le 5150) dans `launchSettings.json` ; `--setup-check` ; les
  témoins du mode matériel par machine (POST, `VER`, `MSD /S`, Windows sur l'ami486) ; la couverture comptée (la
  sonde du mode) ; `PCEM_BUGS.md` (un champ *Corrigé en mode matériel* par entrée) ; `VERIFICATION.md` ; PLAN.md ;
  les originaux intacts.
- **Vérification** : la série entière dans les deux modes ; les listings contre M0.
- Coupée en trois le 15/10, sur l'étude, après 9g. Le son qui attend Q13 à Q16 (8c2, 8d3, 8e, 8g), PB-260 et PB-142
  (7e3 bis ou 7e4) et PB-256 (la carte mère) restent ouverts, et la clôture le dit ; quand ils viendront, 10b et 10c
  seront rejouées. Rejouées après le son, en G13.8g3c (18/10) ; PB-256, Q28 et Q29 restent ouverts.
  - **G13.10a — Les profils et les témoins.** Deux profils du mode matériel dans `launchSettings.json` : l'ami486 de
    référence (`ixtal26-486.cfg`) et le 5150. `--setup-check`. Les témoins, en C# seul (`--boot`), dans les deux
    modes, chaque écart expliqué : le 5150 jusqu'au BASIC ; l'XT et PC-DOS 2.00 (`VER`, `DIR`) ; l'IBM AT ; l'ami286 et
    l'ami386dx sur leurs disques (`VER`) ; l'ami486 et DOS 5 (`VER`, `MSD /S`) ; Windows 3.11 sur une copie du disque de
    l'ami486. Les originaux intacts : `os/os.sha256`, `g5w.sha256`. Sans changement de l'émulateur : pas de série.
  - **G13.10b — La couverture et le registre** ✅ *(15/10, VERIFICATION.md § G13.10b)*. La couverture comptée : une porte qui joue, pour chaque correction du
    mode (`ModeMateriel.Corrections`), son cas sous son seul PB, et compte ce que dit la sonde ; une correction qu'aucun
    cas n'atteint rougit. `PCEM_BUGS.md` : chaque entrée des sections A et B porte un champ *Corrigé en mode matériel*,
    ou dit pourquoi elle reste (inconnue, hors du mode, en attente d'une question) ; le recensement le vérifie.
  - **G13.10c — La série dans les deux modes et M0** ✅ *(15/10, VERIFICATION.md § G13.10c)*. La série entière une fois
    en mode PCem, une fois sous `--hardware-mode tout` pour chaque porte qui prend le mode (`ModeOutils`) ; les portes
    qui le refusent sont dites. Les listings de tous les scénarios contre M0 : en mode PCem, le code d'avant G13, aux
    écarts connus près, chacun expliqué. PLAN.md et la fin de `VERIFICATION.md`.
  - **G13.10d — LOCK et MOV CR0** ✅ *(16/10, VERIFICATION.md § G13.10d)* (Q19, Q21). PB-178 (LOCK est un préfixe
    du 8088 et du 8086, avant ou après REP), PB-286 (MOV CR0 lève #GP(0) sur PG sans PE, 386 et 486) et PB-287, neuf
    (sur NW sans CD, 486), chacun son cas et sa sonde ; le contrôle de fuite de PB-178. La série entière dans les deux
    modes, qui rejoue 10c, et les listings contre M2-10c.

**Les questions de la clôture (G13.10), posées à Julien le 15/10** :
- **Q19 (10b)** — PB-107 (une image de CD de plus de 2 Gio prend une longueur tronquée) et PB-178 (LOCK du 8088, une
  instruction d'un octet chez PCem et non un préfixe), que l'audit du registre a trouvés sans étape : aucune ne les a
  pris. PB-178 suit Intel (« the instruction that follows it ») : un préfixe, l'interruption entre lui et son
  instruction restant inconnue. PB-107 est une correction d'hôte, sans comportement du matériel documenté (Q2 de
  D3-stockage, restée sans réponse). *Proposé : corriger PB-178 dans une petite étape avant 10c (la part documentée ;
  l'interruption, inconnue, reproduite) ; laisser PB-107, dit non corrigé.* **Tranchée le 16/10 : oui** — G13.10d.
  L'interruption entre LOCK et son instruction n'a pas pu rester celle de PCem : PCem ne fait pas de LOCK un
  préfixe, et garder la fenêtre en gardant le préfixe de segment porterait celui-ci d'une instruction à l'autre. LOCK
  suit la règle du cœur pour tous ses préfixes (rien ne s'intercale), le silicium restant inconnu.
- **Q20 (10a)** — `os/os.sha256` (04/10) : `386-HDD-C.img` (modifiée le 10/10) et `486-HDD-C.img` (le 11/10) ne
  concordent plus, hors des portes, qui travaillent sur des copies. Des sessions de l'émulateur ? Récrire les deux
  empreintes ? *Proposé : les récrire si ces écritures sont les vôtres.* **Tranchée le 16/10 : oui** — récrites
  (4e8bde8), l'état actuel devient la référence.
- **Q21 (10c)** — PB-286, trouvé par la série sous « tout » : `MOV CR0` accepte PG = 1 avec PE = 0, où le silicium
  lève #GP(0) (386 PRM § 9.8.13, point 13 ; 486 PRM, l'exception 13 ; documenté). Dans cet état, que `r9-mmu` pose sans
  passer par l'instruction, sous PB-51 et sur le cœur 486, une #GP de la limite de CS livrée en mode réel par une table
  de pages hors RAM revient en FFFF:FFFF sans consommer de cycle : l'émulateur ne rend plus la main (mesuré, la porte
  tuée après 53 minutes). En mode PCem, IP franchit FFFFh (PB-51) et la porte reste verte ; une boucle de fautes avec
  une pile en RAM avance (les écritures comptent leurs cycles, mesuré sous PB-51 et en mode PCem). *Proposé : corriger
  PB-286 en mode matériel dans la petite étape de PB-178 (Q19), avant de rejouer 10c ; `r9-mmu` reste du mode PCem seul
  (`pcem_seul`), puisqu'elle pose CR0 sans l'instruction. LOADALL386 sur le 386 charge CR0 tel quel, PG compris : le
  silicium, inconnu, reste reproduit.* **Tranchée le 16/10 : oui** — G13.10d, avec PB-287 (NW sans CD sur le 486, le
  même site, trouvé en lisant l'i486 PRM), et la série de 10d rejoue 10c dans les deux modes.

### G13.11 — Le mode matériel par défaut  ✅ *(16/10, VERIFICATION.md § G13.11)*

- La décision n° 17. Les profils du dépôt et le lanceur posent `hardware_mode = tout` par défaut : une machine lancée
  sans rien de plus tourne en mode matériel. Le mode PCem se demande explicitement ; c'est celui des portes, de
  l'oracle et des listings M2, qui le posent eux-mêmes.
- Avant d'agir, un relevé : chaque chemin qui lance une machine (le lanceur, `--boot`, les bancs, `listings-jit.sh`,
  les portes de `boot-diff` et de `banc`, les témoins), le mode qu'il obtient aujourd'hui et celui qu'il doit avoir ;
  une valeur explicite du mode PCem pour la clé et l'option ; la bannière du mode.
- **Vérification** : la série en mode PCem inchangée (les portes posent le mode PCem) ; les listings M2 contre ceux de
  l'étape d'avant ; une machine lancée sans clé annonce le mode matériel, une machine `hardware_mode = pcem` le mode
  PCem ; les témoins de G13.10a dans les deux modes.
- **Le relevé** (16/10, `/var/tmp/ixtal-g13/releve-g13-11.md`) : la clé se lit par `config_get_int` (`pc.cs:570`),
  `hardware_mode = tout` ou `= pcem` y passe pour absente, sans un mot — seuls 0 et 1 servent ; à lire en chaîne, avec
  `pcem` (synonyme d'`aucun`) et les listes de l'option. **La conception retenue** : le défaut « tout » posé par le
  lanceur, en tête de `TryConfigureMachine` (la fenêtre, `--headless`, l'écran de construction) ; la priorité, l'option,
  puis la clé, puis le défaut ; `--boot`, `--timer-check`, `--menu-check` et `--setup-check`, les verbes des portes,
  des listings et de la recette g5w, restent en mode PCem sans option, et la série ne change pas d'un octet ; l'outil
  Diff, figé avant son aiguillage, n'est pas touché. L'annonce dit toujours le mode ; l'écran de construction écrit
  `tout` par défaut ; une valeur inconnue de la clé, refusée avec un message (le défaut gardé).

## Les décisions (proposées ; à trancher)

1. **Le principe et la portée.** Le principe ci-dessus. La portée : les PB des sections A et B, plus les défauts
   voisins que les contre-lectures ont vérifiés, inscrits d'abord (G13.1). Les drapeaux indéfinis du 386 et les temps
   (PB-11, PB-12, les temps de commutation de tâche) restent hors du mode. *Validé le 07/10.*
2. **G13.0 tout de suite, dans les deux modes.** PB-09 et PB-17 sous R9, puisque la règle existe ; PB-121 et le filet
   de l'hôte (vider les images sur toute sortie anormale, `savenvr` sans exception), qui protègent les fichiers de
   l'utilisateur sans relever de R9. *Validé le 07/10 : les quatre.*
3. **Le nom** : `hardware_mode` et `--hardware-mode`, l'anglais des clés de PCem et de `zip_path` ; ou `materiel`.
   *Validé le 07/10 : `hardware_mode` et `--hardware-mode`.*
4. **Le mécanisme** : des `static readonly` figés et des tables recopiées, un interrupteur pour l'utilisateur et un
   masque par PB pour les outils ; la clé dans le `.cfg`, que boot-diff refuse. *Validé le 07/10.*
5. **Les données extérieures** : récupérer les vecteurs SST (les formes manquantes du 8088, le corpus du 386, celui du
   8086 ; celui du 286 après réparation du lecteur). Des données de test publiques, sans installation. *Validé le 07/10.*
6. **Le banc `tools/perfbanc/`**, non commité, avec la ligne `InternalsVisibleTo` d'`iXtal26.csproj` : le commiter en
   G13.2 pour M1. *Validé le 07/10 : oui, commité en G13.2.*
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
    une SB, un 8237…) ? Une mesure trancherait une vingtaine de points. *Réponse du 07/10 : aucun matériel ; les
    comportements inconnus restent reproduits.*
15. **`TRANSCRIPTION.md`** : un relèvement inscrit de R3, de 240 à 260 lignes, pour R10 et ses amendements. *Validé le 07/10.*
16. **Les rapports de reconnaissance** : commités avec ce plan, dans `iXtal26/Docs/G13-reconnaissance/`. *Validé le 07/10.*
17. **Sortir de PCem, petit à petit** (Julien, le 16/10 : « la série G13 est là pour nous sortir des défauts de PCem
    et nous rapprocher du matériel réel de l'époque »). Le mode matériel devient celui de l'utilisateur : les profils
    et le lanceur le posent par défaut (G13.11) ; le mode PCem reste celui des portes, de l'oracle et des listings,
    tant qu'ils servent. Ce que PCem n'a pas (les cavaliers de la SB 16 et de l'AWE32) vaut dans les deux modes, seul
    le défaut suivant le mode, sans clé le mode PCem restant celui de PCem. *Validé le 16/10.*

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
