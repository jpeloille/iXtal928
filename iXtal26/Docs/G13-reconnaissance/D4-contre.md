# G13 — La vidéo : contre-lecture du rapport D4

Le 6 octobre 2026. Contre-lecture de `D4-video.md`, en lecture seule du dépôt (`master`, `bc609ce`) :
rien de construit, rien de lancé. Le C et le C# ont été relus aux lignes citées. Les sources
primaires ont été relues en texte intégral (OCR d'archive.org), et les schémas IBM en image, rendus
à 400-600 ppp : ils sont lisibles, contrairement à ce que dit le rapport.

**Niveaux** : *documenté* (une source primaire le dit), *déduit*, *inconnu*. **Classements** du
rapport : (a) corrigeable et vérifiable, (b) vérification faible, (c) vrai comportement inconnu,
(d) à ne pas corriger. J'ajoute **R9** : un défaut qui arrête l'hôte, à corriger hors G13 et dans
les deux modes. Le rapport le range à tort en (d).

## Sources ajoutées

- **[T1]** IBM, *Technical Reference* 6025008 (PC, août 1981), texte OCR. Pp. 2-42/2-43 : ports
  et 3BAh de la MDA. Listing du BIOS : en-tête de l'INT 10h (« HARDWARE WILL ALWAYS CAUSE BLINK —
  SETTING BIT 5 OR 6 WILL CAUSE ERRATIC BLINKING OR NO CURSOR AT ALL »), VIDEO_PARMS (p. A-46
  env.), SET_CTYPE et M16 (pp. A-47/A-48 env.).
  <https://archive.org/details/bitsavers_ibmpcpc602renceAug81_17295874>
