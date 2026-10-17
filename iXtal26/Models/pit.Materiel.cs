// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — le code propre au mode matériel du 8253/8254 (G13 ; R10 de TRANSCRIPTION.md).
// STATUS: materiel
//
// Les corrections des défauts de PCem dans pit.c, appelées par les gardes de pit.cs (`if (materiel.pb_nn)`), seules
// sur leur ligne. La source, le cas qui discrimine et la panne qui le rougit sont dans l'entrée PB de PCEM_BUGS.md ;
// la fiche est celle du 8254 d'Intel (231164-005).

namespace iXtal26.Models;

internal static partial class pit
{
    // pcem bug, fixed in hardware mode: PB-21 — speakval, le rapport des comptes des canaux 2 et 0 (pit.c:418), lit le
    //   compte 0 comme 0 ; le 8254 le compte 2^16 (PDF p. 17 : « The largest possible initial count is 0; this is
    //   equivalent to 2^16 for binary counting »), comme pit_load (pit.c:119) : au numérateur et au dénominateur. Le
    //   calcul de PCem refait, en float ; le quotient borné par 65 536, la conversion sans dépassement.
    private static void speakval_materiel(PIT pit)
    {
        float l0 = pit.l[0] != 0 ? pit.l[0] : 0x10000, l2 = pit.l[2] != 0 ? pit.l[2] : 0x10000;
        var v = (int)(((l2 / l0) * 0x4000) - 0x2000);
        Sound.sound_speaker.speakval = v > 0x2000 ? 0x2000 : v;
        ModeMateriel.Sonde[21]++;
    }
}
