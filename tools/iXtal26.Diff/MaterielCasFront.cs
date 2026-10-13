// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — outillage de vérification.
//
// Les cas du re-front (G13.8d4, PLAN-G13.md) : PB-285 (une IRQ du DSP levée sur la ligne déjà haute ne repose pas la
// demande au PIC), sur le banc du son (MaterielCasSon.cs) et celui de la ligne partagée (MaterielCasLigne.cs). La
// demande prise par le processeur se joue à la main : l'IRQ 7 seule démasquée au 8259 maître, puis l'INTA
// (picinterrupt), qui efface le bit d'IRR et pose celui d'ISR. La ligne reste haute tant que la source n'est pas
// acquittée ; une levée de plus ne fait pas de front.

using iXtal26.Models;
using iXtal26.Sound;

namespace iXtal26.Diff;

internal static partial class MaterielCas
{
    /// <summary>La demande de l'IRQ <paramref name="irq"/> prise par le processeur : l'IRQ seule démasquée, puis
    /// l'INTA. La demande doit être au registre IRR, et l'INTA rendre son vecteur.</summary>
    private static void Prise(int irq = 7)
    {
        if (((pic.pic_.pend >> irq) & 1) == 0)
            throw new InvalidOperationException($"IRQ {irq} : aucune demande à prendre");
        io.outb(0x21, (byte)~(1 << irq));
        var v = pic.picinterrupt();
        if (v != pic.pic_.vector + irq)
            throw new InvalidOperationException($"IRQ {irq} : l'INTA rend {v:X2}h");
    }

    /// <summary>Les tics d'entrée, joués à la main.</summary>
    private static void TicsEntree(sb_dsp_t d, int n)
    {
        for (var i = 0; i < n; i++)
            sound_sb_dsp.sb_poll_i(d);
    }

