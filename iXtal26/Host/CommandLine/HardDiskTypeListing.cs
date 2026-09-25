// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun)
// STATUS: host

using System.Text;

namespace iXtal26.Host.CommandLine;

internal static class HardDiskTypeListing
{
    private const string Preamble = """
        Les 46 types de disque du BIOS (pcem-dev/src/wx-ui/wx-config.c:1295-1302).
        17 secteurs par piste, implicites dans toute la table.

        « * » : géométrie que l'IBM Fixed Disk Adapter accepte. Les autres se créent,
        mais la carte n'en dira qu'un avertissement, annoncera le disque en type 0 et
        le POST divergera. La carte dtc5150x, elle, n'impose aucune restriction, et
        mfm_at prend sa géométrie du BIOS : sur un 286, les 46 types sont ouverts.

        Cette table est celle de la liste déroulante de PCem. Mesurée contre la table
        de la ROM AMI 286 (F000:E401), elle concorde sur 45 entrées et se trompe sur
        le type 39 : la ROM dit 987 cylindres, PCem 462. Voir PB-34.

        Et l'INT 13h de cette ROM écrête les cylindres à 1023 (fonction AH=08h), donc
        au-delà de 1024 cylindres la fin du disque est déclarée mais inatteignable par
        DOS. Concerne le seul type 46.


        """;

    public static void Print()
    {
        var listing = new StringBuilder(Preamble);

        for (var biosType = 1; biosType <= BiosDiskType.Highest; biosType++)
            AppendType(listing, biosType);

        StandardOutput.WriteAtOnce(listing.ToString());
    }

    private static void AppendType(StringBuilder listing, int biosType)
    {
        var (cylinders, heads) = HddImage.hd_types[biosType - 1];

        if (cylinders == 0 || heads == 0)
        {
            listing.AppendLine($"    {HddImage.Label(biosType)}   (réservé, non créable)");
            return;
        }

        var adapterSwitches = HddImage.XebecSwitch(cylinders, heads, HddImage.TypeSectorsPerTrack);

        listing.AppendLine($"  {(adapterSwitches >= 0 ? "*" : " ")} {HddImage.Label(biosType)}" +
                           $"   {HddImage.SizeOf(cylinders, heads, HddImage.TypeSectorsPerTrack)} octets" +
                           (adapterSwitches >= 0 ? $", interrupteurs type {adapterSwitches}" : ""));
    }
}
