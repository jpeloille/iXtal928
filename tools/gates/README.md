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
pas toucher `/tmp/g5w` ni les `/tmp/ixtal-*` (les copies d'images du boot-diff, qui gardent
la date de leur source — un nettoyage par âge les efface en pleine porte).

`g5w-recipe.sh` fabrique `/tmp/g5w` depuis les disques de `os/` (copies, AUTOEXEC sans
`KEYB FR`, CMOS de type 46) et vérifie ses empreintes contre `g5w.sha256` ; `cfg/` porte les
configurations des portes (`*.cfg.in` : gabarits de l'espace disque).
