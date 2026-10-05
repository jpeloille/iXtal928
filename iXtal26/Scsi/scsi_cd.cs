// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/scsi/scsi_cd.c  (scsi_cd.h : includes/private/scsi/scsi_cd.h)
// STATUS: deviated — G10.4 (PLAN-G10.md) : le lecteur de CD-ROM de PCem, entier — les douze modèles et
//         les dix-huit vitesses, le minutage des recherches et des lectures, les commandes, MODE SENSE et
//         MODE SELECT, IDENTIFY PACKET et SET FEATURES, la table scsi_cd. Branché sur le pont ATAPI ;
//         scsi_cd_init n'est appelé seul que par le bus des cartes SCSI (G11).
//
// DEVIATIONS (R9, PB-115) : MECHANISM STATUS de longueur 0 (:993) ; data_out débordé (:1576-1581) ; la
//   lecture au-delà de data_in (:1565) ; le compte négatif passé à readsector (:1187).
// Reproduits : PB-117 (READ CAPACITY), PB-118 (GET EVENT STATUS NOTIFICATION), PB-119 (MODE SELECT),
//   PB-120 (la TOC brute).

// CS8981 : `scsi_cd_c` porte le nom de l'unité C, que le membre `scsi_cd` garde (TRANSCRIPTION.md).
#pragma warning disable CS8981

using iXtal26.Cdrom;
using iXtal26.Diag;
using iXtal26.Ide;
using static iXtal26.Ide.ide_atapi;
using static iXtal26.Scsi.scsi;
using static iXtal26.timer;

namespace iXtal26.Scsi;

// pcem: scsi_cd.c:147-194 — classe : le bus et le pont en gardent l'adresse (device_data, timer_add).
internal sealed class scsi_cd_data_t
{
    internal uint8_t[] data_in = new uint8_t[scsi_cd_c.BUFFER_SIZE];
    internal uint8_t[] data_out = new uint8_t[scsi_cd_c.BUFFER_SIZE];

    internal int blocks;

    internal int cmd_pos, new_cmd_pos;
    internal pc_timer_t callback_timer = new();
    internal int wait_time;
    internal scsi_bus_t? bus;

    internal int addr, len;
    internal int sector_pos;

    internal uint8_t[] buf = new uint8_t[512];

    internal uint8_t status;

    internal int data_pos_read, data_pos_write;
    internal int data_bytes_read, bytes_expected;

    internal int cdlen, cdpos;

    internal int bytes_received, bytes_required;

    internal int sense_key, asc, ascq;

    internal int id;

    internal int received_cdb;
    internal uint8_t[] cdb = new uint8_t[CDB_MAX_LEN];

    internal int prev_status;
    internal int cd_status;

    internal uint8_t prefix_len;
    internal uint8_t page_current;

    internal int is_atapi;
    internal atapi_device_t? atapi_dev;

    internal int pio_mode, mdma_mode;

    internal int short_seek, long_seek;
    internal int max_speed, cur_speed;

    internal int cur_model;
}

// pcem: scsi_cd.c:200-204 — struct anonyme ; recopiée par valeur seulement.
internal struct cd_speed_t
{
    internal int speed;
    internal int short_seek;
    internal int long_seek;

    internal cd_speed_t(int speed, int short_seek, int long_seek)
    {
        this.speed = speed;
        this.short_seek = short_seek;
        this.long_seek = long_seek;
    }
}

// pcem: scsi_cd.c:234-251 — struct anonyme.
internal sealed class cd_model_t
{
    // suffix numbers are sizes from the specs
    internal readonly string vendor_8;
    internal readonly string model_and_firmware_40;
    internal readonly string serial_20;
    internal readonly string model_16;
    internal readonly string firmware_4;

    internal readonly string serial2_20;
    internal readonly string firmware2_8;
    internal readonly string model2_40;

    // for PCem config only
    internal readonly string model_string_40;
    internal readonly string model_config_string_40;
    internal readonly int interfaces;
    internal readonly int speed; // Not an index, but a "speed" value. -1 == allow override

    internal cd_model_t(string vendor_8, string model_and_firmware_40, string serial_20, string model_16, string firmware_4,
                        string serial2_20, string firmware2_8, string model2_40, string model_string_40,
                        string model_config_string_40, int interfaces, int speed)
    {
        this.vendor_8 = vendor_8;
        this.model_and_firmware_40 = model_and_firmware_40;
        this.serial_20 = serial_20;
        this.model_16 = model_16;
        this.firmware_4 = firmware_4;
        this.serial2_20 = serial2_20;
        this.firmware2_8 = firmware2_8;
        this.model2_40 = model2_40;
        this.model_string_40 = model_string_40;
        this.model_config_string_40 = model_config_string_40;
        this.interfaces = interfaces;
        this.speed = speed;
    }
}

internal static class scsi_cd_c
{
    // pcem: scsi_cd.c:12
    private static int MIN(int a, int b) => a < b ? a : b;

    // pcem: scsi_cd.c:14-16
    internal const int BUFFER_SIZE = 256 * 1024;

    private const int MAX_NR_SECTORS = 16;

    // pcem: scsi_cd.c:18
    private const int CMD_POS_IDLE = 0, CMD_POS_WAIT = 1, CMD_POS_START_SECTOR = 2, CMD_POS_TRANSFER = 3, CMD_POS_COMPLETE = 4;

    /* ATAPI Commands */
    // pcem: scsi_cd.c:21-48
    private const uint8_t GPCMD_TEST_UNIT_READY = 0x00;
    private const uint8_t GPCMD_REQUEST_SENSE = 0x03;
    private const uint8_t GPCMD_READ_6 = 0x08;
    private const uint8_t GPCMD_INQUIRY = 0x12;
    private const uint8_t GPCMD_MODE_SELECT_6 = 0x15;
    private const uint8_t GPCMD_MODE_SENSE_6 = 0x1a;
    private const uint8_t GPCMD_START_STOP_UNIT = 0x1b;
    private const uint8_t GPCMD_PREVENT_REMOVAL = 0x1e;
    private const uint8_t GPCMD_READ_CDROM_CAPACITY = 0x25;
    private const uint8_t GPCMD_READ_10 = 0x28;
    private const uint8_t GPCMD_SEEK = 0x2b;
    private const uint8_t GPCMD_READ_SUBCHANNEL = 0x42;
    private const uint8_t GPCMD_READ_TOC_PMA_ATIP = 0x43;
    private const uint8_t GPCMD_READ_HEADER = 0x44;
    private const uint8_t GPCMD_PLAY_AUDIO_10 = 0x45;
    private const uint8_t GPCMD_PLAY_AUDIO_MSF = 0x47;
    private const uint8_t GPCMD_GET_EVENT_STATUS_NOTIFICATION = 0x4a;
    private const uint8_t GPCMD_PAUSE_RESUME = 0x4b;
    private const uint8_t GPCMD_STOP_PLAY_SCAN = 0x4e;
    private const uint8_t GPCMD_READ_DISC_INFORMATION = 0x51;
    private const uint8_t GPCMD_MODE_SELECT_10 = 0x55;
    private const uint8_t GPCMD_MODE_SENSE_10 = 0x5a;
    private const uint8_t GPCMD_PLAY_AUDIO_12 = 0xa5;
    private const uint8_t GPCMD_READ_12 = 0xa8;
    private const uint8_t GPCMD_SEND_DVD_STRUCTURE = 0xad;
    private const uint8_t GPCMD_SET_SPEED = 0xbb;
    private const uint8_t GPCMD_MECHANISM_STATUS = 0xbd;
    private const uint8_t GPCMD_READ_CD = 0xbe;

    /* Mode page codes for mode sense/set */
    // pcem: scsi_cd.c:51-55
    private const int GPMODE_R_W_ERROR_PAGE = 0x01;
    private const int GPMODE_CDROM_PAGE = 0x0d;
    private const int GPMODE_CDROM_AUDIO_PAGE = 0x0e;
    private const int GPMODE_CAPABILITIES_PAGE = 0x2a;
    private const int GPMODE_ALL_PAGES = 0x3f;

    /* ATAPI Sense Keys */
    // pcem: scsi_cd.c:58-61
    private const int SENSE_NONE = 0;
    private const int SENSE_NOT_READY = 2;
    private const int SENSE_ILLEGAL_REQUEST = 5;
    private const int SENSE_UNIT_ATTENTION = 6;

    // pcem: scsi_cd.c:63-65
    private const int ASCQ_AUDIO_PLAY_OPERATION_IN_PROGRESS = 0x11;
    private const int ASCQ_AUDIO_PLAY_OPERATION_PAUSED = 0x12;
    private const int ASCQ_AUDIO_PLAY_OPERATION_COMPLETED = 0x13;

    /* Tell RISC OS that we have a 4x CD-ROM drive (600kb/sec data, 706kb/sec raw).
       Not that it means anything */
    // pcem: scsi_cd.c:69
    private const int CDROM_SPEED = 706;

    /* Event notification classes for GET EVENT STATUS NOTIFICATION */
    // pcem: scsi_cd.c:72-78
    private const int GESN_NO_EVENTS = 0;
    private const int GESN_OPERATIONAL_CHANGE = 1;
    private const int GESN_POWER_MANAGEMENT = 2;
    private const int GESN_EXTERNAL_REQUEST = 3;
    private const int GESN_MEDIA = 4;
    private const int GESN_MULTIPLE_HOSTS = 5;
    private const int GESN_DEVICE_BUSY = 6;

    /* Event codes for MEDIA event status notification */
    // pcem: scsi_cd.c:81-89
    private const uint8_t MEC_NO_CHANGE = 0;
    private const uint8_t MEC_EJECT_REQUESTED = 1;
    private const uint8_t MEC_NEW_MEDIA = 2;
    private const uint8_t MEC_MEDIA_REMOVAL = 3;       /* only for media changers */
    private const uint8_t MEC_MEDIA_CHANGED = 4;       /* only for media changers */
    private const uint8_t MEC_BG_FORMAT_COMPLETED = 5; /* MRW or DVD+RW b/g format completed */
    private const uint8_t MEC_BG_FORMAT_RESTARTED = 6; /* MRW or DVD+RW b/g format restarted */
    private const uint8_t MS_TRAY_OPEN = 1;
    private const uint8_t MS_MEDIA_PRESENT = 2;

