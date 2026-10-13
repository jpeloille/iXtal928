// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — outillage de vérification.
//
// Les cas des commandes de DMA du DSP (G13.8c1, PLAN-G13.md) : PB-149 (l'octet de mode de B0h à CFh) et PB-239 (la
// commande mise en file), sur le banc du son (MaterielCasSon.cs). Un scénario : le 8237 (le canal 1, ou le canal 5
// pour le 16 bits), des commandes, puis des tics joués à la main, dont certains précédés d'une commande ; il rend les
// tics qui lèvent l'IRQ (acquittée aussitôt, 2xEh en 8 bits, 2xFh en 16 bits).

using iXtal26.Memory;
using iXtal26.Sound;

namespace iXtal26.Diff;

internal static partial class MaterielCas
{
    /// <summary>Le canal 5 du 8237 haut : <paramref name="compte"/> + 1 mots depuis <paramref name="adresse"/>, en
    /// lecture de la mémoire, simple ou automatique ; le canal 4 en cascade, comme Dma1.</summary>
    private static void Dma5(uint adresse, int compte, bool auto)
    {
        io.outb(0xd6, 0xc0);
        io.outb(0xd4, 0x00);
        io.outb(0xd4, 0x05);
        io.outb(0xd8, 0);
        io.outb(0xd6, (byte)(0x49 | (auto ? 0x10 : 0)));
        io.outb(0xc4, (byte)(adresse >> 1));
        io.outb(0xc4, (byte)(adresse >> 9));
        io.outb(0x8b, (byte)((adresse >> 16) & 0xfe));
        io.outb(0xc6, (byte)compte);
        io.outb(0xc6, (byte)(compte >> 8));
        io.outb(0xd4, 0x01);
    }

    /// <summary><paramref name="tics"/> tics de sortie (ou d'entrée, <paramref name="entree"/>), chacun joué si sa
    /// minuterie tourne ; avant le tic t, les commandes de <paramref name="avant"/>[t] (un tableau vide : un reset). Rend
    /// les tics de l'IRQ 8 ou 16 bits.</summary>
    private static string TicsDma(sb_dsp_t d, int tics, Dictionary<int, int[][]>? avant = null, bool seize = false,
                                  bool entree = false)
    {
        var irq = new List<string>();
        for (var t = 1; t <= tics; t++)
        {
            if (avant != null && avant.TryGetValue(t, out var cs))
                foreach (var c in cs)
                {
                    if (c.Length == 0)
                    {
                        ResetDsp();
                        LireDsp();
                    }
                    else
                        Dsp(c);
                }
            // Le tic ne vient que si sa minuterie tourne, comme sur la machine (pollsb la relancerait).
            if (entree)
            {
                if (timer.timer_is_enabled(d.input_timer) != 0)
                    sound_sb_dsp.sb_poll_i(d);
            }
            else if (timer.timer_is_enabled(d.output_timer) != 0)
                sound_sb_dsp.pollsb(d);
            if ((seize ? d.sb_irq16 : d.sb_irq8) == 0)
                continue;
            irq.Add(t.ToString());
            io.inb((ushort)(Sb + (seize ? 0xf : 0xe)));
        }
        return irq.Count == 0 ? "IRQ aucune" : $"IRQ aux tics {string.Join(" ", irq)}";
    }

