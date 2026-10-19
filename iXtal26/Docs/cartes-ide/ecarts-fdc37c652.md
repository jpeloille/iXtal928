# La carte fille à FDC37C652 contre l'émulateur : la note des écarts

> Écrite le 20 octobre 2026, sur le feu vert de Julien du même jour : « télécharge les fiches et fais la note
> d'écarts » (`PLAN-CARTE-IDE.md`, l'étape suivante). Elle ne corrige rien et ne numérote aucun PB : un écart qui
> serait un défaut de PCem y figure comme candidat, pour le plan de conception.
>
> **La source primaire** : SMC, *FDC37C651/FDC37C652 Super I/O Floppy Disk Controllers*, Rev. 9/9/92, 90 pages
> (`sources/SMC_37C651.pdf`, SM1 du manifeste ; « fiche, p. n » ci-dessous). Le texte vient de `pdftotext -layout`
> sur la couche de texte du scan ; les tableaux mal lus ont été relus à la page. Contre-lecture (Sonnet) : aucun
> bloquant, cinq reprises et sept précisions, toutes prises.
>
> **L'émulateur lu** : le mode matériel, sur `d56d161` plus le plan. Le 386 (`ixtal26-386.cfg`, `ami386dx`) monte
> `mfm_at`, le 486 (`ixtal26-486.cfg`, `ami486`) monte `ide`. Les deux prennent les disquettes de `common_init`
> (`Models/model.cs:435`, `fdc_add`), sans aucun des réglages de variante du contrôleur (`enh_mode` 0, `is_nsc` 0,
> `ps1` 0, `enable_3f1` 1), et les deux ports série de `Models/model.cs:441-442`.

## Les sources téléchargées

Toutes sont dans `sources/`, hors de git, scellées par `sources/MANIFEST.tsv` et `tools/fetch-sources.sh` (comme
celles de `Docs/ibm5150/`). Elles sont ici plutôt que dans `Docs/cartes-ide/sources/`, comme le prévoyait le plan,
parce que le dépôt a déjà son dossier de sources et son outil.

| Id | Fichier | Pages | Texte | Rôle |
|---|---|---|---|---|
| SM1 | `SMC_37C651.pdf` — FDC37C651/652, Rev. 9/9/92 | 90 | oui | **La référence de la carte** |
| SM2 | `AN4.14_FDC37C6XX_DMA_IDE_Support.pdf` — SMSC, 1997 | 2 | oui | L'IDE en DMA, par de la logique externe |
| SM3 | `SMC_37C655.pdf` — FDC37C665IR/666IR | 166 | oui | La génération suivante (82077, 16550), à comparer |
| G1 | `GM82C765B.pdf` — Goldstar | 36 | oui | À comparer ; le serveur refuse curl sans User-Agent de navigateur, déposé à la main puis scellé |
| W1 | `W83757.pdf` — Winbond | 29 | **non** (scan sans texte) | La Super I/O de la DTC 2278D, à comparer |
| W2 | `W83759_199505.pdf` — Winbond, Rev. A1 | 41 | oui | IDE VLB, à comparer |
| WD1 | `25_WD37C65C.pdf` — Western Digital, nov. 1991 | 58 | oui | Le 765 intégré concurrent, à comparer |

Les fiches TH99 ne sont pas téléchargées : ce sont des pages HTML de source secondaire, et aucune ne décrit une carte
à 652 avec IDE. Leurs URL restent dans `PLAN-CARTE-IDE.md`.

## La carte en une page (ce qu'émuler veut dire)

- **Quatre blocs derrière un seul circuit** : les disquettes (3F2h, 3F4h, 3F5h, 3F7h), deux UART, un port parallèle,
  le décodage de l'IDE (1F0h-1F7h, 3F6h, 3F7h) ; plus GAMECS en 201h, le 652 seul (fiche, p. 1, p. 19, tableau 1 ; GAMECS p. 1 et p. 15).
