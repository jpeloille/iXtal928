// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — le code propre au mode matériel du disque SCSI (G13 ; R10 de TRANSCRIPTION.md).
// STATUS: materiel
//
// Les corrections des défauts de PCem dans scsi_hd.c, appelées par les gardes de scsi_hd.cs (`if (materiel.pb_nn)`),
// seules sur leur ligne. La source, le cas qui discrimine et la panne qui le rougit sont dans l'entrée PB de
// PCEM_BUGS.md. La référence : SCSI-2 (X3.131-1994) — chapitre 7 (l'état, la sense), chapitre 8 (REQUEST SENSE,
// INQUIRY), chapitre 9 (les disques). Les méthodes portent NoInlining (M2, G13.7c2) ; aucune garde dans scsi_add_data
// ni scsi_get_data, petites méthodes inlinées à chaque octet (M2, G13.7d1).

// CS8981 : `scsi_hd_c` porte le nom de l'unité C (scsi_hd.cs).
#pragma warning disable CS8981

using System.Runtime.CompilerServices;
using iXtal26.Disc;
using static iXtal26.Scsi.scsi;

namespace iXtal26.Scsi;

internal static partial class scsi_hd_c
{
    // L'état que PCem n'a pas, dans une classe à part (un champ statique de plus dans scsi_hd_c changerait son
    // constructeur statique).
    private static class EtatHd
    {
        internal sealed class Extension
        {
            internal uint8_t[] entree = [];
            internal uint8_t[] sortie = [];
        }

        internal static readonly ConditionalWeakTable<scsi_hd_data, Extension> Extensions = new();
    }

    private static void Grandir(ref uint8_t[] t, int n)
    {
        if (t.Length < n)
            Array.Resize(ref t, Math.Max(n, 2 * t.Length));
    }

