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
        var inPlace = false;
        int? joystickType = null, forcedPs2 = null;

        while (cursor.NextIsOption)
        {
            var option = cursor.TakeNext();

            // G9.1 — sans argument, avant le test « attend un argument » des autres options.
            if (option == "--in-place")
            {
                inPlace = true;
                continue;
            }

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
                case "--hardware-mode":
                    machine.HardwareMode = value;
                    break;
                case "--settle":
                    if (!int.TryParse(value, out settleSlices) || settleSlices < 0)
                        return Failure.Usage("--settle attend un entier positif.");
                    break;
                case "--config":
                    if (!ConfigurationFile.TryLoadNow(value))
                        return ExitCode.UsageError;
                    break;
                // G13.4 — la manette des bancs (JOYBANC) : son type, appliqué après --config ; son état, par la
                //   commande de script « @manette ».
                case "--joystick-type":
                    if (!int.TryParse(value, out var jt) || (uint)jt >= (uint)Joystick.gameport.joystick_type_count())
                        return Failure.Usage($"--joystick-type attend un type de manette, de 0 à " +
                                             $"{Joystick.gameport.joystick_type_count() - 1}.");
                    joystickType = jt;
                    break;
                // G13.4 — PORTE DE VÉRIFICATION, PAS UNE MACHINE OFFERTE (le pendant du --force-ps2 de boot-diff,
                //   PS2.1) : la souris PS/2 N (2, deux boutons ; 3, l'Intellimouse) montée sans le refus des machines
                //   sans MODEL_PS2, pour jouer PS2BANC en mode matériel, que boot-diff refuse.
                case "--force-ps2":
                    if (value is not ("2" or "3"))
                        return Failure.Usage("--force-ps2 attend 2 (souris PS/2 à deux boutons) ou 3 (Intellimouse).");
                    forcedPs2 = int.Parse(value);
                    break;
                default:
                    return Failure.Usage($"Option inconnue après --boot : {option}");
            }
        }

        if (joystickType is { } type)
            Joystick.gameport.joystick_type = type;
        if (forcedPs2 is { } ps2)
        {
            Mouse.mouse.mouse_type = ps2;
            Console.WriteLine($"--force-ps2 : porte de vérification — souris {ps2} montée sans MODEL_PS2 ; pas une machine offerte.");
        }

        if (!machine.TryApplyMachineAndCheckProcessor() || !machine.TryApplyHardDiskController() ||
            !machine.TryMountHardDisks() || !machine.TryApplyHardwareMode())
            return ExitCode.UsageError;

        // G9.1 (prévention, après l'incident du 03/10) : --boot travaille sur des COPIES de ses
        //   images, comme boot-diff — l'invité y écrit sans toucher aux images de l'utilisateur.
        //   --in-place rend l'écriture en place ; aucun outil du dépôt ne s'en sert.
        BootImageCopies.Enabled = !inPlace;
        if (!inPlace)
            BootImageCopies.CopyMountedImages();

        return (ExitCode)BootTest.Run(paths.resolve_roms_path(romDirectory), sliceCount, typedLines, settleSlices);
    }
}
