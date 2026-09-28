// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: aucun — PCem sort le carré du haut-parleur tel quel (sound_speaker.c:17-37).
// STATUS: host

namespace iXtal26.Host;

/// <summary>
/// Le haut-parleur du 5150 entre le mixeur et la carte son de l'hôte.
///
/// Le mixeur rend un carré exact, 0 / 0x1400. Un casque moderne le restitue en entier,
/// fondamentale comprise, et le bip paraît rond. Le cône de 57 mm du 5150, sans baffle
/// et vissé dans un châssis métallique, n'en rendait presque pas la fondamentale grave,
/// résonnait dans le médium et saturait : un son nasillard et criard.
///
/// Quatre étages, en double, à 48 kHz :
/// passe-haut (le cône sans baffle, et la composante continue du carré unipolaire),
/// crête (la résonance du cône), passe-bas (sa limite haute), gain et saturation douce.
/// Biquads de R. Bristow-Johnson, « Audio EQ Cookbook ».
///
/// DEVIATION: n'existe pas chez PCem. Appliqué à la copie int16 que SdlAudio envoie à
///   SDL, jamais au tampon du mixeur : sound_hash et speaker-probe n'en voient rien.
/// </summary>
internal sealed class SpeakerModel
{
    private const double SampleRate = 48000.0;

    // À ajuster à l'oreille : aucune mesure publiée du 5150 n'a servi à les fixer.
    internal const double HighPassHz = 350.0;
    internal const double ResonanceHz = 2800.0;
    internal const double ResonanceQ = 2.0;
    internal const double ResonanceGainDb = 6.0;
    internal const double LowPassHz = 8000.0;
    internal const double MakeupGain = 2.5;
    internal const double SaturationLevel = 24000.0;

    private Biquad _highPass = Biquad.HighPass(HighPassHz, 0.7071);
    private Biquad _resonance = Biquad.Peak(ResonanceHz, ResonanceQ, ResonanceGainDb);
    private Biquad _lowPass = Biquad.LowPass(LowPassHz, 0.7071);

    internal void Reset()
    {
        _highPass.Reset();
        _resonance.Reset();
        _lowPass.Reset();
    }

    internal double Process(double x)
    {
        double y = _lowPass.Process(_resonance.Process(_highPass.Process(x))) * MakeupGain;
        return SaturationLevel * Math.Tanh(y / SaturationLevel);
    }

    private struct Biquad
    {
        private double _b0, _b1, _b2, _a1, _a2;
        private double _z1, _z2;

        private static Biquad Normalised(double b0, double b1, double b2, double a0, double a1, double a2) =>
            new() { _b0 = b0 / a0, _b1 = b1 / a0, _b2 = b2 / a0, _a1 = a1 / a0, _a2 = a2 / a0 };

        internal static Biquad HighPass(double hz, double q)
        {
            double w = 2 * Math.PI * hz / SampleRate, c = Math.Cos(w), alpha = Math.Sin(w) / (2 * q);
            return Normalised((1 + c) / 2, -(1 + c), (1 + c) / 2, 1 + alpha, -2 * c, 1 - alpha);
        }

        internal static Biquad LowPass(double hz, double q)
        {
            double w = 2 * Math.PI * hz / SampleRate, c = Math.Cos(w), alpha = Math.Sin(w) / (2 * q);
            return Normalised((1 - c) / 2, 1 - c, (1 - c) / 2, 1 + alpha, -2 * c, 1 - alpha);
        }

        internal static Biquad Peak(double hz, double q, double gainDb)
        {
            double w = 2 * Math.PI * hz / SampleRate, c = Math.Cos(w), alpha = Math.Sin(w) / (2 * q);
            double a = Math.Pow(10, gainDb / 40);
            return Normalised(1 + alpha * a, -2 * c, 1 - alpha * a, 1 + alpha / a, -2 * c, 1 - alpha / a);
        }

        internal void Reset() => _z1 = _z2 = 0;

        // Forme directe transposée II.
        internal double Process(double x)
        {
            double y = _b0 * x + _z1;
            _z1 = _b1 * x - _a1 * y + _z2;
            _z2 = _b2 * x - _a2 * y;
            return y;
        }
    }

