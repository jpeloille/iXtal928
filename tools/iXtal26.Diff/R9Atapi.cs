// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// r9-atapi [DOSSIER] — les sites où PCem s'arrête dans le pont ATAPI, le bus SCSI et le lecteur de CD-ROM
// (G10.4), en C# SEUL.
//
// PB-113 : les dix-sept fatal() du pont (ide_atapi.c) et les deux du bus (scsi.c). PB-114 : le DMA sans bus
// master (ide_atapi.c:473, :482). PB-115 : MECHANISM STATUS de longueur 0 (scsi_cd.c:993), data_out débordé
// (:1581), la lecture au-delà de data_in (:1565), le compte négatif de READ(12) (:1187). Chaque site est nommé,
// « fichier.c:ligne », et atteint :
//   - par un scénario de l'invité : les ports de l'unité IDE 2 (170h-177h) conduits depuis l'outil, sur un
//     ami486 amorcé, le lecteur chargé de iso-2048.iso d'isogen — le BIOS ne touche pas ce canal (mesuré,
//     VERIFICATION.md § G10.4), et nIEN coupe les IRQ du lecteur ;
//   - par un état forgé, pour les sites que l'invité n'atteint pas (la machine d'états du pont et le bus,
//     VERIFICATION.md § G10.4) : l'état du pont et du bus posé à la main, ou la lecture du bus remplacée
//     (ide_atapi.bus_lu_force) quand scsi.c ne produit pas de lui-même la suite voulue.
// Chaque garde se signale à Diag.R9 : la porte exige que le site visé ait été atteint et qu'aucune exception ne
// sorte ; une garde retirée (le fatal() de PCem rendu) la rougit, l'exception nommée. L'oracle n'est jamais
// appelé : il tomberait.

using iXtal26.Diag;
using iXtal26.Ide;
using iXtal26.Scsi;

namespace iXtal26.Diff;

internal static class R9Atapi
{
    private const ushort Base = 0x170;
    private const ushort Ctl = 0x376;

