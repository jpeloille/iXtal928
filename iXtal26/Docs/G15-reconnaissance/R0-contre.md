# G15, reconnaissance — R0-contre : la contre-lecture des noms

Contre-lecture du 4 novembre 2026, au commit `34814ec`, de `R0-noms.md`. Lecture seule : rien construit, rien lancé ;
les comptes viennent de `grep`, `git grep`, `awk` et de scripts Python jetables (`python3 -I`) écrits hors du dépôt
(`/var/tmp/ixtal-g15/R0-contre/`, et le dossier de séance), qui lisent les sources et les sorties de `R0-noms/`.
Classement : **ERREUR** (le constat est faux), **IMPRÉCISION** (ce qui est juste suit), **REMARQUE**, **CONFIRMÉ**.
Niveaux : vérifié (relu ou recompté), déduit, inconnu.

## 0. En bref

1. R0 est solide sur le fond : les grands comptes se reproduisent (1 076 `const`, 398 `using static`, 787 gardes
   `materiel.pb_`, 2 982 marqueurs `// pcem:`, 1 844 et 1 362 lignes longues, 82 renvois `ibm5150`, 1 264 champs
   `<>O`), `private` explicite est bien fait, les neuf chaînes sont réelles.
2. Trois erreurs : `check-doc5150.sh:67` est la ligne 68 ; « 98 noms de constantes sont dans le harnais » vaut 89
   noms, dont 67 dans son code, et seuls 2 passent littéralement à un `Oracle.h_*` ; sur les « 30 paires » de noms
   qui ne diffèrent que par la casse, 11 ne collisionnent pas sous la règle cible (un champ privé reste en
   `camelCase`) et 5 ne viennent pas du C.
3. Les chaînes liées à un membre sont au moins onze, non neuf : `Reference.cs:51` et `:53` portent
   `svga_render_8bpp_highres` dans les états attendus du banc, et `Reference.Nom(s.render)` fait écrire au compilateur
   le littéral `"s.render"` (`[CallerArgumentExpression]`), qui change sans qu'une chaîne du source change.
4. Les comptes de lignes longues (1 018/757 et 518/501, « borne haute ») ne se reproduisent pas : la même simulation
   donne 1 163/898 et 810/786. Ils sous-estiment, et Q6 propose le contraire de la règle qui conditionne le commit.
5. `Microsoft.CodeAnalysis.Workspaces.dll` n'est pas dans `Roslyn/bincore`, c'est vrai ; il est dans le même SDK, à la
   même version de Roslyn, sous `DotnetTools/dotnet-format/`. R0 n'en dit rien ; G14 avait écarté `Renamer` par choix.
6. Deux décisions de forme manquent à Q1 : le style des champs `internal` (2 663 des 2 970 champs hors de la forme,
   les globales du C) et des champs privés `static` ; le compte « hors de la forme » en dépend.
7. Q2 (le nom C sur une ligne `// pcem:`) rend le défaut de `check-doc5150.sh` systématique, et décale les
   numéros de ligne de ~290 renvois `fichier.cs:N` vivants ; R0 ne le dit pas.
8. Le découpage tient, mais dix passes ne sont pas nécessaires : l'hôte (N2) et les locales/paramètres (N9) ne
   changent pas le code de l'émulateur, et E1 et E2 peuvent n'en faire qu'une (§ 3).

## 1. Constat par constat

### 1.1 L'inventaire (R0 § 1.1)

- **8 976 types et membres ; 7 608 hors de la forme** : **IMPRÉCISION**. Le total se reproduit (530 + 3 263 + 1 076 +
  122 + 3 856 + 129 = 8 976 ; 6 303 + 1 256 + 49 = 7 608), mais le genre « types » compte chaque partie d'une classe
  partielle. Commande :
  `awk -F'\t' '$3~/^type_/{k=$7"|"$4; if(!(k in s)){s[k]=1;n++; if($4!~/^[A-Z][A-Za-z0-9]*$/)m++}} END{print n,m}'
  inv-ixtal.tsv` → `452 300`. Il y a 452 types distincts (300 hors de la forme), non 530 (371) : 78 et 71 parties
  de `partial` (les 44 `.Materiel.cs`, `ide`…). Total distinct : 8 898 et 7 537. La conclusion ne bouge pas.
- **4 461 / 411 / 69 champs** : CONFIRMÉ en tant que somme (3 263 + 1 076 + 122 = 4 461).
- **Les 1 076 `const`, 1 046 entiers** : CONFIRMÉ. `git ls-files 'iXtal26/*.cs' | xargs grep -E '\bconst\s'` ne
  compte que 985 lignes ; l'écart est dû aux déclarations multiples (`const int A = 1, B = 2;`, 36 lignes). Le
  type de la liste (`int` 753, `uint8_t` 218…) se reproduit par `awk '$3=="const"{print $8}' inv-ixtal.tsv`. Les
  `const` locaux (19 lignes à 8 espaces ou plus, ex. `x87_noyau.Materiel.cs:2114` et `:2251`, `SdlMenu.cs:1142`)
  n'y sont pas : **REMARQUE**, ils ne sont pas non plus dans les « locales ».
- **Les fonctions locales** : **REMARQUE**, omises. `inv-*.tsv` en relève 26 dans iXtal26 (3 hors de la forme :
  `LRmw`, `DPL`…), 142 dans Diff, 2 dans le banc ; ni le tableau ni N9 ne les nomment. Elles ont leurs paramètres.
