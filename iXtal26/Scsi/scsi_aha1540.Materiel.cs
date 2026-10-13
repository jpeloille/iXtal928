// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — le code propre au mode matériel de l'Adaptec AHA-1542C (G13 ; R10 de TRANSCRIPTION.md).
// STATUS: materiel
//
// Les corrections des défauts de PCem dans scsi_aha1540.c, appelées par les gardes de scsi_aha1540.cs
// (`if (materiel.pb_nn)`), seules sur leur ligne. La source, le cas qui discrimine et la panne qui le rougit sont dans
// l'entrée PB de PCEM_BUGS.md. La référence : AHA-1540C/1542C Technical Reference (Adaptec, 510397-00 A, avril 1993) —
// chapitre 3 (les réglages d'usine, les interrupteurs), chapitre 4 (le port de contrôle, les resets), chapitre 5 (Return
// Setup Data, Set EEPROM, Return EEPROM, les mailbox, le CCB, le sense automatique). Les méthodes portent NoInlining
// (M2, G13.7c2).

// CS8981 : `scsi_aha1540` n'a que des minuscules ASCII — le nom de l'unité C (scsi_aha1540.cs).
#pragma warning disable CS8981

using System.Runtime.CompilerServices;
using static iXtal26.Memory.mem;
using static iXtal26.Models.pic;
using static iXtal26.Scsi.scsi;

namespace iXtal26.Scsi;

internal static partial class scsi_aha1540
{
    /// <summary>L'EEPROM d'usine : celle de référence de PCem (`nvr/default/aha1542c.nvr`), avec l'ID 7, le DMA 5 et
    /// l'IRQ 11 du TR (chapitre 3) au codage de PCem — octet 0 : l'ID ; octet 1 : le DMA en bits 4-6, l'IRQ moins 9 en
    /// bits 0-2. Les autres octets, ceux de la référence : déduit.</summary>
    private static uint8_t[] eeprom_usine_materiel()
    {
        var e = new uint8_t[256];
        e[0] = 0x07;
        e[1] = 0x52;
        e[2] = 0xb6;
        e[14] = 0x08;
        return e;
    }

    /// <summary>PB-144 : l'EEPROM du mode matériel, dans son fichier (`nvr/.aha1542c.materiel.nvr`, Q8 : une EEPROM
    /// écrite dans un mode ne change pas l'autre) ; sans lui, l'usine. `nvr/default/` ne livre aucune référence pour ce
    /// mode : le repli de nvrfopen n'y trouve rien.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void eeprom_materiel(aha154x_t scsi)
    {
        ModeMateriel.Sonde[144]++;
        scsi.fn = "aha1542c.materiel.nvr";
        FileStream? f = Devices.nvr.nvrfopen(scsi.fn, "rb");
        if (f is null)
        {
                eeprom_usine_materiel().CopyTo(scsi.eeprom, 0);
                return;
        }
        Array.Clear(scsi.eeprom);
        Flash.rom.fread(scsi.eeprom, 0, 1, 32, f);
        f.Close();
    }

    /// <summary>PB-140 : Return Setup Data (0Dh) en entier — les octets 00h à 24h comme chez PCem, puis 25h à 2Bh (le
    /// réessai à zéro, les interrupteurs, la somme, la mailbox du BIOS), 2Ch et au-delà à zéro ; une longueur nulle rend
    /// 256 octets. La commande entière : la garde est en tête du `case` (M2 : une garde après une boucle de
    /// process_cmd change le code que le JIT en tire, même repliée).</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool setup_data_materiel(aha154x_t scsi)
    {
        ModeMateriel.Sonde[140]++;
        int c;

        for (c = 0; c < 256; c++)
                scsi.result[c] = 0;
        scsi.result[1] = scsi.atbs;
        scsi.result[2] = scsi.bon;
        scsi.result[3] = scsi.boff;
        scsi.result[4] = (uint8_t)scsi.mbc;
        scsi.result[5] = (uint8_t)(scsi.mba >> 16);
        scsi.result[6] = (uint8_t)(scsi.mba >> 8);
        scsi.result[7] = (uint8_t)(scsi.mba & 0xff);
        scsi.result[38] = interrupteurs_materiel(scsi);
        scsi.result[39] = 0xa3;
        scsi.result[40] = 0xc2;
        scsi.result[41] = (uint8_t)(scsi.bios_mba >> 16);
        scsi.result[42] = (uint8_t)(scsi.bios_mba >> 8);
        scsi.result[43] = (uint8_t)(scsi.bios_mba & 0xff);
        scsi.cmd_state = CMD_STATE_SEND_RESULT;
        scsi.result_pos = 0;
        scsi.result_len = scsi.@params[0] == 0 ? 256 : scsi.@params[0];
        return true;
    }

