// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — le code propre au mode matériel du fil CD (G13 ; R10 de TRANSCRIPTION.md).
// STATUS: materiel
//
// Les corrections des défauts de PCem dans sound.c, appelées par les gardes de sound.cs (`if (materiel.pb_nn)`), seules
// sur leur ligne. La source, le cas qui discrimine et la panne qui le rougit sont dans l'entrée PB de PCEM_BUGS.md. Les
// méthodes portent NoInlining (M2, G13.7c2).
//
// Le pont du CD (G13.8e2, PB-148) : la sortie audio du lecteur entre dans la carte son (le câble du CD), qui
// l'enregistre par son mélangeur d'entrée. PCem ne la mène qu'à la sortie de l'hôte, par blocs de 100 ms ; le tampon de
// la carte lit ici le bloc du lecteur à l'instant de chacun de ses échantillons de 48 kHz.

using System.Runtime.CompilerServices;

namespace iXtal26.Sound;

internal static partial class sound
{
    // ===== G13.8e2 — le pont du CD =====

    /// <summary>Les deux derniers blocs du lecteur (CD_BUFLEN trames stéréo à 44,1 kHz, 100 ms), tels qu'il les sort :
    /// le volume et le choix des voies de l'ATAPI appliqués, ni le volume de la carte ni celui de l'hôte. Le bloc
    /// courant a commencé au dernier tic du CD (cd_pos à 0) ; le précédent, 4 800 trames de 48 kHz plus tôt.</summary>
    private static class PontCd
    {
        internal static int16_t[] bloc = new int16_t[CD_BUFLEN * 2];
        internal static int16_t[] precedent = new int16_t[CD_BUFLEN * 2];
    }

    /// <summary>PB-148 : sound_cd_tick de PCem, à la lettre, et le bloc que sort le lecteur gardé pour la carte (le
    /// volume et le choix des voies de l'ATAPI, saturés à 16 bits, même sans soundon : la sortie de l'hôte n'est pas
    /// celle du lecteur). Ses commentaires sont ceux de la boucle du fil de PCem. Vrai : le tic est fait.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool cd_tick_materiel()
    {
        int c;

        Array.Clear(cd_buffer);
        // omitted: ioctl_audio_callback(cd_buffer, CD_BUFLEN * 2) (sound.c:150) — le lecteur physique, exclu.
        Cdrom.cdrom_image.image_audio_callback(cd_buffer, CD_BUFLEN * 2);
        lecteur_materiel();
        if (soundon != 0)
        {
                int32_t atapi_vol_l = (int32_t)Scsi.scsi_cd_c.atapi_get_cd_volume(0);
                int32_t atapi_vol_r = (int32_t)Scsi.scsi_cd_c.atapi_get_cd_volume(1);
                Span<int> channel_select = stackalloc int[2];

                channel_select[0] = (int)Scsi.scsi_cd_c.atapi_get_cd_channel(0);
                channel_select[1] = (int)Scsi.scsi_cd_c.atapi_get_cd_channel(1);

                for (c = 0; c < CD_BUFLEN * 2; c += 2)
                {
                        int32_t cd_buffer_temp0 = 0, cd_buffer_temp1 = 0;

                        /*First, adjust input from drive according to ATAPI volume.*/
                        cd_buffer[c] = (int16_t)(((int32_t)cd_buffer[c] * atapi_vol_l) / 255);
                        cd_buffer[c + 1] = (int16_t)(((int32_t)cd_buffer[c + 1] * atapi_vol_r) / 255);

                        /*Apply ATAPI channel select*/
                        if ((channel_select[0] & 1) != 0)
                                cd_buffer_temp0 += cd_buffer[c];
                        if ((channel_select[0] & 2) != 0)
                                cd_buffer_temp1 += cd_buffer[c];
                        if ((channel_select[1] & 1) != 0)
                                cd_buffer_temp0 += cd_buffer[c + 1];
                        if ((channel_select[1] & 2) != 0)
                                cd_buffer_temp1 += cd_buffer[c + 1];

                        /*Apply sound card CD volume*/
                        cd_buffer_temp0 = (cd_buffer_temp0 * (int)cd_vol_l) / 65535;
                        cd_buffer_temp1 = (cd_buffer_temp1 * (int)cd_vol_r) / 65535;

                        if (cd_buffer_temp0 > 32767)
                                cd_buffer_temp0 = 32767;
                        if (cd_buffer_temp0 < -32768)
                                cd_buffer_temp0 = -32768;
                        if (cd_buffer_temp1 > 32767)
                                cd_buffer_temp1 = 32767;
                        if (cd_buffer_temp1 < -32768)
                                cd_buffer_temp1 = -32768;

                        cd_buffer[c] = (int16_t)cd_buffer_temp0;
                        cd_buffer[c + 1] = (int16_t)cd_buffer_temp1;
                }

                givealbuffer_cd(cd_buffer);
        }
        return true;
    }

