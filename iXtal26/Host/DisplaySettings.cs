// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/wx-ui/wx-sdl2.c:236-290 (sdl_loadconfig / sdl_saveconfig, section [SDL2])
// STATUS: host

using iXtal26.PluginApi;

namespace iXtal26.Host;

/// <summary>Taille du moniteur simulé. La valeur est la diagonale, écrite telle quelle
/// dans la clé `monitor` ; Integer (0) est le mode sans moniteur, à pixels entiers.</summary>
internal enum CrtMonitor
{
    Integer = 0,
    In14 = 14,
    In15 = 15,
    In17 = 17,
}

/// <summary>
/// Réglages d'affichage de l'hôte, partagés par SdlHost (qui les applique) et SdlMenu
/// (qui les change). Rien ici ne touche video.Buffer32 : les oracles n'en voient rien.
///
/// Précédence : défauts, puis la section [SDL2] du .cfg machine, puis la ligne de
/// commande (--monitor, --crt, --pixel-mm), comme pour le reste de la machine.
///
/// DEVIATION: les clés `monitor`, `crt` et `pixel_mm` n'existent pas chez PCem. Elles
///   vivent dans la section [SDL2] où PCem range son `scale` (wx-sdl2.c:249), mais ne
///   réutilisent pas `scale` : chez PCem c'est un indice 0,5× … 2,5× de la trame, ici la
///   surface est celle d'un moniteur, quelle que soit la trame.
/// </summary>
internal sealed class DisplaySettings
{
    private const string Section = "SDL2";

    /// <summary>Pixel d'un 14 pouces VGA de 1990 : 640 points sur ~27 cm visibles. Sert
    /// au mode Integer, dont le facteur vaut round(EraPixelMm / PixelMm).</summary>
    internal const double EraPixelMm = 0.42;

    /// <summary>Repli quand ni la config ni --pixel-mm ne disent la taille d'un pixel de
    /// l'écran hôte : un 24" en 2560 × 1440 est à 0,21, un 27" Full HD à 0,31.</summary>
    internal const double DefaultPixelMm = 0.25;

    /// <summary>Ordre de défilement dans le menu.</summary>
    internal static readonly CrtMonitor[] Cycle =
        [CrtMonitor.In14, CrtMonitor.In15, CrtMonitor.In17, CrtMonitor.Integer];

    internal CrtMonitor Monitor { get; set; } = CrtMonitor.In15;

    internal bool Scanlines { get; set; }

    internal double PixelMm { get; set; } = DefaultPixelMm;

    /// <summary>Le .cfg d'où viennent les réglages : --config ou le fichier chargé par
    /// l'écran de construction. Null : réglages de session.</summary>
    internal string? ConfigPath { get; set; }

    internal CrtMonitor? MonitorOverride { get; set; }

    internal bool? ScanlinesOverride { get; set; }

    internal double? PixelMmOverride { get; set; }

    /// <summary>Posé par l'hôte à chaque image : les lignes sont demandées mais il y a
    /// moins de deux pixels hôte par ligne émulée pour les dessiner.</summary>
    internal bool ScanlinesTooFine { get; set; }

    /// <summary>
    /// Surface VISIBLE du tube, en mm, 4:3. Un 14" montrait ~27 cm de large : la
    /// diagonale annoncée est celle du verre, pas celle de l'image.
    /// </summary>
    internal static (double Width, double Height) VisibleMm(CrtMonitor monitor) => monitor switch
    {
        CrtMonitor.In14 => (270.0, 202.5),
        CrtMonitor.In15 => (280.0, 210.0),
        CrtMonitor.In17 => (320.0, 240.0),
        _ => (0.0, 0.0),
    };

    internal static string Describe(CrtMonitor monitor) =>
        monitor == CrtMonitor.Integer ? "pixels entiers" : $"{(int)monitor} pouces";

    internal static bool TryParseMonitor(string text, out CrtMonitor monitor)
    {
        monitor = text switch
        {
            "14" => CrtMonitor.In14,
            "15" => CrtMonitor.In15,
            "17" => CrtMonitor.In17,
            "entier" => CrtMonitor.Integer,
            _ => (CrtMonitor)(-1),
        };

        return (int)monitor >= 0;
    }

    /// <summary>Lit [SDL2] dans l'arbre CFG_MACHINE déjà chargé, puis applique la ligne
    /// de commande. Rappelable : l'écran de construction peut charger un autre fichier.</summary>
    internal void Load()
    {
        int monitor = config.config_get_int(config.CFG_MACHINE, Section, "monitor", (int)Monitor);
        if (Array.IndexOf(Cycle, (CrtMonitor)monitor) >= 0)
            Monitor = (CrtMonitor)monitor;

        Scanlines = config.config_get_int(config.CFG_MACHINE, Section, "crt", Scanlines ? 1 : 0) != 0;

        float pixelMm = config.config_get_float(config.CFG_MACHINE, Section, "pixel_mm", (float)PixelMm);
        if (pixelMm is > 0f and <= 2f)
            PixelMm = pixelMm;

        Monitor = MonitorOverride ?? Monitor;
        Scanlines = ScanlinesOverride ?? Scanlines;
        PixelMm = PixelMmOverride ?? PixelMm;
    }

    /// <summary>
    /// Seuls les fichiers de configs/ sont réécrits. config_save réémet l'arbre, qui n'a
    /// pas de commentaires (voir SdlSetup.SaveMachine) : un ixtal26.cfg documenté à la
    /// main perdrait toute sa documentation.
    /// </summary>
    internal bool CanSave =>
        ConfigPath is not null &&
        string.Equals(Path.GetFileName(Path.GetDirectoryName(Path.GetFullPath(ConfigPath))), "configs",
                      StringComparison.Ordinal);

    /// <summary>Écrit les trois clés et rend la phrase à afficher.</summary>
    internal string Save()
    {
        if (ConfigPath is null)
            return "pour cette session (pas de config chargee).";

        if (!CanSave)
            return $"pour cette session ({Path.GetFileName(ConfigPath)} hors de configs/).";

        config.config_set_int(config.CFG_MACHINE, Section, "monitor", (int)Monitor);
        config.config_set_int(config.CFG_MACHINE, Section, "crt", Scanlines ? 1 : 0);
        config.config_set_float(config.CFG_MACHINE, Section, "pixel_mm", (float)PixelMm);
        config.config_save(config.CFG_MACHINE, ConfigPath);

        return $"enregistre dans configs/{Path.GetFileName(ConfigPath)}.";
    }
}
