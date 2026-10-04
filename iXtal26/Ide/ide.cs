// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/ide/ide.c  (ide.h : includes/private/ide/ide.h)
// STATUS: partial — G5.1 : le contrôleur ATA, disque dur seul. ATAPI omis (PLAN-G5.md,
//         décision n° 1) : aucun lecteur n'est jamais IDE_CDROM ici, et les appels atapi_*
//         que seul un disque dur ne peut atteindre que par WIN_PACKETCMD s'arrêtent
//         bruyamment. Le bus master (SFF-8038i) n'est pas branché : ses pointeurs restent nuls,
//         comme chez PCem sur toute machine sans PIIX.
//
// L'HÉRITIER DE mfm_at.cs. Même jeu de registres (0x1F0-0x1F7, 0x3F6), même IRQ 14 sur le
// PIC esclave ; ce que l'IDE ajoute : un second canal (0x170-0x177, 0x376, IRQ 15), deux
// unités par canal, IDENTIFY, l'adressage LBA, les transferts MULTIPLE — et la géométrie
// que le disque ANNONCE (IDENTIFY) au lieu de la subir (SPECIFY seul chez mfm_at).
//
// QUATRE LECTEURS, INDICÉS PAR ide_drives[] : 0 et 1 sur le canal primaire (maître,
// esclave), 2 et 3 sur le secondaire. L'indice est aussi celui de ide_fn[] et de hdc[] —
// donc des clés hdc_, hdd_, hde_, hdf_ : C: et D: sur le primaire, E: et F: sur le
// secondaire. cur_ide[board] désigne le lecteur sélectionné de chaque canal.

// CS8600, CS8602 : les gestionnaires d'E/S et de temporisation reçoivent le `void *priv`
// du C en `object` ; ide.c ne s'en sert jamais (ide_read_pri et consorts l'ignorent).
#pragma warning disable CS8600, CS8602
// CS8981 : `ide` n'a que des minuscules ASCII — le nom de l'unité C, comme rom.cs et rtc.cs.
#pragma warning disable CS8981

using iXtal26.Disc;
using iXtal26.PluginApi;
using static iXtal26.PluginApi.device;
using static iXtal26.Models.pic;
using static iXtal26.io;
using static iXtal26.pc;
using static iXtal26.timer;

namespace iXtal26.Ide;

// pcem: ide.c:57-79 — classe et non struct : tout le fichier manipule des IDE* ;
// ide_drives[cur_ide[board]] et son voisin `^ 1` sont des alias qu'on modifie en place.
internal sealed class IDE
{
    internal int type;
    internal int board;
    internal uint8_t atastat;
    internal uint8_t error;
    internal int secount, sector, cylinder, head, drive, cylprecomp;
    internal uint8_t command;
    internal uint8_t fdisk;
    internal int pos;
    internal int reset;

    // LE C DÉCLARE `uint16_t buffer[65536]`, ET CE TABLEAU EST RENDU EN OCTETS — même
    // arbitrage que mfm_at.cs : le tampon est indexé par MOTS (buffer[pos >> 1]), écrit
    // par octets (ide_padstr, par un `char *`) et passé tel quel à hdd_write_sectors, qui
    // prend un uint8_t[]. Le flux d'octets est la vérité ; W et SetW posent le petit-
    // boutisme que le C suppose.
    internal uint8_t[] buffer = new uint8_t[65536 * 2];
    internal int irqstat;
    internal int service;
    internal int lba;
    internal uint32_t lba_addr;
    internal int skip512;
    internal int blocksize, blockcount;
    internal uint8_t[] sector_buffer = new uint8_t[256 * 512];
    internal int do_initial_read;
    internal int sector_pos;
    internal hdd_file_t hdd_file = new();
    // omitted: `atapi_device_t atapi` — ATAPI hors G5. Ses cinq pointeurs posés par
    //   resetide (ide.c:294-298) n'ont pas de lecteur ici.

    internal uint16_t W(int i) => (uint16_t)(buffer[i * 2] | (buffer[i * 2 + 1] << 8));

    internal void SetW(int i, uint16_t v)
    {
        buffer[i * 2] = (uint8_t)v;
        buffer[i * 2 + 1] = (uint8_t)(v >> 8);
    }
}

internal static class ide
{
    // pcem: ide.c:1 — propriété et non constante : TIMER_USEC reste nul jusqu'à
    // setpitclock() (timer.cs), comme dans mfm_at.cs.
    private static uint64_t IDE_TIME => 10 * TIMER_USEC;

    /* ATA Commands */
    // pcem: ide.c:25-48
    private const uint8_t WIN_SRST = 0x08; /* ATAPI Device Reset */
    private const uint8_t WIN_RECAL = 0x10;
    private const uint8_t WIN_RESTORE = WIN_RECAL;
    private const uint8_t WIN_READ = 0x20;          /* 28-Bit Read */
    private const uint8_t WIN_READ_NORETRY = 0x21;  /* 28-Bit Read - no retry*/
    private const uint8_t WIN_WRITE = 0x30;         /* 28-Bit Write */
    private const uint8_t WIN_WRITE_NORETRY = 0x31; /* 28-Bit Write */
    private const uint8_t WIN_VERIFY = 0x40;        /* 28-Bit Verify */
    private const uint8_t WIN_VERIFY_ONCE = 0x41;   /* Deprecated command - same as 0x40 */
    private const uint8_t WIN_FORMAT = 0x50;
    private const uint8_t WIN_SEEK = 0x70;
    private const uint8_t WIN_DRIVE_DIAGNOSTICS = 0x90; /* Execute Drive Diagnostics */
    private const uint8_t WIN_SPECIFY = 0x91;           /* Initialize Drive Parameters */
    private const uint8_t WIN_PACKETCMD = 0xA0;         /* Send a packet command. */
    private const uint8_t WIN_PIDENTIFY = 0xA1;         /* Identify ATAPI device */
    private const uint8_t WIN_READ_MULTIPLE = 0xC4;
    private const uint8_t WIN_WRITE_MULTIPLE = 0xC5;
    private const uint8_t WIN_SET_MULTIPLE_MODE = 0xC6;
    private const uint8_t WIN_READ_DMA = 0xC8;
    private const uint8_t WIN_WRITE_DMA = 0xCA;
    private const uint8_t WIN_SETIDLE1 = 0xE3;
    private const uint8_t WIN_CHECK_POWER_MODE = 0xE5;
    private const uint8_t WIN_IDENTIFY = 0xEC; /* Ask drive to identify itself */
    private const uint8_t WIN_SET_FEATURES = 0xEF;

