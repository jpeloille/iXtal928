// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/disc/disc_sector.c + includes/private/disc/disc_sector.h
// SHA256: voir oracle.tsv ; vérifier avec tools/check-oracle.sh
// STATUS: transcribed — la machine à états des formats « par secteurs » (.IMG) :
//         recherche, lecture, écriture, lecture d'adresse, formatage.
//
// noms: PCem → iXtal26 → ce qu'il désigne — R1(e). « = » : nom inchangé.
//   c  cyl          l'en-tête CHRN d'un secteur, vocabulaire du 765 : cylindre,
//   h  head         tête, numéro d'enregistrement, code de taille (128 << size_code).
//   r  sector_id    PAS « sect » : ce fichier porte déjà disc_sector_sector et
//   n  size_code    cur_sector ; une quatrième graphie du mot serait une régression.
//   disc_sector_n   disc_sector_size_code   code de taille demandé par la commande
//   cur_sector      =    où la tête se trouve dans la piste, en secteurs
//   cur_byte        =    où elle se trouve dans le secteur, en octets
//   index_count     =    tours d'index écoulés ; deux tours sans trouver = échec
//   disc_intersector_delay  =  40 tics d'horloge-octet entre deux secteurs (:201)
//   disc_sector_status  =  la cause d'échec à rendre au contrôleur (FDC_STATUS_*)
//   STATE_*         les treize états : chercher, lire, écrire, adresse, formater
//   disc_sector_writeback[]  rappel vers img_writeback : écrit la piste dans le fichier
//   TYPES : sector_t = un secteur et son en-tête CHRN, plus le tampon et l'offset qui
//   remplacent le `uint8_t *data` du C ; disc_sector = le conteneur du fichier.
//   Le sens des appels s'inverse ici : ce fichier RAPPELLE fdc_data, fdc_notfound,
//   fdc_finishread et fdc_writeprotect — le média pousse les octets, le contrôleur subit.

// CS8602 : `s.data[cur_byte]` et `disc_sector_writeback[drive](...)` déréférencent
// des références que l'analyse de nullabilité croit nulles. Elles ne le sont que
// pour un secteur jamais ajouté ou un lecteur sans image — cas où le C
// déréférencerait NULL de la même façon.
#pragma warning disable CS8602

using static iXtal26.Disc.disc;
using static iXtal26.Floppy.fdc_c;
using static iXtal26.Floppy.fdd_c;

namespace iXtal26.Disc;

/*Handling for 'sector based' image formats (like .IMG) as opposed to 'stream based' formats (eg .FDI)*/

// pcem: disc_sector.c:11-15 — copiée par valeur (`sector_t *s = &...` n'est jamais
// stocké) : struct.
// DEVIATION: `uint8_t *data` pointe DANS track_data[side] de disc_img.c ; sans
//   pointeur, c'est le tampon et l'offset — TRANSCRIPTION.md, table des conventions.
internal struct sector_t
{
    internal uint8_t cyl, head, sector_id, size_code;
    internal int rate;
    internal uint8_t[]? data;
    internal int data_off;
}

// pcem: disc_sector.h:14 — void (*disc_sector_writeback[2])(int drive, int track)
internal delegate void disc_sector_writeback_fn(int drive, int track);

internal static partial class disc_sector
{
    // pcem: disc_sector.c:9
    private const int MAX_SECTORS = 256;

    // pcem: disc_sector.c:17-19
    private static sector_t[,,] disc_sector_data = new sector_t[2, 2, MAX_SECTORS];
    private static int[,] disc_sector_count = new int[2, 2];
    internal static disc_sector_writeback_fn?[] disc_sector_writeback = new disc_sector_writeback_fn?[2];

    // pcem: disc_sector.c:21-35
    private const int STATE_IDLE = 0;
    private const int STATE_READ_FIND_SECTOR = 1;
    private const int STATE_READ_SECTOR = 2;
    private const int STATE_READ_FIND_FIRST_SECTOR = 3;
    private const int STATE_READ_FIRST_SECTOR = 4;
    private const int STATE_READ_FIND_NEXT_SECTOR = 5;
    private const int STATE_READ_NEXT_SECTOR = 6;
    private const int STATE_WRITE_FIND_SECTOR = 7;
    private const int STATE_WRITE_SECTOR = 8;
    private const int STATE_READ_FIND_ADDRESS = 9;
    private const int STATE_READ_ADDRESS = 10;
    private const int STATE_FORMAT_FIND = 11;
    private const int STATE_FORMAT = 12;

