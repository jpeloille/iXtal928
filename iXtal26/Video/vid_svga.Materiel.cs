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
// verrous, chargés à chaque lecture de la mémoire d'affichage). Les méthodes portent NoInlining (M2, G13.7c2).

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
}
