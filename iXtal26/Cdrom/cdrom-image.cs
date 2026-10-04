// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/cdrom/cdrom-image.cc  (cdrom-image.h : includes/private/cdrom/cdrom-image.h)
// STATUS: deviated — G10.3 (PLAN-G10.md) : le pilote ATAPI des images, entier — son état (image_path,
//         image_changed, cdrom, cdrom_capacity, image_cd_*, cd_buffer), le lecteur audio
//         (image_audio_callback, playaudio, pause, resume, stop, seek, getcurrentsubchannel,
//         is_track_audio), les données (ready, medium_changed, readtoc, readtoc_session, readtoc_raw,
//         readsector, readsector_raw, size, status), image_open, image_close et la table image_atapi.
//         Le rappel audio est appelé ici comme une fonction pure ; son appel par le son
//         (sound_cd_thread, sound.c:143-199) est G10.5.
//
// DEVIATIONS (PB-111, décision n° 2 de G10.3, des deux côtés — l'oracle compile harness_cdrom.cpp avec
// -ftrivial-auto-var-init=zero) : là où une piste manque, GetAudioTrackInfo et GetAudioSub n'écrivent
// rien et le C lit des variables automatiques non initialisées — attr d'is_track_audio et de
// playaudio, les champs de getcurrentsubchannel (:208-249). Zéro ici. Conséquence inscrite : PLAY AUDIO
// en MSF teste la piste sur la position encore compactée (:83), hors de toute piste, donc attr vaut 0 —
// la lecture part, même sur une piste de données.
//
// ÉTAT STATIQUE : tout ce qui précède traverse les ouvertures et les amorçages chez PCem ;
// image_clear_state_for_oracle_parity le remet à zéro, pendant de h_cd_reset (harness_cdrom.cpp).

// CS8981 : `cdrom_image` n'a que des minuscules ASCII — le nom de l'unité C, comme ide.cs.
#pragma warning disable CS8981

using System.Runtime.InteropServices;
using iXtal26.Ide;
using static iXtal26.Cdrom.CDROM_Interface_Image;

namespace iXtal26.Cdrom;

internal static class cdrom_image
{
    private static void pclog(string s) => Console.Error.Write(s);

    // pcem: cdrom-image.cc:10-11 — char image_path[1024].
    internal static string image_path = "";
    internal static int image_changed = 0;

    // pcem: cdrom-image.cc:15
    internal static CDROM_Interface_Image? cdrom = null;

    // pcem: cdrom-image.cc:17
    private static int MSFtoLBA(int m, int s, int f) => (((m * 60) + s) * 75) + f;

    // pcem: cdrom-image.cc:19
    internal static uint32_t cdrom_capacity = 0;

    // pcem: cdrom-image.cc:21
    internal const int CD_STOPPED = 0, CD_PLAYING = 1, CD_PAUSED = 2;

    // pcem: cdrom-image.cc:23-24
    internal static int image_cd_state = CD_STOPPED;
    internal static uint32_t image_cd_pos = 0, image_cd_end = 0;

    // pcem: cdrom-image.cc:26-28 — int16_t cd_buffer[BUF_SIZE], où ReadSector écrit des octets : gardé
    //   en octets, lu en mots de 16 bits (petit-boutiste, comme l'hôte de l'oracle).
    private const int BUF_SIZE = 32768;
    internal static readonly uint8_t[] cd_buffer = new uint8_t[BUF_SIZE * 2];
    internal static int cd_buflen = 0;

