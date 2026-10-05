// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/ide/ide_atapi.c  (ide_atapi.h : includes/private/ide/ide_atapi.h)
// STATUS: deviated — G10.3 : le type ATAPI, la table du pilote de CD-ROM de l'hôte, le pointeur `atapi`.
//         G10.4 (PLAN-G10.md) : le pont lui-même, entier — atapi_device_t, les données, la sélection,
//         IIR et DRQ, la machine d'états du paquet, le reset.
//
// DEVIATIONS :
//   1. Les tampons `uint8_t *b` de la table ATAPI deviennent (tableau, décalage) — scsi_cd.c en passe à
//      l'intérieur de data_in (&data->data_in[c * 2352], scsi_cd.c:1108 ; &data->data_in[5], :1397).
//   2. R9, PB-113 : les dix-sept fatal() du pont. La sélection (:102, :106, :110), qu'un PACKET pendant
//      une phase en cours atteint, abandonne la transaction (scsi_bus_reset) et reprend, comme un vrai
//      lecteur ; les quatorze de la machine d'états, que l'invité n'atteint pas, finissent la commande
//      en erreur (atapi_abort). Survie : iXtal26.Diff r9-atapi, site par site.
//   3. R9, PB-114 : sans bus master, les états RETRY_*_DMA réarment leur chronomètre au lieu d'appeler
//      un pointeur nul (:473, :482).
//   4. `uint8_t *atastat`, `uint8_t *error`, `int *cylinder` : trois pointeurs sur les champs de l'IDE
//      propriétaire (resetide, ide.c:296-298), qui sont lus et écrits ici par `ide`.

using iXtal26.Diag;
using iXtal26.Scsi;
using static iXtal26.Ide.ide;
using static iXtal26.Scsi.scsi;
using static iXtal26.timer;

namespace iXtal26.Ide;

// pcem: ide_atapi.h:10-15
internal delegate int atapi_readtoc_fn(uint8_t[] b, int o, uint8_t starttrack, int msf, int maxlen, int single);
internal delegate int atapi_readtoc_session_fn(uint8_t[] b, int o, int msf, int maxlen);
internal delegate int atapi_readtoc_raw_fn(uint8_t[] b, int o, int maxlen);
internal delegate uint8_t atapi_getcurrentsubchannel_fn(uint8_t[] b, int o, int msf);
internal delegate int atapi_readsector_fn(uint8_t[] b, int o, int sector, int count);
internal delegate void atapi_readsector_raw_fn(uint8_t[] b, int o, int sector);

// pcem: ide_atapi.h:7-27
internal sealed class ATAPI
{
    internal readonly Func<int> ready;
    internal readonly Func<int> medium_changed;
    internal readonly atapi_readtoc_fn readtoc;
    internal readonly atapi_readtoc_session_fn readtoc_session;
    internal readonly atapi_readtoc_raw_fn readtoc_raw;
    internal readonly atapi_getcurrentsubchannel_fn getcurrentsubchannel;
    internal readonly atapi_readsector_fn readsector;
    internal readonly atapi_readsector_raw_fn readsector_raw;
    internal readonly Action<uint32_t, uint32_t, int> playaudio;
    internal readonly Action<uint32_t> seek;
    internal readonly Action load;
    internal readonly Action eject;
    internal readonly Action pause;
    internal readonly Action resume;
    internal readonly Func<uint32_t> size;
    internal readonly Func<int> status;
    internal readonly Func<uint32_t, int, int> is_track_audio;
    internal readonly Action stop;
    internal readonly Action exit;

