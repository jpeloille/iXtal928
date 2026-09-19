// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/ppi.c  (struct PPI : includes/private/ibm.h:136-141)
// STATUS: transcribed — les 19 lignes de ppi.c en entier.

/*IBM 5150 cassette nonsense
  Calls F979 twice
  Expects CX to be nonzero, BX >$410 and <$540
    CX is loops between bit 4 of $62 changing
    BX is timer difference between calls
  */

namespace iXtal26;

// pcem: ibm.h:136-139
internal sealed class PPI
{
    internal int s2;
    internal uint8_t pa, pb;
}

internal static partial class ppi_c
{
    // pcem: ppi.c:14
    internal static readonly PPI ppi = new();

    // pcem: ppi.c:16-19
    internal static void ppi_reset()
    {
        ppi.pa = 0x0; // 0x1D;
        ppi.pb = 0x40;
    }
}
