// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — outillage de vérification et de mesure de la réécriture de SoftFloat (iXtal26.SoftFloat).
//
// Deux modes.
//
// `ver <fonction> [options de testfloat_gen]` lit sur l'entrée les lignes de `testfloat_gen <fonction>` : les
// opérandes, puis le résultat et les indicateurs que la SoftFloat C de référence (build/Linux-x86_64-GCC, variante
// 8086) a calculés. Pour chaque ligne, il recalcule le résultat avec la réécriture, le compare au bit près (la valeur
// et les cinq indicateurs, NaN compris), et réécrit la ligne avec son propre résultat sur la sortie, pour que
// `testfloat_ver -checkAll <fonction>` la juge à son tour. Le bilan va sur stderr ; le code de sortie est 1 au premier
// écart.
//
// `mesure` chronomètre la réécriture contre le `double` de l'hôte, sur les mêmes opérandes (des valeurs normales tirées
// d'une graine fixe) : ce que coûterait de calculer le x87 sur 80 bits au lieu de 64.

using System.Diagnostics;
using System.Globalization;
using iXtal26.SoftFloat;
using static iXtal26.SoftFloat.softfloat;

namespace iXtal26.SoftFloatBanc;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length >= 2 && args[0] == "ver")
            return Ver.Executer(args[1], args[2..]);
        if (args.Length >= 1 && args[0] == "mesure")
            return Mesure.Executer(args[1..]);
        Console.Error.WriteLine("usage : iXtal26.SoftFloatBanc ver <fonction> [options de testfloat_gen] < cas");
        Console.Error.WriteLine("        iXtal26.SoftFloatBanc mesure [millions d'opérations par banc]");
        return 2;
    }
}

internal static class Ver
{
    private enum Forme { Abz80, Az80, Az80Rx, AbBool, A80F32, A80F64, A80I32Rx, A80I64Rx, F32A80, F64A80, I32A80, I64A80 }

    private static readonly Dictionary<string, Forme> Formes = new()
    {
        ["extF80_add"] = Forme.Abz80, ["extF80_sub"] = Forme.Abz80, ["extF80_mul"] = Forme.Abz80,
        ["extF80_div"] = Forme.Abz80, ["extF80_rem"] = Forme.Abz80,
        ["extF80_sqrt"] = Forme.Az80, ["extF80_roundToInt"] = Forme.Az80Rx,
        ["extF80_eq"] = Forme.AbBool, ["extF80_le"] = Forme.AbBool, ["extF80_lt"] = Forme.AbBool,
        ["extF80_eq_signaling"] = Forme.AbBool, ["extF80_le_quiet"] = Forme.AbBool, ["extF80_lt_quiet"] = Forme.AbBool,
        ["extF80_to_f32"] = Forme.A80F32, ["extF80_to_f64"] = Forme.A80F64,
        ["extF80_to_i32"] = Forme.A80I32Rx, ["extF80_to_i64"] = Forme.A80I64Rx,
        ["f32_to_extF80"] = Forme.F32A80, ["f64_to_extF80"] = Forme.F64A80,
        ["i32_to_extF80"] = Forme.I32A80, ["i64_to_extF80"] = Forme.I64A80,
    };

