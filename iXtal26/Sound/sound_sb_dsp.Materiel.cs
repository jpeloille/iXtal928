// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — le code propre au mode matériel du DSP des Sound Blaster (G13 ; R10 de TRANSCRIPTION.md).
// STATUS: materiel
//
// Les corrections des défauts de PCem dans sound_sb_dsp.c, appelées par les gardes de sound_sb_dsp.cs
// (`if (materiel.pb_nn)`), seules sur leur ligne. La source, le cas qui discrimine et la panne qui le rougit sont dans
// l'entrée PB de PCEM_BUGS.md. Les références : le guide de Creative (Sound Blaster Series Hardware Programming Guide,
// 1993), chapitre 2 (le reset), chapitre 5 (la MIDI) et chapitre 6 (les commandes) ; les micrologiciels désassemblés
// (décision n° 8 de PLAN-G12.md) : 2.02 (sbv202.asm, Schlaepfer), 3.02, 4.05 et 4.13 (CT1741_DSP). Les méthodes portent
// NoInlining (M2, G13.7c2).
//
// L'état que PCem n'a pas (la grande vitesse, le mode UART, l'octet MIDI attendu) vit dans une table à part (EtatDsp) :
// un champ de plus dans sb_dsp_t changerait sa disposition, et le code que le JIT tire des méthodes qui le lisent.
// `sb_commands`, statique et partagé, n'est jamais écrit ici.

using System.Runtime.CompilerServices;
using static iXtal26.Sound.sound_mpu401_uart;
using static iXtal26.timer;

namespace iXtal26.Sound;

internal static partial class sound_sb_dsp
{
    private static class EtatDsp
    {
        internal sealed class Etat
        {
            /// <summary>90h, 91h, 98h ou 99h reçue d'un DSP 2.01 à 3.xx : la grande vitesse dure tant que le DMA
            /// tourne ; le micrologiciel a posé sa signature de reset « chaud » (PB-241).</summary>
            internal bool grande_vitesse;
            /// <summary>34h à 37h reçue : le mode UART de sortie, dont seul un reset fait sortir (PB-240).</summary>
            internal bool uart;
            /// <summary>38h reçue : l'octet suivant part à la sortie MIDI (PB-240).</summary>
            internal bool midi_attendu;
        }

        internal static readonly ConditionalWeakTable<sb_dsp_t, Etat> Etats = new();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static EtatDsp.Etat etat_materiel(sb_dsp_t dsp) => EtatDsp.Etats.GetOrCreateValue(dsp);

    /// <summary>Le reset de PCem, sans le bloc, le haut-parleur ni l'octet d'identification de E2h : ce que le reset
    /// « chaud » et le reset « froid » du mode matériel partagent.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void reset_commun_materiel(sb_dsp_t dsp)
    {
        timer_disable(dsp.output_timer);
        timer_disable(dsp.input_timer);

        dsp.sb_command = 0;

        sb_irqc(dsp, 0);
        sb_irqc(dsp, 1);
        dsp.sb_16_pause = 0;
        dsp.sb_read_wp = dsp.sb_read_rp = 0;
        dsp.sb_data_stat = -1;
        dsp.sb_pausetime = -1;

        dsp.sbreset = 0;

        dsp.record_pos_read = 0;
        dsp.record_pos_write = SB_DSP_REC_SAFEFTY_MARGIN;

        Models.pic.picintc((uint16_t)(1 << dsp.sb_irqnum));

        dsp.asp_data_len = 0;
    }

    // ===== G13.8a — l'état du DSP =====

    /// <summary>PB-236 : le reset « froid », au démarrage et par 2x6h — l'état du guide (p. 2-2, « returns it to its
    /// default state ») et des micrologiciels : le bloc à 07FFh (2.02 `cold_boot`, 3.02, 4.05 en 0432h) ; avant le 4.xx,
    /// la constante de temps à 9Ch, 10 kHz (2.02 `:899`) et la sortie coupée (la broche de coupure, P2.0, à 1 après le
    /// reset du 8051 ; `cold_boot` ne la baisse pas). Sur la SB 1.0, la coupure suit D1h et D3h (PB-147) : sans elle, rien
    /// ne la lèverait. La SB 16 n'a pas de coupure (D1h et D3h n'y font rien), et sa constante au reset est inconnue :
    /// laissée.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool reset_materiel(sb_dsp_t dsp)
    {
        ModeMateriel.Sonde[236]++;
        reset_commun_materiel(dsp);
        dsp.sbe2 = 0xAA;
        dsp.sbe2count = 0;
        dsp.sb_speaker = 0;
        dsp.sb_8_length = 0xffff;
        dsp.sb_8_autolen = 0x07ff;
        if (dsp.sb_type < SB16)
        {
                dsp.sb_timei = dsp.sb_timeo = 0x9c;
                dsp.sblatcho = dsp.sblatchi = TIMER_USEC * (uint64_t)(256 - 0x9c);
                dsp.sb_freq = 1000000 / (256 - 0x9c);
                if (dsp.sb_type >= SB15 || materiel.pb_147)
                        dsp.muted = 1;
        }
        return true;
    }

    /// <summary>PB-241 : la grande vitesse (90h, 91h, 98h, 99h) — sur un DSP 2.01 à 3.xx, « the DSP will not accept
    /// any other commands » (guide, p. 6-20) ; le 2.02 tourne en `X0748` jusqu'à la fin du bloc de 91h et 99h. Tous, du
    /// 2.01 au 4.13, y posent leur signature de reset « chaud » (34h 12h : 2.02 `cmdg_hs_e`, 4.05 `hs_dma_continuous`,
    /// 4.13) ; le 4.xx continue de prendre les commandes. En tête de sb_exec_command.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void grande_vitesse_pose_materiel(sb_dsp_t dsp)
    {
        if (dsp.sb_type < SB2)
                return;
        if (dsp.sb_command is not (0x90 or 0x91 or 0x98 or 0x99))
                return;
        ModeMateriel.Sonde[241]++;
        etat_materiel(dsp).grande_vitesse = true;
    }

