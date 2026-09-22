// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — capacité hôte. PCem ne sait pas faire ça.
// STATUS: host
//
// FABRIQUER UNE DISQUETTE FORMATÉE, ET Y DÉPOSER DES FICHIERS DU DISQUE HÔTE.
//
// Ce que le dépôt savait déjà faire s'arrête juste avant : SdlMenu.CreateBlank, pendant
// de wx-createdisc.cc:62-73, écrit N x 512 octets NULS — pas de BPB, pas de FAT, pas de
// signature. C'est fidèle, et c'est voulu : une image vierge est l'équivalent d'une
// disquette formatée bas niveau et logiquement vide. Le seul chemin vers une disquette
// utilisable passait donc par FORMAT sous DOS, DANS la machine émulée — ce qui ne fait
// entrer aucun octet venu de l'hôte.
//
// D'où ce fichier, qui est l'autre moitié : il pose le système de fichiers que DOS
// attend, et il y écrit des fichiers lus sur le disque hôte. SdlMenu.CreateBlank n'est
// pas touché — son zéro-remplissage est une propriété de parité avec PCem, adossée à un
// commentaire qui la garantit ; y injecter un BPB réécrirait une transcription.
//
// RIEN ICI N'EST DEVINÉ. Les quatre BPB sont lus dans le FORMAT.COM de PC DOS 2.00
// lui-même (voir la table ci-dessous), et recoupés contre deux disquettes réellement
// formatées par DOS que le dépôt porte déjà. Écrire un système de fichiers de mémoire
// produit une image que DOS lit de travers en silence, ce qui est exactement le genre de
// défaut que ce dépôt refuse.
//
// CONSÉQUENCE À CONNAÎTRE, et elle contredit à moitié SdlMenu.cs:530-536. Ce commentaire
// garantit que les cinq lectures de BPB d'img_load (disc_img.cs:223-232) rendent 0 sur
// une image vierge, donc que la garde de disc_img.cs:242 force la branche de devinette
// par TAILLE. C'est toujours vrai de CreateBlank, et c'est FAUX des images d'ici : notre
// BPB est valide, donc img_load prend la branche BPB (disc_img.cs:309-392). Les deux
// doivent rendre la même géométrie pour les quatre formats — c'est un invariant, il est
// vérifié en § M14, et si elles divergent c'est l'image qui est fausse, pas le chargeur.

using System.Globalization;

namespace iXtal26.Host;

internal static class FatImage
{
    internal const int SectorSize = 512;

    // L'octet de remplissage de FORMAT. Ce n'est PAS un choix : c'est celui que DOS 2.00
    // passe en params[4] de la commande FORMAT TRACK du FDC, et qu'on retrouve dans la
    // zone de données comme dans le répertoire racine des deux images de référence.
    // SdlMenu.cs:528-530 dit « le 0xF6 est celui de l'invité » — vrai pour une image
    // vierge, et sans objet ici, où c'est NOUS le formateur.
    internal const uint8_t FillByte = 0xF6;

    // Marqueur de fin de répertoire. Le premier octet d'une entrée : 0x00 = fin, plus
    // rien après ; 0xE5 = entrée effacée, réutilisable.
    internal const uint8_t DirEnd = 0x00;
    internal const uint8_t DirErased = 0xE5;

    // Fin de chaîne dans la FAT. DOS 2.00 écrit 0xFFF (vérité terrain) ; en LECTURE tout
    // ce qui vaut 0xFF8 ou plus est une fin. 0xFF7 est un cluster défectueux : il n'est
    // ni libre ni allouable, et il ne termine rien.
    internal const int ClusterEnd = 0xFFF;
    internal const int ClusterEndMin = 0xFF8;
    internal const int ClusterBad = 0xFF7;

    /// <summary>
    /// Un format de disquette, décrit par les champs de son BPB. Les noms sont ceux des
    /// offsets du secteur d'amorce, pas des abréviations de circonstance.
    /// </summary>
    internal struct Format
    {
        internal string Name;               // libellé montré à l'utilisateur
        internal string Stem;               // radical du nom de fichier engendré
        internal int SectorsPerCluster;     // 0x0D
        internal int ReservedSectors;       // 0x0E
        internal int NumberOfFats;          // 0x10
        internal int RootEntries;           // 0x11
        internal int TotalSectors;          // 0x13
        internal int MediaDescriptor;       // 0x15
        internal int SectorsPerFat;         // 0x16
        internal int SectorsPerTrack;       // 0x18
        internal int Heads;                 // 0x1A
    }

    // LES QUATRE BPB, LUS DANS FORMAT.COM DE PC DOS 2.00.
    //
    // Le binaire porte une table de quatre enregistrements de 18 octets, à l'offset
    // 0xC7FA de os/pcdos20/pcdos20b.img, immédiatement suivie de « Formatting...$ » :
    //
    //   01 01 00 02 40 00 40 01 fe 01 00 08 00 01 00 00 00 00   160 Ko
    //   02 01 00 02 70 00 80 02 ff 01 00 08 00 02 00 00 00 00   320 Ko
    //   01 01 00 02 40 00 68 01 fc 02 00 09 00 01 00 00 00 00   180 Ko
    //   02 01 00 02 70 00 d0 02 fd 02 00 09 00 02 00 00 00 00   360 Ko
    //
    // C'est le BPB privé de son mot « octets par secteur », soit les offsets 0x0D à 0x1D
    // du secteur d'amorce, plus un octet de remplissage.
    //
    // Les deux DERNIERS enregistrements reproduisent OCTET POUR OCTET les secteurs 0 de
    // os/pcdos20/pcdos20b.img (180 Ko) et os/vierge-360k.img (360 Ko), deux disquettes
    // réellement formatées par DOS 2.00. Les deux premiers sont donc du même niveau de
    // preuve, et c'est ce qui rend le 160 Ko et le 320 Ko non conjecturaux — aucune de
    // ces deux tailles n'existe sur disque dans ce dépôt.
    //
    // Les quatre sont aussi les SEULS formats que le lecteur 5,25" DD du 5150 sait lire :
    // drive_types[1] (fdd.cs:92-94) ne porte que FLAG_HOLE0 et max_track = 41, donc
    // fdd_can_read_medium (fdd.cs:219-233) refuse toute image HD ou ED. Même liste, et
    // pour la même raison, que SdlMenu.DiscFormats.
    //
    // L'ordre est celui de l'affichage, pas celui de la table de FORMAT (qui range les
    // 8 secteurs d'abord).
    internal static readonly Format[] Formats =
    {
        new Format { Name = "160 Ko   8 sect. x 40 pistes x 1 face",  Stem = "160k",
                     SectorsPerCluster = 1, ReservedSectors = 1, NumberOfFats = 2,
                     RootEntries = 64,  TotalSectors = 320, MediaDescriptor = 0xFE,
                     SectorsPerFat = 1, SectorsPerTrack = 8, Heads = 1 },

        new Format { Name = "180 Ko   9 sect. x 40 pistes x 1 face",  Stem = "180k",
                     SectorsPerCluster = 1, ReservedSectors = 1, NumberOfFats = 2,
                     RootEntries = 64,  TotalSectors = 360, MediaDescriptor = 0xFC,
                     SectorsPerFat = 2, SectorsPerTrack = 9, Heads = 1 },

        new Format { Name = "320 Ko   8 sect. x 40 pistes x 2 faces", Stem = "320k",
                     SectorsPerCluster = 2, ReservedSectors = 1, NumberOfFats = 2,
                     RootEntries = 112, TotalSectors = 640, MediaDescriptor = 0xFF,
                     SectorsPerFat = 1, SectorsPerTrack = 8, Heads = 2 },

        new Format { Name = "360 Ko   9 sect. x 40 pistes x 2 faces", Stem = "360k",
                     SectorsPerCluster = 2, ReservedSectors = 1, NumberOfFats = 2,
                     RootEntries = 112, TotalSectors = 720, MediaDescriptor = 0xFD,
                     SectorsPerFat = 2, SectorsPerTrack = 9, Heads = 2 },
    };

    // ---------------------------------------------------------------- géométrie dérivée
    //
    // CALCULÉE, jamais écrite en dur : le BPB est la seule source, et une constante posée
    // à côté de lui finirait par en diverger. Les valeurs pour les quatre formats sont
    // 313 / 351 / 315 / 354 clusters, et c'est § M14 qui les mesure.

    internal static int RootSectors(Format f) => f.RootEntries * 32 / SectorSize;

    internal static int DataStart(Format f)
        => f.ReservedSectors + f.NumberOfFats * f.SectorsPerFat + RootSectors(f);