    // pcem: ide.h:48-56 — bits de `atastat` et de `error`.
    internal const uint8_t ERR_STAT = 0x01;
    internal const uint8_t DRQ_STAT = 0x08; /* Data request */
    internal const uint8_t DSC_STAT = 0x10;
    internal const uint8_t SERVICE_STAT = 0x10;
    internal const uint8_t READY_STAT = 0x40;
    internal const uint8_t BUSY_STAT = 0x80;
    internal const uint8_t ABRT_ERR = 0x04; /* Command aborted */

    /** Evaluate to non-zero if the currently selected drive is an ATAPI device */
    // pcem: ide.c:51 — toujours faux ici : sans ATAPI, aucun lecteur n'est IDE_CDROM.
    private static bool IDE_DRIVE_IS_CDROM(IDE ide) => ide.type == IDE_CDROM;

    // pcem: ide.c:53
    internal const int IDE_NONE = 0;
    internal const int IDE_HDD = 1;
    internal const int IDE_CDROM = 2;

    // pcem: ide.h:43 — la valeur de cdrom_drive qui désigne une image (cdrom-image.cc:461, pc.c:297).
    internal const int CDROM_IMAGE = 200;

    // pcem: ide.c:81-85
    private readonly record struct IDE_HDD_EMU(int romset, string model, int tracks, int hpc, int spt);

    // pcem: ide.c:87-88 — cdrom_channel = 2 chez PCem : le maître secondaire devient un
    // lecteur de CD-ROM ATAPI (resetide, ide.c:282-284).
    //
    // DEVIATION: -1, et non 2. ATAPI est hors G5 (PLAN-G5.md, décision n° 1) ; -1 est la
    //   configuration PCem où les quatre lecteurs sont déclarés « Hard drive »
    //   (wx-config.c:891). L'oracle pose la même valeur (harness.c). loadconfig refuse
    //   une clé `cdrom_channel` qui demanderait un lecteur de CD (pc.cs).
    internal static int cdrom_channel = -1;
    internal static int zip_channel = -1;

    // pcem: ide.c:90
    internal static readonly IDE[] ide_drives =
    [
        new IDE(), new IDE(), new IDE(), new IDE(), new IDE(), new IDE(), new IDE(),
    ];

    // pcem: ide.c:92 — écrit par callbackide, lu nulle part ailleurs dans le périmètre.
    internal static IDE? ext_ide;

    /**
     * Table of specific emulated HDD models, starting with Conner drives used
     * by GRiD. Their BIOS only works with these, so this allows running GRiD
     * BIOS unpatched.
     */
    // pcem: ide.c:99-103 — ROM_GRID1520 vaut 31 dans l'énum de ibm.h (ROM_AMI286 = 27,
    // puis TG286M, AWARD286, GDC212M) ; pc.cs n'en porte pas la constante, aucune
    // machine GRiD n'étant transcrite.
    private const int ROM_GRID1520 = 31;

    private static readonly IDE_HDD_EMU[] hddemu =
    [
        new(ROM_GRID1520, "Conner Peripherals 20MB - CP3024", 615, 4, 17), // type 2, 20MB
        new(ROM_GRID1520, "Conner Peripherals 40MB - CP3044", 980, 5, 17), // type 17,
        new(ROM_GRID1520, "Conner Peripherals 104MB - CP3104", 776, 8, 33), // extended type 224 104MB
    ];

    // omitted: `char ide_fn[7][512]` (ide.c:105) — il vit dans hdd_c (Disc/hdd.cs), où
    //   mfm_xebec et mfm_at le lisaient déjà avant que ce fichier existe.

    // pcem: ide.c:107-110 — le bus master PCI (ide_sff8038i.c, hors G5) les pose ; nuls
    // sur toute machine ISA, et c'est ce nul que WIN_READ_DMA / WIN_WRITE_DMA testent.
    internal delegate int bus_master_data_fn(int channel, uint8_t[] data, int size, object? p);
    internal delegate void bus_master_irq_fn(int channel, object? p);
    internal static bus_master_data_fn? ide_bus_master_read_data;
    internal static bus_master_data_fn? ide_bus_master_write_data;
    internal static bus_master_irq_fn? ide_bus_master_set_irq;
    internal static object? ide_bus_master_p;

    // pcem: ide.c:112
    internal static readonly pc_timer_t[] ide_timer = [new pc_timer_t(), new pc_timer_t()];

    // pcem: ide.c:114
    internal static readonly int[] cur_ide = new int[2];

    // pcem: ide.c:116
    internal static uint8_t getstat(IDE ide) => ide.atastat;

    // pcem: ide.c:118-128
    internal static void ide_irq_raise(IDE ide)
    {
        // omitted: pclog("IDE_IRQ_RAISE\n") — commenté chez PCem.
        if ((ide.fdisk & 2) == 0)
        {
                picint((uint16_t)((ide.board != 0) ? (1 << 15) : (1 << 14)));
                if (ide_bus_master_set_irq != null)
                        ide_bus_master_set_irq(ide.board, ide_bus_master_p);
        }
        ide.irqstat = 1;
        ide.service = 1;
    }

    // pcem: ide.c:130-136
    private static void ide_irq_lower(IDE ide)
    {
        picintc((uint16_t)((ide.board != 0) ? (1 << 15) : (1 << 14)));
        ide.irqstat = 0;
    }

    // pcem: ide.c:138-143 — le masque 0x40 est l'IRQ 14 (bit 6 du PIC esclave), et il sert
    // AUSSI au canal secondaire, dont la ligne est l'IRQ 15 (0x80).
    // pcem bug, reproduced: PB-71
    internal static void ide_irq_update(IDE ide)
    {
        if (ide.irqstat != 0 && ((pic2.pend | pic2.ins) & 0x40) == 0 && (ide.fdisk & 2) == 0)
                picint((uint16_t)((ide.board != 0) ? (1 << 15) : (1 << 14)));
        else if (((pic2.pend | pic2.ins) & 0x40) != 0)
                picintc((uint16_t)((ide.board != 0) ? (1 << 15) : (1 << 14)));
    }

    /**
     * Copy a string into a buffer, padding with spaces, and placing characters as
     * if they were packed into 16-bit values, stored little-endian.
     */
    // pcem: ide.c:153-164 — `str` est un `char *` sur le tampon de mots ; ici l'indice
    // d'octet de départ dans IDE.buffer.
    private static void ide_padstr(uint8_t[] str, int str_off, string src, int len)
    {
        int i, v;
        int s = 0;

        for (i = 0; i < len; i++)
        {
                if (s < src.Length)
                {
                        v = src[s++];
                }
                else
                {
                        v = ' ';
                }
                str[str_off + (i ^ 1)] = (uint8_t)v;
        }
    }

