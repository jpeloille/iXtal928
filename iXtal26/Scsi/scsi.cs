// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/scsi/scsi.c  (scsi.h : includes/private/scsi/scsi.h)
// STATUS: partial — G10.4 (PLAN-G10.md) : le bus SCSI que le pont ATAPI fait parler (scsi_bus_update,
//         scsi_bus_read, scsi_bus_match, scsi_bus_kick, scsi_bus_atapi_init, scsi_bus_reset), et les
//         types et constantes de scsi.h. G11.0 (PLAN-G11.md) : le bus des cartes SCSI, scsi_bus_init et
//         scsi_bus_close.
//
// DEVIATION (R9, PB-113) : les deux fatal() du bus (:85, :264) le remettent au repos (scsi_bus_reset) ;
//   le pont voit alors BSY tomber et abandonne la commande (Ide/ide_atapi.cs, atapi_abort).

// CS8981 : `scsi` n'a que des minuscules ASCII — le nom de l'unité C, comme ide.cs.
#pragma warning disable CS8981

using System.Runtime.CompilerServices;
using iXtal26.Diag;
using iXtal26.Ide;

namespace iXtal26.Scsi;

// pcem: scsi.h:7-25
internal delegate object? scsi_init_fn(scsi_bus_t bus, int id);
internal delegate object? scsi_atapi_init_fn(scsi_bus_t bus, int id, atapi_device_t atapi_dev);
internal delegate void scsi_close_fn(object p);
internal delegate void scsi_reset_fn(object p);
internal delegate void scsi_start_command_fn(object p);
internal delegate int scsi_command_fn(uint8_t[] cdb, object p);
internal delegate uint8_t scsi_get_status_fn(object p);
internal delegate uint8_t scsi_get_sense_key_fn(object p);
internal delegate int scsi_get_bytes_required_fn(object p);
internal delegate void scsi_atapi_identify_fn(uint8_t[] buffer, object p);
internal delegate int scsi_atapi_set_feature_fn(uint8_t feature, uint8_t val, object p);
internal delegate uint8_t scsi_read_fn(object p);
internal delegate void scsi_write_fn(uint8_t val, object p);
internal delegate int scsi_read_complete_fn(object p);
internal delegate int scsi_write_complete_fn(object p);

// pcem: scsi.h:7-25 — `uint16_t *buffer` d'atapi_identify : le tampon d'octets de l'IDE (Ide/ide.cs, IDE.buffer).
// Les quatre entrées nullables sont celles que scsi_hd laisse à NULL (scsi_hd.c:768-788).
internal sealed class scsi_device_t
{
    internal readonly scsi_init_fn init;
    internal readonly scsi_atapi_init_fn? atapi_init;
    internal readonly scsi_close_fn close;
    internal readonly scsi_reset_fn? reset;

    internal readonly scsi_start_command_fn start_command;
    internal readonly scsi_command_fn command;

    internal readonly scsi_get_status_fn get_status;
    internal readonly scsi_get_sense_key_fn get_sense_key;
    internal readonly scsi_get_bytes_required_fn get_bytes_required;

    internal readonly scsi_atapi_identify_fn? atapi_identify;
    internal readonly scsi_atapi_set_feature_fn? atapi_set_feature;

    internal readonly scsi_read_fn read;
    internal readonly scsi_write_fn write;
    internal readonly scsi_read_complete_fn read_complete;
    internal readonly scsi_write_complete_fn write_complete;

