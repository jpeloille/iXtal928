// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — outillage de la preuve des noms (G14.0c, PLAN-G14.md § G14.0c ; R2-noms.md § 1.7, R2-contre.md
// § 0 point 7).
//
// LE VIDAGE DE L'IL d'un assemblage, par System.Reflection.Metadata (dans le runtime : rien à installer).
//
//   ildump vider DLL SORTIE [--noms TABLE [--jusqua ETAPE]]
//   ildump comparer REF AUTRE [--noms TABLE [--jusqua ETAPE]] [--sorties DOSSIER]
//
// Le vidage est une suite d'entrées « == clé », triées par clé (l'ordre ordinal) : l'assemblage, chaque type, chaque
// méthode. La clé est le nom qualifié : l'ordre des TypeDef et des jetons, qui suit l'ordre de compilation des
// fichiers, n'y entre pas (R2-noms.md § 1.7). Chaque jeton de l'IL est résolu en nom ; la table des noms s'applique
// à chaque nom de type, à la clé comme au corps, et seulement au vidage de REF (les noms anciens deviennent les
// nouveaux). Ce qui est vidé :
//   - l'assemblage : nom, version, culture, drapeaux, clé publique, algorithme ; les références d'assemblage et de
//     module et les ressources (leur sha256), chacune avec ses attributs ; les attributs de l'assemblage et du
//     module ; le point d'entrée ;
//   - un type : drapeaux, base, interfaces, disposition (paquet, taille), paramètres génériques et leurs contraintes,
//     attributs, les champs DANS L'ORDRE des métadonnées (nom, signature, drapeaux, constante, décalage, marshalling,
//     sha256 des données d'un champ à RVA), les propriétés et les événements (accesseurs), les MethodImpl ;
//     chacun avec ses attributs : ceux des interfaces implémentées, des propriétés, des événements, des MethodImpl,
//     des paramètres génériques et de leurs contraintes, des champs ;
//   - une méthode : drapeaux, drapeaux d'implémentation, signature, paramètres (nom, drapeaux, constante, attributs),
//     P/Invoke, paramètres génériques (leurs attributs et leurs contraintes), attributs ; le corps : pile maximale,
//     initialisation des locales, locales, blocs d'exception, et l'IL décodé instruction par instruction, chaque
//     opérande résolu (méthode, champ, type, chaîne littérale, signature, cible de saut).
// Un attribut se vide par son constructeur et son blob brut, en hexadécimal (à l'octet).
// Ce qui n'est PAS vidé, parce que cela change d'une construction à l'autre sans changer le code : le MVID,
// l'horodatage et la somme du PE, le répertoire de débogage (le chemin du PDB), l'ordre des lignes des tables. Et le
// seul bruit connu, retiré et dit : le suffixe « +commit » d'AssemblyInformationalVersionAttribute, que le SDK pose
// d'après le commit de l'arbre ; les deux commits sont imprimés en tête de la comparaison.
//
// La table (tools/noms/g14.txt) est vérifiée contre les assemblages : chaque type ANCIEN doit être un type de premier
// niveau de REF, qu'il le définisse ou le référence (iXtal26.Diff référence les types d'iXtal26 ; un assemblage qui
// ne fait que référencer peut ne pas nommer tel type, des deux côtés, et c'est dit) ; avec comparer, chaque NOUVEAU
// doit être un type d'AUTRE, absent de REF, et l'ANCIEN absent d'AUTRE.
// Une entrée qui désigne un membre (le parent est un type de REF) est refusée : la table ne renomme que des types.
// Les entrées de fichiers ne concernent pas l'IL : elles sont comptées et ignorées.
//
// Retour : 0 identiques ; 1 des écarts (entrées changées, disparues ou apparues) ; 2 usage, lecture ou table
// refusée.

using System.Collections.Immutable;
using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text;
using iXtal26.Noms;

