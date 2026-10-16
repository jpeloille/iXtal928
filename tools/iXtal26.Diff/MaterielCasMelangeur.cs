// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — outillage de vérification.
//
// Les cas des mélangeurs (G13.8d1, PLAN-G13.md) : PB-237 (les volumes du reset), PB-145 (la SB 2.0 sans mélangeur) et
// PB-153 (3Dh et 3Eh au reset, la traduction du 16 bits vers le canal 8 bits par 81h), sur le banc du son
// (MaterielCasSon.cs). Le mélangeur se lit par ses ports (2x4h l'index, 2x5h la donnée ; 254h pour le CT1335 en 250h),
// le volume CD dans sound.cd_vol_l et cd_vol_r, que le mélangeur pose.

using iXtal26.Memory;
using iXtal26.Sound;

namespace iXtal26.Diff;

internal static partial class MaterielCas
{
    private const string Ct1335 = "[Sound Blaster v2.0]\nmixaddr = 592\n";

    private static void MelEcrit(ushort port, int index, int val)
    {
        io.outb(port, (byte)index);
        io.outb((ushort)(port + 1), (byte)val);
    }

    /// <summary>Les registres <paramref name="index"/> du mélangeur en <paramref name="port"/>, « 0Ah=06 ».</summary>
    private static string MelLit(ushort port, params int[] index) =>
        string.Join(" ", index.Select(i =>
        {
            io.outb(port, (byte)i);
            return $"{i:X2}h={io.inb((ushort)(port + 1)):X2}";
        }));

    private static string Cd() => $"CD {sound.cd_vol_l}/{sound.cd_vol_r}";

