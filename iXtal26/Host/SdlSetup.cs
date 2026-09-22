// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/wx-ui/wx-config_sel.c (Configuration Manager : lister les
//         machines de configs/, en charger une, en enregistrer une)
//         + wx-config.c:658-675 (choix de la machine), :742-753 (mémoire, bornée et
//         granulée par le modèle), :773-792 (types de lecteurs), :2038-2120 (assigner
//         une image de disque dur existante)
// STATUS: host
//
// CONSTRUIRE LA MACHINE AVANT DE LA LANCER.
//
// PCem fait cela, et pas dans la fenêtre d'émulation : `pc_main` tourne d'abord, puis
// wxWidgets démarre et `wx_load_config` ouvre le Configuration Manager AVANT
// `start_emulation` — sauf si `--config` a parlé, auquel cas `config_override` le saute
// (wx-sdl2.c:481-488). C'est exactement la règle retenue ici, options en place de
// `--config`.
//
// L'ÉCRAN NE MONTE RIEN, et c'est sa propriété centrale. Il remplit ce que
// `pc.loadconfig` remplit — le modèle par `pc.setmodel`, `cfg_mem_size`,
// `cfg_drive_type[]`, `cfg_hdd_controller`, `discfns[]`, `hdc[]`, `ide_fn[]` — puis rend
// la main. C'est `initpc` puis `resetpchard` qui montent, comme pour `--config`. Un seul
// chemin de montage, donc pas un second à maintenir en accord avec le premier.
//
// omitted: « Parcourir... », le sélecteur de fichiers du système. SdlMenu le porte pour
//   les disquettes (Browse/OnDialogResult/CollectDialogResult), et c'est du code à
//   rendez-vous entre deux fils : le recopier ici en ferait une seconde implémentation
//   d'un mécanisme délicat. L'écran liste os/, ce qui est la demande — « issu de ma
//   liste disponible » — et --floppy-a / --hdd prennent un chemin quelconque.
// omitted: tout ce que la boîte de PCem règle et que cette machine n'a pas — CPU, FPU,
//   dynarec, waitstates, carte vidéo, carte son, CD-ROM, ZIP, LPT, souris, joystick,
//   réseau (wx-config.c, pages 0 à 7). Le 5150 et le XT n'ont qu'un 8088 à 4,77 MHz,
//   une CGA et le haut-parleur : les proposer serait proposer des machines qui
//   n'existent pas dans ce dépôt.

using iXtal26.PluginApi;
using SDL3;

namespace iXtal26.Host;

internal sealed class SdlSetup
{
    /// <summary>Les lignes que l'écran principal propose, dans l'ordre d'affichage.</summary>
    private enum Item
    {
        Model, Memory, FloppyA, FloppyB, Controller, DiskC, DiskD, Load, Save, Start,
    }

    private enum Screen { Main, Pick, CreateHdd }

    /// <summary>
    /// Valeur sentinelle d'une entrée de liste qui n'est pas un chemin mais une action.
    /// Un caractère de contrôle en tête : aucun chemin de fichier ne commence par là,
    /// et la confusion serait silencieuse.
    /// </summary>
    private const string ActionCreateHdd = "\u0001creer";

    private const string NoneValue = "";

    private static readonly Item[] MainItems =
    [
        Item.Model, Item.Memory, Item.FloppyA, Item.FloppyB, Item.Controller,
        Item.DiskC, Item.DiskD, Item.Load, Item.Save, Item.Start,
    ];

    private readonly IntPtr _window;
    private readonly IntPtr _renderer;
    private readonly string _romsPath;

    private Screen _screen;
    private int _mainIndex;
    private string _message = "";

    private Item _pickItem;
    private string _pickTitle = "";
    private string[] _pickLabels = [];
    private string[] _pickValues = [];
    private int _pickIndex;
    private int _pickTop;

    private int _createIndex;
    private int _createTop;

    /// <summary>Quel lecteur reçoit l'image que l'écran de création va fabriquer.</summary>
    private int _createTarget;

    internal SdlSetup(IntPtr window, IntPtr renderer, string romsPath)
    {
        _window = window;
        _renderer = renderer;
        _romsPath = romsPath;
    }

