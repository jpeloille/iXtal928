// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — outillage de vérification.
//
// Les cas de l'ADPCM (G13.8b, PLAN-G13.md) : PB-90, PB-91, PB-146, PB-234, PB-235, PB-284, sur le banc du son
// (MaterielCasSon.cs). Un scénario : des octets en mémoire à 10000h, le canal 1 du 8237, des commandes au DSP, puis
// des tics de sortie joués à la main ; il rend `sbref` après chaque tic, les tics qui lèvent l'IRQ 8 bits (acquittée
// aussitôt par 2xEh) et le compte restant du 8237.
//
// Ces corrections se composent (contre-lecture K6) : l'attendu d'un scénario dépend des autres drapeaux de l'ADPCM.
// Quatre attendus par scénario, tirés de deux modèles au tic tenus hors du dépôt (VERIFICATION.md § G13.8b) : « pcem »
// (aucun drapeau), « seul » (le PB du cas seul), « tout » (les six), « tout-sauf » (les six moins celui du cas, pour
// l'attendu inversé sous « tout »). « tout » est aussi celui du vecteur du micrologiciel 2.02, transcrit à part du code
// machine ; les deux modèles s'accordent sur 4 000 scénarios tirés au hasard. Une autre combinaison des six drapeaux
// n'a pas d'attendu : le cas échoue.

using iXtal26.Memory;
using iXtal26.Sound;

namespace iXtal26.Diff;

internal static partial class MaterielCas
{
    private static readonly int[] PbAdpcm = [90, 91, 146, 234, 235, 284];

    /// <summary>La clé de l'attendu : le drapeau du cas pris à <paramref name="hw"/>, les cinq autres tels que
    /// demandés.</summary>
    private static string CleAdpcm(int pb, bool hw)
    {
        var autres = PbAdpcm.Count(p => p != pb && ModeMateriel.Demande(p));
        return autres switch
        {
            0 => hw ? "seul" : "pcem",
            5 => hw ? "tout" : "tout-sauf",
            _ => $"{autres} autres drapeaux de l'ADPCM",
        };
    }

    private static int VoirAdpcm(int pb, bool hw, string nom, string vu, Dictionary<string, string> attendus)
    {
        var cle = CleAdpcm(pb, hw);
        var att = attendus.TryGetValue(cle, out var a) ? a : $"(pas d'attendu pour {cle})";
        return Voir(nom, vu == att, vu, att, hw);
    }

    /// <summary>Le scénario : <paramref name="octets"/> à 10000h, le canal 1 sur <paramref name="compte"/> + 1 octets,
    /// masqué si <paramref name="masque"/> et démasqué avant le tic <paramref name="demasque"/> ; les commandes ; puis
    /// <paramref name="tics"/> tics de sortie, ou, à 0, les tics jusqu'à la première IRQ.</summary>
    private static string Joue(sb_dsp_t d, byte[] octets, int compte, bool auto, int[][] commandes, int tics,
                               bool masque = false, int demasque = 0)
    {
        for (var i = 0; i < octets.Length; i++)
            mem.mem_writeb_phys(0x10000 + (uint)i, octets[i]);
        Dma1(0x10000, compte, auto);
        if (masque)
            io.outb(0x0a, 0x05);
        foreach (var c in commandes)
            Dsp(c);
        if (tics == 0)
        {
            for (var t = 1; t < 0x1000; t++)
            {
                sound_sb_dsp.pollsb(d);
                if (d.sb_irq8 != 0)
                    return $"IRQ au tic {t}, {compte - Compte1():X} octets pris";
            }
            return "aucune IRQ";
        }
        var sorties = new List<string>();
        var irq = new List<string>();
        for (var t = 1; t <= tics; t++)
        {
            if (t == demasque)
                io.outb(0x0a, 0x01);
            sound_sb_dsp.pollsb(d);
            sorties.Add(d.sbref.ToString("X2"));
            if (d.sb_irq8 == 0)
                continue;
            irq.Add(t.ToString());
            io.inb(Sb + 0xe);
        }
        return $"sorties {string.Join(" ", sorties)} ; IRQ {(irq.Count == 0 ? "aucune" : string.Join(" ", irq))} ; " +
               $"compte {Compte1():X4}";
    }

