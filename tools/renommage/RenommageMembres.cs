// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — outillage de la preuve des membres (G15.0b4, PLAN-G15.md § G15.0b ; R0-noms.md § 1.5).
//
// LE RENOMMAGE DES MEMBRES, par les symboles que Roslyn lie, et la VÉRIFICATION DE LIAISON.
//
// Pour une étape de la table (une seule : ses entrées nomment les choses sous les noms qu'elles ont au début de
// l'étape, que l'arbre porte), ce qui est touché, et rien d'autre :
//   - la déclaration d'un membre de la table (méthode, et chacune de ses surcharges ; champ ; constante ; propriété ;
//     événement ; membre d'enum), d'un type imbriqué de la table (et ses constructeurs), d'un paramètre de la table ;
//     les deux parties d'une méthode partielle ;
//   - chaque nom (SimpleNameSyntax, et le nom d'un argument nommé) que le modèle sémantique lie à l'un d'eux, écrit
//     sous l'ancien nom ; une méthode d'extension réduite se lie à sa définition ;
//   - chaque chaîne littérale simple de la liste close, égale à l'ancien littéral : écrite "…" telle quelle, sans
//     échappement, ni @, ni interpolation, ni chaîne brute, ni u8 ;
//   - un nameof d'une méthode surchargée (un groupe de méthodes), si toutes ses surcharges prennent le même nom.
// Jamais un commentaire (un renvoi cref vers un membre de la table est compté et dit, non touché), jamais une autre
// chaîne.
//
// LA VÉRIFICATION DE LIAISON (R0 § 1.5 : un champ renommé vers le nom d'une locale qui le masque, deux surcharges
// importées par deux `using static`, changeraient le sens sans erreur de compilation). Avant les modifications, chaque
// nom de chaque projet (hors trivia et hors code d'un #if inactif) est relevé avec le symbole auquel il se lie : un
// symbole des sources par l'emplacement de sa déclaration (fichier, début), un espace de noms par son nom, un symbole
// de métadonnées par son identifiant de documentation. Après, dans la compilation neuve, chaque nom, à sa position
// reportée à travers les modifications, doit se lier au même symbole. Un écart, même un seul, et rien n'est écrit. Un
// nom au début d'une modification plus longue que lui (un nom qualifié que le mode de G14 remplace d'un bloc,
// `Models.pic` → `Ics.Intel8259A`) n'est plus un nom à lui seul : il est compté à part, non vérifié par position.
// Ce que la liaison ne voit pas, parce qu'aucun nom ne le porte : les membres qu'une construction appelle par le
// compilateur (GetEnumerator et Current d'un foreach, Dispose d'un using, Add d'un initialiseur, Deconstruct,
// GetAwaiter, les opérateurs), et la correspondance implicite d'une implémentation d'interface ; et la valeur d'un
// nameof, qui suit le nom renommé. L'IL (ildump) les juge.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using iXtal26.Noms;

namespace iXtal26.Renommage;

internal static class RenommageMembres
{
    /// <summary>Le symbole qui fait foi : la définition, la partie déclarative d'une méthode partielle.</summary>
    internal static ISymbol? Normal(ISymbol? s)
    {
        if (s is IAliasSymbol a) s = a.Target;
        if (s is IMethodSymbol { ReducedFrom: { } rf }) s = rf;
        if (s is IMethodSymbol { PartialDefinitionPart: { } def }) s = def;
        return s?.OriginalDefinition;
    }

    /// <summary>
    /// La clé d'un symbole, stable d'une compilation à l'autre (un membre d'iXtal26 vu depuis Diff, par la référence de
    /// compilation, est un autre objet) : son identifiant de documentation ; pour un paramètre, celui de sa méthode et
    /// son nom.
    /// </summary>
    internal static string Cle(ISymbol s) => s is IParameterSymbol p
        ? Cle(Normal(p.ContainingSymbol)!) + "#" + p.Name
        : s.GetDocumentationCommentId() ?? s.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

