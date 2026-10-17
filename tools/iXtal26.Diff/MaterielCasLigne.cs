// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — outillage de vérification.
//
// Les cas de la ligne d'IRQ partagée de la SB 16 (G13.8d2, PLAN-G13.md) : PB-238 (2xEh n'acquitte que l'IRQ 8 bits ; la
// ligne retombe quand aucune source ne la tient) et PB-154 (l'IRQ du MPU-401, 82h bit 2, 3Fh ignorée en mode UART), sur
// le banc du son (MaterielCasSon.cs). Les IRQ du DSP se lèvent par F2h (8 bits) et F3h (16 bits) ; le MPU se conduit par
// 331h (les commandes, l'état) et 330h (la donnée). La ligne se lit au registre IRR du 8259 (pic_.pend), à l'IRQ 7 de la
// carte, et 82h du mélangeur dit les sources.

using iXtal26.Models;
using iXtal26.Sound;

namespace iXtal26.Diff;

internal static partial class MaterielCas
{
    /// <summary>Le bit de l'IRQ <paramref name="irq"/> au registre IRR du 8259 maître.</summary>
    private static string Irr(int irq = 7) => $"IRR{irq}={(pic.pic_.pend >> irq) & 1}";

    private static string Etat82() => MelLit(0x224, 0x82);

    private static string Mpu331() => $"331h={io.inb(0x331):X2}";

    // PB-238 — 2xEh n'acquitte que l'IRQ 8 bits, et la ligne que partagent le 8 bits, le 16 bits et le MPU ne retombe
    // qu'avec la dernière source (guide, p. 2-5). PCem efface les deux IRQ du DSP à 2xEh et baisse la ligne. F2h F3h puis
    // 2xEh : PCem 82h=00 et IRR à 0 ; la carte 82h=02 et IRR à 1. Les cas que le MPU tient ne discriminent que sous
    // PB-154 aussi (sans lui, le MPU ne lève rien) : témoins sinon ; les trois sources acquittées une à une suivent PB-154.
    // Le reset en mode UART du DSP passe, sous PB-240, par le reset « chaud ». Témoins : F2h puis 2xEh, F2h F3h puis
    // 2xFh, les deux acquittés, et le reset sans le MPU.
    private static int Pb238(bool hw)
    {
        int Un(string nom, Func<sb_dsp_t, string> corps, string pcem, string mat, bool temoin = false) =>
            AvecSb("sb16", d =>
            {
                var v = corps(d);
                var att = temoin || hw ? mat : pcem;
                return Voir(nom, v == att, v, att, hw);
            });
        var bad = Un("SB 16 : F2h, F3h, puis 2xEh lu", _ =>
        {
            Dsp(0xf2, 0xf3);
            io.inb(Sb + 0xe);
            return $"{Etat82()} {Irr()}";
        }, "82h=00 IRR7=0", "82h=02 IRR7=1");
        bad += Un("SB 16 : F3h, puis 2xEh lu", _ =>
        {
            Dsp(0xf3);
            io.inb(Sb + 0xe);
            return $"{Etat82()} {Irr()}";
        }, "82h=00 IRR7=0", "82h=02 IRR7=1");
        bad += Un("SB 16 : F2h, F3h, 2xEh lu, puis 2xFh lu (témoin)", _ =>
        {
            Dsp(0xf2, 0xf3);
            io.inb(Sb + 0xe);
            io.inb(Sb + 0xf);
            return $"{Etat82()} {Irr()}";
        }, "", "82h=00 IRR7=0", temoin: true);
        bad += Un("SB 16 : F2h, puis 2xEh lu (témoin)", _ =>
        {
            Dsp(0xf2);
            io.inb(Sb + 0xe);
            return $"{Etat82()} {Irr()}";
        }, "", "82h=00 IRR7=0", temoin: true);
        bad += Un("SB 16 : F2h, F3h, puis 2xFh lu (témoin)", _ =>
        {
            Dsp(0xf2, 0xf3);
            io.inb(Sb + 0xf);
            return $"{Etat82()} {Irr()}";
        }, "", "82h=01 IRR7=1", temoin: true);
        bad += Un("SB 16 : 3Fh au MPU, F3h, puis 2xFh lu", _ =>
        {
            io.outb(0x331, 0x3f);
            Dsp(0xf3);
            io.inb(Sb + 0xf);
            return $"{Etat82()} {Irr()}";
        }, "82h=00 IRR7=0", materiel.pb_154 ? "82h=04 IRR7=1" : "82h=00 IRR7=0", temoin: !materiel.pb_154);
        bad += Un("SB 16 : 3Fh au MPU, F2h, F3h, puis le reset du DSP", _ =>
        {
            io.outb(0x331, 0x3f);
            Dsp(0xf2, 0xf3);
            ResetDsp();
            return $"{Etat82()} {Irr()}";
        }, "82h=00 IRR7=0", materiel.pb_154 ? "82h=04 IRR7=1" : "82h=00 IRR7=0", temoin: !materiel.pb_154);
        bad += Un("SB 16 : F2h, F3h, 3Fh au MPU, puis 2xEh, 2xFh et 330h lus un à un", _ =>
        {
            Dsp(0xf2, 0xf3);
            io.outb(0x331, 0x3f);
            var l = new List<string> { $"{Etat82()} {Irr()}" };
            foreach (var port in new ushort[] { Sb + 0xe, Sb + 0xf, 0x330 })
            {
                io.inb(port);
                l.Add($"{Etat82()} {Irr()}");
            }
            return string.Join(", ", l);
        }, "82h=03 IRR7=1, 82h=00 IRR7=0, 82h=00 IRR7=0, 82h=00 IRR7=0",
           materiel.pb_154 ? "82h=07 IRR7=1, 82h=06 IRR7=1, 82h=04 IRR7=1, 82h=00 IRR7=0"
                           : "82h=03 IRR7=1, 82h=02 IRR7=1, 82h=00 IRR7=0, 82h=00 IRR7=0");
        // Le reset du DSP en mode UART : « chaud » sous PB-240 (reset_chaud_materiel), rejoué l'IRQ hors du PIC.
        bad += Un("SB 16 : 3Fh au MPU, 34h (le mode UART du DSP), puis le reset du DSP", _ =>
        {
            io.outb(0x331, 0x3f);
            Dsp(0x34);
            ResetDsp();
            return $"{Etat82()} {Irr()}";
        }, "82h=00 IRR7=0", materiel.pb_154 ? "82h=04 IRR7=1" : "82h=00 IRR7=0", temoin: !materiel.pb_154);
        bad += Un("SB 16 : F2h, F3h, puis le reset du DSP (témoin)", _ =>
        {
            Dsp(0xf2, 0xf3);
            ResetDsp();
            return $"{Etat82()} {Irr()}";
        }, "", "82h=00 IRR7=0", temoin: true);
        return bad;
    }

