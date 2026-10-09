# Pourquoi on corrige

> Ouvert le 7 octobre 2026, avec G13.0. Pour chaque correction de l'émulateur, en quoi elle corrige une vraie erreur.
> `PCEM_BUGS.md` garde le constat et la ligne de C, `VERIFICATION.md` la preuve ; ce fichier garde la raison, en clair.

La référence est le vrai PC. Chaque entrée répond aux mêmes questions :
- **Le vrai PC** : ce que fait la machine réelle, ou ce qu'elle ne ferait jamais. Pour le mode matériel de G13, c'est
  la source documentée que l'entrée PB cite.
- **L'émulateur** : ce que faisait PCem, ou iXtal avant la correction.
- **La correction** : ce qu'elle rétablit, et ce qu'elle laisse tel quel.

Une section par étape qui corrige quelque chose, dans l'ordre, écrite avec son commit. Ce qu'une étape touche sans le
changer s'y dit aussi, comme PB-88 ci-dessous.

## G13.0 — l'invité et les images ne tuent plus l'hôte

*Le 7 octobre 2026, commit 9d6c1c9 ; `PLAN-G13.md` § G13.0, `VERIFICATION.md` § G13.0.*

Aucune de ces corrections ne change ce que fait la machine émulée quand tout se passe bien. Elles retirent des
comportements que seul l'émulateur avait : planter, ou perdre des données.

### PB-09 — la CGA (R9)

- **Le vrai PC.** Un vrai PC n'en souffre pas. La carte CGA n'a pas de tampon de ligne : elle lit chaque caractère
  directement dans sa mémoire vidéo. Un programme peut programmer le registre R1 du 6845 jusqu'à 255, et la carte
  affiche simplement plus de caractères par ligne, souvent illisibles, mais l'ordinateur continue de tourner.
- **L'émulateur.** Le tampon de 256 octets est une invention de PCem, qui écrit hors de ce tableau et corrompt sa
  propre mémoire. Chez nous, l'émulateur s'arrêtait net.
- **La correction.** Le tampon de 512 octets reproduit exactement les octets que la carte lirait.

### PB-17 — les pistes de plus de 20 Ko (R9)

- **Le vrai PC.** Ces disquettes existent. Le format XDF d'IBM en densité étendue, sur des disquettes de 2,88 Mo, met
  environ 23 à 24 Ko par face de piste. Un vrai lecteur de 2,88 Mo les lit, et un format exotique à petits secteurs ne
  fait au pire qu'une erreur de lecture. Aucune disquette, même étrange, ne fait planter un vrai PC quand on l'insère.
- **L'émulateur.** PCem écrit au-delà de son tampon de piste de 20 Ko, dans sa propre mémoire. Chez nous, la seule
  insertion de l'image arrêtait l'émulateur.
- **La correction.** Le tampon fait 195 × 512 octets par face, la plus longue piste qu'une BPB admise puisse
  annoncer : ces images se lisent désormais entières.

### PB-168 — une BPB à 0 secteur par piste (R9)

- **Le vrai PC.** La BPB n'est qu'un champ du secteur de boot, écrit par le formateur. Elle peut être nulle ou
  absurde : disque CP/M, disque de jeu non-DOS, image abîmée. Sur un vrai PC, le BIOS ne s'en sert pas, et DOS répond
  au pire « General failure ». Introduire la disquette ne plante jamais la machine.
- **L'émulateur.** PCem, lui, meurt d'une division par zéro, et iXtal levait au même endroit.
- **La correction.** Une telle BPB est traitée comme toute BPB que PCem juge absurde : la géométrie se déduit de la
  taille du fichier.

### Le fichier vide (une faute de transcription)

- **Le vrai PC.** C'est l'équivalent d'une disquette vierge : un vrai lecteur répond par une erreur, sans planter.
- **L'émulateur.** Ce n'est pas un défaut de PCem, qui survit, mais une erreur de notre transcription : sur un fichier
  de zéro octet, le C laisse échouer `fseek`, le C# levait.
- **La correction.** iXtal fait comme PCem : le fichier se monte, pris pour une disquette de 160 Ko simple face, la
  géométrie déduite de sa taille.

### PB-121 — le reset avec un disque SCSI (hôte)

