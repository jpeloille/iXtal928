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

    // pcem bug, fixed in hardware mode: PB-270 — le jeu de commandes d'un lecteur ATAPI dépend de sa génération (Q5 :
    //   selon l'année du modèle). SFF-8020i r2.6 (janvier 1996), Table 37, et MMC-2 (T10/1228-D r11a, août 1999),
    //   Table B.2, listent les commandes d'un lecteur de CD ATAPI ; une commande absente de la liste de sa génération
    //   rend ILLEGAL REQUEST, INVALID COMMAND OPERATION CODE (05/20h ; SFF-8020i, Table 87) — déduit de l'absence.
    //   Aucune des deux n'a REZERO UNIT, READ(6), SEEK(6), MODE SELECT(6) ni MODE SENSE(6) : refusés dans les deux
    //   générations. SFF-8020i n'a ni GET EVENT STATUS NOTIFICATION, ni PLAY AUDIO(12), ni READ DISC INFORMATION :
    //   refusés par les modèles de cette génération. MMC-2 rend le premier obligatoire ; les deux autres, elle les
    //   liste pour les DVD (« D ») et les graveurs (« E, R, K ») — qu'un lecteur de CD-ROM de 1999 les ait est inconnu :
    //   servis, comme chez PCem. Les modèles de PCem rangés par leur année (PCEM_BUGS.md, PB-270) : MMC-2 pour le 40x
    //   de Philips et le 48x de Mitsumi (1999, déduit de la vitesse), le 72x de Kenwood (2000, documenté) et, par
    //   choix, le lecteur générique de PCem, qui n'a pas d'année ; SFF-8020i pour les autres, du 4x d'Aztech au 36x de
    //   BTC (1996, documenté pour trois ; jusqu'à 1998, déduit). Sur ATAPI seulement : derrière une carte SCSI, le même
    //   lecteur sert le jeu de SCSI-2, où ces commandes de six octets existent. Une attention d'unité en attente passe
    //   devant (la suite de PCem la rend, puis GET EVENT STATUS NOTIFICATION est refusé dans son cas).
    private static bool mmc2_materiel(scsi_cd_data_t data) =>
        cd_models[data.cur_model].model_config_string_40 is "pcemcd" or "pca403cd" or "crmc-fx4820t" or "ucr421";

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool commande_absente_materiel(scsi_cd_data_t data, uint8_t[] cdb)
    {
        if (data.is_atapi == 0 || data.sense_key == SENSE_UNIT_ATTENTION)
            return false;
        var absente = cdb[0] switch
        {
            SCSI_REZERO_UNIT or GPCMD_READ_6 or SCSI_SEEK_6 or GPCMD_MODE_SELECT_6 or GPCMD_MODE_SENSE_6 => true,
            GPCMD_GET_EVENT_STATUS_NOTIFICATION or GPCMD_PLAY_AUDIO_12 or GPCMD_READ_DISC_INFORMATION => !mmc2_materiel(data),
            _ => false,
        };
        if (!absente)
            return false;
        ModeMateriel.Sonde[270]++;
        atapi_cmd_error(data, SENSE_ILLEGAL_REQUEST, ASC_ILLEGAL_OPCODE, 0);
        data.cmd_pos = CMD_POS_IDLE;
        return true;
    }

    // L'état que PCem n'a pas : la présence du disque au dernier GET EVENT STATUS NOTIFICATION, par lecteur, dans une
    // classe à part — un champ de plus dans scsi_cd_data_t, ou un champ statique de plus dans scsi_cd_c (son
    // constructeur statique), changerait le code machine du mode PCem.
    private static class EtatGesn
    {
        internal static readonly ConditionalWeakTable<scsi_cd_data_t, StrongBox<bool>> PresenceVue = new();
    }

    // pcem bug, fixed in hardware mode: PB-118 — GET EVENT STATUS NOTIFICATION selon MMC-2 r11a § 6.1.5 (Tables 162 à
    //   166, 178 à 180), sur un lecteur de cette génération (refusé sur les autres, PB-270, avec qui il fait groupe) : la
    //   longueur d'allocation aux octets 7 et 8 ; le mode asynchrone (IMMED = 0) refusé, « shall fail as an illegal
    //   request », l'ASC de PCem gardé (ni la r11a ni le brouillon T10/97-108r0 n'en nomment) ; la classe des supports (bit 4 de l'octet 4) seule
    //   servie : l'en-tête, la longueur des données qui suivent le champ de longueur (6) en ordre réseau, la classe 4,
    //   les classes servies (10h), puis l'événement — NewMedia (2) une fois quand un disque est apparu ou a changé,
    //   MediaRemoval (3) une fois quand il a disparu, NoEvent (0) sinon — et l'état (Media Present, bit 1 ; le plateau
    //   n'est pas modélisé). Une demande sans la classe des supports : l'en-tête seul, NEA et la classe 0
    //   (`00 02 80 10`). Sans effet de bord : PCem charge le disque (atapi->load()) à chaque appel.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int gesn_materiel(scsi_cd_data_t data, uint8_t[] cdb, int changed)
    {
        if (data.is_atapi != 0 && !mmc2_materiel(data))
        {
            ModeMateriel.Sonde[270]++;
            atapi_cmd_error(data, SENSE_ILLEGAL_REQUEST, ASC_ILLEGAL_OPCODE, 0);
            return SCSI_PHASE_STATUS;
        }
        ModeMateriel.Sonde[118]++;
        if ((cdb[1] & 0x01) == 0)
        {
            atapi_cmd_error(data, SENSE_ILLEGAL_REQUEST, ASC_ILLEGAL_OPCODE, 0);
            return SCSI_PHASE_STATUS;
        }
        var alloc_length = (cdb[7] << 8) | cdb[8];
        var b = data.data_in;
        int len;
        if ((cdb[4] & (1 << 4)) != 0)
        {
            var vu = EtatGesn.PresenceVue.GetValue(data, _ => new StrongBox<bool>(false));
            var present = atapi!.ready() != 0;
            var evenement = present && (changed != 0 || !vu.Value) ? 2 : !present && vu.Value ? 3 : 0;
            vu.Value = present;
            b[0] = 0;
            b[1] = 6;
            b[2] = 4;
            b[3] = 1 << 4;
            b[4] = (uint8_t)evenement;
            b[5] = (uint8_t)(present ? 0x02 : 0x00);
            b[6] = 0;
            b[7] = 0;
            len = 8;
        }
        else
        {
            b[0] = 0;
            b[1] = 2;
            b[2] = 0x80;
            b[3] = 1 << 4;
            len = 4;
        }
        data.bytes_expected = data.data_pos_write = Math.Min(alloc_length, len);
        data.cmd_pos = CMD_POS_IDLE;
        return alloc_length != 0 ? SCSI_PHASE_DATA_IN : SCSI_PHASE_STATUS;
    }

    // pcem bug, fixed in hardware mode: PB-271 — READ CD MSF (B9h), obligatoire dans SFF-8020i r2.6 (Table 37,
    //   § 10.8.16) comme dans MMC-2 r11a (Table B.2, « B » : tout lecteur de CD ; § 6.1.16) : le paquet traduit en READ CD
    //   (BEh), que PCem sert, avant toute autre étape — l'adresse de départ, le compte (la fin moins le départ : « A
    //   starting MSF address equal to an ending MSF address prevents a read operation »), les octets 1, 9 et 10 aux
    //   mêmes places. Un départ au-delà de la fin : ILLEGAL REQUEST (INVALID FIELD IN COMMAND PACKET, 24h, de la Table 106
    //   de SFF-8020i) ; une seconde au-delà de 59 ou une image au-delà de 74 : 24h aussi (déduit) ; un départ avant
    //   00:02:00 (le prégap de la piste 1, que le moteur ne sert pas) : LOGICAL BLOCK ADDRESS OUT OF RANGE, 21h (déduit).
    //   Le reste comme READ CD : l'octet 9 autre que 10h ou F8h rend 20h, l'ASC de PCem pour READ CD.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool read_cd_msf_materiel(scsi_cd_data_t data, uint8_t[] cdb)
    {
        if (cdb[0] != 0xB9)
            return false;
        ModeMateriel.Sonde[271]++;
        if (cdb[4] > 59 || cdb[5] > 74 || cdb[7] > 59 || cdb[8] > 74)
        {
            atapi_cmd_error(data, SENSE_ILLEGAL_REQUEST, ASC_INV_FIELD_IN_CMD_PACKET, 0);
            data.cmd_pos = CMD_POS_IDLE;
            return true;
        }
        var debut = cdb[3] * 4500 + cdb[4] * 75 + cdb[5] - 150;
        var fin = cdb[6] * 4500 + cdb[7] * 75 + cdb[8] - 150;
        if (debut > fin)
        {
            atapi_cmd_error(data, SENSE_ILLEGAL_REQUEST, ASC_INV_FIELD_IN_CMD_PACKET, 0);
            data.cmd_pos = CMD_POS_IDLE;
            return true;
        }
        if (debut < 0)
        {
            atapi_cmd_error(data, SENSE_ILLEGAL_REQUEST, ASC_LBA_OUT_OF_RANGE, 0);
            data.cmd_pos = CMD_POS_IDLE;
            return true;
        }
        var n = fin - debut;
        cdb[0] = GPCMD_READ_CD;
        cdb[2] = (uint8_t)(debut >> 24);
        cdb[3] = (uint8_t)(debut >> 16);
        cdb[4] = (uint8_t)(debut >> 8);
        cdb[5] = (uint8_t)debut;
        cdb[6] = (uint8_t)(n >> 16);
        cdb[7] = (uint8_t)(n >> 8);
        cdb[8] = (uint8_t)n;
        cdb[11] = 0;
        Array.Copy(cdb, data.cdb, CDB_MAX_LEN);
        return false;
    }

    // pcem bug, fixed in hardware mode: PB-272 — PLAY AUDIO(12) (MMC-2 r11a § 6.1.11, Table 209) : l'adresse aux octets 2
    //   à 5, la longueur aux octets 6 à 9, sur quatre octets chacune. Les autres PLAY AUDIO, tels quels.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int play_audio_12_materiel(uint8_t[] cdb, int valeur, int o)
    {
        if (cdb[0] != GPCMD_PLAY_AUDIO_12)
            return valeur;
        ModeMateriel.Sonde[272]++;
        return (cdb[o] << 24) | (cdb[o + 1] << 16) | (cdb[o + 2] << 8) | cdb[o + 3];
    }

    // pcem bug, fixed in hardware mode: PB-273 — « If the CD-ROM information type (data vs. audio) changes within the
    //   transfer length, the command shall be terminated with a CHECK CONDITION and the sense key shall be set to
    //   ILLEGAL REQUEST and the additional sense code set to END OF USER AREA ENCOUNTERED ON THIS TRACK » (SFF-8020i r2.6
    //   § 10.8.8 ; 05/63h, Table 73). La plage — du départ à la fin exclue, en LBA : le MSF moins 150 —
    //   jugée piste par piste dans le moteur d'images ; une plage vide, ou un autre pilote que celui des images, non
    //   jugés.
    private const int ASC_END_OF_USER_AREA_MATERIEL = 0x63;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool plage_mixte_materiel(scsi_cd_data_t data, uint8_t[] cdb, int pos, int len)
    {
        long debut, fin;
        if (cdb[0] == GPCMD_PLAY_AUDIO_MSF)
        {
            debut = ((pos >> 16 & 0xff) * 60 + (pos >> 8 & 0xff)) * 75 + (pos & 0xff) - 150;
            fin = ((len >> 16 & 0xff) * 60 + (len >> 8 & 0xff)) * 75 + (len & 0xff) - 150;
        }
        else
        {
            debut = (uint)pos;
            fin = debut + (uint)len;
        }
        if (!iXtal26.Cdrom.cdrom_image.plage_audio_materiel(debut, fin))
        {
            ModeMateriel.Sonde[273]++;
            atapi_cmd_error(data, SENSE_ILLEGAL_REQUEST, ASC_END_OF_USER_AREA_MATERIEL, 0);
            data.cmd_pos = CMD_POS_IDLE;
            return true;
        }
        return false;
    }
}