    /// <summary>Vrai pour démarrer la machine, faux pour quitter sans rien monter.</summary>
    internal bool Run()
    {
        SDL.SetWindowTitle(_window, "iXtal26 - construire la machine");

        while (true)
        {
            while (SDL.PollEvent(out SDL.Event e))
            {
                // La croix de la fenêtre pendant la construction : on quitte, et le
                // processus sort par 0. Renoncer n'est pas échouer.
                if ((SDL.EventType)e.Type == SDL.EventType.Quit)
                    return false;

                if ((SDL.EventType)e.Type != SDL.EventType.KeyDown)
                    continue;

                if (Handle(e.Key.Scancode, out bool start))
                    return start;
            }

            SDL.SetRenderDrawColor(_renderer, 0, 0, 0, 255);
            SDL.RenderClear(_renderer);
            SdlMenu.DrawBox(_renderer, BuildLines(out int selected), selected);
            SDL.RenderPresent(_renderer);

            // 16 ms, comme la pause du menu quand la machine est en pause : on ne fait
            // tourner aucune émulation ici, inutile de brûler un cœur.
            SDL.Delay(16);
        }
    }

    /// <summary>Vrai quand l'écran a fini ; `start` dit s'il faut démarrer.</summary>
    private bool Handle(SDL.Scancode sc, out bool start)
    {
        start = false;

        switch (_screen)
        {
            case Screen.Pick:
                HandleList(sc, _pickLabels.Length, ref _pickIndex, ref _pickTop, ApplyPick);
                return false;

            case Screen.CreateHdd:
                HandleList(sc, 46, ref _createIndex, ref _createTop, ApplyCreate);
                return false;

            default:
                return HandleMain(sc, out start);
        }
    }

    private bool HandleMain(SDL.Scancode sc, out bool start)
    {
        start = false;

        switch (sc)
        {
            case SDL.Scancode.Up:
                _mainIndex = (_mainIndex + MainItems.Length - 1) % MainItems.Length;
                return false;

            case SDL.Scancode.Down:
                _mainIndex = (_mainIndex + 1) % MainItems.Length;
                return false;

            case SDL.Scancode.Escape:
                return true; // start reste faux : quitter

            case SDL.Scancode.Return or SDL.Scancode.KpEnter:
                if (MainItems[_mainIndex] == Item.Start)
                {
                    start = true;
                    return true;
                }

                Activate(MainItems[_mainIndex]);
                return false;

            default:
                return false;
        }
    }

    /// <summary>
    /// Navigation commune aux deux listes, fenêtre glissante comprise. Une seule copie :
    /// l'écran de choix d'image et celui des 46 types se comportaient déjà pareil dans
    /// SdlMenu, en deux exemplaires qui auraient pu diverger.
    /// </summary>
    private void HandleList(SDL.Scancode sc, int count, ref int index, ref int top, Action apply)
    {
        switch (sc)
        {
            case SDL.Scancode.Up:
                index = (index + count - 1) % count;
                break;

            case SDL.Scancode.Down:
                index = (index + 1) % count;
                break;

            case SDL.Scancode.Escape:
                _screen = Screen.Main;
                return;

            case SDL.Scancode.Return or SDL.Scancode.KpEnter:
                apply();
                return;

            default:
                return;
        }

        if (index < top)
            top = index;
        else if (index >= top + SdlMenu.PickWindow)
            top = index - SdlMenu.PickWindow + 1;
    }

    // --- ouverture des sous-écrans ------------------------------------------------

    private void Activate(Item item)
    {
        _message = "";
        _pickItem = item;
        _pickIndex = 0;
        _pickTop = 0;

        switch (item)
        {
            case Item.Model: BuildModelList(); break;
            case Item.Memory: BuildMemoryList(); break;
            case Item.FloppyA or Item.FloppyB: BuildImageList(item); break;
            case Item.Controller: BuildControllerList(); break;
            case Item.DiskC or Item.DiskD: BuildImageList(item); break;
            case Item.Load: BuildConfigList(); break;
            case Item.Save: SaveMachine(); return;
            default: return;
        }

        if (_pickLabels.Length == 0)
            return; // BuildXxx a posé le message ; on reste sur l'écran principal

        // RECALER LA FENÊTRE sur l'entrée courante. Sans cela, ouvrir la liste mémoire
        // d'un 5150 à 640 Ko plaçait le curseur en 19e position et affichait les dix
        // premières : aucune ligne n'était en vidéo inverse, et l'écran paraissait
        // n'avoir rien de sélectionné. Mesuré par la sonde, pas vu — la fenêtre ne se
        // pilote pas ici.
        if (_pickIndex >= SdlMenu.PickWindow)
            _pickTop = _pickIndex - SdlMenu.PickWindow + 1;

        _screen = Screen.Pick;
    }

