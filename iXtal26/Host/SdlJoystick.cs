// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/wx-ui/wx-sdl2-joystick.c (joystick_init, joystick_close, joystick_get_axis,
//         joystick_poll) + includes/private/plat-joystick.h (plat_joystick_t)
// STATUS: host
//
// G10.1 (PLAN-G10.md). Les manettes de l'HÔTE vers le port jeu émulé, par SDL3 là où PCem passe
// par SDL2 : SDL_NumJoysticks et SDL_JoystickOpen(indice) deviennent SDL_GetJoysticks et
// SDL_OpenJoystick(identifiant), SDL_JoystickGet* deviennent SDL_GetJoystick*, SDL_JoystickUpdate
// devient SDL_UpdateJoysticks ; les valeurs du chapeau (SDL_HAT_*) sont celles de SDL2.
// joystick_poll lit les manettes à chaque tranche (pc.c:493) et les fait correspondre aux manettes
// émulées selon la section [Joysticks] du .cfg (pc.cs, load_joysticks), dont les numéros hors
// borne sont déjà ramenés au défaut (R9). --joystick-check, l'auto-contrôle : une manette
// VIRTUELLE de SDL3, sans matériel.

using System.Runtime.InteropServices;
using iXtal26.Joystick;
using iXtal26.PluginApi;
using SDL3;

namespace iXtal26.Host;

// pcem: plat-joystick.h:17-30 — le nom et l'identifiant d'un axe, d'un bouton ou d'un chapeau.
internal sealed class plat_joystick_ctl_t
{
    internal string name = "";
    internal int id;

    internal static plat_joystick_ctl_t[] make(int n)
    {
        var r = new plat_joystick_ctl_t[n];
        for (int i = 0; i < n; i++)
                r[i] = new plat_joystick_ctl_t();
        return r;
    }
}

// pcem: plat-joystick.h:10-35
internal sealed class plat_joystick_t
{
    internal string name = "";

    internal int[] a = new int[8];
    internal int[] b = new int[32];
    internal int[] p = new int[4];

    internal plat_joystick_ctl_t[] axis = plat_joystick_ctl_t.make(8);
    internal plat_joystick_ctl_t[] button = plat_joystick_ctl_t.make(32);
    internal plat_joystick_ctl_t[] pov = plat_joystick_ctl_t.make(4);

    internal int nr_axes;
    internal int nr_buttons;
    internal int nr_povs;
}

internal static class SdlJoystick
{
    // pcem: wx-sdl2-joystick.c:9, :12-13
    internal static int joysticks_present;
    internal static readonly plat_joystick_t[] plat_joystick_state =
        Enumerable.Range(0, plat_joystick.MAX_PLAT_JOYSTICKS).Select(_ => new plat_joystick_t()).ToArray();
    private static readonly IntPtr[] sdl_joy = new IntPtr[plat_joystick.MAX_PLAT_JOYSTICKS];

    // pcem: SDL2/SDL_joystick.h — les valeurs du chapeau, les mêmes en SDL3.
    private const int SDL_HAT_UP = 0x01, SDL_HAT_RIGHT = 0x02, SDL_HAT_DOWN = 0x04, SDL_HAT_LEFT = 0x08;
    private const int SDL_HAT_RIGHTUP = SDL_HAT_RIGHT | SDL_HAT_UP, SDL_HAT_RIGHTDOWN = SDL_HAT_RIGHT | SDL_HAT_DOWN;
    private const int SDL_HAT_LEFTUP = SDL_HAT_LEFT | SDL_HAT_UP, SDL_HAT_LEFTDOWN = SDL_HAT_LEFT | SDL_HAT_DOWN;

    private static void pclog(string s) => Console.Error.Write(s);

    /// <summary>pcem: wx-sdl2.c:476 — après la vidéo, comme pc_main ; branche joystick_poll.</summary>
    internal static void Init()
    {
        joystick_init();
        plat_joystick.joystick_poll = joystick_poll;
    }

    internal static void Close()
    {
        plat_joystick.joystick_poll = null;
        joystick_close();
    }

