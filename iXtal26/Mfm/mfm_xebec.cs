// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/mfm/mfm_xebec.c  (mfm_xebec.h : includes/private/mfm/mfm_xebec.h)
// STATUS: transcribed — mfm_xebec.c en entier, les DEUX cartes.
//
// L'IBM PC Fixed Disk Adapter, de conception Xebec : le contrôleur de disque dur
// du 5160, et la raison d'être du XT. Une carte, pas la carte mère — son INT 13h
// vient de sa ROM d'extension en C800:0000, le BIOS du 5160 n'en portant pas une
// ligne. C'est ce qui explique que le XT démarre sans (VERIFICATION.md § M10).
//
// Ports 0x320-0x323, IRQ 5, canal DMA 3.
//
// LE FICHIER PORTE DEUX CARTES. mfm_xebec_device et dtc_5150x_device partagent
// xebec_close, xebec_read, xebec_write et xebec_callback ; seuls leur init et leur
// ROM diffèrent. Le DTC vient donc gratuitement. Seul mfm_xebec est câblé dans la
// configuration ; le DTC est transcrit parce qu'il est là, pas parce qu'il sert.

// CS8600 : `(xebec_t)p` part du `void *p` que timer.cs déclare `object?`
// (mfm_xebec.c:289) — même situation que `(PIT_nr *)p` dans pit.cs.
// CS8602 : `priv` est le void* que device_priv range et que io.cs rend en
// `object` ; le cast est celui du C (mfm_xebec.c:112, :162).
#pragma warning disable CS8600, CS8602

using iXtal26.Disc;
using iXtal26.Flash;
using iXtal26.PluginApi;
using static iXtal26.Models.dma;
using static iXtal26.Models.pic;
using static iXtal26.io;
using static iXtal26.pc;
using static iXtal26.timer;

namespace iXtal26.Mfm;

// pcem: mfm_xebec.c:39-46 — classe : mfm_xebec.c:753 prend l'adresse de
// drives[d].hdd_file et la passe à hdd_load.
//
// pcem bug, not reproduced: PB-30 — cfg_spt, mort chez PCem, n'est pas transcrit (ci-dessous).
// omitted: `cfg_spt` (:41) — déclaré, jamais écrit. Les 17 secteurs par piste sont
//   codés en dur partout (:260, :266, :275, :384, :660). Code mort chez PCem.
internal sealed class mfm_drive_t
{
    internal int cfg_hpc;
    internal int cfg_cyl;
    internal int current_cylinder;
    internal hdd_file_t hdd_file = new();
}

// pcem: mfm_xebec.c:48-78
internal sealed class xebec_t
{
    internal rom_t bios_rom = new();

    internal pc_timer_t callback_timer = new();

    internal int state;

    internal uint8_t status;

    internal uint8_t[] command = new uint8_t[6];
    internal int command_pos;

    // data[] est le tampon de l'ACCÈS PROGRAMMÉ et des données de commande ;
    // sector_buf[] celui du DMA et du disque. Les deux ne se fondent PAS : quatre
    // Array.Copy les pontent (mfm_xebec.c:418, :469, :521, :629) et c'est cette
    // frontière PIO ↔ DMA qui disparaîtrait si on n'en gardait qu'un.
    internal uint8_t[] data = new uint8_t[512];
    internal int data_pos, data_len;

    internal uint8_t[] sector_buf = new uint8_t[512];

    internal uint8_t irq_dma_mask;

    internal uint8_t completion_byte;
    internal uint8_t error;

    internal int drive_sel;

    internal mfm_drive_t[] drives = { new(), new() };

    internal int sector, head, cylinder;
    internal int sector_count;

    internal uint8_t switches;
}

internal static partial class mfm_xebec
{
    // DEVIATION: pclog() et warning() appartiennent à plugin-api/logging.c, pas
    //   transcrit. Shims locaux, comme mem_bios.cs:20.
    private static void pclog(string s) => Console.Error.Write(s);
    private static void warning(string s) => Console.Error.Write(s);

    // pcem: mfm_xebec.c:24
    private static uint64_t XEBEC_TIME => 2000 * TIMER_USEC;

    // pcem: mfm_xebec.c:27-37
    // pcem bug, not reproduced: PB-30 — STATE_DUNNO, mort chez PCem, n'est pas transcrit.
    // omitted: STATE_DUNNO (:37) — déclaré, jamais utilisé.
    private const int STATE_IDLE = 0;
    private const int STATE_RECEIVE_COMMAND = 1;
    private const int STATE_START_COMMAND = 2;
    private const int STATE_RECEIVE_DATA = 3;
    private const int STATE_RECEIVED_DATA = 4;
    private const int STATE_SEND_DATA = 5;
    private const int STATE_SENT_DATA = 6;
    private const int STATE_COMPLETION_BYTE = 7;

