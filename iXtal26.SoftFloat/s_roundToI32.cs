// SPDX-FileCopyrightText: 2011, 2012, 2013, 2014, 2015, 2016, 2017 The Regents of the University of California
// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: BSD-3-Clause
//
// ORACLE: sources/softfloat/SoftFloat-3e/source/s_roundToI32.c
//         (SoftFloat-3e.zip, sha256 21130ce885d35c1fe73fc1e1bf2244178167e05c6747cad5f450cc991714c746 ; hors dépôt)
//
// La notice d'origine, gardée comme la licence le demande :
//
// This C source file is part of the SoftFloat IEEE Floating-Point Arithmetic
// Package, Release 3e, by John R. Hauser.
//
// Copyright 2011, 2012, 2013, 2014, 2015, 2016, 2017 The Regents of the
// University of California.  All rights reserved.
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
// DEVIATION (G13, point n° 10, validée par Julien le 11/10) — C1 : la magnitude entière avant l'incrément, comparée à celle d'après (softfloat_state.cs) ;
// faux sur l'invalide. Rien d'autre ne change.

namespace iXtal26.SoftFloat;

internal static partial class softfloat
{
    internal static int_fast32_t
     softfloat_roundToI32(
         bool sign, uint_fast64_t sig, uint_fast8_t roundingMode, bool exact )
    {
        uint_fast16_t roundIncrement, roundBits;
        uint_fast32_t sig32;
        uint32_t uZ;
        int_fast32_t z;
        uint_fast64_t sigTrunc; // DEVIATION: C1

        /*------------------------------------------------------------------------
        *------------------------------------------------------------------------*/
        roundIncrement = 0x800;
        if (
            (roundingMode != softfloat_round_near_maxMag)
                && (roundingMode != softfloat_round_near_even)
        ) {
            roundIncrement = 0;
            if (
                sign
                    ? (roundingMode == softfloat_round_min)
                          || (roundingMode == softfloat_round_odd)
                    : (roundingMode == softfloat_round_max)
            ) {
                roundIncrement = 0xFFF;
            }
        }
        roundBits = sig & 0xFFF;
        sigTrunc = sig>>12; // DEVIATION: C1
        sig += roundIncrement;
        if ( (sig & 0xFFFFF00000000000UL) != 0 ) goto invalid;
        sig32 = sig>>12;
        if (
            (roundBits == 0x800) && (roundingMode == softfloat_round_near_even)
        ) {
            sig32 &= ~(uint_fast32_t) 1;
        }
        softfloat_roundedUp = sig32 != sigTrunc; // DEVIATION: C1
        uZ = (uint32_t) (sign ? 0 - sig32 : sig32);
        z = (int32_t) uZ;
        if ( z != 0 && ((z < 0) ^ sign) ) goto invalid;
        if ( roundBits != 0 ) {
            if ( roundingMode == softfloat_round_odd ) z |= 1;
            if ( exact ) softfloat_exceptionFlags |= softfloat_flag_inexact;
        }
        return z;
        /*------------------------------------------------------------------------
        *------------------------------------------------------------------------*/
     invalid:
        softfloat_roundedUp = false; // DEVIATION: C1
        softfloat_raiseFlags( softfloat_flag_invalid );
        return sign ? i32_fromNegOverflow : i32_fromPosOverflow;
    }
}
