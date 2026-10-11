// SPDX-FileCopyrightText: 2011, 2012, 2013, 2014, 2015, 2017 The Regents of the University of California
// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: BSD-3-Clause
//
// ORACLE: sources/softfloat/SoftFloat-3e/source/s_roundPackToExtF80.c
//         (SoftFloat-3e.zip, sha256 21130ce885d35c1fe73fc1e1bf2244178167e05c6747cad5f450cc991714c746 ; hors dépôt)
//
// La notice d'origine, gardée comme la licence le demande :
//
// This C source file is part of the SoftFloat IEEE Floating-Point Arithmetic
// Package, Release 3e, by John R. Hauser.
//
// Copyright 2011, 2012, 2013, 2014, 2015, 2017 The Regents of the University of
// California.  All rights reserved.
//
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
//
//  1. Redistributions of source code must retain the above copyright notice,
//     this list of conditions, and the following disclaimer.
//
//  2. Redistributions in binary form must reproduce the above copyright notice,
//     this list of conditions, and the following disclaimer in the documentation
//     and/or other materials provided with the distribution.
//
//  3. Neither the name of the University nor the names of its contributors may
//     be used to endorse or promote products derived from this software without
//     specific prior written permission.
//
// THIS SOFTWARE IS PROVIDED BY THE REGENTS AND CONTRIBUTORS "AS IS", AND ANY
// EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE, ARE
// DISCLAIMED.  IN NO EVENT SHALL THE REGENTS OR CONTRIBUTORS BE LIABLE FOR ANY
// DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
//
// Un seul écart de forme : en C, `goto overflow` saute dans le bloc du débordement à 80 bits, ce que C# interdit. Le
// bloc est sorti sous l'étiquette, après la fin du chemin ordinaire, qui saute à packReturn ; le débordement à 80
// bits pose roundMask à zéro puis y saute. Le chemin de chacun est celui du C.
//
// DEVIATION (G13, point n° 10, validée par Julien le 11/10) — C1 : chaque chemin retient la troncature (`expTrunc`, `sigTrunc`) avant d'arrondir,
// et packReturn note dans softfloat_roundedUp si le résultat en diffère (softfloat_state.cs). Rien d'autre ne change.
//
// DEVIATION (G13, le noyau, N5 ; validée par Julien le 11/10 : le point (3) du sous-bloc, PLAN-G13.md § G13.6, le noyau) — l'exposant non borné : quand
// softfloat_unboundedExp (softfloat_state.cs) nomme un côté (softfloat_flag_overflow, softfloat_flag_underflow), un
// résultat qui sort de ce côté de la plage de 80 bits est arrondi à la précision demandée sur un exposant non borné,
// puis rangé à l'exposant décalé de 24 576 (3 × 2^13 ; 387 PRM § 3.2.5.1 et § 3.2.5.2 ; SDM vol. 1, 2003, § 8.5.4 et §
// 8.5.5) : c'est la réponse du x87 à OE et UE démasquées, quand la destination est un registre. Côté du dépassement par
// défaut, la petitesse se juge sur ce résultat (exposant ≤ 0 : après l'arrondi), et UE est levée qu'il soit exact ou non
// (387 PRM § 3.2.5.2 : « regardless of inexactness »). Si le décalage ne suffit pas (FSCALE), la réponse masquée de
// l'arrondi au plus près : ±∞ avec OE et PE, ±0 avec UE et PE. Tout est dans roundPackUnbounded ; softfloat_unboundedExp
// nul (le défaut, et tout ce que TestFloat vérifie), le chemin du C reste seul.

namespace iXtal26.SoftFloat;