- **~570 locales** : CONFIRMÉ (573 dans les sources ; 578 noms hors de `camelCase` dans le PDB, dont 29 générés
  `CS$<>8__locals0`).
- **300 paramètres** : CONFIRMÉ (5 359 lus ; la colonne `param` de `inv-ixtal.tsv`).
- **La forme visée** : **REMARQUE** (le point 6 de § 0). `conforme.py` exige le `PascalCase` pour tout champ non privé.
  Commande : `awk '$3=="field"{a=($6~/private/)?"p":"n"; ...}'` → 2 756 champs `internal` (2 663 hors de la
  forme), 91 `public` (90), 416 privés (217). Les globales du C sont `internal` par la règle de
  `iXtal26.csproj:67-69`. Le .NET ne fixe pas le style d'un champ `internal` (`_camel`, `s_camel` ou Pascal), et le
  banc/Diff ont 74 champs `private static readonly` en Pascal (`ZipOracle`, `BootDiff.cs:149`) que R0 dit « conformes
  ou non selon la règle », quand il compte à tort conformes les 15 de l'hôte. Les noms de PCem en `snake_case` privés
  ont un autre choix à faire : `speakerPos` ou `_speakerPos`. Q1 doit trancher les trois.
- **Hôte conforme (70 noms)** : CONFIRMÉ (2 + 31 + 9 + 7 + 18 + 3 = 70 ; `SdlJoystick.cs`, `HddImage.cs:44` et `:77`
  `MAX_CYLINDERS`, `hd_types` relus).
- **99 % du transcrit et du matériel** : CONFIRMÉ (6 303 + 1 256 = 7 559 sur 7 608).
- **398 `using static iXtal26.` dans 134 fichiers ; 5 et 6 dans Diff et le banc** : CONFIRMÉ
  (`git ls-files … | xargs grep -E '^\s*(global )?using static iXtal26\.' | wc -l`).
- **93 187 lignes de code** : **IMPRÉCISION**, le filtre de `TRANSCRIPTION.md` (commentaires et lignes vides ôtés)
  donne 93 688 ; Diff 23 079 et banc 1 604 se reproduisent. Les 54 108 lignes touchées ne se reproduisent pas :
  le script n'est pas dans `R0-noms/`.
- **Les scripts** : **IMPRÉCISION** de l'annexe A. Sont absents de `/var/tmp/ixtal-g15/R0-noms/` les calculs de
  54 108/8 787/569 lignes, des lignes longues (1 018/757, 518/501), des 654 membres et 3 092 sites, des 413 littéraux
  et des 74. La phrase « les `grep` sont dans le texte » ne couvre pas ces comptes.
- **Les 30 paires** : **ERREUR** sur la nature, chiffre exact. `python3 -I collisions.py inv-ixtal.tsv` → `collisions
  30`. Mais `collisions.py` applique `pascal()` à tous les noms, champs privés compris. Sous la règle cible de R0
  (champ privé en `camelCase`), disparaissent 11 paires : `SdlAudio._factorySpeaker`, `SdlMenu` (`_screen`,
  `_dialogPurpose`, `_imagesRoot`), `SdlSetup._screen`, `keyboard.scancode_set1/2/3` (privés), `ModeMateriel.demande`,
  `rtc.internal_clock`, `_x87_transcendantes.Expm1`. Parmi les 19 restantes, 5 ne viennent pas du C : `Zero`, `Vide`,
  `Classe` (`x87_noyau.Materiel.cs:59`, `:75`, `:95`), `Types` (`mfm_xebec.Materiel.cs:119`) et
  `PoserGroupe_arith_386` (`386_ops_arith.cs:1500`). « Ce sont des distinctions du C par la casse ou le soulignement »
  est donc faux pour 16 des 30. Les exemples cités (`CS`/`cs` `x86.cs:153-160`, `PREFETCH_RUN` `386.cs:314` et
  `prefetch_run` `:245`) sont exacts. Le type `ide`/`IDE` a trois déclarations (`ide.cs:41`, `:80`,
  `ide.Materiel.cs:22`) et deux noms.
- **Les trois CS0542** : CONFIRMÉ (`keyboard_amstrad_`, `keyboard_olim24_`, `keyboard_xt_` ;
  `keyboard_amstrad.cs:44-46`).
- **Les six conteneurs qui commencent par un chiffre** : CONFIRMÉ (`386Materiel`, `386`, `386Common`, `808x`,
  `386Noyau`, `808xNoyau`). **REMARQUE** : `TRANSCRIPTION.md:198-199` écrit déjà la règle de `_808x`.
- **Les collisions non comptées** : **REMARQUE**. Le script ne regarde qu'un conteneur à la fois ; il ne dit rien des
  noms qui se rejoignent par deux `using static` ni des locales et paramètres qui prendraient le nom d'un membre.
  R0 les traite en § 3 (liaison), sans les compter ; le compilateur voit les ambiguïtés, pas les masquages.
- **Les lignes longues** : 1 844 dans iXtal26, 1 362 dans Diff, 13 dans le banc : CONFIRMÉ. **Les 1 018/757 et
  518/501** :
  **IMPRÉCISION**. Simulation indépendante (remplacer chaque identifiant non conforme de l'inventaire par `pascal()`,
  hors commentaire de fin de ligne) : iXtal26 1 163 lignes longues touchées, 898 encore longues ; Diff (inventaires
  d'iXtal26 et de Diff, hors `h_*`) 810 et 786. R0 donne ces nombres pour « au plus » (borne haute) : ils sont plus
  bas que les miens de 12 % et de 36 %. Aucune ligne n'est allongée dans les deux simulations (le `_` retiré
  raccourcit). Le constat tient (plus de mille lignes), le chiffre est à refaire avec le script gardé.
