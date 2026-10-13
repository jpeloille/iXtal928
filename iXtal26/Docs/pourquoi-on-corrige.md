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

## G13.5, début — le 286, le 386 et le 486 (mode matériel) : l'arithmétique et les instructions que le corpus mesure

*Le 10 octobre 2026, écrite avec son commit ; `PLAN-G13.md` § G13.5, `VERIFICATION.md` § G13.5a.*

Le cœur de PCem qui exécute le 286, le 386 et le 486 a ses propres défauts, distincts de ceux du 8088. Les
SingleStepTests du 386 (un Intel 386EX) et du 286 (un Harris 80C286) les mesurent instruction par instruction, en
mode réel. Neuf défauts, tous en mode matériel ; le corpus du 286 dit, pour chacun, si le 286 le partage.

### PB-181 — la demi-retenue d'ADC

- **Le vrai PC.** AF est la retenue qui sort du bit 3, celle dont DAA et AAA ont besoin ; dans ADC, la retenue
  entrante y compte.
- **L'émulateur.** PCem recopie sur le quartet bas une formule écrite pour l'octet entier : avec une retenue entrante
  et un quartet bas de Fh, AF manque, 3 % des cas.
- **La correction.** AF est le bit 4 de op1 ^ op2 ^ résultat, la définition même de la retenue du bit 3.

### PB-182 — LOCK

- **Le vrai PC.** Le 386 n'accepte LOCK que devant les instructions qui lisent, modifient et réécrivent la mémoire
  (ADD, OR, ADC, SBB, AND, SUB, XOR, NOT, NEG, INC, DEC, XCHG, BTS, BTR, BTC ; CMPXCHG et XADD sur le 486) ; ailleurs,
  ou devant leur forme registre, il lève l'exception d'opcode invalide.
- **L'émulateur.** PCem ne refuse que LOCK NOP ; tout le reste s'exécute comme si LOCK n'était pas là.
- **La correction.** La liste, et l'exception hors d'elle. Un programme qui teste le processeur par ce chemin voit un
  386.

### PB-183 et PB-262 — BT, BTS, BTR et BTC

- **Le vrai PC.** Ces instructions prennent un bit dans une chaîne de bits en mémoire : un décalage négatif remonte en
  arrière, une adresse de 16 bits reste dans son segment, et le décalage immédiat d'un mot se prend modulo 16.
- **L'émulateur.** PCem traite le décalage comme non signé (il va jusqu'à 8 Ko trop loin en avant), laisse l'adresse
  sortir du segment, et, avec un immédiat sur deux (ceux dont le bit 4 est posé), vise un bit hors du mot : BT rend
  alors 0, BTS, BTR et BTC n'écrivent rien.
- **La correction.** Le décalage signé, l'adresse repliée, l'immédiat réduit. Le troisième défaut, trouvé par la mesure
  de cette étape, est inscrit au registre (PB-262) ; le second élargit PB-183.

### PB-184 — MOVSX r16

- **Le vrai PC.** Le 386 mesuré exécute MOVSX vers un registre de 16 bits depuis un mot : une simple copie.
- **L'émulateur.** PCem n'a que la forme 32 bits, et lève l'exception d'opcode invalide sur l'autre.
- **La correction.** La forme 16 bits, dans une table d'opcodes propre au mode, posée au démarrage du processeur.

### PB-185, PB-186 et PB-188 — AAA, AAS, AAD, AAM et DAS

- **Le vrai PC.** Le 386 ajuste AX entier dans AAA et AAS (AX + 106h), pose SF, ZF et PF d'après AL dans AAD et AAM, et
  décide le second pas de DAS sur l'AL et le CF d'origine, comme le décrit le manuel d'Intel.
- **L'émulateur.** PCem reprend l'ajustement du 8086, les drapeaux d'après AX, et le DAS du 8088 : de 4 % à 50 % des
  cas de ces instructions s'écartent du silicium.
- **La correction.** Celle du manuel, que le 386EX suit au cas près.

### PB-187 — AAM 0

- **Le vrai PC.** AAM divise AL par son opérande ; par zéro, il lève l'erreur de division.
- **L'émulateur.** PCem remplace la base nulle par 10, et le programme continue sur un résultat inventé.
- **La correction.** L'erreur de division, l'adresse de l'AAM empilée. Les drapeaux que le 386EX laisse alors ne suivent
  aucune règle connue : ils ne sont pas imités.

## G13.5, suite — le 286, le 386 et le 486 (mode matériel) : la longueur et les bornes d'une instruction

*Le 10 octobre 2026, écrite avec son commit ; `PLAN-G13.md` § G13.5, `VERIFICATION.md` § G13.5b.*

Avant d'exécuter une instruction, le processeur la lit : ses octets doivent tenir dans le segment de code, et ils ne
doivent pas être trop nombreux. PCem ne vérifie ni l'un ni l'autre, et décode quelques instructions système d'une
longueur que le silicium ne leur donne pas. Quatre défauts, tous en mode matériel.

### PB-51 — la limite du segment de code

- **Le vrai PC.** Chaque octet d'une instruction doit se trouver dans la limite du segment de code ; en mode réel, à
  l'offset FFFFh au plus. Une instruction qui la passe lève l'exception de protection générale avant de s'exécuter, et
  c'est son adresse qui est empilée. Le 386EX du corpus le fait sur 148 instructions à cheval sur FFFFh.
- **L'émulateur.** PCem lit les octets sans regarder la limite : l'instruction déborde dans les 64 Ko suivants, et
  l'exécution continue au-delà.
- **La correction.** Un décodeur de longueur, en tête d'instruction, compte les octets qu'elle va lire et lève
  l'exception si l'un d'eux passe la limite. Il ne travaille qu'au bord d'un segment ou devant une longue suite de
  préfixes ; ailleurs, un seul test suffit.

### PB-50 — la longueur d'une instruction

- **Le vrai PC.** Une instruction a 15 octets au plus sur le 386 et le 486, 10 sur le 286, préfixes compris ; au-delà,
  l'exception de protection générale. Le 286 du corpus la lève à onze octets, sur 239 cas, et le 386EX exécute ses
  instructions de quinze.
- **L'émulateur.** PCem ne compte rien : une mémoire remplie de préfixes forme une seule instruction de plus d'un
  million d'octets.
- **La correction.** Le même décodeur compte aussi la longueur.

### PB-43 — MOV vers et depuis les registres de contrôle, de débogage et de test

- **Le vrai PC.** Ces instructions désignent toujours un registre : le champ qui, ailleurs, annonce une adresse en
  mémoire est ignoré, et l'instruction a trois octets.
