#!/usr/bin/env bash
# SPDX-FileCopyrightText: 2026 Julien Peloille
# SPDX-License-Identifier: GPL-2.0-only
#
# Contrôle les renvois de iXtal26/Docs/ibm5150/. Échoue si :
#   - un fichier de code cité n'existe pas, ou ne porte pas le symbole nommé ;
#   - un renvoi à PCem sort de son fichier ;
#   - un PB cité n'a pas d'en-tête dans PCEM_BUGS.md ;
#   - une source citée manque à sources/MANIFEST.tsv ;
#   - un opcode de la table du chapitre 02 n'a pas son case dans execx86, ou si la table
#     n'en compte pas 256.
#
#   ./tools/check-doc5150.sh                les renvois
#   ./tools/check-doc5150.sh --couverture   les renvois, et les 42 fichiers du cœur cités
#
# Le code C# se cite `chemin.cs#symbole`, sans numéro de ligne : G13 fait bouger le code,
# et un numéro périmé se lit comme s'il était juste. Un symbole qui disparaît, lui, fait
# échouer ce contrôle. Les chemins se lisent sous iXtal26/, sauf ceux qui commencent par
# iXtal26/, tools/ ou pcem-dev/.

set -uo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.." || exit 2

# IXTAL_DOC5150 désigne un autre dossier à contrôler : celui des pannes injectées, par exemple.
DOC=${IXTAL_DOC5150:-iXtal26/Docs/ibm5150}
MANIFEST=sources/MANIFEST.tsv
REGISTRE=PCEM_BUGS.md
CPU=iXtal26/Cpu/808x.cs

couverture=0
case ${1:-} in
--couverture) couverture=1 ;;
"") ;;
*) echo "usage : $0 [--couverture]" >&2; exit 2 ;;
esac

fail=0
err() { echo "  $1" >&2; fail=1; }

