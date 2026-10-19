# GR.1 — La profondeur de G9 : contre-lecture du rapport R1

Le 20 octobre 2026. Contre-lecture de `R1-g9.md` en lecture seule du dépôt (`master`, `d56d161`) : rien de construit,
rien de lancé. Le C, le C# et les outils ont été relus aux lignes citées ; les comptes ont été refaits par des
commandes (notées entre parenthèses) ; la ROM `roms/et4000.bin` a été relue au désassemblage jetable
(`objdump -D -b binary -m i8086`, `ndisasm` absent) et à la main, hors dépôt. Seul ce fichier est écrit.

**Niveaux** : *documenté* (le texte du plan, du registre ou du code le dit en toutes lettres), *déduit* (raisonné sur
le code ou la ROM relus, non exécuté), *inconnu* (à mesurer). **Numérotation** : **C** = confirmé, **K** = corrigé,
**A** = ajouté.

## Verdict

Le rapport est solide et son état des lieux est exact : aucun des quatorze points n'est fait (un partiel, `j`),
aucun n'est caduc, les bancs d'EGA et d'Hercules n'ont rien perdu de leur objet. Ses lectures de la ROM tiennent, y
compris le point neuf (la détection du RAMDAC) : j'ai refait la trace de 7CE0h et je retrouve le type 4 en mode PCem
et le type 2 en mode matériel. Sept corrections (K1 à K7), dont deux changent un texte à écrire (le nombre d'octets
d'ET4BANC qui changent est douze, non neuf ; « E4h » n'est pas un code 32 bits du décodeur), neuf ajouts (A1 à A9), dont
deux qui touchent l'ordre des étapes (le recouvrement avec GR.0c, la dépendance de GR.1e à GR.0). Sa décision
ouverte (la couverture des `.Materiel.cs`) est à moitié tranchée par les décisions prises (§ 4).

## 1. Confirmé (C)

- **C1. Soixante-dix `// pcem:`, 17 aux bornes fausses** — *vérifié*. `grep -c '// pcem:'` rend 16 (vid_mda.cs), 14
  (vid_hercules.cs), 23 (vid_ega.cs), 13 (vid_et4000.cs), 4 (vid_unk_ramdac.cs) = 70. Bornes des fonctions de
  `pcem-dev/src/video/` relevées par `awk` sur les `}` de colonne 0 et par `sed -n` sur les en-têtes. Je refais les
  dix-sept lignes de son tableau, une à une, et toutes se confirment : Hercules 40-74 (cité 40-75), 76-94 (77-96),
  96-101 (98-102), 103-107 (104-108), 109-119 (110-119), 121-300 (121-299) ; EGA `ega_t` 3-68
  (`includes/private/video/vid_ega.h:68`, cité 3-70), 189-256, 258-326, 328-383, 385-455, 457-519, 521-699, 701-884,
  886-932, 934-1033 (cités un cran trop courts ou trop longs) ; RAMDAC `case 1: default:` aux lignes 59-60
  (cité 61-62, qui sont l'accolade et le `if (oldbpp …)`). Les trente-trois autres (vid_mda 16, vid_et4000 13,
  le reste d'ega, d'hercules et du RAMDAC) : justes (ouvertures et fermetures relevées, `vid_mda.h:25`, `vid_et4000.c`
  583-612, `vid_hercules.c` 302-365, `vid_ega.c` 1035-1123, `vid_unk_ramdac.c` 14-76 et 78-96).
- **C2. Le `DEVIATION` d'`ega_write`** — *vérifié*. `vid_ega.cs:1036` cite `:770-823` et `:837-880` ; dans
  `vid_ega.c` les deux `switch (ega->gdcreg[3] & 0x18)` sont aux lignes 778 et 839, leurs accolades fermantes en 819 et
  880 (`grep -n 'switch' | awk '$1>700 && $1<890'` puis `sed -n 815,822p`). Plage fausse comme dit : 778-819.
