// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — outillage de vérification.
//
// Les cas qui discriminent les corrections du son (G13.8, PLAN-G13.md), en C# seul. Le banc : une machine neuve par
// situation (l'ami486, la carte du cas), le processeur arrêté après initpc ; la carte conduite par ses ports (220h), le
// 8237 programmé par les siens (le canal 1), et le tic de sortie du DSP joué à la main (pollsb), un octet par tic : les
// instants sont exacts, et rien ne reprogramme le DMA sous le cas. La sortie se lit dans sbdatl après sb_dsp_update, la
// sortie MIDI par son compte (midi_count).
//
// G13.8a, l'état du DSP : PB-236 (le reset), PB-241 (la grande vitesse), PB-240 (la MIDI), PB-165 (08h de la SB 16),
// PB-147 (D1h et D3h de la SB 1.0).

using iXtal26.Memory;
using iXtal26.Sound;

namespace iXtal26.Diff;

internal static partial class MaterielCas
{
    private const ushort Sb = 0x220;

    /// <summary>L'ami486 et la carte <paramref name="carte"/> (le nom interne de sound.cs), réglée par
    /// <paramref name="section"/> (la section de son device, ou rien), puis le corps sur son DSP ; l'empreinte MIDI remise
    /// à zéro ; la machine fermée ensuite.</summary>
    private static int AvecSb(string carte, Func<sb_dsp_t, int> corps, string section = "") => AvecCd(d =>
    {
        var cfg = Path.Combine(d, "sb.cfg");
        File.WriteAllText(cfg, $"model = ami486\ncpu = 10\nmem_size = 4096\ngfxcard = tvga9000b\nsndcard = {carte}\n{section}");
        try
        {
            if (!pc.loadconfig(cfg))
                throw new InvalidOperationException("loadconfig");
            Floppy.fdd_c.discfns[0] = Floppy.fdd_c.discfns[1] = "";
            for (var u = 0; u < 4; u++)
                Disc.hdd_c.ide_fn[u] = "";
            if (!pc.initpc(RomsBanc))
                throw new InvalidOperationException("initpc");
            sound_mpu401_uart.midi_raz();
            return corps((sound_sb.sb_pri ?? throw new InvalidOperationException("pas de SB après initpc")).dsp);
        }
        finally
        {
            try { pc.closepc(); } catch (Exception) { }
        }
    });

    private static void Dsp(params int[] octets)
    {
        foreach (var v in octets)
            io.outb(Sb + 0xc, (byte)v);
    }

    private static void ResetDsp()
    {
        io.outb(Sb + 6, 1);
        io.outb(Sb + 6, 0);
    }

    /// <summary>Les octets que le DSP a à rendre (2xEh bit 7), lus à 2xAh ; « rien » s'il n'en a pas.</summary>
    private static string LireDsp()
    {
        var l = new List<string>();
        while ((io.inb(Sb + 0xe) & 0x80) != 0 && l.Count < 64)
            l.Add(io.inb(Sb + 0xa).ToString("X2"));
        return l.Count == 0 ? "rien" : string.Join(" ", l);
    }

    /// <summary>Le canal 1 du 8237 : <paramref name="compte"/> + 1 octets depuis <paramref name="adresse"/>, en lecture
    /// de la mémoire (la sortie) ou en écriture (l'entrée), simple ou automatique. Le canal 4 du 8237 haut en cascade et
    /// démasqué, comme le POST de l'AT le laisse : sans lui, le 8237 de la fiche (PB-252, PB-253, sous « tout ») ne
    /// sert pas le bas.</summary>
    private static void Dma1(uint adresse, int compte, bool auto, bool entree = false)
    {
        io.outb(0xd6, 0xc0);
        io.outb(0xd4, 0x00);
        io.outb(0x0a, 0x05);
        io.outb(0x0c, 0);
        io.outb(0x0b, (byte)((entree ? 0x45 : 0x49) | (auto ? 0x10 : 0)));
        io.outb(0x02, (byte)adresse);
        io.outb(0x02, (byte)(adresse >> 8));
        io.outb(0x83, (byte)(adresse >> 16));
        io.outb(0x03, (byte)compte);
        io.outb(0x03, (byte)(compte >> 8));
        io.outb(0x0a, 0x01);
    }

