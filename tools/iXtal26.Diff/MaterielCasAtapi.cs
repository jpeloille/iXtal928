// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — outillage de vérification.
//
// Les cas qui discriminent les corrections des commandes du lecteur de CD (G13.7c2, PLAN-G13.md), en C# seul : PB-117
// (READ CD-ROM CAPACITY), PB-119 (MODE SELECT), PB-120 (la TOC brute). Sur le banc de r9-atapi : l'ami486 amorcé, le
// lecteur en maître secondaire, chargé d'une image que le cas écrit, ses ports (170h-177h) conduits depuis l'outil et
// nIEN posé ; chaque commande rend son état, son erreur et ses données. Chaque attendu porte sa source.

using iXtal26.Scsi;

namespace iXtal26.Diff;

internal static partial class MaterielCas
{
    /// <summary>L'ami486 et son lecteur ATAPI chargé de <paramref name="image"/>, puis le corps ; la machine fermée ensuite.</summary>
    private static int AvecAtapi(Func<string, string> image, Func<int> corps, string? modele = null, bool prepare = true) => AvecCd(d =>
    {
        var cfg = Path.Combine(d, "atapi.cfg");
        var chemin = image(d);
        File.WriteAllText(cfg, "model = ami486\ncpu = 10\nmem_size = 4096\ngfxcard = tvga9000b\nhdd_controller = ide\n" +
                               (chemin.Length > 0 ? $"cdrom_channel = 2\ncdrom_drive = 200\ncdrom_path = {chemin}\n"
                                                  : "cdrom_channel = 2\ncdrom_drive = -1\n") +
                               (modele is null ? "" : $"cd_model = {modele}\n"));
        try
        {
            if (!pc.loadconfig(cfg))
                throw new InvalidOperationException("loadconfig");
            Floppy.fdd_c.discfns[0] = Floppy.fdd_c.discfns[1] = "";
            for (var u = 0; u < 4; u++)
                Disc.hdd_c.ide_fn[u] = "";
            if (!pc.initpc("roms"))
                throw new InvalidOperationException("initpc");
            R9Atapi.Tranches(100);
            io.outb(0x376, 0x02);
            if (prepare)
                R9Atapi.Prepare();
            return corps();
        }
        finally
        {
            try { pc.closepc(); } catch (Exception) { }
        }
    });

    /// <summary>Une ISO de <paramref name="n"/> secteurs marqués, son PVD au secteur 16.</summary>
    private static string Iso(string d, int n)
    {
        var b = Marques(n, 2048);
        Pvd(b, 16 * 2048);
        return Ecrire(d, $"iso{n}.iso", b);
    }

    /// <summary>Un paquet ; les données qu'il rend, ou celles qu'on lui envoie ; puis l'état et l'erreur.</summary>
    private static (byte st, byte err, byte[] d) Paquet(byte[] cdb, byte[]? sortie = null)
    {
        var lu = new List<byte>();
        if (!R9Atapi.Paquet(cdb))
            return (R9Atapi.St(), 0xFF, []);
        if (sortie is not null)
        {
            if (R9Atapi.AttendreBsy() && (R9Atapi.St() & 0x08) != 0)
                for (var i = 0; i < sortie.Length; i += 2)
                    io.outw(0x170, (ushort)(sortie[i] | (i + 1 < sortie.Length ? sortie[i + 1] << 8 : 0)));
        }
        else
        {
            for (var bloc = 0; bloc < 64 && R9Atapi.AttendreBsy() && (R9Atapi.St() & 0x08) != 0; bloc++)
            {
                var n = io.inb(0x174) | (io.inb(0x175) << 8);
                for (var i = 0; i < (n + 1) / 2; i++)
                {
                    var w = io.inw(0x170);
                    lu.Add((byte)w);
                    lu.Add((byte)(w >> 8));
                }
            }
        }
        R9Atapi.AttendreBsy();
        return (R9Atapi.St(), io.inb(0x171), lu.ToArray());
    }

    /// <summary>L'ASC de REQUEST SENSE (son octet 12).</summary>
    private static int Asc() => Paquet(R9Atapi.Cdb(0x03, 0, 0, 0, 18)).d is { Length: >= 13 } s ? s[12] : -1;

    private static string Hex(byte[] b, int o, int n) =>
        string.Join(" ", b.Skip(o).Take(n).Select(x => x.ToString("X2")));

