// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/floppy/fdd.c + includes/private/floppy/fdd.h
// SHA256: voir oracle.tsv ; vérifier avec tools/check-oracle.sh
// STATUS: transcribed — le lecteur : type, piste courante, densel, tr/min, pas
//         double. Conteneur fdd_c : le tableau fdd[2] de fdd.c:8-19 garde son nom.
//
// noms: ce que les identifiants de fdd.c désignent — R1(e).
//   fdd[]             l'état MÉCANIQUE des deux lecteurs ; fdc.c a son propre état
//   type              index dans drive_types[] ; le 5150 a le type 1 (5,25" DD)
//   track             piste où la tête se trouve vraiment ≠ fdc.track[], qui est une
//                     croyance du contrôleur et peut en différer
//   densel            DENSity SELect, la broche de densité reçue du contrôleur
//   drate/kbps/fdc_kbps  déclarés par fdd.c, jamais lus nulle part
//   max_track         dernière piste atteignable ; 0 = lecteur absent
//   flags             FLAG_* : vitesses, format, trous supportés, double pas
//   FLAG_HOLE0/1/2    trou du média : double, haute, extra densité
//   SEEK_RECALIBRATE  -999 : un pas assez grand pour être borné à la piste 0
//   fdd_swap          échange A:/B:, appliqué au lecteur et non au contrôleur
//   TYPES : fdd_t = la struct anonyme de fdd.c:8-19 (classe, son tableau est muté) ;
//   drive_type_t = une entrée de drive_types[] (struct, copiée par valeur) ; fdd_c =
//   le conteneur du fichier, suffixe _c parce que `fdd` est pris par le tableau.

// CS0162 : fdd_getrpm (fdd.c:117-149) porte un switch après un if/else dont toutes
// les branches retournent. Code mort en C, erreur en C# ; reproduit sous ce pragma
// plutôt qu'effacé — PCEM_BUGS.md PB-15.
#pragma warning disable CS0162

using static iXtal26.Disc.disc;
using static iXtal26.Floppy.fdc_c;
using static iXtal26.timer;

namespace iXtal26.Floppy;

// pcem: fdd.c:8-19 — la struct anonyme de PCem.
internal sealed class fdd_t
{
    internal int type;

    internal int track;

    internal int densel;

    internal int drate;

    internal int kbps;
    internal int fdc_kbps;
}

// pcem: fdd.c:38-41 — copiée par valeur seulement, jamais adressée : struct.
internal struct drive_type_t
{
    internal int max_track;
    internal int flags;
}

internal static partial class fdd_c
{
    // pcem: fdd.h:3
    internal const int SEEK_RECALIBRATE = -999;

    // pcem: fdd.c:6 — déclaré ibm.h:155 ; pc.c le remplit depuis argv ou la config.
    internal static string[] discfns = { "", "" };

    // pcem: fdd.c:8-19
    private static fdd_t[] fdd = { new(), new() };

    /* Flags:
       Bit 0:	300 rpm supported;
       Bit 1:	360 rpm supported;
       Bit 2:	size (0 = 3.5", 1 = 5.25");
       Bit 3:	double density supported;
       Bit 4:	high density supported;
       Bit 5:	extended density supported;
       Bit 6:	double step for 40-track media;
    */
    // pcem: fdd.c:30-36
    private const int FLAG_RPM_300 = 1;
    private const int FLAG_RPM_360 = 2;
    private const int FLAG_525 = 4;
    private const int FLAG_HOLE0 = 8;
    private const int FLAG_HOLE1 = 16;
    private const int FLAG_HOLE2 = 32;
    private const int FLAG_DOUBLE_STEP = 64;

    // pcem: fdd.c:38-64
    private static drive_type_t[] drive_types = {
        new() { /*None*/
                max_track = 0,
                flags = 0 },
        new() { /*5.25" DD*/
                max_track = 41,
                flags = FLAG_RPM_300 | FLAG_525 | FLAG_HOLE0 },
        new() { /*5.25" HD*/
                max_track = 82,
                flags = FLAG_RPM_360 | FLAG_525 | FLAG_HOLE0 | FLAG_HOLE1 | FLAG_DOUBLE_STEP },
        new() { /*5.25" HD Dual RPM*/
                max_track = 82,
                flags = FLAG_RPM_300 | FLAG_RPM_360 | FLAG_525 | FLAG_HOLE0 | FLAG_HOLE1 | FLAG_DOUBLE_STEP },
        new() { /*3.5" DD*/
                max_track = 82,
                flags = FLAG_RPM_300 | FLAG_HOLE0 },
        new() { /*3.5" HD*/
                max_track = 82,
                flags = FLAG_RPM_300 | FLAG_HOLE0 | FLAG_HOLE1 },
        new() { /*3.5" HD 3-Mode*/
                max_track = 82,
                flags = FLAG_RPM_300 | FLAG_RPM_360 | FLAG_HOLE0 | FLAG_HOLE1 },
        new() { /*3.5" ED*/
                max_track = 82,
                flags = FLAG_RPM_300 | FLAG_HOLE0 | FLAG_HOLE1 | FLAG_HOLE2 } };

