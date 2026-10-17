// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — outillage de mesure.
//
// CE QUE COÛTE LE SON DU MODE MATÉRIEL (G13.8g3a, M1 ; PLAN-G13.md, § La vérification).
//
// En mode PCem, le JIT plie les gardes du son (M2). Ce banc mesure l'autre face, le coût d'un tampon de sortie quand
// les corrections sont demandées, un processus par mode (BenchmarkDotNet lance un processus par cas, et le mode se fige
// une fois par processus). L'unité est un tampon : sound_buf_len_al tics à 48 kHz (2 400, 50 ms), joués par les
// minuteries de la machine — le compteur de temps avancé d'échéance en échéance, timer_process à chaque pas, jusqu'à ce
// que sound_poll ait rendu le tampon (les gestionnaires des cartes appelés). Le processeur ne tourne pas : la machine
// est montée sans exécuter le BIOS (pc.initpc), la carte conduite par ses ports (220h), le 8237 programmé par les
// siens, en automatique sur un bloc de 4 Ko en RAM ; l'IRQ n'est jamais acquittée, le DSP en automatique continue. Les
// autres minuteries (la vidéo, le PIT) tournent dans les deux modes : elles sont dans la base.
// - Sb16Sortie : la SB 16, B6h, 16 bits stéréo à 44 100 Hz (PB-155, la fréquence ; PB-148, l'anneau du CAN) ;
// - Sb16Entree : la SB 16, CEh, 8 bits stéréo à 22 050 Hz en entrée, la sortie B6h en cours, à la même cadence (42h
//   pose le registre de 41h) (PB-148, PB-151) ;
// - ProSortie : la Pro v2, 1Ch, stéréo, la constante E9h (PB-148 ; PB-237, le reset du CT1345) ;
// - Adpcm : la SB 2.0, 7Dh, l'ADPCM 4 bits à 11 kHz (les six de G13.8b demandés ; PB-91 et PB-284, le 2 bits et le
//   2,6 bits, hors du scénario) ;
// - AweVoix : l'AWE32, 30 voix posées par les ports, envoyées à la réverbération et au chorus (PB-162, PB-163) ;
// - AweWc : l'AWE32 sans voix, 0, 16 ou 64 lectures de WC (A22h, pointeur 3Bh) par tampon, comme un traqueur (PB-158).
// Chaque cas porte aussi « son », le domaine entier. Avant la mesure, chaque processus imprime la somme de son tampon.

using BenchmarkDotNet.Attributes;
using iXtal26.Memory;
using iXtal26.Sound;

namespace iXtal26.PerfBanc;

public class SonSb16Sortie
{
    [Params("aucun", "PB-155", "PB-148", "son")] public string Mode = "";

    [GlobalSetup]
    public void Preparer() => SonBanc.Preparer(Mode, "sb16", SonBanc.Sb16Sortie);

    [Benchmark]
    public long Tampon() => SonBanc.Tampon(0);
}

public class SonSb16Entree
{
    [Params("aucun", "PB-148", "PB-151", "son")] public string Mode = "";

    [GlobalSetup]
    public void Preparer() => SonBanc.Preparer(Mode, "sb16", SonBanc.Sb16Entree);

    [Benchmark]
    public long Tampon() => SonBanc.Tampon(0);
}

public class SonProSortie
{
    [Params("aucun", "PB-148", "PB-237", "son")] public string Mode = "";

    [GlobalSetup]
    public void Preparer() => SonBanc.Preparer(Mode, "sbprov2", SonBanc.ProSortie);

    [Benchmark]
    public long Tampon() => SonBanc.Tampon(0);
}

public class SonAdpcm
{
    [Params("aucun", "PB-90,PB-91,PB-146,PB-234,PB-235,PB-284", "son")] public string Mode = "";

    [GlobalSetup]
    public void Preparer() => SonBanc.Preparer(Mode, "sb2.0", SonBanc.Adpcm);

    [Benchmark]
    public long Tampon() => SonBanc.Tampon(0);
}

public class SonAweVoix
{
    [Params("aucun", "PB-162,PB-163", "son")] public string Mode = "";

    [GlobalSetup]
    public void Preparer() => SonBanc.Preparer(Mode, "sbawe32", SonBanc.AweVoix);

    [Benchmark]
    public long Tampon() => SonBanc.Tampon(0);
}

public class SonAweWc
{
    [Params("aucun", "PB-158", "son")] public string Mode = "";
    [Params(0, 16, 64)] public int Lectures;

