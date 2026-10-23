# G14 — Nommer les puces ; le plan, sur reconnaissance

> Écrit le 23 octobre 2026, au commit `1d49a7f` (GR clos, hors l'écoute de GR.3i). Bloc G14 de `PLAN.md`
> (décision utilisateur du 03/10 ; feu vert le 23/10 : « feu vert pour G14 »). Reconnaissance en trois lectures, en
> lecture seule, chacune suivie d'une contre-lecture qui l'a corrigée : `iXtal26/Docs/G14-reconnaissance/` (R0 la
> référence de performance, R1 l'inventaire des puces, R2 ce qui dépend des noms ; `Rn-contre.md`). On retient ici
> les contre-lectures. Chaque constat y cite sa ligne ; ce plan renvoie aux rapports pour le détail.

## Où on en est

G14 commence. Rien n'est renommé. La référence de performance figée, que `PLAN.md` § G15 place « avant G14 »,
n'existe pas encore.

### Ce que les lectures ont trouvé

- **Le banc ne mesure pas encore ce que `PLAN.md` demande.** `tools/perfbanc/` a six bancs et 52 cas. Tous ont été
  écrits en G13 pour mesurer le coût du mode matériel, et aucun ne fait tourner une machine entière. Les boucles de
  Fetch et d'Exec386 tournent avec IF = 0 (`FetchBanc.cs:86`, `Exec386Banc.cs:123`) : le PIC n'y est jamais lu, et
  aucune minuterie n'échoit dans la fenêtre, alors que `pic`, `pit` et `dma` sont les premiers modules renommés.
  - Le 8088 et le 386 sont partiels ; le 286 est absent ; le 486 se réduit à un gestionnaire.
  - La mémoire, la vidéo et le son sont partiels : ni CGA, ni mode graphique SVGA, ni OPL qui joue.
  - Les instructions par seconde de chaque machine sont absentes.
  - GardeBanc et Is486Banc mesurent des copies locales du code : ils ne disent rien d'un renommage.
  (R0 § 1, R0-contre § 1.)
- **Les chiffres consignés ne peuvent pas juger.** Le processeur de l'hôte a changé depuis M5.1 (un i5-14600KF à
  cœurs P et E). .NET est passé de 10.0.12 à 10.0.13 par apt le 21/10. Le bruit d'un processus à l'autre va de 2,5
  à 5 %. Une référence figée doit donc être un binaire, remesuré côte à côte, et non des chiffres. (R0 § 1.4.)
- **L'intervalle de BenchmarkDotNet ne compte pas le bruit entre processus.** Il traite les itérations comme
  indépendantes, et ses demi-intervalles font 0,1 à 0,5 % sur Exec386. « Intervalles disjoints » ne suffit donc pas
  à dire « significatif ». (R0-contre § 3.2.)
- **Il n'existe aucun module CRTC.** Le 6845 est écrit en ligne dans cinq cartes (CGA, MDA, Hercules, PC1512, M24),
  comme dans PCem, qui le recopie lui-même par carte. Seule `vid_mda.Masque6845` porte le nom de la puce, et c'est
  une table du mode matériel. « Le CRTC → `Motorola6845` » demanderait d'extraire un module neuf : c'est un
  refactoring, où l'IL change. (R1 § 1.4, R2 § 1.1 ; Q36.)
- **Quatorze modules ont été retenus pour la puce qu'ils modélisent ; contre-lus, il en reste treize.** La
  contre-lecture sort `ppi_c` : `ppi.cs` fait 19 lignes, et les ports du 8255 sont dans les claviers. Les
  attributions sont plus pauvres qu'annoncé :
  - `serial` est une famille 8250/16450/16550, réglée par `has_fifo` ;
  - `pit` exécute la relecture du 8254 sur toutes les machines (`pit.c:328`) ;
  - les références de NEAT, de l'ALi 1429 et de la Headland ne sont écrites ni dans PCem ni dans `sources/`.
  (R1 § 1.5, R1-contre § 1.)
- **Ni le harnais ni les sondes ne désignent une classe C# par son nom.** `h_state`, sst-probe, boot-diff,
  `VgaProbe.Fields` et les 58 clés de R9 nomment le C. Les journaux des deux séries de GR.1i2 (827 chacune), les tsv,
  les `sst-baseline*` et les attendus ne contiennent ni « .cs » ni « iXtal26.<x> ». La porte « séries identiques à
  l'unité » n'est donc pas faussée par un renommage. (R2 § 1.1-1.2, R2-contre § 1.)
- **Ce qui dépend vraiment des noms :**
  - les `using static` et les références qualifiées ;
  - les 32 lignes `csharp_path` d'`oracle.tsv`, que `check-oracle.sh` vérifie ;
  - les globs de `.editorconfig`, par répertoire (`:40`) et par fichier (`:97`). La contre-lecture montre qu'un `Ics/`
    hors du glob ne fait pas tomber la construction (`Ide/`, `Scsi/` et `Mfm/` y sont déjà hors) ;
  - les listings du JIT : `listings-jit.sh compare` indexe par nom qualifié. Sans table des noms, renommer `pic`,
    `pit` et `dma` donnerait 42 méthodes disparues, 42 apparues et 39 changées sur 1 507 ;
  - `GardeBanc.cs:93` lit `setadc8` de `_808x` par réflexion : hors de G14.
  (R2 § 1.3, R2-contre § 1.)
- **Un remplacement textuel est dangereux.** Les risques :
  - 14 paramètres ou locales `PIT pit` ;
  - des chaînes de PCem (`"dma"`, `"serial1"`, `ami386dx_opti495.nvr`) ;
  - `pit.c` à distinguer de `pit.cs` dans les commentaires.

  `iXtal26.sln` ne contient ni `tools/iXtal26.Diff` ni `tools/perfbanc`. Le refactor de Rider est atteignable par
  son serveur MCP (`rename_refactoring`), sans option pour les commentaires ni les chaînes. (R2 § 1.5, R2-contre
  § 1.6.)
- **La preuve « seuls des noms changent » est à outiller.** Rien d'existant ne compare l'IL ni les journaux de série
  par une table des noms. Le diff inverse a un précédent (`9530e21`). Toutes les captures du JIT de G13 ont été
  prises sous 10.0.12 ; la référence est à refaire. (R2 § 1.7.)
- **Documents.** Sont vivants :
  - `PCEM_BUGS.md` (51 renvois) ;
  - `Docs/ibm5150/` (11, contrôlés par `check-doc5150.sh`) ;
  - `TRANSCRIPTION.md` (R1(e) `:23`, `:131`, `:132` « miroir du chemin C », `:153-165`) ;
  - `oracle.tsv`, le README, les notices, les commentaires des `.cfg` (`ixtal26-286.cfg:18` cite `neat.cs`).

  `VERIFICATION.md` et les plans clos sont des journaux datés. (R2 § 1.4, R1-contre § 1.)

## Le principe

Une puce, un nom : la classe qui modélise une puce prend le nom de cette puce et entre dans `Ics/`. Rien d'autre ne
change. La preuve en est faite à chaque commit, de quatre façons :
- l'IL identique, à la table des noms près ;
- le code machine identique, à la table des noms près ;
- toutes les séries identiques à l'unité ;
- le banc égal à la référence figée d'origine.

## Les étapes

L'ordre : G14.0a, 0b, 0c, 0d (la référence et les outils, sans toucher l'émulateur), puis G14.1 à G14.4 (un commit
par famille de puces), puis G14.5 (la clôture). G14.0 ne dépend pas de Q36 ; G14.1 à G14.4 non plus.

