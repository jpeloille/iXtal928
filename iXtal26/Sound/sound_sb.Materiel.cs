// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — le code propre au mode matériel des mélangeurs des Sound Blaster (G13 ; R10 de TRANSCRIPTION.md).
// STATUS: materiel
//
// Les corrections des défauts de PCem dans sound_sb.c, appelées par les gardes de sound_sb.cs (`if (materiel.pb_nn)`),
// seules sur leur ligne. La source, le cas qui discrimine et la panne qui le rougit sont dans l'entrée PB de
// PCEM_BUGS.md. La référence : le guide de Creative (Sound Blaster Series Hardware Programming Guide, 1993), chapitre 2
// (80h à 82h) et chapitre 4 (les mélangeurs). Les méthodes portent NoInlining (M2, G13.7c2).
//
// Le reset d'un mélangeur pose les valeurs du guide, puis laisse PCem recalculer ses volumes : une écriture de la
// valeur de reset d'un registre que le reset pose aussi (0Ah, 0Eh ou 43h), par le chemin de PCem lui-même, qui
// recalcule tout à chaque écriture. L'index revient ensuite à 0, comme après le reset de PCem.

using System.Runtime.CompilerServices;
using static iXtal26.PluginApi.device;
using static iXtal26.Sound.sound_opl;
using static iXtal26.Sound.sound_sb_dsp;

namespace iXtal26.Sound;

internal static partial class sound_sb
{
    /// <summary>PB-145 : sb_2_init remet le CT1335 à zéro même sans mélangeur ; sans lui (`mixaddr` = 0), rien ne règle
    /// le volume CD, que sound_reset a laissé à 65 535, comme pour la SB 1.0 et la 1.5. Vrai : le reset est sauté.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool sans_melangeur_materiel()
    {
        if (device_get_config_int("mixaddr") != 0)
            return false;
        ModeMateriel.Sonde[145]++;
        return true;
    }

    /// <summary>PB-237 : le reset du CT1335 (00h écrit à l'index 0) — le général et la MIDI à 4 (−11 dB), le CD et la
    /// voix à 0 (−46 dB ; guide, p. 4-5). Vrai : l'écriture est faite.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool reset_ct1335_materiel(uint16_t addr, object p)
    {
        sb_ct1335_mixer_t mixer = ((sb_t)p).mixer_sb2;
        if ((addr & 1) == 0 || mixer.index != 0)
            return false;
        ModeMateriel.Sonde[237]++;
        mixer.regs[0x02] = 4 << 1;
        mixer.regs[0x06] = 4 << 1;
        mixer.regs[0x08] = 0 << 1;
        mixer.index = 0x0A;
        sb_ct1335_mixer_write(addr, 0 << 1, p);
        mixer.index = 0;
        return true;
    }

    /// <summary>PB-237 : le reset du CT1345 — la voix, le général et la MIDI à 4 (−11 dB) des deux côtés ; le reste,
    /// celui de PCem, est celui du guide (p. 4-8 et 4-9). Vrai : l'écriture est faite.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool reset_ct1345_materiel(uint16_t addr, object p)
    {
        sb_ct1345_mixer_t mixer = ((sb_t)p).mixer_sbpro;
        if ((addr & 1) == 0 || mixer.index != 0)
            return false;
        ModeMateriel.Sonde[237]++;
        mixer.regs[0x0A] = 0 << 1;
        mixer.regs[0x0C] = (0 << 5) | (0 << 3) | (0 << 1);
        mixer.regs[0x04] = (4 << 5) | (4 << 1);
        mixer.regs[0x22] = (4 << 5) | (4 << 1);
        mixer.regs[0x26] = (4 << 5) | (4 << 1);
        mixer.regs[0x28] = (0 << 5) | (0 << 1);
        mixer.regs[0x2E] = (0 << 5) | (0 << 1);
        // 0Eh par le chemin de PCem : à l'index 0Eh, il passe aussi la stéréo au DSP (sb_dsp_set_stereo), comme le reset.
        mixer.index = 0x0E;
        sb_ct1345_mixer_write(addr, (0 << 5) | (0 << 1), p);
        mixer.index = 0;
        return true;
    }

