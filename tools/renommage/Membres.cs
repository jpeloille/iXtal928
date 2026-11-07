// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — outillage de la preuve des membres (G15.0b, PLAN-G15.md § G15.0b ; Q40).
//
// LE GÉNÉRATEUR DE LA TABLE DES MEMBRES, et sa VÉRIFICATION, sur les symboles que Roslyn lie (les mêmes projets que le
// renommage : renommer.sh les charge).
//
//   renommage generer --famille PRÉFIXE [--famille PRÉFIXE…] --exceptions FICHIER --etape ETAPE --sortie FICHIER
//                     J1 [J2…]
//   renommage verifier --noms TABLE [--noms TABLE…] [--depuis ETAPE] [--jusqua ETAPE] J1 [J2…]
//
// GENERER écrit, pour une famille (les fichiers dont le chemin relatif au dépôt commence par l'un des PRÉFIXES), les
// entrées de l'étape ETAPE, triées par clé : chaque type déclaré dans la famille, imbriqués compris, et chacun de ses
// membres, prend le nom que la règle de Q40 (tools/noms/RegleNoms.cs) lui donne, ou celui que les exceptions écrivent :
//   - PascalCase : les types, les méthodes (un nom pour toutes les surcharges), les propriétés, les événements, les
//     constantes, les membres d'enum, les champs qui ne sont pas privés ;
//   - camelCase, sans préfixe « _ » : les champs privés, sauf un `private static readonly` déjà en PascalCase (que la
//     règle laisse tel quel), qui le reste (Q40) ;
//   - un type dont le nom finit par « _t » le perd quand rien ne collisionne (pic_t → Pic), le garde sinon (PicT) :
//     ses voisins sont les types de son espace de noms (tous, de la famille ou non), ou, pour un type imbriqué, les
//     autres types imbriqués et les membres de son conteneur, sous les noms que la règle leur donne ;
//   - un `override`, ou l'implémentation implicite d'un membre d'interface du MÊME assemblage, suit le membre qu'il
//     redéfinit : le nom de celui-ci s'il est dans la famille, le sien sinon (sans entrée) ;
//   - restent tels quels, sans entrée : les constructeurs, les accesseurs, les opérateurs et les conversions, les
//     destructeurs, les implémentations explicites d'interface, l'override ou l'implémentation d'un membre d'un autre
//     assemblage (ToString, Dispose), les méthodes `extern` (les P/Invoke, PLAN-G15, décision n° 3), et tout ce que le
//     compilateur déclare ;
//   - les paramètres et les locales ne sont pas générés (G15.9).
// Une classe partielle ne se renomme qu'entière : un type dont une partie seulement est dans la famille est refusé.
// LES EXCEPTIONS (FICHIER, la grammaire de la table, plus) : « //   CLÉ  NOUVEAU  raison » remplace la règle pour CLÉ
// (un type, Type+Imbriqué, ou Type::membre, sous l'ancien nom ; pour un type, NOUVEAU est son nom simple, « `N »
// compris s'il est générique) ; « //   CLÉ  =  raison » garde le nom ; une CLÉ de membre qui finit par « * » vaut pour
// tous les membres du type dont le nom commence ainsi (materiel::pb_*, Q43). La raison est exigée ; une exception qui
// ne désigne rien de la famille est refusée, pour qu'aucune ne traîne.
// SONT REFUSÉS (retour 2, rien n'est écrit, chaque refus dit) : deux noms d'une même portée qui tombent sur le même
// (les membres et les types imbriqués d'un type ; les types de premier niveau d'un espace de noms d'un assemblage, ceux
// de la famille contre tous), un membre qui prend le nom de son conteneur (CS0542), un type qui prendrait le nom
// complet d'un espace de noms (CS0101), un nom qui commence par un chiffre ou qui est vide, un nom qui est un mot-clé
// (une exception comprise).
// Les noms qui ne diffèrent que par la casse (CS et Cs) ne sont pas une collision pour C# ; ils sont dits.
//
// VERIFIER lit la table (tools/noms/g14.txt, g15.txt, à la suite) et en juge UNE étape (--depuis X --jusqua X ; une
// sélection de plusieurs étapes avec des entrées est refusée : chacune nomme les choses sous les noms qu'elles ont à
// son début, et l'arbre n'en montre qu'un état), contre l'arbre tel qu'il est au début de cette étape : chaque type,
// type imbriqué, membre et paramètre doit exister sous son ancien nom (un membre absent est refusé), le nouveau nom
// d'un type de premier niveau ne doit pas exister ; chaque littéral de la liste close doit être écrit au moins une fois
// dans les sources ; et, la table appliquée, aucun type ne doit porter deux membres du même nom ni un membre du nom de
// son conteneur (une collision avec un membre que la table ne renomme pas compris). Retour 0 si l'étape tient, 2 sinon.

