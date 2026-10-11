# G13.6, le noyau — les gestionnaires du noyau de 80 bits du x87, d'une seule description, en deux instanciations :
# _386_noyau (le 286, le 386 et le 486 ; a16 et a32) et _808x_noyau (le 8087 ; a16). Comme le 8087.h de PCem recompile
# x87_ops.h dans l'unité du 808x, chaque corps est écrit une fois, et ses aides de contexte (préfixées d'un @) prennent
# la classe de leur cœur : fetch_ea, SEG_CHECK_*, geteaw, seteaw, FP_ENTER et CLOCK_CYCLES sont celles de _386 et de
# _386_common, ou celles du 8087 (x87_8087.cs, 808x.cs). @LIMITE(n, r|w), le contrôle de l'opérande de n octets
# contre la limite de son segment et ses droits (PB-54, _x87_noyau.limite), n'existe pas sur le 8087, qui n'a pas de
# limite ; chaque forme de mémoire l'emploie. @POINTEURS et @POINTEURS_M, les pointeurs de l'instruction et de son
# opérande (PB-62), dans chaque instruction qui n'est pas de contrôle : CS:oldpc, ou, sur le 8087, l'adresse physique de
# l'ESC (_x87_noyau.pointeurs_8087), que le 8087 déduit de cpu_state.pc : avant le cpu_state.pc++ d'une forme de registre. @RETOMBE, la sortie INT du 8087 qui retombe (PB-69), n'existe que sur le 8087.
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
        @LIMITE(2, r)
        tempw = @geteaw();
        if (cpu_state.abrt != 0)
                return 1;
        cpu_state.npxc = tempw;
        @CLOCK_CYCLES(x87_timings_c.x87_timings.fldcw);
        return 0;
