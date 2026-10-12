// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — outillage de vérification.
//
// Les cas qui discriminent les corrections du moteur d'images de CD (G13.7c1, PLAN-G13.md), en C# seul : PB-106 (le
// secteur de 2 336 octets), PB-108 (la piste qui ouvre son fichier), PB-109 (l'échec collant), PB-122 (le lead-out de
// la TOC). Hors machine, comme cdimage-check : chaque cas écrit ses images dans un dossier du TMPDIR, les ouvre par le
// moteur (CDROM_Interface_Image, ou image_open et la table du pilote pour la TOC) et lit ce qu'un lecteur rendrait.
// Chaque secteur porte sa marque : son numéro dans le fichier, répété — le secteur lu se reconnaît à son premier octet.

using iXtal26.Cdrom;
using iXtal26.Ide;
using static iXtal26.Ide.ide_atapi;

namespace iXtal26.Diff;

internal static partial class MaterielCas
{
    /// <summary>Un cas du moteur, dans un dossier du TMPDIR effacé ensuite ; le pilote d'images remis à zéro.</summary>
    private static int AvecCd(Func<string, int> corps)
    {
        var tmp = Path.Combine(Path.GetTempPath(), $"materiel-cas-cd-{Environment.ProcessId}");
        Directory.CreateDirectory(tmp);
        try
        {
            return corps(tmp);
        }
        catch (Exception e)
        {
            Console.WriteLine($"  [ECHEC] ARRÊT — {e.GetType().Name} : {e.Message.Trim()}");
            return 1;
        }
        finally
        {
            cdrom_image.image_clear_state_for_oracle_parity();
            ide_atapi.atapi = null;
            Directory.Delete(tmp, true);
        }
    }

    /// <summary>Des secteurs de <paramref name="taille"/> octets, chacun rempli de son numéro (modulo 256).</summary>
    private static byte[] Marques(int secteurs, int taille)
    {
        var b = new byte[secteurs * taille];
        for (var i = 0; i < b.Length; i++)
            b[i] = (byte)(i / taille);
        return b;
    }

    /// <summary>Le PVD d'ISO 9660 (type 1, « CD001 », version 1) posé à <paramref name="o"/>.</summary>
    private static void Pvd(byte[] b, int o)
    {
        b[o] = 1;
        "CD001"u8.CopyTo(b.AsSpan(o + 1));
        b[o + 6] = 1;
    }

    private static string Ecrire(string d, string nom, byte[] contenu)
    {
        var chemin = Path.Combine(d, nom);
        File.WriteAllBytes(chemin, contenu);
        return chemin;
    }

    private static string Feuille(string d, string nom, params string[] lignes) =>
        Ecrire(d, nom, System.Text.Encoding.ASCII.GetBytes(string.Join("\n", lignes) + "\n"));

    private static CDROM_Interface_Image? Monter(string chemin)
    {
        var c = new CDROM_Interface_Image();
        if (c.SetDevice(chemin, 0))
            return c;
        c.Dispose();
        return null;
    }

    /// <summary>Le premier octet du secteur <paramref name="lba"/> lu (brut ou cuit), ou -1 si la lecture échoue.</summary>
    private static int Lire(CDROM_Interface_Image c, int lba, bool brut)
    {
        var b = new byte[2352];
        return c.ReadSector(b, 0, brut, (ulong)lba) ? b[0] : -1;
    }

