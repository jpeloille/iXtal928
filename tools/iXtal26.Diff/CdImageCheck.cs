// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// cdimage-check [DOSSIER] (G10.3) — LE MOTEUR D'IMAGES DE CD, des deux côtés.
//
// Les images d'isogen (tools/isogen/isogen.py) ouvertes par l'image_open de PCem (harness_cdrom.cpp,
// qui inclut cdrom_image.cpp et cdrom-image.cc) et par celui du C# (Cdrom/), puis la même suite
// d'appels de part et d'autre : chaque entrée de la table atapi (ide_atapi.h:8-26), sur des tampons
// préremplis du même motif, et le rappel audio. Après CHAQUE appel, l'état entier est comparé :
// l'objet cdrom, la table posée, image_changed, la capacité, le lecteur audio, cd_buffer (haché), les
// pistes (number, track_number, attr, start, length, skip, sectorSize, mode2, le fichier et l'état de
// son ifstream), image_path et mcn. Puis des CONSTATS, sur ces mêmes valeurs identiques des deux côtés :
// ce que PCem fait vraiment là où le registre le dit (PB-106, PB-107, PB-108, la capacité, l'échec
// collant, la TOC brute). Un constat faux rougit la porte : la lecture du C l'aurait mal compris.
//
// Hors machine : rien n'amorce, rien ne passe par l'IDE ni par le son (G10.4, G10.5). Sans DOSSIER, la
// porte écrit les images elle-même dans un répertoire temporaire, les vérifie contre isogen.sha256 et
// les efface en sortant (IsoGen).

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using iXtal26.Cdrom;
using iXtal26.Ide;

namespace iXtal26.Diff;

internal static class CdImageCheck
{
    [DllImport(Oracle.Lib)] private static extern void h_cd_reset();
    [DllImport(Oracle.Lib)] private static extern void h_cd_fin();
    [DllImport(Oracle.Lib)] private static extern int h_cd_open([MarshalAs(UnmanagedType.LPUTF8Str)] string path);
    [DllImport(Oracle.Lib)] private static extern void h_cd_close();
    [DllImport(Oracle.Lib)] private static extern void h_cd_null_open();
    [DllImport(Oracle.Lib)] private static extern void h_cd_set_drive(int drive, int old);
    [DllImport(Oracle.Lib)] private static extern long h_cd_call(int op, long a, long b, long c, long d, byte[] buf, int off);
    [DllImport(Oracle.Lib)] private static extern void h_cd_audio_callback(short[] output, int len);
    [DllImport(Oracle.Lib)] private static extern int h_cd_state(long[] v, int max, byte[] path, int pathmax, byte[] mcn, int mcnmax);

    // Les entrées de la table, dans l'ordre de ide_atapi.h:8-26 (et de l'enum de harness_cdrom.cpp).
    private enum Op
    {
        Ready, MediumChanged, ReadToc, ReadTocSession, ReadTocRaw, Subchannel, ReadSector, ReadSectorRaw,
        PlayAudio, Seek, Load, Eject, Pause, Resume, Size, Status, IsTrackAudio, Stop, Exit,
    }

    private const int TAILLE = 16384;       // le tampon d'un appel : sept secteurs bruts, prérempli
    private const int ECHANTILLONS = 8820;  // CD_BUFLEN * 2 (sound.h:18-19), le compte de sound_cd_thread
    private const int PLAGE = 150;          // les deux secondes qui séparent LBA et MSF

    private static readonly string[] Champs =
    [
        "cdrom", "atapi", "image_changed", "cdrom_capacity", "image_cd_state", "image_cd_pos", "image_cd_end",
        "cd_buflen", "cdrom_drive", "old_cdrom_drive", "cd_buffer (FNV)", "pistes",
    ];

    private static readonly string[] ChampsPiste =
        ["number", "track_number", "attr", "start", "length", "skip", "sectorSize", "mode2", "fichier", "rdstate"];

    private static int appels, etats, cas, casRouges, ecarts, constats, constatsFaux, appelsAvant;
    private static string nomCas = "", bilan = "";
    private static bool casRouge;
    private static byte[] dernier = [];