    // Dans l'ordre des initialiseurs C (cdrom-image.cc:482-500, cdrom-null.c:61-79).
    internal ATAPI(Func<int> ready, Func<int> medium_changed, atapi_readtoc_fn readtoc,
                   atapi_readtoc_session_fn readtoc_session, atapi_readtoc_raw_fn readtoc_raw,
                   atapi_getcurrentsubchannel_fn getcurrentsubchannel, atapi_readsector_fn readsector,
                   atapi_readsector_raw_fn readsector_raw, Action<uint32_t, uint32_t, int> playaudio,
                   Action<uint32_t> seek, Action load, Action eject, Action pause, Action resume,
                   Func<uint32_t> size, Func<int> status, Func<uint32_t, int, int> is_track_audio, Action stop,
                   Action exit)
    {
        this.ready = ready;
        this.medium_changed = medium_changed;
        this.readtoc = readtoc;
        this.readtoc_session = readtoc_session;
        this.readtoc_raw = readtoc_raw;
        this.getcurrentsubchannel = getcurrentsubchannel;
        this.readsector = readsector;
        this.readsector_raw = readsector_raw;
        this.playaudio = playaudio;
        this.seek = seek;
        this.load = load;
        this.eject = eject;
        this.pause = pause;
        this.resume = resume;
        this.size = size;
        this.status = status;
        this.is_track_audio = is_track_audio;
        this.stop = stop;
        this.exit = exit;
    }
}

// pcem: ide_atapi.h:31-55 — classe : resetide (ide.c:294-298) et scsi_cd (data->atapi_dev) en gardent l'adresse.
internal sealed class atapi_device_t
{
    internal scsi_bus_t bus = new();

    internal uint8_t[] command = new uint8_t[12];
    internal int command_pos;

    internal int state;

    internal int max_transfer_len;

    internal uint8_t[] data = new uint8_t[65536];
    internal int data_read_pos, data_write_pos;

    internal int bus_state;

    internal IDE? ide;

    internal int board;
    // DEVIATION: atastat, error et cylinder (ide_atapi.h:49-51) — lus et écrits par `ide` (en-tête).

    internal int use_dma;
}

// iXtal26 (outillage) — la lecture du bus par le pont, remplacée par la porte r9-atapi seule.
internal delegate int bus_lu_fn(scsi_bus_t bus);

internal static class ide_atapi
{
    // pcem: ide_atapi.c:7 — propriété et non constante : TIMER_USEC reste nul jusqu'à setpitclock(). Cent
    //   microsecondes, là où ide.c en compte dix sous le même nom (ide.c:1).
    private static uint64_t IDE_TIME => 100 * TIMER_USEC;

    // pcem: ide_atapi.c:9-21 — internes : la porte r9-atapi forge des états du pont.
    internal const int ATAPI_STATE_IDLE = 0;
    internal const int ATAPI_STATE_COMMAND = 1;
    internal const int ATAPI_STATE_GOT_COMMAND = 2;
    internal const int ATAPI_STATE_NEXT_PHASE = 3;
    internal const int ATAPI_STATE_READ_STATUS = 4;
    internal const int ATAPI_STATE_READ_MESSAGE = 5;
    internal const int ATAPI_STATE_END_PHASE = 6;
    internal const int ATAPI_STATE_READ_DATA = 7;
    internal const int ATAPI_STATE_READ_DATA_WAIT = 8;
    internal const int ATAPI_STATE_WRITE_DATA = 9;
    internal const int ATAPI_STATE_WRITE_DATA_WAIT = 10;
    internal const int ATAPI_STATE_RETRY_READ_DMA = 11;
    internal const int ATAPI_STATE_RETRY_WRITE_DMA = 12;

    // pcem: ide_atapi.c:23-24
    private const int FEATURES_DMA = 0x01;
    private const int FEATURES_OVERLAP = 0x02;

    // pcem: ide_atapi.c:26 — NULL jusqu'à ce qu'un pilote soit posé : cdrom_null_open ou image_open, appelés
    //   par le bloc CD d'initpc et de resetpchard (pc.cs).
    internal static ATAPI? atapi;

    // iXtal26 (outillage) — r9-atapi, les états forgés : nul hors de la porte. Posé, il tient lieu de la
    //   lecture du bus, pour atteindre les sites de PB-113 que scsi.c ne produit pas de lui-même.
    internal static bus_lu_fn? bus_lu_force;

    private static int scsi_bus_read(scsi_bus_t bus) => bus_lu_force is null ? scsi.scsi_bus_read(bus) : bus_lu_force(bus);

