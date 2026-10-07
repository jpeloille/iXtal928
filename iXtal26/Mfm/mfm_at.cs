// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/mfm/mfm_at.c  (mfm_at.h : includes/private/mfm/mfm_at.h)
// STATUS: transcribed — mfm_at.c en entier.
//
// LE CONTRÔLEUR DE DISQUE DUR D'UN AT, et il ne ressemble pas à celui du XT. Le Fixed
// Disk Adapter du 5160 est une carte avec sa propre ROM d'extension en C800:0000, qui
// porte son INT 13h ; celui-ci n'a pas de ROM du tout — l'INT 13h vient du BIOS de la
// carte mère, et la géométrie du disque vient du CMOS. C'est pourquoi un AT demande de
// déclarer un « type » de disque là où un XT ne demande rien.
//
// Ports 0x1F0-0x1F7 et 0x3F6, IRQ 14 — donc le PIC ESCLAVE, et mfm_irq_update lit
// directement pic2.pend et pic2.ins.
//
// C'EST L'ANCÊTRE DIRECT DE L'IDE : le jeu de registres est celui que l'ATA reprendra
// mot pour mot, d'où les noms `IDE_TIME` et les commentaires « ide » que PCem a laissés
// dans ce fichier. Il n'y a pourtant aucun IDE ici : les commandes s'arrêtent à
// SET_PARAMETERS, il n'y a pas d'IDENTIFY, et le disque n'annonce pas sa géométrie —
// c'est le BIOS qui la lui impose.
//
// IL ADRESSE LES 46 TYPES DE LA TABLE, et c'est son intérêt face au Xebec. Le Xebec
// n'accepte que quatre géométries, codées dans ses interrupteurs (mfm_xebec.cs,
// XebecSwitch) ; ici la géométrie arrive par CMD_SET_PARAMETERS, donc tout ce que le
// BIOS sait déclarer passe. Mesuré dans la ROM AMI : cette commande ne transporte que
// les têtes et les secteurs par piste, jamais le nombre de cylindres, qui n'arrive
// qu'au coup par coup par les registres 0x1F4 et 0x1F5.

// CS8600 : `(mfm_t)p` part du `void *p` que timer.cs déclare `object?`
// (mfm_at.c:388) — même situation que dans mfm_xebec.cs.
// CS8602 : `priv` est le void* que device_priv range et que io.cs rend en `object` ;
// le cast est celui du C (mfm_at.c:155, :297, :311, :354).
#pragma warning disable CS8600, CS8602

using iXtal26.Disc;
using iXtal26.PluginApi;
using static iXtal26.PluginApi.device;
using static iXtal26.Models.pic;
using static iXtal26.io;
using static iXtal26.pc;
using static iXtal26.timer;

namespace iXtal26.Mfm;

// LES DEUX TYPES SONT IMBRIQUÉS, ET C'EST UNE NÉCESSITÉ, PAS UN GOÛT. mfm_at.c et
// mfm_xebec.c déclarent chacun un `typedef struct mfm_drive_t` — deux types distincts
// de même nom, ce que le C autorise parce qu'ils sont locaux à leur unité de
// compilation. mfm_xebec.cs a posé le sien au niveau du namespace ; poser celui-ci à
// côté serait un doublon. L'imbriquer dans la classe de l'unité est le rendu C# le plus
// proche de « local à cette unité », et il garde le NOM du C — le renommer serait une
// déviation visible dans tout le fichier.
internal static class mfm_at
{
    // pcem: mfm_at.c:21 — propriété et non constante : TIMER_USEC reste nul jusqu'à
    // setpitclock() (timer.cs:49-50), donc la multiplication doit se faire à l'usage.
    private static uint64_t IDE_TIME => TIMER_USEC * 10;

