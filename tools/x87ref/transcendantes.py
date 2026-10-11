# SPDX-FileCopyrightText: 2026 Julien Peloille
# SPDX-License-Identifier: GPL-2.0-only
#
# G13.6, le noyau, N6 — la référence des transcendantes du x87, hors machine : chaque résultat calculé en précision
# arbitraire (decimal et fractions, de la bibliothèque standard de Python, sans installation), puis arrondi une seule
# fois à 64 bits selon RC, comme SoftFloat arrondit (softfloat_roundPackToExtF80 : la petitesse après l'arrondi, PE,
# UE, OE et C1, « arrondi vers le haut »). Le noyau (x87_transcendantes.Materiel.cs) calcule sur 128 bits et arrondit
# une fois ; ce qu'il rend doit être ce que rend cet outil, au bit près. Le modèle, les spéciaux et les choix sont ceux
# que le noyau écrit (x87_noyau.Materiel.cs, § les transcendantes) ; l'outil les recalcule de son côté, en rationnels
# exacts pour la réduction par le Pi de 66 bits (SDM vol. 1, 2016, § 8.3.8), en décimal à 140 chiffres pour le reste.
#
#   python3 tools/x87ref/transcendantes.py constantes          les constantes du noyau, réécrites en place entre les
#                                                                marques de x87_transcendantes.Materiel.cs (rejouable :
#                                                                après l'appel, `git status --short` vide)
#   python3 tools/x87ref/transcendantes.py vecteurs N GRAINE [F,…]
#                                                              N cas tirés par instruction, en TSV (le banc : x87-transc)
#   python3 tools/x87ref/transcendantes.py cas F RC X [Y]      un cas ; X (ST(0)) et Y (ST(1)) en 20 chiffres
#                                                                hexadécimaux (signe et exposant, puis significande)
#
# Une ligne de vecteurs : l'instruction, RC (0 à 3), X, Y (ou -), puis le nouvel ST(0), le nouvel ST(1) (FPTAN et
# FSINCOS, sinon -), les drapeaux levés (I, Z, D, O, U, P, ou -), C1 (0 ou 1) et C2 (0 ou 1 ; - pour les instructions
# qui le laissent indéfini). Les cas sont masqués (mot de contrôle 037Fh | RC << 10) et la pile en règle (ST(7) vide).
import os
import random
import sys
from decimal import Decimal, getcontext, localcontext
from fractions import Fraction

PREC = 140  # chiffres décimaux, ≈ 465 bits : la marge de l'arrondi à 64 bits se contrôle à 2^−300 près (arrondir)
getcontext().prec = PREC
getcontext().Emin = -999999999
getcontext().Emax = 999999999

EMAX = 16383
EMIN = -16382
P66 = 0xC90FDAA22168C234C  # Pi = P66 · 2^−66 (SDM vol. 1, 2016, § 8.3.8 : 0.C90FDAA2 2168C234 C × 2^2)
FONCTIONS = ['F2XM1', 'FYL2X', 'FYL2XP1', 'FSIN', 'FCOS', 'FPTAN', 'FSINCOS', 'FPATAN']


# ---- Les valeurs de 80 bits ----

def valeur(se, sig):
    """La valeur exacte d'un registre normal, dénormal ou nul (Fraction)."""
    e = se & 0x7FFF
    s = -1 if se & 0x8000 else 1
    if e == 0:
        e = 1
    return s * Fraction(sig) * Fraction(2) ** (e - 0x3FFF - 63)


def hexa(v):
    return '%04X%016X' % v if v is not None else '-'


def lire(h):
    return int(h[:4], 16), int(h[4:], 16)


def dec(f):
    """Une Fraction en Decimal, à la précision du contexte."""
    return Decimal(f.numerator) / Decimal(f.denominator)


def frac(d):
    return Fraction(d)


# ---- L'arrondi à 64 bits ----

