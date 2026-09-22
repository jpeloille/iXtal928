// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/wx-ui/wx-config.c:1427-1440 (create_drive_raw)
//         + :1295-1302 (hd_types[46]), :1580-1586 (libellé d'un type),
//         :1607-1633 (les quatre validations), :1682-1683 (message de fin),
//         :1722-1731 (géométrie -> numéro de type)
// STATUS: host
//
// FABRIQUER UNE IMAGE DE DISQUE DUR VIERGE.
//
// PCem sait le faire, et pas là où on le cherche : `wx-createdisc.cc` ne fait que des
// disquettes ; le disque dur passe par le bouton « New… » de la page Hard disc de la
// boîte de configuration, donc par `hdnew_dlgproc`. Ce fichier en est le pendant —
// seule l'enveloppe wxWidgets disparaît, comme pour Host/SdlMenu.cs.
//
// Trois raisons de ne pas laisser l'utilisateur fabriquer le fichier à la main :
//
//   1. La géométrie n'est pas libre. xebec_set_switches exige 17 secteurs par piste et
//      l'un de quatre couples (cylindres, têtes). Hors de là la carte se contente d'un
//      warning(), laisse ses interrupteurs à zéro — le disque est alors annoncé en
//      type 0, pas absent — et le POST diverge. RIEN NE LE REFUSE.
//   2. 17 est câblé dans l'ADRESSAGE, pas seulement dans la validation
//      (mfm_xebec.cs, xebec_get_sector). Un hdc_sectors autre que 17 fait diverger la
//      capacité déclarée de la capacité adressable.
//   3. Une image créée implicitement fait ZÉRO octet : hdd_load_ext la crée vide et ne
//      la pré-alloue jamais, et hdd_read_sectors ignore la fin de fichier. Lire un
//      secteur jamais écrit rend le contenu résiduel du tampon de la carte, pas des
//      zéros. Les deux côtés du diff font la même chose, donc ce n'est pas un défaut de
//      fidélité — c'est un défaut de reproductibilité, et il rend le garde-fou
//      « INCHANGÉE depuis le départ » de BootDiff aveugle à son propre point de départ.
//
// Un seul endroit qui crée, appelé par --create-hdd ET par le menu Ctrl+F12 : deux
// appelants avec chacun sa table, c'est le défaut que § M12 a dû corriger entre
// BootTest et BootDiff.

namespace iXtal26.Host;

internal static class HddImage
{
    // pcem: wx-config.c:27 — le commentaire de PCem dit pourquoi ce nombre :
    //   « Award 430VX won't POST with a larger drive ».
    internal const int MAX_CYLINDERS = 265264;

    // pcem: wx-config.c:1586 — les 46 types du BIOS n'ont pas de champ « secteurs » :
    // 17 y est implicite, écrit en dur dans le calcul de taille comme dans la
    // reconnaissance inverse (:1727). C'est aussi la valeur que le Fixed Disk Adapter
    // est seul à savoir adresser.
    internal const int TypeSectorsPerTrack = 17;

    // pcem: wx-config.c:1295-1302 — la table des types de disque du BIOS, 46 couples
    // (cylindres, têtes), verbatim et dans l'ordre. L'indice 0 est le « Type 01 ».
    //
    // L'ENTRÉE 14 VAUT (0, 0), et ce n'est pas une coquille : c'est le type 15, réservé
    // dans la table de l'IBM AT. PCem la laisse dans sa liste déroulante, où elle
    // affiche « size=0MB » ; ici elle est refusée à la création, parce qu'un fichier de
    // zéro octet est exactement ce que ce fichier existe pour éviter.
    internal static readonly (int cylinders, int heads)[] hd_types =
    {
        (306, 4),   (615, 4),   (615, 6),  (940, 8),  (940, 6),  (615, 4),  (462, 8),  (733, 5), (900, 15), (820, 3),
        (855, 5),   (855, 7),   (306, 8),  (733, 7),  (0, 0),    (612, 4),  (977, 5),  (977, 7), (1024, 7), (733, 5),
        (733, 7),   (733, 5),   (306, 4),  (925, 7),  (925, 9),  (754, 7),  (754, 11), (699, 7), (823, 10), (918, 7),
        (1024, 11), (1024, 15), (1024, 5), (612, 2),  (1024, 9), (1024, 8), (615, 8),  (987, 3), (462, 7),  (820, 6),
        (977, 5),   (981, 5),   (830, 7),  (830, 10), (917, 15), (1224, 15),
    };

