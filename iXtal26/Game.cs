using SDL3;

namespace iXtal26;

/// <summary>
/// Fenêtre SDL3, boucle de jeu et rendu.
/// C'est le fichier à modifier : <see cref="Update"/> pour la logique, <see cref="Render"/> pour l'affichage.
/// </summary>
public sealed class Game : IDisposable
{
    private const string Title = "SDL3 Window";
    private const int Width = 1280;
    private const int Height = 720;

    private IntPtr _window;
    private IntPtr _renderer;
    private bool _running;
    private float _fps;

    // État de la démo : un carré qui se déplace et rebondit sur les bords.
    private SDL.FRect _box = new() { X = 64, Y = 64, W = 96, H = 96 };
    private float _vx = 420f;
    private float _vy = 310f;

    /// <summary>Initialise SDL, la fenêtre et le renderer. Renvoie false si quelque chose échoue.</summary>
    public bool Init()
    {
        SDL.SetAppMetadata(Title, "1.0", "com.example.ixtal26");

        if (!SDL.Init(SDL.InitFlags.Video))
            return Fail("SDL.Init");

        if (!SDL.CreateWindowAndRenderer(Title, Width, Height, SDL.WindowFlags.Resizable,
                out _window, out _renderer))
            return Fail("SDL.CreateWindowAndRenderer");

        // 1 = synchronisé sur le rafraîchissement de l'écran, 0 = aucune limite.
        SDL.SetRenderVSync(_renderer, 1);

        _running = true;
        return true;
    }

    /// <summary>
    /// Boucle principale. <paramref name="maxFrames"/> à 0 = tourne jusqu'à la fermeture ;
    /// une valeur positive affiche N images puis rend la main (utile pour tester sans interaction).
    /// </summary>
    public int Run(int maxFrames)
    {
        var last = SDL.GetTicksNS();
        var frames = 0;

        while (_running)
        {
            while (SDL.PollEvent(out var e))
                HandleEvent(in e);

            var now = SDL.GetTicksNS();
            var dt = (float)((now - last) / 1_000_000_000.0);
            last = now;

            // Borne le pas de temps : évite un saut après un freeze ou un déplacement de fenêtre.
            if (dt > 0.1f)
                dt = 0.1f;

            if (dt > 0f)
                _fps = _fps <= 0f ? 1f / dt : (_fps * 0.9f) + (0.1f / dt);

            Update(dt);
            Render();

            if (maxFrames > 0 && ++frames >= maxFrames)
                _running = false;
        }

        return 0;
    }

    /// <summary>Traite un événement SDL (fermeture, clavier, souris, manette...).</summary>
    private void HandleEvent(in SDL.Event e)
    {
        switch ((SDL.EventType)e.Type)
        {
            case SDL.EventType.Quit:
                _running = false;
                break;

            case SDL.EventType.KeyDown when e.Key.Scancode == SDL.Scancode.Escape:
                _running = false;
                break;
        }
    }

    /// <summary>Logique du jeu. <paramref name="dt"/> est le temps écoulé depuis l'image précédente, en secondes.</summary>
    private void Update(float dt)
    {
        SDL.GetRenderOutputSize(_renderer, out var w, out var h);

        // État clavier instantané, indexé par scancode (complémentaire des événements).
        var keys = SDL.GetKeyboardState(out _);
        var ax = 0f;
        var ay = 0f;
        if (keys[(int)SDL.Scancode.Left]) ax -= 1f;
        if (keys[(int)SDL.Scancode.Right]) ax += 1f;
        if (keys[(int)SDL.Scancode.Up]) ay -= 1f;
        if (keys[(int)SDL.Scancode.Down]) ay += 1f;

        _vx += ax * 900f * dt;
        _vy += ay * 900f * dt;

        _box.X += _vx * dt;
        _box.Y += _vy * dt;

        if (_box.X < 0f)
        {
            _box.X = 0f;
            _vx = -_vx;
        }

        if (_box.Y < 0f)
        {
            _box.Y = 0f;
            _vy = -_vy;
        }

        if (_box.X + _box.W > w)
        {
            _box.X = w - _box.W;
            _vx = -_vx;
        }

        if (_box.Y + _box.H > h)
        {
            _box.Y = h - _box.H;
            _vy = -_vy;
        }
    }

    /// <summary>Dessine une image complète.</summary>
    private void Render()
    {
        SDL.SetRenderDrawColor(_renderer, 18, 20, 28, 255);
        SDL.RenderClear(_renderer);

        SDL.SetRenderDrawColor(_renderer, 90, 200, 250, 255);
        SDL.RenderFillRect(_renderer, in _box);

        // Police de debug intégrée à SDL (ASCII uniquement, pas d'accents).
        SDL.SetRenderDrawColor(_renderer, 235, 235, 240, 255);
        SDL.RenderDebugText(_renderer, 8f, 8f, $"{_fps,4:F0} FPS   fleches: pousser   Echap: quitter");

        SDL.RenderPresent(_renderer);
    }

    public void Dispose()
    {
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

        SDL.Quit();
    }

    private static bool Fail(string what)
    {
        Console.Error.WriteLine($"{what} a echoue : {SDL.GetError()}");
        return false;
    }
}