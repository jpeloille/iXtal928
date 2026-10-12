// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — le code propre au mode matériel du lecteur ZIP (G13 ; R10 de TRANSCRIPTION.md).
// STATUS: materiel
//
// Les corrections des défauts de PCem dans scsi_zip.c, appelées par les gardes de scsi_zip.cs (`if (materiel.pb_nn)`),
// seules sur leur ligne. La source, le cas qui discrimine et la panne qui le rougit sont dans l'entrée PB de
// PCEM_BUGS.md. Les références : SFF-8070i révision 1.3 (5 juillet 2001), ATAPI Removable Rewritable Media, la norme
// des lecteurs ATAPI amovibles comme le ZIP 100 ; SCSI-2 (X3.131-1994), chapitre 9. Les méthodes portent NoInlining :
// inlinées dans les branches mortes des gardes, elles mangeraient le budget d'inlining de scsi_zip_command (M2, G13.7c2).

// CS8981 : `scsi_zip_c` porte le nom de l'unité C (scsi_zip.cs).
#pragma warning disable CS8981

using System.Runtime.CompilerServices;
using static iXtal26.Scsi.scsi;

namespace iXtal26.Scsi;

internal static partial class scsi_zip_c
{
    // L'état que PCem n'a pas, dans une classe à part : un champ de plus dans scsi_zip_data, ou un champ statique de
    // plus dans scsi_zip_c (son constructeur statique), changerait le code machine du mode PCem.
    private static class EtatZip
    {
        /// <summary>Les octets au-delà des tampons de PCem : la suite de data_in, la suite de data_out.</summary>
        internal sealed class Extension
        {
            internal uint8_t[] entree = [];
            internal uint8_t[] sortie = [];
        }

        internal static readonly ConditionalWeakTable<scsi_zip_data, Extension> Extensions = new();

        /// <summary>Un reset matériel a eu lieu : l'attention d'unité 29h attend la prochaine commande qui vérifie
        /// le lecteur.</summary>
        internal static readonly ConditionalWeakTable<scsi_zip_data, StrongBox<bool>> Reset = new();
    }

    private const int ASC_POWER_ON_RESET_MATERIEL = 0x29;

    private static void Grandir(ref uint8_t[] t, int n)
    {
        if (t.Length < n)
            Array.Resize(ref t, Math.Max(n, 2 * t.Length));
    }