    internal static int Run(string? dossier)
    {
        Oracle.CheckAbi();
        string? genere = null;
        try
        {
            if (dossier is null)
                dossier = genere = IsoGen.Generer();
            Tout(dossier);
        }
        catch (Exception e)
        {
            // isogen qui échoue ou dérive, un côté qui monte une image que l'autre refuse : la porte
            // s'arrête, rouge, et le dit — plutôt qu'avorter le processus.
            casRouges++;
            Console.WriteLine($"ROUGE : la porte s'arrête {(cas == 0 ? "avant le premier cas" : $"au cas « {nomCas} »")} sur " +
                              $"{e.GetType().Name} — {e.Message}");
        }
        finally
        {
            h_cd_fin();
            Raz();
            if (genere is not null)
                IsoGen.Effacer(genere);
        }

        Console.WriteLine();
        var vert = casRouges == 0 && constatsFaux == 0 && cas > 0;
        Console.WriteLine(vert
            ? $"Vert : {cas} cas, {appels} appels et {etats} états identiques des deux côtés ; {constats} constats vérifiés."
            : $"ROUGE : {casRouges} cas sur {cas} divergent ({ecarts} écarts), {constatsFaux} constat(s) faux sur {constats}.");
        return vert ? 0 : 1;
    }

    private static void Tout(string d)
    {
        string F(string nom) => Path.Combine(d, nom);

        // Les ISO, et ce que LoadIsoFile refuse.
        foreach (var nom in new[]
                 {
                     "iso-2048.iso", "iso-2048-queue.iso", "iso-2048-sa-texte.iso", "iso-2048-sa-rem.iso",
                     "iso-2352-mode1.bin", "iso-2352-mode2.bin", "hsf-2048.iso", "tronque-pvd.iso", "creuse-2g5.iso",
                 })
            Image(F(nom));
        foreach (var nom in new[] { "iso-2336-mode2.bin", "vide.iso", "sans-pvd.iso", "absent.iso" })
            Refus(F(nom));
        Refus(d, "un répertoire");   // ifstream l'ouvre puis échoue en lecture (badbit), FileStream le refuse

        // Les feuilles CUE, et leurs refus.
        foreach (var nom in new[]
                 {
                     "mixte.cue", "multi.cue", "formats.cue", "style.cue", "ok-ligne511.cue", "bizarre-recul.cue",
                 })
            Image(F(nom));
        foreach (var nom in new[]
                 {
                     "refus-piste2.cue", "refus-trou.cue", "refus-index.cue", "refus-commande.cue", "refus-type.cue",
                     "refus-wave.cue", "refus-absent.cue", "refus-guillemet.cue", "refus-ligne512.cue",
                     "refus-msf.cue", "refus-sans-piste.cue",
                 })
            Refus(F(nom));

        Suites(d);
        Nulle();
        Constats(d);
        Bilan();
    }

    // ---- les cas -------------------------------------------------------------------------------------

    /// <summary>Une image que PCem monte : ouverte, puis toute la batterie, puis fermée.</summary>
    private static void Image(string chemin)
    {
        Debut(Path.GetFileName(chemin));
        Lecteur(ide.CDROM_IMAGE, ide.CDROM_IMAGE);
        if (Ouvre(chemin) != 0 || cdrom_image.cdrom is null)
        {
            Ecart("image_open", "l'image devait se monter des deux côtés");
            return;
        }

        var t = cdrom_image.cdrom!.tracks;
        bilan = $"montée, {t.Count - 1} piste(s) et le lead-out, capacité {cdrom_image.cdrom_capacity}";
        Batterie();
        Ferme();
        Fermee();
    }

    /// <summary>Une image que PCem refuse : image_open rend 1, cdrom reste nul, atapi aussi.</summary>
    private static void Refus(string chemin, string? nom = null)
    {
        Debut((nom ?? Path.GetFileName(chemin)) + " (refus)");
        Lecteur(ide.CDROM_IMAGE, ide.CDROM_IMAGE);
        if (Ouvre(chemin) != 1)
            Ecart("image_open", "l'image devait être refusée");
        bilan = "refusée";
        Appel(Op.Ready);
        Appel(Op.Size);
    }

