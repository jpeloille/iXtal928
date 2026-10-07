// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — le code propre au mode matériel du 8088 et du 8086 (G13 ; R10 de TRANSCRIPTION.md).
// STATUS: materiel
//
// Les corrections des défauts de PCem dans 808x.c, appelées par les gardes de 808x.cs (`if (materiel.pb_nn)`), seules
// sur leur ligne. Chaque fonction ouvre sur son marqueur ; la source, le cas qui discrimine et la panne qui le rougit
// sont dans l'entrée PB de PCEM_BUGS.md. R2 ne s'applique pas : ce code n'a pas de C en regard. Il incrémente la sonde
// (ModeMateriel.Sonde), qui dit qu'il a servi.

using static iXtal26.Cpu._386_common;
using static iXtal26.Cpu.x86;

namespace iXtal26.Cpu;

internal static partial class _808x
{
    // pcem bug, fixed in hardware mode: PB-01 — AF est la retenue (l'emprunt) du bit 3, retenue entrante comprise : le
    //   bit 4 de a ^ b ^ résultat, pour ADC comme pour SBB.
    private static void af_materiel(uint32_t a, uint32_t b, uint32_t c)
    {
        ModeMateriel.Sonde[1]++;
        if (((a ^ b ^ c) & 0x10) != 0)
                cpu_state.flags |= A_FLAG;
    }
}
