// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — le code propre au mode matériel du pilote d'images de CD (G13 ; R10 de TRANSCRIPTION.md).
// STATUS: materiel
//
// Les corrections des défauts de PCem dans cdrom-image.cc, appelées par les gardes de cdrom-image.cs
// (`if (materiel.pb_nn)`), seules sur leur ligne. La source, le cas qui discrimine et la panne qui le rougit sont dans
// l'entrée PB de PCEM_BUGS.md. La référence : SFF-8020i révision 2.6 (22 janvier 1996), § 10.8.19.

using static iXtal26.Cdrom.CDROM_Interface_Image;

namespace iXtal26.Cdrom;

internal static partial class cdrom_image
{
    // pcem bug, fixed in hardware mode: PB-120 — READ TOC au format 10b (SFF-8020i r2.6 § 10.8.19, Tables 128, 129 et
    //   131) : l'en-tête, la longueur (2 + 11 × le nombre de descripteurs) puis la première et la dernière session
    //   (1 : une seule) ; les descripteurs de onze octets — la session, ADR/contrôle, TNO 0, POINT, trois octets de
    //   temps et un zéro, puis PMIN, PSEC et PFRAME, en binaire — « the first TOC entries shall be the A0, A1, A2h
    //   pointers », puis une entrée par piste. A0h : le numéro de la première piste et le type du disque (00h, ou 20h
    //   pour un CD-ROM XA dont la première piste est en mode 2) ; A1h : le numéro de la dernière piste ; A2h : le
    //   début du lead-out. L'ADR/contrôle de A0h est celui de la première piste, celui de A1h et de A2h celui de la
    //   dernière (déduit : la Table 131 ne donne que « 4/6 » et ADR 1). La table entière ; la commande la coupe à la
    //   longueur d'allocation.
    private static int image_readtoc_raw_materiel(CDROM_Interface_Image cd, uint8_t[] b, int o)
    {
        ModeMateriel.Sonde[120]++;
        int premiere = 0, derniere = 0, numero = 0;
        uint8_t attr = 0;
        TMSF t = default, leadOut = default;
        cd.GetAudioTracks(ref premiere, ref derniere, ref leadOut);
        var len = 4;

        void Descripteur(uint8_t adr_controle, uint8_t point, uint8_t pmin, uint8_t psec, uint8_t pframe)
        {
            b[o + len++] = 1;
            b[o + len++] = adr_controle;
            b[o + len++] = 0;
            b[o + len++] = point;
            b[o + len++] = 0;
            b[o + len++] = 0;
            b[o + len++] = 0;
            b[o + len++] = 0;
            b[o + len++] = pmin;
            b[o + len++] = psec;
            b[o + len++] = pframe;
        }

        cd.GetAudioTrackInfo(premiere, ref numero, ref t, ref attr);
        var type = (uint8_t)(cd.tracks[premiere - 1].mode2 && attr == DATA_TRACK ? 0x20 : 0x00);
        Descripteur(attr, 0xA0, (uint8_t)numero, type, 0);
        cd.GetAudioTrackInfo(derniere, ref numero, ref t, ref attr);
        Descripteur(attr, 0xA1, (uint8_t)numero, 0, 0);
        Descripteur(attr, 0xA2, leadOut.min, leadOut.sec, leadOut.fr);
        for (var piste = premiere; piste <= derniere; piste++)
        {
            cd.GetAudioTrackInfo(piste, ref numero, ref t, ref attr);
            Descripteur(attr, (uint8_t)numero, t.min, t.sec, t.fr);
        }

        b[o] = (uint8_t)((len - 2) >> 8);
        b[o + 1] = (uint8_t)(len - 2);
        b[o + 2] = 1;
        b[o + 3] = 1;
        return len;
    }

    // pcem bug, fixed in hardware mode: PB-123 — la position du lecteur (image_cd_pos, image_cd_end) compte les 150
    //   secteurs de l'amorce : 00:02:00 est le LBA 0 (SFF-8020i r2.6 § 10.8.8 : « PLAY AUDIO commands with a starting
    //   LBA address of 0000 0000h shall begin the audio play operation at 00m 02s 00f »). Une adresse LBA, de PLAY AUDIO
    //   ou de SEEK, y entre plus 150 ; une adresse MSF y entre telle quelle.
    private static uint32_t position_lba_materiel(uint32_t pos, int ismsf)
    {
        if (ismsf != 0)
            return pos;
        ModeMateriel.Sonde[123]++;
        return unchecked(pos + 150);
    }

    // pcem bug, fixed in hardware mode: PB-123 — READ SUB-CHANNEL en LBA : l'adresse absolue est le LBA, la position
    //   absolue moins 150 (§ 10.8.18, Table 115).
    private static uint32_t lba_absolue_materiel(uint32_t frames)
    {
        ModeMateriel.Sonde[123]++;
        return unchecked(frames - 150);
    }

    // pcem bug, fixed in hardware mode: PB-215 — le MSF de PLAY AUDIO MSF jugé au LBA qu'il désigne : les images
    //   moins 150 (§ 10.8.8). GetTrack compare des LBA.
    private static uint32_t msf_vers_lba_materiel(uint32_t frames)
    {
        ModeMateriel.Sonde[215]++;
        return unchecked(frames - 150);
    }

    // pcem bug, fixed in hardware mode: PB-214 — la piste d'une demande MSF cherchée à son LBA (le MSF converti, moins
    //   150), pas à la valeur compactée m × 65 536 + s × 256 + f : SFF-8020i r2.6 § 10.8.9 — la plage demandée est jouée
    //   si elle est audio. Hors de toute piste, l'attribut de PCem (zéro, PB-111) : la lecture part.
    private static uint8_t attribut_piste_materiel(uint32_t pos, int ismsf, uint8_t attr)
    {
        if (ismsf == 0 || cdrom is null)
            return attr;
        ModeMateriel.Sonde[214]++;
        var lba = MSFtoLBA((int)((pos >> 16) & 0xff), (int)((pos >> 8) & 0xff), (int)(pos & 0xff)) - 150;
        uint8_t a = 0;
        int numero = 0;
        TMSF t = default;
        cdrom.GetAudioTrackInfo(cdrom.GetTrack(lba), ref numero, ref t, ref a);
        return a;
    }

    // pcem bug, fixed in hardware mode: PB-273 — la plage [debut, fin) en LBA est-elle audio d'un bout à l'autre ? Chaque
    //   piste qu'elle touche (le lead-out exclu) doit l'être. Vrai pour une plage vide, ou sans image.
    internal static bool plage_audio_materiel(long debut, long fin)
    {
        if (cdrom is null || fin <= debut)
            return true;
        var pistes = cdrom.tracks;
        for (var i = 0; i < pistes.Count - 1; i++)
        {
            long a = pistes[i].start, b = pistes[i + 1].start;
            if (a < fin && debut < b && pistes[i].attr != AUDIO_TRACK)
                return false;
        }
        return true;
    }
}