    // pcem: wx-sdl2-joystick.c:15-53
    // pcem bug, not reproduced: PB-93 — plus de huit manettes branchées à l'hôte débordent sdl_joy[8] et
    //   plat_joystick_state[8] (:22-23, :34) ; ici, les huit premières seulement.
    internal static void joystick_init()
    {
        int c;

        SDL.InitSubSystem(SDL.InitFlags.Joystick);
        uint[]? ids = SDL.GetJoysticks(out int count);
        joysticks_present = ids is null ? 0 : Math.Min(count, plat_joystick.MAX_PLAT_JOYSTICKS);

        Array.Clear(sdl_joy);
        for (c = 0; c < joysticks_present; c++)
        {
                sdl_joy[c] = SDL.OpenJoystick(ids![c]);

                if (sdl_joy[c] != IntPtr.Zero)
                {
                        int d;
                        plat_joystick_t pj = plat_joystick_state[c];

                        pclog($"Opened Joystick {c}\n");
                        pclog($" Name: {SDL.GetJoystickName(sdl_joy[c])}\n");
                        pclog($" Number of Axes: {SDL.GetNumJoystickAxes(sdl_joy[c])}\n");
                        pclog($" Number of Buttons: {SDL.GetNumJoystickButtons(sdl_joy[c])}\n");
                        pclog($" Number of Hats: {SDL.GetNumJoystickHats(sdl_joy[c])}\n");

                        // pcem: :34 — strncpy(…, 64), au plus 63 caractères utiles.
                        string name = SDL.GetJoystickNameForID(ids[c]) ?? "";
                        pj.name = name.Length > 63 ? name[..63] : name;
                        pj.nr_axes = SDL.GetNumJoystickAxes(sdl_joy[c]);
                        pj.nr_buttons = SDL.GetNumJoystickButtons(sdl_joy[c]);
                        pj.nr_povs = SDL.GetNumJoystickHats(sdl_joy[c]);

                        for (d = 0; d < Math.Min(pj.nr_axes, 8); d++)
                        {
                                pj.axis[d].name = $"Axis {d}";
                                pj.axis[d].id = d;
                        }
                        for (d = 0; d < Math.Min(pj.nr_buttons, 8); d++)
                        {
                                pj.button[d].name = $"Button {d}";
                                pj.button[d].id = d;
                        }
                        for (d = 0; d < Math.Min(pj.nr_povs, 4); d++)
                        {
                                pj.pov[d].name = $"POV {d}";
                                pj.pov[d].id = d;
                        }
                }
        }
    }

    // pcem: wx-sdl2-joystick.c:54-61
    internal static void joystick_close()
    {
        int c;

        for (c = 0; c < joysticks_present; c++)
        {
                if (sdl_joy[c] != IntPtr.Zero)
                        SDL.CloseJoystick(sdl_joy[c]);
        }
    }

    // pcem: wx-sdl2-joystick.c:63-96
    private static int joystick_get_axis(int joystick_nr, int mapping)
    {
        if ((mapping & plat_joystick.POV_X) != 0)
        {
                switch (plat_joystick_state[joystick_nr].p[mapping & 3])
                {
                case SDL_HAT_LEFTUP:
                case SDL_HAT_LEFT:
                case SDL_HAT_LEFTDOWN:
                        return -32767;

                case SDL_HAT_RIGHTUP:
                case SDL_HAT_RIGHT:
                case SDL_HAT_RIGHTDOWN:
                        return 32767;

                default:
                        return 0;
                }
        }
        else if ((mapping & plat_joystick.POV_Y) != 0)
        {
                switch (plat_joystick_state[joystick_nr].p[mapping & 3])
                {
                case SDL_HAT_LEFTUP:
                case SDL_HAT_UP:
                case SDL_HAT_RIGHTUP:
                        return -32767;

                case SDL_HAT_LEFTDOWN:
                case SDL_HAT_DOWN:
                case SDL_HAT_RIGHTDOWN:
                        return 32767;

                default:
                        return 0;
                }
        }
        else
                return plat_joystick_state[joystick_nr].a[plat_joystick_state[joystick_nr].axis[mapping].id];
    }