    // Dans l'ordre de l'initialiseur C (scsi_cd.c:1687-1707).
    internal scsi_device_t(scsi_init_fn init, scsi_atapi_init_fn? atapi_init, scsi_close_fn close, scsi_reset_fn? reset,
                           scsi_start_command_fn start_command, scsi_command_fn command,
                           scsi_get_status_fn get_status, scsi_get_sense_key_fn get_sense_key,
                           scsi_get_bytes_required_fn get_bytes_required, scsi_atapi_identify_fn? atapi_identify,
                           scsi_atapi_set_feature_fn? atapi_set_feature, scsi_read_fn read, scsi_write_fn write,
                           scsi_read_complete_fn read_complete, scsi_write_complete_fn write_complete)
    {
        this.init = init;
        this.atapi_init = atapi_init;
        this.close = close;
        this.reset = reset;
        this.start_command = start_command;
        this.command = command;
        this.get_status = get_status;
        this.get_sense_key = get_sense_key;
        this.get_bytes_required = get_bytes_required;
        this.atapi_identify = atapi_identify;
        this.atapi_set_feature = atapi_set_feature;
        this.read = read;
        this.write = write;
        this.read_complete = read_complete;
        this.write_complete = write_complete;
    }
}

// pcem: scsi.h:29-47
internal sealed class scsi_bus_t
{
    internal int state;
    internal int new_state;
    internal int clear_req;
    internal uint32_t bus_in, bus_out;
    internal int dev_id;

    internal int command_pos;
    internal uint8_t[] command = new uint8_t[scsi.CDB_MAX_LEN];

    internal scsi_device_t?[] devices = new scsi_device_t?[8];
    internal object?[] device_data = new object?[8];

    internal int change_state_delay;
    internal int new_req_delay;

    internal int is_atapi;
}

internal static class scsi
{
    // pcem: scsi.h:27
    internal const int CDB_MAX_LEN = 20;

    // pcem: scsi.h:54-77
    internal const uint8_t SCSI_TEST_UNIT_READY = 0x00;
    internal const uint8_t SCSI_REZERO_UNIT = 0x01;
    internal const uint8_t SCSI_REQUEST_SENSE = 0x03;
    internal const uint8_t SCSI_FORMAT = 0x04;
    internal const uint8_t SCSI_READ_6 = 0x08;
    internal const uint8_t SCSI_WRITE_6 = 0x0a;
    internal const uint8_t SCSI_SEEK_6 = 0x0b;
    internal const uint8_t SCSI_INQUIRY = 0x12;
    internal const uint8_t SCSI_MODE_SELECT_6 = 0x15;
    internal const uint8_t SCSI_MODE_SENSE_6 = 0x1a;
    internal const uint8_t SCSI_START_STOP_UNIT = 0x1b;
    internal const uint8_t SCSI_PREVENT_ALLOW_MEDIUM_REMOVAL = 0x1e;
    internal const uint8_t SCSI_READ_CAPACITY_10 = 0x25;
    internal const uint8_t SCSI_READ_10 = 0x28;
    internal const uint8_t SCSI_WRITE_10 = 0x2a;
    internal const uint8_t SCSI_SEEK_10 = 0x2b;
    internal const uint8_t SCSI_WRITE_AND_VERIFY = 0x2e;
    internal const uint8_t SCSI_VERIFY_10 = 0x2f;
    internal const uint8_t SCSI_READ_BUFFER = 0x3c;
    internal const uint8_t SCSI_MODE_SENSE_10 = 0x5a;

    internal const uint8_t SCSI_RESERVE = 0x16;
    internal const uint8_t SCSI_RELEASE = 0x17;
    internal const uint8_t SEND_DIAGNOSTIC = 0x1d;

    internal const uint8_t STATUS_GOOD = 0x00;
    internal const uint8_t STATUS_CHECK_CONDITION = 0x02;

    internal const uint8_t MSG_COMMAND_COMPLETE = 0x00;

    // pcem: scsi.h:85-101 — les lignes du bus. BUS_ATN et BUS_ACK ont la même valeur, comme chez PCem.
    internal const int BUS_DBP = 0x01;
    internal const int BUS_SEL = 0x02;
    internal const int BUS_IO = 0x04;
    internal const int BUS_CD = 0x08;
    internal const int BUS_MSG = 0x10;
    internal const int BUS_REQ = 0x20;
    internal const int BUS_BSY = 0x40;
    internal const int BUS_RST = 0x80;
    internal const int BUS_ACK = 0x200;
    internal const int BUS_ATN = 0x200;
    internal const int BUS_ARB = 0x8000;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int BUS_SETDATA(int val) => (int)((uint32_t)val << 16);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int BUS_GETDATA(int val) => (val >> 16) & 0xff;
    internal const int BUS_DATAMASK = 0xff0000;