class Arrondi:
    """Les drapeaux et C1 d'une instruction, accumulés sur ses arrondis (C1 : celui du dernier)."""
    def __init__(self, rc):
        self.rc = rc
        self.drapeaux = set()
        self.c1 = 0

    def __call__(self, v, exact, err=None):
        """v : la valeur (Fraction, non nulle), exacte si `exact`, sinon l'approximation d'un irrationnel à `err` près
        (par défaut |v| · 2^−400, la précision du décimal). Rend (se, sig), comme softfloat_roundPackToExtF80 (80
        bits ; la petitesse après l'arrondi)."""
        rc = self.rc
        neg = v < 0
        a = -v if neg else v
        if err is None:
            err = a / Fraction(2) ** 400
        e = a.numerator.bit_length() - a.denominator.bit_length()
        if Fraction(2) ** e > a:
            e -= 1
        assert Fraction(2) ** e <= a < Fraction(2) ** (e + 1)

        def quantifier(q):
            """a / 2^q arrondi à l'entier selon RC ; rend (N, inexact, vers le haut)."""
            s = a / Fraction(2) ** q
            n = s.numerator // s.denominator
            f = s - n
            if not exact:
                # Un irrationnel : jamais sur une frontière ; l'écart doit dépasser l'erreur de l'approximation.
                marge = err / Fraction(2) ** q
                assert f > marge and 1 - f > marge and abs(f - Fraction(1, 2)) > marge, 'cas difficile'
            if f == 0:
                return n, False, False
            if rc == 0:
                haut = f > Fraction(1, 2) or (f == Fraction(1, 2) and n & 1)
            elif rc == 1:
                haut = neg
            elif rc == 2:
                haut = not neg
            else:
                haut = False
            return (n + 1 if haut else n), True, haut

        def deborde():
            self.drapeaux |= {'O', 'P'}
            if rc == 0 or (rc == 1 and neg) or (rc == 2 and not neg):
                self.c1 = 1
                return (0xFFFF if neg else 0x7FFF), 1 << 63
            self.c1 = 0
            return (0xFFFE if neg else 0x7FFE), (1 << 64) - 1

        # La petitesse après l'arrondi : le résultat arrondi à 64 bits, l'exposant non borné, sous 2^EMIN.
        n, inexact, haut = quantifier(e - 63)
        petit = Fraction(n) * Fraction(2) ** (e - 63) < Fraction(2) ** EMIN
        if n == 1 << 64:
            n >>= 1
            e += 1
        if e > EMAX:
            return deborde()
        if e < EMIN:
            n, inexact, haut = quantifier(EMIN - 63)
            e = EMIN
        if inexact:
            self.drapeaux.add('P')
            if petit:
                self.drapeaux.add('U')
        self.c1 = int(haut)
        s = 0x8000 if neg else 0
        if n == 0:
            return s, 0
        if n < 1 << 63:
            return s, n  # un dénormal
        return s | (e + 0x3FFF), n


# ---- Les fonctions, en précision arbitraire ----

_LN2 = None
_PI = None


def ln2():
    global _LN2
    if _LN2 is None:
        with localcontext() as c:
            c.prec = PREC + 40
            _LN2 = Decimal(2).ln()
    return +_LN2


def pi():
    """π par Machin : 16 atan(1/5) − 4 atan(1/239)."""
    global _PI
    if _PI is None:
        with localcontext() as c:
            c.prec = PREC + 60

            def atan_inv(n):
                x = Decimal(1) / n
                x2 = x * x
                s, t, k = x, x, 1
                while True:
                    t = -t * x2
                    k += 2
                    d = t / k
                    if abs(d) < Decimal(10) ** -(c.prec + 5):
                        break
                    s += d
                return s
            _PI = 16 * atan_inv(5) - 4 * atan_inv(239)
    return +_PI


def serie(premier, raison, diviseur, eps):
    """Σ t_k, t_0 = premier, t_(k+1) = t_k · raison / diviseur(k + 1), jusqu'à |t| < eps."""
    s, t, k = premier, premier, 0
    while True:
        k += 1
        t = t * raison / diviseur(k)
        if abs(t) < eps:
            return s
        s += t


def expm1(y):
    """e^y − 1, sans perte relative pour |y| petit."""
    if abs(y) >= Decimal('0.5'):
        return y.exp() - 1
    return serie(y, y, lambda k: k + 1, abs(y) * Decimal(10) ** -(PREC + 10))


def ln1p(x):
    """ln(1 + x) par 2 atanh(x / (2 + x)), sans perte relative pour |x| petit (x > −1)."""
    if abs(x) >= Decimal('0.5'):
        return (1 + x).ln()
    u = x / (2 + x)
    s, t, k = u, u, 1
    eps = abs(u) * Decimal(10) ** -(PREC + 10)
    while True:
        t = t * u * u
        k += 2
        if abs(t / k) < eps:
            return 2 * s
        s += t / k


