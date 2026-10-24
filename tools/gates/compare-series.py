#!/usr/bin/env python3
# SPDX-FileCopyrightText: 2026 Julien Peloille
# SPDX-License-Identifier: GPL-2.0-only
#
# ORACLE: (aucun) — outillage de la preuve des noms (G14.0c, PLAN-G14.md § G14.0c ; R2-noms.md § 1.3, R2-contre.md
# § 1.7).
#
# LA COMPARAISON DE DEUX SÉRIES, journal par journal, le bruit connu retiré. Python 3 de la machine, bibliothèque
# standard seule.
#
#   compare-series.py [OPTIONS] REF.tsv AUTRE.tsv
#   compare-series.py [OPTIONS] --normaliser SERIE.tsv PORTE     le journal normalisé d'une porte, pour lire un écart
#
# Une série est le .tsv de par.sh et ses journaux, SORTIE-PORTE.txt à côté (par.sh:113). Les portes se lisent dans la
# première colonne du .tsv. Pour chaque porte des deux séries : la ligne du .tsv (la colonne de durée ôtée) et le
# journal entier, normalisés, doivent être identiques à l'octet. Une porte présente d'un seul côté est un écart, sauf
# sous --sous-ensemble (AUTRE ne joue qu'une partie de REF : les portes de REF absentes d'AUTRE sont comptées et dites,
# celles d'AUTRE absentes de REF restent des écarts).
#
# Le bruit retiré — rien d'autre ; chaque règle compte ses remplacements, imprimés à la fin :
#   1. la durée : la troisième colonne du .tsv (« 6s »), le temps de l'hôte que par.sh:112 mesure ;
#   2. le répertoire temporaire de la série, /var/tmp/ixtal-par/NOM ou /tmp/ixtal-par/NOM (par.sh:62 ; NOM est le nom
#      du .tsv sans .tsv), ou celui de --tmp : remplacé par <TMP> ;
#   3. le PID dans le nom d'un fichier ou d'un répertoire temporaire, juste sous <TMP> : « nvr-check-3459141 »,
#      « isogen-1954847 » devient « nvr-check-<PID> ». Seulement pour les préfixes que le code écrit avant
#      Environment.ProcessId (PREFIXES_PID ci-dessous, relevés dans iXtal26.Diff et iXtal26 : NvrCheck.cs:20,
#      CdImageCheck.cs:730, BootDiff.cs:630 et :1161…), et seulement si les chiffres finissent le mot (suivis de « - »,
#      « / », « . », « … », d'une espace, d'une ponctuation ou de la fin) : « <TMP>/dump-1 » reste comparé. La ligne
#      d'isogen (« isogen : images écrites dans … ») n'a pas d'autre bruit ;
#   4. le texte coupé après un chemin temporaire : une citation tronquée à 100 caractères (MaterielMode.cs:153,
#      « … ») qui contient <TMP> montre d'autant moins de texte que le chemin et le PID sont longs. Les deux citations
#      d'une même ligne sont comparées sur leur partie visible commune : celle qui en montre le plus est coupée au
#      nombre de caractères de l'autre, les marques <TMP> et <PID> ne comptant pas ; le reste est comparé à l'octet ;
#   5. le temps de l'hôte du bloc « 7. HÔTE (NON DÉTERMINISTE…) » de timer-check (TimerCheck.cs:411-414) : le temps
#      de la fenêtre, la marge et la capacité, dont le nombre devient <HÔTE> ;
#   6. sous --racine R1 R2 et --work W1 W2 : le dépôt joué (REPO) et l'espace des portes disque (WORK, g5w-recipe.sh)
#      de chaque côté, remplacés par <RACINE> et <WORK>, plus longs d'abord ;
#   7. la longueur d'une séance de banc, « les deux séances sont identiques (N caractères) » (Banc.cs:95) : la séance
#      cite le répertoire temporaire de la série, N bouge avec la longueur de son nom. N n'est remplacé par <LONGUEUR>
#      que si les répertoires temporaires des deux séries n'ont pas la même longueur ; sinon il reste comparé.
# Un chemin absolu que ces règles ne couvrent pas reste dans le texte, et sort en écart s'il diffère.
#
# Retour : 0 identiques ; 1 des écarts ; 2 usage, ou série illisible (un .tsv qui nomme deux fois la même porte est
# refusé, jamais écrasé).

import argparse
import os
import re
import sys

BRUIT = ['durée', 'répertoire de la série', 'PID', 'texte coupé', 'temps de l\'hôte', 'racine et work',
         'longueur de séance']
HOTE = [re.compile(r'^(\s*temps hôte de la fenêtre : )\S+( s)$'),
        re.compile(r'^(\s*marge   = )\S+( \(s invitées par s hôte)'),
        re.compile(r'^(\s*capacité = )\S+( MHz de cycles émulés par seconde hôte)')]
