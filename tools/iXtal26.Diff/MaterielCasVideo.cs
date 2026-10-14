// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — outillage de vérification.
//
// Les cas du socle SVGA (G13.9a, PLAN-G13.md) : PB-220 (CR00 + 5), PB-80 (la lecture en chain4 compact charge les
// verrous) et la part (a) de PB-221 (les lectures rapides de mot et de double mot chargent les verrous). Le banc de la
// vidéo : une ami486 montée avec la carte du cas, sans BIOS lancé ; les registres posés par les ports, comme un pilote
// le ferait, la mémoire d'affichage lue et écrite par ses projections (mem_readb_phys et les suivantes), la VRAM et les
// verrous lus dans svga_t.

using iXtal26.Memory;
using iXtal26.Video;

namespace iXtal26.Diff;

internal static partial class MaterielCas
{
    /// <summary>Un cas sur une ami486 montée avec la carte <paramref name="carte"/> (son internal_name).</summary>
    private static int AvecSvga(string carte, Func<svga_t, int> corps) => AvecCd(d =>
    {
        var cfg = Path.Combine(d, "video.cfg");
        File.WriteAllText(cfg, $"model = ami486\ncpu = 10\nmem_size = 4096\ngfxcard = {carte}\nsndcard = none\n");
        try
        {
            if (!pc.loadconfig(cfg))
                throw new InvalidOperationException("loadconfig");
            Floppy.fdd_c.discfns[0] = Floppy.fdd_c.discfns[1] = "";
            for (var u = 0; u < 4; u++)
                Disc.hdd_c.ide_fn[u] = "";
            if (!pc.initpc(RomsBanc))
                throw new InvalidOperationException("initpc");
            var svga = vid_svga.svga_get_pri() ?? throw new InvalidOperationException("pas de socle SVGA après initpc");
            // Le CRTC en 3Dxh, la mémoire en A0000h-AFFFFh, en graphique.
            io.outb(0x3c2, 0x63);
            Gr(0x06, 0x05);
            return corps(svga);
        }
        finally
        {
            try { pc.closepc(); } catch (Exception) { }
        }
    });

    private static void Seq(int idx, int val)
    {
        io.outb(0x3c4, (byte)idx);
        io.outb(0x3c5, (byte)val);
    }

    private static void Gr(int idx, int val)
    {
        io.outb(0x3ce, (byte)idx);
        io.outb(0x3cf, (byte)val);
    }

    private static void Crtc(int idx, int val)
    {
        io.outb(0x3d4, (byte)idx);
        io.outb(0x3d5, (byte)val);
    }

    private static string Verrous(svga_t s) => $"{s.la:X2}{s.lb:X2}{s.lc:X2}{s.ld:X2}";

    /// <summary>Des octets posés dans la VRAM, à <paramref name="a"/>.</summary>
    private static void Vram(svga_t s, uint a, params byte[] octets)
    {
        for (var i = 0; i < octets.Length; i++)
            s.vram[a + i] = octets[i];
    }

    /// <summary>Un cas du socle : le corps, rendu face à l'attendu de PCem ou du matériel.</summary>
    private static int UnSvga(string nom, string carte, Func<svga_t, string> corps, string pcem, string mat, bool hw,
                              bool temoin = false) =>
        AvecSvga(carte, s =>
        {
            var v = corps(s);
            var att = temoin || hw ? mat : pcem;
            return Voir(nom, v == att, v, att, hw);
        });

    // PB-220 — CR00 est le total des caractères de la ligne moins 5 (IBM, p. 2-56) ; PCem ajoute 6. En mode 3, CR00 =
    // 5Fh : 101 caractères chez PCem, 100 sur la carte. Toutes les cartes du socle en héritent ; la TVGA double le total
    // par GR0F bit 3 (tvga_recalctimings), après : le cas le montre doublé depuis le total corrigé.
    private static int Pb220(bool hw)
    {
        string Htotal(svga_t s, int cr00)
        {
            Crtc(0x00, cr00);
            return $"htotal={s.htotal}";
        }
        var bad = 0;
        foreach (var carte in new[] { "vga", "et4000ax", "cl_gd5429", "px_trio64", "tvga9000b" })
            bad += UnSvga($"{carte} : CR00 = 5Fh (le mode 3)", carte, s => Htotal(s, 0x5f), "htotal=101", "htotal=100", hw);
        bad += UnSvga("tvga9000b : GR0F = 08h, puis CR00 = 5Fh", "tvga9000b", s =>
        {
            Gr(0x0f, 0x08);
            return Htotal(s, 0x5f);
        }, "htotal=202", "htotal=200", hw);
        bad += UnSvga("vga : CR00 = 01h, puis 00h", "vga", s =>
        {
            Crtc(0x00, 0x01);
            return Htotal(s, 0x00);
        }, "htotal=6", "htotal=5", hw);
        return bad;
    }