    /// <summary>L'octet 26h de Return Setup Data : un interrupteur sur On rend 1 (TR, chapitre 5). Bits 0-2 : SW2-SW4,
    /// le port (`dipsw`, la table du chapitre 3) ; bit 3 : SW5, le contrôleur de disquettes — la carte de PCem n'a pas
    /// de 82077, SW5 sur On (désactivé) : déduit ; bits 4-6 : SW6-SW8, l'adresse de la ROM (DC000h à C8000h, de 16 Ko
    /// en 16 Ko) ; bit 7 : la donnée lue de l'EEPROM, inconnue, laissée à 0 comme chez PCem.</summary>
    private static uint8_t interrupteurs_materiel(aha154x_t scsi)
    {
        var rom = (int)((0xdc000 - scsi.mapping.@base) / 0x4000) & 7;
        return (uint8_t)((scsi.dipsw & 7) | 0x08 | (rom << 4));
    }

    /// <summary>PB-277 : Set EEPROM (22h) en entier — comme chez PCem (PB-136 reproduit, param_lu), mais quand l'IRQ
    /// change avec une interruption en attente, la carte la présente désormais sur la nouvelle ligne (set_irq, à la fin
    /// de la commande) et l'ancienne retombe — déduit.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool eeprom_programmee_materiel(aha154x_t scsi)
    {
        ModeMateriel.Sonde[277]++;
        int c;

        // pcem bug, reproduced: PB-136 — params[c + 3] pour c < params[1], au-delà de params[64] (param_lu).
        for (c = 0; c < scsi.@params[1]; c++)
                scsi.eeprom[(scsi.@params[2] + c) & 0xff] = param_lu(scsi, c + 3);
        if ((scsi.eeprom[1] & 7) + 9 != scsi.irq && (scsi.isr & ISR_ANYINTR) != 0)
                clear_irq(scsi);
        scsi.host_id = scsi.eeprom[0] & 7;
        scsi.dma = (scsi.eeprom[1] >> 4) & 7;
        scsi.irq = (scsi.eeprom[1] & 7) + 9;

        aha1542c_eeprom_save(scsi);
        set_irq(scsi, ISR_HACC);
        scsi.cmd_state = CMD_STATE_IDLE;
        return true;
    }

    /// <summary>PB-218 : Return EEPROM (23h), le drapeau à 0 : les options d'usine (TR, chapitre 5), au lieu de la
    /// configuration ; tout autre drapeau, la configuration, comme chez PCem.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool eeprom_rendue_materiel(aha154x_t scsi)
    {
        ModeMateriel.Sonde[218]++;
        var source = scsi.@params[0] == 0 ? eeprom_usine_materiel() : scsi.eeprom;
        int c;

        for (c = 0; c < scsi.@params[1]; c++)
                scsi.result[c] = source[(scsi.@params[2] + c) & 0xff];
        scsi.cmd_state = CMD_STATE_SEND_RESULT;
        scsi.result_pos = 0;
        scsi.result_len = c;
        return true;
    }

    // ---- G13.7e1b : les mailbox (PB-141, PB-139) ----

    private const uint8_t MBI_CCB_NON_TROUVE_MATERIEL = 0x03;

    // L'état que PCem n'a pas, dans une classe à part (un champ statique de plus dans scsi_aha1540 changerait son
    // constructeur statique).
    private static class EtatAha
    {
        internal sealed class Mailbox
        {
            /// <summary>Une Mailbox Initialization valide reçue depuis la mise sous tension ou le dernier HRST ou SRST.</summary>
            internal bool initialisee;
            /// <summary>Un balayage demandé par 02h, qui dure tant qu'un MBO plein reste à servir.</summary>
            internal bool balayage;
            /// <summary>Le dernier MBO servi : le balayage reprend à l'emplacement suivant (tourniquet).</summary>
            internal int dernier = -1;
        }

        internal static readonly ConditionalWeakTable<aha154x_t, Mailbox> Mailboxes = new();