- **C3. 40 des 53 méthodes compilées dans M2-13b** — *vérifié*. `grep -c -E '^\s+(public |internal |private )?(static )
  [^=;]*\('` rend 13, 10, 19, 9, 2 = 53 pour les cinq fichiers et 10, 12, 17, 13, 1 = 53 pour les `.Materiel.cs` (le
  « 53 de plus » est juste, par hasard). Sur `/var/tmp/ixtal-g13/M2-13b/*.txt`, `grep -h -o "Assembly listing for
  method iXtal26.Video.vid_xxx:…" | sort -u` rend 11, 10, 14, 7, 2 ; sans le `.cctor` de chaque classe : 10, 9, 13, 6,
  2 = 40, les noms listés sont ceux de son tableau. Aucune méthode `*_materiel`, `rendu_faisceau`, `Cle` ou
  `ipf_materiel` n'y figure. Soixante-trois `case` et cent cinquante-quatre `if (` dans `vid_ega.cs` (`grep -c`).
- **C4. HERCBANC change un octet, EGABANC aucun** — *vérifié*. `tools/gates/bancs/hercbanc.attendus` : trois
  rangées, `pcem` 8900**00**2D…, `materiel[video]` 8900**88**2D…, `materiel[tout]` 8900**80**2D… : le troisième octet,
  et lui seul (comparaison par script jetable). `egabanc.attendus` n'a aucune étiquette de mode : les mêmes rangées
  pour les deux modes, comme le dit l'en-tête (`Banc.cs:46-73` : une rangée sans étiquette vaut pour les deux).
- **C5. Les trois dernières lignes de la sonde** — *vérifié*. `tools/oracle/harness_tvga.c:23-44` : `h_tvga_probe`
  sort sur `gfxcard != GFX_TVGA && != GFX_TVGA9000B` (de même les blocs GD5429 et S3) : l'ET4000 les laisse à zéro.
  `et4000_t` (`vid_et4000.c:15-32`) : `banking` est redondant (`:64` écrit `write_bank` et `read_bank` du même octet,
  `:281` le relit) ; `ramdac.state` et `ramdac.ctrl` ne sont dans aucun champ (point `j`).
- **C6. Les clés** — *vérifié*. `memory` (64/128/256, défaut 256), `monitor_type` (6, 7, 9, 58, 74, 90 :
  `DISPLAY_GREEN 3`, `AMBER 4`, `WHITE 5`, `video.h:111-113`, soit 10 | n << 4), `vid_ega.cs:1338-1345` ; `display_type`
  `vid_mda.cs:463`, `vid_hercules.cs:489` ; la validation par liste `PluginApi/device.cs:258-272`
  (`config_hors_liste`, retour au défaut, avertissement). `BootDiff.cs:345-350` est bien `PousserConfigDevices` (le
  chemin est `tools/iXtal26.Diff/BootDiff.cs`) : il pousse `machine_named_entries()` à l'oracle ; aucun `.cfg` de
  `tools/gates/cfg/` n'a de section `[EGA]`, `[MDA]` ou `[Hercules]` (`grep -n 'EGA\]\|MDA\]\|Hercules\]'` : vide).
  `R9VideoCfg.cs` ne cite ni `memory` ni `display_type` (`grep`) : seuls `monitor_type` (douze essais), `io_base`,
  `font`. Le « manque » du n° 5 de « Ce qu'un bloc livre » (PLAN.md:38-39) est réel.