- **Le vrai PC.** Sur un vrai disque, une écriture terminée reste écrite, et la dernière écriture l'emporte. Un reset
  du PC ne défait rien de ce qui est déjà sur le disque.
- **L'émulateur.** Après un reset depuis le menu, la machine relisait des données périmées, comme si l'écriture
  n'avait jamais eu lieu. À la sortie, un vieux tampon écrasait ce qui avait été écrit depuis. L'image SCSI de
  l'utilisateur perdait des fichiers, ce qu'aucun vrai disque ne fait.
- **La correction.** Le flux encore ouvert sur l'image est vidé avant qu'elle soit rouverte : la machine neuve relit
  l'image à jour, et le vieux tampon, vide, n'écrase plus rien.

### PB-33 — `savenvr` sans répertoire `nvr/` (hôte)

- **Le vrai PC.** Rien de matériel là-dedans : sur un vrai PC, le CMOS vit dans une mémoire sauvegardée par pile. Et
  éteindre après le retour à l'invite DOS ne perd rien de ce qui est déjà écrit.
- **L'émulateur.** La conséquence était contraire à la vraie vie : un simple dossier manquant sur le disque de l'hôte
  faisait perdre les dernières écritures de la séance, parce que `savenvr` levait avant `closepc`, le seul endroit
  qui vide les images.
- **La correction.** `savenvr` dit que le CMOS n'est pas écrit et rend la main ; les images sont vidées comme à toute
  sortie normale. Le CMOS de la séance reste perdu faute de dossier : l'équivalent d'une pile morte, rien de plus.

### Le filet des images (hôte)

- **Le vrai PC.** Sur une vraie machine, dès qu'une écriture est terminée pour le programme, elle est sur le support,
  même si la machine plante ensuite.
- **L'émulateur.** Ces écritures attendaient dans un tampon de l'hôte jusqu'à la fermeture normale. Un plantage les
  perdait, et l'image pouvait rester dans un état qu'un vrai disque n'aurait pas eu après ces écritures, par exemple
  une FAT incohérente.
- **La correction.** Le filet rétablit la règle du matériel : sur une exception non rattrapée ou un signal de fin, il
  vide les tampons des images (disques durs, disquettes, ZIP) sans rien exécuter de l'émulateur. Un SIGKILL ou une
  panne de l'hôte restent hors de sa portée. La sortie en code 70 n'est que de l'hygiène : elle évite des rapports de
  plantage de plusieurs dizaines de Mo dans `/var/crash`.

### PB-88 — la M24 (rien ne change)

Rien n'a changé dans son comportement : elle était déjà corrigée depuis G1. G13.0 n'a ajouté que le test qui le
prouve, `r9-m24`.

## G13.2 — le mode matériel, et son pilote

*Le 7 octobre 2026, écrite avec le commit de G13.2 ; `PLAN-G13.md` § G13.2, `VERIFICATION.md` § G13.2.*

À partir d'ici, les corrections changent ce que fait la machine émulée, et elles ne valent qu'en mode matériel : le
mode PCem, le défaut, reproduit toujours les défauts de PCem, parce que PCem reste l'oracle des portes. Le mode
matériel se demande au lancement (`--hardware-mode`, la clé `hardware_mode`, la ligne « Mode » de l'écran de
construction).

### PB-01 — l'AF d'ADC et de SBB du 8088 (mode matériel)

- **Le vrai PC.** AF est la retenue du bit 3 vers le bit 4, retenue entrante comprise : ADC additionne trois termes,
  SBB en soustrait trois, et le drapeau suit l'opération entière. C'est la définition d'Intel, et c'est ce que le
  corpus SingleStepTests a mesuré sur un vrai 8088. Avec CF = 1, AL = 09h, `ADC AL,06h` donne 10h et pose AF ; le
  `DAA` qui suit corrige alors en 16h, la bonne somme décimale de 9 et 6 plus la retenue.
- **L'émulateur.** PCem calcule AF comme pour ADD, sans la retenue entrante : la retenue du bit 3 lui échappe quand
  elle ne vient que d'elle. AF reste à 0, et le `DAA` laisse 10h. Une addition BCD sur plusieurs octets, faite par
  ADC puis DAA, rend un chiffre faux ; environ 3 à 4 % des ADC et SBB du corpus diffèrent du 8088, toujours sur ce
  seul bit.
