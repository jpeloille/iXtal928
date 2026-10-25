#!/usr/bin/env bash
# SPDX-FileCopyrightText: 2026 Julien Peloille
# SPDX-License-Identifier: GPL-2.0-only
#
# ORACLE: (aucun) — outillage de mesure.
#
# LE BANC CONTRE LA RÉFÉRENCE FIGÉE (G14.0b ; PLAN-G14.md § G14.0b, décisions n° 6, 7 et 12 ; R0-contre.md § 3).
#
#   tools/perfbanc/compare-ref.sh [--construire] [--empreintes-r FICHIER] [--lancees-r FICHIER] R_ARBRE C_ARBRE \
#       SORTIE [GROUPES]
#
# R est l'arbre de référence (le binaire figé), C le candidat ; deux worktrees, chacun construit en Release. GROUPES,
# facultatif, est une expression régulière (grep -E) sur les noms de groupes de la liste ci-dessous.
#
# Pour chaque groupe retenu, quatre passages, R puis C puis C puis R (ABBA : la dérive de l'hôte, la chaleur, se
# répartit entre les deux côtés) ; chacun lance le banc de son arbre,
#   dotnet <arbre>/tools/perfbanc/bin/Release/net10.0/iXtal26.PerfBanc.dll --filter … --launchCount 3 \
#       --warmupCount N --iterationCount M [--invocationCount K] --exporters fulljson
# sous `setarch $(uname -m) -R taskset -c $COEURS` : les adresses ne sont pas tirées au hasard, et le banc tourne sur
# des cœurs P, un fil par cœur physique (R0-contre § 2.7). Puis compare_ref.py rend le verdict (son en-tête dit les
# règles). Les groupes dont un cas est rouge statistique sont rejoués une seconde fois, dans l'ordre C R R C, par le
# script lui-même ; ceux qui y sont rouges encore le sont une troisième fois, dans l'ordre R C C R (Q37, tranchée
# par Julien le 24/10). Un rouge statistique n'est retenu que si les trois passes le voient.
#
# --construire : construit les deux arbres (dotnet build -c Release du banc) avant la première mesure ; sinon, les
# DLL doivent être là. --empreintes-r FICHIER : un `sha256sum` des trois DLL de R (iXtal26, iXtal26.SoftFloat,
# iXtal26.PerfBanc, chemins relatifs à R) ; un écart refuse la passe (G14.0d : la référence ne bouge plus). Ce sont
# les DLL de bin/, et non celles que BenchmarkDotNet reconstruit depuis le source et lance : un source de R modifié
# sans reconstruction n'y paraît pas. --lancees-r FICHIER (G14.0d) garde donc le binaire lancé : les empreintes
# normalisées des trois DLL lancées du côté R, celles du gel, une ligne « EMPREINTE  NOM » chacune (`compare_ref.py gel
# SORTIE` les écrit depuis une passe). Chaque passage de R est vérifié dès sa fin : une empreinte qui diffère, ou une
# DLL lancée qui n'est pas relevée, refuse la passe, qui s'arrête là. Le comparateur le vérifie aussi, passage par
# passage, et l'en-tête du rapport le dit. L'option reste facultative : la garde est à la charge de l'appelant (la
# consigne de G14.1-4 l'impose). L'empreinte normalisée dépend de la longueur du chemin de l'arbre (celle du chemin du
# PDB déplace les sections) : le gel ne vaut que pour un R dont le chemin a la longueur de /var/tmp/ixtal-g14/ref-R.
#
# L'affinité : COEURS (par défaut 4,6,8,10). L'i5-14600KF a six cœurs P (CPU 0-11, deux fils chacun : 0 et 1 sont le
# même cœur, 2 et 3 aussi…) et huit cœurs E (12-19, moins rapides). 4, 6, 8 et 10 : quatre cœurs P distincts, un fil
# chacun ; le cœur 0, où le noyau sert le plus d'interruptions, reste à l'hôte. Quatre et non deux : le banc tourne
# dans un processus enfant de BenchmarkDotNet, qui a lui-même son processus parent et le fil du ramasse-miettes.
#
# Code de sortie : 0 vert, 1 rouge, 3 refus (ni vert ni rouge : la passe est à refaire), 2 erreur d'usage ou d'outil.
# Les codes 4 et 5 sont internes : compare_ref.py rend 4 quand des rouges statistiques attendent leur passe 2, et 5
# quand des rouges confirmés deux fois attendent leur passe 3 ; le script joue alors la passe, et ne sort jamais avec
# eux (sauf si on lance le comparateur seul sur une sortie inachevée).
# Le rapport : SORTIE/rapport.txt et SORTIE/rapport.tsv ; les journaux, JSON et contextes, sous SORTIE/passeN/.
#
# Mesuré en G14.0b (VERIFICATION.md § G14.0b) : les 65 cas en 53 à 56 min, plus la confirmation (de 1,5 min pour
# Fetch à 25 min pour Machine386). BenchmarkDotNet reconstruit le banc et iXtal26 dans un répertoire qu'il génère
# (iXtal26.PerfBanc-<job>-1) : les DLL lancées ne sont pas celles de l'arbre, et leur sha256 dépend du chemin ; le
# script garde leur empreinte normalisée (compare_ref.py), qui doit être la même aux deux passages d'un côté.

