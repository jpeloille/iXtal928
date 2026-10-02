// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/video/video.c + includes/private/video/video.h
// STATUS: partial — buffer32, blit, cgapal/makecol/makecol32/RGB, loadfont (MDA, CGA),
//         fontdat/fontdatm, edatlookup, xsize/ysize, video_res_*, video_updatetiming
//         réduit, le registre VIDEO_CARD réduit à v_cga et v_vga, video_init.
//         Omis : les 62 autres cartes, les shaders et les visionneuses de débogage.

using System.Runtime.CompilerServices;
using iXtal26.Cpu;
using iXtal26.Models;
using static iXtal26.Cpu.x86;
using static iXtal26.Flash.rom;

namespace iXtal26.Video;

// pcem: video.h:68
internal delegate void video_blit_memtoscreen_fn(int x, int y, int y1, int y2, int w, int h);

// pcem: video.h:14-18 — `typedef RGB PALETTE[256]` devient un RGB[256] là où il sert
// (svga_t.vgapal). Struct : copié par valeur, jamais par adresse.
internal struct RGB
{
    internal uint8_t r, g, b;
}

// pcem: devices.h:54-58
internal struct video_timings_t
{
    internal int type;
    internal int write_b, write_w, write_l;
    internal int read_b, read_w, read_l;
}

// pcem: devices.h:60-67
internal sealed class VIDEO_CARD
{
    internal string name = "";
    internal string internal_name = "";
    internal PluginApi.device_t? device;
    internal int legacy_id;
    internal int flags;
    internal video_timings_t timing;
}

// pcem: video.h:81-91
internal enum fontformat_t
{
    FONT_MDA,      /* MDA 8x14 */
    FONT_PC200,    /* MDA 8x14 and CGA 8x8, four fonts */
    FONT_CGA,      /* CGA 8x8, two fonts */
    FONT_WY700,    /* Wy700 16x16, two fonts */
    FONT_MDSI,     /* MDSI Genius 8x12 */
    FONT_T3100E,   /* Toshiba T3100e, four fonts */
    FONT_KSC5601,  /* Korean KSC-5601 */
    FONT_SIGMA400, /* Sigma Color 400, 8x8 and 8x16 */
    FONT_IM1024,   /* Image Manager 1024 */
}

internal static partial class video
{
    // DEVIATION: pclog() appartient à ibm.h / plugin-api/logging.c, pas encore
    //   transcrit. Même forme que mem_bios.cs:20 et rom.cs:32.
    private static void pclog(string s) => Console.Error.Write(s);

    // omitted: les 48 VIDEO_CARD de video.c:69-193 autres que v_cga et v_vga — MDA, EGA,
    //   SVGA, Voodoo, hors cible. Le registre est TRANSCRIT depuis la VGA, réduit à ses
    //   deux entrées : avec une seule il était une constante, et video_is_* rendait une
    //   réponse figée. Voir video_cards plus bas.
    // omitted: video_card_has_config et video_card_getid (video.c:340-363) — seule la
    //   boîte de configuration de l'UI les appelle, et aucune des deux cartes n'a de
    //   dialogue de réglage.
    // omitted: video_fullscreen / video_fullscreen_scale / video_fullscreen_first /
    //   video_force_aspect_ration / vid_disc_indicator / vid_resize / readflash
    //   (video.c:542-544) — état de la fenêtre et des visionneuses de débogage.
    // omitted: rotatevga[8][256] (video.c:551) — table de l'EGA ; vid_svga.c porte sa
    //   propre svga_rotate.
    // omitted: fontdatw / fontdat8x12 / fontdat12x18 / fontdatksc5601 /
    //   fontdatksc5601_user (video.c:922-926) — Wyse 700, MDSI Genius, Image Manager
    //   1024 et KSC-5601.
    // omitted: create_bitmap / destroy_bitmap / screen (wx-sdl2-video.c:15, 57, 59) —
    //   inversion de dépendance cœur -> hôte, voir le registre des omissions.

    // pcem: video.c:62
    internal const int VIDEO_ISA = 0;
    internal const int VIDEO_BUS = 1;

    // pcem: video.h:108-114
    internal const int DISPLAY_RGB = 0;
    internal const int DISPLAY_COMPOSITE = 1;
    internal const int DISPLAY_RGB_NO_BROWN = 2;
    internal const int DISPLAY_GREEN = 3;
    internal const int DISPLAY_AMBER = 4;
    internal const int DISPLAY_WHITE = 5;
    internal const int DISPLAY_TRUECOLOUR = 6;

    // pcem: video.h:20
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint32_t makecol(int r, int g, int b) => (uint32_t)((b) | ((g) << 8) | ((r) << 16));

    // pcem: video.h:21
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint32_t makecol32(int r, int g, int b) => (uint32_t)((b) | ((g) << 8) | ((r) << 16));

    // pcem: video.h:4-8, video.c:918, wx-sdl2-video.c:59-69
    //
    // DEVIATION: VIDEO_BITMAP est une struct à tableau de taille zéro (extension
    //   GCC) : `uint8_t *line[0]`, allouée en un seul malloc avec `dat`, puis
    //   indexée `((uint32_t *)buffer32->line[y])[x]`. Ici un seul tableau plat de
    //   uint32_t, indexé `Buffer32[y * Stride + x]`. La géométrie est celle de
    //   create_bitmap(2048, 2048) (video.c:1067) : w = h = 2048, quatre octets par
    //   pixel. Le champ `w` n'est lu nulle part dans le chemin CGA ; `h` l'est par
    //   hline, d'où Height.
    internal const int Stride = 2048;
    internal const int Height = 2048;

