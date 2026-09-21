// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: wx-sdl2.c, wx-sdl2-video.c
// STATUS: host

using System.Diagnostics;
using System.Runtime.InteropServices;
using iXtal26.Video;
using SDL3;

namespace iXtal26.Host;

/// <summary>
/// Relie le cœur d'émulation à une fenêtre SDL3 : cadençage sur horloge murale,
/// consommation du blit du CGA, routage des évènements clavier.
/// </summary>
public sealed class SdlHost : IDisposable
{
    /// <summary>
    /// Le titre NOMME LA MACHINE. Il portait « IBM PC 5150 » en dur, ce qui, depuis que
    /// le dépôt en a deux (§ M10), faisait afficher « 5150 » à un XT qui tournait — le
    /// seul endroit où l'utilisateur regarde, et il mentait.
    ///
    /// Une méthode et non un champ : un initialiseur de champ s'évaluerait AVANT
    /// pc.initpc(), donc avant qu'un modèle soit choisi. Les quatre sites d'appel sont
    /// tous postérieurs.
    ///
    /// Le nom est celui de la table, verbatim de PCem — « [8088] IBM PC », « [8088] IBM
    /// XT ». Pas une étiquette maison : celle-ci se maintient toute seule, et une
    /// troisième machine héritera de son titre sans qu'on y touche.
    ///
    /// ASCII pur, délibérément : SDL3-CS ne marshale pas le titre en UTF-8 et le tiret
    /// cadratin ressortait « â€" » dans la barre de fenêtre. Un titre est du texte que
    /// l'utilisateur lit ; ce n'est pas l'endroit où défendre la typographie.
    /// </summary>
    private static string WindowTitle() =>
        $"iXtal26 - {Models.model_c.models[Models.model_c.model].name}";

    /// <summary>Repli tant que le CGA n'a pas appelé updatewindowsize : 656 x (200 * 2 + 16).</summary>
    private const int DefaultWindowWidth = 656;

    private const int DefaultWindowHeight = 416;

    /// <summary>
    /// vid_cga.cs:413 arrête le balayage à « displine >= 360 », et la hauteur demandée
    /// au blit vaut (lastline - firstline) + 8, donc 368 lignes au pire. Une texture
    /// 2048 x 2048, calquée sur Buffer32, coûterait 16 Mo pour n'en servir que 368.
    /// </summary>
    private const int TextureHeight = 512;

    /// <summary>Intervalle, en tranches, entre deux rafraîchissements du titre.</summary>
    private const int TitleInterval = 200;

    /// <summary>
    /// Budget de turbo par défaut, en tranches. L'invite BASIC tombe à la tranche 5 167 et
    /// l'invite de date de PC DOS 2.00 à 5 520 (VERIFICATION.md § M6, bissectées) : on
    /// laisse de quoi voir la bannière s'écrire. Ici et pas dans Program.cs parce que le
    /// menu s'en sert aussi pour réarmer le turbo après un reset — un seul 5800 dans l'arbre.
    /// </summary>
    internal const int DefaultTurboSlices = 5800;

    /// <summary>
    /// Intervalle minimal, en millisecondes d'horloge MURALE, entre deux présentations
    /// pendant le turbo. Le CGA émet ~29 images par seconde ÉMULÉE ; à 14x le temps
    /// réel cela ferait ~400 présentations par seconde murale, qu'aucun écran ne montre.
    /// </summary>
    private const int TurboPresentIntervalMs = 16;

    private readonly string _romsPath;
    private readonly bool _headless;
    private readonly int _maxSlices;
    private readonly bool _verbose;

    /// <summary>
    /// Nombre de tranches pendant lesquelles on n'attend pas l'horloge murale au
    /// démarrage ; 0 = pas de turbo. Ne concerne QUE le mode cadencé : avec
    /// --slices N, l'exécution entière reste libre, et c'est ce qui garantit que
    /// deux exécutions traversent les mêmes états.
    ///
    /// Plus readonly depuis que le menu peut relancer le turbo après un reset : une
    /// session lancée sans --turbo en obtient alors DefaultTurboSlices.
    /// </summary>
    private int _turboSlices;

    /// <summary>
    /// Tranche à laquelle le turbo courant a commencé. Le compteur doit être RELATIF :
    /// « slices &lt; _turboSlices » ne peut plus être vrai à la tranche 12 000, donc sans
    /// cette base un reset ne pourrait jamais relancer le turbo.
    /// </summary>
    private int _turboBase;

    private bool _turboActive;

    /// <summary>Une frappe met fin au turbo : à partir de là, quelqu'un regarde. Le bip
    /// de fin de POST aussi : voir la boucle de Run().</summary>
    private bool _turboStopped;

    /// <summary>Lequel des deux a mis fin au turbo. Purement pour --verbose, mais un
    /// bilan qui dit « interrompu par une frappe » quand personne n'a touché au clavier
    /// est un bilan qui ment.</summary>
    private bool _turboStoppedByBeep;