namespace iXtal26.IlDump;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            return Lancer(args);
        }
        catch (Exception e) when (e is FormatException or IOException or BadImageFormatException
                                      or InvalidOperationException)
        {
            Console.Error.WriteLine($"ildump : {e.Message}");
            return 2;
        }
    }

    private static int Usage()
    {
        Console.Error.WriteLine("usage : ildump vider DLL SORTIE [--noms TABLE [--jusqua ETAPE]]");
        Console.Error.WriteLine("        ildump comparer REF AUTRE [--noms TABLE [--jusqua ETAPE]] "
                                + "[--sorties DOSSIER]");
        return 2;
    }

    private static int Lancer(string[] args)
    {
        var pos = new List<string>();
        string? noms = null, jusqua = null, sorties = null;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--noms" when i + 1 < args.Length: noms = args[++i]; break;
                case "--jusqua" when i + 1 < args.Length: jusqua = args[++i]; break;
                case "--sorties" when i + 1 < args.Length: sorties = args[++i]; break;
                default:
                    if (args[i].StartsWith("--", StringComparison.Ordinal)) return Usage();
                    pos.Add(args[i]);
                    break;
            }
        }
        if (jusqua != null && noms == null) return Usage();
        var table = noms == null ? null : TableDesNoms.Lire(noms, jusqua);
        if (pos.Count == 3 && pos[0] == "vider")
        {
            using var r = new Assemblage(pos[1]);
            if (table != null) VerifierTable(table, r, null);
            var v = new Videur(r, table).Vider();
            Ecrire(v, pos[2]);
            Console.WriteLine($"  {v.Count} entrées, {Compter(v)} ; vidées dans {pos[2]}");
            return 0;
        }
        if (pos.Count == 3 && pos[0] == "comparer")
        {
            using var r = new Assemblage(pos[1]);
            using var a = new Assemblage(pos[2]);
            if (table != null) VerifierTable(table, r, a);
            var videurR = new Videur(r, table);
            var vr = videurR.Vider();
            var va = new Videur(a, null).Vider();
            if (sorties != null)
            {
                Directory.CreateDirectory(sorties);
                Ecrire(vr, Path.Combine(sorties, "ref.txt"));
                Ecrire(va, Path.Combine(sorties, "autre.txt"));
            }
            Console.WriteLine($"  commit de REF : {r.Commit} ; commit d'AUTRE : {a.Commit}");
            if (table != null)
                Console.WriteLine($"  table : {table.Types.Count} types et "
                                  + $"{table.Entrees.Count(e => e.EstFichier)} fichiers (ignorés par l'IL) ; "
                                  + $"{videurR.Reecrits} noms réécrits dans le vidage de REF");
            return Comparer(vr, va);
        }
        return Usage();
    }

    private static string Compter(SortedDictionary<string, List<string>> v)
    {
        var types = v.Keys.Count(k => k.StartsWith("type ", StringComparison.Ordinal));
        var methodes = v.Keys.Count(k => k.StartsWith("méthode ", StringComparison.Ordinal));
        var il = v.Values.Sum(l => l.Count(x => x.StartsWith("    IL_", StringComparison.Ordinal)));
        return $"{types} types, {methodes} méthodes, {il} instructions";
    }

    private static void Ecrire(SortedDictionary<string, List<string>> v, string chemin)
    {
        using var w = new StreamWriter(chemin, false, new UTF8Encoding(false));
        foreach (var (k, lignes) in v)
        {
            w.Write("== "); w.Write(k); w.Write('\n');
            foreach (var l in lignes) { w.Write(l); w.Write('\n'); }
        }
    }

    private static int Comparer(SortedDictionary<string, List<string>> r, SortedDictionary<string, List<string>> a)
    {
        var disparues = r.Keys.Where(k => !a.ContainsKey(k)).ToList();
        var apparues = a.Keys.Where(k => !r.ContainsKey(k)).ToList();
        var changees = r.Keys.Where(k => a.ContainsKey(k) && !r[k].SequenceEqual(a[k])).ToList();
        Console.WriteLine($"  {r.Count} entrées dans REF ({Compter(r)}), {a.Count} dans AUTRE ; {changees.Count} "
                          + $"changées, {disparues.Count} disparues, {apparues.Count} apparues");
        foreach (var k in changees)
        {
            Console.WriteLine($"  changée    {k}");
            var lr = r[k]; var la = a[k];
            var i = 0;
            while (i < lr.Count && i < la.Count && lr[i] == la[i]) i++;
            Console.WriteLine($"      REF   : {(i < lr.Count ? lr[i].Trim() : "(fin)")}");
            Console.WriteLine($"      AUTRE : {(i < la.Count ? la[i].Trim() : "(fin)")}");
        }
        foreach (var k in disparues) Console.WriteLine($"  disparue   {k}");
        foreach (var k in apparues) Console.WriteLine($"  apparue    {k}");
        return changees.Count + disparues.Count + apparues.Count == 0 ? 0 : 1;
    }

    /// <summary>La table contre les assemblages ; une incohérence la fait refuser (retour 2).</summary>
    private static void VerifierTable(TableDesNoms t, Assemblage r, Assemblage? a)
    {
        var typesR = r.TypesDePremierNiveau();
        var typesA = a?.TypesDePremierNiveau();
        // L'assemblage qui définit les types (iXtal26) est jugé strictement ; un assemblage qui ne fait que les
        // référencer (iXtal26.Diff) peut ne pas nommer tel type, des deux côtés : c'est dit, pas refusé.
        var definis = r.TypesDefinis();
        var strict = t.Types.Keys.Any(definis.Contains);
        var absents = new List<string>();
        foreach (var e in t.Entrees.Where(e => !e.EstFichier))
        {
            if (!strict && !typesR.Contains(e.Ancien) && (typesA == null || !typesA.Contains(e.Nouveau))
                && !typesR.Contains(e.Ancien[..e.Ancien.LastIndexOf('.')]))
            {
                absents.Add(e.Ancien);
                continue;
            }
            if (!typesR.Contains(e.Ancien))
            {
                var parent = e.Ancien[..e.Ancien.LastIndexOf('.')];
                if (typesR.Contains(parent))
                    throw new FormatException($"ligne {e.Ligne} : « {e.Ancien} » désigne un membre de {parent}, pas un "
                                              + "type : la table ne renomme que des types (décision n° 1)");
                throw new FormatException($"ligne {e.Ligne} : « {e.Ancien} » n'est pas un type de premier niveau "
                                          + $"de {r.Chemin}");
            }
            if (typesR.Contains(e.Nouveau))
                throw new FormatException($"ligne {e.Ligne} : « {e.Nouveau} » existe déjà dans {r.Chemin}");
            if (typesA == null) continue;
            if (typesA.Contains(e.Ancien))
                throw new FormatException($"ligne {e.Ligne} : « {e.Ancien} » est encore un type de {a!.Chemin} : la "
                                          + "table renomme ce qu'AUTRE garde");
            if (!typesA.Contains(e.Nouveau))
                throw new FormatException($"ligne {e.Ligne} : « {e.Nouveau} » n'est pas un type de {a!.Chemin}");
        }
        if (absents.Count > 0)
            Console.WriteLine($"  {r.Chemin} ne définit aucun type de la table ; il ne nomme pas "
                              + $"{absents.Count} de ses types, des deux côtés : " + string.Join(", ", absents));
    }
}

