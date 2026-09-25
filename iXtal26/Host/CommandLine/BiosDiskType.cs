// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun)
// STATUS: host

namespace iXtal26.Host.CommandLine;

internal static class BiosDiskType
{
    public static int Highest => HddImage.hd_types.Length;

    public static bool IsInRange(int biosType) => biosType >= 1 && biosType <= Highest;
}