    /// <summary>Les suites d'ouvertures : image_changed, medium_changed, cdrom_drive, l'échec d'image_open
    /// qui laisse atapi et la capacité, la réouverture sans fermeture.</summary>
    private static void Suites(string d)
    {
        Debut("suites d'ouvertures");
        Lecteur(ide.CDROM_IMAGE, ide.CDROM_IMAGE);
        Ouvre(Path.Combine(d, "iso-2048.iso"));
        Appel(Op.MediumChanged);
        Appel(Op.MediumChanged);
        Ouvre(Path.Combine(d, "iso-2048.iso"));
        Appel(Op.MediumChanged);
        Ouvre(Path.Combine(d, "iso-2352-mode1.bin"));
        Appel(Op.Ready);
        Appel(Op.MediumChanged);
        Lecteur(ide.CDROM_IMAGE, 0);
        Appel(Op.MediumChanged);
        Appel(Op.MediumChanged);
        Ouvre(Path.Combine(d, "absent.iso"));
        Appel(Op.Ready);
        Appel(Op.MediumChanged);
        Appel(Op.Size);
        Appel(Op.Status);
        Lecteur(0, 0);
        Ouvre(Path.Combine(d, "mixte.cue"));
        Appel(Op.Size);
        Appel(Op.Ready);
        Appel(Op.MediumChanged);
        Lecteur(-1, 0);
        Appel(Op.MediumChanged);
        Ouvre(Path.Combine(d, "mixte.cue"));
        Appel(Op.MediumChanged);
        Ferme();
        Appel(Op.Size);
        Appel(Op.Status);
        Ferme();
    }

    /// <summary>Le lecteur sans disque : cdrom_null_open, puis les dix-neuf entrées.</summary>
    private static void Nulle()
    {
        Debut("lecteur sans disque (cdrom-null.c)");
        appels++;
        h_cd_null_open();
        cdrom_null.cdrom_null_open(0);
        Etat("cdrom_null_open");
        Tous();
    }

    // ---- la batterie -----------------------------------------------------------------------------------