- **La correction.** En mode matériel, AF devient le bit 4 de `a ^ b ^ résultat`, la formule qui vaut pour ADC comme
  pour SBB. Rien d'autre ne bouge : le contrôle de fuite le prouve contre PCem, instruction par instruction.

## G13.3 — le 8088 et le 8086 (mode matériel)

*Le 8 octobre 2026, écrite avec le commit de G13.3 ; `PLAN-G13.md` § G13.3, `VERIFICATION.md` § G13.3.*

Dix-sept corrections, toutes en mode matériel ; le mode PCem reste celui de l'oracle. Trois groupes ne valent
qu'ensemble et se demandent ensemble : PB-03 et PB-257 ; PB-07 et PB-179 ; PB-45, PB-169 et PB-258. Les règles mesurées viennent du corpus
SingleStepTests, enregistré sur un AMD D8088 et un Intel 8086.

### PB-02 — RCL et RCR mot par CL

- **Le vrai PC.** Chaque pas de rotation sort un bit dans CF ; après la dernière, CF tient le dernier bit sorti.
- **L'émulateur.** Après la boucle, PCem remet dans CF le bit qui était ENTRÉ au dernier pas : faux environ une fois
  sur deux, et l'OF de RCL, calculé ensuite, aussi. Les formes octet, elles, ont ces lignes commentées.
- **La correction.** Le bloc fautif est sauté ; le CF de la boucle reste.

### PB-03 et PB-257 — le temps volé par le rafraîchissement

- **Le vrai PC.** Sur le 5150 et l'XT, la DMA rafraîchit la mémoire en volant des cycles de bus au processeur. Ce
  temps passe pour tout le monde, le 8253 compris : l'horloge de l'invité ne retarde pas. Un canal masqué, lui, ne
  demande rien au 8237 et ne coûte rien.
- **L'émulateur.** PCem débite bien ces cycles, mais après avoir compté ceux de l'instruction : ils n'arrivent jamais
  au compteur de temps, et l'heure de l'invité retarde de 54 ppm sur son propre processeur (1,9 % pendant le test
  mémoire du POST). Il facture aussi le cycle d'un transfert refusé.
- **La correction.** Les cycles du rafraîchissement entrent au compteur de temps ; un transfert refusé ne coûte plus
  rien. Sur 300 secondes de 5150, plus aucun cycle n'échappe au compteur : reste l'écart de 5,87 ppm de l'hôte (le
  budget d'une tranche arrondi par PCem), que l'invité ne voit pas.

### PB-07 et PB-179 — un mot à cheval

- **Le vrai PC.** Un mot à l'offset FFFFh prend son second octet à l'offset 0 du même segment ; un mot en FFFFFh prend
  le sien en 00000h, le 8088 n'ayant que vingt lignes d'adresse.
