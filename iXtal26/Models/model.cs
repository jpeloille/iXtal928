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

    // DEVIATION: la RAM par défaut d'AVANT toute configuration. pc.cs:94 est un
    //   initialiseur de champ : il s'exécute avant qu'un modèle soit choisi et ne peut
    //   donc pas lire models[model]. Ce sont les 640 Ko du 5150, délibérément, parce que
    //   toute mesure consignée dans VERIFICATION.md les suppose — un défaut qui dérive
    //   ferait cesser en silence toutes ces mesures d'être reproductibles.
    //   Les bornes de VALIDATION, elles, viennent du modèle (pc.cs:148).
    internal const int DEFAULT_RAM = 640;

    // pcem: model.c:777-778
    //   MODEL m_ibmpc = {"[8088] IBM PC", ROM_IBMPC, "ibmpc", {{"", cpus_8088}, ...},
    //                    MODEL_GFX_NONE, 64, 640, 32, xt_init, NULL};
    internal static readonly MODEL m_ibmpc = new MODEL
    {
        name = "[8088] IBM PC",
        id = pc.ROM_IBMPC,
        internal_name = "ibmpc",
        flags = MODEL_GFX_NONE,
        min_ram = 64,
        max_ram = 640,
        ram_granularity = 32,
        init = xt_init,
    };

    // pcem: model.c:782-783
    //   MODEL m_ibmxt = {"[8088] IBM XT", ROM_IBMXT, "ibmxt", {{"", cpus_8088}, ...},
    //                    MODEL_GFX_NONE, 64, 640, 64, xt_init, NULL};
    //
    // MÊME init que le 5150 : PCem câble les deux machines avec xt_init (model.c:202).
    // Tout le delta tient au romset, à la granularité RAM — 64 et non 32 — et à la
    // cassette, que seul le 5150 reçoit et que xt_init omet déjà des deux côtés.
    internal static readonly MODEL m_ibmxt = new MODEL
    {
        name = "[8088] IBM XT",
        id = pc.ROM_IBMXT,
        internal_name = "ibmxt",
        flags = MODEL_GFX_NONE,
        min_ram = 64,
        max_ram = 640,
        ram_granularity = 64,
        init = xt_init,
    };

    // pcem: model.c:335-346 — at_init, LA MACHINE AT.
    //
    // CE QUI SÉPARE UN AT D'UN XT tient en six gestes, et chacun compte :
    //   - AT = 1, qui aiguille runpc vers exec386 ET fait prendre à resetx86 le
    //     vecteur F000:FFF0 au lieu de FFFF:0000, et rammask 24 bits ;
    //   - pit_refresh_timer_at au lieu de _xt : le rafraîchissement mémoire bascule
    //     un bit du PPI au lieu de déclencher un cycle DMA, et le POST le COMPTE ;
    //   - dma16_init : le second 8237, celui des transferts 16 bits ;
    //   - keyboard_at_init : le 8042, qui tient A20 ET la ligne de reset — seule
    //     sortie du mode protégé d'un 286 ;
    //   - nvr_device : le MC146818, horloge et CMOS, que le POST lit avant tout ;
    //   - pic2_init : le second 8259, cascadé sur l'IRQ 2, sans qui l'IRQ 8 de
    //     l'horloge n'a personne à qui parler.
    //
    // cpu_config_286() N'EST PAS DANS LE C, et c'est une DEVIATION assumée. PCem la
    // fait par cpu_set(), appelé depuis resetpchard, que ce dépôt n'a pas porté —
    // cpu.c n'est réduit qu'à la vitesse du 8088. L'oracle fait le même geste au même
    // endroit : h_cpu_config_286() est appelé avant resetx86() (harness.c:374), pour
    // la même raison qu'ici — resetx86 BRANCHE sur AT, is386 et cpu_16bitbus.
    // Elle vit dans 386.State.cs, avec les autres pendants de h_*.
    //
    // mem_add_bios EST APPELÉ APRÈS `AT = 1`, COMME DANS LE C, et sa branche AT reste
    // omise. Ce n'est pas une contradiction mais un accord mesuré :
    //   - le C mappe 0xE0000-0xEFFFF quand AT, ce que mem.cs omet explicitement ;
    //   - L'ORACLE NE LA CRÉE PAS NON PLUS : son inline appelle mem_add_bios() AVANT
    //     de poser AT = 1 (harness.c:847 puis :852), donc la branche est fausse chez
    //     lui aussi. Les deux côtés s'accordent, et diffèrent de PCem.
    //   - Et ce mappage serait un ALIAS : biosmask vaut 0xffff pour l'AT
    //     (mem_bios.c:64, que le case ROM_IBMAT ne change pas), donc
    //     `rom + (0x20000 & biosmask)` vaut `rom + 0` — les mêmes 64 Ko une seconde
    //     fois. Le POST atteint l'écran sans, mesuré.
    // NE PAS « corriger » l'ordre : déplacer AT = 1 après mem_add_bios ne changerait
    // rien ici et ferait mentir la citation.
    internal static void at_init()
    {
        Cpu.x86.AT = 1;
        Cpu._386.cpu_config_286();
        common_init();
        mem.mem_add_bios();
        pit.pit_set_out_func(pit.pit_, 1, pit.pit_refresh_timer_at);
        dma.dma16_init();
        Keyboard.keyboard_at.keyboard_at_init();
        PluginApi.device.device_add(Devices.nvr.nvr_device);
        pic.pic2_init();
        Cpu._808x.nmi_mask = 0;
        // omitted: device_add(&gameport_device) — port jeu, hors périmètre, et
        //   l'oracle ne le lie pas davantage (harness.c le dit sur place).
        // omitted: nmi_init() — c'est le XT qui l'appelle, pas l'AT : sur un AT le
        //   masque de NMI est le bit 7 du port 0x70, tenu par writenvr.
    }

    // pcem: model.c:348-351
    //
    // omitted: mem_remap_top_384k() (model.c:350) — son SEUL apport sur at_init, et il
    //   est inerte ici, mesuré deux fois :
    //     1. l'oracle ne l'appelle JAMAIS — `grep -n 'mem_remap' tools/oracle/` rend
    //        zéro, son inline de at_init s'en passe ;
    //     2. son corps est gardé par `if (mem_size > 640)` (mem.c:1295), et un AT de
    //        ce dépôt tourne à 512 Ko — c'est la taille que la sonde pose et celle que
    //        le POST affiche, « 00512 KB OK ».
    //   L'écrire créerait une divergence là où il n'y en a pas.
    internal static void ibm_at_init()
    {
        at_init();
    }

    // pcem: model.c:452-455 — at_neat_init : at_init puis le chipset.
    internal static void at_neat_init()
    {
        at_init();
        neat.neat_init();
    }

    // pcem: model.c:986-995 — LE CLONE AMI 286, seconde machine à 286 du dépôt.
    //
    // POURQUOI ELLE EXISTE ICI : le BIOS de l'IBM AT affiche « 161-System Options Not
    // Set-(Run SETUP) » sans pile CMOS valide, et son SETUP est sur la disquette de
    // diagnostics, absente. Un BIOS AMI porte le sien EN ROM. C'est la seule façon,
    // avec ce que le dépôt a, d'obtenir un 286 qui démarre sans invite.
    //
    // ET ELLE VÉRIFIE LE CŒUR AUTREMENT : un second BIOS emprunte d'autres chemins du
    // mode protégé que celui d'IBM, ce qui vaut mieux qu'une seconde configuration de
    // la même machine — même argument qu'à M10 pour le XT 5160.
    //
    // MODEL_HAS_IDE est porté FIDÈLEMENT alors que ce dépôt n'a aucun contrôleur IDE :
    // src/ide/ est au registre des omissions. Le flag ne fait qu'autoriser l'interface
    // à proposer un disque IDE ; le retirer serait réécrire la table.
    internal static readonly MODEL m_ami286 = new MODEL
    {
        name = "[286] AMI 286 clone",
        id = pc.ROM_AMI286,
        internal_name = "ami286",
        flags = MODEL_GFX_NONE | MODEL_AT | MODEL_HAS_IDE,
        min_ram = 512,
        max_ram = 16384,
        ram_granularity = 128,
        init = at_neat_init,
    };

    // pcem: model.c:1106-1115
    //
    // LE PREMIER MODÈLE DU DÉPÔT QUI N'EST PAS UN 8088, et son flag MODEL_AT est ce
    // que BootDiff lit pour choisir le cœur des DEUX côtés, avant l'amorçage.
    //
    // omitted: le membre `cpu` — `{{"", cpus_ibmat}, {"", NULL}, {"", NULL}}`. La
    //   struct MODEL de ce dépôt ne le porte pas : les tables de CPU appartiennent à
    //   cpu.c, réduit à la vitesse du 8088, et cpu_config_286() tient lieu de l'entrée
    //   qu'on y lirait. L'ORACLE EST DANS LE MÊME ÉTAT, et c'est mesuré :
    //   models[ROM_IBMAT]->cpu[0].cpus est NUL côté oracle (at-probe, offset 104), et
    //   les deux fonctions de cpu.c qui le déréférencent sont enveloppées à vide.
    internal static readonly MODEL m_ibmat = new MODEL
    {
        name = "[286] IBM AT",
        id = pc.ROM_IBMAT,
        internal_name = "ibmat",
        flags = MODEL_GFX_NONE | MODEL_AT,
        min_ram = 256,
        max_ram = 15872,
        ram_granularity = 128,
        init = ibm_at_init,
    };

    // pcem: models[] (device.c:16), peuplé par pcem_add_model (device.c:221) depuis
    // model_init_builtin (model.c:1625-1746). Deux entrées sur les 97 de PCem.
    //
    // L'ORDRE COMPTE : `model` vaut 0 sans configuration, donc la première entrée est
    // la machine par défaut. Toute mesure de VERIFICATION.md suppose le 5150 ; déplacer
    // m_ibmpc d'ici les invaliderait toutes sans qu'une seule porte ne rougisse.
    // TOUTE MACHINE NEUVE ENTRE EN DERNIER, et ce n'est pas un détail de style :
    // l'insérer ailleurs qu'à la fin décalerait les indices de celles qui suivent et
    // changerait la machine par défaut sans qu'une seule porte ne rougisse. m_ibmat
    // est entrée ainsi, puis m_ami286 derrière elle — d'où l'ordre ci-dessous.
    internal static readonly MODEL[] models = { m_ibmpc, m_ibmxt, m_ibmat, m_ami286 };

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
