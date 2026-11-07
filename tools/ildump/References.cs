// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — outillage de la preuve des membres (G15.0b2, PLAN-G15.md § G15.0b ; le relevé du code mort de
// G15.1, Q47).
//
// LE RELEVÉ DES RÉFÉRENCES : ildump references DLL [DLL…] [--faux-positifs FICHIER]
//
// Les méthodes et les champs que la PREMIÈRE DLL définit (iXtal26), et ceux que l'IL des DLL données (elle-même,
// iXtal26.Diff, le banc) nomme. Une méthode est référencée si une instruction la nomme (call, callvirt, newobj,
// ldftn, ldvirtftn, jmp, ldtoken), hors de son propre corps, ou si un attribut nomme son constructeur ; un champ est
// LU s'il est chargé (ldfld, ldsfld, leurs adresses, ldtoken), où que ce soit, ÉCRIT sinon (et compté à part quand
// l'écriture est dans un constructeur, d'instance ou statique, de la DLL qui définit). Dans la DLL qui définit, un
// jeton de définition désigne la définition elle-même ; une référence (MemberRef : un membre d'un type générique, ou
// d'une autre DLL) vaut pour toutes les surcharges de son nom, la méthode elle-même comprise (une borne : le relevé
// sous-compte ce qui est mort, il ne le surcompte pas ; une récursion dans un type générique n'est pas vue). Les
// interfaces suivies pour l'implémentation sont celles que le type nomme directement.
// Ne sont pas candidats : ce que le compilateur déclare (un nom en « < », ou [CompilerGenerated] : les membres d'un
// record, les accesseurs d'une propriété automatique), les constructeurs statiques, les méthodes
// virtuelles ou abstraites (un override, une implémentation d'interface ; et l'implémentation d'un membre d'une
// interface de l'assemblage, static abstract compris, par son nom), celles que le runtime implémente (Invoke
// d'un délégué), le point d'entrée ; les constantes (le compilateur les recopie : l'IL ne les nomme jamais), les
// champs d'un enum, les champs à RVA.
// CE QUE L'IL NE VOIT PAS, et qui fait des faux positifs : la réflexion (GetMethod("ILLEGAL"), GetField("memcycs")…),
// les méthodes que BenchmarkDotNet appelle par ses attributs, les appels sous une constante fausse ou un #if (le
// compilateur ne les émet pas). FICHIER les nomme, « Type::nom  raison » par ligne (# commente) : ils sont imprimés
// à part, avec leur raison ; une entrée qui ne désigne aucun candidat est dite.
// Retour 0 ; 2 usage ou lecture.

using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

namespace iXtal26.IlDump;

internal static class References
{
    private static readonly OpCode[] Simples = new OpCode[0x100];
    private static readonly OpCode[] Doubles = new OpCode[0x100];

    static References()
    {
        foreach (var f in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (f.GetValue(null) is not OpCode o) continue;
            var v = (ushort)o.Value;
            if (o.Size == 1) Simples[v] = o; else Doubles[v & 0xFF] = o;
        }
    }

    private static readonly HashSet<string> Lectures = new(StringComparer.Ordinal)
        { "ldfld", "ldsfld", "ldflda", "ldsflda", "ldtoken" };

    private sealed class Def(string type, string nom, string cle)
    {
        public string Type { get; } = type;
        public string Nom { get; } = nom;
        public string Cle { get; } = cle;
        public bool Candidat { get; set; } = true;
        public int Refs { get; set; }
        public int Lus { get; set; }
        public int Ecrits { get; set; }
        public int EcritsInit { get; set; }
    }