    // pcem bug, fixed in hardware mode: PB-128 — READ(10) et WRITE(10) transfèrent la longueur demandée, jusqu'à
    //   65 535 blocs (SCSI-2 § 9.2.6, § 9.2.21) ; la carte transfère ce que la cible rend, dans la limite du CCB. Au-delà
    //   des tampons de PCem (2 × 256 Ko pour data_in, 256 Ko pour data_out), le mode matériel prend en charge tout le
    //   transfert : la lecture remplit io puis une extension, hors de la classe, que scsi_hd_read relit ; l'écriture lit
    //   ce que scsi_hd_write a rangé dans io et dans l'extension. En deçà, la voie de PCem, juste.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int lecture_complete_materiel(scsi_hd_data data)
    {
        if ((long)data.len * 512 <= 2 * BUFFER_SIZE)
            return -1;
        ModeMateriel.Sonde[128]++;
        var e = EtatHd.Extensions.GetOrCreateValue(data);
        var pos = 0;
        for (; data.len != 0; data.len--, data.addr++)
        {
            hdd_file.hdd_read_sectors(data.hdd, data.addr, 1, data.buf);
            pc.readflash_set(pc.READFLASH_HDC, data.hd_id);
            for (var k = 0; k < 512; k++, pos++)
            {
                if (pos < 2 * BUFFER_SIZE)
                    data.io[pos] = data.buf[k];
                else
                {
                    Grandir(ref e.entree, pos - 2 * BUFFER_SIZE + 1);
                    e.entree[pos - 2 * BUFFER_SIZE] = data.buf[k];
                }
            }
        }
        data.data_pos_write = pos;
        data.sector_pos = 0;
        data.cmd_pos = CMD_POS_IDLE;
        return BUS_IO;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static uint8_t extension_lire_materiel(scsi_hd_data data)
    {
        var e = EtatHd.Extensions.GetOrCreateValue(data);
        var i = data.data_pos_read++ - 2 * BUFFER_SIZE;
        return i < e.entree.Length ? e.entree[i] : (uint8_t)0;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool sortie_ecrire_materiel(scsi_hd_data data, uint8_t val)
    {
        var e = EtatHd.Extensions.GetOrCreateValue(data);
        var i = data.data_pos_write++ - BUFFER_SIZE;
        if (i == 0)
            ModeMateriel.Sonde[128]++;
        Grandir(ref e.sortie, i + 1);
        e.sortie[i] = val;
        data.bytes_received++;
        return true;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int ecriture_complete_materiel(scsi_hd_data data)
    {
        if (data.bytes_required <= BUFFER_SIZE)
            return -1;
        ModeMateriel.Sonde[128]++;
        var e = EtatHd.Extensions.GetOrCreateValue(data);
        var pos = 0;
        for (; data.len != 0; data.len--, data.addr++)
        {
            for (var k = 0; k < 512; k++, pos++)
                data.buf[k] = pos < BUFFER_SIZE ? data.io[BUFFER_SIZE + pos]
                                                : pos - BUFFER_SIZE < e.sortie.Length ? e.sortie[pos - BUFFER_SIZE] : (uint8_t)0;
            hdd_file.hdd_write_sectors(data.hdd, data.addr, 1, data.buf);
            pc.readflash_set(pc.READFLASH_HDC, data.hd_id);
        }
        data.data_pos_read = pos;
        data.sector_pos = 0;
        data.cmd_pos = CMD_POS_IDLE;
        return BUS_CD | BUS_IO;
    }

    // pcem bug, fixed in hardware mode: PB-129 — READ CAPACITY rend l'adresse du dernier bloc (SCSI-2 § 9.2.7) : le
    //   nombre de secteurs moins un, et la taille du bloc, 512.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int capacite_materiel(scsi_hd_data data)
    {
        ModeMateriel.Sonde[129]++;
        var dernier = data.hdd.sectors - 1;
        foreach (var v in new[] { dernier >> 24, dernier >> 16, dernier >> 8, dernier, 0, 0, 512 >> 8, 512 & 0xff })
            scsi_add_data((uint8_t)v, data);
        data.cmd_pos = CMD_POS_IDLE;
        return BUS_IO;
    }

    private static int hors_disque(scsi_hd_data data, long addr, long n)
    {
        if (addr + n <= data.hdd.sectors)
            return -1;
        ModeMateriel.Sonde[129]++;
        data.status = STATUS_CHECK_CONDITION;
        data.sense_key = KEY_ILLEGAL_REQ;
        data.asc = ASC_LBA_OUT_OF_RANGE;
        data.ascq = 0;
        data.cmd_pos = CMD_POS_IDLE;
        return BUS_CD | BUS_IO;
    }

    // pcem bug, fixed in hardware mode: PB-129 — « If a command is issued that requests access to a logical block not
    //   within the capacity of the medium, the command is terminated with CHECK CONDITION » (SCSI-2 § 9.1.2), ILLEGAL
    //   REQUEST, LOGICAL BLOCK ADDRESS OUT OF RANGE (05/21h, § 8.2.14) ; avant toute donnée (déduit). READ, WRITE (6 et
    //   10) et VERIFY(10) ; PCem les sert sans erreur (le tampon d'avant) ou n'écrit rien.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int plage_materiel(scsi_hd_data data) => hors_disque(data, (uint)data.addr, data.len);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int verify_plage_materiel(scsi_hd_data data, uint8_t[] cdb)
    {
        var addr = (uint)(cdb[2] << 24 | cdb[3] << 16 | cdb[4] << 8 | cdb[5]);
        var n = cdb[7] << 8 | cdb[8];
        return n == 0 ? -1 : hors_disque(data, addr, n);
    }

    // pcem bug, fixed in hardware mode: PB-132 — une longueur nulle : « no data shall be transferred. This condition
    //   shall not be considered an error » (SCSI-2 § 9.2.6 READ(10) ; § 8.2.14 REQUEST SENSE, § 8.2.5 INQUIRY, § 8.2.10
    //   MODE SENSE(6), § 8.2.8 MODE SELECT(6)) : la cible passe au statut, GOOD, sans phase de données. PCem y entre, et
    //   n'en sort plus. WRITE(10) de compte 0, PCem le finit déjà en GOOD sans données ; READ(6) et WRITE(6) comptent 0
    //   pour 256, comme PCem. REQUEST SENSE efface la sense même sans la transférer.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int allocation_nulle_materiel(scsi_hd_data data, uint8_t[] cdb)
    {
        var n = cdb[0] == SCSI_INQUIRY ? cdb[4] | (cdb[3] << 8) : cdb[4];
        if (n != 0)
            return -1;
        ModeMateriel.Sonde[132]++;
        if (cdb[0] == SCSI_REQUEST_SENSE)
            data.sense_key = data.asc = data.ascq = 0;
        data.cmd_pos = CMD_POS_IDLE;
        return BUS_CD | BUS_IO;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int longueur_nulle_materiel(scsi_hd_data data)
    {
        if (data.len != 0)
            return -1;
        ModeMateriel.Sonde[132]++;
        data.cmd_pos = CMD_POS_IDLE;
        return BUS_CD | BUS_IO;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int mode_select_vide_materiel(scsi_hd_data data, uint8_t[] cdb)
    {
        if (cdb[4] != 0)
            return -1;
        ModeMateriel.Sonde[132]++;
        data.cmd_pos = CMD_POS_IDLE;
        return BUS_CD | BUS_IO;
    }

    // pcem bug, fixed in hardware mode: PB-131 — la sense est « cleared upon receipt of any subsequent I/O process
    //   (including REQUEST SENSE) » (SCSI-2 § 8.2.14 ; § 7.6, la condition d'allégeance) : effacée au début de toute
    //   commande autre que REQUEST SENSE, qui la rapporte puis l'efface. Le disque ne pose aucune attention d'unité.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void sense_suivante_materiel(scsi_hd_data data, uint8_t[] cdb)
    {
        if (cdb[0] == SCSI_REQUEST_SENSE || data.sense_key == 0 && data.asc == 0)
            return;
        ModeMateriel.Sonde[131]++;
        data.sense_key = data.asc = data.ascq = 0;
    }

    // pcem bug, fixed in hardware mode: PB-131 — REQUEST SENSE au format fixe de SCSI-2 (§ 8.2.14, Table 65) : 70h, la clé,
    //   la longueur additionnelle n − 7 = 0Ah, l'ASC et l'ASCQ, sur 18 octets coupés à l'allocation. Le bit Valid à 0 :
    //   le champ d'information porte « the unsigned logical block address associated with the sense key », mais pour
    //   une plage refusée la norme ne dit pas laquelle (la première demandée, la première hors du disque) — déduit, le
    //   champ laissé non valide, ce que le bit permet. L'octet 1, bit 0 (le format descripteur de SPC-3) est réservé en
    //   SCSI-2 : ignoré. La sense effacée.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int request_sense_materiel(scsi_hd_data data, uint8_t[] cdb)
    {
        ModeMateriel.Sonde[131]++;
        var s = new uint8_t[18];
        s[0] = 0x70;
        s[2] = (uint8_t)data.sense_key;
        s[7] = 0x0A;
        s[12] = (uint8_t)data.asc;
        s[13] = (uint8_t)data.ascq;
        for (var k = 0; k < Math.Min(cdb[4], s.Length); k++)
            scsi_add_data(s[k], data);
        data.sense_key = data.asc = data.ascq = 0;
        data.cmd_pos = CMD_POS_IDLE;
        return BUS_IO;
    }

    // pcem bug, fixed in hardware mode: PB-131 — INQUIRY (SCSI-2 § 8.2.5.1, Tables 45 et 48) : la version 2 (ANSI
    //   X3.131-1994), le format de réponse 2, la longueur additionnelle n − 4 = 5Bh pour 96 octets, CmdQue à 0 (la
    //   cible ne met pas les commandes en file) ; les octets que PCem a déjà rangés, corrigés en place.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void inquiry_materiel(scsi_hd_data data)
    {
        ModeMateriel.Sonde[131]++;
        if (data.data_pos_write > 2)
            data.io[2] = 2;
        if (data.data_pos_write > 4)
            data.io[4] = 0x5B;
        if (data.data_pos_write > 7)
            data.io[7] = 0;
    }

    // pcem bug, fixed in hardware mode: PB-131 — EVPD : la cible n'a pas de pages de données vitales ; « If the target
    //   does not support vital product data and this bit is set to one, the target shall return CHECK CONDITION status
    //   with the sense key set to ILLEGAL REQUEST and an additional sense code of INVALID FIELD IN CDB » ; et « If the
    //   EVPD bit is zero and the page code field is not zero » de même (SCSI-2 § 8.2.5). Avant l'allocation nulle : le
    //   refus ne dépend pas de la longueur.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int inquiry_evpd_materiel(scsi_hd_data data, uint8_t[] cdb)
    {
        if ((cdb[1] & 1) == 0 && cdb[2] == 0)
            return -1;
        return refus_materiel(data, ASC_INV_FIELD_IN_CMD_PACKET);
    }

    // pcem bug, fixed in hardware mode: PB-131 — un code inconnu : INVALID COMMAND OPERATION CODE (05/20h) ; un champ
    //   de la commande refusé : INVALID FIELD IN CDB (05/24h) ; PCem rend LOGICAL UNIT NOT SUPPORTED (05/25h) partout. Le
    //   refus d'un LUN non nul, avant le switch, garde son 05/25h : son sort est celui de PB-133 et de PB-219.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int refus_materiel(scsi_hd_data data, int asc)
    {
        ModeMateriel.Sonde[131]++;
        data.status = STATUS_CHECK_CONDITION;
        data.sense_key = KEY_ILLEGAL_REQ;
        data.asc = asc;
        data.ascq = 0;
        data.cmd_pos = CMD_POS_IDLE;
        return BUS_CD | BUS_IO;
    }

    // pcem bug, fixed in hardware mode: PB-131 — SEND DIAGNOSTIC sans autotest et sans liste : « A parameter list length
    //   of zero indicates that no data shall be transferred. This condition shall not be considered an error » (SCSI-2
    //   § 8.2.15) ; aucune opération demandée : GOOD. Une liste non vide, l'opération propre au fabricant : PB-275.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int diagnostic_vide_materiel(scsi_hd_data data, uint8_t[] cdb)
    {
        if ((cdb[3] << 8 | cdb[4]) != 0)
            return -1;
        ModeMateriel.Sonde[131]++;
        data.cmd_pos = CMD_POS_IDLE;
        return BUS_CD | BUS_IO;
    }
}
