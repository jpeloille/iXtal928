// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — outillage de mesure.
//
// G13.3, suite — LE TEMPS D'UN REP DEVANT AUTRE CHOSE QU'UNE CHAÎNE.
//
// PCem facture 20 cycles et vide la file (808x.c, rep(), `default:`) ; le mode matériel (PB-177) n'en fait qu'un
// préfixe, la file intacte. Le silicium, lui, est dans le corpus : chaque cas SST porte sa trace, un élément par cycle
// d'horloge. Les temps absolus ne se comparent pas (le modèle de temps de PCem n'est pas celui du 8088, et la
// sonde part d'une file vide quand SST la donne pleine dans la moitié des cas du 8088 et tous ceux du 8086) ; le
// SURCOÛT du REP, si : dans une forme, les cas avec REP et sans REP tirent leurs opérandes de la même façon.
//
// Par forme, et dans chaque strate (le nombre des autres préfixes, la file de départ vide ou non), la moyenne avec REP
// moins la moyenne sans REP : celle du silicium (la longueur de la trace), celle du cœur C# (les cycles de ses pas,
// dans le mode demandé), et leur écart, la différence des différences, avec son erreur type. Les strates se pèsent par
// leurs cas avec REP. Les chaînes (A4h, ACh, ADh) et AAM 0 sont hors de la mesure ; un cas dont le cœur n'atteint pas
// l'IP final de SST aussi (compté à part). Le total se donne aussi par file de départ : la sonde part toujours d'une file
// vide, et seuls les cas où SST la donne vide aussi se comparent au plus près (le silicium y lit l'octet du REP sur le
// bus, comme le cœur) ; file pleine, le silicium a l'octet sans attendre, le cœur non. --controle fait de la mesure une
// porte : rouge si l'écart de la file vide sort de deux erreurs types, ou si le corpus n'a pas de tels cas (le 8086).

namespace iXtal26.Diff;

internal static class SstRepTemps
{
    private sealed class Groupe
    {
        public int N;
        public double Sil, Emu, Ecart, Ecart2;

        public void Ajouter(int sil, int emu)
        {
            N++;
            Sil += sil;
            Emu += emu;
            Ecart += sil - emu;
            Ecart2 += (double)(sil - emu) * (sil - emu);
        }

        public double Variance => N > 1 ? (Ecart2 - Ecart * Ecart / N) / (N - 1) : 0;
    }