    /**
     * Fill in ide->buffer with the output of the "IDENTIFY DEVICE" command
     */
    // pcem: ide.c:169-211 — la géométrie vient de hdc[], la CONFIGURATION, et non de
    // hdd_file : un SPECIFY ne la change pas.
    private static void ide_identify(IDE ide)
    {
        Array.Clear(ide.buffer, 0, 512);
        string hdd_model = "PCemHD";
        int h;
        ref PcemHDC hd = ref hdd_c.hdc[cur_ide[ide.board]];

        // ide->buffer[1] = 101; /* Cylinders */

        if ((hd.tracks * hd.hpc * hd.spt) >= 16514064)
                ide.SetW(1, 16383);
        else
                ide.SetW(1, (uint16_t)hd.tracks); /* Cylinders */
        ide.SetW(3, (uint16_t)hd.hpc);             /* Heads */
        ide.SetW(6, (uint16_t)hd.spt);             /* Sectors */

        ide_padstr(ide.buffer, 10 * 2, "", 20);     /* Serial Number */
        ide_padstr(ide.buffer, 23 * 2, "v1.0", 8);  /* Firmware */
        for (h = 0; h < hddemu.Length; h++)
                if (romset == hddemu[h].romset
                    && hd.tracks == hddemu[h].tracks
                    && hd.hpc == hddemu[h].hpc
                    && hd.spt == hddemu[h].spt)
                {
                        hdd_model = hddemu[h].model;
                        break;
                }

        ide_padstr(ide.buffer, 27 * 2, hdd_model, 40); /* Model */

        ide.SetW(0, 1 << 6);                 /*Fixed drive*/
        ide.SetW(20, 3);                     /*Buffer type*/
        ide.SetW(21, 512);                   /*Buffer size*/
        ide.SetW(47, 16);                    /*Max sectors on multiple transfer command*/
        ide.SetW(48, 1);                     /*Dword transfers supported*/
        ide.SetW(49, (1 << 9) | (1 << 8));   /* LBA and DMA supported */
        ide.SetW(50, 0x4000);                /* Capabilities */
        ide.SetW(51, 2 << 8);                /*PIO timing mode*/
        ide.SetW(52, 2 << 8);                /*DMA timing mode*/
        ide.SetW(59, (uint16_t)(ide.blocksize != 0 ? (ide.blocksize | 0x100) : 0));
        ide.SetW(60, (uint16_t)((hd.tracks * hd.hpc * hd.spt) & 0xFFFF)); /* Total addressable sectors (LBA) */
        ide.SetW(61, (uint16_t)((hd.tracks * hd.hpc * hd.spt) >> 16));
        ide.SetW(63, 7);   /*Multiword DMA*/
        ide.SetW(80, 0xe); /*ATA-1 to ATA-3 supported*/
    }

    /*
     * Return the sector offset for the current register values
     */
    // pcem: ide.c:216-225 — off_t, donc 64 bits ; ramené à int par l'appelant, comme le
    // C le fait en passant la valeur au paramètre `int offset` de hdd_read_sectors.
    private static long ide_get_sector(IDE ide)
    {
        if (ide.lba != 0)
        {
                return (long)ide.lba_addr + ide.skip512;
        }
        else
        {
                int heads = ide.hdd_file.hpc;
                int sectors = ide.hdd_file.spt;

                return ((((long)ide.cylinder * heads) + ide.head) * sectors) + (ide.sector - 1) + ide.skip512;
        }
    }

    /**
     * Move to the next sector using CHS addressing
     */
    // pcem: ide.c:230-246
    private static void ide_next_sector(IDE ide)
    {
        if (ide.lba != 0)
        {
                ide.lba_addr++;
        }
        else
        {
                ide.sector++;

                if (ide.sector == (ide.hdd_file.spt + 1))
                {
                        ide.sector = 1;
                        ide.head++;

                        if (ide.head == ide.hdd_file.hpc)
                        {
                                ide.head = 0;
                                ide.cylinder++;
                        }
                }
        }
    }

    // pcem: ide.c:248-255
    private static void loadhd(IDE ide, int d, string fn)
    {
        hdd_file.hdd_load(ide.hdd_file, d, fn);

        if (ide.hdd_file.f != null)
                ide.type = IDE_HDD;
        else
                ide.type = IDE_NONE;
    }

    // pcem: ide.c:257-263
    internal static void ide_set_signature(IDE ide)
    {
        ide.secount = 1;
        ide.sector = 1;
        ide.head = 0;
        ide.cylinder = (IDE_DRIVE_IS_CDROM(ide) ? 0xEB14 : ((ide.type == IDE_HDD) ? 0 : 0xFFFF));
        //	if (ide->type == IDE_HDD)  ide->drive = 0;
    }

    // pcem: ide.c:265-303
    internal static void resetide()
    {
        int d;

        /* Close hard disk image files (if previously open) */
        for (d = 0; d < 4; d++)
        {
                ide_drives[d].type = IDE_NONE;
                hdd_file.hdd_close(ide_drives[d].hdd_file);

                ide_drives[d].atastat = READY_STAT | DSC_STAT;
                ide_drives[d].service = 0;
                ide_drives[d].board = (d & 2) != 0 ? 1 : 0;
        }

        if (hdd_controller_current_is_ide())
        {
                for (d = 0; d < 4; d++)
                {
                        ide_drives[d].drive = d;

                        if (cdrom_channel == d || zip_channel == d)
                        {
                                // omitted: le lecteur ATAPI de ide.c:282-287
                                //   (scsi_bus_atapi_init sur scsi_cd ou scsi_zip) — hors
                                //   G5. Inatteignable : loadconfig refuse ces canaux.
                                fatal("ATAPI non transcrit (G5) : cdrom_channel / zip_channel\n");
                        }
                        else
                        {
                                loadhd(ide_drives[d], d, hdd_c.ide_fn[d]);
                        }

                        ide_set_signature(ide_drives[d]);

                        // omitted: les cinq pointeurs de ide_drives[d].atapi (ide.c:294-298).
                }
        }

        cur_ide[0] = 0;
        cur_ide[1] = 2;
    }

    // pcem: hdd.c:125 via hdd.c:66-78 — des cartes transcrites, « ide » (hdd.c:155) et, depuis
    // G10.2, « xtide » (hdd.c:156) portent is_ide ; xtide_at et xtide_ps1 aussi, mais ne sont pas
    // transcrites.
    internal static bool hdd_controller_current_is_ide() => hdd_c.hdd_controller_name is "ide" or "xtide";

    // pcem: ide.c:306 — compteur mort, déclaré et jamais lu.
    // omitted: `int idetimes`.

