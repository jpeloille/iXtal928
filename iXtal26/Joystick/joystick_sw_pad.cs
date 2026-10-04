// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/joystick/joystick_sw_pad.c
//         + includes/private/joystick/joystick_sw_pad.h
// STATUS: transcribed — sw_data, sw_timer_over, sw_trigger_timer_over, sw_parity, sw_init,
//         sw_close, sw_read, sw_write, sw_read_axis, sw_a0_over, joystick_sw_pad.
//
// G10.1 (PLAN-G10.md). Le Microsoft SideWinder Game Pad : une manette NUMÉRIQUE. Une écriture en
// 201h lance un paquet, envoyé bit à bit sur les lignes de boutons au rythme de deux chronomètres
// (l'horloge sur le bit 4) ; jusqu'à quatre manettes en chaîne, en mode A (un bit par coup
// d'horloge) ou B (trois), alternés ; le paquet d'identification si l'écriture suit de peu la
// chute de l'axe 0. Les notes de PCem en tête de joystick_sw_pad.c (:1-21) disent le protocole.

#pragma warning disable CS8981

using static iXtal26.timer;

namespace iXtal26.Joystick;

// pcem: joystick_sw_pad.c:31-40
internal sealed class sw_data
{
    internal pc_timer_t poll_timer = new();
    internal int poll_left;
    internal int poll_clock;
    internal uint64_t poll_data;
    internal int poll_mode;

    internal pc_timer_t trigger_timer = new();
    internal int data_mode;
}

internal static class joystick_sw_pad_c
{
    // pcem: joystick_sw_pad.c:42-56
    private static void sw_timer_over(object? p)
    {
        sw_data sw = (sw_data)p!;

        sw.poll_clock = sw.poll_clock == 0 ? 1 : 0;

        if (sw.poll_clock != 0)
        {
                sw.poll_data >>= (sw.poll_mode != 0 ? 3 : 1);
                sw.poll_left--;
        }

        if (sw.poll_left == 1 && sw.poll_clock == 0)
                timer_advance_u64(sw.poll_timer, TIMER_USEC * 160);
        else if (sw.poll_left != 0)
                timer_set_delay_u64(sw.poll_timer, TIMER_USEC * 5);
    }

    // pcem: joystick_sw_pad.c:58
    private static void sw_trigger_timer_over(object? p) { }

    // pcem: joystick_sw_pad.c:60-69 — `uint16_t data` : l'appelant passe un uint64_t, tronqué.
    private static int sw_parity(uint16_t data)
    {
        int bits_set = 0;

        while (data != 0)
        {
                bits_set++;
                data &= (uint16_t)(data - 1);
        }

        return bits_set & 1;
    }

    // pcem: joystick_sw_pad.c:71-79
    private static object? sw_init()
    {
        sw_data sw = new sw_data();
        // pcem: :72-73 — malloc + memset ; `new` zéro-initialise.

        timer_add(sw.poll_timer, sw_timer_over, sw, 0);
        timer_add(sw.trigger_timer, sw_trigger_timer_over, sw, 0);

        return sw;
    }

    // pcem: joystick_sw_pad.c:81-85
    // omitted: free(sw) — sous GC. Chez PCem, free laisse les deux chronomètres dans la liste :
    //   un changement de type à chaud depuis l'interface (gameport_update_joystick_type) les
    //   ferait battre sur une mémoire libérée. L'invité ne l'atteint pas, et iXtal n'a pas ce
    //   changement à chaud (pcem bug, not reproduced: PB-105, sans action).
    private static void sw_close(object? p) { }

    // pcem: joystick_sw_pad.c:87-109
    private static uint8_t sw_read(object? p)
    {
        sw_data sw = (sw_data)p!;
        uint8_t temp = 0;

        if (!plat_joystick.JOYSTICK_PRESENT(0))
                return 0xff;

        if (timer_is_enabled(sw.poll_timer) != 0)
        {
                if (sw.poll_clock != 0)
                        temp |= 0x10;

                if (sw.poll_mode != 0)
                        temp |= (uint8_t)((sw.poll_data & 7) << 5);
                else
                {
                        temp |= (uint8_t)(((sw.poll_data & 1) << 5) | 0xc0);
                        if (sw.poll_left > 31 && (sw.poll_left & 1) == 0)
                                temp &= unchecked((uint8_t)~0x80);
                }
        }
        else
                temp |= 0xf0;

        return temp;
    }

