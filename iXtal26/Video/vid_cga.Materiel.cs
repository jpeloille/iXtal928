// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — le code propre au mode matériel de la CGA (G13 ; R10 de TRANSCRIPTION.md).
// STATUS: materiel
//
// Les corrections des défauts de PCem dans vid_cga.c, appelées par les gardes de vid_cga.cs (`if (materiel.pb_nn)`),
// seules sur leur ligne. La source, le cas qui discrimine et la panne qui le rougit sont dans l'entrée PB de
// PCEM_BUGS.md. Les références : IBM, Options and Adapters Technical Reference, vol. 2 (1984), CGA (p. 15-17 ; le schéma,
// feuilles 1 et 3) ; Motorola, data sheet du MC6845. Les méthodes portent NoInlining (M2, G13.7c2).

using System.Runtime.CompilerServices;
using static iXtal26.Video.video;

namespace iXtal26.Video;

internal static partial class vid_cga
{
    /// <summary>PB-04, PB-222 et PB-223 : une ligne de texte, celle de PCem en 80 ou en 40 colonnes, sauf, selon leurs
    /// drapeaux : la cellule du curseur, sur ses lignes, jamais éteinte par le clignotement (feuille 1 : +ALPHA DOTS, le
    /// NAND de -CURSOR DLY ; l'exemption `(ma == ca) && con`, sans la phase du curseur, R10 ne masquant pas le
    /// curseur) ; le curseur qui force les points
    /// à 1, un pavé de la couleur d'avant-plan (feuille 1 : le curseur en OU dans +ALPHA DOTS) ; les caractères qui
    /// clignotent sur 32 trames, le bit 4 du compteur (feuille 3, U12 : -BLINK, la sortie QA du second compteur). Le
    /// débordement de PB-89 au-delà de R1 = 127 (254 en 80 colonnes) reste celui de PCem.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void texte_materiel(cga_t cga, uint16_t ca, bool colonnes80)
    {
        bool exemption = materiel.pb_04, plein = materiel.pb_222;
        int rythme = materiel.pb_223 ? 16 : 8;
        if (exemption)
                ModeMateriel.Sonde[4]++;
        if (plein)
                ModeMateriel.Sonde[222]++;
        if (materiel.pb_223)
                ModeMateriel.Sonde[223]++;
        int line = cga.displine * Stride;
        for (int x = 0; x < cga.crtc[1]; x++)
        {
                uint8_t chr, attr;
                if ((cga.cgamode & 8) != 0)
                {
                        if (colonnes80)
                        {
                                chr = cga.charbuffer[x << 1];
                                attr = cga.charbuffer[(x << 1) + 1];
                        }
                        else
                        {
                                chr = cga.vram[((cga.ma << 1) & 0x3fff)];
                                attr = cga.vram[(((cga.ma << 1) + 1) & 0x3fff)];
                        }
                }
                else
                        chr = attr = 0;
                bool drawcursor = cga.ma == ca && cga.con != 0 && cga.cursoron != 0;
                // La cellule que le 6845 marque CURSOR sur ses lignes, quelle que soit la phase du curseur ; R10 bits 5-6
                // à 01 : il n'émet rien (ses deux modes de clignotement propres, PB-231, restent ceux de PCem).
                bool cellule = cga.ma == ca && cga.con != 0 && (cga.crtc[10] & 0x60) != 0x20;
                uint32_t fg, bg;
                if ((cga.cgamode & 0x20) != 0)
                {
                        fg = (uint32_t)(attr & 15);
                        bg = (uint32_t)((attr >> 4) & 7);
                        if ((cga.cgablink & rythme) != 0 && (attr & 0x80) != 0 && !(exemption && cellule))
                                fg = bg;
                }
                else
                {
                        fg = (uint32_t)(attr & 15);
                        bg = (uint32_t)(attr >> 4);
                }
                cga.ma++;
                int rangee = fontdat[((chr + cga.fontbase) << 3) | (cga.sc & 7)];
                for (int c = 0; c < 8; c++)
                {
                        uint32_t point = (rangee & (1 << (c ^ 7))) != 0 ? fg : bg;
                        if (drawcursor)
                                point = plein ? fg : point ^ 0xffffffu;
                        if (colonnes80)
                                Buffer32[line + (x << 3) + c + 8] = point;
                        else
                                Buffer32[line + (x << 4) + (c << 1) + 8] = Buffer32[line + (x << 4) + (c << 1) + 1 + 8] = point;
                }
        }
    }

    /// <summary>PB-230 : R16 et R17, le crayon optique, sont en lecture seule sur le MC6845 (data sheet ; le 6845 de
    /// Motorola est celui de la CGA, IBM p. 5) : leur écriture ne change rien. Vrai : l'écriture est ignorée.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool crayon_materiel(cga_t cga)
    {
        if (cga.crtcreg != 16 && cga.crtcreg != 17)
                return false;
        ModeMateriel.Sonde[230]++;
        return true;
    }
}