    [GlobalSetup]
    public void Preparer() => SonBanc.Preparer(Mode, "sbawe32", _ => { }, Lectures);

    [Benchmark]
    public long Tampon() => SonBanc.Tampon(Lectures);
}

internal static class SonBanc
{
    private const ushort Sb = 0x220;
    private const uint Bloc = 0x20000;

    private static void Dsp(params int[] octets)
    {
        foreach (var v in octets)
            io.outb(Sb + 0xc, (byte)v);
    }

    /// <summary>Le canal 1 du 8237, en automatique, 4 Ko depuis le bloc ; le canal 4 du 8237 haut en cascade et
    /// démasqué, comme le POST de l'AT le laisse (sans lui, le 8237 de la fiche, sous « tout », ne sert pas le
    /// bas).</summary>
    private static void Dma1(bool entree)
    {
        io.outb(0xd6, 0xc0);
        io.outb(0xd4, 0x00);
        io.outb(0x0a, 0x05);
        io.outb(0x0c, 0);
        io.outb(0x0b, (byte)(entree ? 0x55 : 0x59));
        io.outb(0x02, (byte)(Bloc & 0xff));
        io.outb(0x02, (byte)((Bloc >> 8) & 0xff));
        io.outb(0x83, (byte)((Bloc >> 16) & 0xff));
        io.outb(0x03, 0xff);
        io.outb(0x03, 0x0f);
        io.outb(0x0a, 0x01);
    }

    /// <summary>Le canal 5 du 8237 haut, en automatique, 2 K mots depuis le bloc + 64 Ko.</summary>
    private static void Dma5()
    {
        const uint a = Bloc + 0x10000;
        io.outb(0xd6, 0xc0);
        io.outb(0xd4, 0x00);
        io.outb(0xd4, 0x05);
        io.outb(0xd8, 0);
        io.outb(0xd6, 0x59);
        io.outb(0xc4, (byte)((a >> 1) & 0xff));
        io.outb(0xc4, (byte)((a >> 9) & 0xff));
        io.outb(0x8b, (byte)((a >> 16) & 0xfe));
        io.outb(0xc6, 0xff);
        io.outb(0xc6, 0x07);
        io.outb(0xd4, 0x01);
    }

    private static void Reset()
    {
        io.outb(Sb + 6, 1);
        io.outb(Sb + 6, 0);
        io.inb(Sb + 0xa);
    }

    internal static void Sb16Sortie(sb_dsp_t _)
    {
        Reset();
        Dma5();
        Dsp(0xd1, 0x41, 0xac, 0x44, 0xb6, 0x30, 0xff, 0x03);
    }

    internal static void Sb16Entree(sb_dsp_t d)
    {
        Sb16Sortie(d);
        Dma1(entree: true);
        Dsp(0x42, 0x56, 0x22, 0xce, 0x20, 0xff, 0x03);
    }

    internal static void ProSortie(sb_dsp_t _)
    {
        Reset();
        io.outb(Sb + 4, 0x0e);
        io.outb(Sb + 5, 0x02);
        Dma1(entree: false);
        Dsp(0xd1, 0x40, 0xe9, 0x48, 0xff, 0x07, 0x1c);
    }

    internal static void Adpcm(sb_dsp_t _)
    {
        Reset();
        Dma1(entree: false);
        Dsp(0xd1, 0x40, 0xa5, 0x48, 0xff, 0x07, 0x7d);
    }

    private static void Pointeur(int reg, int canal) => io.outw(0xe22, (ushort)((reg << 5) | canal));

    private static void Double(ushort port, int reg, int canal, uint v)
    {
        Pointeur(reg, canal);
        io.outw(port, (ushort)v);
        io.outw((ushort)(port + 2), (ushort)(v >> 16));
    }

    /// <summary>30 voix : la boucle de 1000h mots depuis 100h (la ROM), la hauteur 4000h, le volume 4000h sans filtre,
    /// le panoramique au centre, 40h vers la réverbération (PTRX) et le chorus (CSL) ; l'audio ouvert (HWCF3, bit 2).
    /// L'enveloppe reste éteinte : le volume tient sa cible. Le mot haut de CCCA passe par A22h (Data2, registre 0).
    /// La réverbération et le chorus reçoivent leurs entrées, sans gain de sortie ni retard posés : ils tournent à
    /// chaque tampon, leur coût est mesuré, mais la somme ne les voit pas.</summary>
    internal static void AweVoix(sb_dsp_t _)
    {
        Pointeur(1, 31);
        io.outw(0xa20, 0x0004);
        for (var c = 0; c < 30; c++)
        {
            Double(0x620, 6, c, 0x80000100);
            Double(0x620, 7, c, 0x40001100);
            Double(0xa20, 0, c, 0x00000100);
            Double(0x620, 1, c, 0x40004000);
            Double(0x620, 0, c, 0x40000000);
            Double(0x620, 3, c, 0x4000ffff);
            Double(0x620, 2, c, 0x4000ffff);
        }
    }

