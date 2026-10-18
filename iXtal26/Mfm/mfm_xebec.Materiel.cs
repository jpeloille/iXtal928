// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — le code propre au mode matériel de l'IBM Fixed Disk Adapter (G13 ; R10 de TRANSCRIPTION.md).
// STATUS: materiel
//
// Les corrections des défauts de PCem dans mfm_xebec.c, appelées par les gardes de mfm_xebec.cs (`if (materiel.pb_nn)`),
// seules sur leur ligne. La source, le cas qui discrimine et la panne qui le rougit sont dans l'entrée PB de
// PCEM_BUGS.md. La référence : IBM Fixed Disk Adapter, Hardware Reference Library 6361503, Programming Considerations.

using System.Runtime.CompilerServices;
using iXtal26.Disc;
using iXtal26.PluginApi;
using static iXtal26.PluginApi.device;

namespace iXtal26.Mfm;

internal static partial class mfm_xebec
{
    // L'état que PCem n'a pas, à côté de la carte : un champ de plus dans xebec_t changerait sa disposition, et le code
    // que le JIT produit pour les méthodes qui la lisent, en mode PCem aussi.
    private sealed class AdresseSense
    {
        /// <summary>La commande précédente visait une adresse du disque (Sense Bytes, octet 0, bit 7).</summary>
        internal bool valide;

        /// <summary>L'adresse de SEEK, que PCem ne range pas dans xebec_t ; -1 pour les autres commandes.</summary>
        internal int cylindre = -1, tete, secteur;
    }

    private static readonly ConditionalWeakTable<xebec_t, AdresseSense> Adresses = new();

    // pcem bug, fixed in hardware mode: PB-23 — l'octet de fin porte en bit 5 « the logical unit number of the drive »
    //   (Status Register, p. 3).
    private static void xebec_unite_materiel(xebec_t xebec)
    {
        xebec.completion_byte = (uint8_t)(xebec.drive_sel != 0 ? 0x20 : 0);
        ModeMateriel.Sonde[23]++;
    }

    // pcem bug, fixed in hardware mode: PB-22 — FORMAT TRACK (classe 0, 06h) porte un secteur nul dans son DCB (p. 11) :
    //   la piste désignée se formate depuis son début, quel que soit le secteur que la commande précédente a laissé.
    private static void xebec_format_materiel(xebec_t xebec)
    {
        xebec.sector = 0;
        ModeMateriel.Sonde[22]++;
    }

    // pcem bug, fixed in hardware mode: PB-25 — les têtes se numérotent depuis 0 : la tête égale au nombre de têtes,
    //   celui d'INITIALIZE DRIVE CHARACTERISTICS ou celui de l'image, est au-delà du disque, l'erreur 21h « Illegal Disk
    //   Address » (« an address that is beyond the maximum range », p. 6). PCem la laisse passer (`>`) et lit la tête 0
    //   du cylindre suivant. L'unité absente (aucune image, hpc nul) n'est pas une adresse hors du disque : la carte la
    //   dit « not ready » (04h), au refus de PB-267 (xebec_absente_r9).
    private static bool xebec_tete_materiel(xebec_t xebec, mfm_drive_t drive)
    {
        if (drive.hdd_file.f == null || (xebec.head < drive.cfg_hpc && xebec.head < drive.hdd_file.hpc))
                return false;
        pclog("mfm_get_sector: past end of configured heads\n");
        xebec.error = ERR_ILLEGAL_SECTOR_ADDRESS;
        ModeMateriel.Sonde[25]++;
        return true;
    }

    // pcem bug, fixed in hardware mode: PB-217 — au début de chaque commande, sauf REQUEST SENSE STATUS, ce que le sense
    //   dira d'elle : l'adresse est valide « only when the previous command required a disk address » (Sense Bytes,
    //   p. 4). Les commandes à adresse que la carte sert : FORMAT DRIVE, VERIFY, FORMAT TRACK, READ, WRITE, SEEK.
    private static void xebec_adresse_materiel(xebec_t xebec)
    {
        if (xebec.state != STATE_START_COMMAND || xebec.command[0] == CMD_READ_STATUS)
                return;
        var a = Adresses.GetOrCreateValue(xebec);
        a.valide = xebec.command[0] is 0x04 or CMD_VERIFY_SECTORS or CMD_FORMAT_TRACK or CMD_READ_SECTORS
                                     or CMD_WRITE_SECTORS or CMD_SEEK;
        a.cylindre = -1;
        if (xebec.command[0] == CMD_SEEK)
        {
                a.cylindre = xebec.command[3] | ((xebec.command[2] & 0xc0) << 2);
                a.tete = xebec.command[1] & 0x1f;
                a.secteur = xebec.command[2] & 0x3f;
        }
    }