    // DEVIATION: `static readonly`, alloué ici et non dans initvideo().
    //   La longueur devient une CONSTANTE pour le JIT, ce qu'un champ réassignable
    //   ne peut pas être — et cga_poll écrit ~31 M fois par seconde émulée dans ce
    //   tableau. Même taille, même contenu, même durée de vie utile : create_bitmap
    //   (video.c:1067) alloue une fois pour toutes, lui aussi. initvideo() remet à
    //   zéro au lieu de réallouer. VERIFICATION.md § M5.2.
    internal static readonly uint32_t[] Buffer32 = new uint32_t[Stride * Height];

    // pcem: video.c:548-555
    internal static int egareads = 0, egawrites = 0;
    internal static int changeframecount = 2;

    internal static int frames = 0;
    internal static int video_frames = 0;
    internal static int video_refresh_rate = 0;

    // pcem: video.c:557
    internal static int fullchange;

    // pcem: video.c:559 — lue par svga_render_4bpp_* (vid_svga_render.cs), remplie par
    // initvideo.
    internal static readonly uint8_t[,] edatlookup = new uint8_t[4, 4];

    /*Video timing settings -

    8-bit - 1mb/sec
            B = 8 ISA clocks
            W = 16 ISA clocks
            L = 32 ISA clocks

    Slow 16-bit - 2mb/sec
            B = 6 ISA clocks
            W = 8 ISA clocks
            L = 16 ISA clocks

    Fast 16-bit - 4mb/sec
            B = 3 ISA clocks
            W = 3 ISA clocks
            L = 6 ISA clocks

    Slow VLB/PCI - 8mb/sec (ish)
            B = 4 bus clocks
            W = 8 bus clocks
            L = 16 bus clocks

    Mid VLB/PCI -
            B = 4 bus clocks
            W = 5 bus clocks
            L = 10 bus clocks

    Fast VLB/PCI -
            B = 3 bus clocks
            W = 3 bus clocks
            L = 4 bus clocks
    */

    // pcem: video.c:594-596 — la septième ligne n'est pas initialisée en C ; C#
    // exige un initialiseur complet pour un tableau multidimensionnel, et le
    // zero-fill implicite du C est donc écrit.
    // DEVIATION: `video_speed = 0` chez PCem (video.c:594), mais loadconfig() l'écrase
    //   TOUJOURS par la clé, défaut -1 (pc.c:665). Trois des quatre appelants d'initpc
    //   ne lisent aucun fichier (pc.cs, cfg_mem_size) : l'initialiseur porte donc la
    //   valeur avec laquelle PCem tourne réellement, comme gfxcard porte la CGA.
    internal static int video_speed = -1;
    internal static readonly int[,] video_timing = new int[7, 4] { { VIDEO_ISA, 8, 16, 32 }, { VIDEO_ISA, 6, 8, 16 }, { VIDEO_ISA, 3, 3, 6 },
                                                                  { VIDEO_BUS, 4, 8, 16 },  { VIDEO_BUS, 4, 5, 10 }, { VIDEO_BUS, 3, 3, 4 },
                                                                  { 0, 0, 0, 0 } };

    // pcem: cpu.h:163
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ISA_CYCLES(int x) => x * cpu_c.isa_cycles;

    // pcem: video.c:200 — G1.1 : la M24.
    private static readonly video_timings_t timing_m24 = new video_timings_t
        { type = VIDEO_ISA, write_b = 8, write_w = 16, write_l = 32, read_b = 8, read_w = 16, read_l = 32 };

    // pcem: video.c:598-749
    internal static void video_updatetiming()
    {
        if (video_speed == -1)
        {
                video_timings_t timing;
                int new_gfxcard = 0;

                // pcem: video.c:603-718 — G1.1 : le switch (romset) des machines à vidéo intégrée,
                // réduit à la M24 (:629-630). DEVIATION : il passe AVANT la lecture de la carte —
                // la même valeur finale ; mais gfxcard peut valoir GFX_BUILTIN (-1) sur une machine
                // à vidéo fixe, où le C ne fait que FORMER &video_cards[-1] et le C# lèverait.
                // omitted: les autres étiquettes (timing_dram, timing_pc1640, timing_vga…), machines
                //   hors dépôt.
                if (pc.romset == pc.ROM_OLIM24)
                        timing = timing_m24;
                else
                {
                        new_gfxcard = video_old_to_new(pc.gfxcard);
                        timing = video_cards[new_gfxcard].timing;
                }

                if (timing.type == VIDEO_ISA)
                {
                        video_timing_read_b = ISA_CYCLES(timing.read_b);
                        video_timing_read_w = ISA_CYCLES(timing.read_w);
                        video_timing_read_l = ISA_CYCLES(timing.read_l);
                        video_timing_write_b = ISA_CYCLES(timing.write_b);
                        video_timing_write_w = ISA_CYCLES(timing.write_w);
                        video_timing_write_l = ISA_CYCLES(timing.write_l);
                }
                else
                {
                        video_timing_read_b = (int)(pit.bus_timing * timing.read_b);
                        video_timing_read_w = (int)(pit.bus_timing * timing.read_w);
                        video_timing_read_l = (int)(pit.bus_timing * timing.read_l);
                        video_timing_write_b = (int)(pit.bus_timing * timing.write_b);
                        video_timing_write_w = (int)(pit.bus_timing * timing.write_w);
                        video_timing_write_l = (int)(pit.bus_timing * timing.write_l);
                }
        }
        else if (video_timing[video_speed, 0] == VIDEO_ISA)
        {
                video_timing_read_b = ISA_CYCLES(video_timing[video_speed, 1]);
                video_timing_read_w = ISA_CYCLES(video_timing[video_speed, 2]);
                video_timing_read_l = ISA_CYCLES(video_timing[video_speed, 3]);
                video_timing_write_b = ISA_CYCLES(video_timing[video_speed, 1]);
                video_timing_write_w = ISA_CYCLES(video_timing[video_speed, 2]);
                video_timing_write_l = ISA_CYCLES(video_timing[video_speed, 3]);
        }
        else
        {
                video_timing_read_b = (int)(pit.bus_timing * video_timing[video_speed, 1]);
                video_timing_read_w = (int)(pit.bus_timing * video_timing[video_speed, 2]);
                video_timing_read_l = (int)(pit.bus_timing * video_timing[video_speed, 3]);
                video_timing_write_b = (int)(pit.bus_timing * video_timing[video_speed, 1]);
                video_timing_write_w = (int)(pit.bus_timing * video_timing[video_speed, 2]);
                video_timing_write_l = (int)(pit.bus_timing * video_timing[video_speed, 3]);
        }
        // omitted: pclog("Video timing %i %i %i\n", …) (video.c:744) — sortie pure.
        if (cpu_16bitbus != 0)
        {
                video_timing_read_l = video_timing_read_w * 2;
                video_timing_write_l = video_timing_write_w * 2;
        }
    }

