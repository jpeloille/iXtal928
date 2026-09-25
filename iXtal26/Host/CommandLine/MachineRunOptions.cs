// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun)
// STATUS: host

using iXtal26.Floppy;

namespace iXtal26.Host.CommandLine;

internal sealed class MachineRunOptions
{
    public string RomDirectory { get; set; } = "roms";

    public bool IsHeadless { get; set; }

    public int? SliceLimit { get; set; }

    public bool IsVerbose { get; set; }

    public bool ForcesSetupScreen { get; set; }

    public int? TurboSlices { get; set; }

    public string? ConfigurationPath { get; set; }

    public MachineOverrides Machine { get; } = new();

    public bool IsMachineChosenByArguments =>
        ConfigurationPath is not null || Machine.DescribesMachine ||
        fdd_c.discfns[0].Length != 0 || fdd_c.discfns[1].Length != 0;

    public bool ShowsSetupScreen =>
        (ForcesSetupScreen || !IsMachineChosenByArguments) && !IsHeadless && SliceLimit is null;
}
