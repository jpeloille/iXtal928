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
- **Ce qui reste.** Le temps. PCem facture le REP 20 cycles et vide la file ; devant IDIV, le seul cas que le corpus
  mesure, le 8088 le paie 3 à 4 cycles. Le mode matériel garde le prix de PCem : 24 cycles de trop, une décision à
  prendre.