    // PB-149 — B0h à CFh (DSP 4.xx) ne lisent que les bits 4 et 5 de l'octet de mode (guide, p. 6-23 ; 4.05
    // `cmd_dma8`, `cmd_dma16`). SB 16, 16 octets en simple cycle : C0h 04h 0Fh 00h (le bit 2 réservé) — PCem n'a pas de
    // case, rien ne bouge ; la carte joue 16 octets et lève l'IRQ au 16e tic. C0h 01h : de l'ADPCM 4 bits chez PCem (deux
    // tics par octet), du PCM sur la carte. B0h 14h, en 16 bits sur le canal 5 ; C8h 04h, à l'entrée. Témoin : C0h 10h.
    private static int Pb149(bool hw)
    {
        int Un(string nom, Func<sb_dsp_t, string> corps, string pcem, string mat, bool temoin = false) =>
            AvecSb("sb16", d =>
            {
                var v = corps(d);
                var att = temoin || hw ? mat : pcem;
                return Voir(nom, v == att, v, att, hw);
            });
        var bad = Un("SB 16 : C0h 04h 0Fh 00h", d => { Dma1(0x10000, 0x0f, false); Dsp(0xc0, 0x04, 0x0f, 0x00); return TicsDma(d, 40); },
                     "IRQ aucune", "IRQ aux tics 16");
        bad += Un("SB 16 : C0h 01h 0Fh 00h", d => { Dma1(0x10000, 0x0f, false); Dsp(0xc0, 0x01, 0x0f, 0x00); return TicsDma(d, 40); },
                  "IRQ aux tics 32", "IRQ aux tics 16");
        bad += Un("SB 16 : B0h 14h 0Fh 00h, en 16 bits", d => { Dma5(0x20000, 0x0f, false); Dsp(0xb0, 0x14, 0x0f, 0x00); return TicsDma(d, 40, seize: true); },
                  "IRQ aucune", "IRQ aux tics 16");
        bad += Un("SB 16 : C8h 04h 0Fh 00h, à l'entrée", d => { Dma1(0x10000, 0x0f, false, entree: true); Dsp(0xc8, 0x04, 0x0f, 0x00); return TicsDma(d, 40, entree: true); },
                  "IRQ aucune", "IRQ aux tics 16");
        bad += Un("SB 16 : C0h 10h 0Fh 00h (témoin)", d => { Dma1(0x10000, 0x0f, false); Dsp(0xc0, 0x10, 0x0f, 0x00); return TicsDma(d, 40); },
                  "IRQ aux tics 16", "IRQ aux tics 16", temoin: true);
        return bad;
    }