- **C7. La ROM** — *déduit, relu au désassemblage*.
  - `strings` : « TsengLabs Int'l ET4000 BIOS Date : 04/07/93 », « V8.01X » ; `grep -a -c -i vesa` : 0.
  - La liste `13 2d 2e 2f 30 38` est bien en C000:0D8C (octets à 0x0D8C ; `mov bx,0D8Ch / mov cx,6` en 0D94-0D97 ; deux
    autres listes jointes en 0D9C et 0DAB, de 5 et 15 modes).
  - **Le mode 38h est bien la table de C000:7200, et 37h celle de C000:71C0** : le rapport ne le prouve pas, je le
    prouve. Le sélecteur de table (7724h-775Bh) calcule `6800h + mot[mot[771Eh = 76BCh] + 2 × octet[0FF3h + mode]]` ; en
    Python jetable sur la ROM : 13h → 6F00h, 2Dh → 70C0h, 2Eh → 7100h, 2Fh → 7140h, 30h → 7180h, 37h → **71C0h**,
    38h → **7200h**. Les deux tables : 128 colonnes, 48 rangées, CR00 = 99h, CR06 = 2Fh, CR07 = F5h, GR5 = 00h (37h) et
    40h (38h), SR4 = 06h et 0Eh, CR13 = 40h et 80h (octets 10, 16-19 et 60 de chaque table de 64). 158 caractères,
    0x32F + 2 = 817 lignes, 768 affichées (CR12 = FFh et CR07 bit 6) : confirmé.
  - Le HiColor : `10F0h`/`10F1h`/`10F2h` aiguillés en 238Bh-239Dh vers 79E8h, 7CCEh, 7CDBh. Les paramètres de 10F0h
    (7A1E-7A4Dh) sont ceux du rapport (2Dh, 2Fh avec 2 ; 2Eh, 30h avec 3 ; 13h pour un type ≥ 2), la table 79DEh
    `00 00 00 01 01 03 01 02 01 01` aussi : le test de BL = FFh/FEh est `shr` de la table par le type (7A06h).
  - **Pas de VESA** : preuve plus forte que l'absence de la chaîne. Le répartiteur d'INT 10h (10C5h-10DBh) fait
    `cmp ah,1Dh / jae sortie` : toute fonction ≥ 1Dh, la 4Fh comprise, est ignorée. Recherche d'octets sur la ROM :
    `80 FC 4F`, `3C 4F`, `B8 00 4F`, `B4 4F`, `80 EC 4F` : aucune occurrence.
- **C8. Le point neuf, la détection du RAMDAC** — *déduit, trace refaite à la main sur 7CE0h-7E47h et sur
  `vid_unk_ramdac.c` / `.cs`*. Les aides : 7E21h = lecture de 3C8h puis quatre lectures de 3C6h (arme l'IPF),
  7E31h = lecture de 3C8h, 7E3Ah = IPF armé puis écriture de 3C6h, 7E47h = IPF armé puis lecture de 3C6h.
  - Commun : la commande `cl` et le masque `bh` relus, le masque remplacé par `~cl`, la cinquième lecture comparée à
    `~cl` (≠ : une puce à registre de commande) ; E0h puis 60h écrits par l'IPF et relus (E0h : bit 7-5 retenus ;
    60h ≠ 0) ; `cl` remis ; type 1 puis 3 posés en attente.
  - **Mode PCem** : après l'IPF, la lecture de 7D2Fh rend la commande et désarme (`state = 0`, `vid_unk_ramdac.cs`
    `unk_ramdac_in`), la lecture de 7D32h rend le masque, égal à `~cl` : on tombe en 7D37h. FFh écrit armé : `break`
    (`vid_unk_ramdac.c:24`, vérifié) → il va au masque ; la commande relue vaut `cl` ≠ FFh → `bl = 4`. **Type 4**
    (et 3 si `cl` valait FFh, mot du rapport).
  - **Mode matériel (PB-229)** : `ipf_materiel` (`vid_unk_ramdac.Materiel.cs:21`) rend la commande à chaque lecture ;
    7D32h rend `cl` ≠ `~cl` : branche 7D4Ah. Quatre lectures (la commande), `bl = 6` non pris ; FFh écrit après un 3C8h
    lu va au masque ; quatre lectures rendent FFh (ni 8Eh, ni 82h, ni 44h) ; 7D94h écrit le masque `bh & EFh`, arme
    l'IPF, écrit la commande `cl | 10h`, relit le masque, **teste le bit 4** (`not ah` avant le `test`, une subtilité
    qu'un lecteur pressé manque) : zéro → le chemin ne prend pas le type 5 ; 7DB7h écrit 02h en 3C8h, change le
    masque (`^ 80h`), le relit : différent → pas le type 9. **Type 2**. Dépendances : PB-100 (c) (le `break` sur FFh,
    PCEM_BUGS.md:1973-1977 et 1993-1994) et PB-229 (PCEM_BUGS.md:5331-5358, le code cité `:84-87` est dans
    `unk_ramdac_in`, `vid_unk_ramdac.c:78-96`).
  - Conséquences confirmées : BL = FFh sous 10F0h : table[4] = 01h (bit 0 : accepté, 16 bits) en mode PCem,
    table[2] = 00h : refusé en mode matériel. Le décodage 7E54h (80h, ou A0h si le type ≥ 2) donne dans les deux modes
    A0h → 15 bits (`unk_ramdac_out`, cas 4).
