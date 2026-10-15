// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — outillage de vérification.
//
// G13 — la liste d'acceptation du mode matériel (PLAN-G13.md, § Le mécanisme). L'oracle est PCem : une commande qui
// compare le C# à l'oracle ne dirait rien en mode matériel, ni par un vert ni par un rouge. Chaque commande déclare
// donc si elle prend le mode ; une commande neuve le refuse (retour 2) tant qu'elle n'est pas inscrite ici.
//
// Quatre portes R9 n'atteignent leur garde que par un défaut de PCem : r9-atapi par PB-119, r9-zip par PB-125 et
// PB-126, r9-scsihd par PB-128 et PB-132, r9-aha par PB-276 (l'ancien PB-132, côté carte). PB-119 (G13.7c2), PB-125 et
// PB-126 (G13.7d1), PB-128 et PB-132 côté cible (G13.7d2a) sont corrigés : en mode matériel, r9-atapi, r9-zip et
// r9-scsihd n'atteignent plus toutes leurs gardes et rougiraient (« la garde n'a pas été atteinte ») — la série ne les
// lance qu'en mode PCem, où les gardes sont atteintes ; les corrections ont leurs cas (materiel-cas). PB-276 corrigé
// (G13.7e2a), r9-aha atteint encore chacune de ses gardes en mode matériel, sous PB-276 comme sous « tout » (ses essais
// passent par PB-135 et par la configuration, non par la navette) : elle reste acceptée.

namespace iXtal26.Diff;

internal static class ModeOutils
{
    /// <summary>Les commandes en C# seul, qui prennent le mode sans condition.</summary>
    private static readonly HashSet<string> Toujours =
    [
        "r9-mmu", "r9-cl5429", "r9-sbcfg", "r9-joycfg", "r9-s3", "r9-sb16", "r9-emu8k", "r9-awecfg", "r9-cue",
        "r9-atapi", "r9-cdcfg", "r9-zip", "r9-aha", "r9-scsihd", "r9-cga", "r9-m24", "r9-disquette", "r9-filet", "r9-xebec",
        "reset-scsi-check", "recensement", "config-check", "ops-count", "speed-check", "boot-profile",
        "refresh-check", "fdc-trace", "materiel-cas", "materiel-mode", "couverture-materiel", "banc", "sst-rep-temps",
    ];

    /// <summary>Vrai si la commande prend le mode matériel ; sinon, la raison du refus.</summary>
    internal static bool Accepte(string[] args, out string raison)
    {
        raison = "";
        if (Toujours.Contains(args[0]))
            return true;
        switch (args[0])
        {
            case "sst-probe" or "sst386-probe" or "sst286-probe":
                if (Option(args, "--target") == "csharp")
                    return true;
                raison = "le mode matériel se vérifie contre le silicium du corpus, sous --target csharp seulement";
                return false;
            case "pm-check":
                if (Option(args, "--target") == "csharp")
                    return true;
                raison = "le banc du mode protégé se juge en mode matériel sur ses attentes, sous --target csharp seulement";
                return false;
            case "bench":
                if (Option(args, "--side") == "csharp")
                    return true;
                raison = "seul le côté C# (--side csharp) se mesure en mode matériel";
                return false;
            case "fuzz":
                if (args.Contains("--fuite") || args.Contains("--accord"))
                    return true;
                raison = "le fuzzeur compare à l'oracle ; en mode matériel, il ne sert qu'au contrôle de fuite (--fuite) et " +
                         "au domaine d'accord du noyau (--accord)";
                return false;
        }
        raison = "la commande compare le C# à l'oracle, qui est PCem";
        return false;
    }

    private static string? Option(string[] args, string nom)
    {
        var i = Array.IndexOf(args, nom);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }
}
