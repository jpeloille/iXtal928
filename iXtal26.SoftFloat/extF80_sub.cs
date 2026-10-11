// SPDX-FileCopyrightText: 2011, 2012, 2013, 2014 The Regents of the University of California
// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: BSD-3-Clause
//
// ORACLE: sources/softfloat/SoftFloat-3e/source/extF80_sub.c
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
    internal static extFloat80_t extF80_sub( extFloat80_t a, extFloat80_t b )
    {
        extFloat80_t uA;
        uint_fast16_t uiA64;
        uint_fast64_t uiA0;
        bool signA;
        extFloat80_t uB;
        uint_fast16_t uiB64;
        uint_fast64_t uiB0;
        bool signB;

        uA = a;
        uiA64 = uA.signExp;
        uiA0  = uA.signif;
        signA = signExtF80UI64( uiA64 );
        uB = b;
        uiB64 = uB.signExp;
        uiB0  = uB.signif;
        signB = signExtF80UI64( uiB64 );
        // INLINE_LEVEL 5 : l'appel direct, sans le pointeur de fonction.
        if ( signA == signB ) {
            return softfloat_subMagsExtF80( uiA64, uiA0, uiB64, uiB0, signA );
        } else {
            return softfloat_addMagsExtF80( uiA64, uiA0, uiB64, uiB0, signA );
        }
    }
}
