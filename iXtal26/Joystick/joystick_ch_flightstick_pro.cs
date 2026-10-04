// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/joystick/joystick_ch_flightstick_pro.c
//         + includes/private/joystick/joystick_ch_flightstick_pro.h
// STATUS: transcribed — ch_flightstick_pro_init/close/read/write/read_axis/a0_over,
//         joystick_ch_flightstick_pro.
//
// G10.1 (PLAN-G10.md). La CH Flightstick Pro : un manche à trois axes (le troisième, la
// manette des gaz, lu sur l'axe 3 du port), quatre boutons et un chapeau, que la vraie manette
// code en combinaisons de boutons.

#pragma warning disable CS8981

namespace iXtal26.Joystick;

internal static class joystick_ch_flightstick_pro_c
{
    // pcem: joystick_ch_flightstick_pro.c:9
    private static object? ch_flightstick_pro_init() { return null; }

    // pcem: joystick_ch_flightstick_pro.c:11
    private static void ch_flightstick_pro_close(object? p) { }

    // pcem: joystick_ch_flightstick_pro.c:13-38
    // pcem bug, reproduced: PB-103 — 315° (le haut-gauche d'un chapeau à huit directions) ne
    //   tombe dans aucun cas : « > 315 », puis « < 315 » ; le chapeau se lit centré.
    private static uint8_t ch_flightstick_pro_read(object? p)
    {
        uint8_t ret = 0xf0;

        if (plat_joystick.JOYSTICK_PRESENT(0))
        {
                if (plat_joystick.joystick_state[0].button[0] != 0)
                        ret &= unchecked((uint8_t)~0x10);
                if (plat_joystick.joystick_state[0].button[1] != 0)
                        ret &= unchecked((uint8_t)~0x20);
                if (plat_joystick.joystick_state[0].button[2] != 0)
                        ret &= unchecked((uint8_t)~0x40);
                if (plat_joystick.joystick_state[0].button[3] != 0)
                        ret &= unchecked((uint8_t)~0x80);
                if (plat_joystick.joystick_state[0].pov[0] != -1)
                {
                        if (plat_joystick.joystick_state[0].pov[0] > 315 || plat_joystick.joystick_state[0].pov[0] < 45)
                                ret &= unchecked((uint8_t)~0xf0);
                        else if (plat_joystick.joystick_state[0].pov[0] >= 45 && plat_joystick.joystick_state[0].pov[0] < 135)
                                ret &= unchecked((uint8_t)~0xb0);
                        else if (plat_joystick.joystick_state[0].pov[0] >= 135 && plat_joystick.joystick_state[0].pov[0] < 225)
                                ret &= unchecked((uint8_t)~0x70);
                        else if (plat_joystick.joystick_state[0].pov[0] >= 225 && plat_joystick.joystick_state[0].pov[0] < 315)
                                ret &= unchecked((uint8_t)~0x30);
                }
        }

        return ret;
    }

    // pcem: joystick_ch_flightstick_pro.c:40
    private static void ch_flightstick_pro_write(object? p) { }

    // pcem: joystick_ch_flightstick_pro.c:42-57
    private static int ch_flightstick_pro_read_axis(object? p, int axis)
    {
        if (!plat_joystick.JOYSTICK_PRESENT(0))
                return gameport.AXIS_NOT_PRESENT;

        switch (axis)
        {
        case 0:
                return plat_joystick.joystick_state[0].axis[0];
        case 1:
                return plat_joystick.joystick_state[0].axis[1];
        case 2:
                return 0;
        case 3:
                return plat_joystick.joystick_state[0].axis[2];
        }
        return 0;
    }

    // pcem: joystick_ch_flightstick_pro.c:59
    private static void ch_flightstick_pro_a0_over(object? p) { }

    // pcem: joystick_ch_flightstick_pro.c:61-74
    internal static readonly joystick_if_t joystick_ch_flightstick_pro = new()
    {
        name = "CH Flightstick Pro",
        init = ch_flightstick_pro_init,
        close = ch_flightstick_pro_close,
        read = ch_flightstick_pro_read,
        write = ch_flightstick_pro_write,
        read_axis = ch_flightstick_pro_read_axis,
        a0_over = ch_flightstick_pro_a0_over,
        max_joysticks = 1,
        axis_count = 3,
        button_count = 4,
        pov_count = 1,
        axis_names = joystick_if_t.names(8, "X axis", "Y axis", "Throttle"),
        button_names = joystick_if_t.names(32, "Button 1", "Button 2", "Button 3", "Button 4"),
        pov_names = joystick_if_t.names(4, "POV"),
    };
}
