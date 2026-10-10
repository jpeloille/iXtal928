# G4.6 — l'instanciation 8087 : les handlers générés du cœur 386, recopiés dans _808x ;
# et les tables ops_808x_fpu_*, depuis x87_ops.h (OP_TABLE -> ops_808x_##name, 8087.h:7).
import re
import os
R=os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..') + '/'
bodies=[]
for f in ['x87_ops_loadstore.cs','x87_ops_arith.cs','x87_ops_misc.cs']:
    t=open(R+'iXtal26/Cpu/'+f,encoding='utf-8').read()
    a=t.index('internal static partial class _386\n{')+len('internal static partial class _386\n{')
    b=t.rstrip().rindex('}')
    # G13.1 — PB-54 est sans objet sur le 8087 (pas de limite de segment, CHECK_WRITE vide) : ses
    # marqueurs, d'une ligne chacun, ne sont pas recopiés.
    corps='\n'.join(x for x in t[a:b].rstrip('\n').split('\n') if '// pcem bug, reproduced: PB-54 ' not in x)
    # G13.6 — PB-61 n'est corrigé que sur le 287 et après : le 8087 n'a pas DF E0 (PB-200). Sa copie n'est pas un
    # marqueur.
    corps=corps.replace('// pcem bug, fixed in hardware mode: PB-61 — ', '// Sur le 287 et après, PB-61 (corrigé en mode matériel) — ')
    # G13.6b — de même PB-213, corrigé sur le 387 et le 486 : après une comparaison, le C1 du 8087 est indéfini (287 PRM
    # table 2-6).
    corps=corps.replace('// pcem bug, fixed in hardware mode: PB-213 — ', '// Corrigé sur le 387 et après, PB-213 — ')
    bodies.append('    // ======== depuis %s ========\n'%f + corps)
body='\n\n'.join(bodies)
hdr="""// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/includes/private/cpu/x87_ops_loadstore.h, x87_ops_arith.h, x87_ops_misc.h,
//         recompilés dans l'unité du 808x par 8087.h:86 (`#include "x87_ops.h"`)
// SHA256: voir oracle.tsv ; vérifier avec tools/check-oracle.sh
// STATUS: complete — G4.6 : la SECONDE instanciation des handlers x87, pour le 8087.
//
// GÉNÉRÉ : le corps de x87_ops_loadstore.cs, x87_ops_arith.cs et x87_ops_misc.cs, recopié tel
// quel dans _808x. Chaque nom qui dépend du contexte — FP_ENTER, fetch_ea_*, SEG_CHECK_*,
// CHECK_WRITE, PREFETCH_RUN, readmemw/l/q, writememb/w/l/q, geteaw/l/q, seteaw/l/q, x87_ld80,
// x87_st80 et les aides de FSAVE — se résout d'abord dans _808x (x87_8087.cs, 808x.cs), comme
// le C, où 8087.h les redéfinit avant l'#include ; le reste vient de _386 (`using static`).
// Ne pas éditer à la main : régénérer après toute modification des trois fichiers sources.
// Les marqueurs de PB-54 n'y sont pas recopiés : le 8087 n'a pas de limite (CHECK_WRITE vide).

using static iXtal26.Cpu._386;
using static iXtal26.Cpu._386_common;
using static iXtal26.Cpu.x86;

namespace iXtal26.Cpu;

internal static partial class _808x
{
"""
open(R+'iXtal26/Cpu/x87_ops_808x.cs','w',encoding='utf-8').write(hdr+body+'\n}\n')
# les tables
src=open(R+'pcem-dev/includes/private/cpu/x87_ops.h').read().split('\n')
ill='FPU_ILLEGAL_a16'; tables=[]; i=0
while i<len(src):
    l=src[i]
    m=re.match(r'#define ILLEGAL (\w+)',l)
    if m: ill=m.group(1)
    m=re.match(r'OpFn OP_TABLE\((fpu_d[89a-f]_a16)\)\[(\d+)\] = \{(.*)',l)
    if m:
        name,n=m.group(1),int(m.group(2)); start=i+1; txt=m.group(3); j=i
        while '};' not in src[j]: j+=1; txt+=' '+src[j]
        txt=re.sub(r'/\*.*?\*/','',txt.split('};')[0])
        ents=[e.strip() for e in txt.split(',') if e.strip()]
        ents=[ill if e=='ILLEGAL' else e for e in ents]
        assert len(ents)==n
        tables.append((name,n,start,j+1,ents)); i=j
    i+=1
