// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/plugin-api/config.c
// SHA256: voir oracle.tsv ; vérifier avec tools/check-oracle.sh
// STATUS: partial — get_extension seul, consommé par disc_load. Le fichier de
//         configuration de PCem (config_get_*, config_save) n'a pas de pendant :
//         iXtal26 se configure par arguments.

namespace iXtal26.PluginApi;

internal static partial class config
{
    // pcem: config.c:416-428
    // DEVIATION: `char *` dans s devient une sous-chaîne ; `&s[strlen(s)]` est la
    //   chaîne vide.
    internal static string get_extension(string s)
    {
        int c = s.Length - 1;

        if (c <= 0)
                return s;

        while (c != 0 && s[c] != '.')
                c--;

        if (c == 0)
                return s.Substring(s.Length);

        return s.Substring(c + 1);
    }
}
