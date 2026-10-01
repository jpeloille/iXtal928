// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/pc.c  (initpc :178-300, resetpc_cad :344-351,
//         resetpchard :353-442, runpc :470-553, closepc :576-592)
//
// LA PLAGE DE resetpchard DISAIT :353-400 ET LA FONCTION VA JUSQU'A 442. Mesure par
// appariement d'accolades, pas a l'oeil. Les quarante-deux lignes de queue n'etaient
// donc ni transcrites ni inscrites au registre — un trou qu'aucun oracle ne pouvait
// signaler, puisque rien ne les reclamait.
// omitted: keyboard_at_reset() (pc.c:405) — appel INCONDITIONNEL, le second apres
//   celui de model_init (:372). Idempotent aujourd'hui : son corps n'est que des
//   constantes plus video_is_mda(), et aucun code invite ne tourne entre les deux.
//   Il CESSE de l'etre des qu'un second resetpchard tourne sur un 8042 deja
//   interroge, keyboard_at.cs incrementant les deux bits bas de input_port a chaque
//   commande 0xC0.
// omitted: image_close() (:411) et le bloc cdrom — lecteur de CD-ROM, hors cible.
// omitted: mem_set_704kb() (:370-371) — garde `!AT && max_ram <= 768`, fausse pour les
//   trois machines du depot. NOTE : le C teste `AT` AVANT que model_init() ne le pose,
//   donc sur un changement de machine a chaud il lit le AT de la PRECEDENTE. Defaut
//   de PCem, sans effet ici, mais c'est le meme piege d'ordre que AT=1 vs mem_add_bios.
// STATUS: partial — quatre machines : IBM PC 5150 et XT 5160 (8088), IBM AT 5170 et
//         clone AMI 286 (286, sélection de CPU par table depuis M16) ; disquette,
//         disque dur MFM, CMOS, CGA et VGA, haut-parleur. Ni réseau, ni souris, ni
//         joystick, ni son. Ce qui reste est la séquence d'amorçage et la tranche.
//
// pc.c n'est pas une boucle : runpc() est une TRANCHE de 10 ms
// (cpu_get_speed() / 100 cycles). Le cadençage sur horloge murale vit dans l'hôte
// (wx-sdl2.c:159-190), pas ici — voir Host/SdlHost.cs.

using iXtal26.Cpu;
using iXtal26.Diag;
using iXtal26.Memory;
using static iXtal26.Cpu.x86;

namespace iXtal26;

internal static partial class pc
{
    // pcem: ibm.h:163-265 — l'énumération des romsets. Deux sont des cibles depuis
    // M10, ROM_IBMPC et ROM_IBMXT, une troisième depuis B2, ROM_IBMAT ; les autres
    // existent parce que les périphériques transcrits (clavier, fdc, mem_bios)
    // branchent dessus, et qu'on ne réécrit pas leurs conditions.
    //
    // CETTE TABLE DISAIT « DANS L'ORDRE DU C » ET QUATRE ENTRÉES MENTAIENT. Les
    // premières vingt-six suivent l'énum ; les quatre suivantes avaient reçu 26, 27,
    // 28 et 29 — les places libres — au lieu de leurs vraies valeurs 82, 94, 65 et
    // 100. Corrigé ici, en comparant la table entière à l'énum et non en la relisant :
    // 102 entrées en C, quatre désaccords.
    //
    // LATENT ET NON VIVANT, mais il fallait le corriger avant d'en ajouter d'autres :
    // `romset` TRAVERSE L'ABI — BootDiff et AtProbe font h_set_romset(pc.romset) —
    // donc une valeur fausse ici nomme une AUTRE machine côté oracle. ROM_XI8088 = 26
    // désignait ROM_CMDPC30. Sans conséquence aujourd'hui parce que romset ne vaut
    // jamais que 0, 1 ou 25, et que les quatre ne sont lues que dans des `==`.
    internal const int ROM_IBMPC = 0;
    internal const int ROM_IBMXT = 1;
    internal const int ROM_IBMPCJR = 2;
    internal const int ROM_GENXT = 3;
    internal const int ROM_CBM_PC10 = 4;
    internal const int ROM_HYUNDAI_SUPER16T = 5;
    internal const int ROM_HYUNDAI_SUPER16TE = 6;
    internal const int ROM_DTKXT = 7;
    internal const int ROM_EUROPC = 8;
    internal const int ROM_OLIM24 = 9;
    internal const int ROM_TANDY = 10;
    internal const int ROM_PC1512 = 11;
    internal const int ROM_PC200 = 12;
    internal const int ROM_PC1640 = 13;
    internal const int ROM_PC2086 = 14;
    internal const int ROM_PC3086 = 15;
    internal const int ROM_AMIXT = 16;
    internal const int ROM_LTXT = 17;
    internal const int ROM_LXT3 = 18;
    internal const int ROM_PX386 = 19;
    internal const int ROM_DTK386 = 20;
    internal const int ROM_PXXT = 21;
    internal const int ROM_JUKOPC = 22;
    internal const int ROM_TANDY1000HX = 23;
    internal const int ROM_TANDY1000SL2 = 24;
    internal const int ROM_IBMAT = 25;
    internal const int ROM_XI8088 = 82;  // hors cible, présent pour les gardes
    internal const int ROM_LEDGE_MODELM = 94;  // hors cible, présent pour les gardes
    internal const int ROM_ATARIPC3 = 65;  // hors cible, présent pour les gardes
    internal const int ROM_PC5086 = 100;

    // B1b : les deux que keyboard_at.cs lit. ROM_IBMXT286 apparaît dans la SEULE
    // condition de keyboard_at_read qui décide si l'accès coûte huit cycles ISA, et
    // un IBM AT est l'autre moitié de cette condition. ROM_T3100E garde une douzaine
    // de branches du 8042, toutes vers le Toshiba, non transcrit.
    // ROM_AMI286 = 27 dans l'énum du C, et 26-27 étaient les deux seules places encore
    // libres après ROM_IBMAT — d'où la correction de 84e39ec, qui avait rendu leurs
    // vraies valeurs aux quatre entrées squattant ici.
    internal const int ROM_AMI286 = 27;
    // G2, D0.2 : l'ami386, dont le cœur 386 a besoin pour que cpu_set() lise un 386.
    // Valeur lue par le compilateur sur ibm.h (43), pas comptée à la main.
    internal const int ROM_AMI386SX = 43;
    // G3.2 : l'ami386dx, valeur lue par le compilateur sur ibm.h (58).
    internal const int ROM_AMI386DX_OPTI495 = 58;
    // G6.1 : l'ami486, valeur relevée sur l'énum de ibm.h (46).
    internal const int ROM_AMI486 = 46;
    internal const int ROM_IBMXT286 = 66;  // hors cible, présent pour les gardes
    internal const int ROM_T3100E = 70;    // hors cible, présent pour les gardes  // hors cible, présent pour les gardes (fdc.c:98, :628)

