// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/wx-ui/wx-sdl2.c:725-770 (IDM_DISC_A/B, IDM_EJECT_A/B,
//         IDM_FILE_HRESET, IDM_FILE_RESET_CAD, IDM_FILE_EXIT)
//         + wx-createdisc.cc:22-29, 62-73 (IDM_DISC_CREATE)
// STATUS: host

using iXtal26.Disc;
using iXtal26.Floppy;
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
    private const int Cols = 46;

    /// <summary>Entrées d'image visibles à la fois dans l'écran de choix.</summary>
    private const int PickWindow = 10;

    private enum Screen { Main, Pick, Create, Dialog }

    // Les entrées de l'écran principal, dans l'ordre d'affichage. L'index sélectionné
    // indexe ce tableau : pas de correspondance à maintenir entre le texte et l'action.
    private static readonly (string Label, MainItem Item)[] MainItems =
    [
        ("Inserer une disquette dans A:", MainItem.InsertA),
        ("Inserer une disquette dans B:", MainItem.InsertB),
        ("Ejecter A:", MainItem.EjectA),
        ("Ejecter B:", MainItem.EjectB),
        ("Creer une disquette vierge...", MainItem.CreateBlank),
        ("Reset materiel (temps reel)", MainItem.HardReset),
        ("Reset materiel + turbo", MainItem.HardResetTurbo),
        ("Ctrl+Alt+Suppr (redemarrage a chaud)", MainItem.Cad),
        ("Quitter", MainItem.Quit),
    ];

    private enum MainItem { InsertA, InsertB, EjectA, EjectB, CreateBlank, HardReset, HardResetTurbo, Cad, Quit }

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

    /// <summary>Lecteur visé par l'écran de choix : 0 = A:, 1 = B:.</summary>
    private int _pickDrive;

    private string[] _images = [];
    private string _imagesRoot = "";

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

    internal SdlMenu(IntPtr window, IntPtr renderer, string romsPath)
    {
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
    private string ImagesRoot()
    {
        string root = PluginApi.paths.resolve_roms_path("os");

        if (Directory.Exists(root))
            return root;

        string? parent = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(_romsPath));

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

            case SDL.Scancode.Return or SDL.Scancode.KpEnter:
                break;

            default:
                return MenuAction.None;
        }

        switch (MainItems[_mainIndex].Item)
        {
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

    private void HandlePick(SDL.Scancode sc)
    {
        // Une entrée de plus que la liste : la dernière est « Parcourir... ».
        int count = _images.Length + 1;

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

            case SDL.Scancode.Return or SDL.Scancode.KpEnter:
                if (_pickIndex < _images.Length)
                {
                    Insert(_pickDrive, _images[_pickIndex]);
                    _screen = Screen.Main;
                }
                else
                {
                    Browse();
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
    private static string? FreeName(string root, string stem)
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
    /// Balaie os/ pour les quatre extensions chargeables. La racine est résolue par
    /// resolve_roms_path, qui donne d'abord sa chance au répertoire courant puis remonte
    /// depuis le binaire (paths.cs:132) — sous Rider le courant est bin/Debug/net10.0/.
    /// </summary>
    private void ScanImages()
    {
        _imagesRoot = ImagesRoot();

        if (!Directory.Exists(_imagesRoot))
        {
            _images = [];
            _message = $"repertoire d'images introuvable : {_imagesRoot}";
            return;
        }

        try
        {
            List<string> found = [];

            foreach (string path in Directory.EnumerateFiles(_imagesRoot, "*", SearchOption.AllDirectories))
            {
                foreach (string ext in Extensions)
                {
                    if (path.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                    {
                        found.Add(path);
                        break;
                    }
                }
            }

            // Ordinal, donc stable d'une exécution à l'autre quelle que soit la culture.
            found.Sort(StringComparer.Ordinal);
            _images = found.ToArray();

            if (_images.Length == 0)
                _message = "aucune image dans os/ — utiliser Parcourir...";
        }
        catch (IOException ex)
        {
            _images = [];
            _message = $"lecture de os/ impossible : {ex.Message}";
        }
        catch (UnauthorizedAccessException ex)
        {
            _images = [];
            _message = $"lecture de os/ refusee : {ex.Message}";
        }
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
    private void Browse()
    {
        _screen = Screen.Dialog;
        _message = "";
        _dialogPath = null;
        _dialogError = null;
        Volatile.Write(ref _dialogDone, 0);

        string start = Directory.Exists(_imagesRoot) ? _imagesRoot : AppContext.BaseDirectory;

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

        Insert(_pickDrive, _dialogPath);
    }

    /// <summary>
    /// Dessine la surimpression. Appelée par SdlHost.Render() APRÈS la texture du CGA et
    /// avant RenderPresent, donc sans jamais toucher au framebuffer émulé.
    /// </summary>
    internal void Render()
    {
        string[] lines = BuildLines(out int selected);

        int rows = lines.Length;

        if (!SDL.GetRenderOutputSize(_renderer, out int outW, out int outH))
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

        SDL.SetRenderScale(_renderer, scale, scale);

        // Coordonnées en unités mises à l'échelle à partir d'ici.
        float boxW = boxWpx;
        float boxH = boxHpx;
        float x0 = ((float)outW / scale - boxW) / 2f;
        float y0 = ((float)outH / scale - boxH) / 2f;

        SDL.SetRenderDrawBlendMode(_renderer, SDL.BlendMode.Blend);

        var box = new SDL.FRect { X = x0, Y = y0, W = boxW, H = boxH };
        SDL.SetRenderDrawColor(_renderer, 0, 0, 0, 220);
        SDL.RenderFillRect(_renderer, in box);

        SDL.SetRenderDrawColor(_renderer, 170, 170, 170, 255);
        SDL.RenderRect(_renderer, in box);

        for (int i = 0; i < rows; i++)
        {
            float y = y0 + PadY + (i * Row);

            if (i == selected)
            {
                // Vidéo inverse pour la ligne choisie : un curseur « > » seul se perd
                // dans une liste de chemins.
                var row = new SDL.FRect { X = x0 + 1, Y = y - 1, W = boxW - 2, H = Row };
                SDL.SetRenderDrawColor(_renderer, 170, 170, 170, 255);
                SDL.RenderFillRect(_renderer, in row);
                SDL.SetRenderDrawColor(_renderer, 0, 0, 0, 255);
            }
            else
            {
                SDL.SetRenderDrawColor(_renderer, 200, 200, 200, 255);
            }

            SDL.RenderDebugText(_renderer, x0 + PadX, y, lines[i]);
        }

        SDL.SetRenderDrawBlendMode(_renderer, SDL.BlendMode.None);
        SDL.SetRenderScale(_renderer, 1f, 1f);
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

                foreach ((string label, _) in MainItems)
                    lines.Add("   " + label);

                lines.Add("");
                lines.Add(" Fleches: choisir   Entree: valider");
                lines.Add(" Echap ou Ctrl+F12: fermer");
                break;

            case Screen.Pick:
                lines.Add($" Disquette pour {DriveLetter(_pickDrive)}:");
                lines.Add("");

                int top = _pickTop;
                int end = Math.Min(_images.Length, top + PickWindow);

                for (int i = top; i < end; i++)
                {
                    if (i == _pickIndex)
                        selected = lines.Count;

                    lines.Add("   " + Describe(_images[i]));
                }

                // « Parcourir... » n'est visible que si la fenêtre atteint le bas de la
                // liste — sinon elle sauterait par-dessus les entrées non affichées.
                if (end >= _images.Length)
                {
                    if (_pickIndex == _images.Length)
                        selected = lines.Count;

                    lines.Add("   Parcourir...");
                }

                if (_images.Length > PickWindow)
                    lines.Add($" ({_pickIndex + 1} sur {_images.Length})");

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

            default:
                lines.Add($" Disquette pour {DriveLetter(_pickDrive)}:");
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

    private static string Describe(string path)
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

    private static string DriveLetter(int drive) => drive == 0 ? "A" : "B";

    private static string Truncate(string s, int max)
        => s.Length <= max ? s : string.Concat("...", s.AsSpan(s.Length - max + 3));
}