    /// <summary>
    /// Taille en octets d'une géométrie. `long` et non `int` : le C fait ce calcul deux
    /// fois, en int pour les 46 types (wx-config.c:1586, où il ne peut pas déborder — le
    /// plus gros fait 159 805 440 octets) et en uint64_t pour la saisie libre
    /// (:1717-1719, où il déborderait, 265264 x 16 x 63 x 512 valant 136 Go). Un seul
    /// calcul en 64 bits est donc le pendant des deux, pas une déviation.
    /// </summary>
    internal static long SizeOf(int cylinders, int heads, int spt)
        => (long)cylinders * heads * spt * 512;

    /// <summary>
    /// pcem: wx-config.c:1580-1586. Le libellé exact de PCem, y compris son espace avant
    /// le deux-points et ses mégaoctets tronqués par division entière.
    /// </summary>
    /// <param name="type">Numéro de type BIOS, 1 à 46.</param>
    internal static string Label(int type)
    {
        (int cylinders, int heads) = hd_types[type - 1];

        return $"Type {type:D2} : cylinders={cylinders}, heads={heads}, " +
               $"size={SizeOf(cylinders, heads, TypeSectorsPerTrack) / (1024 * 1024)}MB";
    }

    /// <summary>
    /// pcem: wx-config.c:1722-1731 — le numéro de type BIOS d'une géométrie, ou 0 pour
    /// « Custom type ».
    ///
    /// La boucle s'arrête au PREMIER type qui correspond, et c'est observable : la table
    /// contient des doublons, (306, 4) étant à la fois le type 01 et le type 23, et
    /// (615, 4) à la fois le type 02 et le type 06. Une géométrie de 306 x 4 est donc
    /// nommée « type 01 » et jamais « type 23 ».
    /// </summary>
    internal static int TypeFor(int cylinders, int heads, int spt)
    {
        for (int c = 1; c <= 46; c++)
        {
            if (spt == TypeSectorsPerTrack && heads == hd_types[c - 1].heads &&
                cylinders == hd_types[c - 1].cylinders)
                    return c;
        }

        return 0;
    }

    /// <summary>
    /// Indice dans xebec_hd_types, ou -1 si le Fixed Disk Adapter n'accepte pas cette
    /// géométrie. Pendant exact du test de xebec_set_switches (mfm_xebec.c:737-741), et
    /// il lit LA MÊME table : la recopier ici la ferait dériver de la carte.
    ///
    /// Six des 46 types passent, pas quatre — les doublons de la table BIOS font que
    /// (306, 4) vaut pour les types 01 et 23, et (615, 4) pour les types 02 et 06.
    /// D'où un calcul, jamais une liste écrite à la main.
    /// </summary>
    internal static int XebecSwitch(int cylinders, int heads, int spt)
    {
        if (spt != 17)
            return -1;

        for (int c = 0; c < Mfm.mfm_xebec.xebec_hd_types.Length; c++)
        {
            if (heads == Mfm.mfm_xebec.xebec_hd_types[c].hpc &&
                cylinders == Mfm.mfm_xebec.xebec_hd_types[c].tracks)
                    return c;
        }

        return -1;
    }

    /// <summary>
    /// pcem: wx-config.c:1607-1633. Les quatre refus du dialogue, dans l'ordre, avec
    /// leurs messages VERBATIM — ce sont des chaînes de PCem, pas les nôtres.
    ///
    /// Noter ce que PCem ne teste PAS : ni zéro, ni négatif. Une géométrie nulle passe
    /// ses quatre bornes et produit un fichier vide. On ajoute donc un cinquième refus,
    /// marqué DEVIATION, parce qu'un fichier de zéro octet est le défaut même que ce
    /// fichier existe pour empêcher (voir l'en-tête, point 3).
    /// </summary>
    internal static bool Validate(int cylinders, int heads, int spt, out string error)
    {
        if (spt > 63)
        {
            error = "Drive has too many sectors (maximum is 63)";
            return false;
        }
        if (heads > 16)
        {
            error = "Drive has too many heads (maximum is 16)";
            return false;
        }
        if (cylinders > MAX_CYLINDERS)
        {
            error = $"Drive has too many cylinders (maximum is {MAX_CYLINDERS})";
            return false;
        }

        // DEVIATION: PCem n'a pas ce test. Ses trois bornes sont des plafonds ; le
        //   plancher n'existe pas, et le type 15 de sa propre table — (0, 0) — le
        //   traverse sans un mot pour produire un fichier de zéro octet.
        if (cylinders <= 0 || heads <= 0 || spt <= 0)
        {
            error = "Drive has a null geometry (cylinders, heads and sectors must all be positive)";
            return false;
        }

        error = "";
        return true;
    }