    internal static int ClusterCount(Format f)
        => (f.TotalSectors - DataStart(f)) / f.SectorsPerCluster;

    /// <summary>Le plus grand numéro de cluster valide. Les clusters commencent à 2.</summary>
    internal static int MaxCluster(Format f) => ClusterCount(f) + 1;

    internal static int BytesPerCluster(Format f) => f.SectorsPerCluster * SectorSize;

    /// <summary>Premier secteur du cluster <paramref name="cluster"/>, qui vaut 2 ou plus.</summary>
    internal static int ClusterSector(Format f, int cluster)
        => DataStart(f) + (cluster - 2) * f.SectorsPerCluster;

    internal static long ImageSize(Format f) => (long)f.TotalSectors * SectorSize;

    /// <summary>
    /// Les quatre contrôles de cohérence interne de la table. Ils ne peuvent pas échouer
    /// sur les quatre formats ci-dessus — c'est justement pourquoi ils sont écrits : ils
    /// attrapent une CINQUIÈME entrée ajoutée un jour sans recalculer ce qu'elle implique.
    /// </summary>
    internal static bool ValidateFormat(Format f, out string error)
    {
        if (f.RootEntries * 32 % SectorSize != 0)
        {
            error = $"{f.Name} : le repertoire racine ({f.RootEntries} entrees) " +
                    "ne fait pas un nombre entier de secteurs.";
            return false;
        }

        if (f.TotalSectors != f.Heads * 40 * f.SectorsPerTrack)
        {
            error = $"{f.Name} : {f.TotalSectors} secteurs au total, mais " +
                    $"{f.Heads} x 40 x {f.SectorsPerTrack} en fait " +
                    $"{f.Heads * 40 * f.SectorsPerTrack}.";
            return false;
        }

        if (DataStart(f) >= f.TotalSectors)
        {
            error = $"{f.Name} : la zone de donnees commence au secteur {DataStart(f)}, " +
                    $"hors des {f.TotalSectors} secteurs du support.";
            return false;
        }

        // La FAT doit pouvoir décrire ses propres clusters : (maxCluster + 1) entrées de
        // 12 bits, arrondies à l'octet supérieur. C'est ce contrôle qui explique pourquoi
        // le 180 Ko a DEUX secteurs de FAT là où le 160 Ko n'en a qu'un — 353 entrées
        // font 530 octets, soit 18 de trop pour un secteur.
        int need = ((MaxCluster(f) + 1) * 3 + 1) / 2;

        if (need > f.SectorsPerFat * SectorSize)
        {
            error = $"{f.Name} : la FAT tient sur {f.SectorsPerFat * SectorSize} octets, " +
                    $"mais {MaxCluster(f) + 1} entrees de 12 bits en demandent {need}.";
            return false;
        }

        error = "";
        return true;
    }

    /// <summary>Indice dans <see cref="Formats"/> d'un nom donné à la ligne de commande
    /// (« 360k », « 360 », « 360ko »), ou -1.</summary>
    internal static int FormatIndex(string name)
    {
        string want = name.Trim().ToUpperInvariant();

        if (want.EndsWith("KO", StringComparison.Ordinal))
            want = want.Substring(0, want.Length - 2);
        else if (want.EndsWith("K", StringComparison.Ordinal))
            want = want.Substring(0, want.Length - 1);

        for (int c = 0; c < Formats.Length; c++)
        {
            string stem = Formats[c].Stem.ToUpperInvariant();

            if (want == stem.Substring(0, stem.Length - 1))
                return c;
        }

        return -1;
    }

    /// <summary>Le format dont le BPB correspond à celui lu dans une image, ou -1.</summary>
    internal static int FormatMatching(int spc, int rootEntries, int totalSectors,
                                       int media, int spf, int spt, int heads)
    {
        for (int c = 0; c < Formats.Length; c++)
        {
            if (Formats[c].SectorsPerCluster == spc && Formats[c].RootEntries == rootEntries &&
                Formats[c].TotalSectors == totalSectors && Formats[c].MediaDescriptor == media &&
                Formats[c].SectorsPerFat == spf && Formats[c].SectorsPerTrack == spt &&
                Formats[c].Heads == heads)
                    return c;
        }

        return -1;
    }

    // ------------------------------------------------------------- le secteur d'amorce

    /// <summary>
    /// La chaîne OEM des offsets 0x03 à 0x0A. DOS 2.00 ne lit JAMAIS ce champ, et rien
    /// dans ce dépôt non plus : il sert à ce qu'un xxd distingue une image fabriquée ici
    /// d'une image sortie de FORMAT. Huit caractères exactement.
    /// </summary>
    internal const string OemName = "IXTAL1.0";

    /// <summary>Offset du talon d'amorce dans le secteur : juste après le BPB de DOS 2.00,
    /// qui s'arrête à 0x1D, et à la cible du saut court EB 2C.</summary>
    internal const int StubOffset = 0x2E;

    // LE TALON D'AMORCE, assemblé puis RE-DÉSASSEMBLÉ avant d'être figé ici.
    //
    // Il est obligatoire, pas décoratif, et c'est le BIOS du 5150 qui l'impose. Son
    // INT 19h (roms/ibmpc/pc102782.bin, désassemblé en 0xE701) lit un secteur par
    // INT 13h puis fait « jae » vers un « jmp 0000:7C00 » INCONDITIONNEL. Il ne compare
    // RIEN à 0xAA55 — les deux occurrences de cette valeur dans la ROM appartiennent au
    // balayage des ROM d'extension, pas à l'amorçage. Un secteur 0 rempli de zéros
    // s'exécuterait donc en « add [bx+si],al » et partirait dans le décor.
    //
    // On n'embarque PAS les 466 octets du chargeur d'IBM, qu'on a pourtant sous la main :
    // .gitignore exclut os/* au motif que ce sont des logiciels sous copyright, et les
    // recopier dans un .cs versionné commettrait exactement ce que cette règle refuse.
    //
    // Source assemblée (as --32, .code16, lié en 0x7C2E), vérifiée par
    // objdump -D -b binary -m i8086 :
    //
    //      cli                      7c2e: fa
    //      xorw %ax,%ax             7c2f: 31 c0
    //      movw %ax,%ss             7c31: 8e d0
    //      movw $0x7C00,%sp         7c33: bc 00 7c      pile juste sous le secteur chargé
    //      movw %ax,%ds             7c36: 8e d8
    //      sti                      7c38: fb
    //      movw $msg,%si            7c39: be 52 7c      <- OPÉRANDE RECALCULÉE, voir plus bas
    //  next:
    //      lodsb                    7c3c: ac
    //      orb  %al,%al             7c3d: 08 c0
    //      jz   waitkey             7c3f: 74 09
    //      movb $0x0E,%ah           7c41: b4 0e         téléscripteur BIOS
    //      movw $0x0007,%bx         7c43: bb 07 00      page 0, attribut 7
    //      int  $0x10               7c46: cd 10
    //      jmp  next                7c48: eb f2
    //  waitkey:
    //      xorw %ax,%ax             7c4a: 31 c0
    //      int  $0x16               7c4c: cd 16         attendre une touche
    //      int  $0x19               7c4e: cd 19         réamorcer
    //  hang:
    //      jmp  hang                7c50: eb fe         si INT 19h revenait, ne pas divaguer
    //
    // INT 19h relit la même disquette non système et réaffiche : boucle infinie VOULUE,
    // identique à celle du vrai chargeur d'IBM. Une campagne --slices ou --headless qui
    // amorcerait sur une image d'ici ne se terminerait jamais d'elle-même.
    private static readonly uint8_t[] BootStub =
    {
        0xFA, 0x31, 0xC0, 0x8E, 0xD0, 0xBC, 0x00, 0x7C, 0x8E, 0xD8, 0xFB,
        0xBE, 0x00, 0x00,                                   // movw $msg,%si — opérande posée par BuildBootSector
        0xAC, 0x08, 0xC0, 0x74, 0x09, 0xB4, 0x0E, 0xBB, 0x07, 0x00, 0xCD, 0x10, 0xEB, 0xF2,
        0x31, 0xC0, 0xCD, 0x16, 0xCD, 0x19, 0xEB, 0xFE,
    };

    /// <summary>Indice, dans <see cref="BootStub"/>, de l'opérande 16 bits du
    /// « movw $msg,%si ». Le message suit immédiatement le talon, et son adresse est donc
    /// CALCULÉE à partir de la longueur du talon — jamais figée. Un message retouché
    /// déplacerait la cible, et le talon afficherait du BPB.</summary>
    private const int StubMessageOperand = 12;