    // pcem: video.c:751-752
    internal static int video_timing_read_b, video_timing_read_w, video_timing_read_l;
    internal static int video_timing_write_b, video_timing_write_w, video_timing_write_l;

    // pcem: video.c:754
    internal static int video_res_x, video_res_y, video_bpp;

    // pcem: video.c:546 — lues par les rendus 15 et 16 bpp, que le RAMDAC TKD8001 de
    // la Trident 8900D rend atteignables (M19).
    internal static uint32_t[] video_15to32 = null!, video_16to32 = null!;

    // pcem: video.c:756
    internal static video_blit_memtoscreen_fn? video_blit_memtoscreen_func;

    // pcem: video.c:64-67
    internal const int VIDEO_FLAG_TYPE_CGA = 0;
    internal const int VIDEO_FLAG_TYPE_MDA = 1;
    internal const int VIDEO_FLAG_TYPE_SPECIAL = 2;
    internal const int VIDEO_FLAG_TYPE_MASK = 3;

    // pcem: video.c:96
    internal static readonly VIDEO_CARD v_cga = new VIDEO_CARD
    {
        name = "CGA", internal_name = "cga", device = vid_cga.cga_device, legacy_id = pc.GFX_CGA,
        flags = VIDEO_FLAG_TYPE_CGA,
        timing = new video_timings_t { type = VIDEO_ISA, write_b = 8, write_w = 16, write_l = 32, read_b = 8, read_w = 16, read_l = 32 },
    };

    // pcem: video.c:99-100
    internal static readonly VIDEO_CARD v_cl_gd5429 = new VIDEO_CARD
    {
        name = "Cirrus Logic CL-GD5429", internal_name = "cl_gd5429", device = vid_cl5429.gd5429_device, legacy_id = pc.GFX_CL_GD5429,
        flags = VIDEO_FLAG_TYPE_SPECIAL,
        timing = new video_timings_t { type = VIDEO_BUS, write_b = 4, write_w = 4, write_l = 8, read_b = 10, read_w = 10, read_l = 20 },
    };

    // pcem: video.c:164-166
    internal static readonly VIDEO_CARD v_px_trio64 = new VIDEO_CARD
    {
        name = "Phoenix S3 Trio64", internal_name = "px_trio64", device = vid_s3.s3_phoenix_trio64_device, legacy_id = pc.GFX_PHOENIX_TRIO64,
        flags = VIDEO_FLAG_TYPE_SPECIAL,
        timing = new video_timings_t { type = VIDEO_BUS, write_b = 3, write_w = 2, write_l = 4, read_b = 25, read_w = 25, read_l = 40 },
    };

    // pcem: video.c:177-178
    internal static readonly VIDEO_CARD v_tvga8900d = new VIDEO_CARD
    {
        name = "Trident TVGA8900D", internal_name = "tvga8900d", device = vid_tvga.tvga8900d_device, legacy_id = pc.GFX_TVGA,
        flags = VIDEO_FLAG_TYPE_SPECIAL,
        timing = new video_timings_t { type = VIDEO_ISA, write_b = 3, write_w = 3, write_l = 6, read_b = 8, read_w = 8, read_l = 12 },
    };

    // pcem: video.c:179-181
    internal static readonly VIDEO_CARD v_tvga9000b = new VIDEO_CARD
    {
        name = "Trident TVGA9000B", internal_name = "tvga9000b", device = vid_tvga.tvga9000b_device, legacy_id = pc.GFX_TVGA9000B,
        flags = VIDEO_FLAG_TYPE_SPECIAL,
        timing = new video_timings_t { type = VIDEO_ISA, write_b = 7, write_w = 7, write_l = 12, read_b = 7, read_w = 7, read_l = 12 },
    };

    // pcem: video.c:191
    internal static readonly VIDEO_CARD v_vga = new VIDEO_CARD
    {
        name = "VGA", internal_name = "vga", device = vid_vga.vga_device, legacy_id = pc.GFX_VGA,
        flags = VIDEO_FLAG_TYPE_SPECIAL,
        timing = new video_timings_t { type = VIDEO_ISA, write_b = 8, write_w = 16, write_l = 32, read_b = 8, read_w = 16, read_l = 32 },
    };

