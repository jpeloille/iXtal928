// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/scsi/scsi_aha1540.c  (scsi_aha1540.h : includes/private/scsi/scsi_aha1540.h)
// STATUS: deviated — G11.0 (PLAN-G11.md) : l'Adaptec AHA-1542C, ses trois machines d'états (commandes, CCB,
//         bus), la fenêtre de sa ROM (deux banques de 16 Ko, les interrupteurs, la RAM d'ombre) et son EEPROM.
//
// omitted: la BusLogic BT-545S (scsi_bt545s_init, :2163-2191 ; :2203 ; bt545s_config, :2229-2280 ; :2291-2299),
//   `type` et toutes ses branches SCSI_BT545S, les commandes BUSLOGIC traitées (:1255-1334), `primed` (:22), et
//   MB_FORMAT_8 entier (`mb_format`, ses branches et les trois fatal de :1507, :1575, :1663) : seule la commande
//   81h pose MB_FORMAT_8 (:1255-1266), et elle est refusée sur la 1542C (:650-655) — décision n° 1 de PLAN-G11.md.
// omitted: les pclog (sortie pure) ; G11.0 compte à leur place les commandes reçues (commandes_vues).
//
// DEVIATIONS (R9, PB-135) : :530, :679, :799, :841, :883, :980, :1743 — la commande finit en erreur (décision
//   n° 5). Reproduits : PB-136 à PB-144 (PCEM_BUGS.md), dont la lecture de 22h au-delà de params[64] (PB-136,
//   param_lu) ; les fatal inatteignables de PB-134.

// CS8981 : `scsi_aha1540` n'a que des minuscules ASCII — le nom de l'unité C, comme scsi.cs.
#pragma warning disable CS8981

using iXtal26.Diag;
using iXtal26.Flash;
using iXtal26.Memory;
using iXtal26.PluginApi;
using static iXtal26.io;
using static iXtal26.Memory.mem;
using static iXtal26.Models.pic;
using static iXtal26.PluginApi.device;
using static iXtal26.Scsi.scsi;
using static iXtal26.timer;

namespace iXtal26.Scsi;

// pcem: scsi_aha1540.c:133-159
internal sealed class aha154x_ccb_t
{
    internal uint32_t addr;

    internal uint8_t opcode;
    internal int target_id, transfer_dir, lun;
    internal int scsi_cmd_len;
    internal int req_sense_len;
    internal uint32_t data_len;
    internal uint32_t data_pointer;
    internal uint32_t link_pointer;
    internal uint8_t link_id;

    internal uint8_t[] cdb = new uint8_t[256];

    internal uint8_t status;

    internal int from_mailbox;

    internal uint32_t data_seg_list_pointer;
    internal uint32_t data_seg_list_len;
    internal uint32_t data_seg_list_idx;

    internal int residual;

    internal int bytes_transferred;
    internal uint32_t total_data_len;
}

// pcem: scsi_aha1540.c:161-176
internal sealed class aha154x_cdb_t
{
    internal int idx, len;
    internal uint8_t[] data = new uint8_t[256];

    internal int data_idx, data_len;
    internal uint32_t data_pointer;

    internal uint8_t last_status;

    internal uint32_t data_seg_list_pointer;
    internal uint32_t data_seg_list_len;
    internal uint32_t data_seg_list_idx;

    internal int scatter_gather;
    internal int bytes_transferred;
}

// pcem: scsi_aha1540.c:61-179 — classe : la fenêtre de ROM, les ports et le chronomètre en gardent l'adresse.
internal sealed class aha154x_t
{
    internal rom_t bios_rom = new();
    internal mem_mapping_t mapping = new();
    internal uint32_t bios_addr;

    internal uint8_t[] shadow_ram = new uint8_t[0x4000];
    internal uint8_t shadow;

    internal uint32_t bios_bank;

    internal uint8_t[] eeprom = new uint8_t[256];
    internal string fn = "";

    internal scsi_bus_t bus = new();

    internal uint8_t isr;
    internal uint8_t isr_pending;
    internal uint8_t status;

    internal int mbo_irq_enable;

    internal uint8_t cmd_data;
    internal uint8_t data_in;

    internal uint8_t command;
    internal int command_len;
    internal int cur_param;
    // params[64], result_pos, result_len, result[256] : contigus dans le struct C, sans remplissage — 22h lit
    //   au-delà de params (PB-136), voir param_lu.
    internal uint8_t[] @params = new uint8_t[64];

    internal int result_pos, result_len;
    internal uint8_t[] result = new uint8_t[256];

    internal uint8_t[] dma_buffer = new uint8_t[64];

    internal uint8_t bon, boff;
    internal uint8_t atbs;

    internal int mbc;
    internal uint32_t mba;
    internal uint32_t mba_i;
    internal int mbo_req;

    internal int bios_mbc;
    internal uint32_t bios_mba;
    internal int bios_mbo_req;
    internal int bios_mbo_inited;

    internal int cmd_state;
    internal int ccb_state;
    internal int scsi_state;

    internal int bios_cmd_state;

    internal pc_timer_t timer = new();

    internal int current_mbo;
    internal int current_mbo_is_bios;
    internal int current_mbi;

    internal uint8_t mblt, mbu;

    internal uint8_t[] int_buffer = new uint8_t[512];

    internal uint8_t e_d;
    internal uint16_t to;

    internal uint8_t dipsw;
    internal int irq, dma, host_id;

    internal aha154x_ccb_t ccb = new();

    internal aha154x_cdb_t cdb = new();

    internal int reg3_idx;
}

internal static class scsi_aha1540
{
    // pcem: scsi_aha1540.c:30-57
    internal const int CMD_STATE_RESET = 0, CMD_STATE_IDLE = 1, CMD_STATE_GET_PARAMS = 2, CMD_STATE_CMD_IN_PROGRESS = 3,
                       CMD_STATE_SEND_RESULT = 4;

    internal const int CCB_STATE_IDLE = 0, CCB_STATE_SEND_COMMAND = 1, CCB_STATE_WAIT_COMMAND = 2,
                       CCB_STATE_SEND_REQUEST_SENSE = 3, CCB_STATE_WAIT_REQUEST_SENSE = 4;

    internal const int SCSI_STATE_IDLE = 0, SCSI_STATE_SELECT = 1, SCSI_STATE_SELECT_FAILED = 2,
                       SCSI_STATE_SEND_COMMAND = 3, SCSI_STATE_NEXT_PHASE = 4, SCSI_STATE_END_PHASE = 5,
                       SCSI_STATE_READ_DATA = 6, SCSI_STATE_WRITE_DATA = 7, SCSI_STATE_READ_STATUS = 8,
                       SCSI_STATE_READ_MESSAGE = 9;

    // pcem: scsi_aha1540.c:181-255
    private const uint8_t STATUS_INVDCMD = 0x01;
    private const uint8_t STATUS_DF = 0x04;
    private const uint8_t STATUS_CDF = 0x08;
    private const uint8_t STATUS_IDLE = 0x10;
    private const uint8_t STATUS_INIT = 0x20;
    private const uint8_t STATUS_STST = 0x80;

    private const uint8_t CTRL_BRST = 0x10;
    private const uint8_t CTRL_IRST = 0x20;
    private const uint8_t CTRL_SRST = 0x40;
    private const uint8_t CTRL_RESET = 0x80;

    private const uint8_t ISR_MBIF = 0x01;
    private const uint8_t ISR_MBOA = 0x02;
    private const uint8_t ISR_HACC = 0x04;
    private const uint8_t ISR_ANYINTR = 0x80;

    private const uint8_t COMMAND_NOP = 0x00;
    private const uint8_t COMMAND_MAILBOX_INITIALIZATION = 0x01;
    private const uint8_t COMMAND_START_SCSI_COMMAND = 0x02;
    private const uint8_t COMMAND_START_BIOS_COMMAND = 0x03;
    private const uint8_t COMMAND_ADAPTER_INQUIRY = 0x04;
    private const uint8_t COMMAND_MAILBOX_OUT_IRQ_ENA = 0x05;
    private const uint8_t COMMAND_SET_SELECTION_TIMEOUT = 0x06;
    private const uint8_t COMMAND_SET_BUS_ON_TIME = 0x07;
    private const uint8_t COMMAND_SET_BUS_OFF_TIME = 0x08;
    private const uint8_t COMMAND_SET_AT_BUS_SPEED = 0x09;
    private const uint8_t COMMAND_INQUIRE_INSTALLED_DEVICES = 0x0a;
    private const uint8_t COMMAND_RETURN_CONFIG_DATA = 0x0b;
    private const uint8_t COMMAND_RETURN_SETUP_DATA = 0x0d;
    private const uint8_t COMMAND_WRITE_CHANNEL_2_BUF = 0x1a;
    private const uint8_t COMMAND_READ_CHANNEL_2_BUF = 0x1b;
    private const uint8_t COMMAND_ECHO = 0x1f;

    private const uint8_t COMMAND_ADAPTEC_RETURN_EEPROM_DATA = 0x23;
    private const uint8_t COMMAND_ADAPTEC_SET_SHADOW_PARAMS = 0x24;
    private const uint8_t COMMAND_ADAPTEC_BIOS_MAILBOX_INIT = 0x25;
    private const uint8_t COMMAND_ADAPTEC_SET_BIOS_BANK_1 = 0x26;
    private const uint8_t COMMAND_ADAPTEC_SET_BIOS_BANK_2 = 0x27;
    private const uint8_t COMMAND_ADAPTEC_GET_EXT_BIOS_INFO = 0x28;
    private const uint8_t COMMAND_ENABLE_MAILBOX_INTERFACE = 0x29;
    private const uint8_t COMMAND_ADAPTEC_START_BIOS_SCSI_CMD = 0x82;

    private const uint8_t COMMAND_BUSLOGIC_81 = 0x81;
    private const uint8_t COMMAND_BUSLOGIC_84 = 0x84;
    private const uint8_t COMMAND_BUSLOGIC_85 = 0x85;
    private const uint8_t COMMAND_BUSLOGIC_ADAPTER_MODEL_NR = 0x8b;
    private const uint8_t COMMAND_BUSLOGIC_INQUIRY_SYNC_PERIOD = 0x8c;
    private const uint8_t COMMAND_BUSLOGIC_EXTENDED_SETUP_INFO = 0x8d;
    private const uint8_t COMMAND_BUSLOGIC_STRICT_RR = 0x8f;
    private const uint8_t COMMAND_BUSLOGIC_SET_CCB_FORMAT = 0x96;

    private const uint8_t COMMAND_FREE_CCB = 0;
    private const uint8_t COMMAND_START_CCB = 1;
    private const uint8_t COMMAND_ABORT_CCB = 2;

    private const uint8_t CCB_INITIATOR = 0;
    private const uint8_t CCB_INITIATOR_SCATTER_GATHER = 2;
    private const uint8_t CCB_INITIATOR_RESIDUAL = 3;
    private const uint8_t CCB_INITIATOR_SCATTER_GATHER_RESIDUAL = 4;

    private const int CCB_SENSE_14 = 0x00;
    private const int CCB_SENSE_NONE = 0x01;

    private const uint8_t MBI_CCB_COMPLETE = 0x01;
    private const uint8_t MBI_CCB_ABORTED = 0x02;
    private const uint8_t MBI_CCB_COMPLETE_WITH_ERROR = 0x04;

    private const uint8_t HOST_STATUS_COMMAND_COMPLETE = 0x00;
    private const uint8_t HOST_STATUS_SELECTION_TIME_OUT = 0x11;

    private const int POLL_TIME_US = 10;
    private const int MAX_BYTES_TRANSFERRED_PER_POLL = 50;
    /*10us poll period with 50 bytes transferred per poll = 5MB/sec*/

    // Diagnostic : les commandes reçues par code (PLAN-G11.md, mesures M1 à M8) ; remis à zéro par l'init.
    internal static readonly uint64_t[] commandes_vues = new uint64_t[256];

    // pcem: scsi_aha1540.c:90-93 — PB-136 : l'octet `i` à partir de params, dans le struct C : params[0..63], puis
    //   result_pos et result_len (int, petit-boutien), puis result[]. 22h lit jusqu'à l'indice 257 (result[185]).
    private static uint8_t param_lu(aha154x_t scsi, int i)
    {
        if (i < 64)
                return scsi.@params[i];
        if (i < 68)
                return (uint8_t)(scsi.result_pos >> (8 * (i - 64)));
        if (i < 72)
                return (uint8_t)(scsi.result_len >> (8 * (i - 68)));
        return scsi.result[i - 72];
    }

    // pcem: scsi_aha1540.c:261-264 — picint prend un uint16_t : l'IRQ 16 (EEPROM à 7) donne 0 (PB-144).
    // pcem bug, reproduced: PB-144 — un code d'IRQ 7 donne l'IRQ 16 : picint((uint16_t)(1 << 16)) ne lève rien.
    private static void set_irq(aha154x_t scsi, uint8_t val)
    {
        picint((uint16_t)(1 << scsi.irq));
        scsi.isr |= (uint8_t)(val | ISR_ANYINTR);
    }