    // pcem: ibm.h:272 — le romset courant, défini par pc.c chez PCem.
    internal static int romset = ROM_IBMPC;

    internal static int framecount, framecountx;

    // pcem: pc.c:77 — la carte vidéo, en identifiant HÉRITÉ (GFX_*) : video_old_to_new
    // le traduit en indice de video_cards[]. Zéro, donc la CGA, tant que rien ne le pose
    // — le défaut de PCem quand la clé gfxcard est absente (pc.c:660-664), et celui de
    // toutes les mesures déjà consignées.
    internal static int gfxcard;

    // pcem: ibm.h:274-318 — les valeurs de l'énumération GFX_* que ce dépôt lit.
    // GFX_VGA vaut 13, GFX_TVGA 4, GFX_CL_GD5429 19, GFX_PHOENIX_TRIO64 22, GFX_TVGA9000B 42 : leur place dans l'énumération,
    // pas un choix.
    internal const int GFX_BUILTIN = -1;
    internal const int GFX_CGA = 0;
    internal const int GFX_TVGA = 4; /*Using Trident TVGA8900D BIOS*/
    internal const int GFX_VGA = 13;
    internal const int GFX_CL_GD5429 = 19; /*Cirrus Logic CL-GD5429*/
    internal const int GFX_PHOENIX_TRIO64 = 22; /*S3 764/Trio64 (Phoenix)*/
    internal const int GFX_TVGA9000B = 42; /*Trident TVGA9000B*/

    // pcem: pc.c:78 — témoin d'activité disque, lu par la barre d'état de l'hôte.
    internal static int readflash;

    // pcem: pc.c:87 — lu par svga_doblit. Réglage de la configuration GLOBALE
    // (pc.c:622), tiers au registre des omissions : il garde le défaut de PCem, 0.
    internal static int vid_resize;

    // pcem: pc.c:89 — svga_read et svga_write y ajoutent le coût de chaque accès ; seule
    // la barre d'état de l'interface le lit chez PCem.
    internal static int cycles_lost = 0;

    // pcem: ibm.h:19-23 — les macros readflash_*.
    internal const int READFLASH_FDC = 0;
    internal const int READFLASH_HDC = 4;
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    internal static void readflash_set(int offset, int drive) { readflash |= 1 << ((offset) + (drive)); }

    // pcem: pc.c:109-120 — M21.
    internal static int pollmouse_delay = 2;
    internal static void pollmouse()
    {
        int x, y, z;
        pollmouse_delay--;
        if (pollmouse_delay != 0)
                return;
        pollmouse_delay = 2;
        Mouse.mouse.mouse_poll_host?.Invoke();
        Mouse.mouse.mouse_get_mickeys(out x, out y, out z);
        Mouse.mouse.mouse_poll(x, y, z, Mouse.mouse.mouse_buttons);
    }

    // DEVIATION: fatal() de PCem appelle dumpregs() puis exit(-1). Ici on lève :
    //   l'hôte décide quoi en faire, et un test n'a pas à voir son processus
    //   disparaître.
    internal static void fatal(string s)
    {
        Counters.n_fatal++;
        Console.Error.Write("iXtal26 FATAL: " + s);
        throw new InvalidOperationException(s);
    }

    // DEVIATION: chez PCem, loadconfig() écrit mem_size directement et pc_main est le
    //   SEUL appelant d'initpc (wx-sdl2.c:444). iXtal26 en a quatre — Program, BootTest,
    //   TimerCheck, BootDiff — et trois d'entre eux ne lisent aucun fichier. La valeur
    //   configurée vit donc ici, et initpc la consomme : ceux qui n'appellent pas
    //   loadconfig obtiennent le DÉFAUT, c'est-à-dire exactement la machine d'avant M8.
    //   Ce défaut n'est pas un réglage : c'est ce qui garde reproductibles toutes les
    //   mesures déjà consignées (§ M4.1 « 640 Ko », § M6 « 26 750 702 instructions »).
    internal static int cfg_mem_size = Models.model_c.DEFAULT_RAM;

    // pcem: pc.c:776-777 — drive_a_type / drive_b_type. Défaut PCem : 7 (3,5" ED),
    // celui d'une machine moderne. Ici 1 (5,25" DD), le lecteur du 5150, comme
    // h_boot et comme les deux fdd_set_type qu'initpc portait en dur.
    internal static int[] cfg_drive_type = { 1, 1 };

    // pcem: hdd.c:22 — `hdd_controller_name`. Vit ici et non dans Disc/hdd.cs pour la
    // même raison que cfg_mem_size et cfg_drive_type : la valeur CONFIGURÉE est
    // consommée par resetpchard, et les points d'entrée qui ne lisent aucun fichier
    // doivent obtenir le défaut — c'est-à-dire aucune carte.
    internal static string cfg_hdd_controller = "";

    // pcem: pc.c:655 — la clé `fpu`, internal_name du coprocesseur (« none », « 8087 »,
    // « 287 », « 287xl », « 387 »). G4.1. Défaut « none », celui de PCem.
    // DEVIATION: PCem la résout aussitôt contre la machine et le CPU du fichier (pc.c:656).
    //   Ici --model et --cpu peuvent s'appliquer APRÈS le fichier : la chaîne est gardée, et
    //   initpc la résout contre la machine FINALE, après check_cpu — même raison que
    //   l'indice de CPU. Un nom absent de la table de la machine donne son entrée 0,
    //   « none », comme fpu_get_type.
    internal static string cfg_fpu = "none";

    /// <summary>
    /// pcem: pc.c:643-652. Choisit la machine par son internal_name et en déduit le
    /// romset. Partagé par la clé `model` du fichier et par l'option --model : les deux
    /// doivent refuser de la MÊME façon, sinon la ligne de commande ouvrirait une porte
    /// que le fichier ferme.
    ///
    /// DEVIATION: PCem retombe sur l'indice 0 EN SILENCE (model.c:177) et démarre une
    ///   autre machine que celle demandée, sans un mot. On refuse, en citant ce qui
    ///   existe : c'est la seule façon de ne pas mesurer une machine pour une autre. Le
    ///   clamp `if (model >= model_count())` (pc.c:649) devient sans objet.
    /// </summary>
    internal static bool setmodel(string mname)
    {
        int m = Models.model_c.model_get_model_from_internal_name(mname);

        if (m < 0)
        {
                Console.Error.WriteLine($"model = « {mname} » : machine inconnue. Connues :");
                foreach (Models.MODEL k in Models.model_c.models)
                        Console.Error.WriteLine($"  {k.internal_name}  ({k.name})");
                return false;
        }

        Models.model_c.model = m;
        romset = Models.model_c.model_getromset();  /* pc.c:652 */
        return true;
    }

