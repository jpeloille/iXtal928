// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — sonde hôte, pas de pendant dans PCem ni dans le harnais.
// STATUS: host
//
// UNE SONDE, DANS UN FICHIER À PART, ET C'EST LE SUJET DU FICHIER.
//
// Elle appartient à `disc_img`, dont la moitié transcrite vit dans disc_img.cs : la
// classe est `partial`, donc la sonde voit `img[]`, qui est privé. Mais elle N'EST PAS
// une transcription, et la loger dans le fichier transcrit en ferait du code hôte non
// déclaré au milieu d'une région gouvernée par R1 et R2.
//
// Le précédent qu'on pourrait invoquer ne tient pas : Floppy.fdc_c.Probe vit bien dans un
// fichier transcrit, mais elle a un PENDANT DANS L'ORACLE — h_disc_probe(), harness.c:853
// — et son commentaire le dit. Celle-ci n'en a aucun. Ce n'est pas la même catégorie, et
// la frontière de fichier est ce qui le rend lisible sans avoir à faire confiance à un
// commentaire.
//
// POURQUOI ELLE EXISTE. img_load a deux chemins qui posent la géométrie : la lecture du
// BPB (disc_img.c:309-392) et la devinette par TAILLE (:246-305). C'est la garde de :242
// — donc la validité du BPB, donc bpb_disable — qui choisit. Tant que les seules images
// du dépôt étaient vierges, un seul des deux pouvait s'exécuter : cinq lectures de BPB
// à zéro forcent la branche taille, et SdlMenu.cs:530-536 le garantit par construction.
//
// Host/FatImage.cs fabrique des images qui portent un VRAI BPB. Elles prennent donc
// l'autre branche, et les deux doivent rendre la même géométrie pour les quatre formats
// — sinon c'est l'image qui est fausse, pas le chargeur. Sans cette sonde l'invariant ne
// pourrait qu'être supposé : rien d'autre n'expose ces champs, `img[]` étant privé ici
// comme son pendant static l'est dans disc_img.c. Mesuré en VERIFICATION.md § M14.

namespace iXtal26.Disc;

internal static partial class disc_img
{
    /// <summary>La géométrie qu'<c>img_load</c> a déduite pour un lecteur : ce qu'elle a
    /// retenu, et par quel chemin elle l'a retenu — c'est à l'appelant de faire varier
    /// <see cref="bpb_disable"/> et de comparer.</summary>
    internal static (int sectors, int tracks, int sides, int sector_size, int hole, int xdf_type)
        Probe(int drive)
    {
        return (img[drive].sectors, img[drive].tracks, img[drive].sides,
                img[drive].sector_size, img[drive].hole, img[drive].xdf_type);
    }
}