    // pcem: ide.c:307-325
    internal static void writeidew(int ide_board, uint16_t val)
    {
        IDE ide = ide_drives[cur_ide[ide_board]];

        if (ide.command == WIN_PACKETCMD)
        {
                // omitted: atapi_data_write (ide.c:311) — ATAPI hors G5. Un disque dur
                //   n'y arrive qu'avec WIN_PACKETCMD : arrêt bruyant, comme l'oracle.
                fatal("ATAPI non transcrit (G5) : atapi_data_write\n");
        }
        else
        {
                ide.SetW(ide.pos >> 1, val);
                ide.pos += 2;

                if (ide.pos >= 512)
                {
                        ide.pos = 0;
                        ide.atastat = BUSY_STAT;
                        if (ide.command == WIN_WRITE_MULTIPLE)
                                callbackide(ide_board);
                        else
                                timer_set_delay_u64(ide_timer[ide_board], 6 * IDE_TIME);
                }
        }
    }

    // pcem: ide.c:327-331
    internal static void writeidel(int ide_board, uint32_t val)
    {
        writeidew(ide_board, (uint16_t)val);
        writeidew(ide_board, (uint16_t)(val >> 16));
    }

    // pcem: ide.c:333-605
    internal static void writeide(int ide_board, uint16_t addr, uint8_t val)
    {
        IDE ide = ide_drives[cur_ide[ide_board]];
        IDE ide_other = ide_drives[cur_ide[ide_board] ^ 1];

        // omitted: les blocs commentés et les pclog de ide.c:337-352.
        addr |= 0x80;

        if (ide.type == IDE_NONE && (addr == 0x1f0 || addr == 0x1f7))
                return;

        switch (addr)
        {
        case 0x1F0: /* Data */
                writeidew(ide_board, (uint16_t)(val | (val << 8)));
                return;

        case 0x1F1: /* Features */
                ide.cylprecomp = val;
                ide_other.cylprecomp = val;
                return;

        case 0x1F2: /* Sector count */
                ide.secount = val;
                ide_other.secount = val;
                return;

        case 0x1F3: /* Sector */
                ide.sector = val;
                ide.lba_addr = (ide.lba_addr & 0xFFFFF00) | val;
                ide_other.sector = val;
                ide_other.lba_addr = (ide_other.lba_addr & 0xFFFFF00) | val;
                return;

        case 0x1F4: /* Cylinder low */
                ide.cylinder = (ide.cylinder & 0xFF00) | val;
                ide.lba_addr = (ide.lba_addr & 0xFFF00FF) | ((uint32_t)val << 8);
                ide_other.cylinder = (ide_other.cylinder & 0xFF00) | val;
                ide_other.lba_addr = (ide_other.lba_addr & 0xFFF00FF) | ((uint32_t)val << 8);
                return;

        case 0x1F5: /* Cylinder high */
                ide.cylinder = (ide.cylinder & 0xFF) | (val << 8);
                ide.lba_addr = (ide.lba_addr & 0xF00FFFF) | ((uint32_t)val << 16);
                ide_other.cylinder = (ide_other.cylinder & 0xFF) | (val << 8);
                ide_other.lba_addr = (ide_other.lba_addr & 0xF00FFFF) | ((uint32_t)val << 16);
                return;

        case 0x1F6: /* Drive/Head */
                if (cur_ide[ide_board] != ((val >> 4) & 1) + (ide_board << 1))
                {
                        cur_ide[ide_board] = ((val >> 4) & 1) + (ide_board << 1);

                        // LA SÉLECTION PENDANT UN RESET REND LA MAIN TOUT DE SUITE : la tête et
                        // le bit LBA de CETTE écriture ne sont pas retenus (ide.c:404-420).
                        // pcem bug, reproduced: PB-72
                        if (ide.reset != 0 || ide_other.reset != 0)
                        {
                                ide.atastat = ide_other.atastat = READY_STAT | DSC_STAT;
                                ide.error = ide_other.error = 1;
                                ide.secount = ide_other.secount = 1;
                                ide.sector = ide_other.sector = 1;
                                ide.head = ide_other.head = 0;
                                ide.cylinder = ide_other.cylinder = 0;
                                ide.reset = ide_other.reset = 0;
                                // ide->blocksize = ide_other->blocksize = 0;
                                if (IDE_DRIVE_IS_CDROM(ide))
                                        ide.cylinder = 0xEB14;
                                if (IDE_DRIVE_IS_CDROM(ide_other))
                                        ide_other.cylinder = 0xEB14;

                                timer_disable(ide_timer[ide_board]);
                                return;
                        }

                        ide = ide_drives[cur_ide[ide_board]];
                }

                ide.head = val & 0xF;
                ide.lba = val & 0x40;
                ide_other.head = val & 0xF;
                ide_other.lba = val & 0x40;

                ide.lba_addr = (ide.lba_addr & 0x0FFFFFF) | ((uint32_t)(val & 0xF) << 24);
                ide_other.lba_addr = (ide_other.lba_addr & 0x0FFFFFF) | ((uint32_t)(val & 0xF) << 24);

                ide_irq_update(ide);
                return;

        case 0x1F7: /* Command register */
                if (ide.type == IDE_NONE)
                        return;
                ide_irq_lower(ide);
                ide.command = val;

                ide.error = 0;
                switch (val)
                {
                case WIN_SRST: /* ATAPI Device Reset */
                        if (IDE_DRIVE_IS_CDROM(ide))
                                ide.atastat = BUSY_STAT;
                        else
                                ide.atastat = READY_STAT;
                        timer_set_delay_u64(ide_timer[ide_board], 100 * IDE_TIME);
                        return;

                case WIN_RESTORE:
                case WIN_SEEK:
                        ide.atastat = READY_STAT | BUSY_STAT;
                        timer_set_delay_u64(ide_timer[ide_board], 100 * IDE_TIME);
                        return;

                case WIN_READ_MULTIPLE:
                        // UN BLOCSIZE NUL ARRÊTE PCem : l'invité peut envoyer READ MULTIPLE
                        // avant tout SET MULTIPLE MODE, et PCem appelle fatal().
                        // pcem bug, not reproduced: PB-73
                        // DEVIATION: ABRT, comme le disque réel — règle de TRANSCRIPTION.md,
                        //   l'invité ne tue pas l'hôte.
                        if (ide.blocksize == 0 && (ide.type != IDE_CDROM))
                        {
                                MultipleSansBloc(ide);
                                return;
                        }
                        ide.blockcount = 0;
                        goto case WIN_READ;

                case WIN_READ:
                case WIN_READ_NORETRY:
                case WIN_READ_DMA:
                        ide.atastat = BUSY_STAT;
                        timer_set_delay_u64(ide_timer[ide_board], 200 * IDE_TIME);
                        ide.do_initial_read = 1;
                        return;

                case WIN_WRITE_MULTIPLE:
                        // pcem bug, not reproduced: PB-73
                        if (ide.blocksize == 0 && (ide.type != IDE_CDROM))
                        {
                                MultipleSansBloc(ide);
                                return;
                        }
                        ide.blockcount = 0;
                        goto case WIN_WRITE;

                case WIN_WRITE:
                case WIN_WRITE_NORETRY:
                        ide.atastat = DRQ_STAT | DSC_STAT | READY_STAT;
                        ide.pos = 0;
                        return;

                case WIN_WRITE_DMA:
                        ide.atastat = BUSY_STAT;
                        timer_set_delay_u64(ide_timer[ide_board], 200 * IDE_TIME);
                        return;

                case WIN_VERIFY:
                case WIN_VERIFY_ONCE:
                        ide.atastat = BUSY_STAT;
                        timer_set_delay_u64(ide_timer[ide_board], 200 * IDE_TIME);
                        return;

                case WIN_FORMAT:
                        ide.atastat = DRQ_STAT;
                        ide.pos = 0;
                        return;

                case WIN_SPECIFY: /* Initialize Drive Parameters */
                        ide.atastat = BUSY_STAT;
                        timer_set_delay_u64(ide_timer[ide_board], 30 * IDE_TIME);
                        return;

                case WIN_DRIVE_DIAGNOSTICS: /* Execute Drive Diagnostics */
                        ide.atastat = BUSY_STAT;
                        timer_set_delay_u64(ide_timer[ide_board], 200 * IDE_TIME);
                        return;

                case WIN_PIDENTIFY:         /* Identify Packet Device */
                case WIN_SET_MULTIPLE_MODE: /*Set Multiple Mode*/
                case WIN_SETIDLE1:          /* Idle */
                case WIN_CHECK_POWER_MODE:
                        ide.atastat = BUSY_STAT;
                        callbackide(ide_board);
                        return;

                case WIN_IDENTIFY: /* Identify Device */
                case WIN_SET_FEATURES:
                        ide.atastat = BUSY_STAT;
                        timer_set_delay_u64(ide_timer[ide_board], 200 * IDE_TIME);
                        return;

                case WIN_PACKETCMD: /* ATAPI Packet */
                        // omitted: atapi_command_start (ide.c:568-569) — gardé par
                        //   `ide->type == IDE_CDROM`, jamais vrai ici.
                        ide.atastat = BUSY_STAT;
                        timer_set_delay_u64(ide_timer[ide_board], IDE_TIME);

                        // omitted: `ide->atapi.bus_state = 0` (ide.c:574) — champ ATAPI.
                        return;

                case 0xF0:
                default:
                        ide.atastat = READY_STAT | ERR_STAT | DSC_STAT;
                        ide.error = ABRT_ERR;
                        ide_irq_raise(ide);
                        /*                        fatal("Bad IDE command %02X\n", val);*/
                        return;
                }

        case 0x3F6: /* Device control */
                if ((ide.fdisk & 4) != 0 && (val & 4) == 0 && (ide.type != IDE_NONE || ide_other.type != IDE_NONE))
                {
                        timer_set_delay_u64(ide_timer[ide_board], 500 * IDE_TIME);
                        ide.reset = ide_other.reset = 1;
                        ide.atastat = ide_other.atastat = BUSY_STAT;
                }
                if ((val & 4) != 0)
                {
                        /*Drive held in reset*/
                        timer_disable(ide_timer[ide_board]);
                        ide.atastat = ide_other.atastat = BUSY_STAT;
                }
                ide.fdisk = ide_other.fdisk = val;
                ide_irq_update(ide);
                return;
        }
        //        fatal("Bad IDE write %04X %02X\n", addr, val);
    }