    /// <summary>
    /// pcem: pc.c:660-664. Choisit la carte vidéo par son internal_name — "cga" ou
    /// "vga" — et pose gfxcard, l'identifiant HÉRITÉ. Partagé par la clé `gfxcard` du
    /// fichier et par l'option --gfxcard, pour la même raison que setmodel.
    ///
    /// DEVIATION: video_get_video_from_internal_name rend 0 — la CGA — pour un nom
    ///   inconnu, et PCem démarre alors EN SILENCE une autre carte que celle demandée.
    ///   On refuse en citant ce qui existe, comme setmodel. Le nom VIDE, lui, garde le
    ///   comportement de PCem : c'est la clé absente, donc la CGA.
    ///
    /// La présence de la ROM n'est PAS vérifiée ici : les chemins de ROM ne sont posés
    /// que par initpc, APRÈS la configuration. Une ROM absente le dit alors par le pclog
    /// de rom_init (« ROM image not found : ibm_vga.bin »), comme chez PCem.
    /// </summary>
    internal static bool setgfxcard(string name)
    {
        int c;

        for (c = 0; c < Video.video.video_cards.Length; c++)
        {
                if (Video.video.video_cards[c].internal_name == name)
                        break;
        }
        if (name.Length > 0 && c == Video.video.video_cards.Length)
        {
                Console.Error.WriteLine($"gfxcard = « {name} » : carte inconnue. Connues :");
                foreach (Video.VIDEO_CARD k in Video.video.video_cards)
                        Console.Error.WriteLine($"  {k.internal_name}  ({k.name})");
                return false;
        }

        gfxcard = Video.video.video_get_video_from_internal_name(name);
        return true;
    }

    /// <summary>
    /// Contrôle le processeur choisi — fabricant et indice — contre la table de la
    /// machine COURANTE, et refuse en citant ce qui existe.
    ///
    /// DEVIATION: PCem ne valide rien. Un fabricant sans table retombe EN SILENCE sur
    ///   0/0 (cpu.c:171-175) ; un indice égal à la sentinelle atteint le `default:
    ///   fatal` de cpu.c:1128 ; au-delà, c'est une lecture hors du tableau. Son interface
    ///   ramène l'indice dans la table (wx-config.c:957-959) — une ligne de commande n'a
    ///   pas d'interface, et une valeur tapée est explicite : même politique que
    ///   setmodel et check_mem_size, qui refusent plutôt que démarrer autre chose.
    /// </summary>
    internal static bool check_cpu()
    {
        var mdl = Models.model_c.models[Models.model_c.model];
        var manu = cpu_c.cpu_manufacturer;
        var cpus = manu >= 0 && manu < mdl.cpu.Length ? mdl.cpu[manu].cpus : null;

        var n = 0;
        while (cpus is not null && cpus[n].cpu_type != -1)
                n++;

        if (cpus is not null && cpu_c.cpu >= 0 && cpu_c.cpu < n)
                return true;

        Console.Error.WriteLine(cpus is null
            ? $"cpu_manufacturer = {manu} : « {mdl.internal_name} » n'a pas de processeur de ce fabricant. Connu : 0."
            : $"cpu = {cpu_c.cpu} : hors de la table de « {mdl.internal_name} ». Connus :");
        var table = mdl.cpu[0].cpus!;
        for (var c = 0; table[c].cpu_type != -1; c++)
                Console.Error.WriteLine($"  cpu = {c}  ({table[c].name})");
        return false;
    }

    /// <summary>
    /// pcem: pc.c:695-700 — les bornes RAM d'une machine, en Ko. PCem y cache un piège
    /// d'unités : min_ram, max_ram et ram_granularity sont en Ko, SAUF pour un AT à
    /// granularité inférieure à 128, où ils sont en Mo. Vivant depuis l'ami386dx (G3.2).
    /// </summary>
    internal static (int minKb, int maxKb, int stepKb) ram_bounds_kb(Models.MODEL mdl)
    {
        var unit = ((mdl.flags & Models.model_c.MODEL_AT) != 0 && mdl.ram_granularity < 128) ? 1024 : 1;
        return (mdl.min_ram * unit, mdl.max_ram * unit, mdl.ram_granularity * unit);
    }

    /// <summary>
    /// Contrôle une taille mémoire contre les bornes de la machine COURANTE. Rend false
    /// sans rien écrire dans cfg_mem_size : à la différence de la clé de configuration,
    /// qui retombe sur un défaut, une valeur tapée en ligne de commande est explicite —
    /// la corriger en silence donnerait une machine que l'utilisateur n'a pas demandée.
    /// </summary>
    internal static bool check_mem_size(int kb)
    {
        var mdl = Models.model_c.models[Models.model_c.model];
        var (minKb, maxKb, stepKb) = ram_bounds_kb(mdl);
        if (kb >= minKb && kb <= maxKb && (kb - minKb) % stepKb == 0)
                return true;

        Console.Error.WriteLine(
            $"mem_size = {kb} : hors des bornes de « {mdl.internal_name} ». Attendu de " +
            $"{minKb} à {maxKb} Ko par pas de {stepKb}.");
        return false;
    }

