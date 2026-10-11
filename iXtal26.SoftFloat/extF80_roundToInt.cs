// SPDX-FileCopyrightText: 2011, 2012, 2013, 2014, 2017 The Regents of the University of California
// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: BSD-3-Clause
//
// ORACLE: sources/softfloat/SoftFloat-3e/source/extF80_roundToInt.c
//         (SoftFloat-3e.zip, sha256 21130ce885d35c1fe73fc1e1bf2244178167e05c6747cad5f450cc991714c746 ; hors dépôt)
//
// La notice d'origine, gardée comme la licence le demande :
//
// This C source file is part of the SoftFloat IEEE Floating-Point Arithmetic
// Package, Release 3e, by John R. Hauser.
//
// Copyright 2011, 2012, 2013, 2014, 2017 The Regents of the University of
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
// Le `switch` du C tombe d'un cas dans le suivant (near_even dans near_maxMag) : `goto case` en C#.
//
// DEVIATION (G13, point n° 10, validée par Julien le 11/10) — C1 : faux d'entrée ; vrai quand |x| < 1 est rendu à 1 ; sinon, la mantisse et l'exposant rendus comparés
// à la troncature (la retenue de 1,xxx vers 2 passe dans l'exposant ; softfloat_state.cs). Rien d'autre ne change.

namespace iXtal26.SoftFloat;

internal static partial class softfloat
{
    internal static extFloat80_t
     extF80_roundToInt( extFloat80_t a, uint_fast8_t roundingMode, bool exact )
    {
        extFloat80_t uA;
        uint_fast16_t uiA64, signUI64;
        int_fast32_t exp;
        uint_fast64_t sigA;
        uint_fast16_t uiZ64;
        uint_fast64_t sigZ;
        exp32_sig64 normExpSig;
        uint128 uiZ;
        uint_fast64_t lastBitMask, roundBitsMask;
        extFloat80_t uZ;

        softfloat_roundedUp = false; // DEVIATION: C1
        /*------------------------------------------------------------------------
        *------------------------------------------------------------------------*/
        uA = a;
        uiA64 = uA.signExp;
        signUI64 = uiA64 & packToExtF80UI64( true, 0 );
        exp = expExtF80UI64( uiA64 );
        sigA = uA.signif;
        /*------------------------------------------------------------------------
        *------------------------------------------------------------------------*/
        if ( (sigA & 0x8000000000000000UL) == 0 && (exp != 0x7FFF) ) {
            if ( sigA == 0 ) {
                uiZ64 = signUI64;
                sigZ = 0;
                goto uiZ;
            }
            normExpSig = softfloat_normSubnormalExtF80Sig( sigA );
            exp += normExpSig.exp;
            sigA = normExpSig.sig;
        }
        /*------------------------------------------------------------------------
        *------------------------------------------------------------------------*/
        if ( 0x403E <= exp ) {
            if ( exp == 0x7FFF ) {
                if ( (sigA & 0x7FFFFFFFFFFFFFFFUL) != 0 ) {
                    uiZ = softfloat_propagateNaNExtF80UI( uiA64, sigA, 0, 0 );
                    uiZ64 = uiZ.v64;
                    sigZ  = uiZ.v0;
                    goto uiZ;
                }
                sigZ = 0x8000000000000000UL;
            } else {
                sigZ = sigA;
            }
            uiZ64 = signUI64 | (uint_fast16_t) exp;
            goto uiZ;
        }
        if ( exp <= 0x3FFE ) {
            if ( exact ) softfloat_exceptionFlags |= softfloat_flag_inexact;
            switch ( roundingMode ) {
             case softfloat_round_near_even:
                if ( (sigA & 0x7FFFFFFFFFFFFFFFUL) == 0 ) break;
                goto case softfloat_round_near_maxMag;
             case softfloat_round_near_maxMag:
                if ( exp == 0x3FFE ) goto mag1;
                break;
             case softfloat_round_min:
                if ( signUI64 != 0 ) goto mag1;
                break;
             case softfloat_round_max:
                if ( signUI64 == 0 ) goto mag1;
                break;
             case softfloat_round_odd:
                goto mag1;
            }
            uiZ64 = signUI64;
            sigZ  = 0;
            goto uiZ;
         mag1:
            softfloat_roundedUp = true; // DEVIATION: C1 — |x| < 1 rendu à 1
            uiZ64 = signUI64 | 0x3FFF;
            sigZ  = 0x8000000000000000UL;
            goto uiZ;
        }
        /*------------------------------------------------------------------------
        *------------------------------------------------------------------------*/
        uiZ64 = signUI64 | (uint_fast16_t) exp;
        lastBitMask = (uint_fast64_t) 1<<(int) (0x403E - exp);
        roundBitsMask = lastBitMask - 1;
        sigZ = sigA;
        if ( roundingMode == softfloat_round_near_maxMag ) {
            sigZ += lastBitMask>>1;
        } else if ( roundingMode == softfloat_round_near_even ) {
            sigZ += lastBitMask>>1;
            if ( (sigZ & roundBitsMask) == 0 ) sigZ &= ~lastBitMask;
        } else if (
            roundingMode == (signUI64 != 0 ? softfloat_round_min : softfloat_round_max)
        ) {
            sigZ += roundBitsMask;
        }
        sigZ &= ~roundBitsMask;
        if ( sigZ == 0 ) {
            ++uiZ64;
            sigZ = 0x8000000000000000UL;
        }
        if ( sigZ != sigA ) {
            if ( roundingMode == softfloat_round_odd ) sigZ |= lastBitMask;
            if ( exact ) softfloat_exceptionFlags |= softfloat_flag_inexact;
        }
        softfloat_roundedUp = sigZ != (sigA & ~roundBitsMask) || uiZ64 != (signUI64 | (uint_fast16_t) exp); // DEVIATION: C1
     uiZ:
        uZ.signExp = (uint16_t) uiZ64;
        uZ.signif = sigZ;
        return uZ;
    }
}
