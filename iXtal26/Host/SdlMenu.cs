// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/wx-ui/wx-sdl2.c:725-770 (IDM_DISC_A/B, IDM_EJECT_A/B,
//         IDM_FILE_HRESET, IDM_FILE_RESET_CAD, IDM_FILE_EXIT)
//         + wx-createdisc.cc:22-29, 62-73 (IDM_DISC_CREATE)
// STATUS: host

using iXtal26.Disc;
using iXtal26.Floppy;
using iXtal26.PluginApi;
using SDL3;

namespace iXtal26.Host;

/// <summary>Ce que l'hôte doit faire ; le menu exécute le reste lui-même.</summary>
internal enum MenuAction
{
    None,
    HardReset,
    HardResetTurbo,
    Cad,
    Quit,

    /// <summary>Moniteur ou lignes de balayage changés : l'hôte réapplique, le menu
    /// reste ouvert pour qu'on voie l'effet.</summary>
    DisplayChanged,
}

/// <summary>
/// Menu en surimpression, ouvert par Ctrl+F12 : insérer ou éjecter une disquette, et
/// réinitialiser la machine. PCem porte ces mêmes commandes dans un menu wxWidgets
/// (wx-sdl2.c:725-770) ; ici elles tiennent dans la fenêtre SDL, dessinées avec la
/// police 8x8 intégrée à SDL.
///
/// Le menu NE DESSINE JAMAIS dans video.Buffer32 : il se compose sur le renderer, après
/// que la texture du CGA a été rendue. C'est ce qui garde l'image émulée comparable à
/// l'oracle (même raison que le refus documenté dans SdlHost.UpdateTitle).
/// </summary>
internal sealed class SdlMenu
{
    /// <summary>Les quatre extensions que disc.cs:110-113 sait charger. FDI est omis (registre des omissions).</summary>
    private static readonly string[] Extensions = [".img", ".ima", ".360", ".xdf"];

    private static readonly SDL.DialogFileFilter[] DialogFilters =
    [
        new SDL.DialogFileFilter("Disc image", "img;ima;360;xdf"),
        new SDL.DialogFileFilter("All files", "*"),
    ];

    /// <summary>Le filtre du choix d'une SOURCE à déposer : un fichier à mettre sur une
    /// disquette n'a aucune extension particulière.</summary>
    private static readonly SDL.DialogFileFilter[] AnyFileFilter =
    [
        new SDL.DialogFileFilter("All files", "*"),
    ];

    /// <summary>Taille d'une cellule de la police de débogage de SDL, en pixels non mis à l'échelle.</summary>
    private const int Cell = 8;

    /// <summary>
    /// Hauteur d'une ligne. Les glyphes de SDL remplissent leur cellule de 8 px de haut :
    /// empiler les lignes tous les 8 px ne laisse AUCUN blanc entre elles, et la liste
    /// devient un pâté où les jambages d'une ligne touchent les hampes de la suivante.
    /// Constaté à l'écran, pas déduit.
    /// </summary>
    private const int Row = Cell + 2;

    /// <summary>Largeur de la boîte, en caractères. Le plus long libellé y tient avec sa marge.</summary>
    internal const int Cols = 46;

    /// <summary>Entrées d'image visibles à la fois dans l'écran de choix.</summary>
    internal const int PickWindow = 10;

    private enum Screen { Main, Pick, Create, CreateFat, CreateHdd, Dialog }

    /// <summary>
    /// À quoi sert la boîte de dialogue en cours. Le sélecteur du système est ASYNCHRONE
    /// et il n'en existe qu'un : sans cette étiquette, le résultat qui revient est
    /// indiscernable entre « l'image à insérer », « l'image où déposer » et « le fichier
    /// hôte à déposer ». Le dépôt en enchaîne deux, d'où le besoin.
    /// </summary>
    private enum DialogPurpose { InsertDisc, PutTarget, PutSource }

    // Les entrées de l'écran principal, dans l'ordre d'affichage. L'index sélectionné
    // indexe ce tableau : pas de correspondance à maintenir entre le texte et l'action.
    private static readonly (string Label, MainItem Item)[] MainItems =
    [
        ("Inserer une disquette dans A:", MainItem.InsertA),
        ("Inserer une disquette dans B:", MainItem.InsertB),
        ("Ejecter A:", MainItem.EjectA),
        ("Ejecter B:", MainItem.EjectB),
        ("Creer une disquette vierge...", MainItem.CreateBlank),
        ("Creer une disquette formatee...", MainItem.CreateFat),
        ("Deposer un fichier de l'hote...", MainItem.PutFile),
        ("Creer un disque dur vierge...", MainItem.CreateBlankHdd),
        ("Moniteur", MainItem.Monitor),
        ("Lignes CRT", MainItem.Scanlines),
        ("Filtrage", MainItem.Filter),
        ("Taille d'image", MainItem.Fill),
        ("Reset materiel (temps reel)", MainItem.HardReset),
        ("Reset materiel + turbo", MainItem.HardResetTurbo),
        ("Ctrl+Alt+Suppr (redemarrage a chaud)", MainItem.Cad),
        ("Quitter", MainItem.Quit),
    ];

    private enum MainItem
    {
        InsertA, InsertB, EjectA, EjectB, CreateBlank, CreateFat, PutFile, CreateBlankHdd,
        Monitor, Scanlines, Filter, Fill, HardReset, HardResetTurbo, Cad, Quit,
    }

    // pcem: wx-createdisc.cc:22-29 — réduit aux quatre formats que le lecteur 5,25" DD du
    // 5150 sait lire : drive_types[1] ne porte que FLAG_HOLE0 et max_track = 41
    // (fdd.cs:92-94), donc fdd_can_read_medium (fdd.cs:219-233) refuse toute image HD ou ED.
    //
    // PCem ne stocke PAS la géométrie, seulement le PRODUIT faces x pistes x secteurs :
    // elle est re-déduite de la TAILLE DU FICHIER par img_load (disc_img.cs:242-305). La
    // factorisation « 1 * 40 * 8 » n'est donc que de la documentation au niveau du source,
    // exactement comme dans le C.
    // omitted: 720 kB, 1.2 MB, 1.44 MB, 2.88 MB (wx-createdisc.cc:26-28) — haute et extra
    //   densité, illisibles par un lecteur de type 1.
    // omitted: « 100 MB (Zip) » (wx-createdisc.cc:28) — 2048 * 96 secteurs, ZIP_SECTORS
    //   (scsi_zip.c:15) : ce n'est pas une disquette, et PCem lui donne un menu séparé.
    private static readonly (string Name, string Stem, int NrSectors)[] DiscFormats =
    [
        ("160 Ko   8 sect. x 40 pistes x 1 face",  "160k", 1 * 40 * 8),
        ("180 Ko   9 sect. x 40 pistes x 1 face",  "180k", 1 * 40 * 9),
        ("320 Ko   8 sect. x 40 pistes x 2 faces", "320k", 2 * 40 * 8),
        ("360 Ko   9 sect. x 40 pistes x 2 faces", "360k", 2 * 40 * 9),
    ];

    private readonly IntPtr _window;
    private readonly IntPtr _renderer;

    /// <summary>
    /// Chemin de ROM déjà résolu par l'hôte. Sert de repère de repli pour trouver os/ :
    /// initpc a garanti que ce répertoire existe, donc son frère est la racine du dépôt.
    /// </summary>
    private readonly string _romsPath;

    // Enraciné contre le GC pour toute la vie du menu : SDL garde le pointeur de
    // fonction jusqu'à l'appel du rappel, qui arrive plusieurs secondes plus tard.
    private readonly SDL.DialogFileCallback _dialogCallback;

    private Screen _screen;
    private int _mainIndex;
    private int _pickIndex;
    private int _pickTop;
    private int _createIndex;

    /// <summary>Type de disque dur choisi, 0 pour le type 01. Quarante-six entrées : il
    /// faut aussi mémoriser le haut de la fenêtre, comme l'écran de choix d'image.</summary>
    private int _createHddIndex;

    private int _createHddTop;

    /// <summary>Format choisi dans l'écran de disquette FORMATÉE, index dans
    /// FatImage.Formats.</summary>
    private int _createFatIndex;

    /// <summary>Lecteur visé par l'écran de choix : 0 = A:, 1 = B:, ou PickPutTarget
    /// quand la liste sert à désigner l'image où DÉPOSER au lieu du lecteur où
    /// insérer.</summary>
    private int _pickDrive;

    /// <summary>Valeur de _pickDrive qui fait de l'écran de choix un choix de CIBLE de
    /// dépôt. -1 et pas un booléen de plus : c'est le même écran, la même liste et le
    /// même défilement, et deux états parallèles finiraient par se contredire.</summary>
    private const int PickPutTarget = -1;

    /// <summary>Image désignée pour un dépôt, entre le choix de la cible et celui de la
    /// source. Le sélecteur du système étant asynchrone, elle doit survivre au retour à
    /// la boucle d'évènements.</summary>
    private string _putImage = "";

    private DialogPurpose _dialogPurpose;

    /// <summary>Une ligne de l'écran de choix : une image, un sous-dossier ou « .. ».</summary>
    private readonly record struct PickEntry(string Path, string Label, bool IsDirectory);

