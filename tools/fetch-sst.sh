#!/usr/bin/env bash
# SPDX-FileCopyrightText: 2026 Julien Peloille
# SPDX-License-Identifier: GPL-2.0-only
#
# Récupère les vecteurs SingleStepTests 8088 (v2) dans vectors/sst/v2/.
#
# Le corpus complet fait ~726 Mo pour 324 fichiers ; il est .gitignored. Seul
# vectors/sst/MANIFEST.sha256 est commité, pour qu'un corpus qui change en amont
# casse la CI au lieu de déplacer silencieusement les poteaux.
#
#   ./tools/fetch-sst.sh                 récupère ce que liste le manifeste
#   ./tools/fetch-sst.sh --all           récupère les 324 formes (~726 Mo)
#   ./tools/fetch-sst.sh --verify        vérifie sans rien télécharger
#   ./tools/fetch-sst.sh 00 90 F6.6      récupère ces formes et les ajoute au manifeste

set -uo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.." || exit 2

BASE=https://raw.githubusercontent.com/SingleStepTests/8088/main/v2
DIR=vectors/sst/v2
MANIFEST=vectors/sst/MANIFEST.sha256

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
    (cd "$DIR" && sha256sum -c ../MANIFEST.sha256) || exit 1
    exit 0
    ;;
--all)
    fetch metadata.json || exit 1
    # Les formes à récupérer sortent de metadata.json : les opcodes simples
    # donnent "XX.json.gz", les opcodes de groupe une forme par sous-registre.
    mapfile -t forms < <(python3 -c "
import json
m = json.load(open('$DIR/metadata.json'))['opcodes']
for op, v in m.items():
    if 'reg' in v:
        for r in v['reg']:
            print(f'{op}.{r}')
    else:
        print(op)
")
    for f in "${forms[@]}"; do fetch "$f.json.gz"; done
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
echo "manifeste : $(wc -l < "$MANIFEST") entrees, $(du -sh "$DIR" | cut -f1) sur le disque."
