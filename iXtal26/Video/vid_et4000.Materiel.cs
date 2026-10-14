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
// (et4000.bin, la routine en 1050h). Les méthodes portent NoInlining (M2, G13.7c2).
//
// L'état que PCem n'a pas (le registre 3BFh, le KEY, 3CDh ouvert) vit dans une table à part (EtatEt4) : un champ de
// plus dans et4000_t changerait sa disposition.

using System.Runtime.CompilerServices;
using static iXtal26.io;

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
}