    // PB-237 — le reset des mélangeurs pose les volumes du guide : CT1335, la voix (0Ah) à 0 ⇒ −46 dB (p. 4-5) ; CT1345,
    // la voix, le général et la MIDI à 4 ⇒ −11 dB (p. 4-9) ; CT1745, 30h à 35h à 24 ⇒ −14 dB (p. 4-15). PCem les relève
    // à 0 dB, au démarrage (les init remettent le mélangeur à zéro) et à chaque reset ; le CD, qui suit le général, est
    // plus fort aussi. Témoins : les registres que PCem pose comme le guide (le CD, la ligne, les sélecteurs, les
    // aigus et les graves), et l'index relu 00h après le reset.
    private static int Pb237(bool hw)
    {
        int Un(string carte, string nom, Func<sb_dsp_t, string> corps, string pcem, string mat, string section = "",
               bool temoin = false) =>
            AvecSb(carte, d =>
            {
                var v = corps(d);
                var att = temoin || hw ? mat : pcem;
                return Voir(nom, v == att, v, att, hw);
            }, section);
        var bad = Un("sb2.0", "SB 2.0, CT1335 en 250h : au démarrage", _ => MelLit(0x254, 0x02, 0x06, 0x08, 0x0a),
                     "02h=08 06h=08 08h=00 0Ah=06", "02h=08 06h=08 08h=00 0Ah=00", Ct1335);
        bad += Un("sb2.0", "SB 2.0, CT1335 : 0Ah=04h, 02h=0Eh, puis le reset", _ =>
        {
            MelEcrit(0x254, 0x0a, 0x04);
            MelEcrit(0x254, 0x02, 0x0e);
            MelEcrit(0x254, 0x00, 0x00);
            return $"index {io.inb(0x254):X2} {MelLit(0x254, 0x02, 0x0a)}";
        }, "index 00 02h=08 0Ah=06", "index 00 02h=08 0Ah=00", Ct1335);
        bad += Un("sbprov2", "SB Pro 2, CT1345 : au démarrage", _ => $"{MelLit(0x224, 0x04, 0x22, 0x26, 0x28)} {Cd()}",
                  "04h=EE 22h=EE 26h=EE 28h=00 CD 81/81", "04h=88 22h=88 26h=88 28h=00 CD 20/20");
        bad += Un("sbprov2", "SB Pro 2, CT1345 : 22h=FFh, 0Eh=02h, puis le reset", d =>
        {
            MelEcrit(0x224, 0x22, 0xff);
            MelEcrit(0x224, 0x0e, 0x02);
            var avant = d.stereo;
            MelEcrit(0x224, 0x00, 0x00);
            return $"stéréo {avant} puis {d.stereo}, index {io.inb(0x224):X2} {MelLit(0x224, 0x0e, 0x22)} {Cd()}";
        }, "stéréo 2 puis 0, index 00 0Eh=00 22h=EE CD 81/81", "stéréo 2 puis 0, index 00 0Eh=00 22h=88 CD 20/20");
        bad += Un("sb16", "SB 16, CT1745 : au démarrage", _ => $"{MelLit(0x224, 0x30, 0x31, 0x32, 0x33, 0x34, 0x35, 0x22)} {Cd()}",
                  "30h=F8 31h=F8 32h=F8 33h=F8 34h=F8 35h=F8 22h=FF CD 12/12",
                  "30h=C0 31h=C0 32h=C0 33h=C0 34h=C0 35h=C0 22h=CC CD 2/2");
        bad += Un("sb16", "SB 16, CT1745 : 30h=08h, 43h=01h, 44h=F0h, puis le reset", _ =>
        {
            MelEcrit(0x224, 0x30, 0x08);
            MelEcrit(0x224, 0x43, 0x01);
            MelEcrit(0x224, 0x44, 0xf0);
            MelEcrit(0x224, 0x00, 0x00);
            return $"index {io.inb(0x224):X2} {MelLit(0x224, 0x30, 0x43, 0x44)}";
        }, "index 00 30h=F8 43h=00 44h=80", "index 00 30h=C0 43h=00 44h=80");
        bad += Un("sb16", "SB 16, CT1745 : les autres registres du reset (témoin)", _ =>
                      MelLit(0x224, 0x36, 0x38, 0x3a, 0x3b, 0x3c, 0x3f, 0x41, 0x43, 0x44, 0x47),
                  "", "36h=00 38h=00 3Ah=00 3Bh=00 3Ch=1F 3Fh=00 41h=00 43h=00 44h=80 47h=80", temoin: true);
        bad += Un("sbprov2", "SB Pro 2, CT1345 : le CD et la ligne au démarrage (témoin)", _ => MelLit(0x224, 0x0a, 0x0c, 0x0e, 0x28, 0x2e),
                  "", "0Ah=00 0Ch=00 0Eh=00 28h=00 2Eh=00", temoin: true);
        // Le reset du CT1745 est partagé avec PB-153 : sa part (3Dh, 3Eh) ne suit que PB-153.
        bad += Un("sb16", "SB 16, CT1745 : 3Dh et 3Eh au démarrage, selon PB-153 (témoin)", _ => MelLit(0x224, 0x3d, 0x3e),
                  "", materiel.pb_153 ? "3Dh=15 3Eh=0B" : "3Dh=55 3Eh=2B", temoin: true);
        return bad;
    }

    // PB-145 — sb_2_init remet le CT1335 à zéro même sans mélangeur (`mixaddr` = 0) : le volume CD tombe à 20 sur
    // 65 535, sans port pour le relever. Sans mélangeur, rien ne le règle : 65 535, comme la SB 1.0 et la 1.5 (déduit).
    // Témoins : la SB 2.0 avec son CT1335 en 250h (le minimum du guide, 20, dans les deux modes) et la SB 1.5.
    private static int Pb145(bool hw)
    {
        int Un(string carte, string nom, string pcem, string mat, string section = "", bool temoin = false) =>
            AvecSb(carte, _ =>
            {
                var v = Cd();
                var att = temoin || hw ? mat : pcem;
                return Voir(nom, v == att, v, att, hw);
            }, section);
        var bad = Un("sb2.0", "SB 2.0 sans mélangeur : le CD au démarrage", "CD 20/20", "CD 65535/65535");
        bad += Un("sb2.0", "SB 2.0, CT1335 en 250h (témoin)", "", "CD 20/20", Ct1335, temoin: true);
        bad += Un("sb1.5", "SB 1.5 (témoin)", "", "CD 65535/65535", temoin: true);
        return bad;
    }

