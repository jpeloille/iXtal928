// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// sb16-filter-check (G12.1) — LE FIR DE LA SB 16, des deux côtés.
//
// recalc_sb16_filter (sound_sb_dsp.c:79-105) calcule les 51 coefficients de low_fir_sb16 par cos() et sin() de la
// libm de l'HÔTE : glibc côté oracle, .NET côté C# (qui l'appelle). Un ulp d'écart changerait les échantillons de
// la SB 16, que le hachage du son verrait sans qu'aucune instruction ne diverge. On compare donc les coefficients
// au bit près, pour chaque fréquence que l'invité peut demander : 41h de 1 à 65 535 Hz, et les 256 constantes de
// temps de 40h, 1 000 000 / (256 - tc) jusqu'à 1 MHz (PLAN-G12.md, décision n° 13). Des deux côtés le calcul passe
// par sb_exec_command, sur un DSP de brouillon de type SB16 dont sb_freq vaut -1 (sinon pas de calcul,
// :385, :400). Jamais la fréquence 0 (R9, PB-150).

using iXtal26.Sound;

namespace iXtal26.Diff;

internal static class Sb16FilterCheck
{
    internal static int Run()
    {
        Oracle.CheckAbi();

        var o = new float[sound_sb_dsp.SB16_NCoef];
        var d = new sb_dsp_t();
        var cas = 0;
        var bad = 0;
        foreach (var (cmd, valeurs) in new (int, IEnumerable<int>)[] { (0x41, Enumerable.Range(1, 65535)), (0x40, Enumerable.Range(0, 256)) })
        {
            foreach (var v in valeurs)
            {
                Oracle.h_sb16_filter(cmd, v, o);
                d.sb_type = sound_sb_dsp.SB16;
                d.sb_freq = -1;
                d.sb_command = (byte)cmd;
                if (cmd == 0x41)
                {
                    d.sb_data[0] = (byte)(v >> 8);
                    d.sb_data[1] = (byte)v;
                }
                else
                    d.sb_data[0] = (byte)v;
                sound_sb_dsp.sb_exec_command(d);
                cas++;
                for (var n = 0; n < o.Length; n++)
                {
                    var c = sound_sb_dsp.low_fir_sb16_coef[n];
                    if (BitConverter.SingleToInt32Bits(o[n]) == BitConverter.SingleToInt32Bits(c))
                        continue;
                    if (bad < 10)
                        Console.WriteLine($"  {cmd:X2}h {v} : coef[{n}] oracle {o[n]:R} ({BitConverter.SingleToInt32Bits(o[n]):X8}), " +
                                          $"C# {c:R} ({BitConverter.SingleToInt32Bits(c):X8})");
                    bad++;
                }
            }
        }

        Console.WriteLine(bad == 0
            ? $"Vert : {cas} jeux de {o.Length} coefficients identiques au bit près (41h de 1 à 65 535 Hz, 40h sur ses 256 " +
              "constantes) — cos et sin de .NET rendent ici ce que rend glibc."
            : $"ROUGE : {bad} coefficient(s) divergent(s) sur {cas} jeux.");
        return bad == 0 ? 0 : 1;
    }
}