- **Les noms de PCem** (2 533 champs sur 2 757…) : inconnu, non recompté (`pcem-ids.txt` relu : 30 251 lignes).

### 1.2 `private` explicite (R0 § 1.2)

- **« Aucun membre sans modificateur, hors six constructeurs statiques »** : CONFIRMÉ, par une méthode indépendante.
  `awk -F'\t' '$5!="oui"{print $3}' inv-ixtal.tsv` ne sort que `ctor` (4), `enum_member`, `local`, `local_function`,
  `param` ; Diff un `ctor`, le banc un. Contre-épreuve par `grep` sur les déclarations à 4 espaces
  (`static`, `int`, `string`, `void`, `readonly`…) : les seules sans modificateur sont les six constructeurs
  (`386.cs:89`, `disc_img.cs:68`, `lpt.cs:284`, `materiel.cs:638`, `Oracle.cs:98`, `GardeBanc.cs:69`), des fonctions
  locales (`Program.cs:198` `static string Cote`, `BootTest.cs:300`, `Emu8000.Materiel.cs:92`) et les paramètres
  positionnels de `record struct` (`DisplaySettings.cs:30`).
- **`iXtal26.csproj:67-69`, `:39`** : CONFIRMÉ. IDE0040 absent de `.editorconfig` (`grep -c` 0) ;
  `Microsoft.CodeAnalysis.CSharp.CodeStyle.dll` présent sous `sdk/10.0.114/Sdks/Microsoft.NET.Sdk/codestyle/cs/`.
- **« Ces extinctions ne font rien aujourd'hui »** : CONFIRMÉ par l'absence : six répertoires transcrits (`Cdrom`,
  `Ide`, `Joystick`, `Lpt`, `Mfm`, `Scsi`) ne sont pas dans le glob de `.editorconfig:40` et construisent sous
  `TreatWarningsAsErrors` (annexe B de R0). **REMARQUE** : le rendre actif demande une sévérité dans la règle
  (`dotnet_naming_rule.….severity`), que R0 ne précise pas.

### 1.3 Les constantes (R0 § 1.3)

- **1 046 entières, 267 / 636 / 143** : CONFIRMÉ (`awk '{o[$5]++}' consts.tsv` → `enumC 267`, `define 636`, `ixtal
  143`).
  Seulement 2 noms sont à la fois dans `pcem-defines.txt` et `pcem-enums.txt` ; le script donne la priorité au
  `#define`.
- **« 43 familles d'un `enum` de PCem »** : **IMPRÉCISION**. Ce sont 43 couples (fichier, préfixe), dont 8 ne
  portent qu'une constante : 35 familles de deux constantes ou plus (259 constantes) et 8 isolées
  (`awk '$3=="enumC"' groupes.tsv` → 35, 259). Surtout, le regroupement est par préfixe, non par `enum { … }` du C :
  `pcem-enums.txt` ne donne pas l'identité de l'`enum`. Un `enum` C# par préfixe peut couper un `enum` du C.
- **132 familles, 847 constantes, 199 isolées** : CONFIRMÉ (`awk '$4>=2{f++;c+=$4}' groupes.tsv` → `132 847`).
- **48 familles de bits, 223 constantes** : CONFIRMÉ (`cut -f5 groupes.tsv | sort | uniq -c`).
- **Usages : 351 `case`, 246 masques, 82 jamais lues, 106 index** : CONFIRMÉ comme sorties de `consts.tsv`
  (`awk '$12>0'` → 351, `$11>0` → 246, `$9==0` → 82, `$10>0` → 106).
- **Trois `enum` de PCem déjà en C# ; ceux de `HState.cs:26-40`** : CONFIRMÉ (`hdd_file.cs:28`, `video.cs:49`,
  `dbopl.cs:178`). **REMARQUE** : le dépôt a 16 `enum`, dont `Classe : byte` (`x87_noyau.Materiel.cs:95`), un `enum`
  à type sous-jacent, précédent utile à Q5 pour les `uint8_t`.
- **« 98 noms de constantes sont aussi des identifiants de `tools/oracle/` »** : **ERREUR**. 98 est le nombre de
  lignes de `consts.tsv` marquées `harnais`, non de noms : `awk '$19=="harnais"{print $3}' | sort -u | wc -l` → 89.
  Le script cherche le nom dans le texte brut des `.c` et `.h`, commentaires compris
  (`re.findall(r'[A-Za-z_]\w*', open(…).read())`, `consts.py`) : sans commentaires ni chaînes, il en reste 67. Les 22
  autres sont des mots (`ADD`, `B`, `DE`, `ES`, `JMP`, `SPECIAL`, `ZERO`) ou des constantes de PCem seulement
  (`IDE_NONE`, `FPU_NONE`, `RTC_MONTH`…).