    /// <summary>La racine du dépôt, au-dessus du banc (roms/ et iXtal26.sln) : BenchmarkDotNet lance ses processus
    /// depuis un répertoire de build sous tools/perfbanc.</summary>
    private static string Depot()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "iXtal26.sln")) &&
                Directory.Exists(Path.Combine(d.FullName, "roms")))
                return d.FullName;
        throw new InvalidOperationException("le dépôt (roms/, iXtal26.sln) est introuvable au-dessus du banc");
    }

    /// <summary>Pose le mode avant le gel, monte l'ami486 et la carte (à l'IRQ 7, comme les profils du dépôt, Q23),
    /// remplit le bloc d'une dent de scie, lance la carte, puis joue deux tampons (le régime) et imprime la somme du
    /// second.</summary>
    internal static void Preparer(string mode, string carte, Action<sb_dsp_t> lancer, int lectures = 0)
    {
        if (!ModeMateriel.Demander(mode, "SonBanc"))
            throw new InvalidOperationException($"mode « {mode} » refusé");
        var cfg = Path.Combine(Path.GetTempPath(), $"sonbanc-{Environment.ProcessId}.cfg");
        var section = carte switch
        {
            "sb16" => "[Sound Blaster 16]\nirq = 7\n",
            "sbawe32" => "[Sound Blaster AWE32]\nirq = 7\n",
            "sb2.0" => "[Sound Blaster v2.0]\nirq = 7\n",
            _ => "",
        };
        File.WriteAllText(cfg, $"model = ami486\ncpu = 10\nmem_size = 4096\ngfxcard = tvga9000b\nsndcard = {carte}\n" +
                               section);
        if (!pc.loadconfig(cfg))
            throw new InvalidOperationException("loadconfig");
        File.Delete(cfg);
        Floppy.fdd_c.discfns[0] = Floppy.fdd_c.discfns[1] = "";
        for (var u = 0; u < 4; u++)
            Disc.hdd_c.ide_fn[u] = "";
        if (!pc.initpc(Path.Combine(Depot(), "roms")))
            throw new InvalidOperationException("la machine ne se monte pas");
        sound.soundon = 0;
        for (var k = 0; k < 0x20000; k++)
            mem.mem_writeb_phys((uint)(Bloc + k), (byte)(k * 7));
        var d = (sound_sb.sb_pri ?? throw new InvalidOperationException("pas de SB après initpc")).dsp;
        lancer(d);
        if (lectures > 0)
            Pointeur(1, 27);
        Tampon(lectures);
        Console.WriteLine($"// SonBanc ({ModeMateriel.Description}, {carte}, {lectures} lectures) : un tampon de " +
                          $"{sound.sound_buf_len_al} tics, la somme {Tampon(lectures)} ; le DSP après deux tampons, " +
                          $"8 bits {d.sb_8_enable}, 16 bits {d.sb_16_enable}, coupé {d.muted}");
    }

    /// <summary>Un tampon : le temps avancé d'échéance en échéance jusqu'à ce que sound_poll l'ait rendu ; aux tics
    /// k × len / lectures, WC lu (le pointeur posé par Preparer). Rend la somme du tampon rendu.</summary>
    internal static long Tampon(int lectures)
    {
        var len = sound.sound_buf_len_al;
        var prochaine = 0;
        var avant = sound.sound_pos_global;
        while (true)
        {
            var pas = unchecked((int)(timer.timer_target - (uint)timer.tsc));
            timer.tsc += (ulong)Math.Max(pas, 1);
            timer.timer_process();
            var pos = sound.sound_pos_global;
            if (pos < avant)
                break;
            avant = pos;
            while (lectures > 0 && prochaine < lectures && pos >= prochaine * len / lectures)
            {
                io.inw(0xa22);
                prochaine++;
            }
        }
        long somme = 0;
        for (var i = 0; i < 2 * len; i++)
            somme += sound.outbuffer[i];
        return somme;
    }
}