PREFIXES_PID = ['isogen', 'ixtal-boot', 'ixtal-boot-oracle', 'ixtal-svga-linear', 'ixtal26-fat', 'materiel-cas',
                'materiel-cas-aha', 'materiel-cas-cd', 'materiel-mode', 'nvr-check', 'r9-aha', 'r9-atapi', 'r9-awecfg',
                'r9-cdcfg', 'r9-disquette', 'r9-emu8k', 'r9-filet', 'r9-gfxfixe', 'r9-hdccfg', 'r9-joycfg',
                'r9-portscfg', 'r9-sb16', 'r9-sbcfg', 'r9-videocfg', 'r9-xebec', 'r9-zip', 'reset-scsi', 'setup-check',
                'setup-check-ar', 'setup-check-roms', 'ixtal-csharp-a', 'ixtal-csharp-b', 'ixtal-csharp-zip',
                'ixtal-oracle-a', 'ixtal-oracle-b', 'ixtal-oracle-zip', 'ixtal-p2-csharp-a', 'ixtal-p2-csharp-b',
                'ixtal-p2-csharp-zip', 'ixtal-p2-oracle-a', 'ixtal-p2-oracle-b', 'ixtal-p2-oracle-zip']
PID = re.compile(r'(<TMP>/(?:' + '|'.join(re.escape(x) for x in sorted(PREFIXES_PID, key=len, reverse=True))
                 + r'))-\d+(?=[-/.…\s»,;:)]|$)')
COUPE = re.compile(r'«([^«»\n]*?)(<TMP>[^«»\n]*)… »')
MARQUE = re.compile(r'<TMP>|<PID>')
LONGUEUR = re.compile(r'^(  les deux séances sont identiques \()\d+( caractères\))$', re.M)


class Serie:
    def __init__(self, tsv, tmp, racine, work):
        if not tsv.endswith('.tsv') or not os.path.isfile(tsv):
            echec(f'compare-series : {tsv} n\'est pas le .tsv d\'une série')
        self.tsv = tsv
        self.prefixe = tsv[:-4]
        self.nom = os.path.basename(self.prefixe)
        tmps = [tmp] if tmp else [f'/var/tmp/ixtal-par/{self.nom}', f'/tmp/ixtal-par/{self.nom}']
        self.longueur_tmp = len(tmp) if tmp else len(self.nom)
        self.longueur_libre = False
        self.tmp = re.compile('(?:' + '|'.join(re.escape(t) for t in tmps) + r')(?![\w.-])')
        self.chemins = sorted([(c, '<RACINE>') for c in racine] + [(c, '<WORK>') for c in work],
                              key=lambda x: -len(x[0]))
        self.lignes = {}
        self.ordre = []
        for l in open(tsv, encoding='utf-8', errors='surrogateescape'):
            l = l.rstrip('\n')
            if l == 'FIN' or not l:
                continue
            champs = l.split('\t')
            if champs[0] in self.lignes:
                echec(f'compare-series : {tsv} nomme deux fois la porte {champs[0]} ; refusé')
            self.ordre.append(champs[0])
            self.lignes[champs[0]] = champs

    def normaliser(self, texte, compte):
        for c, r in self.chemins:
            n = texte.count(c)
            if n:
                texte = texte.replace(c, r)
                compte[5] += n
        texte, n = self.tmp.subn('<TMP>', texte)
        compte[1] += n
        texte, n = PID.subn(r'\1-<PID>', texte)
        compte[2] += n
        if self.longueur_libre:
            texte, n = LONGUEUR.subn(r'\g<1><LONGUEUR>\g<2>', texte)
            compte[6] += n
        sortie = []
        for l in texte.split('\n'):
            for h in HOTE:
                l, n = h.subn(r'\g<1><HÔTE>\g<2>', l)
                compte[4] += n
            sortie.append(l)
        return '\n'.join(sortie)

    def ligne(self, porte, compte):
        c = list(self.lignes[porte])
        if len(c) >= 3:
            del c[2]
            compte[0] += 1
        return self.normaliser('\t'.join(c), compte)

    def journal(self, porte, compte):
        f = f'{self.prefixe}-{porte}.txt'
        if not os.path.isfile(f):
            return None
        with open(f, encoding='utf-8', errors='surrogateescape') as h:
            return self.normaliser(h.read(), compte)


def echec(m):
    print(m, file=sys.stderr)
    sys.exit(2)


def visibles(t):
    return len(MARQUE.sub('', t))


def couper(t, n):
    # les n premiers caractères visibles de t, les marques gardées là où elles tombent
    sortie, i = [], 0
    while i < len(t) and n > 0:
        m = MARQUE.match(t, i)
        if m:
            sortie.append(m.group(0)); i = m.end(); continue
        sortie.append(t[i]); i += 1; n -= 1
    while i < len(t):
        m = MARQUE.match(t, i)
        if not m:
            break
        sortie.append(m.group(0)); i = m.end()
    return ''.join(sortie)