def sin_d(r):
    """sin r, |r| ≤ 1."""
    return serie(r, -r * r, lambda k: (2 * k) * (2 * k + 1), abs(r) * Decimal(10) ** -(PREC + 10))


def cos_d(r):
    """cos r, |r| ≤ 1."""
    return serie(Decimal(1), -r * r, lambda k: (2 * k - 1) * (2 * k), Decimal(10) ** -(PREC + 10))


def atan_d(t):
    """atan t, 0 ≤ t ≤ 1 : trois demi-angles, atan t = 2 atan(t / (1 + √(1 + t²))), puis la série."""
    n = 0
    while t > Decimal('0.1'):
        t = t / (1 + (1 + t * t).sqrt())
        n += 1
    s, p, k = t, t, 1
    eps = abs(t) * Decimal(10) ** -(PREC + 10)
    while True:
        p = -p * t * t
        k += 2
        if abs(p / k) < eps:
            break
        s += p / k
    return s * 2 ** n


def puissance_de_deux(f):
    """k si f (Fraction > 0) vaut 2^k, sinon None."""
    n, d = f.numerator, f.denominator
    if n & (n - 1) == 0 and d == 1:
        return n.bit_length() - 1
    if n == 1 and d & (d - 1) == 0:
        return -(d.bit_length() - 1)
    return None


def f2xm1(x):
    """2^x − 1 : (la valeur, exacte ?). Hors de [−1, 1], la même valeur mathématique (un choix, comme le noyau)."""
    if x > 20000:
        return Fraction(2) ** 20000, True  # déborde : la valeur exacte importe peu
    if x < -200:
        return Fraction(-1) + Fraction(1, 2 ** 300), True  # −1 + 2^x, 2^x < 2^−200 : le même arrondi
    if x.denominator == 1:
        return Fraction(2) ** x.numerator - 1, True
    return frac(expm1(dec(x) * ln2())), False


def log2(x):
    """log₂ x, x > 0 : (la valeur, exacte ?)."""
    k = puissance_de_deux(x)
    if k is not None:
        return Fraction(k), True
    return frac(dec(x).ln() / ln2()), False


def log2_1p(x):
    """log₂(1 + x), x > −1, x non nul : (la valeur, exacte ?)."""
    k = puissance_de_deux(1 + x)
    if k is not None:
        return Fraction(k), True
    return frac(ln1p(dec(x)) / ln2()), False


def reduire(ax):
    """|x| réduit par le Pi de 66 bits, exactement : (r, q mod 4), |x| = q · Pi/2 + r, |r| ≤ Pi/4."""
    if ax < Fraction(1, 2):
        return ax, 0
    n = ax * 2 ** 67
    assert n.denominator == 1
    n = n.numerator
    q, a = divmod(n, P66)
    if 2 * a > P66:
        a -= P66
        q += 1
    return Fraction(a, 2 ** 67), q & 3


def trigo(f, x):
    """sin, cos ou tan de x (Fraction, 0 < |x| < 2^63), au modèle du noyau : (la valeur approchée, son erreur ou None).
    Sous 2^−60, rien n'est réduit, et le terme qui départage l'arrondi (x³/6 pour sin x, x²/2 pour cos x) peut échapper
    au décimal : les premiers termes de la série, en rationnels exacts, et une borne du reste."""
    neg = x < 0
    r, k = reduire(-x if neg else x)
    if abs(r) < Fraction(1, 2 ** 60):
        s, es = r - r ** 3 / 6, abs(r) ** 5 / 100
        c, ec = 1 - r ** 2 / 2 + r ** 4 / 24, r ** 6 / 700
        if f == 'sin':
            v, err = [(s, es), (c, ec), (-s, es), (-c, ec)][k]
        elif f == 'cos':
            v, err = [(c, ec), (-s, es), (-c, ec), (s, es)][k]
        elif k % 2 == 0:
            v, err = r + r ** 3 / 3, abs(r) ** 5 / 5
        else:
            v, err = -(1 / r - r / 3), abs(r) ** 3 / 40
        return (-v if neg and f != 'cos' else v), err
    with localcontext() as c:
        c.prec = PREC + 20
        rd = dec(r)
        s, co = sin_d(rd), cos_d(rd)
        if f == 'sin':
            v = [s, co, -s, -co][k]
            v = -v if neg else v
        elif f == 'cos':
            v = [co, -s, -co, s][k]
        else:
            v = s / co if k % 2 == 0 else -co / s
            v = -v if neg else v
        return frac(+v), None


