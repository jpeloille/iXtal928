# G13.6, le noyau — les gestionnaires du noyau de 80 bits du x87, d'une seule description, en deux instanciations :
# _386_noyau (le 286, le 386 et le 486 ; a16 et a32) et _808x_noyau (le 8087 ; a16). Comme le 8087.h de PCem recompile
# x87_ops.h dans l'unité du 808x, chaque corps est écrit une fois, et ses aides de contexte (préfixées d'un @) prennent
# la classe de leur cœur : fetch_ea, SEG_CHECK_*, geteaw, seteaw, FP_ENTER et CLOCK_CYCLES sont celles de _386 et de
# _386_common, ou celles du 8087 (x87_8087.cs, 808x.cs). @LIMITE(n), le contrôle de l'opérande contre la limite de son
# segment (le chemin de PB-189), n'existe pas sur le 8087, qui n'a pas de limite ; aucun corps ne l'emploie avant N3,
# où PB-54 rejoint le noyau. @RETOMBE, la sortie INT du 8087 qui retombe (PB-69), n'existe que sur le 8087.
#
# Rejouable à l'octet près : après toute retouche, `python3 tools/x87gen/genmat.py ; git status --short` vide.
import os, re
R = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..') + '/'

# ---- La description ----
# (nom, emplacements, corps, cibles). Un emplacement : ('d9', 0xE3) pour une forme de registre (l'octet ModRM ; pour
# D8 et DC, l'indice de 24 à 31), ('d9', '/5') pour une forme de mémoire (la ligne reg). Une forme de mémoire a un
# gestionnaire _a16 et un _a32 ; @EA est fetch_ea_16 ou fetch_ea_32. Les cibles : '386' et '8087'.
D = []

def instr(nom, ou, corps, cibles=('386', '8087'), doc=''):
    D.append((nom, ou, corps.strip('\n'), cibles, doc))

instr('FNINIT', [('db', 0xE3)], '''
        if (@FP_ENTER()) return 1;
        cpu_state.pc++;
        _x87_noyau.finit();
        @RETOMBE
        @CLOCK_CYCLES(x87_timings_c.x87_timings.finit);
        _386.CPU_BLOCK_END();
        return 0;
''', doc='FNINIT : l\'état de FNINIT selon le type (_x87_noyau.finit) ; sur le 8087, IR effacé, sa sortie INT retombe.')

instr('FNCLEX', [('db', 0xE2)], '''
        if (@FP_ENTER()) return 1;
        cpu_state.pc++;
        cpu_state.npxs &= 0x7F00;
        @RETOMBE
        @CLOCK_CYCLES(x87_timings_c.x87_timings.fclex);
        return 0;
''', doc='FNCLEX : les six exceptions, SF, ES et B effacés (387 PRM, FCLEX ; 287 PRM, FCLEX : B compris) ; C3-C0 et\n'
         'TOP gardés ; sur le 8087, la sortie INT retombe.')

instr('FLDCW', [('d9', '/5')], '''
        uint16_t tempw;
        if (@FP_ENTER()) return 1;
        if (@EA(fetchdat)) return 1;
        if (@SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        tempw = @geteaw();
        if (cpu_state.abrt != 0)
                return 1;
        cpu_state.npxc = tempw;
        @CLOCK_CYCLES(x87_timings_c.x87_timings.fldcw);
        return 0;
''', doc='FLDCW m16 : le mot de contrôle. Démasquer une exception en attente active ERROR# (387 PRM § 4.8.2) : N5.')

instr('FNSTCW', [('d9', '/7')], '''
        if (@FP_ENTER()) return 1;
        if (@EA(fetchdat)) return 1;
        if (@SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
        @seteaw(cpu_state.npxc);
        @CLOCK_CYCLES(x87_timings_c.x87_timings.fstcw_sw);
        return cpu_state.abrt;
''', doc='FNSTCW m16.')