- **« Leur valeur traverse le P/Invoke en `int` »** : **IMPRÉCISION**. Une recherche des noms de constantes sur les
  lignes
  de Diff qui contiennent `Oracle.h_` n'en trouve que deux : `ROM_IBMAT` (`AtProbe.cs:36`) et `FPU_387`
  (`X87Cases.cs:287`, `X87Parity.cs:223`). Le vrai flux passe par des champs : `Oracle.h_set_romset(pc.romset)`
  (`BootDiff.cs:734`, `CpuConfigCheck.cs:91`), `h_set_fpu(cpu_c.fpu_type)` (`BootDiff.cs:738`, `CpuConfigCheck.cs:93`),
  `h_set_gfxcard(pc.gfxcard)` (`:747`, `:95`), `h_set_cpu(cpu_manufacturer, cpu)`. Si Q5(a) retype `romset`,
  `gfxcard`, `fpu_type`, ces appels veulent un `(int)` : quatre ou cinq sites de Diff, que R0 ne nomme pas.
- **112 constantes lues par Diff ou le banc** : CONFIRMÉ (`awk '$18~/Diff|perfbanc/'` → 112).

### 1.4 Ce qui dépend des noms (R0 § 1.4)

Chaque chaîne relue au fichier ; l'effet annoncé est jugé ensuite.

- `386.Materiel.cs:313-315` `GetMethod("ILLEGAL")` (relu : oui) : **IMPRÉCISION** : le champ est dans `_386_materiel`
  (statique), l'exception est un `TypeInitializationException` au premier accès, donc du mode matériel seul (les
  appels de `386_ops_mov_seg.cs:123` etc. viennent du mode matériel, déduit). « La machine ne démarre plus » vaut pour
  « tout », non pour « aucun ». Déduit. Le message de `:315` (`"_386.ILLEGAL introuvable…"`) nomme aussi le membre.
- `x87.Materiel.cs:40` `h.Method.Name == "opFCLEX"` (relu : oui) : CONFIRMÉ : `partout(h => … ? efface_b(h) : null)` ;
  un nom absent n'efface rien, sans erreur ; PB-59 ; mode « tout » seul.
- `MaterielCas.cs:514` `GetField("memcycs")!` (relu : oui) : CONFIRMÉ (`NullReferenceException`).
- `MaterielCasPit.cs:52`, `:55` (relu : oui) : CONFIRMÉ.
- `MaterielCasFrequence.cs:85-87` `"_iir_x"`, `"_iir_y"` (relu : oui) : CONFIRMÉ, et c'est le plus insidieux :
  `EndsWith` ne trouve plus rien, le `foreach` ne remet rien à zéro, aucune erreur.
- `OpsCount.cs:32` `"opNonTranscrit"` (relu : oui) : CONFIRMÉ : `missing` reste vide, la porte `ops-count` imprime un
  décompte faux (visible au tsv).
- `GardeBanc.cs:93` `GetMethod("setadc8")!` (relu : oui) : CONFIRMÉ. `GardeBanc.cs:108` imprime aussi
  `copie.Method.Name` (message seul).
- `Is486Banc.cs:107-108` `"opCMPSB_a16"` (relu : oui) : CONFIRMÉ (deux occurrences).
- `RenduBanc.cs:158-159`, `:164-165` (relu : oui) : CONFIRMÉ, et la preuve du rouge est écrite : `compare_ref.py:569`
  compare l'état imprimé de R et de C (`etats["R"][0], etats["C"][0]`).

- **« Neuf » sites** : **IMPRÉCISION**, il y en a au moins onze. (a) `Reference.cs:51` et `:53` : les états attendus de
  `Rendu8bpp` contiennent `svga_render_8bpp_highres` ; l'arbre C devra attendre le nouveau nom, R l'ancien
  (`sed -n 26,150p Reference.cs | grep -o '[a-z]*_[a-z0-9_]*'` → `svga_render_8bpp_highres` 2 fois). (b)
  `RenduBanc.cs:158`
  `Reference.Nom(s.render)` : `[CallerArgumentExpression(nameof(d))]` (`Reference.cs:224`) fait inscrire par le
  compilateur le texte de l'argument, `"s.render"`, dans le tas `#US` : un renommage du champ `render` change un
  littéral qu'aucune ligne du source ne montre. R0 écrit que `nameof` n'a qu'un site et conclut « neuf littéraux ». Il
  faut compter les `[Caller*]` : `git grep -nE '\[Caller'` → un seul site, `Reference.cs:224`, un seul appelant
  (`RenduBanc.cs:158`). Vérifié.
- **Le message de `GardeBanc.cs:108`, `RenduBanc.cs:160`** : REMARQUE, ils s'impriment mais ne sont pas comparés.
- **Les chaînes qui nomment le C** : 413 littéraux de Diff : **IMPRÉCISION**, ne se reproduit pas. Chaînes de Diff
  égales
  à un nom de champ, constante, méthode, propriété ou membre d'`enum` d'iXtal26 : 482 (noms de trois lettres au
  moins) à 548 (tout), 503 si seuls les noms hors de la forme comptent (347 distincts). Le script n'est pas gardé.
  La conclusion (ne pas y toucher) est juste, `BootDiff.cs` (148 chez moi, 121 chez R0) et `VgaProbe.cs` en tête.
- **`h_state` et `HState.cs`** : CONFIRMÉ en lecture (`LayoutKind.Sequential`).
- **Réflexion** : CONFIRMÉ complet pour les motifs `GetField`, `GetMethod`, `GetFields`, `.Method.Name`,
  `typeof(…).Get`,
  `Enum.`, `JsonSerializer`, `StackFrame`, `Caller*`, `Activator` sur iXtal26, Diff et le banc. Les `e.GetType().Name`
  (une trentaine, Diff) nomment des types de la BCL. `SstProbe.cs:98` : `PropertyNameCaseInsensitive = true` ✓.
