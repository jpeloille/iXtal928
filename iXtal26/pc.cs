// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/pc.c  (initpc :178-300, resetpchard :353-400, runpc :470-553)
// STATUS: partial — réduit à l'IBM PC 5150 : pas de disquette, ni son, ni réseau,
//         ni disque dur, ni souris, ni joystick, ni NVR. Ce qui reste est la
//         séquence d'amorçage et la tranche d'exécution.
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
    // pcem: ibm.h:163-... — l'énumération des romsets, dans l'ordre du C. Seul
    // ROM_IBMPC est une cible ; les autres existent parce que les périphériques
    // transcrits (clavier, mem_bios) branchent dessus, et qu'on ne réécrit pas
    // leurs conditions.
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
    internal const int ROM_XI8088 = 26;  // hors cible, présent pour les gardes
    internal const int ROM_LEDGE_MODELM = 27;  // hors cible, présent pour les gardes
    internal const int ROM_ATARIPC3 = 28;  // hors cible, présent pour les gardes

    // pcem: ibm.h:272 — le romset courant, défini par pc.c chez PCem.
    internal static int romset = ROM_IBMPC;

    // pcem: ibm.h — vitesse du 8088 de l'IBM PC/XT, cpu_tables.c:33.
    internal const int CPU_SPEED_8088 = 4772728;

    internal static int framecount, framecountx;

    internal static int cpu_get_speed() => CPU_SPEED_8088;

    // DEVIATION: fatal() de PCem appelle dumpregs() puis exit(-1). Ici on lève :
    //   l'hôte décide quoi en faire, et un test n'a pas à voir son processus
    //   disparaître.
    internal static void fatal(string s)
    {
        Counters.n_fatal++;
        Console.Error.Write("iXtal26 FATAL: " + s);
        throw new InvalidOperationException(s);
    }

    /// <summary>
    /// pcem: pc.c:178-300 (réduit). Une seule fois, au démarrage : alloue les
    /// tables, charge les ROMs, initialise les registres d'E/S.
    /// </summary>
    internal static bool initpc(string romsPath)
    {
        PluginApi.paths.set_roms_paths(romsPath);

        PluginApi.device.device_init();
        Video.video.initvideo();

        // pcem: pc.c — mem_size vient de la configuration AVANT tout mem_alloc().
        // Le poser dans model_init(), comme je l'avais fait, arrive un cran trop
        // tard : mem_alloc() a déjà alloué zéro octet et mappé le vide, et la
        // machine tourne alors sans RAM. Le BIOS ne plante pas pour autant — il
        // s'exécute depuis la ROM — mais la BDA reste à 0xFF.
        mem.mem_size = Models.model.MAX_RAM;

        mem.mem_init();

        if (mem_bios.loadbios() == 0)
        {
                Console.Error.WriteLine(
                    $"Impossible de charger le BIOS de l'IBM PC 5150 depuis « {romsPath} ».\n" +
                    "Attendu : ibmpc/pc102782.bin (8 Ko) et, optionnellement, les quatre\n" +
                    "ROMs BASIC ibmpc/basicc11.f6/.f8/.fa/.fc.");
                return false;
        }

        // omitted: codegen_init() — dynarec non porté.
        timer.timer_reset();
        io.io_init();

        // omitted: sound_reset() (pc.c:73), fdc_init/disc_init/fdi_init/img_init
        //   (pc.c:75-78) — hors périmètre 5150 minimal.
        //
        // setpitclock() N'EST PAS ici : pc.c:56-74 ne l'appelle pas. Il appartient
        // à pc_reset() (pc.c:184-187), donc APRÈS model_init() et ses pit_init().
        // Je l'avais mis ici ; voir VERIFICATION.md § M4.0.

        resetpchard();
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

        io.io_init();
        // omitted: cpu_set() — cpu.c n'est pas porté ; la configuration 8088 est
        //   posée par model.cs. Voir TRANSCRIPTION.md.
        mem.mem_alloc();

        Models.model.model_init();
        Video.video.video_init();

        pc_reset();
    }

    // pcem: pc.c — remise à zéro du CPU et des périphériques sensibles au reset.
    internal static void pc_reset()
    {
        _808x.resetx86();
        // omitted: dma_reset(), fdc_reset(), nvr_recalc() — hors périmètre 5150
        //   minimal ; dma et pic sont réarmés par leurs propres *_init().
        Models.pic.pic_reset();

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
        Models.pit.setpitclock(14318184.0f);

        // omitted: ali1429_reset() (pc.c:190) et le video_init() commenté (pc.c:192).
    }

    /// <summary>
    /// pcem: pc.c:470-553. UNE TRANCHE de 10 ms d'émulation, pas une boucle.
    /// L'hôte l'appelle 100 fois par seconde ; c'est lui qui porte le cadençage.
    /// </summary>
    internal static void runpc()
    {
        int cycles_to_run = cpu_get_speed() / 100;

        Video.video.startblit();

        // Le 5150 n'est ni AT ni 386 : toujours le cœur 808x.
        _808x.execx86(cycles_to_run);

        Keyboard.keyboard.keyboard_poll_host();
        Keyboard.keyboard.keyboard_process();
        // omitted: pollmouse(), joystick_poll() — hors périmètre.

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