    /// <summary>Les tics de sortie jusqu'à l'IRQ 8 bits : le rang du tic qui la lève, ou 0 si elle ne vient pas.</summary>
    private static int TicsIrq(sb_dsp_t d, int max)
    {
        for (var i = 1; i <= max; i++)
        {
            sound_sb_dsp.pollsb(d);
            if (d.sb_irq8 != 0)
                return i;
        }
        return 0;
    }

    private static void Tics(sb_dsp_t d, int n)
    {
        for (var i = 0; i < n; i++)
            sound_sb_dsp.pollsb(d);
    }

    // PB-236 — le DSP au démarrage et au reset (guide, p. 2-2 ; 2.02 `cold_boot`, 3.02, 4.05) : le bloc à 07FFh, avant
    // le 4.xx la constante de temps à 9Ch et la sortie coupée. PCem garde le bloc à FFFFh, la constante et `muted`
    // (0 au démarrage). SB 2.0 au démarrage, puis 1Ch : PCem lève l'IRQ au 65 536e octet, la constante à 00h, la sortie
    // ouverte ; la carte au 2 048e, 9Ch (sortie et entrée, 10 000 Hz), coupée. SB 2.0 : 40h D3h, D1h, un reset, D8h, puis 1Ch : PCem garde D3h et la
    // sortie ouverte ; la carte 9Ch, coupée ; D8h rend 00h des deux côtés. SB 16 au démarrage : le bloc seul, la
    // constante et la sortie inchangées. Témoins : 48h FFh 00h puis 1Ch, l'IRQ au 256e octet des deux côtés ; la SB 1.0
    // au démarrage, coupée sous PB-147 seulement (sans lui, D1h ne la rouvrirait pas).
    private static int Pb236(bool hw)
    {
        var bad = 0;
        string Etat(sb_dsp_t d, string avant)
        {
            Dma1(0x10000, 0xffff, auto: true);
            Dsp(0x1c);
            var t = TicsIrq(d, 0x11000);
            return $"{avant}constante {d.sb_timeo:X2}h/{d.sb_timei:X2}h, {d.sb_freq} Hz, muted {d.muted}, IRQ au {t}e octet";
        }
        bad += AvecSb("sb2.0", d =>
        {
            var v = Etat(d, "");
            var att = hw ? "constante 9Ch/9Ch, 10000 Hz, muted 1, IRQ au 2048e octet" : "constante 00h/00h, 0 Hz, muted 0, IRQ au 65536e octet";
            return Voir("SB 2.0 au démarrage, puis 1Ch", v == att, v, att, hw);
        });
        bad += AvecSb("sb2.0", d =>
        {
            Dsp(0x40, 0xd3, 0xd1);
            ResetDsp();
            var aa = LireDsp();
            Dsp(0xd8);
            var v = Etat(d, $"{aa}, D8h {LireDsp()}, ");
            var att = hw ? "AA, D8h 00, constante 9Ch/9Ch, 10000 Hz, muted 1, IRQ au 2048e octet" : "AA, D8h 00, constante D3h/D3h, 22222 Hz, muted 0, IRQ au 65536e octet";
            return Voir("SB 2.0 : 40h D3h, D1h, reset, D8h, puis 1Ch", v == att, v, att, hw);
        });
        bad += AvecSb("sb16", d =>
        {
            var v = Etat(d, "");
            var att = hw ? "constante 00h/00h, 0 Hz, muted 0, IRQ au 2048e octet" : "constante 00h/00h, 0 Hz, muted 0, IRQ au 65536e octet";
            return Voir("SB 16 au démarrage, puis 1Ch", v == att, v, att, hw);
        });
        bad += AvecSb("sb2.0", d =>
        {
            Dsp(0x48, 0xff, 0x00);
            Dma1(0x10000, 0xffff, auto: true);
            Dsp(0x1c);
            var v = $"IRQ au {TicsIrq(d, 0x11000)}e octet";
            const string att = "IRQ au 256e octet";
            return Voir("SB 2.0 : 48h FFh 00h, puis 1Ch (témoin)", v == att, v, att, hw);
        });
        bad += AvecSb("sb", d =>
        {
            var v = $"muted {d.muted}";
            var att = $"muted {(hw && iXtal26.materiel.pb_147 ? 1 : 0)}";
            return Voir("SB 1.0 au démarrage (témoin, avec PB-147)", v == att, v, att, hw);
        });
        return bad;
    }

