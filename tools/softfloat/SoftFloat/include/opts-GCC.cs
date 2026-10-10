// SPDX-FileCopyrightText: 2017 The Regents of the University of California
// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: BSD-3-Clause
//
// ORACLE: sources/softfloat/SoftFloat-3e/source/include/opts-GCC.h
//         (SoftFloat-3e.zip, sha256 21130ce885d35c1fe73fc1e1bf2244178167e05c6747cad5f450cc991714c746 ; hors dépôt)
//
// La notice d'origine, gardée comme la licence le demande :
//
// This C header file is part of the SoftFloat IEEE Floating-Point Arithmetic
// Package, Release 3e, by John R. Hauser.
//
// Copyright 2017 The Regents of the University of California.  All rights
// reserved.
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
// SOFTFLOAT_BUILTIN_CLZ et SOFTFLOAT_INTRINSIC_INT128, posés par build/Linux-x86_64-GCC/platform.h : __builtin_clzll
// devient BitOperations.LeadingZeroCount (qui rend aussi 64 pour zéro), le produit `unsigned __int128` devient
// Math.BigMul.

using System.Numerics;
using System.Runtime.CompilerServices;

namespace iXtal26.SoftFloat;

internal static partial class softfloat
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint_fast8_t softfloat_countLeadingZeros32( uint32_t a )
        { return (uint_fast8_t) BitOperations.LeadingZeroCount( a ); }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint_fast8_t softfloat_countLeadingZeros64( uint64_t a )
        { return (uint_fast8_t) BitOperations.LeadingZeroCount( a ); }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint128 softfloat_mul64ByShifted32To128( uint64_t a, uint32_t b )
    {
        uint128 uZ;
        uZ.v64 = Math.BigMul( a, (uint_fast64_t) b<<32, out uZ.v0 );
        return uZ;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint128 softfloat_mul64To128( uint64_t a, uint64_t b )
    {
        uint128 uZ;
        uZ.v64 = Math.BigMul( a, b, out uZ.v0 );
        return uZ;
    }
}
