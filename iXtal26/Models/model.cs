// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/models/model.c  (common_init :192-200, xt_init :202-211,
//         m_ibmpc :777-778, model_init :686-694, model_getromset :148,
//         model_get_model_from_internal_name :168-178)
//         + includes/public/pcem/devices.h:69-82 (MODEL)
// STATUS: partial — une seule machine, l'IBM PC 5150. Les 96 autres MODEL et
//         leurs fonctions d'init sont omises.
//
// Fait notable : PCem câble le 5150 et le XT 5160 avec le MÊME xt_init(). La seule
// différence tient au romset (ROM_IBMPC vs ROM_IBMXT), à la RAM de base (32 vs
// 64 Ko) et à la cassette, que seul le 5150 reçoit.
//
// Ce fichier porte la TABLE, pas les machines. Ajouter un XT 5160 y devient une
// entrée plus son init ; mais la machine elle-même demande son BIOS, ses
// périphériques et son propre passage au vert de boot-diff — un jalon, pas un
// réglage.

using iXtal26.Memory;

namespace iXtal26.Models;

// pcem: model.c:686 — `void (*init)()`.
internal delegate void model_init_fn();

// pcem: includes/public/pcem/devices.h:69-82, RÉDUIT.
// omitted: `cpu[5]` — les paquets CPU alternatifs ; cpu.c n'est pas transcrit et le
//   5150 n'a qu'un 8088. omitted: `device` — le device_t de configuration par machine
//   (PCjr, Amstrad, Xi8088) ; m_ibmpc le laisse à NULL, le 5150 n'a pas d'options.
internal sealed class MODEL
{
    internal string name = "";

    // pcem: c'est le ROMSET, valeur de l'énumération ROM_* (ibm.h:164+).
    internal int id;

    // pcem: LA CLÉ ÉCRITE DANS LE .cfg, 24 octets chez PCem.
    internal string internal_name = "";

    internal int flags;
    internal int min_ram, max_ram;
    internal int ram_granularity;
    internal model_init_fn? init;
}

// DEVIATION: PCem a le fichier model.c ET le global `int model` (l'indice courant).
//   TRANSCRIPTION.md tranche cette collision conteneur/membre en suffixant le
//   CONTENEUR : x86seg_c, fdc_c, fdd_c — donc model_c, et le membre garde son nom.
internal static partial class model_c
{
    // pcem: models/model.h:7-23 — le bitmask. Seul MODEL_GFX_NONE sert ici ; les
    // autres sont transcrits parce qu'une entrée de table future les portera.
    internal const int MODEL_AT = 1;
    internal const int MODEL_PS2 = 2;
    internal const int MODEL_AMSTRAD = 4;
    internal const int MODEL_OLIM24 = 8;
    internal const int MODEL_HAS_IDE = 0x10;
    internal const int MODEL_MCA = 0x20;
    internal const int MODEL_PCI = 0x40;
    /*Machine has no integrated graphics*/
    internal const int MODEL_GFX_NONE = 0x000;
    /*Machine has integrated graphics that can not be disabled*/
    internal const int MODEL_GFX_FIXED = 0x100;
    /*Machine has integrated graphics that can be disabled by jumpers or switches*/
    internal const int MODEL_GFX_DISABLE_HW = 0x200;
    /*Machine has integrated graphics that can be disabled through software*/
    internal const int MODEL_GFX_DISABLE_SW = 0x300;
    internal const int MODEL_GFX_MASK = 0x300;

    // pcem: model.c:777-778
    //   MODEL m_ibmpc = {"[8088] IBM PC", ROM_IBMPC, "ibmpc", {{"", cpus_8088}, ...},
    //                    MODEL_GFX_NONE, 64, 640, 32, xt_init, NULL};
    internal const int MIN_RAM = 64;
    internal const int MAX_RAM = 640;
    internal const int RAM_GRANULARITY = 32;

    internal static readonly MODEL m_ibmpc = new MODEL
    {
        name = "[8088] IBM PC",
        id = pc.ROM_IBMPC,
        internal_name = "ibmpc",
        flags = MODEL_GFX_NONE,
        min_ram = MIN_RAM,
        max_ram = MAX_RAM,
        ram_granularity = RAM_GRANULARITY,
        init = xt_init,
    };

    // pcem: models[] (device.c:16), peuplé par pcem_add_model (device.c:221) depuis
    // model_init_builtin (model.c:1625-1746). Une entrée sur les 97 de PCem.
    internal static readonly MODEL[] models = { m_ibmpc };

    // pcem: ibm.h — l'indice de la machine courante.
    internal static int model = 0;

    // pcem: model.c:148
    internal static int model_getromset() => models[model].id;

    // pcem: model.c:166
    internal static string model_get_internal_name() => models[model].internal_name;

    // pcem: model.c:168-178
    // DEVIATION: le C rend l'INDICE 0 EN SILENCE quand le nom est inconnu — donc une
    //   AMI XT clone au lieu de la machine demandée, sans un mot. Ici -1, que
    //   loadconfig transforme en refus nommé. Même politique que le contrôle de ROM
    //   d'initpc : deux fautes différentes méritent deux messages.
    internal static int model_get_model_from_internal_name(string s)
    {
        int c = 0;

        while (c < models.Length)
        {
                if (models[c].internal_name == s)
                        return c;
                c++;
        }

        return -1;
    }

    // omitted: model_getmodel (model.c:152-162) — sa boucle attend une sentinelle
    //   `id == -1` que pcem_add_model n'écrit JAMAIS, donc elle déréférence NULL quand
    //   le romset est introuvable. Aucun appelant utile ici.
    // omitted: model_count, model_getname, model_getdevice, model_get_config_int/string
    //   (model.c:140-190) — registre de machines et config par machine, sans objet
    //   avec une seule entrée sans device_t.

    // pcem: model.c:192-200
    internal static void common_init()
    {
        dma.dma_init();
        Floppy.fdc_c.fdc_add();
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
        Keyboard.keyboard_xt.keyboard_xt_init();
        nmi.nmi_init();
        // omitted: device_add(&gameport_device) — port jeu, hors périmètre.
        // omitted: device_add(&cassette_device) — port cassette du 5150 ; le BIOS
        //   le teste mais n'échoue pas en son absence (il bascule sur BASIC).
    }

    // pcem: model.c:686-694
    internal static void model_init()
    {
        // mem_size est posé par initpc, avant mem_alloc (voir pc.cs).
        models[model].init?.Invoke();
        // omitted: device_add(models[model]->device) (model.c:693) — m_ibmpc n'a pas
        //   de device_t de configuration.
    }
}
