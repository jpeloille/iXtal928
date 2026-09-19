// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: tools/oracle/harness_stubs.c  (PIC pic, pic2 ; picint ; picinterrupt)
// STATUS: partial — stub de M1, paire avec harness_stubs.c.
//         Remplacé à M3 par la transcription de pcem-dev/src/models/pic.c.
//
// harness_stubs.c déclare `PIC pic, pic2;` sans les initialiser autrement qu'à
// zéro. Donc pend = mask = 0, et le `takeint` de fin de boucle d'execx86
// (808x.c:3985) reste faux : aucune interruption externe n'est livrée en M1.
// C'est voulu — les interruptions sont un jalon à part (M3), et le compteur
// n_picinterrupt prouvera qu'on n'a pas seulement « été d'accord sur rien ».

using iXtal26.Diag;

namespace iXtal26.Models;

internal sealed class PIC
{
    internal uint8_t icw1, icw2, icw3, icw4, mask, ins, pend, mask2;
    internal int icw, read;
    internal uint8_t vector;
}

internal static partial class pic
{
    internal static readonly PIC pic_ = new(), pic2 = new();

    internal static void picint(uint16_t num) => Counters.n_picint++;

    internal static uint8_t picinterrupt()
    {
        Counters.n_picinterrupt++;
        return 0xFF;
    }
}
