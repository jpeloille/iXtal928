#!/usr/bin/env bash
# SPDX-FileCopyrightText: 2026 Julien Peloille
# SPDX-License-Identifier: GPL-2.0-only
#
# listings-jit.sh — les listings du JIT d'iXtal26, scénario par scénario (G13 : M0 et M2, PLAN-G13.md § La
# vérification). Le mode PCem ne coûte rien si chaque garde du mode matériel est pliée par le JIT : le code machine
# des méthodes gardées est alors celui d'avant la garde, à l'octet près. C'est la preuve directe du 0 %.
#
#   tools/listings-jit.sh capture DLL SORTIE [SCÉNARIO…]   joue les scénarios avec iXtal26.dll (Release), un listing
#                                                         par scénario dans SORTIE (défaut : tous les scénarios)
#   tools/listings-jit.sh compare REF AUTRE               compare deux captures méthode par méthode : les méthodes
#                                                         changées, apparues ou disparues ; retour 1 s'il y en a
#   tools/listings-jit.sh methode CAPTURE NOM             imprime le listing d'une méthode (le premier scénario qui
#                                                         la compile), pour lire un écart
#
# La référence M0 est la capture du commit 860f2b1 (G13.1), le dernier sans garde ; une capture se rejoue à
# l'identique depuis son commit, avec le même runtime (noté dans SORTIE/runtime.txt). Chaque scénario tourne dans un
# bac à sable, comme les portes de par.sh : nvr/default et os/pcdos20 y sont COPIÉS (une machine AT réécrit son CMOS
# en sortant), le reste du dépôt y est lié. Les listings viennent de DOTNET_JitDisasm='*' et JitDisasmDiffable (les
# adresses remplacées), le résumé de JitDisasmSummary (aucune méthode ne doit y être en MinOpts) ; le projet compile
# sans paliers, chaque méthode une seule fois. Release seulement : en Debug, rien ne se plie. Les scénarios tournent
# sans randomisation des adresses (setarch -R) : selon l'endroit où tombent ses champs statiques, le JIT les atteint
# par un adressage relatif ou par une adresse absolue, et deux exécutions du même binaire donnaient deux listings.
set -uo pipefail
REPO=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)

# Les scénarios : un nom, puis les arguments d'iXtal26. Ils couvrent les cœurs 8088, 286, 386 et 486, le 8087, le 287
# et le 387 (les tests de coprocesseur du POST), PC-DOS sur l'XT, et sous DEBUG les instructions dont une garde
# n'est compilée par aucun amorçage (SBB octet : setsbc8). G13.4 : le PC1512 (amstrad_init), et la CH et la TM lues
# sous DEBUG (O 201, I 201). La souris PS/2 n'a pas de scénario : aucune machine du dépôt ne la monte, et --force-ps2
# n'existe sous --boot que depuis G13.4. Dans un texte de --type, « _ » tient lieu d'espace.
SCENARIOS=(
  "xt-dos|--boot roms 8000 --model ibmxt --floppy-a os/pcdos20/pcdos20b.img --floppy-b os/pcdos20/pcdos20s.img --settle 600 --type  --type  --type DIR --type B:DEBUG --type A_100 --type SBB_AL,1 --type SBB_AX,1 --type INT_3 --type  --type G=100 --type Q"
  "pc-8087-dos|--boot roms 7000 --config tools/gates/cfg/pc-8087.cfg --floppy-a os/pcdos20/pcdos20b.img --type  --type "
  "ibmat-287|--boot roms 3000 --config tools/gates/cfg/ibmat-287.cfg"
  "ami386dx-387|--boot roms 3000 --config tools/gates/cfg/ami386dx-387.cfg"
  "ami486-dx2|--boot roms 3000 --config tools/gates/cfg/ami486-dx2.cfg"
  "pc1512|--boot roms 3000 --model pc1512"
  "pc-joy-ch|--boot roms 7000 --config tools/gates/cfg/pc-joy-ch.cfg --floppy-a os/pcdos20/pcdos20b.img --floppy-b os/pcdos20/pcdos20s.img --settle 300 --type  --type  --type B:DEBUG --type O_201_0 --type I_201 --type Q"
  "pc-joy-tm|--boot roms 7000 --config tools/gates/cfg/pc-joy-tm.cfg --floppy-a os/pcdos20/pcdos20b.img --floppy-b os/pcdos20/pcdos20s.img --settle 300 --type  --type  --type B:DEBUG --type O_201_0 --type I_201 --type Q"
)