internal static partial class softfloat
{
    internal static extFloat80_t
     softfloat_roundPackToExtF80(
         bool sign,
         int_fast32_t exp,
         uint_fast64_t sig,
         uint_fast64_t sigExtra,
         uint_fast8_t roundingPrecision
     )
    {
        uint_fast8_t roundingMode;
        bool roundNearEven;
        uint_fast64_t roundIncrement = 0, roundMask = 0, roundBits;
        bool isTiny, doIncrement;
        int_fast32_t expTrunc = 0; // DEVIATION: C1
        uint_fast64_t sigTrunc = 0; // DEVIATION: C1
        uint64_extra sig64Extra;
        extFloat80_t uZ;

        if ( softfloat_unboundedExp != 0 && (sig | sigExtra) != 0
                && roundPackUnbounded( sign, exp, sig, sigExtra, roundingPrecision, out uZ ) ) {
            return uZ; // DEVIATION: exposant non borné
        }
        /*------------------------------------------------------------------------
        *------------------------------------------------------------------------*/
        roundingMode = softfloat_roundingMode;
        roundNearEven = (roundingMode == softfloat_round_near_even);
        if ( roundingPrecision == 80 ) goto precision80;
        if ( roundingPrecision == 64 ) {
            roundIncrement = 0x0000000000000400UL;
            roundMask = 0x00000000000007FFUL;
        } else if ( roundingPrecision == 32 ) {
            roundIncrement = 0x0000008000000000UL;
            roundMask = 0x000000FFFFFFFFFFUL;
        } else {
            goto precision80;
        }
        sig |= b2u( sigExtra != 0 );
        if ( ! roundNearEven && (roundingMode != softfloat_round_near_maxMag) ) {
            roundIncrement =
                (roundingMode
                     == (sign ? softfloat_round_min : softfloat_round_max))
                    ? roundMask
                    : 0;
        }
        roundBits = sig & roundMask;
        /*------------------------------------------------------------------------
        *------------------------------------------------------------------------*/
        if ( 0x7FFD <= (uint32_t) (exp - 1) ) {
            if ( exp <= 0 ) {
                /*----------------------------------------------------------------
                *----------------------------------------------------------------*/
                isTiny =
                       (softfloat_detectTininess
                            == softfloat_tininess_beforeRounding)
                    || (exp < 0)
                    || (sig <= (uint64_t) (sig + roundIncrement));
                sig = softfloat_shiftRightJam64( sig, (uint_fast32_t) (1 - exp) );
                roundBits = sig & roundMask;
                expTrunc = 0; // DEVIATION: C1
                sigTrunc = sig & ~roundMask; // DEVIATION: C1
                if ( roundBits != 0 ) {
                    if ( isTiny ) softfloat_raiseFlags( softfloat_flag_underflow );
                    softfloat_exceptionFlags |= softfloat_flag_inexact;
                    if ( roundingMode == softfloat_round_odd ) {
                        sig |= roundMask + 1;
                    }
                }
                sig += roundIncrement;
                exp = (int_fast32_t) b2u( (sig & 0x8000000000000000UL) != 0 );
                roundIncrement = roundMask + 1;
                if ( roundNearEven && (roundBits<<1 == roundIncrement) ) {
                    roundMask |= roundIncrement;
                }
                sig &= ~roundMask;
                goto packReturn;
            }
            if (
                   (0x7FFE < exp)
                || ((exp == 0x7FFE) && ((uint64_t) (sig + roundIncrement) < sig))
            ) {
                goto overflow;
            }
        }
        /*------------------------------------------------------------------------
        *------------------------------------------------------------------------*/
        expTrunc = exp; // DEVIATION: C1
        sigTrunc = sig & ~roundMask; // DEVIATION: C1
        if ( roundBits != 0 ) {
            softfloat_exceptionFlags |= softfloat_flag_inexact;
            if ( roundingMode == softfloat_round_odd ) {
                sig = (sig & ~roundMask) | (roundMask + 1);
                goto packReturn;
            }
        }
        sig = (uint64_t) (sig + roundIncrement);
        if ( sig < roundIncrement ) {
            ++exp;
            sig = 0x8000000000000000UL;
        }
        roundIncrement = roundMask + 1;
        if ( roundNearEven && (roundBits<<1 == roundIncrement) ) {
            roundMask |= roundIncrement;
        }
        sig &= ~roundMask;
        goto packReturn;
        /*------------------------------------------------------------------------
        *------------------------------------------------------------------------*/
     precision80:
        doIncrement = (0x8000000000000000UL <= sigExtra);
        if ( ! roundNearEven && (roundingMode != softfloat_round_near_maxMag) ) {
            doIncrement =
                (roundingMode
                     == (sign ? softfloat_round_min : softfloat_round_max))
                    && sigExtra != 0;
        }
        /*------------------------------------------------------------------------
        *------------------------------------------------------------------------*/
        if ( 0x7FFD <= (uint32_t) (exp - 1) ) {
            if ( exp <= 0 ) {
                /*----------------------------------------------------------------
                *----------------------------------------------------------------*/
                isTiny =
                       (softfloat_detectTininess
                            == softfloat_tininess_beforeRounding)
                    || (exp < 0)
                    || ! doIncrement
                    || (sig < 0xFFFFFFFFFFFFFFFFUL);
                sig64Extra =
                    softfloat_shiftRightJam64Extra( sig, sigExtra, (uint_fast32_t) (1 - exp) );
                exp = 0;
                sig = sig64Extra.v;
                sigExtra = sig64Extra.extra;
                expTrunc = 0; // DEVIATION: C1
                sigTrunc = sig; // DEVIATION: C1
                if ( sigExtra != 0 ) {
                    if ( isTiny ) softfloat_raiseFlags( softfloat_flag_underflow );
                    softfloat_exceptionFlags |= softfloat_flag_inexact;
                    if ( roundingMode == softfloat_round_odd ) {
                        sig |= 1;
                        goto packReturn;
                    }
                }
                doIncrement = (0x8000000000000000UL <= sigExtra);
                if (
                    ! roundNearEven
                        && (roundingMode != softfloat_round_near_maxMag)
                ) {
                    doIncrement =
                        (roundingMode
                             == (sign ? softfloat_round_min : softfloat_round_max))
                            && sigExtra != 0;
                }
                if ( doIncrement ) {
                    ++sig;
                    sig &=
                        ~(uint_fast64_t)
                             b2u( (sigExtra & 0x7FFFFFFFFFFFFFFFUL) == 0
                                  & roundNearEven );
                    exp = (int_fast32_t) b2u( (sig & 0x8000000000000000UL) != 0 );
                }
                goto packReturn;
            }
            if (
                   (0x7FFE < exp)
                || ((exp == 0x7FFE) && (sig == 0xFFFFFFFFFFFFFFFFUL)
                        && doIncrement)
            ) {
                /*----------------------------------------------------------------
                *----------------------------------------------------------------*/
                roundMask = 0;
                goto overflow;
            }
        }
        /*------------------------------------------------------------------------
        *------------------------------------------------------------------------*/
        expTrunc = exp; // DEVIATION: C1
        sigTrunc = sig; // DEVIATION: C1
        if ( sigExtra != 0 ) {
            softfloat_exceptionFlags |= softfloat_flag_inexact;
            if ( roundingMode == softfloat_round_odd ) {
                sig |= 1;
                goto packReturn;
            }
        }
        if ( doIncrement ) {
            ++sig;
            if ( sig == 0 ) {
                ++exp;
                sig = 0x8000000000000000UL;
            } else {
                sig &=
                    ~(uint_fast64_t)
                         b2u( (sigExtra & 0x7FFFFFFFFFFFFFFFUL) == 0
                              & roundNearEven );
            }
        }
        goto packReturn;
        /*------------------------------------------------------------------------
        | Le bloc du débordement, sorti du `if` à 80 bits (voir l'en-tête).
        *------------------------------------------------------------------------*/
     overflow:
        softfloat_raiseFlags(
            softfloat_flag_overflow | softfloat_flag_inexact );
        expTrunc = 0x7FFE; // DEVIATION: C1 — la troncature d'un débordement est le plus grand fini
        sigTrunc = ~roundMask; // DEVIATION: C1
        if (
               roundNearEven
            || (roundingMode == softfloat_round_near_maxMag)
            || (roundingMode
                    == (sign ? softfloat_round_min : softfloat_round_max))
        ) {
            exp = 0x7FFF;
            sig = 0x8000000000000000UL;
        } else {
            exp = 0x7FFE;
            sig = ~roundMask;
        }
        /*------------------------------------------------------------------------
        *------------------------------------------------------------------------*/
     packReturn:
        softfloat_roundedUp = (sig != sigTrunc) || (exp != expTrunc); // DEVIATION: C1
        uZ.signExp = (uint16_t) packToExtF80UI64( sign, exp );
        uZ.signif = sig;
        return uZ;
    }

