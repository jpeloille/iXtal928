// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/sound/sound.c:57-62 (sound_card_available) + sound_sb.c:1349-1352 (les drapeaux de la
//         SB 16 et de l'AWE32)
// STATUS: host
//
// LES CARTES SON QU'ON PEUT MONTER, et sur quelle machine. PCem ne filtre ses cartes son que par le MCA et par
// leur ROM (wx-config.c:190-193) : ses device_t de la SB 16 et de l'AWE32 ont des drapeaux à 0, et son écran les
// offre sur un XT. LA RÈGLE ISA 16 BITS (PLAN-G12.md, décision n° 4 ; celle des cartes de disque, décision n° 3 de
// PLAN-G11.md) : une carte 16 bits n'est pas montée sur une machine sans MODEL_AT (8088, 8086) — sans
// dma16_init, son DMA 16 bits rendrait DMA_NODATA (dma.c:516-517) et sa sortie n'avancerait jamais. Une vraie
// SB 16 tolérait un connecteur 8 bits, sans DMA 16 bits ni IRQ haute : le refus est un choix de l'hôte.
// pc.check_sndcard l'applique à la machine finale, par toutes les entrées (.cfg, --model, --sndcard).

namespace iXtal26.Host;

internal static class SoundCards
{
    /// <summary>Les cartes ISA 16 bits du registre (sound.cs), par leur internal_name.</summary>
    private static readonly string[] Isa16 = ["sb16", "sbawe32"];

    internal static bool RequiresAtMachine(string internalName) => Array.IndexOf(Isa16, internalName) >= 0;

    /// <summary>La carte peut-elle être montée sur la machine courante ? « none » et le nom vide le peuvent
    /// toujours.</summary>
    internal static bool IsAvailable(string internalName) =>
        !RequiresAtMachine(internalName) || HardDiskControllers.CurrentMachineIsAt;
}
