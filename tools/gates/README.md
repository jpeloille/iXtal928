# Les portes

`series.sh` est LA liste des portes du dépôt, dans l'ordre canonique ; `par.sh` la joue en
parallèle. Une série se joue toujours sur une COPIE FIGÉE de l'outil de diff — jamais sur le
répertoire de build, qu'on continue de modifier pendant qu'elle tourne :

    dotnet build tools/iXtal26.Diff -c Release
    S=/tmp/ix-serie; rm -rf $S; cp -r tools/iXtal26.Diff/bin/Release/net10.0 $S
    cp tools/oracle/libixtal26oracle.so $S/
    tools/gates/g5w-recipe.sh                      # l'espace des portes disque, /tmp/g5w
    rm -rf /tmp/gates-fige; cp -r tools/gates /tmp/gates-fige   # le lanceur aussi, figé
    REPO=$PWD /tmp/gates-fige/par.sh $S/iXtal26.Diff.dll /tmp/serie-N.tsv 10 /tmp/serie-N-1.tsv

`/tmp/serie-N.tsv` : une ligne par porte (nom, rc, durée, verdict), `FIN` à la fin ; à côté,
un journal par porte (`/tmp/serie-N-NOM.txt`). Deux séries se comparent journal par journal,
durées retirées. La quatrième option donne les durées de la série précédente, pour lancer
les plus longues d'abord (62 minutes à dix de front, contre 192 en séquentiel).

