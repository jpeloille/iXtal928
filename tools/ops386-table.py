#!/usr/bin/env python3
# SPDX-FileCopyrightText: 2026 Julien Peloille
# SPDX-License-Identifier: GPL-2.0-only
"""G2, D0.2 — la table ops_386 de PCem, rapportée à ce que ops_286 a déjà posé.

Lit les tables OP_TABLE(286), OP_TABLE(286_0f), OP_TABLE(386) et OP_TABLE(386_0f) de
pcem-dev/includes/private/cpu/386_ops.h, par leur NOM et non par numéro de ligne, et
classe chaque emplacement de ops_386 / ops_386_0f :

  - PARTAGÉ : son handler porte le même nom qu'un handler de ops_286 (resp. ops_286_0f),
    donc la même fonction C — le C# le prend dans ops_286 ;
  - NEUF : aucun handler du 286 ne porte ce nom — il reste opNonTranscrit, qui échoue
    en se nommant.

  ops386-table.py --emit FICHIER.cs   écrit le fichier C# des emplacements partagés ;
  ops386-table.py                     n'écrit rien et imprime le décompte.

Le fichier émis ne se retouche pas à la main : on relance le script. Le décompte de ce
que le C# a RÉELLEMENT posé est `iXtal26.Diff ops-count`, qui lit la table vivante.
"""
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
SRC = ROOT / "pcem-dev/includes/private/cpu/386_ops.h"


def table(text, name):
    m = re.search(r"OpFn OP_TABLE\(" + re.escape(name) + r"\)\[1024\] = \{(.*?)\n\};", text, re.S)
    if not m:
        sys.exit(f"OP_TABLE({name}) introuvable dans {SRC}")
    body = re.sub(r"/\*.*?\*/", "", m.group(1), flags=re.S)
    body = re.sub(r"//[^\n]*", "", body)
    names = [t.strip() for t in body.split(",") if t.strip()]
    if len(names) != 1024:
        sys.exit(f"OP_TABLE({name}) : {len(names)} entrées, 1024 attendues")
    return names


def share(new, old):
    """Pour chaque emplacement de `new`, l'indice dans `old` d'un handler du même nom,
    ou None. Le même indice d'abord, puis le premier du quadrant 0 : c'est lui que le
    C# pose toujours, les quadrants hauts étant des recopies."""
    first = {}
    for k in range(256):
        first.setdefault(old[k], k)
    out = []
    for j, n in enumerate(new):
        if old[j] == n and j < 256:
            out.append(j)
        else:
            out.append(first.get(n))
    return out


def main():
    text = SRC.read_text()
    t286, t286f = table(text, "286"), table(text, "286_0f")
    t386, t386f = table(text, "386"), table(text, "386_0f")
    s386, s386f = share(t386, t286), share(t386f, t286f)

    for label, names, sh in (("ops_386", t386, s386), ("ops_386_0f", t386f, s386f)):
        new = [j for j, k in enumerate(sh) if k is None]
        quads = [sum(1 for j in new if j >> 8 == q) for q in range(4)]
        print(f"{label} : {1024 - len(new)} partagés, {len(new)} neufs "
              f"({len({names[j] for j in new})} handlers distincts) — par quadrant {quads}")

    if len(sys.argv) == 3 and sys.argv[1] == "--emit":
        lines = [
            "// SPDX-FileCopyrightText: 2026 Julien Peloille",
            "// SPDX-License-Identifier: GPL-2.0-only",
            "//",
            "// ORACLE: pcem-dev/includes/private/cpu/386_ops.h  (OP_TABLE(386), OP_TABLE(386_0f))",
            "// SHA256: voir oracle.tsv ; vérifier avec tools/check-oracle.sh",
            "// STATUS: partial — GÉNÉRÉ par tools/ops386-table.py --emit ; ne pas retoucher à",
            "//         la main. Les emplacements dont le handler porte le MÊME NOM qu'un",
            "//         handler de ops_286 : même fonction C, donc même délégué C#. Les",
            "//         autres restent opNonTranscrit et échouent en se nommant (G2, D1-D7).",
            "",
            "namespace iXtal26.Cpu;",
            "",
            "internal static partial class _386",
            "{",
            "    // pcem: 386_ops.h — OP_TABLE(386) et OP_TABLE(386_0f), part partagée avec le 286.",
            "    private static void PoserTable386Partagee()",
            "    {",
        ]
        for j, k in enumerate(s386):
            if k is not None:
                lines.append(f"        ops_386[0x{j:03X}] = ops_286[0x{k:03X}]; // {t386[j]}")
        lines.append("")
        for j, k in enumerate(s386f):
            if k is not None:
                lines.append(f"        ops_386_0f[0x{j:03X}] = ops_286_0f[0x{k:03X}]; // {t386f[j]}")
        lines += ["    }", "}", ""]
        Path(sys.argv[2]).write_text("\n".join(lines))
        print(f"écrit : {sys.argv[2]}")


if __name__ == "__main__":
    main()