    // pcem: mfm_xebec.c:80-88
    // pcem bug, not reproduced: PB-30 — STAT_DRQ, mort chez PCem, n'est pas transcrit.
    // omitted: STAT_DRQ 0x10 (:81) — déclaré, jamais utilisé.
    private const int STAT_IRQ = 0x20;
    private const int STAT_BSY = 0x08;
    private const int STAT_CD = 0x04;
    private const int STAT_IO = 0x02;
    private const int STAT_REQ = 0x01;

    private const int IRQ_ENA = 0x02;
    private const int DMA_ENA = 0x01;

    // pcem: mfm_xebec.c:90-105
    private const int CMD_TEST_DRIVE_READY = 0x00;
    private const int CMD_RECALIBRATE = 0x01;
    private const int CMD_READ_STATUS = 0x03;
    private const int CMD_VERIFY_SECTORS = 0x05;
    private const int CMD_FORMAT_TRACK = 0x06;
    private const int CMD_READ_SECTORS = 0x08;
    private const int CMD_WRITE_SECTORS = 0x0a;
    private const int CMD_SEEK = 0x0b;
    private const int CMD_INIT_DRIVE_PARAMS = 0x0c;
    private const int CMD_WRITE_SECTOR_BUFFER = 0x0f;
    private const int CMD_BUFFER_DIAGNOSTIC = 0xe0;
    private const int CMD_CONTROLLER_DIAGNOSTIC = 0xe4;
    private const int CMD_DTC_GET_DRIVE_PARAMS = 0xfb;
    private const int CMD_DTC_SET_STEP_RATE = 0xfc;
    private const int CMD_DTC_SET_GEOMETRY = 0xfe;
    private const int CMD_DTC_GET_GEOMETRY = 0xff;

    // pcem: mfm_xebec.c:107-109
    private const int ERR_NOT_READY = 0x04;
    private const int ERR_SEEK_ERROR = 0x15;
    private const int ERR_ILLEGAL_SECTOR_ADDRESS = 0x21;

    // pcem: mfm_xebec.c:111-160
    private static uint8_t xebec_read(uint16_t port, object priv)
    {
        xebec_t xebec = (xebec_t)priv;
        uint8_t temp = 0xff;

        switch (port)
        {
        case 0x320: /*Read data*/
                xebec.status &= unchecked((uint8_t)~STAT_IRQ);
                switch (xebec.state)
                {
                case STATE_COMPLETION_BYTE:
                        if ((xebec.status & 0xf) != (STAT_CD | STAT_IO | STAT_REQ | STAT_BSY))
                                fatal($"Read data STATE_COMPLETION_BYTE, status={xebec.status:x2}\n");

                        temp = xebec.completion_byte;
                        xebec.status = 0;
                        xebec.state = STATE_IDLE;
                        break;

                case STATE_SEND_DATA:
                        // pcem bug, reproduced: PB-26 — la chaîne annonce
                        //   STATE_COMPLETION_BYTE dans le cas STATE_SEND_DATA, et
                        //   « Data write » dans le chemin de LECTURE. Deux libellés
                        //   faux par copier-coller ; le comportement, lui, est juste.
                        if ((xebec.status & 0xf) != (STAT_IO | STAT_REQ | STAT_BSY))
                                fatal($"Read data STATE_COMPLETION_BYTE, status={xebec.status:x2}\n");
                        if (xebec.data_pos >= xebec.data_len)
                                fatal("Data write with full data!\n");
                        temp = xebec.data[xebec.data_pos++];
                        if (xebec.data_pos == xebec.data_len)
                        {
                                xebec.status = STAT_BSY;
                                xebec.state = STATE_SENT_DATA;
                                timer_set_delay_u64(xebec.callback_timer, XEBEC_TIME);
                        }
                        break;

                default:
                        fatal($"Read data register - {xebec.state}, {xebec.status:x2}\n");
                        break;
                }
                break;

        case 0x321: /*Read status*/
                temp = xebec.status;
                break;

        case 0x322: /*Read option jumpers*/
                temp = xebec.switches;
                break;
        }

        return temp;
    }