- **[T2]** IBM, *Options and Adapters Technical Reference*, vol. 2, rév. avril 1984, texte OCR. Il
  contient [S1], [S2] et [S5] du rapport :
  - MDA pp. 1-8 ;
  - CGA p. 12 (lecture du tampon), p. 15 (décodage de 3D0h-3D7h, « Z = don't care »), p. 17
    (« Write Only ») ;
  - EGA pp. 9, 15-17, 25, 30-37, 41-42, 56-61, 113-114.

  <https://archive.org/details/bitsavers_ibmpccardsptionsandAdaptersVolume2Apr84_25079400>
- **[T3]** [S2], schémas de la CGA, feuilles 1 (p. 28) et 3 (p. 30), lues en image à 600 ppp. La
  couche texte de ces pages est illisible ; l'image ne l'est pas.
- **[T4]** [S1], schémas de la MDA, feuille 5 (p. 23), lue en image à 400 ppp.
- **[T5]** IBM, *PS/2 Hardware Interface Technical Reference — Common Interfaces* (84F9735,
  oct. 1990), VGA :
  - p. 2-45 : Input Status 1, bits autres que 0 et 3 « Undefined on Read » ;
  - p. 2-56 : CR00, « the total number of characters minus 5 ».

  <https://archive.org/details/bitsavers_ibmpcps284erfaceTechnicalReferenceCommonInterfaces_39004874>
- **[T6]** [S7], data book ET4000, OCR intégral : pp. 85, 102-104, 111, 123, 136, 142, 144,
  150-151, 158. <https://archive.org/details/bitsavers_tsengLabsTicsController1990_11230195>
- **[T7]** [S8], SC15025/26, texte complet : tables 1 à 3, 5, 9 et 10, pp. 3-83 à 3-91.
- **[T8]** [S9], CL-GD542X : § 6.27 p. 6-34, GRB p. 9-32, description de GR5[3].
- **[T9]** [S3], MC6845 : table des registres (R0-R13 « Read : No » ; R16-R17 « Write : No ») ;
  R10 bits 5-6.
- **[T10]** Sources secondaires, des émulateurs :
  - 86Box : `src/video/vid_svga.c:830-832` (« +5 is required for Tyrian ») ; `vid_cga.c:374-385`
    et `:868-877` ; `vid_mda.c:73-77` et `:160` ;
  - MAME : `src/devices/bus/isa/cga.cpp:496-501`.
- **[T11]** Microsoft, *Exceptions in managed threads* : une exception non gérée termine le
  processus. <https://learn.microsoft.com/dotnet/standard/threading/exceptions-in-managed-threads>

---

## 0. PB-09 et R9 : le verdict

**Le rapport a raison. PB-09 viole R9 aujourd'hui, et la conséquence dépasse l'arrêt de
l'émulateur. C'est à corriger tout de suite, hors G13, dans les deux modes.**

- **Le code** (*documenté*, lu) :
  - `charbuffer` fait 256 octets (`Video/vid_cga.cs:63`), et `crtcmask[1] = 0xff` (`:78`) laisse
    R1 monter à 255.
  - Deux sites lèvent. La copie de fin de ligne lève dès x = 256 (`:554-557`). La relecture
    80 colonnes, `charbuffer[x << 1]`, lève dès x = 128 (`:249-250`).
  - Le commentaire du C# le dit lui-même (`:551-552`, « le C# lève »).
- **La condition exacte** (*déduit* du code) :
  - R1 ≥ 129 ;
  - 3D8h bit 0 = 1 : modes 2 et 3, ou tout mode où un programme pose ce bit, le bit 1 n'y change
    rien ;
  - une rangée affichée (`cgadispon`, `:554`).

  La relecture exige aussi 3D8h bit 3 ; la copie, non. L'exception tombe en moins d'une trame.
- **Le scénario.** Le `o 3d4 1` / `o 3d5 c8` du rapport suffit, en mode 3. `OUT &H3D4,1 : OUT
  &H3D5,200` en BASIC aussi. Un programme qui tente 132 colonnes (R1 = 84h) aussi (*déduit*).
- **Rien ne la rattrape** (*documenté*, lu) :
  - L'appel remonte de `cga_poll` au chronomètre, à `execx86`/`exec386` (`pc.cs:1416-1427`), à
    `runpc`, à la boucle de `Host/SdlHost.cs:459` et `:641`, à `Launcher.Run`
    (`Host/CommandLine/Launcher.cs:17-28`), puis à `Program.cs:9`.
  - Tous les `catch` du projet filtrent des exceptions d'entrée-sortie : `IOException`,
    `UnauthorizedAccessException`, `ArgumentException`, `NotSupportedException`,
    `FileNotFoundException`, `DirectoryNotFoundException`. Aucun gestionnaire
    `UnhandledException`, et `BootTest.cs` n'en a pas non plus.
  - Le .NET termine donc le processus ([T11]).
- **Ce que le rapport ne dit pas** (*déduit* de trois commentaires du dépôt) :
  - `closepc` est le seul endroit qui vide les tampons d'écriture des images de disquette et de
    disque dur (`pc.cs:1306-1320`, `Host/SdlHost.cs:1343-1348`, `Disc/hdd_file.cs:88-93`) ;
    `savenvr` n'est pas appelé non plus.
  - **L'invité peut donc faire perdre les dernières écritures sur les images**, le disque C: de
    Julien compris.
- **L'oracle.** PCem déborde dans `revision`, `composite` et `snow_enabled` (`vid_cga.h:31-35`),
  puis hors du bloc alloué (`vid_cga.c:418`) : il corrompt son tas avec des octets choisis par
  l'invité. L'entrée PB-09 (`PCEM_BUGS.md:1797-1806`) parle des « champs voisins de cga_t » ; au-delà
  de R1 = 136 environ, le bourrage de fin de structure compris, c'est le tas.
- **Contre PB-88.**
  - La M24 est traitée : lecture à 0, écriture sautée (`vid_olivetti_m24.cs:230-236`, `:545-551`).
  - Mais aucun test de survie en C# seul n'existe pour elle : pas de `r9-m24` dans
    `tools/iXtal26.Diff/Program.cs:235-287`. R9 l'exige pourtant (`TRANSCRIPTION.md:76`).
  - R9 date du 1er octobre et n'a pas été repassée sur la section B : PB-09 est passé entre les
    mailles (*déduit* : l'entrée et le marqueur disent toujours « reproduit »).
- **Quelle sortie : la seconde voie, `charbuffer` de 512.** R9 prescrit « le comportement du
  matériel s'il est connu » (`TRANSCRIPTION.md:73`). Il l'est pour le cas légal R0 > R1 > 128
  (*documenté*) :
  - la CGA n'a pas de tampon de ligne et lit son tampon d'affichage ([S2] p. 4 ; [T2] CGA p. 12,
    « the adapter fetches character and attribute information from its display buffer ») ;
  - le 6845 compte R1 caractères par ligne ([T9]).

  Trois arguments départagent les voies :
  - *Fidélité au matériel.* Un tampon de 512 octets rend exactement les octets que la carte lit.
  - *Identité avec l'oracle* (*déduit*). Pour R1 = 129 et 130, PCem écrit puis relit ces octets
    dans `revision`, inerte après l'amorçage. La voie 512 reste donc identique à l'oracle là où il
    survit, comme R9 le demande ; la voie de PB-88 (lecture à 0) s'en écarte.
  - *Coût.* Le rapport écrit « Chemin chaud : non », ce qui est faux pour la voie de PB-88 : elle
    ajoute un test par caractère dans la boucle la plus chaude de la carte (`:249-250`), comme la
    M24 le paie déjà. La voie 512 n'ajoute rien.

  Pour la M24, dont le débordement réécrit aussitôt `ctrl`, `base` et `cgamode`, la voie « 0 »
  reste défendable : c'est « le plus sûr » de R9. Le registre dira pourquoi les deux cartes
  diffèrent.
- **V0, complété** :
  - la voie 512 ;
  - `r9-cga` en C# seul (R1 = 129, 200 et 255 ; 3D8h = 01h puis 29h) et le `r9-m24` qui manque ;
  - PB-09 passé en « NON reproduit (R9) » ;
  - PLAN.md § G13 corrigé : la section B garde 13 PB reproduits, et 09 rejoint les non-reproduits ;
  - une série entière, puisque l'émulateur change (règle des séries du 04/10) ;
  - si une sonde de la CGA hache un jour `charbuffer` (GR.0), elle ne hachera que les 256 premiers
    octets.

---

## 1. Confirmé

1. **PB-04, les sites** (*documenté*, lus).
   - C : `vid_cga.c:165` et `:198` ; le champ en `vid_cga.h:27`, la locale en `:122`.
   - C# : `vid_cga.cs:262` et `:306`. Le marqueur de `:259-261` n'a pas l'identifiant, `:306` n'en
     a pas, et `PCEM_BUGS.md:110` cite `:249`, ligne périmée.
   - L'intention de PCem se lit ailleurs : la MDA (`vid_mda.c:111`), l'Hercules
     (`vid_hercules.c:163-164`), la M24 (`:177`) et le PC1512 (`:205`) testent la locale.
2. **PB-80** (*documenté*).
   - Sites : `vid_svga.c:1391-1395` contre `:1081-1090` ; `vid_cl5429.c:1183-1187` et `:741-748` ;
     C# `vid_svga.cs:1711` et `vid_cl5429.cs:1322`.
   - Les verrous de la GD542x se chargent à chaque lecture CPU ([T8] § 6.27, p. 6-34). CR22 n'est
     pas modélisé : `vid_cl5429.c:463-489` rend `crtc[0x22]`. GRB[3] porte les verrous de huit
     octets ([T8] p. 9-32).
   - La sonde a le champ `la|lb|lc|ld` (`VgaProbe.cs:33`).
3. **PB-89** (*documenté*).
   - Les sites sont exacts, et rien ne sort du tableau : `Buffer32` fait 2 048 × 2 048
     (`video.cs:115-124`).
   - Le motif se retrouve dans la CGA : `vid_cga.c:147-153`, `:188-270`, `:275`, `:277`,
     `:280-294`.
4. **PB-97** (*documenté*).
   - (a) R9 = 2Dh ne finit jamais la rangée, R4 ≥ 80h jamais la trame : `sc &= 31` et `vc &= 127`
     (`vid_mda.c:160`, `:173`).
   - (b) R0-R13 sont en écriture seule ([T2] CGA p. 17, [T9]).
   - (d) En entrelacé, les rangées paires vont à la trame paire ([T9]).
   - La MDA est bâtie sur le 6845 de Motorola ([T2] MDA p. 1). HERCBANC relit les registres
     (`hercbanc.py:202-203`), et 86Box garde les trois défauts ([T10]).
5. **PB-99, sous-défauts (1) à (5)** (*documenté*, relus dans [T2]).
   - Pages : EGA pp. 30-37, 56-61, 15-17 et 113-114.
   - Marqueurs : ceux de `vid_ega.cs:139`, `:237`, `:264`, `:300`, `:692` et `:1190` existent ;
     `:759` et `:766` n'en ont pas.
   - Traits de la VGA non inscrits : `vid_ega.c:198-220`, C# `:322-340`.
   - POD14 ([T2] EGA pp. 113-114) et les lignes d'EGABANC (`:211`, `:234-236`) sont exacts.
   - L'IRQ 2 de l'EGA est absente : `vid_ega.c` n'appelle aucun `picint`.
6. **PB-100** (*documenté*).
   - (1) La table 4.3-2 n'a que 00h-18h et 32h-37h ([T6] p. 111) : CR3F n'existe pas sur l'AX.
     CR30 et CR31 restent inscriptibles chez PCem (`vid_et4000.c:34-37`).
   - (2) CR13 s'ajoute à chaque rangée, sans cas particulier ([T6] p. 123).
   - (4) La pose du KEY est en [T6] p. 102. Son retrait est absent de tout le data book : le texte
     OCR entier a été parcouru.
   - (7) Les modes 3a et 3b font 4 octets par point, le quatrième jeté ([T7] table 2 p. 3-84 et
     table 5). La table 3 de [T7] (p. 3-86) est bien citée, et l'IPF aussi (pp. 3-88/89).
7. **PB-102** (*documenté*).
   - Les calculs sont justes : 18,43 kHz et 49,8 Hz pour la MDA ; 18,14 kHz et 49,0 Hz pour
     l'Hercules en texte, 18,52 kHz et 50,05 Hz en graphique.
   - MDA : caractère de 9 points et moniteur de 18 kHz ([T2] MDA p. 2). Hercules : 0,5625 µs et
     1 µs, 16 × 4 et 9 × 14, environ 54 µs, 50 Hz (manuel GB101, p. 10).
   - PCem sait appliquer 9/8 à l'EGA (`vid_ega.c:231-232`).
   - Il faut recalculer aussi sur 3B8h et 3BFh : le mode vient de `(ctrl & 2) && (ctrl2 & 1)`
     (`vid_hercules.c:145`).
8. **PB-96** (*documenté*). Quatre lignes RA vont au générateur de caractères ([S1] p. 3).
   Le générateur fait 8 Ko ([T2] MDA p. 1), ce qu'épouse `loadfont` (`video.c:939-959`) :
   rangées 0-7 de la MDA, puis 8-15, puis les deux polices de la CGA.
9. **PB-35** (*documenté*). Le classement (c) tient. Les citations de FreeVGA sont exactes ; elles
   viennent d'une source secondaire.
10. **PB-36** (*documenté*).
    - Le classement (d) tient.
    - La M24 manque à l'entrée (`vid_olivetti_m24.c:104-121`, marqueur `vid_olivetti_m24.cs:153`).
    - Le PC1512 n'est pas concerné : ses temps sont des constantes (`vid_pc1512.c:143-155`,
      `vid_pc1512.cs:183`).
    - La remarque sur `TIMER_USEC` est juste.
11. **PB-37** (*documenté*). La correction ne se réduit pas à « avancer p ». Il faut brancher dans la
    fonction : `vid_svga.cs:966-969` compare le délégué. Le décompte de 368 908 appels est exact
    (`PCEM_BUGS.md:2583`).
12. **PB-38** (*documenté*). Le défaut est sans effet : `ma` est rechargé à chaque ligne
    (`vid_svga.c:561-579`). M19 ne l'a jamais atteint (VERIFICATION.md, « Jamais atteints :
    16bpp_lowres »).