- **`<>O` : 1 264 champs ; 201 méthodes générées** : CONFIRMÉ pour les champs (`awk '$1=="field"&&$2~/<>O/' md-ixtal.tsv
  | wc -l` → 1 264, 68 types). **IMPRÉCISION** pour les méthodes : 201 est le nombre de méthodes dont le nom commence
  par
  `<` ; 200 sont des `g__`/`b__`, la 201e est le point d'entrée `<Main>$`.
- **« G14 n'y a jamais eu affaire »** : CONFIRMÉ. `grep -n '<>\|g__\|b__\|DisplayClass\|CompilerGenerated'
  tools/ildump/Program.cs tools/listings-jit.sh tools/renommage/Program.cs` → rien. Les noms de ces membres se
  dérivent de
  noms de méthodes, pas de types ; la thèse tient. Les `b__N_M` ne dépendent que de l'ordre de déclaration.
- **`PCEM_BUGS.md`** : CONFIRMÉ à ±1 (613 `.cs` contre 614 selon le motif de `grep -o`, 182 `materiel.pb_` contre 183,
  241 `*Reproduit*`, 208 `*Corrigé en mode matériel*`, 24 `fichier.cs:N`).
- **`Docs/ibm5150`** : CONFIRMÉ (82 renvois `chemin.cs#symbole`, 72 distincts). **`check-doc5150.sh:67`** : **ERREUR**
  de
  ligne : `grep -qw -- "$sym" "$f"` est à la ligne **68** (`sed -n 66,68p`), cité deux fois (§ 1.4, § 3). Le défaut
  décrit
  est réel : `grep -qw` sur le fichier entier, commentaires compris.
- **Les commentaires du harnais** (`harness.c:318`, `:784`, `:861`, `:961`) : CONFIRMÉ, relus.
- **Les générateurs** : CONFIRMÉ pour les huit fichiers (les en-têtes disent « GÉNÉRÉ »). **REMARQUE** : trois traits
  que
  R0 ne dit pas. Les générateurs écrivent directement dans `iXtal26/Cpu/` avec la racine du dépôt en dur
  (`open(R+'iXtal26/Cpu/x87_ops_808x.cs','w')`, `gen46.py:47`) ; `gen46.py` lit les `.cs` produits par `gen43`/`gen44`
  et retouche leur texte par expressions régulières sur des noms de membres (`geteal`, `writememq`…, `gen46.py:398`) ;
  `genmat.py` embarque des gabarits C# de ~480 lignes (`@writememq`, `_x87_noyau.off`). Renommer les `.cs` avant les
  générateurs casse `gen46`, non seulement « défait le renommage ». La porte « régénéré = commité » n'a jamais été
  jouée ici : R0 ne dit pas si les huit fichiers se régénèrent aujourd'hui à l'octet.
- **Les consommateurs hors iXtal26/Diff/banc** : **REMARQUE**. `tools/softfloat/Banc` ne lit que SoftFloat. Les
  `.attendus`
  et `tools/gates` citent des fichiers `.cs` en commentaire, jamais un membre.

### 1.5 La preuve étendue aux membres (R0 § 1.5)

- **`g14.txt:30-31`, `Program.cs:13-16`, `:39`, `:195` d'`ildump`** : CONFIRMÉ (les membres sont refusés ; la clé est le
  nom qualifié ; `ildump` vide les paramètres avec leur nom, `Program.cs:24`).
- **« Roslyn/bincore sans l'API `Workspaces` : pas de `Renamer` »** : CONFIRMÉ à la lettre, **REMARQUE** de fond.
  `find /usr/lib/dotnet -name 'Microsoft.CodeAnalysis*Workspaces*.dll'` → `sdk/10.0.114/DotnetTools/dotnet-format/`
  seulement, et la version est la même que celle de `bincore` (`5.0.0-2.26522.126+0427b9ab` lue dans
  `Microsoft.CodeAnalysis.dll` des deux dossiers et dans `Workspaces.dll`). Rien à installer : le `HintPath` de
  `tools/renommage/iXtal26.Renommage.csproj:23-32` pourrait viser ce dossier. `Renamer` (membres, paramètres,
  locales, groupes de méthodes, surcharges) ferait ce que R0 range dans « il faudrait écrire » ; il ne fait ni les
  chaînes ni la vérification de liaison. G14 l'a écarté sciemment (`VERIFICATION.md:16778` : « ne s'appuie pas sur
  `Microsoft.CodeAnalysis.Workspaces` »), pour des types seuls. À R0 de dire que l'option existe et de la mettre à Q ou
  en N0 ; je ne la tranche pas. Le SDK a changé (10.0.113 en G14, 10.0.114 ici) : l'emplacement n'est pas stable.
- **« Les listings ne nomment que des membres dans les en-têtes et les `call` »** : CONFIRMÉ. `ami386dx-387.txt` de
  `ref-R-captures-10.0.14/jit` : 5 174 lignes avec `iXtal26`, dont 3 658 `call`/`tail` (3 517 + 141) ; le reste ce
  sont les en-têtes `JIT compiled … Type:membre`.