    // PB-241 — en grande vitesse, le DSP 2.01 à 3.xx ne prend aucune commande (guide, p. 6-20 ; 2.02 `X0748`), et le
    // reset qui la termine est « chaud » (`start`, la signature 34h 12h, posée aussi par le 4.05 et le 4.13) : le bloc,
    // la constante et le haut-parleur gardés. SB 2.0 : 40h A5h, D1h, 48h FFh 0Fh, 90h, puis E1h : PCem rend 02h 01h, la
    // carte rien. Puis un reset, D8h et E1h : AAh des deux côtés ; PCem remet le bloc à FFFFh et le haut-parleur à
    // l'arrêt (D8h 00h), la carte garde 0FFFh et le haut-parleur (FFh) ; la constante A5h des deux côtés (sous « tout »,
    // le reset froid de PB-236 la mettrait à 9Ch). De même à l'entrée (98h). La SB 16, qui prend E1h en grande vitesse
    // (04h 05h des deux côtés, le témoin), y garde aussi son bloc au reset. Témoin : 91h sur un bloc de 16 octets, fini
    // au 16e tic, rend le DSP aux commandes (E1h : 02h 01h des deux côtés) et efface la signature : le reset qui suit
    // est froid (FFFFh, 07FFh sous PB-236).
    private static int Pb241(bool hw)
    {
        var bad = 0;
        bad += AvecSb("sb2.0", d =>
        {
            Dsp(0x40, 0xa5, 0xd1, 0x48, 0xff, 0x0f);
            Dma1(0x10000, 0xffff, auto: true);
            Dsp(0x90);
            Tics(d, 4);
            Dsp(0xe1);
            var v = $"E1h : {LireDsp()}";
            var att = hw ? "E1h : rien" : "E1h : 02 01";
            var b = Voir("SB 2.0 : 40h A5h, D1h, 48h FFh 0Fh, 90h, puis E1h", v == att, v, att, hw);
            ResetDsp();
            var aa = LireDsp();
            var bloc = d.sb_8_autolen;
            Dsp(0xd8);
            var hp = LireDsp();
            Dsp(0xe1);
            v = $"{aa}, bloc {bloc:X4}h, constante {d.sb_timeo:X2}h, muted {d.muted}, D8h {hp}, E1h : {LireDsp()}";
            att = hw ? "AA, bloc 0FFFh, constante A5h, muted 0, D8h FF, E1h : 02 01"
                     : "AA, bloc FFFFh, constante A5h, muted 0, D8h 00, E1h : 02 01";
            return b + Voir("puis un reset, D8h et E1h", v == att, v, att, hw);
        });
        bad += AvecSb("sb2.0", d =>
        {
            Dsp(0x48, 0xff, 0x0f);
            Dma1(0x10000, 0xffff, auto: true, entree: true);
            Dsp(0x98, 0xe1);
            var v = $"E1h : {LireDsp()}";
            var att = hw ? "E1h : rien" : "E1h : 02 01";
            return Voir("SB 2.0 : 48h FFh 0Fh, 98h, puis E1h", v == att, v, att, hw);
        });
        bad += AvecSb("sb16", d =>
        {
            Dsp(0x48, 0xff, 0x0f);
            Dma1(0x10000, 0xffff, auto: true);
            Dsp(0x90, 0xe1);
            var v = $"E1h : {LireDsp()}";
            const string att0 = "E1h : 04 05";
            var b = Voir("SB 16 : 48h FFh 0Fh, 90h, puis E1h (témoin)", v == att0, v, att0, hw);
            ResetDsp();
            v = $"{LireDsp()}, bloc {d.sb_8_autolen:X4}h";
            var att = hw ? "AA, bloc 0FFFh" : "AA, bloc FFFFh";
            return b + Voir("puis un reset", v == att, v, att, hw);
        });
        bad += AvecSb("sb2.0", d =>
        {
            Dsp(0x48, 0x0f, 0x00);
            Dma1(0x10000, 0xffff, auto: false);
            Dsp(0x91);
            var t = TicsIrq(d, 64);
            Dsp(0xe1);
            var e1 = LireDsp();
            ResetDsp();
            var v = $"IRQ au {t}e octet, E1h : {e1}, puis reset : {LireDsp()}, bloc {d.sb_8_autolen:X4}h";
            var att = $"IRQ au 16e octet, E1h : 02 01, puis reset : AA, bloc {(hw && iXtal26.materiel.pb_236 ? "07FF" : "FFFF")}h";
            return Voir("SB 2.0 : 48h 0Fh 00h, 91h jusqu'à la fin du bloc, E1h, puis un reset (témoin)", v == att, v, att, hw);
        });
        return bad;
    }