    // PB-239 — une sortie en simple cycle reçue pendant un transfert va en file : le bloc en cours finit (l'IRQ), puis
    // le bloc en file, dans le format en cours, sans automatique (guide, p. 6-8 ; 2.02 et 3.02 pendant tout transfert,
    // 4.05 pendant un automatique). 48h FFh 00h, 1Ch, puis 14h 0Fh 00h au 17e tic : PCem relance (l'IRQ au 32e tic) ;
    // la carte, au 256e puis au 272e (SB 2.0 et SB 16). 14h 3Fh 00h, puis 14h 0Fh 00h au 17e : la SB 2.0 au 64e et au
    // 80e ; la SB 16 relance comme PCem (le témoin). Sur la SB 16, C6h puis C0h 10h (signé) : le bloc en file reste non
    // signé (la sortie d'un octet 00h, 8000h) ; B6h puis B0h, en 16 bits. En ADPCM (SB 2.0, 74h 1Fh 00h, puis 74h 07h
    // 00h au 21e tic), les attendus composés avec ceux de 8b (MaterielCasAdpcm.cs). Témoin : la file effacée par un
    // reset (14h, 14h en file, un reset, puis 48h 07h 00h et 1Ch, qui ne passe pas par la file : l'IRQ tous les 8 tics
    // des deux côtés). D0h puis 14h : la pause relâche la broche du 2.02, la commande part de neuf (le témoin) ; sur la
    // SB 16, elle va en file, et le bloc repris par D4h finit avant elle. DAh puis 14h, ou 14h puis DAh, sur la SB 2.0
    // : le bloc finit le transfert, file comprise (256 contre 32). Deux C0h sur la SB 16 : la première en file efface
    // l'automatique, la seconde part de neuf (le témoin) ; et le transfert arrêté par un reset, qui ne met pas en file
    // la commande suivante (PCem laisse sb_8_enable à 1 : l'IRQ au 28e tic des deux côtés).
    private static int Pb239(bool hw)
    {
        int Un(string carte, string nom, Func<sb_dsp_t, string> corps, string pcem, string mat, bool temoin = false) =>
            AvecSb(carte, d =>
            {
                var v = corps(d);
                var att = temoin || hw ? mat : pcem;
                return Voir(nom, v == att, v, att, hw);
            });
        var file = new Dictionary<int, int[][]> { [17] = [[0x14, 0x0f, 0x00]] };
        var bad = 0;
        foreach (var carte in new[] { "sb2.0", "sb16" })
            bad += Un(carte, $"{carte} : 48h FFh 00h, 1Ch, puis 14h 0Fh 00h", d =>
            {
                Dma1(0x10000, 0xffff, true);
                Dsp(0x48, 0xff, 0x00);
                Dsp(0x1c);
                return TicsDma(d, 300, file);
            }, "IRQ aux tics 32", "IRQ aux tics 256 272");
        bad += Un("sb2.0", "SB 2.0 : 14h 3Fh 00h, puis 14h 0Fh 00h", d =>
        {
            Dma1(0x10000, 0xffff, true);
            Dsp(0x14, 0x3f, 0x00);
            return TicsDma(d, 100, file);
        }, "IRQ aux tics 32", "IRQ aux tics 64 80");
        bad += Un("sb16", "SB 16 : 14h 3Fh 00h, puis 14h 0Fh 00h (témoin)", d =>
        {
            Dma1(0x10000, 0xffff, true);
            Dsp(0x14, 0x3f, 0x00);
            return TicsDma(d, 100, file);
        }, "IRQ aux tics 32", "IRQ aux tics 32", temoin: true);
        bad += Un("sb16", "SB 16 : C6h 00h FFh 00h, puis C0h 10h 0Fh 00h", d =>
        {
            Dma1(0x10000, 0xffff, true);
            Dsp(0xc6, 0x00, 0xff, 0x00);
            var v = TicsDma(d, 300, new() { [17] = [[0xc0, 0x10, 0x0f, 0x00]] });
            return $"{v}, sortie {(ushort)d.sbdatl:X4}h";
        }, "IRQ aux tics 32, sortie 0000h", "IRQ aux tics 256 272, sortie 8000h");
        bad += Un("sb16", "SB 16 : B6h 10h FFh 00h, puis B0h 10h 0Fh 00h, en 16 bits", d =>
        {
            Dma5(0x20000, 0xffff, true);
            Dsp(0xb6, 0x10, 0xff, 0x00);
            return TicsDma(d, 300, new() { [17] = [[0xb0, 0x10, 0x0f, 0x00]] }, seize: true);
        }, "IRQ aux tics 32", "IRQ aux tics 256 272");
        bad += AvecSb("sb2.0", d =>
        {
            var donnees = new byte[161];
            donnees[0] = 0x80;
            for (var i = 1; i < donnees.Length; i++)
                donnees[i] = new byte[] { 0x17, 0x71, 0x35, 0x53 }[(i - 1) % 4];
            for (var i = 0; i < donnees.Length; i++)
                mem.mem_writeb_phys(0x10000 + (uint)i, donnees[i]);
            Dma1(0x10000, 0xff, false);
            Dsp(0x74, 0x1f, 0x00);
            var v = $"{TicsDma(d, 139, new() { [21] = [[0x74, 0x07, 0x00]] })}, {0xff - Compte1()} octets pris";
            var autres = PbAdpcm.Count(p => ModeMateriel.Demande(p));
            var cle = autres switch { 0 => hw ? "seul" : "pcem", 6 => hw ? "tout" : "tout-sauf", _ => $"{autres} drapeaux de l'ADPCM" };
            var att = new Dictionary<string, string>
            {
                ["pcem"] = "IRQ aux tics 34, 19 octets pris",
                ["seul"] = "IRQ aux tics 62 78, 40 octets pris",
                ["tout"] = "IRQ aux tics 66 82, 40 octets pris",
                ["tout-sauf"] = "IRQ aux tics 38, 18 octets pris",
            }.GetValueOrDefault(cle, $"(pas d'attendu pour {cle})");
            return Voir("SB 2.0 : 74h 1Fh 00h, puis 74h 07h 00h au 21e tic", v == att, v, att, hw);
        });
        bad += Un("sb2.0", "SB 2.0 : 14h, 14h en file, un reset, puis 48h 07h 00h et 1Ch (témoin)", d =>
        {
            Dma1(0x10000, 0xffff, true);
            Dsp(0x14, 0x3f, 0x00);
            return TicsDma(d, 60, new() { [17] = [[0x14, 0x0f, 0x00]], [20] = [[]], [21] = [[0x48, 0x07, 0x00], [0x1c]] });
        }, "IRQ aux tics 28 36 44 52 60", "IRQ aux tics 28 36 44 52 60", temoin: true);
        bad += Un("sb2.0", "SB 2.0 : 48h FFh 00h, 1Ch, puis D0h et 14h 0Fh 00h (témoin)", d =>
        {
            Dma1(0x10000, 0xffff, true);
            Dsp(0x48, 0xff, 0x00);
            Dsp(0x1c);
            return TicsDma(d, 300, new() { [17] = [[0xd0], [0x14, 0x0f, 0x00]] });
        }, "IRQ aux tics 32", "IRQ aux tics 32", temoin: true);
        bad += Un("sb16", "SB 16 : 48h FFh 00h, 1Ch, D0h et 14h 0Fh 00h, puis D4h", d =>
        {
            Dma1(0x10000, 0xffff, true);
            Dsp(0x48, 0xff, 0x00);
            Dsp(0x1c);
            return TicsDma(d, 320, new() { [17] = [[0xd0], [0x14, 0x0f, 0x00]], [30] = [[0xd4]] });
        }, "IRQ aux tics 32", "IRQ aux tics 269 285");
        bad += Un("sb2.0", "SB 2.0 : 48h FFh 00h, 1Ch, puis DAh et 14h 0Fh 00h", d =>
        {
            Dma1(0x10000, 0xffff, true);
            Dsp(0x48, 0xff, 0x00);
            Dsp(0x1c);
            return TicsDma(d, 300, new() { [17] = [[0xda], [0x14, 0x0f, 0x00]] });
        }, "IRQ aux tics 32", "IRQ aux tics 256");
        bad += Un("sb2.0", "SB 2.0 : 48h FFh 00h, 1Ch, 14h 0Fh 00h au 17e tic, puis DAh au 18e", d =>
        {
            Dma1(0x10000, 0xffff, true);
            Dsp(0x48, 0xff, 0x00);
            Dsp(0x1c);
            return TicsDma(d, 300, new() { [17] = [[0x14, 0x0f, 0x00]], [18] = [[0xda]] });
        }, "IRQ aux tics 32", "IRQ aux tics 256");
        bad += Un("sb16", "SB 16 : C6h, puis C0h au 17e tic et C0h 07h au 20e (témoin)", d =>
        {
            Dma1(0x10000, 0xffff, true);
            Dsp(0xc6, 0x00, 0xff, 0x00);
            return TicsDma(d, 300, new() { [17] = [[0xc0, 0x00, 0x0f, 0x00]], [20] = [[0xc0, 0x00, 0x07, 0x00]] });
        }, "IRQ aux tics 27", "IRQ aux tics 27", temoin: true);
        bad += Un("sb2.0", "SB 2.0 : 14h, un reset, puis 14h 07h 00h (témoin)", d =>
        {
            Dma1(0x10000, 0xffff, true);
            Dsp(0x14, 0x3f, 0x00);
            return TicsDma(d, 60, new() { [20] = [[]], [21] = [[0x14, 0x07, 0x00]] });
        }, "IRQ aux tics 28", "IRQ aux tics 28", temoin: true);
        return bad;
    }
}