    private void BuildModelList()
    {
        var models = Models.model_c.models;

        _pickTitle = " Machine";
        _pickLabels = new string[models.Length];
        _pickValues = new string[models.Length];

        for (int i = 0; i < models.Length; i++)
        {
            _pickLabels[i] = $"   {models[i].name}";
            _pickValues[i] = models[i].internal_name;

            if (i == Models.model_c.model)
                _pickIndex = i;
        }
    }

    /// <summary>
    /// pcem: wx-config.c:742-753 — la plage et le pas viennent du MODÈLE, pas d'une
    /// constante. C'est le piège du XT : 64 Ko de granularité contre 32 sur le 5150,
    /// donc « 96 » existe sur l'un et pas sur l'autre.
    /// </summary>
    private void BuildMemoryList()
    {
        var mdl = Models.model_c.models[Models.model_c.model];
        int n = ((mdl.max_ram - mdl.min_ram) / mdl.ram_granularity) + 1;

        _pickTitle = " Memoire";
        _pickLabels = new string[n];
        _pickValues = new string[n];

        for (int i = 0; i < n; i++)
        {
            int kb = mdl.min_ram + (i * mdl.ram_granularity);

            _pickLabels[i] = $"   {kb} Ko";
            _pickValues[i] = kb.ToString();

            if (kb == pc.cfg_mem_size)
                _pickIndex = i;
        }
    }

    private void BuildControllerList()
    {
        _pickTitle = " Controleur de disque dur";
        _pickLabels = ["   (aucun)", "   mfm_xebec  IBM Fixed Disk Adapter", "   dtc5150x   DTC 5150X"];
        _pickValues = [NoneValue, "mfm_xebec", "dtc5150x"];

        for (int i = 0; i < _pickValues.Length; i++)
            if (_pickValues[i] == pc.cfg_hdd_controller)
                _pickIndex = i;
    }

    /// <summary>
    /// Les images de os/. Pour un DISQUE DUR la liste est filtrée sur les tailles que la
    /// table du BIOS reconnaît : une disquette de 360 Ko proposée comme disque dur serait
    /// refusée plus tard, et la proposer est déjà une erreur.
    /// </summary>
    private void BuildImageList(Item item)
    {
        bool hdd = item is Item.DiskC or Item.DiskD;
        string root = SdlMenu.ImagesRoot(_romsPath);
        string[] images;

        try
        {
            images = Directory.Exists(root) ? SdlMenu.FindImages(root) : [];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _message = $"lecture de {root} impossible : {ex.Message}";
            _pickLabels = [];
            return;
        }

        List<string> labels = [];
        List<string> values = [];

        labels.Add(hdd ? "   (aucun)" : "   (vide)");
        values.Add(NoneValue);

        foreach (string path in images)
        {
            long size;

            try
            {
                size = new FileInfo(path).Length;
            }
            catch (IOException)
            {
                continue;
            }

            if (hdd)
            {
                var (_, _, _, type) = HddImage.GuessGeometry(size);

                if (type == 0)
                    continue;

                labels.Add($"   {SdlMenu.Truncate(Path.GetFileName(path), SdlMenu.Cols - 16)}  type {type:D2}");
            }
            else
            {
                labels.Add("   " + SdlMenu.Describe(path));
            }

            values.Add(path);
        }

        if (hdd)
        {
            labels.Add("   Creer un disque dur vierge...");
            values.Add(ActionCreateHdd);
        }

        _pickTitle = item switch
        {
            Item.FloppyA => " Lecteur A:",
            Item.FloppyB => " Lecteur B:",
            Item.DiskC => " Disque dur C:",
            _ => " Disque dur D:",
        };

        _pickLabels = labels.ToArray();
        _pickValues = values.ToArray();

        string current = item switch
        {
            Item.FloppyA => Floppy.fdd_c.discfns[0],
            Item.FloppyB => Floppy.fdd_c.discfns[1],
            Item.DiskC => Disc.hdd_c.ide_fn[0],
            _ => Disc.hdd_c.ide_fn[1],
        };

        for (int i = 0; i < _pickValues.Length; i++)
            if (_pickValues[i] == current)
                _pickIndex = i;
    }