    /// <summary>DEVIATION : l'exposant non borné (voir l'en-tête). Arrondit sig × 2^(exp − 0x3FFF − 63), étendu par
    /// sigExtra, à la précision demandée sans borner l'exposant ; rend faux, sans rien toucher, si le résultat reste dans
    /// la plage ou en sort d'un côté que softfloat_unboundedExp ne nomme pas : le chemin borné le traite alors.</summary>
    private static bool roundPackUnbounded(
        bool sign, int_fast32_t exp, uint_fast64_t sig, uint_fast64_t sigExtra, uint_fast8_t roundingPrecision,
        out extFloat80_t uZ)
    {
        uZ = default;
        // Normaliser la valeur, sur 128 bits : un dénormal (exp ≤ 0, ou bit de tête nul) prend son exposant vrai.
        if ( sig == 0 ) {
            sig = sigExtra;
            sigExtra = 0;
            exp -= 64;
        }
        int shift = softfloat_countLeadingZeros64( sig );
        if ( shift != 0 ) {
            sig = sig << shift | sigExtra >> (64 - shift);
            sigExtra <<= shift;
            exp -= shift;
        }
        uint_fast8_t roundingMode = softfloat_roundingMode;
        bool roundNearEven = roundingMode == softfloat_round_near_even;
        bool directed = ! roundNearEven && roundingMode != softfloat_round_near_maxMag;
        bool toward = roundingMode == (sign ? softfloat_round_min : softfloat_round_max);
        bool inexact;
        int_fast32_t expTrunc = exp;
        uint_fast64_t sigTrunc;
        if ( roundingPrecision == 32 || roundingPrecision == 64 ) {
            uint_fast64_t roundMask = roundingPrecision == 64 ? 0x00000000000007FFUL : 0x000000FFFFFFFFFFUL;
            uint_fast64_t roundIncrement = (roundMask + 1) >> 1;
            sig |= b2u( sigExtra != 0 );
            if ( directed ) roundIncrement = toward ? roundMask : 0;
            uint_fast64_t roundBits = sig & roundMask;
            sigTrunc = sig & ~roundMask;
            inexact = roundBits != 0;
            if ( inexact && roundingMode == softfloat_round_odd ) {
                sig = sigTrunc | (roundMask + 1);
            } else {
                sig += roundIncrement;
                if ( sig < roundIncrement ) {
                    ++exp;
                    sig = 0x8000000000000000UL;
                }
                if ( roundNearEven && (roundBits << 1 == roundMask + 1) ) roundMask |= roundMask + 1;
                sig &= ~roundMask;
            }
        } else {
            sigTrunc = sig;
            inexact = sigExtra != 0;
            bool doIncrement = directed ? toward && inexact : 0x8000000000000000UL <= sigExtra;
            if ( inexact && roundingMode == softfloat_round_odd ) {
                sig |= 1;
            } else if ( doIncrement ) {
                ++sig;
                if ( sig == 0 ) {
                    ++exp;
                    sig = 0x8000000000000000UL;
                } else if ( roundNearEven && (sigExtra & 0x7FFFFFFFFFFFFFFFUL) == 0 ) {
                    sig &= ~(uint_fast64_t) 1;
                }
            }
        }
        bool roundedUp = (sig != sigTrunc) || (exp != expTrunc);
        if ( 0x7FFE < exp && (softfloat_unboundedExp & softfloat_flag_overflow) != 0 ) {
            softfloat_raiseFlags( softfloat_flag_overflow );
            exp -= 0x6000;
            if ( 0x7FFE < exp ) {
                // Le décalage ne suffit pas : ±∞, la réponse masquée au plus près.
                softfloat_raiseFlags( softfloat_flag_inexact );
                softfloat_roundedUp = true;
                uZ.signExp = (uint16_t) packToExtF80UI64( sign, 0x7FFF );
                uZ.signif = 0x8000000000000000UL;
                return true;
            }
        } else if ( exp <= 0 && (softfloat_unboundedExp & softfloat_flag_underflow) != 0 ) {
            softfloat_raiseFlags( softfloat_flag_underflow );
            exp += 0x6000;
            if ( exp <= 0 ) {
                // Le décalage ne suffit pas : ±0, la réponse masquée au plus près.
                softfloat_raiseFlags( softfloat_flag_inexact );
                softfloat_roundedUp = false;
                uZ.signExp = (uint16_t) packToExtF80UI64( sign, 0 );
                uZ.signif = 0;
                return true;
            }
        } else {
            return false;
        }
        if ( inexact ) softfloat_exceptionFlags |= softfloat_flag_inexact;
        softfloat_roundedUp = roundedUp; // DEVIATION: C1, sur le résultat arrondi avant le décalage
        uZ.signExp = (uint16_t) packToExtF80UI64( sign, exp );
        uZ.signif = sig;
        return true;
    }
}
