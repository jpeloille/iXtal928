// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — outillage de vérification.
//
// Les cas de l'EGA (G13.9d, PLAN-G13.md) : PB-99, les traits de la VGA que PCem prête à l'EGA — (1) l'attribut 10h
// bit 7 et 14h dans la palette, (2) la protection de CR0-CR7 par CR11 bit 7, (5) le texte redessiné sur fullchange
// seulement, (7) les bits 9 de CR07 et CR09, CR0A bit 5 qui éteint le curseur, l'attribut 10h bit 5 qui remet le
// défilement fin à la ligne de partage ; (6) la mémoire de 64 Ko, que les substitutions de CR17 et les tables de la
// police de SR3 dépassaient (G13.9c2). Le banc : une IBM XT montée avec l'EGA, sans BIOS lancé ; les registres par
// les ports (3C2h à 01h, les ports en 3Dxh) ; une ligne ou une fin de ligne jouée par un appel de ega_poll sur un état
// posé à la main.

using iXtal26.Video;

namespace iXtal26.Diff;

internal static partial class MaterielCas
{
    private static ega_t Ega() => vid_ega.Probe ?? throw new InvalidOperationException("pas d'EGA après initpc");

    private static int UnEga(string nom, Func<ega_t, string> corps, string pcem, string mat, bool hw,
                             bool temoin = false, int memoire = 256, string cles = "") =>
        UnMono(nom, "ega", () =>
        {
            io.outb(0x3c2, 0x01);
            return corps(Ega());
        }, pcem, mat, hw, temoin, $"[EGA]\nmemory = {memoire}\n{cles}");

    private static void Attr(int idx, int val)
    {
        io.inb(0x3da);
        io.outb(0x3c0, (byte)idx);
        io.outb(0x3c0, (byte)val);
    }

    private static void CrtcEga(int reg, int val)
    {
        io.outb(0x3d4, (byte)reg);
        io.outb(0x3d5, (byte)val);
    }

