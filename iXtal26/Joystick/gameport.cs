// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/joystick/gameport.c + pcem-dev/src/joystick/joystick_standard.c
//         + includes/private/joystick/gameport.h, joystick_standard.h
//         + includes/private/plat-joystick.h (joystick_t, joystick_state)
// STATUS: transcribed — gameport.c en entier (gameport_t, gameport_time, gameport_write,
//         gameport_read, gameport_timer_over, gameport_init(_common), gameport_201_init,
//         gameport_update_joystick_type, gameport_close, les deux device_t, les
//         joystick_get_*, joystick_list et ses sept types) et joystick_standard.c en entier
//         (les quatre joystick_if_t). Les trois autres types (G10.1) : joystick_ch_flightstick_pro.cs,
//         joystick_sw_pad.cs, joystick_tm_fcs.cs.
//
// SANS MANETTE, CE QUE VOIT L'INVITÉ. joystick_state reste à zéro (aucun
// plat_joystick_nr), donc JOYSTICK_PRESENT(n) est faux pour tout n :
// joystick_standard_read rend 0xf0 (aucun bouton), read_axis rend AXIS_NOT_PRESENT
// pour les quatre axes, et gameport_time DÉSARME chaque chronomètre au lieu de le
// lancer. Une écriture en 0x201 pose donc les quatre bits d'axe (state |= 0x0f),
// que plus rien ne retombe : la lecture rend 0xff, comme PCem sans manette branchée.
// Une manette se branche par la section [Joysticks] du .cfg et joystick_poll (l'hôte,
// Host/SdlJoystick.cs), ou par l'injection de l'outil de vérification (--joy-at).

// CS8981 : `gameport` n'a que des minuscules ASCII — le nom de l'unité C, comme ide.cs et rom.cs.
#pragma warning disable CS8981
#pragma warning disable CS8600, CS8602, CS8604

using iXtal26.PluginApi;
using static iXtal26.Cpu.x86;
using static iXtal26.io;
using static iXtal26.timer;

namespace iXtal26.Joystick;

internal delegate object? joystick_init_fn();
internal delegate void joystick_close_fn(object? p);
internal delegate uint8_t joystick_read_fn(object? p);
internal delegate void joystick_write_fn(object? p);
internal delegate int joystick_read_axis_fn(object? p, int axis);
internal delegate void joystick_a0_over_fn(object? p);

// pcem: gameport.h:6-19
// DEVIATION: `char name[80]`, `char axis_names[8][32]`… deviennent des string ;
//   les tableaux de noms gardent leur nombre d'entrées (8, 32, 4), les entrées que
//   l'initialiseur C laisse à zéro valent "" (chaîne vide du C).
internal sealed class joystick_if_t
{
    internal string name = "";
    internal joystick_init_fn init = null!;
    internal joystick_close_fn close = null!;
    internal joystick_read_fn read = null!;
    internal joystick_write_fn write = null!;
    internal joystick_read_axis_fn read_axis = null!;
    internal joystick_a0_over_fn a0_over = null!;
    internal int axis_count, button_count, pov_count;
    internal int max_joysticks;
    internal string[] axis_names = names(8);
    internal string[] button_names = names(32);
    internal string[] pov_names = names(4);

    internal static string[] names(int n, params string[] first)
    {
        string[] r = new string[n];
        for (int i = 0; i < n; i++)
                r[i] = i < first.Length ? first[i] : "";
        return r;
    }
}

// pcem: plat-joystick.h:45-54
internal sealed class joystick_t
{
    internal int[] axis = new int[8];
    internal int[] button = new int[32];
    internal int[] pov = new int[4];

    internal int plat_joystick_nr;
    internal int[] axis_mapping = new int[8];
    internal int[] button_mapping = new int[32];
    internal int[,] pov_mapping = new int[4, 2];
}

// pcem: gameport.c:41-45
internal sealed class gameport_axis_t
{
    internal pc_timer_t timer = new();
    internal int axis_nr;
    internal gameport_t gameport = null!;
}