    internal static int Run(string romsPath, string? dossier)
    {
        string? genere = null;
        var cfg = Path.Combine(Path.GetTempPath(), $"r9-atapi-{Environment.ProcessId}.cfg");
        var bad = 0;
        try
        {
            if (dossier is null)
                dossier = genere = IsoGen.Generer();
            File.WriteAllText(cfg, "model = ami486\ncpu = 10\nmem_size = 4096\ngfxcard = tvga9000b\nhdd_controller = ide\n" +
                                   $"cdrom_channel = 2\ncdrom_drive = 200\ncdrom_path = {Path.Combine(dossier, "iso-2048.iso")}\n");
            Machine = () => Amorce(romsPath, cfg);

            // --- les scénarios de l'invité, chacun sur une machine neuve ----------------------------------
            bad += Invite("ide_atapi.c:110", "DEVICE RESET (08h) en pleine lecture, puis un PACKET", () =>
            {
                Prepare();
                if (!Paquet(Read10(16, 4), 0x0800) || Bloc() != 2048)
                    return "le premier bloc de READ(10) devait venir";
                Out(7, 0x08);
                Tranches(2);
                return Commande(Cdb(0x00), out var f) && (f.st & 1) == 0 ? null : $"TEST UNIT READY après la reprise : {Dit(f)}";
            });
            bad += Invite("ide_atapi.c:110", "un PACKET pendant la phase de données", () =>
            {
                Prepare();
                if (!Paquet(Read10(16, 4), 0x0800) || Bloc() != 2048)
                    return "le premier bloc de READ(10) devait venir";
                return Commande(Cdb(0x00), out var f) && (f.st & 1) == 0 ? null : $"TEST UNIT READY après la reprise : {Dit(f)}";
            });
            bad += Invite("ide_atapi.c:473", "READ(10) avec le bit DMA, sans bus master", () =>
            {
                Prepare();
                if (!Paquet(Read10(16, 1), 0xFFFE, 1))
                    return "le paquet devait partir";
                Tranches(5);
                if ((St() & 0x80) == 0)
                    return "le lecteur devait rester BSY (l'invité attend)";
                return Reprise();
            });
            bad += Invite("ide_atapi.c:482", "MODE SELECT(10) avec le bit DMA, sans bus master", () =>
            {
                Prepare();
                if (!Paquet(Cdb(0x55, 0x10, 0, 0, 0, 0, 0, 0, 8), 0xFFFE, 1))
                    return "le paquet devait partir";
                Tranches(5);
                if ((St() & 0x80) == 0)
                    return "le lecteur devait rester BSY (l'invité attend)";
                return Reprise();
            });
            bad += Invite("scsi_cd.c:993", "MECHANISM STATUS de longueur 0", () =>
            {
                Prepare();
                if (!Commande(Cdb(0xBD), out var f))
                    return "la commande devait finir";
                return (f.st & 1) == 0 && f.lus == 0 ? null : $"ATTENDU GOOD sans données : {Dit(f)}";
            });
            bad += Invite("scsi_cd.c:1581", "MODE SELECT(10) de longueur 0, puis des écritures au-delà de 256 Ko (position forgée)", () =>
            {
                Prepare();
                if (!Paquet(Cdb(0x55, 0x10)))
                    return "le paquet devait partir";
                for (var i = 0; i < 3; i++)
                {
                    if (!AttendreBsy())
                        return "DRQ attendu (la phase sans fin, PB-119)";
                    if (i == 0)
                        scsi_cd_c.cd_data!.data_pos_write = scsi_cd_c.BUFFER_SIZE - 2;
                    io.outw(Base, 0x5AA5);
                    Tranches(1);
                }
                return Reprise();
            });
            bad += Invite("scsi_cd.c:1565", "READ(10) de 200 secteurs depuis le LBA 0 : le troisième remplissage échoue", () =>
            {
                Prepare();
                if (!Paquet(Read10(0, 200)))
                    return "le paquet devait partir";
                var lus = Donnees(100);
                var f = Fin();
                // 2 × 32 768 octets lus, puis data_in de 32 768 à sa fin, puis l'octet nul de la garde.
                const int attendu = 65536 + (scsi_cd_c.BUFFER_SIZE - 32768) + 1;
                if (lus != attendu || (f.st & 1) == 0 || f.err != 0x54)
                    return $"ATTENDU {attendu} octets puis CHECK CONDITION, ILLEGAL REQUEST (erreur 54h) : {lus} octets, {Dit(f)}";
                return null;
            });
            bad += Invite("scsi_cd.c:1187", "READ(12) d'un compte négatif (≥ 2^31 secteurs)", () =>
            {
                Prepare();
                // Le compte est choisi pour que l'attente de la lecture soit courte : (12 000 × TIMER_USEC / vitesse) ×
                // compte, sur 64 bits, retombe sous une milliseconde (scsi_cd.c:1170-1180).
                var k = 12000UL * timer.TIMER_USEC / (ulong)scsi_cd_c.cd_data!.cur_speed;
                var n = unchecked((uint)-(int)(ulong.MaxValue / k));
                if (!Commande(Cdb(0xA8, 0, 0, 0, 0, 0, (byte)(n >> 24), (byte)(n >> 16), (byte)(n >> 8), (byte)n), out var f))
                    return "la commande devait finir";
                return (f.st & 1) != 0 && f.err == 0x54 ? null : $"ATTENDU CHECK CONDITION, ILLEGAL REQUEST : {Dit(f)}";
            });

            // --- les états forgés, sur une machine amorcée ---------------------------------------------------
            Amorce(romsPath, cfg);
            var a = Ide.ide.ide_drives[2].atapi;
            bad += Forge("ide_atapi.c:102", "la sélection : BSY tombe à la première lecture", a, () =>
            {
                MessageVersRepos(a.bus, 1);
                ide_atapi.atapi_command_start(a, 0);
                return a.state == ide_atapi.ATAPI_STATE_COMMAND ? null : "la sélection devait reprendre";
            }, abandon: false);
            bad += Forge("ide_atapi.c:106", "la sélection : BSY tombe à la seconde lecture", a, () =>
            {
                MessageVersRepos(a.bus, 2);
                ide_atapi.atapi_command_start(a, 0);
                return a.state == ide_atapi.ATAPI_STATE_COMMAND ? null : "la sélection devait reprendre";
            }, abandon: false);
            bad += Forge("ide_atapi.c:95", "la sélection échoue deux fois (le périphérique ôté)", a, () =>
            {
                // Le bus sans périphérique, comme scsi_bus_atapi_init le laisse quand atapi_init échoue (scsi.c:298-299).
                var dev = a.bus.devices[0];
                var donnees = a.bus.device_data[0];
                a.bus.devices[0] = null;
                a.bus.device_data[0] = null;
                try
                {
                    ide_atapi.atapi_command_start(a, 0);
                }
                finally
                {
                    a.bus.devices[0] = dev;
                    a.bus.device_data[0] = donnees;
                }
                return R9.Compte("ide_atapi.c:102") == 2 ? null : "deux échecs en :102 attendus";
            });
            bad += Pont("ide_atapi.c:157", ide_atapi.ATAPI_STATE_GOT_COMMAND, a, b => { });
            bad += Pont("ide_atapi.c:159", ide_atapi.ATAPI_STATE_GOT_COMMAND, a, b => Bus(b, scsi.STATE_DATAIN, scsi.BUS_BSY | scsi.BUS_IO));
            bad += Pont("ide_atapi.c:164", ide_atapi.ATAPI_STATE_GOT_COMMAND, a, b => Bus(b, scsi.STATE_COMMANDWAIT, scsi.BUS_BSY | scsi.BUS_CD));
            bad += Pont("ide_atapi.c:173", ide_atapi.ATAPI_STATE_GOT_COMMAND, a, b =>
            {
                Bus(b, scsi.STATE_COMMAND, scsi.BUS_BSY | scsi.BUS_CD | scsi.BUS_REQ);
                b.change_state_delay = 2;
                b.new_state = scsi.BUS_IDLE;
            });
            bad += Pont("ide_atapi.c:205", ide_atapi.ATAPI_STATE_GOT_COMMAND, a, b =>
            {
                var suite = new[] { scsi.BUS_BSY | scsi.BUS_CD | scsi.BUS_REQ, scsi.BUS_BSY | scsi.BUS_CD | scsi.BUS_REQ, scsi.BUS_CD | scsi.BUS_REQ };
                var i = 0;
                ide_atapi.bus_lu_force = _ => suite[Math.Min(i++, suite.Length - 1)];
            });
            bad += Pont("ide_atapi.c:221", ide_atapi.ATAPI_STATE_NEXT_PHASE, a, b => { });
            bad += Pont("ide_atapi.c:281", ide_atapi.ATAPI_STATE_NEXT_PHASE, a, b => Bus(b, scsi.STATE_MESSAGEIN, scsi.BUS_BSY | scsi.BUS_REQ | scsi.BUS_MSG));
            bad += Pont("ide_atapi.c:296", ide_atapi.ATAPI_STATE_READ_STATUS, a, b => { });
            bad += Pont("ide_atapi.c:299", ide_atapi.ATAPI_STATE_READ_STATUS, a, b => Bus(b, scsi.STATE_DATAIN, scsi.BUS_BSY | scsi.BUS_IO));
            bad += Pont("ide_atapi.c:324", ide_atapi.ATAPI_STATE_READ_MESSAGE, a, b => { });
            bad += Pont("ide_atapi.c:346", ide_atapi.ATAPI_STATE_READ_MESSAGE, a, b =>
                Bus(b, scsi.STATE_MESSAGEIN, scsi.BUS_BSY | scsi.BUS_REQ | scsi.BUS_CD | scsi.BUS_IO | scsi.BUS_MSG | scsi.BUS_SETDATA(0x55)));
            bad += Pont("ide_atapi.c:362", ide_atapi.ATAPI_STATE_READ_DATA, a, b => a.max_transfer_len = 2);
            bad += Pont("ide_atapi.c:416", ide_atapi.ATAPI_STATE_WRITE_DATA, a, b => a.data_write_pos = 2);
            bad += Pont("ide_atapi.c:453", ide_atapi.ATAPI_STATE_END_PHASE, a, b => Bus(b, scsi.STATE_STATUS, scsi.BUS_BSY | scsi.BUS_REQ));
            bad += Forge("scsi.c:85", "le bus en sélection reçoit ATN", a, () =>
            {
                Bus(a.bus, scsi.STATE_PHASESEL, scsi.BUS_BSY);
                a.bus.dev_id = 0;
                scsi.scsi_bus_update(a.bus, scsi.BUS_ATN);
                return a.bus.state == scsi.STATE_IDLE && a.bus.bus_out == 0 ? null : "le bus devait revenir au repos";
            }, abandon: false);
            bad += Forge("scsi.c:264", "le bus change vers une phase qu'il ne connaît pas (C/D seul)", a, () =>
            {
                Bus(a.bus, scsi.STATE_COMMANDWAIT, scsi.BUS_BSY);
                a.bus.dev_id = 0;
                a.bus.change_state_delay = 1;
                a.bus.new_state = scsi.BUS_CD;
                scsi.scsi_bus_read(a.bus);
                return a.bus.state == scsi.STATE_IDLE && a.bus.bus_out == 0 ? null : "le bus devait revenir au repos";
            }, abandon: false);
        }
        catch (Exception e)
        {
            Console.WriteLine($"r9-atapi : ARRÊT hors essai — {e.GetType().Name} : {e.Message.Trim()}");
            bad++;
        }
        finally
        {
            ide_atapi.bus_lu_force = null;
            File.Delete(cfg);
            if (genere is not null)
                IsoGen.Effacer(genere);
        }

        Console.WriteLine(bad == 0
            ? $"r9-atapi : {Essais} essais — vingt-cinq sites de PCem et le second échec de la sélection —, chaque garde atteinte, tout survit."
            : $"r9-atapi : {bad} essai(s) en défaut sur {Essais}.");
        return bad == 0 ? 0 : 1;
    }