def atan2(y, x):
    """atan2(y, x), y et x finis non nuls : (la valeur, approchée)."""
    a, b = abs(y), abs(x)
    if x > 0 and a / b < Fraction(1, 2 ** 60):
        # atan t = t − t³/3 + …, t = |y / x| : le terme qui départage l'arrondi échappe au décimal.
        t = a / b
        v = t - t ** 3 / 3
        return (-v if y < 0 else v), t ** 5 / 4
    with localcontext() as c:
        c.prec = PREC + 20
        if a <= b:
            t = atan_d(dec(a / b))
        else:
            t = pi() / 2 - atan_d(dec(b / a))
        if x < 0:
            t = pi() - t
        return frac(-t if y < 0 else t), None


# ---- Les instructions, cas spéciaux compris ----

INDEFINI = (0xFFFF, 0xC000000000000000)
UN = (0x3FFF, 1 << 63)


def classe(v):
    se, sig = v
    e = se & 0x7FFF
    if e == 0x7FFF:
        if sig >> 63 == 0:
            return 'nonsup'
        if sig & ((1 << 63) - 1) == 0:
            return 'inf'
        return 'qnan' if sig >> 62 & 1 else 'snan'
    if e == 0:
        return 'zero' if sig == 0 else 'den'
    return 'nonsup' if sig >> 63 == 0 else 'normal'


def nan(c):
    return c in ('qnan', 'snan')


def nan_de(a, b):
    """La règle des NaN (SoftFloat, la spécialisation du 8086, softfloat_propagateNaNExtF80UI(a, b)) : un QNaN plutôt
    qu'un SNaN, sinon le plus grand significande, à égalité le positif ; rendu silencieux."""
    ca, cb = classe(a), classe(b)
    if not nan(cb):
        r = a
    elif not nan(ca):
        r = b
    elif ca == 'snan' and cb == 'qnan':
        r = b
    elif cb == 'snan' and ca == 'qnan':
        r = a
    else:
        r = a if a[1] > b[1] or (a[1] == b[1] and a[0] < b[0]) else b
    return r[0], r[1] | 3 << 62


def negatif(v):
    return bool(v[0] & 0x8000)


def zero(neg):
    return (0x8000 if neg else 0), 0


def infini(neg):
    return (0xFFFF if neg else 0x7FFF), 1 << 63


class Resultat:
    def __init__(self, rc):
        self.ar = Arrondi(rc)
        self.r0 = self.r1 = None
        self.c2 = None

    def drapeau(self, d):
        self.ar.drapeaux.add(d)

    def invalide(self):
        self.drapeau('I')
        return INDEFINI


def calcule(f, rc, x, y=None):
    """Le résultat de f (x = ST(0), y = ST(1)) : un Resultat (r0, r1, drapeaux, C1, C2)."""
    R = Resultat(rc)
    cx = classe(x)
    cy = classe(y) if y is not None else 'normal'
    if f in ('FSIN', 'FCOS', 'FPTAN', 'FSINCOS'):
        R.c2 = 0
    # Le format non supporté, puis les NaN (SNaN : IE).
    if cx == 'nonsup' or cy == 'nonsup':
        r = R.invalide()
    elif nan(cx) or nan(cy):
        if 'snan' in (cx, cy):
            R.drapeau('I')
        r = nan_de(x, y) if y is not None else nan_de(x, x)
    elif f == 'F2XM1':
        r = f2xm1_(R, x, cx)
    elif f in ('FYL2X', 'FYL2XP1'):
        r = logarithme(R, f == 'FYL2XP1', x, y, cx, cy)
    elif f == 'FPATAN':
        r = fpatan(R, x, y, cx, cy)
    else:
        return trigonometrie(R, f, x, cx)
    if f in ('FPTAN', 'FSINCOS'):
        R.r1, R.r0 = r, r  # un NaN, ou l'indéfini, aux deux
    else:
        R.r0 = r
    return R


def denormal(R, *c):
    if 'I' not in R.ar.drapeaux and 'Z' not in R.ar.drapeaux and 'den' in c:
        R.drapeau('D')