    // PB-117 — READ CD-ROM CAPACITY rend l'adresse du dernier bloc avant le lead-out (SFF-8020i r2.6 § 10.8.14, et la
    // note du modèle CD-ROM : « returns the logical block address of the last block prior to the lead-out area »). Une
    // ISO de 32 secteurs : PCem rend 33 (00000021h), le matériel 31 (0000001Fh) ; la taille du bloc, 2 048 des deux
    // côtés. Témoin : READ(10) du LBA 31, GOOD dans les deux modes.
    private static int Pb117(bool hw) => AvecAtapi(d => Iso(d, 32), () =>
    {
        var bad = 0;
        var (st, err, b) = Paquet(R9Atapi.Cdb(0x25));
        var ok = (st & 1) == 0 && b.Length == 8;
        var dernier = ok ? b[0] << 24 | b[1] << 16 | b[2] << 8 | b[3] : -1;
        var taille = ok ? b[4] << 24 | b[5] << 16 | b[6] << 8 | b[7] : -1;
        bad += Voir("READ CD-ROM CAPACITY d'une ISO de 32 secteurs : le dernier bloc, la taille du bloc",
                    (dernier, taille) == (hw ? 31 : 33, 2048), ok ? $"{dernier}, {taille}" : $"état {st:X2}h, erreur {err:X2}h",
                    hw ? "31, 2048" : "33, 2048", hw);
        (st, err, b) = Paquet(R9Atapi.Read10(31, 1));
        bad += Voir("témoin : READ(10) du LBA 31", (st & 1) == 0 && b.Length == 2048 && b[0] == 31,
                    $"état {st:X2}h, {b.Length} octets", "GOOD, 2 048 octets du secteur 31, dans les deux modes", hw);
        return bad;
    });

