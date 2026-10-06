// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// r9-sb16 — la Sound Blaster 16 (G12.1), en C# SEUL.
//
// PB-150 : 41h ou 42h avec la fréquence 0 rend sblatcho nul en C (sound_sb_dsp.c:393) : dès qu'une minuterie du DSP
// tourne, l'échéance ne recule plus et timer_process boucle sans fin — l'hôte fige. La garde ramène 0 à 1 Hz
// (PLAN-G12.md, décision n° 10). Les essais, chacun sur un ami486 neuf et la carte conduite par ses ports depuis
// l'outil : 41h 0000h puis un DMA 8 bits automatique ; l'entrée directe (20h), qui arme la minuterie d'entrée pour
// toujours, puis 42h 0000h ; 41h 0000h deux fois, la minuterie armée par 80h entre les deux ; puis des tranches
// d'amorçage, qui doivent rendre la main. L'oracle n'est jamais appelé : il figerait. La règle ISA 16 bits
// (décision n° 4) : la carte refusée, avec un avertissement, sur les quatre machines sans MODEL_AT, par le .cfg et
// par --sndcard. Et trois survies sans garde, que l'oracle passe aussi (SB16BANC les joue sous boot-diff) :
// 3Bh = C0h (PB-152, l'indice borné) et l'entrée stéréo au-delà de FFFEh, en 8 et en 16 bits (PB-151, l'alias).

using iXtal26.Diag;
using iXtal26.Sound;

namespace iXtal26.Diff;

internal static class R9Sb16
{
    private const ushort Base = 0x220;