    // PB-99 — l'EGA d'IBM : 10h bits 4-7, 07h bits 6-7, 09h et 0Ah bits 5-7, 11h bits 6-7 « Not Used » ; ni 14h ; la
    // palette, six bits par registre (Options and Adapters, vol. 2, EGA, pp. 30-37, 56-61). Les cas de l'entrée : (1)
    // AC00 = 01h, AC14 = 0Fh, AC10 = 80h : egapal[0] F1h chez PCem, 01h sur l'EGA ; (2) CR11 = 80h puis CR01 = 27h : PCem
    // garde CR01, l'EGA prend 27h ; (7) CR07 = 20h, CR06 = 70h : vtotal 271h contre 71h ; CR09 = 40h, CR18 = 10h : split
    // 212h contre 12h. CR0A = 20h, une fin d'affichage (la ligne qui atteint dispend), le compteur de trames à 16 : le
    // curseur éteint chez PCem, allumé sur l'EGA. L'attribut 10h = 20h, la ligne de partage, le défilement fin à 5 : 0
    // chez PCem, 5 sur l'EGA. (5) Une ligne de texte jouée sans fullchange : PCem ne la redessine pas, l'EGA si. Témoins :
    // CR11 = 00h ; AC10 = 00h ; une ligne avec fullchange.
    private static int Pb99(bool hw)
    {
        var bad = UnEga("EGA : AC14 = 0Fh, AC10 = 80h, AC00 = 01h", e =>
        {
            Attr(0x14, 0x0f);
            Attr(0x10, 0x80);
            Attr(0x00, 0x01);
            return $"egapal[0]={e.egapal[0]:X2}";
        }, "egapal[0]=F1", "egapal[0]=01", hw);
        bad += UnEga("EGA : AC10 = 00h, AC00 = 2Ah (témoin)", e =>
        {
            Attr(0x10, 0x00);
            Attr(0x00, 0x2a);
            return $"egapal[0]={e.egapal[0]:X2}";
        }, "", "egapal[0]=2A", hw, temoin: true);
        bad += UnEga("EGA : CR11 = 80h, puis CR01 = 27h", e =>
        {
            CrtcEga(0x11, 0x80);
            CrtcEga(0x01, 0x27);
            return $"CR01={e.crtc[1]:X2}";
        }, "CR01=00", "CR01=27", hw);
        bad += UnEga("EGA : CR11 = 00h, puis CR01 = 27h (témoin)", e =>
        {
            CrtcEga(0x11, 0x00);
            CrtcEga(0x01, 0x27);
            return $"CR01={e.crtc[1]:X2}";
        }, "", "CR01=27", hw, temoin: true);
        bad += UnEga("EGA : CR07 = 20h, CR06 = 70h ; CR09 = 40h, CR18 = 10h", e =>
        {
            CrtcEga(0x07, 0x20);
            CrtcEga(0x06, 0x70);
            CrtcEga(0x09, 0x40);
            CrtcEga(0x18, 0x10);
            return $"vtotal={e.vtotal:X} split={e.split:X}";
        }, "vtotal=271 split=212", "vtotal=71 split=12", hw);
        // Les bits 9 de dispend et de vsyncstart (CR07 bits 6 et 7), et les bits 8 que l'EGA garde (CR07 bits 0, 1, 2
        // et 4) : CR07 = D7h.
        bad += UnEga("EGA : CR07 = D7h, CR06 = 20h, CR12 = 10h, CR10 = 18h, CR18 = 08h", e =>
        {
            CrtcEga(0x06, 0x20);
            CrtcEga(0x12, 0x10);
            CrtcEga(0x10, 0x18);
            CrtcEga(0x18, 0x08);
            CrtcEga(0x07, 0xd7);
            return $"vtotal={e.vtotal:X} dispend={e.dispend:X} vsyncstart={e.vsyncstart:X} split={e.split:X}";
        }, "vtotal=121 dispend=311 vsyncstart=319 split=10A", "vtotal=121 dispend=111 vsyncstart=119 split=10A", hw);
        // Une fin de ligne (linepos = 1, l'affichage éteint) qui mène vc à dispend, avant le retour (vsyncstart) et la fin
        // de trame (vtotal) : le curseur reprend sa phase.
        string Fin(ega_t e, int cr0a)
        {
            CrtcEga(0x06, 0x20);
            CrtcEga(0x10, 0x18);
            CrtcEga(0x12, 0x0f);
            CrtcEga(0x0a, cr0a);
            (e.linepos, e.dispon, e.vc, e.blink, e.cursoron) = (1, 0, e.dispend - 1, 16, 0);
            vid_ega.ega_poll(e);
            return $"curseur={(e.cursoron != 0 ? "allumé" : "éteint")}";
        }
        bad += UnEga("EGA : CR0A = 20h, la ligne qui atteint dispend, le compteur de trames à 16", e => Fin(e, 0x20),
                     "curseur=éteint", "curseur=allumé", hw);
        bad += UnEga("EGA : CR0A = 00h, la ligne qui atteint dispend, le compteur de trames à 16 (témoin)", e => Fin(e, 0x00),
                     "", "curseur=allumé", hw, temoin: true);
        string Partage(ega_t e, int a10)
        {
            CrtcEga(0x18, 0x40);
            Attr(0x10, a10);
            (e.linepos, e.dispon, e.vc, e.scrollcache) = (1, 0, e.split - 1, 5);
            vid_ega.ega_poll(e);
            return $"défilement={e.scrollcache}";
        }
        bad += UnEga("EGA : l'attribut 10h = 20h, la ligne de partage, le défilement fin à 5", e => Partage(e, 0x20),
                     "défilement=0", "défilement=5", hw);
        bad += UnEga("EGA : l'attribut 10h = 00h, la ligne de partage, le défilement fin à 5 (témoin)", e => Partage(e, 0x00),
                     "", "défilement=5", hw, temoin: true);
        // Une ligne de texte (GR6 bit 0 nul), l'affichage allumé : la ligne de buffer32 remplie d'un motif avant.
        string Ligne(ega_t e, int plein)
        {
            io.outb(0x3ce, 6);
            io.outb(0x3cf, 0);
            (e.linepos, e.dispon, e.scrblank, e.displine, e.firstline) = (0, 1, 0, 10, 0);
            Video.video.fullchange = plein;
            var l = 10 * Video.video.Stride;
            Array.Fill(Video.video.Buffer32, 0x123456u, l, 64);
            vid_ega.ega_poll(e);
            var change = false;
            for (var i = 0; i < 64; i++)
                change |= Video.video.Buffer32[l + i] != 0x123456u;
            return change ? "redessinée" : "laissée";
        }
        bad += UnEga("EGA : une ligne de texte, sans fullchange", e => Ligne(e, 0), "laissée", "redessinée", hw);
        bad += UnEga("EGA : une ligne de texte, avec fullchange (témoin)", e => Ligne(e, 1), "", "redessinée", hw,
                     temoin: true);
        // (6) Une ligne graphique, l'adresse en octets (CR17 bit 6 à 1). Les octets 0 à 3 à 00h, 10000h à 10003h à FFh
        // (posés à la main : la carte de 64 Ko n'en a pas) ; la palette 0 à 00h, 3 et Fh à 3Fh ; le dernier point du
        // premier caractère, et CR17 après la ligne (le registre que l'invité a écrit). En 2 bpp (GR5 = 20h) et en
        // 16 couleurs (GR5 = 00h, SR1 bit 3 à 0), CR17 = 41h (bit 1 à 0) à la rangée 2 : la substitution pose le bit
        // 0x10000 ; en 2 bpp, CR17 = 43h (sans substitution) et l'adresse de début de trame à 10000h (CR0C = 40h), que
        // PCem ne masque qu'après le premier caractère.
        string Rangee(ega_t e, int gr5, int cr17, int sc, uint ma)
        {
            Attr(0x00, 0x00);
            Attr(0x03, 0x3f);
            Attr(0x0f, 0x3f);
            Attr(0x12, 0x0f);
            CrtcEga(0x17, cr17);
            io.outb(0x3c4, 1);
            io.outb(0x3c5, 0);
            io.outb(0x3ce, 6);
            io.outb(0x3cf, 1);
            io.outb(0x3ce, 5);
            io.outb(0x3cf, (byte)gr5);
            for (var k = 0; k < 4; k++)
                (e.vram[k], e.vram[0x10000 + k]) = (0, 0xff);
            (e.linepos, e.dispon, e.scrblank, e.displine, e.firstline, e.sc, e.ma, e.scrollcache) =
                (0, 1, 0, 10, 0, sc, ma, 0);
            vid_ega.ega_poll(e);
            var p = Video.video.Buffer32[10 * Video.video.Stride + 32 + (gr5 == 0x20 ? 15 : 7)];
            var lu = p == e.pallook[0x3f] ? "lu en 10000h" : p == e.pallook[0] ? "lu en 0" : $"{p:X6}";
            return $"{lu}, CR17={e.crtc[0x17]:X2}";
        }
        foreach (var (gr5, cr17, sc, ma, nom) in new[]
                 {
                     (0x20, 0x41, 2, 0u, "CR17 = 41h, une ligne en 2 bpp à la rangée 2"),
                     (0x00, 0x41, 2, 0u, "CR17 = 41h, une ligne en 16 couleurs à la rangée 2"),
                     (0x20, 0x43, 0, 0x10000u, "CR17 = 43h, une ligne en 2 bpp, l'adresse de début à 10000h"),
                 })
        {
            var r = $"CR17={cr17:X2}";
            bad += UnEga($"EGA de 64 Ko : {nom}", e => Rangee(e, gr5, cr17, sc, ma), $"lu en 10000h, {r}",
                         $"lu en 0, {r}", hw, memoire: 64);
            bad += UnEga($"EGA de 256 Ko : {nom} (témoin)", e => Rangee(e, gr5, cr17, sc, ma), "",
                         $"lu en 10000h, {r}", hw, temoin: true);
        }
        // (6) La police : 41h d'attribut 07h, la rangée 0 ; la table 0 a FFh à cette rangée, les tables 1 et 2 00h
        // (posées à la main) ; la palette 0 à 00h, 7 à 3Fh ; le premier point, sans curseur. SR3 = 05h (les tables A
        // et B à 1) sur 64 Ko, SR3 = 0Ah (à 2) sur 128 Ko : la table que la carte n'a pas retombe sur la table 0.
        string Police(ega_t e, int sr3)
        {
            Attr(0x00, 0x00);
            Attr(0x07, 0x3f);
            io.outb(0x3c4, 3);
            io.outb(0x3c5, (byte)sr3);
            io.outb(0x3ce, 6);
            io.outb(0x3cf, 0);
            (e.vram[0], e.vram[1]) = (0x41, 0x07);
            e.vram[2 + 0x41 * 128] = 0xff;
            e.vram[0x10002 + 0x41 * 128] = e.vram[0x20002 + 0x41 * 128] = 0x00;
            (e.linepos, e.dispon, e.scrblank, e.displine, e.firstline, e.sc, e.ma, e.con) = (0, 1, 0, 10, 0, 0, 0, 0);
            Video.video.fullchange = 1;
            vid_ega.ega_poll(e);
            var p = Video.video.Buffer32[10 * Video.video.Stride + 32];
            return p == e.pallook[e.egapal[7]] ? "la table 0"
                : p == e.pallook[e.egapal[0]] ? "une autre table" : $"{p:X6}";
        }
        bad += UnEga("EGA de 64 Ko : SR3 = 05h, 41h à la rangée 0", e => Police(e, 0x05), "une autre table",
                     "la table 0", hw, memoire: 64);
        bad += UnEga("EGA de 128 Ko : SR3 = 0Ah, 41h à la rangée 0", e => Police(e, 0x0a), "une autre table",
                     "la table 0", hw, memoire: 128);
        bad += UnEga("EGA de 256 Ko : SR3 = 05h, 41h à la rangée 0 (témoin)", e => Police(e, 0x05), "",
                     "une autre table", hw, temoin: true);
        bad += Faisceau99(hw);
        bad += Pod14(hw);
        return bad;
    }