    internal const int BUS_IDLE = 1 << 31;

    // pcem: scsi.h:113-129
    internal const int KEY_NONE = 0;
    internal const int KEY_NOT_READY = 2;
    internal const int KEY_ILLEGAL_REQ = 5;
    internal const int KEY_UNIT_ATTENTION = 6;
    internal const int KEY_DATA_PROTECT = 7;

    internal const int ASC_AUDIO_PLAY_OPERATION = 0x00;
    internal const int ASC_ILLEGAL_OPCODE = 0x20;
    internal const int ASC_LBA_OUT_OF_RANGE = 0x21;
    internal const int ASC_INV_FIELD_IN_CMD_PACKET = 0x24;
    internal const int ASC_INVALID_LUN = 0x25;
    internal const int ASC_WRITE_PROTECT = 0x27;
    internal const int ASC_MEDIUM_MAY_HAVE_CHANGED = 0x28;
    internal const int ASC_MEDIUM_NOT_PRESENT = 0x3a;
    internal const int ASC_DATA_PHASE_ERROR = 0x4b;
    internal const int ASC_ILLEGAL_MODE_FOR_THIS_TRACK = 0x64;

    // pcem: scsi.h:131-136
    internal const int SCSI_PHASE_DATA_OUT = 0;
    internal const int SCSI_PHASE_DATA_IN = BUS_IO;
    internal const int SCSI_PHASE_COMMAND = BUS_CD;
    internal const int SCSI_PHASE_STATUS = BUS_CD | BUS_IO;
    internal const int SCSI_PHASE_MESSAGE_OUT = BUS_MSG | BUS_CD;
    internal const int SCSI_PHASE_MESSAGE_IN = BUS_MSG | BUS_CD | BUS_IO;

    // pcem: scsi.h:138-139
    internal const int START_STOP_START = 0x01;
    internal const int START_STOP_LOEJ = 0x02;

    // pcem: scsi.c:9-16 — internes : la porte r9-atapi forge des états du bus.
    internal const int STATE_IDLE = 0;
    internal const int STATE_COMMAND = 1;
    internal const int STATE_COMMANDWAIT = 2;
    internal const int STATE_DATAIN = 3;
    internal const int STATE_DATAOUT = 4;
    internal const int STATE_STATUS = 5;
    internal const int STATE_MESSAGEIN = 6;
    internal const int STATE_PHASESEL = 7;

    // pcem: scsi.c:18-19
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SET_BUS_STATE(scsi_bus_t bus, int state)
    {
        bus.bus_out = (bus.bus_out & ~(uint32_t)(BUS_CD | BUS_IO | BUS_MSG)) | (uint32_t)(state & (BUS_CD | BUS_IO | BUS_MSG));
    }

    // pcem: scsi.c:21
    private static readonly int[] cmd_len = [6, 10, 10, 6, 16, 12, 6, 6];

    // pcem: scsi.c:23-32
    private static int get_dev_id(uint8_t data)
    {
        int c;

        for (c = 0; c < 8; c++)
        {
                if ((data & (1 << c)) != 0)
                        return c;
        }

        return -1;
    }

