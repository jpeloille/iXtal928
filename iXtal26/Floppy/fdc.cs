// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/floppy/fdc.c + includes/private/floppy/fdc.h
// SHA256: voir oracle.tsv ; vérifier avec tools/check-oracle.sh
// STATUS: transcribed — le NEC 765 / 82077 : ports 0x3f0-0x3f7, commandes, FIFO,
//         DMA canal 2, IRQ 6, chien de garde PCjr. Conteneur fdc_c : l'instance
//         `static FDC fdc` de fdc.c:80 garde son nom.
//
// noms: PCem → iXtal26 → ce qu'il désigne — R1(e). « = » : nom inchangé. Abréviations
//   de la fiche du µPD765 / 82077 sauf mention contraire.
//   stat        msr             Main Status Register, port 0x3f4 — bits MSR_* plus bas
//   dat         data_reg        registre de données, port 0x3f5
//   res[]       result[]        phase résultat, lue depuis la FIN : [RES_N-result_togo]
//   pnum/ptot   param_pos/param_total   paramètres reçus / attendus
//   paramstogo  result_togo     octets de résultat restant à rendre
//   discint     exec_state      état dispatché par fdc_callback ; sentinelles EXEC_*
//   fdc_reset_stat  reset_sense_togo  les quatre Sense interrupt suivant un reset
//   tc          terminal_count  le signal du DMA qui clôt le transfert
//   eot[]       end_of_track[]  dernier secteur de la piste, paramètre de commande
//   rwc[]       rate_override[] forçage de débit par lecteur (Winbond W83877F, :294)
//   drvrate[]   drive_rate[]    débit propre au lecteur, distinct de fdc.rate
//   pretrk      precomp_track   piste de début de précompensation d'écriture
//   perp        perp_mode       commande 0x12, mode perpendiculaire (disquettes ED)
//   fifo/tfifo  fifo_enabled/fifo_threshold   FIFO et seuil, posés par Configure
//   fifobuf/fifobufpos  fifo_buf/fifo_pos     le tampon et son index
//   inread      reading         une lecture est en cours
//   written     byte_written    MORT : jamais posé à 1 — PB-19
//   dor         =               Digital Output Register, 0x3f2 : moteur, sélection
//   st0         =               ST0 mémorisé, rendu par Sense interrupt status
//   params[]    =               octets reçus en phase commande
//   command     =               opcode ; 0x06/0x26/0x46… diffèrent par MT, MFM, SK
//   track[]     =               piste où le contrôleur CROIT être ; la vraie : fdd.c
//   sector_size =               code N : la taille vaut 128 << N octets
//   specify[]   =               les deux octets de Specify : pas, chargement, DMA
//   lock        =               commande 0x94/0x14 : protège FIFO et seuil d'un reset
//   densel*     =               DENSity SELect : broche de densité envoyée au lecteur
//   pos         =               index dans format_dat ; remis à zéro à chaque commande
//   is_nsc      =               Super I/O National Semiconductor ; pas sur 5150
//   enh_mode    =               mode étendu du TDR, port 0x3f3 ; pas sur 5150
//   pcjr/ps1    =               variantes de machine ; le PCjr a le chien de garde
//   disc_3f7    =               dernière valeur écrite au CCR, port 0x3f7
//   lastbyte    =               le dernier octet lu a vidé la phase résultat
//   abort, discmodified[], discrate[]  =  MORTS — PB-20
//   TYPES : FDC = la struct de fdc.c:27-78 ; fdc_c = le conteneur du fichier, suffixe
//   _c parce que PCem n'a pas de classe et que `fdc` est pris par l'instance.
//   Les FONCTIONS gardent toutes les noms de PCem : elles sont l'API entre modules.
//   Les étiquettes de la sonde (BootDiff.cs:264) sont des chaînes et gardent elles
//   aussi les noms PCem : elles désignent les globales du C, pas les champs d'ici.

// CS8600/CS8602 : `FDC fdc = (FDC)p;` dans fdc_watchdog_poll — le `void *p` des
// timers est déclaré `object?` ; ici il vaut toujours &fdc (fdc.c:1265). Même cas
// qu'à vid_cga.cs:16.
#pragma warning disable CS8600, CS8602

using System.Runtime.CompilerServices;
using static iXtal26.Cpu.x86;
using static iXtal26.Disc.disc;
using static iXtal26.Disc.disc_sector;
using static iXtal26.Floppy.fdd_c;
using static iXtal26.Models.dma;
using static iXtal26.Models.pic;
using static iXtal26.io;
using static iXtal26.pc;
using static iXtal26.timer;

namespace iXtal26.Floppy;

// pcem: fdc.c:27-78
// Classe et non struct : timer_add prend l'adresse de fdc.timer et de
// fdc.watchdog_timer, et fdc_add_pcjr passe &fdc en `void *p`.
internal sealed class FDC
{
    internal uint8_t dor, msr, command, data_reg, st0;
    internal int head;
    internal int[] track = new int[256];
    internal int sector, drive, lastdrive;
    internal int sector_size;
    internal int rw_track;
    internal int pos;
    internal uint8_t[] @params = new uint8_t[256];
    internal uint8_t[] result = new uint8_t[256];
    internal int param_pos, param_total;
    internal int rate;
    internal uint8_t[] specify = new uint8_t[256];
    internal int[] end_of_track = new int[256];
    internal int @lock;
    internal int perp_mode;
    internal uint8_t config, precomp_track;
    internal int abort;
    internal uint8_t[] format_dat = new uint8_t[256];
    internal int format_state;
    internal int terminal_count;
    internal int byte_written;

    internal int pcjr, ps1;

    internal pc_timer_t watchdog_timer = new();
    internal int watchdog_count;

    internal int data_ready;
    internal int reading;

    internal int dskchg_activelow;
    internal int enable_3f1;

    internal int bitcell_period;

    internal int is_nsc; /* 1 = FDC is on a National Semiconductor Super I/O chip, 0 = other FDC. This is needed,
                        because the National Semiconductor Super I/O chips add some FDC commands. */
    internal int enh_mode;
    internal int[] rate_override = new int[2];
    internal int boot_drive;
    internal int densel_polarity;
    internal int densel_force;
    internal int[] drive_rate = new int[2];

    internal int dma;
    internal int fifo_enabled, fifo_threshold;
    internal int fifo_pos;
    internal uint8_t[] fifo_buf = new uint8_t[16];

    internal int int_pending;

    internal pc_timer_t timer = new();
}

internal static partial class fdc_c
{
    // pcem: fdc.c:15-23
    private const int ST1_DE = (1 << 5);
    private const int ST1_ND = (1 << 2);
    private const int ST1_NW = (1 << 1);
    private const int ST1_MA = (1 << 0);

    private const int ST2_DD = (1 << 5);
    private const int ST2_WC = (1 << 4); /*Wrong cylinder*/
    private const int ST2_BC = (1 << 1); /*Bad cylinder*/
    private const int ST2_MD = (1 << 0);

    // DEVIATION: valeurs de registre que fdc.c écrit en hexadécimal nu. Nommées ici et
    //   pas dans PCem — TRANSCRIPTION.md, § Nommage explicite. Les bits du DOR n'y sont
    //   pas : le PCjr (fdc.c:287-300) ne leur donne pas la même disposition qu'un AT, et
    //   un seul jeu de noms serait faux pour l'un des deux.
    private const int MSR_RQM = 0x80;
    private const int MSR_DIO = 0x40; /*1 = FDC vers UC*/
    private const int MSR_NDM = 0x20; /*Transfert hors DMA*/
    private const int MSR_CB = 0x10;  /*Commande en cours*/
    private const int MSR_DRIVE_MASK = 0x0f;

