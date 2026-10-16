// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — outillage de vérification.
//
// Les cas de la fréquence du DSP 4.xx et de la saturation du FIR (G13.8c2, PLAN-G13.md) : PB-155, sur le banc du son
// (MaterielCasSon.cs). Le registre de fréquence de 8 bits du micrologiciel (4.05 pour la SB 16, 4.13 pour l'AWE32) ;
// ses Hz, ceux de MAME (46,61512 MHz / 1024 / 256 par pas, secondaire). La saturation, sur le tampon de la carte joué à
// la main, un échantillon par appel. Le registre différé (G13.8c3) : PB-288.

using iXtal26.Sound;

namespace iXtal26.Diff;

internal static partial class MaterielCas
{
    // PB-155 — le registre (`v405-8k_e51aff23.asm:1688-1856`, `v413-8k_e22e9001.asm:1865-2055`) : 41h et 42h par X09a7
    // (FFh dès l'octet fort B1h, 1Ch sous 13h, l'arrondi de 23 × f / 4096 ; le 4.05 perd une retenue), 40h par la table
    // (la constante bornée à EBh ; le 4.05 diffère du 4.13 en 83h et en C8h). PCem garde la fréquence libre. Témoins :
    // 22 050 et 44 100 Hz, que le registre rend juste (7Ch, F8h) ; et 40h sur la SB Pro 2, le chemin de PCem. La
    // saturation : à 37 521 Hz (le registre D3h, la même fréquence dans les deux modes, donc les mêmes coefficients),
    // la somme des |coefficients| du FIR dépasse 2 ; ±32 767 posés au signe de chaque coefficient, la voix et le master
    // à 32 767 : le produit dépasse 2^31 — INT_MIN chez PCem, puis −21 846 en sortie ; saturé, +21 844. Témoin : la
    // même salve à 22 050 Hz, sous 2^31, la même sortie dans les deux modes. Sur la SB 16 et l'AWE32.
    private static int Pb155(bool hw)
    {
        var bad = 0;
        string Freq(sb_dsp_t d) => $"{d.sb_freq} Hz, {d.sb_timeo:X}h/{d.sb_timei:X}h";
        int Un(string carte, string nom, int[] octets, int pcem, int mat, bool temoin = false) => AvecSb(carte, d =>
        {
            Dsp(octets);
            var v = Freq(d);
            int f = temoin || hw ? mat : pcem;
            var att = $"{f} Hz, {256 + f:X}h/{256 + f:X}h";
            var nomCarte = carte == "sb16" ? "SB 16" : "AWE32";
            return Voir($"{nomCarte} : {nom}{(temoin ? " (témoin)" : "")}", v == att, v, att, hw);
        });
        bad += Un("sb16", "41h FFh FFh", [0x41, 0xff, 0xff], 65535, 45345);
        bad += Un("sb16", "42h FFh FFh (l'entrée)", [0x42, 0xff, 0xff], 65535, 45345);
        bad += Un("sb16", "41h 12h FFh (1Ch)", [0x41, 0x12, 0xff], 4863, 4979);
        bad += Un("sb16", "41h 13h 00h (1Bh)", [0x41, 0x13, 0x00], 4864, 4801);
        bad += Un("sb16", "41h 59h 0Ch (la retenue perdue du 4.05 : 70h)", [0x41, 0x59, 0x0c], 22796, 19916);
        bad += Un("sbawe32", "41h 59h 0Ch (le 4.13 : 80h)", [0x41, 0x59, 0x0c], 22796, 22761);
        bad += Un("sb16", "41h A0h 00h (E6h)", [0x41, 0xa0, 0x00], 40960, 40899);
        bad += Un("sb16", "41h B0h FFh (FEh, le plus grand calculé)", [0x41, 0xb0, 0xff], 45311, 45167);
        bad += Un("sb16", "41h B1h 00h (FFh, le seuil)", [0x41, 0xb1, 0x00], 45312, 45345);
        bad += Un("sbawe32", "42h FFh FFh (l'entrée)", [0x42, 0xff, 0xff], 65535, 45345);
        bad += Un("sb16", "41h 56h 22h, 22 050 Hz (7Ch)", [0x41, 0x56, 0x22], 22050, 22050, temoin: true);
        bad += Un("sbawe32", "41h ACh 44h, 44 100 Hz (F8h)", [0x41, 0xac, 0x44], 44100, 44100, temoin: true);
        // 40h : PCem garde la constante (sb_timeo < 256) ; le mode matériel, le registre (256 + f).
        int Constante(string carte, string nom, int c, int pcemHz, int mat) => AvecSb(carte, d =>
        {
            Dsp(0x40, c);
            var v = Freq(d);
            var att = hw ? $"{mat} Hz, {256 + mat:X}h/{256 + mat:X}h" : $"{pcemHz} Hz, {c:X}h/{c:X}h";
            return Voir($"{(carte == "sb16" ? "SB 16" : "AWE32")} : 40h {c:X2}h", v == att, v, att, hw);
        });
        bad += Constante("sb16", "", 0xff, 1000000, 45345);
        bad += Constante("sb16", "", 0xc8, 17857, 35565);
        bad += Constante("sbawe32", "", 0xc8, 17857, 17782);
        bad += Constante("sb16", "", 0x83, 8000, 7824);
        bad += Constante("sbawe32", "", 0x83, 8000, 8002);
        bad += AvecSb("sbprov2", d =>
        {
            Dsp(0x40, 0xd3);
            var v = Freq(d);
            return Voir("SB Pro 2 : 40h D3h (témoin, le DSP 3.02)", v == "22222 Hz, D3h/D3h", v, "22222 Hz, D3h/D3h",
                        hw);
        });
        // La saturation du FIR.
        int Salve(string carte, int hi, int lo, int pcem, int mat, bool temoin, int graves = 8, int aigus = 8) =>
            AvecSb(carte, d =>
        {
            Dsp(0x41, hi, lo);
            var sb = sound_sb.sb_pri!;
            var m = sb.mixer_sb16;
            m.voice_l = m.voice_r = m.master_l = m.master_r = 32767;
            m.fm_l = m.fm_r = 0;
            m.bass_l = m.bass_r = graves;
            m.treble_l = m.treble_r = aigus;
            m.output_gain_L = m.output_gain_R = 0;
            d.muted = 0;
            // Les états des filtres d'égalisation (statiques, partagés par les cas d'un même processus) remis à zéro :
            // le témoin ne dépend pas des cas d'avant.
            foreach (var champ in typeof(sound_sb).GetFields(System.Reflection.BindingFlags.NonPublic |
                                                              System.Reflection.BindingFlags.Static))
                if (champ.Name.EndsWith("_iir_x") || champ.Name.EndsWith("_iir_y"))
                    foreach (var voie in (float[][])champ.GetValue(null)!)
                        Array.Clear(voie);
            var coef = sound_sb_dsp.low_fir_sb16_coef;
            var avant = sound.sound_pos_global;
            var buf = new int[2];
            try
            {
                for (var j = 0; j < 52; j++)
                {
                    // L'échantillon écrit k appels avant le dernier est pris par coef[52 − k] (k ≥ 2), le dernier par
                    // coef[0] ; celui de l'avant-dernier n'est pris par aucun (52 cases pour 51 coefficients).
                    var k = 51 - j;
                    var c = k == 0 ? coef[0] : k == 1 ? 0f : coef[52 - k];
                    var s = (short)(c > 0 ? 32767 : c < 0 ? -32767 : 0);
                    d.sbdatl = d.sbdatr = s;
                    d.pos = 0;
                    sound.sound_pos_global = 1;
                    buf[0] = buf[1] = 0;
                    if (carte == "sb16")
                        sound_sb.sb_get_buffer_sb16(buf, 1, sb);
                    else
                        sound_sb.sb_get_buffer_emu8k(buf, 1, sb);
                }
            }
            finally
            {
                sound.sound_pos_global = avant;
            }
            var somme = coef.Sum(x => Math.Abs(x));
            var v = $"{buf[0]}";
            var att = $"{(temoin || hw ? mat : pcem)}";
            var nomCarte = carte == "sb16" ? "SB 16" : "AWE32";
            return Voir($"{nomCarte} : 41h {hi:X2}h {lo:X2}h, la salve aux signes du FIR " +
                        $"(Σ|coef| = {somme:0.0000}){(graves != 8 ? $", graves {graves}, aigus {aigus}" : "")}" +
                        $"{(temoin ? " (témoin)" : "")}", v == att, v, att, hw);
        });
        bad += Salve("sb16", 0x92, 0x91, -21846, 21844, false);
        bad += Salve("sbawe32", 0x92, 0x91, -21846, 21844, false);
        bad += Salve("sb16", 0x56, 0x22, 19135, 19135, true);
        // Les graves et les aigus réglés (12 et 4) : les blocs d'égalisation de la copie du tampon, joués aussi.
        bad += Salve("sbawe32", 0x56, 0x22, 10491, 10491, true, graves: 12, aigus: 4);
        return bad;
    }
    // PB-288 — le registre différé (`X0a0e` range 37h, `X0a26` le donne au codec au départ ou à la reprise d'un
    // transfert ; `v405:1856-1876`). Un automatique 8 bits (1Ch) à 22 050 Hz, puis 41h 2Bh 11h (11 025 Hz) pendant le
    // transfert : PCem passe à 11 025 sur-le-champ ; la carte garde 22 050 jusqu'à D4h, à un nouveau 1Ch ou au reset.
    // Les deux fréquences sont justes dans les deux registres (7Ch et 3Eh, PB-155) : les attendus ne dépendent pas de
    // PB-155. Au reset, la commande gardée s'applique ; sous PB-236, le reset « froid » pose ensuite 38h (9 958 Hz).
    // Témoins : 41h hors transfert, sur-le-champ des deux côtés ; 40h sur la SB Pro 2 (DSP 3.02) pendant 1Ch.
    private static int Pb288(bool hw)
    {
        var bad = 0;
        string Freq(sb_dsp_t d) => $"{d.sb_freq} Hz, {d.sb_timeo:X}h/{d.sb_timei:X}h";
        string F(int f) => $"{f} Hz, {256 + f:X}h/{256 + f:X}h";
        // L'automatique de sortie (ou d'entrée), le canal 1 sur 64 Ko, à 22 050 Hz.
        void Auto(bool entree = false)
        {
            Dma1(0x10000, 0xffff, auto: true, entree: entree);
            Dsp(0x41, 0x56, 0x22);
            Dsp(0x48, 0xff, 0x00);
            Dsp(entree ? 0x2c : 0x1c);
        }
        int Un(string carte, string nom, Func<sb_dsp_t, string> corps, string pcem, string mat, bool temoin = false) =>
            AvecSb(carte, d =>
            {
                var v = corps(d);
                var att = hw && !temoin ? mat : pcem;
                var nomCarte = carte switch { "sb16" => "SB 16", "sbawe32" => "AWE32", _ => "SB Pro 2" };
                return Voir($"{nomCarte} : {nom}{(temoin ? " (témoin)" : "")}", v == att, v, att, hw);
            });
        bad += Un("sb16", "1Ch à 22 050 Hz, 41h 2Bh 11h pendant le transfert", d =>
        {
            Auto();
            Dsp(0x41, 0x2b, 0x11);
            return Freq(d);
        }, F(11025), F(22050));
        bad += Un("sb16", "1Ch, 41h 2Bh 11h, puis D4h (la reprise)", d =>
        {
            Auto();
            Dsp(0x41, 0x2b, 0x11);
            var avant = Freq(d);
            Dsp(0xd4);
            return $"{avant} ; {Freq(d)}";
        }, $"{F(11025)} ; {F(11025)}", $"{F(22050)} ; {F(11025)}");
        bad += Un("sb16", "1Ch, D0h (la pause), 41h 2Bh 11h, puis D4h", d =>
        {
            Auto();
            Dsp(0xd0);
            Dsp(0x41, 0x2b, 0x11);
            var avant = Freq(d);
            Dsp(0xd4);
            return $"{avant} ; {Freq(d)}";
        }, $"{F(11025)} ; {F(11025)}", $"{F(22050)} ; {F(11025)}");
        bad += Un("sb16", "1Ch, 41h 2Bh 11h, puis 1Ch (un nouveau départ)", d =>
        {
            Auto();
            Dsp(0x41, 0x2b, 0x11);
            var avant = Freq(d);
            Dsp(0x1c);
            return $"{avant} ; {Freq(d)}";
        }, $"{F(11025)} ; {F(11025)}", $"{F(22050)} ; {F(11025)}");
        bad += Un("sb16", "1Ch, 41h 2Bh 11h, puis E1h et D3h (ni départ ni reprise)", d =>
        {
            Auto();
            Dsp(0x41, 0x2b, 0x11);
            Dsp(0xe1);
            LireDsp();
            Dsp(0xd3);
            return Freq(d);
        }, F(11025), F(22050));
        bad += Un("sbawe32", "1Ch, 42h 2Bh 11h pendant le transfert (le 4.13)", d =>
        {
            Auto();
            Dsp(0x42, 0x2b, 0x11);
            return Freq(d);
        }, F(11025), F(22050));
        bad += Un("sb16", "2Ch (l'entrée) à 22 050 Hz, 42h 2Bh 11h pendant le transfert, puis 24h 0Fh 00h", d =>
        {
            Auto(entree: true);
            Dsp(0x42, 0x2b, 0x11);
            var avant = Freq(d);
            Dsp(0x24, 0x0f, 0x00);
            return $"{avant} ; {Freq(d)}";
        }, $"{F(11025)} ; {F(11025)}", $"{F(22050)} ; {F(11025)}");
        // 40h : la constante, gardée de même ; à D4h, la fréquence que 40h A6h donne hors transfert (11 111 Hz chez
        // PCem ; sous PB-155, le registre 3Eh, 11 025 Hz : jamais 22 050).
        var hors = "";
        AvecSb("sb16", d =>
        {
            Dsp(0x40, 0xa6);
            hors = Freq(d);
            return 0;
        });
        bad += Un("sb16", "1Ch, 40h A6h, puis D4h", d =>
        {
            Auto();
            Dsp(0x40, 0xa6);
            var avant = Freq(d);
            Dsp(0xd4);
            return $"{avant} ; {Freq(d)}";
        }, $"{hors} ; {hors}", $"{F(22050)} ; {hors}");
        // Le reset : la commande gardée s'applique ; sous PB-236, le reset « froid » pose ensuite 38h.
        bad += Un("sb16", "1Ch, 41h 2Bh 11h, puis un reset", d =>
        {
            Auto();
            Dsp(0x41, 0x2b, 0x11);
            ResetDsp();
            LireDsp();
            return Freq(d);
        }, materiel.pb_236 ? F(9958) : F(11025), materiel.pb_236 ? F(9958) : F(11025));
        // Les autres départs de `X0a26` : le transfert, 41h 2Bh 11h gardée, puis chaque étape (la fréquence après
        // chacune) ; sur la carte, la commande gardée s'applique à la première étape de la liste, pas avant.
        int Etapes(string carte, string nom, Action debut, int[][] etapes, int premier) => Un(carte, nom, d =>
        {
            debut();
            Dsp(0x41, 0x2b, 0x11);
            var l = new List<string> { Freq(d) };
            foreach (var e in etapes)
            {
                Dsp(e);
                l.Add(Freq(d));
            }
            return string.Join(" ; ", l);
        }, string.Join(" ; ", Enumerable.Repeat(F(11025), etapes.Length + 1)),
           string.Join(" ; ", Enumerable.Range(0, etapes.Length + 1).Select(i => F(i > premier ? 11025 : 22050))));
        void Seize()
        {
            Dma5(0x20000, 0xffff, true);
            Dsp(0x41, 0x56, 0x22);
            Dsp(0xb6, 0x10, 0xff, 0x00);
        }
        bad += Etapes("sb16", "B6h (16 bits), 41h 2Bh 11h, D5h (la pause), puis D6h (la reprise)", Seize,
                      [[0xd5], [0xd6]], 1);
        bad += Etapes("sb16", "B6h, 41h 2Bh 11h, puis B0h 10h 0Fh 00h", Seize, [[0xb0, 0x10, 0x0f, 0x00]], 0);
        bad += Etapes("sb16", "C6h (8 bits), 41h 2Bh 11h, puis C0h 00h 0Fh 00h (en file sous PB-239)", () =>
        {
            Dma1(0x10000, 0xffff, auto: true);
            Dsp(0x41, 0x56, 0x22);
            Dsp(0xc6, 0x00, 0xff, 0x00);
        }, [[0xc0, 0x00, 0x0f, 0x00]], 0);
        bad += Etapes("sb16", "80h FFh 00h (le silence), 41h 2Bh 11h, puis 80h 0Fh 00h", () =>
        {
            Dsp(0x41, 0x56, 0x22);
            Dsp(0x80, 0xff, 0x00);
        }, [[0x80, 0x0f, 0x00]], 0);
        bad += Etapes("sb16", "1Ch, 41h 2Bh 11h, puis 91h (la grande vitesse)", () => Auto(), [[0x91]], 0);
        bad += Etapes("sb16", "1Ch, 41h 2Bh 11h, puis 74h 0Fh 00h (l'ADPCM)", () => Auto(), [[0x74, 0x0f, 0x00]], 0);
        // 41h puis 40h pendant le transfert : le même registre 37h, la dernière commande gagne.
        bad += Un("sb16", "1Ch, 41h ACh 44h, 40h A6h, puis D4h", d =>
        {
            Auto();
            Dsp(0x41, 0xac, 0x44);
            Dsp(0x40, 0xa6);
            var avant = Freq(d);
            Dsp(0xd4);
            return $"{avant} ; {Freq(d)}";
        }, $"{hors} ; {hors}", $"{F(22050)} ; {hors}");
        bad += Un("sbawe32", "1Ch, 40h A6h pendant le transfert (le 4.13)", d =>
        {
            Auto();
            Dsp(0x40, 0xa6);
            return Freq(d);
        }, "11111 Hz, A6h/A6h", F(22050));
        // Le reset « chaud » (le mode UART, PB-240) garde 37h : la commande gardée s'y applique aussi.
        bad += Un("sb16", "1Ch, 41h 2Bh 11h, 34h (le mode UART), puis un reset", d =>
        {
            Auto();
            Dsp(0x41, 0x2b, 0x11);
            Dsp(0x34);
            ResetDsp();
            LireDsp();
            return Freq(d);
        }, F(11025), F(11025));
        // Le transfert fini sans départ, la commande gardée attend toujours ; 41h hors transfert l'efface : le 1Ch
        // suivant prend 44 100 Hz (F8h), non 11 025.
        bad += Un("sb16", "14h 0Fh 00h, 41h 2Bh 11h, la fin du bloc, puis 41h ACh 44h et 1Ch", d =>
        {
            Dma1(0x10000, 0xffff, auto: true);
            Dsp(0x41, 0x56, 0x22);
            Dsp(0x14, 0x0f, 0x00);
            Dsp(0x41, 0x2b, 0x11);
            var t = TicsIrq(d, 200);
            var fin = $"IRQ au tic {t}, {Freq(d)}";
            Dsp(0x41, 0xac, 0x44);
            Dsp(0x48, 0xff, 0x00);
            Dsp(0x1c);
            return $"{fin} ; {Freq(d)}";
        }, $"IRQ au tic 16, {F(11025)} ; {F(44100)}", $"IRQ au tic 16, {F(22050)} ; {F(44100)}");
        bad += Un("sb16", "41h 2Bh 11h hors transfert", d =>
        {
            Dsp(0x41, 0x2b, 0x11);
            return Freq(d);
        }, F(11025), F(11025), temoin: true);
        bad += Un("sbprov2", "1Ch, puis 40h D3h pendant le transfert (le DSP 3.02)", d =>
        {
            Dma1(0x10000, 0xffff, auto: true);
            Dsp(0x48, 0xff, 0x00);
            Dsp(0x1c);
            Dsp(0x40, 0xd3);
            return Freq(d);
        }, "22222 Hz, D3h/D3h", "22222 Hz, D3h/D3h", temoin: true);
        return bad;
    }
}