// pcem: gameport.c:47-54
internal sealed class gameport_t
{
    internal uint8_t state;

    internal gameport_axis_t[] axis = { new(), new(), new(), new() };

    internal joystick_if_t joystick = null!;
    internal object? joystick_dat;
}

// Le côté hôte de plat-joystick.h, réduit à ce que lisent gameport.c et
// joystick_standard.c.
internal static partial class plat_joystick
{
    // pcem: plat-joystick.h:56-57, wx-sdl2-joystick.c:10 — rempli par loadconfig (pc.c:786-805 :
    //   plat_joystick_nr et les correspondances) et par joystick_poll (l'hôte), ou par l'injection
    //   de l'outil de vérification (iXtal26.Diff --joy-at), qui pose directement axes, boutons et
    //   chapeau des deux côtés.
    internal const int MAX_JOYSTICKS = 4;
    internal static readonly joystick_t[] joystick_state = { new(), new(), new(), new() };

    // pcem: plat-joystick.h:42-43 — une correspondance d'axe peut viser l'axe X ou Y d'un chapeau.
    internal const int POV_X = unchecked((int)0x80000000);
    internal const int POV_Y = 0x40000000;

    // pcem: plat-joystick.h:37
    internal const int MAX_PLAT_JOYSTICKS = 8;

    // pcem: pc.c:493 — joystick_poll(), posé par l'hôte (Host/SdlJoystick.cs, wx-sdl2-joystick.c).
    //   Nul hors de l'hôte : l'outil de vérification et les tests n'interrogent aucune manette.
    internal static Action? joystick_poll;

    // pcem: plat-joystick.h:59
    internal static bool JOYSTICK_PRESENT(int n) => joystick_state[n].plat_joystick_nr != 0;
}

// Classe conteneur suffixée `_c` (comme ppi_c, model_c) : les globales
// joystick_standard* ne peuvent pas porter le nom de la classe (CS0542). Classe
// distincte de `gameport` aussi pour l'ordre d'initialisation : joystick_list
// (gameport.c:17-19) doit voir ces objets déjà construits.
internal static partial class joystick_standard_c
{
    // pcem: joystick_standard.c:9
    private static object? joystick_standard_init() { return null; }

    // pcem: joystick_standard.c:11
    private static void joystick_standard_close(object? p) { }

    // pcem: joystick_standard.c:13-30
    private static uint8_t joystick_standard_read(object? p)
    {
        uint8_t ret = 0xf0;

        if (plat_joystick.JOYSTICK_PRESENT(0))
        {
                if (plat_joystick.joystick_state[0].button[0] != 0)
                        ret &= unchecked((uint8_t)~0x10);
                if (plat_joystick.joystick_state[0].button[1] != 0)
                        ret &= unchecked((uint8_t)~0x20);
        }
        if (plat_joystick.JOYSTICK_PRESENT(1))
        {
                if (plat_joystick.joystick_state[1].button[0] != 0)
                        ret &= unchecked((uint8_t)~0x40);
                if (plat_joystick.joystick_state[1].button[1] != 0)
                        ret &= unchecked((uint8_t)~0x80);
        }

        return ret;
    }

    // pcem: joystick_standard.c:32-47
    private static uint8_t joystick_standard_read_4button(object? p)
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

    // pcem: joystick_standard.c:49
    private static void joystick_standard_write(object? p) { }

    // pcem: joystick_standard.c:51-71
    private static int joystick_standard_read_axis(object? p, int axis)
    {
        switch (axis)
        {
        case 0:
                if (!plat_joystick.JOYSTICK_PRESENT(0))
                        return gameport.AXIS_NOT_PRESENT;
                return plat_joystick.joystick_state[0].axis[0];
        case 1:
                if (!plat_joystick.JOYSTICK_PRESENT(0))
                        return gameport.AXIS_NOT_PRESENT;
                return plat_joystick.joystick_state[0].axis[1];
        case 2:
                if (!plat_joystick.JOYSTICK_PRESENT(1))
                        return gameport.AXIS_NOT_PRESENT;
                return plat_joystick.joystick_state[1].axis[0];
        case 3:
                if (!plat_joystick.JOYSTICK_PRESENT(1))
                        return gameport.AXIS_NOT_PRESENT;
                return plat_joystick.joystick_state[1].axis[1];
        }
        return 0;
    }