''', doc='FLDCW m16 : le mot de contrôle. Démasquer une exception en attente active ERROR# (387 PRM § 4.8.2) : N5. Les bits\n'
         'réservés (6 et 13 à 15, et 7, l\'IEM du 8087, sur les autres) restent tels qu\'écrits, relus par FNSTCW,\n'
         'comme PCem : ce que le silicium en relit n\'est pas documenté (inconnu).')

instr('FNSTCW', [('d9', '/7')], '''
        if (@FP_ENTER()) return 1;
        if (@EA(fetchdat)) return 1;
        if (@SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
        @LIMITE(2, w)
        @seteaw(cpu_state.npxc);
        @CLOCK_CYCLES(x87_timings_c.x87_timings.fstcw_sw);
        return cpu_state.abrt;
''', doc='FNSTCW m16.')

instr('FNSTSW', [('dd', '/7')], '''
        if (@FP_ENTER()) return 1;
        if (@EA(fetchdat)) return 1;
        if (@SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
        @LIMITE(2, w)
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
''', cibles=('386',), doc='FNSTSW AX : le mot d\'état, TOP compris (287, 387, 486).')

instr('FNSTSW_AX', [('df', 0xE0)], '''
        if (@FP_ENTER()) return 1;
        cpu_state.pc++;
        _x87_noyau.dfe0_8087();
        @CLOCK_CYCLES(x87_timings_c.x87_timings.fstcw_sw);
        return 0;
''', cibles=('8087',), doc='DF E0 sur le 8087, qui n\'a pas FNSTSW AX : AX inchangé (_x87_noyau.dfe0_8087 ; PB-200) ; les cycles de PCem.')

instr('FNOP', [('d9', 0xD0)], '''
        if (@FP_ENTER()) return 1;
        @POINTEURS
        cpu_state.pc++;
        @CLOCK_CYCLES(x87_timings_c.x87_timings.fnop);
        return 0;
''', doc='FNOP.')

for nom, op, sens, doc in [('FINCSTP', 0xF7, '+ 1', 'TOP monte'), ('FDECSTP', 0xF6, '- 1', 'TOP descend')]:
    instr(nom, [('d9', op)], '''
        if (@FP_ENTER()) return 1;
        @POINTEURS
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
        @POINTEURS
        cpu_state.pc++;
        _x87_noyau.etiqueter(_x87_noyau.phys((int)(fetchdat & 7)), _x87_noyau.VIDE);
        @CLOCK_CYCLES(x87_timings_c.x87_timings.ffree);
        return 0;
''', doc='FFREE ST(i) : l\'étiquette de ST(i) vide ; TOP et le registre gardés.')

# ---- N3a : les mouvements de données ----
# Le gestionnaire lit ou écrit la mémoire ; la couche (_x87_noyau) fait le reste. Un stockage n'écrit, puis ne dépile,
# que si la couche ne l'a pas arrêté (une exception démasquée) ; une faute de la mémoire (abrt) arrête aussi.

def lecture(nom, ou, n, decl, lire, appel, temps, doc):
    instr(nom, ou, '''
        %s
        if (@FP_ENTER()) return 1;
        if (@EA(fetchdat)) return 1;
        if (@SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        @LIMITE(%d, r)
        @POINTEURS_M
%s
        if (cpu_state.abrt != 0) return 1;
        %s
        @CLOCK_CYCLES(x87_timings_c.x87_timings.%s);
        return 0;
''' % (decl, n, lire, appel, temps), doc=doc)

def stockage(nom, ou, n, appel, ecrire, pop, temps, doc):
    instr(nom, ou, '''
        if (@FP_ENTER()) return 1;
        if (@EA(fetchdat)) return 1;
        if (@SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
        @LIMITE(%d, w)
        @POINTEURS_M
        if (!%s)
        {
%s
                if (cpu_state.abrt != 0) return 1;%s
        }
        @CLOCK_CYCLES(x87_timings_c.x87_timings.%s);
        return 0;
''' % (n, appel, ecrire, '\n                _x87_noyau.depiler();' if pop else '', temps), doc=doc)

lecture('FLDs', [('d9', '/0')], 4, 'uint32_t t;', '        t = @geteal();', '_x87_noyau.fld_f32(t);', 'fld_32',
        'FLD m32 (_x87_noyau.fld_f32).')
lecture('FLDd', [('dd', '/0')], 8, 'uint64_t t;', '        t = @geteaq();', '_x87_noyau.fld_f64(t);', 'fld_64',
        'FLD m64 (_x87_noyau.fld_f64).')
lecture('FLDe', [('db', '/5')], 10, 'uint64_t s; uint16_t e;',
        '        s = @readmemq(x86.easeg, cpu_state.eaaddr);\n        e = @readmemw(x86.easeg, cpu_state.eaaddr + 8);',
        '_x87_noyau.fld_f80(s, e);', 'fld_80', 'FLD m80 : les 80 bits tels quels (_x87_noyau.fld_f80).')
lecture('FILDiw', [('df', '/0')], 2, 'uint16_t t;', '        t = @geteaw();', '_x87_noyau.fild((int16_t)t);', 'fild_16',
        'FILD m16.')
lecture('FILDil', [('db', '/0')], 4, 'uint32_t t;', '        t = @geteal();', '_x87_noyau.fild((int32_t)t);', 'fild_32',
        'FILD m32.')
lecture('FILDiq', [('df', '/5')], 8, 'uint64_t t;', '        t = @geteaq();', '_x87_noyau.fild((int64_t)t);', 'fild_64',
        'FILD m64.')
lecture('FBLD', [('df', '/4')], 10, 'uint64_t s; uint16_t e;',
        '        s = @readmemq(x86.easeg, cpu_state.eaaddr);\n        e = @readmemw(x86.easeg, cpu_state.eaaddr + 8);',
        '_x87_noyau.fbld(s, e);', 'fbld', 'FBLD m80 : dix-huit chiffres décimaux (_x87_noyau.fbld ; PB-52).')

for n, reg, pop in [('FSTs', '/2', False), ('FSTPs', '/3', True)]:
    stockage(n, [('d9', reg)], 4, '_x87_noyau.fst_f32(out var t)', '                @seteal(t);', pop, 'fst_32',
             '%s m32 (_x87_noyau.fst_f32).' % n[:-1])
for n, reg, pop in [('FSTd', '/2', False), ('FSTPd', '/3', True)]:
    stockage(n, [('dd', reg)], 8, '_x87_noyau.fst_f64(out var t)', '                @seteaq(t);', pop, 'fst_64',
             '%s m64 (_x87_noyau.fst_f64).' % n[:-1])
stockage('FSTPe', [('db', '/7')], 10, '_x87_noyau.fst_f80(out var t)',
         '                @writememq(x86.easeg, cpu_state.eaaddr, t.signif);\n'
         '                @writememw(x86.easeg, cpu_state.eaaddr + 8, t.signExp);', True, 'fst_80',
         'FSTP m80 : les 80 bits tels quels (_x87_noyau.fst_f80).')
for n, esc, reg, pop, bits, ecr, temps in [
        ('FISTiw', 'df', '/2', False, 16, '@seteaw((uint16_t)t)', 'fist_16'),
        ('FISTPiw', 'df', '/3', True, 16, '@seteaw((uint16_t)t)', 'fist_16'),
        ('FISTil', 'db', '/2', False, 32, '@seteal((uint32_t)t)', 'fist_32'),
        ('FISTPil', 'db', '/3', True, 32, '@seteal((uint32_t)t)', 'fist_32'),
        ('FISTPiq', 'df', '/7', True, 64, '@seteaq((uint64_t)t)', 'fist_64')]:
    stockage(n, [(esc, reg)], bits // 8, '_x87_noyau.fist(%d, out var t)' % bits, '                %s;' % ecr, pop, temps,
             '%s m%d (_x87_noyau.fist).' % ('FISTP' if pop else 'FIST', bits))
stockage('FBSTP', [('df', '/6')], 10, '_x87_noyau.fbstp(out var s, out var e)',
         '                @writememq(x86.easeg, cpu_state.eaaddr, s);\n'
         '                @writememw(x86.easeg, cpu_state.eaaddr + 8, e);', True, 'fbstp',
         'FBSTP m80 (_x87_noyau.fbstp ; PB-195).')

def registre(nom, ou, corps, temps, doc):
    instr(nom, ou, '''
        if (@FP_ENTER()) return 1;
        @POINTEURS
        cpu_state.pc++;
        %s
        @CLOCK_CYCLES(x87_timings_c.x87_timings.%s);
        return 0;
''' % (corps, temps), doc=doc)

registre('FLD', [('d9', 0xC0 + i) for i in range(8)], '_x87_noyau.fld_st((int)(fetchdat & 7));', 'fld', 'FLD ST(i).')
registre('FXCH', [('d9', 0xC8 + i) for i in range(8)], '_x87_noyau.fxch((int)(fetchdat & 7));', 'fxch', 'FXCH ST(i).')
registre('FST', [('dd', 0xD0 + i) for i in range(8)], '_x87_noyau.fst_st((int)(fetchdat & 7));', 'fst', 'FST ST(i).')
for nom, esc, doc in [('FSTP', 'dd', 'FSTP ST(i).'), ('FSTP_d9', 'd9', 'FSTP ST(i), D9 D8+i comme PCem.')]:
    registre(nom, [(esc, 0xD8 + i) for i in range(8)],
             'if (!_x87_noyau.fst_st((int)(fetchdat & 7)))\n                _x87_noyau.depiler();', 'fst', doc)
registre('FCHS', [('d9', 0xE0)], '_x87_noyau.signe(false);', 'fchs', 'FCHS.')
registre('FABS', [('d9', 0xE1)], '_x87_noyau.signe(true);', 'fabs', 'FABS.')
registre('FXAM', [('d9', 0xE5)], '_x87_noyau.fxam();', 'fxam', 'FXAM (_x87_noyau.fxam).')
registre('FLDz1', [('d9', 0xE8), ('d9', 0xEE)], '_x87_noyau.constante((int)(fetchdat & 7));', 'fld_z1', 'FLD1 et FLDZ.')
registre('FLDconst', [('d9', 0xE9 + k) for k in range(5)], '_x87_noyau.constante((int)(fetchdat & 7));', 'fld_const',
         'FLDL2T, FLDL2E, FLDPI, FLDLG2, FLDLN2 (_x87_noyau.constante).')

# ---- N3b : l'environnement et l'état ----
# FLDENV, FNSTENV, FRSTOR et FNSAVE, des instructions de contrôle (elles laissent les pointeurs). La couche donne la
# disposition de l'image (_x87_noyau.disposition) et ses mots, offset et valeur (off, mot, n) ; le gestionnaire les lit
# ou les écrit un à un, et ne change l'état qu'une fois l'image entière transférée.

def ecrire_image(nom, ou, taille, appel, fin, temps, doc):
    instr(nom, ou, '''
        int d;
        if (@FP_ENTER()) return 1;
        if (@EA(fetchdat)) return 1;
        if (@SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
        d = _x87_noyau.disposition();
        @LIMITE(%s, w)
        %s
        for (var k = 0; k < _x87_noyau.n; k++)
        {
                @writememw(x86.easeg, cpu_state.eaaddr + _x87_noyau.off[k], _x87_noyau.mot[k]);
                if (cpu_state.abrt != 0) return 1;
        }
        %s
        @CLOCK_CYCLES(x87_timings_c.x87_timings.%s);
        return 0;
''' % (taille, appel, fin, temps), doc=doc)

def lire_image(nom, ou, taille, registres, temps, doc):
    instr(nom, ou, '''
        int d;
        if (@FP_ENTER()) return 1;
        if (@EA(fetchdat)) return 1;
        if (@SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        d = _x87_noyau.disposition();
        @LIMITE(%s, r)
        _x87_noyau.a_lire(d, %s);
        for (var k = 0; k < _x87_noyau.n; k++)
        {
                _x87_noyau.mot[k] = @readmemw(x86.easeg, cpu_state.eaaddr + _x87_noyau.off[k]);
                if (cpu_state.abrt != 0) return 1;
        }
        _x87_noyau.fldenv(d, %s);
        @CLOCK_CYCLES(x87_timings_c.x87_timings.%s);
        return 0;
''' % (taille, registres, registres, temps), doc=doc)

lire_image('FLDENV', [('d9', '/4')], '_x87_noyau.taille_env(d)', 'false', 'fldenv',
           'FLDENV : l\'environnement, dans la disposition du mode (_x87_noyau.fldenv ; PB-62, PB-208).')
ecrire_image('FNSTENV', [('d9', '/6')], '_x87_noyau.taille_env(d)', '_x87_noyau.fstenv(d);', '_x87_noyau.fstenv_fin();',
             'fstenv', 'FNSTENV : l\'environnement, puis les six exceptions masquées (PB-62 ; PB-207).')
lire_image('FRSTOR', [('dd', '/4')], '_x87_noyau.taille_env(d) + 80', 'true', 'frstor',
           'FRSTOR : l\'environnement et les huit registres (PB-62, PB-199, PB-208).')
ecrire_image('FNSAVE', [('dd', '/6')], '_x87_noyau.taille_env(d) + 80', '_x87_noyau.fsave(d);', '_x87_noyau.fsave_fin();',
             'fsave', 'FNSAVE : l\'environnement et les huit registres de 80 bits, puis l\'état de FNINIT (PB-62, PB-199).')

# ---- N4a : l'arithmétique et les comparaisons ----
# Les quatre opérations (_x87_noyau.operation), en mémoire (m32, m64, m32 entier, m16 entier : D8, DC, DA, DE /0, /1, /4
# à /7) et de registre (D8, DC et DE), FSQRT ; les comparaisons (_x87_noyau.comparer) : FCOM et FCOMP en mémoire, de
# registre en D8 et en DC D0+i et D8+i (des alias, comme PCem), FCOMPP, FUCOM, FUCOMP, FUCOMPP, FICOM, FICOMP, FTST.
# Les temps sont ceux de PCem, sauf FCOM, FCOMP et FCOMPP de registre, au temps de fcom (PB-57, comme G13.6a).
OPS = [(0, 'FADD', 'ADD', False, 'fadd'), (1, 'FMUL', 'MUL', False, 'fmul'), (4, 'FSUB', 'SUB', False, 'fadd'),
       (5, 'FSUBR', 'SUB', True, 'fadd'), (6, 'FDIV', 'DIV', False, 'fdiv'), (7, 'FDIVR', 'DIV', True, 'fdiv')]
MEM = [('s', 'd8', 4, 'uint32_t t;', '        t = @geteal();', '_x87_noyau.de_f32(t, out var den)', 'den', '_32', 'm32'),
       ('d', 'dc', 8, 'uint64_t t;', '        t = @geteaq();', '_x87_noyau.de_f64(t, out var den)', 'den', '_64', 'm64'),
       ('il', 'da', 4, 'uint32_t t;', '        t = @geteal();', '_x87_noyau.de_i((int32_t)t)', 'false', '_i32', 'm32 entier'),
       ('iw', 'de', 2, 'uint16_t t;', '        t = @geteaw();', '_x87_noyau.de_i((int16_t)t)', 'false', '_i16', 'm16 entier')]
for suf, esc, n, decl, lire, ext, den, temps, nom in MEM:
    for reg, op, k, inv, tps in OPS:
        mn = ('FI' + op[1:]) if suf in ('il', 'iw') else op
        lecture(op + suf, [(esc, '/%d' % reg)], n, decl, lire,
                '_x87_noyau.arith_m(_x87_noyau.%s, %s, %s, %s);' % (k, 'true' if inv else 'false', ext, den),
                tps + temps, '%s %s (_x87_noyau.arith_m ; PB-48, PB-60).' % (mn, nom))
    for reg, op, pop in [(2, 'FCOM', False), (3, 'FCOMP', True)]:
        mn = ('FI' + op[1:]) if suf in ('il', 'iw') else op
        appel = '_x87_noyau.fcom_m(%s, %s)' % (ext, den)
        lecture(op + suf, [(esc, '/%d' % reg)], n, decl, lire,
                ('if (!%s)\n                _x87_noyau.depiler();' % appel) if pop else appel + ';',
                'fcom' + temps, '%s %s (_x87_noyau.comparer).' % (mn, nom))

I = '(int)(fetchdat & 7)'
for reg, op, k, inv, tps in OPS:
    registre(op + '_st0', [('d8', 24 + reg)], '_x87_noyau.arith_st0(_x87_noyau.%s, %s, %s);' % (k, 'true' if inv else 'false', I),
             tps, '%s ST(0), ST(i) (D8 %02X+i).' % (op, 0xC0 + 8 * reg))
# DC et DE : la destination est ST(i) ; /4 (E0+i) est FSUBR et /5 (E8+i) FSUB, /6 (F0+i) FDIVR et /7 (F8+i) FDIV.
STI = [(0, 'FADD', 'ADD', False, 'fadd'), (1, 'FMUL', 'MUL', False, 'fmul'), (4, 'FSUBR', 'SUB', True, 'fadd'),
       (5, 'FSUB', 'SUB', False, 'fadd'), (6, 'FDIVR', 'DIV', True, 'fdiv'), (7, 'FDIV', 'DIV', False, 'fdiv')]
for reg, op, k, inv, tps in STI:
    registre(op + '_sti', [('dc', 24 + reg)], '_x87_noyau.arith_sti(_x87_noyau.%s, %s, %s, false);' % (k, 'true' if inv else 'false', I),
             tps, '%s ST(i), ST(0) (DC %02X+i).' % (op, 0xC0 + 8 * reg))
    registre(op + 'P', [('de', 0xC0 + 8 * reg + i) for i in range(8)],
             '_x87_noyau.arith_sti(_x87_noyau.%s, %s, %s, true);' % (k, 'true' if inv else 'false', I),
             tps, '%sP ST(i), ST(0) (DE %02X+i).' % (op, 0xC0 + 8 * reg))
registre('FSQRT', [('d9', 0xFA)], '_x87_noyau.fsqrt();', 'fsqrt', 'FSQRT (_x87_noyau.fsqrt).')
registre('FTST', [('d9', 0xE4)], '_x87_noyau.ftst();', 'ftst', 'FTST (_x87_noyau.ftst ; PB-64, en projectif PB-70).')
for nom, ou, signale, pops, tps, doc in [
        ('FCOM_st', [('d8', 26)], 'true', 0, 'fcom', 'FCOM ST(i) (D8 D0+i ; PB-57, au temps de fcom).'),
        ('FCOMP_st', [('d8', 27)], 'true', 1, 'fcom', 'FCOMP ST(i) (D8 D8+i ; PB-57).'),
        ('FCOM_dc', [('dc', 26)], 'true', 0, 'fcom', 'FCOM ST(i), DC D0+i : un alias, comme PCem.'),
        ('FCOMP_dc', [('dc', 27)], 'true', 1, 'fcom', 'FCOMP ST(i), DC D8+i : un alias, comme PCem.'),
        ('FCOMPP', [('de', 0xD9)], 'true', 2, 'fcom', 'FCOMPP (DE D9 ; PB-57, PB-58 : −0 et +0 égaux).'),
        ('FUCOM', [('dd', 0xE0 + i) for i in range(8)], 'false', 0, 'fucom', 'FUCOM ST(i) : un QNaN ne lève pas IE.'),
        ('FUCOMP', [('dd', 0xE8 + i) for i in range(8)], 'false', 1, 'fucom', 'FUCOMP ST(i).'),
        ('FUCOMPP', [('da', 0xE9)], 'false', 2, 'fucom', 'FUCOMPP (DA E9).')]:
    i = '1' if nom.endswith('PP') else I
    corps = '_x87_noyau.fcom_st(%s, %s)' % (i, signale)
    if pops:
        corps = 'if (!%s)\n        {\n%s        }' % (corps, '                _x87_noyau.depiler();\n' * pops)
    else:
        corps += ';'
    registre(nom, ou, corps, tps, doc)

# ---- N4b : FRNDINT, FSCALE, FXTRACT, FPREM et FPREM1 ----
registre('FRNDINT', [('d9', 0xFC)], '_x87_noyau.frndint();', 'frndint', 'FRNDINT (_x87_noyau.frndint ; PB-197, PB-211).')
registre('FSCALE', [('d9', 0xFD)], '_x87_noyau.fscale();', 'fscale', 'FSCALE (_x87_noyau.fscale ; PB-198).')
registre('FXTRACT', [('d9', 0xF4)], '_x87_noyau.fxtract();', 'fxtract', 'FXTRACT (_x87_noyau.fxtract ; PB-194).')
registre('FPREM', [('d9', 0xF8)], '_x87_noyau.fprem(false);', 'fprem', 'FPREM (_x87_noyau.fprem ; PB-65).')
registre('FPREM1', [('d9', 0xF5)], '_x87_noyau.fprem(true);', 'fprem1', 'FPREM1 (_x87_noyau.fprem ; PB-65).')

# ---- N6 : les transcendantes ----
registre('F2XM1', [('d9', 0xF0)], '_x87_noyau.f2xm1();', 'f2xm1', 'F2XM1 (_x87_noyau.f2xm1 ; PB-68).')
registre('FYL2X', [('d9', 0xF1)], '_x87_noyau.fyl2x(false);', 'fyl2x', 'FYL2X (_x87_noyau.fyl2x ; PB-68).')
registre('FYL2XP1', [('d9', 0xF9)], '_x87_noyau.fyl2x(true);', 'fyl2xp1', 'FYL2XP1 (_x87_noyau.fyl2x ; PB-68).')
registre('FPTAN', [('d9', 0xF2)], '_x87_noyau.fptan(false);', 'fptan', 'FPTAN (_x87_noyau.fptan ; PB-68).')
registre('FPATAN', [('d9', 0xF3)], '_x87_noyau.fpatan();', 'fpatan', 'FPATAN (_x87_noyau.fpatan ; PB-68).')
registre('FSINCOS', [('d9', 0xFB)], '_x87_noyau.fptan(true);', 'fsincos', 'FSINCOS (_x87_noyau.fptan ; PB-68 ; sur le 8087 et le 287, PB-266).')
registre('FSIN', [('d9', 0xFE)], '_x87_noyau.fsin_cos(0);', 'fsin_cos', 'FSIN (_x87_noyau.fsin_cos ; PB-68 ; sur le 8087 et le 287, PB-266).')
registre('FCOS', [('d9', 0xFF)], '_x87_noyau.fsin_cos(1);', 'fsin_cos', 'FCOS (_x87_noyau.fsin_cos ; PB-68 ; sur le 8087 et le 287, PB-266).')

instr('FSETPM', [('db', 0xE4)], '''
        if (@FP_ENTER()) return 1;
        cpu_state.pc++;
        if (cpu_c.fpu_type is cpu_c.FPU_287 or cpu_c.FPU_287XL)
                _x87_noyau.fsetpm = true;
        @CLOCK_CYCLES(x87_timings_c.x87_timings.fnop);
        return 0;
''', cibles=('386',), doc='FSETPM : le 287 et le 287XL passent au mode protégé, jusqu\'au RESET (287 PRM, FSETPM ; PB-62) ; le 387 et le 486\n'
                          'n\'en font rien (387 PRM § 4.8). Le 8087 n\'a pas DB E4 : DB_E5.')

for nom, op, effet, quoi in [('FENI', 0xE0, 'cpu_state.npxc &= unchecked((uint16_t)~0x80);', 'efface'),
                             ('FDISI', 0xE1, 'cpu_state.npxc |= 0x80;', 'pose')]:
    instr(nom, [('db', op)], '''
        if (@FP_ENTER()) return 1;
        cpu_state.pc++;
        %s
        @CLOCK_CYCLES(x87_timings_c.x87_timings.fdisi_eni);
        return 0;
''' % effet, cibles=('8087',), doc='%s : %s IEM, le masque des requêtes d\'interruption du 8087, bit 7 du mot de contrôle (Numerics\n'
                                  'Supplement, 1980, %s) ; une instruction de contrôle, qui laisse les pointeurs.' % (nom, quoi, nom))
    instr(nom, [('db', op)], '''
        if (@FP_ENTER()) return 1;
        cpu_state.pc++;
        @CLOCK_CYCLES(x87_timings_c.x87_timings.fnop);
        return 0;
''', cibles=('386',), doc='%s : sans effet sur le 287, le 387 et le 486 (287 PRM : « the 80287 will perform no specific operation and\n'
                         'no internal states will be affected » ; 387 PRM, annexe D, point 2).' % nom)

DB_E5 = '''
        if (@FP_ENTER()) return 1;
        cpu_state.pc++;
        @CLOCK_CYCLES(x87_timings_c.x87_timings.fnop);
        return 0;
'''
DB_E5_DOC = ('DB E5, et DB E4 sur le 8087 : réservés (les tables des opcodes du 287 PRM et du manuel de l\'iAPX 86/88, 1981) ;\n'
             'PCem les exécute en FNOP ; le noyau, sans effet ni pointeurs, aux cycles de FNOP (inconnu).')
instr('DB_E5', [('db', 0xE5)], DB_E5, cibles=('386',), doc=DB_E5_DOC)
instr('DB_E5', [('db', 0xE4), ('db', 0xE5)], DB_E5, cibles=('8087',), doc=DB_E5_DOC)

# ---- Les deux instanciations ----
# Les aides de contexte et leur classe, par cible.
CTX = {
    '386': {'FP_ENTER': '_386.FP_ENTER', 'CLOCK_CYCLES': '_386.CLOCK_CYCLES', 'SEG_CHECK_READ': 'SEG_CHECK_READ',
            'SEG_CHECK_WRITE': 'SEG_CHECK_WRITE', 'geteaw': 'geteaw', 'seteaw': 'seteaw'},
    '8087': {'FP_ENTER': '_808x.FP_ENTER', 'CLOCK_CYCLES': '_808x.CLOCK_CYCLES', 'SEG_CHECK_READ': '_808x.SEG_CHECK_READ',
             'SEG_CHECK_WRITE': '_808x.SEG_CHECK_WRITE', 'geteaw': '_808x.geteaw', 'seteaw': '_808x.seteaw'},
}
# Le 8087 lit et écrit son opérande à l'adresse physique, mot après mot (PB-201 : 808x.Materiel.cs) ; geteaw, la seule
# lecture d'un mot, est celle de l'UC.
for k in ['geteal', 'geteaq', 'seteal', 'seteaq', 'readmemq', 'readmemw', 'writememq', 'writememw']:
    CTX['386'][k] = k
    CTX['8087'][k] = '_808x.' + k + '_8087'
CTX['8087']['seteaw'] = '_808x.seteaw_8087'

def instancier(corps, cible, a, esc):
    t = corps
    ea = {'386': 'fetch_ea_%d' % a, '8087': '_808x.fetch_ea_16'}[cible]
    t = t.replace('@EA(', ea + '(')
    lim = '        if (_x87_noyau.limite(%s, %s)) return 1;'
    t = re.sub(r'        @LIMITE\((.+), ([rw])\)\n',
               (lambda m: (lim % (m.group(1), 'true' if m.group(2) == 'w' else 'false')) + '\n') if cible == '386' else '', t)
    if cible == '386':
        t = t.replace('@POINTEURS_M\n', '_x87_noyau.pointeurs_m(0x%s, fetchdat);\n' % esc.upper())
        t = t.replace('@POINTEURS\n', '_x87_noyau.pointeurs(0x%s, fetchdat);\n' % esc.upper())
    else:
        t = t.replace('@POINTEURS_M\n', '_x87_noyau.pointeurs_8087(0x%s, fetchdat, true);\n' % esc.upper())
        t = t.replace('@POINTEURS\n', '_x87_noyau.pointeurs_8087(0x%s, fetchdat, false);\n' % esc.upper())
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
        esc = ou[0][0]
        assert all(o[0] == esc for o in ou), nom
        variantes = [('_a%d' % a, a) for a in tailles] if memoire else [('', 16)]
        for suffixe, a in variantes:
            out.append('    // %s' % doc.replace('\n', '\n    //   ') if suffixe in ('', '_a16') else None)
            out.append('    internal static int op%s%s(uint32_t fetchdat)\n    {\n%s\n    }\n' %
                       (nom, suffixe, instancier(corps, cible, a, esc)))
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
