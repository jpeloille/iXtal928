// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/mouse/mouse_ps2.c + includes/private/mouse/mouse_ps2.h
// STATUS: transcribed — mouse_scan, mouse_ps2_t, mouse_ps2_write, mouse_ps2_poll,
//         mouse_ps2_init, mouse_intellimouse_init, mouse_ps2_close, mouse_ps2_2_button,
//         mouse_intellimouse. Omis : la branche ROM_PC5086 (upc_set_mouse), machine absente.
//
// `mouse_buttons` est la globale de l'hôte (plat-mouse.h ; ici mouse.cs), lue telle quelle
// comme en C par E9h, EBh et le paquet — pas le `b` du poll, qui ne sert qu'au test de
// changement.
//
// PS2.0 (PLAN-PS2.md). La souris parle au 8042 de keyboard_at.cs : elle y pousse ses octets
// par keyboard_at_adddata_mouse, et le 8042 lui passe ce que l'invité écrit après D4h par le
// mouse_write que mouse_ps2_init enregistre.

using static iXtal26.Keyboard.keyboard_at;
using static iXtal26.Mouse.mouse;

namespace iXtal26.Mouse;

// pcem: mouse_ps2.c:16-34
// Classe et non struct : son adresse est prise (keyboard_at_set_mouse).
internal sealed class mouse_ps2_t
{
    internal int mode;

    internal uint8_t flags;
    internal uint8_t resolution;
    internal uint8_t sample_rate;

    internal uint8_t command;

    internal int cd;

    internal int x, y, z, b;

    internal int is_intellimouse;
    internal int intellimouse_mode;

    internal uint8_t[] last_data = new uint8_t[6];
}

internal static partial class mouse_ps2
{
    // pcem: mouse_ps2.c:10. Jusqu'à PS2.0 il vivait dans keyboard_at.cs (DEVIATION) ; il
    //   rejoint son fichier. Le 8042 le lit et l'écrit (A7h, A8h, D4h, octet de commande).
    internal static int mouse_scan = 0;

    // pcem: mouse_ps2.c:12
    internal const int MOUSE_STREAM = 0;
    internal const int MOUSE_REMOTE = 1;
    internal const int MOUSE_ECHO = 2;

    // pcem: mouse_ps2.c:14-15
    internal const int MOUSE_ENABLE = 0x20;
    internal const int MOUSE_SCALE = 0x10;