    // pcem: scsi_aha1540.c:266
    private static void clear_irq(aha154x_t scsi) { picintc((uint16_t)(1 << scsi.irq)); }

    // pcem: scsi_aha1540.c:268-333
    private static void aha154x_out(uint16_t port, uint8_t val, object? p)
    {
        aha154x_t scsi = (aha154x_t)p!;

        switch (port & 3)
        {
        case 0: /*Control register*/
                // pcem bug, reproduced: PB-132 — aucun des trois resets ne remet le bus SCSI au repos.
                if ((val & CTRL_RESET) != 0)
                {
                        scsi.isr = 0;
                        clear_irq(scsi);
                        scsi.status = STATUS_STST;
                        scsi.cmd_state = CMD_STATE_RESET;
                        scsi.ccb_state = CCB_STATE_IDLE;
                        scsi.scsi_state = SCSI_STATE_IDLE;
                        scsi.current_mbi = 0;
                        scsi.mbo_irq_enable = 0;
                }
                if ((val & CTRL_SRST) != 0)
                {
                        scsi.isr = 0;
                        clear_irq(scsi);
                        scsi.status = STATUS_INIT;
                        scsi.cmd_state = CMD_STATE_IDLE;
                        scsi.ccb_state = CCB_STATE_IDLE;
                        scsi.scsi_state = SCSI_STATE_IDLE;
                        scsi.current_mbi = 0;
                        scsi.mbo_irq_enable = 0;
                }
                if ((val & CTRL_IRST) != 0)
                {
                        scsi.isr = 0;
                        clear_irq(scsi);
                        scsi.status &= unchecked((uint8_t)~STATUS_INVDCMD);
                }
                if ((val & CTRL_BRST) != 0)
                {
                        scsi.ccb_state = CCB_STATE_IDLE;
                        scsi.scsi_state = SCSI_STATE_IDLE;
                }
                break;

        case 1: /*Command/data register*/
                if ((scsi.status & STATUS_CDF) != 0)
                        break;
                scsi.status |= STATUS_CDF;
                scsi.cmd_data = val;
                // pcem bug, reproduced: PB-142 — réentrant dans tous les états (RESET, CMD_IN_PROGRESS, SEND_RESULT).
                process_cmd(scsi);
                break;
        }
    }

    // pcem: scsi_aha1540.c:335-390
    private static uint8_t aha154x_in(uint16_t port, object? p)
    {
        aha154x_t scsi = (aha154x_t)p!;
        uint8_t temp = 0xff;

        switch (port & 3)
        {
        case 0: /*Status register*/
                temp = scsi.status;
                if (scsi.cmd_state == CMD_STATE_IDLE && scsi.ccb_state == CCB_STATE_IDLE && scsi.scsi_state == SCSI_STATE_IDLE)
                        temp |= STATUS_IDLE;
                break;

        case 1: /*Data in register*/
                scsi.status &= unchecked((uint8_t)~STATUS_DF);
                temp = scsi.data_in;
                break;

        case 2: /*Interrupt status register*/
                temp = (uint8_t)(scsi.isr & ~0x70);
                break;

        case 3: /*???*/
                scsi.reg3_idx++;
                switch (scsi.reg3_idx & 3)
                {
                case 0:
                        temp = (uint8_t)'A';
                        break;
                case 1:
                        temp = (uint8_t)'D';
                        break;
                case 2:
                        temp = (uint8_t)'A';
                        break;
                case 3:
                        temp = (uint8_t)'P';
                        break;
                }
                break;
        }

        return temp;
    }

    // pcem: scsi_aha1540.c:392-405
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

    // pcem: scsi_aha1540.c:407-533
    // pcem bug, reproduced: PB-253 — le CCB lu en maître de bus : canal DMA, masque, mode et 8237 ignorés.
    private static void process_cdb(aha154x_t scsi)
    {
        int c;
        uint8_t temp;

        scsi.ccb.opcode = mem_readb_phys(scsi.ccb.addr);
        switch (scsi.ccb.opcode)
        {
        case CCB_INITIATOR:
        case CCB_INITIATOR_RESIDUAL:
                temp = mem_readb_phys(scsi.ccb.addr + 0x01);
                // pcem bug, reproduced: PB-144 — les bits 3 et 4, rangés, jamais lus : ni contrôle de longueur
                //   (12h), ni « aucun transfert » s'ils sont posés tous deux. Le sens, lui, est conforme à la carte.
                scsi.ccb.transfer_dir = (temp >> 3) & 3;
                scsi.ccb.scsi_cmd_len = mem_readb_phys(scsi.ccb.addr + 0x02);
                scsi.ccb.req_sense_len = mem_readb_phys(scsi.ccb.addr + 0x03);

                // pcem bug, reproduced: PB-133 — le LUN n'est jamais transmis au disque (ni IDENTIFY).
                scsi.ccb.lun = temp & 7;
                scsi.ccb.target_id = (temp >> 5) & 7;

                // pcem bug, reproduced: PB-144 — ccb.addr + n sans bouclage à 24 bits (:425-433).
                scsi.ccb.data_len = (uint32_t)(mem_readb_phys(scsi.ccb.addr + 0x06) |
                                               (mem_readb_phys(scsi.ccb.addr + 0x05) << 8) |
                                               (mem_readb_phys(scsi.ccb.addr + 0x04) << 16));
                scsi.ccb.data_pointer = (uint32_t)(mem_readb_phys(scsi.ccb.addr + 0x09) |
                                                   (mem_readb_phys(scsi.ccb.addr + 0x08) << 8) |
                                                   (mem_readb_phys(scsi.ccb.addr + 0x07) << 16));
                scsi.ccb.link_pointer = (uint32_t)(mem_readb_phys(scsi.ccb.addr + 0x0c) |
                                                   (mem_readb_phys(scsi.ccb.addr + 0x0b) << 8) |
                                                   (mem_readb_phys(scsi.ccb.addr + 0x0a) << 16));
                scsi.ccb.link_id = mem_readb_phys(scsi.ccb.addr + 0x0d);
                for (c = 0; c < scsi.ccb.scsi_cmd_len; c++)
                        scsi.ccb.cdb[c] = mem_readb_phys(scsi.ccb.addr + 0x12 + (uint32_t)c);
                scsi.ccb_state = CCB_STATE_SEND_COMMAND;
                scsi.ccb.status = 0;
                scsi.ccb.from_mailbox = 1;
                scsi.ccb.residual = (scsi.ccb.opcode == CCB_INITIATOR_RESIDUAL) ? 1 : 0;
                scsi.ccb.total_data_len = scsi.ccb.data_len;
                break;

        case CCB_INITIATOR_SCATTER_GATHER:
        case CCB_INITIATOR_SCATTER_GATHER_RESIDUAL:
                temp = mem_readb_phys(scsi.ccb.addr + 0x01);
                scsi.ccb.transfer_dir = (temp >> 3) & 3;
                scsi.ccb.scsi_cmd_len = mem_readb_phys(scsi.ccb.addr + 0x02);
                scsi.ccb.req_sense_len = mem_readb_phys(scsi.ccb.addr + 0x03);

                // pcem bug, reproduced: PB-133 — de même pour un CCB à liste de dispersion.
                scsi.ccb.lun = temp & 7;
                scsi.ccb.target_id = (temp >> 5) & 7;

                scsi.ccb.data_seg_list_len = (uint32_t)(mem_readb_phys(scsi.ccb.addr + 0x06) |
                                                        (mem_readb_phys(scsi.ccb.addr + 0x05) << 8) |
                                                        (mem_readb_phys(scsi.ccb.addr + 0x04) << 16));
                scsi.ccb.data_seg_list_pointer = (uint32_t)(mem_readb_phys(scsi.ccb.addr + 0x09) |
                                                            (mem_readb_phys(scsi.ccb.addr + 0x08) << 8) |
                                                            (mem_readb_phys(scsi.ccb.addr + 0x07) << 16));
                scsi.ccb.data_seg_list_idx = 0;

                scsi.ccb.link_pointer = (uint32_t)(mem_readb_phys(scsi.ccb.addr + 0x0c) |
                                                   (mem_readb_phys(scsi.ccb.addr + 0x0b) << 8) |
                                                   (mem_readb_phys(scsi.ccb.addr + 0x0a) << 16));
                scsi.ccb.link_id = mem_readb_phys(scsi.ccb.addr + 0x0d);
                for (c = 0; c < scsi.ccb.scsi_cmd_len; c++)
                        scsi.ccb.cdb[c] = mem_readb_phys(scsi.ccb.addr + 0x12 + (uint32_t)c);
                scsi.ccb_state = CCB_STATE_SEND_COMMAND;
                scsi.ccb.status = 0;
                scsi.ccb.from_mailbox = 1;
                scsi.ccb.residual = (scsi.ccb.opcode == CCB_INITIATOR_SCATTER_GATHER_RESIDUAL) ? 1 : 0;

                if (scsi.ccb.residual != 0)
                {
                        /*Read total data length from scatter list, to use when calculating residual*/
                        uint32_t addr = scsi.ccb.data_seg_list_pointer;

                        scsi.ccb.total_data_len = 0;

                        // Une liste de 0xFFFFFF octets fait 2,8 millions de tours dans une échéance : borné, reproduit.
                        for (c = 0; (uint32_t)c < scsi.ccb.data_seg_list_len; c += 6)
                        {
                                uint32_t len = (uint32_t)(mem_readb_phys(addr + (uint32_t)c + 2) | (mem_readb_phys(addr + (uint32_t)c + 1) << 8) |
                                                          (mem_readb_phys(addr + (uint32_t)c) << 16));

                                scsi.ccb.total_data_len += len;
                        }
                }
                break;

        default:
                // pcem bug, not reproduced: PB-135 — fatal("Unknown CCD opcode %02x\n") (:530) : un CCB d'opcode
                //   autre que 0, 2, 3 ou 4 (le mode cible, 1). L'oracle continue, le MBO reste START et repart.
                // DEVIATION: le CCB finit en erreur sans transaction, par le chemin de la sélection ratée
                //   (:1490-1521), au statut d'hôte 16h (opcode de CCB invalide).
                R9.Garde("scsi_aha1540.c:530");
                mem_writeb_phys(scsi.ccb.addr + 0xe, 0x16);
                if (scsi.current_mbo_is_bios != 0)
                {
                        mem_writeb_phys(scsi.bios_mba + (uint32_t)(scsi.current_mbo * 4), COMMAND_FREE_CCB);
                        mem_writeb_phys(scsi.ccb.addr + 0xd, MBI_CCB_COMPLETE_WITH_ERROR);
                        if (scsi.mbo_irq_enable != 0)
                                set_irq(scsi, ISR_MBOA);
                }
                else
                {
                        mem_writeb_phys(scsi.mba + (uint32_t)(scsi.current_mbo * 4), COMMAND_FREE_CCB);

                        mem_writeb_phys(scsi.mba_i + (uint32_t)(scsi.current_mbi * 4), MBI_CCB_COMPLETE_WITH_ERROR);
                        mem_writeb_phys(scsi.mba_i + (uint32_t)(scsi.current_mbi * 4) + 1, (uint8_t)(scsi.ccb.addr >> 16));
                        mem_writeb_phys(scsi.mba_i + (uint32_t)(scsi.current_mbi * 4) + 2, (uint8_t)(scsi.ccb.addr >> 8));
                        mem_writeb_phys(scsi.mba_i + (uint32_t)(scsi.current_mbi * 4) + 3, (uint8_t)(scsi.ccb.addr & 0xff));
                        scsi.current_mbi++;
                        if (scsi.current_mbi >= scsi.mbc)
                                scsi.current_mbi = 0;
                        set_irq(scsi, (uint8_t)((scsi.mbo_irq_enable != 0 ? ISR_MBOA : 0) | ISR_MBIF));
                }
                break;
        }
    }

