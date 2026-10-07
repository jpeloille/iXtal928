# 01 — Le 8088 : l'architecture, la file d'attente, les cycles de bus

> Lu sur `dc03cf6`, le 8 octobre 2026. Sources : I1 (chapitre 2), I2 (fiche du 8088, dès la p. 7-25), T1 (section 1,
> et le listing du BIOS p. 5-34), R1.
>
> Une phrase suivie d'un renvoi à une source est *documentée*. Ce qui est lu dans le code se vérifie par son renvoi.
> Seuls les raisonnements portent une mention : *déduit*, ou *inconnu* quand rien ne permet de trancher.

Ce chapitre présente le processeur comme une machine qui lit, exécute et attend le bus. Il traite aussi l'état dans
lequel le reset le laisse. Les instructions font l'objet du chapitre 02, les interruptions et le pas à pas du
chapitre 03.

## 1. Le composant

### Deux unités qui travaillent en même temps

Le 8088 se divise en deux unités [I1 p. 2-4] :
- **l'unité d'exécution** (EU) exécute les instructions ;
- **l'unité d'interface de bus** (BIU) va chercher les instructions, lit les opérandes et écrit les résultats.

L'EU n'a pas accès au bus. Elle prend ses instructions dans une file d'attente que tient la BIU, et passe par elle pour
chaque lecture et chaque écriture. Les adresses qu'elle manipule ont 16 bits ; c'est la BIU qui les étend à tout le
mégaoctet [I1 p. 2-5]. Les deux unités travaillent en parallèle : pendant que l'EU exécute, la BIU charge les octets
qui suivent, si bien que la lecture des instructions ne coûte, la plupart du temps, aucun temps visible [I1 p. 2-4].

Sur le 8088, Intel nuance ce point : le processeur est aussi limité par la vitesse de ses chargements, quand il
enchaîne des opérations simples [I2 p. 7-38].

### La file d'attente

- Elle contient jusqu'à **quatre octets** sur le 8088, et jusqu'à six sur le 8086 [I1 p. 2-5, 2-6].
- La BIU du 8088 charge un octet dès qu'une place se libère dans la file, à condition que l'EU n'ait pas demandé le
  bus. Celle du 8086 attend que deux places soient libres [I1 p. 2-6]. Intel justifie cette file plus courte : sur un
  bus de huit bits, le préchargement accaparerait le bus [I2 p. 7-37].
- Quand l'EU exécute un saut, la BIU vide la file, charge l'instruction à la nouvelle adresse et la passe aussitôt à
  l'EU, puis recommence à remplir la file [I1 p. 2-6].
- Quand l'EU demande une lecture ou une écriture, la BIU suspend le préchargement. Un chargement déjà commencé va
  d'abord jusqu'à son terme [I1 p. 2-6].

### Les registres

- **Huit registres généraux** de 16 bits [I1 p. 2-6, fig. 2-7] :
  - AX, BX, CX et DX, dont on peut adresser séparément chaque moitié (AH et AL, etc.) ;
  - SP, BP, SI et DI.
- **Leurs emplois implicites.** Certaines instructions se servent d'un registre sans le nommer : CX compte les
  boucles et les chaînes, CL les décalages et rotations variables, DX porte l'adresse des E/S indirectes, SI et DI
  parcourent les chaînes [I1 p. 2-6, table 2-1].
- **Quatre registres de segment** : CS pour le code, SS pour la pile, DS pour les données, ES pour le segment
  supplémentaire [I1 p. 2-7].
- **Le pointeur d'instruction**, IP.
- **Les drapeaux** :
  - six drapeaux d'état, que l'EU met à jour après une opération : CF, PF, AF, ZF, SF et OF [I1 p. 2-7, fig. 2-9] ;
  - trois drapeaux de contrôle, que positionnent les programmes : DF fixe le sens des chaînes, IF autorise les
    interruptions externes masquables, TF met en pas à pas [I1 p. 2-8].

### Les adresses

- Une adresse physique a 20 bits. Elle va de 0 à FFFFFh, soit un mégaoctet [I1 p. 2-11].
- Un programme ne manipule que des adresses logiques, formées d'un segment et d'un déplacement [I1 p. 2-11].
- Pour obtenir l'adresse physique, la BIU décale le segment de quatre bits et lui ajoute le déplacement. Comme le
  déplacement reste sur 16 bits, l'adressage se fait modulo 64 Ko : une adresse qui dépasse la fin d'un segment
  revient au début du même segment [I1 p. 2-12].

### Le cycle de bus

