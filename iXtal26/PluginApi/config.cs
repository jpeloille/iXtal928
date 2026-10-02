// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/plugin-api/config.c
// SHA256: voir oracle.tsv ; vérifier avec tools/check-oracle.sh
// STATUS: partial — le moteur de configuration de PCem. Omis : config_dump (sortie
//         pure), config_new (morte : config_file n'est jamais renseigné),
//         add_config_callback et les callbacks (pas de plugins dans iXtal26).
//
// config.c N'EST PAS LIÉ DANS L'ORACLE (tools/oracle/Makefile ; config_get_int y est
// un stub de harness_stubs.c qui rend toujours le défaut). Ce fichier n'a donc AUCUN
// pendant exécutable à comparer, et la politique de bug s'inverse : les défauts de
// config.c sont CORRIGÉS ici, chacun marqué // DEVIATION:, au lieu d'être reproduits
// comme ceux de disc.c (PB-14, PB-15, PB-18). Là-bas l'oracle exécute le bug ; ici il
// n'existe pas, donc le reproduire ne prouverait rien et coûterait des fichiers perdus.

namespace iXtal26.PluginApi;

// pcem: config.c:20-26 et 28-33
// DEVIATION: le `list_t` en premier membre et les casts `(section_t *)head->next` sont
//   de l'héritage à la C. Ici chaque nœud porte son `next` typé ; le chaînage simple,
//   l'insertion en queue et le parcours linéaire sont identiques. Classes et non
//   structs : leur adresse est prise et stockée, comme pc_timer_t (TRANSCRIPTION.md).
internal sealed class section_t
{
    internal string name = "";
    internal entry_t? entry_head;
    internal section_t? next;
}

internal sealed class entry_t
{
    internal string name = "";
    internal string data = "";
    internal entry_t? next;
}

internal static partial class config
{
    // pcem: includes/public/pcem/config.h:5-6
    internal const int CFG_MACHINE = 0;
    internal const int CFG_GLOBAL = 1;

    // pcem: config.c:17-18
    private static section_t? global_config_head;
    private static section_t? machine_config_head;

    // pcem: config.c:7 — chemin du fichier machine actif.
    internal static string config_file_default = "";

    // pcem: config.c:8 — nom sans extension. Chez PCem il nomme aussi la NVRAM
    // (nvr.c:33-54), couplage qui fait perdre le CMOS quand on renomme un .cfg.
    //
    // LE COUPLAGE EXISTE ICI AUSSI — nvr.cs:85 lit ce champ — mais RIEN NE
    // L'AFFECTE JAMAIS, là où PCem le pose depuis le nom du fichier de
    // configuration (pc.c:217-219, wx-config_sel.c:90-92), tous deux omis.
    // Conséquence observable : le CMOS s'appelle « nvr/.at.nvr », un fichier
    // caché, et --config ne change pas son nom.
    internal static string config_name = "";

    // pcem: config.c:130 — `list_t *head = is_global ? &global : &machine;`. Le C prend
    // l'ADRESSE de la tête pour pouvoir la réécrire ; la propriété ref est le seul rendu
    // qui garde cette propriété (TRANSCRIPTION.md, les sept C#-ismes autorisés).
    private static ref section_t? head_of(int is_global)
        => ref is_global != 0 ? ref global_config_head : ref machine_config_head;

    // pcem: config.c:35-44 — la macro list_add, insertion EN QUEUE. O(n) par insertion
    // comme le C : c'est l'ordre d'apparition qui est observable, config_save le réémet.
    private static void list_add_section(section_t @new, int is_global)
    {
        ref section_t? head = ref head_of(is_global);

        if (head == null)
        {
                head = @new;
                return;
        }

        section_t next = head;
        while (next.next != null)
                next = next.next;

        next.next = @new;
        @new.next = null;
    }

    private static void list_add_entry(entry_t @new, section_t section)
    {
        if (section.entry_head == null)
        {
                section.entry_head = @new;
                return;
        }

        entry_t next = section.entry_head;
        while (next.next != null)
                next = next.next;

        next.next = @new;
        @new.next = null;
    }

    // omitted: config_dump (config.c:46-69) — sortie pure, une enfilade de pclog.