    // pcem: scsi_cd.c:91-92
    private const int CHECK_READY = 2;
    private const int ALLOW_UA = 1;

    /* Table of all ATAPI commands and their flags, needed for the new disc change / not ready handler. */
    // pcem: scsi_cd.c:95-125
    private static readonly uint8_t[] atapi_cmd_table = atapi_cmd_table_init();

    private static uint8_t[] atapi_cmd_table_init()
    {
        uint8_t[] t = new uint8_t[0x100];

        t[GPCMD_TEST_UNIT_READY] = CHECK_READY;
        t[GPCMD_REQUEST_SENSE] = ALLOW_UA;
        t[GPCMD_READ_6] = CHECK_READY;
        t[GPCMD_INQUIRY] = ALLOW_UA;
        t[GPCMD_MODE_SELECT_6] = 0;
        t[GPCMD_MODE_SENSE_6] = 0;
        t[GPCMD_START_STOP_UNIT] = 0;
        t[GPCMD_PREVENT_REMOVAL] = CHECK_READY;
        t[GPCMD_READ_CDROM_CAPACITY] = CHECK_READY;
        t[GPCMD_READ_10] = CHECK_READY;
        t[GPCMD_SEEK] = CHECK_READY;
        t[GPCMD_READ_SUBCHANNEL] = CHECK_READY;
        t[GPCMD_READ_TOC_PMA_ATIP] = CHECK_READY | ALLOW_UA; /* Read TOC - can get through UNIT_ATTENTION, per VIDE-CDD.SYS */
        t[GPCMD_READ_HEADER] = CHECK_READY;
        t[GPCMD_PLAY_AUDIO_10] = CHECK_READY;
        t[GPCMD_PLAY_AUDIO_MSF] = CHECK_READY;
        t[GPCMD_GET_EVENT_STATUS_NOTIFICATION] = ALLOW_UA;
        t[GPCMD_PAUSE_RESUME] = CHECK_READY;
        t[GPCMD_STOP_PLAY_SCAN] = CHECK_READY;
        t[GPCMD_READ_DISC_INFORMATION] = CHECK_READY;
        t[GPCMD_MODE_SELECT_10] = 0;
        t[GPCMD_MODE_SENSE_10] = 0;
        t[GPCMD_PLAY_AUDIO_12] = CHECK_READY;
        t[GPCMD_READ_12] = CHECK_READY;
        t[GPCMD_SEND_DVD_STRUCTURE] = CHECK_READY; /* Read DVD structure (NOT IMPLEMENTED YET) */
        t[GPCMD_SET_SPEED] = 0;
        t[GPCMD_MECHANISM_STATUS] = 0;
        t[GPCMD_READ_CD] = CHECK_READY;
        t[0xBF] = CHECK_READY; /* Send DVD structure (NOT IMPLEMENTED YET) */
        return t;
    }

    // pcem: scsi_cd.c:127-133
    private const int IMPLEMENTED = 1;

    private static readonly uint8_t[] mode_sense_pages = mode_sense_pages_init();

    private static uint8_t[] mode_sense_pages_init()
    {
        uint8_t[] t = new uint8_t[0x40];

        t[GPMODE_R_W_ERROR_PAGE] = IMPLEMENTED;
        t[GPMODE_CDROM_PAGE] = IMPLEMENTED;
        t[GPMODE_CDROM_AUDIO_PAGE] = IMPLEMENTED;
        t[GPMODE_CAPABILITIES_PAGE] = IMPLEMENTED;
        t[GPMODE_ALL_PAGES] = IMPLEMENTED;
        return t;
    }

    // pcem: scsi_cd.c:135-143 — globales : scsi_cd_init n'efface que la page 0Eh, la seule modifiable.
    internal static readonly uint8_t[][] mode_pages_in = mode_pages_in_init();
    private const uint8_t PAGE_CHANGEABLE = 1;
    private const uint8_t PAGE_CHANGED = 2;
    internal static readonly uint8_t[] page_flags = page_flags_init();

    private static uint8_t[][] mode_pages_in_init()
    {
        uint8_t[][] t = new uint8_t[256][];

        for (int i = 0; i < 256; i++)
                t[i] = new uint8_t[256];
        return t;
    }

    private static uint8_t[] page_flags_init()
    {
        uint8_t[] t = new uint8_t[256];

        t[GPMODE_R_W_ERROR_PAGE] = 0;
        t[GPMODE_CDROM_PAGE] = 0;
        t[GPMODE_CDROM_AUDIO_PAGE] = PAGE_CHANGEABLE;
        t[GPMODE_CAPABILITIES_PAGE] = 0;
        return t;
    }

    // omitted: `extern int cd_status` (scsi_cd.c:145) — déclaré, jamais lu : data->cd_status en tient lieu.

    // pcem: scsi_cd.c:196
    internal static scsi_cd_data_t? cd_data = null;

    /*Note - all transfer speeds currently assume CAV behaviour. Drives above about 8x
      should use CLV instead, but this is not currently supported.*/
    // pcem: scsi_cd.c:200-208
    internal static readonly cd_speed_t[] cd_speeds =
    [
        new(1, 240, 1446), new(2, 160, 1000), new(3, 150, 900), new(4, 112, 675), new(6, 112, 675), new(8, 112, 675),
        new(12, 75, 400),  new(16, 58, 350),  new(20, 50, 300), new(24, 45, 270), new(32, 45, 270), new(36, 45, 270),
        new(40, 50, 300),  new(44, 50, 300),  new(48, 50, 300), new(52, 45, 270), new(56, 45, 270), new(72, 45, 270),
    ];

    // pcem: scsi_cd.h:5, :11-16
    internal const int MAX_CD_SPEED = 72;
    internal const int MAX_CD_MODEL = 12;
    internal const int CD_MODEL_INTERFACE_ALL = 0; // even when controller is not IDE and not SCSI (to always have the PCemCD model as default, even when not selectable)
    internal const int CD_MODEL_INTERFACE_IDE = 1;
    internal const int CD_MODEL_INTERFACE_SCSI = 2;

    // pcem: scsi_cd.c:210 — clé cd_speed (pc.c:780).
    // DEVIATION: 24, le défaut de la clé, et non le zéro statique du C : les points d'entrée qui ne lisent aucun
    //   .cfg ont le défaut de PCem, comme cfg_mem_size (pc.cs) ; à zéro, la première lecture diviserait par lui.
    internal static int cd_speed = 24;

    // pcem: scsi_cd.c:212
    internal static int cd_get_speed(int i) => cd_speeds[i].speed;

    // pcem: scsi_cd.c:214-232
    internal static void cd_set_speed(int speed)
    {
        if (cd_data is not null)
        {
                int c = 0;

                while (true)
                {
                        if (cd_speeds[c].speed == speed)
                                break;
                        if (cd_speeds[c].speed >= MAX_CD_SPEED)
                                break;

                        c++;
                }

                cd_data.max_speed = speed;
                cd_data.cur_speed = speed;
                cd_data.short_seek = cd_speeds[c].short_seek;
                cd_data.long_seek = cd_speeds[c].long_seek;
        }
    }

    // pcem: scsi_cd.c:234-455
    internal static readonly cd_model_t[] cd_models =
    [
        // Generic PCem CD
        new("PCem", "PCemCD v1.0", "53R141", "PCemCD", "1.0",
            "", "v1.0", "PCemCD", "PCemCD", "pcemcd", CD_MODEL_INTERFACE_ALL, -1),

        // A 4x CD-ROM drive from Aztech. Choose if your system image has the SGIDECD.SYS driver
        new("AZT", "AZT 46802I v1.15", "53R141", "46802I", "1.15",
            "", "v1.15", "CDA46802I", "AZT CDA 468-02I (4X)", "azt_cda_468_02i_4x", CD_MODEL_INTERFACE_IDE, 4),

        // A 6X CD-ROM drive from NEC.
        new("NEC", "NEC CDR-1300A 1.05", "63K3320T113", "CDR-1300A", "1.05",
            "", "v1.05", "CDR-1300A", "NEC CDR-1300A (6X)", "cdr1300a", CD_MODEL_INTERFACE_IDE, 6),

        // An 8X CD-ROM drive from Sony.
        new("Sony", "Sony CDU311 3.0h", "5345074", "CD-ROM CDU311", "3.0h",
            "", "3.0h", "SONY CDU311", "Sony CDU311 (8X)", "cdu311", CD_MODEL_INTERFACE_IDE, 8),

        // A 12X CD-ROM drive from Toshiba.
        new("Toshiba", "Toshiba XM-5702B TA70", "784P009803", "CD-ROM XM-5702B", "TA70",
            "", "TA70", "TOSHIBA XM-5702B", "Toshiba XM-5702B (12X)", "xm5702b", CD_MODEL_INTERFACE_IDE, 12),

        // A 16X CD-ROM drive from Goldstar.
        new("GoldStar", "GoldStar CRD-8160B 3.14", "11S02K1151ZJ13VG108019", "CD-ROM CRD-8160B", "3.14",
            "", "3.14", "GOLDSTAR CRD-8160B", "GoldStar CRD-8160B (16X)", "crd-8160b", CD_MODEL_INTERFACE_IDE, 16),

        // A 24X CD-ROM drive from Creative Labs by Matshita.
        new("MATSHITA", "Matshita CR-587-B 7S13", "8307DDB76196", "CR-587", "7S13",
            "", "7S13", "MATSHITA CR-587-B", "Creative CR-587-B (24X)", "cr-587-b", CD_MODEL_INTERFACE_IDE, 24),

        // A 32X CD-ROM drive from Creative Labs by Matshita.
        new("MATSHITA", "Matshita CR-588-B LS15", "8516DFA30742", "CR-588-B", "LS15",
            "", "LS15", "MATSHITA CR-588-B", "Creative CR-588-B (32X)", "cr-588-b", CD_MODEL_INTERFACE_IDE, 32),

        // A 36X CD-ROM drive from BTC.
        new("BTC", "BTC BCD36XH U1.0", "P81729496", "CD-ROM BCD36XH", "U1.0",
            "", "U1.0", "BCD36XH", "BTC BCD36XH (36X)", "bcd36xh", CD_MODEL_INTERFACE_IDE, 36),

        // A 40X CD-ROM drive from Philips.
        new("Philips", "Philips PCA403CD U31P", "P66839-20942 B1", "CD-ROM PCA403CD", "U31P",
            "", "U31P", "PHILIPS PCA403CD", "Philips PCA403CD (40X)", "pca403cd", CD_MODEL_INTERFACE_IDE, 40),

        // A 48X CD-ROM drive from Mitsumi.
        new("Mitsumi", "Mitsumi CRMC-FX4820T D02A", "10600125426", "CRMC-FX4820T", "D02A",
            "", "D02A", "MITSUMI CRMC-FX4820T", "Mitsumi CRMC-FX4820T (48X)", "crmc-fx4820t", CD_MODEL_INTERFACE_IDE, 48),
        // A 72X CD-ROM drive from Kenwood.
        new("KENWOOD", "KENWOOD UCR-421 208E", "9Z18612480", "CD-ROM UCR-421", "208E",
            "", "208E", "KENWOOD UCR-421", "Kenwood True-X UCR-421 (72X)", "ucr421", CD_MODEL_INTERFACE_IDE, 72),
    ];