- **L'émulateur.** PCem lit l'octet suivant en mémoire linéaire, 64 Ko plus loin, et, quand la page est en cache, au
  sommet de la RAM, au-delà de son tableau (l'émulateur lit là une marge à zéro).
- **La correction.** Ces mots se lisent et s'écrivent octet par octet, chacun à sa vraie adresse.

### PB-45, PB-169 et PB-258 — DIV et IDIV

- **Le vrai PC.** IDIV octet divise AX signé. Un quotient qui ne tient pas dans son registre lève l'interruption 0,
  comme un diviseur nul ; sur le 8086 et le 8088, un quotient de 80h (8000h) aussi.
- **L'émulateur.** PCem lit AX sans son signe, et ne lève l'interruption que pour un diviseur nul : un quotient trop
  grand est tronqué en silence.
- **Et la pile (PB-258, trouvé en corrigeant PB-169).** Une interruption empile sur la pile, SS:SP. Sous un préfixe de
  segment (`DS: DIV BL`), PCem empile dans le segment du préfixe : il écrase une donnée, et le retour de
  l'interruption dépile n'importe quoi.
- **La correction.** Le signe, l'interruption, la pile dans SS. Les drapeaux que le processeur empile à ce moment
  restent ceux de PCem (PB-180) : le 8088 empile ceux que son microcode a laissés, une règle qu'on ne connaît pas.

### PB-87 — l'instruction à cheval sur FFFFh

- **Le vrai PC.** Au-delà de l'offset FFFFh, le processeur lit la suite de l'instruction à l'offset 0 du segment.
- **L'émulateur.** PCem la lit 64 Ko plus loin, quand sa file de préfetch est vide.
- **La correction.** La lecture se fait à l'offset 0. Une seule exception, pour l'hôte : une instruction de plus de
  64 Kio, une chaîne de préfixes qui remplirait son segment, ne finirait jamais sur le silicium ; l'émulateur, lui,
  doit rendre la main (R9), et reprend là la lecture de PCem.

### PB-170 et PB-171 — DAA et DAS

- **Le vrai PC.** Mesuré sur les deux processeurs : le second ajustement compare l'AL d'ORIGINE à 99h, ou à 9Fh si AF
  valait 1 ; DAS ne garde pas l'emprunt de son premier pas dans CF.
- **L'émulateur.** PCem suit le pseudo-code d'Intel pour DAA, juste au regard du manuel mais pas du silicium (6
  entrées sur 1 024), et reprend l'emprunt d'en bas dans DAS (24 entrées).
- **La correction.** La règle mesurée, qui rend tous les cas du corpus, au 8088 comme au 8086.

### PB-172 — REP LODS

- **Le vrai PC.** LODS charge AL (AX) à chaque répétition.
- **L'émulateur.** PCem lit l'octet et le garde pour lui : AL ne change pas.
- **La correction.** AL et AX reçoivent la valeur lue. Vérifiée contre le silicium depuis que la sonde SST joue la
  chaîne jusqu'à sa dernière répétition : les 2 000 REP LODSB et les 2 000 REP LODSW du corpus, au 8088 et au 8086.

### PB-173 — SETMO et SETMOC

- **Le vrai PC.** Les formes /6 des décalages, non documentées, mettent l'opérande à FFh (FFFFh) ; mesuré, avec
  toujours les mêmes drapeaux : SF et PF.
- **L'émulateur.** PCem les exécute comme SHL.
- **La correction.** L'opérande à FFh, les drapeaux mesurés ; SETMOC ne fait rien quand CL vaut 0.

### PB-174 et PB-176 — les décalages par CL

- **Le vrai PC.** Le 8088 pose OF après un décalage par CL : celui du dernier pas (Intel le dit indéfini, le silicium
  le fixe). SAR rend dans CF une copie du signe dès que le compte atteint la largeur.
- **L'émulateur.** PCem laisse OF tel qu'avant l'instruction, et SAR rend CF nul au-delà de 8 (16).
- **La correction.** L'OF du dernier pas, le CF du signe.

### PB-175 — AAM et AAD

- **Le vrai PC.** SF, ZF et PF suivent AL.
- **L'émulateur.** PCem les tire d'AX : ZF faux quand AL est nul et AH ne l'est pas, SF toujours nul après AAD.
- **La correction.** Les drapeaux d'après AL.

### PB-177 — un REP devant autre chose qu'une chaîne

- **Le vrai PC.** Un préfixe vaut pour l'instruction qui le suit, dans n'importe quel ordre ; un REP devant une
  instruction qui n'est pas une chaîne est sans effet, et 6Eh est l'alias de JLE (OUTS naît avec le 186).
- **L'émulateur.** PCem relance l'instruction à son début plus un octet, en la terminant : un préfixe de segment
  placé avant le REP est perdu, REP DS: ne répète pas, REP 6Eh envoie des octets sur un port.
- **La correction.** On reprend juste après le REP, dans la même instruction, préfixes compris. Et l'IDIV qu'un REP
  précède rend l'opposé de son quotient, comme le 8088 et le 8086 le font (mesuré sur le corpus) : un effet de leur
  microcode, que PCem ignore.
- **Le temps.** PCem facture le REP 20 cycles et vide la file : le mode matériel l'a gardé un jour, puis l'a corrigé
  (§ G13.3, suite, ci-dessous).

## G13.3, suite — le temps d'un REP devant autre chose qu'une chaîne (mode matériel)

*Le 9 octobre 2026, écrite avec son commit ; `PLAN-G13.md` § G13.3, `VERIFICATION.md` § G13.3, le temps du REP.*

### PB-177 — le prix du REP

- **Le vrai PC.** Un REP devant une instruction qui n'est pas une chaîne n'est qu'un préfixe : le processeur le lit
  et passe à l'octet suivant, la file de préfetch intacte. Mesuré sur le corpus, où ce cas n'existe que devant IDIV :
  quand le 8088 part d'une file vide, le REP lui coûte 7,6 cycles en moyenne, le temps d'aller chercher son octet et
  de le décoder ; quand la file le tient déjà, presque rien.
- **L'émulateur.** PCem lit l'octet qui suit le REP, voit que ce n'est pas une chaîne, revient en arrière, facture
  20 cycles et vide la file : l'instruction qui suit se relit octet par octet sur le bus. En tout, le REP coûtait
  27,8 cycles dans les mêmes cas, 20 de trop.
- **La correction.** Le REP regarde le premier octet qui suit ses préfixes de segment sans le lire. Devant une chaîne,
  rien ne change. Devant autre chose, il ne coûte que le prix d'un préfixe, 4 cycles comme les préfixes de segment
  du cœur, et la file reste pleine. Il coûte maintenant 7,8 cycles, contre 7,6 sur le silicium.
- **Ce qui reste.** Quand la file du silicium tient déjà l'octet du REP, le cœur paie encore 8 cycles de plus : la
  sonde part toujours d'une file vide, ce que le corpus ne fait qu'une fois sur deux au 8088, jamais au 8086. Cet
  écart vient de la mesure, pas du REP. Les instructions autres qu'IDIV ne sont pas mesurées : le corpus n'en a pas
  sous REP. Elles prennent le même prix de préfixe.

## G13.4 — la carte mère (mode matériel) : le 8259

*Le 9 octobre 2026, écrite avec son commit ; `PLAN-G13.md` § G13.4, `VERIFICATION.md` § G13.4a.*

Le 8259 est le contrôleur d'interruptions : il reçoit les huit lignes IRQ (seize sur l'AT, par un second 8259 branché
sur l'IRQ 2 du premier), choisit la plus prioritaire et la présente au processeur. Cinq défauts, tous en mode matériel.
La source est la fiche du 8259A d'Intel ; PICBANC, un petit programme sous DEBUG, les montre sur l'XT et sur l'AT.

### PB-246 — l'ordre de service sur l'AT

- **Le vrai PC.** IR0 est la plus prioritaire, IR7 la moins. Sur l'AT, les IRQ 8 à 15 passent par IR2 : elles se
  servent après l'horloge (IRQ 0) et le clavier (IRQ 1), avant les IRQ 3 à 7.
- **L'émulateur.** PCem teste l'esclave avant tout, dès IR0 : une IRQ 8 à 15 passe avant l'horloge et le clavier.
- **La correction.** L'esclave se sert à son rang, IR2.

### PB-05 — l'IRQ perdue

- **Le vrai PC.** Servir une IRQ efface sa seule demande.
- **L'émulateur.** Servir l'IRQ 8 + n efface aussi la demande de l'IRQ n du premier 8259 : l'IRQ 8 de l'horloge
  temps réel efface un tic d'horloge en attente. Avec PB-246, ce tic est perdu.
- **La correction.** Servir l'esclave ne touche au premier 8259 que par sa ligne IR2.

### PB-247 — une interruption dans une interruption de même rang

- **Le vrai PC.** Tant qu'une IRQ est en service (son gestionnaire n'a pas envoyé sa fin d'interruption), le 8259
  retient les IRQ de même rang ou de rang moindre, même si le gestionnaire a rouvert les interruptions.
- **L'émulateur.** Sur le 8088, PCem ne regarde que le masque : l'IRQ 1 du clavier interrompt l'INT 08h du BIOS, qui
  rouvre les interruptions avant sa fin. Et une chaîne REP s'arrête pour cette IRQ.
- **La correction.** Le 8259 et le 8088 tiennent compte de ce qui est en service ; la chaîne va au bout.

### PB-248 — les commandes que PCem ne connaît pas

- **Le vrai PC.** Le 8259 sait faire tourner ses priorités, fixer la moins prioritaire, ne rien faire (« no
  operation »), être interrogé sans interruption (le poll), et lever l'effet de ce qui est en service (le masque
  spécial).