    /// <summary>
    /// pcem: wx-config_sel.c:41-69 — la liste des machines enregistrées est un simple
    /// glob de `configs/*.cfg`. On y ajoute les `.cfg` de la racine, qui sont les
    /// exemples livrés avec le dépôt.
    /// </summary>
    private void BuildConfigList()
    {
        List<string> labels = [];
        List<string> values = [];

        foreach (string dir in new[] { ConfigsRoot(), RepoRoot() })
        {
            if (!Directory.Exists(dir))
                continue;

            string[] found;

            try
            {
                found = Directory.GetFiles(dir, "*.cfg");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            Array.Sort(found, StringComparer.Ordinal);

            foreach (string path in found)
            {
                if (values.Contains(path))
                    continue;

                labels.Add("   " + SdlMenu.Truncate(Path.GetFileName(path), SdlMenu.Cols - 4));
                values.Add(path);
            }
        }

        if (values.Count == 0)
        {
            _message = "aucune machine enregistree dans configs/ ni a la racine.";
            _pickLabels = [];
            return;
        }

        _pickTitle = " Charger une machine";
        _pickLabels = labels.ToArray();
        _pickValues = values.ToArray();
    }

    // --- application des choix ------------------------------------------------------

    private void ApplyPick()
    {
        string value = _pickValues[_pickIndex];

        switch (_pickItem)
        {
            case Item.Model:
                // setmodel refuse un nom inconnu en listant ce qui existe ; la liste
                // vient des modèles eux-mêmes, donc il ne peut pas refuser ici.
                pc.setmodel(value);

                // La mémoire courante peut ne plus être admise : le XT est à 64 Ko de
                // granularité, le 5150 à 32. On la ramène plutôt que de laisser une
                // machine que pc.check_mem_size refusera au démarrage.
                if (!pc.check_mem_size(pc.cfg_mem_size))
                {
                    var mdl = Models.model_c.models[Models.model_c.model];
                    int kb = mdl.min_ram +
                             (((pc.cfg_mem_size - mdl.min_ram) / mdl.ram_granularity) *
                              mdl.ram_granularity);

                    pc.cfg_mem_size = Math.Clamp(kb, mdl.min_ram, mdl.max_ram);
                    _message = $"memoire ramenee a {pc.cfg_mem_size} Ko pour cette machine.";
                }
                break;

            case Item.Memory:
                pc.cfg_mem_size = int.Parse(value);
                break;

            case Item.FloppyA:
                Floppy.fdd_c.discfns[0] = value;
                break;

            case Item.FloppyB:
                Floppy.fdd_c.discfns[1] = value;
                break;

            case Item.Controller:
                pc.cfg_hdd_controller = value;
                break;

            case Item.DiskC or Item.DiskD:
                if (value == ActionCreateHdd)
                {
                    _createTarget = _pickItem == Item.DiskC ? 0 : 1;
                    _createIndex = 0;
                    _createTop = 0;
                    _screen = Screen.CreateHdd;
                    return;
                }

                AssignHdd(_pickItem == Item.DiskC ? 0 : 1, value);
                break;

            case Item.Load:
                LoadMachine(value);
                break;
        }

        _screen = Screen.Main;
    }

    /// <summary>
    /// Pose une image de disque dur et SA géométrie. Les deux vont ensemble : hdd_load_ext
    /// prend spt/hpc/tracks de la configuration, pas du fichier, donc une image sans sa
    /// géométrie serait lue avec celle du disque précédent.
    /// </summary>
    private void AssignHdd(int drive, string path)
    {
        Disc.hdd_c.ide_fn[drive] = path;

        if (path.Length == 0)
        {
            Disc.hdd_c.hdc[drive].spt = 0;
            Disc.hdd_c.hdc[drive].hpc = 0;
            Disc.hdd_c.hdc[drive].tracks = 0;
            return;
        }

        long size;

        try
        {
            size = new FileInfo(path).Length;
        }
        catch (IOException ex)
        {
            _message = $"image illisible : {ex.Message}";
            return;
        }

        var (cylinders, heads, spt, type) = HddImage.GuessGeometry(size);

        Disc.hdd_c.hdc[drive].spt = spt;
        Disc.hdd_c.hdc[drive].hpc = heads;
        Disc.hdd_c.hdc[drive].tracks = cylinders;

        // Une carte est posée si aucune n'est choisie : une image montée sans contrôleur
        // n'est vue par personne, et rien ne le dirait.
        if (pc.cfg_hdd_controller.Length == 0)
            pc.cfg_hdd_controller = "mfm_xebec";

        // Le cas indécidable de § M13 : 21 307 392 octets, c'est le type 13 (306 x 8) ou
        // le type 16 (612 x 4), et le Fixed Disk Adapter accepte les deux.
        HddImage.TypesWithSize(size, out bool ambiguous);

        if (ambiguous)
            _message = $"taille ambigue : type {type:D2} retenu. --hdd-type impose l'autre.";
    }

    private void ApplyCreate()
    {
        int type = _createIndex + 1;
        var (cylinders, heads) = HddImage.hd_types[type - 1];

        if (!HddImage.Validate(cylinders, heads, HddImage.TypeSectorsPerTrack, out string error))
        {
            _message = $"type {type:D2} : {error}";
            return;
        }

        string root = SdlMenu.ImagesRoot(_romsPath);

        try
        {
            Directory.CreateDirectory(root);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _message = $"creation de {root} impossible : {ex.Message}";
            return;
        }

        string? path = SdlMenu.FreeName(root, $"hdd-type{type:D2}");

        if (path is null)
        {
            _message = "trop d'images vierges de ce type dans os/.";
            return;
        }

        if (!HddImage.Create(path, cylinders, heads, HddImage.TypeSectorsPerTrack, out string msg))
        {
            _message = msg;
            return;
        }

        // ASSIGNÉE dans la foulée, là où le menu Ctrl+F12 ne le fait pas : sur une machine
        // en marche la carte a déjà lu ide_fn[] et il faudrait un reset ; ici rien ne
        // tourne encore, et créer un disque pour ne pas s'en servir n'aurait pas de sens.
        AssignHdd(_createTarget, path);

        _message = $"{Path.GetFileName(path)} cree et affecte.";
        _screen = Screen.Main;
    }

    /// <summary>
    /// pcem: wx-config_sel.c:161-181 — charger puis laisser la boîte montrer le résultat.
    ///
    /// discfns[] est vidé AVANT : loadconfig saute la clé `disc_a` quand le lecteur est
    /// déjà rempli (précédence de la ligne de commande, pc.cs). Sans ce vidage, charger
    /// une machine après en avoir composé une laisserait la disquette de la première.
    /// </summary>
    private void LoadMachine(string path)
    {
        Floppy.fdd_c.discfns[0] = "";
        Floppy.fdd_c.discfns[1] = "";

        if (!pc.loadconfig(path))
        {
            _message = $"{Path.GetFileName(path)} refuse — voir la console.";
            return;
        }

        _message = $"{Path.GetFileName(path)} charge.";
    }

    /// <summary>
    /// pcem: wx-config_sel.c:125-160 et pc.c:867-922 (saveconfig, partie machine).
    ///
    /// TOUJOURS dans configs/, jamais à la racine, et c'est délibéré : config_save
    /// réécrit le fichier depuis l'arbre en mémoire, et cet arbre n'a pas de commentaires
    /// — le parseur les saute et entry_t n'a pas de champ pour eux. Enregistrer par-dessus
    /// ixtal26.cfg ou ixtal26-xt.cfg les amputerait de toute leur documentation.
    ///
    /// DEVIATION: le nom n'est pas saisi, il est proposé — machine.cfg, machine-2.cfg…
    ///   PCem le demande dans une boîte de texte (wx-config_sel.c:130) ; ce menu n'a pas
    ///   d'éditeur de texte, et en ajouter un pour cela seul serait cher. Renommer le
    ///   fichier hors de l'émulateur revient au même.
    /// </summary>
    private void SaveMachine()
    {
        string root = ConfigsRoot();

        try
        {
            Directory.CreateDirectory(root);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _message = $"creation de {root} impossible : {ex.Message}";
            return;
        }

        string? path = FreeConfigName(root);

        if (path is null)
        {
            _message = "trop de machines enregistrees dans configs/.";
            return;
        }

        string imagesRoot = SdlMenu.ImagesRoot(_romsPath);

        config.config_set_string(config.CFG_MACHINE, null, "model",
                                 Models.model_c.model_get_internal_name());
        config.config_set_int(config.CFG_MACHINE, null, "mem_size", pc.cfg_mem_size);
        config.config_set_int(config.CFG_MACHINE, null, "drive_a_type", pc.cfg_drive_type[0]);
        config.config_set_int(config.CFG_MACHINE, null, "drive_b_type", pc.cfg_drive_type[1]);

        // Chemins RELATIFS quand l'image est dans os/, par le même helper que
        // --create-hdd : une machine enregistrée depuis bin/Release/net10.0 porterait
        // sinon des chemins absolus propres à cette installation (§ M12.1).
        config.config_set_string(config.CFG_MACHINE, null, "disc_a",
                                 HddImage.ConfigPath(Floppy.fdd_c.discfns[0], imagesRoot));
        config.config_set_string(config.CFG_MACHINE, null, "disc_b",
                                 HddImage.ConfigPath(Floppy.fdd_c.discfns[1], imagesRoot));

        config.config_set_string(config.CFG_MACHINE, null, "hdd_controller", pc.cfg_hdd_controller);

        for (int d = 0; d < 2; d++)
        {
            string pfx = d == 0 ? "hdc" : "hdd";

            config.config_set_int(config.CFG_MACHINE, null, $"{pfx}_sectors", Disc.hdd_c.hdc[d].spt);
            config.config_set_int(config.CFG_MACHINE, null, $"{pfx}_heads", Disc.hdd_c.hdc[d].hpc);
            config.config_set_int(config.CFG_MACHINE, null, $"{pfx}_cylinders", Disc.hdd_c.hdc[d].tracks);
            config.config_set_string(config.CFG_MACHINE, null, $"{pfx}_fn",
                                     HddImage.ConfigPath(Disc.hdd_c.ide_fn[d], imagesRoot));
        }

        config.config_set_int(config.CFG_MACHINE, null, "bpb_disable", Disc.disc_img.bpb_disable);

        config.config_save(config.CFG_MACHINE, path);

        _message = $"enregistree : configs/{Path.GetFileName(path)}";
    }

    // --- chemins ---------------------------------------------------------------------

    /// <summary>
    /// pcem: paths.c:206-207 — `configs_path` = `<pcem_path>/configs/`. Frère de os/ et
    /// de roms/, et trouvé par le même repli : resolve_roms_path rend le chemin tel quel
    /// quand le répertoire n'existe pas encore, et « configs » relatif vaudrait alors
    /// bin/Release/net10.0/configs sous Rider.
    /// </summary>
    private string ConfigsRoot()
    {
        string root = paths.resolve_roms_path("configs");

        if (Directory.Exists(root))
            return root;

        return Path.Combine(RepoRoot(), "configs");
    }

    /// <summary>
    /// Le frère de roms/, c'est-à-dire la racine du dépôt.
    ///
    /// La chaîne VIDE compte autant que null, et c'est ce qui manquait : lancé depuis la
    /// racine, resolve_roms_path rend « roms » tel quel, dont GetDirectoryName rend « »
    /// — Directory.GetFiles("") lève, l'exception était avalée, et la liste des machines
    /// enregistrées sortait vide alors qu'ixtal26.cfg et ixtal26-xt.cfg sont là.
    /// </summary>
    private string RepoRoot()
    {
        string? parent = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(_romsPath));

        return string.IsNullOrEmpty(parent) ? "." : parent;
    }