    // pcem: mouse_ps2.c:36-161
    internal static void mouse_ps2_write(uint8_t val, object? p)
    {
        // `p` est celui que mouse_ps2_init a passé à keyboard_at_set_mouse : jamais nul.
        mouse_ps2_t mouse = (mouse_ps2_t)p!;
        if (materiel.pb_94)
                if (mouse_ps2_entree_materiel(mouse, val))
                        return;

        if (mouse.cd != 0)
        {
                mouse.cd = 0;
                switch (mouse.command)
                {
                case 0xe8: /*Set mouse resolution*/
                        mouse.resolution = val;
                        keyboard_at_adddata_mouse(0xfa);
                        break;

                case 0xf3: /*Set sample rate*/
                        mouse.sample_rate = val;
                        keyboard_at_adddata_mouse(0xfa);
                        break;

                        //                        default:
                        //                        fatal("mouse_ps2 : Bad data write %02X for command %02X\n", val,
                        //                        mouse->command);
                }
        }
        else
        {
                uint8_t temp;

                mouse.command = val;
                // pcem bug, fixed in hardware mode: PB-94 — une commande hors de ce switch (F6h, EAh,
                //   F0h, EEh, ECh, EDh…) ne reçoit RIEN, pas même FAh (le `default` est commenté,
                //   :144-145) ; MOUSE_REMOTE et MOUSE_ECHO ne sont donc jamais posés.
                switch (mouse.command)
                {
                case 0xe6: /*Set scaling to 1:1*/
                        mouse.flags &= unchecked((uint8_t)~MOUSE_SCALE);
                        keyboard_at_adddata_mouse(0xfa);
                        break;

                case 0xe7: /*Set scaling to 2:1*/
                        mouse.flags |= MOUSE_SCALE;
                        keyboard_at_adddata_mouse(0xfa);
                        break;

                case 0xe8: /*Set mouse resolution*/
                        mouse.cd = 1;
                        keyboard_at_adddata_mouse(0xfa);
                        break;

                case 0xe9: /*Status request*/
                        keyboard_at_adddata_mouse(0xfa);
                        // pcem bug, fixed in hardware mode: PB-95 — la disposition du paquet (gauche en bit 0, droit en
                        //   bit 1), et le milieu en 3 ; l'octet d'état d'IBM met le gauche en bit 2, le droit en bit 0.
                        if (materiel.pb_95)
                                if (mouse_ps2_etat_materiel(mouse))
                                        break;
                        temp = mouse.flags;
                        if ((mouse_buttons & 1) != 0)
                                temp |= 1;
                        if ((mouse_buttons & 2) != 0)
                                temp |= 2;
                        if ((mouse_buttons & 4) != 0)
                                temp |= 3;
                        keyboard_at_adddata_mouse(temp);
                        keyboard_at_adddata_mouse(mouse.resolution);
                        keyboard_at_adddata_mouse(mouse.sample_rate);
                        break;

                case 0xeb: /*Get mouse data*/
                        keyboard_at_adddata_mouse(0xfa);

                        temp = 0;
                        if (mouse.x < 0)
                                temp |= 0x10;
                        if (mouse.y < 0)
                                temp |= 0x20;
                        if ((mouse_buttons & 1) != 0)
                                temp |= 1;
                        if ((mouse_buttons & 2) != 0)
                                temp |= 2;
                        if ((mouse_buttons & 4) != 0 && (mouse_get_type(mouse_type) & MOUSE_TYPE_3BUTTON) != 0)
                                temp |= 4;
                        keyboard_at_adddata_mouse(temp);
                        keyboard_at_adddata_mouse((uint8_t)(mouse.x & 0xff));
                        keyboard_at_adddata_mouse((uint8_t)(mouse.y & 0xff));
                        if (mouse.intellimouse_mode != 0)
                                keyboard_at_adddata_mouse((uint8_t)mouse.z);
                        // pcem bug, fixed in hardware mode: PB-261 — les compteurs de mouvement ne reviennent pas à
                        //   zéro après le paquet : la lecture suivante rend encore le même mouvement.
                        if (materiel.pb_261)
                                mouse_ps2_lu_materiel(mouse);
                        break;

                case 0xf2: /*Read ID*/
                        keyboard_at_adddata_mouse(0xfa);
                        if (mouse.intellimouse_mode != 0)
                                keyboard_at_adddata_mouse(0x03);
                        else
                                keyboard_at_adddata_mouse(0x00);
                        break;

                case 0xf3: /*Set sample rate*/
                        mouse.cd = 1;
                        keyboard_at_adddata_mouse(0xfa);
                        break;

                case 0xf4: /*Enable*/
                        mouse.flags |= MOUSE_ENABLE;
                        keyboard_at_adddata_mouse(0xfa);
                        break;

                case 0xf5: /*Disable*/
                        mouse.flags &= unchecked((uint8_t)~MOUSE_ENABLE);
                        keyboard_at_adddata_mouse(0xfa);
                        break;

                case 0xff: /*Reset*/
                        mouse.mode = MOUSE_STREAM;
                        mouse.flags = 0;
                        mouse.intellimouse_mode = 0;
                        mouse_queue_start = mouse_queue_end = 0;
                        if (materiel.pb_254)
                                keyboard_at_souris_vider_materiel();
                        if (materiel.pb_94)
                                mouse_ps2_defauts_materiel(mouse);
                        keyboard_at_adddata_mouse(0xfa);
                        keyboard_at_adddata_mouse(0xaa);
                        keyboard_at_adddata_mouse(0x00);
                        break;

                        //                        default:
                        //                        fatal("mouse_ps2 : Bad command %02X\n", val, mouse->command);
                default:
                        if (materiel.pb_94)
                                mouse_ps2_autre_materiel(mouse, val);
                        break;
                }
        }

        if (mouse.is_intellimouse != 0)
        {
                int c;

                for (c = 0; c < 5; c++)
                        mouse.last_data[c] = mouse.last_data[c + 1];

                mouse.last_data[5] = val;

                if (mouse.last_data[0] == 0xf3 && mouse.last_data[1] == 0xc8 && mouse.last_data[2] == 0xf3 &&
                    mouse.last_data[3] == 0x64 && mouse.last_data[4] == 0xf3 && mouse.last_data[5] == 0x50)
                        mouse.intellimouse_mode = 1;
        }
    }