13. **PB-98** (*documenté*). L'EGA a le même défaut (`vid_ega.c:1068-1073`).
14. **Défauts non inscrits du § 4, confirmés** (*documenté*) :
    - 3BAh bit 3 : [T1] p. 2-42/43, [T2] MDA p. 8, manuel GB101 pp. 12-13 ;
    - la polarité du bit 7 de l'Hercules : le manuel (pp. 12-13) dit « 0 = vertical retrace … 1 =
      active display », et `vid_hercules.c:91` fait l'inverse ;
    - la M24 et le PC1512 en 40 colonnes : `vid_olivetti_m24.c:209`, `vid_pc1512.c:237` ;
    - la relecture de R0-R13 par la CGA, la M24 et le PC1512 : `vid_cga.c:64-65` ;
    - CR22 de la GD5429 ;
    - l'IRQ 2 de l'EGA.
15. **R2** (*documenté*, recompté par la commande de R2). Les ratios du rapport sont justes :
    `vid_unk_ramdac.cs` 1,14 (74 lignes C# contre 65), soit 7 lignes de marge ; `vid_svga.cs`
    1,31.

---

## 2. Corrigé

1. **PB-09 n'est pas un (d).** C'est un R9, à corriger maintenant : voir § 0.
   - Le « chemin chaud : non » ne vaut que pour la voie 512 ; c'est elle que R9 désigne.
   - « Texte 80 colonnes » veut dire en fait 3D8h bit 0 = 1.
2. **PB-04 : le vrai comportement est documenté, pas « inconnu »** ([T3], feuille 1, p. 28,
   portes U49 LS02, U13 LS08, U14 LS32 et U28 LS10). La sortie s'écrit
   `+ALPHA DOTS = NOR(-CURSOR BLINK, -CURSOR DLY) OR (+CHG DOTS AND (-BLINK OR NAND(-CURSOR DLY,
   +ENABLE BLINK, +AT7)))`. On en tire :
   - sur ses lignes, la cellule du curseur n'est **jamais** éteinte par le clignotement, quelle
     que soit la phase du curseur ;
   - le curseur **force les points à 1** : un pavé plein de la couleur d'avant-plan.

   Conséquences :
   - Le signal s'appelle « -CURSOR DLY », pas « +CURSOR DLY », et la topologie est lisible.
   - « Lire la locale » ne suffit pas en mode matériel. La locale contient `cursoron`, la phase du
     curseur. Elle n'équivaut au matériel que tant que curseur et caractères clignotent sur le même
     bit, ce qui est faux sur la vraie carte (point 4). L'exemption doit être
     `(ma == ca) && con`.
   - **PB-04 passe de (b) à (a).**
3. **§ 4, n° 3, le curseur en XOR : documenté, pas déduit** ([T3] feuille 1).
   - La MDA a la même logique ([T4], feuille 5) : U3 74LS08 fait CURSOR BLINK ∧ +CURSOR DLY, qui
     entre dans le OU U43 74S32 et forme +ALPHA DOTS.
   - PCem fait un XOR pour les deux cartes : `vid_cga.c:171-177` et `vid_mda.c:128-131`.
   - MAME, cité par le rapport ([S11]), est secondaire ; il rejoint le schéma pour le pavé plein.
4. **§ 4, n° 4, le rythme du clignotement : la phrase est fausse, et le défaut va dans les deux
   sens.**
   - Chez PCem, chaque carte prend le même bit pour le curseur et les caractères. C'est le bit 3
     pour la CGA (`vid_cga.c:165`, `:198`, `:346`). C'est le bit 4 pour la MDA, l'Hercules, la M24
     et le PC1512 (`vid_mda.c:111`, `:187` ; `vid_hercules.c:163`, `:246` ; M24 `:177`, `:353` ;
     PC1512 `:205`, `:366`).
   - Sur la CGA ([T3], feuille 3, U12 LS393), *documenté* : -CURSOR BLINK est la sortie QD d'un
     compteur 4 bits que +V SYNC DLY fait avancer ; -BLINK est la sortie QA du second, que QD fait
     avancer. La MDA est câblée de même ([T4], feuille 5, U28).
   - Le curseur a donc une période de 16 trames, les caractères de 32 :
     - les caractères de la CGA clignotent **deux fois trop vite** chez PCem ;
     - le curseur de la MDA, et celui de l'Hercules qui la recopie, clignote **deux fois trop
       lentement**.
   - MAME a les bons rythmes, mais son `else if` n'exempte la cellule que pendant que le curseur
     est dessiné ; le schéma l'exempte toujours.
   - L'Hercules a son propre matériel : son rythme est *inconnu*. Le manuel dit seulement que le
     clignotement de 3B8h « has no effect on the cursor ».
5. **PB-97 (c) : documenté, pas déduit** ([T1]).
   - SET_CTYPE fait `MOV AH,10 ; MOV CURSOR_MODE,CX ; CALL M16`, et M16 « OUTPUTS THE CX REGISTER
     TO THE 6845 REGS NAMED IN AH ».
   - VIDEO_PARMS : 0Bh/0Ch pour le mode 7, 06h/07h pour la CGA.
   - Le curseur 0607h tombe donc à mi-cellule sur une vraie MDA.

   **La table de masques « celle de la CGA » n'est pas celle du MC6845.** `crtcmask` met R16 et R17
   à FFh (`vid_cga.c:16-17`), alors que le data sheet les dit en lecture seule ([T9]). La table du
   mode matériel se construit depuis [T9], sans recopie. (Référence : `crtcmask` est en
   `vid_cga.cs:77-79`, pas `:76-77`.)
6. **PB-99 (6) : le défaut est bien plus étroit qu'écrit**, dans le registre comme dans le rapport.
   - Le rendu est borné par `vrammask = vram_limit − 1` (`vid_ega.c:263-264`, `:324`, `:337`,
     `:1056-1057`).
   - La seule fuite vient des substitutions de rangée de CR17, appliquées après le masque
     (`:344-347`, `:405-408`, `:477-480`). Avec 64 Ko, seul `0x10000` (CR17 bit 1 = 0) sort.
     Aucun mode du BIOS d'IBM ne pose ce bit à 0 (*déduit*, tables à vérifier dans [T2]).
   - Le commentaire du C# était déjà exact (`vid_ega.cs:1190-1193`).
   - « Documenté : avec 64 Ko, le compteur reboucle sur MA13 » lit mal [T2] EGA p. 42. La page
     donne une règle de programmation : choisir MA13 par CR17 bit 5 quand 64 Ko sont installés.
     Le repliement des adresses au-delà de la mémoire présente est *déduit*.
   - Classement (b), portée minime.
7. **PB-100 (5), le bit 2 de SR7 : (c), pas (d).**
   - « Set to 1 (always) » ([T6] p. 142) décrit un registre « read/write » : c'est une règle de
     programmation, pas un bit câblé.
   - Le relevé FCh d'ET4BANC est la valeur de l'oracle, pas une mesure.
   - Si la phrase voulait dire « câblé », PCem devrait aussi forcer le bit 4 en révision E (même
     page) ; il ne le fait pas (`vid_et4000.c:269-271`).
   - La valeur relue reste *inconnue*. Ce n'est pas « un non-défaut documenté ».
8. **PB-100 (6), FFh : ce n'est pas « 1 ligne ».** FFh pose D4, l'ERPF ([T7] table 10). La puce
   envoie alors 3C7h-3C9h vers les registres étendus, jusqu'à ce que l'ERPF retombe. FFh pose aussi
   le mode réservé 111. La correction fidèle exige le modèle de l'ERPF : (c), ou (b) avec l'ERPF.
9. **PB-100 (3) : (b), pas (a).**
   - Le data book déconseille la carte de 128 Ko en modes étendus ([T6] p. 85, « should not be
     used » ; pp. 150-151, « should not be programmed »). Son effet sur les segments de 3CDh n'est
     pas documenté. Le 0x1FFFF de PCem est son propre choix.
   - Le masque nul à l'amorçage ne se voit pas : `svga_out` ne change la carte que si les bits 3-2
     changent (`vid_svga.c:175-195`), et le BIOS pose un mode avant tout accès.
   - La page de GDC 6 est pp. 150-151, pas p. 150.
10. **PB-100 (4) : la liste de ce que protège le KEY est incomplète** ([T6]). Il manque :
    - ATC 16h, « protected by KEY » (p. 158). C'est là que vivent les bits HiColor que lit
      `vid_et4000.c:401`.
    - La relecture du bit 7 du Feature Control, 3CAh (p. 104).

    Et 3CDh n'est accessible qu'après une première pose du KEY (p. 144).
11. **PB-100 (7), formulation exacte** ([T7] tables 2, 5, 9 et 10).
    - Les modes à 4 octets existent, mais ils exigent le registre de repack (étendu 10h, atteint par
      l'ERPF).
    - D5 choisit les fronts d'horloge (1a/1b, 3a/3b), pas « 24 ou 32 bits ».
    - PCem invente aussi un décodage du code réservé 111 : 16, 24 ou 32 bpp selon D2 et D5
      (`vid_unk_ramdac.c:45-58`).
12. **PB-37, trois précisions** (*documenté*).
    - Le « diff attendu : seul `#buffer32` » est fragile. Le rendu (`vid_svga.c:511`) précède le
      rechargement de `ma` (`:561-579`) : une sonde prise dans la phase « off » voit `ma` différer.
      Le rapport le note pour PB-38, pas ici.
    - Seule la branche sans remappage passe à `x += 4`. L'autre l'a déjà
      (`vid_svga_render.c:707` contre `:720`).
    - L'atteinte est synthétique : TKD8001 piloté à la main sur le mode 5Dh (VERIFICATION.md,
      § M19, étape 7).
13. **Section A ou C : un même critère pour PB-37 et PB-35.**
    - Si PB-37 monte en A parce que son effet se voit dès qu'on l'atteint, PB-35 a un effet que
      l'invité lit aussi.
    - L'entrée PB-35 affirme sans source « Un vrai DAC lirait à l'index de LECTURE »
      (`PCEM_BUGS.md:2508`), ce que contredit son classement (c). C'est à corriger dans le
      registre.
    - Il faut, ou les deux en A, ou un critère de C réécrit : « écart sans comportement matériel
      connu ».
14. **PB-80, la portée** (*documenté*).
    - « Seule la GD5429 l'atteint » ne vaut que pour la lecture d'octet inscrite. Les lectures de
      mot et de double mot du chemin `fast` sautent les verrous aussi, l'ET4000 compris (§ 3,
      n° 2).
    - Le registre dit « atteint par les cartes de G7 », ce qui est inexact. La Trio64 ne pose ni
      `packed_chain4` ni `fb_only` : aucun `vid_s3.c` dans `grep packed_chain4`.