    // pcem: plugin-api/device.c:17 et video.c:1301-1354 — le registre, rempli par
    // video_init_builtin dans l'ordre de ses pcem_add_video.
    //
    // DEVIATION: six entrées au lieu des quarante-neuf que video_init_builtin enregistre
    //   (cinquante pcem_add_video, dont v_pgc sous USE_EXPERIMENTAL_PGC), dans l'ordre RELATIF de PCem
    //   (v_cga en :1312, v_cl_gd5429 en :1314, v_px_trio64 en :1340, v_tvga8900d en :1345, v_tvga9000b en :1346, v_vga en :1351), et en tableau fixe comme models[]
    //   (model.cs) plutôt que par pcem_add_video. Les INDICES diffèrent donc de ceux de
    //   PCem — v_cga y est à 10 — mais aucun indice ne sort de ce fichier : la
    //   configuration écrit l'internal_name, et gfxcard porte l'identifiant HÉRITÉ
    //   (GFX_*), que video_old_to_new traduit. La sentinelle NULL de fin de liste est
    //   la longueur du tableau.
    internal static readonly VIDEO_CARD[] video_cards = { v_cga, v_cl_gd5429, v_px_trio64, v_tvga8900d, v_tvga9000b, v_vga };

    // pcem: video.c:215-223
    internal static int video_card_available(int card)
    {
        if (card == pc.GFX_BUILTIN)
                return 1;

        if (video_cards[card].device != null)
                return PluginApi.device.device_available(video_cards[card].device!);

        return 1;
    }

    // pcem: video.c:225-232
    internal static string video_card_getname(int card)
    {
        if (card == pc.GFX_BUILTIN)
                return "Built-in video";
        if (card >= video_cards.Length)
                return "";

        return video_cards[card].name;
    }

    // pcem: video.c:234-338
    // omitted: le switch sur romset (video.c:235-336), sauf la M24 (G1.1, :264-265) —
    //   trente-deux étiquettes de romset pour les cartes intégrées de machines hors dépôt.
    internal static PluginApi.device_t? video_card_getdevice(int card, int romset)
    {
        switch (romset)
        {
        case pc.ROM_OLIM24:
                return vid_olivetti_m24.m24_device;
        }
        return video_cards[card].device;
    }

    // pcem: video.c:365-378
    internal static int video_old_to_new(int card)
    {
        int c = 0;

        if (card == pc.GFX_BUILTIN)
                return pc.GFX_BUILTIN;

        while (c < video_cards.Length && video_cards[c].device != null)
        {
                if (video_cards[c].legacy_id == card)
                        return c;
                c++;
        }

        return 0;
    }

    // pcem: video.c:380-385
    internal static int video_new_to_old(int card)
    {
        if (card == pc.GFX_BUILTIN)
                return pc.GFX_BUILTIN;

        return video_cards[card].legacy_id;
    }

    // pcem: video.c:387-392
    internal static string video_get_internal_name(int card)
    {
        if (card == pc.GFX_BUILTIN)
                return "builtin";

        return video_cards[card].internal_name;
    }

    // pcem: video.c:394-407
    internal static int video_get_video_from_internal_name(string s)
    {
        int c = 0;

        if (s == "builtin")
                return pc.GFX_BUILTIN;

        while (c < video_cards.Length)
        {
                if (video_cards[c].internal_name == s)
                        return video_cards[c].legacy_id;
                c++;
        }

        return 0;
    }

    // pcem: video.c:409-454, :455-502, :503-540
    // omitted: le switch sur romset qui ouvre chacune des trois (video.c:410-452,
    //   :456-500, :504-538) — des machines à vidéo intégrée, dont aucune n'est au dépôt :
    //   les quatre romsets du dépôt tombent tous dans la lecture des drapeaux de la carte.
    //   Le PPI du XT les lit pour composer les interrupteurs DIP (keyboard_xt.cs), le
    //   8042 de l'AT pour son port d'entrée (keyboard_at.cs). L'oracle rend les mêmes
    //   réponses depuis gfxcard (harness_stubs.c).
    // G1.1 — la M24 : MDA non, CGA oui, EGA/VGA non (video.c:433, :470, :512), avant toute
    //   lecture de la carte — gfxcard peut y valoir GFX_BUILTIN.
    internal static int video_is_mda()
    {
        if (pc.romset == pc.ROM_OLIM24)
                return 0;
        return (video_cards[video_old_to_new(pc.gfxcard)].flags & VIDEO_FLAG_TYPE_MASK) == VIDEO_FLAG_TYPE_MDA ? 1 : 0;
    }

    internal static int video_is_cga()
    {
        if (pc.romset == pc.ROM_OLIM24)
                return 1;
        return (video_cards[video_old_to_new(pc.gfxcard)].flags & VIDEO_FLAG_TYPE_MASK) == VIDEO_FLAG_TYPE_CGA ? 1 : 0;
    }

    internal static int video_is_ega_vga()
    {
        if (pc.romset == pc.ROM_OLIM24)
                return 0;
        return (video_cards[video_old_to_new(pc.gfxcard)].flags & VIDEO_FLAG_TYPE_MASK) == VIDEO_FLAG_TYPE_SPECIAL ? 1 : 0;
    }

    // pcem: video.c:758-916
    internal static void video_init()
    {
        // omitted: pclog("Video_init %i %i\n", romset, gfxcard) (video.c:759).
        // omitted: le switch sur romset (video.c:761-914) — PCjr, Tandy, PC1512,
        //   PC1640, PC200, PPC512, Olivetti M24, PC2086/3086, MegaPC, SPC4620P,
        //   SPC6033P, Acer 386, AMA932J, PS/1, PS/2, T3100e, T1000, PC425X, PB410A,
        //   PB570, PB520R, CBM SL386SX25 : aucune n'est au dépôt, et les quatre romsets
        //   qui y sont tombent tous dans la ligne qui suit.
        // pcem: video.c:800-802 — G1.1 : la M24 a SA vidéo, gfxcard ignoré.
        switch (pc.romset)
        {
        case pc.ROM_OLIM24:
                PluginApi.device.device_add(vid_olivetti_m24.m24_device);
                return;
        }
        PluginApi.device.device_add(video_cards[video_old_to_new(pc.gfxcard)].device!);
    }