    // pcem: mfm_xebec.c:162-224
    private static void xebec_write(uint16_t port, uint8_t val, object priv)
    {
        xebec_t xebec = (xebec_t)priv;

        switch (port)
        {
        case 0x320: /*Write data*/
                switch (xebec.state)
                {
                case STATE_RECEIVE_COMMAND:
                        // pcem bug, reproduced: PB-26 — « STATE_START_COMMAND » dans le cas STATE_RECEIVE_COMMAND.
                        if ((xebec.status & 0xf) != (STAT_BSY | STAT_CD | STAT_REQ))
                                fatal($"Bad write data state - STATE_START_COMMAND, status={xebec.status:x2}\n");
                        if (xebec.command_pos >= 6)
                                fatal("Command write with full command!\n");
                        /*Command data*/
                        xebec.command[xebec.command_pos++] = val;
                        if (xebec.command_pos == 6)
                        {
                                xebec.status = STAT_BSY;
                                xebec.state = STATE_START_COMMAND;
                                timer_set_delay_u64(xebec.callback_timer, XEBEC_TIME);
                        }
                        break;

                case STATE_RECEIVE_DATA:
                        if ((xebec.status & 0xf) != (STAT_BSY | STAT_REQ))
                                fatal($"Bad write data state - STATE_RECEIVE_DATA, status={xebec.status:x2}\n");
                        if (xebec.data_pos >= xebec.data_len)
                                fatal("Data write with full data!\n");
                        /*Command data*/
                        xebec.data[xebec.data_pos++] = val;
                        if (xebec.data_pos == xebec.data_len)
                        {
                                xebec.status = STAT_BSY;
                                xebec.state = STATE_RECEIVED_DATA;
                                timer_set_delay_u64(xebec.callback_timer, XEBEC_TIME);
                        }
                        break;

                default:
                        fatal($"Write data unknown state - {xebec.state} {xebec.status:x2}\n");
                        break;
                }
                break;

        case 0x321: /*Controller reset*/
                xebec.status = 0;
                break;

        case 0x322: /*Generate controller-select-pulse*/
                xebec.status = STAT_BSY | STAT_CD | STAT_REQ;
                xebec.command_pos = 0;
                xebec.state = STATE_RECEIVE_COMMAND;
                break;

        case 0x323: /*DMA/IRQ mask register*/
                xebec.irq_dma_mask = val;
                break;
        }
    }

    // pcem: mfm_xebec.c:226-233
    private static void xebec_complete(xebec_t xebec)
    {
        xebec.status = STAT_REQ | STAT_CD | STAT_IO | STAT_BSY;
        xebec.state = STATE_COMPLETION_BYTE;
        if ((xebec.irq_dma_mask & IRQ_ENA) != 0)
        {
                xebec.status |= STAT_IRQ;
                picint(1 << 5);
        }
    }

    // pcem: mfm_xebec.c:235-239
    private static void xebec_error(xebec_t xebec, uint8_t error)
    {
        xebec.completion_byte |= 0x02;
        xebec.error = error;
        pclog($"xebec_error - {xebec.error:x2}\n");
    }

    // pcem: mfm_xebec.c:241-269
    private static int xebec_get_sector(xebec_t xebec, out long addr)
    {
        mfm_drive_t drive = xebec.drives[xebec.drive_sel];
        int heads = drive.cfg_hpc;

        addr = 0;

        if (drive.current_cylinder != xebec.cylinder)
        {
                pclog("mfm_get_sector: wrong cylinder\n");
                xebec.error = ERR_ILLEGAL_SECTOR_ADDRESS;
                return 1;
        }
        // pcem bug, reproduced: PB-25 — les têtes sont numérotées DEPUIS 0, donc le
        //   test devrait être `>=`. Avec `>`, head == hpc passe le filtre et le
        //   calcul d'adresse ci-dessous vise une piste entière au-delà du cylindre
        //   demandé, en silence. Preuve interne : le test des secteurs, plus bas,
        //   utilise bien `>=`. mfm_at.c:113 et :121 portent le même défaut.
        if (xebec.head > heads)
        {
                pclog("mfm_get_sector: past end of configured heads\n");
                xebec.error = ERR_ILLEGAL_SECTOR_ADDRESS;
                return 1;
        }
        // pcem bug, reproduced: PB-25 — le même `>`, contre les têtes de l'image (:255).
        if (xebec.head > drive.hdd_file.hpc)
        {
                pclog("mfm_get_sector: past end of heads\n");
                xebec.error = ERR_ILLEGAL_SECTOR_ADDRESS;
                return 1;
        }
        if (xebec.sector >= 17)
        {
                pclog("mfm_get_sector: past end of sectors\n");
                xebec.error = ERR_ILLEGAL_SECTOR_ADDRESS;
                return 1;
        }

        addr = ((((long)xebec.cylinder * heads) + xebec.head) * 17) + xebec.sector;

        return 0;
    }

    // pcem: mfm_xebec.c:271-286
    //
    // La désynchronisation de `cylinder` et `current_cylinder` est VOLONTAIRE :
    // cylinder++ n'est pas écrêté, current_cylinder l'est, et le xebec_get_sector
    // suivant voit la différence et rend ERR_ILLEGAL_SECTOR_ADDRESS. C'est la butée
    // de fin de disque, pas un oubli.
    private static void xebec_next_sector(xebec_t xebec)
    {
        mfm_drive_t drive = xebec.drives[xebec.drive_sel];

        xebec.sector++;
        if (xebec.sector >= 17)
        {
                xebec.sector = 0;
                xebec.head++;
                if (xebec.head >= drive.cfg_hpc)
                {
                        xebec.head = 0;
                        xebec.cylinder++;
                        drive.current_cylinder++;
                        if (drive.current_cylinder >= drive.cfg_cyl)
                                drive.current_cylinder = drive.cfg_cyl - 1;
                }
        }
    }

