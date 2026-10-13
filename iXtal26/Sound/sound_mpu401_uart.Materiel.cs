// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — le code propre au mode matériel du MPU-401 de la SB 16 et de l'AWE32 (G13 ; R10 de TRANSCRIPTION.md).
// STATUS: materiel
//
// Les corrections des défauts de PCem dans sound_mpu401_uart.c, appelées par les gardes de sound_mpu401_uart.cs
// (`if (materiel.pb_nn)`), seules sur leur ligne. La source, le cas qui discrimine et la panne qui le rougit sont dans
// l'entrée PB de PCEM_BUGS.md. La référence : le guide de Creative (Sound Blaster Series Hardware Programming Guide,
// 1993), chapitre 2 (l'IRQ partagée, p. 2-5) et chapitre 5 (le MPU-401, p. 5-5 à 5-10). Les méthodes portent NoInlining
// (M2, G13.7c2).
//
// L'IRQ du MPU est celle de la carte : son état vit avec celui du DSP (sound_sb_dsp.Materiel.cs), qui tient la ligne
// partagée. La carte du MPU est sb_pri, la seule Sound Blaster de la machine ; un autre MPU suit PCem.

using System.Runtime.CompilerServices;

namespace iXtal26.Sound;

internal static partial class sound_mpu401_uart
{
    /// <summary>Le DSP de la carte du MPU (la SB 16 ou l'AWE32), ou null pour un MPU qui n'est pas celui de sb_pri.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static sb_dsp_t? dsp_materiel(mpu401_uart_t mpu) =>
        sound_sb.sb_pri is { } sb && sb.mpu == mpu ? sb.dsp : null;

    /// <summary>PB-154 : les commandes du MPU. 3Fh hors du mode UART : l'ACK et l'IRQ (guide, p. 5-9) ; en mode UART,
    /// ignorée (déduit de p. 5-5 : seul le reset y est reconnu). FFh, dans les deux modes : l'ACK de PCem (en mode UART,
    /// inconnu) et l'IRQ que lève l'octet rendu (déduit : chez Roland, la donnée prête mène l'interruption ; en mode
    /// UART, elle suit l'ACK reproduit). Les autres commandes, ignorées, et les données restent à PCem. Vrai :
    /// l'écriture est faite.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool commande_materiel(uint16_t addr, uint8_t val, object p)
    {
        mpu401_uart_t mpu = (mpu401_uart_t)p;
        if ((addr & 1) == 0 || (val != 0xff && val != 0x3f))
            return false;
        sb_dsp_t? dsp = dsp_materiel(mpu);
        if (dsp == null)
            return false;
        ModeMateriel.Sonde[154]++;
        if (val == 0x3f && mpu.uart_mode != 0)
            return true;
        mpu.rx_data = 0xfe;
        mpu.uart_mode = val == 0x3f ? 1 : 0;
        mpu.status = 0;
        sound_sb_dsp.mpu_leve_materiel(dsp);
        return true;
    }

    /// <summary>PB-154 : la lecture de la donnée (330h) efface l'IRQ du MPU (guide, p. 5-9 et 2-5) ; PCem rend ensuite
    /// l'octet.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void acquitte_materiel(uint16_t addr, object p)
    {
        if ((addr & 1) != 0)
            return;
        sb_dsp_t? dsp = dsp_materiel((mpu401_uart_t)p);
        if (dsp == null)
            return;
        ModeMateriel.Sonde[154]++;
        sound_sb_dsp.mpu_acquitte_materiel(dsp);
    }
}