    /// <summary>
    /// Premier nom libre : machine.cfg, puis -2, -3… Même politique que SdlMenu.FreeName,
    /// qui ne sert pas ici parce qu'elle fige le préfixe « vierge- » et l'extension .img.
    /// </summary>
    private static string? FreeConfigName(string root)
    {
        for (int n = 1; n <= 100; n++)
        {
            string name = n == 1 ? "machine.cfg" : $"machine-{n}.cfg";
            string path = Path.Combine(root, name);

            if (!File.Exists(path))
                return path;
        }

        return null;
    }

    // --- rendu --------------------------------------------------------------------

    private string[] BuildLines(out int selected)
    {
        List<string> lines = [];

        selected = -1;

        switch (_screen)
        {
            case Screen.Pick:
                lines.Add(_pickTitle);
                lines.Add("");

                int end = Math.Min(_pickLabels.Length, _pickTop + SdlMenu.PickWindow);

                for (int i = _pickTop; i < end; i++)
                {
                    if (i == _pickIndex)
                        selected = lines.Count;

                    lines.Add(_pickLabels[i]);
                }

                if (_pickLabels.Length > SdlMenu.PickWindow)
                    lines.Add($" ({_pickIndex + 1} sur {_pickLabels.Length})");

                lines.Add("");
                lines.Add(" Echap: annuler");
                break;

            case Screen.CreateHdd:
                lines.Add($" Creer un disque dur pour {(_createTarget == 0 ? "C:" : "D:")}");
                lines.Add("");

                int hddEnd = Math.Min(46, _createTop + SdlMenu.PickWindow);

                for (int i = _createTop; i < hddEnd; i++)
                {
                    if (i == _createIndex)
                        selected = lines.Count;

                    lines.Add(HddMenuLabel(i + 1));
                }

                lines.Add($" ({_createIndex + 1} sur 46)");
                lines.Add("");
                lines.Add(" * : accepte par le Fixed Disk Adapter ; les");
                lines.Add(" autres se creent, mais le POST diverge.");
                lines.Add(" Echap: annuler");
                break;

            default:
                lines.Add(" Construire la machine");
                lines.Add("");

                for (int i = 0; i < MainItems.Length; i++)
                {
                    if (MainItems[i] == Item.Load || MainItems[i] == Item.Start)
                        lines.Add("");

                    if (i == _mainIndex)
                        selected = lines.Count;

                    lines.Add(MainLine(MainItems[i]));
                }

                lines.Add("");
                lines.Add(" Fleches: choisir   Entree: modifier");
                lines.Add(" Echap: quitter sans demarrer");
                break;
        }

        if (_message.Length != 0)
        {
            lines.Add("");
            lines.Add(" " + SdlMenu.Truncate(_message, SdlMenu.Cols - 2));
        }

        return lines.ToArray();
    }

