// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — infrastructure de transcription, pas de contrepartie C.
//
// Alias des types stdint de C. Leur raison d'être est que la signature C# d'une
// fonction transcrite soit caractère pour caractère celle du C :
//
//     static uint16_t readmemw(uint32_t s, uint16_t a)          <- C
//     static uint16_t readmemw(uint32_t s, uint16_t a)          <- C#
//
// Effet de bord voulu : la largeur est visible à chaque déclaration et à chaque
// paramètre. C'est la première des trois défenses contre le risque n°2 du plan
// (divergence de largeur et de signe), parce que le bon cast se lit sur la
// signature au lieu de se déduire.
//
// uintptr_t n'a délibérément PAS d'alias : il n'apparaît dans PCem que là où un
// pointeur hôte est stocké (readlookup2/writelookup2, _mem_exec), et chacun de ces
// sites se résout en un offset `int` dans un tableau managé. L'absence d'alias est
// l'application de la règle.

global using uint8_t = System.Byte;
global using uint16_t = System.UInt16;
global using uint32_t = System.UInt32;
global using uint64_t = System.UInt64;
global using int8_t = System.SByte;
global using int16_t = System.Int16;
global using int32_t = System.Int32;
global using int64_t = System.Int64;