- **`ildump` : clé triée par nom qualifié** : **REMARQUE**. Avec une table de membres, l'ordre de tri d'une clé change
  entre R et C (`Pic_` n'est pas à la place de `pic_`) ; la comparaison doit trier après avoir appliqué la table, ou
  comparer par table de correspondance. R0 ne le dit pas.
- **Le garde-fou de Q39 pour les listings** : **REMARQUE**. `listings-jit.sh compare --dll-ref/--dll-autre` (Q39)
  exige que
  les deux jetons désignent la même chaîne ; avec la liste close des littéraux qui changent (R0 § 1.5, pour `ildump`),
  il faudra admettre, côté listings, « une autre chaîne » pour ces onze sites. R0 ne le prévoit que pour `ildump`.
- **L'effet sur l'IL** : CONFIRMÉ comme raisonnement (tas `#Strings`, pas d'opcode ; locales dans le PDB seul ; `enum`
  → substitution de types). Le point sur l'interpolation d'un `enum` (`AppendFormatted<E>` imprime le nom) est juste ;
  vérifié qu'aucun `enum` de `consts` n'est interpolé aujourd'hui (les `enum` existants sont lus par `(int)`,
  `DecodageBanc.cs:130`, `FetchBanc.cs:75`).

### 1.6 Les règles vivantes (R0 § 1.6)

- **`TRANSCRIPTION.md:100-102`, `:132`, `:133`, `:154-174`, `:176-199`, `.editorconfig:30-45`, `fdc.cs:47`,
  `808x.cs:17-19`** :
  CONFIRMÉ, relus, citations exactes.
- **22 blocs `// noms:`, 18 « gardent les noms de PCem »** : CONFIRMÉ à une unité près (Disc 3, Floppy 2, Ics 12,
  Video 5 ; 19 fichiers contiennent « gardent » ; `Emu8000.cs:33` : « gardent leur nom »).
- **R1(e) : 40 lignes** (`TRANSCRIPTION.md:23-24`), **plafond de 260, 249 lignes** (`:49`, `wc -l`) : CONFIRMÉ.
- **R4 et l'`enum`** : **IMPRÉCISION**. R0 écrit que `[Flags]` « serait un C#-isme hors de la liste close de R4
  (`TRANSCRIPTION.md:59-64`) ». Mais l'`enum` lui-même n'est pas dans cette liste (« `delegate` · `class` · propriété
  `ref`
  · `[StructLayout]` · alias · `[MethodImpl]` · `ref` locale ») : l'argument vaut pour tout `enum`, qui est ordonné par
  `PLAN.md:569-570`. R4 est donc à amender pour l'`enum` dans tous les cas, et l'exclusion de `[Flags]` ne se fonde pas
  sur R4. Sur les 16 `enum` existants, 8 sont dans `Host/`, 3 dans `Diag/`, 3 sont ceux de PCem (`hdd_img_type`,
  `fontformat_t`, `SynthMode`), un est du matériel (`Classe`), un de `TimerCheck.cs` : aucun n'est une règle de R4.
- **R2 et les `enum`** : **REMARQUE**. Pour N2-N8 R0 prévoit « le nombre de lignes vives inchangé » ; un `enum` ajoute
  une
  ligne d'en-tête par famille (131 au plus) et en retire aucune ; à tenir dans la porte d'E1/E2.

## 2. Les questions Q1 à Q7

Test : `PLAN.md:569-570` (« les conventions .NET (PascalCase, `private` explicite, `enum` au lieu des constantes
`int`) ») ;
PLAN-G14 décision n° 1 (`PLAN-G14.md:298-300`) ; règle du 18/10 (une question seulement si les décisions la laissent
ouverte).

- **Q1 (la forme d'un nom)** : **ouverte en partie**. Les règles de nommage du .NET (acronymes de deux lettres en
  majuscules, `Fdc`, `Pit`) sont déjà « les conventions .NET » de `PLAN.md` : ce n'est pas une question, c'est la règle.
  Restent ouverts, et R0 ne les pose pas tous : le style des champs `internal` et des privés `static` (§ 1.1), le nom
  d'un conteneur à chiffre (`_808x`), la liste d'exceptions des 19 vraies paires. **IMPRÉCISION/élargissement** : « le
  suffixe `_t` tombe quand rien ne collisionne » n'est pas du PascalCase mais un renommage de plus ; `pic_t` devient
  `PicT` par la règle. À présenter comme une option ou à retirer.
- **Q2 (le lieu de la correspondance)** : ouverte (le plan est muet), mais la proposition **élargit** G15 : une ligne
  `// pcem:` portant le nom C de chaque fonction renommée ajoute jusqu'à ~3 300 lignes de commentaire (2 386 + 886
  méthodes hors de la forme ; le fichier `808x.cs` : 23 marqueurs pour 42 méthodes, relu `grep -c`), et le nom C
  resterait
  dans le fichier : `check-doc5150.sh:68` (`grep -qw`) passerait alors **toujours**, pour tout renvoi `#symbole` périmé.
  Elle décale aussi les numéros de ligne de tout fichier touché : `git ls-files … | xargs grep -E
  '[A-Za-z0-9_.-]+\.cs:[0-9]+'`
  hors journaux (`VERIFICATION.md`, `*reconnaissance*`, `PLAN*`) → 292 renvois vivants (code, `TRANSCRIPTION.md`,
  `Docs/`),
  que G14.5 avait recalés à la main (24 pour +4 lignes). La table générée de `tools/noms/` suffit à la preuve ; la
  lecture
  contre le C est un confort, non une exigence de `PLAN.md`. À poser comme option, avec ce coût.
