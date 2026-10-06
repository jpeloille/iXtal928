// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// reset-scsi-check — l'image d'un disque SCSI à travers un reset matériel (PB-121 ; G13.0, PLAN-G13.md), en C# seul.
//
// scsi_bus_close ne ferme rien : après un reset matériel (le menu de l'hôte), le flux du disque d'avant reste ouvert
// avec son dernier secteur écrit en tampon, pendant que la machine neuve rouvre le même fichier. La machine neuve
// relisait donc ce secteur tel qu'il était sur le disque, et à la sortie le vieux tampon, vidé le dernier, écrasait
// ce qui avait été écrit depuis : l'image de l'utilisateur perdait ses écritures. hdd_file vide désormais un flux
// encore ouvert sur la même image avant de la rouvrir.
//
// L'essai : l'ami486, l'AHA-1542C et un disque vierge à l'ID 0, dans le TMPDIR. Le secteur 100 reçoit « A » par le
// disque de la première machine, puis reset matériel ; la machine neuve doit relire « A », écrit « B » au même
// secteur, puis closepc ; le fichier doit garder « B ». Sans la correction : la relecture rend des zéros, et le
// fichier finit avec « A ».

using iXtal26.Disc;
using iXtal26.Scsi;

namespace iXtal26.Diff;

internal static class ResetScsiCheck
{
    private const int Secteur = 100;

    internal static int Run(string romsPath)
    {
        var tmp = Path.Combine(Path.GetTempPath(), $"reset-scsi-{Environment.ProcessId}");
        Directory.CreateDirectory(tmp);
        var image = Path.Combine(tmp, "d0.img");
        var cfg = Path.Combine(tmp, "reset.cfg");
        var bad = 0;
        try
        {
            using (var f = new FileStream(image, FileMode.Create, FileAccess.Write))
                f.SetLength(20 * 64 * 32 * 512L);
            File.WriteAllText(cfg, "model = ami486\ncpu = 10\nmem_size = 4096\ngfxcard = tvga9000b\nhdd_controller = aha1542c\n" +
                                   $"hdc_cylinders = 20\nhdc_heads = 64\nhdc_sectors = 32\nhdc_fn = {image}\n");
            Floppy.fdd_c.discfns[0] = "";
            Floppy.fdd_c.discfns[1] = "";
            if (!pc.loadconfig(cfg) || !pc.initpc(romsPath))
                return 2;

            var a = Rempli((byte)'A');
            var b = Rempli((byte)'B');
            hdd_file.hdd_write_sectors(Disque("la première machine").hdd, Secteur, 1, a);

            pc.resetpchard();

            var relu = new byte[512];
            var neuf = Disque("la machine neuve");
            hdd_file.hdd_read_sectors(neuf.hdd, Secteur, 1, relu);
            if (relu.AsSpan().SequenceEqual(a))
                Console.WriteLine("  après le reset, la machine neuve relit « A » : l'écriture d'avant le reset est sur l'image");
            else
            {
                bad++;
                Console.WriteLine($"  après le reset, la machine neuve relit {Decrit(relu)} au lieu de « A » : le flux d'avant n'a pas été vidé");
            }
            hdd_file.hdd_write_sectors(neuf.hdd, Secteur, 1, b);

            pc.closepc();

            var final = new byte[512];
            using (var f = new FileStream(image, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                f.Seek(Secteur * 512L, SeekOrigin.Begin);
                f.ReadExactly(final);
            }
            if (final.AsSpan().SequenceEqual(b))
                Console.WriteLine("  après closepc, l'image garde « B », la dernière écriture");
            else
            {
                bad++;
                Console.WriteLine($"  après closepc, l'image porte {Decrit(final)} au lieu de « B » : un vieux tampon a écrasé l'écriture");
            }
        }
        catch (Exception e)
        {
            bad++;
            Console.WriteLine($"reset-scsi-check : ARRÊT — {e.GetType().Name} : {e.Message.Trim()}");
        }
        finally
        {
            Directory.Delete(tmp, true);
        }

        Console.WriteLine(bad == 0 ? "\nVert : l'image SCSI garde ses écritures à travers un reset matériel (PB-121)."
                                   : $"\n{bad} échec(s) : le reset matériel perd des écritures de l'image.");
        return bad == 0 ? 0 : 1;
    }

    private static scsi_hd_data Disque(string machine)
    {
        if (scsi_aha1540.courante() is { } carte && carte.bus.device_data[0] is scsi_hd_data d)
            return d;
        throw new InvalidOperationException($"pas de disque SCSI à l'ID 0 sur {machine}");
    }

    private static byte[] Rempli(byte octet)
    {
        var t = new byte[512];
        Array.Fill(t, octet);
        return t;
    }

    private static string Decrit(byte[] secteur) => secteur[0] == 0 ? "des zéros" : $"« {(char)secteur[0]} »";
}