### G14.0a — Les bancs que la référence demande

Sur le patron des bancs existants : un `Preparer` qui pose le mode avant le gel, puis vérifie et imprime son état.
- **Le décodage.** `Exec286` et `Exec486` reprennent les boucles d'Exec386 sous `Reset286` et `Reset486`. Une boucle
  8088 ajoute un ModRM mémoire, un préfixe et une chaîne courte.
- **La mémoire.** Un accès lent : une lecture en ROM, une écriture en mémoire vidéo par le chemin des gestionnaires.
- **La vidéo.** La CGA en texte (`cga_poll`) ; un mode graphique de 8 bpp sur la GD5429 et sur la Trident.
- **Le son.** L'OPL de la Pro v2 avec des voix qui jouent.
- **`MachineBanc`.** Une tranche fixe de `pc.runpc`, sur les neuf modèles de `models[]`, dans les deux modes,
  machine posée sans disque et le PIT programmé. Il imprime son empreinte d'instructions et de cycles. C'est le
  seul banc qui fait passer les IRQ, le PIT et le DMA.

**Portes et preuves :**
- la construction sans avertissement ;
- `--job dry` sur tous les cas ;
- `iXtal26.dll` identique au sha256 avant et après : l'émulateur ne change pas, donc aucune série.

**Contrôles négatifs :**
- une copie de `Preparer` qui diverge exprès est refusée ;
- `MachineBanc` sans le mode posé le dit.

