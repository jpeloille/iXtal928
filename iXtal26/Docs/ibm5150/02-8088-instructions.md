# 02 — Le 8088 : les instructions

> Lu sur `9ccfdd4`, le 8 octobre 2026 ; le code y est celui de `dc03cf6`. Sources : I1 (§ 2.7 et 2.8, tables 2-20 et
> 2-21 ; chapitre 4, § 4.2 et table 4-13), T1 (sections 1 et 6). Mesures : `sst-baseline.tsv` et
> `sst-baseline-materiel.tsv` ; classement du corpus : `vectors/sst/v2/metadata.json`.
>
> Une phrase suivie d'un renvoi à une source est *documentée*. Ce qui est lu dans le code se vérifie par son renvoi.
> Les comptes de SingleStepTests sont *mesurés*. Seuls les raisonnements portent une mention : *déduit*, ou *inconnu*.

Ce chapitre suit une instruction de son premier octet à son dernier cycle. Il donne ensuite, opcode par opcode, ce que
dit Intel, ce que fait le code et ce que mesure SingleStepTests. Le chapitre 01 décrit la file d'attente et le bus ;
le chapitre 03 traite des interruptions.

## 1. Le composant

### Le format d'une instruction

Une instruction du 8088 occupe d'un à six octets [I1 p. 4-18, 4-20] :
- **l'opcode**, le premier octet, qui fixe l'opération ;
- pour la plupart des instructions, **un octet « mod reg r/m »** :
  - `mod` dit si l'opérande est un registre ou la mémoire, et la taille du déplacement ;
  - `reg` désigne un registre, ou, dans les instructions de groupe, la variante de l'opération ;
  - `r/m` désigne le second registre, ou la formule de l'adresse en mémoire ;
- **un déplacement** de 0, 1 ou 2 octets (DISP-LO, DISP-HI) ;
- **une donnée immédiate** de 0, 1 ou 2 octets (DATA-LO, DATA-HI).

Un préfixe peut précéder l'opcode, et coûte chacun deux horloges [I1 p. 2-60, 2-63, 2-64, 2-65] :
- un changement de segment (ES:, CS:, SS:, DS:) ;
- LOCK ;
- une répétition (REP, REPE, REPNE).

### L'adresse effective

- Quand un opérande est en mémoire, l'EU calcule son déplacement dans le segment : c'est l'adresse effective (EA).
  C'est un nombre de 16 bits non signé, somme d'un déplacement, d'une base (BX ou BP) et d'un index (SI ou DI), chacun
  facultatif [I1 p. 2-68, 2-69].
- La BIU ajoute ensuite à l'EA la base du segment, et effectue le cycle de bus [I1 p. 2-68].
- Le calcul de l'EA prend du temps à l'EU [I1 p. 2-51, table 2-20] :

  | Composants | Horloges |
  |---|---|
  | Déplacement seul | 6 |
  | Base ou index seul (BX, BP, SI, DI) | 5 |
  | Déplacement + base ou index | 9 |
  | BP+DI, BX+SI | 7 |
  | BP+SI, BX+DI | 8 |
  | BP+DI+déplacement, BX+SI+déplacement | 11 |
  | BP+SI+déplacement, BX+DI+déplacement | 12 |
  | Changement de segment | + 2 |

### Les temps

La table 2-21 d'Intel donne, pour chaque instruction et chaque forme d'opérandes, les horloges, le nombre de transferts
sur le bus et la taille [I1 p. 2-51 à 2-68]. Quatre règles en précisent la lecture :
- **le bus de huit bits** : sur le 8088, on ajoute quatre horloges par transfert d'un mot [I1 p. 2-51, note de la
  table 2-21] ;
- **la file** : la table suppose que l'instruction est déjà dans la file, ce qui vaut « dans la plupart des cas, mais
  pas tous », et que la BIU obtient le bus dès qu'elle le demande [I1 p. 2-50, 2-51] ;
- **les branchements** : leurs temps comprennent déjà le remplissage de la file et la lecture de l'instruction cible
  [I1 p. 2-50] ;
- **l'écart à la réalité** : avec un mélange courant d'instructions, la durée réelle reste à 5-10 % près de la somme de
  la table, mais on peut construire des suites bien plus lentes. Le temps d'une suite donnée est en revanche toujours
  le même, à conditions extérieures égales [I1 p. 2-51].

### Les familles

Intel range les instructions en six familles [I1 p. 2-30 et suivantes] :
- les transferts (MOV, PUSH, POP, XCHG, IN, OUT, LEA…) ;
- l'arithmétique (ADD, ADC, SUB, SBB, INC, DEC, NEG, CMP, MUL, DIV, les ajustements décimaux…) ;
- la logique, les décalages et les rotations ;
- les chaînes (MOVS, CMPS, SCAS, LODS, STOS, avec ou sans répétition) ;
- les branchements (JMP, CALL, RET, les sauts conditionnels, LOOP, INT, IRET) ;
- le contrôle du processeur (les drapeaux, HLT, WAIT, ESC, LOCK).

Pour chaque instruction, la table 2-21 dit aussi quels drapeaux elle modifie. Certains sont marqués « U » :
« undefined — contains no reliable value » [I1 p. 2-50].

### Les opcodes inutilisés

La table 4-13 d'Intel déroule les 256 valeurs du premier octet [I1 p. 4-27 à 4-35]. Elle marque « (not used) » :
- **des opcodes entiers** : 0F, 60 à 6F, C0, C1, C8, C9, D6 et F1 ;
- **des variantes de groupes** : 82 /1, /4, /6 ; 83 /1, /4, /6 ; 8C et 8E avec un registre de segment de 4 à 7 ;
  8F /1 à /7 ; C6 et C7 /1 à /7 ; D0 à D3 /6 ; F6 et F7 /1 ; FE /2 à /7 ; FF /7.

Intel ne dit pas ce que le processeur fait de ces octets. Le corpus SingleStepTests, relevé sur un AMD D8088 de 1982,
les classe dans son `vectors/sst/v2/metadata.json` (*documenté (secondaire)*) :
- **alias** : 60 à 6F, 82, C0, C1, C8, C9, F6 /1, F7 /1 et FF /7 ;
- **normal** : 0F, 83 en entier, 8C et 8E ;
- **undocumented** : D6, et D0 à D3 /6 ;
- **préfixe** : F1, comme F0 ;
- **undefined** : 8F, C6 et C7 /1 à /7, et FE /2 à /7.

Le corpus ne dit pas de quoi un alias est l'alias.

## 2. Dans le 5150

- IBM reprend le jeu d'instructions du 8088 dans sa section 6 [T1 p. 6-1]. Il en donne les codages, mais pas les
  temps.
