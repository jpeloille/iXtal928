// SPDX-FileCopyrightText: 2011, 2012, 2013, 2014, 2015, 2017 The Regents of the University of California
// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: BSD-3-Clause
//
// ORACLE: sources/softfloat/SoftFloat-3e/source/s_roundPackToF32.c
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
// DEVIATION (G13, point n° 10, validée par Julien le 11/10) — C1 : la troncature est la mantisse décalée sans l'incrément ; packReturn note si le
// résultat en diffère, le débordement si l'infini est rendu (softfloat_state.cs). Rien d'autre ne change.

namespace iXtal26.SoftFloat;

internal static partial class softfloat
{
    internal static float32_t
     softfloat_roundPackToF32( bool sign, int_fast16_t exp, uint_fast32_t sig )
    {
        uint_fast8_t roundingMode;
        bool roundNearEven;
        uint_fast8_t roundIncrement, roundBits;
        bool isTiny;
        uint_fast64_t sigTrunc; // DEVIATION: C1
        uint_fast32_t uiZ;
        float32_t uZ;

        /*------------------------------------------------------------------------
        *------------------------------------------------------------------------*/
        roundingMode = softfloat_roundingMode;
        roundNearEven = (roundingMode == softfloat_round_near_even);
        roundIncrement = 0x40;
        if ( ! roundNearEven && (roundingMode != softfloat_round_near_maxMag) ) {
            roundIncrement =
                (uint_fast8_t) ((roundingMode
                     == (sign ? softfloat_round_min : softfloat_round_max))
                    ? 0x7F
                    : 0);
        }
        roundBits = (uint_fast8_t) (sig & 0x7F);
        /*------------------------------------------------------------------------
        *------------------------------------------------------------------------*/
        if ( 0xFD <= (uint) exp ) {
            if ( exp < 0 ) {
                /*----------------------------------------------------------------
                *----------------------------------------------------------------*/
                isTiny =
                    (softfloat_detectTininess == softfloat_tininess_beforeRounding)
                        || (exp < -1) || (sig + roundIncrement < 0x80000000);
                sig = softfloat_shiftRightJam32( (uint32_t) sig, (uint_fast16_t) (-exp) );
                exp = 0;
                roundBits = (uint_fast8_t) (sig & 0x7F);
                if ( isTiny && roundBits != 0 ) {
                    softfloat_raiseFlags( softfloat_flag_underflow );
                }
            } else if ( (0xFD < exp) || (0x80000000 <= sig + roundIncrement) ) {
                /*----------------------------------------------------------------
                *----------------------------------------------------------------*/
                softfloat_raiseFlags(
                    softfloat_flag_overflow | softfloat_flag_inexact );
                softfloat_roundedUp = roundIncrement != 0; // DEVIATION: C1 — l'infini, pas le plus grand fini
                uiZ = packToF32UI( sign, 0xFF, 0 ) - b2u( roundIncrement == 0 );
                goto uiZ;
            }
        }
        /*------------------------------------------------------------------------
        *------------------------------------------------------------------------*/
        sigTrunc = sig>>7; // DEVIATION: C1
        sig = (sig + roundIncrement)>>7;
        if ( roundBits != 0 ) {
            softfloat_exceptionFlags |= softfloat_flag_inexact;
            if ( roundingMode == softfloat_round_odd ) {
                sig |= 1;
                goto packReturn;
            }
        }
        sig &= ~(uint_fast32_t) b2u( (roundBits ^ 0x40) == 0 & roundNearEven );
        if ( sig == 0 ) exp = 0;
        /*------------------------------------------------------------------------
        *------------------------------------------------------------------------*/
     packReturn:
        softfloat_roundedUp = sig != sigTrunc; // DEVIATION: C1
        uiZ = packToF32UI( sign, exp, sig );
     uiZ:
        uZ.v = (uint32_t) uiZ;
        return uZ;
    }
}