    /*Rough estimate - MFM drives spin at 3600 RPM, with 17 sectors per track,
      meaning (3600/60)*17 = 1020 sectors per second, or 980us per sector.

      This is required for OS/2 on slow 286 systems, as the hard drive formatter
      will crash with 'internal processing error' if write sector interrupts are too
      close in time*/
    // pcem: mfm_at.c:29
    private static uint64_t SECTOR_TIME => TIMER_USEC * 980;

    // pcem: mfm_at.c:31-38
    private const uint8_t STAT_ERR = 0x01;
    private const uint8_t STAT_INDEX = 0x02;
    private const uint8_t STAT_CORRECTED_DATA = 0x04;
    private const uint8_t STAT_DRQ = 0x08; /* Data request */
    private const uint8_t STAT_DSC = 0x10;
    private const uint8_t STAT_SEEK_COMPLETE = 0x20;
    private const uint8_t STAT_READY = 0x40;
    private const uint8_t STAT_BUSY = 0x80;

    // pcem: mfm_at.c:40-45
    private const uint8_t ERR_DAM_NOT_FOUND = 0x01; /*Data Address Mark not found*/
    private const uint8_t ERR_TR000 = 0x02;         /*Track 0 not found*/
    private const uint8_t ERR_ABRT = 0x04;          /*Command aborted*/
    private const uint8_t ERR_ID_NOT_FOUND = 0x10;  /*ID not found*/
    private const uint8_t ERR_DATA_CRC = 0x40;      /*Data CRC error*/
    private const uint8_t ERR_BAD_BLOCK = 0x80;     /*Bad Block detected*/

    // pcem: mfm_at.c:47-54
    private const uint8_t CMD_RESTORE = 0x10;
    private const uint8_t CMD_READ = 0x20;
    private const uint8_t CMD_WRITE = 0x30;
    private const uint8_t CMD_VERIFY = 0x40;
    private const uint8_t CMD_FORMAT = 0x50;
    private const uint8_t CMD_SEEK = 0x70;
    private const uint8_t CMD_DIAGNOSE = 0x90;
    private const uint8_t CMD_SET_PARAMETERS = 0x91;

    // pcem: mfm_at.c:58-63 — classe : mfm_at.c:509 prend l'adresse de
    // drives[d].hdd_file et la passe à hdd_load.
    internal sealed class mfm_drive_t
    {
        internal int cfg_spt;
        internal int cfg_hpc;
        internal int current_cylinder;
        internal hdd_file_t hdd_file = new();
    }

    // pcem: mfm_at.c:65-81
    internal sealed class mfm_t
    {
        internal uint8_t status;
        internal uint8_t error;
        internal int secount, sector, cylinder, head, cylprecomp;
        internal uint8_t command;
        internal uint8_t fdisk;
        internal int pos;

        internal int drive_sel;
        internal int reset;

        // LE C DÉCLARE `uint16_t buffer[256]`, ET CE TABLEAU EST RENDU EN OCTETS.
        //
        // Ce n'est pas un choix de confort : le même tampon est indexé par MOTS par
        // mfm_readw/mfm_writew (`buffer[pos >> 1]`) et lu comme un SECTEUR DE 512
        // OCTETS par hdd_read_sectors, qui prend un uint8_t[]. C'est donc le flux
        // d'octets qui est la vérité, et le `uint16_t` n'est qu'une largeur d'accès.
        //
        // L'aliasing du C suppose le petit-boutisme, et le rendu ci-dessous le pose
        // explicitement : buffer[pos] porte l'octet de poids faible du mot. Sur x86 les
        // deux sont bit pour bit identiques ; sur une machine gros-boutiste le C serait
        // faux et ce code, lui, resterait juste — l'écart est nommé ici et nulle part
        // ailleurs.
        internal uint8_t[] buffer = new uint8_t[512];

        internal int irqstat;

        internal pc_timer_t callback_timer = new();

        internal mfm_drive_t[] drives = { new mfm_drive_t(), new mfm_drive_t() };
    }