/// <summary>Un assemblage ouvert : le PE et ses métadonnées.</summary>
internal sealed class Assemblage : IDisposable
{
    private readonly FileStream _flux;
    public PEReader Pe { get; }
    public MetadataReader Md { get; }
    public string Chemin { get; }
    public string Commit { get; private set; } = "(aucun)";

    public Assemblage(string chemin)
    {
        Chemin = chemin;
        _flux = File.OpenRead(chemin);
        Pe = new PEReader(_flux);
        if (!Pe.HasMetadata) throw new BadImageFormatException($"{chemin} n'a pas de métadonnées .NET");
        Md = Pe.GetMetadataReader();
    }

    public void Dispose()
    {
        Pe.Dispose();
        _flux.Dispose();
    }

    public void NoterCommit(string c) => Commit = c;

    public HashSet<string> TypesDefinis()
    {
        var s = new HashSet<string>(StringComparer.Ordinal);
        foreach (var h in Md.TypeDefinitions)
        {
            var td = Md.GetTypeDefinition(h);
            if (!td.GetDeclaringType().IsNil) continue;
            var ns = Md.GetString(td.Namespace);
            s.Add(ns.Length == 0 ? Md.GetString(td.Name) : ns + "." + Md.GetString(td.Name));
        }
        return s;
    }

    public HashSet<string> TypesDePremierNiveau()
    {
        var s = new HashSet<string>(StringComparer.Ordinal);
        foreach (var h in Md.TypeDefinitions)
        {
            var td = Md.GetTypeDefinition(h);
            if (!td.GetDeclaringType().IsNil) continue;
            var ns = Md.GetString(td.Namespace);
            var n = Md.GetString(td.Name);
            s.Add(ns.Length == 0 ? n : ns + "." + n);
        }
        // Les types d'un autre assemblage qu'il référence (iXtal26.Diff référence iXtal26) : leur nom compte aussi.
        foreach (var h in Md.TypeReferences)
        {
            var tr = Md.GetTypeReference(h);
            if (tr.ResolutionScope.Kind == HandleKind.TypeReference) continue;
            var ns = Md.GetString(tr.Namespace);
            var n = Md.GetString(tr.Name);
            s.Add(ns.Length == 0 ? n : ns + "." + n);
        }
        return s;
    }
}

/// <summary>Le vidage d'un assemblage en entrées triées, la table appliquée aux noms de types.</summary>
internal sealed class Videur : ISignatureTypeProvider<string, object?>
{
    private static readonly OpCode[] Simples = new OpCode[0x100];
    private static readonly OpCode[] Doubles = new OpCode[0x100];

    static Videur()
    {
        foreach (var f in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (f.GetValue(null) is not OpCode o) continue;
            var v = (ushort)o.Value;
            if (o.Size == 1) Simples[v] = o; else Doubles[v & 0xFF] = o;
        }
    }

    private readonly Assemblage _a;
    private readonly MetadataReader _md;
    private readonly TableDesNoms? _table;
    private readonly SortedDictionary<string, List<string>> _sortie = new(StringComparer.Ordinal);

