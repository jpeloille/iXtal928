// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun)
// STATUS: host

namespace iXtal26.Host.CommandLine;

internal static class StandardOutput
{
    public static void WriteAtOnce(string text)
    {
        Console.Out.Flush();

        using var stream = Console.OpenStandardOutput();
        stream.Write(Console.OutputEncoding.GetBytes(text.ReplaceLineEndings(Environment.NewLine)));
    }
}