    // PB-240 — la MIDI de la SB (guide, p. 5-3, p. 6-12 à 6-14 ; 2.02 `cmd_midi_write_poll`, `midi_main_loop`) : 38h
    // envoie l'octet suivant à la sortie MIDI ; 34h à 37h ouvrent le mode UART, dont seul un reset fait sortir. SB 2.0 :
    // 38h 90h : PCem lance une sortie DMA et n'envoie rien ; la carte envoie 90h et ne lance rien. 48h FFh 0Fh, 34h,
    // puis 90h, E1h, 41h : PCem lance le DMA, rend 02h 01h, rien à la MIDI ; la carte envoie les trois octets. Puis un
    // reset et E1h : AAh, 02h 01h des deux côtés, plus rien à la MIDI ; le reset « chaud » de la carte garde le bloc
    // (0FFFh), PCem le remet à FFFFh. 37h, l'autre bout de la plage, puis 90h : de même, un octet à la MIDI. 38h puis un
    // reset : l'octet attendu est oublié, E1h s'exécute des deux côtés. Témoins : 38h en paramètre (40h 38h), la
    // constante 38h des deux côtés, rien à la MIDI ; la SB 1.0 (DSP 1.05), sans mode UART : 34h puis E1h rend 01h 05h
    // des deux côtés.
    private static int Pb240(bool hw)
    {
        var bad = 0;
        string Midi(sb_dsp_t d) => $"DMA {(d.sb_8_enable != 0 ? "lancé" : "arrêté")}, {sound_mpu401_uart.midi_count} octet(s) MIDI";
        bad += AvecSb("sb2.0", d =>
        {
            Dma1(0x10000, 0xffff, auto: true);
            Dsp(0x38, 0x90);
            var v = Midi(d);
            var att = hw ? "DMA arrêté, 1 octet(s) MIDI" : "DMA lancé, 0 octet(s) MIDI";
            return Voir("SB 2.0 : 38h 90h", v == att, v, att, hw);
        });
        bad += AvecSb("sb2.0", d =>
        {
            Dsp(0x48, 0xff, 0x0f);
            Dma1(0x10000, 0xffff, auto: true);
            Dsp(0x34, 0x90, 0xe1, 0x41);
            var v = $"DMA {(d.sb_8_enable != 0 ? "lancé" : "arrêté")}, E1h : {LireDsp()}, {sound_mpu401_uart.midi_count} octet(s) MIDI";
            var att = hw ? "DMA arrêté, E1h : rien, 3 octet(s) MIDI" : "DMA lancé, E1h : 02 01, 0 octet(s) MIDI";
            var b = Voir("SB 2.0 : 48h FFh 0Fh, 34h, puis 90h, E1h, 41h", v == att, v, att, hw);
            var n = sound_mpu401_uart.midi_count;
            ResetDsp();
            var aa = LireDsp();
            var bloc = d.sb_8_autolen;
            Dsp(0xe1);
            v = $"{aa}, bloc {bloc:X4}h, E1h : {LireDsp()}, {sound_mpu401_uart.midi_count - n} octet(s) MIDI de plus";
            att = hw ? "AA, bloc 0FFFh, E1h : 02 01, 0 octet(s) MIDI de plus" : "AA, bloc FFFFh, E1h : 02 01, 0 octet(s) MIDI de plus";
            return b + Voir("puis un reset, et E1h", v == att, v, att, hw);
        });
        bad += AvecSb("sb2.0", d =>
        {
            Dma1(0x10000, 0xffff, auto: true);
            Dsp(0x37, 0x90);
            var v = Midi(d);
            var att = hw ? "DMA arrêté, 1 octet(s) MIDI" : "DMA lancé, 0 octet(s) MIDI";
            return Voir("SB 2.0 : 37h 90h", v == att, v, att, hw);
        });
        bad += AvecSb("sb2.0", d =>
        {
            Dsp(0x38);
            ResetDsp();
            var aa = LireDsp();
            Dsp(0xe1);
            var v = $"{aa}, E1h : {LireDsp()}, {sound_mpu401_uart.midi_count} octet(s) MIDI";
            const string att = "AA, E1h : 02 01, 0 octet(s) MIDI";
            return Voir("SB 2.0 : 38h, un reset, puis E1h", v == att, v, att, hw);
        });
        bad += AvecSb("sb2.0", d =>
        {
            Dsp(0x40, 0x38);
            var v = $"constante {d.sb_timeo:X2}h, {sound_mpu401_uart.midi_count} octet(s) MIDI";
            const string att = "constante 38h, 0 octet(s) MIDI";
            return Voir("SB 2.0 : 40h 38h (témoin)", v == att, v, att, hw);
        });
        bad += AvecSb("sb", d =>
        {
            Dsp(0x34, 0xe1);
            var v = $"E1h : {LireDsp()}, {sound_mpu401_uart.midi_count} octet(s) MIDI";
            const string att = "E1h : 01 05, 0 octet(s) MIDI";
            return Voir("SB 1.0 : 34h, puis E1h (témoin)", v == att, v, att, hw);
        });
        return bad;
    }

