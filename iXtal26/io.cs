// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: tools/oracle/harness_stubs.c  (couche E/S)
// STATUS: partial — stub de M1, paire avec harness_stubs.c.
//         Remplacé à M2 par la transcription de pcem-dev/src/io.c (188 lignes,
//         sept tableaux plats [0x10000][2] de pointeurs de fonction).
//
// Bus ouvert : 0xFF en lecture, écritures avalées. C'est ce que voit un XT sans
// carte sur le port visé, et c'est ce que l'oracle doit rendre à l'identique.

using iXtal26.Diag;

namespace iXtal26;

internal static partial class io
{
    internal static uint8_t inb(uint16_t port)
    {
        Counters.n_inb++;
        return 0xFF;
    }

    internal static void outb(uint16_t port, uint8_t val) => Counters.n_outb++;
}