    private const int ST0_IC_INVALID = 0x80;
    private const int ST0_IC_ABNORMAL = 0x40;
    private const int ST0_SE = 0x20; /*Seek end*/
    private const int ST0_NR = 0x08; /*Not ready*/
    private const int ST0_HD = 0x04;

    private const int ST1_OR = 0x10; /*Overrun*/

    /*Sense drive status rend ST3, dont la disposition n'est celle d'aucun des trois autres*/
    private const int ST3_FT = 0x80; /*Fault*/
    private const int ST3_WP = 0x40;
    private const int ST3_RDY = 0x20;
    private const int ST3_T0 = 0x10;
    private const int ST3_TS = 0x08; /*Two-sided*/

    /*Disposition du résultat à SEPT octets. result[] est adressé depuis la FIN
      (result[RES_N - result_togo]) : les résultats à un, deux et dix octets occupent
      d'autres tranches du même tableau et gardent donc leurs indices nus.*/
    private const int RES_ST0 = 4;
    private const int RES_ST1 = 5;
    private const int RES_ST2 = 6;
    private const int RES_C = 7;
    private const int RES_H = 8;
    private const int RES_R = 9;
    private const int RES_N = 10;

    /*Sentinelles de exec_state, hors de l'espace des opcodes*/
    private const int EXEC_END_INT = -3;
    private const int EXEC_END = -2;
    private const int EXEC_RESET = -1;
    private const int EXEC_INVALID = 0xfc;

    // pcem: fdc.h:31
    internal const int FDC_STATUS_AM_NOT_FOUND = 0;
    internal const int FDC_STATUS_NOT_FOUND = 1;
    internal const int FDC_STATUS_WRONG_CYLINDER = 2;
    internal const int FDC_STATUS_BAD_CYLINDER = 3;

    // pcem: cpu.h:163 — #define ISA_CYCLES(x) (x * isa_cycles), comme video.cs:153
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ISA_CYCLES(int x) => x * Cpu.cpu_c.isa_cycles;

    // pcem: fdc.c:25
    private static int reset_sense_togo = 0;
    /*FDC*/
    // pcem: fdc.c:80
    internal static FDC fdc = new();

    // pcem: fdc.c:84-90
    internal static int lastbyte = 0;
    internal static uint8_t disc_3f7;

    // pcem bug, reproduced: PB-20 — discmodified[] et discrate[] : ni lus ni écrits, nulle part.
    internal static int[] discmodified = new int[2];
    internal static int[] discrate = new int[2];

    internal static int exec_state;
    // pcem: fdc.c:91-103
    internal static void fdc_reset()
    {
        fdc.msr = MSR_RQM;
        fdc.param_pos = fdc.param_total = 0;
        fdc.st0 = 0;
        fdc.@lock = 0;
        fdc.head = 0;
        // pcem bug, reproduced: PB-20 — abort est écrit ici et lu nulle part ; même
        // cas que discmodified[] et discrate[] plus haut.
        fdc.abort = 0;
        if (AT == 0 && romset != ROM_XI8088 && romset != ROM_PC5086)
        {
                fdc.rate = 2;
        }
    }

    // pcem: fdc.c:105-108
    internal static void fdc_reset_fifo_buf()
    {
        Array.Clear(fdc.fifo_buf, 0, 16);
        fdc.fifo_pos = 0;
    }

    // pcem: fdc.c:110-119
    internal static void fdc_fifo_buf_write(int val)
    {
        if (fdc.fifo_pos < fdc.fifo_threshold)
        {
                fdc.fifo_buf[fdc.fifo_pos] = (uint8_t)val;
                fdc.fifo_pos++;
                fdc.fifo_pos %= fdc.fifo_threshold;
                if (fdc.fifo_pos == fdc.fifo_threshold)
                        fdc.fifo_pos = 0;
        }
    }

    // pcem: fdc.c:121-132
    internal static int fdc_fifo_buf_read()
    {
        int temp = 0;
        if (fdc.fifo_pos < fdc.fifo_threshold)
        {
                temp = fdc.fifo_buf[fdc.fifo_pos];
                fdc.fifo_pos++;
                fdc.fifo_pos %= fdc.fifo_threshold;
                if (fdc.fifo_pos == fdc.fifo_threshold)
                        fdc.fifo_pos = 0;
        }
        return temp;
    }

    /* For DMA mode, just goes ahead in FIFO buffer but doesn't actually read or write anything. */
    // pcem: fdc.c:135-143
    internal static void fdc_fifo_buf_dummy()
    {
        if (fdc.fifo_pos < fdc.fifo_threshold)
        {
                fdc.fifo_pos++;
                fdc.fifo_pos %= fdc.fifo_threshold;
                if (fdc.fifo_pos == fdc.fifo_threshold)
                        fdc.fifo_pos = 0;
        }
    }

    // pcem: fdc.c:145-148
    private static void fdc_int()
    {
        if (fdc.pcjr == 0)
                picint(1 << 6);
    }

    // pcem: fdc.c:150-163
    private static void fdc_watchdog_poll(object? p)
    {
        FDC fdc = (FDC)p;

        fdc.watchdog_count--;
        if (fdc.watchdog_count != 0)
                timer_advance_u64(fdc.watchdog_timer, 1000 * TIMER_USEC);
        else
        {
                if ((fdc.dor & 0x20) != 0)
                        picint(1 << 6);
        }
    }

    /* fdc.rate_override per Winbond W83877F datasheet:
            0 = normal;
            1 = 500 kbps, 360 rpm;
            2 = 500 kbps, 300 rpm;
            3 = 250 kbps

            Drive is only aware of selected rate and densel, so on real hardware, the rate expected by FDC and the rate actually being
            processed by drive can mismatch, in which case the FDC won't receive the correct data.
    */

    // pcem: fdc.c:174
    internal static int bit_rate = 250;

    // pcem: fdc.c:176-192
    internal static void fdc_update_is_nsc(int is_nsc) { fdc.is_nsc = is_nsc; }

    internal static void fdc_update_enh_mode(int enh_mode) { fdc.enh_mode = enh_mode; }

    internal static int fdc_get_rwc(int drive) { return fdc.rate_override[drive]; }

    internal static void fdc_update_rwc(int drive, int rwc) { fdc.rate_override[drive] = rwc; }

    internal static int fdc_get_boot_drive() { return fdc.boot_drive; }

    internal static void fdc_update_boot_drive(int boot_drive) { fdc.boot_drive = boot_drive; }

    internal static void fdc_update_densel_polarity(int densel_polarity) { fdc.densel_polarity = densel_polarity; }

    internal static void fdc_update_densel_force(int densel_force) { fdc.densel_force = densel_force; }

    internal static void fdc_update_drvrate(int drive, int drvrate) { fdc.drive_rate[drive] = drvrate; }

