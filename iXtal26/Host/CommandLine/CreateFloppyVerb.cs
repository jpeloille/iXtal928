// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun)
// STATUS: host

using System.Text;

namespace iXtal26.Host.CommandLine;

internal static class CreateFloppyVerb
{
    private const int BytesPerKilobyte = 1024;

    public static ExitCode Run(ArgumentCursor cursor, string romDirectory)
    {
        if (!cursor.NextIsValue)
        {
            PrintFormats();
            return ExitCode.Success;
        }

        var formatName = cursor.TakeNext();
        var formatIndex = FatImage.FormatIndex(formatName);

        if (formatIndex < 0)
        {
            return Failure.Usage(
                $"--create-floppy : « {formatName} » n'est pas un format connu.",
                "--create-floppy sans argument les liste tous.");
        }

        var format = FatImage.Formats[formatIndex];
        string imagePath;

        if (cursor.NextIsValue)
        {
            imagePath = cursor.TakeNext();
        }
        else if (!FreeImagePath.TryChoose("--create-floppy", SdlMenu.ImagesRoot(romDirectory), "fat" + format.Stem,
                                          out imagePath))
        {
            return ExitCode.RuntimeFailure;
        }

        PrintPlannedImage(imagePath, format);

        if (!FatImage.Create(imagePath, format, out var creationMessage))
            return Failure.Runtime($"--create-floppy : {creationMessage}");

        Console.WriteLine(creationMessage);
        Console.WriteLine();
        Console.WriteLine("Disquette NON SYSTÈME : amorcer dessus affiche un message et attend une");
        Console.WriteLine("touche, en boucle. Pour y déposer des fichiers : --floppy-put.");
        return ExitCode.Success;
    }

    private static void PrintFormats()
    {
        var listing = new StringBuilder();
        listing.AppendLine("Formats de disquette (les seuls que le lecteur 5,25\" DD du 5150 sait lire) :");
        listing.AppendLine();

        foreach (var format in FatImage.Formats)
        {
            listing.AppendLine($"  {format.Stem,-5} {format.Name}");
            listing.AppendLine($"        {FatImage.ClusterCount(format)} clusters de " +
                               $"{FatImage.BytesPerCluster(format)} octets, " +
                               $"{format.RootEntries} entrées de répertoire, " +
                               $"média 0x{format.MediaDescriptor:X2}");
        }

        listing.AppendLine();
        listing.AppendLine("L'image est formatée en FAT12, vide et NON SYSTÈME : pour la rendre");
        listing.AppendLine("amorçable, faire SYS B: depuis DOS dans la machine.");
        StandardOutput.WriteAtOnce(listing.ToString());
    }

    private static void PrintPlannedImage(string imagePath, FatImage.Format format)
    {
        var imageSize = FatImage.ImageSize(format);

        Console.WriteLine($"Création de {imagePath}");
        Console.WriteLine($"  format      : {format.Name}");
        Console.WriteLine($"  taille      : {imageSize} octets ({imageSize / BytesPerKilobyte} Ko)");
        Console.WriteLine($"  FAT12       : {format.NumberOfFats} copies de " +
                          $"{format.SectorsPerFat} secteur(s), " +
                          $"{FatImage.ClusterCount(format)} clusters de " +
                          $"{FatImage.BytesPerCluster(format)} octets");
        Console.WriteLine($"  racine      : {format.RootEntries} entrées, " +
                          $"au secteur {format.ReservedSectors + format.NumberOfFats * format.SectorsPerFat}");
    }
}