    // pcem: scsi_aha1540.c:535-1369
    // pcem bug, reproduced: PB-253 — 03h, 1Ah et 1Bh en maître de bus : canal DMA, masque, mode et 8237 ignorés.
    private static void process_cmd(aha154x_t scsi)
    {
        uint32_t addr = 0;
        int c;

        switch (scsi.cmd_state)
        {
        case CMD_STATE_RESET:
                scsi.status = STATUS_INIT;
                scsi.cmd_state = CMD_STATE_IDLE;
                break;

        case CMD_STATE_IDLE:
                if ((scsi.status & STATUS_CDF) != 0)
                {
                        int invalid = 0;

                        scsi.status &= unchecked((uint8_t)~STATUS_CDF);

                        scsi.command = scsi.cmd_data;
                        commandes_vues[scsi.command]++;
                        // Les branches de la BT-545S sont omises : sur la 1542C, leurs commandes sont invalides.
                        switch (scsi.command)
                        {
                        case COMMAND_NOP:
                        case COMMAND_START_SCSI_COMMAND:
                        case COMMAND_ADAPTER_INQUIRY:
                        case COMMAND_INQUIRE_INSTALLED_DEVICES:
                        case COMMAND_RETURN_CONFIG_DATA:
                                scsi.command_len = 0;
                                break;

                        case COMMAND_ADAPTEC_SET_BIOS_BANK_1:
                        case COMMAND_ADAPTEC_SET_BIOS_BANK_2:
                        case COMMAND_ADAPTEC_GET_EXT_BIOS_INFO:
                        case COMMAND_ADAPTEC_START_BIOS_SCSI_CMD:
                                scsi.command_len = 0;
                                break;

                        case COMMAND_BUSLOGIC_84:
                        case COMMAND_BUSLOGIC_85:
                                invalid = 1;
                                break;

                        case COMMAND_ECHO:
                        case COMMAND_MAILBOX_OUT_IRQ_ENA:
                        case COMMAND_SET_BUS_ON_TIME:
                        case COMMAND_SET_BUS_OFF_TIME:
                        case COMMAND_SET_AT_BUS_SPEED:
                        case COMMAND_RETURN_SETUP_DATA:
                                scsi.command_len = 1;
                                break;

                        case COMMAND_ADAPTEC_SET_SHADOW_PARAMS:
                                scsi.command_len = 1;
                                break;

                        case 0x22:
                                scsi.command_len = 35; /*COMMAND_ADAPTEC_PROGRAM_EEPROM*/
                                break;

                        case COMMAND_BUSLOGIC_SET_CCB_FORMAT:
                        case COMMAND_BUSLOGIC_STRICT_RR:
                        case COMMAND_BUSLOGIC_EXTENDED_SETUP_INFO:
                        case COMMAND_BUSLOGIC_ADAPTER_MODEL_NR:
                        case COMMAND_BUSLOGIC_INQUIRY_SYNC_PERIOD:
                                invalid = 1;
                                break;

                        case COMMAND_ENABLE_MAILBOX_INTERFACE:
                                scsi.command_len = 2;
                                break;

                        case COMMAND_WRITE_CHANNEL_2_BUF:
                        case COMMAND_READ_CHANNEL_2_BUF:
                                scsi.command_len = 3;
                                break;

                        case COMMAND_SET_SELECTION_TIMEOUT:
                                scsi.command_len = 4;
                                break;

                        case COMMAND_ADAPTEC_RETURN_EEPROM_DATA:
                                scsi.command_len = 3;
                                break;

                        case COMMAND_MAILBOX_INITIALIZATION:
                                scsi.command_len = 4;
                                break;

                        case COMMAND_BUSLOGIC_81:
                                invalid = 1;
                                break;

                        case COMMAND_START_BIOS_COMMAND:
                                scsi.command_len = 10;
                                scsi.bios_cmd_state = 0;
                                break;

                        case COMMAND_ADAPTEC_BIOS_MAILBOX_INIT:
                                scsi.command_len = 4;
                                break;

                        default:
                                if ((scsi.command > 0xd && scsi.command < 0x1a) ||
                                    (scsi.command > 0x2a && scsi.command != 0x82))
                                        invalid = 1;
                                else
                                {
                                        // pcem bug, not reproduced: PB-135 — fatal("Bad AHA154x command %02x\n") (:679) :
                                        //   0Ch, 1Ch, 1Dh, 1Eh, 20h, 21h, 2Ah. L'oracle continue, puis :1337 à chaque échéance.
                                        // DEVIATION: le chemin `invalid` du micrologiciel (INVDCMD, HACC).
                                        R9.Garde("scsi_aha1540.c:679");
                                        invalid = 1;
                                }
                                break;
                        }

                        if (invalid != 0)
                        {
                                scsi.status |= STATUS_INVDCMD;
                                set_irq(scsi, ISR_HACC);
                                scsi.cmd_state = CMD_STATE_IDLE;
                        }
                        else if (scsi.command_len == 0)
                                scsi.cmd_state = CMD_STATE_CMD_IN_PROGRESS;
                        else
                        {
                                scsi.cmd_state = CMD_STATE_GET_PARAMS;
                                scsi.cur_param = 0;
                        }
                }
                if (scsi.cmd_state != CMD_STATE_CMD_IN_PROGRESS)
                {
                        break;
                }
                /*Fallthrough*/
                goto case CMD_STATE_CMD_IN_PROGRESS;

        case CMD_STATE_CMD_IN_PROGRESS:
                switch (scsi.command)
                {
                case COMMAND_NOP:
                        set_irq(scsi, ISR_HACC);
                        scsi.cmd_state = CMD_STATE_IDLE;
                        break;

                case COMMAND_MAILBOX_INITIALIZATION:
                        // pcem bug, reproduced: PB-141 — un compte nul accepté (la carte rend INVDCMD).
                        scsi.mbc = scsi.@params[0];
                        scsi.mba = (uint32_t)(scsi.@params[3] | (scsi.@params[2] << 8) | (scsi.@params[1] << 16));
                        scsi.mba_i = scsi.mba + (uint32_t)(scsi.mbc * 4);
                        scsi.current_mbo = 0;
                        scsi.current_mbi = 0;
                        scsi.status &= unchecked((uint8_t)~STATUS_INIT);
                        set_irq(scsi, ISR_HACC);
                        scsi.cmd_state = CMD_STATE_IDLE;
                        break;

                case COMMAND_START_SCSI_COMMAND:
                        // pcem bug, reproduced: PB-141 — un balayage compté par 02h ; ni HRST, ni SRST, ni 01h
                        //   ne remettent ce compte à zéro.
                        scsi.mbo_req++;
                        scsi.cmd_state = CMD_STATE_IDLE;
                        /*Don't send IRQ until we actually start processing a mailbox*/
                        break;

                case COMMAND_START_BIOS_COMMAND:
                        // pcem bug, reproduced: PB-138 — ni from_mailbox ni current_mbo posés (sauf 15h) ; ccb.status et
                        //   int_buffer périmés après une sélection ratée.
                        if (scsi.ccb_state != CCB_STATE_IDLE)
                                break;
                        if (scsi.@params[0] >= 0x16)
                        {
                                /*Only commands below 0x16 are implemented*/
                                scsi.status |= STATUS_INVDCMD;
                                set_irq(scsi, ISR_HACC);
                                scsi.cmd_state = CMD_STATE_IDLE;
                        }
                        else
                                switch (scsi.@params[0])
                                {
                                case 0x00: /*Reset Disk System*/
                                        scsi.data_in = 0;
                                        set_irq(scsi, ISR_HACC);
                                        scsi.status |= STATUS_DF;
                                        scsi.cmd_state = CMD_STATE_IDLE;
                                        break;

                                case 0x01: /*Get Status of Last Operation*/
                                        scsi.data_in = 0;
                                        set_irq(scsi, ISR_HACC);
                                        scsi.status |= STATUS_DF;
                                        scsi.cmd_state = CMD_STATE_IDLE;
                                        break;

                                case 0x02: /*Read Sector(s) Into Memory*/
                                        switch (scsi.bios_cmd_state)
                                        {
                                        case 0:
                                        {
                                                int sector = scsi.@params[5];
                                                int head = (scsi.@params[4] & 0xf) | ((scsi.@params[3] & 3) << 4);
                                                int cylinder = (scsi.@params[3] >> 2) | ((scsi.@params[2] & 0xf) << 6);

                                                // pcem bug, reproduced: PB-144 — le secteur sans −1 (:773).
                                                addr = (uint32_t)(sector + (head * 32) + (cylinder * 64 * 32));
                                        }
                                                scsi.ccb.cdb[0] = SCSI_READ_10;
                                                scsi.ccb.cdb[1] = 0;
                                                scsi.ccb.cdb[2] = (uint8_t)(addr >> 24);
                                                scsi.ccb.cdb[3] = (uint8_t)(addr >> 16);
                                                scsi.ccb.cdb[4] = (uint8_t)(addr >> 8);
                                                scsi.ccb.cdb[5] = (uint8_t)(addr & 0xff);
                                                scsi.ccb.cdb[6] = 0;
                                                scsi.ccb.cdb[7] = 0;
                                                scsi.ccb.cdb[8] = scsi.@params[6];
                                                scsi.ccb.cdb[9] = 0;
                                                scsi.ccb.lun = 0;
                                                scsi.ccb.target_id = scsi.@params[1] & 7;
                                                scsi.ccb.scsi_cmd_len = 10;
                                                scsi.ccb.data_pointer =
                                                        (uint32_t)(scsi.@params[9] | (scsi.@params[8] << 8) | (scsi.@params[7] << 16));
                                                scsi.ccb.data_len = (uint32_t)(512 * scsi.@params[6]);
                                                scsi.ccb.req_sense_len = CCB_SENSE_14;
                                                scsi.ccb_state = CCB_STATE_SEND_COMMAND;
                                                scsi.bios_cmd_state++;
                                                break;
                                        case 1:
                                                if (scsi.ccb.status != 0)
                                                {
                                                        // pcem bug, not reproduced: PB-135 — fatal("Read sector fail %02x\n") (:799).
                                                        // DEVIATION: la commande finit en erreur (20h, défaillance du contrôleur).
                                                        R9.Garde("scsi_aha1540.c:799");
                                                        scsi.data_in = 0x20;
                                                }
                                                else
                                                        scsi.data_in = 0x00;
                                                set_irq(scsi, ISR_HACC);
                                                scsi.status |= STATUS_DF;
                                                scsi.cmd_state = CMD_STATE_IDLE;
                                                break;
                                        }
                                        break;

                                case 0x03: /*Write Disk Sector(s)*/
                                        switch (scsi.bios_cmd_state)
                                        {
                                        case 0:
                                        {
                                                int sector = scsi.@params[5];
                                                int head = (scsi.@params[4] & 0xf) | ((scsi.@params[3] & 3) << 4);
                                                int cylinder = (scsi.@params[3] >> 2) | ((scsi.@params[2] & 0xf) << 6);

                                                // pcem bug, reproduced: PB-144 — le secteur sans −1 (:815).
                                                addr = (uint32_t)(sector + (head * 32) + (cylinder * 64 * 32));
                                        }
                                                scsi.ccb.cdb[0] = SCSI_WRITE_10;
                                                scsi.ccb.cdb[1] = 0;
                                                scsi.ccb.cdb[2] = (uint8_t)(addr >> 24);
                                                scsi.ccb.cdb[3] = (uint8_t)(addr >> 16);
                                                scsi.ccb.cdb[4] = (uint8_t)(addr >> 8);
                                                scsi.ccb.cdb[5] = (uint8_t)(addr & 0xff);
                                                scsi.ccb.cdb[6] = 0;
                                                scsi.ccb.cdb[7] = 0;
                                                scsi.ccb.cdb[8] = scsi.@params[6];
                                                scsi.ccb.cdb[9] = 0;
                                                scsi.ccb.lun = 0;
                                                scsi.ccb.target_id = scsi.@params[1] & 7;
                                                scsi.ccb.scsi_cmd_len = 10;
                                                scsi.ccb.data_pointer =
                                                        (uint32_t)(scsi.@params[9] | (scsi.@params[8] << 8) | (scsi.@params[7] << 16));
                                                scsi.ccb.data_len = (uint32_t)(512 * scsi.@params[6]);
                                                scsi.ccb.req_sense_len = CCB_SENSE_14;
                                                scsi.ccb_state = CCB_STATE_SEND_COMMAND;
                                                scsi.bios_cmd_state++;
                                                break;
                                        case 1:
                                                if (scsi.ccb.status != 0)
                                                {
                                                        // pcem bug, not reproduced: PB-135 — fatal("Write sector fail %02x\n") (:841).
                                                        // DEVIATION: la commande finit en erreur (20h), jamais annoncée réussie.
                                                        R9.Garde("scsi_aha1540.c:841");
                                                        scsi.data_in = 0x20;
                                                }
                                                else
                                                        scsi.data_in = 0x00;
                                                set_irq(scsi, ISR_HACC);
                                                scsi.status |= STATUS_DF;
                                                scsi.cmd_state = CMD_STATE_IDLE;
                                                break;
                                        }
                                        break;

                                case 0x04: /*Verify Disk Sector(s)*/
                                        switch (scsi.bios_cmd_state)
                                        {
                                        case 0:
                                        {
                                                int sector = scsi.@params[5];
                                                int head = (scsi.@params[4] & 0xf) | ((scsi.@params[3] & 3) << 4);
                                                int cylinder = (scsi.@params[3] >> 2) | ((scsi.@params[2] & 0xf) << 6);

                                                // pcem bug, reproduced: PB-144 — le secteur sans −1 (:857).
                                                addr = (uint32_t)(sector + (head * 32) + (cylinder * 64 * 32));
                                        }
                                                scsi.ccb.cdb[0] = SCSI_VERIFY_10;
                                                scsi.ccb.cdb[1] = 0;
                                                scsi.ccb.cdb[2] = (uint8_t)(addr >> 24);
                                                scsi.ccb.cdb[3] = (uint8_t)(addr >> 16);
                                                scsi.ccb.cdb[4] = (uint8_t)(addr >> 8);
                                                scsi.ccb.cdb[5] = (uint8_t)(addr & 0xff);
                                                scsi.ccb.cdb[6] = 0;
                                                scsi.ccb.cdb[7] = 0;
                                                scsi.ccb.cdb[8] = scsi.@params[6];
                                                scsi.ccb.cdb[9] = 0;
                                                scsi.ccb.lun = 0;
                                                scsi.ccb.target_id = scsi.@params[1] & 7;
                                                scsi.ccb.scsi_cmd_len = 10;
                                                scsi.ccb.data_pointer =
                                                        (uint32_t)(scsi.@params[9] | (scsi.@params[8] << 8) | (scsi.@params[7] << 16));
                                                scsi.ccb.data_len = (uint32_t)(512 * scsi.@params[6]);
                                                scsi.ccb.req_sense_len = CCB_SENSE_14;
                                                scsi.ccb_state = CCB_STATE_SEND_COMMAND;
                                                scsi.bios_cmd_state++;
                                                break;
                                        case 1:
                                                if (scsi.ccb.status != 0)
                                                {
                                                        // pcem bug, not reproduced: PB-135 — fatal("Verify sector fail %02x\n") (:883).
                                                        // DEVIATION: la commande finit en erreur (20h).
                                                        R9.Garde("scsi_aha1540.c:883");
                                                        scsi.data_in = 0x20;
                                                }
                                                else
                                                        scsi.data_in = 0x00;
                                                set_irq(scsi, ISR_HACC);
                                                scsi.status |= STATUS_DF;
                                                scsi.cmd_state = CMD_STATE_IDLE;
                                                break;
                                        }
                                        break;

                                        /*AHA1542C firmware just fails all of these*/
                                case 0x05:
                                case 0x06:
                                case 0x07:
                                case 0x0a:
                                case 0x0b:
                                case 0x12:
                                case 0x13:
                                        scsi.status |= STATUS_INVDCMD;
                                        set_irq(scsi, ISR_HACC);
                                        scsi.cmd_state = CMD_STATE_IDLE;
                                        break;

                                case 0x08: /*Get Drive Parameters*/
                                        switch (scsi.bios_cmd_state)
                                        {
                                        case 0:
                                                scsi.ccb.cdb[0] = SCSI_READ_CAPACITY_10;
                                                scsi.ccb.cdb[1] = 0;
                                                scsi.ccb.cdb[2] = 0;
                                                scsi.ccb.cdb[3] = 0;
                                                scsi.ccb.cdb[4] = 0;
                                                scsi.ccb.cdb[5] = 0;
                                                scsi.ccb.cdb[6] = 0;
                                                scsi.ccb.cdb[7] = 0;
                                                scsi.ccb.cdb[8] = 0;
                                                scsi.ccb.cdb[9] = 0;
                                                scsi.ccb.lun = 0;
                                                scsi.ccb.target_id = scsi.@params[1] & 7;
                                                scsi.ccb.scsi_cmd_len = 10;
                                                scsi.ccb.data_pointer = 0xFFFFFFFF;
                                                scsi.ccb.data_len = 8;
                                                scsi.ccb.req_sense_len = CCB_SENSE_NONE;
                                                scsi.ccb_state = CCB_STATE_SEND_COMMAND;
                                                scsi.bios_cmd_state++;
                                                break;
                                        case 1:
                                                addr = (uint32_t)(scsi.@params[9] | (scsi.@params[8] << 8) | (scsi.@params[7] << 16));
                                                mem_writeb_phys(addr, scsi.int_buffer[0]);
                                                mem_writeb_phys(addr + 1, scsi.int_buffer[1]);
                                                mem_writeb_phys(addr + 2, scsi.int_buffer[2]);
                                                mem_writeb_phys(addr + 3, scsi.int_buffer[3]);
                                                scsi.data_in = 0;
                                                set_irq(scsi, ISR_HACC);
                                                scsi.status |= STATUS_DF;
                                                scsi.cmd_state = CMD_STATE_IDLE;
                                                break;
                                        }
                                        break;

                                case 0x09: /*Initialize Drive Parameters*/
                                case 0x0c: /*Seek To Cylinder*/
                                case 0x0d: /*Reset Hard Disks*/
                                case 0x0e: /*Read Sector Buffer*/
                                case 0x0f: /*Write Sector Buffer*/
                                case 0x10: /*Check If Drive Ready*/
                                case 0x11: /*Recalibrate Drive*/
                                case 0x14: /*Controller Internal Diagnostic*/
                                        scsi.data_in = 0;
                                        set_irq(scsi, ISR_HACC);
                                        scsi.status |= STATUS_DF;
                                        scsi.cmd_state = CMD_STATE_IDLE;
                                        break;

                                case 0x15: /*Get Disk Type*/
                                        switch (scsi.bios_cmd_state)
                                        {
                                        case 0:
                                                scsi.ccb.cdb[0] = SCSI_INQUIRY;
                                                scsi.ccb.cdb[1] = 0;
                                                scsi.ccb.cdb[2] = 0;
                                                scsi.ccb.cdb[3] = 0;
                                                scsi.ccb.cdb[4] = 36;
                                                scsi.ccb.cdb[5] = 0;
                                                scsi.ccb.lun = 0;
                                                scsi.ccb.target_id = scsi.@params[1] & 7;
                                                scsi.ccb.scsi_cmd_len = 6;
                                                scsi.ccb.data_pointer = 0xFFFFFFFF;
                                                scsi.ccb.data_len = 36;
                                                scsi.ccb.req_sense_len = CCB_SENSE_NONE;
                                                scsi.ccb_state = CCB_STATE_SEND_COMMAND;
                                                scsi.ccb.from_mailbox = 0;
                                                scsi.bios_cmd_state++;
                                                break;
                                        case 1:
                                                if ((scsi.int_buffer[0] & 0x1f) != 0)
                                                {
                                                        /*Not a direct-access device*/
                                                        // pcem bug, not reproduced: PB-135 — fatal("Not a direct-access device\n") (:980) :
                                                        //   une cible non disque, ou un ID vide après le sense d'un CCB de mailbox (70h).
                                                        // DEVIATION: comme les commandes BIOS refusées (:900-902), INVDCMD et HACC.
                                                        R9.Garde("scsi_aha1540.c:980");
                                                        scsi.status |= STATUS_INVDCMD;
                                                        set_irq(scsi, ISR_HACC);
                                                        scsi.cmd_state = CMD_STATE_IDLE;
                                                        break;
                                                }
                                                scsi.ccb.cdb[0] = SCSI_READ_CAPACITY_10;
                                                scsi.ccb.cdb[1] = 0;
                                                scsi.ccb.cdb[2] = 0;
                                                scsi.ccb.cdb[3] = 0;
                                                scsi.ccb.cdb[4] = 0;
                                                scsi.ccb.cdb[5] = 0;
                                                scsi.ccb.cdb[6] = 0;
                                                scsi.ccb.cdb[7] = 0;
                                                scsi.ccb.cdb[8] = 0;
                                                scsi.ccb.cdb[9] = 0;
                                                scsi.ccb.lun = 0;
                                                scsi.ccb.target_id = scsi.@params[1] & 7;
                                                scsi.ccb.scsi_cmd_len = 10;
                                                scsi.ccb.data_pointer = 0xFFFFFFFF;
                                                scsi.ccb.data_len = 8;
                                                scsi.ccb.req_sense_len = CCB_SENSE_NONE;
                                                scsi.ccb_state = CCB_STATE_SEND_COMMAND;
                                                scsi.bios_cmd_state++;
                                                break;
                                        case 2:
                                                addr = (uint32_t)(scsi.@params[9] | (scsi.@params[8] << 8) | (scsi.@params[7] << 16));
                                                mem_writeb_phys(addr, scsi.int_buffer[0]);
                                                mem_writeb_phys(addr + 1, scsi.int_buffer[1]);
                                                mem_writeb_phys(addr + 2, scsi.int_buffer[2]);
                                                mem_writeb_phys(addr + 3, scsi.int_buffer[3]);
                                                scsi.data_in = 0;
                                                set_irq(scsi, ISR_HACC);
                                                scsi.status |= STATUS_DF;
                                                scsi.cmd_state = CMD_STATE_IDLE;
                                                break;

                                        default:
                                                // pcem bug, reproduced: PB-134 — état jamais posé (:1016).
                                                pc.fatal($"Get Disk Type state {scsi.bios_cmd_state}\n");
                                                break;
                                        }
                                        break;

                                default:
                                        scsi.status |= STATUS_INVDCMD;
                                        set_irq(scsi, ISR_HACC);
                                        scsi.cmd_state = CMD_STATE_IDLE;
                                        break;
                                }
                        break;

                case COMMAND_ADAPTER_INQUIRY:
                        scsi.result[0] = 0x44; /*AHA-1542CF*/
                        scsi.result[1] = 0x30;
                        scsi.result[2] = 0x30; /*Firmware revision*/
                        scsi.result[3] = 0x31;
                        scsi.cmd_state = CMD_STATE_SEND_RESULT;
                        scsi.result_pos = 0;
                        scsi.result_len = 4;
                        break;

                case COMMAND_MAILBOX_OUT_IRQ_ENA:
                        scsi.mbo_irq_enable = scsi.@params[0];
                        set_irq(scsi, ISR_HACC);
                        scsi.cmd_state = CMD_STATE_IDLE;
                        break;

                case COMMAND_SET_SELECTION_TIMEOUT:
                        // pcem bug, reproduced: PB-144 — délai de sélection, temps de bus, vitesse : sans effet.
                        scsi.e_d = scsi.@params[0];
                        scsi.to = (uint16_t)((scsi.@params[1] << 8) | scsi.@params[2]);
                        set_irq(scsi, ISR_HACC);
                        scsi.cmd_state = CMD_STATE_IDLE;
                        break;

                case COMMAND_SET_BUS_ON_TIME:
                        scsi.bon = scsi.@params[0];
                        set_irq(scsi, ISR_HACC);
                        scsi.cmd_state = CMD_STATE_IDLE;
                        break;

                case COMMAND_SET_BUS_OFF_TIME:
                        scsi.boff = scsi.@params[0];
                        set_irq(scsi, ISR_HACC);
                        scsi.cmd_state = CMD_STATE_IDLE;
                        break;

                case COMMAND_SET_AT_BUS_SPEED:
                        scsi.atbs = scsi.@params[0];
                        set_irq(scsi, ISR_HACC);
                        scsi.cmd_state = CMD_STATE_IDLE;
                        break;

                case COMMAND_INQUIRE_INSTALLED_DEVICES:
                        for (c = 0; c < 8; c++)
                                scsi.result[c] = (uint8_t)(scsi.bus.devices[c] is not null ? 1 : 0);
                        scsi.cmd_state = CMD_STATE_SEND_RESULT;
                        scsi.result_pos = 0;
                        scsi.result_len = 8;
                        break;

                case COMMAND_RETURN_CONFIG_DATA:
                        scsi.result[0] = (uint8_t)(1 << scsi.dma);
                        scsi.result[1] = (uint8_t)(1 << (scsi.irq - 9));
                        scsi.result[2] = (uint8_t)scsi.host_id;
                        scsi.cmd_state = CMD_STATE_SEND_RESULT;
                        scsi.result_pos = 0;
                        scsi.result_len = 3;
                        break;

                case COMMAND_ECHO:
                        scsi.result[0] = scsi.@params[0];
                        scsi.cmd_state = CMD_STATE_SEND_RESULT;
                        scsi.result_pos = 0;
                        scsi.result_len = 1;
                        break;

                case COMMAND_RETURN_SETUP_DATA:
                        scsi.result[0] = 0x00;                       /*SPS*/
                        scsi.result[1] = scsi.atbs;                  /*AT bus speed*/
                        scsi.result[2] = scsi.bon;                   /*BON*/
                        scsi.result[3] = scsi.boff;                  /*BOFF*/
                        scsi.result[4] = (uint8_t)scsi.mbc;          /*DS:E8 - mailbox count*/
                        scsi.result[5] = (uint8_t)(scsi.mba >> 16);  /*DS:DA - set by command 0x81?*/
                        scsi.result[6] = (uint8_t)(scsi.mba >> 8);   /*DS:DB*/
                        scsi.result[7] = (uint8_t)(scsi.mba & 0xff); /*DS:DC*/
                        scsi.result[8] = 0x00;                       /*STA0*/
                        scsi.result[9] = 0x00;                       /*STA1*/
                        scsi.result[10] = 0x00;                      /*STA2*/
                        scsi.result[11] = 0x00;                      /*STA3*/
                        scsi.result[12] = 0x00;                      /*STA4*/
                        scsi.result[13] = 0x00;                      /*STA5*/
                        scsi.result[14] = 0x00;                      /*STA6*/
                        scsi.result[15] = 0x00;                      /*STA7*/
                        scsi.result[16] = 0x00;                      /*DS (DS:1e5b)*/
                        for (c = 0; c < 20; c++)
                                scsi.result[c + 17] = 0;             /*Customer signature*/
                        scsi.result[37] = 0;                         /*Auto-retry*/
                        scsi.result[38] = 0;                         /*Board switches*/
                        scsi.result[39] = 0xa3;                      /*Checksum*/
                        scsi.result[40] = 0xc2;
                        scsi.result[41] = (uint8_t)(scsi.bios_mba >> 16);
                        scsi.result[42] = (uint8_t)(scsi.bios_mba >> 8);
                        scsi.result[43] = (uint8_t)(scsi.bios_mba & 0xff);
                        scsi.cmd_state = CMD_STATE_SEND_RESULT;
                        scsi.result_pos = 0;
                        // pcem bug, reproduced: PB-140 — tronqué à 20 octets, la suite mise à zéro.
                        scsi.result_len = Math.Min((int)scsi.@params[0], 20);
                        for (; scsi.result_len < scsi.@params[0]; scsi.result_len++)
                                scsi.result[scsi.result_len] = 0;
                        break;

                case COMMAND_WRITE_CHANNEL_2_BUF:
                        addr = (uint32_t)(scsi.@params[2] | (scsi.@params[1] << 8) | (scsi.@params[0] << 16));
                        for (c = 0; c < 64; c++)
                                scsi.dma_buffer[c] = mem_readb_phys(addr + (uint32_t)c);
                        set_irq(scsi, ISR_HACC);
                        scsi.cmd_state = CMD_STATE_IDLE;
                        break;

                case COMMAND_READ_CHANNEL_2_BUF:
                        addr = (uint32_t)(scsi.@params[2] | (scsi.@params[1] << 8) | (scsi.@params[0] << 16));
                        for (c = 0; c < 64; c++)
                                mem_writeb_phys(addr + (uint32_t)c, scsi.dma_buffer[c]);
                        set_irq(scsi, ISR_HACC);
                        scsi.cmd_state = CMD_STATE_IDLE;
                        break;

                case 0x22:
                        /*COMMAND_ADAPTEC_PROGRAM_EEPROM*/
                        // pcem bug, reproduced: PB-136 — params[c + 3] pour c < params[1] (jusqu'à 255) : au-delà de
                        //   params[64], le struct C (param_lu). L'IRQ change même avec une interruption en attente.
                        for (c = 0; c < scsi.@params[1]; c++)
                                scsi.eeprom[(scsi.@params[2] + c) & 0xff] = param_lu(scsi, c + 3);
                        scsi.host_id = scsi.eeprom[0] & 7;
                        scsi.dma = (scsi.eeprom[1] >> 4) & 7;
                        scsi.irq = (scsi.eeprom[1] & 7) + 9;

                        aha1542c_eeprom_save(scsi);
                        set_irq(scsi, ISR_HACC);
                        scsi.cmd_state = CMD_STATE_IDLE;
                        break;

                case COMMAND_ADAPTEC_RETURN_EEPROM_DATA:
                        // pcem bug, reproduced: PB-218 — l'octet 0 est ignoré (1 : la configuration ;
                        //   0 : les options d'usine) : la configuration est toujours rendue.
                        for (c = 0; c < scsi.@params[1]; c++)
                        {
                                scsi.result[c] = scsi.eeprom[(scsi.@params[2] + c) & 0xff];
                        }
                        scsi.cmd_state = CMD_STATE_SEND_RESULT;
                        scsi.result_pos = 0;
                        scsi.result_len = c;
                        break;

                case COMMAND_ADAPTEC_SET_SHADOW_PARAMS:
                        scsi.shadow = scsi.@params[0];
                        set_irq(scsi, ISR_HACC);
                        scsi.cmd_state = CMD_STATE_IDLE;
                        break;

                case COMMAND_ADAPTEC_BIOS_MAILBOX_INIT:
                        scsi.bios_mbc = scsi.@params[0];
                        scsi.bios_mba = (uint32_t)(scsi.@params[3] | (scsi.@params[2] << 8) | (scsi.@params[1] << 16));
                        scsi.bios_mbo_inited = 1;
                        set_irq(scsi, ISR_HACC);
                        scsi.cmd_state = CMD_STATE_IDLE;
                        break;

                case COMMAND_ADAPTEC_SET_BIOS_BANK_1:
                        mem_mapping_set_exec(scsi.mapping, scsi.bios_rom.rom, 0);
                        scsi.bios_bank = 0x0000;
                        set_irq(scsi, ISR_HACC);
                        scsi.cmd_state = CMD_STATE_IDLE;
                        break;

                case COMMAND_ADAPTEC_SET_BIOS_BANK_2:
                        mem_mapping_set_exec(scsi.mapping, scsi.bios_rom.rom, 0x4000);
                        scsi.bios_bank = 0x4000;
                        set_irq(scsi, ISR_HACC);
                        scsi.cmd_state = CMD_STATE_IDLE;
                        break;

                case COMMAND_ADAPTEC_GET_EXT_BIOS_INFO:
                        scsi.result[0] = 0;
                        scsi.result[1] = scsi.mblt;
                        scsi.cmd_state = CMD_STATE_SEND_RESULT;
                        scsi.result_pos = 0;
                        scsi.result_len = 2;
                        break;

                case COMMAND_ENABLE_MAILBOX_INTERFACE:
                        scsi.mbu = scsi.@params[0];
                        scsi.mblt = scsi.@params[1];
                        set_irq(scsi, ISR_HACC);
                        scsi.cmd_state = CMD_STATE_IDLE;
                        break;

                case COMMAND_ADAPTEC_START_BIOS_SCSI_CMD:
                        scsi.bios_mbo_req++;
                        scsi.cmd_state = CMD_STATE_IDLE;
                        break;

                // omitted: COMMAND_BUSLOGIC_81 à COMMAND_BUSLOGIC_SET_CCB_FORMAT (:1255-1334) — refusées en IDLE.

                default:
                        // pcem bug, reproduced: PB-134 — PCem n'y vient qu'après :679 (:1337), que le C# refuse ici :
                        //   le refus de PB-135.
                        pc.fatal($"Bad AHA154x command in progress {scsi.command:x2}\n");
                        break;
                }
                break;

        case CMD_STATE_GET_PARAMS:
                if ((scsi.status & STATUS_CDF) != 0)
                {
                        scsi.status &= unchecked((uint8_t)~STATUS_CDF);

                        scsi.@params[scsi.cur_param++] = scsi.cmd_data;

                        if (scsi.cur_param == scsi.command_len)
                                scsi.cmd_state = CMD_STATE_CMD_IN_PROGRESS;
                }
                break;

        case CMD_STATE_SEND_RESULT:
                if ((scsi.status & STATUS_DF) == 0)
                {
                        if (scsi.result_pos == scsi.result_len)
                        {
                                set_irq(scsi, ISR_HACC);
                                scsi.cmd_state = CMD_STATE_IDLE;
                        }
                        else
                        {
                                scsi.status |= STATUS_DF;
                                scsi.data_in = scsi.result[scsi.result_pos++];
                        }
                }
                break;

        default:
                // pcem bug, reproduced: PB-134 — un état que la machine ne pose jamais (:1367).
                pc.fatal($"Unknown state {scsi.cmd_state}\n");
                break;
        }
    }