def f2xm1_(R, x, cx):
    if cx == 'inf':
        return infini(False) if not negatif(x) else (0xBFFF, 1 << 63)
    if cx == 'zero':
        return x
    denormal(R, cx)
    v, exact = f2xm1(valeur(*x))
    return R.ar(v, exact)


def logarithme(R, p1, x, y, cx, cy):
    sx, sy = negatif(x), negatif(y)
    moins_un = p1 and x == (0xBFFF, 1 << 63)
    if cx == 'zero' and not p1 or moins_un:
        if cy == 'zero':
            return R.invalide()
        if cy != 'inf':
            R.drapeau('Z')
        return infini(not sy)
    if cx == 'zero':
        r = R.invalide() if cy == 'inf' else zero(sx != sy)
    elif sx and (not p1 or cx == 'inf' or valeur(*x) < -1):
        return R.invalide()
    elif cx == 'inf':
        r = R.invalide() if cy == 'zero' else infini(sy)
    else:
        vx = valeur(*x)
        l, exact = log2_1p(vx) if p1 else log2(vx)
        if l == 0:
            r = R.invalide() if cy == 'inf' else zero(sy)
        elif cy == 'inf':
            r = infini(sy != (l < 0))
        elif cy == 'zero':
            r = zero(sy != (l < 0))
        else:
            denormal(R, cx, cy)
            return R.ar(valeur(*y) * l, exact)
    denormal(R, cx, cy)
    return r


def fpatan(R, x, y, cx, cy):
    """atan(y / x), x = ST(0), y = ST(1) (SDM vol. 2, 2016, table 3-30)."""
    sx, sy = negatif(x), negatif(y)
    denormal(R, cx, cy)
    if cy == 'inf':
        if cx == 'inf':
            return R.ar(-3 * frac(pi()) / 4 if sy else 3 * frac(pi()) / 4, False) if sx else \
                R.ar(-frac(pi()) / 4 if sy else frac(pi()) / 4, False)
        return R.ar(-frac(pi()) / 2 if sy else frac(pi()) / 2, False)
    if cy == 'zero':
        return R.ar(-frac(pi()) if sy else frac(pi()), False) if sx else zero(sy)
    if cx == 'inf':
        return R.ar(-frac(pi()) if sy else frac(pi()), False) if sx else zero(sy)
    if cx == 'zero':
        return R.ar(-frac(pi()) / 2 if sy else frac(pi()) / 2, False)
    v, err = atan2(valeur(*y), valeur(*x))
    return R.ar(v, False, err)


def trigonometrie(R, f, x, cx):
    """FSIN, FCOS, FPTAN, FSINCOS (SDM vol. 2, 2016, FSIN, FCOS, FPTAN, FSINCOS ; C2 si |x| ≥ 2^63)."""
    deux = f in ('FPTAN', 'FSINCOS')
    if cx == 'inf':
        # IE, l'indéfini et C2 = 1 (387 PRM, table 3-10 ; 486 PRM, la même table).
        R.r0 = R.invalide()
        R.r1 = R.r0 if deux else None
        R.c2 = 1
        return R
    if cx != 'zero' and (x[0] & 0x7FFF) >= 0x3FFF + 63:
        R.c2 = 1
        R.r0 = x
        return R
    if cx == 'zero':
        s, c = x, UN
    else:
        denormal(R, cx)
        v = valeur(*x)
        def ar(g):
            w, err = trigo(g, v)
            return R.ar(w, False, err)
        if f == 'FSINCOS':
            s = ar('sin')
            c = ar('cos')  # C1, celui du cosinus, arrondi le dernier
        elif f == 'FCOS':
            c = ar('cos')
        else:
            s = ar('tan' if f == 'FPTAN' else 'sin')
    if f == 'FSIN':
        R.r0 = s
    elif f == 'FCOS':
        R.r0 = c
    elif f == 'FPTAN':
        R.r1, R.r0 = s, UN
    else:
        R.r1, R.r0 = s, c
    return R


# ---- Les constantes du noyau ----

