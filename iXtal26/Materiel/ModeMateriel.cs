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
        new(14, "stockage", "le débit de 300 kbit/s de la disquette : l'octet dure 80/3 µs"),
        new(19, "stockage", "en écriture hors DMA, l'octet que l'UC n'a pas fourni à temps pose OR"),
        new(22, "stockage", "FORMAT TRACK du Xebec formate la piste désignée depuis son début"),
        new(23, "stockage", "l'octet de fin du Xebec porte le bit d'unité"),
        new(25, "stockage", "la tête égale au nombre de têtes est hors du disque (Xebec, AT)"),
        new(32, "processeur", "l'IDT trop courte lève #GP au code n × 8 + 2 + EXT"),
        new(39, "processeur", "CALL et JMP sur une porte de tâche changent de tâche"),
        new(40, "processeur", "CALL sur une tâche n'empile pas l'adresse de retour"),
        new(43, "processeur", "MOV CRx, DRx et TRx ignorent le champ mod (386, 486)"),
        new(45, "processeur", "IDIV octet divise AX signé"),
        new(48, "x87", "chaque opération suit RC, et PC la précision des quatre opérations (le noyau)"),
        new(50, "processeur", "une instruction de plus de 15 octets (10 sur le 286) lève #GP"),
        new(51, "processeur", "la lecture d'une instruction contrôle la limite de CS (286, 386, 486)"),
        new(52, "x87", "FBLD charge dix-huit chiffres décimaux (le noyau)"),
        new(54, "x87", "chaque accès du x87 contrôle la limite de son opérande et, en mode protégé, ses droits (le noyau)"),
        new(55, "x87", "FLD m80 charge les 80 bits tels quels (le noyau)"),
        new(56, "x87", "les registres de 80 bits : FSTP m80 les range tels quels, les calculs ont 64 bits de significande (le noyau)"),
        new(57, "x87", "FCOM, FCOMP et FCOMPP de registre comparent comme le silicium, au temps de fcom"),
        new(58, "x87", "FCOMPP compare −0 et +0 égaux, sans le contournement de détection"),
        new(59, "x87", "une division par zéro démasquée pose ES et B, et l'exception va où la carte la mène"),
        new(60, "x87", "deux NaN : le plus grand significande survit, quel que soit l'ordre des opérandes (le noyau)"),
        new(61, "x87", "FNSTSW AX rend le mot d'état entier, TOP compris (287, 387, 486)"),
        new(62, "x87", "les pointeurs d'instruction et d'opérande, rangés et relus dans les quatre dispositions (le noyau)"),
        new(63, "x87", "FXAM rend la classe de ST(0), et le signe dans C1"),
        new(64, "x87", "FTST rend un NaN non ordonné"),
        new(65, "x87", "FPREM et FPREM1 : le reste exact, la réduction partielle et C2 ; FPREM1 au plus près (le noyau)"),
        new(66, "x87", "les constantes au plus près (ln 2 compris), et selon RC sur le 287XL, le 387 et le 486"),
        new(67, "x87", "FST et FSTP ST(i) copient aussi l'entier exact de TAG_UINT64"),
        new(68, "x87", "les transcendantes sur 80 bits, arrondies une fois selon RC ; C2 au-delà de 2^63, réduites par le Pi du silicium (le noyau)"),
        new(69, "x87", "sur un PC ou un XT, l'exception du 8087 lève la NMI, que le port A0h masque"),
        new(70, "x87", "le 8087 et le 287 comparent en projectif tant que IC est nul"),
        new(71, "stockage", "chaque canal IDE suit sa ligne d'interruption, et non celle de l'IRQ 14"),
        new(74, "stockage", "READ VERIFY vérifie tous ses secteurs ; l'adresse du dernier, le compte nul, ou IDNF"),
        new(78, "processeur", "LOADALL386 lève #UD sur le 486"),
        new(87, "processeur", "au repli de l'IP, l'instruction se lit à l'offset 0 du segment"),
        new(94, "carte-mere", "la souris PS/2 répond à F6h, EAh, F0h, EEh et ECh, et rejette une commande inconnue"),
        new(95, "carte-mere", "l'octet d'état de la souris PS/2 : gauche en bit 2, droit en bit 0, mode distant en bit 6"),
        new(101, "carte-mere", "le PC1512 n'a pas de second port parallèle à 278h"),
        new(103, "carte-mere", "le chapeau de la CH et de la TM lit 315° (haut-gauche) en haut"),
        new(106, "stockage", "un secteur de CD de 2 336 octets a ses données à +8"),
        new(108, "stockage", "la piste qui ouvre son fichier y commence à son INDEX 01"),
        new(109, "stockage", "un échec de lecture de l'image ne colle pas au fichier"),
        new(117, "stockage", "READ CD-ROM CAPACITY rend l'adresse du dernier bloc avant le lead-out"),
        new(118, "stockage", "GET EVENT STATUS NOTIFICATION selon MMC-2 sur un lecteur de cette génération"),
        new(119, "stockage", "MODE SELECT lit l'en-tête de sa forme, rend 1Ah ou 26h, et finit à longueur nulle"),
        new(120, "stockage", "la TOC brute a ses points A0h, A1h, A2h et sa longueur"),
        new(122, "stockage", "le lead-out de la TOC porte ADR 1 et le contrôle de la dernière piste"),
        new(123, "stockage", "l'audio CD en LBA : l'adresse et la position du lecteur à 150 secteurs près"),
        new(125, "stockage", "le lecteur ZIP transfère au-delà de 512 Ko"),
        new(126, "stockage", "le ZIP : la capacité, la fin du disque, les longueurs nulles, le disque gardé au reset"),
        new(128, "stockage", "le disque SCSI transfère au-delà de 512 Ko"),
        new(129, "stockage", "le disque SCSI annonce son dernier bloc, et refuse au-delà (05/21h)"),
        new(130, "stockage", "MODE SENSE du disque SCSI : l'en-tête, le descripteur, les pages 03h et 04h, PC et DBD"),
        new(131, "stockage", "REQUEST SENSE, INQUIRY et les refus du disque SCSI selon SCSI-2"),
        new(132, "stockage", "une longueur nulle finit sans phase de données (le disque SCSI)"),
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
        new(194, "x87", "FXTRACT sépare l'exposant et le significande (le noyau)"),
        new(195, "x87", "FBSTP arrondit selon RC ; trop grand, un NaN ou un infini : IE et l'indéfini BCD (le noyau)"),
        new(196, "x87", "FIST et FISTP hors bornes : IE et l'indéfini entier (le noyau)"),
        new(197, "x87", "FRNDINT garde |x| ≥ 2^63, les infinis et les NaN (le noyau)"),
        new(198, "x87", "FSCALE par un NaN ou un infini : la table des spéciaux (le noyau)"),
        new(199, "x87", "FSAVE range chaque registre en réel de 80 bits (le noyau)"),
        new(200, "x87", "DF E0 laisse AX sur le 8087, qui n'a pas FNSTSW AX (le noyau)"),
        new(201, "x87", "le 8087 lit et écrit chaque mot de son opérande à l'adresse physique, sans boucler dans le segment (le noyau)"),
        new(202, "x87", "FINIT, FSAVE et le RESET gardent C3-C0 sur le 8087 et le 287 (le noyau)"),
        new(203, "x87", "au RESET, le coprocesseur prend l'état initial de son type (le noyau)"),
        new(204, "x87", "les ports F0h et F1h de l'AT, le verrou de BUSY# et IGNNE# du 486"),
        new(206, "x87", "FLD m32 d'un SNaN lève IE (387, 486 ; le noyau)"),
        new(207, "x87", "FSTENV masque les six exceptions après avoir rangé l'environnement"),
        new(208, "x87", "les étiquettes rangées sont celles du contenu, et recalculées au chargement (287XL, 387, 486 ; le noyau)"),
        new(211, "x87", "FRNDINT garde le signe du zéro (le noyau)"),
        new(212, "x87", "FLD m64 d'un SNaN lève IE et le charge silencieux (387, 486 ; le noyau)"),
        new(213, "x87", "les comparaisons remettent C1 à zéro (287XL, 387, 486)"),
        new(214, "stockage", "PLAY AUDIO MSF cherche sa piste au LBA qu'il désigne"),
        new(215, "stockage", "le contrôle de piste de PLAY AUDIO MSF juge le LBA, sans les 150 secteurs"),
        new(216, "stockage", "INTRQ suit l'unité choisie et nIEN ; un front par interruption, aucune perdue"),
        new(217, "stockage", "le sense du Xebec porte le bit « adresse valide » et l'adresse"),
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
        new(264, "x87", "la pile du x87 déborde : IE, et SF et C1 sur le 387 et le 486 (le noyau)"),
        new(265, "x87", "FRNDINT, FIST et FISTP au plus près sans l'arrondi de b + 1,0 (le noyau)"),
        new(268, "stockage", "un READ du CD qui sort du disque est refusé d'emblée, 05/21h"),
        new(269, "stockage", "MECHANISM STATUS lit son allocation sur deux octets, et rend 1Ah sous huit octets"),
        new(270, "stockage", "le lecteur ATAPI refuse les commandes que la norme de sa génération n'a pas"),
        new(271, "stockage", "READ CD MSF, obligatoire, servi"),
        new(272, "stockage", "PLAY AUDIO(12) lit son adresse et sa longueur sur quatre octets"),
        new(273, "stockage", "une plage de PLAY AUDIO qui change de type (audio, données) rend 05/63h"),
    ];

    /// <summary>Les corrections qui ne valent qu'ensemble (PLAN-G13.md, § Le mécanisme) : en demander une demande les
    /// autres. PB-03 et PB-257 : les cycles du rafraîchissement portés au TSC, sans ceux d'un transfert refusé, qui y
    /// iraient sinon. PB-07 et PB-179 : la même condition de repli dans readmemw et writememw. PB-45, PB-169 et PB-258 :
    /// le signe du dividende, la capacité du quotient et la pile de l'erreur, dans le même calcul. PB-50 et PB-51 : la
    /// longueur et la limite, lues par le même décodeur en tête d'instruction. PB-32 et PB-192 : la limite de l'IDT et
    /// le code d'erreur de son dépassement, au même test. PB-59, PB-69 et PB-204 : l'exception du x87, posée puis acheminée
    /// par la carte, au même site (x87.Materiel.cs). PB-48, 52, 54 à 58, 60 à 68, 70, 194 à 203, 206 à 208, 211 à 213, 264 et
    /// 265 : le noyau de 80 bits (x87_noyau.Materiel.cs), un seul état, que ses gestionnaires partagent ; il reprend les
    /// corrections en `double` de G13.6a et 6b (PB-57, 58, 61, 63, 64, 66, 67, 70, 207, 213), dont il remplace les tables
    /// (G13.6, le noyau, N7c ; leurs gestionnaires en `double` ont quitté le dépôt en N8a). PB-71 et PB-216 : la ligne
    /// d'interruption d'un canal IDE, dans la même fonction (ide_irq_update, G13.7b). PB-118 et PB-270 : GET EVENT STATUS
    /// NOTIFICATION, servi ou refusé selon la génération du lecteur (G13.7c3).</summary>
    internal static readonly int[][] Groupes =
        [[3, 257], [7, 179], [32, 192], [45, 169, 258], [50, 51], [59, 69, 204], [71, 216], [118, 270],
         [48, 52, 54, 55, 56, 57, 58, 60, 61, 62, 63, 64, 65, 66, 67, 68, 70, 194, 195, 196, 197, 198, 199, 200, 201, 202, 203, 206, 207, 208, 211, 212, 213, 264, 265]];

    /// <summary>Les corrections en chantier, qui n'entrent ni dans « tout », ni dans leur domaine : aucune depuis que le
    /// noyau de 80 bits du x87 est complet (PLAN-G13.md, § G13.6, le noyau, N7c ; décision n° 4). Le mécanisme reste pour
    /// un chantier futur.</summary>
    internal static readonly int[] EnChantier = [];

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

    /// <summary>Toutes les corrections, sauf celles en chantier.</summary>
    internal static SortedSet<int> Tout => [.. Corrections.Select(c => c.Pb).Except(EnChantier)];

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
                pbs.UnionWith(Corrections.Where(c => c.Domaine == mot && !EnChantier.Contains(c.Pb)).Select(c => c.Pb));
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