    // pcem: cdrom-image.cc:30-55 — output : int16_t[len].
    internal static void image_audio_callback(int16_t[] output, int len)
    {
        if (image_cd_state != CD_PLAYING)
            return;
        while (cd_buflen < len)
        {
            if (image_cd_pos < image_cd_end)
            {
                //                      pclog("Read to %i\n", cd_buflen);
                if (!cdrom!.ReadSector(cd_buffer, cd_buflen * 2, true, unchecked(image_cd_pos - 150)))
                {
                    //                                pclog("DeviceIoControl returned false\n");
                    Array.Clear(cd_buffer, cd_buflen * 2, (BUF_SIZE - cd_buflen) * 2);
                    image_cd_state = CD_STOPPED;
                    cd_buflen = len;
                }
                else
                {
                    //                                pclog("DeviceIoControl returned true\n");
                    image_cd_pos++;
                    cd_buflen += (RAW_SECTOR_SIZE / 2);
                }
            }
            else
            {
                Array.Clear(cd_buffer, cd_buflen * 2, (BUF_SIZE - cd_buflen) * 2);
                image_cd_state = CD_STOPPED;
                cd_buflen = len;
            }
        }

        MemoryMarshal.Cast<uint8_t, int16_t>(cd_buffer.AsSpan(0, len * 2)).CopyTo(output);
        Array.Copy(cd_buffer, len * 2, cd_buffer, 0, (BUF_SIZE - len) * 2);
        cd_buflen -= len;
    }

    // pcem: cdrom-image.cc:57
    internal static void image_audio_stop() { image_cd_state = CD_STOPPED; }

    // pcem: cdrom-image.cc:59-75
    private static int image_is_track_audio(uint32_t pos, int ismsf)
    {
        if (cdrom is null)
            return 0;
        if (ismsf != 0)
        {
            int m = (int)((pos >> 16) & 0xff);
            int s = (int)((pos >> 8) & 0xff);
            int f = (int)(pos & 0xff);
            pos = unchecked((uint32_t)MSF_TO_FRAMES(m, s, f));
        }

        // DEVIATION: attr non initialisé en C quand GetTrack rend -1 ; zéro (en-tête).
        uint8_t attr = 0;
        TMSF tmsf = default;
        int number = 0;
        cdrom.GetAudioTrackInfo(cdrom.GetTrack(unchecked((int)pos)), ref number, ref tmsf, ref attr);

        return attr == AUDIO_TRACK ? 1 : 0;
    }

    // pcem: cdrom-image.cc:77-113
    private static void image_playaudio(uint32_t pos, uint32_t len, int ismsf)
    {
        if (cdrom is null)
            return;
        int number = 0;
        // DEVIATION: attr non initialisé en C quand GetTrack rend -1 — dont toute demande en MSF ; zéro
        //   (en-tête) : la lecture part.
        uint8_t attr = 0;
        TMSF tmsf = default;
        cdrom.GetAudioTrackInfo(cdrom.GetTrack(unchecked((int)pos)), ref number, ref tmsf, ref attr);
        if (attr == DATA_TRACK)
        {
            pclog("Can't play data track\n");
            image_cd_pos = 0;
            image_cd_state = CD_STOPPED;
            return;
        }
        pclog($"Play audio - {pos:X8} {len:X8} {ismsf}\n");
        if (ismsf != 0)
        {
            int m, s, f;
            m = (int)((pos >> 16) & 0xff);
            s = (int)((pos >> 8) & 0xff);
            f = (int)(pos & 0xff);
            pos = unchecked((uint32_t)MSFtoLBA(m, s, f));

            m = (int)((len >> 16) & 0xff);
            s = (int)((len >> 8) & 0xff);
            f = (int)(len & 0xff);
            len = unchecked((uint32_t)MSFtoLBA(m, s, f));

            pclog($"MSF - pos = {pos:X8} len = {len:X8}\n");
        }
        else
            len += pos;
        image_cd_pos = pos; // + 150;
        image_cd_end = len; // + 150;
        image_cd_state = CD_PLAYING;
        if (image_cd_pos < 150)
            image_cd_pos = 150;
        cd_buflen = 0;
        pclog($"Audio start {image_cd_pos:X8} {image_cd_end:X8} {image_cd_state} {cd_buflen} {unchecked((int)len)}\n");
    }

    // pcem: cdrom-image.cc:115-120
    private static void image_pause()
    {
        if (cdrom is null)
            return;
        if (image_cd_state == CD_PLAYING)
            image_cd_state = CD_PAUSED;
    }

    // pcem: cdrom-image.cc:122-127
    private static void image_resume()
    {
        if (cdrom is null)
            return;
        if (image_cd_state == CD_PAUSED)
            image_cd_state = CD_PLAYING;
    }

