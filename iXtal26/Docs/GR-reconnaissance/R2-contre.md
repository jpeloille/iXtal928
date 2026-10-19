# GR.2 — La profondeur de G10 : contre-lecture du rapport R2

Le 20 octobre 2026. Contre-lecture de `R2-g10.md`, en lecture seule du dépôt (`master`, `d56d161`) : rien de construit,
rien de lancé. Les fichiers:lignes cités ont été relus ; chaque compte a été refait par une commande notée.
Les durées viennent de `/var/tmp/ixtal-g13/series/*.tsv` (hors dépôt).

**Niveaux** : *documenté* (un texte du dépôt ou une norme le dit), *déduit*, *inconnu*. **Marques** : **C** confirmé,
**K** corrigé, **A** ajouté (le rapport l'a manqué). Verdict d'ensemble : les comptes tiennent, deux attributions et
deux durées sont à corriger, quatre points manquent, et aucune des six décisions n'est vraiment ouverte (§ 4).

## 1. Les comptes refaits

**C — 30 étiquettes de `scsi_cd_command`.** `awk 'NR>=816&&NR<=1557 && /^ *case |^ *default/'` sur `scsi_cd.c` :
30 étiquettes de premier niveau (899, 903, 922, 961, 980, 989, 1009, 1039, 1124-1126, 1200, 1214-1215, 1248-1249, 1272,
1343, 1359-1361, 1385, 1406, 1416, 1501, 1505, 1513, 1534, 1548, 1553), hors les `case` imbriqués (TOC 1016-1030,
INQUIRY 1433-1466). SEND DVD STRUCTURE (1553) tombe dans le `default` (1554). Documenté.

**C — 16 sous l'oracle.** Les CDB de `atapibanc.py:350-462`, comptés par un script jetable : ATAPIBANC (22 paquets)
envoie
00h, 03h, 12h, 15h, 25h, 28h, 43h, 4Ah, 55h, 5Ah (et D8h, le `default`) ; ATAPIAUD (20 paquets) 00h, 03h, 42h, 43h, 45h,
47h, 4Bh, 4Eh, 55h, A5h. L'union fait 16 codes. Documenté. **C — 8 en C# seul** : `Cdb(` de `MaterielCasAtapi.cs`
(01h:361, 08h:356, 0Bh:362, 1Ah:357, 51h:360, A8h:272, BDh:297, BEh:262). Les séries `g13b` jouent chaque cas dans les
deux modes (`materiel-cas-pbNNN` et `-materiel`, 0 à 3 s) ; en mode matériel, un CR-587-B refuse 01h, 08h, 0Bh, 1Ah
avant le `switch` (`scsi_cd.cs:893`, `scsi_cd.Materiel.cs:181-197`) : seul le mode PCem atteint l'étiquette. Déduit.
**C — 6 jamais** : `grep -nE '0x(1B|1E|2B|44|AD|BB)'` dans `tools/iXtal26.Diff/{MaterielCas*,R9*,Banc,BootDiff}.cs` ne
trouve aucun CDB du CD (les 0x1B de `MaterielCasMaitre.cs:127` et `R9Aha.cs:89` sont des commandes de l'AHA).

**K — BEh n'est pas à PB-270.** Le tableau du § 1.2 range READ CD (BEh) sous « PB-270 (refus) ». Le CDB est dans
`Pb268` (`MaterielCasAtapi.cs:262`, READ CD brut de 2 blocs depuis le LBA 31) : PCem répond GOOD, le matériel 05/21h.
`commande_absente_materiel` (`scsi_cd.Materiel.cs:185-190`) ne nomme pas BEh. Les 2 048 et 2 352 octets (`:1039-1122`)
restent donc un seul cas, du bord de la capacité. Documenté.

**C — 25 étiquettes de `scsi_zip_command`.** Même `awk` sur `scsi_zip.c:250-974` : 25 étiquettes (285 à 925), plus le
`default` (966). **C — 19 / 1 / 5.** Les CDB de `atapibanc.py:381-432` : 00h, 01h, 03h, 06h, 08h, 0Ah, 12h, 16h, 17h,
1Ah, 1Bh, 1Dh, 23h, 25h, 28h, 2Ah, 2Bh, 2Fh, 5Ah = 19 étiquettes (16h et 17h sont deux étiquettes, RESERVE et RELEASE ;
1Ah et 5Ah deux aussi), plus D8h pour le `default` ; MODE SELECT(6) (`MaterielCasZip.cs:135`) = 1 ; jamais : 04h FORMAT,
0Bh SEEK(6), 0Dh IOMEGA EJECT, 1Eh PREVENT, 2Eh WRITE AND VERIFY = 5. Total 25. Documenté. Précision : IOMEGA EJECT sous
ATAPI appelle `zip_eject()` (`scsi_zip.c:916-918`) ; seule la branche `else` (`scsi_zip_illegal`) est inatteignable.

**C — paquets par banc.** Script Python qui compte les `PACKET(` hors définitions : ATAPIBANC 22, ATAPIAUD 20, ZIPBANC
34.
`.keys` : `grep -c .` donne 81, 78, 198. Documenté.

**C — les 34 cas de `cdimage-check`.** `CdImageCheck.cs:98-133` : 9 images (`Image`) + 4 refus d'ISO + 1 « un
répertoire » + 6 feuilles acceptées + 11 refus de feuille + `Suites` + `Nulle` + `Constats` = 34 (`Debut` à 139, 157,
170,
206, 358). Le § 1.5 du rapport ne détaille que les feuilles (19) ; les 15 autres cas (neuf ISO, quatre refus d'ISO, le
répertoire,
les suites, la lecteur nul, les constats) n'y sont pas nommés. Documenté.

**C — la voie SCSI des canaux.** `pc.cs:1078-1089` : sous `aha1542c`, `cdrom_channel` et `zip_channel` ramenés à -1 avec
un avertissement ; PLAN-G11.md:253-258 (décision n° 2) et :308 (n° 11) ; PCEM_BUGS.md:6631-6649 (PB-283). Documenté.
`tools/ahabanc/ahabanc.S:12-21` : deux disques SCSI. Documenté.

**C — PLAN-G13.md:448-450 non tenu pour le stockage.** `ls tools/gates/bancs` : 21 fichiers `.attendus`, aucun pour
ATAPIBANC, ATAPIAUD, ZIPBANC, AHABANC ni LPTBANC. `series.sh:735-759` : le stockage de G13.7 passe par des cas sur « le
banc de r9-atapi » (la machine, ports conduits de l'outil), non par le programme tapé. Sous « tout », les portes de banc
sont « refusées » (`g13b-tout.tsv`, vérifié). Documenté.

**K — les durées.**
- `g13b` : ZIPBANC 1 742 s, ATAPIBANC 722 s, `-vide` 768 s, ATAPIAUD 749 s, cd-vide 157 s, cd-iso 125 s : confirmés.
- Plage « sur treize séries » : fausse. Il y a 25 séries en mode PCem dans le dossier (`ls | grep -vc 'tout\|sub'`),
  et ZIPBANC y va de **1 380 s (g9e2) à 2 740 s (g9g)**, soit 23 à 46 min, non 25 à 43. La plage 1 515-2 594 s est celle
  des quinze séries de g7e4 à g13b. ATAPIBANC : 704 à 1 415 s sur les 25, comme écrit.
- « Les quatre portes de LPTBANC font 31 à 46 s » : trois (41, 46, 31 s) ; la quatrième, `bd-ami486-lpt-dac-stereo`,
  fait 9 s. Plage réelle 9 à 46 s.
- 3 981 s des 47 104 s : confirmé (`awk` sur `g13b.tsv` : somme des quatre portes, somme des 765 lignes) ; 8,5 %.
- 1 351 s pour BLTBANC : confirmé ; ZIPBANC est la plus longue de `g13b`.

**C — la frappe et le temps.** Le rapport suppose que « doubler la table doublerait les lignes saisies » : le temps de
porte est proportionnel au nombre de lignes de `.keys` : (722 − 125 s de démarrage) / 81 = 7,4 s ; (749 − 125) / 78 =
8,0 s ; (1 742 − 125) / 198 = 8,2 s par ligne. Déduit, mais très serré.

**C — le patron `harness_aha.c`.** `harness_aha.c:1-12` inclut `scsi_aha1540.c` et `scsi_hd.c` ; `h_aha_probe` ;
`BootDiff.cs:694`. Les `BUFFER_SIZE` (`scsi_cd.c:14`, `scsi_zip.c:13`, `scsi_hd.c:13`) et les `scsi_add_data` statiques
(`scsi_cd.c:559`, `scsi_zip.c:204`, `scsi_hd.c:82`) sont bien en collision : une unité de traduction par fichier.
La file de la DSS : `lpt_dss.c:8-17`, `lpt_dac.c:8-15`, `lpt.c:38-39` ; la faute de la file « pleine à 15 » et sa seule
détection par le diff d'instructions (VERIFICATION.md:5650-5664) : confirmées. Documenté.

## 2. Ce que le rapport a manqué

**A1 — L'ATAPI se sonde sans nouveau harnais.** `atapi_device_t` est publique par son en-tête
(`includes/private/ide/ide_atapi.h:33-56`), et `harness_ide.c` inclut déjà `ide.c` et lit `ide_drives[d].atapi`
(`harness_ide.c:14-19`). La moitié « pont » de `h_atapi_probe` (commande, état, positions, `data` haché, `bus`) se
loge dans `harness_ide.c`, sans déplacer un fichier de la liste liée. Seuls `scsi_cd_data_t` et `scsi_zip_data`
(privés à leur `.c`) demandent des inclusions, et celles du port parallèle : `lpt.c`, `lpt_dss.c`, `lpt_dac.c` dans une
même unité (`lpt1_device_p` est statique), sans collision de noms statiques (`dss_*` contre `dac_*`, relu). Le § 2 de
GR.2a en deviendrait deux étapes : le pont (peu de risque pour la liste liée), puis les deux fichiers de données.
Déduit.

**A2 — La moitié de la frappe de ZIPBANC est des octets de données.** Les secteurs 100 et 101 (`atapibanc.py:365-366`)
sont des suites arithmétiques `(i*7+3)&0xFF` et `(i*13+0x5A)&0xFF` ; ils sont tapés trois fois (SECT100 une fois,
SECT101
deux fois), soit 3 × 512 = 1 536 octets, 96 lignes `E` de 16 octets sur les 190 de `zipbanc.keys` (`grep -c '^E '`).
Une boucle de dix instructions dans le .COM les engendrerait, au lieu de les saisir. Gain estimé : une centaine de
lignes, environ 800 s par série (au tarif de 8,2 s la ligne) — sans nouveau mécanisme, sans toucher la recette g5w.
Aussi : les deux lignes de script de G13.7d1 ont coûté 36 lignes saisies (162 à 198), parce que l'une est un secteur
tapé. Cela ne contredit pas D1 mais le précède : on mesure d'abord. Déduit (compté, non joué).

**A3 — Douze modèles de lecteur, deux joués.** `scsi_cd.c:251-456` : 12 entrées de `cd_models` (`grep -c
CD_MODEL_INTERFACE`). Les portes n'emploient que `cr-587-b` (`ami486-atapi.cfg.in:17`, `-audio`, `cd-iso`) et le
générique `pcemcd` (`-vide`). `--cd-model` existe (`BootDiff.cs:379`, `:448`) mais aucune ligne de `series.sh` ni de
`cfg/` ne l'emploie (`grep -rn cd-model tools/gates`). Les chaînes d'INQUIRY (`scsi_cd.c:1457-1490`), la page de
l'identité (`:1633-1635`) et les vitesses des dix autres modèles n'ont aucun témoin sous l'oracle ; ce sont des tables
recopiées. D6 ne voit que la machine ; la lacune est le modèle.

**A4 — Les attendus du mode matériel dépendent aussi du son.** ATAPIAUD joue avec la SB Pro v2 « au volume CD de son
reset » (PLAN-G10.md:326). G13.8 a changé ces volumes de reset (PCEM_BUGS.md:3104-3127, PB-145 et voisins ; décision
n° 13 de PLAN-G13). Les attendus du mode matériel de GR.2c ne sont donc pas ceux des seuls PB-123/214/215/270/272/273 :
le hachage du son change aussi. Déduit.

**A5 — Le banc en C# seul a une limite de 1 200 s et joue deux fois.** `Banc.cs:6-20` : chaque scénario est joué deux
fois
(déterminisme), une séance au-delà de 1 200 s est tuée. ZIPBANC seul en C# : de l'ordre de 500 s (déduit de BLTBANC, 410
s
seul pour 1 351 s en porte, soit un rapport de 3,3 ; 1 742 / 3,3 ≈ 530 s) ; deux jeux ≈ 1 060 s, sous la charge d'une
série
la marge disparaît (BLTBANC a déjà dû monter de 600 à 1 200 s). Le rapport ne le chiffre pas ; il justifie A2 (couper la
frappe) avant D5 (porter ZIPBANC en C# seul).

**A6 — La lecture seule du ZIP dépend des droits de l'hôte.** `zip_load` (`scsi_zip.c:105-114`) met `read_only` quand
`fopen(fn, "rb+")` échoue. Sous un compte root, ou sur un fichier sans bit d'écriture mais ouvert quand même, l'état
ne se pose pas : la porte GR.2d « image en lecture seule » doit s'assurer que l'ouverture échoue (chmod 444 et un compte
ordinaire), sans quoi le `case` `:873-877` ne s'atteint pas. Documenté pour le code, déduit pour l'environnement.

**A7 — Le dénominateur de la couverture.** Les branches `is_atapi == 0` de `scsi_cd.c`, `scsi_zip.c` et `scsi.c`
(dont le `fatal` de 15h sur CD SCSI, PLAN-G11.md:257-258) sont inatteignables sous la configuration d'iXtal. Le compte
de GR.2f doit les sortir du dénominateur, et non les compter « non atteintes ». Le rapport ne le dit que pour
IOMEGA EJECT. Déduit.

**A8 — Les 8 étiquettes « C# seul » ne sont pas vérifiées contre PCem en mode PCem.** Leurs attendus du mode PCem
sont écrits à la main dans `Pb270` (`servie`, `MaterielCasAtapi.cs:353-364`) et `Pb268`. Rien ne les compare à l'oracle.
Le rapport le laisse entendre (« 8 de plus en C# seul ») sans dire que le compte de `gcov` (D2) ne les verra qu'après
GR.2b, quand la table les fera passer par boot-diff.

**A9 — Un cas « répertoire » existe déjà.** `CdImageCheck.cs:112` : `Refus(d, "un répertoire")`, l'image elle-même
désignant un répertoire, à parité. Le trou de PLAN-G10.md:298-300 n'est donc que la forme `FILE` d'une feuille CUE ; le
rapport (§ 1.5) range le tout dans « tous ouverts ». Déduit.

**A10 — Les chiffres de la lecture du rapport à nuancer.** « READ(6) longueur 0 (256 ? `:1135`) » : le code
(`scsi_cd.c:1135`) fait `cdlen = cdb[4]` sans le cas 256 ; ce n'est pas une branche de plus mais un défaut candidat
de même famille que ceux de l'annexe (SFF-8020i : 0 vaut 256 pour READ(6) ; PCem le compte pour 0). Déduit. La `fatal`
de `scsi_cd.c:1581` est bien à la ligne 1581 (l'`R9Atapi.cs:89` aussi), celle de MECHANISM STATUS à 993.

## 3. Les étapes GR.2a à GR.2f, jugées

- **GR.2a** : bonne, à scinder (A1). La sonde de la file de la DSS ne voit pas la faute connue (un seuil lu, non stocké)
  ;
  elle voit les écritures perdues. Le diff d'instructions de LPTBANC (1 000 lectures de 379h) la voit déjà : la valeur
  marginale de `h_lpt_probe` est faible, celle de la sonde ATAPI/CD/ZIP plus forte. Déduit.
- **GR.2b** : bonne, mais commencer par A2 puis A3 (modèles). La table piloterait deux canaux de preuve (oracle,
  attendus) ;
  le § 1.3 le dit bien.
- **GR.2c-2f** : sans objection, avec A4, A6, A7.
- Les 22+20+34 paquets actuels sont en réalité les chiffres de départ : l'étape est « 30 étiquettes + 25 + prologue »,
  soit une quarantaine de lignes de table en plus (déduit).

## 4. Les décisions D1 à D6 : une question pour Julien ?

La règle : une question seulement si les décisions de Julien la laissent ouverte.

- **D1 — La frappe dans DEBUG : pas ouverte.** Un choix d'outillage dans le périmètre de GR.2 (PLAN.md:505-507, « piloté
  par une table »). PLAN-G10 n° 5 fixe le témoin (ATAPIBANC seul, sans pilote DOS) et n° 20 ne parle que de la ligne de
  l'ISO dans `g5w.sha256` ; la recette g5w fabrique déjà des images par émulation en C# seul (G10.2). Ordre de preuve :
  (1) A2, la boucle de données ; (2) mesurer la part de la frappe avant de changer de mécanisme ; (3) si elle reste
  lourde,
  une image de bancs séparée du disque de base (pas dans `c486.img`, dont l'empreinte tient toutes les portes). Une
  porte garde la saisie dans DEBUG. Le rapport range le gain « des dizaines de minutes » comme déduit : le calcul de
  § 1 (8,2 s la ligne) le soutient.
- **D2 — L'outil du compte : pas ouverte.** Tranchée par les règles existantes : le PCem vendoré reste intact et le mode
  PCem ne bouge pas (listings JIT contre M0, PLAN-G13.md:158). Des compteurs dans `scsi_cd.cs` toucheraient ces
  listings ; `gcov` sur un oracle à part, non. Le mécanisme de sonde du mode (`ModeMateriel.Sonde`,
  `MaterielCouverture.cs`)
  couvre déjà la couverture par PB, et le rapport ne le cite pas : à réutiliser pour les gardes propres au C#.
- **D3 — FORMAT du ZIP : pas ouverte.** PLAN-G10 n° 8 (les portes travaillent sur des copies), n° 11 (l'image creuse,
  la sonde de quota, l'effacement sur tous les chemins, le disque et non le tmpfs après l'incident du 4 octobre) et
  `par.sh` (`ESPACE_MIN_GO`). FORMAT écrit 100 Mo non creux : une porte à part sur disque est l'application, non un
  choix.
- **D4 — FILE répertoire, nom non UTF-8 : pas ouverte.** PLAN-G13 n° 9 : les défauts de l'hôte sans pendant matériel
  sont
  corrigés dans les deux modes s'ils protègent les fichiers ou l'hôte, laissés sinon ; PLAN-G10 n° 13 couvre les
  plantages (R9), non les refus. Ici personne ne tombe : c'est une DEVIATION à inscrire (A9 : la forme image est déjà à
  parité), prouvée par `r9-cue` étendu — ce que le rapport propose, sans qu'il y ait à demander.
- **D5 — Les bancs du stockage en C# seul : pas ouverte, c'est une dette.** PLAN-G13.md:165 et :448-450 le décident
  explicitement (« les bancs (ATAPIBANC, ZIPBANC, AHABANC) en C# seul, avec leurs attendus du mode matériel »). G13.7
  l'a
  clos par des cas sur le banc de `r9-atapi`. À rendre dans GR.2b/2d, et l'AHABANC, qui n'est pas de GR.2, à signaler
  comme dette de G13.7e : le dire au prochain compte rendu, pas en question.
- **D6 — Le modèle des machines de plus : pas ouverte.** PLAN-G13 Q5 a tranché les générations (PB-270) ; pour l'ami286
  et l'ami386dx on recopie `ami486-atapi.cfg.in` (`cd_model = cr-587-b`). Une porte MMC-2 est déjà couverte en C# seul
  (`ucr421`, `MaterielCasAtapi.cs:336`, `:343`, `:393`). La vraie lacune est A3 (dix modèles jamais joués), qui n'est
  pas non plus
  une question mais une porte.

**Bilan** : aucune question à poser à Julien. Les six propositions sont résolues par PLAN-G10 n° 5, 8, 11, 13, 20 ;
PLAN-G13 n° 9, 14, Q5 et le texte de G13.7 ; PLAN.md:505-510.

## 5. Les six candidats de l'annexe : un mot chacun

Relus aux lignes citées (toutes justes). Aucun n'est au registre (`grep` des mots-clés dans PCEM_BUGS.md : PB-123,
214, 215 touchent les positions, non l'en-tête).

1. READ SUB-CHANNEL, longueur du sous-canal à 0 : **plausible** (`scsi_cd.c:1392-1402`, l'en-tête n'en est jamais mis à
   jour).
2. READ SUB-CHANNEL, format ≠ 01h rendu en 20h : **plausible**, mais le code d'erreur exact (20h ou 24h) est un déduit.
3. READ HEADER, MSF refusé et mode toujours 1 : **plausible** (`:1200-1212`, le bit MSF est défini par MMC).
4. READ TOC, format inconnu en 21h : **plausible** (`:1029-1033`) ; les formats 3 à 5 existent en MMC, donc le « refus »
   à
   préciser selon le modèle (SFF-8020i ou MMC-2, comme PB-270).
5. READ DISC INFORMATION, tampon non effacé, dernière piste à 1 : **plausible** (`:1343-1357`, seul `memset` autour :
   929, 1225).
6. ZIP, VERIFY et SEEK sans borne : **déjà connu**. PCEM_BUGS.md, PB-126 (`:2625`) écrit « VERIFY, SEEK et FORMAT UNIT
   ne
   sont pas jugés » ; ce n'est pas un défaut neuf mais une omission déclarée (inconnu).

## 6. Ce qui est confirmé tel quel

Le § 1.1 (G11 et G13.7), l'état de GR.2 (« n'a été entamé par aucune étape », seul ZIPBANC a bougé en `69d8b3c`), le §
1.4
(contraintes de l'oracle), le § 1.6 (l'ami286 joue IDECHK ; `rep outsw` est 80186+ ; `ami286-ide.cfg.in` porte
`hdd_controller = ide`), le § 1.7 (le tableau des risques, ligne à ligne : relu contre PLAN-G10.md:256-333), les six
étapes et leurs contrôles négatifs. La date du « 3 à 23 » de PLAN.md:507 : non vérifiée ici (`git log` du 04/10 non
relu).
