import re, sys
import os
R=os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..') + '/'
ar=open(R+'pcem-dev/includes/private/cpu/x87_ops_arith.h').read().split('\n')
# ---- 1. l'expansion de opFPU (lignes 3-112) ----
mac=ar[2:112]                               # lignes 3..112
body='\n'.join(l.rstrip().rstrip('\\').rstrip() for l in mac[1:])
inst=[('s','x87_ts',16,'t.i','geteal','t.s','_32'),('s','x87_ts',32,'t.i','geteal','t.s','_32'),
      ('d','x87_td',16,'t.i','geteaq','t.d','_64'),('d','x87_td',32,'t.i','geteaq','t.d','_64'),
      ('iw','uint16_t',16,'t','geteaw','(double)(int16_t)t','_i16'),('iw','uint16_t',32,'t','geteaw','(double)(int16_t)t','_i16'),
      ('il','uint32_t',16,'t','geteal','(double)(int32_t)t','_i32'),('il','uint32_t',32,'t','geteal','(double)(int32_t)t','_i32')]
USE={'t.s':'t.s','t.d':'t.d','(double)(int16_t)t':'(double)unchecked((int16_t)t)','(double)(int32_t)t':'(double)unchecked((int32_t)t)'}
DECL={'x87_ts':'x87_ts t = default;','x87_td':'x87_td t = default;','uint16_t':'uint16_t t;','uint32_t':'uint32_t t;'}
funcs=[]
for name,optype,asz,lv,get,use,cp in inst:
    b=body.replace('##name##',name).replace('_a##a_size','_a'+str(asz))
    b=b.replace('fetch_ea_##a_size','fetch_ea_'+str(asz)).replace('##cycle_postfix',cp)
    assert '##' not in b, b[:200]
    for m in re.finditer(r'static int (\w+)\(uint32_t fetchdat\) \{\n(.*?)\n        \}',b,re.S):
        fn=m.group(1).replace('##','')
        lines=[x[8:] if x.startswith('        ') else x for x in m.group(2).split('\n')]
        funcs.append((fn,'mem',lines,optype,lv,get,use,'x87_ops_arith.h:3-120, opFPU('+name+', '+optype+', '+str(asz)+')'))
# ---- 2. les formes registre (lignes 122-452) ----
txt='\n'.join(ar)
for m in re.finditer(r'static int (\w+)\(uint32_t fetchdat\) \{\n(.*?)\n\}',txt[txt.find('static int opFADD(uint32_t'):],re.S):
    fn=m.group(1)
    if fn in ('opFCOMI','opFCOMIP','opFUCOMI','opFUCOMIP'): continue
    start=txt.find('static int %s(uint32_t fetchdat)'%fn); ln=txt[:start].count('\n')+1
    funcs.append((fn,'reg',m.group(2).split('\n'),None,None,None,None,'x87_ops_arith.h:%d'%ln))
