// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — outillage de vérification.
//
// Les cas des cavaliers des SB 1.x, 2.0 et Pro (G13.8d3b, PLAN-G13.md, Q24) : PB-290 (le DMA 1 seul des 1.x et 2.0,
// l'IRQ 5 d'usine de la 2.0) et PB-291 (le DMA 0 de la Pro), sur le banc du son (MaterielCasSon.cs). Chaque cas relit
// l'IRQ et le DMA que l'init a posés ; ceux d'un DMA refusé ou ajouté jouent aussi 14h 07h 00h (huit octets en simple
// cycle) sur le canal que la carte doit servir, programmé seul : le DSP qui lit un autre canal n'a aucun octet et ne lève
// pas l'IRQ.

using iXtal26.Memory;
using iXtal26.Sound;

namespace iXtal26.Diff;

internal static partial class MaterielCas
{
    /// <summary>Un canal du 8237 bas, seul démasqué : <paramref name="compte"/> + 1 octets depuis
    /// <paramref name="adresse"/>, en lecture de la mémoire, simple ; le canal 4 en cascade, comme Dma1.</summary>
    private static void DmaBas(int canal, uint adresse, int compte)
    {
        ushort[] page = [0x87, 0x83, 0x81, 0x82];
        io.outb(0xd6, 0xc0);
        io.outb(0xd4, 0x00);
        io.outb(0x0f, 0x0f);
        io.outb(0x0c, 0);
        io.outb(0x0b, (byte)(0x48 | canal));
        io.outb((ushort)(canal * 2), (byte)adresse);
        io.outb((ushort)(canal * 2), (byte)(adresse >> 8));
        io.outb(page[canal], (byte)(adresse >> 16));
        io.outb((ushort)(canal * 2 + 1), (byte)compte);
        io.outb((ushort)(canal * 2 + 1), (byte)(compte >> 8));
        io.outb(0x0a, (byte)canal);
    }

    /// <summary>L'IRQ et le DMA posés par l'init, puis, si <paramref name="canal"/> est donné, 14h 07h 00h sur ce canal
    /// et les tics de l'IRQ.</summary>
    private static string Cavaliers(sb_dsp_t d, int canal = -1)
    {
        var lu = $"IRQ {d.sb_irqnum}, DMA {d.sb_8_dmanum}";
        if (canal < 0)
            return lu;
        for (var i = 0; i < 8; i++)
            mem.mem_writeb_phys(0x10000 + (uint)i, (byte)(0x80 + 0x10 * i));
        DmaBas(canal, 0x10000, 0x07);
        Dsp(0x14, 0x07, 0x00);
        return $"{lu}, {TicsDma(d, 12)}";
    }

    private static int UnCavalier(bool hw, string nom, string carte, string section, int canal, string pcem, string mat,
                                  bool temoin = false) =>
        AvecSb(carte, d =>
        {
            var v = Cavaliers(d, canal);
            var att = temoin || hw ? mat : pcem;
            return Voir(nom, v == att, v, att, hw);
        }, section);

    // PB-290 — les SB 1.0, 1.5 et 2.0 n'ont que le DMA 1 (URM de la 1.5, p. 63-64 ; guide de la 2.0, p. 1-6 et B-4) ;
    // la 2.0 sort d'usine à l'IRQ 5 (p. 1-6). dma = 3 : PCem prend le canal 3, la carte refuse la clé (le DMA 1) ; la
    // 2.0 sans clé : IRQ 7 chez PCem, 5 sur la carte, de même hors de la liste. Témoins, deux réglages permis partout.
    private static int Pb290(bool hw)
    {
        const string V10 = "[Sound Blaster v1.0]\n", V15 = "[Sound Blaster v1.5]\n", V20 = "[Sound Blaster v2.0]\n";
        var bad = UnCavalier(hw, "SB 1.0, dma = 3 : puis 14h 07h 00h sur le canal 1", "sb", V10 + "dma = 3\n", 1,
                             "IRQ 7, DMA 3, IRQ aucune", "IRQ 7, DMA 1, IRQ aux tics 8");
        bad += UnCavalier(hw, "SB 1.5, dma = 3 : puis 14h 07h 00h sur le canal 1", "sb1.5", V15 + "dma = 3\n", 1,
                          "IRQ 7, DMA 3, IRQ aucune", "IRQ 7, DMA 1, IRQ aux tics 8");
        bad += UnCavalier(hw, "SB 2.0 sans clé", "sb2.0", "", -1, "IRQ 7, DMA 1", "IRQ 5, DMA 1");
        bad += UnCavalier(hw, "SB 2.0, irq = 10 (hors de la liste)", "sb2.0", V20 + "irq = 10\n", -1, "IRQ 7, DMA 1",
                          "IRQ 5, DMA 1");
        bad += UnCavalier(hw, "SB 2.0, irq = 7, dma = 3 : puis 14h 07h 00h sur le canal 1", "sb2.0",
                          V20 + "irq = 7\ndma = 3\n", 1, "IRQ 7, DMA 3, IRQ aucune", "IRQ 7, DMA 1, IRQ aux tics 8");
        bad += UnCavalier(hw, "SB 2.0, irq = 3, dma = 1 (témoin) : puis 14h 07h 00h sur le canal 1", "sb2.0",
                          V20 + "irq = 3\ndma = 1\n", 1, "", "IRQ 3, DMA 1, IRQ aux tics 8", temoin: true);
        bad += UnCavalier(hw, "SB 1.0, irq = 2 (témoin)", "sb", V10 + "irq = 2\n", -1, "", "IRQ 2, DMA 1",
                          temoin: true);
        return bad;
    }

    // PB-291 — la SB Pro règle son DMA sur 0, 1 ou 3 (Getting Started, CSBPP-002, p. 6 et 8) ; PCem n'offre que 1 et 3.
    // dma = 0 : PCem refuse la clé (le DMA 1), la carte prend le canal 0. Témoins : dma = 3, et dma = 2, refusé partout.
    private static int Pb291(bool hw)
    {
        const string P1 = "[Sound Blaster Pro v1]\n", P2 = "[Sound Blaster Pro v2]\n";
        var bad = UnCavalier(hw, "Pro v2, dma = 0 : puis 14h 07h 00h sur le canal 0", "sbprov2", P2 + "dma = 0\n", 0,
                             "IRQ 7, DMA 1, IRQ aucune", "IRQ 7, DMA 0, IRQ aux tics 8");
        bad += UnCavalier(hw, "Pro v1, dma = 0 : puis 14h 07h 00h sur le canal 0", "sbprov1", P1 + "dma = 0\n", 0,
                          "IRQ 7, DMA 1, IRQ aucune", "IRQ 7, DMA 0, IRQ aux tics 8");
        bad += UnCavalier(hw, "Pro v2, dma = 3 (témoin) : puis 14h 07h 00h sur le canal 3", "sbprov2",
                          P2 + "dma = 3\n", 3, "", "IRQ 7, DMA 3, IRQ aux tics 8", temoin: true);
        bad += UnCavalier(hw, "Pro v2, dma = 2, hors des deux listes (témoin)", "sbprov2", P2 + "dma = 2\n", -1, "",
                          "IRQ 7, DMA 1", temoin: true);
        return bad;
    }
}
