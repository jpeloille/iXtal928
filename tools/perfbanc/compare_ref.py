#!/usr/bin/env python3
# SPDX-FileCopyrightText: 2026 Julien Peloille
# SPDX-License-Identifier: GPL-2.0-only
#
# ORACLE: (aucun) — outillage de mesure.
#
# LE COMPARATEUR DE compare-ref.sh (G14.0b ; PLAN-G14.md § G14.0b, décisions n° 6, 7 et 12 ; R0-contre.md § 3.1-3.2).
#
# Python 3 de la machine, bibliothèque standard seule. Sept commandes :
#   sonde      les processus lourds à côté (motifs connus, ou plus d'une demi-UC sur une seconde), hors de l'arbre
#              de processus de compare-ref.sh ; imprime un JSON.
#   consigner  écrit le passage.json d'un passage : son côté, son code de sortie, ses durées, les sondes et la liste des
#              runtimes avant et après, les affinités vues sur les processus du banc, les DLL que BenchmarkDotNet a
#              construites et lancées (sha256 et empreinte normalisée, § « L'empreinte d'une DLL »).
#   analyser   lit une sortie de compare-ref.sh (contexte.json, passeN/<groupe>/<ordre>-<côté>/) et rend le verdict :
#              le rapport texte et le TSV, et un code de sortie (0 vert, 1 rouge, 3 refus, 4 rouges à confirmer).
#   empreinte  l'empreinte normalisée d'une DLL, seule (pour les contrôles).
#   gel        (G14.0d) le gel des DLL lancées du côté R, lu dans une sortie de compare-ref.sh : une ligne
#              « EMPREINTE  NOM » par DLL ; refusé (retour 1, la raison sur la sortie d'erreur) si les passages de R
#              ont lancé plus d'une empreinte d'une DLL, ou n'en ont pas relevé une.
#   gel-lire   vérifie qu'un fichier est un gel bien formé (les trois DLL, une fois chacune) et l'imprime ; retour 1
#              sinon, que compare-ref.sh traduit en code 2 (une erreur d'usage, avant toute mesure).
#   gel-verifier  compare les DLL lancées d'un passage (passage.json) au gel : retour 0 si elles sont celles du gel,
#              3 sinon, l'écart imprimé. compare-ref.sh l'appelle après chaque passage de R sous --lancees-r.
#
# LE VERDICT, cas par cas :
# - REFUS (ni vert ni rouge) : le runtime, le SDK, la version de BenchmarkDotNet, le système, le processeur, le job
#   (variables d'environnement, comptes d'itérations) ou le JIT du processus enfant diffèrent entre deux passages ;
#   DOTNET_TieredCompilation=0 absent ; l'affinité ou la personnalité (ASLR coupé) vue sur les processus du banc diffère
#   de celle demandée ; `dotnet --list-runtimes` ou `--list-sdks` change pendant un passage ; un processus lourd
#   tournait au début ou à la fin d'un passage ; le binaire lancé change entre deux passages d'un même côté ;
#   BenchmarkDotNet sort avec un code non nul ; un journal manque ; un passage n'a pas ses trois moyennes de
#   lancement ; un cas manque des deux côtés, ou du côté R ; son état imprimé change d'un passage à l'autre du même
#   côté, ou manque des deux côtés (tous les cas de la référence l'impriment) ; la passe de confirmation refuse le cas
#   ou ne l'a pas ; sous --lancees-r (G14.0d), une DLL lancée du côté R qui n'est pas celle du gel, ou qui n'est pas
#   relevée.
# - ROUGE EXACT : l'allocation par opération diffère (stable des deux côtés : à l'octet ; instable d'un côté : les
#   intervalles [min, max] des deux côtés sont disjoints ; le nombre de cas instables est dans l'en-tête) ; une ligne
#   d'état « // Banc (…) : … » diffère ; l'empreinte de MachineBanc diffère ; le cas manque du côté C seul.
# - ROUGE STATISTIQUE (décision n° 12) : l'unité est la moyenne de chaque lancement (les mesures Workload/Result du
#   JSON complet, par LaunchIndex) ; 3 lancements × 2 passages de l'ABBA = 6 valeurs par côté. Sur le logarithme, les
#   36 différences ln C_j − ln R_i ; l'estimateur de Hodges-Lehmann est leur médiane, et l'intervalle est celui de
#   Mann-Whitney, exact : [d_(k), d_(mn+1−k)], k le plus grand tel que P(U ≤ k−1) ≤ 0,005 sous l'hypothèse nulle. Pour
#   6 + 6 valeurs, k = 3, et le niveau réel est 1 − 2 × 4/924 = 99,13 % ; la borne basse dépasse zéro si au moins 34 des
#   36 paires ont C au-dessus de R, ce qui arrive avec la probabilité 4/924 = 0,43 % sous l'hypothèse nulle. Rouge si
#   la borne basse de C/R − 1 est au-dessus de zéro. Un rouge doit être confirmé par une passe 2 de son groupe (CRRC),
#   puis, Q37 tranchée par Julien le 24/10, par une passe 3 de son groupe seul (RCCR, comme la passe 1 : les trois
#   passes alternent alors l'ordre) ; chaque passe est analysée seule, et le rouge n'est retenu que si les trois le
#   voient. Un refus en passe 2 ou 3 donne un refus ; un rouge exact ne demande pas de passe de plus.
#   Pourquoi pas un bootstrap : avec six valeurs par côté, ses intervalles sont trop étroits (sa couverture est
#   inférieure au niveau annoncé), alors que celui de Mann-Whitney est exact, sans hypothèse de forme, à la seule
#   condition que les lancements soient échangeables ; l'ABBA les y rend en répartissant la dérive de l'hôte.
#   La borne haute sous zéro (C plus rapide) est dite, verte.

import argparse
import json
import math
import os
import re
import struct
import sys
import time
from pathlib import Path