- Ce sont donc les temps d'Intel pour le 8088 qui valent (*déduit*). Dans le 5150, une horloge dure 210 ns
  [T1 p. 1-4].
- Tant qu'aucun périphérique adressé n'active la ligne READY, un cycle mémoire du processeur prend quatre horloges, et
  un cycle d'E/S cinq [T1 p. 1-4, 1-15]. IN et OUT coûtent donc une horloge de plus par transfert que ne le dit Intel
  (*déduit*).
- Le rafraîchissement prend environ 7 % du bus selon IBM [T1 p. 1-15], 5,6 % par le calcul (chapitre 01). Il retarde
  le programme d'autant (*déduit*).
- Le BIOS commence par éprouver les drapeaux et les sauts conditionnels (chapitre 01, § 2). Il se sert de SAHF, LAHF,
  SHR, SHL et des sauts conditionnels [T1 p. 5-34]. Une erreur dans les drapeaux de ces instructions se voit donc dès
  la première page du POST (*déduit*).

## 3. Le code

### Le décodage

`Cpu/808x.cs#execx86` lit l'opcode par `FETCH`, puis aiguille sur `switch (opcode)` (chez PCem,
`pcem-dev/src/cpu/808x.c:1272`) :
- 255 opcodes ont leur `case` ;
- CEh (INTO) n'en a pas. Il tombe dans le `default` (`pcem-dev/src/cpu/808x.c:3902-3905`), qui lit un octet de plus :
  c'est le constat C3.

### Les préfixes

- **Les changements de segment** (`case 0x26`, `0x2E`, `0x36`, `0x3E` ; chez PCem dès
  `pcem-dev/src/cpu/808x.c:1583`). Le code :
  1. range DS et SS dans `oldds` et `oldss` (`Cpu/808x.cs#oldds`, `Cpu/808x.cs#oldss`) ;
  2. fait pointer DS **et** SS sur le segment demandé et pose `cpu_state.ssegs` (`Cpu/x86.cs#ssegs`) ;
  3. compte 4 horloges ;
  4. revient à `opcodestart` (`Cpu/808x.cs#opcodestart`) pour lire l'opcode suivant.

  Le préfixe et son instruction s'exécutent ainsi dans le même tour de boucle : aucune interruption ne peut s'intercaler
  entre eux (*déduit*). À la fin de l'instruction, DS et SS reprennent leur valeur. Les opérations de pile remettent SS
  avant de s'en servir, par exemple PUSH mémoire (`case 0xFF`, `/6`) : le changement de segment ne touche pas la pile.
- **La répétition** (`case 0xF2`, `0xF3`). `Cpu/808x.cs#rep` (`pcem-dev/src/cpu/808x.c:910`) lit l'octet qui suit.
  - Un préfixe ES:, CS: ou SS: placé après REP est reconnu. DS: ne l'est pas : il tombe dans le `default` de `rep()`,
    qui coûte 20 horloges et rend la main à l'instruction suivante. C'est l'une des facettes de PB-177.
  - MOVS, CMPS et STOS bouclent à l'intérieur de `rep()`, sur CX, et font avancer le temps des minuteries
    (`clockhardware`) à chaque élément.
  - LODS, SCAS et 6Eh ne traitent qu'un élément, puis ramènent l'IP sur le préfixe REP (`cpu_state.pc = ipc`) : la
    répétition se poursuit au tour suivant d'`execx86`.

  PB-177 relève les écarts de `rep()`.
- **LOCK** (`case 0xF0`, `0xF1`). Le code en fait une instruction d'un octet, qui coûte 4 horloges, et non un préfixe :
  c'est PB-178.

### L'octet mod reg r/m et l'adresse effective

- `Cpu/808x.cs#fetchea` lit le second octet (`rmdat`) et le découpe en `cpu_mod`, `cpu_reg` et `cpu_rm`.
- `Cpu/808x.cs#fetcheal` (`pcem-dev/src/cpu/808x.c:381`) calcule l'EA (`eaaddr`) et son segment (`easeg`). Il se
  sert des tables de
  `Cpu/808x.cs#makemod1table`, qui donnent pour chaque `r/m` sa base, son index et son segment par défaut : SS dès que
  BP sert de base, DS sinon.
- Le temps de l'EA reprend exactement la table 2-20 d'Intel :
  - `FETCHADD(6)` pour un déplacement seul ;
  - `FETCHADD(5)` pour un registre ;
  - 7 ou 8 pour deux registres (`slowrm`) ;
  - 9 pour un déplacement et un registre ;
  - 11 ou 12 pour un déplacement et deux registres.

  Mais ces horloges ne sont pas retirées du budget de l'instruction : elles vont à la file, qui s'en sert pour
  précharger (chapitre 01).
- Les quatre accesseurs `Cpu/808x.cs#geteab`, `Cpu/808x.cs#geteaw`, `Cpu/808x.cs#seteab` et `Cpu/808x.cs#seteaw`
  lisent et écrivent l'opérande : un registre désigné par `cpu_rm`, ou la mémoire en `easeg + eaaddr`.

### Les drapeaux

- `Cpu/808x.cs#makeznptable` précalcule ZF, SF et PF pour chaque octet et chaque mot. `Cpu/808x.cs#setznp8` et
  `Cpu/808x.cs#setznp16` les posent.
- `Cpu/808x.cs#setadd8`, `Cpu/808x.cs#setadc8`, `Cpu/808x.cs#setsbc8` et leurs pendants calculent les drapeaux des
  additions et des soustractions.
- Les variantes `nc`, comme `Cpu/808x.cs#setadd8nc` et `Cpu/808x.cs#setsub8nc`, laissent CF intact : INC et DEC ne le
  touchent pas.
- En mode matériel, l'AF d'ADC et de SBB passe par `Cpu/808x.cs#af_materiel` (`Cpu/808x.Materiel.cs#af_materiel`),
  selon `Materiel/materiel.cs#pb_01`. C'est PB-01, le seul défaut du 8088 corrigé à ce jour.

### Les temps