    /// <summary>Le compte courant du canal 1 du 8237 (03h, octet faible puis fort).</summary>
    private static int Compte1()
    {
        io.outb(0x0c, 0);
        var lo = io.inb(0x03);
        return lo | (io.inb(0x03) << 8);
    }

    // PB-90 — le terme du compte sort sur la broche EOP du 8237, pas dans l'octet ; PCem le range dans sbdat2
    // (DMA_OVER). SB 2.0, le canal 1 en automatique sur 4 octets (80h, 00h, 00h, 00h) ; 75h 07h 00h : au premier
    // échantillon du 4e octet, le terme, PCem fait passer `sbref` de 80h à 44h ; sans le drapeau, 80h jusqu'au bout.
    private static int Pb90(bool hw) => AvecSb("sb2.0", d => VoirAdpcm(90, hw, "SB 2.0 : 75h 07h 00h, le terme au 4e octet",
        Joue(d, [0x80, 0x00, 0x00, 0x00], 3, true, [[0x75, 0x07, 0x00]], 16), new()
        {
            ["pcem"] = "sorties 80 80 80 80 44 44 44 44 44 44 44 44 44 44 44 44 ; IRQ 12 ; compte 0003",
            ["seul"] = "sorties 80 80 80 80 80 80 80 80 80 80 80 80 80 80 80 80 ; IRQ 12 ; compte 0003",
            ["tout"] = "sorties 80 80 80 80 80 80 80 80 80 80 80 80 80 80 80 80 ; IRQ 15 ; compte 0003",
            ["tout-sauf"] = "sorties 80 80 80 80 44 44 44 44 44 44 44 44 08 08 08 08 ; IRQ 15 ; compte 0003",
        }));

    // PB-91 — l'ADPCM 2 bits ne décrémente pas son compte chez PCem : 16h 03h 00h ne finit jamais ; la carte lève
    // l'IRQ après le 4e octet (au 12e tic chez PCem corrigé, au 16e avec la fin de bloc du DSP, PB-234). En
    // automatique (48h 03h 00h, puis 1Fh), la carte recharge tous les 4 octets ; PCem jamais.
    private static int Pb91(bool hw)
    {
        var bad = AvecSb("sb2.0", d => VoirAdpcm(91, hw, "SB 2.0 : 16h 03h 00h",
            Joue(d, new byte[4], 3, false, [[0x16, 0x03, 0x00]], 20), new()
            {
                ["pcem"] = "sorties 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 ; IRQ aucune ; compte FFFF",
                ["seul"] = "sorties 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 ; IRQ 12 ; compte FFFF",
                ["tout"] = "sorties 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 ; IRQ 16 ; compte FFFF",
                ["tout-sauf"] = "sorties 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 ; IRQ aucune ; compte FFFF",
            }));
        bad += AvecSb("sb2.0", d => VoirAdpcm(91, hw, "SB 2.0 : 48h 03h 00h, puis 1Fh",
            Joue(d, new byte[8], 3, true, [[0x48, 0x03, 0x00], [0x1f]], 36), new()
            {
                ["pcem"] = "sorties 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 ; IRQ aucune ; compte 0001",
                ["seul"] = "sorties 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 ; IRQ 12 28 ; compte 0001",
                ["tout"] = "sorties 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 ; IRQ 13 29 ; compte 0001",
                ["tout-sauf"] = "sorties 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 ; IRQ aucune ; compte 0001",
            }));
        return bad;
    }

