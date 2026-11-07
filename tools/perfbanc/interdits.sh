#!/usr/bin/env bash
# SPDX-FileCopyrightText: 2026 Julien Peloille
# SPDX-License-Identifier: GPL-2.0-only
#
# ORACLE: (aucun) — outillage de mesure.
#
# LE BANC DES CONSTRUCTIONS INTERDITES (G15.0a ; PLAN-G15.md § G15.0a, décision n° 6 ; InterditsBanc.cs).
#
#   tools/perfbanc/interdits.sh [--passages N] [--cas REGEX] [--code-seul] SORTIE
#
# Lance les neuf cas Interdit* du banc de cet arbre (construit en Release : tools/perfbanc/bin/Release/net10.0/), comme
# compare-ref.sh lance ceux de la référence (compare-ref.sh:281-283) : sous `setarch $(uname -m) -R taskset -c
# $COEURS`, --launchCount 3, l'échauffement, les itérations et les invocations fixés, un lancement de BenchmarkDotNet
# par cas (CAS ci-dessous : ses invocations par itération), la machine au calme avant chacun et à sa fin (la sonde de
# compare_ref.py ; une sonde qui échoue refuse). N passages (2 par défaut) : les cas dans l'ordre au premier, à
# rebours au second, et ainsi de suite ; chaque colonne a donc 3 × N moyennes de lancement. --cas REGEX ne garde que les cas dont le nom
# s'y accorde (grep -E). --code-seul ne mesure rien : il relève le code machine et rend le verdict d'une SORTIE déjà
# mesurée.
#
# Puis, une fois par cas, le code machine : un lancement --job dry de la colonne Retenue (sa préparation appelle
# toutes les colonnes, donc les compile toutes) sous DOTNET_JitDisasm, les méthodes des copies et
# celles du vrai code (DESASSEMBLER ci-dessous), que BenchmarkDotNet recopie de la sortie de ses processus dans son
# journal : SORTIE/jit/<cas>.log. Les variables passent par --envVars : le processus du banc n'hérite pas de
# l'environnement du lanceur (mesuré : sous DOTNET_JitDisasm posé par le shell, aucun listing), et --envVars
# remplace celles du job de Program.cs, d'où DOTNET_TieredCompilation:0 redit (mesuré : sans lui, des listings
# « Tier0 » et « Tier1-OSR »). Enfin le verdict, interdits.py (son en-tête dit la règle) : SORTIE/rapport.txt et
# SORTIE/rapport.tsv.
#
# Code de sortie : 0 le rapport écrit ; 3 refus (la machine n'est pas revenue au calme, un lancement a échoué) ; 2
# erreur d'usage.

set -u
export LC_ALL=C

OUTIL="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ARBRE="$(cd "$OUTIL/../.." && pwd)"
DLL="$OUTIL/bin/Release/net10.0/iXtal26.PerfBanc.dll"
COEURS="${COEURS:-4,6,8,10}"

# Les cas : nom | invocations par itération (un multiple de 16, le déroulement par défaut), choisies sur un passage
# d'étalonnage pour une itération d'environ 100 ms de la colonne Retenue (les colonnes plus lentes durent plus).
CAS=(
  "Interdit1Instance|16384"
  "Interdit2Linq|3408"
  "Interdit3Interface|4576"
  "Interdit4Fermeture|4528"
  "Interdit5Delegue|21232"
  "Interdit6Allocation|8080"
  "Interdit7Boite|5632"
  "Interdit8Chaine|21712"
  "Interdit9Params|7104"
)
ECHAUFFEMENTS=3
ITERATIONS=8

# Le code machine à relever : les méthodes des copies, et celles du vrai code que chaque cas appelle (une classe se
# nomme par un motif qui couvre son espace de noms : « Interdit7:* » ne relève rien, « *Interdit7:* » relève).
DESASSEMBLER='*Interdit*:* *.Alu*:* *.MemoireInstance:* *.ProcesseurInstance:* *.RenduDirect*:* *.RenduCapture:*
*.Journal*:* *._386_common:readmemb *.sound_speaker:speaker_get_buffer *:<OP_ARITH>b__*
*:<PoserSautsConditionnels>b__0 *.x86seg_c:loadseg *.x86seg_c:do_seg_load *.vid_svga:svga_recalctimings
*.vid_svga:svga_write *.io:outb *.io:outb_corps *._386:PREFETCH_RUN *._386:prefetch_run'

usage() {
  sed -n '9p' "$0" | sed 's/^# \{0,1\}//' >&2
  exit 2
}

PASSAGES=2
FILTRE=""
CODE_SEUL=0
while [ $# -gt 0 ]; do
  case "$1" in
    --passages) [ $# -ge 2 ] || usage; PASSAGES="$2"; shift 2 ;;
    --cas) [ $# -ge 2 ] || usage; FILTRE="$2"; shift 2 ;;
    --code-seul) CODE_SEUL=1; shift ;;
    -*) usage ;;
    *) break ;;
  esac