    // --- un essai ----------------------------------------------------------------------------------------

    private static Action Machine = () => { };
    private static int Essais;

    /// <summary>L'ami486 amorcé, le lecteur sur l'unité IDE 2, puis cent tranches de POST et nIEN : pas d'IRQ
    /// du lecteur.</summary>
    private static void Amorce(string romsPath, string cfg)
    {
        if (!pc.loadconfig(cfg))
            throw new InvalidOperationException("loadconfig");
        Floppy.fdd_c.discfns[0] = Floppy.fdd_c.discfns[1] = "";
        for (var d = 0; d < 4; d++)
            Disc.hdd_c.ide_fn[d] = "";
        if (!pc.initpc(romsPath))
            throw new InvalidOperationException("initpc");
        Tranches(100);
        io.outb(Ctl, 0x02);
    }

    /// <summary>Un scénario de l'invité, sur une machine neuve : un essai qui s'arrête n'en laisse pas une
    /// cassée au suivant.</summary>
    private static int Invite(string site, string nom, Func<string?> corps) =>
        Essai(site, nom, () =>
        {
            Machine();
            return corps();
        });

    /// <summary>Un scénario de l'invité : `corps` rend null s'il est conforme ; le site doit avoir été atteint.</summary>
    private static int Essai(string site, string nom, Func<string?> corps)
    {
        Essais++;
        R9.Raz();
        try
        {
            var faute = corps();
            if (faute is null && R9.Compte(site) == 0)
                faute = "la garde n'a pas été atteinte — l'essai ne prouve rien";
            Console.WriteLine($"  {site} — {nom} : {(faute is null ? $"survit (garde atteinte {R9.Compte(site)} fois)" : $"EN DÉFAUT — {faute}")}");
            return faute is null ? 0 : 1;
        }
        catch (Exception e)
        {
            Console.WriteLine($"  {site} — {nom} : ARRÊT — {e.GetType().Name} : {e.Message.Trim()}");
            return 1;
        }
        finally
        {
            ide_atapi.bus_lu_force = null;
        }
    }

