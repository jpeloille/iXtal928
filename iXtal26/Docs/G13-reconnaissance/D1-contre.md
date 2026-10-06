# G13 — Contre-lecture de D1 : le processeur

> Contre-lecture du 6 octobre 2026, au commit `bc609ce`, en lecture seule : rien n'a été construit,
> lancé ni modifié dans le dépôt. Méthode : chaque ligne de C citée par D1 relue dans `pcem-dev/` ;
> recomptes des deux lignes de base par `awk` (commandes ci-dessous) ; calculs exhaustifs sur 8 bits
> (AF, DAA, DAS) faits par des scripts jetables hors du dépôt ; sources extérieures relues (README
> SingleStepTests, `80386.csv`, `metadata.json` du 8088, spécification MOO, pages du 386 PRM, tables du
> SDM, Bochs, OS/2 Museum, R. Collins, K. Shirriff). Les vecteurs SST n'ont pas été téléchargés.
> Niveaux : **documenté** (source primaire : Intel, ou mesure publiée sur silicium), **déduit**
> (raisonnement ou calcul sur sources), **inconnu**. Numérotation : C = confirmé, K = corrigé, A = ajouté.

## 0. En bref

- Les recomptes tiennent : 8088, **19** formes déviantes sur 84, dont **12** couvertes par un PB
  (PB-01, 4 320 échecs sur 27 694) ; 386, **742** sur 941, **aucune** couverte (C1, C2).
- Les six défauts « lus à la ligne » du 386 existent tous à la ligne citée. Deux réserves : le CF de BT*
  n'est expliqué qu'en partie par le décalage non signé (K7), et les 742 formes comptent des drapeaux
  que le corpus marque lui-même comme indéfinis, faute d'appliquer son `f_umask` (K8).
- L'élargissement de PB-87 est réel. Il est même nécessaire : la lecture fausse en `:150` suppose
  toujours une lecture fausse en `:145` juste avant (C14).
- **Une erreur de manuel** : le 386 PRM dit que le JMP de tâche efface NT, ce que fait PCem. Le
  « voisin non inscrit » de PB-39 n'est pas un défaut au sens du 386 PRM, mais une contradiction avec
  le SDM (K1).
- **Une prédiction fragile** : corriger le débordement de DIV/IDIV ne fera pas monter `F6.x`/`F7.x`
  vers 100 % tant que les drapeaux empilés par le #DE ne sont pas modélisés (K6).
- Le DAA du 8088 ne se corrige pas « selon Intel » : le pseudo-code du SDM rend exactement le résultat
  de PCem ; l'écart vient du silicium seul (K5).
- Manquent au rapport : l'AAM/AAD du 8088, le CF de SAR par CL, trois défauts de `rep()`, le DAS du
  cœur 386, le filtre REP de la sonde 8088, le lecteur MOO incapable de lire le 286, et le
  traitement par UC des corrections du cœur partagé (A1 à A10).

---

## 1. Confirmé

**C1 — Recompte du 8088.** *Documenté (le fichier).* Méthode, une forme est déviante si
`passe < cas` :
`awk -F'\t' '!/^#/ && $1!="forme" {n++; c+=$2; p+=$3; if ($3<$2) {d++; f+=$2-$3}} END {print n,c,p,d,f}' sst-baseline.tsv`
→ `84 817998 790304 19 27694`. PB-01 couvre 12 formes (`10`–`15`, `18`–`1D`) et **4 320 échecs
(15,6 %)**. Les 7 autres (`27`, `2F`, `AD`, `D0.6`, `D2.4`, `F6.6`, `F7.6`) totalisent **23 374
échecs (84,4 %)** sans aucune entrée (64 + 186 + 511 + 9 968 + 2 352 + 5 198 + 5 095).

**C2 — Recompte du 386.** *Documenté (le fichier).* Même méthode, colonnes `passe` (2) et `joues`
(3) :
`awk -F'\t' '!/^#/ && $1!="forme" {n++; j+=$3; p+=$2; if ($2<$3) {d++; f+=$3-$2}} END {print n,d,f,j,p}' sst386-baseline.tsv`
→ 941 formes, **742 déviantes**, 199 à 100 %, **284 449 échecs** sur 1 758 699 cas joués ;
`hors_carte` vaut 0 partout. Les sept familles de `VERIFICATION.md` § G2 somment exactement ces
chiffres. Aucune forme n'a pour famille dominante un défaut inscrit. La seule couverture partielle
concerne les cas `AAM 0` de `D4` (environ 1/256 de 2 500, soit une dizaine) : le texte de PB-46 en
décrit le comportement côté 386 (`PCEM_BUGS.md:1989-1990`) sans l'instruire comme défaut (*déduit*).

**C3 — PB-01.** Les quatre tests d'AF sont bien `808x.c:786`, `:817`, `:849`, `:882`, sans `tempc`.
Côté C#, `808x.cs:580`, `:617`, `:655`, `:694`, sans marqueur ; la référence du registre a dérivé.
Calcul exhaustif sur (a, b, retenue), 8 bits : PCem se trompe sur **4 096/131 072** triplets pour ADC
comme pour SBB ; `((a ^ b ^ r) & 0x10)` n'en manque **aucun**, dans les deux cas. *Déduit (calcul)*.
Les SBB registre (`18`–`1B`) échouent jusqu'à 4,85 %, au-dessus des 3,1 % attendus : les cas
`sbb r,r` à opérandes égaux (les quartets bas sont alors égaux) font échouer AF dès que CF = 1. Cela
reste un écart d'AF, et la prédiction « 10 000/10 000 » de D1 est plausible. *Déduit* : la ligne de
base ne montre que le premier échec de chaque forme.

**C4 — L'AF d'ADC du cœur 386** (`x86_flags.h:299-307`). La formule est telle que D1 la cite. Calcul
exhaustif : **3 840/131 072** (2,93 %) pour ADC, **0** pour SBB (`:317-321`), et aucun écart quand
les deux quartets bas sont des chiffres BCD valides. Le défaut est distinct de PB-01 et frappe aussi le
286, dont la table `ops_286` (`cpu.c:324`) est bâtie sur les mêmes en-têtes. Vrai comportement : AF
est la retenue du bit 3, *documenté* (SDM vol. 1, § 3.4.3.1). La mesure le confirme : « l'AF d'ADC
(2 370 cas) », `VERIFICATION.md:3867`.

