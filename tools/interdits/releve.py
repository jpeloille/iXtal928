#!/usr/bin/env python3
# SPDX-FileCopyrightText: 2026 Julien Peloille
# SPDX-License-Identifier: GPL-2.0-only
#
# ORACLE: (aucun) — outillage de la preuve (G15.0a ; PLAN-G15.md, décision n° 7 ; R1-interdits.md § 2, I2).
#
# LE RELEVÉ DES CONSTRUCTIONS INTERDITES dans les chemins chauds, sans paquet : le texte (un grep) et l'IL (le vidage
# de tools/ildump). Python 3 de la machine, bibliothèque standard seule. Trois commandes :
#
#   releve.py liste  ARBRE                         les fichiers de la frontière, un par ligne
#   releve.py texte  ARBRE [--noms TABLE]          le relevé du texte, en TSV, sur la sortie
#   releve.py il     ARBRE VIDAGE [--noms TABLE] [--compte]
#                                                  le relevé de l'IL d'un assemblage (ildump vider), en TSV
#
# LA FRONTIÈRE est chauds.txt, à côté de ce script (sa forme y est dite) : les fichiers suivis par git de l'arbre qui
# s'y accordent. Sous --noms TABLE (la table de G14, tools/noms/g14.txt), l'arbre est d'avant la table (le commit R) :
# ses chemins et ses noms de types sont traduits vers les nouveaux, entrée par entrée, avant le relevé ; la frontière
# est lue dans les nouveaux noms. Le relevé de R sous la table et celui d'un arbre d'après doivent alors être égaux.
#
# LE TEXTE. Chaque fichier de la frontière, ses commentaires ôtés (`//` jusqu'à la fin de la ligne, `/* … */`, hors
# des chaînes) ; une ligne par occurrence d'un motif : chemin, motif, numéro de ligne, la ligne (ses blancs réduits).
# Les motifs (MOTIFS ci-dessous) : linq, lambda (un `=>` hors d'un membre à corps d'expression et de sa suite ; les
# bras d'une expression `switch` y comptent, six dans la base), fetchdat (les
# lambdas des fabriques d'opcodes, `fetchdat =>`), delegue (`delegate (`), func (`Func<`, `Action`), new, segdat (`new
# uint16_t[4]`), stackalloc, poly (`interface`, `virtual`, `abstract`, `override`, hors d'un nom en `@` : le champ
# `@override` de la SVGA), params, chaine (une interpolation,
# string.Format, Concat, Join), boite (`(object)`), instance (une classe qui n'est pas statique). Un motif compte une
# fois par ligne.
#
# L'IL. Les méthodes des types de la frontière : un type de premier niveau déclaré dans un fichier de la frontière, et
# les types qu'il imbrique (les classes de fermeture du compilateur comprises). Un type partiel dont une partie est
# hors de la frontière (_386 et Cpu/386.State.cs, _808x et Cpu/808x.State.cs) y est en entier : le relevé est au
# grain du type, plus large que la frontière, jamais plus étroit. Une ligne par instruction relevée : la méthode,
# l'instruction, son opérande, son rang parmi les instructions égales de la méthode. Les instructions : newobj, newarr,
# box, callvirt, ldftn, ldvirtftn, et les call vers System.Linq, vers String.Concat, Format et Join, et vers
# DefaultInterpolatedStringHandler. Sous --compte, une ligne par méthode et par instruction : leur nombre (la forme
# de la base écrite dans le dépôt, tools/interdits/base-R.il.tsv ; le détail se régénère de R).
#
# Ce que le relevé ne distingue pas, et qui ne change rien à la comparaison à la base (le même outil des deux côtés) :
# un motif dans une chaîne (les chaînes sont gardées ; aucune dans la base) ; `callvirt` et `newobj` comptent aussi les
# appels d'instance ordinaires et les structures ; la garde des noms courts ne voit ni une variable de `foreach` ni un
# paramètre de lambda.
#
# Retour : 0 ; 2 usage, lecture ou table refusée.

import re
import subprocess
import sys
from fnmatch import fnmatchcase
from pathlib import Path

MOTIFS = [
    ("linq", re.compile(r"\bSystem\.Linq\b|\bEnumerable\.|\.(Select|Where|SelectMany|Aggregate|Any|All|Sum|Average|"
                        r"First|FirstOrDefault|Last|LastOrDefault|Single|ToArray|ToList|ToDictionary|ToHashSet|"
                        r"OrderBy|OrderByDescending|ThenBy|GroupBy|Distinct|Skip|Take|Zip|Cast|OfType|"
                        r"SequenceEqual)\(")),
    ("lambda", None),
    ("fetchdat", re.compile(r"\bfetchdat\s*=>")),
    ("delegue", re.compile(r"\bdelegate\s*[({]")),
    ("func", re.compile(r"\bFunc\s*<|\bAction\b")),
    ("new", re.compile(r"\bnew\b")),
    ("segdat", re.compile(r"\bnew\s+uint16_t\s*\[\s*4\s*\]")),
    ("stackalloc", re.compile(r"\bstackalloc\b")),
    ("poly", re.compile(r"(?<![@\w])(interface|virtual|abstract|override)\b")),
    ("params", re.compile(r"\bparams\b")),
    ("chaine", re.compile(r"\$@?\"|@\$\"|\b[Ss]tring\.(Format|Concat|Join)\b")),
    ("boite", re.compile(r"\(\s*object\s*\)")),
    ("instance", re.compile(r"^\s*((public|internal|private|protected|sealed|abstract|partial|unsafe|file)\s+)*"
                            r"class\s")),
]
MODIFICATEURS = r"(public|internal|private|protected|static|readonly|override|virtual|abstract|sealed|unsafe|" \
                r"extern|new|partial|async|ref)"