set -u
export LC_ALL=C

OUTIL="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
COMPARATEUR="$OUTIL/compare_ref.py"
COEURS="${COEURS:-4,6,8,10}"

# LE JEU DE CAS DE LA RÉFÉRENCE (décision n° 6). Un groupe est l'unité de l'ABBA : un lancement du banc, une
# construction du projet que BenchmarkDotNet génère, ses cas. Champs : nom | cas attendus | invocations par itération
# (vide : celles du banc) | échauffements | itérations | filtres de BenchmarkDotNet (des globs sur le nom complet du
# cas, paramètres compris), séparés par « ; ».
# - Les cas neufs de G14.0a, tous (44) : Exec286, Exec486, Exec8088, MemoireLente, RenduCga, Rendu8bpp, SonOplPro et
#   Machine, dans les deux modes.
# - Les bancs de G13, en « aucun » et en « tout » :
#   - Fetch : FetchPcem et FetchMateriel ; FetchPb87 sort (PB-87 est dans « tout ») ;
#   - Exec386 : les trois boucles en mode PCem ; Exec386Pb51, Exec386PrefixePb51 et Exec386MemoirePb189 sortent : le
#     chemin du mode matériel d'exec386 (le décodeur de PB-50/51, les limites de PB-189) est mesuré par Exec286 et
#     Exec486 en « tout » (le même exec386, toutes les corrections), et sur l'i386SX par Machine ami386 en « tout » ;
#   - Video : VideoBanc n'a pas de « tout » ; PB-99 (l'EGA) et PB-226 (l'ET4000) sont gardés, parce qu'aucun autre cas
#     ne passe par le mode matériel de ces deux cartes (Machine monte la CGA, la Trident, la GD5429) ; à 16 lectures
#     seulement : la trame à 0 lecture est contenue dans celle à 16 (le balayage, plus les lectures de 3DAh) ;
#   - Son : « aucun » et « son » (le domaine entier, le « tout » de SonBanc) de chaque cas ; les PB seuls sortent
#     (leur code est dans « son ») ; SonAweWc à 16 lectures seulement (le tampon de l'AWE32 et les lectures de WC).
# - Hors de la référence : GardeBanc et Is486Banc (des copies locales du code), les cas par PB nommés ci-dessus.
# Machine est coupé en trois groupes par cœur (execx86, le 286, exec386 du 386 et du 486) : un rouge n'y fait
# rejouer que son tiers. 65 cas en tout.
GROUPES_REF=()
groupe() {
  local nom="$1" n="$2" k="$3" w="$4" i="$5"
  shift 5
  local IFS=';'
  GROUPES_REF+=("$nom|$n|$k|$w|$i|$*")
}
groupe Fetch 2 512 3 8 '*.FetchPcem.*' '*.FetchMateriel.*'
groupe Exec386 3 512 3 8 '*.Exec386Pcem.*' '*.Exec386PrefixePcem.*' '*.Exec386MemoirePcem.*'
groupe Exec286 6 512 3 8 '*.Exec286.*'
groupe Exec486 6 384 3 8 '*.Exec486.*'
groupe Exec8088 2 512 3 8 '*.Exec8088.*'
groupe MemoireLente 4 128 3 8 '*.MemoireLente.*'
groupe RenduCga 2 384 3 8 '*.RenduCga.*'
groupe Rendu8bpp 4 384 3 8 '*.Rendu8bpp.*'
groupe Video 4 128 3 8 \
  '*.VideoEgaPcem.*Lectures: 16)' '*.VideoEgaPb99.*Lectures: 16)' \
  '*.VideoEt4Pcem.*Lectures: 16)' '*.VideoEt4Pb226.*Lectures: 16)'