    /// <summary>
    /// Les cibles de l'étape : chaque symbole de la table (membres de toutes leurs surcharges, types imbriqués,
    /// paramètres), avec son nouveau nom. Un membre, un type ou un paramètre absent est refusé.
    /// </summary>
    internal static Dictionary<string, string> Cibles(IReadOnlyList<Projet> projets, TableDesNoms table,
                                                      Action<string> refuser)
    {
        var res = new Dictionary<string, string>(StringComparer.Ordinal);
        INamedTypeSymbol? Type(string nom)
        {
            foreach (var p in projets)
                if (p.Comp.GetTypeByMetadataName(nom) is { } t && t.DeclaringSyntaxReferences.Length > 0) return t;
            return null;
        }
        foreach (var x in table.Entrees)
        {
            switch (x.Genre)
            {
                case GenreNom.Imbrique:
                {
                    var t = Type(x.Ancien);
                    if (t == null) refuser($"{x.Ou} : le type imbriqué « {x.Ancien} » n'existe pas");
                    else res[Cle(t.OriginalDefinition)] = x.Nouveau;
                    break;
                }
                case GenreNom.Membre:
                {
                    var t = Type(x.Type);
                    var ms = t?.GetMembers(x.Ancien).Where(m => !m.IsImplicitlyDeclared).ToList() ?? [];
                    if (ms.Count == 0) refuser($"{x.Ou} : le membre « {x.Type}::{x.Ancien} » n'existe pas");
                    foreach (var m in ms) res[Cle(Normal(m)!)] = x.Nouveau;
                    break;
                }
                case GenreNom.Parametre:
                {
                    var t = Type(x.Type);
                    var ps = (t?.GetMembers(x.Methode).OfType<IMethodSymbol>() ?? [])
                             .SelectMany(m => m.Parameters.Where(p => p.Name == x.Ancien)).ToList();
                    if (ps.Count == 0)
                        refuser($"{x.Ou} : aucune méthode « {x.Type}::{x.Methode} » n'a de paramètre « {x.Ancien} »");
                    foreach (var p in ps) res[Cle(p.OriginalDefinition)] = x.Nouveau;
                    break;
                }
            }
        }
        return res;
    }

