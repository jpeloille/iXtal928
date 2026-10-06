# G13 — La vidéo : reconnaissance pour le plan (D4)

Le 6 octobre 2026. Lecture seule du dépôt (`master`, `bc609ce`) : rien de modifié, rien de lancé
hormis des `grep` et le comptage de R2. Périmètre : PB-04, PB-80, PB-89, PB-97, PB-99, PB-100,
PB-102 (section A) ; PB-09, PB-96 (section B) ; PB-35, PB-36, PB-37, PB-38, PB-98 (section C).
Cartes : CGA, MDA, Hercules, EGA, socle SVGA, Trident 8900D/9000B, GD5429, Trio64, ET4000AX,
M24, PC1512.

**Niveaux** des affirmations sur le vrai matériel : *documenté* (une source primaire le dit),
*déduit* (raisonnement depuis des sources), *inconnu* (aucune source trouvée).
**Classements** : (a) corrigeable et vérifiable ; (b) corrigeable, vérification faible ;
(c) vrai comportement inconnu, laissé reproduit jusqu'à une mesure ; (d) à ne pas corriger.

---

## 0. Ce qu'il faut savoir d'abord

1. **PB-09 est un R9 actif, pas un sujet de G13.** En mode texte 80 colonnes, un R1 > 128
   fait lever une `IndexOutOfRangeException` dans `cga_poll`. Aucun `catch` ne la rattrape
   (`Program.cs`, `Host/SdlHost.cs`, `pc.cs` lus) : l'invité arrête l'émulateur
   (`o 3d4 1` / `o 3d5 c8` sous DEBUG suffit). Le commentaire du C# le dit lui-même
   (« le C# lève »). La M24 a le même défaut, déjà traité par R9 (PB-88).
2. **Vérifier le mode matériel vidéo demande surtout les outils de GR.0 et GR.1** : pixels des
   cartes non SVGA, MDABANC, EGABANC étendu, ET4BANC en HiColor, sonde `et4000_t`. Seul le
   socle SVGA (PB-80, PB-37) se vérifie aujourd'hui.
3. **PB-100 contient deux non-défauts documentés et une affirmation inexacte.** CR3F n'existe
   pas sur l'ET4000AX, et le bit 2 de SR7 est « Set to 1 (always) » d'après le data book Tseng.
   Enfin, la Sierra SC15025/26 a bien un format à 4 octets par point, contrairement à ce que
   dit l'entrée.
4. **PB-102 et PB-97 (masques, correctif « Turbo XT ») sont documentés et vérifiables**, et
   l'effet du « Turbo XT » s'atteint avec des logiciels ordinaires. Mais corriger PB-102 change
   toutes les temporisations de la MDA et de l'Hercules. Tout boot-diff de ces cartes diverge
   alors dès le début, et il faut pouvoir activer les corrections une à une pour vérifier les
   autres.
5. **PB-99 est hétérogène et incomplet.** Deux sites ne portent pas le marqueur, et des
   traits VGA ne sont pas inscrits (CR07 bits 5-7, CR09 bit 6, CR0A bit 5). La partie 3DAh
   demande un modèle neuf de la position du faisceau, et il doit passer le test POD14 du BIOS
   EGA d'IBM.
6. **Marqueurs** : PB-04 et PB-09 n'ont pas leur identifiant dans le code. Le second site de
   PB-04 n'a aucun marqueur, et les numéros de ligne de PCEM_BUGS.md sont périmés.
7. **Défauts non inscrits trouvés en route** (§ 4) : 3BAh bit 3 de la MDA et de l'Hercules,
   polarité inversée du bit 7 de l'Hercules, modèle du curseur CGA, rythme de clignotement CGA,
   entre autres.

---

## 1. Tableau récapitulatif

| PB | Titre court | Effet pour l'invité | Classement | Chemin chaud | Vérification du mode matériel |
|---|---|---|---|---|---|
| 04 | CGA : `drawcursor` lu dans le champ | Le caractère clignotant sous le curseur perd son avant-plan sur les lignes du curseur | **b** | oui (rendu texte CGA, par caractère) | image (GR.0) ; faible tant que le modèle du curseur n'est pas mesuré |
| 80 | Chain4 compact : verrous non chargés | Écriture qui combine les verrous (mode 1…) après une lecture : verrous périmés (GD5429) | **a** | oui (lecture CPU de la VRAM) | C# seul (doc. Cirrus) + diff attendu sur le seul champ `la\|lb\|lc\|ld` |
| 89 | M24/PC1512 : la bordure déborde sur la ligne suivante | Points sur la ligne d'en dessous si R1 hors BIOS | **b** | rendu, borne par ligne | C# seul (ligne n+1 intacte) ; pas d'image de référence |
| 97 | 6845 MDA/Hercules : masques, relecture, « Turbo XT », entrelacé | Curseur 0607h en 11-12 au lieu de 6-7 ; valeurs hors bornes ; relecture | **a** (masques, Turbo XT) / **b** (entrelacé) / **c** (valeur relue) | non (sauf entrelacé, par ligne) | C# seul (datasheet) + image (GR.0) + diff attendu sur MDABANC (GR.1) |
| 99 | EGA aux traits de VGA | Couleurs (AC10 b7), écritures CR0-7 ignorées, relectures, 3DAh, curseur en retard, mémoire | **a** (AC10/14, CR11, rafraîchissement du texte) / **b** (3DAh, mémoire) / **c** (valeur des registres en écriture seule) | oui pour le rendu du texte et l'adresse d'affichage | POST IBM (POD14), EGABANC, images (GR.0), boot-diff 64 Ko (GR.1) |
| 100 | ET4000AX et RAMDAC | CR13 = 0, fenêtre de 128 Ko, KEY, FFh armé, profondeurs | **a** (fenêtre) / **b** (CR13, KEY ouvert, FFh) / **c** (KEY fermé, 32 bits) / **d** (CR3F, SR7) | non | C# seul + diff attendu sur ET4BANC étendu (GR.1) |
| 102 | MDA/Hercules : caractère de 8 points | Balayage 20,74 kHz / 56 Hz au lieu de 18,43 / 50 ; graphique Hercules ×2 | **a** | non | C# seul (périodes contre IBM et Hercules) + `--menu-check` |
| 09 | CGA : `charbuffer` débordable | **Arrêt de l'émulateur** (exception) | **d** pour G13 (R9, les deux modes, maintenant) | non | C# seul (`r9-cga` : survit) |
| 96 | MDA/Hercules : police au-delà de 16 lignes | Bas de cellule = haut du caractère suivant si R9 > 15 | **a** | rendu, par caractère (négligeable) | image (GR.0) avec MDABANC (GR.1) |
| 35 | DAC : `vgapal[-1]` après `OUT 3C8h,0` | Deux composantes lues valent 0 (fin de `pallook`) | **c** | non | mesure par DAC |
| 36 | Temps « hors affichage » négatif | Aucun | **d** (sans effet) | non | — |
| 37 | Rendu 24 bpp basse résolution figé | Image fausse (8 points par ligne) — atteint par la campagne 8900D | **b** | rendu (ce seul mode) | image calculée + diff attendu sur `#buffer32` |
| 38 | Rendu 16 bpp basse résolution : `ma` deux fois | Aucun | **d** (sans effet) | — | — |
| 98 | Hercules : projection laissée à la fermeture | Aucun (côté hôte) | **d** (sans effet) | — | — |

**Comptes par classement principal** : (a) 4 (PB-80, PB-96, PB-97, PB-102) ; (b) 3 (PB-04,
PB-89, PB-37) ; (c) 1 (PB-35) ; (d) 4 (PB-09 pour R9, PB-36, PB-38, PB-98) ; mixtes 2 (PB-99,
PB-100). Par sous-défaut :
- PB-97 : a 2, b 1, c 1 ;
- PB-99 : a 4, b 2, c 1 (traits non inscrits en plus) ;
- PB-100 : a 1, b 3, c 2, d 2.

---

## 2. Sources

- **[S1]** IBM, *IBM Monochrome Display and Printer Adapter*, Personal Computer Hardware
  Reference Library, n° 6361511.
  - p. 1 : « designed around the Motorola 6845 CRT Controller module ».
  - p. 2 : « Supports 9-PEL by 14-PEL character box », « Has 18-kHz monitor ».
  - p. 3 : schéma bloc, « RA (4) » vers le générateur de caractères.
  - p. 5 : table des registres du 6845 et valeurs : 61, 50, 52, F, 19, 6, 19, 19, 02, D, B,
    C.
  - p. 7 : 3B4h et 3B5h pour le 6845 ; 3B0h à 3B3h, 3B6h, 3B7h « Not Used ».
  - p. 8 : 3BAh, bit 0 « +Horizontal Drive », bit 3 « +Black/White Video ».
  - Schémas : feuille 2 (p. 20, MC6845 U35, sorties RA0-RA3) ; feuille 3 (p. 21, oscillateur
    16,257 MHz) ; feuille 5 (p. 23, logique du clignotement).
  - <https://minuszerodegrees.net/oa/OA%20-%20IBM%20Monochrome%20Display%20and%20Printer%20Adapter.pdf>
- **[S2]** IBM, *IBM Color/Graphics Monitor Adapter*, n° 6361509.
  - p. 4 : schéma bloc ; la carte lit le tampon d'affichage par des verrous, sans tampon de
    ligne.
  - p. 5 : « Motorola 6845 ».
  - p. 15 : le registre d'index, « write-only ».
  - p. 17 : « 6845 Register Description » ; R0-R13 « Write Only », R14-R15 « Read/Write »,
    R16-R17 « Read Only ».
  - p. 21 : registre d'état 3DAh.
  - p. 28 : schéma feuille 1 ; « +ALPHA DOTS » formé de -CURSOR BLINK, +CHG DOTS, -BLINK,
    +CURSOR DLY, +ENABLE BLINK et +AT7.
  - <https://minuszerodegrees.net/oa/OA%20-%20IBM%20Color%20Graphics%20Monitor%20Adapter%20(CGA).pdf>
- **[S3]** Motorola, data sheet *MC6845 CRT Controller*, transcription de K. Thacker :
  - table des registres (accès, largeur : R4, R6, R7 sur 7 bits, R5, R9, R11 sur 5, R8 sur
    6, R10 sur 7, R12 et R14 sur 6) ;
  - modes d'entrelacement de R8, avec « R9 must be odd » et « The even numbered scan lines
    are displayed in the even field and the odd numbered scan lines … in the odd field » ;
  - R10 bits 5-6 ;
  - « Any 8-bit number may be programmed as long as the contents of R0 are greater than the
    contents of R1 ».
  - <https://cpctech.cpcwiki.de/docs/mc6845/mc6845.htm>
- **[S4]** K. Thacker, *The 6845 CRTC*. Source secondaire : mesures sur les CRTC de l'Amstrad
  CPC. « On type 0 and 1, if a Write Only register is read from, '0' is returned. » Rien sur
  le MC6845 (type 2). <https://cpctech.cpcwiki.de/docs/crtcnew.html>
- **[S5]** IBM, *IBM Enhanced Graphics Adapter*, Technical Reference, 2 août 1984.
  - p. 9 : chaînage des plans 0-1 et 2-3, « only when necessary (less than 128K) ».
  - pp. 15-17 : Input Status Register One, bits 4-5 « Diagnostic Usage », table du
    multiplexeur.
  - pp. 30-31 : registre 07h, bits 0-5 (bit 5 « Cursor Location Bit 8 »), bits 6-7 « Not
    Used ».
  - p. 32 : 09h, bits 0-4 seulement ; 0Ah, bits 0-4, et 5-7 « Not Used ».
  - p. 33 : 0Bh, bits 5-6 d'avance du curseur ; 0Ch « read/write ».
  - pp. 35-37 : à l'index 10h, Vertical Retrace Start en écriture et Light Pen High en
    lecture ; 11h, bit 4 « Clear Vertical Interrupt », bit 5 « Enable Vertical Interrupt »,
    bits 6-7 « Not Used » ; Light Pen Low en lecture.
  - p. 42 : 17h bit 5, « Address Wrap… MA 13 should be selected when the 64K memory is
    installed ».
  - pp. 56-61 : le contrôleur d'attributs, index 00h-13h seulement ; 10h bits 4-7 « Not
    Used » ; 12h « Video Status MUX » ; tous ces registres en écriture seule.
  - pp. 112-114 : listing du BIOS, POD14 : « SEE IF RED, GREEN, BLUE AND INTENSIFY DOTS WORK »
    par 3DAh bits 4-5, et sinon « ONE LONG AND THREE SHORT ».
  - <https://www.minuszerodegrees.net/oa/OA%20-%20IBM%20Enhanced%20Graphics%20Adapter.pdf>
- **[S6]** Hercules Computer Technology, *Hercules Graphics Card GB101 – Owner's Manual*,
  section 2 « For Advanced Users ».
  - pp. 8-9 : 3B5h « In/Out ».
  - p. 10 : « 0.5625 microseconds per character in text mode and 1 microsecond per character
    in bit-mapped mode » ; « In bit-mapped mode, one character is 16 dots wide and 4 scans
    tall. In text mode, it is 9 dots wide and 14 scans tall » ; une ligne d'environ 54 µs.
  - p. 11 : 3B8h bit 5, « This blinker has no effect on the cursor ».
  - pp. 12-13 : 3BAh, bit 0 SYNC, bit 3 « 1 = dots on », bit 7 « 0 = vertical retrace ».
  - <https://minuszerodegrees.net/manuals/Hercules/Hercules%20Graphics%20Card%20GB101%20-%20Owner's%20Manual.pdf>
- **[S7]** Tseng Labs, data book *ET4000 Graphics Controller*, 1990.
  - p. 102 : le KEY, « Write 03 to Hercules Compatibility Register (3BF); Write A0 to Mode
    Control Register (3#8) ».
  - p. 111 : table 4.3-2 ; index CRTC 00h-18h et 32h-37h seulement, tous R/W ; le KEY pour
    écrire au-delà de 18h, sauf 33h et 35h (35h protégé par CR11 bit 7).
  - p. 123 : 13h Row Offset, une valeur ajoutée à chaque rangée, sans cas particulier.
  - pp. 136-137 : 36h et 37h « protected by key ».
  - p. 142 : TS 7, bit 2 « Set to 1 (always) », bit 4 idem en révision E.
  - p. 144 : 3CDh, « the KEY must be set at least once after each power-on reset ».
  - p. 150 : GDC 6, « 00 = Hex A0000 for 128K bytes ».
  - <https://www.bitsavers.org/components/tsengLabs/Tseng_Labs_ET4000_Graphics_Controller_1990.pdf>
    (texte : <https://archive.org/details/bitsavers_tsengLabsTicsController1990_11230195>)
- **[S8]** Sierra Semiconductor, data sheet *SC15025/SC15026 HiCOLOR-24*.
  - p. 3-83 : le registre d'adresse a 8 bits.
  - p. 3-84 : table 2, modes de « repack » ; 3a et 3b font 4 octets → 24 bits, le quatrième
    jeté ; registre étendu 10h.
  - p. 3-86 : table 3 ; D7 D6 D0 = 000 pour 8 bits, 100 et 101 pour 15 bits, 110 pour 16 bits,
    010 et 011 pour 24 bits RVB et BVR, 001 et 111 réservés.
  - pp. 3-88/89 : l'IPF se pose par quatre lectures consécutives de 3C6h ; « the next
    microprocessor write … will cause the data … to be directed to the command register » ;
    l'IPF retombe à toute écriture, ou à toute lecture d'une autre adresse ; ERPF = D4.
  - <https://www.bitsavers.org/components/sierra/SC15025_15026.pdf>
- **[S9]** Cirrus Logic, *CL-GD542X Technical Reference Manual*, janvier 1994, § 6.27 « CR22:
  Graphics Data Latches Readback Register » : « These latches are loaded whenever display memory
  is read by the CPU ». Le manuel décrit aussi « Enable Eight Byte Data Latches ».
  <https://archive.org/details/bitsavers_cirrusLogichnicalReferenceManualJan1994_24841698>
- **[S10]** J. Neal, *FreeVGA — VGA Color Registers*. Source secondaire. Entrelacer lectures et
  écritures du DAC « may produce unexpected results » ; la lecture de 3C8h est « dependent on
  the particular DAC implementation ».
  <https://web.stanford.edu/class/cs140/projects/pintos/specs/freevga/vga/colorreg.htm>
- **[S11]** MAME, `src/devices/bus/isa/cga.cpp`. Source secondaire, un émulateur : curseur plein
  (`data = 0xff` si `m_framecnt & 0x08`) ; clignotement des caractères au bit 4 du compteur ;
  la cellule du curseur exclue par `else if`.
  <https://github.com/mamedev/mame/blob/master/src/devices/bus/isa/cga.cpp>
- **[S12]** 86Box, `src/video/vid_mda.c`. Source secondaire : garde le correctif « Turbo XT »,
  l'entrelacé `& 7` et l'absence de masques. Le successeur de PCem n'a donc rien corrigé ici.
  <https://github.com/86Box/86Box/blob/master/src/video/vid_mda.c>

**Pas consultés** : R. Wilton, *Programmer's Guide to PC & PS/2 Video Systems* (pas d'accès en
ligne fiable : aucune affirmation ne repose dessus) ; data sheet IMS G171, dans l'INMOS
*Graphics Databook* 1990, <http://www.transputer.net/ibooks/72-trn-204-01/graph2nd.pdf>
(13 Mo, non extrait ici, à lire pour PB-35) ; fiches Trident 8900D/TKD8001 et S3 Trio64 (aucun
PB de la liste ne leur est propre) ; IBM VGA Technical Reference (la doc Cirrus suffit à PB-80).

---

## 3. Les PB, un par un

### PB-04 — CGA : `drawcursor` lu dans le champ au lieu de la locale

1. **Défaut** : sur les lignes du curseur, la cellule du curseur n'est pas exemptée du
   clignotement. Le test lit `cga_t.drawcursor`, que rien n'écrit (toujours 0), au lieu de la
   locale.
   **Effet** : un caractère clignotant sous le curseur (attribut bit 7, 3D8h bit 5) perd son
   avant-plan pendant la phase éteinte. Le curseur, une inversion `^ 0xFFFFFF` ramenée à
   `^ 15` par le `& 0xf` de fin de ligne, montre alors sur ces lignes un pavé uni (le fond
   inversé) au lieu du glyphe inversé. Visible, rare.
2. **Sites** :
   - C : `vid_cga.c:165` (80 colonnes) et `:198` (40 colonnes) ; le champ en `vid_cga.h:27`,
     la locale en `:122`.
   - C# : `Video/vid_cga.cs:262`, marqueur en `:259-261` **sans l'identifiant PB-04** ;
     `:306`, **sans marqueur**. PCEM_BUGS.md cite `:249`, ligne périmée.
3. **Vrai comportement** :
   - *documenté* : les points « +ALPHA DOTS » de la CGA se forment de -CURSOR BLINK, +CHG
     DOTS, -BLINK, +CURSOR DLY, +ENABLE BLINK et +AT7 ([S2] p. 28). Le curseur entre dans la
     logique du clignotement, mais la topologie des portes n'est pas lisible sur le scan
     consulté.
   - *déduit* : c'est l'intention de PCem lui-même. La MDA (`vid_mda.c:111`), l'Hercules
     (`vid_hercules.c:163-164`), la M24 et le PC1512 en 80 colonnes testent la locale. MAME
     [S11] dessine un curseur plein de la couleur d'avant-plan et n'exempte la cellule que
     pendant la phase allumée du curseur.
   - *inconnu* : l'aspect exact sur une vraie CGA. À mesurer : IBM CGA dans un 5150 ou un
     5160, mode 3, attribut 8Fh sous le curseur, curseur 06-07 puis 00-07, carte de capture
     ou photo sur 64 trames.
4. **Correction** :
   - Quoi : lire la locale en mode matériel, aux deux sites ; 2 à 4 lignes.
   - Chemin chaud : **oui**, la boucle de rendu texte de la CGA (`cga_poll` écrit ~31 M fois
     par seconde émulée dans `Buffer32`) ; le test du drapeau se fait par caractère.
   - Coût nul si le drapeau est un `static readonly` que le JIT lit comme une constante
     (précédent : `Buffer32`, `video.cs:118-124`, VERIFICATION § M5.2).
   - Temps visibles : non. État sondé : aucun aujourd'hui ; GR.0 ajoute le hachage de
     `buffer32` de la CGA.
5. **Vérification** : une image après GR.0. Un banc pose un caractère clignotant sous le
   curseur, puis on échantillonne 32 trames ; l'image attendue se calcule hors de l'émulateur
   (police, palette). Elle reste faible tant que le modèle du curseur n'est pas tranché (§ 4).
6. **Classement** : **(b)**.
7. **Dépendances** :
   - le modèle du curseur et le rythme de clignotement de la CGA (non inscrits, § 4) ;
   - la même famille dans la M24 et le PC1512 en 40 colonnes (§ 4) ;
   - PB-09 et PB-89 (même fichier) ;
   - GR.0 (pixels de la CGA, vraie palette dans l'oracle).

### PB-80 — En chain4 compact, la lecture ne charge pas les verrous

1. **Défaut** : en `packed_chain4` ou `fb_only`, `svga_read_linear` rend l'octet sans charger
   les quatre verrous. La GD5429 fait de même, par la fenêtre linéaire **et** par la banque
   (`gd5429_read` appelle `gd5429_read_linear`).
   **Effet** : après une telle lecture, toute écriture qui combine les verrous lit ceux d'une
   lecture antérieure : mode 1 (copie), modes 0 ou 2 avec masque de bits ou fonction logique,
   mode 3.
   Dans iXtal, seule la GD5429 l'atteint, en modes compacts (SR7 bit 0 : modes 256 couleurs
   étendus du BIOS Cirrus, pilotes). L'ET4000 a `packed_chain4` mais pas de fenêtre linéaire ;
   la Trio64 n'a pas de `packed_chain4` ; la VGA d'IBM n'y passe pas.
2. **Sites** :
   - C : `vid_svga.c:1391-1395`, contre `svga_read` en `:1081-1090` ; `vid_cl5429.c:1183-1187`
     (`gd5429_read_linear`) et `:741-748` (`gd5429_read`).
   - C# : `Video/vid_svga.cs:1711` (bloc `:1712-1717`) ; `Video/vid_cl5429.cs:1322`.
3. **Vrai comportement** :
   - *documenté* pour la GD542x : « These latches are loaded whenever display memory is read
     by the CPU » ([S9] § 6.27), sans exception pour le mode compact.
   - *documenté* aussi : la GD542x **relit** ses verrous par CR22 (verrou choisi par
     GR4[1:0]). PCem ne le modélise pas : `gd5429_in`, `vid_cl5429.c:464-489`, rend
     `crtc[0x22]`.
   - *déduit* : les verrous chargés sont les quatre octets du mot qui contient l'octet lu
     (`addr & ~3`), comme la forme par banque de PCem (`vid_svga.c:1085-1089`). À confirmer
     pour les verrous de huit octets (GR0B).
4. **Correction** :
   - Quoi : charger `la` à `ld` depuis `addr & vram_mask & ~3` dans les deux branches
     compactes ; environ 5 lignes par site, 10 en tout.
   - Chemin chaud : **oui**, chaque lecture CPU de la VRAM en mode compact ; quatre lectures
     d'octet de plus en mode matériel, un test de drapeau en mode PCem (ou rien, avec un
     `static readonly`).
   - Temps visibles : non. État sondé : **oui**, le champ `la|lb|lc|ld` de la sonde VGA
     (163 champs).
5. **Vérification**, forte :
   - (i) *C# seul*, sur le patron de `r9-cl5429` : GD5429 amorcée, mode compact, lecture de
     l'octet n, écriture en mode 1 en m ; on attend VRAM[m..m+3] = VRAM[(n & ~3)..+3].
   - (ii) *diff attendu* : BLTBANC (ou un banc court) en mode matériel contre l'oracle ; seul
     `la|lb|lc|ld` diffère, tant que rien ne consomme les verrous.
   - (iii) en option, CR22 modélisé en mode matériel ([S9]), pour que le banc relise les
     verrous lui-même.
6. **Classement** : **(a)**.
7. **Dépendances** : aucune avec GR, la sonde SVGA existe. PB-81 et PB-82 (R9, mêmes
   fonctions de la GD5429) ; les verrous de huit octets.

### PB-89 — M24 et PC1512 : la bordure déborde sur la ligne suivante de `buffer32`

1. **Défaut** : R1 n'est pas masqué ; l'abscisse `c + (crtc[1] << 4) + 8` monte à 4 095 pour
   une ligne de 2 048 points.
   **Effet** : avec un R1 hors des modes du BIOS, des points de bordure et d'image tombent sur
   la ligne d'en dessous, à l'écran de l'hôte. Jamais hors du tableau ; l'état de la carte
   n'est pas touché.
2. **Sites** :
   - C, M24 : `vid_olivetti_m24.c:154-166`, `:218-231`, `:258-259`, `:279`, `hline` `:287-289`.
   - C, PC1512 : `vid_pc1512.c:182-193`, `hline` `:317-319`.
   - C# : `vid_olivetti_m24.cs:193`, `vid_pc1512.cs:228` et `:424`.
   - **Non inscrit, même motif** dans la CGA : `vid_cga.c:150-154`, boucles `:188-271`, `hline`
     `:275` et `:277`, conversion de fin de ligne `:280-294`.
3. **Vrai comportement** :
   - *documenté* : R1 doit rester sous R0 ([S3]), le 6845 n'est pas défini au-delà.
   - *déduit* : sur la vraie carte, ce qui dépasse la ligne balayée tombe dans le retour de
     ligne ; rien de la ligne n ne s'affiche en n+1.
   - *inconnu* : l'image réelle avec R1 > R0, qui dépend du moniteur.
4. **Correction** :
   - Quoi : borner les écritures à la ligne, avec une borne de boucle calculée une fois par
     ligne ; 10 à 15 lignes par carte, la CGA en plus si le motif est inscrit.
   - Chemin chaud : le rendu, mais la borne coûte une fois par ligne. Temps visibles : non.
     État sondé : `buffer32`, après GR.0.
5. **Vérification** : *C# seul* ; avec R1 = 255, la ligne n+1 de `Buffer32` reste intacte
   après le rendu de n. Pas d'image de référence.
6. **Classement** : **(b)**.
7. **Dépendances** : PB-88 (R9 de la M24, même R1), PB-36 (R1 > R0 + 1), PB-09 ; GR.0 (pixels
   de la M24 et du PC1512).

### PB-97 — Le 6845 de la MDA et de l'Hercules

Quatre défauts sous un seul numéro ; mieux vaut les traiter à part.

1. **Défauts et effets** :
   - **(a) Pas de masques** : chaque registre s'écrit sur 8 bits. Une valeur à bits hauts
     change la trame : R9 = 2Dh ne finit jamais la rangée (`sc == crtc[9]` n'est jamais vrai),
     R4 ≥ 80h ne finit jamais la trame. R14 se relit sur 8 bits.
   - **(b) Relecture** : R0-R13 se relisent ; un logiciel qui identifie la carte par relecture
     lit ce qu'il a écrit.
   - **(c) Correctif « Generic Turbo XT »** : R10 = 6 et R11 = 7 deviennent 0Bh/0Ch. Un
     logiciel écrit pour la CGA qui pose le curseur 0607h (INT 10h AH = 01h) voit sur la MDA
     un curseur en rangées 11-12 au lieu de 6-7. Atteignable avec des logiciels ordinaires ;
     aucune machine d'iXtal n'a le BIOS « Generic Turbo XT », que le correctif visait.
   - **(d) Entrelacé** (R8 = 3) : `(sc << 1) & 7` perd les rangées 8 et au-delà ; glyphes
     faux. Usage nul sur un moniteur MDA.
2. **Sites** :
   - C : `vid_mda.c:28` (écriture), `:29-34` (Turbo XT), `:55` (lecture), `:99-100`
     (entrelacé) ; `vid_hercules.c:54`, `:55-60`, `:89`, `:137-138`.
   - C# : `Video/vid_mda.cs:86` couvre (a) et (c), (b) dans le commentaire ; `:177` couvre
     (d). `Video/vid_hercules.cs:80` et `:181`.
   - Banc : `tools/hercbanc/hercbanc.py:202` relit les douze registres, et dépend de (b).
   - Hors liste, même famille : la CGA (`vid_cga.c:64-65`), la M24 (`vid_olivetti_m24.c:82-83`)
     et le PC1512 (`vid_pc1512.c:102-103`) relisent aussi R0-R13, masqués à l'écriture.
3. **Vrai comportement** :
   - (a) *documenté* : largeurs des registres du MC6845 ([S3]) ; la MDA est bâtie sur le 6845
     de Motorola ([S1] p. 1).
   - (b) *documenté* : R0-R13 « Write Only » ([S2] p. 17, [S3]). La **valeur rendue** est
     *inconnue* : le data sheet n'en dit rien, et [S4] (secondaire) mesure 0 sur des CRTC
     d'autres fabricants (types 0 et 1), rien pour le MC6845. À mesurer : une IBM MDA (MC6845
     ou HD46505SP en seconde source) et une Hercules GB101, sous DEBUG, `o 3b4 n` puis
     `i 3b5` pour n = 0 à 17, plus une lecture de 3B4h.
   - (c) *documenté* : R10 et R11 sont les rangées de début et de fin du curseur ([S3]).
     *Déduit* : le BIOS d'IBM recopie CX dans R10/R11, d'où un curseur en rangées 6-7 sur la
     vraie MDA (listing du BIOS du 5150 pas relu ici).
   - (d) *documenté* : en entrelacé « sync and video », les rangées paires vont à la trame
     paire et les impaires à l'impaire ([S3]), et quatre lignes RA vont au générateur de
     caractères ([S1] p. 3). *Déduit* : rangée = (2k + trame) & 15.
4. **Correction** :
   - (a) appliquer en mode matériel la table de masques du MC6845 (celle de la CGA,
     `crtcmask`, `vid_cga.cs:76-77`) ; environ 3 lignes par fichier ;
   - (b) laisser en l'état jusqu'à la mesure ;
   - (c) sauter la réécriture : 1 ligne par fichier ;
   - (d) `((sc << 1) | trame) & 15`, ce qui ajoute une bascule de trame à `mda_t` et à
     `hercules_t` (la CGA en a une, `oddeven`) ; environ 4 lignes par fichier.
   - Total : 15 à 20 lignes. Chemin chaud : non, sauf (d), une fois par ligne.
   - Temps visibles : (a) oui, quand un logiciel écrit hors bornes ; (c) et (d) non.
   - État sondé : le hachage `#crtc` des sondes MDA (17 champs, champ 0 = 4) et Hercules
     (champ 0 = 5).
5. **Vérification** :
   - (a) *C# seul* : écrire FFh dans R0-R15, comparer `crtc[]` aux largeurs du data sheet,
     relire R14 = 3Fh, puis contrôler une trame avec R4 = 99h, qui doit se comporter comme
     19h.
   - (c) *C# seul* : R10 = 6 et R11 = 7 restent 6 et 7. Puis l'image, après GR.0 : le curseur
     occupe les rangées 6-7.
   - (d) l'image, après GR.0.
   - *Diff attendu* sur MDABANC (prévu par GR.1 : « le correctif « Turbo XT » de PB-97 ») :
     seule l'écriture de R10/R11 diverge, et rien d'autre.
6. **Classement** : **(a)** pour (a) et (c) ; **(b)** pour (d) ; **(c)** pour la valeur relue.
7. **Dépendances** :
   - PB-96 (le masque de R9 borne `sc` à 31) ;
   - PB-102 et PB-36 (mêmes fonctions de temporisation) ;
   - les bits de 3BAh (§ 4) ;
   - GR.0 (pixels), GR.1 (MDABANC), GR.4 (le champ *Effet* de PB-97 à réécrire).

### PB-99 — L'EGA aux traits de la VGA

1. **Défauts et effets**, avec le vrai comportement :
   - **(1) Attribut 10h bit 7 et attribut 14h** (`:39-42`), et 10h bit 5, qui remet le
     défilement fin à 0 après la ligne de partage (`:613`).
     *Documenté* : l'EGA n'a que les index 00h-13h, et 10h bits 4-7 sont « Not Used »
     ([S5] pp. 56-61).
     Effet : couleurs fausses si un logiciel pose 10h bit 7, car les bits 4-5 de `egapal`
     viennent alors de 14h. Les bits 2-3 de 14h n'ont pas d'effet visible : `pallook16` et
     `pallook64` ignorent les bits 6-7 (`vid_ega.c:1017-1024`).
   - **(2) CR11 bit 7 protège CR0-CR7** (`:128`).
     *Documenté* : sur l'EGA, CR11 bits 6-7 sont « Not Used », et ses bits 4-5 servent
     l'interruption verticale ([S5] pp. 36-37).
     Effet : un logiciel écrit pour la VGA qui pose CR11 bit 7 puis écrit CR0-CR7 voit ses
     écritures ignorées chez PCem, appliquées sur l'EGA. Les temps changent.
   - **(3) Tous les registres se relisent** (`:152-179`).
     *Documenté* : seuls 0Ch-0Fh sont en lecture-écriture ; 10h et 11h rendent en lecture le
     crayon optique ([S5] pp. 33-37) ; le reste est « write-only ».
     La **valeur rendue par un registre en écriture seule** est *inconnue*. À mesurer : IBM
     EGA dans un XT ou un AT, lecture de 3C0h, 3C1h, 3C4h/3C5h, 3CEh/3CFh, 3D4h/3D5h pour
     chaque index, sous DEBUG.
   - **(4) 3DAh bits 4-5 basculent à chaque lecture** (`:182`, « Fools IBM EGA video BIOS
     self-test »).
     *Documenté* : deux des six sorties couleur, choisies par l'attribut 12h bits 4-5
     ([S5] pp. 16-17 et 60).
     *Documenté* aussi : le POST du BIOS d'IBM s'en sert. POD14 écrit une ligne d'espaces en
     vidéo inverse intensifiée, pose la palette 0Fh à 3Fh, puis, pour chacune des trois
     positions du multiplexeur, attend « point allumé » puis « point éteint », chacun en
     65 536 lectures au plus ; sinon un bip long et trois courts ([S5] pp. 112-114).
   - **(5) Le texte n'est redessiné que sur `fullchange`** (`:559`).
     *Déduit* : le matériel relit la mémoire à chaque trame.
     Effet : un curseur déplacé par CRTC 0Eh/0Fh, sans écriture en VRAM, apparaît jusqu'à
     16 trames plus tard (environ 0,27 s), par exemple dans un éditeur. Une police (SR3) ou un
     attribut 10h changés ne se voient qu'au rafraîchissement suivant.
   - **(6) La mémoire configurée ne borne que le processeur** (`:345-347`, `:406-408`,
     `:478-480`).
     *Documenté* : avec 64 Ko, le compteur reboucle sur MA13 ([S5] p. 42), et les plans se
     chaînent sous 128 Ko ([S5] p. 9).
     *Déduit* : les adresses d'affichage reviennent dans la mémoire installée ; PCem lit des
     octets qu'une petite carte n'a pas.
2. **Sites** :
   - C# : `Video/vid_ega.cs:139`, `:237`, `:264`, `:300`, `:692`, `:1190`.
   - **Non marqués** : `:759` (10h bit 5, pourtant cité par l'entrée) et `:766` (CR0A bit 5 =
     curseur éteint, `vid_ega.c:619`, cité par PLAN-G9 n° 6 mais absent de l'entrée).
   - **Non inscrits** : CR07 bits 5-7 et CR09 bit 6, lus comme les bits 9 de la VGA
     (`vid_ega.c:198-220`, C# vers `:320-345`). [S5] pp. 30-32 dit pourtant 07h bits 6-7
     « Not Used », 07h bit 5 « Cursor Location Bit 8 », 09h bits 5-7 « Not Used ».
   - **Absent de PCem** : l'interruption verticale de l'EGA (IRQ 2 par CR11 bits 4-5, Input
     Status 0 bit 7 ; [S5] pp. 14-15, 36-37, « Vertical Interrupt Feature » p. 72).
   - Bancs : `tools/egabanc/egabanc.py:211` (3DAh) et `:234` (relecture du CRTC).
3. **Vrai comportement** : voir le point 1.
4. **Correction**, par sous-défaut :
   - (1) `egapal[c] = attrregs[c] & 0x3f` ; ignorer 14h et 10h bits 5 et 7 ; environ
     3 lignes.
   - (2) ne pas protéger ; 1 ligne.
   - (3) attendre la mesure ; 10h et 11h rendraient alors le verrou du crayon optique, lui
     aussi *inconnu* sans crayon.
   - (4) échantillonner la couleur sur 6 bits au point du faisceau au moment de la lecture,
     d'après l'abscisse tirée du chronomètre de ligne et les indices de la ligne courante :
     un **mécanisme neuf**, 40 à 60 lignes, qui suppose (5).
   - (5) dessiner le texte à chaque ligne affichée ; 1 ligne.
   - (6) masquer l'adresse d'affichage par la mémoire installée ; environ 6 lignes dans les
     trois rendus.
   - Traits non inscrits : environ 6 lignes.
   - Total : 70 à 90 lignes.
   - Chemin chaud : **oui** pour (5), dont le coût hôte rejoint celui de la CGA, et pour (6),
     une opération de plus par groupe de points ; (4) touche la lecture de 3DAh, très
     fréquente dans les boucles d'attente.
   - Temps visibles : (2) oui ; (4) oui, les bits lus.
   - État sondé, dans la sonde EGA (champ 0 = 6) : `#crtc` pour (2), `#egapal` pour (1),
     `stat` pour (4) ; les pixels après GR.0, qui ajoute aussi `pallook`.
5. **Vérification** :
   - (1) et (2) : *C# seul* contre [S5] ; *diff attendu* sur un EGABANC qui pose AC10 bit 7
     ou CR11 bit 7 (GR.1 l'étend déjà à AND et OR).
   - (5) : l'image après GR.0 ; déplacer le curseur sans écrire en VRAM, et l'image attendue
     se calcule.
   - (4) : critère d'acceptation, le POST du BIOS d'IBM passe en mode matériel (POD14 sans
     bip), et EGABANC échantillonne 3DAh sur un écran uni de couleur connue, ce qui donne les
     bits du tableau de [S5] p. 60.
   - (6) : boot-diff EGA 64 Ko de GR.1, puis l'image.
6. **Classement** : **(a)** pour (1), (2), (5) et les traits non inscrits ; **(b)** pour (4)
   et (6) ; **(c)** pour la valeur relue de (3).
7. **Dépendances** : (4) dépend de (5) ; PB-36 (temporisations de l'EGA) ; GR.0 (pixels et
   `pallook`), GR.1 (EGA 64 Ko et moniteur monochrome sous boot-diff, EGABANC étendu), GR.4
   (le champ *Effet* de PB-99).

### PB-100 — La Tseng ET4000AX et son RAMDAC

1. **Défauts, effets et vrai comportement** :
   - **(1) `crtc_mask` efface CR38-CR3F**, et le débordement de `htotal` lu en `:399` est
     mort.
     *Documenté* : l'ET4000 n'a que les index 00h-18h et 32h-37h ([S7] p. 111, table 4.3-2).
     CR3F est un registre de l'ET4000/W32 : pour l'AX, le masque de PCem est **fidèle**.
     **Ce n'est pas un défaut.** En passant, CR30 et CR31, absents de l'AX, restent
     inscriptibles chez PCem ; c'est mineur.
   - **(2) CR13 = 0 vaut 256** (`:397-398`).
     *Documenté* : le décalage s'ajoute à chaque rangée ([S7] p. 123), sans cas particulier.
     *Déduit* : 0 répète la même rangée.
     Effet seulement si un logiciel écrit CR13 = 0.
   - **(3) La fenêtre de 128 Ko n'est posée que sur une transition** (`:72-75`), et
     `banked_mask` vaut 0 à l'amorçage.
     *Documenté* : GDC 6 = 00 donne A0000h pour 128 Ko ([S7] p. 150).
     Effet : un GDC 6 écrit à 0 alors qu'il vaut déjà 0 laisse le masque de l'état précédent ;
     à l'amorçage, toute la banque tombe à l'offset 0.
   - **(4) Pas de séquence KEY** (`:269-270` et l'ensemble).
     *Documenté* : la pose (03h en 3BFh, A0h en 3D8h ou 3B8h, [S7] p. 102) et la portée.
     Sont protégées les écritures de CRTC au-delà de 18h sauf 33h et 35h (p. 111), CR36 et
     CR37 (pp. 136-137), TS 6 et TS 7 (p. 142), 3CDh après le premier KEY (p. 144), ainsi que
     la lecture de l'Input Status 0 bits 5-6 (p. 102).
     *Inconnu* : comment le KEY se retire ; ce n'est pas dans les pages lues. Une mesure ou
     une lecture complète du data book est nécessaire.
   - **(5) SR7 relu avec le bit 2 forcé** (`:269-270`).
     *Documenté* : bit 2 « Set to 1 (always) », et bit 4 aussi en révision E ([S7] p. 142).
     Cohérent avec le relevé FCh d'ET4BANC. **Ce n'est pas un défaut.**
   - **(6) FFh écrit une fois le RAMDAC armé tombe dans le masque des points**
     (`vid_unk_ramdac.c:24`).
     *Documenté* pour la SC15025/26 : après les quatre lectures, l'écriture suivante va au
     registre de commande, sans exception ([S8] pp. 3-88/89). Or FFh donne D7 D6 D0 = 111,
     un mode **réservé** ([S8] p. 3-86) : le mode qui en résulte est *inconnu*.
   - **(7) Le décodage des profondeurs rend 32 bits** (`:27-61`).
     *Documenté* : 010 et 011 donnent 24 bits RVB et BVR, 001 et 111 sont réservés, et 3 ou
     4 octets par point se choisissent par le registre de « repack » étendu 10h ([S8] tables
     2 et 3), que PCem ne modélise pas.
     Le « 32 » de PCem tiré de D5 n'a donc pas d'appui. Mais « que le SC1502x n'a pas »,
     comme le dit l'entrée, est **inexact** : les modes 3a et 3b font 4 octets par point.
     **La puce exacte** de la carte modélisée est *inconnue* (PCem écrit « SC1502x »).
2. **Sites** :
   - C : `vid_et4000.c:34-37`, `:72-75`, `:269-270`, `:397-398`, `:399` ;
     `vid_unk_ramdac.c:24`, `:27-61`.
   - C# : `Video/vid_et4000.cs:48`, `:92`, `:139`, `:178` ; `Video/vid_unk_ramdac.cs:39`.
3. **Vrai comportement** : voir le point 1.
4. **Correction**, par sous-défaut :
   - (2) retirer le cas spécial : 1 ligne ;
   - (3) le masque tiré de la carte mémoire à chaque écriture de GDC 6 et à l'amorçage :
     environ 4 lignes ;
   - (4) un état KEY, avec des gestionnaires 3BFh et 3B8h (le gestionnaire de l'ET4000 ne
     couvre que 3C0h-3DFh) et le filtre des registres protégés : 25 à 40 lignes, la règle de
     retrait restant à établir ;
   - (6) FFh au registre de commande : 1 ligne, le mode réservé restant *inconnu* ;
   - (1), (5) et (7) : rien.
   - Chemin chaud : non.
   - Temps visibles : (4) peut bloquer CR34 bit 1 (sélection d'horloge CS2) ; (6) et (7)
     changent `bpp`, donc `hdisp` et la ligne.
   - État sondé : la sonde SVGA de 163 champs couvre l'ET4000 (`#crtc`, `rowoffset`,
     `banked_mask`, `bpp`, banques) ; l'état du RAMDAC et `et4000_t` arrivent avec GR.1.
5. **Vérification** :
   - (2) et (3) : *C# seul* sur `svga.rowoffset` et `banked_mask`, puis une écriture CPU en
     A0000h + 10000h.
   - (4), (6), (7) : ET4BANC étendu par GR.1 (« le FFh armé et les 32 bits du RAMDAC »,
     HiColor), en *diff attendu*.
   - Le retrait du KEY et le mode 111 : une mesure sur une ET4000AX avec SC15025 ou une autre
     Sierra.
6. **Classement** : **(a)** pour (3) ; **(b)** pour (2), (4) côté pose et portée, et (6) ;
   **(c)** pour (4) côté retrait, et (7) ; **(d)** pour (1) et (5), à corriger dans le
   registre.
7. **Dépendances** : GR.1 (ET4BANC HiColor, sonde `et4000_t`, boot-diff DOS sur l'ami486),
   GR.4 (le champ *Effet* de PB-100) ; PB-37/38, que les modes HiColor de l'ET4000 pourraient
   atteindre si AR10 bit 6 est posé (à mesurer dans GR.1) ; PB-80, non atteint ici (pas de
   fenêtre linéaire).

### PB-102 — La MDA et l'Hercules balayent avec un caractère de 8 points

1. **Défaut** : `MDACONST` compte un caractère de 8 points à 16,257 MHz (`pit.c:42`), là où la
   MDA dessine 9 points. L'Hercules fait de même en texte, et compte des unités de 16 points
   en graphique.
   **Effet** : MDA et Hercules en texte à 20,74 kHz / 56,04 Hz au lieu de 18,43 kHz / 50 Hz ;
   Hercules en graphique à 37,6 kHz / 102 Hz (calculé). Le retour de trame lu en 3BAh, le
   clignotement du curseur et les boucles d'attente en dépendent, de même que le moniteur
   simulé de l'hôte (VERIFICATION.md § « L'hôte : le moniteur automatique »).
2. **Sites** : C `pit.c:42`, `vid_mda.c:79-80`, `vid_hercules.c:115-116` ; l'EGA, elle, applique
   9/8 (`vid_ega.c:231`). C# `Video/vid_mda.cs:152`, `Video/vid_hercules.cs:154`.
3. **Vrai comportement**, *documenté* :
   - MDA : caractère de 9 points ([S1] p. 2), oscillateur de 16,257 MHz ([S1] feuille 3,
     p. 21), moniteur 18 kHz ([S1] p. 2). Avec R0 = 61h, soit 98 caractères, la ligne fait
     54,25 µs, soit **18,43 kHz** ; (25 + 1) × 14 + 6 = 370 lignes donnent **49,8 Hz**.
   - Hercules : 0,5625 µs par caractère en texte et 1 µs en graphique, avec des caractères de
     16 points sur 4 lignes en graphique ([S6] p. 10). Texte : 55,1 µs, soit **18,14 kHz** et
     49,0 Hz. Graphique : R0 = 35h donne 54 µs, soit **18,52 kHz** ; (5Bh + 1) × 4 + 2 =
     370 lignes donnent **50,05 Hz**.
   - L'horloge de l'Hercules (16 MHz, déduite de ces temps) n'est donc pas celle de la MDA.
4. **Correction** :
   - Quoi : en mode matériel, la MDA prend `MDACONST × 9/8` ; l'Hercules prend 0,5625 µs par
     caractère en texte et 1 µs en graphique.
   - Recalculer aussi sur une écriture de 3B8h ou de 3BFh, puisque le pas dépend alors du
     mode ; PCem ne recalcule qu'aux écritures du 6845.
   - Taille : environ 8 lignes. Chemin chaud : non. Temps visibles : **oui**, c'est l'objet
     même de la correction.
   - État sondé : `dispontime`, `dispofftime` et `timer` des sondes MDA et Hercules. **Tout
     boot-diff MDA ou Hercules diverge** en mode matériel.
5. **Vérification**, forte et sans diff :
   - *C# seul* : mesurer au chronomètre les périodes de ligne et de trame, comme le font
     `TimerCheck` et `SpeedCheck`, et les comparer aux valeurs ci-dessus.
   - `--menu-check` en mode matériel : le moniteur lit 18,43 kHz pour la MDA, 18,14 ou
     18,52 kHz pour l'Hercules.
6. **Classement** : **(a)**.
7. **Dépendances** :
   - PB-36 (même fonction) et PB-97 (a) (R0, R4, R9 entrent dans la trame) ;
   - les bits de 3BAh (§ 4) et `SignalTiming` côté hôte ;
   - GR.3 (profils XT + Hercules et 5150 + MDA, témoin Windows avec `HERCULES.DRV`).

### PB-09 — `charbuffer` de la CGA débordable

1. **Défaut** : `charbuffer[256]`, une boucle jusqu'à 2 × R1, et R1 non masqué (≤ 255).
   **Effet** : en C, une corruption du tas. **Dans iXtal, une `IndexOutOfRangeException` non
   rattrapée : l'émulateur s'arrête**, en texte 80 colonnes, dès que R1 > 128.
2. **Sites** : C `vid_cga.h:31`, `vid_cga.c:157-158` (lectures), `:405` (copie). C#
   `vid_cga.cs:249-250` et `:548-557` ; le marqueur en `:548` est **sans identifiant**, et
   l'entrée cite `:538`, ligne périmée.
3. **Vrai comportement** : *documenté*, la carte lit le tampon d'affichage par des verrous,
   sans tampon de ligne ([S2] p. 4). *Déduit* : avec R1 > 128, elle affiche les octets de la
   VRAM ; l'image exacte avec R1 > R0 n'est pas définie ([S3]).
4. **Correction**, à faire **dans les deux modes, par R9 et pas par G13**. Deux voies :
   - le précédent PB-88 de la M24 : écriture sautée au-delà de 255, lecture rendue à 0 ;
   - un `charbuffer` de 512, fidèle à la carte et sûr.
   L'oracle n'y est jamais conduit : PCem y corrompt son tas. Environ 4 lignes, plus un test
   `r9-cga` en C# seul (environ 40 lignes). Chemin chaud : non, une copie par ligne.
5. **Vérification** : *C# seul* ; R1 = 200 en 80 colonnes, l'émulateur survit. Avec la
   seconde voie, l'image après GR.0.
6. **Classement** : **(d) pour G13**, puisque cela relève de R9, à traiter maintenant.
7. **Dépendances** : PB-88 (précédent), PB-89 (débordement sur la ligne suivante quand R1 est
   grand), PB-36.

### PB-96 — La police monochrome lue au-delà de ses 16 lignes

1. **Défaut** : `fontdatm[chr][sc]`, avec `sc` jusqu'à 31, lit les lignes du caractère
   suivant.
   **Effet** : avec R9 > 15, le bas d'une cellule montre le haut du caractère de code
   suivant.
2. **Sites** : C `vid_mda.c:119`, `:122` (`sc &= 31` en `:160` et `:223`), `video.c:921` ;
   `vid_hercules.c:173`, `:176`. C# `Video/vid_mda.cs:60-67` (`fontdatm_plat`),
   `Video/vid_hercules.cs:224`.
3. **Vrai comportement** :
   - *documenté* : quatre lignes d'adresse de rangée seulement, « RA (4) », vont au générateur
     de caractères ([S1] p. 3 ; feuille 2, RA0-RA3). La disposition de `loadfont`, rangées
     0-7 puis 8-15 (`video.c:939-949`), reflète ce câblage.
   - *déduit* : rangée = `sc & 15`, les rangées 16-31 répétant 0-15 du même caractère.
     Pour l'Hercules, déduit par analogie : son schéma n'a pas été consulté.
4. **Correction** : `fontdatm_plat(chr, sc & 15)` en mode matériel, 1 à 2 lignes.
   Chemin chaud : rendu texte, par caractère et par ligne, coût négligeable. Temps visibles :
   non. État sondé : pixels seulement, après GR.0.
5. **Vérification** : l'image, après GR.0, avec MDABANC (GR.1 prévoit « le registre 9 au-delà
   de 15 pour PB-96 ») ; les glyphes attendus se calculent depuis `mda.rom` avec la rangée
   `& 15`.
6. **Classement** : **(a)**.
7. **Dépendances** : PB-97 (a), le masque de R9 ; GR.0 et GR.1.

### PB-35 — Lire le DAC après `OUT 3C8h,0` indexe `vgapal[-1]` (section C)

1. **Défaut** : `dac_read = val - 1` vaut -1 après `OUT 3C8h,0`.
   **Effet** : les deux premières composantes lues sont les octets 1 à 3 de `pallook[511]`,
   0 pour une VGA. Le BIOS ne le fait jamais ; seule la campagne synthétique de M15 l'atteint.
2. **Sites** : C `vid_svga.c:122-126`, `:241-247`. C# `Video/vid_svga.cs:248` (`vgapal_at`).
3. **Vrai comportement** : *inconnu*, il dépend du DAC ([S10]). Seule certitude, *documentée*
   pour la SC15025/26 : le registre d'adresse a 8 bits ([S8] p. 3-83) ; l'index -1 n'existe
   pas sur le matériel. Le data sheet de l'IMS G171 n'a pas été lu (voir § 2).
4. **Correction** : rien de documenté à viser. Le masque `& 255`, qui lirait l'entrée 255,
   serait arbitraire.
5. **Vérification** : une mesure par DAC (VGA d'IBM, Trio64, GD5429, 8900D avec TKD8001,
   ET4000 avec SC1502x) sous DEBUG : `o 3c8 0`, trois `i 3c9`, avant et après avoir écrit les
   entrées 0 et 255.
6. **Classement** : **(c)**. Une correction en mode matériel n'a pas d'objet avant la mesure :
   le rendu est faux, mais seulement pour un programme synthétique.
7. **Dépendances** : aucune.

### PB-36 — Un temps « hors affichage » négatif (section C)

1. **Défaut** : `(uint64_t)` d'un double négatif quand R1 dépasse R0 + 1 ; le chronomètre
   recule, puis repart.
   **Effet** : aucun, la période de ligne reste celle de `htotal`. Le C# reproduit GCC de
   façon déterministe.
2. **Sites** :
   - C : `vid_svga.c:442-451`, `vid_cga.c:109-116`, `vid_mda.c:74-83`,
     `vid_hercules.c:110-119`, `vid_ega.c:236-249`, et **aussi la M24**
     (`vid_olivetti_m24.c:104-121`), marquée dans le C# mais absente de l'entrée.
   - C# : `vid_svga.cs:703`, `vid_cga.cs:189`, `vid_mda.cs:154`, `vid_hercules.cs:156`,
     `vid_ega.cs:370`, `vid_olivetti_m24.cs:153`.
3. **Vrai comportement** : R0 doit dépasser R1 ([S3]). *Déduit* : au-delà, l'affichage ne
   s'éteint jamais dans la ligne. C'est déjà ce que rend l'arithmétique de PCem : la phase
   « off » ne dure rien.
4. **Correction** : une borne (temps « off » ≥ 0) ne changerait rien de visible. Pour la SVGA,
   la borne `TIMER_USEC` allongerait même la ligne et s'éloignerait du matériel.
5. **Vérification** : sans objet.
6. **Classement** : **(d)**, nettoyage sans effet ; laisser reproduit.
7. **Dépendances** : PB-09 et PB-89 (R1 grand) ; PB-102 (mêmes fonctions).

### PB-37 — `svga_render_24bpp_lowres` n'avance jamais son pointeur (section C)

1. **Défaut** : `p` n'avance pas ; chaque tour réécrit les huit mêmes points.
   **Effet** : en 24 bpp basse résolution, seuls les huit premiers points de chaque ligne
   changent. La campagne de la 8900D l'atteint (368 908 appels : RAMDAC TKD8001 à `bpp = 24`
   et AR10 bit 6). C'est un **effet visible** : la place de l'entrée en section C se discute.
2. **Sites** : C `vid_svga_render.c:707-718`, `:720-738`. C# `Video/vid_svga_render.cs:834`.
3. **Vrai comportement** :
   - *déduit* : une vraie puce affiche toute la ligne, et le format, chaque point de 24 bits
     doublé, est l'intention du code (voir le rendu haute résolution voisin, `:742-789`) ;
   - *inconnu* : si la 8900D avec TKD8001 offre vraiment 24 bpp avec AR10 bit 6 (fiches
     Trident non consultées).
4. **Correction** :
   - Quoi : en mode matériel, les deux branches passent à `x += 4` et `p += 8`, d'après le
     rendu haute résolution et la branche avec remappage. **Pas seulement « avancer p »** :
     avec le `x++` actuel, on écrirait quatre fois trop large et on déborderait sur les
     lignes suivantes.
   - Taille : 4 à 6 lignes. Chemin chaud : le rendu, dans ce seul mode. Temps visibles : non.
   - Brancher **dans** la fonction : `vid_svga.cs:966-969` compare `svga.render` à
     `svga_render_24bpp_lowres`, et un délégué neuf casserait cette comparaison.
5. **Vérification** : une image calculée hors de l'émulateur (VRAM → points doublés) sur l'état
   de la campagne 8900D, en C# seul ; *diff attendu* : seul `#buffer32` diffère de l'oracle.
6. **Classement** : **(b)**.
7. **Dépendances** : PB-38 ; PB-100, si l'ET4000 HiColor atteint les rendus basse résolution
   (GR.1).

### PB-38 — `svga_render_16bpp_lowres` avance `ma` deux fois (section C)

1. **Défaut** : `ma` avance deux fois. **Effet** : aucun ; `svga_poll` recharge `ma` depuis
   `maback` à la fin de chaque ligne affichée (`vid_svga.c:561-579`). Le chemin n'est pas
   atteint.
2. **Sites** : C `vid_svga_render.c:620-642`. C# `Video/vid_svga_render.cs:757`.
3. **Vrai comportement** : sans objet.
4. **Correction** : 1 ligne, sans effet.
5. **Vérification** : sans objet. Le champ `ma` de la sonde ne différerait qu'échantillonné
   entre le rendu et le rechargement.
6. **Classement** : **(d)**, nettoyage sans effet. À revoir si GR.1 montre que l'ET4000
   HiColor y passe.
7. **Dépendances** : PB-37 ; GR.1.

### PB-98 — L'Hercules ne retire pas sa projection à la fermeture (section C)

1. **Défaut** : `free` sans `mem_mapping_remove`. **Effet** : aucun ; c'est côté hôte, et la
   liste se vide au `mem_alloc` suivant.
2. **Sites** :
   - C : `vid_hercules.c:341-346` (la MDA, elle, retire la sienne, `vid_mda.c:282`) ;
     `xtide.c:106-110` ; l'EGA aussi (`vid_ega.c:1068-1073`), comme le note GR.4.
   - C# : `Video/vid_hercules.cs:422`, `Ide/xtide.cs:114` ; `Video/vid_ega.cs:1210-1213` en
     parle dans un commentaire, sans marqueur.
3. **Vrai comportement** : sans équivalent matériel.
4. **Correction** : aucune en mode matériel, ce n'est pas un comportement.
5. **Vérification** : sans objet.
6. **Classement** : **(d)**, nettoyage sans effet, pour G15 ou jamais.
7. **Dépendances** : GR.4 (étendre l'entrée à l'EGA).

---

## 4. Hors liste : corrections du registre et défauts non inscrits

**Avant G13, dans le registre et les marqueurs**, sur le chemin de GR.4 qui touche déjà les
entrées PB-97, PB-99 et PB-100 :

- PB-04 : ajouter l'identifiant au marqueur `vid_cga.cs:259`, marquer le site `:306`, mettre à
  jour la ligne citée.
- PB-09 : la même chose en `:548` ; puis le passer en R9, non reproduit.
- PB-99 : marquer `:759` et `:766` ; inscrire CR0A bit 5, CR07 bits 5-7 et CR09 bit 6.
- PB-100 : (1) et (5) ne sont pas des défauts pour l'AX ; reformuler (7).
- PB-36 : ajouter la M24.
- PB-37 : repasser en section A, puisque l'effet est observable.

**Défauts non inscrits**, à inscrire avant d'entrer dans le mode matériel :

1. **3BAh bit 3 de la MDA et de l'Hercules** : PCem y met le retour vertical. Selon IBM, c'est
   « +Black/White Video », le signal des points ([S1] p. 8) ; selon Hercules, « dots on »
   ([S6] p. 13). Documenté.
2. **3BAh bit 7 de l'Hercules, polarité inversée** : PCem rend 1 pendant le retour, alors que
   le manuel dit « 0 = vertical retrace » ([S6] p. 13). Documenté. PLAN-G9 n° 5 l'avait
   relevé « à vérifier » ; c'est vérifié.
3. **Le curseur de la CGA** : PCem l'inverse en XOR, alors qu'il est plein, de la couleur
   d'avant-plan ([S11], et la logique des points de [S2] p. 28). Déduit.
4. **Le clignotement des caractères de la CGA** suit le rythme du curseur (`cgablink & 8`),
   contre `& 16` pour la MDA, la M24 et le PC1512, et le bit 4 chez MAME [S11]. Déduit.
5. **M24 et PC1512 en 40 colonnes** : le clignotement n'exempte pas le curseur
   (`vid_olivetti_m24.c:209`, `vid_pc1512.c:237`). C'est la famille de PB-04.
6. **Le motif de PB-89 dans la CGA** (sites cités sous PB-89).
7. **Relecture de R0-R13 par la CGA, la M24 et le PC1512** : la famille de PB-97 (b).
8. **CR22 de la GD5429**, la relecture des verrous, n'est pas modélisé ([S9]). Le modéliser en
   mode matériel donnerait une sonde à PB-80.
9. **L'interruption verticale de l'EGA** (IRQ 2) est absente : c'est une fonction et non un
   défaut, plus grosse que le reste ([S5] pp. 14-15, 36-37, 72).
10. **Ports 3B0h à 3B3h, 3B6h, 3B7h de la MDA**, « Not Used » pour IBM ([S1] p. 7), que PCem
    décode comme index et données du 6845. Le vrai décodage est *inconnu*, à mesurer.

---

## 5. Proposition d'étapes

**Préalable commun**, hors vidéo, à confirmer au plan général de G13 :

- le drapeau du mode matériel, en `static readonly` lu comme une constante par le JIT
  (précédent : `Buffer32`) ;
- son option et sa clé de `.cfg` ;
- l'amendement de R1(d) pour le marqueur `pcem bug, fixed in hardware mode: PB-nn` ;
- un **« diff attendu »** dans `iXtal26.Diff` : en mode matériel, la première divergence et les
  champs de sonde qui diffèrent doivent être exactement ceux annoncés ;
- des **commutateurs par PB**, au moins dans les outils, sans quoi PB-102 masque toutes les
  autres corrections de la MDA ;
- une mesure de coût avec `tools/perfbanc`, dans les deux modes.

**V0 — maintenant, hors G13 (R9 et registre).**
- PB-09 en R9, dans les deux modes ; les marqueurs et les corrections du § 4.
- Vérification : `r9-cga` en C# seul (l'émulateur survit) ; les boot-diffs CGA en mode PCem
  restent identiques, R1 ≤ 128 dans tous les arcs.

**V1 — G13, le socle SVGA, sans dépendance à GR.**
- PB-80 (le socle et la GD5429), PB-37 ; PB-38 laissé (sans effet).
- Vérification :
  - `hw-verrous` et `hw-24bpp` en C# seul ;
  - BLTBANC en *diff attendu*, où seul `la|lb|lc|ld` diffère ;
  - **non-régression** : toutes les portes VGA et SVGA sont identiques en mode PCem, et en
    mode matériel sur les arcs qui ne touchent pas ces chemins (VGA d'IBM, Trio64, POST de la
    8900D). C'est la preuve que le drapeau ne touche que ce qu'il annonce.
- Taille : environ 15 lignes, plus 150 de tests.

**V2 — après GR.0 et GR.1 : la MDA et l'Hercules.**
- PB-102, PB-97 (a, c, d), PB-96 ; les bits de 3BAh s'ils sont inscrits.
- Vérification :
  - en C# seul : les périodes contre [S1] et [S6], les masques contre [S3], les rangées du
    curseur ;
  - les images de GR.0 ;
  - `--menu-check` en mode matériel ;
  - MDABANC et HERCBANC en *diff attendu*, PB-102 coupé.
- Taille : environ 30 lignes, plus 200 de tests.

**V3 — après GR.0 et GR.1 : l'EGA.**
- D'abord PB-99 (1), (2), (5), (6) et les traits non inscrits ; puis (4), dont le critère est
  POD14 sans bip et EGABANC sur un écran uni.
- (3) reste en (c) jusqu'à la mesure.
- Taille : 70 à 90 lignes.

**V4 — après GR.1 : l'ET4000.**
- PB-100 (3), (2), (6) ; (4) côté pose et portée, si la décision le retient ; (1), (5) et (7)
  restent tels quels.
- Vérification : ET4BANC étendu en *diff attendu*, et le C# seul.
- Taille : 10 à 50 lignes.

**V5 — après GR.0 : la CGA, la M24 et le PC1512.**
- PB-04, avec le modèle du curseur si la question 9 le retient ; PB-89 dans la M24, le PC1512
  et, si inscrite, la CGA.
- Vérification : images de GR.0, et le test de débordement en C# seul.

**V6 — clôture de la section C.** PB-35 reste reproduit, avec le protocole de mesure écrit ;
PB-36, PB-38 et PB-98 restent reproduits, et le registre dit pourquoi.

**Ordre recommandé** : V0 tout de suite ; V1 au tour « vidéo » de G13 ; V2 à V5 après GR.0 et
GR.1. Cela revient à placer GR avant la partie « cartes de G9 et famille CGA » de G13, ou à la
greffer à GR comme sous-étape « mode matériel ».

---

## 6. Risques

1. **Remplacer un comportement connu (PCem) par un comportement inventé.** Le danger est
   maximal pour le faisceau de l'EGA (3DAh), le retrait du KEY, les valeurs des registres en
   écriture seule, le mode 111 du RAMDAC et le curseur de la CGA. Parade : seuls (a) et (b)
   entrent ; (c) attend une mesure.
2. **La vérification par diff disparaît pour la MDA et l'Hercules** dès que PB-102 est actif.
   Il faut des commutateurs par PB et des tests en C# seul.
3. **Les chemins chauds** :
   - le rendu texte de la CGA (PB-04) ;
   - le texte de l'EGA dessiné à chaque trame (PB-99 (5)), un coût hôte du même ordre que la
     CGA ;
   - les lectures de VRAM (PB-80) ;
   - la lecture de 3DAh (PB-99 (4)), martelée par les boucles d'attente.
   À mesurer avec `perfbanc`, et le mode PCem ne doit pas ralentir.
4. **R2 et R1** : les fichiers transcrits grossissent. Comptage par la commande de R2, fichier
   entier :
   - `vid_mda.cs` 1,07 ; `vid_hercules.cs` 0,97 ; `vid_ega.cs` 0,93 ; `vid_cga.cs` 1,00 ;
     `vid_svga_render.cs` 1,03 ; `vid_cl5429.cs` 0,86 ;
   - `vid_unk_ramdac.cs` 1,13, soit environ 7 lignes de marge ;
   - `vid_svga.cs` 1,30, mais il porte environ 380 lignes de sonde (`Probe`, l. 2035-2413).
   Le marqueur neuf demande un amendement de TRANSCRIPTION.md.
5. **La fidélité peut ressembler à une régression** : le curseur 0607h à mi-cellule sur la
   MDA, FFh qui va au registre de commande, les registres verrouillés par le KEY. C'est ce que
   fait le vrai matériel, mais il faut le dire, et le mode reste opt-in.
6. **Collision avec GR** : GR.1 relit contradictoirement les cinq fichiers de G9 et compte
   leur couverture, et GR.4 réécrit les entrées PB-97, PB-99 et PB-100. Faire G13 avant GR
   ajoute des branches à relire et à couvrir ; le faire après évite de toucher deux fois les
   mêmes entrées.
7. **Les chemins atteints changent avec GR.1** : les rendus basse résolution par l'ET4000
   HiColor, pour PB-37 et PB-38, sont à mesurer.

---

## 7. Questions de principe à poser

1. **G13 vidéo avant ou après GR ?** La recommandation : V1 (socle SVGA) dans G13, V2 à V5
   après GR.0 et GR.1, ou greffés à GR.
2. **PB-09 tout de suite, par R9, dans les deux modes ?** Avec quelle sortie : celle de PB-88
   (écriture sautée, lecture à 0), ou un `charbuffer` de 512, fidèle à la carte ?
3. **Les (c) restent-ils reproduits jusqu'à une mesure sur du vrai matériel ?** Avez-vous accès
   à une IBM MDA, CGA ou EGA, une Hercules GB101, une ET4000AX avec DAC Sierra, une GD5429 ou
   une 8900D ? Des protocoles DEBUG courts peuvent être fournis.
4. **Inscrire les défauts non inscrits du § 4** (nouveaux PB) avant G13, et les faire entrer
   dans le mode matériel ?
5. **Valider les corrections du registre** : PB-100 (1) et (5) ne sont pas des défauts, (7)
   est à reformuler ; PB-37 passe en section A ; PB-36 s'étend à la M24.
6. **Où vit le code du mode matériel ?** En ligne dans les fichiers transcrits (le plafond de
   R2 compte), ou dans des fichiers partiels séparés, hors R2 ? Et l'amendement de R1 pour le
   marqueur neuf.
7. **Un seul commutateur, ou un par domaine, par carte ou par PB ?** Vérifier la MDA sans
   PB-102 l'exige au moins dans les outils.
8. **Le mode matériel peut-il ajouter des mécanismes neufs** (le faisceau de l'EGA pour 3DAh,
   le KEY de l'ET4000, CR22 de la GD5429, l'interruption verticale de l'EGA) ? Ou se borne-t-il
   à retirer les écarts de PCem ?
9. **PB-04** : faut-il aussi remplacer le curseur XOR de PCem par le curseur plein du
   matériel ? Sans cela, corriger PB-04 seul ne rend pas l'aspect réel.
10. **Le KEY de l'ET4000** : l'implanter avec la pose et la portée documentées, le retrait
    restant inconnu, ou le laisser reproduit ?
