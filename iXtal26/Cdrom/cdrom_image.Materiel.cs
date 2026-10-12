// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — le code propre au mode matériel du moteur d'images de CD (G13 ; R10 de TRANSCRIPTION.md).
// STATUS: materiel
//
// Les corrections des défauts de PCem dans dosbox/cdrom_image.cpp, appelées par les gardes de cdrom_image.cs
// (`if (materiel.pb_nn)`), seules sur leur ligne. La source, le cas qui discrimine et la panne qui le rougit sont dans
// l'entrée PB de PCEM_BUGS.md. Les références : SFF-8020i révision 2.6 (22 janvier 1996) pour le secteur et la TOC ;
// le CDRWIN User's Guide (Golden Hawk Technology), annexe A, pour la feuille CUE.

namespace iXtal26.Cdrom;

internal sealed partial class CDROM_Interface_Image
{
    // pcem bug, fixed in hardware mode: PB-106 — un secteur de 2 336 octets commence au sous-en-tête : ses données
    //   sont à +8, pas à +24 comme dans un secteur brut de 2 352 (SFF-8020i r2.6, Figure 11). Les 24 déjà ajoutés
    //   par PCem, moins 16.
    private static int mode2_2336_materiel(int sectorSize, bool mode2, int seek)
    {
        if (!mode2 || sectorSize != 2336)
            return seek;
        ModeMateriel.Sonde[106]++;
        return seek - 16;
    }

    // pcem bug, fixed in hardware mode: PB-122 — le lead-out porte ADR 1 et le contrôle de la dernière piste (SFF-8020i
    //   r2.6 § 10.8.19 et Table 131) : l'octet de cette piste, 14h pour des données, 10h pour de l'audio. Appelée
    //   quand la dernière piste est déjà rangée.
    private int lead_out_materiel()
    {
        ModeMateriel.Sonde[122]++;
        return tracks[tracks.Count - 1].attr;
    }

    // pcem bug, fixed in hardware mode: PB-108 — les positions d'INDEX se comptent depuis le début du FILE (CDRWIN,
    //   annexe A) : une piste qui ouvre son fichier, la première de la feuille ou la première d'un nouveau FILE,
    //   commence dans le fichier à la position de son INDEX 01, quel que soit son INDEX 00. PCem en retranche
    //   l'INDEX 00 quand il le voit (prestart > 0), et rien quand il vaut 00:00:00 ou manque : le prégap est lu à la
    //   place de la piste. Dans le même fichier que la piste précédente, le décalage est celui de PCem, juste : la
    //   piste précédente finit à l'INDEX 00. Le compte de trames ; AddTrack le multiplie par la taille du secteur.
    private int index_01_materiel(Track curr, int skip)
    {
        if (tracks.Count != 0 && tracks[tracks.Count - 1].file == curr.file || curr.start == skip)
            return skip;
        ModeMateriel.Sonde[108]++;
        return curr.start;
    }

    // pcem bug, fixed in hardware mode: PB-123 — READ SUB-CHANNEL : la position relative compte depuis le début de la
    //   piste (SFF-8020i r2.6 § 10.8.18, Table 115), sans les 150 secteurs de l'amorce, que seule l'adresse absolue en
    //   MSF porte.
    private TMSF position_relative_materiel(int sector, int track)
    {
        ModeMateriel.Sonde[123]++;
        TMSF rel = default;
        FRAMES_TO_MSF(sector - tracks[track - 1].start, ref rel);
        return rel;
    }

    internal sealed partial class BinaryFile
    {
        // pcem bug, fixed in hardware mode: PB-109 — un lecteur qui échoue sur un secteur lit le suivant : l'échec ne
        //   colle pas au fichier. Les bits de l'ifstream effacés avant chaque accès, comme un clear() ; un fichier
        //   qui ne s'est pas ouvert reste en échec.
        private void clear_materiel()
        {
            if (file is null || !badbit && !eofbit && !failbit)
                return;
            ModeMateriel.Sonde[109]++;
            badbit = eofbit = failbit = false;
        }
    }
}