    /// <summary>Un état forgé : le pont remis à zéro, puis `corps`. Avec `abandon`, la commande doit finir en
    /// erreur comme atapi_abort la finit (ERR, ABRT, phase d'état, le pont au repos).</summary>
    private static int Forge(string site, string nom, atapi_device_t a, Func<string?> corps, bool abandon = true)
    {
        return Essai(site, $"{nom} (état forgé)", () =>
        {
            ide_atapi.atapi_reset(a);
            timer.timer_disable(Ide.ide.ide_timer[1]);
            a.ide!.atastat = Ide.ide.READY_STAT;
            a.ide.error = 0;
            var faute = corps();
            timer.timer_disable(Ide.ide.ide_timer[1]);
            if (faute is not null || !abandon)
                return faute;
            return a.state == ide_atapi.ATAPI_STATE_IDLE && (a.ide.atastat & Ide.ide.ERR_STAT) != 0 &&
                   a.ide.error == Ide.ide.ABRT_ERR && ide_atapi.atapi_read_iir(a) == 3
                ? null
                : $"ATTENDU la commande abandonnée (état {a.state}, statut {a.ide.atastat:X2}, erreur {a.ide.error:X2})";
        });
    }

    /// <summary>Un site de la machine d'états : le pont dans `etat`, le bus préparé par `bus`, un tour d'atapi_process_packet.</summary>
    private static int Pont(string site, int etat, atapi_device_t a, Action<scsi_bus_t> bus)
    {
        return Forge(site, $"le pont en {Nom(etat)}", a, () =>
        {
            a.state = etat;
            bus(a.bus);
            ide_atapi.atapi_process_packet(a);
            return null;
        });
    }

