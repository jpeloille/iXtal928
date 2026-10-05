// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/scsi/scsi_zip.c  (scsi_zip.h : includes/private/scsi/scsi_zip.h)
// STATUS: deviated — G10.6 (PLAN-G10.md) : le lecteur ZIP 100 de PCem, entier — zip_load, zip_eject,
//
// DEVIATIONS (R9, PB-125) : data_in et data_out débordés (:209, :222-224, :979, :985-990) — l'octet au-delà
//   du tampon est compté, pas gardé, et se relit nul. Le secteur hors de l'image va au garde de hdd_file.cs.
// Reproduits : PB-126 (READ CAPACITY, le secteur 196 608, START STOP UNIT, la perte au reset, les phases
//   de longueur nulle).

// CS8981 : `scsi_zip_c` porte le nom de l'unité C, que le membre `scsi_zip` garde (TRANSCRIPTION.md).
#pragma warning disable CS8981

using iXtal26.Diag;
using iXtal26.Disc;
using iXtal26.Ide;
using static iXtal26.Ide.ide_atapi;
using static iXtal26.Scsi.scsi;
using static iXtal26.timer;

namespace iXtal26.Scsi;

// pcem: scsi_zip.c:37-74 — classe : le bus et le pont en gardent l'adresse (device_data, timer_add).
internal sealed class scsi_zip_data
{
    internal int blocks;

    internal int cmd_pos, new_cmd_pos;

    internal int addr, len;
    internal int sector_pos;

    internal uint8_t[] buf = new uint8_t[512];

    internal uint8_t status;

    internal uint8_t[] data_in = new uint8_t[scsi_zip_c.BUFFER_SIZE];
    internal uint8_t[] data_out = new uint8_t[scsi_zip_c.BUFFER_SIZE];
    internal int data_pos_read, data_pos_write;

    internal int bytes_received, bytes_required;

    internal hdd_file_t hdd = new();
    internal int disc_loaded;
    internal int disc_changed;

    internal int sense_key, asc, ascq;

    internal int hd_id;

    internal pc_timer_t callback_timer = new();

    internal scsi_bus_t? bus;

    internal int is_atapi;
    internal atapi_device_t? atapi_dev;

    internal int read_only;

    internal int pio_mode, mdma_mode;
}

internal static class scsi_zip_c
{
    // DEVIATION: pclog() appartient à plugin-api/logging.c, pas transcrit. Shim local, comme hdd_file.cs.
    private static void pclog(string s) { }

    // pcem: plugin-api/logging.c — warning(), une boîte de dialogue chez PCem ; ici l'erreur standard.
    private static void warning(string s) => Console.Error.Write(s);

    // pcem: scsi_zip.c:13-20
    internal const int BUFFER_SIZE = 256 * 1024;

    internal const int ZIP_SECTORS = 96 * 2048;

    private static readonly uint64_t RW_DELAY = TIMER_USEC * 500;

    private const uint8_t SCSI_IOMEGA_SENSE = 0x06;
    private const uint8_t SCSI_IOMEGA_EJECT = 0x0d; /*ATAPI only?*/

    // pcem: scsi_zip.c:30
    private const uint8_t SCSI_IOMEGA_SET_PROTECTION_MODE = 0x0c;

    // pcem: scsi_zip.c:34
    private const uint8_t ATAPI_READ_FORMAT_CAPACITIES = 0x23;

    // pcem: scsi_zip.c:36
    private const int CMD_POS_IDLE = 0, CMD_POS_WAIT = 1, CMD_POS_START_SECTOR = 2, CMD_POS_TRANSFER = 3;

    // pcem: scsi_zip.c:76
    internal static scsi_zip_data? zip_data;

    // pcem: scsi_zip.c:78
    private const int CHECK_READY = 2;

    // pcem: scsi_zip.c:80-103
    private static readonly uint8_t[] scsi_zip_cmd_flags = scsi_zip_cmd_flags_init();

    private static uint8_t[] scsi_zip_cmd_flags_init()
    {
        var t = new uint8_t[0x100];
        t[SCSI_TEST_UNIT_READY] = CHECK_READY;
        t[SCSI_REQUEST_SENSE] = 0;
        t[SCSI_READ_6] = CHECK_READY;
        t[SCSI_INQUIRY] = 0;
        t[SCSI_MODE_SELECT_6] = 0;
        t[SCSI_MODE_SENSE_6] = 0;
        t[SCSI_START_STOP_UNIT] = 0;
        t[SCSI_PREVENT_ALLOW_MEDIUM_REMOVAL] = CHECK_READY;
        t[SCSI_READ_10] = CHECK_READY;
        t[SCSI_SEEK_6] = CHECK_READY;
        t[SCSI_SEEK_10] = CHECK_READY;
        t[SCSI_IOMEGA_SENSE] = 0;
        t[SCSI_REZERO_UNIT] = CHECK_READY;
        t[SCSI_READ_CAPACITY_10] = CHECK_READY;
        t[SCSI_WRITE_6] = CHECK_READY;
        t[SCSI_WRITE_10] = CHECK_READY;
        t[SCSI_WRITE_AND_VERIFY] = CHECK_READY;
        t[SCSI_VERIFY_10] = CHECK_READY;
        t[SCSI_FORMAT] = CHECK_READY;
        t[SCSI_RESERVE] = 0;
        t[SCSI_RELEASE] = 0;
        t[SEND_DIAGNOSTIC] = 0;
        return t;
    }

    // pcem: scsi_zip.c:105-139
    internal static void zip_load(string fn)
    {
        if (zip_data is not null)
        {
                FileStream? f;
                int read_only = 0;

                f = Open(fn, FileAccess.ReadWrite);
                if (f is null)
                {
                        f = Open(fn, FileAccess.Read);
                        read_only = 1;
                }
                if (f is not null)
                {
                        int size;

                        // DEVIATION: fseek(f, -1, SEEK_END) puis ftell(f) + 1 : la longueur ; un fichier vide, où fseek échoue,
                        //   rend 1.
                        size = f.Length > 0 ? (int)Math.Min(f.Length, int.MaxValue) : 1;
                        f.Dispose();

                        if (size != ZIP_SECTORS * 512)
                        {
                                warning($"File is incorrect size for Zip image\nMust be exactly {ZIP_SECTORS * 512} bytes\n");
                                return;
                        }

                        if (zip_data.disc_loaded != 0)
                                hdd_file.hdd_close(zip_data.hdd);

                        hdd_file.hdd_load_ext(zip_data.hdd, fn, 2048, 1, 96, read_only);

                        if (zip_data.hdd.f is not null)
                        {
                                zip_data.disc_loaded = 1;
                                zip_data.disc_changed = 1;
                                zip_data.read_only = read_only;
                        }
                }
        }
    }

