// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/wx-ui/wx-sdl2.c:444-479 (pc_main)
// STATUS: host

using System.Globalization;
using iXtal26.Models;
using iXtal26.PluginApi;

namespace iXtal26.Host.CommandLine;

internal static class Launcher
{
    private static ExitCode? KeepParsing => null;

    public static ExitCode Run(string[] arguments)
    {
        FiletImages.Poser();
        var cursor = new ArgumentCursor(arguments);
        var options = new MachineRunOptions();

        while (cursor.MoveNext())
        {
            if (ApplyArgument(cursor, options) is { } earlyExit)
                return earlyExit;
        }

        return RunMachine(options);
    }

    private static ExitCode? ApplyArgument(ArgumentCursor cursor, MachineRunOptions options)
    {
        var argument = cursor.Current;

        switch (argument)
        {
            case "-h" or "--help":
                UsageText.Print();
                return ExitCode.Success;
            case "--boot":
                return BootVerb.Run(cursor);
            case "--timer-check":
                return TimerCheckVerb.Run(cursor);
            case "--create-hdd":
                return CreateHardDiskVerb.Run(cursor, options.RomDirectory);
            case "--create-floppy":
                return CreateFloppyVerb.Run(cursor, options.RomDirectory);
            case "--floppy-put":
                return FloppyPutVerb.Run(cursor);
            case "--make-nvr":
                return MakeNvrVerb.Run(cursor, options.RomDirectory);
            case "--setup-check":
                return (ExitCode)SdlSetup.SelfCheck(paths.resolve_roms_path(options.RomDirectory));
            case "--menu-check":
                return (ExitCode)SdlMenu.SelfCheck(paths.resolve_roms_path(options.RomDirectory));
            case "--fat-check":
                return (ExitCode)FatImage.SelfCheck();
            case "--speaker-check":
                return (ExitCode)SpeakerModel.SelfCheck(cursor.HasNext ? cursor.TakeNext() : null);
            case "--joystick-check":
                return (ExitCode)SdlJoystick.SelfCheck();
            case "--floppy-a" or "--floppy-b":
                return MountFloppy(argument, cursor);
            case "--hdd" or "--hdd-d":
                return CollectHardDiskImage(argument, cursor, options.Machine);
            case "--hdd-type" or "--hdd-d-type":
                return CollectForcedHardDiskType(argument, cursor, options.Machine);
            case "--config":
                return CollectConfigurationPath(cursor, options);
            case "--ram" or "--drive-a" or "--drive-b":
                return CollectMemoryOrDriveType(argument, cursor, options.Machine);
            case "--model":
                if (!cursor.HasNext)
                    return Failure.Usage("--model attend un nom de machine.");
                options.Machine.Model = cursor.TakeNext();
                return KeepParsing;
            case "--hdd-controller":
                if (!cursor.HasNext)
                    return Failure.Usage("--hdd-controller attend un nom de contrôleur de disque dur.");
                options.Machine.HardDiskController = cursor.TakeNext();
                return KeepParsing;
            case "--zip":
                if (!cursor.HasNext)
                    return Failure.Usage("--zip attend le chemin d'une image ZIP de 100 663 296 octets.");
                options.Machine.ZipImage = cursor.TakeNext();
                return KeepParsing;
            case "--gfxcard":
                if (!cursor.HasNext)
                    return Failure.Usage("--gfxcard attend un nom de carte vidéo.");
                options.Machine.GraphicsCard = cursor.TakeNext();
                return KeepParsing;
            case "--cpu":
                if (!cursor.TryTakeInt32(out var processorIndex) || processorIndex < 0)
                    return Failure.Usage("--cpu attend l'indice du processeur dans la table de la machine (0, 1, …).");
                options.Machine.ProcessorIndex = processorIndex;
                return KeepParsing;
            case "--rom-path":
                if (!cursor.HasNext)
                    return Failure.Usage("--rom-path attend un chemin de répertoire.");
                options.RomDirectory = cursor.TakeNext();
                return KeepParsing;
            case "--slices":
                if (!cursor.TryTakeInt32(out var sliceLimit) || sliceLimit < 1)
                    return Failure.Usage("--slices attend un entier strictement positif.");
                options.SliceLimit = sliceLimit;
                return KeepParsing;
            case "--turbo":
                return CollectTurboSlices(cursor, options);
            case "--pixel-mm":
                if (!cursor.HasNext ||
                    !double.TryParse(cursor.TakeNext(), NumberStyles.Float, CultureInfo.InvariantCulture,
                                     out var pixelMillimetres) ||
                    pixelMillimetres is <= 0 or > 2)
                    return Failure.Usage("--pixel-mm attend la taille d'un pixel de l'écran hôte en mm (ex. 0.16 ou 0.27).");
                options.Display.PixelMmOverride = pixelMillimetres;
                return KeepParsing;
            case "--crt":
                options.Display.ScanlinesOverride = true;
                return KeepParsing;
            case "--host-diagonal":
                if (!cursor.HasNext ||
                    !double.TryParse(cursor.TakeNext(), NumberStyles.Float, CultureInfo.InvariantCulture,
                                     out var hostDiagonal) ||
                    hostDiagonal is <= 0 or > 200)
                    return Failure.Usage("--host-diagonal attend la diagonale de l'écran hôte en pouces (ex. 27).");
                options.Display.HostDiagonalOverride = hostDiagonal;
                return KeepParsing;
            case "--fill":
                if (!cursor.TryTakeInt32(out var fillPercent) ||
                    fillPercent is < DisplaySettings.MinFillPercent or > DisplaySettings.MaxFillPercent)
                    return Failure.Usage(
                        $"--fill attend un pourcentage de {DisplaySettings.MinFillPercent} à {DisplaySettings.MaxFillPercent}.");
                options.Display.FillPercentOverride = fillPercent;
                return KeepParsing;
            case "--monitor":
                if (!cursor.HasNext || !DisplaySettings.TryParseMonitor(cursor.TakeNext(), out var monitor))
                    return Failure.Usage("--monitor attend auto, nec3v, 14, 15, 17 ou entier.");
                options.Display.MonitorOverride = monitor;
                return KeepParsing;
            case "--headless":
                options.IsHeadless = true;
                return KeepParsing;
            case "--setup":
                options.ForcesSetupScreen = true;
                return KeepParsing;
            case "--verbose":
                options.IsVerbose = true;
                return KeepParsing;
            case "--frames":
                return Failure.Usage(
                    "--frames a été retiré : utiliser --slices N.",
                    "Une tranche vaut 10 ms émulées, pas une image : les deux comptes diffèrent.");
            default:
                Console.Error.WriteLine($"Argument inconnu : {argument}");
                UsageText.Print();
                return ExitCode.UsageError;
        }
    }

