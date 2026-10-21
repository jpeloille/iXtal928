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
// Used ») ; pp. 14-17 (les registres d'état), 26-27 et 39-40 (le blanc), 36-37 (CR11), 59-60 (11h et 12h) ; le
// listing du BIOS, pp. 114 et 122 (POD14, OUT_DX). Les méthodes des corrections portent NoInlining (M2, G13.7c2) ;
// cavalier_p3 (G13.12a), appelée dans les deux modes, non.

using System.Runtime.CompilerServices;
using static iXtal26.io;
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

    /// <summary>PB-99 (4) : la ligne sous le faisceau, rendue par la carte telle que le mode matériel la
    /// dessine (le texte à chaque ligne, le rendu graphique dans sa mémoire ; l'écran éteint de SR1, des zéros), sa
    /// palette remplacée par l'identité : les sorties de l'attribut, six bits, au lieu des couleurs de
    /// l'hôte.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void rendu_faisceau(object p)
    {
        ega_t ega = (ega_t)p;
        uint32_t ma = ega.ma;
        uint32_t[] pallook = ega.pallook;
        ega.pallook = Faisceau.identite;
        if (ega.scrblank == 0)
        {
                if ((ega.gdcreg[6] & 1) == 0)
                        ega_draw_text(ega);
                else
                        graphique_materiel(ega);
        }
        ega.pallook = pallook;
        ega.ma = ma;
    }

    /// <summary>PB-99 (4) : le point sous le faisceau, les six sorties de l'attribut (P0 le bleu, P1 le vert, P2 le
    /// rouge, P3 à P5 les secondaires). La ligne a deux phases chez PCem : l'affichage, de la fin de son balayage
    /// (linepos à 0) au rendu de la ligne, qui dure hdisp caractères (CR01 + 1), et le reste, jusqu'au total (CR00 +
    /// 2) ; le caractère sous le faisceau, la part écoulée de la phase (la minuterie de la carte). Dans le blanc
    /// horizontal (CR02, CR03 bits 0-4) ou vertical (CR15 et CR07 bit 3, CR16 bits 0-4), 0 ; dans l'affichage d'une
    /// ligne affichée, le point de la ligne rendue ; ailleurs, la bordure (11h, six bits). Les sorties nulles pendant
    /// le blanc : déduit. Le décalage de l'affichage (CR03 bits 5-6) : non compté, PCem ne le compte pas.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static uint32_t point_materiel(ega_t ega)
    {
        int total = ega.crtc[0] + 2;
        bool affichage = ega.linepos == 0;
        double f = Faisceau.part(ega.timer, affichage ? ega.dispontime : ega.dispofftime);
        int c = affichage ? (int)(f * ega.hdisp) : ega.hdisp + (int)(f * (total - ega.hdisp));
        if (Faisceau.blanc(c, ega.crtc[2], ega.crtc[3], 5, total))
                return 0;
        if (Faisceau.blanc(ega.vc, ega.crtc[0x15] | ((ega.crtc[7] & 8) << 5), ega.crtc[0x16], 5, ega.vtotal))
                return 0;
        if (!affichage || ega.dispon == 0)
                return (uint32_t)(ega.attrregs[0x11] & 0x3f);
        int largeur = ega.hdisp * ((ega.seqregs[1] & 1) != 0 ? 8 : 9) * ((ega.seqregs[1] & 8) != 0 ? 2 : 1);
        uint32_t[] points = Faisceau.ligne(ega, ega.timer, ega.displine, largeur, rendu_faisceau);
        return points[32 + (int)(f * largeur)] & 0x3f;
    }

    /// <summary>PB-99 (8) : une écriture en 3C1h est une écriture de l'attribut, comme en 3C0h : le BIOS d'IBM y écrit
    /// la donnée (le listing, p. 122, OUT_DX : l'index en DX, la donnée en DX + 1 ; p. 114, DX = 3C0h dans POD14, pour
    /// la palette 0Fh et pour 12h), et la bascule de l'index et de la donnée revient à l'index après chaque paire. La
    /// carte ne distingue donc pas 3C0h et 3C1h en écriture (déduit du BIOS ; le décodage n'est pas lu au
    /// schéma).</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static uint16_t attribut_materiel(uint16_t addr)
    {
        if (addr != 0x3c1)
                return addr;
        ModeMateriel.Sonde[99]++;
        return 0x3c0;
    }

    /// <summary>PB-99 (4) : 3DAh, l'Input Status One de l'EGA. Les bits 4 et 5 sont deux des six sorties de l'attribut,
    /// choisies par 12h bits 4-5 (pp. 16-17, 60) : 00, le rouge et le bleu ; 01, le bleu secondaire et le vert ; 10, le
    /// rouge secondaire et le vert secondaire ; le point sous le faisceau (point_materiel), et non une bascule à chaque
    /// lecture. 11, « Not Used » : la bascule de PCem, inconnue. Les autres bits, ceux de PCem.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static uint8_t etat1_materiel(ega_t ega)
    {
        ModeMateriel.Sonde[99]++;
        ega.attrff = 0;
        int p;
        switch ((ega.attrregs[0x12] >> 4) & 3)
        {
        case 0:
                p = (int)point_materiel(ega);
                return (uint8_t)((ega.stat & ~0x30) | (((p >> 2) & 1) << 5) | ((p & 1) << 4));
        case 1:
                p = (int)point_materiel(ega);
                return (uint8_t)((ega.stat & ~0x30) | (((p >> 3) & 1) << 5) | (((p >> 1) & 1) << 4));
        case 2:
                p = (int)point_materiel(ega);
                return (uint8_t)((ega.stat & ~0x30) | (((p >> 5) & 1) << 5) | (((p >> 4) & 1) << 4));
        }
        ega.stat ^= 0x30;
        return ega.stat;
    }

    /// <summary>PB-228 : 3C2h, l'Input Status Zero de l'EGA : le bit 4, l'interrupteur de PCem ; le bit 7, « CRT
    /// Interrupt » (p. 15), 1 pendant l'affichage, 0 pendant le retour vertical (le bit 3 de 3DAh), quand CR11 bit 5
    /// autorise l'interruption (pp. 36-37). L'interruption interdite : le bit 7 de PCem, nul, inconnu. Le verrou de
    /// l'interruption (CR11 bit 4) et l'IRQ 2 : absents de PCem, hors de l'entrée.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static uint8_t etat0_materiel(ega_t ega)
    {
        ModeMateriel.Sonde[228]++;
        int v = egaswitchread switch
        {
                0xc => (egaswitches & 1) != 0 ? 0x10 : 0,
                0x8 => (egaswitches & 2) != 0 ? 0x10 : 0,
                0x4 => (egaswitches & 4) != 0 ? 0x10 : 0,
                0x0 => (egaswitches & 8) != 0 ? 0x10 : 0,
                _ => 0xff
        };
        if (v != 0xff && (ega.crtc[0x11] & 0x20) == 0 && (ega.stat & 8) == 0)
                v |= 0x80;
        return (uint8_t)v;
    }

    // ===== G13.12a — les cavaliers de l'EGA =====

    /// <summary>PB-294 : la liste de monitor_type — les six réglages de SW1-SW4 où l'EGA est la carte primaire
    /// (Options and Adapters, vol. 2, EGA, p. 81-82 ; le listing du BIOS, p. 108) : 6 et 7 (le Color Display), 8 et
    /// 9 (l'Enhanced Display, en émulation et en haute résolution), 10 et 11 (le monochrome, la CGA secondaire en 40
    /// ou 80 colonnes), ceux-ci dans les trois teintes de tube de PCem. PCem n'offre pas 8 ni 11. Les réglages 0 à 5
    /// (l'EGA secondaire, une MDA ou une CGA primaire à côté) restent refusés (Q30) : iXtal ne monte qu'une carte
    /// vidéo.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int[] moniteurs_materiel()
    {
        ModeMateriel.Sonde[294]++;
        int brut = PluginApi.config.config_get_int(PluginApi.config.CFG_MACHINE, ega_device.name, "monitor_type", 9);
        bool tube = (brut >> 4) is DISPLAY_GREEN or DISPLAY_AMBER or DISPLAY_WHITE;
        if (brut is >= 0 and <= 5 || (brut & 0xf) <= 5 && tube)
                Console.Error.WriteLine($"iXtal26 : [EGA] monitor_type = {brut} : l'EGA en carte secondaire (SW1-SW4 " +
                                        "de 0 à 5) suppose une MDA ou une CGA primaire ; iXtal ne monte qu'une carte " +
                                        "vidéo.");
        return [6, 7, 8, 9, 10 | (DISPLAY_WHITE << 4), 10 | (DISPLAY_GREEN << 4), 10 | (DISPLAY_AMBER << 4),
                11 | (DISPLAY_WHITE << 4), 11 | (DISPLAY_GREEN << 4), 11 | (DISPLAY_AMBER << 4)];
    }

    /// <summary>PB-294 : le monochrome, 10 et 11 (p. 81-82).</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int mono_materiel(int monitor_type)
    {
        ModeMateriel.Sonde[294]++;
        return (monitor_type & 0xf) is 10 or 11 ? 1 : 0;
    }

    /// <summary>G13.12a, dans les deux modes (Q25, Q31) : P3 sur les broches 2-3, toutes les adresses de la carte en
    /// 2XX (Options and Adapters, vol. 2, EGA, p. 85) : 2A0h-2DFh, rendus à ega_in et ega_out en 3XX, où le décodage de
    /// 3Bxh et 3Dxh par 3C2h bit 0 reste celui de la carte. « Operation of the adapter in the 2XX mode is not
    /// supported in BIOS » : le BIOS de la carte ne la trouve plus, et l'avertissement le dit.</summary>
    private static void cavalier_p3(ega_t ega)
    {
        Console.Error.WriteLine("iXtal26 : [EGA] io_base = 2XX (P3) : la carte répond en 2A0h-2DFh, que son BIOS ne " +
                                "gère pas (Options and Adapters, vol. 2, EGA, p. 85) ; le BIOS ne la trouvera pas.");
        io_sethandler(0x02a0, 0x0040, (addr, p) => ega_in((uint16_t)(addr + 0x100), p), null, null,
                      (addr, val, p) => ega_out((uint16_t)(addr + 0x100), val, p), null, null, ega);
    }
}