    /// <summary>
    /// pcem: wx-config.c:1427-1440 (create_drive_raw) + :1645-1651 (l'ouverture) et
    /// :1682-1683 (le message de fin).
    ///
    /// Toute la création : un tampon de 512 octets à zéro, écrit cylindres x têtes x
    /// secteurs fois. Structurellement la même chose que wx-createdisc.cc:62-73, donc
    /// que SdlMenu.CreateBlank — et pour la même raison, l'écriture est RÉELLE secteur
    /// par secteur et non un SetLength : SetLength donnerait un fichier sparse, même
    /// taille apparente et blocs non alloués, là où le fichier doit être comparable
    /// octet à octet par CompareImages.
    ///
    /// Rien n'est écrit dans le secteur 0 : ni MBR, ni table de partition, ni signature
    /// 55AA. C'est FDISK, dans la machine émulée, qui les pose — et le message de fin de
    /// PCem le dit lui-même.
    /// </summary>
    internal static bool Create(string path, int cylinders, int heads, int spt, out string message)
    {
        // DEVIATION: PCem ouvre en « wb » (wx-config.c:1647), donc ÉCRASE en silence.
        //   Il peut se le permettre : son chemin vient d'un sélecteur de fichiers, dont
        //   le système demande confirmation. Ici il vient d'un argument ou d'un nom
        //   calculé, et rien ne redemanderait. CreateNew refuse — même arbitrage que
        //   SdlMenu.CreateBlank, et même raison qu'à FreeName : jamais d'écrasement
        //   silencieux, qui est précisément le défaut d'une boîte « Enregistrer sous ».
        try
        {
            using (FileStream f = new FileStream(path, FileMode.CreateNew, FileAccess.Write))
            {
                uint8_t[] buf = new uint8_t[512];

                for (int c = 0; c < (cylinders * heads * spt); c++)
                        f.Write(buf, 0, 512);
            }
        }
        catch (IOException ex)
        {
            // pcem: wx-config.c:1649 — « Can't open file for write ». PCem n'a qu'un
            //   message pour tous les échecs d'ouverture ; on y ajoute la raison du
            //   système, sans quoi « existe déjà » et « disque plein » se confondent.
            message = $"Can't open file for write : {ex.Message}";
            return false;
        }
        catch (UnauthorizedAccessException ex)
        {
            message = $"Can't open file for write : {ex.Message}";
            return false;
        }

        // pcem: wx-config.c:1682-1683, verbatim. PCem ne monte pas l'image créée : il
        //   dit quoi faire ensuite, et c'est tout ce qu'il y a à faire.
        message = "Drive created, remember to partition and format the new drive.";
        return true;
    }

    /// <summary>
    /// Le bloc de clés à coller dans un fichier .cfg. Sans pendant chez PCem, qui écrit
    /// directement dans sa configuration depuis le dialogue (wx-config.c:2005-2036) —
    /// ici le fichier reste maître, et il porte de la prose qu'un écrivain de
    /// configuration abîmerait.
    ///
    /// Le préfixe est `hdc_`, soit le lecteur C: : les préfixes de ces clés sont des
    /// LETTRES DE LECTEUR DOS et non des numéros de contrôleur (Disc/hdd.cs).
    /// </summary>
    internal static string ConfigBlock(string path, int cylinders, int heads, int spt)
        => $"hdd_controller = mfm_xebec\n" +
           $"hdc_sectors = {spt}\n" +
           $"hdc_heads = {heads}\n" +
           $"hdc_cylinders = {cylinders}\n" +
           $"hdc_fn = {path}\n";
}
