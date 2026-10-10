// SPDX-FileCopyrightText: 2011, 2012, 2013, 2014, 2015, 2016, 2017 The Regents of the University of California
// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: BSD-3-Clause
//
// ORACLE: sources/softfloat/SoftFloat-3e/source/include/internals.h
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
// Les macros deviennent des méthodes inlinées. Les parties f16 et f128, et la variante sans SOFTFLOAT_FAST_INT64, ne
// sont pas réécrites : le x87 n'en a pas l'usage. Les prototypes des fonctions sont dans leurs fichiers.
//
// `b2u` n'a pas de contrepartie C : c'est la conversion implicite d'un bool en entier (0 ou 1) que C fait seul.

using System.Runtime.CompilerServices;

namespace iXtal26.SoftFloat;

internal struct exp16_sig32 { internal int_fast16_t exp; internal uint_fast32_t sig; }
internal struct exp16_sig64 { internal int_fast16_t exp; internal uint_fast64_t sig; }
internal struct exp32_sig64 { internal int_fast32_t exp; internal uint64_t sig; }

internal static partial class softfloat
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint_fast64_t b2u( bool b ) => b ? 1UL : 0UL;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool signF32UI( uint_fast32_t a ) => ((uint32_t) a>>31) != 0;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int_fast16_t expF32UI( uint_fast32_t a ) => (int_fast16_t) (a>>23) & 0xFF;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint_fast32_t fracF32UI( uint_fast32_t a ) => a & 0x007FFFFF;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint_fast64_t packToF32UI( bool sign, int_fast16_t exp, uint_fast32_t sig )
        => ((uint32_t) b2u( sign )<<31) + ((uint32_t) exp<<23) + sig;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool signF64UI( uint_fast64_t a ) => ((uint64_t) a>>63) != 0;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int_fast16_t expF64UI( uint_fast64_t a ) => (int_fast16_t) (a>>52) & 0x7FF;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint_fast64_t fracF64UI( uint_fast64_t a ) => a & 0x000FFFFFFFFFFFFFUL;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint64_t packToF64UI( bool sign, int_fast16_t exp, uint_fast64_t sig )
        => (uint64_t) ((b2u( sign )<<63) + ((uint_fast64_t) exp<<52) + sig);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool signExtF80UI64( uint_fast16_t a64 ) => ((uint16_t) a64>>15) != 0;
    /// <summary>Le C rend le type de `a64` (uint_fast16_t) ; la valeur tient sur 15 bits, et chaque appel la range
    /// dans un int_fast32_t.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int_fast32_t expExtF80UI64( uint_fast16_t a64 ) => (int_fast32_t) (a64 & 0x7FFF);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint_fast16_t packToExtF80UI64( bool sign, int_fast32_t exp )
        => b2u( sign )<<15 | (uint_fast16_t) exp;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool isNaNExtF80UI( uint_fast16_t a64, uint_fast64_t a0 )
        => ((a64 & 0x7FFF) == 0x7FFF) && (a0 & 0x7FFFFFFFFFFFFFFFUL) != 0;
}