    internal static int Executer(string fonction, string[] options)
    {
        if (!Formes.TryGetValue(fonction, out var forme))
        {
            Console.Error.WriteLine($"fonction inconnue : {fonction}");
            return 2;
        }
        byte arrondi = softfloat_round_near_even;
        var exact = false;
        byte precision = 80;
        byte petitesse = softfloat_tininess_afterRounding;
        foreach (var o in options)
            switch (o)
            {
                case "-rnear_even": arrondi = softfloat_round_near_even; break;
                case "-rminMag": arrondi = softfloat_round_minMag; break;
                case "-rmin": arrondi = softfloat_round_min; break;
                case "-rmax": arrondi = softfloat_round_max; break;
                case "-rnear_maxMag": arrondi = softfloat_round_near_maxMag; break;
                case "-rodd": arrondi = softfloat_round_odd; break;
                case "-exact": exact = true; break;
                case "-notexact": exact = false; break;
                case "-precision32": precision = 32; break;
                case "-precision64": precision = 64; break;
                case "-precision80": precision = 80; break;
                case "-tininessbefore": petitesse = softfloat_tininess_beforeRounding; break;
                case "-tininessafter": petitesse = softfloat_tininess_afterRounding; break;
                default:
                    if (o.StartsWith('-')) { Console.Error.WriteLine($"option inconnue : {o}"); return 2; }
                    break;
            }
        softfloat_roundingMode = arrondi;
        extF80_roundingPrecision = precision;
        softfloat_detectTininess = petitesse;

        using var entree = new StreamReader(Console.OpenStandardInput(), bufferSize: 1 << 20);
        using var sortie = new StreamWriter(Console.OpenStandardOutput(), bufferSize: 1 << 20);
        long cas = 0, ecarts = 0, ecartsC1 = 0, c1Poses = 0;
        string? ligne;
        while ((ligne = entree.ReadLine()) != null)
        {
            if (ligne.Length == 0)
                continue;
            var t = ligne.Split(' ');
            var nOperandes = forme is Forme.Abz80 or Forme.AbBool ? 2 : 1;
            if (t.Length != nOperandes + 2)
            {
                Console.Error.WriteLine($"ligne mal formée : {ligne}");
                return 2;
            }
            softfloat_exceptionFlags = 0;
            softfloat_roundedUp = false;
            var resultat = Calculer(forme, fonction, t, arrondi, exact);
            var drapeaux = softfloat_exceptionFlags;
            var c1 = softfloat_roundedUp;
            var indicateurs = drapeaux.ToString("X2");
            cas++;
            if (forme == Forme.AbBool)
            {
                // Une comparaison n'arrondit pas : C1 reste faux.
                if (c1 && ++ecartsC1 <= 10)
                    Console.Error.WriteLine($"écart C1 : {ligne}  →  C1 posé par une comparaison");
            }
            else if (arrondi != softfloat_round_odd)
            {
                // C1 contre sa définition : un résultat inexact, non invalide, arrondi vers le haut diffère de celui
                // qu'arrondit minMag (la troncature). Une fonction à paramètre `exact` ne lève l'inexactitude que sous
                // -exact : là, la différence seule la dit (l'arrondi à l'entier n'a pas de zéro signé qui change).
                softfloat_roundingMode = softfloat_round_minMag;
                var tronque = Calculer(forme, fonction, t, softfloat_round_minMag, exact);
                softfloat_roundingMode = arrondi;
                var inexact = (drapeaux & softfloat_flag_inexact) != 0
                              || forme is Forme.Az80Rx or Forme.A80I32Rx or Forme.A80I64Rx;
                var attendu = inexact && (drapeaux & softfloat_flag_invalid) == 0 && tronque != resultat;
                if (attendu != c1 && ++ecartsC1 <= 10)
                    Console.Error.WriteLine($"écart C1 : {ligne}  →  C1 {(c1 ? 1 : 0)}, attendu {(attendu ? 1 : 0)}");
                if (attendu)
                    c1Poses++;
            }
            if (resultat != t[nOperandes] || indicateurs != t[nOperandes + 1])
            {
                if (++ecarts <= 10)
                    Console.Error.WriteLine($"écart : {ligne}  →  {resultat} {indicateurs}");
            }
            for (var k = 0; k < nOperandes; k++)
            {
                sortie.Write(t[k]);
                sortie.Write(' ');
            }
            sortie.Write(resultat);
            sortie.Write(' ');
            sortie.WriteLine(indicateurs);
        }
        Console.Error.WriteLine($"{fonction} {string.Join(' ', options)} : {cas} cas, {ecarts + ecartsC1} écarts au bit près " +
                                $"(dont {ecartsC1} sur C1 ; C1 posé {c1Poses} fois)");
        return ecarts == 0 && ecartsC1 == 0 && cas > 0 ? 0 : 1;
    }