    // PB-119 — MODE SELECT (SFF-8020i r2.6 § 10.8.4 et Table 58 ; SCSI-2, Tables 91 et 92). La page audio (0Eh) porte
    // le volume des ports 0 et 1 (ses octets 9 et 11), que le lecteur garde (atapi_get_cd_volume). Cinq temps :
    // MODE SELECT(10) de longueur 0 — PCem entre en phase de données (DRQ) et n'en sort pas, le matériel rend GOOD sans
    // phase ; MODE SELECT(10) de la page, la longueur des données du mode (octets 0 et 1, réservés en MODE SELECT) à
    // 0016h — PCem la prend pour celle des descripteurs et saute la page, le matériel l'applique ; la page puis une
    // page inconnue (0Fh) — PCem applique la première et rend 24h, le matériel rend 26h et ne change rien ; une liste
    // qui tronque la page — PCem lit au-delà et rend GOOD, le matériel 1Ah ; MODE SELECT(6) de la page, en-tête de
    // quatre octets — PCem cherche la page quatre octets trop loin et rend 24h, le matériel l'applique.
    private static int Pb119(bool hw) => AvecAtapi(d => Iso(d, 32), () =>
    {
        var bad = 0;
        static byte[] Page(int v0, int v1) => [0x0E, 0x0E, 0x05, 0x04, 0x00, 0x80, 0x00, 75, 0x01, (byte)v0, 0x02, (byte)v1, 0, 0, 0, 0];
        static byte[] Select10(int n) => R9Atapi.Cdb(0x55, 0x10, 0, 0, 0, 0, 0, n >> 8, n & 0xFF);
        int Vol() => (int)scsi_cd_c.atapi_get_cd_volume(0);

        var accepte = R9Atapi.Paquet(Select10(0));
        R9Atapi.AttendreBsy();
        var st = R9Atapi.St();
        var iir = io.inb(0x172) & 3;
        bad += Voir("MODE SELECT(10) de longueur 0 : le paquet accepté, puis DRQ et la raison d'interruption",
                    accepte && (st & 1) == 0 && (hw ? (st & 0x08) == 0 && iir == 3 : (st & 0x08) != 0),
                    $"paquet {(accepte ? "accepté" : "refusé")}, état {st:X2}h, IIR {iir}",
                    hw ? "accepté, GOOD sans DRQ, IIR 3 (la phase d'état)" : "accepté, DRQ (une phase de données sans fin)", hw);
        if ((st & 0x08) != 0)
        {
            R9Atapi.Out(7, 0x08);
            R9Atapi.Tranches(2);
            R9Atapi.Prepare();
        }

        byte[] l1 = [0x00, 0x16, 0, 0, 0, 0, 0, 0, .. Page(0x81, 0x41)];
        var (s1, _, _) = Paquet(Select10(l1.Length), l1);
        var v1 = Vol();
        bad += Voir("MODE SELECT(10) de la page audio, la longueur des données du mode à 0016h : l'état, le volume du port 0",
                    (s1 & 1) == 0 && v1 == (hw ? 0x81 : 0xFF), $"état {s1:X2}h, volume {v1:X2}h",
                    hw ? "GOOD, 81h (la page appliquée)" : "GOOD, FFh (la page sautée)", hw);

        byte[] l2 = [0, 0, 0, 0, 0, 0, 0, 0, .. Page(0x82, 0x42), 0x0F, 0x06, 0, 0, 0, 0, 0, 0];
        var (s2, _, _) = Paquet(Select10(l2.Length), l2);
        var a2 = Asc();
        var v2 = Vol();
        bad += Voir("la page audio puis une page inconnue (0Fh) : l'état, l'ASC, le volume du port 0",
                    (s2 & 1) != 0 && a2 == (hw ? 0x26 : 0x24) && v2 == (hw ? v1 : 0x82), $"état {s2:X2}h, ASC {a2:X2}h, volume {v2:X2}h",
                    hw ? $"ERR, 26h, {v1:X2}h (rien ne change)" : "ERR, 24h, 82h (la première page appliquée)", hw);

        byte[] l3 = [0, 0, 0, 0, 0, 0, 0, 0, .. Page(0x83, 0x43).AsSpan(0, 10)];
        var (s3, _, _) = Paquet(Select10(l3.Length), l3);
        var a3 = (s3 & 1) != 0 ? Asc() : 0;
        bad += Voir("une liste de 18 octets qui tronque la page audio : l'état, l'ASC", hw ? (s3 & 1) != 0 && a3 == 0x1A : (s3 & 1) == 0,
                    $"état {s3:X2}h, ASC {a3:X2}h", hw ? "ERR, 1Ah (PARAMETER LIST LENGTH ERROR)" : "GOOD", hw);

        byte[] l5 = [0, 0, 0, 0];
        var (s5, _, _) = Paquet(Select10(l5.Length), l5);
        var a5 = (s5 & 1) != 0 ? Asc() : 0;
        bad += Voir("MODE SELECT(10) d'une liste de 4 octets, l'en-tête tronqué : l'état, l'ASC", hw ? (s5 & 1) != 0 && a5 == 0x1A : (s5 & 1) == 0,
                    $"état {s5:X2}h, ASC {a5:X2}h", hw ? "ERR, 1Ah" : "GOOD", hw);

        byte[] l6 = [0, 0, 0, 0, 0, 0, 0, 8, 0, 0, 0, 0, 0, 0, 8, 0, .. Page(0x85, 0x45)];
        var (s6, _, _) = Paquet(Select10(l6.Length), l6);
        var a6 = (s6 & 1) != 0 ? Asc() : 0;
        var v6 = Vol();
        bad += Voir("MODE SELECT(10), un descripteur de bloc de 8 octets (sa longueur aux octets 6 et 7), puis la page audio : " +
                    "l'état, l'ASC, le volume du port 0", hw ? (s6 & 1) == 0 && v6 == 0x85 : (s6 & 1) != 0 && a6 == 0x24,
                    $"état {s6:X2}h, ASC {a6:X2}h, volume {v6:X2}h", hw ? "GOOD, 85h" : "ERR, 24h (le descripteur lu comme une page)", hw);

        byte[] l4 = [0, 0, 0, 0, .. Page(0x84, 0x44)];
        var (s4, _, _) = Paquet(R9Atapi.Cdb(0x15, 0x10, 0, 0, l4.Length), l4);
        var a4 = (s4 & 1) != 0 ? Asc() : 0;
        var v4 = Vol();
        // Sous PB-270 (le mode « tout », le domaine « stockage »), MODE SELECT(6) n'existe pas sur ATAPI : 20h.
        bad += Voir("MODE SELECT(6) de la page audio, en-tête de quatre octets : l'état, l'ASC, le volume du port 0",
                    hw && materiel.pb_270 ? (s4 & 1) != 0 && a4 == 0x20
                    : hw ? (s4 & 1) == 0 && v4 == 0x84 : (s4 & 1) != 0 && a4 == 0x24, $"état {s4:X2}h, ASC {a4:X2}h, volume {v4:X2}h",
                    hw && materiel.pb_270 ? "ERR, 20h (PB-270)" : hw ? "GOOD, 84h" : "ERR, 24h", hw);
        return bad;
    });