- **Q3 (les fichiers)** : ouverte (`PLAN.md` ne dit rien des fichiers). **REMARQUE** : l'argument « les renvois
  nomment les
  chemins » est affaibli par G14, qui a déplacé et renommé 18 fichiers en tenant `oracle.tsv`, `PCEM_BUGS.md`, `.cfg`,
  `check-doc5150` à jour (PLAN-G14 décision n° 1 : « la classe, son fichier, son répertoire »). Le coût réel est Q39
  (ordre du tas `#US`), déjà outillé. La proposition « non » reste défendable ; son argument est à reformuler. Et
  `TRANSCRIPTION.md:133` dit déjà « une puce (G14) : `Ics/Intel8259A.cs` » : le miroir du chemin C est déjà percé.
- **Q4 (`materiel.pb_nn`)** : ouverte. Le texte de G14 dit « G15 renommera chaque membre » ; une exception pour `pb_nn`
  est donc une dérogation à demander, ce que R0 fait. Chiffres relus : 787 gardes, 76 fichiers (`grep -cE`),
  `.editorconfig:93-99`. **CONFIRMÉ**.
- **Q5 (les enums)** : ouverte à moitié. `PLAN.md` dit « `enum` au lieu des constantes `int` » **sans exception** ; Q5
  (b) (les
  48 familles de bits, 223 constantes) et (c) (les 199 isolées) s'en écartent, R0 le dit pour (b) seulement. (a) va plus
  loin que la lettre : retyper les champs, paramètres et retours qui ne portent que ces valeurs (la lettre ne parle
  que des
  constantes). Les arguments de (b) sont faibles ou mal placés : `[Flags]` n'est pas plus étranger que l'`enum` à R4
  (§ 1.6) ;
  le cast à chaque accès de registre est un argument contre le retypage des champs, non contre l'`enum` des valeurs
  nommées, qui peut garder des champs `uint8_t` et un cast à l'écriture (`(byte)Stat.IFull`). À poser en deux temps :
  (1) quelles constantes deviennent `enum` (la lettre : toutes celles qui forment un ensemble), (2) quels champs sont
  retypés.