    // pcem: fdd.c:66
    internal static int fdd_swap = 0;

    // pcem: fdd.c:68-89
    internal static uint64_t fdd_seek(int drive, int track_diff)
    {
        drive ^= fdd_swap;

        if (drive >= 2)
                return 1000 * TIMER_USEC;

        fdd[drive].track += track_diff;

        if (fdd[drive].track < 0)
                fdd[drive].track = 0;

        if (fdd[drive].track > drive_types[fdd[drive].type].max_track)
                fdd[drive].track = drive_types[fdd[drive].type].max_track;

        fdc_discchange_clear(drive);

        disc_seek(drive, fdd[drive].track);
        return 1000 * TIMER_USEC;
    }

    // pcem: fdd.c:91-97
    internal static void fdd_disc_changed(int drive)
    {
        drive ^= fdd_swap;

        /*Force reload of current track data*/
        if (drive < 2)
                disc_seek(drive, fdd[drive].track);
    }

    // pcem: fdd.c:99-110
    internal static int fdd_track0(int drive)
    {
        drive ^= fdd_swap;

        if (drive >= 2)
                return 0;

        /* If drive is disabled, TRK0 never gets set. */
        if (drive_types[fdd[drive].type].max_track == 0)
                return 0;

        return fdd[drive].track == 0 ? 1 : 0;
    }

    // pcem: fdd.c:112-115
    internal static void fdd_set_densel(int densel)
    {
        fdd[0].densel = densel;
        fdd[1].densel = densel;
    }

    // pcem: fdd.c:117-149
    internal static int fdd_getrpm(int drive)
    {
        int hole;

        drive ^= fdd_swap;

        if (drive >= 2)
                return 0;

        hole = disc_hole(drive);

        if ((drive_types[fdd[drive].type].flags & FLAG_RPM_360) == 0)
                return 300;
        if ((drive_types[fdd[drive].type].flags & FLAG_RPM_300) == 0)
                return 360;

        if ((drive_types[fdd[drive].type].flags & FLAG_525) != 0)
        {
                return fdd[drive].densel != 0 ? 360 : 300;
        }
        else
        {
                /* disc_hole(drive) returns 0 for double density media, 1 for high density, and 2 for extended density. */
                if (hole == 1)
                {
                        return fdd[drive].densel != 0 ? 300 : 360;
                }
                else
                {
                        return 300;
                }
        }

        // pcem bug, reproduced: PB-15 — inatteignable, chaque branche ci-dessus retourne.
        switch (fdd[drive].type)
        {
        case 0:
                return 300;
        case 1:
                return 360;
        }
    }

    // pcem: fdd.c:151
    internal static void fdd_setswap(int swap) { fdd_swap = swap != 0 ? 1 : 0; }

    // pcem: fdd.c:153-168
    internal static int fdd_can_read_medium(int drive)
    {
        int hole;

        drive ^= fdd_swap;

        if (drive >= 2)
                return 0;

        hole = disc_hole(drive);

        hole = 1 << (hole + 3);

        return (drive_types[fdd[drive].type].flags & hole) != 0 ? 1 : 0;
    }

    // pcem: fdd.c:170-174
    internal static int fdd_doublestep_40(int drive)
    {
        if (drive >= 2)
                return 0;
        return drive_types[fdd[drive].type].flags & FLAG_DOUBLE_STEP;
    }

    // pcem: fdd.c:176-179
    internal static void fdd_set_type(int drive, int type)
    {
        if (drive < 2)
                fdd[drive].type = type;
    }

    // pcem: fdd.c:181-185
    internal static int fdd_get_type(int drive)
    {
        if (drive >= 2)
                return 0;
        return fdd[drive].type;
    }

    // pcem: fdd.c:187-191
    internal static int fdd_is_525(int drive)
    {
        if (drive >= 2)
                return 0;
        return drive_types[fdd[drive].type].flags & FLAG_525;
    }

    // pcem: fdd.c:193-197
    internal static int fdd_is_ed(int drive)
    {
        if (drive >= 2)
                return 0;
        return drive_types[fdd[drive].type].flags & FLAG_HOLE2;
    }
}