    // pcem: video.c:920-921
    // DEVIATION: `uint8_t fontdat[2048][8]` aplati en [2048 * 8], indexé (c << 3) | d.
    //   Un tableau [,] échappe à l'analyse de plages de RyuJIT (deux bornes et une
    //   multiplication par accès), et cga_poll le lit huit fois par caractère —
    //   7,7 M lectures par seconde émulée. Le bloc plat est ce que le C a en mémoire.
    //   fontdatm, hors du chemin chaud, garde sa forme. VERIFICATION.md § M5.1.
    internal static readonly uint8_t[] fontdat = new uint8_t[2048 * 8];
    internal static readonly uint8_t[,] fontdatm = new uint8_t[2048, 16];

    // pcem: video.c:928
    internal static int xsize = 1, ysize = 1;

    // pcem: video.c:930-1047
    internal static void loadfont(string s, fontformat_t format)
    {
        FileStream? f = romfopen(s, "rb");
        int c, d;

        // pcem: video.c:934 — ce pclog était omis comme « sortie pure ». Il ne
        // l'est pas : quand la police manque, fontdat reste à zéro, CHAQUE cellule
        // rend un glyphe vide, et l'écran est noir SANS UN MOT. Le fond des cellules
        // en vidéo inverse continue d'être peint, donc la fenêtre montre des pavés
        // gris sur fond noir — un symptôme qu'on peut passer une heure à attribuer
        // au rendu alors que c'est un fichier introuvable.
        if (f == null)
        {
                pclog($"loadfont : {s} introuvable — police non chargée.\n");
                return;
        }
        // omitted: FONT_PC200 (video.c:961-974), FONT_WY700 (video.c:984-990),
        //   FONT_MDSI (video.c:991-997), FONT_T3100E (video.c:998-1021),
        //   FONT_KSC5601 (video.c:1022-1028), FONT_SIGMA400 (video.c:1029-1039) et
        //   FONT_IM1024 (video.c:1040-1044) — cartes hors palier : leurs tables de
        //   destination (fontdatw, fontdat8x12, fontdatksc5601, fontdat12x18) sont
        //   omises avec elles.
        //
        // MAIS LEURS ÉTIQUETTES DE `case` NE LE SONT PAS, et c'est tout le sujet.
        // Dans le C, `default:` n'est accolé qu'à `case FONT_CGA` (video.c:975-976) :
        // les sept formats ci-dessus sont des cas EXPLICITES qui écrivent chacun dans
        // LEUR table et ne touchent jamais fontdat. Les avoir laissés tomber dans
        // `default` faisait de chaque appel une relecture en FONT_CGA de fontdat
        // ENTIER. Et mem_bios.c:59-62 appelle loadfont quatre fois de suite, sans
        // condition : mda.rom remplissait fontdat correctement, puis wy700.rom
        // (16 384 o = 2048 x 8) l'écrasait intégralement, puis 8x12.bin (4 096 o)
        // écrasait les 512 premiers caractères et laissait 0xFF sur le reste —
        // FileStream.ReadByte() rend -1 en fin de fichier, que le cast en uint8_t
        // transforme en 0xFF. Résultat à l'écran : un seul glyphe répété sur toute
        // la grille, y compris à la place des espaces.
        //
        // Aucun oracle ne pouvait l'attraper : la police ne touche AUCUN état CPU.
        // Les 24 944 866 instructions du diff d'amorçage restent identiques, et le
        // vidage texte de BootTest lit la VRAM — qui est juste. Seul le chemin pixel
        // est faux, et il n'a eu de lecteur qu'à l'ouverture de la fenêtre.
        // Voir VERIFICATION.md § M4.3.
        switch (format)
        {
        case fontformat_t.FONT_PC200:
        case fontformat_t.FONT_WY700:
        case fontformat_t.FONT_MDSI:
        case fontformat_t.FONT_T3100E:
        case fontformat_t.FONT_KSC5601:
        case fontformat_t.FONT_SIGMA400:
        case fontformat_t.FONT_IM1024:
                break;
        case fontformat_t.FONT_MDA: /* MDA */
                for (c = 0; c < 256; c++) { /* 8x14 MDA in 8x8 cell (lines 0-7) */
                        for (d = 0; d < 8; d++) {
                                fontdatm[c, d] = (uint8_t)f.ReadByte();
                        }
                }
                for (c = 0; c < 256; c++) { /* 8x14 MDA in 8x8 cell (lines 8-13 + padding lines) */
                        for (d = 0; d < 8; d++) {
                                fontdatm[c, d + 8] = (uint8_t)f.ReadByte();
                        }
                }
                for (c = 0; c < 256; c++) { /* 8x8 CGA (thin, secondary, normally unused) */
                        for (d = 0; d < 8; d++) {
                                fontdat[((c + 256) << 3) | d] = (uint8_t)f.ReadByte();
                        }
                }
                for (c = 0; c < 256; c++) { /* 8x8 CGA (thick, primary) */
                        for (d = 0; d < 8; d++) {
                                fontdat[(c << 3) | d] = (uint8_t)f.ReadByte();
                        }
                }
                break;
        default:
        case fontformat_t.FONT_CGA:        /* CGA */
                for (c = 0; c < 2048; c++) /* Allow up to 2048 chars */
                {
                        for (d = 0; d < 8; d++) {
                                fontdat[(c << 3) | d] = (uint8_t)f.ReadByte();
                        }
                }
                break;
        }
        f.Close();
    }

    // pcem: video.c:1049-1058
    //
    // DEVIATION: blit_data reste une struct — son adresse n'est jamais prise. Les
    //   quatre membres de synchronisation sont omis avec le thread de blit.
    internal struct blit_data_t
    {
        internal int x, y, y1, y2, w, h;
        internal int busy;
        internal int buffer_in_use;
        // omitted: blit_thread / wake_blit_thread / blit_complete / buffer_not_in_use
        //   (video.c:1054-1057).
    }