    /// <summary>
    /// Les sites : les déclarations, les noms qui s'y lient, les littéraux de la liste close. noter(arbre, étendue,
    /// texte neuf, sorte, clé) les inscrit ; un nom lié de façon ambiguë à une cible est refusé.
    /// </summary>
    internal static int Sites(IReadOnlyList<Projet> projets, TableDesNoms table, Dictionary<string, string> cibles,
                              Action<SyntaxTree, TextSpan, string, string, string> noter, Action<string> refuser)
    {
        var crefs = 0;
        string Dit(ISymbol s) => s.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
        foreach (var p in projets)
        {
            foreach (var t in p.Arbres)
            {
                var m = p.Comp.GetSemanticModel(t);
                var racine = t.GetRoot();
                foreach (var n in racine.DescendantNodes())
                {
                    SyntaxToken id = default;
                    ISymbol? decl = null;
                    switch (n)
                    {
                        case MethodDeclarationSyntax md: id = md.Identifier; decl = m.GetDeclaredSymbol(md); break;
                        case PropertyDeclarationSyntax pd: id = pd.Identifier; decl = m.GetDeclaredSymbol(pd); break;
                        case EventDeclarationSyntax ed: id = ed.Identifier; decl = m.GetDeclaredSymbol(ed); break;
                        case EnumMemberDeclarationSyntax em: id = em.Identifier; decl = m.GetDeclaredSymbol(em); break;
                        case VariableDeclaratorSyntax vd when vd.Parent?.Parent is BaseFieldDeclarationSyntax:
                            id = vd.Identifier; decl = m.GetDeclaredSymbol(vd); break;
                        case ParameterSyntax ps: id = ps.Identifier; decl = m.GetDeclaredSymbol(ps); break;
                        case BaseTypeDeclarationSyntax bt: id = bt.Identifier; decl = m.GetDeclaredSymbol(bt); break;
                        case DelegateDeclarationSyntax dd: id = dd.Identifier; decl = m.GetDeclaredSymbol(dd); break;
                        case ConstructorDeclarationSyntax cd:
                            id = cd.Identifier; decl = m.GetDeclaredSymbol(cd)?.ContainingType; break;
                        case DestructorDeclarationSyntax de:
                            id = de.Identifier; decl = m.GetDeclaredSymbol(de)?.ContainingType; break;
                        case LiteralExpressionSyntax le when le.IsKind(SyntaxKind.StringLiteralExpression)
                                                             && le.Token.Text.StartsWith('"')
                                                             && table.Litteraux.TryGetValue(le.Token.ValueText,
                                                                 out var nl)
                                                             && le.Token.Text == "\"" + le.Token.ValueText + "\"":
                            noter(t, le.Token.Span, "\"" + nl + "\"", "littéral", "\"" + le.Token.ValueText + "\"");
                            continue;
                    }
                    if (decl == null) continue;
                    var nd = Normal(decl);
                    if (nd != null && cibles.TryGetValue(Cle(nd), out var nv) && id.Text != nv)
                        noter(t, id.Span, nv, n is ConstructorDeclarationSyntax or DestructorDeclarationSyntax
                            ? "constructeur" : "déclaration", Dit(nd));
                }
                foreach (var cref in racine.DescendantNodes(descendIntoTrivia: true).OfType<CrefSyntax>())
                    if (Normal(m.GetSymbolInfo(cref).Symbol) is { } cs && cibles.ContainsKey(Cle(cs))) crefs++;
                foreach (var s in racine.DescendantNodes().OfType<SimpleNameSyntax>())
                {
                    var info = m.GetSymbolInfo(s);
                    var sym = Normal(info.Symbol ?? m.GetAliasInfo(s));
                    // nameof(M) d'une méthode surchargée se lie à un groupe de méthodes : il suit si toutes les
                    // surcharges du groupe prennent le même nouveau nom.
                    if (sym == null && info.CandidateReason == CandidateReason.MemberGroup
                        && info.CandidateSymbols.Length > 0)
                    {
                        var nvs = info.CandidateSymbols.Select(c => Normal(c) is { } nc
                            ? cibles.GetValueOrDefault(Cle(nc)) : null).Distinct().ToList();
                        if (nvs.Count == 1 && nvs[0] is { } ng && s.Identifier.Text != ng)
                        {
                            noter(t, s.Identifier.Span, ng, "nom (groupe de méthodes)", Dit(info.CandidateSymbols[0]));
                            continue;
                        }
                    }
                    if (sym == null)
                    {
                        if (info.CandidateSymbols.Any(c => Normal(c) is { } nc && cibles.ContainsKey(Cle(nc))))
                            refuser($"{t.FilePath}:{t.GetLineSpan(s.Span).StartLinePosition.Line + 1} : « {s} » se lie "
                                    + "à une cible de la table de façon ambiguë");
                        continue;
                    }
                    if (!cibles.TryGetValue(Cle(sym), out var nv)) continue;
                    if (s.Identifier.Text != sym.Name || s.Identifier.Text == nv) continue;
                    noter(t, s.Identifier.Span, nv, "nom", Dit(sym));
                }
            }
        }
        return crefs;
    }