    // pcem: cdrom-image.cc:129-133
    private static void image_stop()
    {
        if (cdrom is null)
            return;
        image_cd_state = CD_STOPPED;
    }

    // pcem: cdrom-image.cc:135-140
    private static void image_seek(uint32_t pos)
    {
        if (cdrom is null)
            return;
        image_cd_pos = pos;
        image_cd_state = CD_STOPPED;
    }

    // pcem: cdrom-image.cc:142-153
    private static int image_ready()
    {
        if (cdrom is null)
            return 0;

        if (image_path.Length == 0)
            return 0;

        if (image_changed != 0)
            image_changed = 0;

        return 1;
    }

    // pcem: cdrom-image.cc:155-177 — le plus grand début de piste, lead-out compris.
    private static int image_get_last_block(uint8_t starttrack, int msf, int maxlen, int single)
    {
        int c;
        uint32_t lb = 0;

        if (cdrom is null)
            return 0;

        int first_track = 0;
        int last_track = 0;
        int number = 0;
        uint8_t attr = 0;
        TMSF tmsf = default;
        cdrom.GetAudioTracks(ref first_track, ref last_track, ref tmsf);

        for (c = 0; c <= last_track; c++)
        {
            uint32_t address;
            cdrom.GetAudioTrackInfo(c + 1, ref number, ref tmsf, ref attr);
            address = unchecked((uint32_t)(MSFtoLBA(tmsf.min, tmsf.sec, tmsf.fr) - 150));
            if (address > lb)
                lb = address;
        }
        return unchecked((int)lb);
    }

    // pcem: cdrom-image.cc:179-196
    private static int image_medium_changed()
    {
        if (cdrom is null)
            return 0;

        if (image_path.Length == 0)
            return 0;

        if (cdrom_ioctl.old_cdrom_drive != cdrom_ioctl.cdrom_drive)
        {
            cdrom_ioctl.old_cdrom_drive = cdrom_ioctl.cdrom_drive;
            return 1;
        }

        if (image_changed != 0)
        {
            image_changed = 0;
            return 1;
        }
        return 0;
    }

    // pcem: cdrom-image.cc:198-253
    private static uint8_t image_getcurrentsubchannel(uint8_t[] b, int o, int msf)
    {
        if (cdrom is null)
            return 0;
        uint8_t ret;
        int pos = 0;

        uint32_t cdpos = image_cd_pos;
        if (cdpos >= 150)
            cdpos -= 150;
        // DEVIATION: hors des pistes, GetAudioSub n'écrit rien et b[] reçoit l'indéterminé ; zéro (en-tête).
        TMSF relPos = default, absPos = default;
        uint8_t attr = 0, track = 0, index = 0;
        cdrom.GetAudioSub(unchecked((int)cdpos), ref attr, ref track, ref index, ref relPos, ref absPos);

        if (image_cd_state == CD_PLAYING)
            ret = 0x11;
        else if (image_cd_state == CD_PAUSED)
            ret = 0x12;
        else
            ret = 0x13;

        b[o + pos++] = attr;
        b[o + pos++] = track;
        b[o + pos++] = index;

        if (msf != 0)
        {
            uint32_t dat = unchecked((uint32_t)MSFtoLBA(absPos.min, absPos.sec, absPos.fr));
            b[o + pos + 3] = (uint8_t)(dat % 75);
            dat /= 75;
            b[o + pos + 2] = (uint8_t)(dat % 60);
            dat /= 60;
            b[o + pos + 1] = unchecked((uint8_t)dat);
            b[o + pos] = 0;
            pos += 4;
            dat = unchecked((uint32_t)MSFtoLBA(relPos.min, relPos.sec, relPos.fr));
            b[o + pos + 3] = (uint8_t)(dat % 75);
            dat /= 75;
            b[o + pos + 2] = (uint8_t)(dat % 60);
            dat /= 60;
            b[o + pos + 1] = unchecked((uint8_t)dat);
            b[o + pos] = 0;
            pos += 4;
        }
        else
        {
            uint32_t dat = unchecked((uint32_t)MSFtoLBA(absPos.min, absPos.sec, absPos.fr));
            b[o + pos++] = (uint8_t)((dat >> 24) & 0xff);
            b[o + pos++] = (uint8_t)((dat >> 16) & 0xff);
            b[o + pos++] = (uint8_t)((dat >> 8) & 0xff);
            b[o + pos++] = (uint8_t)(dat & 0xff);
            dat = unchecked((uint32_t)MSFtoLBA(relPos.min, relPos.sec, relPos.fr));
            b[o + pos++] = (uint8_t)((dat >> 24) & 0xff);
            b[o + pos++] = (uint8_t)((dat >> 16) & 0xff);
            b[o + pos++] = (uint8_t)((dat >> 8) & 0xff);
            b[o + pos++] = (uint8_t)(dat & 0xff);
        }

        return ret;
    }