Chaque `case` retire un coût fixe, souvent double : un pour la forme registre, un pour la forme mémoire. Ainsi
`ADD r/m8,r8` (`case 0x00`) coûte 3 horloges entre registres et 24 avec la mémoire. En comparant ces coûts à la
table 2-21 (*déduit*, voir la table ci-dessous) :
1. **Les formes mémoire.** Leur coût est le temps d'Intel pour la forme **mot** sur le 8088, sans l'EA :
   - 24 pour `ADD r/m,reg`, soit 16 + 2 transferts × 4 ;
   - 13 pour `ADD reg,r/m`, soit 9 + 4 ;
   - 25 pour XCHG, 23 pour INC mémoire.

   Le code applique ce coût aux octets comme aux mots. Quatre exceptions :
   - les chaînes : MOVS et STOS prennent le coût de l'octet, CMPS, LODS et SCAS celui du mot, pour les deux tailles ;
   - le groupe 1 avec une valeur immédiate (80 à 83) : 23 avec la mémoire, ni le 25 du mot ni le 17 de l'octet ; seul
     CMP, à 14, retrouve le temps du mot ;
   - JMP par la mémoire (FF /4) : 18 au lieu de 22 ;
   - JMP far par la mémoire (FF /5) : 24 au lieu de 32.
2. **L'EA** ne s'ajoute jamais au coût fixe : son temps sert au préchargement (voir plus haut).
3. **Les sauts conditionnels et les boucles** coûtent ce que dit Intel : 4 non pris, et 4 plus 12 s'ils sont pris,
   soit 16. Seul LOOPNE diffère : 18 ou 6, au lieu de 19 ou 5. Intel compte pourtant, dans ces temps, le remplissage de
   la file et la lecture de l'instruction cible [I1 p. 2-50]. Le code, lui, vide la file (`FETCHCLEAR`), et
   l'instruction suivante attend encore ses octets : ce remplissage est compté deux fois.
4. **RET, RETF et IRET** coûtent 8 horloges de plus que dans la table d'I1. C'est surprenant : Intel y donne 8 pour
   RET, moins qu'un JMP (15), alors que ses temps de branchement comprennent le remplissage de la file [I1 p. 2-50].
   Nos sources n'ont pas d'autre table de temps : on ne sait pas si une édition plus récente d'Intel donne d'autres
   valeurs (*inconnu*).
5. **MUL, IMUL, DIV et IDIV** prennent la borne basse d'Intel, quelles que soient les valeurs.
6. **IN et OUT** prennent le coût du mot pour les octets aussi. Le cycle d'E/S de cinq horloges du 5150 n'est pas
   modélisé à part.
7. **Les petites différences** :
   - INC et DEC de registre 16 bits coûtent 3 au lieu de 2.
   - Un préfixe de segment coûte 4 au lieu de 2. Ce pourrait être les 2 du préfixe plus les 2 que la table 2-20 ajoute
     à l'EA, mais le code les compte même quand l'instruction n'a pas d'EA.
   - AAA et AAS coûtent 8 au lieu de 4, comme RET, sous la même réserve.
   - CLI coûte 3 au lieu de 2, et TEST de l'accumulateur par une valeur immédiate 5 au lieu de 4.
   - WAIT coûte 4, là où Intel compte 3 plus 5 par tour d'attente.
8. **Sans 8087**, un ESC (`D8` à `DF`) lit son octet mod reg r/m et ne coûte rien de plus. Intel compte 2 horloges, ou
   8 plus l'EA, avec une lecture.

Aucun de ces écarts de temps n'est inscrit au registre, et les temps restent hors du mode matériel (PLAN-G13, décision
n° 1). Le dépôt ne les compare pas non plus au silicium : les vecteurs de SingleStepTests portent la trace des cycles
et l'état de la file, mais la sonde ne vérifie que l'état final (`tools/iXtal26.Diff/SstProbe.cs`).

## 4. La table des opcodes

Les colonnes :
- **Intel** : la forme d'après la table 4-13 [I1 p. 4-27 à 4-35], en notation normalisée. Ainsi RETF note le retour
  intersegment, m32 le pointeur que chargent LES et LDS, et OUT prend l'ordre usuel de ses opérandes ;
- **Intel, 8088** : les horloges de la table 2-21 [I1 p. 2-51 à 2-68], avec les quatre horloges que la note de la
  table ajoute par mot transféré. « r ; m » se lit « registre ; mémoire », « 16 ou 4 » « pris ou non pris » ;
- **Code** : le coût fixe du `case` d'`execx86`, et ce que fait le code quand Intel ne dit rien ;
- **SST** : les cas réussis sur les cas joués, en mode PCem, d'après `sst-baseline.tsv`.
  - La sonde ne joue pas les cas qui commencent par F2 ou F3, ni AAM 0. Les formes A4, AC et AD comptent 2000 cas
    chacune, mais 517, 545 et 485 d'entre eux commencent par un préfixe de répétition. « A4 : 1483/1483 » ne dit donc
    rien de `rep()`.
  - Un tiret signale une forme absente du corpus du dépôt, qui en a 103 sur les 324 que compte
    `tools/fetch-sst.sh` ;
- **Écart** : l'entrée du registre ou le constat.