        internal sealed class Transfert
        {
            /// <summary>La cible a voulu plus de données que le CCB n'en alloue : le surplus est passé (G13.7e2a).</summary>
            internal bool depassement;
        }

        internal static readonly ConditionalWeakTable<aha154x_t, Transfert> Transferts = new();
    }

    private static EtatAha.Mailbox mailbox_materiel(aha154x_t scsi) => EtatAha.Mailboxes.GetOrCreateValue(scsi);

    /// <summary>PB-141 : HRST et SRST abandonnent les balayages en cours et exigent une nouvelle Mailbox Initialization
    /// (TR, chapitre 4 : « the mailbox structure must be reinitialized » ; SRST : « all queued commands are
    /// abandoned ») ; HRST rend aussi la mailbox du BIOS à son état de mise sous tension (« identical to that of normal
    /// power-on ») ; SRST la laisse déclarée — le TR n'en dit rien.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void reset_mailbox_materiel(aha154x_t scsi, uint8_t val)
    {
        if ((val & (CTRL_RESET | CTRL_SRST)) == 0)
                return;
        ModeMateriel.Sonde[141]++;
        var m = mailbox_materiel(scsi);
        m.initialisee = false;
        m.balayage = false;
        m.dernier = -1;
        scsi.mbo_req = 0;
        scsi.bios_mbo_req = 0;
        if ((val & CTRL_RESET) != 0)
                scsi.bios_mbo_inited = 0;
    }

    /// <summary>PB-141 : Mailbox Initialization (01h) — un compte nul pose INVDCMD et HACC, la mailbox inchangée (TR,
    /// chapitre 5 : « Must be greater than zero ») ; sinon, comme PCem, et le tourniquet repart de l'emplacement 0.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool mailbox_init_materiel(aha154x_t scsi)
    {
        ModeMateriel.Sonde[141]++;
        var m = mailbox_materiel(scsi);
        if (scsi.@params[0] == 0)
        {
                scsi.status |= STATUS_INVDCMD;
                set_irq(scsi, ISR_HACC);
                scsi.cmd_state = CMD_STATE_IDLE;
                return true;
        }
        scsi.mbc = scsi.@params[0];
        scsi.mba = (uint32_t)(scsi.@params[3] | (scsi.@params[2] << 8) | (scsi.@params[1] << 16));
        scsi.mba_i = scsi.mba + (uint32_t)(scsi.mbc * 4);
        scsi.current_mbo = 0;
        scsi.current_mbi = 0;
        scsi.status &= unchecked((uint8_t)~STATUS_INIT);
        m.initialisee = true;
        m.balayage = false;
        m.dernier = -1;
        set_irq(scsi, ISR_HACC);
        scsi.cmd_state = CMD_STATE_IDLE;
        return true;
    }

    /// <summary>PB-141 : Start SCSI Command (02h) — sans mailbox initialisée, INVDCMD et HACC ; sinon un balayage, qui
    /// dure jusqu'à ce que tous les MBO pleins soient servis (TR, chapitre 5), sans HACC.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool demarrage_materiel(aha154x_t scsi)
    {
        ModeMateriel.Sonde[141]++;
        var m = mailbox_materiel(scsi);
        if (!m.initialisee)
        {
                scsi.status |= STATUS_INVDCMD;
                set_irq(scsi, ISR_HACC);
        }
        else
                m.balayage = true;
        scsi.cmd_state = CMD_STATE_IDLE;
        return true;
    }

    /// <summary>Le MBI libre où la carte rendra le prochain achèvement, cherché en tourniquet depuis `current_mbi` (TR,
    /// chapitre 5 : « scans the first byte of an MBI entry to find a free mailbox ») ; faux s'il n'y en a aucun.</summary>
    private static bool mbi_libre_materiel(aha154x_t scsi)
    {
        for (var k = 0; k < scsi.mbc; k++)
        {
                var i = (scsi.current_mbi + k) % scsi.mbc;
                if (mem_readb_phys(scsi.mba_i + (uint32_t)(i * 4)) == 0)
                {
                        scsi.current_mbi = i;
                        return true;
                }
        }
        return false;
    }

