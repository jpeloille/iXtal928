// SPDX-FileCopyrightText: 2011, 2012, 2013, 2014, 2015, 2017 The Regents of the University of California
// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: BSD-3-Clause
//
// ORACLE: sources/softfloat/SoftFloat-3e/source/s_roundPackToF64.c
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

namespace iXtal26.SoftFloat;

internal static partial class softfloat
{
    internal static float64_t
     softfloat_roundPackToF64( bool sign, int_fast16_t exp, uint_fast64_t sig )
    {
        uint_fast8_t roundingMode;
        bool roundNearEven;
        uint_fast16_t roundIncrement, roundBits;
        bool isTiny;
        uint_fast64_t uiZ;
        float64_t uZ;

        /*------------------------------------------------------------------------
        *------------------------------------------------------------------------*/
        roundingMode = softfloat_roundingMode;
        roundNearEven = (roundingMode == softfloat_round_near_even);
        roundIncrement = 0x200;
        if ( ! roundNearEven && (roundingMode != softfloat_round_near_maxMag) ) {
            roundIncrement =
                (roundingMode
                     == (sign ? softfloat_round_min : softfloat_round_max))
                    ? 0x3FFUL
                    : 0;
        }
        roundBits = sig & 0x3FF;
        /*------------------------------------------------------------------------
        *------------------------------------------------------------------------*/
        if ( 0x7FD <= (uint16_t) exp ) {
            if ( exp < 0 ) {
                /*----------------------------------------------------------------
                *----------------------------------------------------------------*/
                isTiny =
                    (softfloat_detectTininess == softfloat_tininess_beforeRounding)
                        || (exp < -1)
                        || (sig + roundIncrement < 0x8000000000000000UL);
                sig = softfloat_shiftRightJam64( sig, (uint_fast32_t) (-exp) );
                exp = 0;
                roundBits = sig & 0x3FF;
                if ( isTiny && roundBits != 0 ) {
                    softfloat_raiseFlags( softfloat_flag_underflow );
                }
            } else if (
                (0x7FD < exp)
                    || (0x8000000000000000UL <= sig + roundIncrement)
            ) {
                /*----------------------------------------------------------------
                *----------------------------------------------------------------*/
                softfloat_raiseFlags(
                    softfloat_flag_overflow | softfloat_flag_inexact );
                uiZ = packToF64UI( sign, 0x7FF, 0 ) - b2u( roundIncrement == 0 );
                goto uiZ;
            }
        }
        /*------------------------------------------------------------------------
        *------------------------------------------------------------------------*/
        sig = (sig + roundIncrement)>>10;
        if ( roundBits != 0 ) {
            softfloat_exceptionFlags |= softfloat_flag_inexact;
            if ( roundingMode == softfloat_round_odd ) {
                sig |= 1;
                goto packReturn;
            }
        }
        sig &= ~(uint_fast64_t) b2u( (roundBits ^ 0x200) == 0 & roundNearEven );
        if ( sig == 0 ) exp = 0;
        /*------------------------------------------------------------------------
        *------------------------------------------------------------------------*/
     packReturn:
        uiZ = packToF64UI( sign, exp, sig );
     uiZ:
        uZ.v = uiZ;
        return uZ;
    }
}