    /// <summary>La référence d'un symbole pour la liaison : sa déclaration (fichier, début), ou son nom.</summary>
    private static (string Fichier, int Debut, string Nom)? Ref(ISymbol? s)
    {
        s = Normal(s);
        if (s == null) return null;
        // Un espace de noms se déclare dans bien des fichiers, et son premier emplacement change avec eux : son nom
        // fait foi.
        if (s is INamespaceSymbol) return ("", -1, "Namespace " + s.ToDisplayString());
        var loc = s.Locations.FirstOrDefault(l => l.IsInSource);
        if (loc != null) return (loc.SourceTree!.FilePath, loc.SourceSpan.Start, s.Kind.ToString());
        // Un symbole de métadonnées par son identifiant de documentation (les paramètres d'une surcharge compris).
        return ("", -1, s.Kind + " " + (s.GetDocumentationCommentId()
                                        ?? s.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
    }

    /// <summary>Les liaisons d'avant : chaque nom de chaque arbre, par (fichier, début du nom).</summary>
    internal static Dictionary<(string, int, int), (string Fichier, int Debut, string Nom)?> Relever(
        IReadOnlyList<Projet> projets)
    {
        var res = new Dictionary<(string, int, int), (string, int, string)?>();
        foreach (var p in projets)
            foreach (var t in p.Arbres)
            {
                var m = p.Comp.GetSemanticModel(t);
                foreach (var s in t.GetRoot().DescendantNodes().OfType<SimpleNameSyntax>())
                {
                    var info = m.GetSymbolInfo(s);
                    res[(t.FilePath, s.Identifier.SpanStart, s.Identifier.Span.Length)] =
                        Ref(info.Symbol ?? m.GetAliasInfo(s));
                }
            }
        return res;
    }

    /// <summary>
    /// Une position d'avant reportée à travers les modifications d'un fichier (triées, sans chevauchement) ; -1 si elle
    /// tombe à l'intérieur d'une modification (un nom qualifié remplacé d'un bloc, en G14).
    /// </summary>
    private static int Reporter(IReadOnlyList<TextChange>? modifs, int pos, int longueur = 0)
    {
        if (modifs == null) return pos;
        var d = 0;
        foreach (var c in modifs)
        {
            if (c.Span.Start > pos) break;
            // Une modification qui commence au nom et le dépasse (un nom qualifié remplacé d'un bloc, en G14) : le nom
            // n'est plus un nom à lui seul.
            if (c.Span.Start == pos && longueur > 0 && c.Span.Length > longueur) return -1;
            if (c.Span.Start == pos && c.Span.Length > 0) return pos + d;
            if (c.Span.End <= pos) d += c.NewText!.Length - c.Span.Length;
            else return -1;
        }
        return pos + d;
    }

    /// <summary>
    /// La vérification de liaison : chaque nom relevé avant, à sa position reportée dans la compilation neuve, se lie
    /// au même symbole (sa déclaration reportée, ou le même symbole de métadonnées). Rend la liste des écarts.
    /// </summary>
    internal static List<string> Verifier(
        Dictionary<(string, int, int), (string Fichier, int Debut, string Nom)?> avant,
        IReadOnlyDictionary<string, List<TextChange>> modifs, IReadOnlyList<CSharpCompilation> neuves)
    {
        var ecarts = new List<string>();
        var dedans = 0;
        var absents = 0;
        var arbres = new Dictionary<string, (SyntaxTree T, CSharpCompilation C)>(StringComparer.Ordinal);
        foreach (var c in neuves)
            foreach (var t in c.SyntaxTrees)
                arbres.TryAdd(t.FilePath, (t, c));
        var modeles = new Dictionary<SyntaxTree, SemanticModel>();
        foreach (var ((fichier, debut, longueur), attendu) in avant)
        {
            if (!arbres.TryGetValue(fichier, out var tc)) { absents++; continue; }
            var pos = Reporter(modifs.GetValueOrDefault(fichier), debut, longueur);
            if (pos < 0) { dedans++; continue; }
            var jeton = tc.T.GetRoot().FindToken(pos);
            if (jeton.SpanStart != pos || jeton.Parent is not SimpleNameSyntax s)
            {
                ecarts.Add($"{fichier}:{debut} : aucun nom à la position reportée {pos}");
                continue;
            }
            if (!modeles.TryGetValue(tc.T, out var m)) modeles[tc.T] = m = tc.C.GetSemanticModel(tc.T);
            var info = m.GetSymbolInfo(s);
            var vu = Ref(info.Symbol ?? m.GetAliasInfo(s));
            (string, int, string)? voulu = attendu;
            if (attendu is { } a && a.Debut >= 0)
            {
                var d = Reporter(modifs.GetValueOrDefault(a.Fichier), a.Debut);
                if (d < 0) { dedans++; continue; }
                voulu = (a.Fichier, d, a.Nom);
            }
            if (!Equals(vu, voulu))
            {
                var ligne = tc.T.GetLineSpan(s.Span).StartLinePosition.Line + 1;
                ecarts.Add($"{fichier}:{ligne} : « {s} » se liait à {Dire(voulu)}, se lie à {Dire(vu)}");
            }
        }
        Console.WriteLine($"  liaison : {avant.Count} noms relevés, {avant.Count - dedans - absents - ecarts.Count} "
                          + $"liés au même symbole, {ecarts.Count} écarts ; {dedans} dans un nom qualifié "
                          + $"remplacé d'un bloc (non vérifiés par position), {absents} dans un fichier absent de "
                          + "la compilation neuve");
        return ecarts;
    }

    private static string Dire((string Fichier, int Debut, string Nom)? r) => r is not { } x ? "(rien)"
        : x.Debut >= 0 ? $"{x.Nom} déclaré en {Path.GetFileName(x.Fichier)}@{x.Debut}" : x.Nom;
}