    // pcem: scsi.c:34-206
    internal static int scsi_bus_update(scsi_bus_t bus, int bus_assert)
    {
        scsi_device_t? dev = null;
        object? dev_data = null;

        if ((bus_assert & BUS_ARB) != 0)
                bus.state = STATE_IDLE;

        if (bus.dev_id != -1)
        {
                dev = bus.devices[bus.dev_id];
                dev_data = bus.device_data[bus.dev_id];
        }

        switch (bus.state)
        {
        case STATE_IDLE:
                bus.clear_req = bus.change_state_delay = bus.new_req_delay = 0;
                if ((bus_assert & BUS_SEL) != 0 && (bus_assert & BUS_BSY) == 0)
                {
                        uint8_t sel_data = (uint8_t)BUS_GETDATA(bus_assert);

                        bus.dev_id = get_dev_id(sel_data);
                        if (bus.dev_id != -1 && bus.devices[bus.dev_id] != null)
                        {
                                bus.bus_out |= BUS_BSY;
                                bus.state = STATE_PHASESEL;
                        }
                        break;
                }
                break;
        case STATE_PHASESEL:
                if ((bus_assert & BUS_SEL) == 0)
                {
                        if ((bus_assert & BUS_ATN) == 0)
                        {
                                if (bus.dev_id != -1 && bus.devices[bus.dev_id] != null)
                                {
                                        bus.state = STATE_COMMAND;
                                        bus.bus_out = BUS_BSY | BUS_REQ;
                                        bus.command_pos = 0;
                                        SET_BUS_STATE(bus, BUS_CD);
                                }
                                else
                                {
                                        bus.state = STATE_IDLE;
                                        bus.bus_out = 0;
                                }
                        }
                        else
                        {
                                // pcem bug, not reproduced: PB-113 — fatal("dropped sel %x\n") (:85).
                                // DEVIATION: le bus au repos ; le pont voit BSY tomber et abandonne.
                                R9.Garde("scsi.c:85");
                                scsi_bus_reset(bus);
                        }
                }
                break;
        case STATE_COMMAND:
                if ((bus_assert & BUS_ACK) != 0 && (bus.bus_in & BUS_ACK) == 0)
                {
                        bus.command[bus.command_pos++] = (uint8_t)BUS_GETDATA(bus_assert);
                        bus.clear_req = 3;
                        bus.new_state = (int)(bus.bus_out & (BUS_IO | BUS_CD | BUS_MSG));
                        bus.bus_out &= ~(uint32_t)BUS_REQ;

                        if (bus.command_pos == (bus.is_atapi != 0 ? 12 : cmd_len[bus.command[0] >> 5]))
                        {
                                int new_state;

                                dev!.start_command(dev_data!);
                                new_state = dev.command(bus.command, dev_data!);
                                if ((new_state & (BUS_IO | BUS_CD | BUS_MSG)) == BUS_CD)
                                {
                                        bus.state = STATE_COMMANDWAIT;
                                        bus.clear_req = 0;
                                }
                                else
                                {
                                        bus.new_state = new_state;
                                        bus.change_state_delay = 4;
                                }
                        }
                }
                break;

        case STATE_COMMANDWAIT:
        {
                int new_state;

                new_state = dev!.command(bus.command, dev_data!);
                if ((new_state & (BUS_IO | BUS_CD | BUS_MSG)) != BUS_CD)
                {
                        bus.new_state = new_state;
                        bus.change_state_delay = 4;
                        bus.clear_req = 4;
                }
        }
                break;

        case STATE_DATAIN:
                if ((bus_assert & BUS_ACK) != 0 && (bus.bus_in & BUS_ACK) == 0)
                {
                        if (dev!.read_complete(dev_data!) != 0)
                        {
                                bus.bus_out &= ~(uint32_t)BUS_REQ;
                                bus.new_state = BUS_CD | BUS_IO;
                                bus.change_state_delay = 4;
                                bus.new_req_delay = 8;
                        }
                        else
                        {
                                uint8_t val = dev.read(dev_data!);

                                bus.bus_out = (bus.bus_out & ~(uint32_t)BUS_DATAMASK) | (uint32_t)(BUS_SETDATA(val) | BUS_DBP | BUS_REQ);
                                bus.clear_req = 3;
                                bus.bus_out &= ~(uint32_t)BUS_REQ;
                                bus.new_state = BUS_IO;
                        }
                }
                break;

        case STATE_DATAOUT:
                if ((bus_assert & BUS_ACK) != 0 && (bus.bus_in & BUS_ACK) == 0)
                {
                        dev!.write((uint8_t)BUS_GETDATA(bus_assert), dev_data!);

                        if (dev.write_complete(dev_data!) != 0)
                        {
                                int new_state;
                                bus.bus_out &= ~(uint32_t)BUS_REQ;
                                new_state = dev.command(bus.command, dev_data!);

                                bus.new_state = new_state;
                                bus.change_state_delay = 4;
                                bus.new_req_delay = 8;
                        }
                        else
                        {
                                bus.bus_out |= BUS_REQ;
                        }
                }
                break;

        case STATE_STATUS:
                if ((bus_assert & BUS_ACK) != 0 && (bus.bus_in & BUS_ACK) == 0)
                {
                        bus.bus_out &= ~(uint32_t)BUS_REQ;
                        bus.new_state = BUS_CD | BUS_IO | BUS_MSG;
                        bus.change_state_delay = 4;
                        bus.new_req_delay = 8;
                }
                break;

        case STATE_MESSAGEIN:
                if ((bus_assert & BUS_ACK) != 0 && (bus.bus_in & BUS_ACK) == 0)
                {
                        bus.bus_out &= ~(uint32_t)BUS_REQ;
                        bus.new_state = BUS_IDLE;
                        bus.change_state_delay = 4;
                }
                break;
        }
        bus.bus_in = (uint32_t)bus_assert;

        return (int)(bus.bus_out | bus.bus_in);
    }

