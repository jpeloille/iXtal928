// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — le code propre au mode matériel du lecteur de CD (G13 ; R10 de TRANSCRIPTION.md).
// STATUS: materiel
//
// Les corrections des défauts de PCem dans scsi_cd.c, appelées par les gardes de scsi_cd.cs (`if (materiel.pb_nn)`),
// seules sur leur ligne. La source, le cas qui discrimine et la panne qui le rougit sont dans l'entrée PB de
// PCEM_BUGS.md. Les références : SFF-8020i révision 2.6 (22 janvier 1996), ATA Packet Interface for CD-ROMs ; SCSI-2
// (X3.131-1994) pour l'en-tête de MODE SELECT(6).

// Les méthodes appelées depuis scsi_cd_command portent NoInlining : inlinées dans les branches que la garde rend mortes,
// avant que le JIT ne les retire, elles mangeaient le budget d'inlining de la méthode, et un appel de PCem
// (ide_padstr8) cessait d'y être inliné — le code machine du mode PCem changeait (M2, G13.7c2).

// CS8981 : `scsi_cd_c` porte le nom de l'unité C (scsi_cd.cs).
#pragma warning disable CS8981

using System.Runtime.CompilerServices;
using static iXtal26.Ide.ide_atapi;
using static iXtal26.Scsi.scsi;

namespace iXtal26.Scsi;

internal static partial class scsi_cd_c
{
    // SFF-8020i r2.6 § 10.8.4 : PARAMETER LIST LENGTH ERROR, INVALID FIELD IN PARAMETER LIST.
    private const int ASC_PARAMETER_LIST_LENGTH_ERROR_MATERIEL = 0x1a;
    private const int ASC_INV_FIELD_IN_PARAMETER_LIST_MATERIEL = 0x26;

    // pcem bug, fixed in hardware mode: PB-117 — READ CD-ROM CAPACITY rend l'adresse du dernier bloc avant le
    //   lead-out (SFF-8020i r2.6, § 10.8.14 et la note du modèle CD-ROM : « the logical block address of the last
    //   block prior to the lead-out area ») : le début du lead-out moins un. La capacité du moteur vaut ce début plus
    //   un (cdrom-image.cs, image_open) : moins deux.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static uint32_t dernier_bloc_materiel(uint32_t size)
    {
        if (size < 2)
            return size;
        ModeMateriel.Sonde[117]++;
        return size - 2;
    }

    // pcem bug, fixed in hardware mode: PB-268 — « If a command is issued that requests access to a logical block not
    //   within the capacity of the medium, the command is terminated with CHECK CONDITION » (SCSI-2 § 9.1.2), LOGICAL
    //   BLOCK ADDRESS OUT OF RANGE (05/21h, SFF-8020i r2.6, Table 87). Vérifié d'emblée, avant toute donnée (déduit :
    //   la commande est terminée) : PCem sert les tranches lisibles puis échoue, sert ensuite des octets périmés, ou
    //   déborde son compte d'octets, et READ CD brut rend GOOD et des zéros. Le nombre de blocs du disque : la capacité
    //   du moteur moins un (le début du lead-out, PB-117). Une longueur nulle ne lit rien : pas d'erreur.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool lecture_hors_disque_materiel(scsi_cd_data_t data)
    {
        if (data.cdlen == 0)
            return false;
        var blocs = (long)atapi!.size() - 1;
        if ((uint)data.cdpos + (long)(uint)data.cdlen <= blocs)
            return false;
        ModeMateriel.Sonde[268]++;
        atapi_cmd_error(data, SENSE_ILLEGAL_REQUEST, ASC_LBA_OUT_OF_RANGE, 0);
        data.cmd_pos = CMD_POS_IDLE;
        return true;
    }

