// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — outillage de la preuve des noms (G14.0c, PLAN-G14.md § G14.0c ; étendu aux membres en G15.0b,
// PLAN-G15.md § G15.0b).
//
// La lecture de la table des noms (tools/noms/g14.txt, puis tools/noms/g15.txt, dont les en-têtes disent la
// grammaire), partagée par tools/ildump et tools/renommage, qui l'incluent par <Compile Include>. tools/listings-jit.sh
// en a la lecture des types et des fichiers, en python (ses refus sont ceux de G14, sur toute la table). Plusieurs
// fichiers se lisent à la suite, dans l'ordre donné, comme un seul : leurs étapes se suivent (la référence est le
// commit R, d'avant G14), --depuis et --jusqua bornent celles qu'on garde.
// CHAQUE ÉTAPE nomme ce qu'elle renomme sous les noms qu'il porte à son début, c'est-à-dire après les étapes d'avant
// (iXtal26.Ics.PIC, nouveau de G14.1, est l'ancien d'une étape de G15) : les refus (doublons, chaînes, collisions,
// CS0542) se jugent étape par étape, et un nom se traduit en passant les étapes dans l'ordre (NouveauNomDeType,
// NouveauMembre), comme ildump et listings-jit.sh le font d'un vidage de R.

using System.Text.RegularExpressions;

namespace iXtal26.Noms;

/// <summary>Ce qu'une entrée renomme.</summary>
internal enum GenreNom
{
    /// <summary>Un type de premier niveau, nom qualifié (iXtal26.Ics.Intel8259A).</summary>
    Type,
    /// <summary>Un type imbriqué (iXtal26.Ics.Intel8259A+pic_t), son conteneur inchangé dans l'entrée.</summary>
    Imbrique,
    /// <summary>Un fichier du dépôt (iXtal26/….cs).</summary>
    Fichier,
    /// <summary>Un membre d'un type (Type::membre) : champ, constante, méthode et ses surcharges, propriété, événement,
    /// membre d'enum.</summary>
    Membre,
    /// <summary>Un paramètre, dans toutes les surcharges de sa méthode (Type::méthode(paramètre)).</summary>
    Parametre,
    /// <summary>Une chaîne littérale qui suit un membre (la liste close : "ancien" "nouveau").</summary>
    Litteral,
}

/// <summary>
/// Une entrée. Pour un membre ou un paramètre, <see cref="Type"/> est le type qui le déclare, sous son ANCIEN nom
/// (iXtal26.Ics.Intel8259A+pic_t), <see cref="Methode"/> la méthode d'un paramètre, <see cref="Ancien"/> et
/// <see cref="Nouveau"/> les deux identifiants ; pour un type imbriqué, <see cref="Type"/> est son conteneur et
/// <see cref="Nouveau"/> son nouveau nom simple ; pour les autres genres, les deux noms entiers de la ligne.
/// </summary>
internal sealed record EntreeNom(GenreNom Genre, string Ancien, string Nouveau, string Type, string Methode,
                                 string Etape, string Source, int Ligne)
{
    public bool EstFichier => Genre == GenreNom.Fichier;
    public string Ou => $"{Source}:{Ligne}";
}