**C5 — LOCK sans #UD.** `x86_ops_misc.h:701-712` : seul `LOCK NOP` est refusé (`:707`). *Documenté
pour le 386* par le 386 PRM, § 14.7, item 9 : « An undefined-opcode exception (interrupt 6) results
from using LOCK before any other instruction ». Pour le 286, voir A5.

**C6 — MOVSX r16,r/m16 en ILLEGAL.** C'est vrai en `386_ops.h:1432` (a16), et aussi en **`:1958`**
(a32, forme `670FBF`), que D1 ne cite pas. Mesure : `660FBF` (forme r32) passe à 96,8 %, `0FBF` à
2,7 %, `670FBF` à 2,3 %. Vrai comportement : *documenté par la mesure seule* (386EX) ; `80386.csv`
décrit `0FBF` comme « MOVSX r16/32, r/m16 ». Les manuels d'Intel (386 PRM, SDM) ne listent que la
forme r32 : de ce côté-là, *inconnu*.

**C7 — AAA/AAS à la façon du 8086.** `AL += 6; AH++` en AAA (`x86_ops_bcd.h:3-15`) et en AAS
(**`:41-53`**, D1 ne cite que `:3-15`). Le SDM écrit `AX := AX + 106H`. Le 386 PRM écrit le contraire
(page AAA : « AL := (AL + 6) AND 0FH; AH := AH + 1 ») : la contradiction est *documentée*. La mesure
(`37` à 91,6 %, `3F` à 93,2 %, famille R, EAX divergent en premier) va dans le sens du SDM (*déduit*,
à confirmer sur les cas).

**C8 — AAD/AAM, `setznp16(AX)`** (`x86_ops_bcd.h:23`, `:35`). Le défaut est confirmé, avec une nuance.
Pour AAD, AH vaut 0 au moment du calcul, donc **ZF est juste** et seul SF est faux (bit 15 toujours
nul) : `D5` à 50,2 % est le profil d'un SF faux une fois sur deux. Pour AAM, ZF est faux quand
AL mod base = 0 avec AH ≠ 0 ; SF ne l'est que pour des bases atypiques (1, ou plus de 80h).
*Documenté* : SDM, pages AAD et AAM (« SF, ZF, and PF flags are set according to the resulting binary
value in the AL register ») ; `80386.csv` donne `f_umask` 0xF7EE, donc SF, ZF et PF comparés.

**C9 — REP LODSB/LODSW du 8088.** `808x.c:1110-1147` : `temp2`/`tempw2` ne sont jamais copiés dans
AL/AX. *Documenté* (définition de LODS) et mesuré (`AD`).

**C10 — SETMO/SETMOC.** `808x.c:2798-2799`, `:2920-2921`, `:3068-3069`, `:3219-3220` : `/6` est traité
comme SHL. `metadata.json` (8088 v2) marque `D0.6`, `D1.6`, `D2.6` et `D3.6` « undocumented », masque
0xF72A. Vrai comportement : *mesuré seulement* (AMD D8088).

**C11 — L'OF des décalages par CL.** `808x.c:3068-3119` et `:3219-3271` n'écrivent jamais `V_FLAG`.
`metadata.json` donne 65 519 (0xFFEF) à `D2.4`, `D2.5`, `D2.7`, `D3.4`, `D3.5`, `D3.7` : seul AF est
masqué, OF est comparé. Intel le dit indéfini pour un compte différent de 1 ; le 8088 le pose de façon
déterministe. *Documenté par la mesure* ; la règle exacte est *déduite*.

**C12 — Le débordement de DIV/IDIV.** `808x.c:3571` et `:3723` ne testent que le diviseur nul ; IDIV
de même (`:3615`, `:3746`). *Documenté* : Intel, page DIV (« Interrupt 0 if the quotient is too big ») ;
386 PRM § 14.7, item 11 (quotient 80h/8000h → exception 0 sur le 8086/8088). Vérifié à la main dans
VERIFICATION (`F6.6[2]`).

**C13 — PB-02.** `808x.c:2887-2919` (formes `,1`) sont sains ; seuls `:3184-3187` et `:3207-3210`
écrasent CF ; le pendant octet est commenté en `:3059-3060`. L'erreur du registre est réelle, y compris
son « CF faux après toute rotation ». `metadata.json` : aucun masque pour `D3.2`/`D3.3`. Le cœur 386
n'a pas ce défaut : `D1.2`, `D1.3`, `D3.2` et `D3.3` y passent au taux de la famille E1 (96,9 %).

**C14 — L'élargissement de PB-87 est réel, et nécessaire.** *Déduit (lecture).* `808x.c:145` n'a pas de
garde `is8086` : le 8088 est touché. La lecture de `:150` n'a lieu que si `pc` est impair après
`pc = pc + 1` (`:146`). Une adresse fausse en `:150` (0x10001 ou plus) suppose donc que `:145` vient
de lire à 0x10000 ou plus : **elle ne se trompe jamais seule**. Le scénario de PB-87 (« lit un second
octet… porte pc à 0x10001 ») contient ainsi une lecture fausse en `:145`. Le fuzzeur ne l'a pas vue,
parce que C et C# la font à l'identique : seule `:150` avait été transcrite autrement en M1.
Effet observable : quand une instruction est à cheval sur FFFFh et que la file est vide (après tout
saut, `FETCHCLEAR` met `prefetchw` à 0, `:242-243`), ses octets d'après FFFFh viennent de
`cs + 0x10000`. Vrai comportement : le 8086 lit à l'offset 0, *documenté* (386 PRM § 14.7, item 8). Le
cas est rare dans du code réel.

**C15 — PB-41.** La phrase du SDM est exacte (vol. 3A, p. 7-16, « modified and not maintained »). Pour
Bochs (`tasking.cc`), le commentaire « incoming TSS is 16bit: upper word of general registers is set
to 0xFFFF » et le code `newEAX = 0xffff0000 | temp16` sont vérifiés. **Ajout** : PCem et Bochs mettent
aussi FS et GS à nul pour une TSS 16 bits (`x86seg.c:2835-2838` ; Bochs, `raw_fs_selector = 0`). Le
classement (c) est confirmé ; la valeur exacte reste *inconnue*.

