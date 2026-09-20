// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/disc/disc.c + includes/private/disc/disc.h
// SHA256: voir oracle.tsv ; vérifier avec tools/check-oracle.sh
// STATUS: partial — le répartiteur : chargeurs par extension, DRIVE drives[2],
//         disc_poll et ses périodes, sélection de lecteur et moteur. Omis : le
//         chargeur FDI (registre des omissions, TRANSCRIPTION.md).
//
// noms: ce que les identifiants de disc.c désignent — R1(e).
//   drives[]        la table de fonctions du format chargé, par lecteur
//   loaders[]       chargeurs par extension ; sentinelle {0,0,0} en fin (disc.c:53)
//   driveloaders[]  quel chargeur a ouvert quel lecteur, pour savoir qui le fermera
//   curdrive        lecteur courant vu du média, posé par disc_set_drivesel
//   disc_drivesel   le même choix vu du contrôleur (fdc.c:571) ; les deux coexistent
//   disc_period     période de l'horloge-octet en µs ; c'est ELLE qui fait tourner
//                   la machine à états, disc_poll n'étant rien d'autre
//   disc_notfound   compte à rebours : à zéro, le secteur est déclaré introuvable
//   motoron         moteur en rotation ; conditionne l'armement de disc_poll
//   drive_empty[]   aucun média chargé ; se lit comme un changement de disquette
//   disc_changed[]  drapeau du port 0x3f7, effacé par un seek
//   writeprot[]     protection du média ; fwriteprot[] = celle forcée par l'hôte
//   SECTOR_FIRST/NEXT  -2 / -1 : lire le premier secteur rencontré, puis le suivant
//   motorspin / fdc_ready / fdc_indexcount / defaultwriteprot / oldtrack[]   MORTS,
//                   PB-20 ; fdc_ready et fdc_indexcount ont même leur extern commenté

// CS8602 : `loaders[c].load(drive, fn)` et `.close(drive)` sont des delegates
// nullables parce que la sentinelle {0, 0, 0} termine la table (disc.c:53) ; le
// C les appelle sans les tester, sauf `close` — même chose ici.
#pragma warning disable CS8602

using static iXtal26.Disc.disc_img;
using static iXtal26.Floppy.fdc_c;
using static iXtal26.Floppy.fdd_c;
using static iXtal26.PluginApi.config;
using static iXtal26.timer;

namespace iXtal26.Disc;

// pcem: disc.h:3-12 — les pointeurs de fonction de DRIVE.
internal delegate void drive_seek_fn(int drive, int track);
internal delegate void drive_readsector_fn(int drive, int sector, int track, int side, int density, int sector_size);
internal delegate void drive_writesector_fn(int drive, int sector, int track, int side, int density, int sector_size);
internal delegate void drive_readaddress_fn(int drive, int track, int side, int density);
internal delegate void drive_format_fn(int drive, int track, int side, int density, uint8_t fill);
internal delegate int drive_hole_fn(int drive);
internal delegate void drive_stop_fn();
internal delegate void drive_poll_fn();

// pcem: disc.h:3-12
internal sealed class DRIVE
{
    internal drive_seek_fn? seek;
    internal drive_readsector_fn? readsector;
    internal drive_writesector_fn? writesector;
    internal drive_readaddress_fn? readaddress;
    internal drive_format_fn? format;
    internal drive_hole_fn? hole;
    internal drive_stop_fn? stop;
    internal drive_poll_fn? poll;
}

// pcem: disc.c:47-51 — la struct anonyme des chargeurs, copiée par valeur : struct.
internal delegate void loader_load_fn(int drive, string fn);
internal delegate void loader_close_fn(int drive);

internal struct loader_t
{
    internal string? ext;
    internal loader_load_fn? load;
    internal loader_close_fn? close;
    internal int size;
}

internal static partial class disc
{
    // pcem: disc.h:64-65
    /*Used in the Read A Track command. Only valid for disc_readsector(). */
    internal const int SECTOR_FIRST = -2;
    internal const int SECTOR_NEXT = -1;

    // pcem: disc.c:11-33
    internal static int disc_drivesel = 0;
    internal static pc_timer_t disc_poll_timer = new();

