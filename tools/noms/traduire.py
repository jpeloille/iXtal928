# SPDX-FileCopyrightText: 2026 Julien Peloille
# SPDX-License-Identifier: GPL-2.0-only
#
# ORACLE: (aucun) — outillage de la preuve des membres (G15.0b5, PLAN-G15.md § G15.0b).
#
# traduire.py — la traduction des identifiants d'un texte C# par la table des noms, pour les générateurs de Cpu/
# (tools/regenerer-cpu.sh), qui écrivent du C# par des transformations de texte et ne connaissent que les noms de
# PCem. Hors de la sémantique de Roslyn : un identifiant du code (ni commentaire, ni chaîne, ni caractère ; mais le code
# des trous d'une chaîne interpolée, son format « :X » compris) prend son nouveau nom par son nom simple, étape après
# étape : les types (l'arité d'un générique ôtée), les types imbriqués, les membres, les paramètres ; une chaîne
# simple "…" de la liste close, sans « \ », prend le sien. Les entrées de fichiers ne le concernent pas. C'est une
# traduction par le texte, et sa porte est l'octet : ce que les générateurs régénèrent, traduit, doit égaler à l'octet
# ce que tools/renommage a renommé par les symboles. Une traduction fausse (un nom simple partagé par deux membres,
# dont un seul est renommé) en sort comme un écart.
#
#   python3 traduire.py [--noms TABLE…] [--depuis E] [--jusqua E] [--inverse] FICHIER…   (réécrit en place)
#
# --inverse traduit dans l'autre sens, du nom courant au nom d'avant les étapes, les étapes à rebours : la lecture que
# gentab.py et gen46.py font des sources de Cpu/ attend les noms de PCem. Refusé (retour 2) : une table refusée ; dans
# une étape, un même nom simple vers deux nouveaux (dans le sens direct), ou deux anciens, ou deux littéraux, vers un
# même nouveau (dans le sens inverse). Ce que cette lecture ne voit pas, et que la porte à l'octet jugera : un nom
# simple renommé x → y dans une étape et une autre entité y → z dans la suivante (x devient z) ; un nom qui n'est pas
# renommé mais égale le nouveau d'une entrée, traduit à l'envers ; une chaîne ou un caractère dans un trou interpolé ;
# une chaîne brute, lue d'un bloc.
import os, re, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import table_noms

# Un identifiant ASCII, borné devant : ni dans un littéral numérique (0x1F, 10UL, 1e5), ni au milieu d'un autre mot.
ID = re.compile(r'(?<![A-Za-z0-9_\u0080-\uffff])[A-Za-z_][A-Za-z0-9_]*(?![\u0080-\uffff])')

def morceaux(t):
    # Le texte en morceaux : ('code', …), ('commentaire', …), ('chaine', …), ('car', …), dans l'ordre.
    res, i, debut, n = [], 0, 0, len(t)
    while i < n:
        c = t[i]
        if t.startswith('//', i):
            j = t.find('\n', i); j = n if j < 0 else j; genre = 'commentaire'
        elif t.startswith('/*', i):
            j = t.find('*/', i + 2); j = n if j < 0 else j + 2; genre = 'commentaire'
        elif t.startswith('$"', i) or t.startswith('$@"', i) or t.startswith('@$"', i):
            # Une chaîne interpolée : son texte reste, ses trous {…} sont du code (une chaîne dans un trou, ou un
            # format « :x » après un « : » de premier niveau, ne sont pas lus : la porte à l'octet les jugerait).
            verb = t[i:i + 3] in ('$@"', '@$"'); j = i + (3 if verb else 2)
            if debut < i: res.append(('code', t[debut:i]))
            res.append(('chaine-i', t[i:j])); k = j
            while j < n:
                if t.startswith('{{', j) or t.startswith('}}', j): j += 2; continue
                if t[j] == '{':
                    res.append(('chaine-i', t[k:j + 1])); j += 1; prof, k = 1, j
                    while j < n and prof:
                        if t[j] == '{': prof += 1
                        elif t[j] == '}': prof -= 1
                        if prof: j += 1
                    res.append(('code', t[k:j])); k = j; continue
                if verb and t.startswith('""', j): j += 2; continue
                if not verb and t[j] == '\\': j += 2; continue
                if t[j] == '"': j += 1; break
                j += 1
            res.append(('chaine-i', t[k:j])); i = debut = j; continue
        elif t.startswith('"""', i) or t.startswith('$"""', i):
            # Une chaîne brute : jusqu'au prochain """ (ses trous, s'il y en a, ne sont pas lus).
            j = t.find('"""', t.index('"""', i) + 3); j = n if j < 0 else j + 3
            while j < n and t[j] == '"': j += 1
            genre = 'chaine'
        elif t.startswith('@"', i):
            j = i + 2
            while j < n:
                if t[j] == '"' and t.startswith('""', j): j += 2; continue
                if t[j] == '"': j += 1; break
                j += 1
            genre = 'chaine'
        elif c == '"':
            j = i + 1
            while j < n and t[j] != '"':
                j += 2 if t[j] == '\\' else 1
            j += 1; genre = 'chaine'
        elif c == "'":
            j = i + 1
            while j < n and t[j] != "'":
                j += 2 if t[j] == '\\' else 1
            j += 1; genre = 'car'
        else:
            i += 1; continue
        if debut < i: res.append(('code', t[debut:i]))
        res.append((genre, t[i:j])); i = debut = j
    if debut < n: res.append(('code', t[debut:n]))
    return res

