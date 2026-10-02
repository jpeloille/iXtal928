// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/lpt/lpt.c
// STATUS: partial — lpt1_dat, lpt1_ctrl, lpt1_write, lpt1_read, SANS périphérique branché
//         (lpt1_device NULL : l_none, le défaut de PCem, lpt.c:41-55). G1.2 : amstrad.c les
//         appelle pour 378h-37Ah. Omis : lpt_init, lpt2_*, le registre lpt_devices (DAC,
//         DSS, Epson LX-810) — common_init n'appelle pas lpt_init, des deux côtés
//         (model.cs). L'oracle recopie les mêmes deux fonctions (harness_stubs.c).

// CS8981 : `lpt` n'a que des minuscules ASCII — le nom du fichier C, comme rom.cs.
#pragma warning disable CS8981

namespace iXtal26.Lpt;

internal static class lpt
{
    // pcem: lpt.c:63-64 — lpt2_dat et lpt2_ctrl omis.
    private static uint8_t lpt1_dat;
    private static uint8_t lpt1_ctrl;

    // pcem: lpt.c:66-79
    // omitted: lpt1_device->write_data / write_ctrl (:69-70, :74-75) — lpt1_device est NULL.
    internal static void lpt1_write(uint16_t port, uint8_t val, object? priv)
    {
        switch (port & 3)
        {
        case 0:
                lpt1_dat = val;
                break;
        case 2:
                lpt1_ctrl = val;
                break;
        }
    }

    // pcem: lpt.c:106-118
    // omitted: lpt1_device->read_status (:110-111) — lpt1_device est NULL : le `return 0`.
    internal static uint8_t lpt1_read(uint16_t port, object? priv)
    {
        switch (port & 3)
        {
        case 0:
                return lpt1_dat;
        case 1:
                return 0;
        case 2:
                return lpt1_ctrl;
        }
        return 0xff;
    }
}
