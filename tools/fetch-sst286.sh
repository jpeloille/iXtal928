#!/usr/bin/env bash
# SPDX-FileCopyrightText: 2026 Julien Peloille
# SPDX-License-Identifier: GPL-2.0-only
#
# G13.2 — récupère les vecteurs SingleStepTests 80286 (Harris N80C286-12, mode réel, format MOO 1.1)
# dans vectors/sst286/v1_real_mode/, avec metadata.json et la liste de révocation.
#
# 326 formes, ~325 Mo, .gitignored : le corpus entier, comme pour le 386. Seul
# vectors/sst286/MANIFEST.sha256 est commité : un corpus qui change en amont doit casser, pas
# déplacer les poteaux en silence.
#
#   ./tools/fetch-sst286.sh              récupère ce que liste le manifeste
#   ./tools/fetch-sst286.sh --all        récupère les 326 formes
#   ./tools/fetch-sst286.sh --verify     vérifie sans rien télécharger
#   ./tools/fetch-sst286.sh 00 F6.7 D4   récupère ces formes et les ajoute au manifeste

set -uo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.." || exit 2

REPO=SingleStepTests/80286
BASE=https://raw.githubusercontent.com/$REPO/main
SUB=v1_real_mode
DIR=vectors/sst286/$SUB
MANIFEST=vectors/sst286/MANIFEST.sha256

mkdir -p "$DIR"

fetch() { # <chemin relatif au dépôt amont> <destination>
    [ -f "$2" ] && return 0
    if curl -sSfL --retry 3 -o "$2" "$BASE/$1"; then
        echo "  recupere $1 ($(stat -c%s "$2") o)"
    else
        rm -f "$2"
        echo "  ABSENT EN AMONT $1" >&2
        return 1
    fi
}

case "${1:-}" in
--verify)
    [ -f "$MANIFEST" ] || { echo "pas de manifeste"; exit 2; }
    (cd vectors/sst286 && sha256sum -c -) < "$MANIFEST" || exit 1
    exit 0
    ;;
--all)
    mapfile -t names < <(curl -sSfL "https://api.github.com/repos/$REPO/contents/$SUB" |
        python3 -c "import json,sys; [print(e['name']) for e in json.load(sys.stdin) if e['name'].endswith('.MOO.gz')]")
    [ "${#names[@]}" -gt 0 ] || { echo "liste amont vide" >&2; exit 1; }
    for n in "${names[@]}"; do fetch "$SUB/$n" "$DIR/$n"; done
    ;;
"")
    [ -f "$MANIFEST" ] || { echo "pas de manifeste ; utiliser --all ou nommer des formes"; exit 2; }
    while read -r _ name; do
        case "$name" in
            revocation_list.txt) ;;
            *) fetch "$SUB/${name#$SUB/}" "vectors/sst286/$name" ;;
        esac
    done < "$MANIFEST"
    ;;
*)
    for form in "$@"; do fetch "$SUB/$form.MOO.gz" "$DIR/$form.MOO.gz"; done
    ;;
esac

# Les masques des drapeaux indéfinis, opcode par opcode (« flags-mask », comme le 8088).
fetch "$SUB/metadata.json" "$DIR/metadata.json"
rm -f vectors/sst286/revocation_list.txt
fetch revocation_list.txt vectors/sst286/revocation_list.txt

# Le manifeste reflète toujours ce qui est présent sur le disque.
(cd vectors/sst286 && sha256sum revocation_list.txt $SUB/metadata.json $SUB/*.MOO.gz 2>/dev/null | sort -k2) \
    > "$MANIFEST"
echo "manifeste : $(wc -l < "$MANIFEST") entrees, $(du -sh "$DIR/" | cut -f1) sur le disque."
