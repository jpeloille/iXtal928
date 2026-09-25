// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun)
// STATUS: host

namespace iXtal26.Host.CommandLine;

internal static class MakeNvrVerb
{
    public static ExitCode Run(ArgumentCursor cursor, string romDirectory)
    {
        if (!cursor.HasValuesAhead(2))
        {
            return Failure.Usage(
                "--make-nvr CONFIG.cfg SORTIE.nvr [--force]",
                "Exemple : --make-nvr ixtal26-286.cfg nvr/.ami286.nvr");
        }

        var configurationPath = cursor.TakeNext();
        var outputPath = cursor.TakeNext();
        var overwritesExisting = cursor.TryTake("--force");

        return (ExitCode)NvrImage.Make(configurationPath, outputPath, romDirectory, overwritesExisting);
    }
}