    /// <summary>Contenu de _pickDir : « .. » s'il y a lieu, les sous-dossiers qui
    /// contiennent des images, puis les images du niveau.</summary>
    private PickEntry[] _entries = [];
    private string _imagesRoot = "";

    /// <summary>Dossier affiché par l'écran de choix, toujours sous _imagesRoot. Gardé
    /// d'une ouverture à l'autre : on retrouve le dossier de la dernière insertion.</summary>
    private string _pickDir = "";

    /// <summary>Dernier message d'état, affiché sous les entrées. Vide = rien à dire.</summary>
    private string _message = "";

    // Résultat de la boîte de dialogue native. ÉCRITS DEPUIS UN AUTRE FIL (voir
    // OnDialogResult) ; _dialogDone est la barrière qui les publie.
    private string? _dialogPath;
    private string? _dialogError;
    private int _dialogDone;

    internal bool IsOpen { get; private set; }

    internal int Opens { get; private set; }
    internal int Inserts { get; private set; }
    internal int Resets { get; private set; }
    internal int Creations { get; private set; }
    internal int Puts { get; private set; }

    /// <summary>Réglages d'affichage de l'hôte : le menu les change et les enregistre,
    /// SdlHost les applique sur MenuAction.DisplayChanged.</summary>
    private readonly DisplaySettings _display;

    internal SdlMenu(IntPtr window, IntPtr renderer, string romsPath, DisplaySettings? display = null)
    {
        _display = display ?? new DisplaySettings();
        _window = window;
        _renderer = renderer;
        _romsPath = romsPath;
        _dialogCallback = OnDialogResult;
    }

    /// <summary>
    /// Où vivent les images. resolve_roms_path donne d'abord sa chance au répertoire
    /// courant puis remonte depuis le binaire (paths.cs:132) — mais il rend le chemin
    /// TEL QUEL quand rien n'est trouvé, et « os » relatif vaut alors bin/Debug/net10.0/os
    /// sous Rider. Pour LIRE c'est sans conséquence, la liste sort vide ; pour ÉCRIRE cela
    /// créerait le répertoire au mauvais endroit. D'où le repli sur le frère de roms/,
    /// dont initpc a déjà garanti l'existence.
    /// </summary>
    private string ImagesRoot() => ImagesRoot(_romsPath);

    /// <summary>
    /// `internal static` depuis M12.1 : --create-hdd doit écrire dans le MÊME os/ que le
    /// menu, sans quoi une image créée en ligne de commande n'apparaîtrait pas dans la
    /// liste du menu, et réciproquement. Deux résolutions de chemin, ce sont deux
    /// répertoires qui finissent par différer.
    /// </summary>
    internal static string ImagesRoot(string romsPath)
    {
        string root = PluginApi.paths.resolve_roms_path("os");

        if (Directory.Exists(root))
            return root;

        string? parent = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(romsPath));