    internal static int Run(string romsPath)
    {
        var cfg = Path.Combine(Path.GetTempPath(), $"r9-sb16-{Environment.ProcessId}.cfg");
        var bad = 0;
        var n = 0;
        try
        {
            File.WriteAllText(cfg, "model = ami486\ncpu = 10\nmem_size = 4096\ngfxcard = tvga9000b\nsndcard = sb16\n");

            bad += Essai("sound_sb_dsp.c:393", "41h 0000h, puis un DMA 8 bits automatique (1Ch)", ref n, () =>
            {
                var d = Amorce(romsPath, cfg);
                Reset();
                W(0x41); W(0x00); W(0x00);
                W(0x48); W(0xFF); W(0x0F);
                W(0x1C);
                Tranches(300);
                return d.sb_freq == 1 && d.sb_timeo == 257 && d.sb_8_enable == 1 ? null
                    : $"ATTENDU sb_freq 1, sb_timeo 257, le DMA lancé (lu {d.sb_freq}, {d.sb_timeo}, {d.sb_8_enable})";
            });
            // 20h arme la minuterie d'entrée pour toujours (:363-369) ; 42h 0000h, ensuite, en annulerait le pas.
            bad += Essai("sound_sb_dsp.c:393", "l'entrée directe (20h), puis 42h 0000h", ref n, () =>
            {
                var d = Amorce(romsPath, cfg);
                Reset();
                W(0x41); W(0x56); W(0x22);
                W(0x20);
                W(0x42); W(0x00); W(0x00);
                Tranches(300);
                return d.sb_freq == 1 && d.sb_timei == 257 ? null : $"ATTENDU sb_freq 1, sb_timei 257 (lu {d.sb_freq}, {d.sb_timei})";
            });
            bad += Essai("sound_sb_dsp.c:393", "41h 0000h deux fois de suite, la minuterie de sortie armée par 80h", ref n, () =>
            {
                var d = Amorce(romsPath, cfg);
                Reset();
                W(0x41); W(0x00); W(0x00);
                W(0x80); W(0xFF); W(0x7F);
                W(0x41); W(0x00); W(0x00);
                Tranches(300);
                return R9.Compte("sound_sb_dsp.c:393") == 2 ? null : $"ATTENDU la garde deux fois (lu {R9.Compte("sound_sb_dsp.c:393")})";
            });

            foreach (var modele in new[] { "ibmpc", "ibmxt", "olivetti_m24", "pc1512" })
                bad += Essai("sound_sb.c:1349", $"la SB 16 sur « {modele} » par le .cfg", ref n, () =>
                {
                    // sb_pri est l'état de l'outil : la machine précédente n'est pas fermée entre deux essais.
                    sound_sb.sb_pri = null;
                    File.WriteAllText(cfg, $"model = {modele}\nsndcard = sb16\n");
                    Amorce(romsPath, cfg, false);
                    return Sound.sound.sound_card_current == 0 && sound_sb.sb_pri is null ? null
                        : $"ATTENDU aucune carte son (lu {Sound.sound.sound_card_get_internal_name(Sound.sound.sound_card_current)})";
                });
            bad += Essai("sound_sb.c:1349", "la SB 16 sur « ibmxt » par --sndcard après le .cfg", ref n, () =>
            {
                sound_sb.sb_pri = null;
                File.WriteAllText(cfg, "model = ibmxt\n");
                if (!pc.loadconfig(cfg))
                    throw new InvalidOperationException("loadconfig");
                if (!pc.setsndcard("sb16"))
                    throw new InvalidOperationException("setsndcard");
                Floppy.fdd_c.discfns[0] = Floppy.fdd_c.discfns[1] = "";
                if (!pc.initpc(romsPath))
                    throw new InvalidOperationException("initpc");
                return Sound.sound.sound_card_current == 0 && sound_sb.sb_pri is null ? null : "ATTENDU aucune carte son";
            });

            File.WriteAllText(cfg, "model = ami486\ncpu = 10\nmem_size = 4096\ngfxcard = tvga9000b\nsndcard = sb16\n");
            bad += Survie("3Bh = C0h, puis d'autres écritures du CT1745 (PB-152, l'indice borné)", ref n, () =>
            {
                Amorce(romsPath, cfg);
                io.outb(Base + 4, 0x3B);
                io.outb(Base + 5, 0xC0);
                io.outb(Base + 4, 0x30);
                io.outb(Base + 5, 0xF8);
                Tranches(50);
                return sound_sb.sb_pri!.mixer_sb16.regs[0x3B] == 0xC0 ? null : "ATTENDU 3Bh relu C0h";
            });
            bad += Survie("C8h en stéréo (20h), record_pos_read à FFFEh : l'indice FFFFh lit buffer[0] (PB-151)", ref n, () =>
            {
                var d = Amorce(romsPath, cfg);
                Reset();
                W(0xC8); W(0x20); W(0xFF); W(0x00);
                d.record_pos_read = 0xFFFE;
                d.buffer[0] = 0x1234;
                sound_sb_dsp.sb_poll_i(d);
                return d.record_pos_read == 0 ? null : $"ATTENDU record_pos_read revenu à 0 (lu {d.record_pos_read:X})";
            });
            bad += Survie("B8h en stéréo (20h), record_pos_read à FFFEh, en 16 bits (PB-151)", ref n, () =>
            {
                var d = Amorce(romsPath, cfg);
                Reset();
                W(0xB8); W(0x20); W(0xFF); W(0x00);
                d.record_pos_read = 0xFFFE;
                sound_sb_dsp.sb_poll_i(d);
                return null;
            });
        }
        finally
        {
            File.Delete(cfg);
        }
        Console.WriteLine(bad == 0 ? $"r9-sb16 : {n} essais, chaque garde atteinte, tout survit." : $"r9-sb16 : {bad} essai(s) en défaut sur {n}.");
        return bad == 0 ? 0 : 1;
    }

    /// <summary>Une machine neuve, amorcée cent tranches ; la SB de la configuration, si elle est montée.</summary>
    private static sb_dsp_t Amorce(string romsPath, string cfg, bool avecCarte = true)
    {
        if (!pc.loadconfig(cfg))
            throw new InvalidOperationException("loadconfig");
        Floppy.fdd_c.discfns[0] = Floppy.fdd_c.discfns[1] = "";
        for (var d = 0; d < 4; d++)
            Disc.hdd_c.ide_fn[d] = "";
        if (!pc.initpc(romsPath))
            throw new InvalidOperationException("initpc");
        Tranches(100);
        if (!avecCarte)
            return new sb_dsp_t();
        return (sound_sb.sb_pri ?? throw new InvalidOperationException("pas de SB après initpc")).dsp;
    }

    private static void Tranches(int n)
    {
        for (var i = 0; i < n; i++)
            pc.runpc();
    }

    private static void Reset()
    {
        io.outb(Base + 6, 1);
        io.outb(Base + 6, 0);
        _ = io.inb(Base + 0xa);
    }

    private static void W(int v) => io.outb(Base + 0xc, (byte)v);

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

    /// <summary>Une survie sans garde (PCem ne s'y arrête pas) : aucune garde R9 ne doit être atteinte.</summary>
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