15. **PB-102, la vérification.** `--menu-check` borne aujourd'hui la MDA et l'Hercules entre 18 et
    21 kHz (`Host/SdlMenu.cs:1770-1774`). Ces bornes acceptent 20,74 comme 18,43 : il faut les
    resserrer selon le mode.
16. **PB-96 : (a) pour la MDA, (b) pour l'Hercules.** Le schéma de l'Hercules n'a pas été lu : le
    rapport le dit lui-même.
17. **La dépendance à GR est surestimée** (*déduit*).
    - « Seul le socle SVGA se vérifie aujourd'hui » (§ 0.2) contredit le rapport lui-même : il
      propose des vérifications en C# seul pour PB-102, PB-97 (a) et (c), PB-89, PB-100 (2) et (3).
    - GR.0 sert à l'identité des pixels en mode PCem. Une image attendue du mode matériel se
      vérifie en C# seul, sur `Buffer32`, dès maintenant.
    - HERCBANC existe déjà (`tools/hercbanc`). Le POST de l'EGA, donc POD14, tourne dans les
      boot-diffs actuels.
18. **L'ordre décidé.**
    - PLAN.md place GR **après** G13 (`PLAN.md:427`, « Après G13 et avant la référence de
      performance figée de G14 »). Mettre V2 à V5 après GR.0 et GR.1 renverse une décision de
      Julien : il faut son feu vert, et le rapport doit le dire.
    - « Avant G13, … sur le chemin de GR.4 » (§ 4) est ambigu, puisque GR.4 vient après G13.
      Ces corrections du registre vont en V0, sauf PB-100 (5) (point 7).