    // pcem: scsi.c:208-278
    internal static int scsi_bus_read(scsi_bus_t bus)
    {
        scsi_device_t? dev = null;
        object? dev_data = null;

        if (bus.dev_id != -1)
        {
                dev = bus.devices[bus.dev_id];
                dev_data = bus.device_data[bus.dev_id];
        }

        if (bus.clear_req != 0)
        {
                bus.clear_req--;
                if (bus.clear_req == 0)
                {
                        SET_BUS_STATE(bus, bus.new_state);
                        bus.bus_out |= BUS_REQ;
                }
        }

        if (bus.change_state_delay != 0)
        {
                bus.change_state_delay--;
                if (bus.change_state_delay == 0)
                {
                        uint8_t val;

                        SET_BUS_STATE(bus, bus.new_state);

                        switch ((int)(bus.bus_out & (BUS_IO | BUS_CD | BUS_MSG)))
                        {
                        case BUS_IO:
                                bus.state = STATE_DATAIN;
                                // pcem bug, reproduced: PB-132 — lu dès l'entrée en DATA IN, même sans données :
                                //   read_complete ne devient plus vrai, la phase ne finit pas.
                                val = dev!.read(dev_data!);
                                bus.bus_out = (bus.bus_out & ~(uint32_t)BUS_DATAMASK) | (uint32_t)(BUS_SETDATA(val) | BUS_DBP);
                                break;

                        case 0:
                                if ((bus.new_state & BUS_IDLE) != 0)
                                {
                                        bus.state = STATE_IDLE;
                                        bus.bus_out &= ~(uint32_t)BUS_BSY;
                                }
                                else
                                        bus.state = STATE_DATAOUT;
                                break;

                        case (BUS_IO | BUS_CD):
                                bus.state = STATE_STATUS;
                                bus.bus_out = (bus.bus_out & ~(uint32_t)BUS_DATAMASK) | (uint32_t)(BUS_SETDATA(dev!.get_status(dev_data!)) | BUS_DBP);
                                break;

                        case (BUS_CD | BUS_IO | BUS_MSG):
                                bus.state = STATE_MESSAGEIN;
                                bus.bus_out = (bus.bus_out & ~(uint32_t)BUS_DATAMASK) | (uint32_t)(BUS_SETDATA(0) | BUS_DBP);
                                break;

                        default:
                                // pcem bug, not reproduced: PB-113 — fatal("change_state_delay bad state %x\n") (:264).
                                // DEVIATION: le bus au repos ; le pont voit BSY tomber et abandonne.
                                R9.Garde("scsi.c:264");
                                scsi_bus_reset(bus);
                                break;
                        }
                }
        }
        if (bus.new_req_delay != 0)
        {
                bus.new_req_delay--;
                if (bus.new_req_delay == 0)
                {
                        bus.bus_out |= BUS_REQ;
                }
        }

        return (int)bus.bus_out; // | bus->bus_in;
    }

