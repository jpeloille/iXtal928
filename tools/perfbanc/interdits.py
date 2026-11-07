#!/usr/bin/env python3
# SPDX-FileCopyrightText: 2026 Julien Peloille
# SPDX-License-Identifier: GPL-2.0-only
#
# ORACLE: (aucun) — outillage de mesure.
#
# LE VERDICT DU BANC DES CONSTRUCTIONS INTERDITES (G15.0a ; PLAN-G15.md, décision n° 6 ; interdits.sh).
#
#   python3 tools/perfbanc/interdits.py SORTIE
#
# Python 3 de la machine, bibliothèque standard seule. Lit SORTIE/passageN/<cas>/results/*-report-full.json et
# SORTIE/passageN/<cas>/*.log, écrit SORTIE/rapport.txt et SORTIE/rapport.tsv.
#
# L'UNITÉ est la moyenne de chaque lancement (les mesures Workload/Result, par LaunchIndex, comme compare_ref.py :
# l'intervalle de BenchmarkDotNet suppose des itérations indépendantes, et les colonnes sont des processus
# distincts) : 3 lancements par passage, 3 × N par colonne. L'ÉCART d'une colonne X à la retenue est l'estimateur
# de Hodges-Lehmann de ln X − ln Retenue (la médiane des différences de toutes les paires), rendu en X/Retenue − 1 ;
# son étendue, la plus petite et la plus grande de ces différences.
#
# LE SEUIL de la décision n° 6 : le plus grand de l'écart de l'A/A (|AA/Retenue − 1|, le placement mesuré sur le cas
# même) et de la résolution de § G14.0b. Celle-ci n'est mesurée que sur les cas de la référence : on prend la plus
# grossière qu'elle a mesurée sur un cœur, +5,01 % (Exec486, « aucun », sans-prefixe), qui est aussi l'ordre du
# placement relevé en G14 (2 à 3 %, G14-reconnaissance/R0-contre.md) et la borne sous laquelle une copie ne conclut
# pas (R1-contre.md § 4.2). Et, quand le vrai code et sa copie ont le même code machine, l'écart de la colonne Reel à
# celle qu'elle incarne : c'est alors le placement du vrai, dans un autre assemblage, que l'A/A, deux copies du même
# assemblage, ne voit pas (mesuré à l'étalonnage : readmemb et sa copie, égaux à l'octet, à +12 %).
#
# LE CODE MACHINE (SORTIE/jit/<cas>.log, interdits.sh) : la méthode du vrai code et celle de sa copie, comparées
# ligne à ligne, les commentaires du listing ôtés ; « égal », « égal aux décalages près » (seuls les décalages des
# opérandes en mémoire, [reg+0xNN], diffèrent : les champs d'une classe de fermeture, que le vrai a plus nombreux,
# aux cas 4 et 6), « égal au registre de l'argument près » (le vrai est une méthode d'instance, la lambda d'une classe
# cache qui ne capture rien, au cas 3 depuis G15.P, et sa copie une méthode statique : le premier argument arrive dans
# rsi au lieu de rdi, et seules les lignes qui le lisent diffèrent, rsi ou esi d'un côté, rdi ou edi de l'autre), ou
# le nombre de lignes qui diffèrent.
#
# LE VERDICT, colonne interdite par colonne interdite :
# - « ça coûte » : l'écart dépasse le seuil, et l'étendue des différences est tout entière du même côté (chaque
#   lancement de la colonne interdite est plus lent que chaque lancement de la retenue) ;
# - « pas de coût mesurable sur ce chemin » sinon ; la règle de PLAN.md reste (décision n° 6 : le banc l'éclaire sans
#   la défaire). Un écart sous zéro au-delà du seuil (la forme interdite plus rapide) est dit tel quel.
# LA VALIDITÉ du cas : la colonne Reel doit tenir dans le seuil de la colonne qu'elle incarne (Retenue, sauf aux cas
# 4 et 6 : Interdite ; au cas 3, Delegue jusqu'à G15.P ; l'en-tête d'InterditsBanc.cs), ou avoir le même code
# machine qu'elle.
# Sinon, le cas est « sans valeur, à reprendre » : la copie ne mesure pas le vrai code. Les octets alloués par
# opération (MemoryDiagnoser) sont dits à côté de chaque colonne ; ils ne changent pas le verdict, qui est celui du
# temps.
#
# Retour : 0 le rapport écrit, même avec des cas sans valeur (ils y sont dits) ; 2 une lecture impossible (un cas ou
# une colonne sans ses moyennes, une ligne « // Interdit » absente).

