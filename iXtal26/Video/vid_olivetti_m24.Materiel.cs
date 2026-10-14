// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — le code propre au mode matériel de la vidéo de la M24 (G13 ; R10 de TRANSCRIPTION.md).
// STATUS: materiel
//
// Les corrections des défauts de PCem dans vid_olivetti_m24.c, appelées par les gardes de vid_olivetti_m24.cs
// (`if (materiel.pb_nn)`), seules sur leur ligne. La source, le cas qui discrimine et la panne qui le rougit sont dans l'entrée PB de
// PCEM_BUGS.md. Les méthodes portent NoInlining (M2, G13.7c2).

using System.Runtime.CompilerServices;
using static iXtal26.Video.video;
using static iXtal26.timer;

namespace iXtal26.Video;

internal static partial class vid_olivetti_m24
{
    /// <summary>PB-89 : le balayage de la M24, la ligne bornée à elle-même (LigneBornee) ; celui de PCem seul hors du
    /// rendu d'une ligne (linepos à 1) ou sous un R1 qui ne peut pas sortir de la ligne.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void poll_materiel(object? p)
    {
        m24_t m24 = (m24_t)p!;
        if (m24.linepos != 0 || !LigneBornee.deborde(m24.crtc[1]))
        {
                m24_poll(p);
                return;
        }
        LigneBornee.bornee(m24_poll, p, m24.displine);
    }

    /// <summary>PB-89 : le balayage du mode matériel (poll_materiel) sur la minuterie de la carte.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ligne_init_materiel(m24_t m24) => timer_set_callback(m24.timer, poll_materiel);
}
