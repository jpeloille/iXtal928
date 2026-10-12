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
}