    private static void Batterie()
    {
        var pistes = cdrom_image.cdrom!.tracks.ToArray();
        var donnees = pistes[..^1];

        Appel(Op.Ready);
        Appel(Op.Ready);
        Appel(Op.MediumChanged);
        Appel(Op.MediumChanged);
        Appel(Op.Size);
        Appel(Op.Status);

        // La TOC : toutes les combinaisons de piste de départ, de format, de longueur et de « single ».
        foreach (var debut in new long[] { 0, 1, 2, 3, 4, 0xAA, 0xAB, 0xFF })
        foreach (var msf in new long[] { 0, 1 })
        foreach (var max in new long[] { 0, 4, 12, 20, 28, 804 })
        foreach (var seule in new long[] { 0, 1 })
            Appel(Op.ReadToc, debut, msf, max, seule);
        foreach (var msf in new long[] { 0, 1 })
        foreach (var max in new long[] { 0, 12, 804 })
            Appel(Op.ReadTocSession, msf, max);
        foreach (var max in new long[] { 0, 4, 14, 15, 26, 37, 804 })
            Appel(Op.ReadTocRaw, max);

        // Les secteurs : d'abord dans les pistes, puis aux bords — un échec de lecture colle au fichier.
        foreach (var s in new long[] { 0, 1, 15, 16, 17 })
            Appel(Op.ReadSector, s, 1);
        foreach (var t in donnees)
        {
            Appel(Op.ReadSector, t.start, 1);
            Appel(Op.ReadSector, t.start + 1, 1);
            Appel(Op.ReadSector, t.start + t.length / 2, 1);
            Appel(Op.ReadSector, t.start, 2);
            Appel(Op.ReadSector, t.start, 3);
            Appel(Op.ReadSectorRaw, t.start);
            Appel(Op.ReadSectorRaw, t.start + 1, 0, 0, 0, 100);
        }
        Appel(Op.ReadSector, 16, 0);
        Appel(Op.ReadSectorRaw, 16);
        foreach (var t in donnees)
        {
            Appel(Op.ReadSector, t.start + t.length - 1, 1);
            Appel(Op.ReadSector, t.start + t.length - 1, 3);
            Appel(Op.ReadSectorRaw, t.start + t.length - 1);
            Appel(Op.ReadSector, t.start + t.length, 1);
            Appel(Op.ReadSector, t.start - 1, 1);
        }
        foreach (var s in new long[] { pistes[^1].start, pistes[^1].start - 1, -1, int.MaxValue, int.MinValue })
            Appel(Op.ReadSector, s, 1);

        // Le sous-canal et la nature des pistes, à l'arrêt.
        Sous();
        foreach (var t in donnees)
        {
            Appel(Op.IsTrackAudio, t.start, 0);
            Appel(Op.IsTrackAudio, t.start + PLAGE, 0);
            Appel(Op.IsTrackAudio, Msf(t.start + PLAGE), 1);
            Appel(Op.IsTrackAudio, Msf(t.start), 1);
        }

        // Le lecteur audio : chaque piste, en LBA ; puis en MSF ; sous 150 ; jusqu'au bout du disque.
        foreach (var t in donnees)
        {
            Appel(Op.PlayAudio, t.start + PLAGE, 200, 0);
            Rappels(1);
            Sous();
            Appel(Op.Pause);
            Rappels(1);
            Sous();
            Appel(Op.Status);
            Appel(Op.Resume);
            Rappels(2);
            Appel(Op.Seek, t.start + PLAGE + 5);
            Appel(Op.Status);
            Rappels(1);
            Appel(Op.Resume);
            Appel(Op.Stop);
            Appel(Op.Status);
        }
        var der = donnees[^1];
        Appel(Op.PlayAudio, Msf(der.start + PLAGE), Msf(der.start + PLAGE + 30), 1);
        Rappels(2);
        Sous();
        Appel(Op.PlayAudio, 10, 5, 0);
        Rappels(2);
        Appel(Op.PlayAudio, der.start + PLAGE, 1_000_000, 0);
        for (var i = 0; i < 60 && cdrom_image.image_cd_state == cdrom_image.CD_PLAYING; i++)
            Rappels(1);
        Rappels(1);
        Sous();
        Appel(Op.Status);

        Appel(Op.Load);
        Appel(Op.Eject);
        Appel(Op.Exit);
    }

    /// <summary>Après image_close : la table reste image_atapi, chaque entrée voit cdrom nul.</summary>
    private static void Fermee() => Tous();

    /// <summary>Les dix-neuf entrées de la table active, une fois.</summary>
    private static void Tous()
    {
        Appel(Op.Ready);
        Appel(Op.MediumChanged);
        Appel(Op.ReadToc, 0, 0, 804, 0);
        Appel(Op.ReadTocSession, 1, 804);
        Appel(Op.ReadTocRaw, 804);
        Appel(Op.Subchannel, 1, 0, 0, 0, 5);
        Appel(Op.ReadSector, 16, 1);
        Appel(Op.ReadSectorRaw, 16);
        Appel(Op.PlayAudio, PLAGE, 10, 0);
        Rappels(1);
        Appel(Op.Seek, 0);
        Appel(Op.Load);
        Appel(Op.Eject);
        Appel(Op.Pause);
        Appel(Op.Resume);
        Appel(Op.Size);
        Appel(Op.Status);
        Appel(Op.IsTrackAudio, PLAGE, 0);
        Appel(Op.Stop);
        Appel(Op.Exit);
    }

    private static void Sous()
    {
        Appel(Op.Subchannel, 0);
        Appel(Op.Subchannel, 1, 0, 0, 0, 5);
    }

    // MSF compacté (0x00MMSSFF) d'un nombre de trames.
    private static long Msf(long f) => ((f / 4500) << 16) | ((f / 75 % 60) << 8) | (f % 75);

