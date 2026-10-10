#!/usr/bin/env bash
# SPDX-FileCopyrightText: 2026 Julien Peloille
# SPDX-License-Identifier: GPL-2.0-only
#
# La réécriture de SoftFloat contre TestFloat-3e. Pour chaque fonction et chaque jeu d'options, `testfloat_gen` tire
# les cas et les résultats de la SoftFloat C de référence (variante 8086), le banc recalcule chaque cas et le compare
# au bit près, puis `testfloat_ver -checkAll` juge les résultats du banc. Une ligne TSV par passe : fonction, options,
# cas, écarts au bit près, verdict de testfloat_ver.
#
# Usage : verifier.sh <niveau 1|2> <fichier TSV> [processus en parallèle, 4 par défaut]
# Prérequis : sources/softfloat/ construit (SoftFloat-3e en SPECIALIZE_TYPE=8086, puis TestFloat-3e), et le banc
# compilé en Release.
set -euo pipefail

racine=$(cd "$(dirname "$0")/../../.." && pwd)
tf=$racine/sources/softfloat/TestFloat-3e/build/Linux-x86_64-GCC
banc=$racine/tools/softfloat/Banc/bin/Release/net10.0/iXtal26.SoftFloatBanc
niveau=${1:?niveau 1 ou 2}
sortie=${2:?fichier TSV}
par=${3:-4}

arrondis="-rnear_even -rminMag -rmin -rmax -rnear_maxMag -rodd"
passes=$(mktemp)
for f in extF80_add extF80_sub extF80_mul extF80_div extF80_sqrt; do
    for r in $arrondis; do for p in -precision32 -precision64 -precision80; do for t in -tininessafter -tininessbefore; do
        echo "$f $r $p $t"
    done; done; done
done >> "$passes"
for r in $arrondis; do for t in -tininessafter -tininessbefore; do
    echo "extF80_rem $r $t"; echo "extF80_to_f32 $r $t"; echo "extF80_to_f64 $r $t"
done; done >> "$passes"
for r in $arrondis; do for e in -exact -notexact; do
    echo "extF80_roundToInt $r $e"; echo "extF80_to_i32 $r $e"; echo "extF80_to_i64 $r $e"
done; done >> "$passes"
for f in extF80_eq extF80_le extF80_lt extF80_eq_signaling extF80_le_quiet extF80_lt_quiet \
         f32_to_extF80 f64_to_extF80 i32_to_extF80 i64_to_extF80; do
    echo "$f"
done >> "$passes"

une_passe() {
    local f=$1; shift
    local bilan verdict
    local trace="$TMPDIR_PASSE/$f${*// /}"
    "$tf/testfloat_gen" -level "$niveau" "$@" "$f" | "$banc" ver "$f" "$@" 2> "$trace.banc" \
        | "$tf/testfloat_ver" -checkAll -errors 5 "$@" "$f" > "$trace.ver" 2>&1 || true
    bilan=$(tail -1 "$trace.banc")
    verdict=$(tr '\r' '\n' < "$trace.ver" | grep -E 'errors found|tests performed' | tail -1 | tr -s ' ')
    local cas ecarts
    cas=$(sed -n 's/.*: \([0-9]*\) cas, .*/\1/p' <<< "$bilan")
    ecarts=$(sed -n 's/.*cas, \([0-9]*\) écarts.*/\1/p' <<< "$bilan")
    printf '%s\t%s\t%s\t%s\t%s\n' "$f" "$*" "${cas:-?}" "${ecarts:-?}" "$verdict"
}
export -f une_passe
export tf banc niveau
TMPDIR_PASSE=$(mktemp -d)
export TMPDIR_PASSE

debut=$(date +%s)
xargs -P "$par" -L 1 bash -c 'une_passe "$@"' _ < "$passes" | LC_ALL=C sort > "$sortie"
fin=$(date +%s)
n=$(wc -l < "$sortie")
mauvais=$(awk -F'\t' '$4 != "0" || $5 !~ /no errors found/' "$sortie" | wc -l)
echo "niveau $niveau : $n passes en $((fin - debut)) s ; $mauvais passes avec un écart ou une erreur"
awk -F'\t' '{c += $3} END {print c " cas en tout"}' "$sortie"
rm -rf "$TMPDIR_PASSE" "$passes"
[ "$mauvais" -eq 0 ]