    // pcem: mouse_ps2.c:163-212
    internal static void mouse_ps2_poll(int x, int y, int z, int b, object p)
    {
        mouse_ps2_t mouse = (mouse_ps2_t)p;
        var packet = new uint8_t[] { 0x08, 0, 0 };

        if (x == 0 && y == 0 && z == 0 && b == mouse.b)
                return;

        if (mouse_scan == 0)
                return;

        mouse.x += x;
        mouse.y -= y;
        mouse.z -= z;
        if (mouse.mode == MOUSE_STREAM && (mouse.flags & MOUSE_ENABLE) != 0 && ((mouse_queue_end - mouse_queue_start) & 0xf) < 13)
        {
                mouse.b = b;
                // pclog("Send packet : %i %i\n", ps2_x, ps2_y);
                if (mouse.x > 255)
                        mouse.x = 255;
                if (mouse.x < -256)
                        mouse.x = -256;
                if (mouse.y > 255)
                        mouse.y = 255;
                if (mouse.y < -256)
                        mouse.y = -256;
                if (mouse.z < -8)
                        mouse.z = -8;
                if (mouse.z > 7)
                        mouse.z = 7;
                if (mouse.x < 0)
                        packet[0] |= 0x10;
                if (mouse.y < 0)
                        packet[0] |= 0x20;
                if ((mouse_buttons & 1) != 0)
                        packet[0] |= 1;
                if ((mouse_buttons & 2) != 0)
                        packet[0] |= 2;
                if ((mouse_buttons & 4) != 0 && (mouse_get_type(mouse_type) & MOUSE_TYPE_3BUTTON) != 0)
                        packet[0] |= 4;
                packet[1] = (uint8_t)(mouse.x & 0xff);
                packet[2] = (uint8_t)(mouse.y & 0xff);

                keyboard_at_adddata_mouse(packet[0]);
                keyboard_at_adddata_mouse(packet[1]);
                keyboard_at_adddata_mouse(packet[2]);
                if (mouse.intellimouse_mode != 0)
                        keyboard_at_adddata_mouse((uint8_t)mouse.z);

                mouse.x = mouse.y = mouse.z = 0;
        }
    }

    // pcem: mouse_ps2.c:214-230
    internal static object mouse_ps2_init()
    {
        // pcem: :215-216 — malloc + memset ; `new` zéro-initialise.
        mouse_ps2_t mouse = new mouse_ps2_t();

        //        mouse_poll  = mouse_ps2_poll;
        //        mouse_write = mouse_ps2_write;
        mouse.cd = 0;
        mouse.flags = 0;
        mouse.mode = MOUSE_STREAM;
        if (materiel.pb_94)
                mouse_ps2_init_materiel(mouse);

        // omitted: `if (romset == ROM_PC5086) upc_set_mouse(...)` (:224-225) — le PC5086 n'est
        //   pas dans le dépôt ; reste la branche du 8042.
        keyboard_at_set_mouse(mouse_ps2_write, mouse);

        Probe = mouse;
        return mouse;
    }

    // pcem: mouse_ps2.c:232-238
    internal static object mouse_intellimouse_init()
    {
        mouse_ps2_t mouse = (mouse_ps2_t)mouse_ps2_init();

        mouse.is_intellimouse = 1;

        return mouse;
    }

    // pcem: mouse_ps2.c:240-244
    internal static void mouse_ps2_close(object p)
    {
        // pcem: free(mouse) — le ramasse-miettes.
        Probe = null;
    }

    // pcem: mouse_ps2.c:246-248
    internal static readonly mouse_t mouse_ps2_2_button = new()
    {
        name = "2-button mouse (PS/2)", init = mouse_ps2_init, close = mouse_ps2_close, poll = mouse_ps2_poll,
        type = MOUSE_TYPE_PS2,
    };
    internal static readonly mouse_t mouse_intellimouse = new()
    {
        name = "Microsoft Intellimouse (PS/2)", init = mouse_intellimouse_init, close = mouse_ps2_close, poll = mouse_ps2_poll,
        type = MOUSE_TYPE_PS2 | MOUSE_TYPE_3BUTTON,
    };

    // iXtal26 (outillage, sans pendant C) — PS2.0 : la souris montée, pour la sonde de
    // iXtal26.Diff (pendant de h_mouse_probe dans l'oracle).
    internal static mouse_ps2_t? Probe;

    /// <summary>La sonde de la souris PS/2 : douze champs, dans l'ordre de h_mouse_probe.</summary>
    internal static void ProbeState(uint64_t[] o)
    {
        Array.Clear(o);
        var m = Probe;
        o[0] = (uint64_t)(uint32_t)mouse_scan | ((uint64_t)(m is null ? 0u : 1u) << 32);
        o[1] = (uint32_t)mouse_queue_start | ((uint64_t)(uint32_t)mouse_queue_end << 32);
        o[2] = Fnv(mouse_queue);
        if (m is null)
                return;
        o[3] = (uint32_t)m.mode | ((uint64_t)m.flags << 32) | ((uint64_t)m.resolution << 40) | ((uint64_t)m.sample_rate << 48);
        o[4] = m.command | ((uint64_t)(uint32_t)m.cd << 32);
        o[5] = (uint32_t)m.x | ((uint64_t)(uint32_t)m.y << 32);
        o[6] = (uint32_t)m.z | ((uint64_t)(uint32_t)m.b << 32);
        o[7] = (uint32_t)m.is_intellimouse | ((uint64_t)(uint32_t)m.intellimouse_mode << 32);
        o[8] = Fnv(m.last_data);
    }

    internal const int ProbeN = 9;

    private static uint64_t Fnv(ReadOnlySpan<uint8_t> p)
    {
        uint64_t h = 1469598103934665603UL;
        foreach (var v in p)
            h = (h ^ v) * 1099511628211UL;
        return h;
    }
}