    // pcem: ide_atapi.c:28-53
    internal static void atapi_data_write(atapi_device_t atapi_dev, uint16_t val)
    {
        IDE ide = atapi_dev.ide!;

        switch (atapi_dev.state)
        {
        case ATAPI_STATE_COMMAND:
                atapi_dev.command[atapi_dev.command_pos++] = (uint8_t)(val & 0xff);
                atapi_dev.command[atapi_dev.command_pos++] = (uint8_t)(val >> 8);
                if (atapi_dev.command_pos >= 12)
                {
                        atapi_dev.state = ATAPI_STATE_GOT_COMMAND;
                        timer_set_delay_u64(ide_timer[atapi_dev.board], 6 * IDE_TIME);
                        ide.atastat = (uint8_t)(BUSY_STAT | (ide.atastat & ERR_STAT));
                }
                break;

        case ATAPI_STATE_WRITE_DATA_WAIT:
                atapi_dev.data[atapi_dev.data_write_pos++] = (uint8_t)(val & 0xff);
                atapi_dev.data[atapi_dev.data_write_pos++] = (uint8_t)(val >> 8);
                if (atapi_dev.data_write_pos >= atapi_dev.data_read_pos)
                {
                        atapi_dev.bus_state = 0;
                        atapi_dev.state = ATAPI_STATE_WRITE_DATA;
                        timer_set_delay_u64(ide_timer[atapi_dev.board], 6 * IDE_TIME);
                        ide.atastat = (uint8_t)(BUSY_STAT | (ide.atastat & ERR_STAT));
                }
                break;
        }
    }

    // pcem: ide_atapi.c:55-78
    internal static uint16_t atapi_data_read(atapi_device_t atapi_dev)
    {
        uint16_t temp = 0xffff;

        switch (atapi_dev.state)
        {
        case ATAPI_STATE_READ_DATA_WAIT:
                if (atapi_dev.data_read_pos >= atapi_dev.data_write_pos)
                {
                        break;
                }

                temp = atapi_dev.data[atapi_dev.data_read_pos++];
                if (atapi_dev.data_read_pos < atapi_dev.data_write_pos)
                        temp |= (uint16_t)(atapi_dev.data[atapi_dev.data_read_pos++] << 8);
                if (atapi_dev.data_read_pos >= atapi_dev.data_write_pos)
                {
                        atapi_dev.state = ATAPI_STATE_NEXT_PHASE;
                        timer_set_delay_u64(ide_timer[atapi_dev.board], 6 * IDE_TIME);
                        atapi_dev.ide!.atastat = (uint8_t)(BUSY_STAT | (atapi_dev.ide.atastat & ERR_STAT));
                }
                break;
        }

        return temp;
    }

    // pcem: ide_atapi.c:80-93
    private static int wait_for_bus(scsi_bus_t bus, int state, int req_needed)
    {
        int c;

        for (c = 0; c < 20; c++)
        {
                int bus_state = scsi_bus_read(bus);

                if ((bus_state & (BUS_IO | BUS_CD | BUS_MSG)) == state && (bus_state & BUS_BSY) != 0)
                {
                        if (req_needed == 0 || (bus_state & BUS_REQ) != 0)
                                return 1;
                }
        }

        return 0;
    }

    // pcem: ide_atapi.c:95-114
    internal static void atapi_command_start(atapi_device_t atapi, uint8_t features)
    {
        scsi_bus_t bus = atapi.bus;

        atapi.use_dma = features & 1;

        // pcem bug, not reproduced: PB-113 — un PACKET pendant une phase en cours (après DEVICE RESET, qui ne
        //   remet pas le pont, ou un transfert abandonné) trouve le bus occupé : fatal() en :102, :106, :110.
        // DEVIATION: comme un vrai lecteur, la transaction en cours est abandonnée (scsi_bus_reset, qui remet
        //   aussi le périphérique) et la sélection reprise ; un second échec finit la commande en erreur.
        for (int essai = 0; ; essai++)
        {
                string? site = atapi_select(bus);

                if (site is null)
                        break;
                R9.Garde(site);
                if (essai == 1)
                {
                        atapi_abort(atapi, "ide_atapi.c:95");
                        return;
                }
                scsi_bus_reset(bus);
        }

        atapi.state = ATAPI_STATE_COMMAND;
        atapi.command_pos = 0;
    }

