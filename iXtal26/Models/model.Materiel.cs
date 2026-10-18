// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — le code propre au mode matériel des machines (G13 ; R10 de TRANSCRIPTION.md).
// STATUS: materiel
//
// Les corrections des défauts de PCem dans model.c, appelées par les gardes de model.cs (`if (materiel.pb_nn)`),
// seules sur leur ligne. La source, le cas qui discrimine et la panne qui le rougit sont dans l'entrée PB de
// PCEM_BUGS.md.

namespace iXtal26.Models;

internal static partial class model_c
{
    // pcem bug, fixed in hardware mode: PB-256 — la M24 et le PC1512 rafraîchissent leur mémoire comme le 5150 et
    //   l'XT : la sortie 1 du 8253 demande le canal 0 du DMA (Amstrad PC1512 Technical Reference Manual, section 1,
    //   § 1.5, 1.5.1 et 1.7.2, toutes les 15,13 µs ; la ROM 1.43 de la M24 pose le canal 0 en 58h et le compteur 1
    //   à 13h, F000:DC41-DC5A). Le branchement de xt_init (model.c:205) ; le coût d'un cycle, celui de PCem pour
    //   l'XT (refreshread : quatre cycles et l'achèvement du fetch), n'est pas documenté pour ces deux UC — un temps
    //   déduit.
    private static void model_rafraichissement_materiel()
    {
        pit.pit_set_out_func(pit.pit_, 1, pit.pit_refresh_timer_xt);
        ModeMateriel.Sonde[256]++;
    }
}
