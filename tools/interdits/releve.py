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
#   releve.py comparer ARBRE BASE VIDAGE… [--noms TABLE… [--depuis E] [--jusqua E]]
#                                                  le relevé d'ARBRE (texte, et IL compté) contre la base
#
# LA FRONTIÈRE est chauds.txt, à côté de ce script (sa forme y est dite) : les fichiers suivis par git de l'arbre qui
# s'y accordent. Sous --noms TABLE (la table de G14, tools/noms/g14.txt), l'arbre est d'avant la table (le commit R) :
# ses chemins et ses noms de types sont traduits vers les nouveaux, entrée par entrée, avant le relevé ; la frontière
# est lue dans les nouveaux noms. Le relevé de R sous la table et celui d'un arbre d'après doivent alors être égaux.
# Une entrée de la table des membres (A::membre, A+B) y est refusée : la table de G15 se lit par comparer.
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
# COMPARER (G15.0b5b). La base est un préfixe : BASE.texte.tsv (le relevé du texte) et BASE.il.tsv (les comptes de l'IL,
# d'un vidage `ildump vider --generes` : les noms générés y portent leur méthode, pas un numéro qui change quand une
# méthode s'ajoute). Le relevé d'ARBRE est fait aux noms d'ARBRE ; ses VIDAGE… (iXtal26, SoftFloat), faits avec
# --generes, à concaténer comme la base. Sous --noms (la table des membres de G15, tools/noms/g15.txt, sa grammaire :
# tools/noms/table_noms.py), la BASE est aux noms d'avant les étapes : chaque ligne en est traduite vers les nouveaux
# par tools/noms/traduire.py (les identifiants du code par leur nom simple, la liste close ; la méthode d'un nom généré,
# séparée de son préfixe ; un accesseur, par sa propriété), avant la comparaison ; une étape qui déplace des fichiers
# est refusée (G15 les garde, Q42). Le texte se compare sans les numéros de ligne (le chemin, le motif, la ligne : un
# multiensemble ; un retrait de lignes plus haut ne fait pas d'écart), l'IL compte par compte. Retour : 0 égaux ; 1 un
# écart (chaque ligne de trop ou de moins dite) ; 2 usage, lecture, table refusée. La limite de la traduction par le nom
# simple : un nom renommé dans une méthode et gardé dans une autre (un paramètre homonyme) est traduit dans les deux ;
# l'écart le montre, la porte rougit (VERIFICATION.md § G15.0b5b).
#
# Ce que le relevé ne distingue pas, et qui ne change rien à la comparaison à la base (le même outil des deux côtés) :
# un motif dans une chaîne (les chaînes sont gardées ; aucune dans la base) ; `callvirt` et `newobj` comptent aussi les
# appels d'instance ordinaires et les structures ; la garde des noms courts ne voit ni une variable de `foreach` ni un
# paramètre de lambda.
#
# Retour : 0 (comparer : 0 égaux, 1 un écart) ; 2 usage, lecture ou table refusée.

import re
import subprocess
import sys
from collections import Counter
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
        if ligne.startswith('//   "'):
            erreur(f"un littéral de la liste close ({ligne[5:].split()[0]}) : liste, texte et il ne lisent que la "
                   "table de G14 ; comparer lit celle de G15")
        if not ligne.startswith("//   iXtal26"):
            continue
        mots = ligne[5:].split()
        if len(mots) < 2:
            erreur(f"entrée illisible : {ligne}")
        ancien, nouveau = mots[0], mots[1]
        if "::" in ancien or "+" in ancien or "(" in ancien:
            erreur(f"une entrée de la table des membres ({ancien}) : liste, texte et il ne lisent que la table de "
                   "G14 ; comparer lit celle de G15")
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


