// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — outillage de mesure.
//
// L'OPL QUI JOUE, POUR LA RÉFÉRENCE DE PERFORMANCE (G14.0a ; PLAN.md § G15 : « son »).
//
// SonBanc mesure les DSP, l'ADPCM et l'AWE32 ; l'OPL de ses cartes y reste muet. Ce banc fait jouer l'OPL3 de la
// SB Pro v2 (le DBOPL, opl3_update2 à chaque tampon) : le mode OPL3 (105h = 1), puis ses 18 voix à deux opérateurs,
// une note tenue chacune (l'enveloppe en maintien, l'attaque au plus vite, la sortie à gauche et à droite), à 18
// hauteurs différentes, par les ports 388h-38Bh. La machine est celle de SonBanc (l'ami486, la 9000B, montés sans
// BIOS), montée ici par Reference.Monter ; la mesure est SonBanc.Tampon, un tampon de 2 400 tics à 48 kHz ; le DSP ne
// joue rien. SonBanc.Preparer n'est pas employé : sa ligne d'état aurait la clé de SonProSortie. Avant la mesure,
// chaque processus joue deux tampons (le régime), puis imprime la somme du troisième et ses extrêmes : nuls, l'OPL ne
// jouerait pas, et le banc refuse.

using BenchmarkDotNet.Attributes;
using iXtal26.Sound;

namespace iXtal26.PerfBanc;

public class SonOplPro
{
    [Params("aucun", "tout")] public string Mode = "";

    [GlobalSetup]
    public void Preparer() => OplBanc.Preparer(Mode);

    [Benchmark]
    public long Tampon() => SonBanc.Tampon(0);
}

internal static class OplBanc
{
    private static void Opl(int banc, int registre, int valeur)
    {
        var port = (ushort)(banc == 0 ? 0x388 : 0x38a);
        io.outb(port, (byte)registre);
        io.outb((ushort)(port + 1), (byte)valeur);
    }

    /// <summary>Le mode OPL3, puis les neuf voix de chaque banc : le modulateur et la porteuse de la voix c sont les
    /// opérateurs c % 3 + 8 × (c / 3) et trois plus loin.</summary>
    internal static void Voix(sb_dsp_t _)
    {
        Opl(1, 0x05, 0x01);
        Opl(0, 0x01, 0x20);
        for (var b = 0; b < 2; b++)
            for (var c = 0; c < 9; c++)
            {
                var modulateur = c % 3 + 8 * (c / 3);
                foreach (var op in new[] { modulateur, modulateur + 3 })
                {
                    Opl(b, 0x20 + op, 0x21);
                    Opl(b, 0x60 + op, 0xf4);
                    Opl(b, 0x80 + op, 0x24);
                    Opl(b, 0xe0 + op, 0x00);
                }
                Opl(b, 0x40 + modulateur, 0x10);
                Opl(b, 0x40 + modulateur + 3, 0x00);
                Opl(b, 0xc0 + c, 0x30);
                var fnum = 0x200 + 16 * (9 * b + c);
                Opl(b, 0xa0 + c, fnum & 0xff);
                Opl(b, 0xb0 + c, 0x20 | (4 << 2) | (fnum >> 8));
            }
    }

    /// <summary>Pose le mode avant le gel, monte la machine, lance l'OPL, joue deux tampons ; puis un troisième, sa
    /// somme et ses extrêmes.</summary>
    internal static void Preparer(string mode)
    {
        Reference.Demander(mode, "SonOplPro");
        Reference.Monter("model = ami486\ncpu = 10\nmem_size = 4096\ngfxcard = tvga9000b\nsndcard = sbprov2\n",
                         "SonOplPro");
        Reference.VerifierMode(mode, "SonOplPro");
        sound.soundon = 0;
        var d = (sound_sb.sb_pri ?? throw new InvalidOperationException("SonOplPro : pas de SB après initpc")).dsp;
        Voix(d);
        SonBanc.Tampon(0);
        SonBanc.Tampon(0);
        var somme = SonBanc.Tampon(0);
        var (min, max) = (int.MaxValue, int.MinValue);
        for (var i = 0; i < 2 * sound.sound_buf_len_al; i++)
            (min, max) = (Math.Min(min, sound.outbuffer[i]), Math.Max(max, sound.outbuffer[i]));
        if (min == 0 && max == 0)
            throw new InvalidOperationException("SonOplPro : le tampon est muet, l'OPL ne joue pas");
        Reference.Verifier("SonOplPro", "sbprov2, OPL3, 18 voix", mode,
                           $"un tampon de {sound.sound_buf_len_al} tics, la somme du troisième {somme}, " +
                           $"de {min} à {max}");
    }
}
