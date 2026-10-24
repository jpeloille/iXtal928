// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — outillage de la preuve des noms (G14.0c, PLAN-G14.md § G14.0c).
//
// La lecture de la table des noms (tools/noms/g14.txt, dont l'en-tête dit la grammaire), partagée par tools/ildump et
// tools/renommage, qui l'incluent par <Compile Include>. tools/listings-jit.sh en a la même lecture, en python.

using System.Text.RegularExpressions;

namespace iXtal26.Noms;

/// <summary>Une entrée : un type de premier niveau, ou un fichier, avec son ancien et son nouveau nom.</summary>
internal sealed record EntreeNom(string Ancien, string Nouveau, bool EstFichier, string Etape, int Ligne);

internal sealed class TableDesNoms
{
    private static readonly Regex Etape = new(@"^//\s+étape\s+(\S+)", RegexOptions.CultureInvariant);
    private static readonly Regex Entree = new(@"^//   (iXtal26\S*)(?:\s+(\S+))?", RegexOptions.CultureInvariant);
    private static readonly Regex TypeQualifie = new(@"^iXtal26(\.[A-Za-z_][A-Za-z0-9_]*)+$",
        RegexOptions.CultureInvariant);
    private static readonly Regex Chemin = new(@"^iXtal26/[A-Za-z0-9_./-]+\.cs$", RegexOptions.CultureInvariant);

    public List<EntreeNom> Entrees { get; } = [];
    public List<string> Etapes { get; } = [];

    /// <summary>Les types renommés, ancien → nouveau.</summary>
    public Dictionary<string, string> Types { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Lit la table, et ne garde que les étapes jusqu'à <paramref name="jusqua"/> comprise (toutes si null). Toute
    /// incohérence lève une exception : la table est refusée, jamais lue à moitié.
    /// </summary>
    public static TableDesNoms Lire(string chemin, string? jusqua)
    {
        var t = new TableDesNoms();
        var etape = "";
        var vue = jusqua == null;
        var arret = false;
        var n = 0;
        foreach (var brute in File.ReadLines(chemin))
        {
            n++;
            var l = brute.TrimEnd();
            var e = Etape.Match(l);
            if (e.Success)
            {
                if (arret) break;
                etape = e.Groups[1].Value;
                t.Etapes.Add(etape);
                if (etape == jusqua) { vue = true; arret = true; }
                continue;
            }
            var m = Entree.Match(l);
            if (!m.Success) continue;
            var ancien = m.Groups[1].Value;
            var nouveau = m.Groups[2].Success ? m.Groups[2].Value : "";
            var fichier = Chemin.IsMatch(ancien) && Chemin.IsMatch(nouveau);
            var type = TypeQualifie.IsMatch(ancien) && TypeQualifie.IsMatch(nouveau);
            if (!fichier && !type)
                throw new FormatException($"{chemin}:{n} : entrée invalide (« {ancien} » → « {nouveau} ») : il faut "
                                          + "deux noms de type qualifiés, ou deux chemins iXtal26/….cs");
            if (etape == "")
                throw new FormatException($"{chemin}:{n} : entrée hors de toute étape (« // étape X » manque)");
            if (ancien == nouveau)
                throw new FormatException($"{chemin}:{n} : « {ancien} » ne change pas de nom");
            t.Entrees.Add(new EntreeNom(ancien, nouveau, fichier, etape, n));
        }
        if (jusqua != null && !vue)
            throw new FormatException($"{chemin} : l'étape « {jusqua} » n'existe pas (étapes : "
                                      + string.Join(", ", t.Etapes) + ")");
        var anciens = new HashSet<string>(StringComparer.Ordinal);
        var nouveaux = new HashSet<string>(StringComparer.Ordinal);
        foreach (var x in t.Entrees)
        {
            if (!anciens.Add(x.Ancien))
                throw new FormatException($"{chemin}:{x.Ligne} : « {x.Ancien} » est déjà l'ancien d'une entrée");
            if (!nouveaux.Add(x.Nouveau))
                throw new FormatException($"{chemin}:{x.Ligne} : « {x.Nouveau} » est déjà le nouveau d'une entrée");
        }
        foreach (var x in t.Entrees)
            if (anciens.Contains(x.Nouveau))
                throw new FormatException($"{chemin}:{x.Ligne} : chaîne de noms : « {x.Nouveau} » est aussi un ancien");
        foreach (var x in t.Entrees.Where(x => !x.EstFichier))
            t.Types[x.Ancien] = x.Nouveau;
        if (t.Entrees.Count == 0)
            throw new FormatException($"{chemin} : aucune entrée retenue");
        return t;
    }

    /// <summary>Le nom d'un type de premier niveau après la table (inchangé s'il n'y est pas).</summary>
    public string Appliquer(string nomQualifie) =>
        Types.TryGetValue(nomQualifie, out var n) ? n : nomQualifie;
}
