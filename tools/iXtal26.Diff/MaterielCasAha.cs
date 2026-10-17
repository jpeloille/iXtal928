// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — outillage de vérification.
//
// Les cas qui discriminent les corrections de la configuration de l'AHA-1542C (G13.7e1a, PLAN-G13.md), en C# seul :
// PB-140 (Return Setup Data entier), PB-144 (l'EEPROM du mode matériel, l'usine), PB-218 (le drapeau de Return EEPROM),
// PB-277 (Set EEPROM qui change l'IRQ). Sur le banc de G13.7d2a (AvecScsi) : la carte conduite par ses ports (R9Aha).
// L'EEPROM de la carte est lue et écrite dans un `nvr/` temporaire (AvecNvr), jamais dans celui du dépôt ; la référence
// `nvr/default/aha1542c.nvr` (ID 7, DMA 7, IRQ 10) y est copiée.

using iXtal26.PluginApi;

namespace iXtal26.Diff;

internal static partial class MaterielCas
{
    private const byte DF = 0x04, HACC = 0x04;

    /// <summary>Le corps, dans un répertoire courant neuf qui porte son `nvr/` : initpc y résout les chemins du CMOS
    /// (pc.cs, resolve_roms_path essaie d'abord le répertoire courant) ; les ROM, par leur chemin absolu (RomsBanc), et
    /// les disques restent où ils sont. Les
    /// CMOS du `nvr/` d'origine y sont copiés, sans EEPROM de la carte, et `nvr/default/` entier ; `avant` garnit
    /// `nvr/` ; le répertoire courant rétabli ensuite, et l'EEPROM du `nvr/` d'origine vérifiée intacte.</summary>
    private static int AvecNvr(Func<string, int> corps, Action<string>? avant = null)
    {
        var racine = Path.Combine(Path.GetTempPath(), $"materiel-cas-aha-{Environment.ProcessId}");
        var nvr = Path.Combine(racine, "nvr");
        var origine = Path.GetFullPath(paths.resolve_roms_path("nvr"));
        string Cartes() => string.Join(" ", Directory.GetFiles(origine, ".aha1542c*").Select(f => $"{Path.GetFileName(f)}:{File.GetLastWriteTimeUtc(f).Ticks}"));
        var cartes = Cartes();
        var courant = Directory.GetCurrentDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(nvr, "default"));
            foreach (var f in Directory.GetFiles(origine, ".*.nvr").Where(f => !Path.GetFileName(f).StartsWith(".aha1542c.")))
                File.Copy(f, Path.Combine(nvr, Path.GetFileName(f)));
            foreach (var f in Directory.GetFiles(Path.Combine(origine, "default")))
                File.Copy(f, Path.Combine(nvr, "default", Path.GetFileName(f)));
            avant?.Invoke(nvr);
            RomsBanc = Path.GetFullPath("roms");
            Directory.SetCurrentDirectory(racine);
            return corps(nvr);
        }
        finally
        {
            Directory.SetCurrentDirectory(courant);
            RomsBanc = "roms";
            if (Directory.Exists(racine))
                Directory.Delete(racine, true);
            // Le nvr/ d'origine n'a pas été touché : aucune EEPROM de la carte n'y est apparue ni n'y a été réécrite.
            if (Cartes() != cartes)
                throw new InvalidOperationException($"le nvr/ d'origine a changé : {origine}");
        }
    }

    /// <summary>Une commande de la carte et ses octets rendus, jusqu'à HACC ; IRST ensuite.</summary>
    private static byte[] Commande(params int[] octets)
    {
        R9Aha.Cmd(octets);
        var rendu = new List<byte>();
        for (var i = 0; i < 2000; i++)
        {
            if ((R9Aha.Etat() & DF) != 0)
                rendu.Add(iXtal26.io.inb(R9Aha.P + 1));
            else if ((R9Aha.Isr() & HACC) != 0)
                break;
            else
                pc.runpc();
        }
        iXtal26.io.outb(R9Aha.P, 0x20);
        return rendu.ToArray();
    }

    /// <summary>Set EEPROM (22h) : `donnees` à partir de `decalage`, les 35 octets de paramètres complétés de zéros.</summary>
    private static int[] SetEeprom(int decalage, params int[] donnees)
    {
        var p = new int[36];
        p[0] = 0x22;
        p[2] = donnees.Length;
        p[3] = decalage;
        donnees.CopyTo(p, 4);
        return p;
    }

    private static string Hex(byte[] b) => Convert.ToHexString(b);

    // L'EEPROM de référence de PCem (nvr/default/aha1542c.nvr) et celle d'usine du mode matériel : 32 octets.
    private const string Reference = "0771B600000000000000000000000800" + "00000000000000000000000000000000";
    private const string Usine = "0752B600000000000000000000000800" + "00000000000000000000000000000000";

    // PB-144 — sans EEPROM écrite, les réglages d'usine du TR (chapitre 3 : ID 7, DMA 5, IRQ 11) ; PCem lit sa
    // référence (ID 7, DMA 7, IRQ 10). Et un fichier par mode (Q8) : PCem lit et écrit `nvr/.aha1542c.nvr`, le mode
    // matériel `nvr/.aha1542c.materiel.nvr`. Return Configuration Data (0Bh) d'une carte neuve : PCem 80h (DMA 7) 02h
    // (IRQ 10) 07h ; le matériel 20h (DMA 5) 04h (IRQ 11) 07h ; Return EEPROM (23h, drapeau 1) des
    // 32 octets : la référence, contre l'usine. Une EEPROM écrite (22h, l'ID 6) : son fichier, celui du mode, et une
    // machine neuve la relit ; le fichier de l'autre mode, garni d'un ID 5, n'est pas lu.
    private static int Pb144(bool hw)
    {
        var bad = AvecNvr(_ => AvecScsi(() =>
        {
            var b = 0;
            var cfg = Hex(Commande(0x0B));
            b += Voir("carte neuve, 0Bh", cfg == (hw ? "200407" : "800207"), cfg, hw ? "200407" : "800207", hw);
            var e = Hex(Commande(0x23, 1, 32, 0));
            b += Voir("carte neuve, 23h (drapeau 1, 32 octets)", e == (hw ? Usine : Reference), e, hw ? Usine : Reference, hw);
            return b;
        }));
        bad += AvecNvr(nvr =>
        {
            var b = AvecScsi(() =>
            {
                Commande(SetEeprom(0, 0x06));
                return 0;
            });
            var propre = hw ? ".aha1542c.materiel.nvr" : ".aha1542c.nvr";
            var autre = hw ? ".aha1542c.nvr" : ".aha1542c.materiel.nvr";
            var fichiers = $"{(File.Exists(Path.Combine(nvr, propre)) ? propre : "-")} {(File.Exists(Path.Combine(nvr, autre)) ? autre : "-")}";
            b += Voir("22h (l'ID 6) : les fichiers écrits", fichiers == $"{propre} -", fichiers, $"{propre} -", hw);
            b += AvecScsi(() =>
            {
                var cfg = Hex(Commande(0x0B));
                var att = hw ? "200406" : "800206";
                return Voir("machine neuve après 22h, 0Bh", cfg == att, cfg, att, hw);
            });
            return b;
        });
        bad += AvecNvr(_ => AvecScsi(() =>
        {
            var cfg = Hex(Commande(0x0B));
            var att = hw ? "200407" : "800207";
            return Voir("l'EEPROM de l'autre mode (ID 5) ignorée, 0Bh", cfg == att, cfg, att, hw);
        }), nvr =>
        {
            var e = Convert.FromHexString(Reference);
            e[0] = 0x05;
            File.WriteAllBytes(Path.Combine(nvr, hw ? ".aha1542c.nvr" : ".aha1542c.materiel.nvr"), e);
        });
        return bad;
    }

    // PB-218 — Return EEPROM (23h), octet 0 : « 1 = Return configured options, 0 = Return default options » (TR,
    // chapitre 5). Après un 22h qui pose l'octet 1 à 61h (DMA 6, IRQ 10) : le drapeau 1 rend 07 61 des deux côtés ; le
    // drapeau 0, 07 61 chez PCem, 07 52 (l'usine) sur le matériel ; 32 octets au drapeau 0, la configuration contre
    // l'usine ; un décalage de 1, deux octets : 61 B6 contre 52 B6.
    private static int Pb218(bool hw) => AvecNvr(_ => AvecScsi(() =>
    {
        var bad = 0;
        Commande(SetEeprom(1, 0x61));
        var conf = Reference[..2] + "61" + Reference[4..];
        var (un, zero, tout, decale) = (Hex(Commande(0x23, 1, 2, 0)), Hex(Commande(0x23, 0, 2, 0)), Hex(Commande(0x23, 0, 32, 0)),
                                        Hex(Commande(0x23, 0, 2, 1)));
        bad += Voir("23h, drapeau 1, 2 octets", un == "0761", un, "0761", hw);
        bad += Voir("23h, drapeau 0, 2 octets", zero == (hw ? "0752" : "0761"), zero, hw ? "0752" : "0761", hw);
        bad += Voir("23h, drapeau 0, 32 octets", tout == (hw ? Usine : conf), tout, hw ? Usine : conf, hw);
        bad += Voir("23h, drapeau 0, décalage 1, 2 octets", decale == (hw ? "52B6" : "61B6"), decale, hw ? "52B6" : "61B6", hw);
        return bad;
    }));

    // PB-140 — Return Setup Data (0Dh) : les octets 00h à 2Bh, 2Ch-FFh à zéro ; « A value of zero is accepted and 256
    // bytes are returned » (TR, chapitre 5). 44 octets : PCem en rend 20 puis 24 zéros ; le matériel 26h (les
    // interrupteurs : la carte en 334h, SW2 sur On ; SW5 sur Off, l'usine, déduit (G13.8d3c) ; la ROM en DC000h,
    // SW6-SW8 sur Off : 01h), A3h C2h en 27h-28h, la mailbox du BIOS (25h, posée à 012345h) en 29h-2Bh. 50 octets :
    // 2Ch-31h à zéro des deux côtés. 0 : aucun octet chez PCem, 256 sur la carte, nuls au-delà de 2Bh — le tampon des
    // résultats sali d'abord (22h range AAh BBh en 50, hors des 32 octets de l'EEPROM, et 23h de 60 octets les y rend).
    // La ROM en D8000h (SW6 sur On, bit 4 seul — l'ordre des bits : SW6 et SW8 inversés donneraient le bit 6) : 26h =
    // 11h ; en C8000h (SW6 et SW8 sur On) : 51h. SW5 sur On (floppy = 0, G13.8d3c) : 09h. Les 20 premiers octets,
    // communs, sont comparés aussi.
    private static int Pb140(bool hw)
    {
        var bad = AvecNvr(_ => AvecScsi(() =>
        {
            var b = 0;
            Commande(0x25, 1, 0x01, 0x23, 0x45);
            var r = Commande(0x0D, 44);
            var tete = Hex(r.Take(20).ToArray());
            b += Voir("0Dh de 44 : la longueur rendue", r.Length == 44, $"{r.Length}", "44", hw);
            b += Voir("0Dh de 44 : les octets 00h-13h", tete == "0000000000000000000000000000000000000000", tete, "20 zéros (rien d'initialisé)", hw);
            var queue = r.Length == 44 ? Hex(r[37..44]) : "?";
            var att = hw ? "0001A3C2012345" : "00000000000000";
            b += Voir("0Dh de 44 : les octets 25h-2Bh", queue == att, queue, att, hw);
            var r50 = Commande(0x0D, 50);
            var fin = r50.Length == 50 ? Hex(r50[44..50]) : $"{r50.Length} octets";
            b += Voir("0Dh de 50 : les octets 2Ch-31h", fin == "000000000000", fin, "000000000000", hw);
            Commande(SetEeprom(50, 0xAA, 0xBB));
            Commande(0x23, 1, 60, 0);
            var r0 = Commande(0x0D, 0);
            var n0 = r0.Length;
            b += Voir("0Dh de 0 : la longueur rendue", n0 == (hw ? 256 : 0), $"{n0}", hw ? "256" : "0", hw);
            if (hw && n0 == 256)
            {
                var nuls = r0.Skip(44).All(x => x == 0);
                b += Voir("0Dh de 0 : 26h-28h, puis le reste nul", Hex(r0[38..41]) == "01A3C2" && nuls,
                          Hex(r0[38..41]) + (nuls ? ", nuls" : ", non nuls"), "01A3C2, nuls", hw);
            }
            return b;
        }));
        foreach (var (cle, nom, octet) in new[]
                 {
                     ("bios_addr = 884736", "ROM en D8000h", "11"), ("bios_addr = 819200", "ROM en C8000h", "51"),
                     ("floppy = 0", "SW5 sur On (floppy = 0)", "09"),
                 })
            bad += AvecNvr(_ => AvecScsi(() =>
            {
                var r = Commande(0x0D, 44);
                var v = r.Length == 44 ? $"{r[38]:X2}" : $"{r.Length} octets";
                var att = hw ? octet : "00";
                return Voir($"{nom}, 0Dh de 44 : l'octet 26h", v == att, v, att, hw);
            }, section: $"\n[Adaptec AHA-1542C (SCSI)]\naddr = 820\n{cle}\n"));
        return bad;
    }

    // PB-277 — Set EEPROM (22h) qui change l'IRQ (de 10 à 11, l'octet 1 à 72h), une interruption de la carte en
    // attente (une commande 1Fh, ECHO, achevée sans IRST) : chez PCem l'IRQ 10 reste levée au PIC esclave ; sur la
    // carte, l'interruption passe sur la nouvelle ligne, l'ancienne retombe (déduit). L'IRQ 11 levée des deux côtés.
    // IRST ensuite baisse l'IRQ 11, la ligne de la carte, des deux côtés. Témoin : 22h qui ne change pas l'IRQ, l'IRQ 10
    // levée des deux côtés. L'EEPROM de départ, la référence (l'IRQ 10), posée dans le fichier de chaque mode : le cas
    // tient aussi sous PB-144 (« tout »). Les lignes 10 et 11 masquées au PIC
    // pendant le cas, pour que le BIOS ne les acquitte pas.
    private static int Pb277(bool hw)
    {
        int Essai(int octet1, string nom, bool dix, bool onze) => AvecNvr(_ => AvecScsi(() =>
        {
            iXtal26.io.outb(0xA1, (byte)(iXtal26.io.inb(0xA1) | 0x0C));
            R9Aha.Cmd(0x1F, 0x5A);
            for (var i = 0; i < 100 && (R9Aha.Etat() & DF) == 0; i++)
                pc.runpc();
            iXtal26.io.inb(R9Aha.P + 1);
            for (var i = 0; i < 100 && (R9Aha.Isr() & HACC) == 0; i++)
                pc.runpc();
            var avant = (Models.pic.pic2.pend & 0x04) != 0;
            R9Aha.Cmd(SetEeprom(1, octet1));
            for (var i = 0; i < 100 && (R9Aha.Etat() & 0x10) == 0; i++)
                pc.runpc();
            var (i10, i11) = ((Models.pic.pic2.pend & 0x04) != 0, (Models.pic.pic2.pend & 0x08) != 0);
            iXtal26.io.outb(R9Aha.P, 0x20);
            var irst = (Models.pic.pic2.pend & 0x08) != 0;
            var vu = $"IRQ 10 {(avant ? "levée" : "basse")} avant, puis {(i10 ? "levée" : "basse")}, IRQ 11 {(i11 ? "levée" : "basse")}, " +
                     $"puis {(irst ? "levée" : "basse")} après IRST";
            var att = $"IRQ 10 levée avant, puis {(dix ? "levée" : "basse")}, IRQ 11 {(onze ? "levée" : "basse")}, puis basse après IRST";
            return Voir(nom, vu == att, vu, att, hw);
        }), nvr =>
        {
            foreach (var f in new[] { ".aha1542c.nvr", ".aha1542c.materiel.nvr" })
                File.WriteAllBytes(Path.Combine(nvr, f), Convert.FromHexString(Reference));
        });
        return Essai(0x72, "22h, l'IRQ de 10 à 11", !hw, true) + Essai(0x71, "22h, l'IRQ inchangée (témoin)", true, false);
    }

    // PB-292 — l'AHA-1542C sort d'usine en 330h, SW2-SW4 sur Off (TR, chapitre 3) ; PCem, sans clé, la monte en 334h.
    // Sans clé, puis addr = 999 (hors de la liste) : où le registre d'état répond (330h, 334h ; FFh, rien), et
    // l'interrupteur relu par la sonde ; l'avertissement de pc.check_sndcard, une SB 16 au MPU en 330h et l'AHA sans
    // clé (rien chez PCem, averti sur la carte). Témoin : addr = 230h.
    private static int Pb292(bool hw)
    {
        int Un(string nom, string cle, string pcem, string mat, bool temoin = false) => AvecNvr(_ => AvecScsi(() =>
        {
            var o = new ulong[Scsi.scsi_aha1540.PROBE_N];
            Scsi.scsi_aha1540.Probe(o);
            var rep = new[] { 0x230, 0x330, 0x334 }.Where(p => iXtal26.io.inb((ushort)p) != 0xFF)
                                                   .Select(p => $"{p:X}h");
            var v = $"répond en {string.Join(" ", rep)}, interrupteurs {o[42]}";
            var att = temoin || hw ? mat : pcem;
            return Voir(nom, v == att, v, att, hw);
        }, section: $"\n[Adaptec AHA-1542C (SCSI)]\n{cle}"));
        var bad = Un("sans clé", "", "répond en 334h, interrupteurs 1", "répond en 330h, interrupteurs 0");
        bad += Un("addr = 999, hors de la liste", "addr = 999\n", "répond en 334h, interrupteurs 1",
                  "répond en 330h, interrupteurs 0");
        bad += Un("addr = 230h (témoin)", "addr = 560\n", "", "répond en 230h, interrupteurs 2", temoin: true);
        var v = AvertAha("", "");
        var att = hw ? "averti" : "rien";
        bad += Voir("l'AHA sans clé, la SB 16 au MPU en 330h : l'avertissement", v == att, v, att, hw);
        return bad;
    }
}