    // pcem: cdrom-image.cc:255
    private static void image_eject() { }

    // pcem: cdrom-image.cc:257
    private static void image_load() { }

    // pcem: cdrom-image.cc:259-263
    private static int image_readsector(uint8_t[] b, int o, int sector, int count)
    {
        if (cdrom is null)
            return -1;
        return cdrom.ReadSectors(b, o, false, unchecked((ulong)(long)sector), unchecked((ulong)(long)count)) ? 0 : 1;
    }

    // pcem: cdrom-image.cc:265-269 — l'échec n'est pas rendu : sur une piste cuite, b reste tel quel
    //   (« READ CD brut sur piste cuite qui laisse des données périmées », PLAN-G10.md).
    private static void image_readsector_raw(uint8_t[] b, int o, int sector)
    {
        if (cdrom is null)
            return;
        cdrom.ReadSector(b, o, true, unchecked((ulong)(long)sector));
    }

    // pcem: cdrom-image.cc:271-347
    private static int image_readtoc(uint8_t[] b, int o, uint8_t starttrack, int msf, int maxlen, int single)
    {
        if (cdrom is null)
            return 0;
        int len = 4;
        int c, d;
        uint32_t temp;

        int first_track = 0;
        int last_track = 0;
        int number = 0;
        uint8_t attr = 0;
        TMSF tmsf = default;
        cdrom.GetAudioTracks(ref first_track, ref last_track, ref tmsf);

        b[o + 2] = unchecked((uint8_t)first_track);
        b[o + 3] = unchecked((uint8_t)last_track);

        d = 0;
        for (c = 0; c <= last_track; c++)
        {
            cdrom.GetAudioTrackInfo(c + 1, ref number, ref tmsf, ref attr);
            if (number >= starttrack)
            {
                d = c;
                break;
            }
        }
        // Sans piste au-delà de starttrack, c vaut last_track + 1 : hors borne, rien n'est écrit, et
        // number garde le numéro du lead-out (0xAA).
        cdrom.GetAudioTrackInfo(c + 1, ref number, ref tmsf, ref attr);
        b[o + 2] = unchecked((uint8_t)number);

        for (c = d; c <= last_track; c++)
        {
            if ((len + 8) > maxlen)
                break;
            cdrom.GetAudioTrackInfo(c + 1, ref number, ref tmsf, ref attr);

            //                pclog("Len %i max %i Track %02X - %02X %02X %02i:%02i:%02i
            //                %08X\n",len,maxlen,toc[c].cdte_track,toc[c].cdte_adr,toc[c].cdte_ctrl,toc[c].cdte_addr.msf.minute,
            //                toc[c].cdte_addr.msf.second, toc[c].cdte_addr.msf.frame,MSFtoLBA(toc[c].cdte_addr.msf.minute,
            //                toc[c].cdte_addr.msf.second, toc[c].cdte_addr.msf.frame));
            b[o + len++] = 0; /* reserved */
            b[o + len++] = attr;
            b[o + len++] = unchecked((uint8_t)number); /* track number */
            b[o + len++] = 0;      /* reserved */

            if (msf != 0)
            {
                b[o + len++] = 0;
                b[o + len++] = tmsf.min;
                b[o + len++] = tmsf.sec;
                b[o + len++] = tmsf.fr;
            }
            else
            {
                temp = unchecked((uint32_t)(MSFtoLBA(tmsf.min, tmsf.sec, tmsf.fr) - 150));
                b[o + len++] = (uint8_t)(temp >> 24);
                b[o + len++] = unchecked((uint8_t)(temp >> 16));
                b[o + len++] = unchecked((uint8_t)(temp >> 8));
                b[o + len++] = unchecked((uint8_t)temp);
            }
            if (single != 0)
                break;
        }
        b[o + 0] = (uint8_t)(((len - 2) >> 8) & 0xff);
        b[o + 1] = (uint8_t)((len - 2) & 0xff);
        // omitted: la table des matières imprimée en commentaire (:330-345).
        return len;
    }

