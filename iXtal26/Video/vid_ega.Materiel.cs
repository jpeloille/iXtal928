// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — le code propre au mode matériel de l'EGA (G13 ; R10 de TRANSCRIPTION.md).
// STATUS: materiel
//
// Les corrections des défauts de PCem dans vid_ega.c, appelées par les gardes de vid_ega.cs (`if (materiel.pb_nn)`),
// seules sur leur ligne. La source, le cas qui discrimine et la panne qui le rougit sont dans l'entrée PB de
// PCEM_BUGS.md. La référence : IBM, Options and Adapters Technical Reference, vol. 2 (1984), EGA, pp. 30-37 et 56-61
// (les registres de l'attribut et du CRTC ; 10h bits 4-7, 07h bits 6-7, 09h et 0Ah bits 5-7, 11h bits 6-7 « Not
// Used »). Les méthodes portent NoInlining (M2, G13.7c2).

using System.Runtime.CompilerServices;
using static iXtal26.Video.video;

namespace iXtal26.Video;

internal static partial class vid_ega
{
    /// <summary>PB-99 (1) : la palette de l'EGA, six bits par registre de l'attribut (00h-0Fh) ; ni 10h bit 7 ni 14h, des
    /// registres de la VGA.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void palette_materiel(ega_t ega)
    {
        ModeMateriel.Sonde[99]++;
        for (int c = 0; c < 16; c++)
                ega.egapal[c] = (uint8_t)(ega.attrregs[c] & 0x3f);
    }

    /// <summary>PB-99 (2) : l'écriture d'un registre du CRTC, celle de PCem, sans la protection de CR0 à CR7 par CR11
    /// bit 7, un registre de la VGA (11h bits 6-7 « Not Used »).</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void crtc_ecrit_materiel(ega_t ega, uint8_t val)
    {
        ModeMateriel.Sonde[99]++;
        uint8_t old = ega.crtc[ega.crtcreg];
        ega.crtc[ega.crtcreg] = val;
        if (old != val)
        {
                if (ega.crtcreg < 0xe || ega.crtcreg > 0x10)
                {
                        fullchange = changeframecount;
                        ega_recalctimings(ega);
                }
        }
    }

    /// <summary>PB-99 (7) : les totaux verticaux de l'EGA, sans les bits 9 de la VGA (CR07 bits 5-7 et CR09 bit 6) :
    /// le bit 8 de chacun, de CR07 bits 0, 1, 2 et 4.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void bits9_materiel(ega_t ega)
    {
        ModeMateriel.Sonde[99]++;
        ega.vtotal = (ega.crtc[6] | ((ega.crtc[7] & 1) << 8)) + 1;
        ega.dispend = (ega.crtc[0x12] | ((ega.crtc[7] & 2) << 7)) + 1;
        ega.vsyncstart = (ega.crtc[0x10] | ((ega.crtc[7] & 4) << 6)) + 1;
        ega.split = (ega.crtc[0x18] | ((ega.crtc[7] & 0x10) << 4)) + 2;
    }

    /// <summary>PB-99 (5) : le texte, dessiné à chaque ligne comme la carte le balaie ; PCem ne le redessine que sur
    /// fullchange (un curseur, une police ou un attribut changés n'apparaissent qu'au rafraîchissement suivant).</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void texte_materiel(ega_t ega)
    {
        ModeMateriel.Sonde[99]++;
        ega_draw_text(ega);
    }

    /// <summary>PB-99 : le curseur de l'EGA, sans CR0A bit 5 (0Ah bits 5-7 « Not Used ») : la phase du compteur de
    /// trames seule.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void curseur_materiel(ega_t ega)
    {
        ModeMateriel.Sonde[99]++;
        ega.cursoron = ega.blink & 16;
    }
}