    // pcem: mfm_xebec.c:288-714
    private static void xebec_callback(object? p)
    {
        xebec_t xebec = (xebec_t)p;
        mfm_drive_t drive;

        xebec.drive_sel = (xebec.command[1] & 0x20) != 0 ? 1 : 0;
        // pcem bug, reproduced: PB-23 — drive_sel vaut 0 ou 1 à la ligne au-dessus,
        //   donc `& 0x20` vaut TOUJOURS 0 : le bit d'unité de l'octet de fin ne
        //   signale jamais le lecteur D. L'intention était `command[1] & 0x20`.
        //   Preuve interne : CMD_READ_STATUS fait correctement
        //   `data[1] = drive_sel ? 0x20 : 0`.
        xebec.completion_byte = (uint8_t)(xebec.drive_sel & 0x20);

        drive = xebec.drives[xebec.drive_sel];

        switch (xebec.command[0])
        {
        case CMD_TEST_DRIVE_READY:
                if (drive.hdd_file.f == null)
                        xebec_error(xebec, ERR_NOT_READY);
                xebec_complete(xebec);
                break;

        case CMD_RECALIBRATE:
                if (drive.hdd_file.f == null)
                        xebec_error(xebec, ERR_NOT_READY);
                else
                {
                        xebec.cylinder = 0;
                        drive.current_cylinder = 0;
                }
                xebec_complete(xebec);
                break;

        case CMD_READ_STATUS:
                // pcem bug, reproduced: PB-27 — ce switch interne n'a pas de
                //   `default:`, là où les six autres commandes appellent fatal().
                //   Sur un état inattendu, le contrôleur se fige : ni octet de fin,
                //   ni chronomètre réarmé, ni IRQ. Deux autres switches, ceux du
                //   DTC, ont le même trou.
                switch (xebec.state)
                {
                case STATE_START_COMMAND:
                        xebec.state = STATE_SEND_DATA;
                        xebec.data_pos = 0;
                        xebec.data_len = 4;
                        xebec.status = STAT_BSY | STAT_IO | STAT_REQ;
                        // pcem bug, reproduced: PB-217 — ni le bit « adresse valide » (octet 0, bit 7) ni
                        //   l'adresse (octets 1 à 3 : unité et tête, haut du cylindre et secteur, bas du cylindre).
                        xebec.data[0] = xebec.error;
                        xebec.data[1] = (uint8_t)(xebec.drive_sel != 0 ? 0x20 : 0);
                        xebec.data[2] = xebec.data[3] = 0;
                        xebec.error = 0;
                        break;

                case STATE_SENT_DATA:
                        xebec_complete(xebec);
                        break;
                }
                break;

        case CMD_VERIFY_SECTORS:
                switch (xebec.state)
                {
                case STATE_START_COMMAND:
                        xebec.cylinder = xebec.command[3] | ((xebec.command[2] & 0xc0) << 2);
                        drive.current_cylinder = (xebec.cylinder >= drive.cfg_cyl) ? drive.cfg_cyl - 1 : xebec.cylinder;
                        xebec.head = xebec.command[1] & 0x1f;
                        xebec.sector = xebec.command[2] & 0x1f;
                        xebec.sector_count = xebec.command[4];
                        do
                        {
                                if (xebec_get_sector(xebec, out _) != 0)
                                {
                                        pclog("xebec_get_sector failed\n");
                                        xebec_error(xebec, xebec.error);
                                        xebec_complete(xebec);
                                        return;
                                }

                                xebec_next_sector(xebec);

                                xebec.sector_count = (xebec.sector_count - 1) & 0xff;
                        } while (xebec.sector_count != 0);

                        xebec_complete(xebec);

                        readflash_set(READFLASH_HDC, xebec.drive_sel);
                        break;

                default:
                        fatal($"CMD_VERIFY_SECTORS: bad state {xebec.state}\n");
                        break;
                }
                break;

        case CMD_FORMAT_TRACK:
        {
                long addr;

                xebec.cylinder = xebec.command[3] | ((xebec.command[2] & 0xc0) << 2);
                drive.current_cylinder = (xebec.cylinder >= drive.cfg_cyl) ? drive.cfg_cyl - 1 : xebec.cylinder;
                xebec.head = xebec.command[1] & 0x1f;
                // pcem bug, reproduced: PB-22 — `xebec.sector` n'est PAS réinitialisé
                //   ici, alors que les trois autres commandes d'accès font
                //   `sector = command[2] & 0x1f`. Or xebec_get_sector l'inclut dans
                //   l'adresse : le formatage démarre au secteur laissé par la
                //   commande précédente, déborde sur la piste suivante et laisse le
                //   début de la piste visée intact. Résidu ≥ 17 : la commande échoue
                //   en ERR_ILLEGAL_SECTOR_ADDRESS.

                if (xebec_get_sector(xebec, out addr) != 0)
                {
                        pclog("xebec_get_sector failed\n");
                        xebec_error(xebec, xebec.error);
                        xebec_complete(xebec);
                        return;
                }

                hdd_file.hdd_format_sectors(drive.hdd_file, (int)addr, 17);

                xebec_complete(xebec);
        } break;

        case CMD_READ_SECTORS:
                switch (xebec.state)
                {
                case STATE_START_COMMAND:
                        xebec.cylinder = xebec.command[3] | ((xebec.command[2] & 0xc0) << 2);
                        drive.current_cylinder = (xebec.cylinder >= drive.cfg_cyl) ? drive.cfg_cyl - 1 : xebec.cylinder;
                        xebec.head = xebec.command[1] & 0x1f;
                        xebec.sector = xebec.command[2] & 0x1f;
                        xebec.sector_count = xebec.command[4];
                        xebec.state = STATE_SEND_DATA;
                        xebec.data_pos = 0;
                        xebec.data_len = 512;
                        {
                                long addr;

                                if (xebec_get_sector(xebec, out addr) != 0)
                                {
                                        xebec_error(xebec, xebec.error);
                                        xebec_complete(xebec);
                                        return;
                                }

                                hdd_file.hdd_read_sectors(drive.hdd_file, (int)addr, 1, xebec.sector_buf);
                                readflash_set(READFLASH_HDC, xebec.drive_sel);
                        }
                        if ((xebec.irq_dma_mask & DMA_ENA) != 0)
                                timer_set_delay_u64(xebec.callback_timer, XEBEC_TIME);
                        else
                        {
                                xebec.status = STAT_BSY | STAT_IO | STAT_REQ;
                                Array.Copy(xebec.sector_buf, xebec.data, 512);
                        }
                        break;

                case STATE_SEND_DATA:
                        xebec.status = STAT_BSY;
                        if ((xebec.irq_dma_mask & DMA_ENA) != 0)
                        {
                                // `for` SANS initialiseur : sur DMA_NODATA la boucle
                                // suspend en conservant data_pos, et le chronomètre
                                // la reprend. C'est le mécanisme, pas un oubli.
                                for (; xebec.data_pos < 512; xebec.data_pos++)
                                {
                                        int val = dma_channel_write(3, xebec.sector_buf[xebec.data_pos]);

                                        if (val == DMA_NODATA)
                                        {
                                                pclog("CMD_READ_SECTORS out of data!\n");
                                                xebec.status = STAT_BSY | STAT_CD | STAT_IO | STAT_REQ;
                                                timer_set_delay_u64(xebec.callback_timer, XEBEC_TIME);
                                                return;
                                        }
                                }
                                xebec.state = STATE_SENT_DATA;
                                timer_set_delay_u64(xebec.callback_timer, XEBEC_TIME);
                        }
                        else
                                fatal("Read sectors no DMA! - shouldn't get here\n");
                        break;

                case STATE_SENT_DATA:
                        xebec_next_sector(xebec);

                        xebec.data_pos = 0;

                        xebec.sector_count = (xebec.sector_count - 1) & 0xff;

                        if (xebec.sector_count != 0)
                        {
                                long addr;

                                if (xebec_get_sector(xebec, out addr) != 0)
                                {
                                        xebec_error(xebec, xebec.error);
                                        xebec_complete(xebec);
                                        return;
                                }

                                hdd_file.hdd_read_sectors(drive.hdd_file, (int)addr, 1, xebec.sector_buf);
                                readflash_set(READFLASH_HDC, xebec.drive_sel);

                                xebec.state = STATE_SEND_DATA;

                                if ((xebec.irq_dma_mask & DMA_ENA) != 0)
                                        timer_set_delay_u64(xebec.callback_timer, XEBEC_TIME);
                                else
                                {
                                        xebec.status = STAT_BSY | STAT_IO | STAT_REQ;
                                        Array.Copy(xebec.sector_buf, xebec.data, 512);
                                }
                        }
                        else
                                xebec_complete(xebec);
                        break;

                default:
                        fatal($"CMD_READ_SECTORS: bad state {xebec.state}\n");
                        break;
                }
                break;

        case CMD_WRITE_SECTORS:
                switch (xebec.state)
                {
                case STATE_START_COMMAND:
                        xebec.cylinder = xebec.command[3] | ((xebec.command[2] & 0xc0) << 2);
                        drive.current_cylinder = (xebec.cylinder >= drive.cfg_cyl) ? drive.cfg_cyl - 1 : xebec.cylinder;
                        xebec.head = xebec.command[1] & 0x1f;
                        xebec.sector = xebec.command[2] & 0x1f;
                        xebec.sector_count = xebec.command[4];
                        xebec.state = STATE_RECEIVE_DATA;
                        xebec.data_pos = 0;
                        xebec.data_len = 512;
                        if ((xebec.irq_dma_mask & DMA_ENA) != 0)
                                timer_set_delay_u64(xebec.callback_timer, XEBEC_TIME);
                        else
                                xebec.status = STAT_BSY | STAT_REQ;
                        break;

                case STATE_RECEIVE_DATA:
                        xebec.status = STAT_BSY;
                        if ((xebec.irq_dma_mask & DMA_ENA) != 0)
                        {
                                for (; xebec.data_pos < 512; xebec.data_pos++)
                                {
                                        int val = dma_channel_read(3);

                                        if (val == DMA_NODATA)
                                        {
                                                pclog("CMD_WRITE_SECTORS out of data!\n");
                                                xebec.status = STAT_BSY | STAT_CD | STAT_IO | STAT_REQ;
                                                timer_set_delay_u64(xebec.callback_timer, XEBEC_TIME);
                                                return;
                                        }

                                        xebec.sector_buf[xebec.data_pos] = (uint8_t)(val & 0xff);
                                }

                                xebec.state = STATE_RECEIVED_DATA;
                                timer_set_delay_u64(xebec.callback_timer, XEBEC_TIME);
                        }
                        else
                                fatal("Write sectors no DMA! - should never get here\n");
                        break;

                case STATE_RECEIVED_DATA:
                        if ((xebec.irq_dma_mask & DMA_ENA) == 0)
                                Array.Copy(xebec.data, xebec.sector_buf, 512);

                        {
                                long addr;

                                if (xebec_get_sector(xebec, out addr) != 0)
                                {
                                        xebec_error(xebec, xebec.error);
                                        xebec_complete(xebec);
                                        return;
                                }

                                hdd_file.hdd_write_sectors(drive.hdd_file, (int)addr, 1, xebec.sector_buf);
                        }

                        readflash_set(READFLASH_HDC, xebec.drive_sel);

                        xebec_next_sector(xebec);
                        xebec.data_pos = 0;
                        xebec.sector_count = (xebec.sector_count - 1) & 0xff;

                        if (xebec.sector_count != 0)
                        {
                                xebec.state = STATE_RECEIVE_DATA;
                                if ((xebec.irq_dma_mask & DMA_ENA) != 0)
                                        timer_set_delay_u64(xebec.callback_timer, XEBEC_TIME);
                                else
                                        xebec.status = STAT_BSY | STAT_REQ;
                        }
                        else
                                xebec_complete(xebec);
                        break;

                default:
                        fatal($"CMD_WRITE_SECTORS: bad state {xebec.state}\n");
                        break;
                }
                break;

        case CMD_SEEK:
                if (drive.hdd_file.f == null)
                        xebec_error(xebec, ERR_NOT_READY);
                else
                {
                        int cylinder = xebec.command[3] | ((xebec.command[2] & 0xc0) << 2);

                        drive.current_cylinder = (cylinder >= drive.cfg_cyl) ? drive.cfg_cyl - 1 : cylinder;

                        if (cylinder != drive.current_cylinder)
                                xebec_error(xebec, ERR_SEEK_ERROR);
                }
                xebec_complete(xebec);
                break;

        case CMD_INIT_DRIVE_PARAMS:
                switch (xebec.state)
                {
                case STATE_START_COMMAND:
                        xebec.state = STATE_RECEIVE_DATA;
                        xebec.data_pos = 0;
                        // Huit octets reçus, TROIS exploités : le taux de pas
                        // d'écriture et la précompensation sont jetés.
                        xebec.data_len = 8;
                        xebec.status = STAT_BSY | STAT_REQ;
                        break;

                case STATE_RECEIVED_DATA:
                        drive.cfg_cyl = xebec.data[1] | (xebec.data[0] << 8);
                        drive.cfg_hpc = xebec.data[2];
                        pclog($"Drive {xebec.drive_sel}: cylinders={drive.cfg_cyl}, heads={drive.cfg_hpc}\n");
                        xebec_complete(xebec);
                        break;

                default:
                        fatal($"CMD_INIT_DRIVE_PARAMS bad state {xebec.state}\n");
                        break;
                }
                break;

        case CMD_WRITE_SECTOR_BUFFER:
                switch (xebec.state)
                {
                case STATE_START_COMMAND:
                        xebec.state = STATE_RECEIVE_DATA;
                        xebec.data_pos = 0;
                        xebec.data_len = 512;
                        if ((xebec.irq_dma_mask & DMA_ENA) != 0)
                                timer_set_delay_u64(xebec.callback_timer, XEBEC_TIME);
                        else
                                xebec.status = STAT_BSY | STAT_REQ;
                        break;

                case STATE_RECEIVE_DATA:
                        if ((xebec.irq_dma_mask & DMA_ENA) != 0)
                        {
                                xebec.status = STAT_BSY;

                                for (; xebec.data_pos < 512; xebec.data_pos++)
                                {
                                        int val = dma_channel_read(3);

                                        if (val == DMA_NODATA)
                                        {
                                                pclog("CMD_WRITE_SECTOR_BUFFER out of data!\n");
                                                xebec.status = STAT_BSY | STAT_CD | STAT_IO | STAT_REQ;
                                                timer_set_delay_u64(xebec.callback_timer, XEBEC_TIME);
                                                return;
                                        }

                                        xebec.data[xebec.data_pos] = (uint8_t)(val & 0xff);
                                }

                                xebec.state = STATE_RECEIVED_DATA;
                                timer_set_delay_u64(xebec.callback_timer, XEBEC_TIME);
                        }
                        else
                                fatal("CMD_WRITE_SECTOR_BUFFER - should never get here!\n");
                        break;
                case STATE_RECEIVED_DATA:
                        Array.Copy(xebec.data, xebec.sector_buf, 512);
                        xebec_complete(xebec);
                        break;

                default:
                        fatal($"CMD_WRITE_SECTOR_BUFFER bad state {xebec.state}\n");
                        break;
                }
                break;

        case CMD_BUFFER_DIAGNOSTIC:
        case CMD_CONTROLLER_DIAGNOSTIC:
                xebec_complete(xebec);
                break;

        // pcem: mfm_xebec.c:643 — opcode nu, sans constante nommée chez PCem.
        case 0xfa:
                xebec_complete(xebec);
                break;

        case CMD_DTC_SET_STEP_RATE:
                xebec_complete(xebec);
                break;

        case CMD_DTC_GET_DRIVE_PARAMS:
                switch (xebec.state)
                {
                case STATE_START_COMMAND:
                        xebec.state = STATE_SEND_DATA;
                        xebec.data_pos = 0;
                        xebec.data_len = 4;
                        xebec.status = STAT_BSY | STAT_IO | STAT_REQ;
                        Array.Clear(xebec.data, 0, 4);
                        xebec.data[0] = (uint8_t)(drive.hdd_file.tracks & 0xff);
                        xebec.data[1] = (uint8_t)(17 | ((drive.hdd_file.tracks >> 2) & 0xc0));
                        // pcem bug, reproduced: PB-28 — aucun test de hdd_file.f ici,
                        //   contrairement aux trois autres commandes qui en ont un.
                        //   Sur une unité absente, hpc vaut 0 et la troncature donne
                        //   0xff.
                        xebec.data[2] = (uint8_t)(drive.hdd_file.hpc - 1);
                        pclog($"Get drive params {xebec.data[0]:x2} {xebec.data[1]:x2} " +
                              $"{xebec.data[2]:x2} {drive.hdd_file.tracks}\n");
                        break;

                case STATE_SENT_DATA:
                        xebec_complete(xebec);
                        break;

                default:
                        // pcem bug, reproduced: PB-26 — la chaîne annonce
                        //   CMD_INIT_DRIVE_PARAMS à l'intérieur de
                        //   CMD_DTC_GET_DRIVE_PARAMS.
                        fatal($"CMD_INIT_DRIVE_PARAMS bad state {xebec.state}\n");
                        break;
                }
                break;

        case CMD_DTC_GET_GEOMETRY:
                // pcem bug, reproduced: PB-27 — pas de `default:`, voir CMD_READ_STATUS.
                switch (xebec.state)
                {
                case STATE_START_COMMAND:
                        xebec.state = STATE_SEND_DATA;
                        xebec.data_pos = 0;
                        xebec.data_len = 16;
                        xebec.status = STAT_BSY | STAT_IO | STAT_REQ;
                        Array.Clear(xebec.data, 0, 16);
                        xebec.data[0x4] = (uint8_t)(drive.hdd_file.tracks & 0xff);
                        xebec.data[0x5] = (uint8_t)((drive.hdd_file.tracks >> 8) & 0xff);
                        xebec.data[0xa] = (uint8_t)drive.hdd_file.hpc;
                        break;

                case STATE_SENT_DATA:
                        xebec_complete(xebec);
                        break;
                }
                break;

        case CMD_DTC_SET_GEOMETRY:
                // pcem bug, reproduced: PB-27 — pas de `default:`, voir CMD_READ_STATUS.
                switch (xebec.state)
                {
                case STATE_START_COMMAND:
                        xebec.state = STATE_RECEIVE_DATA;
                        xebec.data_pos = 0;
                        xebec.data_len = 16;
                        xebec.status = STAT_BSY | STAT_REQ;
                        break;

                case STATE_RECEIVED_DATA:
                        /*Bit of a cheat here - we always report the actual geometry of the drive in use*/
                        xebec_complete(xebec);
                        break;
                }
                break;

        default:
                fatal($"Unknown Xebec command - {xebec.command[0]:x2} {xebec.command[1]:x2} " +
                      $"{xebec.command[2]:x2} {xebec.command[3]:x2} {xebec.command[4]:x2} {xebec.command[5]:x2}\n");
                break;
        }
    }

