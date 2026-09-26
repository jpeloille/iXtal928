// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/wx-ui/wx-sdl2.c:236-290 (sdl_loadconfig / sdl_saveconfig, section [SDL2])
// STATUS: host

using iXtal26.PluginApi;

namespace iXtal26.Host;

/// <summary>Moniteur simulé. Auto : le NEC MultiSync 3V derrière une carte VGA ou
/// Trident, un générique 14" derrière la CGA, que le 3V ne sait pas synchroniser.
/// Integer : pas de moniteur, pixels entiers.</summary>
internal enum CrtMonitor
{
    Auto,
    Nec3V,
    Generic14,
    Generic15,
    Generic17,
    Integer,
}

/// <summary>
/// Ce qu'un moniteur montre et ce qu'il accepte. Diagonale VISIBLE, en pouces ; plages
/// de balayage en kHz et Hz. Un générique accepte tout.
/// </summary>
internal readonly record struct MonitorProfile(
    string Name, double VisibleDiagonalInches,
    double MinHorizontalKHz, double MaxHorizontalKHz, double MinVerticalHz, double MaxVerticalHz)
{
    internal bool Accepts(double horizontalKHz, double verticalHz) =>
        horizontalKHz >= MinHorizontalKHz && horizontalKHz <= MaxHorizontalKHz &&
        verticalHz >= MinVerticalHz && verticalHz <= MaxVerticalHz;

    internal bool IsGeneric => MinHorizontalKHz <= 0 && double.IsPositiveInfinity(MaxHorizontalKHz);
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
/// DEVIATION: les clés `monitor`, `crt`, `fill_percent`, `visible_fraction`, `pixel_mm`
///   et `host_diagonal` n'existent pas chez PCem. Elles vivent dans la section [SDL2] où
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

    /// <summary>Part de la diagonale annoncée réellement visible, pour les génériques.
    /// 0,93 : le ratio des NEC MultiSync (3V : 15" -> 14", 95 : 19" -> 18",
    /// crtdatabase.com).</summary>
    internal const double DefaultVisibleFraction = 0.93;

    /// <summary>
    /// NEC MultiSync 3V (JC-1535VMA, 1994) : 15" nominal, 14" visible, pas 0,28 mm,
    /// 31-50 kHz, 55-90 Hz (crtdatabase.com/crts/nec/nec-jc-1535vma). VGA, SVGA et
    /// 1024×768 à 60 Hz (48,4 kHz) ; ni CGA (15,7 kHz) ni EGA (21,8 kHz).
    /// </summary>
    internal static readonly MonitorProfile Nec3V = new("NEC MultiSync 3V", 14.0, 31.0, 50.0, 55.0, 90.0);

    /// <summary>Ordre de défilement dans le menu.</summary>
    internal static readonly CrtMonitor[] Cycle =
    [
        CrtMonitor.Auto, CrtMonitor.Nec3V, CrtMonitor.Generic14, CrtMonitor.Generic15,
        CrtMonitor.Generic17, CrtMonitor.Integer,
    ];

    internal CrtMonitor Monitor { get; set; } = CrtMonitor.Auto;

    internal bool Scanlines { get; set; }

    /// <summary>Bornes et défaut de FillPercent. 90 : l'image ne touchait pas les bords
    /// du tube, par réglage d'usine ou de son propriétaire ; aucune fiche ne le donne.</summary>
    internal const int MinFillPercent = 70;
    internal const int MaxFillPercent = 100;
    internal const int DefaultFillPercent = 90;

    /// <summary>
    /// Molettes H-SIZE/V-SIZE : part de la surface visible du tube réellement couverte par
    /// l'image, en %. Même taux sur les deux axes, le 4:3 reste. Sans effet en pixels
    /// entiers, où il n'y a pas de tube.
    /// </summary>
    internal int FillPercent { get; set; } = DefaultFillPercent;

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

    internal int? FillPercentOverride { get; set; }

    internal double? PixelMmOverride { get; set; }

    internal double? HostDiagonalOverride { get; set; }

    /// <summary>Posé par l'hôte à chaque image : les lignes sont demandées mais il y a
    /// moins de deux pixels hôte par ligne émulée pour les dessiner.</summary>
    internal bool ScanlinesTooFine { get; set; }

    /// <summary>Posé par l'hôte à chaque image : le signal reçu quand le moniteur le
    /// refuse (« 15.7 kHz / 60 Hz »), null sinon.</summary>
    internal string? OutOfRange { get; set; }

    /// <summary>Le moniteur effectif pour la carte vidéo configurée.</summary>
    internal CrtMonitor Effective() =>
        Resolve(Monitor, Video.video.video_get_internal_name(Video.video.video_old_to_new(pc.gfxcard)) != "cga");

    /// <summary>Auto résolu : le 3V s'il peut afficher la carte, sinon un générique 14".</summary>
    internal static CrtMonitor Resolve(CrtMonitor monitor, bool vgaClassCard) =>
        monitor != CrtMonitor.Auto ? monitor : vgaClassCard ? CrtMonitor.Nec3V : CrtMonitor.Generic14;

    /// <summary>Profil d'un moniteur résolu (ni Auto ni Integer).</summary>
    internal static MonitorProfile Profile(CrtMonitor monitor, double visibleFraction)
    {
        if (monitor == CrtMonitor.Nec3V)
            return Nec3V;

        int nominal = monitor switch
        {
            CrtMonitor.Generic14 => 14,
            CrtMonitor.Generic17 => 17,
            _ => 15,
        };

        return new MonitorProfile($"generique {nominal} pouces", nominal * visibleFraction,
                                  0, double.PositiveInfinity, 0, double.PositiveInfinity);
    }

    /// <summary>
    /// Surface VISIBLE du tube, en mm : diagonale visible décomposée en 4:3 (le triangle
    /// 3-4-5 : largeur 4/5, hauteur 3/5). 3V, 14" visibles : 284,5 × 213,4 mm.
    /// </summary>
    internal static (double Width, double Height) VisibleMm(MonitorProfile profile)
    {
        double diagonalMm = profile.VisibleDiagonalInches * 25.4;
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

    internal static string Describe(CrtMonitor monitor) => monitor switch
    {
        CrtMonitor.Auto => "auto",
        CrtMonitor.Nec3V => "NEC MultiSync 3V",
        CrtMonitor.Integer => "pixels entiers",
        _ => Profile(monitor, DefaultVisibleFraction).Name,
    };

    /// <summary>Valeur de la clé `monitor` et de --monitor.</summary>
    internal static string ConfigName(CrtMonitor monitor) => monitor switch
    {
        CrtMonitor.Nec3V => "nec3v",
        CrtMonitor.Generic14 => "14",
        CrtMonitor.Generic15 => "15",
        CrtMonitor.Generic17 => "17",
        CrtMonitor.Integer => "entier",
        _ => "auto",
    };

    /// <summary>« 0 » est la valeur que le premier format de la clé donnait à entier.</summary>
    internal static bool TryParseMonitor(string text, out CrtMonitor monitor)
    {
        switch (text.Trim().ToLowerInvariant())
        {
            case "auto": monitor = CrtMonitor.Auto; return true;
            case "nec3v" or "3v": monitor = CrtMonitor.Nec3V; return true;
            case "14": monitor = CrtMonitor.Generic14; return true;
            case "15": monitor = CrtMonitor.Generic15; return true;
            case "17": monitor = CrtMonitor.Generic17; return true;
            case "entier" or "0": monitor = CrtMonitor.Integer; return true;
            default: monitor = CrtMonitor.Auto; return false;
        }
    }

    /// <summary>Lit [SDL2] dans l'arbre CFG_MACHINE déjà chargé, puis applique la ligne
    /// de commande. Rappelable : l'écran de construction peut charger un autre fichier.</summary>
    internal void Load()
    {
        if (TryParseMonitor(config.config_get_string(config.CFG_MACHINE, Section, "monitor",
                                                     ConfigName(Monitor)), out CrtMonitor monitor))
            Monitor = monitor;

        Scanlines = config.config_get_int(config.CFG_MACHINE, Section, "crt", Scanlines ? 1 : 0) != 0;
        Smooth = config.config_get_int(config.CFG_MACHINE, Section, "scale_mode", Smooth ? 1 : 0) != 0;
        FillPercent = Math.Clamp(config.config_get_int(config.CFG_MACHINE, Section, "fill_percent", FillPercent),
                                 MinFillPercent, MaxFillPercent);

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
        FillPercent = FillPercentOverride ?? FillPercent;
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

        config.config_set_string(config.CFG_MACHINE, Section, "monitor", ConfigName(Monitor));
        config.config_set_int(config.CFG_MACHINE, Section, "crt", Scanlines ? 1 : 0);
        config.config_set_int(config.CFG_MACHINE, Section, "scale_mode", Smooth ? 1 : 0);
        config.config_set_int(config.CFG_MACHINE, Section, "fill_percent", FillPercent);
        config.config_set_float(config.CFG_MACHINE, Section, "visible_fraction", (float)VisibleFraction);

        if (PixelMm > 0)
            config.config_set_float(config.CFG_MACHINE, Section, "pixel_mm", (float)PixelMm);

        if (HostDiagonalInches > 0)
            config.config_set_float(config.CFG_MACHINE, Section, "host_diagonal", (float)HostDiagonalInches);

        config.config_save(config.CFG_MACHINE, ConfigPath);

        return $"enregistre dans configs/{Path.GetFileName(ConfigPath)}.";
    }
}
