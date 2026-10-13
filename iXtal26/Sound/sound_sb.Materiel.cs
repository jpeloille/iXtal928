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
}