    // pcem: disc_sector.c:37-48
    internal static int disc_sector_state;
    private static int disc_sector_track;
    private static int disc_sector_side;
    private static int disc_sector_drive;
    private static int disc_sector_sector;
    private static int disc_sector_size_code;
    private static int disc_intersector_delay = 0;
    private static uint8_t disc_sector_fill;
    private static int cur_sector, cur_byte;
    private static int index_count;

    private static int disc_sector_status;

    // pcem: disc_sector.c:50
    internal static void disc_sector_reset(int drive, int side) { disc_sector_count[drive, side] = 0; }

    // pcem: disc_sector.c:52-66
    internal static void disc_sector_add(int drive, int side, uint8_t c, uint8_t h, uint8_t r, uint8_t n, int rate, uint8_t[] data, int data_off)
    {
        sector_t s = new();
        if (disc_sector_count[drive, side] >= MAX_SECTORS)
                return;

        s.cyl = c;
        s.head = h;
        s.sector_id = r;
        s.size_code = n;
        s.rate = rate;
        s.data = data;
        s.data_off = data_off;
        disc_sector_data[drive, side, disc_sector_count[drive, side]] = s;

        disc_sector_count[drive, side]++;
    }

    // pcem: disc_sector.c:68-70
    private static int get_bitcell_period()
    {
        return (disc_sector_data[disc_sector_drive, disc_sector_side, cur_sector].rate * 300) / fdd_getrpm(disc_sector_drive);
    }

    // pcem: disc_sector.c:72-90
    internal static void disc_sector_readsector(int drive, int sector, int track, int side, int rate, int sector_size)
    {
        if (sector == SECTOR_FIRST)
                disc_sector_state = STATE_READ_FIND_FIRST_SECTOR;
        else if (sector == SECTOR_NEXT)
                disc_sector_state = STATE_READ_FIND_NEXT_SECTOR;
        else
                disc_sector_state = STATE_READ_FIND_SECTOR;
        disc_sector_track = track;
        disc_sector_side = side;
        disc_sector_drive = drive;
        disc_sector_sector = sector;
        disc_sector_size_code = sector_size;
        index_count = 0;

        disc_sector_status = FDC_STATUS_AM_NOT_FOUND;
    }

    // pcem: disc_sector.c:92-105
    internal static void disc_sector_writesector(int drive, int sector, int track, int side, int rate, int sector_size)
    {
        disc_sector_state = STATE_WRITE_FIND_SECTOR;
        disc_sector_track = track;
        disc_sector_side = side;
        disc_sector_drive = drive;
        disc_sector_sector = sector;
        disc_sector_size_code = sector_size;
        index_count = 0;

        disc_sector_status = FDC_STATUS_AM_NOT_FOUND;
    }

    // pcem: disc_sector.c:107-118
    internal static void disc_sector_readaddress(int drive, int track, int side, int rate)
    {
        disc_sector_state = STATE_READ_FIND_ADDRESS;
        disc_sector_track = track;
        disc_sector_side = side;
        disc_sector_drive = drive;
        index_count = 0;

        disc_sector_status = FDC_STATUS_AM_NOT_FOUND;
    }

    // pcem: disc_sector.c:120-127
    internal static void disc_sector_format(int drive, int track, int side, int rate, uint8_t fill)
    {
        disc_sector_state = STATE_FORMAT_FIND;
        disc_sector_track = track;
        disc_sector_side = side;
        disc_sector_drive = drive;
        disc_sector_fill = fill;
        index_count = 0;
    }

    // pcem: disc_sector.c:129
    internal static void disc_sector_stop() { disc_sector_state = STATE_IDLE; }

    // pcem: disc_sector.c:131-147
    private static void advance_byte()
    {
        if (disc_intersector_delay != 0)
        {
                disc_intersector_delay--;
                return;
        }
        cur_byte++;
        if (cur_byte >= (128 << disc_sector_data[disc_sector_drive, disc_sector_side, cur_sector].size_code))
        {
                cur_byte = 0;
                cur_sector++;
                if (cur_sector >= disc_sector_count[disc_sector_drive, disc_sector_side])
                {
                        cur_sector = 0;
                        fdc_indexpulse();
                        index_count++;
                }
                disc_intersector_delay = 40;
        }
    }