    /// <summary>
    /// pcem: pc.c:607-843 (fortement réduit). PCem lit 74 clés ; soixante-quatre
    /// décrivent du matériel qu'un 5150 transcrit n'a pas — sept disques durs, CD-ROM,
    /// ZIP, son, réseau, joystick. Les autres arrivent avec les jalons qui les rendent
    /// variables (§ M8 du plan : vitesse CPU, carte vidéo, FPU).
    ///
    /// À appeler AVANT initpc, comme pc_main le fait (wx-sdl2.c:451 puis :459). Un
    /// fichier absent n'est pas une faute : config_load rend alors tous les défauts.
    /// </summary>
    internal static bool loadconfig(string fn)
    {
        PluginApi.config.config_load(PluginApi.config.CFG_MACHINE, fn);

        // pcem: pc.c:643-652 — la clé `model` porte l'internal_name, pas l'indice.
        string mname = PluginApi.config.config_get_string(
            PluginApi.config.CFG_MACHINE, null, "model", Models.model_c.model_get_internal_name());

        if (!setmodel(mname))
                return false;

        // pcem: pc.c:653-654 — le fabricant et l'INDICE dans la table de la machine :
        // `cpu = 5` est un 286/20 sur l'ami286 et n'existe pas dans la table cpus_ibmat de PCem. La validité
        // ne se juge donc que contre la machine FINALE — --model s'applique après ce
        // fichier — et c'est check_cpu(), appelé par initpc, qui la juge.
        // omitted: cpu_use_dynarec (pc.c:657), cpu_waitstates (:658) — le dynarec n'est
        //   pas porté, et l'override d'états d'attente n'a pas de machine qui le demande.
        cpu_c.cpu_manufacturer = PluginApi.config.config_get_int(PluginApi.config.CFG_MACHINE, null, "cpu_manufacturer", 0);
        cpu_c.cpu = PluginApi.config.config_get_int(PluginApi.config.CFG_MACHINE, null, "cpu", 0);
        // pcem: pc.c:655 — G4.1 ; la résolution (:656) est dans initpc, voir cfg_fpu.
        cfg_fpu = PluginApi.config.config_get_string(PluginApi.config.CFG_MACHINE, null, "fpu", "none");

        // pcem: pc.c:660-664 — la clé porte l'internal_name de la carte ; absente, "" et
        // donc la CGA.
        if (!setgfxcard(PluginApi.config.config_get_string(PluginApi.config.CFG_MACHINE, null, "gfxcard", "")))
                return false;
        // pcem: pc.c:665
        Video.video.video_speed = PluginApi.config.config_get_int(PluginApi.config.CFG_MACHINE, null, "video_speed", -1);

        // pcem: pc.c:694 — `config_get_int(CFG_MACHINE, NULL, "mem_size", 4096)`.
        // DEVIATION: le défaut de PCem est 4096 Ko, celui d'une machine 486. Ici c'est
        //   DEFAULT_RAM, la seule valeur qui reproduise la machine que VERIFICATION.md
        //   décrit. Un défaut qui dérive ferait cesser en silence toutes les mesures
        //   consignées d'être reproductibles. Ce n'est PAS max_ram du modèle : les deux
        //   valent 640 aujourd'hui, mais les confondre ferait dériver le défaut avec la
        //   première machine qui monte plus haut.
        cfg_mem_size = PluginApi.config.config_get_int(
            PluginApi.config.CFG_MACHINE, null, "mem_size", Models.model_c.DEFAULT_RAM);

        // pcem: pc.c:695-700 — le clamp de PCem ne borne QUE par le bas, et son test
        // porte un piège d'unités : min_ram est en Ko avant l'AT, en Mo pour un AT à
        // granularité < 128. Sans objet sur un 5150. On borne des DEUX côtés, parce
        // qu'une valeur hors bornes ne produirait pas une erreur mais un SW2 absurde
        // (keyboard_xt.cs:174-183 : (mem_size - 64) / 32) et un POST qui ment.
        //
        // Ce motif-là vaut pour le 5150 SEUL : sur un XT, la lecture 0x62 part dans la
        // branche `else` (keyboard_xt.cs:192-204) et ne porte plus la taille mémoire du
        // tout — le BIOS du 5160 la détermine en balayant. La borne y reste utile pour
        // une autre raison : mem_alloc dimensionne la RAM, et 96 Ko sur une machine à
        // pas de 64 serait une machine que PCem ne décrit pas.
        // Les bornes viennent du MODÈLE choisi, pas de constantes : le XT est à 64 Ko de
        // granularité là où le 5150 est à 32 (model.c:777 contre :782). Les champs
        // min_ram/max_ram/ram_granularity de MODEL existaient depuis M8 et n'étaient lus
        // par personne — une table à une seule entrée est une constante, et c'est la
        // deuxième machine qui les rend vivants.
        var mdl = Models.model_c.models[Models.model_c.model];
        // G3.2 : le piège d'unités ci-dessus devient vivant avec l'ami386dx, en Mo.
        var (minKb, maxKb, stepKb) = ram_bounds_kb(mdl);
        if (cfg_mem_size < minKb || cfg_mem_size > maxKb
            || (cfg_mem_size - minKb) % stepKb != 0)
        {
                Console.Error.WriteLine(
                    $"mem_size = {cfg_mem_size} : hors des bornes de « {mdl.internal_name} ». " +
                    $"Attendu de {minKb} à {maxKb} Ko par pas de " +
                    $"{stepKb}. On garde {(cfg_mem_size < minKb ? minKb : maxKb)}.");
                // Sous le minimum, le minimum, comme PCem (pc.c:695-700) : le 640 Ko par
                // défaut ferait sinon de l'ami386dx une machine à 256 Mo.
                cfg_mem_size = cfg_mem_size < minKb ? minKb : maxKb;
        }

        // pcem: pc.c:776-777. Le type gouverne max_track et les drapeaux de densité
        // (fdd.cs:88-112) : un type hors table indexerait hors bornes à la première
        // recherche de piste, donc on borne plutôt que de laisser lever plus tard.
        cfg_drive_type[0] = config_get_drive_type("drive_a_type", 0);
        cfg_drive_type[1] = config_get_drive_type("drive_b_type", 1);

        // pcem: pc.c:673-687 — disc_a / disc_b. La ligne de commande, si elle a parlé,
        // a déjà rempli discfns[] : PCem SAUTE alors la clé (override_drive_a,
        // pc.c:227-240) au lieu de la lire puis de l'écraser. La nuance compte quand le
        // chemin du fichier est mauvais et celui de la ligne de commande bon.
        if (Floppy.fdd_c.discfns[0].Length == 0)
                Floppy.fdd_c.discfns[0] = config_get_disc("disc_a");
        if (Floppy.fdd_c.discfns[1].Length == 0)
                Floppy.fdd_c.discfns[1] = config_get_disc("disc_b");

        // pcem: pc.c:688-692 — le nom INTERNE de la carte de disque dur.
        //
        // Le défaut est "" et non "none", et c'est reproduit : le `else` de PCem est
        // MORT, config_get_string ne rendant jamais NULL. Un nom vide ne correspond à
        // rien dans la recherche, et PCem sort alors par un `fatal` COMMENTÉ
        // (hdd.c:140) — donc sans carte et sans un mot. Effet identique à "none", par
        // un chemin différent. On ne « corrige » pas vers "none".
        cfg_hdd_controller = PluginApi.config.config_get_string(
            PluginApi.config.CFG_MACHINE, null, "hdd_controller", "");

        // pcem: pc.c:703-705 — cdrom_channel (défaut 2) et zip_channel (défaut -1).
        //
        // DEVIATION: le défaut est -1 (Ide/ide.cs) ; ATAPI est hors G5. Une configuration
        //   qui DEMANDE un lecteur de CD-ROM ou de ZIP sur un canal IDE est refusée ici,
        //   bruyamment, plutôt que de monter un disque dur à sa place.
        int cd = PluginApi.config.config_get_int(PluginApi.config.CFG_MACHINE, null, "cdrom_channel", -1);
        int zip = PluginApi.config.config_get_int(PluginApi.config.CFG_MACHINE, null, "zip_channel", -1);
        if (cd >= 0 || zip >= 0)
        {
                Console.Error.WriteLine("cdrom_channel / zip_channel : ATAPI n'est pas transcrit (PLAN-G5.md, " +
                                        "décision n° 1) ; retirer la clé, ou la poser à -1.");
                return false;
        }

        // pcem: pc.c:719-734 — géométrie et image des disques C: et D:.
        //
        // LES PRÉFIXES SONT DES LETTRES DE LECTEUR DOS : hdc_ = C:, hdd_ = D:. Le
        // Fixed Disk Adapter en gère deux (mfm_xebec.c:733, boucle sur d ∈ {0,1}),
        // d'où ces deux-là et pas plus.
        //
        // G5.1 — et E:, F: (pc.c:735-748) : les deux lecteurs du canal IDE secondaire.
        // omitted: hdg_*, hdh_*, hdi_* (pc.c:749-774) — les trois derniers, qui
        //   n'appartiennent qu'aux contrôleurs SCSI, hors périmètre. PCem déroule le bloc
        //   sept fois plutôt que de boucler ; on en garde quatre.
        for (int d = 0; d < 4; d++)
        {
                string pfx = "hd" + (char)('c' + d);
                Disc.hdd_c.hdc[d].spt = PluginApi.config.config_get_int(
                    PluginApi.config.CFG_MACHINE, null, $"{pfx}_sectors", 0);
                Disc.hdd_c.hdc[d].hpc = PluginApi.config.config_get_int(
                    PluginApi.config.CFG_MACHINE, null, $"{pfx}_heads", 0);
                Disc.hdd_c.hdc[d].tracks = PluginApi.config.config_get_int(
                    PluginApi.config.CFG_MACHINE, null, $"{pfx}_cylinders", 0);
                Disc.hdd_c.ide_fn[d] = config_get_hdd_fn($"{pfx}_fn");
        }

        // pcem: pc.c:778
        Disc.disc_img.bpb_disable = PluginApi.config.config_get_int(
            PluginApi.config.CFG_MACHINE, null, "bpb_disable", 0);

        return true;
    }