- **Q6 (lignes de plus de 120)** : ouverte, mais la proposition est **à corriger**. Le contrôle est `git diff -U0 |
  grep '^+'`
  (mémoire du dépôt : « lignes ≤ 120 : bloquant » ; R1-contre de G14, constat 7) : toute ligne touchée au-delà de 120
  est
  « neuve », qu'elle soit plus longue ou non. « Une ligne que le renommage seul n'allonge pas garde sa coupe » revient à
  demander que le contrôle admette 898 lignes d'iXtal26 et 786 de Diff (mes chiffres), ou à dispenser le renommage ;
  G14 a fait l'inverse (sept lignes déjà longues d'`EmuTablesCheck.cs`, `e5d51e0`). Il faut dire que c'est une
  dérogation à
  la règle, non une application, ou chiffrer le recoupage (de l'ordre de 1 700 lignes).
- **Q7 (SoftFloat)** : ouverte (`PLAN.md` dit « Normaliser le C# » ; G13 décision n° 10 lui donne le pacte du
  transcrit).
  Chiffres CONFIRMÉS (53 fichiers, 59 types, 75 méthodes, 49 champs et constantes). Le renommer touche aussi `Cpu/` (les
  `softfloat.softfloat_flag_*` de `x87_noyau.Materiel.cs:2251`).
- **Ce qui n'est pas ouvert** : CONFIRMÉ. Seul ajout : « les paramètres et les locales suivent la forme .NET » inclut
  les
  fonctions locales (§ 1.1).

## 3. Le découpage (R0 § 2)

- **N0 (outils) avant tout** : nécessaire, comme G14.0. **N1 (règles)** : peut s'ouvrir avec N2, sans code d'émulateur.
- **Dix passes** : non nécessaires. La mémoire du dépôt (« série entière : seulement si l'émulateur change ») et le
  principe de la preuve (IL et listings identiques par la table) permettent :
  - **N2 (hôte, outils)** : sous-ensemble ciblé, non une série entière (l'hôte n'est pas le cœur ; G14 l'a pratiqué
    pour l'outillage). Le banc reste dû à chaque commit par `PLAN.md` (« à chaque commit de G15 »).
  - **N9 (paramètres, locales)** : l'IL des locales est identique **sans** table (R0 l'écrit), celui des paramètres
    l'est à
    l'option près ; si le DLL est identique (`ildump` 0/0/0, listings 0/0/0), la série est redondante : sous-ensemble.
  - **E1 + E2** : une seule passe (même preuve, substitution de types) ; la séparation n'a de raison que de poser Q5 par
    étapes.
  - Restent sept séries entières (N3 à N8 et E) au lieu de dix. Deux familles de N3-N7 à IL égal peuvent se fusionner si
    l'on accepte une reprise plus grosse (PLAN-G14 décision n° 11).
- **Un enum avant les noms** : à ne pas faire (même lignes touchées deux fois) ; l'ordre de R0 est le bon.
- **N8 et les générateurs** : les huit fichiers sont à renommer **par** leurs générateurs, avant ou avec les `.cs`
  qu'ils
  lisent (`gen46.py`), non après. À écrire dans N8.
- **Ce qui est nécessaire à la preuve et que N0 ne dit pas** : tri d'`ildump` après la table (§ 1.5), liste close côté
  listings, cas `[CallerArgumentExpression]`, états attendus de `Reference.cs`.

## 4. Ce que R0 omet

1. Les fonctions locales (170) et les `const` locaux (§ 1.1).
2. Le style des champs `internal` et des champs privés `static` (§ 1.1, Q1) : 2 663 noms dépendent de ce choix.
3. Les onze sites de chaînes (§ 1.4), dont les états attendus du banc (`Reference.cs:51`, `:53`) et le littéral du
   compilateur (`CallerArgumentExpression`).
4. L'existence de `Workspaces`/`Renamer` dans le SDK (§ 1.5).
5. Les ~290 renvois `fichier.cs:N` vivants, sensibles à Q2 et Q6.
6. Le fait que le défaut de `check-doc5150.sh` devient systématique avec Q2.
7. Les appels `h_set_*` de Diff qui passent des champs (`pc.romset`, `cpu_c.fpu_type`, `pc.gfxcard`) (§ 1.3).
8. L'ordre et l'entrée des générateurs (`gen46` lit des `.cs`), leur écriture en dur dans `iXtal26/Cpu/` (§ 1.4).
9. Les paires comptées à tort ou qui disparaissent sous la règle cible (§ 1.1).
10. Les scripts des comptes de lignes (annexe A).

## 5. Bilan chiffré

- Constats relus : une centaine (comptes, renvois fichier:ligne, citations de règles).
- **ERREUR : 3** — `check-doc5150.sh:67` (c'est `:68`) ; « 98 noms … dans le harnais » (89 noms, 67 dans le code,
  2 sur un appel `Oracle.h_*`) ; la nature des 30 paires (16 de 30 ne sont pas des distinctions du C qui collisionnent).
- **IMPRÉCISION : 14** — types comptés par partie de `partial` (452/300 distincts) ; `const` locaux ; 201 méthodes
  dont `<Main>$` ; 43 familles (35 + 8, par préfixe) ; neuf chaînes (au moins onze) ; effet d'`ILLEGAL` (mode matériel
  seul) ; 413 littéraux non reproduit (482 à 548) ; 93 187 lignes (93 688) ; 1 018/757 et 518/501 non reproduits ;
  scripts
  manquants ; « traverse le P/Invoke » ; R4 sans `enum` ; Q1 (`_t`) et Q6 (règle des 120) ; 22 blocs/18 (19 « gardent
  »).
- **REMARQUE : 11** — champs `internal` ; `Workspaces` ; tri d'`ildump` ; listings et littéraux ; générateurs ; renvois
  `.cs:N` ; Q2 et `check-doc5150` ; `Classe : byte` ; collisions entre conteneurs non comptées ; sévérité des règles de
  nommage ; R2 et les `enum`.
- **CONFIRMÉ : ~45** — dont : `private` explicite (0 hors 6 constructeurs, par `awk` et par `grep`), 1 046 constantes
  (267/636/143), 398 `using static`, 787 gardes, 2 982 marqueurs, 1 844/1 362 lignes longues, `1 264` champs `<>O`, 82
  renvois `ibm5150`, les trois CS0542, les six conteneurs à chiffre, huit des neuf chaînes (effet et ligne), les quatre
  commentaires du harnais, les règles vivantes citées, `Workspaces` absent de `bincore`, Q4, Q7.
- Questions Q1-Q7 : toutes ouvertes en partie ; deux propositions élargissent G15 ou s'écartent de la règle des 120
  (Q2, Q6) ;
  Q1 (`_t`) ajoute un renommage ; Q5 s'écarte de la lettre de `PLAN.md` (R0 le dit pour (b) seulement).

## 6. Corrections à porter au plan

1. `check-doc5150.sh:67` → `:68`, dans § 1.4 et § 3.
2. « 98 noms » → 89 noms, 67 dans le code du harnais ; dire que deux constantes seulement traversent le P/Invoke
   littéralement, et nommer les champs (`pc.romset`, `cpu_c.fpu_type`, `pc.gfxcard`) que Q5(a) obligerait à caster.
3. Les 30 paires : refaire le compte sous la règle cible (19), dire lesquelles viennent du C et lesquelles d'iXtal26.
4. Les chaînes : au moins onze ; ajouter `Reference.cs:51`, `:53` et le littéral `[CallerArgumentExpression]`.
5. Q1 : ajouter le style des champs `internal` et des privés `static` ; présenter la chute de `_t` comme une option.
6. Q2 : chiffrer (jusqu'à ~3 300 lignes de commentaire), dire l'effet sur `check-doc5150.sh:68` et sur les 292 renvois
   `.cs:N` ; la poser comme une option.
7. Q3 : reformuler l'argument à la lumière de G14 (18 fichiers déplacés, outillés).
8. Q5 : poser (1) le périmètre des constantes (la lettre), (2) le retypage des champs ; retirer l'argument « R4 » de
   `[Flags]`
   ou dire que R4 n'a pas non plus l'`enum`.
9. Q6 : dire que garder la coupe d'une ligne touchée déroge à la règle des 120 ; refaire le compte (1 163/898, 810/786)
   avec un script gardé.
10. § 1.5 : mentionner `Workspaces`/`Renamer` dans `DotnetTools/dotnet-format` (même Roslyn) comme option de N0.
11. § 1.5 et N0 : tri d'`ildump` après la table ; liste close des littéraux aussi dans `listings-jit.sh` ; les
    générateurs
    avant (ou avec) les `.cs` qu'ils lisent.
12. Annexe A : garder les scripts des comptes de lignes, de littéraux et d'usages ; corriger 201 (200 + `<Main>$`),
    530/371 (452/300 types distincts), 43 (35 + 8) et 93 187.
13. Inventaire : ajouter les fonctions locales et les `const` locaux à N9.
