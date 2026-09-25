// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun)
// STATUS: host

using iXtal26.Cpu;

namespace iXtal26.Host.CommandLine;

internal sealed class MachineOverrides
{
    private const int DriveCount = 2;

    private readonly int?[] floppyDriveTypes = new int?[DriveCount];
    private readonly string?[] hardDiskImagePaths = new string?[DriveCount];
    private readonly int?[] forcedHardDiskTypes = new int?[DriveCount];

    public string? Model { get; set; }

    public string? GraphicsCard { get; set; }

    public int? ProcessorIndex { get; set; }

    public int? MemoryKilobytes { get; set; }

    public bool DescribesMachine =>
        Model is not null || ProcessorIndex is not null || MemoryKilobytes is not null ||
        floppyDriveTypes[0] is not null || floppyDriveTypes[1] is not null ||
        hardDiskImagePaths[0] is not null || hardDiskImagePaths[1] is not null;

    public void SetFloppyDriveType(int drive, int driveType) => floppyDriveTypes[drive] = driveType;

    public void SetHardDiskImage(int drive, string imagePath) => hardDiskImagePaths[drive] = imagePath;

    public void ForceHardDiskType(int drive, int biosType) => forcedHardDiskTypes[drive] = biosType;

    public bool TryApplyModelGraphicsCardAndProcessor()
    {
        if (Model is not null && !pc.setmodel(Model))
            return false;

        if (GraphicsCard is not null && !pc.setgfxcard(GraphicsCard))
            return false;

        if (ProcessorIndex is { } processorIndex)
        {
            cpu_c.cpu_manufacturer = 0;
            cpu_c.cpu = processorIndex;
        }

        return true;
    }

    public bool TryApplyMachineAndCheckProcessor() =>
        TryApplyModelGraphicsCardAndProcessor() && pc.check_cpu();

    public bool TryApplyMemorySize()
    {
        if (MemoryKilobytes is not { } memoryKilobytes)
            return true;

        if (!pc.check_mem_size(memoryKilobytes))
            return false;

        pc.cfg_mem_size = memoryKilobytes;
        return true;
    }

    public void ApplyFloppyDriveTypes()
    {
        for (var drive = 0; drive < DriveCount; drive++)
        {
            if (floppyDriveTypes[drive] is { } driveType)
                pc.cfg_drive_type[drive] = driveType;
        }
    }

    public bool TryMountHardDisks()
    {
        for (var drive = 0; drive < DriveCount; drive++)
        {
            var imagePath = hardDiskImagePaths[drive];
            var forcedType = forcedHardDiskTypes[drive];

            if (imagePath is null && forcedType is not null)
            {
                var suffix = DriveMounter.HardDiskOptionSuffix(drive);
                Console.Error.WriteLine($"--hdd{suffix}-type sans --hdd{suffix} : rien à typer.");
                return false;
            }

            if (imagePath is not null && !DriveMounter.TryMountHardDisk(drive, imagePath, forcedType))
                return false;
        }

        return true;
    }
}
