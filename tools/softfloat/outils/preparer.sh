#!/usr/bin/env bash
# SPDX-FileCopyrightText: 2026 Julien Peloille
# SPDX-License-Identifier: GPL-2.0-only
#
# Prépare sources/softfloat/ (hors dépôt, comme le reste de sources/) : les archives de SoftFloat 3e et de TestFloat 3e
# de John R. Hauser, vérifiées par leur empreinte, décompressées, puis construites comme la référence de la
# réécriture : SoftFloat en build/Linux-x86_64-GCC avec SPECIALIZE_TYPE=8086 (les règles du x87 ; la build par défaut
# est 8086-SSE), TestFloat contre elle. Sans effet sur ce qui est déjà là et conforme.
set -euo pipefail

racine=$(cd "$(dirname "$0")/../../.." && pwd)
cible=$racine/sources/softfloat
mkdir -p "$cible/dl"
cd "$cible/dl"
while read -r somme archive; do
    [ -f "$archive" ] || curl -sSfLO "http://www.jhauser.us/arithmetic/$archive"
    echo "$somme  $archive" | sha256sum -c --quiet
done <<'EOF'
21130ce885d35c1fe73fc1e1bf2244178167e05c6747cad5f450cc991714c746 SoftFloat-3e.zip
6d4bdf0096b48a653aa59fc203a9e5fe18b5a58d7a1b715107c7146776a0aad6 TestFloat-3e.zip
EOF
cd "$cible"
[ -d SoftFloat-3e ] || unzip -q dl/SoftFloat-3e.zip
[ -d TestFloat-3e ] || unzip -q dl/TestFloat-3e.zip
make -s -C SoftFloat-3e/build/Linux-x86_64-GCC SPECIALIZE_TYPE=8086
make -s -C TestFloat-3e/build/Linux-x86_64-GCC
ls -l TestFloat-3e/build/Linux-x86_64-GCC/testfloat_gen TestFloat-3e/build/Linux-x86_64-GCC/testfloat_ver
