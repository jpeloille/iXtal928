// SPDX-FileCopyrightText: 2011, 2012, 2013, 2014 The Regents of the University of California
// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: BSD-3-Clause
//
// ORACLE: sources/softfloat/SoftFloat-3e/source/extF80_mul.c
//         (SoftFloat-3e.zip, sha256 21130ce885d35c1fe73fc1e1bf2244178167e05c6747cad5f450cc991714c746 ; hors dépôt)
//
// La notice d'origine, gardée comme la licence le demande :
//
// This C source file is part of the SoftFloat IEEE Floating-Point Arithmetic
// Package, Release 3e, by John R. Hauser.
//
// Copyright 2011, 2012, 2013, 2014 The Regents of the University of California.
// All rights reserved.
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
    internal static extFloat80_t extF80_mul( extFloat80_t a, extFloat80_t b )
    {
        extFloat80_t uA;
        uint_fast16_t uiA64;
        uint_fast64_t uiA0;
        bool signA;
        int_fast32_t expA;
        uint_fast64_t sigA;
        extFloat80_t uB;
        uint_fast16_t uiB64;
        uint_fast64_t uiB0;
        bool signB;
        int_fast32_t expB;
        uint_fast64_t sigB;
        bool signZ;
        uint_fast64_t magBits;
        exp32_sig64 normExpSig;
        int_fast32_t expZ;
        uint128 sig128Z, uiZ;
        uint_fast16_t uiZ64;
        uint_fast64_t uiZ0;
        extFloat80_t uZ;

        /*------------------------------------------------------------------------
        *------------------------------------------------------------------------*/
        uA = a;
        uiA64 = uA.signExp;
        uiA0  = uA.signif;
        signA = signExtF80UI64( uiA64 );
        expA  = expExtF80UI64( uiA64 );
        sigA  = uiA0;
        uB = b;
        uiB64 = uB.signExp;
        uiB0  = uB.signif;
        signB = signExtF80UI64( uiB64 );
        expB  = expExtF80UI64( uiB64 );
        sigB  = uiB0;
        signZ = signA ^ signB;
        /*------------------------------------------------------------------------
        *------------------------------------------------------------------------*/
        if ( expA == 0x7FFF ) {
            if (
                   (sigA & 0x7FFFFFFFFFFFFFFFUL) != 0
                || ((expB == 0x7FFF) && (sigB & 0x7FFFFFFFFFFFFFFFUL) != 0)
            ) {
                goto propagateNaN;
            }
            magBits = (uint_fast64_t) expB | sigB;
            goto infArg;
        }
        if ( expB == 0x7FFF ) {
            if ( (sigB & 0x7FFFFFFFFFFFFFFFUL) != 0 ) goto propagateNaN;
            magBits = (uint_fast64_t) expA | sigA;
            goto infArg;
        }
        /*------------------------------------------------------------------------
        *------------------------------------------------------------------------*/
        if ( expA == 0 ) expA = 1;
        if ( (sigA & 0x8000000000000000UL) == 0 ) {
            if ( sigA == 0 ) goto zero;
            normExpSig = softfloat_normSubnormalExtF80Sig( sigA );
            expA += normExpSig.exp;
            sigA = normExpSig.sig;
        }
        if ( expB == 0 ) expB = 1;
        if ( (sigB & 0x8000000000000000UL) == 0 ) {
            if ( sigB == 0 ) goto zero;
            normExpSig = softfloat_normSubnormalExtF80Sig( sigB );
            expB += normExpSig.exp;
            sigB = normExpSig.sig;
        }
        /*------------------------------------------------------------------------
        *------------------------------------------------------------------------*/
        expZ = expA + expB - 0x3FFE;
        sig128Z = softfloat_mul64To128( sigA, sigB );
        if ( sig128Z.v64 < 0x8000000000000000UL ) {
            --expZ;
            sig128Z =
                softfloat_add128(
                    sig128Z.v64, sig128Z.v0, sig128Z.v64, sig128Z.v0 );
        }
        return
            softfloat_roundPackToExtF80(
                signZ, expZ, sig128Z.v64, sig128Z.v0, extF80_roundingPrecision );
        /*------------------------------------------------------------------------
        *------------------------------------------------------------------------*/
     propagateNaN:
        uiZ = softfloat_propagateNaNExtF80UI( uiA64, uiA0, uiB64, uiB0 );
        uiZ64 = uiZ.v64;
        uiZ0  = uiZ.v0;
        goto uiZ;
        /*------------------------------------------------------------------------
        *------------------------------------------------------------------------*/
     infArg:
        if ( magBits == 0 ) {
            softfloat_raiseFlags( softfloat_flag_invalid );
            uiZ64 = defaultNaNExtF80UI64;
            uiZ0  = defaultNaNExtF80UI0;
        } else {
            uiZ64 = packToExtF80UI64( signZ, 0x7FFF );
            uiZ0  = 0x8000000000000000UL;
        }
        goto uiZ;
        /*------------------------------------------------------------------------
        *------------------------------------------------------------------------*/
     zero:
        uiZ64 = packToExtF80UI64( signZ, 0 );
        uiZ0  = 0;
     uiZ:
        uZ.signExp = (uint16_t) uiZ64;
        uZ.signif  = uiZ0;
        return uZ;
    }
}