    // DEVIATION: PB-73 — READ ou WRITE MULTIPLE sans taille de bloc : ce que fait le disque
    //   réel, ABRT, dans la forme du `default:` de writeide (ide.c:577-583).
    private static void MultipleSansBloc(IDE ide)
    {
        ide.command = 0;
        ide.atastat = READY_STAT | ERR_STAT | DSC_STAT;
        ide.error = ABRT_ERR;
        ide_irq_raise(ide);
    }

    // pcem: ide.c:607-720
    internal static uint8_t readide(int ide_board, uint16_t addr)
    {
        IDE ide = ide_drives[cur_ide[ide_board]];
        uint8_t temp = 0xff;
        uint16_t tempw;

        addr |= 0x80;

        // omitted: les blocs commentés et les pclog de ide.c:614-628.
        if (ide.type == IDE_NONE && (addr == 0x1f0 || addr == 0x1f7))
                return 0;
        switch (addr)
        {
        case 0x1F0: /* Data */
                tempw = readidew(ide_board);
                temp = (uint8_t)(tempw & 0xff);
                break;

        case 0x1F1: /* Error */
                temp = ide.error;
                break;

        case 0x1F2: /* Sector count */
                // omitted: atapi_read_iir (ide.c:643-644) — gardé par IDE_CDROM.
                temp = (uint8_t)ide.secount;
                break;

        case 0x1F3: /* Sector */
                temp = (uint8_t)ide.sector;
                break;

        case 0x1F4: /* Cylinder low */
                temp = (uint8_t)(ide.cylinder & 0xFF);
                break;

        case 0x1F5: /* Cylinder high */
                temp = (uint8_t)(ide.cylinder >> 8);
                break;

        case 0x1F6: /* Drive/Head */
                temp = (uint8_t)(ide.head | ((cur_ide[ide_board] & 1) != 0 ? 0x10 : 0) | (ide.lba != 0 ? 0x40 : 0) | 0xa0);
                break;

        case 0x1F7: /* Status */
                if (ide.type == IDE_NONE)
                {
                        temp = 0;
                        break;
                }
                ide_irq_lower(ide);
                // omitted: la branche IDE_CDROM de ide.c:674-683 (SERVICE_STAT,
                //   atapi_read_drq) — jamais prise ici.
                temp = ide.atastat;
                break;

        case 0x3F6: /* Alternate Status */
                if (ide.type == IDE_NONE)
                {
                        temp = 0;
                        break;
                }
                // omitted: la branche IDE_CDROM de ide.c:699-708.
                temp = ide.atastat;
                break;
        }
        return temp;
        //        fatal("Bad IDE read %04X\n", addr);
    }

    // pcem: ide.c:722-762
    internal static uint16_t readidew(int ide_board)
    {
        IDE ide = ide_drives[cur_ide[ide_board]];
        uint16_t temp;

        if (ide.command == WIN_PACKETCMD)
        {
                // omitted: atapi_data_read (ide.c:735) — voir writeidew.
                fatal("ATAPI non transcrit (G5) : atapi_data_read\n");
                return 0;
        }
        else
        {
                temp = ide.W(ide.pos >> 1);
                ide.pos += 2;
                if (ide.pos >= 512 && ide.command != 0)
                {
                        ide.pos = 0;
                        ide.atastat = READY_STAT | DSC_STAT;
                        if (ide.command == WIN_READ || ide.command == WIN_READ_NORETRY || ide.command == WIN_READ_MULTIPLE)
                        {
                                ide.secount = (ide.secount - 1) & 0xff;
                                if (ide.secount != 0)
                                {
                                        ide_next_sector(ide);
                                        ide.atastat = BUSY_STAT | READY_STAT | DSC_STAT;
                                        if (ide.command == WIN_READ_MULTIPLE)
                                                callbackide(ide_board);
                                        else
                                                timer_set_delay_u64(ide_timer[ide_board], 6 * IDE_TIME);
                                }
                        }
                }
        }
        return temp;
    }