    // PB-146 — 1Fh, 2Ch, 7Dh et 7Fh n'ont pas de paramètre : le bloc de 48h, et pour 1Fh, 7Dh et 7Fh l'octet de
    // référence. SB 2.0, le canal 1 en automatique sur 1000h octets : 48h 7Fh 01h, 40h A5h, puis 7Dh : PCem prend
    // sb_data (A5h 01h) et lève l'IRQ après 1A6h octets ; la carte après 180h (181h avec la fin de bloc du DSP,
    // PB-234). Témoin : 48h 7Fh 01h puis 7Dh, où les octets « périmés » sont ceux de 48h (SBBANC). 2Ch, l'entrée :
    // 48h 0Fh 00h, 40h A5h, 2Ch : l'IRQ au 10h-ième octet écrit, A6h chez PCem.
    private static int Pb146(bool hw)
    {
        var donnees = new byte[0x1001];
        donnees[0] = 0x80;
        for (var i = 1; i < donnees.Length; i++)
            donnees[i] = 0x11;
        var bad = AvecSb("sb2.0", d => VoirAdpcm(146, hw, "SB 2.0 : 48h 7Fh 01h, 40h A5h, puis 7Dh",
            Joue(d, donnees, 0xfff, true, [[0x48, 0x7f, 0x01], [0x40, 0xa5], [0x7d]], 0), new()
            {
                ["pcem"] = "IRQ au tic 842, 1A6 octets pris",
                ["seul"] = "IRQ au tic 764, 180 octets pris",
                ["tout"] = "IRQ au tic 767, 181 octets pris",
                ["tout-sauf"] = "IRQ au tic 846, 1A7 octets pris",
            }));
        bad += AvecSb("sb2.0", d => VoirAdpcm(146, hw, "SB 2.0 : 48h 7Fh 01h, puis 7Dh (témoin)",
            Joue(d, donnees, 0xfff, true, [[0x48, 0x7f, 0x01], [0x7d]], 0), new()
            {
                ["pcem"] = "IRQ au tic 766, 180 octets pris",
                ["seul"] = "IRQ au tic 764, 180 octets pris",
                ["tout"] = "IRQ au tic 767, 181 octets pris",
                ["tout-sauf"] = "IRQ au tic 770, 181 octets pris",
            }));
        bad += AvecSb("sb2.0", d =>
        {
            Dma1(0x10000, 0xff, auto: true, entree: true);
            Dsp(0x48, 0x0f, 0x00);
            Dsp(0x40, 0xa5);
            Dsp(0x2c);
            var t = 0;
            for (var i = 1; i <= 0x200 && t == 0; i++)
            {
                sound_sb_dsp.sb_poll_i(d);
                if (d.sb_irq8 != 0)
                    t = i;
            }
            var v = $"IRQ au {t}e octet écrit";
            var att = hw ? "IRQ au 16e octet écrit" : "IRQ au 166e octet écrit";
            return Voir("SB 2.0 : 48h 0Fh 00h, 40h A5h, puis 2Ch", v == att, v, att, hw);
        });
        return bad;
    }

    // PB-234 — le bloc ADPCM tic par tic, comme le DSP. SB 2.0, 75h 03h 00h sur 80h, 77h, 77h, 77h : PCem lève l'IRQ au
    // 4e tic, à la lecture du 4e octet, après 4 échantillons ; le DSP joue les 3 octets de données, 6 échantillons,
    // et lève l'IRQ au 7e. 74h 01h 00h sur 17h, 70h, sans référence : le DSP tire 3 échantillons du premier octet
    // (haut, bas, haut), PCem 2. 48h 01h 00h, puis 7Dh, en automatique : au tic qui recharge le bloc, le DSP lit
    // l'octet sans le décoder, et l'IRQ vient après le dernier échantillon du bloc.
    private static int Pb234(bool hw)
    {
        var bad = AvecSb("sb2.0", d => VoirAdpcm(234, hw, "SB 2.0 : 75h 03h 00h, la fin du bloc",
            Joue(d, [0x80, 0x77, 0x77, 0x77], 15, false, [[0x75, 0x03, 0x00]], 9), new()
            {
                ["pcem"] = "sorties 87 96 B4 F0 F0 F0 F0 F0 F0 ; IRQ 4 ; compte 000B",
                ["seul"] = "sorties 87 96 B4 F0 FF FF FF FF FF ; IRQ 7 ; compte 000B",
                ["tout"] = "sorties 87 96 B4 F0 FF FF FF FF FF ; IRQ 7 ; compte 000B",
                ["tout-sauf"] = "sorties 87 96 B4 F0 F0 F0 F0 F0 F0 ; IRQ 4 ; compte 000B",
            }));
        bad += AvecSb("sb2.0", d => VoirAdpcm(234, hw, "SB 2.0 : 74h 01h 00h, le premier octet sans référence",
            Joue(d, [0x17, 0x70], 15, false, [[0x74, 0x01, 0x00]], 7), new()
            {
                ["pcem"] = "sorties 01 08 08 08 08 08 08 ; IRQ 2 ; compte 000D",
                ["seul"] = "sorties 01 08 0B 1A 1C 1C 1C ; IRQ 6 ; compte 000D",
                ["tout"] = "sorties 01 08 0B 1A 1C 1C 1C ; IRQ 6 ; compte 000D",
                ["tout-sauf"] = "sorties 01 08 08 08 08 08 08 ; IRQ 2 ; compte 000D",
            }));
        bad += AvecSb("sb2.0", d => VoirAdpcm(234, hw, "SB 2.0 : 48h 01h 00h, puis 7Dh, la recharge",
            Joue(d, [0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88], 15, true, [[0x48, 0x01, 0x00], [0x7d]], 10), new()
            {
                ["pcem"] = "sorties 01 02 04 06 09 0C 10 14 19 24 ; IRQ 2 6 10 ; compte 0009",
                ["seul"] = "sorties 01 02 03 05 07 07 0A 0E 12 12 ; IRQ 6 10 ; compte 000A",
                ["tout"] = "sorties 13 15 15 18 1C 20 20 25 32 4C ; IRQ 3 7 ; compte 0009",
                ["tout-sauf"] = "sorties 13 15 18 1B 1F 23 28 33 4D 81 ; IRQ 1 4 8 ; compte 0008",
            }));
        return bad;
    }