L=["""// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/includes/private/cpu/x87_ops.h  (les tables a16, lignes 310-1040), sous
//         8087.h:7 — `#define OP_TABLE(name) ops_808x_##name`
// SHA256: voir oracle.tsv ; vérifier avec tools/check-oracle.sh
// STATUS: complete — G4.6 : les huit tables a16 du 8087, générées verbatim ; le 808x n'appelle
//         que les formes a16 (808x.c:3304-3366). Elles nomment les handlers de x87_ops_808x.cs.

namespace iXtal26.Cpu;

internal static partial class _808x
{"""]
# G13.1 — les marqueurs `// pcem bug, reproduced: PB-nn` (PCEM_BUGS.md) : (table, premier indice de la rangée de
# huit) -> lignes posées au-dessus de la rangée. Ces tables ne servent qu'au 8087.
B='// pcem bug, reproduced: '
M52=B+'PB-52 — DF /4 (FBLD m80bcd) est ILLEGAL : rien n\'est chargé ni poussé.'
MARQ={('fpu_df',0x20):[M52], ('fpu_df',0x60):[M52], ('fpu_df',0xa0):[M52],
      ('fpu_df',0xc0):[B+'PB-210 — DF C0-DF (FFREEP et les alias FXCH7, FSTP8, FSTP9) : ILLEGAL.'],
      ('fpu_df',0xe0):[B+'PB-200 — DF E0 (FNSTSW AX, une instruction du 287) écrit AX sur le 8087.'],
      ('fpu_dd',0xc8):[B+'PB-210 — DD C8-CF (FXCH4, alias non documenté de FXCH) : ILLEGAL.'],
      ('fpu_de',0xd0):[B+'PB-210 — DE D0-D7 (FCOMP5, alias non documenté de FCOMP) : ILLEGAL.'],
      ('fpu_dd',0xe0):[B+'PB-209 — FUCOM et FUCOMP (DD E0-EF), du 387, s\'exécutent sur le 8087.'],
      ('fpu_da',0xe8):[B+'PB-209 — FUCOMPP (DA E9), du 387, s\'exécute sur le 8087.'],
      ('fpu_d9',0xf0):[B+'PB-194 — D9 F4 (FXTRACT) est ILLEGAL : la pile ne bouge pas.',
                       B+'PB-209 — FPREM1 (D9 F5), du 387, s\'exécute sur le 8087.'],
      ('fpu_d9',0xf8):[B+'PB-68 — FSINCOS, FSIN et FCOS (D9 FB, FE, FF), du 387, s\'exécutent sur le 8087.']}
for name,n,a,b,ents in tables:
    L.append('    // pcem: x87_ops.h:%d-%d, OP_TABLE(%s) sous 8087.h'%(a,b,name))
    L.append('    internal static readonly OpFn[] ops_808x_%s =' % name)
    L.append('    [')
    for k in range(0,n,8):
        for c in MARQ.get((name[:6],k),[]): L.append('        '+c)
        L.append('        '+' '.join(e+',' for e in ents[k:k+8]))
    L.append('    ];'); L.append('')
L[-1]='}'
open(R+'iXtal26/Cpu/x87_ops_808x_tables.cs','w',encoding='utf-8').write('\n'.join(L)+'\n')
print(len(tables),'tables')
