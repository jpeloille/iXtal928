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

`g5w-recipe.sh` fabrique `/tmp/g5w` depuis les disques de `os/` (copies, AUTOEXEC sans
`KEYB FR`, CMOS de type 46) et vérifie ses empreintes contre `g5w.sha256` ; `cfg/` porte les
configurations des portes (`*.cfg.in` : gabarits de l'espace disque).