def aligner(a, b, compte):
    # La règle 4 : les citations tronquées d'une même ligne, ramenées à leur partie visible commune.
    la, lb = a.split('\n'), b.split('\n')
    if len(la) != len(lb):
        return a, b
    for i, (x, y) in enumerate(zip(la, lb)):
        cx, cy = list(COUPE.finditer(x)), list(COUPE.finditer(y))
        if not cx or len(cx) != len(cy):
            continue
        for mx, my in reversed(list(zip(cx, cy))):
            n = min(visibles(mx.group(2)), visibles(my.group(2)))
            nx, ny = couper(mx.group(2), n), couper(my.group(2), n)
            if nx != mx.group(2) or ny != my.group(2):
                compte[3] += 1
            x = x[:mx.start(2)] + nx + x[mx.end(2):]
            y = y[:my.start(2)] + ny + y[my.end(2):]
        la[i], lb[i] = x, y
    return '\n'.join(la), '\n'.join(lb)


def premier_ecart(a, b):
    la, lb = a.split('\n'), b.split('\n')
    for i in range(max(len(la), len(lb))):
        x = la[i] if i < len(la) else '(fin)'
        y = lb[i] if i < len(lb) else '(fin)'
        if x != y:
            return i + 1, x, y
    return 0, '', ''


def main():
    p = argparse.ArgumentParser(add_help=True)
    p.add_argument('--normaliser', action='store_true')
    p.add_argument('--sous-ensemble', action='store_true')
    p.add_argument('--tmp', nargs=2, metavar=('REF', 'AUTRE'))
    p.add_argument('--racine', nargs=2, metavar=('REF', 'AUTRE'))
    p.add_argument('--work', nargs=2, metavar=('REF', 'AUTRE'))
    p.add_argument('serie')
    p.add_argument('autre')
    a = p.parse_args()
    compte = [0] * len(BRUIT)
    if a.normaliser:
        s = Serie(a.serie, a.tmp[0] if a.tmp else None, [a.racine[0]] if a.racine else [],
                  [a.work[0]] if a.work else [])
        j = s.journal(a.autre, compte)
        if j is None:
            echec(f'compare-series : pas de journal {s.prefixe}-{a.autre}.txt')
        sys.stdout.write(j)
        return 0
    r = Serie(a.serie, a.tmp[0] if a.tmp else None, [a.racine[0]] if a.racine else [],
              [a.work[0]] if a.work else [])
    o = Serie(a.autre, a.tmp[1] if a.tmp else None, [a.racine[1]] if a.racine else [],
              [a.work[1]] if a.work else [])
    if r.longueur_tmp != o.longueur_tmp:
        r.longueur_libre = o.longueur_libre = True
    communes = [x for x in r.ordre if x in o.lignes]
    seules_r = [x for x in r.ordre if x not in o.lignes]
    seules_o = [x for x in o.ordre if x not in r.lignes]
    ecarts = []
    for porte in communes:
        lr, lo = aligner(r.ligne(porte, compte), o.ligne(porte, compte), compte)
        if lr != lo:
            ecarts.append((porte, 'ligne du .tsv', lr, lo))
            continue
        jr, jo = r.journal(porte, compte), o.journal(porte, compte)
        if jr is not None and jo is not None:
            jr, jo = aligner(jr, jo, compte)
        if jr is None or jo is None:
            ecarts.append((porte, 'journal absent', 'présent' if jr is not None else 'absent',
                           'présent' if jo is not None else 'absent'))
        elif jr != jo:
            n, x, y = premier_ecart(jr, jo)
            ecarts.append((porte, f'journal, ligne {n}', x, y))
    print(f'  REF {r.nom} : {len(r.ordre)} portes ; AUTRE {o.nom} : {len(o.ordre)} portes ; {len(communes)} '
          f'communes, comparées ; {len(ecarts)} en écart')
    if seules_r:
        print(f'  {len(seules_r)} portes de REF absentes d\'AUTRE'
              + (' (sous-ensemble : dites, non comptées)' if a.sous_ensemble else ' : écart'))
    if seules_o:
        print(f'  {len(seules_o)} portes d\'AUTRE absentes de REF : écart')
    for porte, quoi, x, y in ecarts:
        print(f'  écart  {porte} ({quoi})')
        print(f'      REF   : {x[:200]}')
        print(f'      AUTRE : {y[:200]}')
    if not a.sous_ensemble:
        for x in seules_r:
            print(f'  seule dans REF    {x}')
    for x in seules_o:
        print(f'  seule dans AUTRE  {x}')
    print('  bruit retiré : ' + ' ; '.join(f'{n} × {c}' for c, n in zip(BRUIT, compte)))
    mauvais = ecarts or seules_o or (seules_r and not a.sous_ensemble)
    return 1 if mauvais else 0


if __name__ == '__main__':
    sys.exit(main())