- **Sa durée.** Un cycle de bus dure au moins quatre horloges, T1 à T4 :
  - l'adresse sort en T1 ;
  - T2 sert surtout à inverser le sens du bus pendant une lecture ;
  - la donnée passe en T3 et T4.

  Si le périphérique adressé n'est pas prêt, des horloges d'attente (Tw) s'insèrent entre T3 et T4 [I2 p. 7-34,
  7-35].
- **Le bus de huit bits.** Le bus de données du 8088 n'a que huit bits : lire ou écrire un mot coûte donc quatre
  horloges de plus [I2 p. 7-38]. Les registres internes et le résultat de chaque instruction sont les mêmes que sur le
  8086 [I2 p. 7-37].
- **Le mode maximum.** Si la broche MN/MX est reliée à la masse, le processeur passe en mode maximum. Il code alors
  l'état du bus sur trois lignes, que décode un contrôleur 8288 [I1 p. 2-8].
- **L'état de la file.** En mode maximum, deux lignes, QS0 et QS1, indiquent à chaque horloge ce que la file vient de
  faire. Un coprocesseur s'en sert pour suivre une instruction ESC dans la file et savoir quand, et si, elle s'exécute
  [I1 p. 2-29, table 2-5].

### Le reset

| Élément | Après un reset [I1 p. 2-29, table 2-4] |
|---|---|
| Drapeaux | à zéro |
| IP | 0000h |
| CS | FFFFh |
| DS, SS, ES | 0000h |
| File | vide |

La première instruction s'exécute donc en FFFF0h, où l'on place normalement un saut long vers le vrai début du
programme [I1 p. 2-29]. Intel ne dit pas ce que contiennent les registres généraux après un reset.

## 2. Dans le 5150

### Le processeur et sa vitesse

- La carte mère est bâtie autour d'un 8088 en mode maximum, « so a comicroprocessor can be added as a feature »
  [T1 p. 1-3].
- Il tourne à 4,77 MHz, c'est-à-dire le quartz de 14,31818 MHz divisé par 3 [T1 p. 1-3, 1-4]. Le chapitre 04 suit
  les horloges de la machine.

### Les cycles de bus

- Une horloge dure 210 ns. Un cycle de bus mémoire en prend quatre, soit 840 ns. Un cycle d'E/S en prend cinq, soit
  1,05 µs [T1 p. 1-4].
- Ces durées valent tant qu'aucun périphérique adressé n'active la ligne READY du canal pour allonger le cycle
  [T1 p. 1-15].

### Le rafraîchissement de la mémoire

- La mémoire dynamique doit être rafraîchie. Une voie du 8253 demande périodiquement un transfert DMA fictif, qui
  produit une lecture de mémoire [T1 p. 1-14]. Ce transfert passe par le canal 0 du 8237 [T1 p. 1-21].
- Il a lieu toutes les 72 horloges, soit environ 15 µs. Il dure quatre horloges et occupe, selon IBM, environ 7 % du
  bus [T1 p. 1-15]. Le calcul donne 4/72, soit 5,6 %.
- Le processeur perd ce temps de bus (*déduit*). Les chapitres 08 et 09 décrivent le mécanisme.

### Les premières instructions du BIOS

- En FFFF0h, le BIOS 10/27/82 place `JMP F000:E05B` [R1 F000:FFF0]. Sa date, « 10/27/82 », suit en F000:FFF5
  [R1 F000:FFF5].
- Sa première tâche est de vérifier le processeur : « verify 8088 flags, registers and conditional jumps »
  [T1 p. 5-34, l. 302-304]. Il procède ainsi :
  1. il masque les interruptions (`CLI`), puis met SF, ZF, AF, PF et CF à 1 par `MOV AH,0D5h` et `SAHF`
     [T1 p. 5-34, l. 310-312] ;
  2. il vérifie CF, ZF, PF et SF par `JNC`, `JNZ`, `JNP` et `JNS` [l. 313-316] ;
  3. il vérifie AF en le faisant passer dans CF : `LAHF`, `SHR AH,CL`, `JNC` [l. 317-320] ;
  4. il recommence l'épreuve avec OF, puis avec tous les drapeaux à zéro [l. 321 et suivantes].

  Au premier saut qui ne se comporte pas comme prévu, il s'arrête sur le `HLT` d'ERR01 [T1 p. 5-34]. Un émulateur
  dont les drapeaux sont faux s'arrête donc dès la première page du POST, sans rien afficher.

## 3. Le code

### Les fichiers