    /// <summary>
    /// Le message du talon. ASCII pur, sans accent : il s'affiche par le téléscripteur du
    /// BIOS en page de code 437, qui n'a pas les accentués d'UTF-8, et DOS n'est pas là
    /// pour traduire. Terminé par un zéro, que la boucle du talon teste.
    /// </summary>
    private const string BootMessage =
        "\r\nDisquette non systeme\r\nRemplacer et frapper une touche\r\n";

    /// <summary>
    /// Le secteur 0 complet : saut, OEM, BPB, talon, message, signature.
    ///
    /// La signature 55AA en 0x1FE est écrite bien que le BIOS du 5150 ne la lise pas :
    /// c'est ce que porte la vérité terrain, et tout autre consommateur — montage Linux,
    /// outils tiers — la cherche.
    /// </summary>
    internal static uint8_t[] BuildBootSector(Format f)
    {
        uint8_t[] s = new uint8_t[SectorSize];

        // EB 2C 90 : saut court vers 0x2E, puis nop. Un EB compte son déplacement depuis
        // l'octet QUI SUIT ses deux octets, soit 0x02 ici : 0x02 + 0x2C = 0x2E. Même
        // encodage que la vérité terrain, où DOS 2.00 loge ses constantes de chargeur
        // dans le trou 0x1E-0x2D que le saut enjambe.
        s[0x00] = 0xEB;
        s[0x01] = (uint8_t)(StubOffset - 2);
        s[0x02] = 0x90;

        for (int c = 0; c < 8; c++)
            s[0x03 + c] = (uint8_t)OemName[c];

        PutWord(s, 0x0B, SectorSize);
        s[0x0D] = (uint8_t)f.SectorsPerCluster;
        PutWord(s, 0x0E, f.ReservedSectors);
        s[0x10] = (uint8_t)f.NumberOfFats;
        PutWord(s, 0x11, f.RootEntries);
        PutWord(s, 0x13, f.TotalSectors);
        s[0x15] = (uint8_t)f.MediaDescriptor;
        PutWord(s, 0x16, f.SectorsPerFat);
        PutWord(s, 0x18, f.SectorsPerTrack);
        PutWord(s, 0x1A, f.Heads);
        PutWord(s, 0x1C, 0);        // secteurs cachés : une disquette n'est pas partitionnée

        for (int c = 0; c < BootStub.Length; c++)
            s[StubOffset + c] = BootStub[c];

        // L'adresse du message, calculée depuis la longueur du talon. 0x7C00 est l'adresse
        // de chargement imposée par l'INT 19h du BIOS.
        int msg = StubOffset + BootStub.Length;

        PutWord(s, StubOffset + StubMessageOperand, 0x7C00 + msg);

        for (int c = 0; c < BootMessage.Length; c++)
            s[msg + c] = (uint8_t)BootMessage[c];

        s[msg + BootMessage.Length] = 0x00;

        s[0x1FE] = 0x55;
        s[0x1FF] = 0xAA;

        return s;
    }

    internal static void PutWord(uint8_t[] buf, int offset, int value)
    {
        buf[offset] = (uint8_t)(value & 0xFF);
        buf[offset + 1] = (uint8_t)((value >> 8) & 0xFF);
    }

    internal static int GetWord(uint8_t[] buf, int offset)
        => buf[offset] | (buf[offset + 1] << 8);

    // --------------------------------------------------------- l'empaquetage FAT12
    //
    // Deux entrées de 12 bits pour trois octets. L'entrée n commence à l'octet
    // n + (n >> 1), soit n * 3 / 2 : les entrées paires prennent l'octet entier puis le
    // quartet BAS du suivant, les impaires le quartet HAUT du premier puis l'octet entier.
    //
    // C'EST LE SITE DE BUG DE TOUT CE FICHIER. Les deux branches d'écriture partagent un
    // octet avec leur voisine, donc chacune doit être une lecture-modification-écriture.
    // Écrire « fat[off + 1] = v >> 8 » en branche paire efface le quartet bas de l'entrée
    // impaire suivante ; écrire « fat[off] = v << 4 » en branche impaire efface le quartet
    // haut de l'entrée paire précédente. Dans les deux cas le symptôme n'apparaît que sur
    // le fichier D'À CÔTÉ, donc longtemps après, et l'image reste plausible à l'œil.
    //
    // Vérité terrain, FAT1 de os/vierge-360k.img :
    //   fd ff ff 03 40 00 05 60 00 07 80 00 09 a0 00 0b c0 00 0d e0 00 0f 00 01 11 f0 ff
    // qui se décode en : entrée 0 = 0xFFD (le descripteur de média en octet bas),
    // entrée 1 = 0xFFF, entrées 2 à 16 = n + 1, entrée 17 = 0xFFF. C'est la chaîne de
    // BASIC.COM, et elle couvre les deux alignements sur toute sa longueur.

    internal static int FatGet(uint8_t[] fat, int n)
    {
        int off = n + (n >> 1);
        int w = fat[off] | (fat[off + 1] << 8);

        return ((n & 1) != 0) ? (w >> 4) : (w & 0x0FFF);
    }

    internal static void FatSet(uint8_t[] fat, int n, int v)
    {
        int off = n + (n >> 1);

        if ((n & 1) == 0)
        {
            fat[off] = (uint8_t)(v & 0xFF);
            fat[off + 1] = (uint8_t)((fat[off + 1] & 0xF0) | ((v >> 8) & 0x0F));
        }
        else
        {
            fat[off] = (uint8_t)((fat[off] & 0x0F) | ((v << 4) & 0xF0));
            fat[off + 1] = (uint8_t)((v >> 4) & 0xFF);
        }
    }

    /// <summary>
    /// Une FAT neuve : les deux entrées réservées, le reste à zéro (donc libre).
    /// L'entrée 0 porte le descripteur de média en octet bas — DOS 2.00 s'en sert pour
    /// identifier le support, et elle doit donc rester cohérente avec l'offset 0x15 du
    /// secteur d'amorce.
    /// </summary>
    internal static uint8_t[] BuildFat(Format f)
    {
        uint8_t[] fat = new uint8_t[f.SectorsPerFat * SectorSize];

        FatSet(fat, 0, 0xF00 | f.MediaDescriptor);
        FatSet(fat, 1, ClusterEnd);

        return fat;
    }

    // ------------------------------------------------------------------- la création

