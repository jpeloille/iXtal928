// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/wx-ui/wx-sdl2-keyboard.c + wx-sdl2-display.c
//         (SDLScancodeToSystemScancode :144-252, sdl_scancode :253-261,
//          SDL_KEYDOWN/SDL_KEYUP :479-496)
// STATUS: host

using iXtal26.Keyboard;
using SDL3;

namespace iXtal26.Host;

/// <summary>
/// Pompe clavier de l'hôte : traduit un évènement SDL3 en scancode système PC/XT
/// (jeu 1) et remplit keyboard.rawinputkey, dont keyboard_poll_host() dérive pcem_key.
/// </summary>
public static class SdlKeyboard
{
    // DEVIATION: sdl_scancode() (display.c:253-261) balaie linéairement les 105 entrées à
    // chaque évènement ; ici la table est indexée par (int)SDL.Scancode. Aucun effet
    // observable : les 105 clés sont distinctes, et l'inconnu rend -1 des deux côtés.
    private static readonly int[] SystemScancode = BuildTable();

    /// <summary>Scancode système PC/XT de la touche, ou -1 si elle n'est pas mappée.</summary>
    public static int MapScancode(SDL.Scancode sc)
    {
        int index = (int)sc;

        // Le C rend -1 pour toute valeur absente de la table, y compris hors bornes :
        // sans ce garde la déviation ci-dessus lèverait là où l'oracle rend -1.
        if (index < 0 || index >= SystemScancode.Length)
            return -1;

        return SystemScancode[index];
    }

    /// <summary>Consomme un évènement SDL de touche ; tout autre type est ignoré.</summary>
    public static void HandleEvent(in SDL.Event e)
    {
        switch ((SDL.EventType)e.Type)
        {
            case SDL.EventType.KeyDown:
            {
                int keyIdx = MapScancode(e.Key.Scancode);
                if (keyIdx != -1)
                    keyboard.rawinputkey[keyIdx] = PressedValue(e.Key.Timestamp);

                // omitted: contournement clavier international de display.c:480-485
                // (#ifdef __WINDOWS__) — il compare le timestamp brut à rawinputkey[LCTRL],
                // égalité que le masquage de PressedValue romprait de toute façon.
                break;
            }

            case SDL.EventType.KeyUp:
            {
                int keyIdx = MapScancode(e.Key.Scancode);
                if (keyIdx != -1)
                    keyboard.rawinputkey[keyIdx] = 0;
                break;
            }

            // SDL ne livre pas le KeyUp d'une touche relâchée pendant que la fenêtre
            // n'a pas le focus : sans ce relâchement global, keyboard_poll_host() la
            // verrait enfoncée à jamais et la machine émulée répéterait le caractère.
            case SDL.EventType.WindowFocusLost:
                Reset();
                break;
        }

        // omitted: raccourcis Ctrl+Alt+PgDn / Ctrl+Alt+PgUp / Ctrl+End de display.c:499-520
        // (plein écran, capture d'écran, relâchement de la souris) — hors périmètre.
    }

    /// <summary>
    /// Relâche toutes les touches. À appeler sur perte de focus : SDL ne livre pas le
    /// KeyUp d'une touche relâchée hors fenêtre, et la machine émulée la verrait
    /// enfoncée à jamais.
    /// </summary>
    public static void Reset()
    {
        Array.Clear(keyboard.rawinputkey);
    }

    // Le timestamp SDL3 est en nanosecondes sur 64 bits là où SDL2 donnait des
    // millisecondes sur 32. keyboard_poll_host() teste « rawinputkey[c] > 0 » : un cast
    // nu passerait négatif une seconde sur deux et la touche compterait pour relâchée.
    private static int PressedValue(ulong timestamp)
    {
        int value = (int)(timestamp & 0x7FFFFFFF);
        return value == 0 ? 1 : value;
    }