- **C9. Les attendus et les fins de banc** — *vérifié*. `egabanc.py:216-218` : rotation 3 (fonction SET, GR3 = 03h),
  XOR (18h) seuls ; `et4banc.py:228` : `INB(0x3C8) + OUTB(0x3C6,0xFF) + INB(0x3C6)`, FFh désarmé par le 3C8h ; `:232` :
  retour au mode 3. `series.sh:295-296` : POST seul pour l'ET4000 sur l'ami486 ; `:302` : le commentaire qui dit
  que boot-diff refuse le mode matériel ; `VgaProbe.cs:71-79` : cinq cartes, pas `et4000ax`. Les citations
  VERIFICATION.md (§§ G9.0 à G9.3 à 5417, 5492, 5531, 5572 ; G13.9a à 9g à 11191-11785 ; 10b à 11896 ; 12a à 13115) et
  PLAN.md (498-504, 327-328, 38-39) sont justes.
- **C10. Les cibles du banc MDA** — *vérifié*. `vid_mda.c:29-34` (le correctif Turbo XT, sur 3B5h), `:55`
  (relecture entière), `:98-100` (entrelacé), `:119` et `:122` (`fontdatm[chr][sc]`), `:252` (`cgapal_rebuild`) ;
  l'oracle : `harness_stubs.c:466` (`cgapal_rebuild` vide), `vid_svga.cs:2321` et `:2350` (« l'oracle ne charge pas
  fontdatm »).

## 2. Corrigé (K)

- **K1. « ET4BANC : neuf octets changent » → douze.** *Vérifié.* Comparaison des deux rangées d'`et4banc.attendus`
  par script jetable : rangée 1 (`3DAh`), octets 1, 2, 12, 13 (31h→B1h, 00h→80h, 31h→81h, 00h→80h) ; rangée 2, quatre
  octets de 3C6h (`FF`→`00`) ; rangée 3, quatre octets de 3C6h (`FF`→`A0`) : **4 + 4 + 4 = 12**. Le commentaire de
  l'en-tête de ce fichier (« deux échantillons de 3DAh », « quatre lectures », « de même après A0h ») dit la même
  chose ;
  VERIFICATION.md:11798 écrit « neuf octets … les deux échantillons de 3DAh … et huit lectures de 3C6h » (deux
  échantillons = quatre octets, OU et ET), donc douze : la source du rapport avait l'erreur. À corriger dans R1 § 1.4
  (tableau), dans le titre de GR.1c/GR.1d et dans VERIFICATION.md à l'occasion de GR.5.
- **K2. « 75 champs de `svga_t` lus par la sonde » → 75 places, 71 champs.** *Vérifié.* `sed -n 2241,2340p harness.c |
  grep -c 'out\[f++\]'` rend 75 (le `memcpy(&out[f++], &svga->clock` compris). Y figurent un marqueur (`out[f++] = 1`),
  trois globales qui ne sont pas des champs (`buffer32`, `xsize`, `ysize`), et des champs imbriqués (`mapping.base`,
  `mapping.size`, `timer.ts_integer`, `timer.ts_frac`) ; `la`/`lb`/`lc`/`ld` tiennent en une place. D'où 71 membres de
  `svga_t` dans 75 places. Les places 0 à 74 suivies de 11 (TVGA), 16 (GD5429) et 20 (S3) font bien 122, puis 41 pour
  M24, PC1512, MDA, Hercules et EGA : 163 (`H_VGA_PROBE_N`, `harness.h:572`).