    // pcem: disc_sector.c:149-373
    internal static void disc_sector_poll()
    {
        sector_t s;
        int data;

        if (cur_sector >= disc_sector_count[disc_sector_drive, disc_sector_side])
                cur_sector = 0;
        if (cur_byte >= (128 << disc_sector_data[disc_sector_drive, disc_sector_side, cur_sector].size_code))
                cur_byte = 0;

        s = disc_sector_data[disc_sector_drive, disc_sector_side, cur_sector];
        switch (disc_sector_state)
        {
        case STATE_IDLE:
                break;

        case STATE_READ_FIND_SECTOR:
                if (index_count > 1)
                {
                        fdc_notfound(disc_sector_status);
                        disc_sector_state = STATE_IDLE;
                        break;
                }
                if (cur_byte == 0 && fdc_get_bitcell_period() == get_bitcell_period() &&
                    fdd_can_read_medium(disc_sector_drive ^ fdd_swap) != 0)
                        disc_sector_status = FDC_STATUS_NOT_FOUND; /*Disc readable, assume address marker found*/

                if (cur_byte != 0 || fdc_get_bitcell_period() != get_bitcell_period() ||
                    fdd_can_read_medium(disc_sector_drive ^ fdd_swap) == 0 || disc_intersector_delay != 0)
                {
                        advance_byte();
                        break;
                }
                if (disc_sector_track != s.cyl || disc_sector_side != s.head || disc_sector_sector != s.sector_id ||
                    disc_sector_size_code != s.size_code)
                {
                        if (disc_sector_track != s.cyl)
                                disc_sector_status =
                                        (disc_sector_track == 0xff) ? FDC_STATUS_BAD_CYLINDER : FDC_STATUS_WRONG_CYLINDER;
                        advance_byte();
                        break;
                }
                disc_sector_state = STATE_READ_SECTOR;
                goto case STATE_READ_SECTOR;
        case STATE_READ_SECTOR:
                if (fdc_data(s.data[s.data_off + cur_byte]) != 0)
                {
                        return;
                }
                advance_byte();
                if (cur_byte == 0)
                {
                        disc_sector_state = STATE_IDLE;
                        fdc_finishread();
                }
                break;

        case STATE_READ_FIND_FIRST_SECTOR:
                if (fdd_can_read_medium(disc_sector_drive ^ fdd_swap) == 0)
                {
                        fdc_notfound(FDC_STATUS_AM_NOT_FOUND);
                        disc_sector_state = STATE_IDLE;
                        break;
                }
                if (cur_byte == 0 && fdc_get_bitcell_period() == get_bitcell_period() &&
                    fdd_can_read_medium(disc_sector_drive ^ fdd_swap) != 0)
                        disc_sector_status = FDC_STATUS_NOT_FOUND; /*Disc readable, assume address marker found*/

                if (cur_byte != 0 || index_count == 0 || fdc_get_bitcell_period() != get_bitcell_period() || disc_intersector_delay != 0)
                {
                        advance_byte();
                        break;
                }
                disc_sector_state = STATE_READ_FIRST_SECTOR;
                goto case STATE_READ_FIRST_SECTOR;
        case STATE_READ_FIRST_SECTOR:
                if (fdc_data(s.data[s.data_off + cur_byte]) != 0)
                        return;
                advance_byte();
                if (cur_byte == 0)
                {
                        disc_sector_state = STATE_IDLE;
                        fdc_finishread();
                }
                break;

        case STATE_READ_FIND_NEXT_SECTOR:
                if (fdd_can_read_medium(disc_sector_drive ^ fdd_swap) == 0)
                {
                        fdc_notfound(FDC_STATUS_AM_NOT_FOUND);
                        disc_sector_state = STATE_IDLE;
                        break;
                }
                if (index_count != 0)
                {
                        fdc_notfound(disc_sector_status);
                        disc_sector_state = STATE_IDLE;
                        break;
                }
                if (cur_byte != 0 || fdc_get_bitcell_period() != get_bitcell_period() || disc_intersector_delay != 0)
                {
                        advance_byte();
                        break;
                }
                disc_sector_state = STATE_READ_NEXT_SECTOR;
                goto case STATE_READ_NEXT_SECTOR;
        case STATE_READ_NEXT_SECTOR:
                if (fdc_data(s.data[s.data_off + cur_byte]) != 0)
                        break;
                advance_byte();
                if (cur_byte == 0)
                {
                        disc_sector_state = STATE_IDLE;
                        fdc_finishread();
                }
                break;

        case STATE_WRITE_FIND_SECTOR:
                if (fdd_can_read_medium(disc_sector_drive ^ fdd_swap) == 0)
                {
                        fdc_notfound(FDC_STATUS_AM_NOT_FOUND);
                        disc_sector_state = STATE_IDLE;
                        break;
                }

                if (cur_byte == 0 && fdc_get_bitcell_period() == get_bitcell_period() &&
                    fdd_can_read_medium(disc_sector_drive ^ fdd_swap) != 0)
                        disc_sector_status = FDC_STATUS_NOT_FOUND; /*Disc readable, assume address marker found*/

                if (writeprot[disc_sector_drive] != 0)
                {
                        fdc_writeprotect();
                        disc_sector_state = STATE_IDLE;
                        return;
                }
                if (index_count > 1)
                {
                        fdc_notfound(disc_sector_status);
                        disc_sector_state = STATE_IDLE;
                        break;
                }
                if (cur_byte != 0 || fdc_get_bitcell_period() != get_bitcell_period() || disc_intersector_delay != 0)
                {
                        advance_byte();
                        break;
                }
                if (disc_sector_track != s.cyl || disc_sector_side != s.head || disc_sector_sector != s.sector_id ||
                    disc_sector_size_code != s.size_code)
                {
                        if (disc_sector_track != s.cyl)
                                disc_sector_status =
                                        (disc_sector_track == 0xff) ? FDC_STATUS_BAD_CYLINDER : FDC_STATUS_WRONG_CYLINDER;
                        advance_byte();
                        break;
                }
                disc_sector_state = STATE_WRITE_SECTOR;
                goto case STATE_WRITE_SECTOR;
        case STATE_WRITE_SECTOR:
                data = fdc_getdata(cur_byte == ((128 << s.size_code) - 1) ? 1 : 0);
                if (data == -1)
                        break;
                s.data[s.data_off + cur_byte] = (uint8_t)data;
                advance_byte();
                if (cur_byte == 0)
                {
                        disc_sector_state = STATE_IDLE;
                        disc_sector_writeback[disc_sector_drive](disc_sector_drive, disc_sector_track);
                        fdc_finishread();
                }
                break;

        case STATE_READ_FIND_ADDRESS:
                if (fdd_can_read_medium(disc_sector_drive ^ fdd_swap) == 0)
                {
                        fdc_notfound(FDC_STATUS_AM_NOT_FOUND);
                        disc_sector_state = STATE_IDLE;
                        break;
                }

                if (cur_byte == 0 && fdc_get_bitcell_period() == get_bitcell_period() &&
                    fdd_can_read_medium(disc_sector_drive ^ fdd_swap) != 0)
                        disc_sector_status = FDC_STATUS_NOT_FOUND; /*Disc readable, assume address marker found*/

                if (index_count != 0)
                {
                        fdc_notfound(disc_sector_status);
                        disc_sector_state = STATE_IDLE;
                        break;
                }
                if (cur_byte != 0 || fdc_get_bitcell_period() != get_bitcell_period() || disc_intersector_delay != 0)
                {
                        advance_byte();
                        break;
                }
                disc_sector_state = STATE_READ_ADDRESS;
                goto case STATE_READ_ADDRESS;
        case STATE_READ_ADDRESS:
                fdc_sectorid(s.cyl, s.head, s.sector_id, s.size_code, 0, 0);
                advance_byte();
                disc_sector_state = STATE_IDLE;
                break;

        case STATE_FORMAT_FIND:
                if (writeprot[disc_sector_drive] != 0)
                {
                        fdc_writeprotect();
                        disc_sector_state = STATE_IDLE;
                        return;
                }
                if (index_count == 0 || fdc_get_bitcell_period() != get_bitcell_period() || disc_intersector_delay != 0)
                {
                        advance_byte();
                        break;
                }
                if (fdd_can_read_medium(disc_sector_drive ^ fdd_swap) == 0)
                {
                        fdc_notfound(FDC_STATUS_NOT_FOUND);
                        disc_sector_state = STATE_IDLE;
                        break;
                }
                if (fdc_get_bitcell_period() != get_bitcell_period())
                {
                        fdc_notfound(FDC_STATUS_NOT_FOUND);
                        disc_sector_state = STATE_IDLE;
                        break;
                }
                disc_sector_state = STATE_FORMAT;
                goto case STATE_FORMAT;
        case STATE_FORMAT:
                if (disc_intersector_delay == 0 && fdc_get_bitcell_period() == get_bitcell_period())
                        s.data[s.data_off + cur_byte] = disc_sector_fill;
                advance_byte();
                if (index_count == 2)
                {
                        disc_sector_writeback[disc_sector_drive](disc_sector_drive, disc_sector_track);
                        fdc_finishread();
                        disc_sector_state = STATE_IDLE;
                }
                break;
        }
    }
}
