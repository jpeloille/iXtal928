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