    /// <summary>
    /// Fabrique l'image : secteur d'amorce, les copies de FAT, le répertoire racine, la
    /// zone de données.
    ///
    /// L'écriture est RÉELLE secteur par secteur et non un SetLength, pour la même raison
    /// qu'à SdlMenu.CreateBlank et HddImage.Create : SetLength donnerait un fichier
    /// sparse, même taille apparente et blocs non alloués, là où l'image doit être
    /// comparable octet à octet.
    ///
    /// CreateNew, donc jamais d'écrasement silencieux — même arbitrage que FreeName.
    /// </summary>
    internal static bool Create(string path, Format f, out string message)
    {
        if (!ValidateFormat(f, out message))
            return false;

        try
        {
            using (FileStream file = new FileStream(path, FileMode.CreateNew, FileAccess.Write))
            {
                file.Write(BuildBootSector(f), 0, SectorSize);

                uint8_t[] fat = BuildFat(f);

                for (int c = 0; c < f.NumberOfFats; c++)
                    file.Write(fat, 0, fat.Length);

                // Le répertoire racine, rempli de l'octet de FORMAT — À UNE EXCEPTION
                // PRÈS, et elle est load-bearing : l'octet 0 de la PREMIÈRE entrée porte
                // le marqueur de fin de répertoire. Sans lui, DOS lirait une entrée dont
                // le nom commence par 0xF6, qui n'est ni 0x00 ni 0xE5, donc un fichier.
                // Un DIR sur une disquette neuve annoncerait du charabia.
                uint8_t[] root = new uint8_t[RootSectors(f) * SectorSize];

                for (int c = 0; c < root.Length; c++)
                    root[c] = FillByte;

                root[0] = DirEnd;
                file.Write(root, 0, root.Length);

                uint8_t[] sector = new uint8_t[SectorSize];

                for (int c = 0; c < SectorSize; c++)
                    sector[c] = FillByte;

                for (int c = DataStart(f); c < f.TotalSectors; c++)
                    file.Write(sector, 0, SectorSize);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            message = $"ecriture de {Path.GetFileName(path)} impossible : {ex.Message}";
            return false;
        }

        message = $"{Path.GetFileName(path)} cree : {f.Name}, " +
                  $"{ClusterCount(f)} clusters de {BytesPerCluster(f)} octets, " +
                  $"{f.RootEntries} entrees de repertoire.";
        return true;
    }

    // --------------------------------------------------- ouvrir une image existante

    /// <summary>
    /// Lit le BPB d'une image ouverte, le confronte à la table, puis charge la FAT et le
    /// répertoire racine en mémoire.
    ///
    /// Le format est jugé SUR LE BPB et pas sur la taille du fichier. Les deux se
    /// recoupent ensuite, mais commencer par la taille reviendrait à accepter n'importe
    /// quelle image de 368 640 octets — une 1,44 Mo tronquée, par exemple — et à
    /// l'interpréter avec une géométrie qu'elle ne porte pas.
    /// </summary>
    private static bool Open(FileStream file, out Format f, out uint8_t[] fat,
                             out uint8_t[] root, out string error)
    {
        f = default;
        fat = Array.Empty<uint8_t>();
        root = Array.Empty<uint8_t>();

        if (file.Length % SectorSize != 0)
        {
            error = $"l'image fait {file.Length} octets, qui n'est pas un multiple de {SectorSize}.";
            return false;
        }

        uint8_t[] boot = new uint8_t[SectorSize];

        file.Seek(0, SeekOrigin.Begin);

        if (file.Read(boot, 0, SectorSize) != SectorSize)
        {
            error = "l'image est trop courte pour porter un secteur d'amorce.";
            return false;
        }

        int bps = GetWord(boot, 0x0B);
        int spc = boot[0x0D];
        int rootEntries = GetWord(boot, 0x11);
        int total = GetWord(boot, 0x13);
        int media = boot[0x15];
        int spf = GetWord(boot, 0x16);
        int spt = GetWord(boot, 0x18);
        int heads = GetWord(boot, 0x1A);

        // Une image sortie de SdlMenu.CreateBlank est 100 % nulle : les cinq champs
        // rendent 0 et on tombe exactement ici. Le message doit donc dire quoi faire,
        // pas seulement constater — c'est le cas le plus probable de tous.
        if (bps == 0 || spc == 0 || rootEntries == 0 || total == 0 || spf == 0)
        {
            error = "cette image ne porte pas de systeme de fichiers (son BPB est vide). " +
                    "La fabriquer avec --create-floppy, ou la formater par FORMAT sous DOS.";
            return false;
        }

        int index = FormatMatching(spc, rootEntries, total, media, spf, spt, heads);

        if (index < 0)
        {
            error = $"BPB inconnu : {bps} o/secteur, {spc} secteur(s)/cluster, " +
                    $"{rootEntries} entrees racine, {total} secteurs, media 0x{media:X2}, " +
                    $"{spf} secteur(s)/FAT, {spt} secteurs/piste, {heads} face(s). " +
                    "Seuls les quatre formats du lecteur 5,25\" DD sont geres.";
            return false;
        }

        f = Formats[index];

        if (file.Length != ImageSize(f))
        {
            error = $"le BPB annonce {f.Name} ({ImageSize(f)} octets), " +
                    $"mais le fichier en fait {file.Length}.";
            return false;
        }

        // Le nombre de clusters est DÉRIVÉ, et c'est ce qui clôt la question FAT12 contre
        // FAT16 sur cette page : au-delà de 4084 clusters ce ne serait plus du FAT12.
        // Aucun des quatre formats ne peut y arriver — 354 au maximum — donc ce refus
        // n'est pas atteignable. Il est écrit pour qu'une cinquième entrée de la table ne
        // puisse pas le devenir en silence.
        if (ClusterCount(f) >= 4085)
        {
            error = $"{f.Name} : {ClusterCount(f)} clusters, ce n'est plus du FAT12.";
            return false;
        }

        fat = new uint8_t[f.SectorsPerFat * SectorSize];
        file.Seek((long)f.ReservedSectors * SectorSize, SeekOrigin.Begin);
        file.ReadExactly(fat, 0, fat.Length);

        // LES DEUX COPIES, comparées. Ne jamais en élire une en silence : si elles
        // divergent, l'image a déjà été écrite par deux choses qui ne se sont pas vues,
        // et en choisir une détruirait ce que l'autre décrit.
        uint8_t[] second = new uint8_t[fat.Length];

        for (int c = 1; c < f.NumberOfFats; c++)
        {
            file.Seek((long)(f.ReservedSectors + c * f.SectorsPerFat) * SectorSize,
                      SeekOrigin.Begin);
            file.ReadExactly(second, 0, second.Length);

            for (int d = 0; d < fat.Length; d++)
            {
                if (fat[d] != second[d])
                {
                        error = $"les copies 1 et {c + 1} de la FAT different a l'octet {d} " +
                                $"(0x{fat[d]:X2} contre 0x{second[d]:X2}). Image incoherente.";
                        return false;
                }
            }
        }

        root = new uint8_t[RootSectors(f) * SectorSize];
        file.Seek((long)(f.ReservedSectors + f.NumberOfFats * f.SectorsPerFat) * SectorSize,
                  SeekOrigin.Begin);
        file.ReadExactly(root, 0, root.Length);

        error = "";
        return true;
    }

    /// <summary>
    /// L'image est-elle montée dans un lecteur de la machine émulée ?
    ///
    /// REFUS, jamais d'éjection automatique, et le danger a deux étages. img_seek tient
    /// une piste entière en cache (disc_img.cs:438) qu'img_writeback réécrit : modifier
    /// le fichier sous son FileStream ouvert le ferait écraser par la piste périmée. Et
    /// au-dessus, DOS garde SA propre copie de la FAT et du répertoire racine du disque
    /// qu'il croit en place — éjecter, écrire dans son dos, réinsérer, et DOS peut
    /// réécrire sa FAT périmée et effacer l'entrée qu'on vient de poser. Aucune
    /// vérification d'image au repos ne voit ce mode d'échec.
    /// </summary>
    internal static int MountedIn(string path)
    {
        string full;

        try
        {
            full = Path.GetFullPath(path);
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or NotSupportedException)
        {
            return -1;
        }

        for (int d = 0; d < 2; d++)
        {
            if (Disc.disc.drive_empty[d] != 0 || Floppy.fdd_c.discfns[d].Length == 0)
                    continue;

            try
            {
                if (string.Equals(Path.GetFullPath(Floppy.fdd_c.discfns[d]), full,
                                  StringComparison.Ordinal))
                        return d;
            }
            catch (Exception ex) when (ex is IOException or ArgumentException or NotSupportedException)
            {
                continue;
            }
        }

        return -1;
    }

    // ------------------------------------------------------------ le nom 8.3 et l'entrée

    // Le jeu de caractères qu'un nom DOS admet, en plus de A-Z et 0-9. Tout le reste est
    // refusé : l'espace, et les délimiteurs . " / \ [ ] : ; | = , + * ? < >
    private const string NameExtra = "$%'-_@~`!(){}^#&";

    // Les noms de périphériques de DOS. Un fichier qui porterait l'un d'eux serait ouvert
    // comme le PÉRIPHÉRIQUE, jamais comme le fichier — le radical seul suffit, l'extension
    // ne protège pas.
    private static readonly string[] DeviceNames =
    {
        "CON", "AUX", "PRN", "NUL", "COM1", "COM2", "COM3", "COM4", "LPT1", "LPT2", "LPT3",
    };

    /// <summary>
    /// Le nom d'hôte converti en onze octets 8.3, ou un refus qui NOMME la règle violée.
    ///
    /// JAMAIS DE TRONCATURE. Couper « rapport-annuel.txt » en « RAPPORT.TXT » fabrique
    /// silencieusement un doublon avec « rapport-mensuel.txt », et l'utilisateur ne
    /// l'apprend qu'en cherchant le second sur la disquette. Même posture que FreeName :
    /// on refuse plutôt que d'écraser sans le dire.
    /// </summary>
    internal static bool ToShortName(string hostName, out uint8_t[] name11, out string error)
    {
        name11 = new uint8_t[11];

        for (int c = 0; c < 11; c++)
            name11[c] = 0x20;

        for (int c = 0; c < hostName.Length; c++)
        {
            if (hostName[c] < 0x20 || hostName[c] > 0x7E)
            {
                    error = $"« {hostName} » : le caractere en position {c + 1} n'est pas " +
                            "de l'ASCII imprimable. La traduction vers la page 437 du CGA " +
                            "n'est pas geree.";
                    return false;
            }
        }

        // ToUpperInvariant et non ToUpper : la culture turque rendrait « i » en « İ »,
        // hors ASCII, et le nom de la disquette dépendrait de la locale de la machine.
        string name = hostName.ToUpperInvariant();
        int dot = name.LastIndexOf('.');
        string stem = dot < 0 ? name : name.Substring(0, dot);
        string ext = dot < 0 ? "" : name.Substring(dot + 1);

        if (stem.Length == 0)
        {
            error = $"« {hostName} » : DOS n'a pas de nom sans radical.";
            return false;
        }

        if (stem.Length > 8)
        {
            error = $"« {hostName} » : le radical « {stem} » fait {stem.Length} caracteres, " +
                    "8 au maximum. Renommer la source — tronquer fabriquerait un doublon.";
            return false;
        }

        if (ext.Length > 3)
        {
            error = $"« {hostName} » : l'extension « {ext} » fait {ext.Length} caracteres, " +
                    "3 au maximum.";
            return false;
        }

        if (ext.IndexOf('.') >= 0 || stem.IndexOf('.') >= 0)
        {
            error = $"« {hostName} » : un nom DOS n'a qu'un seul point.";
            return false;
        }

        for (int c = 0; c < DeviceNames.Length; c++)
        {
            if (stem == DeviceNames[c])
            {
                    error = $"« {hostName} » : « {stem} » est un peripherique DOS. Un fichier " +
                            "de ce nom serait ouvert comme le peripherique.";
                    return false;
            }
        }

        for (int c = 0; c < stem.Length; c++)
        {
            if (!IsNameChar(stem[c]))
            {
                    error = $"« {hostName} » : le caractere « {stem[c]} » n'est pas admis " +
                            "dans un nom DOS.";
                    return false;
            }

            name11[c] = (uint8_t)stem[c];
        }

        for (int c = 0; c < ext.Length; c++)
        {
            if (!IsNameChar(ext[c]))
            {
                    error = $"« {hostName} » : le caractere « {ext[c]} » n'est pas admis " +
                            "dans une extension DOS.";
                    return false;
            }

            name11[8 + c] = (uint8_t)ext[c];
        }

        // 0xE5 en premier octet veut dire « entrée effacée ». Il est IMPOSSIBLE ici,
        // aucun caractère admis ne valant 0xE5 — écrit noir sur blanc plutôt que laissé
        // au hasard, parce que c'est le genre de chemin dont on croit qu'il est couvert.
        error = "";
        return true;
    }

    private static bool IsNameChar(char c)
        => (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || NameExtra.IndexOf(c) >= 0;

    /// <summary>
    /// L'horodatage DOS. Deux mots : date = ((annee - 1980) &lt;&lt; 9) | (mois &lt;&lt; 5) | jour,
    /// heure = (h &lt;&lt; 11) | (min &lt;&lt; 5) | (sec / 2) — deux secondes de résolution.
    ///
    /// Recoupé sur la vérité terrain : l'entrée BASIC.COM de os/vierge-360k.img porte
    /// 0x0668 et 0x6060, soit 1983-03-08 et 12:03:00.
    ///
    /// Heure LOCALE, comme DOS, qui n'a pas de fuseau : un DIR doit montrer l'heure que
    /// « ls -l » montre sur l'hôte.
    /// </summary>
    internal static bool DosStamp(DateTime t, out int date, out int time, out string error)
    {
        date = 0;
        time = 0;

        if (t.Year < 1980 || t.Year > 2107)
        {
            error = $"l'horodatage {t:yyyy-MM-dd HH:mm:ss} sort de 1980-2107, " +
                    "que le format DOS sur deux mots ne sait pas porter.";
            return false;
        }

        date = ((t.Year - 1980) << 9) | (t.Month << 5) | t.Day;
        time = (t.Hour << 11) | (t.Minute << 5) | (t.Second / 2);
        error = "";
        return true;
    }

    // ----------------------------------------------------------------- le dépôt

    /// <summary>Un fichier accepté, avec tout ce qui a déjà été décidé pour lui.</summary>
    private struct Planned
    {
        internal string Source;         // chemin sur l'hôte
        internal string ShortName;      // « NOM.EXT », pour les messages
        internal long Size;
        internal int[] Clusters;        // la chaîne, dans l'ordre ; vide si le fichier est vide
    }

    /// <summary>
    /// Dépose des fichiers de l'hôte dans une image existante.
    ///
    /// DEUX PHASES, et la coupure est nette : la phase 1 décide tout — noms, entrées de
    /// répertoire, chaînes de clusters — en ne touchant QUE des tampons en mémoire ;
    /// la phase 2 écrit. Le fichier image n'est ouvert en écriture qu'une fois le
    /// dernier refus possible écarté. Une image ne doit jamais porter une demi-chaîne :
    /// un cluster alloué mais non chaîné est de la place perdue qu'aucun CHKDSK de 1983
    /// ne rendra.
    ///
    /// Les tampons `fat` et `root` sont mutés pendant la phase 1, ce qui fait que les
    /// fichiers se voient les uns les autres : le deuxième ne peut pas se voir attribuer
    /// un cluster que le premier a pris, ni un nom qu'il porte déjà.
    /// </summary>
    internal static bool Put(string imagePath, string[] sources, out string message)
    {
        int mounted = MountedIn(imagePath);

        if (mounted >= 0)
        {
            message = $"{Path.GetFileName(imagePath)} est montee dans " +
                      $"{(char)('A' + mounted)}:. L'ejecter d'abord — la modifier sous la " +
                      "machine ferait ecraser l'ecriture par la piste en cache, ou par la " +
                      "FAT que DOS garde de son cote.";
            return false;
        }

        FileStream? file = null;

        try
        {
            file = new FileStream(imagePath, FileMode.Open, FileAccess.ReadWrite);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Pas de repli en lecture seule, contrairement à img_load (disc_img.cs:206),
            // qui rouvre en « rb » et pose writeprot : ici il n'y a rien à faire d'une
            // image qu'on ne peut pas écrire.
            message = $"{Path.GetFileName(imagePath)} n'est pas ouvrable en ecriture : {ex.Message}";
            return false;
        }

        try
        {
            if (!Open(file, out Format f, out uint8_t[] fat, out uint8_t[] root, out string error))
            {
                message = $"{Path.GetFileName(imagePath)} : {error}";
                return false;
            }

            Planned[] plan = new Planned[sources.Length];

            // ---- phase 1 : tout décider, ne rien écrire -------------------------------
            for (int c = 0; c < sources.Length; c++)
            {
                if (!PlanOne(f, fat, root, sources[c], ref plan[c], out error))
                {
                        message = error;
                        return false;
                }
            }

            StampDirectoryEnd(f, root);

            // ---- phase 2 : écrire ----------------------------------------------------
            for (int c = 0; c < plan.Length; c++)
            {
                if (!WriteData(file, f, plan[c], out error))
                {
                        message = error;
                        return false;
                }
            }

            file.Seek((long)(f.ReservedSectors + f.NumberOfFats * f.SectorsPerFat) * SectorSize,
                      SeekOrigin.Begin);
            file.Write(root, 0, root.Length);

            // LES DEUX COPIES DEPUIS LE MÊME TAMPON. Les construire séparément est la
            // seule façon de les faire diverger, et c'est précisément ce qu'Open refuse
            // de rattraper ensuite.
            for (int c = 0; c < f.NumberOfFats; c++)
            {
                file.Seek((long)(f.ReservedSectors + c * f.SectorsPerFat) * SectorSize,
                          SeekOrigin.Begin);
                file.Write(fat, 0, fat.Length);
            }

            file.Flush(true);

            int free = 0;

            for (int n = 2; n <= MaxCluster(f); n++)
            {
                if (FatGet(fat, n) == 0)
                        free++;
            }

            string names = "";

            for (int c = 0; c < plan.Length; c++)
                names += (c != 0 ? ", " : "") + plan[c].ShortName;

            message = $"{plan.Length} fichier(s) depose(s) dans {Path.GetFileName(imagePath)} : " +
                      $"{names}. Reste {free} cluster(s) libre(s) sur {ClusterCount(f)}, " +
                      $"soit {(long)free * BytesPerCluster(f)} octets.";
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            message = $"{Path.GetFileName(imagePath)} : {ex.Message}";
            return false;
        }
        finally
        {
            file.Dispose();
        }
    }

    /// <summary>
    /// Phase 1 pour un fichier : tous les refus, puis la réservation en mémoire de son
    /// entrée de répertoire et de sa chaîne de clusters.
    /// </summary>
    private static bool PlanOne(Format f, uint8_t[] fat, uint8_t[] root, string source,
                                ref Planned p, out string error)
    {
        FileInfo info;

        try
        {
            info = new FileInfo(source);
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or NotSupportedException)
        {
            error = $"« {source} » : {ex.Message}";
            return false;
        }

        if (!info.Exists)
        {
            // Directory.Exists séparément : « c'est un repertoire » est une erreur
            // différente de « ca n'existe pas », et la confondre envoie chercher une
            // faute de frappe là où il n'y en a pas. Il n'y a pas de sous-répertoires
            // ici — seule la racine existe.
            error = Directory.Exists(source)
                ? $"« {source} » est un repertoire. Ce formateur ne gere que la racine " +
                  "de la disquette, donc les fichiers un par un."
                : $"« {source} » n'existe pas.";
            return false;
        }

        if ((info.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            error = $"« {source} » est un lien. Designer sa cible.";
            return false;
        }

        if (!ToShortName(info.Name, out uint8_t[] name11, out error))
            return false;

        if (!DosStamp(info.LastWriteTime, out int date, out int time, out error))
        {
            error = $"« {info.Name} » : {error}";
            return false;
        }

        // Le champ taille est un uint32. Testé séparément du manque de place, parce que
        // le calcul du nombre de clusters déborderait AVANT d'arriver à ce refus-là.
        if (info.Length > uint.MaxValue)
        {
            error = $"« {info.Name} » fait {info.Length} octets ; le champ taille d'une " +
                    "entree DOS est sur 32 bits.";
            return false;
        }

        p.ShortName = ShortNameText(name11);

        // Doublon. Le balayage parcourt TOUTES les entrées et ne s'arrête pas au premier
        // 0x00 : les fichiers déjà planifiés à ce tour y sont, et une entrée au-delà du
        // marqueur de fin reste un nom pris tant que rien ne l'a effacée.
        for (int e = 0; e < f.RootEntries; e++)
        {
            if (root[e * 32] == DirEnd || root[e * 32] == DirErased || root[e * 32] == FillByte)
                    continue;

            bool same = true;

            for (int c = 0; c < 11; c++)
                same &= root[e * 32 + c] == name11[c];

            if (same)
            {
                    error = $"« {p.ShortName} » est deja sur l'image (entree {e}). " +
                            "Ce formateur ne remplace rien.";
                    return false;
            }
        }

        int entry = -1;

        for (int e = 0; e < f.RootEntries; e++)
        {
            if (root[e * 32] == DirEnd || root[e * 32] == DirErased || root[e * 32] == FillByte)
            {
                    entry = e;
                    break;
            }
        }

        if (entry < 0)
        {
            error = $"« {p.ShortName} » : le repertoire racine est plein " +
                    $"({f.RootEntries} entrees). Celui d'un FAT12 ne s'agrandit pas.";
            return false;
        }

        // Un fichier VIDE ne prend AUCUN cluster, et son premier cluster vaut 0. C'est ce
        // que fait DOS, et il faut l'écrire : la boucle d'allocation naturelle réserverait
        // un cluster pour zéro octet, que rien ne relirait jamais.
        int needed = info.Length == 0
            ? 0
            : (int)((info.Length + BytesPerCluster(f) - 1) / BytesPerCluster(f));

        int[] clusters = new int[needed];
        int found = 0;

        for (int n = 2; n <= MaxCluster(f) && found < needed; n++)
        {
            // Libre = 0. Un cluster défectueux vaut 0xFF7, donc il n'est pas libre et
            // cette boucle ne peut pas l'allouer — ce qui est exactement la règle.
            if (FatGet(fat, n) == 0)
                    clusters[found++] = n;
        }

        if (found < needed)
        {
            int free = 0;

            for (int n = 2; n <= MaxCluster(f); n++)
            {
                if (FatGet(fat, n) == 0)
                        free++;
            }

            error = $"« {p.ShortName} » demande {needed} cluster(s) de " +
                    $"{BytesPerCluster(f)} octets, il en reste {free}. Rien n'a ete ecrit.";
            return false;
        }

        for (int c = 0; c < needed; c++)
            FatSet(fat, clusters[c], c + 1 < needed ? clusters[c + 1] : ClusterEnd);

        int at = entry * 32;

        for (int c = 0; c < 11; c++)
            root[at + c] = name11[c];

        root[at + 0x0B] = 0x20;         // attribut : archive, celui de tout fichier ordinaire

        for (int c = 0x0C; c < 0x16; c++)
            root[at + c] = 0;           // les dix octets réservés, nuls sur la vérité terrain

        PutWord(root, at + 0x16, time);
        PutWord(root, at + 0x18, date);
        PutWord(root, at + 0x1A, needed == 0 ? 0 : clusters[0]);
        PutWord(root, at + 0x1C, (int)(info.Length & 0xFFFF));
        PutWord(root, at + 0x1E, (int)((info.Length >> 16) & 0xFFFF));

        p.Source = source;
        p.Size = info.Length;
        p.Clusters = clusters;

        error = "";
        return true;
    }

    /// <summary>
    /// Le marqueur de fin de répertoire : un 0x00 sur l'octet 0 de la première entrée
    /// jamais utilisée.
    ///
    /// C'EST L'OUBLI LE PLUS DISCRET DE TOUT CE FICHIER. FORMAT remplit la racine de
    /// 0xF6, qui n'est ni 0x00 ni 0xE5, donc un DIR lirait une entrée nommée 0xF6F6F6…
    /// et l'annoncerait comme un fichier. La vérité terrain le montre à l'œuvre : dans
    /// os/vierge-360k.img, l'entrée 1 — la première libre après BASIC.COM — a son octet 0
    /// à 0x00 et les 31 suivants à 0xF6.
    ///
    /// Idempotent : s'il y a déjà un 0x00 avant la première entrée de remplissage, il n'y
    /// a rien à faire.
    /// </summary>
    private static void StampDirectoryEnd(Format f, uint8_t[] root)
    {
        for (int e = 0; e < f.RootEntries; e++)
        {
            if (root[e * 32] == DirEnd)
                    return;

            if (root[e * 32] == FillByte)
            {
                    root[e * 32] = DirEnd;
                    return;
            }
        }
    }

    /// <summary>
    /// Phase 2 pour un fichier : ses données, cluster par cluster.
    ///
    /// Le DERNIER cluster est complété jusqu'à sa taille pleine avec l'octet de
    /// remplissage, et non laissé tel quel. Ce que DOS lit au-delà de la taille déclarée
    /// n'a aucun sens, mais laisser la queue du cluster précédent y transparaître ferait
    /// dépendre l'image de ce qui s'y trouvait avant — donc deux dépôts identiques
    /// produiraient deux images différentes.
    /// </summary>
    private static bool WriteData(FileStream file, Format f, Planned p, out string error)
    {
        uint8_t[] buf = new uint8_t[BytesPerCluster(f)];

        try
        {
            using (FileStream src = new FileStream(p.Source, FileMode.Open, FileAccess.Read))
            {
                // La taille relue sur le flux OUVERT, et comparée. Entre le FileInfo de la
                // phase 1 et ici, la source a pu changer — la chaîne de clusters serait
                // alors dimensionnée pour un fichier qui n'existe plus.
                if (src.Length != p.Size)
                {
                        error = $"« {p.ShortName} » : la source faisait {p.Size} octets au " +
                                $"controle et {src.Length} a la lecture. Elle a change.";
                        return false;
                }

                for (int c = 0; c < p.Clusters.Length; c++)
                {
                        for (int d = 0; d < buf.Length; d++)
                                buf[d] = FillByte;

                        src.ReadExactly(buf, 0,
                                        (int)Math.Min(buf.Length, p.Size - (long)c * buf.Length));

                        file.Seek((long)ClusterSector(f, p.Clusters[c]) * SectorSize,
                                  SeekOrigin.Begin);
                        file.Write(buf, 0, buf.Length);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            error = $"« {p.ShortName} » : {ex.Message}";
            return false;
        }

        error = "";
        return true;
    }

    /// <summary>« NOM.EXT » depuis les onze octets, pour les messages.</summary>
    internal static string ShortNameText(uint8_t[] name11)
    {
        string stem = "";
        string ext = "";

        for (int c = 0; c < 8; c++)
        {
            if (name11[c] != 0x20)
                    stem += (char)name11[c];
        }

        for (int c = 8; c < 11; c++)
        {
            if (name11[c] != 0x20)
                    ext += (char)name11[c];
        }

        return ext.Length != 0 ? stem + "." + ext : stem;
    }

    // ------------------------------------------------------------- l'auto-contrôle
    //
    // Il existe pour le même motif que --setup-check et que config-check : ce code n'a
    // aucun autre moyen d'être exercé sur des valeurs dont on connaît la réponse. Un
    // système de fichiers faux ne se voit pas — l'image a la bonne taille, le bon nombre
    // de secteurs, et DOS la lit de travers en silence.

    /// <summary>
    /// La FAT1 de os/vierge-360k.img, ses 27 premiers octets. C'est la chaîne de
    /// BASIC.COM, et elle couvre les DEUX alignements de l'empaquetage 12 bits sur toute
    /// sa longueur — ce qui en fait la meilleure éprouvette que le dépôt porte.
    /// </summary>
    private static readonly uint8_t[] GroundTruthFat =
    {
        0xFD, 0xFF, 0xFF, 0x03, 0x40, 0x00, 0x05, 0x60, 0x00, 0x07, 0x80, 0x00, 0x09, 0xA0,
        0x00, 0x0B, 0xC0, 0x00, 0x0D, 0xE0, 0x00, 0x0F, 0x00, 0x01, 0x11, 0xF0, 0xFF,
    };

    /// <returns>0 si tout passe, 1 sinon.</returns>
    internal static int SelfCheck()
    {
        int fail = 0;

        void Check(string what, bool ok, string got)
        {
            Console.WriteLine($"  [{(ok ? "ok" : "ECHEC")}] {what} : {got}");

            if (!ok)
                    fail++;
        }

        Console.WriteLine("Auto-contrôle du formateur de disquette.");
        Console.WriteLine();
        Console.WriteLine("Cohérence interne des quatre formats :");

        for (int c = 0; c < Formats.Length; c++)
        {
            Format f = Formats[c];

            Check(f.Stem, ValidateFormat(f, out string err) && ImageSize(f) % SectorSize == 0,
                  err.Length != 0 ? err
                  : $"{ClusterCount(f)} clusters de {BytesPerCluster(f)} o, " +
                    $"racine sur {RootSectors(f)} sect., donnees au secteur {DataStart(f)}, " +
                    $"{ImageSize(f)} octets");
        }

        Console.WriteLine();
        Console.WriteLine("Empaquetage FAT12 — 1. valeurs absolues lues dans la vérité terrain :");

        // C'EST CE CONTRÔLE-LÀ qui épingle la branche IMPAIRE indépendamment. L'entrée 3
        // est impaire et vaut 4 ; aucune inversion des deux branches ne peut contrefaire
        // ça. Sans lui, un aller-retour seul exercerait lecture et écriture COMPOSÉES, où
        // deux défauts inverses l'un de l'autre s'annulent et le vert ne prouve rien.
        Check("entree 0 = 0xFFD (descripteur de media 0xFD en octet bas)",
              FatGet(GroundTruthFat, 0) == 0xFFD, $"0x{FatGet(GroundTruthFat, 0):X3}");
        Check("entree 1 = 0xFFF", FatGet(GroundTruthFat, 1) == 0xFFF,
              $"0x{FatGet(GroundTruthFat, 1):X3}");

        bool chainOk = true;
        string chain = "";

        for (int n = 2; n <= 16; n++)
        {
            chainOk &= FatGet(GroundTruthFat, n) == n + 1;
            chain += $"{n}->{FatGet(GroundTruthFat, n)} ";
        }

        Check("entrees 2 a 16 chainent vers n+1 (dont les impaires)", chainOk, chain.Trim());
        Check("entree 17 = 0xFFF, fin de chaine", FatGet(GroundTruthFat, 17) == 0xFFF,
              $"0x{FatGet(GroundTruthFat, 17):X3}");

        Console.WriteLine();
        Console.WriteLine("Empaquetage FAT12 — 2. écriture depuis zéro, comparée à la vérité terrain :");

        // BASIC.COM : premier cluster 2, taille 0x3F80 = 16 256 octets, 2 secteurs par
        // cluster sur une 360 Ko, donc exactement 16 clusters et la chaîne 2 à 17.
        uint8_t[] built = new uint8_t[GroundTruthFat.Length];

        FatSet(built, 0, 0xF00 | 0xFD);
        FatSet(built, 1, ClusterEnd);

        for (int n = 2; n <= 16; n++)
            FatSet(built, n, n + 1);

        FatSet(built, 17, ClusterEnd);

        int diff = -1;

        for (int c = 0; c < GroundTruthFat.Length; c++)
        {
            if (built[c] != GroundTruthFat[c])
            {
                    diff = c;
                    break;
            }
        }

        Check("27 octets ecrits depuis une FAT vierge, identiques a ceux de DOS", diff < 0,
              diff < 0 ? "identiques"
                       : $"premier ecart a l'octet {diff} : 0x{built[diff]:X2} au lieu " +
                         $"de 0x{GroundTruthFat[diff]:X2}");

        Console.WriteLine();
        Console.WriteLine("Empaquetage FAT12 — 3. aller-retour, et les deux copies :");

        // Par-dessus les deux précédents, et pas à leur place : celui-ci seul passerait
        // avec une paire lecture/écriture toutes deux fausses de la même façon.
        uint8_t[] round = BuildFat(Formats[3]);
        bool roundOk = true;

        for (int n = 2; n <= MaxCluster(Formats[3]); n++)
            FatSet(round, n, (n * 7 + 3) & 0xFFF);

        for (int n = 2; n <= MaxCluster(Formats[3]); n++)
            roundOk &= FatGet(round, n) == ((n * 7 + 3) & 0xFFF);

        Check($"{MaxCluster(Formats[3]) - 1} entrees ecrites puis relues", roundOk,
              roundOk ? "toutes relues a l'identique" : "au moins une entree relue fausse");
        Check("les deux entrees reservees ont survecu aux voisines",
              FatGet(round, 0) == 0xFFD && FatGet(round, 1) == 0xFFF,
              $"0x{FatGet(round, 0):X3} 0x{FatGet(round, 1):X3}");

        Console.WriteLine();
        Console.WriteLine("Secteur d'amorce :");

        uint8_t[] boot = BuildBootSector(Formats[3]);

        Check("saut court EB vers le talon", boot[0] == 0xEB && 0x02 + boot[1] == StubOffset,
              $"EB {boot[1]:X2} {boot[2]:X2} -> 0x{0x02 + boot[1]:X2}");
        Check("BPB relu = BPB ecrit",
              GetWord(boot, 0x0B) == SectorSize && boot[0x0D] == Formats[3].SectorsPerCluster &&
              GetWord(boot, 0x11) == Formats[3].RootEntries &&
              GetWord(boot, 0x13) == Formats[3].TotalSectors &&
              boot[0x15] == Formats[3].MediaDescriptor &&
              GetWord(boot, 0x16) == Formats[3].SectorsPerFat &&
              GetWord(boot, 0x18) == Formats[3].SectorsPerTrack &&
              GetWord(boot, 0x1A) == Formats[3].Heads,
              $"bps={GetWord(boot, 0x0B)} spc={boot[0x0D]} racine={GetWord(boot, 0x11)} " +
              $"total={GetWord(boot, 0x13)} media=0x{boot[0x15]:X2} spf={GetWord(boot, 0x16)} " +
              $"spt={GetWord(boot, 0x18)} faces={GetWord(boot, 0x1A)}");

        // L'opérande du « movw $msg,%si » doit pointer sur le premier octet du message,
        // pas sur du BPB. Elle est CALCULÉE depuis la longueur du talon, et ce contrôle
        // est ce qui le garantit si le message ou le talon changent un jour.
        int operand = GetWord(boot, StubOffset + StubMessageOperand);
        int msgAt = operand - 0x7C00;

        Check("l'adresse du message pointe sur le message",
              msgAt == StubOffset + BootStub.Length && boot[msgAt] == (uint8_t)BootMessage[0],
              $"0x{operand:X4} -> offset 0x{msgAt:X2}, octet 0x{boot[msgAt]:X2}");
        Check("le message et son zero tiennent avant la signature",
              msgAt + BootMessage.Length < 0x1FE && boot[msgAt + BootMessage.Length] == 0,
              $"finit a 0x{msgAt + BootMessage.Length:X3}");
        Check("signature 55AA", boot[0x1FE] == 0x55 && boot[0x1FF] == 0xAA,
              $"{boot[0x1FE]:X2} {boot[0x1FF]:X2}");

        Console.WriteLine();
        Console.WriteLine("Refus d'une image montée :");

        // Le refus le plus important du fichier, et le seul que l'utilisateur rencontrera
        // tous les jours — il tombe dès qu'on veut déposer sur la disquette qu'on est en
        // train de regarder. Sans ce contrôle, il ne s'exécuterait que le jour où il
        // compte.
        string savedFn = Floppy.fdd_c.discfns[1];
        int savedEmpty = Disc.disc.drive_empty[1];

        try
        {
            Floppy.fdd_c.discfns[1] = "os/une-image.img";
            Disc.disc.drive_empty[1] = 0;

            Check("une image montee en B: est reconnue", MountedIn("os/une-image.img") == 1,
                  $"lecteur {MountedIn("os/une-image.img")}");
            Check("le meme chemin ecrit autrement est reconnu aussi",
                  MountedIn("os/./une-image.img") == 1,
                  $"lecteur {MountedIn("os/./une-image.img")}");
            Check("une autre image ne l'est pas", MountedIn("os/autre.img") < 0,
                  $"lecteur {MountedIn("os/autre.img")}");

            Disc.disc.drive_empty[1] = 1;
            Check("lecteur vide : plus rien n'est monte", MountedIn("os/une-image.img") < 0,
                  $"lecteur {MountedIn("os/une-image.img")}");
        }
        finally
        {
            Floppy.fdd_c.discfns[1] = savedFn;
            Disc.disc.drive_empty[1] = savedEmpty;
        }

        Console.WriteLine();
        Console.WriteLine("Conversion d'un nom d'hôte en 8.3 :");

        Check("un nom ordinaire passe",
              ToShortName("Lisezmoi.txt", out uint8_t[] n1, out _) &&
              ShortNameText(n1) == "LISEZMOI.TXT", ToShortName("Lisezmoi.txt", out n1, out _)
                  ? ShortNameText(n1) : "refuse");
        Check("un radical de 9 est REFUSE, pas tronque",
              !ToShortName("abcdefghi.txt", out _, out string e1), e1);
        Check("un peripherique est refuse", !ToShortName("con.txt", out _, out string e2), e2);
        Check("un nom sans radical est refuse", !ToShortName(".profile", out _, out string e3), e3);
        Check("sans extension c'est bon",
              ToShortName("MAKEFILE", out uint8_t[] n2, out _) && ShortNameText(n2) == "MAKEFILE",
              ToShortName("MAKEFILE", out n2, out _) ? ShortNameText(n2) : "refuse");

        // Recoupé sur la vérité terrain : l'entrée BASIC.COM de os/vierge-360k.img porte
        // 0x0668 et 0x6060.
        Check("horodatage 1983-03-08 12:03:00 = 0x0668 / 0x6060",
              DosStamp(new DateTime(1983, 3, 8, 12, 3, 0), out int d1, out int t1, out _) &&
              d1 == 0x0668 && t1 == 0x6060, $"date 0x{d1:X4} heure 0x{t1:X4}");
        Check("1979 est refuse, pas ecrete",
              !DosStamp(new DateTime(1979, 12, 31, 23, 59, 58), out _, out _, out string e4), e4);

        Console.WriteLine();
        Console.WriteLine("Marqueur de fin de répertoire :");

        // L'INVARIANT : après un dépôt, il y a EXACTEMENT un 0x00 dans la racine, et il
        // est après toutes les entrées utilisées. Sans lui, DOS lirait une entrée dont le
        // nom commence par 0xF6 et l'annoncerait comme un fichier ; ou il s'arrêterait
        // avant un fichier qu'on vient d'écrire.
        //
        // Les quatre dispositions de départ ne se rencontrent pas à l'essai — une image
        // sortie de Create en a une seule — mais elles se rencontrent SUR LE DISQUE :
        // FORMAT ne pose aucun 0x00, DOS en pose un, DEL laisse des 0xE5, et une entrée
        // peut traîner APRÈS le marqueur.
        (string What, uint8_t[] Start)[] roots =
        {
            ("racine sortie de FORMAT : que du remplissage",   [FillByte, FillByte, FillByte, FillByte]),
            ("racine sortie de Create : un 0x00 en tete",      [DirEnd, FillByte, FillByte, FillByte]),
            ("une entree effacee avant le marqueur",           [0x41, DirErased, DirEnd, FillByte]),
            ("une entree orpheline APRES le marqueur",         [DirEnd, 0x42, FillByte, FillByte]),
        };

        for (int c = 0; c < roots.Length; c++)
        {
            Format f = Formats[3];
            uint8_t[] root = new uint8_t[RootSectors(f) * SectorSize];

            for (int d = 0; d < root.Length; d++)
                root[d] = FillByte;

            for (int d = 0; d < roots[c].Start.Length; d++)
                root[d * 32] = roots[c].Start[d];

            // Le dépôt d'un fichier, réduit à ce que la racine en voit : prendre la
            // première entrée libre — 0x00, 0xE5 ou 0xF6 — puis reposer le marqueur.
            int at = -1;

            for (int e = 0; e < f.RootEntries; e++)
            {
                if (root[e * 32] == DirEnd || root[e * 32] == DirErased || root[e * 32] == FillByte)
                {
                        at = e;
                        break;
                }
            }

            root[at * 32] = 0x5A;       /* « Z », une entrée utilisée quelconque */
            StampDirectoryEnd(f, root);

            int markers = 0;
            int firstMarker = -1;
            int lastUsed = -1;

            for (int e = 0; e < f.RootEntries; e++)
            {
                if (root[e * 32] == DirEnd)
                {
                        markers++;

                        if (firstMarker < 0)
                                firstMarker = e;
                }
                else if (root[e * 32] != DirErased && root[e * 32] != FillByte)
                {
                        lastUsed = e;
                }
            }

            Check(roots[c].What, markers == 1 && firstMarker > lastUsed,
                  $"ecrit en {at}, {markers} marqueur(s), le 0x00 en {firstMarker}, " +
                  $"derniere entree utilisee en {lastUsed}");
        }

        Console.WriteLine();
        Console.WriteLine("Invariant de géométrie — les deux branches d'img_load :");

        fail += GeometryInvariant();

        Console.WriteLine();
        Console.WriteLine(fail == 0 ? "Vert : tous les contrôles passent."
                                    : $"{fail} contrôle(s) en échec.");

        return fail == 0 ? 0 : 1;
    }

    /// <summary>
    /// LA PORTE NON NÉGOCIABLE de ce fichier, et elle est MESURÉE, pas supposée.
    ///
    /// Une image vierge fait rendre 0 aux cinq lectures de BPB d'img_load, donc la garde
    /// de disc_img.cs:242 force la branche de devinette par TAILLE. Les images d'ici
    /// portent un BPB valide, donc elles prennent l'autre branche. Pour les quatre
    /// formats, les deux doivent rendre la même géométrie — sinon c'est notre image qui
    /// est fausse, pas le chargeur.
    ///
    /// bpb_disable est la clé de configuration (disc_img.cs:72, lue par pc.cs:259) qui
    /// force la branche taille : la poser à 1 puis à 0 sur LE MÊME fichier compare
    /// exactement les deux chemins.
    /// </summary>
    private static int GeometryInvariant()
    {
        string dir = Path.Combine(Path.GetTempPath(),
                                  "ixtal26-fat-" + Environment.ProcessId.ToString(CultureInfo.InvariantCulture));
        int fail = 0;
        int saved = Disc.disc_img.bpb_disable;

        try
        {
            Directory.CreateDirectory(dir);

            for (int c = 0; c < Formats.Length; c++)
            {
                Format f = Formats[c];
                string path = Path.Combine(dir, f.Stem + ".img");

                if (!Create(path, f, out string err))
                {
                        Console.WriteLine($"  [ECHEC] {f.Stem} : {err}");
                        fail++;
                        continue;
                }

                Disc.disc_img.bpb_disable = 0;
                Disc.disc.disc_load(0, path);
                var bpb = Disc.disc_img.Probe(0);
                Disc.disc.disc_close(0);

                Disc.disc_img.bpb_disable = 1;
                Disc.disc.disc_load(0, path);
                var size = Disc.disc_img.Probe(0);
                Disc.disc.disc_close(0);

                bool same = bpb == size;
                bool expected = bpb.sectors == f.SectorsPerTrack && bpb.tracks == 40 &&
                                bpb.sides == f.Heads && bpb.sector_size == SectorSize &&
                                bpb.xdf_type == 0;

                Console.WriteLine($"  [{(same && expected ? "ok" : "ECHEC")}] {f.Stem} : " +
                                  $"par BPB {bpb.sectors}x{bpb.tracks}x{bpb.sides} " +
                                  $"(secteur {bpb.sector_size}, trou {bpb.hole}, xdf {bpb.xdf_type}), " +
                                  $"par taille {size.sectors}x{size.tracks}x{size.sides} " +
                                  $"(secteur {size.sector_size}, trou {size.hole}, xdf {size.xdf_type})");

                if (!same || !expected)
                        fail++;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.WriteLine($"  [ECHEC] invariant de geometrie : {ex.Message}");
            fail++;
        }
        finally
        {
            Disc.disc_img.bpb_disable = saved;

            try
            {
                Directory.Delete(dir, true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Console.WriteLine($"  (note : {dir} n'a pas pu etre efface : {ex.Message})");
            }
        }

        return fail;
    }
}
