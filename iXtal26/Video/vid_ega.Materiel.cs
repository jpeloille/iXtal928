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
using static iXtal26.timer;
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

    /// <summary>PB-99 (6) : les tables de la police, celles de SR3 (PCem), gardées dans la mémoire configurée. La carte
    /// de 64 Ko n'a que 16 Ko par plan (128 Ko : 32 Ko) ; les bits d'adresse qu'elle n'a pas sont perdus, la table 1
    /// lit la table 0 (déduit). Le décalage d'un caractère et de sa rangée (au plus 7FFCh) reste sous 10000h : masquer
    /// la table vaut masquer chaque adresse lue.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void police_materiel(ega_t ega, uint8_t val)
    {
        ModeMateriel.Sonde[99]++;
        ega.charsetb = (uint32_t)(((((val >> 2) & 3) * 0x10000) + 2) & ega.vrammask);
        ega.charseta = (uint32_t)((((val & 3) * 0x10000) + 2) & ega.vrammask);
    }

    /// <summary>PB-99 (6) : une ligne graphique, celle de PCem, sans les bits d'adresse que la carte n'a pas (déduit).
    /// L'adresse de début de ligne d'abord masquée par vrammask : PCem ne masque `ma` qu'après chaque caractère, et
    /// l'adresse de début de trame (CR0C:CR0D) peut dépasser la mémoire au premier caractère, en mode octet (CR17
    /// bit 6) ; dans la carte, ces bits n'existent pas. Puis, sur la carte de 64 Ko, la substitution de CR17 bit 1 (la
    /// rangée en MA14, le bit 0x10000 de l'adresse de PCem) tombe sur un bit absent : l'adresse, masquée avant la
    /// substitution, garde ce bit à 0, et le rendu de PCem, CR17 bit 1 tenu à 1 le temps de la ligne, rend ce que
    /// rendrait le masque appliqué après. Le registre, rendu ensuite, est celui que l'invité a écrit. La substitution
    /// de CR17 bit 0 (0x8000) reste dans la mémoire : celle de PCem.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void graphique_materiel(ega_t ega)
    {
        ModeMateriel.Sonde[99]++;
        ega.ma &= (uint32_t)ega.vrammask;
        uint8_t cr17 = ega.crtc[0x17];
        if ((ega.vrammask & 0x10000) == 0)
                ega.crtc[0x17] |= 2;
        switch (ega.gdcreg[5] & 0x20)
        {
        case 0x00:
                if ((ega.seqregs[1] & 8) != 0)
                        ega_draw_4bpp_lowres(ega);
                else
                        ega_draw_4bpp_highres(ega);
                break;
        case 0x20:
                ega_draw_2bpp(ega);
                break;
        }
        ega.crtc[0x17] = cr17;
    }

    /// <summary>PB-89 : le balayage de l'EGA, la ligne bornée à elle-même (LigneBornee) ; celui de PCem seul hors du
    /// rendu d'une ligne (linepos à 1) ou sous un CR01 qui ne peut pas sortir de la ligne.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void poll_materiel(object? p)
    {
        ega_t ega = (ega_t)p!;
        if (ega.linepos != 0 || !LigneBornee.deborde(ega.hdisp))
        {
                ega_poll(p);
                return;
        }
        LigneBornee.bornee(ega_poll, p, ega.displine);
    }

    /// <summary>PB-89 : la minuterie de la carte posée avec le balayage du mode matériel (poll_materiel), comme PCem la
    /// pose avec le sien.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ligne_add_materiel(ega_t ega) => timer_add(ega.timer, poll_materiel, ega, 1);
}
