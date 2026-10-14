// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — le code propre au mode matériel de la Tseng ET4000AX (G13 ; R10 de TRANSCRIPTION.md).
// STATUS: materiel
//
// Les corrections des défauts de PCem dans vid_et4000.c, appelées par les gardes de vid_et4000.cs
// (`if (materiel.pb_nn)`), seules sur leur ligne. La source, le cas qui discrimine et la panne qui le rougit sont dans
// l'entrée PB de PCEM_BUGS.md. La référence : Tseng Labs, data book ET4000 Graphics Controller (1990), p. 111 (la table
// 4.3-2 du CRTC). Les méthodes portent NoInlining (M2, G13.7c2).

using System.Runtime.CompilerServices;

namespace iXtal26.Video;

internal static partial class vid_et4000
{
    /// <summary>PB-227 : CR35, le débordement vertical, est « protected by bit 7 of CRTC 11 » (table 4.3-2) : sous CR11
    /// bit 7, son écriture est ignorée. Vrai : l'écriture est ignorée.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool cr35_materiel(svga_t svga)
    {
        if (svga.crtcreg != 0x35 || (svga.crtc[0x11] & 0x80) == 0)
                return false;
        ModeMateriel.Sonde[227]++;
        return true;
    }

    /// <summary>PB-100 : CR13 nul vaut 0 (le décalage d'une rangée, data book Tseng) : la même rangée répétée,
    /// et non 256.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void cr13_materiel(svga_t svga)
    {
        if (svga.crtc[0x13] != 0)
                return;
        ModeMateriel.Sonde[100]++;
        svga.rowoffset = 0;
    }
}
