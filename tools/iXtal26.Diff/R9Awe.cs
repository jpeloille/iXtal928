// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// r9-emu8k et r9-awecfg — l'AWE32 et son EMU8000 (G12.2), en C# SEUL.
//
// r9-emu8k — PB-161 : les quartets Eh et Fh d'init2, canal 14h (et 16h quand la réverbération est liée), donnent
// des tampons de 33 et 34 × 242 entrées, au-delà de MAX_REFL_SIZE (7 744, sound_emu8k.h:110) : en C, les peignes
// écrivent au-delà de leur tableau, dans sb_t et le tas. Les tampons de 8 228 entrées les tiennent, et une garde
// marque chacune des quatre affectations (sound_emu8k.c:1164, :1165, :1167, :1173 ; PLAN-G12.md, décision n° 15).
// Chaque essai, sur un ami486 neuf : une note de la ROM, son envoi à la réverbération au maximum, la taille posée,
// puis assez de tranches pour que le tampon fasse son tour : ses entrées au-delà de 7 744 doivent être écrites. Et
// une survie sans garde : le quartet Dh, la dernière taille qui tienne.
//
// r9-awecfg — la configuration : l'AWE32 sans sa ROM, ou avec une ROM d'une autre taille que 1 Mio (R9 : PCem
// tombe, sound_emu8k.c:2019-2022 ; décision n° 5) ; l'AWE32 sur l'ibmxt (la règle ISA 16 bits, décision n° 4) ;
// emu_addr et onboard_ram hors de leurs listes (PB-93 : le défaut, averti) ; et dans leurs listes, l'EMU8000
// trouvé à son adresse, sa RAM à sa taille.

using iXtal26.Diag;
using iXtal26.Sound;

namespace iXtal26.Diff;

internal static class R9Awe
{
    private const ushort D0 = 0x620, D1 = 0xA20, D2 = 0xA22, D3 = 0xE20, Ptr = 0xE22;
    private const string Awe = "Sound Blaster AWE32";

    internal static int RunEmu8k(string romsPath)
    {
        var cfg = Path.Combine(Path.GetTempPath(), $"r9-emu8k-{Environment.ProcessId}.cfg");
        var bad = 0;
        var n = 0;
        try
        {
            File.WriteAllText(cfg, "model = ami486\ncpu = 10\nmem_size = 4096\ngfxcard = tvga9000b\nsndcard = sbawe32\n");
            // (le site, l'essai, la réverbération liée, le canal d'init2, sa valeur, le tampon qui doit déborder 7 744)
            var essais = new (string site, string nom, bool liee, int canal, ushort val, Func<emu8k_reverb_eng_t, emu8k_reverb_combfilter_t> tampon)[]
            {
                ("sound_emu8k.c:1164", "init2[14h] = 0F00h : la réflexion 5 à 33 × 242", false, 0x14, 0x0F00, r => r.reflections[5]),
                ("sound_emu8k.c:1165", "init2[14h] = 0E00h, liée : la queue gauche à 33 × 242", true, 0x14, 0x0E00, r => r.tailL),
                ("sound_emu8k.c:1167", "init2[14h] = 0E00h : la queue droite à 33 × 242", false, 0x14, 0x0E00, r => r.tailR),
                ("sound_emu8k.c:1173", "init2[16h] = 0F00h, liée : la queue droite à 34 × 242", true, 0x16, 0x0F00, r => r.tailR),
            };
            foreach (var (site, nom, liee, canal, val, tampon) in essais)
                bad += Essai(site, nom, ref n, () =>
                {
                    var e = Amorce(romsPath, cfg);
                    Reverberer(e, liee, canal, val);
                    var t = tampon(e.reverb_engine);
                    var ecrites = 0;
                    for (var i = sound_emu8k.MAX_REFL_SIZE; i < t.bufsize; i++)
                        if (t.reflection[i] != 0)
                            ecrites++;
                    return t.bufsize > sound_emu8k.MAX_REFL_SIZE && ecrites > 0 ? null
                        : $"ATTENDU un tampon de plus de 7 744 entrées, écrit au-delà (lu {t.bufsize}, {ecrites} entrée(s) écrites au-delà)";
                });
            bad += Survie("init2[14h] = 0D00h : 31 et 32 × 242, la dernière taille qui tienne", ref n, () =>
            {
                var e = Amorce(romsPath, cfg);
                Reverberer(e, false, 0x14, 0x0D00);
                var r = e.reverb_engine;
                return r.reflections[5].bufsize == 31 * 242 && r.tailL.bufsize == sound_emu8k.MAX_REFL_SIZE &&
                       r.tailR.bufsize == sound_emu8k.MAX_REFL_SIZE ? null
                    : $"ATTENDU 7 502, 7 744, 7 744 (lu {r.reflections[5].bufsize}, {r.tailL.bufsize}, {r.tailR.bufsize})";
            });
        }
        finally
        {
            File.Delete(cfg);
        }
        Console.WriteLine(bad == 0 ? $"r9-emu8k : {n} essais, chaque garde atteinte, tout survit." : $"r9-emu8k : {bad} essai(s) en défaut sur {n}.");
        return bad == 0 ? 0 : 1;
    }