    // pcem: mfm_xebec.c:716-723
    //
    // `internal` et non `private` depuis M12.1 : Host/HddImage.cs la lit pour marquer,
    // dans la table des 46 types du BIOS, ceux que CETTE carte accepte. Recopier les
    // quatre couples là-bas les ferait dériver du fichier qui les fait respecter, et la
    // carte refuserait alors en silence une géométrie que l'utilitaire aurait proposée.
    internal static readonly (int tracks, int hpc)[] xebec_hd_types =
    {
        (306, 4), /*Type 0*/
        (612, 4), /*Type 16*/
        (615, 4), /*Type 2*/
        (306, 8), /*Type 13*/
    };

    // pcem: mfm_xebec.c:725-747
    //
    // LA GÉOMÉTRIE N'EST PAS LIBRE. Il faut exactement 17 secteurs par piste et l'un
    // des quatre couples ci-dessus, sans quoi les bits de l'unité restent à zéro —
    // elle est alors présentée comme type 0, pas comme absente, et le POST diverge.
    // Encodage : unité 0 en bits 3-2, unité 1 en bits 1-0.
    private static void xebec_set_switches(xebec_t xebec)
    {
        int c, d;

        xebec.switches = 0;

        for (d = 0; d < 2; d++)
        {
                mfm_drive_t drive = xebec.drives[d];

                if (drive.hdd_file.f == null)
                        continue;

                for (c = 0; c < 4; c++)
                {
                        if (drive.hdd_file.spt == 17 && drive.hdd_file.hpc == xebec_hd_types[c].hpc &&
                            drive.hdd_file.tracks == xebec_hd_types[c].tracks)
                        {
                                xebec.switches |= (uint8_t)(c << (d != 0 ? 0 : 2));
                                break;
                        }
                }

                if (c == 4)
                        warning($"Drive {(d != 0 ? 'D' : 'C')}: has format not supported by Fixed Disk Adapter\n");
        }
    }