- **K3. PCEM_BUGS.md:2001-2005** (R1 § 1.2, G13.9e, « la part (c) ») : ces lignes sont le KEY. La part (c) du RAMDAC est
  en 1973-1977 (le décodage et FFh) et 1993-1994 (« le masque de CR30-CR31, SR7, FFh et les profondeurs du RAMDAC (c),
  inconnus »). *Vérifié* (`grep -n 'FFh et les profondeurs'`).
- **K4. « Les autres renvois en ligne … justes » est faux pour trois renvois d'`vid_ega.cs`.** *Vérifié* (`grep -n -o
  -E '//.*\(:[0-9]+…'` puis `sed -n`, 17 renvois d'ega, 15 d'autres fichiers revus). `vid_ega.cs:248` cite
  « pclog("Write 3d4 …") (:122) » : le `pclog` est en `vid_ega.c:121` (122 est `ega->crtcreg = val & 31`) ; `:252`
  cite « Write 3d5 (:126) » : en 125 ; `:383` cite « printf("Recalc! …") (:228-229) » : en 227-228. Aucune erreur de
  comportement. Les autres renvois relus (39-42, 128, 145-146, 152, 200, 238, 249, 549-550, 613, 619, 937, 1044,
  1071-1072 de vid_ega ; mda 28, 29-34, 239, 283-284 ; et4000 22-31, 72-75, 270-271, 397-399, 589 ; hercules 167-185,
  308) sont justes. Le total à relire passe de 18 bornes à **21** (17 + le `DEVIATION` + ces 3).
- **K5. « 40h puis E4h (32 bits) » (GR.1d et tableau, point h).** Le décodeur (`vid_unk_ramdac.c:27-61`, même texte en
  C#) calcule `(val & 1) | ((val & 0xC0) >> 5)` : 40h → 2 (32 bits, D5 = 0), 60h → 2 (24 bits, D5 = 1), 41h → 3
  (id.), A0h → 4 (15 bits), **E4h → 6 : 16 bits**, C1h/E1h → 7 sans D2 : 16 bits, C5h → 7 avec D2 sans D5 : 32 bits,
  E5h → 24 bits. Pour 32 bits par le code 7 il faut C5h ; E4h est un cas de 16 bits (et E0h, que le BIOS écrit, aussi).
  *Vérifié* sur le C et sur le C#. Le banc doit donc relever 40h (32), 60h (24), C5h (32 par le code 7), E5h (24), E4h
  (16) ; et son contrôle négatif « le décodage de 40h rendu à 24 bits » reste valable.
- **K6. Le contrôle négatif de GR.1a** (« 3C8h qui ne remet pas `state` à zéro → ET4BANC rouge à la sonde, et non au
  diff ») n'est pas sûr. *Déduit.* `state` ne se voit que par des lectures de 3C6h ; un défaut de 3C8h change donc la
  valeur lue (commande ou masque) et rougit le diff avant la sonde ; et le BIOS du mode suivant (30h) touche
  3C8h/3C9h avant la fin du banc, ce qui ramène `state` à 0 quoi qu'il arrive. Pour que la sonde seule rougisse, la
  panne doit porter sur `ctrl` mémorisé sans effet à la lecture (une commande retenue quand `val == FFh`, par exemple,
  ce que le décodage masque dans `bpp` si la valeur ne change pas la profondeur), ou sur `banking` rendu sans effet sur
  les banques. À réécrire ; sans cela la preuve « la sonde mord » (n° 3 de « Ce qu'un bloc livre ») n'est pas faite.
- **K7. « Une série entière par étape qui change l'émulateur ou l'oracle »** (R1 § 2, en tête). La règle du projet
  (mémoire du 07/10, « série entière : seulement si l'émulateur change ») veut un sous-ensemble ciblé pour l'outillage.
  L'oracle seul, la sonde ajoutée à `vid_svga.Probe` (appelée par l'outil, jamais par l'émulation) et un banc `.py`
  n'étant pas un changement de l'émulateur, GR.1a, 1c, 1d, 1e relèvent du sous-ensemble (les portes touchées + `abi` +
  une preuve que M2 est identique, car `Probe` est dans la DLL). *Déduit* : la frontière est au pilote.

## 3. Ajouté (A)

- **A1. Deux jeux de CRTC pour 37h et 38h, choisis par `40:0089h` bit 5.** *Vérifié au désassemblage.* En 77EBh-77FBh,
  pour AL = 37h ou 38h, `call 747Dh` teste `[0489h] & 20h` ; armé, le BIOS prend la table de 25 octets de C000:7281
  (37h)
  ou C000:729A (38h) : CR00 = A1h, CR01 = 7Fh, CR06 = 26h, CR07 = FDh (soit 162 caractères, 808 lignes : le minutage non
  entrelacé), CR13 = 40h/80h ; non armé, les tables 71C0h/7200h du rapport (817 lignes). Les `and 6Fh` de 11D8h ne
  touchent pas le bit 5 : il vient de l'initialisation en 0447h (un mot de configuration) ; sa valeur dans l'émulateur
  est **inconnue**. Le banc GR.1c doit relire `40:0089h` avant le mode 38h et dire lequel des deux minutages il voit ;
  le
  rapport n'en parle pas.
- **A2. L'octet MISC des deux tables (27h) laisse l'horloge à `case 1` dans `et4000_recalctimings`.** *Déduit.*
  `vid_et4000.c:406-419` : `(miscout >> 2 & 3) | (CR34 << 1 & 4)` ; 27h donne 01 : `case 1: break`, l'horloge reste
  celle
  de la VGA (28 MHz) tant que le BIOS ne pose pas CR34 bit 1 (alors 5 → 65 MHz). Le « 44,9 MHz du 8514 » de l'annexe
  n'est donc jamais choisi par PCem ; c'est ce que CR34 vaudra, et que le rapport laisse « à relever », qui décide
  entre 28 et 65 MHz. Un motif de plus pour que GR.1c relève CR34.
- **A3. La détection du RAMDAC a trois appelants, non deux.** *Vérifié* (`grep 'call 0x7ce0'`) : 79E8h (10F0h), 7CCEh
  (10F1h), **7E8Bh** (une routine de 7E8Ah qui ramène la commande à 00h quand un RAMDAC est trouvé, appelée depuis le
  chemin d'un mode non HiColor). Et 10F2h (7CDBh → 7EE9h) lit la commande par 7E47h puis la décode : sous PB-229, la
  lecture de 7E47h ne désarme plus l'IPF, ce que 10F2h n'avait pas à craindre. Le banc HiColor doit appeler 10F2h
  dans les deux modes : un résultat commun en témoigne.
- **A4. Le test de BL dans 10F0h** : BL = 38h est refusé (ni dans la liste de 7A26h-7A36h, ni 13h) ; 10F0h ne pose
  jamais
  le 1024×768 : il reste réservé à AH = 00h, AL = 38h. Et BL bit 7 (7A4Ah) fait « sans effacement ». Les modes
  HiColor atteignables sont 2Dh, 2Eh, 2Fh, 30h (+13h si type ≥ 2). *Vérifié.* GR.1d ne sort donc pas en 1024×768 HiColor
  (1,5 Mo > 1 Mo de la carte, `svga_init(…, 1 << 20)`, `vid_et4000.c:493`).
- **A5. Recouvrement avec R0 (GR.0c) et collision d'ABI.** R0 propose, en GR.0c, « EGABANC qui finit en 10h, HERCBANC
  sur la
  page 1 du graphique » (`R0-pixels.md:256-266`), exactement GR.1e de R1 ; et R0 prend l'ABI 58 en GR.0a, R1 aussi en
  GR.1a (`Oracle.cs:90` : 57 aujourd'hui). À répartir (§ 5).
- **A6. L'EGA n'est pas indépendante de GR.0 pour « finir dans le mode ».** R1 § 2 écrit que GR.1a à 1e ne dépendent
  pas de
  GR.0 « puisque la sonde SVGA hache déjà `buffer32` et `pallook` » ; c'est vrai pour l'ET4000 (`harness.c:2263-2270`,
  `buffer32` et `pallook`) et faux pour l'EGA et l'Hercules, que R1 § 1.3 c dit sans hachage d'image (`harness_ega.c:
  59-101` : `vram`, registres, `egapal`, pas `pallook` ni `buffer32`). Pour l'EGA, finir en 10h ne change que ce que la
  sonde voyait déjà (la VRAM et les registres) tant que GR.0b n'a pas posé le hachage ; le gain visible de GR.1e est
  AND/OR (visibles par la VRAM). Aucun `ega_draw_*` n'est d'ailleurs compilé en PCem hors `4bpp_highres` et `text`
  (C3).
- **A7. Le 8088 et les opcodes 186+.** *Déduit.* Un balayage de `et4000.dis` trouve 348 lignes `pusha/popa/enter/leave/
  bound/ins*/outs*`, et vingt-deux `push imm`/`imul imm`/`shl imm` ; examinés (0x0A2, 0x34F-0x367, 0x36DA…, 0x704E,
  0x7766-0x7784), tous sont des tables ou du texte désassemblés par erreur ; le chemin de détection du RAMDAC
  (7CE0h-7E47h) et le répartiteur d'INT 10h (10C1h-10E0h) sont du 8086 pur. Rien ne prouve que toute la ROM l'est :
  *inconnu*,
  comme dit. Le risque propre au 8088 de PCem : les codes 60h-6Fh y sont des alias de 70h-7Fh (sauts), l'exécution d'un
  186 y serait **silencieusement fausse**, pas un arrêt ; la mesure de GR.1b (invite atteinte, mode 3 posé, comme M19)
  est la bonne, mais un échec se verra comme un « le BIOS ne rend pas la main », non comme une exception.
- **A8. Deux comptes d'attendus qui ne disent pas ce que R1 recopie.** *Vérifié.* `hercbanc.attendus` : 16 + 11 = 27
  octets par mode (son en-tête écrit « les 30 octets relevés ») ; `egabanc.attendus` : trois rangées de 16 = 48 (son
  en-tête et R1 : 41). `et4banc.attendus` : 16 + 16 + 9 = 41, juste. La comparaison est faite par sous-chaîne de ligne
  (`Banc.cs:99-101`), l'écart ne rend aucune porte fausse ; mais « 30 relevés » et « 41 relevés » du tableau de R1 § 1.4
  pour boot-diff ne sont pas des comptes d'attendus : *inconnu* d'où ils viennent (VERIFICATION.md § G9.1-G9.2 ne les
  donne pas).
- **A9. La couverture se prête déjà à une mesure en mode matériel.** Les dossiers `/var/tmp/ixtal-g13/M2-9*-sansgardes`
  existent (gardes dépliées, G13.9) ; ils ne sont pas des listings de campagne, mais montrent la voie du compte matériel
  par M2 sans compteur. *Déduit, non audité.*

## 4. La décision ouverte et les étapes proposées, jugées

**La décision unique de R1 (la portée de la couverture : les `.Materiel.cs`) est à moitié tranchée.** Le plan dit
« dans le mode testé » (PLAN.md:36-37, n° 4) et la décision n° 17 fait du mode matériel le mode de l'utilisateur ;
G13.10b compte déjà les corrections qui ont leur cas (vidéo : 21, VERIFICATION.md:11896). Que les `.Materiel.cs` entrent
dans le compte n'est donc pas à décider : la règle du projet le tranche. **Reste ouverte la méthode** : M2 ne compile
aucune de ces méthodes en mode PCem (C3), et boot-diff refuse le mode matériel ; le compteur jetable de GR.1h doit être
joué par les portes `banc-*-materiel` et les `materiel-cas` (C# seul), non par boot-diff. Proposition : retirer la
décision, la remplacer par « deux tableaux, un par mode, la campagne de chacun nommée », et l'écrire dans GR.1h.

**Les étapes** (jugées sur l'ordre, les dépendances, ce que GR.0 apporte) :

- GR.1a (sonde de l'ET4000) : bonne, **à corriger**. Le `.c` de l'ET4000 est dans le `SRC` du `Makefile` (ligne 106) :
  l'inclure dans un `harness_et4000.c` suppose de l'en retirer, comme `vid_tvga.c` (`Makefile:41-48`) ; le rapport ne
  le dit pas. ABI 57 → 58 ou 59 selon l'ordre avec GR.0a (A5). Contrôle négatif à réécrire (K6). Sous-ensemble de
  portes (K7).
- GR.1b (DOS sur l'ami486, ROM sur 8088) : indépendante, juste ; le risque de silence (A7) à ajouter. Peut se
  faire en premier.
- GR.1c (ET4BANC jusqu'au 38h) : indépendante de GR.0 (la sonde SVGA hache `buffer32` et `pallook`), juste, avec A1
  (relire
  `40:0089h`) et A2 (relire CR34) ; « neuf octets » (K1) à reprendre pour ses attendus réécrits.
- GR.1d (HiColor) : dépend de 1c seulement pour l'outil ; juste, avec K5 (E4h), A3, A4. Le type 4 contre 2 est
  *déduit* et les attendus peuvent être posés d'avance, c'est un bon contrôle que le banc les confirme.
- GR.1e (AND/OR ; fins de banc) : **à scinder**. AND/OR : indépendant. « EGABANC finit en 10h, HERCBANC en graphique » :
  fait double emploi avec GR.0c (A5) et n'apporte rien sans le hachage de GR.0b (A6). Proposé : GR.1e ne garde que
  AND/OR ; les fins de banc vont dans GR.0c, qui le dit déjà.
- GR.1f (clés) : à **scinder** aussi. `memory = 64` et le moniteur mono (`monitor_type` 58, 74, 90, l'échange 3Bx/3Dx)
  ne
  demandent que la trace et les registres, que le diff voit déjà ; l'ambre de l'EGA (pallook) et de la MDA/Hercules
  (`cgapal_rebuild`) attendent GR.0b. Proposé : GR.1f1 maintenant (64 Ko, mono, `r9-videocfg` étendu à `memory` et
  `display_type` hors liste), GR.1f2 après GR.0 (l'ambre). Les `.cfg` sont au nom du device : `[EGA]`, `[MDA]`,
  `[Hercules]` (`device.name`), à vérifier au premier essai (*déduit*).
- GR.1g (MDABANC) : après GR.0, juste ; la part « registres » (R10-R11, R0-R17 relus, 3BAh) ne dépend pas de GR.0 et
  peut
  partir avant, la part pixels (PB-96, entrelacé, `fontdatm` à plat) après.
- GR.1h (couverture) : à bouger **après** GR.1c-g et après GR.0 (les portes qu'ils créent sont la campagne), ce qu'il
  fait ; la méthode matériel est à écrire (décision ci-dessus).
- GR.1i (relecture) : bonne ; 21 bornes (K4) ; les 21 sont des commentaires seuls, M2 identique. Elle peut se faire
  tout de suite, sans attendre les autres étapes (aucune porte ne lit les `// pcem:`).

Dépendances à GR.0, en résumé : GR.1a, 1b, 1c, 1d, 1e (AND/OR), 1f1, 1g (registres) : aucune. GR.1f2, 1g (pixels), et
les
fins de banc d'EGA et d'Hercules : GR.0b (hachage) et GR.0c. GR.1h : après tout.

## 5. Ce qu'il faut faire de ce rapport

Corriger R1 : K1 à K7 ; ajouter A1 à A5, A8 ; remplacer sa décision ouverte (§ 4) ; décider, avec R0, qui porte
les fins de banc (GR.0c, proposé) et l'ordre des ABI. Aucun défaut de PCem neuf ici (rien à ajouter à « Candidats, hors
de la portée » ; A1 et A2 sont des faits de la ROM, pas des défauts du PCem).
