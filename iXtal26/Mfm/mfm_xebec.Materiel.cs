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
}