    // pcem bug, fixed in hardware mode: PB-217 — les quatre octets du sense (Sense Bytes, p. 4) : octet 0, l'adresse
    //   valide (bit 7), puis le type et le code de l'erreur ; octet 1, l'unité (bit 5) et la tête ; octet 2, les deux bits
    //   hauts du cylindre (7-6) et le secteur ; octet 3, le bas du cylindre. L'adresse est celle où la commande s'est
    //   arrêtée — les registres de la carte —, ou celle du SEEK. REQUEST SENSE devient à son tour la commande précédente :
    //   un second sense n'a plus d'adresse.
    private static void xebec_sense_materiel(xebec_t xebec)
    {
        var a = Adresses.GetOrCreateValue(xebec);
        if (a.valide)
        {
                var (c, h, s) = a.cylindre >= 0 ? (a.cylindre, a.tete, a.secteur) : (xebec.cylinder, xebec.head, xebec.sector);
                xebec.data[0] |= 0x80;
                xebec.data[1] = (uint8_t)((xebec.drive_sel != 0 ? 0x20 : 0) | (h & 0x1f));
                xebec.data[2] = (uint8_t)(((c >> 2) & 0xc0) | (s & 0x3f));
                xebec.data[3] = (uint8_t)(c & 0xff);
        }
        a.valide = false;
        ModeMateriel.Sonde[217]++;
    }

    // ===== G13.12b — les interrupteurs de la carte =====

    // La table que PCem n'a pas (`config = null`), dans une classe à part : un champ statique de plus dans mfm_xebec
    // changerait son constructeur statique.
    private static class CavaliersXebec
    {
        /// <summary>SW1-SW2, l'unité 0, et SW3-SW4, l'unité 1 : le type d'IBM (20MB Fixed Disk Drive Adapter, p. 3) ;
        /// 0, sans clé, la déduction de PCem.</summary>
        internal static readonly device_config_t[] config =
        [
            new device_config_t { name = "drive0_type", type = CONFIG_SELECTION, default_int = 0, selection = Types() },
            new device_config_t { name = "drive1_type", type = CONFIG_SELECTION, default_int = 0, selection = Types() },
            new device_config_t { type = -1 },
        ];

        /// <summary>Le type d'IBM de chaque rang de xebec_hd_types (l'ordre de FD_TBL, listing p. 32).</summary>
        internal static readonly int[] types = [1, 16, 2, 13];

        private static device_config_selection_t[] Types() =>
        [
            new() { description = "Auto", value = 0 }, new() { description = "Type 1", value = 1 },
            new() { description = "Type 2", value = 2 }, new() { description = "Type 13", value = 13 },
            new() { description = "Type 16", value = 16 },
        ];
    }

    // G13.12b (Q25, Q26) — drive0_type et drive1_type, dans les deux modes : une clé posée remplace les deux bits de
    //   son unité (l'unité 0 en bits 3-2, l'unité 1 en bits 1-0, GET_PARM, listing p. 31) ; un type que l'image n'a
    //   pas est averti, la carte prenant la géométrie que le BIOS lui donne pour ce type.
    private static void xebec_cavaliers(xebec_t xebec)
    {
        for (int d = 0; d < 2; d++)
        {
                int type = device_get_config_int(d != 0 ? "drive1_type" : "drive0_type");
                if (type == 0)
                        continue;
                int c = Array.IndexOf(CavaliersXebec.types, type);
                int decalage = d != 0 ? 0 : 2;
                xebec.switches = (uint8_t)((xebec.switches & ~(3 << decalage)) | (c << decalage));
                hdd_file_t f = xebec.drives[d].hdd_file;
                var (tracks, hpc) = xebec_hd_types[c];
                if (f.f != null && (f.spt != 17 || f.hpc != hpc || f.tracks != tracks))
                        Console.Error.WriteLine($"iXtal26 : [{mfm_xebec_device.name}] drive{d}_type = {type} : " +
                                                $"l'image de {(d != 0 ? 'D' : 'C')}: fait " +
                                                $"{f.tracks}/{f.hpc}/{f.spt}, l'interrupteur dit le type {type} " +
                                                $"({tracks}/{hpc}/17) ; le BIOS de la carte prendra sa géométrie.");
        }
    }
}