    // pcem: config.c:71-92
    // DEVIATION: le C libère les nœuds mais NE REMET PAS head->next à NULL — tout accès
    //   ultérieur est un use-after-free. Sous GC il suffit de lâcher la tête, ce qui
    //   corrige le défaut au lieu de le reproduire (voir l'en-tête).
    internal static void config_free(int is_global)
    {
        head_of(is_global) = null;
    }

    // pcem: config.c:94-125
    internal static int config_free_section(int is_global, string name)
    {
        ref section_t? head = ref head_of(is_global);
        section_t? current_section = head;
        section_t? prev_section = null;

        while (current_section != null)
        {
                section_t? next_section = current_section.next;
                if (current_section.name == name)
                {
                        if (prev_section == null)
                                head = next_section;
                        else
                                prev_section.next = next_section;
                        return 1;
                }
                prev_section = current_section;
                current_section = next_section;
        }
        return 0;
    }

    // pcem: config.c:127-219
    internal static void config_load(int is_global, string fn)
    {
        ref section_t? head = ref head_of(is_global);

        // pcem: config.c:132-136 — la tête est vidée puis une section ANONYME est créée
        // d'office. C'est elle la section racine, celle qu'on adresse avec head == null,
        // et config_save l'émet sans en-tête.
        head = null;

        section_t current_section = new section_t();
        list_add_section(current_section, is_global);

        StreamReader? f = fopen_rt(fn);
        if (f == null)
                return;

        // DEVIATION: le C fait `fgets` puis `if (feof(f)) break;` sans tester le retour
        //   de fgets (config.c:145-147) — LA DERNIÈRE LIGNE EST PERDUE quand le fichier
        //   ne se termine pas par un saut de ligne, ce qui est le cas de tout fichier
        //   édité par certains éditeurs. Corrigé : on lit jusqu'à null.
        using (f)
        {
                string? buffer;
                while ((buffer = f.ReadLine()) != null)
                {
                        int c = 0;

                        // DEVIATION: le C ne saute que les espaces (`buffer[c] == ' '`,
                        //   config.c:151) : une ligne indentée par TABULATION casse le
                        //   parsing du nom de clé. Les deux blancs sont acceptés ici.
                        while (c < buffer.Length && (buffer[c] == ' ' || buffer[c] == '\t'))
                                c++;

                        if (c >= buffer.Length)
                                continue;

                        if (buffer[c] == '#') /*Comment*/
                                continue;

                        if (buffer[c] == '[') /*Section*/
                        {
                                int d = c + 1;
                                while (d < buffer.Length && buffer[d] != ']')
                                        d++;

                                if (d >= buffer.Length)
                                        continue;

                                section_t new_section = new section_t();
                                new_section.name = buffer.Substring(c + 1, d - c - 1);
                                list_add_section(new_section, is_global);

                                current_section = new_section;
                        }
                        else
                        {
                                int d = c;
                                while (d < buffer.Length && buffer[d] != '=' && buffer[d] != ' ' && buffer[d] != '\t')
                                        d++;

                                if (d >= buffer.Length)
                                        continue;

                                string name = buffer.Substring(c, d - c);

                                while (d < buffer.Length && (buffer[d] == '=' || buffer[d] == ' ' || buffer[d] == '\t'))
                                        d++;

                                if (d >= buffer.Length)
                                        continue;

                                // pcem: config.c:203-209 — la valeur est TOUT le reste de
                                // la ligne, espaces internes compris. ReadLine a déjà ôté
                                // le \r\n que le C retirait à la main.
                                entry_t new_entry = new entry_t();
                                new_entry.name = name;
                                new_entry.data = buffer.Substring(d);
                                list_add_entry(new_entry, current_section);
                        }
                }
        }
    }

    // pcem: config.c:431-456
    internal static void config_save(int is_global, string fn)
    {
        StreamWriter? f = fopen_wt(fn);
        if (f == null)
                return;

        using (f)
        {
                section_t? current_section = head_of(is_global);

                while (current_section != null)
                {
                        // pcem: config.c:441 — la section anonyme est émise SANS en-tête.
                        if (current_section.name.Length != 0)
                                f.Write($"\n[{current_section.name}]\n");

                        entry_t? current_entry = current_section.entry_head;

                        while (current_entry != null)
                        {
                                f.Write($"{current_entry.name} = {current_entry.data}\n");

                                current_entry = current_entry.next;
                        }

                        current_section = current_section.next;
                }
        }
    }

