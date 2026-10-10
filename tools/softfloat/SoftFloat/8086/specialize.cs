// SPDX-FileCopyrightText: 2011, 2012, 2013, 2014, 2015, 2016, 2018 The Regents of the University of California
// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: BSD-3-Clause
//
// ORACLE: sources/softfloat/SoftFloat-3e/source/8086/specialize.h
//         (SoftFloat-3e.zip, sha256 21130ce885d35c1fe73fc1e1bf2244178167e05c6747cad5f450cc991714c746 ; hors dépôt)
//
// La notice d'origine, gardée comme la licence le demande :
//
// This C header file is part of the SoftFloat IEEE Floating-Point Arithmetic
// Package, Release 3e, by John R. Hauser.
//
// Copyright 2011, 2012, 2013, 2014, 2015, 2016, 2018 The Regents of the
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
//
// La variante « 8086 » : les règles du x87 pour les NaN, l'indéfini, et la petitesse après l'arrondi. Les parties
// f16 et f128 ne sont pas réécrites.

using System.Runtime.CompilerServices;

namespace iXtal26.SoftFloat;

internal struct commonNaN
{
    internal bool sign;
    internal uint64_t v0, v64;
}

internal static partial class softfloat
{
    internal const uint_fast8_t init_detectTininess = softfloat_tininess_afterRounding;

    internal const int_fast32_t i32_fromPosOverflow = -0x7FFFFFFF - 1;
    internal const int_fast32_t i32_fromNegOverflow = -0x7FFFFFFF - 1;
    internal const int_fast32_t i32_fromNaN         = -0x7FFFFFFF - 1;
    internal const int_fast64_t i64_fromPosOverflow = -0x7FFFFFFFFFFFFFFF - 1;
    internal const int_fast64_t i64_fromNegOverflow = -0x7FFFFFFFFFFFFFFF - 1;
    internal const int_fast64_t i64_fromNaN         = -0x7FFFFFFFFFFFFFFF - 1;

    internal const uint32_t defaultNaNF32UI = 0xFFC00000;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool softfloat_isSigNaNF32UI( uint_fast32_t uiA )
        => ((uiA & 0x7FC00000) == 0x7F800000) && (uiA & 0x003FFFFF) != 0;

    internal const uint64_t defaultNaNF64UI = 0xFFF8000000000000UL;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool softfloat_isSigNaNF64UI( uint_fast64_t uiA )
        => ((uiA & 0x7FF8000000000000UL) == 0x7FF0000000000000UL) && (uiA & 0x0007FFFFFFFFFFFFUL) != 0;

    internal const uint_fast16_t defaultNaNExtF80UI64 = 0xFFFF;
    internal const uint64_t defaultNaNExtF80UI0 = 0xC000000000000000UL;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool softfloat_isSigNaNExtF80UI( uint_fast16_t uiA64, uint_fast64_t uiA0 )
        => ((uiA64 & 0x7FFF) == 0x7FFF) && (uiA0 & 0x4000000000000000UL) == 0
           && (uiA0 & 0x3FFFFFFFFFFFFFFFUL) != 0;
}