**C16 — Les quatre erreurs de texte signalées sont vraies.**
- PB-02 cite des sites sains (C13).
- `x86_flags.cs:35-40` justifie AF_SET du cœur 386 par un échec du 8088 (« adc dl, ch… », tiré de
  `sst-baseline.tsv`). Or SBB est juste dans ce cœur, et ADC y est faux d'une autre formule. De plus,
  « depuis M0 » est inexact : les formes `10`–`1D` sont entrées en M1.2 (`VERIFICATION.md:42`).
- −5,87 ppm : `PCEM_BUGS.md:91` l'impute à PB-11, `VERIFICATION.md:554` à `pc.c:473`. Le calcul :
  4 772 728/100 = 47 727,28, tronqué à 47 727, soit −5,867 ppm. PB-11 change les cycles d'une
  instruction, pas le nombre de cycles par seconde émulée : il ne peut pas toucher la fréquence de
  l'INT 8 (*déduit*). La reprise par D7 est réelle (`D7-mecanisme.md:432`, `:609-610`).
- README SST 80386 : « beyond the maximum of 10 bytes … An exception interrupt #6 will occur »
  (vérifié), contraire au 386 PRM § 14.7, item 6 (15 octets, exception 13). Sur l'origine, voir K11.

**C17 — Les corpus 8086 et 80286 existent**, avec les caractéristiques annoncées (*documenté*, README).
- 8086 : Intel P80C86A-2, 2 000 cas par opcode, `v1/*.json.gz` et MOO dans `v1_binary`, file de
  préfetch pleine au départ, REP devant 10 % des IDIV, CL masqué à 6 bits.
- 80286 : Harris N80C286-12, 326 formes, 1 000 à 5 000 cas, MOO dans `v1_real_mode`. Le README dit
  qu'au-delà de 10 octets l'exception est #13, qu'un mot à l'offset FFFFh lève une exception, et que
  le LOCK est « rarely prepended » ; chaque cas commence par un saut, donc file vide.
- L'oracle sait déjà faire tourner les deux cœurs (`Oracle.cs:174-182`, `Core8086`, `Core286`).
  Pour la compatibilité avec la sonde, voir K3.

**C18 — Le générateur 8088 évite le franchissement de FFFFh.** *Déduit, et solide.* `SstProbe` remplit
la RAM de 0x90 (`SstProbe.cs:193`) et démarre file vide : PCem lirait 0x90 à `cs + 0x10000` et
décoderait autre chose. Sur les 65 formes à 100 % (650 000 cas), une IP uniforme donnerait une
quinzaine de franchissements ; on n'en observe aucun. Nuance : « aucun échec de ce type n'apparaît »
(D1) ne vaut que pour ces formes, puisque la ligne de base ne montre que le premier échec. Le README
8088 ne dit rien de ce point.

**C19 — Sites et effets des autres PB, relus et conformes à D1.**
- PB-39 : `x86seg.c:761-779`, `:1284-1296`.
- PB-40 : `x86_ops_call.h:3-97`, appelants `:110` et `:128`.
- PB-43 et PB-44 : les douze sites de `x86_ops_mov_ctrl.h`, et `prefetch_run`
  (`386_dynarec.c:155-206`), qui compte les déplacements.
- PB-45 : `808x.c:3614`.
- PB-50 : `x86_ops_prefix.h`, et `x86_ops_misc.h:701-712`.
- PB-77 : `cpu.c:316`, `:483-485`.
- PB-78 : `x86_ops_misc.h:930-974`, sans garde ni contrôle de CPL. R. Collins confirme les deux
  points : « Attempting to execute LOADALL at any other privilege level will generate an
  exception 13 » ; « the 486 does not have a LOADALL instruction ».
- PB-42 : `x86seg.c:2428-2440`. LTR : `x86_ops_pmode.h:230-262`.
- Les marqueurs du C# sont là où D1 les situe.

**C20 — PB-77 n'a pas d'effet pour l'invité.** L'écran de construction ne propose pas de processeur
(`SdlSetup.cs:31`), et `resetpchard` rejoue `cpu_set` avec la même UC (`pc.cs:1193`). *Déduit*.

---

## 2. Corrigé

**K1 — Le NT du JMP de tâche : D1 se trompe de manuel.** *Documenté.* Le 386 PRM, Table 7-2 (§ 7.6),
écrit pour JMP « NT bit of incoming task: **Cleared** », ce que fait PCem (`x86seg.c:768`). C'est le
SDM (vol. 3A, Table 7-2) qui écrit « Set to value from TSS of new task » ; Bochs suit le SDM (NT n'est
posé que pour CALL et INT). Au regard du manuel du 386, ce n'est donc pas un défaut, mais une
**contradiction entre manuels**, à classer (c). Il ne faut ni l'inscrire en U0 ni le corriger en U5
sans décision. D1 § 3 (PB-39, point 7) et § 5.3 sont à reprendre.

**K2 — La porte de tâche et ses exceptions (PB-39).** *Documenté.* La contradiction ne porte pas sur
le seul DPL. Dans le 386 PRM, la page CALL lève **#TS** pour toutes les vérifications de la porte et
du sélecteur de TSS : DPL, RPL, bit TI, limite de la GDT, TSS non occupée ; elle lève aussi #TS(0)
pour un IP hors limite. La page JMP lève **#GP** pour les mêmes vérifications. Seule la présence
(#NP) leur est commune. La voie TSS directe est dans le même cas (#TS dans CALL, #GP dans JMP).
« Les autres vérifications … sont communes aux deux pages » est vrai des conditions, faux des
exceptions. Le volet (b) de PB-39 couvre donc tous les codes d'erreur.

**K3 — « `Moo.cs` lit déjà le format MOO » : faux pour le 286.** *Documenté.* `Moo.cs` ne lit que
`RG32`/`RM32` et **lève une exception** sur `REGS` (`Moo.cs:145-146`). La spécification MOO réserve
`REGS`/`RMSK` aux UC 16 bits (8088, 8086, 80286). Une sonde 286 demande d'étendre le lecteur, et de lire
`EXCP`, qui donne l'adresse des drapeaux empilés. Le corpus 8086 en JSON (`v1/*.json.gz`, même nommage
que le 8088 v2) est vraisemblablement lisible par `SstProbe` (*déduit* ; schéma non vérifié faute de
vecteurs).