| Fichier | Rôle | Original de PCem |
|---|---|---|
| `Cpu/808x.cs` | l'interpréteur : les accès mémoire, la file, le temps, la boucle, le reset | `pcem-dev/src/cpu/808x.c` |
| `Cpu/x86.cs` | les registres et les drapeaux, sous les noms de PCem | `pcem-dev/includes/private/cpu/x86.h` |
| `Cpu/386_common.cs` | l'instance `cpu_state`, seule utile ici | `pcem-dev/src/cpu/386_common.c` |
| `Cpu/x86seg.cs` | le chargement des segments, dont le 5150 n'emploie que le mode réel | `pcem-dev/src/cpu/x86seg.c` |
| `Cpu/cpu.cs` | `cpu_set`, qui choisit le 8088 | `pcem-dev/src/cpu/cpu.c` |

`Cpu/cpu.cs#cpu_set` fait du processeur un 8088 en mettant `is8086` à 0 (`Cpu/808x.cs#is8086`). C'est cet indicateur
qui départage le 8088 et le 8086 partout dans le cœur. Par défaut, la machine prend la première ligne de
`Cpu/cpu_tables.cs#cpus_8088`, « 8088/4.77 », cadencée à 4 772 728 Hz.

### Les registres

- `Cpu/x86.cs#x86reg` est une union de 32, 16 et 8 bits, comme celle de `x86.h`.
- Les registres généraux sont `cpu_state.regs[0]` à `[7]`, dans l'ordre où les opcodes les codent : AX, CX, DX, BX,
  SP, BP, SI, DI. Des propriétés leur donnent leur nom, par exemple `Cpu/x86.cs#AX`.
- Les drapeaux occupent les bits définis par Intel, de `Cpu/x86.cs#C_FLAG` (bit 0, CF) à `Cpu/x86.cs#V_FLAG`
  (bit 11, OF). PCem nomme SF `N_FLAG` (`Cpu/x86.cs#N_FLAG`, bit 7).

### Les adresses

- **La base des segments.** `Cpu/x86seg.cs#loadcs` pour CS, et `Cpu/x86seg.cs#loadseg` pour DS, ES et SS, fixent la
  base du segment à `seg << 4` et sa limite à FFFFh : c'est le calcul d'Intel.
- **Le mégaoctet.** Sur un PC, `Cpu/808x.cs#resetx86` fixe `rammask` (`Memory/mem.cs#rammask`) à FFFFFh. Au-delà
  d'un mégaoctet, l'adresse revient donc à 0, comme sur un 8088 qui n'a que vingt lignes d'adresse (*déduit*).
- **IP.** Il est tenu sur 32 bits (`cpu_state.pc`) et n'est ramené à 16 bits qu'**à la fin** de chaque instruction,
  par `cpu_state.pc &= 0xFFFF`, dans `Cpu/808x.cs#execx86`. D'où PB-87.
- **Les mots.** `Cpu/808x.cs#readmemw` et `Cpu/808x.cs#writememw` accèdent aux deux octets d'un mot aux adresses
  linéaires `s + a` et `s + a + 1`. À l'offset FFFFh, l'octet haut tombe ainsi dans le segment suivant, au lieu de
  l'offset 0 du même segment. D'où PB-179.

### La file d'attente

Cinq variables reproduisent la BIU (`pcem-dev/src/cpu/808x.c:114-260`) :
- `Cpu/808x.cs#prefetchqueue` : six octets, dont le 8088 n'utilise que quatre ;
- `Cpu/808x.cs#prefetchw` : le nombre d'octets en file ;
- `Cpu/808x.cs#prefetchpc` : l'offset du prochain octet à charger ;
- `Cpu/808x.cs#fetchcycles` : les horloges de préchargement accumulées, plafonnées à 16. Elles valent 4 par octet en
  file, plus l'avance du chargement en cours, que portent les deux bits bas (`fetchcycles & 3`) ;
- `Cpu/808x.cs#fetchclocks` : les horloges que l'EU a passées à attendre un chargement pendant l'instruction en cours.

Quatre fonctions appliquent les règles d'Intel :