import json
import math
import re
import sys
from pathlib import Path

RESOLUTION = 0.0501

COLONNES = {
    "Interdit1Instance": ["Reel", "Retenue", "AA", "Interdite", "InterditeInlinee"],
    "Interdit2Linq": ["Reel", "Retenue", "AA", "Interdite"],
    "Interdit3Interface": ["Reel", "Retenue", "AA", "Delegue", "Interface", "Virtuel"],
    "Interdit4Fermeture": ["Reel", "Retenue", "AA", "Interdite"],
    "Interdit5Delegue": ["Reel", "Retenue", "AA", "Interdite"],
    "Interdit6Allocation": ["Reel", "Retenue", "AA", "Interdite"],
    "Interdit7Boite": ["Reel", "Retenue", "AA", "Interdite"],
    "Interdit8Chaine": ["Reel", "Retenue", "AA", "Champ", "Interdite"],
    "Interdit9Params": ["Reel", "Retenue", "AA", "Interdite"],
}
INTERDITES = {"Interdit1Instance": ["Interdite", "InterditeInlinee"],
              "Interdit3Interface": ["Delegue", "Interface", "Virtuel"],
              "Interdit8Chaine": ["Champ", "Interdite"]}
INCARNE = {"Interdit4Fermeture": "Interdite", "Interdit6Allocation": "Interdite"}
# Les paires (le vrai, sa copie) dont le code machine se compare : chaque méthode par des fragments de l'en-tête de
# son listing, qui doivent tous s'y trouver.
PAIRES = {
    "Interdit1Instance": [(["iXtal26.Cpu._386_common:readmemb("],
                           ["Interdit1:ReadmembStatique[iXtal26.PerfBanc.CopieA]"])],
    "Interdit2Linq": [(["iXtal26.Sound.sound_speaker:speaker_get_buffer("],
                       ["Interdit2:GetBuffer[iXtal26.PerfBanc.CopieA]"])],
    "Interdit3Interface": [(["iXtal26.Cpu._386+", f"[iXtal26.Cpu._386+alu_{f.upper()}]:<OP_ARITH>b__"],
                            [f"Interdit3:opARITH_b_rmw_a16[iXtal26.PerfBanc.Alu{f},iXtal26.PerfBanc.CopieA]"])
                           for f in ("Add", "Or", "Sub")],
    "Interdit4Fermeture": [(["iXtal26.Cpu._386+", ":<PoserSautsConditionnels>b__0("],
                            ["Interdit4+", ":<Fabrique>b__0("])],
    "Interdit5Delegue": [(["iXtal26.Video.vid_svga:svga_recalctimings("],
                          ["Interdit5:Recalctimings[iXtal26.PerfBanc.RenduDirectA]"])],
    "Interdit6Allocation": [(["iXtal26.Cpu.x86seg_c:loadseg("], ["Interdit6:LoadsegTas("]),
                            (["iXtal26.Cpu.x86seg_c:do_seg_load("], ["Interdit6:DoSegLoadTas("])],
    "Interdit7Boite": [(["iXtal26.io:outb("], ["Interdit7:Outb[iXtal26.PerfBanc.CopieA]"]),
                       (["iXtal26.io:outb_corps("], ["Interdit7:OutbCorps[iXtal26.PerfBanc.CopieA]"])],
    "Interdit8Chaine": [(["iXtal26.Video.vid_svga:svga_write("], ["Interdit8:Write[iXtal26.PerfBanc.JournalPlieA]"])],
    "Interdit9Params": [(["iXtal26.Cpu._386:prefetch_run("], ["Interdit9:Corps[iXtal26.PerfBanc.CopieA]"])],
}
DECALAGE = re.compile(r"([+-])0x[0-9A-F]+\]")
# Le premier argument entier : rsi dans une méthode d'instance (rdi y porte `this`), rdi dans une méthode statique.
ARGUMENT = re.compile(r"\b([er])si\b")