internal sealed class TableDesNoms
{
    private static readonly Regex Etape = new(@"^//\s+étape\s+(\S+)", RegexOptions.CultureInvariant);
    private static readonly Regex Entree = new(@"^//   ((?:iXtal26|"")\S*)(?:\s+(\S+))?",
        RegexOptions.CultureInvariant);
    private const string Segment = @"[A-Za-z_][A-Za-z0-9_]*(?:`[0-9]+)?";
    private static readonly Regex TypeQualifie = new($@"^iXtal26(?:\.{Segment})+(?:\+{Segment})*$",
        RegexOptions.CultureInvariant);
    private static readonly Regex Membre = new(
        $@"^(?<type>iXtal26(?:\.{Segment})+(?:\+{Segment})*)::(?<membre>[A-Za-z_][A-Za-z0-9_]*)"
        + @"(?:\((?<param>[A-Za-z_][A-Za-z0-9_]*)\))?$", RegexOptions.CultureInvariant);
    private static readonly Regex Identifiant = new(@"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant);
    private static readonly Regex Litteral = new(@"^""[^""\s]*""$", RegexOptions.CultureInvariant);
    private static readonly Regex Chemin = new(@"^iXtal26/[A-Za-z0-9_./-]+\.cs$", RegexOptions.CultureInvariant);

    public List<EntreeNom> Entrees { get; } = [];
    public List<string> Etapes { get; } = [];

    /// <summary>Les étapes gardées, dans l'ordre, chacune avec ses seules entrées.</summary>
    private readonly List<TableDesNoms> _parEtape = [];

    /// <summary>
    /// Les types de premier niveau renommés, du nom qu'ils ont avant la première étape gardée au nom qu'ils ont
    /// après la dernière (les étapes composées). Les dictionnaires qui suivent (types imbriqués, membres, paramètres)
    /// gardent la clé de l'étape qui les nomme : ils sont exacts quand une seule étape est gardée (tools/renommage) ;
    /// pour plusieurs, NouveauNomDeType et NouveauMembre les passent dans l'ordre.
    /// </summary>
    public Dictionary<string, string> Types { get; } = new(StringComparer.Ordinal);

    /// <summary>Les types imbriqués renommés, nom qualifié ancien (A+B) → le nouveau nom simple.</summary>
    public Dictionary<string, string> Imbriques { get; } = new(StringComparer.Ordinal);

    /// <summary>Les membres renommés : le type déclarant (son ancien nom) → (ancien membre → nouveau).</summary>
    public Dictionary<string, Dictionary<string, string>> Membres { get; } = new(StringComparer.Ordinal);

    /// <summary>Les paramètres renommés : « type::méthode » (anciens) → (ancien paramètre → nouveau).</summary>
    public Dictionary<string, Dictionary<string, string>> Parametres { get; } = new(StringComparer.Ordinal);

    /// <summary>La liste close des littéraux qui suivent un membre, ancien → nouveau (sans les guillemets).</summary>
    public Dictionary<string, string> Litteraux { get; } = new(StringComparer.Ordinal);

    /// <summary>Vrai si la table renomme autre chose que des types de premier niveau et des fichiers.</summary>
    public bool NommeDesMembres => Imbriques.Count + Membres.Count + Parametres.Count + Litteraux.Count > 0;

    /// <summary>Lit une table (voir l'autre surcharge).</summary>
    public static TableDesNoms Lire(string chemin, string? jusqua) => Lire([chemin], jusqua);

    /// <summary>
    /// Lit les tables, à la suite, et ne garde que les étapes de <paramref name="depuis"/> (la première si null) à
    /// <paramref name="jusqua"/> (la dernière si null), comprises. Toute incohérence lève une exception : la table est
    /// refusée, jamais lue à moitié.
    /// </summary>
    public static TableDesNoms Lire(IReadOnlyList<string> chemins, string? jusqua, string? depuis = null)
    {
        var t = new TableDesNoms();
        var toutes = new List<string>();
        var vue = jusqua == null;
        var commence = depuis == null;
        var arret = false;
        foreach (var chemin in chemins)
        {
            if (arret) break;
            var etape = "";
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
                    if (toutes.Contains(etape))
                        throw new FormatException($"{chemin}:{n} : l'étape « {etape} » est ouverte deux fois");
                    toutes.Add(etape);
                    if (etape == depuis) commence = true;
                    if (commence) t.Etapes.Add(etape);
                    if (etape == jusqua) { vue = true; arret = true; }
                    continue;
                }
                var m = Entree.Match(l);
                if (!m.Success) continue;
                var x = Analyser(chemin, n, m.Groups[1].Value, m.Groups[2].Success ? m.Groups[2].Value : "", etape);
                if (commence) t.Entrees.Add(x);
            }
        }
        var ou = string.Join(", ", chemins);
        if (jusqua != null && !vue)
            throw new FormatException($"{ou} : l'étape « {jusqua} » n'existe pas (étapes : "
                                      + string.Join(", ", toutes) + ")");
        if (depuis != null && !commence)
            throw new FormatException(toutes.Contains(depuis)
                ? $"{ou} : l'étape « {depuis} » vient après « {jusqua} »"
                : $"{ou} : l'étape « {depuis} » n'existe pas (étapes : " + string.Join(", ", toutes) + ")");
        if (t.Entrees.Count == 0)
            throw new FormatException($"{ou} : aucune entrée retenue");
        // Un même ancien dans deux étapes : la première l'a renommé, il n'existe plus pour la seconde, sauf si une
        // étape entre elles le redonne (son nouveau). Refusé sinon, comme G14 refusait un ancien en double.
        var vus = new Dictionary<string, EntreeNom>(StringComparer.Ordinal);
        foreach (var et in t.Etapes)
        {
            var dEtape = t.Entrees.Where(x => x.Etape == et).ToList();
            foreach (var x in dEtape)
                if (vus.TryGetValue(Cle(x), out var avant))
                    throw new FormatException($"{x.Ou} : « {Cle(x)} » est déjà l'ancien d'une entrée de l'étape "
                                              + $"{avant.Etape} ({avant.Ou}), qui l'a renommé");
            foreach (var x in dEtape) vus.Remove(CleNouvelle(x));
            foreach (var x in dEtape) vus[Cle(x)] = x;
        }
        foreach (var et in t.Etapes)
        {
            var u = new TableDesNoms();
            u.Etapes.Add(et);
            u.Entrees.AddRange(t.Entrees.Where(x => x.Etape == et));
            u.Verifier();
            t._parEtape.Add(u);
        }
        t.Composer();
        return t;
    }

    /// <summary>
    /// Les dictionnaires de la table entière : les types de premier niveau composés d'une étape à l'autre, le reste
    /// réuni sous les clés de chaque étape.
    /// </summary>
    private void Composer()
    {
        foreach (var u in _parEtape)
        {
            // Un ancien de l'étape qui est le nom courant d'un type déjà suivi le prolonge ; sinon il commence ici.
            var courants = Types.Values.ToHashSet(StringComparer.Ordinal);
            foreach (var k in Types.Keys.ToList())
                if (u.Types.TryGetValue(Types[k], out var plus)) Types[k] = plus;
            foreach (var (a, nv) in u.Types)
                if (!courants.Contains(a)) Types.Add(a, nv);
            foreach (var (k, v) in u.Imbriques) Imbriques[k] = v;
            foreach (var (k, d) in u.Membres)
            {
                if (!Membres.TryGetValue(k, out var dd)) Membres[k] = dd = new(StringComparer.Ordinal);
                foreach (var (a, nv) in d) dd[a] = nv;
            }
            foreach (var (k, d) in u.Parametres)
            {
                if (!Parametres.TryGetValue(k, out var dd)) Parametres[k] = dd = new(StringComparer.Ordinal);
                foreach (var (a, nv) in d) dd[a] = nv;
            }
            foreach (var (k, v) in u.Litteraux) Litteraux[k] = v;
        }
    }

    /// <summary>
    /// Un membre (type déclarant et membre, sous les noms d'avant la première étape gardée) après toutes les étapes :
    /// à chaque étape, le membre d'abord (sa clé porte le type sous le nom du début de l'étape), puis le type.
    /// </summary>
    public (string Type, string Membre) NouveauMembre(string type, string membre)
    {
        foreach (var u in _parEtape)
        {
            if (u.Membres.TryGetValue(type, out var d) && d.TryGetValue(membre, out var nv)) membre = nv;
            type = u.NouveauNomDeTypeDUneEtape(type);
        }
        return (type, membre);
    }

    /// <summary>Un paramètre après toutes les étapes (comme <see cref="NouveauMembre"/>).</summary>
    public string NouveauParametre(string type, string methode, string parametre)
    {
        foreach (var u in _parEtape)
        {
            if (u.Parametres.TryGetValue($"{type}::{methode}", out var d) && d.TryGetValue(parametre, out var nv))
                parametre = nv;
            if (u.Membres.TryGetValue(type, out var dm) && dm.TryGetValue(methode, out var nm)) methode = nm;
            type = u.NouveauNomDeTypeDUneEtape(type);
        }
        return parametre;
    }

    private static EntreeNom Analyser(string chemin, int n, string ancien, string nouveau, string etape)
    {
        if (etape == "")
            throw new FormatException($"{chemin}:{n} : entrée hors de toute étape (« // étape X » manque)");
        if (ancien == nouveau)
            throw new FormatException($"{chemin}:{n} : « {ancien} » ne change pas de nom");
        EntreeNom E(GenreNom g, string a, string nv, string type = "", string methode = "") =>
            new(g, a, nv, type, methode, etape, chemin, n);
        if (Chemin.IsMatch(ancien) && Chemin.IsMatch(nouveau)) return E(GenreNom.Fichier, ancien, nouveau);
        if (Litteral.IsMatch(ancien) && Litteral.IsMatch(nouveau))
            return E(GenreNom.Litteral, ancien[1..^1], nouveau[1..^1]);
        if (TypeQualifie.IsMatch(ancien) && TypeQualifie.IsMatch(nouveau))
        {
            var ia = ancien.LastIndexOf('+');
            var inv = nouveau.LastIndexOf('+');
            if (ia < 0 && inv < 0) return E(GenreNom.Type, ancien, nouveau);
            if (ia < 0 || inv < 0 || ancien[..ia] != nouveau[..inv])
                throw new FormatException($"{chemin}:{n} : un type imbriqué garde son conteneur dans l'entrée "
                                          + $"(« {ancien} » → « {nouveau} ») ; le conteneur a sa propre entrée");
            return E(GenreNom.Imbrique, ancien, nouveau[(inv + 1)..], ancien[..ia]);
        }
        var mm = Membre.Match(ancien);
        if (mm.Success && Identifiant.IsMatch(nouveau))
        {
            var type = mm.Groups["type"].Value;
            var membre = mm.Groups["membre"].Value;
            if (mm.Groups["param"].Success)
                return E(GenreNom.Parametre, mm.Groups["param"].Value, nouveau, type, membre);
            return E(GenreNom.Membre, membre, nouveau, type);
        }
        throw new FormatException($"{chemin}:{n} : entrée invalide (« {ancien} » → « {nouveau} ») : il faut deux noms "
                                  + "de type qualifiés, deux chemins iXtal26/….cs, deux littéraux \"…\", ou un "
                                  + "membre Type::membre (ou Type::méthode(paramètre)) et un identifiant");
    }

    /// <summary>La clé d'une entrée, unique dans la table : ce qu'elle désigne, sous son ancien nom.</summary>
    public static string Cle(EntreeNom x) => x.Genre switch
    {
        GenreNom.Membre => $"{x.Type}::{x.Ancien}",
        GenreNom.Parametre => $"{x.Type}::{x.Methode}({x.Ancien})",
        GenreNom.Litteral => $"\"{x.Ancien}\"",
        _ => x.Ancien,
    };

    /// <summary>La clé de ce qu'une entrée désigne après son étape (son nouveau nom).</summary>
    private static string CleNouvelle(EntreeNom x) => x.Genre switch
    {
        GenreNom.Membre => $"{x.Type}::{x.Nouveau}",
        GenreNom.Parametre => $"{x.Type}::{x.Methode}({x.Nouveau})",
        GenreNom.Litteral => $"\"{x.Nouveau}\"",
        GenreNom.Imbrique => $"{x.Type}+{x.Nouveau}",
        _ => x.Nouveau,
    };

    /// <summary>Les refus qui ne demandent que la table : doublons, chaînes, collisions, CS0542.</summary>
    private void Verifier()
    {
        var anciens = new Dictionary<string, EntreeNom>(StringComparer.Ordinal);
        foreach (var x in Entrees)
            if (!anciens.TryAdd(Cle(x), x))
                throw new FormatException($"{x.Ou} : « {Cle(x)} » est déjà l'ancien d'une entrée "
                                          + $"({anciens[Cle(x)].Ou})");

        // Les types de premier niveau et les fichiers : un nouveau unique, pas de chaîne (G14).
        foreach (var g in new[] { GenreNom.Type, GenreNom.Fichier })
        {
            var nouveaux = new HashSet<string>(StringComparer.Ordinal);
            var olds = Entrees.Where(x => x.Genre == g).Select(x => x.Ancien).ToHashSet(StringComparer.Ordinal);
            foreach (var x in Entrees.Where(x => x.Genre == g))
            {
                if (!nouveaux.Add(x.Nouveau))
                    throw new FormatException($"{x.Ou} : « {x.Nouveau} » est déjà le nouveau d'une entrée");
                if (olds.Contains(x.Nouveau))
                    throw new FormatException($"{x.Ou} : chaîne de noms : « {x.Nouveau} » est aussi un ancien");
            }
        }
        foreach (var x in Entrees.Where(x => x.Genre == GenreNom.Type)) Types[x.Ancien] = x.Nouveau;
        foreach (var x in Entrees.Where(x => x.Genre == GenreNom.Imbrique)) Imbriques[x.Ancien] = x.Nouveau;

        // Une portée : un type (ses types imbriqués et ses membres partagent l'espace des noms de membres), ou une
        // méthode (ses paramètres). Deux anciens vers un même nouveau, ou un nouveau qui est l'ancien d'une autre
        // entrée de la même portée, sont refusés.
        void Portee(IEnumerable<EntreeNom> l, string quoi)
        {
            var liste = l.ToList();
            var olds = liste.Select(x => x.Genre == GenreNom.Imbrique ? NomSimple(x.Ancien) : x.Ancien)
                            .ToHashSet(StringComparer.Ordinal);
            var parNouveau = new Dictionary<string, EntreeNom>(StringComparer.Ordinal);
            foreach (var x in liste)
            {
                if (!parNouveau.TryAdd(x.Nouveau, x))
                    throw new FormatException($"{x.Ou} : collision dans {quoi} : « {Cle(x)} » et "
                                              + $"« {Cle(parNouveau[x.Nouveau])} » ({parNouveau[x.Nouveau].Ou}) "
                                              + $"deviennent tous deux « {x.Nouveau} »");
                if (olds.Contains(x.Nouveau))
                    throw new FormatException($"{x.Ou} : chaîne de noms dans {quoi} : « {x.Nouveau} » est aussi un "
                                              + "ancien");
            }
        }
        foreach (var g in Entrees.Where(x => x.Genre is GenreNom.Membre or GenreNom.Imbrique)
                                 .GroupBy(x => x.Type, StringComparer.Ordinal))
        {
            Portee(g, g.Key);
            // CS0542 : un membre ne porte pas le nom de son conteneur (après le renommage du conteneur).
            var conteneur = NomSimple(NouveauNomDeTypeDUneEtape(g.Key)).Split('`')[0];
            foreach (var x in g.Where(x => x.Nouveau == conteneur))
                throw new FormatException($"{x.Ou} : « {Cle(x)} » prendrait le nom de son conteneur, « {conteneur} » "
                                          + "(CS0542)");
        }
        foreach (var g in Entrees.Where(x => x.Genre == GenreNom.Parametre)
                                 .GroupBy(x => $"{x.Type}::{x.Methode}", StringComparer.Ordinal))
            Portee(g, g.Key);
        foreach (var x in Entrees.Where(x => x.Genre == GenreNom.Membre))
        {
            if (!Membres.TryGetValue(x.Type, out var d)) Membres[x.Type] = d = new(StringComparer.Ordinal);
            d[x.Ancien] = x.Nouveau;
        }
        foreach (var x in Entrees.Where(x => x.Genre == GenreNom.Parametre))
        {
            var k = $"{x.Type}::{x.Methode}";
            if (!Parametres.TryGetValue(k, out var d)) Parametres[k] = d = new(StringComparer.Ordinal);
            d[x.Ancien] = x.Nouveau;
        }
        foreach (var x in Entrees.Where(x => x.Genre == GenreNom.Litteral)) Litteraux[x.Ancien] = x.Nouveau;
    }

    /// <summary>Le dernier segment d'un nom de type qualifié (après le dernier « . » ou « + »).</summary>
    public static string NomSimple(string type) => type[(type.LastIndexOfAny(['.', '+']) + 1)..];

    /// <summary>Le nom d'un type de premier niveau après la table (inchangé s'il n'y est pas).</summary>
    public string Appliquer(string nomQualifie) =>
        Types.TryGetValue(nomQualifie, out var n) ? n : nomQualifie;

    /// <summary>
    /// Le nom qualifié d'un type, premier niveau ou imbriqué (A+B+C, sous les noms d'avant la première étape gardée),
    /// après toutes les étapes, passées dans l'ordre.
    /// </summary>
    public string NouveauNomDeType(string ancien)
    {
        if (_parEtape.Count == 0) return NouveauNomDeTypeDUneEtape(ancien);
        foreach (var u in _parEtape) ancien = u.NouveauNomDeTypeDUneEtape(ancien);
        return ancien;
    }

    /// <summary>Le nom qualifié d'un type après une seule étape : chaque segment prend son nouveau nom.</summary>
    private string NouveauNomDeTypeDUneEtape(string ancien)
    {
        var parts = ancien.Split('+');
        var res = Appliquer(parts[0]);
        var vieux = parts[0];
        for (var i = 1; i < parts.Length; i++)
        {
            vieux += "+" + parts[i];
            res += "+" + (Imbriques.TryGetValue(vieux, out var nv) ? nv : parts[i]);
        }
        return res;
    }
}