    // PB-106 — un secteur XA mode 2 de 2 336 octets commence au sous-en-tête (8 octets) : ses données sont à +8
    // (SFF-8020i r2.6, Figure 11). PCem lit à +24. Une image MODE2/2336 de 20 secteurs, son PVD au secteur 16 : PCem la
    // refuse, le matériel la monte et son secteur 16 rend le PVD dès l'octet 0. La même image en piste MODE2/2336 d'une
    // feuille : PCem rend le secteur 16 à partir du 16e octet du PVD, le matériel à partir de l'octet 0.
    private static int Pb106(bool hw) => AvecCd(d =>
    {
        var bad = 0;
        var img = new byte[20 * 2336];
        for (var s = 0; s < 20; s++)
            img.AsSpan(s * 2336 + 8, 2048).Fill((byte)s);
        Pvd(img, 16 * 2336 + 8);
        var iso = Ecrire(d, "m2336.bin", img);
        using (var c = Monter(iso))
        {
            var lu = c is null ? "refusée" : $"montée, secteur 16 : {Lire(c, 16, false):X2}h";
            bad += Voir("l'image MODE2/2336, son PVD au secteur 16", hw ? lu == "montée, secteur 16 : 01h" : lu == "refusée", lu,
                        hw ? "montée, secteur 16 : 01h (le PVD dès l'octet 0)" : "refusée", hw);
        }

        var cue = Feuille(d, "m2336.cue", "FILE \"m2336.bin\" BINARY", "  TRACK 01 MODE2/2336", "    INDEX 01 00:00:00");
        using (var c = Monter(cue))
        {
            var b = new byte[2048];
            var ok = c is not null && c.ReadSector(b, 0, false, 16);
            var vu = ok ? $"{b[0]:X2} {b[1]:X2} {b[2]:X2}" : "échec";
            bad += Voir("la piste MODE2/2336 d'une feuille, le secteur 16 cuit", ok && (hw ? b[0] == 1 && b[1] == (byte)'C' : b[0] == 0x10 && b[1] == 0x10),
                        vu, hw ? "01 43 44 (le PVD dès l'octet 0)" : "10 10 10 (le 16e octet du PVD et la suite)", hw);
        }

        // Témoin : MODE2/2352, où +24 est juste, égal dans les deux modes.
        var brut = new byte[20 * 2352];
        for (var s = 0; s < 20; s++)
            brut.AsSpan(s * 2352 + 24, 2048).Fill((byte)(0x40 + s));
        Ecrire(d, "m2352.bin", brut);
        var cue2 = Feuille(d, "m2352.cue", "FILE \"m2352.bin\" BINARY", "  TRACK 01 MODE2/2352", "    INDEX 01 00:00:00");
        using (var c = Monter(cue2))
        {
            var v = c is null ? -2 : Lire(c, 16, false);
            bad += Voir("témoin : la piste MODE2/2352, le secteur 16 cuit", v == 0x50, $"{v:X2}h", "50h (+24, dans les deux modes)", hw);
        }

        return bad;
    });