def comparer(arbre, base, vidages, noms, jusqua, depuis):
    sys.path.insert(0, str(Path(__file__).resolve().parent.parent / "noms"))
    import table_noms
    import traduire
    try:
        tr = traduire.Traducteur(noms, jusqua, depuis)
    except table_noms.TableRefusee as e:
        erreur(f"table refusée : {e}")
    except OSError as e:
        erreur(f"la table : {e}")
    # G15 garde les fichiers (Q42) : une entrée de fichier, que la base ne saurait pas suivre, est refusée.
    for e in tr.etapes:
        if e["fichiers"]:
            erreur(f"l'étape {e['nom']} déplace des fichiers : comparer ne suit pas les chemins")

    def nouveau(texte):
        return tr.traduire(texte)[0]

    # Un nom généré sous --generes porte sa méthode collée à son préfixe (<>c__DisplayClassOpBTx#0_0, b__Loop#0) :
    # séparée le temps de la traduction, pour que la méthode y soit un identifiant.
    genere = re.compile(r"(c__DisplayClass|b__|d__|<>9__|c__|g__)(?=[A-Za-z_][A-Za-z0-9_]*#\d)")

    # Un accesseur (get_X, set_X, add_X, remove_X) suit sa propriété ou son événement, comme dans ildump : le préfixe
    # est séparé, sauf si le nom entier est lui-même renommé (une méthode ordinaire, get_cycles).
    accesseur = re.compile(r"(?<=::)(get_|set_|add_|remove_)([A-Za-z_][A-Za-z0-9_]*)(?=\()")

    def separer(m):
        return m.group(0) if tr.traduire(m.group(0))[1] else m.group(1) + "\x00" + m.group(2)

    def nouvelle_cle(cle):
        cle = accesseur.sub(separer, genere.sub(lambda m: m.group(1) + "\x00", cle))
        return nouveau(cle).replace("\x00", "")

    try:
        bt = Path(f"{base}.texte.tsv").read_text().splitlines()
        bi = Path(f"{base}.il.tsv").read_text().splitlines()
    except OSError as e:
        erreur(f"la base : {e}")
    # Le texte : (chemin, motif, ligne), sans le numéro.
    t_base = Counter()
    for x in bt:
        if not x:
            continue
        c, m, _, l = x.split("\t", 3)
        t_base[(c, m, nouveau(l))] += 1
    t_arbre, ou = Counter(), {}
    for x in texte(arbre, {}, {}):
        c, m, k, l = x.split("\t", 3)
        t_arbre[(c, m, l)] += 1
        ou.setdefault((c, m, l), []).append(k)
    # L'IL : la clé (méthode, instruction) et son compte.
    i_base, i_arbre = Counter(), Counter()
    for x in bi:
        if x:
            cle, n = x.rsplit("\t", 1)
            i_base[nouvelle_cle(cle)] += int(n)
    for v in vidages:
        for x in il(arbre, v, {}, {}, compte=True):
            cle, n = x.rsplit("\t", 1)
            i_arbre[cle] += int(n)
    ecarts = []
    for k in sorted(set(t_base) | set(t_arbre)):
        d = t_arbre[k] - t_base[k]
        if d:
            lignes = ", ".join(ou.get(k, [])) or "-"
            ecarts.append(f"texte {'+' if d > 0 else ''}{d}\t{k[0]}:{lignes}\t{k[1]}\t{k[2]}")
    for k in sorted(set(i_base) | set(i_arbre)):
        if i_base[k] != i_arbre[k]:
            ecarts.append(f"il {i_base[k]} -> {i_arbre[k]}\t{k}")
    print("\n".join(ecarts) if ecarts else "", end="\n" if ecarts else "")
    print(f"  comparer : texte {sum(t_arbre.values())} lignes (base {sum(t_base.values())}), "
          f"IL {sum(i_arbre.values())} instructions (base {sum(i_base.values())}), {len(tr.etapes)} étapes ; "
          f"{len(ecarts)} écarts", file=sys.stderr)
    return 1 if ecarts else 0


def main():
    args = sys.argv[1:]
    if args and args[0] == "comparer":
        noms, jusqua, depuis, pos, i = [], None, None, [], 1
        while i < len(args):
            a = args[i]
            if a in ("--noms", "--jusqua", "--depuis"):
                if i + 1 >= len(args):
                    erreur(f"{a} demande une valeur")
                if a == "--noms":
                    noms.append(args[i + 1])
                elif a == "--jusqua":
                    jusqua = args[i + 1]
                else:
                    depuis = args[i + 1]
                i += 2
            elif a.startswith("--"):
                erreur(f"option inconnue : {a}")
            else:
                pos.append(a)
                i += 1
        if len(pos) < 3 or ((jusqua or depuis) and not noms):
            erreur("usage : releve.py comparer ARBRE BASE VIDAGE… [--noms TABLE… [--depuis E] [--jusqua E]]")
        try:
            return comparer(pos[0], pos[1], pos[2:], noms, jusqua, depuis)
        except (OSError, ValueError, UnicodeDecodeError) as e:
            erreur(f"lecture : {e}")
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