# Un membre à corps d'expression : une méthode, un opérateur, une fonction locale ou une propriété, dont la ligne
# porte la signature avant le `=>`.
MEMBRE = re.compile(r"^\s*(\[[^\]]*\]\s*)*(?!(return|yield|await|throw|case|else)\b)(" + MODIFICATEURS +
                    r"\s+)*[\w<>\[\],.?()]+\s+[\w.]+(<[^<>]*>)?\s*(\([^=]*\))?\s*=>")
STATIQUE = re.compile(r"\bstatic\b")
ILS = {"newobj", "newarr", "box", "callvirt", "ldftn", "ldvirtftn"}
APPELS = re.compile(r"\[System\.Linq\]|System\.String::(Concat|Format|Join)\b|DefaultInterpolatedStringHandler")
INSTR = re.compile(r"^\s+IL_[0-9A-F]+: (\S+)(?: (.*))?$")


def erreur(msg):
    print(f"releve : {msg}", file=sys.stderr)
    sys.exit(2)


def lire_table(table):
    """Les entrées de la table de G14 (sa grammaire, tools/noms/g14.txt) : types et chemins, ancien → nouveau."""
    types, chemins = {}, {}
    for ligne in Path(table).read_text().splitlines():
        if not ligne.startswith("//   iXtal26"):
            continue
        mots = ligne[5:].split()
        if len(mots) < 2:
            erreur(f"entrée illisible : {ligne}")
        ancien, nouveau = mots[0], mots[1]
        cible = chemins if ancien.endswith(".cs") else types
        if ancien in cible:
            erreur(f"ancien en double : {ancien}")
        cible[ancien] = nouveau
    return types, chemins


def renommer_types(texte, types):
    """Chaque nom qualifié ancien vers le nouveau, à une frontière de nom (pas iXtal26.Models.PIT dans PIT_nr)."""
    for ancien in sorted(types, key=len, reverse=True):
        texte = re.sub(re.escape(ancien) + r"(?![A-Za-z0-9_])", types[ancien], texte)
    return texte


def frontiere(arbre):
    motifs = []
    for ligne in (Path(__file__).resolve().parent / "chauds.txt").read_text().splitlines():
        ligne = ligne.strip()
        if ligne and not ligne.startswith("#"):
            motifs.append(ligne)
    return motifs


def suivis(arbre):
    r = subprocess.run(["git", "-C", str(arbre), "ls-files", "--", "*.cs"], capture_output=True, text=True)
    if r.returncode != 0:
        erreur(f"git ls-files dans {arbre} : {r.stderr.strip()}")
    return r.stdout.split()


def liste(arbre, chemins):
    """(chemin dans l'arbre, chemin nouveau) des fichiers suivis qui sont dans la frontière."""
    motifs = frontiere(arbre)
    out = []
    for f in suivis(arbre):
        nouveau = chemins.get(f, f)
        dedans = False
        for m in motifs:
            if m.startswith("!"):
                if fnmatchcase(nouveau, m[1:]):
                    dedans = False
            elif fnmatchcase(nouveau, m):
                dedans = True
        if dedans:
            out.append((f, nouveau))
    return sorted(out, key=lambda x: x[1])


def sans_commentaires(texte):
    """Les commentaires ôtés, les chaînes et les caractères gardés ; les sauts de ligne gardés (les numéros
    tiennent)."""
    out, i, n = [], 0, len(texte)
    while i < n:
        c = texte[i]
        if texte.startswith("//", i):
            j = texte.find("\n", i)
            i = n if j < 0 else j
            continue
        if texte.startswith("/*", i):
            j = texte.find("*/", i + 2)
            j = n if j < 0 else j + 2
            out.append("\n" * texte.count("\n", i, j))
            i = j
            continue
        if c == '"' or (c in "$@" and i + 1 < n and texte[i + 1] in '"$@'):
            j = i
            verbatim = False
            while j < n and texte[j] in "$@":
                verbatim |= texte[j] == "@"
                j += 1
            j += 1
            while j < n:
                if texte[j] == "\\" and not verbatim:
                    j += 2
                    continue
                if texte[j] == '"':
                    if verbatim and j + 1 < n and texte[j + 1] == '"':
                        j += 2
                        continue
                    j += 1
                    break
                j += 1
            out.append(texte[i:j])
            i = j
            continue
        if c == "'":
            j = i + 1
            while j < n and texte[j] != "'":
                j += 2 if texte[j] == "\\" else 1
            out.append(texte[i:j + 1])
            i = j + 1
            continue
        out.append(c)
        i += 1
    return "".join(out)


