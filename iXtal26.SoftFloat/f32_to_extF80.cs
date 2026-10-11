// SPDX-FileCopyrightText: 2011, 2012, 2013, 2014, 2015 The Regents of the University of California
// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: BSD-3-Clause
//
// ORACLE: sources/softfloat/SoftFloat-3e/source/f32_to_extF80.c
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

namespace iXtal26.SoftFloat;

internal static partial class softfloat
{
    internal static extFloat80_t f32_to_extF80( float32_t a )
    {
        float32_t uA;
        uint_fast32_t uiA;
        bool sign;
        int_fast16_t exp;
        uint_fast32_t frac;
        commonNaN commonNaN = default;
        uint128 uiZ;
        uint_fast16_t uiZ64;
        uint_fast64_t uiZ0;
        exp16_sig32 normExpSig;
        extFloat80_t uZ;

        /*------------------------------------------------------------------------
        *------------------------------------------------------------------------*/
        uA = a;
        uiA = uA.v;
        sign = signF32UI( uiA );
        exp  = expF32UI( uiA );
        frac = fracF32UI( uiA );
        /*------------------------------------------------------------------------
        *------------------------------------------------------------------------*/
        if ( exp == 0xFF ) {
            if ( frac != 0 ) {
                softfloat_f32UIToCommonNaN( uiA, ref commonNaN );
                uiZ = softfloat_commonNaNToExtF80UI( ref commonNaN );
                uiZ64 = uiZ.v64;
                uiZ0  = uiZ.v0;
            } else {
                uiZ64 = packToExtF80UI64( sign, 0x7FFF );
                uiZ0  = 0x8000000000000000UL;
            }
            goto uiZ;
        }
        /*------------------------------------------------------------------------
        *------------------------------------------------------------------------*/
        if ( exp == 0 ) {
            if ( frac == 0 ) {
                uiZ64 = packToExtF80UI64( sign, 0 );
                uiZ0  = 0;
                goto uiZ;
            }
            normExpSig = softfloat_normSubnormalF32Sig( frac );
            exp = normExpSig.exp;
            frac = normExpSig.sig;
        }
        /*------------------------------------------------------------------------
        *------------------------------------------------------------------------*/
        uiZ64 = packToExtF80UI64( sign, exp + 0x3F80 );
        uiZ0  = (uint_fast64_t) (frac | 0x00800000)<<40;
     uiZ:
        uZ.signExp = (uint16_t) uiZ64;
        uZ.signif  = uiZ0;
        return uZ;
    }
}
