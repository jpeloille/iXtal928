// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun)
// STATUS: host

namespace iXtal26.Host.CommandLine;

internal static class FreeImagePath
{
    public static bool TryChoose(string verb, string imagesRoot, string stem, out string imagePath)
    {
        imagePath = "";

        try
        {
            Directory.CreateDirectory(imagesRoot);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"{verb} : création de {imagesRoot} impossible : {exception.Message}");
            return false;
        }

        var freePath = SdlMenu.FreeName(imagesRoot, stem);

        if (freePath is null)
        {
            Console.Error.WriteLine($"{verb} : trop d'images « vierge-{stem} » dans {imagesRoot}.");
            return false;
        }

        imagePath = freePath;
        return true;
    }
}