    internal static int[] disc_track = new int[2];
    internal static int[] writeprot = new int[2], fwriteprot = new int[2];

    internal static DRIVE[] drives = { new(), new() };
    internal static int[] drive_type = new int[2];

    internal static int curdrive = 0;

    internal static int defaultwriteprot = 0;

    internal static int fdc_ready;

    internal static int[] drive_empty = { 1, 1 };
    internal static int[] disc_changed = new int[2];

    internal static int motorspin;
    internal static int motoron;

    internal static int fdc_indexcount = 52;

    // pcem: disc.c:47-53
    private static loader_t[] loaders = {
        new() { ext = "IMG", load = img_load, close = img_close, size = -1 }, new() { ext = "IMA", load = img_load, close = img_close, size = -1 }, new() { ext = "360", load = img_load, close = img_close, size = -1 },
        new() { ext = "XDF", load = img_load, close = img_close, size = -1 },
        // omitted: {"FDI", fdi_load, fdi_close, -1} (disc.c:53) — chargeur de flux FDI, registre des omissions
        new() { ext = null, load = null, close = null } };

    // pcem: disc.c:55
    private static int[] driveloaders = new int[4];

    // pcem: disc.c:57-92
    internal static void disc_load(int drive, string fn)
    {
        int c = 0, size;
        string p;
        FileStream? f;
        if (fn == null)
                return;
        p = get_extension(fn);
        // omitted: `if (!p) return;` (disc.c:64-65) — get_extension (config.c:416-428)
        //   ne rend jamais NULL ; en C# la chaîne n'est pas nullable.
        // omitted: pclog("Loading :%i %s %s\n", drive, fn, p) — sortie pure
        f = fopen(fn, "rb");
        if (f == null)
                return;
        f.Seek(-1, SeekOrigin.End);
        size = (int)f.Position + 1;
        f.Close();
        while (loaders[c].ext != null)
        {
                if (string.Equals(p, loaders[c].ext, StringComparison.OrdinalIgnoreCase) && (size == loaders[c].size || loaders[c].size == -1))
                {
                        // omitted: pclog("Loading as %s\n", p) — sortie pure
                        driveloaders[drive] = c;
                        loaders[c].load(drive, fn);
                        drive_empty[drive] = 0;
                        disc_changed[drive] = 1;
                        // pcem bug, reproduced: PB-18 — strcpy sur lui-même quand fn EST discfns[drive] (pc.c:367).
                        discfns[drive] = fn;
                        fdd_disc_changed(drive);
                        return;
                }
                c++;
        }
        // omitted: pclog("Couldn't load %s %s\n", fn, p) — sortie pure
        drive_empty[drive] = 1;
        discfns[drive] = "";
    }

    // pcem: disc.c:94-107
    internal static void disc_close(int drive)
    {
        if (loaders[driveloaders[drive]].close != null)
                loaders[driveloaders[drive]].close(drive);
        drive_empty[drive] = 1;
        discfns[drive] = "";
        drives[drive].hole = null;
        drives[drive].poll = null;
        drives[drive].seek = null;
        drives[drive].readsector = null;
        drives[drive].writesector = null;
        drives[drive].readaddress = null;
        drives[drive].format = null;
    }

    // pcem: disc.c:109-110
    internal static int disc_notfound = 0;
    private static int disc_period = 32;

    // pcem: disc.c:112-120
    internal static int disc_hole(int drive)
    {
        drive ^= fdd_swap;

        if (drive < 2 && drives[drive].hole != null)
        {
                return drives[drive].hole(drive);
        }
        else
        {
                return 0;
        }
    }

    // pcem: disc.c:122-133
    internal static void disc_poll(object? p)
    {
        timer_advance_u64(disc_poll_timer, (uint64_t)disc_period * TIMER_USEC);

        if (disc_drivesel < 2 && drives[disc_drivesel].poll != null)
                drives[disc_drivesel].poll();

        if (disc_notfound != 0)
        {
                disc_notfound--;
                if (disc_notfound == 0)
                        fdc_notfound(FDC_STATUS_AM_NOT_FOUND);
        }
    }

