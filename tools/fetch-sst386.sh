#!/usr/bin/env bash
# SPDX-FileCopyrightText: 2026 Julien Peloille
# SPDX-License-Identifier: GPL-2.0-only
#
# G2, D0.5 — récupère les vecteurs SingleStepTests 80386 (386EX, mode réel, format MOO)
# dans vectors/sst386/v1_ex_real_mode/, avec la liste de révocation ; G13.2, avec 80386.csv, la
# table des opcodes du corpus, dont sst386-probe lit la colonne f_umask (les drapeaux indéfinis).
#
# 941 fichiers, ~576 Mo, .gitignored. Seul vectors/sst386/MANIFEST.sha256 est commité,
# comme pour le corpus 8088 : un corpus qui change en amont doit casser, pas déplacer
# les poteaux en silence.
#
#   ./tools/fetch-sst386.sh              récupère ce que liste le manifeste
#   ./tools/fetch-sst386.sh --all        récupère les 941 formes
#   ./tools/fetch-sst386.sh --verify     vérifie sans rien télécharger
#   ./tools/fetch-sst386.sh 00 6601 0FA0 récupère ces formes et les ajoute au manifeste

set -uo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.." || exit 2

REPO=SingleStepTests/80386
BASE=https://raw.githubusercontent.com/$REPO/main
SUB=v1_ex_real_mode
DIR=vectors/sst386/$SUB
MANIFEST=vectors/sst386/MANIFEST.sha256

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
    (cd vectors/sst386 && sha256sum -c MANIFEST.sha256) || exit 1
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
            revocation_list.txt | 80386.csv) ;;
            *) fetch "$SUB/${name#$SUB/}" "vectors/sst386/$name" ;;
        esac
    done < "$MANIFEST"
    ;;
*)
    for form in "$@"; do fetch "$SUB/$form.MOO.gz" "$DIR/$form.MOO.gz"; done
    ;;
esac

rm -f vectors/sst386/revocation_list.txt
fetch revocation_list.txt vectors/sst386/revocation_list.txt
fetch 80386.csv vectors/sst386/80386.csv

# Le manifeste reflète toujours ce qui est présent sur le disque.
(cd vectors/sst386 && sha256sum 80386.csv revocation_list.txt $SUB/*.MOO.gz 2>/dev/null | sort -k2) > "$MANIFEST"
echo "manifeste : $(wc -l < "$MANIFEST") entrees, $(du -sh "$DIR/" | cut -f1) sur le disque."