    internal static int RunCfg(string romsPath)
    {
        var bad = 0;
        var n = 0;
        var tmp = Path.Combine(Path.GetTempPath(), $"r9-awecfg-{Environment.ProcessId}");
        try
        {
            // Deux chemins de ROM : sans awe32.raw, et avec une awe32.raw de 512 Kio ; le reste lié à roms/.
            var sans = Path.Combine(tmp, "sans");
            var courte = Path.Combine(tmp, "courte");
            foreach (var d in new[] { sans, courte })
            {
                Directory.CreateDirectory(d);
                foreach (var f in Directory.EnumerateFileSystemEntries(romsPath))
                    if (Path.GetFileName(f) != "awe32.raw")
                        File.CreateSymbolicLink(Path.Combine(d, Path.GetFileName(f)), Path.GetFullPath(f));
            }
            var rom = File.ReadAllBytes(Path.Combine(romsPath, "awe32.raw"));
            File.WriteAllBytes(Path.Combine(courte, "awe32.raw"), rom[..(512 << 10)]);

            const string ami = "model = ami486\ncpu = 10\nmem_size = 4096\ngfxcard = tvga9000b\nsndcard = sbawe32\n";
            bad += Refus("sound_emu8k.c:2019", "l'AWE32 sans awe32.raw", sans, ami, "awe32.raw", ref n);
            bad += Refus("sound_emu8k.c:2019", "l'AWE32 et une awe32.raw de 512 Kio", courte, ami, "awe32.raw", ref n);
            bad += Refus("sound_sb.c:1349", "l'AWE32 sur « ibmxt »", romsPath, "model = ibmxt\nsndcard = sbawe32\n", "ISA 16 bits", ref n);

            // (l'essai, la section, l'adresse du DSP, celle de la puce, la RAM en Ko, les avertissements « hors de la liste »)
            var sections = new (string nom, string section, int dsp, int addr, int ram, int avertis)[]
            {
                ("dans la liste : le DSP en 240h, l'EMU8000 en 660h, sans RAM", "addr = 576\nemu_addr = 1632\nonboard_ram = 0\n", 0x240, 0x660, 0, 0),
                ("dans la liste : 28 Mo de RAM", "onboard_ram = 28672\n", 0x220, 0x620, 28672, 0),
                ("PB-93 : emu_addr FFFEh", "emu_addr = 65534\n", 0x220, 0x620, 512, 1),
                ("PB-93 : onboard_ram -1", "onboard_ram = -1\n", 0x220, 0x620, 512, 1),
                ("PB-93 : onboard_ram 1024", "onboard_ram = 1024\n", 0x220, 0x620, 512, 1),
            };
            foreach (var (nom, section, dsp, addr, ram, avertis) in sections)
                bad += Section(romsPath, nom, ami + $"\n[{Awe}]\n" + section, dsp, addr, ram, avertis, ref n);
        }
        finally
        {
            Directory.Delete(tmp, true);
        }
        Console.WriteLine(bad == 0 ? $"r9-awecfg : {n} essais, les refus avertis, hors liste → défaut averti, tout survit."
            : $"r9-awecfg : {bad} essai(s) en défaut sur {n}.");
        return bad == 0 ? 0 : 1;
    }

    /// <summary>Une machine neuve, amorcée cent tranches ; l'EMU8000 de son AWE32.</summary>
    private static emu8k_t Amorce(string romsPath, string cfg)
    {
        sound_sb.sb_pri = null;
        if (!pc.loadconfig(cfg))
            throw new InvalidOperationException("loadconfig");
        Floppy.fdd_c.discfns[0] = Floppy.fdd_c.discfns[1] = "";
        for (var d = 0; d < 4; d++)
            Disc.hdd_c.ide_fn[d] = "";
        if (!pc.initpc(romsPath))
            throw new InvalidOperationException("initpc");
        Tranches(100);
        return sound_sb.sb_pri?.emu8k ?? throw new InvalidOperationException("pas d'EMU8000 après initpc");
    }