    // iXtal26 (outillage, sans pendant C) — G8.3 : les entrées des sections NOMMÉES du tiers
    // machine, dans l'ordre du fichier. iXtal26.Diff les recopie dans l'oracle
    // (h_set_device_config), où config.c n'est pas lié : les deux côtés lisent alors les
    // mêmes sections de device.
    internal static IEnumerable<(string head, string name, string data)> machine_named_entries()
    {
        for (section_t? sec = machine_config_head; sec != null; sec = sec.next)
        {
                if (sec.name.Length == 0)
                        continue;
                for (entry_t? e = sec.entry_head; e != null; e = e.next)
                        yield return (sec.name, e.name, e.data);
        }
    }

    // pcem: config.c:226-242 — `name == NULL` désigne la section anonyme.
    private static section_t? find_section(string? name, int is_global)
    {
        section_t? current_section = head_of(is_global);
        name ??= "";

        while (current_section != null)
        {
                if (current_section.name == name)
                        return current_section;

                current_section = current_section.next;
        }
        return null;
    }

    // pcem: config.c:244-256
    private static entry_t? find_entry(section_t section, string name)
    {
        entry_t? current_entry = section.entry_head;

        while (current_entry != null)
        {
                if (current_entry.name == name)
                        return current_entry;

                current_entry = current_entry.next;
        }
        return null;
    }

    // pcem: config.c:258-267
    private static section_t create_section(string? name, int is_global)
    {
        section_t new_section = new section_t();

        new_section.name = name ?? "";
        list_add_section(new_section, is_global);

        return new_section;
    }

    // pcem: config.c:269-276
    private static entry_t create_entry(section_t section, string name)
    {
        entry_t new_entry = new entry_t();
        new_entry.name = name;
        list_add_entry(new_entry, section);

        return new_entry;
    }

    // pcem: config.c:278-296
    // DEVIATION: le C fait `sscanf(entry->data, "%i", &value)` SANS TESTER LE RETOUR
    //   (config.c:293) : sur une valeur non numérique il rend `value` non initialisée,
    //   donc de l'UB, au lieu du défaut. On rend le défaut.
    internal static int config_get_int(int is_global, string? head, string name, int def)
    {
        section_t? section = find_section(head, is_global);

        if (section == null)
                return def;

        entry_t? entry = find_entry(section, name);

        if (entry == null)
                return def;

        return sscanf_i(entry.data, def);
    }

    // pcem: config.c:298-316. Même correction que ci-dessus.
    internal static float config_get_float(int is_global, string? head, string name, float def)
    {
        section_t? section = find_section(head, is_global);

        if (section == null)
                return def;

        entry_t? entry = find_entry(section, name);

        if (entry == null)
                return def;

        return float.TryParse(entry.data.Trim(), System.Globalization.NumberStyles.Float,
                              System.Globalization.CultureInfo.InvariantCulture, out float value)
               ? value : def;
    }

    // pcem: config.c:318-333
    // DEVIATION: le C rend un pointeur DANS entry->data, sans copie : l'appelant peut
    //   écrire à travers. Une chaîne C# est immuable, la propriété est donc plus forte.
    internal static string config_get_string(int is_global, string? head, string name, string def)
    {
        section_t? section = find_section(head, is_global);

        if (section == null)
                return def;

        entry_t? entry = find_entry(section, name);

        if (entry == null)
                return def;

        return entry.data;
    }