    // pcem: wx-sdl2-joystick.c:97-153
    internal static void joystick_poll()
    {
        int c, d;

        SDL.UpdateJoysticks();
        for (c = 0; c < joysticks_present; c++)
        {
                int b;
                plat_joystick_t pj = plat_joystick_state[c];

                pj.a[0] = SDL.GetJoystickAxis(sdl_joy[c], 0);
                pj.a[1] = SDL.GetJoystickAxis(sdl_joy[c], 1);
                pj.a[2] = SDL.GetJoystickAxis(sdl_joy[c], 2);
                pj.a[3] = SDL.GetJoystickAxis(sdl_joy[c], 3);
                pj.a[4] = SDL.GetJoystickAxis(sdl_joy[c], 4);
                pj.a[5] = SDL.GetJoystickAxis(sdl_joy[c], 5);

                for (b = 0; b < 16; b++)
                        pj.b[b] = SDL.GetJoystickButton(sdl_joy[c], b) ? 1 : 0;

                for (b = 0; b < 4; b++)
                        pj.p[b] = (int)SDL.GetJoystickHat(sdl_joy[c], b);
        }

        for (c = 0; c < gameport.joystick_get_max_joysticks(gameport.joystick_type); c++)
        {
                joystick_t js = plat_joystick.joystick_state[c];

                if (js.plat_joystick_nr != 0)
                {
                        int joystick_nr = js.plat_joystick_nr - 1;

                        for (d = 0; d < gameport.joystick_get_axis_count(gameport.joystick_type); d++)
                                js.axis[d] = joystick_get_axis(joystick_nr, js.axis_mapping[d]);
                        for (d = 0; d < gameport.joystick_get_button_count(gameport.joystick_type); d++)
                                js.button[d] = plat_joystick_state[joystick_nr].b[js.button_mapping[d]];
                        for (d = 0; d < gameport.joystick_get_pov_count(gameport.joystick_type); d++)
                        {
                                int x, y;
                                double angle, magnitude;

                                x = joystick_get_axis(joystick_nr, js.pov_mapping[d, 0]);
                                y = joystick_get_axis(joystick_nr, js.pov_mapping[d, 1]);

                                angle = (Math.Atan2((double)y, (double)x) * 360.0) / (2 * Math.PI);
                                magnitude = Math.Sqrt((double)x * (double)x + (double)y * (double)y);

                                if (magnitude < 16384)
                                        js.pov[d] = -1;
                                else
                                        js.pov[d] = ((int)angle + 90 + 360) % 360;
                        }
                }
                else
                {
                        for (d = 0; d < gameport.joystick_get_axis_count(gameport.joystick_type); d++)
                                js.axis[d] = 0;
                        for (d = 0; d < gameport.joystick_get_button_count(gameport.joystick_type); d++)
                                js.button[d] = 0;
                        for (d = 0; d < gameport.joystick_get_pov_count(gameport.joystick_type); d++)
                                js.pov[d] = -1;
                }
        }
    }

    // ---------------------------------------------------------------------------------------
    // --joystick-check : l'auto-contrôle de l'hôte, sur une manette virtuelle de SDL3.
    // ---------------------------------------------------------------------------------------

