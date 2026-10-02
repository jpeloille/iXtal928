// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/models/olivetti_m24.c + includes/private/models/olivetti_m24.h
// STATUS: transcribed — les 15 lignes de olivetti_m24.c en entier.

namespace iXtal26.Models;

internal static partial class olivetti_m24
{
    // pcem: olivetti_m24.c:5-13
    internal static uint8_t olivetti_m24_read(uint16_t port, object priv)
    {
        switch (port)
        {
        case 0x66:
                return 0x00;
        case 0x67:
                return 0x20 | 0x40 | 0x0C;
        }
        return 0xff;
    }

    // pcem: olivetti_m24.c:15
    internal static void olivetti_m24_init() { io.io_sethandler(0x0066, 0x0002, olivetti_m24_read, null, null, null, null, null, null); }
}