def texte(arbre, types, chemins):
    lignes = []
    for f, nouveau in liste(arbre, chemins):
        source = sans_commentaires((Path(arbre) / f).read_text())
        if types:
            source = renommer_types(source, types)
            # Les noms courts des conteneurs renommés, tels que le code les écrit (Intel8253.pit_ au lieu de pit.pit_),
            # sauf dans un fichier où le même nom est aussi un paramètre ou une locale (`PIT pit`, Ics/Intel8253.cs).
            for ancien, nouv in types.items():
                a, b = ancien.rsplit(".", 1)[1], nouv.rsplit(".", 1)[1]
                if a != b and not re.search(r"\b[\w<>\[\]?]+\s+" + re.escape(a) + r"\s*[,);=]", source):
                    source = re.sub(r"(?<![A-Za-z0-9_.])" + re.escape(a) + r"(?=\.)", b, source)
        for k, ligne in enumerate(source.split("\n"), 1):
            norm = " ".join(ligne.split())
            if not norm:
                continue
            for nom, motif in MOTIFS:
                if nom == "lambda":
                    # Une ligne qui commence par `=>` continue un membre à corps d'expression ouvert plus haut.
                    vu = "=>" in ligne and not MEMBRE.match(ligne) and not ligne.lstrip().startswith("=>")
                elif nom == "instance":
                    vu = bool(motif.match(ligne)) and not STATIQUE.search(ligne)
                else:
                    vu = bool(motif.search(ligne))
                if vu:
                    lignes.append(f"{nouveau}\t{nom}\t{k}\t{norm}")
    return lignes


def types_chauds(arbre, types, chemins):
    """Les types de premier niveau déclarés dans les fichiers de la frontière, qualifiés, dans les nouveaux noms."""
    decl = re.compile(r"^(?:\w+\s+)*(?:class|struct|interface|enum|record|delegate)\s+(?:[\w<>\[\],.?]+\s+)?(\w+)")
    out = set()
    for f, _ in liste(arbre, chemins):
        source = sans_commentaires((Path(arbre) / f).read_text())
        m = re.search(r"^namespace\s+([\w.]+)\s*;", source, re.M)
        if not m:
            # Un fichier sans espace de noms de fichier ne doit déclarer aucun type (GlobalUsings.cs).
            if any(decl.match(x) for x in source.split("\n")):
                erreur(f"{f} : des types, mais pas d'espace de noms de fichier")
            continue
        for ligne in source.split("\n"):
            d = decl.match(ligne)
            if d and not ligne.startswith((" ", "\t")):
                out.add(renommer_types(f"{m.group(1)}.{d.group(1)}", types))
    return out


def il(arbre, vidage, types, chemins, compte=False):
    chauds = types_chauds(arbre, types, chemins)
    lignes, methode, rangs = [], None, {}
    for ligne in Path(vidage).read_text().splitlines():
        if ligne.startswith("== "):
            methode = None
            if ligne.startswith("== méthode "):
                cle = renommer_types(ligne[len("== méthode "):], types)
                if cle.split("::", 1)[0].split("+", 1)[0] in chauds:
                    methode, rangs = cle, {}
            continue
        if methode is None:
            continue
        m = INSTR.match(ligne)
        if not m:
            continue
        op, operande = m.group(1), renommer_types(m.group(2) or "", types)
        if op in ILS or (op == "call" and APPELS.search(operande)):
            k = rangs[(op, operande)] = rangs.get((op, operande), 0) + 1
            lignes.append(f"{methode}\t{op}\t{operande}\t{k}")
    if compte:
        n = {}
        for x in lignes:
            cle = "\t".join(x.split("\t")[:2])
            n[cle] = n.get(cle, 0) + 1
        return sorted(f"{k}\t{v}" for k, v in n.items())
    return sorted(lignes)


def main():
    args = sys.argv[1:]
    types, chemins = {}, {}
    compte = "--compte" in args
    if compte:
        args.remove("--compte")
    if "--noms" in args:
        k = args.index("--noms")
        if k + 1 >= len(args):
            erreur("--noms sans table")
        types, chemins = lire_table(args[k + 1])
        del args[k:k + 2]
    if not args or args[0] not in ("liste", "texte", "il"):
        erreur("usage : releve.py liste|texte ARBRE [--noms TABLE] ; releve.py il ARBRE VIDAGE [--noms TABLE]")
    if args[0] == "il":
        if len(args) != 3:
            erreur("usage : releve.py il ARBRE VIDAGE [--noms TABLE]")
        print("\n".join(il(args[1], args[2], types, chemins, compte)))
        return 0
    if len(args) != 2:
        erreur("usage : releve.py liste|texte ARBRE [--noms TABLE]")
    if args[0] == "liste":
        print("\n".join(n for _, n in liste(args[1], chemins)))
    else:
        print("\n".join(texte(args[1], types, chemins)))
    return 0


if __name__ == "__main__":
    sys.exit(main())
