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
// G13.9f, le faisceau (Faisceau) : le point sous le faisceau à l'instant d'une lecture de l'état de la carte (PB-99 (4)
// et PB-228 sur l'EGA, PB-224 sur la MDA et l'Hercules, PB-226 sur l'ET4000), tiré du temps de la ligne (la minuterie
// de la carte) et de sa mémoire d'affichage (la ligne rendue par la carte, sa palette remplacée par l'identité : les
// sorties de l'attribut, avant le DAC).
//
// La copie des lignes suivantes et la ligne du faisceau vivent dans des classes à part (LigneBornee, Faisceau) : pas de
// champ neuf dans video.

using System.Runtime.CompilerServices;
using static iXtal26.timer;

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

    internal static class Faisceau
    {
        /// <summary>La palette identité : chaque sortie de l'attribut (de 0 à 511, la taille des tables de la VGA)
        /// rendue telle quelle dans buffer32.</summary>
        internal static readonly uint32_t[] identite = Enumerable.Range(0, 512).Select(i => (uint32_t)i).ToArray();

        /// <summary>Les lignes gardées autour du rendu du faisceau : la ligne et les deux suivantes, où PB-89 peut
        /// déborder.</summary>
        private static readonly uint32_t[] gardees = new uint32_t[3 * Stride];

        /// <summary>Les points de la ligne sous le faisceau, rendus par la carte : la ligne et ses
        /// débordements.</summary>
        private static readonly uint32_t[] points = new uint32_t[3 * Stride];

        private static object? carte;
        private static uint64_t echeance;
        private static int ecritures;

        /// <summary>La part écoulée de la phase en cours de la ligne, de 0 à 1 exclu : la minuterie de la carte expire
        /// au bout de la phase, qui dure <paramref name="duree"/> (dispontime ou dispofftime).</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static double part(pc_timer_t timer, uint64_t duree)
        {
            if (duree == 0)
                return 0;
            double f = 1.0 - (double)timer_get_remaining_u64(timer) / duree;
            return f < 0 ? 0 : f >= 1 ? 0.999999 : f;
        }

        /// <summary>Les points de la ligne <paramref name="displine"/>, rendus par <paramref name="rendu"/> dans
        /// buffer32, la palette de la carte remplacée par l'identité ; buffer32 rendu tel qu'il était. Seule l'étendue
        /// que le rendu peut toucher est gardée, effacée, lue et rendue : les 32 points de la bordure de gauche, les
        /// <paramref name="largeur"/> points de la ligne et 128 de marge (le caractère de plus du défilement fin, au
        /// plus 18 points ; la ligne suivante seulement sous un R1 hors des modes des BIOS, PB-89 : le faisceau ne la
        /// borne pas, il lit le point que le rendu de PCem met là). Un rendu par phase de la ligne (l'échéance de la
        /// minuterie) et par écriture en mémoire d'affichage : egawrites, que comptent tous les chemins d'écriture de
        /// ces cartes (mda_write, hercules_write, ega_write ; svga_write, svga_writew, svga_writel et leurs variantes
        /// linéaires, et les chemins de PB-221 qui y retombent), global (une autre carte l'incrémente aussi : un rendu
        /// de trop, sans effet) et jamais remis à zéro dans ce port. Les lectures suivantes de la même phase reprennent
        /// les mêmes points ; un registre écrit au milieu de la phase ne se voit qu'à la phase suivante.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static uint32_t[] ligne(object p, pc_timer_t timer, int displine, int largeur, Action<object> rendu)
        {
            uint64_t ts = ((uint64_t)timer.ts_integer << 32) | timer.ts_frac;
            if (ReferenceEquals(p, carte) && ts == echeance && egawrites == ecritures)
                return points;
            int debut = displine * Stride;
            int n = Math.Min(Math.Min(gardees.Length, 32 + largeur + 128), Buffer32.Length - debut);
            Array.Copy(Buffer32, debut, gardees, 0, n);
            Array.Clear(Buffer32, debut, n);
            rendu(p);
            Array.Copy(Buffer32, debut, points, 0, n);
            Array.Copy(gardees, 0, Buffer32, debut, n);
            carte = p;
            echeance = ts;
            ecritures = egawrites;
            return points;
        }

        /// <summary>Si le compteur <paramref name="c"/> est dans l'intervalle de blanc qui commence à
        /// <paramref name="debut"/> et finit à la première valeur suivante du compteur dont les <paramref name="bits"/>
        /// bits bas valent ceux de <paramref name="fin"/> ; le compteur va de 0 à <paramref name="total"/> - 1, puis
        /// repart de 0 (les registres de blanc du CRTC de l'EGA et de l'ET4000).</summary>
        internal static bool blanc(int c, int debut, int fin, int bits, int total)
        {
            if (debut >= total || c >= total)
                return false;
            int m = (1 << bits) - 1;
            int k = debut + 1 + ((fin - (debut + 1)) & m);
            if (k < total)
                return c >= debut && c < k;
            return c >= debut || c < (fin & m);
        }
    }
}