    /// <summary>Le reset du CT1745, sous PB-237 ou PB-153 (une seule fonction : les deux posent le même reset). PB-237 :
    /// le général, la voix et la MIDI à 24 (−14 dB ; guide, p. 4-15) ; PB-153 : 3Dh et 3Eh à 15h et 0Bh, la ligne, le CD
    /// et le micro, la MIDI ouverte (p. 4-16). Chaque part ne suit que sa correction ; sans elle, la valeur de PCem.
    /// Le reste est celui de PCem, et du guide. 80h et 81h n'en dépendent pas, comme chez PCem (le guide ne dit rien
    /// de leur reset). Vrai : l'écriture est faite.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool reset_ct1745_materiel(uint16_t addr, object p)
    {
        sb_ct1745_mixer_t mixer = ((sb_t)p).mixer_sb16;
        if ((addr & 1) == 0 || mixer.index != 0)
            return false;
        uint8_t volume = 31 << 3;
        if (materiel.pb_237)
        {
            ModeMateriel.Sonde[237]++;
            volume = 24 << 3;
        }
        for (var r = 0x30; r <= 0x35; r++)
            mixer.regs[r] = volume;
        for (var r = 0x36; r <= 0x3A; r++)
            mixer.regs[r] = 0 << 3;
        mixer.regs[0x3B] = 0 << 6;
        mixer.regs[0x3C] = OUTPUT_MIC | OUTPUT_CD_R | OUTPUT_CD_L | OUTPUT_LINE_R | OUTPUT_LINE_L;
        if (materiel.pb_153)
        {
            ModeMateriel.Sonde[153]++;
            mixer.regs[0x3D] = INPUT_MIC | INPUT_CD_L | INPUT_LINE_L;
            mixer.regs[0x3E] = INPUT_MIC | INPUT_CD_R | INPUT_LINE_R;
        }
        else
        {
            mixer.regs[0x3D] = INPUT_MIC | INPUT_CD_L | INPUT_LINE_L | INPUT_MIDI_L;
            mixer.regs[0x3E] = INPUT_MIC | INPUT_CD_R | INPUT_LINE_R | INPUT_MIDI_R;
        }
        mixer.regs[0x3F] = mixer.regs[0x40] = 0 << 6;
        mixer.regs[0x41] = mixer.regs[0x42] = 0 << 6;
        mixer.regs[0x44] = mixer.regs[0x45] = 8 << 4;
        mixer.regs[0x46] = mixer.regs[0x47] = 8 << 4;
        mixer.index = 0x43;
        sb_ct1745_mixer_write(addr, 0, p);
        mixer.index = 0;
        return true;
    }

    /// <summary>PB-153 : 81h écrit avec un seul canal 8 bits et aucun canal 16 bits — le 16 bits passe par le canal 8
    /// bits (guide, p. 2-7 : la traduction quand tous les bits 16 bits sont à 0) ; tout bit 16 bits l'arrête, et PCem
    /// pose le canal. Plusieurs canaux 8 bits sans canal 16 bits, ou 00h, restent ceux de PCem (inconnus), la
    /// traduction inchangée. Vrai : l'écriture est faite.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool dma_ct1745_materiel(uint16_t addr, uint8_t val, object p)
    {
        sb_t sb = (sb_t)p;
        if ((addr & 1) == 0 || sb.mixer_sb16.index != 0x81)
            return false;
        int seize = val & 0xE0, huit = val & 0x0B;
        if (seize != 0)
        {
            traduction_materiel(sb.dsp, false);
            return false;
        }
        if (huit == 0 || (huit & (huit - 1)) != 0)
            return false;
        ModeMateriel.Sonde[153]++;
        sb_dsp_setdma8(sb.dsp, huit == 1 ? 0 : huit == 2 ? 1 : 3);
        traduction_materiel(sb.dsp, true);
        sb.mixer_sb16.regs[0x81] = val;
        return true;
    }

