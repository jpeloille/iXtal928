// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — le code propre au mode matériel du Fixed Disk and Diskette Drive Adapter de l'AT (G13 ; R10 de
// TRANSCRIPTION.md).
// STATUS: materiel
//
// Les corrections des défauts de PCem dans mfm_at.c, appelées par les gardes de mfm_at.cs (`if (materiel.pb_nn)`),
// seules sur leur ligne. La source, le cas qui discrimine et la panne qui le rougit sont dans l'entrée PB de
// PCEM_BUGS.md.

namespace iXtal26.Mfm;

internal static partial class mfm_at
{
    // pcem bug, fixed in hardware mode: PB-25 — les têtes se numérotent depuis 0 (Drive/Head, 1F6h, bits 0-3) : la tête
    //   égale au nombre de têtes, celui de SET PARAMETERS ou celui de l'image, n'existe pas ; aucun champ ID ne s'y
    //   présente, et mfm_at rend IDNF (10h) pour tout refus d'adresse (déduit, PCEM_BUGS.md). PCem la laisse passer (`>`)
    //   et lit la tête 0 du cylindre suivant.
    private static bool mfm_tete_materiel(mfm_t mfm, mfm_drive_t drive)
    {
        if (mfm.head < drive.cfg_hpc && mfm.head < drive.hdd_file.hpc)
                return false;
        ModeMateriel.Sonde[25]++;
        return true;
    }
}
