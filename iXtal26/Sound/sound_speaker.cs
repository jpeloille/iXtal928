// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/sound/sound_speaker.c
// STATUS: transcribed — sound_speaker.c:5-55 en entier. Omis : `gated` (:5),
//         mort chez PCem aussi, sa seule référence étant le printf commenté :20.

using iXtal26.Models;

namespace iXtal26.Sound;

internal static partial class sound_speaker
{
    // pcem: sound_speaker.c:5-6 — déclarées ibm.h:345 et :350.
    internal static int speakval, speakon;
    internal static int ppispeakon;

    // pcem: sound_speaker.c:8
    internal static int speaker_mute = 0;

    // pcem: sound_speaker.c:10
    private static int16_t[] speaker_buffer = new int16_t[sound.MAXSOUNDBUFLEN];

    // pcem: sound_speaker.c:12
    private static int speaker_pos = 0;

    // pcem: sound_speaker.c:14-15
    internal static int speaker_gated = 0;
    internal static int speaker_enable = 0, was_speaker_enable = 0;

    // pcem: sound_speaker.c:17-37
    internal static void speaker_update()
    {
        int16_t val;

        for (; speaker_pos < sound.sound_pos_global; speaker_pos++)
        {
                if (speaker_gated != 0 && was_speaker_enable != 0)
                {
                        if (pit.pit_.m[2] == 0 || pit.pit_.m[2] == 4)
                                val = (int16_t)speakval;
                        else if (pit.pit_.l[2] < 0x40)
                                val = 0xa00;
                        else
                                val = speakon != 0 ? (int16_t)0x1400 : (int16_t)0;
                }
                else
                        val = was_speaker_enable != 0 ? (int16_t)0x1400 : (int16_t)0;

                if (speaker_enable == 0)
                        was_speaker_enable = 0;

                speaker_buffer[speaker_pos] = val;
        }
    }

    // pcem: sound_speaker.c:39-50
    private static void speaker_get_buffer(int32_t[] buffer, int len, object? p)
    {
        int c;

        speaker_update();

        if (speaker_mute == 0)
        {
                for (c = 0; c < len * 2; c++)
                        buffer[c] += speaker_buffer[c >> 1];
        }

        speaker_pos = 0;
    }

    // pcem: sound_speaker.c:52-55
    internal static void speaker_init()
    {
        sound.sound_add_handler(speaker_get_buffer, null);
        speaker_mute = 0;
    }

    // Sonde haut-parleur — neuf champs, ordre identique à h_speaker_probe
    // (tools/oracle/harness.c). speaker_pos est `private` ici et `static` en C :
    // c'est le curseur de rattrapage du générateur, et une désynchronisation s'y
    // voit avant de s'entendre. Le neuvième champ est l'empreinte du son
    // réellement produit, cumulée bloc par bloc dans sound_poll — une empreinte
    // identique des deux côtés ne prouve rien si elle est restée à sa graine.
    internal static void Probe(uint64_t[] o)
    {
        o[0] = (uint64_t)(int64_t)speaker_gated;
        o[1] = (uint64_t)(int64_t)speaker_enable;
        o[2] = (uint64_t)(int64_t)was_speaker_enable;
        o[3] = (uint64_t)(int64_t)speakon;
        o[4] = (uint64_t)(int64_t)speakval;
        o[5] = (uint64_t)(int64_t)ppispeakon;
        o[6] = (uint64_t)(int64_t)speaker_pos;
        o[7] = (uint64_t)(int64_t)sound.sound_pos_global;
        o[8] = sound.sound_hash;
    }
}
