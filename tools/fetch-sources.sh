#!/usr/bin/env bash
# SPDX-FileCopyrightText: 2026 Julien Peloille
# SPDX-License-Identifier: GPL-2.0-only
#
# Récupère les sources primaires de iXtal26/Docs/ibm5150/ dans sources/.
#
# Les PDF (~235 Mo) sont sous copyright et ignorés par sources/.gitignore. Seul
# sources/MANIFEST.tsv est commité : une source qui change en amont fait échouer --verify
# au lieu de déplacer en silence les pages citées.
#
#   ./tools/fetch-sources.sh             récupère ce que liste le manifeste
#   ./tools/fetch-sources.sh I1 T1       récupère ces sources seulement
#   ./tools/fetch-sources.sh --verify    vérifie sans rien télécharger
#
# Un sha256 à « - » se remplit au premier téléchargement. Ensuite il fait foi : une somme
# qui diffère est une erreur, jamais une mise à jour.

set -uo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.." || exit 2

DIR=sources
MANIFEST=$DIR/MANIFEST.tsv
[ -f "$MANIFEST" ] || { echo "pas de manifeste : $MANIFEST" >&2; exit 2; }

verify=0
declare -A wanted=()
for a in "$@"; do
    case $a in
    --verify) verify=1 ;;
    *) wanted[$a]=1 ;;
    esac
done

fail=0
n=0
tmp=$(mktemp) || exit 2
trap 'rm -f "$tmp"' EXIT

# Les champs vides s'écrivent « - » : deux tabulations de suite n'en feraient qu'une
# pour read, qui compte la tabulation parmi les blancs.
while IFS= read -r line; do
    case $line in
    '#'*|'') printf '%s\n' "$line" >> "$tmp"; continue ;;
    esac
    IFS=$'\t' read -r id sha file url rest <<< "$line"
    if [ ${#wanted[@]} -gt 0 ] && [ -z "${wanted[$id]:-}" ]; then
        printf '%s\n' "$line" >> "$tmp"; continue
    fi
    unset "wanted[$id]"
    n=$((n + 1))
    path=$DIR/$file
    if [ ! -f "$path" ]; then
        if [ $verify -eq 1 ]; then
            echo "  ABSENT $id $file" >&2
            fail=1
            printf '%s\n' "$line" >> "$tmp"; continue
        fi
        if curl -sSfL --retry 3 -o "$path.part" "$url"; then
            mv "$path.part" "$path"
            echo "  recupere $id $file ($(stat -c%s "$path") o)"
        else
            rm -f "$path.part"
            echo "  ABSENT EN AMONT $id $url" >&2
            fail=1
            printf '%s\n' "$line" >> "$tmp"; continue
        fi
    fi
    got=$(sha256sum "$path" | cut -d' ' -f1)
    if [ "$sha" = "-" ]; then
        if [ $verify -eq 1 ]; then
            echo "  NON SCELLE $id : le manifeste n'a pas encore sa somme" >&2
            fail=1
        else
            sha=$got
            echo "  scelle $id $got"
        fi
    elif [ "$sha" != "$got" ]; then
        echo "  SOMME FAUSSE $id : $got au lieu de $sha" >&2
        fail=1
    elif [ $verify -eq 1 ]; then
        echo "  ok $id"
    fi
    printf '%s\t%s\t%s\t%s\t%s\n' "$id" "$sha" "$file" "$url" "$rest" >> "$tmp"
done < "$MANIFEST"

for id in "${!wanted[@]}"; do
    echo "  INCONNU $id : absent du manifeste" >&2
    fail=1
done

# --verify ne réécrit jamais le manifeste ; un téléchargement y scelle les sommes neuves.
[ $verify -eq 0 ] && cat "$tmp" > "$MANIFEST"

echo "sources : $n vues, $(du -sh "$DIR/" | cut -f1) sur le disque."
exit $fail
