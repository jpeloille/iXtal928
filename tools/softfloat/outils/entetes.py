#!/usr/bin/env python3
# SPDX-FileCopyrightText: 2026 Julien Peloille
# SPDX-License-Identifier: GPL-2.0-only
#
# Pose l'en-tête des fichiers réécrits de SoftFloat : chaque fichier .cs qui commence par `//@ENTETE <chemin C>`
# reçoit les lignes SPDX (les années du fichier C, puis Julien), la ligne ORACLE, et la notice d'origine du fichier C,
# mot pour mot, comme la licence BSD le demande (« Redistributions of source code must retain the above copyright
# notice, this list of conditions, and the following disclaimer »).
#
# Usage : entetes.py <racine SoftFloat-3e> <répertoire des .cs>
import pathlib, re, sys

racine, cible = pathlib.Path(sys.argv[1]), pathlib.Path(sys.argv[2])
ORACLE_ZIP = "SoftFloat-3e.zip, sha256 21130ce885d35c1fe73fc1e1bf2244178167e05c6747cad5f450cc991714c746"
n = 0
for cs in sorted(cible.rglob("*.cs")):
    lignes = cs.read_text(encoding="utf-8").split("\n")
    m = re.match(r"//@ENTETE (\S+)$", lignes[0])
    if not m:
        continue
    c = racine / m.group(1)
    texte = c.read_text(encoding="latin-1").replace("\r", "").split("\n")
    debut = next(i for i, l in enumerate(texte) if re.match(r"^/\*=+$", l))
    fin = next(i for i, l in enumerate(texte) if re.match(r"^=+\*/$", l))
    notice = texte[debut + 1:fin]
    while notice and not notice[0].strip():
        notice.pop(0)
    while notice and not notice[-1].strip():
        notice.pop()
    plat = " ".join(l.strip() for l in notice)
    annees = re.search(r"Copyright ([0-9, ]+?) The Regents of the University of California", plat).group(1)
    tete = [
        f"// SPDX-FileCopyrightText: {annees} The Regents of the University of California",
        "// SPDX-FileCopyrightText: 2026 Julien Peloille",
        "// SPDX-License-Identifier: BSD-3-Clause",
        "//",
        f"// ORACLE: sources/softfloat/SoftFloat-3e/{m.group(1)}",
        f"//         ({ORACLE_ZIP} ; hors dépôt)",
        "//",
        "// La notice d'origine, gardée comme la licence le demande :",
        "//",
    ] + [("// " + l).rstrip() for l in notice]
    cs.write_text("\n".join(tete + lignes[1:]), encoding="utf-8")
    n += 1
print(f"{n} en-têtes posés")
