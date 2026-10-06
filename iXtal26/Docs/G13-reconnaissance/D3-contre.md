# D3 — Contre-lecture : le stockage

> Contre-lecture du 6 octobre 2026 de `D3-stockage.md`, sur `master` à `bc609ce`. Lecture seule : aucun fichier du
> dépôt modifié, rien construit ni lancé. Les images de `os/` n'ont été lues que pour leur taille et les octets 0Bh à
> 1Ah de leur premier secteur. Les sources primaires citées ont été relues à leur URL (liste en C9) ; celles qui ne
> l'ont pas été sont dites « non revérifiée ici ».
>
> Niveaux : *documenté*, *déduit*, *inconnu*, comme D3. Classes (a) à (d) comme D3, plus « hors G13 » : une
> correction qui vaut dans les deux modes et n'attend pas le mode matériel (R9, ou protection des fichiers de
> l'utilisateur).

## En tête : les deux verdicts

- **PB-17 : l'hôte tombe bien, et rien ne rattrape l'exception** (C1). Mais D3 se trompe sur les images qui la
  déclenchent et sur la taille du remède (K1, K2). Deux plantages voisins ne sont pas inscrits : une division par
  zéro (A1) et un fichier vide (A2). Aucune image actuelle de `os/` ne déclenche l'un des trois (C2).
- **PB-121 : vrai. C'est une corruption de l'image de l'utilisateur, dans l'usage courant** : un reset matériel
  depuis le menu, avec le profil SCSI (C3). **C'est un défaut à corriger hors de G13, dans les deux modes, et
  avant G13.** Aucune porte n'en dépend (K3). R9 ne le couvre pas : il faut une décision.

---

## 1. Confirmé

**C1 — PB-17 : l'exception est réelle et rien ne la rattrape.**
- Le mécanisme : `img_seek` lit `sectors × sector_size` octets par face dans `track_data[côté]`, un tableau de
  20 480 octets (`Disc/disc_img.cs:38`). La lecture passe par `fread`, qui prend `buf.AsSpan(0, size × count)`
  (`:86-87`). Au-delà de 20 480 octets, cet appel lève `ArgumentOutOfRangeException`.
- Le chemin au menu : `SdlMenu.Insert` (`Host/SdlMenu.cs:966-985`, appelé en `:522`, `:819` et `:1091`), puis
  `disc_load` (`Disc/disc.cs:120-158`, `:148`), `fdd_disc_changed` (`Floppy/fdd.cs:140-147`), `disc_seek`
  (`Disc/disc.cs:296-299`) et `img_seek` (`Disc/disc_img.cs:438-466`).
- Le chemin au démarrage : `resetpchard` (`pc.cs:1196-1198`), qu'appellent `initpc` (`pc.cs:967`) et le reset
  matériel du menu (`Host/SdlHost.cs:481-487`).
- Aucun `catch` sur ces chemins. Les filtres de l'hôte ne visent que des exceptions d'E/S, autour d'ouvertures de
  fichiers. Aucun n'entoure `Insert`, la boucle (`Host/SdlHost.cs:459-544`) ni `RunMachine`
  (`Host/CommandLine/Launcher.cs:262-268`), et aucun gestionnaire `UnhandledException` n'existe dans `iXtal26/`.
  Les filtres de `pc.cs:1025` et de `Host/FatImage.cs:638`, `:654` et `:951` admettent `ArgumentException`,
  dont `ArgumentOutOfRangeException` dérive, mais aucun n'est sur ce chemin. — **documenté** (code).
- Le registre le savait déjà : « le C# lève » (`PCEM_BUGS.md:1841`), et le marqueur aussi
  (`Disc/disc_img.cs:454-455`). PB-17 date d'avant R9 (1er octobre) et n'a jamais été repris. Le précédent
  s'applique : PB-110, une feuille CUE, a été traité comme une donnée de l'utilisateur sous R9. Ce précédent est
  la décision n° 13 de `PLAN-G10.md:222-224`. Le registre l'appelle « décision n° 4 de G10.3 »
  (`PCEM_BUGS.md:1177`) ; c'est la numérotation locale de G10.3, dont `VERIFICATION.md:6152-6153` dit qu'elle
  devient n° 11 à 13. — **documenté**.

**C2 — Aucune image de l'utilisateur ne déclenche PB-17 aujourd'hui.**
- `os/` contient 36 fichiers `.img`, `.ima`, `.360` ou `.xdf`. Les disquettes ont un BPB valide à 9 ou 18
  secteurs de 512 octets, soit des pistes de 4 608 ou 9 216 octets.
- Les disques durs portent un MBR : leur BPB est invalide, et `img_load` passe par la taille, avec 36 secteurs au
  plus, soit 18 432 octets (`Disc/disc_img.cs:240-304`). Le menu les liste pourtant comme disquettes
  (`Host/SdlMenu.cs:43`).
- Les profils posent `bpb_disable = 0` (par exemple `ixtal26-486-scsi.cfg`). Avec `bpb_disable = 1`, le chemin
  par taille ne déborde jamais : c'est un contournement d'ici là. — **documenté** (lecture des en-têtes et du
  code).

**C3 — PB-121 : le scénario reconstitué. L'image peut réellement être écrasée par des données périmées.**
1. Au démarrage, `scsi_bus_init` (`Scsi/scsi.cs:507-527`) appelle `scsi_hd_init` (`Scsi/scsi_hd.cs:80-93`), qui
   appelle `hdd_load`. Le fichier s'ouvre en `rb+` : c'est le flux S1, inscrit en tête du registre fort
   `flux_ouverts` (`Disc/hdd_file.cs:72-79`).
2. Le disque écrit secteur par secteur (`Scsi/scsi_hd.cs:647`, `:704`). Chaque `Write` de 512 octets reste dans
   le tampon de 4 Ko du `FileStream` jusqu'au prochain `Seek` sur ce même flux. Les deux faits sont mesurés dans
   le dépôt : `pc.cs:1323-1324` (M13) et `VERIFICATION.md:6695-6697` (G11.0). Le dernier secteur écrit avant le
   reset reste donc en vol, si la dernière opération du disque était une écriture.
