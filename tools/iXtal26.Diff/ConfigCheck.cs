// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — outillage de vérification.
//
// ALLER-RETOUR DU MOTEUR DE CONFIGURATION.
//
// config.c n'est PAS lié dans l'oracle : il n'y a rien à quoi le comparer. Sa moitié
// lecture est exercée par boot-diff --config, qui traverse config_load et
// config_get_int/string à chaque appel. Sa moitié ÉCRITURE — les six config_set_* et
// config_save — n'a aucun appelant tant que le menu n'édite pas la configuration.
//
// Ce dépôt traite un chemin mort comme un chemin cassé, parce que les deux se
// ressemblent exactement. D'où cette porte : on écrit un fichier avec les setters, on
// le relit avec le parseur, et on compare. Elle attrape ce qu'une relecture ne voit
// pas — le « %f » à six décimales de config_set_float, la ligne blanche que
// config_save insère avant chaque [section], et la section anonyme émise sans en-tête.

using iXtal26.PluginApi;

namespace iXtal26.Diff;

public static class ConfigCheck
{
    public static int Run()
    {
        var path = Path.Combine(Path.GetTempPath(), "ixtal-config-check.cfg");
        var failures = 0;

        config.config_free(config.CFG_MACHINE);

        // Section racine anonyme (head == null), celle que config_save émet SANS
        // en-tête, plus deux sections nommées : le nom de section est celui du device
        // chez PCem, d'où « CGA ».
        config.config_set_string(config.CFG_MACHINE, null, "model", "ibmpc");
        config.config_set_int(config.CFG_MACHINE, null, "mem_size", 256);
        config.config_set_int(config.CFG_MACHINE, null, "drive_a_type", 1);
        config.config_set_string(config.CFG_MACHINE, null, "disc_a", "os/pcdos20/pcdos20b.img");
        config.config_set_int(config.CFG_MACHINE, "CGA", "snow_enabled", 1);
        config.config_set_int(config.CFG_MACHINE, "CGA", "addr", 0x220);
        config.config_set_float(config.CFG_MACHINE, "GL3", "input_scale", 1.5f);

        // Réécriture d'une clé existante : elle doit muter l'entrée en place, pas en
        // créer une seconde — find_entry rendrait alors toujours la première et
        // config_save écrirait les deux.
        config.config_set_int(config.CFG_MACHINE, null, "mem_size", 512);

        config.config_save(config.CFG_MACHINE, path);

        if (!File.Exists(path))
        {
            Console.Error.WriteLine($"config_save n'a rien écrit dans {path}.");
            return 1;
        }

        Console.WriteLine($"écrit : {path}");
        Console.WriteLine(File.ReadAllText(path).TrimEnd() + "\n");

        // Relecture dans un arbre neuf.
        config.config_free(config.CFG_MACHINE);
        config.config_load(config.CFG_MACHINE, path);

        failures += Str("model", null, "model", "ibmpc");
        failures += Int("mem_size réécrit", null, "mem_size", 512);
        failures += Int("drive_a_type", null, "drive_a_type", 1);
        failures += Str("disc_a", null, "disc_a", "os/pcdos20/pcdos20b.img");
        failures += Int("[CGA] snow_enabled", "CGA", "snow_enabled", 1);

        // 0x220 est écrit en DÉCIMAL par config_set_int (sprintf "%i") et doit relire
        // 544. C'est la valeur, pas sa graphie, qui fait l'aller-retour.
        failures += Int("[CGA] addr", "CGA", "addr", 0x220);

        // Une clé absente doit rendre le défaut, et une section absente aussi : c'est
        // par là que toute la configuration par périphérique tient sans fichier.
        failures += Int("clé absente -> défaut", null, "nexiste_pas", 4242, 4242);
        failures += Int("section absente -> défaut", "NEANT", "quoi", 7, 7);

        var f = config.config_get_float(config.CFG_MACHINE, "GL3", "input_scale", 0f);
        if (Math.Abs(f - 1.5f) > 1e-6f)
        {
            Console.Error.WriteLine($"  ÉCHEC [GL3] input_scale : {f}, attendu 1.5");
            failures++;
        }

        // Le parseur, sur ce que le C rate : tabulation en tête, dernière ligne sans
        // saut de ligne final, valeur non numérique, commentaire.
        File.WriteAllText(path, "# commentaire\n\tindente = 1\nbavard = pas un nombre\nsans_fin = 9");
        config.config_free(config.CFG_MACHINE);
        config.config_load(config.CFG_MACHINE, path);

        failures += Int("ligne indentée par tabulation", null, "indente", 1);
        failures += Int("valeur non numérique -> défaut", null, "bavard", 77, 77);
        failures += Int("dernière ligne sans saut final", null, "sans_fin", 9);

        File.Delete(path);

        if (failures != 0)
        {
            Console.Error.WriteLine($"\n{failures} contrôle(s) en échec.");
            return 1;
        }

        Console.WriteLine("\nVert : l'aller-retour du moteur de configuration est fidèle.");
        return 0;
    }

    private static int Int(string label, string? head, string name, int expected, int def = -1)
    {
        var got = config.config_get_int(config.CFG_MACHINE, head, name, def);
        if (got == expected)
        {
            Console.WriteLine($"  ok   {label} = {got}");
            return 0;
        }

        Console.Error.WriteLine($"  ÉCHEC {label} : {got}, attendu {expected}");
        return 1;
    }

    private static int Str(string label, string? head, string name, string expected)
    {
        var got = config.config_get_string(config.CFG_MACHINE, head, name, "");
        if (got == expected)
        {
            Console.WriteLine($"  ok   {label} = « {got} »");
            return 0;
        }

        Console.Error.WriteLine($"  ÉCHEC {label} : « {got} », attendu « {expected} »");
        return 1;
    }
}