| Op | Intel | Intel, 8088 | Code | SST | Écart |
|---|---|---|---|---|---|
| 00 | ADD r/m8, r8 | 3 ; 16+EA | 3 ; 24 | 10000/10000 | |
| 01 | ADD r/m16, r16 | 3 ; 24+EA | 3 ; 24 | 10000/10000 | |
| 02 | ADD r8, r/m8 | 3 ; 9+EA | 3 ; 13 | 10000/10000 | |
| 03 | ADD r16, r/m16 | 3 ; 13+EA | 3 ; 13 | 10000/10000 | |
| 04 | ADD AL, imm8 | 4 | 4 | 10000/10000 | |
| 05 | ADD AX, imm16 | 4 | 4 | 10000/10000 | |
| 06 | PUSH ES | 14 | 14 | — | |
| 07 | POP ES | 12 | 12 | — | |
| 08 | OR r/m8, r8 | 3 ; 16+EA | 3 ; 24 | 10000/10000 | |
| 09 | OR r/m16, r16 | 3 ; 24+EA | 3 ; 24 | 10000/10000 | |
| 0A | OR r8, r/m8 | 3 ; 9+EA | 3 ; 13 | 10000/10000 | |
| 0B | OR r16, r/m16 | 3 ; 13+EA | 3 ; 13 | 10000/10000 | |
| 0C | OR AL, imm8 | 4 | 4 | 10000/10000 | |
| 0D | OR AX, imm16 | 4 | 4 | 10000/10000 | |
| 0E | PUSH CS | 14 | 14 | — | |
| 0F | (not used) | — | POP CS ; 12 | — | |
| 10 | ADC r/m8, r8 | 3 ; 16+EA | 3 ; 24 | 9650/10000 | PB-01 |
| 11 | ADC r/m16, r16 | 3 ; 24+EA | 3 ; 24 | 9696/10000 | PB-01 |
| 12 | ADC r8, r/m8 | 3 ; 9+EA | 3 ; 13 | 9680/10000 | PB-01 |
| 13 | ADC r16, r/m16 | 3 ; 13+EA | 3 ; 13 | 9682/10000 | PB-01 |
| 14 | ADC AL, imm8 | 4 | 4 | 9719/10000 | PB-01 |
| 15 | ADC AX, imm16 | 4 | 4 | 9697/10000 | PB-01 |
| 16 | PUSH SS | 14 | 14 | — | |
| 17 | POP SS | 12 | 12 | — | |
| 18 | SBB r/m8, r8 | 3 ; 16+EA | 3 ; 24 | 9575/10000 | PB-01 |
| 19 | SBB r/m16, r16 | 3 ; 24+EA | 3 ; 24 | 9515/10000 | PB-01 |
| 1A | SBB r8, r/m8 | 3 ; 9+EA | 3 ; 13 | 9530/10000 | PB-01 |
| 1B | SBB r16, r/m16 | 3 ; 13+EA | 3 ; 13 | 9549/10000 | PB-01 |
| 1C | SBB AL, imm8 | 4 | 4 | 9671/10000 | PB-01 |
| 1D | SBB AX, imm16 | 4 | 4 | 9716/10000 | PB-01 |
| 1E | PUSH DS | 14 | 14 | — | |
| 1F | POP DS | 12 | 12 | — | |
| 20 | AND r/m8, r8 | 3 ; 16+EA | 3 ; 24 | 10000/10000 | |
| 21 | AND r/m16, r16 | 3 ; 24+EA | 3 ; 24 | 10000/10000 | |
| 22 | AND r8, r/m8 | 3 ; 9+EA | 3 ; 13 | 10000/10000 | |
| 23 | AND r16, r/m16 | 3 ; 13+EA | 3 ; 13 | 10000/10000 | |
| 24 | AND AL, imm8 | 4 | 4 | 10000/10000 | |
| 25 | AND AX, imm16 | 4 | 4 | 10000/10000 | |
| 26 | ES: (préfixe) | 2 | 4 | — | |
| 27 | DAA | 4 | 4 | 9936/10000 | PB-170 |
| 28 | SUB r/m8, r8 | 3 ; 16+EA | 3 ; 24 | 10000/10000 | |
| 29 | SUB r/m16, r16 | 3 ; 24+EA | 3 ; 24 | 10000/10000 | |
| 2A | SUB r8, r/m8 | 3 ; 9+EA | 3 ; 13 | 10000/10000 | |
| 2B | SUB r16, r/m16 | 3 ; 13+EA | 3 ; 13 | 10000/10000 | |
| 2C | SUB AL, imm8 | 4 | 4 | 10000/10000 | |
| 2D | SUB AX, imm16 | 4 | 4 | 10000/10000 | |
| 2E | CS: (préfixe) | 2 | 4 | — | |
| 2F | DAS | 4 | 4 | 9814/10000 | PB-171 |
| 30 | XOR r/m8, r8 | 3 ; 16+EA | 3 ; 24 | 10000/10000 | |
| 31 | XOR r/m16, r16 | 3 ; 24+EA | 3 ; 24 | 10000/10000 | |
| 32 | XOR r8, r/m8 | 3 ; 9+EA | 3 ; 13 | 10000/10000 | |
| 33 | XOR r16, r/m16 | 3 ; 13+EA | 3 ; 13 | 10000/10000 | |
| 34 | XOR AL, imm8 | 4 | 4 | 10000/10000 | |
| 35 | XOR AX, imm16 | 4 | 4 | 10000/10000 | |
| 36 | SS: (préfixe) | 2 | 4 | — | |
| 37 | AAA | 4 | 8 | 10000/10000 | |
| 38 | CMP r/m8, r8 | 3 ; 9+EA | 3 ; 13 | 10000/10000 | |
| 39 | CMP r/m16, r16 | 3 ; 13+EA | 3 ; 13 | 10000/10000 | |
| 3A | CMP r8, r/m8 | 3 ; 9+EA | 3 ; 13 | 10000/10000 | |
| 3B | CMP r16, r/m16 | 3 ; 13+EA | 3 ; 13 | 10000/10000 | |
| 3C | CMP AL, imm8 | 4 | 4 | 10000/10000 | |
| 3D | CMP AX, imm16 | 4 | 4 | 10000/10000 | |
| 3E | DS: (préfixe) | 2 | 4 | — | |
| 3F | AAS | 4 | 8 | 10000/10000 | |
| 40 | INC AX | 2 | 3 | 10000/10000 | |
| 41 | INC CX | 2 | 3 | — | |
| 42 | INC DX | 2 | 3 | — | |
| 43 | INC BX | 2 | 3 | 10000/10000 | |
| 44 | INC SP | 2 | 3 | — | |
| 45 | INC BP | 2 | 3 | — | |
| 46 | INC SI | 2 | 3 | — | |
| 47 | INC DI | 2 | 3 | — | |
| 48 | DEC AX | 2 | 3 | 10000/10000 | |
| 49 | DEC CX | 2 | 3 | — | |
| 4A | DEC DX | 2 | 3 | — | |
| 4B | DEC BX | 2 | 3 | 10000/10000 | |
| 4C | DEC SP | 2 | 3 | — | |
| 4D | DEC BP | 2 | 3 | — | |
| 4E | DEC SI | 2 | 3 | — | |
| 4F | DEC DI | 2 | 3 | — | |
| 50 | PUSH AX | 15 | 15 | 10000/10000 | |
| 51 | PUSH CX | 15 | 15 | — | |
| 52 | PUSH DX | 15 | 15 | — | |
| 53 | PUSH BX | 15 | 15 | 10000/10000 | |
| 54 | PUSH SP | 15 | 15 | — | |
| 55 | PUSH BP | 15 | 15 | — | |
| 56 | PUSH SI | 15 | 15 | — | |
| 57 | PUSH DI | 15 | 15 | — | |
| 58 | POP AX | 12 | 12 | 10000/10000 | |
| 59 | POP CX | 12 | 12 | — | |
| 5A | POP DX | 12 | 12 | — | |
| 5B | POP BX | 12 | 12 | 10000/10000 | |
| 5C | POP SP | 12 | 12 | — | |
| 5D | POP BP | 12 | 12 | — | |
| 5E | POP SI | 12 | 12 | — | |
| 5F | POP DI | 12 | 12 | — | |
| 60 | (not used) | — | comme 70 (JO) | — | |
| 61 | (not used) | — | comme 71 (JNO) | — | |
| 62 | (not used) | — | comme 72 (JB) | — | |
| 63 | (not used) | — | comme 73 (JNB) | — | |
| 64 | (not used) | — | comme 74 (JE) | — | |
| 65 | (not used) | — | comme 75 (JNE) | — | |
| 66 | (not used) | — | comme 76 (JBE) | — | |
| 67 | (not used) | — | comme 77 (JNBE) | — | |
| 68 | (not used) | — | comme 78 (JS) | — | |
| 69 | (not used) | — | comme 79 (JNS) | — | |
| 6A | (not used) | — | comme 7A (JP) | — | |
| 6B | (not used) | — | comme 7B (JNP) | — | |
| 6C | (not used) | — | comme 7C (JL) | — | |
| 6D | (not used) | — | comme 7D (JNL) | — | |
| 6E | (not used) | — | comme 7E (JLE) | — | |
| 6F | (not used) | — | comme 7F (JNLE) | — | |
| 70 | JO | 16 ou 4 | 16 ou 4 | 10000/10000 | |
| 71 | JNO | 16 ou 4 | 16 ou 4 | — | |
| 72 | JB, JNAE | 16 ou 4 | 16 ou 4 | 10000/10000 | |
| 73 | JNB, JAE | 16 ou 4 | 16 ou 4 | — | |
| 74 | JE, JZ | 16 ou 4 | 16 ou 4 | 10000/10000 | |
| 75 | JNE, JNZ | 16 ou 4 | 16 ou 4 | 10000/10000 | |
| 76 | JBE, JNA | 16 ou 4 | 16 ou 4 | 10000/10000 | |
| 77 | JNBE, JA | 16 ou 4 | 16 ou 4 | — | |
| 78 | JS | 16 ou 4 | 16 ou 4 | 10000/10000 | |
| 79 | JNS | 16 ou 4 | 16 ou 4 | — | |
| 7A | JP, JPE | 16 ou 4 | 16 ou 4 | 10000/10000 | |
| 7B | JNP, JPO | 16 ou 4 | 16 ou 4 | — | |
| 7C | JL, JNGE | 16 ou 4 | 16 ou 4 | 10000/10000 | |
| 7D | JNL, JGE | 16 ou 4 | 16 ou 4 | — | |
| 7E | JLE, JNG | 16 ou 4 | 16 ou 4 | 10000/10000 | |
| 7F | JNLE, JG | 16 ou 4 | 16 ou 4 | 10000/10000 | |
| 80 | groupe 1 : r/m8, imm8 | 4 ; 17+EA (CMP : 4 ; 10+EA) | 4 ; 23 (CMP : 4 ; 14) | — | voir les groupes |
| 81 | groupe 1 : r/m16, imm16 | 4 ; 25+EA (CMP : 4 ; 14+EA) | 4 ; 23 (CMP : 4 ; 14) | — | voir les groupes |
| 82 | groupe 1 : r/m8, imm8 ; /1, /4, /6 not used | comme 80 | même `case` que 80 | — | voir les groupes |
| 83 | groupe 1 : r/m16, imm8 étendu ; /1, /4, /6 not used | 4 ; 25+EA (CMP : 4 ; 14+EA) | 4 ; 23 (CMP : 4 ; 14) | — | voir les groupes |
| 84 | TEST r/m8, r8 | 3 ; 9+EA | 3 ; 13 | — | |
| 85 | TEST r/m16, r16 | 3 ; 13+EA | 3 ; 13 | — | |
| 86 | XCHG r8, r/m8 | 4 ; 17+EA | 4 ; 25 | — | |
| 87 | XCHG r16, r/m16 | 4 ; 25+EA | 4 ; 25 | — | |
| 88 | MOV r/m8, r8 | 2 ; 9+EA | 2 ; 13 | 10000/10000 | |
| 89 | MOV r/m16, r16 | 2 ; 13+EA | 2 ; 13 | — | |
| 8A | MOV r8, r/m8 | 2 ; 8+EA | 2 ; 12 | — | |
| 8B | MOV r16, r/m16 | 2 ; 12+EA | 2 ; 12 | — | |
| 8C | MOV r/m16, sreg ; sreg 4 à 7 not used | 2 ; 13+EA | 2 ; 13 ; avec sreg 4 à 7, rien n'est écrit | — | constat C4 |
| 8D | LEA r16, m16 | 2+EA | 2 | — | |
| 8E | MOV sreg, r/m16 ; sreg 4 à 7 not used | 2 ; 12+EA | 2 ; 12 ; avec sreg 4 à 7, rien n'est chargé | — | constat C4 |
| 8F | POP r/m16 ; /1 à /7 not used | 25+EA | 25 ; le champ reg est ignoré | — | |
| 90 | NOP (XCHG AX, AX) | 3 | 3 | 10000/10000 | |
| 91 | XCHG AX, CX | 3 | 3 | — | |
| 92 | XCHG AX, DX | 3 | 3 | — | |
| 93 | XCHG AX, BX | 3 | 3 | — | |
| 94 | XCHG AX, SP | 3 | 3 | — | |
| 95 | XCHG AX, BP | 3 | 3 | — | |
| 96 | XCHG AX, SI | 3 | 3 | — | |
| 97 | XCHG AX, DI | 3 | 3 | — | |
| 98 | CBW | 2 | 2 | — | |
| 99 | CWD | 5 | 5 | — | |
| 9A | CALL far | 36 | 36 | — | |
| 9B | WAIT | 3+5n | 4 | — | |
| 9C | PUSHF | 14 | 14 | — | |
| 9D | POPF | 12 | 12 | — | |
| 9E | SAHF | 4 | 4 | — | |
| 9F | LAHF | 4 | 4 | — | |
| A0 | MOV AL, m8 | 10 | 14 | — | |
| A1 | MOV AX, m16 | 14 | 14 | — | |
| A2 | MOV m8, AL | 10 | 14 | — | |
| A3 | MOV m16, AX | 14 | 14 | — | |
| A4 | MOVSB | 18 | 18 | 1483/1483 | |
| A5 | MOVSW | 26 | 18 | — | |
| A6 | CMPSB | 22 | 30 | — | |
| A7 | CMPSW | 30 | 30 | — | |
| A8 | TEST AL, imm8 | 4 | 5 | — | |
| A9 | TEST AX, imm16 | 4 | 5 | — | |
| AA | STOSB | 11 | 11 | — | |
| AB | STOSW | 15 | 11 | — | |
| AC | LODSB | 12 | 16 | 1009/1455 | PB-172 |
| AD | LODSW | 16 | 16 | 1004/1515 | PB-172 |
| AE | SCASB | 15 | 19 | — | |
| AF | SCASW | 19 | 19 | — | |
| B0 | MOV AL, imm8 | 4 | 4 | — | |
| B1 | MOV CL, imm8 | 4 | 4 | — | |
| B2 | MOV DL, imm8 | 4 | 4 | — | |
| B3 | MOV BL, imm8 | 4 | 4 | — | |
| B4 | MOV AH, imm8 | 4 | 4 | — | |
| B5 | MOV CH, imm8 | 4 | 4 | — | |
| B6 | MOV DH, imm8 | 4 | 4 | — | |
| B7 | MOV BH, imm8 | 4 | 4 | — | |
| B8 | MOV AX, imm16 | 4 | 4 | — | |
| B9 | MOV CX, imm16 | 4 | 4 | — | |
| BA | MOV DX, imm16 | 4 | 4 | — | |
| BB | MOV BX, imm16 | 4 | 4 | — | |
| BC | MOV SP, imm16 | 4 | 4 | — | |
| BD | MOV BP, imm16 | 4 | 4 | — | |
| BE | MOV SI, imm16 | 4 | 4 | — | |
| BF | MOV DI, imm16 | 4 | 4 | — | |
| C0 | (not used) | — | comme C2 (RET imm16) | — | |
| C1 | (not used) | — | comme C3 (RET) | — | |
| C2 | RET imm16 | 16 | 24 | — | |
| C3 | RET | 12 | 20 | — | |
| C4 | LES r16, m32 | 24+EA | 24 | — | |
| C5 | LDS r16, m32 | 24+EA | 24 | — | |
| C6 | MOV r/m8, imm8 ; /1 à /7 not used | 4 ; 10+EA | 4 ; 14 ; le champ reg est ignoré | — | |
| C7 | MOV r/m16, imm16 ; /1 à /7 not used | 4 ; 14+EA | 4 ; 14 ; le champ reg est ignoré | 10000/10000, reg 0 à 7 | |
| C8 | (not used) | — | comme CA (RETF imm16) | — | |
| C9 | (not used) | — | comme CB (RETF) | — | |
| CA | RETF imm16 | 25 | 33 | — | |
| CB | RETF | 26 | 34 | — | |
| CC | INT 3 | 72 | 72 | — | |
| CD | INT imm8 | 71 | 71 ; IF n'est pas remis à zéro | — | constat C5 |
| CE | INTO | 73 ou 4 | pas de `case` : `default`, 8 horloges et un octet de trop | — | constat C3 |
| CF | IRET | 36 | 44 | — | |
| D0 | groupe 2 : r/m8, 1 | 2 ; 15+EA | 2 ; 23 | voir les groupes | voir les groupes |
| D1 | groupe 2 : r/m16, 1 | 2 ; 23+EA | 2 ; 23 | voir les groupes | voir les groupes |
| D2 | groupe 2 : r/m8, CL | 8+4/bit ; 20+EA+4/bit | 8+4/bit ; 28+4/bit | voir les groupes | voir les groupes |
| D3 | groupe 2 : r/m16, CL | 8+4/bit ; 28+EA+4/bit | 8+4/bit ; 28+4/bit | voir les groupes | voir les groupes |
| D4 | AAM | 83 | 83 | 8415/9953 | PB-175, PB-46 |
| D5 | AAD | 60 | 60 | 5131/10000 | PB-175 |
| D6 | (not used) | — | SALC (AL ← FFh si CF, sinon 00h) ; 4 | — | |
| D7 | XLAT | 11 | 11 | — | |
| D8 | ESC 0 | 2 ; 8+EA | rien sans 8087 | — | |
| D9 | ESC 1 | 2 ; 8+EA | rien sans 8087 | — | |
| DA | ESC 2 | 2 ; 8+EA | rien sans 8087 | — | |
| DB | ESC 3 | 2 ; 8+EA | rien sans 8087 | — | |
| DC | ESC 4 | 2 ; 8+EA | rien sans 8087 | — | |
| DD | ESC 5 | 2 ; 8+EA | rien sans 8087 | — | |
| DE | ESC 6 | 2 ; 8+EA | rien sans 8087 | — | |
| DF | ESC 7 | 2 ; 8+EA | rien sans 8087 | — | |
| E0 | LOOPNE, LOOPNZ | 19 ou 5 | 18 ou 6 | — | |
| E1 | LOOPE, LOOPZ | 18 ou 6 | 18 ou 6 | — | |
| E2 | LOOP | 17 ou 5 | 17 ou 5 | — | |
| E3 | JCXZ | 18 ou 6 | 18 ou 6 | — | |
| E4 | IN AL, imm8 | 10 | 14 | — | |
| E5 | IN AX, imm8 | 14 | 14 | — | |
| E6 | OUT imm8, AL | 10 | 14 | — | |
| E7 | OUT imm8, AX | 14 | 14 | — | |
| E8 | CALL near | 23 | 23 | 10000/10000 | |
| E9 | JMP near | 15 | 15 | — | |
| EA | JMP far | 15 | 15 | — | |
| EB | JMP short | 15 | 15 | — | |
| EC | IN AL, DX | 8 | 12 | — | |
| ED | IN AX, DX | 12 | 12 | — | |
| EE | OUT DX, AL | 8 | 12 | — | |
| EF | OUT DX, AX | 12 | 12 | — | |
| F0 | LOCK (préfixe) | 2 | 4 | — | PB-178 |
| F1 | (not used) | — | comme F0 | — | PB-178 |
| F2 | REPNE, REPNZ | 2 | `rep(0)` | — | PB-177 |
| F3 | REP, REPE, REPZ | 2 | `rep(1)` | — | PB-177 |
| F4 | HLT | 2 | 2 | — | |
| F5 | CMC | 2 | 2 | — | |
| F6 | groupe 3 : r/m8 | voir les groupes | voir les groupes | voir les groupes | voir les groupes |
| F7 | groupe 3 : r/m16 | voir les groupes | voir les groupes | voir les groupes | voir les groupes |
| F8 | CLC | 2 | 2 | — | |
| F9 | STC | 2 | 2 | — | |
| FA | CLI | 2 | 3 | — | |
| FB | STI | 2 | 2 | — | |
| FC | CLD | 2 | 2 | — | |
| FD | STD | 2 | 2 | — | |
| FE | groupe 4 : INC, DEC r/m8 ; /2 à /7 not used | 3 ; 15+EA | 3 ; 23 ; de /2 à /7, DEC | — | |
| FF | groupe 5 : r/m16 ; /7 not used | voir les groupes | voir les groupes | voir les groupes | voir les groupes |