    // ---- les constats ----------------------------------------------------------------------------------

    private static void Constats(string d)
    {
        string F(string nom) => Path.Combine(d, nom);
        var iso = File.ReadAllBytes(F("iso-2048.iso"));
        var pvd = iso.AsSpan(16 * 2048, 2048).ToArray();

        Debut("constats");
        Lecteur(ide.CDROM_IMAGE, ide.CDROM_IMAGE);

        Ouvre(F("iso-2048.iso"));
        Constat(Appel(Op.Size) == 33, "la capacité est la fin + 1 : 33 pour un volume de 32 secteurs (cdrom-image.cc:475)");
        Appel(Op.ReadTocRaw, 804);
        Constat(Appel(Op.ReadTocRaw, 804) == 15, "la TOC brute n'a qu'une entrée, sans lead-out (cdrom-image.cc:400)");

        Constat(Ouvre(F("iso-2336-mode2.bin")) == 1,
                "PB-106 : une vraie image MODE2/2336 est refusée (CanReadPVD lit 24 octets après le secteur, il en faut 8)");

        Ouvre(F("formats.cue"));
        Appel(Op.ReadSector, 32 + 16, 1);
        Constat(dernier[0] != 1 && dernier.AsSpan(0, 2032).SequenceEqual(pvd.AsSpan(16)),
                "PB-106 : la piste MODE2/2336 d'une feuille se lit 16 octets trop loin (le secteur 16 rend le PVD à partir de son 16e octet)");

        Ouvre(F("creuse-2g5.iso"));
        var t = cdrom_image.cdrom!.tracks;
        Constat(t[0].length < 0 && Appel(Op.ReadSector, 16, 1) == 1,
                $"PB-107 : une image de 2 684 354 560 octets prend une longueur négative ({t[0].length}), et rien ne se lit");

        Ouvre(F("multi.cue"));
        var p2 = cdrom_image.cdrom!.tracks[1];
        var piste2 = File.ReadAllBytes(F("multi-piste2.bin"));
        Appel(Op.ReadSectorRaw, p2.start);
        Constat(p2.start == 37 && dernier.AsSpan(0, 2352).SequenceEqual(piste2.AsSpan(0, 2352)),
                "PB-108 : INDEX 00 en 00:00:00 ne compte pas — la piste 2 (INDEX 01 au secteur 5 de son fichier) rend le secteur 0, son prégap");

        Ouvre(F("tronque-pvd.iso"));
        t = cdrom_image.cdrom!.tracks;
        var bf = (CDROM_Interface_Image.BinaryFile)t[0].file!;
        Constat(t[0].sectorSize == 2048 && t[0].length == 0 && bf.failbit,
                "l'échec collant : une image coupée sept octets après le début du PVD est montée, de longueur 0 (getLength rend -1 sous failbit)");

        Ouvre(F("multi.cue"));
        var p3 = cdrom_image.cdrom!.tracks[2];
        var r1 = Appel(Op.ReadSector, p3.start + p3.length - 1, 1);
        var r2 = Appel(Op.ReadSector, p3.start, 1);
        Constat(r1 == 1 && r2 == 1,
                "l'échec collant : le dernier secteur, incomplet, de la piste 3 échoue, et la piste ne se relit plus");
        Ferme();
    }

    private static void Constat(bool ok, string texte)
    {
        constats++;
        if (!ok)
            constatsFaux++;
        Console.WriteLine($"  constat {(ok ? "vrai" : "FAUX")} : {texte}");
    }

    // ---- les deux côtés ----------------------------------------------------------------------------------

    private static void Raz()
    {
        cdrom_image.image_clear_state_for_oracle_parity();
        cdrom_ioctl.cdrom_drive = cdrom_ioctl.old_cdrom_drive = 0;
        ide_atapi.atapi = null;
    }

    private static void Bilan()
    {
        if (cas > 0)
            Console.WriteLine($"  {nomCas}{(bilan.Length > 0 ? $" : {bilan}" : "")} — {appels - appelsAvant} appels{(casRouge ? ", ÉCARTS" : "")}");
        bilan = "";
        appelsAvant = appels;
    }

