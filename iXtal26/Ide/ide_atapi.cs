// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/includes/private/ide/ide_atapi.h:7-29 + pcem-dev/src/ide/ide_atapi.c:26
// STATUS: partial — G10.3 : le type ATAPI, la table du pilote de CD-ROM de l'hôte (ide_atapi.h:7-27),
//         et le pointeur `atapi` (ide_atapi.c:26). Le pont ATAPI lui-même (atapi_device_t et
//         ide_atapi.c) est G10.4 (PLAN-G10.md).
//
// DEVIATION: les tampons `uint8_t *b` deviennent (tableau, décalage) — scsi_cd.c en passe à
//   l'intérieur de data_in (&data->data_in[c * 2352], scsi_cd.c:1108 ; &data->data_in[5], :1397).

namespace iXtal26.Ide;

// pcem: ide_atapi.h:10-15
internal delegate int atapi_readtoc_fn(uint8_t[] b, int o, uint8_t starttrack, int msf, int maxlen, int single);
internal delegate int atapi_readtoc_session_fn(uint8_t[] b, int o, int msf, int maxlen);
internal delegate int atapi_readtoc_raw_fn(uint8_t[] b, int o, int maxlen);
internal delegate uint8_t atapi_getcurrentsubchannel_fn(uint8_t[] b, int o, int msf);
internal delegate int atapi_readsector_fn(uint8_t[] b, int o, int sector, int count);
internal delegate void atapi_readsector_raw_fn(uint8_t[] b, int o, int sector);

// pcem: ide_atapi.h:7-27
internal sealed class ATAPI
{
    internal readonly Func<int> ready;
    internal readonly Func<int> medium_changed;
    internal readonly atapi_readtoc_fn readtoc;
    internal readonly atapi_readtoc_session_fn readtoc_session;
    internal readonly atapi_readtoc_raw_fn readtoc_raw;
    internal readonly atapi_getcurrentsubchannel_fn getcurrentsubchannel;
    internal readonly atapi_readsector_fn readsector;
    internal readonly atapi_readsector_raw_fn readsector_raw;
    internal readonly Action<uint32_t, uint32_t, int> playaudio;
    internal readonly Action<uint32_t> seek;
    internal readonly Action load;
    internal readonly Action eject;
    internal readonly Action pause;
    internal readonly Action resume;
    internal readonly Func<uint32_t> size;
    internal readonly Func<int> status;
    internal readonly Func<uint32_t, int, int> is_track_audio;
    internal readonly Action stop;
    internal readonly Action exit;

    // Dans l'ordre des initialiseurs C (cdrom-image.cc:482-500, cdrom-null.c:61-79).
    internal ATAPI(Func<int> ready, Func<int> medium_changed, atapi_readtoc_fn readtoc,
                   atapi_readtoc_session_fn readtoc_session, atapi_readtoc_raw_fn readtoc_raw,
                   atapi_getcurrentsubchannel_fn getcurrentsubchannel, atapi_readsector_fn readsector,
                   atapi_readsector_raw_fn readsector_raw, Action<uint32_t, uint32_t, int> playaudio,
                   Action<uint32_t> seek, Action load, Action eject, Action pause, Action resume,
                   Func<uint32_t> size, Func<int> status, Func<uint32_t, int, int> is_track_audio, Action stop,
                   Action exit)
    {
        this.ready = ready;
        this.medium_changed = medium_changed;
        this.readtoc = readtoc;
        this.readtoc_session = readtoc_session;
        this.readtoc_raw = readtoc_raw;
        this.getcurrentsubchannel = getcurrentsubchannel;
        this.readsector = readsector;
        this.readsector_raw = readsector_raw;
        this.playaudio = playaudio;
        this.seek = seek;
        this.load = load;
        this.eject = eject;
        this.pause = pause;
        this.resume = resume;
        this.size = size;
        this.status = status;
        this.is_track_audio = is_track_audio;
        this.stop = stop;
        this.exit = exit;
    }
}

internal static class ide_atapi
{
    // pcem: ide_atapi.c:26 — NULL jusqu'à ce qu'un pilote soit posé : cdrom_null_open ou image_open,
    //   appelés par le bloc CD de pc.c (G10.4).
    internal static ATAPI? atapi;
}