**K4 — L'AAM 0 du cœur 386 n'est pas « hors de portée de SST »** (D1 § 5.3). `D4` est au corpus 386,
en mode réel, et ses cas à immédiat nul (environ 1/256) attendent #DE (*documenté* : SDM, page AAM,
« #DE If an immediate value of 0 is used »). Ce comportement est déjà décrit dans PB-46
(`PCEM_BUGS.md:1989-1990`). Sa place est parmi les écarts mesurés, pas avec le mode protégé.

**K5 — DAA et DAS n'ont ni la même cause ni la même référence.** D1 écrit « DAS : idem » et ne source
pas le vrai comportement.
- **DAA.** Calcul exhaustif sur (AL, AF, CF) : le pseudo-code du SDM rend **exactement** le résultat
  de PCem sur les 1 024 entrées. Corriger DAA « selon Intel » ne changerait rien : l'écart du 8088 est
  propre au silicium. Le cas [20] n'a que deux antécédents (AL = 9Eh, CF = 0, AF = 0 ou 1). Il existe
  une observation publiée : dans un commentaire sous l'article de K. Shirriff (righto.com, janvier
  2023), GloriousCow, l'auteur de MartyPC, écrit que, mesuré sur un 8088, le seuil est 99h ou 9Fh
  selon l'AF initial (9Fh si AF = 1). Cette règle prédit 6/1 024 = **0,59 %** d'écarts contre PCem ;
  la mesure donne 64/10 000 = **0,64 %**. Niveau : *documenté par la mesure* (SST, plus l'observation
  publiée) ; Intel ne documente pas ce comportement, le SDM donnant le résultat de PCem.
- **DAS.** PCem s'écarte du SDM sur 24/1 024 entrées (`808x.c:1666-1678`) : le CF de l'ajustement bas
  est réutilisé au second test, qui compare de plus l'AL déjà ajusté. Le cas [15] (AL = 01h, AF = 1,
  CF = 0) est exactement celui du SDM. La règle « 9Fh si AF » prédit **1,76 %**, et la mesure donne
  **1,86 %**. *Documenté* (SDM) pour le cas observé, *déduit* pour la règle.

**K6 — La prédiction « ~100 % » pour PB-45 et le débordement est fragile, et D1 n'en tire pas la
conséquence de sa propre hypothèse.** L'INT 0 empile FLAGS en mémoire, et la sonde compare la
mémoire **sans masque** : le masque 0xF72A ne porte que sur le registre final. PB-46 a mesuré que le
8088 empile des drapeaux **recalculés** par la division (octet bas 0x46 pour AAM 0, 47 cas,
`PCEM_BUGS.md:1999-2001`), alors que PCem empile `flags | 0xF000` sans y toucher (`808x.c:3595`,
`:3637`, `:3730`, `:3754`). Les 32 cas « à diviseur nul » de `F6.7` s'expliquent vraisemblablement
ainsi (*déduit*, appuyé sur PB-46). Mais les milliers de cas de débordement de `F6.6`, `F7.6`, `F6.7`
et `F7.7` prendront la même voie une fois le débordement détecté. Ils échoueront alors sur la pile,
sauf à modéliser les drapeaux que le microcode laisse au moment du #DE : Intel ne les documente pas,
seule la mesure SST permet de les tirer. Le gain sûr se limite au signe (+1 143 sur `F6.7`) ; le reste
est conditionnel.

**K7 — Le CF de BT* : le défaut lu n'explique pas l'ampleur mesurée.** La division non signée du
décalage (`x86_ops_bit.h:8`, `:28`, `:48`, `:68`, et les pendants BTS/BTR/BTC) existe. Le vrai
comportement est *documenté* : décalage signé (386 PRM § 17.2, « BitOffset can range from -2 gigabits
to 2 gigabits » ; SDM vol. 2, Table 3-2, −2¹⁵ à 2¹⁵ − 1 en 16 bits). Mais `VERIFICATION.md:3867`
compte « ≈ 1 150 à 1 350 cas par forme » sur le CF. Avec un décalage 16 bits uniforme, l'adresse ne
change que si l'opérande est en mémoire et que le bit 15 est posé (au plus 3/8 des cas). Comme la RAM
de la sonde est remplie de 0x90, CF ne diffère alors qu'une fois sur deux : au plus 19 à 25 % des
2 500 cas, soit environ 470 à 625. **Plus de la moitié des écarts de CF a une autre cause, non lue**
(*déduit*). À instruire sur les vecteurs avant d'inscrire ce défaut comme leur explication.

**K8 — 742 formes déviantes ne font pas 742 défauts.** La sonde 386 n'applique pas le masque des
drapeaux indéfinis que fournit le corpus : `80386.csv`, colonne `f_umask` (README 80386 ; valeurs
relues : BT 0xF72B, BSF 0xF76A, IMUL 0xFF2B, SHLD 0xF7EF, AAA/AAS 0xF73B, AAM/AAD 0xF7EE, DAA/DAS
0xF7FF). La majorité des 104 650 échecs de la famille F1 portent sur des bits que le corpus déclare
lui-même indéfinis (`VERIFICATION.md:3864-3866`). Le nombre de formes déviantes bougerait peu,
puisque E1 et E2 touchent presque toutes les formes ; le nombre d'échecs, et leur lecture, beaucoup.
L'ampleur exacte est *inconnue* sans les vecteurs.

**K9 — La ligne de base 8088 ne joue pas les cas qui commencent par REP.** `IsUnimplementedPrefix`
(`SstProbe.cs:117`, `:332`) écarte F2 et F3 en premier octet. Ce filtre est périmé depuis M1.4 et laisse
1 002 cas de côté sur `A4`/`AD` (`VERIFICATION.md:731-743`). D1 ignore les conséquences :
- REP LODS n'est vérifié qu'en partie (`AD` : 1 515 cas joués sur 2 000) ;
- sur `F6.7`, 304 cas qui commencent par REP ne sont pas joués (PB-45 : 9 696 joués). La négation sous
  REP ne serait vérifiée que sur les REP placés après un préfixe de segment, et ceux-là ont un autre
  défaut (A3, iii).