    private static void Debut(string nom)
    {
        Bilan();
        cas++;
        nomCas = nom;
        casRouge = false;
        h_cd_reset();
        Raz();
        Etat("début");
    }

    private static void Ecart(string pas, string quoi)
    {
        ecarts++;
        if (!casRouge)
        {
            casRouge = true;
            casRouges++;
        }
        if (ecarts <= 40)
            Console.WriteLine($"ÉCART {nomCas} — {pas} : {quoi}");
    }

    private static void Lecteur(int drive, int old)
    {
        h_cd_set_drive(drive, old);
        cdrom_ioctl.cdrom_drive = drive;
        cdrom_ioctl.old_cdrom_drive = old;
        Etat($"cdrom_drive {drive}, old_cdrom_drive {old}");
    }

    private static int Ouvre(string chemin)
    {
        appels++;
        var ro = h_cd_open(chemin);
        var rc = cdrom_image.image_open(chemin);
        var pas = $"image_open({Path.GetFileName(chemin)})";
        if (ro != rc)
            Ecart(pas, $"rend {ro} / {rc}");
        Etat(pas);
        return ro;
    }

    private static void Ferme()
    {
        appels++;
        h_cd_close();
        cdrom_image.image_close();
        Etat("image_close");
    }

    private static long Appel(Op op, long a = 0, long b = 0, long c = 0, long d = 0, int off = 0)
    {
        appels++;
        var bo = Prerempli();
        var bc = Prerempli();
        var ro = h_cd_call((int)op, a, b, c, d, bo, off);
        var rc = AppelCs(op, a, b, c, d, bc, off);
        var pas = $"{op}({a}, {b}, {c}, {d}{(off != 0 ? $", +{off}" : "")})";
        if (ro != rc)
            Ecart(pas, $"rend {ro} / {rc}");
        var i = Premier(bo, bc);
        if (i >= 0)
            Ecart(pas, $"tampon, octet {i} : {bo[i]:X2} / {bc[i]:X2}");
        Etat(pas);
        dernier = bo.AsSpan(off).ToArray();
        return ro;
    }

    private static long AppelCs(Op op, long a, long b, long c, long d, byte[] buf, int off)
    {
        var t = ide_atapi.atapi;
        if (t is null)
            return long.MinValue;
        switch (op)
        {
        case Op.Ready: return t.ready();
        case Op.MediumChanged: return t.medium_changed();
        case Op.ReadToc: return t.readtoc(buf, off, unchecked((byte)a), (int)b, (int)c, (int)d);
        case Op.ReadTocSession: return t.readtoc_session(buf, off, (int)a, (int)b);
        case Op.ReadTocRaw: return t.readtoc_raw(buf, off, (int)a);
        case Op.Subchannel: return t.getcurrentsubchannel(buf, off, (int)a);
        case Op.ReadSector: return t.readsector(buf, off, unchecked((int)a), unchecked((int)b));
        case Op.ReadSectorRaw: t.readsector_raw(buf, off, unchecked((int)a)); return 0;
        case Op.PlayAudio: t.playaudio(unchecked((uint)a), unchecked((uint)b), (int)c); return 0;
        case Op.Seek: t.seek(unchecked((uint)a)); return 0;
        case Op.Load: t.load(); return 0;
        case Op.Eject: t.eject(); return 0;
        case Op.Pause: t.pause(); return 0;
        case Op.Resume: t.resume(); return 0;
        case Op.Size: return t.size();
        case Op.Status: return t.status();
        case Op.IsTrackAudio: return t.is_track_audio(unchecked((uint)a), (int)b);
        case Op.Stop: t.stop(); return 0;
        case Op.Exit: t.exit(); return 0;
        }
        return long.MinValue + 1;
    }