    // pcem: disc.c:135-154
    internal static int disc_get_bitcell_period(int rate)
    {
        int bit_rate = 0;

        switch (rate)
        {
        case 0: /*High density*/
                bit_rate = 500;
                break;
        case 1: /*Double density (360 rpm)*/
                bit_rate = 300;
                break;
        case 2: /*Double density*/
                bit_rate = 250;
                break;
        case 3: /*Extended density*/
                bit_rate = 1000;
                break;
        }

        return 1000000 / bit_rate * 2; /*Bitcell period in ns*/
    }

    // pcem: disc.c:156-180
    internal static void disc_set_rate(int drive, int drvden, int rate)
    {
        switch (rate)
        {
        case 0: /*High density*/
                disc_period = 16;
                break;
        case 1:
                switch (drvden)
                {
                case 0: /*Double density (360 rpm)*/
                        disc_period = 26;
                        break;
                case 1: /*High density (360 rpm)*/
                        disc_period = 16;
                        break;
                case 2:
                        disc_period = 4;
                        break;
                }
                // pcem bug, reproduced: PB-14 — pas de break : le cas 1 retombe dans le cas 2.
                goto case 2;
        case 2: /*Double density*/
                disc_period = 32;
                break;
        case 3: /*Extended density*/
                disc_period = 8;
                break;
        }
    }

    // pcem: disc.c:182-201
    internal static void disc_reset()
    {
        int drive;

        curdrive = 0;
        disc_period = 32;
        timer_add(disc_poll_timer, disc_poll, null, 0);

        for (drive = 0; drive < 2; drive++)
        {
                if (loaders[driveloaders[drive]].close != null)
                        loaders[driveloaders[drive]].close(drive);
                drive_empty[drive] = 1;
                drives[drive].hole = null;
                drives[drive].poll = null;
                drives[drive].seek = null;
                drives[drive].readsector = null;
                drives[drive].writesector = null;
                drives[drive].readaddress = null;
                drives[drive].format = null;
        }
    }

    // pcem: disc.c:203
    internal static void disc_init() { disc_reset(); }

    // pcem: disc.c:205
    internal static int[] oldtrack = { 0, 0 };
    // pcem: disc.c:206-214
    internal static void disc_seek(int drive, int track)
    {
        if (drive < 2 && drives[drive].seek != null)
                drives[drive].seek(drive, track);
    }

    // pcem: disc.c:216-223
    internal static void disc_readsector(int drive, int sector, int track, int side, int density, int sector_size)
    {
        drive ^= fdd_swap;

        if (drive < 2 && drives[drive].readsector != null)
                drives[drive].readsector(drive, sector, track, side, density, sector_size);
        else
                disc_notfound = 1000;
    }

    // pcem: disc.c:225-232
    internal static void disc_writesector(int drive, int sector, int track, int side, int density, int sector_size)
    {
        drive ^= fdd_swap;

        if (drive < 2 && drives[drive].writesector != null)
                drives[drive].writesector(drive, sector, track, side, density, sector_size);
        else
                disc_notfound = 1000;
    }

    // pcem: disc.c:234-239
    internal static void disc_readaddress(int drive, int track, int side, int density)
    {
        drive ^= fdd_swap;

        if (drive < 2 && drives[drive].readaddress != null)
                drives[drive].readaddress(drive, track, side, density);
    }

    // pcem: disc.c:241-248
    internal static void disc_format(int drive, int track, int side, int density, uint8_t fill)
    {
        drive ^= fdd_swap;

        if (drive < 2 && drives[drive].format != null)
                drives[drive].format(drive, track, side, density, fill);
        else
                disc_notfound = 1000;
    }

    // pcem: disc.c:250-255
    internal static void disc_stop(int drive)
    {
        drive ^= fdd_swap;

        if (drive < 2 && drives[drive].stop != null)
                drives[drive].stop();
    }

    // pcem: disc.c:257-261
    internal static void disc_set_drivesel(int drive)
    {
        drive ^= fdd_swap;

        disc_drivesel = drive;
    }

    // pcem: disc.c:263-269
    internal static void disc_set_motor_enable(int motor_enable)
    {
        if (motor_enable != 0 && motoron == 0)
                timer_set_delay_u64(disc_poll_timer, (uint64_t)disc_period * TIMER_USEC);
        else if (motor_enable == 0)
                timer_disable(disc_poll_timer);
        motoron = motor_enable;
    }
}