    // PB-165 — 08h sur la SB 16 : le DSP 4.05 (`cmd_csp_version`) ne lit aucun paramètre ; PCem en attend un et avale
    // l'octet suivant. 08h E1h : PCem rend 18h et plus rien ; la carte 18h, puis 04h 05h (la valeur 18h, inconnue sans
    // la puce, reste celle de PCem). Témoin : l'AWE32 (4.13), déjà juste chez PCem : 18h 04h 0Dh des deux côtés.
    private static int Pb165(bool hw)
    {
        var bad = 0;
        bad += AvecSb("sb16", d =>
        {
            Dsp(0x08, 0xe1);
            var v = LireDsp();
            var att = hw ? "18 04 05" : "18";
            return Voir("SB 16 : 08h E1h", v == att, v, att, hw);
        });
        bad += AvecSb("sbawe32", d =>
        {
            Dsp(0x08, 0xe1);
            var v = LireDsp();
            const string att = "18 04 0D";
            return Voir("AWE32 : 08h E1h (témoin)", v == att, v, att, hw);
        });
        return bad;
    }

    // PB-147 — sur la SB 1.0, D3h coupe et D1h relie aussi la sortie du CNA (guide, p. 6-25 et 6-26) ; PCem n'y fait que
    // la pause du DMA, fidèle. Le canal 1 en simple cycle sur 100h octets à 00h : D3h, 14h FFh 00h, D4h, quatre tics :
    // la sortie vaut 8000h (−32 768) chez PCem, 0 sur la carte. Témoin : D1h, puis de même, 8000h des deux côtés (sous
    // « tout », la sortie coupée au démarrage par PB-236 : D1h la rouvre).
    private static int Pb147(bool hw)
    {
        var bad = 0;
        string Sortie(sb_dsp_t d, int commande)
        {
            for (uint k = 0; k < 0x100; k++)
                mem.mem_writeb_phys(0x10000 + k, 0);
            Dma1(0x10000, 0xff, auto: false);
            Dsp(commande, 0x14, 0xff, 0x00, 0xd4);
            Tics(d, 4);
            sound_sb_dsp.sb_dsp_update(d);
            return $"sortie {(ushort)d.sbdatl:X4}h";
        }
        bad += AvecSb("sb", d =>
        {
            var v = Sortie(d, 0xd3);
            var att = hw ? "sortie 0000h" : "sortie 8000h";
            return Voir("SB 1.0 : D3h, 14h FFh 00h, D4h, quatre tics", v == att, v, att, hw);
        });
        bad += AvecSb("sb", d =>
        {
            var v = Sortie(d, 0xd1);
            const string att = "sortie 8000h";
            return Voir("SB 1.0 : D1h, 14h FFh 00h, D4h, quatre tics (témoin)", v == att, v, att, hw);
        });
        return bad;
    }
}