    // pcem: scsi_aha1540.c:1371-1693
    // pcem bug, reproduced: PB-253 — mailbox, CCB et MBI en maître de bus : canal DMA, masque, 8237 ignorés.
    private static void process_ccb(aha154x_t scsi)
    {
        int c;

        switch (scsi.ccb_state)
        {
        case CCB_STATE_IDLE:
                // pcem bug, reproduced: PB-141 — un balayage par 02h compté, toujours depuis l'emplacement 0.
                if ((scsi.status & STATUS_INIT) == 0 && scsi.mbo_req != 0)
                {
                        for (c = 0; c < scsi.mbc; c++)
                        {
                                uint32_t ccb;
                                uint8_t command;

                                uint32_t mbo = mem_readl_phys(scsi.mba + (uint32_t)(c * 4));
                                command = (uint8_t)(mbo & 0xff);
                                ccb = (mbo >> 24) | ((mbo >> 8) & 0xff00) | ((mbo << 8) & 0xff0000);

                                if (command == COMMAND_START_CCB)
                                {
                                        scsi.current_mbo = c;
                                        scsi.current_mbo_is_bios = 0;
                                        scsi.ccb.addr = ccb;

                                        process_cdb(scsi);

                                        scsi.mbo_req--;

                                        break;
                                }
                                else if (command == COMMAND_ABORT_CCB)
                                {
                                        scsi.mbo_req--;

                                        // pcem bug, reproduced: PB-139 — c * 8 et non c * 4 ; et rien n'est interrompu.
                                        mem_writeb_phys(scsi.mba + (uint32_t)(c * 8), 0);

                                        mem_writeb_phys(scsi.mba_i + (uint32_t)(scsi.current_mbi * 4), MBI_CCB_ABORTED);
                                        mem_writeb_phys(scsi.mba_i + (uint32_t)(scsi.current_mbi * 4) + 1, (uint8_t)(ccb >> 16));
                                        mem_writeb_phys(scsi.mba_i + (uint32_t)(scsi.current_mbi * 4) + 2, (uint8_t)(ccb >> 8));
                                        mem_writeb_phys(scsi.mba_i + (uint32_t)(scsi.current_mbi * 4) + 3, (uint8_t)(ccb & 0xff));

                                        scsi.current_mbi++;
                                        if (scsi.current_mbi >= scsi.mbc)
                                                scsi.current_mbi = 0;
                                        set_irq(scsi, (uint8_t)((scsi.mbo_irq_enable != 0 ? ISR_MBOA : 0) | ISR_MBIF | ISR_HACC));
                                        break;
                                }
                        }
                }
                // pcem bug, reproduced: PB-141 — ni l'état CCB ni STATUS_INIT ne gardent ce balayage : un CCB de la
                //   mailbox normale pris à la même échéance est écrasé.
                if (scsi.bios_mbo_inited != 0 && scsi.bios_mbo_req != 0)
                {
                        for (c = 0; c < scsi.bios_mbc; c++)
                        {
                                uint32_t ccb;
                                uint8_t command;

                                uint32_t mbo = mem_readl_phys(scsi.bios_mba + (uint32_t)(c * 4));
                                command = (uint8_t)(mbo & 0xff);
                                ccb = (mbo >> 24) | ((mbo >> 8) & 0xff00) | ((mbo << 8) & 0xff0000);

                                if (command == COMMAND_START_CCB)
                                {
                                        scsi.current_mbo = c;
                                        scsi.current_mbo_is_bios = 1;
                                        scsi.ccb.addr = ccb;

                                        process_cdb(scsi);

                                        scsi.bios_mbo_req--;
                                        set_irq(scsi, ISR_HACC);
                                        break;
                                }
                        }
                }
                break;

        case CCB_STATE_SEND_COMMAND:
                if (scsi.scsi_state != SCSI_STATE_IDLE)
                        break; /*Wait until SCSI state machine is ready for new command*/
                for (c = 0; c < scsi.ccb.scsi_cmd_len; c++)
                {
                        scsi.cdb.data[c] = scsi.ccb.cdb[c];
                }
                scsi.cdb.len = scsi.ccb.scsi_cmd_len;
                scsi.cdb.idx = 0;
                if (scsi.ccb.opcode == CCB_INITIATOR_SCATTER_GATHER ||
                    scsi.ccb.opcode == CCB_INITIATOR_SCATTER_GATHER_RESIDUAL)
                {
                        scsi.cdb.data_seg_list_len = scsi.ccb.data_seg_list_len;
                        scsi.cdb.data_seg_list_pointer = scsi.ccb.data_seg_list_pointer;
                        scsi.cdb.data_seg_list_idx = 0;
                        scsi.cdb.scatter_gather = 1;
                        scsi.cdb.data_len = 0;
                        scsi.cdb.data_idx = 0;
                }
                else
                {
                        scsi.cdb.data_pointer = scsi.ccb.data_pointer;
                        scsi.cdb.data_len = (int)scsi.ccb.data_len;
                        scsi.cdb.scatter_gather = 0;
                }
                scsi.cdb.bytes_transferred = 0;
                scsi.cdb.data_idx = 0;
                scsi.scsi_state = SCSI_STATE_SELECT;
                /*SCSI will now select the device and execute the command*/
                scsi.ccb_state = CCB_STATE_WAIT_COMMAND;
                break;

        case CCB_STATE_WAIT_COMMAND:
                if (scsi.scsi_state == SCSI_STATE_SELECT_FAILED)
                {
                        // pcem bug, reproduced: PB-138 — commande BIOS, cible absente : ni ccb.status ni int_buffer.
                        if (scsi.ccb.from_mailbox != 0)
                        {
                                mem_writeb_phys(scsi.ccb.addr + 0xe, HOST_STATUS_SELECTION_TIME_OUT);

                                if (scsi.current_mbo_is_bios != 0)
                                {
                                        mem_writeb_phys(scsi.bios_mba + (uint32_t)(scsi.current_mbo * 4), COMMAND_FREE_CCB);
                                        mem_writeb_phys(scsi.ccb.addr + 0xd, MBI_CCB_COMPLETE_WITH_ERROR);
                                }
                                else
                                {
                                        mem_writeb_phys(scsi.mba + (uint32_t)(scsi.current_mbo * 4), COMMAND_FREE_CCB);

                                        // pcem bug, reproduced: PB-141 — MBI écrasé s'il est occupé (:1500-1503).
                                        mem_writeb_phys(scsi.mba_i + (uint32_t)(scsi.current_mbi * 4), MBI_CCB_COMPLETE_WITH_ERROR);
                                        mem_writeb_phys(scsi.mba_i + (uint32_t)(scsi.current_mbi * 4) + 1, (uint8_t)(scsi.ccb.addr >> 16));
                                        mem_writeb_phys(scsi.mba_i + (uint32_t)(scsi.current_mbi * 4) + 2, (uint8_t)(scsi.ccb.addr >> 8));
                                        mem_writeb_phys(scsi.mba_i + (uint32_t)(scsi.current_mbi * 4) + 3, (uint8_t)(scsi.ccb.addr & 0xff));
                                }

                                if (scsi.current_mbo_is_bios == 0)
                                {
                                        scsi.current_mbi++;
                                        if (scsi.current_mbi >= scsi.mbc)
                                                scsi.current_mbi = 0;
                                        set_irq(scsi, (uint8_t)((scsi.mbo_irq_enable != 0 ? ISR_MBOA : 0) | ISR_MBIF));
                                }
                                else if (scsi.mbo_irq_enable != 0)
                                        set_irq(scsi, ISR_MBOA);
                        }
                        scsi.ccb_state = CCB_STATE_IDLE;
                        scsi.scsi_state = SCSI_STATE_IDLE;
                        break;
                }
                if (scsi.scsi_state != SCSI_STATE_IDLE)
                        break; /*Wait until SCSI state machine has completed command*/
                scsi.ccb.status = scsi.cdb.last_status;
                scsi.ccb.bytes_transferred = scsi.cdb.bytes_transferred;
                // pcem bug, reproduced: PB-137 — le sense automatique suit toute commande, même GOOD.
                scsi.ccb_state = CCB_STATE_SEND_REQUEST_SENSE;
                break;

        case CCB_STATE_SEND_REQUEST_SENSE:
                if (scsi.ccb.req_sense_len == CCB_SENSE_NONE)
                {
                        if (scsi.ccb.from_mailbox != 0)
                        {
                                uint32_t residue = scsi.ccb.total_data_len - (uint32_t)scsi.ccb.bytes_transferred;

                                mem_writeb_phys(scsi.ccb.addr + 0xe, HOST_STATUS_COMMAND_COMPLETE);
                                mem_writeb_phys(scsi.ccb.addr + 0xf, scsi.ccb.status);

                                if (scsi.ccb.residual != 0)
                                {
                                        mem_writeb_phys(scsi.ccb.addr + 0x4, (uint8_t)(residue >> 16));
                                        mem_writeb_phys(scsi.ccb.addr + 0x5, (uint8_t)(residue >> 8));
                                        mem_writeb_phys(scsi.ccb.addr + 0x6, (uint8_t)(residue & 0xff));
                                }

                                if (scsi.current_mbo_is_bios != 0)
                                {
                                        mem_writeb_phys(scsi.bios_mba + (uint32_t)(scsi.current_mbo * 4), COMMAND_FREE_CCB);

                                        if (scsi.ccb.status == STATUS_GOOD)
                                                mem_writeb_phys(scsi.ccb.addr + 0xd, MBI_CCB_COMPLETE);
                                        else
                                                mem_writeb_phys(scsi.ccb.addr + 0xd, MBI_CCB_COMPLETE_WITH_ERROR);
                                }
                                else
                                {
                                        mem_writeb_phys(scsi.mba + (uint32_t)(scsi.current_mbo * 4), COMMAND_FREE_CCB);

                                        // pcem bug, reproduced: PB-141 — MBI écrasé s'il est occupé (:1565-1571).
                                        if (scsi.ccb.status == STATUS_GOOD)
                                                mem_writeb_phys(scsi.mba_i + (uint32_t)(scsi.current_mbi * 4), MBI_CCB_COMPLETE);
                                        else
                                                mem_writeb_phys(scsi.mba_i + (uint32_t)(scsi.current_mbi * 4),
                                                                MBI_CCB_COMPLETE_WITH_ERROR);
                                        mem_writeb_phys(scsi.mba_i + (uint32_t)(scsi.current_mbi * 4) + 1, (uint8_t)(scsi.ccb.addr >> 16));
                                        mem_writeb_phys(scsi.mba_i + (uint32_t)(scsi.current_mbi * 4) + 2, (uint8_t)(scsi.ccb.addr >> 8));
                                        mem_writeb_phys(scsi.mba_i + (uint32_t)(scsi.current_mbi * 4) + 3, (uint8_t)(scsi.ccb.addr & 0xff));
                                }

                                if (scsi.current_mbo_is_bios == 0)
                                {
                                        scsi.current_mbi++;
                                        if (scsi.current_mbi >= scsi.mbc)
                                                scsi.current_mbi = 0;
                                        set_irq(scsi, (uint8_t)((scsi.mbo_irq_enable != 0 ? ISR_MBOA : 0) | ISR_MBIF));
                                }
                                else if (scsi.mbo_irq_enable != 0)
                                        set_irq(scsi, ISR_MBOA);
                        }
                        scsi.ccb_state = CCB_STATE_IDLE;
                        break;
                }
                /*Build request sense command*/
                // pcem bug, reproduced: PB-137 — après chaque commande, même GOOD ; octet de contrôle 14 ; et la
                //   destination inversée : le CCB de mailbox dans int_buffer, la commande BIOS à ccb.addr périmé.
                scsi.cdb.data[0] = SCSI_REQUEST_SENSE;
                scsi.cdb.data[1] = (uint8_t)(scsi.ccb.lun << 5);
                scsi.cdb.data[2] = 0x00;
                scsi.cdb.data[3] = 0x00;
                if (scsi.ccb.req_sense_len == CCB_SENSE_14)
                        scsi.cdb.data[4] = 14;
                else
                        scsi.cdb.data[4] = (uint8_t)scsi.ccb.req_sense_len;
                scsi.cdb.data[5] = 14;
                scsi.cdb.len = 6;
                scsi.cdb.idx = 0;
                if (scsi.ccb.from_mailbox != 0)
                        scsi.cdb.data_pointer = 0xFFFFFFFF;
                else
                        scsi.cdb.data_pointer = scsi.ccb.addr + 0x12 + (uint32_t)scsi.ccb.scsi_cmd_len;
                scsi.cdb.data_idx = 0;
                scsi.cdb.data_len = scsi.cdb.data[4];
                scsi.cdb.scatter_gather = 0;
                scsi.scsi_state = SCSI_STATE_SELECT;
                scsi.ccb_state = CCB_STATE_WAIT_REQUEST_SENSE;
                break;

        case CCB_STATE_WAIT_REQUEST_SENSE:
                if (scsi.scsi_state != SCSI_STATE_IDLE)
                        break; /*Wait until SCSI state machine has completed command*/

                if (scsi.ccb.from_mailbox != 0)
                {
                        uint32_t residue = scsi.ccb.total_data_len - (uint32_t)scsi.ccb.bytes_transferred;
                        mem_writeb_phys(scsi.ccb.addr + 0xe, HOST_STATUS_COMMAND_COMPLETE);
                        mem_writeb_phys(scsi.ccb.addr + 0xf, scsi.ccb.status);

                        if (scsi.ccb.residual != 0)
                        {
                                mem_writeb_phys(scsi.ccb.addr + 0x4, (uint8_t)(residue >> 16));
                                mem_writeb_phys(scsi.ccb.addr + 0x5, (uint8_t)(residue >> 8));
                                mem_writeb_phys(scsi.ccb.addr + 0x6, (uint8_t)(residue & 0xff));
                        }

                        if (scsi.current_mbo_is_bios != 0)
                        {
                                mem_writeb_phys(scsi.bios_mba + (uint32_t)(scsi.current_mbo * 4), COMMAND_FREE_CCB);

                                if (scsi.ccb.status == STATUS_GOOD)
                                        mem_writeb_phys(scsi.ccb.addr + 0xd, MBI_CCB_COMPLETE);
                                else
                                        mem_writeb_phys(scsi.ccb.addr + 0xd, MBI_CCB_COMPLETE_WITH_ERROR);
                        }
                        else
                        {
                                mem_writeb_phys(scsi.mba + (uint32_t)(scsi.current_mbo * 4), COMMAND_FREE_CCB);

                                // pcem bug, reproduced: PB-141 — MBI écrasé s'il est occupé (:1654-1659).
                                if (scsi.ccb.status == STATUS_GOOD)
                                        mem_writeb_phys(scsi.mba_i + (uint32_t)(scsi.current_mbi * 4), MBI_CCB_COMPLETE);
                                else
                                        mem_writeb_phys(scsi.mba_i + (uint32_t)(scsi.current_mbi * 4), MBI_CCB_COMPLETE_WITH_ERROR);
                                mem_writeb_phys(scsi.mba_i + (uint32_t)(scsi.current_mbi * 4) + 1, (uint8_t)(scsi.ccb.addr >> 16));
                                mem_writeb_phys(scsi.mba_i + (uint32_t)(scsi.current_mbi * 4) + 2, (uint8_t)(scsi.ccb.addr >> 8));
                                mem_writeb_phys(scsi.mba_i + (uint32_t)(scsi.current_mbi * 4) + 3, (uint8_t)(scsi.ccb.addr & 0xff));
                        }

                        if (scsi.current_mbo_is_bios == 0)
                        {
                                scsi.current_mbi++;
                                if (scsi.current_mbi >= scsi.mbc)
                                        scsi.current_mbi = 0;
                                set_irq(scsi, (uint8_t)((scsi.mbo_irq_enable != 0 ? ISR_MBOA : 0) | ISR_MBIF));
                        }
                        else if (scsi.mbo_irq_enable != 0)
                                set_irq(scsi, ISR_MBOA);
                }
                scsi.ccb_state = CCB_STATE_IDLE;
                break;

        default:
                // pcem bug, reproduced: PB-134 — un état que la machine ne pose jamais (:1691).
                pc.fatal($"Unknown CCB_state {scsi.ccb_state}\n");
                break;
        }
    }