    /// <summary>PB-154 : 82h — le bit 2, l'IRQ du MPU pendante (guide, p. 2-5) ; les bits 0 et 1, ceux de PCem.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static uint8_t irq_etat_materiel(sb_t sb)
    {
        ModeMateriel.Sonde[154]++;
        return (uint8_t)(((sb.dsp.sb_irq8 != 0) ? 1 : 0) | ((sb.dsp.sb_irq16 != 0) ? 2 : 0) |
                         (mpu_pendante_materiel(sb.dsp) ? 4 : 0));
    }

    // ===== G13.8c2 — la saturation du FIR =====

    /// <summary>PB-155 : la conversion saturante (NaN, que des entrées finies ne donnent pas, rend INT_MIN, comme chez
    /// PCem : seuls les dépassements changent).</summary>
    private static int32_t sature(double d) =>
        double.IsNaN(d) ? int32_t.MinValue :
        d >= 2147483647.0 ? int32_t.MaxValue : d <= -2147483648.0 ? int32_t.MinValue : (int32_t)d;

    // CS8600 et CS8602 : `(sb_t)p`, comme dans sound_sb.cs (son pragma, l. 25).
#pragma warning disable CS8600, CS8602
    /// <summary>PB-155 et PB-148 : sb_get_buffer_sb16 de PCem, à la lettre, sauf la conversion du FIR, qui sature
    /// sous PB-155 au lieu de rendre INT_MIN (le FIR est le rééchantillonneur de PCem : la carte ne claque pas), et
    /// l'enregistrement : la sélection d'entrée (selection_materiel) et, sous PB-148, l'anneau du CAN rempli
    /// (trame_materiel) à la place du bloc mort de sb_enable_i. Un tampon entier : les gardes sont en tête de la
    /// fonction, jamais par échantillon. Les commentaires sont ceux de la fonction de PCem ; les lignes longues,
    /// recoupées.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool tampon_sb16_materiel(int32_t[] buffer, int len, object? p)
    {
        sb_t sb = (sb_t)p;
        sb_ct1745_mixer_t mixer = sb.mixer_sb16;
        var s = selection_materiel(mixer);
        var an = enregistre_materiel(sb.dsp, out int freq, out long r0);
        int kprec = -1, dern_l = 0, dern_r = 0;
        int cd_vol_g = sb_att_2dbstep_5bits[mixer.regs[0x36] >> 3];
        int cd_vol_d = sb_att_2dbstep_5bits[mixer.regs[0x37] >> 3];

        int c;

        opl3_update2(sb.opl);
        sb_dsp_update(sb.dsp);
        for (c = 0; c < len * 2; c += 2) {
                int32_t out_l, out_r, in_l, in_r;

                out_l = ((((sb.opl.buffer[c] * mixer.fm_l) >> 16) * (sb.opl_emu != 0 ? 47000 : 51000)) >> 15);
                out_r = ((((sb.opl.buffer[c + 1] * mixer.fm_r) >> 16) * (sb.opl_emu != 0 ? 47000 : 51000)) >> 15);

                /*TODO: multi-recording mic with agc/+20db, cd and line in with channel inversion */
                in_l = s.gg * out_l + s.gd * out_r;
                in_r = s.dg * out_l + s.dd * out_r;

                out_l += (conversion_fir((double)(low_fir_sb16(0, (float)sb.dsp.buffer[c]) *
                                                    mixer.voice_l)) / 3) >> 15;
                out_r += (conversion_fir((double)(low_fir_sb16(1, (float)sb.dsp.buffer[c + 1]) *
                                                    mixer.voice_r)) / 3) >> 15;

                out_l = (out_l * mixer.master_l) >> 15;
                out_r = (out_r * mixer.master_r) >> 15;

                if (mixer.bass_l != 8 || mixer.bass_r != 8 || mixer.treble_l != 8 || mixer.treble_r != 8) {
                        if (mixer.bass_l > 8)
                                out_l += Cpu._386.CvtI32((double)(low_iir(0, (float)out_l) *
                                                                  sb_bass_treble_4bits[mixer.bass_l]));
                        if (mixer.bass_r > 8)
                                out_r += Cpu._386.CvtI32((double)(low_iir(1, (float)out_r) *
                                                                  sb_bass_treble_4bits[mixer.bass_r]));
                        if (mixer.treble_l > 8)
                                out_l += Cpu._386.CvtI32((double)(high_iir(0, (float)out_l) *
                                                                  sb_bass_treble_4bits[mixer.treble_l]));
                        if (mixer.treble_r > 8)
                                out_r += Cpu._386.CvtI32((double)(high_iir(1, (float)out_r) *
                                                                  sb_bass_treble_4bits[mixer.treble_r]));
                        if (mixer.bass_l < 8)
                                out_l = Cpu._386.CvtI32((double)((out_l)*sb_bass_treble_4bits[mixer.bass_l] +
                                                  low_cut_iir(0, (float)out_l) *
                                                          (1.0f - sb_bass_treble_4bits[mixer.bass_l])));
                        if (mixer.bass_r < 8)
                                out_r = Cpu._386.CvtI32((double)((out_r)*sb_bass_treble_4bits[mixer.bass_r] +
                                                  low_cut_iir(1, (float)out_r) *
                                                          (1.0f - sb_bass_treble_4bits[mixer.bass_r])));
                        if (mixer.treble_l < 8)
                                out_l = Cpu._386.CvtI32((double)((out_l)*sb_bass_treble_4bits[mixer.treble_l] +
                                                  high_cut_iir(0, (float)out_l) *
                                                          (1.0f - sb_bass_treble_4bits[mixer.treble_l])));
                        if (mixer.treble_r < 8)
                                out_r = Cpu._386.CvtI32((double)((out_r)*sb_bass_treble_4bits[mixer.treble_r] +
                                                  high_cut_iir(1, (float)out_r) *
                                                          (1.0f - sb_bass_treble_4bits[mixer.treble_r])));
                }
                if (an != null) {
                        long k = (r0 + (long)(c / 2) * freq) / 48000;
                        sound.cd_trame_materiel(len - 1 - c / 2, out int32_t cd_g, out int32_t cd_d);
                        cd_g = (cd_g * cd_vol_g) >> 15;
                        cd_d = (cd_d * cd_vol_d) >> 15;
                        dern_l = (in_l + s.cgg * cd_g + s.cgd * cd_d) << mixer.input_gain_L;
                        dern_r = (in_r + s.cdg * cd_g + s.cdd * cd_d) << mixer.input_gain_R;
                        while (kprec < k)
                                trame_materiel(an, ++kprec, dern_l, dern_r);
                }

                buffer[c] += (out_l << mixer.output_gain_L);
                buffer[c + 1] += (out_r << mixer.output_gain_R);
        }
        if (an != null) {
                // Au-delà de 48 000 Hz (41h et 42h jusqu'à 65 535 Hz chez PCem, plus que la carte), les trames finies
                //   que le dernier échantillon n'a pas atteintes.
                long finies = (r0 + (long)len * freq) / 48000;
                while (kprec + 1 < finies)
                        trame_materiel(an, ++kprec, dern_l, dern_r);
                sound_sb_dsp.fin_tampon_materiel(an, len, freq);
        }
        sb.dsp.record_pos_write += ((len * sb.dsp.sb_freq) / 48000) * 2;
        sb.dsp.record_pos_write &= 0xFFFF;

        sb.pos = 0;
        sb.opl.pos = 0;
        sb.dsp.pos = 0;
        return true;
    }