    /// <summary>
    /// Le chemin d'une image de DISQUE DUR. Distinct de config_get_disc, et pour une
    /// raison de comportement : hdd_load_ext CRÉE le fichier s'il n'existe pas
    /// (hdd_file.c:67-74). Refuser un chemin introuvable, comme on le fait pour une
    /// disquette, interdirait donc le premier démarrage sur un disque neuf.
    ///
    /// On résout si le fichier existe — pour que Rider, qui lance depuis
    /// bin/Debug/net10.0, trouve la même image que la racine du dépôt — et on rend la
    /// chaîne telle quelle sinon, ce que fait PCem, dont le répertoire courant est
    /// toujours celui de l'installation.
    /// </summary>
    private static string config_get_hdd_fn(string key)
    {
        string fn = PluginApi.config.config_get_string(
            PluginApi.config.CFG_MACHINE, null, key, "");

        if (fn.Length == 0)
                return "";

        return PluginApi.paths.resolve_file_path(fn) ?? fn;
    }

    /// <summary>
    /// pcem: pc.c:673-687, avec la résolution de chemin que PCem n'a pas besoin de
    /// faire — son répertoire courant est toujours celui de l'installation.
    ///
    /// Ici un chemin relatif doit survivre au répertoire de travail : Rider lance
    /// depuis bin/Debug/net10.0, où « os/… » n'existe pas. Et disc_load SE TAIT quand
    /// il ne trouve pas le fichier (disc.cs:132-133) : sans ce contrôle, le lecteur
    /// restait vide, la machine partait sur BASIC, et rien ne disait pourquoi.
    /// C'est la même politique que --floppy-a, qui refuse une image absente en citant
    /// son chemin plutôt que de laisser le silence décider.
    /// </summary>
    private static string config_get_disc(string key)
    {
        string fn = PluginApi.config.config_get_string(
            PluginApi.config.CFG_MACHINE, null, key, "");

        if (fn.Length == 0)
                return "";

        string? resolved = PluginApi.paths.resolve_file_path(fn);

        if (resolved == null)
        {
                Console.Error.WriteLine(
                    $"{key} = « {fn} » : image de disquette introuvable, lecteur laissé vide.");
                return "";
        }

        return resolved;
    }

    // pcem: pc.c:776-777, avec la validation que PCem n'a pas : fdd_set_type accepte
    // n'importe quel entier (fdd.c:176-179) et drive_types n'a que huit entrées.
    private static int config_get_drive_type(string key, int drive)
    {
        int t = PluginApi.config.config_get_int(
            PluginApi.config.CFG_MACHINE, null, key, cfg_drive_type[drive]);

        if (t < 0 || t > 7)
        {
                Console.Error.WriteLine(
                    $"{key} = {t} : hors de la table des lecteurs (0 à 7, fdd.cs:88-112). " +
                    $"On garde {cfg_drive_type[drive]}.");
                return cfg_drive_type[drive];
        }

        return t;
    }

    /// <summary>
    /// Les fichiers de ROM qu'attend la machine COURANTE, pour les messages d'échec.
    /// Pendant, côté diagnostic, du `switch (romset)` de loadbios (mem_bios.cs:99) :
    /// deux machines, deux jeux de fichiers, et un message qui cite l'autre envoie
    /// chercher au mauvais endroit.
    /// </summary>
    private static string RomHint() => romset switch
    {
        ROM_IBMXT => "ibmxt/xt.rom (64 Ko), ou à défaut la paire " +
                     "ibmxt/5000027.u19 + ibmxt/1501512.u18 (32 Ko chacune)",
        _ => "ibmpc/pc102782.bin (8 Ko) et, optionnellement, les quatre ROMs BASIC " +
             "ibmpc/basicc11.f6/.f8/.fa/.fc",
    };