| La règle d'Intel | Le code |
|---|---|
| L'EU prend son octet dans la file ; si la file est vide, elle attend la fin d'un cycle de bus. | `Cpu/808x.cs#FETCH`. S'il y a un octet en file, il le retire (`fetchcycles -= 4`). Sinon, il retire de `cycles`, et ajoute à `fetchclocks`, les horloges qui manquent pour boucler un cycle de 4, puis lit l'octet directement. |
| La BIU charge un octet par cycle de bus libre, jusqu'à quatre octets. | `Cpu/808x.cs#FETCHADD` convertit des horloges libres en octets chargés, à raison d'un par tranche de 4, tant que la file en compte moins de quatre. On l'appelle pendant le calcul de l'adresse effective (`Cpu/808x.cs#fetcheal`), dans les boucles de REP, et à la fin de chaque instruction. |
| Un chargement commencé se termine avant l'accès de l'EU. | `Cpu/808x.cs#FETCHCOMPLETE` termine le cycle en cours. Son seul appelant est `Cpu/808x.cs#refreshread`. Malgré son nom, ce dernier est appelé par `Models/dma.cs#dma_channel_read` et `Models/dma.cs#dma_channel_write` à **chaque** transfert DMA d'une machine qui n'est pas un AT : le rafraîchissement sur le canal 0, mais aussi la disquette sur le canal 2. Voir « Les écarts ». |
| Un saut vide la file. | `Cpu/808x.cs#FETCHCLEAR`, appelé par chaque saut pris et chaque interruption (sauf INTO, absent du cœur : constat C3), ainsi que par HLT, les reprises de REP et les resets. Il pose aussi `memcycs = cycdiff - cycles` : à la fin de l'instruction, la BIU ne reçoit alors que les horloges écoulées après le saut. |

Sur un 8086, les mêmes fonctions utilisent six octets et chargent deux octets à la fois depuis une adresse paire : c'est
`is8086` qui fait la différence.

### Le temps d'une instruction

`Cpu/808x.cs#execx86` reçoit un budget d'horloges, `cycles` (`Cpu/x86.cs#cycles`), que `pc.cs#runpc` lui donne à
chaque tranche de 10 ms (chapitre 21). Pour chaque instruction :

1. **Le départ.** `cycdiff` (`Cpu/808x.cs#cycdiff`) retient le budget restant. Le code retire ensuite `nextcyc`
   (`Cpu/808x.cs#nextcyc`), l'attente qu'a laissée un `FETCHCOMPLETE` sur file vide.
2. **L'exécution.** `FETCH` lit l'opcode, et le `case` correspondant retire le temps de l'EU. Pour `ADD r/m8,reg`
   (`case 0x00`), ce temps est de 3 horloges entre registres et de 24 avec la mémoire. Le chapitre 02 compare ces temps
   aux tables d'Intel.
3. **Les données.** Chaque accès mémoire aux données ajoute son temps de bus à `memcycs` (`Cpu/808x.cs#memcycs`) :
   - 4 horloges par octet (`Cpu/808x.cs#readmemb`, `Cpu/808x.cs#writememb`) ;
   - 8 par mot sur le 8088 (`8 >> is8086`), soit les « quatre horloges de plus » d'Intel [I2 p. 7-38].

   Font exception la lecture d'un octet à l'adresse `cs + pc` (la garde de `readmemb`), et les E/S, dont le temps est
   inclus dans le coût fixe de l'instruction. Les lectures de préchargement (`Cpu/808x.cs#readmembf`) ne coûtent rien
   ici : leur temps est compté par la file, dans `fetchcycles`, et dans `fetchclocks` quand l'EU attend.
4. **Le solde.** `FETCHADD((cycdiff - cycles) - memcycs - fetchclocks)` donne à la BIU les horloges pendant lesquelles
   le bus est resté libre. Si les accès aux données ont pris plus de temps que l'instruction, le dépassement est retiré
   du budget : l'instruction est alors limitée par le bus.
5. **Le temps des minuteries.** `Cpu/808x.cs#clockhardware` convertit les horloges du processeur en tops de
   l'oscillateur maître, par `xt_cpu_multi` (`Cpu/808x.cs#xt_cpu_multi`), un rapport en virgule fixe 32:32. Il fait
   avancer `timer.cs#tsc` et, si une minuterie arrive à échéance, appelle `timer.cs#timer_process`.

### Le rafraîchissement

La chaîne part du front montant de la sortie 1 du 8253 :
1. `Models/pit.cs#pit_refresh_timer_xt` ;
2. `Models/dma.cs#dma_channel_read`, sur le canal 0 ;
3. `Cpu/808x.cs#refreshread`, qui appelle `FETCHCOMPLETE` puis ajoute 4 à `memcycs`.

Le code compte quatre horloges de bus par rafraîchissement, comme IBM [T1 p. 1-15]. S'y ajoutent les 1 à 3 horloges
que `FETCHCOMPLETE` débite pour finir un préchargement en cours (*déduit* de la lecture).

### Le reset

`Cpu/808x.cs#resetx86` (`pcem-dev/src/cpu/808x.c:662-704`) :
- charge CS à FFFFh (`loadcs(0xFFFF)`) et IP à 0 ;
- met les drapeaux à 2 et les registres généraux à 0 ;
- fixe `rammask` à FFFFFh et vide la file (`FETCHCLEAR`) ;
- appelle `Cpu/x86seg.cs#x86seg_reset`, qui place la base de CS en FFFF0h, et celle de DS, ES et SS à 0
  (`Cpu/x86seg.cs#seg_reset`).

