// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/models/model.c  (common_init :192-200, xt_init :202-211,
//         at_init :335-351, at_neat_init :452-455, m_ibmpc :777-778, m_ibmxt :782-783,
//         m_ami286 :986-995, m_ibmat :1106-1115, m_ami386 :1238-1247, model_init :686-694,
//         model_getromset :148, model_get_model_from_internal_name :168-178)
//         + includes/public/pcem/devices.h:69-82 (MODEL, et son membre cpu[5] depuis M16)
// STATUS: partial — cinq machines sur 97 : IBM PC 5150, IBM XT 5160, IBM AT 5170,
//         le clone AMI 286, et le clone AMI 386SX sans son init (G2, D0.2). Les 92
//         autres MODEL et leurs fonctions d'init sont omises.
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

// pcem: devices.h:73-76 — `struct { char name[8]; CPU *cpus; } cpu[5];`
// DEVIATION: struct ANONYME chez PCem, nommée ici — C# n'a pas de type anonyme en champ.
internal struct MODEL_cpu
{
    internal string name;
    internal Cpu.CPU[]? cpus;

    internal MODEL_cpu(string name, Cpu.CPU[]? cpus)
    {
        this.name = name;
        this.cpus = cpus;
    }
}

// pcem: includes/public/pcem/devices.h:69-82, RÉDUIT.
// omitted: `device` — le device_t de configuration par machine (PCjr, Amstrad,
//   Xi8088) ; les quatre machines du dépôt le laissent à NULL.
internal sealed class MODEL
{
    internal string name = "";

    // pcem: c'est le ROMSET, valeur de l'énumération ROM_* (ibm.h:164+).
    internal int id;

    // pcem: LA CLÉ ÉCRITE DANS LE .cfg, 24 octets chez PCem.
    internal string internal_name = "";

    // pcem: les paquets de CPU de la machine, un par FABRICANT (cpu_manufacturer) ;
    // `cpu` indexe l'entrée dans la table. CINQ places, comme en C : un fabricant de 3
    // ou 4 doit tomber sur un `cpus` nul — le test de cpu.c:171 —, pas hors du tableau.
    internal MODEL_cpu[] cpu = new MODEL_cpu[5];

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
        cpu = [new("", Cpu.cpu_tables.cpus_8088), new("", null), new("", null), new(), new()],
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
        cpu = [new("", Cpu.cpu_tables.cpus_8088), new("", null), new("", null), new(), new()],
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
    // LA CONFIGURATION DU PROCESSEUR N'EST PAS ICI, et c'est la place du C : PCem la
    // pose par cpu_set(), appelé depuis resetpchard AVANT mem_alloc (pc.c:363). Jusqu'à
    // M16 ce dépôt appelait cpu_config_286() à cette ligne-ci, faute d'avoir porté
    // cpu_set() — donc APRÈS mem_alloc, qui lit cpu_16bitbus pour plafonner la RAM.
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

    // pcem: model.c:482-485 — at_headland_init : at_init puis le chipset (G3.1).
    internal static void at_headland_init()
    {
        at_init();
        headland.headland_init();
    }