    // pcem: scsi_aha1540.c:1695-2057
    // pcem bug, reproduced: PB-253 — les données en maître de bus : canal DMA, masque, mode et 8237 ignorés.
    private static void process_scsi(aha154x_t scsi)
    {
        int c;
        int bytes_transferred = 0;

        switch (scsi.scsi_state)
        {
        case SCSI_STATE_IDLE:
                break;

        case SCSI_STATE_SELECT:
                scsi.cdb.last_status = 0;
                // pcem bug, reproduced: PB-144 — l'ID de l'hôte n'est pas exclu : seul un ID au-delà de 6 est refusé.
                scsi_bus_update(scsi.bus, BUS_SEL | BUS_SETDATA(1 << scsi.ccb.target_id));
                if ((scsi_bus_read(scsi.bus) & BUS_BSY) == 0 || scsi.ccb.target_id > 6)
                {
                        scsi.scsi_state = SCSI_STATE_SELECT_FAILED;
                        break;
                }

                scsi_bus_update(scsi.bus, 0);
                if ((scsi_bus_read(scsi.bus) & BUS_BSY) == 0)
                {
                        scsi.scsi_state = SCSI_STATE_SELECT_FAILED;
                        break;
                }

                /*Device should now be selected*/
                if (wait_for_bus(scsi.bus, BUS_CD, 1) == 0)
                {
                        scsi.scsi_state = SCSI_STATE_SELECT_FAILED;
                        break;
                }

                scsi.scsi_state = SCSI_STATE_SEND_COMMAND;
                break;

        case SCSI_STATE_SELECT_FAILED:
                break;

        case SCSI_STATE_SEND_COMMAND:
                while (scsi.cdb.idx < scsi.cdb.len && bytes_transferred < MAX_BYTES_TRANSFERRED_PER_POLL)
                {
                        int bus_out;

                        for (c = 0; c < 20; c++)
                        {
                                int bus_state = scsi_bus_read(scsi.bus);

                                // pcem bug, reproduced: PB-134 — BSY tombe après l'ACK du message, jamais ici (:1741).
                                if ((bus_state & BUS_BSY) == 0)
                                        pc.fatal("SEND_COMMAND - dropped BSY\n");
                                if ((bus_state & (BUS_IO | BUS_CD | BUS_MSG)) != BUS_CD)
                                {
                                        // pcem bug, not reproduced: PB-135 — fatal("SEND_COMMAND - bus phase incorrect\n")
                                        //   (:1743) : une CDB plus longue que son groupe (scsi.c:21).
                                        // DEVIATION: la carte suit la phase de la cible ; les octets de trop ne partent pas.
                                        R9.Garde("scsi_aha1540.c:1743");
                                        scsi.scsi_state = SCSI_STATE_NEXT_PHASE;
                                        return;
                                }
                                if ((bus_state & BUS_REQ) != 0)
                                        break;
                        }
                        if (c == 20)
                        {
                                break;
                        }

                        bus_out = BUS_SETDATA(scsi.cdb.data[scsi.cdb.idx]);
                        scsi.cdb.idx++;
                        bytes_transferred++;

                        scsi_bus_update(scsi.bus, bus_out | BUS_ACK);
                        scsi_bus_update(scsi.bus, bus_out & ~BUS_ACK);
                }
                if (scsi.cdb.idx == scsi.cdb.len)
                        scsi.scsi_state = SCSI_STATE_NEXT_PHASE;
                break;

        case SCSI_STATE_NEXT_PHASE:
                /*Wait for SCSI command to move to next phase*/
                for (c = 0; c < 20; c++)
                {
                        int bus_state = scsi_bus_read(scsi.bus);

                        // pcem bug, reproduced: PB-134 — BSY tombe après l'ACK du message, jamais ici (:1773).
                        if ((bus_state & BUS_BSY) == 0)
                                pc.fatal("NEXT_PHASE - dropped BSY waiting\n");

                        if ((bus_state & BUS_REQ) != 0)
                        {
                                int bus_out;
                                switch (bus_state & (BUS_IO | BUS_CD | BUS_MSG))
                                {
                                case 0:
                                        scsi.scsi_state = SCSI_STATE_WRITE_DATA;
                                        break;

                                case BUS_IO:
                                        scsi.scsi_state = SCSI_STATE_READ_DATA;
                                        break;

                                case (BUS_CD | BUS_IO):
                                        scsi.scsi_state = SCSI_STATE_READ_STATUS;
                                        break;

                                case (BUS_CD | BUS_IO | BUS_MSG):
                                        scsi.scsi_state = SCSI_STATE_READ_MESSAGE;
                                        break;

                                case BUS_CD:
                                        // pcem bug, reproduced: PB-143 — la CDB courte complétée de zéros.
                                        bus_out = BUS_SETDATA(0);

                                        scsi_bus_update(scsi.bus, bus_out | BUS_ACK);
                                        scsi_bus_update(scsi.bus, bus_out & ~BUS_ACK);
                                        break;

                                default:
                                        // pcem bug, reproduced: PB-134 — une phase que le disque ne pose pas (:1807).
                                        pc.fatal($" Bad new phase {bus_state:x}\n");
                                        break;
                                }
                                break;
                        }
                }
                break;

        case SCSI_STATE_END_PHASE:
                /*Wait for SCSI command to move to next phase*/
                for (c = 0; c < 20; c++)
                {
                        int bus_state = scsi_bus_read(scsi.bus);

                        if ((bus_state & BUS_BSY) == 0)
                        {
                                scsi.scsi_state = SCSI_STATE_IDLE;
                                break;
                        }

                        // pcem bug, reproduced: PB-134 — après le message, la cible ne demande plus rien (:1828).
                        if ((bus_state & BUS_REQ) != 0)
                                pc.fatal("END_PHASE - unexpected REQ\n");
                }
                break;

        case SCSI_STATE_READ_DATA:
                while (scsi.cdb.data_idx < scsi.cdb.data_len && scsi.scsi_state == SCSI_STATE_READ_DATA &&
                       bytes_transferred < MAX_BYTES_TRANSFERRED_PER_POLL)
                {
                        int d;

                        for (d = 0; d < 20; d++)
                        {
                                int bus_state = scsi_bus_read(scsi.bus);

                                // pcem bug, reproduced: PB-134 — BSY tombe après l'ACK du message, jamais ici (:1842).
                                if ((bus_state & BUS_BSY) == 0)
                                        pc.fatal("READ_DATA - dropped BSY waiting\n");

                                if ((bus_state & (BUS_IO | BUS_CD | BUS_MSG)) != BUS_IO)
                                {
                                        scsi.scsi_state = SCSI_STATE_NEXT_PHASE;
                                        break;
                                }

                                if ((bus_state & BUS_REQ) != 0)
                                {
                                        uint8_t data = (uint8_t)BUS_GETDATA(bus_state);
                                        int bus_out = 0;

                                        // pcem bug, reproduced: PB-144 — adresse sans bouclage à 24 bits (:1857).
                                        if (scsi.cdb.data_pointer == 0xFFFFFFFF)
                                                scsi.int_buffer[scsi.cdb.data_idx] = data;
                                        else
                                                mem_writeb_phys(scsi.cdb.data_pointer + (uint32_t)scsi.cdb.data_idx, data);
                                        scsi.cdb.data_idx++;
                                        scsi.cdb.bytes_transferred++;

                                        scsi_bus_update(scsi.bus, bus_out | BUS_ACK);
                                        scsi_bus_update(scsi.bus, bus_out & ~BUS_ACK);
                                        break;
                                }
                        }

                        bytes_transferred++;
                }
                // pcem bug, reproduced: PB-132 — au bout du CCB, NEXT_PHASE renvoie ici tant que la cible reste en
                //   DATA IN : la navette sans fin, sans statut d'hôte 12h.
                if (scsi.cdb.data_idx == scsi.cdb.data_len)
                {
                        if (scsi.cdb.scatter_gather != 0)
                        {
                                if (scsi.cdb.data_seg_list_idx >= scsi.cdb.data_seg_list_len)
                                        scsi.scsi_state = SCSI_STATE_NEXT_PHASE;
                                else
                                {
                                        scsi.cdb.data_len = mem_readb_phys(scsi.cdb.data_seg_list_pointer +
                                                                           scsi.cdb.data_seg_list_idx + 2) |
                                                            (mem_readb_phys(scsi.cdb.data_seg_list_pointer +
                                                                            scsi.cdb.data_seg_list_idx + 1)
                                                             << 8) |
                                                            (mem_readb_phys(scsi.cdb.data_seg_list_pointer +
                                                                            scsi.cdb.data_seg_list_idx)
                                                             << 16);
                                        scsi.cdb.data_pointer = (uint32_t)(mem_readb_phys(scsi.cdb.data_seg_list_pointer +
                                                                                          scsi.cdb.data_seg_list_idx + 5) |
                                                                           (mem_readb_phys(scsi.cdb.data_seg_list_pointer +
                                                                                           scsi.cdb.data_seg_list_idx + 4)
                                                                            << 8) |
                                                                           (mem_readb_phys(scsi.cdb.data_seg_list_pointer +
                                                                                           scsi.cdb.data_seg_list_idx + 3)
                                                                            << 16));
                                        scsi.cdb.data_idx = 0;
                                        scsi.cdb.data_seg_list_idx += 6;
                                }
                        }
                        else
                                scsi.scsi_state = SCSI_STATE_NEXT_PHASE;
                }
                break;

        case SCSI_STATE_WRITE_DATA:
                while (scsi.cdb.data_idx < scsi.cdb.data_len && bytes_transferred < MAX_BYTES_TRANSFERRED_PER_POLL)
                {
                        int d;

                        for (d = 0; d < 20; d++)
                        {
                                int bus_state = scsi_bus_read(scsi.bus);

                                // pcem bug, reproduced: PB-134 — BSY tombe après l'ACK du message, jamais ici (:1920).
                                if ((bus_state & BUS_BSY) == 0)
                                        pc.fatal("WRITE_DATA - dropped BSY waiting\n");

                                if ((bus_state & (BUS_IO | BUS_CD | BUS_MSG)) != 0)
                                {
                                        scsi.scsi_state = SCSI_STATE_NEXT_PHASE;
                                        break;
                                }

                                if ((bus_state & BUS_REQ) != 0)
                                {
                                        uint8_t data;
                                        int bus_out;

                                        // pcem bug, reproduced: PB-144 — adresse sans bouclage à 24 bits (:1935).
                                        if (scsi.cdb.data_pointer == 0xFFFFFFFF)
                                                data = scsi.int_buffer[scsi.cdb.data_idx];
                                        else
                                                data = mem_readb_phys(scsi.cdb.data_pointer + (uint32_t)scsi.cdb.data_idx);
                                        scsi.cdb.data_idx++;
                                        scsi.cdb.bytes_transferred++;

                                        bus_out = BUS_SETDATA(data);
                                        scsi_bus_update(scsi.bus, bus_out | BUS_ACK);
                                        scsi_bus_update(scsi.bus, bus_out & ~BUS_ACK);
                                        break;
                                }
                        }

                        bytes_transferred++;
                }
                // pcem bug, reproduced: PB-132 — au bout du CCB, NEXT_PHASE renvoie ici tant que la cible reste en
                //   DATA OUT : la navette sans fin, sans statut d'hôte 12h.
                if (scsi.cdb.data_idx == scsi.cdb.data_len)
                {
                        if (scsi.cdb.scatter_gather != 0)
                        {
                                if (scsi.cdb.data_seg_list_idx >= scsi.cdb.data_seg_list_len)
                                        scsi.scsi_state = SCSI_STATE_NEXT_PHASE;
                                else
                                {
                                        scsi.cdb.data_len = mem_readb_phys(scsi.cdb.data_seg_list_pointer +
                                                                           scsi.cdb.data_seg_list_idx + 2) |
                                                            (mem_readb_phys(scsi.cdb.data_seg_list_pointer +
                                                                            scsi.cdb.data_seg_list_idx + 1)
                                                             << 8) |
                                                            (mem_readb_phys(scsi.cdb.data_seg_list_pointer +
                                                                            scsi.cdb.data_seg_list_idx)
                                                             << 16);
                                        scsi.cdb.data_pointer = (uint32_t)(mem_readb_phys(scsi.cdb.data_seg_list_pointer +
                                                                                          scsi.cdb.data_seg_list_idx + 5) |
                                                                           (mem_readb_phys(scsi.cdb.data_seg_list_pointer +
                                                                                           scsi.cdb.data_seg_list_idx + 4)
                                                                            << 8) |
                                                                           (mem_readb_phys(scsi.cdb.data_seg_list_pointer +
                                                                                           scsi.cdb.data_seg_list_idx + 3)
                                                                            << 16));
                                        scsi.cdb.data_idx = 0;
                                        scsi.cdb.data_seg_list_idx += 6;
                                }
                        }
                        else
                                scsi.scsi_state = SCSI_STATE_NEXT_PHASE;
                }
                break;

        case SCSI_STATE_READ_STATUS:
                for (c = 0; c < 20; c++)
                {
                        int bus_state = scsi_bus_read(scsi.bus);

                        // pcem bug, reproduced: PB-134 — BSY tombe après l'ACK du message, jamais ici (:1994).
                        if ((bus_state & BUS_BSY) == 0)
                                pc.fatal("READ_STATUS - dropped BSY waiting\n");

                        if ((bus_state & (BUS_IO | BUS_CD | BUS_MSG)) != (BUS_CD | BUS_IO))
                        {
                                scsi.scsi_state = SCSI_STATE_NEXT_PHASE;
                                break;
                        }

                        if ((bus_state & BUS_REQ) != 0)
                        {
                                uint8_t status = (uint8_t)BUS_GETDATA(bus_state);
                                int bus_out = 0;

                                if (scsi.ccb.from_mailbox != 0)
                                        mem_writeb_phys(scsi.ccb.addr + 0xf, status);
                                scsi.cdb.last_status = status;

                                scsi_bus_update(scsi.bus, bus_out | BUS_ACK);
                                scsi_bus_update(scsi.bus, bus_out & ~BUS_ACK);

                                scsi.scsi_state = SCSI_STATE_NEXT_PHASE;
                                break;
                        }
                }
                break;

        case SCSI_STATE_READ_MESSAGE:
                for (c = 0; c < 20; c++)
                {
                        int bus_state = scsi_bus_read(scsi.bus);

                        // pcem bug, reproduced: PB-134 — BSY tombe après l'ACK du message, jamais ici (:2025).
                        if ((bus_state & BUS_BSY) == 0)
                                pc.fatal("READ_MESSAGE - dropped BSY waiting\n");

                        if ((bus_state & (BUS_IO | BUS_CD | BUS_MSG)) != (BUS_CD | BUS_IO | BUS_MSG))
                        {
                                scsi.scsi_state = SCSI_STATE_NEXT_PHASE;
                                break;
                        }

                        if ((bus_state & BUS_REQ) != 0)
                        {
                                uint8_t msg = (uint8_t)BUS_GETDATA(bus_state);
                                int bus_out = 0;

                                scsi_bus_update(scsi.bus, bus_out | BUS_ACK);
                                scsi_bus_update(scsi.bus, bus_out & ~BUS_ACK);

                                switch (msg)
                                {
                                case MSG_COMMAND_COMPLETE:
                                        scsi.scsi_state = SCSI_STATE_END_PHASE;
                                        break;

                                default:
                                        // pcem bug, reproduced: PB-134 — seul COMMAND COMPLETE arrive ici (:2047).
                                        pc.fatal($"READ_MESSAGE - unknown message {msg:x2}\n");
                                        break;
                                }
                                break;
                        }
                }
                break;

        default:
                // pcem bug, reproduced: PB-134 — un état que la machine ne pose jamais (:2055).
                pc.fatal($"Unknown SCSI_state {scsi.scsi_state}\n");
                break;
        }
    }