Lever le filtre change les dénominateurs : c'est une re-base, donc une décision.

**K10 — Erreurs de texte supplémentaires, non relevées par D1.**
- PB-51 situe la boucle d'exec386 dans `386_dynarec.c`. Elle est dans `386.c:153`, avec la lecture
  d'opcode en `:178` : c'est ce fichier que l'oracle lie (`tools/oracle/Makefile:77`) et que transcrit
  `386.cs`. D1 cite le bon fichier sans signaler l'erreur du registre.
- PB-32 : l'expression fautive est en `x86seg.c:1660`, le `pclog` en `:1659`. Le registre et D1
  citent tous deux `:1659`.
- PB-87 ne décrit que la ligne `:150` (voir C14).
- D1 § 5.2, famille E2 : « PCem n'a dans les handlers que SEG_CHECK_READ … pas CHECK_READ » est
  inexact. `x86_ops_mov.h` appelle `CHECK_READ`/`CHECK_WRITE` en 23 sites (de `:183` à `:717` :
  moffs, MOV r/m, et la macro de `:664-717`), plus `x86_ops_misc.h:50`. Cela concorde avec les MOV a32
  (`6788`–`678B`) à environ 94 %, au lieu d'environ 82 % pour le reste de E2. Un contrôle de limite
  existe donc déjà dans le cœur, ce qui compte pour chiffrer E2/E3.

**K11 — L'origine de la phrase du README 80386.** « Vraisemblablement recopiée du README 80286 » est
à nuancer : le README 80286 dit #13, pas #6. Le « #6 » rappelle l'ancienne documentation du 286, que
l'errata Intel du 15 octobre 1984 corrige (« exception #13 … instead of exception #6 »). *Déduit*.

**K12 — Le classement (a) de PB-77 est peu cohérent avec le but de G13**, qui vise un comportement
observable par l'invité. L'UC étant fixée par processus (C20), (d) « outillage seulement » est plus
juste ; c'est d'ailleurs la question 9 de D1.

---

## 3. Ajouté

**A1 — L'AAM/AAD du 8088, oublié par D1 § 5.1.** `808x.c:3280-3293` appelle `setznp16(AX)` après AAM
et AAD : c'est le défaut de C8, dans l'autre cœur. `metadata.json` donne aux deux le masque 63 470
(0xF7EE) : SF, ZF et PF sont comparés. `D4` et `D5` sont hors manifeste, mais PB-46 montre que `D4`
a déjà été récupéré une fois. *Documenté* (SDM) et vérifiable par SST.

**A2 — Le CF de SAR par CL, sur le 8088.** `808x.c:3104` (`(temp >> (c - 1)) & 1`, sur un `temp`
8 bits étendu par des zéros) et `:3258` (forme mot) rendent CF = 0 pour un opérande négatif et un
compte de plus de 8 (resp. 16). Le vrai CF est alors le bit de signe. Au-delà de 32, le décalage est
un comportement indéfini en C ; x86 masque le compte à 5 bits, le C# aussi. Le corpus masque CL à
6 bits (README 8088), si bien que `D2.7` et `D3.7` l'exerceraient. *Déduit* (lecture et sémantique de
SAR).

**A3 — Trois défauts de `rep()` du 8088, et le LOCK.** *Déduit* (lecture, et sémantique des préfixes du
8086).
- (i) `case 0x6E: /*REP OUTSB*/` (`808x.c:950-969`). Sur le 8088/8086, 6Eh est l'alias de JLE
  (`:2010`), et OUTS n'existe pas avant le 186 : `F3 6E` devrait exécuter le saut et ignorer REP.
  PCem envoie CX octets sur le port DX.
- (ii) Il n'y a pas de `case 0x3E`. `REP DS: MOVSB` tombe dans le `default` (`:1201-1204`), qui
  relance à `ipc + 1` : l'instruction de chaîne ne s'exécute **qu'une fois**.
- (iii) Ce `default` relance à `ipc + 1`, et `ipc` est le début de toute l'instruction
  (`cpu_state.oldpc`, `:915`). Avec un préfixe de segment **avant** REP, la reprise saute ce préfixe,
  perdu pour l'instruction non-chaîne : `26 F3 F7 /7` lit son opérande dans DS. Les IDIV « seg REP » du
  corpus (`F6.7`, `F7.7`) en sont touchés, et la négation sous REP ne se corrige pas sans (iii).
- De plus, LOCK est traité comme une instruction d'un octet (`808x.c:3489-3492` : `break`, pas
  `goto opcodestart`). Un préfixe de segment placé avant LOCK est perdu à la fin de cette
  « instruction » (`:3924-3928`), et une interruption ou un pas-à-pas peut s'intercaler entre LOCK et
  l'instruction verrouillée.

**A4 — Le DAS du cœur 386.** `x86_ops_bcd.h:81-105` reprend l'algorithme du 8088 et s'écarte du SDM
sur 24/1 024 entrées. Le DAS (`2F`) est à 95,6 %, en famille R, alors que le DAA (`27`), identique
au SDM, est à 100 %. D1 § 5.2 ne le cite pas. *Documenté* (SDM) et mesuré.

**A5 — Toute correction du cœur partagé doit être gardée par UC.** `opLOCK` sert aussi au 286
(`386_ops_prefix.cs:228-229`, `386_ops.h:10968-10969`). Le 386 PRM documente le #UD sur LOCK pour le
386 (§ 14.7, item 9), pas pour le 286 : *inconnu*. Le corpus 80286 (« LOCK rarely prepended »)
tranchera. Même question pour AAA/AAS (106h) sur le 286. L'AF d'ADC, lui, vaut pour toutes les UC.

**A6 — Le corpus 8086 comme contre-épreuve.** Son silicium Intel permet de vérifier les comportements
non documentés mesurés sur l'AMD D8088 : seuil de DAA/DAS, SETMO/SETMOC, OF par CL, drapeaux empilés
par #DE. C'est une réponse partielle au risque 3 de D1 (« quel silicium fait foi »), absente du rapport.