    public int Reecrits { get; private set; }

    public Videur(Assemblage a, TableDesNoms? table)
    {
        _a = a;
        _md = a.Md;
        _table = table;
    }

    public SortedDictionary<string, List<string>> Vider()
    {
        ViderAssemblage();
        foreach (var h in _md.TypeDefinitions) ViderType(h);
        return _sortie;
    }

    // ---- les noms ----

    private string NomType(TypeDefinitionHandle h)
    {
        var td = _md.GetTypeDefinition(h);
        var n = _md.GetString(td.Name);
        var d = td.GetDeclaringType();
        if (!d.IsNil) return NomType(d) + "+" + n;
        var ns = _md.GetString(td.Namespace);
        return Renommer(ns.Length == 0 ? n : ns + "." + n);
    }

    private string Renommer(string qualifie)
    {
        if (_table == null || !_table.Types.TryGetValue(qualifie, out var nouveau)) return qualifie;
        Reecrits++;
        return nouveau;
    }

    private string NomReference(TypeReferenceHandle h)
    {
        var tr = _md.GetTypeReference(h);
        var n = _md.GetString(tr.Name);
        var ns = _md.GetString(tr.Namespace);
        var portee = tr.ResolutionScope;
        if (portee.Kind == HandleKind.TypeReference) return NomReference((TypeReferenceHandle)portee) + "+" + n;
        var nom = Renommer(ns.Length == 0 ? n : ns + "." + n);
        return portee.Kind switch
        {
            HandleKind.AssemblyReference =>
                $"[{_md.GetString(_md.GetAssemblyReference((AssemblyReferenceHandle)portee).Name)}]{nom}",
            HandleKind.ModuleReference =>
                $"[module {_md.GetString(_md.GetModuleReference((ModuleReferenceHandle)portee).Name)}]{nom}",
            _ => nom,
        };
    }

    private string NomEntite(EntityHandle h) => h.Kind switch
    {
        HandleKind.TypeDefinition => NomType((TypeDefinitionHandle)h),
        HandleKind.TypeReference => NomReference((TypeReferenceHandle)h),
        HandleKind.TypeSpecification =>
            _md.GetTypeSpecification((TypeSpecificationHandle)h).DecodeSignature(this, null),
        HandleKind.MethodDefinition => NomMethode((MethodDefinitionHandle)h),
        HandleKind.MemberReference => NomMembre((MemberReferenceHandle)h),
        HandleKind.MethodSpecification => NomSpecialisation((MethodSpecificationHandle)h),
        HandleKind.FieldDefinition => NomChamp((FieldDefinitionHandle)h),
        HandleKind.StandaloneSignature => "sig " + Signature(_md.GetStandaloneSignature((StandaloneSignatureHandle)h)),
        _ => $"<{h.Kind} {MetadataTokens.GetToken(h):X8}>",
    };

    private string NomMethode(MethodDefinitionHandle h)
    {
        var m = _md.GetMethodDefinition(h);
        var s = m.DecodeSignature(this, null);
        return $"{NomType(m.GetDeclaringType())}::{_md.GetString(m.Name)}{Generique(s.GenericParameterCount)}"
               + $"({string.Join(", ", s.ParameterTypes)}) : {s.ReturnType}{Statique(s.Header)}";
    }

    private static string Generique(int n) => n == 0 ? "" : $"`{n}";
    private static string Statique(SignatureHeader h) => h.IsInstance ? "" : " [static]";

    private string NomChamp(FieldDefinitionHandle h)
    {
        var f = _md.GetFieldDefinition(h);
        return $"{NomType(f.GetDeclaringType())}::{_md.GetString(f.Name)} : {f.DecodeSignature(this, null)}";
    }

    private string NomMembre(MemberReferenceHandle h)
    {
        var m = _md.GetMemberReference(h);
        var parent = NomEntite(m.Parent);
        var nom = _md.GetString(m.Name);
        if (m.GetKind() == MemberReferenceKind.Field) return $"{parent}::{nom} : {m.DecodeFieldSignature(this, null)}";
        var s = m.DecodeMethodSignature(this, null);
        return $"{parent}::{nom}{Generique(s.GenericParameterCount)}({string.Join(", ", s.ParameterTypes)}) : "
               + $"{s.ReturnType}{Statique(s.Header)}";
    }

    private string NomSpecialisation(MethodSpecificationHandle h)
    {
        var ms = _md.GetMethodSpecification(h);
        return $"{NomEntite(ms.Method)}<{string.Join(", ", ms.DecodeSignature(this, null))}>";
    }

    private string Signature(StandaloneSignature s)
    {
        if (s.GetKind() == StandaloneSignatureKind.LocalVariables)
            return "locales (" + string.Join(", ", s.DecodeLocalSignature(this, null)) + ")";
        var m = s.DecodeMethodSignature(this, null);
        return $"{m.Header} ({string.Join(", ", m.ParameterTypes)}) : {m.ReturnType}";
    }