    /// <summary>Le son actif, une note de la ROM sur le canal 0, envoyée à la réverbération au maximum, l'amortisseur
    /// passant (à zéro, il éteindrait l'entrée des peignes) ; la réverbération liée ou non (init1[7]), le canal d'init2
    /// à sa valeur ; puis 150 tranches.</summary>
    private static void Reverberer(emu8k_t e, bool liee, int canal, ushort val)
    {
        Wr(1, 31, D1, 0x0004);                                  // hwcf3 : le son actif
        Wr(2, 3, D1, 0x00FF);                                   // init1[3] : le mélange au maximum
        Wr(3, 1, D1, 0x00FF);                                   // init3[1] : l'entrée des réflexions
        Wr(3, 0x1F, D2, 0x00FF);                                // init4[1Fh] : le retour des queues
        Wr(2, 0x1D, D2, 0x0000);                                // init2[1Dh] : l'amortisseur passant (damp2 = 1)
        Wr(2, 7, D1, liee ? (ushort)0x8474 : (ushort)0x0000);
        Wr(2, canal, D2, val);
        Wr(5, 0, D1, 0x0080);                                   // DCYSUSV : le moteur coupé
        Wr(4, 0, D1, 0x8000);                                   // ENVVOL : sans délai
        Wr(4, 0, D2, 0x7F7F);                                   // ATKHLDV
        Wr(0, 0, D3, 0xE000);                                   // IP
        Wr(1, 0, D3, 0xFF00);                                   // IFATN
        Wr(1, 0, (ushort)(D0 + 2), 0x4000);                     // PTRX : la hauteur, l'envoi à la réverbération FFh
        Wr(1, 0, D0, 0xFF00);
        Wr(0, 0, (ushort)(D0 + 2), 0x4000);                     // CPF
        Wr(3, 0, (ushort)(D0 + 2), 0xFFFF);                     // VTFT
        Wr(3, 0, D0, 0xFFFF);
        Wr(2, 0, D0, 0xFFFF);                                   // CVCF : le filtre ouvert
        Wr(6, 0, D0, 0x0100);                                   // PSST : la boucle de 100h à 8000h, au centre
        Wr(6, 0, (ushort)(D0 + 2), 0x8000);
        Wr(7, 0, D0, 0x8000);                                   // CSL
        Wr(7, 0, (ushort)(D0 + 2), 0x0000);
        Wr(0, 0, D1, 0x0100);                                   // CCCA : le départ en 100h
        Wr(0, 0, D2, 0x0000);
        Wr(5, 0, D1, 0x7F7F);                                   // DCYSUSV : le moteur rallumé
        Tranches(150);
        _ = e;
    }

    private static void Wr(int reg, int canal, ushort port, ushort val)
    {
        io.outw(Ptr, (ushort)((reg << 5) | canal));
        io.outw(port, val);
    }

    private static void Tranches(int n)
    {
        for (var i = 0; i < n; i++)
            pc.runpc();
    }

    /// <summary>Un refus : la carte ne doit pas être montée, l'avertissement dit, la garde atteinte.</summary>
    private static int Refus(string site, string nom, string roms, string corps, string avertissement, ref int n)
    {
        n++;
        var cfg = Path.Combine(Path.GetTempPath(), $"r9-awecfg-{Environment.ProcessId}-{n}.cfg");
        File.WriteAllText(cfg, corps);
        R9.Raz();
        var err = Console.Error;
        var capte = new StringWriter();
        try
        {
            sound_sb.sb_pri = null;
            Console.SetError(capte);
            if (!pc.loadconfig(cfg))
                throw new InvalidOperationException("loadconfig");
            Floppy.fdd_c.discfns[0] = Floppy.fdd_c.discfns[1] = "";
            if (!pc.initpc(roms))
                throw new InvalidOperationException("initpc");
            Tranches(50);
            Console.SetError(err);
            var ligne = capte.ToString().Split('\n').FirstOrDefault(l => l.Contains("sndcard") && l.Contains(avertissement));
            string? faute = null;
            if (sound.sound_card_current != 0 || sound_sb.sb_pri is not null)
                faute = $"ATTENDU aucune carte son (lu {sound.sound_card_get_internal_name(sound.sound_card_current)})";
            else if (ligne is null)
                faute = $"pas d'avertissement « {avertissement} »";
            else if (R9.Compte(site) == 0)
                faute = "la garde n'a pas été atteinte — l'essai ne prouve rien";
            if (ligne is not null)
                Console.WriteLine($"    {ligne.Trim()}");
            Console.WriteLine($"  {site} — {nom} : {(faute is null ? $"refusée, survit (garde atteinte {R9.Compte(site)} fois)" : $"EN DÉFAUT — {faute}")}");
            return faute is null ? 0 : 1;
        }
        catch (Exception e)
        {
            Console.SetError(err);
            Console.WriteLine($"  {site} — {nom} : ARRÊT — {e.GetType().Name} : {e.Message.Trim()}");
            return 1;
        }
        finally
        {
            Console.SetError(err);
            File.Delete(cfg);
        }
    }