    // pcem: fdc.c:194-227
    internal static void fdc_update_rate(int drive)
    {
        if ((fdc.rate_override[drive] == 1) || (fdc.rate_override[drive] == 2))
        {
                bit_rate = 500;
        }
        else if (fdc.rate_override[drive] == 3)
        {
                bit_rate = 250;
        }
        else
                switch (fdc.rate)
                {
                case 0: /*High density*/
                        bit_rate = 500;
                        break;
                case 1: /*Double density (360 rpm)*/
                        switch (fdc.drive_rate[drive])
                        {
                        case 0:
                                bit_rate = 300;
                                break;
                        case 1:
                                bit_rate = 500;
                                break;
                        case 2:
                                bit_rate = 2000;
                                break;
                        }
                        break;
                case 2: /*Double density*/
                        bit_rate = 250;
                        break;
                case 3: /*Extended density*/
                        bit_rate = 1000;
                        break;
                }

        fdc.bitcell_period = 1000000 / bit_rate * 2; /*Bitcell period in ns*/
    }

    // pcem: fdc.c:229
    internal static int fdc_get_bitcell_period() { return fdc.bitcell_period; }

    // pcem: fdc.c:231-266
    private static int fdc_get_densel(int drive)
    {
        switch (fdc.rate_override[drive])
        {
        case 1:
        case 3:
                return 0;
        case 2:
                return 1;
        }

        if (fdc.is_nsc == 0)
        {
                switch (fdc.densel_force)
                {
                case 2:
                        return 1;
                case 3:
                        return 0;
                }
        }
        else
        {
                switch (fdc.densel_force)
                {
                case 0:
                        return 0;
                case 1:
                        return 1;
                }
        }

        switch (fdc.rate)
        {
        case 0:
        case 3:
                return fdc.densel_polarity != 0 ? 1 : 0;
        case 1:
        case 2:
                return fdc.densel_polarity != 0 ? 0 : 1;
        }

        return 0;
    }

    // pcem: fdc.c:268-272
    private static void fdc_rate(int drive)
    {
        fdc_update_rate(drive);
        disc_set_rate(drive, fdc.drive_rate[drive], fdc.rate);
        fdd_set_densel(fdc_get_densel(drive));
    }