    // PB-154 — le MPU-401 de la SB 16 lève l'IRQ de la carte quand il rend un octet : au passage en UART (3Fh, guide
    // p. 5-9) et à l'ACK de FFh (déduit) ; la lecture de 330h l'efface ; 82h bit 2 la montre ; en mode UART, 3Fh est
    // ignorée (p. 5-5). PCem ne lève rien (IRQ -1) et acquitte 3Fh en mode UART. L'IRQ suit 80h. Le cas de 2xEh suit
    // PB-238 : sans lui, 2xEh baisse la ligne que le MPU tient. FFh en mode UART lève l'IRQ avec l'ACK de PCem (l'ACK
    // inconnu ; l'IRQ qui le suit, déduite) ; l'AWE32 monte le même MPU. Témoins : 3Fh puis 330h lu (l'ACK, rien de
    // pendant), F3h ou F2h et 3Fh puis 330h lu (le DSP tient la ligne), et la MIDI en mode UART.
    private static int Pb154(bool hw)
    {
        int Un(string nom, Func<sb_dsp_t, string> corps, string pcem, string mat, bool temoin = false,
               string carte = "sb16") =>
            AvecSb(carte, d =>
            {
                var v = corps(d);
                var att = temoin || hw ? mat : pcem;
                return Voir(nom, v == att, v, att, hw);
            });
        var bad = Un("SB 16 : 3Fh au MPU", _ =>
        {
            io.outb(0x331, 0x3f);
            return $"{Irr()} {Etat82()} {Mpu331()}";
        }, "IRR7=0 82h=00 331h=00", "IRR7=1 82h=04 331h=00");
        bad += Un("SB 16 : FFh au MPU, hors du mode UART", _ =>
        {
            io.outb(0x331, 0xff);
            return $"{Irr()} {Etat82()} {Mpu331()}";
        }, "IRR7=0 82h=00 331h=00", "IRR7=1 82h=04 331h=00");
        bad += Un("SB 16 : 3Fh, 330h lu, puis 3Fh de nouveau, en mode UART", _ =>
        {
            io.outb(0x331, 0x3f);
            io.inb(0x330);
            io.outb(0x331, 0x3f);
            return $"{Irr()} {Etat82()} {Mpu331()}";
        }, "IRR7=0 82h=00 331h=00", "IRR7=0 82h=00 331h=80");
        bad += Un("SB 16 : 3Fh, 330h lu, puis FFh en mode UART", _ =>
        {
            io.outb(0x331, 0x3f);
            io.inb(0x330);
            io.outb(0x331, 0xff);
            return $"{Irr()} {Etat82()} {Mpu331()}";
        }, "IRR7=0 82h=00 331h=00", "IRR7=1 82h=04 331h=00");
        bad += Un("AWE32 : 3Fh au MPU", _ =>
        {
            io.outb(0x331, 0x3f);
            return $"{Irr()} {Etat82()} {Mpu331()}";
        }, "IRR7=0 82h=00 331h=00", "IRR7=1 82h=04 331h=00", carte: "sbawe32");
        bad += Un("SB 16 : 80h=02h (IRQ 5), puis 3Fh au MPU", _ =>
        {
            MelEcrit(0x224, 0x80, 0x02);
            io.outb(0x331, 0x3f);
            return $"{Irr(5)} {Irr()}";
        }, "IRR5=0 IRR7=0", "IRR5=1 IRR7=0");
        bad += Un("SB 16 : 3Fh au MPU, puis 2xEh lu", _ =>
        {
            io.outb(0x331, 0x3f);
            io.inb(Sb + 0xe);
            return $"{Irr()} {Etat82()}";
        }, "IRR7=0 82h=00", materiel.pb_238 ? "IRR7=1 82h=04" : "IRR7=0 82h=04");
        bad += Un("SB 16 : 3Fh au MPU, puis 330h lu (témoin)", _ =>
        {
            io.outb(0x331, 0x3f);
            var v = io.inb(0x330);
            return $"330h={v:X2} {Irr()} {Etat82()} {Mpu331()}";
        }, "", "330h=FE IRR7=0 82h=00 331h=80", temoin: true);
        bad += Un("SB 16 : F3h, 3Fh au MPU, puis 330h lu (témoin)", _ =>
        {
            Dsp(0xf3);
            io.outb(0x331, 0x3f);
            io.inb(0x330);
            return $"{Irr()} {Etat82()}";
        }, "", "IRR7=1 82h=02", temoin: true);
        bad += Un("SB 16 : F2h, 3Fh au MPU, puis 330h lu (témoin)", _ =>
        {
            Dsp(0xf2);
            io.outb(0x331, 0x3f);
            io.inb(0x330);
            return $"{Irr()} {Etat82()}";
        }, "", "IRR7=1 82h=01", temoin: true);
        bad += Un("SB 16 : 3Fh, 330h lu, puis 90h 3Ch 7Fh à 330h (témoin)", _ =>
        {
            io.outb(0x331, 0x3f);
            io.inb(0x330);
            foreach (var b in new byte[] { 0x90, 0x3c, 0x7f })
                io.outb(0x330, b);
            return $"MIDI {sound_mpu401_uart.midi_count} {Irr()} {Etat82()}";
        }, "", "MIDI 3 IRR7=0 82h=00", temoin: true);
        return bad + MpuCavalier(hw);
    }