    // PB-120 — READ TOC au format 10b, la TOC brute (SFF-8020i r2.6 § 10.8.19, Tables 128, 129 et 131) : la longueur
    // (2 + 11 × les descripteurs), une session ; A0h (la première piste, le type du disque), A1h (la dernière), A2h (le
    // lead-out), puis une entrée par piste, POINT = la piste. Une ISO de 32 secteurs : PCem rend `00 00 01 01` et une
    // entrée `01 14 00 00 … 00 02 00` (la piste à l'octet de session, POINT nul) ; le matériel `00 2E 01 01`, A0h, A1h,
    // A2h (00:02:32) et `01 14 00 01 00 00 00 00 00 02 00`. Une feuille dont l'unique piste est en mode 2 (MODE2/2352) :
    // le type du disque 20h (CD-ROM XA) sur le matériel.
    private static int Pb120(bool hw)
    {
        var bad = AvecAtapi(d => Iso(d, 32), () =>
        {
            var (st, _, b) = Paquet(R9Atapi.Cdb(0x43, 0, 2, 0, 0, 0, 0, 0x03, 0x00));
            var vu = $"état {st:X2}h, {b.Length} octets : {Hex(b, 0, Math.Min(b.Length, 48))}";
            var attendu = hw
                ? "00 2E 01 01 01 14 00 A0 00 00 00 00 01 00 00 01 14 00 A1 00 00 00 00 01 00 00 01 14 00 A2 00 00 00 00 00 02 20 01 14 00 01 00 00 00 00 00 02 00"
                : "00 00 01 01 01 14 00 00 00 00 00 00 00 02 00";
            // Le transfert se fait en mots : une longueur impaire (15 chez PCem) est complétée d'un octet.
            var n = (attendu.Length + 1) / 3;
            return Voir("READ TOC, format 10b, d'une ISO de 32 secteurs", (st & 1) == 0 && b.Length == (n + 1) / 2 * 2 &&
                        Hex(b, 0, n) == attendu, vu, attendu, hw);
        });
        bad += AvecAtapi(d =>
        {
            var brut = new byte[20 * 2352];
            Ecrire(d, "xa.bin", brut);
            return Feuille(d, "xa.cue", "FILE \"xa.bin\" BINARY", "  TRACK 01 MODE2/2352", "    INDEX 01 00:00:00");
        }, () =>
        {
            var (st, _, b) = Paquet(R9Atapi.Cdb(0x43, 0, 2, 0, 0, 0, 0, 0x03, 0x00));
            var type = hw ? (b.Length > 13 ? b[13] : -1) : -2;
            return Voir("READ TOC, format 10b, d'une feuille à une piste MODE2/2352 : le type du disque (PSEC de A0h)",
                        (st & 1) == 0 && (hw ? type == 0x20 : b.Length == 16 && b[3] == 1 && b[7] == 0),
                        hw ? $"{type:X2}h" : $"{b.Length} octets, dernière piste {b[3]}, POINT {b[7]}",
                        hw ? "20h (CD-ROM XA)" : "16 octets (15 et le complément), pas de point A0h, POINT nul", hw);
        });
        // Deux pistes, données puis audio : l'ADR/contrôle de A0h (la première), de A1h et de A2h (la dernière) ; puis
        // la longueur d'allocation de 15 octets : la table coupée, sa longueur en tête intacte.
        bad += AvecAtapi(d =>
        {
            Iso(d, 32);
            Ecrire(d, "audio.bin", Marques(30, 2352));
            return Feuille(d, "deux.cue", "FILE \"iso32.iso\" BINARY", "  TRACK 01 MODE1/2048", "    INDEX 01 00:00:00",
                           "FILE \"audio.bin\" BINARY", "  TRACK 02 AUDIO", "    INDEX 01 00:00:00");
        }, () =>
        {
            var (st, _, b) = Paquet(R9Atapi.Cdb(0x43, 0, 2, 0, 0, 0, 0, 0x03, 0x00));
            var vu = $"état {st:X2}h, {b.Length} octets : {Hex(b, 0, Math.Min(b.Length, 26))}";
            var bad2 = Voir("READ TOC, format 10b, de deux pistes (données, audio) : l'en-tête, A0h et A1h", (st & 1) == 0 &&
                            (hw ? Hex(b, 0, 26) == "00 39 01 01 01 14 00 A0 00 00 00 00 01 00 00 01 10 00 A1 00 00 00 00 02 00 00" &&
                                  b.Length == 0x3C && b[27] == 0x10 && b[29] == 0xA2
                                : b.Length == 26 && b[0] == 0 && b[1] == 0 && b[5] == 0x14 && b[16] == 0x10),
                            vu, hw ? "00 39 01 01, A0h en 14h (piste 1), A1h et A2h en 10h (piste 2), 60 octets"
                                   : "00 00 01 02, deux entrées (14h, 10h), 26 octets (22 et le complément)", hw);
            (st, _, b) = Paquet(R9Atapi.Cdb(0x43, 0, 2, 0, 0, 0, 0, 0, 15));
            return bad2 + Voir("la même, sous une longueur d'allocation de 15 octets", (st & 1) == 0 && b.Length == 16 &&
                               (hw ? b[0] == 0 && b[1] == 0x39 && b[7] == 0xA0 : b[0] == 0 && b[1] == 0 && b[4] == 1),
                               $"état {st:X2}h, {b.Length} octets : {Hex(b, 0, b.Length)}",
                               hw ? "16 octets, la longueur 0039h en tête, A0h coupé" : "16 octets, la première entrée", hw);
        });
        return bad;
    }

