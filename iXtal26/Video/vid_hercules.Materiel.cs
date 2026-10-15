// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — le code propre au mode matériel de l'Hercules (G13 ; R10 de TRANSCRIPTION.md).
// STATUS: materiel
//
// Les corrections des défauts de PCem dans vid_hercules.c, appelées par les gardes de vid_hercules.cs
// (`if (materiel.pb_nn)`), seules sur leur ligne. La source, le cas qui discrimine et la panne qui le rougit sont dans
// l'entrée PB de PCEM_BUGS.md. Les références : Hercules, GB101 Owner's Manual (p. 10, les temps d'un caractère ;
// pp. 12-13, 3BAh, le bit 3 « 1 = dots on ») ; Motorola, data sheet du MC6845 (la table des registres ; que la GB101
// porte un 6845 de cette famille : déduit, son manuel décrit ses registres pp. 8-9). Les méthodes portent NoInlining
// (M2, G13.7c2).
//
// L'état que PCem n'a pas (la valeur écrite dans chaque registre du 6845) vit dans une table à part (EtatHerc).

using System.Runtime.CompilerServices;
using iXtal26.Models;
using static iXtal26.Memory.mem;
using static iXtal26.Video.video;
using static iXtal26.timer;

namespace iXtal26.Video;

internal static partial class vid_hercules
{
    private static class EtatHerc
    {
        internal sealed class Etat
        {
            /// <summary>La valeur écrite dans chaque registre du 6845, avant son masque (PB-97, la relecture).</summary>
            internal readonly uint8_t[] brut = new uint8_t[32];
        }

        internal static readonly ConditionalWeakTable<hercules_t, Etat> Etats = new();
    }

    /// <summary>PB-97 : l'écriture d'un registre du 6845, à sa largeur (la table de la MDA), sans le correctif du
    /// « Generic Turbo XT ».</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void crtc_ecrit_materiel(hercules_t hercules, uint8_t val)
    {
        ModeMateriel.Sonde[97]++;
        EtatHerc.Etats.GetOrCreateValue(hercules).brut[hercules.crtcreg] = val;
        hercules.crtc[hercules.crtcreg] = (uint8_t)(val & vid_mda.Masque6845.largeur[hercules.crtcreg]);
        hercules_recalctimings(hercules);
    }

    /// <summary>PB-97 : la relecture d'un registre rend la valeur écrite, comme chez PCem (la vraie, inconnue).</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static uint8_t crtc_relu_materiel(hercules_t hercules) =>
        EtatHerc.Etats.GetOrCreateValue(hercules).brut[hercules.crtcreg];

    /// <summary>PB-102 : le caractère du 6845 dure 0,5625 µs en texte (9 points) et 1 µs en graphique (16 points), le
    /// manuel, p. 10 ; MDACONST en est un de 1 / 2,032125 MHz. Le graphique, comme hercules_poll le dessine : 3B8h bit 1
    /// et 3BFh bit 0. La conversion d'un temps négatif reste celle de PCem (PB-36).</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void temps_materiel(hercules_t hercules)
    {
        ModeMateriel.Sonde[102]++;
        bool graphique = (hercules.ctrl & 2) != 0 && (hercules.ctrl2 & 1) != 0;
        double caractere = pit.MDACONST * 2.032125 * (graphique ? 1.0 : 0.5625);
        double _dispontime = hercules.crtc[1] * caractere;
        double _dispofftime = (hercules.crtc[0] + 1 - hercules.crtc[1]) * caractere;
        hercules.dispontime = unchecked((uint64_t)(int64_t)_dispontime);
        hercules.dispofftime = unchecked((uint64_t)(int64_t)_dispofftime);
    }

    /// <summary>PB-102 : 3B8h écrit, comme chez PCem, puis les temps recalculés (le graphique change l'unité).</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ctrl_materiel(hercules_t hercules, uint8_t val)
    {
        hercules.ctrl = val;
        hercules_recalctimings(hercules);
    }

    /// <summary>PB-102 : 3BFh écrit, comme chez PCem (la projection de 32 ou de 64 Ko), puis les temps recalculés.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ctrl2_materiel(hercules_t hercules, uint8_t val)
    {
        hercules.ctrl2 = val;
        if ((val & 2) != 0)
                mem_mapping_set_addr(hercules.mapping, 0xb0000, 0x10000);
        else
                mem_mapping_set_addr(hercules.mapping, 0xb0000, 0x08000);
        hercules_recalctimings(hercules);
    }

    /// <summary>PB-225 : 3BAh, le bit 7 à 0 pendant le retour vertical et à 1 pendant l'affichage (manuel, pp. 12-13) ;
    /// le retour, celui que PCem pose au bit 3 (son sens, PB-224, reste celui de PCem).</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static uint8_t stat_materiel(hercules_t hercules)
    {
        ModeMateriel.Sonde[225]++;
        return (uint8_t)((hercules.stat & 0xf) | ((~hercules.stat & 8) << 4));
    }

    /// <summary>PB-89 : le balayage de l'Hercules, la ligne bornée à elle-même (LigneBornee) ; celui de PCem seul hors
    /// du rendu d'une ligne (linepos à 1) ou sous un R1 qui ne peut pas sortir de la ligne.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void poll_materiel(object? p)
    {
        hercules_t hercules = (hercules_t)p!;
        if (hercules.linepos != 0 || !LigneBornee.deborde(hercules.crtc[1]))
        {
                hercules_poll(p);
                return;
        }
        LigneBornee.bornee(hercules_poll, p, hercules.displine);
    }