instr('FNSTSW', [('dd', '/7')], '''
        if (@FP_ENTER()) return 1;
        if (@EA(fetchdat)) return 1;
        if (@SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
        @seteaw((uint16_t)((cpu_state.npxs & 0xC7FF) | ((cpu_state.TOP & 7) << 11)));
        @CLOCK_CYCLES(x87_timings_c.x87_timings.fstcw_sw);
        return cpu_state.abrt;
''', doc='FNSTSW m16 : le mot d\'état, TOP compris.')

instr('FNSTSW_AX', [('df', 0xE0)], '''
        if (@FP_ENTER()) return 1;
        cpu_state.pc++;
        x86.AX = (uint16_t)((cpu_state.npxs & 0xC7FF) | ((cpu_state.TOP & 7) << 11));
        @CLOCK_CYCLES(x87_timings_c.x87_timings.fstcw_sw);
        return 0;
''', cibles=('386',), doc='FNSTSW AX : le mot d\'état, TOP compris (287, 387, 486 ; le 8087 n\'a pas DF E0 : N7, PB-200).')

instr('FNOP', [('d9', 0xD0)], '''
        if (@FP_ENTER()) return 1;
        cpu_state.pc++;
        @CLOCK_CYCLES(x87_timings_c.x87_timings.fnop);
        return 0;
''', doc='FNOP.')

for nom, op, sens, doc in [('FINCSTP', 0xF7, '+ 1', 'TOP monte'), ('FDECSTP', 0xF6, '- 1', 'TOP descend')]:
    instr(nom, [('d9', op)], '''
        if (@FP_ENTER()) return 1;
        cpu_state.pc++;
        cpu_state.TOP = (cpu_state.TOP %s) & 7;
        if (cpu_c.fpu_type >= cpu_c.FPU_287XL)
                cpu_state.npxs &= unchecked((uint16_t)~_x87_noyau.C1);
        @CLOCK_CYCLES(x87_timings_c.x87_timings.fincdecstp);
        return 0;
''' % sens, doc='%s : %s, sans toucher aux étiquettes ni aux registres ; C1 à zéro sur le 387 et le 486 (486 PRM,\n'
                 'table 15-1 ; le 287XL : déduit), indéfini sur le 8087 et le 287, laissé.' % (nom, doc))

instr('FFREE', [('dd', 0xC0 + i) for i in range(8)], '''
        if (@FP_ENTER()) return 1;
        cpu_state.pc++;
        _x87_noyau.etiqueter(_x87_noyau.phys((int)(fetchdat & 7)), _x87_noyau.VIDE);
        @CLOCK_CYCLES(x87_timings_c.x87_timings.ffree);
        return 0;
''', doc='FFREE ST(i) : l\'étiquette de ST(i) vide ; TOP et le registre gardés.')

# ---- Les deux instanciations ----
# Les aides de contexte et leur classe, par cible.
CTX = {
    '386': {'FP_ENTER': '_386.FP_ENTER', 'CLOCK_CYCLES': '_386.CLOCK_CYCLES', 'SEG_CHECK_READ': 'SEG_CHECK_READ',
            'SEG_CHECK_WRITE': 'SEG_CHECK_WRITE', 'geteaw': 'geteaw', 'seteaw': 'seteaw'},
    '8087': {'FP_ENTER': '_808x.FP_ENTER', 'CLOCK_CYCLES': '_808x.CLOCK_CYCLES', 'SEG_CHECK_READ': '_808x.SEG_CHECK_READ',
             'SEG_CHECK_WRITE': '_808x.SEG_CHECK_WRITE', 'geteaw': '_808x.geteaw', 'seteaw': '_808x.seteaw'},
}

def instancier(corps, cible, a):
    t = corps
    ea = {'386': 'fetch_ea_%d' % a, '8087': '_808x.fetch_ea_16'}[cible]
    t = t.replace('@EA(', ea + '(')
    lim = '        if (_386_materiel.limite_materiel(%s)) return 1;'
    t = re.sub(r'        @LIMITE\((\d+)\)\n', (lambda m: (lim % m.group(1)) + '\n') if cible == '386' else '', t)
    t = t.replace('        @RETOMBE\n', '        _x87_noyau.int_retombe();\n' if cible == '8087' else '')
    for k, v in CTX[cible].items():
        t = t.replace('@' + k + '(', v + '(')
    assert '@' not in t, t
    return t