Comparé à la table 2-4 d'Intel :
- **CS, IP, DS, SS, ES et la file** sont conformes.
- **Les drapeaux** valent 2 et non 0. Le bit 1 n'est pas un drapeau : Intel le range parmi les bits indéfinis de
  l'image des drapeaux, et demande aux programmes de ne rien supposer sur eux [I1 p. 2-100].
- **Les registres généraux** sont mis à zéro. Intel ne dit pas ce qu'un vrai 8088 y laisse (*inconnu*). Le zéro de
  PCem est reproduit tel quel.

## 4. Les écarts

| PB | Le vrai 8088 | Le code | Mode matériel |
|---|---|---|---|
| PB-03 | Le rafraîchissement prive le processeur de temps de bus, mais ce temps s'écoule comme le reste. | Par `refreshread`, `FETCHCOMPLETE` débite 1 à 3 horloges après que `clockhardware` a pris son `diff`. Ces horloges n'atteignent jamais `tsc`, et l'horloge de l'invité prend du retard sur son processeur. | prévu en G13.3, avec PB-257 |
| PB-87 | IP revient à 0 au-delà de FFFFh. | Si une instruction chevauche FFFFh alors que la file est vide, l'octet suivant est lu en `cs + 10000h`. | prévu en G13.3, priorité basse |
| PB-179 | Un mot qui dépasse la fin d'un segment revient à son début. | Un mot lu ou écrit à l'offset FFFFh prend son octet haut dans le segment suivant. | avec PB-07, en G13.3 (le plan ne le nomme pas encore) |
| PB-07 | Au-delà de FFFFFh, l'adresse revient à 0. | Un mot au sommet de la RAM lit ou écrit son octet haut hors de l'allocation : dans le tas chez PCem, et dans une marge de quatre octets nuls en C# comme dans l'oracle. | prévu en G13.3 pour le repli à 1 Mo ; la valeur du bus flottant reste inconnue |
| PB-11 | Sans objet : c'est une question de temps. | La garde de `readmemw` compare un offset de 16 bits à une adresse linéaire. Elle est donc presque toujours vraie, et une lecture de mot qui tombe sur l'instruction elle-même paie les 8 horloges que `readmemb` n'aurait pas facturées. | exclu : les temps restent hors du mode (PLAN-G13, décision n° 1) |

Un autre écart avec Intel n'est pas inscrit au registre :
- Selon Intel, un chargement en cours s'achève avant tout accès de l'EU.
- Le code ne le fait que pour les cycles de DMA (rafraîchissement et disquette), par `refreshread`, le seul appelant de
  `FETCHCOMPLETE`. Pour les autres accès, il fait le compte à la fin de l'instruction, par `memcycs` (*déduit* de la
  lecture).

Le modèle de bus de PCem est donc une approximation. PB-11 le rappelle : ses temps ne sont pas comparés horloge par
horloge aux traces de SingleStepTests.

Aucun de ces écarts ne relève encore du mode matériel. En G13.2, seul PB-01 y est entré, et il concerne le
chapitre 02.

## 5. Pour l'observer

Les portes sont définies dans `tools/gates/series.sh`.
- **`fuzz8088` et `fuzz8088-stream`** : des instructions tirées au hasard, exécutées par le cœur C# et par le `808x.c`
  de PCem compilé dans l'oracle. Après chaque instruction, on compare chaque registre et chaque variable de temps.
- **`sst8088`** : le corpus SingleStepTests 8088 v2 (`vectors/sst/`). Pour chaque forme, la ligne obtenue (cas,
  passe, masque, premier échec) doit être identique à celle de `sst-baseline.tsv` : on reproduit PCem, on ne cherche
  pas à faire mieux.
- **`sst8088-materiel`** : le même test en mode matériel (`--hardware-mode PB-01`), comparé à
  `sst-baseline-materiel.tsv`.
- **`bd-pc-cga`** : l'amorçage du 5150 jusqu'au BASIC, comparé à l'oracle instruction par instruction. Depuis G10.0,
  on attend 25 456 706 instructions identiques (`VERIFICATION.md`, § G10.0 ; `PLAN-286.md`, « Ce qui ne doit pas
  bouger »). Le compte a changé deux fois depuis les 25 457 269 du § M6 : en M21, parce que le POST sonde désormais
  COM1 et COM2, puis en G10.0, parce qu'il trouve aussi LPT1, LPT2 et le port jeu.