    /// <summary>
    /// pcem: pc.c:178-300 (réduit). Une seule fois, au démarrage : alloue les
    /// tables, charge les ROMs, initialise les registres d'E/S.
    /// </summary>
    internal static bool initpc(string romsPath)
    {
        // pcem: wx-sdl2.c:450 — l'IHM pose ce crochet AVANT initpc, et device.c:68
        // l'appelle sans le tester. Il est posé ici, donc pour TOUS les points
        // d'entrée : --headless, --boot, --timer-check et BootDiff n'ont pas
        // d'hôte, et un latch nul ferait boucler timer_process() à l'infini sur
        // le chronomètre du son. tools/oracle/harness.c fait de même.
        PluginApi.device._sound_speed_changed = Sound.sound.sound_speed_changed;

        PluginApi.paths.set_roms_paths(romsPath);

        // pcem: paths.c:206-207, 214-215 — LES DEUX CHEMINS DU CMOS, posés ici et non
        // par paths_init().
        //
        // POURQUOI PAS paths_init() : il pose AUSSI le chemin des ROM, que la ligne
        // au-dessus vient d'établir depuis --rom-path. Il est mort dans ce dépôt pour
        // cette raison — c'est l'appelant qui décide où sont les ROM, pas le binaire —
        // et l'appeler ici les écraserait.
        //
        // Résolus par la MÊME politique que les ROM : resolve_roms_path essaie d'abord
        // le chemin tel quel depuis le répertoire courant (paths.cs:158), puis remonte
        // l'arbre depuis AppContext.BaseDirectory (paths.cs:196) — et c'est bien cette
        // seconde passe qui sert, parce que Rider lance depuis bin/Debug/net10.0 là où
        // PCem a toujours pour répertoire courant celui de son installation. Sans ça le
        // CMOS partait dans « ./.ami286.nvr », à la racine du dépôt — mesuré.
        PluginApi.paths.set_default_nvr_path(
                PluginApi.paths.resolve_roms_path("nvr") is { Length: > 0 } n ? n : "nvr");
        PluginApi.paths.set_default_nvr_default_path(
                PluginApi.config.append_slash(
                        PluginApi.paths.resolve_roms_path("nvr/default") is { Length: > 0 } d
                                ? d : "nvr/default", 512));

        // set_roms_paths() écarte silencieusement un répertoire absent (paths.cs:88).
        // Sans ce test, num_roms_paths valait 0, romfopen() bouclait zéro fois et
        // l'échec remontait jusqu'à loadbios(), qui accusait les ROMs — alors que le
        // répertoire entier manquait. Deux fautes très différentes méritent deux
        // messages, sinon on cherche des fichiers dans un dossier qui n'existe pas.
        if (PluginApi.paths.num_roms_paths == 0)
        {
            // Le fichier cité est celui de la MACHINE choisie. Citer pc102782.bin en
            // dur, comme avant M11.1, faisait accuser des fichiers du 5150 pour un
            // échec du 5160 — et envoyait chercher au mauvais endroit.
            Console.Error.WriteLine(
                $"Aucun répertoire de ROM utilisable : « {romsPath} » est absent ou illisible.\n" +
                $"Attendu : un répertoire roms/ contenant {RomHint()}.");
            return false;
        }

        // Le processeur se juge contre la machine FINALE : --config a pu poser `cpu`,
        // puis --model changer de machine. Refusé ICI, avant d'amorcer quoi que ce
        // soit, pour que tous les points d'entrée — fenêtre, --boot, --timer-check, les
        // outils Diff — refusent de la même façon. Voir check_cpu.
        if (!check_cpu())
            return false;

        // pcem: pc.c:656 — G4.1 : la clé `fpu` résolue contre la machine et le CPU finaux.
        cpu_c.fpu_type = cpu_c.fpu_get_type(Models.model_c.model, cpu_c.cpu_manufacturer, cpu_c.cpu, cfg_fpu);

        // G2, D0.2 : une machine de la table dont l'init n'est pas transcrite (l'ami386
        // jusqu'à G3.1, où Headland l'a rendue amorçable). REFUS BRUYANT, comme h_boot
        // côté oracle, plutôt qu'une demi-machine.
        if (Models.model_c.models[Models.model_c.model].init is null)
        {
            Console.Error.WriteLine(
                $"La machine « {Models.model_c.models[Models.model_c.model].internal_name} » n'est pas " +
                "encore amorçable : son chipset n'est pas transcrit (PLAN.md).");
            return false;
        }

        PluginApi.device.device_init();
        Video.video.initvideo();

        // pcem: pc.c — mem_size vient de la configuration AVANT tout mem_alloc().
        // Le poser dans model_init(), comme je l'avais fait, arrive un cran trop
        // tard : mem_alloc() a déjà alloué zéro octet et mappé le vide, et la
        // machine tourne alors sans RAM. Le BIOS ne plante pas pour autant — il
        // s'exécute depuis la ROM — mais la BDA reste à 0xFF.
        mem.mem_size = cfg_mem_size;

        mem.mem_init();

        if (mem_bios.loadbios() == 0)
        {
                Console.Error.WriteLine(
                    $"Impossible de charger le BIOS de « {Models.model_c.models[Models.model_c.model].name} »" +
                    $" depuis « {PluginApi.paths.roms_paths} ».\n" +
                    $"Attendu : {RomHint()}.");
                return false;
        }

        // omitted: codegen_init() — dynarec non porté.
        timer.timer_reset();
        Sound.sound.sound_reset();   // pc.c:277
        io.io_init();
        Floppy.fdc_c.fdc_init();
        Disc.disc.disc_init();
        // omitted: fdi_init() (pc.c:281) — chargeur FDI, registre des omissions.
        Disc.disc_img.img_init();

        // pcem: pc.c:776-777 — loadconfig() pose drive_a_type/drive_b_type AVANT
        // initpc, depuis le fichier de configuration (défaut PCem : 7, 3,5" ED). Le
        // 5150 a des lecteurs 5,25" double densité 360 Ko : type 1 (fdd.c:44-46).
        // tools/oracle/harness.c pose les mêmes deux valeurs.
        Floppy.fdd_c.fdd_set_type(0, cfg_drive_type[0]);
        Floppy.fdd_c.fdd_set_type(1, cfg_drive_type[1]);

        //
        // setpitclock() N'EST PAS ici : pc.c:56-74 ne l'appelle pas. Il appartient
        // à pc_reset() (pc.c:184-187), donc APRÈS model_init() et ses pit_init().
        // Je l'avais mis ici ; voir VERIFICATION.md § M4.0.

        resetpchard();
        // pcem: pc.c:317 — G6.3, la fin d'initpc (après le fullspeed() que ce dépôt n'a pas).
        Models.ali1429.ali1429_reset();
        return true;
    }