    // pcem: joystick_sw_pad.c:111-179
    private static void sw_write(object? p)
    {
        sw_data sw = (sw_data)p!;
        // pcem: :113 — le uint32_t de timer_get_remaining_us dans un int.
        int time_since_last = unchecked((int)timer_get_remaining_us(sw.trigger_timer));

        if (!plat_joystick.JOYSTICK_PRESENT(0))
                return;

        if (sw.poll_left == 0)
        {
                sw.poll_clock = 1;
                timer_set_delay_u64(sw.poll_timer, TIMER_USEC * 40);

                if (time_since_last > 9900 && time_since_last < 9940)
                {
                        sw.poll_mode = 0;
                        sw.poll_left = 49;
                        sw.poll_data = 0x2400UL | (0x1830UL << 15) | (0x19b0UL << 30);
                }
                else
                {
                        int c;

                        sw.poll_mode = sw.data_mode;
                        sw.data_mode = sw.data_mode == 0 ? 1 : 0;

                        if (sw.poll_mode != 0)
                        {
                                sw.poll_left = 1;
                                sw.poll_data = 7;
                        }
                        else
                        {
                                sw.poll_left = 1;
                                sw.poll_data = 1;
                        }

                        for (c = 0; c < 4; c++)
                        {
                                uint64_t data = 0x3fff;
                                int b;

                                if (!plat_joystick.JOYSTICK_PRESENT(c))
                                        break;

                                if (plat_joystick.joystick_state[c].axis[1] < -16383)
                                        data &= unchecked((uint64_t)~1L);
                                if (plat_joystick.joystick_state[c].axis[1] > 16383)
                                        data &= unchecked((uint64_t)~2L);
                                if (plat_joystick.joystick_state[c].axis[0] > 16383)
                                        data &= unchecked((uint64_t)~4L);
                                if (plat_joystick.joystick_state[c].axis[0] < -16383)
                                        data &= unchecked((uint64_t)~8L);

                                // pcem: :152 — `~(1 << (b + 4))` est un int, étendu par le signe.
                                for (b = 0; b < 10; b++)
                                {
                                        if (plat_joystick.joystick_state[c].button[b] != 0)
                                                data &= unchecked((uint64_t)(long)~(1 << (b + 4)));
                                }

                                if (sw_parity(unchecked((uint16_t)data)) != 0)
                                        data |= 0x4000;

                                if (sw.poll_mode != 0)
                                {
                                        sw.poll_left += 5;
                                        sw.poll_data |= (data << (c * 15 + 3));
                                }
                                else
                                {
                                        sw.poll_left += 15;
                                        sw.poll_data |= (data << (c * 15 + 1));
                                }
                        }
                }
        }

        timer_disable(sw.trigger_timer);
    }

    // pcem: joystick_sw_pad.c:181-186
    private static int sw_read_axis(object? p, int axis)
    {
        if (!plat_joystick.JOYSTICK_PRESENT(0))
                return gameport.AXIS_NOT_PRESENT;

        return 0; /*No analogue support on Sidewinder game pad*/
    }

    // pcem: joystick_sw_pad.c:188-192
    private static void sw_a0_over(object? p)
    {
        sw_data sw = (sw_data)p!;

        timer_set_delay_u64(sw.trigger_timer, TIMER_USEC * 10000);
    }

    // pcem: joystick_sw_pad.c:194-205
    internal static readonly joystick_if_t joystick_sw_pad = new()
    {
        name = "Microsoft SideWinder Pad",
        init = sw_init,
        close = sw_close,
        read = sw_read,
        write = sw_write,
        read_axis = sw_read_axis,
        a0_over = sw_a0_over,
        max_joysticks = 4,
        axis_count = 2,
        button_count = 10,
        axis_names = joystick_if_t.names(8, "X axis", "Y axis"),
        button_names = joystick_if_t.names(32, "A", "B", "C", "X", "Y", "Z", "L", "R", "Start", "M"),
    };
}