    // ---- ISignatureTypeProvider ----

    public string GetArrayType(string e, ArrayShape s) => $"{e}[{new string(',', s.Rank - 1)}]";
    public string GetByReferenceType(string e) => e + "&";
    public string GetFunctionPointerType(MethodSignature<string> s) =>
        $"method {s.ReturnType}*({string.Join(", ", s.ParameterTypes)})";
    public string GetGenericInstantiation(string g, ImmutableArray<string> a) => $"{g}<{string.Join(", ", a)}>";
    public string GetGenericMethodParameter(object? c, int i) => "!!" + i;
    public string GetGenericTypeParameter(object? c, int i) => "!" + i;
    public string GetModifiedType(string m, string u, bool requis) => $"{u} {(requis ? "modreq" : "modopt")}({m})";
    public string GetPinnedType(string e) => e + " pinned";
    public string GetPointerType(string e) => e + "*";
    public string GetPrimitiveType(PrimitiveTypeCode c) => c.ToString();
    public string GetSZArrayType(string e) => e + "[]";
    public string GetTypeFromDefinition(MetadataReader r, TypeDefinitionHandle h, byte k) => NomType(h);
    public string GetTypeFromReference(MetadataReader r, TypeReferenceHandle h, byte k) => NomReference(h);
    public string GetTypeFromSpecification(MetadataReader r, object? c, TypeSpecificationHandle h, byte k) =>
        _md.GetTypeSpecification(h).DecodeSignature(this, c);

    // ---- les entrées ----

    private List<string> Entree(string cle)
    {
        if (_sortie.ContainsKey(cle)) throw new InvalidOperationException($"clé en double : {cle}");
        var l = new List<string>();
        _sortie[cle] = l;
        return l;
    }

    private string Blob(BlobHandle h) => h.IsNil ? "-" : Convert.ToHexString(_md.GetBlobBytes(h));

    private void Attributs(List<string> l, string indent, CustomAttributeHandleCollection c)
    {
        var a = new List<string>();
        foreach (var h in c)
        {
            var ca = _md.GetCustomAttribute(h);
            var ctor = NomEntite(ca.Constructor);
            var valeur = Blob(ca.Value);
            if (ctor.Contains("AssemblyInformationalVersionAttribute", StringComparison.Ordinal))
                valeur = SansCommit(ca.Value);
            a.Add($"{indent}attribut {ctor} = {valeur}");
        }
        a.Sort(StringComparer.Ordinal);
        l.AddRange(a);
    }

    /// <summary>Le seul bruit connu : « 1.0.0+commit » ; le commit est noté, puis retiré.</summary>
    private string SansCommit(BlobHandle h)
    {
        var r = _md.GetBlobReader(h);
        r.ReadUInt16();
        var s = r.ReadSerializedString() ?? "";
        var plus = s.IndexOf('+');
        if (plus >= 0)
        {
            _a.NoterCommit(s[(plus + 1)..]);
            s = s[..plus] + "+(commit retiré)";
        }
        return "« " + s + " »";
    }

    private string Constante(ConstantHandle h)
    {
        if (h.IsNil) return "";
        var c = _md.GetConstant(h);
        var b = _md.GetBlobReader(c.Value);
        string v = c.TypeCode switch
        {
            ConstantTypeCode.Boolean => b.ReadBoolean().ToString(),
            ConstantTypeCode.Char => ((int)b.ReadChar()).ToString(CultureInfo.InvariantCulture),
            ConstantTypeCode.SByte => b.ReadSByte().ToString(CultureInfo.InvariantCulture),
            ConstantTypeCode.Byte => b.ReadByte().ToString(CultureInfo.InvariantCulture),
            ConstantTypeCode.Int16 => b.ReadInt16().ToString(CultureInfo.InvariantCulture),
            ConstantTypeCode.UInt16 => b.ReadUInt16().ToString(CultureInfo.InvariantCulture),
            ConstantTypeCode.Int32 => b.ReadInt32().ToString(CultureInfo.InvariantCulture),
            ConstantTypeCode.UInt32 => b.ReadUInt32().ToString(CultureInfo.InvariantCulture),
            ConstantTypeCode.Int64 => b.ReadInt64().ToString(CultureInfo.InvariantCulture),
            ConstantTypeCode.UInt64 => b.ReadUInt64().ToString(CultureInfo.InvariantCulture),
            ConstantTypeCode.Single => BitConverter.SingleToInt32Bits(b.ReadSingle()).ToString("X8"),
            ConstantTypeCode.Double => BitConverter.DoubleToInt64Bits(b.ReadDouble()).ToString("X16"),
            ConstantTypeCode.String => Litteral(b.ReadUTF16(b.Length)),
            _ => Blob(c.Value),
        };
        return $" = {c.TypeCode} {v}";
    }