### G14.0b — L'outil de comparaison, et sa résolution mesurée

Un script, `tools/perfbanc/compare-ref.sh`, avec un comparateur en python3 (rien à installer). Pour chaque classe de
banc, il joue R puis C puis C puis R (ABBA) :
- R est le binaire figé, C le candidat ;
- `--launchCount 3`, avec échauffement, itérations et invocations fixés ;
- sous `setarch -R taskset -c` sur les cœurs P, export JSON.

**Les refus.** La passe est refusée, ni verte ni rouge, si :
- le runtime, le SDK, l'environnement du job ou l'affinité diffèrent entre les deux côtés ;
- `dotnet --list-runtimes` change pendant la passe ;
- une série ou une construction tourne à côté.

**Les rouges exacts.** Une allocation par opération qui diffère, un état imprimé (lu dans le journal) qui diffère,
une empreinte d'instructions qui diffère.

**Le rouge statistique** (décision n° 12) :
- l'unité est la moyenne de chaque lancement : six valeurs par côté et par cas ;
- un cas est rouge si la borne basse de l'intervalle non paramétrique à 99 % de C/R − 1 est au-dessus de zéro ;
- un rouge se confirme par une seconde passe ABBA de sa classe ; il n'est retenu que si les deux passes sont rouges.

**Portes et preuves :**
- dix passes A/A (R contre un second worktree au même commit), dont le taux de faux rouges est consigné ;
- le no-op (un commentaire changé) reste vert.

**Contrôles négatifs :**
- sur des JSON modifiés à la main : un runtime différent, la variable de paliers absente d'un côté, 8 octets
  d'allocation de plus, un état imprimé changé. Chacun est refusé ou rouge, avec sa raison ;
- dans des copies jetables, un coût non pliable dosé à ~3 % puis ~10 % dans `execx86` et dans `exec386`, et le même
  dans le rappel du PIT et dans `picinterrupt`. Les rouges sont attendus là où le chemin passe, `MachineBanc`
  compris ;
- une allocation dans `sound_poll`, attendue rouge exact.

La plus petite injection vue rouge est la résolution réelle du protocole. Elle est consignée par cas. Là où elle
est plus grossière que l'écart que le plan exige, c'est dit, et les listings portent la preuve du 0 %.

### G14.0c — Les outils de la preuve des noms

- **La table des noms.** Le format est celui des blocs `// noms:`, lu par les outils : un ancien nom qualifié, un
  nouveau, pour les types, les namespaces et les fichiers.
- **`listings-jit.sh compare --noms TABLE`.** La table est appliquée aux en-têtes et aux corps des listings avant la
  comparaison.
- **`tools/ildump/`.** Un vidage de l'IL de `iXtal26.dll` par `System.Reflection.Metadata`, qui est dans le runtime.
  Il est indexé par nom qualifié, trié, et la table y est appliquée. Il compare aussi les champs, les constantes et
  les métadonnées.