    // pcem: joystick_standard.c:73-88
    private static int joystick_standard_read_axis_4button(object? p, int axis)
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
                return 0;
        }
        return 0;
    }

    // pcem: joystick_standard.c:89-104
    private static int joystick_standard_read_axis_6button(object? p, int axis)
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
                return plat_joystick.joystick_state[0].button[4] != 0 ? -32767 : 32768;
        case 3:
                return plat_joystick.joystick_state[0].button[5] != 0 ? -32767 : 32768;
        }
        return 0;
    }

    // pcem: joystick_standard.c:105-128
    private static int joystick_standard_read_axis_8button(object? p, int axis)
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
                if (plat_joystick.joystick_state[0].button[4] != 0)
                        return -32767;
                if (plat_joystick.joystick_state[0].button[6] != 0)
                        return 32768;
                return 0;
        case 3:
                if (plat_joystick.joystick_state[0].button[5] != 0)
                        return -32767;
                if (plat_joystick.joystick_state[0].button[7] != 0)
                        return 32768;
                return 0;
        }
        return 0;
    }

    // pcem: joystick_standard.c:130
    private static void joystick_standard_a0_over(object? p) { }

    // pcem: joystick_standard.c:132-143
    internal static readonly joystick_if_t joystick_standard = new()
    {
        name = "Standard 2-button joystick(s)",
        init = joystick_standard_init,
        close = joystick_standard_close,
        read = joystick_standard_read,
        write = joystick_standard_write,
        read_axis = joystick_standard_read_axis,
        a0_over = joystick_standard_a0_over,
        max_joysticks = 2,
        axis_count = 2,
        button_count = 2,
        axis_names = joystick_if_t.names(8, "X axis", "Y axis"),
        button_names = joystick_if_t.names(32, "Button 1", "Button 2"),
    };
    // pcem: joystick_standard.c:144-155
    internal static readonly joystick_if_t joystick_standard_4button = new()
    {
        name = "Standard 4-button joystick",
        init = joystick_standard_init,
        close = joystick_standard_close,
        read = joystick_standard_read_4button,
        write = joystick_standard_write,
        read_axis = joystick_standard_read_axis_4button,
        a0_over = joystick_standard_a0_over,
        max_joysticks = 1,
        axis_count = 2,
        button_count = 4,
        axis_names = joystick_if_t.names(8, "X axis", "Y axis"),
        button_names = joystick_if_t.names(32, "Button 1", "Button 2", "Button 3", "Button 4"),
    };
    // pcem: joystick_standard.c:156-168
    internal static readonly joystick_if_t joystick_standard_6button = new()
    {
        name = "Standard 6-button joystick",
        init = joystick_standard_init,
        close = joystick_standard_close,
        read = joystick_standard_read_4button,
        write = joystick_standard_write,
        read_axis = joystick_standard_read_axis_6button,
        a0_over = joystick_standard_a0_over,
        max_joysticks = 1,
        axis_count = 2,
        button_count = 6,
        axis_names = joystick_if_t.names(8, "X axis", "Y axis"),
        button_names = joystick_if_t.names(32, "Button 1", "Button 2", "Button 3", "Button 4", "Button 5", "Button 6"),
    };
    // pcem: joystick_standard.c:169-181
    internal static readonly joystick_if_t joystick_standard_8button = new()
    {
        name = "Standard 8-button joystick",
        init = joystick_standard_init,
        close = joystick_standard_close,
        read = joystick_standard_read_4button,
        write = joystick_standard_write,
        read_axis = joystick_standard_read_axis_8button,
        a0_over = joystick_standard_a0_over,
        max_joysticks = 1,
        axis_count = 2,
        button_count = 8,
        axis_names = joystick_if_t.names(8, "X axis", "Y axis"),
        button_names = joystick_if_t.names(32, "Button 1", "Button 2", "Button 3", "Button 4", "Button 5", "Button 6",
                                           "Button 7", "Button 8"),
    };
}

