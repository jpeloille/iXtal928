// SPDX-FileCopyrightText: 2011, 2012, 2013, 2014, 2015, 2016, 2017 The Regents of the University of California
// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: BSD-3-Clause
//
// ORACLE: sources/softfloat/SoftFloat-3e/source/include/softfloat.h
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
// Les énumérations de l'interface, en constantes de la classe. L'état (softfloat_roundingMode et les autres) est dans
// softfloat_state.cs ; THREAD_LOCAL est vide dans la build de référence, l'état est global comme là.

namespace iXtal26.SoftFloat;

internal static partial class softfloat
{
    internal const uint_fast8_t softfloat_tininess_beforeRounding = 0;
    internal const uint_fast8_t softfloat_tininess_afterRounding  = 1;

    internal const uint_fast8_t softfloat_round_near_even   = 0;
    internal const uint_fast8_t softfloat_round_minMag      = 1;
    internal const uint_fast8_t softfloat_round_min         = 2;
    internal const uint_fast8_t softfloat_round_max         = 3;
    internal const uint_fast8_t softfloat_round_near_maxMag = 4;
    internal const uint_fast8_t softfloat_round_odd         = 6;

    internal const uint_fast8_t softfloat_flag_inexact   =  1;
    internal const uint_fast8_t softfloat_flag_underflow =  2;
    internal const uint_fast8_t softfloat_flag_overflow  =  4;
    internal const uint_fast8_t softfloat_flag_infinite  =  8;
    internal const uint_fast8_t softfloat_flag_invalid   = 16;
}