    /// <summary>
    /// pcem: pc.c:353-400 (réduit). Reconstruit la machine entière : c'est ce que
    /// fait un appui sur le bouton reset.
    /// </summary>
    internal static void resetpchard()
    {
        timer.timer_reset();
        PluginApi.device.device_close_all();
        PluginApi.device.device_init();

        // pcem: pc.c:361 — AVANT speaker_init() : sound_reset() remet
        // sound_handlers_num à 0 et effacerait l'enregistrement du handler.
        Sound.sound.sound_reset();

        io.io_init();
        // pcem: pc.c:363 — AVANT mem_alloc, qui lit cpu_16bitbus pour plafonner la RAM
        // d'un bus de 24 bits (mem.cs, mem_alloc). Ce dépôt posait la configuration du
        // 286 dans at_init, donc APRÈS : sur un ami286 à 16 384 Ko, la RAM haute sortait
        // mal plafonnée au premier amorçage, et autrement au second.
        Cpu.cpu_c.cpu_set();
        mem.mem_alloc();
        Floppy.fdc_c.fdc_init();
        Disc.disc.disc_reset();
        Disc.disc.disc_load(0, Floppy.fdd_c.discfns[0]);
        Disc.disc.disc_load(1, Floppy.fdd_c.discfns[1]);

        Models.model_c.model_init();
        // pcem: pc.c:373 — M21 : la souris série Microsoft sur COM1 (mouse_type = 0).
        Mouse.mouse.mouse_emu_init();
        Video.video.video_init();
        Sound.sound_speaker.speaker_init();   // pc.c:375

        // pcem: pc.c:392 — hdd_controller_init(hdd_controller_name).
        //
        // DEVIATION: le registre HDD_CONTROLLER (seize cartes, hdd.c:149-164) est
        //   écarté comme SOUND_CARD — et comme VIDEO_CARD l'était jusqu'à M15, où la
        //   VGA l'a fait transcrire, réduit à deux entrées (video.cs). Avec une seule
        //   carte câblée, hdd_controller_init (hdd.c:128-140) se réduit à son unique
        //   effet — un device_add.
        //
        //   Un nom inconnu, y compris le "" par défaut, ne monte aucune carte et ne
        //   dit rien : c'est exactement ce que fait PCem, dont le `fatal` de
        //   hdd.c:140 est COMMENTÉ.
        //
        // APRÈS mem_alloc(), et ce n'est pas un détail : mem_alloc détruit toute la
        // liste de mappages (mem.cs:722-746), donc une carte à ROM d'extension posée
        // avant verrait son mappage effacé. La position de pc.c:392 le garantit.
        //   TROIS CARTES DEPUIS LE 286, et la troisième n'est pas interchangeable avec
        //   les deux autres : mfm_at répond en 0x1F0 sur l'IRQ 14 et prend sa géométrie
        //   du BIOS, là où le Xebec et le DTC portent leur propre ROM d'extension et
        //   n'adressent que quatre géométries. Un AT veut mfm_at ; un XT ne peut pas
        //   s'en servir, son BIOS n'ayant pas d'INT 13h pour disque dur.
        Disc.hdd_c.hdd_controller_name = cfg_hdd_controller;
        if (cfg_hdd_controller == "mfm_xebec")
                PluginApi.device.device_add(Mfm.mfm_xebec.mfm_xebec_device);
        else if (cfg_hdd_controller == "dtc5150x")
                PluginApi.device.device_add(Mfm.mfm_xebec.dtc_5150x_device);
        else if (cfg_hdd_controller == "mfm_at")
                PluginApi.device.device_add(Mfm.mfm_at.mfm_at_device);
        // G5.1 — l'IDE standard (hdd.c:155), disque dur seul.
        else if (cfg_hdd_controller == "ide")
                PluginApi.device.device_add(Ide.ide.ide_device);

        pc_reset();

        // pcem: pc.c:395 — resetide(). Elle charge les images des quatre lecteurs IDE si la
        // carte est « ide » (ide.c:278), et ne fait sinon que refermer des fichiers jamais
        // ouverts. Celle de pc.c:290 (initpc) ouvre les mêmes images, que celle-ci referme
        // puis rouvre : l'effet net est le sien seul, et l'oracle ne garde que celle-ci.
        Ide.ide.resetide();

        // pcem: pc.c:397 — loadnvr(). Lit « nvr/.<machine>.nvr », sinon le CMOS de
        // référence « nvr/default/<machine>.nvr ». SANS AUCUN DES DEUX il pose 0xFF
        // partout sauf la date du 1er janvier 1980 et le bit 24 heures ; la somme de
        // contrôle est alors fausse et le POST de l'AT s'arrête sur « 162-System
        // Options Not Set », comme un vrai 5170 à pile vide.
        //
        // ET SANS ELLE LE POST TOMBE DANS DE L'UB DE PCem. nvrram[RTC_MONTH] valant 0,
        // rtc_recalc appelle rtc_get_days(0, …) qui fait `rtc_days_in_month[month - 1]`,
        // donc l'indice -1 : le C lit l'octet qui précède le tableau et continue, le C#
        // lève. Trouvé par le premier boot-diff AT qui franchissait le mode protégé.
        // L'oracle l'appelle maintenant au même point (harness.c).
        Devices.nvr.loadnvr();

        // pcem: pc.c:403 — G6.3, le troisième ali1429_reset, après loadnvr.
        Models.ali1429.ali1429_reset();

        // pcem: pc.c:407. Inerte : rien dans l'arbre porté n'active l'un ou l'autre cache,
        // et cpu_set() les a lus à zéro. Transcrit pour que cpu_update_waitstates() ne
        // repose sur aucun zéro implicite.
        cpu_c.cpu_cache_int_enabled = cpu_c.cpu_cache_ext_enabled = 0;

        // omitted: image_close() et le bloc cdrom (pc.c:411-433) — voir l'en-tête.
        // omitted: sound_update_buf_length() (:438) — n'était pas marqué. Il recalcule la
        //   longueur du tampon son depuis la configuration audio, que resetpchard ne change
        //   pas ici, et sound_poll le rappelle à chaque bloc (sound.cs).
        // pcem: pc.c:439 — inerte : cpu_set() a déjà posé cpu_turbo à 1.
        cpu_c.cpu_set_turbo(1);
    }

    // pcem: pc.c:344-351 — Ctrl+Alt+Suppr, poussé dans la file du clavier. Redémarrage
    // À CHAUD : le BIOS trouve 0x1234 en 0040:0072 et saute le test mémoire de 46 s, là
    // où resetpchard() réalloue la RAM à zéro (mem.cs:726) et repart à froid.
    // 83 est le Suppr du pavé du clavier 83 touches du 5150, pas le 0xD3 étendu de l'AT.
    internal static void resetpc_cad()
    {
        Keyboard.keyboard.keyboard_send_scancode(29, 0); /* Ctrl key pressed */
        Keyboard.keyboard.keyboard_send_scancode(56, 0); /* Alt key pressed */
        Keyboard.keyboard.keyboard_send_scancode(83, 0); /* Delete key pressed */
        Keyboard.keyboard.keyboard_send_scancode(29, 1); /* Ctrl key released */
        Keyboard.keyboard.keyboard_send_scancode(56, 1); /* Alt key released */
        Keyboard.keyboard.keyboard_send_scancode(83, 1); /* Delete key released */
    }