19. **Les comptes du § 1.**
    - PB-97 est compté en (a) alors qu'il est mixte, comme PB-99 et PB-100.
    - PB-09 n'est pas un (d).
20. **§ 4, n° 10, les ports 3B0h-3B3h, 3B6h et 3B7h.** « Not Used » n'est pas « non décodé ».
    - IBM documente un décodage partiel pour la CGA : A1 et A2 « don't care » pour 3D4h/3D5h ([T2]
      CGA p. 15).
    - Pour la MDA, il faut lire le schéma ([S1] feuilles 1-2) avant de mesurer. PCem est
      probablement juste (*déduit*) : (c) faible.
21. **Une curiosité de [S5] p. 30.** Le bit 5 de 07h y est « Cursor Location Bit 8 » de l'index
    0Ah, un registre de 5 bits. Le prendre au mot n'aurait pas de sens : bit inutilisé (*déduit*).

---

## 3. Ajouté

1. **Un défaut non inscrit du socle SVGA, qui touche toutes les cartes : `htotal = CR00 + 6`**
   (`vid_svga.c:334`, « +6 is required for Tyrian » ; `vid_svga.cs:589`).
   - IBM : « total number of characters minus 5 » ([T5] p. 2-56), donc + 5. 86Box est passé à + 5
     en gardant le commentaire ([T10]).
   - *Effet mesuré par le dépôt* : la VGA balaye à 31,16 kHz et 69,39 Hz (VERIFICATION.md, § « L'hôte :
     le moniteur automatique »), au lieu de 31,47 kHz et 70,09 Hz. C'est 1 % de trop sur la ligne,
     pour 3DAh, les boucles d'attente et la fréquence verticale.
   - Classement (a), documenté, vérifiable en C# seul et par `--menu-check` aux bornes resserrées
     (VGA entre 31 et 32 kHz aujourd'hui).
   - Comme PB-102, il fait diverger tout boot-diff SVGA en mode matériel : un commutateur propre est
     nécessaire.
   - Risque : « Tyrian » a dicté le + 6. Il faut savoir quel autre écart ce + 6 compensait.
   - Il entre en V1 (socle SVGA), après inscription.
2. **La famille de PB-80 est plus large : le chemin `fast`.**
   - `fast` (`vid_svga.c:110-111`, `:203-204`) ne teste que GR8 = FFh, GR3 bits 3-4 et GR1. Il ne
     teste ni le mode d'écriture (GR5), ni SR2, ni la rotation (GR3 bits 0-2).
   - Quand `fast` est vrai, les écritures de mot et de double mot posent l'octet du processeur tel
     quel (`:1471-1527`). Les lectures ne chargent aucun verrou, par banque (`:1530-1570`) comme en
     linéaire (`:1623-1658`). La GD5429 y passe aussi (`vid_cl5429.c:760-771`).
   - L'ET4000 l'atteint : `packed_chain4` vaut 1 en permanence (`vid_et4000.c:496`). Un
     `REP MOVSW` en mode 1 ou sous SR2 partiel diverge alors de deux écritures d'octet.
   - Vrai comportement : le chargement des verrous est *documenté* ([T8] § 6.27) ; qu'un cycle de
     mot vaille deux cycles d'octet est *déduit*. Classement (a) ou (b). Chemin chaud : oui.
   - À inscrire, ou à rattacher à PB-80.
3. **Le bit 3 de 3BAh demande le mécanisme neuf de PB-99 (4).** [T4], feuille 5 : +B & W VIDEO =
   +ALPHA DOTS ⊕ RVV (U54 74S86). C'est le point vidéo au passage du faisceau, pas un bit d'état à
   retourner : (b). Risque : un programme qui attend le retour vertical sur le bit 3, par habitude
   de la CGA, change de comportement, même sur un écran vide.
4. **L'ET4000 et 3DAh** ([T6] p. 103, *documenté*).
   - Le bit 7 est le « vertical retrace complement », 1 pendant l'affichage. PCem rend 0 :
     `svga_in` renvoie `cgastat` (`vid_svga.c:262-269`).
   - Les bits 4-5 sont la rétroaction vidéo par AR12, où PCem bascule.
   - Sur la VGA d'IBM, ces bits sont « Undefined on Read » ([T5] p. 2-45) : l'écart ne vaut que pour
     l'ET4000. Classement (b).
5. **L'EGA : Input Status 0 bit 7** ([T2] EGA p. 15, « CRT Interrupt — A logical 1 indicates video
   is being displayed … 0 … vertical retrace »). PCem rend 0 en permanence : 3C2h ne rend que le
   bit 4 (`vid_ega.c:155-167`). C'est distinct de l'IRQ 2 absente. Classement (b) : l'état exact
   quand l'interruption est désactivée est *inconnu*.