    // pcem: scsi.c:280-285
    internal static int scsi_bus_match(scsi_bus_t bus, int bus_assert)
    {
        return (bus_assert & (BUS_CD | BUS_IO | BUS_MSG)) == (int)(bus.bus_out & (BUS_CD | BUS_IO | BUS_MSG)) ? 1 : 0;
    }

    // pcem: scsi.c:287-290
    internal static void scsi_bus_kick(scsi_bus_t bus)
    {
        scsi_bus_update(bus, 0);
    }

    // pcem: scsi.c:292-302
    internal static void scsi_bus_atapi_init(scsi_bus_t bus, scsi_device_t device, int id, atapi_device_t atapi_dev)
    {
        Array.Clear(bus.devices);
        Array.Clear(bus.device_data);

        bus.devices[0] = device;
        bus.device_data[0] = bus.devices[0]!.atapi_init!(bus, id, atapi_dev);
        if (bus.device_data[0] is null)
                bus.devices[0] = null;

        bus.is_atapi = 1;
    }

    // pcem: scsi.c:304-324 — G11.0 : le bus des cartes SCSI (scsi_aha1540.c:2149). dev_id et state ne sont pas
    //   posés : 0, comme le memset du struct de la carte (scsi_aha1540.c:2124).
    internal static void scsi_bus_init(scsi_bus_t bus)
    {
        int c;

        Array.Clear(bus.devices);
        Array.Clear(bus.device_data);

        for (c = 0; c < 7; c++)
        {
                if (ide.cdrom_channel == c)
                        bus.devices[c] = scsi_cd_c.scsi_cd;
                else if (ide.zip_channel == c)
                        bus.devices[c] = scsi_zip_c.scsi_zip;
                else
                        bus.devices[c] = scsi_hd_c.scsi_hd;

                bus.device_data[c] = bus.devices[c]!.init(bus, c);
                if (bus.device_data[c] is null)
                        bus.devices[c] = null;
        }

        bus.is_atapi = 0;
    }

    // pcem: scsi.c:326-336
    // pcem bug, reproduced: PB-121 — les tableaux sont effacés AVANT la boucle, qui ne trouve donc rien : aucun
    //   périphérique n'est fermé, et le fichier d'un disque reste ouvert avec son tampon (vidé par closepc,
    //   hdd_file.fflush_tous).
    internal static void scsi_bus_close(scsi_bus_t bus)
    {
        int c;

        Array.Clear(bus.devices);
        Array.Clear(bus.device_data);

        for (c = 0; c < 8; c++)
        {
                if (bus.device_data[c] is not null)
                        bus.devices[c]!.close(bus.device_data[c]!);
        }
    }

    // pcem: scsi.c:338-352
    internal static void scsi_bus_reset(scsi_bus_t bus)
    {
        int c;

        bus.state = STATE_IDLE;
        bus.clear_req = 0;
        bus.change_state_delay = 0;
        bus.new_req_delay = 0;
        bus.bus_in = bus.bus_out = 0;
        bus.command_pos = 0;

        for (c = 0; c < 8; c++)
        {
                if (bus.device_data[c] is not null && bus.devices[c]!.reset is not null)
                        bus.devices[c]!.reset!(bus.device_data[c]!);
        }
    }
}