    // DEVIATION: fopen(fn, "rb+") puis fopen(fn, "rb") : null si l'ouverture échoue ; sans verrou, comme stdio.
    private static FileStream? Open(string fn, FileAccess access)
    {
        try
        {
            return new FileStream(fn, FileMode.Open, access, FileShare.ReadWrite | FileShare.Delete);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    // pcem: scsi_zip.c:141-148
    internal static void zip_eject()
    {
        if (zip_data is not null)
        {
                if (zip_data.disc_loaded != 0)
                {
                        hdd_file.hdd_close(zip_data.hdd);
                        zip_data.disc_loaded = 0;
                }
        }
    }

    // pcem: scsi_zip.c:150-154
    internal static int zip_loaded()
    {
        if (zip_data is not null)
                return zip_data.disc_loaded;
        return 0;
    }

    // pcem: scsi_zip.c:156-163
    private static void scsi_zip_callback(object? p)
    {
        scsi_zip_data data = (scsi_zip_data)p!;

        if (data.cmd_pos == CMD_POS_WAIT)
        {
                data.cmd_pos = data.new_cmd_pos;
                scsi_bus_kick(data.bus!);
        }
    }

    // pcem: scsi_zip.c:165-177
    private static object? scsi_zip_init(scsi_bus_t bus, int id)
    {
        scsi_zip_data data = new();

        data.disc_loaded = 0;

        data.hd_id = id;
        data.bus = bus;
        timer_add(data.callback_timer, scsi_zip_callback, data, 0);

        zip_data = data;
        return data;
    }

    // pcem: scsi_zip.c:179-186
    private static object? scsi_zip_atapi_init(scsi_bus_t bus, int id, atapi_device_t atapi_dev)
    {
        scsi_zip_data data = (scsi_zip_data)scsi_zip_init(bus, id)!;

        data.is_atapi = 1;
        data.atapi_dev = atapi_dev;

        return data;
    }

    // pcem: scsi_zip.c:188-195 — free(data) : le GC.
    private static void scsi_zip_close(object p)
    {
        scsi_zip_data data = (scsi_zip_data)p;

        if (data.disc_loaded != 0)
                hdd_file.hdd_close(data.hdd);
        zip_data = null;
    }

    // pcem: scsi_zip.c:197-202
    private static void scsi_zip_reset(object p)
    {
        scsi_zip_data data = (scsi_zip_data)p;

        timer_disable(data.callback_timer);
        data.cmd_pos = CMD_POS_IDLE;
    }

    // pcem: scsi_zip.c:204-217
    private static int scsi_add_data(uint8_t val, scsi_zip_data data)
    {

        // pcem bug, not reproduced: PB-125 — data_in[data_pos_write++] sans borne (:209) : READ(10) de plus de
        //   512 secteurs écrit au-delà de data_in, dans data_out puis hors de la structure.
        // DEVIATION: au-delà du tampon, l'octet est compté, pas gardé (il se relit nul, scsi_zip_read).
        if (data.data_pos_write >= BUFFER_SIZE)
        {
                R9.Garde("scsi_zip.c:209");
                data.data_pos_write++;
                return 0;
        }
        data.data_in[data.data_pos_write++] = val;



        return 0;
    }

    // pcem: scsi_zip.c:219-227
    private static int scsi_get_data(scsi_zip_data data)
    {
        // pcem bug, not reproduced: PB-125 — data_out[data_pos_read++] lu, puis
        //   pc.fatal("scsi_get_data beyond buffer limits\n") (:222-224) : WRITE de plus de 512 secteurs.
        // DEVIATION: au-delà du tampon, l'octet se lit nul, la position avance.
        if (data.data_pos_read >= BUFFER_SIZE)
        {
                R9.Garde("scsi_zip.c:224");
                data.data_pos_read++;
                return 0;
        }
        uint8_t val = data.data_out[data.data_pos_read++];

        return val;
    }

    // pcem: scsi_zip.c:229-234
    private static void scsi_zip_illegal(scsi_zip_data data)
    {
        data.status = STATUS_CHECK_CONDITION;
        data.sense_key = KEY_ILLEGAL_REQ;
        data.asc = ASC_INVALID_LUN;
        data.ascq = 0;
    }

    // pcem: scsi_zip.c:236-241
    private static void scsi_zip_cmd_error(scsi_zip_data data, int sensekey, int asc, int ascq)
    {
        data.status = STATUS_CHECK_CONDITION;
        data.sense_key = sensekey;
        data.asc = asc;
        data.ascq = ascq;
    }

    // pcem: scsi_zip.c:243-248 — la macro add_data_len(v), sur i et len de scsi_zip_command.
    private static void add_data_len(scsi_zip_data data, ref int i, int len, int v)
    {
        if (i < len)
                scsi_add_data((uint8_t)v, data);
        i++;
    }

    // pcem: scsi_zip.c:250-974
    private static int scsi_zip_command(uint8_t[] cdb, object p)
    {
        scsi_zip_data data = (scsi_zip_data)p;
        int /*addr, */ len;
        int i = 0, c;
        int desc;
        int bus_state = 0;

        if (data.cmd_pos == CMD_POS_IDLE)
                pclog($"SCSI ZIP command {cdb[0]:x2} {data.disc_loaded}\n");
        data.status = STATUS_GOOD;
        data.data_pos_read = data.data_pos_write = 0;

        if ((cdb[0] != SCSI_REQUEST_SENSE /* && cdb[0] != SCSI_INQUIRY*/) && (cdb[1] & 0xe0) != 0)
        {
                pclog("non-zero LUN\n");
                /*Non-zero LUN - abort command*/
                scsi_zip_illegal(data);
                bus_state = BUS_CD | BUS_IO;
                return bus_state;
        }

        if ((scsi_zip_cmd_flags[cdb[0]] & CHECK_READY) != 0 && data.disc_loaded == 0)
        {
                pclog("Disc not loaded\n");
                scsi_zip_cmd_error(data, KEY_NOT_READY, ASC_MEDIUM_NOT_PRESENT, 0);
                bus_state = BUS_CD | BUS_IO;
                return bus_state;
        }
        if ((scsi_zip_cmd_flags[cdb[0]] & CHECK_READY) != 0 && data.disc_changed != 0)
        {
                pclog("Disc changed\n");
                scsi_zip_cmd_error(data, KEY_UNIT_ATTENTION, ASC_MEDIUM_MAY_HAVE_CHANGED, 0);
                data.disc_changed = 0;
                bus_state = BUS_CD | BUS_IO;
                return bus_state;
        }

        switch (cdb[0])
        {
        case SCSI_TEST_UNIT_READY:
                bus_state = BUS_CD | BUS_IO;
                break;

        case SCSI_IOMEGA_SENSE:
                len = cdb[4];
                if (cdb[2] == 1)
                {
                        pclog($"SCSI_IOMEGA_SENSE 1 {len:x2}\n");
                        add_data_len(data, ref i, len, 0x58);
                        add_data_len(data, ref i, len, 0);
                        /*This page is related to disc health status - setting this page to 0
                          makes disc health read as 'marginal'*/
                        for (c = 2; c < 0x58; c++)
                                add_data_len(data, ref i, len, 0xff);
                        while (i < len)
                                add_data_len(data, ref i, len, 0);
                        data.cmd_pos = CMD_POS_IDLE;
                        bus_state = BUS_IO;
                }
                else if (cdb[2] == 2)
                {
                        pclog($"SCSI_IOMEGA_SENSE 2 {len:x2}\n");
                        add_data_len(data, ref i, len, 0x3d);
                        add_data_len(data, ref i, len, 0);
                        for (c = 0; c < 19; c++)
                                add_data_len(data, ref i, len, 0);
                        if (data.read_only != 0)
                                add_data_len(data, ref i, len, 2);
                        else
                                add_data_len(data, ref i, len, 0);
                        for (c = 0; c < 39; c++)
                                add_data_len(data, ref i, len, 0);
                        while (i < len)
                                add_data_len(data, ref i, len, 0);
                        data.cmd_pos = CMD_POS_IDLE;
                        bus_state = BUS_IO;
                }
                else
                {
                        pclog($"IOMEGA_SENSE {cdb[2]:x2} {len:x2}\n");
                        scsi_zip_illegal(data);
                        bus_state = BUS_CD | BUS_IO;
                }
                break;

        case SCSI_REZERO_UNIT:
                bus_state = BUS_CD | BUS_IO;
                break;

        case SCSI_REQUEST_SENSE:
                desc = cdb[1] & 1;
                len = cdb[4];

                if (desc == 0)
                {
                        add_data_len(data, ref i, len, 0x70);
                        add_data_len(data, ref i, len, 0);
                        add_data_len(data, ref i, len, data.sense_key); /*Sense key*/
                        add_data_len(data, ref i, len, 0);               /*Information (4 bytes)*/
                        add_data_len(data, ref i, len, 0);
                        add_data_len(data, ref i, len, 0);
                        add_data_len(data, ref i, len, 0);
                        add_data_len(data, ref i, len, 0); /*Additional sense length*/
                        add_data_len(data, ref i, len, 0); /*Command specific information (4 bytes)*/
                        add_data_len(data, ref i, len, 0);
                        add_data_len(data, ref i, len, 0);
                        add_data_len(data, ref i, len, 0);
                        add_data_len(data, ref i, len, data.asc);  /*ASC*/
                        add_data_len(data, ref i, len, data.ascq); /*ASCQ*/
                        add_data_len(data, ref i, len, 0);          /*FRU code*/
                        add_data_len(data, ref i, len, 0);          /*Sense key specific (3 bytes)*/
                        add_data_len(data, ref i, len, 0);
                        add_data_len(data, ref i, len, 0);
                }
                else
                {
                        add_data_len(data, ref i, len, 0x72);
                        add_data_len(data, ref i, len, data.sense_key); /*Sense Key*/
                        add_data_len(data, ref i, len, data.asc);       /*ASC*/
                        add_data_len(data, ref i, len, data.ascq);      /*ASCQ*/
                        add_data_len(data, ref i, len, 0);
                        add_data_len(data, ref i, len, 0);
                        add_data_len(data, ref i, len, 0);
                        add_data_len(data, ref i, len, 0); /*additional sense length*/
                }

                data.sense_key = data.asc = data.ascq = 0;

                data.cmd_pos = CMD_POS_IDLE;
                bus_state = BUS_IO;
                break;

        case SCSI_INQUIRY:
                len = cdb[4] | (cdb[3] << 8);

                if ((cdb[1] & 0xe0) != 0)
                {
                        add_data_len(data, ref i, len, 0 | (3 << 5)); /*No physical device on this LUN*/
                }
                else
                {
                        add_data_len(data, ref i, len, 0 | (0 << 5)); /*Hard disc*/
                }
                add_data_len(data, ref i, len, 0x80); /*Removeable device*/
                if (data.is_atapi != 0)
                {
                        add_data_len(data, ref i, len, 0);    /*Not ANSI compliant*/
                        add_data_len(data, ref i, len, 0x21); /*ATAPI compliant*/
                }
                else
                {
                        add_data_len(data, ref i, len, 2); /*SCSI-2 compliant*/
                        add_data_len(data, ref i, len, 0x02);
                }
                add_data_len(data, ref i, len, 0); /*Additional length*/
                add_data_len(data, ref i, len, 0);
                add_data_len(data, ref i, len, 0);
                add_data_len(data, ref i, len, 2);

                add_data_len(data, ref i, len, 'I');
                add_data_len(data, ref i, len, 'O');
                add_data_len(data, ref i, len, 'M');
                add_data_len(data, ref i, len, 'E');
                add_data_len(data, ref i, len, 'G');
                add_data_len(data, ref i, len, 'A');
                add_data_len(data, ref i, len, ' ');
                add_data_len(data, ref i, len, ' ');

                add_data_len(data, ref i, len, 'Z');
                add_data_len(data, ref i, len, 'I');
                add_data_len(data, ref i, len, 'P');
                add_data_len(data, ref i, len, ' ');
                add_data_len(data, ref i, len, '1');
                add_data_len(data, ref i, len, '0');
                add_data_len(data, ref i, len, '0');
                add_data_len(data, ref i, len, ' ');

                add_data_len(data, ref i, len, ' ');
                add_data_len(data, ref i, len, ' ');
                add_data_len(data, ref i, len, ' ');
                add_data_len(data, ref i, len, ' ');
                add_data_len(data, ref i, len, ' ');
                add_data_len(data, ref i, len, ' ');
                add_data_len(data, ref i, len, ' ');
                add_data_len(data, ref i, len, ' ');

                add_data_len(data, ref i, len, 'E'); /*Product revision level*/
                add_data_len(data, ref i, len, '.');
                add_data_len(data, ref i, len, '0');
                add_data_len(data, ref i, len, '8');

                add_data_len(data, ref i, len, 0); /*Drive serial number*/
                add_data_len(data, ref i, len, 0);
                add_data_len(data, ref i, len, 0);
                add_data_len(data, ref i, len, 0);
                add_data_len(data, ref i, len, 0);
                add_data_len(data, ref i, len, 0);
                add_data_len(data, ref i, len, 0);
                add_data_len(data, ref i, len, 0);

                add_data_len(data, ref i, len, 0); /*Vendor unique*/
                add_data_len(data, ref i, len, 0);
                add_data_len(data, ref i, len, 0);
                add_data_len(data, ref i, len, 0);
                add_data_len(data, ref i, len, 0);
                add_data_len(data, ref i, len, 0);
                add_data_len(data, ref i, len, 0);
                add_data_len(data, ref i, len, 0);
                add_data_len(data, ref i, len, 0);
                add_data_len(data, ref i, len, 0);
                add_data_len(data, ref i, len, 0);
                add_data_len(data, ref i, len, 0);

                add_data_len(data, ref i, len, 0);
                add_data_len(data, ref i, len, 0);

                add_data_len(data, ref i, len, 0); /*Vendor descriptor*/
                add_data_len(data, ref i, len, 0);
                add_data_len(data, ref i, len, 0);
                add_data_len(data, ref i, len, 0);
                add_data_len(data, ref i, len, 0);
                add_data_len(data, ref i, len, 0);
                add_data_len(data, ref i, len, 0);
                add_data_len(data, ref i, len, 0);
                add_data_len(data, ref i, len, 0);
                add_data_len(data, ref i, len, 0);
                add_data_len(data, ref i, len, 0);
                add_data_len(data, ref i, len, 0);
                add_data_len(data, ref i, len, 0);
                add_data_len(data, ref i, len, 0);
                add_data_len(data, ref i, len, 0);
                add_data_len(data, ref i, len, 0);

                add_data_len(data, ref i, len, 0); /*Reserved*/
                add_data_len(data, ref i, len, 0);
                add_data_len(data, ref i, len, 0);
                add_data_len(data, ref i, len, 0);
                add_data_len(data, ref i, len, 0);
                add_data_len(data, ref i, len, 0);
                add_data_len(data, ref i, len, 0);
                add_data_len(data, ref i, len, 0);
                add_data_len(data, ref i, len, 0);
                add_data_len(data, ref i, len, 0);
                add_data_len(data, ref i, len, 0);
                add_data_len(data, ref i, len, 0);
                add_data_len(data, ref i, len, 0);
                add_data_len(data, ref i, len, 0);
                add_data_len(data, ref i, len, 0);
                add_data_len(data, ref i, len, 0);
                add_data_len(data, ref i, len, 0);
                add_data_len(data, ref i, len, 0);
                add_data_len(data, ref i, len, 0);
                add_data_len(data, ref i, len, 0);
                add_data_len(data, ref i, len, 0);
                add_data_len(data, ref i, len, 0);

                data.cmd_pos = CMD_POS_IDLE;
                bus_state = BUS_IO;
                break;

        case SCSI_PREVENT_ALLOW_MEDIUM_REMOVAL:
                bus_state = BUS_CD | BUS_IO;
                break;

        case SCSI_MODE_SENSE_6:
        case SCSI_MODE_SENSE_10:
                pclog($"MODE_SENSE_6 {cdb[2]:x2} {cdb[4]:x2}\n");
                if ((cdb[2] & 0x3f) != 0x01 && (cdb[2] & 0x3f) != 0x02 && (cdb[2] & 0x3f) != 0x2f && (cdb[2] & 0x3f) != 0x3f)
                {
                        pclog("Invalid MODE_SENSE\n");
                        scsi_zip_cmd_error(data, KEY_ILLEGAL_REQ, ASC_INV_FIELD_IN_CMD_PACKET, 0);
                        bus_state = BUS_CD | BUS_IO;
                        break;
                }
                if (cdb[0] == SCSI_MODE_SENSE_6)
                {
                        len = cdb[4];

                        add_data_len(data, ref i, len, 0);
                        add_data_len(data, ref i, len, 0);

                        if (data.read_only != 0)
                                add_data_len(data, ref i, len, 0x80);
                        else
                                add_data_len(data, ref i, len, 0);
                        add_data_len(data, ref i, len, 0x08);
                }
                else
                {
                        len = cdb[8] | (cdb[7] << 8);

                        add_data_len(data, ref i, len, 0);
                        add_data_len(data, ref i, len, 0);
                        add_data_len(data, ref i, len, 0);

                        if (data.read_only != 0)
                                add_data_len(data, ref i, len, 0x80);
                        else
                                add_data_len(data, ref i, len, 0);

                        add_data_len(data, ref i, len, 0);
                        add_data_len(data, ref i, len, 0);

                        add_data_len(data, ref i, len, 0);
                        add_data_len(data, ref i, len, 0x08);
                }

                add_data_len(data, ref i, len, (ZIP_SECTORS >> 24) & 0xff);
                add_data_len(data, ref i, len, (ZIP_SECTORS >> 16) & 0xff);
                add_data_len(data, ref i, len, (ZIP_SECTORS >> 8) & 0xff);
                add_data_len(data, ref i, len, ZIP_SECTORS & 0xff);
                add_data_len(data, ref i, len, (512 >> 24) & 0xff);
                add_data_len(data, ref i, len, (512 >> 16) & 0xff);
                add_data_len(data, ref i, len, (512 >> 8) & 0xff);
                add_data_len(data, ref i, len, 512 & 0xff);

                if ((cdb[2] & 0x3f) == 0x01 || (cdb[2] & 0x3f) == 0x3f)
                {
                        add_data_len(data, ref i, len, 0x01);
                        add_data_len(data, ref i, len, 0x0a);
                        add_data_len(data, ref i, len, 0xc8); /*Automatic read and write allocation enable, most expedient error recovery*/
                        add_data_len(data, ref i, len, 22);   /*Read retry count = 22*/
                        add_data_len(data, ref i, len, 0);
                        add_data_len(data, ref i, len, 0);
                        add_data_len(data, ref i, len, 0);
                        add_data_len(data, ref i, len, 0);
                        add_data_len(data, ref i, len, 90); /*Write retry count = 90*/
                        add_data_len(data, ref i, len, 0);
                        add_data_len(data, ref i, len, 20512 >> 8);
                        add_data_len(data, ref i, len, 20512 & 0xff);
                }

                if ((cdb[2] & 0x3f) == 0x02 || (cdb[2] & 0x3f) == 0x3f)
                {
                        add_data_len(data, ref i, len, 0x02);
                        add_data_len(data, ref i, len, 0x0e);
                        add_data_len(data, ref i, len, 0);
                        add_data_len(data, ref i, len, 0);
                        add_data_len(data, ref i, len, 0);
                        add_data_len(data, ref i, len, 0);
                        add_data_len(data, ref i, len, 0);
                        add_data_len(data, ref i, len, 0);
                        add_data_len(data, ref i, len, 0);
                        add_data_len(data, ref i, len, 0);
                        add_data_len(data, ref i, len, 0);
                        add_data_len(data, ref i, len, 0);
                        add_data_len(data, ref i, len, 0);
                        add_data_len(data, ref i, len, 0);
                        add_data_len(data, ref i, len, 0);
                        add_data_len(data, ref i, len, 0);
                }

                if ((cdb[2] & 0x3f) == 0x2f || (cdb[2] & 0x3f) == 0x3f)
                {
                        add_data_len(data, ref i, len, 0x2f);
                        add_data_len(data, ref i, len, 0x04);
                        add_data_len(data, ref i, len, 0x5c);
                        add_data_len(data, ref i, len, 0x0f);
                        add_data_len(data, ref i, len, 0xff);
                        add_data_len(data, ref i, len, 0x0f);
                }

                while (i < len)
                        add_data_len(data, ref i, len, 0);

                len = data.data_pos_write;
                if (cdb[0] == SCSI_MODE_SENSE_6)
                {
                        data.data_in[0] = (uint8_t)(len - 1);
                }
                else
                {
                        data.data_in[0] = (uint8_t)((len - 2) >> 8);
                        data.data_in[1] = (uint8_t)((len - 2) & 255);
                }

                data.cmd_pos = CMD_POS_IDLE;
                bus_state = BUS_IO;
                break;

        case SCSI_READ_CAPACITY_10:
                // pcem bug, reproduced: PB-126 — READ CAPACITY rend le nombre de blocs (196 608), non le dernier
                //   LBA (196 607).
                scsi_add_data((ZIP_SECTORS >> 24) & 0xff, data);
                scsi_add_data((ZIP_SECTORS >> 16) & 0xff, data);
                scsi_add_data((ZIP_SECTORS >> 8) & 0xff, data);
                scsi_add_data(ZIP_SECTORS & 0xff, data);
                scsi_add_data((512 >> 24) & 0xff, data);
                scsi_add_data((512 >> 16) & 0xff, data);
                scsi_add_data((512 >> 8) & 0xff, data);
                scsi_add_data(512 & 0xff, data);
                data.cmd_pos = CMD_POS_IDLE;
                bus_state = BUS_IO;
                break;

        case SCSI_READ_6:
                if (data.cmd_pos == CMD_POS_WAIT)
                {
                        bus_state = BUS_CD;
                        break;
                }

                if (data.cmd_pos == CMD_POS_IDLE)
                {
                        pc.readflash_set(pc.READFLASH_HDC, data.hd_id);
                        data.addr = cdb[3] | (cdb[2] << 8) | ((cdb[1] & 0x1f) << 16);
                        data.len = cdb[4];
                        if (data.len == 0)
                                data.len = 256;

                        data.cmd_pos = CMD_POS_WAIT;
                        timer_set_delay_u64(data.callback_timer, RW_DELAY);
                        data.new_cmd_pos = CMD_POS_START_SECTOR;
                        data.sector_pos = 0;

                        bus_state = BUS_CD;
                        atapi_set_transfer_granularity(data.atapi_dev!, 512);
                        break;
                }
                while (data.len != 0)
                {
                        if (data.cmd_pos == CMD_POS_START_SECTOR)
                        {
                                // pcem bug, reproduced: PB-126 — au secteur 196 608, hdd_read_sectors ne lit rien
                                //   (hdd_file.c:174-180) et buf garde le secteur précédent.
                                hdd_file.hdd_read_sectors(data.hdd, data.addr, 1, data.buf);
                                pc.readflash_set(pc.READFLASH_HDC, data.hd_id);
                        }
                        data.cmd_pos = CMD_POS_TRANSFER;
                        for (; data.sector_pos < 512; data.sector_pos++)
                        {
                                int ret = scsi_add_data(data.buf[data.sector_pos], data);

                                if (ret == -1)
                                {
                                        pc.fatal("scsi_add_data -1\n");
                                        break;
                                }
                                if ((ret & 0x100) != 0)
                                {
                                        pc.fatal("scsi_add_data 0x100\n");
                                        data.len = 1;
                                        break;
                                }
                        }
                        data.cmd_pos = CMD_POS_START_SECTOR;

                        data.sector_pos = 0;
                        data.len--;
                        data.addr++;
                }
                data.cmd_pos = CMD_POS_IDLE;
                bus_state = BUS_IO;
                break;

        case SCSI_READ_10:
                if (data.cmd_pos == CMD_POS_WAIT)
                {
                        bus_state = BUS_CD;
                        break;
                }

                if (data.cmd_pos == CMD_POS_IDLE)
                {
                        pc.readflash_set(pc.READFLASH_HDC, data.hd_id);
                        data.addr = cdb[5] | (cdb[4] << 8) | (cdb[3] << 16) | (cdb[2] << 24);
                        data.len = cdb[8] | (cdb[7] << 8);

                        data.cmd_pos = CMD_POS_WAIT;
                        timer_set_delay_u64(data.callback_timer, RW_DELAY);
                        data.new_cmd_pos = CMD_POS_START_SECTOR;
                        data.sector_pos = 0;

                        bus_state = BUS_CD;
                        atapi_set_transfer_granularity(data.atapi_dev!, 512);
                        break;
                }
                while (data.len != 0)
                {
                        if (data.cmd_pos == CMD_POS_START_SECTOR)
                        {
                                // pcem bug, reproduced: PB-126 — au secteur 196 608, hdd_read_sectors ne lit rien
                                //   (hdd_file.c:174-180) et buf garde le secteur précédent.
                                hdd_file.hdd_read_sectors(data.hdd, data.addr, 1, data.buf);
                                pc.readflash_set(pc.READFLASH_HDC, data.hd_id);
                        }
                        data.cmd_pos = CMD_POS_TRANSFER;
                        for (; data.sector_pos < 512; data.sector_pos++)
                        {
                                int ret = scsi_add_data(data.buf[data.sector_pos], data);

                                if (ret == -1)
                                {
                                        pc.fatal("scsi_add_data -1\n");
                                        break;
                                }
                                if ((ret & 0x100) != 0)
                                {
                                        pc.fatal("scsi_add_data 0x100\n");
                                        data.len = 1;
                                        break;
                                }
                        }
                        data.cmd_pos = CMD_POS_START_SECTOR;

                        data.sector_pos = 0;
                        data.len--;
                        data.addr++;
                }
                data.cmd_pos = CMD_POS_IDLE;
                bus_state = BUS_IO;
                break;

        case SCSI_WRITE_6:
                if (data.read_only != 0)
                {
                        pclog("zip read only\n");
                        scsi_zip_cmd_error(data, KEY_DATA_PROTECT, ASC_WRITE_PROTECT, 0);
                        bus_state = BUS_CD | BUS_IO;
                        break;
                }
                if (data.cmd_pos == CMD_POS_WAIT)
                {
                        bus_state = BUS_CD;
                        break;
                }
                if (data.cmd_pos == CMD_POS_IDLE)
                {
                        data.addr = cdb[3] | (cdb[2] << 8) | ((cdb[1] & 0x1f) << 16);
                        data.len = cdb[4];
                        if (data.len == 0)
                                data.len = 256;
                        pc.readflash_set(pc.READFLASH_HDC, data.hd_id);
                        data.bytes_required = data.len * 512;

                        data.cmd_pos = CMD_POS_WAIT;
                        timer_set_delay_u64(data.callback_timer, RW_DELAY);
                        data.new_cmd_pos = CMD_POS_TRANSFER;
                        data.sector_pos = 0;

                        bus_state = BUS_CD;
                        atapi_set_transfer_granularity(data.atapi_dev!, 512);
                        break;
                }
                if (data.cmd_pos == CMD_POS_TRANSFER && data.bytes_received != data.bytes_required)
                {
                        bus_state = 0;
                        break;
                }

                while (data.len != 0)
                {
                        for (; data.sector_pos < 512; data.sector_pos++)
                        {
                                int ret = scsi_get_data(data);

                                if (ret == -1)
                                {
                                        pc.fatal("scsi_get_data -1\n");
                                        break;
                                }
                                data.buf[data.sector_pos] = (uint8_t)(ret & 0xff);
                                if ((ret & 0x100) != 0)
                                {
                                        pc.fatal("scsi_get_data 0x100\n");
                                        data.len = 1;
                                        break;
                                }
                        }

                        hdd_file.hdd_write_sectors(data.hdd, data.addr, 1, data.buf);
                        pc.readflash_set(pc.READFLASH_HDC, data.hd_id);

                        data.sector_pos = 0;
                        data.len--;
                        data.addr++;
                }
                data.cmd_pos = CMD_POS_IDLE;
                bus_state = BUS_CD | BUS_IO;
                break;

        case SCSI_WRITE_10:
        case SCSI_WRITE_AND_VERIFY:
                if (data.read_only != 0)
                {
                        pclog("zip read only\n");
                        scsi_zip_cmd_error(data, KEY_DATA_PROTECT, ASC_WRITE_PROTECT, 0);
                        bus_state = BUS_CD | BUS_IO;
                        break;
                }
                if (data.cmd_pos == CMD_POS_WAIT)
                {
                        bus_state = BUS_CD;
                        break;
                }
                if (data.cmd_pos == CMD_POS_IDLE)
                {
                        data.addr = cdb[5] | (cdb[4] << 8) | (cdb[3] << 16) | (cdb[2] << 24);
                        data.len = cdb[8] | (cdb[7] << 8);
                        pc.readflash_set(pc.READFLASH_HDC, data.hd_id);
                        data.bytes_required = data.len * 512;

                        data.cmd_pos = CMD_POS_WAIT;
                        timer_set_delay_u64(data.callback_timer, RW_DELAY);
                        data.new_cmd_pos = CMD_POS_TRANSFER;
                        data.sector_pos = 0;

                        bus_state = BUS_CD;
                        atapi_set_transfer_granularity(data.atapi_dev!, 512);
                        break;
                }
                if (data.cmd_pos == CMD_POS_TRANSFER && data.bytes_received != data.bytes_required)
                {
                        bus_state = 0;
                        break;
                }

                while (data.len != 0)
                {
                        for (; data.sector_pos < 512; data.sector_pos++)
                        {
                                int ret = scsi_get_data(data);

                                if (ret == -1)
                                {
                                        pc.fatal("scsi_get_data -1\n");
                                        break;
                                }
                                data.buf[data.sector_pos] = (uint8_t)(ret & 0xff);
                                if ((ret & 0x100) != 0)
                                {
                                        pc.fatal("scsi_get_data 0x100\n");
                                        data.len = 1;
                                        break;
                                }
                        }

                        hdd_file.hdd_write_sectors(data.hdd, data.addr, 1, data.buf);
                        pc.readflash_set(pc.READFLASH_HDC, data.hd_id);

                        data.sector_pos = 0;
                        data.len--;
                        data.addr++;
                }
                data.cmd_pos = CMD_POS_IDLE;
                bus_state = BUS_CD | BUS_IO;
                break;

        case SCSI_VERIFY_10:
                data.cmd_pos = CMD_POS_IDLE;
                bus_state = BUS_CD | BUS_IO;
                break;

        case SCSI_MODE_SELECT_6:
                // pcem bug, reproduced: PB-126 — de longueur 0, la phase de données sortante ne finit jamais :
                //   write_complete n'est lu qu'après un octet reçu (scsi.c:157-161), et 1 ≠ 0.
                if (data.bytes_received == 0)
                {
                        data.bytes_required = cdb[4];
                        bus_state = 0;
                        break;
                }
                data.cmd_pos = CMD_POS_IDLE;
                bus_state = BUS_CD | BUS_IO;
                break;

        case SCSI_FORMAT:
                if (data.read_only != 0)
                {
                        scsi_zip_cmd_error(data, KEY_DATA_PROTECT, ASC_WRITE_PROTECT, 0);
                        bus_state = BUS_CD | BUS_IO;
                        break;
                }
                pc.readflash_set(pc.READFLASH_HDC, 0);
                hdd_file.hdd_format_sectors(data.hdd, 0, ZIP_SECTORS);

                data.cmd_pos = CMD_POS_IDLE;
                bus_state = BUS_CD | BUS_IO;
                break;

        case SCSI_START_STOP_UNIT:
                /*if ((cdb[4] & (START_STOP_LOEJ | START_STOP_START)) == START_STOP_LOEJ)*/
                /*This is incorrect based on the SCSI spec, but it's what the Iomega Win9x drivers send to eject a disc*/
                // pcem bug, reproduced: PB-126 — START STOP UNIT éjecte sur START = 0 et LOEJ = 0 (arrêter le moteur),
                //   et non sur LOEJ = 1 (éjecter).
                if ((cdb[4] & (START_STOP_LOEJ | START_STOP_START)) == 0)
                        zip_eject();
                bus_state = BUS_CD | BUS_IO;
                break;

                /*These aren't really meaningful in a single initiator system, so just return*/
        case SCSI_RESERVE:
        case SCSI_RELEASE:
                bus_state = BUS_CD | BUS_IO;
                break;

        case SCSI_SEEK_6:
        case SCSI_SEEK_10:
                bus_state = BUS_CD | BUS_IO;
                break;

        case SEND_DIAGNOSTIC:
                if ((cdb[1] & (1 << 2)) != 0)
                {
                        /*Self-test*/
                        /*Always passes*/
                        bus_state = BUS_CD | BUS_IO;
                }
                else
                {
                        /*Treat all other diagnostic requests as illegal*/
                        scsi_zip_illegal(data);
                        bus_state = BUS_CD | BUS_IO;
                }
                break;

        case SCSI_IOMEGA_EJECT:
                if (data.is_atapi != 0)
                        zip_eject();
                else
                        scsi_zip_illegal(data);

                bus_state = BUS_CD | BUS_IO;
                break;

        case ATAPI_READ_FORMAT_CAPACITIES:
                if (data.is_atapi != 0)
                {
                        len = cdb[8] | (cdb[7] << 8);

                        /*List header*/
                        scsi_add_data(0, data);
                        scsi_add_data(0, data);
                        scsi_add_data(0, data);
                        scsi_add_data(16, data); /*list length*/

                        /*Current/Maximum capacity header*/
                        scsi_add_data((ZIP_SECTORS >> 24) & 0xff, data);
                        scsi_add_data((ZIP_SECTORS >> 16) & 0xff, data);
                        scsi_add_data((ZIP_SECTORS >> 8) & 0xff, data);
                        scsi_add_data(ZIP_SECTORS & 0xff, data);
                        if (data.disc_loaded != 0)
                                scsi_add_data(2, data); /*Formatted media - current media capacity*/
                        else
                                scsi_add_data(3, data); /*Maximum formattable capacity*/
                        scsi_add_data((512 >> 16) & 0xff, data);
                        scsi_add_data((512 >> 8) & 0xff, data);
                        scsi_add_data(512 & 0xff, data);

                        /*Formattable capacity descriptor*/
                        scsi_add_data((ZIP_SECTORS >> 24) & 0xff, data);
                        scsi_add_data((ZIP_SECTORS >> 16) & 0xff, data);
                        scsi_add_data((ZIP_SECTORS >> 8) & 0xff, data);
                        scsi_add_data(ZIP_SECTORS & 0xff, data);
                        scsi_add_data(0, data);
                        scsi_add_data((512 >> 16) & 0xff, data);
                        scsi_add_data((512 >> 8) & 0xff, data);
                        scsi_add_data(512 & 0xff, data);

                        data.cmd_pos = CMD_POS_IDLE;
                        bus_state = BUS_IO;
                }
                else
                {
                        scsi_zip_illegal(data);
                        bus_state = BUS_CD | BUS_IO;
                }
                break;

        default:
                pclog($"Bad SCSI ZIP command {cdb[0]:x2}\n");
                scsi_zip_illegal(data);
                bus_state = BUS_CD | BUS_IO;
                break;
        }

        return bus_state;
    }

    // pcem: scsi_zip.c:976-980
    private static uint8_t scsi_zip_read(object p)
    {
        scsi_zip_data data = (scsi_zip_data)p;

        // pcem bug, not reproduced: PB-125 — data_in[data_pos_read++] sans borne (:979) : une lecture de
        //   longueur nulle ne finit jamais (PB-126), et l'invité qui insiste lit hors de la structure.
        // DEVIATION: au-delà du tampon, l'octet se lit nul, la position avance.
        if (data.data_pos_read >= BUFFER_SIZE)
        {
                R9.Garde("scsi_zip.c:979");
                data.data_pos_read++;
                return 0;
        }
        return data.data_in[data.data_pos_read++];
    }

    // pcem: scsi_zip.c:982-991
    private static void scsi_zip_write(uint8_t val, object p)
    {
        scsi_zip_data data = (scsi_zip_data)p;

        // pcem bug, not reproduced: PB-125 — data_out[262 144] est écrit hors du tableau, puis
        //   fatal("Exceeded data_out buffer size\n") (:985-990) : WRITE de plus de 512 secteurs.
        // DEVIATION: au-delà du tampon, l'octet est compté mais pas gardé.
        if (data.data_pos_write >= BUFFER_SIZE)
        {
                R9.Garde("scsi_zip.c:990");
                data.data_pos_write++;
                data.bytes_received++;
                return;
        }
        data.data_out[data.data_pos_write++] = val;

        data.bytes_received++;
    }

    // pcem: scsi_zip.c:993-996
    private static int scsi_zip_read_complete(object p)
    {
        scsi_zip_data data = (scsi_zip_data)p;

        return (data.data_pos_read == data.data_pos_write) ? 1 : 0;
    }

    // pcem: scsi_zip.c:998-1002
    private static int scsi_zip_write_complete(object p)
    {
        scsi_zip_data data = (scsi_zip_data)p;

        return (data.bytes_received == data.bytes_required) ? 1 : 0;
    }

    // pcem: scsi_zip.c:1004-1010
    private static void scsi_zip_start_command(object p)
    {
        scsi_zip_data data = (scsi_zip_data)p;

        data.bytes_received = 0;
        data.bytes_required = 0;
        data.data_pos_read = data.data_pos_write = 0;
    }

    // pcem: scsi_zip.c:1012-1016
    private static uint8_t scsi_zip_get_status(object p)
    {
        scsi_zip_data data = (scsi_zip_data)p;

        return data.status;
    }

    // pcem: scsi_zip.c:1018-1022
    private static uint8_t scsi_zip_get_sense_key(object p)
    {
        scsi_zip_data data = (scsi_zip_data)p;

        return (uint8_t)data.sense_key;
    }

    // pcem: scsi_zip.c:1024-1028
    private static int scsi_zip_get_bytes_required(object p)
    {
        scsi_zip_data data = (scsi_zip_data)p;

        return data.bytes_required - data.bytes_received;
    }

    // Un mot de 16 bits dans le tampon d'octets de l'IDE, petit-boutiste (Ide/ide.cs, IDE.SetW).
    private static void setw(uint8_t[] buffer, int i, int v)
    {
        buffer[i * 2] = (uint8_t)v;
        buffer[i * 2 + 1] = (uint8_t)(v >> 8);
    }

    // pcem: scsi_zip.c:1030-1053
    private static void scsi_zip_atapi_identify(uint8_t[] buffer, object p)
    {
        scsi_zip_data data = (scsi_zip_data)p;

        Array.Clear(buffer, 0, 512);

        setw(buffer, 0,
                0x8000 | (0 << 8) | 0x80 | (2 << 5));  /* ATAPI device, direct-access device, removable media, accelerated DRQ */
        ide.ide_padstr(buffer, 10 * 2, "", 20);    /* Serial Number */
        ide.ide_padstr(buffer, 23 * 2, "E.08", 8); /* Firmware */
        ide.ide_padstr(buffer, 27 * 2, "IOMEGA ZIP 100 ATAPI", 40); /* Model */
        setw(buffer, 49, 0x300);                                            /*DMA and LBA supported*/
        setw(buffer, 51, 120);
        setw(buffer, 52, 120);
        setw(buffer, 53, 2); /*Words 64-70 are valid*/
        setw(buffer, 62, 0x0000);
        setw(buffer, 63, 0x0003 | (0x100 << data.mdma_mode)); /*Multi-word DMA 0 & 1*/
        setw(buffer, 64, 0x0001);                              /*PIO Mode 3*/
        if (data.pio_mode >= 3)
                setw(buffer, 64, 0x0001 | (0x100 << (data.pio_mode - 3)));
        setw(buffer, 65, 120);     /*Minimum multi-word cycle time*/
        setw(buffer, 66, 120);     /*Recommended multi-word cycle time*/
        setw(buffer, 67, 120);     /*Minimum PIO cycle time*/
        setw(buffer, 126, 0xfffe); /* Interpret zero byte count limit as maximum length */
    }

    // pcem: scsi_zip.c:1055-1089
    private static int scsi_zip_atapi_set_feature(uint8_t feature, uint8_t val, object p)
    {
        scsi_zip_data data = (scsi_zip_data)p;

        switch (feature)
        {
        case ide.FEATURE_SET_TRANSFER_MODE:
                if ((val & 0xfe) == 0)
                {
                        /*Default transfer mode*/
                        data.pio_mode = 0;
                        return 1;
                }
                else if ((val & 0xf8) == 0x08)
                {
                        /*PIO transfer mode*/
                        if ((val & 7) > 3)
                                return 0;
                        data.pio_mode = (val & 7);
                        return 1;
                }
                else if ((val & 0xf8) == 0x20)
                {
                        /*Multi-word DMA transfer mode*/
                        if ((val & 7) > 1)
                                return 0;
                        data.mdma_mode = (val & 7);
                        return 1;
                }
                return 0; /*Invalid data*/

        case ide.FEATURE_ENABLE_IRQ_OVERLAPPED:
        case ide.FEATURE_ENABLE_IRQ_SERVICE:
        case ide.FEATURE_DISABLE_REVERT:
        case ide.FEATURE_DISABLE_IRQ_OVERLAPPED:
        case ide.FEATURE_DISABLE_IRQ_SERVICE:
        case ide.FEATURE_ENABLE_REVERT:
                return 1;
        }

        return 0; /*Feature not supported*/
    }

    // pcem: scsi_zip.c:1091-1111
    internal static readonly scsi_device_t scsi_zip = new(scsi_zip_init, scsi_zip_atapi_init, scsi_zip_close, scsi_zip_reset,
                                                          scsi_zip_start_command, scsi_zip_command,
                                                          scsi_zip_get_status, scsi_zip_get_sense_key,
                                                          scsi_zip_get_bytes_required,
                                                          scsi_zip_atapi_identify, scsi_zip_atapi_set_feature,
                                                          scsi_zip_read, scsi_zip_write, scsi_zip_read_complete,
                                                          scsi_zip_write_complete);
}