3. L'utilisateur choisit « Reset materiel » (`Host/SdlMenu.cs:102-103`, puis `Host/SdlHost.cs:481-487`).
   `resetpchard` appelle `device_close_all`, puis `scsi_aha1542c_close` (`Scsi/scsi_aha1540.cs:2000-2005`), puis
   `scsi_bus_close` (`Scsi/scsi.cs:535-546`), qui ne ferme rien. S1 reste ouvert, tampon plein, tenu par le
   registre.
4. `device_add` rappelle `scsi_bus_init`, qui ouvre S2 sur le même fichier (partage `ReadWrite | Delete`,
   `Disc/hdd_file.cs:70-77`). S2 passe en tête : le registre vaut `[S2, S1]`.
5. L'invité relit le secteur X par S2 et reçoit son contenu d'avant la dernière écriture.
6. À la sortie, `closepc` appelle `fflush_tous` (`pc.cs:1344`, `Disc/hdd_file.cs:95-102`) : S2 est vidé,
   puis S1. X reprend la valeur écrite avant le reset, par-dessus tout ce qui a été écrit en X depuis.

Chaque nouveau reset ajoute un flux orphelin. Comme le plus ancien est vidé en dernier, c'est la plus ancienne
écriture en vol qui gagne.

C'est l'usage courant :
- le profil existe (`ixtal26-486-scsi.cfg` : `hdd_controller = aha1542c`, `hdc_fn = os/486-SCSI-C.img`) ;
- le reset matériel est une entrée du menu principal ;
- à l'invite de DOS, après une copie, la dernière opération du disque est presque toujours l'écriture d'un
  secteur de répertoire ou de FAT.

Un exemple : `COPY A:X C:`, puis reset. DOS relit le répertoire sans X ; l'utilisateur recopie X. À la sortie,
le secteur de répertoire d'avant le reset écrase celui d'après : l'entrée de la seconde copie disparaît, et ses
clusters deviennent perdus. Il y a corruption dès que la session d'après le reset écrit X, ou écrit ailleurs en
se fondant sur la version périmée de X.

Niveaux : **documenté** pour le mécanisme (code et mesures du dépôt) ; **déduit** pour l'effet sur le système de
fichiers.

**C4 — PB-121 est mal rangé en section C.** Son entrée décrit elle-même un effet observable : « écrase ce qui a
été écrit depuis » (`PCEM_BUGS.md:2682-2686`). — **documenté**.

**C5 — L'effet réel de PB-16 dans iXtal est bien celui que décrit D3.** On peut le préciser.
- `disc_close` et `disc_reset` remettent `seek` à nul (`Disc/disc.cs:159-171`, `:266-282`), et `disc_seek` le
  teste (`:296-299`). `Stream.Close()` est idempotent.
- READ DATA tombe sur `disc_notfound = 1000` (`Disc/disc.cs:302-309`) : « adresse non trouvée ».
- READ ID n'a ni rappel ni échéance (`Disc/disc.cs:323-329`, `Floppy/fdc.cs:766-771`). Le contrôleur reste en
  phase d'exécution jusqu'au délai du BIOS.
- Le menu annonce « inséré » (`Host/SdlMenu.cs:974-984`). — **documenté** (code).

**C6 — PB-25 : les deux sites de l'AT ne portent pas de marqueur** (`Mfm/mfm_at.cs:186`, `:190`). De plus,
l'entrée ne cite que `mfm_at.c:113` (pas `:121`), et sa ligne « Reproduit » ne nomme que `Mfm/mfm_xebec.cs`
(`PCEM_BUGS.md:202-203`). — **documenté**.

**C7 — PB-30 : le registre dit « conservées », le code les omet** (`PCEM_BUGS.md:2398` contre
`Mfm/mfm_xebec.cs:39`, `:98`, `:109`). — **documenté**.

**C8 — Les sites sans marqueur sont confirmés.**
- PB-109 n'est nommé que par l'en-tête (`Cdrom/cdrom_image.cs:18`).
- PB-127 a huit sites sans marqueur (`Scsi/scsi_zip.cs:764-960`).
- PB-134 n'apparaît que dans les en-têtes (`Scsi/scsi_hd.cs:14`, `Scsi/scsi_aha1540.cs:16`).
- PB-129 à PB-131 aussi (`Scsi/scsi_hd.cs:14`), et seul `:148` nomme PB-130. — **documenté**.

**C9 — Les sources relues à leur URL, et ce qu'elles établissent ici.**
- IBM Fixed Disk Adapter (URL de D3) :
  - p. 3, *Status Register* : bit 1 erreur, bit 5 « the logical unit number of the drive » ;
  - p. 4, *Sense Bytes* : bit 7 « address-valid », octets 1 à 3 ;
  - p. 6 : 20h « Invalid Command », 21h « Illegal Disk Address » ;
  - p. 7 et 10-11 : le DCB, et Format Track, opcode 06, secteur à zéro dans l'octet 2.
- IBM PC AT Technical Reference : *Data Rates* (« three data rates: 250,000, 300,000 and 500,000 ») ; le listing
  du BIOS (« 01: 300 KBS »).