    public static int Lancer(List<string> dlls, string? fauxPositifs)
    {
        var faux = new Dictionary<string, string>(StringComparer.Ordinal);
        if (fauxPositifs != null)
            foreach (var brute in File.ReadLines(fauxPositifs))
            {
                var l = brute.Trim();
                if (l.Length == 0 || l.StartsWith('#')) continue;
                var i = l.IndexOfAny([' ', '\t']);
                if (i < 0) throw new FormatException($"{fauxPositifs} : « {l} » sans raison");
                if (!faux.TryAdd(l[..i], l[i..].Trim()))
                    throw new FormatException($"{fauxPositifs} : « {l[..i]} » en double");
            }
        var assemblages = dlls.Select(d => new Assemblage(d)).ToList();
        try
        {
            var a0 = assemblages[0];
            var md = a0.Md;
            var nomAsm = md.GetString(md.GetAssemblyDefinition().Name);
            string NomType(TypeDefinitionHandle h)
            {
                var td = md.GetTypeDefinition(h);
                var d = td.GetDeclaringType();
                if (!d.IsNil) return NomType(d) + "+" + md.GetString(td.Name);
                var ns = md.GetString(td.Namespace);
                return ns.Length == 0 ? md.GetString(td.Name) : ns + "." + md.GetString(td.Name);
            }
            var methodes = new Dictionary<MethodDefinitionHandle, Def>();
            var champs = new Dictionary<FieldDefinitionHandle, Def>();
            var parNom = new Dictionary<string, List<Def>>(StringComparer.Ordinal);
            var ep = a0.Pe.PEHeaders.CorHeader!.EntryPointTokenOrRelativeVirtualAddress;
            foreach (var th in md.TypeDefinitions)
            {
                var td = md.GetTypeDefinition(th);
                var tn = NomType(th);
                // Les membres d'une interface de l'assemblage que le type implémente : leurs implémentations (un membre
                // static abstract s'appelle par le paramètre de type, !!T::Op8, que l'IL ne lie pas à la structure).
                var dInterface = new HashSet<string>(StringComparer.Ordinal);
                foreach (var ih in td.GetInterfaceImplementations())
                {
                    var it = md.GetInterfaceImplementation(ih).Interface;
                    if (it.Kind == HandleKind.TypeSpecification)
                    {
                        var rs = md.GetBlobReader(md.GetTypeSpecification((TypeSpecificationHandle)it).Signature);
                        if (rs.ReadSignatureTypeCode() == SignatureTypeCode.GenericTypeInstance)
                        {
                            rs.ReadSignatureTypeCode();
                            it = rs.ReadTypeHandle();
                        }
                    }
                    if (it.Kind != HandleKind.TypeDefinition) continue;
                    foreach (var imh in md.GetTypeDefinition((TypeDefinitionHandle)it).GetMethods())
                        dInterface.Add(md.GetString(md.GetMethodDefinition(imh).Name));
                }
                var genereType = tn.Contains('<');
                var enumeration = !td.BaseType.IsNil && td.BaseType.Kind == HandleKind.TypeReference
                                  && md.GetString(md.GetTypeReference((TypeReferenceHandle)td.BaseType).Name) == "Enum";
                foreach (var mh in td.GetMethods())
                {
                    var m = md.GetMethodDefinition(mh);
                    var n = md.GetString(m.Name);
                    var d = new Def(tn, n, $"{tn}::{n}");
                    var synthetisee = m.GetCustomAttributes()
                                       .Any(ca => NomAttribut(md, ca) == "CompilerGeneratedAttribute");
                    d.Candidat = !genereType && !synthetisee && !n.Contains('<') && n != ".cctor"
                                 && (m.Attributes & (MethodAttributes.Virtual | MethodAttributes.Abstract)) == 0
                                 && (m.ImplAttributes & MethodImplAttributes.Runtime) == 0
                                 && MetadataTokens.GetToken(mh) != ep && !dInterface.Contains(n);
                    methodes[mh] = d;
                    Ajouter(parNom, d);
                }
                foreach (var fh in td.GetFields())
                {
                    var f = md.GetFieldDefinition(fh);
                    var n = md.GetString(f.Name);
                    var d = new Def(tn, n, $"{tn}::{n}");
                    d.Candidat = !genereType && !n.Contains('<') && !enumeration
                                 && (f.Attributes & FieldAttributes.Literal) == 0 && f.GetRelativeVirtualAddress() == 0;
                    champs[fh] = d;
                    Ajouter(parNom, d);
                }
            }

            var defsChamps = champs.Values.ToHashSet();
            foreach (var a in assemblages)
            {
                var m2 = a.Md;
                var meme = ReferenceEquals(a, a0);
                string? NomRef(EntityHandle parent)
                {
                    switch (parent.Kind)
                    {
                        case HandleKind.TypeReference:
                        {
                            var tr = m2.GetTypeReference((TypeReferenceHandle)parent);
                            var n = m2.GetString(tr.Name);
                            if (tr.ResolutionScope.Kind == HandleKind.TypeReference)
                                return NomRef((TypeReferenceHandle)tr.ResolutionScope) is { } p ? p + "+" + n : null;
                            if (tr.ResolutionScope.Kind != HandleKind.AssemblyReference
                                || m2.GetString(m2.GetAssemblyReference((AssemblyReferenceHandle)tr.ResolutionScope)
                                       .Name) != nomAsm) return null;
                            var ns = m2.GetString(tr.Namespace);
                            return ns.Length == 0 ? n : ns + "." + n;
                        }
                        case HandleKind.TypeDefinition when meme:
                            return NomType((TypeDefinitionHandle)parent);
                        case HandleKind.TypeSpecification:
                        {
                            var r = m2.GetBlobReader(m2.GetTypeSpecification((TypeSpecificationHandle)parent)
                                                       .Signature);
                            if (r.ReadSignatureTypeCode() != SignatureTypeCode.GenericTypeInstance) return null;
                            r.ReadSignatureTypeCode();
                            return NomRef(r.ReadTypeHandle());
                        }
                        default: return null;
                    }
                }
                // Les définitions qu'un jeton désigne (une, ou toutes les surcharges d'un nom).
                IEnumerable<Def> Cibles(EntityHandle h)
                {
                    switch (h.Kind)
                    {
                        case HandleKind.MethodDefinition when meme:
                            return [methodes[(MethodDefinitionHandle)h]];
                        case HandleKind.FieldDefinition when meme:
                            return [champs[(FieldDefinitionHandle)h]];
                        case HandleKind.MethodSpecification:
                            return Cibles(m2.GetMethodSpecification((MethodSpecificationHandle)h).Method);
                        case HandleKind.MemberReference:
                        {
                            var mr = m2.GetMemberReference((MemberReferenceHandle)h);
                            var t = NomRef(mr.Parent);
                            if (t == null) return [];
                            return parNom.GetValueOrDefault($"{t}::{m2.GetString(mr.Name)}") ?? [];
                        }
                        default: return [];
                    }
                }
                foreach (var th in m2.TypeDefinitions)
                {
                    foreach (var mh in m2.GetTypeDefinition(th).GetMethods())
                    {
                        var m = m2.GetMethodDefinition(mh);
                        var soi = meme ? methodes[mh] : null;
                        var init = m2.GetString(m.Name) is ".cctor" or ".ctor";
                        if (m.RelativeVirtualAddress == 0) continue;
                        var r = a.Pe.GetMethodBody(m.RelativeVirtualAddress).GetILReader();
                        while (r.RemainingBytes > 0)
                        {
                            int b = r.ReadByte();
                            var o = b == 0xFE ? Doubles[r.ReadByte()] : Simples[b];
                            if (o.Size == 0) throw new BadImageFormatException($"opcode inconnu {b:X2}");
                            switch (o.OperandType)
                            {
                                case OperandType.InlineMethod or OperandType.InlineField or OperandType.InlineTok:
                                {
                                    var h = MetadataTokens.EntityHandle(r.ReadInt32());
                                    foreach (var d in Cibles(h))
                                    {
                                        if (ReferenceEquals(d, soi)) continue;
                                        d.Refs++;
                                        if (defsChamps.Contains(d))
                                        {
                                            if (Lectures.Contains(o.Name!)) d.Lus++;
                                            else if (init && meme) d.EcritsInit++;
                                            else d.Ecrits++;
                                        }
                                    }
                                    break;
                                }
                                case OperandType.InlineSwitch:
                                {
                                    var n = r.ReadInt32();
                                    r.Offset += 4 * n;
                                    break;
                                }
                                default:
                                    r.Offset += Taille(o.OperandType);
                                    break;
                            }
                        }
                    }
                }
                // Les constructeurs que nomment les attributs.
                foreach (var ch in m2.CustomAttributes)
                    foreach (var d in Cibles(m2.GetCustomAttribute(ch).Constructor)) d.Refs++;
            }

            var mortes = methodes.Values.Where(d => d.Candidat && d.Refs == 0).OrderBy(d => d.Cle).ToList();
            var jamais = champs.Values.Where(d => d.Candidat && d.Refs == 0).OrderBy(d => d.Cle).ToList();
            var nonLus = champs.Values.Where(d => d.Candidat && d.Refs > 0 && d.Lus == 0).OrderBy(d => d.Cle).ToList();
            Console.WriteLine($"  {nomAsm} : {methodes.Count} méthodes ({methodes.Values.Count(d => d.Candidat)} "
                              + $"candidates), {champs.Count} champs ({champs.Values.Count(d => d.Candidat)} "
                              + $"candidats) ; l'IL de {string.Join(", ", dlls.Select(Path.GetFileName))}");
            var servis = new HashSet<string>(StringComparer.Ordinal);
            void Imprimer(string titre, List<Def> l, Func<Def, string> detail)
            {
                var connus = l.Where(d => faux.ContainsKey(d.Cle)).ToList();
                Console.WriteLine($"  {titre} : {l.Count}, dont {connus.Count} faux positifs connus");
                foreach (var d in l)
                {
                    if (faux.TryGetValue(d.Cle, out var raison))
                    {
                        servis.Add(d.Cle);
                        Console.WriteLine($"    connu    {d.Cle}{detail(d)} — {raison}");
                    }
                    else Console.WriteLine($"    {d.Cle}{detail(d)}");
                }
            }
            Imprimer("méthodes jamais nommées par l'IL", mortes, _ => "");
            Imprimer("champs jamais nommés par l'IL", jamais, _ => "");
            Imprimer("champs écrits, jamais lus", nonLus,
                     d => d.Ecrits == 0 ? $"  (écrit seulement dans des constructeurs : {d.EcritsInit})"
                         : $"  ({d.Ecrits} écritures hors des constructeurs, {d.EcritsInit} dedans)");
            foreach (var k in faux.Keys.Where(k => !servis.Contains(k)).OrderBy(k => k, StringComparer.Ordinal))
                Console.WriteLine($"  faux positif sans candidat (référencé, ou inconnu) : {k}");
            return 0;
        }
        finally
        {
            foreach (var a in assemblages) a.Dispose();
        }
    }

