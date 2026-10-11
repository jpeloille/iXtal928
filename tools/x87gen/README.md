# Les générateurs du x87 (G4)

Les fichiers de handlers x87 d'`iXtal26/Cpu/` ne sont pas écrits à la main : ils sont
générés depuis le C vendoré par ces scripts, puis relus. Les rejouer doit rendre les fichiers
commités **à l'octet près** — c'est la vérification à faire après toute retouche d'un
générateur (`git status` vide).

| script | lit | écrit | étape |
|---|---|---|---|
| `gen43.py` | `x87_ops_arith.h` (macro `opFPU` expansée) | `x87_ops_arith.cs` | G4.3 |
| `gen44.py` | `x87_ops_misc.h` | `x87_ops_misc.cs` | G4.4, G4.5 |
| `gentab.py` | `x87_ops.h`, tables `OP_TABLE(fpu_*)` | `x87_ops_tables.cs` | G4.3-G4.5 |
| `gen46.py` | les trois `.cs` ci-dessus, et `x87_ops.h` | `x87_ops_808x.cs`, `x87_ops_808x_tables.cs` | G4.6 |
| `genmat.py` | sa propre description (le noyau de 80 bits du mode matériel) | `x87_noyau_386.Materiel.cs`, `x87_noyau_808x.Materiel.cs` | G13.6, le noyau |

Ordre : `gen43`, `gen44`, `gentab`, puis `gen46`, qui recopie les trois premiers dans `_808x`. `genmat` est
indépendant : il ne lit pas le C, il instancie sa description deux fois, pour `_386_noyau` (le 286, le 386 et le 486,
a16 et a32) et pour `_808x_noyau` (le 8087), chaque aide de contexte qualifiée par la classe de son cœur.
`x87_ops_loadstore.cs` (G4.2) a été transcrit à la main, par règles, et n'a pas de générateur.

    for g in gen43 gen44 gentab gen46 genmat; do python3 tools/x87gen/$g.py; done; git status --short