    private static ExitCode? MountFloppy(string argument, ArgumentCursor cursor)
    {
        if (!cursor.HasNext)
            return Failure.Usage($"{argument} attend le chemin d'une image de disquette.");

        return DriveMounter.TryMountFloppy(argument == "--floppy-a" ? 0 : 1, cursor.TakeNext())
            ? KeepParsing
            : ExitCode.UsageError;
    }

    private static ExitCode? CollectHardDiskImage(string argument, ArgumentCursor cursor, MachineOverrides machine)
    {
        if (!cursor.HasNext)
            return Failure.Usage($"{argument} attend le chemin d'une image de disque dur.");

        machine.SetHardDiskImage(argument == "--hdd" ? 0 : 1, cursor.TakeNext());
        return KeepParsing;
    }

    private static ExitCode? CollectForcedHardDiskType(string argument, ArgumentCursor cursor,
                                                       MachineOverrides machine)
    {
        if (!cursor.TryTakeInt32(out var biosType) || !BiosDiskType.IsInRange(biosType))
        {
            return Failure.Usage(
                $"{argument} attend un type de disque du BIOS, de 1 à 46.",
                "--create-hdd sans argument les liste tous.");
        }

        machine.ForceHardDiskType(argument == "--hdd-type" ? 0 : 1, biosType);
        return KeepParsing;
    }