    // PB-108 — les positions d'INDEX se comptent depuis le début du FILE (CDRWIN User's Guide, annexe A ; libodraw, CUE
    // sheet format § 5.6) : la piste qui ouvre son fichier y commence à son INDEX 01. Un fichier audio de 30 trames, chacune
    // marquée de son numéro ; la piste 2, seule dans ce fichier, INDEX 01 en 00:00:05 : son premier secteur est la trame 5.
    // PCem rend la trame 0 avec INDEX 00 en 00:00:00 ou sans INDEX 00, la trame 3 avec INDEX 00 en 00:00:02 (il retranche
    // l'INDEX 00). Puis la piste 1 qui ouvre la feuille, INDEX 00 en 00:00:00 et INDEX 01 en 00:00:05, suivie dans le même
    // fichier de la piste 2 en 00:00:15 : PCem décale les deux pistes de cinq trames. Témoin : la piste dans le fichier de
    // la précédente, INDEX 00 en 00:00:10 et INDEX 01 en 00:00:15, égale dans les deux modes — sa trame, et la longueur
    // de la piste 1, qui finit à l'INDEX 00 (le seul effet du décalage dans ce cas : le décalage dans le fichier n'en
    // dépend pas).
    private static int Pb108(bool hw) => AvecCd(d =>
    {
        var bad = 0;
        Ecrire(d, "donnees.bin", Marques(10, 2048));
        Ecrire(d, "audio.bin", Marques(30, 2352));

        int Debut(string nom, string[] piste2, int numero = 2)
        {
            string[] tete = ["FILE \"donnees.bin\" BINARY", "  TRACK 01 MODE1/2048", "    INDEX 01 00:00:00",
                             "FILE \"audio.bin\" BINARY", "  TRACK 02 AUDIO"];
            using var c = Monter(Feuille(d, nom, [.. tete, .. piste2]));
            if (c is null)
                return -2;
            var p = c.tracks[numero - 1];
            return Lire(c, p.start, true);
        }

        foreach (var (nom, lignes, pcem, quoi) in new[]
                 {
                     ("i00-0.cue", new[] { "    INDEX 00 00:00:00", "    INDEX 01 00:00:05" }, 0, "INDEX 00 en 00:00:00"),
                     ("i00-2.cue", new[] { "    INDEX 00 00:00:02", "    INDEX 01 00:00:05" }, 3, "INDEX 00 en 00:00:02"),
                     ("sans-i00.cue", new[] { "    INDEX 01 00:00:05" }, 0, "sans INDEX 00"),
                 })
        {
            var v = Debut(nom, lignes);
            var attendu = hw ? 5 : pcem;
            bad += Voir($"la piste 2 dans son fichier, INDEX 01 en 00:00:05, {quoi} : la trame de son premier secteur", v == attendu,
                        $"{v}", $"{attendu}", hw);
        }

        using (var c = Monter(Feuille(d, "piste1.cue", "FILE \"audio.bin\" BINARY", "  TRACK 01 AUDIO", "    INDEX 00 00:00:00",
                                      "    INDEX 01 00:00:05", "  TRACK 02 AUDIO", "    INDEX 01 00:00:15")))
        {
            var (v1, v2) = c is null ? (-2, -2) : (Lire(c, c.tracks[0].start, true), Lire(c, c.tracks[1].start, true));
            var (a1, a2) = hw ? (5, 15) : (0, 10);
            bad += Voir("la piste 1 qui ouvre la feuille (INDEX 00 en 00:00:00, INDEX 01 en 00:00:05), puis la piste 2 en 00:00:15 " +
                        "du même fichier : les trames de leurs premiers secteurs", (v1, v2) == (a1, a2), $"{v1}, {v2}", $"{a1}, {a2}", hw);
        }

        using (var c = Monter(Feuille(d, "meme.cue", "FILE \"audio.bin\" BINARY", "  TRACK 01 AUDIO", "    INDEX 01 00:00:00",
                                      "  TRACK 02 AUDIO", "    INDEX 00 00:00:10", "    INDEX 01 00:00:15")))
        {
            var (v, n) = c is null ? (-2, -2) : (Lire(c, c.tracks[1].start, true), c.tracks[0].length);
            bad += Voir("témoin : la piste 2 dans le fichier de la piste 1, INDEX 00 en 00:00:10, INDEX 01 en 00:00:15 ; la trame " +
                        "de son premier secteur, la longueur de la piste 1", (v, n) == (15, 10), $"{v}, {n}",
                        "15, 10 (la piste 1 finit à l'INDEX 00), dans les deux modes", hw);
        }

        return bad;
    });