    // pcem bug, fixed in hardware mode: PB-125 — READ(10) transfère la longueur demandée, jusqu'à 65 535 blocs (SCSI-2
    //   § 9.2.6 ; SFF-8070i r1.3, READ(10), Table 37) : rien ne borne un transfert à 512 Ko. Au-delà des deux tampons
    //   contigus de PCem (data_in puis data_out, 2 × 256 Ko), la suite de data_in va dans une extension, hors de la
    //   classe, et se relit de là.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int extension_ecrire_materiel(scsi_zip_data data, uint8_t val)
    {
        ModeMateriel.Sonde[125]++;
        var e = EtatZip.Extensions.GetOrCreateValue(data);
        var i = data.data_pos_write++ - 2 * BUFFER_SIZE;
        Grandir(ref e.entree, i + 1);
        e.entree[i] = val;
        return 0;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static uint8_t extension_lire_materiel(scsi_zip_data data)
    {
        var e = EtatZip.Extensions.GetOrCreateValue(data);
        var i = data.data_pos_read++ - 2 * BUFFER_SIZE;
        return i < e.entree.Length ? e.entree[i] : (uint8_t)0;
    }

    // pcem bug, fixed in hardware mode: PB-125 — WRITE(10) de même (SCSI-2 § 9.2.21) : au-delà de data_out (256 Ko), la
    //   suite va dans l'extension, et l'écriture des secteurs l'y relit.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool sortie_ecrire_materiel(scsi_zip_data data, uint8_t val)
    {
        ModeMateriel.Sonde[125]++;
        var e = EtatZip.Extensions.GetOrCreateValue(data);
        var i = data.data_pos_write++ - BUFFER_SIZE;
        Grandir(ref e.sortie, i + 1);
        e.sortie[i] = val;
        data.bytes_received++;
        return true;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int sortie_lire_materiel(scsi_zip_data data)
    {
        var e = EtatZip.Extensions.GetOrCreateValue(data);
        var i = data.data_pos_read++ - BUFFER_SIZE;
        return i < e.sortie.Length ? e.sortie[i] : 0;
    }

    // pcem bug, fixed in hardware mode: PB-126 — READ CAPACITY rend l'adresse du dernier bloc (SFF-8070i r1.3, Table 41,
    //   « the last valid LBA ») : 196 607, et la taille du bloc, 512.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int capacite_materiel(scsi_zip_data data)
    {
        ModeMateriel.Sonde[126]++;
        const int dernier = ZIP_SECTORS - 1;
        foreach (var v in new[] { dernier >> 24, dernier >> 16, dernier >> 8, dernier, 0, 0, 512 >> 8, 512 & 0xff })
            scsi_add_data((uint8_t)v, data);
        data.cmd_pos = CMD_POS_IDLE;
        return BUS_IO;
    }

    // pcem bug, fixed in hardware mode: PB-126 — READ et WRITE (6 et 10) : « If a command is issued that requests access
    //   to a logical block not within the capacity of the medium, the command is terminated with CHECK CONDITION »
    //   (SCSI-2 § 9.1.2) — 05/21h, avant toute donnée (déduit : la commande est terminée) ; PCem lit au-delà de l'image
    //   sans erreur (le tampon d'avant) ou n'y lit rien (PB-125). Une longueur nulle de READ(10) ou WRITE(10) : ni
    //   transfert ni erreur (SCSI-2 § 9.2.6, § 9.2.21) — GOOD sans phase de données ; PCem entre dans une phase qui ne
    //   finit pas. READ(6) et WRITE(6) comptent 0 pour 256 blocs, comme PCem.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int plage_materiel(scsi_zip_data data, uint8_t[] cdb)
    {
        if (data.len == 0)
        {
            ModeMateriel.Sonde[126]++;
            data.cmd_pos = CMD_POS_IDLE;
            return BUS_CD | BUS_IO;
        }
        if ((uint)data.addr + (long)data.len <= ZIP_SECTORS)
            return -1;
        ModeMateriel.Sonde[126]++;
        scsi_zip_cmd_error(data, KEY_ILLEGAL_REQ, ASC_LBA_OUT_OF_RANGE, 0);
        data.cmd_pos = CMD_POS_IDLE;
        return BUS_CD | BUS_IO;
    }

    // pcem bug, fixed in hardware mode: PB-126 — MODE SELECT(6) de longueur 0 : « A parameter list length of zero
    //   indicates that no data shall be transferred. This condition shall not be considered as an error » (SFF-8070i
    //   r1.3, MODE SELECT ; SCSI-2 § 8.2.8) — GOOD sans phase de données.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int mode_select_vide_materiel(scsi_zip_data data, uint8_t[] cdb)
    {
        if (cdb[4] != 0)
            return -1;
        ModeMateriel.Sonde[126]++;
        data.cmd_pos = CMD_POS_IDLE;
        return BUS_CD | BUS_IO;
    }

    // pcem bug, fixed in hardware mode: PB-126 — un reset matériel ne retire pas le disque d'un vrai lecteur : le lecteur
    //   neuf reprend celui de l'ancien (son fichier, la protection en écriture), puis rend l'attention d'unité « POWER
    //   ON, RESET OR BUS DEVICE RESET OCCURRED » (06/29h, SFF-8070i r1.3, § 9.6 et Table 61) à la première commande —
    //   lecteur — sauf INQUIRY (§ 9.6) ; la garde du disque, déduite ; elle recouvre l'attention du disque changé (28h)
    //   que PCem poserait aussi : une seule est rendue (si un disque est chargé entre le reset et la première commande,
    //   son 28h est recouvert aussi).
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void reprise_materiel(scsi_zip_data data)
    {
        var ancien = zip_data;
        if (ancien is null || ancien.disc_loaded == 0)
            return;
        ModeMateriel.Sonde[126]++;
        data.hdd = ancien.hdd;
        data.disc_loaded = 1;
        data.read_only = ancien.read_only;
        EtatZip.Reset.GetOrCreateValue(data).Value = true;
    }

    //   SFF-8070i r1.3 § 9.6 : l'attention vaut pour toute commande sauf INQUIRY, qui s'exécute et la laisse ; REQUEST
    //   SENSE la rapporte (06/29h dans ses données, sans CHECK CONDITION) et l'efface.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int attention_reset_materiel(scsi_zip_data data, uint8_t[] cdb)
    {
        if (cdb[0] == SCSI_INQUIRY || !EtatZip.Reset.TryGetValue(data, out var r) || !r.Value)
            return -1;
        r.Value = false;
        data.disc_changed = 0;
        if (cdb[0] == SCSI_REQUEST_SENSE)
        {
            data.sense_key = KEY_UNIT_ATTENTION;
            data.asc = ASC_POWER_ON_RESET_MATERIEL;
            data.ascq = 0;
            return -1;
        }
        scsi_zip_cmd_error(data, KEY_UNIT_ATTENTION, ASC_POWER_ON_RESET_MATERIEL, 0);
        return BUS_CD | BUS_IO;
    }
}