    private static string Litteral(string s)
    {
        var sb = new StringBuilder("\"");
        foreach (var ch in s)
        {
            if (ch == '"' || ch == '\\') sb.Append('\\').Append(ch);
            else if (ch < 0x20 || ch > 0x7E) sb.Append($"\\u{(int)ch:X4}");
            else sb.Append(ch);
        }
        return sb.Append('"').ToString();
    }

    private void ViderAssemblage()
    {
        var l = Entree("assemblage");
        if (_md.IsAssembly)
        {
            var ad = _md.GetAssemblyDefinition();
            l.Add($"  nom {_md.GetString(ad.Name)} {ad.Version} culture={_md.GetString(ad.Culture)} "
                  + $"drapeaux={ad.Flags} algorithme={ad.HashAlgorithm} clé={Blob(ad.PublicKey)}");
            Attributs(l, "  ", ad.GetCustomAttributes());
        }
        var mod = _md.GetModuleDefinition();
        l.Add($"  module {_md.GetString(mod.Name)} génération={mod.Generation}");
        Attributs(l, "  module ", mod.GetCustomAttributes());
        var refs = new List<List<string>>();
        foreach (var h in _md.AssemblyReferences)
        {
            var r = _md.GetAssemblyReference(h);
            var x = new List<string>
            {
                $"  référence {_md.GetString(r.Name)} {r.Version} culture={_md.GetString(r.Culture)} "
                + $"drapeaux={r.Flags} clé={Blob(r.PublicKeyOrToken)}",
            };
            Attributs(x, "    ", r.GetCustomAttributes());
            refs.Add(x);
        }
        for (var i = 1; i <= _md.GetTableRowCount(TableIndex.ModuleRef); i++)
        {
            var mr = _md.GetModuleReference(MetadataTokens.ModuleReferenceHandle(i));
            var x = new List<string> { "  module externe " + _md.GetString(mr.Name) };
            Attributs(x, "    ", mr.GetCustomAttributes());
            refs.Add(x);
        }
        foreach (var h in _md.ManifestResources)
        {
            var r = _md.GetManifestResource(h);
            var x = new List<string>
            {
                $"  ressource {_md.GetString(r.Name)} {r.Attributes} "
                + (r.Implementation.IsNil ? Ressource(r.Offset) : "externe " + r.Implementation.Kind),
            };
            Attributs(x, "    ", r.GetCustomAttributes());
            refs.Add(x);
        }
        Blocs(l, refs);
        var ep = _a.Pe.PEHeaders.CorHeader!.EntryPointTokenOrRelativeVirtualAddress;
        l.Add($"  point d'entrée {(ep == 0 ? "-" : NomEntite(MetadataTokens.EntityHandle(ep)))}");
    }

    /// <summary>Une ressource incluse : sa longueur et son sha256.</summary>
    private string Ressource(long decalage)
    {
        var dir = _a.Pe.PEHeaders.CorHeader!.ResourcesDirectory;
        var bloc = _a.Pe.GetSectionData(dir.RelativeVirtualAddress);
        var r = bloc.GetReader((int)decalage, bloc.Length - (int)decalage);
        var n = r.ReadInt32();
        var octets = r.ReadBytes(n);
        return $"{n} octets sha256 {Convert.ToHexString(SHA256.HashData(octets))[..16]}";
    }