    // pcem bug, fixed in hardware mode: PB-269 — MECHANISM STATUS (SFF-8020i r2.6 § 10.8.3) : la longueur d'allocation
    //   aux octets 8 et 9 (l'octet 7 est réservé) ; « The ATAPI CD-ROM Drive shall terminate the command with CHECK
    //   CONDITION status if the parameter list length results in the truncation of any Mechanism Status header »,
    //   PARAMETER LIST LENGTH ERROR (05/1Ah) — la seule commande où l'allocation courte est une erreur (§ 10.2.5). Une
    //   longueur nulle : GOOD sans données.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int allocation_materiel(uint8_t[] cdb, int alloc_length)
    {
        var lu = (cdb[8] << 8) | cdb[9];
        if (lu != alloc_length)
            ModeMateriel.Sonde[269]++;
        return lu;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool mechanism_status_tronque_materiel(scsi_cd_data_t data, int alloc_length)
    {
        if (alloc_length == 0 || alloc_length >= 8)
            return false;
        ModeMateriel.Sonde[269]++;
        atapi_cmd_error(data, KEY_ILLEGAL_REQ, ASC_PARAMETER_LIST_LENGTH_ERROR_MATERIEL, 0);
        data.cmd_pos = CMD_POS_IDLE;
        return true;
    }

    // pcem bug, fixed in hardware mode: PB-119 — « A parameter list length of zero indicates that no data shall be
    //   transferred. This condition shall not be considered as an error » (SFF-8020i r2.6 § 10.8.4) : GOOD, sans
    //   phase de données. PCem y entre, et elle ne finit jamais.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool mode_select_vide_materiel(scsi_cd_data_t data, int len)
    {
        if (len != 0)
            return false;
        ModeMateriel.Sonde[119]++;
        data.cmd_pos = CMD_POS_IDLE;
        return true;
    }

    // pcem bug, fixed in hardware mode: PB-119 — la liste de paramètres de MODE SELECT : l'en-tête de 8 octets de
    //   MODE SELECT(10) (SFF-8020i r2.6, Table 58 ; SCSI-2, Table 92 : la longueur des descripteurs de bloc aux octets
    //   6 et 7, nulle sur ATAPI, où ils sont réservés), de 4 octets en MODE SELECT(6) (SCSI-2, Table 91 : cette
    //   longueur à l'octet 3) ; la longueur des données du mode (octets 0 et 1) est réservée en MODE SELECT. SFF-8020i
    //   réserve les octets 3 à 7 (aucun descripteur de bloc sur ATAPI, où un hôte les met à zéro) : leur lecture vient
    //   de SCSI-2, que le même lecteur sert derrière une carte SCSI. Une liste
    //   qui tronque l'en-tête ou une page : PARAMETER LIST LENGTH ERROR (05/1Ah) ; une page inconnue, ou d'une
    //   longueur autre que celle que MODE SENSE rend : INVALID FIELD IN PARAMETER LIST (05/26h), « and shall not
    //   change any mode parameters » (§ 10.8.4) — la liste entière vérifiée avant d'appliquer. Les pages gardées sont
    //   celles de PCem (l'audio seule ; les trois autres, acceptées sans effet).
    private static bool cdrom_mode_select_materiel(scsi_cd_data_t data, int data_len)
    {
        ModeMateriel.Sonde[119]++;
        var b = data.data_out;
        var entete = data.prefix_len == 6 ? 4 : 8;
        if (data_len < entete)
        {
            atapi_cmd_error(data, KEY_ILLEGAL_REQ, ASC_PARAMETER_LIST_LENGTH_ERROR_MATERIEL, 0);
            return true;
        }
        var debut = entete + (data.prefix_len == 6 ? b[3] : (b[6] << 8) | b[7]);
        if (debut > data_len)
        {
            atapi_cmd_error(data, KEY_ILLEGAL_REQ, ASC_PARAMETER_LIST_LENGTH_ERROR_MATERIEL, 0);
            return true;
        }

        for (var pos = debut; pos < data_len;)
        {
            if (pos + 2 > data_len || pos + 2 + b[pos + 1] > data_len)
            {
                atapi_cmd_error(data, KEY_ILLEGAL_REQ, ASC_PARAMETER_LIST_LENGTH_ERROR_MATERIEL, 0);
                return true;
            }
            var attendue = (b[pos] & 0x3f) switch
            {
                GPMODE_CDROM_AUDIO_PAGE => 0xe,
                GPMODE_R_W_ERROR_PAGE => 0x6,
                GPMODE_CDROM_PAGE => 0x6,
                GPMODE_CAPABILITIES_PAGE => 0x12,
                _ => -1,
            };
            if (b[pos + 1] != attendue)
            {
                atapi_cmd_error(data, KEY_ILLEGAL_REQ, ASC_INV_FIELD_IN_PARAMETER_LIST_MATERIEL, 0);
                return true;
            }
            pos += 2 + b[pos + 1];
        }

        for (var pos = debut; pos < data_len; pos += 2 + b[pos + 1])
        {
            if ((b[pos] & 0x3f) == GPMODE_CDROM_AUDIO_PAGE)
            {
                Array.Copy(b, pos + 2, mode_pages_in[GPMODE_CDROM_AUDIO_PAGE], 0, 0xe);
                page_flags[GPMODE_CDROM_AUDIO_PAGE] |= PAGE_CHANGED;
            }
        }
        return true;
    }
}
