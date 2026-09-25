// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun)
// STATUS: host

using iXtal26.PluginApi;

namespace iXtal26.Host.CommandLine;

internal static class FloppyPutVerb
{
    public static ExitCode Run(ArgumentCursor cursor)
    {
        if (!cursor.NextIsValue)
        {
            return Failure.Usage(
                "--floppy-put attend une image puis au moins un fichier.",
                "  --floppy-put os/disquette.img /home/moi/PROG.COM LISEZ.TXT");
        }

        var imageArgument = cursor.TakeNext();
        var imagePath = paths.resolve_file_path(imageArgument);

        if (imagePath is null)
            return Failure.Usage($"--floppy-put : image introuvable « {imageArgument} ».");

        var sourcePaths = cursor.TakeValues();

        if (sourcePaths.Length == 0)
            return Failure.Usage("--floppy-put : aucun fichier à déposer.");

        if (!FatImage.Put(imagePath, sourcePaths, out var message))
            return Failure.Runtime($"--floppy-put : {message}");

        Console.WriteLine(message);
        return ExitCode.Success;
    }
}
