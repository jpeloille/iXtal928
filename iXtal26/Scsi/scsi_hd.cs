// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/scsi/scsi_hd.c  (scsi_hd.h : includes/private/scsi/scsi_hd.h)
// STATUS: deviated — G11.0 (PLAN-G11.md) : le disque dur SCSI de PCem, entier, sur le bus des cartes SCSI
//         (scsi_bus_init, scsi.c:304-324).
//
// DEVIATION (forme, décision n° 9 de PLAN-G11.md) : data_in et data_out, contigus dans le struct C
//   (scsi_hd.c:31-32), sont UN tableau `io` de 2 × BUFFER_SIZE — data_in en io[0..], data_out en
//   io[BUFFER_SIZE..]. En C, un READ(10) de 513 à 1 024 secteurs déborde de data_in dans data_out sans
//   planter, et l'invité reçoit les bonnes données : ce régime est reproduit.
// DEVIATIONS (R9, PB-128) : au-delà de 2 × BUFFER_SIZE pour data_in (:87, :717), au-delà de BUFFER_SIZE
//   pour data_out (:723-728, :99-102), l'octet est compté, pas gardé, et se relit nul.
// Reproduits : PB-129 à PB-133 (PCEM_BUGS.md) ; les huit fatal() inatteignables de PB-134.

// CS8981 : `scsi_hd_c` porte le nom de l'unité C, que le membre `scsi_hd` garde (TRANSCRIPTION.md).
#pragma warning disable CS8981

using iXtal26.Diag;
using iXtal26.Disc;
using static iXtal26.Scsi.scsi;
using static iXtal26.timer;

namespace iXtal26.Scsi;

// pcem: scsi_hd.c:19-46 — classe : le bus en garde l'adresse (device_data, timer_add).
internal sealed class scsi_hd_data
{
    internal int blocks;

    internal int cmd_pos, new_cmd_pos;

    internal int addr, len;
    internal int sector_pos;

    internal uint8_t[] buf = new uint8_t[512];

    internal uint8_t status;

    // pcem bug, reproduced: PB-128 — jusqu'à 2 × 256 Ko, data_in déborde dans data_out, comme dans le struct C.
    // data_in[BUFFER_SIZE] puis data_out[BUFFER_SIZE] : voir l'en-tête.
    internal uint8_t[] io = new uint8_t[2 * scsi_hd_c.BUFFER_SIZE];
    internal int data_pos_read, data_pos_write;

    internal int bytes_received, bytes_required;

    internal hdd_file_t hdd = new();

    internal int sense_key, asc, ascq;

    internal int hd_id;

    internal pc_timer_t callback_timer = new();

    internal scsi_bus_t? bus;
}

internal static class scsi_hd_c
{
    // pcem: scsi_hd.c:13-17
    internal const int BUFFER_SIZE = 256 * 1024;

    // Une propriété, comme la macro : TIMER_USEC suit la vitesse de la machine amorcée (setpitclock).
    private static uint64_t RW_DELAY => TIMER_USEC * 500;

    private const int CMD_POS_IDLE = 0, CMD_POS_WAIT = 1, CMD_POS_START_SECTOR = 2, CMD_POS_TRANSFER = 3;

    // pcem: scsi_hd.c:48-55
    private static void scsi_hd_callback(object? p)
    {
        scsi_hd_data data = (scsi_hd_data)p!;

        if (data.cmd_pos == CMD_POS_WAIT)
        {
                data.cmd_pos = data.new_cmd_pos;
                scsi_bus_kick(data.bus!);
        }
    }

    // pcem: scsi_hd.c:57-73 — free(data) : le GC.
    private static object? scsi_hd_init(scsi_bus_t bus, int id)
    {
        scsi_hd_data data = new();

        hdd_file.hdd_load(data.hdd, id, hdd_c.ide_fn[id]);

        if (data.hdd.f is null)
                return null;

        data.hd_id = id;
        data.bus = bus;
        timer_add(data.callback_timer, scsi_hd_callback, data, 0);

        return data;
    }

    // pcem: scsi_hd.c:75-80 — jamais appelée : scsi_bus_close ne ferme rien (PB-121).
    private static void scsi_hd_close(object p)
    {
        scsi_hd_data data = (scsi_hd_data)p;

        hdd_file.hdd_close(data.hdd);
    }