    // pcem: fdc.c:274-644
    internal static void fdc_write(uint16_t addr, uint8_t val, object priv)
    {
        int drive;

        cycles -= ISA_CYCLES(8);

        switch (addr & 7)
        {
        case 1:
                return;
        case 2: /*DOR*/
                if (fdc.pcjr != 0)
                {
                        if ((fdc.dor & 0x40) != 0 && (val & 0x40) == 0)
                        {
                                timer_set_delay_u64(fdc.watchdog_timer, 1000 * TIMER_USEC);
                                fdc.watchdog_count = 1000;
                                picintc(1 << 6);
                        }
                        if ((val & 0x80) != 0 && (fdc.dor & 0x80) == 0)
                        {
                                timer_set_delay_u64(fdc.timer, 8 * TIMER_USEC);
                                exec_state = EXEC_RESET;
                                fdc_reset();
                        }
                        disc_set_motor_enable(val & 0x01);
                        fdc.drive = 0;
                }
                else
                {
                        if ((val & 4) != 0 && (fdc.dor & 4) == 0)
                        {
                                timer_set_delay_u64(fdc.timer, 8 * TIMER_USEC);
                                exec_state = EXEC_RESET;
                                fdc_reset();
                        }
                        disc_set_motor_enable((val & 0xf0) != 0 ? 1 : 0);
                        fdc.drive = val & 3;
                }
                fdc.dor = val;
                return;
        case 3:
                /* TDR */
                if (fdc.enh_mode != 0)
                {
                        drive = (fdc.dor & 1) ^ fdd_swap;
                        fdc.rate_override[drive] = (val & 0x30) >> 4;
                }
                return;
        case 4:
                if ((val & 0x80) != 0)
                {
                        timer_set_delay_u64(fdc.timer, 8 * TIMER_USEC);
                        exec_state = EXEC_RESET;
                        fdc_reset();
                }
                return;
        case 5: /*Command register*/
                if ((fdc.msr & ~MSR_DRIVE_MASK) == (MSR_RQM | MSR_NDM | MSR_CB))
                {
                        if (fdc.pcjr != 0 || fdc.fifo_enabled == 0)
                        {
                                fdc.data_reg = val;
                                fdc.msr &= unchecked((uint8_t)~MSR_RQM);
                        }
                        else
                        {
                                fdc_fifo_buf_write(val);
                                if (fdc.fifo_pos == 0)
                                        fdc.msr &= unchecked((uint8_t)~MSR_RQM);
                        }
                        break;
                }
                if (fdc.param_pos == fdc.param_total)
                {
                        fdc.terminal_count = 0;
                        fdc.data_ready = 0;

                        fdc.command = val;
                        switch (fdc.command)
                        {
                        case 1: /*Mode*/
                                if (fdc.is_nsc == 0)
                                        goto default;
                                fdc.param_pos = 0;
                                fdc.param_total = 4;
                                fdc.msr = MSR_RQM | MSR_CB;
                                fdc.pos = 0;
                                fdc.format_state = 0;
                                break;

                        case 0x02:
                        case 0x42: /*Read track*/
                                fdc.param_pos = 0;
                                fdc.param_total = 8;
                                fdc.msr = MSR_RQM | MSR_CB;
                                fdc.pos = 0;
                                break;
                        case 0x03: /*Specify*/
                                fdc.param_pos = 0;
                                fdc.param_total = 2;
                                fdc.msr = MSR_RQM | MSR_CB;
                                break;
                        case 0x04: /*Sense drive status*/
                                fdc.param_pos = 0;
                                fdc.param_total = 1;
                                fdc.msr = MSR_RQM | MSR_CB;
                                break;
                        case 0x05:
                        case 0x45:
                        case 0x85:
                        case 0xc5: /*Write data*/
                                fdc.param_pos = 0;
                                fdc.param_total = 8;
                                fdc.msr = MSR_RQM | MSR_CB;
                                fdc.pos = 0;
                                break;
                        case 0x06:
                        case 0x26:
                        case 0x46:
                        case 0x66: /*Read data*/
                        case 0x86:
                        case 0xa6:
                        case 0xc6:
                        case 0xe6:
                                fdc.param_pos = 0;
                                fdc.param_total = 8;
                                fdc.msr = MSR_RQM | MSR_CB;
                                fdc.pos = 0;
                                break;
                        case 0x07: /*Recalibrate*/
                                fdc.param_pos = 0;
                                fdc.param_total = 1;
                                fdc.msr = MSR_RQM | MSR_CB;
                                break;
                        case 0x08: /*Sense interrupt status*/
                                if (fdc.int_pending != 0 || reset_sense_togo != 0)
                                {
                                        fdc.lastdrive = fdc.drive;
                                        exec_state = 8;
                                        fdc.pos = 0;
                                        fdc_callback(null);
                                }
                                else
                                {
                                        fdc.msr = MSR_CB;
                                        exec_state = EXEC_INVALID;
                                        timer_set_delay_u64(fdc.timer, 100 * TIMER_USEC);
                                }
                                break;
                        case 0x0a:
                        case 0x4a: /*Read sector ID*/
                                fdc.param_pos = 0;
                                fdc.param_total = 1;
                                fdc.msr = MSR_RQM | MSR_CB;
                                fdc.pos = 0;
                                break;
                        case 0x0d:
                        case 0x4d:
                        case 0x8d:
                        case 0xcd: /*Format track*/
                                fdc.param_pos = 0;
                                fdc.param_total = 5;
                                fdc.msr = MSR_RQM | MSR_CB;
                                fdc.pos = 0;
                                fdc.format_state = 0;
                                break;
                        case 0x0f: /*Seek*/
                                fdc.param_pos = 0;
                                fdc.param_total = 2;
                                fdc.msr = MSR_RQM | MSR_CB;
                                break;
                        case 0x0e: /*Dump registers*/
                                fdc.lastdrive = fdc.drive;
                                exec_state = 0x0e;
                                fdc.pos = 0;
                                fdc_callback(null);
                                break;
                        case 0x10: /*Get version*/
                                fdc.lastdrive = fdc.drive;
                                exec_state = 0x10;
                                fdc.pos = 0;
                                fdc_callback(null);
                                break;
                        case 0x12: /*Set perpendicular mode*/
                                fdc.param_pos = 0;
                                fdc.param_total = 1;
                                fdc.msr = MSR_RQM | MSR_CB;
                                fdc.pos = 0;
                                break;
                        case 0x13: /*Configure*/
                                fdc.param_pos = 0;
                                fdc.param_total = 3;
                                fdc.msr = MSR_RQM | MSR_CB;
                                fdc.pos = 0;
                                break;
                        case 0x14: /*Unlock*/
                        case 0x94: /*Lock*/
                                fdc.lastdrive = fdc.drive;
                                exec_state = fdc.command;
                                fdc.pos = 0;
                                fdc_callback(null);
                                break;

                        case 0x18:
                                if (fdc.is_nsc == 0)
                                        goto default;
                                fdc.lastdrive = fdc.drive;
                                exec_state = 0x10;
                                fdc.pos = 0;
                                fdc_callback(null);
                                break;

                        default:
                        // bad_command: — `goto bad_command` s'écrit `goto default`, l'étiquette est la même.
                                fdc.msr = MSR_CB;
                                exec_state = EXEC_INVALID;
                                timer_set_delay_u64(fdc.timer, 100 * TIMER_USEC);
                                break;
                        }
                }
                else
                {
                        fdc.@params[fdc.param_pos++] = val;
                        if (fdc.param_pos == fdc.param_total)
                        {
                                uint64_t time;

                                fdc.msr = MSR_NDM | MSR_CB;
                                timer_set_delay_u64(fdc.timer, 256 * TIMER_USEC);
                                disc_drivesel = fdc.drive & 1;
                                reset_sense_togo = 0;
                                disc_set_drivesel(fdc.drive & 1);
                                exec_state = fdc.command & 0x1f;
                                switch (fdc.command)
                                {
                                case 0x02:
                                case 0x42: /*Read track*/
                                        fdc_rate(fdc.drive);
                                        fdc.head = fdc.@params[2];
                                        fdc.sector = fdc.@params[3];
                                        fdc.sector_size = fdc.@params[4];
                                        fdc.end_of_track[fdc.drive] = fdc.@params[5];
                                        if ((fdc.config & 0x40) != 0)
                                                fdd_seek(fdc.drive, fdc.@params[1] - fdc.track[fdc.drive]);
                                        fdc.track[fdc.drive] = fdc.@params[1];
                                        disc_readsector(fdc.drive, SECTOR_FIRST, fdc.track[fdc.drive], fdc.head, fdc.rate,
                                                        fdc.@params[4]);
                                        timer_disable(fdc.timer);
                                        readflash_set(READFLASH_FDC, fdc.drive);
                                        fdc.reading = 1;
                                        break;

                                case 0x03: /*Specify*/
                                        fdc.msr = MSR_RQM;
                                        fdc.specify[0] = fdc.@params[0];
                                        fdc.specify[1] = fdc.@params[1];
                                        fdc.dma = (fdc.specify[1] & 1) ^ 1;
                                        timer_disable(fdc.timer);
                                        break;

                                case 0x05:
                                case 0x45:
                                case 0x85:
                                case 0xc5: /*Write data*/
                                        fdc_rate(fdc.drive);
                                        fdc.head = fdc.@params[2];
                                        fdc.sector = fdc.@params[3];
                                        fdc.sector_size = fdc.@params[4];
                                        fdc.end_of_track[fdc.drive] = fdc.@params[5];
                                        fdc.rw_track = fdc.@params[1];
                                        if ((fdc.config & 0x40) != 0)
                                        {
                                                fdd_seek(fdc.drive, fdc.rw_track - fdc.track[fdc.drive]);
                                                fdc.track[fdc.drive] = fdc.rw_track;
                                        }

                                        disc_writesector(fdc.drive, fdc.sector, fdc.rw_track, fdc.head, fdc.rate, fdc.@params[4]);
                                        timer_disable(fdc.timer);
                                        fdc.byte_written = 0;
                                        readflash_set(READFLASH_FDC, fdc.drive);
                                        fdc.pos = 0;
                                        if (fdc.pcjr != 0)
                                                fdc.msr = MSR_RQM | MSR_NDM | MSR_CB;
                                        break;

                                case 0x06:
                                case 0x26:
                                case 0x46:
                                case 0x66: /*Read data*/
                                case 0x86:
                                case 0xa6:
                                case 0xc6:
                                case 0xe6:
                                        fdc_rate(fdc.drive);
                                        fdc.head = fdc.@params[2];
                                        fdc.sector = fdc.@params[3];
                                        fdc.sector_size = fdc.@params[4];
                                        fdc.end_of_track[fdc.drive] = fdc.@params[5];
                                        fdc.rw_track = fdc.@params[1];
                                        if ((fdc.config & 0x40) != 0)
                                        {
                                                fdd_seek(fdc.drive, fdc.rw_track - fdc.track[fdc.drive]);
                                                fdc.track[fdc.drive] = fdc.rw_track;
                                        }

                                        disc_readsector(fdc.drive, fdc.sector, fdc.rw_track, fdc.head, fdc.rate, fdc.@params[4]);
                                        timer_disable(fdc.timer);
                                        readflash_set(READFLASH_FDC, fdc.drive);
                                        fdc.reading = 1;
                                        break;

                                case 0x07: /*Recalibrate*/
                                        fdc.msr = (uint8_t)(1 << fdc.drive);
                                        timer_disable(fdc.timer);
                                        time = fdd_seek(fdc.drive, SEEK_RECALIBRATE);
                                        if (time != 0)
                                                timer_set_delay_u64(fdc.timer, time);
                                        break;

                                case 0x0d:
                                case 0x4d:
                                case 0x8d:
                                case 0xcd: /*Format*/
                                        fdc_rate(fdc.drive);
                                        fdc.head = (fdc.@params[0] & 4) != 0 ? 1 : 0;
                                        fdc.format_state = 1;
                                        fdc.pos = 0;
                                        fdc.msr = MSR_NDM | MSR_CB;
                                        break;

                                case 0x0f: /*Seek*/
                                        fdc.msr = (uint8_t)(1 << fdc.drive);
                                        fdc.head = 0;
                                        timer_disable(fdc.timer);
                                        time = fdd_seek(fdc.drive, fdc.@params[1] - fdc.track[fdc.drive]);
                                        if (time != 0)
                                                timer_set_delay_u64(fdc.timer, time);
                                        break;

                                case 0x0a:
                                case 0x4a: /*Read sector ID*/
                                        fdc_rate(fdc.drive);
                                        timer_disable(fdc.timer);
                                        fdc.head = (fdc.@params[0] & 4) != 0 ? 1 : 0;
                                        disc_readaddress(fdc.drive, fdc.track[fdc.drive], fdc.head, fdc.rate);
                                        break;

                                case 0x94: /*Unlock*/
                                        exec_state = 0x94;
                                        break;
                                }
                        }
                }
                return;
        case 7:
                if (AT == 0 && romset != ROM_XI8088 && romset != ROM_PC5086)
                        return;
                fdc.rate = val & 3;

                disc_3f7 = val;
                return;
        }
    }

