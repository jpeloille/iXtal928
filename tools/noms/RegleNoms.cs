// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — outillage de la preuve des membres (G15.0b, PLAN-G15.md § G15.0b ; Q40).
//
// LA RÈGLE DE CONVERSION de Q40 (tranchée le 04/11 par sa proposition), appliquée à un identifiant, sans rien savoir
// de ce qu'il désigne : le générateur de tools/renommage choisit PascalCase ou camelCase selon le genre du membre, et
// ses exceptions écrites passent avant elle. Partagée par tools/renommage, qui l'inclut par <Compile Include>.
//
// Les mots d'un identifiant : coupé à chaque « _ » ; puis, dans chaque morceau, avant une majuscule qui suit une
// minuscule ou un chiffre (opADD → op ADD), et avant la dernière majuscule d'une suite de majuscules suivie d'une
// minuscule (HTTPServer → HTTP Server). Un chiffre reste dans le mot qui le précède (x86seg, getr8 : un mot) ; après
// un « _ », il fait un mot à lui seul (fetch_ea_16 : fetch, ea, 16). Chaque mot s'écrit ensuite :
//   - tout en majuscules et de deux lettres, il le reste (CS, AL, IO : le sigle de deux lettres de Q40 ; les chiffres
//     ne comptent pas, ST0 reste ST0) ;
//   - tout en majuscules et plus long, une majuscule suivie de minuscules (FDC → Fdc, PIT → Pit, ADD → Add) ;
//   - sinon, sa première lettre en majuscule, le reste tel quel (rmw → Rmw, Table → Table, x86seg → X86seg).
// PascalCase est la suite des mots ainsi écrits. camelCase est la même suite, son premier mot en minuscules (un sigle
// de deux lettres compris : CS_base → csBase). Un identifiant échappé (@char) perd son « @ » : le mot n'est plus un
// mot-clé une fois capitalisé ; s'il l'est encore en camelCase, le générateur le refuse.
// Ce que la règle ne fait pas, et que le générateur refuse ou laisse aux exceptions : un résultat qui commence par un
// chiffre (_808x → 808x), un résultat vide (« _ »), une collision.

using System.Text;

namespace iXtal26.Noms;

internal static class RegleNoms
{
    /// <summary>Les mots d'un identifiant, dans l'ordre (aucun mot vide).</summary>
    public static List<string> Mots(string id)
    {
        if (id.StartsWith('@')) id = id[1..];
        var mots = new List<string>();
        foreach (var morceau in id.Split('_', StringSplitOptions.RemoveEmptyEntries))
        {
            var debut = 0;
            for (var i = 1; i < morceau.Length; i++)
            {
                var p = morceau[i - 1];
                var c = morceau[i];
                var coupe = char.IsUpper(c) && (char.IsLower(p) || char.IsDigit(p))
                            || char.IsUpper(p) && char.IsUpper(c) && i + 1 < morceau.Length
                                               && char.IsLower(morceau[i + 1]);
                if (!coupe) continue;
                mots.Add(morceau[debut..i]);
                debut = i;
            }
            mots.Add(morceau[debut..]);
        }
        return mots;
    }

    private static bool ToutMajuscules(string mot) => mot.Any(char.IsLetter) && !mot.Any(char.IsLower);

    private static string Ecrire(string mot)
    {
        var lettres = mot.Count(char.IsLetter);
        if (ToutMajuscules(mot))
            return lettres == 2 ? mot : char.ToUpperInvariant(mot[0]) + mot[1..].ToLowerInvariant();
        return char.ToUpperInvariant(mot[0]) + mot[1..];
    }

    /// <summary>L'identifiant en PascalCase (une chaîne vide si l'identifiant n'a aucun mot).</summary>
    public static string Pascal(string id)
    {
        var sb = new StringBuilder();
        foreach (var m in Mots(id)) sb.Append(Ecrire(m));
        return sb.ToString();
    }

    /// <summary>L'identifiant en camelCase : le premier mot en minuscules, les suivants comme en PascalCase.</summary>
    public static string Camel(string id)
    {
        var mots = Mots(id);
        if (mots.Count == 0) return "";
        var sb = new StringBuilder(mots[0].ToLowerInvariant());
        foreach (var m in mots.Skip(1)) sb.Append(Ecrire(m));
        return sb.ToString();
    }
}