groupe Son 12 160 3 8 \
  '*.SonSb16Sortie.*"aucun")' '*.SonSb16Sortie.*"son")' '*.SonSb16Entree.*"aucun")' '*.SonSb16Entree.*"son")' \
  '*.SonProSortie.*"aucun")' '*.SonProSortie.*"son")' '*.SonAdpcm.*"aucun")' '*.SonAdpcm.*"son")' \
  '*.SonAweVoix.*"aucun")' '*.SonAweVoix.*"son")' \
  '*.SonAweWc.*"aucun", Lectures: 16)' '*.SonAweWc.*"son", Lectures: 16)'
groupe SonOplPro 2 160 3 8 '*.SonOplPro.*'
groupe Machine808x 8 '' 1 4 \
  '*.Machine.*"ibmpc", *' '*.Machine.*"ibmxt", *' '*.Machine.*"pc1512", *' '*.Machine.*"olivetti_m24", *'
groupe Machine286 4 '' 1 4 '*.Machine.*"ami286", *' '*.Machine.*"ibmat", *'
groupe Machine386 6 '' 1 4 '*.Machine.*"ami386", *' '*.Machine.*"ami386dx", *' '*.Machine.*"ami486", *'

usage() {
  sed -n '9,10p' "$0" | sed 's/^# \{0,1\}//' >&2
  exit 2
}

CONSTRUIRE=0
EMPREINTES_R=""
LANCEES_R=""
while [ $# -gt 0 ]; do
  case "$1" in
    --construire) CONSTRUIRE=1; shift ;;
    --empreintes-r) [ $# -ge 2 ] || usage; EMPREINTES_R="$(realpath -e -- "$2")" || usage; shift 2 ;;
    --lancees-r) [ $# -ge 2 ] || usage; LANCEES_R="$(realpath -e -- "$2")" || usage; shift 2 ;;
    --) shift; break ;;
    -*) usage ;;
    *) break ;;
  esac