ALPHA_UNILATERAL = 0.005
# Lourds dès qu'ils tournent : les séries, l'émulateur, une construction lancée en ligne de commande, un autre banc.
# Lourds s'ils travaillent : les serveurs de construction (ils restent quelques minutes après elle, au repos). Tout
# autre processus : lourd au-delà d'une UC et demie sur la seconde de la sonde (une construction, une compression) ;
# noté, sans refus, au-delà de 0,3 UC (l'IDE ouvert à côté a des pointes d'une UC : le refuser rendrait toute passe
# impossible, le A/A dit si elles pèsent).
PROGRAMMES_LOURDS = {"series.sh", "par.sh", "iXtal26.dll"}
VERBES_DOTNET = {"build", "test", "publish", "run", "msbuild", "restore"}
PROGRAMMES_SI_ACTIFS = {"VBCSCompiler", "MSBuild.dll"}


def motif_lourd(argv, actif):
    """Le programme d'un processus, lu dans argv[0] ou, derrière un interpréteur (bash, dotnet), argv[1] : jamais le
    texte libre de la ligne de commande (un shell qui contient « series.sh » dans une commande n'est pas une série)."""
    noms = [Path(a).name for a in argv[:2]]
    for n in noms:
        if n in PROGRAMMES_LOURDS or n.startswith("iXtal26.PerfBanc"):
            return n
    if noms and noms[0] == "dotnet" and len(argv) > 1 and argv[1] in VERBES_DOTNET:
        return f"dotnet {argv[1]}"
    if actif:
        for n in noms:
            if n in PROGRAMMES_SI_ACTIFS:
                return n
    return ""


SEUIL_SI_ACTIFS = 0.05
LIGNE_ETAT = re.compile(r"^// ([A-Za-z0-9]+) \((.*)\) : (.*)$")
ADDR_NO_RANDOMIZE = 0x0040000
CODE = {"vert": 0, "rouge": 1, "refus": 3, "a-confirmer": 4, "a-rejouer": 5}


# ---------------------------------------------------------------------------------------------------------- la sonde

def _cpu_par_pid():
    """Le temps UC consommé (tics) par processus, et le lien parent."""
    vus = {}
    for p in Path("/proc").iterdir():
        if not p.name.isdigit():
            continue
        try:
            stat = (p / "stat").read_text()
        except OSError:
            continue
        fin = stat.rfind(")")
        champs = stat[fin + 2:].split()
        vus[int(p.name)] = (int(champs[1]), int(champs[11]) + int(champs[12]))
    return vus


def _cmdline(pid):
    try:
        return Path(f"/proc/{pid}/cmdline").read_bytes().decode("utf-8", "replace")
    except OSError:
        return ""


def sonde(args):
    exclus = {os.getpid()}
    if args.racine:
        exclus.add(args.racine)
    t0 = _cpu_par_pid()
    time.sleep(args.duree)
    t1 = _cpu_par_pid()
    hz = os.sysconf("SC_CLK_TCK")

    def descend(pid):
        vus = set()
        while pid > 1 and pid not in vus:
            if pid in exclus:
                return True
            vus.add(pid)
            pid = t1.get(pid, (0, 0))[0]
        return False

    lourds, actifs = [], []
    for pid, (_, cpu1) in t1.items():
        if descend(pid):
            continue
        cmd = _cmdline(pid)
        charge = (cpu1 - t0.get(pid, (0, cpu1))[1]) / hz / args.duree
        motif = motif_lourd(cmd.rstrip("\0").split("\0"), charge > SEUIL_SI_ACTIFS)
        quoi = {"pid": pid, "uc": round(charge, 3), "motif": motif, "cmd": cmd.replace("\0", " ")[:200]}
        if motif or charge > args.seuil:
            lourds.append(quoi)
        elif charge > args.seuil_note:
            actifs.append(quoi)
    print(json.dumps({"date": time.strftime("%Y-%m-%dT%H:%M:%S"), "charge": os.getloadavg(), "lourds": lourds,
                      "actifs": actifs}, ensure_ascii=False))


# ------------------------------------------------------------------------------------- l'empreinte d'une DLL (PE/CLI)