    private static string MainLine(Item item)
    {
        switch (item)
        {
            case Item.Model:
                return Field("Machine", Models.model_c.models[Models.model_c.model].name);

            case Item.Memory:
                return Field("Memoire", $"{pc.cfg_mem_size} Ko");

            case Item.FloppyA:
                return Field("Lecteur A:", ShortName(Floppy.fdd_c.discfns[0], "(vide)"));

            case Item.FloppyB:
                return Field("Lecteur B:", ShortName(Floppy.fdd_c.discfns[1], "(vide)"));

            case Item.Controller:
                return Field("Controleur", pc.cfg_hdd_controller.Length == 0
                                            ? "(aucun)" : pc.cfg_hdd_controller);

            case Item.DiskC:
                return Field("Disque C:", DiskSummary(0));

            case Item.DiskD:
                return Field("Disque D:", DiskSummary(1));

            case Item.Load:
                return "   Charger une machine enregistree...";

            case Item.Save:
                return "   Enregistrer cette machine...";

            default:
                return "   Demarrer";
        }
    }

    private static string Field(string label, string value)
        => "   " + label.PadRight(11) + ": " + SdlMenu.Truncate(value, SdlMenu.Cols - 18);

    private static string ShortName(string path, string empty)
        => path.Length == 0 ? empty : Path.GetFileName(path);