    // G13.8d3a — le cavalier du MPU (mpu_addr : 330h, 300h, ou 0, absent), le même dans les deux modes : témoins. 3Fh,
    // puis la donnée lue (FEh, l'ACK), là où le MPU est monté ; FFh là où rien ne répond. L'avertissement de
    // pc.check_sndcard (appelé par initpc, ici après loadconfig) suit le port du MPU : l'AHA-1542C en 330h le croise en
    // 330h, non en 300h ; en 334h, nulle part.
    private static int MpuCavalier(bool hw)
    {
        int Un(string nom, string section, Func<string> corps, string att) =>
            AvecSb("sb16", _ =>
            {
                var v = corps();
                return Voir(nom, v == att, v, att, hw);
            }, "[Sound Blaster 16]\nirq = 7\n" + section);
        string Ack(ushort port)
        {
            io.outb((ushort)(port + 1), 0x3f);
            return $"{port:X3}h={io.inb(port):X2}";
        }
        var bad = Un("SB 16, mpu_addr = 300h (témoin)", "mpu_addr = 768\n", () => $"{Ack(0x300)} {Ack(0x330)}",
                     "300h=FE 330h=FF");
        bad += Un("SB 16, mpu_addr = 0, sans MPU (témoin)", "mpu_addr = 0\n", () => $"{Ack(0x300)} {Ack(0x330)}",
                  "300h=FF 330h=FF");
        foreach (var (aha, mpu, att) in new[]
                 {
                     ("addr = 816", "", "averti"), ("addr = 816", "mpu_addr = 768", "rien"), ("", "", "rien"),
                 })
            bad += AvecCd(d =>
            {
                var cfg = Path.Combine(d, "aha.cfg");
                var nomAha = Scsi.scsi_aha1540.scsi_aha1542c_device.name;
                File.WriteAllText(cfg, "model = ami486\ncpu = 10\nmem_size = 4096\ngfxcard = tvga9000b\n" +
                                       "sndcard = sb16\nhdd_controller = aha1542c\n" +
                                       $"[Sound Blaster 16]\n{mpu}\n[{nomAha}]\n{aha}\n");
                var err = Console.Error;
                var capte = new StringWriter();
                try
                {
                    Console.SetError(capte);
                    if (!pc.loadconfig(cfg))
                        throw new InvalidOperationException("loadconfig");
                    pc.check_sndcard();
                }
                finally
                {
                    Console.SetError(err);
                }
                var v = capte.ToString().Contains("MPU-401 de la carte") ? "averti" : "rien";
                var nom = $"l'AHA-1542C {(aha == "" ? "sans clé (334h)" : "en 330h")}, " +
                          $"le MPU {(mpu == "" ? "en 330h" : "en 300h")} : l'avertissement (témoin)";
                return Voir(nom, v == att, v, att, hw);
            });
        return bad;
    }
}