def listings(f):
    """Les listings d'un journal : l'en-tête, et les lignes de code (les commentaires ôtés)."""
    out, cle = {}, None
    for ligne in Path(f).read_text(errors="replace").splitlines():
        if ligne.startswith("; Assembly listing for method "):
            cle = ligne[len("; Assembly listing for method "):]
            out.setdefault(cle, [])
            continue
        if cle is None:
            continue
        if ligne.startswith("; Total bytes of code"):
            cle = None
            continue
        s = ligne.strip()
        if s and not s.startswith(";"):
            out[cle].append(s)
    return out


def comparer_code(sortie, nom):
    """Le verdict du code machine de chaque paire : (égal, texte)."""
    f = sortie / "jit" / f"{nom}.log"
    if not f.exists():
        return False, "pas de listing (jit/ absent)"
    l = listings(f)
    textes, egal = [], True
    for vrai, copie in PAIRES.get(nom, []):
        cles = []
        for frag in (vrai, copie):
            c = [k for k in l if all(x in k for x in frag)]
            cles.append(c[0] if len(c) == 1 else None)
        if None in cles:
            textes.append(f"{vrai[-1]} : listing introuvable")
            egal = False
            continue
        a, b = (l[k] for k in cles)
        na, nb = ([DECALAGE.sub(r"\1OFF]", x) for x in c] for c in (a, b))
        instance = ":this (" in cles[0] and ":this (" not in cles[1]
        if a == b:
            textes.append(f"{vrai[-1]} égal ({len(a)} lignes)")
        elif na == nb:
            n = sum(x != y for x, y in zip(a, b))
            textes.append(f"{vrai[-1]} égal aux décalages près ({len(a)} lignes, {n} décalages)")
        elif instance and len(a) == len(b) and all(x == y or ARGUMENT.sub(r"\1di", x) == y for x, y in zip(na, nb)):
            n = sum(x != y for x, y in zip(na, nb))
            textes.append(f"{vrai[-1]} égal au registre de l'argument près ({len(a)} lignes, {n} où le vrai, "
                          f"méthode d'instance, lit rsi et sa copie statique rdi)")
        else:
            n = sum(x != y for x, y in zip(a, b)) + abs(len(a) - len(b))
            textes.append(f"{vrai[-1]} : {n} lignes diffèrent ({len(a)} contre {len(b)})")
            egal = False
    return egal, " ; ".join(textes)


def erreur(msg):
    print(f"interdits.py : {msg}", file=sys.stderr)
    sys.exit(2)


def lire(sortie):
    """Par cas : les moyennes de lancement et les octets alloués de chaque colonne, les lignes « // Interdit »."""
    cas = {}
    for d in sorted(sortie.glob("passage*/Interdit*")):
        nom = d.name
        c = cas.setdefault(nom, {"moyennes": {}, "alloc": {}, "etats": set()})
        for j in sorted((d / "results").glob("*-report-full.json")):
            for b in json.loads(j.read_text()).get("Benchmarks", []):
                lancements = {}
                for m in b.get("Measurements") or []:
                    if m.get("IterationMode") == "Workload" and m.get("IterationStage") == "Result":
                        lancements.setdefault(m["LaunchIndex"], []).append(m["Nanoseconds"] / m["Operations"])
                col = b["Method"]
                c["moyennes"].setdefault(col, []).extend(sum(v) / len(v) for _, v in sorted(lancements.items()))
                c["alloc"].setdefault(col, set()).add((b.get("Memory") or {}).get("BytesAllocatedPerOperation"))
        for f in d.glob("*.log"):
            for ligne in f.read_text(errors="replace").splitlines():
                if ligne.startswith("// Interdit ("):
                    c["etats"].add(ligne)
    return cas