    private static void Rappels(int n)
    {
        for (var k = 0; k < n; k++)
        {
            appels++;
            var so = Motif16();
            var sc = Motif16();
            h_cd_audio_callback(so, ECHANTILLONS);
            cdrom_image.image_audio_callback(sc, ECHANTILLONS);
            for (var i = 0; i < ECHANTILLONS; i++)
            {
                if (so[i] == sc[i])
                    continue;
                Ecart("rappel audio", $"échantillon {i} : {so[i]} / {sc[i]}");
                break;
            }
            Etat("rappel audio");
        }
    }

    private static void Etat(string pas)
    {
        etats++;
        var v = new long[12 + 10 * 256];
        var po = new byte[1100];
        var mo = new byte[300];
        var n = h_cd_state(v, v.Length, po, po.Length, mo, mo.Length);
        if (n < 0)
        {
            Ecart(pas, "h_cd_state : tampon trop court");
            return;
        }
        var vo = v[..n];
        var (vc, pc, mc) = EtatCs();
        var champ = DiffEtat(vo, vc);
        if (champ is not null)
            Ecart(pas, "état, " + champ);
        var cheminO = CStr(po);
        if (cheminO != pc)
            Ecart(pas, $"image_path « {cheminO} » / « {pc} »");
        var mcnO = CStr(mo);
        if (mcnO != mc)
            Ecart(pas, $"mcn « {mcnO} » / « {mc} »");
    }

    private static (long[] v, string path, string mcn) EtatCs()
    {
        var v = new List<long>();
        var cd = cdrom_image.cdrom;
        var a = ide_atapi.atapi;
        v.Add(cd is null ? 0 : 1);
        v.Add(a is null ? 0 : ReferenceEquals(a, cdrom_null.null_atapi) ? 1 : ReferenceEquals(a, cdrom_image.image_atapi) ? 2 : 4);
        v.Add(cdrom_image.image_changed);
        v.Add(cdrom_image.cdrom_capacity);
        v.Add(cdrom_image.image_cd_state);
        v.Add(cdrom_image.image_cd_pos);
        v.Add(cdrom_image.image_cd_end);
        v.Add(cdrom_image.cd_buflen);
        v.Add(cdrom_ioctl.cdrom_drive);
        v.Add(cdrom_ioctl.old_cdrom_drive);
        v.Add(unchecked((long)Fnv(cdrom_image.cd_buffer)));
        var nt = cd is null ? -1 : cd.tracks.Count;
        v.Add(nt);
        var vus = new List<CDROM_Interface_Image.TrackFile>();
        for (var i = 0; i < nt; i++)
        {
            var t = cd!.tracks[i];
            var rang = -1;
            if (t.file is not null)
            {
                rang = vus.IndexOf(t.file);
                if (rang < 0)
                {
                    vus.Add(t.file);
                    rang = vus.Count - 1;
                }
            }
            v.Add(t.number);
            v.Add(t.track_number);
            v.Add(t.attr);
            v.Add(t.start);
            v.Add(t.length);
            v.Add(t.skip);
            v.Add(t.sectorSize);
            v.Add(t.mode2 ? 1 : 0);
            v.Add(rang);
            v.Add(t.file is CDROM_Interface_Image.BinaryFile bf
                ? (bf.badbit ? 1 : 0) | (bf.eofbit ? 2 : 0) | (bf.failbit ? 4 : 0)
                : -1);
        }
        return (v.ToArray(), cdrom_image.image_path, cd?.mcn ?? "");
    }

    private static string? DiffEtat(long[] o, long[] c)
    {
        for (var i = 0; i < Math.Max(o.Length, c.Length); i++)
        {
            var vo = i < o.Length ? o[i].ToString() : "—";
            var vc = i < c.Length ? c[i].ToString() : "—";
            if (vo == vc)
                continue;
            var nom = i < Champs.Length ? Champs[i] : $"piste {(i - Champs.Length) / 10 + 1}, {ChampsPiste[(i - Champs.Length) % 10]}";
            return $"{nom} : {vo} / {vc}";
        }
        return null;
    }