    // pcem: scsi_cd.c:457 — model_string_40 du modèle choisi (clé cd_model, pc.c:781).
    internal static string? cd_model = null;

    // pcem: scsi_cd.c:459
    internal static string cd_get_model(int i) => cd_models[i].model_string_40;

    // pcem: scsi_cd.c:461
    internal static string cd_get_config_model(int i) => cd_models[i].model_config_string_40;

    // pcem: scsi_cd.c:463-480 — MAX_CD_MODEL vaut 12 pour douze entrées : un nom absent de la table ferait lire
    //   cd_models[12], hors de la table (PB-93 ; pc.cs refuse un cd_model inconnu, rien d'autre n'y arrive).
    internal static void cd_set_model(string? model)
    {
        if (cd_data is not null)
        {
                int c = 0;

                while (true)
                {
                        if (model is null)
                                break;
                        if (c > MAX_CD_MODEL)
                                break;
                        if (StrNCmp40(cd_models[c].model_string_40, model))
                                break;

                        c++;
                }

                cd_data.cur_model = c;
        }
    }

    // pcem: scsi_cd.c:482
    internal static int cd_get_model_interfaces(int i) => cd_models[i].interfaces;

    // pcem: scsi_cd.c:484
    internal static int cd_get_model_speed(int i) => cd_models[i].speed;

    // pcem: scsi_cd.c:486-502
    internal static string cd_model_to_config(string? model)
    {
        int c = 0;

        while (true)
        {
                if (model is null)
                        break;
                if (c > MAX_CD_MODEL)
                {
                        c = 0; // default
                        break;
                }
                if (StrNCmp40(cd_models[c].model_string_40, model))
                        break;

                c++;
        }
        return cd_models[c].model_config_string_40;
    }

    // pcem: scsi_cd.c:504-520
    internal static string cd_model_from_config(string? config)
    {
        int c = 0;

        while (true)
        {
                if (config is null)
                        break;
                if (c > MAX_CD_MODEL)
                {
                        c = 0; // default
                        break;
                }
                if (StrNCmp40(cd_models[c].model_config_string_40, config))
                        break;

                c++;
        }
        return cd_models[c].model_string_40;
    }

    // `!strncmp(a, b, 40)` : les quarante premiers caractères égaux.
    private static bool StrNCmp40(string a, string b) =>
        string.CompareOrdinal(a, 0, b, 0, 40) == 0;

    // pcem: scsi_cd.c:522-533
    private static void scsi_cd_callback(object? p)
    {
        scsi_cd_data_t data = (scsi_cd_data_t)p!;

        if (data.cmd_pos == CMD_POS_WAIT)
        {
                data.wait_time--;
                if (data.wait_time == 0)
                {
                        data.cmd_pos = data.new_cmd_pos;
                        scsi_bus_kick(data.bus!);
                }
                else
                        timer_advance_u64(data.callback_timer, 1000 * TIMER_USEC);
        }
    }

    // pcem: scsi_cd.c:536
    private const int SECTOR_TIME = 12000; /*Adjusted from above value to account for transfer time on current SCSI/ATAPI code*/

    /*The seek model is currently very basic. Seeks of less than MIN_SEEK sectors are treated as
      immediate. Seeks between MIN_SEEK and MAX_SEEK sectors scale linearly between short_seek and
      long seek.

      This really shouldn't be a linear scale, but this is a good enough approximation for now.*/
    // pcem: scsi_cd.c:543-544
    private const int MIN_SEEK = 2000;
    private const int MAX_SEEK = 333000;

    // pcem: scsi_cd.c:546-557 — ABS (ibm.h:16) sur un int, puis l'arithmétique non signée du C.
    private static int get_seek_time(scsi_cd_data_t data, uint32_t start, uint32_t dest)
    {
        int d = unchecked((int)(start - dest));
        uint32_t diff = (uint32_t)(d > 0 ? d : -d);

        if (diff < MIN_SEEK)
                return 0;
        if (diff > MAX_SEEK)
                diff = MAX_SEEK;

        diff -= MIN_SEEK;

        return (int)((uint32_t)data.short_seek + (((uint32_t)data.long_seek * diff) / (MAX_SEEK - MIN_SEEK)));
    }

    // pcem: scsi_cd.c:559-562
    private static void scsi_add_data(scsi_cd_data_t data, uint8_t val)
    {
        data.data_in[data.data_pos_write++] = val;
        data.bytes_expected++;
    }

    // pcem: scsi_cd.c:564-569
    private static void atapi_cmd_error(scsi_cd_data_t data, int sensekey, int asc, int ascq)
    {
        data.status = STATUS_CHECK_CONDITION;
        data.sense_key = sensekey;
        data.asc = asc;
        data.ascq = ascq;
    }

    // pcem: scsi_cd.c:571-577
    private static void atapi_sense_clear(scsi_cd_data_t data, int command, int ignore_ua)
    {
        if ((data.sense_key == SENSE_UNIT_ATTENTION) || ignore_ua != 0)
        {
                data.sense_key = 0;
                data.asc = 0;
                data.ascq = 0;
        }
    }

    // pcem: scsi_cd.c:579-606 — buffer[4] et buffer[5] sont lus AVANT d'être écrits : start_command les a mis à
    //   zéro, donc « support présent », « nouveau support », et atapi->load() à chaque appel.
    // pcem bug, reproduced: PB-118
    private static int atapi_event_status(uint8_t[] buffer)
    {
        uint8_t event_code, media_status = 0;

        if (buffer[5] != 0)
        {
                media_status = MS_TRAY_OPEN;
                atapi!.stop();
        }
        else
        {
                media_status = MS_MEDIA_PRESENT;
        }

        event_code = MEC_NO_CHANGE;
        if (media_status != MS_TRAY_OPEN)
        {
                if (buffer[4] == 0)
                {
                        event_code = MEC_NEW_MEDIA;
                        atapi!.load();
                }
                else if (buffer[4] == 2)
                {
                        event_code = MEC_EJECT_REQUESTED;
                        atapi!.eject();
                }
        }

        buffer[4] = event_code;
        buffer[5] = media_status;
        buffer[6] = 0;
        buffer[7] = 0;

        return 8;
    }

    // pcem: scsi_cd.c:608-618
    private static void ide_padstr8(uint8_t[] buf, int buf_off, int buf_size, string src)
    {
        int i;
        int s = 0;

        for (i = 0; i < buf_size; i++)
        {
                if (s < src.Length)
                {
                        buf[buf_off + i] = (uint8_t)src[s++];
                }
                else
                {
                        buf[buf_off + i] = (uint8_t)' ';
                }
        }
    }

    // pcem: scsi_cd.c:620-638
    private static object? scsi_cd_init(scsi_bus_t bus, int id)
    {
        scsi_cd_data_t data = new();

        data.bus = bus;
        data.id = id;

        page_flags[GPMODE_CDROM_AUDIO_PAGE] &= 0xFD;            /* Clear changed flag for CDROM AUDIO mode page. */
        Array.Clear(mode_pages_in[GPMODE_CDROM_AUDIO_PAGE], 0, 256); /* Clear the page itself. */
        page_flags[GPMODE_CDROM_AUDIO_PAGE] &= unchecked((uint8_t)~PAGE_CHANGED);

        timer_add(data.callback_timer, scsi_cd_callback, data, 0);

        cd_data = data;
        cd_set_speed(cd_speed);
        cd_set_model(cd_model);

        return data;
    }

    // pcem: scsi_cd.c:640-647
    private static object? scsi_cd_atapi_init(scsi_bus_t bus, int id, atapi_device_t atapi_dev)
    {
        scsi_cd_data_t data = (scsi_cd_data_t)scsi_cd_init(bus, id)!;

        data.is_atapi = 1;
        data.atapi_dev = atapi_dev;

        return data;
    }

    // pcem: scsi_cd.c:649-654 — free(data) : le GC.
    private static void scsi_cd_close(object p)
    {
        cd_data = null;
    }

    // pcem: scsi_cd.c:656-661
    private static void scsi_cd_reset(object p)
    {
        scsi_cd_data_t data = (scsi_cd_data_t)p;

        timer_disable(data.callback_timer);
        data.cmd_pos = CMD_POS_IDLE;
    }

    // pcem: scsi_cd.c:663
    private static void scsi_cd_illegal(scsi_cd_data_t data) { atapi_cmd_error(data, KEY_ILLEGAL_REQ, ASC_INVALID_LUN, 0); }