    // pcem: mfm_xebec.c:749-764
    private static object? xebec_init()
    {
        xebec_t xebec = new();

        hdd_file.hdd_load(xebec.drives[0].hdd_file, 0, hdd_c.ide_fn[0]);
        hdd_file.hdd_load(xebec.drives[1].hdd_file, 1, hdd_c.ide_fn[1]);
        xebec_set_switches(xebec);

        rom.rom_init(xebec.bios_rom, "ibm_xebec_62x0822_1985.bin", 0xc8000, 0x4000, 0x3fff, 0, Memory.mem.MEM_MAPPING_EXTERNAL);

        io_sethandler(0x0320, 0x0004, xebec_read, null, null, xebec_write, null, null, xebec);

        timer_add(xebec.callback_timer, xebec_callback, xebec, 0);

        return xebec;
    }

    // pcem: mfm_xebec.c:766-774
    private static void xebec_close(object p)
    {
        xebec_t xebec = (xebec_t)p;

        rom.rom_deinit(xebec.bios_rom);
        hdd_file.hdd_close(xebec.drives[0].hdd_file);
        hdd_file.hdd_close(xebec.drives[1].hdd_file);

        // omitted: free(xebec) — le GC s'en charge.
    }

    // pcem: mfm_xebec.c:776
    private static int xebec_available() => rom.rom_present("ibm_xebec_62x0822_1985.bin");