using System.Text;
using System.Text.RegularExpressions;
using iXtal26.Noms;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace iXtal26.Renommage;

internal static class Membres
{
    /// <summary>Une exception de la règle : la clé, le nouveau nom (null : garder), sa raison, sa ligne.</summary>
    private sealed record Derogation(string Cle, string? Nouveau, string Raison, string Ou)
    {
        public bool Prefixe => Cle.EndsWith('*');
        public bool Vue { get; set; }
    }

    private static readonly Regex LigneException =
        new(@"^//   (iXtal26\S*)\s+(\S+)(?:\s+(.*))?$", RegexOptions.CultureInvariant);

    private static List<Derogation> LireExceptions(string chemin)
    {
        var l = new List<Derogation>();
        var n = 0;
        foreach (var brute in File.ReadLines(chemin))
        {
            n++;
            var m = LigneException.Match(brute.TrimEnd());
            if (!m.Success) continue;
            var raison = m.Groups[3].Success ? m.Groups[3].Value.Trim() : "";
            if (raison == "")
                throw new FormatException($"{chemin}:{n} : une exception sans raison (« {m.Groups[1].Value} »)");
            var nv = m.Groups[2].Value;
            l.Add(new Derogation(m.Groups[1].Value, nv == "=" ? null : nv, raison, $"{chemin}:{n}"));
        }
        var doublons = l.GroupBy(x => x.Cle).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (doublons.Count > 0)
            throw new FormatException($"{chemin} : exceptions en double : {string.Join(", ", doublons)}");
        return l;
    }

    /// <summary>Le nom qualifié de métadonnées d'un type (A.B.C+D+E).</summary>
    internal static string NomQualifie(INamedTypeSymbol t)
    {
        if (t.ContainingType != null) return NomQualifie(t.ContainingType) + "+" + t.MetadataName;
        return t.ContainingNamespace.IsGlobalNamespace ? t.MetadataName
            : t.ContainingNamespace.ToDisplayString() + "." + t.MetadataName;
    }

    /// <summary>Les types déclarés dans les sources d'un projet, imbriqués compris, dans l'ordre des noms.</summary>
    private static IEnumerable<INamedTypeSymbol> TypesDeclares(Compilation c)
    {
        var pile = new Stack<INamespaceOrTypeSymbol>();
        pile.Push(c.Assembly.GlobalNamespace);
        var res = new List<INamedTypeSymbol>();
        while (pile.Count > 0)
        {
            var s = pile.Pop();
            foreach (var m in s.GetMembers())
            {
                if (m is INamespaceSymbol ns) pile.Push(ns);
                else if (m is INamedTypeSymbol t && !t.IsImplicitlyDeclared && t.DeclaringSyntaxReferences.Length > 0
                         && !t.Name.Contains('<'))
                {
                    res.Add(t);
                    pile.Push(t);
                }
            }
        }
        return res.OrderBy(NomQualifie, StringComparer.Ordinal);
    }