- **Un normaliseur des journaux de série.** Il retire le bruit connu : le temps de l'hôte, le PID, isogen et la
  colonne de durée des tsv. Il compare deux séries journal par journal.
- **Les règles :**
  - `.editorconfig` : `Ics` entre au glob du code transcrit (`:40`) ; les commentaires `:47-53` sont repris ; et
    `:90-91`, qui annonce que G14 renommerait `materiel` et les `pb_nn`, est corrigé, puisque `PLAN.md` ne le prévoit
    pas ;
  - `TRANSCRIPTION.md` : R1(e) `:23`, `:131`, `:132` et `:153-165` sont amendés pour `Ics/`, dans le plafond de
    260 lignes.
- **La répétition générale.** Dans une copie jetable, `pic` devient `Intel8259A` par l'outil de la décision n° 8.
  L'IL et les listings doivent sortir identiques avec la table et rouges sans elle, et le banc vert.

**Portes et preuves :**
- `iXtal26.dll` identique au sha256 (`.editorconfig` ne règle que des diagnostics) ;
- `compare` et `ildump` d'une capture contre elle-même : 0 écart ;
- un sous-ensemble ciblé de la série, puisque des outils partagés changent.

**Contrôles négatifs :**
- une table qui renomme une méthode inexistante est refusée ;
- une constante changée dans une copie sort en écart, avec ou sans la table : la table n'efface pas un vrai
  changement ;
- un journal de série modifié d'un octet utile sort en écart.

### G14.0d — Le gel

- **Le commit R.** Les bancs, les outils, les règles, et la table A/A dans `VERIFICATION.md` (hôte, processeur,
  runtime, moyennes, résolution).
- **Le worktree de référence.** `/var/tmp/ixtal-g14/ref-R`, détaché au commit R et construit une fois en Release.
  Les sha256 de `iXtal26.dll`, `iXtal26.SoftFloat.dll` et `iXtal26.PerfBanc.dll` sont consignés et vérifiés à chaque
  lancement. `roms/` y est lié et `roms.sha256` est vérifié. Ce worktree ne bouge plus jusqu'à la fin de G15.
- **Les captures.** Les listings du JIT et le vidage d'IL au commit R, sous le runtime du jour, gardés sous
  `/var/tmp/ixtal-g14/` et doublés dans `/var/tmp/ixtal-g13/`.
- **Les séries de référence.** La série entière dans les deux modes, au commit R : `g14r` et `g14r-tout`.

**Porte :** R remesuré contre lui-même, vert.
**Contrôle négatif :** l'outil lancé contre un worktree à un autre commit le refuse, par l'empreinte de la DLL.

### G14.1 — Le 8259A, le 8253 et le 8237

`pic` → `Intel8259A`, `pit` → `Intel8253`, `dma` → `Intel8237`, avec leurs `.Materiel.cs`, dans `Ics/` et dans
l'espace de noms `iXtal26.Ics`.
- **Le renommage.** Il passe par l'outil de la décision n° 8, sans rien toucher dans les commentaires ni dans les
  chaînes. Les fichiers se déplacent par `git mv`.
- **Les blocs `// noms:`.** Un par fichier transcrit, sur le modèle de `fdc.cs:10-49` : le fichier C, le conteneur,
  et « les fonctions gardent les noms de PCem ». Celui d'`Intel8253` dit « 8253-5 ; le code est celui de PCem, qui
  fait aussi la relecture du 8254 sur toutes les machines (`pit.c:328`) ».
- **Les documents vivants.** `oracle.tsv` (`csharp_path`), `TRANSCRIPTION.md`, `PCEM_BUGS.md`, `Docs/ibm5150/`, les
  commentaires du code et des `.cfg`. Les lignes longues que le renommage touche sont recoupées par `git diff -U0`.