- **L'émulateur.** Toutes ces commandes, PCem les prend pour une fin d'interruption, et il ignore le poll et le masque
  spécial.
- **La correction.** Les huit formes de la commande, le poll et le masque spécial, selon la fiche. Aucun BIOS du dépôt
  ne s'en sert ; un programme qui s'en sert voit maintenant ce qu'il demande.

### PB-255 — la lecture après l'initialisation

- **Le vrai PC.** Après l'initialisation, une lecture du 8259 rend les demandes en attente.
- **L'émulateur.** PCem rend ce qui est en service, ou ce qu'une commande d'avant avait choisi.
- **La correction.** La lecture revient sur les demandes à l'initialisation et à la mise sous tension.


## G13.4, suite — la carte mère (mode matériel) : le 8237

*Le 9 octobre 2026, écrite avec son commit ; `PLAN-G13.md` § G13.4, `VERIFICATION.md` § G13.4b.*

Le 8237 est le contrôleur de DMA : il déplace des octets entre un périphérique et la mémoire sans passer par le
processeur, pour la disquette, le disque de l'XT ou une carte son. Le PC en a un (canaux 0 à 3) ; l'AT en a deux, le
second (canaux 4 à 7) branché en cascade : le premier n'obtient le bus que par le canal 4 du second. Six défauts, tous
en mode matériel. La source est la fiche du 8237A d'Intel ; DMABANC, un petit programme sous DEBUG, les montre sur
l'XT et sur l'AT.

