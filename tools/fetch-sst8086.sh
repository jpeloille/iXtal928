#!/usr/bin/env bash
# SPDX-FileCopyrightText: 2026 Julien Peloille
# SPDX-License-Identifier: GPL-2.0-only
#
# G13.2 — récupère les vecteurs SingleStepTests 8086 (v1, Intel P80C86A-2) dans vectors/sst8086/v1/.
#
# Même format que le 8088 v2 (JSON gzippé, metadata.json et ses masques de drapeaux), 2 000 cas par
# forme au lieu de 10 000. Le corpus complet fait ~181 Mo pour 324 fichiers ; il est .gitignored. Seul
# vectors/sst8086/MANIFEST.sha256 est commité, comme pour le 8088 : un corpus qui change en amont doit
# casser, pas déplacer les poteaux en silence.
#
#   ./tools/fetch-sst8086.sh             récupère ce que liste le manifeste
#   ./tools/fetch-sst8086.sh --all       récupère les 323 formes
#   ./tools/fetch-sst8086.sh --verify    vérifie sans rien télécharger
#   ./tools/fetch-sst8086.sh 00 90 F6.6  récupère ces formes et les ajoute au manifeste
#
# --all prend la liste de l'API de GitHub, comme fetch-sst386.sh, et non celle de metadata.json :
# celui-ci nomme C6.0 à C7.7 et 8F.0 à 8F.7 là où les fichiers s'appellent C6, C7 et 8F.

set -uo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.." || exit 2

REPO=SingleStepTests/8086
SUB=v1
BASE=https://raw.githubusercontent.com/$REPO/main/$SUB
DIR=vectors/sst8086/$SUB
MANIFEST=vectors/sst8086/MANIFEST.sha256

mkdir -p "$DIR"

fetch() { # <nom de fichier>
    local f=$1
    [ -f "$DIR/$f" ] && return 0
    if curl -sSfL --retry 3 -o "$DIR/$f" "$BASE/$f"; then
        echo "  recupere $f ($(stat -c%s "$DIR/$f") o)"
    else
        rm -f "$DIR/$f"
        echo "  ABSENT EN AMONT $f" >&2
        return 1
    fi
}

case "${1:-}" in
--verify)
    [ -f "$MANIFEST" ] || { echo "pas de manifeste"; exit 2; }
    # Le manifeste se lit depuis la racine : $DIR peut être un lien vers les vecteurs d'un autre arbre.
    (cd "$DIR" && sha256sum -c -) < "$MANIFEST" || exit 1
    exit 0
    ;;
--all)
    fetch metadata.json || exit 1
    mapfile -t names < <(curl -sSfL "https://api.github.com/repos/$REPO/contents/$SUB" |
        python3 -c "import json,sys; [print(e['name']) for e in json.load(sys.stdin)
                                      if e['name'].endswith('.json.gz')]")
    [ "${#names[@]}" -gt 0 ] || { echo "liste amont vide" >&2; exit 1; }
    for n in "${names[@]}"; do fetch "$n"; done
    ;;
"")
    [ -f "$MANIFEST" ] || { echo "pas de manifeste ; utiliser --all ou nommer des formes"; exit 2; }
    fetch metadata.json
    while read -r _ name; do fetch "$name"; done < "$MANIFEST"
    ;;
*)
    fetch metadata.json
    for form in "$@"; do fetch "$form.json.gz"; done
    ;;
esac

# Le manifeste reflète toujours ce qui est présent sur le disque.
(cd "$DIR" && sha256sum ./*.json.gz metadata.json 2>/dev/null | sed 's| \./| |' | sort -k2) > "$MANIFEST"
echo "manifeste : $(wc -l < "$MANIFEST") entrees, $(du -sh "$DIR/" | cut -f1) sur le disque."