    private void ViderType(TypeDefinitionHandle h)
    {
        var td = _md.GetTypeDefinition(h);
        var nom = NomType(h);
        var l = Entree("type " + nom);
        l.Add($"  drapeaux {td.Attributes}");
        l.Add($"  base {(td.BaseType.IsNil ? "-" : NomEntite(td.BaseType))}");
        var lay = td.GetLayout();
        if (!lay.IsDefault) l.Add($"  disposition paquet={lay.PackingSize} taille={lay.Size}");
        var itf = new List<List<string>>();
        foreach (var ih in td.GetInterfaceImplementations())
        {
            var ii = _md.GetInterfaceImplementation(ih);
            var b = new List<string> { "  interface " + NomEntite(ii.Interface) };
            Attributs(b, "    ", ii.GetCustomAttributes());
            itf.Add(b);
        }
        Blocs(l, itf);
        Generiques(l, td.GetGenericParameters());
        Attributs(l, "  ", td.GetCustomAttributes());
        foreach (var fh in td.GetFields())
        {
            var f = _md.GetFieldDefinition(fh);
            var ligne = $"  champ {_md.GetString(f.Name)} : {f.DecodeSignature(this, null)} [{f.Attributes}]"
                        + Constante(f.GetDefaultValue());
            var off = f.GetOffset();
            if (off >= 0) ligne += $" décalage={off}";
            if (!f.GetMarshallingDescriptor().IsNil) ligne += $" marshal={Blob(f.GetMarshallingDescriptor())}";
            var rva = f.GetRelativeVirtualAddress();
            if (rva != 0) ligne += $" données={DonneesRva(f, rva)}";
            l.Add(ligne);
            Attributs(l, "    ", f.GetCustomAttributes());
        }
        var props = new List<List<string>>();
        foreach (var ph in td.GetProperties())
        {
            var p = _md.GetPropertyDefinition(ph);
            var acc = p.GetAccessors();
            var s = p.DecodeSignature(this, null);
            var b = new List<string>
            {
                $"  propriété {_md.GetString(p.Name)} : {s.ReturnType}({string.Join(", ", s.ParameterTypes)}) "
                + $"[{p.Attributes}] get={Acc(acc.Getter)} set={Acc(acc.Setter)}" + Constante(p.GetDefaultValue()),
            };
            Attributs(b, "    ", p.GetCustomAttributes());
            props.Add(b);
        }
        foreach (var eh in td.GetEvents())
        {
            var e = _md.GetEventDefinition(eh);
            var acc = e.GetAccessors();
            var b = new List<string>
            {
                $"  événement {_md.GetString(e.Name)} : {NomEntite(e.Type)} [{e.Attributes}] "
                + $"add={Acc(acc.Adder)} remove={Acc(acc.Remover)} raise={Acc(acc.Raiser)}",
            };
            Attributs(b, "    ", e.GetCustomAttributes());
            props.Add(b);
        }
        foreach (var mi in td.GetMethodImplementations())
        {
            var m = _md.GetMethodImplementation(mi);
            var b = new List<string> { $"  methodimpl {NomEntite(m.MethodBody)} <- {NomEntite(m.MethodDeclaration)}" };
            Attributs(b, "    ", m.GetCustomAttributes());
            props.Add(b);
        }
        Blocs(l, props);
        foreach (var mh in td.GetMethods()) ViderMethode(mh);
    }

    /// <summary>Des blocs (une ligne d'en-tête, puis ses attributs), triés par leur texte, puis aplatis.</summary>
    private static void Blocs(List<string> l, List<List<string>> blocs)
    {
        blocs.Sort((a, b) => string.CompareOrdinal(string.Join("\n", a), string.Join("\n", b)));
        foreach (var b in blocs) l.AddRange(b);
    }

    private string Acc(MethodDefinitionHandle h) => h.IsNil ? "-" : _md.GetString(_md.GetMethodDefinition(h).Name);

    private void Generiques(List<string> l, GenericParameterHandleCollection c)
    {
        foreach (var gh in c)
        {
            var g = _md.GetGenericParameter(gh);
            var cs = new List<List<string>>();
            foreach (var ch in g.GetConstraints())
            {
                var gc = _md.GetGenericParameterConstraint(ch);
                var b = new List<string> { "    contrainte " + NomEntite(gc.Type) };
                Attributs(b, "      ", gc.GetCustomAttributes());
                cs.Add(b);
            }
            l.Add($"  générique {g.Index} {_md.GetString(g.Name)} [{g.Attributes}]");
            Attributs(l, "    ", g.GetCustomAttributes());
            Blocs(l, cs);
        }
    }

    /// <summary>Le sha256 des données d'un champ à RVA (les tableaux initialisés), sur la taille de son type.</summary>
    private string DonneesRva(FieldDefinition f, int rva)
    {
        var r = _md.GetBlobReader(f.Signature);
        r.ReadSignatureHeader();
        var code = r.ReadSignatureTypeCode();
        int taille = code switch
        {
            SignatureTypeCode.Boolean or SignatureTypeCode.SByte or SignatureTypeCode.Byte => 1,
            SignatureTypeCode.Char or SignatureTypeCode.Int16 or SignatureTypeCode.UInt16 => 2,
            SignatureTypeCode.Int32 or SignatureTypeCode.UInt32 or SignatureTypeCode.Single => 4,
            SignatureTypeCode.Int64 or SignatureTypeCode.UInt64 or SignatureTypeCode.Double => 8,
            _ => -1,
        };
        if (code == SignatureTypeCode.TypeHandle)
        {
            var th = r.ReadTypeHandle();
            if (th.Kind == HandleKind.TypeDefinition)
                taille = _md.GetTypeDefinition((TypeDefinitionHandle)th).GetLayout().Size;
        }
        if (taille <= 0) return $"rva sans taille connue ({code})";
        var octets = _a.Pe.GetSectionData(rva).GetContent(0, taille);
        return $"{taille} octets sha256 {Convert.ToHexString(SHA256.HashData(octets.AsSpan()))[..16]}";
    }