    private ulong _lastPresentMs;
    private int _presentsSkipped;

    // Départ du coût du chemin de blit, en tops de Stopwatch : ConsumeBlit (recopie
    // ligne à ligne + UpdateTexture) d'un côté, Render (RenderPresent, donc l'attente
    // du compositeur) de l'autre. Les 9,49 s mur contre 5,42 s user d'un amorçage sans
    // frein se partagent entre ces deux-là, et rien ne disait lequel payait.
    private long _copyTicks;
    private long _presentTicks;

    // Compteurs de --verbose. Un chemin de blit mort est autrement indiscernable d'un
    // écran légitimement noir : SDL ne signale rien quand on ne l'appelle PAS.
    private int _blitsSeen;
    private int _blitsUploaded;
    private int _updateFailures;
    private int _eventsSeen;
    private SDL.EventType _lastEventType;
    private int _lastX, _lastY, _lastY1, _lastY2, _lastW, _lastH;

    private IntPtr _window;
    private IntPtr _renderer;
    private IntPtr _texture;

    /// <summary>
    /// Menu Ctrl+F12. Null en --headless : Init() rend avant d'avoir un renderer, et le
    /// menu n'a alors rien où se dessiner ni personne pour l'ouvrir.
    /// </summary>
    private SdlMenu? _menu;

    /// <summary>
    /// Sortie audio. Null en --headless, et null aussi si le périphérique refuse de
    /// s'ouvrir : une carte son absente n'est pas une panne de 5150. La machine
    /// émulée ne change pas pour autant — sound_poll tourne et speaker_update
    /// remplit dans les deux cas, seul le dépôt disparaît.
    /// </summary>
    private SdlAudio? _audio;

    /// <summary>
    /// Action demandée par le menu, à exécuter au sommet de la boucle de Run(). Elle ne
    /// peut pas l'être dans PumpEvents : resetpchard() reconstruit la machine entière, et
    /// les compteurs d'horloge à réarmer sont des locales de Run().
    /// </summary>
    private MenuAction _pendingAction;

    private bool _sdlInitialised;
    private bool _disposed;
    private bool _running;

    // Image visible, recopiée ligne à ligne depuis Buffer32 ; allouée au premier blit
    // et réallouée seulement si la géométrie grandit.
    private uint[] _screen = [];
    private int _frameWidth;
    private int _frameHeight;

    private int _windowWidth;
    private int _windowHeight;

    /// <summary>
    /// <paramref name="headless"/> n'initialise aucune ressource SDL : c'est ce qui
    /// garantit que le cœur ne dépend pas du front-end. <paramref name="maxSlices"/>
    /// borne le nombre d'appels à runpc() ; 0 = jusqu'à la fermeture de la fenêtre.
    /// <paramref name="turboSlices"/> lève le frein d'horloge sur les premières
    /// tranches ; 0 = jamais.
    /// </summary>
    public SdlHost(string romsPath, bool headless, int maxSlices, bool verbose, int turboSlices = 0)
    {
        _romsPath = romsPath;
        _headless = headless;
        _maxSlices = maxSlices;
        _verbose = verbose;
        _turboSlices = turboSlices;
    }