### Les groupes

Dans un groupe, le champ `reg` de l'octet mod reg r/m choisit l'opération (« /0 » à « /7 »). Le code aiguille sur
`switch (rmdat & 0x38)`. La colonne SST donne la forme « opcode.reg » du corpus.

| Groupe | Intel | Intel, 8088 | Code | SST | Écart |
|---|---|---|---|---|---|
| 80, 82 /0 | ADD r/m8, imm8 | 4 ; 17+EA | 4 ; 23 | — | |
| 80, 82 /1 | OR (82 : not used) | 4 ; 17+EA | 4 ; 23 | — | |
| 80, 82 /2 | ADC | 4 ; 17+EA | 4 ; 23 | — | PB-01 |
| 80, 82 /3 | SBB | 4 ; 17+EA | 4 ; 23 | — | PB-01 |
| 80, 82 /4 | AND (82 : not used) | 4 ; 17+EA | 4 ; 23 | — | |
| 80, 82 /5 | SUB | 4 ; 17+EA | 4 ; 23 | — | |
| 80, 82 /6 | XOR (82 : not used) | 4 ; 17+EA | 4 ; 23 | — | |
| 80, 82 /7 | CMP | 4 ; 10+EA | 4 ; 14 | — | |
| 81 /0 à /6 | ADD, OR, ADC, SBB, AND, SUB, XOR r/m16, imm16 | 4 ; 25+EA | 4 ; 23 | — | PB-01 pour ADC et SBB |
| 81 /7 | CMP r/m16, imm16 | 4 ; 14+EA | 4 ; 14 | — | |
| 83 /0 à /6 | les mêmes, imm8 étendu en signe ; /1, /4 et /6 not used | 4 ; 25+EA | 4 ; 23, OR, AND et XOR compris | — | PB-01 pour ADC et SBB |
| 83 /7 | CMP r/m16, imm8 | 4 ; 14+EA | 4 ; 14 | — | |
| D0, D1 /0 à /3 | ROL, ROR, RCL, RCR, par 1 | 2 ; 15+EA (mot : 23+EA) | 2 ; 23 | D1.2, D1.3 : 10000/10000 | |
| D0, D1 /4 | SAL, SHL, par 1 | 2 ; 15+EA (mot : 23+EA) | 2 ; 23 | D0.4 : 10000/10000 | |
| D0, D1 /5 | SHR, par 1 | 2 ; 15+EA (mot : 23+EA) | 2 ; 23 | — | |
| D0, D1 /6 | (not used) | — | comme /4 (SHL) | D0.6 : 32/10000 ; D1.6 : 0/10000 | PB-173 |
| D0, D1 /7 | SAR, par 1 | 2 ; 15+EA (mot : 23+EA) | 2 ; 23 | — | |
| D2, D3 /0, /1 | ROL, ROR, par CL | 8+4/bit ; 20+EA+4/bit (mot : 28+EA+4/bit) | 8+4/bit ; 28+4/bit | — | |
| D2, D3 /2, /3 | RCL, RCR, par CL | 8+4/bit ; 20+EA+4/bit (mot : 28+EA+4/bit) | 8+4/bit ; 28+4/bit | D2.2, D2.3 : 5000/5000 ; D3.2 : 2627/5000 ; D3.3 : 2555/5000 | PB-02 (mot) |
| D2, D3 /4 | SAL, SHL, par CL | 8+4/bit ; 20+EA+4/bit (mot : 28+EA+4/bit) | 8+4/bit ; 28+4/bit | D2.4 : 2648/5000 ; D3.4 : 2598/5000 | PB-174 |
| D2, D3 /5 | SHR, par CL | 8+4/bit ; 20+EA+4/bit (mot : 28+EA+4/bit) | 8+4/bit ; 28+4/bit | D2.5 : 2635/5000 ; D3.5 : 2597/5000 | PB-174 |
| D2, D3 /6 | (not used) | — | comme /4 (SHL) | D2.6 : 174/5000 ; D3.6 : 155/5000 | PB-173, PB-174 |
| D2, D3 /7 | SAR, par CL | 8+4/bit ; 20+EA+4/bit (mot : 28+EA+4/bit) | 8+4/bit ; 28+4/bit | D2.7 : 1593/5000 ; D3.7 : 1740/5000 | PB-174, PB-176 |
| F6, F7 /0 | TEST r/m, imm | 5 ; 11+EA | 5 ; 11 | — | |
| F6, F7 /1 | (not used) | — | comme /0 (TEST) | — | |
| F6, F7 /2 | NOT | 3 ; 16+EA (mot : 24+EA) | 3 ; 24 | — | |
| F6, F7 /3 | NEG | 3 ; 16+EA (mot : 24+EA) | 3 ; 24 | — | |
| F6 /4 | MUL AL, r/m8 | 70-77 ; (76-83)+EA | 70 | F6.4 : 10000/10000 | |
| F6 /5 | IMUL AL, r/m8 | 80-98 ; (86-104)+EA | 80 | — | |
| F6 /6 | DIV AL, r/m8 | 80-90 ; (86-96)+EA | 80 | F6.6 : 4802/10000 | PB-169, PB-180 |
| F6 /7 | IDIV AL, r/m8 | 101-112 ; (107-118)+EA | 101 | F6.7 : 1169/9696 | PB-45, PB-169, PB-180, PB-177 |
| F7 /4 | MUL AX, r/m16 | 118-133 ; (128-143)+EA | 118 | F7.4 : 10000/10000 | |
| F7 /5 | IMUL AX, r/m16 | 128-154 ; (138-164)+EA | 128 | — | |
| F7 /6 | DIV AX, r/m16 | 144-162 ; (154-172)+EA | 144 | F7.6 : 4905/10000 | PB-169, PB-180 |
| F7 /7 | IDIV AX, r/m16 | 165-184 ; (175-194)+EA | 165 | F7.7 : 2243/9674 | PB-169, PB-180, PB-177, PB-47 |
| FE /0, /1 | INC, DEC r/m8 | 3 ; 15+EA | 3 ; 23 | — | |
| FE /2 à /7 | (not used) | — | DEC | — | |
| FF /0, /1 | INC, DEC r/m16 | 23+EA (mémoire) | 3 ; 23 | FF.0 : 10000/10000 | |
| FF /2 | CALL r/m16 | 20 ; 29+EA | 20 ; 29 | — | |
| FF /3 | CALL m32 (far) | 53+EA | 53 | — | |
| FF /4 | JMP r/m16 | 11 ; 22+EA | 11 ; 18 | — | |
| FF /5 | JMP m32 (far) | 32+EA | 24 | — | |
| FF /6 | PUSH r/m16 | 24+EA (mémoire) | 15 ; 24 | — | |
| FF /7 | (not used) | — | pas de `case` : rien d'autre que la lecture de l'octet mod reg r/m | — | constat C4 |

