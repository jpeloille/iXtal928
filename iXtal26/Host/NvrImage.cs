// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — outillage hôte. La RÉFÉRENCE est une ROM : la routine d'écriture
//         du SETUP AMI, amic206.bin:0xacd0-0xad11.
// STATUS: host
//
// FABRIQUER UN CMOS, PARCE QUE LE SETUP EST UNE IMPASSE. Un BIOS AMI porte son SETUP en
// ROM, mais le piloter demande d'envoyer Suppr, les flèches et F10 à l'aveugle, et de
// deviner la disposition de ses écrans. L'IBM AT, lui, n'a pas de SETUP en ROM du tout :
// le sien est sur la disquette de diagnostics, que ce dépôt n'a pas.
//
// CE FICHIER N'INVENTE AUCUN OCTET. Il part du CMOS de référence que PCem livre —
// nvr/default/<machine>.nvr — et n'amende que ce que NOTRE machine contredit. Les octets
// dont personne ici ne connaît le sens (0x13, 0x33, 0x37, 0x3F, 0x6E chez l'AMI) sont
// recopiés tels quels. Un octet qu'on ne comprend pas et qu'on écrase est un octet qu'on
// devra rechercher pendant des heures le jour où le POST s'en plaindra.
//
// ET IL NE FAIT RIEN D'AUTRE QUE CE QUE FAIT LE SETUP DE LA ROM. Mesuré en
// amic206.bin:0xacd0-0xad11 : il écrit 0x10 à 0x3F sauf 0x32, resomme 0x10-0x2D, pose
// 0x2E en poids fort puis 0x2F en poids faible, met 0x0E et 0x0F à zéro, et redémarre.
// Les quatre gestes sont ici.
//
// LA DISPOSITION DU CMOS ET SA SOMME DE CONTRÔLE SONT DANS nvr/README.md, mesurées dans
// les deux ROM et non reprises d'une documentation.

using iXtal26.PluginApi;

namespace iXtal26.Host;

internal static class NvrImage
{
    /// <summary>
    /// La plage sommée, mesurée : de 0x10 à 0x2D inclus. La boucle de vérification de
    /// l'AT (0x06fe-0x0727) démarre à 0x10 et s'arrête AVANT 0x2E ; celle de l'AMI
    /// (0x825a-0x8296) fait de même avec ses propres registres.
    /// </summary>
    private const int SumFirst = 0x10;
    private const int SumLast = 0x2D;

    /// <summary>
    /// Somme 16 bits, NON tronquée à 8 : le C de la ROM fait `add bx,ax` avec l'octet
    /// étendu en zéro. Rangée gros-boutiste, 0x2E en poids fort.
    ///
    /// UNE SOMME NULLE EST REJETÉE par les deux BIOS (`or bx,bx / jz` en 0x0715 côté AT,
    /// `or dx,dx / jne` en 0x8290 côté AMI), même si 0x2E et 0x2F concordent. C'est ce
    /// qui fait qu'un CMOS tout à zéro échoue exactement comme un CMOS à 0xFF — et donc
    /// que l'écran ne départage pas deux CMOS.
    /// </summary>
    internal static int Checksum(uint8_t[] cmos)
    {
        var sum = 0;
        for (var i = SumFirst; i <= SumLast; i++)
                sum += cmos[i];
        return sum & 0xFFFF;
    }

    /// <summary>
    /// Le quartet de type de lecteur du CMOS, pour un type de lecteur de PCem.
    ///
    /// DEUX NUMÉROTATIONS SE CROISENT ICI, ET LES CONFONDRE EST SILENCIEUX. La table de
    /// PCem (fdd.cs:88-112) décrit des MÉCANIQUES — 5,25" ou 3,5", simple ou double
    /// densité, double vitesse ou non. Le CMOS, lui, décrit des CAPACITÉS, et n'en
    /// connaît que cinq. Un 3,5" haute densité est le type PCem 5 mais le quartet CMOS 4.
    ///
    /// Les deux variantes exotiques retombent sur leur capacité nominale : la double
    /// vitesse 5,25" est un 1,2 Mo pour le BIOS, le 3 modes est un 1,44 Mo.
    /// </summary>
    internal static int FloppyNibble(int pcemType) => pcemType switch
    {
        0 => 0, // aucun lecteur
        1 => 1, // 5,25" DD     ->  360 Ko
        2 => 2, // 5,25" HD     -> 1,2 Mo
        3 => 2, // 5,25" HD double vitesse
        4 => 3, // 3,5"  DD     ->  720 Ko
        5 => 4, // 3,5"  HD     -> 1,44 Mo
        6 => 4, // 3,5"  HD 3 modes
        7 => 5, // 3,5"  ED     -> 2,88 Mo
        _ => 0,
    };