def generer(cible):
    classe = {'386': '_386_noyau', '8087': '_808x_noyau'}[cible]
    tailles = [16, 32] if cible == '386' else [16]
    out = []
    pose = []
    for nom, ou, corps, cibles, doc in D:
        if cible not in cibles:
            continue
        memoire = any(isinstance(o[1], str) for o in ou)
        variantes = [('_a%d' % a, a) for a in tailles] if memoire else [('', 16)]
        for suffixe, a in variantes:
            out.append('    // %s' % doc.replace('\n', '\n    //   ') if suffixe in ('', '_a16') else None)
            out.append('    internal static int op%s%s(uint32_t fetchdat)\n    {\n%s\n    }\n' %
                       (nom, suffixe, instancier(corps, cible, a)))
        for esc, o in ou:
            for suffixe, a in variantes:
                t = ('_386.x86_opcodes_%s_a%d!' % (esc, a)) if cible == '386' else ('_808x.ops_808x_fpu_%s_a16' % esc)
                if isinstance(o, str):
                    pose.append('        _x87_noyau.memoire(%s, %s, op%s%s);' % (t, o[1:], nom, suffixe))
                else:
                    pose.extend('        %s[0x%02X] = op%s;' % (t if cible == '8087' else '_386.x86_opcodes_%s_a%d!' % (esc, b), o, nom)
                                for b in (tailles if cible == '386' else [16]))
    out = [x for x in out if x is not None]
    if cible == '386':
        tables = ['        _386.x86_opcodes_%s_a%d = _x87_noyau.table(%d, 0x%s);' % (e, a, 32 if e in ('d8', 'dc') else 256, e.upper())
                  for e in ['d8', 'd9', 'da', 'db', 'dc', 'dd', 'de', 'df'] for a in (16, 32)]
        doc = ('    /// <summary>Les seize tables du noyau, qui remplacent celles du mode : chaque instruction écrite à son entrée,\n'
               '    /// les autres à l\'arrêt (_x87_noyau.a_ecrire).</summary>')
    else:
        tables = ['        _x87_noyau.remplir(_808x.ops_808x_fpu_%s_a16, 0x%s);' % (e, e.upper())
                  for e in ['d8', 'd9', 'da', 'db', 'dc', 'dd', 'de', 'df']]
        doc = ('    /// <summary>Les huit tables du 8087, remplacées en place (execx86 les indexe en dur) : chaque instruction écrite\n'
               '    /// à son entrée, les autres à l\'arrêt (_x87_noyau.a_ecrire).</summary>')
    corps = '\n'.join(out) + '\n' + doc + '\n    internal static void poser_tables()\n    {\n' + '\n'.join(tables + pose) + '\n    }\n'
    hdr = '''// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — le noyau de 80 bits du x87, ses gestionnaires %s (G13.6, le noyau ; PLAN-G13.md).
// STATUS: materiel
//
// GÉNÉRÉ par tools/x87gen/genmat.py, d'une description partagée avec %s ; ne pas éditer à la
// main. La couche commune (l'état, la pile, les exceptions, l'arrondi) est dans x87_noyau.Materiel.cs. Une classe à
// part : des membres de plus dans %s renuméroteraient ses lambdas.

using static iXtal26.Cpu._386_common;

namespace iXtal26.Cpu;

internal static class %s
{
''' % ({'386': 'du 286, du 386 et du 486, a16 et a32', '8087': 'du 8087, a16'}[cible],
       {'386': 'x87_noyau_808x.Materiel.cs', '8087': 'x87_noyau_386.Materiel.cs'}[cible],
       {'386': '_386', '8087': '_808x'}[cible], classe)
    fichier = {'386': 'x87_noyau_386.Materiel.cs', '8087': 'x87_noyau_808x.Materiel.cs'}[cible]
    open(R + 'iXtal26/Cpu/' + fichier, 'w', encoding='utf-8').write(hdr + corps + '}\n')

generer('386')
generer('8087')