    /// <summary>
    /// Le genre d'un membre pour la règle ; « suit » pour un override ou une implémentation implicite d'un membre du
    /// même assemblage ; null s'il garde son nom sans entrée.
    /// </summary>
    private static string? Genre(ISymbol m)
    {
        if (m.IsImplicitlyDeclared || m.DeclaringSyntaxReferences.Length == 0 || m.Name.Contains('<')) return null;
        switch (m)
        {
            case IMethodSymbol me:
                if (me.MethodKind != MethodKind.Ordinary || me.IsExtern) return null;
                if (me.ExplicitInterfaceImplementations.Length > 0) return null;
                if (me.GetAttributes().Any(a => a.AttributeClass?.Name == "DllImportAttribute")) return null;
                return Redefini(me) is { } b ? (MemeAssemblage(b, me) ? "suit" : null) : "méthode";
            case IPropertySymbol p:
                if (p.IsIndexer || p.ExplicitInterfaceImplementations.Length > 0) return null;
                return Redefini(p) is { } bp ? (MemeAssemblage(bp, p) ? "suit" : null) : "propriété";
            case IEventSymbol ev:
                if (ev.ExplicitInterfaceImplementations.Length > 0) return null;
                return Redefini(ev) is { } be ? (MemeAssemblage(be, ev) ? "suit" : null) : "événement";
            case IFieldSymbol f:
                if (f.ContainingType.TypeKind == TypeKind.Enum) return "membre d'enum";
                if (f.IsConst) return "constante";
                return f.DeclaredAccessibility == Accessibility.Private ? "champ privé" : "champ";
            default:
                return null;
        }
    }

    private static bool MemeAssemblage(ISymbol a, ISymbol b) =>
        SymbolEqualityComparer.Default.Equals(a.ContainingAssembly, b.ContainingAssembly);

    /// <summary>
    /// Le membre que celui-ci redéfinit, à l'origine de sa chaîne : la base la plus haute d'un override, ou le membre
    /// d'interface qu'il implémente implicitement ; null s'il ne redéfinit rien.
    /// </summary>
    private static ISymbol? Redefini(ISymbol m)
    {
        ISymbol? b = m switch
        {
            IMethodSymbol { IsOverride: true } me => me.OverriddenMethod,
            IPropertySymbol { IsOverride: true } p => p.OverriddenProperty,
            IEventSymbol { IsOverride: true } e => e.OverriddenEvent,
            _ => null,
        };
        if (b != null) return Redefini(b) ?? b;
        foreach (var i in m.ContainingType.AllInterfaces)
            foreach (var im in i.GetMembers(m.Name))
                if (SymbolEqualityComparer.Default.Equals(m.ContainingType.FindImplementationForInterfaceMember(im), m))
                    return im;
        return null;
    }

    private static bool EstPascal(string id) => RegleNoms.Pascal(id) == id;

    private static string Regle(string genre, ISymbol m)
    {
        if (genre == "champ privé")
        {
            var f = (IFieldSymbol)m;
            return f.IsStatic && f.IsReadOnly && EstPascal(f.Name) ? f.Name : RegleNoms.Camel(f.Name);
        }
        return RegleNoms.Pascal(m.Name);
    }

    private static string RelatifAuDepot(string arbre, ISymbol s) =>
        Path.GetRelativePath(arbre, s.DeclaringSyntaxReferences[0].SyntaxTree.FilePath);