    /// <summary>
    /// Les bits 5-4 de l'octet d'équipement 0x14, et ils ne se devinent pas.
    ///
    /// LES DEUX SITES DE TEST DU BIOS AMI S'EXCLUENT, ils ne se contredisent pas. Le
    /// premier (0x9716) ne s'applique QUE si le vecteur d'INT 10h ne pointe plus dans
    /// F000 — donc si une carte a posé sa propre ROM d'extension — et il EXIGE alors 00.
    /// Le second (0x97dd) vaut pour une vidéo portée par la carte mère, et il REFUSE 00,
    /// qui ne décrit aucune carte.
    ///
    /// D'où : une VGA, qui a sa ROM en C000, se déclare 00 ; une CGA, dont l'INT 10h
    /// reste celui du BIOS, se déclare 10 en binaire, soit 80x25 couleur.
    ///
    /// C'est ce qui explique que les DEUX fichiers livrés par PCem portent 00 sur ce
    /// champ et que les deux machines s'en plaignent : ils décrivent une machine à carte
    /// vidéo séparée, que ce dépôt ne montait pas.
    /// </summary>
    internal const int DisplayOwnRom = 0x00;   // VGA, EGA : carte à ROM d'extension
    internal const int DisplayCga80 = 0x20;    // CGA 80x25 couleur
    internal const int DisplayCga40 = 0x10;    // CGA 40x25 couleur
    internal const int DisplayMda = 0x30;      // MDA / Hercules 80x25