**A7 — Le mot à cheval a deux facettes** : l'offset FFFFh, qui doit replier dans le segment (386 PRM
§ 14.7, item 7), et l'adresse linéaire FFFFFh, qui doit replier à 1 Mo sur un bus de 20 bits.
`readmemw`/`writememw` traitent `s + a + 1` sans masque (`808x.c:79`, `:110`) : à FFFFFh, l'octet
haut vient de 100000h. Côté oracle, c'est le cas de PB-07 (section B, lecture D6), traité par
`h_pad_ram`. Une correction de `readmemw` en mode matériel doit couvrir les deux facettes, en accord
avec D6. *Déduit*.

**A8 — Des entrées R9 du domaine ne sont classées par aucune lecture.** PB-46, PB-47, PB-49
(section B) et PB-79 (section A, la MMU) n'apparaissent dans aucune table de D1 à D7 : D1 ne les cite
qu'en dépendance, et D6 ne nomme pas PB-79. Elles dévient déjà de PCem ; la question de G13 est de
savoir si elles vont plus loin vers le matériel :
- PB-46 : drapeaux empilés mesurés, règle inconnue (voir K6) ;
- PB-47 : déjà #DE, rien à faire ;
- PB-49 : Intel ne garantit l'inhibition que pour le premier chargement de SS ; au-delà, inconnu ;
- PB-79 : le bus ouvert est déjà la réponse la plus plausible.

À classer explicitement.

**A9 — La voie TSS en accepte trop.** Outre le DPL et la présence, `loadcscall`/`loadcsjmp`
(`x86seg.c:888-894` pour CALL, `:582-588` pour JMP) et la porte d'interruption de `pmodeint` (`:1967-1973`) acceptent une TSS
**dans la LDT** (`seg & 4`), alors qu'Intel exige TI = 0 (« Must specify global in the local/global
bit », pages CALL et JMP). `pmodeint` ne vérifie pas non plus que la TSS est disponible (`:1963-1999`).
Le « modèle de la correction » que D1 propose pour PB-39 est donc lui-même incomplet. *Documenté*.

**A10 — Ce qui manque au plan U0–U5.**
- **U0.**
  - Deux re-bases, à décider : lever le filtre REP de `SstProbe` (K9) et appliquer `f_umask` dans
    `Sst386Probe` (K8).
  - Étendre `Moo.cs` à `REGS`/`RMSK` avant toute `sst286-probe` (K3).
  - Corriger aussi les textes de PB-51, PB-32 et PB-87 (K10, C14), et ne pas inscrire le NT du JMP
    comme défaut (K1).
  - Inscrire A1 à A4 si le périmètre « écarts mesurés » est retenu.
  - `tools/perfbanc/` n'est pas suivi (`git status`) : un outil de porte doit être commité avant de
    servir de preuve de coût.
- **U1.**
  - Les prédictions sur `F6.x`/`F7.x` sont conditionnées par K6 ; la négation sous REP dépend de A3
    (iii) et de K9.
  - DAA/DAS suivront la règle « 9Fh si AF », qui n'est pas celle d'Intel (K5).
  - Ajouter A1 et A2.
  - Récupérer aussi `D2.2`/`D2.3`, témoins de PB-02, hors manifeste comme `D3.2`/`D3.3` ; `D1.2`/`D1.3`,
    les formes `,1` saines, comme témoins ; et `D4`/`D5`.
- **U2.** Le fuzzeur 8086 en flux, qui a trouvé PB-87, peut servir de contrôle de localisation en mode
  matériel. Ajouter A7.
- **U3.**
  - Le contrôle « aucune autre forme ne bouge » ne tient pas pour LOCK : environ 3 % des cas de presque
    toutes les formes 386 sont des LOCK.
  - MOVSX r16 demande un nouveau handler dans une table générée (`386_ops_table386.cs`,
    `tools/ops386-table.py`) ; la garde ne peut pas se loger dans le `ILLEGAL` partagé. À régler avec
    D7, car le cas diffère de PB-78, dont la garde tient dans le handler.
  - Garder les corrections par UC, 286 compris (A5).
- **U5.** Retirer « le NT des JMP de tâche » (K1). Étendre la table d'attentes aux codes d'erreur des
  deux pages (K2) et à la condition TI = 0 (A9).
- **Avant d'inscrire F2 et M.** `669C`, `669D` et `66CF` à 0 % sont un signal systématique. Il faut
  d'abord écarter un artefact de la sonde (les bits hauts d'EFLAGS posés par `h_setregs386`) :
  *inconnu*.

**Questions à ajouter pour Julien.**
1. Re-baser la ligne de base 8088 sans le filtre REP, et celle du 386 avec `f_umask` ?
2. Modéliser les drapeaux que le 8088 empile au #DE (règle non documentée, à tirer de SST), sans quoi
   la correction du débordement ne fait rien gagner sur `F6.x`/`F7.x` ?
3. Pour un 386, quel manuel fait foi, le 386 PRM ou le SDM, sur le NT après un JMP de tâche et sur AAA ?
4. Les corrections du cœur partagé (LOCK, AAA/AAS) valent-elles pour le 286 sans mesure ?
5. Inscrire les défauts lus ici (A1 à A4, `rep()`, le LOCK du 8088) ?
6. Classer les entrées R9 du domaine (A8) ?

---

## 4. Classement par PB, après contre-lecture