    internal static blit_data_t blit_data;

    // DEVIATION: drapeau posé par video_blit_memtoscreen à la place de
    //   thread_set_event(blit_data.wake_blit_thread). L'hôte le teste, lit le
    //   rectangle dans blit_data, fait la remontée et appelle video_blit_complete().
    //   Absent de PCem.
    internal static int blit_pending;

    // pcem: video.c:1062
    internal static readonly uint32_t[] cgapal = new uint32_t[16];

    // pcem: video.c:1064-1105
    internal static void initvideo()
    {
        // DEVIATION: buffer32 = create_bitmap(2048, 2048) (video.c:1067) — le tableau
        //   plat remplace le malloc unique et la table de lignes. Il est alloué à la
        //   déclaration (voir le champ) ; ici on le REMET À ZÉRO, ce que le `new`
        //   d'avant faisait implicitement. initvideo() tourne deux fois par
        //   répétition de `bench` (chaque ré-amorçage passe par pc.initpc) : sans ce
        //   Clear, la seconde mesure hériterait de l'image de la première et
        //   l'empreinte du framebuffer cesserait d'être reproductible.
        Array.Clear(Buffer32);

        int c, d;

        // omitted: le remplissage de rotatevga[] (video.c:1069-1075) — table de l'EGA.
        for (c = 0; c < 4; c++)
        {
                for (d = 0; d < 4; d++)
                {
                        edatlookup[c, d] = 0;
                        if ((c & 1) != 0)
                                edatlookup[c, d] |= 1;
                        if ((d & 1) != 0)
                                edatlookup[c, d] |= 2;
                        if ((c & 2) != 0)
                                edatlookup[c, d] |= 0x10;
                        if ((d & 2) != 0)
                                edatlookup[c, d] |= 0x20;
                }
        }
        video_15to32 = new uint32_t[65536];
        for (c = 0; c < 65536; c++)
                video_15to32[c] = (uint32_t)(((c & 31) << 3) | (((c >> 5) & 31) << 11) | (((c >> 10) & 31) << 19));

        video_16to32 = new uint32_t[65536];
        for (c = 0; c < 65536; c++)
                video_16to32[c] = (uint32_t)(((c & 31) << 3) | (((c >> 5) & 63) << 10) | (((c >> 11) & 31) << 19));

        cgapal_rebuild(DISPLAY_RGB, 0);

        // omitted: thread_create_event ×3 et thread_create(blit_thread)
        //   (video.c:1101-1104) — voir la DEVIATION mono-thread de
        //   video_blit_memtoscreen.
    }

    // pcem: video.c:1107-1116
    internal static void closevideo()
    {
        // omitted: thread_kill(blit_thread) et thread_destroy_event ×3
        //   (video.c:1108-1111) — voir la DEVIATION mono-thread.
        // omitted: free(video_15to32), free(video_16to32), destroy_bitmap(buffer32)
        //   (video.c:1113-1115) — libération manuelle, sans objet sous GC.
    }

    // omitted: blit_thread (video.c:1118-1128) — la boucle de thread qui attendait
    //   wake_blit_thread, appelait video_blit_memtoscreen_func, remettait
    //   blit_data.busy à 0 et signalait blit_complete. Conséquence assumée :
    //   blit_data.busy reste à 1 après le premier blit, et aucun chemin ne
    //   l'observe puisque video_wait_for_blit est vide.

    // pcem: video.c:1130-1133
    internal static void video_blit_complete()
    {
        blit_data.buffer_in_use = 0;
        // omitted: thread_set_event(blit_data.buffer_not_in_use) (video.c:1132).
    }

    // pcem: video.c:1135-1139
    //
    // DEVIATION: mono-thread — corps vide, sites d'appel intacts. Sous Linux
    //   thread_reset_event est vide (thread-pthread.c:46) et thread_wait_event n'a
    //   pas de boucle de prédicat : le handshake dégénère en attente active sur un
    //   int non atomique, il n'y a aucune sémantique à préserver.
    internal static void video_wait_for_blit()
    {
    }

    // pcem: video.c:1140-1144
    internal static void video_wait_for_buffer()
    {
    }

    // pcem: video.c:1146-1160
    internal static void video_blit_memtoscreen(int x, int y, int y1, int y2, int w, int h)
    {
        video_frames++;
        if (h <= 0)
                return;
        video_wait_for_blit();
        blit_data.busy = 1;
        blit_data.buffer_in_use = 1;
        blit_data.x = x;
        blit_data.y = y;
        blit_data.y1 = y1;
        blit_data.y2 = y2;
        blit_data.w = w;
        blit_data.h = h;

        // DEVIATION: thread_set_event(blit_data.wake_blit_thread) (video.c:1159).
        //
        // Le drapeau SEUL ne suffit pas, et c'est un piège qu'on a payé. PCem réveille
        // ici le thread de blit, et l'appel SUIVANT bloque sur video_wait_for_blit()
        // (video.c:1150-1153) tant que blit_data.busy vaut 1 : le cœur ne peut donc
        // JAMAIS écraser un rectangle non encore remonté. Un drapeau, lui, n'est pas une
        // file — deux blits dans la même tranche de 10 ms n'en faisaient qu'un, et deux
        // images étaient silencieusement perdues au moment où le POST reprogramme le CRTC.
        //
        // Sans thread, l'appel SYNCHRONE du crochet hôte a exactement la propriété du
        // handshake d'origine : quand il rend la main, le rectangle est remonté. Le
        // drapeau reste posé pour les hôtes qui n'installent pas de crochet (BootTest,
        // mode --headless), où rien ne le lit mais où il reste diagnostiquable.
        blit_pending = 1;
        video_blit_memtoscreen_func?.Invoke(x, y, y1, y2, w, h);
    }