    private static string Calculer(Forme forme, string fonction, string[] t, byte arrondi, bool exact) => forme switch
    {
        Forme.Abz80 => Ecrire80(Binaire(fonction, Lire80(t[0]), Lire80(t[1]))),
        Forme.Az80 => Ecrire80(extF80_sqrt(Lire80(t[0]))),
        Forme.Az80Rx => Ecrire80(extF80_roundToInt(Lire80(t[0]), arrondi, exact)),
        Forme.AbBool => Comparer(fonction, Lire80(t[0]), Lire80(t[1])) ? "1" : "0",
        Forme.A80F32 => extF80_to_f32(Lire80(t[0])).v.ToString("X8"),
        Forme.A80F64 => extF80_to_f64(Lire80(t[0])).v.ToString("X16"),
        Forme.A80I32Rx => ((uint) extF80_to_i32(Lire80(t[0]), arrondi, exact)).ToString("X8"),
        Forme.A80I64Rx => ((ulong) extF80_to_i64(Lire80(t[0]), arrondi, exact)).ToString("X16"),
        Forme.F32A80 => Ecrire80(f32_to_extF80(new float32_t { v = (uint) Hex(t[0]) })),
        Forme.F64A80 => Ecrire80(f64_to_extF80(new float64_t { v = Hex(t[0]) })),
        Forme.I32A80 => Ecrire80(i32_to_extF80((int) (uint) Hex(t[0]))),
        Forme.I64A80 => Ecrire80(i64_to_extF80((long) Hex(t[0]))),
        _ => throw new InvalidOperationException(),
    };

    private static extFloat80_t Binaire(string f, extFloat80_t a, extFloat80_t b) => f switch
    {
        "extF80_add" => extF80_add(a, b),
        "extF80_sub" => extF80_sub(a, b),
        "extF80_mul" => extF80_mul(a, b),
        "extF80_div" => extF80_div(a, b),
        _ => extF80_rem(a, b),
    };

    private static bool Comparer(string f, extFloat80_t a, extFloat80_t b) => f switch
    {
        "extF80_eq" => extF80_eq(a, b),
        "extF80_le" => extF80_le(a, b),
        "extF80_lt" => extF80_lt(a, b),
        "extF80_eq_signaling" => extF80_eq_signaling(a, b),
        "extF80_le_quiet" => extF80_le_quiet(a, b),
        _ => extF80_lt_quiet(a, b),
    };

    private static ulong Hex(string s) => ulong.Parse(s, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);

    private static extFloat80_t Lire80(string s)
    {
        if (s.Length != 20)
            throw new FormatException($"extF80 de {s.Length} chiffres : {s}");
        return new extFloat80_t
        {
            signExp = (ushort) Hex(s[..4]),
            signif = Hex(s[4..]),
        };
    }

    private static string Ecrire80(extFloat80_t z) => z.signExp.ToString("X4") + z.signif.ToString("X16");
}

internal static class Mesure
{
    private const int N = 4096;