    // PB-153 — le CT1745 : le reset pose 3Dh et 3Eh à 15h et 0Bh (la ligne, le CD et le micro ; guide, p. 4-16), PCem à
    // 55h et 2Bh, la MIDI reliée. 81h écrit avec un seul canal 8 bits et aucun canal 16 bits fait passer le 16 bits par le
    // canal 8 bits, deux octets par échantillon (p. 2-7) : PCem garde le canal 5, que rien ne sert, et le transfert ne
    // bouge pas. La traduction ne suit ni le reset du mélangeur ni celui du DSP (comme PCem garde 81h ; déduit). En
    // stéréo, deux mots par échantillon ; avec PB-239, une commande en file pendant un automatique traduit. Témoins :
    // 81h=22h et 81h=E2h après la traduction (un canal 16 bits de nouveau), 81h=0Ah, deux canaux 8 bits (inconnu : celui
    // de PCem, le dernier), et 30h, la part du reset de PB-237.
    private static int Pb153(bool hw)
    {
        int Un(string nom, Func<sb_dsp_t, string> corps, string pcem, string mat, bool temoin = false) =>
            AvecSb("sb16", d =>
            {
                var v = corps(d);
                var att = temoin || hw ? mat : pcem;
                return Voir(nom, v == att, v, att, hw);
            });
        // Quatre échantillons signés : 1234h, 5678h, 9ABCh, DEF0h, l'octet bas d'abord.
        byte[] mots = [0x34, 0x12, 0x78, 0x56, 0xbc, 0x9a, 0xf0, 0xde];
        void Memoire()
        {
            for (var i = 0; i < mots.Length; i++)
                mem.mem_writeb_phys(0x10000 + (uint)i, mots[i]);
        }
        var bad = Un("SB 16 : 3Dh et 3Eh au démarrage", _ => MelLit(0x224, 0x3d, 0x3e), "3Dh=55 3Eh=2B", "3Dh=15 3Eh=0B");
        bad += Un("SB 16 : 3Dh=7Fh, 3Eh=7Fh, puis le reset", _ =>
        {
            MelEcrit(0x224, 0x3d, 0x7f);
            MelEcrit(0x224, 0x3e, 0x7f);
            MelEcrit(0x224, 0x00, 0x00);
            return MelLit(0x224, 0x3d, 0x3e);
        }, "3Dh=55 3Eh=2B", "3Dh=15 3Eh=0B");
        bad += Un("SB 16 : 81h=02h, relu", _ =>
        {
            MelEcrit(0x224, 0x81, 0x02);
            return MelLit(0x224, 0x81);
        }, "81h=22", "81h=02");
        bad += Un("SB 16 : 81h=02h, puis B0h 10h 03h 00h sur le canal 1", d =>
        {
            Memoire();
            MelEcrit(0x224, 0x81, 0x02);
            Dma1(0x10000, 0x07, false);
            Dsp(0xb0, 0x10, 0x03, 0x00);
            return $"{TicsDma(d, 12, seize: true)}, sortie {(ushort)d.sbdatl:X4}";
        }, "IRQ aucune, sortie 0000", "IRQ aux tics 4, sortie DEF0");
        bad += Un("SB 16 : 81h=02h, puis B8h 10h 03h 00h sur le canal 1, à l'entrée", d =>
        {
            MelEcrit(0x224, 0x81, 0x02);
            Dma1(0x10000, 0x07, false, entree: true);
            Dsp(0xb8, 0x10, 0x03, 0x00);
            return TicsDma(d, 12, seize: true, entree: true);
        }, "IRQ aucune", "IRQ aux tics 4");
        bad += Un("SB 16 : 81h=02h, puis le reset du mélangeur", _ =>
        {
            MelEcrit(0x224, 0x81, 0x02);
            MelEcrit(0x224, 0x00, 0x00);
            return MelLit(0x224, 0x81);
        }, "81h=22", "81h=02");
        bad += Un("SB 16 : 81h=02h, 81h=22h, puis B0h 10h 03h 00h sur le canal 5 (témoin)", d =>
        {
            Memoire();
            mem.mem_writeb_phys(0x20000, 0x34);
            mem.mem_writeb_phys(0x20001, 0x12);
            MelEcrit(0x224, 0x81, 0x02);
            MelEcrit(0x224, 0x81, 0x22);
            Dma5(0x20000, 0x03, false);
            Dsp(0xb0, 0x10, 0x03, 0x00);
            return $"{MelLit(0x224, 0x81)} {TicsDma(d, 12, seize: true)}";
        }, "", "81h=22 IRQ aux tics 4", temoin: true);
        bad += Un("SB 16 : 81h=02h, puis le reset du DSP", _ =>
        {
            MelEcrit(0x224, 0x81, 0x02);
            ResetDsp();
            LireDsp();
            return MelLit(0x224, 0x81);
        }, "81h=22", "81h=02");
        bad += Un("SB 16 : 81h=02h, puis B0h 30h 03h 00h sur le canal 1, en stéréo", d =>
        {
            Memoire();
            MelEcrit(0x224, 0x81, 0x02);
            Dma1(0x10000, 0x07, false);
            Dsp(0xb0, 0x30, 0x03, 0x00);
            return $"{TicsDma(d, 12, seize: true)}, sortie {(ushort)d.sbdatl:X4} {(ushort)d.sbdatr:X4}";
        }, "IRQ aucune, sortie 0000 0000", "IRQ aux tics 2, sortie 9ABC DEF0");
        // B6h traduit (huit mots en automatique), puis B0h au 3e tic : avec PB-239, en file — l'IRQ à la fin du bloc,
        // puis à celle de la file ; sans lui, B0h relance, comme chez PCem.
        bad += Un("SB 16 : 81h=02h, B6h 10h 07h 00h, puis B0h 10h 03h 00h au 3e tic", d =>
        {
            Memoire();
            MelEcrit(0x224, 0x81, 0x02);
            Dma1(0x10000, 0x0f, true);
            Dsp(0xb6, 0x10, 0x07, 0x00);
            return TicsDma(d, 30, new() { [3] = [[0xb0, 0x10, 0x03, 0x00]] }, seize: true);
        }, "IRQ aucune", materiel.pb_239 ? "IRQ aux tics 8 12" : "IRQ aux tics 6");
        bad += Un("SB 16 : 81h=02h, puis 81h=E2h (témoin)", _ =>
        {
            MelEcrit(0x224, 0x81, 0x02);
            MelEcrit(0x224, 0x81, 0xe2);
            return MelLit(0x224, 0x81);
        }, "", "81h=82", temoin: true);
        bad += Un("SB 16 : 30h au démarrage, selon PB-237 (témoin)", _ => MelLit(0x224, 0x30),
                  "", materiel.pb_237 ? "30h=C0" : "30h=F8", temoin: true);
        bad += Un("SB 16 : 81h=0Ah, deux canaux 8 bits (témoin)", _ =>
        {
            MelEcrit(0x224, 0x81, 0x0a);
            return MelLit(0x224, 0x81);
        }, "", "81h=28", temoin: true);
        // 0Ah, le micro de la Pro, recopié en 3Ah (G13.8e1) : à l'échelle du guide, ((0Ah & 7) × 3 + 10) << 3 ; relu
        // ((3Ah >> 3) − 10) / 3, borné à 0..7. PCem : 0Ah × 3 + 10, relu (3Ah − 10) / 3 (FDh au démarrage).
        bad += Un("SB 16 : 0Ah et 3Ah au démarrage", _ => MelLit(0x224, 0x0a, 0x3a), "0Ah=FD 3Ah=00", "0Ah=00 3Ah=00");
        foreach (var (r, v, pcem, mat) in new[]
                 {
                     (0x0a, 0x07, "0Ah=07 3Ah=1F", "0Ah=07 3Ah=F8"), (0x0a, 0xff, "0Ah=FF 3Ah=07", "0Ah=07 3Ah=F8"),
                     (0x3a, 0xa8, "0Ah=34 3Ah=A8", "0Ah=03 3Ah=A8"),
                 })
            bad += Un($"SB 16 : {r:X2}h={v:X2}h, puis 0Ah et 3Ah relus", _ =>
            {
                MelEcrit(0x224, r, v);
                return MelLit(0x224, 0x0a, 0x3a);
            }, pcem, mat);
        return bad;
    }
}