### PB-249 — démasquer tous les canaux d'un coup

- **Le vrai PC.** Une commande (port 0Eh, DCh sur l'AT) démasque les quatre canaux d'un contrôleur.
- **L'émulateur.** PCem l'ignore : les canaux restent masqués, leurs transferts ne partent pas.
- **La correction.** La commande efface les quatre masques.

### PB-250 — le transfert demandé par le logiciel

- **Le vrai PC.** Un programme peut lancer lui-même un transfert, sans périphérique (port 09h, D2h) : en mode bloc, le
  8237 va jusqu'au bout du compte.
- **L'émulateur.** PCem l'ignore : rien ne se passe.
- **La correction.** La requête est servie, jusqu'au bout du compte. La vérification et la lecture ne touchent pas la
  mémoire ; l'écriture non plus, parce que personne ne fournit la donnée.

### PB-251 — la remise à zéro par le logiciel

- **Le vrai PC.** La remise à zéro d'un contrôleur (port 0Dh, DAh) fait comme le reset : elle efface aussi la
  commande, l'état et les requêtes.
- **L'émulateur.** PCem ne remet que les masques et le sélecteur d'octet : un contrôleur désactivé le reste.
- **La correction.** Elle efface aussi la commande, l'état et les requêtes.

### PB-252 — l'état à la mise sous tension

- **Le vrai PC.** Au reset, les huit canaux sont masqués, la commande et l'état effacés.
- **L'émulateur.** PCem démasque les huit canaux et garde la commande et l'état d'avant.
- **La correction.** Les masques posés, le reste effacé. Les BIOS du dépôt démasquent leurs canaux avant de s'en
  servir : leurs POST n'en changent pas.

### PB-253 — la cascade sur l'AT

- **Le vrai PC.** Si le canal 4 est masqué, s'il n'est pas en mode cascade, ou si le second 8237 est désactivé, les
  canaux 0 à 3 n'obtiennent pas le bus.
- **L'émulateur.** PCem ne modélise pas la cascade : les canaux 0 à 3 transfèrent quoi qu'il arrive.
- **La correction.** Les canaux 0 à 3 de l'AT passent par le canal 4. La 1542C, une carte SCSI qui prend le bus
  elle-même, passe outre de la même façon ; elle relève du stockage, et son défaut est inscrit à part (PB-260).

### PB-157 — la commande du second 8237

- **Le vrai PC.** La commande du second 8237 (port D0h) peut le désactiver ; son registre temporaire (DAh) se relit
  à 00h.
- **L'émulateur.** PCem ne range pas la commande, et DAh rend le dernier octet écrit.
- **La correction.** La commande est rangée et DAh rend 00h. Le reste de l'ancienne entrée (ce que rendent les
  lectures que la fiche interdit, et les bits de requête de l'état) n'a pas de valeur connue ou demande une ligne que
  PCem n'a pas : il reste reproduit, sous un numéro à lui (PB-259).

## G13.4, fin — la carte mère (mode matériel) : le 8042, la souris PS/2 et les ports

*Le 9 octobre 2026, écrite avec son commit ; `PLAN-G13.md` § G13.4, `VERIFICATION.md` § G13.4c.*

Le 8042 est le contrôleur du clavier de l'AT : il reçoit les codes du clavier et, sur les machines qui en ont une, les
octets de la souris PS/2, et les tend au processeur par un seul port (60h). Avec lui, la souris PS/2 elle-même, le
second port parallèle du PC1512 et le chapeau de deux manettes. Six défauts, tous en mode matériel. PS2BANC et
JOYBANC, deux programmes sous DEBUG, les montrent dans les deux modes.

### PB-254 — les files d'attente du 8042

- **Le vrai PC.** Le clavier de l'AT garde seize codes quand le programme ne les lit pas ; il remplace le dix-septième
  par un code de débordement, que le BIOS signale d'un bip, et perd les suivants. La souris attend, retenue par le
  contrôleur, et ne perd rien.
- **L'émulateur.** Chaque file de PCem tient seize places sans garde : au seizième octet en attente, elle paraît vide,
  et tout ce qu'elle tenait est perdu. Vingt touches non lues n'en laissent passer que quatre.
- **La correction.** Le clavier garde seize codes, puis le code de débordement ; le contrôleur et la souris gardent
  tout, dans l'ordre (64 octets au plus, une borne pour que l'émulateur ne grossisse pas sans fin).