    // pcem: ide.c:764-769
    internal static uint32_t readidel(int ide_board)
    {
        uint16_t temp;
        temp = readidew(ide_board);
        return temp | ((uint32_t)readidew(ide_board) << 16);
    }

    // pcem: ide.c:771 — compté, jamais lu hors d'un commentaire.
    internal static int times30 = 0;

    // pcem: ide.c:772-1129
    internal static void callbackide(int ide_board)
    {
        IDE ide = ide_drives[cur_ide[ide_board]];
        IDE ide_other = ide_drives[cur_ide[ide_board] ^ 1];

        ext_ide = ide;
        if (ide.command == 0x30)
                times30++;
        if (ide.reset != 0)
        {
                ide.atastat = ide_other.atastat = READY_STAT | DSC_STAT;
                ide.error = ide_other.error = 1;
                ide.secount = ide_other.secount = 1;
                ide.sector = ide_other.sector = 1;
                ide.head = ide_other.head = 0;
                ide.cylinder = ide_other.cylinder = 0;
                ide.reset = ide_other.reset = 0;
                // Les quatre `atapi->stop()` de ide.c:796-812 visent le pilote CD de l'hôte,
                // que PCem pose TOUJOURS (cdrom_null_open, pc.c:293) : sans lecteur, c'est
                // null_stop, vide (cdrom-null.c:19). Pas de pointeur nul, rien à transcrire.
                if (IDE_DRIVE_IS_CDROM(ide))
                {
                        ide.cylinder = 0xEB14;
                        // omitted: atapi->stop() ; atapi_reset(&ide->atapi) — IDE_CDROM.
                }
                if (ide.type == IDE_NONE)
                {
                        ide.cylinder = 0xFFFF;
                        ide.error = 0xff;
                        // omitted: atapi->stop() — null_stop, vide.
                }
                if (IDE_DRIVE_IS_CDROM(ide_other))
                {
                        ide_other.cylinder = 0xEB14;
                        // omitted: atapi->stop() ; atapi_reset(&ide_other->atapi) — IDE_CDROM.
                }
                if (ide_other.type == IDE_NONE)
                {
                        ide_other.cylinder = 0xFFFF;
                        ide_other.error = 0xff;
                        // omitted: atapi->stop() — null_stop, vide.
                }
                return;
        }
        switch (ide.command)
        {
        // Initialize the Task File Registers as follows: Status = 00h, Error = 01h, Sector Count = 01h, Sector Number =
        // 01h, Cylinder Low = 14h, Cylinder High =EBh and Drive/Head = 00h.
        case WIN_SRST: /*ATAPI Device Reset */
                ide.atastat = READY_STAT | DSC_STAT;
                ide.error = 1; /*Device passed*/
                ide.secount = ide.sector = 1;
                ide_set_signature(ide);
                if (IDE_DRIVE_IS_CDROM(ide))
                        ide.atastat = 0;
                ide_irq_raise(ide);
                if (IDE_DRIVE_IS_CDROM(ide))
                        ide.service = 0;
                return;

        case WIN_RESTORE:
        case WIN_SEEK:
                if (IDE_DRIVE_IS_CDROM(ide))
                {
                        // omitted: pclog("WIN_RESTORE callback on CD-ROM\n").
                        goto abort_cmd;
                }
                ide.atastat = READY_STAT | DSC_STAT;
                ide_irq_raise(ide);
                return;

        case WIN_READ:
        case WIN_READ_NORETRY:
                if (IDE_DRIVE_IS_CDROM(ide))
                {
                        ide_set_signature(ide);
                        goto abort_cmd;
                }
                if (ide.do_initial_read != 0)
                {
                        ide.do_initial_read = 0;
                        ide.sector_pos = 0;
                        hdd_file.hdd_read_sectors(ide.hdd_file, (int)ide_get_sector(ide), ide.secount != 0 ? ide.secount : 256,
                                                  ide.sector_buffer);
                }
                Array.Copy(ide.sector_buffer, ide.sector_pos * 512, ide.buffer, 0, 512);
                ide.sector_pos++;
                ide.pos = 0;
                ide.atastat = DRQ_STAT | READY_STAT | DSC_STAT;
                ide_irq_raise(ide);

                readflash_set(READFLASH_HDC, ide.drive);
                return;

        case WIN_READ_DMA:
                if (IDE_DRIVE_IS_CDROM(ide))
                {
                        goto abort_cmd;
                }
                if (ide.do_initial_read != 0)
                {
                        ide.do_initial_read = 0;
                        ide.sector_pos = 0;
                        hdd_file.hdd_read_sectors(ide.hdd_file, (int)ide_get_sector(ide), ide.secount != 0 ? ide.secount : 256,
                                                  ide.sector_buffer);
                }
                ide.pos = 0;

                // SANS BUS MASTER, RIEN NE SE PASSE : atastat reste BUSY et aucune
                // interruption ne vient. Une machine ISA ne pose jamais le pointeur ; un
                // pilote qui tente le DMA attend indéfiniment — chez PCem comme ici.
                if (ide_bus_master_read_data != null)
                {
                        // Le C passe &sector_buffer[sector_pos * 512] : une copie tient lieu
                        // de pointeur intérieur, le bus master ne faisant que lire.
                        var chunk = new uint8_t[512];
                        Array.Copy(ide.sector_buffer, ide.sector_pos * 512, chunk, 0, 512);
                        if (ide_bus_master_read_data(ide_board, chunk, 512, ide_bus_master_p) != 0)
                                timer_set_delay_u64(ide_timer[ide_board], 6 * IDE_TIME); /*DMA not performed, try again later*/
                        else
                        {
                                /*DMA successful*/
                                ide.sector_pos++;
                                ide.atastat = DRQ_STAT | READY_STAT | DSC_STAT;

                                ide.secount = (ide.secount - 1) & 0xff;
                                if (ide.secount != 0)
                                {
                                        ide_next_sector(ide);
                                        ide.atastat = BUSY_STAT;
                                        timer_set_delay_u64(ide_timer[ide_board], 6 * IDE_TIME);
                                }
                                else
                                {
                                        ide_irq_raise(ide);
                                }
                        }
                }

                readflash_set(READFLASH_HDC, ide.drive);
                return;

        case WIN_READ_MULTIPLE:
                if (IDE_DRIVE_IS_CDROM(ide))
                {
                        goto abort_cmd;
                }
                if (ide.do_initial_read != 0)
                {
                        ide.do_initial_read = 0;
                        ide.sector_pos = 0;
                        hdd_file.hdd_read_sectors(ide.hdd_file, (int)ide_get_sector(ide), ide.secount != 0 ? ide.secount : 256,
                                                  ide.sector_buffer);
                }
                Array.Copy(ide.sector_buffer, ide.sector_pos * 512, ide.buffer, 0, 512);
                ide.sector_pos++;
                ide.pos = 0;
                ide.atastat = DRQ_STAT | READY_STAT | DSC_STAT;
                if (ide.blockcount == 0) // || ide->secount == 1)
                {
                        ide_irq_raise(ide);
                }
                ide.blockcount++;
                if (ide.blockcount >= ide.blocksize)
                        ide.blockcount = 0;

                readflash_set(READFLASH_HDC, ide.drive);
                return;

        case WIN_WRITE:
        case WIN_WRITE_NORETRY:
                if (IDE_DRIVE_IS_CDROM(ide))
                {
                        goto abort_cmd;
                }
                hdd_file.hdd_write_sectors(ide.hdd_file, (int)ide_get_sector(ide), 1, ide.buffer);
                ide_irq_raise(ide);
                ide.secount = (ide.secount - 1) & 0xff;
                if (ide.secount != 0)
                {
                        ide.atastat = DRQ_STAT | READY_STAT | DSC_STAT;
                        ide.pos = 0;
                        ide_next_sector(ide);
                }
                else
                        ide.atastat = READY_STAT | DSC_STAT;

                readflash_set(READFLASH_HDC, ide.drive);
                return;

        case WIN_WRITE_DMA:
                if (IDE_DRIVE_IS_CDROM(ide))
                {
                        goto abort_cmd;
                }

                if (ide_bus_master_write_data != null)
                {
                        if (ide_bus_master_write_data(ide_board, ide.buffer, 512, ide_bus_master_p) != 0)
                                timer_set_delay_u64(ide_timer[ide_board], 6 * IDE_TIME); /*DMA not performed, try again later*/
                        else
                        {
                                /*DMA successful*/
                                hdd_file.hdd_write_sectors(ide.hdd_file, (int)ide_get_sector(ide), 1, ide.buffer);

                                ide.atastat = DRQ_STAT | READY_STAT | DSC_STAT;

                                ide.secount = (ide.secount - 1) & 0xff;
                                if (ide.secount != 0)
                                {
                                        ide_next_sector(ide);
                                        ide.atastat = BUSY_STAT;
                                        timer_set_delay_u64(ide_timer[ide_board], 6 * IDE_TIME);
                                }
                                else
                                {
                                        ide_irq_raise(ide);
                                }
                        }
                }

                readflash_set(READFLASH_HDC, ide.drive);
                return;

        case WIN_WRITE_MULTIPLE:
                if (IDE_DRIVE_IS_CDROM(ide))
                {
                        goto abort_cmd;
                }
                hdd_file.hdd_write_sectors(ide.hdd_file, (int)ide_get_sector(ide), 1, ide.buffer);
                ide.blockcount++;
                if (ide.blockcount >= ide.blocksize || ide.secount == 1)
                {
                        ide.blockcount = 0;
                        ide_irq_raise(ide);
                }
                ide.secount = (ide.secount - 1) & 0xff;
                if (ide.secount != 0)
                {
                        ide.atastat = DRQ_STAT | READY_STAT | DSC_STAT;
                        ide.pos = 0;
                        ide_next_sector(ide);
                }
                else
                        ide.atastat = READY_STAT | DSC_STAT;

                readflash_set(READFLASH_HDC, ide.drive);
                return;

        case WIN_VERIFY:
        case WIN_VERIFY_ONCE:
                if (IDE_DRIVE_IS_CDROM(ide))
                {
                        goto abort_cmd;
                }
                // VERIFY NE BOUGE RIEN : ni les registres d'adresse, ni le compteur — le
                // disque rend la main au premier rappel, comme s'il n'y avait qu'un secteur.
                // pcem bug, reproduced: PB-74
                ide.pos = 0;
                ide.atastat = READY_STAT | DSC_STAT;
                ide_irq_raise(ide);

                readflash_set(READFLASH_HDC, ide.drive);
                return;

        case WIN_FORMAT:
                if (IDE_DRIVE_IS_CDROM(ide))
                {
                        goto abort_cmd;
                }
                hdd_file.hdd_format_sectors(ide.hdd_file, (int)ide_get_sector(ide), ide.secount);
                ide.atastat = READY_STAT | DSC_STAT;
                ide_irq_raise(ide);

                readflash_set(READFLASH_HDC, ide.drive);
                return;

        case WIN_DRIVE_DIAGNOSTICS:
                ide_set_signature(ide);
                ide.error = 1; /*No error detected*/
                if (IDE_DRIVE_IS_CDROM(ide))
                {
                        ide.atastat = 0;
                }
                else
                {
                        ide.atastat = READY_STAT | DSC_STAT;
                        ide_irq_raise(ide);
                }
                return;

        case WIN_SPECIFY: /* Initialize Drive Parameters */
                if (IDE_DRIVE_IS_CDROM(ide))
                {
                        // omitted: pclog("IS CDROM - ABORT\n").
                        goto abort_cmd;
                }
                ide.hdd_file.spt = ide.secount;
                ide.hdd_file.hpc = ide.head + 1;
                ide.atastat = READY_STAT | DSC_STAT;
                ide_irq_raise(ide);
                return;

        case WIN_PIDENTIFY: /* Identify Packet Device */
                // omitted: la branche IDE_CDROM de ide.c:1050-1059 (atapi_identify).
                goto abort_cmd;

        case WIN_SET_MULTIPLE_MODE:
                if (IDE_DRIVE_IS_CDROM(ide))
                {
                        goto abort_cmd;
                }
                ide.blocksize = ide.secount;
                ide.atastat = READY_STAT | DSC_STAT;
                // omitted: pclog("Set multiple mode - %i\n") — sortie pure.
                ide_irq_raise(ide);
                return;

        case WIN_SETIDLE1: /* Idle */
                goto abort_cmd;

        case WIN_IDENTIFY: /* Identify Device */
                if (ide.type == IDE_NONE)
                {
                        goto abort_cmd;
                }
                if (IDE_DRIVE_IS_CDROM(ide))
                {
                        ide_set_signature(ide);
                        goto abort_cmd;
                }
                else
                {
                        ide_identify(ide);
                        ide.pos = 0;
                        ide.atastat = DRQ_STAT | READY_STAT | DSC_STAT;
                        ide_irq_raise(ide);
                }
                return;

        case WIN_SET_FEATURES:
                if (ide.type == IDE_NONE)
                        goto abort_cmd;
                if (!IDE_DRIVE_IS_CDROM(ide))
                        goto abort_cmd;

                // omitted: atapi_set_feature (ide.c:1099-1103) — IDE_CDROM seulement.
                goto abort_cmd;

        case WIN_CHECK_POWER_MODE:
                if (ide.type == IDE_NONE)
                {
                        goto abort_cmd;
                }
                ide.secount = 0xff;
                ide.atastat = READY_STAT | DSC_STAT;
                ide_irq_raise(ide);
                return;

        case WIN_PACKETCMD: /* ATAPI Packet */
                if (!IDE_DRIVE_IS_CDROM(ide))
                        goto abort_cmd;

                // omitted: atapi_process_packet (ide.c:1118) — IDE_CDROM seulement.
                return;
        }

    abort_cmd:
        ide.command = 0;
        ide.atastat = READY_STAT | ERR_STAT | DSC_STAT;
        ide.error = ABRT_ERR;
        ide.pos = 0;
        ide_irq_raise(ide);
    }