    // PB-235 — l'octet que le 8237 ne sert pas : PCem décode −1 et le compte ; le DSP l'attend. SB 2.0, le canal 1
    // masqué (0Ah ← 05h) ; 74h 07h 00h : PCem lève l'IRQ au 14e tic sur des octets fictifs ; la carte ne sort rien, et
    // reprend quand le canal est démasqué, avant le 21e tic. Témoins : sur un flux servi, 75h, 77h et 17h sortent ce
    // que sort PCem (sous PB-235 seul, le tic de PCem refait par le mode matériel).
    private static int Pb235(bool hw)
    {
        var bad = AvecSb("sb2.0", d => VoirAdpcm(235, hw, "SB 2.0 : 74h 07h 00h, le canal masqué jusqu'au 21e tic",
            Joue(d, [0x77, 0x77, 0x77, 0x77, 0x77, 0x77, 0x77, 0x77], 15, false, [[0x74, 0x07, 0x00]], 40, masque: true, demasque: 21), new()
            {
                ["pcem"] = "sorties 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 ; IRQ 14 ; compte 000F",
                ["seul"] = "sorties 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 07 16 34 70 AC E8 FF FF FF FF FF FF FF FF FF FF FF FF FF FF ; IRQ 34 ; compte 0007",
                ["tout"] = "sorties 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 07 16 34 70 AC E8 FF FF FF FF FF FF FF FF FF FF FF FF FF ; IRQ 39 ; compte 0007",
                ["tout-sauf"] = "sorties 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 ; IRQ 18 ; compte 000F",
            }));
        byte[] flux = [0x80, 0x5A, 0xC3, 0x3C, 0x96];
        bad += AvecSb("sb2.0", d => VoirAdpcm(235, hw, "SB 2.0 : 75h 03h 00h servi (témoin)",
            Joue(d, flux, 4, false, [[0x75, 0x03, 0x00]], 4), new()
            {
                ["pcem"] = "sorties 85 80 77 7E ; IRQ 4 ; compte 0000",
                ["seul"] = "sorties 85 80 77 7E ; IRQ 4 ; compte 0000",
                ["tout"] = "sorties 85 80 77 7E ; IRQ aucune ; compte 0001",
                ["tout-sauf"] = "sorties 85 80 77 7E ; IRQ aucune ; compte 0001",
            }));
        bad += AvecSb("sb2.0", d => VoirAdpcm(235, hw, "SB 2.0 : 77h 03h 00h servi (témoin)",
            Joue(d, flux, 4, false, [[0x77, 0x03, 0x00]], 6), new()
            {
                ["pcem"] = "sorties 82 80 80 7E 7E 7C ; IRQ 6 ; compte 0000",
                ["seul"] = "sorties 82 80 80 7E 7E 7C ; IRQ 6 ; compte 0000",
                ["tout"] = "sorties 82 80 80 7E 7E 7D ; IRQ aucune ; compte 0001",
                ["tout-sauf"] = "sorties 82 80 80 7E 7E 7D ; IRQ aucune ; compte 0001",
            }));
        bad += AvecSb("sb2.0", d => VoirAdpcm(235, hw, "SB 2.0 : 17h 03h 00h servi (témoin)",
            Joue(d, flux, 4, false, [[0x17, 0x03, 0x00]], 8), new()
            {
                ["pcem"] = "sorties 81 84 82 81 80 81 81 80 ; IRQ aucune ; compte 0000",
                ["seul"] = "sorties 81 84 82 81 80 81 81 80 ; IRQ aucune ; compte 0000",
                ["tout"] = "sorties 81 84 82 81 80 81 81 80 ; IRQ aucune ; compte 0001",
                ["tout-sauf"] = "sorties 81 84 82 81 80 81 81 80 ; IRQ aucune ; compte 0001",
            }));
        return bad;
    }