    // pcem: ide_atapi.c:100-110 — les trois étapes de la sélection ; null, ou le site où PCem s'arrêtait.
    private static string? atapi_select(scsi_bus_t bus)
    {
        scsi_bus_update(bus, BUS_SEL | BUS_SETDATA(1 << 0));
        if ((scsi_bus_read(bus) & BUS_BSY) == 0)
                return "ide_atapi.c:102"; // fatal("STATE_SCSI_SELECT failed to select target\n")

        scsi_bus_update(bus, 0);
        if ((scsi_bus_read(bus) & BUS_BSY) == 0)
                return "ide_atapi.c:106"; // fatal("STATE_SCSI_SELECT failed to select target 2\n")

        /*Device should now be selected*/
        if (wait_for_bus(bus, BUS_CD, 1) == 0)
                return "ide_atapi.c:110"; // fatal("Device failed to request command\n")

        return null;
    }

    // pcem: ide_atapi.c:116-128
    internal static uint8_t atapi_read_iir(atapi_device_t atapi_dev)
    {
        uint8_t val = 0;
        int bus_status = atapi_dev.bus_state;

        if ((bus_status & BUS_CD) != 0)
                val |= 1;
        if ((bus_status & BUS_IO) != 0)
                val |= 2;

        return val;
    }

    // pcem: ide_atapi.c:130
    internal static uint8_t atapi_read_drq(atapi_device_t atapi_dev) => (uint8_t)(atapi_dev.bus_state & BUS_REQ);

    // pcem: ide_atapi.c:132-135
    internal static void atapi_set_transfer_granularity(atapi_device_t atapi_dev, int size)
    {
        if (atapi_dev.max_transfer_len > size)
                atapi_dev.max_transfer_len -= (atapi_dev.max_transfer_len % size);
    }

