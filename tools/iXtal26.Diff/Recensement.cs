// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — outillage de vérification.
//
// recensement — le registre des défauts de PCem contre les marqueurs du code (G13.1, PLAN-G13.md).
//
// Une seule méthode : le numéro PB-nn sur la ligne du marqueur, jamais un numéro de ligne, qui dérive. La porte lit
// PCEM_BUGS.md et les sources de iXtal26/ (ni bin/ ni obj/) depuis le répertoire courant, la racine du dépôt ou son
// bac à sable, et exige :
//   - que chaque marqueur (« pcem bug, reproduced » ou « pcem bug, not reproduced ») porte un numéro sur sa ligne,
//     et que ce numéro ait son entrée au registre ;
//   - que chaque entrée reproduite des sections A et B ait au moins un marqueur « reproduced », sauf une absence
//     (« *Reproduit* : par absence »), qui n'a pas de site ;
//   - qu'aucun marqueur ne contredise le statut de son entrée : « reproduced » pour une entrée qui n'est que NON
//     reproduite, ou l'inverse ;
//   - que le registre numérote ses entrées sans trou ni doublon, chacune avec un statut ;
//   - que chaque entrée des sections A et B porte son champ *G13*, et, reproduite, sa *Source* et son *Cas qui
//     discrimine*.
// Tout numéro sur la ligne d'un marqueur compte comme un site : un renvoi à un autre défaut va sur la ligne suivante.
// Les contrôles négatifs : un numéro retiré d'un marqueur, ou un champ *G13* retiré d'une entrée, la rendent rouge.

using System.Text.RegularExpressions;

namespace iXtal26.Diff;

internal static class Recensement
{
    private const string Registre = "PCEM_BUGS.md";
    private const string Sources = "iXtal26";

    private sealed class Entree
    {
        internal char Section;
        internal bool Reproduit, Absence, NonReproduit, Source, Cas, G13;
    }