    // PB-268 — « If a command is issued that requests access to a logical block not within the capacity of the medium,
    // the command is terminated with CHECK CONDITION » (SCSI-2 § 9.1.2) ; 05/21h (SFF-8020i r2.6, Table 87). Une ISO de 32
    // secteurs. READ(10) de 40 blocs depuis le LBA 0 : PCem sert les deux premières tranches de seize secteurs, puis le
    // reste ; le matériel refuse d'emblée, sans données. READ CD (BEh) brut de deux blocs depuis le LBA 31 : PCem rend
    // GOOD ; le matériel 21h. READ(12) de 100000h blocs depuis le LBA 0 : le compte d'octets de PCem déborde ; le
    // matériel 21h, sans données. Témoin : READ(10) des LBA 30 et 31, GOOD dans les deux modes.
    private static int Pb268(bool hw) => AvecAtapi(d => Iso(d, 32), () =>
    {
        var bad = 0;
        var (st, _, b) = Paquet(R9Atapi.Read10(0, 40));
        var a = (st & 1) != 0 ? Asc() : 0;
        bad += Voir("READ(10) de 40 blocs depuis le LBA 0 d'un disque de 32 : l'état, l'ASC, les octets servis",
                    hw ? (st & 1) != 0 && a == 0x21 && b.Length == 0 : b.Length >= 32 * 2048,
                    $"état {st:X2}h, ASC {a:X2}h, {b.Length} octets", hw ? "ERR, 21h, aucun octet" : "au moins 65 536 octets servis", hw);
        (st, _, b) = Paquet(R9Atapi.Cdb(0xBE, 0, 0, 0, 0, 31, 0, 0, 2, 0xF8));
        a = (st & 1) != 0 ? Asc() : 0;
        bad += Voir("READ CD brut de 2 blocs depuis le LBA 31 : l'état, l'ASC", hw ? (st & 1) != 0 && a == 0x21 : (st & 1) == 0,
                    $"état {st:X2}h, ASC {a:X2}h, {b.Length} octets", hw ? "ERR, 21h" : "GOOD", hw);
        if (!hw)
        {
            R9Atapi.Out(7, 0x08);
            R9Atapi.Tranches(2);
            R9Atapi.Prepare();
        }
        (st, _, b) = Paquet(R9Atapi.Cdb(0xA8, 0, 0, 0, 0, 0, 0, 0x10, 0, 0));
        a = (st & 1) != 0 ? Asc() : 0;
        if (hw)
            bad += Voir("READ(12) de 100000h blocs depuis le LBA 0 : l'état, l'ASC, les octets servis", (st & 1) != 0 && a == 0x21 && b.Length == 0,
                        $"état {st:X2}h, ASC {a:X2}h, {b.Length} octets", "ERR, 21h, aucun octet", hw);
        else
        {
            R9Atapi.Out(7, 0x08);
            R9Atapi.Tranches(2);
            R9Atapi.Prepare();
        }
        (st, _, b) = Paquet(R9Atapi.Read10(30, 2));
        bad += Voir("témoin : READ(10) des LBA 30 et 31", (st & 1) == 0 && b.Length == 4096 && b[0] == 30 && b[2048] == 31,
                    $"état {st:X2}h, {b.Length} octets", "GOOD, 4 096 octets des secteurs 30 et 31, dans les deux modes", hw);
        return bad;
    });

