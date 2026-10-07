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
//   - que chaque marqueur (« pcem bug, reproduced », « not reproduced » ou « fixed in hardware mode ») porte un numéro
//     sur sa ligne, et que ce numéro ait son entrée au registre ;
//   - que chaque entrée reproduite des sections A et B ait au moins un marqueur « reproduced », sauf une absence
//     (« *Reproduit* : par absence »), qui n'a pas de site ;
//   - qu'aucun marqueur ne contredise le statut de son entrée : « reproduced » pour une entrée qui n'est que NON
//     reproduite, ou l'inverse ;
//   - que le registre numérote ses entrées sans trou ni doublon, chacune avec un statut ;
//   - que chaque entrée des sections A et B porte son champ *G13*, et, reproduite, sa *Source* et son *Cas qui
//     discrimine* ;
//   - G13.2 : qu'un défaut corrigé en mode matériel le soit partout d'accord — ses marqueurs « fixed in hardware mode »,
//     le champ *Corrigé en mode matériel* de son entrée et la table des corrections (ModeMateriel) nomment les mêmes
//     défauts, et aucun de ses sites ne reste « reproduced » ;
//   - qu'aucune configuration lue par une porte (tools/gates/cfg, les gabarits .cfg.in, les profils que series.sh
//     passe à --config) ne porte la clé hardware_mode : les portes jouent le mode PCem.
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
        internal bool Reproduit, Absence, NonReproduit, Source, Cas, G13, Corrige;
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
        var corriges = new SortedSet<int>();
        var encoreReproduits = new SortedSet<int>();
        int marqueurs = 0, marqueursNon = 0, marqueursCorriges = 0;
        foreach (var f in Directory.EnumerateFiles(Sources, "*.cs", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            var rel = f.Replace('\\', '/');
            if (rel.Contains("/bin/") || rel.Contains("/obj/"))
                continue;
            var lignes = File.ReadAllLines(f);
            for (var i = 0; i < lignes.Length; i++)
            {
                var m = Regex.Match(lignes[i], @"pcem bug, (not reproduced|reproduced|fixed in hardware mode)");
                if (!m.Success)
                    continue;
                var non = m.Groups[1].Value == "not reproduced";
                var corrige = m.Groups[1].Value == "fixed in hardware mode";
                if (non)
                    marqueursNon++;
                else if (corrige)
                    marqueursCorriges++;
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
                    if (corrige)
                        corriges.Add(n);
                    else if (!non)
                        encoreReproduits.Add(n);
                    if (!non && !e.Reproduit)
                        fautes.Add($"{rel}:{i + 1} : « {m.Groups[1].Value} » pour PB-{n:00}, que le registre ne dit que NON reproduit");
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

        // G13.2 — les corrections du mode matériel : les marqueurs, le registre et la table disent les mêmes.
        var table = new SortedSet<int>(ModeMateriel.Corrections.Select(c => c.Pb));
        var champ = new SortedSet<int>(entrees.Where(x => x.Value.Corrige).Select(x => x.Key));
        foreach (var n in corriges.Union(table).Union(champ))
        {
            var ou = new List<string>();
            if (!corriges.Contains(n)) ou.Add("sans marqueur « fixed in hardware mode »");
            if (!table.Contains(n)) ou.Add("absent de la table de ModeMateriel");
            if (!champ.Contains(n)) ou.Add("sans champ *Corrigé en mode matériel*");
            if (encoreReproduits.Contains(n)) ou.Add("un de ses sites reste « reproduced »");
            if (ou.Count > 0)
                fautes.Add($"PB-{n:00}, corrigé en mode matériel : {string.Join(", ", ou)}");
        }

        // Aucune configuration lue par une porte ne porte la clé hardware_mode.
        var cfgs = new List<string>();
        if (Directory.Exists("tools/gates/cfg"))
            cfgs.AddRange(Directory.EnumerateFiles("tools/gates/cfg").Where(f => f.EndsWith(".cfg") || f.EndsWith(".cfg.in")));
        if (File.Exists("tools/gates/series.sh"))
            foreach (Match cle in Regex.Matches(File.ReadAllText("tools/gates/series.sh"), @"--config\s+(\S+)"))
                if (!cle.Groups[1].Value.Contains('$') && File.Exists(cle.Groups[1].Value))
                    cfgs.Add(cle.Groups[1].Value);
        foreach (var f in cfgs.Distinct())
            if (File.ReadLines(f).Any(l => Regex.IsMatch(l, @"^\s*hardware_mode\s*=")))
                fautes.Add($"{f} : une configuration lue par une porte porte la clé hardware_mode");

        int a = entrees.Values.Count(e => e.Section == 'A'), b = entrees.Values.Count(e => e.Section == 'B'),
            c = entrees.Values.Count(e => e.Section == 'C');
        int abRep = entrees.Values.Count(e => e.Section is 'A' or 'B' && e.Reproduit);
        Console.WriteLine($"  registre : {entrees.Count} entrées (A {a}, B {b}, C {c}), dont {abRep} reproduites en A et B");
        Console.WriteLine($"  marqueurs : {marqueurs} « reproduced », {marqueursCorriges} « fixed in hardware mode », " +
                          $"{marqueursNon} « not reproduced » ; {reproduits.Count} défauts cités comme reproduits, " +
                          $"{corriges.Count} corrigés en mode matériel ; {cfgs.Distinct().Count()} configurations de portes lues");
        foreach (var f in fautes.Take(60))
            Console.WriteLine($"  {f}");
        if (fautes.Count > 60)
            Console.WriteLine($"  … et {fautes.Count - 60} autres");

        Console.WriteLine(fautes.Count == 0
            ? $"\nVert : {entrees.Count} défauts au registre, {marqueurs + marqueursCorriges + marqueursNon} marqueurs ; chaque marqueur porte son numéro, chaque défaut reproduit des sections A et B a ses sites et ses champs."
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
            else if (l.StartsWith("*Corrigé en mode matériel*"))
                cur.Corrige = true;
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