| PB | D1 | Après contre-lecture | Chemin chaud | Vérification | Raison du changement ou réserve |
|---|---|---|---|---|---|
| 01 | (a) | **(a)** | tiède | SST 8088 `10`–`15`, `18`–`1D` (au manifeste) | aucune |
| 02 | (a) | **(a)** | non | SST `D3.2`/`D3.3`, témoins `D2.2`/`D2.3`, `D1.2`/`D1.3`, tous à récupérer | OF pour un compte > 1 : déduit |
| 45 | (a), groupé | **(a) conditionnel** | non | `F6.7` : +1 143 sûrs (signe) ; le reste dépend de K6, K9, A3 (iii) | débordement à inscrire ; « ~100 % » non tenable en l'état |
| 87 | (a) | **(a), priorité basse** | oui (`FETCH`) | banc C# (386 PRM § 14.7, item 8) ; fuzzeur 8086 en flux | site premier `:145` (C14) ; rare pour l'invité |
| 39 | (a)/(b) | **(a)** nominal / **(b)** tous les codes d'erreur | non | pm-check, attentes tirées de la doc | contradiction #TS/#GP sur toutes les vérifications (K2) ; TI = 0 (A9) |
| 40 | (a) | **(a)** | non | pm-check (ESP 0x7000) | aucune |
| 41 | (c) | **(c)** | non | mesure sur un 386 réel | amender l'entrée ; FS/GS nuls aussi (C15) |
| 43 | (b) | **(b)** | non | banc ; fuzzeur `--0f 20…26` | aucune |
| 44 | (d) | **(d)** | — | — | absorbé par PB-43 |
| 50 | (a) | **(a)** | tiède | SST 80286, qui exige le lecteur `REGS` (K3) ; banc 386/486 | aucune |
| 51 | (a) | **(a)** | oui | banc (NOP en FFFFh → INT 0Dh, IP empilé 0) | IP empilé 0 : OS/2 Museum (source secondaire) |
| 77 | (a) | **(d)** proposé | non | `cpu-config-check`, C# seul | aucun effet pour l'invité (C20, K12) |
| 78 | (b) | **(b)** | non | banc (0F 07 → INT 6 sur 486) | Collins vérifié |
| 11 | (d) | **(d)** | — | — | temps seul |
| 12 | (d) | **(d)** | — | — | temps seul |
| 32 | (a) | **(a)** | non | pm-check (code 0102h) | ligne fautive `:1660` (K10) |
| 42 | (d) | **(d)** | — | via LTR à TI = 1 | le correctif utile est LTR, à inscrire |
| (voisin de 39) NT au JMP | « non inscrit, à corriger » | **(c)**, contradiction 386 PRM/SDM | — | — | PCem suit le 386 PRM (K1) |

---

## 5. Défauts non inscrits, vérifiés

Ceux qui tiennent. « Mesuré » renvoie à une ligne de base, « lu » à la seule lecture du C.

### 5.1 Cœur 8088/8086 (`808x.c`, `is8086` compris)

| # | Défaut | Site C | Mesuré / lu | Vrai comportement |
|---|---|---|---|---|
| 1 | Débordement de DIV/IDIV non détecté (avec le 80h/8000h et la négation sous REP) | `:3571`, `:3615`, `:3723`, `:3746` | mesuré (`F6.6`, `F7.6`) | documenté (Intel ; 386 PRM § 14.7, item 11 ; README SST) |
| 2 | DAA : écart propre au silicium | `:1592-1610` | mesuré (`27`) | mesuré seulement ; règle « 9Fh si AF », observation publiée ; le SDM donne le résultat de PCem (K5) |
| 3 | DAS : CF de l'ajustement bas réutilisé, seuil sur l'AL ajusté | `:1665-1683` | mesuré (`2F`) | SDM pour le cas observé ; règle « 9Fh si AF » (K5) |
| 4 | REP LODSB/LODSW : AL/AX jamais chargés | `:1110-1147` | mesuré (`AD`) | documenté |
| 5 | SETMO/SETMOC traités comme SHL | `:2798`, `:2920`, `:3068`, `:3219` | mesuré (`D0.6`) | mesuré seulement (non documenté par Intel) |
| 6 | OF des décalages par CL jamais posé | `:3068-3119`, `:3219-3271` | mesuré (`D2.4`) | mesuré (Intel : indéfini) |
| 7 | AAM/AAD : SF/ZF calculés sur AX (A1) | `:3280-3293` | lu, prédit `D4`/`D5` | documenté (SDM) |
| 8 | SAR par CL : CF faux au-delà de 8/16 sur un opérande négatif (A2) | `:3104`, `:3258` | lu, prédit `D2.7`/`D3.7` | déduit (définition de SAR) |
| 9 | `rep()` : 6Eh exécuté comme OUTSB ; DS: absent ; préfixe de segment perdu avant REP (A3) | `:950-969`, `:915`, `:1201-1204` | lu | déduit |
| 10 | LOCK traité comme une instruction d'un octet (A3) | `:3489-3492` | lu | déduit ; effet limité |
| 11 | Mot à l'offset FFFFh ou à l'adresse linéaire FFFFFh sans repli (A7) | `:73-80`, `:105-111` | lu ; non exercé par SST | documenté (386 PRM § 14.7, item 7) pour l'offset ; déduit pour 1 Mo |
| 12 | Lecture principale de `FETCH` au repli : extension de PB-87, pas une entrée neuve | `:145` | lu | documenté (386 PRM § 14.7, item 8) |
| 13 | Drapeaux empilés par l'INT 0 non recalculés (lié à PB-46, et à K6) | `:3595`, `:3637`, `:3730`, `:3754` | mesuré (47 cas `D4`) | mesuré ; règle inconnue |

### 5.2 Cœur 286/386/486 (en-têtes `x86_*.h`, partagés par le 286 sauf mention)

| # | Défaut | Site C | Mesuré / lu | Vrai comportement |
|---|---|---|---|---|
| 1 | AF d'ADC (3 840/131 072) | `x86_flags.h:299-307` | mesuré (F1) | documenté |
| 2 | LOCK sans #UD | `x86_ops_misc.h:707` | mesuré (E1) | documenté pour 386/486 ; 286 inconnu (A5) |
| 3 | BT* : décalage non signé | `x86_ops_bit.h:8`, `:28`, `:48`, `:68` (et BTS/BTR/BTC) | lu ; explique une partie de F1 seulement | documenté (386 PRM § 17.2 ; SDM Table 3-2) — ampleur à instruire (K7) |
| 4 | MOVSX r16,r/m16 en ILLEGAL | `386_ops.h:1432`, `:1958` | mesuré (`0FBF`, `670FBF`) | mesuré seulement (386EX) |
| 5 | AAA/AAS à la façon du 8086 | `x86_ops_bcd.h:3-15`, `:41-53` | mesuré (R, `37`/`3F`) | SDM, contredit par le 386 PRM ; la mesure suit le SDM (déduit) |
| 6 | AAD : SF ; AAM : ZF (et SF) | `x86_ops_bcd.h:23`, `:35` | mesuré (`D5` à 50,2 %, `D4`) | documenté |
| 7 | AAM 0 : base 10 au lieu de #DE | `x86_ops_bcd.h:31-32` | mesuré (`D4`, environ 1/256) ; décrit dans PB-46 | documenté (SDM) |
| 8 | DAS : écart au SDM (A4) | `x86_ops_bcd.h:81-105` | mesuré (`2F`) | documenté (SDM) |
| 9 | Limite des données non contrôlée en mode réel, hors MOV (E2/E3) | handlers sans `CHECK_*` | mesuré | documenté (386 PRM § 14.7, item 7) ; chemin chaud |
| 10 | LTR ne contrôle ni type, ni bit occupé, ni présence, ni TI | `x86_ops_pmode.h:230-262` | lu | documenté (386 PRM, page LTR) |
| 11 | Voie TSS : ni DPL, ni présence ; TSS acceptée dans la LDT ; type non vérifié par la porte d'INT (A9) | `x86seg.c:582-588`, `:888-894`, `:1284-1291`, `:761-773`, `:1963-1999` | lu | documenté (pages CALL et JMP) |
| 12 | EXT jamais posé ; limite de l'IDT testée par `addr >= limit` | `x86seg.c:1648`, `:1660`, `:1687`, `:1692` | lu | documenté (386 PRM § 9.7) |
| 13 | LOADALL386 sans contrôle de privilège | `x86_ops_misc.h:930-974` | lu | Collins, source secondaire |