# G13.1 — les marqueurs `// pcem bug, reproduced: PB-nn` (PCEM_BUGS.md), posés ici et nulle part à la main.
M48='// pcem bug, reproduced: PB-48 — RC ne vaut que pour ce FADD mémoire, par x87_fadd_dirige :'
M48b='//   fesetround(rounding_modes[RC]) ; ST(0) += use_var ; fesetround(FE_TONEAREST) (x87_ops_arith.h:12-16).'
M60='// pcem bug, reproduced: PB-60 — le NaN qui survit suit l\'ordre des opérandes que GCC a choisi.'
MC1='// pcem bug, fixed in hardware mode: PB-213 — C1 n\'est pas remis à zéro (387 et suivants ; x87.Materiel.cs).'
def conv(fn,kind,lines,optype,lv,get,use):
    out=[]; k=0
    while k<len(lines):
        l=lines[k]; s=l.strip()
        if s.startswith('if (fplog)'):
            k+=1
            while not lines[k].rstrip().endswith(';'): k+=1
            k+=1; continue
        if s=='': out.append(''); k+=1; continue
        r=s
        if kind=='mem':
            r=r.replace('optype t;',DECL[optype])
            if r=='load_var = get();':
                r={'t.i/geteal':'t.i = geteal();','t.i/geteaq':'t.i = geteaq();','t/geteaw':'t = geteaw();','t/geteal':'t = geteal();'}[lv+'/'+get]
            r=r.replace('(double)use_var','(double)'+USE[use] if not USE[use].startswith('(double)') else USE[use]).replace('use_var',USE[use])
            if r.startswith('if ((cpu_state.npxc >> 10) & 3)') and lines[k+1].strip().startswith('fesetround(rounding_modes'):
                # le bloc de quatre lignes de PB-48
                assert lines[k+2].strip()=='ST(0) += use_var;' and lines[k+4].strip()=='fesetround(FE_TONEAREST);'
                ind=' '*8
                out+=[ind+M48,
                      ind+M48b,
                      ind+'if (((cpu_state.npxc >> 10) & 3) != 0)',
                      ind+'        ST(0) = x87_fadd_dirige(%s, ST(0), (cpu_state.npxc >> 10) & 3);'%USE[use],
                      ind+'else',
                      ind+'        '+M60,
                      ind+'        ST(0) = X87AddSd(%s, ST(0)); // PB-60 : la mémoire en premier'%USE[use]]
                k+=5; continue
        if kind=='mem' and r=='ST(0) *= %s;'%USE[use]:
            r='ST(0) = X87MulSd(%s, ST(0)); // PB-60 : la mémoire en premier'%USE[use]
        # PB-60 — l'ordre des opérandes commutatifs que GCC a choisi, mesuré par x87-nan-order.
        NAN={('opFADD','ST(0) = ST(0) + ST(fetchdat & 7);'):'ST(0) = X87AddSd(ST((int)(fetchdat & 7)), ST(0)); // PB-60 : ST(i) en premier',
             ('opFADDr','ST(fetchdat & 7) = ST(fetchdat & 7) + ST(0);'):'ST((int)(fetchdat & 7)) = X87AddSd(ST(0), ST((int)(fetchdat & 7))); // PB-60 : ST(0) en premier',
             ('opFADDP','ST(fetchdat & 7) = ST(fetchdat & 7) + ST(0);'):'ST((int)(fetchdat & 7)) = X87AddSd(ST((int)(fetchdat & 7)), ST(0)); // PB-60 : ST(i) en premier',
             ('opFMUL','ST(0) = ST(0) * ST(fetchdat & 7);'):'ST(0) = X87MulSd(ST((int)(fetchdat & 7)), ST(0)); // PB-60 : ST(i) en premier',
             ('opFMULr','ST(fetchdat & 7) = ST(0) * ST(fetchdat & 7);'):'ST((int)(fetchdat & 7)) = X87MulSd(ST(0), ST((int)(fetchdat & 7))); // PB-60 : ST(0) en premier',
             ('opFMULP','ST(fetchdat & 7) = ST(0) * ST(fetchdat & 7);'):'ST((int)(fetchdat & 7)) = X87MulSd(ST(0), ST((int)(fetchdat & 7))); // PB-60 : ST(0) en premier'}
        if (fn,r) in NAN: r=NAN[(fn,r)]
        r=r.replace('FP_ENTER();','if (FP_ENTER()) return 1;')
        r=re.sub(r'^fetch_ea_(16|32)\(fetchdat\);',r'if (fetch_ea_\1(fetchdat)) return 1;',r)
        r=r.replace('SEG_CHECK_READ(cpu_state.ea_seg);','if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;')
        r=r.replace('if (cpu_state.abrt)','if (cpu_state.abrt != 0)')
        r=r.replace('CLOCK_CYCLES(x87_timings.','CLOCK_CYCLES(x87_timings_c.x87_timings.')
        r=r.replace('cpu_state.npxs &= ~(C0 | C2 | C3);','cpu_state.npxs &= unchecked((uint16_t)~(x87_c.C0 | x87_c.C2 | x87_c.C3));')
        r=r.replace('cpu_state.npxs |= C3;','cpu_state.npxs |= x87_c.C3;').replace('cpu_state.npxs |= C0; /*Nasty hack to fix 80387 detection*/','cpu_state.npxs |= x87_c.C0; /*Nasty hack to fix 80387 detection*/').replace('cpu_state.npxs |= C0;','cpu_state.npxs |= x87_c.C0;')
        r=r.replace('TAG_VALID','x87_c.TAG_VALID')
        r=r.replace('ST(fetchdat & 7)','ST((int)(fetchdat & 7))')
        r=r.replace('cpu_state.tag[(cpu_state.TOP + fetchdat) & 7]','cpu_state.tag[(cpu_state.TOP + (int)fetchdat) & 7]')
        m=re.match(r'x87_div\((.*?), (.*), (.*)\);$',r)
        if m:
            r='if (x87_div(ref %s, %s, %s)) return 1;'%(m.group(1),m.group(2),m.group(3))
        if 'Nasty hack' not in r and '*(uint64_t *)&ST(0)' in r:
            out.append(' '*8+'// pcem bug, fixed in hardware mode: PB-58 — −0 contre +0 rend C0 (« plus petit »), pas C3 ; en mode')
            out.append(' '*8+'//   matériel, la table du mode porte le gestionnaire corrigé (x87.Materiel.cs).')
            r=r.replace('*(uint64_t *)&ST(0)','BitConverter.DoubleToUInt64Bits(ST(0))').replace('*(uint64_t *)&ST(1)','BitConverter.DoubleToUInt64Bits(ST(1))')
        if fn=='opFCOM' and r.startswith('if (ST(0) == ST('):
            out.append(' '*8+'// pcem bug, fixed in hardware mode: PB-57 — `==` et `<` du C, pas x87_compare : un NaN rend')
            out.append(' '*8+'//   « plus grand » (C3 = C2 = C0 = 0) au lieu de « non ordonné » ; en mode matériel, la table du')
            out.append(' '*8+'//   mode porte le gestionnaire corrigé (x87.Materiel.cs).')
        if re.search(r'\b(C0|C2|C3)\b',r) and 'x87_c.' not in r: sys.exit('C? non traduit: '+r)
        if 'use_var' in r or 'load_var' in r or 'optype' in r or 'fesetround' in r: sys.exit('reste: '+r)
        # G13.1 — les marqueurs canoniques, une ligne de commentaire au-dessus du site.
        if 'X87AddSd(' in r or 'X87MulSd(' in r:
            out.append(' '*8+M60)
        if r=='cpu_state.npxs &= unchecked((uint16_t)~(x87_c.C0 | x87_c.C2 | x87_c.C3));':
            out.append(' '*8+MC1)
        ind=' '*(len(l)-len(l.lstrip()))
        out.append(ind+r if ind else '        '+r); k+=1
    return out
