// SPDX-FileCopyrightText: 2011, 2012, 2013, 2014, 2018 The Regents of the University of California
// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: BSD-3-Clause
//
// ORACLE: sources/softfloat/SoftFloat-3e/source/8086/s_propagateNaNExtF80UI.c
//         (SoftFloat-3e.zip, sha256 21130ce885d35c1fe73fc1e1bf2244178167e05c6747cad5f450cc991714c746 ; hors dépôt)
//
// La notice d'origine, gardée comme la licence le demande :
//
// This C source file is part of the SoftFloat IEEE Floating-Point Arithmetic
// Package, Release 3e, by John R. Hauser.
//
// Copyright 2011, 2012, 2013, 2014, 2018 The Regents of the University of
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
    internal static uint128
     softfloat_propagateNaNExtF80UI(
         uint_fast16_t uiA64,
         uint_fast64_t uiA0,
         uint_fast16_t uiB64,
         uint_fast64_t uiB0
     )
    {
        bool isSigNaNA, isSigNaNB;
        uint_fast64_t uiNonsigA0, uiNonsigB0;
        uint_fast16_t uiMagA64, uiMagB64;
        uint128 uiZ;

        /*------------------------------------------------------------------------
        *------------------------------------------------------------------------*/
        isSigNaNA = softfloat_isSigNaNExtF80UI( uiA64, uiA0 );
        isSigNaNB = softfloat_isSigNaNExtF80UI( uiB64, uiB0 );
        /*------------------------------------------------------------------------
        | Make NaNs non-signaling.
        *------------------------------------------------------------------------*/
        uiNonsigA0 = uiA0 | 0xC000000000000000UL;
        uiNonsigB0 = uiB0 | 0xC000000000000000UL;
        /*------------------------------------------------------------------------
        *------------------------------------------------------------------------*/
        if ( isSigNaNA | isSigNaNB ) {
            softfloat_raiseFlags( softfloat_flag_invalid );
            if ( isSigNaNA ) {
                if ( isSigNaNB ) goto returnLargerMag;
                if ( isNaNExtF80UI( uiB64, uiB0 ) ) goto returnB;
                goto returnA;
            } else {
                if ( isNaNExtF80UI( uiA64, uiA0 ) ) goto returnA;
                goto returnB;
            }
        }
     returnLargerMag:
        uiMagA64 = uiA64 & 0x7FFF;
        uiMagB64 = uiB64 & 0x7FFF;
        if ( uiMagA64 < uiMagB64 ) goto returnB;
        if ( uiMagB64 < uiMagA64 ) goto returnA;
        if ( uiA0 < uiB0 ) goto returnB;
        if ( uiB0 < uiA0 ) goto returnA;
        if ( uiA64 < uiB64 ) goto returnA;
     returnB:
        uiZ.v64 = uiB64;
        uiZ.v0  = uiNonsigB0;
        return uiZ;
     returnA:
        uiZ.v64 = uiA64;
        uiZ.v0  = uiNonsigA0;
        return uiZ;
    }
}
