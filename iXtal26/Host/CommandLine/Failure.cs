// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun)
// STATUS: host

namespace iXtal26.Host.CommandLine;

internal static class Failure
{
    public static ExitCode Usage(params ReadOnlySpan<string> lines)
    {
        WriteToStandardError(lines);
        return ExitCode.UsageError;
    }

    public static ExitCode Runtime(params ReadOnlySpan<string> lines)
    {
        WriteToStandardError(lines);
        return ExitCode.RuntimeFailure;
    }

    private static void WriteToStandardError(ReadOnlySpan<string> lines)
    {
        foreach (var line in lines)
            Console.Error.WriteLine(line);
    }
}
