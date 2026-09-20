// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/sound/soundopenal.c
// STATUS: host — l'étage de sortie audio. L'interface est celle de PCem
//         (inital / givealbuffer / closeal) ; seule l'enveloppe OpenAL disparaît,
//         comme l'enveloppe wxWidgets a disparu dans SdlMenu.cs.

using SDL3;

namespace iXtal26.Host;

/// <summary>
/// Le tampon du mixeur (Sound/sound.cs) vers le périphérique audio, en SDL3.
///
/// Mode PUSH délibéré : le flux est ouvert avec un callback nul et alimenté par
/// <see cref="GiveBuffer"/>, appelée depuis le fil d'émulation — exactement comme
/// le crochet de blit. Un callback audio SDL3 s'exécuterait sur un fil séparé, et
/// serait le premier thread du projet ; video.cs:383-395 et :626-635 ont des corps
/// vides précisément parce qu'iXtal26 est mono-thread.
/// </summary>
internal sealed class SdlAudio : IDisposable
{
    // soundopenal.c:25 — AL_FORMAT_STEREO16 à FREQ. Le haut-parleur est mono,
    // dupliqué sur les deux canaux par speaker_get_buffer (sound_speaker.c:46).
    private const int Freq = 48000;
    private const int Channels = 2;

    // soundopenal.c:98 — alGenBuffers(4, buffers). C'est toute la profondeur de
    // file de PCem : quatre blocs de sound_buf_len_al, soit ~200 ms.
    private const int QueueDepthBlocks = 4;

    private IntPtr _stream;
    private int16_t[] _buf16 = new int16_t[Sound.sound.MAXSOUNDBUFLEN * Channels];

    /// <summary>Blocs jetés faute de place dans la file. Sans compteur, la perte
    /// serait silencieuse — le même angle mort que le chemin pixel de § M4.5.</summary>
    internal int BlocksDropped { get; private set; }

    /// <summary>Blocs réellement déposés.</summary>
    internal int BlocksQueued { get; private set; }

    /// <summary>Coupe la sortie sans rien changer à la machine émulée : sound_poll
    /// tourne, speaker_update remplit, seul le dépôt est sauté. C'est ce que fait le
    /// turbo, qui déroule ~14 s émulées par seconde murale.</summary>
    internal bool Muted { get; set; }

    /// <summary>
    /// Pendant d'inital() (soundopenal.c:86). Rend false sans bruit fatal : une
    /// sortie audio absente n'est pas une panne de 5150, la machine doit tourner
    /// muette. L'appelant décide quoi en dire.
    /// </summary>
    internal bool Init()
    {
        var spec = new SDL.AudioSpec
        {
            Format = SDL.AudioFormat.AudioS16LE,
            Channels = Channels,
            Freq = Freq,
        };

        _stream = SDL.OpenAudioDeviceStream(SDL.AudioDeviceDefaultPlayback, in spec, null, IntPtr.Zero);
        if (_stream == IntPtr.Zero)
            return false;

        // soundopenal.c:135 — alSourcePlay. Sans ça le périphérique reste en pause
        // et la file se remplit jusqu'au plafond sans qu'une note sorte.
        if (!SDL.ResumeAudioStreamDevice(_stream))
        {
            SDL.DestroyAudioStream(_stream);
            _stream = IntPtr.Zero;
            return false;
        }

        ApplyGain();
        return true;
    }

    /// <summary>
    /// Pendant de givealbuffer (soundopenal.c:143). Branché sur
    /// Sound.sound.sound_give_buffer_func, donc appelé une fois tous les
    /// sound_buf_len_al échantillons — 2 400 par défaut, soit 50 ms émulées.
    /// </summary>
    internal void GiveBuffer(int32_t[] buf)
    {
        if (_stream == IntPtr.Zero)
            return;

        var samples = Sound.sound.sound_buf_len_al * Channels;

        if (Muted)
        {
            BlocksDropped++;
            return;
        }

        // CONTRE-PRESSION. soundopenal.c:173 ne dépose un bloc que s'il reste un
        // tampon libre parmi quatre, et JETTE le bloc sinon : l'étage de sortie de
        // PCem est perdant par conception, parce que le rythme d'écriture est celui
        // du CPU émulé et non celui de la carte. Un anneau qui absorberait tout
        // ferait dériver la latence sans fin au lieu de perdre des blocs — et en
        // turbo, quatorze secondes de son par seconde murale.
        //
        // C'est aussi la règle déjà écrite pour la vidéo (SdlHost.cs, la DEVIATION
        // sur SetRenderVSync) : borner la file, jamais bloquer le fil d'émulation.
        var queuedBytes = SDL.GetAudioStreamQueued(_stream);
        if (queuedBytes >= QueueDepthBlocks * samples * sizeof(int16_t))
        {
            BlocksDropped++;
            return;
        }

        // soundopenal.c:184-191 — écrêtage int32 -> int16. L'accumulation se fait en
        // int32 pour que plusieurs cartes puissent sommer sans saturer trop tôt ; le
        // 5150 n'en a qu'une, mais le point de coupe reste celui de PCem.
        for (var c = 0; c < samples; c++)
        {
            var v = buf[c];
            _buf16[c] = v < -32768 ? (int16_t)(-32768)
                      : v > 32767 ? (int16_t)32767
                      : (int16_t)v;
        }

        SDL.PutAudioStreamData(_stream, System.Runtime.InteropServices.MemoryMarshal.AsBytes(
            _buf16.AsSpan(0, samples)), samples * sizeof(int16_t));
        BlocksQueued++;
    }

    /// <summary>
    /// soundopenal.c:176-178 applique sound_gain à alListenerf(AL_GAIN), donc JAMAIS
    /// aux échantillons. SDL3 n'a pas de listener : le gain va sur le flux, ce qui
    /// laisse le tampon du mixeur bit pour bit identique à celui de l'oracle.
    /// </summary>
    private void ApplyGain()
    {
        SDL.SetAudioStreamGain(_stream, MathF.Pow(10f, Sound.sound.sound_gain / 20f));
    }

    /// <summary>Pendant de closeal (soundopenal.c:70).</summary>
    public void Dispose()
    {
        if (_stream == IntPtr.Zero)
            return;

        SDL.DestroyAudioStream(_stream);
        _stream = IntPtr.Zero;
    }
}