    /// <summary>PB-155 et PB-148 : sb_get_buffer_emu8k de PCem, à la lettre, sauf la conversion du FIR et
    /// l'enregistrement, comme tampon_sb16_materiel ; l'EMU8000 entre par la MIDI, sous son volume (PCem, comme la
    /// carte). Ses commentaires sont ceux de la fonction de PCem.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool tampon_emu8k_materiel(int32_t[] buffer, int len, object? p)
    {
        sb_t sb = (sb_t)p;
        sb_ct1745_mixer_t mixer = sb.mixer_sb16;
        emu8k_t emu = sb.emu8k!;
        var s = selection_materiel(mixer);
        var an = enregistre_materiel(sb.dsp, out int freq, out long r0);
        int kprec = -1, dern_l = 0, dern_r = 0;
        int cd_vol_g = sb_att_2dbstep_5bits[mixer.regs[0x36] >> 3];
        int cd_vol_d = sb_att_2dbstep_5bits[mixer.regs[0x37] >> 3];

        int c;

        opl3_update2(sb.opl);
        sound_emu8k.emu8k_update(emu);
        sb_dsp_update(sb.dsp);
        for (c = 0; c < len * 2; c += 2) {
                int32_t out_l, out_r, in_l, in_r;
                int c_emu8k = (((c / 2) * 44100) / 48000) * 2;

                out_l = ((((sb.opl.buffer[c] * mixer.fm_l) >> 15) * (sb.opl_emu != 0 ? 47000 : 51000)) >> 16);
                out_r = ((((sb.opl.buffer[c + 1] * mixer.fm_r) >> 15) * (sb.opl_emu != 0 ? 47000 : 51000)) >> 16);

                out_l += ((emu.buffer[c_emu8k] * mixer.fm_l) >> 15);
                out_r += ((emu.buffer[c_emu8k + 1] * mixer.fm_r) >> 15);

                /*TODO: multi-recording mic with agc/+20db, cd and line in with channel inversion  */
                in_l = s.gg * out_l + s.gd * out_r;
                in_r = s.dg * out_l + s.dd * out_r;

                out_l += (conversion_fir((double)(low_fir_sb16(0, (float)sb.dsp.buffer[c]) *
                                                    mixer.voice_l)) / 3) >> 15;
                out_r += (conversion_fir((double)(low_fir_sb16(1, (float)sb.dsp.buffer[c + 1]) *
                                                    mixer.voice_r)) / 3) >> 15;

                out_l = (out_l * mixer.master_l) >> 15;
                out_r = (out_r * mixer.master_r) >> 15;

                if (mixer.bass_l != 8 || mixer.bass_r != 8 || mixer.treble_l != 8 || mixer.treble_r != 8) {
                        if (mixer.bass_l > 8)
                                out_l += Cpu._386.CvtI32((double)(low_iir(0, (float)out_l) *
                                                                  sb_bass_treble_4bits[mixer.bass_l]));
                        if (mixer.bass_r > 8)
                                out_r += Cpu._386.CvtI32((double)(low_iir(1, (float)out_r) *
                                                                  sb_bass_treble_4bits[mixer.bass_r]));
                        if (mixer.treble_l > 8)
                                out_l += Cpu._386.CvtI32((double)(high_iir(0, (float)out_l) *
                                                                  sb_bass_treble_4bits[mixer.treble_l]));
                        if (mixer.treble_r > 8)
                                out_r += Cpu._386.CvtI32((double)(high_iir(1, (float)out_r) *
                                                                  sb_bass_treble_4bits[mixer.treble_r]));
                        if (mixer.bass_l < 8)
                                out_l = Cpu._386.CvtI32((double)(out_l * sb_bass_treble_4bits[mixer.bass_l] +
                                                  low_cut_iir(0, (float)out_l) *
                                                          (1.0f - sb_bass_treble_4bits[mixer.bass_l])));
                        if (mixer.bass_r < 8)
                                out_r = Cpu._386.CvtI32((double)(out_r * sb_bass_treble_4bits[mixer.bass_r] +
                                                  low_cut_iir(1, (float)out_r) *
                                                          (1.0f - sb_bass_treble_4bits[mixer.bass_r])));
                        if (mixer.treble_l < 8)
                                out_l = Cpu._386.CvtI32((double)(out_l * sb_bass_treble_4bits[mixer.treble_l] +
                                                  high_cut_iir(0, (float)out_l) *
                                                          (1.0f - sb_bass_treble_4bits[mixer.treble_l])));
                        if (mixer.treble_r < 8)
                                out_r = Cpu._386.CvtI32((double)(out_r * sb_bass_treble_4bits[mixer.treble_r] +
                                                  high_cut_iir(1, (float)out_r) *
                                                          (1.0f - sb_bass_treble_4bits[mixer.treble_r])));
                }
                if (an != null) {
                        long k = (r0 + (long)(c / 2) * freq) / 48000;
                        sound.cd_trame_materiel(len - 1 - c / 2, out int32_t cd_g, out int32_t cd_d);
                        cd_g = (cd_g * cd_vol_g) >> 15;
                        cd_d = (cd_d * cd_vol_d) >> 15;
                        dern_l = (in_l + s.cgg * cd_g + s.cgd * cd_d) << mixer.input_gain_L;
                        dern_r = (in_r + s.cdg * cd_g + s.cdd * cd_d) << mixer.input_gain_R;
                        while (kprec < k)
                                trame_materiel(an, ++kprec, dern_l, dern_r);
                }

                buffer[c] += (out_l << mixer.output_gain_L);
                buffer[c + 1] += (out_r << mixer.output_gain_R);
        }

        if (an != null) {
                // Au-delà de 48 000 Hz (41h et 42h jusqu'à 65 535 Hz chez PCem, plus que la carte), les trames finies
                //   que le dernier échantillon n'a pas atteintes.
                long finies = (r0 + (long)len * freq) / 48000;
                while (kprec + 1 < finies)
                        trame_materiel(an, ++kprec, dern_l, dern_r);
                sound_sb_dsp.fin_tampon_materiel(an, len, freq);
        }
        sb.dsp.record_pos_write += ((len * sb.dsp.sb_freq) / 48000) * 2;
        sb.dsp.record_pos_write &= 0xFFFF;
        sb.pos = 0;
        sb.opl.pos = 0;
        sb.dsp.pos = 0;
        emu.pos = 0;
        return true;
    }