L=["""// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/includes/private/cpu/x87_ops_arith.h  (lignes 1-452)
// SHA256: voir oracle.tsv ; vérifier avec tools/check-oracle.sh
// STATUS: partial — G4.3 : les quatre-vingt-huit handlers d'arithmétique que les tables non
//         686 référencent. La macro opFPU (:3-112) est EXPANSÉE pour ses huit instanciations
//         (:114-120) — FADD, FCOM, FCOMP, FDIV, FDIVR, FMUL, FSUB, FSUBR × s, d, iw, il × a16,
//         a32 : 64 handlers —, puis les 24 formes registre. FCOMI, FCOMIP, FUCOMI et FUCOMIP
//         (:209-236, :421-449) ne sont que dans les tables `_686_` : omis, hors de portée.
//         Comptés après `gcc -E` de l'unité de l'oracle : 92 handlers, 16 appels fesetround.
//
// GÉNÉRÉ par règles depuis le C (voir x87_ops_loadstore.cs), et trois écarts nommés :
//   - x87_div (macro, `return 1` du handler) devient `if (x87_div(ref dst, a, b)) return 1;` ;
//   - le bloc fesetround des opFADD mémoire devient x87_fadd_dirige (PB-48, DEVIATION) ;
//   - x87_compare / x87_ucompare, de l'asm x87 hôte chez PCem, sont leur sémantique (DEVIATION).
// Quatre défauts de PCem reproduits : PB-57 (opFCOM), PB-58 (opFCOMPP), PB-59 (x87_div) et
// PB-60 (le NaN propagé suit l'ordre des opérandes que GCC a choisi : X87AddSd, X87MulSd) ; PB-57 et
// PB-58 corrigés en mode matériel, dans les tables du mode (x87.Materiel.cs).

using static iXtal26.Cpu._386_common;
using static iXtal26.Cpu.x86;

namespace iXtal26.Cpu;

internal static partial class _386
{"""]
for fn,kind,lines,optype,lv,get,use,cite in funcs:
    L.append('    // pcem: '+cite)
    L.append('    private static int %s(uint32_t fetchdat)'%fn)
    L.append('    {')
    for l in conv(fn,kind,lines,optype,lv,get,use):
        L.append(('    '+l) if l else '')
    L.append('    }'); L.append('')
L[-1]='}'
open(R+'iXtal26/Cpu/x87_ops_arith.cs','w',encoding='utf-8').write('\n'.join(L)+'\n')
print(len(funcs),'handlers générés')