    /// <summary>
    /// --speaker-check : le bip du POST tel que le mixeur le rend (carré 0 / 0x1400,
    /// diviseur 1331 du canal 2, 896 Hz), mesuré par Goertzel avant et après le modèle,
    /// puis écrit en deux WAV pour l'écoute. Critère : la fondamentale baisse par rapport
    /// aux harmoniques 3 et 5, et rien n'atteint la pleine échelle.
    /// </summary>
    internal static int SelfCheck(string? outputDirectory)
    {
        const double pitHz = 1193182.0;
        const int divisor = 1331;
        const int seconds = 1;
        double toneHz = pitHz / divisor;

        int n = (int)SampleRate * seconds;
        var ideal = new double[n];
        for (int i = 0; i < n; i++)
            ideal[i] = (i * toneHz / SampleRate) % 1.0 < 0.5 ? 0x1400 : 0;

        var model = new SpeakerModel();
        var shaped = new double[n];
        for (int i = 0; i < n; i++)
            shaped[i] = model.Process(ideal[i]);

        Console.WriteLine($"bip du POST : {toneHz:F1} Hz, {seconds} s à 48 kHz");
        Console.WriteLine("                   h1       h3       h5       h7     crête   eff.");
        Report("son fidele   ", ideal, toneHz);
        Report("reglage usine", shaped, toneHz);

        double ratioIdeal = Goertzel(ideal, toneHz * 3) / Goertzel(ideal, toneHz);
        double ratioShaped = Goertzel(shaped, toneHz * 3) / Goertzel(shaped, toneHz);
        double peak = shaped.Max(Math.Abs);

        bool ok = true;
        ok &= Check("h3/h1 monte", ratioShaped > ratioIdeal,
                    $"{Db(ratioIdeal):F1} dB -> {Db(ratioShaped):F1} dB");
        ok &= Check("sous la pleine echelle", peak < 32767, $"crete {peak:F0}");

        if (outputDirectory is not null)
        {
            Directory.CreateDirectory(outputDirectory);
            WriteWav(Path.Combine(outputDirectory, "bip-son-fidele.wav"), ideal);
            WriteWav(Path.Combine(outputDirectory, "bip-reglage-usine.wav"), shaped);
            Console.WriteLine($"WAV ecrits dans {outputDirectory}");
        }

        return ok ? 0 : 1;
    }

    private static bool Check(string what, bool ok, string detail)
    {
        Console.WriteLine($"{(ok ? "OK  " : "ECHEC")} {what} ({detail})");
        return ok;
    }

    private static double Db(double ratio) => 20 * Math.Log10(ratio);

    private static void Report(string label, double[] s, double f)
    {
        // Le dixième de seconde initial est écarté : le régime transitoire des filtres.
        double[] steady = s[(s.Length / 10)..];
        double rms = Math.Sqrt(steady.Average(v => v * v));
        Console.WriteLine($"{label} " +
                          string.Join(" ", new[] { 1, 3, 5, 7 }.Select(h => $"{Db(Goertzel(steady, f * h) / 32768),6:F1}dB")) +
                          $" {steady.Max(Math.Abs),7:F0} {rms,6:F0}");
    }

    /// <summary>Amplitude de la composante à f, en unités d'échantillon.</summary>
    private static double Goertzel(double[] s, double f)
    {
        double w = 2 * Math.PI * f / SampleRate, k = 2 * Math.Cos(w), p1 = 0, p2 = 0;
        foreach (double x in s)
        {
            double p0 = x + k * p1 - p2;
            p2 = p1;
            p1 = p0;
        }

        double re = p1 - p2 * Math.Cos(w), im = p2 * Math.Sin(w);
        return 2 * Math.Sqrt(re * re + im * im) / s.Length;
    }

    private static void WriteWav(string path, double[] s)
    {
        using var w = new BinaryWriter(File.Create(path));
        w.Write("RIFF"u8);
        w.Write(36 + s.Length * 2);
        w.Write("WAVEfmt "u8);
        w.Write(16);
        w.Write((short)1);
        w.Write((short)1);
        w.Write((int)SampleRate);
        w.Write((int)SampleRate * 2);
        w.Write((short)2);
        w.Write((short)16);
        w.Write("data"u8);
        w.Write(s.Length * 2);
        foreach (double v in s)
            w.Write((short)Math.Clamp(Math.Round(v), -32768, 32767));
    }
}
