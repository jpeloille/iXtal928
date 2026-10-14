// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — le code propre au mode matériel du RAMDAC de l'ET4000 (G13 ; R10 de TRANSCRIPTION.md).
// STATUS: materiel
//
// Les corrections des défauts de PCem dans vid_unk_ramdac.c, appelées par les gardes de vid_unk_ramdac.cs
// (`if (materiel.pb_nn)`), seules sur leur ligne. La référence : Sierra, data sheet SC15025/SC15026 HiCOLOR-24,
// pp. 3-88/3-89 (l'IPF). Les méthodes portent NoInlining (M2, G13.7c2).

using System.Runtime.CompilerServices;

namespace iXtal26.Video;

internal static partial class vid_unk_ramdac
{
    /// <summary>PB-229 : l'IPF, armé par quatre lectures de 3C6h, ne retombe qu'à la mise sous tension, à une écriture ou
    /// à la lecture d'une autre adresse (pp. 3-88/3-89) : chaque lecture de plus rend encore le registre de
    /// commande.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static uint8_t ipf_materiel(unk_ramdac_t ramdac)
    {
        ModeMateriel.Sonde[229]++;
        return ramdac.ctrl;
    }
}
