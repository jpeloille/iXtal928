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
using static iXtal26.Models.dma;
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
            /// <summary>Le bloc ADPCM du DSP (PB-234) : les échantillons qui restent dans l'octet en cours (`r3` du
            /// micrologiciel), et cet octet, qui tourne à chaque échantillon (`r6`).</summary>
            internal int r3, r6;
            /// <summary>L'octet que le DSP attend du 8237 (PB-235) : aucun, la référence, le premier octet, un octet
            /// de données, ou celui qui recharge un bloc automatique.</summary>
            internal int attente;
            /// <summary>La longueur d'une commande en simple cycle mise en file pendant un transfert de sortie, 8 et 16
            /// bits (PB-239) ; -1 sans file.</summary>
            internal int file8 = -1, file16 = -1;
            /// <summary>DAh reçue d'un DSP 1.05 à 3.xx (PB-239) : le bloc en cours finit le transfert, file comprise
            /// (2.02 `autoinit_exit`, testé avant `dma_mode_on`).</summary>
            internal bool sortie8;
            /// <summary>81h du CT1745 écrit avec un seul canal 8 bits et aucun canal 16 bits : le 16 bits passe par le
            /// canal 8 bits, deux octets par échantillon (PB-153). Ni le reset du mélangeur ni celui du DSP ne
            /// l'arrêtent : PCem garde 80h et 81h à l'un et à l'autre, et le guide ne dit rien de leur reset.</summary>
            internal bool traduit;
            /// <summary>L'IRQ du MPU-401 de la carte, pendante (PB-154) : levée par l'octet que le MPU rend, effacée par
            /// la lecture de 330h ; la troisième source de la ligne partagée (PB-238), que 82h montre au bit 2.</summary>
            internal bool mpu;
            /// <summary>Le reset du DSP rejoué pendant que le MPU tient la ligne (PB-238) : il ne la baisse pas.</summary>
            internal bool reset_ligne;
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

    // ===== G13.8b — l'ADPCM =====

    private const int ATTENTE_AUCUNE = 0, ATTENTE_REFERENCE = 1, ATTENTE_PREMIER = 2, ATTENTE_DONNEE = 3,
                      ATTENTE_RECHARGE = 4;

    /// <summary>Les tables de PB-284, dans une classe à part : un champ statique de plus dans sound_sb_dsp changerait
    /// son constructeur statique, et le code que le JIT tire des méthodes qui lisent ses tables (M2 :
    /// `low_fir_sb16_coef` n'y était plus une constante).</summary>
    private static class TablesAdpcm
    {
        /// <summary>PB-284 : la cinquième rangée du 2,6 bits au pas `r5` = 10h (`delta = m × 16 + 8`).</summary>
        internal static readonly int8_t[] scaleMap26 = {0,  1,  2,   3,   0, -1, -2, -3, 1,  3,   5,   7,   -1, -3, -5, -7, 2,  6,   10,  14,
                                                        -2, -6, -10, -14, 4, 12, 20, 28, -4, -12, -20, -28, 8,  24, 40, 56, -8, -24, -40, -56};
        /// <summary>PB-284 : la sixième rangée du 2 bits au pas `r5` = 20h (« 00 » ajoute 16).</summary>
        internal static readonly int8_t[] scaleMap2 = {0, 1, 0, -1, 1, 3, -1, -3, 2, 6, -2, -6, 4, 12, -4, -12, 8, 24, -8, -24, 16, 48, -16, -48};
    }

    /// <summary>Les lectures ADPCM du mode matériel : celles de PCem, ou, sous PB-90, sans le drapeau de fin de
    /// compte.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int lecture_adpcm_materiel(sb_dsp_t dsp)
    {
        if (!materiel.pb_90)
                return sb_8_read_dma(dsp);
        return lecture_materiel(dsp);
    }

    /// <summary>PB-90 : le terme du compte sort sur la broche EOP du 8237, pas dans l'octet (fiche 8237A, tableau 1) ;
    /// le DSP lit l'octet seul (le drapeau seul retiré : un canal de 16 bits garde son mot). `DMA_NODATA` passe tel
    /// quel. Les chemins PCM, que le drapeau ne touche pas (il sort des 16 bits), restent ceux de PCem : sb_8_read_dma,
    /// que le JIT inline, ne porte pas de garde (PLAN-G13.md, la règle de placement).</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int lecture_materiel(sb_dsp_t dsp)
    {
        int v = dma_channel_read(dsp.sb_8_dmanum);
        if (v == DMA_NODATA || (v & DMA_OVER) == 0)
                return v;
        ModeMateriel.Sonde[90]++;
        return v & ~DMA_OVER;
    }

    /// <summary>PB-235 : l'octet que le 8237 ne sert pas, le DSP l'attend (2.02 `X01ac`, `X0840`) : ni octet fictif,
    /// ni compte.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool attend_materiel(int v)
    {
        if (v != DMA_NODATA || !materiel.pb_235)
                return false;
        ModeMateriel.Sonde[235]++;
        return true;
    }

    /// <summary>La sortie d'un échantillon, comme `pollsb` (la Pro en stéréo alterne les voies).</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void sortie_materiel(sb_dsp_t dsp)
    {
        dsp.sbdat = (int16_t)((dsp.sbref ^ 0x80) << 8);
        if (dsp.sb_type >= SBPRO && dsp.sb_type < SB16 && dsp.stereo != 0) {
                if (dsp.sbleftright != 0)
                        dsp.sbdatl = dsp.sbdat;
                else
                        dsp.sbdatr = dsp.sbdat;
                dsp.sbleftright = dsp.sbleftright == 0 ? 1 : 0;
        } else
                dsp.sbdatl = dsp.sbdatr = dsp.sbdat;
    }

    /// <summary>Un pas du décodeur : l'indice (le signe et l'amplitude) sur la rangée du pas, comme `pollsb` ; les
    /// tables de PB-284 en mode matériel.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void pas_materiel(sb_dsp_t dsp, int indice, int8_t[] echelle, uint8_t[] ajustement, int max)
    {
        int tempi = indice + dsp.sbstep;
        if (tempi < 0)
                tempi = 0;
        if (tempi > max)
                tempi = max;
        int @ref = dsp.sbref + echelle[tempi];
        if (@ref > 0xff)
                dsp.sbref = 0xff;
        else if (@ref < 0x00)
                dsp.sbref = 0x00;
        else
                dsp.sbref = (uint8_t)@ref;
        dsp.sbstep = (int8_t)((dsp.sbstep + ajustement[tempi]) & 0xff);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int8_t[] echelle26_materiel()
    {
        if (!materiel.pb_284)
                return scaleMap26;
        ModeMateriel.Sonde[284]++;
        return TablesAdpcm.scaleMap26;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int8_t[] echelle2_materiel()
    {
        if (!materiel.pb_284)
                return scaleMap2;
        ModeMateriel.Sonde[284]++;
        return TablesAdpcm.scaleMap2;
    }

    /// <summary>PB-284 : le troisième échantillon d'un octet de 2,6 bits — le signe au bit 1, l'amplitude au bit 0
    /// seul (2.02 `adpcm_2_6_decode`, `cjne r3,#1` puis `anl a,#1`) ; PCem, l'amplitude 2 × bit 0. `octet` porte le
    /// signe en bit 2 et le bit 0 en bit 1, comme `(sbdat2 << 1) & 7`.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int troisieme_materiel(int indice)
    {
        if (!materiel.pb_284)
                return indice;
        ModeMateriel.Sonde[284]++;
        return (indice & 4) | ((indice >> 1) & 1);
    }

    /// <summary>PB-91, PB-235, PB-284, PB-90 sans PB-234 : le tic ADPCM de PCem (`pollsb`, :923-1031), l'octet lu à la fin du
    /// précédent, chacune de ces corrections à sa place. Toutes coupées, c'est le tic de PCem ; la fin du bloc reste
    /// celle de `pollsb`, après le `switch`. L'octet non servi (PB-235) est compté une fois, à la demande, comme le DSP
    /// décompte avant de demander ; mais si c'est le dernier du bloc, la fin de `pollsb` le termine sans l'attendre
    /// (la fin de bloc du DSP est PB-234).</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool adpcm_pcem_materiel(sb_dsp_t dsp)
    {
        if (dsp.sbdat2 == DMA_NODATA && materiel.pb_235) {
                int v = lecture_adpcm_materiel(dsp);
                if (attend_materiel(v))
                        return true;
                dsp.sbdat2 = v;
        }
        switch (dsp.sb_8_format) {
        case ADPCM_4:
                pas_materiel(dsp, dsp.sbdacpos != 0 ? dsp.sbdat2 & 0xF : dsp.sbdat2 >> 4, scaleMap4, adjustMap4, 63);
                if (++dsp.sbdacpos >= 2) {
                        dsp.sbdacpos = 0;
                        dsp.sbdat2 = lecture_adpcm_materiel(dsp);
                        dsp.sb_8_length--;
                }
                break;
        case ADPCM_26:
                pas_materiel(dsp, dsp.sbdacpos == 0 ? dsp.sbdat2 >> 5 : dsp.sbdacpos == 1 ? (dsp.sbdat2 >> 2) & 7
                                : troisieme_materiel((dsp.sbdat2 << 1) & 7), echelle26_materiel(), adjustMap26, 39);
                if (++dsp.sbdacpos >= 3) {
                        dsp.sbdacpos = 0;
                        dsp.sbdat2 = lecture_adpcm_materiel(dsp);
                        dsp.sb_8_length--;
                }
                break;
        default:
                pas_materiel(dsp, (dsp.sbdat2 >> ((3 - dsp.sbdacpos) * 2)) & 3, echelle2_materiel(), adjustMap2, 23);
                if (++dsp.sbdacpos >= 4) {
                        dsp.sbdacpos = 0;
                        dsp.sbdat2 = lecture_adpcm_materiel(dsp);
                        if (materiel.pb_91)
                                compte2_materiel(dsp);
                }
                break;
        }
        sortie_materiel(dsp);
        return true;
    }

    /// <summary>PB-91 : l'ADPCM 2 bits décrémente son compte à chaque octet lu (2.02 `X01a2`, `X01a4`), comme le 4 et
    /// le 2,6 bits.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void compte2_materiel(sb_dsp_t dsp)
    {
        ModeMateriel.Sonde[91]++;
        dsp.sb_8_length--;
    }

    /// <summary>PB-234 : le décodage d'un échantillon par le micrologiciel — l'octet `r6` tourne : de 4 bits en 4
    /// bits (`swap`), de 3 en 3 en 2,6 bits (le troisième, `r3` = 1, sur 2 bits), de 2 en 2 en 2 bits. L'indice est
    /// celui de `pollsb` ; les mêmes tables (K3 ; PB-284 pour le 2 et le 2,6 bits).</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void decode_materiel(sb_dsp_t dsp, EtatDsp.Etat e)
    {
        int r6 = e.r6;
        switch (dsp.sb_8_format) {
        case ADPCM_4:
                pas_materiel(dsp, r6 >> 4, scaleMap4, adjustMap4, 63);
                e.r6 = ((r6 & 0x0f) << 4) | ((r6 >> 4) & 0x0f);
                break;
        case ADPCM_26:
                if (e.r3 == 1) {
                        pas_materiel(dsp, troisieme_materiel(((r6 >> 5) & 4) | ((r6 >> 5) & 2)), echelle26_materiel(),
                                     adjustMap26, 39);
                        break;
                }
                pas_materiel(dsp, r6 >> 5, echelle26_materiel(), adjustMap26, 39);
                e.r6 = ((r6 << 3) | ((r6 & 0xff) >> 5)) & 0xff;
                break;
        default:
                pas_materiel(dsp, (r6 >> 6) & 3, echelle2_materiel(), adjustMap2, 23);
                e.r6 = (r6 << 2) & 0xff;
                break;
        }
        sortie_materiel(dsp);
    }

    /// <summary>Les échantillons d'un octet, que `r3` reçoit quand le DSP lit un octet de données.</summary>
    private static int par_octet_materiel(sb_dsp_t dsp) => dsp.sb_8_format switch { ADPCM_4 => 2, ADPCM_26 => 3, _ => 4 };

    /// <summary>PB-234 : la commande ADPCM, comme le micrologiciel la lance (2.02 `:1555-1600`, `:1659-1700` ; les
    /// mêmes constantes en 3.02, 4.05 et 4.13). Avec référence, l'octet va au CNA aussitôt, le pas à 1 (`sbstep` 0) et
    /// `r3` à 1 : le premier tic lit le premier octet de données. Sans référence, le premier octet est lu pendant la
    /// commande, sans toucher au compte, et `r3` part de 4 (2 et 4 bits) ou de 3 (2,6 bits). Le compte est celui du
    /// paramètre, N : N octets lus ensuite.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool adpcm_depart_materiel(sb_dsp_t dsp, int format, int autoinit, int len, bool reference)
    {
        ModeMateriel.Sonde[234]++;
        var e = etat_materiel(dsp);
        e.attente = ATTENTE_AUCUNE;
        sb_start_dma(dsp, 1, autoinit, (uint8_t)format, len);
        if (reference) {
                e.r3 = 1;
                int v = lecture_adpcm_materiel(dsp);
                if (attend_materiel(v)) {
                        e.attente = ATTENTE_REFERENCE;
                        return true;
                }
                sb_dsp_update(dsp);
                reference_materiel(dsp, v);
                return true;
        }
        e.r3 = format == ADPCM_26 ? 3 : 4;
        int w = lecture_adpcm_materiel(dsp);
        if (attend_materiel(w)) {
                e.attente = ATTENTE_PREMIER;
                return true;
        }
        e.r6 = w;
        return true;
    }

    /// <summary>PB-234 : l'octet de référence sort aussitôt sur le CNA (2.02 `mov port_dac_out,a`) ; hors de `pollsb`,
    /// le tampon est d'abord rattrapé (sb_dsp_update, comme 10h). En stéréo, la Pro alterne les voies comme chez PCem :
    /// les vecteurs ADPCM n'ont pas de stéréo, l'alternance est celle de la carte, inconnue.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void reference_materiel(sb_dsp_t dsp, int v)
    {
        dsp.sbref = (uint8_t)v;
        dsp.sbstep = 0;
        sortie_materiel(dsp);
    }

    /// <summary>PB-234 et PB-90 : les commandes ADPCM de sortie, en tête de leur case dans sb_exec_command. 1Fh, 7Dh
    /// et 7Fh sans PB-146 gardent les paramètres de PCem (les octets de la commande précédente, sans référence). Sans
    /// PB-234, le départ de PCem, ses octets lus sans le drapeau de PB-90.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool adpcm_commande_materiel(sb_dsp_t dsp)
    {
        int len = dsp.sb_data[0] + (dsp.sb_data[1] << 8);
        if (dsp.sb_command is not (0x1F or 0x7D or 0x7F) && materiel.pb_239 && file_materiel(dsp, 1, len))
                return true;
        if (!materiel.pb_234)
                return adpcm_commande_pcem_materiel(dsp);
        switch (dsp.sb_command) {
        case 0x16: return adpcm_depart_materiel(dsp, ADPCM_2, 0, len, false);
        case 0x17: return adpcm_depart_materiel(dsp, ADPCM_2, 0, len, true);
        case 0x74: return adpcm_depart_materiel(dsp, ADPCM_4, 0, len, false);
        case 0x75: return adpcm_depart_materiel(dsp, ADPCM_4, 0, len, true);
        case 0x76: return adpcm_depart_materiel(dsp, ADPCM_26, 0, len, false);
        case 0x77: return adpcm_depart_materiel(dsp, ADPCM_26, 0, len, true);
        }
        if (dsp.sb_type < SB15)
                return false;
        return adpcm_depart_materiel(dsp, dsp.sb_command == 0x1F ? ADPCM_2 : dsp.sb_command == 0x7D ? ADPCM_4 : ADPCM_26,
                                     1, len, false);
    }

    /// <summary>PB-90 et PB-239 sans PB-234 : le départ de PCem (sound_sb_dsp.c, 16h, 17h, 74h à 77h, 1Fh, 7Dh, 7Fh), le
    /// premier octet de données lu sans le drapeau de fin de compte sous PB-90 ; la référence, tronquée à 8 bits, ne
    /// le gardait pas.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool adpcm_commande_pcem_materiel(sb_dsp_t dsp)
    {
        int len = dsp.sb_data[0] + (dsp.sb_data[1] << 8);
        int c = dsp.sb_command;
        int format = c is 0x74 or 0x75 or 0x7D ? ADPCM_4 : c is 0x76 or 0x77 or 0x7F ? ADPCM_26 : ADPCM_2;
        int autoinit = c is 0x1F or 0x7D or 0x7F ? 1 : 0;
        if (autoinit != 0 && dsp.sb_type < SB15)
                return false;
        if (c is 0x17 or 0x75 or 0x77) {
                dsp.sbref = (uint8_t)lecture_adpcm_materiel(dsp);
                dsp.sbstep = 0;
        }
        sb_start_dma(dsp, 1, autoinit, (uint8_t)format, len);
        dsp.sbdat2 = lecture_adpcm_materiel(dsp);
        dsp.sb_8_length--;
        if (c is 0x17 or 0x75 or 0x77)
                dsp.sb_8_length--;
        return true;
    }

    /// <summary>PB-146 : 1Fh, 2Ch, 7Dh et 7Fh n'ont pas de paramètre ; le bloc est celui de 48h (guide, p. 6-8 à 6-19 ;
    /// 2.02 `:1395-1398`, `:1612-1616`), et 1Fh, 7Dh et 7Fh lisent d'abord l'octet de référence (« with reference
    /// byte » ; 2.02 `:1659-1672`). Sans PB-234, le départ de 17h, 75h et 77h de PCem, deux octets décomptés.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool automatique_materiel(sb_dsp_t dsp)
    {
        if (dsp.sb_type < SB15)
                return false;
        ModeMateriel.Sonde[146]++;
        if (dsp.sb_command == 0x2C) {
                sb_start_dma_i(dsp, 1, 1, 0, dsp.sb_8_autolen);
                return true;
        }
        int format = dsp.sb_command == 0x1F ? ADPCM_2 : dsp.sb_command == 0x7D ? ADPCM_4 : ADPCM_26;
        if (materiel.pb_234)
                return adpcm_depart_materiel(dsp, format, 1, dsp.sb_8_autolen, true);
        dsp.sbref = (uint8_t)lecture_adpcm_materiel(dsp);
        dsp.sbstep = 0;
        sb_start_dma(dsp, 1, 1, (uint8_t)format, dsp.sb_8_autolen);
        dsp.sbdat2 = lecture_adpcm_materiel(dsp);
        dsp.sb_8_length -= 2;
        return true;
    }

    /// <summary>Le tic ADPCM du mode matériel, en tête des cas ADPCM de `pollsb` sous PB-234, PB-235, PB-91, PB-284 et
    /// PB-90.
    /// Sans PB-234, celui de PCem (adpcm_pcem_materiel). Avec, le vecteur du micrologiciel (2.02
    /// `vector_dma_dac_adpcm2`, `_adpcm4`, `_adpcm2_6`, `sbv202.asm:343-640` ; 3.02, 4.05 et 4.13 de même) : `r3`
    /// décompté ; à zéro, l'octet suivant lu, le compte décrémenté, et décodé dans le même tic ; le compte nul, la fin
    /// du bloc — l'IRQ après le dernier échantillon ; en automatique, le compte de 48h rechargé et le premier octet du
    /// bloc suivant lu, sans décodage dans ce tic. Le compte reste ≥ 0 : le test de `pollsb` après le `switch` ne
    /// joue pas. Rend toujours true.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool adpcm_materiel(sb_dsp_t dsp)
    {
        if (!materiel.pb_234)
                return adpcm_pcem_materiel(dsp);
        ModeMateriel.Sonde[234]++;
        var e = etat_materiel(dsp);
        if (e.attente != ATTENTE_AUCUNE)
                return attente_materiel(dsp, e);
        if (--e.r3 > 0) {
                decode_materiel(dsp, e);
                return true;
        }
        if (dsp.sb_8_length != 0) {
                if (dsp.sb_8_format != ADPCM_2)
                        dsp.sb_8_length--;
                else if (materiel.pb_91)
                        compte2_materiel(dsp);
                e.r3 = par_octet_materiel(dsp);
                int v = lecture_adpcm_materiel(dsp);
                if (attend_materiel(v)) {
                        e.attente = ATTENTE_DONNEE;
                        return true;
                }
                e.r6 = v;
                decode_materiel(dsp, e);
                return true;
        }
        if (sortie_fin_materiel(e))
                dsp.sb_8_autoinit = 0;
        else if (dsp.sb_8_autoinit != 0 || e.file8 >= 0) {
                dsp.sb_8_length = dsp.sb_8_autolen;
                if (e.file8 >= 0)
                        file_reprise_materiel(dsp, e);
                e.r3 = par_octet_materiel(dsp);
                int w = lecture_adpcm_materiel(dsp);
                if (attend_materiel(w)) {
                        e.attente = ATTENTE_RECHARGE;
                        return true;
                }
                e.r6 = w;
                sb_irq(dsp, 1);
                return true;
        }
        dsp.sb_8_enable = 0;
        timer_disable(dsp.output_timer);
        sb_irq(dsp, 1);
        return true;
    }

    /// <summary>PB-235 avec PB-234 : le DSP attend l'octet, et reprend là où il l'attendait — la référence au CNA, le
    /// premier octet rangé, l'octet de données décodé, ou le bloc rechargé et l'IRQ levée.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool attente_materiel(sb_dsp_t dsp, EtatDsp.Etat e)
    {
        int v = lecture_adpcm_materiel(dsp);
        if (attend_materiel(v))
                return true;
        int attente = e.attente;
        e.attente = ATTENTE_AUCUNE;
        if (attente == ATTENTE_REFERENCE) {
                reference_materiel(dsp, v);
                return true;
        }
        e.r6 = v;
        if (attente == ATTENTE_DONNEE)
                decode_materiel(dsp, e);
        else if (attente == ATTENTE_RECHARGE)
                sb_irq(dsp, 1);
        return true;
    }

    // ===== G13.8c1 — l'octet de mode du DSP 4.xx, la commande mise en file =====

    /// <summary>PB-239 : un transfert de sortie en cours, au sens du micrologiciel — 2.0x et 3.xx : tout transfert hors
    /// pause (le 2.02 et le 3.02 testent la broche d'activation du DMA, `cmd_dac_dma`, `cmd_dac_adpcm`, que D0h relâche,
    /// `cmdg_misc`) ; 4.05 et 4.13 : un automatique seulement, en pause ou non (`dma_dac1_normal`, `jnb dma8_mode` ;
    /// B0h à CFh, `X051a` ; un drapeau par largeur, `dma16_mode`). Le transfert vit si sa minuterie tourne : après un
    /// reset, PCem laisse `sb_8_enable` à 1.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool en_cours_materiel(sb_dsp_t dsp, int dma8)
    {
        if (!timer_is_enabled_materiel(dsp))
                return false;
        if (dma8 != 0)
                return dsp.sb_8_enable != 0 && dsp.sb_8_output != 0 &&
                       (dsp.sb_type < SB16 ? dsp.sb_8_pause == 0 : dsp.sb_8_autoinit != 0);
        return dsp.sb_16_enable != 0 && dsp.sb_16_output != 0 && dsp.sb_16_autoinit != 0;
    }

    private static bool timer_is_enabled_materiel(sb_dsp_t dsp) => timer_is_enabled(dsp.output_timer) != 0;

    /// <summary>PB-239 : une commande de sortie en simple cycle reçue pendant un transfert de sortie ne relance pas ;
    /// sa longueur est mise en file (2.02 `:1519-1530`, `:1618-1631` ; 3.02 `:1131-1143` ; 4.05 `:2623-2634`,
    /// `X051a`), le bloc en cours se finit dans son format (le vecteur du DSP ne change pas ; le 4.05 jette l'octet de
    /// mode de B0h à CFh, et le bloc de 48h reste). Une autre commande de la même largeur, en file avant la fin,
    /// remplace la longueur (sauf après B0h à CFh sur le 4.05 : `X051a` efface `dma8_mode`, la suivante part de neuf).
    /// Après DAh, sur les DSP 1.05 à 3.xx, la commande est prise et perdue : le transfert finira avec le bloc. Rend true
    /// si la commande est prise ; sinon, elle part de neuf, et les files, s'il en restait (un départ de l'autre largeur
    /// coupe celle-ci), sont effacées.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool file_materiel(sb_dsp_t dsp, int dma8, int len)
    {
        var e = etat_materiel(dsp);
        if (!en_cours_materiel(dsp, dma8)) {
                file_reset_materiel(dsp);
                return false;
        }
        ModeMateriel.Sonde[239]++;
        if (dma8 != 0 && e.sortie8)
                return true;
        if (dma8 != 0)
                e.file8 = len;
        else
                e.file16 = len;
        return true;
    }

    /// <summary>PB-239 : 14h, en tête de son case.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool dma8_file_materiel(sb_dsp_t dsp) => file_materiel(dsp, 1, dsp.sb_data[0] + (dsp.sb_data[1] << 8));

    /// <summary>PB-239 : la reprise de la file à la fin du bloc — sa longueur, l'automatique coupé (2.02 X011f :
    /// `clr dma_autoinit_on`, `len_left = length`).</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void file_reprise_materiel(sb_dsp_t dsp, EtatDsp.Etat e)
    {
        ModeMateriel.Sonde[239]++;
        dsp.sb_8_length = e.file8;
        dsp.sb_8_autoinit = 0;
        e.file8 = -1;
    }

    /// <summary>PB-239 : en tête de l'état « fin de bloc » de `pollsb`, 8 et 16 bits : avec une file, l'IRQ du bloc
    /// fini, puis le bloc en file dans le même format, sans automatique. Rend true si la file est reprise (`pollsb`
    /// rend alors la main : la sortie de l'autre largeur est éteinte, sb_start_dma l'a coupée, et la pause de 80h ne
    /// court pas pendant un transfert).</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool fin_file_materiel(sb_dsp_t dsp, int dma8)
    {
        var e = etat_materiel(dsp);
        if (dma8 != 0) {
                if (sortie_fin_materiel(e))
                        return false;
                if (e.file8 < 0)
                        return false;
                file_reprise_materiel(dsp, e);
                sb_irq(dsp, 1);
                return true;
        }
        if (e.file16 < 0)
                return false;
        ModeMateriel.Sonde[239]++;
        dsp.sb_16_length = e.file16;
        dsp.sb_16_autoinit = 0;
        e.file16 = -1;
        sb_irq(dsp, 0);
        return true;
    }

    /// <summary>PB-149 et PB-239 : B0h à CFh (DSP 4.xx), en tête de leurs cases. Sous PB-149, l'octet de mode masqué à
    /// ses bits 4 et 5 (guide, p. 6-23 ; 4.05 `cmd_dma8` et `cmd_dma16` ne testent qu'eux, `:1031-1039`,
    /// `:1133-1141`). Sous PB-239, une sortie en simple cycle pendant un automatique de la même largeur va en file, le
    /// bloc de 48h gardé ; sinon la commande de PCem, le bloc posé par la longueur (4.05, `dma8_block_len`).</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool commande_dsp4_materiel(sb_dsp_t dsp)
    {
        if (dsp.sb_type < SB16)
                return false;
        int c = dsp.sb_command;
        int dma8 = (c & 0x40) != 0 ? 1 : 0;
        int autoinit = c & 4;
        int len = dsp.sb_data[1] + (dsp.sb_data[2] << 8);
        int mode = dsp.sb_data[0];
        if (materiel.pb_149 && (mode & ~0x30) != 0) {
                ModeMateriel.Sonde[149]++;
                mode &= 0x30;
        }
        if ((c & 8) != 0) {
                sb_start_dma_i(dsp, dma8, autoinit, (uint8_t)mode, len);
        } else {
                if (autoinit == 0 && materiel.pb_239 && file_materiel(dsp, dma8, len)) {
                        file_dsp4_materiel(dsp, dma8);
                        return true;
                }
                sb_start_dma(dsp, dma8, autoinit, (uint8_t)mode, len);
        }
        if (dma8 != 0)
                dsp.sb_8_autolen = len;
        else
                dsp.sb_16_autolen = len;
        return true;
    }

    /// <summary>PB-239 : un départ neuf efface la file de sa largeur (sb_start_dma n'est pas gardé : le JIT
    /// l'inline peut-être).</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void file_effacee_materiel(sb_dsp_t dsp, int dma8)
    {
        var e = etat_materiel(dsp);
        if (dma8 != 0)
                e.file8 = -1;
        else
                e.file16 = -1;
    }

    /// <summary>PB-239 : le reset arrête le transfert ; la file part avec lui. En tête de sb_dsp_reset.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void file_reset_materiel(sb_dsp_t dsp)
    {
        file_effacee_materiel(dsp, 1);
        file_effacee_materiel(dsp, 0);
        etat_materiel(dsp).sortie8 = false;
    }

    /// <summary>PB-239 : B0h à CFh mise en file sur le 4.05 efface le drapeau de l'automatique (`X051a`, `clr dma8_mode` ;
    /// `dma16_mode` en 16 bits) : le bloc en cours sera le dernier, et la commande suivante part de neuf.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void file_dsp4_materiel(sb_dsp_t dsp, int dma8)
    {
        if (dma8 != 0)
                dsp.sb_8_autoinit = 0;
        else
                dsp.sb_16_autoinit = 0;
    }

    /// <summary>PB-239 : DAh, en tête de son case — sur les DSP 1.05 à 3.xx, le bloc en cours finira le transfert,
    /// même avec une commande en file (2.02 `cmd_exit_autoinit` pose `autoinit_exit`, que les vecteurs testent avant
    /// `dma_mode_on`). Le 4.05 reprend la file (`dma8_active` testé d'abord) : rien à noter.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void sortie_auto_materiel(sb_dsp_t dsp)
    {
        if (dsp.sb_type >= SB16)
                return;
        ModeMateriel.Sonde[239]++;
        etat_materiel(dsp).sortie8 = true;
    }

    /// <summary>PB-239 : à la fin du bloc, après DAh, le transfert s'arrête ; la file est perdue.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool sortie_fin_materiel(EtatDsp.Etat e)
    {
        if (!e.sortie8)
                return false;
        e.sortie8 = false;
        e.file8 = -1;
        return true;
    }

    // ===== G13.8d1 — la traduction du 16 bits vers le canal 8 bits (81h du CT1745) =====

    /// <summary>PB-153 : 81h pose ou arrête la traduction (sound_sb.Materiel.cs).</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void traduction_materiel(sb_dsp_t dsp, bool traduit) => etat_materiel(dsp).traduit = traduit;

    /// <summary>PB-153 : la traduction est posée.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static bool traduit_materiel(sb_dsp_t dsp)
    {
        if (!etat_materiel(dsp).traduit)
                return false;
        ModeMateriel.Sonde[153]++;
        return true;
    }

    /// <summary>PB-153 : 81h relu pendant la traduction — le seul canal 8 bits, aucun bit 16 bits.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static uint8_t dma_lu_materiel(sb_dsp_t dsp) => dsp.sb_8_dmanum switch { 0 => 1, 1 => 2, 3 => 8, _ => 0 };

    /// <summary>PB-153 : un mot lu par le canal 8 bits, l'octet bas d'abord (déduit : l'ordre du x86 ; le guide n'en dit
    /// rien). Sans l'un des deux octets, DMA_NODATA (le bas déjà lu est perdu, comme le premier mot d'une paire stéréo
    /// de PCem ; déduit) ; le DMA_OVER du 8237 disparaît, comme à la conversion en int16_t de sb_16_read_dma.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int mot_lu_materiel(sb_dsp_t dsp)
    {
        int bas = dma_channel_read(dsp.sb_8_dmanum);
        if (bas == DMA_NODATA)
                return DMA_NODATA;
        int haut = dma_channel_read(dsp.sb_8_dmanum);
        if (haut == DMA_NODATA)
                return DMA_NODATA;
        return (bas & 0xFF) | ((haut & 0xFF) << 8);
    }

    /// <summary>PB-153 : un mot écrit par le canal 8 bits, l'octet bas d'abord (déduit, comme à la lecture) ; 1 si le
    /// 8237 ne prend pas le bas (le retour de sb_16_write_dma).</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int mot_ecrit_materiel(sb_dsp_t dsp, uint16_t val)
    {
        if (dma_channel_write(dsp.sb_8_dmanum, (uint16_t)(val & 0xFF)) == DMA_NODATA)
                return 1;
        dma_channel_write(dsp.sb_8_dmanum, (uint16_t)(val >> 8));
        return 0;
    }

    /// <summary>PB-153 : le tic de sortie 16 bits pendant la traduction — celui de PCem (pollsb), les mots lus par le
    /// canal 8 bits ; l'IRQ reste celle du 16 bits (2xFh l'acquitte : déduit, le guide n'en dit rien). Vrai : le tic
    /// est joué.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool seize_traduit_materiel(sb_dsp_t dsp)
    {
        if (!traduit_materiel(dsp))
                return false;
        int g, d;

        sb_dsp_update(dsp);

        switch (dsp.sb_16_format) {
        case 0x00: /*Mono unsigned*/
                g = mot_lu_materiel(dsp);
                if (g == DMA_NODATA)
                        break;
                dsp.sbdatl = dsp.sbdatr = (int16_t)(g ^ 0x8000);
                dsp.sb_16_length--;
                break;
        case 0x10: /*Mono signed*/
                g = mot_lu_materiel(dsp);
                if (g == DMA_NODATA)
                        break;
                dsp.sbdatl = dsp.sbdatr = (int16_t)g;
                dsp.sb_16_length--;
                break;
        case 0x20: /*Stereo unsigned*/
                g = mot_lu_materiel(dsp);
                d = mot_lu_materiel(dsp);
                if (g == DMA_NODATA || d == DMA_NODATA)
                        break;
                dsp.sbdatl = (int16_t)(g ^ 0x8000);
                dsp.sbdatr = (int16_t)(d ^ 0x8000);
                dsp.sb_16_length -= 2;
                break;
        case 0x30: /*Stereo signed*/
                g = mot_lu_materiel(dsp);
                d = mot_lu_materiel(dsp);
                if (g == DMA_NODATA || d == DMA_NODATA)
                        break;
                dsp.sbdatl = (int16_t)g;
                dsp.sbdatr = (int16_t)d;
                dsp.sb_16_length -= 2;
                break;
        }

        if (dsp.sb_16_length < 0) {
                if (materiel.pb_239)
                        if (fin_file_materiel(dsp, 0))
                                return true;
                if (dsp.sb_16_autoinit != 0)
                        dsp.sb_16_length = dsp.sb_16_autolen;
                else {
                        dsp.sb_16_enable = 0;
                        timer_disable(dsp.output_timer);
                }
                sb_irq(dsp, 0);
        }
        return true;
    }

    /// <summary>PB-153 : le tic d'entrée 16 bits pendant la traduction — celui de PCem (sb_poll_i), les mots écrits par
    /// le canal 8 bits ; PB-151 (record_lu) y reste reproduit. Vrai : le tic est joué.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool seize_traduit_i_materiel(sb_dsp_t dsp)
    {
        if (!traduit_materiel(dsp))
                return false;
        switch (dsp.sb_16_format) {
        case 0x00: /*Unsigned mono*/
                if (mot_ecrit_materiel(dsp, (uint16_t)(dsp.record_buffer[dsp.record_pos_read] ^ 0x8000)) != 0)
                        return true;
                dsp.sb_16_length--;
                dsp.record_pos_read += 2;
                dsp.record_pos_read &= 0xFFFF;
                break;
        case 0x10: /*Signed mono*/
                if (mot_ecrit_materiel(dsp, (uint16_t)dsp.record_buffer[dsp.record_pos_read]) != 0)
                        return true;
                dsp.sb_16_length--;
                dsp.record_pos_read += 2;
                dsp.record_pos_read &= 0xFFFF;
                break;
        case 0x20: /*Unsigned stereo*/
                if (mot_ecrit_materiel(dsp, (uint16_t)(dsp.record_buffer[dsp.record_pos_read] ^ 0x8000)) != 0)
                        return true;
                mot_ecrit_materiel(dsp, (uint16_t)(record_lu(dsp, dsp.record_pos_read + 1) ^ 0x8000));
                dsp.sb_16_length -= 2;
                dsp.record_pos_read += 2;
                dsp.record_pos_read &= 0xFFFF;
                break;
        case 0x30: /*Signed stereo*/
                if (mot_ecrit_materiel(dsp, (uint16_t)dsp.record_buffer[dsp.record_pos_read]) != 0)
                        return true;
                mot_ecrit_materiel(dsp, (uint16_t)record_lu(dsp, dsp.record_pos_read + 1));
                dsp.sb_16_length -= 2;
                dsp.record_pos_read += 2;
                dsp.record_pos_read &= 0xFFFF;
                break;
        }

        if (dsp.sb_16_length < 0) {
                if (dsp.sb_16_autoinit != 0)
                        dsp.sb_16_length = dsp.sb_16_autolen;
                else {
                        dsp.sb_16_enable = 0;
                        timer_disable(dsp.input_timer);
                }
                sb_irq(dsp, 0);
        }
        return true;
    }

    // ===== G13.8d2 — la ligne d'IRQ partagée : 2xEh, 2xFh, le reset et le MPU-401 =====

    /// <summary>La ligne d'IRQ que partagent le 8 bits (et SB-MIDI), le 16 bits et le MPU-401 (guide, p. 2-5) : elle est
    /// tenue tant qu'une source est pendante (le OU des trois, déduit).</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool ligne_tenue_materiel(sb_dsp_t dsp) =>
        dsp.sb_irq8 != 0 || dsp.sb_irq16 != 0 || etat_materiel(dsp).mpu;

    /// <summary>PB-238 : la lecture de 2xEh n'acquitte que l'IRQ 8 bits (et SB-MIDI) ; la ligne ne retombe au PIC que si
    /// ni l'IRQ 16 bits ni le MPU ne la tiennent (guide, p. 2-5). Rend l'état de PCem (bit 7 : un octet à lire).</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static uint8_t lit_2xe_materiel(sb_dsp_t dsp)
    {
        ModeMateriel.Sonde[238]++;
        dsp.sb_irq8 = 0;
        if (!ligne_tenue_materiel(dsp))
            Models.pic.picintc((uint16_t)(1 << dsp.sb_irqnum));
        if (IS_AZTECH(dsp))
            return (uint8_t)((dsp.sb_read_rp == dsp.sb_read_wp) ? 0x00 : 0x80);
        return (uint8_t)((dsp.sb_read_rp == dsp.sb_read_wp) ? 0x7F : 0xFF);
    }

    /// <summary>PB-238 : la lecture de 2xFh acquitte l'IRQ 16 bits ; la ligne retombe si ni l'IRQ 8 bits ni le MPU ne la
    /// tiennent (PCem ne regarde que l'IRQ 8 bits : le MPU n'a pas d'IRQ chez lui).</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static uint8_t lit_2xf_materiel(sb_dsp_t dsp)
    {
        ModeMateriel.Sonde[238]++;
        dsp.sb_irq16 = 0;
        if (!ligne_tenue_materiel(dsp))
            Models.pic.picintc((uint16_t)(1 << dsp.sb_irqnum));
        return 0xff;
    }

    /// <summary>PB-238 : le reset du DSP efface ses deux IRQ ; quand le MPU tient la ligne, elle ne retombe pas. Le reset
    /// est rejoué l'IRQ de la carte hors du PIC : 1 &lt;&lt; 16 vaut 0 dans un uint16_t, et picintc(0) ne fait rien.
    /// Vrai : le reset est fait.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool reset_ligne_materiel(sb_dsp_t dsp)
    {
        var e = etat_materiel(dsp);
        if (!e.mpu || e.reset_ligne)
            return false;
        ModeMateriel.Sonde[238]++;
        int irq = dsp.sb_irqnum;
        e.reset_ligne = true;
        dsp.sb_irqnum = 16;
        sb_dsp_reset(dsp);
        dsp.sb_irqnum = irq;
        e.reset_ligne = false;
        return true;
    }

    /// <summary>PB-154 : le MPU lève son IRQ, sur la ligne de la carte (celle que 80h choisit) ; le PIC ne la voit monter
    /// que si aucune autre source ne la tenait (une ligne sur front : déduit).</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void mpu_leve_materiel(sb_dsp_t dsp)
    {
        bool tenue = ligne_tenue_materiel(dsp);
        etat_materiel(dsp).mpu = true;
        if (!tenue)
            Models.pic.picint((uint16_t)(1 << dsp.sb_irqnum));
    }

    /// <summary>PB-154 : la lecture de 330h efface l'IRQ du MPU (guide, p. 5-9 et 2-5) ; la ligne retombe si aucune
    /// autre source ne la tient. Sans IRQ pendante, rien. 80h changé pendant qu'une source est pendante : la ligne
    /// baissée est la nouvelle, et l'ancienne reste au PIC, comme chez PCem pour les IRQ du DSP (sb_dsp_setirq ne
    /// touche pas au PIC ; la carte, inconnue).</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void mpu_acquitte_materiel(sb_dsp_t dsp)
    {
        var e = etat_materiel(dsp);
        if (!e.mpu)
            return;
        e.mpu = false;
        if (!ligne_tenue_materiel(dsp))
            Models.pic.picintc((uint16_t)(1 << dsp.sb_irqnum));
    }

    /// <summary>PB-154 : l'IRQ du MPU pendante, pour 82h.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static bool mpu_pendante_materiel(sb_dsp_t dsp) => etat_materiel(dsp).mpu;
}