done
[ $# -ge 3 ] && [ $# -le 4 ] || usage
R_ARBRE="$(cd "$1" 2>/dev/null && pwd)" || { echo "compare-ref : $1 n'existe pas" >&2; exit 2; }
C_ARBRE="$(cd "$2" 2>/dev/null && pwd)" || { echo "compare-ref : $2 n'existe pas" >&2; exit 2; }
SORTIE="$3"
FILTRE="${4:-.}"
if [ -e "$SORTIE" ] && [ -n "$(ls -A "$SORTIE" 2>/dev/null)" ]; then
  echo "compare-ref : $SORTIE existe et n'est pas vide" >&2
  exit 2
fi
mkdir -p "$SORTIE" && SORTIE="$(cd "$SORTIE" && pwd)"
BANC=tools/perfbanc/bin/Release/net10.0
DEBUT=$(date +%s.%N)
# Le gel des DLL lancées, lu avant toute mesure : un fichier mal formé est une erreur d'usage.
if [ -n "$LANCEES_R" ] && ! python3 -I "$COMPARATEUR" gel-lire "$LANCEES_R" > /dev/null; then
  echo "compare-ref : $LANCEES_R n'est pas un gel des DLL lancées (compare_ref.py gel)" >&2
  exit 2
fi
REFUS=()

journal() { printf '%s %s\n' "$(date +%H:%M:%S)" "$*" | tee -a "$SORTIE/deroulement.log" >&2; }

runtimes() { { dotnet --list-runtimes; dotnet --list-sdks; } 2>&1; }

sonde() { python3 -I "$COMPARATEUR" sonde --racine $$ --duree 1; }

# Le moment de mesurer : aucun processus lourd. Au début d'un passage, on attend (au plus dix minutes) qu'il finisse ;
# à la fin, il refuse la passe (compare_ref.py le lit dans passage.json).
attendre_calme() {
  local f="$1" k
  local lourds
  for k in $(seq 1 20); do
    sonde > "$f"
    lourds="$(python3 -I -c 'import json, sys
print("; ".join(x["cmd"][:70] for x in json.load(open(sys.argv[1]))["lourds"]))' "$f")"
    [ -z "$lourds" ] && return 0
    journal "un processus lourd tourne ($lourds) ; attente"
    sleep 30
  done
  return 1
}

# Les arbres.
verifier_arbre() {
  local cote="$1" arbre="$2"
  [ -f "$arbre/tools/perfbanc/iXtal26.PerfBanc.csproj" ] || { echo "compare-ref : $arbre n'a pas le banc" >&2; exit 2; }
  if ! (cd "$arbre/roms" 2>/dev/null && sha256sum --quiet -c roms.sha256 > /dev/null 2>&1); then
    REFUS+=("côté $cote : roms/ ne passe pas roms.sha256")
  fi
}
verifier_arbre R "$R_ARBRE"
verifier_arbre C "$C_ARBRE"

if [ "$CONSTRUIRE" = 1 ]; then
  attendre_calme "$SORTIE/sonde-construction.json" || { journal "refus : la machine n'est pas au calme"; exit 3; }
  for a in "$R_ARBRE" "$C_ARBRE"; do
    journal "construction de $a"
    if ! dotnet build -c Release -nologo -v q --disable-build-servers "$a/tools/perfbanc/iXtal26.PerfBanc.csproj" \
         > "$SORTIE/construction-$(basename "$a").log" 2>&1; then
      journal "la construction de $a échoue (construction-$(basename "$a").log)"
      exit 2
    fi
  done
fi

dll_arbre() {
  local arbre="$1" nom
  for nom in iXtal26.dll iXtal26.SoftFloat.dll iXtal26.PerfBanc.dll; do
    if [ -f "$arbre/$BANC/$nom" ]; then
      printf '%s\t%s\n' "$nom" "$(sha256sum < "$arbre/$BANC/$nom" | cut -d' ' -f1)"
    else
      printf '%s\t\n' "$nom"
    fi
  done
}
dll_arbre "$R_ARBRE" > "$SORTIE/dll-R.tsv"
dll_arbre "$C_ARBRE" > "$SORTIE/dll-C.tsv"
if [ -n "$EMPREINTES_R" ]; then
  if ! (cd "$R_ARBRE" && sha256sum --quiet -c "$EMPREINTES_R" > "$SORTIE/empreintes-r.log" 2>&1); then
    REFUS+=("R n'est pas la référence : ses DLL ne sont pas celles de $EMPREINTES_R (empreintes-r.log)")
  fi
fi

# L'affinité telle que le noyau l'écrit (/proc/PID/status), pour la comparer à celle vue sur les processus du banc.
COEURS_NOYAU="$(taskset -c "$COEURS" awk '/^Cpus_allowed_list/ {print $2}' /proc/self/status)" || {
  echo "compare-ref : taskset -c $COEURS refusé" >&2; exit 2; }
RUNTIMES_DEBUT="$(runtimes)"
# Le noyau, le processeur, le gouverneur : consignés pour voir l'hôte dériver d'une passe à l'autre.
printf '%s\n' "$RUNTIMES_DEBUT" > "$SORTIE/runtimes-debut.txt"

# Les groupes retenus.
CHOISIS=()
for g in "${GROUPES_REF[@]}"; do
  nom="${g%%|*}"
  if printf '%s\n' "$nom" | grep -Eq -- "$FILTRE"; then CHOISIS+=("$g"); fi
done
[ ${#CHOISIS[@]} -gt 0 ] || { echo "compare-ref : aucun groupe ne répond à « $FILTRE »" >&2; exit 2; }

ecrire_contexte() {
  local fin="$1"
  python3 -I - "$SORTIE" "$R_ARBRE" "$C_ARBRE" "$COEURS_NOYAU" "$DEBUT" "$fin" "${LANCEES_R:--}" "${#REFUS[@]}" \
      "${REFUS[@]}" -- "${CHOISIS[@]}" <<'EOF'
import json, platform, subprocess, sys, time
from pathlib import Path
sortie, r, c, coeurs, debut, fin, gel, n = sys.argv[1:9]
refus = sys.argv[9:9 + int(n)]
groupes = sys.argv[sys.argv.index("--") + 1:]
def lire(f):
    p = Path(sortie) / f
    return p.read_text() if p.exists() else None
def dll(f):
    return dict(l.split("\t") for l in (Path(sortie) / f).read_text().splitlines())
def commit(a):
    try:
        return subprocess.run(["git", "-C", a, "rev-parse", "HEAD"], capture_output=True, text=True).stdout.strip()
    except OSError:
        return "?"
def sale(a):
    return subprocess.run(["git", "-C", a, "status", "--porcelain", "--untracked-files=no"], capture_output=True,
                          text=True).stdout.splitlines()
proc = "?"
for l in Path("/proc/cpuinfo").read_text().splitlines():
    if l.startswith("model name"):
        proc = l.split(":", 1)[1].strip()
        break
try:
    gouv = Path("/sys/devices/system/cpu/cpu4/cpufreq/scaling_governor").read_text().strip()
except OSError:
    gouv = "?"
ctx = {
    "arbre": {"R": r, "C": c}, "commit": {"R": commit(r), "C": commit(c)},
    "modifies": {"R": sale(r), "C": sale(c)},
    "dll_arbre": {"R": dll("dll-R.tsv"), "C": dll("dll-C.tsv")}, "coeurs": coeurs,
    "processeur": proc, "gouverneur": gouv, "noyau": platform.release(),
    "runtimes_debut": lire("runtimes-debut.txt"), "runtimes_fin": lire("runtimes-fin.txt"),
    "debut": time.strftime("%Y-%m-%dT%H:%M:%S", time.localtime(float(debut))),
    "duree_s": round(float(fin) - float(debut), 1) if fin != "-" else None,
    "refus": refus,
    "lancees_r_fichier": None if gel == "-" else gel,
    "lancees_r": None if gel == "-" else {l.split()[1]: l.split()[0] for l in Path(gel).read_text().splitlines()
                                            if l.strip()},
    "cas_attendus": {g.split("|")[0]: int(g.split("|")[1]) for g in groupes},
    "job": {g.split("|")[0]: "|".join(g.split("|")[2:5]) for g in groupes},
}
(Path(sortie) / "contexte.json").write_text(json.dumps(ctx, ensure_ascii=False, indent=1))
EOF
}

# Un passage : un côté, un groupe.
passage() {
  local passe="$1" ordre="$2" cote="$3" ligne="$4"
  local nom attendus invocations echauffements iterations filtres arbre dir
  IFS='|' read -r nom attendus invocations echauffements iterations filtres <<< "$ligne"
  if [ "$cote" = R ]; then arbre="$R_ARBRE"; else arbre="$C_ARBRE"; fi
  dir="$SORTIE/passe$passe/$nom/$ordre-$cote"
  mkdir -p "$dir"
  local -a f=() opts=(--launchCount 3 --warmupCount "$echauffements" --iterationCount "$iterations")
  IFS=';' read -r -a f <<< "$filtres"
  [ -n "$invocations" ] && opts+=(--invocationCount "$invocations")
  rm -rf "$arbre/$BANC"/iXtal26.PerfBanc-*
  if ! attendre_calme "$dir/sonde-avant.json"; then
    journal "refus : un processus lourd ne finit pas (passe $passe, $nom, $ordre-$cote)"
    REFUS+=("passe $passe, $nom, $ordre-$cote : la machine n'est pas revenue au calme en dix minutes")
    return 1
  fi
  runtimes > "$dir/runtimes-avant.txt"
  journal "passe $passe, $nom, $ordre-$cote ($arbre)"
  local debut fin rc pid p
  debut=$(date +%s.%N)
  (cd "$arbre" && exec setarch "$(uname -m)" -R taskset -c "$COEURS" dotnet "$arbre/$BANC/iXtal26.PerfBanc.dll" \
      --filter "${f[@]}" "${opts[@]}" --exporters fulljson --keepFiles --artifacts "$dir") \
      > "$dir/sortie.log" 2>&1 &
  pid=$!
  : > "$dir/affinite.txt"
  # La personnalité se lit par le shell lui-même (un `read` intégré) : il est l'ancêtre des processus du banc, et
  # ptrace_scope = 1 refuse la lecture à tout autre processus.
  local perso dll
  while kill -0 "$pid" 2> /dev/null; do
    for p in $(pgrep -f 'iXtal26\.PerfBanc' 2> /dev/null); do
      [ -r "/proc/$p/status" ] || continue
      # Les processus du banc seuls : dotnet, et en argv[1] le banc ou le projet que BenchmarkDotNet génère.
      dll="$(tr '\0' '\n' < "/proc/$p/cmdline" 2> /dev/null | sed -n 2p)"
      case "${dll##*/}" in iXtal26.PerfBanc*.dll) ;; *) continue ;; esac
      perso=""
      read -r perso < "/proc/$p/personality" 2> /dev/null
      printf '%s\t%s\t%s\n' "${dll##*/}" \
        "$(awk '/^Cpus_allowed_list/ {print $2}' "/proc/$p/status" 2> /dev/null)" \
        "$perso" >> "$dir/affinite.txt"
    done
    sleep 3
  done
  wait "$pid"
  rc=$?
  fin=$(date +%s.%N)
  sort -u -o "$dir/affinite.txt" "$dir/affinite.txt"
  sonde > "$dir/sonde-apres.json"
  runtimes > "$dir/runtimes-apres.txt"
  python3 -I "$COMPARATEUR" consigner --dir "$dir" --cote "$cote" --arbre "$arbre" --groupe "$nom" \
    --passe "$passe" --ordre "$ordre" --rc "$rc" --debut "$debut" --fin "$fin" \
    --sonde-avant "$dir/sonde-avant.json" --sonde-apres "$dir/sonde-apres.json" \
    --runtimes-avant "$dir/runtimes-avant.txt" --runtimes-apres "$dir/runtimes-apres.txt" --generes "$arbre/$BANC"
  rm -rf "$arbre/$BANC"/iXtal26.PerfBanc-*
  journal "  fini en $(awk -v a="$debut" -v b="$fin" 'BEGIN { printf "%.0f", b - a }') s, code $rc"
  # G14.0d : le binaire que BenchmarkDotNet a lancé du côté R doit être celui du gel ; sinon, la passe s'arrête ici.
  if [ "$cote" = R ] && [ -n "$LANCEES_R" ]; then
    local ecart
    if ! ecart="$(python3 -I "$COMPARATEUR" gel-verifier --passage "$dir/passage.json" --gel "$LANCEES_R")"; then
      journal "refus : le binaire lancé du côté R n'est pas celui du gel (passe $passe, $nom, $ordre-$cote) : $ecart"
      REFUS+=("passe $passe, $nom, $ordre-$cote : le binaire lancé du côté R n'est pas celui du gel : $ecart")
      return 1
    fi
  fi
}

jouer_passe() {
  local passe="$1" ordre_cotes="$2"; shift 2
  local ligne k c
  for ligne in "$@"; do
    k=0
    for c in $ordre_cotes; do
      k=$((k + 1))
      passage "$passe" "$k" "$c" "$ligne" || return 1
    done
  done
}

ecrire_contexte -
if [ ${#REFUS[@]} -gt 0 ]; then
  journal "refus avant la mesure : ${REFUS[*]}"
  python3 -I "$COMPARATEUR" analyser "$SORTIE"
  exit 3
fi

abandon() {
  runtimes > "$SORTIE/runtimes-fin.txt"
  ecrire_contexte "$(date +%s.%N)"
  python3 -I "$COMPARATEUR" analyser "$SORTIE"
  exit 3
}

journal "passe 1 (R C C R) : ${#CHOISIS[@]} groupes, cœurs $COEURS_NOYAU"
jouer_passe 1 "R C C R" "${CHOISIS[@]}" || abandon
ecrire_contexte -
python3 -I "$COMPARATEUR" analyser "$SORTIE" --silencieux
code=$?
if [ "$code" = 4 ]; then
  CONFIRMER=()
  while read -r nom; do
    for g in "${CHOISIS[@]}"; do [ "${g%%|*}" = "$nom" ] && CONFIRMER+=("$g"); done
  done < "$SORTIE/a-confirmer.txt"
  journal "passe 2 (C R R C), la confirmation : ${CONFIRMER[*]%%|*}"
  jouer_passe 2 "C R R C" "${CONFIRMER[@]}" || abandon
  ecrire_contexte -
  python3 -I "$COMPARATEUR" analyser "$SORTIE" --silencieux
  code=$?
fi
# Q37 (Julien, 24/10) : un rouge confirmé deux fois est rejoué une troisième fois, par son groupe seul, dans l'ordre
# de la passe 1 ; il n'est retenu que si cette passe le voit aussi.
if [ "$code" = 5 ]; then
  REJOUER=()
  while read -r nom; do
    for g in "${CHOISIS[@]}"; do [ "${g%%|*}" = "$nom" ] && REJOUER+=("$g"); done
  done < "$SORTIE/a-rejouer.txt"
  journal "passe 3 (R C C R), la troisième : ${REJOUER[*]%%|*}"
  jouer_passe 3 "R C C R" "${REJOUER[@]}" || abandon
fi
runtimes > "$SORTIE/runtimes-fin.txt"
ecrire_contexte "$(date +%s.%N)"
python3 -I "$COMPARATEUR" analyser "$SORTIE"
code=$?
journal "verdict : code $code ($(head -1 "$SORTIE/rapport.txt"))"
exit "$code"
