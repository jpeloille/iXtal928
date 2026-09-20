// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/disc/disc_img.c + includes/private/disc/disc_img.h
// SHA256: voir oracle.tsv ; vérifier avec tools/check-oracle.sh
// STATUS: transcribed — le chargeur d'images brutes (.IMG/.IMA/.360/.XDF) :
//         géométrie par BPB ou par taille, cartes XDF, lecture/écriture de piste.

// CS8602/CS8604 : `img[drive].f` est testé contre null en tête de chaque fonction,
// mais l'analyse de nullabilité ne suit pas un champ atteint à travers un index de
// tableau — ni pour le déréférencer, ni pour le passer à fread/fwrite. Même cas
// qu'à io.cs:14.
#pragma warning disable CS8602, CS8604

using static iXtal26.Disc.disc;
using static iXtal26.Disc.disc_sector;
using static iXtal26.Floppy.fdd_c;

namespace iXtal26.Disc;

// pcem: disc_img.c:7-16 — la struct anonyme de PCem. Classe : track_data est
// adressé par disc_sector_add et le tampon survit à l'appel.
internal sealed class img_t
{
    internal FileStream? f;
    internal uint8_t[][] track_data = { new uint8_t[20 * 1024], new uint8_t[20 * 1024] };
    internal int sectors, tracks, sides;
    internal int sector_size;
    internal int rate;
    internal int xdf_type; /* 0 = not XDF, 1-5 = one of the five XDF types */
    internal int hole;
    internal double bitcell_period_300rpm;
}

internal static partial class disc_img
{
    // pcem: disc_img.c:7-16
    private static img_t[] img = { new(), new() };

    // pcem: disc_img.c:18-20
    // DEVIATION: tableaux en escalier plutôt que rectangulaires, pour qu'add_to_map
    //   reçoive une ligne là où le C reçoit un pointeur ; les lignes sont allouées
    //   dans le constructeur statique, là où le C les a en statique.
    private static uint8_t[][] xdf_track0 = new uint8_t[5][];
    private static uint8_t[] xdf_spt = new uint8_t[5];
    private static uint8_t[][][] xdf_map = new uint8_t[5][][];

    static disc_img()
    {
        for (int i = 0; i < 5; i++)
        {
                xdf_track0[i] = new uint8_t[3];
                xdf_map[i] = new uint8_t[24][];
                for (int j = 0; j < 24; j++)
                        xdf_map[i][j] = new uint8_t[3];
        }
    }

    // pcem: disc_img.c:22
    internal static int bpb_disable;

    // DEVIATION: stdio de la libc, comme rom.cs:34. `FILE *` devient FileStream ;
    //   fopen garde ses deux modes ("rb+" puis "rb", disc_img.c:133-135), fread et
    //   fwrite gardent leur forme (tampon, taille, nombre, flux) ; fseek, fclose et
    //   fgetc se lisent directement sur le flux.
    internal static FileStream? fopen(string s, string mode)
    {
            try { return new FileStream(s, FileMode.Open, mode == "rb+" ? FileAccess.ReadWrite : FileAccess.Read); }
            catch (IOException) { return null; }
            catch (UnauthorizedAccessException) { return null; }
            catch (ArgumentException) { return null; }
    }

    private static int fread(uint8_t[] buf, int size, int count, FileStream fp)
            => fp.ReadAtLeast(buf.AsSpan(0, size * count), size * count, false) / size;

    private static void fwrite(uint8_t[] buf, int size, int count, FileStream fp)
            => fp.Write(buf, 0, size * count);

    // pcem: disc_img.c:26-46
    private static int img_sector_size_code(int drive)
    {
        switch (img[drive].sector_size)
        {
        case 128:
                return 0;
        case 256:
                return 1;
        default:
        case 512:
                return 2;
        case 1024:
                return 3;
        case 2048:
                return 4;
        case 4096:
                return 5;
        case 8192:
                return 6;
        case 16384:
                return 7;
        }
    }