    // PB-269 — MECHANISM STATUS (SFF-8020i r2.6 § 10.8.3, Table 45) : la longueur d'allocation aux octets 8 et 9 (l'octet 7
    // est réservé) ; une longueur qui tronque l'en-tête de huit octets rend PARAMETER LIST LENGTH ERROR (05/1Ah), « except
    // for the Mechanism Status Command », la seule (§ 10.2.5). Allocation de 4 octets : PCem rend GOOD et 4 octets, le
    // matériel 1Ah. L'octet 7 à 1, les octets 8 et 9 à 0 : PCem lit 10000h et rend 8 octets, le matériel lit 0, GOOD sans
    // données. Témoin : 8 octets, GOOD dans les deux modes.
    private static int Pb269(bool hw) => AvecAtapi(d => Iso(d, 32), () =>
    {
        var bad = 0;
        var (st, _, b) = Paquet(R9Atapi.Cdb(0xBD, 0, 0, 0, 0, 0, 0, 0, 0, 4));
        var a = (st & 1) != 0 ? Asc() : 0;
        bad += Voir("MECHANISM STATUS, allocation de 4 octets : l'état, l'ASC, les octets", hw ? (st & 1) != 0 && a == 0x1A && b.Length == 0
                                                                                              : (st & 1) == 0 && b.Length == 4,
                    $"état {st:X2}h, ASC {a:X2}h, {b.Length} octets", hw ? "ERR, 1Ah, aucun octet" : "GOOD, 4 octets", hw);
        (st, _, b) = Paquet(R9Atapi.Cdb(0xBD, 0, 0, 0, 0, 0, 0, 1, 0, 0));
        bad += Voir("MECHANISM STATUS, l'octet 7 à 1, les octets 8 et 9 à 0 : l'état, les octets", (st & 1) == 0 && b.Length == (hw ? 0 : 8),
                    $"état {st:X2}h, {b.Length} octets", hw ? "GOOD, aucun octet" : "GOOD, 8 octets", hw);
        (st, _, b) = Paquet(R9Atapi.Cdb(0xBD, 0, 0, 0, 0, 0, 0, 0, 0, 8));
        bad += Voir("témoin : MECHANISM STATUS, allocation de 8 octets", (st & 1) == 0 && b.Length == 8, $"état {st:X2}h, {b.Length} octets",
                    "GOOD, 8 octets, dans les deux modes", hw);
        return bad;
    });

    // PB-118 — GET EVENT STATUS NOTIFICATION selon MMC-2 r11a § 6.1.5 (Tables 162 à 166, 178 à 180), sur le Kenwood
    // UCR-421 (72x, 2000 : un lecteur MMC-2). Deux appels, mode scruté, classe des supports, disque chargé : PCem rend
    // `04 00 04 10 02 02 00 00` les deux fois (la longueur en petit-boutiste, NewMedia toujours) ; le matériel
    // `00 06 04 10 02 02 00 00` puis `00 06 04 10 00 02 00 00`. Sans la classe des supports : PCem `00 00 80 10`, le
    // matériel `00 02 80 10`. La longueur d'allocation aux octets 7 et 8 (00 04), l'octet 9 à 00 : PCem lit 000400h et
    // rend 8 octets, le matériel 4. Lecteur vide : le matériel `00 06 04 10 00 00 00 00`.
    private static int Pb118(bool hw)
    {
        static byte[] Gesn(int classes, int a7, int a8) => R9Atapi.Cdb(0x4A, 1, 0, 0, classes, 0, 0, a7, a8, 0);
        var bad = AvecAtapi(d => Iso(d, 32), () =>
        {
            var b1 = Paquet(Gesn(0x10, 0, 8)).d;
            var b2 = Paquet(Gesn(0x10, 0, 8)).d;
            var (h1, h2) = (Hex(b1, 0, b1.Length), Hex(b2, 0, b2.Length));
            var r = Voir("deux GET EVENT STATUS NOTIFICATION, classe des supports, disque chargé",
                         hw ? h1 == "00 06 04 10 02 02 00 00" && h2 == "00 06 04 10 00 02 00 00"
                            : h1 == "04 00 04 10 02 02 00 00" && h2 == h1,
                         $"{h1} | {h2}", hw ? "00 06 04 10 02 02 00 00 | 00 06 04 10 00 02 00 00" : "04 00 04 10 02 02 00 00, deux fois", hw);
            var b3 = Paquet(Gesn(0x02, 0, 8)).d;
            r += Voir("sans la classe des supports (classe 1 demandée)", Hex(b3, 0, b3.Length) == (hw ? "00 02 80 10" : "00 00 80 10"),
                      Hex(b3, 0, b3.Length), hw ? "00 02 80 10" : "00 00 80 10", hw);
            var b4 = Paquet(Gesn(0x10, 0, 4)).d;
            r += Voir("la longueur d'allocation 0004h aux octets 7 et 8", b4.Length == (hw ? 4 : 8), $"{b4.Length} octets",
                      hw ? "4 octets" : "8 octets (PCem lit 000400h sur trois octets)", hw);
            return r;
        }, "ucr421");
        if (hw)
            bad += AvecAtapi(_ => "", () =>
            {
                var b = Paquet(Gesn(0x10, 0, 8)).d;
                return Voir("lecteur vide : GET EVENT STATUS NOTIFICATION", Hex(b, 0, b.Length) == "00 06 04 10 00 00 00 00",
                            Hex(b, 0, b.Length), "00 06 04 10 00 00 00 00", hw);
            }, "ucr421");
        return bad;
    }

