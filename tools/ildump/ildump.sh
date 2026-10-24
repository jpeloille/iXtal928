#!/usr/bin/env bash
# SPDX-FileCopyrightText: 2026 Julien Peloille
# SPDX-License-Identifier: GPL-2.0-only
#
# ildump.sh — construit tools/ildump (Release) puis le lance ; les arguments sont ceux d'ildump (Program.cs en dit
# l'usage). Avant de lancer, il vérifie lui-même que sa construction ne tire aucun paquet NuGet : les seules sources de
# restauration sont le répertoire du projet (RestoreSources, iXtal26.IlDump.csproj), qui ne contient aucun .nupkg, et
# /usr/lib/dotnet/library-packs, le répertoire local que le SDK ajoute ; obj/project.assets.json ne doit lister aucune
# bibliothèque. Rien n'est téléchargé, rien n'est installé. Le journal de la construction va dans obj/ : effacé si
# elle réussit, gardé si elle échoue (son chemin est dit).
set -euo pipefail
ICI="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
if ls "$ICI"/*.nupkg > /dev/null 2>&1; then echo "ildump.sh : un .nupkg dans $ICI ; refusé." >&2; exit 2; fi
mkdir -p "$ICI/obj"
J="$ICI/obj/construction.log"
if ! dotnet build "$ICI/iXtal26.IlDump.csproj" -c Release -nologo -v q > "$J" 2>&1; then
  cat "$J" >&2; echo "ildump.sh : la construction a échoué ; son journal : $J" >&2; exit 2
fi
rm -f "$J"
python3 - "$ICI/obj/project.assets.json" << 'PY' || exit 2
import json, sys
a = json.load(open(sys.argv[1], encoding='utf-8'))
libs = a.get('libraries', {})
if libs:
    sys.exit('ildump.sh : la construction tire des paquets NuGet : ' + ', '.join(sorted(libs)))
PY
exec dotnet "$ICI/bin/Release/net10.0/iXtal26.IlDump.dll" "$@"