    private void ViderMethode(MethodDefinitionHandle h)
    {
        var m = _md.GetMethodDefinition(h);
        var l = Entree("méthode " + NomMethode(h));
        l.Add($"  drapeaux {m.Attributes} impl={m.ImplAttributes}");
        var imp = m.GetImport();
        if (!imp.Module.IsNil)
            l.Add($"  pinvoke {_md.GetString(_md.GetModuleReference(imp.Module).Name)}!{_md.GetString(imp.Name)} "
                  + $"[{imp.Attributes}]");
        Generiques(l, m.GetGenericParameters());
        Attributs(l, "  ", m.GetCustomAttributes());
        foreach (var ph in m.GetParameters())
        {
            var p = _md.GetParameter(ph);
            l.Add($"  paramètre {p.SequenceNumber} {_md.GetString(p.Name)} [{p.Attributes}]"
                  + Constante(p.GetDefaultValue())
                  + (p.GetMarshallingDescriptor().IsNil ? "" : $" marshal={Blob(p.GetMarshallingDescriptor())}"));
            Attributs(l, "    ", p.GetCustomAttributes());
        }
        if (m.RelativeVirtualAddress == 0) return;
        var corps = _a.Pe.GetMethodBody(m.RelativeVirtualAddress);
        l.Add($"  pile {corps.MaxStack} initlocales={corps.LocalVariablesInitialized}");
        if (!corps.LocalSignature.IsNil)
            l.Add("  " + Signature(_md.GetStandaloneSignature(corps.LocalSignature)));
        foreach (var x in corps.ExceptionRegions)
        {
            var quoi = x.Kind switch
            {
                ExceptionRegionKind.Catch => "catch " + NomEntite(x.CatchType),
                ExceptionRegionKind.Filter => $"filtre IL_{x.FilterOffset:X4}",
                _ => x.Kind.ToString(),
            };
            l.Add($"  bloc try IL_{x.TryOffset:X4}+{x.TryLength} gestionnaire IL_{x.HandlerOffset:X4}"
                  + $"+{x.HandlerLength} {quoi}");
        }
        Il(l, corps.GetILReader());
    }

    private void Il(List<string> l, BlobReader r)
    {
        while (r.RemainingBytes > 0)
        {
            var off = r.Offset;
            int b = r.ReadByte();
            OpCode o;
            if (b == 0xFE) o = Doubles[r.ReadByte()];
            else o = Simples[b];
            if (o.Size == 0) throw new BadImageFormatException($"opcode inconnu {b:X2} à IL_{off:X4}");
            string arg = o.OperandType switch
            {
                OperandType.InlineNone => "",
                OperandType.ShortInlineBrTarget => Cible(r.ReadSByte(), r.Offset),
                OperandType.InlineBrTarget => Cible(r.ReadInt32(), r.Offset),
                OperandType.ShortInlineI => r.ReadSByte().ToString(CultureInfo.InvariantCulture),
                OperandType.ShortInlineVar => r.ReadByte().ToString(CultureInfo.InvariantCulture),
                OperandType.InlineVar => r.ReadUInt16().ToString(CultureInfo.InvariantCulture),
                OperandType.InlineI => r.ReadInt32().ToString(CultureInfo.InvariantCulture),
                OperandType.InlineI8 => r.ReadInt64().ToString(CultureInfo.InvariantCulture),
                OperandType.ShortInlineR => BitConverter.SingleToInt32Bits(r.ReadSingle()).ToString("X8"),
                OperandType.InlineR => BitConverter.DoubleToInt64Bits(r.ReadDouble()).ToString("X16"),
                OperandType.InlineString =>
                    Litteral(_md.GetUserString(MetadataTokens.UserStringHandle(r.ReadInt32() & 0xFFFFFF))),
                OperandType.InlineSwitch => Aiguillage(ref r),
                OperandType.InlineField or OperandType.InlineMethod or OperandType.InlineType
                    or OperandType.InlineTok or OperandType.InlineSig =>
                    NomEntite(MetadataTokens.EntityHandle(r.ReadInt32())),
                _ => throw new BadImageFormatException($"opérande inconnu {o.OperandType}"),
            };
            l.Add(arg.Length == 0 ? $"    IL_{off:X4}: {o.Name}" : $"    IL_{off:X4}: {o.Name} {arg}");
        }
    }

    private static string Cible(int delta, int apres) => $"IL_{apres + delta:X4}";

    private static string Aiguillage(ref BlobReader r)
    {
        var n = r.ReadInt32();
        var d = new int[n];
        for (var i = 0; i < n; i++) d[i] = r.ReadInt32();
        var base_ = r.Offset;
        return "(" + string.Join(", ", d.Select(x => $"IL_{base_ + x:X4}")) + ")";
    }
}
