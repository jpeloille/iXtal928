// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — le code propre au mode matériel de la Tseng ET4000AX (G13 ; R10 de TRANSCRIPTION.md).
// STATUS: materiel
//
// Les corrections des défauts de PCem dans vid_et4000.c, appelées par les gardes de vid_et4000.cs
// (`if (materiel.pb_nn)`), seules sur leur ligne. La source, le cas qui discrimine et la panne qui le rougit sont dans
// l'entrée PB de PCEM_BUGS.md. La référence : Tseng Labs, data book ET4000 Graphics Controller (1990), pp. 102-104 (le
// KEY, l'Input Status 0, le Feature Control), 108 (3BFh), 111 (la table 4.3-2 du CRTC), 136-137, 142, 144 et 158 (les
// registres que garde le KEY) ; le retrait du KEY, que le data book ne décrit pas, est celui du BIOS de la carte
// (et4000.bin, la routine en 1050h) ; pp. 103 (l'Input Status 1), 115-116 et 124-125 (le blanc), 135 (CR35), 142-143
// (TS 7 bit 7, la VGA ou l'EGA), 153 (l'index de l'ATC), 156-157 (11h et 12h). Les méthodes portent NoInlining (M2,
// G13.7c2).
//
// L'état que PCem n'a pas (le registre 3BFh, le KEY, 3CDh ouvert) vit dans une table à part (EtatEt4) : un champ de
// plus dans et4000_t changerait sa disposition.

using System.Runtime.CompilerServices;
using static iXtal26.io;
using static iXtal26.Video.vid_svga;
using static iXtal26.Video.video;

namespace iXtal26.Video;

internal static partial class vid_et4000
{
    private static class EtatEt4
    {
        internal sealed class Etat
        {
            /// <summary>Le registre de compatibilité Hercules, 3BFh, en écriture seule (data book, p. 108).</summary>
            internal uint8_t herc;

            /// <summary>Le KEY posé : les registres étendus s'écrivent. À la mise sous tension, son état est inconnu,
            /// comme les valeurs des registres qu'il garde (le data book n'en donne que quelques bits, pp. 136 et 142),
            /// que le BIOS de la carte écrit avant sa première pose puis relit (mesuré : ces écritures perdues, il pose
            /// CR37 à 03h au lieu de 0Fh) : jusqu'au premier retrait, tout s'écrit, comme chez PCem, sauf 3CDh
            /// (banque).</summary>
            internal bool cle = true;

            /// <summary>Le KEY posé au moins une fois depuis la mise sous tension : 3CDh s'écrit (p. 144).</summary>
            internal bool banque;
        }

        internal static readonly ConditionalWeakTable<et4000_t, Etat> Etats = new();
    }

    private static EtatEt4.Etat Cle(et4000_t et4000) => EtatEt4.Etats.GetOrCreateValue(et4000);