internal static partial class gameport
{
    // pcem: gameport.h:33
    internal const int AXIS_NOT_PRESENT = -99999;

    // pcem: gameport.c:15
    internal static int joystick_type;

    // pcem: gameport.c:17-19
    private static readonly joystick_if_t?[] joystick_list =
    {
        joystick_standard_c.joystick_standard, joystick_standard_c.joystick_standard_4button,
        joystick_standard_c.joystick_standard_6button, joystick_standard_c.joystick_standard_8button,
        joystick_ch_flightstick_pro_c.joystick_ch_flightstick_pro, joystick_sw_pad_c.joystick_sw_pad,
        joystick_tm_fcs_c.joystick_tm_fcs, null,
    };

    /// <summary>iXtal26 — le nombre de types de joystick_list (sept), sans le NULL terminal : la
    /// borne de joystick_type, que PCem ne vérifie pas (PB-93, pc.cs loadconfig ; iXtal26.Diff
    /// --joystick-type).</summary>
    internal static int joystick_type_count() => joystick_list.Length - 1;

    // pcem: gameport.c:21-25
    internal static string? joystick_get_name(int joystick)
    {
        if (joystick_list[joystick] == null)
                return null;
        return joystick_list[joystick]!.name;
    }

    // pcem: gameport.c:27
    internal static int joystick_get_max_joysticks(int joystick) { return joystick_list[joystick]!.max_joysticks; }

    // pcem: gameport.c:29
    internal static int joystick_get_axis_count(int joystick) { return joystick_list[joystick]!.axis_count; }

    // pcem: gameport.c:31
    internal static int joystick_get_button_count(int joystick) { return joystick_list[joystick]!.button_count; }

    // pcem: gameport.c:33
    internal static int joystick_get_pov_count(int joystick) { return joystick_list[joystick]!.pov_count; }

    // pcem: gameport.c:35
    internal static string joystick_get_axis_name(int joystick, int id) { return joystick_list[joystick]!.axis_names[id]; }

    // pcem: gameport.c:37
    internal static string joystick_get_button_name(int joystick, int id) { return joystick_list[joystick]!.button_names[id]; }

    // pcem: gameport.c:39
    internal static string joystick_get_pov_name(int joystick, int id) { return joystick_list[joystick]!.pov_names[id]; }

    // pcem: gameport.c:56
    private static gameport_t? gameport_global = null;

    // pcem: gameport.c:58-67
    private static void gameport_time(gameport_t gameport, int nr, int axis)
    {
        if (axis == AXIS_NOT_PRESENT)
        {
                timer_disable(gameport.axis[nr].timer);
        }
        else
        {
                axis += 32768;
                axis = (axis * 100) / 65; /*Axis now in ohms*/
                axis = (axis * 11) / 1000;
                // (uint64_t) : la conversion implicite int -> uint64_t du C.
                timer_set_delay_u64(gameport.axis[nr].timer, TIMER_USEC * unchecked((uint64_t)(axis + 24))); /*max = 11.115 ms*/
        }
    }

    // pcem: gameport.c:69-83
    internal static void gameport_write(uint16_t addr, uint8_t val, object p)
    {
        gameport_t gameport = (gameport_t)p;

        gameport.state |= 0x0f;

        gameport_time(gameport, 0, gameport.joystick.read_axis(gameport.joystick_dat, 0));
        gameport_time(gameport, 1, gameport.joystick.read_axis(gameport.joystick_dat, 1));
        gameport_time(gameport, 2, gameport.joystick.read_axis(gameport.joystick_dat, 2));
        gameport_time(gameport, 3, gameport.joystick.read_axis(gameport.joystick_dat, 3));

        gameport.joystick.write(gameport.joystick_dat);

        cycles -= ISA_CYCLES(8);
    }