    // pcem: scsi_hd.c:82-95
    private static int scsi_add_data(uint8_t val, scsi_hd_data data)
    {
        // pcem bug, not reproduced: PB-128 — data_in[data_pos_write++] sans borne (:87) : au-delà des deux
        //   tampons, les champs de contrôle du struct, puis le tas.
        // DEVIATION: au-delà de 2 × BUFFER_SIZE, l'octet est compté, pas gardé (il se relit nul, scsi_hd_read).
        if (data.data_pos_write >= 2 * BUFFER_SIZE)
        {
                R9.Garde("scsi_hd.c:87");
                data.data_pos_write++;
                return 0;
        }
        data.io[data.data_pos_write++] = val;

        return 0;
    }

    // pcem: scsi_hd.c:97-105
    private static int scsi_get_data(scsi_hd_data data)
    {
        // pcem bug, not reproduced: PB-128 — data_out[data_pos_read++] lu, puis
        //   pc.fatal("scsi_get_data beyond buffer limits\n") (:101-102).
        // DEVIATION: au-delà du tampon, l'octet se lit nul, la position avance.
        if (data.data_pos_read >= BUFFER_SIZE)
        {
                R9.Garde("scsi_hd.c:101");
                data.data_pos_read++;
                return 0;
        }
        uint8_t val = data.io[BUFFER_SIZE + data.data_pos_read++];

        return val;
    }

    // pcem: scsi_hd.c:107-112
    // pcem bug, reproduced: PB-131 — tout refus porte 05/25h (LUN non supporté), un code inconnu compris.
    private static void scsi_hd_illegal(scsi_hd_data data)
    {
        data.status = STATUS_CHECK_CONDITION;
        data.sense_key = KEY_ILLEGAL_REQ;
        data.asc = ASC_INVALID_LUN;
        data.ascq = 0;
    }

    // pcem: scsi_hd.c:114-117 — la macro add_data_len(v), sur i et len de scsi_hd_command. i avance même
    //   au-delà de len : le bourrage de MODE SENSE (:410-412) en dépend (PB-130).
    private static void add_data_len(scsi_hd_data data, ref int i, int len, int v)
    {
        if (i < len)
                scsi_add_data((uint8_t)v, data);
        i++;
    }

