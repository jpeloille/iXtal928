// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — le code propre au mode matériel des Amstrad (G13 ; R10 de TRANSCRIPTION.md).
// STATUS: materiel
//
// Les corrections des défauts de PCem dans amstrad.c, appelées par les gardes d'amstrad.cs (`if (materiel.pb_nn)`),
// seules sur leur ligne. La source, le cas qui discrimine et la panne qui le rougit sont dans l'entrée PB de
// PCEM_BUGS.md.

namespace iXtal26.Models;

internal static partial class amstrad
{
    // pcem bug, fixed in hardware mode: PB-101 — le PC1512 n'a qu'un port parallèle, en 378h-37Ah (Amstrad PC1512
    //   Technical Reference Manual, section 1, § 1.3, 1.4 et 1.10) : le LPT2 que lpt_init a posé à 278h se retire, là
    //   où il est.
    private static void amstrad_lpt2_materiel()
    {
        Lpt.lpt.lpt2_remove();
        ModeMateriel.Sonde[101]++;
    }
}
