// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — le mode matériel (G13 ; PLAN-G13.md, § Le mécanisme ; R10 de TRANSCRIPTION.md).
// STATUS: host
//
// Le mode PCem reproduit les défauts de PCem, l'oracle (R8) ; le mode matériel les corrige À CÔTÉ, défaut par défaut,
// d'après la documentation du vrai matériel (PCEM_BUGS.md, champ *Corrigé en mode matériel*). Deux étages : pour
// l'utilisateur, un interrupteur (la clé hardware_mode, l'écran de construction, --hardware-mode tout) ; pour les
// outils, une liste de corrections (--hardware-mode PB-01,processeur…), pour en couper une ou n'en essayer qu'une.
//
// Ici, la DEMANDE : qui la pose, la table des corrections et de leurs domaines, le gel. La valeur figée vit dans la
// classe `materiel`, qui ne contient rien d'autre : son constructeur statique copie la demande une seule fois, quand
// Figer() le lance, en tête d'initpc, dans les remises à zéro des harnais et en tête d'iXtal26.Diff, avant le premier
// cœur. Après le gel, une demande qui CHANGE quelque chose est refusée : le mode se choisit au lancement. Une demande
// qui ne change rien passe (la seconde machine d'une porte relit sa clé). Un site atteint avant le gel lit la demande
// (Demande), jamais le champ figé.
//
// La sonde : un compteur par correction, incrémenté dans le seul code du mode matériel. Elle dit quelles corrections
// un scénario a exercées ; en mode PCem, ce code est mort et elle reste à zéro.

using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace iXtal26;

internal static class ModeMateriel
{
    /// <summary>Une correction : son numéro PB, son domaine, ce qu'elle rétablit.</summary>
    internal readonly record struct Correction(int Pb, string Domaine, string Objet);

    /// <summary>Les corrections du mode matériel, dans l'ordre des numéros. Un domaine est un alias de liste, pour la
    /// ligne de commande et les outils ; il ne décide de rien d'autre.</summary>
    internal static readonly Correction[] Corrections =
    [
        new(1, "processeur", "l'AF d'ADC et de SBB du 8088 et du 8086 compte la retenue entrante"),
    ];

    internal static readonly string[] Domaines = ["processeur", "x87", "stockage", "video", "son", "carte-mere"];

    /// <summary>La sonde : combien de fois chaque correction a servi, par numéro PB.</summary>
    internal static readonly int[] Sonde = new int[Corrections[^1].Pb + 1];

    private static readonly SortedSet<int> demande = [];
    private static bool gelEnCours;

    /// <summary>Vrai une fois le mode figé : la classe materiel a copié la demande.</summary>
    internal static bool Fige { get; private set; }

    /// <summary>La demande pour une correction. Le constructeur de materiel la copie ; un site qui court avant le gel la
    /// lit à la place du champ figé.</summary>
    internal static bool Demande(int pb) => demande.Contains(pb);

    /// <summary>Au moins une correction demandée.</summary>
    internal static bool Actif => demande.Count > 0;

    /// <summary>La demande en clair, sans accent (la barre de titre est en ASCII) : « PCem », ou « materiel (PB-01) ».
    /// Toutes les corrections demandées s'écrivent « materiel (tout) ».</summary>
    internal static string Description =>
        !Actif ? "PCem" : demande.SetEquals(Tout) ? "materiel (tout)" : $"materiel ({string.Join(", ", demande.Select(n => $"PB-{n:00}"))})";

    /// <summary>La demande en liste de ligne de commande (« PB-01,PB-45 », ou « aucun »), pour un processus fils.</summary>
    internal static string ListeDemandee => Actif ? string.Join(",", demande.Select(n => $"PB-{n:00}")) : "aucun";

    /// <summary>Toutes les corrections.</summary>
    internal static SortedSet<int> Tout => [.. Corrections.Select(c => c.Pb)];

    /// <summary>Pose la demande d'une LISTE : « tout », « aucun », des domaines ou des PB-nn, séparés par des virgules.
    /// Rend faux, le message écrit, sur un nom inconnu ou sur un changement après le gel.</summary>
    internal static bool Demander(string liste, string source)
    {
        if (!Lire(liste, out var pbs, out var erreur))
        {
            Console.Error.WriteLine($"{source} : {erreur}");
            return false;
        }
        return Poser(pbs, source);
    }

    /// <summary>La clé hardware_mode d'un .cfg : 1, toutes les corrections ; 0, aucune. Une autre valeur est ramenée à 0,
    /// avec un avertissement.</summary>
    internal static bool DemanderParCle(int valeur)
    {
        if (valeur is not (0 or 1))
        {
            Console.Error.WriteLine($"hardware_mode = {valeur} : hors de 0 et 1 ; le mode PCem (0) à la place.");
            valeur = 0;
        }
        return Poser(valeur == 1 ? Tout : [], $"hardware_mode = {valeur}");
    }

    /// <summary>L'interrupteur de l'écran de construction : toutes les corrections, ou aucune.</summary>
    internal static bool Basculer() => Poser(Actif ? [] : Tout, "l'écran de construction");

    /// <summary>Fige le mode : la classe materiel copie la demande. Idempotent ; à appeler avant le premier cœur.</summary>
    internal static void Figer()
    {
        if (Fige)
            return;
        gelEnCours = true;
        RuntimeHelpers.RunClassConstructor(typeof(materiel).TypeHandle);
        gelEnCours = false;
    }

    /// <summary>Appelé par le constructeur de materiel, et par lui seul : le gel a lieu. Hors de Figer(), une garde a été
    /// lue avant le premier cœur ; le gel vaut quand même, et le message le dit.</summary>
    internal static void Gel()
    {
        if (!gelEnCours)
            Console.Error.WriteLine($"mode matériel : figé hors de son point de gel, par une garde lue avant le premier " +
                                    $"cœur ; il vaut {Description}.");
        Fige = true;
    }

    private static bool Poser(SortedSet<int> pbs, string source)
    {
        if (Fige)
        {
            if (pbs.SetEquals(demande))
                return true;
            Console.Error.WriteLine($"{source} : le mode se choisit au lancement, avant le premier cœur ; il est figé " +
                                    $"({Description}), la demande est refusée.");
            return false;
        }
        demande.Clear();
        demande.UnionWith(pbs);
        return true;
    }

    private static bool Lire(string liste, out SortedSet<int> pbs, out string erreur)
    {
        pbs = [];
        erreur = "";
        foreach (var brut in liste.Split(',', StringSplitOptions.TrimEntries))
        {
            var mot = brut.ToLowerInvariant();
            if (mot == "tout")
                pbs.UnionWith(Tout);
            else if (mot == "aucun")
                continue;
            else if (Domaines.Contains(mot))
                pbs.UnionWith(Corrections.Where(c => c.Domaine == mot).Select(c => c.Pb));
            else if (Regex.Match(brut, @"^PB-(\d+)$", RegexOptions.IgnoreCase) is { Success: true } m)
            {
                var n = int.Parse(m.Groups[1].Value);
                if (!Corrections.Any(c => c.Pb == n))
                {
                    erreur = $"PB-{n:00} n'est pas corrigé en mode matériel ; les corrections : " +
                             $"{string.Join(", ", Corrections.Select(c => $"PB-{c.Pb:00}"))}.";
                    return false;
                }
                pbs.Add(n);
            }
            else
            {
                erreur = $"« {brut} » n'est ni « tout », ni « aucun », ni un domaine ({string.Join(", ", Domaines)}), " +
                         "ni un PB-nn.";
                return false;
            }
        }
        return true;
    }
}