    /// <summary>La conversion du FIR des copies : saturante sous PB-155, celle de PCem (cvttss2si, INT_MIN)
    /// sinon.</summary>
    private static int32_t conversion_fir(double d) => materiel.pb_155 ? sature(d) : Cpu._386.CvtI32(d);

    // ===== G13.8e1 — l'enregistrement : la MIDI du CT1745, l'anneau du CAN =====

    /// <summary>Les interrupteurs MIDI et CD des mélangeurs d'entrée (3Dh, 3Eh) : le gauche reçoit gg × MIDI.L + gd ×
    /// MIDI.R + cgg × CD.L + cgd × CD.R, le droit dg × MIDI.L + dd × MIDI.R + cdg × CD.L + cdd × CD.R.</summary>
    private readonly record struct Selection(int gg, int gd, int dg, int dd, int cgg, int cgd, int cdg, int cdd);

    /// <summary>La sélection d'entrée d'un tampon. Sous PB-153, le mélangeur d'entrée additionne les interrupteurs
    /// fermés (guide, fig. 4-5, p. 4-14) ; sinon, la lecture de PCem : MIDI.L s'il est fermé, sinon MIDI.R ; de même
    /// CD.L, sinon CD.R (la ligne du CD, en commentaire chez PCem, s'écrit comme celle de la MIDI).</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Selection selection_materiel(sb_ct1745_mixer_t mixer)
    {
        int Un(int sel, int bit) => (sel & bit) != 0 ? 1 : 0;
        int Sinon(int sel, int a, int b) => Un(sel, a) == 0 ? Un(sel, b) : 0;
        int gl = mixer.input_selector_left, dr = mixer.input_selector_right;
        if (materiel.pb_153)
                return new(Un(gl, INPUT_MIDI_L), Un(gl, INPUT_MIDI_R), Un(dr, INPUT_MIDI_L), Un(dr, INPUT_MIDI_R),
                           Un(gl, INPUT_CD_L), Un(gl, INPUT_CD_R), Un(dr, INPUT_CD_L), Un(dr, INPUT_CD_R));
        return new(Un(gl, INPUT_MIDI_L), Sinon(gl, INPUT_MIDI_L, INPUT_MIDI_R),
                   Un(dr, INPUT_MIDI_L), Sinon(dr, INPUT_MIDI_L, INPUT_MIDI_R),
                   Un(gl, INPUT_CD_L), Sinon(gl, INPUT_CD_L, INPUT_CD_R),
                   Un(dr, INPUT_CD_L), Sinon(dr, INPUT_CD_L, INPUT_CD_R));
    }