    // pcem: disc_img.c:48-51
    internal static void img_init()
    {
        img[0] = new(); img[1] = new();   // memset(img, 0, sizeof(img))
    }

    // pcem: disc_img.c:53-57
    private static void add_to_map(uint8_t[] arr, uint8_t p1, uint8_t p2, uint8_t p3)
    {
        arr[0] = p1;
        arr[1] = p2;
        arr[2] = p3;
    }

    // pcem: disc_img.c:59
    private static int xdf_maps_initialized = 0;

    // pcem: disc_img.c:61-116
    private static void initialize_xdf_maps()
    {
        // XDF 5.25" 2HD
        /* Adds, in this order: sectors per FAT, sectors per each side of track 0, difference between that and virtual sector
         * number specified in BPB. */
        add_to_map(xdf_track0[0], 9, 17, 2);
        xdf_spt[0] = 3;
        /* Adds, in this order: side, sequential order (not used in PCem), sector size. */
        add_to_map(xdf_map[0][0], 0, 0, 3);
        add_to_map(xdf_map[0][1], 0, 2, 6);
        add_to_map(xdf_map[0][2], 1, 0, 2);
        add_to_map(xdf_map[0][3], 0, 1, 2);
        add_to_map(xdf_map[0][4], 1, 2, 6);
        add_to_map(xdf_map[0][5], 1, 1, 3);

        // XDF 3.5" 2HD
        add_to_map(xdf_track0[1], 11, 19, 4);
        xdf_spt[1] = 4;
        add_to_map(xdf_map[1][0], 0, 0, 3);
        add_to_map(xdf_map[1][1], 0, 2, 4);
        add_to_map(xdf_map[1][2], 1, 3, 6);
        add_to_map(xdf_map[1][3], 0, 1, 2);
        add_to_map(xdf_map[1][4], 1, 1, 2);
        add_to_map(xdf_map[1][5], 0, 3, 6);
        add_to_map(xdf_map[1][6], 1, 0, 4);
        add_to_map(xdf_map[1][7], 1, 2, 3);

        // XDF 3.5" 2ED
        add_to_map(xdf_track0[2], 22, 37, 9);
        xdf_spt[2] = 4;
        add_to_map(xdf_map[2][0], 0, 0, 3);
        add_to_map(xdf_map[2][1], 0, 1, 4);
        add_to_map(xdf_map[2][2], 0, 2, 5);
        add_to_map(xdf_map[2][3], 0, 3, 7);
        add_to_map(xdf_map[2][4], 1, 0, 3);
        add_to_map(xdf_map[2][5], 1, 1, 4);
        add_to_map(xdf_map[2][6], 1, 2, 5);
        add_to_map(xdf_map[2][7], 1, 3, 7);

        // XXDF 3.5" 2HD
        add_to_map(xdf_track0[3], 12, 20, 4);
        xdf_spt[3] = 2;
        add_to_map(xdf_map[3][0], 0, 0, 5);
        add_to_map(xdf_map[3][1], 1, 1, 6);
        add_to_map(xdf_map[3][2], 0, 1, 6);
        add_to_map(xdf_map[3][3], 1, 0, 5);

        // XXDF 3.5" 2ED
        add_to_map(xdf_track0[4], 21, 39, 9);
        xdf_spt[4] = 2;
        add_to_map(xdf_map[4][0], 0, 0, 6);
        add_to_map(xdf_map[4][1], 1, 1, 7);
        add_to_map(xdf_map[4][2], 0, 1, 7);
        add_to_map(xdf_map[4][3], 1, 0, 6);

        xdf_maps_initialized = 1;
    }

