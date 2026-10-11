// SPDX-FileCopyrightText: 2011, 2012, 2013, 2014, 2015, 2016 The Regents of the University of California
// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: BSD-3-Clause
//
// ORACLE: sources/softfloat/SoftFloat-3e/source/i32_to_extF80.c
//         (SoftFloat-3e.zip, sha256 21130ce885d35c1fe73fc1e1bf2244178167e05c6747cad5f450cc991714c746 ; hors dépôt)
//
// La notice d'origine, gardée comme la licence le demande :
//
// This C source file is part of the SoftFloat IEEE Floating-Point Arithmetic
// Package, Release 3e, by John R. Hauser.
//
// Copyright 2011, 2012, 2013, 2014, 2015, 2016 The Regents of the University of
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
    internal static extFloat80_t i32_to_extF80( int32_t a )
    {
        uint_fast16_t uiZ64;
        uint_fast32_t absA;
        bool sign;
        int_fast8_t shiftDist;
        extFloat80_t uZ;

        uiZ64 = 0;
        absA = 0;
        if ( a != 0 ) {
            sign = (a < 0);
            absA = sign ? 0 - (uint_fast32_t) a : (uint_fast32_t) a;
            shiftDist = (int_fast8_t) softfloat_countLeadingZeros32( (uint32_t) absA );
            uiZ64 = packToExtF80UI64( sign, 0x401E - shiftDist );
            absA <<= shiftDist;
        }
        uZ.signExp = (uint16_t) uiZ64;
        uZ.signif = (uint_fast64_t) absA<<32;
        return uZ;
    }
}