    /// <summary>Amorce la machine puis, si besoin, la fenêtre. False + message sur stderr en cas d'échec.</summary>
    public bool Init()
    {
        if (_headless && _maxSlices <= 0)
        {
            Console.Error.WriteLine(
                "Mode headless : un nombre de tranches strictement positif est obligatoire, " +
                "sans fenêtre rien n'arrête la boucle.");
            return false;
        }

        // initpc() écrit lui-même la raison de son échec (ROMs absentes).
        if (!pc.initpc(_romsPath))
            return false;

        if (_headless)
            return true;

        SDL.SetAppMetadata(WindowTitle(), "1.0", "com.example.ixtal26");

        if (!SDL.Init(SDL.InitFlags.Video | SDL.InitFlags.Audio))
            return Fail("SDL.Init");

        _sdlInitialised = true;

        // video_width / video_height sont posés par updatewindowsize (video.cs:543),
        // appelée depuis vid_cga.cs:491 avec (xsize, (ysize << 1) + 16) : le doublement
        // vertical de l'aspect CGA est déjà dans la valeur, l'hôte ne recalcule rien.
        // Ils valent zéro avant le premier balayage, d'où le repli.
        _windowWidth = video.video_width > 0 ? video.video_width : DefaultWindowWidth;
        _windowHeight = video.video_height > 0 ? video.video_height : DefaultWindowHeight;

        if (!SDL.CreateWindowAndRenderer(WindowTitle(), _windowWidth, _windowHeight,
                SDL.WindowFlags.Resizable, out _window, out _renderer))
            return Fail("SDL.CreateWindowAndRenderer");

        // DEVIATION: PCem laisse la vsync À ZÉRO par défaut (video_vsync = 0,
        //   wx-sdl2-video.c:30) et n'en fait qu'une option de menu. La forcer ici
        //   bloquerait RenderPresent jusqu'au vblank AU MILIEU de l'accumulateur
        //   drawits, puisque iXtal26 présente sur le fil d'émulation : sur une sortie
        //   à 30 Hz, les ~60 présentations/s du CGA ne passent plus, drawits accumule
        //   le retard, et la branche « if (drawits > 50) drawits = 0 » le jette
        //   SANS RIEN DIRE. L'émulé perdrait du temps sans diagnostic. On reste donc
        //   sur le défaut de PCem.
        SDL.SetRenderVSync(_renderer, 0);

        _texture = SDL.CreateTexture(_renderer, SDL.PixelFormat.XRGB8888,
            SDL.TextureAccess.Streaming, video.Stride, TextureHeight);

        if (_texture == IntPtr.Zero)
            return Fail("SDL.CreateTexture");

        // Pixel d'époque : l'interpolation bilinéaire rendrait le 8x8 du CGA flou.
        SDL.SetTextureScaleMode(_texture, SDL.ScaleMode.Nearest);

        _menu = new SdlMenu(_window, _renderer, _romsPath);

        // Pendant de sound_init() (sound.c:201), que PCem appelle depuis son IHM
        // (wx-sdl2.c:470) et non depuis pc.c. Un échec n'est pas fatal : le 5150
        // tourne muet, et le chronomètre du son du cœur n'en sait rien.
        var audio = new SdlAudio();
        if (audio.Init())
        {
            _audio = audio;
            Sound.sound.sound_give_buffer_func = _audio.GiveBuffer;
        }
        else
        {
            audio.Dispose();
            Console.Error.WriteLine(
                $"Audio indisponible ({SDL.GetError()}) — la machine tourne, sans le haut-parleur.");
        }

        // Le CGA n'émet son premier blit qu'à la tranche ~348, soit 3,5 s d'horloge
        // murale : sans cette peinture, la fenêtre reste au contenu indéfini que le
        // compositeur veut bien lui donner pendant tout ce temps.
        Render();

        // DEVIATION: PCem réveille un thread de blit ; iXtal26 est mono-thread et
        //   branche donc un crochet SYNCHRONE. Ce n'est pas un raccourci — c'est ce
        //   qui redonne la propriété du handshake d'origine : video_blit_memtoscreen
        //   ne rend la main qu'une fois le rectangle remonté, donc aucune image ne
        //   peut être écrasée avant lecture. Voir le commentaire de video.cs au site
        //   d'appel. Le drapeau blit_pending n'est plus consulté par cet hôte.
        video.video_blit_memtoscreen_func = OnBlit;

        return true;
    }

    /// <summary>
    /// Crochet appelé par le cœur DEPUIS video_blit_memtoscreen, donc au beau milieu
    /// d'execx86. C'est voulu et c'est fidèle : PCem y bloque le fil d'émulation sur
    /// video_wait_for_blit. Mono-thread, l'appel direct a la même propriété, et le
    /// renderer SDL reste utilisé depuis le fil qui l'a créé.
    /// </summary>
    private void OnBlit(int x, int y, int y1, int y2, int w, int h)
    {
        video.blit_pending = 0;
        _blitsSeen++;

        // Pendant le turbo, la machine tourne ~14x plus vite que l'horloge : présenter
        // chaque image demanderait des centaines de RenderPresent par seconde murale à
        // un écran qui en montre soixante. On en saute — mais on ne saute que la part
        // HÔTE : video_blit_complete reste inconditionnel, c'est lui qui rend la
        // propriété du handshake de PCem (aucune image écrasée avant lecture). Le CGA
        // redessine toutes ses lignes à chaque image : une image sautée n'est jamais
        // une ligne perdue, seulement une image jamais montrée.
        var now = _headless ? 0UL : SDL.GetTicks();

        if (_turboActive && !_headless && now - _lastPresentMs < TurboPresentIntervalMs)
        {
            _presentsSkipped++;
            video.video_blit_complete();
            return;
        }

        var t0 = Stopwatch.GetTimestamp();
        ConsumeBlit();
        var t1 = Stopwatch.GetTimestamp();

        video.video_blit_complete();

        var t2 = Stopwatch.GetTimestamp();
        Render();

        _copyTicks += t1 - t0;
        _presentTicks += Stopwatch.GetTimestamp() - t2;
        _lastPresentMs = now;
    }