    // pcem: scsi_aha1540.c:2059-2068
    private static void aha154x_callback(object? p)
    {
        aha154x_t scsi = (aha154x_t)p!;

        timer_advance_u64(scsi.timer, TIMER_USEC * POLL_TIME_US);

        process_cmd(scsi);
        process_ccb(scsi);
        process_scsi(scsi);
    }

    // pcem: scsi_aha1540.c:2070-2086
    private static uint8_t aha1542c_read(uint32_t addr, object? p)
    {
        aha154x_t scsi = (aha154x_t)p!;
        uint8_t temp = 0xff;

        addr &= 0x3fff;

        if (addr == 0x3f7e) /*DIP switches*/
                temp = scsi.dipsw;
        else if (addr == 0x3f7f) /*Negation of DIP switches, to satisfy BIOS checksum!*/
                temp = (uint8_t)((scsi.dipsw ^ 0xff) + 1);
        else if (addr >= 0x3f80 && (scsi.shadow & 2) != 0)
                temp = scsi.shadow_ram[addr];
        else
                temp = scsi.bios_rom.rom[addr | scsi.bios_bank];

        return temp;
    }

    // pcem: scsi_aha1540.c:2087-2094
    private static void aha1542c_write(uint32_t addr, uint8_t val, object? p)
    {
        aha154x_t scsi = (aha154x_t)p!;

        addr &= 0x3fff;

        if (addr >= 0x3f80 && (scsi.shadow & 1) != 0)
                scsi.shadow_ram[addr] = val;
    }