    private static ulong Fnv(byte[] b)
    {
        var h = 0xcbf29ce484222325UL;
        foreach (var x in b)
        {
            h ^= x;
            h *= 0x100000001b3UL;
        }
        return h;
    }

    private static string CStr(byte[] b)
    {
        var n = Array.IndexOf(b, (byte)0);
        return System.Text.Encoding.UTF8.GetString(b, 0, n < 0 ? b.Length : n);
    }

    private static byte[] Prerempli()
    {
        var b = new byte[TAILLE];
        for (var i = 0; i < b.Length; i++)
            b[i] = (byte)(0xC5 ^ (i * 7));
        return b;
    }

    private static short[] Motif16()
    {
        var s = new short[ECHANTILLONS];
        for (var i = 0; i < s.Length; i++)
            s[i] = (short)(0x5A5A ^ (i * 13));
        return s;
    }

    private static int Premier(byte[] a, byte[] b)
    {
        for (var i = 0; i < a.Length; i++)
        {
            if (a[i] != b[i])
                return i;
        }
        return -1;
    }
}

// isogen (tools/isogen/isogen.py) pour les portes du CD : les images écrites dans un répertoire temporaire
// (sous TMPDIR, celui de la série), vérifiées contre tools/isogen/isogen.sha256 — un générateur qui
// dérive rougit la porte —, effacées en sortant, l'image creuse de 2,5 Go comprise : finally à la fin
// de la porte, ProcessExit, et un gestionnaire de SIGTERM, SIGINT, SIGHUP et SIGQUIT — sous Linux, .NET
// ne lève pas ProcessExit sur SIGTERM (mesuré : le répertoire restait). Seul SIGKILL y échappe.
internal static class IsoGen
{
    private static readonly List<PosixSignalRegistration> Signaux = [];

    internal static string Generer()
    {
        var d = Path.Combine(Path.GetTempPath(), $"isogen-{Environment.ProcessId}");
        Effacer(d);
        Directory.CreateDirectory(d);
        AppDomain.CurrentDomain.ProcessExit += (_, _) => Effacer(d);
        foreach (var s in new[] { PosixSignal.SIGTERM, PosixSignal.SIGINT, PosixSignal.SIGHUP, PosixSignal.SIGQUIT })
            Signaux.Add(PosixSignalRegistration.Create(s, _ => Effacer(d)));
        try
        {
            using (var p = Process.Start(new ProcessStartInfo("python3", ["tools/isogen/isogen.py", d])
                   {
                       RedirectStandardOutput = true,
                       RedirectStandardError = true,
                   })!)
            {
                var sortie = p.StandardOutput.ReadToEnd();
                var erreurs = p.StandardError.ReadToEnd();
                p.WaitForExit();
                Console.Write(sortie);
                Console.Error.Write(erreurs);
                if (p.ExitCode != 0)
                    throw new InvalidOperationException($"isogen : code de sortie {p.ExitCode}");
            }

            Verifier(d);
            return d;
        }
        catch
        {
            Effacer(d);
            throw;
        }
    }

    private static void Verifier(string d)
    {
        var n = 0;
        foreach (var l in File.ReadAllLines("tools/isogen/isogen.sha256"))
        {
            if (l.Length < 67)
                continue;
            var attendu = l[..64];
            var nom = l[66..];
            string calcule;
            using (var f = File.OpenRead(Path.Combine(d, nom)))
                calcule = Convert.ToHexStringLower(SHA256.HashData(f));
            if (calcule != attendu)
                throw new InvalidOperationException($"isogen : {nom} n'a pas l'empreinte d'isogen.sha256 — le générateur a dérivé.");
            n++;
        }

        var ecrits = Directory.EnumerateFiles(d, "*", SearchOption.AllDirectories).Count();
        if (ecrits != n)
            throw new InvalidOperationException($"isogen : {ecrits} fichiers écrits, {n} dans isogen.sha256.");
        Console.WriteLine($"isogen : {n} fichiers conformes à isogen.sha256.");
    }

    internal static void Effacer(string d)
    {
        try
        {
            if (Directory.Exists(d))
                Directory.Delete(d, true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