    // PB-284 — les tables 2 bits et 2,6 bits du DSP (2.02 `adpcm_2_decode`, `adpcm_2_6_decode`). SB 2.0 : 77h sur 80h
    // puis 6Dh : le troisième échantillon, 94h chez PCem, 90h sur la carte ; 77h sur 00h puis trois 6Ch : le neuvième,
    // au pas 10h, 54h contre 57h ; 17h sur 00h, 55h, 54h : le huitième, au pas 20h, 94h contre 9Eh.
    private static int Pb284(bool hw)
    {
        var bad = AvecSb("sb2.0", d => VoirAdpcm(284, hw, "SB 2.0 : 77h sur 80h, 6Dh, le troisième échantillon",
            Joue(d, [0x80, 0x6D], 15, false, [[0x77, 0x03, 0x00]], 3), new()
            {
                ["pcem"] = "sorties 83 8A 94 ; IRQ aucune ; compte 000C",
                ["seul"] = "sorties 83 8A 90 ; IRQ aucune ; compte 000C",
                ["tout"] = "sorties 83 8A 90 ; IRQ aucune ; compte 000D",
                ["tout-sauf"] = "sorties 83 8A 94 ; IRQ aucune ; compte 000D",
            }));
        bad += AvecSb("sb2.0", d => VoirAdpcm(284, hw, "SB 2.0 : 77h sur 00h, 6Ch, 6Ch, 6Ch, le pas 10h",
            Joue(d, [0x00, 0x6C, 0x6C, 0x6C], 15, false, [[0x77, 0x05, 0x00]], 9), new()
            {
                ["pcem"] = "sorties 03 0A 0C 13 21 25 33 4F 54 ; IRQ aucune ; compte 000A",
                ["seul"] = "sorties 03 0A 0C 13 21 25 33 4F 57 ; IRQ aucune ; compte 000A",
                ["tout"] = "sorties 03 0A 0C 13 21 25 33 4F 57 ; IRQ aucune ; compte 000B",
                ["tout-sauf"] = "sorties 03 0A 0C 13 21 25 33 4F 54 ; IRQ aucune ; compte 000B",
            }));
        bad += AvecSb("sb2.0", d => VoirAdpcm(284, hw, "SB 2.0 : 17h sur 00h, 55h, 54h, le pas 20h",
            Joue(d, [0x00, 0x55, 0x54], 15, false, [[0x17, 0x05, 0x00]], 8), new()
            {
                ["pcem"] = "sorties 01 04 0A 16 2E 5E 8E 94 ; IRQ aucune ; compte 000B",
                ["seul"] = "sorties 01 04 0A 16 2E 5E 8E 9E ; IRQ aucune ; compte 000B",
                ["tout"] = "sorties 01 04 0A 16 2E 5E 8E 9E ; IRQ aucune ; compte 000C",
                ["tout-sauf"] = "sorties 01 04 0A 16 2E 5E 8E 94 ; IRQ aucune ; compte 000C",
            }));
        return bad;
    }
}