    // pcem: scsi_aha1540.c:2096-2107 — l'EEPROM se lit à l'init, dans `nvr/.aha1542c.nvr`, sinon dans la référence
    //   `nvr/default/aha1542c.nvr` (nvrfopen) ; sans l'un ni l'autre, des zéros : ID 0, DMA 0, IRQ 9 (PB-144).
    private static void aha1542c_eeprom_load(aha154x_t scsi, string fn)
    {
        FileStream? f;

        scsi.fn = fn;
        f = Devices.nvr.nvrfopen(scsi.fn, "rb");
        if (f is null)
        {
                Array.Clear(scsi.eeprom, 0, 35);
                return;
        }
        Flash.rom.fread(scsi.eeprom, 0, 1, 32, f);
        f.Close();
    }

    // pcem: scsi_aha1540.c:2109-2116
    private static void aha1542c_eeprom_save(aha154x_t scsi)
    {
        FileStream? f = Devices.nvr.nvrfopen(scsi.fn, "wb");

        if (f is null)
                return;
        f.Write(scsi.eeprom, 0, 32);
        f.Close();
    }

    // pcem: scsi_aha1540.c:2118 — uint16_t : -1 devient 0xFFFF.
    private static readonly uint16_t[] port_sw_mapping = [0x330, 0x334, 0x230, 0x234, 0x130, 0x134, 0xFFFF, 0xFFFF];