Pendant une série : ne pas éditer `series.sh` ni `par.sh` (bash les lit au fil de l'eau), ne
pas toucher `/tmp/g5w` ni le `TMPDIR` de la série (les copies d'images du boot-diff, qui gardent
la date de leur source — un nettoyage par âge les efface en pleine porte).

Le `TMPDIR` des portes est un répertoire par série, EN MÉMOIRE : `/tmp/ixtal-par/NOM` par défaut,
sur le tmpfs (`TMPDIR_SERIE` pour le changer ; le disque, `/var/tmp/ixtal-par/NOM`, se demande
explicitement — mesuré, il ne coûte rien de plus). Chaque boot-diff y écrit la trace de
l'oracle, huit octets par instruction, jusqu'à 2,5 Go, et ses copies d'images. `par.sh` exige
d'abord 1,5 Go libres par voie, 15 pour dix (`ESPACE_MIN_GO`), libres AU SENS DU QUOTA :
`/tmp` est un tmpfs à quota par utilisateur, 80 % de sa taille, que `df` ne montre pas et que
`par.sh` lit par `quotactl_fd`. Il refuse la série en dessous (retour 3), vide ce répertoire au
départ, et l'efface à la fin si toutes les portes sont vertes ; sinon il le garde pour la
relecture. Ce ménage ne vaut que pour un répertoire juste sous `/tmp/ixtal-par/` ou
`/var/tmp/ixtal-par/` : un `TMPDIR_SERIE` fourni ailleurs n'est jamais vidé. L'incident du
4 octobre (VERIFICATION.md § G10.0) : deux séries de neuf voies ont dépassé ce quota et fabriqué
des rouges. Une trace que l'oracle n'a pas pu écrire en entier est maintenant dite (« TRACE
TRONQUÉE », retour 3), et la trace est effacée sur tous les chemins de sortie du boot-diff,
signaux compris (SIGKILL excepté).

`PORTES="nom1 nom2 …"` ne joue que ces portes : une validation ciblée, pour un changement qui ne
touche pas l'émulateur (l'outillage, la documentation, l'hôte). La série entière se joue une fois
par étape qui change l'émulateur.

Une série à la fois sur la machine : `par.sh` prend un verrou `flock` sur
`/var/tmp/ixtal-par/.verrou-machine` avant d'examiner l'espace, et le garde jusqu'à la fin. Une
seconde série attend son tour (« en attente du verrou machine »). Les portes héritent du
descripteur : une série tuée garde la machine tant qu'une de ses portes tourne encore.

`g5w-recipe.sh` fabrique `/tmp/g5w` depuis les disques de `os/` (copies, AUTOEXEC sans
`KEYB FR`, CMOS de type 46, et depuis G10.2 le disque amorçable du XTIDE, partitionné et formaté
par émulation en C# seul, `xtide-format.keys`) et vérifie ses empreintes contre `g5w.sha256` ; `cfg/` porte les
configurations des portes (`*.cfg.in` : gabarits de l'espace disque).

Les portes du CD (G10.3, `cdimage-check` et `r9-cue`) n'ont pas d'espace préparé : chacune écrit
les images d'isogen (`tools/isogen/isogen.py`) dans un répertoire de son `TMPDIR`, les vérifie
contre `tools/isogen/isogen.sha256` — un générateur qui dérive rougit la porte —, et les efface en
sortant, signaux compris (SIGKILL excepté). Parmi elles, une image CREUSE de 2,5 Go : 64 Kio seulement
comptent au quota, ce qu'isogen vérifie par une sonde avant de l'écrire.

Les portes de l'ATAPI (G10.4) montent le lecteur sur l'unité IDE 2 de l'ami486. Leur disque est
`iso-2048.iso` d'isogen, que la recette g5w écrit dans le WORK (`isogen.py DOSSIER iso-2048.iso`,
l'image seule) et dont `g5w.sha256` garde l'empreinte ; elle n'est jamais écrite, donc pas copiée
par côté. Le BIOS de l'ami486 ne touche pas le canal secondaire : `--expect-cd CANAL,vide|image`
exige le lecteur et son pilote des deux côtés, sans quoi un lecteur perdu des deux côtés laisserait
le boot-diff vert. Le banc ATAPIBANC (`tools/atapibanc`) parle ATAPI aux ports sous DOS ;
`r9-atapi` et `r9-cdcfg`, en C# seul, écrivent les images d'isogen comme `r9-cue`.

L'audio CD (G10.5) : `bd-ami486-atapi-audio` monte `mixte.cue` d'isogen (une piste de données, deux
pistes audio), que la recette écrit aussi dans le WORK avec `mixte.bin`. ATAPIAUD
(`atapibanc.py --audio`) y joue les deux pistes ; la sonde du CD de boot-diff compare, quand un lecteur
est monté, ce que reçoit `givealbuffer_cd` (empreinte, blocs, échantillons non nuls) et les volumes CD
de la carte. `--expect-cd-son` exige des échantillons non nuls des deux côtés : sans lui, un CD resté
muet des deux côtés laisserait la porte verte.

Le lecteur ZIP (G10.6) : `bd-ami486-zip-banc` monte `zip100.img`, un disque vierge de 100 663 296
octets que la recette écrit dans le WORK, sur l'unité IDE 2 (`zip_channel`), chargé à la fin de
l'amorçage (`zip_path`). L'image est copiée par côté, comme les disques durs : ZIPBANC
(`atapibanc.py --zip`) y écrit deux secteurs, et boot-diff compare les deux copies à la fin. `r9-zip`,
en C# seul, écrit sa propre image vierge dans le TMPDIR.

L'Adaptec AHA-1542C (G11) : les portes `bd-*-aha-*` montent la carte et ses disques SCSI aux ID 0 et 1
(clés `hdc_` et `hdd_`). La recette prépare quatre familles de fichiers :
- les disquettes de DOS 5 et de Windows 3.11 (`dos5-1.img`, `dos5-3.img`, `win3-3.img`) ;
- un disque vierge de 20 Mio (`scsi20.img`) ;
- le disque de DOS (`scsic.img`), tiré par émulation du script de `bd-ami486-aha-format` (`aha-format.keys`) ;
- les CMOS sans disque des trois machines (`aha*.nvr`).

`--expect-aha` exige la carte des deux côtés. Sa sonde (140 champs) refuse tout `fatal()` de l'oracle et
toute garde R9 côté C# : la suite d'un `fatal()` de l'oracle n'est pas comparable. `bd-ami486-aha-banc`
tape AHABANC (`tools/ahabanc`, GNU as) dans DEBUG ; la carte y écrit `nvr/.aha1542c.nvr` (22h), dans le
répertoire de la porte. `r9-aha` et `r9-scsihd`, en C# seul, écrivent leurs disques vierges dans le TMPDIR.

Les Sound Blaster de G12 : les portes `bd-pc-sb*` et `bd-ami486-sb*` montent la carte par `--sndcard` ou par la
section du device d'un .cfg de `cfg/` (`mixaddr` de la SB 2.0). La sonde du son lit, pour toute SB, le type du
DSP, le mélangeur de la carte et le volume CD qu'elle pose ; `--expect-sb` exige la carte des deux côtés.
SBBANC a un script par carte (`sbbanc.py --carte`, `sbbanc-CARTE.keys`), saisi dans DEBUG sur le 5150 ;
`sbbanc.keys` reste celui de la Pro v2.
La SB 16 (G12.1) : `bd-ami486-sb16` exige aussi le DMA 16 bits (`--expect-sb 220,7,1,5`), et la sonde du DMA
(`h_dma_probe`, 29 champs) est comparée dès qu'une SB est montée. SB16BANC (`tools/sb16banc`, GNU as) est saisi
dans DEBUG sur l'ami486, DOS 5 amorcé du disque SCSI de G11 (`ami486-sb16-banc.cfg`, `scsic.img`) ;
`sb16-filter-check` compare les coefficients du FIR, `r9-sb16` est en C# seul.
