import re
import os
R=os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..') + '/'
src=open(R+'pcem-dev/includes/private/cpu/x87_ops.h').read().split('\n')
ill='FPU_ILLEGAL_a16'; tables=[]; i=0
while i<len(src):
    l=src[i]
    m=re.match(r'#define ILLEGAL (\w+)',l)
    if m: ill=m.group(1)
    m=re.match(r'OpFn OP_TABLE\((fpu_d[89a-f]_a(16|32))\)\[(\d+)\] = \{(.*)',l)
    if m:
        name,n=m.group(1),int(m.group(3)); start=i+1; txt=m.group(4); j=i
        while '};' not in src[j]: j+=1; txt+=' '+src[j]
        txt=re.sub(r'/\*.*?\*/','',txt.split('};')[0])
        ents=[e.strip() for e in txt.split(',') if e.strip()]
        ents=[ill if e=='ILLEGAL' else e for e in ents]
        assert len(ents)==n,(name,len(ents))
        tables.append((name,n,start,j+1,ents)); i=j
    i+=1
print([ (t[0],t[1]) for t in tables])
# handlers déjà en C#
cs=''
import glob
for f in glob.glob(R+'iXtal26/Cpu/*.cs'):
    if f.endswith('x87_ops_tables.cs'): continue
    cs+=open(f,encoding='utf-8').read()
defined=set(re.findall(r'private static int (\w+)\(uint32_t fetchdat\)',cs))
used=[]; [used.append(e) for t in tables for e in t[4] if e not in used]
missing=[e for e in used if e not in defined]
print(len(used),'handlers distincts dans les tables ;',len(missing),'pas encore transcrits')
L=["""// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/includes/private/cpu/x87_ops.h  (les tables, lignes 310-1040)
// SHA256: voir oracle.tsv ; vérifier avec tools/check-oracle.sh
// STATUS: partial — G4.3 : les seize tables d'échappement non 686 (D8 à DF, a16 et a32),
//         GÉNÉRÉES verbatim depuis le C, emplacement par emplacement, `ILLEGAL` résolu selon
//         le #define en cours (FPU_ILLEGAL_a16 ou _a32). Les tables `_686_` (FCMOV, FCOMI) et
//         nofpu sont hors de portée ou ailleurs (386_ops_fpu.cs).
//
// Les handlers que les tables nomment et qui ne sont pas encore transcrits sont posés en
// fin de fichier sous LEUR NOM, en souches qui s'arrêtent bruyamment (opX87NonTranscrit) :
// G4.4 (x87_ops_misc.h hors transcendantes) et G4.5 (les transcendantes) les remplaceront un
// par un, et les tables, elles, ne bougeront plus.

namespace iXtal26.Cpu;

internal static partial class _386
{"""]
for name,n,a,b,ents in tables:
    L.append('    // pcem: x87_ops.h:%d-%d'%(a,b))
    L.append('    private static OpFn[] Table_%s() =>'%name)
    L.append('    [')
    for k in range(0,n,8):
        L.append('        '+' '.join(e+',' for e in ents[k:k+8]))
    L.append('    ];'); L.append('')
L.append('    // ---- Souches : nommés par les tables, pas encore transcrits (G4.4, G4.5) ----')
for e in missing:
    L.append('    private static int %s(uint32_t fetchdat) => opX87NonTranscrit(fetchdat);'%e)
L.append('}')
open(R+'iXtal26/Cpu/x87_ops_tables.cs','w',encoding='utf-8').write('\n'.join(L)+'\n')
print('souches :',' '.join(missing))