    // PB-109 — un lecteur qui échoue sur un secteur lit le suivant (déduit : l'échec collant est un état de la
    // libstdc++, sans analogue matériel). Une feuille sur un fichier de 20 secteurs et 1 000 octets : le 21e secteur,
    // incomplet, échoue dans les deux modes ; puis le secteur 0 : PCem échoue encore, le matériel le lit. Une image ISO
    // coupée sept octets après le début de son PVD (16 × 2 048 + 7 octets) : PCem la monte de longueur 0 (getLength
    // rend -1 sous failbit), le matériel de 16 secteurs.
    private static int Pb109(bool hw) => AvecCd(d =>
    {
        var bad = 0;
        var f = Marques(21, 2048).AsSpan(0, 20 * 2048 + 1000).ToArray();
        Ecrire(d, "court.bin", f);
        using (var c = Monter(Feuille(d, "court.cue", "FILE \"court.bin\" BINARY", "  TRACK 01 MODE1/2048", "    INDEX 01 00:00:00")))
        {
            var (dernier, premier) = c is null ? (-2, -2) : (Lire(c, 20, false), Lire(c, 0, false));
            bad += Voir("le secteur 20, incomplet, puis le secteur 0", dernier == -1 && premier == (hw ? 0 : -1),
                        $"{dernier}, {premier}", hw ? "-1 (échec), 0 (lu)" : "-1, -1 (le fichier ne se lit plus)", hw);
        }

        var t = new byte[16 * 2048 + 7];
        Pvd(t, 16 * 2048);
        using (var c = Monter(Ecrire(d, "tronque.iso", t)))
        {
            var n = c is null ? -2 : c.tracks[0].length;
            bad += Voir("l'image coupée sept octets après le début du PVD : sa longueur", n == (hw ? 16 : 0), $"{n}",
                        hw ? "16 secteurs" : "0 (getLength rend -1 sous failbit)", hw);
        }

        return bad;
    });

    // PB-122 — le lead-out de la TOC porte ADR 1 et le contrôle de la dernière piste (SFF-8020i r2.6 § 10.8.19, Table
    // 131 : le point A2h a ADR 1 ; le contrôle, déduit). READ TOC (format 0, LBA, depuis la piste 1) d'une ISO de 20
    // secteurs : le descripteur du lead-out vaut 00 00 AA 00 00 00 00 14 chez PCem, 00 14 AA 00 00 00 00 14 sur le
    // matériel. D'une feuille dont la dernière piste est audio : 00 10 AA 00 sur le matériel. Les pistes, en témoins.
    private static int Pb122(bool hw) => AvecCd(d =>
    {
        var bad = 0;
        var iso = Marques(20, 2048);
        Pvd(iso, 16 * 2048);
        Ecrire(d, "iso.iso", iso);
        Ecrire(d, "audio.bin", Marques(30, 2352));
        var cue = Feuille(d, "mixte.cue", "FILE \"iso.iso\" BINARY", "  TRACK 01 MODE1/2048", "    INDEX 01 00:00:00",
                          "FILE \"audio.bin\" BINARY", "  TRACK 02 AUDIO", "    INDEX 01 00:00:00");

        foreach (var (chemin, pistes, controle) in new[] { (Path.Combine(d, "iso.iso"), 1, 0x14), (cue, 2, 0x10) })
        {
            if (cdrom_image.image_open(chemin) != 0)
            {
                bad += Voir($"{Path.GetFileName(chemin)} montée", false, "refusée", "montée", hw);
                continue;
            }
            var b = new byte[804];
            var len = ide_atapi.atapi!.readtoc(b, 0, 1, 0, 804, 0);
            var lo = 4 + pistes * 8;
            var desc = Convert.ToHexString(b, lo, 8);
            var attendu = hw ? controle : 0;
            bad += Voir($"{Path.GetFileName(chemin)} : READ TOC, le descripteur du lead-out", len == lo + 8 && b[lo + 1] == attendu && b[lo + 2] == 0xAA,
                        desc, $"00 {attendu:X2} AA 00 …", hw);
            var temoin = b[5] == 0x14 && (pistes == 1 || b[13] == 0x10);
            bad += Voir($"témoin : {Path.GetFileName(chemin)}, les octets ADR/contrôle des pistes", temoin,
                        pistes == 1 ? $"{b[5]:X2}" : $"{b[5]:X2}, {b[13]:X2}", pistes == 1 ? "14" : "14, 10", hw);
            cdrom_image.image_close();
        }

        return bad;
    });