    // PB-270 — le jeu de commandes d'un lecteur ATAPI selon sa génération (Q5) : SFF-8020i r2.6 (Table 37) et MMC-2 r11a
    // (Table B.2). Sur le Matshita CR-587-B (24x, SFF-8020i) : READ(6), MODE SENSE(6), GET EVENT STATUS NOTIFICATION,
    // PLAY AUDIO(12) et READ DISC INFORMATION — PCem les sert, le matériel rend 05/20h. Sur le Kenwood UCR-421 (MMC-2) :
    // READ(6) rend 20h, GET EVENT STATUS NOTIFICATION et READ DISC INFORMATION sont servis. Témoin : READ(10), GOOD.
    private static int Pb270(bool hw)
    {
        // Ce que PCem rend à chacune, servie : l'ASC (0 : GOOD) ; PLAY AUDIO(12) sur une piste de données, 64h.
        (string nom, byte[] cdb, int servie)[] commandes =
        [
            ("READ(6) du LBA 16", R9Atapi.Cdb(0x08, 0, 0, 16, 1), 0),
            ("MODE SENSE(6) de toutes les pages", R9Atapi.Cdb(0x1A, 0, 0x3F, 0, 0xFF), 0),
            ("GET EVENT STATUS NOTIFICATION", R9Atapi.Cdb(0x4A, 1, 0, 0, 0x10, 0, 0, 0, 8), 0),
            ("PLAY AUDIO(12) du LBA 0, un bloc", R9Atapi.Cdb(0xA5, 0, 0, 0, 0, 0, 0, 0, 0, 1), 0x64),
            ("READ DISC INFORMATION", R9Atapi.Cdb(0x51, 0, 0, 0, 0, 0, 0, 0, 34), 0),
            ("REZERO UNIT", R9Atapi.Cdb(0x01), 0),
            ("SEEK(6) au LBA 16", R9Atapi.Cdb(0x0B, 0, 0, 16), 0),
        ];
        int Essais(string modele, Func<int, bool> refusee) => AvecAtapi(d => Iso(d, 32), () =>
        {
            var bad = 0;
            for (var i = 0; i < commandes.Length; i++)
            {
                var (st, _, _) = Paquet(commandes[i].cdb);
                var asc = (st & 1) != 0 ? Asc() : 0;
                var attendu = hw && refusee(i);
                var servie = commandes[i].servie;
                bad += Voir($"{modele} : {commandes[i].nom}", attendu ? (st & 1) != 0 && asc == 0x20
                                                                   : servie == 0 ? (st & 1) == 0 : (st & 1) != 0 && asc == servie,
                            $"état {st:X2}h, ASC {asc:X2}h", attendu ? "ERR, 20h (INVALID COMMAND OPERATION CODE)"
                                                                     : servie == 0 ? "GOOD (servie)" : $"ERR, {servie:X2}h (servie, comme chez PCem)", hw);
            }
            var (s, _, b) = Paquet(R9Atapi.Read10(16, 1));
            return bad + Voir($"{modele}, témoin : READ(10) du LBA 16", (s & 1) == 0 && b.Length == 2048, $"état {s:X2}h", "GOOD", hw);
        }, modele);
        // GET EVENT STATUS NOTIFICATION, qui traverse l'attention d'unité, envoyé quand elle est en attente (TEST UNIT
        // READY l'a rendue, son sense reste) : refusé par le CR-587-B du matériel (gesn_materiel) ; servi par PCem.
        var ua = AvecAtapi(d => Iso(d, 32), () =>
        {
            var (t, _, _) = Paquet(R9Atapi.Cdb(0x00));     // TEST UNIT READY : l'attention d'unité rendue, son sense gardé
            if ((t & 1) == 0)
                return Voir("TEST UNIT READY après le chargement", false, $"état {t:X2}h", "ERR (UNIT ATTENTION)", hw);
            var (st, _, _) = Paquet(commandes[2].cdb);
            var asc = (st & 1) != 0 ? Asc() : 0;
            return Voir("cr-587-b : GET EVENT STATUS NOTIFICATION sous l'attention d'unité du chargement",
                        hw ? (st & 1) != 0 && asc == 0x20 : (st & 1) == 0, $"état {st:X2}h, ASC {asc:X2}h", hw ? "ERR, 20h" : "GOOD", hw);
        }, "cr-587-b", prepare: false);
        return ua + Essais("cr-587-b", _ => true) + Essais("ucr421", i => i is 0 or 1 or 5 or 6);
    }