    /// <summary>Les bips de l'amorçage d'une IBM XT et de l'EGA d'IBM, son BIOS lancé, <paramref name="tranches"/>
    /// tranches de 10 ms : les fronts montants du haut-parleur (61h bit 1), relevés à chaque tranche.</summary>
    private static string Bips(int tranches)
    {
        int bips = 0, avant = 0;
        for (int i = 0; i < tranches; i++)
        {
            pc.runpc();
            int now = Sound.sound_speaker.speaker_enable;
            if (now != 0 && avant == 0)
                bips++;
            avant = now;
        }
        return $"{bips} bip(s)";
    }

    // PB-99 (4) — POD14, le test du BIOS de l'EGA (Options and Adapters, vol. 2, EGA, pp. 113-114 ; le listing,
    // POD14_10 à POD14_17) : une rangée de pavés blancs (la palette 0Fh à 3Fh), puis, pour 12h = 0Fh, 1Fh et 2Fh, il
    // attend que 3DAh bits 4-5 passent à 1 puis à 0, 65 536 lectures au plus ; sinon, un bip long et trois courts. PCem
    // bascule ces bits à chaque lecture (« Fools IBM EGA video BIOS self-test ») ; le faisceau les tire des points de
    // la rangée, et la palette 0Fh et 12h ne s'écrivent qu'avec 3C1h (la part (8)). Les deux modes : aucun bip, quinze
    // secondes de l'amorçage (POD14 tombe dans les cinq premières ; sans la part (4) ou (8), un bip long et trois
    // courts).
    private static int Pod14(bool hw) =>
        UnEga("XT et EGA, l'amorçage du BIOS, quinze secondes : POD14 sans bip", _ => Bips(1500), "0 bip(s)",
              "0 bip(s)", hw);

