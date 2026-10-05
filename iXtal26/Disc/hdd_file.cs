// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/hdd/hdd_file.c  (hdd_file_t : includes/private/hdd/hdd_file.h:8-16)
// STATUS: partial — la branche HDD_IMG_RAW seule. VHD et ramdisk omis.
//
// Le calque de secteurs du disque dur : ouvrir, lire, écrire, formater, fermer.
// Voisin de Disc/disc_img.cs, son pendant pour la disquette, et mêmes conventions.
//
// VHD ET RAMDISK SONT ÉCARTÉS, et c'est le seul arbitrage de ce fichier.
// hdd_file.c inclut minivhd/ et ramdisk/ : 3 723 + 273 lignes de C tiers, dont
// 1 797 (cwalk.c, libxml2_encoding.c) ne servent qu'aux VHD DIFFÉRENTIELS et à
// leurs chemins parents UTF-16. Un 5160 avec une image brute n'en emprunte pas une
// ligne. Les deux prédicats d'aiguillage — mvhd_file_is_vhd (par contenu) et
// is_ramdisk_file (par extension) — sont donc rendus faux, et les deux branches
// mortes marquées sur place. Coût : deux stubs contre 3 996 lignes.

// CS8602 : `hdd.f` est un FILE* que le C ne teste JAMAIS dans les trois fonctions
// d'E/S (hdd_file.c:165, :195, :228) — la garde est chez l'appelant, qui lit
// `drive->hdd_file.f` avant d'émettre une commande (mfm_xebec.c:733). Reproduire
// cette absence de test est le comportement ; ajouter un `!` par site réécrirait
// les lignes, comme dans device.cs.
#pragma warning disable CS8602

namespace iXtal26.Disc;

// pcem: hdd_file.h:3-7
internal enum hdd_img_type
{
    HDD_IMG_RAW,
    HDD_IMG_VHD,
    HDD_IMG_RAW_RAM,
}

// pcem: hdd_file.h:8-16 — classe et non struct : mfm_xebec.c:753 prend l'adresse
// de drive->hdd_file et la passe à hdd_load.
//
// DEVIATION: `void *f` devient un FileStream. Le C y range un FILE*, un MVHDMeta*
//   ou un ramdisk_t* selon img_type ; les deux derniers étant omis, le type se
//   resserre sur le seul qui reste.
internal sealed class hdd_file_t
{
    internal FileStream? f;
    internal int spt;
    internal int hpc;
    internal int tracks;
    internal int sectors;
    internal int read_only;
    internal hdd_img_type img_type;
}

internal static partial class hdd_file
{
    // DEVIATION: pclog() appartient à plugin-api/logging.c, pas transcrit. Shim
    //   local, comme mem_bios.cs:20 et disc_img.cs.
    private static void pclog(string s) => Console.Error.Write(s);

    // DEVIATION: stdio de la libc, comme disc_img.cs:77. Trois modes ici et non
    //   deux : "rb" en lecture seule, "rb+" en lecture-écriture, et "wb+" pour la
    //   CRÉATION — voir hdd_load_ext.
    //
    //   `enoent` sort l'information qu'errno porte en C : hdd_load_ext distingue
    //   « le fichier n'existe pas, j'en crée un » de « il existe et refuse de
    //   s'ouvrir », et sans ce drapeau les deux chemins se confondraient.
    private static FileStream? fopen(string s, string mode, out bool enoent)
    {
            enoent = false;
            try
            {
                    return mode switch
                    {
                            "rb" => new FileStream(s, FileMode.Open, FileAccess.Read),
                            "wb+" => new FileStream(s, FileMode.Create, FileAccess.ReadWrite),
                            _ => new FileStream(s, FileMode.Open, FileAccess.ReadWrite),
                    };
            }
            catch (FileNotFoundException) { enoent = true; return null; }
            catch (DirectoryNotFoundException) { enoent = true; return null; }
            catch (IOException) { return null; }
            catch (UnauthorizedAccessException) { return null; }
            catch (ArgumentException) { return null; }
    }

