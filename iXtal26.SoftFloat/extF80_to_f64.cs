// SPDX-FileCopyrightText: 2011, 2012, 2013, 2014, 2015 The Regents of the University of California
// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: BSD-3-Clause
//
// ORACLE: sources/softfloat/SoftFloat-3e/source/extF80_to_f64.c
//         (SoftFloat-3e.zip, sha256 21130ce885d35c1fe73fc1e1bf2244178167e05c6747cad5f450cc991714c746 ; hors dépôt)
//
// La notice d'origine, gardée comme la licence le demande :
//
// This C source file is part of the SoftFloat IEEE Floating-Point Arithmetic
// Package, Release 3e, by John R. Hauser.
//
// Copyright 2011, 2012, 2013, 2014, 2015 The Regents of the University of
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
// Le garde `sizeof (int_fast16_t) < sizeof (int_fast32_t)` est faux dans la build de référence (les deux sur 64 bits) :
// son bloc n'est pas réécrit.

namespace iXtal26.SoftFloat;

internal static partial class softfloat
{
    internal static float64_t extF80_to_f64( extFloat80_t a )
    {
        extFloat80_t uA;
        uint_fast16_t uiA64;
        uint_fast64_t uiA0;
        bool sign;
        int_fast32_t exp;
        uint_fast64_t sig;
        commonNaN commonNaN = default;
        uint_fast64_t uiZ;
        float64_t uZ;

        /*------------------------------------------------------------------------
        *------------------------------------------------------------------------*/
        uA = a;
        uiA64 = uA.signExp;
        uiA0  = uA.signif;
        sign = signExtF80UI64( uiA64 );
        exp  = expExtF80UI64( uiA64 );
        sig  = uiA0;
        /*------------------------------------------------------------------------
        *------------------------------------------------------------------------*/
        if ( ((uint_fast64_t) exp | sig) == 0 ) {
            uiZ = packToF64UI( sign, 0, 0 );
            goto uiZ;
        }
        /*------------------------------------------------------------------------
        *------------------------------------------------------------------------*/
        if ( exp == 0x7FFF ) {
            if ( (sig & 0x7FFFFFFFFFFFFFFFUL) != 0 ) {
                softfloat_extF80UIToCommonNaN( uiA64, uiA0, ref commonNaN );
                uiZ = softfloat_commonNaNToF64UI( ref commonNaN );
            } else {
                uiZ = packToF64UI( sign, 0x7FF, 0 );
            }
            goto uiZ;
        }
        /*------------------------------------------------------------------------
        *------------------------------------------------------------------------*/
        sig = softfloat_shortShiftRightJam64( sig, 1 );
        exp -= 0x3C01;
        return softfloat_roundPackToF64( sign, exp, sig );
        /*------------------------------------------------------------------------
        *------------------------------------------------------------------------*/
     uiZ:
        uZ.v = uiZ;
        return uZ;
    }
}