def c128(v):
    """Une constante, en (signe, exposant, significande de 128 bits au plus près) : v ≈ ±m · 2^(e − 127)."""
    f = frac(v) if isinstance(v, Decimal) else Fraction(v)
    s = f < 0
    f = abs(f)
    e = f.numerator.bit_length() - f.denominator.bit_length()
    if Fraction(2) ** e > f:
        e -= 1
    q = f / Fraction(2) ** (e - 127)
    m = q.numerator // q.denominator
    if q - m >= Fraction(1, 2):
        m += 1
    if m == 1 << 128:
        m >>= 1
        e += 1
    return s, e, m


def reel(v):
    s, e, m = c128(v)
    return 'new(%s, %d, new UInt128(0x%016X, 0x%016X))' % ('true' if s else 'false', e, m >> 64, m & ((1 << 64) - 1))


def fact(n):
    r = 1
    for k in range(2, n + 1):
        r *= k
    return r


def constantes():
    with localcontext() as c:
        c.prec = PREC + 40
        p = pi()
        out = []
        for nom, v, doc in [('Ln2', ln2(), 'ln 2'), ('Log2e', 1 / ln2(), 'log₂ e = 1 / ln 2'),
                            ('Racine2', Decimal(2).sqrt(), '√2'), ('Pi', p, 'π'), ('PiSur2', p / 2, 'π / 2'),
                            ('PiSur4', p / 4, 'π / 4'), ('TroisPiSur4', 3 * p / 4, '3π / 4')]:
            out.append('    // %s' % doc)
            out.append('    private static readonly Reel %s = %s;' % (nom, reel(v)))
        for nom, coeffs, doc in [
                ('Expm1', [Fraction(1, fact(n + 1)) for n in range(27)], '1 / (n + 1)!, n de 0 à 26 : (e^y − 1) / y'),
                ('Atanh', [Fraction(1, 2 * k + 1) for k in range(26)], '1 / (2k + 1), k de 0 à 25 : atanh(u) / u'),
                ('Sin', [Fraction((-1) ** k, fact(2 * k + 1)) for k in range(16)],
                 '(−1)^k / (2k + 1)!, k de 0 à 15 : sin(r) / r'),
                ('Cos', [Fraction((-1) ** k, fact(2 * k)) for k in range(17)], '(−1)^k / (2k)!, k de 0 à 16 : cos(r)'),
                ('Atan', [Fraction((-1) ** k, 2 * k + 1) for k in range(17)],
                 '(−1)^k / (2k + 1), k de 0 à 16 : atan(u) / u'),
                ('AtanK', [atan_d(Decimal(k) / 8) for k in range(1, 9)], 'atan(k / 8), k de 1 à 8')]:
            out.append('    // %s' % doc)
            out.append('    private static readonly Reel[] %s =' % nom)
            out.append('    [')
            out.extend('        %s,' % reel(q) for q in coeffs)
            out.append('    ];')
        return '\n'.join(out)


# ---- Les vecteurs ----

def tirer(r, f):
    """ST(0) tiré pour f : surtout dans le domaine, avec ses bords, des dénormaux et des spéciaux."""
    def reel_(emin, emax):
        e = r.randint(emin, emax)
        sig = r.getrandbits(64) | 1 << 63
        if r.random() < 0.1:
            sig = 1 << 63 | (r.getrandbits(8) << 55 if r.random() < 0.5 else 0)
        return (0x8000 if r.random() < 0.5 else 0) | (e + 0x3FFF), sig
    u = r.random()
    if u < 0.03:
        return r.choice([(0, 0), (0x8000, 0), (0x7FFF, 1 << 63), (0xFFFF, 1 << 63), (0x7FFF, 0xC000000000000001),
                         (0x7FFF, 0xA000000000000000), (0x3FFF, 1 << 63), (0xBFFF, 1 << 63), (0x3FFE, 1 << 63),
                         (0xBFFE, 1 << 63), (0x3FFF, 0x4000000000000000)])
    if u < 0.06:
        return (0x8000 if r.random() < 0.5 else 0), r.getrandbits(63) | 1
    if f == 'F2XM1':
        return reel_(-70, -1) if u < 0.85 else reel_(-16382, -70) if u < 0.95 else reel_(0, 16)
    if f == 'FYL2XP1':
        return reel_(-70, -2) if u < 0.85 else reel_(-16382, -70) if u < 0.92 else reel_(-1, 6)
    if f in ('FSIN', 'FCOS', 'FPTAN', 'FSINCOS'):
        if u < 0.1:
            # Près d'un multiple de Pi/2 : k Pi/2 tronqué à 64 bits, à quelques unités près.
            k = r.randint(1, 1 << r.randint(1, 40))
            n = k * P66
            ex = n.bit_length() - 1 - 67
            sig = (n >> (n.bit_length() - 64)) + r.randint(-3, 3)
            return (0x8000 if r.random() < 0.5 else 0) | (ex + 0x3FFF), sig | 1 << 63
        return reel_(-3, 3) if u < 0.6 else reel_(-70, 70) if u < 0.9 else reel_(-16382, 64)
    if f == 'FPATAN':
        return reel_(-20, 20) if u < 0.85 else reel_(-16382, 16383)
    if u < 0.5:
        se, sig = reel_(-2, 1)
    elif u < 0.6:
        se, sig = 0x3FFF, (1 << 63) | r.getrandbits(r.randint(1, 30))
    elif u < 0.65:
        se, sig = 0x3FFE, ((1 << 64) - 1) ^ r.getrandbits(r.randint(1, 30))
    else:
        se, sig = reel_(-16382, 16383)
    return se & 0x7FFF, sig