    // pcem: mfm_at.c:86-92
    private static void mfm_irq_raise(mfm_t mfm)
    {
        // omitted: pclog("IDE_IRQ_RAISE\n") — commenté chez PCem.
        if ((mfm.fdisk & 2) == 0)
                picint(1 << 14);

        mfm.irqstat = 1;
    }

    // pcem: mfm_at.c:94
    private static void mfm_irq_lower(mfm_t mfm) { picintc(1 << 14); }

    // pcem: mfm_at.c:96-99 — LIT LE PIC ESCLAVE DIRECTEMENT. 0x40 est le bit 6 de
    // pic2, soit l'IRQ 14 ; le test veut dire « l'esclave ne porte ni une demande ni un
    // service en cours sur cette ligne ». C'est le seul lecteur de pic2.pend et
    // pic2.ins hors de pic.cs lui-même.
    internal static void mfm_irq_update(mfm_t mfm)
    {
        if (mfm.irqstat != 0 && ((pic2.pend | pic2.ins) & 0x40) == 0 && (mfm.fdisk & 2) == 0)
                picint(1 << 14);
    }

    /*
     * Return the sector offset for the current register values
     */
    // pcem: mfm_at.c:104-133
    //
    // CINQ REFUS, ET CE SONT LES SEULS DIAGNOSTICS D'UNE GÉOMÉTRIE QUI NE CORRESPOND
    // PAS. Les deux premiers comparent aux valeurs que le BIOS a programmées par
    // CMD_SET_PARAMETERS, les deux derniers à celles du fichier image ; entre les deux,
    // un désaccord entre le type déclaré au CMOS et la géométrie du fichier se
    // manifeste ici et pas avant. Le risque est réel : voir PB-34, où la table de types
    // de PCem contredit celle du BIOS sur son entrée 39.
    private static int mfm_get_sector(mfm_t mfm, out int addr)
    {
        mfm_drive_t drive = mfm.drives[mfm.drive_sel];
        int heads = drive.cfg_hpc;
        int sectors = drive.cfg_spt;

        addr = 0;

        // omitted: les cinq pclog de ce bloc — sortie pure. Conditions, dans l'ordre :
        //   « wrong cylinder », « past end of configured heads », « past end of
        //   configured sectors », « past end of heads », « past end of sectors ».
        if (drive.current_cylinder != mfm.cylinder)
                return 1;
        // pcem bug, reproduced: PB-25 — `>` et non `>=` : la tête cfg_hpc vise la tête 0 du cylindre suivant (:113).
        if (mfm.head > heads)
                return 1;
        if (mfm.sector >= sectors + 1)
                return 1;
        // pcem bug, reproduced: PB-25 — le même `>`, contre les têtes de l'image (:121).
        if (mfm.head > drive.hdd_file.hpc)
                return 1;
        if (mfm.sector >= drive.hdd_file.spt + 1)
                return 1;

        // Le C calcule en off_t, donc en 64 bits, là où hdd_read_sectors de ce dépôt
        // prend un int — comme hdd_file.cs l'a posé à M12. Le plus gros type de la
        // table fait 1224 x 15 x 17 = 312 120 secteurs : aucun débordement possible.
        addr = (((mfm.cylinder * heads) + mfm.head) * sectors) + (mfm.sector - 1);

        return 0;
    }

    /**
     * Move to the next sector using CHS addressing
     */
    // pcem: mfm_at.c:138-152
    private static void mfm_next_sector(mfm_t mfm)
    {
        mfm_drive_t drive = mfm.drives[mfm.drive_sel];

        mfm.sector++;
        if (mfm.sector == (drive.cfg_spt + 1))
        {
                mfm.sector = 1;
                mfm.head++;
                if (mfm.head == drive.cfg_hpc)
                {
                        mfm.head = 0;
                        mfm.cylinder++;
                        if (drive.current_cylinder < drive.hdd_file.tracks)
                                drive.current_cylinder++;
                }
        }
    }