**Portes et preuves :**
- la construction sans avertissement ;
- `check-oracle.sh` sans dérive ;
- l'IL et les listings identiques par la table (0 changée, 0 disparue, 0 apparue) ;
- les séries entières dans les deux modes identiques à `g14r` et `g14r-tout` après normalisation, SST et fuzzeurs
  compris ;
- le banc vert contre R ;
- la `.so` de l'oracle inchangée au sha256, puisque `tools/oracle/` n'est pas touché ;
- `grep` des anciens noms qualifiés et des anciens `using static` : 0 ;
- aucune ligne neuve de plus de 120 caractères.

**Contrôles négatifs :**
- `compare` et `ildump` sans la table sortent les trois classes disparues et apparues ;
- `oracle.tsv` laissé à l'ancien chemin fait sortir `check-oracle.sh` en « ABSENT C# » ;
- les étiquettes de boot-diff (`"dma[{c}]…"`) sont inchangées : `git diff` de `BootDiff.cs` limité aux sites qualifiés.

### G14.2 — Le 8042, le MC146818 et l'UART

`keyboard_at` → `Intel8042` ; `nvr` → `Motorola146818`, qui quitte `Devices/` ; `serial` → `National8250`. Leur bloc
`// noms:` dit trois choses :
- `National8250` couvre le 8250, le 16450 et le 16550 par `has_fifo` ;
- `Motorola146818` porte aussi `nvrfopen` et `loadnvr`, que l'EEPROM de l'AHA-1542C emploie ;
- l'horloge reste dans `rtc.cs` (décision n° 4).

Mêmes portes et mêmes contrôles négatifs que G14.1.

### G14.3 — Les puces des cartes

`sound_emu8k` → `Emu8000` ; `vid_tkd8001_ramdac` → `TridentTKD8001` ; `vid_unk_ramdac` → `SierraSC1502x`, la famille
que PCem nomme. Mêmes portes ; celles du son (SonBanc, SB16BANC, AWEBANC) et de la vidéo (ET4BANC, la 8900D) sont
dans les séries.

### G14.4 — Les jeux de puces

`opti495` → `Opti82C495`, que PCem nomme (`opti495.c:1`). Les trois autres prennent la référence de leur fiche, si
une fiche publique la confirme pour le code transcrit : `neat` → `ChipsCS8221`, `ali1429` → `AliM1429`, `headland` →
`HeadlandHT18`. La fiche est rangée dans `sources/` et `MANIFEST.tsv`. Une référence que rien ne confirme garde le nom
de PCem, et c'est dit. Les mêmes portes ; les boot-diffs de l'ami286, de l'ami386, de l'ami386dx et de l'ami486 sont
ceux qui les exercent.

### G14.5 — La clôture

`VERIFICATION.md` § G14 contient :
- en tête, une fois, la table des anciens et des nouveaux chemins (décision n° 10) ;
- les commits, les séries, les comparaisons d'IL et de listings avec et sans la table ;
- les passes du banc et leurs résolutions ;
- le sort du 6845 : aucun module en G14 (Q36, tranchée le 23/10).

S'y ajoutent les deux commentaires du harnais qui citent `nvr.cs` et `keyboard_at.cs`
(`harness.c:2092`, `harness_stubs.c:172`), corrigés avec `check-oracle.sh --fix`. La `.so` est reconstruite, égale
hors débogage, comme en GR.4e. `PLAN.md` dit « G14 est fait » et met [ICI] avant G15.

## La vérification

- **L'IL et le code machine** : identiques, à la table des noms près, à chaque commit de G14.1 à G14.4.
- **Les séries** : entières, dans les deux modes, identiques à l'unité à celles du commit R après normalisation ;
  l'émulateur change à chaque renommage, la règle du dépôt les demande donc entières.
- **Le banc** : contre le binaire figé R, jamais contre le commit précédent, selon le protocole de G14.0b.
- **L'oracle** : sa `.so` inchangée de G14.0d à G14.4.

## Les décisions

