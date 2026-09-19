// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: wx-sdl2.c, wx-sdl2-video.c
// STATUS: host

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
    // ASCII pur, délibérément : SDL3-CS ne marshale pas le titre en UTF-8 et le
    // tiret cadratin ressortait « â€" » dans la barre de fenêtre. Un titre est du
    // texte que l'utilisateur lit ; ce n'est pas l'endroit où défendre la typographie.
    private const string WindowTitle = "iXtal26 - IBM PC 5150";

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

    private readonly string _romsPath;
    private readonly bool _headless;
    private readonly int _maxSlices;
    private readonly bool _verbose;

    // Compteurs de --verbose. Un chemin de blit mort est autrement indiscernable d'un
    // écran légitimement noir : SDL ne signale rien quand on ne l'appelle PAS.
    private int _blitsSeen;
    private int _blitsUploaded;
    private int _updateFailures;
    private int _lastX, _lastY, _lastY1, _lastY2, _lastW, _lastH;

    private IntPtr _window;
    private IntPtr _renderer;
    private IntPtr _texture;
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
    /// </summary>
    public SdlHost(string romsPath, bool headless, int maxSlices, bool verbose)
    {
        _romsPath = romsPath;
        _headless = headless;
        _maxSlices = maxSlices;
        _verbose = verbose;
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

        SDL.SetAppMetadata(WindowTitle, "1.0", "com.example.ixtal26");

        if (!SDL.Init(SDL.InitFlags.Video))
            return Fail("SDL.Init");

        _sdlInitialised = true;

        // video_width / video_height sont posés par updatewindowsize (video.cs:543),
        // appelée depuis vid_cga.cs:491 avec (xsize, (ysize << 1) + 16) : le doublement
        // vertical de l'aspect CGA est déjà dans la valeur, l'hôte ne recalcule rien.
        // Ils valent zéro avant le premier balayage, d'où le repli.
        _windowWidth = video.video_width > 0 ? video.video_width : DefaultWindowWidth;
        _windowHeight = video.video_height > 0 ? video.video_height : DefaultWindowHeight;

        if (!SDL.CreateWindowAndRenderer(WindowTitle, _windowWidth, _windowHeight,
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
        ConsumeBlit();
        video.video_blit_complete();
        Render();
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
        var slices = 0;
        var slicesAtLastTitle = 0;
        var lastTitleMs = oldTime;

        _running = true;

        while (_running)
        {
            // PCem sonde les évènements sur un thread séparé ; iXtal26 est mono-thread,
            // donc on les sonde ici, à chaque tour : c'est quelques microsecondes.
            if (!_headless)
                PumpEvents();

            if (!_running)
                break;

            if (timed)
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

            pc.runpc();
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
                UpdateTitle(slices - slicesAtLastTitle, nowMs - lastTitleMs);
                slicesAtLastTitle = slices;
                lastTitleMs = nowMs;
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
                          $"{_blitsUploaded} téléversés, {_updateFailures} en échec");

        if (_headless)
            Console.WriteLine("             (headless : aucun crochet installé, zéro consommé est NORMAL)");

        if (_blitsSeen == 0)
            Console.WriteLine("dernier    : aucun");
        else
            Console.WriteLine($"dernier    : x={_lastX} y={_lastY} y1={_lastY1} y2={_lastY2} " +
                              $"w={_lastW} h={_lastH}");

        Console.WriteLine($"géométrie  : xsize={video.xsize} ysize={video.ysize} " +
                          $"fenêtre {video.video_width}x{video.video_height}");
    }

    /// <summary>Vide la file d'évènements SDL et la transmet intégralement au clavier.</summary>
    private void PumpEvents()
    {
        while (SDL.PollEvent(out var e))
        {
            var type = (SDL.EventType)e.Type;

            if (type is SDL.EventType.Quit or SDL.EventType.WindowCloseRequested)
                _running = false;

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
    /// </summary>
    private void UpdateTitle(int slices, ulong elapsedMs)
    {
        if (elapsedMs == 0)
            return;

        var percent = slices * 1000L / (long)elapsedMs;
        SDL.SetWindowTitle(_window, $"{WindowTitle} — {percent} %");
    }

    /// <summary>Libère texture, renderer, fenêtre puis SDL. Idempotent, et sans effet en headless.</summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        // Le cœur garde sinon un délégué vers un hôte détruit, et le prochain blit
        // rendrait à travers un renderer libéré.
        video.video_blit_memtoscreen_func = null;

        _disposed = true;

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