Trois règles se lisent dans le code :
- **les décalages par CL** coûtent 4 horloges par bit dans la boucle de chaque `case` (`c * 4` pour SHL et SHR) ;
- **avec CL = 0**, un décalage s'arrête aussitôt (`if (c == 0) break;`). Il n'écrit rien, ne touche aucun drapeau et ne
  retire aucune horloge, là où Intel compte 8, ou 20 plus l'EA ;
- **MUL, IMUL, DIV et IDIV** prennent la borne basse d'Intel, comme pour la forme registre, et l'accès à l'opérande
  passe par `memcycs`.

## 5. Les écarts

Chaque ligne du registre ci-dessous concerne une instruction du 8088. La classe G13 est celle du registre : (a)
corrigeable et vérifiable, (b) corrigeable mais mal vérifiable, (c) comportement réel inconnu, laissé reproduit, (d) à
ne pas corriger [`PCEM_BUGS.md`, en-tête].

| PB | Ce que fait le code | Mesure | G13 |
|---|---|---|---|
| PB-01 | L'AF d'ADC et de SBB ignore la retenue entrante. | 10 à 1D : 9515 à 9719 sur 10000 ; 10000 partout en mode matériel | (a) ; **corrigé en mode matériel** (G13.2) |
| PB-02 | RCL et RCR mot par CL écrasent le CF sortant par le CF entrant. | D3.2 : 2627/5000 ; D3.3 : 2555/5000 | (a) |
| PB-45 | IDIV octet étend AX par des zéros au lieu du signe. | F6.7 | (a), conditionnel |
| PB-169 | DIV et IDIV ne déclenchent l'INT 0 que pour un diviseur nul, pas sur un dépassement. | F6.6, F6.7, F7.6, F7.7 | (a), avec PB-45 et PB-180 |
| PB-170 | DAA suit le pseudo-code d'Intel, là où le silicium compare autrement. | 27 : 9936/10000 | (a) |
| PB-171 | DAS reprend le CF de l'emprunt du bas et compare l'AL déjà ajusté. | 2F : 9814/10000 | (a) |
| PB-172 | REP LODSB et REP LODSW ne chargent jamais AL ni AX. | AC : 1009/1455 ; AD : 1004/1515 | (a) |
| PB-173 | SETMO et SETMOC (D0 à D3 /6) sont traités comme SHL. | D0.6 : 32/10000 ; D1.6 : 0/10000 | (a) |
| PB-174 | Les décalages par CL n'écrivent jamais OF. | D2.4, D2.5, D3.4 et D3.5 : environ la moitié des cas ; avec PB-173 et PB-176, D2.6 à D3.7 | (a) |
| PB-175 | AAM et AAD posent SF et ZF d'après AX. | D4 : 8415/9953 ; D5 : 5131/10000 | (a) |
| PB-176 | SAR par CL rend CF nul au-delà de 8 (16) sur un opérande négatif. | D2.7, D3.7 | (a) |
| PB-177 | `rep()` exécute 6Eh comme OUTSB, oublie DS: et perd un préfixe placé avant REP. | — | (b) |
| PB-178 | LOCK est une instruction d'un octet, pas un préfixe ; F1 de même, que le corpus classe aussi en préfixe. | — | (b) |
| PB-180 | L'INT 0 empile les drapeaux d'avant la division (chapitre 03). | — | (c) |
| PB-46 | AAM 0 divise par zéro en C. | — | hors du mode : non reproduit |
| PB-47 | IDIV divise INT_MIN par −1 en C. | — | hors du mode : non reproduit |
| PB-11, PB-12 | Des écarts de temps internes au modèle (PB-11 au chapitre 01 ; PB-12 : REP MOVSB oublie de remettre `memcycs` à zéro). | — | (d) |

