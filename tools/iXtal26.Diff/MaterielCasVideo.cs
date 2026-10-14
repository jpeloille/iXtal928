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
        // PB-221 (b) — l'écriture : un mot écrit vaut deux octets, chacun sous le mode d'écriture et la rotation. Le cas de
        // l'entrée : les verrous à 11223344h, GR5 = 41h (le mode 1), GR8 = FFh, le mot BBAAh en A000:0100 : PCem AAh BBh,
        // la carte 11h 22h (les verrous des plans 0 et 1) ; le double mot en A000:0104, 11h à 44h. Une rotation (GR3 = 01h,
        // le mode 0) : 0201h devient 80h 01h. Par la banque sur l'ET4000 (svga_writew, svga_writel), par la fenêtre sur la
        // GD5429 en compact (svga_writew_linear, svga_writel_linear, que gd5429_writew prend). Témoins : le mode 0 sans
        // rotation, le même dans les deux modes, la correction sans servir ; la GD5429 sous GR0B bit 2 (le mode d'écriture
        // étendu), où le chemin de PCem reste.
        string Ecrit(svga_t s, string carte, int gr5, int gr3, int taille, uint val)
        {
            if (carte == "cl_gd5429")
            {
                Seq(0x06, 0x12);
                Seq(0x07, 0x01);
            }
            Seq(0x04, 0x0e);
            Gr(0x08, 0xff);
            Gr(0x05, gr5);
            Gr(0x03, gr3);
            (s.la, s.lb, s.lc, s.ld) = (0x11, 0x22, 0x33, 0x44);
            var a = taille == 2 ? 0x100u : 0x104u;
            Vram(s, a, 0, 0, 0, 0);
            var lineaire = carte == "cl_gd5429";
            var sonde = ModeMateriel.Sonde[221];
            if (taille == 2)
            {
                if (lineaire)
                    vid_svga.svga_writew_linear(a, (ushort)val, s);
                else
                    vid_svga.svga_writew(0xa0000 + a, (ushort)val, s);
            }
            else if (lineaire)
                vid_svga.svga_writel_linear(a, val, s);
            else
                vid_svga.svga_writel(0xa0000 + a, val, s);
            var v = $"fast={s.fast} VRAM={string.Join(" ", Enumerable.Range(0, taille).Select(i => s.vram[a + i].ToString("X2")))}";
            return gr5 == 0x40 && gr3 == 0 ? $"{v} correction={ModeMateriel.Sonde[221] - sonde}" : v;
        }
        foreach (var carte in new[] { "et4000ax", "cl_gd5429" })
        {
            bad += UnSvga($"{carte} : les verrous à 11223344h, GR5 = 41h, le mot BBAAh en A000:0100", carte,
                          s => Ecrit(s, carte, 0x41, 0x00, 2, 0xbbaa), "fast=1 VRAM=AA BB", "fast=1 VRAM=11 22", hw);
            bad += UnSvga($"{carte} : les verrous à 11223344h, GR5 = 41h, le double mot AABBCCDDh en A000:0104", carte,
                          s => Ecrit(s, carte, 0x41, 0x00, 4, 0xaabbccdd), "fast=1 VRAM=DD CC BB AA",
                          "fast=1 VRAM=11 22 33 44", hw);
            bad += UnSvga($"{carte} : GR5 = 40h, GR3 = 01h (une rotation), le mot 0201h en A000:0100", carte,
                          s => Ecrit(s, carte, 0x40, 0x01, 2, 0x0201), "fast=1 VRAM=01 02", "fast=1 VRAM=80 01", hw);
        }
        bad += UnSvga("et4000ax : GR5 = 40h, sans rotation, le mot BBAAh en A000:0100 (témoin)", "et4000ax",
                      s => Ecrit(s, "et4000ax", 0x40, 0x00, 2, 0xbbaa), "", "fast=1 VRAM=AA BB correction=0", hw,
                      temoin: true);
        bad += UnSvga("cl_gd5429 : GR0B = 04h, GR5 = 41h, le mot BBAAh par la fenêtre (le chemin de PCem, témoin)", "cl_gd5429",
                      s =>
                      {
                          Gr(0x0b, 0x04);
                          var v = Ecrit(s, "cl_gd5429", 0x41, 0x00, 2, 0xbbaa);
                          return v;
                      }, "", "fast=1 VRAM=AA BB", hw, temoin: true);
        bad += UnSvga("et4000ax, GR8 = 7Fh : les verrous à 11223344h, puis le mot en A000:0100 (témoin)", "et4000ax",
                      s => Lu(s, "et4000ax", 2, 0x7f), "", "fast=0 lu=6655 verrous=55667788", hw, temoin: true);
        return bad;
    }

    // PB-37 — le rendu 24 bpp basse résolution : chaque point de 24 bits doublé sur toute la ligne (le rendu haute
    // résolution voisin écrit par `*p++` ; déduit, l'intention du code). PCem n'avance jamais la sortie : chaque tour
    // réécrit les huit premiers points, avec le dernier groupe. La VRAM à 00h, 01h, 02h…, hdisp = 7, la ligne 0 rendue par
    // un appel direct : sans remappage, PCem fait huit tours (x++), la carte deux de quatre points ; avec le remappage
    // (une fonction d'identité), deux tours dans les deux modes. Les points 0, 2, 8 et 10, et ma après la ligne.
    private static int Pb37(bool hw)
    {
        string Rendu(svga_t s, bool remap)
        {
            for (var i = 0; i < 0x100; i++)
                s.vram[i] = (byte)i;
            var p = (8 - (s.scrollcache & 6)) + 24;
            Array.Clear(Video.video.Buffer32, 0, Video.video.Stride);
            (s.hdisp, s.ma, s.displine, s.fullchange) = (7, 0, 0, 1);
            s.remap_required = remap ? 1 : 0;
            s.remap_func = (_, a) => a;
            vid_svga_render.svga_render_24bpp_lowres(s);
            var b = Video.video.Buffer32;
            return $"p0={b[p]:X6} p2={b[p + 2]:X6} p8={b[p + 8]:X6} p10={b[p + 10]:X6} ma={s.ma}";
        }
        var bad = UnSvga("vga, 24 bpp basse résolution, sans remappage", "vga", s => Rendu(s, false),
                         "p0=565554 p2=595857 p8=000000 p10=000000 ma=96", "p0=020100 p2=050403 p8=0E0D0C p10=11100F ma=24",
                         hw);
        bad += UnSvga("vga, 24 bpp basse résolution, avec remappage", "vga", s => Rendu(s, true),
                      "p0=0E0D0C p2=11100F p8=000000 p10=000000 ma=24", "p0=020100 p2=050403 p8=0E0D0C p10=11100F ma=24",
                      hw);
        return bad;
    }
}