capture() {
  local dll out; dll=$(realpath -m "$1"); out=$(realpath -m "$2"); shift 2
  [ -f "$dll" ] || { echo "listings-jit : $dll introuvable" >&2; exit 2; }
  mkdir -p "$out"
  { dotnet --list-runtimes | grep Microsoft.NETCore.App; echo "dll $(sha256sum "$dll" | cut -c1-16)"; } > "$out/runtime.txt"
  local s nom args w
  for s in "${SCENARIOS[@]}"; do
    nom=${s%%|*}; args=${s#*|}
    if [ $# -gt 0 ] && [[ " $* " != *" $nom "* ]]; then continue; fi
    w=$(mktemp -d "${TMPDIR:-/tmp}/listings-jit-XXXXXX")
    mkdir -p "$w/nvr" "$w/os"
    for e in "$REPO"/*; do case "$(basename "$e")" in nvr|os) ;; *) ln -s "$e" "$w/";; esac; done
    cp -r "$REPO/nvr/default" "$w/nvr/"; cp -r "$REPO/os/pcdos20" "$w/os/"
    # Le texte de --type vide (Entrée seule) se passe par un argument vide : l'aplatissement des arguments le perdrait.
    local -a argv=()
    read -r -a mots <<< "$args"
    local i=0
    while [ $i -lt ${#mots[@]} ]; do
      argv+=("${mots[$i]}")
      if [ "${mots[$i]}" = "--type" ]; then
        if [ $((i + 1)) -lt ${#mots[@]} ] && [[ "${mots[$((i + 1))]}" != --* ]]; then
          argv+=("${mots[$((i + 1))]//_/ }"); i=$((i + 1))
        else
          argv+=("")
        fi
      fi
      i=$((i + 1))
    done
    rm -f "$out/$nom.txt"
    (cd "$w" && env -u DOTNET_TieredCompilation -u DOTNET_TieredPGO -u DOTNET_ReadyToRun \
      DOTNET_JitDisasm='*' DOTNET_JitDisasmDiffable=1 DOTNET_JitDisasmSummary=1 DOTNET_JitStdOutFile="$out/$nom.txt" \
      setarch "$(uname -m)" -R dotnet "$dll" "${argv[@]}" > "$out/$nom.sortie" 2>&1)
    echo "  $nom : retour $?, $(grep -c '^; Assembly listing for method' "$out/$nom.txt") méthodes," \
         "$(grep -c 'MinOpts' "$out/$nom.txt") mentions de MinOpts"
    rm -rf "$w"
  done
}

# Le découpage : un listing commence à « ; Assembly listing for method NOM (NIVEAU) ».
decoupe() {
  python3 - "$@" << 'PY'
import os, re, sys, json
def lire(d):
    m = {}
    for f in sorted(os.listdir(d)):
        if not f.endswith('.txt') or f == 'runtime.txt':
            continue
        cur, buf = None, []
        for l in open(os.path.join(d, f), encoding='utf-8', errors='replace'):
            h = re.match(r'^; Assembly listing for method (.*?)(?: \(([^)]*)\))?\s*$', l)
            if h:
                if cur and cur not in m:
                    m[cur] = (f, ''.join(buf))
                cur, buf = h.group(1), [l]
            elif cur is not None:
                if re.match(r'^\s*\d+: JIT compiled ', l):
                    continue
                buf.append(l)
        if cur and cur not in m:
            m[cur] = (f, ''.join(buf))
    return m
mode = sys.argv[1]
if mode == 'compare':
    a, b = lire(sys.argv[2]), lire(sys.argv[3])
    seuls_a = sorted(set(a) - set(b)); seuls_b = sorted(set(b) - set(a))
    change = sorted(k for k in set(a) & set(b) if a[k][1] != b[k][1])
    print(f'  {len(a)} méthodes dans la référence, {len(b)} dans l\'autre ; {len(change)} changées, '
          f'{len(seuls_a)} disparues, {len(seuls_b)} apparues')
    for k in change: print('  changée   ', k)
    for k in seuls_a: print('  disparue  ', k)
    for k in seuls_b: print('  apparue   ', k)
    sys.exit(1 if change or seuls_a or seuls_b else 0)
else:
    m = lire(sys.argv[2]); nom = sys.argv[3]
    hits = [k for k in m if nom in k]
    for k in hits[:5]:
        print(f'=== {k} ({m[k][0]})'); print(m[k][1])
    sys.exit(0 if hits else 1)
PY
}

case "${1:-}" in
capture) shift; [ $# -ge 2 ] || { echo "usage : listings-jit.sh capture DLL SORTIE [SCÉNARIO…]" >&2; exit 2; }; capture "$@" ;;
compare) [ $# -eq 3 ] || { echo "usage : listings-jit.sh compare REF AUTRE" >&2; exit 2; }; decoupe compare "$2" "$3" ;;
methode) [ $# -eq 3 ] || { echo "usage : listings-jit.sh methode CAPTURE NOM" >&2; exit 2; }; decoupe methode "$2" "$3" ;;
*) sed -n '5,12p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'; exit 2 ;;
esac