S'y ajoutent trois constats absents du registre (`constats.md`) :
- **C3** : INTO manque.
- **C4** : le code ne fait rien de FF /7, que le corpus dit alias, ni de 8C et 8E avec un registre de segment de 4
  à 7, qu'il dit normaux.
- **C5** : INT n (CD) ne remet pas IF à zéro, alors qu'Intel le demande [I1 p. 2-46]. INT 3, l'INTR et la NMI le font.

Restent les opcodes et variantes qu'Intel dit inutilisés. Le code leur donne une conduite :
- des alias : 60 à 6F, 82, C0, C1, C8, C9, F1, F6 /1 et F7 /1 ;
- OR, AND et XOR pour 83 /1, /4 et /6, avec une valeur immédiate étendue en signe ;
- SALC (D6) et POP CS (0F) ;
- DEC pour FE /2 à /7 ;
- le champ `reg` ignoré pour 8F, C6 et C7 ;
- rien du tout, pour FF /7, et pour 8C et 8E avec un registre de segment de 4 à 7.

Ces conduites s'accordent avec le classement du corpus pour les alias, 83, SALC et POP CS. Elles s'en écartent pour F1,
que le corpus range parmi les préfixes (PB-178), et pour les deux cas de C4.

Une seule de ces conduites est mesurée, celle de C7 : sa forme du corpus couvre les huit valeurs de `reg` et passe
partout, si bien qu'ignorer `reg` y est juste (*mesuré*). Pour les autres, seules les formes correspondantes du corpus
trancheraient (0F, 60 à 6F, 82, 83, 8C, 8E, 8F, C0, C1, C6, C8, C9, D6, F1, F6.1, F7.1, FE.2 à FE.7, FF.7), et le
dépôt ne les a pas.