    /// <summary>Boucle principale. Renvoie le code de retour du processus.</summary>
    public int Run()
    {
        // Sans borne de tranches on suit l'horloge murale ; avec, on boucle serré,
        // sinon deux exécutions du même test ne donnent pas le même nombre de cycles.
        var timed = _maxSlices <= 0;

        // SIGNÉ, et il passe volontairement sous zéro : c'est le seul frein qui
        // empêche l'émulation de prendre de l'avance sur l'horloge (wx-sdl2.c:168-190).
        var drawits = 0;

        var oldTime = _headless ? 0UL : SDL.GetTicks();
        var turboStartMs = oldTime;

        // Images sautées déjà comptées quand le turbo courant a commencé ; voir le
        // réarmement plus bas. Zéro pour le turbo de lancement.
        var presentsBase = 0;
        var slices = 0;
        var slicesAtLastTitle = 0;
        var lastTitleMs = oldTime;

        // Temps passé DANS runpc(), en tops de Stopwatch. Le pourcentage du titre est
        // borné à 100 par le frein drawits ci-dessous : il détecte un décrochage, il ne
        // dit rien de la marge. Ce compteur, lui, la donne (voir UpdateTitle).
        var busyTicks = 0L;
        var busyTicksAtLastTitle = 0L;

        _running = true;

        while (_running)
        {
            // PCem sonde les évènements sur un thread séparé ; iXtal26 est mono-thread,
            // donc on les sonde ici, à chaque tour : c'est quelques microsecondes.
            if (!_headless)
                PumpEvents();

            if (!_running)
                break;

            // Les actions du menu s'exécutent ICI, au sommet de la boucle : donc hors de
            // runpc() et hors de OnBlit. resetpchard() reconstruit la machine entière
            // (device_close_all, mem_alloc, model_init) ; l'appeler depuis le crochet de
            // blit, c'est-à-dire depuis l'intérieur d'execx86, arracherait l'état sous les
            // pieds de l'instruction en cours. PumpEvents ne doit donc jamais agir lui-même.
            if (_pendingAction != MenuAction.None)
            {
                MenuAction action = _pendingAction;
                _pendingAction = MenuAction.None;

                switch (action)
                {
                    case MenuAction.HardReset:
                        pc.resetpchard();
                        break;

                    case MenuAction.HardResetTurbo:
                        pc.resetpchard();

                        // Turbo NEUF. Une session lancée sans --turbo en obtient le budget
                        // par défaut : sinon l'entrée de menu ne ferait rien, ce qui est
                        // indiscernable d'une entrée cassée.
                        if (_turboSlices <= 0)
                            _turboSlices = DefaultTurboSlices;

                        _turboBase = slices;
                        _turboStopped = _turboStoppedByBeep = false;
                        turboStartMs = SDL.GetTicks();

                        // Base, et PAS une remise à zéro : _presentsSkipped est aussi le
                        // total qu'imprime --verbose, et ce total existe pour distinguer
                        // un chemin de blit mort d'un écran légitimement noir.
                        presentsBase = _presentsSkipped;
                        break;

                    case MenuAction.Cad:
                        // L'invité redémarre de lui-même, sur sa propre cadence : le BIOS
                        // trouve 0x1234 en 0040:0072 et saute le test mémoire. Rien à
                        // recharger — l'insertion s'est faite à chaud, le lecteur porte
                        // déjà l'image.
                        pc.resetpc_cad();
                        break;

                    default:
                        _running = false;
                        break;
                }

                if (!_running)
                    break;

                // Horloge NEUVE, exactement comme à la sortie du turbo plus bas : le temps
                // passé dans le menu et dans resetpchard() ne doit pas s'accumuler dans
                // drawits, que la branche « if (drawits > 50) drawits = 0 » jetterait SANS
                // RIEN DIRE. Et le compteur du titre repart de la même origine, sinon la
                // première fenêtre glissante d'après-menu affiche un pourcentage absurde.
                oldTime = SDL.GetTicks();
                drawits = 0;
                lastTitleMs = oldTime;
                slicesAtLastTitle = slices;
                busyTicksAtLastTitle = busyTicks;
            }

            // Menu ouvert : la machine est EN PAUSE, comme PCem qui pose pause = 1 autour
            // de chaque action (wx-sdl2.c:728-738). C'est aussi ce qui fait de cette boucle
            // le seul présentateur : sans tranche il n'y a pas de blit, donc pas d'OnBlit,
            // et le menu doit s'afficher même sur un invité qui a coupé la vidéo.
            //
            // Effet de bord assumé : UpdateTitle n'est atteint qu'après runpc(), donc le
            // pourcentage du titre se FIGE tant que le menu est ouvert. C'est correct —
            // la machine ne tourne pas — mais ça ressemble à un gel, d'où cette ligne.
            if (_menu is not null && _menu.IsOpen)
            {
                _menu.Poll();
                Render();
                SDL.Delay(16);
                continue;
            }

            // Le turbo ne vit QUE dans le mode cadencé : --slices N tourne déjà sans
            // frein de bout en bout, et c'est un contrat que PrintUsage documente
            // (« deux exécutions traversent alors les mêmes états »). L'y mêler
            // rendrait ce mode partiellement cadencé, donc non reproductible.
            // RELATIF à _turboBase : à 0 au lancement c'est le test d'origine, et après un
            // « Reset materiel + turbo » à la tranche 12 000 c'est ce qui redonne un budget.
            // LE BIP MET FIN AU TURBO. Le BIOS n'arme le haut-parleur qu'une fois le
            // test mémoire passé : le bip EST le signal de fin de POST, donc
            // exactement ce que le turbo existe pour atteindre. Et il tombe là où la
            // taille RAM le place — tranche 4769 à 640 Ko (mesuré), bien plus tôt à 64.
            // Sans cette ligne, le budget par défaut de 5800 tranches l'engloutit
            // entier, puisque la sortie est coupée pendant le turbo : on entendrait le
            // silence précisément à la seconde où il y a quelque chose à entendre.
            // Même mécanique que la frappe — on pose _turboStopped, la condition
            // ci-dessous fait le reste, y compris l'horloge neuve et la réouverture
            // de la sortie.
            if (_turboActive && Sound.sound_speaker.speaker_enable != 0)
                _turboStopped = _turboStoppedByBeep = true;

            var turbo = timed && _turboSlices > 0 && !_turboStopped
                        && slices - _turboBase < _turboSlices;

            if (turbo != _turboActive)
            {
                _turboActive = turbo;

                // Muet pendant le turbo. À ~14x le temps réel, le mixeur produit
                // quatorze secondes de son par seconde murale : la file se remplit,
                // la contre-pression jette, et il ne reste qu'un hachis. Le turbo est
                // une décision d'hôte que PCem n'a pas — on coupe la sortie, comme on
                // saute déjà les présentations. La machine émulée ne change pas d'un
                // cycle : sound_poll tourne, speaker_update remplit.
                if (_audio is not null)
                    _audio.Muted = turbo;

                // Fin du turbo : horloge NEUVE. Sans ce réarmement, drawits encaisse
                // d'un coup les ~50 s de retard accumulées pendant le turbo, et la
                // branche « if (drawits > 50) drawits = 0 » les jette SANS RIEN DIRE.
                // Le compteur du titre est remis à la même origine, sinon la première
                // fenêtre glissante d'après turbo afficherait un pourcentage absurde.
                if (!turbo)
                {
                    oldTime = SDL.GetTicks();

                    // Dit ce que le turbo a coûté et rapporté, une fois, au moment où
                    // il rend la main. Sans cette ligne, la seule trace du turbo est
                    // un titre de fenêtre qui a déjà changé quand on le lit.
                    // Compté depuis _turboBase : un turbo relancé par le menu à la
                    // tranche 12 000 rapporterait sinon les 12 000 d'avant avec lui.
                    var turboMs = oldTime - turboStartMs;
                    var turboSlices = slices - _turboBase;
                    Console.WriteLine(
                        $"turbo : {turboSlices} tranches ({turboSlices / 100.0:0.#} s émulées) en " +
                        $"{turboMs / 1000.0:0.00} s mur" +
                        (turboMs > 0 ? $" (x{turboSlices * 10.0 / turboMs:0.0})" : "") +
                        $", {_presentsSkipped - presentsBase} images sautées" +
                        (!_turboStopped ? ""
                            : _turboStoppedByBeep ? ", rendu au temps réel par le bip de fin de POST"
                            : ", interrompu par une frappe"));

                    drawits = 0;
                    lastTitleMs = oldTime;
                    slicesAtLastTitle = slices;
                    busyTicksAtLastTitle = busyTicks;
                }
            }

            if (timed && !turbo)
            {
                var newTime = SDL.GetTicks();
                drawits += (int)(newTime - oldTime);
                oldTime = newTime;

                if (drawits <= 0)
                {
                    SDL.Delay(1);
                    continue;
                }

                // wx-sdl2.c:176-179, dans cet ordre : on décompte d'abord la tranche
                // de 10 ms, puis on ABANDONNE le retard résiduel au-delà de 50 ms.
                // Après un gel (fenêtre déplacée), rattraper en accéléré serait pire.
                drawits -= 10;
                if (drawits > 50)
                    drawits = 0;
            }

            var t0 = Stopwatch.GetTimestamp();
            pc.runpc();
            busyTicks += Stopwatch.GetTimestamp() - t0;
            slices++;

            // Plus de PresentIfBlitted ici : le crochet OnBlit a déjà tout fait,
            // pendant runpc(). Il ne reste que la taille de fenêtre, qui ne change
            // qu'à la reprogrammation du CRTC.
            if (!_headless)
                SyncWindowSize();

            if (_maxSlices > 0 && slices >= _maxSlices)
                _running = false;

            if (timed && slices - slicesAtLastTitle >= TitleInterval)
            {
                var nowMs = SDL.GetTicks();
                UpdateTitle(slices - slicesAtLastTitle, nowMs - lastTitleMs,
                            busyTicks - busyTicksAtLastTitle, turbo);
                slicesAtLastTitle = slices;
                lastTitleMs = nowMs;
                busyTicksAtLastTitle = busyTicks;
            }
        }

        if (_verbose)
            PrintSummary(slices);

        // Une fenêtre noire et un code 0 sont indiscernables d'un succès dans une
        // chaîne d'intégration. Si PAS UNE SEULE image n'a atteint la texture alors
        // que le cœur en a émis, c'est un échec, et il faut le dire au shell.
        if (!_headless && _updateFailures > 0 && _blitsUploaded == 0)
        {
            Console.Error.WriteLine(
                $"aucune image n'a atteint la texture ({_updateFailures} echecs d'UpdateTexture).");
            return 1;
        }

        return 0;
    }

