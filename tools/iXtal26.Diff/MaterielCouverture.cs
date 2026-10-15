// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — outillage de vérification.
//
// couverture-materiel — la couverture comptée du mode matériel (G13.10b, PLAN-G13.md). Chaque correction de la table
// (ModeMateriel.Corrections) a son cas (materiel-cas), et la série joue chacun sous son seul PB. Cette commande les joue
// tous dans un même processus, sous le mode demandé (la série : --hardware-mode tout), là où une correction peut changer
// ce qu'une autre lit (en G13.9f, le cas de PB-225 rougissait sous « tout », le bit 3 de PB-224 en plus) ; puis elle
// compte, par domaine, les corrections, leurs cas verts et ce qu'a dit la sonde de chacune. Rouge : une correction sans
// cas, un cas sans correction, un cas rouge, une sonde muette pour une correction demandée.

namespace iXtal26.Diff;

internal static class MaterielCouverture
{
    internal static int Run(string[] args)
    {
        var table = ModeMateriel.Corrections.ToDictionary(c => c.Pb);
        var cas = MaterielCas.Liste();
        var fautes = new List<string>();
        var seuls = new List<int>();
        foreach (var a in args.Where(a => a.StartsWith("PB-", StringComparison.OrdinalIgnoreCase)))
            if (int.TryParse(a[3..], out var k) && cas.ContainsKey(k))
                seuls.Add(k);
            else
                fautes.Add($"{a} : pas de cas");
        foreach (var n in table.Keys.Where(n => !cas.ContainsKey(n)))
            fautes.Add($"PB-{n:00} : une correction sans cas");
        foreach (var n in cas.Keys.Where(n => !table.ContainsKey(n)))
            fautes.Add($"PB-{n:00} : un cas sans correction");

        Console.WriteLine($"couverture du mode matériel, mode {ModeMateriel.Description} : {table.Count} corrections, " +
                          $"{cas.Count} cas\n");
        var parDomaine = new SortedDictionary<string, (int corrections, int verts, int sondes)>();
        var sortie = Console.Out;
        foreach (var (n, jouer) in seuls.Count > 0 ? seuls.Select(k => KeyValuePair.Create(k, cas[k])) : cas.OrderBy(x => x.Key))
        {
            if (!table.TryGetValue(n, out var c))
                continue;
            var demande = ModeMateriel.Demande(n);
            var avant = ModeMateriel.Sonde[n];
            var capture = new StringWriter();
            int bad;
            Console.SetOut(capture);
            try
            {
                bad = jouer(demande);
            }
            catch (Exception ex)
            {
                bad = 1;
                capture.WriteLine($"  exception : {ex.GetType().Name} : {ex.Message}");
            }
            finally
            {
                Console.SetOut(sortie);
            }
            var passages = ModeMateriel.Sonde[n] - avant;
            var muette = demande && passages == 0;
            var (k, v, s) = parDomaine.GetValueOrDefault(c.Domaine);
            parDomaine[c.Domaine] = (k + 1, v + (bad == 0 && !muette ? 1 : 0), s + (passages > 0 ? 1 : 0));
            Console.WriteLine($"  PB-{n:00} ({c.Domaine}) : {(bad == 0 ? "vert" : $"{bad} échec(s)")}, la sonde {passages}" +
                              (muette ? " — muette" : ""));
            if (bad != 0)
            {
                fautes.Add($"PB-{n:00} : {bad} échec(s)");
                foreach (var l in capture.ToString().Split('\n').Where(l => l.Contains("[ECHEC]") || l.Contains("exception")))
                    Console.WriteLine($"      {l.Trim()}");
            }
            if (muette)
                fautes.Add($"PB-{n:00} : demandée, la sonde n'a pas servi");
        }

        Console.WriteLine("\npar domaine : corrections, cas verts, sondes qui ont servi");
        foreach (var (d, (k, v, s)) in parDomaine)
            Console.WriteLine($"  {d,-11} {k,4} {v,4} {s,4}");
        foreach (var f in fautes)
            Console.WriteLine($"  [ECHEC] {f}");
        Console.WriteLine(fautes.Count == 0
            ? $"\nVert : {table.Count} corrections, chacune son cas, tous verts sous le mode {ModeMateriel.Description}."
            : $"\n{fautes.Count} faute(s) dans la couverture du mode matériel.");
        return fautes.Count == 0 ? 0 : 1;
    }
}
