// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun)
// STATUS: host

using System.Diagnostics.CodeAnalysis;
using iXtal26.PluginApi;

namespace iXtal26.Host.CommandLine;

internal static class ConfigurationFile
{
    public static bool TryResolve(string argument, [NotNullWhen(true)] out string? resolvedPath)
    {
        resolvedPath = paths.resolve_file_path(argument);

        if (resolvedPath is not null)
            return true;

        Console.Error.WriteLine($"Fichier de configuration introuvable : « {argument} ».");
        return false;
    }

    public static bool TryLoadNow(string argument) =>
        TryResolve(argument, out var resolvedPath) && pc.loadconfig(resolvedPath);
}