Le principe de G14, sa portée et sa porte sont décidés (`PLAN.md` § G14 et § G15, 03/10). Les décisions ci-dessous
en découlent ; la règle du 18/10 n'en laisse qu'une question à Julien, Q36, tranchée le 23/10.

1. **La portée : le conteneur seul.** La classe, son fichier, son répertoire et son espace de noms changent ; les
   membres et les types d'état (`pic_`, `pit_`, `PIT`, `PIC`, `dma_t`, `SERIAL`, `keyboard_at_t`) gardent leurs noms.
   *(Le modèle que `PLAN.md` désigne, `Floppy/` et `Disc/` : « les fonctions gardent les noms de PCem »,
   `fdc.cs:47`. G15 renommera chaque membre en PascalCase, `PLAN.md:566` : les renommer en G14 doublerait le
   travail et la preuve.)*
2. **L'espace de noms** est `iXtal26.Ics`. *(Le répertoire et l'espace de noms coïncident dans tout le dépôt, sauf
   `Materiel/`.)*
3. **La forme des noms.** Le fabricant, puis la référence telle que la fiche l'imprime. Le préfixe MC de Motorola
   est retiré ; le grade de vitesse aussi. Un suffixe n'est gardé que s'il change le comportement modélisé (le A du
   8259A : ICW4 et l'AEOI). Une référence qui porte déjà le fabricant ne le répète pas (`Emu8000`). *(Les exemples
   de Julien : `Intel8259A`, `Intel8253`, `Intel8237`, `Motorola6845`.)*
4. **Les puces retenues** : les trois décidées, puis `Intel8042`, `Motorola146818`, `National8250`, `Emu8000`,
   `TridentTKD8001`, `SierraSC1502x` et les quatre jeux de puces. Chacune est un module d'une puce, ou de deux
   exemplaires d'une puce, qui se renomme sans découpage.
   - **Non retenus** : `ppi_c` (19 lignes ; le 8255 n'y est pas modélisé, ses ports sont dans les claviers) ;
     l'OPL (deux puces, trois modules, et une bibliothèque tierce nommée d'après elle) ; les mélangeurs (en ligne
     dans la carte) ; le DSP et le MPU-401 ; les cœurs UC (plusieurs puces chacun) ; le FDC et les cartes vidéo
     (« des devices », `PLAN.md:549`) ; `rtc` (n° 4 bis) ; le haut-parleur, le port jeu et la NMI (aucune puce).
   - **4 bis.** `rtc` et `rtc_h` restent à la racine sous leur nom : fusionner les deux classes du MC146818
     changerait l'ordre d'initialisation de leurs statiques, et l'IL avec.

   *(« Et ainsi de suite », `PLAN.md:549`, laisse la liste au plan.)*
5. **`Intel8253` reste le nom**, bien que le code fasse aussi la relecture du 8254. Le bloc `// noms:` le dit.
   *(Le nom est décidé par Julien, `PLAN.md:548`.)*
