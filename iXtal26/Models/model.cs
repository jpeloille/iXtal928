// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/models/model.c  (common_init :192-200, xt_init :202-211,
//         m_ibmpc :777-778, model_init :686)
// STATUS: partial — une seule machine, l'IBM PC 5150. Les 96 autres MODEL et
//         leurs fonctions d'init sont omises.
//
// Fait notable : PCem câble le 5150 et le XT 5160 avec le MÊME xt_init(). La seule
// différence tient au romset (ROM_IBMPC vs ROM_IBMXT), à la RAM de base (32 vs
// 64 Ko) et à la cassette, que seul le 5150 reçoit.

using iXtal26.Memory;

namespace iXtal26.Models;

internal static partial class model
{
    // pcem: ibm.h — romsets. Seul ROM_IBMPC est transcrit.
    internal const int ROM_IBMPC = 0;

    // pcem: ibm.h:272
    internal static int romset = ROM_IBMPC;

    // pcem: model.c:777-778
    //   MODEL m_ibmpc = {"[8088] IBM PC", ROM_IBMPC, "ibmpc", {{"", cpus_8088}, ...},
    //                    MODEL_GFX_NONE, 64, 640, 32, xt_init, NULL};
    // Les champs retenus : RAM min 64 Ko, max 640 Ko, pas 32 Ko.
    internal const int MIN_RAM = 64;
    internal const int MAX_RAM = 640;
    internal const int RAM_GRANULARITY = 32;

    // pcem: model.c:192-200
    internal static void common_init()
    {
        dma.dma_init();
        // omitted: fdc_add() — contrôleur de disquettes, hors périmètre.
        // omitted: lpt_init() — port parallèle, hors périmètre.
        pic.pic_init();
        pit.pit_init();
        // omitted: serial1_init/serial2_init — UART, hors périmètre.
    }

    // pcem: model.c:202-211
    internal static void xt_init()
    {
        common_init();
        mem.mem_add_bios();
        pit.pit_set_out_func(pit.pit_, 1, pit.pit_refresh_timer_xt);
        keyboard_xt.keyboard_xt_init();
        nmi.nmi_init();
        // omitted: device_add(&gameport_device) — port jeu, hors périmètre.
        // omitted: device_add(&cassette_device) — port cassette du 5150 ; le BIOS
        //   le teste mais n'échoue pas en son absence (il bascule sur BASIC).
    }

    // pcem: model.c:686
    internal static void model_init()
    {
        mem.mem_size = MAX_RAM;
        xt_init();
    }
}
