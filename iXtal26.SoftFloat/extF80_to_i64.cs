// SPDX-FileCopyrightText: 2011, 2012, 2013, 2014, 2015, 2016, 2017 The Regents of the University of California
// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: BSD-3-Clause
//
// ORACLE: sources/softfloat/SoftFloat-3e/source/extF80_to_i64.c
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

namespace iXtal26.SoftFloat;

internal static partial class softfloat
{
    internal static int_fast64_t
     extF80_to_i64( extFloat80_t a, uint_fast8_t roundingMode, bool exact )
    {
        extFloat80_t uA;
        uint_fast16_t uiA64;
        bool sign;
        int_fast32_t exp;
        uint_fast64_t sig;
        int_fast32_t shiftDist;
        uint_fast64_t sigExtra;
        uint64_extra sig64Extra;

        /*------------------------------------------------------------------------
        *------------------------------------------------------------------------*/
        uA = a;
        uiA64 = uA.signExp;
        sign = signExtF80UI64( uiA64 );
        exp  = expExtF80UI64( uiA64 );
        sig = uA.signif;
        /*------------------------------------------------------------------------
        *------------------------------------------------------------------------*/
        shiftDist = 0x403E - exp;
        if ( shiftDist <= 0 ) {
            /*--------------------------------------------------------------------
            *--------------------------------------------------------------------*/
            if ( shiftDist != 0 ) {
                softfloat_raiseFlags( softfloat_flag_invalid );
                return
                    (exp == 0x7FFF) && (sig & 0x7FFFFFFFFFFFFFFFUL) != 0
                        ? i64_fromNaN
                        : sign ? i64_fromNegOverflow : i64_fromPosOverflow;
            }
            /*--------------------------------------------------------------------
            *--------------------------------------------------------------------*/
            sigExtra = 0;
        } else {
            /*--------------------------------------------------------------------
            *--------------------------------------------------------------------*/
            sig64Extra = softfloat_shiftRightJam64Extra( sig, 0, (uint_fast32_t) shiftDist );
            sig = sig64Extra.v;
            sigExtra = sig64Extra.extra;
        }
        return softfloat_roundToI64( sign, sig, sigExtra, roundingMode, exact );
    }
}
