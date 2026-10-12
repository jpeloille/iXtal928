// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — le code propre au mode matériel du contrôleur de disquette (G13 ; R10 de TRANSCRIPTION.md).
// STATUS: materiel
//
// Les corrections des défauts de PCem dans fdc.c, appelées par les gardes de fdc.cs (`if (materiel.pb_nn)`), seules
// sur leur ligne. La source, le cas qui discrimine et la panne qui le rougit sont dans l'entrée PB de PCEM_BUGS.md.

namespace iXtal26.Floppy;

internal static partial class fdc_c
{
    // pcem bug, fixed in hardware mode: PB-19 — en écriture hors DMA, le µPD765 attend chaque octet dans son temps
    //   (« every 31 µs in the FM mode, and every 15 µs in the MFM mode » au débit du 8 pouces, NEC µPD765 Data Sheet,
    //   déc. 1978, WRITE DATA), faute de quoi il pose OR dans ST1 et termine la commande. Le disque prend ici l'octet
    //   suivant (fdc_getdata) : si la demande que le contrôleur a faite à l'UC court encore (RQM et NDM, sans DIO :
    //   l'UC n'a pas écrit le registre de données depuis), l'octet est perdu. Le chemin de la FIFO (82077) n'est pas
    //   celui du µPD765 : il reste celui de PCem. Le premier octet, que PCem ne demande pas hors du PCjr, n'est pas
    //   jugé : la demande n'existe pas. WRITE DATA seulement : FORMAT TRACK prend aussi ses octets par fdc_getdata, mais
    //   au pas de 8 µs du chronomètre du contrôleur, qui n'est pas celui du disque — il reste celui de PCem.
    private static bool fdc_ecrasement_materiel()
    {
        if ((fdc.command & 0x1f) == 0x05 && (fdc.pcjr != 0 || fdc.fifo_enabled == 0) &&
            (fdc.msr & (MSR_RQM | MSR_DIO | MSR_NDM)) == (MSR_RQM | MSR_NDM))
        {
                ModeMateriel.Sonde[19]++;
                fdc_overrun();
                return true;
        }
        return false;
    }
}