    /// <summary>
    /// Amende un CMOS de référence pour décrire la machine décrite par un fichier de
    /// configuration. Rend le tableau amendé, somme de contrôle posée.
    /// </summary>
    /// <param name="cmos">Les 128 octets de nvr/default/&lt;machine&gt;.nvr, amendés en place.</param>
    /// <param name="memSize">mem_size du .cfg, en Ko.</param>
    /// <param name="driveA">drive_a_type du .cfg, numérotation PCem.</param>
    /// <param name="driveB">drive_b_type du .cfg, numérotation PCem.</param>
    /// <param name="hddType">Type de disque dur du BIOS, 1 à 46, ou 0 pour aucun.</param>
    /// <param name="displayBits">Bits 5-4 de l'octet 0x14, déjà décalés.</param>
    internal static void Amend(uint8_t[] cmos, int memSize, int driveA, int driveB,
                              int hddType, int displayBits)
    {
        // pcem: amic206.bin:0xad08 et :0xad0e — le SETUP met ces deux-là à zéro avant de
        // redémarrer. 0x0E est l'octet de DIAGNOSTIC, et c'est lui le vecteur entre la
        // détection et l'affichage : le POST y pose ses bits puis relit l'octet pour
        // décider quoi écrire à l'écran. Le remettre à zéro efface les plaintes de
        // l'amorçage précédent ; s'il reste une vraie cause, le POST le repose.
        cmos[0x0E] = 0;
        cmos[0x0F] = 0;

        // RTC_2412. À poser AVANT tout, parce que loadnvr n'écrase 0x0A et 0x0B
        // qu'APRÈS avoir appelé time_internal_set_nvrram, qui décode les heures et le
        // siècle avec le 0x0B DU FICHIER. La référence AMI associe 0x0B = 0, donc le
        // mode 12 heures, à une heure BCD de 0x17 — 23 heures, qui n'existe pas en mode
        // 12 heures. Poser le bit rend la lecture cohérente.
        cmos[0x0B] = 0x02;

        // Quartet haut = A:, quartet bas = B:.
        cmos[0x10] = (uint8_t)((FloppyNibble(driveA) << 4) | FloppyNibble(driveB));

        // LE QUARTET BAS RESTE À ZÉRO, ET CE N'EST PAS UN OUBLI. mfm_init charge les deux
        // unités et le registre de commande fait fatal("Command on non-present drive")
        // (mfm_at.cs) si le BIOS en adresse une qui n'a pas de fichier. Déclarer un D:
        // sans hdd_fn tuerait l'émulateur pendant le POST.
        //
        // 0x0F dans le quartet veut dire « type >= 16, le vrai numéro est en 0x19 » —
        // mesuré dans la ROM AMI, 0xA875-0xA886 : `cmp al,0Eh / jne` sur l'index, puis
        // lecture de 0x19, puis `cmp al,2Eh / ja` qui rejette au-delà du type 47.
        if (hddType <= 0)
        {
                cmos[0x12] = 0;
                cmos[0x19] = 0;
        }
        else if (hddType < 15)
        {
                cmos[0x12] = (uint8_t)(hddType << 4);
                cmos[0x19] = 0;
        }
        else
        {
                cmos[0x12] = 0xF0;
                cmos[0x19] = (uint8_t)hddType;
        }
        cmos[0x1A] = 0; // type étendu de D:, sans objet

        // Octet d'équipement. Bits 7-6 : nombre de lecteurs moins un. Bits 5-4 :
        // l'affichage. Bit 2 : clavier. Bit 0 : au moins un lecteur de disquette.
        // Bit 1 (coprocesseur) reste à zéro — ce dépôt n'a pas de 287.
        var drives = (driveA != 0 ? 1 : 0) + (driveB != 0 ? 1 : 0);
        var equip = (uint8_t)(displayBits | 0x04);
        if (drives > 0)
                equip |= (uint8_t)(0x01 | ((drives - 1) << 6));
        cmos[0x14] = equip;

        // Mémoire de base : toujours 640 Ko dès que la carte en a autant. Le POST la
        // compare à ce qu'il a compté, en unités de 64 Ko (`shr ax,6` en 0x8e73).
        var baseKb = memSize > 640 ? 640 : memSize;
        cmos[0x15] = (uint8_t)(baseKb & 0xFF);
        cmos[0x16] = (uint8_t)(baseKb >> 8);

        // MÉMOIRE ÉTENDUE, DEUX COUPLES DANS LA MÊME UNITÉ : 0x17/0x18 est ce que la
        // configuration DÉCLARE, 0x30/0x31 ce que le POST a TROUVÉ, tous deux en
        // kilo-octets et petit-boutistes. Le test est « déclaré ≠ trouvé ».
        //
        // MESURÉ, PAS DÉDUIT, et le premier modèle était faux. Un amorçage de l'AMI 286
        // à 4 096 Ko laisse 0x30/0x31 = 00 0C, soit 3 072 Ko — la valeur exacte de
        // mem_size - 1024, et non son quotient par 64. La lecture du désassemblage qui
        // faisait de 0x30/0x31 des unités de 64 Ko ne tenait pas devant le fichier.
        //
        // C'est aussi ce qui explique la plainte des deux fichiers livrés par PCem :
        // ami286.nvr déclare 1 024 Ko d'étendue, et sur une machine qui n'en a pas le
        // POST réécrit 0x30/0x31 à zéro. 1024 ≠ 0.
        //
        // Le seuil est `> 1024` et non `>= 1024`, comme mem_alloc : une carte de 1024 Ko
        // pile n'a rien au-dessus de 1 Mo sur un clone NEAT.
        var extKb = memSize > 1024 ? memSize - 1024 : 0;
        cmos[0x17] = (uint8_t)(extKb & 0xFF);
        cmos[0x18] = (uint8_t)(extKb >> 8);
        cmos[0x30] = (uint8_t)(extKb & 0xFF);
        cmos[0x31] = (uint8_t)(extKb >> 8);

        // pcem: amic206.bin:0xacfc-0xad05 — 0x2E en poids FORT, 0x2F en poids faible.
        var sum = Checksum(cmos);
        cmos[0x2E] = (uint8_t)(sum >> 8);
        cmos[0x2F] = (uint8_t)(sum & 0xFF);
    }