    /// <summary>La feuille mixte.cue d'isogen (une piste de données, LBA 0 ; la piste 2 audio en LBA 42, trente
    /// secteurs ; la piste 3 audio en LBA 222 ; le lead-out en 247), montée par le pilote d'images ; le corps ; le
    /// dossier d'isogen effacé ensuite.</summary>
    private static int AvecMixte(Func<ATAPI, string, int> corps) => AvecCd(_ =>
    {
        var d = IsoGen.Generer();
        try
        {
            if (cdrom_image.image_open(Path.Combine(d, "mixte.cue")) != 0)
                return Voir("mixte.cue montée", false, "refusée", "montée", false);
            return corps(ide_atapi.atapi!, d);
        }
        finally
        {
            IsoGen.Effacer(d);
        }
    });

    private static string Msf(byte[] b, int o) => $"{b[o + 1]:D2}:{b[o + 2]:D2}:{b[o + 3]:D2}";

    private static uint Be(byte[] b, int o) => (uint)(b[o] << 24 | b[o + 1] << 16 | b[o + 2] << 8 | b[o + 3]);

    // PB-123 — la position du lecteur compte les 150 secteurs de l'amorce : 00:02:00 est le LBA 0 (SFF-8020i r2.6
    // § 10.8.8) ; READ SUB-CHANNEL rend l'adresse absolue et la relative au début de la piste (§ 10.8.18, Table 115).
    // PLAY AUDIO en LBA depuis 42, trente secteurs : PCem range 42 (ramené à 150) et 72, rien ne joue ; le matériel 192
    // et 222. SEEK au LBA 42 : PCem 42, le matériel 192. PLAY AUDIO MSF depuis 00:02:42, huit secteurs joués : en MSF,
    // l'absolue 00:02:50 des deux côtés, la relative 00:02:08 chez PCem, 00:00:08 ; en LBA, 200 et 158 chez PCem, 50 et 8.
    private static int Pb123(bool hw) => AvecMixte((a, d) =>
    {
        var bad = 0;
        a.playaudio(42, 30, 0);
        var (p, e) = (cdrom_image.image_cd_pos, cdrom_image.image_cd_end);
        bad += Voir("PLAY AUDIO en LBA depuis 42, trente secteurs : la position et la fin du lecteur", (p, e) == (hw ? (192u, 222u) : (150u, 72u)),
                    $"{p}, {e}", hw ? "192, 222" : "150, 72 (rien ne joue)", hw);
        // Le son rendu : le premier secteur joué est le secteur 42 de mixte.bin (INDEX 01 de la piste 2) ; PCem ne
        // joue rien (des zéros).
        var son = new short[1176];
        cdrom_image.image_audio_callback(son, son.Length);
        var fichier = File.ReadAllBytes(Path.Combine(d, "mixte.bin")).AsSpan(42 * 2352, 2352);
        var attendu = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, short>(fichier).ToArray();
        var ok = hw ? son.SequenceEqual(attendu) : son.All(x => x == 0);
        bad += Voir("le son rendu par le premier rappel", ok, ok ? (hw ? "le secteur 42" : "des zéros") : $"premiers échantillons {son[0]}, {son[1]}",
                    hw ? "le secteur 42 de mixte.bin" : "des zéros (rien ne joue)", hw);
        a.stop();
        a.seek(42);
        p = cdrom_image.image_cd_pos;
        bad += Voir("SEEK au LBA 42 : la position du lecteur", p == (hw ? 192u : 42u), $"{p}", hw ? "192" : "42", hw);
        a.playaudio(0x00022A, 0x000300, 1);
        cdrom_image.image_audio_callback(new short[1176 * 8], 1176 * 8);
        var b = new byte[16];
        a.getcurrentsubchannel(b, 0, 1);
        var (abs, rel) = (Msf(b, 3), Msf(b, 7));
        bad += Voir("PLAY AUDIO MSF depuis 00:02:42, huit secteurs joués ; READ SUB-CHANNEL en MSF : l'absolue, la relative",
                    (abs, rel) == ("00:02:50", hw ? "00:00:08" : "00:02:08"), $"{abs}, {rel}", hw ? "00:02:50, 00:00:08" : "00:02:50, 00:02:08", hw);
        a.getcurrentsubchannel(b, 0, 0);
        var (la, lr) = (Be(b, 3), Be(b, 7));
        bad += Voir("READ SUB-CHANNEL en LBA : l'absolue, la relative", (la, lr) == (hw ? (50u, 8u) : (200u, 158u)), $"{la}, {lr}",
                    hw ? "50, 8" : "200, 158", hw);
        return bad;
    });