6. **La référence de performance** :
   - les neuf modèles de `models[]` pour « chaque machine », `ibmat` et `ami386` compris ;
   - les deux modes, « aucun » (les portes, l'oracle) et « tout » (l'utilisateur) ;
   - les chemins chauds que `PLAN.md` énumère.

   Les cas par PB de G13, GardeBanc et Is486Banc restent dans le banc pour leur usage, hors de la référence.
   *(`PLAN.md` § G15 ; G13.11 a fait de « tout » le mode de l'utilisateur.)*
7. **La forme du gel** : le binaire juge, les chiffres alertent. Le worktree R est remesuré côte à côte. Les chiffres
   consignés ne servent qu'à voir l'hôte dériver. Aucun runtime privé n'est installé : les deux côtés tournent
   toujours sur le même runtime, et une passe où il change est refusée. *(« Contre la référence d'origine, jamais
   contre le commit précédent », `PLAN.md:589` ; aucune installation sans l'accord de Julien.)*
8. **L'outil du renommage** : le refactor de Rider par son serveur MCP (`rename_refactoring`, en préversion d'abord).
   Sa liste des sites touchés est confrontée aux comptes des lectures (iXtal26 : 235 sites dans 69 fichiers ;
   iXtal26.Diff : 193 dans 12, `const` comprises). Les outils hors de la solution passent par une solution provisoire
   hors du dépôt qui les charge. Si la préversion touche une chaîne ou un commentaire de PCem, un script Roslyn
   sémantique prend sa place. Ni `sed` global, ni « rechercher dans les commentaires ». *(« Refactor Rider »,
   `PLAN.md:552`.)*
9. **Le harnais n'est pas touché de G14.0d à G14.4** : la `.so` garde la même empreinte, et c'est la moitié
   « oracle » de la preuve. Ses deux commentaires périmés se corrigent en G14.5.
10. **L'historique n'est pas récrit.** Les registres vivants suivent ; `VERIFICATION.md` d'avant G14, les plans clos
    et les rapports de reconnaissance restent tels qu'écrits. La table des chemins en tête de § G14 fait le lien, et
    le bloc `// noms:` de chaque fichier aussi. *(La pratique du dépôt : annoter, ne pas récrire,
    `VERIFICATION.md:1445`.)*
11. **Un commit par famille** (G14.1 à G14.4), chacun avec sa série entière dans les deux modes et sa passe du banc.
    Cela fait quatre séries et quatre passes, au lieu de treize. Une famille dont une preuve rougit est refaite seule.
    *(La règle du dépôt : la série entière quand l'émulateur change.)*
12. **Le rouge statistique** suit G14.0b, sans plancher fixe : la règle de Julien, « toute régression statistiquement
    significative », n'admet pas de tolérance. Le A/A vérifie le taux de faux rouges ; il ne fixe pas de seuil.
    *(`PLAN.md:591-592` ; R0-contre § 3.2.)*
13. **Le commit** : le préfixe `g14:` pour chaque étape, `plan:` pour ce fichier.

**La question à Julien** :

- **Q36 (G14.5)** — `PLAN.md` écrit « le CRTC → `Motorola6845` ». Il n'existe aucun module CRTC, ni chez PCem ni
  dans iXtal26 : chacune des cinq cartes (CGA, MDA, Hercules, PC1512, M24) porte ses registres du 6845 et son code
  en ligne, et PCem recopie le même masque trois fois. En faire un module serait une extraction, et non un
  renommage : l'IL change, et la preuve « seuls des noms changent » ne tient plus pour ces cinq cartes, sur le
  chemin chaud du rendu.
  *Proposé : pas de `Motorola6845` en G14 ; le 6845 reste dans les cartes, et leurs blocs `// noms:` le nomment. Si
  tu veux le module, il fera une étape à part après G14.4, avec sa propre preuve : séries identiques et banc vert,
  sans l'égalité d'IL.*
  *Validé par Julien le 23/10 : « Q36 : ok pour ta proposition, pas de Motorola6845 en G14 ».*

## Les risques

- **La durée.** Une passe ABBA du banc complet prend plusieurs heures (R0-contre : 34 à 79 min par côté et par
  passage, à itération fixée), plus une série entière dans les deux modes à chaque famille. Rien ne tourne à côté.
- **L'hôte qui dérive** (apt, fréquence, chaleur) : il est neutralisé par le côte à côte et par le refus d'une passe où
  le runtime change. La dérive se lit dans les chiffres consignés.
- **Le refactor qui déborde** dans un commentaire, une chaîne de PCem, ou le paramètre `pit` : la préversion, l'IL et
  le diff le voient avant le commit.
- **L'ordre des statiques** : un déplacement de fichier peut changer l'ordre de compilation. Le vidage d'IL trié par
  nom l'ignore. Les initialiseurs statiques, eux, sont propres à chaque classe et ne bougent pas avec le fichier ;
  les séries le confirment.
- **Le banc moins fin que la règle** sur les cas bruyants (le son, ±3 %) : c'est dit cas par cas, et le 0 % y repose
  sur les listings.
