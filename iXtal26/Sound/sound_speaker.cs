// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/sound/sound_speaker.c
// STATUS: partial — état seulement, sans sortie audio.
//
// Le haut-parleur du PC est piloté par le canal 2 du PIT et par les deux bits bas
// du port 0x61. On transcrit CET ÉTAT parce que le BIOS l'écrit pendant le POST
// (le bip) et que le PPI le relit — mais pas la synthèse, qui appartient au
// mixeur OpenAL de PCem, hors périmètre.

namespace iXtal26.Sound;

internal static partial class sound_speaker
{
    // pcem: sound_speaker.c / ppi.c — le gate vient du PIT, l'enable du port 0x61.
    internal static int speaker_gated;
    internal static int speaker_enable;

    // pcem: ibm.h — état de la sortie, écrit par le canal 2 du PIT.
    internal static int speakon;
    internal static int speakval;

    // pcem: sound_speaker.c — remplit le tampon audio. Sans sortie : inerte.
    internal static void speaker_update() { }
}