    /// <summary>Les copies de la SB 16 et de l'AWE32 : la sonde de PB-155, puis anneau_tampon_materiel.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static sound_sb_dsp.AnneauEntree? enregistre_materiel(sb_dsp_t dsp, out int freq, out long r0)
    {
        if (materiel.pb_155)
                ModeMateriel.Sonde[155]++;
        return anneau_tampon_materiel(dsp, out freq, out r0);
    }

    /// <summary>PB-148 : l'anneau du CAN à remplir par ce tampon (null sans PB-148), la fréquence de l'entrée et le
    /// reste de la tête d'écriture ; la part commune aux trois cartes qui enregistrent.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static sound_sb_dsp.AnneauEntree? anneau_tampon_materiel(sb_dsp_t dsp, out int freq, out long r0)
    {
        freq = Math.Max(dsp.sb_freq, 0);
        r0 = 0;
        if (!materiel.pb_148)
                return null;
        ModeMateriel.Sonde[148]++;
        var an = sound_sb_dsp.anneau_materiel(dsp);
        r0 = an.reste;
        return an;
    }

    /// <summary>PB-148 : la trame <paramref name="k"/> du tampon (comptée de la tête d'écriture), le gain d'entrée déjà
    /// appliqué (3Fh, 40h : 0 à 18 dB) ; le CAN sature, comme PCem. Chaque trame prend le premier échantillon de 48 kHz
    /// à son instant ou après (PCem écrivait à chaque échantillon : le dernier gagnait).</summary>
    private static void trame_materiel(sound_sb_dsp.AnneauEntree an, long k, int32_t l, int32_t r)
    {
        int i = (int)((an.ecrit + 2 * k) & 0xFFFF);
        an.e[i] = (int16_t)Math.Clamp(l, -32768, 32767);
        an.e[i + 1] = (int16_t)Math.Clamp(r, -32768, 32767);
    }

