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
using iXtal26.Models;
using iXtal26.Video;
using static iXtal26.Cpu.x86;

namespace iXtal26;

internal static partial class pc
{
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
        paths.set_roms_path(romsPath);

        device.device_init();
        video.initvideo();
        mem.mem_init();

        if (!mem_bios.loadbios())
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
        device.device_close_all();
        device.device_init();

        io.io_init();
        // omitted: cpu_set() — cpu.c n'est pas porté ; la configuration 8088 est
        //   posée par model.cs. Voir TRANSCRIPTION.md.
        mem.mem_alloc();

        model.model_init();
        video.video_init();

        pc_reset();
    }

    // pcem: pc.c — remise à zéro du CPU et des périphériques sensibles au reset.
    internal static void pc_reset()
    {
        _808x.resetx86();
        // omitted: dma_reset(), fdc_reset(), nvr_recalc() — hors périmètre 5150
        //   minimal ; dma et pic sont réarmés par leurs propres *_init().
        pic.pic_reset();
        timer.timer_reset();
    }

    /// <summary>
    /// pcem: pc.c:470-553. UNE TRANCHE de 10 ms d'émulation, pas une boucle.
    /// L'hôte l'appelle 100 fois par seconde ; c'est lui qui porte le cadençage.
    /// </summary>
    internal static void runpc()
    {
        int cycles_to_run = cpu_get_speed() / 100;

        video.startblit();

        // Le 5150 n'est ni AT ni 386 : toujours le cœur 808x.
        _808x.execx86(cycles_to_run);

        keyboard.keyboard_poll_host();
        keyboard.keyboard_process();
        // omitted: pollmouse(), joystick_poll() — hors périmètre.

        video.endblit();

        framecountx++;
        framecount++;
        if (framecountx >= 100)
        {
                framecountx = 0;
                framecount = 0;
        }
    }
}