    /// <summary>
    /// Bilan de --verbose.
    ///
    /// « émis » est video_frames, que video.cs:351 incrémente AVANT son « if (h &lt;= 0)
    /// return » : il compte donc aussi des appels que le cœur rejette lui-même et qui
    /// n'ont jamais été des images. L'écart avec « consommés » n'est PAS le nombre
    /// d'images perdues — depuis le crochet synchrone, il ne peut plus y en avoir.
    ///
    /// En --headless, « consommés » vaut zéro par construction : aucun crochet n'est
    /// installé. C'est dit explicitement, faute de quoi ce bilan ressemblerait très
    /// exactement à un chemin de blit mort — ce que --verbose existe pour écarter.
    /// </summary>
    private void PrintSummary(int slices)
    {
        Console.WriteLine($"tranches   : {slices} ({slices / 100.0:0.##} s émulées)");
        Console.WriteLine($"blits      : {video.video_frames} émis par le cœur, {_blitsSeen} consommés, " +
                          $"{_blitsUploaded} téléversés, {_updateFailures} en échec" +
                          (_presentsSkipped > 0 ? $", {_presentsSkipped} sautés en turbo" : ""));

        // Le chemin de blit est de l'HÔTE, pas de la machine : ces deux chiffres
        // disent lequel de la recopie ou de l'attente du compositeur paie, question
        // que le banc (headless) ne peut pas poser puisqu'il n'installe aucun crochet.
        if (!_headless && _blitsUploaded > 0)
            Console.WriteLine(
                $"chemin blit: recopie+UpdateTexture {_copyTicks / (double)Stopwatch.Frequency * 1000:0} ms, " +
                $"RenderPresent {_presentTicks / (double)Stopwatch.Frequency * 1000:0} ms " +
                $"sur {_blitsUploaded} images");

        if (_headless)
            Console.WriteLine("             (headless : aucun crochet installé, zéro consommé est NORMAL)");

        // Le chemin audio est aussi muet qu'un écran noir quand il échoue : sans ces
        // deux chiffres, « pas de son » ne distingue pas un périphérique absent d'un
        // mixeur qui ne produit rien. Même leçon que § M4.5.
        Console.WriteLine(_audio is null
            ? "audio      : aucune sortie ouverte"
            : $"audio      : {_audio.BlocksQueued} blocs déposés, {_audio.BlocksDropped} jetés " +
              $"({Sound.sound.sound_buf_len_al} échantillons par bloc, " +
              $"{Sound.sound.sound_buf_len_al / 48.0:0.#} ms)");

        if (_blitsSeen == 0)
            Console.WriteLine("dernier    : aucun");
        else
            Console.WriteLine($"dernier    : x={_lastX} y={_lastY} y1={_lastY1} y2={_lastY2} " +
                              $"w={_lastW} h={_lastH}");

        Console.WriteLine($"géométrie  : xsize={video.xsize} ysize={video.ysize} " +
                          $"fenêtre {video.video_width}x{video.video_height}");
        Console.WriteLine($"évènements : {_eventsSeen} reçus de SDL, dernier {_lastEventType}");
        Console.WriteLine($"clavier    : {SdlKeyboard.KeyEventsSeen} KeyDown reçus de SDL, " +
                          $"{SdlKeyboard.KeyEventsMapped} mappés ; dernier scancode " +
                          $"{SdlKeyboard.LastScancode} -> {SdlKeyboard.LastMapped}");

        // Un menu qui ne s'ouvre jamais parce que le gestionnaire de fenêtres intercepte
        // Ctrl+F12 est autrement indiscernable d'un menu qui s'ouvre et ne dessine rien.
        if (_menu is not null)
            Console.WriteLine($"menu       : {_menu.Opens} ouvertures, {_menu.Inserts} insertions, " +
                              $"{_menu.Resets} resets, {_menu.Creations} creations");
    }