    // PB-271 — READ CD MSF (B9h), obligatoire (SFF-8020i r2.6, Table 37 et § 10.8.16 ; MMC-2 r11a, Table B.2 et § 6.1.16).
    // De 00:02:16 à 00:02:17, données utilisateur (octet 9 = 10h) : PCem rend 05/20h ; le matériel le secteur 16 (le PVD).
    // Le départ égal à la fin : GOOD sans données. Le départ au-delà de la fin : 24h (Table 106). Vingt secteurs : deux
    // tranches, la commande traduite relancée par scsi_cd_read. Un départ avant 00:02:00 : 21h ; une image à 75 : 24h.
    private static int Pb271(bool hw) => AvecAtapi(d => Iso(d, 32), () =>
    {
        var bad = 0;
        var (st, _, b) = Paquet(R9Atapi.Cdb(0xB9, 0, 0, 0, 2, 16, 0, 2, 17, 0x10));
        var asc = (st & 1) != 0 ? Asc() : 0;
        bad += Voir("READ CD MSF de 00:02:16 à 00:02:17", hw ? (st & 1) == 0 && b.Length == 2048 && b[0] == 1 && b[1] == (byte)'C'
                                                           : (st & 1) != 0 && asc == 0x20,
                    $"état {st:X2}h, ASC {asc:X2}h, {b.Length} octets", hw ? "GOOD, le PVD (2 048 octets)" : "ERR, 20h", hw);
        if (hw)
        {
            (st, _, b) = Paquet(R9Atapi.Cdb(0xB9, 0, 0, 0, 2, 16, 0, 2, 16, 0x10));
            bad += Voir("le départ égal à la fin", (st & 1) == 0 && b.Length == 0, $"état {st:X2}h, {b.Length} octets", "GOOD, aucun octet", hw);
            (st, _, b) = Paquet(R9Atapi.Cdb(0xB9, 0, 0, 0, 2, 17, 0, 2, 16, 0x10));
            asc = (st & 1) != 0 ? Asc() : 0;
            bad += Voir("le départ au-delà de la fin", (st & 1) != 0 && asc == 0x24, $"état {st:X2}h, ASC {asc:X2}h", "ERR, 24h", hw);
            (st, _, b) = Paquet(R9Atapi.Cdb(0xB9, 0, 0, 0, 2, 0, 0, 2, 20, 0x10));
            bad += Voir("de 00:02:00 à 00:02:20, vingt secteurs (deux tranches de seize, la commande relancée)",
                        (st & 1) == 0 && b.Length == 20 * 2048 && b[16 * 2048] == 1 && b[19 * 2048] == 19,
                        $"état {st:X2}h, {b.Length} octets", "GOOD, 40 960 octets, le PVD à 16 × 2 048, le secteur 19", hw);
            // En brut (F8h) : READ CD de PCem n'y teste pas l'échec de lecture, seul le refus de la traduction rend 21h.
            (st, _, b) = Paquet(R9Atapi.Cdb(0xB9, 0, 0, 0, 1, 0, 0, 2, 1, 0xF8));
            asc = (st & 1) != 0 ? Asc() : 0;
            bad += Voir("un départ avant 00:02:00, en brut", (st & 1) != 0 && asc == 0x21, $"état {st:X2}h, ASC {asc:X2}h", "ERR, 21h", hw);
            (st, _, b) = Paquet(R9Atapi.Cdb(0xB9, 0, 0, 0, 2, 75, 0, 3, 0, 0x10));
            asc = (st & 1) != 0 ? Asc() : 0;
            bad += Voir("une image à 75", (st & 1) != 0 && asc == 0x24, $"état {st:X2}h, ASC {asc:X2}h", "ERR, 24h", hw);
        }
        return bad;
    });
}