    private static ExitCode? CollectConfigurationPath(ArgumentCursor cursor, MachineRunOptions options)
    {
        if (!cursor.HasNext)
            return Failure.Usage("--config attend le chemin d'un fichier de configuration.");

        if (!ConfigurationFile.TryResolve(cursor.TakeNext(), out var configurationPath))
            return ExitCode.UsageError;

        options.ConfigurationPath = configurationPath;
        return KeepParsing;
    }

    private static ExitCode? CollectMemoryOrDriveType(string argument, ArgumentCursor cursor,
                                                      MachineOverrides machine)
    {
        if (!cursor.TryTakeInt32(out var value) || value < 0)
            return Failure.Usage($"{argument} attend un entier positif.");

        switch (argument)
        {
            case "--ram":
                machine.MemoryKilobytes = value;
                break;
            case "--drive-a":
                machine.SetFloppyDriveType(0, value);
                break;
            default:
                machine.SetFloppyDriveType(1, value);
                break;
        }

        return KeepParsing;
    }

    private static ExitCode? CollectTurboSlices(ArgumentCursor cursor, MachineRunOptions options)
    {
        options.TurboSlices = SdlHost.DefaultTurboSlices;

        if (!cursor.NextIsValue)
            return KeepParsing;

        if (!int.TryParse(cursor.TakeNext(), out var turboSlices) || turboSlices < 1)
            return Failure.Usage("--turbo attend un nombre de tranches entier positif.");

        options.TurboSlices = turboSlices;
        return KeepParsing;
    }

    private static ExitCode RunMachine(MachineRunOptions options)
    {
        if (options.IsHeadless && options.SliceLimit is null)
            return Failure.Usage("--headless exige --slices N : sans fenêtre, rien ne peut demander l'arrêt.");

        if (options.TurboSlices is not null && options.SliceLimit is not null)
            return Failure.Usage("--turbo et --slices sont incompatibles : --slices n'attend jamais l'horloge murale.");

        var romDirectory = paths.resolve_roms_path(options.RomDirectory);

        if (!TryConfigureMachine(options))
            return ExitCode.UsageError;

        if (options.IsVerbose)
            PrintMachineSummary(options.ConfigurationPath);

        var showsSetupScreen = options.ShowsSetupScreen;

        if (!showsSetupScreen && !pc.check_cpu())
            return ExitCode.UsageError;

        using var host = new SdlHost(romDirectory, options.IsHeadless, options.SliceLimit ?? 0, options.IsVerbose,
                                     options.TurboSlices ?? 0, options.Display);

        if (!host.Init(showsSetupScreen))
            return host.SetupCancelled ? ExitCode.Success : ExitCode.RuntimeFailure;

        return (ExitCode)host.Run();
    }

    private static bool TryConfigureMachine(MachineRunOptions options)
    {
        if (options.ConfigurationPath is not null && !pc.loadconfig(options.ConfigurationPath))
            return false;

        options.Display.ConfigPath = options.ConfigurationPath;

        var machine = options.Machine;

        if (!machine.TryApplyModelGraphicsCardAndProcessor() || !machine.TryApplyHardDiskController() ||
            !machine.TryApplyMemorySize())
            return false;

        machine.ApplyFloppyDriveTypes();
        machine.ApplyZipImage();
        return machine.TryMountHardDisks();
    }

    private static void PrintMachineSummary(string? configurationPath)
    {
        Console.WriteLine($"machine : {model_c.models[model_c.model].name}, " +
                          $"mem_size = {pc.cfg_mem_size} Ko, " +
                          $"lecteurs {pc.cfg_drive_type[0]}/{pc.cfg_drive_type[1]}" +
                          (configurationPath is null ? " (défauts)" : $" ({configurationPath})"));
    }
}