    /**
     * Fill in ide->buffer with the output of the ATAPI "MODE SENSE" command
     *
     * @param pos Offset within the buffer to start filling in data
     *
     * @return Offset within the buffer after the end of the data
     */
    // pcem: scsi_cd.c:672-753
    private static uint32_t ide_atapi_mode_sense(scsi_cd_data_t data, uint32_t pos, uint8_t type)
    {
        uint8_t[] buf = data.data_in;

        if (type == GPMODE_ALL_PAGES || type == GPMODE_R_W_ERROR_PAGE)
        {
                /* &01 - Read error recovery */
                buf[pos++] = GPMODE_R_W_ERROR_PAGE;
                buf[pos++] = 6; /* Page length */
                buf[pos++] = 0; /* Error recovery parameters */
                buf[pos++] = 5; /* Read retry count */
                buf[pos++] = 0; /* Reserved */
                buf[pos++] = 0; /* Reserved */
                buf[pos++] = 0; /* Reserved */
                buf[pos++] = 0; /* Reserved */
        }

        if (type == GPMODE_ALL_PAGES || type == GPMODE_CDROM_PAGE)
        {
                /* &0D - CD-ROM Parameters */
                buf[pos++] = GPMODE_CDROM_PAGE;
                buf[pos++] = 6;  /* Page length */
                buf[pos++] = 0;  /* Reserved */
                buf[pos++] = 1;  /* Inactivity time multiplier *NEEDED BY RISCOS* value is a guess */
                buf[pos++] = 0;
                buf[pos++] = 60; /* MSF settings */
                buf[pos++] = 0;
                buf[pos++] = 75; /* MSF settings */
        }

        if (type == GPMODE_ALL_PAGES || type == GPMODE_CDROM_AUDIO_PAGE)
        {
                /* &0e - CD-ROM Audio Control Parameters */
                buf[pos++] = GPMODE_CDROM_AUDIO_PAGE;
                buf[pos++] = 0xE; /* Page length */
                if ((page_flags[GPMODE_CDROM_AUDIO_PAGE] & PAGE_CHANGED) != 0)
                {
                        int i;

                        for (i = 0; i < 14; i++)
                        {
                                buf[pos++] = mode_pages_in[GPMODE_CDROM_AUDIO_PAGE][i];
                        }
                }
                else
                {
                        buf[pos++] = 5;    /* Reserved */
                        buf[pos++] = 4;    /* Reserved */
                        buf[pos++] = 0;    /* Reserved */
                        buf[pos++] = 0x80; /* Reserved */
                        buf[pos++] = 0;
                        buf[pos++] = 75;   /* Logical audio block per second */
                        buf[pos++] = 1;    /* CDDA Output Port 0 Channel Selection */
                        buf[pos++] = 0xFF; /* CDDA Output Port 0 Volume */
                        buf[pos++] = 2;    /* CDDA Output Port 1 Channel Selection */
                        buf[pos++] = 0xFF; /* CDDA Output Port 1 Volume */
                        buf[pos++] = 0;    /* CDDA Output Port 2 Channel Selection */
                        buf[pos++] = 0;    /* CDDA Output Port 2 Volume */
                        buf[pos++] = 0;    /* CDDA Output Port 3 Channel Selection */
                        buf[pos++] = 0;    /* CDDA Output Port 3 Volume */
                }
        }

        if (type == GPMODE_ALL_PAGES || type == GPMODE_CAPABILITIES_PAGE)
        {
                /* &2A - CD-ROM capabilities and mechanical status */
                buf[pos++] = GPMODE_CAPABILITIES_PAGE;
                buf[pos++] = 0x12; /* Page length */
                buf[pos++] = 0;
                buf[pos++] = 0;    /* CD-R methods */
                buf[pos++] = 1;    /* Supports audio play, not multisession */
                buf[pos++] = 0;    /* Some other stuff not supported */
                buf[pos++] = 0;    /* Some other stuff not supported (lock state + eject) */
                buf[pos++] = 0x03; /* Some other stuff not supported */
                buf[pos++] = (uint8_t)((data.max_speed * 176) >> 8);
                buf[pos++] = (uint8_t)(data.max_speed * 176); /* Maximum speed */
                buf[pos++] = 1;
                buf[pos++] = 0; /* Number of audio levels - on and off only */
                buf[pos++] = 0;
                buf[pos++] = 0; /* Buffer size - none */
                buf[pos++] = (uint8_t)((data.cur_speed * 176) >> 8);
                buf[pos++] = (uint8_t)(data.cur_speed * 176); /* Current speed */
                buf[pos++] = 0;                               /* Reserved */
                buf[pos++] = 0;                               /* Drive digital format */
                buf[pos++] = 0;                               /* Reserved */
                buf[pos++] = 0;                               /* Reserved */
        }

        return pos;
    }

    // pcem: scsi_cd.c:755-805 — la longueur lue en tête (data_out[0..1]) est celle des données du mode, prise
    //   pour celle du descripteur de bloc, et l'en-tête compté huit octets même en MODE SELECT(6) (prefix_len
    //   n'est jamais lu).
    // pcem bug, reproduced: PB-119
    private static void cdrom_mode_select(scsi_cd_data_t data, int data_len)
    {
        int len = data.data_out[1] | (data.data_out[0] << 8);
        int pos = 8 + len; /*Skip over mode parameter header*/

        while (pos < data_len)
        {
                int page_code = data.data_out[pos] & 0x3f;
                len = data.data_out[pos + 1];

                pos += 2;

                switch (page_code)
                {
                case GPMODE_CDROM_AUDIO_PAGE:
                        if (len != 0xe)
                        {
                                atapi_cmd_error(data, KEY_ILLEGAL_REQ, ASC_INV_FIELD_IN_CMD_PACKET, 0);
                                return;
                        }
                        Array.Copy(data.data_out, pos, mode_pages_in[GPMODE_CDROM_AUDIO_PAGE], 0, 0xe);
                        page_flags[GPMODE_CDROM_AUDIO_PAGE] |= PAGE_CHANGED;
                        pos += len;
                        break;

                case GPMODE_R_W_ERROR_PAGE:
                        if (len != 0x6)
                        {
                                atapi_cmd_error(data, KEY_ILLEGAL_REQ, ASC_INV_FIELD_IN_CMD_PACKET, 0);
                                return;
                        }
                        pos += len;
                        break;

                case GPMODE_CDROM_PAGE:
                        if (len != 0x6)
                        {
                                atapi_cmd_error(data, KEY_ILLEGAL_REQ, ASC_INV_FIELD_IN_CMD_PACKET, 0);
                                return;
                        }
                        pos += len;
                        break;

                case GPMODE_CAPABILITIES_PAGE:
                        if (len != 0x12)
                        {
                                atapi_cmd_error(data, KEY_ILLEGAL_REQ, ASC_INV_FIELD_IN_CMD_PACKET, 0);
                                return;
                        }
                        pos += len;
                        break;

                default:
                        atapi_cmd_error(data, KEY_ILLEGAL_REQ, ASC_INV_FIELD_IN_CMD_PACKET, 0);
                        return;
                }
        }
    }

    // pcem: scsi_cd.c:807-810 — lu par le son (sound.c), G10.5.
    internal static uint32_t atapi_get_cd_channel(int channel)
    {
        return (page_flags[GPMODE_CDROM_AUDIO_PAGE] & PAGE_CHANGED) != 0
            ? mode_pages_in[GPMODE_CDROM_AUDIO_PAGE][channel != 0 ? 8 : 6]
            : (uint32_t)(channel + 1);
    }

    // pcem: scsi_cd.c:812-815 — lu par le son (sound.c), G10.5.
    internal static uint32_t atapi_get_cd_volume(int channel)
    {
        return (page_flags[GPMODE_CDROM_AUDIO_PAGE] & PAGE_CHANGED) != 0
            ? mode_pages_in[GPMODE_CDROM_AUDIO_PAGE][channel != 0 ? 9 : 7]
            : 0xFFu;
    }