    /// <summary>Le nom du type d'un attribut (son constructeur, défini ou référencé).</summary>
    private static string NomAttribut(MetadataReader md, CustomAttributeHandle h)
    {
        var c = md.GetCustomAttribute(h).Constructor;
        EntityHandle t = c.Kind switch
        {
            HandleKind.MemberReference => md.GetMemberReference((MemberReferenceHandle)c).Parent,
            HandleKind.MethodDefinition => md.GetMethodDefinition((MethodDefinitionHandle)c).GetDeclaringType(),
            _ => default,
        };
        return t.Kind switch
        {
            HandleKind.TypeReference => md.GetString(md.GetTypeReference((TypeReferenceHandle)t).Name),
            HandleKind.TypeDefinition => md.GetString(md.GetTypeDefinition((TypeDefinitionHandle)t).Name),
            _ => "",
        };
    }

    private static void Ajouter(Dictionary<string, List<Def>> d, Def x)
    {
        if (!d.TryGetValue(x.Cle, out var l)) d[x.Cle] = l = [];
        l.Add(x);
    }

    private static int Taille(OperandType t) => t switch
    {
        OperandType.InlineNone => 0,
        OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
        OperandType.InlineVar => 2,
        OperandType.InlineI8 or OperandType.InlineR => 8,
        _ => 4,
    };
}
