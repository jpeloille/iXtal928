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
/// commande (--monitor, --crt, --pixel-mm, --host-diagonal), comme pour le reste de la
/// machine.
///
/// `scale_mode` est la clé de PCem, avec son sens (0 plus proche voisin, 1 linéaire,
/// défaut 1 : wx-sdl2-video.c:29, wx-sdl2-video-renderer.c:20).
///
/// DEVIATION: les clés `monitor`, `crt`, `visible_fraction`, `pixel_mm` et
///   `host_diagonal` n'existent pas chez PCem. Elles vivent dans la section [SDL2] où
///   PCem range son `scale` (wx-sdl2.c:249), mais ne réutilisent pas `scale` : chez PCem
///   c'est un indice 0,5× … 2,5× de la trame, ici la surface est celle d'un moniteur,
///   quelle que soit la trame.
/// </summary>
internal sealed class DisplaySettings
{
    private const string Section = "SDL2";

    /// <summary>Pixel d'un 14 pouces VGA de 1990 : 640 points sur ~27 cm visibles. Sert
    /// au mode Integer, dont le facteur vaut round(EraPixelMm / PixelMm).</summary>
    internal const double EraPixelMm = 0.42;

    /// <summary>Repli quand ni pixel_mm ni host_diagonal ne sont connus. 0,2331 est l'écran
    /// de développement : 27" 16:9 en 2560 × 1440, soit 596,7 mm / 2560. Un 27" Full HD
    /// est à 0,31, un 27" 4K à 0,16.</summary>
    internal const double DefaultPixelMm = 0.2331;

    /// <summary>Part de la diagonale annoncée qui était réellement visible : un 15"
    /// montrait ~13,8" d'image, le reste étant le verre sous le cache.</summary>
    internal const double DefaultVisibleFraction = 0.92;

    /// <summary>Ordre de défilement dans le menu.</summary>
    internal static readonly CrtMonitor[] Cycle =
        [CrtMonitor.In14, CrtMonitor.In15, CrtMonitor.In17, CrtMonitor.Integer];

    internal CrtMonitor Monitor { get; set; } = CrtMonitor.In15;

    internal bool Scanlines { get; set; }

    /// <summary>Filtrage sous un moniteur : linéaire (doux) ou plus proche voisin (net).
    /// Les pixels entiers sont toujours nets : leur facteur est exact.</summary>
    internal bool Smooth { get; set; } = true;

    internal double VisibleFraction { get; set; } = DefaultVisibleFraction;

    /// <summary>Taille d'un pixel hôte forcée, en mm. Zéro : déduite par ResolvePixelMm.</summary>
    internal double PixelMm { get; set; }

    /// <summary>Diagonale de la dalle hôte, en pouces. Zéro : inconnue.</summary>
    internal double HostDiagonalInches { get; set; }

    /// <summary>Le .cfg d'où viennent les réglages : --config ou le fichier chargé par
    /// l'écran de construction. Null : réglages de session.</summary>
    internal string? ConfigPath { get; set; }

    internal CrtMonitor? MonitorOverride { get; set; }

    internal bool? ScanlinesOverride { get; set; }

    internal double? PixelMmOverride { get; set; }

    internal double? HostDiagonalOverride { get; set; }

    /// <summary>Posé par l'hôte à chaque image : les lignes sont demandées mais il y a
    /// moins de deux pixels hôte par ligne émulée pour les dessiner.</summary>
    internal bool ScanlinesTooFine { get; set; }

    /// <summary>
    /// Surface VISIBLE du tube, en mm : diagonale annoncée × fraction visible, décomposée
    /// en 4:3 (le triangle 3-4-5 : largeur 4/5, hauteur 3/5 de la diagonale). 15" à 0,92 :
    /// 280,4 × 210,3 mm.
    /// </summary>
    internal static (double Width, double Height) VisibleMm(CrtMonitor monitor, double visibleFraction)
    {
        double diagonalMm = (int)monitor * visibleFraction * 25.4;
        return (diagonalMm * 4.0 / 5.0, diagonalMm * 3.0 / 5.0);
    }

    /// <summary>
    /// Taille d'un pixel hôte : pixel_mm s'il est donné, sinon la diagonale de la dalle
    /// rapportée à sa résolution native (que SDL connaît, pas sa taille physique), sinon
    /// DefaultPixelMm. 27" en 2560 × 1440 : 108,8 ppi, 0,2335 mm.
    /// </summary>
    internal double ResolvePixelMm(int nativeWidth, int nativeHeight)
    {
        if (PixelMm > 0)
            return PixelMm;

        if (HostDiagonalInches > 0 && nativeWidth > 0 && nativeHeight > 0)
            return HostDiagonalInches * 25.4 / Math.Sqrt((double)nativeWidth * nativeWidth +
                                                         (double)nativeHeight * nativeHeight);

        return DefaultPixelMm;
    }

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
        Smooth = config.config_get_int(config.CFG_MACHINE, Section, "scale_mode", Smooth ? 1 : 0) != 0;

        float fraction = config.config_get_float(config.CFG_MACHINE, Section, "visible_fraction",
                                                 (float)VisibleFraction);
        if (fraction is > 0.5f and <= 1f)
            VisibleFraction = fraction;

        float pixelMm = config.config_get_float(config.CFG_MACHINE, Section, "pixel_mm", (float)PixelMm);
        if (pixelMm is > 0f and <= 2f)
            PixelMm = pixelMm;

        float diagonal = config.config_get_float(config.CFG_MACHINE, Section, "host_diagonal",
                                                 (float)HostDiagonalInches);
        if (diagonal is > 0f and <= 200f)
            HostDiagonalInches = diagonal;

        Monitor = MonitorOverride ?? Monitor;
        Scanlines = ScanlinesOverride ?? Scanlines;
        PixelMm = PixelMmOverride ?? PixelMm;
        HostDiagonalInches = HostDiagonalOverride ?? HostDiagonalInches;
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

    /// <summary>Écrit les clés et rend la phrase à afficher. pixel_mm et host_diagonal ne
    /// sont écrits que s'ils sont connus : un zéro écrit se relirait comme « inconnu », mais
    /// laisserait croire à qui ouvre le fichier qu'il a été choisi.</summary>
    internal string Save()
    {
        if (ConfigPath is null)
            return "pour cette session (pas de config chargee).";

        if (!CanSave)
            return $"pour cette session ({Path.GetFileName(ConfigPath)} hors de configs/).";

        config.config_set_int(config.CFG_MACHINE, Section, "monitor", (int)Monitor);
        config.config_set_int(config.CFG_MACHINE, Section, "crt", Scanlines ? 1 : 0);
        config.config_set_int(config.CFG_MACHINE, Section, "scale_mode", Smooth ? 1 : 0);
        config.config_set_float(config.CFG_MACHINE, Section, "visible_fraction", (float)VisibleFraction);

        if (PixelMm > 0)
            config.config_set_float(config.CFG_MACHINE, Section, "pixel_mm", (float)PixelMm);

        if (HostDiagonalInches > 0)
            config.config_set_float(config.CFG_MACHINE, Section, "host_diagonal", (float)HostDiagonalInches);

        config.config_save(config.CFG_MACHINE, ConfigPath);

        return $"enregistre dans configs/{Path.GetFileName(ConfigPath)}.";
    }
}
