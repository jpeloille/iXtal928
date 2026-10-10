// SPDX-FileCopyrightText: 2011, 2012, 2013, 2014, 2015 The Regents of the University of California
// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: BSD-3-Clause
//
// ORACLE: sources/softfloat/SoftFloat-3e/source/s_subMagsExtF80.c
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
    internal static extFloat80_t
     softfloat_subMagsExtF80(
         uint_fast16_t uiA64,
         uint_fast64_t uiA0,
         uint_fast16_t uiB64,
         uint_fast64_t uiB0,
         bool signZ
     )
    {
        int_fast32_t expA;
        uint_fast64_t sigA;
        int_fast32_t expB;
        uint_fast64_t sigB;
        int_fast32_t expDiff;
        uint_fast16_t uiZ64;
        uint_fast64_t uiZ0;
        int_fast32_t expZ;
        uint_fast64_t sigExtra;
        uint128 sig128, uiZ;
        extFloat80_t uZ;

        /*------------------------------------------------------------------------
        *------------------------------------------------------------------------*/
        expA = expExtF80UI64( uiA64 );
        sigA = uiA0;
        expB = expExtF80UI64( uiB64 );
        sigB = uiB0;
        /*------------------------------------------------------------------------
        *------------------------------------------------------------------------*/
        expDiff = expA - expB;
        if ( 0 < expDiff ) goto expABigger;
        if ( expDiff < 0 ) goto expBBigger;
        if ( expA == 0x7FFF ) {
            if ( ((sigA | sigB) & 0x7FFFFFFFFFFFFFFFUL) != 0 ) {
                goto propagateNaN;
            }
            softfloat_raiseFlags( softfloat_flag_invalid );
            uiZ64 = defaultNaNExtF80UI64;
            uiZ0  = defaultNaNExtF80UI0;
            goto uiZ;
        }
        /*------------------------------------------------------------------------
        *------------------------------------------------------------------------*/
        expZ = expA;
        if ( expZ == 0 ) expZ = 1;
        sigExtra = 0;
        if ( sigB < sigA ) goto aBigger;
        if ( sigA < sigB ) goto bBigger;
        uiZ64 =
            packToExtF80UI64( (softfloat_roundingMode == softfloat_round_min), 0 );
        uiZ0 = 0;
        goto uiZ;
        /*------------------------------------------------------------------------
        *------------------------------------------------------------------------*/
     expBBigger:
        if ( expB == 0x7FFF ) {
            if ( (sigB & 0x7FFFFFFFFFFFFFFFUL) != 0 ) goto propagateNaN;
            uiZ64 = packToExtF80UI64( signZ ^ true, 0x7FFF );
            uiZ0  = 0x8000000000000000UL;
            goto uiZ;
        }
        if ( expA == 0 ) {
            ++expDiff;
            sigExtra = 0;
            if ( expDiff == 0 ) goto newlyAlignedBBigger;
        }
        sig128 = softfloat_shiftRightJam128( sigA, 0, (uint_fast32_t) (-expDiff) );
        sigA = sig128.v64;
        sigExtra = sig128.v0;
     newlyAlignedBBigger:
        expZ = expB;
     bBigger:
        signZ = ! signZ;
        sig128 = softfloat_sub128( sigB, 0, sigA, sigExtra );
        goto normRoundPack;
        /*------------------------------------------------------------------------
        *------------------------------------------------------------------------*/
     expABigger:
        if ( expA == 0x7FFF ) {
            if ( (sigA & 0x7FFFFFFFFFFFFFFFUL) != 0 ) goto propagateNaN;
            uiZ64 = uiA64;
            uiZ0  = uiA0;
            goto uiZ;
        }
        if ( expB == 0 ) {
            --expDiff;
            sigExtra = 0;
            if ( expDiff == 0 ) goto newlyAlignedABigger;
        }
        sig128 = softfloat_shiftRightJam128( sigB, 0, (uint_fast32_t) expDiff );
        sigB = sig128.v64;
        sigExtra = sig128.v0;
     newlyAlignedABigger:
        expZ = expA;
     aBigger:
        sig128 = softfloat_sub128( sigA, 0, sigB, sigExtra );
        /*------------------------------------------------------------------------
        *------------------------------------------------------------------------*/
     normRoundPack:
        return
            softfloat_normRoundPackToExtF80(
                signZ, expZ, sig128.v64, sig128.v0, extF80_roundingPrecision );
        /*------------------------------------------------------------------------
        *------------------------------------------------------------------------*/
     propagateNaN:
        uiZ = softfloat_propagateNaNExtF80UI( uiA64, uiA0, uiB64, uiB0 );
        uiZ64 = uiZ.v64;
        uiZ0  = uiZ.v0;
     uiZ:
        uZ.signExp = (uint16_t) uiZ64;
        uZ.signif  = uiZ0;
        return uZ;
    }
}
