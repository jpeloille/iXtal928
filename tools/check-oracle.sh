#!/usr/bin/env bash
# Vérifie que les fichiers C cités par oracle.tsv n'ont pas dérivé depuis leur
# transcription. Sort non-zéro en cas de dérive.
#
# pcem-dev/ n'est pas un checkout git : l'ancrage ne peut pas être un SHA de commit,
# c'est un hash de contenu. Tourne en moins d'une seconde sur tout l'arbre.
#
#   ./tools/check-oracle.sh          vérifie
#   ./tools/check-oracle.sh --fix    réécrit les hashes (à n'utiliser qu'après une
#                                    mise à jour délibérée de pcem-dev/, jamais pour
#                                    faire taire une dérive)

set -uo pipefail

cd "$(dirname "${BASH_SOURCE[0]}")/.." || exit 2

MANIFEST=oracle.tsv
FIX=0
[ "${1:-}" = "--fix" ] && FIX=1

[ -f "$MANIFEST" ] || { echo "introuvable : $MANIFEST" >&2; exit 2; }

# --- Intégrité de l'arbre vendoré ------------------------------------------
# pcem-dev/ n'est pas un checkout git : l'ancrage est un hash de contenu, publié
# dans VENDORED.md. On le vérifie d'abord — un fichier C modifié en place
# invaliderait tous les hashes individuels plus bas, et surtout ferait cesser à
# l'oracle d'être un oracle.
tree_fail=0
if [ -d pcem-dev ] && [ -f VENDORED.md ]; then
    expected=$(grep -oE 'sha256 de l.arbre : [0-9a-f]{64}' VENDORED.md | grep -oE '[0-9a-f]{64}')
    actual=$(find pcem-dev -type f -not -path 'pcem-dev/.idea/*' -print0 \
             | sort -z | xargs -0 sha256sum | sha256sum | cut -d' ' -f1)
    if [ -n "$expected" ] && [ "$actual" != "$expected" ]; then
        echo "ARBRE VENDORE MODIFIE — pcem-dev/ a bougé."
        echo "            VENDORED.md : $expected"
        echo "            reel        : $actual"
        echo "            L'instrumentation se fait par patch sur une copie de travail,"
        echo "            jamais en place. Voir VENDORED.md."
        tree_fail=1
    fi
fi

drift=0 missing=0 checked=0 host=0
declare -a fixups

while IFS=$'\t' read -r csharp_path c_path c_sha256 c_lines status note; do
    # Commentaires, ligne d'en-tête, lignes vides.
    case "$csharp_path" in ''|'#'*|$'\xef\xbb\xbf#'*|csharp_path) continue ;; esac

    if [ ! -f "$csharp_path" ]; then
        echo "ABSENT C#   $csharp_path"
        missing=$((missing + 1))
        continue
    fi

    if [ "$c_path" = "-" ]; then
        host=$((host + 1))
        continue
    fi

    if [ ! -f "$c_path" ]; then
        echo "ABSENT C    $c_path  (cité par $csharp_path)"
        missing=$((missing + 1))
        continue
    fi

    actual=$(sha256sum "$c_path" | cut -d' ' -f1)
    checked=$((checked + 1))

    if [ "$actual" != "$c_sha256" ]; then
        echo "DERIVE      $c_path"
        echo "            manifeste : $c_sha256"
        echo "            reel      : $actual"
        echo "            transcrit dans $csharp_path (lignes $c_lines, $status)"
        drift=$((drift + 1))
        fixups+=("$c_path	$actual")
    fi
done < "$MANIFEST"

if [ "$FIX" = 1 ] && [ "$drift" -gt 0 ]; then
    for entry in "${fixups[@]}"; do
        p=${entry%%	*}; h=${entry##*	}
        # Remplace le 3e champ des lignes dont le 2e champ est ce chemin C.
        awk -F'\t' -v OFS='\t' -v p="$p" -v h="$h" \
            '$2 == p { $3 = h } { print }' "$MANIFEST" > "$MANIFEST.tmp" \
            && mv "$MANIFEST.tmp" "$MANIFEST"
    done
    echo "--fix : $drift hash(es) reecrit(s) dans $MANIFEST."
    drift=0
fi

echo "oracle : $checked transcrit(s) verifie(s), $host fichier(s) hote ignore(s), $drift derive(s), $missing absent(s), arbre vendore $([ "$tree_fail" -eq 0 ] && echo OK || echo MODIFIE)."
[ "$drift" -eq 0 ] && [ "$missing" -eq 0 ] && [ "$tree_fail" -eq 0 ]
