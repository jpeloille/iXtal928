// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/plugin-api/paths.c
// STATUS: partial — chemins de recherche des ROMs seulement (roms_paths,
//         get_roms_path, set_roms_paths, paths_init). nvr/, configs/, logs/,
//         screenshots/, plugins/ et la persistance de configuration sont omis.

// append_slash vit dans config.c chez PCem, donc dans config.cs ici. set_roms_paths et
// paths_init l'appellent sans préfixe, comme le C qui a les deux dans sa portée globale.
using static iXtal26.PluginApi.config;

namespace iXtal26.PluginApi;

internal static partial class paths
{
    // pcem: paths.c:9, 15-19
    internal static string default_roms_paths = "";

    /* the number of roms paths */
    internal static int num_roms_paths;
    internal static string roms_paths = "";
    /* this is where pcem.cfg is */
    internal static string pcem_path = "";

    // omitted: default_nvr_path, default_configs_path, default_logs_path,
    //   default_screenshots_path, nvr_path, configs_path, logs_path,
    //   screenshots_path, plugins_default_path, nvr_default_path
    //   (paths.c:10-13, 20-31) — aucun chemin hors ROM au palier (a).

    // pcem: paths.c:33-39
    // DEVIATION: le #ifdef _WIN32 devient un test à l'exécution.
    internal static char get_path_separator()
    {
            if (OperatingSystem.IsWindows())
                    return ';';
            else
                    return ':';
    }

    // pcem: paths.c:41-60
    // DEVIATION: `char *s` + `int size` -> `out string s` + size. La double
    //   troncature du C (safe_strncpy à size-1, puis s[min(size-1, z)] = 0) se
    //   réduit à une longueur de min(size - 1, z), z étant toujours <= len - j.
    internal static int get_roms_path(int pos, out string s, int size)
    {
            int j, i, z, len;
            char path_separator;

            s = "";
            path_separator = get_path_separator();
            len = roms_paths.Length;
            j = 0;
            for (i = 0; i < len; i++)
            {
                    if (roms_paths[i] == path_separator || i == len - 1)
                    {
                            if ((pos--) == 0)
                            {
                                    z = (i - j) + ((i == len - 1) ? 1 : 0);
                                    s = roms_paths.Substring(j, (size - 1 < z) ? size - 1 : z);
                                    return 1;
                            }
                            j = i + 1;
                    }
            }
            return 0;
    }

    // pcem: paths.c:62-88
    internal static void set_roms_paths(string path)
    {
            string s;
            int j, i, z, len;
            string path_separator;

            roms_paths = "";
            path_separator = get_path_separator().ToString();
            len = path.Length;
            j = 0;
            num_roms_paths = 0;
            for (i = 0; i < len; i++)
            {
                    if (path[i] == path_separator[0] || i == len - 1)
                    {
                            z = (i - j) + ((i == len - 1) ? 1 : 0) + 1;
                            s = path.Substring(j, z - 1);
                            // omitted: s[(511 < z) ? 511 : z] = 0 — safe_strncpy a déjà
                            //   posé le NUL en z-1, et la troncature à 511 n'a pas
                            //   d'objet sans tampon de 512.
                            s = append_slash(s, 512);
                            if (dir_exists(s) != 0)
                            {
                                    if (num_roms_paths > 0)
                                            roms_paths += path_separator;
                                    roms_paths += s;
                                    num_roms_paths++;
                            }
                            j = i + 1;
                    }
            }
    }

    // pcem: paths.c:90
    // DEVIATION: wx_dir_exists appartient à l'UI wx, remplacée par l'hôte SDL3.
    internal static int dir_exists(string path) => Directory.Exists(path) ? 1 : 0;

    /* set the default roms paths, this makes them permanent */
    // pcem: paths.c:112-116
    internal static void set_default_roms_paths(string s)
    {
            default_roms_paths = s;
            set_roms_paths(s);
    }