    // pcem: cdrom-image.cc:349-381 — b[0] et b[1] ne sont pas écrits.
    private static int image_readtoc_session(uint8_t[] b, int o, int msf, int maxlen)
    {
        if (cdrom is null)
            return 0;
        int len = 4;

        int number = 0;
        TMSF tmsf = default;
        uint8_t attr = 0;
        cdrom.GetAudioTrackInfo(1, ref number, ref tmsf, ref attr);

        //        pclog("Read TOC session - %i %02X %02X %i %i %02X %02X %02X\n",0, 0, 0,1,1,attr,0,number);

        b[o + 2] = 1;
        b[o + 3] = 1;
        b[o + len++] = 0; /* reserved */
        b[o + len++] = attr;
        b[o + len++] = unchecked((uint8_t)number); /* track number */
        b[o + len++] = 0;      /* reserved */
        if (msf != 0)
        {
            b[o + len++] = 0;
            b[o + len++] = tmsf.min;
            b[o + len++] = tmsf.sec;
            b[o + len++] = tmsf.fr;
        }
        else
        {
            uint32_t temp = unchecked((uint32_t)(MSFtoLBA(tmsf.min, tmsf.sec, tmsf.fr) - 150));
            b[o + len++] = (uint8_t)(temp >> 24);
            b[o + len++] = unchecked((uint8_t)(temp >> 16));
            b[o + len++] = unchecked((uint8_t)(temp >> 8));
            b[o + len++] = unchecked((uint8_t)temp);
        }

        return len;
    }

    // pcem: cdrom-image.cc:383-425 — de first_track à last_track : sans le lead-out, et sans b[0] ni b[1].
    private static int image_readtoc_raw(uint8_t[] b, int o, int maxlen)
    {
        if (cdrom is null)
            return 0;

        int track;
        int len = 4;

        int first_track = 0;
        int last_track = 0;
        int number = 0;
        uint8_t attr = 0;
        TMSF tmsf = default;
        cdrom.GetAudioTracks(ref first_track, ref last_track, ref tmsf);

        b[o + 2] = unchecked((uint8_t)first_track);
        b[o + 3] = unchecked((uint8_t)last_track);

        for (track = first_track; track <= last_track; track++)
        {
            if ((len + 11) > maxlen)
            {
                pclog("image_readtocraw: This iteration would fill the buffer beyond the bounds, aborting...\n");
                return len;
            }

            cdrom.GetAudioTrackInfo(track, ref number, ref tmsf, ref attr);

            //              pclog("read_toc: Track %02X - number %02X control %02X adr %02X address %02X %02X %02X %02X\n",
            //              track, toc[track].cdte_track, toc[track].cdte_ctrl, toc[track].cdte_adr, 0,
            //              toc[track].cdte_addr.msf.minute, toc[track].cdte_addr.msf.second, toc[track].cdte_addr.msf.frame);

            b[o + len++] = unchecked((uint8_t)track);
            b[o + len++] = attr;
            b[o + len++] = 0;
            b[o + len++] = 0;
            b[o + len++] = 0;
            b[o + len++] = 0;
            b[o + len++] = 0;
            b[o + len++] = 0;
            b[o + len++] = tmsf.min;
            b[o + len++] = tmsf.sec;
            b[o + len++] = tmsf.fr;
        }
        return len;
    }

