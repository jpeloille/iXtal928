// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — outillage de vérification.
//
// Le banc de l'IBM Fixed Disk Adapter (Xebec) et du Fixed Disk Adapter de l'AT, en C# seul, pour materiel-cas (PB-22,
// PB-23, PB-25, PB-217) et r9-xebec (PB-267). Une machine neuve (l'ibmxt, ou l'ibmat), l'image du disque C: écrite
// dans le TMPDIR, et l'UC arrêtée sur CLI ; HLT en 0000:0500 : le BIOS ne parle jamais à la carte, le temps avance
// par tranches (pc.runpc) et les rappels de la carte s'y jouent. L'outil conduit la carte par ses ports, comme le
// ferait le BIOS de sa ROM (IBM Fixed Disk Adapter, 6361503, Programming Considerations) : l'impulsion de sélection
// (322h), les six octets du DCB, puis les données et l'octet de fin au registre de données (320h), au rythme de
// l'état (321h) ; ni IRQ ni DMA (323h à 0), sauf pour les essais qui les posent.

using iXtal26.Cpu;
using iXtal26.Diag;
using iXtal26.Memory;
using iXtal26.Mfm;

namespace iXtal26.Diff;

internal static class XebecBanc
{
    internal const ushort P = 0x320;
    internal const byte REQ = 0x01, IO = 0x02, CD = 0x04, BSY = 0x08;

    /// <summary>Le type 16 du BIOS de la carte : 612 cylindres, 4 têtes, 17 secteurs.</summary>
    internal const int Cyl = 612, Tetes = 4, Spt = 17;

    internal static xebec_t Carte => (xebec_t)io.port_priv[P, 0]!;

    /// <summary>L'adresse de bloc d'un (cylindre, tête, secteur) du Xebec, secteurs depuis 0.</summary>
    internal static int Lba(int c, int h, int s) => (c * Tetes + h) * Spt + s;

    /// <summary>Une image de C: (le type 16), ses secteurs `marques` remplis de leur octet ; un dossier neuf.</summary>
    internal static string Image(string dossier, string nom, int cyl, int tetes, int spt,
                                 params (int lba, byte octet)[] marques)
    {
        var chemin = Path.Combine(dossier, nom);
        using var f = new FileStream(chemin, FileMode.Create, FileAccess.Write);
        f.SetLength((long)cyl * tetes * spt * 512);
        foreach (var (lba, octet) in marques)
        {
            f.Seek(lba * 512L, SeekOrigin.Begin);
            f.Write(Enumerable.Repeat(octet, 512).ToArray());
        }
        return chemin;
    }

    /// <summary>Monte l'ibmxt et son Xebec sur l'image `c` (et `d` pour l'unité 1), arrête l'UC, puis INITIALIZE DRIVE
    /// CHARACTERISTICS de chaque unité montée, comme la ROM de la carte au démarrage.</summary>
    internal static void Monter(string roms, string dossier, string c, string? d = null)
    {
        var cfg = Path.Combine(dossier, "xebec.cfg");
        File.WriteAllText(cfg, "model = ibmxt\nmem_size = 640\ngfxcard = cga\nhdd_controller = mfm_xebec\n" +
                               $"hdc_cylinders = {Cyl}\nhdc_heads = {Tetes}\nhdc_sectors = {Spt}\nhdc_fn = {c}\n" +
                               (d is null ? "" : $"hdd_cylinders = {Cyl}\nhdd_heads = {Tetes}\nhdd_sectors = {Spt}\nhdd_fn = {d}\n"));
        Demarrer(roms, cfg);
        Echange([0x0C, 0, 0, 0, 0, 0], [Cyl >> 8, Cyl & 0xFF, Tetes, 0, 0, 0, 0, 11]);
        if (d is not null)
            Echange([0x0C, 0x20, 0, 0, 0, 0], [Cyl >> 8, Cyl & 0xFF, Tetes, 0, 0, 0, 0, 11]);
    }