def tirer_y(r, x):
    u = r.random()
    if u < 0.04:
        return r.choice([(0, 0), (0x8000, 0), (0x7FFF, 1 << 63), (0xFFFF, 1 << 63), (0x3FFF, 1 << 63),
                         (0x7FFF, 0xC000000000000000), (0x7FFF, 0xA000000000000000)])
    if u < 0.06:
        return (0x8000 if r.random() < 0.5 else 0), r.getrandbits(63) | 1
    if u < 0.1 and classe(x) == 'normal':
        return (x[0] ^ (0x8000 if r.random() < 0.5 else 0)), x[1]  # |y| = |x|
    if u < 0.13 and classe(x) == 'normal' and (x[0] & 0x7FFF) > 300:
        # |y| = |x| · 2^−k : un rapport exact, petit ou non, dont atan départage l'arrondi par son terme en t³.
        return (x[0] - r.randint(1, 300)) ^ (0x8000 if r.random() < 0.5 else 0), x[1]
    e = r.randint(-20, 20) if u < 0.8 else r.randint(-16382, 16383)
    return (0x8000 if r.random() < 0.5 else 0) | (e + 0x3FFF), r.getrandbits(64) | 1 << 63


def ligne(f, rc, x, y, R):
    dr = ''.join(sorted(R.ar.drapeaux, key='IZDOUP'.index)) or '-'
    return '%s\t%d\t%s\t%s\t%s\t%s\t%s\t%d\t%s' % (f, rc, hexa(x), hexa(y), hexa(R.r0), hexa(R.r1), dr, R.ar.c1,
                                                 '-' if R.c2 is None else R.c2)


def vecteurs(n, graine, fonctions):
    r = random.Random(graine)
    for f in fonctions:
        for _ in range(n):
            x = tirer(r, f)
            y = tirer_y(r, x) if f in ('FYL2X', 'FYL2XP1', 'FPATAN') else None
            rc = r.randint(0, 3)
            try:
                R = calcule(f, rc, x, y)
            except AssertionError:
                print('# difficile\t%s\t%d\t%s\t%s' % (f, rc, hexa(x), hexa(y)))
                continue
            print(ligne(f, rc, x, y, R))


def main(a):
    if not a:
        print(__doc__ or '', file=sys.stderr)
        return 2
    if a[0] == 'constantes':
        f = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', 'iXtal26', 'Cpu',
                         'x87_transcendantes.Materiel.cs')
        t = open(f, encoding='utf-8').read()
        d, fin = '    // CONSTANTES-DEBUT\n', '    // CONSTANTES-FIN\n'
        assert t.count(d) == 1 and t.count(fin) == 1
        t = t[:t.index(d) + len(d)] + constantes() + '\n' + t[t.index(fin):]
        open(f, 'w', encoding='utf-8').write(t)
    elif a[0] == 'vecteurs':
        vecteurs(int(a[1]), int(a[2]), a[3].split(',') if len(a) > 3 else FONCTIONS)
    elif a[0] == 'cas':
        x = lire(a[3])
        y = lire(a[4]) if len(a) > 4 else None
        print(ligne(a[1], int(a[2]), x, y, calcule(a[1], int(a[2]), x, y)))
    else:
        return 2
    return 0


if __name__ == '__main__':
    sys.exit(main(sys.argv[1:]))