    // pcem: scsi_hd.c:119-712
    private static int scsi_hd_command(uint8_t[] cdb, object p)
    {
        scsi_hd_data data = (scsi_hd_data)p;
        int /*addr, */ len;
        int i = 0;
        int desc;
        int bus_state = 0;

        data.status = STATUS_GOOD;
        data.data_pos_read = data.data_pos_write = 0;

        // pcem bug, reproduced: PB-133 — le LUN lu dans cdb[1] seulement : celui du CCB n'arrive jamais (ni IDENTIFY).
        if ((cdb[0] != SCSI_REQUEST_SENSE /* && cdb[0] != SCSI_INQUIRY*/) && (cdb[1] & 0xe0) != 0)
        {
                /*Non-zero LUN - abort command*/
                scsi_hd_illegal(data);
                bus_state = BUS_CD | BUS_IO;
                return bus_state;
        }

        switch (cdb[0])
        {
        case SCSI_TEST_UNIT_READY:
                bus_state = BUS_CD | BUS_IO;
                break;

        case SCSI_REZERO_UNIT:
                bus_state = BUS_CD | BUS_IO;
                break;

        case SCSI_REQUEST_SENSE:
                // pcem bug, reproduced: PB-131 — 18 octets à longueur additionnelle 0, sans bit Valid ; le format
                //   descripteur sur cdb[1] bit 0 ; effacé par REQUEST SENSE seul, il survit aux autres commandes.
                desc = cdb[1] & 1;
                // pcem bug, reproduced: PB-132 — une allocation nulle entre quand même en DATA IN, et n'en sort plus.
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
                // pcem bug, reproduced: PB-132 — une allocation nulle entre quand même en DATA IN, et n'en sort plus.
                len = cdb[4] | (cdb[3] << 8);

                // pcem bug, reproduced: PB-219 — branche morte, le test du LUN plus haut refusant avant ;
                //   et non conforme : un qualificatif 011b exige le type 1Fh, donc 7Fh, et non 60h.
                if ((cdb[1] & 0xe0) != 0)
                {
                        add_data_len(data, ref i, len, 0 | (3 << 5)); /*No physical device on this LUN*/
                }
                else
                {
                        add_data_len(data, ref i, len, 0 | (0 << 5)); /*Hard disc*/
                }
                // pcem bug, reproduced: PB-131 — 96 octets, version 0, longueur additionnelle 0, CmdQue annoncé
                //   (octet 7), EVPD ignoré.
                add_data_len(data, ref i, len, 0); /*Not removeable*/
                add_data_len(data, ref i, len, 0); /*No version*/
                add_data_len(data, ref i, len, 2); /*Response data*/
                add_data_len(data, ref i, len, 0); /*Additional length*/
                add_data_len(data, ref i, len, 0);
                add_data_len(data, ref i, len, 0);
                add_data_len(data, ref i, len, 2);

                add_data_len(data, ref i, len, 'P');
                add_data_len(data, ref i, len, 'C');
                add_data_len(data, ref i, len, 'e');
                add_data_len(data, ref i, len, 'm');
                add_data_len(data, ref i, len, ' ');
                add_data_len(data, ref i, len, ' ');
                add_data_len(data, ref i, len, ' ');
                add_data_len(data, ref i, len, ' ');

                add_data_len(data, ref i, len, 'S');
                add_data_len(data, ref i, len, 'C');
                add_data_len(data, ref i, len, 'S');
                add_data_len(data, ref i, len, 'I');
                add_data_len(data, ref i, len, '_');
                add_data_len(data, ref i, len, 'H');
                add_data_len(data, ref i, len, 'D');
                add_data_len(data, ref i, len, ' ');

                add_data_len(data, ref i, len, ' ');
                add_data_len(data, ref i, len, ' ');
                add_data_len(data, ref i, len, ' ');
                add_data_len(data, ref i, len, ' ');
                add_data_len(data, ref i, len, ' ');
                add_data_len(data, ref i, len, ' ');
                add_data_len(data, ref i, len, ' ');
                add_data_len(data, ref i, len, ' ');

                add_data_len(data, ref i, len, 0); /*Product revision level*/
                add_data_len(data, ref i, len, 0);
                add_data_len(data, ref i, len, 0);
                add_data_len(data, ref i, len, 0);

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
                // pcem bug, reproduced: PB-132 — une allocation nulle entre quand même en DATA IN, et n'en sort plus.
                len = cdb[4];

                // pcem bug, reproduced: PB-130 — en-tête 00 00 08 00 : 08h dans le paramètre propre, la longueur
                //   du descripteur à 0, puis huit octets de descripteur (sectors >> 24 en densité, 24 bits de blocs).
                add_data_len(data, ref i, len, 0);
                add_data_len(data, ref i, len, 0);

                add_data_len(data, ref i, len, 0x08);
                add_data_len(data, ref i, len, 0);

                add_data_len(data, ref i, len, (data.hdd.sectors >> 24) & 0xff);
                add_data_len(data, ref i, len, (data.hdd.sectors >> 16) & 0xff);
                add_data_len(data, ref i, len, (data.hdd.sectors >> 8) & 0xff);
                add_data_len(data, ref i, len, data.hdd.sectors & 0xff);
                add_data_len(data, ref i, len, (512 >> 24) & 0xff);
                add_data_len(data, ref i, len, (512 >> 16) & 0xff);
                add_data_len(data, ref i, len, (512 >> 8) & 0xff);
                add_data_len(data, ref i, len, 512 & 0xff);

                // pcem bug, reproduced: PB-130 — géométrie fixe (256 secteurs, 4 096 cylindres, 64 têtes),
                //   page 30h « PCEM » ; PC et DBD ignorés ; une page inconnue rend GOOD.
                if ((cdb[2] & 0x3f) == 0x03 || (cdb[2] & 0x3f) == 0x3f)
                {
                        add_data_len(data, ref i, len, 3);
                        add_data_len(data, ref i, len, 0x16);
                        add_data_len(data, ref i, len, 0);
                        add_data_len(data, ref i, len, 1); /*Tracks per zone*/
                        add_data_len(data, ref i, len, 0);
                        add_data_len(data, ref i, len, 1); /*Alternate sectors per zone*/
                        add_data_len(data, ref i, len, 0);
                        add_data_len(data, ref i, len, 1); /*Alternate tracks per zone*/
                        add_data_len(data, ref i, len, 0);
                        add_data_len(data, ref i, len, 1); /*Alternate tracks per volume*/
                        add_data_len(data, ref i, len, 1);
                        add_data_len(data, ref i, len, 0); /*Sectors per track*/
                        add_data_len(data, ref i, len, 2);
                        add_data_len(data, ref i, len, 0); /*Data bytes per physical sector*/
                        add_data_len(data, ref i, len, 0);
                        add_data_len(data, ref i, len, 0); /*Interleave*/
                        add_data_len(data, ref i, len, 0);
                        add_data_len(data, ref i, len, 0); /*Track skew*/
                        add_data_len(data, ref i, len, 0);
                        add_data_len(data, ref i, len, 0); /*Cylinder skew*/
                        add_data_len(data, ref i, len, 0); /*Drive type*/
                        add_data_len(data, ref i, len, 0);
                        add_data_len(data, ref i, len, 0);
                        add_data_len(data, ref i, len, 0);
                }

                if ((cdb[2] & 0x3f) == 0x04 || (cdb[2] & 0x3f) == 0x3f)
                {
                        add_data_len(data, ref i, len, 4);
                        add_data_len(data, ref i, len, 0x16);
                        add_data_len(data, ref i, len, 0);
                        add_data_len(data, ref i, len, 0x10);
                        add_data_len(data, ref i, len, 0);  /*Cylinder count*/
                        add_data_len(data, ref i, len, 64); /*Heads*/
                        add_data_len(data, ref i, len, 0);
                        add_data_len(data, ref i, len, 0);
                        add_data_len(data, ref i, len, 0); /*Write recomp*/
                        add_data_len(data, ref i, len, 0);
                        add_data_len(data, ref i, len, 0);
                        add_data_len(data, ref i, len, 0); /*Reduced write current*/
                        add_data_len(data, ref i, len, 0);
                        add_data_len(data, ref i, len, 0); /*Drive step rate*/
                        add_data_len(data, ref i, len, 0);
                        add_data_len(data, ref i, len, 0);
                        add_data_len(data, ref i, len, 0); /*Landing zone cylinder*/
                        add_data_len(data, ref i, len, 0); /*RPL*/
                        add_data_len(data, ref i, len, 0); /*Rotational offset*/
                        add_data_len(data, ref i, len, 0);
                        add_data_len(data, ref i, len, 0x10);
                        add_data_len(data, ref i, len, 0); /*Rotain rate*/
                        add_data_len(data, ref i, len, 0);
                        add_data_len(data, ref i, len, 0);
                }

                if ((cdb[2] & 0x3f) == 0x30 || (cdb[2] & 0x3f) == 0x3f)
                {
                        add_data_len(data, ref i, len, 0xb0);
                        add_data_len(data, ref i, len, 0x16);
                        add_data_len(data, ref i, len, 'P');
                        add_data_len(data, ref i, len, 'C');
                        add_data_len(data, ref i, len, 'E');
                        add_data_len(data, ref i, len, 'M');
                        add_data_len(data, ref i, len, ' ');
                        add_data_len(data, ref i, len, ' ');
                        add_data_len(data, ref i, len, ' ');
                        add_data_len(data, ref i, len, ' ');
                        add_data_len(data, ref i, len, ' ');
                        add_data_len(data, ref i, len, ' ');
                        add_data_len(data, ref i, len, ' ');
                        add_data_len(data, ref i, len, ' ');
                        add_data_len(data, ref i, len, ' ');
                        add_data_len(data, ref i, len, ' ');
                        add_data_len(data, ref i, len, ' ');
                        add_data_len(data, ref i, len, ' ');
                        add_data_len(data, ref i, len, ' ');
                        add_data_len(data, ref i, len, ' ');
                        add_data_len(data, ref i, len, ' ');
                        add_data_len(data, ref i, len, ' ');
                        add_data_len(data, ref i, len, ' ');
                        add_data_len(data, ref i, len, ' ');
                }

                // pcem bug, reproduced: PB-130 — len décroît pendant que i monte : min(i0, L) + ⌈(L − i0)/2⌉
                //   octets au lieu de L.
                for (; len >= 0; len--)
                {
                        add_data_len(data, ref i, len, 0);
                }

                len = data.data_pos_write;
                if (cdb[0] == SCSI_MODE_SENSE_6)
                {
                        data.io[0] = (uint8_t)(len - 1);
                }
                else
                {
                        data.io[0] = (uint8_t)((len - 2) >> 8);
                        data.io[1] = (uint8_t)((len - 2) & 255);
                }
                data.cmd_pos = CMD_POS_IDLE;
                bus_state = BUS_IO;
                break;

        case SCSI_READ_CAPACITY_10:
                // pcem bug, reproduced: PB-129 — le nombre de blocs, non l'adresse du dernier : un secteur de trop.
                scsi_add_data((uint8_t)((data.hdd.sectors >> 24) & 0xff), data);
                scsi_add_data((uint8_t)((data.hdd.sectors >> 16) & 0xff), data);
                scsi_add_data((uint8_t)((data.hdd.sectors >> 8) & 0xff), data);
                scsi_add_data((uint8_t)(data.hdd.sectors & 0xff), data);
                scsi_add_data((uint8_t)((512 >> 24) & 0xff), data);
                scsi_add_data((uint8_t)((512 >> 16) & 0xff), data);
                scsi_add_data((uint8_t)((512 >> 8) & 0xff), data);
                scsi_add_data((uint8_t)(512 & 0xff), data);
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
                        break;
                }
                while (data.len != 0)
                {
                        if (data.cmd_pos == CMD_POS_START_SECTOR)
                        {
                                // pcem bug, reproduced: PB-129 — au-delà de la fin, rien n'est lu : buf périmé, GOOD.
                                hdd_file.hdd_read_sectors(data.hdd, data.addr, 1, data.buf);
                                pc.readflash_set(pc.READFLASH_HDC, data.hd_id);
                        }

                        data.cmd_pos = CMD_POS_TRANSFER;
                        for (; data.sector_pos < 512; data.sector_pos++)
                        {
                                int ret = scsi_add_data(data.buf[data.sector_pos], data);

                                // pcem bug, reproduced: PB-134 — scsi_add_data rend 0 : jamais ces deux fatal().
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
                        // pcem bug, reproduced: PB-132 — un compte nul entre quand même en DATA IN, et n'en sort plus.
                        data.len = cdb[8] | (cdb[7] << 8);

                        data.cmd_pos = CMD_POS_WAIT;
                        timer_set_delay_u64(data.callback_timer, RW_DELAY);
                        data.new_cmd_pos = CMD_POS_START_SECTOR;
                        data.sector_pos = 0;

                        bus_state = BUS_CD;
                        break;
                }
                while (data.len != 0)
                {
                        if (data.cmd_pos == CMD_POS_START_SECTOR)
                        {
                                // pcem bug, reproduced: PB-129 — au-delà de la fin, rien n'est lu : buf périmé, GOOD.
                                hdd_file.hdd_read_sectors(data.hdd, data.addr, 1, data.buf);
                                pc.readflash_set(pc.READFLASH_HDC, data.hd_id);
                        }
                        data.cmd_pos = CMD_POS_TRANSFER;
                        for (; data.sector_pos < 512; data.sector_pos++)
                        {
                                int ret = scsi_add_data(data.buf[data.sector_pos], data);
                                // pcem bug, reproduced: PB-134 — scsi_add_data rend 0 : jamais ces deux fatal().
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
                                // pcem bug, reproduced: PB-134 — scsi_get_data rend un octet : jamais ces deux fatal().
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

                        // pcem bug, reproduced: PB-129 — au-delà de la fin, rien n'est écrit, et GOOD.
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
                                // pcem bug, reproduced: PB-134 — scsi_get_data rend un octet : jamais ces deux fatal().
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

                        // pcem bug, reproduced: PB-129 — au-delà de la fin, rien n'est écrit, et GOOD.
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
                // pcem bug, reproduced: PB-131 — VERIFY simulé : rien n'est lu ni comparé, aucun LBA n'est vérifié.
                data.cmd_pos = CMD_POS_IDLE;
                bus_state = BUS_CD | BUS_IO;
                break;

        case SCSI_MODE_SELECT_6:
                // pcem bug, reproduced: PB-131 — MODE SELECT simulé : les pages reçues sont jetées.
                // pcem bug, reproduced: PB-132 — une longueur nulle entre quand même en DATA OUT, et n'en sort plus.
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
                // pcem bug, reproduced: PB-131 — FORMAT UNIT simulé : instantané, rien n'est effacé.
                bus_state = BUS_CD | BUS_IO;
                break;

        case SCSI_START_STOP_UNIT:
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
                        scsi_hd_illegal(data);
                        bus_state = BUS_CD | BUS_IO;
                }
                break;

        default:
                // omitted: pclog("Bad SCSI HD command %02x\n") (:705) — sortie pure.
                scsi_hd_illegal(data);
                bus_state = BUS_CD | BUS_IO;
                break;
        }

        return bus_state;    }

    // pcem: scsi_hd.c:714-718
    private static uint8_t scsi_hd_read(object p)
    {
        scsi_hd_data data = (scsi_hd_data)p;

        // pcem bug, not reproduced: PB-128 — data_in[data_pos_read++] sans borne (:717) : une phase DATA IN vide
        //   (READ(10) de compte 0, allocation 0) ne finit jamais, et la carte lit au-delà du struct.
        // DEVIATION: au-delà de 2 × BUFFER_SIZE, l'octet se relit nul, la position avance.
        if (data.data_pos_read >= 2 * BUFFER_SIZE)
        {
                R9.Garde("scsi_hd.c:717");
                data.data_pos_read++;
                return 0;
        }
        return data.io[data.data_pos_read++];
    }

    // pcem: scsi_hd.c:720-729
    private static void scsi_hd_write(uint8_t val, object p)
    {
        scsi_hd_data data = (scsi_hd_data)p;

        // pcem bug, not reproduced: PB-128 — data_out[data_pos_write++] puis
        //   pc.fatal("Exceeded data_out buffer size\n") (:723-728).
        // DEVIATION: au-delà du tampon, l'octet est compté, pas gardé.
        if (data.data_pos_write >= BUFFER_SIZE)
        {
                R9.Garde("scsi_hd.c:728");
                data.data_pos_write++;
                data.bytes_received++;
                return;
        }
        data.io[BUFFER_SIZE + data.data_pos_write++] = val;

        data.bytes_received++;
    }

    // pcem: scsi_hd.c:731-735
    // pcem bug, reproduced: PB-132 — une phase DATA IN vide : l'octet lu à l'entrée (scsi.c:241) désaccorde
    //   les deux positions, et la cible ne quitte plus la phase.
    private static int scsi_hd_read_complete(object p)
    {
        scsi_hd_data data = (scsi_hd_data)p;

        return (data.data_pos_read == data.data_pos_write) ? 1 : 0;
    }

    // pcem: scsi_hd.c:736-740
    private static int scsi_hd_write_complete(object p)
    {
        scsi_hd_data data = (scsi_hd_data)p;

        return (data.bytes_received == data.bytes_required) ? 1 : 0;
    }

    // pcem: scsi_hd.c:742-748
    private static void scsi_hd_start_command(object p)
    {
        scsi_hd_data data = (scsi_hd_data)p;

        data.bytes_received = 0;
        data.bytes_required = 0;
        data.data_pos_read = data.data_pos_write = 0;
    }

    // pcem: scsi_hd.c:750-754
    private static uint8_t scsi_hd_get_status(object p)
    {
        scsi_hd_data data = (scsi_hd_data)p;

        return data.status;
    }

    // pcem: scsi_hd.c:756-760
    private static uint8_t scsi_hd_get_sense_key(object p)
    {
        scsi_hd_data data = (scsi_hd_data)p;

        return (uint8_t)data.sense_key;
    }

    // pcem: scsi_hd.c:762-766
    private static int scsi_hd_get_bytes_required(object p)
    {
        scsi_hd_data data = (scsi_hd_data)p;

        return data.bytes_required - data.bytes_received;
    }

    // pcem: scsi_hd.c:768-788
    internal static readonly scsi_device_t scsi_hd = new(scsi_hd_init, null, scsi_hd_close, null,
                                                          scsi_hd_start_command, scsi_hd_command,
                                                          scsi_hd_get_status, scsi_hd_get_sense_key,
                                                          scsi_hd_get_bytes_required,
                                                          null, null,
                                                          scsi_hd_read, scsi_hd_write,
                                                          scsi_hd_read_complete, scsi_hd_write_complete);
}