    /// <summary>Vide la file d'évènements SDL et la transmet intégralement au clavier.</summary>
    private void PumpEvents()
    {
        while (SDL.PollEvent(out var e))
        {
            var type = (SDL.EventType)e.Type;
            _eventsSeen++;
            _lastEventType = type;

            if (type is SDL.EventType.Quit or SDL.EventType.WindowCloseRequested)
                _running = false;

            // Ctrl+F12 : bascule du menu de l'hôte. Le `continue` n'est pas cosmétique —
            // sans lui F12 (0x58) et Ctrl (0x1D) atterrissent dans keyboard.rawinputkey et
            // l'invité les voit. Le bloc est AU-DESSUS de la ligne du turbo ci-dessous pour
            // la même raison de fond : un accord de l'hôte n'est pas « quelqu'un tape ».
            //
            // Inerte sous --slices : ce mode ne cadence RIEN et son contrat est que deux
            // exécutions traversent les mêmes états (Program.cs refuse déjà --turbo à côté
            // pour cette raison). Insérer une disquette à la main le casserait autant.
            if (_menu is not null && _maxSlices <= 0 && type is SDL.EventType.KeyDown
                && e.Key.Scancode == SDL.Scancode.F12
                && (e.Key.Mod & SDL.Keymod.Ctrl) != 0 && !e.Key.Repeat)
            {
                if (_menu.IsOpen)
                    _menu.Close();
                else
                    _menu.Open();

                continue;
            }

            // Menu ouvert : il avale TOUT. L'invité ne doit pas recevoir les flèches avec
            // lesquelles on navigue, et elles ne doivent pas non plus tuer le turbo.
            if (_menu is not null && _menu.IsOpen)
            {
                MenuAction action = _menu.HandleEvent(in e);

                if (action != MenuAction.None)
                {
                    // Fermer AVANT d'agir : sans cela Run() retombe dans la pause au tour
                    // suivant, et la machine est réinitialisée mais figée — rien ne se
                    // passe à l'écran. Close() relâche aussi l'Entrée qui vient de valider.
                    _menu.Close();
                    _pendingAction = action;
                }

                continue;
            }

            // Une frappe met fin au turbo : à partir de là, quelqu'un regarde l'écran
            // et attend que la machine réponde à SA vitesse, pas à celle de l'hôte.
            if (type is SDL.EventType.KeyDown)
                _turboStopped = true;

            // Tout part au clavier, y compris la perte de focus : c'est lui qui décide
            // ce qu'il en fait. Échap n'arrête rien ici — la touche est à la machine.
            SdlKeyboard.HandleEvent(in e);
        }
    }

