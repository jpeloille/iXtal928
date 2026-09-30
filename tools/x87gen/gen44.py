import re, sys
import os
R=os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..') + '/'
src=open(R+'pcem-dev/includes/private/cpu/x87_ops_misc.h').read()
lines=src.split('\n')
SKIP=set()  # G4.5 : les transcendantes entrent
funcs=[]
i=0
while i<len(lines):
    m=re.match(r'static int (\w+)\((uint32_t fetchdat)?\) \{',lines[i])
    if m:
        j=i
        while lines[j]!='}': j+=1
        funcs.append((m.group(1),m.group(2) is not None,i+1,j+1,lines[i+1:j]))
        i=j
    i+=1
def conv(fn,body):
    out=[]; k=0
    while k<len(body):
        l=body[k]; s=l.strip()
        if s.startswith('if (fplog)'):
            k+=1
            while not body[k].rstrip().endswith(';'): k+=1
            k+=1; continue
        if s=='': out.append(''); k+=1; continue
        ind=' '*(len(l)-len(l.lstrip()))
        if s.startswith('/*') and not s.endswith('*/'):
            # commentaire multi-ligne
            while True:
                out.append(ind+'// '+s.replace('/*','').replace('*/','').strip())
                if s.endswith('*/'): break
                k+=1; s=body[k].strip()
            k+=1; continue
        if s.startswith('codegen_set_rounding_mode('):
            out.append(ind+'// omitted: codegen_set_rounding_mode(...) — le dynarec (souche vide dans l\'oracle).'); k+=1; continue
        r=s
        r=r.replace('FP_ENTER();','if (FP_ENTER()) return 1;')
        r=re.sub(r'^fetch_ea_(16|32)\(fetchdat\);',r'if (fetch_ea_\1(fetchdat)) return 1;',r)
        r=r.replace('SEG_CHECK_READ(cpu_state.ea_seg);','if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;')
        r=r.replace('SEG_CHECK_WRITE(cpu_state.ea_seg);','if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;')
        r=r.replace('if (cpu_state.abrt)','if (cpu_state.abrt != 0)')
        r=r.replace('if (cpu_state.ismmx)','if (cpu_state.ismmx != 0)')
        r=r.replace('CLOCK_CYCLES(x87_timings.','CLOCK_CYCLES(x87_timings_c.x87_timings.')
        r=r.replace('fpu_type == FPU_8087','cpu_c.fpu_type == cpu_c.FPU_8087')
        r=r.replace('cpu_state.npxc &= ~FPCW_DISI;','cpu_state.npxc &= unchecked((uint16_t)~FPCW_DISI);')
        r=r.replace('*(uint64_t *)cpu_state.tag = 0;','Array.Clear(cpu_state.tag);')
        r=r.replace('(*(uint64_t *)cpu_state.tag == 0x0101010101010101ull))','TagsTousValides())')
        r=r.replace('!cpu_state.TOP &&','cpu_state.TOP == 0 &&')
        r=r.replace('cpu_state.npxs &= ~(C0 | C1 | C2 | C3);','cpu_state.npxs &= unchecked((uint16_t)~(x87_c.C0 | x87_c.C1 | x87_c.C2 | x87_c.C3));')
        r=r.replace('cpu_state.npxs &= ~(C0 | C2 | C3);','cpu_state.npxs &= unchecked((uint16_t)~(x87_c.C0 | x87_c.C2 | x87_c.C3));')
        r=r.replace('cpu_state.npxs |= (C0 | C3);','cpu_state.npxs |= x87_c.C0 | x87_c.C3;')
        for c in ['C0','C1','C2','C3']:
            r=r.replace('cpu_state.npxs |= %s;'%c,'cpu_state.npxs |= x87_c.%s;'%c)
        r=r.replace('TAG_EMPTY','x87_c.TAG_EMPTY').replace('TAG_VALID','x87_c.TAG_VALID')
        r=r.replace('cpu_state.tag[(cpu_state.TOP + fetchdat) & 7]','cpu_state.tag[(cpu_state.TOP + (int)fetchdat) & 7]')
        r=r.replace('cpu_state.MM[(cpu_state.TOP + fetchdat) & 7]','cpu_state.MM[(cpu_state.TOP + (int)fetchdat) & 7]')
        r=r.replace('ST(fetchdat & 7)','ST((int)(fetchdat & 7))')
        r=r.replace('cpu_state.tag[cpu_state.TOP & 7] = old_tag;','cpu_state.tag[cpu_state.TOP & 7] = (uint8_t)old_tag;')
        # G4.5 — la libm : Math.* rend les bits de la glibc de l'oracle (x87-parity, G4.0).
        r=re.sub(r'(?<![\w.])(pow|log|tan|atan2|sin|cos)\(', lambda m: 'Math.'+{'pow':'Pow','log':'Log','tan':'Tan','atan2':'Atan2','sin':'Sin','cos':'Cos'}[m.group(1)]+'(', r)
        r=r.replace('cpu_state.npxs &= ~C2;','cpu_state.npxs &= unchecked((uint16_t)~x87_c.C2);')
        if fn in ('opFPTAN','opFSIN','opFCOS','opFSINCOS') and r.startswith('cpu_state.npxs &= unchecked((uint16_t)~x87_c.C2)'):
            out.append(ind+'// pcem bug, reproduced: PB-68 — C2 toujours effacé : pas de borne |x| < 2^63, la libm')
            out.append(ind+'//   réduit tout argument, et « réduction incomplète » n\'est jamais signalée.')
        r=r.replace('fabs(ST(0))','Math.Abs(ST(0))').replace('sqrt(ST(0))','Math.Sqrt(ST(0))').replace('pow(2.0,','Math.Pow(2.0,')
        r=r.replace('temp64 = (int64_t)(ST(0) / ST(1));','temp64 = CvtI64(ST(0) / ST(1));').replace('temp64 = (int64_t)ST(1);','temp64 = CvtI64(ST(1));')
        r=re.sub(r'if \(temp64 & (\d)\)',r'if ((temp64 & \1) != 0)',r)
        r=r.replace('x87_push_u64(0x3fe62e42fefa39f0ull);','x87_push_u64(0x3fe62e42fefa39f0UL);')
        r=r.replace('x87_gettag()','x87_c.x87_gettag()').replace('x87_settag(','x87_c.x87_settag(')
        r=re.sub(r'\bx87_(pc|op)_(off|seg)\b',r'x87_c.x87_\1_\2',r)
        r=r.replace('cpu_state.npxs = (cpu_state.npxs & ~(7 << 11)) | ((cpu_state.TOP & 7) << 11);','cpu_state.npxs = (uint16_t)((cpu_state.npxs & ~(7 << 11)) | ((cpu_state.TOP & 7) << 11));')
        r=r.replace('seteaw((cpu_state.npxs & 0xC7FF) | ((cpu_state.TOP & 7) << 11));','seteaw((uint16_t)((cpu_state.npxs & 0xC7FF) | ((cpu_state.TOP & 7) << 11)));')
        m=re.match(r'writememw\(easeg, (.*?), (x87_c\.x87_\w+)\);$',r)
        if m: r='writememw(easeg, %s, (uint16_t)%s);'%(m.group(1),m.group(2))
        r=r.replace('writememl(easeg, cpu_state.eaaddr + 24, (x87_c.x87_op_off >> 16) << 12);','writememl(easeg, cpu_state.eaaddr + 24, (x87_c.x87_op_off >> 16) << 12);')
        r=re.sub(r'writememl\(easeg, (cpu_state\.eaaddr \+ \d+), (x87_c\.x87_\w+_seg)\);',r'writememl(easeg, \1, (uint32_t)\2);',r)
        r=r.replace('FSTOR();','_ = FSTOR();').replace('FSAVE();','_ = FSAVE();').replace('FLDENV();','_ = FLDENV();').replace('FSTENV();','_ = FSTENV();')
        # repère des défauts
        if fn=='opFSTSW_AX' and r=='AX = cpu_state.npxs;':
            out.append(ind+'// pcem bug, reproduced: PB-61 — npxs BRUT : sans les trois bits de TOP que la forme')
            out.append(ind+'//   mémoire (opFSTSW_a16) y compose.')
        if fn in ('opFST','opFSTP') and r.startswith('cpu_state.tag[(cpu_state.TOP + (int)fetchdat)'):
            out.append(ind+'// pcem bug, reproduced: PB-67 — le tag est copié, TAG_UINT64 compris, mais pas MM[].q.')
        if fn=='opFXAM' and r.startswith('cpu_state.npxs &='):
            out.append(ind+'// pcem bug, reproduced: PB-63 — trois classes seulement : vide, zéro, « normal ».')
        if fn=='opFTST' and r.startswith('if (ST(0) == 0.0)'):
            out.append(ind+'// pcem bug, reproduced: PB-64 — un NaN rend « plus grand », pas « non ordonné ».')
        if fn in ('opFPREM','opFPREM1') and r.startswith('temp64 = '):
            out.append(ind+'// pcem bug, reproduced: PB-65 — un quotient tronqué d\'un coup, C2 jamais posé, et')
            out.append(ind+'//   FPREM1 identique à FPREM.')
        if fn=='opFLDLN2':
            out.append(ind+'// pcem bug, reproduced: PB-66 — ln 2 d\'un ulp au-dessus du double le plus proche.') if r.startswith('x87_push_u64') else None
        if 'pclog' in r or 'fplog' in r: sys.exit('reste log : '+r)
        if re.search(r'\bC[0-3]\b',r) and 'x87_c.' not in r: sys.exit('C? : '+r)
        if re.search(r'\bFPU_8087\b',r) and 'cpu_c.' not in r: sys.exit('FPU : '+r)
        out.append(ind+r if ind else '        '+r); k+=1
    return out