    /// <param name="ops">Les formes à mesurer ; vide, toutes celles du corpus.</param>
    /// <param name="casPath">Un TSV d'un cas par ligne (forme, numéro, REP, autres préfixes, file, silicium, cœur), pour
    /// confronter deux modes cas par cas.</param>
    /// <param name="controle">Rouge (1) si l'écart, file de départ vide, sort de deux erreurs types.</param>
    public static int Run(string vectorsDir, bool cpu8086, int limit, IReadOnlyList<string> ops, string? casPath,
                          bool controle)
    {
        using var cas = casPath is null ? null : new StreamWriter(casPath);
        Console.WriteLine($"Le surcoût d'un REP devant autre chose qu'une chaîne — corpus {vectorsDir}, cœur C# " +
                          $"{(cpu8086 ? "8086" : "8088")}, mode {(ModeMateriel.Actif ? "matériel" : "PCem")}\n");
        Console.WriteLine("  forme   REP  sans REP   silicium   cœur C#    écart (silicium − cœur)");

        // [0] l'ensemble, [1] la file de départ vide, [2] pleine.
        double[] poids = new double[3], sSil = new double[3], sEmu = new double[3], sEcart = new double[3],
                 sVar = new double[3];
        int horsIp = 0, formes = 0;
        foreach (var path in Directory.GetFiles(vectorsDir, "*.json.gz").Order())
        {
            var op = Path.GetFileName(path).Replace(".json.gz", "");
            if (ops.Count > 0 && !ops.Contains(op))
                continue;
            var cases = SstProbe.LoadPublic(path);
            var n = limit > 0 ? Math.Min(limit, cases.Count) : cases.Count;
            var strates = new Dictionary<(int autres, bool file, bool rep), Groupe>();
            for (var i = 0; i < n; i++)
            {
                var c = cases[i];
                if (SstProbe.IsAam0(c.bytes) || SstProbe.JouerChaine(c.bytes) > 1)
                    continue;
                int k = 0, autres = 0;
                var rep = false;
                while (k < c.bytes.Length && c.bytes[k] is 0x26 or 0x2E or 0x36 or 0x3E or 0xF0 or 0xF1 or 0xF2 or 0xF3)
                {
                    if (c.bytes[k] is 0xF2 or 0xF3)
                        rep = true;
                    else
                        autres++;
                    k++;
                }
                // Une forme de chaîne sans REP : ses cas avec REP sont hors de la mesure, ceux-ci n'ont pas de pendant.
                if (k < c.bytes.Length && c.bytes[k] is (>= 0xA4 and <= 0xA7) or (>= 0xAA and <= 0xAF))
                    continue;
                var emu = SstProbe.TempsCsharp(c, cpu8086);
                if (emu < 0)
                {
                    horsIp++;
                    continue;
                }
                var cle = (autres, c.initial.queue is { Length: > 0 }, rep);
                if (!strates.TryGetValue(cle, out var g))
                    strates[cle] = g = new Groupe();
                g.Ajouter(c.cycles.GetArrayLength(), emu);
                cas?.WriteLine(string.Join('\t', op, c.Index, rep ? 1 : 0, autres, cle.Item2 ? 1 : 0,
                                           c.cycles.GetArrayLength(), emu));
            }

            double p = 0, dSil = 0, dEmu = 0, dEcart = 0, v = 0;
            int nSans = 0;
            foreach (var ((autres, file, rep), avec) in strates)
            {
                if (!rep || !strates.TryGetValue((autres, file, false), out var sans) || sans.N < 2 || avec.N < 1)
                    continue;
                var dS = avec.N * (avec.Sil / avec.N - sans.Sil / sans.N);
                var dE = avec.N * (avec.Emu / avec.N - sans.Emu / sans.N);
                var dX = avec.N * (avec.Ecart / avec.N - sans.Ecart / sans.N);
                var vX = (double)avec.N * avec.N * (avec.Variance / avec.N + sans.Variance / sans.N);
                p += avec.N;
                nSans += sans.N;
                dSil += dS;
                dEmu += dE;
                dEcart += dX;
                v += vX;
                var t = file ? 2 : 1;
                poids[t] += avec.N;
                sSil[t] += dS;
                sEmu[t] += dE;
                sEcart[t] += dX;
                sVar[t] += vX;
            }
            if (p == 0)
                continue;
            formes++;
            Console.WriteLine($"  {op,-6} {p,4} {nSans,9} {dSil / p,10:+0.0;-0.0} {dEmu / p,9:+0.0;-0.0}" +
                              $"   {dEcart / p:+0.0;-0.0} ± {Math.Sqrt(v) / p:0.0}");
            poids[0] += p;
            sSil[0] += dSil;
            sEmu[0] += dEmu;
            sEcart[0] += dEcart;
            sVar[0] += v;
        }

        if (poids[0] == 0)
        {
            Console.WriteLine("\nAucun cas avec REP devant autre chose qu'une chaîne.");
            return 2;
        }
        Console.WriteLine($"\n  {formes} formes, " + Total(0, "cas avec REP") +
                          (horsIp > 0 ? $" {horsIp} cas hors de la mesure : le cœur n'atteint pas l'IP final de SST." : ""));
        if (poids[1] > 0)
            Console.WriteLine("  file de départ vide (la sonde aussi) : " + Total(1, "cas"));
        if (poids[2] > 0)
            Console.WriteLine("  file de départ pleine : " + Total(2, "cas"));
        if (!controle)
            return 0;
        if (poids[1] == 0)
        {
            Console.WriteLine("\nRouge : aucun cas avec REP dont la file de départ soit vide.");
            return 1;
        }
        var ecart = sEcart[1] / poids[1];
        var deuxEt = 2 * Math.Sqrt(sVar[1]) / poids[1];
        Console.WriteLine(Math.Abs(ecart) <= deuxEt
            ? $"\nVert : file de départ vide, l'écart {ecart:+0.0;-0.0} est dans deux erreurs types ({deuxEt:0.0})."
            : $"\nRouge : file de départ vide, l'écart {ecart:+0.0;-0.0} sort de deux erreurs types ({deuxEt:0.0}).");
        return Math.Abs(ecart) <= deuxEt ? 0 : 1;

        string Total(int t, string quoi) =>
            $"{poids[t]} {quoi} : silicium {sSil[t] / poids[t]:+0.0;-0.0} cycles, cœur C# {sEmu[t] / poids[t]:+0.0;-0.0}, " +
            $"écart {sEcart[t] / poids[t]:+0.0;-0.0} ± {Math.Sqrt(sVar[t]) / poids[t]:0.0}.";
    }
}