- **Le 652 se règle par cavaliers** : ils se lisent sur le front descendant de RESET (fiche, p. 6, broche RST ;
  p. 72 : « The different configurations of the FDC37C652 can only be selected through jumper options » ; p. 78-79).
  Les registres CR0-CR3 en 3F0h/3F1h sont décrits pour le 651 (p. 72-78). Que le 652 ne décode pas du tout 3F0h et
  3F1h est une **déduction** : le tableau 1 (p. 19), commun aux deux, liste « 3F0,3F1 Configuration » sans exception.
- **Ses cavaliers** (p. 78-79) : PCF1-PCF0, le LPT (aucun, 3BCh, 378h, 278h) ; S1CF1-S1CF0, le premier UART (aucun,
  338h, 2F8h, 3F8h) ; S2CF1-S2CF0, le second (aucun, 238h, 3F8h, 2F8h) ; IDECF, l'IDE oui ou non ; FDCCF, les
  disquettes oui ou non. Les IRQ des UART sortent sur PSPIRQ (p. 5) et SSPIRQ (p. 6), que des cavaliers de la carte mènent à IRQ 3
  ou 4 : la carte, pas la puce.
- **Pas de cavalier AT/XT pour l'IDE sur le 652** : le choix AT/XT n'est que CR0 bit 1, sur le 651 (p. 75). La fiche
  ne dit pas où le 652 le prend ; ce qui suit suppose l'AT, le seul qui ait un sens sur un 386 ou un 486.

## Les écarts, bloc par bloc

« Conforme » veut dire : l'émulateur fait ce que dit la fiche. Chaque écart dit ce qu'en ferait la carte émulée ;
la version sans la carte, elle, ne bouge pas.

### 1. Les disquettes

| # | La fiche (652) | L'émulateur | Verdict |
|---|---|---|---|
| D1 | Cinq registres seulement : DOR 3F2h (é), MSR 3F4h (l), données 3F5h, DSR 3F7h (é), DIR 3F7h (l) ; « Registers other than the ones shown are not supported » (p. 19-20, tableau 2) | `fdc_add` prend 3F0h-3F5h et 3F7h (`Floppy/fdc.cs:1452-1453`) | Écart pour 3F3h (absent du tableau 2). Pour 3F0h et 3F1h, déduit (voir plus haut) : **à trancher**. La lecture de 3F2h (DOR « write only ») rend FFh dans le code (pas de cas en lecture) : conforme à un registre en écriture seule |
| D2 | 3F1h : absent du tableau 2 (p. 20) ; le décodage par le 652, déduit | Lu : un registre d'état B de PS/2 (70h, bits des moteurs et du lecteur), `enable_3f1` à 1 pour tous (`fdc.cs:804-818`, `:1438`) | Écart probable, à trancher avec D1. Sur la carte : le bus flottant |
| D3 | 3F3h : non décodé | Lu : 20h (`fdc.cs:819-835`, `enh_mode` 0) ; écrit : ignoré hors `enh_mode` (`:468-475`) | Écart à la lecture. Sur la carte : le bus flottant |
| D4 | 3F4h en lecture seule (le MSR) | Une écriture de bit 7 remet le contrôleur à zéro (`fdc.cs:476-482`), le DSR du 82077 | Écart. Sur la carte : l'écriture ignorée |
| D5 | Le jeu de commandes du 765B, plus SOFTWARE RESET (36h) et RETURN VERSION (10h, réponse 90h, « B-type product ») ; tout autre code : ST0 = 80h, sans interruption (p. 41, p. 51) | Accepte en plus DUMPREG (0Eh), PERPENDICULAR (12h), CONFIGURE (13h), LOCK/UNLOCK (94h/14h) (`fdc.cs:599-629`) ; refuse 36h (`default`, invalide) ; VERSION rend 90h (`:1138-1143`) | Écart, deux sens : cinq codes du 82077 de trop (quatre commandes, LOCK et UNLOCK comptés pour une), SOFTWARE RESET qui manque. VERSION conforme |
| D6 | « true CMOS 765B core » (p. 1, p. 19) : la fiche ne parle d'aucune FIFO, son absence est déduite du cœur 765B | FIFO éteinte au départ (`fdc.cs:1446`) mais CONFIGURE l'allume (`:1152`) | Écart, qui découle de D5 : sans CONFIGURE, pas de FIFO |
| D7 | Le débit (tableau 6, p. 23) : 00 = 500 ; 01 = 300 ou 250 selon la broche DRVTYP ; 10 = 250 ; 11 = 1 000 kbit/s, avec un quartz de 48 MHz et CR3 bit 2 seulement | 0 = 500, 1 = 300 (ou 500 et 2 000 selon le type du lecteur), 2 = 250, 3 = 1 000 toujours (`fdc.cs:330-364`) | 00, 10 conformes. 01 : conforme si DRVTYP est tiré à 1 — la fiche ne dit pas comment une carte le câble, **à trancher**. 11 : écart probable, une carte à quartz de 24 MHz n'a pas le 1 Mbit/s ; la fiche ne dit pas ce qu'elle fait alors, **à trancher** |
| D8 | Le DSR ne garde que les deux bits bas (p. 23) | `fdc.rate = val & 3` (`fdc.cs:782-788`) | Conforme |
| D9 | DIR (3F7h, lecture) : DB7 = DSKCHG inversé, **DB6-DB0 en haute impédance** (p. 23) ; IDED7 ne tamponne que le bit 7 (p. 14) : les bits 6-0 viennent directement du bus du disque IDE | Bit 7 : le changement de disquette du lecteur choisi, si son moteur tourne ; **bit 0 forcé à 1**, bits 1-6 à 0 (`fdc.cs:879-888`) | Bit 7 conforme : le tableau 4 (p. 20) ne choisit un lecteur que si son moteur est demandé. Bits 6-0 : écart, voir I3 |
| D10 | DOR bit 3 (DMAEN) : « When low, DMA and IRQ are tri-stated » (p. 21) ; FINTR « enabled/disabled via bit 3 » (p. 6) | `fdc_int` pose l'IRQ 6 sans regarder le DOR (`fdc.cs:274-278`) ; le DMA ne le regarde pas non plus (`:1241`, `:1390`) | Écart, et **candidat défaut de PCem** pour tout AT (l'adaptateur d'IBM a le même bit) : hors de cette étape, au registre plus tard |
| D11 | Quatre lecteurs directement (DRV0-3, MTR0-3, p. 7) | Deux : `fdc.drive & 1` partout | Sans objet : la demande est de deux lecteurs |
| D12 | SOFTWARE RESET (36h) : la remise à zéro sans toucher au DOR ni au DSR, levée au premier accès, puis une interruption de changement d'état (p. 51) | Absente | Compris dans D5 |

