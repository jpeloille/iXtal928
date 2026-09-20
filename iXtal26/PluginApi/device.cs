// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/plugin-api/device.c (+ includes/public/pcem/devices.h)
// STATUS: partial — device_t, device_config_t réduit, devices[]/device_priv[],
//         device_init/device_add/device_close_all/device_available/
//         device_speed_changed/device_force_redraw/device_add_status_info,
//         device_get_config_int/string, pcem_add_device ; omis : les registres
//         MODEL/VIDEO_CARD/SOUND_CARD/HDD_CONTROLLER/NETWORK_CARD/LPT_DEVICE, la
//         famille model_get_config_* et l'API plugin pcem_*_get_config_*.

// CS8600/CS8602/CS8604 : l'analyse de nullabilité de C# ne suit pas l'état des
// éléments de tableau. `if (devices[c] != null)` — le test de PCem, une ligne
// plus haut — ne la renseigne donc pas, et `device_priv[c]` reste `object?` là
// où le delegate déclare `object` (le C y passe un `void *` qui vaut NULL tant
// que le périphérique n'a pas d'état privé). Même situation que io.cs : les
// trois sont neutralisés ici plutôt qu'avec un `!` par site, qui réécrirait
// chaque ligne de dispatch.
#pragma warning disable CS8600, CS8602, CS8604

using System.Text;
using iXtal26.Diag;

namespace iXtal26.PluginApi;

// pcem: devices.h:39-44 — les pointeurs de fonction de device_t.
internal delegate object? device_init_fn();
internal delegate void device_close_fn(object p);
internal delegate int device_available_fn();
internal delegate void device_speed_changed_fn(object p);
internal delegate void device_force_redraw_fn(object p);

// pcem: device.c:26 — void (*_sound_speed_changed)(void)
internal delegate void sound_speed_changed_t();

// DEVIATION: `char *s` est un tampon de sortie que les handlers concatènent
//   (sound_sb_dsp.c:1273). Sans pointeur, la contrepartie est un StringBuilder ;
//   `max_len` est conservé à sa place dans la signature.
internal delegate void device_add_status_info_fn(StringBuilder s, int max_len, object p);

// pcem: devices.h:27-34
// omitted: `description` (libellé du dialogue de configuration) et `selection[30]`
//   / device_config_selection_t (devices.h:22-25) — le PC 5150 n'a pas de dialogue
//   de configuration. Restent les champs lus par device_get_config_int/string.
internal sealed class device_config_t
{
    internal string name = "";
    internal int type;
    internal string default_string = "";
    internal int default_int;
}

// pcem: devices.h:36-46
internal sealed class device_t
{
    internal string name;
    internal uint32_t flags;
    internal device_init_fn? init;
    internal device_close_fn? close;
    internal device_available_fn? available;
    internal device_speed_changed_fn? speed_changed;
    internal device_force_redraw_fn? force_redraw;
    internal device_add_status_info_fn? add_status_info;
    internal device_config_t[]? config;

    internal device_t(string name, uint32_t flags, device_init_fn? init, device_close_fn? close,
                      device_available_fn? available, device_speed_changed_fn? speed_changed,
                      device_force_redraw_fn? force_redraw, device_add_status_info_fn? add_status_info,
                      device_config_t[]? config)
    {
        this.name = name;
        this.flags = flags;
        this.init = init;
        this.close = close;
        this.available = available;
        this.speed_changed = speed_changed;
        this.force_redraw = force_redraw;
        this.add_status_info = add_status_info;
        this.config = config;
    }
}

internal static partial class device
{
    // Devices should never be more then DEV_MAX.
    // pcem: defines.h:4-5
    internal const int DEV_MAX = 256;

    // pcem: devices.h:8-12
    internal const int CONFIG_STRING = 0;
    internal const int CONFIG_INT = 1;
    internal const int CONFIG_BINARY = 2;
    internal const int CONFIG_SELECTION = 3;
    internal const int CONFIG_MIDI = 4;

    // pcem: devices.h:14-20
    /*Device does not currently work correctly and will be disabled in a release build*/
    internal const uint32_t DEVICE_NOT_WORKING = 1;
    // omitted: DEVICE_AT, DEVICE_MCA, DEVICE_PCI, DEVICE_PS1 (devices.h:16-19) —
    //   drapeaux de machines postérieures au 5150.

    // pcem: config.h:8
    internal const int CFG_MACHINE = 0;