    // pcem: ide.c:1131-1133
    private static void ide_callback_pri(object? p) { callbackide(0); }

    private static void ide_callback_sec(object? p) { callbackide(1); }

    // pcem: ide.c:1135-1147
    private static void ide_write_pri(uint16_t addr, uint8_t val, object priv) { writeide(0, addr, val); }
    private static void ide_write_pri_w(uint16_t addr, uint16_t val, object priv) { writeidew(0, val); }
    private static void ide_write_pri_l(uint16_t addr, uint32_t val, object priv) { writeidel(0, val); }
    private static uint8_t ide_read_pri(uint16_t addr, object priv) => readide(0, addr);
    private static uint16_t ide_read_pri_w(uint16_t addr, object priv) => readidew(0);
    private static uint32_t ide_read_pri_l(uint16_t addr, object priv) => readidel(0);

    private static void ide_write_sec(uint16_t addr, uint8_t val, object priv) { writeide(1, addr, val); }
    private static void ide_write_sec_w(uint16_t addr, uint16_t val, object priv) { writeidew(1, val); }
    private static void ide_write_sec_l(uint16_t addr, uint32_t val, object priv) { writeidel(1, val); }
    private static uint8_t ide_read_sec(uint16_t addr, object priv) => readide(1, addr);
    private static uint16_t ide_read_sec_w(uint16_t addr, object priv) => readidew(1);
    private static uint32_t ide_read_sec_l(uint16_t addr, object priv) => readidel(1);

