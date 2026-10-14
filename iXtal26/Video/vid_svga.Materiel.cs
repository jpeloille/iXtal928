// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — le code propre au mode matériel du socle SVGA (G13 ; R10 de TRANSCRIPTION.md).
// STATUS: materiel
//
// Les corrections des défauts de PCem dans vid_svga.c, appelées par les gardes de vid_svga.cs et de vid_cl5429.cs
// (`if (materiel.pb_nn)`), seules sur leur ligne. La source, le cas qui discrimine et la panne qui le rougit sont dans
// l'entrée PB de PCEM_BUGS.md. Les références : IBM, PS/2 Hardware Interface Technical Reference — Common Interfaces
// (84F9735, 1990), p. 2-56 (CR00) ; Cirrus Logic, CL-GD542X Technical Reference Manual (1994), § 6.27, p. 6-34 (les
// verrous, chargés à chaque lecture de la mémoire d'affichage ; le cycle de mot qui vaut deux cycles d'octet, déduit).
// Les méthodes portent NoInlining (M2, G13.7c2).

using System.Runtime.CompilerServices;

namespace iXtal26.Video;

internal static partial class vid_svga
{
    /// <summary>PB-220 : CR00 est le total des caractères de la ligne moins 5 (IBM, p. 2-56), et non moins 6.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void htotal_materiel(svga_t svga)
    {
        ModeMateriel.Sonde[220]++;
        svga.htotal = svga.crtc[0] + 5;
    }

    /// <summary>Les quatre verrous chargés depuis le double mot de l'octet <paramref name="addr"/>, déjà réduit par
    /// decode_mask, comme svga_read le fait en chain4 compact (vid_svga.c:1085-1089) ; hors de la mémoire, ni l'octet
    /// ni les verrous.</summary>
    private static void verrous_materiel(svga_t svga, uint32_t addr)
    {
        if (addr >= svga.vram_max)
                return;
        uint32_t latch_addr = addr & svga.vram_mask & ~3u;
        svga.la = svga.vram[latch_addr];
        svga.lb = svga.vram[latch_addr | 0x1];
        svga.lc = svga.vram[latch_addr | 0x2];
        svga.ld = svga.vram[latch_addr | 0x3];
    }

    /// <summary>PB-80 : la lecture linéaire en chain4 compact (ou fb_only) charge les verrous, comme toute lecture de la
    /// mémoire d'affichage (Cirrus, p. 6-34). Les verrous de huit octets de la GD5429 restent ceux de PCem.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static uint8_t lit_compact_materiel(svga_t svga, uint32_t addr)
    {
        ModeMateriel.Sonde[80]++;
        addr &= svga.decode_mask;
        if (addr >= svga.vram_max)
                return 0xff;
        verrous_materiel(svga, addr);
        return svga.vram[addr & svga.vram_mask];
    }

    /// <summary>PB-221 : la lecture rapide d'un mot ou d'un double mot, par la banque, charge les verrous de son
    /// dernier octet, <paramref name="addr"/>, comme les lectures d'octet qu'elle remplace.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void verrous_banque_materiel(svga_t svga, uint32_t addr)
    {
        ModeMateriel.Sonde[221]++;
        verrous_materiel(svga, ((addr & svga.banked_mask) + svga.read_bank) & svga.decode_mask);
    }

    /// <summary>PB-221 : de même, par la fenêtre linéaire.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void verrous_lineaire_materiel(svga_t svga, uint32_t addr)
    {
        ModeMateriel.Sonde[221]++;
        verrous_materiel(svga, addr & svga.decode_mask);
    }

    /// <summary>PB-221 (b) : un mot ou un double mot écrit, ce sont deux ou quatre écritures d'octet (déduit de Cirrus,
    /// § 6.27, comme les lectures) : en mode d'écriture 1 à 3 (GR5 bits 0-1), ou sous une rotation (GR3 bits 0-2), les
    /// octets passent par svga_write, comme le chemin lent de la même fonction chez PCem. Faux : le chemin rapide de
    /// PCem, qui donne la même chose en mode 0 sans rotation.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool ecrit_banque_materiel(svga_t svga, uint32_t addr, uint32_t val, int n)
    {
        if (svga.writemode == 0 && (svga.gdcreg[3] & 7) == 0)
                return false;
        ModeMateriel.Sonde[221]++;
        for (int i = 0; i < n; i++)
                svga_write(addr + (uint32_t)i, (uint8_t)(val >> (8 * i)), svga);
        return true;
    }

    /// <summary>PB-221 (b) : de même, par la fenêtre linéaire (svga_write_linear, l'écriture d'octet que la fenêtre de la
    /// GD5429 prend dans la même plage, gd5429_writeb_linear). Sur la GD5429, GR0B bit 2 (le mode d'écriture étendu) ou
    /// bit 4 (l'adressage de 16 bits) changent son écriture d'octet par la banque (gd5429_write_linear), que
    /// svga_write_linear ignore : le chemin rapide de PCem reste alors.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool ecrit_lineaire_materiel(svga_t svga, uint32_t addr, uint32_t val, int n)
    {
        if (svga.writemode == 0 && (svga.gdcreg[3] & 7) == 0)
                return false;
        if (svga.p is gd5429_t && (svga.gdcreg[0xb] & 0x14) != 0)
                return false;
        ModeMateriel.Sonde[221]++;
        for (int i = 0; i < n; i++)
                svga_write_linear(addr + (uint32_t)i, (uint8_t)(val >> (8 * i)), svga);
        return true;
    }
}
