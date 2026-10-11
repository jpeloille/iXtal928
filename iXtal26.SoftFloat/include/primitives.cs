// SPDX-FileCopyrightText: 2011, 2012, 2013, 2014, 2015, 2016, 2017 The Regents of the University of California
// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: BSD-3-Clause
//
// ORACLE: sources/softfloat/SoftFloat-3e/source/include/primitives.h
//         (SoftFloat-3e.zip, sha256 21130ce885d35c1fe73fc1e1bf2244178167e05c6747cad5f450cc991714c746 ; hors dépôt)
//
// La notice d'origine, gardée comme la licence le demande :
//
// This C header file is part of the SoftFloat IEEE Floating-Point Arithmetic
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
//
// INLINE_LEVEL=5, SOFTFLOAT_FAST_INT64, SOFTFLOAT_FAST_DIV64TO32 : les primitives que les fichiers réécrits appellent,
// inlinées comme dans la build de référence. Celles que opts-GCC.h remplace (le compte des zéros de tête, les
// produits) sont dans opts-GCC.cs. Un décalage C de 64 ou plus est indéfini, mais aucune de ces fonctions n'en
// fait ; C# masque le compte à 63 comme le SHR du x86.

using System.Runtime.CompilerServices;

namespace iXtal26.SoftFloat;

internal static partial class softfloat
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint64_t softfloat_shortShiftRightJam64( uint64_t a, uint_fast8_t dist )
        { return a>>dist | b2u( (a & (((uint_fast64_t) 1<<dist) - 1)) != 0 ); }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint32_t softfloat_shiftRightJam32( uint32_t a, uint_fast16_t dist )
    {
        return
            (dist < 31)
                ? a>>(int) dist | (uint32_t) b2u( (uint32_t) (a<<(int) ((0 - dist) & 31)) != 0 )
                : (uint32_t) b2u( a != 0 );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint64_t softfloat_shiftRightJam64( uint64_t a, uint_fast32_t dist )
    {
        return
            (dist < 63)
                ? a>>(int) dist | b2u( (uint64_t) (a<<(int) ((0 - dist) & 63)) != 0 )
                : b2u( a != 0 );
    }

    /// <summary>SOFTFLOAT_FAST_DIV64TO32 : une macro en C.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint32_t softfloat_approxRecip32_1( uint_fast64_t a )
        => (uint32_t) (0x7FFFFFFFFFFFFFFFUL / (uint32_t) a);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool softfloat_le128( uint64_t a64, uint64_t a0, uint64_t b64, uint64_t b0 )
        { return (a64 < b64) || ((a64 == b64) && (a0 <= b0)); }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool softfloat_lt128( uint64_t a64, uint64_t a0, uint64_t b64, uint64_t b0 )
        { return (a64 < b64) || ((a64 == b64) && (a0 < b0)); }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint128 softfloat_shortShiftLeft128( uint64_t a64, uint64_t a0, uint_fast8_t dist )
    {
        uint128 z;
        z.v64 = a64<<dist | a0>>(-dist & 63);
        z.v0 = a0<<dist;
        return z;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint64_extra softfloat_shortShiftRightJam64Extra( uint64_t a, uint64_t extra, uint_fast8_t dist )
    {
        uint64_extra z;
        z.v = a>>dist;
        z.extra = a<<(-dist & 63) | b2u( extra != 0 );
        return z;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint64_extra softfloat_shiftRightJam64Extra( uint64_t a, uint64_t extra, uint_fast32_t dist )
    {
        uint64_extra z;
        if ( dist < 64 ) {
            z.v = a>>(int) dist;
            z.extra = a<<(int) ((0 - dist) & 63);
        } else {
            z.v = 0;
            z.extra = (dist == 64) ? a : b2u( a != 0 );
        }
        z.extra |= b2u( extra != 0 );
        return z;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint128 softfloat_add128( uint64_t a64, uint64_t a0, uint64_t b64, uint64_t b0 )
    {
        uint128 z;
        z.v0 = a0 + b0;
        z.v64 = a64 + b64 + b2u( z.v0 < a0 );
        return z;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint128 softfloat_sub128( uint64_t a64, uint64_t a0, uint64_t b64, uint64_t b0 )
    {
        uint128 z;
        z.v0 = a0 - b0;
        z.v64 = a64 - b64;
        z.v64 -= b2u( a0 < b0 );
        return z;
    }
}