    // omitted: is_ramdisk_file (hdd_file.c:12-24) — teste les extensions .rdimg
    //   et .rdvhd. Rendu FAUX en dur : le ramdisk est hors périmètre, voir
    //   l'en-tête. Le `read_only = 1` qu'il imposait (:29-30) tombe avec lui.
    // omitted: mvhd_file_is_vhd (hdd_file.c:42) et tout le bloc VHD (:42-64) —
    //   rendu FAUX en dur, même raison.

    // pcem: hdd_file.c:26-148
    internal static void hdd_load_ext(hdd_file_t hdd, string fn, int spt, int hpc, int tracks, int read_only)
    {
            // omitted: `int requested_read_only = read_only;` (:27) — sa seule
            //   lecture est dans le bloc ramdisk (:129), omis.

            if (hdd.f == null)
            {
                    /* Try to open existing hard disk image */
                    bool enoent;
                    if (read_only != 0)
                            hdd.f = fopen(fn, "rb", out enoent);
                    else
                            hdd.f = fopen(fn, "rb+", out enoent);
                    if (hdd.f != null)
                    {
                            hdd.img_type = hdd_img_type.HDD_IMG_RAW;
                    }
                    else
                    {
                            /* Failed to open existing hard disk image */
                            if (enoent && read_only == 0)
                            {
                                    /* Failed because it does not exist,
                                       so try to create new file */
                                    hdd.f = fopen(fn, "wb+", out _);
                                    if (hdd.f == null)
                                    {
                                            pclog($"Cannot create file '{fn}'\n");
                                            return;
                                    }
                            }
                            else
                            {
                                    /* Failed for another reason */
                                    pclog($"Cannot open file '{fn}'\n");
                                    return;
                            }
                            hdd.img_type = hdd_img_type.HDD_IMG_RAW;
                    }
            }
            if (hdd.img_type == hdd_img_type.HDD_IMG_RAW)
            {
                    hdd.spt = spt;
                    hdd.hpc = hpc;
                    hdd.tracks = tracks;
            }
            hdd.sectors = hdd.spt * hdd.hpc * hdd.tracks;
            hdd.read_only = read_only;

            // omitted: le bloc ramdisk (:96-147) — ramdisk_init/set_size/load_file.
    }

    // pcem: hdd_file.c:150
    internal static void hdd_load(hdd_file_t hdd, int d, string fn)
        => hdd_load_ext(hdd, fn, hdd_c.hdc[d].spt, hdd_c.hdc[d].hpc, hdd_c.hdc[d].tracks, 0);

    // pcem: hdd_file.c:152-163
    internal static void hdd_close(hdd_file_t hdd)
    {
            if (hdd.f != null)
            {
                    // omitted: les branches VHD (mvhd_close) et ramdisk
                    //   (ramdisk_free) de :155-159.
                    if (hdd.img_type == hdd_img_type.HDD_IMG_RAW)
                            hdd.f.Close();
            }
            hdd.img_type = hdd_img_type.HDD_IMG_RAW;
            hdd.f = null;
    }