    // pcem: fdc.c:646
    internal static int result_togo = 0;
    // pcem: fdc.c:647-745
    internal static uint8_t fdc_read(uint16_t addr, object priv)
    {
        uint8_t temp;
        int drive;

        cycles -= ISA_CYCLES(8);

        switch (addr & 7)
        {
        case 1: /*???*/
                drive = (fdc.dor & 1) ^ fdd_swap;
                if (fdc.enable_3f1 == 0)
                        return 0xff;
                temp = 0x70;
                if (drive != 0)
                        temp &= unchecked((uint8_t)~0x40);
                else
                        temp &= unchecked((uint8_t)~0x20);

                if ((fdc.dor & 0x10) != 0)
                        temp |= 1;
                if ((fdc.dor & 0x20) != 0)
                        temp |= 2;
                break;
        case 3:
                drive = (fdc.dor & 1) ^ fdd_swap;
                if (fdc.ps1 != 0)
                {
                        /*PS/1 Model 2121 seems return drive type in port 0x3f3,
                          despite the 82077AA FDC not implementing this. This is
                          presumably implemented outside the FDC on one of the
                          motherboard's support chips.*/
                        if (fdd_is_525(drive) != 0)
                                temp = 0x20;
                        else if (fdd_is_ed(drive) != 0)
                                temp = 0x10;
                        else
                                temp = 0x00;
                }
                else if (fdc.enh_mode == 0)
                        temp = 0x20;
                else
                {
                        temp = (uint8_t)(fdc.rate_override[drive] << 4);
                }
                break;
        case 4: /*Status*/
                temp = fdc.msr;
                break;
        case 5: /*Data*/
                fdc.msr &= unchecked((uint8_t)~MSR_RQM);
                if ((fdc.msr & ~MSR_DRIVE_MASK) == (MSR_RQM | MSR_DIO | MSR_NDM | MSR_CB))
                {
                        if (fdc.pcjr != 0 || fdc.fifo_enabled == 0)
                                temp = fdc.data_reg;
                        else
                        {
                                temp = (uint8_t)fdc_fifo_buf_read();
                        }
                        break;
                }
                if (result_togo != 0)
                {
                        result_togo--;
                        temp = fdc.result[RES_N - result_togo];
                        if (result_togo == 0)
                        {
                                fdc.msr = MSR_RQM;
                        }
                        else
                        {
                                fdc.msr |= MSR_RQM | MSR_DIO;
                        }
                }
                else
                {
                        if (lastbyte != 0)
                                fdc.msr = MSR_RQM;
                        lastbyte = 0;
                        temp = fdc.data_reg;
                        fdc.data_ready = 0;
                }
                fdc.msr &= unchecked((uint8_t)~MSR_DRIVE_MASK);
                break;
        case 7: /*Disk change*/
                drive = (fdc.dor & 1) ^ fdd_swap;
                if ((fdc.dor & (0x10 << drive)) != 0)
                        temp = (uint8_t)((disc_changed[drive] != 0 || drive_empty[drive] != 0) ? 0x80 : 0);
                else
                        temp = 0;
                if (fdc.dskchg_activelow != 0) /*PC2086/3086 seem to reverse this bit*/
                        temp ^= 0x80;
                temp |= 1;
                break;
        default:
                temp = 0xFF;
                break;
        }
        return temp;
    }