def empreinte_dll(chemin):
    """Le sha256 de la DLL après avoir effacé ce qui dépend du chemin de construction et non du code : l'horodatage et
    la somme du PE, l'en-tête et les données de chaque entrée du répertoire de débogage (CodeView : GUID et chemin du
    PDB ; somme du PDB), et le tas #GUID des métadonnées (le MVID). BenchmarkDotNet reconstruit le banc dans un
    répertoire au nom tiré au hasard (iXtal26.PerfBanc-Job-XXXXXX-1) : le fichier change d'un passage à l'autre,
    l'empreinte non, tant que le code est le même et que le chemin garde sa longueur."""
    import hashlib
    b = bytearray(Path(chemin).read_bytes())
    pe = struct.unpack_from("<I", b, 0x3C)[0]
    if b[pe:pe + 4] != b"PE\0\0":
        raise ValueError(f"{chemin} : pas un PE")
    coff = pe + 4
    nsec, = struct.unpack_from("<H", b, coff + 2)
    taille_opt, = struct.unpack_from("<H", b, coff + 16)
    b[coff + 4:coff + 8] = bytes(4)
    opt = coff + 20
    magic, = struct.unpack_from("<H", b, opt)
    b[opt + 64:opt + 68] = bytes(4)
    rep = opt + (96 if magic == 0x10B else 112)
    sections = []
    for i in range(nsec):
        s = opt + taille_opt + 40 * i
        vsize, va, rsize, rptr = struct.unpack_from("<IIII", b, s + 8)
        sections.append((va, max(vsize, rsize), rptr))

    def fichier(rva):
        for va, taille, rptr in sections:
            if va <= rva < va + taille:
                return rva - va + rptr
        raise ValueError(f"RVA {rva:#x} hors des sections")

    dbg_rva, dbg_taille = struct.unpack_from("<II", b, rep + 8 * 6)
    if dbg_rva:
        d = fichier(dbg_rva)
        for k in range(dbg_taille // 28):
            e = d + 28 * k
            taille_donnees, _, ptr = struct.unpack_from("<III", b, e + 16)
            b[ptr:ptr + taille_donnees] = bytes(taille_donnees)
            b[e:e + 28] = bytes(28)
    clr_rva, _ = struct.unpack_from("<II", b, rep + 8 * 14)
    if clr_rva:
        c = fichier(clr_rva)
        md_rva, _ = struct.unpack_from("<II", b, c + 8)
        md = fichier(md_rva)
        lv, = struct.unpack_from("<I", b, md + 12)
        p = md + 16 + lv + 2
        nflux, = struct.unpack_from("<H", b, p)
        p += 2
        for _ in range(nflux):
            off, taille = struct.unpack_from("<II", b, p)
            fin = b.index(0, p + 8)
            nom = b[p + 8:fin].decode()
            p = p + 8 + ((fin - (p + 8)) // 4 + 1) * 4
            if nom == "#GUID":
                b[md + off:md + off + taille] = bytes(taille)
    return hashlib.sha256(b).hexdigest()


def cmd_empreinte(args):
    for f in args.dll:
        print(empreinte_dll(f), f)


# ------------------------------------------------------------------------- le gel des DLL lancées (G14.0d)

DLL_BANC = ("iXtal26.dll", "iXtal26.SoftFloat.dll", "iXtal26.PerfBanc.dll")


def lire_gel(f):
    """Le gel : une ligne « EMPREINTE  NOM » par DLL lancée, les trois DLL du banc une fois chacune ; rien d'autre."""
    gel = {}
    for ligne in Path(f).read_text().splitlines():
        if not ligne.strip():
            continue
        m = re.fullmatch(r"([0-9a-f]{64})  (\S+)", ligne)
        if not m or m.group(2) not in DLL_BANC or m.group(2) in gel:
            raise ValueError(f"{f} : ligne illisible ou en double : {ligne!r}")
        gel[m.group(2)] = m.group(1)
    if set(gel) != set(DLL_BANC):
        raise ValueError(f"{f} : il manque {', '.join(sorted(set(DLL_BANC) - set(gel)))}")
    return gel


def ecarts_gel(passage, gel):
    """Ce qui sépare les DLL lancées d'un passage (son passage.json) du gel : une liste vide si ce sont les siennes."""
    ecarts = []
    lancees = (passage or {}).get("dll_lancees") or {}
    for nom in DLL_BANC:
        vues = sorted({e["empreinte"] for e in lancees.get(nom, [])})
        if not vues:
            ecarts.append(f"{nom} lancé non relevé")
        elif vues != [gel[nom]]:
            ecarts.append(f"{nom} lancé {', '.join(v[:16] for v in vues)}…, le gel {gel[nom][:16]}…")
    return ecarts


def cmd_gel(args):
    vues = {}
    for passe in (1, 2, 3):
        for groupe, passages in charger_passe(args.sortie, passe).items():
            for o, c, p, _, _ in passages:
                if c != args.cote:
                    continue
                if not p:
                    sys.exit(f"compare_ref gel : passe {passe}, {groupe}, {o}-{c} : passage.json absent")
                for nom in DLL_BANC:
                    e = {x["empreinte"] for x in (p.get("dll_lancees") or {}).get(nom, [])}
                    if not e:
                        sys.exit(f"compare_ref gel : passe {passe}, {groupe}, {o}-{c} : {nom} lancé non relevé")
                    vues.setdefault(nom, set()).update(e)
    for nom in DLL_BANC:
        if len(vues.get(nom, ())) != 1:
            sys.exit(f"compare_ref gel : {nom} : {len(vues.get(nom, ()))} empreintes lancées du côté {args.cote} "
                     f"({', '.join(sorted(v[:16] for v in vues.get(nom, ())))}) ; il en faut une")
    sys.stdout.write("".join(f"{next(iter(vues[nom]))}  {nom}\n" for nom in DLL_BANC))


def cmd_gel_lire(args):
    try:
        gel = lire_gel(args.fichier)
    except (OSError, ValueError) as e:
        sys.exit(f"compare_ref gel-lire : {e}")
    sys.stdout.write("".join(f"{gel[nom]}  {nom}\n" for nom in DLL_BANC))


def cmd_gel_verifier(args):
    try:
        gel = lire_gel(args.gel)
    except (OSError, ValueError) as e:
        print(f"le gel est illisible : {e}")
        return 3
    ecarts = ecarts_gel(_lire_json(args.passage), gel)
    if ecarts:
        print(" ; ".join(ecarts))
        return 3
    return 0


# ----------------------------------------------------------------------------------------------------- consigner

def _lire_json(f, defaut=None):
    try:
        return json.loads(Path(f).read_text())
    except (OSError, ValueError):
        return defaut


def consigner(args):
    import hashlib
    d = Path(args.dir)
    affinites = set()
    f = d / "affinite.txt"
    if f.exists():
        for ligne in f.read_text().splitlines():
            morceaux = ligne.split("\t")
            if len(morceaux) == 3:
                affinites.add(tuple(morceaux))
    generes = {}
    for g in sorted(Path(args.generes).glob("iXtal26.PerfBanc-*")):
        for nom in ("iXtal26.dll", "iXtal26.SoftFloat.dll", "iXtal26.PerfBanc.dll"):
            dll = g / "bin" / "Release" / "net10.0" / nom
            if dll.exists():
                generes.setdefault(nom, []).append({
                    "repertoire": g.name, "sha256": hashlib.sha256(dll.read_bytes()).hexdigest(),
                    "empreinte": empreinte_dll(dll)})
    passage = {
        "cote": args.cote, "arbre": args.arbre, "groupe": args.groupe, "passe": args.passe, "ordre": args.ordre,
        "rc": args.rc, "debut": args.debut, "fin": args.fin, "duree_s": round(args.fin - args.debut, 1),
        "sonde_avant": _lire_json(args.sonde_avant), "sonde_apres": _lire_json(args.sonde_apres),
        "runtimes_avant": Path(args.runtimes_avant).read_text(),
        "runtimes_apres": Path(args.runtimes_apres).read_text(),
        "affinites": sorted(list(a) for a in affinites), "dll_lancees": generes,
    }
    (d / "passage.json").write_text(json.dumps(passage, ensure_ascii=False, indent=1))


# ------------------------------------------------------------------------------------------------------ analyser

def lire_journal(f):
    """Les lignes d'état et l'environnement du processus enfant, rattachés à leur cas (l'en-tête « // Benchmark: »
    porte le DisplayInfo du JSON)."""
    etats, enfant = {}, {}
    cas = None
    for ligne in Path(f).read_text(errors="replace").splitlines():
        if ligne.startswith("// Benchmark: "):
            cas = ligne[len("// Benchmark: "):]
            etats.setdefault(cas, [])
            enfant.setdefault(cas, set())
            continue
        if ligne.startswith("// ***** BenchmarkRunner: End"):
            cas = None
        if cas is None:
            continue
        if ligne.startswith("// Runtime=") or ligne.startswith("// GC="):
            enfant[cas].add(ligne[3:])
            continue
        m = LIGNE_ETAT.match(ligne)
        if m and ligne[3:] not in etats[cas]:
            etats[cas].append(ligne[3:])
    return etats, enfant


def job_sans_id(display):
    m = re.search(r": (?:Job-[A-Z0-9]+|[A-Za-z]+)\((.*)\)( \[.*\])?$", display)
    return m.group(1) if m else "(job illisible)"


def lire_passage(d):
    """Un passage : ses cas (moyennes de lancement, allocation, job, états), son contexte."""
    p = _lire_json(d / "passage.json", {})
    # Le journal de BenchmarkDotNet : BenchmarkRun-<date>.log, ou <espace de noms>.<type>-<date>.log quand le filtre ne
    # choisit qu'un type ; jamais sortie.log (la sortie du script).
    journaux = sorted(f for f in d.glob("*.log") if f.name != "sortie.log")
    etats, enfant = lire_journal(journaux[-1]) if journaux else ({}, {})
    if p is not None:
        p["_journal"] = bool(journaux)
    cas, hotes = {}, []
    for j in sorted((d / "results").glob("*-report-full.json")):
        data = json.loads(j.read_text())
        hotes.append(data.get("HostEnvironmentInfo", {}))
        for b in data.get("Benchmarks", []):
            lancements = {}
            for m in b.get("Measurements") or []:
                if m.get("IterationMode") == "Workload" and m.get("IterationStage") == "Result":
                    lancements.setdefault(m["LaunchIndex"], []).append(m["Nanoseconds"] / m["Operations"])
            moyennes = [sum(v) / len(v) for _, v in sorted(lancements.items())]
            mem = b.get("Memory") or {}
            cas[b["FullName"]] = {
                "moyennes": moyennes,
                "alloc": mem.get("BytesAllocatedPerOperation"),
                "job": job_sans_id(b.get("DisplayInfo", "")),
                "intrinseques": b.get("HardwareIntrinsics"),
                "etats": etats.get(b.get("DisplayInfo"), []),
                "enfant": sorted(enfant.get(b.get("DisplayInfo"), [])),
            }
    return p, cas, hotes


def seuil_mw(m, n, alpha=ALPHA_UNILATERAL):
    """Le k de l'intervalle de Mann-Whitney exact (k le plus grand tel que P(U ≤ k−1) ≤ alpha), et le niveau réel
    bilatéral 1 − 2 P(U ≤ k−1). k = 0 : aucun intervalle à ce niveau."""
    # Le nombre de façons d'obtenir U = u, par récurrence sur (m, n).
    memo = {}

    def compte(a, c, u):
        if u < 0:
            return 0
        if a == 0 or c == 0:
            return 1 if u == 0 else 0
        cle = (a, c, u)
        if cle not in memo:
            memo[cle] = compte(a - 1, c, u - c) + compte(a, c - 1, u)
        return memo[cle]

    total = math.comb(m + n, m)
    cumul, k = 0, 0
    for u in range(m * n + 1):
        cumul += compte(m, n, u)
        if cumul / total <= alpha:
            k = u + 1
        else:
            break
    p = sum(compte(m, n, u) for u in range(k)) / total
    return k, 1 - 2 * p


def hodges_lehmann(r, c):
    """Sur ln : les différences ln c − ln r, leur médiane, l'intervalle de Mann-Whitney ; rendus en C/R − 1."""
    d = sorted(math.log(y) - math.log(x) for x in r for y in c)
    n = len(d)
    med = (d[(n - 1) // 2] + d[n // 2]) / 2
    k, niveau = seuil_mw(len(r), len(c))
    if k == 0:
        return math.expm1(med), None, None, niveau
    return math.expm1(med), math.expm1(d[k - 1]), math.expm1(d[n - k]), niveau


def mediane(v):
    s = sorted(v)
    return (s[(len(s) - 1) // 2] + s[len(s) // 2]) / 2 if s else float("nan")


def analyser_groupe(passages, cas_attendus, gel=None):
    """Une passe ABBA d'un groupe : passages = [(ordre, côté, contexte, cas, hôtes)]. Rend les refus du groupe et,
    par cas, le verdict de la passe. gel : les empreintes des DLL lancées que le côté R doit avoir (G14.0d), ou None."""
    refus, verdicts = [], {}
    cotes = {"R": [x for x in passages if x[1] == "R"], "C": [x for x in passages if x[1] == "C"]}
    if len(cotes["R"]) != 2 or len(cotes["C"]) != 2:
        refus.append(f"la passe n'a pas deux passages par côté (R {len(cotes['R'])}, C {len(cotes['C'])})")
    # Le contexte des passages.
    for ordre, cote, p, _, hotes in passages:
        nom = f"{ordre}-{cote}"
        if not p:
            refus.append(f"{nom} : passage.json absent")
            continue
        for moment in ("sonde_avant", "sonde_apres"):
            s = p.get(moment) or {}
            if s.get("lourds"):
                quoi = "; ".join(f"{x['pid']} {x['motif'] or str(x['uc']) + ' UC'} {x['cmd'][:60]}"
                                 for x in s["lourds"])
                refus.append(f"{nom} : un processus lourd tournait ({moment.split('_')[1]}) : {quoi}")
            if not s:
                refus.append(f"{nom} : la sonde {moment.split('_')[1]} manque")
        if not p.get("_journal"):
            refus.append(f"{nom} : le journal de BenchmarkDotNet est absent (les états ne se lisent pas)")
        if p.get("rc", 0) != 0:
            refus.append(f"{nom} : BenchmarkDotNet est sorti avec le code {p.get('rc')}")
        if p.get("runtimes_avant") != p.get("runtimes_apres"):
            refus.append(f"{nom} : `dotnet --list-runtimes`/`--list-sdks` a changé pendant le passage")
        aff = p.get("affinites") or []
        if not aff:
            refus.append(f"{nom} : aucune affinité relevée sur les processus du banc")
        for _, cpus, perso in aff:
            if not re.fullmatch(r"[0-9a-fA-F]+", perso or ""):
                if cpus:
                    refus.append(f"{nom} : la personnalité d'un processus du banc est illisible ({perso!r})")
                continue
            if cpus != p.get("coeurs_attendus", cpus) or not (int(perso, 16) & ADDR_NO_RANDOMIZE):
                refus.append(f"{nom} : un processus du banc tournait sur {cpus}, personnalité {perso} (ASLR "
                             f"{'coupé' if int(perso, 16) & ADDR_NO_RANDOMIZE else 'actif'})")
        if not hotes:
            refus.append(f"{nom} : aucun JSON de BenchmarkDotNet")
    # Le même environnement partout.
    def comparer(etiquette, valeurs):
        distinctes = {json.dumps(v, sort_keys=True) for _, v in valeurs}
        if len(distinctes) > 1:
            detail = " ; ".join(f"{n} {json.dumps(v, ensure_ascii=False)[:120]}" for n, v in valeurs)
            refus.append(f"{etiquette} diffère entre les passages : {detail}")

    cles_hote = ("BenchmarkDotNetVersion", "OsVersion", "ProcessorName", "RuntimeVersion", "DotNetCliVersion",
                 "Configuration", "HasAttachedDebugger", "Architecture", "HasRyuJit")
    for cle in cles_hote:
        comparer(f"l'hôte ({cle})", [(f"{o}-{c}", sorted({h.get(cle) for h in hs}, key=str))
                                     for o, c, _, _, hs in passages if hs])
    comparer("l'affinité relevée", [(f"{o}-{c}", sorted({a[1] for a in p.get("affinites") or []}))
                                    for o, c, p, _, _ in passages if p])
    comparer("la liste des runtimes", [(f"{o}-{c}", p.get("runtimes_avant")) for o, c, p, _, _ in passages if p])
    # Le binaire lancé : le même d'un passage à l'autre d'un côté.
    for cote, ps in cotes.items():
        for nom in ("iXtal26.dll", "iXtal26.SoftFloat.dll", "iXtal26.PerfBanc.dll"):
            vues = {(e["empreinte"]) for _, _, p, _, _ in ps for e in (p.get("dll_lancees") or {}).get(nom, [])}
            if len(vues) > 1:
                refus.append(f"côté {cote} : le {nom} lancé a changé entre les deux passages (empreintes "
                             f"{', '.join(sorted(v[:12] for v in vues))})")
            if not vues and ps:
                refus.append(f"côté {cote} : le {nom} lancé n'a pas été relevé")
    # G14.0d : le binaire lancé du côté R est celui du gel, passage par passage.
    if gel:
        for o, _, p, _, _ in cotes["R"]:
            for e in ecarts_gel(p, gel):
                refus.append(f"{o}-R : le binaire lancé n'est pas celui du gel : {e}")
    # Les cas.
    tous = set()
    for _, _, _, cas, _ in passages:
        tous |= set(cas)
    if cas_attendus is not None and len(tous) != cas_attendus:
        refus.append(f"{len(tous)} cas trouvés, {cas_attendus} attendus : le filtre ne choisit pas les cas listés")
    for nom in sorted(tous):
        v = {"cas": nom, "raisons": []}
        verdicts[nom] = v
        par = {s: [(o, cas.get(nom)) for o, cc, _, cas, _ in passages if cc == s] for s in ("R", "C")}
        if not par["R"] or not par["C"]:
            # Une passe arrêtée après son premier passage (G14.0d : le binaire lancé n'est pas celui du gel) n'a pas de
            # passage de l'autre côté : rien à comparer, le refus du groupe le dit.
            v["verdict"], v["raisons"] = "refus", ["un côté n'a aucun passage (la passe s'est arrêtée)"]
            continue
        manque = {s: [o for o, x in par[s] if not x or not x["moyennes"]] for s in par}
        if manque["R"] and manque["C"]:
            v["verdict"], v["raisons"] = "refus", ["le cas manque ou a échoué des deux côtés"]
            continue
        if manque["R"]:
            v["verdict"], v["raisons"] = "refus", [f"le cas manque ou a échoué du côté R (passage {manque['R']})"]
            continue
        if manque["C"]:
            v["verdict"], v["raisons"] = "rouge", [f"exact : le cas manque ou a échoué du côté C ({manque['C']})"]
            continue
        x = {s: [e for _, e in par[s]] for s in par}
        # Le job, les intrinsèques, le processus enfant.
        jobs = {e["job"] for s in x for e in x[s]}
        if len(jobs) > 1:
            v["verdict"], v["raisons"] = "refus", ["le job diffère : " + " | ".join(sorted(jobs))]
            continue
        job = jobs.pop()
        if "DOTNET_TieredCompilation=0" not in job:
            v["verdict"], v["raisons"] = "refus", [f"DOTNET_TieredCompilation=0 absent du job ({job})"]
            continue
        m = re.search(r"LaunchCount=(\d+)", job)
        lancements = int(m.group(1)) if m else 3
        courts = {f"{s}{o}": len(e["moyennes"]) for s in par for o, e in par[s] if len(e["moyennes"]) != lancements}
        if courts:
            v["verdict"] = "refus"
            v["raisons"] = [f"un passage n'a pas ses {lancements} moyennes de lancement : {courts}"]
            v["n_R"] = sum(len(e["moyennes"]) for _, e in par["R"])
            v["n_C"] = sum(len(e["moyennes"]) for _, e in par["C"])
            continue
        for cle, quoi in (("intrinseques", "les instructions de l'hôte"), ("enfant", "le runtime du processus "
                                                                                     "enfant")):
            if len({json.dumps(e[cle]) for s in x for e in x[s]}) > 1:
                v["verdict"] = "refus"
                v["raisons"].append(f"{quoi} diffère entre les passages")
        if v.get("verdict") == "refus":
            continue
        # Les états imprimés.
        etats = {s: [tuple(e["etats"]) for e in x[s]] for s in x}
        instable = [s for s in etats if len(set(etats[s])) > 1]
        if instable:
            v["verdict"] = "refus"
            v["raisons"].append(f"l'état imprimé change d'un passage à l'autre du côté {', '.join(instable)}")
            continue
        exacts = []
        er, ec = etats["R"][0], etats["C"][0]
        if er != ec:
            for a, b in zip(list(er) + [""] * len(ec), list(ec) + [""] * len(er)):
                if a != b:
                    quoi = "l'empreinte de la machine" if (a or b).startswith("MachineBanc") else "l'état imprimé"
                    exacts.append(f"exact : {quoi} diffère ; R « {a[:150]} » ; C « {b[:150]} »")
                    break
        if not er and not ec:
            # Tous les cas de la référence impriment leur état ; aucun état des deux côtés, c'est un journal perdu ou
            # un cas qui ne vérifie plus rien : on ne peut pas juger.
            v["verdict"] = "refus"
            v["raisons"].append("aucune ligne d'état imprimée, des deux côtés")
            continue
        # L'allocation.
        # Stable des deux côtés : égalité exacte. Instable d'un côté (des valeurs différentes d'un passage à l'autre) :
        # les intervalles [min, max] des deux côtés doivent se recouvrir ; disjoints, c'est un rouge exact. Le
        # contrôle n'est jamais retiré.
        al = {s: {e["alloc"] for e in x[s]} for s in x}
        v["alloc_R"], v["alloc_C"] = sorted(al["R"], key=str), sorted(al["C"], key=str)
        v["alloc_instable"] = len(al["R"]) > 1 or len(al["C"]) > 1
        if None in al["R"] | al["C"]:
            exacts.append("exact : l'allocation n'est pas relevée d'un côté")
        elif v["alloc_instable"]:
            (rmin, rmax), (cmin, cmax) = (min(al["R"]), max(al["R"])), (min(al["C"]), max(al["C"]))
            if rmax < cmin or cmax < rmin:
                exacts.append(f"exact : l'allocation diffère, R [{rmin}, {rmax}] o, C [{cmin}, {cmax}] o (disjoints)")
            else:
                v["raisons"].append(f"allocation instable, intervalles qui se recouvrent (R [{rmin}, {rmax}], "
                                    f"C [{cmin}, {cmax}])")
        elif al["R"] != al["C"]:
            exacts.append(f"exact : l'allocation par opération diffère, R {v['alloc_R'][0]} o, C {v['alloc_C'][0]} o")
        # La statistique.
        r = [m for e in x["R"] for m in e["moyennes"]]
        c = [m for e in x["C"] for m in e["moyennes"]]
        v["R_ns"], v["C_ns"], v["n_R"], v["n_C"] = mediane(r), mediane(c), len(r), len(c)
        v["disp_R"] = (max(r) - min(r)) / mediane(r) if r else float("nan")
        v["disp_C"] = (max(c) - min(c)) / mediane(c) if c else float("nan")
        hl, bas, haut, niveau = hodges_lehmann(r, c)
        v["ecart"], v["bas"], v["haut"], v["niveau"] = hl, bas, haut, niveau
        if exacts:
            v["verdict"] = "rouge"
            v["raisons"] = exacts + v["raisons"]
        elif bas is None:
            v["verdict"] = "refus"
            v["raisons"].append(f"trop peu de lancements ({len(r)} + {len(c)}) pour un intervalle à 99 %")
        elif bas > 0:
            v["verdict"] = "rouge-stat"
            v["raisons"].append(f"statistique : la borne basse de C/R − 1 est {bas * 100:+.2f} % (> 0)")
        elif haut < 0:
            v["verdict"] = "vert"
            v["raisons"].append(f"C plus rapide : la borne haute est {haut * 100:+.2f} %")
        else:
            v["verdict"] = "vert"
    vus = set()
    refus = [r for r in refus if not (r in vus or vus.add(r))]
    return refus, verdicts


def charger_passe(sortie, passe):
    groupes = {}
    racine = Path(sortie) / f"passe{passe}"
    if not racine.exists():
        return groupes
    for g in sorted(racine.iterdir()):
        if not g.is_dir():
            continue
        passages = []
        for d in sorted(g.iterdir()):
            m = re.fullmatch(r"(\d+)-([RC])", d.name)
            if m and d.is_dir():
                p, cas, hotes = lire_passage(d)
                passages.append((int(m.group(1)), m.group(2), p, cas, hotes))
        groupes[g.name] = passages
    return groupes


def pct(x):
    return "—" if x is None or (isinstance(x, float) and math.isnan(x)) else f"{x * 100:+.2f} %"


def analyser(args):
    sortie = Path(args.sortie)
    ctx = _lire_json(sortie / "contexte.json", {})
    attendus = ctx.get("cas_attendus", {})
    coeurs = ctx.get("coeurs")
    refus_globaux = list(ctx.get("refus", []))
    if ctx.get("runtimes_debut") and ctx.get("runtimes_fin") and ctx["runtimes_debut"] != ctx["runtimes_fin"]:
        refus_globaux.append("`dotnet --list-runtimes`/`--list-sdks` a changé entre le début et la fin")
    for cote in ("R", "C"):
        for nom, sha in (ctx.get("dll_arbre", {}).get(cote) or {}).items():
            if not sha:
                refus_globaux.append(f"côté {cote} : {nom} absent de l'arbre (non construit)")
    resultats, n_passages, notes = {}, 0, []
    for passe in (1, 2, 3):
        for groupe, passages in charger_passe(sortie, passe).items():
            for o, c, p, _, _ in passages:
                n_passages += 1
                if p is not None and coeurs:
                    p["coeurs_attendus"] = coeurs
                for moment in ("sonde_avant", "sonde_apres"):
                    for x in ((p or {}).get(moment) or {}).get("actifs", []):
                        notes.append(f"passe {passe}, {groupe}, {o}-{c} ({moment[6:]}) : {x['uc']} UC, "
                                     f"{x['cmd'].split(' ')[0][-40:]}")
            refus, verdicts = analyser_groupe(passages, attendus.get(groupe), ctx.get("lancees_r"))
            resultats.setdefault(groupe, {})[passe] = (refus, verdicts)
    # G14.0d : chaque groupe attendu (ceux que le filtre GROUPES a retenus, contexte.json) doit être dans la passe 1 ;
    # un groupe qui manque, c'est une sortie tronquée ou abandonnée, et rien ne dit qu'il serait vert.
    for groupe in sorted(attendus):
        if 1 not in resultats.get(groupe, {}):
            refus_globaux.append(f"le groupe {groupe}, attendu, manque de la passe 1")
    lignes_tsv = ["groupe\tcas\tR_ns\tC_ns\tecart\tic_bas\tic_haut\tdisp_R\tdisp_C\talloc_R\talloc_C\tpasse2_ecart\t"
                  "passe2_ic_bas\tpasse2_ic_haut\tpasse3_ecart\tpasse3_ic_bas\tpasse3_ic_haut\tverdict\traison"]
    rapport, compte = [], {"vert": 0, "rouge": 0, "refus": 0, "a-confirmer": 0, "a-rejouer": 0}
    a_confirmer, a_rejouer, refus_total, instables = [], [], list(refus_globaux), []
    for groupe in sorted(resultats):
        p1 = resultats[groupe].get(1)
        p2 = resultats[groupe].get(2)
        p3 = resultats[groupe].get(3)
        if p1 is None:
            continue
        refus1, v1 = p1
        refus2, v2 = p2 if p2 else ([], {})
        refus3, v3 = p3 if p3 else ([], {})
        for r in refus1:
            refus_total.append(f"{groupe} (passe 1) : {r}")
        for r in refus2:
            refus_total.append(f"{groupe} (passe 2) : {r}")
        for r in refus3:
            refus_total.append(f"{groupe} (passe 3) : {r}")
        rapport.append(f"\n== {groupe}")
        for nom in sorted(v1):
            a = v1[nom]
            b = v2.get(nom)
            c = v3.get(nom)
            verdict, raisons = a["verdict"], list(a["raisons"])
            if refus1 or refus_globaux:
                verdict = "refus"
            elif p2 and b is None:
                verdict = "refus"
                raisons.append("la passe de confirmation n'a pas ce cas")
            elif b is not None and b["verdict"] in ("refus", "a-confirmer"):
                verdict = "refus"
                raisons.append("la passe de confirmation refuse ce cas : " + " ; ".join(b["raisons"]))
            elif verdict == "rouge-stat":
                if b is None:
                    verdict = "a-confirmer"
                    a_confirmer.append(groupe)
                elif refus2:
                    verdict = "refus"
                    raisons.append("la passe de confirmation est refusée")
                elif b["verdict"] == "rouge":
                    verdict = "rouge"
                    raisons += ["passe 2 : " + r for r in b["raisons"]]
                elif b["verdict"] == "rouge-stat":
                    # Q37 (Julien, 24/10) : un rouge confirmé deux fois est rejoué une troisième fois, par son groupe
                    # seul ; il n'est retenu que si cette passe est rouge aussi.
                    raisons.append(f"confirmé par la passe 2 : {pct(b.get('ecart'))} [{pct(b.get('bas'))} ; "
                                   f"{pct(b.get('haut'))}]")
                    if not p3:
                        verdict = "a-rejouer"
                        a_rejouer.append(groupe)
                        raisons.append("à rejouer : la passe 3 manque")
                    elif refus3 or c is None or c["verdict"] == "refus":
                        verdict = "refus"
                        raisons.append("la passe 3 refuse ce cas" + (" : " + " ; ".join(c["raisons"])
                                                                     if c is not None else ""))
                    elif c["verdict"] in ("rouge-stat", "rouge"):
                        verdict = "rouge"
                        raisons.append(f"retenu par la passe 3 : {pct(c.get('ecart'))} [{pct(c.get('bas'))} ; "
                                       f"{pct(c.get('haut'))}]")
                    else:
                        verdict = "vert"
                        raisons.append(f"non retenu par la passe 3 : {pct(c.get('ecart'))} [{pct(c.get('bas'))} ; "
                                       f"{pct(c.get('haut'))}]")
                else:
                    verdict = "vert"
                    raisons.append(f"non confirmé par la passe 2 : {pct(b.get('ecart'))} [{pct(b.get('bas'))} ; "
                                   f"{pct(b.get('haut'))}]")
            elif b is not None and b["verdict"] == "rouge" and verdict == "vert":
                verdict = "rouge"
                raisons += ["passe 2 : " + r for r in b["raisons"]]
            if verdict == "vert" and c is not None and c["verdict"] == "rouge":
                verdict = "rouge"
                raisons += ["passe 3 : " + r for r in c["raisons"]]
            compte[verdict] += 1
            court = nom.replace("iXtal26.PerfBanc.", "")
            for passe_vue, w in ((1, a), (2, b), (3, c)):
                if w and w.get("alloc_instable"):
                    instables.append(f"{court} (passe {passe_vue}) : R {w['alloc_R']}, C {w['alloc_C']}")
            raison = " ; ".join(raisons)
            rapport.append(f"  {verdict.upper():12s} {court}")
            if "R_ns" in a:
                rapport.append(f"      R {a['R_ns'] / 1000:12.3f} µs   C {a['C_ns'] / 1000:12.3f} µs   C/R − 1 "
                               f"{pct(a['ecart'])}  IC {a['niveau'] * 100:.2f} % [{pct(a['bas'])} ; "
                               f"{pct(a['haut'])}]  étendue R {pct(a['disp_R'])[1:]} C {pct(a['disp_C'])[1:]}"
                               f"  alloc {a['alloc_R']}/{a['alloc_C']}  n {a.get('n_R')}+{a.get('n_C')}")
            elif "n_R" in a:
                rapport.append(f"      n {a['n_R']}+{a['n_C']} moyennes de lancement")
            if raison:
                rapport.append(f"      {raison}")
            lignes_tsv.append("\t".join(str(z) for z in (
                groupe, court, f"{a.get('R_ns', float('nan')):.1f}", f"{a.get('C_ns', float('nan')):.1f}",
                pct(a.get("ecart")), pct(a.get("bas")), pct(a.get("haut")), pct(a.get("disp_R")),
                pct(a.get("disp_C")), a.get("alloc_R"), a.get("alloc_C"),
                pct(b.get("ecart")) if b else "", pct(b.get("bas")) if b else "", pct(b.get("haut")) if b else "",
                pct(c.get("ecart")) if c else "", pct(c.get("bas")) if c else "", pct(c.get("haut")) if c else "",
                verdict, raison)))
    if refus_total or compte["refus"]:
        final = "refus"
    elif compte["rouge"]:
        final = "rouge"
    elif compte["a-confirmer"]:
        final = "a-confirmer"
    elif compte["a-rejouer"]:
        final = "a-rejouer"
    else:
        final = "vert"
    tete = [f"compare-ref : {final.upper()}",
            f"  R {ctx.get('arbre', {}).get('R')} ({ctx.get('commit', {}).get('R', '?')[:12]})",
            f"  C {ctx.get('arbre', {}).get('C')} ({ctx.get('commit', {}).get('C', '?')[:12]})"]
    for cote in ("R", "C"):
        for nom, sha in sorted((ctx.get("dll_arbre", {}).get(cote) or {}).items()):
            tete.append(f"  {cote} {nom} {sha}")
    if ctx.get("lancees_r"):
        tete.append(f"  gel des DLL lancées de R ({ctx.get('lancees_r_fichier')}) : vérifié à chaque passage de R")
        tete += [f"    {nom} {ctx['lancees_r'].get(nom)}" for nom in DLL_BANC]
    else:
        tete.append("  gel des DLL lancées de R : non demandé (--lancees-r)")
    tete += [f"  hôte : {ctx.get('processeur', '?')} ; gouverneur {ctx.get('gouverneur', '?')} ; noyau "
             f"{ctx.get('noyau', '?')}",
             f"  runtimes : {' | '.join((ctx.get('runtimes_debut') or '?').splitlines())}",
             f"  affinité : taskset -c {coeurs} ; setarch -R ; job : {ctx.get('job', {})}",
             f"  durée : {ctx.get('duree_s', '?')} s ; cas : {sum(compte.values())} (vert {compte['vert']}, rouge "
             f"{compte['rouge']}, refus {compte['refus']}, à confirmer {compte['a-confirmer']}, à rejouer "
             f"{compte['a-rejouer']})",
             f"  méthode : moyennes de lancement, Hodges-Lehmann sur ln, IC de Mann-Whitney exact (6 + 6 : "
             f"{seuil_mw(6, 6)[1] * 100:.2f} %), rouge si la borne basse > 0, confirmé par une passe 2 (CRRC), retenu "
             f"si une passe 3 (RCCR) le voit aussi (Q37)"]
    tete.append(f"  allocations instables d'un passage à l'autre : {len(instables)} cas")
    tete += [f"    - {n}" for n in instables[:20]]
    tete.append(f"  processus actifs à côté (plus de 0,3 UC, notés sans refus) : {len(notes)} relevés sur "
                f"{n_passages} passages")
    tete += [f"    - {n}" for n in notes[:20]]
    if refus_total:
        tete.append("  REFUS :")
        tete += [f"    - {r}" for r in refus_total]
    if a_confirmer:
        tete.append(f"  à confirmer : {' '.join(sorted(set(a_confirmer)))}")
    if a_rejouer:
        tete.append(f"  à rejouer (passe 3) : {' '.join(sorted(set(a_rejouer)))}")
    texte = "\n".join(tete + rapport) + "\n"
    (sortie / "rapport.txt").write_text(texte)
    (sortie / "rapport.tsv").write_text("\n".join(lignes_tsv) + "\n")
    (sortie / "a-confirmer.txt").write_text("\n".join(sorted(set(a_confirmer))) + ("\n" if a_confirmer else ""))
    (sortie / "a-rejouer.txt").write_text("\n".join(sorted(set(a_rejouer))) + ("\n" if a_rejouer else ""))
    if not args.silencieux:
        sys.stdout.write(texte)
    return CODE[final]


def main():
    ap = argparse.ArgumentParser(description="le comparateur de compare-ref.sh (G14.0b)")
    sp = ap.add_subparsers(dest="commande", required=True)
    s = sp.add_parser("sonde")
    s.add_argument("--racine", type=int, default=0, help="le PID dont la descendance est exclue")
    s.add_argument("--duree", type=float, default=1.0)
    s.add_argument("--seuil", type=float, default=1.5, help="les UC au-delà desquelles un processus est lourd")
    s.add_argument("--seuil-note", type=float, default=0.3, help="les UC au-delà desquelles il est noté")
    c = sp.add_parser("consigner")
    for a in ("dir", "cote", "arbre", "groupe", "sonde-avant", "sonde-apres", "runtimes-avant", "runtimes-apres",
              "generes"):
        c.add_argument(f"--{a}", required=True)
    for a in ("passe", "ordre", "rc"):
        c.add_argument(f"--{a}", type=int, required=True)
    for a in ("debut", "fin"):
        c.add_argument(f"--{a}", type=float, required=True)
    a = sp.add_parser("analyser")
    a.add_argument("sortie")
    a.add_argument("--silencieux", action="store_true")
    e = sp.add_parser("empreinte")
    e.add_argument("dll", nargs="+")
    g = sp.add_parser("gel")
    g.add_argument("sortie")
    g.add_argument("--cote", default="R", choices=("R", "C"))
    gl = sp.add_parser("gel-lire")
    gl.add_argument("fichier")
    gv = sp.add_parser("gel-verifier")
    gv.add_argument("--passage", required=True)
    gv.add_argument("--gel", required=True)
    args = ap.parse_args()
    if args.commande == "sonde":
        sonde(args)
    elif args.commande == "consigner":
        consigner(args)
    elif args.commande == "empreinte":
        cmd_empreinte(args)
    elif args.commande == "gel":
        cmd_gel(args)
    elif args.commande == "gel-lire":
        cmd_gel_lire(args)
    elif args.commande == "gel-verifier":
        sys.exit(cmd_gel_verifier(args))
    else:
        sys.exit(analyser(args))


if __name__ == "__main__":
    main()