    /// <summary>PB-100 : 3BFh, le registre de compatibilité Hercules, que PCem ne décode pas (le gestionnaire de la
    /// carte couvre 3C0h-3DFh, et 3A0h-3BFh en mono seulement) : la pose du KEY l'écrit, en couleur comme en
    /// mono.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void cle_init_materiel(et4000_t et4000)
    {
        io_sethandler(0x03bf, 0x0001, null, null, null, herc_materiel, null, null, et4000);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void herc_materiel(uint16_t addr, uint8_t val, object? p)
    {
        Cle((et4000_t)p!).herc = val;
    }

    /// <summary>PB-100 : une écriture du registre de mode (3#8 : 3D8h en couleur, 3B8h en mono, l'adresse que PCem a
    /// déjà rabattue). A0h, 3BFh à 03h : le KEY posé (p. 102), et 3CDh ouvert ; 29h, 3BFh à 01h : le KEY retiré, la
    /// séquence du BIOS de la carte (déduit : le data book ne décrit pas le retrait). Les autres écritures, inconnues,
    /// laissent le KEY tel quel.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void cle_materiel(et4000_t et4000, uint8_t val)
    {
        var e = Cle(et4000);
        if (val == 0xa0 && e.herc == 0x03)
        {
                ModeMateriel.Sonde[100]++;
                e.cle = e.banque = true;
        }
        else if (val == 0x29 && e.herc == 0x01)
        {
                ModeMateriel.Sonde[100]++;
                e.cle = false;
        }
    }

    /// <summary>PB-100 : sans le KEY, les registres du CRTC au-delà de 18h ne s'écrivent pas, sauf 33h et 35h (p. 111 ;
    /// 32h, 34h, 36h et 37h « protected by key », pp. 136-137). Vrai : l'écriture est ignorée.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool crtc_cle_materiel(et4000_t et4000)
    {
        var r = et4000.svga.crtcreg;
        if (r <= 0x18 || r == 0x33 || r == 0x35 || Cle(et4000).cle)
                return false;
        ModeMateriel.Sonde[100]++;
        return true;
    }

    /// <summary>PB-100 : TS 6 et TS 7 « protected by KEY » (p. 142) : sans le KEY, leur écriture est ignorée. Vrai :
    /// l'écriture est ignorée.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool ts_cle_materiel(et4000_t et4000)
    {
        var i = et4000.svga.seqaddr;
        if ((i != 6 && i != 7) || Cle(et4000).cle)
                return false;
        ModeMateriel.Sonde[100]++;
        return true;
    }

    /// <summary>PB-100 : l'ATC 16h « protected by KEY » (p. 158) : sans le KEY, la donnée est ignorée ; la bascule
    /// index/donnée bascule comme pour toute écriture. Vrai : la bascule, celle de PCem.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool atc_cle_materiel(et4000_t et4000)
    {
        svga_t svga = et4000.svga;
        if (svga.attrff == 0 || svga.attraddr != 0x16 || Cle(et4000).cle)
                return false;
        ModeMateriel.Sonde[100]++;
        svga.attrff ^= 1;
        return true;
    }

    /// <summary>PB-100 : 3CDh ne s'écrit qu'après une pose du KEY depuis la mise sous tension (p. 144). Vrai :
    /// l'écriture est ignorée ; sa lecture, inconnue, reste celle de PCem. Le data book y ajoute le reset synchrone (TS
    /// 0 bit 1 à 0), mais à la lettre il fermerait 3CDh à chaque changement de mode du BIOS de la carte, qui pose le
    /// KEY, fait le reset, écrit 3CDh, puis retire le KEY, et le logiciel écrit 3CDh après ce changement de mode sans
    /// poser le KEY : la règle exacte est inconnue, et le reset ne referme rien, comme chez PCem.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool banque_cle_materiel(et4000_t et4000)
    {
        if (Cle(et4000).banque)
                return false;
        ModeMateriel.Sonde[100]++;
        return true;
    }

    /// <summary>PB-227 : CR35, le débordement vertical, est « protected by bit 7 of CRTC 11 » (table 4.3-2) : sous CR11
    /// bit 7, son écriture est ignorée. Vrai : l'écriture est ignorée.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool cr35_materiel(svga_t svga)
    {
        if (svga.crtcreg != 0x35 || (svga.crtc[0x11] & 0x80) == 0)
                return false;
        ModeMateriel.Sonde[227]++;
        return true;
    }

    /// <summary>PB-100 : CR13 nul vaut 0 (le décalage d'une rangée, data book Tseng) : la même rangée répétée,
    /// et non 256.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void cr13_materiel(svga_t svga)
    {
        if (svga.crtc[0x13] != 0)
                return;
        ModeMateriel.Sonde[100]++;
        svga.rowoffset = 0;
    }

    /// <summary>PB-226 : la ligne sous le faisceau, rendue par le rendu de la carte (svga.render), la palette remplacée
    /// par l'identité : les sorties de l'attribut, P0 à P7, au lieu des couleurs du DAC. Le rendu forcé (fullchange),
    /// l'adresse de début et les bornes du blit rendues ensuite.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void rendu_faisceau(object p)
    {
        svga_t svga = (svga_t)p;
        uint32_t ma = svga.ma;
        int premiere = svga.firstline_draw, derniere = svga.lastline_draw, complet = svga.fullchange;
        uint32_t[] pallook = svga.pallook;
        svga.ma &= svga.vram_display_mask;
        svga.fullchange = 1;
        svga.pallook = Faisceau.identite;
        svga.render!(svga);
        svga.pallook = pallook;
        svga.fullchange = complet;
        svga.firstline_draw = premiere;
        svga.lastline_draw = derniere;
        svga.ma = ma;
    }

    /// <summary>PB-226 : le point sous le faisceau, les huit sorties de l'attribut. La ligne a deux phases chez PCem :
    /// l'affichage, de la fin de son balayage (linepos à 0) au rendu de la ligne, hdisp_time caractères, et le reste,
    /// jusqu'au total (htotal) ; le caractère sous le faisceau, la part écoulée de la phase (la minuterie de la carte).
    /// Dans le blanc horizontal (CR02 ; CR03 bits 0-4, et CR05 bit 7 en VGA, pp. 115-116) ou vertical (vblankstart de
    /// PCem, CR15 et ses bits 8 à 10, plus un, p. 124 ; CR16, huit bits en VGA, cinq en EGA, p. 125), 0 ; l'index de
    /// l'ATC bit 5 à 0, la bordure partout (p. 153) ; dans l'affichage d'une ligne affichée, le point de la ligne
    /// rendue ; ailleurs, la bordure (11h, p. 156). La VGA ou l'EGA : TS 7 bit 7 (p. 143). Les sorties nulles pendant
    /// le blanc : déduit.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static uint32_t point_materiel(svga_t svga)
    {
        bool vga = (svga.seqregs[7] & 0x80) != 0;
        bool affichage = svga.linepos == 0;
        double f = Faisceau.part(svga.timer, affichage ? svga.dispontime : svga.dispofftime);
        int c = affichage ? (int)(f * svga.hdisp_time) : svga.hdisp_time + (int)(f * (svga.htotal - svga.hdisp_time));
        int hbe = vga ? (svga.crtc[3] & 0x1f) | ((svga.crtc[5] & 0x80) >> 2) : svga.crtc[3] & 0x1f;
        if (Faisceau.blanc(c, svga.crtc[2], hbe, vga ? 6 : 5, svga.htotal))
                return 0;
        if (Faisceau.blanc(svga.vc, svga.vblankstart, svga.crtc[0x16], vga ? 8 : 5, svga.vtotal))
                return 0;
        if (!affichage || svga.dispon == 0 || (svga.attr_palette_enable & 0x20) == 0)
                return svga.attrregs[0x11];
        uint32_t[] points = Faisceau.ligne(svga, svga.timer, svga.displine, svga.hdisp, rendu_faisceau);
        return points[32 + (int)(f * svga.hdisp)] & 0xff;
    }

    /// <summary>PB-226 : une lecture du socle (svga_in), dont 3DAh, l'Input Status 1 de l'ET4000 (p. 103) : le bit 7,
    /// le complément du retour vertical (le bit 3) ; les bits 4-5, deux des huit sorties de l'attribut choisies par 12h
    /// bits 4-5 : 00, P2 et P0, le point sous le faisceau (point_materiel). Les trois autres choix : le data book se
    /// contredit (p. 103, P5-P4, P3-P1, P7-P6, dans l'ordre de la table, dont la colonne des valeurs de 12h n'est pas
    /// lisible ; p. 157, SG-SB, PI-PG, SI-SR), inconnus ; et les modes de plus de 8 bits par point, dont le rendu ne
    /// passe pas par l'attribut : la bascule de PCem.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static uint8_t in_materiel(uint16_t addr, svga_t svga)
    {
        uint8_t v = svga_in(addr, svga);
        if (addr != 0x3DA)
                return v;
        ModeMateriel.Sonde[226]++;
        v = (uint8_t)((v & 0x7f) | ((svga.cgastat & 8) != 0 ? 0 : 0x80));
        if (((svga.attrregs[0x12] >> 4) & 3) != 0 || svga.bpp > 8)
                return v;
        int p = (int)point_materiel(svga);
        return (uint8_t)((v & ~0x30) | (((p >> 2) & 1) << 5) | ((p & 1) << 4));
    }
}
