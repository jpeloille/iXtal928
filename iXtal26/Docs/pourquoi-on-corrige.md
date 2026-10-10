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