    /// <summary>PB-153 : 0Ah écrit — 3Ah à l'échelle du guide (p. 4-15 et 4-16) : 0Ah, 3 bits, de −42 à 0 dB par 6 dB ;
    /// 3Ah, 5 bits en D7:D3, de −62 à 0 dB par 2 dB : ((0Ah & 7) × 3 + 10) << 3. Vrai.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool micro_ecrit_materiel(sb_ct1745_mixer_t mixer)
    {
        ModeMateriel.Sonde[153]++;
        mixer.regs[0x3A] = (uint8_t)((((mixer.regs[0x0A] & 7) * 3) + 10) << 3);
        return true;
    }

    /// <summary>PB-153 : 0Ah relu — le pas de 6 dB que 3Ah atteint, ((3Ah >> 3) − 10) / 3, borné à 0..7 (déduit pour
    /// 3Ah sous 50h, plus bas que −42 dB, que 0Ah ne peut dire).</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static uint8_t micro_lu_materiel(sb_ct1745_mixer_t mixer)
    {
        ModeMateriel.Sonde[153]++;
        return (uint8_t)Math.Clamp(((mixer.regs[0x3A] >> 3) - 10) / 3, 0, 7);
    }

    // ===== G13.8e2 — l'enregistrement de la SB Pro =====