done
[ $# -eq 1 ] || usage
case "$PASSAGES" in ''|*[!0-9]*|0) usage ;; esac
SORTIE="$1"
[ -f "$DLL" ] || { echo "interdits : $DLL manque (construire le banc en Release)" >&2; exit 2; }
mkdir -p "$SORTIE" || exit 2
SORTIE="$(cd "$SORTIE" && pwd)"

CHOISIS=()
for c in "${CAS[@]}"; do
  if [ -z "$FILTRE" ] || grep -qE -- "$FILTRE" <<< "${c%%|*}"; then CHOISIS+=("$c"); fi
done
[ ${#CHOISIS[@]} -gt 0 ] || { echo "interdits : aucun cas ne s'accorde à « $FILTRE »" >&2; exit 2; }

journal() { echo "$(date '+%F %T') $*" | tee -a "$SORTIE/journal.txt"; }

attendre_calme() {
  local f="$1" k lourds
  for k in $(seq 1 20); do
    python3 -I "$OUTIL/compare_ref.py" sonde > "$f" || return 1
    lourds="$(python3 -I -c 'import json, sys
print("; ".join(x["cmd"][:70] for x in json.load(open(sys.argv[1]))["lourds"]))' "$f")" || return 1
    [ -z "$lourds" ] && return 0
    journal "un processus lourd tourne ($lourds) ; attente"
    sleep 30
  done
  return 1
}

lancer() {
  local dir="$1" filtre="$2"
  shift 2
  mkdir -p "$dir"
  rm -rf "$OUTIL"/iXtal26.PerfBanc-*
  if ! attendre_calme "$dir/sonde-avant.json"; then
    journal "refus : la machine n'est pas revenue au calme ($dir)"
    exit 3
  fi
  dotnet --list-runtimes > "$dir/runtimes.txt"
  (cd "$ARBRE" && exec setarch "$(uname -m)" -R taskset -c "$COEURS" dotnet "$DLL" --filter "$filtre" "$@" \
      --exporters fulljson --artifacts "$dir") > "$dir/sortie.log" 2>&1
  local rc=$?
  # Un processus lourd à la fin du lancement le refuse aussi, comme compare-ref.sh.
  python3 -I "$OUTIL/compare_ref.py" sonde > "$dir/sonde-apres.json" || { journal "refus : la sonde a échoué"; exit 3; }
  if python3 -I -c 'import json, sys; sys.exit(0 if json.load(open(sys.argv[1]))["lourds"] else 1)' \
      "$dir/sonde-apres.json"; then
    journal "refus : un processus lourd tournait à la fin ($dir)"
    exit 3
  fi
  # Une exception non rattrapée, ou celle d'une préparation refusée, que BenchmarkDotNet imprime en tête de ligne (le
  # mot seul ne suffit pas : un listing du JIT nomme InvalidOperationException).
  if [ $rc -ne 0 ] || grep -qE '^(Unhandled exception|System\.[A-Za-z.]*Exception:| ---> )' "$dir/sortie.log"; then
    journal "refus : $filtre a échoué (code $rc), voir $dir/sortie.log"
    exit 3
  fi
}

journal "début : ${#CHOISIS[@]} cas, $PASSAGES passages, cœurs $COEURS, l'arbre $ARBRE" \
  "($(git -C "$ARBRE" rev-parse --short HEAD))"
[ "$CODE_SEUL" = 1 ] || sha256sum "$OUTIL"/bin/Release/net10.0/iXtal26*.dll > "$SORTIE/dll.sha256"
[ "$CODE_SEUL" = 1 ] && PASSAGES=0
for p in $(seq 1 "$PASSAGES"); do
  ordre=("${CHOISIS[@]}")
  if [ $((p % 2)) -eq 0 ]; then
    ordre=()
    for ((k = ${#CHOISIS[@]} - 1; k >= 0; k--)); do ordre+=("${CHOISIS[$k]}"); done
  fi
  for c in "${ordre[@]}"; do
    nom="${c%%|*}"
    invocations="${c##*|}"
    journal "passage $p, $nom"
    lancer "$SORTIE/passage$p/$nom" "*.$nom.*" --launchCount 3 --warmupCount "$ECHAUFFEMENTS" \
      --iterationCount "$ITERATIONS" --invocationCount "$invocations"
  done
done

# Le code machine. Les motifs sur une ligne, séparés par des espaces (la syntaxe de DOTNET_JitDisasm).
motifs="$(tr '\n' ' ' <<< "$DESASSEMBLER")"
for c in "${CHOISIS[@]}"; do
  nom="${c%%|*}"
  journal "code machine, $nom"
  lancer "$SORTIE/jit/$nom" "*.$nom.Retenue" --job dry --envVars DOTNET_TieredCompilation:0 "DOTNET_JitDisasm:$motifs" \
    DOTNET_JitDisasmDiffable:1
  cp "$(ls "$SORTIE/jit/$nom"/*.log | grep -v '/sortie.log$' | tail -1)" "$SORTIE/jit/$nom.log"
done

python3 -I "$OUTIL/interdits.py" "$SORTIE" || exit 2
journal "fin : $SORTIE/rapport.txt"