    private static string Nom(int etat) => etat switch
    {
        ide_atapi.ATAPI_STATE_GOT_COMMAND => "GOT_COMMAND",
        ide_atapi.ATAPI_STATE_NEXT_PHASE => "NEXT_PHASE",
        ide_atapi.ATAPI_STATE_READ_STATUS => "READ_STATUS",
        ide_atapi.ATAPI_STATE_READ_MESSAGE => "READ_MESSAGE",
        ide_atapi.ATAPI_STATE_READ_DATA => "READ_DATA",
        ide_atapi.ATAPI_STATE_WRITE_DATA => "WRITE_DATA",
        ide_atapi.ATAPI_STATE_END_PHASE => "END_PHASE",
        _ => etat.ToString(),
    };

    private static void Bus(scsi_bus_t b, int etat, int sortie)
    {
        b.state = etat;
        b.bus_out = (uint)sortie;
        b.dev_id = 0;
    }

    /// <summary>Le bus en fin de message, le passage au repos (BSY qui tombe) dans `n` lectures.</summary>
    private static void MessageVersRepos(scsi_bus_t b, int n)
    {
        Bus(b, scsi.STATE_MESSAGEIN, scsi.BUS_BSY | scsi.BUS_CD | scsi.BUS_IO | scsi.BUS_MSG);
        b.change_state_delay = n;
        b.new_state = scsi.BUS_IDLE;
    }

