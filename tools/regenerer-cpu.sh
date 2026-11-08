#!/usr/bin/env bash
# SPDX-FileCopyrightText: 2026 Julien Peloille
# SPDX-License-Identifier: GPL-2.0-only
#
# ORACLE: (aucun) — outillage de la preuve des membres (G15.0b5, PLAN-G15.md § G15.0b).
#
# regenerer-cpu.sh [--noms TABLE…] [--depuis E] [--jusqua E] [--ecrire] — les huit fichiers générés de Cpu/,
# régénérés par leurs générateurs et comparés à l'octet aux fichiers de l'arbre (la porte « régénéré = commité » ; les
# générateurs du x87 ont déjà été rejoués à l'octet à la main, VERIFICATION.md § G13 ; ce script en fait une porte,
# sur les huit, et pour les noms de G15).
#
# Les générateurs (tools/x87gen/gen43.py, gen44.py, gentab.py, gen46.py, genmat.py ; tools/ops386-table.py --emit)
# écrivent du C# aux noms de PCem, et deux d'entre eux lisent les sources de Cpu/ par ces noms : gentab.py, pour savoir
# quels gestionnaires sont transcrits ; gen46.py, qui relit et retouche les sorties de gen43.py et gen44.py. Une fois
# Cpu/ renommé (G15.8), ni l'un ni l'autre ne les trouveraient. Le script travaille donc sur une copie :
#   1. iXtal26/Cpu/*.cs, tools/x87gen, tools/ops386-table.py et tools/noms copiés dans un répertoire temporaire,
#      pcem-dev y étant lié (lu seulement) ;
#   2. les sources copiées de Cpu/ retraduites vers les noms d'avant les étapes de la table (traduire.py --inverse) ;
#   3. les générateurs lancés dans la copie, dans leur ordre (gen46 relit gen43 et gen44 ; gentab lit Cpu/) ;
#   4. les huit sorties traduites vers les noms courants (traduire.py) ;
#   5. chacune comparée à l'octet au fichier de l'arbre ; --ecrire les copie dans l'arbre.
# La traduction (tools/noms/traduire.py, son en-tête en dit les limites) suit les types, les membres, les paramètres
# et les littéraux de la table ; les noms des huit fichiers sont fixes (Q42 : les fichiers restent).
# Sans --noms, la table est tools/noms/g15.txt entière : les générateurs ont les noms d'après G14 (à HEAD de G15.0b5,
# régénérés sans table, ils rendent les fichiers commités à l'octet). Retour : 0 les huit égaux (ou écrits) ; 1 un
# écart (le diff dit) ; 2 une erreur (un générateur, une table refusée).
set -uo pipefail
REPO=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
NOMS=(); ECRIRE=0; ET=()
while [ $# -gt 0 ]; do
  case "$1" in
    --noms) [ $# -ge 2 ] || { echo "usage : regenerer-cpu.sh [--noms TABLE…] [--depuis E] [--jusqua E] [--ecrire]" >&2
                              exit 2; }; NOMS+=(--noms "$(realpath "$2")"); shift 2 ;;
    --depuis|--jusqua) [ $# -ge 2 ] || exit 2; ET+=("$1" "$2"); shift 2 ;;
    --ecrire) ECRIRE=1; shift ;;
    *) echo "usage : regenerer-cpu.sh [--noms TABLE…] [--depuis E] [--jusqua E] [--ecrire]" >&2; exit 2 ;;
  esac
done
[ ${#NOMS[@]} -gt 0 ] || NOMS=(--noms "$REPO/tools/noms/g15.txt")
SORTIES="x87_ops_arith.cs x87_ops_misc.cs x87_ops_tables.cs x87_ops_808x.cs x87_ops_808x_tables.cs
x87_noyau_386.Materiel.cs x87_noyau_808x.Materiel.cs 386_ops_table386.cs"
PY=(python3 -I -B)   # -B : pas de __pycache__ (-I ignore PYTHONDONTWRITEBYTECODE)
W=$(mktemp -d "${TMPDIR:-/tmp}/regenerer-cpu-XXXXXX") || exit 2
[ -n "$W" ] && [ -d "$W" ] || exit 2
trap 'rm -rf "$W"' EXIT
{ mkdir -p "$W/iXtal26/Cpu" "$W/tools" && cp "$REPO"/iXtal26/Cpu/*.cs "$W/iXtal26/Cpu/" \
  && cp -r "$REPO/tools/x87gen" "$REPO/tools/noms" "$W/tools/" && cp "$REPO/tools/ops386-table.py" "$W/tools/" \
  && rm -rf "$W/tools/noms/__pycache__" "$W/tools/x87gen/__pycache__" && ln -s "$REPO/pcem-dev" "$W/pcem-dev"; } \
  || { echo "regenerer-cpu : la copie a échoué" >&2; exit 2; }
"${PY[@]}" "$W/tools/noms/traduire.py" "${NOMS[@]}" "${ET[@]}" --inverse "$W"/iXtal26/Cpu/*.cs || exit 2
for g in gen43 gen44 gentab gen46 genmat; do
  (cd "$W" && "${PY[@]}" "tools/x87gen/$g.py" > "$W/$g.log" 2>&1) || { echo "regenerer-cpu : $g a échoué :" >&2
    cat "$W/$g.log" >&2; exit 2; }
  echo "  $g : $(tail -1 "$W/$g.log")"
done
(cd "$W" && "${PY[@]}" tools/ops386-table.py --emit iXtal26/Cpu/386_ops_table386.cs > "$W/ops386.log" 2>&1) || {
  echo "regenerer-cpu : ops386-table.py a échoué :" >&2; cat "$W/ops386.log" >&2; exit 2; }
echo "  ops386-table : $(tail -1 "$W/ops386.log")"
F=(); for f in $SORTIES; do F+=("$W/iXtal26/Cpu/$f"); done
"${PY[@]}" "$W/tools/noms/traduire.py" "${NOMS[@]}" "${ET[@]}" "${F[@]}" || exit 2
rc=0
for f in $SORTIES; do
  if cmp -s "$W/iXtal26/Cpu/$f" "$REPO/iXtal26/Cpu/$f"; then
    echo "  égal à l'octet : iXtal26/Cpu/$f"
  elif [ $ECRIRE = 1 ]; then
    cp "$W/iXtal26/Cpu/$f" "$REPO/iXtal26/Cpu/$f"; echo "  écrit : iXtal26/Cpu/$f"
  else
    echo "  ÉCART : iXtal26/Cpu/$f"; diff -u "$REPO/iXtal26/Cpu/$f" "$W/iXtal26/Cpu/$f" | head -20; rc=1
  fi
done
exit $rc
