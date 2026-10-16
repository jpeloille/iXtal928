// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — outillage de vérification.
//
// Les cas de la fréquence du DSP 4.xx et de la saturation du FIR (G13.8c2, PLAN-G13.md) : PB-155, sur le banc du son
// (MaterielCasSon.cs). Le registre de fréquence de 8 bits du micrologiciel (4.05 pour la SB 16, 4.13 pour l'AWE32) ;
// ses Hz, ceux de MAME (46,61512 MHz / 1024 / 256 par pas, secondaire). La saturation, sur le tampon de la carte joué à
// la main, un échantillon par appel.

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
}