    // PB-294 — SW1-SW4 (Options and Adapters, vol. 2, EGA, p. 81-82 ; le listing du BIOS, p. 108, RD_SWS p. 109) : six
    // réglages où l'EGA est primaire, 6 à 11 ; PCem n'offre pas 8 (l'Enhanced Display en émulation) ni 11 (le
    // monochrome, la CGA secondaire en 80 colonnes). Le cas de l'entrée : monitor_type = 8, puis 91 (11, le tube blanc)
    // ; les interrupteurs relus en 3C2h bit 4 sous les quatre CLKSEL du BIOS (0Dh, 09h, 05h, 01h : SW1 à SW4), et la
    // palette du tube (pallook[3Fh]). PCem refuse la clé, vers 9 : 1001b, la palette des couleurs ; la carte rend 8
    // (0001b) et 11 (1101b) en blanc. Témoins : 7 et 90 (10, le tube blanc), sans écart.
    private static string Interrupteurs(ega_t e)
    {
        var sw = "";
        foreach (var clk in new byte[] { 0x0d, 0x09, 0x05, 0x01 })
        {
            io.outb(0x3c2, clk);
            sw = ((io.inb(0x3c2) & 0x10) != 0 ? "1" : "0") + sw;
        }
        return $"SW4-SW1={sw} pallook[3F]={e.pallook[0x3f]:X6}";
    }

    private static int Pb294(bool hw)
    {
        var bad = UnEga("EGA : monitor_type = 8 (l'Enhanced Display en émulation)", Interrupteurs,
                        "SW4-SW1=1001 pallook[3F]=FFFFFF", "SW4-SW1=1000 pallook[3F]=FFFFFF", hw,
                        cles: "monitor_type = 8\n");
        bad += UnEga("EGA : monitor_type = 91 (11, le monochrome, le tube blanc)", Interrupteurs,
                     "SW4-SW1=1001 pallook[3F]=FFFFFF", "SW4-SW1=1011 pallook[3F]=FFFDED", hw,
                     cles: "monitor_type = 91\n");
        bad += UnEga("EGA : monitor_type = 7 (témoin)", Interrupteurs, "", "SW4-SW1=0111 pallook[3F]=FFFFFF", hw,
                     temoin: true, cles: "monitor_type = 7\n");
        bad += UnEga("EGA : monitor_type = 90 (10, le tube blanc ; témoin)", Interrupteurs, "",
                     "SW4-SW1=1010 pallook[3F]=FFFDED", hw, temoin: true, cles: "monitor_type = 90\n");
        return bad;
    }
}
