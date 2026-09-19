// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/models/nmi.c
// STATUS: transcribed — les 12 lignes de nmi.c en entier.

namespace iXtal26.Models;

internal static partial class nmi
{
    // pcem: nmi.c:5
    internal static int nmi_mask;

    // pcem: nmi.c:7
    internal static void nmi_write(uint16_t port, uint8_t val, object p) { nmi_mask = val & 0x80; }

    // pcem: nmi.c:9-12
    internal static void nmi_init()
    {
        io.io_sethandler(0x00a0, 0x0001, null, null, null, nmi_write, null, null, null);
        nmi_mask = 0;
    }
}