    // pcem: config.c:335-350
    internal static void config_set_int(int is_global, string? head, string name, int val)
    {
        section_t? section = find_section(head, is_global);

        if (section == null)
                section = create_section(head, is_global);

        entry_t? entry = find_entry(section, name);

        if (entry == null)
                entry = create_entry(section, name);

        entry.data = val.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    // pcem: config.c:352-367
    internal static void config_set_float(int is_global, string? head, string name, float val)
    {
        section_t? section = find_section(head, is_global);

        if (section == null)
                section = create_section(head, is_global);

        entry_t? entry = find_entry(section, name);

        if (entry == null)
                entry = create_entry(section, name);

        // pcem: sprintf("%f") — six décimales, point décimal, quelle que soit la locale.
        entry.data = val.ToString("F6", System.Globalization.CultureInfo.InvariantCulture);
    }

    // pcem: config.c:369-384
    internal static void config_set_string(int is_global, string? head, string name, string val)
    {
        section_t? section = find_section(head, is_global);

        if (section == null)
                section = create_section(head, is_global);

        entry_t? entry = find_entry(section, name);

        if (entry == null)
                entry = create_entry(section, name);

        entry.data = val;
    }

    // pcem: config.c:386-394
    internal static string get_filename(string s)
    {
        int c = s.Length - 1;

        while (c > 0)
        {
                if (s[c] == '/' || s[c] == '\\')
                        return s.Substring(c + 1);
                c--;
        }

        return s;
    }

    // pcem: config.c:398-406. Vient de paths.cs, où un commentaire annonçait cette
    // migration : append_slash appartient à config.c, pas à paths.c. `size` est la
    // taille du tampon C ; la branche de troncature est conservée pour que le rendu soit
    // le même sur un chemin qui frôle la limite.
    // DEVIATION: `c = strlen(s) - 1` vaut -1 sur une chaîne vide et le C lit alors
    //   s[-1]. Le cas est écarté ici plutôt que reproduit (voir l'en-tête).
    internal static string append_slash(string s, int size)
    {
            if (s.Length == 0)
                    return s;

            int c = s.Length - 1;
            if (s[c] != '/' && s[c] != '\\')
            {
                    if (c < size - 2)
                            s += "/";
                    else
                            s = s.Substring(0, c) + "/";
            }
            return s;
    }

    // pcem: config.c:408-414 — ajoute '/' malgré son nom.
    internal static string put_backslash(string s)
    {
            if (s.Length == 0)
                    return s;

            int c = s.Length - 1;
            if (s[c] != '/' && s[c] != '\\')
                    s += "/";
            return s;
    }

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

    // DEVIATION: stdio de la libc, comme disc_img.fopen. Un fichier absent n'est pas une
    //   faute : config_load rend alors tous les défauts (config.c:138-139).
    private static StreamReader? fopen_rt(string s)
    {
            try { return new StreamReader(s); }
            catch (IOException) { return null; }
            catch (UnauthorizedAccessException) { return null; }
            catch (ArgumentException) { return null; }
    }

    private static StreamWriter? fopen_wt(string s)
    {
            try { return new StreamWriter(s, false); }
            catch (IOException) { return null; }
            catch (UnauthorizedAccessException) { return null; }
            catch (ArgumentException) { return null; }
    }

    /// <summary>
    /// Pendant de `sscanf(s, "%i", &amp;v)`. `%i` — et non `%d` — accepte le préfixe 0x et
    /// la base 8 : c'est par lui que PCem lit « addr = 0x220 » dans la configuration par
    /// périphérique, sans accesseur hexadécimal dédié (config_get_hex16 n'existe pas).
    /// Rend `def` quand rien n'est lisible, là où le C rendrait une valeur indéterminée.
    /// </summary>
    private static int sscanf_i(string s, int def)
    {
        string t = s.Trim();
        int i = 0;
        bool neg = false;

        if (i < t.Length && (t[i] == '+' || t[i] == '-'))
                neg = t[i++] == '-';

        int start = i, radix = 10;

        if (i + 1 < t.Length && t[i] == '0' && (t[i + 1] == 'x' || t[i + 1] == 'X'))
        {
                radix = 16;
                start = i + 2;
        }
        else if (i < t.Length && t[i] == '0')
        {
                radix = 8;
        }

        long value = 0;
        int digits = 0;

        for (int c = start; c < t.Length; c++)
        {
                int d = t[c] switch
                {
                        >= '0' and <= '9' => t[c] - '0',
                        >= 'a' and <= 'f' => t[c] - 'a' + 10,
                        >= 'A' and <= 'F' => t[c] - 'A' + 10,
                        _ => 99,
                };

                if (d >= radix)
                        break;

                value = (value * radix) + d;
                digits++;

                if (value > uint.MaxValue)
                        return def;
        }

        if (digits == 0)
                return def;

        return neg ? (int)-value : unchecked((int)value);
    }
}