    // pcem: model.c:492-495 — at_opti495_init : at_init puis le chipset (G3.2).
    internal static void at_opti495_init()
    {
        at_init();
        opti495.opti495_init();
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
        cpu = [new("", Cpu.cpu_tables.cpus_286), new("", null), new("", null), new(), new()],
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
    // `cpus_ibmat` (model.c:1109) : 286/6 et 286/8, à TROIS cycles de lecture et
    // d'écriture mémoire. Jusqu'à M16 l'AT de ce dépôt avait ceux de cpus_286[0], DEUX,
    // des deux côtés et sans que rien ne le signale : c'est le levier B de M16 qui l'a
    // rendu à sa table (VERIFICATION.md § M16, étape 5).
    internal static readonly MODEL m_ibmat = new MODEL
    {
        name = "[286] IBM AT",
        id = pc.ROM_IBMAT,
        internal_name = "ibmat",
        cpu = [new("", Cpu.cpu_tables.cpus_ibmat), new("", null), new("", null), new(), new()],
        flags = MODEL_GFX_NONE | MODEL_AT,
        min_ram = 256,
        max_ram = 15872,
        ram_granularity = 128,
        init = ibm_at_init,
    };

    // pcem: model.c:1238-1247 — LE CLONE AMI 386SX, entrée en G2 (D0.2) AVANT sa machine.
    //
    // Le cœur 386 a besoin d'un cpu_set() qui lise un 386, et cpu_set() lit
    // models[model] : il faut donc la machine dans la table, des deux côtés (l'oracle la
    // déclare dans harness_stubs.c). Mais son init, at_headland_init (model.c:482-485),
    // attend le chipset Headland, qui est le bloc G3 de PLAN.md. En G3.1, le chipset
    // (headland.cs) et at_headland_init sont transcrits, et l'oracle lie headland.c.
    //
    // omitted: {"AMD", cpus_Am386SX} et {"Cyrix", cpus_486SLC} (model.c:1241) — la table
    //   Intel seule, des deux côtés, pour que le fuzzeur compare le même processeur.
    internal static readonly MODEL m_ami386 = new MODEL
    {
        name = "[386SX] AMI 386SX clone",
        id = pc.ROM_AMI386SX,
        internal_name = "ami386",
        cpu = [new("Intel", Cpu.cpu_tables.cpus_i386SX), new("", null), new("", null), new(), new()],
        flags = MODEL_GFX_NONE | MODEL_AT | MODEL_HAS_IDE,
        min_ram = 512,
        max_ram = 16384,
        ram_granularity = 128,
        init = at_headland_init,
    };

    // pcem: model.c:1340-1349 — LE CLONE AMI 386DX (G3.2), chipset OPTi 82C495.
    //
    // PIÈGE D'UNITÉS : min_ram, max_ram et ram_granularity y sont en Mo, et non en Ko —
    // PCem le décide sur « MODEL_AT et granularité < 128 » (pc.c:695-700). pc.cs convertit.
    //
    // omitted: {"AMD", cpus_Am386DX} et {"Cyrix", cpus_486DLC} (model.c:1343) — la table
    //   Intel seule, des deux côtés, comme pour l'ami386.
    internal static readonly MODEL m_ami386dx = new MODEL
    {
        name = "[386DX] AMI 386DX clone",
        id = pc.ROM_AMI386DX_OPTI495,
        internal_name = "ami386dx",
        cpu = [new("Intel", Cpu.cpu_tables.cpus_i386DX), new("", null), new("", null), new(), new()],
        flags = MODEL_GFX_NONE | MODEL_AT | MODEL_HAS_IDE,
        min_ram = 1,
        max_ram = 256,
        ram_granularity = 1,
        init = at_opti495_init,
    };

    // pcem: models[] (device.c:16), peuplé par pcem_add_model (device.c:221) depuis
    // model_init_builtin (model.c:1625-1746). Cinq entrées sur les 97 de PCem.
    //
    // L'ORDRE COMPTE : `model` vaut 0 sans configuration, donc la première entrée est
    // la machine par défaut. Toute mesure de VERIFICATION.md suppose le 5150 ; déplacer
    // m_ibmpc d'ici les invaliderait toutes sans qu'une seule porte ne rougisse.
    // TOUTE MACHINE NEUVE ENTRE EN DERNIER, et ce n'est pas un détail de style :
    // l'insérer ailleurs qu'à la fin décalerait les indices de celles qui suivent et
    // changerait la machine par défaut sans qu'une seule porte ne rougisse. m_ibmat
    // est entrée ainsi, puis m_ami286 derrière elle — d'où l'ordre ci-dessous.
    internal static readonly MODEL[] models = { m_ibmpc, m_ibmxt, m_ibmat, m_ami286, m_ami386, m_ami386dx };

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
        serial.serial1_init(0x3f8, 4, 1);
        serial.serial2_init(0x2f8, 3, 1);
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
        // omitted: pclog("Initting as %s") (model.c:687) — sortie pure.
        // pcem: model.c:688 — sans elle, une machine 8088 amorcée APRÈS un AT dans le même
        // processus garde AT = 1 : pc_reset prend la branche AT de setpitclock et resetx86
        // le vecteur du 286. Omise sans marque jusqu'à M16, dont cpu-config-check est le
        // premier outil à amorcer plusieurs machines d'affilée.
        Cpu.x86.AMSTRAD = Cpu.x86.AT = Cpu.x86.PCI = Cpu.x86.TANDY = Cpu.x86.MCA = 0;
        // omitted: ide_set_bus_master(NULL, …) (model.c:689) — pas de contrôleur IDE.

        // mem_size est posé par initpc, avant mem_alloc (voir pc.cs).
        models[model].init?.Invoke();
        // omitted: device_add(models[model]->device) (model.c:693) — m_ibmpc n'a pas
        //   de device_t de configuration.
    }
}