    // PB-214 — PLAY AUDIO MSF : la plage demandée est jouée (SFF-8020i r2.6 § 10.8.9). Une feuille à piste 1 audio (LBA 0
    // à 2 999) et piste 2 de données (3 000 à 5 999) ; PLAY AUDIO MSF de 00:20:00 à 00:30:00 (LBA 1 350 à 2 100) : la
    // valeur compactée, 1400h = 5 120, tombe dans la piste 2 ; PCem annule la lecture (arrêté), le matériel joue
    // (en cours, depuis la position 1 500). Puis 00:41:25, à la fin de la piste audio : en lecture des deux côtés.
    private static int Pb214(bool hw) => AvecCd(d =>
    {
        Ecrire(d, "a.bin", new byte[3000 * 2352]);
        Ecrire(d, "d.bin", new byte[3000 * 2048]);
        var cue = Feuille(d, "ad.cue", "FILE \"a.bin\" BINARY", "  TRACK 01 AUDIO", "    INDEX 01 00:00:00",
                          "FILE \"d.bin\" BINARY", "  TRACK 02 MODE1/2048", "    INDEX 01 00:00:00");
        if (cdrom_image.image_open(cue) != 0)
            return Voir("ad.cue montée", false, "refusée", "montée", hw);
        ide_atapi.atapi!.playaudio(0x001400, 0x001E00, 1);
        var (etat, pos) = (cdrom_image.image_cd_state, cdrom_image.image_cd_pos);
        var bad = Voir("PLAY AUDIO MSF de 00:20:00 à 00:30:00 : l'état du lecteur, sa position", hw ? etat == cdrom_image.CD_PLAYING && pos == 1500
                                                                                                  : etat == cdrom_image.CD_STOPPED,
                       $"état {etat}, position {pos}", hw ? "en cours (1), 1500" : "arrêté (0)", hw);
        // 00:41:25, le LBA 2 950, à la fin de la piste audio (3 100 sans les 150 : la piste de données) ; la valeur
        // compactée, 2919h, hors des pistes : en lecture dans les deux modes.
        ide_atapi.atapi!.stop();
        ide_atapi.atapi!.playaudio(0x002919, 0x002A00, 1);
        etat = cdrom_image.image_cd_state;
        return bad + Voir("PLAY AUDIO MSF depuis 00:41:25 (LBA 2 950, la fin de la piste audio) : l'état du lecteur",
                          etat == cdrom_image.CD_PLAYING, $"état {etat}", "en cours (1), dans les deux modes", hw);
    });

    // PB-215 — le contrôle de piste de PLAY AUDIO MSF : 00:02:00 est le LBA 0 (§ 10.8.8). mixte.cue : 00:02:00 (LBA 0,
    // des données) — PCem le juge audio (au LBA 150, la piste 2), le matériel non ; 00:05:05 (LBA 230, la piste 3) —
    // PCem le juge hors des pistes (380), le matériel audio. Témoin : le LBA 42 en LBA, audio des deux côtés.
    private static int Pb215(bool hw) => AvecMixte((a, _) =>
    {
        var (x, y, t) = (a.is_track_audio(0x000200, 1), a.is_track_audio(0x000505, 1), a.is_track_audio(42, 0));
        return Voir("is_track_audio de 00:02:00, de 00:05:05 (MSF), du LBA 42", (x, y, t) == (hw ? (0, 1, 1) : (1, 0, 1)),
                    $"{x}, {y}, {t}", hw ? "0, 1, 1" : "1, 0, 1", hw);
    });
}