class Traducteur:
    def __init__(self, chemins, jusqua=None, depuis=None):
        self.etapes = table_noms.table_des_noms(chemins, jusqua, depuis, vide_permis=True) if chemins else []
        self.noms, self.lits = [], []
        for e in self.etapes:
            d = {}
            def mettre(a, nv):
                if d.get(a, nv) != nv:
                    raise table_noms.TableRefusee(f'« {a} » prend deux noms dans l\'étape {e["nom"]} ({d[a]}, {nv}) : '
                                                  'la traduction par le nom simple est ambiguë')
                d[a] = nv
            def simple(x): return x.rsplit('.', 1)[-1].rsplit('+', 1)[-1].split('`')[0]
            for a, nv in e['types'].items(): mettre(simple(a), simple(nv))
            for a, nv in e['imbr'].items(): mettre(simple(a), nv.split('`')[0])
            for ms in e['membres'].values():
                for a, nv in ms.items(): mettre(a, nv)
            for ps in e['params'].values():
                for a, nv in ps.items(): mettre(a, nv)
            self.noms.append(d); self.lits.append(dict(e['lits']))
    def _sens(self, inverse):
        if not inverse:
            return self.noms, self.lits
        noms, lits = [], []
        for d, l in zip(reversed(self.noms), reversed(self.lits)):
            r = {}
            for a, nv in d.items():
                if nv in r and r[nv] != a:
                    raise table_noms.TableRefusee(f'« {nv} » vient de deux anciens ({r[nv]}, {a}) : la traduction '
                                                  'inverse est ambiguë')
                r[nv] = a
            rl = {}
            for a, nv in l.items():
                if nv in rl and rl[nv] != a:
                    raise table_noms.TableRefusee(f'"{nv}" vient de deux littéraux ("{rl[nv]}", "{a}") : la traduction '
                                                  'inverse est ambiguë')
                rl[nv] = a
            noms.append(r); lits.append(rl)
        return noms, lits
    def traduire(self, texte, inverse=False):
        noms, lits = self._sens(inverse)
        if not any(noms) and not any(lits):
            return texte, 0
        compte = [0]
        def un(m):
            x = m.group(0)
            for d in noms:
                x = d.get(x, x)
            if x != m.group(0): compte[0] += 1
            return x
        out = []
        for genre, t in morceaux(texte):
            if genre == 'code':
                out.append(ID.sub(un, t))
            elif genre == 'chaine' and t.startswith('"') and not t.startswith('"""') and len(t) >= 2 \
                    and t.endswith('"') and '\\' not in t:
                v = t[1:-1]
                for l in lits:
                    v = l.get(v, v)
                if v != t[1:-1]: compte[0] += 1
                out.append('"' + v + '"')
            else:
                out.append(t)
        return ''.join(out), compte[0]

def main(argv):
    noms, jusqua, depuis, inverse, fichiers, i = [], None, None, False, [], 0
    while i < len(argv):
        a = argv[i]
        if a in ('--noms', '--jusqua', '--depuis') and i + 1 >= len(argv):
            print(f'traduire : {a} demande une valeur', file=sys.stderr); return 2
        if a == '--noms': noms.append(argv[i + 1]); i += 2
        elif a == '--jusqua': jusqua = argv[i + 1]; i += 2
        elif a == '--depuis': depuis = argv[i + 1]; i += 2
        elif a == '--inverse': inverse = True; i += 1
        elif a.startswith('--'): print(f'traduire : option inconnue {a}', file=sys.stderr); return 2
        else: fichiers.append(a); i += 1
    try:
        tr = Traducteur(noms, jusqua, depuis)
        total = 0
        for f in fichiers:
            t = open(f, encoding='utf-8', newline='').read()
            nt, n = tr.traduire(t, inverse)
            if nt != t:
                open(f, 'w', encoding='utf-8', newline='').write(nt)
            total += n
        print(f'  traduire{" (inverse)" if inverse else ""} : {len(fichiers)} fichiers, {total} noms traduits, '
              f'{len(tr.etapes)} étapes')
        return 0
    except table_noms.TableRefusee as e:
        print(f'traduire : table refusée : {e}', file=sys.stderr)
        return 2

if __name__ == '__main__':
    sys.exit(main(sys.argv[1:]))