    internal static int Executer(string[] args)
    {
        var millions = args.Length > 0 ? int.Parse(args[0], CultureInfo.InvariantCulture) : 20;
        var tours = Math.Max(1, millions * 1_000_000 / N);
        var alea = new Random(1987);
        var da = new double[N];
        var db = new double[N];
        var xa = new extFloat80_t[N];
        var xb = new extFloat80_t[N];
        for (var i = 0; i < N; i++)
        {
            da[i] = (alea.NextDouble() + 0.5) * Math.Pow(2, alea.Next(-60, 60)) * (alea.Next(2) == 0 ? 1 : -1);
            db[i] = (alea.NextDouble() + 0.5) * Math.Pow(2, alea.Next(-60, 60)) * (alea.Next(2) == 0 ? 1 : -1);
            xa[i] = f64_to_extF80(new float64_t { v = (ulong) BitConverter.DoubleToInt64Bits(da[i]) });
            xb[i] = f64_to_extF80(new float64_t { v = (ulong) BitConverter.DoubleToInt64Bits(db[i]) });
        }
        softfloat_roundingMode = softfloat_round_near_even;
        extF80_roundingPrecision = 80;

        Console.WriteLine($"// {tours * (long) N / 1_000_000} millions d'opérations par banc, meilleur de 5 passes ; ns par opération");
        Console.WriteLine("opération        double   SoftFloat   rapport");
        Ligne("FADD", () => { ulong s = 0; for (var r = 0; r < tours; r++) for (var i = 0; i < N; i++) s ^= Bits(da[i] + db[i]); return s; },
                      () => { ulong s = 0; for (var r = 0; r < tours; r++) for (var i = 0; i < N; i++) s ^= extF80_add(xa[i], xb[i]).signif; return s; }, tours);
        Ligne("FSUB", () => { ulong s = 0; for (var r = 0; r < tours; r++) for (var i = 0; i < N; i++) s ^= Bits(da[i] - db[i]); return s; },
                      () => { ulong s = 0; for (var r = 0; r < tours; r++) for (var i = 0; i < N; i++) s ^= extF80_sub(xa[i], xb[i]).signif; return s; }, tours);
        Ligne("FMUL", () => { ulong s = 0; for (var r = 0; r < tours; r++) for (var i = 0; i < N; i++) s ^= Bits(da[i] * db[i]); return s; },
                      () => { ulong s = 0; for (var r = 0; r < tours; r++) for (var i = 0; i < N; i++) s ^= extF80_mul(xa[i], xb[i]).signif; return s; }, tours);
        Ligne("FDIV", () => { ulong s = 0; for (var r = 0; r < tours; r++) for (var i = 0; i < N; i++) s ^= Bits(da[i] / db[i]); return s; },
                      () => { ulong s = 0; for (var r = 0; r < tours; r++) for (var i = 0; i < N; i++) s ^= extF80_div(xa[i], xb[i]).signif; return s; }, tours);
        Ligne("FSQRT", () => { ulong s = 0; for (var r = 0; r < tours; r++) for (var i = 0; i < N; i++) s ^= Bits(Math.Sqrt(Math.Abs(da[i]))); return s; },
                       () => { ulong s = 0; for (var r = 0; r < tours; r++) for (var i = 0; i < N; i++) { var a = xa[i]; a.signExp &= 0x7FFF; s ^= extF80_sqrt(a).signif; } return s; }, tours);
        Ligne("FCOM", () => { ulong s = 0; for (var r = 0; r < tours; r++) for (var i = 0; i < N; i++) s += da[i] < db[i] ? 1UL : 0; return s; },
                      () => { ulong s = 0; for (var r = 0; r < tours; r++) for (var i = 0; i < N; i++) s += extF80_lt(xa[i], xb[i]) ? 1UL : 0; return s; }, tours);
        Ligne("FLD m64", () => { ulong s = 0; for (var r = 0; r < tours; r++) for (var i = 0; i < N; i++) s ^= Bits(da[i]); return s; },
                         () => { ulong s = 0; for (var r = 0; r < tours; r++) for (var i = 0; i < N; i++) s ^= f64_to_extF80(new float64_t { v = Bits(da[i]) }).signif; return s; }, tours);
        Ligne("FST m64", () => { ulong s = 0; for (var r = 0; r < tours; r++) for (var i = 0; i < N; i++) s ^= Bits(da[i]); return s; },
                         () => { ulong s = 0; for (var r = 0; r < tours; r++) for (var i = 0; i < N; i++) s ^= extF80_to_f64(xa[i]).v; return s; }, tours);
        Ligne("FIST m32", () => { ulong s = 0; for (var r = 0; r < tours; r++) for (var i = 0; i < N; i++) s ^= (ulong) (long) Math.Round(da[i] * 1e-12); return s; },
                          () => { ulong s = 0; for (var r = 0; r < tours; r++) for (var i = 0; i < N; i++) s ^= (ulong) extF80_to_i32(xa[i], softfloat_round_near_even, true); return s; }, tours);
        return 0;
    }

    private static ulong Bits(double d) => (ulong) BitConverter.DoubleToInt64Bits(d);

    private static void Ligne(string nom, Func<ulong> hote, Func<ulong> sf, int tours)
    {
        var ops = (double) tours * N;
        var th = Meilleur(hote) / ops * 1e9;
        var ts = Meilleur(sf) / ops * 1e9;
        Console.WriteLine($"{nom,-12} {th,8:F2}   {ts,9:F2}   {ts / th,6:F1}×");
    }

    private static double Meilleur(Func<ulong> banc)
    {
        banc();
        var meilleur = double.MaxValue;
        ulong puits = 0;
        for (var p = 0; p < 5; p++)
        {
            var sw = Stopwatch.StartNew();
            puits ^= banc();
            meilleur = Math.Min(meilleur, sw.Elapsed.TotalSeconds);
        }
        if (puits == 0x5EED)
            Console.Write("");
        return meilleur;
    }
}
