// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/hdd/hdd.c  (PcemHDC : includes/private/ibm.h:366-372)
// STATUS: partial — les DEUX globales de hdd.c, et rien d'autre. Le registre
//         HDD_CONTROLLER et ses huit accesseurs sont omis : registre de cartes,
//         même famille que SOUND_CARD (VIDEO_CARD, réduit, depuis M15).
//
// hdd.c fait 182 lignes dont 90 % de registre. Mais il n'est pas entièrement
// écartable : il DÉFINIT `hdc[7]`, la table de géométrie dont `ibm.h:372` ne porte
// que l'extern, et `hdd_controller_name[16]`. Ce sont les seules définitions de
// l'arbre vendoré. Le registre part au registre des omissions ; ces deux-là, non.

namespace iXtal26.Disc;

// pcem: ibm.h:364-370
// omitted: le champ `FILE *f` de PcemHDC — mort dans tout l'arbre vendoré :
//   hdd_file_t porte son propre descripteur, et personne n'écrit hdc[d].f.
internal struct PcemHDC
{
    internal int spt, hpc; /*Sectors per track, heads per cylinder*/
    internal int tracks;
}

// Le conteneur prend `_c` : le paramètre `hdd_file_t hdd` de hdd_file.cs porte déjà
// ce nom, comme fdc_c, fdd_c, model_c et ppi_c avant lui (TRANSCRIPTION.md).
internal static partial class hdd_c
{
    // pcem: hdd.c:19 — la géométrie des sept disques, remplie par loadconfig
    // (pc.c:719-774) et lue par hdd_load (hdd_file.c:150).
    //
    // L'INDICE EST UNE LETTRE DE LECTEUR DOS, pas un numéro de contrôleur :
    // hdc[0] = C:, hdc[1] = D:, … hdc[6] = I:. D'où les préfixes de clés
    // `hdc_*`, `hdd_*`, `hde_*` … Et `hdd_controller` ci-dessous n'a RIEN à voir
    // avec les `hdd_*` de géométrie : collision de préfixe fortuite, chez PCem.
    internal static PcemHDC[] hdc = new PcemHDC[7];

    // pcem: hdd.c:22 — le nom interne de la carte, lu par loadconfig (pc.c:688)
    // et consommé par resetpchard (pc.c:392).
    //
    // DEVIATION: `char[16]` devient une string. Le C tronque à 15 caractères par
    //   strncpy ; le nom interne le plus long du registre de PCem est
    //   "ibmscsi_mca", onze caractères, donc la troncature n'a jamais lieu.
    internal static string hdd_controller_name = "";

    // pcem: ide.c:105 — `char ide_fn[7][512]`, les chemins d'image des sept disques.
    //
    // DÉFINI DANS ide/ide.c, le fichier qu'on ne veut surtout pas lier. PCem lui-même
    // ne l'inclut jamais pour ça : ses six consommateurs re-déclarent l'extern
    // localement, y compris mfm_xebec.c:26. Il vit donc ici, à côté de la géométrie
    // qu'il complète — et il DOIT porter la même valeur des deux côtés du diff, sans
    // quoi hdd_load ouvre un fichier d'un côté et pas de l'autre, et
    // xebec_set_switches calcule deux `switches` différents : divergence dès le
    // premier `in 0x322`.
    //
    // pcem bug, not reproduced: PB-29 — sans objet : rien à reproduire, relevé au passage.
    // scsi_ibm.c:21 déclare `ide_fn[4][512]` contre le `[7][512]` réel d'ide.c:105.
    // Bornes divergentes sur le même objet, dans deux unités de traduction. Sans
    // objet ici — scsi_ibm.c n'est pas transcrit — mais l'entrée revient au registre
    // des défauts de PCem.
    internal static string[] ide_fn = new string[7] { "", "", "", "", "", "", "" };

    // omitted: hdd_controllers[] et les seize HDD_CONTROLLER (hdd.c:149-164,
    //   :167-182), hdd_controller_get_name/get_internal_name/get_flags/available
    //   (:28-50), is_mfm/is_ide/is_scsi/has_config/get_device (:52-122),
    //   current_is_* (:124-126), hdd_controller_init_builtin (:166) — registre de
    //   cartes enfichables, même arbitrage que SOUND_CARD (§ M9) — et que VIDEO_CARD
    //   jusqu'à M15, où la VGA en a fait une table à deux entrées (video.cs).
    //   hdd_controller_init (:128-140) se réduit à son unique effet, un
    //   device_add, écrit sur place dans pc.resetpchard().
    // omitted: null_hdd_device (:24, redéfini :147) — la carte « None ».
}