        return parent is null ? root : Path.Combine(parent, "os");
    }

    internal void Open()
    {
        IsOpen = true;
        Opens++;
        _screen = Screen.Main;
        _mainIndex = 0;
        _message = "";

        // Toutes les touches relâchées : sans cela le Ctrl de l'accord d'ouverture reste
        // enfoncé dans keyboard.rawinputkey[0x1d] pendant toute la durée du menu, et la
        // machine émulée le voit comme un Ctrl tenu à jamais (SdlKeyboard.Reset, qui
        // existe déjà pour le même problème sur perte de focus).
        SdlKeyboard.Reset();
    }

    internal void Close()
    {
        IsOpen = false;

        // Et à la fermeture pour la raison symétrique : l'Entrée qui vient de valider une
        // action ne doit pas rester enfoncée pour le BIOS qui, souvent, redémarre juste après.
        SdlKeyboard.Reset();
    }

    /// <summary>
    /// À appeler une fois par tour de boucle tant que le menu est ouvert, même sans
    /// évènement. Le rappel de la boîte de dialogue native arrive quand le système veut,
    /// souvent sans qu'aucun évènement SDL ne suive : récolter le résultat depuis
    /// HandleEvent SEULEMENT laisserait le fichier choisi en attente jusqu'à la prochaine
    /// frappe, ce qui se lit comme « le selecteur n'a rien fait ».
    /// </summary>
    internal void Poll() => CollectDialogResult();

    /// <summary>
    /// Consomme un évènement. Le menu est modal du point de vue du clavier : l'appelant
    /// ne transmet RIEN à la machine émulée tant que IsOpen. Rend l'action que seul
    /// l'hôte peut exécuter (reset, turbo, quitter) ; insertion et éjection sont faites ici.
    /// </summary>
    internal MenuAction HandleEvent(in SDL.Event e)
    {
        CollectDialogResult();

        if ((SDL.EventType)e.Type != SDL.EventType.KeyDown)
            return MenuAction.None;

        switch (_screen)
        {
            case Screen.Main:
                return HandleMain(e.Key.Scancode);

            case Screen.Pick:
                HandlePick(e.Key.Scancode);
                return MenuAction.None;

            case Screen.Create:
                HandleCreate(e.Key.Scancode);
                return MenuAction.None;

            case Screen.CreateFat:
                HandleCreateFat(e.Key.Scancode);
                return MenuAction.None;

            case Screen.CreateHdd:
                HandleCreateHdd(e.Key.Scancode);
                return MenuAction.None;

            default:
                // La boîte de dialogue est ouverte : elle appartient au système, on ne
                // peut pas la fermer d'ici. Échap renonce seulement à son résultat.
                if (e.Key.Scancode == SDL.Scancode.Escape)
                {
                    _screen = Screen.Main;
                    _message = "choix de fichier abandonne.";
                }

                return MenuAction.None;
        }
    }

    private MenuAction HandleMain(SDL.Scancode sc)
    {
        switch (sc)
        {
            case SDL.Scancode.Up:
                _mainIndex = (_mainIndex + MainItems.Length - 1) % MainItems.Length;
                return MenuAction.None;

            case SDL.Scancode.Down:
                _mainIndex = (_mainIndex + 1) % MainItems.Length;
                return MenuAction.None;

            case SDL.Scancode.Escape:
                Close();
                return MenuAction.None;

            case SDL.Scancode.Left or SDL.Scancode.Right
                when MainItems[_mainIndex].Item == MainItem.Monitor:
                return ChangeMonitor(sc == SDL.Scancode.Left ? -1 : 1);

            case SDL.Scancode.Left or SDL.Scancode.Right
                when MainItems[_mainIndex].Item == MainItem.Fill:
                return ChangeFill(sc == SDL.Scancode.Left ? -1 : 1);

            case SDL.Scancode.Return or SDL.Scancode.KpEnter:
                break;

            default:
                return MenuAction.None;
        }

        switch (MainItems[_mainIndex].Item)
        {
            case MainItem.Monitor:
                return ChangeMonitor(1);

            case MainItem.Scanlines:
                _display.Scanlines = !_display.Scanlines;
                _message = "lignes CRT " + _display.Save();
                return MenuAction.DisplayChanged;

            case MainItem.Fill:
                // Entrée avance de 5 et reboucle sur le minimum : les flèches font le détail.
                return ChangeFill(_display.FillPercent >= DisplaySettings.MaxFillPercent
                                  ? DisplaySettings.MinFillPercent - _display.FillPercent
                                  : Math.Min(5, DisplaySettings.MaxFillPercent - _display.FillPercent));

            case MainItem.Filter:
                _display.Smooth = !_display.Smooth;
                _message = "filtrage " + _display.Save();
                return MenuAction.DisplayChanged;

            case MainItem.InsertA:
                OpenPick(0);
                return MenuAction.None;

            case MainItem.InsertB:
                OpenPick(1);
                return MenuAction.None;

            case MainItem.EjectA:
                Eject(0);
                return MenuAction.None;

            case MainItem.EjectB:
                Eject(1);
                return MenuAction.None;

            case MainItem.CreateBlank:
                _createIndex = DiscFormats.Length - 1; /* 360 Ko : le format du 5150 à deux faces */
                _message = "";
                _screen = Screen.Create;
                return MenuAction.None;

            case MainItem.CreateFat:
                _createFatIndex = FatImage.Formats.Length - 1; /* 360 Ko, le format du 5150 */
                _message = "";
                _screen = Screen.CreateFat;
                return MenuAction.None;

            case MainItem.PutFile:
                OpenPick(PickPutTarget);
                return MenuAction.None;

            case MainItem.CreateBlankHdd:
                _createHddIndex = 0; /* type 01, 306x4x17 : le 10 Mo du XT */
                _createHddTop = 0;
                _message = "";
                _screen = Screen.CreateHdd;
                return MenuAction.None;

            case MainItem.HardReset:
                Resets++;
                return MenuAction.HardReset;

            case MainItem.HardResetTurbo:
                Resets++;
                return MenuAction.HardResetTurbo;

            case MainItem.Cad:
                Resets++;
                return MenuAction.Cad;

            default:
                return MenuAction.Quit;
        }
    }

    private MenuAction ChangeMonitor(int step)
    {
        int at = Array.IndexOf(DisplaySettings.Cycle, _display.Monitor);
        int count = DisplaySettings.Cycle.Length;
        _display.Monitor = DisplaySettings.Cycle[((at < 0 ? 0 : at) + step + count) % count];
        _message = "moniteur " + _display.Save();
        return MenuAction.DisplayChanged;
    }

    private MenuAction ChangeFill(int step)
    {
        _display.FillPercent = Math.Clamp(_display.FillPercent + step,
                                          DisplaySettings.MinFillPercent, DisplaySettings.MaxFillPercent);
        _message = "taille d'image " + _display.Save();
        return MenuAction.DisplayChanged;
    }

    /// <summary>Libellé de l'écran principal : les deux entrées d'affichage portent leur
    /// valeur courante, les autres leur texte fixe.</summary>
    private string MainLabel(string label, MainItem item) => item switch
    {
        MainItem.Monitor => _display.Monitor == CrtMonitor.Auto
                            ? $"{label} : auto ({DisplaySettings.Describe(_display.Effective())})"
                            : $"{label} : {DisplaySettings.Describe(_display.Monitor)}",
        MainItem.Scanlines => !_display.Scanlines ? $"{label} : non"
                              : _display.ScanlinesTooFine ? $"{label} : oui (trop fines ici)"
                              : $"{label} : oui",
        MainItem.Filter => _display.Effective() == CrtMonitor.Integer ? $"{label} : net (pixels entiers)"
                           : _display.Smooth ? $"{label} : doux" : $"{label} : net",
        MainItem.Fill => _display.Effective() == CrtMonitor.Integer
                         ? $"{label} : sans objet (pixels entiers)"
                         : $"{label} : {_display.FillPercent} %",
        _ => label,
    };

    private void HandlePick(SDL.Scancode sc)
    {
        // Une entrée de plus que la liste : la dernière est « Parcourir... ».
        int count = _entries.Length + 1;

        switch (sc)
        {
            case SDL.Scancode.Up:
                _pickIndex = (_pickIndex + count - 1) % count;
                break;

            case SDL.Scancode.Down:
                _pickIndex = (_pickIndex + 1) % count;
                break;

            case SDL.Scancode.Escape:
                _screen = Screen.Main;
                return;

            case SDL.Scancode.Backspace or SDL.Scancode.Left:
                if (!SamePath(_pickDir, _imagesRoot))
                    EnterDirectory(Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(_pickDir))!);
                return;

            case SDL.Scancode.Return or SDL.Scancode.KpEnter
                when _pickIndex < _entries.Length && _entries[_pickIndex].IsDirectory:
                EnterDirectory(_entries[_pickIndex].Path);
                return;

            case SDL.Scancode.Return or SDL.Scancode.KpEnter:
                // Trois issues, et la liste est la même dans les trois : insérer dans un
                // lecteur, désigner la cible d'un dépôt, ou aller chercher hors de os/.
                if (_pickIndex >= _entries.Length)
                {
                    Browse(_pickDrive == PickPutTarget ? DialogPurpose.PutTarget
                                                       : DialogPurpose.InsertDisc);
                }
                else if (_pickDrive == PickPutTarget)
                {
                    _putImage = _entries[_pickIndex].Path;
                    Browse(DialogPurpose.PutSource);
                }
                else
                {
                    Insert(_pickDrive, _entries[_pickIndex].Path);
                    _screen = Screen.Main;
                }

                return;

            default:
                return;
        }

        // Fenêtre de défilement : l'entrée choisie reste visible sans jamais sortir de
        // la boîte, qui a une hauteur fixe.
        if (_pickIndex < _pickTop)
            _pickTop = _pickIndex;
        else if (_pickIndex >= _pickTop + PickWindow)
            _pickTop = _pickIndex - PickWindow + 1;
    }

    private void HandleCreateFat(SDL.Scancode sc)
    {
        switch (sc)
        {
            case SDL.Scancode.Up:
                _createFatIndex = (_createFatIndex + FatImage.Formats.Length - 1) % FatImage.Formats.Length;
                break;

            case SDL.Scancode.Down:
                _createFatIndex = (_createFatIndex + 1) % FatImage.Formats.Length;
                break;

            case SDL.Scancode.Escape:
                _screen = Screen.Main;
                break;

            case SDL.Scancode.Return or SDL.Scancode.KpEnter:
                CreateFormatted(FatImage.Formats[_createFatIndex]);
                _screen = Screen.Main;
                break;
        }
    }

    /// <summary>
    /// Fabrique une disquette FORMATÉE dans os/, vide et prête à recevoir des fichiers.
    ///
    /// DEUX DIFFÉRENCES VOULUES avec sa voisine CreateBlank, et elles vont ensemble.
    /// CreateBlank insère l'image dans A: parce que son cas d'usage EST de la fabriquer
    /// pendant que DOS attend « Insert new diskette for drive A: » pour la formater.
    /// Celle-ci n'a pas besoin de FORMAT, et la suite naturelle est d'y déposer un
    /// fichier — ce que FatImage.Put refuse sur une image montée. L'insérer d'office
    /// mettrait donc l'utilisateur dans le seul état où l'étape suivante est interdite.
    /// </summary>
    private void CreateFormatted(FatImage.Format format)
    {
        string root = ImagesRoot();

        try
        {
            // os/ est .gitignore'd, donc absent d'un clone neuf.
            Directory.CreateDirectory(root);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _message = $"creation de {root} impossible : {ex.Message}";
            return;
        }

        string? path = FreeName(root, "fat" + format.Stem);

        if (path is null)
        {
            _message = "trop d'images de cette taille dans os/.";
            return;
        }

        if (!FatImage.Create(path, format, out string message))
        {
            Console.WriteLine(message);
            _message = $"echec : {Path.GetFileName(path)}";
            return;
        }

        Creations++;
        Console.WriteLine(message);
        _message = $"{Path.GetFileName(path)} cree, formate, vide.";
    }

    private void HandleCreateHdd(SDL.Scancode sc)
    {
        switch (sc)
        {
            case SDL.Scancode.Up:
                _createHddIndex = (_createHddIndex + 45) % 46;
                break;

            case SDL.Scancode.Down:
                _createHddIndex = (_createHddIndex + 1) % 46;
                break;

            case SDL.Scancode.Escape:
                _screen = Screen.Main;
                return;

            case SDL.Scancode.Return or SDL.Scancode.KpEnter:
                CreateBlankHdd(_createHddIndex + 1);
                _screen = Screen.Main;
                return;

            default:
                return;
        }

        // Même fenêtre de défilement que l'écran de choix d'image, et pour la même
        // raison : quarante-six entrées ne tiennent pas dans une boîte de hauteur fixe,
        // là où les quatre formats de disquette y tiennent à plat.
        if (_createHddIndex < _createHddTop)
            _createHddTop = _createHddIndex;
        else if (_createHddIndex >= _createHddTop + PickWindow)
            _createHddTop = _createHddIndex - PickWindow + 1;
    }

    /// <summary>
    /// DEVIATION: le libellé de PCem — « Type %02i : cylinders=%i, heads=%i, size=%iMB »
    ///   (wx-config.c:1581-1586), que HddImage.Label rend verbatim et que --create-hdd
    ///   imprime tel quel — fait jusqu'à 46 caractères. La boîte en a 46 EN TOUT
    ///   (Cols), marque et marge comprises, et rien ne coupe une ligne trop longue : le
    ///   type 46 déborderait. D'où cette forme courte, propre au menu.
    /// « * » marque les géométries que le Fixed Disk Adapter accepte. Six des quarante-six
    /// types, pas quatre : la table du BIOS a des doublons.
    /// </summary>
    private static string HddMenuLabel(int type)
    {
        (int cylinders, int heads) = HddImage.hd_types[type - 1];

        if (cylinders == 0 || heads == 0)
            return $"   Type {type:D2}   reserve, non creable";

        string mark = HddImage.XebecSwitch(cylinders, heads, HddImage.TypeSectorsPerTrack) >= 0 ? "*" : " ";
        long mb = HddImage.SizeOf(cylinders, heads, HddImage.TypeSectorsPerTrack) / (1024 * 1024);

        return $" {mark} Type {type:D2}  {cylinders,4} cyl x {heads,2} tetes  {mb,3} Mo";
    }

    /// <summary>
    /// pcem: wx-config.c:1645-1653 et :1682-1683, par HddImage.
    ///
    /// DEVIATION: l'image n'est PAS montée, là où CreateBlank insère la disquette dans
    ///   A:. La disquette peut l'être parce que DOS attend qu'on l'insère pendant qu'il
    ///   tourne ; un disque dur, non — la carte lit ide_fn[] au device_add de
    ///   resetpchard (pc.cs), et sa géométrie vient du fichier de configuration, qui
    ///   reste maître. PCem ne le monte pas non plus : il dit « remember to partition
    ///   and format the new drive », et c'est tout ce qu'il y a à faire.
    ///
    /// Les clés vont sur la CONSOLE et non dans la boîte : elles font cinq lignes, et
    /// _message en tient une, tronquée à 44 caractères.
    /// </summary>
    private void CreateBlankHdd(int type)
    {
        (int cylinders, int heads) = HddImage.hd_types[type - 1];
        int spt = HddImage.TypeSectorsPerTrack;

        if (!HddImage.Validate(cylinders, heads, spt, out string error))
        {
            _message = $"type {type:D2} refuse : {error}";
            return;
        }

        string root = ImagesRoot();

        try
        {
            // os/ est .gitignore'd, donc absent d'un clone neuf : on le crée au moment
            // d'écrire, pas avant. Sans effet s'il existe déjà.
            Directory.CreateDirectory(root);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _message = $"creation de {root} impossible : {ex.Message}";
            return;
        }

        string? path = FreeName(root, $"hdd-type{type:D2}");

        if (path is null)
        {
            _message = "trop d'images vierges de ce type dans os/.";
            return;
        }

        if (!HddImage.Create(path, cylinders, heads, spt, out string message))
        {
            _message = message;
            return;
        }

        Creations++;

        Console.WriteLine($"{path} : {HddImage.Label(type)}");
        Console.WriteLine(message);
        Console.WriteLine();
        Console.WriteLine("À ajouter au fichier de configuration, puis reset matériel :");
        Console.WriteLine();
        Console.Write(HddImage.ConfigBlock(HddImage.ConfigPath(path, root), cylinders, heads, spt));

        _message = $"{Path.GetFileName(path)} cree, cles en console";
    }

    private void HandleCreate(SDL.Scancode sc)
    {
        switch (sc)
        {
            case SDL.Scancode.Up:
                _createIndex = (_createIndex + DiscFormats.Length - 1) % DiscFormats.Length;
                break;

            case SDL.Scancode.Down:
                _createIndex = (_createIndex + 1) % DiscFormats.Length;
                break;

            case SDL.Scancode.Escape:
                _screen = Screen.Main;
                break;

            case SDL.Scancode.Return or SDL.Scancode.KpEnter:
                CreateBlank(DiscFormats[_createIndex]);
                _screen = Screen.Main;
                break;
        }
    }

    /// <summary>
    /// pcem: wx-createdisc.cc:62-73. Dix lignes utiles, et c'est TOUTE la création : un
    /// tampon de 512 octets à zéro, écrit nr_sectors fois. Pas de fseek, pas de secteur 0
    /// rustiné, pas de BPB, pas de FAT, pas de signature 0xAA55 — le fichier est
    /// strictement N x 512 octets nuls.
    ///
    /// L'octet est 0x00 et NON 0xF6 : le 0xF6 est celui de l'invité, que DOS passe en
    /// params[4] de la commande FORMAT TRACK du FDC (fdc.cs, disc_format -> STATE_FORMAT).
    /// Il n'apparaît dans l'image qu'une fois celle-ci formatée depuis la machine émulée.
    ///
    /// Conséquence garantie par construction : les cinq lectures de BPB d'img_load
    /// (disc_img.cs:223-232) rendent toutes 0, donc « bpb_sides &lt; 1 » est vrai et la garde
    /// de disc_img.cs:242 force la branche de devinette par TAILLE. La même garde rend
    /// inatteignable la division 0/0 de la branche BPB.
    /// </summary>
    private void CreateBlank((string Name, string Stem, int NrSectors) format)
    {
        string root = ImagesRoot();

        try
        {
            // os/ est .gitignore'd, donc absent d'un clone neuf : on le crée au moment
            // d'écrire, pas avant. Sans effet s'il existe déjà.
            Directory.CreateDirectory(root);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _message = $"creation de {root} impossible : {ex.Message}";
            return;
        }

        string? path = FreeName(root, format.Stem);

        if (path is null)
        {
            _message = "trop d'images vierges de cette taille dans os/.";
            return;
        }

        try
        {
            // Écriture RÉELLE, secteur par secteur, et pas SetLength : SetLength donnerait
            // un fichier sparse — même taille apparente, blocs non alloués. PCem écrit
            // nr_sectors fois 512 octets pour de vrai, et c'est ce qui rend l'image
            // comparable octet à octet à dd if=/dev/zero.
            using (FileStream f = new FileStream(path, FileMode.CreateNew, FileAccess.Write))
            {
                uint8_t[] sector = new uint8_t[512];

                for (int d = 0; d < format.NrSectors; d++)
                        f.Write(sector, 0, 512);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _message = $"ecriture de {Path.GetFileName(path)} impossible : {ex.Message}";
            return;
        }

        Creations++;

        // DEVIATION: PCem NE MONTE PAS l'image créée. Son nom de fichier est une variable
        //   locale de creatediscimage_dlgproc (wx-createdisc.cc:54), distincte du global
        //   qu'IDM_DISC_A consomme, et elle disparaît au retour : il faut rouvrir le menu
        //   et choisir le fichier. On l'insère ici dans A:, parce que c'est ce qui permet
        //   de créer la disquette PENDANT que DOS attend qu'on l'insère — « Insert new
        //   diskette for drive A: and strike any key when ready ». Insert() fait le
        //   disc_close + disc_load de PCem et vérifie drive_empty après coup.
        Insert(0, path);

        // Insert() a déjà posé son propre message ; on le remplace seulement s'il a réussi,
        // sinon c'est le sien — « refuse par disc_load » — qu'il faut garder.
        if (disc.drive_empty[0] == 0)
            _message = $"{Path.GetFileName(path)} cree ({format.NrSectors * 512 / 1024} Ko) et insere en A:";
    }

    /// <summary>
    /// Premier nom libre : vierge-360k.img, puis -2, -3… Jamais d'écrasement silencieux,
    /// qui est précisément le défaut d'une boîte « Enregistrer sous ». Null si la centaine
    /// est épuisée — improbable, mais une boucle sans borne ne se justifie pas.
    /// </summary>
    internal static string? FreeName(string root, string stem)
    {
        for (int n = 1; n <= 100; n++)
        {
            string name = n == 1 ? $"vierge-{stem}.img" : $"vierge-{stem}-{n}.img";
            string path = Path.Combine(root, name);

            if (!File.Exists(path))
                return path;
        }

        return null;
    }

    private void OpenPick(int drive)
    {
        _pickDrive = drive;
        _pickIndex = 0;
        _pickTop = 0;
        _message = "";
        _screen = Screen.Pick;

        // Relu à CHAQUE ouverture : une image déposée dans os/ pendant la session apparaît.
        ScanImages();
    }

    /// <summary>
    /// Liste _pickDir pour les quatre extensions chargeables. La racine est résolue par
    /// resolve_roms_path, qui donne d'abord sa chance au répertoire courant puis remonte
    /// depuis le binaire (paths.cs:132) — sous Rider le courant est bin/Debug/net10.0/.
    /// Un _pickDir disparu, ou hors d'une racine qui a changé, ramène à la racine.
    /// </summary>
    private void ScanImages()
    {
        _imagesRoot = ImagesRoot();

        if (!Directory.Exists(_imagesRoot))
        {
            _entries = [];
            _message = $"repertoire d'images introuvable : {_imagesRoot}";
            return;
        }

        if (!Directory.Exists(_pickDir) || !IsUnder(_pickDir, _imagesRoot))
            _pickDir = _imagesRoot;

        try
        {
            _entries = ListDirectory(_pickDir, !SamePath(_pickDir, _imagesRoot));

            if (_entries.Length == 0)
                _message = "aucune image dans os/ — utiliser Parcourir...";
        }
        catch (IOException ex)
        {
            _entries = [];
            _message = $"lecture de {_pickDir} impossible : {ex.Message}";
        }
        catch (UnauthorizedAccessException ex)
        {
            _entries = [];
            _message = $"lecture de {_pickDir} refusee : {ex.Message}";
        }
    }

    private void EnterDirectory(string dir)
    {
        _pickDir = dir;
        _pickIndex = 0;
        _pickTop = 0;
        _message = "";
        ScanImages();
    }

    /// <summary>
    /// Un niveau : « .. » si demandé, les sous-dossiers qui contiennent au moins une
    /// image (à n'importe quelle profondeur), puis les images. Même tri ordinal que
    /// FindImages. Lève ce que l'appelant doit dire lui-même.
    /// </summary>
    private static PickEntry[] ListDirectory(string dir, bool withParent)
    {
        List<PickEntry> entries = [];

        if (withParent)
            entries.Add(new PickEntry(Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(dir))!, "..", true));

        string[] dirs = Directory.GetDirectories(dir);
        Array.Sort(dirs, StringComparer.Ordinal);

        foreach (string sub in dirs)
        {
            if (FindImages(sub).Length != 0)
                entries.Add(new PickEntry(sub, Truncate(Path.GetFileName(sub) + "/", Cols - 4), true));
        }

        string[] files = Directory.GetFiles(dir);
        Array.Sort(files, StringComparer.Ordinal);

        foreach (string file in files)
        {
            if (IsImage(file))
                entries.Add(new PickEntry(file, Describe(file), false));
        }

        return entries.ToArray();
    }

    private static bool IsImage(string path)
    {
        foreach (string ext in Extensions)
        {
            if (path.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static bool SamePath(string a, string b) =>
        string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(a)),
                      Path.TrimEndingDirectorySeparator(Path.GetFullPath(b)), StringComparison.Ordinal);

    private static bool IsUnder(string path, string root)
    {
        string full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        string top = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));

        return full == top || full.StartsWith(top + Path.DirectorySeparatorChar, StringComparison.Ordinal);
    }

    /// <summary>
    /// pcem: wx-sdl2.c:749-752 — disc_close puis disc_load, sans reset. L'invité voit le
    /// changement par la ligne DSKCHG du port 0x3F7, que disc_load arme (disc.cs:145-147).
    /// </summary>
    private void Insert(int drive, string path)
    {
        // L'ordre importe : disc_close appelle img_close, donc FileStream.Close(), donc
        // le vidage des écritures d'img_writeback sur l'image SORTANTE. Charger sans
        // fermer les perdrait.
        disc.disc_close(drive);
        disc.disc_load(drive, path);

        // disc_load se tait quand il échoue : il pose drive_empty = 1 et discfns = ""
        // (disc.cs:153-155). Sans ce test d'après-coup, une image refusée — extension
        // inconnue, fichier illisible — est indiscernable d'une insertion réussie.
        if (disc.drive_empty[drive] != 0)
        {
            _message = $"refuse par disc_load : {Path.GetFileName(path)}";
            return;
        }

        Inserts++;
        _message = $"{DriveLetter(drive)}: {Path.GetFileName(path)} insere.";
    }

    /// <summary>pcem: wx-sdl2.c:759-761 — disc_close seul.</summary>
    private void Eject(int drive)
    {
        if (disc.drive_empty[drive] != 0)
        {
            _message = $"{DriveLetter(drive)}: est deja vide.";
            return;
        }

        disc.disc_close(drive);
        _message = $"{DriveLetter(drive)}: ejecte (ecritures videes sur le fichier).";
    }

    /// <summary>
    /// Sélecteur de fichier du système, pour aller chercher une image hors de os/.
    /// Asynchrone : SDL rend la main tout de suite et appelle _dialogCallback plus tard.
    /// Sous Linux il passe par un portail XDG (donc DBus), ce qui exige que la boucle
    /// d'évènements continue de tourner — elle le fait, l'émulation seule est en pause.
    /// </summary>
    private void Browse(DialogPurpose purpose)
    {
        _screen = Screen.Dialog;
        _dialogPurpose = purpose;
        _message = "";
        _dialogPath = null;
        _dialogError = null;
        Volatile.Write(ref _dialogDone, 0);

        // Chercher une SOURCE à déposer, c'est chercher n'importe quoi n'importe où sur
        // le disque hôte : ni le filtre d'images ni os/ n'ont de sens. C'est la seule
        // chose que ce sélecteur apporte au dépôt, et elle suffit à le justifier — taper
        // un chemin absolu dans une police 8x8 n'est pas une interface.
        bool source = purpose == DialogPurpose.PutSource;
        string start = source ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
                      : Directory.Exists(_pickDir) ? _pickDir
                      : Directory.Exists(_imagesRoot) ? _imagesRoot : AppContext.BaseDirectory;

        if (source)
            SDL.ShowOpenFileDialog(_dialogCallback, IntPtr.Zero, _window,
                                   AnyFileFilter, AnyFileFilter.Length, start, false);
        else
            SDL.ShowOpenFileDialog(_dialogCallback, IntPtr.Zero, _window,
                                   DialogFilters, DialogFilters.Length, start, false);
    }

    /// <summary>
    /// LE SEUL ENDROIT D'iXtal26 OÙ UN AUTRE FIL TOUCHE À L'ÉTAT DE L'HÔTE. SDL prévient
    /// que « the callback may be called from a different thread than the one the function
    /// was invoked on », et sous Linux le portail répond sur le fil DBus. Ce rappel ne
    /// fait donc RIEN d'autre que déposer un résultat et le publier : c'est
    /// CollectDialogResult, sur le fil principal, qui agit. Ne rien appeler d'autre ici.
    /// </summary>
    private void OnDialogResult(IntPtr userdata, IntPtr filelist, int filter)
    {
        // filelist == null est une ERREUR (pas de portail, pas de zenity) ; un pointeur
        // vers null est une annulation. Deux cas très différents, et seul le premier a
        // quelque chose à dire à l'utilisateur.
        if (filelist == IntPtr.Zero)
            _dialogError = SDL.GetError();
        else
            _dialogPath = System.Runtime.InteropServices.Marshal.PtrToStringUTF8(
                System.Runtime.InteropServices.Marshal.ReadIntPtr(filelist));

        Volatile.Write(ref _dialogDone, 1);
    }

    private void CollectDialogResult()
    {
        if (_screen != Screen.Dialog || Volatile.Read(ref _dialogDone) == 0)
            return;

        Volatile.Write(ref _dialogDone, 0);
        _screen = Screen.Main;

        if (_dialogError is not null)
        {
            // Sans cette ligne, une machine sans portail XDG donne un menu qui ne fait
            // rien quand on choisit « Parcourir... » — indiscernable d'un bogue.
            _message = $"selecteur indisponible : {_dialogError}";
            return;
        }

        if (_dialogPath is null)
        {
            _message = "choix de fichier annule.";
            return;
        }

        switch (_dialogPurpose)
        {
            case DialogPurpose.PutTarget:
                // Premier des deux temps : on tient l'image, il faut maintenant la
                // source. On REPART aussitôt en dialogue au lieu de revenir au menu —
                // _screen vient d'être remis à Main juste au-dessus, et Browse le
                // repose.
                _putImage = _dialogPath;
                Browse(DialogPurpose.PutSource);
                return;

            case DialogPurpose.PutSource:
                DoPut(_putImage, _dialogPath);
                return;

            default:
                Insert(_pickDrive, _dialogPath);
                return;
        }
    }

    /// <summary>
    /// Dépose un fichier de l'hôte dans une image, depuis le menu.
    ///
    /// Le refus d'une image MONTÉE vient de FatImage.Put et n'est pas rattrapé ici : le
    /// menu n'éjecte JAMAIS à la place de l'utilisateur. Éjecter, écrire dans le dos de
    /// DOS, réinsérer — et DOS réécrit la FAT qu'il garde de son côté, effaçant l'entrée
    /// qu'on vient de poser. « Ejecter A: » est deux lignes plus haut, à une touche.
    /// </summary>
    private void DoPut(string image, string source)
    {
        if (FatImage.Put(image, [source], out string message))
        {
            Puts++;
            _message = $"{Path.GetFileName(source)} -> {Path.GetFileName(image)}";
            Console.WriteLine(message);
            return;
        }

        // Tronqué à la largeur de la boîte : les messages de FatImage nomment la règle
        // violée et sont donc longs. Le détail complet part en console, comme les clés
        // de CreateBlankHdd.
        Console.WriteLine(message);
        _message = message.Length <= Cols - 2 ? message : message.Substring(0, Cols - 5) + "...";
    }

    /// <summary>
    /// Dessine la surimpression. Appelée par SdlHost.Render() APRÈS la texture du CGA et
    /// avant RenderPresent, donc sans jamais toucher au framebuffer émulé.
    /// </summary>
    internal void Render() => DrawBox(_renderer, BuildLines(out int selected), selected);

    /// <summary>
    /// Dessine une boîte de lignes ASCII centrée, une ligne en vidéo inverse. `internal
    /// static` depuis M13 : l'écran de construction (Host/SdlSetup.cs) s'en sert aussi, et
    /// deux implémentations du même cadre dériveraient — la largeur, l'échelle et le pas
    /// de ligne sont des mesures faites à l'écran, pas des choix qu'on refait.
    /// </summary>
    internal static void DrawBox(IntPtr renderer, string[] lines, int selected)
    {
        int rows = lines.Length;

        if (!SDL.GetRenderOutputSize(renderer, out int outW, out int outH))
            return;

        // Marges intérieures, en pixels non mis à l'échelle : sans elles la première et la
        // dernière ligne touchent le cadre.
        const int PadX = 4;
        const int PadY = 3;

        int boxWpx = (Cols * Cell) + (2 * PadX);
        int boxHpx = (rows * Row) + (2 * PadY);

        // Échelle entière : la police 8x8 reste nette, comme le pixel d'époque de la
        // texture. On prend la plus grande qui laisse 10 % de marge, bornée à 4 — au-delà
        // la boîte mange l'écran sans rien gagner en lisibilité.
        int scale = Math.Clamp(Math.Min(outW * 9 / (10 * boxWpx), outH * 9 / (10 * boxHpx)), 1, 4);

        SDL.SetRenderScale(renderer, scale, scale);

        // Coordonnées en unités mises à l'échelle à partir d'ici.
        float boxW = boxWpx;
        float boxH = boxHpx;
        float x0 = ((float)outW / scale - boxW) / 2f;
        float y0 = ((float)outH / scale - boxH) / 2f;

        SDL.SetRenderDrawBlendMode(renderer, SDL.BlendMode.Blend);

        var box = new SDL.FRect { X = x0, Y = y0, W = boxW, H = boxH };
        SDL.SetRenderDrawColor(renderer, 0, 0, 0, 220);
        SDL.RenderFillRect(renderer, in box);

        SDL.SetRenderDrawColor(renderer, 170, 170, 170, 255);
        SDL.RenderRect(renderer, in box);

        for (int i = 0; i < rows; i++)
        {
            float y = y0 + PadY + (i * Row);

            if (i == selected)
            {
                // Vidéo inverse pour la ligne choisie : un curseur « > » seul se perd
                // dans une liste de chemins.
                var row = new SDL.FRect { X = x0 + 1, Y = y - 1, W = boxW - 2, H = Row };
                SDL.SetRenderDrawColor(renderer, 170, 170, 170, 255);
                SDL.RenderFillRect(renderer, in row);
                SDL.SetRenderDrawColor(renderer, 0, 0, 0, 255);
            }
            else
            {
                SDL.SetRenderDrawColor(renderer, 200, 200, 200, 255);
            }

            SDL.RenderDebugText(renderer, x0 + PadX, y, lines[i]);
        }

        SDL.SetRenderDrawBlendMode(renderer, SDL.BlendMode.None);
        SDL.SetRenderScale(renderer, 1f, 1f);
    }

    /// <summary>
    /// Compose les lignes de l'écran courant et l'indice de celle qui est choisie (-1 si
    /// aucune). ASCII PUR, sans accent : SDL3-CS ne marshale pas l'UTF-8, et le tiret
    /// cadratin du titre de fenêtre ressortait « â€" » (voir SdlHost.WindowTitle).
    /// </summary>
    private string[] BuildLines(out int selected)
    {
        List<string> lines = [];
        selected = -1;

        switch (_screen)
        {
            case Screen.Main:
                lines.Add(" iXtal26 - menu                    Ctrl+F12");
                lines.Add("");
                lines.Add($" A: {DriveSummary(0)}");
                lines.Add($" B: {DriveSummary(1)}");
                lines.Add("");

                selected = lines.Count + _mainIndex;

                foreach ((string label, MainItem item) in MainItems)
                    lines.Add("   " + MainLabel(label, item));

                lines.Add("");
                lines.Add(" Fleches: choisir   Entree: valider");
                lines.Add(" Echap ou Ctrl+F12: fermer");
                break;

            case Screen.Pick:
                lines.Add(_pickDrive == PickPutTarget
                          ? " Image ou deposer (le fichier vient apres)"
                          : $" Disquette pour {DriveLetter(_pickDrive)}:");
                lines.Add(" " + Truncate(PickLocation(), Cols - 2));
                lines.Add("");

                int top = _pickTop;
                int end = Math.Min(_entries.Length, top + PickWindow);

                for (int i = top; i < end; i++)
                {
                    if (i == _pickIndex)
                        selected = lines.Count;

                    lines.Add("   " + _entries[i].Label);
                }

                // « Parcourir... » n'est visible que si la fenêtre atteint le bas de la
                // liste — sinon elle sauterait par-dessus les entrées non affichées.
                if (end >= _entries.Length)
                {
                    if (_pickIndex == _entries.Length)
                        selected = lines.Count;

                    lines.Add("   Parcourir...");
                }

                if (_entries.Length > PickWindow)
                    lines.Add($" ({_pickIndex + 1} sur {_entries.Length})");

                lines.Add("");
                lines.Add(" Entree: ouvrir   Retour: remonter");
                lines.Add(" Echap: annuler");
                break;

            case Screen.CreateHdd:
                lines.Add(" Creer un disque dur vierge dans os/");
                lines.Add("");

                int hddEnd = Math.Min(46, _createHddTop + PickWindow);

                for (int i = _createHddTop; i < hddEnd; i++)
                {
                    if (i == _createHddIndex)
                        selected = lines.Count;

                    lines.Add(HddMenuLabel(i + 1));
                }

                lines.Add($" ({_createHddIndex + 1} sur 46)");
                lines.Add("");

                // Le piege merite d'etre lu, pas decouvert : la carte n'accepte que
                // 17 secteurs et quatre couples (cylindres, tetes). Hors de la elle
                // n'emet qu'un avertissement, annonce le disque en type 0, et le POST
                // diverge. Rien ne le refuse.
                lines.Add(" * : accepte par le Fixed Disk Adapter ; les");
                lines.Add(" autres se creent, mais le POST diverge.");
                lines.Add(" Vierge = secteurs nuls : passer FDISK puis");
                lines.Add(" FORMAT C: /S. Cles a coller : voir console.");
                lines.Add("");
                lines.Add(" Echap: annuler");
                break;

            case Screen.Create:
                lines.Add(" Creer une disquette vierge dans os/");
                lines.Add("");

                for (int i = 0; i < DiscFormats.Length; i++)
                {
                    if (i == _createIndex)
                        selected = lines.Count;

                    lines.Add("   " + DiscFormats[i].Name);
                }

                lines.Add("");

                // Le piège mérite d'être lu, pas découvert : img_load fige la géométrie
                // sur la TAILLE du fichier, et img_writeback calcule ensuite ses offsets
                // dessus. Formater en 360 Ko une image de 160 Ko écrirait hors géométrie.
                lines.Add(" La taille choisie EST le format que l'image");
                lines.Add(" acceptera. Vierge = secteurs nuls : passer");
                lines.Add(" FORMAT sous DOS avant de s'en servir.");
                lines.Add("");
                lines.Add(" Echap: annuler");
                break;

            case Screen.CreateFat:
                lines.Add(" Creer une disquette FORMATEE dans os/");
                lines.Add("");

                for (int i = 0; i < FatImage.Formats.Length; i++)
                {
                    if (i == _createFatIndex)
                        selected = lines.Count;

                    lines.Add("   " + FatImage.Formats[i].Name);
                }

                lines.Add("");
                lines.Add(" FAT12 vide, prete a l'emploi : pas besoin");
                lines.Add(" de FORMAT. Elle n'est PAS inseree, parce");
                lines.Add(" qu'on ne depose pas sur une image montee.");
                lines.Add(" NON SYSTEME : faire SYS pour l'amorcer.");
                lines.Add("");
                lines.Add(" Echap: annuler");
                break;

            default:
                lines.Add(_dialogPurpose switch
                {
                    DialogPurpose.PutTarget => " Image ou deposer",
                    DialogPurpose.PutSource => $" Fichier a mettre dans {Path.GetFileName(_putImage)}",
                    _ => $" Disquette pour {DriveLetter(_pickDrive)}:",
                });
                lines.Add("");
                lines.Add(" Selecteur du systeme ouvert.");
                lines.Add(" Choisir un fichier dans sa fenetre.");
                lines.Add("");
                lines.Add(" Echap: renoncer au resultat");
                break;
        }

        if (_message.Length != 0)
        {
            lines.Add("");
            lines.Add(" " + Truncate(_message, Cols - 2));
        }

        return lines.ToArray();
    }

    /// <summary>Contenu d'un lecteur, tel que le cœur le voit : drive_empty est la vérité.</summary>
    private static string DriveSummary(int drive)
    {
        if (disc.drive_empty[drive] != 0)
            return "(vide)";

        string fn = fdd_c.discfns[drive];
        string mark = disc.writeprot[drive] != 0 ? "  L" : "  L/E";

        return Truncate(Path.GetFileName(fn), Cols - 8) + mark;
    }

    /// <summary>
    /// Les images d'un répertoire, récursivement, triées de façon stable. `internal
    /// static` depuis M13 : l'écran de construction liste la même chose, et deux
    /// balayages avec chacun sa liste d'extensions finiraient par ne pas montrer les
    /// mêmes fichiers. Lève ce que l'appelant doit dire lui-même.
    /// </summary>
    internal static string[] FindImages(string root)
    {
        List<string> found = [];

        foreach (string path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            if (IsImage(path))
                found.Add(path);
        }

        // Ordinal, donc stable d'une exécution à l'autre quelle que soit la culture.
        found.Sort(StringComparer.Ordinal);

        return found.ToArray();
    }

    internal static string Describe(string path)
    {
        string name;

        try
        {
            name = $"{Path.GetFileName(path)}  {new FileInfo(path).Length / 1024} Ko";
        }
        catch (IOException)
        {
            name = Path.GetFileName(path);
        }

        return Truncate(name, Cols - 4);
    }

    /// <summary>« os/ » suivi du chemin de _pickDir sous la racine.</summary>
    private string PickLocation()
    {
        if (!IsUnder(_pickDir, _imagesRoot))
            return "os/";

        string rel = Path.GetRelativePath(_imagesRoot, _pickDir);
        return rel == "." ? "os/" : "os/" + rel.Replace(Path.DirectorySeparatorChar, '/') + "/";
    }

        private static string DriveLetter(int drive) => drive == 0 ? "A" : "B";

    internal static string Truncate(string s, int max)
        => s.Length <= max ? s : string.Concat("...", s.AsSpan(s.Length - max + 3));

    /// <summary>
    /// Auto-contrôle des chemins clavier du menu. Même motif que SdlSetup.SelfCheck et
    /// que config-check : rien ici ne donne le focus à une fenêtre SDL, donc sans lui ces
    /// chemins ne s'exécuteraient jamais avant le jour où ils comptent.
    ///
    /// Il s'arrête NET devant le sélecteur de fichiers du système : Browse appelle
    /// SDL.ShowOpenFileDialog avec _window, qui vaut IntPtr.Zero ici. Les deux temps du
    /// dépôt — choisir l'image, puis la source — ne sont donc pas couverts, et c'est une
    /// limite à connaître plutôt qu'un contrôle à faire semblant d'écrire.
    /// </summary>
    /// <returns>0 si tout passe, 1 sinon.</returns>
    internal static int SelfCheck(string romsPath)
    {
        var m = new SdlMenu(IntPtr.Zero, IntPtr.Zero, romsPath);
        int fail = 0;

        void Check(string what, bool ok, string got)
        {
            Console.WriteLine($"  [{(ok ? "ok" : "ECHEC")}] {what} : {got}");

            if (!ok)
                    fail++;
        }

        // Positionne l'écran principal sur une entrée donnée sans toucher au clavier
        // émulé, puis valide. Open() appelle SdlKeyboard.Reset, qu'on ne veut pas ici.
        MenuAction Activate(MainItem item)
        {
            m._screen = Screen.Main;
            m._mainIndex = 0;

            for (int c = 0; c < MainItems.Length; c++)
            {
                if (MainItems[c].Item == item)
                        m._mainIndex = c;
            }

            return m.HandleMain(SDL.Scancode.Return);
        }

        Console.WriteLine("Auto-contrôle du menu Ctrl+F12.");
        Console.WriteLine();
        Console.WriteLine("Les deux entrées neuves mènent où il faut :");

        Activate(MainItem.CreateFat);
        Check("« Creer une disquette formatee » ouvre son ecran",
              m._screen == Screen.CreateFat, $"ecran {m._screen}");
        Check("le format prechoisi est le 360 Ko",
              m._createFatIndex == FatImage.Formats.Length - 1,
              FatImage.Formats[m._createFatIndex].Stem);

        string[] lines = m.BuildLines(out int sel);
        int listed = 0;

        for (int c = 0; c < lines.Length; c++)
        {
            for (int d = 0; d < FatImage.Formats.Length; d++)
            {
                if (lines[c].Contains(FatImage.Formats[d].Name, StringComparison.Ordinal))
                        listed++;
            }
        }

        Check("l'ecran liste les quatre formats, un choisi",
              listed == FatImage.Formats.Length && sel >= 0,
              $"{listed} format(s), ligne choisie {sel}, {lines.Length} lignes");
        Check("toutes les lignes tiennent dans la boite", AllFit(lines),
              $"la plus longue fait {Longest(lines)} sur {Cols}");

        m.HandleCreateFat(SDL.Scancode.Down);
        Check("Bas depuis le dernier format boucle sur le premier",
              m._createFatIndex == 0, FatImage.Formats[m._createFatIndex].Stem);
        m.HandleCreateFat(SDL.Scancode.Up);
        Check("Haut depuis le premier boucle sur le dernier",
              m._createFatIndex == FatImage.Formats.Length - 1,
              FatImage.Formats[m._createFatIndex].Stem);
        m.HandleCreateFat(SDL.Scancode.Up);
        Check("et Haut encore descend d'un cran, sans boucler",
              m._createFatIndex == FatImage.Formats.Length - 2,
              FatImage.Formats[m._createFatIndex].Stem);

        m.HandleCreateFat(SDL.Scancode.Escape);
        Check("Echap revient au menu SANS rien creer", m._screen == Screen.Main,
              $"ecran {m._screen}, {m.Creations} creation(s)");

        Console.WriteLine();
        Console.WriteLine("Le dépôt ouvre la liste en mode CIBLE, pas en mode lecteur :");

        Activate(MainItem.PutFile);
        Check("« Deposer un fichier » ouvre la liste d'images",
              m._screen == Screen.Pick, $"ecran {m._screen}");
        Check("la liste vise une cible de depot, aucun lecteur",
              m._pickDrive == PickPutTarget, $"_pickDrive = {m._pickDrive}");

        lines = m.BuildLines(out sel);
        Check("son titre ne parle pas de lecteur",
              lines.Length != 0 && !lines[0].Contains("Disquette pour", StringComparison.Ordinal),
              $"« {lines[0].Trim()} »");
        Check("toutes les lignes tiennent dans la boite", AllFit(lines),
              $"la plus longue fait {Longest(lines)} sur {Cols}");

        m.HandlePick(SDL.Scancode.Escape);
        Check("Echap referme la liste sans rien deposer",
              m._screen == Screen.Main && m.Puts == 0, $"ecran {m._screen}, {m.Puts} depot(s)");

        Console.WriteLine();
        Console.WriteLine("Les entrées d'origine n'ont pas bougé :");

        Activate(MainItem.InsertA);
        Check("« Inserer dans A: » vise bien le lecteur 0",
              m._screen == Screen.Pick && m._pickDrive == 0, $"_pickDrive = {m._pickDrive}");

        Console.WriteLine();
        Console.WriteLine("La liste suit l'arborescence de os/ :");

        m._pickDir = "";
        m.OpenPick(0);
        Check("elle s'ouvre a la racine, sans « .. »",
              SamePath(m._pickDir, m._imagesRoot) && (m._entries.Length == 0 || m._entries[0].Label != ".."),
              m.PickLocation());

        int firstDir = Array.FindIndex(m._entries, e => e.IsDirectory);

        if (firstDir < 0)
        {
            Console.WriteLine("  (aucun sous-dossier d'images dans os/ : navigation non exercee)");
        }
        else
        {
            string sub = m._entries[firstDir].Path;
            m._pickIndex = firstDir;
            m.HandlePick(SDL.Scancode.Return);
            Check("Entree sur un dossier l'ouvre, sans quitter l'ecran",
                  m._screen == Screen.Pick && SamePath(m._pickDir, sub), m.PickLocation());
            Check("le dossier commence par « .. »",
                  m._entries.Length != 0 && m._entries[0].Label == "..", $"{m._entries.Length} entree(s)");
            Check("aucune ligne hors de la boite", AllFit(m.BuildLines(out _)), m.PickLocation());

            m.HandlePick(SDL.Scancode.Backspace);
            Check("Retour remonte a la racine", SamePath(m._pickDir, m._imagesRoot), m.PickLocation());

            m.HandlePick(SDL.Scancode.Backspace);
            Check("et ne sort jamais de os/", SamePath(m._pickDir, m._imagesRoot), m.PickLocation());
        }

        m.HandlePick(SDL.Scancode.Escape);

        Check("« Quitter » rend toujours MenuAction.Quit",
              Activate(MainItem.Quit) == MenuAction.Quit, "Quit");
        Check("« Reset materiel » rend toujours MenuAction.HardReset",
              Activate(MainItem.HardReset) == MenuAction.HardReset, "HardReset");

        Console.WriteLine();
        Console.WriteLine("Le moniteur étale toute trame sur la même surface 4:3 :");

        (int W, int H)[] frames = [(640, 480), (800, 600), (1024, 768), (720, 400), (656, 416)];
        SDL.FRect first = SdlHost.ComputeRect(1310, 983, 640, 480, CrtMonitor.Generic15);

        foreach ((int fw, int fh) in frames)
        {
            SDL.FRect r = SdlHost.ComputeRect(1310, 983, fw, fh, CrtMonitor.Generic15);
            Check($"{fw}x{fh} sous un moniteur", r.W == first.W && r.H == first.H && r.W * 3 == r.H * 4,
                  $"{r.W}x{r.H} en ({r.X}, {r.Y})");
        }

        SDL.FRect filled = SdlHost.ComputeRect(1310, 983, 1024, 768, CrtMonitor.Generic15, 90);
        Check("a 90 % : 4:3, centre, 90 % de la largeur",
              filled.W * 3 == filled.H * 4 && Math.Abs(filled.W - first.W * 0.9f) <= 4 &&
              Math.Abs(filled.X * 2 + filled.W - 1310) <= 1 && Math.Abs(filled.Y * 2 + filled.H - 983) <= 1,
              $"{filled.W}x{filled.H} en ({filled.X}, {filled.Y})");
        SDL.FRect integer = SdlHost.ComputeRect(2400, 1350, 640, 480, CrtMonitor.Integer, 70);
        Check("sans effet en pixels entiers", integer.W == 1280 && integer.H == 960, $"{integer.W}x{integer.H}");

        foreach ((int fw, int fh) in frames)
        {
            SDL.FRect r = SdlHost.ComputeRect(2400, 1350, fw, fh, CrtMonitor.Integer);
            Check($"{fw}x{fh} en pixels entiers", r.W % fw == 0 && r.H % fh == 0 && r.W / fw == r.H / fh,
                  $"{r.W}x{r.H}, x{r.W / fw}");
        }

        Console.WriteLine();
        Console.WriteLine("Les deux entrées d'affichage :");

        CrtMonitor before = m._display.Monitor;
        Check("« Moniteur » fait defiler les tailles, menu ouvert",
              Activate(MainItem.Monitor) == MenuAction.DisplayChanged && m._display.Monitor != before,
              $"{before} -> {m._display.Monitor}");

        for (int c = 1; c < DisplaySettings.Cycle.Length; c++)
            m.HandleMain(SDL.Scancode.Right);
        Check("et revient au depart apres un tour complet", m._display.Monitor == before,
              m._display.Monitor.ToString());

        m.HandleMain(SDL.Scancode.Left);
        Check("Gauche recule d'un cran",
              m._display.Monitor == DisplaySettings.Cycle[(Array.IndexOf(DisplaySettings.Cycle, before) +
                                                           DisplaySettings.Cycle.Length - 1) %
                                                          DisplaySettings.Cycle.Length],
              m._display.Monitor.ToString());
        m._display.Monitor = before;

        Check("« Lignes CRT » bascule",
              Activate(MainItem.Scanlines) == MenuAction.DisplayChanged && m._display.Scanlines, "oui");
        Check("sans config, rien n'est ecrit et le menu le dit",
              m._message.Contains("pour cette session", StringComparison.Ordinal), m._message);

        int fill = m._display.FillPercent;
        m._screen = Screen.Main;
        m._mainIndex = Array.FindIndex(MainItems, e => e.Item == MainItem.Fill);
        Check("« Taille d'image » : Droite +1 %, menu ouvert",
              m.HandleMain(SDL.Scancode.Right) == MenuAction.DisplayChanged && m._display.FillPercent == fill + 1,
              $"{fill} -> {m._display.FillPercent} %");
        m._display.FillPercent = DisplaySettings.MaxFillPercent;
        m.HandleMain(SDL.Scancode.Right);
        Check("borne a 100 %", m._display.FillPercent == DisplaySettings.MaxFillPercent, $"{m._display.FillPercent} %");
        m.HandleMain(SDL.Scancode.Return);
        Check("Entree a 100 % reboucle sur 70 %", m._display.FillPercent == DisplaySettings.MinFillPercent,
              $"{m._display.FillPercent} %");
        m.HandleMain(SDL.Scancode.Left);
        Check("et ne descend pas sous 70 %", m._display.FillPercent == DisplaySettings.MinFillPercent,
              $"{m._display.FillPercent} %");
        m._display.FillPercent = fill;

        bool smooth = m._display.Smooth;
        Check("« Filtrage » bascule doux / net",
              Activate(MainItem.Filter) == MenuAction.DisplayChanged && m._display.Smooth != smooth,
              m._display.Smooth ? "doux" : "net");
        m._display.Smooth = smooth;

        m._display.ScanlinesTooFine = true;
        lines = m.BuildLines(out _);
        Check("les libelles les plus longs tiennent dans la boite", AllFit(lines),
              $"la plus longue fait {Longest(lines)} sur {Cols}");
        m._display.Scanlines = false;
        m._display.ScanlinesTooFine = false;

        Console.WriteLine();
        Console.WriteLine("Les surfaces et le pixel hôte se calculent :");

        (double w3v, double h3v) = DisplaySettings.VisibleMm(DisplaySettings.Nec3V);
        Check("NEC MultiSync 3V, 14\" visibles : 284,5 x 213,4 mm, en 4:3",
              Math.Abs(w3v - 284.5) < 0.05 && Math.Abs(h3v - 213.4) < 0.05 && Math.Abs(w3v * 3 - h3v * 4) < 1e-9,
              $"{w3v:0.0} x {h3v:0.0}");

        (double w15, double h15) = DisplaySettings.VisibleMm(
            DisplaySettings.Profile(CrtMonitor.Generic15, DisplaySettings.DefaultVisibleFraction));
        Check("generique 15\" a 0,93 : 283,5 x 212,6 mm",
              Math.Abs(w15 - 283.5) < 0.05 && Math.Abs(h15 - 212.6) < 0.05, $"{w15:0.0} x {h15:0.0}");

        Console.WriteLine();
        Console.WriteLine("Le 3V refuse ce qu'il ne sait pas synchroniser :");

        (string What, double KHz, double Hz, bool Ok)[] signals =
        [
            ("VGA 640x480", 31.47, 59.94, true),
            ("VGA texte 720x400", 31.47, 70.09, true),
            ("SVGA 1024x768 a 60 Hz", 48.36, 60.0, true),
            ("CGA", 15.70, 59.92, false),
            ("EGA 640x350", 21.85, 59.7, false),
            ("1280x1024 a 60 Hz", 64.0, 60.0, false),
        ];

        foreach ((string what, double kHz, double hz, bool ok) in signals)
            Check($"{what} ({kHz} kHz, {hz} Hz) {(ok ? "accepte" : "refuse")}",
                  DisplaySettings.Nec3V.Accepts(kHz, hz) == ok, ok ? "affiche" : "hors plage");

        Check("un generique accepte tout",
              DisplaySettings.Profile(CrtMonitor.Generic14, 0.93).Accepts(15.7, 59.9), "CGA affichee");
        Check("auto : le 3V derriere une VGA",
              DisplaySettings.Resolve(CrtMonitor.Auto, vgaClassCard: true) == CrtMonitor.Nec3V, "3V");
        Check("auto : un generique 14\" derriere la CGA",
              DisplaySettings.Resolve(CrtMonitor.Auto, vgaClassCard: false) == CrtMonitor.Generic14, "14\"");
        Check("les anciennes valeurs de la cle se relisent",
              DisplaySettings.TryParseMonitor("15", out CrtMonitor m15) && m15 == CrtMonitor.Generic15 &&
              DisplaySettings.TryParseMonitor("0", out CrtMonitor m0) && m0 == CrtMonitor.Integer,
              "15 -> generique 15, 0 -> entier");

        var dev = new DisplaySettings { HostDiagonalInches = 27 };
        double pitch = dev.ResolvePixelMm(2560, 1440);
        Check("27\" en 2560x1440 : 0,2335 mm (108,8 ppi)", Math.Abs(pitch - 0.2335) < 5e-5,
              $"{pitch:0.0000} mm, {25.4 / pitch:0.0} ppi");
        dev.PixelMm = 0.2331;
        Check("pixel_mm l'emporte sur la diagonale", dev.ResolvePixelMm(2560, 1440) == 0.2331,
              $"{dev.ResolvePixelMm(2560, 1440)}");
        Check("sans rien : le repli", new DisplaySettings().ResolvePixelMm(0, 0) == DisplaySettings.DefaultPixelMm,
              $"{DisplaySettings.DefaultPixelMm}");

        Console.WriteLine();
        Console.WriteLine("Les réglages se retrouvent d'une session à l'autre, dans configs/ seulement :");

        string scratch = Directory.CreateTempSubdirectory("ixtal26-display-").FullName;

        try
        {
            string saved = Path.Combine(scratch, "configs", "machine.cfg");
            string handWritten = Path.Combine(scratch, "ixtal26.cfg");
            Directory.CreateDirectory(Path.GetDirectoryName(saved)!);
            File.WriteAllText(saved, "model = ibmxt\n");
            File.WriteAllText(handWritten, "# documente a la main\nmodel = ibmxt\n");

            config.config_load(config.CFG_MACHINE, saved);
            var written = new DisplaySettings
            {
                ConfigPath = saved, Monitor = CrtMonitor.Generic17, Scanlines = true, PixelMm = 0.234,
                Smooth = false, HostDiagonalInches = 27, VisibleFraction = 0.9, FillPercent = 84,
            };
            written.Save();

            config.config_load(config.CFG_MACHINE, saved);
            var read = new DisplaySettings();
            read.Load();
            Check("configs/machine.cfg relu : memes valeurs",
                  read.Monitor == CrtMonitor.Generic17 && read.Scanlines && Math.Abs(read.PixelMm - 0.234) < 1e-4 &&
                  !read.Smooth && Math.Abs(read.HostDiagonalInches - 27) < 1e-4 &&
                  Math.Abs(read.VisibleFraction - 0.9) < 1e-4 && read.FillPercent == 84,
                  $"{read.Monitor}, crt {read.Scanlines}, {read.PixelMm:0.###} mm, " +
                  $"{(read.Smooth ? "doux" : "net")}, {read.HostDiagonalInches}\", {read.VisibleFraction:0.##}");
            Check("et la machine y est toujours",
                  config.config_get_string(config.CFG_MACHINE, null, "model", "") == "ibmxt", "model = ibmxt");

            string original = File.ReadAllText(handWritten);
            config.config_load(config.CFG_MACHINE, handWritten);
            string said = new DisplaySettings { ConfigPath = handWritten }.Save();
            Check("un .cfg hors de configs/ n'est pas reecrit",
                  File.ReadAllText(handWritten) == original, said);

            var cli = new DisplaySettings { ConfigPath = saved, MonitorOverride = CrtMonitor.Generic14 };
            config.config_load(config.CFG_MACHINE, saved);
            cli.Load();
            Check("--monitor l'emporte sur la config", cli.Monitor == CrtMonitor.Generic14 && cli.Scanlines,
                  $"{cli.Monitor}, crt {cli.Scanlines}");
        }
        finally
        {
            Directory.Delete(scratch, recursive: true);
        }

        Console.WriteLine();
        Console.WriteLine(fail == 0 ? "Vert : tous les contrôles passent."
                                    : $"{fail} contrôle(s) en échec.");

        return fail == 0 ? 0 : 1;
    }

    private static int Longest(string[] lines)
    {
        int n = 0;

        for (int c = 0; c < lines.Length; c++)
        {
            if (lines[c].Length > n)
                    n = lines[c].Length;
        }

        return n;
    }

    /// <summary>La boîte a une largeur FIXE de Cols caractères : une ligne plus longue
    /// est tronquée à l'écran, donc un texte qu'on croit avoir écrit et que personne ne
    /// lit jamais en entier.</summary>
    private static bool AllFit(string[] lines) => Longest(lines) <= Cols;
}