    /// <summary>Une section [Sound Blaster AWE32] : le DSP en `dsp`, l'EMU8000 trouvé à `addr` (le pointeur écrit s'y
    /// relit), sa RAM de `ram` Ko, les avertissements « hors de la liste » comptés, après 100 tranches.</summary>
    private static int Section(string romsPath, string nom, string corps, int dsp, int addr, int ram, int avertis, ref int n)
    {
        n++;
        var cfg = Path.Combine(Path.GetTempPath(), $"r9-awecfg-{Environment.ProcessId}-{n}.cfg");
        File.WriteAllText(cfg, corps);
        R9.Raz();
        var err = Console.Error;
        var capte = new StringWriter();
        try
        {
            sound_sb.sb_pri = null;
            Console.SetError(capte);
            if (!pc.loadconfig(cfg))
                throw new InvalidOperationException("loadconfig");
            Floppy.fdd_c.discfns[0] = Floppy.fdd_c.discfns[1] = "";
            if (!pc.initpc(romsPath))
                throw new InvalidOperationException("initpc");
            Tranches(100);
            Console.SetError(err);
            var lignes = capte.ToString().Split('\n').Where(l => l.Contains("hors de la liste")).ToList();
            foreach (var l in lignes)
                Console.WriteLine($"    {l.Trim()}");
            var e = sound_sb.sb_pri?.emu8k;
            string? faute = null;
            var lu = "-";
            if (e is null)
                faute = "pas d'EMU8000 après initpc";
            else
            {
                io.outw((ushort)(addr + 0x802), 0x0E5);              // le pointeur : registre 7, canal 5
                var relu = io.inw((ushort)(addr + 0x802)) & 0xFF;
                var ko = (int)((e.ram_end_addr - 0x200000) >> 9);
                var base_ = sound_sb.sb_pri!.dsp.sb_addr;
                lu = $"le DSP en {base_:X}h, pointeur relu {relu:X2}h en {addr + 0x802:X}h, RAM {ko} Ko";
                if (base_ != dsp || relu != 0xE5 || ko != ram)
                    faute = $"ATTENDU le DSP en {dsp:X}h, E5h en {addr + 0x802:X}h et {ram} Ko";
                else if (lignes.Count != avertis)
                    faute = $"{lignes.Count} avertissement(s), ATTENDU {avertis}";
                else if (R9.Resume() is { Length: > 0 } r)
                    faute = $"une garde R9 atteinte ({r})";
            }
            Console.WriteLine($"  {nom} : survit — {lu}{(faute is null ? "" : $" ; {faute}")}");
            return faute is null ? 0 : 1;
        }
        catch (Exception e)
        {
            Console.SetError(err);
            Console.WriteLine($"  {nom} : ARRÊT — {e.GetType().Name} : {e.Message.Trim()}");
            return 1;
        }
        finally
        {
            Console.SetError(err);
            File.Delete(cfg);
        }
    }

    /// <summary>Un essai R9 : `corps` rend null s'il est conforme ; le site doit avoir été atteint.</summary>
    private static int Essai(string site, string nom, ref int n, Func<string?> corps)
    {
        n++;
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
    }

    /// <summary>Une survie sans garde : aucune garde R9 ne doit être atteinte.</summary>
    private static int Survie(string nom, ref int n, Func<string?> corps)
    {
        n++;
        R9.Raz();
        try
        {
            var faute = corps() ?? (R9.Resume() is { Length: > 0 } r ? $"une garde R9 atteinte ({r})" : null);
            Console.WriteLine($"  {nom} : {(faute is null ? "survit" : $"EN DÉFAUT — {faute}")}");
            return faute is null ? 0 : 1;
        }
        catch (Exception e)
        {
            Console.WriteLine($"  {nom} : ARRÊT — {e.GetType().Name} : {e.Message.Trim()}");
            return 1;
        }
    }
}
