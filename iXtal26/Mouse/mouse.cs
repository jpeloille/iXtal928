// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/mouse/mouse.c + includes/private/mouse/mouse.h
// STATUS: partial — mouse_t, mouse_emu_init, mouse_emu_close, mouse_poll,
//         mouse_get_name, mouse_get_type ; mouse_list réduit à la souris série
//         Microsoft (voir la DEVIATION).

namespace iXtal26.Mouse;

internal delegate object mouse_init_fn();
internal delegate void mouse_close_fn(object p);
internal delegate void mouse_poll_fn(int x, int y, int z, int b, object p);

// pcem: mouse.h:20-26
internal sealed class mouse_t
{
    internal string name = "";
    internal mouse_init_fn init = null!;
    internal mouse_close_fn close = null!;
    internal mouse_poll_fn poll = null!;
    internal int type;
}

internal static partial class mouse
{
    // pcem: mouse.h:11-18
    internal const int MOUSE_TYPE_SERIAL = 0;
    internal const int MOUSE_TYPE_PS2 = 1;
    internal const int MOUSE_TYPE_AMSTRAD = 2;
    internal const int MOUSE_TYPE_OLIM24 = 3;
    internal const int MOUSE_TYPE_IF_MASK = 3;
    internal const int MOUSE_TYPE_3BUTTON = 1 << 31;

    // pcem: mouse.c:9-15
    // DEVIATION: trois entrées sur six, AUX INDICES DE PCEM — la valeur de la clé mouse_type
    //   est un indice dans cette table. 0 : la souris série Microsoft, le défaut (pc.c:784) ;
    //   G1.3 : 4, l'Amstrad (amstrad.c), et 5, l'Olivetti M24 (keyboard_olim24.c). Omises
    //   (places nulles) : 1, Mouse Systems ; 2 et 3, les PS/2 à 2 et 3 boutons — un 8042 à
    //   souris que ce dépôt n'a pas. L'oracle n'amorce que la série (harness.c, M21) : les
    //   portes gardent mouse_type = 0.
    private static readonly mouse_t?[] mouse_list = { mouse_serial.mouse_serial_microsoft, null, null, null,
                                                      Models.amstrad.mouse_amstrad,
                                                      Keyboard.keyboard_olim24.mouse_olim24, null };

    // pcem: mouse.c:17-19
    private static mouse_t? cur_mouse;
    private static object? mouse_p;
    internal static int mouse_type = 0;

    // pcem: mouse.c:21-24
    internal static void mouse_emu_init()
    {
        cur_mouse = mouse_list[mouse_type];
        mouse_p = cur_mouse!.init();
    }

    // pcem: mouse.c:26-30
    internal static void mouse_emu_close()
    {
        if (cur_mouse != null)
                cur_mouse.close(mouse_p!);
        cur_mouse = null;
    }

    // pcem: mouse.c:32-35
    internal static void mouse_poll(int x, int y, int z, int b)
    {
        if (cur_mouse != null)
                cur_mouse.poll(x, y, z, b, mouse_p!);
    }

    // pcem: mouse.c:37-42
    internal static string? mouse_get_name(int mouse)
    {
        if (mouse_list[mouse] == null)
                return null;
        return mouse_list[mouse]!.name;
    }
    internal static int mouse_get_type(int mouse) { return mouse_list[mouse]!.type; }

    // pcem: wx-sdl2-mouse.c:5-43 — le côté hôte de la souris, réduit à ce que le cœur
    // lit : mouse_buttons, et les mickeys cumulés que mouse_get_mickeys rend puis remet
    // à zéro. mouse_poll_host est posé par l'hôte SDL (Host/SdlMouse.cs) ; sans fenêtre
    // il reste nul, et pollmouse ne transmet que des zéros — que mouse_serial_poll
    // ignore sans toucher à rien (mouse_serial.c:21-22).
    internal static int mouse_buttons;
    internal static int mouse_x, mouse_y, mouse_z;
    internal static Action? mouse_poll_host;

    internal static void mouse_get_mickeys(out int x, out int y, out int z)
    {
        x = mouse_x;
        y = mouse_y;
        z = mouse_z;
        mouse_x = mouse_y = mouse_z = 0;
    }
}