## 6. Pour l'observer

- **`sst8088`** joue chaque cas du corpus (`vectors/sst/`), sauf ceux qui commencent par F2 ou F3 et AAM 0. Il
  compare les registres, les drapeaux masqués et la mémoire. La ligne obtenue doit être identique à celle de
  `sst-baseline.tsv`, d'où vient la colonne SST des tables ci-dessus. `tools/iXtal26.Diff/SstProbe.cs` ne compare ni
  les cycles ni la file.
- **`sst8088-materiel`** fait de même en mode matériel (`--hardware-mode PB-01`), contre `sst-baseline-materiel.tsv`.
  Les formes 10 à 1D y passent à 10000/10000 ; les autres ne changent pas.
- **`fuzz8088`** et **`fuzz8088-stream`** tirent des instructions au hasard et comparent, instruction par instruction,
  le cœur C# au `808x.c` de l'oracle : ils vérifient la fidélité à PCem, défauts compris.
- **Pour mesurer ce qui manque** : `tools/fetch-sst.sh CE CD FF.7 8C 8E` ajouterait au manifeste les formes
  nommées, que le corpus amont décrit toutes dans son `metadata.json`. CD trancherait le constat C5. CE trancherait le constat C3 ; FF.7, 8C et 8E le constat C4.
  Ajouter des formes au corpus relève de la décision n° 5 de PLAN-G13.