    // pcem: scsi_cd.c:816-1558
    private static int scsi_cd_command(uint8_t[] cdb, object p)
    {
        scsi_cd_data_t data = (scsi_cd_data_t)p;
        uint8_t rcdmode = 0;
        int c;
        int len;
        int msf;
        int pos = 0;
        uint32_t size;
        int is_error;
        uint8_t page_code;
        int max_len;
        uint32_t idx = 0;
        uint32_t size_idx;
        uint32_t preamble_len;
        int toc_format;
        int temp_command;
        int alloc_length;
        int completed;
        int changed;

        if (data.received_cdb == 0)
                Array.Copy(cdb, data.cdb, CDB_MAX_LEN);

        if ((cdb[0] != SCSI_REQUEST_SENSE) && (cdb[1] & 0xe0) != 0)
        {
                // omitted: pclog("non-zero LUN\n").
                /*Non-zero LUN - abort command*/
                scsi_cd_illegal(data);
                return BUS_CD | BUS_IO;
        }

        // omitted: pclog("New CD command %02X %i\n", cdb[0], ins) — sortie pure.
        msf = cdb[1] & 2;

        is_error = 0;
        changed = atapi!.medium_changed();

        /*If UNIT_ATTENTION is set, error out with NOT_READY.
          VIDE-CDD.SYS will then issue a READ_TOC, which can pass through UNIT_ATTENTION and will clear sense.
          NT 3.1 / AZTIDECD.SYS will then issue a REQUEST_SENSE, which can also pass through UNIT_ATTENTION but will clear sense
          AFTER sending it back. In any case, if the command cannot pass through, set our state to errored.*/
        if ((atapi_cmd_table[cdb[0]] & ALLOW_UA) == 0 && data.sense_key == SENSE_UNIT_ATTENTION)
        {
                is_error = 1;
        }
        /*Unless the command issued was a REQUEST_SENSE or TEST_UNIT_READY, clear sense.
          This is important because both VIDE-CDD.SYS and NT 3.1 / AZTIDECD.SYS rely on this behaving VERY specifically.
          VIDE-CDD.SYS will clear sense through READ_TOC, while NT 3.1 / AZTIDECD.SYS will issue a REQUEST_SENSE.*/
        if ((cdb[0] != GPCMD_REQUEST_SENSE) && (cdb[0] != GPCMD_TEST_UNIT_READY))
        {
                /* GPCMD_TEST_UNIT_READY is NOT supposed to clear sense! */
                atapi_sense_clear(data, cdb[0], 1);
        }

        /*If our state has been set to errored, clear it, and return.*/
        if (is_error != 0)
        {
                is_error = 0;
                return SCSI_PHASE_STATUS;
        }

        if ((atapi_cmd_table[cdb[0]] & CHECK_READY) != 0 && atapi.ready() == 0)
        {
                atapi_cmd_error(data, SENSE_NOT_READY, ASC_MEDIUM_NOT_PRESENT, 0);
                return SCSI_PHASE_STATUS;
        }
        if ((atapi_cmd_table[cdb[0]] & CHECK_READY) != 0 && changed != 0)
        {
                // omitted: pclog("Medium changed\n").
                atapi_cmd_error(data, SENSE_UNIT_ATTENTION, ASC_MEDIUM_MAY_HAVE_CHANGED, 0);
                return SCSI_PHASE_STATUS;
        }

        data.prev_status = data.cd_status;
        data.cd_status = atapi.status();
        if (((data.prev_status == cdrom_ioctl.CD_STATUS_PLAYING) || (data.prev_status == cdrom_ioctl.CD_STATUS_PAUSED)) &&
            ((data.cd_status != cdrom_ioctl.CD_STATUS_PLAYING) && (data.cd_status != cdrom_ioctl.CD_STATUS_PAUSED)))
                completed = 1;
        else
                completed = 0;

        data.status = STATUS_GOOD;

        switch (cdb[0])
        {
        case GPCMD_TEST_UNIT_READY:
                data.cmd_pos = CMD_POS_IDLE;
                return SCSI_PHASE_STATUS;

        case SCSI_REZERO_UNIT:
                if (data.cmd_pos == CMD_POS_IDLE)
                {
                        uint32_t old_pos = (uint32_t)data.cdpos;
                        int seek_time;

                        data.cdpos = 0;

                        seek_time = get_seek_time(data, old_pos, (uint32_t)data.cdpos);
                        if (seek_time != 0)
                        {
                                data.cmd_pos = CMD_POS_WAIT;
                                data.new_cmd_pos = CMD_POS_COMPLETE;
                                data.wait_time = seek_time;
                                timer_set_delay_u64(data.callback_timer, 1000 * TIMER_USEC);
                                return SCSI_PHASE_COMMAND;
                        }
                }
                data.cmd_pos = CMD_POS_IDLE;
                return SCSI_PHASE_STATUS;

        case SCSI_REQUEST_SENSE: /* Used by ROS 4+ */
                alloc_length = cdb[4];
                temp_command = cdb[0];

                /*Will return 18 bytes of 0*/
                Array.Clear(data.data_in, 0, 512);

                data.data_in[0] = 0x80 | 0x70;

                if ((data.sense_key > 0) || (data.cd_status < cdrom_ioctl.CD_STATUS_PLAYING))
                {
                        if (completed != 0)
                        {
                                data.data_in[2] = SENSE_ILLEGAL_REQUEST;
                                data.data_in[12] = ASC_AUDIO_PLAY_OPERATION;
                                data.data_in[13] = ASCQ_AUDIO_PLAY_OPERATION_COMPLETED;
                        }
                        else
                        {
                                data.data_in[2] = (uint8_t)data.sense_key;
                                data.data_in[12] = (uint8_t)data.asc;
                                data.data_in[13] = (uint8_t)data.ascq;
                        }
                }
                else
                {
                        data.data_in[2] = SENSE_ILLEGAL_REQUEST;
                        data.data_in[12] = ASC_AUDIO_PLAY_OPERATION;
                        data.data_in[13] = (data.cd_status == cdrom_ioctl.CD_STATUS_PLAYING) ? (uint8_t)ASCQ_AUDIO_PLAY_OPERATION_IN_PROGRESS
                                                                                            : (uint8_t)ASCQ_AUDIO_PLAY_OPERATION_PAUSED;
                }

                data.data_in[7] = 0;

                /* Clear the sense stuff as per the spec. */
                atapi_sense_clear(data, temp_command, 0);

                data.bytes_expected = data.data_pos_write = MIN(alloc_length, 18);
                return alloc_length != 0 ? SCSI_PHASE_DATA_IN : SCSI_PHASE_STATUS;

        case SCSI_SEEK_6:
                if (data.cmd_pos == CMD_POS_IDLE)
                {
                        uint32_t old_pos = (uint32_t)data.cdpos;
                        int seek_time;

                        data.cdpos = (int)((((uint32_t)cdb[1] & 0x1f) << 16) | ((uint32_t)cdb[2] << 8) | (uint32_t)cdb[3]);

                        seek_time = get_seek_time(data, old_pos, (uint32_t)data.cdpos);
                        if (seek_time != 0)
                        {
                                data.cmd_pos = CMD_POS_WAIT;
                                data.new_cmd_pos = CMD_POS_COMPLETE;
                                data.wait_time = seek_time;
                                timer_set_delay_u64(data.callback_timer, 1000 * TIMER_USEC);
                                return SCSI_PHASE_COMMAND;
                        }
                }
                data.cmd_pos = CMD_POS_IDLE;
                return SCSI_PHASE_STATUS;

        case GPCMD_SET_SPEED:
                data.cur_speed = (cdb[3] | (cdb[2] << 8)) / 176;
                if (data.cur_speed < 1)
                        data.cur_speed = 1;
                else if (data.cur_speed > data.max_speed)
                        data.cur_speed = data.max_speed;
                data.cmd_pos = CMD_POS_IDLE;
                return SCSI_PHASE_STATUS;

        case GPCMD_MECHANISM_STATUS: /*0xbd*/
                alloc_length = (cdb[7] << 16) | (cdb[8] << 8) | cdb[9];

                // pcem bug, not reproduced: PB-115 — fatal("Zero allocation length to MECHANISM STATUS not impl.\n")
                //   (:993), à la portée de tout invité.
                // DEVIATION: comme un vrai lecteur, aucune donnée et GOOD — ce que fait déjà la suite du code.
                if (alloc_length == 0)
                        R9.Garde("scsi_cd.c:993");

                data.data_in[0] = 0;
                data.data_in[1] = 0;
                data.data_in[2] = 0;
                data.data_in[3] = 0;
                data.data_in[4] = 0;
                data.data_in[5] = 1;
                data.data_in[6] = 0;
                data.data_in[7] = 0;

                data.data_pos_read = 0;
                data.bytes_expected = data.data_pos_write = MIN(alloc_length, 8);
                return alloc_length != 0 ? SCSI_PHASE_DATA_IN : SCSI_PHASE_STATUS;

        case GPCMD_READ_TOC_PMA_ATIP:
                toc_format = cdb[2] & 0xf;
                if (toc_format == 0)
                        toc_format = (cdb[9] >> 6) & 3;
                alloc_length = cdb[8] + (cdb[7] << 8);
                switch (toc_format)
                {
                case 0: /*Normal*/
                        len = atapi.readtoc(data.data_in, 0, cdb[6], msf, alloc_length, 0);
                        break;
                case 1: /*Multi session*/
                        len = atapi.readtoc_session(data.data_in, 0, msf, alloc_length);
                        data.data_in[0] = 0;
                        data.data_in[1] = 0xA;
                        break;
                case 2: /*Raw*/
                        // La TOC brute : ni lead-out, ni longueur en tête (data_in[0..1] reste à zéro).
                        // pcem bug, reproduced: PB-120
                        len = atapi.readtoc_raw(data.data_in, 0, alloc_length);
                        break;
                default:
                        atapi_cmd_error(data, SENSE_ILLEGAL_REQUEST, ASC_LBA_OUT_OF_RANGE, 0);
                        data.cmd_pos = CMD_POS_IDLE;
                        return SCSI_PHASE_STATUS;
                }
                data.data_pos_read = 0;
                data.bytes_expected = data.data_pos_write = MIN(alloc_length, len);
                return alloc_length != 0 ? SCSI_PHASE_DATA_IN : SCSI_PHASE_STATUS;

        case GPCMD_READ_CD:
                rcdmode = (uint8_t)(cdb[9] & 0xF8);
                if ((rcdmode != 0x10) && (rcdmode != 0xF8))
                {
                        atapi_cmd_error(data, SENSE_ILLEGAL_REQUEST, ASC_ILLEGAL_OPCODE, 0);
                        data.cmd_pos = CMD_POS_IDLE;
                        return SCSI_PHASE_STATUS;
                }

                if (data.cmd_pos == CMD_POS_IDLE)
                {
                        uint32_t old_pos = (uint32_t)data.cdpos;
                        int seek_time;

                        data.cdlen = (cdb[6] << 16) | (cdb[7] << 8) | cdb[8];
                        data.cdpos = (cdb[2] << 24) | (cdb[3] << 16) | (cdb[4] << 8) | cdb[5];

                        data.cmd_pos = CMD_POS_START_SECTOR;
                        data.bytes_expected = data.cdlen * ((rcdmode == 0x10) ? 2048 : 2352);
                        if (data.is_atapi != 0)
                        {
                                if (rcdmode == 0x10)
                                        atapi_set_transfer_granularity(data.atapi_dev!, 2048);
                                else
                                        atapi_set_transfer_granularity(data.atapi_dev!, 2352);
                        }

                        seek_time = get_seek_time(data, old_pos, (uint32_t)data.cdpos);
                        if (seek_time != 0)
                        {
                                data.cmd_pos = CMD_POS_WAIT;
                                data.new_cmd_pos = CMD_POS_START_SECTOR;
                                data.wait_time = seek_time;
                                timer_set_delay_u64(data.callback_timer, 1000 * TIMER_USEC);
                                return SCSI_PHASE_COMMAND;
                        }
                }
                if (data.cmd_pos == CMD_POS_WAIT)
                        return SCSI_PHASE_COMMAND;
                if (data.cdlen == 0)
                {
                        data.cmd_pos = CMD_POS_IDLE;
                        return SCSI_PHASE_STATUS;
                }
                if (data.cmd_pos == CMD_POS_START_SECTOR)
                {
                        uint64_t time = (((uint64_t)SECTOR_TIME * TIMER_USEC) / (uint64_t)data.cur_speed) * (uint64_t)data.cdlen;

                        data.cmd_pos = CMD_POS_WAIT;
                        data.new_cmd_pos = CMD_POS_TRANSFER;

                        if (time > (TIMER_USEC * 1000UL))
                        {
                                data.wait_time = (int)(time / (TIMER_USEC * 1000UL));
                                timer_set_delay_u64(data.callback_timer, time % (TIMER_USEC * 1000UL));
                        }
                        else
                        {
                                timer_set_delay_u64(data.callback_timer, time);
                                data.wait_time = 1;
                        }
                        return SCSI_PHASE_COMMAND;
                }

                if (rcdmode == 0x10)
                {
                        c = MIN(data.cdlen, MAX_NR_SECTORS);
                        if (atapi.readsector(data.data_in, 0, data.cdpos, c) != 0)
                        {
                                atapi_cmd_error(data, SENSE_ILLEGAL_REQUEST, ASC_LBA_OUT_OF_RANGE, 0);
                                data.cmd_pos = CMD_POS_IDLE;
                                return SCSI_PHASE_STATUS;
                        }
                        data.cdpos += c;
                        data.cdlen -= c;
                }
                else
                {
                        for (c = 0; c < MAX_NR_SECTORS; c++)
                        {
                                atapi.readsector_raw(data.data_in, c * 2352, data.cdpos);

                                data.cdpos++;
                                data.cdlen--;
                                if (data.cdlen == 0)
                                {
                                        c++;
                                        break;
                                }
                        }
                }
                pc.readflash_set(pc.READFLASH_HDC, data.id);

                data.data_pos_read = 0;
                data.data_pos_write = c * ((rcdmode == 0x10) ? 2048 : 2352);
                return SCSI_PHASE_DATA_IN;

        case GPCMD_READ_6:
        case GPCMD_READ_10:
        case GPCMD_READ_12:
                if (data.cmd_pos == CMD_POS_IDLE)
                {
                        uint32_t old_pos = (uint32_t)data.cdpos;
                        int seek_time;

                        if (cdb[0] == GPCMD_READ_6)
                        {
                                data.cdlen = cdb[4];
                                data.cdpos = (int)((((uint32_t)cdb[1] & 0x1f) << 16) | ((uint32_t)cdb[2] << 8) | (uint32_t)cdb[3]);
                        }
                        else if (cdb[0] == GPCMD_READ_10)
                        {
                                data.cdlen = (cdb[7] << 8) | cdb[8];
                                data.cdpos = (cdb[2] << 24) | (cdb[3] << 16) | (cdb[4] << 8) | cdb[5];
                        }
                        else
                        {
                                data.cdlen = (int)(((uint32_t)cdb[6] << 24) | ((uint32_t)cdb[7] << 16) | ((uint32_t)cdb[8] << 8) |
                                                   (uint32_t)cdb[9]);
                                data.cdpos = (int)(((uint32_t)cdb[2] << 24) | ((uint32_t)cdb[3] << 16) | ((uint32_t)cdb[4] << 8) |
                                                   (uint32_t)cdb[5]);
                        }

                        data.cmd_pos = CMD_POS_START_SECTOR;
                        data.bytes_expected = data.cdlen * 2048;
                        if (data.is_atapi != 0)
                                atapi_set_transfer_granularity(data.atapi_dev!, 2048);

                        seek_time = get_seek_time(data, old_pos, (uint32_t)data.cdpos);

                        if (seek_time != 0)
                        {
                                data.cmd_pos = CMD_POS_WAIT;
                                data.new_cmd_pos = CMD_POS_START_SECTOR;
                                data.wait_time = seek_time;
                                timer_set_delay_u64(data.callback_timer, 1000 * TIMER_USEC);
                                return SCSI_PHASE_COMMAND;
                        }
                }
                if (data.cmd_pos == CMD_POS_WAIT)
                        return SCSI_PHASE_COMMAND;
                if (data.cdlen == 0)
                {
                        data.cmd_pos = CMD_POS_IDLE;
                        return SCSI_PHASE_STATUS;
                }
                if (data.cmd_pos == CMD_POS_START_SECTOR)
                {
                        uint64_t time = (((uint64_t)SECTOR_TIME * TIMER_USEC) / (uint64_t)data.cur_speed) * (uint64_t)data.cdlen;

                        data.cmd_pos = CMD_POS_WAIT;
                        data.new_cmd_pos = CMD_POS_TRANSFER;

                        if (time > (TIMER_USEC * 1000UL))
                        {
                                data.wait_time = (int)(time / (TIMER_USEC * 1000UL));
                                timer_set_delay_u64(data.callback_timer, time % (TIMER_USEC * 1000UL));
                        }
                        else
                        {
                                timer_set_delay_u64(data.callback_timer, time);
                                data.wait_time = 1;
                        }
                        return SCSI_PHASE_COMMAND;
                }

                pc.readflash_set(pc.READFLASH_HDC, data.id);
                c = MIN(data.cdlen, MAX_NR_SECTORS);
                // pcem bug, not reproduced: PB-115 — READ(12) de 2^31 secteurs ou plus : cdlen négatif, et readsector
                //   reçoit un compte négatif (un new[] démesuré, puis la chute de l'hôte).
                // DEVIATION: un échec de lecture, comme hors du disque.
                if (c < 0)
                {
                        R9.Garde("scsi_cd.c:1187");
                        atapi_cmd_error(data, SENSE_ILLEGAL_REQUEST, ASC_LBA_OUT_OF_RANGE, 0);
                        data.cmd_pos = CMD_POS_IDLE;
                        return SCSI_PHASE_STATUS;
                }
                if (atapi.readsector(data.data_in, 0, data.cdpos, c) != 0)
                {
                        atapi_cmd_error(data, SENSE_ILLEGAL_REQUEST, ASC_LBA_OUT_OF_RANGE, 0);
                        data.cmd_pos = CMD_POS_IDLE;
                        return SCSI_PHASE_STATUS;
                }
                data.cdpos += c;
                data.cdlen -= c;

                data.data_pos_read = 0;
                data.data_pos_write = c * 2048;
                return SCSI_PHASE_DATA_IN;

        case GPCMD_READ_HEADER:
                alloc_length = cdb[8] | (cdb[7] << 8);
                if (msf != 0)
                {
                        atapi_cmd_error(data, SENSE_ILLEGAL_REQUEST, ASC_ILLEGAL_OPCODE, 0);
                        return SCSI_PHASE_STATUS;
                }
                for (c = 0; c < 4; c++)
                        data.data_in[c + 4] = cdb[c + 2];
                data.data_in[0] = 1; /*2048 bytes user data*/
                data.data_in[1] = data.data_in[2] = data.data_in[3] = 0;

                data.bytes_expected = data.data_pos_write = MIN(6, alloc_length);
                return alloc_length != 0 ? SCSI_PHASE_DATA_IN : SCSI_PHASE_STATUS;

        case GPCMD_MODE_SENSE_6:
        case GPCMD_MODE_SENSE_10:
                temp_command = cdb[0];

                if (temp_command == GPCMD_MODE_SENSE_6)
                        len = cdb[4];
                else
                        len = cdb[8] | (cdb[7] << 8);

                c = cdb[2] & 0x3F;

                Array.Clear(data.data_in, 0, len);
                alloc_length = len;
                if ((mode_sense_pages[c] & IMPLEMENTED) == 0)
                {
                        atapi_cmd_error(data, SENSE_ILLEGAL_REQUEST, ASC_INV_FIELD_IN_CMD_PACKET, 0);
                        return SCSI_PHASE_STATUS;
                }

                if (temp_command == GPCMD_MODE_SENSE_6)
                {
                        len = (int)ide_atapi_mode_sense(data, 4, (uint8_t)c);
                        data.data_in[0] = (uint8_t)(len - 1);
                        data.data_in[1] = 3; /*120mm data CD-ROM*/
                }
                else
                {
                        len = (int)ide_atapi_mode_sense(data, 8, (uint8_t)c);
                        data.data_in[0] = (uint8_t)((len - 2) >> 8);
                        data.data_in[1] = (uint8_t)((len - 2) & 255);
                        data.data_in[2] = 3; /*120mm data CD-ROM*/
                }

                data.bytes_expected = data.data_pos_write = MIN(len, alloc_length);
                data.cmd_pos = CMD_POS_IDLE;
                return alloc_length != 0 ? SCSI_PHASE_DATA_IN : SCSI_PHASE_STATUS;

        case GPCMD_MODE_SELECT_6:
        case GPCMD_MODE_SELECT_10:
                // Une longueur 0 : bytes_required vaut 0, write_complete ne devient jamais vrai, et la phase de
                //   données ne finit pas (DRQ, compte d'octets 0 puis FFFEh).
                // pcem bug, reproduced: PB-119
                if (data.cmd_pos == CMD_POS_IDLE)
                {
                        int len_;

                        if (cdb[0] == GPCMD_MODE_SELECT_6)
                        {
                                len_ = cdb[4];
                                data.prefix_len = 6;
                        }
                        else
                        {
                                len_ = (cdb[7] << 8) | cdb[8];
                                data.prefix_len = 10;
                        }

                        data.cmd_pos = CMD_POS_TRANSFER;
                        data.bytes_required = len_;
                        return SCSI_PHASE_DATA_OUT;
                }
                if (data.bytes_received == data.bytes_required)
                {
                        cdrom_mode_select(data, data.bytes_required);
                        data.cmd_pos = CMD_POS_IDLE;
                        return SCSI_PHASE_STATUS;
                }
                return SCSI_PHASE_DATA_OUT;

        case GPCMD_GET_EVENT_STATUS_NOTIFICATION: /*0x4a*/
                temp_command = cdb[0];
                // La longueur d'allocation lue sur trois octets (7 à 9), l'octet de contrôle compris.
                // pcem bug, reproduced: PB-118
                alloc_length = (cdb[7] << 16) | (cdb[8] << 8) | cdb[9];

                {
                        // gesn_cdb : opcode (0), polled (1), reserved2 (2-3), class (4), reserved3 (5-6), len (7-8),
                        //   control (9) ; gesn_event_header, dans data_in : len (0-1, uint16_t de l'hôte), notification_class
                        //   (2), supported_events (3) — structures empaquetées, rendues par leurs décalages.
                        uint32_t used_len;

                        /* It is fine by the MMC spec to not support async mode operations */
                        if ((cdb[1] & 0x01) == 0)
                        { /* asynchronous mode */
                                /* Only polling is supported, asynchronous mode is not. */
                                atapi_cmd_error(data, SENSE_ILLEGAL_REQUEST, ASC_ILLEGAL_OPCODE, 0);
                                return SCSI_PHASE_STATUS;
                        }

                        /* polling mode operation */

                        /*
                         * These are the supported events.
                         *
                         * We currently only support requests of the 'media' type.
                         * Notification class requests and supported event classes are bitmasks,
                         * but they are built from the same values as the "notification class"
                         * field.
                         */
                        data.data_in[3] = 1 << GESN_MEDIA;

                        /*
                         * We use |= below to set the class field; other bits in this byte
                         * are reserved now but this is useful to do if we have to use the
                         * reserved fields later.
                         */
                        data.data_in[2] = 0;

                        /*
                         * Responses to requests are to be based on request priority.  The
                         * notification_class_request_type enum above specifies the
                         * priority: upper elements are higher prio than lower ones.
                         */
                        if ((cdb[4] & (1 << GESN_MEDIA)) != 0)
                        {
                                data.data_in[2] |= GESN_MEDIA;
                                used_len = (uint32_t)atapi_event_status(data.data_in);
                        }
                        else
                        {
                                data.data_in[2] = 0x80; /* No event available */
                                used_len = 4;
                        }
                        len = (int)used_len;
                        // La longueur de l'en-tête, un uint16_t dans l'ordre de l'hôte : petit-boutiste.
                        // pcem bug, reproduced: PB-118
                        data.data_in[0] = (uint8_t)(used_len - 4);
                        data.data_in[1] = (uint8_t)((used_len - 4) >> 8);
                }

                data.bytes_expected = data.data_pos_write = MIN(alloc_length, len);
                data.cmd_pos = CMD_POS_IDLE;
                return alloc_length != 0 ? SCSI_PHASE_DATA_IN : SCSI_PHASE_STATUS;

        case GPCMD_READ_DISC_INFORMATION:
                alloc_length = cdb[8] | (cdb[7] << 8);

                data.data_in[1] = 32;
                data.data_in[2] = 0xe;  /* last session complete, disc finalized */
                data.data_in[3] = 1;    /* first track on disc */
                data.data_in[4] = 1;    /* # of sessions */
                data.data_in[5] = 1;    /* first track of last session */
                data.data_in[6] = 1;    /* last track of last session */
                data.data_in[7] = 0x20; /* unrestricted use */
                data.data_in[8] = 0x00; /* CD-ROM */

                data.bytes_expected = data.data_pos_write = MIN(alloc_length, 34);
                data.cmd_pos = CMD_POS_IDLE;
                return alloc_length != 0 ? SCSI_PHASE_DATA_IN : SCSI_PHASE_STATUS;

        case GPCMD_PLAY_AUDIO_10:
        case GPCMD_PLAY_AUDIO_12:
        case GPCMD_PLAY_AUDIO_MSF:
                /*This is apparently deprecated in the ATAPI spec, and apparently
                  has been since 1995 (!). Hence I'm having to guess most of it*/
                if (cdb[0] == GPCMD_PLAY_AUDIO_10)
                {
                        pos = (cdb[2] << 24) | (cdb[3] << 16) | (cdb[4] << 8) | cdb[5];
                        len = (cdb[7] << 8) | cdb[8];
                }
                else if (cdb[0] == GPCMD_PLAY_AUDIO_MSF)
                {
                        pos = (cdb[3] << 16) | (cdb[4] << 8) | cdb[5];
                        len = (cdb[6] << 16) | (cdb[7] << 8) | cdb[8];
                }
                else
                {
                        pos = (cdb[3] << 16) | (cdb[4] << 8) | cdb[5];
                        len = (cdb[7] << 16) | (cdb[8] << 8) | cdb[9];
                }

                if ((cdrom_ioctl.cdrom_drive < 1) || (data.cd_status <= cdrom_ioctl.CD_STATUS_DATA_ONLY) ||
                    atapi.is_track_audio((uint32_t)pos, (cdb[0] == GPCMD_PLAY_AUDIO_MSF) ? 1 : 0) == 0)
                {
                        atapi_cmd_error(data, SENSE_ILLEGAL_REQUEST, ASC_ILLEGAL_MODE_FOR_THIS_TRACK, 0);
                        return SCSI_PHASE_STATUS;
                }

                atapi.playaudio((uint32_t)pos, (uint32_t)len, (cdb[0] == GPCMD_PLAY_AUDIO_MSF) ? 1 : 0);
                data.cmd_pos = CMD_POS_IDLE;
                return SCSI_PHASE_STATUS;

        case GPCMD_READ_SUBCHANNEL:
                alloc_length = cdb[8] | (cdb[7] << 8);
                if (cdb[3] != 1)
                {
                        atapi_cmd_error(data, SENSE_ILLEGAL_REQUEST, ASC_ILLEGAL_OPCODE, 0);
                        return SCSI_PHASE_STATUS;
                }
                pos = 0;
                data.data_in[pos++] = 0;
                data.data_in[pos++] = 0; /*Audio status*/
                data.data_in[pos++] = 0;
                data.data_in[pos++] = 0; /*Subchannel length*/
                data.data_in[pos++] = 1; /*Format code*/
                data.data_in[1] = atapi.getcurrentsubchannel(data.data_in, 5, msf);
                len = 11 + 5;
                if ((cdb[2] & 0x40) == 0)
                        len = 4;

                data.bytes_expected = data.data_pos_write = MIN(alloc_length, len);
                return alloc_length != 0 ? SCSI_PHASE_DATA_IN : SCSI_PHASE_STATUS;

        case GPCMD_START_STOP_UNIT:
                if (cdb[4] == 0)
                        atapi.stop();
                else if (cdb[4] == 2)
                        atapi.eject();
                else if (cdb[4] == 3)
                        atapi.load();
                data.cmd_pos = CMD_POS_IDLE;
                return SCSI_PHASE_STATUS;

        case GPCMD_INQUIRY:
                page_code = cdb[2];
                max_len = cdb[4];
                alloc_length = max_len;
                temp_command = cdb[0];

                if ((cdb[1] & 1) != 0)
                {
                        preamble_len = 4;
                        size_idx = 3;

                        data.data_in[idx++] = 05;
                        data.data_in[idx++] = page_code;
                        data.data_in[idx++] = 0;

                        idx++;

                        switch (page_code)
                        {
                        case 0x00:
                                data.data_in[idx++] = 0x00;
                                data.data_in[idx++] = 0x83;
                                break;

                        case 0x83:
                                if (idx + 24 > (uint32_t)max_len)
                                {
                                        atapi_cmd_error(data, SENSE_ILLEGAL_REQUEST, ASC_DATA_PHASE_ERROR, 0);
                                        return SCSI_PHASE_STATUS;
                                }
                                data.data_in[idx++] = 0x02;
                                data.data_in[idx++] = 0x00;
                                data.data_in[idx++] = 0x00;
                                data.data_in[idx++] = 20;
                                ide_padstr8(data.data_in, (int)idx, 20, "53R141"); /* Serial */
                                idx += 20;

                                if (idx + 72 > (uint32_t)max_len)
                                {
                                        goto atapi_out;
                                }
                                data.data_in[idx++] = 0x02;
                                data.data_in[idx++] = 0x01;
                                data.data_in[idx++] = 0x00;
                                data.data_in[idx++] = 68;
                                ide_padstr8(data.data_in, (int)idx, 8, cd_models[cd_data!.cur_model].vendor_8); /* Vendor */
                                idx += 8;
                                ide_padstr8(data.data_in, (int)idx, 40,
                                            cd_models[cd_data.cur_model].model_and_firmware_40); /* Product */
                                idx += 40;
                                ide_padstr8(data.data_in, (int)idx, 20, cd_models[cd_data.cur_model].serial_20); /* Product */
                                idx += 20;
                                break;

                        default:
                                atapi_cmd_error(data, SENSE_ILLEGAL_REQUEST, ASC_INV_FIELD_IN_CMD_PACKET, 0);
                                return SCSI_PHASE_STATUS;
                        }
                }
                else
                {
                        preamble_len = 5;
                        size_idx = 4;

                        data.data_in[0] = 5;    /*CD-ROM*/
                        data.data_in[1] = 0x80; /*Removable*/
                        if (data.is_atapi != 0)
                        {
                                data.data_in[2] = 0;    /*Not ANSI compliant*/
                                data.data_in[3] = 0x21; /*ATAPI compliant*/
                        }
                        else
                        {
                                data.data_in[2] = 2; /*SCSI-2 compliant*/
                                data.data_in[3] = 0x02;
                        }
                        data.data_in[4] = 31;
                        data.data_in[5] = 0;
                        data.data_in[6] = 0;
                        data.data_in[7] = 0;

                        ide_padstr8(data.data_in, 8, 8, cd_models[cd_data!.cur_model].vendor_8);    /* Vendor */
                        ide_padstr8(data.data_in, 16, 16, cd_models[cd_data.cur_model].model_16);  /* Product */
                        ide_padstr8(data.data_in, 32, 4, cd_models[cd_data.cur_model].firmware_4); /* Revision */

                        idx = 36;
                }

        atapi_out:
                data.data_in[size_idx] = (uint8_t)(idx - preamble_len);

                data.bytes_expected = data.data_pos_write = MIN(alloc_length, (int)idx);
                return alloc_length != 0 ? SCSI_PHASE_DATA_IN : SCSI_PHASE_STATUS;

        case GPCMD_PREVENT_REMOVAL:
                data.cmd_pos = CMD_POS_IDLE;
                return SCSI_PHASE_STATUS;

        case GPCMD_PAUSE_RESUME:
                if ((cdb[8] & 1) != 0)
                        atapi.resume();
                else
                        atapi.pause();
                data.cmd_pos = CMD_POS_IDLE;
                return SCSI_PHASE_STATUS;

        case GPCMD_SEEK:
                if (data.cmd_pos == CMD_POS_IDLE)
                {
                        uint32_t old_pos = (uint32_t)data.cdpos;
                        int seek_time;

                        data.cdpos = (cdb[3] << 16) | (cdb[4] << 8) | cdb[5];

                        atapi.seek((uint32_t)data.cdpos);

                        seek_time = get_seek_time(data, old_pos, (uint32_t)data.cdpos);
                        if (seek_time != 0)
                        {
                                data.cmd_pos = CMD_POS_WAIT;
                                data.new_cmd_pos = CMD_POS_COMPLETE;
                                data.wait_time = seek_time;
                                timer_set_delay_u64(data.callback_timer, 1000 * TIMER_USEC);
                                return SCSI_PHASE_COMMAND;
                        }
                }
                data.cmd_pos = CMD_POS_IDLE;
                return SCSI_PHASE_STATUS;

        case GPCMD_READ_CDROM_CAPACITY:
                // La capacité de l'image (fin + 1, PB de G10.3) rendue telle quelle : le nombre de blocs + 1, là où
                //   READ CAPACITY attend le dernier LBA — 33 pour 32 secteurs.
                // pcem bug, reproduced: PB-117
                size = atapi.size();
                scsi_add_data(data, (uint8_t)((size >> 24) & 0xff));
                scsi_add_data(data, (uint8_t)((size >> 16) & 0xff));
                scsi_add_data(data, (uint8_t)((size >> 8) & 0xff));
                scsi_add_data(data, (uint8_t)(size & 0xff));
                scsi_add_data(data, (2048 >> 24) & 0xff);
                scsi_add_data(data, (2048 >> 16) & 0xff);
                scsi_add_data(data, (2048 >> 8) & 0xff);
                scsi_add_data(data, 2048 & 0xff);
                // omitted: pclog("READ_CDROM_CAPACITY %08x\n", size).
                data.cmd_pos = CMD_POS_IDLE;
                return SCSI_PHASE_DATA_IN;

        case GPCMD_STOP_PLAY_SCAN:
                atapi.stop();
                data.cmd_pos = CMD_POS_IDLE;
                return SCSI_PHASE_STATUS;

        case GPCMD_SEND_DVD_STRUCTURE:
        default:
                atapi_cmd_error(data, SENSE_ILLEGAL_REQUEST, ASC_ILLEGAL_OPCODE, 0);
                return SCSI_PHASE_STATUS;
        }
    }

