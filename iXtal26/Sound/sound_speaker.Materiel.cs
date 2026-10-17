// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — le code propre au mode matériel du haut-parleur (G13 ; R10 de TRANSCRIPTION.md).
// STATUS: materiel
//
// Les corrections des défauts de PCem dans sound_speaker.c, appelées par les gardes de sound_speaker.cs
// (`if (materiel.pb_nn)`), seules sur leur ligne. La source, le cas qui discrimine et la panne qui le rougit sont dans
// l'entrée PB de PCEM_BUGS.md.

using iXtal26.Models;

namespace iXtal26.Sound;

internal static partial class sound_speaker
{
    // pcem bug, fixed in hardware mode: PB-293 — hors des modes 0 et 4, speaker_update (sound_speaker.c:25-26) sort un
    //   niveau continu de 0A00h pour un compte du canal 2 sous 40h, les tons trop aigus pour la sortie ; le compte 0 y
    //   tombe, alors que le 8254 le compte 2^16 (231164-005, PDF p. 17) et que pit_speaker_timer (pit.c:568) le suit
    //   déjà : la sortie est le carré de speakon, à 18,2 Hz, comme pour tout compte de 40h et plus.
    private static int16_t haut_parleur_materiel(int16_t val)
    {
        if (pit.pit_.m[2] == 0 || pit.pit_.m[2] == 4 || pit.pit_.l[2] != 0)
                return val;
        ModeMateriel.Sonde[293]++;
        return speakon != 0 ? (int16_t)0x1400 : (int16_t)0;
    }
}
