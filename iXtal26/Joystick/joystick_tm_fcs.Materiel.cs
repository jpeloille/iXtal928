// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — le code propre au mode matériel de joystick_tm_fcs.c (G13 ; R10 de TRANSCRIPTION.md).
// STATUS: materiel
//
// La correction de PB-103, appelée par la garde de joystick_tm_fcs.cs (`if (materiel.pb_103)`), seule sur sa
// ligne. La source, le cas qui discrimine et la panne qui le rougit sont dans l'entrée PB de PCEM_BUGS.md.

namespace iXtal26.Joystick;

internal static partial class joystick_tm_fcs_c
{
    // pcem bug, fixed in hardware mode: PB-103 — le vrai chapeau n'a que quatre directions ; le haut-gauche de l'hôte,
    //   315°, se lit comme la direction suivante dans le sens des aiguilles d'une montre, le haut : la borne « >= 315 »,
    //   la convention que suivent les trois autres (45°, 135°, 225°, chacune à la direction suivante).
    private static bool tm_haut_materiel()
    {
        if (plat_joystick.joystick_state[0].pov[0] != 315)
                return false;
        ModeMateriel.Sonde[103]++;
        return true;
    }
}