    // pcem: hdd_file.c:165-192
    internal static int hdd_read_sectors(hdd_file_t hdd, int offset, int nr_sectors, uint8_t[] buffer)
    {
            if (hdd.img_type == hdd_img_type.HDD_IMG_RAW)
            {
                    long addr;
                    int transfer_sectors = nr_sectors;

                    // TRONCATURE SILENCIEUSE, et c'est le comportement : une lecture
                    // qui déborde du disque rend 1, mais les octets lus sont quand
                    // même dans le tampon. L'appelant ne peut pas distinguer « rien
                    // lu » de « lu en partie ».
                    if ((hdd.sectors - offset) < transfer_sectors)
                            transfer_sectors = hdd.sectors - offset;
                    addr = (long)offset * 512;

                    // pcem bug, not reproduced: PB-125 — au-delà de la fin (offset > sectors), transfer_sectors est
                    //   négatif et fread reçoit une taille énorme (:179) ; un offset négatif (un LBA de 2^31 ou plus,
                    //   que le ZIP prend de l'invité) fait échouer fseeko64 et lit depuis la position courante. Les deux
                    //   écrivent au-delà de buffer.
                    // DEVIATION: rien n'est lu, buffer garde son contenu ; le retour est 1, comme une lecture tronquée.
                    if (offset < 0 || transfer_sectors < 0)
                    {
                            Diag.R9.Garde("hdd_file.c:179");
                            return 1;
                    }

                    hdd.f.Seek(addr, SeekOrigin.Begin);
                    hdd.f.ReadAtLeast(buffer.AsSpan(0, transfer_sectors * 512),
                                      transfer_sectors * 512, false);

                    if (nr_sectors != transfer_sectors)
                            return 1;
                    return 0;
            }
            /* Keep the compiler happy */
            return 1;
    }

    // pcem: hdd_file.c:194-225
    internal static int hdd_write_sectors(hdd_file_t hdd, int offset, int nr_sectors, uint8_t[] buffer)
    {
            if (hdd.img_type == hdd_img_type.HDD_IMG_RAW)
            {
                    long addr;
                    int transfer_sectors = nr_sectors;

                    if (hdd.read_only != 0)
                            return 1;

                    if ((hdd.sectors - offset) < transfer_sectors)
                            transfer_sectors = hdd.sectors - offset;
                    addr = (long)offset * 512;

                    // pcem bug, not reproduced: PB-125 — au-delà de la fin, fwrite reçoit une taille énorme et lit
                    //   au-delà de buffer (:212) ; un offset négatif écrit à la position courante du fichier.
                    // DEVIATION: rien n'est écrit ; le retour est 1, comme une écriture tronquée.
                    if (offset < 0 || transfer_sectors < 0)
                    {
                            Diag.R9.Garde("hdd_file.c:212");
                            return 1;
                    }

                    hdd.f.Seek(addr, SeekOrigin.Begin);
                    hdd.f.Write(buffer, 0, transfer_sectors * 512);

                    if (nr_sectors != transfer_sectors)
                            return 1;
                    return 0;
            }
            /* Keep the compiler happy */
            return 1;
    }

    // pcem: hdd_file.c:227-263
    internal static int hdd_format_sectors(hdd_file_t hdd, int offset, int nr_sectors)
    {
            if (hdd.img_type == hdd_img_type.HDD_IMG_RAW)
            {
                    long addr;
                    int c;
                    uint8_t[] zero_buffer = new uint8_t[512];
                    int transfer_sectors = nr_sectors;

                    if (hdd.read_only != 0)
                            return 1;

                    // Array.Clear plutôt que memset : le tableau CLR est déjà nul à
                    // l'allocation, mais la ligne du C a une contrepartie (R6).
                    Array.Clear(zero_buffer);

                    if ((hdd.sectors - offset) < transfer_sectors)
                            transfer_sectors = hdd.sectors - offset;
                    addr = (long)offset * 512;

                    hdd.f.Seek(addr, SeekOrigin.Begin);
                    // SECTEUR PAR SECTEUR, là où lecture et écriture font un seul
                    // bloc (:179, :212). C'est ce que fait le C ; le fondre en une
                    // seule écriture de transfer_sectors * 512 serait l'optimisation
                    // évidente, et ce fichier n'en est pas le lieu.
                    for (c = 0; c < transfer_sectors; c++)
                            hdd.f.Write(zero_buffer, 0, 512);

                    if (nr_sectors != transfer_sectors)
                            return 1;
                    return 0;
            }
            /* Keep the compiler happy */
            return 1;
    }
}