6. **Une contrainte pour V3.** Juste avant les points, le POST de l'EGA mesure la trame au
   chronomètre 0, entre « MAXIMUM VERTICAL TIMING » et « MINIMUM VERTICAL TIMING » ([T2] EGA
   pp. 113-114). Toute correction qui touche les temps de l'EGA doit rester dans ces bornes.
7. **Sierra, deux écarts non inscrits** ([T7] pp. 3-88/3-89, *documenté*).
   - L'IPF ne retombe qu'à la mise sous tension, à une écriture, ou à une lecture d'une **autre**
     adresse. Les lectures répétées de 3C6h rendent donc toujours le registre de commande. PCem
     désarme après une lecture (`vid_unk_ramdac.c:84-87`) : (b).
   - L'ERPF et les registres étendus ne sont pas modélisés : ID 09h-0Ch (« S », « : », B1h, « A »),
     masques secondaires 0Dh-0Fh, repack 10h. D3 non plus, le contournement de la palette en
     HiColor. Classement (c), ou (b) avec l'ERPF.
   - La puce exacte de la carte modélisée reste *inconnue* (`vid_unk_ramdac.c:4-7`).
8. **L'ET4000, d'autres écarts** ([T6]).
   - CR35 est « protected by bit 7 of CRTC 11 » (p. 111). PCem ne protège que < 7 et 7
     (`vid_et4000.c:83-87`) : (a).
   - Le « linear system » de CR36 bit 4 (p. 136) n'est pas modélisé. C'est une fonction : la
     phrase du rapport « l'ET4000 n'a pas de fenêtre linéaire » vaut pour PCem, pas pour la puce.