    /// <summary>La machine du fichier de configuration, la disquette `a` en A: (aucune sinon), l'UC arrêtée en
    /// 0000:0500.</summary>
    internal static void Demarrer(string roms, string cfg, string a = "")
    {
        if (!pc.loadconfig(cfg))
            throw new InvalidOperationException("loadconfig");
        Floppy.fdd_c.discfns[0] = a;
        Floppy.fdd_c.discfns[1] = "";
        R9.Raz();
        if (!pc.initpc(roms))
            throw new InvalidOperationException("initpc");
        Arreter();
    }

    /// <summary>CLI ; HLT ; JMP $-1 en 0000:0500, et l'UC y est posée : aucune interruption ne la réveille.</summary>
    internal static void Arreter()
    {
        byte[] code = [0xFA, 0xF4, 0xEB, 0xFD];
        for (var i = 0; i < code.Length; i++)
            mem.mem_writeb_phys(0x500u + (uint)i, code[i]);
        var r = new ushort[(int)R.COUNT];
        r[(int)R.IP] = 0x500;
        r[(int)R.SP] = 0x400;
        r[(int)R.FLAGS] = 0x0002;
        _808x.SetRegs(r);
    }

    /// <summary>L'UC est toujours dans sa boucle d'arrêt (CS = 0, IP entre 0500h et 0503h).</summary>
    internal static bool Arretee()
    {
        var r = new ushort[(int)R.COUNT];
        _808x.GetRegs(r);
        return r[(int)R.CS] == 0 && r[(int)R.IP] is >= 0x500 and <= 0x504;
    }

    internal static byte Etat() => io.inb(P + 1);

    /// <summary>Le DCB : l'impulsion de sélection, puis ses six octets, chacun quand l'état le demande.</summary>
    internal static void Dcb(int[] dcb)
    {
        io.outb(P + 2, 0);
        foreach (var o in dcb)
        {
            if ((Etat() & (BSY | CD | REQ)) != (BSY | CD | REQ))
                throw new InvalidOperationException($"la carte ne demande pas d'octet de commande : état {Etat():X2}h");
            io.outb(P, (byte)o);
        }
    }

    /// <summary>Le reste de la commande, au rythme de l'état : les données à écrire, les données lues, puis l'octet de
    /// fin. Au plus `tranches` tranches sans demande.</summary>
    internal static (byte fin, byte[] lus) Suite(byte[]? ecrire = null, int tranches = 50)
    {
        var lus = new List<byte>();
        var k = 0;
        for (var t = 0; t < tranches;)
        {
            var st = Etat();
            if ((st & REQ) != 0 && (st & CD) != 0 && (st & IO) != 0)
                return (io.inb(P), lus.ToArray());
            if ((st & (REQ | CD | IO)) == (REQ | IO))
            {
                lus.Add(io.inb(P));
                continue;
            }
            if ((st & (REQ | CD | IO)) == REQ && ecrire is not null && k < ecrire.Length)
            {
                io.outb(P, ecrire[k++]);
                continue;
            }
            pc.runpc();
            t++;
        }
        throw new InvalidOperationException($"la commande ne s'achève pas : état {Etat():X2}h");
    }

    /// <summary>Une commande entière : le DCB, les données, l'octet de fin.</summary>
    internal static (byte fin, byte[] lus) Echange(int[] dcb, int[]? ecrire = null)
    {
        Dcb(dcb);
        return Suite(ecrire?.Select(o => (byte)o).ToArray());
    }

    /// <summary>REQUEST SENSE STATUS (03h) de l'unité `d` : les quatre octets.</summary>
    internal static byte[] Sense(int d = 0) => Echange([0x03, d << 5, 0, 0, 0, 0]).lus;

    /// <summary>READ (08h) d'un secteur : l'octet de fin et les données (vides en erreur).</summary>
    internal static (byte fin, byte[] lus) Lire(int c, int h, int s, int d = 0) =>
        Echange([0x08, (d << 5) | h, ((c >> 2) & 0xC0) | s, c & 0xFF, 1, 0x05]);
}
