// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun)
// STATUS: host

using iXtal26.Disc;
using iXtal26.Floppy;
using iXtal26.PluginApi;

namespace iXtal26.Host.CommandLine;

internal static class DriveMounter
{
    private readonly record struct HardDiskGeometry(int Cylinders, int Heads, int SectorsPerTrack, int BiosType);

    public static string HardDiskOptionSuffix(int drive) => drive == 0 ? "" : "-d";

    public static bool TryMountFloppy(int drive, string imagePath)
    {
        var resolvedPath = paths.resolve_file_path(imagePath);

        if (resolvedPath is null)
        {
            Console.Error.WriteLine($"Image de disquette introuvable : « {imagePath} ».");
            return false;
        }

        fdd_c.discfns[drive] = resolvedPath;
        return true;
    }

    public static bool TryMountHardDisk(int drive, string imagePath, int? forcedType)
    {
        var resolvedPath = paths.resolve_file_path(imagePath);

        if (resolvedPath is null)
        {
            Console.Error.WriteLine($"Image de disque dur introuvable : « {imagePath} ».");
            Console.Error.WriteLine("Pour en fabriquer une : --create-hdd (sans argument, il liste les types).");
            return false;
        }

        if (!TryReadImageSize(resolvedPath, out var imageSize) ||
            !TryResolveGeometry(drive, resolvedPath, imageSize, forcedType, out var geometry))
            return false;

        hdd_c.ide_fn[drive] = resolvedPath;
        hdd_c.hdc[drive].spt = geometry.SectorsPerTrack;
        hdd_c.hdc[drive].hpc = geometry.Heads;
        hdd_c.hdc[drive].tracks = geometry.Cylinders;

        if (pc.cfg_hdd_controller.Length == 0)
            pc.cfg_hdd_controller = HardDiskControllers.DefaultForCurrentMachine;

        Console.WriteLine($"Disque {(drive == 0 ? "C" : "D")}: {resolvedPath} — {HddImage.Label(geometry.BiosType)}" +
                          $", carte {pc.cfg_hdd_controller}");

        WarnIfAdapterRejectsGeometry(geometry);

        if (forcedType is null)
            WarnIfSizeIsAmbiguous(drive, imageSize, geometry.BiosType);

        return true;
    }

    private static bool TryReadImageSize(string resolvedPath, out long imageSize)
    {
        try
        {
            imageSize = new FileInfo(resolvedPath).Length;
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Image de disque dur illisible : « {resolvedPath} » — {exception.Message}");
            imageSize = 0;
            return false;
        }
    }

    private static bool TryResolveGeometry(int drive, string resolvedPath, long imageSize, int? forcedType,
                                           out HardDiskGeometry geometry)
    {
        if (forcedType is { } biosType)
        {
            var (cylinders, heads) = HddImage.hd_types[biosType - 1];
            var forcedSize = HddImage.SizeOf(cylinders, heads, HddImage.TypeSectorsPerTrack);
            geometry = new HardDiskGeometry(cylinders, heads, HddImage.TypeSectorsPerTrack, biosType);

            if (forcedSize == imageSize)
                return true;

            Console.Error.WriteLine(
                $"--hdd{HardDiskOptionSuffix(drive)}-type {biosType} décrit {forcedSize} " +
                $"octets, mais « {resolvedPath} » en fait {imageSize}.");
            return false;
        }

        var (guessedCylinders, guessedHeads, guessedSectorsPerTrack, guessedType) = HddImage.GuessGeometry(imageSize);
        geometry = new HardDiskGeometry(guessedCylinders, guessedHeads, guessedSectorsPerTrack, guessedType);

        if (guessedType != 0)
            return true;

        Console.Error.WriteLine(
            $"« {resolvedPath} » fait {imageSize} octets, ce qui ne correspond à aucun des 46 types " +
            "de disque du BIOS.");
        Console.Error.WriteLine(
            "Les cartes transcrites câblent 17 secteurs par piste : une géométrie déduite " +
            "autrement serait annoncée sans être adressable.");
        Console.Error.WriteLine("--create-hdd liste les tailles admises.");
        return false;
    }

    private static void WarnIfAdapterRejectsGeometry(HardDiskGeometry geometry)
    {
        if (pc.cfg_hdd_controller != "mfm_xebec" ||
            HddImage.XebecSwitch(geometry.Cylinders, geometry.Heads, geometry.SectorsPerTrack) >= 0)
            return;

        Console.Error.WriteLine(
            $"  ATTENTION : le Fixed Disk Adapter n'accepte pas {geometry.Cylinders} x {geometry.Heads}. Il " +
            "annoncera le disque en type 0 et le POST divergera.");
        Console.Error.WriteLine("  Les géométries admises sont listées par --create-hdd.");
    }

    private static void WarnIfSizeIsAmbiguous(int drive, long imageSize, int retainedType)
    {
        var candidateTypes = HddImage.TypesWithSize(imageSize, out var isAmbiguous);

        if (!isAmbiguous)
            return;

        Console.Error.WriteLine($"  ATTENTION : {imageSize} octets ne désigne pas un type unique.");

        foreach (var candidateType in candidateTypes)
            Console.Error.WriteLine($"    {HddImage.Label(candidateType)}");

        Console.Error.WriteLine(
            $"  Le type {retainedType:D2} a été retenu, comme le ferait PCem. Si l'image a été " +
            $"formatée avec une autre géométrie, --hdd{HardDiskOptionSuffix(drive)}-type N le dit.");
    }
}