### PB-94 — les commandes de la souris PS/2

- **Le vrai PC.** La souris acquitte toute commande (FAh), y compris « valeurs par défaut », « mode distant », « mode
  flux » et l'écho, et rejette une commande inconnue (FEh, puis FCh).
- **L'émulateur.** PCem ne répond rien à ces commandes : un pilote attend l'acquittement jusqu'à son délai, puis croit
  la souris absente.
- **La correction.** Les cinq commandes sont faites et acquittées ; une commande inconnue est rejetée. La souris neuve
  ou remise à zéro prend ses valeurs par défaut (100 points par seconde, 4 points par mm), que PCem laisse à zéro. Le
  renvoi du dernier paquet (FEh) n'est pas modélisé.

### PB-95 — l'octet d'état de la souris

- **Le vrai PC.** Dans l'état que rend la souris (commande E9h), IBM met le bouton gauche en bit 2 et le droit en
  bit 0.
- **L'émulateur.** PCem reprend la disposition du paquet de mouvement : un pilote voit le gauche comme le droit, et le
  milieu comme deux boutons.
- **La correction.** La disposition d'IBM, et le mode distant en bit 6.

### PB-261 — la lecture de la souris à la demande

- **Le vrai PC.** En mode distant, le pilote demande chaque mouvement (EBh) ; après l'avoir envoyé, la souris remet ses
  compteurs à zéro.
- **L'émulateur.** PCem ne les remet pas : la lecture suivante rend encore le même mouvement, et le pointeur dérive.
- **La correction.** Les compteurs à zéro après le paquet. Le défaut, trouvé par la contre-lecture de cette étape, est
  inscrit au registre.

### PB-101 — le second port parallèle du PC1512

- **Le vrai PC.** Le PC1512 n'a qu'un port parallèle, en 378h.
- **L'émulateur.** PCem pose un second port en 278h sur toutes les machines, et le retrait propre aux Amstrad vise une
  autre adresse : le PC1512 garde un port que la machine n'a pas, et son BIOS le trouve.
- **La correction.** Le second port est retiré là où il est.

### PB-103 — le chapeau en haut à gauche

- **Le vrai PC.** Le chapeau de la CH Flightstick Pro et de la ThrustMaster FCS n'a que quatre directions.
- **L'émulateur.** Un chapeau d'hôte à huit directions donne 315° pour le haut-gauche, et PCem ne le range nulle part :
  la CH le lit au repos, la TM en bas.
- **La correction.** 315° se lit en haut, comme chacune des trois autres diagonales se lit à la direction suivante
  dans le sens des aiguilles d'une montre. C'est une convention, que rien ne fixe dans le matériel ; elle se renverse
  sans peine.

### Ce qui reste

PB-104, le chapeau calculé sur un axe de la manette de l'hôte quand le .cfg ne dit rien, est un défaut de l'hôte sans
pendant matériel : il reste dans les deux modes (décision n° 9). PB-256, l'absence de rafraîchissement par DMA sur la
M24 et le PC1512, changerait la vitesse de ces deux machines pour un coût que rien ne documente : il reste reproduit,
la question posée.
