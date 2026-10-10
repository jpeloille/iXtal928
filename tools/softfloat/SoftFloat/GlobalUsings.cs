// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — infrastructure de la réécriture, pas de contrepartie C.
//
// Les types de stdint, comme iXtal26/GlobalUsings.cs, pour que chaque signature se lise comme celle du C. Les types
// « fast » prennent la largeur qu'ils ont dans la build de référence (build/Linux-x86_64-GCC, glibc) : uint_fast8_t
// et int_fast8_t sur 8 bits, les fast16 et fast32 sur 64 bits, comme les fast64. Ce n'est pas un détail : SoftFloat
// laisse des valeurs déborder dans ces largeurs (le quotient `q` d'extF80_div, un uint_fast32_t, passe sous zéro par
// `--q` ; `roundIncrement` d'softfloat_roundPackToF32 est un uint_fast8_t), et la réécriture doit déborder de même.

global using uint16_t = System.UInt16;
global using uint32_t = System.UInt32;
global using uint64_t = System.UInt64;
global using int32_t = System.Int32;
global using int64_t = System.Int64;

global using uint_fast8_t = System.Byte;
global using int_fast8_t = System.SByte;
global using uint_fast16_t = System.UInt64;
global using int_fast16_t = System.Int64;
global using uint_fast32_t = System.UInt64;
global using int_fast32_t = System.Int64;
global using uint_fast64_t = System.UInt64;
global using int_fast64_t = System.Int64;
