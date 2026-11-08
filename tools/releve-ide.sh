#!/usr/bin/env bash
# SPDX-FileCopyrightText: 2026 Julien Peloille
# SPDX-License-Identifier: GPL-2.0-only
#
# ORACLE: (aucun) — outillage de la preuve des membres (G15.0b5b, PLAN-G15.md § G15.0b).
#
# releve-ide.sh [ARBRE] — le relevé exact des directives `using` inutiles (IDE0005) et, en second relevé du code mort
# privé, des membres privés inutilisés ou jamais lus (IDE0051, IDE0052), par les analyseurs de style du SDK (aucun
# paquet). R2-nettoyage.md § 1.2 n'en donnait qu'une borne basse (45 `using`) ; ce relevé est la liste que G15.1
# ôtera, pour les projets qu'il construit : ni tools/ildump, ni tools/renommage, ni tools/softfloat/Banc.
#
# Une construction jetable : les fichiers suivis d'ARBRE (par défaut le dépôt du script), leurs modifications non
# commitées comprises, copiés dans un répertoire temporaire ; à la fin de la copie de `.editorconfig`, une section
# `[*.cs]` qui met les trois règles à `warning` (elle vient après le pacte du code transcrit, qui les éteint, et
# l'emporte) ; puis iXtal26, iXtal26.Diff et iXtal26.PerfBanc construits en Debug, avec EnforceCodeStyleInBuild,
# GenerateDocumentationFile (IDE0005 ne se rapporte pas à la construction sans lui) et sans TreatWarningsAsErrors.
# iXtal26.SoftFloat l'est avec iXtal26. Le dépôt n'a qu'un `#if` (RELEASE_BUILD, jamais défini, sans `using`) : une
# configuration suffit.
#
# La sortie, sur la sortie standard, en TSV trié : règle, fichier (relatif à l'arbre), ligne, colonne, la ligne du
# source (ses blancs réduits) pour IDE0005 ou le membre nommé par le message pour IDE0051 et IDE0052. Puis, sur
# l'erreur, le compte par règle et par projet (un fichier compte au premier projet qui le signale, dans cet ordre).
# Retour : 0 ; 2 une construction en erreur, une copie ratée (un fichier suivi absent de l'arbre), une lecture ratée.
set -uo pipefail
ARBRE=$(realpath "${1:-$(dirname "${BASH_SOURCE[0]}")/..}")
[ -d "$ARBRE/.git" ] || [ -f "$ARBRE/.git" ] || { echo "releve-ide : $ARBRE n'est pas un arbre git" >&2; exit 2; }
W=$(mktemp -d "${TMPDIR:-/tmp}/releve-ide-XXXXXX") || exit 2
[ -n "$W" ] && [ -d "$W" ] || exit 2
trap 'rm -rf "$W"' EXIT
mkdir -p "$W/arbre" && git -C "$ARBRE" ls-files -z | rsync -a --from0 --files-from=- "$ARBRE/" "$W/arbre/" \
  || { echo "releve-ide : la copie a échoué" >&2; exit 2; }
printf '\n[*.cs]\n%s\n%s\n%s\n' "dotnet_diagnostic.IDE0005.severity = warning" \
  "dotnet_diagnostic.IDE0051.severity = warning" "dotnet_diagnostic.IDE0052.severity = warning" \
  >> "$W/arbre/.editorconfig"
# Les avertissements de la documentation XML, que GenerateDocumentationFile éveille : hors du relevé.
NOWARN="CS1591%3BCS1573%3BCS1572%3BCS1570%3BCS1574%3BCS1584%3BCS1658%3BCS1587%3BCS1734%3BCS1711%3BCS1712"
for p in iXtal26/iXtal26.csproj tools/iXtal26.Diff/iXtal26.Diff.csproj tools/perfbanc/iXtal26.PerfBanc.csproj; do
  (cd "$W/arbre" && dotnet build "$p" -c Debug -nologo -v q -p:TreatWarningsAsErrors=false \
     -p:GenerateDocumentationFile=true -p:EnforceCodeStyleInBuild=true "-p:NoWarn=$NOWARN" > "$W/build.log" 2>&1) || {
    echo "releve-ide : la construction de $p a échoué :" >&2; grep -E ' error ' "$W/build.log" | head -20 >&2; exit 2; }
  cat "$W/build.log" >> "$W/tout.log"
done
python3 -I -B - "$W/arbre" "$W/tout.log" <<'EOF'
import re, sys
from collections import Counter
from pathlib import Path
arbre, log = Path(sys.argv[1]), Path(sys.argv[2])
vu, compte = set(), Counter()
motif = re.compile(r"^(.+?)\((\d+),(\d+)\): warning (IDE0005|IDE0051|IDE0052): (.*?)(?: \(https://\S+\))?"
                   r"(?: \[(.+)\])?$")
for ligne in log.read_text().splitlines():
    m = motif.match(ligne.strip())
    if not m:
        continue
    f, l, c, regle, msg, projet = m.groups()
    rel = Path(f).resolve().relative_to(arbre.resolve()).as_posix()
    if regle == "IDE0005":
        quoi = " ".join((arbre / rel).read_text().split("\n")[int(l) - 1].split())
    else:
        q = re.search(r"'([^']+)'", msg)
        quoi = q.group(1) if q else msg
    cle = (regle, rel, int(l), int(c), quoi)
    if cle not in vu:
        vu.add(cle)
        compte[(regle, Path(projet).stem if projet else "?")] += 1
for regle, rel, l, c, quoi in sorted(vu):
    print(f"{regle}\t{rel}\t{l}\t{c}\t{quoi}")
for (regle, projet), n in sorted(compte.items()):
    print(f"  {regle} {projet} : {n}", file=sys.stderr)
print(f"  en tout : {len(vu)}", file=sys.stderr)
EOF
[ $? -eq 0 ] || { echo "releve-ide : la lecture des avertissements a échoué" >&2; exit 2; }