    // pcem: fdc.c:747-1034 — `(void *)fdc_callback` dans timer_add : la fonction sans
    // paramètre prend le `void *p` du delegate, les appels directs passent null.
    internal static void fdc_callback(object? p)
    {
        int temp;
        int drive;

        switch (exec_state)
        {
        case EXEC_END_INT: /*End of command with interrupt*/
                fdc_int();
                goto case EXEC_END;
        case EXEC_END: /*End of command*/
                fdc.msr = (uint8_t)((fdc.msr & MSR_DRIVE_MASK) | MSR_RQM);
                return;
        case EXEC_RESET: /*Reset*/
                fdc_int();
                reset_sense_togo = 4;
                return;
        case 1: /*Mode*/
                fdc.msr = MSR_RQM;
                fdc.densel_force = (fdc.@params[2] & 0xC0) >> 6;
                return;

        case 2: /*Read track*/
                readflash_set(READFLASH_FDC, fdc.drive);
                fdc.end_of_track[fdc.drive]--;
                if (fdc.end_of_track[fdc.drive] == 0 || fdc.terminal_count != 0)
                {
                        fdc.reading = 0;
                        exec_state = EXEC_END;
                        fdc_int();
                        fdc.msr = MSR_RQM | MSR_DIO | MSR_CB;
                        fdc.result[RES_ST0] = (uint8_t)((fdc.head != 0 ? ST0_HD : 0) | fdc.drive);
                        fdc.result[RES_ST1] = fdc.result[RES_ST2] = 0;
                        fdc.result[RES_C] = (uint8_t)fdc.track[fdc.drive];
                        fdc.result[RES_H] = (uint8_t)fdc.head;
                        fdc.result[RES_R] = (uint8_t)fdc.sector;
                        fdc.result[RES_N] = fdc.@params[4];
                        result_togo = 7;
                        return;
                }
                else
                        disc_readsector(fdc.drive, SECTOR_NEXT, fdc.track[fdc.drive], fdc.head, fdc.rate, fdc.@params[4]);
                fdc.reading = 1;
                return;
        case 4: /*Sense drive status*/
                drive = fdc.@params[0] & 1;
                if (fdd_get_type(drive) != 0)
                {
                        fdc.result[10] = (uint8_t)((fdc.@params[0] & 7) | ST3_RDY | ST3_TS);
                        if (fdd_track0(drive) != 0)
                                fdc.result[10] |= ST3_T0;
                        if (writeprot[drive] != 0)
                                fdc.result[10] |= ST3_WP;
                }
                else
                {
                        fdc.result[10] = (uint8_t)(ST3_FT | (fdc.@params[0] & 3));
                }

                fdc.msr = (uint8_t)((fdc.msr & MSR_DRIVE_MASK) | MSR_RQM | MSR_DIO | MSR_CB);
                result_togo = 1;
                exec_state = 0;
                return;
        case 5: /*Write data*/
                readflash_set(READFLASH_FDC, fdc.drive);
                if (fdc.sector == fdc.@params[5])
                {
                        fdc.sector = 1;
                        if ((fdc.command & 0x80) != 0)
                        {
                                fdc.head ^= 1;
                                if (fdc.head == 0)
                                {
                                        fdc.rw_track++;
                                        fdc.terminal_count = 1;
                                }
                        }
                        else
                        {
                                fdc.rw_track++;
                                fdc.terminal_count = 1;
                        }
                }
                else
                        fdc.sector++;
                if (fdc.terminal_count != 0)
                {
                        exec_state = EXEC_END;
                        fdc_int();
                        fdc.msr = MSR_RQM | MSR_DIO | MSR_CB;
                        fdc.result[RES_ST0] = (uint8_t)((fdc.head != 0 ? ST0_HD : 0) | fdc.drive);
                        fdc.result[RES_ST1] = fdc.result[RES_ST2] = 0;
                        fdc.result[RES_C] = (uint8_t)fdc.rw_track;
                        fdc.result[RES_H] = (uint8_t)fdc.head;
                        fdc.result[RES_R] = (uint8_t)fdc.sector;
                        fdc.result[RES_N] = fdc.@params[4];
                        result_togo = 7;
                        return;
                }
                disc_writesector(fdc.drive, fdc.sector, fdc.rw_track, fdc.head, fdc.rate, fdc.@params[4]);
                return;
        case 6: /*Read data*/
                readflash_set(READFLASH_FDC, fdc.drive);
                if (fdc.sector == fdc.@params[5])
                {
                        fdc.sector = 1;
                        if ((fdc.command & 0x80) != 0)
                        {
                                fdc.head ^= 1;
                                if (fdc.head == 0)
                                {
                                        fdc.rw_track++;
                                        fdc.terminal_count = 1;
                                }
                        }
                        else
                        {
                                fdc.rw_track++;
                                fdc.terminal_count = 1;
                        }
                }
                else
                        fdc.sector++;
                if (fdc.terminal_count != 0)
                {
                        fdc.reading = 0;
                        exec_state = EXEC_END;
                        fdc_int();
                        fdc.msr = MSR_RQM | MSR_DIO | MSR_CB;
                        fdc.result[RES_ST0] = (uint8_t)((fdc.head != 0 ? ST0_HD : 0) | fdc.drive);
                        fdc.result[RES_ST1] = fdc.result[RES_ST2] = 0;
                        fdc.result[RES_C] = (uint8_t)fdc.rw_track;
                        fdc.result[RES_H] = (uint8_t)fdc.head;
                        fdc.result[RES_R] = (uint8_t)fdc.sector;
                        fdc.result[RES_N] = fdc.@params[4];
                        result_togo = 7;
                        return;
                }
                disc_readsector(fdc.drive, fdc.sector, fdc.rw_track, fdc.head, fdc.rate, fdc.@params[4]);
                fdc.reading = 1;
                return;

        case 7: /*Recalibrate*/
                drive = fdc.@params[0] & 1;
                fdc.track[fdc.drive] = 0;
                if (fdc.drive <= 1 && fdd_get_type(drive) != 0)
                        fdc.st0 = (uint8_t)(ST0_SE | (fdc.@params[0] & 3) | (fdc.head != 0 ? ST0_HD : 0));
                else
                        fdc.st0 = (uint8_t)(ST0_IC_ABNORMAL | ST0_SE | ST0_NR | (fdc.@params[0] & 3) | (fdc.head != 0 ? ST0_HD : 0));
                fdc.int_pending = 1;
                exec_state = EXEC_END_INT;
                timer_set_delay_u64(fdc.timer, 2048 * TIMER_USEC);
                fdc.msr = (uint8_t)(MSR_RQM | (1 << fdc.drive));
                return;

        case 8: /*Sense interrupt status*/
                fdc.msr = (uint8_t)((fdc.msr & MSR_DRIVE_MASK) | MSR_RQM | MSR_DIO | MSR_CB);
                if (reset_sense_togo != 0)
                        fdc.result[9] = (uint8_t)(ST0_IC_INVALID | ST0_IC_ABNORMAL | (4 - reset_sense_togo) | (fdc.head != 0 ? ST0_HD : 0));
                else
                        fdc.result[9] = fdc.st0;
                fdc.result[10] = (uint8_t)fdc.track[fdc.drive];
                if (reset_sense_togo != 0)
                        reset_sense_togo--;
                if (reset_sense_togo == 0)
                        fdc.int_pending = 0;

                result_togo = 2;
                exec_state = 0;
                return;

        case 0x0d: /*Format track*/
                if (fdc.format_state == 1)
                {
                        fdc.format_state = 2;
                        timer_set_delay_u64(fdc.timer, 8 * TIMER_USEC);
                }
                else if (fdc.format_state == 2)
                {
                        temp = fdc_getdata(fdc.pos == ((fdc.@params[2] * 4) - 1) ? 1 : 0);
                        if (temp == -1)
                        {
                                timer_set_delay_u64(fdc.timer, 8 * TIMER_USEC);
                                return;
                        }
                        fdc.format_dat[fdc.pos++] = (uint8_t)temp;
                        if (fdc.pos == (fdc.@params[2] * 4))
                                fdc.format_state = 3;
                        timer_set_delay_u64(fdc.timer, 8 * TIMER_USEC);
                }
                else if (fdc.format_state == 3)
                {
                        disc_format(fdc.drive, fdc.track[fdc.drive], fdc.head, fdc.rate, fdc.@params[4]);
                        fdc.format_state = 4;
                }
                else
                {
                        exec_state = EXEC_END;
                        fdc_int();
                        fdc.msr = MSR_RQM | MSR_DIO | MSR_CB;
                        fdc.result[RES_ST0] = (uint8_t)((fdc.head != 0 ? ST0_HD : 0) | fdc.drive);
                        fdc.result[RES_ST1] = fdc.result[RES_ST2] = 0;
                        fdc.result[RES_C] = (uint8_t)fdc.track[fdc.drive];
                        fdc.result[RES_H] = (uint8_t)fdc.head;
                        fdc.result[RES_R] = (uint8_t)(fdc.format_dat[fdc.pos - 2] + 1);
                        fdc.result[RES_N] = fdc.@params[4];
                        result_togo = 7;
                        fdc.format_state = 0;
                        return;
                }
                return;

        case 15: /*Seek*/
                drive = fdc.@params[0] & 1;
                fdc.track[fdc.drive] = fdc.@params[1];
                if (fdc.drive <= 1 && fdd_get_type(drive) != 0)
                        fdc.st0 = (uint8_t)(ST0_SE | (fdc.@params[0] & 3) | (fdc.head != 0 ? ST0_HD : 0));
                else
                        fdc.st0 = (uint8_t)(ST0_IC_ABNORMAL | ST0_SE | ST0_NR | (fdc.@params[0] & 3) | (fdc.head != 0 ? ST0_HD : 0));
                fdc.int_pending = 1;
                exec_state = EXEC_END_INT;
                timer_set_delay_u64(fdc.timer, 1024 * TIMER_USEC);
                fdc.msr = (uint8_t)(MSR_RQM | (1 << fdc.drive));
                return;
        case 0x0e: /*Dump registers*/
                fdc.msr = (uint8_t)((fdc.msr & MSR_DRIVE_MASK) | MSR_RQM | MSR_DIO | MSR_CB);
                fdc.result[1] = (uint8_t)fdc.track[0];
                fdc.result[2] = (uint8_t)fdc.track[1];
                fdc.result[3] = 0;
                fdc.result[4] = 0;
                fdc.result[5] = fdc.specify[0];
                fdc.result[6] = fdc.specify[1];
                fdc.result[7] = (uint8_t)fdc.end_of_track[fdc.drive];
                fdc.result[8] = (uint8_t)((fdc.perp_mode & 0x7f) | ((fdc.@lock != 0) ? 0x80 : 0));
                fdc.result[9] = fdc.config;
                fdc.result[10] = fdc.precomp_track;
                result_togo = 10;
                exec_state = 0;
                return;

        case 0x10: /*Version*/
                fdc.msr = (uint8_t)((fdc.msr & MSR_DRIVE_MASK) | MSR_RQM | MSR_DIO | MSR_CB);
                fdc.result[10] = 0x90;
                result_togo = 1;
                exec_state = 0;
                return;

        case 0x12:
                fdc.perp_mode = fdc.@params[0];
                fdc.msr = MSR_RQM;
                return;
        case 0x13: /*Configure*/
                fdc.config = fdc.@params[1];
                fdc.precomp_track = fdc.@params[2];
                fdc.fifo_enabled = (fdc.@params[1] & 0x20) != 0 ? 0 : 1;
                fdc.fifo_threshold = (fdc.@params[1] & 0xF) + 1;
                // omitted: pclog("FIFO is now %02X, threshold is %02X\n", ...) — sortie pure
                fdc.msr = MSR_RQM;
                return;
        case 0x14: /*Unlock*/
                fdc.@lock = 0;
                fdc.msr = (uint8_t)((fdc.msr & MSR_DRIVE_MASK) | MSR_RQM | MSR_DIO | MSR_CB);
                fdc.result[10] = 0;
                result_togo = 1;
                exec_state = 0;
                return;
        case 0x94: /*Lock*/
                fdc.@lock = 1;
                fdc.msr = (uint8_t)((fdc.msr & MSR_DRIVE_MASK) | MSR_RQM | MSR_DIO | MSR_CB);
                fdc.result[10] = 0x10;
                result_togo = 1;
                exec_state = 0;
                return;

        case 0x18: /*NSC*/
                fdc.msr = (uint8_t)((fdc.msr & MSR_DRIVE_MASK) | MSR_RQM | MSR_DIO | MSR_CB);
                fdc.result[10] = 0x73;
                result_togo = 1;
                exec_state = 0;
                return;

        case EXEC_INVALID: /*Invalid*/
                fdc.data_reg = fdc.st0 = ST0_IC_INVALID;
                fdc.msr = (uint8_t)((fdc.msr & MSR_DRIVE_MASK) | MSR_RQM | MSR_DIO | MSR_CB);
                fdc.result[10] = fdc.st0;
                result_togo = 1;
                exec_state = 0;
                return;
        }
    }