    /// <summary>
    /// pcem: pc.c:576-592 (réduit). Fermeture du processus, et le SEUL endroit qui vide
    /// les tampons d'écriture sur les images.
    ///
    /// Les deux `disc_close` s'occupent des disquettes : img_writeback (disc_img.cs:551)
    /// écrit dans le FileStream sans Flush(), et c'est le Close() d'img_close
    /// (disc_img.cs:430) qui les pousse.
    ///
    /// `device_close_all` s'occupe du DISQUE DUR, par xebec_close → hdd_close →
    /// FileStream.Close() (mfm_xebec.cs, hdd_file.cs). Il était omis jusqu'à M13, avec ce
    /// motif : « device_close_all() n'a rien à fermer que le processus ne rende de
    /// lui-même ». C'était vrai quand la phrase a été écrite, et M12 l'a rendu faux sans
    /// que personne n'y revienne — depuis, le Fixed Disk Adapter tient un FileStream
    /// tamponné.
    ///
    /// MESURÉ, et la perte était totale, pas latente : un FDISK seul écrit un secteur de
    /// 512 octets, qui tient entièrement dans le tampon de 4 Ko et n'en sort jamais.
    /// L'image restait à ZÉRO octet non nul alors que l'écran affichait la partition
    /// écrite. Les campagnes de § M12 y échappaient par le volume — un FORMAT pousse
    /// 10 Mo, donc tout sauf le dernier tampon partiel — et non par construction.
    /// </summary>
    internal static void closepc()
    {
        // omitted: codegen_close(), atapi->exit(), dumppic(), dumpregs(), closevideo(),
        //   lpt1_device_close(), mouse_emu_close(), zip_eject() (pc.c:577-591) —
        //   dynarec, ATAPI, LPT, souris, ZIP : hors périmètre 5150.
        Disc.disc.disc_close(0);
        Disc.disc.disc_close(1);
        PluginApi.device.device_close_all();
    }

    // pcem: pc.c — remise à zéro du CPU et des périphériques sensibles au reset.
    internal static void pc_reset()
    {
        _808x.resetx86();
        // omitted: dma_reset(), nvr_recalc() — hors périmètre 5150 minimal ; dma et
        //   pic sont réarmés par leurs propres *_init().
        Floppy.fdc_c.fdc_reset();
        Models.pic.pic_reset();
        // pcem: pc.c:182 — M21.
        Models.serial.serial_reset();

        // omitted: timer_reset() — pc.c:178 la porte EN COMMENTAIRE. Je l'avais
        //   ajoutée : elle invalide (magic = 0) tous les chronomètres que
        //   model_init() vient d'enregistrer, et la machine tourne alors sans PIT.
        //   Le seul timer_reset() vivant est celui de resetpchard(), pc.c:354,
        //   AVANT model_init().

        // pcem: pc.c:184-187 — LE domaine d'horloge. setpitclock() pose onze
        // globales (PITCONST, CGACONST, xt_cpu_multi, TIMER_USEC...) que le PIT,
        // le CGA et la conversion TSC du 8088 lisent toutes. L'oublier ne casse
        // rien à la compilation : ça donne une division par zéro au premier accès
        // au PIT.
        //
        // La valeur n'est PAS la fréquence du CPU. Sur une machine non-AT, PCem
        // passe 14 318 184 Hz — l'oscillateur maître du XT — et c'est
        // clockhardware() qui convertit les cycles CPU vers ce domaine via
        // xt_cpu_multi. Passer 4,77 MHz ici ferait tourner toute la machine à un
        // tiers de sa vitesse, sans qu'aucun test d'opcode ne s'en aperçoive.
        //
        // Sur un AT, c'est la rspeed de l'entrée de CPU choisie. Ce fut un littéral,
        // 6 MHz, tant que MODEL ne portait pas de membre `cpu` (jusqu'à M16) — et
        // c'est la ligne qui a fait marcher le POST de l'AT : à 14 318 184, PITCONST
        // vaut 12 cycles CPU par tic de PIT au lieu de 5,03, le compte de
        // rafraîchissement mémoire reste sous le 0xF600 que le BIOS exige, et le POST
        // s'arrête sur un HLT en F000:05C4 après cent millions d'instructions.
        if (AT != 0)
                Models.pit.setpitclock(Models.model_c.models[Models.model_c.model].cpu[cpu_c.cpu_manufacturer].cpus![cpu_c.cpu].rspeed);
        else
                Models.pit.setpitclock(14318184.0f);

        // pcem: pc.c:191 — G6.3. Sur TOUTE machine, comme le C : les registres de l'ALi 1429
        // à 0xFF ; ses ports ne sont posés que sur l'ami486 (ali1429_init).
        Models.ali1429.ali1429_reset();
        // omitted: le video_init() commenté (pc.c:192).
    }

    /// <summary>
    /// pcem: pc.c:470-553. UNE TRANCHE de 10 ms d'émulation, pas une boucle.
    /// L'hôte l'appelle 100 fois par seconde ; c'est lui qui porte le cadençage.
    /// </summary>
    internal static void runpc()
    {
        int cycles_to_run = cpu_c.cpu_get_speed() / 100;

        Video.video.startblit();

        // pcem: pc.c:479-487 — LE SÉLECTEUR DE CŒUR, transcrit pour de vrai.
        //
        // Cette ligne était « Le 5150 n'est ni AT ni 386 : toujours le cœur 808x »
        // et un appel direct à execx86. C'était le garde-fou que PLAN-286.md avait
        // posé dès A0 : aucune machine ne sélectionne le cœur 286 tant qu'il n'est
        // pas complet. La table d'opcodes l'est depuis A11, donc le garde-fou tombe.
        //
        // AT EST LE SEUL DISCRIMINANT ICI, et ce n'est pas une simplification : un
        // 286 est « AT sans is386 ». is386 reste nul pour les trois machines du
        // dépôt — aucune n'a de 386 — donc la première branche est morte
        // aujourd'hui, et transcrite quand même parce que c'est la structure du C.
        if (is386 != 0)
        {
                // omitted: `if (cpu_use_dynarec) exec386_dynarec(...)` — src/codegen/
                //   n'est pas porté, et le 286 comme le 386 sont des interpréteurs par
                //   conception : cpu_flags vaut 0 dans cpu_tables.c, CPU_SUPPORTS_DYNAREC
                //   n'apparaît qu'au 486.
                Cpu._386.exec386(cycles_to_run);
        }
        else if (AT != 0)
                Cpu._386.exec386(cycles_to_run);
        else
                _808x.execx86(cycles_to_run);

        Keyboard.keyboard.keyboard_poll_host();
        Keyboard.keyboard.keyboard_process();
        pollmouse();
        // omitted: joystick_poll() — hors périmètre.

        Video.video.endblit();

        framecountx++;
        framecount++;
        if (framecountx >= 100)
        {
                framecountx = 0;
                framecount = 0;
        }
    }
}
