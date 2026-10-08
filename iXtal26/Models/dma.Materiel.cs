// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — le code propre au mode matériel du 8237 (G13 ; R10 de TRANSCRIPTION.md).
// STATUS: materiel
//
// Les corrections des défauts de PCem dans dma.c, appelées par les gardes de dma.cs (`if (materiel.pb_nn)`), seules
// sur leur ligne. La source, le cas qui discrimine et la panne qui le rougit sont dans l'entrée PB de PCEM_BUGS.md.

using iXtal26.Cpu;
using static iXtal26.Cpu.x86;

namespace iXtal26.Models;

internal static partial class dma
{
    // pcem bug, fixed in hardware mode: PB-257 — un canal masqué, ou dont le mode n'est pas celui du transfert, ne fait
    //   aucune requête au 8237A (fiche 8237A, p. 8, « Mask Register ») : pas de cycle de bus, rien à facturer. Le
    //   transfert accepté coûte son cycle, comme dans PCem ; la sonde compte les cycles qui ne sont plus facturés.
    private static void dma_cycle_materiel(int channel, int mode)
    {
        if (AT == 0)
        {
                if ((dma_m & (1 << channel)) == 0 && (dma_[channel].mode & 0xC) == mode)
                        _808x.refreshread();
                else
                        ModeMateriel.Sonde[257]++;
        }
    }
}