    // pcem: fdc.c:1036-1050
    internal static void fdc_overrun()
    {
        disc_sector_stop();
        timer_disable(fdc.timer);

        fdc_int();
        fdc.msr = MSR_RQM | MSR_DIO | MSR_CB;
        fdc.result[RES_ST0] = (uint8_t)(ST0_IC_ABNORMAL | (fdc.head != 0 ? ST0_HD : 0) | fdc.drive);
        fdc.result[RES_ST1] = ST1_OR;
        fdc.result[RES_ST2] = 0;
        fdc.result[RES_C] = 0;
        fdc.result[RES_H] = 0;
        fdc.result[RES_R] = 0;
        fdc.result[RES_N] = 0;
        result_togo = 7;
    }

    // pcem: fdc.c:1052-1094
    internal static int fdc_data(uint8_t data)
    {
        if (fdc.terminal_count != 0)
                return 0;

        if (fdc.pcjr != 0 || fdc.dma == 0)
        {
                if (fdc.data_ready != 0)
                {
                        fdc_overrun();
                        return -1;
                }

                if (fdc.pcjr != 0 || fdc.fifo_enabled == 0)
                {
                        fdc.data_reg = data;
                        fdc.data_ready = 1;
                        fdc.msr = MSR_RQM | MSR_DIO | MSR_NDM | MSR_CB;
                }
                else
                {
                        // FIFO enabled
                        fdc_fifo_buf_write(data);
                        if (fdc.fifo_pos == 0)
                        {
                                // We have wrapped around, means FIFO is over
                                fdc.data_ready = 1;
                                fdc.msr = MSR_RQM | MSR_DIO | MSR_NDM | MSR_CB;
                        }
                }
        }
        else
        {
                if ((dma_channel_write(2, data) & DMA_OVER) != 0)
                        fdc.terminal_count = 1;

                if (fdc.fifo_enabled == 0)
                {
                        fdc.data_ready = 1;
                        fdc.msr = MSR_RQM | MSR_DIO | MSR_CB;
                }
                else
                {
                        fdc_fifo_buf_dummy();
                        if (fdc.fifo_pos == 0)
                        {
                                // We have wrapped around, means FIFO is over
                                fdc.data_ready = 1;
                                fdc.msr = MSR_RQM | MSR_DIO | MSR_CB;
                        }
                }
        }

        return 0;
    }

    // pcem: fdc.c:1096-1100
    internal static void fdc_finishread()
    {
        fdc.reading = 0;
        timer_set_delay_u64(fdc.timer, 200 * TIMER_USEC);
    }

    // pcem: fdc.c:1102-1133
    internal static void fdc_notfound(int reason)
    {
        timer_disable(fdc.timer);

        fdc_int();
        fdc.msr = MSR_RQM | MSR_DIO | MSR_CB;
        fdc.result[RES_ST0] = (uint8_t)(ST0_IC_ABNORMAL | (fdc.head != 0 ? ST0_HD : 0) | fdc.drive);
        switch (reason)
        {
        case FDC_STATUS_AM_NOT_FOUND:
                fdc.result[RES_ST1] = ST1_ND | ST1_MA;
                fdc.result[RES_ST2] = 0;
                break;
        case FDC_STATUS_NOT_FOUND:
                fdc.result[RES_ST1] = ST1_ND;
                fdc.result[RES_ST2] = 0;
                break;
        case FDC_STATUS_WRONG_CYLINDER:
                fdc.result[RES_ST1] = ST1_ND;
                fdc.result[RES_ST2] = ST2_WC;
                break;
        case FDC_STATUS_BAD_CYLINDER:
                fdc.result[RES_ST1] = ST1_ND;
                fdc.result[RES_ST2] = ST2_WC | ST2_BC;
                break;
        }
        fdc.result[RES_C] = (uint8_t)fdc.rw_track;
        fdc.result[RES_H] = (uint8_t)fdc.head;
        fdc.result[RES_R] = (uint8_t)fdc.sector;
        fdc.result[RES_N] = (uint8_t)fdc.sector_size;
        result_togo = 7;
    }

    // pcem: fdc.c:1135-1149
    internal static void fdc_datacrcerror()
    {
        timer_disable(fdc.timer);

        fdc_int();
        fdc.msr = MSR_RQM | MSR_DIO | MSR_CB;
        fdc.result[RES_ST0] = (uint8_t)(ST0_IC_ABNORMAL | (fdc.head != 0 ? ST0_HD : 0) | fdc.drive);
        fdc.result[RES_ST1] = ST1_DE;
        fdc.result[RES_ST2] = ST2_DD;
        fdc.result[RES_C] = (uint8_t)fdc.rw_track;
        fdc.result[RES_H] = (uint8_t)fdc.head;
        fdc.result[RES_R] = (uint8_t)fdc.sector;
        fdc.result[RES_N] = (uint8_t)fdc.sector_size;
        result_togo = 7;
    }

