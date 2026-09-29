/* SPDX-FileCopyrightText: 2026 Julien Peloille
 * SPDX-License-Identifier: GPL-2.0-only
 *
 * G4.0 — les sondes de PARITÉ du x87, avant toute ligne de x87 côté C#.
 *
 * PCem n'émule pas un x87 à 80 bits : ST[] est un tableau de double (x86.h:93). Un double
 * C# reproduit donc l'oracle au bit près, et le risque se déplace sur trois points que
 * ces sondes mesurent contre le C# (tools/iXtal26.Diff/X87Parity.cs) :
 *
 *   - la libm : x87_ops_misc.h appelle sin, cos, tan, atan2, log, pow, sqrt ;
 *     x87_ops_loadstore.h, fmod et floor. h_libm les appelle ICI, dans la .so, avec les
 *     drapeaux de l'oracle — et non depuis un P/Invoke du C# vers libm.so.6, qui ne
 *     verrait pas ce que GCC plie à la compilation (log(2.0) par MPFR, par exemple) ;
 *   - les conversions double -> entier : de l'UB en C hors bornes, donc ce que GCC émet
 *     (cvttsd2si) et non ce que la norme dit. .NET sature depuis la version 9 ;
 *   - fesetround : x87_ops_arith.h:12-16 encadre FADD & co. GCC -O2 sans
 *     -frounding-math peut plier ou déplacer l'opération hors du mode.
 *
 * Les expressions de x87_ops_misc.h sont recopiées VERBATIM, à la ligne citée : c'est la
 * composition que le C# devra rendre, pas seulement ses briques.
 */

#include <fenv.h>
#include <math.h>
#include <stdint.h>
#include <string.h>

#include "harness.h"

static double h_d(uint64_t u) {
        double d;
        memcpy(&d, &u, sizeof d);
        return d;
}

static uint64_t h_u(double d) {
        uint64_t u;
        memcpy(&u, &d, sizeof u);
        return u;
}

uint64_t h_libm(int n, uint64_t ua, uint64_t ub) {
        /* volatile : les entrées arrivent par la frontière de la .so, mais on interdit
         * aussi tout pliage de GCC sur ces deux-là. Les constantes (2.0, 1.0) restent
         * pliables, comme dans x87_ops_misc.h — c'est voulu. */
        volatile double va = h_d(ua), vb = h_d(ub);
        double a = va, b = vb;
        int64_t temp64;

        switch (n) {
        case H_LIBM_SIN:
                return h_u(sin(a));
        case H_LIBM_COS:
                return h_u(cos(a));
        case H_LIBM_TAN:
                return h_u(tan(a));
        case H_LIBM_ATAN2:
                return h_u(atan2(a, b));
        case H_LIBM_LOG:
                return h_u(log(a));
        case H_LIBM_POW:
                return h_u(pow(a, b));
        case H_LIBM_SQRT:
                return h_u(sqrt(a));
        case H_LIBM_FMOD:
                return h_u(fmod(a, b));
        case H_LIBM_FLOOR:
                return h_u(floor(a));
        case H_LIBM_CEIL:
                return h_u(ceil(a));
        case H_LIBM_F2XM1: /* x87_ops_misc.h:559 */
                return h_u(pow(2.0, a) - 1.0);
        case H_LIBM_FYL2X: /* x87_ops_misc.h:570, ST(1) = b, ST(0) = a */
                return h_u(b * (log(a) / log(2.0)));
        case H_LIBM_FYL2XP1: /* x87_ops_misc.h:582 */
                return h_u(b * (log(a + 1.0) / log(2.0)));
        case H_LIBM_FSCALE: /* x87_ops_misc.h:720-722, ST(0) = a, ST(1) = b */
                temp64 = (int64_t)b;
                if (a != 0.0)
                        a = a * pow(2.0, (double)temp64);
                return h_u(a);
        }
        return 0;
}

uint64_t h_conv(int n, uint64_t ua) {
        volatile double va = h_d(ua);
        double a = va;

        switch (n) {
        case H_CONV_I64:
                return (uint64_t)(int64_t)a;
        case H_CONV_U64:
                return (uint64_t)a;
        case H_CONV_I32:
                return (uint64_t)(int64_t)(int32_t)a;
        case H_CONV_I16:
                return (uint64_t)(int64_t)(int16_t)a;
        }
        return 0;
}

/* Le motif de x87_ops_arith.h:12-16, hors du handler : l'opération entre deux
 * fesetround, sous les drapeaux de l'oracle. op : 0 +, 1 -, 2 *, 3 /. mode : les deux bits
 * RC de npxc (0 au plus près, 1 vers le bas, 2 vers le haut, 3 vers zéro). */
static const int h_rounding_modes[4] = {FE_TONEAREST, FE_DOWNWARD, FE_UPWARD, FE_TOWARDZERO};

uint64_t h_fpu_arith(int op, int mode, uint64_t ua, uint64_t ub) {
        volatile double va = h_d(ua), vb = h_d(ub);
        double a = va, b = vb, r;

        if (mode & 3)
                fesetround(h_rounding_modes[mode & 3]);
        switch (op) {
        case 0:
                r = a + b;
                break;
        case 1:
                r = a - b;
                break;
        case 2:
                r = a * b;
                break;
        default:
                r = a / b;
                break;
        }
        if (mode & 3)
                fesetround(FE_TONEAREST);
        return h_u(r);
}