    // pcem: mfm_at.c:154-294
    internal static void mfm_write(uint16_t port, uint8_t val, object p)
    {
        mfm_t mfm = (mfm_t)p;

        // omitted: pclog("mfm_write: addr=%04x val=%02x\n") — commenté chez PCem.
        switch (port)
        {
        case 0x1F0: /* Data */
                // UN OCTET ÉCRIT EN 0x1F0 DEVIENT UN MOT, l'octet étant recopié dans
                // les deux moitiés. C'est ce que le C fait, et ce n'est pas anodin : le
                // tampon avance de DEUX à chaque écriture d'un octet. Reproduit tel
                // quel — aucun BIOS n'emprunte ce chemin, l'accès au tampon se faisant
                // par mots.
                mfm_writew(port, (uint16_t)(val | (val << 8)), p);
                return;

        case 0x1F1: /* Write precompenstation */
                mfm.cylprecomp = val;
                return;

        case 0x1F2: /* Sector count */
                mfm.secount = val;
                return;

        case 0x1F3: /* Sector */
                mfm.sector = val;
                return;

        case 0x1F4: /* Cylinder low */
                mfm.cylinder = (mfm.cylinder & 0xFF00) | val;
                return;

        case 0x1F5: /* Cylinder high */
                mfm.cylinder = (mfm.cylinder & 0xFF) | (val << 8);
                return;

        case 0x1F6: /* Drive/Head */
                mfm.head = val & 0xF;
                mfm.drive_sel = ((val & 0x10) != 0) ? 1 : 0;
                if (mfm.drives[mfm.drive_sel].hdd_file.f == null)
                        mfm.status = 0;
                else
                        mfm.status = STAT_READY | STAT_DSC;
                return;

        case 0x1F7: /* Command register */
                // CE fatal EST ATTEIGNABLE PAR UNE CONFIGURATION, pas par un défaut de
                // transcription : mfm_init charge les DEUX unités depuis ide_fn[0] et
                // ide_fn[1], et si le CMOS déclare un type pour D: alors que le .cfg
                // n'a pas de hdd_fn, le BIOS adresse une unité dont hdd_file.f est nul.
                // Le quartet bas du CMOS 0x12 doit rester à zéro.
                // PCem s'arrête ici par fatal() : un utilitaire qui sonde l'unité 1 absente
                // arrête l'émulateur (PB-75). Règle de TRANSCRIPTION.md : l'invité ne tue pas
                // l'hôte.
                // pcem bug, not reproduced: PB-75
                // DEVIATION: la commande est refusée — ERR, erreur ABRT, IRQ 14 —, le
                //   comportement sûr le plus proche d'un contrôleur sans unité 1.
                if (mfm.drives[mfm.drive_sel].hdd_file.f == null)
                {
                        mfm.error = ERR_ABRT;
                        mfm.status = STAT_ERR;
                        mfm_irq_raise(mfm);
                        return;
                }

                mfm_irq_lower(mfm);
                mfm.command = val;
                mfm.error = 0;

                switch (val & 0xf0)
                {
                case CMD_RESTORE:
                        // omitted: pclog("Restore\n") — commenté chez PCem.
                        mfm.command &= unchecked((uint8_t)~0x0f); /*Mask off step rate*/
                        mfm.status = STAT_BUSY;
                        timer_set_delay_u64(mfm.callback_timer, 200 * IDE_TIME);
                        break;

                case CMD_SEEK:
                        // omitted: pclog("Seek to cylinder %i\n") — commenté chez PCem.
                        mfm.command &= unchecked((uint8_t)~0x0f); /*Mask off step rate*/
                        mfm.status = STAT_BUSY;
                        timer_set_delay_u64(mfm.callback_timer, 200 * IDE_TIME);
                        break;

                default:
                        switch (val)
                        {
                        case CMD_READ:
                        case CMD_READ + 1:
                        case CMD_READ + 2:
                        case CMD_READ + 3:
                                // omitted: pclog("Read %i sectors ...") — commenté.
                                mfm.command &= unchecked((uint8_t)~3);
                                // pcem bug, not reproduced: PB-76
                                // DEVIATION: READ LONG (avec ECC) est refusé — ERR, erreur
                                //   ABRT, IRQ — au lieu du fatal() de PCem ; le vrai transfert
                                //   de 512 + 4 octets d'ECC n'est modélisé ni ici ni chez PCem.
                                if ((val & 2) != 0)
                                {
                                        mfm.error = ERR_ABRT;
                                        mfm.status = STAT_READY | STAT_DSC | STAT_ERR;
                                        mfm_irq_raise(mfm);
                                        break;
                                }
                                mfm.status = STAT_BUSY;
                                timer_set_delay_u64(mfm.callback_timer, 200 * IDE_TIME);
                                break;

                        case CMD_WRITE:
                        case CMD_WRITE + 1:
                        case CMD_WRITE + 2:
                        case CMD_WRITE + 3:
                                // omitted: pclog("Write %i sectors ...") — commenté.
                                mfm.command &= unchecked((uint8_t)~3);
                                // pcem bug, not reproduced: PB-76
                                // DEVIATION: WRITE LONG (avec ECC) est refusé — ERR, erreur
                                //   ABRT, IRQ — au lieu du fatal() de PCem ; le vrai transfert
                                //   de 512 + 4 octets d'ECC n'est modélisé ni ici ni chez PCem.
                                if ((val & 2) != 0)
                                {
                                        mfm.error = ERR_ABRT;
                                        mfm.status = STAT_READY | STAT_DSC | STAT_ERR;
                                        mfm_irq_raise(mfm);
                                        break;
                                }
                                mfm.status = STAT_DRQ | STAT_DSC; // | STAT_BUSY;
                                mfm.pos = 0;
                                break;

                        case CMD_VERIFY:
                        case CMD_VERIFY + 1:
                                // omitted: pclog("Read verify %i sectors ...") — commenté.
                                mfm.command &= unchecked((uint8_t)~1);
                                mfm.status = STAT_BUSY;
                                timer_set_delay_u64(mfm.callback_timer, 200 * IDE_TIME);
                                break;

                        case CMD_FORMAT:
                                // omitted: pclog("Format track %i head %i\n") — commenté.
                                mfm.status = STAT_DRQ | STAT_BUSY;
                                mfm.pos = 0;
                                break;

                        case CMD_SET_PARAMETERS: /* Initialize Drive Parameters */
                                mfm.status = STAT_BUSY;
                                timer_set_delay_u64(mfm.callback_timer, 30 * IDE_TIME);
                                break;

                        case CMD_DIAGNOSE: /* Execute Drive Diagnostics */
                                mfm.status = STAT_BUSY;
                                timer_set_delay_u64(mfm.callback_timer, 200 * IDE_TIME);
                                break;

                        default:
                                // omitted: pclog("Bad MFM command %02X\n") — sortie pure.
                                mfm.status = STAT_BUSY;
                                timer_set_delay_u64(mfm.callback_timer, 200 * IDE_TIME);
                                break;
                        }
                        break;
                }
                break;

        case 0x3F6: /* Device control */
                if ((mfm.fdisk & 4) != 0 && (val & 4) == 0)
                {
                        timer_set_delay_u64(mfm.callback_timer, 500 * IDE_TIME);
                        mfm.reset = 1;
                        mfm.status = STAT_BUSY;
                        // omitted: pclog("MFM Reset\n") — commenté chez PCem.
                }
                if ((val & 4) != 0)
                {
                        /*Drive held in reset*/
                        timer_disable(mfm.callback_timer);
                        mfm.status = STAT_BUSY;
                }
                mfm.fdisk = val;
                mfm_irq_update(mfm);
                return;
        }
        // omitted: fatal("Bad IDE write %04X %02X\n") — commenté chez PCem. Une
        //   écriture sur un port non listé est donc SILENCIEUSEMENT IGNORÉE, et c'est
        //   le comportement du C.
    }