    private static string DiskSummary(int drive)
    {
        if (Disc.hdd_c.ide_fn[drive].Length == 0)
            return "(aucun)";

        int type = HddImage.TypeFor(Disc.hdd_c.hdc[drive].tracks, Disc.hdd_c.hdc[drive].hpc,
                                    Disc.hdd_c.hdc[drive].spt);

        return Path.GetFileName(Disc.hdd_c.ide_fn[drive]) + (type == 0 ? "" : $"  type {type:D2}");
    }

    /// <summary>Même forme courte qu'au menu Ctrl+F12, et pour la même raison : le
    /// libellé verbatim de PCem fait jusqu'à 46 caractères, la boîte en a 46 en tout.</summary>
    private static string HddMenuLabel(int type)
    {
        (int cylinders, int heads) = HddImage.hd_types[type - 1];

        if (cylinders == 0 || heads == 0)
            return $"   Type {type:D2}   reserve, non creable";

        string mark = HddImage.XebecSwitch(cylinders, heads, HddImage.TypeSectorsPerTrack) >= 0 ? "*" : " ";
        long mb = HddImage.SizeOf(cylinders, heads, HddImage.TypeSectorsPerTrack) / (1024 * 1024);

        return $" {mark} Type {type:D2}  {cylinders,4} cyl x {heads,2} tetes  {mb,3} Mo";
    }
}
