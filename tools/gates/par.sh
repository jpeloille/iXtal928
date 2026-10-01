#!/usr/bin/env bash
# par.sh DLL SORTIE.tsv [PARALLÈLE=10] [RÉFÉRENCE.tsv]
#
# Joue la série de tools/gates/series.sh EN PARALLÈLE. Chaque porte devient un petit script
# (numéroté dans l'ordre canonique), puis xargs -P les lance, les plus longues d'abord (durées
# de la référence). Les portes disque tournent chacune dans $WORK/par/NOM (CMOS et
# configurations copiés). Le .tsv est reconstruit à la fin dans l'ordre canonique, un journal
# par porte à côté de lui : SORTIE-NOM.txt.
#
# DLL doit être une COPIE FIGÉE (avec libixtal26oracle.so à côté) : on ne joue jamais une série
# sur le répertoire de build. Ne jamais toucher $WORK ni /tmp/ixtal-* pendant une série.
#   REPO : la racine du dépôt (défaut : déduite de l'emplacement de par.sh ; à poser quand on
#          joue une copie figée de par.sh, hors du dépôt).
#   WORK : l'espace des portes disque, fabriqué par g5w-recipe.sh (défaut /tmp/g5w).
set -u
DLL=$1; OUT=$2; P=${3:-10}; REF=${4:-}
REPO=${REPO:-$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)}
WORK=${WORK:-/tmp/g5w}
D="${OUT%.tsv}.par"; rm -rf "$D"; mkdir -p "$D/g" "$D/r"
LOGP="${OUT%.tsv}"
N=0
emit() { local f; N=$((N+1)); f=$(printf '%s/g/%03d-%s.sh' "$D" $N "$1"); cat > "$f"; }
run() { local name=$1; shift
  emit "$name" <<G
#!/usr/bin/env bash
t0=\$SECONDS
res=\$(cd $(printf %q "$REPO") && dotnet $(printf %q "$DLL") $(printf '%q ' "$@") 2>&1); rc=\$?
printf '%s\trc=%d\t%ds\t%s\n' $(printf %q "$name") \$rc \$((SECONDS-t0)) "\$(echo "\$res" | grep -v '^\s*\$' | tail -1)" > $(printf %q "$D/r/$(printf %03d $N)-$name.line")
echo "\$res" > $(printf %q "$LOGP-$name.txt")
G
}
runw() { local name=$1 nvr=$2 dest=$3; shift 3; local W="$WORK/par/$name"
  emit "$name" <<G
#!/usr/bin/env bash
t0=\$SECONDS
rm -rf $(printf %q "$W"); mkdir -p $(printf %q "$W/nvr"); ln -s $(printf %q "$REPO/roms") $(printf %q "$W/roms")
cp -r $(printf %q "$WORK/nvr/default") $(printf %q "$W/nvr/"); cp $(printf %q "$WORK")/*.cfg $(printf %q "$W/")
cp $(printf %q "$WORK/$nvr") $(printf %q "$W/nvr/.$dest.nvr")
res=\$(cd $(printf %q "$W") && dotnet $(printf %q "$DLL") $(printf '%q ' "$@") 2>&1); rc=\$?
printf '%s\trc=%d\t%ds\t%s\n' $(printf %q "$name") \$rc \$((SECONDS-t0)) "\$(echo "\$res" | grep -v '^\s*\$' | grep -E 'Vert|DIVERG|Image [CDEF]' | tr '\n' ' ')" > $(printf %q "$D/r/$(printf %03d $N)-$name.line")
echo "\$res" > $(printf %q "$LOGP-$name.txt")
G
}
cd "$REPO"
source "$REPO/tools/gates/series.sh"
for f in "$D"/g/*.sh; do n=$(basename "$f" .sh); n=${n#*-}
  s=""; [ -n "$REF" ] && s=$(awk -F'\t' -v n="$n" '$1==n {gsub("s","",$3); print $3}' "$REF")
  echo "${s:-9999} $f"; done | sort -rn | cut -d' ' -f2 > "$D/ordre"
[ -n "${DRY:-}" ] && exit 0
xargs -a "$D/ordre" -P "$P" -I{} bash {}
cat "$D"/r/*.line > "$OUT"; echo FIN >> "$OUT"