    // pcem: mfm_at.c:296-308
    internal static void mfm_writew(uint16_t port, uint16_t val, object p)
    {
        mfm_t mfm = (mfm_t)p;

        // omitted: pclog("Write IDEw %04X\n") — commenté chez PCem.
        mfm.buffer[mfm.pos] = (uint8_t)val;
        mfm.buffer[mfm.pos + 1] = (uint8_t)(val >> 8);
        mfm.pos += 2;

        if (mfm.pos >= 512)
        {
                mfm.pos = 0;
                mfm.status = STAT_BUSY;
                timer_set_delay_u64(mfm.callback_timer, SECTOR_TIME);
        }
    }

    // pcem: mfm_at.c:310-351
    internal static uint8_t mfm_read(uint16_t port, object p)
    {
        mfm_t mfm = (mfm_t)p;
        uint8_t temp = 0xff;

        switch (port)
        {
        case 0x1F0: /* Data */
                temp = (uint8_t)(mfm_readw(port, mfm) & 0xff);
                break;

        case 0x1F1: /* Error */
                temp = mfm.error;
                break;

        case 0x1F2: /* Sector count */
                temp = (uint8_t)mfm.secount;
                break;

        case 0x1F3: /* Sector */
                temp = (uint8_t)mfm.sector;
                break;

        case 0x1F4: /* Cylinder low */
                temp = (uint8_t)(mfm.cylinder & 0xFF);
                break;

        case 0x1F5: /* Cylinder high */
                temp = (uint8_t)(mfm.cylinder >> 8);
                break;

        case 0x1F6: /* Drive/Head */
                temp = (uint8_t)(mfm.head | (mfm.drive_sel != 0 ? 0x10 : 0) | 0xa0);
                break;

        case 0x1F7: /* Status */
                // LIRE L'ÉTAT ACQUITTE L'INTERRUPTION, et c'est le comportement de
                // l'ATA qui commence ici.
                mfm_irq_lower(mfm);
                temp = mfm.status;
                break;
        }

        // omitted: pclog("mfm_read: addr=%04x val=%02x %04X:%04x\n") — commenté.
        return temp;
    }