### 2. L'IDE

| # | La fiche (652) | L'émulateur | Verdict |
|---|---|---|---|
| I1 | Aucun registre propre : « These registers are not implemented in the FDC37C651 and FDC37C652 » (p. 66) ; le fichier de registres ATA vient du disque | `ide_pri_enable` : 1F0h-1F7h et 3F6h (`Ide/ide.cs:1302-1306`) | Conforme : rien à ajouter pour la carte |
| I2 | Un seul jeu de sélections : HDCS0 pour 1F0h-1F7h, HDCS1 pour 3F5h-3F7h (p. 14) ; aucune autre plage | `ide_sec_enable` en 170h-177h et 376h (`Ide/ide.cs:1314-1318`), sous la clé `secondary` (`ide.cs:1329-1334`, `ide_canal_actif`), présente par défaut (`ide.Materiel.cs:147`) | Sous la carte, le canal secondaire n'existe pas (déduit : la fiche ne décode aucune autre plage). Deux disques en maître et esclave : le compte de la demande |
| I3 | 3F7h en lecture : DB7 de la disquette, DB6-DB0 du disque (p. 14, p. 23). La fiche, « for reference only » (p. 66), donne au disque (p. 69) : bit 6 WGATE, bits 5-2 les têtes, bits 1-0 les sélections de lecteur, actives basses | L'IDE ne prend pas 3F7h ; le contrôleur de disquettes y rend `xx000001b` | Écart. Ce que rend le disque dépend du disque, pas de la carte : **à trancher** dans le plan (la fiche, ou un disque qui ne mène pas ces bits) |
| I4 | 16 bits pour 1F0h seulement, IOCS16 venu du disque (p. 14, p. 19) ; l'IDE en DMA demande de la logique externe (SM2) | 1F0h en 16 bits et en 32 bits (`rpl`/`wpl`) ; pas de DMA IDE | Conforme pour le DMA. Le 32 bits : sur l'ISA, le contrôleur du bus le coupe en deux cycles de 16, même résultat ; seul le temps diffère (voir la performance, au plan) |
| I5 | 3F6h : écriture SRST (bit 2), nIEN (bit 1) ; lecture : l'état alterné (p. 69) | `fdisk` : bit 2 la remise à zéro, bit 1 le masque d'IRQ (`Ide/ide.cs:697-711`, `:208`, `:239`) | Conforme **en mode matériel** : la chute de la ligne d'IRQ sous nIEN y est corrigée (PB-216, `ide.cs:212`, `:226`, `:398` ; PB-71, `:236`, `ide_irq_update_materiel`, `ide.Materiel.cs:57-69`) |
| I6 | Le mode XT (320h-323h, HDACK, p. 14, p. 19) | — | Sans objet sur un 386 ou un 486 |