    // pcem: ide.c:1149-1171 — les délégués sont créés UNE fois : io_removehandler
    // compare les gestionnaires, et deux conversions de groupe de méthodes rendent deux
    // délégués égaux en valeur mais pas forcément en identité.
    private static readonly inb_fn rp = ide_read_pri;
    private static readonly inw_fn rpw = ide_read_pri_w;
    private static readonly inl_fn rpl = ide_read_pri_l;
    private static readonly outb_fn wp = ide_write_pri;
    private static readonly outw_fn wpw = ide_write_pri_w;
    private static readonly outl_fn wpl = ide_write_pri_l;
    private static readonly inb_fn rs = ide_read_sec;
    private static readonly inw_fn rsw = ide_read_sec_w;
    private static readonly inl_fn rsl = ide_read_sec_l;
    private static readonly outb_fn ws = ide_write_sec;
    private static readonly outw_fn wsw = ide_write_sec_w;
    private static readonly outl_fn wsl = ide_write_sec_l;

    internal static void ide_pri_enable()
    {
        io_sethandler(0x01f0, 0x0008, rp, rpw, rpl, wp, wpw, wpl, null);
        io_sethandler(0x03f6, 0x0001, rp, null, null, wp, null, null, null);
    }

    internal static void ide_pri_disable()
    {
        io_removehandler(0x01f0, 0x0008, rp, rpw, rpl, wp, wpw, wpl, null);
        io_removehandler(0x03f6, 0x0001, rp, null, null, wp, null, null, null);
    }

    internal static void ide_sec_enable()
    {
        io_sethandler(0x0170, 0x0008, rs, rsw, rsl, ws, wsw, wsl, null);
        io_sethandler(0x0376, 0x0001, rs, null, null, ws, null, null, null);
    }

    internal static void ide_sec_disable()
    {
        io_removehandler(0x0170, 0x0008, rs, rsw, rsl, ws, wsw, wsl, null);
        io_removehandler(0x0376, 0x0001, rs, null, null, ws, null, null, null);
    }

    // pcem: ide.c:1173-1181
    private static object? ide_init()
    {
        ide_pri_enable();
        ide_sec_enable();

        timer_add(ide_timer[0], ide_callback_pri, null, 0);
        timer_add(ide_timer[1], ide_callback_sec, null, 0);

        return -1;
    }

    // pcem: ide.c:1183-1190
    private static void ide_close(object p)
    {
        int c;

        for (c = 0; c < 4; c++)
        {
                ide_drives[c].type = IDE_NONE;
                hdd_file.hdd_close(ide_drives[c].hdd_file);
        }
    }

    // pcem: ide.c:1192-1199
    internal static void ide_set_bus_master(bus_master_data_fn? read_data, bus_master_data_fn? write_data,
                                            bus_master_irq_fn? set_irq, object? p)
    {
        ide_bus_master_read_data = read_data;
        ide_bus_master_write_data = write_data;
        ide_bus_master_set_irq = set_irq;
        ide_bus_master_p = p;
    }

    // pcem: ide.c:1201-1220
    internal static void ide_reset_devices()
    {
        if (ide_drives[0].type != IDE_NONE || ide_drives[1].type != IDE_NONE)
        {
                timer_set_delay_u64(ide_timer[0], 500 * IDE_TIME);
                ide_drives[0].reset = ide_drives[1].reset = 1;
                ide_drives[0].atastat = ide_drives[1].atastat = BUSY_STAT;
                if (ide_drives[0].type != IDE_NONE)
                        cur_ide[0] = 0;
                else
                        cur_ide[0] = 1;
        }
        if (ide_drives[2].type != IDE_NONE || ide_drives[3].type != IDE_NONE)
        {
                timer_set_delay_u64(ide_timer[1], 500 * IDE_TIME);
                ide_drives[2].reset = ide_drives[3].reset = 1;
                ide_drives[2].atastat = ide_drives[3].atastat = BUSY_STAT;
                if (ide_drives[2].type != IDE_NONE)
                        cur_ide[1] = 2;
                else
                        cur_ide[1] = 3;
        }
    }

    // pcem: ide.c:1222
    internal static device_t ide_device = new device_t("Standard IDE", DEVICE_AT, ide_init, ide_close,
                                                      null, null, null, null, null);
}
