// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun)
// STATUS: host

namespace iXtal26.Host.CommandLine;

internal sealed class ArgumentCursor(string[] arguments)
{
    private int position = -1;

    public string Current => arguments[position];

    public bool HasNext => position + 1 < arguments.Length;

    public bool NextIsOption => HasNext && arguments[position + 1].StartsWith("--", StringComparison.Ordinal);

    public bool NextIsValue => HasNext && !arguments[position + 1].StartsWith('-');

    public bool MoveNext() => ++position < arguments.Length;

    public string TakeNext() => arguments[++position];

    public bool TryTakeInt32(out int value)
    {
        if (HasNext)
            return int.TryParse(TakeNext(), out value);

        value = 0;
        return false;
    }

    public bool HasValuesAhead(int count)
    {
        if (position + count >= arguments.Length)
            return false;

        for (var offset = 1; offset <= count; offset++)
        {
            if (arguments[position + offset].StartsWith('-'))
                return false;
        }

        return true;
    }

    public bool TryTake(string expected)
    {
        if (!HasNext || arguments[position + 1] != expected)
            return false;

        position++;
        return true;
    }

    public string[] TakeValues()
    {
        var count = 0;

        while (position + 1 + count < arguments.Length && !arguments[position + 1 + count].StartsWith('-'))
            count++;

        var values = new string[count];

        for (var index = 0; index < count; index++)
            values[index] = TakeNext();

        return values;
    }
}