- ATA-1 r4 : § 6.3.10, § 7.2.11, § 7.2.13 et § 9.19, lus en texte intégral
  (http://www.os2museum.com/files/docs/ata/ata-r4-utf8.txt).
- ATA-3 r7b : § 4.2.10, les règles des registres, le bit BSY, § 7.19.
- SFF-8020i r2.6 (22 janvier 1996) : Table 37, § 10.8.3, § 10.8.4, § 10.8.8, § 10.8.9 et Table 77, § 10.8.19
  et Table 131, la note sur READ CD-ROM CAPACITY.
- SCSI-2 (version HTML de D3) : § 6.2.2 (hard et soft), § 7.2.2, § 7.5.3, § 7.6, § 7.9, § 8.2.5.1 et Table 45.
- AHA-1540C Technical Reference :
  - le port de contrôle, *Hard Reset Operations*, *SCSI Reset Operations* ;
  - les commandes 01h, 02h, 03h, 0Dh, 22h et 23h ;
  - MBO, MBI, le CCB (octets 1 et 3), les statuts d'hôte ;
  - les réglages d'usine.
- µPD765 : WRITE DATA (« every 31 µs in the FM mode, and every 15 µs in the MFM mode »), bit OR de ST1
  (https://archive.org/download/bitsavers_necdatasheec78_1042541/uPD765_Data_Sheet_Dec78_djvu.txt).
- INF-8070i : Table 41 (« last valid LBA »), Table 59 (LoEj/Start).
- MMC-2 97-108r0 : § 9.1.2.

Non revérifiées ici : la Figure 11 de SFF-8020i (PB-106), le format CUE (PB-108), le PC87415 (PB-71), le manuel
de la 1540A (PB-142), la page de Brouwer (PB-126), SCSI-2 § 8.2.14, § 8.3.3, § 9.1.2 et § 9.2.7.

**C10 — PB-117 : l'attendu 31 est juste.** `cdrom_capacity` vaut le début du lead-out plus un
(`Cdrom/cdrom-image.cs:585`, `image_get_last_block` `:223-247`). Il faut donc rendre `size() − 2`. Le seul
utilisateur de `size()` est `Scsi/scsi_cd.cs:1647`. — **documenté**.

**C11 — PB-144 : le sens de transfert ignoré est conforme à la carte.** Le TR, CCB octet 1 : « For an Initiator
CCB, the direction of data transfer is established by the SCSI command being executed independent of the value
of bits 3 and 4 ». Le TR ajoute deux phrases utiles au contrôle de longueur à écrire :
- « If both bits are set for an Initiator CCB, the command must perform no data transfer » ;
- « if a data underrun/overrun condition occurs ... and the direction bits are set to zero, the host adapter
  completes the operation without error ».

Les réglages d'usine sont aussi confirmés : ID 7, DMA 5, IRQ 11, 330h, DC000h. Et
`nvr/default/aha1542c.nvr` commence par `07 71` : ID 7, DMA 7, IRQ 10. — **documenté**.

**C12 — Restent tels que D3 les classe**, sources confirmées ou non contredites :
- PB-14, 22, 23 (sources relues, C9) ;
- PB-28, 106, 107, 108, 109, 122 ;
- PB-129, 130, 133, 137, 138, 139, 141, 142, 143.

Les comptes de D3 tiennent : 27 + 7 + 3 + 11 = 48.

---

## 2. Corrigé

**K1 — PB-17 : D3 se trompe sur les images qui le déclenchent.**
- `img_load` pose toujours `sector_size = 512` et ne prend jamais la taille de secteur du BPB
  (`Disc/disc_img.cs:238`, C `disc_img.c:158`). La lecture fait donc `spt × 512` octets par face, et non
  `spt × bps` octets.
- Dès lors, sur le chemin du BPB (côtés 1 ou 2, `bps` de 128 à 2 048), l'hôte tombe si et seulement si
  `spt ≥ 41` et `spt × bps ≤ 25 000`.
- Une exception : `bps = 512` avec `spt = 47`, que PB-16 éjecte.
- À 512 octets par secteur, cela donne :
  - de 41 à 45 secteurs, une image ordinaire ;
  - 46 secteurs, l'XDF ED ;
  - 48 secteurs, l'XXDF ED.
- Pour `bps ≤ 609`, n'importe quel `spt` de 41 à `⌊25 000 / bps⌋` déclenche aussi l'exception, même en double
  densité. Exemple : 41 × 128 octets, soit 5 248 octets par piste.
- Le « 24 × 1 024 » de D3 ne tombe pas : la lecture fait 12 288 octets.
- Le chemin par taille ne tombe jamais.
- L'entrée du registre, qui ne parle que de l'XDF ED (`PCEM_BUGS.md:1835-1838`), est à élargir de même.
— **documenté** (code).

**K2 — PB-17 : un tampon de 32 Ko ne suffit pas** (Q3 et la fiche de D3).
- La lecture la plus longue que `img_load` admet fait 195 × 512 = 99 840 octets : `bps = 128` et `spt = 195`,
  soit 24 960 octets par piste selon le BPB.
- Avec 32 Ko, les images de 65 à 195 secteurs, à `bps ≤ 384`, font encore tomber l'hôte.
- Deux remèdes :
  - (i) un tampon d'au moins 99 840 octets, ou 256 × 512 pour couvrir tout `uint8_t`. Les offsets de
    `disc_sector_add` restent dans le tampon, et la table des secteurs est déjà bornée (`MAX_SECTORS = 256` et
    sa garde, `Disc/disc_sector.cs:59`, `:102`) ;
  - (ii) le refus de toute image dont `spt × 512` dépasse 20 480 octets. Ce refus suppose le nettoyage d'hôte de
    PB-16 (`f` à nul, lecteur vide), sans quoi l'image refusée paraît « insérée » (C5).
- Le test de survie doit couvrir 45 × 512, 46 × 512, 48 × 512, 195 × 128, A1 et A2, pas la seule XDF ED.
— **documenté** (code).

**K3 — PB-121 n'est pas une affaire de mode matériel.**
- Selon D3, « le registre de flux et son ordre restent indispensables au mode PCem ». C'est inexact : seul le
  vidage à la sortie est tenu par une porte. Le contrôle négatif de G11.0 rougit sans vidage
  (`VERIFICATION.md:6692-6698`). L'ordre, lui, n'est exercé par aucune porte : « Aucune porte ne fait de reset
  matériel au milieu d'une session » (`PLAN-G11.md:335-337`).
- La phase 2 de `boot-diff`, qui ré-amorce dans le même processus (`tools/iXtal26.Diff/BootDiff.cs:878-880`,
  `:1123`), ne tourne qu'après une divergence. — **documenté**.
- Une correction dans les deux modes ne change donc aucun verdict de porte. Deux formes possibles :
  - (i) une DEVIATION d'hôte, pendant de la décision n° 7 de G11 : vider tous les flux du registre en tête de
    `resetpchard`. `scsi_bus_close` reste transcrit tel quel ;
  - (ii) une vraie fermeture dans `scsi_bus_close`.
- R9 ne la couvre pas : « Hors R9 : ... les défauts sans arrêt de l'hôte, qui restent reproduits »
  (`TRANSCRIPTION.md:76-77`). Il faut une décision : une exception nouvelle, « l'invité ne corrompt pas les fichiers
  de l'utilisateur ».
- Le test, en C# seul : écrire un secteur par un CCB, `resetpchard`, relire (contenu neuf attendu), `closepc`,
  relire le fichier (contenu neuf attendu). Le contrôle négatif : le code d'aujourd'hui rend l'ancien contenu
  aux deux relectures.
- C'est l'étape ST.0, pas ST.6.

**K4 — PLAY AUDIO MSF n'accepte pas une piste de données.** Le point 8 de D3 se trompe.
- Avant de jouer, `scsi_cd` consulte `is_track_audio` (`Scsi/scsi_cd.cs:1472-1477`, C `scsi_cd.c:1375-1378`),
  qui convertit bien le MSF (`Cdrom/cdrom-image.cs:103-121`). Une piste de données est refusée, déjà aujourd'hui,
  en 05/64h.
- La « conséquence inscrite » de PB-111 (`PCEM_BUGS.md:2194-2196`, `Cdrom/cdrom-image.cs:16-18`) vaut pour le
  moteur appelé seul, par `cdimage-check` ; l'invité ne la voit pas. PB-123 le dit d'ailleurs : « Seul PLAY
  AUDIO MSF joue juste » (`PCEM_BUGS.md:1323-1324`).
- La source citée est aussi la mauvaise. § 10.8.8 traite PLAY AUDIO (10). Pour MSF, c'est § 10.8.9 : CHECK
  CONDITION exigé, et 05/64h seulement « Recommended » (Table 77). — **documenté**.
- Les défauts réels de ce chemin sont ailleurs (A4, A5).

**K5 — PB-74 : le compte à zéro en fin normale est documenté, pas déduit.**
- ATA-1 r4 § 7.2.11 : « If this register is zero at command completion, the command was successful ».
- Mais ATA-3 r7b § 7.19 dit « NORMAL OUTPUTS - None required ». L'attendu « secteur 5 » n'est donc fondé que
  sous ATA-1, ce qu'il faut écrire au test. — **documenté**.

**K6 — PB-72 : ATA-3 ne tranche pas, il se contredit.**
- La description du registre d'état dit : « a write to a command block register by the host shall be ignored by
  the device ».
- La section des registres de commande dit : « If the host writes to any Command Block register when BSY or DRQ
  is set to one, the results are indeterminant and may result in the command in progress ending with a command
  abort error ».
- Q6 ne peut donc pas s'appuyer sur ATA-3 seul. PB-72 reste (c) ; il passe en (b) au mieux, par décision.
— **documenté** (ATA-3 r7b, URL de D3).

**K7 — PB-19 : la correction proposée bloquerait toute écriture en PIO.**
- D3 écrit que « la remise à 0 à l'écriture de 3F5h existe déjà ». C'est faux. Les remises à zéro sont au début
  de WRITE DATA (`Floppy/fdc.cs:705`, C `fdc.c:554`) et à la fin de `fdc_getdata` (`:1405`, C `:1219`).
- L'écriture de 3F5h en phase d'exécution (`Floppy/fdc.cs:483-497`, C `fdc.c:328-336`) ne touche pas au drapeau.
- Poser seulement le drapeau à 1 lèverait OR dès le deuxième octet de toute écriture en PIO, même à temps.
- Il faut deux sites : poser à 1 dans `fdc_getdata` en PIO, à la place de la remise à zéro ; remettre à 0 dans
  l'écriture de 3F5h en phase d'exécution.
- Le test doit avoir son cas positif : un écrivain ponctuel ne voit pas OR. Le chemin FIFO reste à penser.
— **documenté** (code ; µPD765 relu).

**K8 — PB-131 : un attendu faux, et un état présent mal décrit.**
- Pour 96 octets d'INQUIRY, la longueur additionnelle vaut **5Bh**, pas 5Ch. SCSI-2 Table 45 : « Additional
  length (n-4) », où n est l'indice du dernier octet ; 36 octets donnent bien 1Fh.
- Le « 60h aujourd'hui » que D3 attribue à INQUIRY sur un LUN absent est faux. Le test général du LUN
  (`Scsi/scsi_hd.cs:168-174`, C `scsi_hd.c:131`, où l'exception d'INQUIRY est commentée) refuse en 05/25h avant
  la branche 60h (`:232-235`), qui est morte. Avec le LUN dans le seul CCB (PB-133), le disque répond comme LUN 0,
  avec 00h.
- SCSI-2 § 7.5.3 : sur un LUN invalide, REQUEST SENSE ne rend pas CHECK CONDITION ; il rend des données de sense
  05/25h. — **documenté**.

**K9 — PB-132 : SCRST n'est pas un HRST.**
- Le TR : SCRST « is managed as a SCSI Soft Reset and will allow partially completed operations to continue
  after the reset occurs », et SCRD « will not be set, since the host itself caused the reset ».
- HRST remet la carte en l'état de la mise sous tension et réinitialise le bus.
- D'où une correction en deux temps. HRST : remettre la carte et appeler `scsi_bus_reset`, en (a). SCRST : remettre
  le bus sans abandonner les CCB. Le sort du CCB en vol, dont la cible a été remise, est **inconnu** : partie (c).
- SCSI-2 § 6.2.2 : les deux alternatives s'excluent dans un système. Le bit SftRe d'INQUIRY vaut 0 chez PCem :
  le disque suit l'alternative dure, d'où UNIT ATTENTION, cohérent avec D3. — **documenté**.

**K10 — PB-140 : l'octet 25h n'est pas celui des interrupteurs.** 25h est l'« Auto Retry Option », rendu nul ;
26h donne les interrupteurs, avec en bit 7 « EEPROM read data » (TR, Return Setup Data). — **documenté**.

**K11 — PB-16 : l'entrée du registre est fausse pour le C aussi.**
- `img_seek` sur le `FILE *` fermé n'est pas atteint en C non plus : `disc_seek` teste le pointeur
  (`disc.c:206-208`), et `disc_close` comme `disc_reset` le remettent à nul (`disc.c:94-107`, `:182-201`).
- Le vrai comportement indéfini est le second `fclose`, sur un pointeur pendant, à la prochaine `disc_close` ou
  au prochain `disc_reset` (`disc_img.c:323-327`). Chaque reset matériel en fait un (`pc.c:366`).
- La glibc peut alors s'arrêter sur un « double free » : chez PCem seulement.
— **documenté** pour le chemin ; **déduit** pour la réaction de la glibc.

**K12 — PB-71 : la source principale est faible.** Le PC87415 est une puce PCI en mode compatible, qui ne prouve
rien pour une carte ISA d'AT. La preuve est ailleurs :
- interne : `ide_irq_raise` et `ide_irq_lower` lèvent et baissent l'IRQ 15 pour le canal secondaire
  (`Ide/ide.cs:205-222`) ;
- dans la norme : ATA-1 § 6.3.10, INTRQ propre à l'unité.
La classe (a) tient. — **documenté**.

**K13 — Le défaut voisin d'`ide_irq_update` est à reformuler.**
- ATA-1 r4 § 6.3.10 : « If nIEN=1, or the drive is not selected, this output is in a high impedance state ».
  Baisser la ligne sur une écriture de 1F6h qui choisit l'autre unité, sans interruption en attente, est donc
  juste.
- Le défaut se limite au cas où l'unité choisie tient encore son interruption, avec nIEN = 0. ATA-1 ne fait
  retomber INTRQ que sur RESET-, SRST, une écriture du registre de commande ou une lecture d'état.
- Il a deux visages :
  - l'IRQ en attente, pas encore prise, est perdue ;
  - prise et en service, elle peut être **doublée**. `picintc` remet `pic_current` à zéro (`Models/pic.cs:398`),
    et après l'EOI une écriture de 1F6h ou de 3F6h la relève (`Ide/ide.cs:230-231`).
— **documenté** (ATA-1 relu, code).

**K14 — PB-120 : deux précisions documentées.**
- L'ordre : « The first TOC entries shall be the A0, A1, A2h pointers » (SFF-8020i § 10.8.19).
- Le type de disque n'est pas déduit, il est documenté : la Table 131, « Disc Type Byte », donne 00h, 10h et
  20h.

**K15 — PB-118 : la variante MMC-2 de D3 est incomplète.** Il manque, d'après 97-108r0 § 9.1.2 :
- le refus en 05/24h quand IMMED = 0 et que la cible n'a pas de file étiquetée ;
- le filtrage par classe demandée (« only the Event Data Header ... Class of 0 ») ;
- « This command shall not return a Unit Attention check condition ».
Le texte de MMC-2 donne aussi deux définitions de la longueur. L'attendu 0006h suit la seconde. — **documenté**.

**K16 — PB-119 : il manque la troncature.** SFF-8020i § 10.8.4 : une longueur qui tronque l'en-tête ou une page
rend CHECK CONDITION, PARAMETER LIST LENGTH ERROR (05/1Ah). — **documenté**.

**K17 — PB-123 : PLAY AUDIO (12) n'est pas dans SFF-8020i.** A5h manque à la Table 37. Sous Q5 au sens strict,
A5h serait refusé en 05/20h ; D3 lui applique pourtant le +150. — **documenté**.

**K18 — PB-136 : le TR est lui-même ambigu.** « If a full 32 byte transfer is not required, the number of bytes
supplied is 31 minus the offset value. An offset of zero transfers 32 bytes. » L'inconnu hors bornes s'en trouve
renforcé, et même l'attendu dans les bornes dépend de la lecture de l'octet 1. — **documenté**.

**K19 — Le compte des `fatal()` du Xebec.** `PLAN.md:375` en compte « dix-sept ». Le C en a 19 vivants (20
occurrences, dont `:149` commentée), le C# 19 aussi. À recompter au plan. — **documenté** (relevé du code).

**K20 — Le classement de PB-17 et de PB-121.** D3 les met en (a) et en G13. Les deux relèvent de « hors G13 » :
- PB-17 au titre de R9, étendu aux données de l'utilisateur par la décision n° 13 ;
- PB-121 au titre de la protection des fichiers de l'utilisateur.
Dans les deux cas, la correction vaut dans les deux modes, et elle est neutre pour les portes.

---

## 3. Ajouté

**A1 — Une division par zéro dans `img_load`, non inscrite.**
- Un BPB valide par ailleurs, avec `bps` de 128 à 2 048 et 1 ou 2 côtés, mais `spt = 0` à l'octet 18h, fait
  diviser par zéro : `tracks = total / (sides × spt)` (`Disc/disc_img.cs:311`, C `disc_img.c:219`).
- En C# : `DivideByZeroException`. En C : SIGFPE.
- L'hôte tombe à l'insertion ou au démarrage, comme pour PB-17, et cela relève de R9 (décision n° 13 par
  analogie). Une entrée est à créer en section B.
- Plausible sur une disquette auto-amorçable sans BPB, dont le secteur 0 est du code. — **documenté** (code).

**A2 — Un fichier vide fait tomber `disc_load` : défaut de transcription.**
- `Disc/disc.cs:134` fait `f.Seek(-1, SeekOrigin.End)`. Sur un fichier de 0 octet, .NET lève
  `IOException(SR.IO_SeekBeforeBegin)` (dotnet/runtime, `OSFileStreamStrategy.Seek`,
  https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/IO/Strategies/OSFileStreamStrategy.cs).
- Chez PCem, `fseek` échoue, `ftell` rend 0, la taille vaut 1, et l'image se monte.
- Ce n'est donc pas un PB : c'est une transcription à corriger. `zip_load` a déjà sa DEVIATION pour ce cas
  (`Scsi/scsi_zip.cs:146-148`).
- Un `.img` vide dans `os/` est listé par le menu. — **documenté**.

**A3 — Un plantage de l'hôte peut aussi perdre les écritures en vol.**
- `closepc` est le seul vidage des images (`pc.cs:1329-1345`, `Disc/hdd_file.cs:88-92`).
- Le `using var host` de `Launcher.cs:262` n'appellerait `Dispose`, donc `closepc`, que si l'exécution déroule la
  pile. Or « Whether the finally block executes depends on whether the operating system chooses to trigger an
  exception unwind operation »
  (https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/statements/exception-handling-statements).
- PB-17, A1 et A2 au menu peuvent donc coûter le dernier secteur écrit de chaque disque dur.
- À mesurer en C# seul. Un filet d'hôte, hors G13, réglerait la question : rattraper au sommet de la boucle,
  vider les images, dire l'erreur. — **déduit**.

**A4 — `image_playaudio` teste la piste sur le MSF compacté, et l'invité peut ne rien entendre sans erreur.**
- Après le contrôle juste de `scsi_cd` (K4), `image_playaudio` cherche la piste à la valeur
  `m × 65 536 + s × 256 + f`, avant la conversion (C `cdrom-image.cc:83`, puis `:89-96` ;
  `Cdrom/cdrom-image.cs:132-134`).
- Si cette valeur tombe dans une piste de données, la lecture est annulée en silence, alors que la commande a
  rendu GOOD (`Scsi/scsi_cd.cs:1479-1481`).
- Cela ne peut arriver que si une piste de données commence au-delà du LBA 65 536, en fin de disque : un CD
  Extra, par exemple.
- Non inscrit. Classe (a), au regard de SFF-8020i § 10.8.9. — **documenté** pour le code ; **déduit** pour la
  fréquence.

**A5 — `is_track_audio` en MSF juge 150 secteurs trop loin.**
- `MSF_TO_FRAMES` ne retranche pas 150 (`includes/private/dosbox/cdrom.h:64`). Or les débuts de piste sont des
  LBA, auxquels `GetAudioTrackInfo` ajoute 150 pour le MSF (`dosbox/cdrom_image.cpp:108`).
- Les deux dernières secondes d'une piste sont donc jugées sur la suivante :
  - 05/64h à tort à la fin de la dernière piste audio, ou d'une piste audio suivie de données ;
  - acceptation à tort de la fin d'une piste de données suivie d'audio.
- Non inscrit. Classe (a). — **documenté** (code).

**A6 — Le sense du Xebec : réel, documenté, sans effet sur le BIOS d'IBM.**
- Ce que rend la carte : octet 0 = A1h quand « the previous command required a disk address » ; octet 1 = unité
  (bit 5) et tête ; octet 2 = bits hauts du cylindre et secteur ; octet 3 = cylindre bas. PCem rend
  `err, unité, 0, 0` (C `mfm_xebec.c:314-328`).
- Le BIOS du Fixed Disk Adapter masque `AND AL,0FH` et `AND BL,30H` (STAT_ERR, lignes 1284-1291 du listing, même
  document) : seul un programme qui lit le sense brut voit l'écart.
- Classe (a), de faible valeur. — **documenté**.

**A7 — `img_load` ignore la taille de secteur du BPB.** Le défaut est non inscrit et explique K1. Une image dont
le BPB porte 1 024 octets par secteur se voit présentée en secteurs de 512 octets, à des offsets de piste faux
(`Disc/disc_img.cs:238`, `:458-466`). Pour le mode matériel, c'est une question de format d'image d'hôte :
classe (d), ou Q2. — **documenté** (code).

**A8 — La commande 23h ignore son drapeau.** Le TR, Return EEPROM, octet 0 : « 1 = Return configured options,
0 = Return default options ». `Scsi/scsi_aha1540.cs:1127-1135` rend toujours la configuration. Non inscrit ;
classe (b). La ROM v1.01 émet deux 23h au POST (`VERIFICATION.md` § G11.0), avec un drapeau inconnu. — Effet
**inconnu**.

**A9 — Une branche morte et non conforme dans INQUIRY.** La branche « 60h » de `scsi_hd` est morte (K8). Elle
est en outre non conforme : un qualificatif 011b exige le type 1Fh, donc 7Fh (SCSI-2 § 8.2.5.1). Une entrée est à
créer en section C ; classe (d). — **documenté**.

**A10 — UNIT ATTENTION est dû aussi à la mise sous tension, et cela touche les témoins de ST.6 et ST.7.**
- SCSI-2 § 7.9 : « whenever the target has been reset by a BUS DEVICE RESET message, a hard reset condition, or
  by a power-on reset ».
- Un disque ou un ZIP conforme rend donc CHECK CONDITION à la première commande de la ROM après la mise sous
  tension. D3 ne le prévoit qu'après un reset.
- Si on le modélise, chaque amorçage passe par le sense automatique corrigé (PB-137). Les « images identiques »
  des témoins sont alors à mesurer, pas à supposer.
- Si on ne le modélise pas, l'écart à § 7.9 est à écrire au plan. — **documenté** (norme) ; issue **inconnue**.

**A11 — PB-132 pousse à PB-121.** Le bus figé fait disparaître les disques « jusqu'au reset matériel du PC »
(`PCEM_BUGS.md:1431`). Le seul recours de l'utilisateur est donc le reset du menu, qui ouvre le scénario de C3.
C'est une raison de plus de corriger PB-121 d'abord. — **déduit**.

**A12 — En ST.5, recoller le disque ZIP doit réutiliser le flux existant.** Chaque `resetide` alloue un lecteur
neuf (`Scsi/scsi_zip.cs:220-231`, C `scsi_zip.c:165-177`). L'ancien flux reste au registre (`Disc/hdd_file.cs:78`). Rouvrir le fichier
reproduirait PB-121 sur l'image ZIP. Aujourd'hui, rien ne la rouvre, donc rien ne l'écrase. — **documenté** (code).

**A13 — PB-26 a un site sans marqueur** : `Mfm/mfm_xebec.cs:211`, soit C `:174`, la chaîne
« STATE_START_COMMAND » dans `STATE_RECEIVE_COMMAND`. Les marqueurs `:164` et `:788` couvrent les trois autres.
— **documenté**.

**A14 — PB-127 n'apparaît même pas dans l'en-tête de `Scsi/scsi_zip.cs`** (lignes 7-10 : PB-125 et PB-126
seulement). — **documenté**.

**A15 — La liste de Q12 est incomplète.**
- Il y manque PB-73, PB-75, PB-76 et PB-110.
- Le repli de PB-76 (ABRT) n'est pas le comportement du WD1003, qui transfère 512 octets plus 4 d'ECC
  (`PCEM_BUGS.md:757`) : question de mode matériel.
- Le vrai comportement de PB-75 (une commande à l'unité 1 absente) : **inconnu**.

**A16 — Le texte de R9 ne couvre que « le code invité »** (`TRANSCRIPTION.md:71-77`). PB-17 et A1 font tomber
l'hôte par une donnée de l'utilisateur. Deux voies : les ranger sous R9 par une décision, sur le modèle de la
n° 13 de G10 ; ou amender R9 (`TRANSCRIPTION.md` compte 218 lignes, sous le plafond de 240).

**A17 — Le registre à corriger, en un seul lieu.**
- PB-16 : le mécanisme côté C (K11).
- PB-17 : son statut devient « non reproduit (R9) », et son ensemble déclencheur s'élargit (K1).
- PB-25 : les sites de l'AT et `:121` (C6).
- PB-26 : le site `:211` (A13).
- PB-30 (C7).
- PB-111 et PB-123 : la formulation MSF (K4).
- PB-121 : de la section C vers la section A (C4).
- PB-127 : l'en-tête (A14).
- Les entrées nouvelles : A1, A4, A5, A6, A7, A8, A9, et K13 si on l'inscrit.

**A18 — Les étapes : ce qui manque.**
- **ST.0 hors G13, dans les deux modes, avant G13.** Elle regroupe :
  - PB-121, sous la forme (i) ou (ii) de K3 ;
  - PB-17, avec la borne de K2 ;
  - A1 ;
  - A2, comme correction de transcription ;
  - le R9 (a) du Xebec : 19 `fatal()`, dont plusieurs à la portée de l'invité (une lecture de 320h au repos
    tombe en `default:`, `Mfm/mfm_xebec.cs:182`). Par la logique même de D3 pour PB-17, R9 n'attend pas le mode
    matériel.
  - Ses tests, en C# seul : la survie des disquettes (les six cas de K2) et le test de PB-121 (K3).
  - Elle change l'émulateur : série entière.
- **Les registres et marqueurs seuls** : une étape à part, en sous-ensemble ciblé.
- **ST.4** : retirer « PLAY AUDIO MSF accepte une piste de données » (K4) ; y mettre A4 et A5.
- **ST.5** : la réutilisation du flux (A12).
- **ST.6** : PB-121 en sort ; ajouter la question de UNIT ATTENTION à la mise sous tension (A10).
- **ST.7** : distinguer SCRST de HRST (K9) ; ajouter A8 si on l'inscrit.
- **ST.2** : le voisin d'`ide_irq_update`, dans sa forme de K13.

**A19 — Les questions qui manquent.**
- **QA** : corriger PB-121 tout de suite, dans les deux modes, hors G13 (K3) ? C'est la seule question urgente
  du domaine. Elle ne doit pas rester noyée dans Q2.
- **QB** : comment ranger les plantages par une image de l'utilisateur, PB-17 et A1 (A16) ?
- **QC** : un filet d'hôte qui vide les images sur exception (A3) ?
- **QD** : inscrire les défauts nouveaux, et sous quels numéros (PB-168 et suivants) ?
- **Q5** : y ajouter PLAY AUDIO (12) (K17).
- **Q6** : écrire la contradiction d'ATA-3 (K6).
- **Q8** : y ajouter le drapeau de 23h (A8).
- **Q11** : SCRST est un reset « soft » ; le sort d'un CCB en vol reste inconnu (K9).

---

## 4. Classement par PB, après contre-lecture

Légende : *vérifiée* = relue à sa source ce jour. « Hors G13 » = corrigé dans les deux modes, hors du mode
matériel.

| PB | D3 | Après contre-lecture | Fondement | Remarque |
|---|---|---|---|---|
| 14 | a | a | AT TR *Data Rates* ; BIOS « 01: 300 KBS » (vérifiés) | — |
| 22 | a | a | FDA, DCB Format Track (vérifié) | — |
| 23 | a | a | FDA, Status Register bit 5 (vérifié) | sans effet pour le BIOS IBM |
| 25 | a | a | FDA erreur 21h (vérifié) ; IDNF de l'AT déduit | marqueurs de l'AT ; registre (C6) |
| 28 | c | c | aucune documentation | — |
| 71 | a | a | preuve interne `ide.cs:205-222` ; ATA-1 § 6.3.10 (vérifié) | PC87415 faible (K12) |
| 72 | c | c ; b au mieux, par décision | ATA-1 § 7.2.13 ; ATA-3 contradictoire (vérifiés) | K6 |
| 74 | a | a, sous ATA-1 | ATA-1 § 9.19 et § 7.2.11 (vérifiés) | ATA-3 : sorties non requises (K5) |
| 106 | a | a | SFF-8020i Fig. 11 (non revérifiée) | — |
| 107 | b | b | déduit | — |
| 108 | a | a | format CUE (non revérifié) | — |
| 109 | a | a | déduit | marqueur de site absent |
| 117 | a | a | SFF-8020i, note de capacité (vérifiée) | — |
| 118 | b | b | SFF-8020i Table 37 ; MMC-2 § 9.1.2 (vérifiés) | IMMED, classes (K15) |
| 119 | a | a | SFF-8020i § 10.8.4 (vérifié) | troncature 05/1Ah (K16) |
| 120 | a | a | SFF-8020i § 10.8.19, Table 131 (vérifiés) | ordre A0-A2 ; type documenté (K14) |
| 122 | a | a | Table 131 : ADR 1, contrôle 4/6 (vérifiée) | contrôle de la dernière piste : déduit |
| 123 | a | a (LBA, sous-canal) | SFF-8020i § 10.8.8 et § 10.8.18 (vérifié : § 10.8.8) | volet MSF faux (K4) ; A4, A5 ; A5h (K17) |
| 126 | a ; START STOP c | a ; START STOP c | INF-8070i Tables 41 et 59 (vérifiées) | recollage sur le même flux (A12) |
| 129 | a | a | SCSI-2 § 9.1.2, § 9.2.7 (non revérifiés) ; § 7.9 (vérifié) | UA à la mise sous tension (A10) |
| 130 | a | a | SCSI-2 § 8.3.3 (non revérifié) | — |
| 131 | a ; FORMAT c | a ; FORMAT c | SCSI-2 Table 45, § 7.5.3, § 7.6 (vérifiés) | 5Bh, pas 5Ch ; pas de « 60h » (K8) |
| 132 | a | a (cible, HRST) ; c (CCB en vol sous SCRST) | TR, port de contrôle ; SCSI-2 § 6.2.2 (vérifiés) | K9 |
| 133 | a | a | TR CCB octet 1 ; SCSI-2 § 7.2.2 (vérifiés) | — |
| 136 | b ; c hors bornes | b ; c hors bornes | TR 22h (vérifié, ambigu) | K18 |
| 137 | a ; 03h b | a ; 03h b | TR CCB octet 3 (vérifié) | — |
| 138 | b | b | TR : 03h réservée au BIOS (vérifié) | — |
| 139 | a | a | TR MBO, MBI 02h et 03h (vérifiés) | — |
| 140 | a | a | TR 0Dh (vérifié) | 25h ≠ interrupteurs (K10) |
| 141 | a ; mailbox du BIOS b | a ; mailbox du BIOS b | TR 01h, 02h, tourniquet (vérifiés) | — |
| 142 | a ; réentrance c | a ; réentrance c | TR *Hard Reset Operations* (vérifié) | — |
| 143 | c | c | TR 14h et 1Ah (vérifiés, non concluants) | — |
| 144 | b (mixte) | b (mixte) | TR CCB, réglages d'usine (vérifiés) | drapeau de 23h (A8) |
| 16 | d | d ; nettoyage d'hôte, préalable au refus de K2 | code | registre faux côté C (K11) |
| 17 | a (R9 d'abord) | **hors G13 : R9**, deux modes | code | ≥ 99 840 o ou refus (K1, K2) |
| 18 | d | d | code | marqueur « sans objet » |
| 15 | d | d | code | — |
| 19 | b | b | µPD765 (vérifié) | correction en deux sites (K7) |
| 20 | d | d | code | — |
| 26 | d | d | code | site `:211` sans marqueur (A13) |
| 27 | b | b, avec le R9 (a) du Xebec | code ; FDA 20h (vérifié) | R9 (a) en ST.0 (A18) |
| 29 | d | d | code | — |
| 30 | d | d | code | registre à corriger (C7) |
| 34 | d | d (hôte, Q2) | ROM AMI 286 | — |
| 112 | d | d | code | — |
| 121 | a | **hors G13 : protection des fichiers**, deux modes, sur décision | code ; `VERIFICATION.md:6692-6698` ; `PLAN-G11.md:335-337` | section C → A (C4) |
| 127 | d | d | code | absent de l'en-tête (A14) |
| 134 | d | d | code | — |
| A1 division par zéro | — | **hors G13 : R9** | code | entrée à créer, section B |
| A2 fichier vide | — | correction de transcription | source .NET | pas un PB |
| A3 écritures perdues sur plantage | — | hôte, hors G13 | doc C# | à mesurer |
| A4 MSF compacté | — | a | SFF-8020i § 10.8.9 (vérifié) | à inscrire |
| A5 MSF + 150 | — | a | code | à inscrire |
| K13 `ide_irq_update` | — | a | ATA-1 § 6.3.10 (vérifié) | forme restreinte |
| A6 sense du Xebec | — | a, faible valeur | FDA *Sense Bytes* (vérifié) | — |
| A7 taille de secteur du BPB | — | d (format d'image) | code | Q2 |
| A8 drapeau de 23h | — | b | TR Return EEPROM (vérifié) | effet inconnu |
| A9 INQUIRY « 60h » mort | — | d | code ; SCSI-2 § 8.2.5.1 | — |

Comptes après contre-lecture, sur les 48 entrées :
- (a) 25 : la section A, sauf 28, 72, 107, 118, 136, 138, 143 et 144 ;
- (b) 7 : 107, 118, 136, 138, 144, 19, 27 ;
- (c) 3 : 28, 72, 143 ;
- (d) 11 : 16, 18, 15, 20, 26, 29, 30, 34, 112, 127, 134 ;
- hors G13 : 2, PB-17 et PB-121.

Les entrées nouvelles s'y ajoutent selon QD.