    /// <summary>Une manette VIRTUELLE de SDL3 (six axes, seize boutons, un chapeau) branchée, puis lue
    /// par joystick_init et joystick_poll comme une vraie ; la section [Joysticks] posée dans l'arbre
    /// de configuration et lue par pc.load_joysticks. Vérifie la lecture (axes, boutons, chapeau),
    /// les correspondances explicites et par POV_X/POV_Y, le défaut du chapeau sur (d, d) (PB-104,
    /// reproduit), l'angle de 315° du haut-gauche (celui de PB-103), et les numéros hors borne
    /// ramenés au défaut (R9) sans que la lecture tombe. Sans fenêtre.</summary>
    internal static int SelfCheck()
    {
        int fail = 0;

        void Check(string what, bool ok, string got)
        {
            Console.WriteLine($"  [{(ok ? "ok" : "ECHEC")}] {what} : {got}");
            if (!ok)
                fail++;
        }

        Console.WriteLine("Auto-contrôle de la manette de l'hôte (SDL3, manette virtuelle).");
        Console.WriteLine();

        if (!SDL.InitSubSystem(SDL.InitFlags.Joystick))
        {
            Console.WriteLine($"  [ECHEC] SDL_InitSubSystem(JOYSTICK) : {SDL.GetError()}");
            return 1;
        }

        IntPtr nom = Marshal.StringToCoTaskMemUTF8("iXtal26 virtuelle");
        uint id = 0;
        IntPtr virt = IntPtr.Zero;
        try
        {
            var desc = new SDL.VirtualJoystickDesc
            {
                Version = (uint)Marshal.SizeOf<SDL.VirtualJoystickDesc>(),
                Type = SDL.JoystickType.Gamepad,
                NAxes = 6,
                NButtons = 16,
                NHats = 1,
                Name = nom,
            };
            id = SDL.AttachVirtualJoystick(in desc);
            Check("manette virtuelle branchée", id != 0, id != 0 ? $"identifiant {id}" : SDL.GetError());
            if (id == 0)
                return 1;

            joystick_init();
            Check("joystick_init la voit", joysticks_present >= 1 && plat_joystick_state[0].nr_axes == 6 &&
                                           plat_joystick_state[0].nr_buttons == 16 && plat_joystick_state[0].nr_povs == 1,
                  $"{joysticks_present} manette(s), « {plat_joystick_state[0].name} », {plat_joystick_state[0].nr_axes} axes, " +
                  $"{plat_joystick_state[0].nr_buttons} boutons, {plat_joystick_state[0].nr_povs} chapeau");

            virt = sdl_joy[0];

            void Pose(short x, short y, short z, int boutons, SDL.JoystickHat chapeau)
            {
                SDL.SetJoystickVirtualAxis(virt, 0, x);
                SDL.SetJoystickVirtualAxis(virt, 1, y);
                SDL.SetJoystickVirtualAxis(virt, 2, z);
                for (int b = 0; b < 16; b++)
                    SDL.SetJoystickVirtualButton(virt, b, ((boutons >> b) & 1) != 0);
                SDL.SetJoystickVirtualHat(virt, 0, chapeau);
                joystick_poll();
            }

            // 1. La CH Flightstick Pro (trois axes, quatre boutons, un chapeau), correspondances par
            //    défaut, sauf le chapeau, posé explicitement sur POV_X|0 et POV_Y|0.
            Joystick_cfg(4, "joystick_0_nr = 1", ("joystick_0_pov_0_x", plat_joystick.POV_X),
                         ("joystick_0_pov_0_y", plat_joystick.POV_Y));
            Pose(12000, -20000, 30000, 0b0101, SDL.JoystickHat.LeftUp);
            joystick_t js = plat_joystick.joystick_state[0];
            Check("CH, axes 0, 1 et 2", js.axis[0] == 12000 && js.axis[1] == -20000 && js.axis[2] == 30000,
                  $"{js.axis[0]}, {js.axis[1]}, {js.axis[2]}");
            Check("CH, boutons 1 et 3 enfoncés", js.button[0] == 1 && js.button[1] == 0 && js.button[2] == 1 && js.button[3] == 0,
                  $"{js.button[0]}{js.button[1]}{js.button[2]}{js.button[3]}");
            Check("CH, chapeau en haut à gauche : 315° (l'angle où PB-103 tombe)", js.pov[0] == 315, $"{js.pov[0]}");
            Pose(0, 0, 0, 0, SDL.JoystickHat.Right);
            Check("CH, chapeau à droite : 90°", js.pov[0] == 90, $"{js.pov[0]}");
            Pose(0, 0, 0, 0, SDL.JoystickHat.Centered);
            Check("CH, chapeau au repos : -1", js.pov[0] == -1, $"{js.pov[0]}");

            // 2. PB-104, reproduit : sans correspondance explicite, le chapeau d se calcule sur
            //    (axe d, axe d). Le manche poussé à droite donne 135°, à gauche 315°, le chapeau
            //    lui-même étant ignoré.
            Joystick_cfg(4, "joystick_0_nr = 1");
            Check("PB-104, correspondances du chapeau par défaut : (0, 0)", js.pov_mapping[0, 0] == 0 && js.pov_mapping[0, 1] == 0,
                  $"({js.pov_mapping[0, 0]}, {js.pov_mapping[0, 1]})");
            Pose(32767, 0, 0, 0, SDL.JoystickHat.Up);
            Check("PB-104, manche à droite, chapeau en haut : 135°", js.pov[0] == 135, $"{js.pov[0]}");
            Pose(-32768, 0, 0, 0, SDL.JoystickHat.Centered);
            Check("PB-104, manche à gauche : 315°", js.pov[0] == 315, $"{js.pov[0]}");

            // 3. Une correspondance explicite : l'axe 0 de la manette émulée sur l'axe 2 de l'hôte,
            //    le bouton 0 sur le bouton 15.
            Joystick_cfg(0, "joystick_0_nr = 1", ("joystick_0_axis_0", 2), ("joystick_0_button_0", 15));
            Pose(111, 222, 333, 1 << 15, SDL.JoystickHat.Centered);
            Check("axe 0 ← axe 2 de l'hôte, bouton 0 ← bouton 15", js.axis[0] == 333 && js.axis[1] == 222 && js.button[0] == 1,
                  $"axe 0 = {js.axis[0]}, axe 1 = {js.axis[1]}, bouton 0 = {js.button[0]}");

            // 4. R9 : les numéros hors borne, ramenés au défaut avec un avertissement ; la lecture
            //    ne tombe pas. Chez PCem, joystick_poll indexerait hors des tableaux.
            Console.WriteLine();
            Console.WriteLine("R9 — numéros hors borne (avertissements attendus ci-dessous) :");
            Joystick_cfg(4, "joystick_0_nr = 1", ("joystick_0_axis_0", 8), ("joystick_0_axis_1", 0x20000000),
                         ("joystick_0_button_0", 32), ("joystick_0_button_1", -1), ("joystick_0_pov_0_x", 99));
            Check("R9, correspondances ramenées au défaut",
                  js.axis_mapping[0] == 0 && js.axis_mapping[1] == 1 && js.button_mapping[0] == 0 && js.button_mapping[1] == 1 &&
                  js.pov_mapping[0, 0] == 0,
                  $"axes ({js.axis_mapping[0]}, {js.axis_mapping[1]}), boutons ({js.button_mapping[0]}, {js.button_mapping[1]}), " +
                  $"chapeau x {js.pov_mapping[0, 0]}");
            Pose(500, 600, 0, 0b11, SDL.JoystickHat.Centered);
            Check("R9, la lecture tient", js.axis[0] == 500 && js.axis[1] == 600 && js.button[0] == 1 && js.button[1] == 1,
                  $"{js.axis[0]}, {js.axis[1]}, boutons {js.button[0]}{js.button[1]}");
            Joystick_cfg(0, "joystick_0_nr = 9");
            Check("R9, joystick_0_nr = 9 ramené à 0 (aucune manette)", js.plat_joystick_nr == 0, $"{js.plat_joystick_nr}");
            Joystick_cfg(0, "joystick_0_nr = -3");
            Check("R9, joystick_0_nr = -3 ramené à 0", js.plat_joystick_nr == 0, $"{js.plat_joystick_nr}");
            Pose(0, 0, 0, 0, SDL.JoystickHat.Centered);
            Check("sans manette : axes à 0, chapeau à -1", js.axis[0] == 0 && js.axis[1] == 0 && js.pov[0] == -1,
                  $"{js.axis[0]}, {js.axis[1]}, chapeau {js.pov[0]}");
        }
        finally
        {
            joystick_close();
            if (id != 0)
                SDL.DetachVirtualJoystick(id);
            Marshal.FreeCoTaskMem(nom);
            for (int c = 0; c < plat_joystick.MAX_JOYSTICKS; c++)
                plat_joystick.joystick_state[c].plat_joystick_nr = 0;
            SDL.QuitSubSystem(SDL.InitFlags.Joystick);
        }

        Console.WriteLine();
        Console.WriteLine(fail == 0 ? "Vert : la manette de l'hôte est lue et mise en correspondance comme chez PCem, R9 compris."
                                    : $"ROUGE : {fail} contrôle(s) en échec.");
        return fail == 0 ? 0 : 1;
    }

    /// <summary>Pour l'auto-contrôle : un type de manette et une section [Joysticks] neuve, posés
    /// dans l'arbre de configuration puis lus par pc.load_joysticks, comme d'un .cfg.</summary>
    private static void Joystick_cfg(int type, string nr, params (string cle, int valeur)[] cles)
    {
        config.config_load(config.CFG_MACHINE, "");
        gameport.joystick_type = type;
        string[] kv = nr.Split(" = ");
        config.config_set_int(config.CFG_MACHINE, "Joysticks", kv[0], int.Parse(kv[1]));
        foreach (var (cle, valeur) in cles)
            config.config_set_int(config.CFG_MACHINE, "Joysticks", cle, valeur);
        for (int c = 0; c < plat_joystick.MAX_JOYSTICKS; c++)
            plat_joystick.joystick_state[c].plat_joystick_nr = 0;
        pc.load_joysticks();
    }
}
