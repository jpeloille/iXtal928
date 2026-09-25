// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun)
// STATUS: host

namespace iXtal26.Host.CommandLine;

internal static class CreateHardDiskVerb
{
    private const int BytesPerMegabyte = 1024 * 1024;

    public static ExitCode Run(ArgumentCursor cursor, string romDirectory)
    {
        if (!cursor.NextIsValue)
        {
            HardDiskTypeListing.Print();
            return ExitCode.Success;
        }

        var specification = cursor.TakeNext();

        if (!TryParseGeometry(specification, out var cylinders, out var heads, out var sectorsPerTrack))
            return ExitCode.UsageError;

        if (!HddImage.Validate(cylinders, heads, sectorsPerTrack, out var validationError))
            return Failure.Usage($"--create-hdd : {validationError}");

        var imagesRoot = SdlMenu.ImagesRoot(romDirectory);
        var biosType = HddImage.TypeFor(cylinders, heads, sectorsPerTrack);
        string imagePath;

        if (cursor.NextIsValue)
        {
            imagePath = cursor.TakeNext();
        }
        else
        {
            var stem = biosType != 0 ? $"hdd-type{biosType:D2}" : $"hdd-{cylinders}x{heads}x{sectorsPerTrack}";

            if (!FreeImagePath.TryChoose("--create-hdd", imagesRoot, stem, out imagePath))
                return ExitCode.RuntimeFailure;
        }

        PrintPlannedImage(imagePath, cylinders, heads, sectorsPerTrack, biosType);

        if (!HddImage.Create(imagePath, cylinders, heads, sectorsPerTrack, out var creationMessage))
            return Failure.Runtime($"--create-hdd : {creationMessage}");

        Console.WriteLine(creationMessage);
        Console.WriteLine();
        Console.WriteLine("À ajouter au fichier de configuration :");
        Console.WriteLine();
        Console.Write(HddImage.ConfigBlock(HddImage.ConfigPath(imagePath, imagesRoot), cylinders, heads,
                                           sectorsPerTrack));

        return ExitCode.Success;
    }

    private static bool TryParseGeometry(string specification, out int cylinders, out int heads,
                                         out int sectorsPerTrack)
    {
        if (specification.Contains(','))
        {
            var fields = specification.Split(',');

            if (fields.Length == 3 && int.TryParse(fields[0], out cylinders) &&
                int.TryParse(fields[1], out heads) && int.TryParse(fields[2], out sectorsPerTrack))
                return true;

            cylinders = heads = sectorsPerTrack = 0;
            Console.Error.WriteLine(
                $"--create-hdd : géométrie illisible « {specification} », attendu CYLINDRES,TETES,SECTEURS.");
            return false;
        }

        if (!int.TryParse(specification, out var biosType) || !BiosDiskType.IsInRange(biosType))
        {
            cylinders = heads = sectorsPerTrack = 0;
            Console.Error.WriteLine(
                $"--create-hdd : « {specification} » n'est ni un type de 1 à 46, ni une géométrie " +
                "CYLINDRES,TETES,SECTEURS.");
            Console.Error.WriteLine("--create-hdd sans argument liste les 46 types.");
            return false;
        }

        (cylinders, heads) = HddImage.hd_types[biosType - 1];
        sectorsPerTrack = HddImage.TypeSectorsPerTrack;
        return true;
    }

    private static void PrintPlannedImage(string imagePath, int cylinders, int heads, int sectorsPerTrack,
                                          int biosType)
    {
        var sectorCount = (long)cylinders * heads * sectorsPerTrack;
        var byteCount = HddImage.SizeOf(cylinders, heads, sectorsPerTrack);
        var adapterSwitches = HddImage.XebecSwitch(cylinders, heads, sectorsPerTrack);

        Console.WriteLine($"Création de {imagePath}");
        Console.WriteLine($"  géométrie   : {cylinders} cylindres x {heads} têtes x {sectorsPerTrack} secteurs " +
                          $"= {sectorCount} secteurs");
        Console.WriteLine($"  taille      : {byteCount} octets ({byteCount / BytesPerMegabyte} Mo)");
        Console.WriteLine(biosType != 0
            ? $"  type BIOS   : {HddImage.Label(biosType)}"
            : "  type BIOS   : aucun — « Custom type » chez PCem");
        Console.WriteLine(adapterSwitches >= 0
            ? $"  carte       : acceptée par l'IBM Fixed Disk Adapter, interrupteurs type {adapterSwitches}"
            : "  carte       : REFUSÉE par l'IBM Fixed Disk Adapter — il n'accepte que 17 secteurs\n" +
              "                et (306,4) (612,4) (615,4) (306,8). Il n'en dira qu'un\n" +
              "                avertissement, annoncera le disque en type 0, et le POST\n" +
              "                divergera. Sans objet pour la carte dtc5150x.");
    }
}
