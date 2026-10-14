// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — le code propre au mode matériel de la vidéo du PC1512 (G13 ; R10 de TRANSCRIPTION.md).
// STATUS: materiel
//
// Les corrections des défauts de PCem dans vid_pc1512.c, appelées par les gardes de vid_pc1512.cs
// (`if (materiel.pb_nn)`), seules sur leur ligne. La source, le cas qui discrimine et la panne qui le rougit sont dans l'entrée PB de
// PCEM_BUGS.md. Les méthodes portent NoInlining (M2, G13.7c2).

using System.Runtime.CompilerServices;
using static iXtal26.Video.video;
using static iXtal26.timer;

namespace iXtal26.Video;

internal static partial class vid_pc1512
{
    /// <summary>PB-89 : le balayage du PC1512, la ligne bornée à elle-même (LigneBornee) ; celui de PCem seul hors du
    /// rendu d'une ligne (linepos à 1) ou sous un R1 qui ne peut pas sortir de la ligne.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void poll_materiel(object? p)
    {
        pc1512_t pc1512 = (pc1512_t)p!;
        if (pc1512.linepos != 0 || !LigneBornee.deborde(pc1512.crtc[1]))
        {
                pc1512_poll(p);
                return;
        }
        LigneBornee.bornee(pc1512_poll, p, pc1512.displine);
    }

    /// <summary>PB-89 : le balayage du mode matériel (poll_materiel) sur la minuterie de la carte.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ligne_init_materiel(pc1512_t pc1512) => timer_set_callback(pc1512.timer, poll_materiel);
}