    /// <summary>
    /// --make-nvr : lit un fichier de configuration, amende le CMOS de référence de sa
    /// machine, et écrit le fichier de session.
    ///
    /// UN FICHIER EXISTANT EST REFUSÉ, JAMAIS ÉCRASÉ — la règle de --create-hdd et de
    /// --create-floppy. Un CMOS est un état de machine : l'écraser en silence ferait
    /// perdre une configuration réglée au SETUP sans un mot.
    /// </summary>
    internal static int Make(string cfgPath, string outPath, string romsPath, bool force)
    {
        if (!File.Exists(cfgPath))
        {
                Console.Error.WriteLine($"--make-nvr : « {cfgPath} » est introuvable.");
                return 2;
        }

        if (File.Exists(outPath) && !force)
        {
                Console.Error.WriteLine($"--make-nvr : « {outPath} » existe déjà, et n'est pas écrasé.");
                Console.Error.WriteLine("Le supprimer, ou passer --force pour l'accepter.");
                return 2;
        }

        config.config_load(config.CFG_MACHINE, cfgPath);

        var modelName = config.config_get_string(config.CFG_MACHINE, null, "model", "");
        var memSize = config.config_get_int(config.CFG_MACHINE, null, "mem_size", 640);
        var driveA = config.config_get_int(config.CFG_MACHINE, null, "drive_a_type", 0);
        var driveB = config.config_get_int(config.CFG_MACHINE, null, "drive_b_type", 0);
        var gfx = config.config_get_string(config.CFG_MACHINE, null, "gfxcard", "cga");

        // Le type de disque se DÉDUIT de la géométrie du .cfg, il ne se déclare pas :
        // c'est la même géométrie que le contrôleur verra, donc les deux ne peuvent pas
        // diverger. HddImage.TypeFor rend 0 pour une géométrie hors table — et 0 veut
        // dire « aucun disque déclaré », ce qui est le comportement sûr.
        var cyl = config.config_get_int(config.CFG_MACHINE, null, "hdc_cylinders", 0);
        var heads = config.config_get_int(config.CFG_MACHINE, null, "hdc_heads", 0);
        var spt = config.config_get_int(config.CFG_MACHINE, null, "hdc_sectors", 0);
        var hddType = (cyl > 0 && heads > 0 && spt > 0) ? HddImage.TypeFor(cyl, heads, spt) : 0;

        var refName = modelName switch
        {
                "ami286" => "ami286.nvr",
                "ami386" => "ami386.nvr",
                "ibmat" => "at.nvr",
                _ => "",
        };

        if (refName.Length == 0)
        {
                Console.Error.WriteLine($"--make-nvr : la machine « {modelName} » n'a pas de CMOS.");
                Console.Error.WriteLine("Seules ibmat, ami286 et ami386 en ont un : les deux machines à 8088 n'en portent pas.");
                return 2;
        }

        var refPath = Path.Combine(
            paths.resolve_roms_path("nvr/default") is { Length: > 0 } d ? d : "nvr/default", refName);

        if (!File.Exists(refPath))
        {
                Console.Error.WriteLine($"--make-nvr : le CMOS de référence « {refPath} » est introuvable.");
                return 1;
        }

        var cmos = File.ReadAllBytes(refPath);
        if (cmos.Length != 128)
        {
                Console.Error.WriteLine($"--make-nvr : « {refPath} » fait {cmos.Length} octets, 128 attendus.");
                return 1;
        }

        var displayBits = gfx == "cga" ? DisplayCga80 : DisplayOwnRom;

        var before = Checksum(cmos);
        Amend(cmos, memSize, driveA, driveB, hddType, displayBits);
        var after = Checksum(cmos);

        File.WriteAllBytes(outPath, cmos);

        Console.WriteLine($"CMOS écrit : {outPath}, 128 octets, d'après {refPath}.");
        Console.WriteLine($"  machine        {modelName}");
        Console.WriteLine($"  mémoire        {memSize} Ko, dont {(memSize > 1024 ? memSize - 1024 : 0)} au-delà de 1 Mo");
        Console.WriteLine($"  lecteurs       0x10 = {cmos[0x10]:X2}  (A: type PCem {driveA}, B: type PCem {driveB})");
        Console.WriteLine($"  disque dur     0x12 = {cmos[0x12]:X2}, 0x19 = {cmos[0x19]:X2}" +
                          (hddType > 0 ? $"  (type {hddType}, {cyl}x{heads}x{spt})" : "  (aucun)"));
        Console.WriteLine($"  équipement     0x14 = {cmos[0x14]:X2}  (affichage {displayBits:X2}, carte « {gfx} »)");
        Console.WriteLine($"  somme          0x{before:X4} -> 0x{after:X4}, en 0x2E/0x2F");

        // La somme nulle est rejetée par les deux BIOS : le dire ici plutôt que de
        // laisser chercher pourquoi le POST se plaint d'un fichier qui a l'air juste.
        if (after == 0)
                Console.Error.WriteLine("  *** SOMME NULLE : les deux BIOS la refusent, quoi que disent 0x2E et 0x2F. ***");

        return 0;
    }
}