### 3. Les ports série, parallèle et jeu

| # | La fiche (652) | L'émulateur | Verdict |
|---|---|---|---|
| S1 | Deux UART « NS 16450 Compatible », sans FIFO (p. 1) | `serial1_init(0x3f8, 4, 1)` et `serial2_init(0x2f8, 3, 1)` : `has_fifo` à 1, des 16550 (`Models/model.cs:441-442`) | Écart. Sous la carte : deux 16450 |
| S2 | Les adresses par cavaliers ; COM3 en 338h, COM4 en 238h, pas en 3E8h/2E8h (p. 79) | COM1 en 3F8h, COM2 en 2F8h ; retrait par `serial1`/`serial2` (G13.13b) | Conforme pour COM1 et COM2. 338h et 238h : nouveaux, si le plan les retient |
| S3 | Le port parallèle bidirectionnel : le 651 seulement (p. 1) ; adresses 3BCh, 378h ou 278h (p. 78) | La lecture de la donnée rend le dernier octet écrit (`Lpt/lpt.cs:175-186`) : un port unidirectionnel ; mais `common_init` monte deux LPT, 378h et 278h | Conforme pour le sens. Sous la carte : un seul port parallèle, à l'adresse de PCF1-PCF0 |
| S4 | GAMECS en 201h, le 652 seul (p. 1, p. 15) : la puce décode, la carte porte le port | `Joystick/gameport.cs` | Rien dans la fiche qui contredise ; le cavalier de la manette est celui de la carte, pas de la puce |

## Ce que le plan de conception aura à trancher

1. **D7** : comment la carte câble DRVTYP (250 ou 300 kbit/s sous le code 01), et ce que fait le code 11 avec un
   quartz de 24 MHz. Aucune des deux réponses n'est dans la fiche ; un manuel de carte ou une mesure est à chercher.
2. **I3** : ce que rendent les bits 6-0 de 3F7h, la fiche de référence ou un disque qui ne les mène pas.
3. **D10** : DMAEN, un défaut de PCem qui vaut pour tous les AT et pas seulement pour la carte ; il ira au registre
   par sa propre étape (règle du 18/10 : les PB neufs d'une étude restent hors de l'étape).
4. **Les deux modes** : sous la carte, D1-D6, D12, I2, I3, S1 et S3 changent. Sans la carte, rien ne bouge, et
   l'articulation avec `serial1/2`, `parallel1/2` (G13.13b) et `primary/secondary` (G13.12b) reste à écrire.
5. **3F0h et 3F1h** (D1, D2) : le 652 les laisse-t-il au bus ? La fiche ne le dit pas en toutes lettres ; un manuel de
   carte ou une carte réelle le dirait.
6. **Les incohérences de la fiche** : HDCS1 couvre 3F5h-3F7h selon la broche (p. 14), 3F6h-3F7h selon la
   p. 69 ; le choix AT/XT de l'IDE n'a pas de cavalier sur le 652 (p. 75, p. 78-79).