    // pcem: scsi_cd.c:1560-1571
    private static uint8_t scsi_cd_read(object p)
    {
        scsi_cd_data_t data = (scsi_cd_data_t)p;
        uint8_t temp;

        data.data_bytes_read++;
        // pcem bug, not reproduced: PB-115 — après un remplissage raté (un READ qui atteint la fin du disque) ou un
        //   bytes_expected débordé (cdlen × 2048 ou × 2352, :1057, :1149), la lecture passe la fin de data_in
        //   (:1565) : hors du tableau en C — data_out, les champs du struct, puis hors de l'allocation et la chute
        //   de l'hôte. En deçà, les octets périmés de data_in sont rendus, comme chez PCem.
        // DEVIATION: à la fin de data_in, le transfert s'arrête — un dernier octet nul, puis CHECK CONDITION,
        //   ILLEGAL REQUEST / LBA OUT OF RANGE.
        if (data.data_pos_read >= BUFFER_SIZE)
        {
                R9.Garde("scsi_cd.c:1565");
                if (data.status == STATUS_GOOD)
                        atapi_cmd_error(data, SENSE_ILLEGAL_REQUEST, ASC_LBA_OUT_OF_RANGE, 0);
                data.bytes_expected = data.data_bytes_read;
                return 0;
        }
        temp = data.data_in[data.data_pos_read++];

        if (data.data_pos_read == data.data_pos_write && data.data_bytes_read < data.bytes_expected)
                scsi_cd_command(data.cdb, data);

        return temp;
    }

