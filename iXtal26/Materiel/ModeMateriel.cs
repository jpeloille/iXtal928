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
        new(2, "processeur", "RCL et RCR mot par CL gardent le CF du dernier bit sorti"),
        new(3, "processeur", "les cycles du rafraîchissement par DMA (5150, XT) arrivent au TSC"),
        new(5, "carte-mere", "servir l'esclave du 8259 ne touche à l'IRR du maître que par la cascade"),
        new(7, "processeur", "un mot à cheval sur deux pages prend son octet haut dans la sienne, au repli de 1 Mo"),
        new(32, "processeur", "l'IDT trop courte lève #GP au code n × 8 + 2 + EXT"),
        new(39, "processeur", "CALL et JMP sur une porte de tâche changent de tâche"),
        new(40, "processeur", "CALL sur une tâche n'empile pas l'adresse de retour"),
        new(43, "processeur", "MOV CRx, DRx et TRx ignorent le champ mod (386, 486)"),
        new(45, "processeur", "IDIV octet divise AX signé"),
        new(50, "processeur", "une instruction de plus de 15 octets (10 sur le 286) lève #GP"),
        new(51, "processeur", "la lecture d'une instruction contrôle la limite de CS (286, 386, 486)"),
        new(57, "x87", "FCOM, FCOMP et FCOMPP de registre comparent comme le silicium, au temps de fcom"),
        new(58, "x87", "FCOMPP compare −0 et +0 égaux, sans le contournement de détection"),
        new(61, "x87", "FNSTSW AX rend le mot d'état entier, TOP compris (287, 387, 486)"),
        new(63, "x87", "FXAM rend la classe de ST(0), et le signe dans C1"),
        new(64, "x87", "FTST rend un NaN non ordonné"),
        new(66, "x87", "les constantes au plus près (ln 2 compris), et selon RC sur le 287XL, le 387 et le 486"),
        new(67, "x87", "FST et FSTP ST(i) copient aussi l'entier exact de TAG_UINT64"),
        new(70, "x87", "le 8087 et le 287 comparent en projectif tant que IC est nul"),
        new(78, "processeur", "LOADALL386 lève #UD sur le 486"),
        new(87, "processeur", "au repli de l'IP, l'instruction se lit à l'offset 0 du segment"),
        new(94, "carte-mere", "la souris PS/2 répond à F6h, EAh, F0h, EEh et ECh, et rejette une commande inconnue"),
        new(95, "carte-mere", "l'octet d'état de la souris PS/2 : gauche en bit 2, droit en bit 0, mode distant en bit 6"),
        new(101, "carte-mere", "le PC1512 n'a pas de second port parallèle à 278h"),
        new(103, "carte-mere", "le chapeau de la CH et de la TM lit 315° (haut-gauche) en haut"),
        new(157, "carte-mere", "la commande du 8237 haut est rangée, et DAh se lit sur le temporaire"),
        new(169, "processeur", "DIV et IDIV lèvent INT 0 sur un quotient hors capacité"),
        new(170, "processeur", "DAA compare l'AL d'origine à 99h, ou à 9Fh si AF valait 1"),
        new(171, "processeur", "DAS teste l'AL et le CF d'origine, au seuil de DAA"),
        new(172, "processeur", "REP LODSB et REP LODSW chargent AL et AX"),
        new(173, "processeur", "SETMO et SETMOC mettent l'opérande à FFh (FFFFh)"),
        new(174, "processeur", "les décalages par CL posent OF, celui du dernier pas"),
        new(175, "processeur", "AAM et AAD posent SF et ZF d'après AL"),
        new(176, "processeur", "SAR par CL rend CF, la copie du signe, au-delà de 8 (16)"),
        new(177, "processeur", "rep() : 6Eh est JLE, REP DS: répète, un préfixe placé avant REP vaut"),
        new(179, "processeur", "un mot à l'offset FFFFh replie à l'offset 0 du segment"),
        new(181, "processeur", "l'AF d'ADC du cœur 286/386/486 compte la retenue entrante"),
        new(182, "processeur", "LOCK lève #UD hors de sa liste et devant une forme registre (386, 486)"),
        new(183, "processeur", "BT, BTS, BTR et BTC déplacent l'adresse d'un décalage signé"),
        new(184, "processeur", "MOVSX r16,r/m16 s'exécute (386, 486)"),
        new(185, "processeur", "AAA et AAS du cœur 286/386/486 ajustent AX entier"),
        new(186, "processeur", "AAD et AAM du cœur 286/386/486 posent SF, ZF et PF d'après AL"),
        new(187, "processeur", "AAM 0 du cœur 286/386/486 lève #DE"),
        new(188, "processeur", "le DAS du cœur 286/386/486 teste l'AL et le CF d'origine"),
        new(189, "processeur", "un opérande en mémoire tient entier dans la limite de son segment : #GP(0), #SS(0) pour la pile"),
        new(190, "processeur", "LTR contrôle le sélecteur, le type et la présence de la TSS"),
        new(191, "processeur", "la voie TSS des CALL, JMP et INT contrôle le DPL, la présence, la GDT et le type"),
        new(192, "processeur", "la porte tient tout entière dans l'IDT ; EXT marque un événement externe"),
        new(193, "processeur", "LOADALL386 lève #GP(0) hors du niveau 0, en mode protégé"),
        new(246, "carte-mere", "la cascade du 8259 se sert à son rang, après l'IRQ 0 et l'IRQ 1"),
        new(247, "carte-mere", "le masque de service du 8259 retient les niveaux de priorité égale ou moindre"),
        new(248, "carte-mere", "l'OCW2 et l'OCW3 du 8259 selon la fiche : rotations, priorité, poll, masque spécial"),
        new(249, "carte-mere", "Clear Mask (0Eh, DCh) efface les quatre masques du 8237"),
        new(250, "carte-mere", "le registre de requête du 8237 : la requête logicielle, en mode bloc, jusqu'au TC"),
        new(251, "carte-mere", "le master clear du 8237 efface aussi la commande, l'état et la requête"),
        new(252, "carte-mere", "au reset, le 8237 pose ses masques et efface la commande, l'état et la requête"),
        new(253, "carte-mere", "les canaux 0 à 3 de l'AT n'ont le bus que par la cascade du canal 4"),
        new(254, "carte-mere", "le clavier de l'AT garde seize codes puis le débordement ; le 8042 ne perd rien d'autre"),
        new(255, "carte-mere", "après ICW1 et à la mise sous tension, le 8259 se lit sur l'IRR"),
        new(257, "carte-mere", "un transfert de DMA refusé ne coûte pas de cycle"),
        new(258, "processeur", "l'erreur de division empile dans SS, sous un préfixe de segment"),
        new(261, "carte-mere", "la souris PS/2 remet ses compteurs de mouvement à zéro après EBh"),
        new(262, "processeur", "le décalage immédiat d'un BT, BTS, BTR ou BTC 16 bits se prend modulo 16"),
        new(263, "processeur", "après un CALL de tâche, l'IP contre la limite du nouveau CS : #TS(0) (386, 486)"),
    ];

    /// <summary>Les corrections qui ne valent qu'ensemble (PLAN-G13.md, § Le mécanisme) : en demander une demande les
    /// autres. PB-03 et PB-257 : les cycles du rafraîchissement portés au TSC, sans ceux d'un transfert refusé, qui y
    /// iraient sinon. PB-07 et PB-179 : la même condition de repli dans readmemw et writememw. PB-45, PB-169 et PB-258 :
    /// le signe du dividende, la capacité du quotient et la pile de l'erreur, dans le même calcul. PB-50 et PB-51 : la
    /// longueur et la limite, lues par le même décodeur en tête d'instruction. PB-32 et PB-192 : la limite de l'IDT et
    /// le code d'erreur de son dépassement, au même test. PB-57, PB-58, PB-64 et PB-70 : les comparaisons du x87, par la même
    /// fonction (x87.Materiel.cs).</summary>
    internal static readonly int[][] Groupes = [[3, 257], [7, 179], [32, 192], [45, 169, 258], [50, 51], [57, 58, 64, 70]];

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
        foreach (var groupe in Groupes)
            if (groupe.Any(pbs.Contains))
                pbs.UnionWith(groupe);
        return true;
    }
}