    // pcem: cdrom-image.cc:427 — ni remis à zéro par image_close, ni par un image_open raté : la
    //   capacité d'avant reste.
    private static uint32_t image_size() => cdrom_capacity;

    // pcem: cdrom-image.cc:429-444
    private static int image_status()
    {
        if (cdrom is null)
            return cdrom_ioctl.CD_STATUS_EMPTY;
        if (cdrom.HasAudioTracks())
        {
            switch (image_cd_state)
            {
            case CD_PLAYING:
                    return cdrom_ioctl.CD_STATUS_PLAYING;
            case CD_PAUSED:
                    return cdrom_ioctl.CD_STATUS_PAUSED;
            case CD_STOPPED:
            default:
                    return cdrom_ioctl.CD_STATUS_STOPPED;
            }
        }
        return cdrom_ioctl.CD_STATUS_DATA_ONLY;
    }

    // pcem: cdrom-image.cc:445
    internal static void image_reset() { }

    // pcem: cdrom-image.cc:447-454 — atapi n'est pas touché : il reste image_atapi, dont chaque entrée
    //   voit cdrom nul.
    internal static void image_close()
    {
        image_cd_state = CD_STOPPED;
        if (cdrom is not null)
        {
            cdrom.Dispose();
            cdrom = null;
        }
        //        memset(image_path, 0, 1024);
    }

    // pcem: cdrom-image.cc:456-478 — rend 0 si l'image est montée, 1 sinon ; un échec laisse atapi tel
    //   quel, et la capacité d'avant.
    internal static int image_open(string fn)
    {
        if (fn != image_path)
            image_changed = 1;

        /* Make sure image_changed stays when changing from an image to another image. */
        if (cdrom_ioctl.cdrom_drive != ide.CDROM_IMAGE)
            image_changed = 1;
        /* strcpy fails on OSX if both parameters are pointing to the same address */
        // pcem bug, not reproduced: PB-110 (R9) — strcpy dans image_path[1024], sans borne (:465) : un
        //   chemin de 1 024 octets ou plus déborde le tableau. Une chaîne C# n'a pas cette borne ; la clé
        //   cdrom_path, en G10.4, la posera.
        image_path = fn;

        // L'objet d'avant, s'il y en a un, n'est pas libéré (PB-112 ; le GC s'en charge).
        cdrom = new CDROM_Interface_Image();
        if (!cdrom.SetDevice(fn, 0))
        {
            image_close();
            return 1;
        }
        image_cd_state = CD_STOPPED;
        image_cd_pos = 0;
        cd_buflen = 0;
        cdrom_capacity = unchecked((uint32_t)(image_get_last_block(0, 0, 4096, 0) + 1));
        ide_atapi.atapi = image_atapi;
        return 0;
    }

    // pcem: cdrom-image.cc:480
    private static void image_exit() { }

    // pcem: cdrom-image.cc:482-500
    internal static readonly ATAPI image_atapi = new(image_ready, image_medium_changed, image_readtoc,
                                                       image_readtoc_session, image_readtoc_raw,
                                                       image_getcurrentsubchannel, image_readsector,
                                                       image_readsector_raw, image_playaudio, image_seek,
                                                       image_load, image_eject, image_pause, image_resume,
                                                       image_size, image_status, image_is_track_audio,
                                                       image_stop, image_exit);

    // ORACLE PARITY (pas de contrepartie PCem) : l'état statique du pilote remis à zéro — pendant de
    // h_cd_reset (harness_cdrom.cpp). La porte cdimage-check l'appelle à chaque cas ; l'amorçage
    // l'appellera en G10.4, comme opl_clear_state_for_oracle_parity (G8).
    internal static void image_clear_state_for_oracle_parity()
    {
        image_close();
        image_path = "";
        image_changed = 0;
        cdrom_capacity = 0;
        image_cd_state = CD_STOPPED;
        image_cd_pos = image_cd_end = 0;
        Array.Clear(cd_buffer);
        cd_buflen = 0;
    }
}