### 5.3 Écartés, ou à instruire avant d'inscrire

- **NT effacé au JMP de tâche** (`x86seg.c:768`) : conforme au 386 PRM, Table 7-2. Ce n'est pas un
  défaut, mais une contradiction avec le SDM (K1).
- **F2 et M** (`669C`, `669D`, `66CF` à 0 %) : non lus ; un artefact de la sonde reste possible.
- **Les drapeaux indéfinis de la famille F1** : le corpus les déclare lui-même indéfinis (`f_umask`)
  et ils ne sont pas des défauts. Le CF de BT* mis à part (K7).

---

## Sources

**Dans le dépôt** :
- registres et constats : `PCEM_BUGS.md` (PB-01, 02, 03, 07, 11, 12, 32, 39 à 47, 49, 50, 51, 77, 78,
  79, 87) ; `VERIFICATION.md` (:42, :554, :731-743, :3836-3990) ; `sst-baseline.tsv`,
  `sst386-baseline.tsv` ; `vectors/sst/MANIFEST.sha256`, `vectors/sst386/MANIFEST.sha256` ;
- C de PCem : `pcem-dev/src/cpu/808x.c`, `386.c`, `386_dynarec.c`, `x86seg.c`, `cpu.c` ;
  `pcem-dev/includes/private/cpu/x86_flags.h`, `x86_ops_bcd.h`, `x86_ops_bit.h`, `x86_ops_misc.h`,
  `x86_ops_mov.h`, `x86_ops_mov_ctrl.h`, `x86_ops_call.h`, `x86_ops_pmode.h`, `x86_ops_atomic.h`,
  `386_ops.h`, `386_common.h` ;
- C# : `iXtal26/Cpu/808x.cs`, `x86_flags.cs`, `386.cs`, `386_ops_prefix.cs`, `x86seg.cs`,
  `iXtal26/pc.cs`, `iXtal26/Host/SdlSetup.cs` ;
- outillage : `tools/iXtal26.Diff/SstProbe.cs`, `Sst386Probe.cs`, `Moo.cs`, `Oracle.cs` ;
  `tools/oracle/Makefile`, `tools/oracle/harness_386.c` ;
- autres lectures : `D7-mecanisme.md` (:432, :609-610).

**Intel** :
- 386 PRM :
  - § 14.7, items 6 à 11 — https://pdos.csail.mit.edu/6.828/2008/readings/i386/s14_07.htm
  - § 7.6, Table 7-2 — https://pdos.csail.mit.edu/6.828/2018/readings/i386/s07_06.htm
  - pages CALL, JMP, AAA, BT — https://pdos.csail.mit.edu/6.828/2018/readings/i386/CALL.htm (et
    `JMP.htm`, `AAA.htm`, `BT.htm`)
  - § 17.2, Bit(BitBase, BitOffset) — https://www.scs.stanford.edu/05au-cs240c/lab/i386/s17_02.htm
- SDM :
  - vol. 3A, Table 7-2 — https://xem.github.io/minix86/manual/intel-x86-and-64-manual-vol3/o_fe12b1e2a880e0ce-251.html
  - vol. 3A, TSS 16 bits, p. 7-16 — https://xem.github.io/minix86/manual/intel-x86-and-64-manual-vol3/o_fe12b1e2a880e0ce-254.html
  - vol. 2, Table 3-2 — https://cdrdv2-public.intel.com/774492/325383-sdm-vol-2abcd.pdf
  - vol. 2, pages DAA, DAS, AAD, AAM, BT — https://www.felixcloutier.com/x86/daa (et `das`, `aad`,
    `aam`, `bt`)
- Errata « 80286 ARPL and Overlength Instructions », 15 octobre 1984 —
  https://www.pcjs.org/documents/manuals/intel/80286/extra_prefixes/

**SingleStepTests et MOO** :
- README des corpus — https://github.com/SingleStepTests/8088, https://github.com/SingleStepTests/8086,
  https://github.com/SingleStepTests/80286, https://github.com/SingleStepTests/80386
- `metadata.json` du 8088 v2 — https://raw.githubusercontent.com/SingleStepTests/8088/main/v2/metadata.json
- `80386.csv` — https://raw.githubusercontent.com/SingleStepTests/80386/main/80386.csv
- format MOO — https://github.com/dbalsom/moo/blob/main/doc/moo_format_v1.md

**Autres** :
- Bochs, `tasking.cc` — https://raw.githubusercontent.com/bochs-emu/Bochs/master/bochs/cpu/tasking.cc
- M. Necasek, OS/2 Museum, 29 octobre 2022 — https://www.os2museum.com/wp/does-eip-wrap-around-in-16-bit-segments/
- R. Collins, LOADALL — https://www.rcollins.org/secrets/opcodes/LOADALL.html,
  https://www.rcollins.org/articles/loadall/tspec_a3_doc.html
- K. Shirriff, « Understanding the x86's Decimal Adjust after Addition (DAA) instruction »,
  janvier 2023, et le commentaire de GloriousCow sur le seuil 99h/9Fh mesuré sur un 8088 —
  http://www.righto.com/2023/01/understanding-x86s-decimal-adjust-after.html