    // PB-285 — le 8259 de l'AT prend l'IRQ sur un front (ICW1, LTIM = 0, posé par le BIOS) : une source qui se lève
    // pendant que la ligne de la carte est déjà haute (une autre source pendante, ou la même non acquittée) ne fait pas
    // de demande neuve (déduit). PCem repose le bit d'IRR à chaque levée : après l'INTA, la ligne toujours haute,
    // l'interruption revient. Chaque site qui lève : F2h, F3h, la fin d'un bloc en sortie et en entrée, 8 et 16 bits,
    // la fin de la pause de 80h, et, sous PB-239, la fin du bloc qui reprend la commande en file (SB 2.0) ; la SB 2.0 sans
    // ligne partagée, l'IRQ 8 bits non acquittée. Le cas que le MPU tient ne discrimine que sous PB-154 (sans lui, le
    // MPU ne lève rien) : témoin sinon. Témoins : l'acquit avant la seconde levée, avec et sans l'INTA, le reset du DSP
    // (la ligne retombée, un front neuf), et 80h changé pendant que la ligne est haute (la levée sur la nouvelle IRQ,
    // celle de PCem dans les deux modes : la carte, inconnue).
    private static int Pb285(bool hw)
    {
        int Un(string nom, Func<sb_dsp_t, string> corps, string pcem, string mat, bool temoin = false,
               string carte = "sb16") =>
            AvecSb(carte, d =>
            {
                var v = corps(d);
                var att = temoin || hw ? mat : pcem;
                return Voir(nom, v == att, v, att, hw);
            });
        var bad = Un("SB 16 : F2h, la demande prise (INTA), puis F2h sans acquit", _ =>
        {
            Dsp(0xf2);
            Prise();
            Dsp(0xf2);
            return $"{Etat82()} {Irr()}";
        }, "82h=01 IRR7=1", "82h=01 IRR7=0");
        bad += Un("SB 16 : F2h, INTA, puis F3h", _ =>
        {
            Dsp(0xf2);
            Prise();
            Dsp(0xf3);
            return $"{Etat82()} {Irr()}";
        }, "82h=03 IRR7=1", "82h=03 IRR7=0");
        bad += Un("SB 2.0 : F2h, INTA, puis F2h sans acquit", _ =>
        {
            Dsp(0xf2);
            Prise();
            Dsp(0xf2);
            return Irr();
        }, "IRR7=1", "IRR7=0", carte: "sb2.0");
        bad += Un("SB 16 : F3h, INTA, puis 14h, quatre octets en sortie, jusqu'au bout du bloc", d =>
        {
            Dsp(0xf3);
            Prise();
            Dma1(0x20000, 3, false);
            Dsp(0x14, 3, 0);
            Tics(d, 6);
            return $"{Etat82()} {Irr()} 8bits={d.sb_8_enable}";
        }, "82h=03 IRR7=1 8bits=0", "82h=03 IRR7=0 8bits=0");
        bad += Un("SB 16 : F2h, INTA, puis B0h 00h, quatre mots en sortie, jusqu'au bout du bloc", d =>
        {
            Dsp(0xf2);
            Prise();
            Dma5(0x20000, 3, false);
            Dsp(0xb0, 0x00, 3, 0);
            Tics(d, 6);
            return $"{Etat82()} {Irr()} 16bits={d.sb_16_enable}";
        }, "82h=03 IRR7=1 16bits=0", "82h=03 IRR7=0 16bits=0");
        bad += Un("SB 16 : F3h, INTA, puis 80h, une pause de quatre tics, joués", d =>
        {
            Dsp(0xf3);
            Prise();
            Dsp(0x80, 3, 0);
            Tics(d, 4);
            return $"{Etat82()} {Irr()} minuterie={timer.timer_is_enabled(d.output_timer)}";
        }, "82h=03 IRR7=1 minuterie=0", "82h=03 IRR7=0 minuterie=0");
        bad += Un("SB 16 : F3h, INTA, puis 24h, quatre octets en entrée, jusqu'au bout du bloc", d =>
        {
            Dsp(0xf3);
            Prise();
            Dma1(0x20000, 3, false, entree: true);
            Dsp(0x24, 3, 0);
            TicsEntree(d, 6);
            return $"{Etat82()} {Irr()} 8bits={d.sb_8_enable}";
        }, "82h=03 IRR7=1 8bits=0", "82h=03 IRR7=0 8bits=0");
        bad += Un("SB 16 : F2h, INTA, puis B8h 00h, quatre mots en entrée, jusqu'au bout du bloc", d =>
        {
            Dsp(0xf2);
            Prise();
            Dma5(0x20000, 3, false, entree: true);
            Dsp(0xb8, 0x00, 3, 0);
            TicsEntree(d, 6);
            return $"{Etat82()} {Irr()} 16bits={d.sb_16_enable}";
        }, "82h=03 IRR7=1 16bits=0", "82h=03 IRR7=0 16bits=0");
        // Sous PB-239, le second 14h va en file et repart à la fin du premier bloc, dont l'IRQ passe par la levée du mode
        // matériel (fin_file_materiel) ; sans lui, il relance sur-le-champ, et la fin de bloc est celle de pollsb.
        bad += Un("SB 2.0 : F2h, INTA, 14h de quatre octets, puis un second 14h au deuxième tic, jusqu'au bout", d =>
        {
            Dsp(0xf2);
            Prise();
            Dma1(0x20000, 15, false);
            Dsp(0x14, 3, 0);
            Tics(d, 2);
            Dsp(0x14, 3, 0);
            Tics(d, 10);
            return $"{Irr()} 8bits={d.sb_8_enable}";
        }, "IRR7=1 8bits=0", "IRR7=0 8bits=0", carte: "sb2.0");
        bad += Un("SB 16 : 3Fh au MPU, INTA, puis F2h", _ =>
        {
            io.outb(0x331, 0x3f);
            if ((pic.pic_.pend & 0x80) != 0)
                Prise();
            Dsp(0xf2);
            return $"{Etat82()} {Irr()}";
        }, materiel.pb_154 ? "82h=05 IRR7=1" : "82h=01 IRR7=1", materiel.pb_154 ? "82h=05 IRR7=0" : "82h=01 IRR7=1",
           temoin: !materiel.pb_154);
        bad += Un("SB 16 : F2h, 2xEh lu, puis F2h (témoin)", _ =>
        {
            Dsp(0xf2);
            io.inb(Sb + 0xe);
            Dsp(0xf2);
            return Irr();
        }, "", "IRR7=1", temoin: true);
        bad += Un("SB 16 : F2h, INTA, 2xEh lu, puis F2h (témoin)", _ =>
        {
            Dsp(0xf2);
            Prise();
            io.inb(Sb + 0xe);
            Dsp(0xf2);
            return Irr();
        }, "", "IRR7=1", temoin: true);
        bad += Un("SB 16 : F2h, INTA, le reset du DSP, puis F2h (témoin)", _ =>
        {
            Dsp(0xf2);
            Prise();
            ResetDsp();
            LireDsp();
            Dsp(0xf2);
            return $"{Etat82()} {Irr()}";
        }, "", "82h=01 IRR7=1", temoin: true);
        bad += Un("SB 16 : F2h, INTA, 80h=02h (IRQ 5), puis F2h (témoin)", _ =>
        {
            Dsp(0xf2);
            Prise();
            MelEcrit(0x224, 0x80, 0x02);
            Dsp(0xf2);
            return $"{Irr(5)} {Irr()}";
        }, "", "IRR5=1 IRR7=0", temoin: true);
        return bad;
    }
}