    /// <summary>PB-148 : le bloc brut que vient de rendre le pilote d'images (cd_buffer), tel que le lecteur le sort —
    /// le volume et le choix des voies de l'ATAPI, comme la boucle de PCem les applique, saturés à 16 bits (le CNA du
    /// lecteur, déduit) — devient le bloc courant du pont ; l'ancien, le précédent.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void lecteur_materiel()
    {
        int32_t vol_l = (int32_t)Scsi.scsi_cd_c.atapi_get_cd_volume(0);
        int32_t vol_r = (int32_t)Scsi.scsi_cd_c.atapi_get_cd_volume(1);
        int sel_l = (int)Scsi.scsi_cd_c.atapi_get_cd_channel(0);
        int sel_r = (int)Scsi.scsi_cd_c.atapi_get_cd_channel(1);
        (PontCd.precedent, PontCd.bloc) = (PontCd.bloc, PontCd.precedent);
        for (int c = 0; c < CD_BUFLEN * 2; c += 2)
        {
                int32_t l = (int16_t)((cd_buffer[c] * vol_l) / 255), r = (int16_t)((cd_buffer[c + 1] * vol_r) / 255);
                int32_t g = ((sel_l & 1) != 0 ? l : 0) + ((sel_r & 1) != 0 ? r : 0);
                int32_t d = ((sel_l & 2) != 0 ? l : 0) + ((sel_r & 2) != 0 ? r : 0);
                PontCd.bloc[c] = (int16_t)Math.Clamp(g, -32768, 32767);
                PontCd.bloc[c + 1] = (int16_t)Math.Clamp(d, -32768, 32767);
        }
    }

    /// <summary>PB-148 : en tête de sound_cd_raz (l'amorçage) — le lecteur n'a encore rien sorti.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void cd_raz_materiel()
    {
        Array.Clear(PontCd.bloc);
        Array.Clear(PontCd.precedent);
    }

    /// <summary>PB-148 : la trame que sort le lecteur <paramref name="recul"/> trames de 48 kHz avant l'instant (0 : le
    /// tic de son en cours, le dernier échantillon du tampon que rend la carte), la gauche et la droite ; au prorata
    /// 44 100 / 48 000 dans son bloc ; avant le bloc précédent, le silence.</summary>
    internal static void cd_trame_materiel(int recul, out int32_t g, out int32_t d)
    {
        int p = cd_pos - recul;
        int16_t[] b = PontCd.bloc;
        if (p < 0)
        {
                p += (CD_BUFLEN * 48000) / CD_FREQ;
                b = PontCd.precedent;
        }
        // Défensif : len ne dépasse pas MAXSOUNDBUFLEN (4 800), le recul jamais le bloc précédent.
        if (p < 0)
        {
                g = d = 0;
                return;
        }
        int f = (int)((long)p * CD_FREQ / 48000) * 2;
        g = b[f];
        d = b[f + 1];
    }
}