    internal static int Generer(IReadOnlyList<Projet> projets, string arbre, IReadOnlyList<string> famille,
                                string fichierExceptions, string etape, string sortie)
    {
        var exceptions = LireExceptions(fichierExceptions);
        var refus = new List<string>();
        var dits = new List<string>();
        bool DansFamille(string f) => famille.Any(p => Path.GetRelativePath(arbre, f).Replace('\\', '/')
                                                         .StartsWith(p, StringComparison.Ordinal));
        Derogation? ExceptionDe(string cle)
        {
            var e = exceptions.FirstOrDefault(x => !x.Prefixe && x.Cle == cle)
                    ?? exceptions.Where(x => x.Prefixe && cle.StartsWith(x.Cle[..^1], StringComparison.Ordinal)
                                             && cle.Contains("::"))
                                 .OrderByDescending(x => x.Cle.Length).FirstOrDefault();
            if (e != null) e.Vue = true;
            return e;
        }

        var tous = new List<INamedTypeSymbol>();
        var types = new List<INamedTypeSymbol>();
        foreach (var p in projets)
            foreach (var t in TypesDeclares(p.Comp))
            {
                tous.Add(t);
                var fichiers = t.DeclaringSyntaxReferences.Select(r => r.SyntaxTree.FilePath).Distinct().ToList();
                var dedans = fichiers.Count(DansFamille);
                if (dedans == 0) continue;
                if (dedans < fichiers.Count)
                {
                    refus.Add($"{NomQualifie(t)} : une classe partielle à cheval sur la famille ("
                              + string.Join(", ", fichiers.Select(f => Path.GetRelativePath(arbre, f))) + ")");
                    continue;
                }
                types.Add(t);
            }
        var dansLaFamille = types.ToHashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

        // Le nouveau nom simple de chaque type de la famille (le « _t » ôté, provisoirement).
        var nomType = new Dictionary<INamedTypeSymbol, string>(SymbolEqualityComparer.Default);
        var sansT = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        foreach (var t in types)
        {
            var e = ExceptionDe(NomQualifie(t));
            if (e != null) { nomType[t] = e.Nouveau ?? t.MetadataName; continue; }
            var tronque = t.Name.EndsWith("_t", StringComparison.Ordinal) && t.Name.Length > 2;
            if (tronque) sansT.Add(t);
            nomType[t] = (tronque ? RegleNoms.Pascal(t.Name[..^2]) : RegleNoms.Pascal(t.Name))
                         + (t.Arity > 0 ? "`" + t.Arity : "");
        }
        string NomFinal(INamedTypeSymbol t) => nomType.TryGetValue(t, out var n) ? n : t.MetadataName;

        // Le nouveau nom d'un membre de la famille : la règle, l'exception, ou celui du membre qu'il redéfinit.
        string? NomDuMembre(ISymbol m, out string? genre)
        {
            genre = Genre(m);
            if (genre == null) return null;
            if (genre == "suit")
            {
                var b = Redefini(m)!;
                return dansLaFamille.Contains(b.ContainingType.OriginalDefinition) ? NomDuMembre(b, out _) ?? b.Name
                    : m.Name;
            }
            var e = ExceptionDe($"{NomQualifie(m.ContainingType)}::{m.Name}");
            return e != null ? e.Nouveau ?? m.Name : Regle(genre, m);
        }
        // Les noms d'une portée (un type) après la règle : ses types imbriqués et ses membres.
        Dictionary<string, List<string>> Finaux(INamedTypeSymbol t, INamedTypeSymbol? sauf)
        {
            var finaux = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            void Porter(string final, string ancien)
            {
                if (!finaux.TryGetValue(final, out var l)) finaux[final] = l = [];
                if (!l.Contains(ancien)) l.Add(ancien);
            }
            foreach (var n in t.GetTypeMembers())
                if (!SymbolEqualityComparer.Default.Equals(n, sauf)) Porter(NomFinal(n).Split('`')[0], n.MetadataName);
            foreach (var m in t.GetMembers().Where(m => m is not INamedTypeSymbol && !m.IsImplicitlyDeclared
                                                        && !m.Name.Contains('<')))
            {
                if (m is IMethodSymbol { MethodKind: not MethodKind.Ordinary }) continue;
                var nv = dansLaFamille.Contains(t) ? NomDuMembre(m, out _) : null;
                Porter(nv ?? m.Name, m.Name);
            }
            return finaux;
        }

        // « _t » : s'il collisionne avec un voisin, le type garde le suffixe, en PascalCase (keyboard_xt_t →
        // KeyboardXtT).
        foreach (var t in types.Where(sansT.Contains))
        {
            var voisins = t.ContainingType != null
                ? Finaux(t.ContainingType, t).Keys.ToHashSet(StringComparer.Ordinal)
                : tous.Where(x => x.ContainingType == null && MemeAssemblage(x, t)
                                  && SymbolEqualityComparer.Default.Equals(x.ContainingNamespace, t.ContainingNamespace)
                                  && !SymbolEqualityComparer.Default.Equals(x, t))
                      .Select(x => NomFinal(x)).ToHashSet(StringComparer.Ordinal);
            if (!voisins.Contains(nomType[t])) continue;
            dits.Add($"{NomQualifie(t)} garde son suffixe : « {nomType[t]} » collisionne");
            nomType[t] = RegleNoms.Pascal(t.Name) + (t.Arity > 0 ? "`" + t.Arity : "");
        }

        var entrees = new List<(string Cle, string Nouveau, string Quoi)>();
        foreach (var t in types)
        {
            var cle = NomQualifie(t);
            var nt = NomFinal(t);
            var simple = nt.Split('`')[0];
            if (simple.Length == 0 || char.IsDigit(simple[0]))
                refus.Add($"{cle} : le nom « {nt} » commence par un chiffre ou est vide ; une exception est due");
            else if (SyntaxFacts.GetKeywordKind(simple) != SyntaxKind.None)
                refus.Add($"{cle} : « {simple} » est un mot-clé");
            if (nt != t.MetadataName)
            {
                var parent = cle.LastIndexOf('+');
                var nouveau = parent < 0 ? (t.ContainingNamespace.IsGlobalNamespace ? nt
                    : t.ContainingNamespace.ToDisplayString() + "." + nt) : cle[..parent] + "+" + nt;
                entrees.Add((cle, nouveau, t.ContainingType == null ? "type" : "type imbriqué"));
            }
            var vus = new HashSet<string>(StringComparer.Ordinal);
            foreach (var m in t.GetMembers().Where(m => m is not INamedTypeSymbol))
            {
                var nv = NomDuMembre(m, out var genre);
                if (nv == null || !vus.Add(m.Name)) continue;
                var cm = $"{cle}::{m.Name}";
                if (nv.Length == 0 || char.IsDigit(nv[0]))
                    refus.Add($"{cm} ({RelatifAuDepot(arbre, m)}) : le nom « {nv} » commence par un chiffre ou est "
                              + "vide ; une exception est due");
                else if (SyntaxFacts.GetKeywordKind(nv) != SyntaxKind.None)
                    refus.Add($"{cm} : « {nv} » est un mot-clé ; une exception est due");
                if (nv != m.Name) entrees.Add((cm, nv, genre == "suit" ? "suit le membre qu'il redéfinit" : genre!));
            }
            var finaux = Finaux(t, null);
            foreach (var (final, anciens) in finaux.Where(x => x.Value.Count > 1))
                refus.Add($"{cle} : collision, {string.Join(" et ", anciens.Select(a => $"« {a} »"))} deviennent tous "
                          + $"« {final} » ; une exception est due");
            if (finaux.TryGetValue(simple, out var homonymes) && t.TypeKind != TypeKind.Enum)
                refus.Add($"{cle} : {string.Join(", ", homonymes.Select(a => $"« {a} »"))} prendrait le nom de son "
                          + $"conteneur, « {simple} » (CS0542) ; une exception est due");
            foreach (var g in finaux.Keys.GroupBy(k => k.ToLowerInvariant()).Where(g => g.Count() > 1))
                dits.Add($"{cle} : des noms qui ne diffèrent que par la casse : {string.Join(", ", g)}");
        }

        // Un type de premier niveau qui prendrait le nom complet d'un espace de noms (CS0101).
        var espaces = new HashSet<string>(StringComparer.Ordinal);
        foreach (var p in projets)
        {
            var pile = new Stack<INamespaceSymbol>();
            pile.Push(p.Comp.Assembly.GlobalNamespace);
            while (pile.Count > 0)
                foreach (var ns in pile.Pop().GetNamespaceMembers())
                {
                    espaces.Add(ns.ToDisplayString());
                    pile.Push(ns);
                }
        }
        foreach (var t in types.Where(x => x.ContainingType == null && !x.ContainingNamespace.IsGlobalNamespace))
        {
            var complet = t.ContainingNamespace.ToDisplayString() + "." + NomFinal(t).Split('`')[0];
            if (espaces.Contains(complet))
                refus.Add($"{NomQualifie(t)} : « {complet} » est un espace de noms (CS0101) ; une exception est due");
        }
        // Les types de premier niveau d'un même espace de noms : ceux de la famille contre tous.
        foreach (var g in tous.Where(x => x.ContainingType == null)
                              .GroupBy(x => (x.ContainingAssembly.Name + " " + x.ContainingNamespace.ToDisplayString(),
                                             NomFinal(x)))
                              .Where(g => g.Count() > 1 && g.Any(dansLaFamille.Contains)))
            refus.Add($"{g.Key.Item1} : collision, {string.Join(" et ", g.Select(NomQualifie))} deviennent tous "
                      + $"« {g.Key.Item2} » ; une exception est due");
        foreach (var e in exceptions.Where(x => !x.Vue))
            refus.Add($"{e.Ou} : l'exception « {e.Cle} » ne désigne rien de la famille");

        foreach (var d in dits) Console.WriteLine($"  dit : {d}");
        if (refus.Count > 0)
        {
            foreach (var r in refus) Console.Error.WriteLine($"  refus : {r}");
            Console.Error.WriteLine($"renommage generer : {refus.Count} refus ; rien n'est écrit");
            return 2;
        }
        entrees.Sort((a, b) => string.CompareOrdinal(a.Cle, b.Cle));
        var largeur = Math.Min(70, entrees.Select(x => x.Cle.Length).DefaultIfEmpty(0).Max()) + 2;
        var sb = new StringBuilder();
        sb.Append($"// étape {etape} — généré par tools/renommage generer (famille : {string.Join(", ", famille)} ; "
                  + $"exceptions : {Path.GetFileName(fichierExceptions)})\n");
        foreach (var (c, nv, quoi) in entrees)
        {
            var ligne = $"//   {c.PadRight(largeur)}";
            if (c.Length >= largeur) ligne = $"//   {c}  ";
            ligne += nv;
            sb.Append(ligne.Length + quoi.Length + 2 <= 120 ? $"{ligne}  {quoi}\n" : ligne + "\n");
        }
        File.WriteAllText(sortie, sb.ToString());
        Console.WriteLine($"  {types.Count} types dans la famille ; {entrees.Count} entrées écrites dans {sortie} "
                          + $"({entrees.Count(x => x.Quoi.StartsWith("type", StringComparison.Ordinal))} types, "
                          + $"{entrees.Count(x => !x.Quoi.StartsWith("type", StringComparison.Ordinal))} membres) ; "
                          + $"{exceptions.Count} exceptions, toutes servies");
        return 0;
    }