    // pcem: device.c:14-24
    internal static int model;
    internal static device_t?[] devices = new device_t?[DEV_MAX];
    internal static device_t? current_device;
    internal static string? current_device_name = null;
    internal static object?[] device_priv = new object?[DEV_MAX];
    // omitted: device.c:7-12 — re-déclarations `extern` de ce que le fichier
    //   définit lui-même, sans contrepartie C#.
    // omitted: models[], video_cards[], sound_cards[], hdd_controllers[],
    //   network_cards[], lpt_devices[] (device.c:16-21) et les types SOUND_CARD,
    //   video_timings_t, VIDEO_CARD, MODEL, HDD_CONTROLLER, NETWORK_CARD,
    //   lpt_device_t, LPT_DEVICE (devices.h:48-118) — registres enfichables.
    // pcem: device.c:26 — void (*_sound_speed_changed)(void). Crochet posé par
    // l'IHM chez PCem (wx-sdl2.c:450, qt-sdl2.c:460) ; ici par pc.initpc(), et
    // INCONDITIONNELLEMENT. C'est lui qui pose sound_poll_latch : laissé nul, le
    // latch vaut 0, timer_advance_u64(t, 0) ne fait pas avancer l'échéance et
    // timer_process() boucle à l'infini sur le chronomètre du son. L'appel de
    // device.c:68 n'est pas testé — un crochet oublié doit se voir au premier
    // setpitclock(), pas se taire.
    internal static sound_speed_changed_t? _sound_speed_changed;
    // omitted: model_getdevice (device.c:28), model_get_config_int/string
    //   (device.c:118-152), model_count et pcem_add_model (device.c:212-224) —
    //   dépendent de MODEL, donc de CPU ; arrivent avec le modèle xt.
    // omitted: video_count/pcem_add_video, lpt_count/pcem_add_lpt,
    //   sound_count/pcem_add_sound, hdd_controller_count/pcem_add_hddcontroller,
    //   network_card_count/pcem_add_networkcard (device.c:226-294).
    // omitted: pcem_device_get_config_int/string, pcem_model_get_config_int/string
    //   (device.c:154-210) — surface de l'API plugin, sans appelant dans le cœur.

    // DEVIATION: fatal() appartient à logging.h/logging.c, qui n'est pas encore
    //   transcrit. Déclaré ici pour que device.c:304 et device.c:312 aient une
    //   contrepartie. Comme dans mem.cs et timer.cs, il compte et n'interrompt pas.
    private static void fatal(string format)
    {
        Counters.n_fatal++;
        Console.Error.Write("iXtal26 FATAL: " + format);
    }

    // DEVIATION: config.c n'est pas transcrit — le PC 5150 n'a ni fichier .cfg ni
    //   dialogue de configuration. Ces deux fonctions rendent donc toujours `def`,
    //   ce que config.c:285-291 et 324-330 font déjà quand la section ou l'entrée
    //   manque, c'est-à-dire toujours ici.
    // omitted: la lecture du fichier .cfg (config.c) et CFG_GLOBAL.
    private static int config_get_int(int is_global, string head, string name, int def) => def;

    private static string config_get_string(int is_global, string head, string name, string def) => def;

    // pcem: device.c:30
    internal static void device_init() { Array.Clear(devices); }

    // pcem: device.c:32
    internal static void device_add(device_t d) { pcem_add_device(d); }

    // pcem: device.c:34-44
    internal static void device_close_all()
    {
        int c;

        for (c = 0; c < 256; c++)
        {
            if (devices[c] != null)
            {
                if (devices[c].close != null)
                    devices[c].close(device_priv[c]);
                devices[c] = null;
                device_priv[c] = null;
            }
        }
    }

    // pcem: device.c:46-55
    internal static int device_available(device_t d)
    {
#if RELEASE_BUILD
        if ((d.flags & DEVICE_NOT_WORKING) != 0)
            return 0;
#endif
        if (d.available != null)
            return d.available();

        return 1;
    }

    // pcem: device.c:57-69
    internal static void device_speed_changed()
    {
        int c;

        for (c = 0; c < 256; c++)
        {
            if (devices[c] != null)
            {
                if (devices[c].speed_changed != null)
                {
                    devices[c].speed_changed(device_priv[c]);
                }
            }
        }

        // pcem: device.c:68
        _sound_speed_changed();
    }

    // pcem: device.c:71-81
    internal static void device_force_redraw()
    {
        int c;

        for (c = 0; c < 256; c++)
        {
            if (devices[c] != null)
            {
                if (devices[c].force_redraw != null)
                {
                    devices[c].force_redraw(device_priv[c]);
                }
            }
        }
    }

    // pcem: device.c:83-92
    internal static void device_add_status_info(StringBuilder s, int max_len)
    {
        int c;

        for (c = 0; c < 256; c++)
        {
            if (devices[c] != null)
            {
                if (devices[c].add_status_info != null)
                    devices[c].add_status_info(s, max_len, device_priv[c]);
            }
        }
    }

    // pcem: device.c:94-104
    internal static int device_get_config_int(string s)
    {
        device_config_t[] config = current_device.config;
        int i = 0;

        while (config[i].type != -1)
        {
            if (string.CompareOrdinal(s, config[i].name) == 0)
                return config_get_int(CFG_MACHINE, current_device.name, s, config[i].default_int);

            i++;
        }
        return 0;
    }

    // pcem: device.c:106-116
    internal static string? device_get_config_string(string s)
    {
        device_config_t[] config = current_device.config;
        int i = 0;

        while (config[i].type != -1)
        {
            if (string.CompareOrdinal(s, config[i].name) == 0)
                return config_get_string(CFG_MACHINE, current_device.name, s, config[i].default_string);

            i++;
        }
        return null;
    }

    // pcem: device.c:296-318
    internal static void pcem_add_device(device_t d)
    {
        int c = 0;
        object? priv = null;

        // pcem bug, reproduced: `devices[c]` est lu AVANT la borne `c < 256`, donc
        //   devices[256] est déréférencé hors tableau quand les 256 fentes sont
        //   prises. Le C lit la globale voisine, le C# lève.
        while (devices[c] != null && c < 256)
            c++;

        if (c >= 256)
            fatal("device_add : too many devices\n");

        current_device = d;
        current_device_name = d.name;

        if (d.init != null)
        {
            priv = d.init();
            if (priv == null)
                fatal("device_add : device init failed\n");
        }

        devices[c] = d;
        device_priv[c] = priv;
        current_device_name = null;
    }
}
