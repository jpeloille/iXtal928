// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — le code propre au mode matériel de l'horloge-octet de la disquette (G13 ; R10 de TRANSCRIPTION.md).
// STATUS: materiel
//
// Les corrections des défauts de PCem dans disc.c, appelées par les gardes de disc.cs (`if (materiel.pb_nn)`), seules
// sur leur ligne. La source, le cas qui discrimine et la panne qui le rougit sont dans l'entrée PB de PCEM_BUGS.md.

using static iXtal26.timer;

namespace iXtal26.Disc;

internal static partial class disc
{
    // pcem bug, fixed in hardware mode: PB-14 — à 300 kbit/s (IBM PC AT Technical Reference, 1502494, « Fixed Disk and
    //   Diskette Drive Adapter », Data Rates), l'octet dure 8 / 0,3 = 80/3 µs. PCem range 26 µs dans son entier
    //   disc_period ; disc_poll vient d'en avancer l'horloge, la fraction de 2/3 µs s'y ajoute ici, au format 32:32 de
    //   TIMER_USEC. Seul le cas 1 de disc_set_rate pose 26, et le cas 2 ne l'écrase plus en mode matériel.
    private static void disc_poll_materiel()
    {
        if (disc_period == 26)
        {
                timer_advance_u64(disc_poll_timer, 80 * TIMER_USEC / 3 - 26 * TIMER_USEC);
                ModeMateriel.Sonde[14]++;
        }
    }
}