    // pcem: disc_img.c:118-319
    internal static void img_load(int drive, string fn)
    {
        int size;
        double bit_rate_300;
        uint16_t bpb_bps;
        uint16_t bpb_total;
        uint8_t bpb_mid; /* Media type ID. */
        uint8_t bpb_sectors;
        uint8_t bpb_sides;
        uint32_t bpt;
        uint8_t max_spt = 0; /* Used for XDF detection. */

        if (xdf_maps_initialized == 0)
                initialize_xdf_maps(); /* Initialize XDF maps, will need them to properly register sectors in tracks. */

        writeprot[drive] = 0;
        img[drive].f = fopen(fn, "rb+");
        if (img[drive].f == null)
        {
                img[drive].f = fopen(fn, "rb");
                if (img[drive].f == null)
                        return;
                writeprot[drive] = 1;
        }
        fwriteprot[drive] = writeprot[drive];

        /* Read the BPB */
        // DEVIATION: `fread(&bpb_bps, 1, 2, f)` lit un uint16_t dans l'ordre de l'hôte,
        //   petit-boutien sur x86 ; recomposé ici octet par octet dans le même ordre.
        img[drive].f.Seek(0x0B, SeekOrigin.Begin);
        bpb_bps = (uint16_t)(img[drive].f.ReadByte() | (img[drive].f.ReadByte() << 8));
        img[drive].f.Seek(0x13, SeekOrigin.Begin);
        bpb_total = (uint16_t)(img[drive].f.ReadByte() | (img[drive].f.ReadByte() << 8));
        img[drive].f.Seek(0x15, SeekOrigin.Begin);
        bpb_mid = (uint8_t)img[drive].f.ReadByte();
        img[drive].f.Seek(0x18, SeekOrigin.Begin);
        bpb_sectors = (uint8_t)img[drive].f.ReadByte();
        img[drive].f.Seek(0x1A, SeekOrigin.Begin);
        bpb_sides = (uint8_t)img[drive].f.ReadByte();

        img[drive].f.Seek(-1, SeekOrigin.End);
        size = (int)img[drive].f.Position + 1;

        img[drive].sides = 2;
        img[drive].sector_size = 512;

        // omitted: pclog("BPB reports %i sides and %i bytes per sector\n", ...) — sortie pure

        if (bpb_disable != 0 || (bpb_sides < 1) || (bpb_sides > 2) || (bpb_bps < 128) || (bpb_bps > 2048))
        {
                /* The BPB is giving us a wacky number of sides and/or bytes per sector, therefore it is most probably
                   not a BPB at all, so we have to guess the parameters from file size. */

                if (size <= (160 * 1024))
                {
                        img[drive].sectors = 8;
                        img[drive].tracks = 40;
                        img[drive].sides = 1;
                        bit_rate_300 = 250;
                }
                else if (size <= (180 * 1024))
                {
                        img[drive].sectors = 9;
                        img[drive].tracks = 40;
                        img[drive].sides = 1;
                        bit_rate_300 = 250;
                }
                else if (size <= (320 * 1024))
                {
                        img[drive].sectors = 8;
                        img[drive].tracks = 40;
                        bit_rate_300 = 250;
                }
                else if (size <= (360 * 1024))
                {
                        img[drive].sectors = 9;
                        img[drive].tracks = 40;
                        bit_rate_300 = 250;
                } /*Double density*/
                else if (size < (1024 * 1024))
                {
                        img[drive].sectors = 9;
                        img[drive].tracks = 80;
                        bit_rate_300 = 250;
                } /*Double density*/
                else if (size <= 1228800)
                {
                        img[drive].sectors = 15;
                        img[drive].tracks = 80;
                        bit_rate_300 = (500.0 * 300.0) / 360.0;
                } /*High density 1.2MB*/
                else if (size <= (0x1A4000 - 1))
                {
                        img[drive].sectors = 18;
                        img[drive].tracks = 80;
                        bit_rate_300 = 500;
                } /*High density (not supported by Tandy 1000)*/
                else if (size <= 2000000)
                {
                        img[drive].sectors = 21;
                        img[drive].tracks = 80;
                        bit_rate_300 = 500;
                } /*DMF format - used by Windows 95 - changed by OBattler to 2000000, ie. the real unformatted capacity @ 500 kbps
                     and 300 rpm */
                else
                {
                        img[drive].sectors = 36;
                        img[drive].tracks = 80;
                        bit_rate_300 = 1000;
                } /*E density*/

                img[drive].xdf_type = 0;
        }
        else
        {
                /* The BPB readings appear to be valid, so let's set the values. */
                /* Number of tracks = number of total sectors divided by sides times sectors per track. */
                img[drive].tracks = (int)(((uint32_t)bpb_total) / (((uint32_t)bpb_sides) * ((uint32_t)bpb_sectors)));
                /* The rest we just set directly from the BPB. */
                img[drive].sectors = bpb_sectors;
                img[drive].sides = bpb_sides;
                /* Now we calculate bytes per track, which is bpb_sectors * bpb_bps. */
                bpt = (uint32_t)bpb_sectors * (uint32_t)bpb_bps;
                /* Now we should be able to calculate the bit rate. */
                // omitted: pclog("The image has %i bytes per track\n", bpt) — sortie pure
                if (bpt <= 6250)
                        bit_rate_300 = 250; /* Double-density */
                else if (bpt <= 7500)
                        bit_rate_300 = 300; /* Double-density, 300 kbps @ 300 rpm */
                else if (bpt <= 10416)
                {
                        bit_rate_300 = (bpb_mid == 0xF0)
                                               ? 500
                                               : ((500.0 * 300.0) /
                                                  360.0); /* High-density @ 300 or 360 rpm, depending on media type ID */
                        max_spt = (uint8_t)((bpb_mid == 0xF0) ? 22 : 18);
                }
                else if (bpt <= 12500) /* High-density @ 300 rpm */
                {
                        bit_rate_300 = 500;
                        max_spt = 22;
                }
                else if (bpt <= 25000) /* Extended density @ 300 rpm */
                {
                        bit_rate_300 = 1000;
                        max_spt = 45;
                }
                else /* Image too big, eject */
                {
                        // omitted: pclog("Image has more than 25000 bytes per track, ejecting...\n") — sortie pure
                        // pcem bug, reproduced: PB-16 — f reste non nul après ce fclose.
                        img[drive].f.Close();
                        return;
                }

                if (bpb_bps == 512) /* BPB reports 512 bytes per sector, let's see if it's XDF or not */
                {
                        if (bit_rate_300 <= 300) /* Double-density disk, not XDF */
                        {
                                img[drive].xdf_type = 0;
                        }
                        else
                        {
                                if (bpb_sectors > max_spt)
                                {
                                        switch (bpb_sectors)
                                        {
                                        case 19: /* High density XDF @ 360 rpm */
                                                img[drive].xdf_type = 1;
                                                break;
                                        case 23: /* High density XDF @ 300 rpm */
                                                img[drive].xdf_type = 2;
                                                break;
                                        case 24: /* High density XXDF @ 300 rpm */
                                                img[drive].xdf_type = 4;
                                                break;
                                        case 46: /* Extended density XDF */
                                                img[drive].xdf_type = 3;
                                                break;
                                        case 48: /* Extended density XXDF */
                                                img[drive].xdf_type = 5;
                                                break;
                                        default: /* Unknown, as we're beyond maximum sectors, get out */
                                                // pcem bug, reproduced: PB-16 — f reste non nul après ce fclose.
                                                img[drive].f.Close();
                                                return;
                                        }
                                }
                                else /* Amount of sectors per track that fits into a track, therefore not XDF */
                                {
                                        img[drive].xdf_type = 0;
                                }
                        }
                }
                else /* BPB reports sector size other than 512, can't possibly be XDF */
                {
                        img[drive].xdf_type = 0;
                }
        }

        if ((bit_rate_300 == 250) || (bit_rate_300 == 300))
        {
                img[drive].hole = 0;
        }
        else if (bit_rate_300 == 1000)
        {
                img[drive].hole = 2;
        }
        else
        {
                img[drive].hole = 1;
        }

        if (img[drive].xdf_type != 0) /* In case of XDF-formatted image, write-protect */
        {
                writeprot[drive] = 1;
                fwriteprot[drive] = writeprot[drive];
        }

        drives[drive].seek = img_seek;
        drives[drive].readsector = disc_sector_readsector;
        drives[drive].writesector = disc_sector_writesector;
        drives[drive].readaddress = disc_sector_readaddress;
        drives[drive].hole = img_hole;
        drives[drive].poll = disc_sector_poll;
        drives[drive].format = disc_sector_format;
        disc_sector_writeback[drive] = img_writeback;

        img[drive].bitcell_period_300rpm = 1000000.0 / bit_rate_300 * 2.0;
        // omitted: pclog x3 (disc_img.c:314-318) — sorties pures
    }