    // pcem: gameport.c:85-100
    internal static uint8_t gameport_read(uint16_t addr, object p)
    {
        gameport_t gameport = (gameport_t)p;
        uint8_t ret;

        ret = (uint8_t)(gameport.state | gameport.joystick.read(gameport.joystick_dat)); // 0xf0;

        cycles -= ISA_CYCLES(8);

        return ret;
    }

    // pcem: gameport.c:102-110
    internal static void gameport_timer_over(object? p)
    {
        gameport_axis_t axis = (gameport_axis_t)p;
        gameport_t gameport = axis.gameport;

        gameport.state &= (uint8_t)~(1 << axis.axis_nr);

        if (axis == gameport.axis[0])
                gameport.joystick.a0_over(gameport.joystick_dat);
    }

    // pcem: gameport.c:112-138
    internal static gameport_t gameport_init_common()
    {
        gameport_t gameport = new gameport_t();
        // pcem: gameport.c:115 — memset(gameport, 0, …) ; `new` zéro-initialise.

        gameport.axis[0].gameport = gameport;
        gameport.axis[1].gameport = gameport;
        gameport.axis[2].gameport = gameport;
        gameport.axis[3].gameport = gameport;

        gameport.axis[0].axis_nr = 0;
        gameport.axis[1].axis_nr = 1;
        gameport.axis[2].axis_nr = 2;
        gameport.axis[3].axis_nr = 3;

        timer_add(gameport.axis[0].timer, gameport_timer_over, gameport.axis[0], 0);
        timer_add(gameport.axis[1].timer, gameport_timer_over, gameport.axis[1], 0);
        timer_add(gameport.axis[2].timer, gameport_timer_over, gameport.axis[2], 0);
        timer_add(gameport.axis[3].timer, gameport_timer_over, gameport.axis[3], 0);

        gameport.joystick = joystick_list[joystick_type]!;
        gameport.joystick_dat = gameport.joystick.init();

        gameport_global = gameport;

        return gameport;
    }

    // pcem: gameport.c:140-148
    internal static void gameport_update_joystick_type()
    {
        gameport_t? gameport = gameport_global;

        if (gameport != null)
        {
                gameport.joystick.close(gameport.joystick_dat);
                gameport.joystick = joystick_list[joystick_type]!;
                gameport.joystick_dat = gameport.joystick.init();
        }
    }

    // pcem: gameport.c:150-156
    internal static object gameport_init()
    {
        gameport_t gameport = gameport_init_common();

        io_sethandler(0x0200, 0x0008, gameport_read, null, null, gameport_write, null, null, gameport);

        return gameport;
    }

    // pcem: gameport.c:158-164
    internal static object gameport_201_init()
    {
        gameport_t gameport = gameport_init_common();

        io_sethandler(0x0201, 0x0001, gameport_read, null, null, gameport_write, null, null, gameport);

        return gameport;
    }

    // pcem: gameport.c:166-174
    internal static void gameport_close(object? p)
    {
        gameport_t gameport = (gameport_t)p;

        gameport.joystick.close(gameport.joystick_dat);

        gameport_global = null;

        // omitted: free(gameport) — libération manuelle, sans objet sous GC.
    }

    // pcem: gameport.c:176
    internal static readonly device_t gameport_device =
        new("Game port", 0, gameport_init, gameport_close, null, null, null, null, null);

    // pcem: gameport.c:178
    internal static readonly device_t gameport_201_device =
        new("Game port (port 201h only)", 0, gameport_201_init, gameport_close, null, null, null, null, null);

    // pcem: cpu.h:163
    private static int ISA_CYCLES(int x) => x * Cpu.cpu_c.isa_cycles;
}