    // pcem: video.c:1162-1299
    internal static void cgapal_rebuild(int display_type, int contrast)
    {
        switch (display_type)
        {
        case DISPLAY_GREEN:
                if (contrast != 0)
                {
                        cgapal[0x0] = makecol(0x00, 0x00, 0x00);
                        cgapal[0x1] = makecol(0x00, 0x34, 0x0c);
                        cgapal[0x2] = makecol(0x04, 0x5d, 0x14);
                        cgapal[0x3] = makecol(0x04, 0x69, 0x18);
                        cgapal[0x4] = makecol(0x08, 0xa2, 0x24);
                        cgapal[0x5] = makecol(0x08, 0xb2, 0x28);
                        cgapal[0x6] = makecol(0x0c, 0xe7, 0x34);
                        cgapal[0x7] = makecol(0x0c, 0xf3, 0x38);
                        cgapal[0x8] = makecol(0x00, 0x1c, 0x04);
                        cgapal[0x9] = makecol(0x04, 0x4d, 0x10);
                        cgapal[0xa] = makecol(0x04, 0x7d, 0x1c);
                        cgapal[0xb] = makecol(0x04, 0x8e, 0x20);
                        cgapal[0xc] = makecol(0x08, 0xc7, 0x2c);
                        cgapal[0xd] = makecol(0x08, 0xd7, 0x30);
                        cgapal[0xe] = makecol(0x14, 0xff, 0x45);
                        cgapal[0xf] = makecol(0x34, 0xff, 0x5d);
                }
                else
                {
                        cgapal[0x0] = makecol(0x00, 0x00, 0x00);
                        cgapal[0x1] = makecol(0x00, 0x34, 0x0c);
                        cgapal[0x2] = makecol(0x04, 0x55, 0x14);
                        cgapal[0x3] = makecol(0x04, 0x5d, 0x14);
                        cgapal[0x4] = makecol(0x04, 0x86, 0x20);
                        cgapal[0x5] = makecol(0x04, 0x92, 0x20);
                        cgapal[0x6] = makecol(0x08, 0xba, 0x2c);
                        cgapal[0x7] = makecol(0x08, 0xc7, 0x2c);
                        cgapal[0x8] = makecol(0x04, 0x8a, 0x20);
                        cgapal[0x9] = makecol(0x08, 0xa2, 0x24);
                        cgapal[0xa] = makecol(0x08, 0xc3, 0x2c);
                        cgapal[0xb] = makecol(0x08, 0xcb, 0x30);
                        cgapal[0xc] = makecol(0x0c, 0xe7, 0x34);
                        cgapal[0xd] = makecol(0x0c, 0xef, 0x38);
                        cgapal[0xe] = makecol(0x24, 0xff, 0x51);
                        cgapal[0xf] = makecol(0x34, 0xff, 0x5d);
                }
                break;
        case DISPLAY_AMBER:
                if (contrast != 0)
                {
                        cgapal[0x0] = makecol(0x00, 0x00, 0x00);
                        cgapal[0x1] = makecol(0x55, 0x14, 0x00);
                        cgapal[0x2] = makecol(0x82, 0x2c, 0x00);
                        cgapal[0x3] = makecol(0x92, 0x34, 0x00);
                        cgapal[0x4] = makecol(0xcf, 0x61, 0x00);
                        cgapal[0x5] = makecol(0xdf, 0x6d, 0x00);
                        cgapal[0x6] = makecol(0xff, 0x9a, 0x04);
                        cgapal[0x7] = makecol(0xff, 0xae, 0x18);
                        cgapal[0x8] = makecol(0x2c, 0x08, 0x00);
                        cgapal[0x9] = makecol(0x6d, 0x20, 0x00);
                        cgapal[0xa] = makecol(0xa6, 0x45, 0x00);
                        cgapal[0xb] = makecol(0xba, 0x51, 0x00);
                        cgapal[0xc] = makecol(0xef, 0x79, 0x00);
                        cgapal[0xd] = makecol(0xfb, 0x86, 0x00);
                        cgapal[0xe] = makecol(0xff, 0xcb, 0x28);
                        cgapal[0xf] = makecol(0xff, 0xe3, 0x34);
                }
                else
                {
                        cgapal[0x0] = makecol(0x00, 0x00, 0x00);
                        cgapal[0x1] = makecol(0x55, 0x14, 0x00);
                        cgapal[0x2] = makecol(0x79, 0x24, 0x00);
                        cgapal[0x3] = makecol(0x86, 0x2c, 0x00);
                        cgapal[0x4] = makecol(0xae, 0x49, 0x00);
                        cgapal[0x5] = makecol(0xbe, 0x55, 0x00);
                        cgapal[0x6] = makecol(0xe3, 0x71, 0x00);
                        cgapal[0x7] = makecol(0xef, 0x79, 0x00);
                        cgapal[0x8] = makecol(0xb2, 0x4d, 0x00);
                        cgapal[0x9] = makecol(0xcb, 0x5d, 0x00);
                        cgapal[0xa] = makecol(0xeb, 0x79, 0x00);
                        cgapal[0xb] = makecol(0xf3, 0x7d, 0x00);
                        cgapal[0xc] = makecol(0xff, 0x9e, 0x04);
                        cgapal[0xd] = makecol(0xff, 0xaa, 0x10);
                        cgapal[0xe] = makecol(0xff, 0xdb, 0x30);
                        cgapal[0xf] = makecol(0xff, 0xe3, 0x34);
                }
                break;
        case DISPLAY_WHITE:
                if (contrast != 0)
                {
                        cgapal[0x0] = makecol(0x00, 0x00, 0x00);
                        cgapal[0x1] = makecol(0x37, 0x3d, 0x40);
                        cgapal[0x2] = makecol(0x55, 0x5c, 0x5f);
                        cgapal[0x3] = makecol(0x61, 0x67, 0x6b);
                        cgapal[0x4] = makecol(0x8f, 0x95, 0x95);
                        cgapal[0x5] = makecol(0x9b, 0xa0, 0x9f);
                        cgapal[0x6] = makecol(0xcc, 0xcf, 0xc8);
                        cgapal[0x7] = makecol(0xdf, 0xde, 0xd4);
                        cgapal[0x8] = makecol(0x24, 0x27, 0x29);
                        cgapal[0x9] = makecol(0x42, 0x48, 0x4c);
                        cgapal[0xa] = makecol(0x70, 0x76, 0x78);
                        cgapal[0xb] = makecol(0x81, 0x87, 0x87);
                        cgapal[0xc] = makecol(0xaf, 0xb3, 0xb0);
                        cgapal[0xd] = makecol(0xbb, 0xbf, 0xba);
                        cgapal[0xe] = makecol(0xef, 0xed, 0xdf);
                        cgapal[0xf] = makecol(0xff, 0xfd, 0xed);
                }
                else
                {
                        cgapal[0x0] = makecol(0x00, 0x00, 0x00);
                        cgapal[0x1] = makecol(0x37, 0x3d, 0x40);
                        cgapal[0x2] = makecol(0x4a, 0x50, 0x54);
                        cgapal[0x3] = makecol(0x55, 0x5c, 0x5f);
                        cgapal[0x4] = makecol(0x78, 0x7e, 0x80);
                        cgapal[0x5] = makecol(0x81, 0x87, 0x87);
                        cgapal[0x6] = makecol(0xa3, 0xa7, 0xa6);
                        cgapal[0x7] = makecol(0xaf, 0xb3, 0xb0);
                        cgapal[0x8] = makecol(0x7a, 0x81, 0x83);
                        cgapal[0x9] = makecol(0x8c, 0x92, 0x92);
                        cgapal[0xa] = makecol(0xac, 0xb0, 0xad);
                        cgapal[0xb] = makecol(0xb3, 0xb7, 0xb4);
                        cgapal[0xc] = makecol(0xd1, 0xd3, 0xcb);
                        cgapal[0xd] = makecol(0xd9, 0xdb, 0xd2);
                        cgapal[0xe] = makecol(0xf7, 0xf5, 0xe7);
                        cgapal[0xf] = makecol(0xff, 0xfd, 0xed);
                }
                break;

        default:
                cgapal[0x0] = makecol(0x00, 0x00, 0x00);
                cgapal[0x1] = makecol(0x00, 0x00, 0xaa);
                cgapal[0x2] = makecol(0x00, 0xaa, 0x00);
                cgapal[0x3] = makecol(0x00, 0xaa, 0xaa);
                cgapal[0x4] = makecol(0xaa, 0x00, 0x00);
                cgapal[0x5] = makecol(0xaa, 0x00, 0xaa);
                if (display_type == DISPLAY_RGB_NO_BROWN)
                {
                        cgapal[0x6] = makecol(0xaa, 0xaa, 0x00);
                }
                else
                {
                        cgapal[0x6] = makecol(0xaa, 0x55, 0x00);
                }
                cgapal[0x7] = makecol(0xaa, 0xaa, 0xaa);
                cgapal[0x8] = makecol(0x55, 0x55, 0x55);
                cgapal[0x9] = makecol(0x55, 0x55, 0xff);
                cgapal[0xa] = makecol(0x55, 0xff, 0x55);
                cgapal[0xb] = makecol(0x55, 0xff, 0xff);
                cgapal[0xc] = makecol(0xff, 0x55, 0x55);
                cgapal[0xd] = makecol(0xff, 0x55, 0xff);
                cgapal[0xe] = makecol(0xff, 0xff, 0x55);
                cgapal[0xf] = makecol(0xff, 0xff, 0xff);
                break;
        }
    }