    // pcem: mfm_xebec.c:778
    internal static device_t mfm_xebec_device = new device_t("IBM PC Fixed Disk Adapter", 0, xebec_init, xebec_close,
                                                             xebec_available, null, null, null, null);

    // pcem: mfm_xebec.c:780-800
    //
    // La seconde carte du fichier. Elle ne passe PAS par xebec_set_switches : ses
    // cavaliers sont forcés à 0xff et sa géométrie recopiée depuis l'image, ce qui
    // la libère de la table des quatre types. Transcrite parce qu'elle est là ;
    // aucune configuration du dépôt ne la câble.
    private static object? dtc_5150x_init()
    {
        xebec_t xebec = new();

        hdd_file.hdd_load(xebec.drives[0].hdd_file, 0, hdd_c.ide_fn[0]);
        hdd_file.hdd_load(xebec.drives[1].hdd_file, 1, hdd_c.ide_fn[1]);
        xebec.switches = 0xff;

        xebec.drives[0].cfg_cyl = xebec.drives[0].hdd_file.tracks;
        xebec.drives[0].cfg_hpc = xebec.drives[0].hdd_file.hpc;
        xebec.drives[1].cfg_cyl = xebec.drives[1].hdd_file.tracks;
        xebec.drives[1].cfg_hpc = xebec.drives[1].hdd_file.hpc;

        rom.rom_init(xebec.bios_rom, "dtc_cxd21a.bin", 0xc8000, 0x4000, 0x3fff, 0, Memory.mem.MEM_MAPPING_EXTERNAL);

        io_sethandler(0x0320, 0x0004, xebec_read, null, null, xebec_write, null, null, xebec);

        timer_add(xebec.callback_timer, xebec_callback, xebec, 0);

        return xebec;
    }

    // pcem: mfm_xebec.c:801
    private static int dtc_5150x_available() => rom.rom_present("dtc_cxd21a.bin");

    // pcem: mfm_xebec.c:803
    internal static device_t dtc_5150x_device = new device_t("DTC 5150X", 0, dtc_5150x_init, xebec_close,
                                                             dtc_5150x_available, null, null, null, null);
}
