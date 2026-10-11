// SPDX-FileCopyrightText: 2011, 2012, 2013, 2014, 2015, 2016, 2017 The Regents of the University of California
// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: BSD-3-Clause
//
// ORACLE: sources/softfloat/SoftFloat-3e/source/extF80_rem.c
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
    internal static extFloat80_t extF80_rem( extFloat80_t a, extFloat80_t b )
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
        int_fast32_t expB;
        uint_fast64_t sigB;
        exp32_sig64 normExpSig;
        int_fast32_t expDiff;
        uint128 rem, shiftedSigB;
        uint_fast32_t q, recip32;
        uint_fast64_t q64;
        uint128 term, altRem, meanRem;
        bool signRem;
        uint128 uiZ;
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
        expB  = expExtF80UI64( uiB64 );
        sigB  = uiB0;
        /*------------------------------------------------------------------------
        *------------------------------------------------------------------------*/
        if ( expA == 0x7FFF ) {
            if (
                   (sigA & 0x7FFFFFFFFFFFFFFFUL) != 0
                || ((expB == 0x7FFF) && (sigB & 0x7FFFFFFFFFFFFFFFUL) != 0)
            ) {
                goto propagateNaN;
            }
            goto invalid;
        }
        if ( expB == 0x7FFF ) {
            if ( (sigB & 0x7FFFFFFFFFFFFFFFUL) != 0 ) goto propagateNaN;
            /*--------------------------------------------------------------------
            | Argument b is an infinity.  Doubling `expB' is an easy way to ensure
            | that `expDiff' later is less than -1, which will result in returning
            | a canonicalized version of argument a.
            *--------------------------------------------------------------------*/
            expB += expB;
        }
        /*------------------------------------------------------------------------
        *------------------------------------------------------------------------*/
        if ( expB == 0 ) expB = 1;
        if ( (sigB & 0x8000000000000000UL) == 0 ) {
            if ( sigB == 0 ) goto invalid;
            normExpSig = softfloat_normSubnormalExtF80Sig( sigB );
            expB += normExpSig.exp;
            sigB = normExpSig.sig;
        }
        if ( expA == 0 ) expA = 1;
        if ( (sigA & 0x8000000000000000UL) == 0 ) {
            if ( sigA == 0 ) {
                expA = 0;
                goto copyA;
            }
            normExpSig = softfloat_normSubnormalExtF80Sig( sigA );
            expA += normExpSig.exp;
            sigA = normExpSig.sig;
        }
        /*------------------------------------------------------------------------
        *------------------------------------------------------------------------*/
        expDiff = expA - expB;
        if ( expDiff < -1 ) goto copyA;
        rem = softfloat_shortShiftLeft128( 0, sigA, 32 );
        shiftedSigB = softfloat_shortShiftLeft128( 0, sigB, 32 );
        if ( expDiff < 1 ) {
            if ( expDiff != 0 ) {
                --expB;
                shiftedSigB = softfloat_shortShiftLeft128( 0, sigB, 33 );
                q = 0;
            } else {
                q = b2u( sigB <= sigA );
                if ( q != 0 ) {
                    rem =
                        softfloat_sub128(
                            rem.v64, rem.v0, shiftedSigB.v64, shiftedSigB.v0 );
                }
            }
        } else {
            recip32 = softfloat_approxRecip32_1( sigB>>32 );
            expDiff -= 30;
            for (;;) {
                q64 = (uint_fast64_t) (uint32_t) (rem.v64>>2) * recip32;
                if ( expDiff < 0 ) break;
                q = (q64 + 0x80000000)>>32;
                rem = softfloat_shortShiftLeft128( rem.v64, rem.v0, 29 );
                term = softfloat_mul64ByShifted32To128( sigB, (uint32_t) q );
                rem = softfloat_sub128( rem.v64, rem.v0, term.v64, term.v0 );
                if ( (rem.v64 & 0x8000000000000000UL) != 0 ) {
                    rem =
                        softfloat_add128(
                            rem.v64, rem.v0, shiftedSigB.v64, shiftedSigB.v0 );
                }
                expDiff -= 29;
            }
            /*--------------------------------------------------------------------
            | (`expDiff' cannot be less than -29 here.)
            *--------------------------------------------------------------------*/
            q = (uint32_t) (q64>>32)>>(int) (~expDiff & 31);
            rem = softfloat_shortShiftLeft128( rem.v64, rem.v0, (uint_fast8_t) (expDiff + 30) );
            term = softfloat_mul64ByShifted32To128( sigB, (uint32_t) q );
            rem = softfloat_sub128( rem.v64, rem.v0, term.v64, term.v0 );
            if ( (rem.v64 & 0x8000000000000000UL) != 0 ) {
                altRem =
                    softfloat_add128(
                        rem.v64, rem.v0, shiftedSigB.v64, shiftedSigB.v0 );
                goto selectRem;
            }
        }
        /*------------------------------------------------------------------------
        *------------------------------------------------------------------------*/
        do {
            altRem = rem;
            ++q;
            rem =
                softfloat_sub128(
                    rem.v64, rem.v0, shiftedSigB.v64, shiftedSigB.v0 );
        } while ( (rem.v64 & 0x8000000000000000UL) == 0 );
     selectRem:
        meanRem = softfloat_add128( rem.v64, rem.v0, altRem.v64, altRem.v0 );
        if (
            (meanRem.v64 & 0x8000000000000000UL) != 0
                || ((meanRem.v64 | meanRem.v0) == 0 && (q & 1) != 0)
        ) {
            rem = altRem;
        }
        signRem = signA;
        if ( (rem.v64 & 0x8000000000000000UL) != 0 ) {
            signRem = ! signRem;
            rem = softfloat_sub128( 0, 0, rem.v64, rem.v0 );
        }
        return
            softfloat_normRoundPackToExtF80(
                signRem, (rem.v64 | rem.v0) != 0 ? expB + 32 : 0, rem.v64, rem.v0, 80 );
        /*------------------------------------------------------------------------
        *------------------------------------------------------------------------*/
     propagateNaN:
        uiZ = softfloat_propagateNaNExtF80UI( uiA64, uiA0, uiB64, uiB0 );
        uiZ64 = uiZ.v64;
        uiZ0  = uiZ.v0;
        goto uiZ;
        /*------------------------------------------------------------------------
        *------------------------------------------------------------------------*/
     invalid:
        softfloat_raiseFlags( softfloat_flag_invalid );
        uiZ64 = defaultNaNExtF80UI64;
        uiZ0  = defaultNaNExtF80UI0;
        goto uiZ;
        /*------------------------------------------------------------------------
        *------------------------------------------------------------------------*/
     copyA:
        if ( expA < 1 ) {
            sigA >>= (int) (1 - expA);
            expA = 0;
        }
        uiZ64 = packToExtF80UI64( signA, expA );
        uiZ0  = sigA;
     uiZ:
        uZ.signExp = (uint16_t) uiZ64;
        uZ.signif  = uiZ0;
        return uZ;
    }
}