shopt -s nullglob
docs=("$DOC"/*.md)
[ ${#docs[@]} -gt 0 ] || { echo "aucun fichier dans $DOC" >&2; exit 2; }

# Les sources : celles du manifeste, plus R1, les octets des ROM du dépôt (roms/).
declare -A sources=([R1]=1)
while IFS=$'\t' read -r id _; do
    case $id in '#'*|'') continue ;; esac
    sources[$id]=1
done < "$MANIFEST"

resolve() { # <chemin cité> → chemin depuis la racine
    case $1 in
    iXtal26/*|tools/*|pcem-dev/*) printf '%s\n' "$1" ;;
    *) printf '%s\n' "iXtal26/$1" ;;
    esac
}

for d in "${docs[@]}"; do
    # Le code : `chemin.cs` ou `chemin.cs#symbole`.
    while IFS=: read -r ln ref; do
        ref=${ref#\`}; ref=${ref%\`}
        path=${ref%%#*}
        sym=
        [ "$ref" != "$path" ] && sym=${ref#*#}
        f=$(resolve "$path")
        if [ ! -f "$f" ]; then
            err "$d:$ln : $path introuvable"
        elif [ -n "$sym" ] && ! grep -qw -- "$sym" "$f"; then
            err "$d:$ln : $sym absent de $path"
        fi
    done < <(grep -on '`[A-Za-z0-9_./-]*\.cs\(#[A-Za-z0-9_]*\)\?`' "$d")
    while IFS=: read -r ln _; do
        err "$d:$ln : numéro de ligne sur du C# ; citer chemin.cs#symbole"
    done < <(grep -on '`[A-Za-z0-9_./-]*\.cs:[0-9]' "$d")

    # PCem : `pcem-dev/chemin`, `pcem-dev/chemin:NN` ou `pcem-dev/chemin:NN-MM`.
    while IFS=: read -r ln ref; do
        ref=${ref#\`}; ref=${ref%\`}
        path=${ref%%:*}
        if [ ! -e "$path" ]; then
            err "$d:$ln : $path introuvable"
            continue
        fi
        [ "$ref" = "$path" ] && continue
        range=${ref#*:}
        end=${range#*-}
        if [ -f "$path" ] && [ "$end" -gt "$(wc -l < "$path")" ]; then
            err "$d:$ln : $ref dépasse la fin de $path ($(wc -l < "$path") lignes)"
        fi
    done < <(grep -on '`pcem-dev/[A-Za-z0-9_./+-]*\(:[0-9]\+\(-[0-9]\+\)\?\)\?`' "$d")

    # Le registre : PB-nn.
    while IFS=: read -r ln pb; do
        grep -qE "^#{2,4} $pb( |$)" "$REGISTRE" || err "$d:$ln : $pb n'a pas d'en-tête dans $REGISTRE"
    done < <(grep -on 'PB-[0-9]\+' "$d")

    # Les sources : [T1 p. 1-10], [I1 p. 2-51], [R1 F000:E5BC]…
    while IFS=: read -r ln cite; do
        id=${cite:1:2}
        [ -n "${sources[$id]:-}" ] || err "$d:$ln : la source $id n'est pas au manifeste"
    done < <(grep -on '\[[A-Z][0-9][] ,]' "$d")
done

# Le chapitre 02 : chaque ligne de table qui commence par un opcode (| 00 |, | `CE` |…)
# doit avoir son case dans le switch principal d'execx86, et les 256 doivent y être.
ch02=("$DOC"/02-*.md)
if [ ${#ch02[@]} -gt 0 ]; then
    declare -A cases=()
    while read -r op; do cases[$op]=1; done < <(awk '
        !sw && /switch \(opcode\)/ { match($0, /^ */); ind = RLENGTH; sw = 1; next }
        sw && /^ *\}[ \t]*$/ { match($0, /^ */); if (RLENGTH == ind) exit }
        sw && /^ *case 0x[0-9A-Fa-f][0-9A-Fa-f]:/ {
            match($0, /^ */)
            if (RLENGTH == ind) { s = $0; sub(/^ *case 0x/, "", s); print toupper(substr(s, 1, 2)) }
        }' "$CPU")
    [ ${#cases[@]} -gt 0 ] || err "$CPU : switch (opcode) introuvable"
    declare -A vus=()
    while IFS=: read -r ln row; do
        op=$(printf '%s' "$row" | sed -E 's/^\| *`?([0-9A-Fa-f]{2})`? *\|.*/\1/' | tr 'a-f' 'A-F')
        vus[$op]=1
        # 0xCE n'a pas de case : PCem le laisse au default (constats.md).
        [ "$op" = "CE" ] && continue
        [ -n "${cases[$op]:-}" ] || err "${ch02[0]}:$ln : l'opcode $op n'a pas de case dans execx86"
    done < <(grep -nE '^\| *`?[0-9A-Fa-f]{2}`? *\|' "${ch02[0]}")
    [ ${#vus[@]} -eq 256 ] || err "${ch02[0]} : la table compte ${#vus[@]} opcodes sur 256"
fi

# --couverture : chacun des 42 fichiers listés par le README sous « ## Les 42 fichiers »
# doit être cité par au moins un chapitre.
if [ $couverture -eq 1 ]; then
    mapfile -t coeur < <(awk '/^## Les 42 fichiers/ { on = 1; next } on && /^## / { exit } on' "$DOC/README.md" \
        | grep -o '`[A-Za-z0-9_./-]*\.cs`' | tr -d '`' | sort -u)
    [ ${#coeur[@]} -eq 42 ] || err "$DOC/README.md : ${#coeur[@]} fichiers sous « Les 42 fichiers », 42 attendus"
    chapitres=()
    for d in "${docs[@]}"; do
        case ${d##*/} in README.md|constats.md) ;; *) chapitres+=("$d") ;; esac
    done
    for f in "${coeur[@]}"; do
        if [ ${#chapitres[@]} -eq 0 ] || ! grep -qF -- "\`$f" "${chapitres[@]}"; then
            err "$f : cité par aucun chapitre"
        fi
    done
fi

[ $fail -eq 0 ] && echo "doc ibm5150 : ${#docs[@]} fichiers, renvois justes."
exit $fail