    // pcem: ide_atapi.c:137-491
    internal static void atapi_process_packet(atapi_device_t atapi_dev)
    {
        IDE ide = atapi_dev.ide!;

        switch (atapi_dev.state)
        {
        case ATAPI_STATE_COMMAND:
                ide.atastat = (uint8_t)(READY_STAT | (ide.atastat & ERR_STAT));
                atapi_dev.max_transfer_len = ide.cylinder & ~1;
                if (atapi_dev.max_transfer_len == 0)
                        atapi_dev.max_transfer_len = 0xfffe;
                atapi_dev.bus_state = BUS_CD | BUS_REQ;
                break;

        case ATAPI_STATE_GOT_COMMAND:
        {
                int pos = 0;
                int bus_state;
                int c;

                for (c = 0; c < 20; c++)
                {
                        bus_state = scsi_bus_read(atapi_dev.bus);

                        // pcem bug, not reproduced: PB-113 — les quatorze fatal() de la machine d'états, ici et
                        //   plus bas, vérifient l'accord du pont et de scsi_cd ; l'invité ne les atteint pas.
                        // DEVIATION: la commande finit en erreur (atapi_abort).
                        if ((bus_state & BUS_BSY) == 0)
                        {
                                atapi_abort(atapi_dev, "ide_atapi.c:157"); // fatal("SEND_COMMAND - dropped BSY\n")
                                return;
                        }
                        if ((bus_state & (BUS_IO | BUS_CD | BUS_MSG)) != BUS_CD)
                        {
                                atapi_abort(atapi_dev, "ide_atapi.c:159"); // fatal("SEND_COMMAND - bus phase incorrect\n")
                                return;
                        }
                        if ((bus_state & BUS_REQ) != 0)
                                break;
                }
                if (c == 20)
                {
                        atapi_abort(atapi_dev, "ide_atapi.c:164"); // fatal("SEND_COMMAND timed out\n")
                        return;
                }

                while (true)
                {
                        int bus_out;

                        for (c = 0; c < 20; c++)
                        {
                                // CS0136 : le C redéclare bus_state dans la boucle ; elle est relue avant usage (:187).
                                bus_state = scsi_bus_read(atapi_dev.bus);

                                if ((bus_state & BUS_BSY) == 0)
                                {
                                        atapi_abort(atapi_dev, "ide_atapi.c:173"); // fatal("SEND_COMMAND - dropped BSY\n")
                                        return;
                                }
                                if ((bus_state & (BUS_IO | BUS_CD | BUS_MSG)) != BUS_CD)
                                        break;
                                if ((bus_state & BUS_REQ) != 0)
                                        break;
                        }
                        if (c == 20)
                        {
                                atapi_dev.state = ATAPI_STATE_NEXT_PHASE;
                                timer_set_delay_u64(ide_timer[atapi_dev.board], 6 * IDE_TIME);
                                break;
                        }

                        bus_state = scsi_bus_read(atapi_dev.bus);

                        if ((bus_state & (BUS_IO | BUS_CD | BUS_MSG | BUS_REQ)) != (BUS_CD | BUS_REQ))
                        {
                                atapi_dev.state = ATAPI_STATE_NEXT_PHASE;
                                timer_set_delay_u64(ide_timer[atapi_dev.board], 6 * IDE_TIME);
                                break;
                        }

                        if (pos >= atapi_dev.command_pos)
                                bus_out = BUS_SETDATA(0); /*Pad out with zeroes*/
                        else
                                bus_out = BUS_SETDATA(atapi_dev.command[pos++]);
                        scsi_bus_update(atapi_dev.bus, bus_out | BUS_ACK);
                        scsi_bus_update(atapi_dev.bus, bus_out & ~BUS_ACK);

                        if ((bus_state & BUS_BSY) == 0)
                        {
                                atapi_abort(atapi_dev, "ide_atapi.c:205"); // fatal("SEND_COMMAND - dropped BSY\n")
                                return;
                        }
                }
        }
                break;

        case ATAPI_STATE_NEXT_PHASE:
        {
                int c;

                for (c = 0; c < 20; c++)
                {
                        int bus_state = scsi_bus_read(atapi_dev.bus);

                        if ((bus_state & BUS_BSY) == 0)
                        {
                                atapi_abort(atapi_dev, "ide_atapi.c:221"); // fatal("NEXT_PHASE - dropped BSY waiting\n")
                                return;
                        }

                        if ((bus_state & BUS_REQ) != 0)
                        {
                                scsi_device_t scsi_dev = atapi_dev.bus.devices[0]!;
                                object scsi_data = atapi_dev.bus.device_data[0]!;

                                switch (bus_state & (BUS_IO | BUS_CD | BUS_MSG))
                                {
                                case 0:
                                        atapi_dev.data_read_pos = scsi_dev.get_bytes_required(scsi_data);

                                        atapi_dev.data_write_pos = 0;

                                        if (atapi_dev.use_dma != 0)
                                        {
                                                if (ide_bus_master_write_data != null)
                                                {
                                                        if (ide_bus_master_write_data(atapi_dev.board, atapi_dev.data,
                                                                                      atapi_dev.data_read_pos,
                                                                                      ide_bus_master_p) != 0)
                                                        {
                                                                atapi_dev.state = ATAPI_STATE_RETRY_WRITE_DMA;
                                                                timer_set_delay_u64(ide_timer[atapi_dev.board], 1 * IDE_TIME);
                                                        }
                                                        else
                                                        {
                                                                atapi_dev.data_write_pos = atapi_dev.data_read_pos;
                                                                atapi_dev.bus_state = 0;
                                                                atapi_dev.state = ATAPI_STATE_WRITE_DATA;
                                                                timer_set_delay_u64(ide_timer[atapi_dev.board], 6 * IDE_TIME);
                                                        }
                                                }
                                                else
                                                {
                                                        atapi_dev.state = ATAPI_STATE_RETRY_WRITE_DMA;
                                                        timer_set_delay_u64(ide_timer[atapi_dev.board], 1 * IDE_TIME);
                                                }
                                        }
                                        else
                                        {
                                                if (atapi_dev.data_read_pos > atapi_dev.max_transfer_len)
                                                        atapi_dev.data_read_pos = atapi_dev.max_transfer_len;

                                                atapi_dev.state = ATAPI_STATE_WRITE_DATA_WAIT;

                                                ide.cylinder = atapi_dev.data_read_pos;

                                                ide.atastat = (uint8_t)(READY_STAT | DRQ_STAT | (ide.atastat & ERR_STAT));
                                                atapi_dev.bus_state = BUS_REQ;
                                                ide_irq_raise(ide);
                                        }
                                        break;

                                case BUS_IO:
                                        atapi_dev.state = ATAPI_STATE_READ_DATA;
                                        atapi_dev.data_read_pos = atapi_dev.data_write_pos = 0;
                                        break;

                                case (BUS_IO | BUS_CD):
                                        atapi_dev.state = ATAPI_STATE_READ_STATUS;
                                        break;

                                case (BUS_CD | BUS_IO | BUS_MSG):
                                        atapi_dev.state = ATAPI_STATE_READ_MESSAGE;
                                        break;

                                default:
                                        atapi_abort(atapi_dev, "ide_atapi.c:281"); // fatal("NEXT_PHASE - bus state changed %x\n")
                                        return;
                                }
                                break;
                        }
                }
                timer_set_delay_u64(ide_timer[atapi_dev.board], 6 * IDE_TIME);
        }
                break;

        case ATAPI_STATE_READ_STATUS:
        {
                int c;

                for (c = 0; c < 20; c++)
                {
                        int bus_state = scsi_bus_read(atapi_dev.bus);

                        if ((bus_state & BUS_BSY) == 0)
                        {
                                atapi_abort(atapi_dev, "ide_atapi.c:296"); // fatal("READ_STATUS - dropped BSY waiting\n")
                                return;
                        }

                        if ((bus_state & (BUS_IO | BUS_CD | BUS_MSG)) != (BUS_CD | BUS_IO))
                        {
                                atapi_abort(atapi_dev, "ide_atapi.c:299"); // fatal("READ_STATUS - changed phase\n")
                                return;
                        }

                        if ((bus_state & BUS_REQ) != 0)
                        {
                                int bus_out = 0;

                                scsi_bus_update(atapi_dev.bus, bus_out | BUS_ACK);
                                scsi_bus_update(atapi_dev.bus, bus_out & ~BUS_ACK);

                                atapi_dev.state = ATAPI_STATE_NEXT_PHASE;
                                break;
                        }
                }
                timer_set_delay_u64(ide_timer[atapi_dev.board], 6 * IDE_TIME);
        }
                break;

        case ATAPI_STATE_READ_MESSAGE:
        {
                int c;

                for (c = 0; c < 20; c++)
                {
                        int bus_state = scsi_bus_read(atapi_dev.bus);

                        if ((bus_state & BUS_BSY) == 0)
                        {
                                atapi_abort(atapi_dev, "ide_atapi.c:324"); // fatal("READ_MESSAGE - dropped BSY waiting\n")
                                return;
                        }

                        if ((bus_state & (BUS_IO | BUS_CD | BUS_MSG)) != (BUS_CD | BUS_IO | BUS_MSG))
                        {
                                atapi_dev.state = ATAPI_STATE_NEXT_PHASE;
                                break;
                        }

                        if ((bus_state & BUS_REQ) != 0)
                        {
                                uint8_t msg = (uint8_t)BUS_GETDATA(bus_state);
                                int bus_out = 0;

                                scsi_bus_update(atapi_dev.bus, bus_out | BUS_ACK);
                                scsi_bus_update(atapi_dev.bus, bus_out & ~BUS_ACK);

                                switch (msg)
                                {
                                case MSG_COMMAND_COMPLETE:
                                        atapi_dev.state = ATAPI_STATE_END_PHASE;
                                        break;

                                default:
                                        atapi_abort(atapi_dev, "ide_atapi.c:346"); // fatal("READ_MESSAGE - unknown message %02x\n")
                                        return;
                                }
                                break;
                        }
                }
                timer_set_delay_u64(ide_timer[atapi_dev.board], 6 * IDE_TIME);
        }
                break;

        case ATAPI_STATE_READ_DATA:
        {
                while (atapi_dev.data_write_pos < atapi_dev.max_transfer_len)
                {
                        int c;

                        for (c = 0; c < 20; c++)
                        {
                                int bus_state = scsi_bus_read(atapi_dev.bus);

                                if ((bus_state & BUS_BSY) == 0)
                                {
                                        atapi_abort(atapi_dev, "ide_atapi.c:362"); // fatal("READ_DATA - dropped BSY waiting\n")
                                        return;
                                }

                                if ((bus_state & (BUS_IO | BUS_CD | BUS_MSG)) != BUS_IO)
                                {
                                        break;
                                }

                                if ((bus_state & BUS_REQ) != 0)
                                {
                                        uint8_t data = (uint8_t)BUS_GETDATA(bus_state);
                                        int bus_out = 0;

                                        atapi_dev.data[atapi_dev.data_write_pos++] = data;

                                        scsi_bus_update(atapi_dev.bus, bus_out | BUS_ACK);
                                        scsi_bus_update(atapi_dev.bus, bus_out & ~BUS_ACK);
                                        break;
                                }
                        }
                        if ((scsi_bus_read(atapi_dev.bus) & (BUS_IO | BUS_CD | BUS_MSG)) != BUS_IO)
                                break;
                }
                if (atapi_dev.use_dma != 0)
                {
                        if (ide_bus_master_read_data != null)
                        {
                                if (ide_bus_master_read_data(atapi_dev.board, atapi_dev.data, atapi_dev.data_write_pos,
                                                             ide_bus_master_p) != 0)
                                {
                                        atapi_dev.state = ATAPI_STATE_RETRY_READ_DMA;
                                        timer_set_delay_u64(ide_timer[atapi_dev.board], 1 * IDE_TIME);
                                }
                                else
                                {
                                        atapi_dev.state = ATAPI_STATE_NEXT_PHASE;
                                        timer_set_delay_u64(ide_timer[atapi_dev.board], 1 * IDE_TIME);
                                }
                        }
                        else
                        {
                                atapi_dev.state = ATAPI_STATE_RETRY_READ_DMA;
                                timer_set_delay_u64(ide_timer[atapi_dev.board], 1 * IDE_TIME);
                        }
                }
                else
                {
                        atapi_dev.state = ATAPI_STATE_READ_DATA_WAIT;
                        ide.atastat = (uint8_t)(READY_STAT | DRQ_STAT | (ide.atastat & ERR_STAT));
                        ide.cylinder = atapi_dev.data_write_pos;
                        atapi_dev.bus_state = BUS_IO | BUS_REQ;
                        ide_irq_raise(ide);
                }
        }
                break;

        case ATAPI_STATE_WRITE_DATA:
        {
                atapi_dev.data_read_pos = 0;
                while (atapi_dev.data_read_pos < atapi_dev.data_write_pos)
                {
                        int bus_state = scsi_bus_read(atapi_dev.bus);

                        if ((bus_state & BUS_BSY) == 0)
                        {
                                atapi_abort(atapi_dev, "ide_atapi.c:416"); // fatal("WRITE_DATA - dropped BSY waiting\n")
                                return;
                        }

                        if ((bus_state & (BUS_IO | BUS_CD | BUS_MSG)) != 0)
                        {
                                atapi_dev.state = ATAPI_STATE_NEXT_PHASE;
                                break;
                        }

                        if ((bus_state & BUS_REQ) != 0)
                        {
                                uint8_t data = atapi_dev.data[atapi_dev.data_read_pos++];
                                int bus_out;

                                bus_out = BUS_SETDATA(data);
                                scsi_bus_update(atapi_dev.bus, bus_out | BUS_ACK);
                                scsi_bus_update(atapi_dev.bus, bus_out & ~BUS_ACK);
                        }
                }

                atapi_dev.state = ATAPI_STATE_NEXT_PHASE;
                timer_set_delay_u64(ide_timer[atapi_dev.board], 1 * IDE_TIME);
        }
                break;

        case ATAPI_STATE_END_PHASE:
        {
                int c;
                /*Wait for SCSI command to move to next phase*/
                for (c = 0; c < 20; c++)
                {
                        int bus_state = scsi_bus_read(atapi_dev.bus);

                        if ((bus_state & BUS_BSY) == 0)
                        {
                                atapi_dev.state = ATAPI_STATE_IDLE;
                                break;
                        }

                        if ((bus_state & BUS_REQ) != 0)
                        {
                                atapi_abort(atapi_dev, "ide_atapi.c:453"); // fatal("END_PHASE - unexpected REQ\n")
                                return;
                        }
                }
                if (atapi_dev.state == ATAPI_STATE_IDLE)
                {
                        scsi_device_t scsi_dev = atapi_dev.bus.devices[0]!;
                        object scsi_data = atapi_dev.bus.device_data[0]!;

                        ide.atastat = READY_STAT;
                        atapi_dev.bus_state = BUS_IO | BUS_CD;
                        if (scsi_dev.get_status(scsi_data) != STATUS_GOOD)
                        {
                                ide.error = (uint8_t)((scsi_dev.get_sense_key(scsi_data) << 4) | ABRT_ERR);
                                if (scsi_dev.get_sense_key(scsi_data) == KEY_UNIT_ATTENTION)
                                        ide.error |= MCR_ERR;
                                ide.atastat |= ERR_STAT;
                        }
                        ide_irq_raise(ide);
                }
                else
                        timer_set_delay_u64(ide_timer[atapi_dev.board], 6 * IDE_TIME);
        }
                break;

        case ATAPI_STATE_RETRY_READ_DMA:
        {
                // pcem bug, not reproduced: PB-114 — sans bus master (toutes les machines du dépôt), PCem appelle ici
                //   un pointeur nul (:473).
                // DEVIATION: le DMA n'a pas lieu, on réessaie plus tard : l'invité attend, l'émulateur vit — BSY
                //   indéfini, comme WIN_READ_DMA côté disque (ide.c:884).
                if (ide_bus_master_read_data is null)
                {
                        R9.Garde("ide_atapi.c:473");
                        timer_set_delay_u64(ide_timer[atapi_dev.board], 1 * IDE_TIME);
                }
                else if (ide_bus_master_read_data(atapi_dev.board, atapi_dev.data, atapi_dev.data_write_pos, ide_bus_master_p) != 0)
                {
                        timer_set_delay_u64(ide_timer[atapi_dev.board], 1 * IDE_TIME);
                }
                else
                {
                        atapi_dev.state = ATAPI_STATE_NEXT_PHASE;
                        timer_set_delay_u64(ide_timer[atapi_dev.board], 6 * IDE_TIME);
                }
        }
                break;

        case ATAPI_STATE_RETRY_WRITE_DMA:
        {
                // pcem bug, not reproduced: PB-114 — le même pointeur nul, en écriture (:482).
                // DEVIATION: l'invité attend, l'émulateur vit.
                if (ide_bus_master_write_data is null)
                {
                        R9.Garde("ide_atapi.c:482");
                        timer_set_delay_u64(ide_timer[atapi_dev.board], 1 * IDE_TIME);
                }
                else if (ide_bus_master_write_data(atapi_dev.board, atapi_dev.data, atapi_dev.data_read_pos, ide_bus_master_p) != 0)
                {
                        timer_set_delay_u64(ide_timer[atapi_dev.board], 1 * IDE_TIME);
                }
                else
                {
                        atapi_dev.bus_state = 0;
                        atapi_dev.state = ATAPI_STATE_WRITE_DATA;
                        timer_set_delay_u64(ide_timer[atapi_dev.board], 6 * IDE_TIME);
                }
        }
                break;
        }
    }

