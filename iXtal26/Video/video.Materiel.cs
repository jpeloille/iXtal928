// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — le code propre au mode matériel commun aux cartes à 6845 (G13 ; R10 de TRANSCRIPTION.md).
// STATUS: materiel
//
// PB-89, la ligne bornée à elle-même : le balayage d'une ligne de la CGA, de la MDA, de l'Hercules, de l'EGA, de la M24
// et du PC1512, appelé par la garde de l'init de chaque carte (`if (materiel.pb_89)`), qui pose son balayage du mode
// matériel sur la minuterie de la carte. La source, le cas qui discrimine et la panne qui le rougit sont dans l'entrée
// PB-89 de PCEM_BUGS.md. La référence : Motorola, data sheet du MC6845 (R1 inférieur à R0). Les méthodes portent
// NoInlining (M2, G13.7c2).
//
// La copie des lignes suivantes vit dans une classe à part (LigneBornee) : pas de champ neuf dans video.

using System.Runtime.CompilerServices;

namespace iXtal26.Video;

internal static partial class video
{
    internal static class LigneBornee
    {
        /// <summary>Deux lignes de buffer32 : le plus long rendu de PCem (le blanc de l'EGA, 18 points par caractère
        /// pour un CR01 de 255, plus 32 de bordure) s'arrête à 4 639, avant la fin de la ligne n + 2.</summary>
        private static readonly uint32_t[] suivantes = new uint32_t[2 * Stride];

        /// <summary>Si un rendu de <paramref name="colonnes"/> caractères peut sortir de sa ligne : au plus 18 points
        /// par caractère et 48 de bordure, sur 2 048 (Stride). Les modes des BIOS (40 à 90 colonnes) restent en
        /// deçà.</summary>
        internal static bool deborde(int colonnes) => colonnes * 18 + 48 > Stride;

        /// <summary>PB-89 : le balayage d'une ligne, <paramref name="poll"/>, dont les points au-delà de la ligne
        /// <paramref name="displine"/> ne touchent pas les lignes suivantes de buffer32. Ce qui dépasse la ligne
        /// balayée tombe dans le retour de ligne, rien de la ligne n ne s'affiche en n + 1 (déduit de la data sheet du
        /// MC6845, R1 inférieur à R0) : les deux lignes suivantes, gardées avant le balayage, sont rendues après. Les
        /// points de la ligne elle-même restent ceux de PCem.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void bornee(timer_callback_t poll, object? p, int displine)
        {
            ModeMateriel.Sonde[89]++;
            int debut = (displine + 1) * Stride;
            Array.Copy(Buffer32, debut, suivantes, 0, suivantes.Length);
            poll(p);
            Array.Copy(suivantes, 0, Buffer32, debut, suivantes.Length);
        }
    }
}