def ecart(x, r):
    """Hodges-Lehmann de ln x − ln r, et l'étendue des différences, rendus en x/r − 1."""
    d = sorted(math.log(b) - math.log(a) for a in r for b in x)
    n = len(d)
    return math.expm1((d[(n - 1) // 2] + d[n // 2]) / 2), math.expm1(d[0]), math.expm1(d[-1])


def pct(v):
    return f"{v * 100:+.2f} %".replace(".", ",")


def main():
    if len(sys.argv) != 2:
        erreur("usage : interdits.py SORTIE")
    sortie = Path(sys.argv[1])
    cas = lire(sortie)
    if not cas:
        erreur(f"aucun cas sous {sortie}")
    lignes, tsv = [], ["cas\tcolonne\tmoyenne_ns\tecart\tmin\tmax\toctets\tverdict"]
    lignes.append("Le banc des constructions interdites (G15.0a) : le verdict de la décision n° 6.")
    lignes.append(f"Seuil : le plus grand de |AA/Retenue − 1| et de la résolution de § G14.0b ({pct(RESOLUTION)}).")
    lignes.append("")
    for nom, c in sorted(cas.items()):
        cols = COLONNES.get(nom)
        if cols is None:
            erreur(f"{nom} : cas inconnu")
        for col in cols:
            if not c["moyennes"].get(col):
                erreur(f"{nom} : la colonne {col} n'a pas de moyennes")
        if not c["etats"]:
            erreur(f"{nom} : aucune ligne « // Interdit » (l'équivalence n'a pas été imprimée)")
        base = c["moyennes"]["Retenue"]
        aa, _, _ = ecart(c["moyennes"]["AA"], base)
        incarne = INCARNE.get(nom, "Retenue")
        reel, rmin, rmax = ecart(c["moyennes"]["Reel"], c["moyennes"][incarne])
        code_egal, code = comparer_code(sortie, nom)
        seuil = max(abs(aa), RESOLUTION, abs(reel) if code_egal else 0.0)
        valide = code_egal or abs(reel) <= seuil
        lignes.append(f"== {nom} ({len(base)} lancements par colonne)")
        for e in sorted(c["etats"]):
            lignes.append(f"   {e}")
        lignes.append(f"   code machine du vrai et de sa copie : {code}")
        lignes.append(f"   A/A : {pct(aa)} ; Reel contre {incarne} : {pct(reel)} [{pct(rmin)}, {pct(rmax)}]"
                      f"{' (placement : le code est égal)' if code_egal else ''} ; seuil {pct(seuil)} — "
                      f"{'valide' if valide else 'SANS VALEUR, À REPRENDRE'}")
        for col in cols:
            m = c["moyennes"][col]
            moy = sum(m) / len(m)
            octets = ",".join(str(a) for a in sorted(c["alloc"][col], key=lambda a: (a is None, a)))
            if col == "Retenue":
                verdict, e, lo, hi = "base", 0.0, 0.0, 0.0
            else:
                e, lo, hi = ecart(m, base)
                if col in INTERDITES.get(nom, ["Interdite"]):
                    if e > seuil and lo > 0:
                        verdict = "ça coûte"
                    elif e < -seuil and hi < 0:
                        verdict = "plus rapide que la retenue"
                    else:
                        verdict = "pas de coût mesurable sur ce chemin"
                else:
                    verdict = "témoin"
            lignes.append(f"   {col:<10} {moy:12.3f} ns  {pct(e):>10} [{pct(lo)}, {pct(hi)}]  {octets:>6} o  {verdict}")
            tsv.append(f"{nom}\t{col}\t{moy:.4f}\t{e:.5f}\t{lo:.5f}\t{hi:.5f}\t{octets}\t{verdict}")
        lignes.append("")
    (sortie / "rapport.txt").write_text("\n".join(lignes) + "\n")
    (sortie / "rapport.tsv").write_text("\n".join(tsv) + "\n")
    print("\n".join(lignes))
    return 0


if __name__ == "__main__":
    sys.exit(main())