    // pcem: ide_atapi.c:493-500
    internal static void atapi_reset(atapi_device_t atapi_dev)
    {
        // omitted: pclog("atapi_reset\n") — sortie pure.
        atapi_dev.state = ATAPI_STATE_IDLE;
        atapi_dev.command_pos = 0;
        atapi_dev.data_read_pos = 0;
        atapi_dev.data_write_pos = 0;
        scsi_bus_reset(atapi_dev.bus);
    }

    // DEVIATION: PB-113 — la sortie sûre des sites où PCem s'arrête : le pont et le bus au repos, la commande
    //   finie en erreur, comme abort_cmd d'ide.c (ERR et ABRT), en phase d'état (IIR = 3), avec l'IRQ.
    private static void atapi_abort(atapi_device_t atapi_dev, string site)
    {
        R9.Garde(site);
        atapi_reset(atapi_dev);
        atapi_dev.ide!.atastat = READY_STAT | ERR_STAT;
        atapi_dev.ide.error = ABRT_ERR;
        atapi_dev.bus_state = BUS_IO | BUS_CD;
        ide_irq_raise(atapi_dev.ide);
    }

    // iXtal26 (outillage) — ORACLE PARITY, pendant de h_atapi_reset (harness_ide.c) : le pont des quatre
    //   unités remis à neuf à chaque amorçage ; chez PCem, il traverse resetide et resetpchard.
    internal static void atapi_clear_state_for_oracle_parity()
    {
        for (int d = 0; d < 4; d++)
                ide_drives[d].atapi = new atapi_device_t();
    }
}