    private static int[] BuildTable()
    {
        int[] table = new int[(int)SDL.Scancode.Count];
        Array.Fill(table, -1);

        // pcem: src/wx-ui/wx-sdl2-display.c:144-252 — SDLScancodeToSystemScancode[], les
        // 105 entrées dans l'ordre. Les noms SDL3-CS sont ceux de l'énumération réelle
        // (Alpha0, Leftbracket, Printscreen, NonUsBackSlash...), vérifiés par réflexion.
        Map(table, SDL.Scancode.A, 0x1e);
        Map(table, SDL.Scancode.B, 0x30);
        Map(table, SDL.Scancode.C, 0x2e);
        Map(table, SDL.Scancode.D, 0x20);
        Map(table, SDL.Scancode.E, 0x12);
        Map(table, SDL.Scancode.F, 0x21);
        Map(table, SDL.Scancode.G, 0x22);
        Map(table, SDL.Scancode.H, 0x23);
        Map(table, SDL.Scancode.I, 0x17);
        Map(table, SDL.Scancode.J, 0x24);
        Map(table, SDL.Scancode.K, 0x25);
        Map(table, SDL.Scancode.L, 0x26);
        Map(table, SDL.Scancode.M, 0x32);
        Map(table, SDL.Scancode.N, 0x31);
        Map(table, SDL.Scancode.O, 0x18);
        Map(table, SDL.Scancode.P, 0x19);
        Map(table, SDL.Scancode.Q, 0x10);
        Map(table, SDL.Scancode.R, 0x13);
        Map(table, SDL.Scancode.S, 0x1f);
        Map(table, SDL.Scancode.T, 0x14);
        Map(table, SDL.Scancode.U, 0x16);
        Map(table, SDL.Scancode.V, 0x2f);
        Map(table, SDL.Scancode.W, 0x11);
        Map(table, SDL.Scancode.X, 0x2d);
        Map(table, SDL.Scancode.Y, 0x15);
        Map(table, SDL.Scancode.Z, 0x2c);
        Map(table, SDL.Scancode.Alpha0, 0x0B);
        Map(table, SDL.Scancode.Alpha1, 0x02);
        Map(table, SDL.Scancode.Alpha2, 0x03);
        Map(table, SDL.Scancode.Alpha3, 0x04);
        Map(table, SDL.Scancode.Alpha4, 0x05);
        Map(table, SDL.Scancode.Alpha5, 0x06);
        Map(table, SDL.Scancode.Alpha6, 0x07);
        Map(table, SDL.Scancode.Alpha7, 0x08);
        Map(table, SDL.Scancode.Alpha8, 0x09);
        Map(table, SDL.Scancode.Alpha9, 0x0A);
        Map(table, SDL.Scancode.Grave, 0x29);
        Map(table, SDL.Scancode.Minus, 0x0c);
        Map(table, SDL.Scancode.Equals, 0x0d);
        Map(table, SDL.Scancode.NonUsBackSlash, 0x56);
        Map(table, SDL.Scancode.Backslash, 0x2b);
        Map(table, SDL.Scancode.Backspace, 0x0e);
        Map(table, SDL.Scancode.Space, 0x39);
        Map(table, SDL.Scancode.Tab, 0x0f);
        Map(table, SDL.Scancode.Capslock, 0x3a);
        Map(table, SDL.Scancode.LShift, 0x2a);
        Map(table, SDL.Scancode.LCtrl, 0x1d);
        Map(table, SDL.Scancode.LGUI, 0xdb);
        Map(table, SDL.Scancode.LAlt, 0x38);
        Map(table, SDL.Scancode.RShift, 0x36);
        Map(table, SDL.Scancode.RCtrl, 0x9d);
        Map(table, SDL.Scancode.RGUI, 0xdc);
        Map(table, SDL.Scancode.RAlt, 0xb8);
        Map(table, SDL.Scancode.SysReq, 0x54);
        Map(table, SDL.Scancode.Application, 0xdd);
        Map(table, SDL.Scancode.Return, 0x1c);
        Map(table, SDL.Scancode.Escape, 0x01);
        Map(table, SDL.Scancode.F1, 0x3B);
        Map(table, SDL.Scancode.F2, 0x3C);
        Map(table, SDL.Scancode.F3, 0x3D);
        Map(table, SDL.Scancode.F4, 0x3e);
        Map(table, SDL.Scancode.F5, 0x3f);
        Map(table, SDL.Scancode.F6, 0x40);
        Map(table, SDL.Scancode.F7, 0x41);
        Map(table, SDL.Scancode.F8, 0x42);
        Map(table, SDL.Scancode.F9, 0x43);
        Map(table, SDL.Scancode.F10, 0x44);
        Map(table, SDL.Scancode.F11, 0x57);
        Map(table, SDL.Scancode.F12, 0x58);
        Map(table, SDL.Scancode.Scrolllock, 0x46);
        Map(table, SDL.Scancode.Leftbracket, 0x1a);
        Map(table, SDL.Scancode.Rightbracket, 0x1b);
        Map(table, SDL.Scancode.Insert, 0xd2);
        Map(table, SDL.Scancode.Home, 0xc7);
        Map(table, SDL.Scancode.Pageup, 0xc9);
        Map(table, SDL.Scancode.Delete, 0xd3);
        Map(table, SDL.Scancode.End, 0xcf);
        Map(table, SDL.Scancode.Pagedown, 0xd1);
        Map(table, SDL.Scancode.Up, 0xc8);
        Map(table, SDL.Scancode.Left, 0xcb);
        Map(table, SDL.Scancode.Down, 0xd0);
        Map(table, SDL.Scancode.Right, 0xcd);
        Map(table, SDL.Scancode.NumLockClear, 0x45);
        Map(table, SDL.Scancode.KpDivide, 0xb5);
        Map(table, SDL.Scancode.KpMultiply, 0x37);
        Map(table, SDL.Scancode.KpMinus, 0x4a);
        Map(table, SDL.Scancode.KpPlus, 0x4e);
        Map(table, SDL.Scancode.KpEnter, 0x9c);
        Map(table, SDL.Scancode.KpPeriod, 0x53);
        Map(table, SDL.Scancode.Kp0, 0x52);
        Map(table, SDL.Scancode.Kp1, 0x4f);
        Map(table, SDL.Scancode.Kp2, 0x50);
        Map(table, SDL.Scancode.Kp3, 0x51);
        Map(table, SDL.Scancode.Kp4, 0x4b);
        Map(table, SDL.Scancode.Kp5, 0x4c);
        Map(table, SDL.Scancode.Kp6, 0x4d);
        Map(table, SDL.Scancode.Kp7, 0x47);
        Map(table, SDL.Scancode.Kp8, 0x48);
        Map(table, SDL.Scancode.Kp9, 0x49);
        Map(table, SDL.Scancode.Semicolon, 0x27);
        Map(table, SDL.Scancode.Apostrophe, 0x28);
        Map(table, SDL.Scancode.Comma, 0x33);
        Map(table, SDL.Scancode.Period, 0x34);
        Map(table, SDL.Scancode.Slash, 0x35);
        Map(table, SDL.Scancode.Printscreen, 0xb7);

        return table;
    }

    private static void Map(int[] table, SDL.Scancode sc, int systemScancode)
    {
        table[(int)sc] = systemScancode;
    }
}