    // pcem: scsi_cd.c:1573-1582
    private static void scsi_cd_write(uint8_t val, object p)
    {
        scsi_cd_data_t data = (scsi_cd_data_t)p;

        // pcem bug, not reproduced: PB-115 — data_out[262 144] est écrit hors du tableau, puis
        //   fatal("Exceeded data_out buffer size\n") (:1576-1581) : un MODE SELECT de longueur 0 (PB-119) ne finit
        //   jamais, et un invité qui insiste y arrive.
        // DEVIATION: au-delà du tampon, l'octet est compté mais pas gardé ; la phase sans fin reste celle de PCem.
        if (data.data_pos_write >= BUFFER_SIZE)
        {
                R9.Garde("scsi_cd.c:1581");
                data.data_pos_write++;
        }
        else
                data.data_out[data.data_pos_write++] = val;

        data.bytes_received++;
    }

    // pcem: scsi_cd.c:1584-1588
    private static int scsi_cd_read_complete(object p)
    {
        scsi_cd_data_t data = (scsi_cd_data_t)p;

        return (data.data_bytes_read == data.bytes_expected) ? 1 : 0;
    }

    // pcem: scsi_cd.c:1589-1593
    private static int scsi_cd_write_complete(object p)
    {
        scsi_cd_data_t data = (scsi_cd_data_t)p;

        return (data.bytes_received == data.bytes_required) ? 1 : 0;
    }