    private static void mbi_materiel(aha154x_t scsi, uint8_t code, uint32_t ccb)
    {
        mem_writeb_phys(scsi.mba_i + (uint32_t)(scsi.current_mbi * 4), code);
        mem_writeb_phys(scsi.mba_i + (uint32_t)(scsi.current_mbi * 4) + 1, (uint8_t)(ccb >> 16));
        mem_writeb_phys(scsi.mba_i + (uint32_t)(scsi.current_mbi * 4) + 2, (uint8_t)(ccb >> 8));
        mem_writeb_phys(scsi.mba_i + (uint32_t)(scsi.current_mbi * 4) + 3, (uint8_t)(ccb & 0xff));
        scsi.current_mbi++;
        if (scsi.current_mbi >= scsi.mbc)
                scsi.current_mbi = 0;
        set_irq(scsi, (uint8_t)((scsi.mbo_irq_enable != 0 ? ISR_MBOA : 0) | ISR_MBIF));
    }

    /// <summary>PB-141 et PB-139 : l'état CCB_STATE_IDLE de process_ccb en entier, chaque part selon sa correction.
    /// PB-141 : le balayage dure tant qu'un MBO plein reste, en tourniquet depuis l'emplacement qui suit le dernier
    /// servi (TR, chapitre 5, MBO) ; un code de MBO inconnu est remis à zéro sans MBI ; aucun CCB ne part sans un MBI
    /// libre pour son achèvement — un seul CCB à la fois, et l'hôte ne fait que libérer des MBI : libre au départ, il
    /// l'est à l'achèvement (déduit : la carte attend au moment de rendre) ; la mailbox du BIOS, qui n'écrit aucun MBI,
    /// n'attend pas le MBI de la normale, et n'est balayée que si la normale n'a rien lancé (déduit). Sans PB-141, le balayage de PCem (un par 02h compté, depuis l'emplacement 0).
    /// PB-139 : ABORT efface son propre MBO, cherche le CCB visé parmi les START qui attendent — trouvé, ce START est
    /// effacé et le MBI dit 02h ; sinon 03h (« Aborted CCB not found ») — un seul MBI ; sans PB-139, l'ABORT de PCem
    /// (c * 8, 02h toujours). Un CCB en vol ne s'abandonne pas ici : le balayage n'a lieu qu'au repos.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool balayage_materiel(aha154x_t scsi)
    {
        var m = mailbox_materiel(scsi);
        var tourniquet = materiel.pb_141;
        var abandon = materiel.pb_139;

        var actif = tourniquet ? m.balayage && m.initialisee : (scsi.status & STATUS_INIT) == 0 && scsi.mbo_req != 0;
        if (actif)
        {
                var servi = false;
                var attente = false;
                for (var k = 0; k < scsi.mbc && !servi && !attente; k++)
                {
                        var c = tourniquet ? (m.dernier + 1 + k) % scsi.mbc : k;
                        uint32_t mbo = mem_readl_phys(scsi.mba + (uint32_t)(c * 4));
                        var command = (uint8_t)(mbo & 0xff);
                        uint32_t ccb = (mbo >> 24) | ((mbo >> 8) & 0xff00) | ((mbo << 8) & 0xff0000);

                        if (command == COMMAND_START_CCB)
                        {
                                if (tourniquet && !mbi_libre_materiel(scsi))
                                {
                                        attente = true;
                                        break;
                                }
                                if (tourniquet)
                                        ModeMateriel.Sonde[141]++;
                                scsi.current_mbo = c;
                                scsi.current_mbo_is_bios = 0;
                                scsi.ccb.addr = ccb;
                                process_cdb(scsi);
                                m.dernier = c;
                                if (!tourniquet)
                                        scsi.mbo_req--;
                                servi = true;
                        }
                        else if (command == COMMAND_ABORT_CCB)
                        {
                                if (tourniquet && !mbi_libre_materiel(scsi))
                                {
                                        attente = true;
                                        break;
                                }
                                if (abandon)
                                        ModeMateriel.Sonde[139]++;
                                if (!tourniquet)
                                        scsi.mbo_req--;
                                m.dernier = c;
                                if (abandon)
                                {
                                        mem_writeb_phys(scsi.mba + (uint32_t)(c * 4), COMMAND_FREE_CCB);
                                        var trouve = false;
                                        for (var j = 0; j < scsi.mbc && !trouve; j++)
                                        {
                                                uint32_t autre = mem_readl_phys(scsi.mba + (uint32_t)(j * 4));
                                                uint32_t pointe = (autre >> 24) | ((autre >> 8) & 0xff00) | ((autre << 8) & 0xff0000);
                                                if ((autre & 0xff) == COMMAND_START_CCB && pointe == ccb)
                                                {
                                                        mem_writeb_phys(scsi.mba + (uint32_t)(j * 4), COMMAND_FREE_CCB);
                                                        trouve = true;
                                                }
                                        }
                                        mbi_materiel(scsi, trouve ? MBI_CCB_ABORTED : MBI_CCB_NON_TROUVE_MATERIEL, ccb);
                                }
                                else
                                {
                                        // L'ABORT de PCem, la correction de PB-139 coupée : c * 8, et HACC avec le MBI.
                                        mem_writeb_phys(scsi.mba + (uint32_t)(c * 8), 0);
                                        mbi_materiel(scsi, MBI_CCB_ABORTED, ccb);
                                        scsi.isr |= ISR_HACC;
                                }
                                servi = true;
                        }
                        else if (command != COMMAND_FREE_CCB && tourniquet)
                        {
                                mem_writeb_phys(scsi.mba + (uint32_t)(c * 4), COMMAND_FREE_CCB);
                        }
                }
                if (tourniquet && !servi && !attente)
                        m.balayage = false;
        }
        if ((!tourniquet || scsi.ccb_state == CCB_STATE_IDLE) && scsi.bios_mbo_inited != 0 && scsi.bios_mbo_req != 0)
        {
                for (var c = 0; c < scsi.bios_mbc; c++)
                {
                        uint32_t mbo = mem_readl_phys(scsi.bios_mba + (uint32_t)(c * 4));
                        var command = (uint8_t)(mbo & 0xff);
                        uint32_t ccb = (mbo >> 24) | ((mbo >> 8) & 0xff00) | ((mbo << 8) & 0xff0000);

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
        return true;
    }

    // ---- G13.7e2a : les données et le bus (PB-276, PB-278) ----

    private const uint8_t HOST_STATUS_DEPASSEMENT_MATERIEL = 0x12;

    private static EtatAha.Transfert transfert_materiel(aha154x_t scsi) => EtatAha.Transferts.GetOrCreateValue(scsi);

    /// <summary>PB-276 : HRST et SCRST posent la condition de reset sur le bus SCSI (TR, chapitre 4 : « A Reset
    /// Condition is generated on the SCSI bus » ; SCRST : « causes a SCSI Bus Reset to be generated ») ; la cible
    /// lâche BSY et le bus revient au repos. SRST ne la pose pas (« A Reset Condition is not generated »), comme PCem.
    /// Ce que devient le CCB en vol : PB-281, reproduit.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void reset_bus_materiel(aha154x_t scsi, uint8_t val)
    {
        if ((val & (CTRL_RESET | CTRL_BRST)) == 0)
                return;
        ModeMateriel.Sonde[276]++;
        scsi_bus_reset(scsi.bus);
        transfert_materiel(scsi).depassement = false;
    }

    /// <summary>PB-276 et PB-278 : la tête de SCSI_STATE_SELECT. La sélection d'une commande (et non celle de son
    /// REQUEST SENSE) ouvre un CCB neuf : le dépassement du précédent est oublié. PB-278 : l'ID de l'hôte n'est pas
    /// une cible ; le sélectionner échoue comme une cible absente, 11h (déduit : la carte ne peut pas se sélectionner
    /// elle-même). Vrai si la sélection est faite ici.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool selection_materiel(aha154x_t scsi)
    {
        if (scsi.ccb_state == CCB_STATE_WAIT_COMMAND)
                transfert_materiel(scsi).depassement = false;
        if (materiel.pb_278 && scsi.ccb.target_id == scsi.host_id)
        {
                ModeMateriel.Sonde[278]++;
                scsi.cdb.last_status = 0;
                scsi.scsi_state = SCSI_STATE_SELECT_FAILED;
                return true;
        }
        return false;
    }

    /// <summary>PB-276 et PB-278 : SCSI_STATE_READ_DATA (lecture) ou SCSI_STATE_WRITE_DATA en entier, octet par
    /// octet, la liste de dispersion relue entre deux segments comme chez PCem.
    /// PB-276 : au bout du CCB, tant que la cible reste en phase de données, la carte passe le surplus — lu et jeté,
    /// ou des zéros envoyés (déduit : le TR dit seulement qu'une commande d'accès au média sans bits de sens « completes
    /// without error », « some or all of the data … may not be transferred » ; les zéros d'une écriture vont au média)
    /// — puis la cible passe au statut ; PCem fait la navette NEXT_PHASE ↔ données sans fin.
    /// PB-278 : un bit de sens posé arme le contrôle de longueur (TR, chapitre 5, CCB octet 1) — le surplus est alors
    /// passé de même, noté, et le CCB rendra 12h (achevement_materiel) ; les deux posés, aucun transfert (« the command
    /// must perform no data transfer », « suppression of data transfer … for Read operations ») : tout ce que la cible
    /// envoie ou demande est passé, sans erreur (déduit) ; le contrôle ne vaut que pour la commande d'un CCB de mailbox
    /// (ni pour son REQUEST SENSE, ni pour la commande BIOS 03h, sans octet de sens). Les adresses des données et de la
    /// liste, lue pendant le transfert, bouclent à 24 bits (déduit : le maître de bus ISA a 24 lignes d'adresse).
    /// Sans l'une ni l'autre correction en jeu, le bout du CCB rend NEXT_PHASE comme chez PCem.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool donnees_materiel(aha154x_t scsi, bool lecture)
    {
        var t = transfert_materiel(scsi);
        var etat = lecture ? SCSI_STATE_READ_DATA : SCSI_STATE_WRITE_DATA;
        var phase = lecture ? BUS_IO : 0;
        var controle = materiel.pb_278 && scsi.ccb_state == CCB_STATE_WAIT_COMMAND && scsi.ccb.from_mailbox != 0 &&
                       scsi.ccb.transfer_dir != 0;
        var aucun = controle && scsi.ccb.transfer_dir == 3;
        var passe = materiel.pb_276 || controle;
        var masque = materiel.pb_278 ? 0xffffffu : 0xffffffffu;
        var bytes_transferred = 0;

        while (scsi.scsi_state == etat && bytes_transferred < MAX_BYTES_TRANSFERRED_PER_POLL)
        {
                if (scsi.cdb.data_idx == scsi.cdb.data_len && scsi.cdb.scatter_gather != 0 &&
                    scsi.cdb.data_seg_list_idx < scsi.cdb.data_seg_list_len)
                {
                        var l = (scsi.cdb.data_seg_list_pointer + scsi.cdb.data_seg_list_idx) & masque;
                        scsi.cdb.data_len = mem_readb_phys((l + 2) & masque) | (mem_readb_phys((l + 1) & masque) << 8) |
                                            (mem_readb_phys(l) << 16);
                        scsi.cdb.data_pointer = (uint32_t)(mem_readb_phys((l + 5) & masque) |
                                                           (mem_readb_phys((l + 4) & masque) << 8) |
                                                           (mem_readb_phys((l + 3) & masque) << 16));
                        scsi.cdb.data_idx = 0;
                        scsi.cdb.data_seg_list_idx += 6;
                        continue;
                }
                var dans_ccb = !aucun && scsi.cdb.data_idx < scsi.cdb.data_len;
                if (!dans_ccb && !passe)
                {
                        scsi.scsi_state = SCSI_STATE_NEXT_PHASE;
                        break;
                }

                for (var d = 0; d < 20; d++)
                {
                        int bus_state = scsi_bus_read(scsi.bus);

                        // pcem bug, reproduced: PB-134 — BSY tombe après l'ACK du message, jamais ici (:1842, :1920).
                        if ((bus_state & BUS_BSY) == 0)
                                pc.fatal(lecture ? "READ_DATA - dropped BSY waiting\n" : "WRITE_DATA - dropped BSY waiting\n");

                        if ((bus_state & (BUS_IO | BUS_CD | BUS_MSG)) != phase)
                        {
                                scsi.scsi_state = SCSI_STATE_NEXT_PHASE;
                                break;
                        }

                        if ((bus_state & BUS_REQ) != 0)
                        {
                                int bus_out = 0;

                                if (!dans_ccb)
                                {
                                        ModeMateriel.Sonde[controle ? 278 : 276]++;
                                        if (controle && !aucun)
                                                t.depassement = true;
                                }
                                else if (lecture)
                                {
                                        var data = (uint8_t)BUS_GETDATA(bus_state);
                                        if (scsi.cdb.data_pointer == 0xFFFFFFFF)
                                                scsi.int_buffer[scsi.cdb.data_idx] = data;
                                        else
                                                mem_writeb_phys((scsi.cdb.data_pointer + (uint32_t)scsi.cdb.data_idx) & masque, data);
                                        scsi.cdb.data_idx++;
                                        scsi.cdb.bytes_transferred++;
                                }
                                else
                                {
                                        uint8_t data;
                                        if (scsi.cdb.data_pointer == 0xFFFFFFFF)
                                                data = scsi.int_buffer[scsi.cdb.data_idx];
                                        else
                                                data = mem_readb_phys((scsi.cdb.data_pointer + (uint32_t)scsi.cdb.data_idx) & masque);
                                        scsi.cdb.data_idx++;
                                        scsi.cdb.bytes_transferred++;
                                        bus_out = BUS_SETDATA(data);
                                }

                                scsi_bus_update(scsi.bus, bus_out | BUS_ACK);
                                scsi_bus_update(scsi.bus, bus_out & ~BUS_ACK);
                                break;
                        }
                }

                bytes_transferred++;
        }
        return true;
    }

    /// <summary>La longueur que le CCB alloue : son champ, ou pour une liste de dispersion la somme de ses segments
    /// (TR, octets 4-6 : « the sum of all segment Data Lengths »), la liste lue à 24 bits.</summary>
    private static uint32_t longueur_ccb_materiel(aha154x_t scsi)
    {
        if (scsi.ccb.opcode != CCB_INITIATOR_SCATTER_GATHER && scsi.ccb.opcode != CCB_INITIATOR_SCATTER_GATHER_RESIDUAL)
                return scsi.ccb.data_len;
        uint32_t somme = 0;
        for (uint32_t c = 0; c + 6 <= scsi.ccb.data_seg_list_len; c += 6)
        {
                var l = (scsi.ccb.data_seg_list_pointer + c) & 0xffffff;
                somme += (uint32_t)(mem_readb_phys((l + 2) & 0xffffff) | (mem_readb_phys((l + 1) & 0xffffff) << 8) |
                                    (mem_readb_phys(l) << 16));
        }
        return somme;
    }

    /// <summary>PB-278 : l'achèvement d'un CCB de mailbox dont un seul bit de sens est posé, quand la cible a transféré
    /// plus ou moins que la longueur allouée — statut d'hôte 12h (TR, chapitre 5, CCB octet 1 : « if a data
    /// underrun/overrun condition occurs, the CCB is returned with an indication of Data Underrun/Overrun (12H) » ; et
    /// le conseil de laisser les bits à zéro aux commandes hors du média « unless transfer length checking is desired »,
    /// qui n'a de sens que si le manque compte), le MBI 04h (« completed with error »), et, pour les opcodes 03h et 04h,
    /// le résiduel : zéro au dépassement (« In the event of data overrun, these bytes are set to zero »), la différence
    /// au manque. La tête de CCB_STATE_SEND_REQUEST_SENSE (sans sense : `apres_sense` faux) et de
    /// CCB_STATE_WAIT_REQUEST_SENSE ; sinon faux, et l'état de PCem suit : sans bit de sens, ou les deux posés (aucun
    /// transfert), le CCB finit sans erreur (« completes the operation without error »).</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool achevement_materiel(aha154x_t scsi, bool apres_sense)
    {
        var t = transfert_materiel(scsi);
        if (apres_sense ? scsi.scsi_state != SCSI_STATE_IDLE : scsi.ccb.req_sense_len != CCB_SENSE_NONE)
                return false;
        var depassement = t.depassement;
        t.depassement = false;
        if (scsi.ccb.from_mailbox == 0 || scsi.ccb.transfer_dir == 0 || scsi.ccb.transfer_dir == 3)
                return false;
        var manque = (uint32_t)scsi.ccb.bytes_transferred < longueur_ccb_materiel(scsi);
        if (!depassement && !manque)
                return false;

        ModeMateriel.Sonde[278]++;
        mem_writeb_phys(scsi.ccb.addr + 0xe, HOST_STATUS_DEPASSEMENT_MATERIEL);
        mem_writeb_phys(scsi.ccb.addr + 0xf, scsi.ccb.status);
        if (scsi.ccb.residual != 0)
        {
                // Au dépassement, toute la longueur est passée : la différence est nulle.
                var residu = scsi.ccb.total_data_len - (uint32_t)scsi.ccb.bytes_transferred;
                mem_writeb_phys(scsi.ccb.addr + 0x4, (uint8_t)(residu >> 16));
                mem_writeb_phys(scsi.ccb.addr + 0x5, (uint8_t)(residu >> 8));
                mem_writeb_phys(scsi.ccb.addr + 0x6, (uint8_t)(residu & 0xff));
        }
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
                mbi_materiel(scsi, MBI_CCB_COMPLETE_WITH_ERROR, scsi.ccb.addr);
        }
        scsi.ccb_state = CCB_STATE_IDLE;
        return true;
    }

    /// <summary>PB-133 : la tête de SCSI_STATE_SELECT. La carte transmet le LUN du CCB par le message IDENTIFY (TR,
    /// chapitre 5, CCB octet 1), et la cible SCSI-2 ignore alors le champ LUN de la CDB (SCSI-2 § 7.2.2), « expected to
    /// be zero ». Nos cibles n'ont pas de phase MESSAGE OUT : le LUN du CCB remplace celui de la CDB, octet 1 bits 5-7
    /// (déduit, sur le TR lui-même : « If the target does not accept an Identify message out, the LUN field in the SCSI
    /// CDB must contain the correct Logical Unit address »). Les commandes de la carte (BIOS 03h, sense
    /// automatique) portent déjà le LUN du CCB, ou 0.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void identify_materiel(aha154x_t scsi)
    {
        var lun = (uint8_t)(scsi.ccb.lun << 5);
        if ((scsi.cdb.data[1] & 0xe0) == lun)
                return;
        ModeMateriel.Sonde[133]++;
        scsi.cdb.data[1] = (uint8_t)((scsi.cdb.data[1] & 0x1f) | lun);
    }

    /// <summary>PB-137 : la tête de CCB_STATE_SEND_REQUEST_SENSE. Le sense automatique ne suit qu'un CHECK CONDITION
    /// (TR, chapitre 5, CCB octet 3 : « When a SCSI device terminates an operation with Check Condition status … the
    /// host adapter itself must take charge of generating the Request Sense command » ; octets 18 + m : « If a Check
    /// Status condition is detected ») : après un autre statut, le CCB s'achève comme sans sense — l'octet 3 tenu
    /// pour 01h, et l'état de PCem suit (avec PB-278). Après un CHECK CONDITION : 00h demande 14 octets, 08h à FFh la longueur
    /// (02h à 07h, réservés : la longueur, comme PCem) ; l'octet de contrôle de la CDB à 0 (déduit : la carte n'a ni
    /// lien ni drapeau à demander ; PCem y met 14) ; le sense rangé à la fin du CCB, en CCB + 12h + m, pour un CCB de
    /// mailbox — et pour la commande BIOS 03h dans le tampon interne (déduit : son CCB est celui de la carte ; PCem
    /// inverse les deux, et la commande BIOS écrit au CCB périmé).</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool sense_auto_materiel(aha154x_t scsi)
    {
        if (scsi.ccb.req_sense_len == CCB_SENSE_NONE)
                return false;
        ModeMateriel.Sonde[137]++;
        if (scsi.ccb.status != STATUS_CHECK_CONDITION)
        {
                scsi.ccb.req_sense_len = CCB_SENSE_NONE;
                return false;
        }
        var n = scsi.ccb.req_sense_len == CCB_SENSE_14 ? 14 : scsi.ccb.req_sense_len;
        scsi.cdb.data[0] = SCSI_REQUEST_SENSE;
        scsi.cdb.data[1] = (uint8_t)(scsi.ccb.lun << 5);
        scsi.cdb.data[2] = 0x00;
        scsi.cdb.data[3] = 0x00;
        scsi.cdb.data[4] = (uint8_t)n;
        scsi.cdb.data[5] = 0x00;
        scsi.cdb.len = 6;
        scsi.cdb.idx = 0;
        if (scsi.ccb.from_mailbox != 0)
                scsi.cdb.data_pointer = scsi.ccb.addr + 0x12 + (uint32_t)scsi.ccb.scsi_cmd_len;
        else
                scsi.cdb.data_pointer = 0xFFFFFFFF;
        scsi.cdb.data_idx = 0;
        scsi.cdb.data_len = n;
        scsi.cdb.scatter_gather = 0;
        scsi.scsi_state = SCSI_STATE_SELECT;
        scsi.ccb_state = CCB_STATE_WAIT_REQUEST_SENSE;
        return true;
    }
}
