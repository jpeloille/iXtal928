# SPDX-FileCopyrightText: 2026 Julien Peloille
# SPDX-License-Identifier: GPL-2.0-only
#
# ORACLE: (aucun) — outillage de la preuve des membres (G15.0b5, PLAN-G15.md § G15.0b).
#
# table_noms.py — la lecture de la table des noms en python, celle de tools/noms/TableDesNoms.cs : partagée par
# tools/listings-jit.sh (compare) et tools/noms/traduire.py (les générateurs de Cpu/). Une table refusée lève
# TableRefusee, avec le motif.
import re

class TableRefusee(Exception):
    pass

def refuser(m):
    raise TableRefusee(m)

def table_des_noms(chemins, jusqua, depuis, vide_permis=False):
    # La grammaire de tools/noms/g14.txt et g15.txt, celle de tools/noms/TableDesNoms.cs : les étapes, lues à la suite
    # dans les fichiers donnés et bornées par --depuis et --jusqua ; chaque entrée classée (type de premier niveau, type
    # imbriqué, fichier, membre Type::membre, paramètre Type::méthode(p), littéral "a" "b"). Cette lecture refuse une
    # entrée invalide, hors de toute étape, sans changement, ou en double dans son étape ; les collisions d'une portée
    # et CS0542 sont jugées par TableDesNoms.cs (tools/renommage verifier), et pour les types par appliquer().
    seg = r'[A-Za-z_][A-Za-z0-9_]*(?:`[0-9]+)?'
    typ = re.compile(r'^iXtal26(\.' + seg + r')+(\+' + seg + r')*$')
    che = re.compile(r'^iXtal26/[A-Za-z0-9_./-]+\.cs$')
    mem = re.compile(r'^(iXtal26(?:\.' + seg + r')+(?:\+' + seg + r')*)::([A-Za-z_][A-Za-z0-9_]*)'
                     r'(?:\(([A-Za-z_][A-Za-z0-9_]*)\))?$')
    ident = re.compile(r'^[A-Za-z_][A-Za-z0-9_]*$'); lit = re.compile(r'^"[^"\s]*"$')
    etapes, toutes, etape, arret = [], [], None, False
    vue, commence, entrees = jusqua is None, depuis is None, 0
    for chemin in chemins:
        if arret:
            break
        ouverte = False
        for n, l in enumerate(open(chemin, encoding='utf-8'), 1):
            l = l.rstrip()
            e = re.match(r'^//\s+étape\s+(\S+)', l)
            if e:
                if arret:
                    break
                nom = e.group(1)
                if nom in toutes:
                    refuser(f'{chemin}:{n} : l\'étape « {nom} » est ouverte deux fois')
                toutes.append(nom); ouverte = True
                if nom == depuis:
                    commence = True
                etape = {'nom': nom, 'types': {}, 'imbr': {}, 'membres': {}, 'params': {}, 'lits': {},
                         'fichiers': 0, 'cles': set()} if commence else None
                if etape is not None:
                    etapes.append(etape)
                if nom == jusqua:
                    vue = arret = True
                continue
            m = re.match(r'^//   ((?:iXtal26|")\S*)(?:\s+(\S+))?', l)
            if not m:
                continue
            anc, nou = m.group(1), m.group(2) or ''
            if not ouverte:
                refuser(f'{chemin}:{n} : entrée hors de toute étape')
            if anc == nou:
                refuser(f'{chemin}:{n} : « {anc} » ne change pas de nom')
            # Chaque entrée est classée, celles des étapes d'avant --depuis comprises (comme TableDesNoms.cs), puis
            # écartée si son étape n'est pas gardée.
            cible = etape if etape is not None else {'nom': '', 'types': {}, 'imbr': {}, 'membres': {},
                                                       'params': {}, 'lits': {}, 'fichiers': 0, 'cles': set()}
            if anc in cible['cles']:
                refuser(f'{chemin}:{n} : « {anc} » en double dans l\'étape {cible["nom"] or "(non gardée)"}')
            cible['cles'].add(anc)
            if etape is not None:
                entrees += 1
            etape_n = cible
            mm = mem.match(anc)
            if che.match(anc) and che.match(nou):
                etape_n['fichiers'] += 1
            elif lit.match(anc) and lit.match(nou):
                etape_n['lits'][anc[1:-1]] = nou[1:-1]
            elif typ.match(anc) and typ.match(nou):
                if '+' not in anc and '+' not in nou:
                    etape_n['types'][anc] = nou
                elif '+' in anc and '+' in nou and anc.rsplit('+', 1)[0] == nou.rsplit('+', 1)[0]:
                    etape_n['imbr'][anc] = nou.rsplit('+', 1)[1]
                else:
                    refuser(f'{chemin}:{n} : un type imbriqué garde son conteneur dans l\'entrée (« {anc} »)')
            elif mm and ident.match(nou):
                if mm.group(3):
                    etape_n['params'].setdefault(f'{mm.group(1)}::{mm.group(2)}', {})[mm.group(3)] = nou
                else:
                    etape_n['membres'].setdefault(mm.group(1), {})[mm.group(2)] = nou
            else:
                refuser(f'{chemin}:{n} : entrée invalide (« {anc} » → « {nou} »)')
    if not vue:
        refuser(f'l\'étape « {jusqua} » n\'existe pas (étapes : {", ".join(toutes)})')
    if not commence:
        refuser(f'l\'étape « {depuis} » vient après « {jusqua} »' if depuis in toutes
                else f'l\'étape « {depuis} » n\'existe pas (étapes : {", ".join(toutes)})')
    # Un même ancien dans deux étapes gardées : la première l'a renommé, il n'existe plus pour la seconde, sauf si une
    # étape entre elles le redonne (comme TableDesNoms.cs).
    vus = set()
    for e in etapes:
        neufs = set()
        for d in [e['types'], e['imbr'], e['lits']]:
            for a_, n_ in d.items():
                if a_ in vus:
                    refuser(f'« {a_} » est l\'ancien d\'une entrée de l\'étape {e["nom"]}, qu\'une étape d\'avant a '
                            'renommé')
                neufs.add(n_ if d is not e['imbr'] else a_.rsplit('+', 1)[0] + '+' + n_)
        for t_, d in e['membres'].items():
            for a_, n_ in d.items():
                if f'{t_}::{a_}' in vus:
                    refuser(f'« {t_}::{a_} » est l\'ancien d\'une entrée de l\'étape {e["nom"]}, qu\'une étape '
                            'd\'avant a renommé')
                neufs.add(f'{t_}::{n_}')
        vus -= neufs
        vus |= set(e['types']) | set(e['imbr']) | set(e['lits']) | {f'{t_}::{a_}' for t_, d in e['membres'].items()
                                                                    for a_ in d}
    if not entrees and not vide_permis:
        refuser('aucune entrée retenue')
    return etapes
def tr_type(etapes, t):
    # Un nom de type (A.B+C, sous son nom d'avant la première étape) après les étapes, chacune à son tour.
    for e in etapes:
        parts = t.split('+'); res = [e['types'].get(parts[0], parts[0])]; vieux = parts[0]
        for q in parts[1:]:
            vieux += '+' + q; res.append(e['imbr'].get(vieux, q))
        t = '+'.join(res)
    return t
def tr_membre(etapes, t, m):
    # Un membre après les étapes : à chaque étape, le membre (sa clé porte le type du début de l'étape), puis le type.
    for e in etapes:
        d = e['membres'].get(t)
        if d and m in d:
            m = d[m]
        t = tr_type([e], t)
    return m
def tr_litteral(etapes, s):
    for e in etapes:
        s = e['lits'].get(s, s)
    return s
