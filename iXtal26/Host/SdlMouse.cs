// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/wx-ui/wx-sdl2-mouse.c (mouse_poll_host) et wx-sdl2.c (capture)
// STATUS: host
//
// La souris de l'hôte vers la souris série émulée (M21). Comme PCem : un clic dans la
// fenêtre CAPTURE la souris (mode relatif SDL, curseur caché), Ctrl+Fin la LIBÈRE. Tant
// qu'elle est capturée, chaque pollmouse() lit le déplacement relatif et les boutons.

using iXtal26.Mouse;
using SDL3;

namespace iXtal26.Host;

internal static class SdlMouse
{
    private static IntPtr _window;
    internal static bool Captured { get; private set; }

    internal static void Init(IntPtr window)
    {
        _window = window;
        mouse.mouse_poll_host = PollHost;
    }

    /// <summary>Traite un évènement SDL ; rend vrai s'il est consommé (clic de capture,
    /// Ctrl+Fin de libération), et ne doit alors pas atteindre le clavier.</summary>
    internal static bool HandleEvent(in SDL.Event e)
    {
        var type = (SDL.EventType)e.Type;

        if (!Captured && type is SDL.EventType.MouseButtonDown)
        {
            Captured = SDL.SetWindowRelativeMouseMode(_window, true);
            SDL.GetRelativeMouseState(out _, out _); // purge le déplacement d'avant
            return true;
        }

        if (Captured && type is SDL.EventType.KeyDown && e.Key.Scancode == SDL.Scancode.End
            && (e.Key.Mod & SDL.Keymod.Ctrl) != 0)
        {
            SDL.SetWindowRelativeMouseMode(_window, false);
            Captured = false;
            return true;
        }

        return false;
    }

    // pcem: wx-sdl2-mouse.c:16-36
    private static void PollHost()
    {
        if (Captured)
        {
            var mb = SDL.GetRelativeMouseState(out var dx, out var dy);
            mouse.mouse_buttons = 0;
            if ((mb & SDL.MouseButtonFlags.Left) != 0)
                mouse.mouse_buttons |= 1;
            if ((mb & SDL.MouseButtonFlags.Right) != 0)
                mouse.mouse_buttons |= 2;
            if ((mb & SDL.MouseButtonFlags.Middle) != 0)
                mouse.mouse_buttons |= 4;
            mouse.mouse_x += (int)dx;
            mouse.mouse_y += (int)dy;
        }
        else
        {
            mouse.mouse_x = mouse.mouse_y = mouse.mouse_z = mouse.mouse_buttons = 0;
        }
    }
}
