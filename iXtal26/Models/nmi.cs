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
    internal static void nmi_write(uint16_t port, uint8_t val, object p)
    {
        nmi_mask = val & 0x80;
        // pcem bug, fixed in hardware mode: PB-69 — rien ne lève la NMI d'un PC ou d'un XT en mode PCem. PCem n'a qu'un
        //   nmi_mask, que le port A0h écrit et que le 808x lit ; la transcription en a deux, celui-ci et celui de
        //   _808x, que le 808x lit et que seul le port 70h de l'AT écrit. Sans effet tant que rien ne pose `nmi` ; en
        //   mode matériel, où le 8087 la pose, A0h tient aussi celui que le 808x lit.
        if (materiel.pb_69)
                Cpu._808x.nmi_mask = nmi_mask;
    }

    // pcem: nmi.c:9-12
    internal static void nmi_init()
    {
        io.io_sethandler(0x00a0, 0x0001, null, null, null, nmi_write, null, null, null);
        nmi_mask = 0;
        // pcem bug, fixed in hardware mode: PB-69 — le masque que lit le 808x, remis avec celui-ci (nmi_write).
        if (materiel.pb_69)
                Cpu._808x.nmi_mask = 0;
    }
}