    // PB-80 — la GD5429 charge ses verrous à chaque lecture de la mémoire d'affichage (Cirrus, § 6.27, p. 6-34), le
    // chain4 compact compris (SR7 bit 0) ; PCem rend l'octet et sort, les verrous de la lecture d'avant restent. Le cas de
    // l'entrée : verrous nuls, VRAM[8h..Bh] = 55h 66h 77h 88h, lecture d'octet en A000:0008, puis le mode d'écriture 1
    // (GR5 = 41h) et l'écriture d'un octet en A000:0010 : VRAM[10h] = 00h chez PCem, 55h sur la carte. Par la banque
    // (gd5429_read) et par la fenêtre linéaire du socle (svga_read_linear, que la GD5429 appelle hors des verrous de huit
    // octets). Témoin : le chain4 simple (SR7 bit 0 nul), qui charge les verrous dans les deux modes.
    private static int Pb80(bool hw)
    {
        string Compact(svga_t s, bool compact, bool lineaire)
        {
            Seq(0x06, 0x12);
            Seq(0x04, 0x0e);
            Seq(0x07, compact ? 0x01 : 0x00);
            Vram(s, 0x08, 0x55, 0x66, 0x77, 0x88);
            Vram(s, 0x10, 0xaa, 0xaa, 0xaa, 0xaa);
            s.la = s.lb = s.lc = s.ld = 0;
            if (lineaire)
                vid_svga.svga_read_linear(0x08, s);
            else
                mem.mem_readb_phys(compact ? 0xa0008u : 0xa0002u);
            var verrous = Verrous(s);
            if (!compact)
                return $"verrous={verrous}";
            Gr(0x05, 0x41);
            mem.mem_writeb_phys(0xa0010, 0x00);
            return $"verrous={verrous} VRAM[10h]={s.vram[0x10]:X2}";
        }
        var bad = UnSvga("cl_gd5429, chain4 compact : A000:0008 lu, puis A000:0010 écrit en mode 1", "cl_gd5429",
                         s => Compact(s, true, false), "verrous=00000000 VRAM[10h]=00", "verrous=55667788 VRAM[10h]=55", hw);
        bad += UnSvga("cl_gd5429, chain4 compact : l'octet 8 lu par la fenêtre linéaire, puis A000:0010 écrit en mode 1",
                      "cl_gd5429", s => Compact(s, true, true), "verrous=00000000 VRAM[10h]=00",
                      "verrous=55667788 VRAM[10h]=55", hw);
        // Le chain4 simple : A000:0002 est l'octet du plan 2 en 8h ; les verrous, les quatre plans de 8h.
        bad += UnSvga("cl_gd5429, chain4 simple : A000:0002 lu (témoin)", "cl_gd5429",
                      s => Compact(s, false, false), "", "verrous=55667788", hw, temoin: true);
        return bad;
    }

    // PB-221 (a) — une lecture de mot ou de double mot vaut deux ou quatre lectures d'octet, et chacune charge les verrous
    // (Cirrus, p. 6-34 ; le cycle de mot, déduit) : ce sont ceux du dernier octet. Le chemin rapide de PCem (`fast` : GR8 =
    // FFh, ni fonction logique ni GR1, en chain4 compact) les laisse. L'ET4000 (chain4 compact toujours) passe par
    // svga_readw et svga_readl, la GD5429 en compact par svga_readw_linear et svga_readl_linear (gd5429_readw). Verrous
    // posés à 11223344h (à la main : une lecture d'octet de la GD5429 en compact passerait par PB-80), puis le mot ou le
    // double mot lu en A000:0100 : 55667788h sur la carte. À cheval sur deux doubles mots (le mot en A000:0103, le double
    // mot en A000:0102), ceux du dernier octet : 99AABBCCh.
    // Témoin : GR8 = 7Fh, le chemin lent, qui les charge dans les deux modes.
    private static int Pb221(bool hw)
    {
        string Lu(svga_t s, string carte, int taille, int gr8, uint a = 0x100)
        {
            if (carte == "cl_gd5429")
            {
                Seq(0x06, 0x12);
                Seq(0x07, 0x01);
            }
            Seq(0x04, 0x0e);
            Gr(0x08, gr8);
            Vram(s, 0x100, 0x55, 0x66, 0x77, 0x88, 0x99, 0xaa, 0xbb, 0xcc);
            (s.la, s.lb, s.lc, s.ld) = (0x11, 0x22, 0x33, 0x44);
            var v = taille == 2 ? $"{mem.mem_readw_phys(0xa0000 + a):X4}" : $"{mem.mem_readl_phys(0xa0000 + a):X8}";
            return $"fast={s.fast} lu={v} verrous={Verrous(s)}";
        }
        var bad = 0;
        foreach (var carte in new[] { "et4000ax", "cl_gd5429" })
        {
            bad += UnSvga($"{carte} : les verrous à 11223344h, puis le mot en A000:0100", carte, s => Lu(s, carte, 2, 0xff),
                          "fast=1 lu=6655 verrous=11223344", "fast=1 lu=6655 verrous=55667788", hw);
            bad += UnSvga($"{carte} : les verrous à 11223344h, puis le double mot en A000:0100", carte, s => Lu(s, carte, 4, 0xff),
                          "fast=1 lu=88776655 verrous=11223344", "fast=1 lu=88776655 verrous=55667788", hw);
            bad += UnSvga($"{carte} : les verrous à 11223344h, puis le mot en A000:0103", carte,
                          s => Lu(s, carte, 2, 0xff, 0x103), "fast=1 lu=9988 verrous=11223344",
                          "fast=1 lu=9988 verrous=99AABBCC", hw);
            bad += UnSvga($"{carte} : les verrous à 11223344h, puis le double mot en A000:0102", carte,
                          s => Lu(s, carte, 4, 0xff, 0x102), "fast=1 lu=AA998877 verrous=11223344",
                          "fast=1 lu=AA998877 verrous=99AABBCC", hw);
        }
        bad += UnSvga("et4000ax, GR8 = 7Fh : les verrous à 11223344h, puis le mot en A000:0100 (témoin)", "et4000ax",
                      s => Lu(s, "et4000ax", 2, 0x7f), "", "fast=0 lu=6655 verrous=55667788", hw, temoin: true);
        return bad;
    }
}
