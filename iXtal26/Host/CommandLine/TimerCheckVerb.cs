// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun)
// STATUS: host

using iXtal26.PluginApi;

namespace iXtal26.Host.CommandLine;

internal static class TimerCheckVerb
{
    private const string DefaultRomDirectory = "roms";
    private const int DefaultEmulatedSeconds = 300;

    public static ExitCode Run(ArgumentCursor cursor)
    {
        var romDirectory = cursor.HasNext ? cursor.TakeNext() : DefaultRomDirectory;
        var emulatedSeconds = DefaultEmulatedSeconds;

        if (cursor.HasNext && !cursor.NextIsOption &&
            (!int.TryParse(cursor.TakeNext(), out emulatedSeconds) || emulatedSeconds <= 0))
            return Failure.Usage("--timer-check attend un nombre de secondes émulées entier positif.");

        var machine = new MachineOverrides();
        var measuresRamLoop = false;
        var bootSlices = TimerCheck.DefaultBootSlices;

        while (cursor.NextIsOption)
        {
            var option = cursor.TakeNext();

            if (!cursor.HasNext)
                return Failure.Usage($"{option} attend un argument.");

            var value = cursor.TakeNext();

            switch (option)
            {
                case "--model":
                    machine.Model = value;
                    break;
                case "--gfxcard":
                    machine.GraphicsCard = value;
                    break;
                case "--charge":
                    if (value is not ("ram" or "repos"))
                        return Failure.Usage("--charge attend « repos » ou « ram ».");
                    measuresRamLoop = value == "ram";
                    break;
                case "--cpu":
                    if (!int.TryParse(value, out var processorIndex) || processorIndex < 0)
                        return Failure.Usage("--cpu attend l'indice du processeur dans la table de la machine.");
                    machine.ProcessorIndex = processorIndex;
                    break;
                case "--floppy-a" or "--floppy-b":
                    if (!DriveMounter.TryMountFloppy(option == "--floppy-a" ? 0 : 1, value))
                        return ExitCode.UsageError;
                    break;
                case "--boot-slices":
                    if (!int.TryParse(value, out bootSlices) || bootSlices <= 0)
                        return Failure.Usage("--boot-slices attend un nombre de tranches entier positif.");
                    break;
                case "--config":
                    if (!ConfigurationFile.TryLoadNow(value))
                        return ExitCode.UsageError;
                    break;
                default:
                    return Failure.Usage($"Option inconnue après --timer-check : {option}");
            }
        }

        if (!machine.TryApplyMachineAndCheckProcessor())
            return ExitCode.UsageError;

        return (ExitCode)TimerCheck.Run(paths.resolve_roms_path(romDirectory), emulatedSeconds, bootSlices,
                                        measuresRamLoop);
    }
}
