// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/joystick/joystick_tm_fcs.c
//         + includes/private/joystick/joystick_tm_fcs.h
// STATUS: transcribed — tm_fcs_init/close/read/write/read_axis/a0_over, joystick_tm_fcs.
//
// G10.1 (PLAN-G10.md). Le ThrustMaster Flight Control System : deux axes, quatre boutons, et
// un chapeau que la vraie manette code en résistance sur l'axe 3 du port.

#pragma warning disable CS8981

namespace iXtal26.Joystick;

internal static partial class joystick_tm_fcs_c
{
    // pcem: joystick_tm_fcs.c:9
    private static object? tm_fcs_init() { return null; }

    // pcem: joystick_tm_fcs.c:11
    private static void tm_fcs_close(object? p) { }

    // pcem: joystick_tm_fcs.c:13-28
    private static uint8_t tm_fcs_read(object? p)
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
        }

        return ret;
    }

    // pcem: joystick_tm_fcs.c:30
    private static void tm_fcs_write(object? p) { }

    // pcem: joystick_tm_fcs.c:32-57
    // pcem bug, fixed in hardware mode: PB-103 — 315° (le haut-gauche d'un chapeau à huit
    //   directions) ne tombe dans aucun cas et rend le 0 final, le code du chapeau EN BAS.
    private static int tm_fcs_read_axis(object? p, int axis)
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
                if (plat_joystick.joystick_state[0].pov[0] == -1)
                        return 32767;
                if (materiel.pb_103)
                        if (tm_haut_materiel())
                                return -32768;
                if (plat_joystick.joystick_state[0].pov[0] > 315 || plat_joystick.joystick_state[0].pov[0] < 45)
                        return -32768;
                if (plat_joystick.joystick_state[0].pov[0] >= 45 && plat_joystick.joystick_state[0].pov[0] < 135)
                        return -16384;
                if (plat_joystick.joystick_state[0].pov[0] >= 135 && plat_joystick.joystick_state[0].pov[0] < 225)
                        return 0;
                if (plat_joystick.joystick_state[0].pov[0] >= 225 && plat_joystick.joystick_state[0].pov[0] < 315)
                        return 16384;
                return 0;
        }
        return 0;
    }

    // pcem: joystick_tm_fcs.c:59
    private static void tm_fcs_a0_over(object? p) { }

    // pcem: joystick_tm_fcs.c:61-74
    internal static readonly joystick_if_t joystick_tm_fcs = new()
    {
        name = "Thrustmaster Flight Control System",
        init = tm_fcs_init,
        close = tm_fcs_close,
        read = tm_fcs_read,
        write = tm_fcs_write,
        read_axis = tm_fcs_read_axis,
        a0_over = tm_fcs_a0_over,
        max_joysticks = 1,
        axis_count = 2,
        button_count = 4,
        pov_count = 1,
        axis_names = joystick_if_t.names(8, "X axis", "Y axis"),
        button_names = joystick_if_t.names(32, "Button 1", "Button 2", "Button 3", "Button 4"),
        pov_names = joystick_if_t.names(4, "POV"),
    };
}
