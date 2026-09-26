// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun)
// STATUS: host

using iXtal26.PluginApi;

namespace iXtal26.Host.CommandLine;

internal static class BootVerb
{
    private const string DefaultRomDirectory = "roms";
    private const int DefaultSliceCount = 20;

    public static ExitCode Run(ArgumentCursor cursor)
    {
        var romDirectory = cursor.NextIsPositional ? cursor.TakeNext() : DefaultRomDirectory;
        var sliceCount = DefaultSliceCount;

        if (cursor.NextIsPositional && (!int.TryParse(cursor.TakeNext(), out sliceCount) || sliceCount <= 0))
            return Failure.Usage("--boot attend un nombre de tranches entier positif.");

        var typedLines = new List<string>();
        var machine = new MachineOverrides();
        var settleSlices = KeyScript.SlicesAfterLine;

        while (cursor.NextIsOption)
        {
            var option = cursor.TakeNext();

            if (!cursor.HasNext)
                return Failure.Usage($"{option} attend un argument.");

            var value = cursor.TakeNext();

            switch (option)
            {
                case "--type":
                    typedLines.Add(value);
                    break;
                case "--floppy-a" or "--floppy-b":
                    if (!DriveMounter.TryMountFloppy(option == "--floppy-a" ? 0 : 1, value))
                        return ExitCode.UsageError;
                    break;
                case "--model":
                    machine.Model = value;
                    break;
                case "--gfxcard":
                    machine.GraphicsCard = value;
                    break;
                case "--cpu":
                    if (!int.TryParse(value, out var processorIndex) || processorIndex < 0)
                        return Failure.Usage("--cpu attend l'indice du processeur dans la table de la machine.");
                    machine.ProcessorIndex = processorIndex;
                    break;
                case "--hdd":
                    machine.SetHardDiskImage(0, value);
                    break;
                case "--hdd-controller":
                    machine.HardDiskController = value;
                    break;
                case "--hdd-type":
                    if (!int.TryParse(value, out var biosType) || !BiosDiskType.IsInRange(biosType))
                        return Failure.Usage("--hdd-type attend un type de disque du BIOS, de 1 à 46.");
                    machine.ForceHardDiskType(0, biosType);
                    break;
                case "--settle":
                    if (!int.TryParse(value, out settleSlices) || settleSlices < 0)
                        return Failure.Usage("--settle attend un entier positif.");
                    break;
                case "--config":
                    if (!ConfigurationFile.TryLoadNow(value))
                        return ExitCode.UsageError;
                    break;
                default:
                    return Failure.Usage($"Option inconnue après --boot : {option}");
            }
        }

        if (!machine.TryApplyMachineAndCheckProcessor() || !machine.TryApplyHardDiskController() ||
            !machine.TryMountHardDisks())
            return ExitCode.UsageError;

        return (ExitCode)BootTest.Run(paths.resolve_roms_path(romDirectory), sliceCount, typedLines, settleSlices);
    }
}