    // --- l'invité, par les ports ---------------------------------------------------------------------------

    internal static void Tranches(int n)
    {
        for (var i = 0; i < n; i++)
            pc.runpc();
    }

    internal static byte St() => io.inb((ushort)(Base + 7));

    internal static void Out(int reg, int v) => io.outb((ushort)(Base + reg), (byte)v);

    internal static bool AttendreBsy(int max = 100)
    {
        for (var i = 0; i < max; i++)
        {
            if ((St() & 0x80) == 0)
                return true;
            pc.runpc();
        }
        return false;
    }

    internal static byte[] Cdb(params int[] b)
    {
        var c = new byte[12];
        for (var i = 0; i < b.Length; i++)
            c[i] = (byte)b[i];
        return c;
    }

    internal static byte[] Read10(int lba, int n) =>
        Cdb(0x28, 0, (lba >> 24) & 0xFF, (lba >> 16) & 0xFF, (lba >> 8) & 0xFF, lba & 0xFF, 0, n >> 8, n & 0xFF);

    /// <summary>A0h, puis le paquet dès que le lecteur lève DRQ.</summary>
    internal static bool Paquet(byte[] cdb, int limite = 0xFFFE, int features = 0)
    {
        Out(6, 0xA0);
        Out(1, features);
        Out(4, limite & 0xFF);
        Out(5, limite >> 8);
        Out(7, 0xA0);
        for (var i = 0; i < 100; i++)
        {
            var s = St();
            if ((s & 0x80) == 0 && (s & 0x08) != 0)
            {
                for (var j = 0; j < 12; j += 2)
                    io.outw(Base, (ushort)(cdb[j] | (cdb[j + 1] << 8)));
                return true;
            }
            pc.runpc();
        }
        return false;
    }

    /// <summary>Un bloc DRQ : le compte d'octets de 174h-175h, lu en mots ; -1 sans DRQ.</summary>
    internal static int Bloc()
    {
        if (!AttendreBsy() || (St() & 0x08) == 0)
            return -1;
        var n = io.inb((ushort)(Base + 4)) | (io.inb((ushort)(Base + 5)) << 8);
        for (var i = 0; i < (n + 1) / 2; i++)
            io.inw(Base);
        return n;
    }

    internal static int Donnees(int max)
    {
        var total = 0;
        for (var b = 0; b < max; b++)
        {
            var n = Bloc();
            if (n <= 0)
                break;
            total += n;
        }
        return total;
    }

    internal static (byte st, byte err, byte iir, int lus) Fin(int lus = 0)
    {
        AttendreBsy();
        return (St(), io.inb((ushort)(Base + 1)), io.inb((ushort)(Base + 2)), lus);
    }

    internal static bool Commande(byte[] cdb, out (byte st, byte err, byte iir, int lus) f)
    {
        f = default;
        if (!Paquet(cdb))
            return false;
        f = Fin(Donnees(100));
        return (f.st & 0x88) == 0;
    }

    internal static string Dit((byte st, byte err, byte iir, int lus) f) =>
        $"statut {f.st:X2}, erreur {f.err:X2}, IIR {f.iir}, {f.lus} octets";

    /// <summary>Le lecteur au propre : l'attention du changement de disque consommée (TEST UNIT READY, puis
    /// REQUEST SENSE), par la reprise si une commande est restée en vol.</summary>
    internal static void Prepare()
    {
        Commande(Cdb(0x00), out _);
        Commande(Cdb(0x03, 0, 0, 0, 18), out _);
    }

    /// <summary>La reprise d'un pilote : DEVICE RESET, puis TEST UNIT READY, qui doit finir.</summary>
    private static string? Reprise()
    {
        Out(7, 0x08);
        Tranches(2);
        return Commande(Cdb(0x00), out var f) ? null : $"la reprise par DEVICE RESET devait rendre le lecteur : {Dit(f)}";
    }
}