    /// <summary>Pendant de updatewindowsize (video.cs:543), appelée une fois par changement.</summary>
    private void SyncWindowSize()
    {
        if (video.video_width <= 0 || video.video_height <= 0)
            return;

        if (video.video_width == _windowWidth && video.video_height == _windowHeight)
            return;

        _windowWidth = video.video_width;
        _windowHeight = video.video_height;
        SDL.SetWindowSize(_window, _windowWidth, _windowHeight);
    }

    /// <summary>Remontée Buffer32 -> texture, sémantique de sdl_blit_memtoscreen (wx-sdl2-video.c:175-193).</summary>
    private void ConsumeBlit()
    {
        var x = video.blit_data.x;
        var y = video.blit_data.y;
        var y1 = video.blit_data.y1;
        var y2 = video.blit_data.y2;
        var w = video.blit_data.w;
        var h = video.blit_data.h;

        _lastX = x;
        _lastY = y;
        _lastY1 = y1;
        _lastY2 = y2;
        _lastW = w;
        _lastH = h;

        // wx-sdl2-video.c:176 — y1 == y2 : rien à recopier, l'image précédente reste.
        if (y1 >= y2 || w <= 0 || h <= 0)
            return;

        // w vient de xsize, que vid_cga calcule depuis le registre 1 du CRTC : un
        // programme invité qui y écrit 0xFF en 40 colonnes donne xsize = 4096, soit
        // le double de la texture. SDL.UpdateTexture rend alors TRUE en lisant hors
        // du tampon source. On borne des deux côtés plutôt que de faire confiance à
        // une valeur que la machine émulée contrôle entièrement.
        if (w > video.Stride)
            w = video.Stride;
        if (y2 > TextureHeight)
            y2 = TextureHeight;
        if (h > TextureHeight)
            h = TextureHeight;

        var rows = h > y2 ? h : y2;
        if (_screen.Length < w * rows)
            _screen = new uint[w * rows];

        var source = video.Buffer32;

        for (var yy = y1 < 0 ? 0 : y1; yy < y2; yy++)
        {
            var line = y + yy;

            // Le CGA passe y = cga->firstline - 4 (vid_cga.cs:494), qui peut être
            // négatif : sans cette garde on lirait hors de Buffer32.
            if (line < 0 || line >= video.Height)
                continue;

            Array.Copy(source, (line * video.Stride) + x, _screen, yy * w, w);
        }

        _frameWidth = w;
        _frameHeight = h;

        var rect = new SDL.Rect { X = 0, Y = 0, W = w, H = h };

        // SDL3-CS n'expose pas de surcharge générique : seul ReadOnlySpan<byte> existe.
        // AsBytes réinterprète le tableau sans copie et sans bloc unsafe.
        ReadOnlySpan<uint> pixels = _screen.AsSpan(0, w * h);

        if (SDL.UpdateTexture(_texture, in rect, MemoryMarshal.AsBytes(pixels), w * 4))
        {
            _blitsUploaded++;
        }
        else
        {
            _updateFailures++;
            Console.Error.WriteLine($"SDL.UpdateTexture a echoue : {SDL.GetError()}");
        }
    }