    // pcem: mfm_at.c:353-376
    internal static uint16_t mfm_readw(uint16_t port, object p)
    {
        mfm_t mfm = (mfm_t)p;
        uint16_t temp;

        temp = (uint16_t)(mfm.buffer[mfm.pos] | (mfm.buffer[mfm.pos + 1] << 8));
        mfm.pos += 2;

        if (mfm.pos >= 512)
        {
                // omitted: pclog("Over! packlen %i %i\n") — commenté chez PCem.
                mfm.pos = 0;
                mfm.status = STAT_READY | STAT_DSC;
                if (mfm.command == CMD_READ)
                {
                        mfm.secount = (mfm.secount - 1) & 0xff;
                        if (mfm.secount != 0)
                        {
                                mfm_next_sector(mfm);
                                mfm.status = STAT_BUSY | STAT_READY | STAT_DSC;
                                timer_set_delay_u64(mfm.callback_timer, SECTOR_TIME);
                        }
                }
        }

        // omitted: pclog("mem_readw: temp=%04x %i\n") — commenté chez PCem.
        return temp;
    }

    // pcem: mfm_at.c:378-385
    private static void do_seek(mfm_t mfm)
    {
        mfm_drive_t drive = mfm.drives[mfm.drive_sel];

        if (mfm.cylinder < drive.hdd_file.tracks)
                drive.current_cylinder = mfm.cylinder;
        else
                drive.current_cylinder = drive.hdd_file.tracks - 1;
    }

