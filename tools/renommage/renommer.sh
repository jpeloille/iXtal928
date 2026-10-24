#!/usr/bin/env bash
# SPDX-FileCopyrightText: 2026 Julien Peloille
# SPDX-License-Identifier: GPL-2.0-only
#
# renommer.sh ARBRE TABLE [ETAPE] — le renommage sémantique des types de la table (G14.0c ; PLAN-G14.md, décision
# n° 8 ; Q38, tranchée par Julien le 24/10 : « Le script »). Program.cs en dit les règles. Les étapes :
#   1. la construction de tools/renommage (Release), sans paquet : les seules sources de restauration sont le
#      répertoire du projet et /usr/lib/dotnet/library-packs (local, du SDK), et obj/project.assets.json ne doit
#      lister aucune bibliothèque ;
#   2. les entrées de compilation d'iXtal26, d'iXtal26.Diff et d'iXtal26.PerfBanc dans ARBRE, par `dotnet msbuild
#      -t:Compile -getItem:Compile -getItem:ReferencePathWithRefAssemblies` (cela compile ARBRE en Release) ;
#   3. le renommage, qui écrit les fichiers modifiés à leur place, puis `git mv` de chaque entrée de fichier.
# ARBRE doit être la racine d'un worktree lié : son .git est un fichier, `git rev-parse --show-toplevel` rend ARBRE
# lui-même (une copie sans .git, ou un répertoire pris dans un autre dépôt, où `git mv` agirait ailleurs, est refusé),
# et son répertoire git n'est pas le répertoire commun (`--absolute-git-dir` contre `--git-common-dir`) : le dépôt
# principal est refusé, sauf si RENOMMER_DEPOT_PRINCIPAL=1 le demande en clair. Le journal des sites
# (fichier:ligne:colonne, sorte, type, avant, après ; et les lignes de plus de 120 caractères que le renommage allonge)
# va dans ARBRE/../renommage-<nom de l'arbre>.tsv, ou dans JOURNAL s'il est posé.
set -euo pipefail
ICI="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
[ $# -ge 2 ] || { echo "usage : renommer.sh ARBRE TABLE [ETAPE]" >&2; exit 2; }
A="$(cd -P "$1" && pwd)"; T="$(realpath "$2")"; E="${3:-}"
refus() { echo "renommer.sh : $A $1 ; refusé." >&2; exit 2; }
[ -e "$A/.git" ] || refus "n'a pas de .git (une copie hors de git : git mv n'y agirait pas)"
HAUT=$(git -C "$A" rev-parse --show-toplevel 2> /dev/null) || refus "n'est pas dans un arbre git"
[ "$(cd -P "$HAUT" && pwd)" = "$A" ] || refus "n'est pas la racine de son arbre git ($HAUT)"
GD=$(git -C "$A" rev-parse --absolute-git-dir)
GC=$(cd "$A" && realpath "$(git rev-parse --git-common-dir)")
if [ "$GD" = "$GC" ] || [ -d "$A/.git" ]; then
  [ "${RENOMMER_DEPOT_PRINCIPAL:-}" = 1 ] || refus "est un dépôt principal (répertoire git $GD, le répertoire commun)"
fi
[ -f "$A/iXtal26/iXtal26.csproj" ] || { echo "renommer.sh : $A n'est pas un arbre d'iXtal26." >&2; exit 2; }
if ls "$ICI"/*.nupkg > /dev/null 2>&1; then echo "renommer.sh : un .nupkg dans $ICI ; refusé." >&2; exit 2; fi
dotnet build "$ICI/iXtal26.Renommage.csproj" -c Release -nologo -v q > /dev/null || {
  echo "renommer.sh : la construction de l'outil a échoué." >&2; exit 2; }
python3 - "$ICI/obj/project.assets.json" << 'PY' || exit 2
import json, sys
libs = json.load(open(sys.argv[1], encoding='utf-8')).get('libraries', {})
if libs:
    sys.exit('renommer.sh : l\'outil tire des paquets NuGet : ' + ', '.join(sorted(libs)))
PY
W=$(mktemp -d "${TMPDIR:-/tmp}/renommer-XXXXXX")
J=()
for p in iXtal26/iXtal26.csproj tools/iXtal26.Diff/iXtal26.Diff.csproj tools/perfbanc/iXtal26.PerfBanc.csproj; do
  n=$(basename "$p" .csproj)
  dotnet msbuild "$A/$p" -nologo -restore -p:Configuration=Release -t:Compile -getItem:Compile \
    -getItem:ReferencePathWithRefAssemblies -getProperty:AssemblyName -getProperty:LangVersion \
    -getProperty:DefineConstants -getProperty:Nullable -getProperty:AllowUnsafeBlocks \
    -getProperty:CheckForOverflowUnderflow -getProperty:OutputType > "$W/$n.json" || {
      echo "renommer.sh : msbuild a échoué sur $p ($W/$n.json)." >&2; exit 2; }
  J+=("$W/$n.json")
done
JOURNAL=${JOURNAL:-$(dirname "$A")/renommage-$(basename "$A").tsv}
ET=(); [ -n "$E" ] && ET=(--jusqua "$E")
dotnet "$ICI/bin/Release/net10.0/iXtal26.Renommage.dll" --noms "$T" "${ET[@]}" --arbre "$A" --journal "$JOURNAL" \
  --deplacements "$W/deplacements.tsv" "${J[@]}"
while IFS=$'\t' read -r ancien nouveau; do
  mkdir -p "$A/$(dirname "$nouveau")"
  git -C "$A" mv "$ancien" "$nouveau"
  echo "  git mv $ancien $nouveau"
done < "$W/deplacements.tsv"
rm -rf "$W"
echo "  journal des sites : $JOURNAL"