    // pcem: fdc.c:1151-1165
    internal static void fdc_headercrcerror()
    {
        timer_disable(fdc.timer);

        fdc_int();
        fdc.msr = MSR_RQM | MSR_DIO | MSR_CB;
        fdc.result[RES_ST0] = (uint8_t)(ST0_IC_ABNORMAL | (fdc.head != 0 ? ST0_HD : 0) | fdc.drive);
        fdc.result[RES_ST1] = ST1_DE;
        fdc.result[RES_ST2] = 0;
        fdc.result[RES_C] = (uint8_t)fdc.rw_track;
        fdc.result[RES_H] = (uint8_t)fdc.head;
        fdc.result[RES_R] = (uint8_t)fdc.sector;
        fdc.result[RES_N] = (uint8_t)fdc.sector_size;
        result_togo = 7;
    }

    // pcem: fdc.c:1167-1180
    internal static void fdc_writeprotect()
    {
        timer_disable(fdc.timer);

        fdc_int();
        fdc.msr = MSR_RQM | MSR_DIO | MSR_CB;
        fdc.result[RES_ST0] = (uint8_t)(ST0_IC_ABNORMAL | (fdc.head != 0 ? ST0_HD : 0) | fdc.drive);
        fdc.result[RES_ST1] = ST1_NW;
        fdc.result[RES_ST2] = 0;
        fdc.result[RES_C] = (uint8_t)fdc.rw_track;
        fdc.result[RES_H] = (uint8_t)fdc.head;
        fdc.result[RES_R] = (uint8_t)fdc.sector;
        fdc.result[RES_N] = (uint8_t)fdc.sector_size;
        result_togo = 7;
    }

    // pcem: fdc.c:1182-1221
    internal static int fdc_getdata(int last)
    {
        int data;

        if (fdc.pcjr != 0 || fdc.dma == 0)
        {
                // pcem bug, reproduced: PB-19 — written n'est jamais posé à 1 : ce test
                // ne peut pas se déclencher, l'écrasement en écriture n'est pas détecté.
                if (fdc.byte_written != 0)
                {
                        fdc_overrun();
                        return -1;
                }
                if (fdc.pcjr != 0 || fdc.fifo_enabled == 0)
                {
                        data = fdc.data_reg;

                        if (last == 0)
                                fdc.msr = MSR_RQM | MSR_NDM | MSR_CB;
                }
                else
                {
                        data = fdc_fifo_buf_read();

                        if (last == 0 && (fdc.fifo_pos == 0))
                                fdc.msr = MSR_RQM | MSR_NDM | MSR_CB;
                }
        }
        else
        {
                data = dma_channel_read(2);

                if (fdc.fifo_enabled == 0)
                {
                        if (last == 0)
                                fdc.msr = MSR_RQM | MSR_CB;
                }
                else
                {
                        fdc_fifo_buf_dummy();

                        if (last == 0 && (fdc.fifo_pos == 0))
                                fdc.msr = MSR_RQM | MSR_CB;
                }

                if ((data & DMA_OVER) != 0)
                        fdc.terminal_count = 1;
        }

        fdc.byte_written = 0;
        return data & 0xff;
    }

    // pcem: fdc.c:1223-1235
    internal static void fdc_sectorid(uint8_t track, uint8_t side, uint8_t sector, uint8_t size, uint8_t crc1, uint8_t crc2)
    {
        fdc_int();
        fdc.msr = MSR_RQM | MSR_DIO | MSR_CB;
        fdc.result[RES_ST0] = (uint8_t)((fdc.head != 0 ? ST0_HD : 0) | fdc.drive);
        fdc.result[RES_ST1] = 0;
        fdc.result[RES_ST2] = 0;
        fdc.result[RES_C] = track;
        fdc.result[RES_H] = side;
        fdc.result[RES_R] = sector;
        fdc.result[RES_N] = size;
        result_togo = 7;
    }

    // pcem: fdc.c:1237-1240
    internal static void fdc_indexpulse()
    {
    }

    // pcem: fdc.c:1242-1254
    internal static void fdc_init()
    {
        timer_add(fdc.timer, fdc_callback, null, 0);
        fdc.dskchg_activelow = 0;
        fdc.enable_3f1 = 1;

        fdc_update_enh_mode(0);
        fdc_update_densel_polarity(1);
        fdc_update_rwc(0, 0);
        fdc_update_rwc(1, 0);
        fdc_update_densel_force(0);

        fdc.fifo_enabled = fdc.fifo_threshold = 0;
    }

    // pcem: fdc.c:1256-1261
    internal static void fdc_add()
    {
        io_sethandler(0x03f0, 0x0006, fdc_read, null, null, fdc_write, null, null, null);
        io_sethandler(0x03f7, 0x0001, fdc_read, null, null, fdc_write, null, null, null);
        fdc.pcjr = 0;
        fdc.ps1 = 0;
    }

    // pcem: fdc.c:1263-1268
    internal static void fdc_add_pcjr()
    {
        io_sethandler(0x00f0, 0x0006, fdc_read, null, null, fdc_write, null, null, null);
        timer_add(fdc.watchdog_timer, fdc_watchdog_poll, fdc, 0);
        fdc.pcjr = 1;
        fdc.ps1 = 0;
    }

    // pcem: fdc.c:1270-1273
    internal static void fdc_remove()
    {
        io_removehandler(0x03f0, 0x0006, fdc_read, null, null, fdc_write, null, null, null);
        io_removehandler(0x03f7, 0x0001, fdc_read, null, null, fdc_write, null, null, null);
    }

    // pcem: fdc.c:1275-1278
    internal static void fdc_discchange_clear(int drive)
    {
        if (drive < 2)
                disc_changed[drive] = 0;
    }

    // pcem: fdc.c:1280-1286
    internal static int fdc_discchange_read() { return (disc_changed[fdc.drive] != 0 || drive_empty[fdc.drive] != 0) ? 1 : 0; }

    internal static void fdc_set_dskchg_activelow() { fdc.dskchg_activelow = 1; }

    internal static void fdc_3f1_enable(int enable) { fdc.enable_3f1 = enable; }

    internal static void fdc_set_ps1() { fdc.ps1 = 1; }

    /// <summary>Sonde de diagnostic — pendant exact de h_disc_probe()
    /// (tools/oracle/harness.c) : les globales du sous-système disquette que les
    /// deux côtés exposent, dans le même ordre, pour que le diff d'amorçage nomme
    /// le champ divergent. L'instance `fdc` est static dans fdc.c et hors de
    /// portée de l'oracle : elle n'y figure pas.</summary>
    internal static void Probe(uint64_t[] o)
    {
        o[0] = (uint64_t)(long)exec_state;
        o[1] = disc_3f7;
        o[2] = (uint64_t)(long)lastbyte;
        o[3] = (uint64_t)(long)result_togo;
        o[4] = (uint64_t)(long)bit_rate;
        o[5] = (uint64_t)(long)motoron;
        o[6] = (uint64_t)(long)disc_drivesel;
        o[7] = (uint64_t)(long)curdrive;
        o[8] = (uint64_t)(long)disc_track[0];
        o[9] = (uint64_t)(long)disc_track[1];
        o[10] = (uint64_t)(long)drive_empty[0];
        o[11] = (uint64_t)(long)drive_empty[1];
        o[12] = (uint64_t)(long)disc_changed[0];
        o[13] = (uint64_t)(long)disc_changed[1];
        o[14] = (uint64_t)(long)writeprot[0];
        o[15] = (uint64_t)(long)writeprot[1];
        o[16] = (uint64_t)(long)disc_notfound;
        o[17] = (uint64_t)(long)readflash;
        o[18] = (uint64_t)(long)disc_poll_timer.enabled;
        o[19] = timer_get_remaining_u64(disc_poll_timer);
    }
}