    // pcem: disc_img.c:321
    internal static int img_hole(int drive) { return img[drive].hole; }

    // pcem: disc_img.c:323-327
    internal static void img_close(int drive)
    {
        if (img[drive].f != null)
                img[drive].f.Close();
        img[drive].f = null;
    }

    // pcem: disc_img.c:329-423
    internal static void img_seek(int drive, int track)
    {
        int side;
        int current_xdft = img[drive].xdf_type - 1;

        uint8_t sectors_fat, effective_sectors; /* Needed for XDF */

        if (img[drive].f == null)
                return;
        // omitted: pclog("Seek drive=%i track=%i ...") — sortie pure
        if (img[drive].tracks <= 41 && fdd_doublestep_40(drive) != 0)
                track /= 2;

        // omitted: pclog("Disk seeked to track %i\n", track) — sortie pure
        disc_track[drive] = track;

        // pcem bug, reproduced: PB-17 — track_data fait 20 Ko, une piste XDF ED en
        //   demande 23 552 ; le C déborde, le C# lève.
        if (img[drive].sides == 2)
        {
                img[drive].f.Seek(track * img[drive].sectors * img[drive].sector_size * 2, SeekOrigin.Begin);
                fread(img[drive].track_data[0], img[drive].sectors * img[drive].sector_size, 1, img[drive].f);
                fread(img[drive].track_data[1], img[drive].sectors * img[drive].sector_size, 1, img[drive].f);
        }
        else
        {
                img[drive].f.Seek(track * img[drive].sectors * img[drive].sector_size, SeekOrigin.Begin);
                fread(img[drive].track_data[0], img[drive].sectors * img[drive].sector_size, 1, img[drive].f);
        }

        disc_sector_reset(drive, 0);
        disc_sector_reset(drive, 1);

        int sector, current_pos;

        if (img[drive].xdf_type != 0)
        {
                sectors_fat = xdf_track0[current_xdft][0];
                effective_sectors = xdf_track0[current_xdft][1];

                if (track == 0)
                {
                        /* Track 0, register sectors according to track 0 map. */
                        /* First, the "Side 0" buffer, will also contain one sector from side 1. */
                        current_pos = 0;
                        for (sector = 0; sector < sectors_fat; sector++)
                        {
                                disc_sector_add(drive, 0, (uint8_t)track, 0, (uint8_t)(sector + 0x81), 2, (int)img[drive].bitcell_period_300rpm,
                                                img[drive].track_data[0], current_pos);
                                current_pos += 512;
                        }
                        disc_sector_add(drive, 1, (uint8_t)track, 1, 0x81, 2, (int)img[drive].bitcell_period_300rpm,
                                        img[drive].track_data[0], current_pos);
                        current_pos += 512;
                        for (sector = 0; sector < 8; sector++)
                        {
                                disc_sector_add(drive, 0, (uint8_t)track, 0, (uint8_t)(sector + 1), 2, (int)img[drive].bitcell_period_300rpm,
                                                img[drive].track_data[0], current_pos);
                                current_pos += 512;
                        }
                        /* Now the "Side 1" buffer, will also contain one sector from side 0. */
                        current_pos = 0;
                        for (sector = 0; sector < 14; sector++)
                        {
                                disc_sector_add(drive, 1, (uint8_t)track, 1, (uint8_t)(sector + 0x82), 2, (int)img[drive].bitcell_period_300rpm,
                                                img[drive].track_data[1], current_pos);
                                current_pos += 512;
                        }
                        current_pos += (5 * 512);
                        for (; sector < effective_sectors - 1; sector++)
                        {
                                disc_sector_add(drive, 1, (uint8_t)track, 1, (uint8_t)(sector + 0x82), 2, (int)img[drive].bitcell_period_300rpm,
                                                img[drive].track_data[1], current_pos);
                                current_pos += 512;
                        }
                        current_pos += 512;
                }
                else
                {
                        /* Non-zero track, this will have sectors of various sizes. */
                        /* First, the "Side 0" buffer. */
                        current_pos = 0;
                        for (sector = 0; sector < xdf_spt[current_xdft]; sector++)
                        {
                                disc_sector_add(drive, xdf_map[current_xdft][sector][0], (uint8_t)track, xdf_map[current_xdft][sector][0],
                                                (uint8_t)(xdf_map[current_xdft][sector][2] + 0x80), xdf_map[current_xdft][sector][2],
                                                (int)img[drive].bitcell_period_300rpm, img[drive].track_data[0], current_pos);
                                current_pos += (128 << xdf_map[current_xdft][sector][2]);
                        }
                        /* Then, the "Side 1" buffer. */
                        current_pos = 0;
                        for (sector = xdf_spt[current_xdft]; sector < (xdf_spt[current_xdft] << 1); sector++)
                        {
                                disc_sector_add(drive, xdf_map[current_xdft][sector][0], (uint8_t)track, xdf_map[current_xdft][sector][0],
                                                (uint8_t)(xdf_map[current_xdft][sector][2] + 0x80), xdf_map[current_xdft][sector][2],
                                                (int)img[drive].bitcell_period_300rpm, img[drive].track_data[1], current_pos);
                                current_pos += (128 << xdf_map[current_xdft][sector][2]);
                        }
                }
        }
        else
        {
                for (side = 0; side < img[drive].sides; side++)
                {
                        for (sector = 0; sector < img[drive].sectors; sector++)
                                disc_sector_add(drive, side, (uint8_t)track, (uint8_t)side, (uint8_t)(sector + 1), (uint8_t)img_sector_size_code(drive),
                                                (int)img[drive].bitcell_period_300rpm,
                                                img[drive].track_data[side], sector * img[drive].sector_size);
                }
        }
    }

    // pcem: disc_img.c:424-439
    internal static void img_writeback(int drive, int track)
    {
        if (img[drive].f == null)
                return;

        if (img[drive].xdf_type != 0)
                return; /*Should never happen*/

        if (img[drive].sides == 2)
        {
                img[drive].f.Seek(track * img[drive].sectors * img[drive].sector_size * 2, SeekOrigin.Begin);
                fwrite(img[drive].track_data[0], img[drive].sectors * img[drive].sector_size, 1, img[drive].f);
                fwrite(img[drive].track_data[1], img[drive].sectors * img[drive].sector_size, 1, img[drive].f);
        }
        else
        {
                img[drive].f.Seek(track * img[drive].sectors * img[drive].sector_size, SeekOrigin.Begin);
                fwrite(img[drive].track_data[0], img[drive].sectors * img[drive].sector_size, 1, img[drive].f);
        }
    }
}
