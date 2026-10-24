// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — outillage de la preuve des noms (G14.0c, PLAN-G14.md § G14.0c, décision n° 8 ; R2-noms.md § 1.7,
// R2-contre.md § 1.9).
//
// LE RENOMMAGE SÉMANTIQUE des types de la table des noms, par le compilateur lui-même (Roslyn du SDK). Il est lancé
// par renommer.sh, qui lui passe les entrées de compilation des projets (le JSON de `dotnet msbuild -getItem`) :
//
//   renommage --noms TABLE [--jusqua ETAPE] --arbre ARBRE --journal FICHIER --deplacements FICHIER J1 [J2…]
//
// J1 est le projet qui déclare les types (iXtal26), les suivants ceux qui le référencent (iXtal26.Diff,
// iXtal26.PerfBanc) : leurs références à iXtal26 sont remplacées par la compilation de J1, pour que chaque nom se
// lie au même symbole. Ce qui est touché, et rien d'autre :
//   - un nom (SimpleNameSyntax) que le modèle sémantique lie à un type de la table, et qui est écrit sous l'ancien
//     nom (`var`, qui se lie au type inféré, n'est pas touché) : son identifiant prend le
//     nouveau nom ; s'il est la partie droite d'un nom qualifié dont la gauche est un espace de noms
//     (`using static iXtal26.Models.pic;`, `iXtal26.Models.pit.Probe(…)`), tout le nom qualifié devient le nouveau ;
//   - la déclaration du type (chaque partie d'une classe partielle) et ses constructeurs ;
//   - la déclaration d'espace de noms du fichier, si tous ses types de premier niveau sont dans la table avec le
//     même nouvel espace ;
//   - une directive `using` ajoutée là où un nom simple le demande : un fichier qui nommait un type déplacé par son
//     espace de noms englobant reçoit `using <nouvel espace>;`, un fichier déplacé qui nommait un type resté dans
//     l'ancien espace reçoit `using <ancien espace>;`.
// Jamais un commentaire, jamais une chaîne : le parcours ne descend pas dans les trivia (les renvois `cref` sont
// comptés et dits, non touchés), et une chaîne n'est pas un nom. Un paramètre ou une locale homonyme (`PIT pit`) se
// lie à lui-même, pas au type : il n'est pas touché.
//
// Refusé (retour 2, rien n'est écrit) : un ANCIEN qui n'est pas un type de premier niveau de J1, un NOUVEAU qui
// existe, un nom lié à un type de la table de façon ambiguë, un fichier dont une partie seulement des types change
// d'espace, un fichier déplacé sans entrée de fichier, un type de la table déclaré hors des fichiers déplacés, une
// compilation de départ en erreur. Après les modifications, les projets sont recompilés en mémoire : une seule
// erreur, ou un avertissement de plus qu'avant, et rien n'est écrit (retour 1). Sinon les fichiers sont écrits à
// leur place ; renommer.sh les déplace ensuite par `git mv`, d'après le fichier des déplacements. Les lignes que le
// renommage porte au-delà de 120 caractères, ou allonge au-delà, sont comptées et inscrites au journal (« ligne de
// plus de 120 caractères allongée », avant et après), pour être recoupées à la main : la règle du dépôt les compte.

using System.Text;
using System.Text.Json;
using iXtal26.Noms;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace iXtal26.Renommage;

internal sealed class Projet
{
    public required string Nom { get; init; }
    public required CSharpParseOptions Analyse { get; init; }
    public required CSharpCompilationOptions Options { get; init; }
    public required List<MetadataReference> Refs { get; init; }
    public required List<string> AutresProjets { get; init; }
    public required List<SyntaxTree> Arbres { get; init; }
    public CSharpCompilation Comp { get; set; } = null!;
}