    /// <summary>PB-241 : la grande vitesse dure tant que le DMA tourne ; la fin du bloc de 91h ou de 99h, qui coupe le
    /// DMA, rend le DSP aux commandes (le 2.02 efface alors sa signature, `X0052` ; le 4.05 de même).</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool grande_vitesse_materiel(sb_dsp_t dsp)
    {
        var e = etat_materiel(dsp);
        if (e.grande_vitesse && dsp.sb_8_enable == 0)
                e.grande_vitesse = false;
        return e.grande_vitesse;
    }

    /// <summary>PB-241 : en grande vitesse, l'octet écrit à 2xCh n'est pas pris par un DSP 2.01 à 3.xx (il ne lit plus
    /// sa boîte). Ce que 2xCh rend alors reste celui de PCem : que la broche d'occupation du 2.02 soit son bit 7 n'est
    /// pas documenté.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool commande_ignoree_materiel(sb_dsp_t dsp)
    {
        if (dsp.sb_type > SBPRO2 || !grande_vitesse_materiel(dsp))
                return false;
        ModeMateriel.Sonde[241]++;
        return true;
    }

    /// <summary>PB-241 et PB-240 : le reset reçu en grande vitesse ou en mode UART est « chaud » — le micrologiciel
    /// trouve sa signature et saute l'initialisation (2.02 `start`, `:873-887` ; 4.05 de même) : le bloc, la constante
    /// de temps, le haut-parleur et l'octet d'identification de E2h gardés ; le DMA arrêté, AAh rendu. Avant le 4.xx,
    /// la sortie, coupée par le reset du 8051, n'est rouverte que si le haut-parleur était actif (2.02, `:883-886`).
    /// Chaque PB efface son propre état à chaque reset, chaud ou non.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool reset_chaud_materiel(sb_dsp_t dsp, int pb)
    {
        var e = etat_materiel(dsp);
        bool chaud;
        if (pb == 241)
        {
                chaud = grande_vitesse_materiel(dsp);
                e.grande_vitesse = false;
        }
        else
        {
                chaud = e.uart;
                e.uart = false;
                e.midi_attendu = false;
        }
        if (!chaud)
                return false;
        ModeMateriel.Sonde[pb]++;
        reset_commun_materiel(dsp);
        dsp.sb_8_enable = 0;
        if (dsp.sb_type < SB16)
                dsp.muted = dsp.sb_speaker != 0 ? 0 : 1;
        return true;
    }

    /// <summary>PB-240 : la MIDI de la SB (guide, p. 5-3, p. 6-12 à 6-14) — 38h envoie l'octet suivant à la sortie
    /// MIDI (2.02 `cmd_midi_write_poll`) ; 34h à 37h (DSP 2.00 et plus) ouvrent le mode UART, où chaque octet écrit part
    /// à la sortie MIDI jusqu'au reset (2.02 `midi_main_loop`). L'entrée (30h, 31h, et celle du mode UART) reste
    /// celle de PCem, faute de source MIDI.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool midi_materiel(sb_dsp_t dsp, uint8_t v)
    {
        var e = etat_materiel(dsp);
        if (e.uart || e.midi_attendu)
        {
                ModeMateriel.Sonde[240]++;
                e.midi_attendu = false;
                timer_set_delay_u64(dsp.wb_timer, TIMER_USEC * 1);
                midi_write(v);
                return true;
        }
        if (dsp.asp_data_len != 0 || dsp.sb_data_stat != -1)
                return false;
        if (v == 0x38)
                e.midi_attendu = true;
        else if (v >= 0x34 && v <= 0x37 && dsp.sb_type >= SB15)
                e.uart = true;
        else
                return false;
        ModeMateriel.Sonde[240]++;
        timer_set_delay_u64(dsp.wb_timer, TIMER_USEC * 1);
        return true;
    }

    /// <summary>PB-165 : 08h sur la SB 16 (DSP 4.05) ne lit aucun paramètre : `cmd_csp_version` (`:1326-1330`, 06C5h)
    /// rend le port 82h de son bus X sans appeler `dsp_data_read`, comme de 4.04 à 4.16. La valeur rendue reste celle
    /// de PCem (18h), inconnue sans la puce. `sb_commands[8]`, partagé, n'est pas touché : la commande s'exécute ici,
    /// dès son octet.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool asp_version_materiel(sb_dsp_t dsp, uint8_t v)
    {
        if (v != 0x08 || dsp.sb_type != SB16 || dsp.asp_data_len != 0 || dsp.sb_data_stat != -1)
                return false;
        ModeMateriel.Sonde[165]++;
        timer_set_delay_u64(dsp.wb_timer, TIMER_USEC * 1);
        dsp.sb_command = v;
        sb_exec_command(dsp);
        return true;
    }

    /// <summary>PB-147 : sur la SB 1.0, D1h relie et D3h coupe aussi la sortie du CNA (guide, p. 6-25 : « The speaker
    /// here refers to the connection of the digitized sound output to the amplifier input », 1.xx coché ; 2.02 et
    /// 3.02 commandent la broche de coupure, déduit pour le 1.05). La pause du DMA que fait PCem, fidèle, suit.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void haut_parleur_materiel(sb_dsp_t dsp, int actif)
    {
        if (dsp.sb_type >= SB15)
                return;
        ModeMateriel.Sonde[147]++;
        dsp.muted = actif != 0 ? 0 : 1;
    }
}
