// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — le code propre au mode matériel de la MDA (G13 ; R10 de TRANSCRIPTION.md).
// STATUS: materiel
//
// Les corrections des défauts de PCem dans vid_mda.c, appelées par les gardes de vid_mda.cs (`if (materiel.pb_nn)`),
// seules sur leur ligne. La source, le cas qui discrimine et la panne qui le rougit sont dans l'entrée PB de
// PCEM_BUGS.md. Les références : IBM, Options and Adapters Technical Reference, vol. 2 (1984), MDA (p. 1-3, le schéma,
// feuilles 2, 3 et 5) ; Motorola, data sheet du MC6845 (la table des registres). Les méthodes portent NoInlining (M2,
// G13.7c2).
//
// L'état que PCem n'a pas (la valeur écrite dans chaque registre du 6845, que la relecture rend) vit dans une table à
// part (EtatMda) : un champ de plus dans mda_t changerait sa disposition.

using System.Runtime.CompilerServices;
using iXtal26.Models;
using static iXtal26.Video.video;

namespace iXtal26.Video;

internal static partial class vid_mda
{
    private static class EtatMda
    {
        internal sealed class Etat
        {
            /// <summary>La valeur écrite dans chaque registre du 6845, avant son masque (PB-97, la relecture).</summary>
            internal readonly uint8_t[] brut = new uint8_t[32];
        }

        internal static readonly ConditionalWeakTable<mda_t, Etat> Etats = new();
    }

    /// <summary>La table du 6845, dans une classe à part : un champ statique de plus dans vid_mda changerait son
    /// constructeur statique (M2).</summary>
    internal static class Masque6845
    {
        /// <summary>PB-97 : la largeur des registres R0 à R15 du MC6845 (data sheet, la table des registres) : R3 sur 4
        /// bits (la largeur de la synchronisation horizontale ; la verticale est fixe chez Motorola), R4, R6, R7 et R10
        /// sur 7, R5, R9 et R11 sur 5, R8 sur 6 (les bits 0-1 et 4-7), R12 et R14 sur 6 ; R0 à R2, R13 et R15 sur 8. R16
        /// et au-delà ne sont pas des registres à écrire : ce que PCem en fait reste (FFh).</summary>
        internal static readonly uint8_t[] largeur =
        [
            0xff, 0xff, 0xff, 0x0f, 0x7f, 0x1f, 0x7f, 0x7f, 0xf3, 0x1f, 0x7f, 0x1f, 0x3f, 0xff, 0x3f, 0xff,
            0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff,
        ];
    }

    /// <summary>PB-97 : l'écriture d'un registre du 6845, à sa largeur, sans le correctif du « Generic Turbo XT » (le
    /// curseur que le logiciel pose reste le sien : le BIOS d'IBM envoie CX tel quel à R10-R11).</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void crtc_ecrit_materiel(mda_t mda, uint8_t val)
    {
        ModeMateriel.Sonde[97]++;
        EtatMda.Etats.GetOrCreateValue(mda).brut[mda.crtcreg] = val;
        mda.crtc[mda.crtcreg] = (uint8_t)(val & Masque6845.largeur[mda.crtcreg]);
        mda_recalctimings(mda);
    }

    /// <summary>PB-97 : la relecture d'un registre rend la valeur écrite, comme chez PCem (la vraie, inconnue).</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static uint8_t crtc_relu_materiel(mda_t mda) => EtatMda.Etats.GetOrCreateValue(mda).brut[mda.crtcreg];

    /// <summary>PB-102 : un caractère de 9 points à 16,257 MHz (IBM, p. 2 et le schéma, feuille 3), et non de 8 :
    /// MDACONST × 9/8, comme l'EGA en mode 9 points (vid_ega.c:231). La conversion d'un temps négatif reste celle de
    /// PCem (PB-36).</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void temps_materiel(mda_t mda)
    {
        ModeMateriel.Sonde[102]++;
        double caractere = pit.MDACONST * (9.0 / 8.0);
        double _dispontime = mda.crtc[1] * caractere;
        double _dispofftime = (mda.crtc[0] + 1 - mda.crtc[1]) * caractere;
        mda.dispontime = unchecked((uint64_t)(int64_t)_dispontime);
        mda.dispofftime = unchecked((uint64_t)(int64_t)_dispofftime);
    }

    /// <summary>PB-96 et PB-222 : une ligne de texte, celle de PCem, sauf, selon leurs drapeaux, la police lue à la
    /// rangée `sc & 15` (quatre lignes RA vont au générateur de caractères, p. 3) et le curseur qui force les points de
    /// la cellule à 1 (feuille 5, U3 et U43 : un pavé plein, de la couleur d'avant-plan de l'attribut). Le débordement
    /// de PB-89 au-delà de R1 = 227 reste celui de PCem.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ligne_materiel(mda_t mda, int line, uint16_t ca)
    {
        int rangee = mda.sc;
        if (materiel.pb_96)
        {
                ModeMateriel.Sonde[96]++;
                rangee &= 15;
        }
        bool plein = materiel.pb_222;
        if (plein)
                ModeMateriel.Sonde[222]++;
        for (int x = 0; x < mda.crtc[1]; x++)
        {
                uint8_t chr = mda.vram[(mda.ma << 1) & 0xfff];
                uint8_t attr = mda.vram[((mda.ma << 1) + 1) & 0xfff];
                int drawcursor = ((mda.ma == ca) && mda.con != 0 && mda.cursoron != 0) ? 1 : 0;
                int blink = ((mda.blink & 16) != 0 && (mda.ctrl & 0x20) != 0 && (attr & 0x80) != 0 && drawcursor == 0) ? 1 : 0;
                if (mda.sc == 12 && ((attr & 7) == 1))
                {
                        for (int c = 0; c < 9; c++)
                                Buffer32[line + (x * 9) + c] = mdacol(attr, blink, 1);
                }
                else
                {
                        for (int c = 0; c < 8; c++)
                                Buffer32[line + (x * 9) + c] =
                                        mdacol(attr, blink, (fontdatm_plat(chr, rangee) & (1 << (c ^ 7))) != 0 ? 1 : 0);
                        if ((chr & ~0x1f) == 0xc0)
                                Buffer32[line + (x * 9) + 8] = mdacol(attr, blink, fontdatm_plat(chr, rangee) & 1);
                        else
                                Buffer32[line + (x * 9) + 8] = mdacol(attr, blink, 0);
                }
                mda.ma++;
                if (drawcursor != 0)
                {
                        if (plein)
                        {
                                for (int c = 0; c < 9; c++)
                                        Buffer32[line + (x * 9) + c] = mdacol(attr, 0, 1);
                        }
                        else
                        {
                                for (int c = 0; c < 9; c++)
                                        Buffer32[line + (x * 9) + c] ^= mdacol(attr, 0, 1);
                        }
                }
        }
    }

    /// <summary>PB-223 : le curseur clignote sur 16 trames (feuille 5, U28 : la sortie QD du premier compteur), le bit 3
    /// du compteur de trames ; les caractères, sur 32, restent au bit 4.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void curseur_materiel(mda_t mda)
    {
        if ((mda.crtc[10] & 0x60) == 0x20)
                return;
        ModeMateriel.Sonde[223]++;
        mda.cursoron = mda.blink & 8;
    }
}