    // pcem: mfm_at.c:387-503
    internal static void mfm_callback(object? p)
    {
        mfm_t mfm = (mfm_t)p;
        mfm_drive_t drive = mfm.drives[mfm.drive_sel];
        int addr;

        // omitted: pclog("mfm_callback: command=%02x reset=%i\n") — commenté.

        if (mfm.reset != 0)
        {
                mfm.status = STAT_READY | STAT_DSC;
                mfm.error = 1;
                mfm.secount = 1;
                mfm.sector = 1;
                mfm.head = 0;
                mfm.cylinder = 0;
                mfm.reset = 0;
                // omitted: pclog("Reset callback\n") — commenté chez PCem.
                return;
        }
        switch (mfm.command)
        {
        case CMD_RESTORE:
                drive.current_cylinder = 0;
                mfm.status = STAT_READY | STAT_DSC;
                mfm_irq_raise(mfm);
                break;

        case CMD_SEEK:
                do_seek(mfm);
                mfm.status = STAT_READY | STAT_DSC;
                mfm_irq_raise(mfm);
                break;

        case CMD_READ:
                do_seek(mfm);
                if (mfm_get_sector(mfm, out addr) != 0)
                {
                        mfm.error = ERR_ID_NOT_FOUND;
                        mfm.status = STAT_READY | STAT_DSC | STAT_ERR;
                        mfm_irq_raise(mfm);
                        break;
                }

                // omitted: pclog("Read %i %i %i %08X\n") — commenté chez PCem.
                hdd_file.hdd_read_sectors(drive.hdd_file, addr, 1, mfm.buffer);
                mfm.pos = 0;
                mfm.status = STAT_DRQ | STAT_READY | STAT_DSC;
                // omitted: pclog("Read sector callback ...") — commenté chez PCem.
                mfm_irq_raise(mfm);
                readflash_set(READFLASH_HDC, mfm.drive_sel);
                break;

        case CMD_WRITE:
                do_seek(mfm);
                if (mfm_get_sector(mfm, out addr) != 0)
                {
                        mfm.error = ERR_ID_NOT_FOUND;
                        mfm.status = STAT_READY | STAT_DSC | STAT_ERR;
                        mfm_irq_raise(mfm);
                        break;
                }
                hdd_file.hdd_write_sectors(drive.hdd_file, addr, 1, mfm.buffer);
                mfm_irq_raise(mfm);
                mfm.secount = (mfm.secount - 1) & 0xff;
                if (mfm.secount != 0)
                {
                        mfm.status = STAT_DRQ | STAT_READY | STAT_DSC;
                        mfm.pos = 0;
                        mfm_next_sector(mfm);
                }
                else
                        mfm.status = STAT_READY | STAT_DSC;
                readflash_set(READFLASH_HDC, mfm.drive_sel);
                break;

        case CMD_VERIFY:
                // NE LIT RIEN. Le C fait le seek, remet pos à zéro, lève l'IRQ et
                // s'arrête : la vérification ne vérifie aucune donnée, et mfm_get_sector
                // n'est même pas appelé. Reproduit tel quel.
                do_seek(mfm);
                mfm.pos = 0;
                mfm.status = STAT_READY | STAT_DSC;
                // omitted: pclog("Read verify callback ...") — commenté chez PCem.
                mfm_irq_raise(mfm);
                readflash_set(READFLASH_HDC, mfm.drive_sel);
                break;

        case CMD_FORMAT:
                do_seek(mfm);
                if (mfm_get_sector(mfm, out addr) != 0)
                {
                        mfm.error = ERR_ID_NOT_FOUND;
                        mfm.status = STAT_READY | STAT_DSC | STAT_ERR;
                        mfm_irq_raise(mfm);
                        break;
                }
                hdd_file.hdd_format_sectors(drive.hdd_file, addr, mfm.secount);
                mfm.status = STAT_READY | STAT_DSC;
                mfm_irq_raise(mfm);
                readflash_set(READFLASH_HDC, mfm.drive_sel);
                break;

        case CMD_DIAGNOSE:
                mfm.error = 1; /*No error detected*/
                mfm.status = STAT_READY | STAT_DSC;
                mfm_irq_raise(mfm);
                break;

        case CMD_SET_PARAMETERS: /* Initialize Drive Parameters */
                // LA GÉOMÉTRIE ARRIVE ICI, ET ELLE NE PORTE NI CYLINDRES NI ADRESSE.
                // Le BIOS a mis les secteurs par piste dans le compteur de secteurs et
                // « têtes - 1 » dans les quatre bits bas du registre 0x1F6 ; c'est tout
                // ce que la commande 0x91 transporte. Le nombre de cylindres n'est
                // jamais annoncé : il n'arrive qu'au coup par coup, par 0x1F4 et 0x1F5.
                // C'est ce qui rend cette carte capable des 46 types là où le Xebec est
                // limité à quatre.
                drive.cfg_spt = mfm.secount;
                drive.cfg_hpc = mfm.head + 1;
                // omitted: pclog("Parameters: spt=%i hpc=%i\n") — sortie pure.
                mfm.status = STAT_READY | STAT_DSC;
                mfm_irq_raise(mfm);
                break;

        default:
                // omitted: pclog("Callback on unknown command %02x\n") — sortie pure.
                mfm.status = STAT_READY | STAT_ERR | STAT_DSC;
                mfm.error = ERR_ABRT;
                mfm_irq_raise(mfm);
                break;
        }
    }

