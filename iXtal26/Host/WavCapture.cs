// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — outillage hôte.
// STATUS: host
//
// GR.3i — LE SON D'UN TÉMOIN, EN FICHIER. Sous --boot, aucune sortie audio n'est branchée : « @son » ne donne que
// l'empreinte du mélange, qui diffère déjà entre le mode PCem et le mode matériel au repos, et ne dit pas ce qui a
// sonné. « @wav CHEMIN » branche à sa place, sur les deux voies où SdlAudio se branche dans la fenêtre, deux fichiers
// WAV de 16 bits stéréo : le mélange de la machine (sound_give_buffer_func, 48 kHz, saturé à 16 bits comme
// GiveBuffer) dans CHEMIN, la voie du CD (sound_give_cd_buffer_func, 44,1 kHz) dans CHEMIN.cd.wav. L'en-tête est
// récrit à chaque bloc : le fichier reste lisible quand --boot s'arrête, sans fermeture.

namespace iXtal26.Host;

internal sealed class WavCapture
{
    private readonly FileStream _file;
    private readonly int _rate;
    private long _bytes;

    private WavCapture(string path, int rate)
    {
        _file = new FileStream(path, FileMode.Create, FileAccess.Write);
        _rate = rate;
        WriteHeader();
    }

    internal static void Start(string path)
    {
        var mix = new WavCapture(path, 48000);
        var cd = new WavCapture(path + ".cd.wav", 44100);
        Sound.sound.sound_give_buffer_func = mix.GiveBuffer;
        Sound.sound.sound_give_cd_buffer_func = cd.GiveCdBuffer;
    }

    private void GiveBuffer(int32_t[] buf)
    {
        var samples = new int16_t[Sound.sound.sound_buf_len_al * 2];
        for (var i = 0; i < samples.Length; i++)
            samples[i] = (int16_t)Math.Clamp(buf[i], int16_t.MinValue, int16_t.MaxValue);
        Append(samples);
    }

    private void GiveCdBuffer(int16_t[] buf) => Append(buf.AsSpan(0, Sound.sound.CD_BUFLEN * 2));

    private void Append(ReadOnlySpan<int16_t> samples)
    {
        _file.Seek(0, SeekOrigin.End);
        _file.Write(System.Runtime.InteropServices.MemoryMarshal.AsBytes(samples));
        _bytes += samples.Length * sizeof(int16_t);
        WriteHeader();
        _file.Flush();
    }

    private void WriteHeader()
    {
        Span<byte> h = stackalloc byte[44];
        "RIFF"u8.CopyTo(h);
        BitConverter.TryWriteBytes(h[4..], (uint)(36 + _bytes));
        "WAVEfmt "u8.CopyTo(h[8..]);
        BitConverter.TryWriteBytes(h[16..], 16u);
        BitConverter.TryWriteBytes(h[20..], (ushort)1);
        BitConverter.TryWriteBytes(h[22..], (ushort)2);
        BitConverter.TryWriteBytes(h[24..], (uint)_rate);
        BitConverter.TryWriteBytes(h[28..], (uint)(_rate * 4));
        BitConverter.TryWriteBytes(h[32..], (ushort)4);
        BitConverter.TryWriteBytes(h[34..], (ushort)16);
        "data"u8.CopyTo(h[36..]);
        BitConverter.TryWriteBytes(h[40..], (uint)_bytes);
        _file.Seek(0, SeekOrigin.Begin);
        _file.Write(h);
    }
}