    internal static int Run()
    {
        if (!File.Exists(Registre) || !Directory.Exists(Sources))
        {
            Console.WriteLine($"recensement : {Registre} ou {Sources}/ introuvable (la porte se lance depuis la racine du dépôt ou de son bac à sable)");
            return 2;
        }

        var fautes = new List<string>();
        var entrees = LireRegistre(fautes);

        var reproduits = new Dictionary<int, int>();
        int marqueurs = 0, marqueursNon = 0;
        foreach (var f in Directory.EnumerateFiles(Sources, "*.cs", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            var rel = f.Replace('\\', '/');
            if (rel.Contains("/bin/") || rel.Contains("/obj/"))
                continue;
            var lignes = File.ReadAllLines(f);
            for (var i = 0; i < lignes.Length; i++)
            {
                var m = Regex.Match(lignes[i], @"pcem bug, (not reproduced|reproduced)");
                if (!m.Success)
                    continue;
                var non = m.Groups[1].Value == "not reproduced";
                if (non)
                    marqueursNon++;
                else
                    marqueurs++;
                var numeros = Regex.Matches(lignes[i], @"\bPB-(\d+)\b").Select(n => int.Parse(n.Groups[1].Value)).ToList();
                if (numeros.Count == 0)
                    fautes.Add($"{rel}:{i + 1} : marqueur sans numéro sur sa ligne");
                if (Regex.IsMatch(lignes[i], @"\bPB-[A-Z]"))
                    fautes.Add($"{rel}:{i + 1} : identifiant provisoire sur un marqueur");
                foreach (var n in numeros)
                {
                    if (!entrees.TryGetValue(n, out var e))
                    {
                        fautes.Add($"{rel}:{i + 1} : PB-{n:00} n'a pas d'entrée au registre");
                        continue;
                    }
                    if (!non)
                        reproduits[n] = reproduits.GetValueOrDefault(n) + 1;
                    if (!non && !e.Reproduit)
                        fautes.Add($"{rel}:{i + 1} : « reproduced » pour PB-{n:00}, que le registre ne dit que NON reproduit");
                    if (non && !e.NonReproduit)
                        fautes.Add($"{rel}:{i + 1} : « not reproduced » pour PB-{n:00}, que le registre ne dit que reproduit");
                }
            }
        }

        foreach (var (n, e) in entrees.OrderBy(x => x.Key))
        {
            if (e.Section is not ('A' or 'B'))
                continue;
            if (e.Reproduit && !e.Absence && !reproduits.ContainsKey(n))
                fautes.Add($"PB-{n:00} (section {e.Section}) : reproduit, mais aucun marqueur « reproduced » ne porte son numéro");
            if (!e.G13)
                fautes.Add($"PB-{n:00} (section {e.Section}) : sans champ *G13*");
            if (e.Reproduit && !(e.Source && e.Cas))
                fautes.Add($"PB-{n:00} (section {e.Section}) : reproduit, sans {(e.Source ? "" : "*Source* ")}{(e.Cas ? "" : "*Cas qui discrimine*")}".TrimEnd());
        }

        int a = entrees.Values.Count(e => e.Section == 'A'), b = entrees.Values.Count(e => e.Section == 'B'),
            c = entrees.Values.Count(e => e.Section == 'C');
        int abRep = entrees.Values.Count(e => e.Section is 'A' or 'B' && e.Reproduit);
        Console.WriteLine($"  registre : {entrees.Count} entrées (A {a}, B {b}, C {c}), dont {abRep} reproduites en A et B");
        Console.WriteLine($"  marqueurs : {marqueurs} « reproduced », {marqueursNon} « not reproduced » ; {reproduits.Count} défauts cités comme reproduits");
        foreach (var f in fautes.Take(60))
            Console.WriteLine($"  {f}");
        if (fautes.Count > 60)
            Console.WriteLine($"  … et {fautes.Count - 60} autres");

        Console.WriteLine(fautes.Count == 0
            ? $"\nVert : {entrees.Count} défauts au registre, {marqueurs + marqueursNon} marqueurs ; chaque marqueur porte son numéro, chaque défaut reproduit des sections A et B a ses sites et ses champs."
            : $"\n{fautes.Count} écart(s) entre le registre et les marqueurs.");
        return fautes.Count == 0 ? 0 : 1;
    }

    private static Dictionary<int, Entree> LireRegistre(List<string> fautes)
    {
        var entrees = new Dictionary<int, Entree>();
        char? section = null;
        Entree? cur = null;
        foreach (var l in File.ReadLines(Registre))
        {
            var s = Regex.Match(l, @"^## ([ABC])\.");
            if (s.Success)
            {
                section = s.Groups[1].Value[0];
                cur = null;
                continue;
            }
            if (l.StartsWith("## "))
            {
                section = null;
                cur = null;
                continue;
            }
            var t = Regex.Match(l, @"^### PB-(\S+)");
            if (t.Success)
            {
                if (section == null)
                    fautes.Add($"{Registre} : PB-{t.Groups[1].Value} hors des sections A, B et C");
                if (!int.TryParse(t.Groups[1].Value, out var n))
                {
                    fautes.Add($"{Registre} : identifiant provisoire PB-{t.Groups[1].Value}");
                    cur = null;
                    continue;
                }
                if (entrees.ContainsKey(n))
                    fautes.Add($"{Registre} : PB-{n:00} en double");
                cur = entrees[n] = new Entree { Section = section ?? '?' };
                continue;
            }
            if (cur == null)
                continue;
            if (l.StartsWith("*Reproduit"))
            {
                cur.Reproduit = true;
                cur.Absence |= l.StartsWith("*Reproduit* : par absence");
            }
            else if (l.StartsWith("*NON reproduit") || l.StartsWith("*Non reproduit"))
                cur.NonReproduit = true;
            else if (l.StartsWith("*Source*"))
                cur.Source = true;
            else if (l.StartsWith("*Cas qui discrimine*"))
                cur.Cas = true;
            else if (l.StartsWith("*G13*"))
                cur.G13 = true;
        }

        var max = entrees.Count == 0 ? 0 : entrees.Keys.Max();
        for (var n = 1; n <= max; n++)
            if (!entrees.ContainsKey(n))
                fautes.Add($"{Registre} : PB-{n:00} manque (les numéros se suivent sans trou)");
        foreach (var (n, e) in entrees.OrderBy(x => x.Key))
            if (!e.Reproduit && !e.NonReproduit)
                fautes.Add($"{Registre} : PB-{n:00} sans statut (*Reproduit* ou *NON reproduit*)");
        return entrees;
    }
}