    // DEVIATION: sans équivalent pcem. pcem ancre ses chemins sur get_pcem_path()
    //   (paths.c:218-241), omis ici ; il a donc fallu dire explicitement ce qui sert
    //   de référence, et ce N'EST PAS le répertoire courant.
    //
    //   Rider lance le binaire avec bin/Debug/net10.0/ pour répertoire courant, où
    //   « roms » n'existe pas. set_roms_paths() écarte silencieusement un répertoire
    //   absent (ligne 88) : num_roms_paths retombait à 0, romfopen() bouclait zéro
    //   fois et loadbios() rendait 0 en accusant les ROMs, alors que le fautif était
    //   le répertoire courant. La même commande depuis la racine du dépôt marchait :
    //   l'échec dépendait d'où on lançait, pas de ce qu'on lançait.
    //
    //   Règle, dans cet ordre :
    //     1. le chemin existe relativement au répertoire courant -> rendu tel quel.
    //        Une exécution depuis la racine du dépôt, un chemin absolu et un
    //        --rom-path qui tombe juste gardent EXACTEMENT leur comportement ;
    //     2. sinon on remonte depuis l'emplacement du BINAIRE jusqu'au premier
    //        répertoire qui le contient — la racine du dépôt en développement, le
    //        répertoire d'installation une fois déployé ;
    //     3. sinon le chemin est rendu inchangé, pour que le message d'échec cite
    //        ce que l'utilisateur a tapé plutôt qu'un chemin qu'il n'a jamais écrit.
    internal static string resolve_roms_path(string path)
    {
            // La chaîne vide sort AVANT la remontée. Path.Combine(d, "") rend d, qui
            // existe toujours : la boucle s'arrêterait à sa première itération et
            // rendrait le répertoire du binaire. « --rom-path "" » recevrait alors
            // « Impossible de charger le BIOS depuis bin/Debug/net10.0/ » au lieu du
            // « aucun répertoire utilisable » que ce cas mérite.
            if (path.Length == 0 || Directory.Exists(path))
                    return path;

            return find_upwards(path) ?? path;
    }

    // Tronc commun de resolve_roms_path et paths_init : le premier répertoire qui
    // contienne `relative`, en remontant depuis l'emplacement du binaire. Rend null
    // si la racine du système est atteinte sans l'avoir trouvé.
    private static string? find_upwards(string relative)
    {
            for (DirectoryInfo? d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
            {
                    string candidate = Path.Combine(d.FullName, relative);
                    if (Directory.Exists(candidate))
                            return candidate;
            }

            return null;
    }

    /* initialize default paths */
    // pcem: paths.c:190-216, réduit aux chemins de ROM.
    // DEVIATION: get_pcem_path (paths.c:218-241) cherche SDL_GetBasePath + ".pcem/"
    //   puis $HOME/.pcem/. iXtal26 n'a pas de répertoire d'installation : on remonte
    //   depuis le binaire jusqu'au premier répertoire contenant `roms/`, qui est la
    //   racine du dépôt. append_filename (config.c:396) est un sprintf("%s%s"),
    //   écrit ici en concaténation.
    //
    //   La remontée est INCONDITIONNELLE ici, là où resolve_roms_path donne d'abord
    //   sa chance au répertoire courant. Les deux politiques diffèrent exprès :
    //   paths_init est la réponse pcem à « où suis-je installé », qui ne doit rien
    //   devoir à l'endroit d'où l'on a tapé la commande.
    internal static void paths_init()
    {
            string s;

            // pcem_path est le PARENT du roms/ trouvé.
            string? found = find_upwards("roms");
            pcem_path = append_slash(
                    (found != null) ? (Path.GetDirectoryName(found) ?? Environment.CurrentDirectory)
                                    : Environment.CurrentDirectory, 512);

            /* set up default paths for this session */
            s = pcem_path + "roms/";
            set_default_roms_paths(s);

            // omitted: nvr/, configs/, screenshots/, logs/, nvr/default/ et
            //   add_config_callback(paths_loadconfig, paths_saveconfig,
            //   paths_onconfigloaded) (paths.c:204-215) — hors chargement de ROM.
    }

    // append_slash et put_backslash ont migré vers PluginApi/config.cs, à qui ils
    // appartiennent (config.c:398-414) — la migration que le commentaire d'ici
    // annonçait. `using static` en tête de fichier garde les appels inchangés.

    // omitted: set_nvr_path, set_logs_path, set_configs_path, set_screenshots_path,
    //   set_default_nvr_path, set_default_nvr_default_path, set_default_logs_path,
    //   set_default_configs_path, set_default_screenshots_path, paths_loadconfig,
    //   paths_saveconfig, paths_onconfigloaded, get_pcem_path (paths.c:92-142,
    //   144-188, 218-241) — chemins hors ROM et persistance de la configuration.
}