    // pcem: scsi_aha1540.c:2120-2161 — malloc + memset : `new` zéro-initialise.
    private static object? scsi_aha1542c_init()
    {
        aha154x_t scsi = new();
        uint32_t addr;
        int c;

        Array.Clear(commandes_vues);

        Flash.rom.rom_init(scsi.bios_rom, "adaptec_aha1542c_bios_534201-00.bin", 0xd8000, 0x8000, 0x7fff, 0, MEM_MAPPING_EXTERNAL);
        mem_mapping_disable(scsi.bios_rom.mapping);

        addr = (uint32_t)device_get_config_int("bios_addr");
        mem_mapping_add(scsi.mapping, addr, 0x4000, aha1542c_read, null, null, aha1542c_write, null, null, scsi.bios_rom.rom,
                        0, 0, scsi);

        // pcem bug, reproduced: PB-142 — état 0 à la mise sous tension : ni STST, ni INIT, ni IDLE avant un reset.
        scsi.status = 0;
        scsi.cmd_state = CMD_STATE_IDLE;
        scsi.ccb_state = CCB_STATE_IDLE;
        scsi.scsi_state = SCSI_STATE_IDLE;

        timer_add(scsi.timer, aha154x_callback, scsi, 1);

        addr = (uint32_t)device_get_config_int("addr");
        io_sethandler((uint16_t)addr, 0x0004, aha154x_in, null, null, aha154x_out, null, null, scsi);
        for (c = 0; c < 8; c++)
        {
                if (port_sw_mapping[c] == addr)
                {
                        scsi.dipsw = (uint8_t)c;
                        break;
                }
        }

        scsi_bus_init(scsi.bus);

        aha1542c_eeprom_load(scsi, "aha1542c.nvr");

        // pcem bug, reproduced: PB-144 — sans EEPROM, des zéros : ID 0, DMA 0, IRQ 9 ; l'usine dit ID 7, DMA 5, IRQ 11.
        scsi.host_id = scsi.eeprom[0] & 7;
        scsi.dma = (scsi.eeprom[1] >> 4) & 7;
        scsi.irq = (scsi.eeprom[1] & 7) + 9;

        return scsi;
    }

    // pcem: scsi_aha1540.c:2193-2199 — free : le GC.
    private static void scsi_aha1542c_close(object p)
    {
        aha154x_t scsi = (aha154x_t)p;

        scsi_bus_close(scsi.bus);
    }

    // pcem: scsi_aha1540.c:2201
    internal static int scsi_aha1542c_available() => Flash.rom.rom_present("adaptec_aha1542c_bios_534201-00.bin");

    // pcem: scsi_aha1540.c:2205-2227
    internal static readonly device_config_t[] aha1542c_config =
    [
        new device_config_t { name = "addr", type = CONFIG_SELECTION, default_int = 0x334,
            selection = [new() { description = "130", value = 0x130 }, new() { description = "134", value = 0x134 },
                         new() { description = "230", value = 0x230 }, new() { description = "234", value = 0x234 },
                         new() { description = "330", value = 0x330 }, new() { description = "334", value = 0x334 }] },
        new device_config_t { name = "bios_addr", type = CONFIG_SELECTION, default_int = 0xdc000,
            selection = [new() { description = "C8000", value = 0xc8000 }, new() { description = "CC000", value = 0xcc000 },
                         new() { description = "D0000", value = 0xd0000 }, new() { description = "D4000", value = 0xd4000 },
                         new() { description = "D8000", value = 0xd8000 }, new() { description = "DC000", value = 0xdc000 }] },
        new device_config_t { type = -1 },
    ];

    // pcem: scsi_aha1540.c:2282-2290
    internal static readonly device_t scsi_aha1542c_device = new("Adaptec AHA-1542C (SCSI)", DEVICE_AT, scsi_aha1542c_init,
                                                                 scsi_aha1542c_close, scsi_aha1542c_available, null, null,
                                                                 null, aha1542c_config);

    // La sonde de la carte (G11.0), champ pour champ celle d'h_aha_probe (tools/oracle/harness_aha.c).
    internal const int PROBE_N = 140;

    private static uint64_t fnv(uint8_t[] p, int n)
    {
        uint64_t h = 1469598103934665603UL;
        for (var c = 0; c < n; c++)
        {
                h ^= p[c];
                h *= 1099511628211UL;
        }
        return h;
    }

    internal static void Probe(uint64_t[] o)
    {
        Array.Clear(o, 0, PROBE_N);
        aha154x_t? s = courante();
        var i = 0;
        if (s is null)
                return;
        o[i++] = 1;
        o[i++] = s.status;
        o[i++] = s.isr;
        o[i++] = (uint64_t)s.cmd_state;
        o[i++] = (uint64_t)s.ccb_state;
        o[i++] = (uint64_t)s.scsi_state;
        o[i++] = (uint64_t)s.bios_cmd_state;
        o[i++] = s.command;
        o[i++] = (uint64_t)s.mbc;
        o[i++] = s.mba;
        o[i++] = s.mba_i;
        o[i++] = (uint64_t)s.mbo_req;
        o[i++] = (uint64_t)s.current_mbo;
        o[i++] = (uint64_t)s.current_mbo_is_bios;
        o[i++] = (uint64_t)s.current_mbi;
        o[i++] = (uint64_t)s.bios_mbc;
        o[i++] = s.bios_mba;
        o[i++] = (uint64_t)s.bios_mbo_req;
        o[i++] = (uint64_t)s.bios_mbo_inited;
        o[i++] = (uint64_t)s.mbo_irq_enable;
        o[i++] = s.ccb.addr;
        o[i++] = (uint64_t)s.ccb.from_mailbox;
        o[i++] = s.ccb.status;
        o[i++] = (uint64_t)s.ccb.req_sense_len;
        o[i++] = (uint64_t)s.ccb.target_id;
        o[i++] = s.ccb.data_len;
        o[i++] = s.ccb.data_pointer;
        o[i++] = (uint64_t)s.cdb.idx;
        o[i++] = (uint64_t)s.cdb.len;
        o[i++] = (uint64_t)s.cdb.data_idx;
        o[i++] = (uint64_t)s.cdb.data_len;
        o[i++] = s.cdb.data_pointer;
        o[i++] = s.cdb.last_status;
        o[i++] = (uint64_t)s.result_pos;
        o[i++] = (uint64_t)s.result_len;
        o[i++] = s.data_in;
        o[i++] = (uint64_t)s.reg3_idx;
        o[i++] = (uint64_t)s.host_id;
        o[i++] = (uint64_t)s.dma;
        o[i++] = (uint64_t)s.irq;
        o[i++] = s.shadow;
        o[i++] = s.bios_bank;
        o[i++] = s.dipsw;
        o[i++] = s.mapping.@base;
        o[i++] = s.e_d;
        o[i++] = s.to;
        o[i++] = s.bon;
        o[i++] = s.boff;
        o[i++] = s.atbs;
        o[i++] = s.mbu;
        o[i++] = s.mblt;
        o[i++] = fnv(s.eeprom, 256);
        o[i++] = fnv(s.shadow_ram, 0x4000);
        o[i++] = fnv(s.int_buffer, 512);
        o[i++] = fnv(s.dma_buffer, 64);
        o[i++] = (uint64_t)s.bus.state;
        o[i++] = (uint64_t)(int64_t)s.bus.dev_id;
        o[i++] = s.bus.bus_out;
        o[i++] = s.bus.bus_in;
        o[i++] = (uint64_t)s.bus.command_pos;
        o[i++] = (uint64_t)s.bus.clear_req;
        o[i++] = (uint64_t)s.bus.change_state_delay;
        o[i++] = (uint64_t)s.bus.new_req_delay;
        for (var c = 0; c < 7; c++)
        {
                if (s.bus.devices[c] != scsi_hd_c.scsi_hd || s.bus.device_data[c] is not scsi_hd_data d)
                {
                        i += 11;
                        continue;
                }
                o[i++] = 1;
                o[i++] = (uint64_t)d.cmd_pos;
                o[i++] = (uint32_t)d.addr;
                o[i++] = (uint32_t)d.len;
                o[i++] = (uint32_t)d.data_pos_read;
                o[i++] = (uint32_t)d.data_pos_write;
                o[i++] = (uint32_t)d.bytes_received;
                o[i++] = (uint32_t)d.bytes_required;
                o[i++] = (uint32_t)(d.sense_key | (d.asc << 8) | (d.ascq << 16)) | ((uint64_t)d.status << 24);
                o[i++] = fnv(d.buf, 512);
                o[i++] = fnv(d.io, 2 * scsi_hd_c.BUFFER_SIZE);
        }
    }

    /// <summary>La carte montée, cherchée comme h_aha_probe la cherche : dans devices[] (device.c:15).</summary>
    internal static aha154x_t? courante()
    {
        for (var c = 0; c < device.devices.Length; c++)
        {
                if (device.devices[c] == scsi_aha1542c_device)
                        return device.device_priv[c] as aha154x_t;
        }
        return null;
    }
}
