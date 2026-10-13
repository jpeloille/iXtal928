// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — le code propre au mode matériel de l'EMU8000 de l'AWE32 (G13 ; R10 de TRANSCRIPTION.md).
// STATUS: materiel
//
// Les corrections des défauts de PCem dans sound_emu8k.c, appelées par les gardes de sound_emu8k.cs
// (`if (materiel.pb_nn)`), seules sur leur ligne. La source, le cas qui discrimine et la panne qui le rougit sont dans
// l'entrée PB de PCEM_BUGS.md. La référence : le guide de l'EMU8000 (Dave Rossum, AWE32/EMU8000 Programmer's Guide,
// E-mu/Creative, 1994-1996), p. 11-14 (les adresses de la DRAM, WC, HWCF3) et p. 20 (l'initialisation). Les méthodes
// portent NoInlining (M2, G13.7c2).
//
// L'état que PCem n'a pas vit dans une classe à part (EtatEmu) : un champ de plus dans emu8k_t changerait sa
// disposition, et le code que le JIT tire des méthodes qui le lisent.

using System.Runtime.CompilerServices;

namespace iXtal26.Sound;

internal static partial class sound_emu8k
{
    private static class EtatEmu
    {
        /// <summary>PB-160 : l'écriture de DCYSUSV rejouée par le chemin de PCem ; la garde la laisse passer.</summary>
        internal static bool rejoue;
    }

    /// <summary>PB-158 : WC, CPF, CVCF et CCCA sont courants (guide, p. 8 à 11 et 14) : chaque lecture fait d'abord
    /// avancer la puce jusqu'à l'instant présent, comme PCem le fait à chaque écriture.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void lit_materiel(emu8k_t emu8k)
    {
        ModeMateriel.Sonde[158]++;
        emu8k_update(emu8k);
    }

    /// <summary>PB-159 : SMALR, SMARR, SMALW et SMARW relus en A22h — le bit 31, MT ou FULL (le drapeau de PCem, au bit 15
    /// du mot haut), et les bits 30 à 24 à zéro (guide, p. 11-12). Le drapeau s'efface à la lecture, comme chez PCem :
    /// sa durée sur la carte est inconnue.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static uint16_t dram_lu_materiel(uint32_t adresse, ref int drapeau)
    {
        ModeMateriel.Sonde[159]++;
        uint16_t ret = (uint16_t)(((adresse & EMU8K_MEM_ADDRESS_MASK) >> 16) | (uint32_t)(drapeau != 0 ? 0x8000 : 0));
        drapeau = 0;
        return ret;
    }

    /// <summary>PB-160 : HWCF3 n'est que ce que le programme y écrit (guide, p. 14 et 20 : son bit de sortie effacé au
    /// reset, 0004h écrit en fin d'initialisation, qui ouvre la sortie). Un moteur d'enveloppe allumé, HWCF3 autre que
    /// 0004h : l'écriture est rejouée par le chemin de PCem, puis HWCF3 rendu à sa valeur. Vrai : l'écriture est
    /// faite.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool dcysusv_materiel(uint16_t addr, uint16_t val, object p)
    {
        emu8k_t emu8k = (emu8k_t)p;
        if (EtatEmu.rejoue || DCYSUSV_GENERATOR_ENGINE_ON(val) == 0 ||
            emu8k.voice[emu8k.cur_voice].env_engine_on != 0 || emu8k.hwcf3 == 0x04)
            return false;
        ModeMateriel.Sonde[160]++;
        uint16_t hwcf3 = emu8k.hwcf3;
        EtatEmu.rejoue = true;
        emu8k_outw(addr, val, p);
        EtatEmu.rejoue = false;
        emu8k.hwcf3 = hwcf3;
        return true;
    }
}