9. **Le MC6845, R16-R17 et l'index.** Les tables de PCem rendent R16 et R17 inscriptibles :
   `crtcmask` à FFh pour la CGA, la M24 et le PC1512, et aucun masque pour la MDA et l'Hercules. Le
   data sheet les dit en lecture seule ([T9]). Le registre d'index 3B4h/3D4h se relit chez PCem
   (`vid_cga.c:62-63`, `vid_mda.c:46-50`) ; IBM le dit « write-only » ([T2] CGA p. 15). Mineur :
   (a) pour R16-R17, (c) pour la valeur relue.
10. **R10 bits 5-6.** PCem ne traite que 01, « pas de curseur » (`vid_cga.c:343-346`). 10 et 11
    font clignoter le 6845 lui-même, en plus de la carte : « ERRATIC BLINKING » ([T1], en-tête de
    l'INT 10h ; [T9]). Le dessin exact est *inconnu* : (c).
11. **Le motif de PB-89 dans la MDA et l'Hercules** (*documenté*, lu).
    - MDA : `(x * 9) + c` monte à 2 294 quand R1 > 227 (`vid_mda.c:107-131`).
    - Hercules : 2 294 en texte ; en graphique, `(x << 4) + c` monte à 4 079 quand R1 > 127
      (`vid_hercules.c:150-156`).
    - L'EGA, elle, borne par `& 2047` (`vid_ega.c:300-320`).
    - Classement (b), avec PB-89.
12. **Le mode matériel en `static readonly`** (*déduit*).
    - Le précédent cité (`Buffer32`, `video.cs:118-124`) porte sur une longueur de tableau, pas sur
      un booléen replié par le JIT de niveau 1.
    - Pour que le drapeau et les commutateurs par PB soient des constantes, ils doivent être fixés
      avant l'initialisation de leur classe, donc au lancement.
    - Un outil qui compare les deux modes doit lancer deux processus.
13. **R3.** `TRANSCRIPTION.md` fait 218 lignes pour un plafond de 240 (`:3`) : l'amendement de
    R1(d) dispose de 22 lignes.
14. **R9 à repasser sur tout le registre**, au-delà de la vidéo. Une passe sur les entrées
    « reproduit » dont le C# lève éviterait un autre PB-09. Hors domaine, `Disc/disc_img.cs:454-455`
    (PB-17, « le C# lève ») se déclenche par une image XDF ED fournie par l'hôte, pas par l'invité :
    à trancher hors G13.

---

## 4. Le classement par PB, après correction

| PB | Rapport | Après contre-lecture | Motif |
|---|---|---|---|
| 04 | b | **a** | Logique documentée ([T3] f. 1) ; exemption `(ma == ca) && con`, sans `cursoron` |
| 80 | a | a | Inchangé ; élargir au chemin `fast` (§ 3 n° 2) ; « cartes de G7 » à corriger |
| 89 | b | b | Étendre à la CGA, la MDA et l'Hercules |
| 97 | a / b / c (compté a) | mixte : (a) masques **a** · (b) relecture **c** · (c) Turbo XT **a** · (d) entrelacé **b** | (c) documenté ([T1]) ; masques tirés de [T9], pas de `crtcmask` |
| 99 | mixte | (1) a · (2) a · (3) c · (4) b · (5) a · (6) **b, portée minime** · traits « bits 9 » a · CR0A b5 a | (6) borné par `vrammask`, fuite par CR17 seulement |
| 100 | (1) d · (2) b · (3) a · (4) b/c · (5) d · (6) b · (7) c | (1) d · (2) b · (3) **b** · (4) b (pose et portée complétées) / c (retrait) · (5) **c** · (6) **c** · (7) c | « Set to 1 » est une consigne ; l'ERPF pour FFh ; 128 Ko déconseillé ([T6]) |
| 102 | a | a | `--menu-check` à resserrer |
| 09 | d (pour G13) | **R9, hors G13, maintenant** | Voie 512 ; `r9-cga` et `r9-m24` ; série entière |
| 96 | a | **a (MDA) / b (Hercules)** | Schéma de l'Hercules non lu |
| 35 | c | c | Section et phrase du registre à corriger, en accord avec PB-37 |
| 36 | d | d | Ajouter la M24 |
| 37 | b | b | Diff attendu : `#buffer32` **et `ma`** |
| 38 | d | d | Sans effet même atteint (`ma` rechargé) |
| 98 | d | d | EGA : la VRAM et la ROM |
| *nouveau* `htotal` + 6 (SVGA) | — | **a** (commutateur propre ; risque Tyrian) | [T5] p. 2-56 ; 31,16 kHz mesurés |
| *nouveau* chemin `fast` (mot, double mot) | — | a/b | [T8] § 6.27 ; ET4000 et GD5429 |
| *nouveau* curseur plein, CGA et MDA | — (« déduit ») | **a** | [T3] f. 1, [T4] f. 5 |
| *nouveau* rythmes de clignotement | — (« déduit ») | **a** (CGA, MDA) · c (Hercules) | [T3] f. 3, [T4] f. 5 |
| *nouveau* 3BAh bit 3 (MDA, Hercules) | — | b | Mécanisme du faisceau |
| *nouveau* 3BAh bit 7 (Hercules) | — | a | Manuel GB101 pp. 12-13 |
| *nouveau* ET4000 3DAh bits 7 et 4-5 | — | b | [T6] p. 103 |
| *nouveau* EGA, Input Status 0 bit 7 | — | b | [T2] EGA p. 15 |
| *nouveau* Sierra IPF / ERPF / D3 | — | b / c | [T7] pp. 3-88 à 3-91 |
| *nouveau* ET4000 CR35 sous CR11 b7 | — | a | [T6] p. 111 |
| *nouveau* R16-R17 inscriptibles, index relu | — | a / c | [T9] ; [T2] CGA p. 15 |
| *nouveau* R10 bits 5-6 | — | c | [T1], [T9] |
| *nouveau* M24/PC1512, exemption en 40 col. | — (§ 4 n° 5) | b | Intention de PCem ; matériel inconnu |
| *nouveau* MDA 3B0h-3B3h, 3B6h, 3B7h | — (§ 4 n° 10) | c, faible | Schéma à lire |