/// <summary>Une cible : un type de la table, ses anciens et nouveaux espace de noms et nom.</summary>
internal sealed record Cible(string Ancien, string AncienNs, string AncienNom, string NouveauNs, string NouveauNom)
{
    public string Nouveau => NouveauNs + "." + NouveauNom;
}

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            return Lancer(args);
        }
        catch (Refus r)
        {
            Console.Error.WriteLine($"renommage : refusé : {r.Message}");
            return 2;
        }
        catch (Exception e) when (e is FormatException or IOException or JsonException)
        {
            Console.Error.WriteLine($"renommage : {e.Message}");
            return 2;
        }
    }

    private sealed class Refus(string m) : Exception(m);

    /// <summary>Les fichiers qui commencent par la marque d'ordre des octets, réécrits avec elle.</summary>
    private static readonly HashSet<string> Bom = new(StringComparer.Ordinal);

    private static int Lancer(string[] args)
    {
        string? noms = null, jusqua = null, arbre = null, journal = null, deplacements = null;
        var jsons = new List<string>();
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--noms" when i + 1 < args.Length: noms = args[++i]; break;
                case "--jusqua" when i + 1 < args.Length: jusqua = args[++i]; break;
                case "--arbre" when i + 1 < args.Length: arbre = Path.GetFullPath(args[++i]); break;
                case "--journal" when i + 1 < args.Length: journal = args[++i]; break;
                case "--deplacements" when i + 1 < args.Length: deplacements = args[++i]; break;
                default: jsons.Add(args[i]); break;
            }
        }
        if (noms == null || arbre == null || journal == null || deplacements == null || jsons.Count == 0)
        {
            Console.Error.WriteLine("usage : renommage --noms TABLE [--jusqua ETAPE] --arbre ARBRE --journal F "
                                    + "--deplacements F J1 [J2…]");
            return 2;
        }
        var table = TableDesNoms.Lire(noms, jusqua);
        var projets = new List<Projet>();
        foreach (var j in jsons) projets.Add(Charger(j, projets));
        var avertissementsAvant = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var p in projets)
        {
            foreach (var d in p.Comp.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Warning))
                avertissementsAvant[$"{p.Nom} {d.Id}"] = avertissementsAvant.GetValueOrDefault($"{p.Nom} {d.Id}") + 1;
            var err = p.Comp.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
            if (err.Count > 0)
                throw new Refus($"la compilation de départ de {p.Nom} a {err.Count} erreurs, la première : {err[0]}");
        }
        Console.WriteLine($"  {projets.Count} projets, {projets.Sum(p => p.Arbres.Count)} fichiers ; la compilation "
                          + "de départ est sans erreur");

        var premier = projets[0].Comp;
        var cibles = new Dictionary<string, Cible>(StringComparer.Ordinal);
        foreach (var (ancien, nouveau) in table.Types)
        {
            var t = premier.GetTypeByMetadataName(ancien)
                    ?? throw new Refus($"« {ancien} » n'est pas un type de premier niveau de {projets[0].Nom}");
            if (t.ContainingType != null) throw new Refus($"« {ancien} » est un type imbriqué");
            if (projets.Any(p => p.Comp.GetTypeByMetadataName(nouveau) != null))
                throw new Refus($"« {nouveau} » existe déjà");
            var ip = nouveau.LastIndexOf('.');
            cibles[ancien] = new Cible(ancien, t.ContainingNamespace.ToDisplayString(), t.MetadataName,
                                       nouveau[..ip], nouveau[(ip + 1)..]);
        }
        var fichiers = table.Entrees.Where(e => e.EstFichier)
                            .ToDictionary(e => Path.GetFullPath(Path.Combine(arbre, e.Ancien)), e => e.Nouveau);

        var modifs = new Dictionary<SyntaxTree, List<TextChange>>();
        var usings = new Dictionary<SyntaxTree, SortedSet<string>>();
        var deplaces = new Dictionary<SyntaxTree, string>();
        var lignes = new List<string>();
        var comptes = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var crefs = 0;

        void Noter(SyntaxTree t, TextSpan s, string texte, string quoi, Cible c)
        {
            if (!modifs.TryGetValue(t, out var l)) modifs[t] = l = [];
            l.Add(new TextChange(s, texte));
            var pos = t.GetLineSpan(s).StartLinePosition;
            lignes.Add($"{Path.GetRelativePath(arbre, t.FilePath)}:{pos.Line + 1}:{pos.Character + 1}\t{quoi}\t"
                       + $"{c.Ancien}\t{t.GetText().ToString(s)}\t{texte}");
            var k = $"{c.Ancien}\t{quoi}";
            comptes[k] = comptes.GetValueOrDefault(k) + 1;
        }

        Cible? CibleDe(ISymbol? s)
        {
            if (s is IAliasSymbol a) s = a.Target;
            if (s is not INamedTypeSymbol n) return null;
            n = n.OriginalDefinition;
            if (n.ContainingType != null || n.ContainingAssembly?.Name != projets[0].Comp.AssemblyName) return null;
            var q = n.ContainingNamespace.IsGlobalNamespace ? n.MetadataName
                : n.ContainingNamespace.ToDisplayString() + "." + n.MetadataName;
            return cibles.GetValueOrDefault(q);
        }

        // Les fichiers déplacés : leurs types de premier niveau, leur espace de noms.
        foreach (var p in projets)
        {
            foreach (var t in p.Arbres)
            {
                var racine = (CompilationUnitSyntax)t.GetRoot();
                var m = p.Comp.GetSemanticModel(t);
                var decls = racine.DescendantNodes(n => n is CompilationUnitSyntax or BaseNamespaceDeclarationSyntax)
                                  .Where(n => n is BaseTypeDeclarationSyntax or DelegateDeclarationSyntax).ToList();
                var types = decls.Select(d => (d, c: CibleDe(m.GetDeclaredSymbol(d)))).ToList();
                if (!types.Any(x => x.c != null)) continue;
                var nss = racine.DescendantNodes(n => n is CompilationUnitSyntax or BaseNamespaceDeclarationSyntax)
                                .OfType<BaseNamespaceDeclarationSyntax>().ToList();
                var rel = Path.GetRelativePath(arbre, t.FilePath);
                if (types.Any(x => x.c == null))
                    throw new Refus($"{rel} : les types {string.Join(", ", types.Where(x => x.c == null).Select(x =>
                        m.GetDeclaredSymbol(x.d)!.Name))} n'ont pas d'entrée, et leur fichier change d'espace");
                var nouveaux = types.Select(x => x.c!.NouveauNs).Distinct().ToList();
                if (nouveaux.Count != 1 || nss.Count != 1 || nss[0] is not FileScopedNamespaceDeclarationSyntax fs)
                    throw new Refus($"{rel} : un seul espace de noms de fichier, et un seul nouvel espace, attendus");
                if (!fichiers.ContainsKey(t.FilePath))
                    throw new Refus($"{rel} déclare un type de la table et n'a pas d'entrée de fichier");
                if (fs.Name.ToString() != nouveaux[0])
                    Noter(t, fs.Name.Span, nouveaux[0], "espace", types[0].c!);
                deplaces[t] = nouveaux[0];
                foreach (var (d, c) in types)
                {
                    var id = d switch
                    {
                        BaseTypeDeclarationSyntax b => b.Identifier,
                        DelegateDeclarationSyntax dd => dd.Identifier,
                        _ => default,
                    };
                    if (id.Text != c!.NouveauNom) Noter(t, id.Span, c.NouveauNom, "déclaration", c);
                    if (d is not TypeDeclarationSyntax td || id.Text == c.NouveauNom) continue;
                    foreach (var k in td.Members.OfType<ConstructorDeclarationSyntax>())
                        Noter(t, k.Identifier.Span, c.NouveauNom, "constructeur", c);
                    foreach (var k in td.Members.OfType<DestructorDeclarationSyntax>())
                        Noter(t, k.Identifier.Span, c.NouveauNom, "destructeur", c);
                }
            }
        }
        foreach (var c in cibles.Values)
        {
            var t = premier.GetTypeByMetadataName(c.Ancien)!;
            foreach (var r in t.DeclaringSyntaxReferences)
                if (!fichiers.ContainsKey(r.SyntaxTree.FilePath))
                    throw new Refus($"{c.Ancien} est déclaré dans {r.SyntaxTree.FilePath}, sans entrée de fichier");
        }
        foreach (var f in fichiers.Keys)
            if (!deplaces.Keys.Any(t => t.FilePath == f))
                throw new Refus($"l'entrée de fichier {Path.GetRelativePath(arbre, f)} ne déclare aucun type "
                                + "de la table");

        // Les références.
        foreach (var p in projets)
        {
            foreach (var t in p.Arbres)
            {
                var m = p.Comp.GetSemanticModel(t);
                var racine = t.GetRoot();
                var nsFichier = racine.DescendantNodes(n => n is CompilationUnitSyntax)
                                      .OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault()?.Name.ToString();
                foreach (var cref in racine.DescendantNodes(descendIntoTrivia: true).OfType<CrefSyntax>())
                    if (CibleDe(m.GetSymbolInfo(cref).Symbol) != null) crefs++;
                foreach (var s in racine.DescendantNodes().OfType<SimpleNameSyntax>())
                {
                    var info = m.GetSymbolInfo(s);
                    var c = CibleDe(info.Symbol ?? m.GetAliasInfo(s));
                    if (c == null)
                    {
                        if (info.Symbol == null && info.CandidateSymbols.Any(x => CibleDe(x) != null))
                            throw new Refus($"{t.FilePath}:{t.GetLineSpan(s.Span).StartLinePosition.Line + 1} : "
                                            + $"« {s} » se lie à un type de la table de façon ambiguë");
                        if (deplaces.ContainsKey(t) && BesoinAncien(m, s, info.Symbol, out var ns))
                            Ajouter(usings, t, ns);
                        continue;
                    }
                    if (s.Parent is QualifiedNameSyntax q && q.Right == s && m.GetSymbolInfo(q.Left).Symbol
                        is INamespaceSymbol)
                    {
                        Noter(t, q.Span, Gauche(q.Left, c) + "." + c.NouveauNom,
                              s.Parent.Parent is UsingDirectiveSyntax ? "using" : "qualifié", c);
                        continue;
                    }
                    if (s.Parent is MemberAccessExpressionSyntax ma && ma.Name == s
                        && m.GetSymbolInfo(ma.Expression).Symbol is INamespaceSymbol)
                    {
                        Noter(t, ma.Span, Gauche(ma.Expression, c) + "." + c.NouveauNom, "qualifié", c);
                        continue;
                    }
                    // `var` (et tout nom écrit autrement que l'ancien) se lie au type inféré : il n'est pas un renvoi.
                    if (s.Identifier.Text != c.AncienNom)
                    {
                        comptes[$"{c.Ancien}\tinféré (var), non touché"] =
                            comptes.GetValueOrDefault($"{c.Ancien}\tinféré (var), non touché") + 1;
                        continue;
                    }
                    if (s.Identifier.Text != c.NouveauNom) Noter(t, s.Identifier.Span, c.NouveauNom, "simple", c);
                    var nsApres = deplaces.TryGetValue(t, out var nd) ? nd : nsFichier;
                    if (c.NouveauNs != c.AncienNs && nsApres != c.NouveauNs) Ajouter(usings, t, c.NouveauNs);
                }
            }
        }

        // Les modifications, puis la recompilation en mémoire, avant d'écrire quoi que ce soit.
        var textes = new Dictionary<SyntaxTree, SourceText>();
        var longues = 0;
        foreach (var p in projets)
        {
            foreach (var t in p.Arbres)
            {
                var src = t.GetText();
                var l = modifs.GetValueOrDefault(t) ?? [];
                l = l.OrderBy(x => x.Span.Start).ThenByDescending(x => x.Span.Length).ToList();
                var garde = new List<TextChange>();
                foreach (var x in l)
                {
                    if (garde.Count > 0 && garde[^1].Span.Contains(x.Span)) continue;
                    if (garde.Count > 0 && garde[^1].Span.OverlapsWith(x.Span))
                        throw new Refus($"{t.FilePath} : deux modifications se chevauchent");
                    garde.Add(x);
                }
                if (usings.TryGetValue(t, out var aAjouter))
                {
                    var cu = (CompilationUnitSyntax)t.GetRoot();
                    var deja = cu.Usings.Where(u => u.StaticKeyword.IsKind(SyntaxKind.None) && u.Alias == null)
                                 .Select(u => u.Name!.ToString()).ToHashSet();
                    var nouveaux = aAjouter.Where(n => !deja.Contains(n)).ToList();
                    if (nouveaux.Count > 0)
                    {
                        var texte = string.Concat(nouveaux.Select(n => $"using {n};\n"));
                        int ou;
                        if (cu.Usings.Count > 0) ou = cu.Usings[^1].FullSpan.End;
                        else
                        {
                            ou = cu.Members[0].SpanStart;
                            texte += "\n";
                        }
                        garde.Add(new TextChange(new TextSpan(ou, 0), texte));
                        foreach (var n in nouveaux)
                        {
                            lignes.Add($"{Path.GetRelativePath(arbre, t.FilePath)}\tusing ajouté\t{n}");
                            comptes[$"(using ajouté)\t{n}"] = comptes.GetValueOrDefault($"(using ajouté)\t{n}") + 1;
                        }
                    }
                }
                if (garde.Count == 0) continue;
                if (t.FilePath.Contains("/obj/", StringComparison.Ordinal)
                    || t.FilePath.Contains("/bin/", StringComparison.Ordinal))
                    throw new Refus($"{t.FilePath} : un fichier généré serait modifié");
                var nouveau = src.WithChanges(garde.OrderBy(x => x.Span.Start));
                textes[t] = nouveau;
                // Les lignes que le renommage porte au-delà de 120 caractères, ou allonge alors qu'elles y étaient
                // déjà (808x.cs) : comptées en caractères (points de code), dites, non refusées.
                var decalage = 0;
                var vues = new HashSet<int>();
                foreach (var x in garde.OrderBy(x => x.Span.Start))
                {
                    var ln = nouveau.Lines.GetLineFromPosition(x.Span.Start + decalage);
                    decalage += x.NewText!.Length - x.Span.Length;
                    if (!vues.Add(ln.LineNumber)) continue;
                    var lgApres = nouveau.ToString(ln.Span).EnumerateRunes().Count();
                    var lgAvant = src.ToString(src.Lines.GetLineFromPosition(x.Span.Start).Span)
                                     .EnumerateRunes().Count();
                    if (lgApres <= 120 || lgApres <= lgAvant) continue;
                    longues++;
                    lignes.Add($"{Path.GetRelativePath(arbre, t.FilePath)}:{ln.LineNumber + 1}\tligne de plus de 120 "
                               + $"caractères allongée\t{lgAvant}\t{lgApres}");
                }
            }
        }
        var avant = new List<CSharpCompilation>();
        var erreurs = 0;
        foreach (var p in projets)
        {
            var arbres = p.Arbres.Select(t => textes.TryGetValue(t, out var s)
                ? CSharpSyntaxTree.ParseText(s, p.Analyse, t.FilePath) : t).ToList();
            var refs = p.Refs.Concat(p.AutresProjets.Select(n => avant.First(c => c.AssemblyName == n)
                .ToMetadataReference())).ToList();
            var comp = CSharpCompilation.Create(p.Nom, arbres, refs, p.Options);
            avant.Add(comp);
            foreach (var d in comp.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error))
            {
                if (erreurs++ < 20) Console.Error.WriteLine($"  erreur après renommage : {d}");
            }
            // Un avertissement de plus est un changement (le projet les traite en erreurs) : refusé de même.
            var apres = comp.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Warning)
                            .GroupBy(d => $"{p.Nom} {d.Id}").ToList();
            foreach (var g in apres.Where(g => g.Count() > avertissementsAvant.GetValueOrDefault(g.Key)))
            {
                erreurs++;
                Console.Error.WriteLine($"  avertissement neuf après renommage : {g.First()}");
            }
        }
        File.WriteAllLines(journal, lignes.OrderBy(x => x, StringComparer.Ordinal));
        foreach (var (k, n) in comptes) Console.WriteLine($"  {n,5}  {k.Replace('\t', ' ')}");
        Console.WriteLine($"  {textes.Count} fichiers modifiés, {lignes.Count - longues} sites ; renvois cref vers la "
                          + $"table (non touchés) : {crefs}");
        Console.WriteLine($"  lignes de plus de 120 caractères que le renommage allonge : {longues} (au journal)");
        if (erreurs > 0)
        {
            Console.Error.WriteLine($"renommage : {erreurs} erreurs après renommage ; rien n'est écrit");
            return 1;
        }
        Console.WriteLine("  la recompilation en mémoire est sans erreur");
        foreach (var (t, s) in textes)
        {
            using var w = new StreamWriter(t.FilePath, false, new UTF8Encoding(Bom.Contains(t.FilePath)));
            s.Write(w);
        }
        File.WriteAllLines(deplacements, fichiers.Select(f => $"{Path.GetRelativePath(arbre, f.Key)}\t{f.Value}"));
        return 0;
    }

    /// <summary>
    /// La partie gauche d'un nom qualifié, l'espace de noms remplacé, dans la forme où elle était écrite :
    /// `iXtal26.Models` devient `iXtal26.Ics`, `Models` (relatif à l'espace englobant iXtal26) devient `Ics`,
    /// `global::` reste. Une forme qu'on ne sait pas récrire relativement prend le nom complet.
    /// </summary>
    private static string Gauche(SyntaxNode gauche, Cible c)
    {
        var g = Global(gauche);
        var ecrit = gauche.ToString();
        if (g.Length > 0) ecrit = ecrit[(ecrit.IndexOf("::", StringComparison.Ordinal) + 2)..];
        ecrit = string.Concat(ecrit.Where(ch => !char.IsWhiteSpace(ch)));
        if (ecrit == c.AncienNs) return g + c.NouveauNs;
        if (g.Length == 0 && c.AncienNs.EndsWith("." + ecrit, StringComparison.Ordinal))
        {
            var prefixe = c.AncienNs[..^ecrit.Length];
            if (c.NouveauNs.StartsWith(prefixe, StringComparison.Ordinal)) return c.NouveauNs[prefixe.Length..];
        }
        return g + c.NouveauNs;
    }

    private static string Global(SyntaxNode gauche)
    {
        var n = gauche;
        while (true)
        {
            switch (n)
            {
                case QualifiedNameSyntax q: n = q.Left; continue;
                case MemberAccessExpressionSyntax m: n = m.Expression; continue;
                case AliasQualifiedNameSyntax a when a.Alias.Identifier.IsKind(SyntaxKind.GlobalKeyword):
                    return "global::";
                default: return "";
            }
        }
    }

    private static void Ajouter(Dictionary<SyntaxTree, SortedSet<string>> u, SyntaxTree t, string ns)
    {
        if (!u.TryGetValue(t, out var s)) u[t] = s = new SortedSet<string>(StringComparer.Ordinal);
        s.Add(ns);
    }

    /// <summary>
    /// Un fichier déplacé qui nomme par un nom simple un type (ou une méthode d'extension) de son ancien espace de
    /// noms, resté : il l'atteignait par l'espace englobant, il lui faut la directive.
    /// </summary>
    private static bool BesoinAncien(SemanticModel m, SimpleNameSyntax s, ISymbol? sym, out string ns)
    {
        ns = "";
        if (s.Parent is QualifiedNameSyntax q && q.Right == s) return false;
        if (s.Parent is MemberAccessExpressionSyntax ma && ma.Name == s
            && m.GetSymbolInfo(ma.Expression).Symbol is INamespaceSymbol) return false;
        if (s.Parent is MemberAccessExpressionSyntax mb && mb.Name == s && sym is not IMethodSymbol) return false;
        var nsFichier = s.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault();
        if (nsFichier == null || m.GetDeclaredSymbol(nsFichier) is not INamespaceSymbol ancien) return false;
        ISymbol? porteur = sym switch
        {
            INamedTypeSymbol n => n.ContainingType == null ? n : null,
            IMethodSymbol { ReducedFrom: not null } e => e.ReducedFrom.ContainingType,
            _ => null,
        };
        if (porteur == null) return false;
        if (!SymbolEqualityComparer.Default.Equals(porteur.ContainingNamespace, ancien)) return false;
        ns = ancien.ToDisplayString();
        return true;
    }

    /// <summary>Un projet, d'après le JSON de `dotnet msbuild -getItem:Compile -getItem:ReferencePath…`.</summary>
    private static Projet Charger(string json, List<Projet> avant)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(json));
        var props = doc.RootElement.GetProperty("Properties");
        string P(string n) => props.TryGetProperty(n, out var v) ? v.GetString() ?? "" : "";
        var items = doc.RootElement.GetProperty("Items");
        var nom = P("AssemblyName");
        if (!LanguageVersionFacts.TryParse(P("LangVersion"), out var lv))
            throw new FormatException($"{json} : LangVersion « {P("LangVersion")} » illisible");
        var analyse = new CSharpParseOptions(lv, DocumentationMode.Parse, SourceCodeKind.Regular,
            P("DefineConstants").Split(';', StringSplitOptions.RemoveEmptyEntries));
        var options = new CSharpCompilationOptions(
            P("OutputType") == "Exe" ? OutputKind.ConsoleApplication : OutputKind.DynamicallyLinkedLibrary,
            nullableContextOptions: P("Nullable") == "enable" ? NullableContextOptions.Enable
                : NullableContextOptions.Disable,
            allowUnsafe: P("AllowUnsafeBlocks") == "true",
            checkOverflow: P("CheckForOverflowUnderflow") == "true",
            optimizationLevel: OptimizationLevel.Release);
        var refs = new List<MetadataReference>();
        var autres = new List<string>();
        foreach (var r in items.GetProperty("ReferencePathWithRefAssemblies").EnumerateArray())
        {
            var chemin = r.GetProperty("FullPath").GetString()!;
            var n = Path.GetFileNameWithoutExtension(chemin);
            if (avant.Any(p => p.Nom == n)) autres.Add(n);
            else refs.Add(MetadataReference.CreateFromFile(chemin));
        }
        var arbres = new List<SyntaxTree>();
        foreach (var c in items.GetProperty("Compile").EnumerateArray())
        {
            var chemin = c.GetProperty("FullPath").GetString()!;
            var octets = File.ReadAllBytes(chemin);
            if (octets.Length >= 3 && octets[0] == 0xEF && octets[1] == 0xBB && octets[2] == 0xBF) Bom.Add(chemin);
            arbres.Add(CSharpSyntaxTree.ParseText(SourceText.From(octets, octets.Length, Encoding.UTF8), analyse,
                                                  chemin));
        }
        var p = new Projet
        {
            Nom = nom, Analyse = analyse, Options = options, Refs = refs, AutresProjets = autres, Arbres = arbres,
        };
        p.Comp = CSharpCompilation.Create(nom, arbres, refs.Concat(autres.Select(n =>
            avant.First(x => x.Nom == n).Comp.ToMetadataReference())), options);
        return p;
    }
}