    // pcem: wx-sdl2-video.c:49-55
    //
    // DEVIATION: hline est défini par l'UI, pas par video.c. Le paramètre
    //   `VIDEO_BITMAP *b` disparaît avec le tableau plat — il n'y a qu'un bitmap.
    //   L'intervalle reste [x1, x2) et les unités restent des uint32_t. Déclaré ici
    //   pour que vid_cga.c:275 et :277 gardent leurs sites d'appel.
    internal static void hline(int x1, int y, int x2, int col)
    {
        if (y < 0 || y >= Height)
                return;

        for (; x1 < x2; x1++)
                Buffer32[y * Stride + x1] = (uint32_t)col;
    }

    // pcem: wx-sdl2.c:133-142
    //
    // DEVIATION: updatewindowsize est défini par l'UI (ibm.h:415). Hissé ici avec
    //   ses deux globales pour que vid_cga.c:366 garde son site d'appel ; l'hôte lit
    //   video_width et video_height.
    internal static int video_width, video_height;

    internal static void updatewindowsize(int x, int y)
    {
        if (video_width == x && video_height == y)
        {
                return;
        }
        video_width = x;
        video_height = y;

        // omitted: display_resize(x, y) (wx-sdl2.c:141) — remontée cœur -> hôte.
    }

    // pcem: wx-sdl2.c:147-149
    //
    // DEVIATION: startblit/endblit sont SDL_LockMutex/SDL_UnlockMutex sur le verrou
    //   d'affichage (ibm.h:410-411). iXtal26 est mono-thread : corps vides, sites
    //   d'appel pc.c:477 et pc.c:494 intacts.
    internal static void startblit()
    {
    }

    internal static void endblit()
    {
    }
}