    /// <summary>Une image : fond noir, puis la zone utile de la texture étirée à la fenêtre.</summary>
    private void Render()
    {
        SDL.SetRenderDrawColor(_renderer, 0, 0, 0, 255);
        SDL.RenderClear(_renderer);

        if (_frameWidth > 0 && _frameHeight > 0)
        {
            var src = new SDL.FRect { X = 0f, Y = 0f, W = _frameWidth, H = _frameHeight };
            SDL.RenderTexture(_renderer, _texture, in src, IntPtr.Zero);
        }

        // Le menu se compose ICI, sur le renderer, après la texture du CGA : il ne touche
        // jamais video.Buffer32. C'est ce qui garde l'image émulée comparable à l'oracle —
        // --boot, boot-diff et les empreintes de framebuffer de § M5.1 ne voient rien.
        if (_menu is not null && _menu.IsOpen)
            _menu.Render();

        SDL.RenderPresent(_renderer);
    }

    /// <summary>
    /// Une tranche = 10 ms émulées (pc.cs:177), donc 100 tranches par seconde murale
    /// valent 100 %. C'est la mesure qui dit si la machine tient ses 4,77 MHz.
    /// Le titre plutôt que RenderDebugText : l'image doit rester comparable à l'oracle.
    ///
    /// FENÊTRE GLISSANTE, pas moyenne cumulée. onesec() (pc.c:168-174) fait
    /// « fps = framecount; framecount = 0; » sur un timer d'une seconde réelle : le
    /// chiffre affiché est instantané. Une moyenne depuis le lancement rendrait
    /// l'indicateur aveugle à ce qu'il existe pour montrer — après dix minutes à
    /// 100 %, une chute à 50 % pendant trente secondes se lirait « 97 % ».
    ///
    /// LE POURCENTAGE NE PEUT PAS DÉPASSER 100. Le frein drawits de Run() attend
    /// l'horloge murale avant chaque tranche : un cœur avec 15x de marge et un cœur à
    /// 1,05x affichent tous deux « 100 % ». D'où la MARGE, à côté : temps émulé des
    /// tranches (10 ms chacune) divisé par le temps réellement passé dans runpc().
    /// Mesuré à M5 : environ 15x sur un cœur P à 4,8 GHz — pas les 50 à 100x que le
    /// csproj supposait avant qu'on ne mesure.
    /// </summary>
    private void UpdateTitle(int slices, ulong elapsedMs, long busyTicks, bool turbo)
    {
        if (elapsedMs == 0)
            return;

        var percent = slices * 1000L / (long)elapsedMs;
        var busySeconds = busyTicks / (double)Stopwatch.Frequency;
        var headroom = busySeconds > 0 ? slices * 0.01 / busySeconds : 0;

        // Pendant le turbo, le pourcentage n'a plus de sens : il dépasse 100 et ne dit
        // rien. Ce qu'on affiche alors est le facteur RÉELLEMENT tenu, présentation
        // comprise — pas la marge du cœur, qui est plus flatteuse.
        if (turbo)
        {
            var actual = slices * 10.0 / elapsedMs;
            SDL.SetWindowTitle(_window, $"{WindowTitle()} — turbo x{actual:0.0} (marge x{headroom:0.0})");
            return;
        }

        SDL.SetWindowTitle(_window, $"{WindowTitle()} — {percent} % — marge x{headroom:0.0}");
    }

    /// <summary>Libère texture, renderer, fenêtre puis SDL. Idempotent, et sans effet en headless.</summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        // Le cœur garde sinon un délégué vers un hôte détruit, et le prochain blit
        // rendrait à travers un renderer libéré.
        video.video_blit_memtoscreen_func = null;

        // Même raison : le cœur garderait un délégué vers un flux détruit, et le
        // prochain sound_poll déposerait dans un IntPtr libéré.
        Sound.sound.sound_give_buffer_func = null;
        _audio?.Dispose();
        _audio = null;

        _disposed = true;

        // pc.c:584-585, par closepc(). C'est le SEUL endroit qui vide les tampons
        // d'écriture sur les images : img_writeback écrit sans Flush(), et seul le Close()
        // de img_close les pousse. Sans cela un DOS qui vient d'écrire sur la disquette
        // perd ses écritures à la fermeture de la fenêtre — ce que le menu, en rendant le
        // va-et-vient de disquettes courant, rendrait courant aussi.
        pc.closepc();

        if (_texture != IntPtr.Zero)
        {
            SDL.DestroyTexture(_texture);
            _texture = IntPtr.Zero;
        }

        if (_renderer != IntPtr.Zero)
        {
            SDL.DestroyRenderer(_renderer);
            _renderer = IntPtr.Zero;
        }

        if (_window != IntPtr.Zero)
        {
            SDL.DestroyWindow(_window);
            _window = IntPtr.Zero;
        }

        if (_sdlInitialised)
        {
            SDL.Quit();
            _sdlInitialised = false;
        }
    }

    private static bool Fail(string what)
    {
        Console.Error.WriteLine($"{what} a echoue : {SDL.GetError()}");
        return false;
    }
}