    /// <summary>PB-148 : sb_get_buffer_sbpro de PCem, à la lettre, sauf l'enregistrement : le CT1345 mène au CAN la
    /// source que choisit 0Ch (guide, p. 4-8) ; seul le CD a une entrée côté hôte (sound.cd_trame_materiel), sans le
    /// volume du CD (le guide ne dit que celui du micro sans effet sur l'enregistrement ; les volumes du CT1345 sont
    /// ceux de la sortie, déduit) ; le micro et la ligne, muets ; le passe-bas d'entrée, non modélisé. L'anneau du CAN,
    /// rempli comme par tampon_sb16_materiel. Ses commentaires sont ceux de la fonction de PCem.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool tampon_sbpro_materiel(int32_t[] buffer, int len, object? p)
    {
        sb_t sb = (sb_t)p;
        sb_ct1345_mixer_t mixer = sb.mixer_sbpro;
        var an = anneau_tampon_materiel(sb.dsp, out int freq, out long r0);
        bool cd = (mixer.input_selector & INPUT_CD_L) != 0;
        int kprec = -1, dern_l = 0, dern_r = 0;

        int c;

        if (sb.dsp.sb_type == SBPRO)
                opl2_update2(sb.opl);
        else
                opl3_update2(sb.opl);

        sb_dsp_update(sb.dsp);
        for (c = 0; c < len * 2; c += 2) {
                int32_t out_l, out_r;

                out_l = ((((sb.opl.buffer[c] * mixer.fm_l) >> 16) * (sb.opl_emu != 0 ? 47000 : 51000)) >> 15);
                out_r = ((((sb.opl.buffer[c + 1] * mixer.fm_r) >> 16) * (sb.opl_emu != 0 ? 47000 : 51000)) >> 15);

                /*TODO: Implement the stereo switch on the mixer instead of on the dsp? */
                if (mixer.output_filter != 0) {
                        out_l += (int32_t)(((sb_iir(0, (float)sb.dsp.buffer[c]) / 1.3) * mixer.voice_l) / 3) >> 15;
                        out_r += (int32_t)(((sb_iir(1, (float)sb.dsp.buffer[c + 1]) / 1.3) * mixer.voice_r) / 3) >> 15;
                } else {
                        out_l += ((int32_t)(sb.dsp.buffer[c] * mixer.voice_l) / 3) >> 15;
                        out_r += ((int32_t)(sb.dsp.buffer[c + 1] * mixer.voice_r) / 3) >> 15;
                }
                if (an != null) {
                        long k = (r0 + (long)(c / 2) * freq) / 48000;
                        dern_l = dern_r = 0;
                        if (cd)
                                sound.cd_trame_materiel(len - 1 - c / 2, out dern_l, out dern_r);
                        while (kprec < k)
                                trame_materiel(an, ++kprec, dern_l, dern_r);
                }
                // TODO: recording CD, Mic with AGC or line in. Note: mic volume does not affect recording.

                out_l = (out_l * mixer.master_l) >> 15;
                out_r = (out_r * mixer.master_r) >> 15;

                buffer[c] += out_l;
                buffer[c + 1] += out_r;
        }
        if (an != null) {
                long finies = (r0 + (long)len * freq) / 48000;
                while (kprec + 1 < finies)
                        trame_materiel(an, ++kprec, dern_l, dern_r);
                sound_sb_dsp.fin_tampon_materiel(an, len, freq);
        }

        sb.pos = 0;
        sb.opl.pos = 0;
        sb.dsp.pos = 0;
        return true;
    }
}