    // pcem: scsi_cd.c:1595-1607
    private static void scsi_cd_start_command(object p)
    {
        scsi_cd_data_t data = (scsi_cd_data_t)p;

        data.bytes_received = 0;
        data.bytes_required = 0;
        data.data_pos_read = data.data_pos_write = 0;
        data.data_bytes_read = data.bytes_expected = 0;

        data.received_cdb = 0;

        data.cmd_pos = CMD_POS_IDLE;
        Array.Clear(data.data_in, 0, 256);
    }

    // pcem: scsi_cd.c:1609-1613
    private static uint8_t scsi_cd_get_status(object p)
    {
        scsi_cd_data_t data = (scsi_cd_data_t)p;

        return data.status;
    }

    // pcem: scsi_cd.c:1615-1619
    private static uint8_t scsi_cd_get_sense_key(object p)
    {
        scsi_cd_data_t data = (scsi_cd_data_t)p;

        return (uint8_t)data.sense_key;
    }

    // pcem: scsi_cd.c:1621-1625
    private static int scsi_cd_get_bytes_required(object p)
    {
        scsi_cd_data_t data = (scsi_cd_data_t)p;

        return data.bytes_required - data.bytes_received;
    }

    // Un mot de 16 bits dans le tampon d'octets de l'IDE, petit-boutiste (Ide/ide.cs, IDE.SetW).
    private static void setw(uint8_t[] buffer, int i, int v)
    {
        buffer[i * 2] = (uint8_t)v;
        buffer[i * 2 + 1] = (uint8_t)(v >> 8);
    }

    // pcem: scsi_cd.c:1627-1649
    private static void scsi_cd_atapi_identify(uint8_t[] buffer, object p)
    {
        scsi_cd_data_t data = (scsi_cd_data_t)p;

        Array.Clear(buffer, 0, 512);

        setw(buffer, 0, 0x8000 | (5 << 8) | 0x80 | (2 << 5)); /* ATAPI device, CD-ROM drive, removable media, accelerated DRQ */
        ide.ide_padstr(buffer, 10 * 2, cd_models[cd_data!.cur_model].serial2_20, 20); /* Serial Number */
        ide.ide_padstr(buffer, 23 * 2, cd_models[cd_data.cur_model].firmware2_8, 8); /* Firmware */
        ide.ide_padstr(buffer, 27 * 2, cd_models[cd_data.cur_model].model2_40, 40);  /* Model */
        setw(buffer, 49, 0x300);                                                       /*DMA and LBA supported*/
        setw(buffer, 51, 120);
        setw(buffer, 52, 120);
        setw(buffer, 53, 2); /*Words 64-70 are valid*/
        setw(buffer, 62, 0x0000);
        setw(buffer, 63, 0x0007 | (0x100 << data.mdma_mode)); /*Multi-word DMA 0, 1 & 2*/
        setw(buffer, 64, 0x0003);                              /*PIO Modes 3 & 4*/
        if (data.pio_mode >= 3)
                setw(buffer, 64, 0x0003 | (0x100 << (data.pio_mode - 3)));
        setw(buffer, 65, 120);     /*Minimum multi-word cycle time*/
        setw(buffer, 66, 120);     /*Recommended multi-word cycle time*/
        setw(buffer, 67, 120);     /*Minimum PIO cycle time*/
        setw(buffer, 126, 0xfffe); /* Interpret zero byte count limit as maximum length */
    }

    // pcem: scsi_cd.c:1651-1685
    private static int scsi_cd_atapi_set_feature(uint8_t feature, uint8_t val, object p)
    {
        scsi_cd_data_t data = (scsi_cd_data_t)p;

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
                        if ((val & 7) > 4)
                                return 0;
                        data.pio_mode = (val & 7);
                        return 1;
                }
                else if ((val & 0xf8) == 0x20)
                {
                        /*Multi-word DMA transfer mode*/
                        if ((val & 7) > 2)
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

    // pcem: scsi_cd.c:1687-1707
    internal static readonly scsi_device_t scsi_cd = new(scsi_cd_init, scsi_cd_atapi_init, scsi_cd_close, scsi_cd_reset,
                                                         scsi_cd_start_command, scsi_cd_command,
                                                         scsi_cd_get_status, scsi_cd_get_sense_key,
                                                         scsi_cd_get_bytes_required,
                                                         scsi_cd_atapi_identify, scsi_cd_atapi_set_feature,
                                                         scsi_cd_read, scsi_cd_write, scsi_cd_read_complete,
                                                         scsi_cd_write_complete);
}