    internal static int Verifier(IReadOnlyList<Projet> projets, TableDesNoms table)
    {
        var etapes = table.Entrees.Select(e => e.Etape).Distinct().ToList();
        if (etapes.Count != 1)
            throw new FormatException($"verifier juge une étape à la fois ; la sélection en a {etapes.Count} avec des "
                                      + $"entrées ({string.Join(", ", etapes)}) : --depuis X --jusqua X");
        var refus = new List<string>();
        INamedTypeSymbol? Type(string ancien)
        {
            foreach (var p in projets)
            {
                var t = p.Comp.GetTypeByMetadataName(ancien);
                if (t != null && t.DeclaringSyntaxReferences.Length > 0) return t;
            }
            return null;
        }
        foreach (var x in table.Entrees)
        {
            switch (x.Genre)
            {
                case GenreNom.Type or GenreNom.Imbrique when Type(x.Ancien) == null:
                    refus.Add($"{x.Ou} : le type « {x.Ancien} » n'existe pas");
                    break;
                case GenreNom.Type when projets.Any(p => p.Comp.GetTypeByMetadataName(x.Nouveau) != null):
                    refus.Add($"{x.Ou} : le type « {x.Nouveau} » existe déjà");
                    break;
                case GenreNom.Membre:
                {
                    var t = Type(x.Type);
                    if (t == null) refus.Add($"{x.Ou} : le type « {x.Type} » n'existe pas");
                    else if (!t.GetMembers(x.Ancien).Any(m => !m.IsImplicitlyDeclared))
                        refus.Add($"{x.Ou} : le membre « {x.Ancien} » n'existe pas dans {x.Type}");
                    break;
                }
                case GenreNom.Parametre:
                {
                    var t = Type(x.Type);
                    var ms = t?.GetMembers(x.Methode).OfType<IMethodSymbol>().ToList() ?? [];
                    if (!ms.Any(m => m.Parameters.Any(p => p.Name == x.Ancien)))
                        refus.Add($"{x.Ou} : aucune méthode « {x.Methode} » de {x.Type} n'a de paramètre "
                                  + $"« {x.Ancien} »");
                    break;
                }
                case GenreNom.Litteral:
                {
                    var vu = projets.Any(p => p.Arbres.Any(a => a.GetRoot().DescendantNodes()
                        .OfType<LiteralExpressionSyntax>().Any(l => l.IsKind(SyntaxKind.StringLiteralExpression)
                                                                    && l.Token.ValueText == x.Ancien)));
                    if (!vu) refus.Add($"{x.Ou} : le littéral \"{x.Ancien}\" n'est écrit nulle part");
                    break;
                }
            }
        }
        // La table appliquée : les noms de chaque type touché, membres non renommés compris.
        foreach (var cle in table.Membres.Keys
                                  .Concat(table.Entrees.Where(e => e.Genre == GenreNom.Imbrique).Select(e => e.Type))
                                  .Distinct(StringComparer.Ordinal))
        {
            var t = Type(cle);
            if (t == null) continue;
            var ren = table.Membres.GetValueOrDefault(cle) ?? [];
            var finaux = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            foreach (var m in t.GetMembers().Where(m => !m.IsImplicitlyDeclared && !m.Name.Contains('<')
                                                        && m is not IMethodSymbol
                                                        { MethodKind: not MethodKind.Ordinary }))
            {
                var ancien = m is INamedTypeSymbol n ? n.MetadataName : m.Name;
                var final = m is INamedTypeSymbol nt
                    ? TableDesNoms.NomSimple(table.NouveauNomDeType(NomQualifie(nt))).Split('`')[0]
                    : ren.GetValueOrDefault(m.Name, m.Name);
                if (!finaux.TryGetValue(final, out var s)) finaux[final] = s = new(StringComparer.Ordinal);
                s.Add(ancien);
            }
            foreach (var (final, anciens) in finaux.Where(x => x.Value.Count > 1))
                refus.Add($"{cle} : collision, {string.Join(" et ", anciens.Select(a => $"« {a} »"))} portent tous "
                          + $"« {final} » une fois la table appliquée");
            var conteneur = TableDesNoms.NomSimple(table.NouveauNomDeType(cle)).Split('`')[0];
            if (finaux.TryGetValue(conteneur, out var h) && t.TypeKind != TypeKind.Enum)
                refus.Add($"{cle} : {string.Join(", ", h)} porterait le nom de son conteneur, « {conteneur} » "
                          + "(CS0542)");
        }
        if (refus.Count > 0)
        {
            foreach (var r in refus) Console.Error.WriteLine($"  refus : {r}");
            Console.Error.WriteLine($"renommage verifier : {refus.Count} refus");
            return 2;
        }
        Console.WriteLine($"  étape {etapes[0]} vérifiée : {table.Types.Count} types, {table.Imbriques.Count} types "
                          + $"imbriqués, {table.Membres.Values.Sum(d => d.Count)} membres, "
                          + $"{table.Parametres.Values.Sum(d => d.Count)} paramètres, {table.Litteraux.Count} littéraux"
                          + " ; tous existent, aucune collision");
        return 0;
    }
}
