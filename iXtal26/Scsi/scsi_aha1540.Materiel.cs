// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — le code propre au mode matériel de l'Adaptec AHA-1542C (G13 ; R10 de TRANSCRIPTION.md).
// STATUS: materiel
//
// Les corrections des défauts de PCem dans scsi_aha1540.c, appelées par les gardes de scsi_aha1540.cs
// (`if (materiel.pb_nn)`), seules sur leur ligne. La source, le cas qui discrimine et la panne qui le rougit sont dans
// l'entrée PB de PCEM_BUGS.md. La référence : AHA-1540C/1542C Technical Reference (Adaptec, 510397-00 A, avril 1993) —
// chapitre 3 (les réglages d'usine, les interrupteurs), chapitre 5 (Return Setup Data, Set EEPROM, Return EEPROM). Les
// méthodes portent NoInlining (M2, G13.7c2).

// CS8981 : `scsi_aha1540` n'a que des minuscules ASCII — le nom de l'unité C (scsi_aha1540.cs).
#pragma warning disable CS8981

using System.Runtime.CompilerServices;
using static iXtal26.Models.pic;

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
}