- **L'émulateur.** PCem décode ce champ comme une adresse, avale des octets de déplacement qui n'en sont pas, et saute
  les instructions qui suivent. Une variante de ce défaut (PB-44, la taille d'adresse mal lue) ne s'observe plus une
  fois celui-ci corrigé.
- **La correction.** Le registre seul, sans déplacement.

### PB-78 — LOADALL sur le 486

- **Le vrai PC.** LOADALL, l'instruction non documentée qui charge tout l'état du processeur, n'existe plus sur le
  486 : son code y est invalide.
- **L'émulateur.** PCem donne au 486 la table d'instructions du 386, LOADALL compris, et un programme qui sonde le
  processeur par ce chemin voit un 386.
- **La correction.** L'exception d'opcode invalide sur le 486.

## G13.5, fin — le 286, le 386 et le 486 (mode matériel) : le mode protégé

*Le 10 octobre 2026, écrite avec son commit ; `PLAN-G13.md` § G13.5, `VERIFICATION.md` § G13.5c.*

En mode protégé, le processeur contrôle chaque passage d'un programme à un autre : une interruption passe par une
table de portes, un changement de tâche par un descripteur qui doit en être un, et les instructions qui chargent
l'état de la machine sont réservées au système. PCem en saute plusieurs, et, sur deux points, ne sait pas faire ce que
le processeur fait. Huit défauts, tous en mode matériel. L'exception levée est celle du manuel de chaque processeur :
sur le 386 et le 486, l'exception de TSS invalide pour un CALL ou une interruption, de protection générale pour un JMP ;
sur le 286, de protection générale partout.

### PB-32 et PB-192 — la table des interruptions

- **Le vrai PC.** Une interruption dont la porte, huit octets, sort de la table lève l'exception de protection générale,
  avec un code qui désigne la porte fautive ; un bit de ce code dit si l'événement venait de l'extérieur du programme
  (une interruption matérielle, une exception) plutôt que d'une instruction INT.
- **L'émulateur.** PCem ne compare à la limite que le premier octet de la porte, et une erreur de priorité
  d'opérateurs dans son code C rend toujours un code nul ; ailleurs, le bit « extérieur » n'est jamais posé.
- **La correction.** La porte entière contre la limite, le bon code, et le bit quand l'événement est extérieur.

### PB-39 — la porte de tâche

- **Le vrai PC.** Un CALL ou un JMP peut passer par une porte de tâche, un descripteur qui désigne la tâche à
  reprendre : le processeur change de tâche.
- **L'émulateur.** PCem ne sait passer que par le descripteur de tâche lui-même, qu'il appelle à tort « porte de
  tâche » ; devant une vraie porte, il lève une exception.
- **La correction.** La porte contrôlée, puis le changement de tâche, comme par le descripteur de tâche.

### PB-40 — l'adresse de retour d'un CALL de tâche

- **Le vrai PC.** Un CALL vers une autre tâche n'empile rien : la nouvelle tâche garde le lien vers l'ancienne, et
  son IRET y revient.
- **L'émulateur.** PCem empile l'adresse de retour après le changement, donc sur la pile de la nouvelle tâche, qu'il
  décale de deux mots.
- **La correction.** Rien d'empilé quand le CALL a changé de tâche.

### PB-190 et PB-191 — les contrôles d'une tâche

- **Le vrai PC.** LTR, qui désigne la tâche courante, n'accepte qu'un descripteur de tâche disponible et présent, dans
  la table globale. Un CALL, un JMP ou une interruption vers une tâche contrôlent de même le privilège, la présence et
  la table du descripteur.
- **L'émulateur.** PCem accepte n'importe quel descripteur, y compris un segment de données ou une tâche déjà en
  cours, et un programme sans privilège peut changer de tâche vers le système.
- **La correction.** Les contrôles, et l'exception quand l'un d'eux échoue.

### PB-193 — LOADALL hors du système

- **Le vrai PC.** LOADALL, qui charge tout l'état du processeur, est réservé au niveau de privilège du système ; un
  programme ordinaire qui l'essaie lève l'exception de protection générale.
- **L'émulateur.** PCem l'exécute à tout niveau : un programme ordinaire prend la machine entière.
- **La correction.** L'exception hors du niveau 0, en mode protégé.

### PB-263 — l'adresse d'arrivée d'un CALL de tâche

- **Le vrai PC.** Après avoir changé de tâche, un CALL vérifie que l'adresse où la nouvelle tâche reprend tient dans
  son segment de code ; sinon, sur le 386 et le 486, l'exception de TSS invalide, dans la nouvelle tâche.
- **L'émulateur.** PCem ne vérifie rien et exécute au-delà du segment.
- **La correction.** La vérification après le changement de tâche.

## G13.5, la limite des données — le 286, le 386 et le 486 (mode matériel)

*Le 10 octobre 2026, écrite avec son commit ; `PLAN-G13.md` § G13.5, `VERIFICATION.md` § G13.5d.*

### PB-189 — un opérande qui sort de son segment

- **Le vrai PC.** Un segment fait au plus 64 Ko en mode réel. Une donnée lue ou écrite doit y tenir tout entière : un
  mot posé à cheval sur la dernière adresse du segment, ou une adresse de 32 bits qui le dépasse, lève une exception de
  protection générale, ou de pile pour la pile. Le 286 regarde chaque mot d'une donnée longue séparément ; le 386, la
  donnée entière.
- **L'émulateur.** PCem ne vérifie la limite que pour une poignée d'instructions : toutes les autres lisent et écrivent
  au-delà du segment, dans la mémoire qui suit, sans exception.
- **La correction.** Chaque accès à la mémoire compare la donnée à la limite de son segment avant de la lire ou de
  l'écrire. Sur le corpus du vrai 386, plus de cent mille cas passent de faux à juste ; trois règles fines du silicium
  sont venues des rares cas que la première version faisait tomber.

## G13.6, début — le coprocesseur arithmétique (mode matériel) : ce que les instructions rendent

*Le 10 octobre 2026, écrite avec son commit ; `PLAN-G13.md` § G13.6, `VERIFICATION.md` § G13.6a.*

Le coprocesseur (8087, 287, 387, et celui du 486) calcule en virgule flottante. PCem le rend presque partout, mais se
trompe sur quelques réponses que les programmes lisent pour décider : l'ordre de deux nombres, la nature d'un nombre,
une constante, la profondeur de la pile. Huit défauts, tous en mode matériel.

### PB-61 — FNSTSW AX

- **Le vrai PC.** L'instruction copie le mot d'état du coprocesseur dans AX, y compris le niveau de sa pile de
  registres ; les programmes de détection le lisent ainsi.
- **L'émulateur.** PCem oublie le niveau de pile : AX dit toujours que la pile est vide.
- **La correction.** Le mot d'état entier, comme la forme qui l'écrit en mémoire le donne déjà.

### PB-57, PB-58, PB-64 et PB-70 — comparer deux nombres

- **Le vrai PC.** Une comparaison rend « plus petit », « égal », « plus grand » ou « non ordonné » ; un NaN (« pas un
  nombre ») n'est jamais ordonné ; −0 et +0 sont égaux. Le 8087 et le 287 connaissent de plus un infini « projectif »,
  leur réglage par défaut, où +∞ et −∞ sont le même point : c'est ainsi qu'un programme distingue un 287 d'un 387.
- **L'émulateur.** Deux formes de la comparaison prennent un NaN pour un nombre plus petit ; une astuce rend −0 plus
  petit que +0 pour tromper une détection ; et l'infini projectif n'existe pas : PCem fait passer tout 287 pour un 387.
- **La correction.** La comparaison du silicium partout, et l'infini projectif sur le 8087 et le 287. Sans l'astuce,
  les démarrages des machines détectent toujours leur coprocesseur.

### PB-63 — FXAM

- **Le vrai PC.** FXAM dit ce qu'est le nombre au sommet de la pile : un NaN, un infini, un zéro, un nombre ordinaire,
  un registre vide, et son signe.
- **L'émulateur.** PCem ne connaît que vide, zéro et « ordinaire », et perd le signe de −0.
- **La correction.** Toutes les classes, et le signe.

### PB-66 — les constantes

- **Le vrai PC.** Le coprocesseur charge en une instruction π, ln 2 et trois autres logarithmes, arrondis au plus
  près ; le 387 et le 486 les arrondissent selon le mode d'arrondi choisi.
- **L'émulateur.** PCem charge ln 2 trop grand d'une unité sur le dernier chiffre, et ignore le mode d'arrondi.
- **La correction.** Les valeurs exactes, arrondies comme le coprocesseur les arrondit.

### PB-67 — copier un entier de 64 bits

- **Le vrai PC.** Un registre du coprocesseur tient exactement tout entier de 64 bits ; le copier d'un registre à un
  autre le garde.
- **L'émulateur.** PCem calcule en 53 bits et garde l'entier exact à part, mais oublie de le copier : la copie rend un
  entier ancien.
- **La correction.** La copie emporte l'entier exact.

## G13.6, suite — le coprocesseur : deux oublis de PCem

*Le 10 octobre 2026, écrite avec son commit ; `PLAN-G13.md` § G13.6, `VERIFICATION.md` § G13.6b.*

### PB-213 — une comparaison efface le bit C1

- **Le vrai PC.** Le mot d'état du 387 et du 486 a quatre bits de « condition ». Une comparaison en pose trois et
  remet le quatrième, C1, à zéro.
- **L'émulateur.** PCem laisse C1 tel qu'une instruction précédente l'a laissé : un programme qui lit les quatre bits
  ensemble se trompe.
- **La correction.** Sur le 387 et le 486, chaque comparaison efface C1 ; le 8087 et le 287, où ce bit est indéfini,
  gardent ce qu'ils avaient.

### PB-207 — ranger l'environnement masque les exceptions

- **Le vrai PC.** FSTENV range l'état du coprocesseur en mémoire, puis masque toutes ses exceptions : un gestionnaire
  d'erreur commence ainsi, pour ne pas en déclencher une seconde.
- **L'émulateur.** PCem range, mais ne masque rien.
- **La correction.** Les six masques posés après le rangement.

## G13.6, fin — le coprocesseur : où va une erreur de calcul

*Le 10 octobre 2026, écrite avec son commit ; `PLAN-G13.md` § G13.6, `VERIFICATION.md` § G13.6c.*

La seule erreur de calcul que PCem connaît est la division par zéro. Un programme peut demander qu'elle l'interrompe
au lieu de rendre l'infini : il la « démasque ». Ce qui se passe alors dépend du coprocesseur et de la carte mère.

### PB-59 — l'erreur laisse sa marque dans le mot d'état

- **Le vrai PC.** Le coprocesseur pose deux bits de plus : « une erreur attend » (ES) et « occupé » (B). Un
  gestionnaire d'erreur les lit pour savoir ce qui s'est passé ; FNCLEX les efface.
- **L'émulateur.** PCem ne pose ni l'un ni l'autre, et son FNCLEX garderait B.
- **La correction.** Les deux bits posés, et effacés par FNCLEX. Sur le 486, réglé pour (bit NE de CR0), l'erreur
  interrompt le processeur lui-même (#MF) devant l'instruction de calcul suivante.

### PB-69 — sur un PC ou un XT, l'erreur passe par la NMI

- **Le vrai PC.** Le 8087 du PC et de l'XT signale par une broche reliée à l'interruption non masquable (NMI). Le port
  A0h l'ouvre ou la ferme ; le BIOS, puis le programme, y accrochent leur gestionnaire.
- **L'émulateur.** PCem envoie l'erreur à l'IRQ13, qui n'existe pas sur ces machines : elle se perd. Et sa NMI, s'il
  en levait une, reviendrait sans fin, quand le processeur ne la prend qu'une fois.
- **La correction.** Le 8087 lève la NMI, que le port A0h laisse passer ou retient ; prise, elle est consommée. La
  transcription avait deux copies du réglage du port A0h, là où PCem n'en a qu'une : en mode matériel, le port règle
  celle que lit le processeur.

### PB-204 — sur un AT, la carte bloque le coprocesseur jusqu'au gestionnaire

- **Le vrai PC.** L'AT mène l'erreur du 287 à l'IRQ13, et la retient : le processeur ne lance plus d'instruction de
  calcul tant que le gestionnaire n'a pas écrit au port F0h. Le port F1h remet le coprocesseur à zéro. Le 486 fait
  de même avec ses propres broches : arrêté devant l'instruction suivante, il repart quand la carte, sur une écriture
  au port F0h, l'autorise à ignorer l'erreur.
- **L'émulateur.** PCem ne retient rien et n'a ni F0h ni F1h : le programme continue ses calculs avant que l'erreur
  soit traitée, et un BIOS qui remet le coprocesseur à zéro par F1h n'y parvient pas.
- **La correction.** Le blocage, les deux ports, et l'arrêt du 486. Les sept instructions de contrôle que le
  processeur lance sans attendre (FNINIT, FNCLEX, FNSTSW, et quatre autres) passent, comme sur la machine.

## G13.6, le noyau — un coprocesseur de 80 bits, en chantier

*Le 11 octobre 2026, écrite avec son commit ; `PLAN-G13.md` § G13.6, le noyau, `VERIFICATION.md` § G13.6, le noyau,
N2.*

Le coprocesseur calcule sur 80 bits ; PCem, sur les 64 bits d'un `double` de l'hôte. Le mode matériel reçoit donc un
coprocesseur à lui, le noyau : huit registres de 80 bits et une bibliothèque de calcul vérifiée bit à bit (SoftFloat).
Il s'écrit en huit étapes. Tant qu'il n'est pas complet, il ne remplace pas celui de PCem pour l'utilisateur : seuls
les tests le demandent, par ses numéros. Cette première étape pose l'état, le RESET, la pile et les instructions de
contrôle (FNINIT, FNCLEX, FLDCW, FNSTCW, FNSTSW, FNOP, FINCSTP, FDECSTP, FFREE).

### PB-203 — le RESET remet le coprocesseur dans son état de départ

- **Le vrai PC.** Au RESET, le coprocesseur reprend un état connu, qui dépend de son modèle. Le 8087 et le 287 sont
  dans l'état de FNINIT : toutes les exceptions masquées, la pile vide. Le 387 aussi, à ceci près qu'il lève une
  erreur exprès : c'est ainsi que le 386 sait, au démarrage, qu'un 387 est présent. Le 486 ne change rien, à moins
  qu'on lui demande son auto-test.
- **L'émulateur.** PCem ne remet rien : son coprocesseur garde l'état d'avant le RESET, et sort de la mise sous
  tension avec un mot de contrôle nul, qui démasque toutes les exceptions.
- **La correction.** Chaque modèle reçoit son état de RESET ; le port F1h de l'AT, qui remet le coprocesseur à zéro,
  passe par le même chemin.

### PB-264 — la pile du coprocesseur déborde

- **Le vrai PC.** La pile a huit places. Empiler une neuvième valeur, ou lire une place vide, est une erreur : le
  coprocesseur la signale (le 387 et le 486 disent en plus laquelle des deux), et range à la place une valeur
  « indéfinie », qu'un programme reconnaît.
- **L'émulateur.** PCem ne regarde jamais si une place est libre : la neuvième valeur écrase la plus ancienne, et une
  place vide se lit comme si elle contenait encore son ancien nombre, sans erreur.
- **La correction.** La pile du noyau signale l'erreur et range l'indéfini. Les instructions qui empilent et lisent
  arrivent aux étapes suivantes ; elles passeront toutes par cette pile.

## G13.6, le noyau, suite — charger et ranger des nombres

*Le 11 octobre 2026, écrite avec son commit ; `PLAN-G13.md` § G13.6, le noyau, `VERIFICATION.md` § G13.6, le noyau,
N3a.*

Le noyau apprend à charger des nombres depuis la mémoire et à les y ranger, dans tous les formats du coprocesseur :
réels de 32, 64 et 80 bits, entiers de 16, 32 et 64 bits, décimaux de dix-huit chiffres. Toujours en chantier : seuls
les tests le demandent.

### PB-52 — charger un nombre décimal

- **Le vrai PC.** FBLD lit dix-huit chiffres décimaux et un signe, et les empile. Des tableurs comme Lotus 1-2-3 s'en
  servent.
- **L'émulateur.** PCem n'a pas cette instruction : elle ne charge rien, et le calcul continue sur ce qui était là.
- **La correction.** Les chiffres convertis exactement et empilés.

### PB-195 — ranger un nombre décimal

- **Le vrai PC.** FBSTP arrondit le nombre selon le mode choisi par le programme, puis le range en décimal ; un nombre
  trop grand pour dix-huit chiffres, ou qui n'en est pas un, donne une valeur « indéfinie » et une erreur.
- **L'émulateur.** PCem tronque au lieu d'arrondir (12,7 devient 12), et un nombre trop grand déborde dans l'octet du
  signe, sans erreur.
- **La correction.** L'arrondi, la borne, l'indéfini et l'erreur.

### PB-196 — ranger un entier trop grand

- **Le vrai PC.** FIST range un entier ; s'il ne tient pas dans 16 ou 32 bits, le coprocesseur range une valeur
  « indéfinie » et signale l'erreur.
- **L'émulateur.** PCem garde les bits du bas : 40 000 devient −25 536, sans erreur.
- **La correction.** L'indéfini et l'erreur.

### PB-55 et PB-56 — charger et ranger 80 bits tels quels

- **Le vrai PC.** Le format de 80 bits est celui des registres : FLD et FSTP le copient sans rien changer.
- **L'émulateur.** PCem le convertit en `double` : un exposant trop grand se replie (2^1024 devient 1), un très petit
  nombre change de valeur, et onze bits de précision se perdent.
- **La correction.** Les 80 bits copiés.

### PB-206 et PB-212 — charger un « NaN signalant »

- **Le vrai PC.** Un NaN signalant est une valeur piège : le 387 et le 486 signalent une erreur en le chargeant, et le
  rendent inoffensif.
- **L'émulateur.** PCem ne signale rien ; en 64 bits, il garde même le piège intact.
- **La correction.** L'erreur, et le NaN rendu silencieux, sur le 387 et le 486 ; le 8087 et le 287, qui ne signalent
  rien, restent ainsi.

## G13.6, le noyau, suite — sauver l'état du coprocesseur

*Le 11 octobre 2026, écrite avec son commit ; `PLAN-G13.md` § G13.6, le noyau, `VERIFICATION.md` § G13.6, le noyau,
N3b.*

Le noyau apprend à sauver et à recharger tout son état : les mots de contrôle et d'état, les étiquettes, l'adresse de
la dernière instruction et les huit registres. Un système multitâche le fait à chaque changement de tâche, un
gestionnaire d'erreur pour savoir quelle instruction a fauté. Toujours en chantier : seuls les tests le demandent.

### PB-62 — l'adresse de la dernière instruction

- **Le vrai PC.** Le coprocesseur retient l'adresse de la dernière instruction de calcul, son code et l'adresse de son
  opérande. FSTENV et FSAVE les rangent, sous quatre formes selon le mode du processeur ; un gestionnaire d'erreur y lit
  l'instruction fautive.
- **L'émulateur.** PCem ne les retient jamais : il range des zéros, et laisse certains champs de l'image tels qu'ils
  étaient en mémoire. Le 287 choisit sa forme selon FSETPM, que PCem ignore.
- **La correction.** Les adresses retenues, rangées et relues sous les quatre formes, FSETPM compris.

### PB-199 et PB-208 — l'image des registres et les étiquettes

- **Le vrai PC.** FSAVE range les huit registres en nombres de 80 bits, et une étiquette par registre dit s'il est
  vide, nul, ordinaire ou spécial (un infini, un NaN).
- **L'émulateur.** PCem range un entier chargé par FILD comme un entier suivi d'une marque, étiquette les infinis et
  les NaN comme des nombres ordinaires, et relit une étiquette « spéciale » comme son marqueur d'entier.
- **La correction.** Les 80 bits, et les étiquettes tirées du contenu ; au rechargement, le 387 et le 486 les
  recalculent.

### PB-54 — les limites de la mémoire

- **Le vrai PC.** Chaque accès à la mémoire est contrôlé : un opérande qui sort de son segment, ou une écriture dans un
  segment protégé en lecture seule, déclenche une erreur au lieu d'écrire ailleurs.
- **L'émulateur.** PCem ne contrôle qu'une seule instruction du coprocesseur, FSTP en 64 bits : les autres lisent et
  écrivent au-delà de la limite, et dans un segment en lecture seule.
- **La correction.** Le contrôle de la limite et des droits, à chaque accès ; les instructions de calcul l'auront avec
  leur réécriture.

## G13.6, le noyau, suite — calculer et comparer

*Le 11 octobre 2026, écrite avec son commit ; `PLAN-G13.md` § G13.6, le noyau, `VERIFICATION.md` § G13.6, le noyau,
N4a.*

Le noyau calcule : les quatre opérations, la racine carrée et les comparaisons, sur 80 bits. Toujours en chantier :
seuls les tests le demandent. Un contrôle neuf vérifie qu'il ne s'écarte pas de PCem là où PCem calcule juste : sur
des nombres ordinaires, arrondis au plus près à 53 bits, les deux rendent le même résultat au bit près.

### PB-48 — le sens de l'arrondi

- **Le vrai PC.** Le programme choisit comment arrondir un résultat inexact : au plus près, vers le haut, vers le bas
  ou vers zéro, et avec combien de bits (24, 53 ou 64). Chaque opération obéit. L'arithmétique d'intervalles et
  certaines bibliothèques mathématiques en dépendent.
- **L'émulateur.** PCem n'arrondit dans le sens demandé que pour une seule instruction, l'addition avec un opérande en
  mémoire ; les autres arrondissent toujours au plus près, et toutes calculent avec 53 bits.
- **La correction.** Chaque opération suit le sens et la précision demandés.

### PB-60 — le NaN qui survit

- **Le vrai PC.** Quand les deux opérandes sont des « NaN » (des valeurs « pas un nombre »), le coprocesseur garde
  celui qui porte la plus grande valeur, selon une règle fixe.
- **L'émulateur.** Le NaN qui survit dépend de l'ordre dans lequel le compilateur de PCem a rangé les opérandes,
  instruction par instruction.
- **La correction.** La règle du coprocesseur, la même pour toutes les instructions.

### Les exceptions et les comparaisons

- **Le vrai PC.** Chaque opération signale ce qui s'est mal passé : une opération impossible (la racine d'un négatif),
  une division par zéro, un résultat trop grand, trop petit ou arrondi. Une comparaison avec un NaN rend « non
  ordonné », et sur le 8087 et le 287, l'infini n'a pas de signe.
- **L'émulateur.** PCem ne signale que la division par zéro. Ses comparaisons ont déjà été corrigées (G13.6a) ; le noyau
  les reprend.
- **La correction.** Les six signaux, posés par le noyau dans l'ordre que décrit Intel.

## G13.6, le noyau, suite — arrondir, mettre à l'échelle, décomposer, prendre un reste

*Le 11 octobre 2026, écrite avec son commit ; `PLAN-G13.md` § G13.6, le noyau, `VERIFICATION.md` § G13.6, le noyau,
N4b.*

Cinq instructions qui servent aux bibliothèques mathématiques : arrondir à l'entier, multiplier par une puissance de
deux, séparer un nombre en exposant et mantisse, et calculer un reste. Toujours en chantier : seuls les tests le
demandent.

### PB-65 — le reste d'une division

- **Le vrai PC.** FPREM calcule le reste exact d'une division ; quand les deux nombres sont très éloignés, il procède
  par étapes et le signale, pour que le programme recommence. FPREM1 donne le reste au sens de la norme IEEE, avec un
  quotient arrondi au plus près. Les fonctions sinus et cosinus écrites à la main s'en servent pour ramener un angle
  dans un petit intervalle.
- **L'émulateur.** PCem divise en double, tronque, et soustrait : le quotient est faux au-delà de quinze chiffres, le
  reste aussi, il ne signale jamais une réduction incomplète, et FPREM1 fait la même chose que FPREM.
- **La correction.** Le reste exact, par étapes au-delà de 63 bits d'écart, et FPREM1 au plus près.

### PB-194 — séparer l'exposant et la mantisse

- **Le vrai PC.** FXTRACT range l'exposant et la mantisse d'un nombre dans deux registres ; les conversions en texte et
  les calculs de puissances s'en servent.
- **L'émulateur.** PCem ne connaît pas l'instruction : elle ne fait rien, la pile ne bouge pas, et le programme
  calcule la suite avec un registre de moins que prévu.
- **La correction.** L'instruction.

### PB-197, PB-211 et PB-265 — arrondir à l'entier

- **Le vrai PC.** FRNDINT arrondit à l'entier ; un nombre déjà entier, si grand soit-il, reste tel quel, et un résultat
  nul garde son signe.
- **L'émulateur.** PCem passe par un entier de 64 bits : au-delà, et pour un infini, il rend un nombre négatif énorme ;
  −0,3 devient +0 au lieu de −0. Et son arrondi au plus près se trompe lui-même sur certains nombres : un négatif
  minuscule devient +1, et un nombre juste sous 2 devient 1. Ce dernier défaut, nouveau, a été trouvé par la
  comparaison du noyau à PCem ; il touche aussi FIST, qui range un entier.
- **La correction.** L'arrondi sur 80 bits, signe compris.

### PB-198 — multiplier par une puissance de deux

- **Le vrai PC.** FSCALE multiplie par deux à la puissance du second registre ; un « pas un nombre » ou un infini en
  facteur suivent des règles fixes.
- **L'émulateur.** PCem rend zéro dans ces cas.
- **La correction.** Les règles du coprocesseur.

## G13.6, le noyau, suite — les erreurs que le programme veut voir

*Le 11 octobre 2026, écrite avec son commit ; `PLAN-G13.md` § G13.6, le noyau, `VERIFICATION.md` § G13.6, le noyau,
N5.*

Un programme peut demander au coprocesseur de l'interrompre à chaque erreur de calcul au lieu de la corriger en
silence : il « démasque » l'erreur. Toujours en chantier : seuls les tests le demandent.

### PB-59 — les erreurs démasquées

- **Le vrai PC.** Six erreurs se démasquent une à une. Celles qui se voient avant le calcul (une pile vide, une
  opération impossible, un nombre trop petit en entrée, une division par zéro) arrêtent l'instruction sans rien
  écrire. Celles qui se voient après (un résultat trop grand, trop petit, ou arrondi) laissent le résultat en place :
  trop grand ou trop petit, il est rangé avec son exposant ramené au milieu de la plage, pour que le programme puisse
  le corriger et continuer ; vers la mémoire, un résultat trop grand ou trop petit n'est pas écrit et le nombre reste
  sur la pile, un résultat seulement arrondi est écrit. Un nombre minuscule
  chargé depuis la mémoire est empilé quand même.
- **L'émulateur.** PCem ne connaît que la division par zéro ; les autres erreurs n'arrêtent jamais rien.
- **La correction.** Chaque erreur, avec sa réponse : l'arrêt, ou le résultat rangé à l'exposant ramené. La
  bibliothèque de calcul, qui ne sait pas faire ce dernier arrondi, gagne un réglage pour le faire, vérifié sur vingt
  millions de cas contre son calcul ordinaire.

## G13.6, le noyau, suite — sinus, logarithmes et exponentielles

*Le 12 octobre 2026, écrite avec son commit ; `PLAN-G13.md` § G13.6, le noyau, `VERIFICATION.md` § G13.6, le noyau,
N6.*

Le coprocesseur sait calculer seul un sinus, un cosinus, une tangente, un arc tangente, un logarithme et une
exponentielle. Toujours en chantier : seuls les tests le demandent.

### PB-68 — les transcendantes

- **Le vrai PC.** Le 387 et le 486 calculent ces fonctions sur leurs 64 bits de précision ; Intel promet, pour ses
  processeurs suivants, moins d'une unité d'erreur sur le dernier bit. Pour un sinus, le 387 ramène d'abord l'angle
  entre −45° et +45° en retirant des quarts de tour, mesurés avec sa propre valeur de π, gravée dans la puce avec 66
  bits utiles ; le 486 fait de même, selon toute vraisemblance. Au-delà de 2^63, ils ne calculent rien : ils laissent le
  nombre et lèvent un drapeau, C2, que le programme lit pour réduire l'angle lui-même. 2^x − 1 et log₂(1 + x) existent pour les x
  minuscules : ils gardent toute leur précision là où 2^x et 1 + x se confondraient avec 1.
- **L'émulateur.** PCem demande ces fonctions à la bibliothèque mathématique de l'ordinateur hôte, en 53 bits : le
  résultat dépend de l'hôte, C2 n'est jamais levé, et 2^x − 1 d'un x minuscule rend 0. Un logarithme d'un nombre négatif
  rend un « pas un nombre » sans signaler l'erreur.
- **La correction.** Chaque fonction est écrite dans l'émulateur, sans rien demander à l'hôte : un calcul sur 128 bits,
  arrondi une seule fois aux 64 bits du coprocesseur, l'angle réduit avec le π de 66 bits du silicium, C2 au-delà de
  2^63, les cas particuliers de la documentation d'Intel. Un outil à part, qui calcule en précision arbitraire, en
  fournit les attendus ; les deux s'accordent au bit près sur près de dix millions de cas tirés au hasard.

FSIN, FCOS et FSINCOS n'existent pas sur le 8087 ni sur le 287 ; PCem les y exécute en 0 cycle. C'est un autre
défaut, PB-266, qui sera corrigé avec les coprocesseurs de 16 bits.

## G13.6, le noyau, suite — le 8087 et le 287, les anciens

*Le 12 octobre 2026, écrite avec son commit ; `PLAN-G13.md` § G13.6, le noyau, `VERIFICATION.md` § G13.6, le noyau,
N7a.*

Le 8087 (1980, aux côtés du 8088 du PC et de l'XT) et le 287 (1983, aux côtés du 286 de l'AT) sont antérieurs à la
norme du calcul flottant que le 387 suivra. Intel a publié la liste de ce qui les sépare du 387 ; PCem les traite
comme un 387. Toujours en chantier : seuls les tests le demandent. Ce que les anciens font de leurs nombres « non
normaux » viendra à l'étape suivante.

### PB-202 — l'initialisation qui garde les codes

- **Le vrai PC.** Quatre bits du mot d'état, C3 à C0, gardent le résultat de la dernière comparaison. Sur le 8087 et
  le 287, l'initialisation (FINIT, et la sauvegarde FSAVE qui la suit, et même le reset de la machine) les laisse en
  place ; le 387 les efface.
- **L'émulateur.** PCem les efface sur tous les coprocesseurs.
- **La correction.** Le noyau les garde sur le 8087 et le 287.

### PB-200 — FNSTSW AX sur le 8087

- **Le vrai PC.** Le 287 a appris à copier son mot d'état directement dans le registre AX du processeur ; le 8087 ne
  sait pas le faire : il ne parle au 8088 qu'à travers la mémoire. Un programme peut s'en servir pour savoir lequel des
  deux il a.
- **L'émulateur.** PCem écrit AX sur le 8087 aussi : le programme croit trouver un 287.
- **La correction.** Sur le 8087, AX reste tel quel.

### PB-201 — l'adresse que le 8087 incrémente

- **Le vrai PC.** Pour lire un nombre en mémoire, le 8088 lit le premier mot et le 8087 relève au passage son adresse
  physique ; il lit seul les mots suivants, à l'adresse suivante, sans connaître les segments du 8088. Un nombre posé à
  cheval sur la fin d'un segment se lit donc au-delà, et pas au début du segment.
- **L'émulateur.** PCem relit le début du segment en lecture, mais écrit au-delà : écrire un nombre puis le relire ne
  rend pas toujours le même.
- **La correction.** Les lectures et les écritures du 8087 suivent l'adresse physique, comme sur le silicium.

### Le reste de la liste

Le noyau suit aussi les autres différences qu'Intel décrit : tout « pas un nombre » est une erreur pour le 8087 et le
287 ; leurs arrondis de résultats trop grands ou trop petits, et l'ordre de leurs erreurs ; leurs fonctions
trigonométriques et logarithmiques, qui ne vérifient pas leurs opérandes ; FXTRACT, FSCALE et FPREM sur leurs cas
particuliers ; FENI et FDISI, propres au 8087. Ce que le silicium fait des instructions du 387 qu'ils n'ont pas
(FSIN, FUCOM…) n'est documenté nulle part : elles restent celles du 387, comme chez PCem.

## G13.6, le noyau, suite — les nombres « non normaux » des anciens

*Le 12 octobre 2026, écrite avec son commit ; `PLAN-G13.md` § G13.6, le noyau, `VERIFICATION.md` § G13.6, le noyau,
N7b.*

Toujours en chantier : seuls les tests le demandent.

### PB-59 — les non normaux, et PB-70 — l'infini sans signe

- **Le vrai PC.** Un nombre flottant s'écrit d'ordinaire « normalisé », son premier chiffre binaire à 1. Le 8087 et le
  287 acceptent aussi des nombres dont les premiers chiffres sont des zéros, les « non normaux » : un nombre minuscule
  lu en mémoire devient l'un d'eux, et le calcul les garde tels quels tant que leur imprécision compte, pour ne jamais
  faire passer un résultat douteux pour un résultat exact. Diviser par l'un d'eux, en prendre la racine ou le convertir
  en entier est une erreur. Le 387 a abandonné ces nombres. Les deux anciens connaissent aussi un infini sans signe,
  le mode « projectif » de leur démarrage : ∞ + ∞ y est une erreur, et la racine de l'infini aussi.
- **L'émulateur.** PCem calcule en 53 bits normalisés : il ne connaît ni les non normaux ni l'infini sans signe.
- **La correction.** Le noyau suit les tables d'Intel : le chargement en non normal, le calcul qui le garde, les
  erreurs, l'infini projectif. Le détail du circuit n'étant décrit nulle part, le calcul range le résultat exact à
  l'exposant que la documentation lui prête ; le dernier bit peut différer du silicium, et les tests le disent.

## G13.6, le noyau, fin du chantier — le coprocesseur de 80 bits pour tous

*Le 12 octobre 2026, écrite avec son commit ; `PLAN-G13.md` § G13.6, le noyau, `VERIFICATION.md` § G13.6, le noyau,
N7c.*

Le noyau de 80 bits était en chantier : seuls les tests pouvaient le demander. Il calcule désormais tout ce que le
coprocesseur sait faire, du 8087 au 486. Il entre donc dans le mode matériel : quiconque demande ce mode, ou son domaine
x87, obtient le calcul du vrai coprocesseur, ses 80 bits, ses arrondis, ses erreurs et leurs signaux, au lieu de celui
de PCem en 53 bits. Les premières corrections du x87, écrites en 53 bits avant le noyau, sont reprises par lui ; elles
disparaîtront du code à la clôture. Les quelques codes d'instruction qu'Intel n'a jamais documentés et que PCem traite
comme des riens le restent : ce qu'en fait le silicium n'est pas connu.

## G13.6, le noyau, suite — les premières corrections du x87 s'effacent

*Le 12 octobre 2026, écrite avec son commit ; `PLAN-G13.md` § G13.6, le noyau, `VERIFICATION.md` § G13.6, le noyau,
N8a.*

Avant le noyau, une dizaine de défauts du x87 avaient été corrigés en 53 bits, à la manière de PCem : la comparaison
des infinis du 287, FXAM, les constantes, le mot d'état… Le noyau les refait sur 80 bits depuis qu'il est au mode. Ces
premières corrections, devenues inutiles, quittent le code ; leurs tests restent, et vérifient désormais le noyau.

## G13.7a — la disquette et le disque MFM

*Le 12 octobre 2026, écrite avec son commit ; `PLAN-G13.md` § G13.7, `VERIFICATION.md` § G13.7a.*

Le stockage des premières machines : le contrôleur de disquette de tous les PC, l'IBM Fixed Disk Adapter de l'XT (une
carte conçue par Xebec) et le contrôleur de disque de l'AT. Julien a tranché le 12 octobre : en mode matériel, on fait
ce que fait le vrai matériel, partout où sa documentation le décrit.

### PB-14 — la disquette à 300 kbit/s

- **Le vrai PC.** L'AT lit une disquette de 360 Ko dans son lecteur de 1,2 Mo à 300 000 bits par seconde : un octet
  passe toutes les 26,67 µs.
- **L'émulateur.** Une instruction `break` oubliée fait retomber ce débit sur celui de 250 kbit/s : 32 µs par octet,
  la disquette tourne 20 % trop lentement. Les données restent justes.
- **La correction.** L'octet dure 80/3 µs, à la fraction près.

### PB-19 — l'octet que le programme n'a pas fourni à temps

- **Le vrai PC.** Quand un programme écrit sur la disquette sans passer par le DMA, il doit donner chaque octet au
  contrôleur avant que la tête ne l'atteigne sur la piste. S'il est en retard, le contrôleur arrête la commande et le
  signale (« overrun »).
- **L'émulateur.** La détection existe chez PCem, mais l'indicateur qu'elle teste n'est jamais posé : l'octet périmé
  est écrit sans rien dire.
- **La correction.** Un octet demandé et non fourni arrête la commande, comme sur le contrôleur NEC.

### PB-22, PB-23, PB-25 et PB-217 — le contrôleur de l'XT

- **Le vrai PC.** Le manuel d'IBM décrit la carte octet par octet : FORMAT TRACK formate la piste désignée depuis son
  début ; l'octet de fin de chaque commande dit quelle unité elle visait ; une tête au-delà de la dernière est une
  adresse illégale ; après une erreur, le « sense » rend l'adresse où elle s'est produite.
- **L'émulateur.** PCem formate à partir du secteur laissé par la commande précédente, déborde sur la piste suivante
  et laisse le début de la piste visée intact ; il oublie l'unité dans l'octet de fin ; il lit la tête qui n'existe
  pas sur le cylindre suivant, sans erreur (le contrôleur de l'AT a le même défaut) ; son sense n'a jamais l'adresse.
- **La correction.** Chacun des quatre comme le manuel le décrit.

### PB-267 — les arrêts du contrôleur de l'XT

- **Le vrai PC.** Un programme maladroit, ou un utilitaire de bas niveau, ne fait pas tomber l'ordinateur : au pire,
  la carte rend une erreur.
- **L'émulateur.** PCem s'arrête net dans une vingtaine de situations que le programme peut provoquer : lire ou
  écrire la carte à contretemps, la relancer pendant une commande, couper son DMA en cours de transfert, lire un second
  disque absent, ou lui envoyer une commande qu'il n'a pas programmée — dont sept que la carte connaît, comme FORMAT
  DRIVE ou le diagnostic de l'unité.
- **La correction**, dans les deux modes (l'émulateur ne doit jamais tomber) : l'accès à contretemps est ignoré ; le
  transfert sans DMA se finit octet par octet ; le disque absent répond « pas prêt » ; les commandes de la carte qui se
  décrivent entièrement sont servies, et les autres rendent l'erreur « commande invalide » du manuel.

### Ce qui reste comme chez PCem

READ LONG et WRITE LONG, sur l'XT comme sur l'AT, lisent et écrivent les 4 octets de code correcteur (l'ECC) que le
contrôleur calcule pour chaque secteur. Le polynôme est publié, mais pas la manière exacte de le calculer : ces octets
seraient inventés. Ces deux commandes restent refusées. La commande propre à la carte DTC (PB-28) et la commande
envoyée à un second disque absent de l'AT (PB-75) restent aussi en l'état : personne ne sait ce que fait le matériel.

## G13.7b — le disque IDE

*Le 12 octobre 2026, écrite avec son commit ; `PLAN-G13.md` § G13.7, `VERIFICATION.md` § G13.7b.*

### PB-71 et PB-216 — l'interruption du disque

- **Le vrai PC.** Quand le disque IDE a fini une commande, il lève un fil d'interruption et le garde levé jusqu'à ce
  que le programme lise son état. Le contrôleur d'interruptions de l'AT ne réagit qu'au moment où le fil monte : une
  interruption, une seule. Chaque canal a son fil, l'IRQ 14 pour le premier, l'IRQ 15 pour le second.
- **L'émulateur.** Chaque fois que le programme choisit un disque ou touche au registre de contrôle, PCem recalcule le
  fil à sa manière : il peut retirer une interruption encore due — elle est perdue —, ou en poser une seconde après
  qu'elle a été servie. Et pour le second canal, il regarde le fil du premier.
- **La correction.** Chaque fil est suivi comme un vrai fil : levé tant que le disque choisi a une interruption en
  attente et ne l'a pas masquée, une seule interruption à la montée ; un reset du disque, logiciel ou matériel, efface
  l'interruption en attente.

### PB-74 — VERIFY

- **Le vrai PC.** La commande VERIFY relit sur le disque le nombre de secteurs demandé, sans rien transférer, et rend
  à la fin l'adresse du dernier secteur vérifié et un compte nul ; un secteur introuvable l'arrête en erreur.
- **L'émulateur.** PCem répond « fait » tout de suite, comme si un seul secteur avait été vérifié, et laisse l'adresse
  et le compte tels quels ; même au-delà de la fin du disque, sans erreur.
- **La correction.** Tous les secteurs sont parcourus, l'adresse et le compte sont ceux de la norme ATA-1, et un
  secteur au-delà du disque rend l'erreur « secteur introuvable ».

### Ce qui reste comme chez PCem

PB-72 — ce qu'un disque fait d'une écriture dans ses registres pendant qu'il se réinitialise — n'est pas défini par
la norme de 1993, et celle de 1997 se contredit : il reste en l'état, comme Julien l'a décidé (Q6). PB-73, la commande
multiple sans taille de bloc, rendait déjà l'erreur du vrai disque.

## G13.7c1 — le moteur d'images de CD

*Le 12 octobre 2026, écrite avec son commit ; `PLAN-G13.md` § G13.7, `VERIFICATION.md` § G13.7c1.*

Un lecteur de CD émulé ne lit pas un disque : il lit un fichier qui en tient lieu, une image. Le moteur d'images
traduit chaque secteur demandé en une position dans ce fichier. Quand il se trompe, le programme lit autre chose que
ce que le vrai lecteur aurait lu du vrai disque.

### PB-106 — le secteur de 2 336 octets

- **Le vrai PC.** Certains CD (les CD-ROM XA) rangent leurs données en « mode 2 » ; une image peut garder chaque
  secteur entier (2 352 octets) ou sans ses 16 premiers octets de synchronisation et d'en-tête (2 336). Dans le
  second cas, les données commencent 8 octets après le début du secteur.
- **L'émulateur.** PCem saute 24 octets dans les deux cas : une image de 2 336 octets n'est jamais reconnue, et une
  telle piste dans une feuille CUE se lit 16 octets trop loin.
- **La correction.** 8 octets pour un secteur de 2 336.

### PB-108 — où commence une piste dans son fichier

- **Le vrai PC.** Une feuille CUE décrit un disque à partir d'un ou plusieurs fichiers ; ses positions INDEX se
  comptent depuis le début du fichier. Une piste dont le fichier commence par le silence qui la précède (son
  « prégap », INDEX 00) commence vraiment à son INDEX 01.
- **L'émulateur.** PCem ne retranche le prégap que s'il ne commence pas au tout début du fichier ; dans la disposition
  la plus courante, un fichier par piste, chaque piste commence donc par son prégap, lu à la place de la musique ou
  des données.
- **La correction.** Une piste qui ouvre son fichier y commence à son INDEX 01.

### PB-109 — l'échec qui colle

- **Le vrai PC.** Un lecteur qui ne peut pas lire un secteur le signale, puis lit le suivant.
- **L'émulateur.** Une lecture qui atteint la fin du fichier laisse le fichier en échec dans la bibliothèque C++ de
  PCem : plus rien ne s'y lit, sur toutes les pistes qui le partagent, jusqu'à ce que l'image soit rechargée.
- **La correction.** Chaque lecture repart d'un fichier sain : seul le secteur fautif échoue.

### PB-122 — la fin du disque dans la table des matières

- **Le vrai PC.** La table des matières du disque (TOC) se termine par une entrée qui marque la fin des pistes, le
  « lead-out » ; comme les pistes, elle porte un octet qui dit le type d'information et la nature de la dernière
  piste (données ou audio).
- **L'émulateur.** PCem met cet octet à zéro.
- **La correction.** Le lead-out reprend l'octet de la dernière piste.

## G13.7c2 — les commandes du lecteur de CD

*Le 12 octobre 2026, écrite avec son commit ; `PLAN-G13.md` § G13.7, `VERIFICATION.md` § G13.7c2.*

Le programme parle au lecteur de CD par des commandes normalisées (SFF-8020i, 1996) : combien de secteurs a le
disque, quelle est sa table des matières, quels réglages appliquer, lire tels secteurs. PCem répond de travers à cinq
d'entre elles.

### PB-117 — la taille du disque

- **Le vrai PC.** À la question « quelle est la capacité ? », le lecteur répond par le numéro du dernier secteur
  lisible : 31 pour un disque de 32 secteurs.
- **L'émulateur.** PCem répond 33, deux de trop ; un pilote qui lit le dernier secteur annoncé reçoit une erreur.
- **La correction.** Le numéro du dernier secteur.

### PB-119 — MODE SELECT, les réglages du lecteur

- **Le vrai PC.** Le programme envoie au lecteur une liste de réglages (le volume de la musique, par exemple),
  précédée d'un en-tête dont la taille dépend de la forme de la commande. Une liste vide n'est pas une erreur ; une
  liste coupée, ou un réglage inconnu, est refusée en entier, sans rien changer.
- **L'émulateur.** PCem lit l'en-tête de travers et peut sauter les réglages ; une liste vide le laisse attendre des
  données qui ne viennent jamais ; il applique les réglages qui précèdent une faute, et ne voit pas une liste coupée.
- **La correction.** L'en-tête lu selon la forme de la commande ; la liste vide finie aussitôt ; la liste vérifiée
  entière avant d'appliquer quoi que ce soit, avec les codes d'erreur de la norme.

### PB-120 — la table des matières « brute »

- **Le vrai PC.** Sous sa forme brute, la table des matières commence par trois entrées : la première piste (et le
  type du disque), la dernière, et le début de la zone de fin ; puis une entrée par piste, et sa longueur en tête.
- **L'émulateur.** PCem ne rend que les pistes, sans ces trois entrées ni la longueur, et range le numéro de piste à
  la mauvaise place.
- **La correction.** La table de la norme.

### PB-268 — une lecture qui sort du disque

- **Le vrai PC.** Un programme qui demande des secteurs au-delà de la fin du disque reçoit une erreur, « adresse hors
  du disque », et aucune donnée.
- **L'émulateur.** PCem lit tant qu'il peut, puis signale l'erreur, mais continue de servir des données périmées
  jusqu'au compte demandé ; pour une très grande demande, son compte déborde ; et une lecture « brute » au-delà du
  disque répond même « réussi », avec des zéros.
- **La correction.** La demande est vérifiée d'emblée, et refusée.

### PB-269 — MECHANISM STATUS

- **Le vrai PC.** Cette commande rend l'état du mécanisme dans un en-tête de huit octets ; un programme qui en demande
  moins reçoit une erreur — c'est la seule commande où la norme l'exige.
- **L'émulateur.** PCem lit la taille demandée sur un octet de trop, et sert un en-tête tronqué sans rien dire.
- **La correction.** La taille lue à sa place, et l'erreur de la norme.

### Ce qui reste comme chez PCem

PB-115 — des lectures que PCem coupait par sécurité. Les lectures normales dans le disque sont déjà complètes ; celles
qui en sortent sont PB-268. Rien d'autre n'y change.

## G13.7c3 — le jeu de commandes du lecteur de CD, selon son année

*Le 12 octobre 2026, écrite avec son commit ; `PLAN-G13.md` § G13.7, `VERIFICATION.md` § G13.7c3.*

PCem propose douze lecteurs de CD, d'un 4x de 1995-1996 à un 72x de 2000, mais tous répondent aux mêmes commandes.
Or la norme des lecteurs a changé entre-temps : en 1996 (SFF-8020i), un lecteur ne connaissait pas certaines
commandes que la norme suivante (MMC-2, 1999) a ajoutées. Julien a tranché : chaque modèle répond selon son année.

### PB-270 — les commandes que le lecteur n'a pas

- **Le vrai PC.** Un lecteur refuse, « commande inconnue », ce que sa norme ne prévoit pas : un lecteur de 1996 ne
  sait pas dire « un disque vient d'être inséré » (GET EVENT STATUS NOTIFICATION), et aucun lecteur ATAPI ne connaît
  les formes courtes de certaines commandes, réservées aux disques SCSI.
- **L'émulateur.** PCem répond à tout, avec n'importe quel modèle.
- **La correction.** Chaque modèle refuse ce que la norme de son année n'a pas ; les années des lecteurs les plus
  anciens et du plus récent sont documentées, les autres déduites de leur vitesse.

### PB-118 — « un disque vient d'être inséré »

- **Le vrai PC.** Un lecteur récent signale l'arrivée d'un disque une fois, puis plus rien tant que rien ne change.
- **L'émulateur.** PCem annonce un nouveau disque à chaque question — même lecteur vide —, avec une longueur mal
  écrite.
- **La correction.** Le signalement de la norme, une fois.

### PB-271 — lire par minutes, secondes et images

- **Le vrai PC.** Tout lecteur de CD sait lire des secteurs désignés par leur position en temps (READ CD MSF) ; la
  norme l'impose.
- **L'émulateur.** PCem n'a pas cette commande.
- **La correction.** Elle est servie, par la même voie que sa sœur adressée par numéro de secteur.

## G13.7c4 — la musique du CD

*Le 12 octobre 2026, écrite avec son commit ; `PLAN-G13.md` § G13.7, `VERIFICATION.md` § G13.7c4.*

Un CD audio se repère de deux façons : par un numéro de secteur, qui part de zéro au début de la musique, ou par un
temps (minutes, secondes, images), qui commence deux secondes plus tôt — ces deux secondes, 150 secteurs, précèdent
la première piste. Le lecteur convertit l'un en l'autre. PCem mélange les deux.

### PB-123 — jouer par numéro de secteur

- **Le vrai PC.** « Joue à partir du secteur 42 » commence la musique au temps 00:02:42 ; « où en es-tu ? » répond
  par la position sur le disque et dans la piste.
- **L'émulateur.** PCem prend le numéro de secteur pour un temps, sans les deux secondes : la musique part 150
  secteurs trop tôt, ou pas du tout ; et sa réponse compte ces deux secondes là où elles n'ont rien à faire.
- **La correction.** Les deux secondes ajoutées ou retirées où il faut.

### PB-214 et PB-215 — jouer par temps

- **Le vrai PC.** Avant de jouer à partir d'un temps, le lecteur vérifie que la piste est de la musique ; une piste
  de données est refusée.
- **L'émulateur.** PCem cherche la piste au mauvais endroit — sur un nombre fabriqué avec les chiffres du temps, ou
  150 secteurs trop loin — : il refuse de la musique, accepte des données, ou « joue » sans rien jouer.
- **La correction.** La piste est cherchée au secteur que désigne le temps.

### PB-272 et PB-273 — la plage à jouer

- **Le vrai PC.** Le programme donne au lecteur un début et une longueur ; le lecteur lit ces nombres en entier, et
  refuse une plage qui passe de la musique aux données.
- **L'émulateur.** PCem lit le début de l'une des commandes sur trois octets au lieu de quatre — une adresse hors du
  disque retombe sur une vraie piste —, et ne vérifie que le début de la plage : il joue des données comme du son.
- **La correction.** Les quatre octets, et la plage vérifiée d'un bout à l'autre.

## G13.7d1 — le lecteur ZIP

*Le 13 octobre 2026, écrite avec son commit ; `PLAN-G13.md` § G13.7, `VERIFICATION.md` § G13.7d1.*

### PB-125 — les gros transferts

- **Le vrai PC.** Un programme peut lire ou écrire d'un coup jusqu'à 65 535 secteurs, plus de 30 Mo.
- **L'émulateur.** PCem range un transfert dans deux réserves de 256 Ko ; au-delà, il écrit hors de sa mémoire —
  l'émulateur coupait le transfert, par sécurité, et rendait des zéros.
- **La correction.** Le transfert va jusqu'au bout. (En mode PCem, les deux réserves sont désormais côte à côte,
  comme dans le C : une lecture de 256 à 512 Ko y passe, comme chez PCem ; une écriture s'arrête à 256 Ko.)

### PB-126 — la fin du disque, et le reset

- **Le vrai PC.** Le lecteur annonce le numéro du dernier secteur, refuse de lire au-delà, ne fait rien d'une
  demande de longueur nulle, et garde son disque quand on réinitialise la machine.
- **L'émulateur.** PCem annonce un secteur de trop et le lit sans erreur, reste bloqué sur une demande vide, et perd
  le disque au reset.
- **La correction.** Le comportement de la norme ; après un reset, le lecteur signale qu'il a été réinitialisé,
  puis repart avec son disque.

### Ce qui reste comme chez PCem

PB-274 — la commande d'éjection : PCem éjecte quand la norme dit « arrêter le moteur », parce que les pilotes
d'Iomega l'envoient ainsi. Ce que faisait le vrai lecteur Iomega n'est pas connu : elle reste.

## G13.7d2a — le disque SCSI

*Le 13 octobre 2026, écrite avec son commit ; `PLAN-G13.md` § G13.7, `VERIFICATION.md` § G13.7d2a.*

### PB-128 — les gros transferts

- **Le vrai PC.** Le disque SCSI lit ou écrit d'un coup jusqu'à 65 535 secteurs ; la carte Adaptec transfère tout ce
  que le disque rend.
- **L'émulateur.** PCem range un transfert dans deux réserves de 256 Ko ; au-delà, il écrit hors de sa mémoire —
  l'émulateur coupait le transfert, par sécurité, et rendait des zéros.
- **La correction.** Le transfert va jusqu'au bout.

### PB-129 — la fin du disque

- **Le vrai PC.** Le disque annonce le numéro de son dernier secteur, et refuse toute lecture ou écriture au-delà
  avec une erreur précise (« adresse hors du disque »).
- **L'émulateur.** PCem annonce un secteur de trop, et répond « tout va bien » à un accès hors du disque — une
  lecture rend le tampon d'avant, une écriture est perdue.
- **La correction.** Le dernier secteur, et le refus.

### PB-131 — les réponses du disque

- **Le vrai PC.** Après une erreur, le disque en donne la raison dans un format fixé par la norme, et l'oublie dès la
  commande suivante ; il se présente comme un disque SCSI-2 ; il distingue une commande inconnue d'un paramètre
  refusé.
- **L'émulateur.** PCem donne la raison dans un format incomplet, la garde indéfiniment, se dit d'une norme
  antérieure, et répond « unité absente » à toute commande qu'il refuse.
- **La correction.** Les réponses de la norme SCSI-2.

### PB-132 — les demandes vides

- **Le vrai PC.** Une commande qui demande zéro octet se termine aussitôt, sans erreur.
- **L'émulateur.** PCem attend des données qui ne viennent jamais : la carte reste bloquée, et tous les disques SCSI
  disparaissent jusqu'au redémarrage.
- **La correction.** La commande se termine aussitôt, côté disque. (Le côté de la carte, qui devrait se débloquer à
  son reset, vient avec la carte Adaptec, PB-276.)

### Ce qui reste comme chez PCem

PB-275 — la vérification, le formatage de bas niveau, les réglages envoyés au disque et ses diagnostics restent
simulés : ce qu'en faisait un disque de l'époque dépend de son fabricant, et n'est pas connu.

## G13.7d2b — les réglages du disque SCSI

*Le 13 octobre 2026, écrite avec son commit ; `PLAN-G13.md` § G13.7, `VERIFICATION.md` § G13.7d2b.*

### PB-130 — MODE SENSE

- **Le vrai PC.** Un programme peut demander au disque ses réglages — sa taille, sa géométrie — sous une forme fixée
  par la norme SCSI-2 ; le disque refuse une page de réglages qu'il ne connaît pas.
- **L'émulateur.** PCem rend un en-tête faux, une géométrie sans rapport avec la taille du disque, une page
  « PCEM » qu'aucun vrai disque n'a, et des zéros en trop ; il accepte n'importe quelle page.
- **La correction.** La forme de la norme, avec la géométrie de l'image — celle d'un disque virtuel est un choix,
  pris cohérent avec la taille qu'il annonce.

## G13.7e1a — la configuration de la carte Adaptec

*Le 13 octobre 2026, écrite avec son commit ; `PLAN-G13.md` § G13.7, `VERIFICATION.md` § G13.7e1a.*

### PB-144 — la carte neuve

- **Le vrai PC.** Une AHA-1542C sort d'usine avec l'ID 7, le canal DMA 5 et l'IRQ 11 ; ses réglages, une fois
  changés, restent dans une petite mémoire de la carte.
- **L'émulateur.** PCem part de réglages tirés de son propre fichier, DMA 7 et IRQ 10, et sans lui de zéros. Ce
  fichier est le même dans les deux modes : une carte réglée en mode matériel l'est aussi en mode PCem.
- **La correction.** Les réglages d'usine, et une mémoire de carte propre au mode matériel (Q8, validée par Julien).

### PB-218 — les réglages d'usine, à la demande

- **Le vrai PC.** Un programme peut demander à la carte ses réglages actuels, ou ceux d'usine.
- **L'émulateur.** PCem rend toujours les réglages actuels.
- **La correction.** Les réglages d'usine quand on les demande. (Le BIOS de la carte demande les réglages actuels : il
  ne voit pas de différence.)

### PB-140 — la description de la carte

- **Le vrai PC.** La carte décrit sa configuration sur 44 octets, dont la position de ses interrupteurs ; un
  programme peut en demander jusqu'à 256.
- **L'émulateur.** PCem coupe la description à 20 octets, et répond « rien » à une demande de 256.
- **La correction.** La description entière, avec les interrupteurs : le port et l'adresse de la ROM réglés, le
  lecteur de disquettes de la carte désactivé — celle de PCem n'en a pas.

### PB-277 — l'interruption qui change de ligne

- **Le vrai PC.** Quand la carte change de ligne d'interruption, l'ancienne ligne retombe.
- **L'émulateur.** PCem laisse l'ancienne ligne levée : une interruption fantôme pour le contrôleur d'interruptions.
- **La correction.** L'ancienne ligne retombe.

### Ce qui reste comme chez PCem

PB-136 — une demande d'écriture plus longue que la mémoire de la carte lit l'état interne de PCem : ce que fait la
vraie carte n'est pas documenté. PB-280 — une IRQ réglée hors des valeurs que propose Adaptec, et les temps de bus,
qu'un émulateur n'a pas. PB-278 et PB-279 (le bloc de commande, la commande privée du BIOS) viennent avec les étapes
suivantes de la carte.

## G13.7e1b — les boîtes aux lettres de la carte Adaptec

*Le 13 octobre 2026, écrite avec son commit ; `PLAN-G13.md` § G13.7, `VERIFICATION.md` § G13.7e1b.*

### PB-141 — les boîtes aux lettres

- **Le vrai PC.** Le pilote dépose ses demandes dans des « boîtes aux lettres » en mémoire, puis dit une fois à la carte
  d'aller voir : la carte les sert toutes, dans l'ordre d'un tourniquet, et dépose chaque réponse dans une boîte de
  retour libre — elle attend s'il n'y en a pas. Une boîte de taille nulle, ou un « va voir » sans boîtes déclarées, est
  refusé ; après un reset de la carte, il faut les déclarer à nouveau.
- **L'émulateur.** PCem ne sert qu'une demande par « va voir », toujours depuis la première boîte ; il écrase une
  réponse que le pilote n'a pas encore lue ; il accepte une taille nulle, et après un reset il garde le compte des
  « va voir » et repart tout seul.
- **La correction.** Le tourniquet, la boîte de retour libre attendue, les refus, et le reset qui oublie tout.

### PB-139 — l'abandon d'une demande

- **Le vrai PC.** Le pilote peut demander l'abandon d'une demande : la carte efface cette demande d'abandon, retire la
  demande visée si elle attend encore, et répond une fois — « abandonnée », ou « introuvable » si elle était déjà
  servie.
- **L'émulateur.** PCem efface la mauvaise boîte (il calcule son adresse avec un pas double), qui peut être une autre
  demande, perdue ; il répond « abandonnée » même quand il n'y a rien à abandonner, et recommence à chaque « va voir ».
- **La correction.** La bonne boîte, et une seule réponse juste.

### Ce qui reste comme chez PCem

La boîte aux lettres réservée au BIOS de la carte : son rapport avec la boîte normale n'est pas documenté ; seule est
corrigée l'évidence — elle n'écrase plus une demande en cours.

## G13.7e2a — les données et le bus SCSI de la carte Adaptec

*Le 13 octobre 2026, écrite avec son commit ; `PLAN-G13.md` § G13.7, `VERIFICATION.md` § G13.7e2a.*

### PB-276 — quand le disque a plus à dire que la place prévue

- **Le vrai PC.** Chaque demande du pilote dit combien d'octets il attend. Si le disque en envoie davantage, le manuel
  dit que la demande finit sans erreur : la carte laisse donc passer le surplus sans le ranger (déduit). Et un reset complet de la carte, ou le reset du
  bus qu'elle sait déclencher, remet tous les appareils SCSI au repos.
- **L'émulateur.** PCem, au bout de la place prévue, attend que le disque change de phase ; le disque attend qu'on
  prenne ses octets ; ni l'un ni l'autre ne cède, sans fin. Aucun reset de la carte ne touche le bus : le disque reste
  occupé, et tous les disques SCSI disparaissent jusqu'au redémarrage du PC.
- **La correction.** Le surplus passé (lu et jeté, ou des zéros envoyés en écriture), la demande achevée ; les deux
  resets qui libèrent le bus.

### PB-278 — les bits de sens, la mémoire de 16 Mo, l'adresse de la carte

- **Le vrai PC.** Deux bits de la demande, quand le pilote en pose un, arment le contrôle de longueur : un disque qui
  envoie trop, ou trop peu, rend une erreur « dépassement » (12h). Les deux à la fois veulent dire « aucun
  transfert » : ce que le disque envoie est ignoré. La carte, sur
  le bus ISA, n'a que 24 lignes d'adresse : au-delà de 16 Mo, elle revient à zéro. Et elle ne se sélectionne pas
  elle-même : l'adresse SCSI qu'elle porte n'est pas un disque.
- **L'émulateur.** PCem range les deux bits sans les lire, écrit au-delà de 16 Mo, et accepte de sélectionner l'adresse
  de la carte.
- **La correction.** Le contrôle et son erreur 12h, « aucun transfert », le bouclage à 16 Mo, l'adresse de la carte
  refusée.

### Ce qui reste comme chez PCem

Une demande en cours au moment d'un reset « doux » de la carte, ou d'un reset du bus : le manuel ne dit pas ce qu'elle
devient (PB-281). Et une demande posée à cheval sur la limite des 16 Mo, qu'aucun pilote ne pose là (PB-282).

## G13.7e2b — le numéro d'unité et le compte rendu d'erreur de la carte Adaptec

*Le 13 octobre 2026, écrite avec son commit ; `PLAN-G13.md` § G13.7, `VERIFICATION.md` § G13.7e2b.*

### PB-133 — l'unité derrière l'adresse SCSI

- **Le vrai PC.** Derrière chaque adresse SCSI, un appareil peut loger jusqu'à huit unités (LUN). Le pilote dit
  laquelle il veut dans sa demande ; la carte la transmet au disque par un message à part, et le disque, qui n'a que
  l'unité 0, répond aux autres selon la norme : à la question « qui es-tu ? » (INQUIRY), « personne ici » (7Fh) ; à
  tout le reste, une erreur « unité non prise en charge ».
- **L'émulateur.** PCem lit le numéro d'unité dans la demande, mais ne le transmet jamais : le disque répond présent
  sur ses huit unités, et un programme qui les balaie croit voir huit disques. Et quand l'unité arrive par l'autre
  voie, dans la commande elle-même, le disque refuse INQUIRY au lieu de dire « personne ici ».
- **La correction.** Le numéro de la demande transmis au disque (nos disques n'ayant pas de messages, il est posé dans
  la commande, où la norme dit que le disque l'ignore autrement — déduit) ; le disque répond aux unités absentes selon
  la norme. Le lecteur CD et le ZIP SCSI ont le même refus : noté à part (PB-283), laissé tel quel en G13.7e3.

### PB-137 — le compte rendu d'erreur automatique

- **Le vrai PC.** Quand un disque finit une demande en erreur (CHECK CONDITION), la carte lui demande aussitôt le
  détail de l'erreur (REQUEST SENSE) et le range à la fin de la demande du pilote, à l'endroit prévu pour lui.
- **L'émulateur.** PCem le demande après chaque commande, même réussie, et le range au mauvais endroit : pour une
  demande du pilote, dans une mémoire interne de la carte, jamais lue ; pour une commande du BIOS, à l'adresse d'une
  ancienne demande — à la mise sous tension, sur la table des vecteurs d'interruption.
- **La correction.** Le détail demandé après une erreur seulement, et rangé à la fin de la demande du pilote ; pour
  la commande du BIOS, dans la mémoire interne de la carte (déduit : le manuel ne dit rien de cette commande).

### Ce qui reste comme chez PCem

Le statut d'hôte 14h, « le compte rendu d'erreur a lui-même échoué », que le manuel décrit : chacun de nos appareils
répond toujours à cette demande, il ne peut pas arriver. Et la commande du BIOS qui suit une demande du pilote se prend
encore pour elle (PB-138, l'étape suivante).

## G13.7e3 — la commande du BIOS de la carte Adaptec

*Le 13 octobre 2026, écrite avec son commit ; `PLAN-G13.md` § G13.7, `VERIFICATION.md` § G13.7e3.*

### PB-138 — la commande du BIOS qui se prend pour une demande du pilote

- **Le vrai PC.** La carte a une commande réservée à son propre BIOS (03h) : lire, écrire ou vérifier des secteurs,
  lire la taille d'un disque. Le manuel ne la décrit pas plus ; mais c'est une commande à part des demandes que le
  pilote dépose dans la boîte aux lettres, et le BIOS de la carte a un code pour l'appareil qui ne répond pas :
  « délai dépassé » (80h).
- **L'émulateur.** PCem garde l'étiquette de la dernière demande du pilote : la commande du BIOS qui la suit croit
  achever cette demande, réécrit son compte rendu, libère sa case — une nouvelle demande que le pilote y aurait posée
  est perdue — et lui envoie un avis de fin. Si cette demande rangeait ses données par morceaux, selon une liste, la
  lecture du BIOS suit l'ancienne liste. Et sur un disque absent, il relit le résultat de la commande précédente :
  si elle avait réussi, le disque absent est « lu » sans erreur, et sa « taille » est celle de l'autre disque.
- **La correction.** La commande du BIOS n'est plus une demande du pilote ; un disque absent rend 80h (déduit : la
  commande n'est pas documentée, mais le code est celui du BIOS de la carte).

### PB-279 — le secteur 1 est le premier

- **Le vrai PC.** Dans les commandes de disque du BIOS, les secteurs d'une piste se comptent à partir de 1 ; la
  commande 03h de la carte en reprend le découpage en cylindre, tête et secteur (déduit, comme le reste de cette
  commande).
- **L'émulateur.** PCem prend le numéro tel quel : le secteur 1 devient le deuxième bloc du disque, et tout ce que la
  commande lit ou écrit est décalé d'un bloc.
- **La correction.** Le secteur compté à partir de 1 (déduit, la commande n'étant pas documentée). Le secteur 0, qui
  n'existe pas dans la convention, reste où PCem le met.

### Ce qui reste comme chez PCem

Le refus d'une unité absente par le lecteur CD et le ZIP (PB-283) : sous la carte Adaptec, iXtal26 ne monte ni l'un
ni l'autre sur le bus SCSI, et le même code sert les lecteurs ATAPI, dont la norme ne dit pas ce qu'ils font de ce
numéro.

## G13.8a — l'état du processeur de la Sound Blaster

*Le 13 octobre 2026, écrite avec son commit ; `PLAN-G13.md` § G13.8, `VERIFICATION.md` § G13.8a.*

### PB-236 — la carte qui s'allume, ou qu'on remet à zéro

- **Le vrai PC.** Le processeur de son de la carte (le DSP) démarre dans un état fixé par son programme interne :
  sortie coupée jusqu'à ce que le logiciel l'ouvre, cadence de 10 kHz, blocs de 2 048 octets entre deux
  interruptions. Le guide de Creative le dit (« returns it to its default state »), et les programmes internes
  désassemblés le montrent.
- **L'émulateur.** PCem ne coupe jamais la sortie, ni au démarrage ni après une remise à zéro ; il garde l'ancienne
  cadence ; et ses blocs font 65 536 octets : un programme qui ne fixe pas la taille attend son interruption trente-deux
  fois trop longtemps.
- **La correction.** L'état du programme interne, au démarrage et à chaque remise à zéro. La SB 16 n'a pas de coupure,
  et sa cadence au démarrage n'est pas connue : elle ne change que pour la taille des blocs. On entend la différence :
  la carte reste muette tant que le logiciel n'a pas ouvert sa sortie — une déduction : les programmes internes lus
  (2.02, 3.02) laissent la broche de coupure au repos après la remise à zéro, sans l'ouvrir ; ceux des premières
  cartes ne sont pas publiés.

### PB-241 — la grande vitesse

- **Le vrai PC.** Pour jouer vite, les DSP 2.01 à 3.xx ont un mode « grande vitesse » où ils ne lisent plus aucune
  commande (« the DSP will not accept any other commands ») ; on en sort à la fin du bloc d'une lecture simple, ou
  sinon par une remise à zéro, qui est alors « à chaud » : le programme interne retrouve sa marque et garde ses
  réglages. La SB 16 accepte les commandes en grande vitesse, mais sa remise à zéro y est « à chaud » aussi.
- **L'émulateur.** PCem continue d'exécuter les commandes, et sa remise à zéro efface tout, la taille des blocs
  comprise.
- **La correction.** En grande vitesse, rien n'est pris (sauf sur la SB 16) ; la remise à zéro qui en sort garde les
  réglages. Ce que la carte répond alors à la question « es-tu prête ? » n'est pas documenté : c'est celui de PCem.

### PB-240 — la prise MIDI de la carte

- **Le vrai PC.** La commande 38h envoie l'octet suivant à la prise MIDI ; 34h à 37h ouvrent un mode où tout ce que
  le logiciel écrit part à la prise, jusqu'à une remise à zéro (« à chaud », elle aussi).
- **L'émulateur.** PCem ignore 38h : l'octet de musique qui la suit est pris pour une commande. Un jeu réglé sur la
  « MIDI de la Sound Blaster » voit ses notes exécutées : 90h lance une lecture de son, 40h change la cadence…
- **La correction.** Les octets partent à la sortie MIDI. L'entrée MIDI reste comme chez PCem : il n'y a rien à y
  brancher.

### PB-165 — la question à la puce absente

- **Le vrai PC.** Sur la SB 16, la commande 08h interroge la puce de traitement optionnelle ; le programme interne du
  DSP 4.05 répond sans lire d'octet de plus.
- **L'émulateur.** PCem attend un octet de plus, et avale donc la commande suivante.
- **La correction.** 08h répond seule. Ce qu'elle répond dépend de la puce, qu'on n'a pas : la valeur reste celle de
  PCem.

### PB-147 — le haut-parleur de la première Sound Blaster

- **Le vrai PC.** Les commandes D1h et D3h relient et coupent la sortie du son numérique, de la 1.0 à la Pro (la
  SB 16 n'a pas cette coupure) ; sur la 1.0, elles suspendent aussi la lecture en cours.
- **L'émulateur.** Sur la SB 1.0, PCem ne fait que la suspension : D3h ne coupe pas le son.
- **La correction.** D3h coupe et D1h rouvre, en plus de la suspension, que PCem faisait déjà comme la carte.

### Ce qui reste comme chez PCem

Ce que répond 08h, et l'entrée MIDI : sans la puce ni de source MIDI, rien ne dit ce que la carte ferait.

## G13.8b — le son compressé (l'ADPCM) de la Sound Blaster

*Le 13 octobre 2026, écrite avec son commit ; `PLAN-G13.md` § G13.8, `VERIFICATION.md` § G13.8b.*

L'ADPCM range deux, trois ou quatre échantillons par octet : chacun dit de combien monter ou descendre depuis le
précédent, et le pas s'agrandit ou rétrécit à mesure. Une erreur sur un échantillon décale donc tous les suivants.
Le programme interne du DSP fait ce travail ; on l'a lu dans quatre versions (2.02, 3.02, 4.05, 4.13), qui font la même
chose.

### PB-90 — la fin du compte prise pour une donnée

- **Le vrai PC.** Quand le contrôleur de DMA a servi son dernier octet, il le signale sur une broche à part ; l'octet,
  lui, arrive intact.
- **L'émulateur.** PCem colle ce signal dans l'octet. En ADPCM, l'échantillon suivant fait un saut vers le bas (de 60
  en 4 bits, de 35 en 2,6 bits), et le reste du son garde le décalage.
- **La correction.** L'octet seul arrive au DSP.

### PB-91 — le format 2 bits qui ne finit jamais

- **Le vrai PC.** En 2 bits comme dans les autres formats, le DSP compte les octets lus et lève son interruption à la
  fin du bloc.
- **L'émulateur.** PCem oublie de compter en 2 bits : le son ne s'arrête plus, et le programme attend une
  interruption qui ne vient pas.
- **La correction.** Le compte, comme le DSP.

### PB-146 — les lectures en boucle

- **Le vrai PC.** Les commandes de lecture et d'enregistrement en boucle (1Fh, 2Ch, 7Dh, 7Fh) n'ont pas de paramètre :
  la taille du bloc est celle fixée avant par 48h. Les trois en ADPCM lisent d'abord un octet de départ, la
  « référence ».
- **L'émulateur.** PCem prend comme taille les octets de la commande précédente, quels qu'ils soient, et décode la
  référence comme une donnée.
- **La correction.** La taille de 48h, la référence lue d'abord.

### PB-234 — le bloc, échantillon par échantillon

- **Le vrai PC.** Le DSP lève son interruption quand le dernier échantillon du bloc est sorti. Au départ d'un bloc
  sans référence, il tire trois échantillons du premier octet en 2 et en 4 bits (en 4 bits, il en rejoue un), deux en
  2,6 bits ; et à chaque nouveau bloc d'une lecture en boucle, il lit le premier octet sans en jouer d'échantillon
  dans ce même temps. Les quatre programmes internes ont ces mêmes nombres : c'est le comportement de la carte, pas une
  faute de lecture.
- **L'émulateur.** PCem finit le bloc dès qu'il en lit le dernier octet, avant de le jouer : ce dernier octet est
  perdu, et l'interruption arrive tôt. Il tire de chaque octet son nombre d'échantillons, sans les exceptions du
  départ.
- **La correction.** Le déroulé du programme interne, tic par tic. Deux modèles écrits à part, l'un d'après PCem,
  l'autre d'après le code machine du DSP, s'accordent avec le mode matériel sur 4 000 scénarios.

### PB-235 — l'octet qui ne vient pas

- **Le vrai PC.** Si le contrôleur de DMA ne sert pas l'octet demandé, le DSP l'attend.
- **L'émulateur.** PCem décode un octet fictif, le compte, et finit le bloc à l'heure, interruption comprise, sans
  avoir rien lu.
- **La correction.** Le DSP attend, puis reprend où il en était. Ce qu'il fait d'une commande reçue pendant
  l'attente n'est pas connu : comme chez PCem.

### PB-284 — deux tables de décodage

- **Le vrai PC.** Le DSP calcule chaque pas d'une seule formule ; en 2,6 bits, le troisième échantillon d'un octet
  n'a qu'un bit d'amplitude.
- **L'émulateur.** PCem lit ses pas dans des tables, justes en 4 bits, mais fausses à leur dernière rangée en 2,6 bits
  (5, 15, 25, 35 au lieu de 8, 24, 40, 56) et en 2 bits (6 au lieu de 16), et il compte double l'amplitude du
  troisième échantillon en 2,6 bits. Un son fort, en 2,6 bits, plafonne et suit mal les attaques.
- **La correction.** Les tables du DSP, et le troisième échantillon sur un bit. Les décodeurs corrigés rendent,
  octet pour octet, ce que rend le programme interne.

### Ce qui reste comme chez PCem

Une commande écrite pendant un bloc fait perdre un tic au vrai DSP, inaudible ; et le dépassement par le haut du
format 2 bits, que le programme interne calcule faux (une valeur indéfinie), reste borné à 255 comme chez PCem.
En stéréo, sur la SB Pro, l'alternance des voies reste celle de PCem : le programme interne n'en dit rien. Une
commande ADPCM reçue pendant un bloc, que le DSP met en file, vient avec PB-239, à l'étape suivante.

## G13.8c1 — les commandes de lecture et d'enregistrement du DSP

*Le 13 octobre 2026, écrite avec son commit ; `PLAN-G13.md` § G13.8, `VERIFICATION.md` § G13.8c1.*

### PB-149 — l'octet de mode de la SB 16

- **Le vrai PC.** Les commandes de la SB 16 (B0h à CFh) prennent un octet de mode dont seuls deux bits comptent :
  stéréo, et signé. Le programme interne ignore les autres.
- **L'émulateur.** PCem prend l'octet entier pour le format. Avec un bit réservé posé, il ne reconnaît plus rien : le
  transfert se fige, sans fin ni interruption ; avec 01h à 03h, il joue de l'ADPCM, que ces commandes n'ont pas.
- **La correction.** L'octet réduit à ses deux bits.

### PB-239 — la commande qui attend son tour

- **Le vrai PC.** Une commande de lecture reçue pendant un transfert ne l'interrompt pas : le DSP finit le bloc en
  cours, lève son interruption, puis enchaîne sur la nouvelle longueur, dans le format en cours. Le guide de Creative
  le dit pour sortir d'une lecture en boucle ; les DSP jusqu'au 3.xx le font pendant tout transfert qui n'est pas en
  pause, ceux de la SB 16 pendant une lecture en boucle seulement.
- **L'émulateur.** PCem relance sur-le-champ : le bloc en cours est coupé net, et l'interruption suivante vient trop
  tôt.
- **La correction.** La commande attend la fin du bloc. Une commande d'enregistrement reçue pendant une lecture
  (ou l'inverse), qui chez le DSP prendrait la file de l'autre sens, reste comme chez PCem.

## G13.8d1 — les mélangeurs de la Sound Blaster

*Le 13 octobre 2026, écrite avec son commit ; `PLAN-G13.md` § G13.8, `VERIFICATION.md` § G13.8d1.*

### PB-237 — les volumes au démarrage

- **Le vrai PC.** À chaque reset, le mélangeur pose les volumes que le guide de Creative documente : la voix à
  −46 dB sur la SB 2.0, la voix, le général et la MIDI à −11 dB sur la Pro, à −14 dB sur la SB 16. Au démarrage
  aussi, déduit : le guide ne parle que du reset.
- **L'émulateur.** PCem les relève exprès à 0 dB : le son est plus fort que sur la carte, et le CD, qui suit le
  volume général, aussi.
- **La correction.** Les volumes du guide. Le son est plus faible qu'en mode PCem : c'est celui de la carte, que le
  programme qui règle le mélangeur remonte comme sur le vrai PC.

### PB-145 — le CD de la SB 2.0 sans mélangeur

- **Le vrai PC.** La SB 2.0 simple n'a pas de mélangeur : rien n'y règle le volume du CD, qui passe tel quel
  (déduit : le guide ne dote de mélangeur que la version CD de la carte).
- **L'émulateur.** PCem remet quand même à zéro un mélangeur absent : le CD tombe presque au silence, et aucun port
  ne permet de le relever.
- **La correction.** Sans mélangeur, pas de reset : le CD reste à son plein volume, comme sur la SB 1.0 et la 1.5.

### PB-153 — la SB 16 : l'entrée au reset, et le 16 bits par le canal 8 bits

- **Le vrai PC.** Au reset, le mélangeur d'entrée de la SB 16 relie la ligne, le CD et le micro, pas la musique du
  synthétiseur. Et la carte sait jouer et enregistrer du 16 bits par un canal DMA 8 bits quand on ne lui donne aucun
  canal 16 bits (le registre 81h) : deux octets par échantillon, l'octet bas d'abord (déduit, l'ordre du PC).
- **L'émulateur.** PCem relie aussi la musique à l'entrée. Sans canal 16 bits, il garde l'ancien, que rien ne sert :
  le transfert ne démarre jamais.
- **La correction.** L'entrée du guide au reset, et la traduction vers le canal 8 bits. Le reste de PB-153 viendra
  avec l'interruption partagée (le MPU-401) et avec l'enregistrement ; l'interruption d'usine attend la réponse de
  Julien.
