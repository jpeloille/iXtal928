// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/cdrom/cdrom-null.c
// STATUS: transcribed — G10.3 (PLAN-G10.md) : le lecteur sans disque, la table null_atapi que pc.c pose
//         quand cdrom_drive vaut -1 (pc.c:292-293 ; branché en G10.4).

// CS8981 : `cdrom_null` n'a que des minuscules ASCII — le nom de l'unité C.
#pragma warning disable CS8981

using iXtal26.Ide;

namespace iXtal26.Cdrom;

internal static class cdrom_null
{
    // pcem: cdrom-null.c:9 — memset(output, 0, len * 2) : len mots de 16 bits.
    internal static void cdrom_null_audio_callback(int16_t[] output, int len) { Array.Clear(output, 0, len); }

    // pcem: cdrom-null.c:11-21
    internal static void cdrom_null_audio_stop() { }

    private static void null_playaudio(uint32_t pos, uint32_t len, int ismsf) { }

    private static void null_pause() { }

    private static void null_resume() { }

    private static void null_stop() { }

    private static void null_seek(uint32_t pos) { }

    // pcem: cdrom-null.c:23-26
    private static int null_ready() => 0;

    /* Always return 0, the contents of a null CD-ROM drive never change. */
    private static int null_medium_changed() => 0;

    // pcem: cdrom-null.c:28-46
    private static uint8_t null_getcurrentsubchannel(uint8_t[] b, int o, int msf) => 0x13;

    private static void null_eject() { }

    private static void null_load() { }

    private static int null_readsector(uint8_t[] b, int o, int sector, int count) => 0;

    private static void null_readsector_raw(uint8_t[] b, int o, int sector) { }

    private static int null_readtoc(uint8_t[] b, int o, uint8_t starttrack, int msf, int maxlen, int single) => 0;

    private static int null_readtoc_session(uint8_t[] b, int o, int msf, int maxlen) => 0;

    private static int null_readtoc_raw(uint8_t[] b, int o, int maxlen) => 0;

    private static uint32_t null_size() => 0;

    private static int null_status() => cdrom_ioctl.CD_STATUS_EMPTY;

    // pcem: cdrom-null.c:48
    internal static void cdrom_null_reset() { }

    // pcem: cdrom-null.c:50-53 — `char d` en C, inutilisé.
    internal static int cdrom_null_open(int d)
    {
        ide_atapi.atapi = null_atapi;
        return 0;
    }

    // pcem: cdrom-null.c:55-59
    internal static void null_close() { }

    private static void null_exit() { }

    private static int null_is_track_audio(uint32_t pos, int ismsf) => 0;

    // pcem: cdrom-null.c:61-79
    internal static readonly ATAPI null_atapi = new(null_ready, null_medium_changed, null_readtoc,
                                                      null_readtoc_session, null_readtoc_raw,
                                                      null_getcurrentsubchannel, null_readsector,
                                                      null_readsector_raw, null_playaudio, null_seek, null_load,
                                                      null_eject, null_pause, null_resume, null_size, null_status,
                                                      null_is_track_audio, null_stop, null_exit);
}