L=["""// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/includes/private/cpu/x87_ops_misc.h  (lignes 1-906)
// SHA256: voir oracle.tsv ; vérifier avec tools/check-oracle.sh
// STATUS: partial — G4.4 et G4.5 : tout x87_ops_misc.h sauf FCMOV (:907-926, tables 686).
//         Les huit transcendantes (G4.5) passent par Math.*, dont la parité au bit avec la
//         glibc de l'oracle est mesurée (x87-parity, G4.0) — expressions de PCem comprises. La pile, les constantes, FCHS, FABS, FTST, FXAM,
//         FSTSW, FSTCW, FLDCW, FNINIT, FNCLEX, FDISI, FENI, FSTENV, FLDENV, FSAVE, FRSTOR en
//         16 et 32 bits, mode réel et protégé, FPREM, FPREM1, FSQRT, FRNDINT, FSCALE.
//
// GÉNÉRÉ par règles depuis le C, comme x87_ops_loadstore.cs et x87_ops_arith.cs. Les
// `codegen_set_rounding_mode(...)` sont omis : le dynarec, une souche vide dans l'oracle.
// FSTOR, FSAVE, FLDENV et FSTENV appellent FP_ENTER une SECONDE fois (fpucount compte deux) :
// c'est le C, sans autre effet. Défauts de PCem reproduits : PB-61 (FNSTSW AX sans TOP), PB-62
// (x87_pc_* et x87_op_* jamais posés ; dispositions de FSAVE et FSTENV incomplètes), PB-63
// (FXAM), PB-64 (FTST), PB-65 (FPREM, FPREM1), PB-66 (FLDLN2), PB-67 (FST registre et
// TAG_UINT64), PB-68 (C2 et les transcendantes).

using static iXtal26.Cpu._386_common;
using static iXtal26.Cpu.x86;

namespace iXtal26.Cpu;

internal static partial class _386
{"""]
n=0
for fn,hasarg,a,b,body in funcs:
    if fn in SKIP: continue
    n+=1
    L.append('    // pcem: x87_ops_misc.h:%d-%d'%(a,b))
    L.append('    private static int %s(%s)'%(fn,'uint32_t fetchdat' if hasarg else ''))
    L.append('    {')
    for l in conv(fn,body):
        L.append(('    '+l) if l else '')
    L.append('    }'); L.append('')
L[-1]='}'
open(R+'iXtal26/Cpu/x87_ops_misc.cs','w',encoding='utf-8').write('\n'.join(L)+'\n')
print(n,'fonctions générées')