    // pcem: mfm_at.c:505-522
    private static object? mfm_init()
    {
        mfm_t mfm = new mfm_t();

        // LES DEUX UNITÉS SONT CHARGÉES, présente ou non. hdd_load laisse hdd_file.f
        // nul si le nom est vide, et c'est ce nul que le registre de commande teste
        // avant son fatal.
        hdd_file.hdd_load(mfm.drives[0].hdd_file, 0, hdd_c.ide_fn[0]);
        hdd_file.hdd_load(mfm.drives[1].hdd_file, 1, hdd_c.ide_fn[1]);

        mfm.status = STAT_READY | STAT_DSC;
        mfm.error = 1; /*No errors*/

        // TROIS ENREGISTREMENTS, ET LE PREMIER EST LE SEUL À PORTER DES GESTIONNAIRES
        // 16 BITS. Le port de données 0x1F0 se lit et s'écrit par mots — c'est par lui
        // que passe le secteur — là où 0x1F1-0x1F7 sont des registres d'octets. Le
        // Xebec n'avait aucun accès 16 bits ; c'est le premier usage de inw_fn/outw_fn
        // par une carte de ce dépôt.
        io_sethandler(0x01f0, 0x0001, mfm_read, mfm_readw, null, mfm_write, mfm_writew, null, mfm);
        io_sethandler(0x01f1, 0x0007, mfm_read, null, null, mfm_write, null, null, mfm);
        io_sethandler(0x03f6, 0x0001, null, null, null, mfm_write, null, null, mfm);

        timer_add(mfm.callback_timer, mfm_callback, mfm, 0);

        return mfm;
    }

    // pcem: mfm_at.c:524-531
    private static void mfm_close(object p)
    {
        mfm_t mfm = (mfm_t)p;

        hdd_file.hdd_close(mfm.drives[0].hdd_file);
        hdd_file.hdd_close(mfm.drives[1].hdd_file);

        // omitted: free(mfm) — le ramasse-miettes.
    }

    // pcem: mfm_at.c:533
    internal static device_t mfm_at_device = new device_t("IBM PC AT Fixed Disk Adapter", DEVICE_AT,
                                                         mfm_init, mfm_close,
                                                         null, null, null, null, null);
}