    /// <summary>PB-89 : le balayage du mode matériel (poll_materiel) sur la minuterie de la carte.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ligne_init_materiel(hercules_t hercules) => timer_set_callback(hercules.timer, poll_materiel);

    /// <summary>PB-224 : une ligne de l'Hercules, le rendu de PCem (vid_hercules.c:153-188, inscrit dans
    /// hercules_poll), recopié pour le faisceau : le graphique (3B8h bit 1 et 3BFh bit 0), 16 points par caractère, la
    /// page de 3B8h bit 7 ; le texte, 9 points, comme la MDA.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ligne_faisceau(hercules_t hercules, int line, uint16_t ca)
    {
        if ((hercules.ctrl & 2) != 0 && (hercules.ctrl2 & 1) != 0)
        {
                ca = (uint16_t)((hercules.sc & 3) * 0x2000);
                if ((hercules.ctrl & 0x80) != 0 && (hercules.ctrl2 & 2) != 0)
                        ca += 0x8000;
                for (int x = 0; x < hercules.crtc[1]; x++)
                {
                        uint16_t dat = (uint16_t)((hercules.vram[((hercules.ma << 1) & 0x1fff) + ca] << 8) |
                                                  hercules.vram[((hercules.ma << 1) & 0x1fff) + ca + 1]);
                        hercules.ma++;
                        for (int c = 0; c < 16; c++)
                                Buffer32[line + (x << 4) + c] = (dat & (32768 >> c)) != 0 ? cgapal[0x7] : 0;
                }
                return;
        }
        for (int x = 0; x < hercules.crtc[1]; x++)
        {
                uint8_t chr = hercules.vram[(hercules.ma << 1) & 0xfff];
                uint8_t attr = hercules.vram[((hercules.ma << 1) + 1) & 0xfff];
                int drawcursor = ((hercules.ma == ca) && hercules.con != 0 && hercules.cursoron != 0) ? 1 : 0;
                int blink = ((hercules.blink & 16) != 0 && (hercules.ctrl & 0x20) != 0 && (attr & 0x80) != 0 &&
                             drawcursor == 0) ? 1 : 0;
                if (hercules.sc == 12 && ((attr & 7) == 1))
                {
                        for (int c = 0; c < 9; c++)
                                Buffer32[line + (x * 9) + c] = mdacol(attr, blink, 1);
                }
                else
                {
                        for (int c = 0; c < 8; c++)
                                Buffer32[line + (x * 9) + c] =
                                        mdacol(attr, blink,
                                               (vid_mda.fontdatm_plat(chr, hercules.sc) & (1 << (c ^ 7))) != 0 ? 1 : 0);
                        if ((chr & ~0x1f) == 0xc0)
                                Buffer32[line + (x * 9) + 8] =
                                        mdacol(attr, blink, vid_mda.fontdatm_plat(chr, hercules.sc) & 1);
                        else
                                Buffer32[line + (x * 9) + 8] = mdacol(attr, blink, 0);
                }
                hercules.ma++;
                if (drawcursor != 0)
                {
                        for (int c = 0; c < 9; c++)
                                Buffer32[line + (x * 9) + c] ^= mdacol(attr, 0, 1);
                }
        }
    }

    /// <summary>PB-224 : la ligne sous le faisceau, rendue comme hercules_poll la rend (ligne_faisceau), la rangée de
    /// l'entrelacé comme au balayage.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void rendu_faisceau(object p)
    {
        hercules_t hercules = (hercules_t)p;
        uint16_t ma = hercules.ma;
        int sc = hercules.sc;
        if ((hercules.crtc[8] & 3) == 3)
                hercules.sc = (hercules.sc << 1) & 7;
        ligne_faisceau(hercules, hercules.displine * Stride,
                       (uint16_t)((hercules.crtc[15] | (hercules.crtc[14] << 8)) & 0x3fff));
        hercules.sc = sc;
        hercules.ma = ma;
    }

    /// <summary>PB-224 : le point sous le faisceau, allumé ou non. Dans l'affichage d'une ligne affichée (linepos à 0,
    /// de la fin de son balayage au rendu), le point de la ligne rendue, R1 caractères de 16 points en graphique et de
    /// 9 en texte, sous la part écoulée de la phase (la minuterie de la carte) ; ailleurs, éteint : la vidéo est
    /// éteinte pendant les retours (déduit).</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool point_materiel(hercules_t hercules)
    {
        if (hercules.linepos != 0 || hercules.dispon == 0)
                return false;
        int largeur = hercules.crtc[1] * ((hercules.ctrl & 2) != 0 && (hercules.ctrl2 & 1) != 0 ? 16 : 9);
        double f = Faisceau.part(hercules.timer, hercules.dispontime);
        uint32_t[] points = Faisceau.ligne(hercules, hercules.timer, hercules.displine, largeur, rendu_faisceau);
        return points[(int)(f * largeur)] != cgapal[0];
    }

    /// <summary>PB-224 : 3BAh, le bit 3, « 1 = dots on » (pp. 12-13), le point sous le faisceau (point_materiel), et
    /// non le retour vertical. Le bit 7, celui de PB-225 quand il est corrigé (le retour vertical que PCem pose au bit
    /// 3 de son état), sinon celui de PCem ; les autres bits, ceux de PCem.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static uint8_t etat_materiel(hercules_t hercules)
    {
        ModeMateriel.Sonde[224]++;
        uint8_t v = materiel.pb_225 ? stat_materiel(hercules)
                                    : (uint8_t)((hercules.stat & 0xf) | ((hercules.stat & 8) << 4));
        return (uint8_t)((v & ~8) | (point_materiel(hercules) ? 8 : 0));
    }
}
